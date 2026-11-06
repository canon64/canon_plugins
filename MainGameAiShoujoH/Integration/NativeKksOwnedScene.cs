using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using ActionGame;
using ActionGame.H;
using CanonGate;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using H;
using MainGameAiShoujoH.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameScene = Manager.Scene;

namespace MainGameAiShoujoH.Integration
{
    // Owns the real native scenes. Update/LateUpdate remain HSceneProc's native
    // implementation, including Hand/HitCollision/Yure/IK/voice/SE/items.
    // No normal HScene orchestration, character creation, KKS result save, or
    // global audio stop is allowed to escape this exact instance boundary.
    internal sealed partial class NativeKksOwnedScene : IMotionModule, IAsyncSessionPreparation, IAsyncSessionCleanup, IMotionSelectionTransition, IKksEndingMotionModule
    {
        static NativeKksOwnedScene active;
        readonly NativeKksSessionRuntime runtime;
        readonly ulong activityGeneration;
        readonly Action<EndReason> requestEnd;
        readonly float startupTimeout;
        readonly List<IDisposable> bodyRestore = new List<IDisposable>();
        readonly List<Motion.NativeKksEndingPlayback> ending = new List<Motion.NativeKksEndingPlayback>();
        readonly List<Action> globalRestore = new List<Action>();
        readonly HashSet<AudioSource> previousActorAudio = new HashSet<AudioSource>();
        GateApi.DedicatedNativeEngineLease gate;
        SessionContext context;
        Harmony patches;
        HSceneProc proc;
        ActionMap map;
        NativeKksActorBinding actors;
        NativeKksCameraLease camera;
        CanvasGroup spriteInput;
        bool spriteInteractable;
        SaveData.Heroine[] heroines;
        SaveData.Player player;
        Action ready;
        Action<Exception> failed;
        Action cleaned;
        Action<Exception> cleanupFailed;
        readonly List<Exception> cleanupErrors = new List<Exception>();
        bool loadingOuter, loadingProc, prepared, disposed, endingStarted, nativeDestroyed;
        bool nativeCall;
        bool bodyReleased;
        bool resourcesRestored;
        int outerHandle, procHandle;
        Coroutine deadline;
        public string Name => "owned-native-HProc";
        public string SelectedMotionId => context?.Request.MotionId;
        public string SelectedState => endingStarted ? ending.FirstOrDefault()?.State : proc?.flags?.nowAnimStateName;
        internal HSceneProc Proc => proc;
        internal bool IsPrepared => prepared && !disposed && proc != null;
        internal string[] AvailableMotions => context == null ? Array.Empty<string>() : new[] { context.Request.MotionId };
        bool Current => !disposed && context != null && context.IsCurrent() && gate?.IsValid == true;
        internal NativeKksOwnedScene(NativeKksSessionRuntime runtime, ulong activityGeneration, Action<EndReason> requestEnd, string policyPath)
        {
            this.runtime = runtime; this.activityGeneration = activityGeneration; this.requestEnd = requestEnd;
            var policy = Motion.DedicatedMotionModule.ReadCatalog<EnginePolicy>(policyPath);
            if (policy == null || policy.version != 1 || !(policy.startupTimeoutSeconds > default(float)) || float.IsNaN(policy.startupTimeoutSeconds) || float.IsInfinity(policy.startupTimeoutSeconds))
                throw new InvalidOperationException("Native engine operational policy is invalid.");
            startupTimeout = policy.startupTimeoutSeconds;
        }
        public void Prepare(SessionContext value) => throw new InvalidOperationException("Native engine preparation requires the asynchronous loading boundary.");
        public void PrepareAsync(SessionContext value, Action completed, Action<Exception> failure)
        {
            if (active != null || context != null || disposed) throw new InvalidOperationException("A native engine is already preparing.");
            context = value; ready = completed; failed = failure;
            if (SceneManager.GetSceneByName("H").isLoaded || SceneManager.GetSceneByName("HProc").isLoaded)
                throw new InvalidOperationException("Another native H scene is already loaded.");
            Presentation.KksNativePoseDefinition.ValidateIdentity(value.Request.KksPose);
            var female = value.Request.Participants.Where(p => p.Role != "male").ToArray();
            var male = value.Request.Participants.Single(p => p.Role == "male");
            if (female.Length != 1 || !male.IsPlayer) throw new NotSupportedException("Owned native bootstrap currently requires one female and the native male player role.");
            gate = GateApi.AcquireDedicatedNativeEngine(Plugin.PluginGuid, value.Request.Owner, female[0].Character, activityGeneration);
            active = this;
            try
            {
                heroines = female.Select(p => new SaveData.Heroine(p.Character.chaFile, false)).ToArray();
                player = new SaveData.Player(male.Character.chaFile, false);
                foreach (var participant in value.Request.Participants)
                {
                    bodyRestore.Add(new NativeKksActorBodyLease(participant.Character));
                    foreach (var audio in participant.Character.GetComponentsInChildren<AudioSource>(true)) previousActorAudio.Add(audio);
                }
                foreach (var participant in female) ending.Add(new Motion.NativeKksEndingPlayback(participant.Character));
                var listener = Manager.Sound.Listener; var correctHand = Manager.Character.enableCorrectHandSize;
                globalRestore.Add(() => Manager.Sound.Listener = listener);
                globalRestore.Add(() => Manager.Character.enableCorrectHandSize = correctHand);
                var previousTables = HFlag.lstABName;
                var nativeTables = CommonLib.GetAssetBundleNameListFromPath("h/list/"); nativeTables.Sort();
                HFlag.lstABName = nativeTables;
                globalRestore.Add(() => HFlag.lstABName = previousTables);
                InstallPatches(); loadingOuter = true;
                deadline = context.Request.Owner.StartCoroutine(StartupDeadline());
                GameScene.LoadReserveAsyncForget(new GameScene.Data { levelName = "H", isAdd = true, isFade = false, onLoad = OuterLoaded }, false);
            }
            catch (Exception error) { Fail(error); }
        }
        void OuterLoaded()
        {
            try
            {
                outerHandle = SceneManager.GetSceneByName("H").handle; loadingOuter = false;
                if (disposed) { StartUnload(); return; }
                if (!Current) { Fail(new OperationCanceledException("Native outer scene load lost ownership.")); return; }
                DisableForeignOutputs(OwnedScene(outerHandle));
                loadingProc = true;
                GameScene.LoadReserveAsyncForget(new GameScene.Data { levelName = "HProc", isAdd = true, isFade = false, onLoad = ProcLoaded }, false);
            }
            catch (Exception error) { Fail(error); }
        }
        void ProcLoaded()
        {
            try
            {
                procHandle = SceneManager.GetSceneByName("HProc").handle; loadingProc = false;
                proc = GameScene.GetRootComponent<HSceneProc>("HProc");
                if (disposed) { if (proc != null && gate.Scene == null) gate.BindCleanupScene(proc); StartUnload(); return; }
                if (proc == null || !Current) throw new InvalidOperationException("Owned native HProc did not load under its original permission.");
                BindProc(proc);
                spriteInput = proc.sprite.GetComponent<CanvasGroup>() ?? proc.sprite.gameObject.AddComponent<CanvasGroup>();
                spriteInteractable = spriteInput.interactable; spriteInput.interactable = false;
                proc.dataH = new OpenHData.Data {
                    lstFemale = heroines.ToList(), player = player, isFreeH = true,
                    map = context.Request.Island, position = context.Request.Position, rotation = context.Request.Rotation,
                    peepCategory = runtime.SelectedAnimation.lstCategory.Select(c => c.category).ToList(),
                    mapNoFreeH = default(int), timezoneFreeH = -1, statusFreeH = -1, stageFreeH1 = -1, stageFreeH2 = -1,
                    state = -1, appoint = -1, maleCoordinateType = context.Request.Participants.Single(p => p.Role == "male").Character.fileStatus.coordinateType,
                    lstFemaleCoordinateType = context.Request.Participants.Where(p => p.Role != "male").Select(p => p.Character.fileStatus.coordinateType).ToList()
                };
                foreach (var participant in context.Request.Participants.Where(p => p.Role != "male"))
                { proc.dataH.clothStates.Add(participant.Character.fileStatus.clothesState.Select(b => (int)b).ToList()); proc.dataH.accessoryStates.Add(participant.Character.fileStatus.showAccessory.ToList()); }
                camera = new NativeKksCameraLease(context.Request.Camera, proc.flags.ctrlCamera, () => context.CanInteract());
                proc.flags.ctrlCamera = camera.Driver; proc.trsMainCam = context.Request.Camera.transform;
                DisableForeignOutputs(OwnedScene(procHandle));
            }
            catch (Exception error) { Fail(error); }
        }
        void BindProc(HSceneProc value)
        {
            if (proc != null && proc != value) throw new InvalidOperationException("A different HProc tried to enter the owned loading boundary.");
            proc = value;
            if (gate.Scene == null) gate.Bind(value);
        }
        void DisableForeignOutputs(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var output in root.GetComponentsInChildren<Camera>(true)) if (output != context.Request.Camera) output.enabled = false;
                foreach (var listener in root.GetComponentsInChildren<AudioListener>(true))
                    if (listener.transform != context.Request.Camera.transform && !listener.transform.IsChildOf(context.Request.Camera.transform)) listener.enabled = false;
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                    if (camera != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) {
                        canvas.worldCamera = context.Request.Camera;
                        context.Request.Camera.cullingMask |= 1 << canvas.gameObject.layer;
                    }
            }
        }
        static UnityEngine.SceneManagement.Scene OwnedScene(int handle)
        {
            for (int i = default(int); i < SceneManager.sceneCount; ++i)
            { var scene = SceneManager.GetSceneAt(i); if (scene.handle == handle) return scene; }
            return default(UnityEngine.SceneManagement.Scene);
        }
        bool Owns(HSceneProc value) => ReferenceEquals(active, this) && value != null && ReferenceEquals(proc, value);
        bool OwnsPending(Component component, string scene) => ReferenceEquals(active, this) && component != null
            && component.gameObject.scene.name == scene && (scene == "H" ? loadingOuter || component.gameObject.scene.handle == outerHandle : loadingProc || component.gameObject.scene.handle == procHandle);
        void InstallPatches()
        {
            patches = new Harmony("canon.MainGameAiShoujoH.NativeEngine." + context.Id.ToString("N"));
            Patch(typeof(HScene), "Start", nameof(BeforeOuterStart));
            Patch(typeof(HSceneProc), "Start", nameof(BeforeStart), nameof(AfterStart));
            Patch(typeof(HSceneProc), "CreateCharacter", nameof(BeforeCreateCharacter));
            Patch(typeof(HSceneProc), "LoadAnimationListInfo", nameof(BeforeInitialSelection));
            Patch(typeof(HSceneProc), "ChangeAnimator", nameof(BeforeChangeAnimator), null, null, nameof(AfterChangeAnimator));
            Patch(typeof(HSceneProc), "CreateListAnimationFileName", null, nameof(AfterNativeCatalog));
            Patch(typeof(HSceneProc), "LoadMapDependent", nameof(BeforeMapDependent));
            Patch(typeof(HSceneProc), "GotoPointMoveScene", nameof(BeforePointMove));
            Patch(typeof(HSceneProc), "GetCloseCategory", nameof(BeforeCloseCategories));
            Patch(typeof(HSceneProc), "ReturnVisibleForHPointMove", nameof(BeforeReturnPointMove));
            Patch(typeof(HSceneProc), "EndProc", nameof(BeforeNativeEnd));
            Patch(typeof(HSceneProc), "NewHeroineEndProc", nameof(BeforeNativeEnd));
            Patch(typeof(HSceneProc), "OnDestroy", nameof(BeforeNativeDestroy));
            Patch(typeof(ActionMap), "Awake", null, nameof(AfterMapAwake));
            Patch(typeof(ActionMap), "Start", nameof(BeforeMapStart));
            Patch(typeof(BaseMap), "Change", nameof(BeforeMapChange), null, new[] { typeof(int), typeof(FadeCanvas.Fade), typeof(bool) });
            Patch(typeof(GlobalMethod), "HGlobalSaveData", nameof(BeforeGlobalSave));
            Patch(typeof(Manager.Sound), "Stop", nameof(BeforeGlobalSoundStop), null, new[] { typeof(Manager.Sound.Type) });
            Patch(typeof(Illusion.Game.Utils.Sound), "Play", nameof(BeforeNativeBgm), null, new[] { typeof(Illusion.Game.Utils.Sound.SettingBGM) });
            Patch(typeof(FadeCanvas), "StartFadeAysnc", nameof(BeforeNativeFade), null, new[] { typeof(FadeCanvas.Fade), typeof(bool) });
            Patch(typeof(HVoiceCtrl), "Init", nameof(BeforeNativeVoice), null, new[] { typeof(string), typeof(string) });
            Patch(typeof(FaceListCtrl), "LoadText", nameof(BeforeNativeFace), null, new[] { typeof(string) });
            Patch(typeof(Tutorial), "Open", nameof(BeforeNativeTutorial));
        }
        void Patch(Type type, string method, string prefix, string postfix = null, Type[] args = null, string finalizer = null)
        {
            var original = args == null ? AccessTools.Method(type, method) : AccessTools.Method(type, method, args);
            if (original == null) throw new MissingMethodException(type.FullName, method);
            patches.Patch(original, prefix == null ? null : new HarmonyMethod(typeof(NativeKksOwnedScene), prefix) { priority = Priority.First },
                postfix == null ? null : new HarmonyMethod(typeof(NativeKksOwnedScene), postfix), null,
                finalizer == null ? null : new HarmonyMethod(typeof(NativeKksOwnedScene), finalizer), null);
        }
        static IEnumerator Empty() { yield break; }
        static bool BeforeOuterStart(HScene __instance, ref IEnumerator __result)
        { if (active?.OwnsPending(__instance, "H") != true) return true; __result = Empty(); return false; }
        static bool BeforeStart(HSceneProc __instance, ref IEnumerator __result)
        {
            var owner = active; if (owner?.OwnsPending(__instance, "HProc") != true) return true;
            if (owner.disposed) { owner.proc = __instance; if (owner.gate.Scene == null) owner.gate.BindCleanupScene(__instance); __result = Empty(); return false; }
            owner.BindProc(__instance); return true;
        }
        static void AfterStart(HSceneProc __instance, ref IEnumerator __result)
        { if (active?.Owns(__instance) == true) __result = active.GuardStart(__result); }
        IEnumerator GuardStart(IEnumerator original)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(original);
            while (!disposed && stack.Count != default(int))
            {
                object current = null; bool moved = false;
                try { if (!Current) throw new OperationCanceledException("Native Start permission expired."); nativeCall = true; moved = stack.Peek().MoveNext(); if (moved) current = stack.Peek().Current; }
                catch (Exception error) { Fail(error); yield break; }
                finally { nativeCall = false; }
                if (!moved) { stack.Pop(); continue; }
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return current; // Only original native startup yields.
            }
            if (disposed) yield break;
            try
            {
                proc.enabled = false; camera.Suspend(); actors.Validate();
                DisableForeignOutputs(OwnedScene(outerHandle)); DisableForeignOutputs(OwnedScene(procHandle));
                runtime.AdoptEngine(this, proc.flags, proc.hand, heroines[0]);
                prepared = true; CancelDeadline(); var completion = ready; ready = null; failed = null; completion?.Invoke();
            }
            catch (Exception error) { Fail(error); }
        }
        static bool BeforeCreateCharacter(HSceneProc __instance, ref IEnumerator __result)
        {
            var owner = active; if (owner?.Owns(__instance) != true) return true;
            try
            {
                var females = owner.context.Request.Participants.Where(p => p.Role != "male").Select(p => p.Character).ToArray();
                var male = owner.context.Request.Participants.Single(p => p.Role == "male").Character;
                owner.actors = new NativeKksActorBinding(__instance, __instance.flags, () => owner.Owns(__instance), females, owner.heroines, male, owner.player);
                __result = Empty(); return false;
            }
            catch (Exception error) { owner.Fail(error); throw; }
        }
        static bool BeforeInitialSelection(HSceneProc __instance, ref HSceneProc.AnimationListInfo __result)
        { if (active?.Owns(__instance) != true || active.prepared) return true; __result = active.runtime.SelectedAnimation; return false; }
        static bool BeforeChangeAnimator(HSceneProc __instance, HSceneProc.AnimationListInfo _nextAinmInfo, ref bool __state)
        {
            if (active?.Owns(__instance) != true) return true;
            __state = active.nativeCall; active.nativeCall = true;
            var identity = active.context.Request.KksPose;
            if (_nextAinmInfo == null || (!active.prepared && (_nextAinmInfo.mode != (HFlag.EMode)identity.Mode || _nextAinmInfo.id != identity.Id))
                || (active.prepared && !active.NativeChoices().Contains(_nextAinmInfo)))
                throw new NotSupportedException("This dedicated native session has not prepared the requested KKS pose identity.");
            return true;
        }
        static Exception AfterChangeAnimator(HSceneProc __instance, HSceneProc.AnimationListInfo _nextAinmInfo, bool __state, Exception __exception)
        {
            if (active?.Owns(__instance) == true) {
                active.nativeCall = __state;
                if (__exception == null) active.runtime.ObserveSelection(_nextAinmInfo);
            }
            return __exception;
        }
        static void AfterNativeCatalog(HSceneProc __instance)
        {
            var owner = active; if (owner?.Owns(__instance) != true) return;
            var choices = (List<HSceneProc.AnimationListInfo>[])AccessTools.Field(typeof(HSceneProc), "lstUseAnimInfo").GetValue(__instance);
            // This backend borrows exactly one woman and one man. Solo/3P
            // scenes require a different participant binding, not a fake actor.
            foreach (var list in choices) list?.RemoveAll(row => row.mode < HFlag.EMode.aibu || row.mode > HFlag.EMode.sonyu);
            var identity = owner.context.Request.KksPose;
            // Keep the native location/experience/role filtering. The previous
            // prototype removed every pose except its one bootstrap identity.
            if (!owner.prepared && !choices.Any(list => list != null && list.Any(row => row.mode == (HFlag.EMode)identity.Mode && row.id == identity.Id)))
                throw new InvalidOperationException("Native KKS eligibility did not offer the requested pose. No unlock or profile override was applied.");
        }
        static bool BeforeGlobalSave() => active?.nativeCall != true;
        static bool BeforeGlobalSoundStop(Manager.Sound.Type __0) => active?.nativeCall != true || __0 != Manager.Sound.Type.GameSE3D;
        static bool BeforeNativeBgm() => active?.nativeCall != true;
        static void BeforeNativeVoice(HVoiceCtrl __instance, ref string __0, ref string __1)
        {
            if (active?.proc == null || active.proc.voice != __instance) return;
            // Installed cha_sample_voice_00 ID0 is セクシー系お姉さま.
            // Adapt this session's native table input, never the saved card.
            __0 = Presentation.KksSexyVoicePlayback.Personality; __1 = string.Empty;
        }
        static void BeforeNativeFace(FaceListCtrl __instance, ref string __0)
        { if (active?.proc != null && (active.proc.face == __instance || active.proc.face1 == __instance)) __0 = Presentation.KksSexyVoicePlayback.Personality; }
        static bool BeforeNativeFade(FadeCanvas __instance, FadeCanvas.Fade __0, ref UniTask __result)
        {
            if (active?.nativeCall != true || __instance != GameScene.sceneFadeCanvas) return true;
            // The dedicated session alone owns Cover/Reveal; original native
            // Start may not reveal actors before the remaining modules are ready.
            __result = UniTask.CompletedTask; return false;
        }
        static bool BeforeNativeTutorial(Tutorial.Category __0, ref bool __result)
        {
            if (active?.nativeCall != true || __0 != Tutorial.Category.H) return true;
            // An interactive first-H tutorial cannot own the black preparation
            // cover. Do not mark it completed or change the user's tutorial save.
            __result = false; return false;
        }
        static bool BeforeMapDependent(HSceneProc __instance) => active?.Owns(__instance) != true;
        static bool BeforePointMove(HSceneProc __instance)
        {
            var owner = active;
            if (owner?.Owns(__instance) != true) return true;
            if (owner.Current && owner.IsPrepared && owner.context.CanInteract()) owner.OpenPoints();
            return false;
        }
        static bool BeforeNativeEnd(HSceneProc __instance)
        {
            var owner = active; if (owner?.Owns(__instance) != true) return true;
            __instance.enabled = false; owner.requestEnd(EndReason.Completed); return false;
        }
        static void AfterMapAwake(ActionMap __instance)
        {
            var owner = active; if (owner?.OwnsPending(__instance, "HProc") != true) return;
            var sceneProc = __instance.GetComponent<HSceneProc>(); if (sceneProc == null) return;
            owner.map = __instance;
            if (owner.disposed) { owner.proc = sceneProc; if (owner.gate.Scene == null) owner.gate.BindCleanupScene(sceneProc); return; }
            owner.BindProc(sceneProc); owner.map = __instance; __instance.isLock = true;
            SetProperty(__instance, typeof(BaseMap), "mapRoot", owner.context.Request.Island);
            SetProperty(__instance, typeof(BaseMap), "no", default(int));
            SetProperty(__instance, typeof(BaseMap), "infoDic", BaseMap.LoadMapInfo());
            SetProperty(__instance, typeof(ActionMap), "mapObjects", Array.Empty<ActionGame.MapObject.Kind>());
            SetProperty(__instance, typeof(ActionMap), "mapObjectGroup", owner.context.Request.Island.transform);
        }
        static bool BeforeMapStart(ActionMap __instance) => active?.map != __instance;
        static bool BeforeMapChange(BaseMap __instance) => active?.map != __instance;
        static void SetProperty(object target, Type type, string name, object value)
        { (AccessTools.PropertySetter(type, name) ?? throw new MissingMemberException(type.FullName, name)).Invoke(target, new[] { value }); }
        static bool BeforeNativeDestroy(HSceneProc __instance)
        {
            var owner = active; if (owner?.Owns(__instance) != true) return true;
            owner.StopNativeObjects(); owner.nativeDestroyed = true;
            owner.Attempt(() => owner.actors?.Dispose());
            // Native cleanup excluding its process-wide Voice.StopAll/Sound.Stop.
            owner.Attempt(() => AccessTools.Field(typeof(HSceneProc), "ctrlObi").GetValue(__instance)?.GetType().GetMethod("EndProcSolver")?.Invoke(AccessTools.Field(typeof(HSceneProc), "ctrlObi").GetValue(__instance), null));
            owner.Attempt(() => AccessTools.Field(typeof(HSceneProc), "ctrlObi").GetValue(__instance)?.GetType().GetMethod("EndProc")?.Invoke(AccessTools.Field(typeof(HSceneProc), "ctrlObi").GetValue(__instance), null));
            owner.Attempt(() => HMotionEyeNeck.EndProc()); owner.Attempt(() => ParentObjectCtrl.EndProc()); owner.Attempt(() => AnimatorLayerCtrl.EndProc());
            return false;
        }
        void StopNativeObjects()
        {
            pointPanel?.Release();
            if (proc == null) return; proc.enabled = false; proc.StopAllCoroutines();
            foreach (var behaviour in proc.GetComponentsInChildren<Behaviour>(true)) if (behaviour != null && !(behaviour is Camera)) behaviour.enabled = false;
            foreach (var participant in context.Request.Participants)
                if (!resourcesRestored && participant.Character != null)
                {
                    if (participant.Character.asVoice != null && !previousActorAudio.Contains(participant.Character.asVoice)) participant.Character.asVoice.Stop();
                    foreach (var audio in participant.Character.GetComponentsInChildren<AudioSource>(true)) if (!previousActorAudio.Contains(audio)) audio.Stop();
                }
            foreach (var audio in proc.GetComponentsInChildren<AudioSource>(true)) audio.Stop();
        }
        void Fail(Exception error)
        {
            CancelDeadline();
            proc?.StopAllCoroutines(); if (proc != null) proc.enabled = false;
            var callback = failed; failed = null; ready = null;
            if (callback != null) callback(error); else { context?.TraceException?.Invoke(error); context?.ReportFailure("Owned native engine: " + error.Message); }
        }
        IEnumerator StartupDeadline()
        {
            yield return new WaitForSecondsRealtime(startupTimeout); deadline = null;
            if (!prepared && !disposed) Fail(new TimeoutException("Owned native startup did not finish: " + (loadingOuter ? "H load" : loadingProc ? "HProc load" : "native Start dependencies")));
        }
        void CancelDeadline() { if (deadline != null && context?.Request.Owner != null) context.Request.Owner.StopCoroutine(deadline); deadline = null; }
        public void Select(string motionId)
        { if (!Current || !IsPrepared || motionId != SelectedMotionId) throw new InvalidOperationException("The native pose identity is not prepared."); }
        public void SetSpeed(float speed) { if (!float.IsNaN(speed) && !float.IsInfinity(speed) && speed == runtime.Flags.speed) return; throw new InvalidOperationException("Native HHoushi owns continuous animation speed; use its native speed command."); }
        // DedicatedSession dispatches to every module. Progression alone maps
        // upper commands into the native owner, so a wheel is never applied twice.
        public void Execute(Command command) { }
        internal void DispatchNativeCommand(Command command)
        {
            if (!Current || !IsPrepared || movingPoint || !context.CanInteract() || command == null) return;
            switch (command.Name)
            {
                case "wheel": proc.flags.SpeedUpClick(Math.Sign(command.Amount) * proc.flags.rateWheelSpeedUp, runtime.Protocol.clipEnd); break;
                case "speed": proc.flags.speedCalc = Mathf.Clamp01(command.Amount); break;
                case "loop-change": proc.flags.click = HFlag.ClickKind.motionchange; break;
                case "finish": proc.flags.click = (HFlag.ClickKind)Enum.Parse(typeof(HFlag.ClickKind), command.Value, true); break;
                case "state": throw new InvalidOperationException("Native HAction owns state transitions; direct state substitution is forbidden.");
            }
        }
        public void ValidateMotionSelection(string id, bool automatic) { if (automatic || id != SelectedMotionId) throw new NotSupportedException("Only the explicitly prepared native pose is available."); }
        public void SuspendSelection() { if (proc != null) proc.enabled = false; if (spriteInput != null) spriteInput.interactable = false; camera?.Suspend(); }
        public void ResumeSelection() { if (Current && IsPrepared && context.CanInteract()) { proc.enabled = true; camera.Resume(); if (spriteInput != null) spriteInput.interactable = spriteInteractable; } }
        public void End(EndReason reason)
        {
            SuspendSelection(); StopNativeObjects();
            ReleaseNativeBody();
            if (reason == EndReason.Completed && prepared)
            {
                runtime.CaptureEnding(); endingStarted = true;
                foreach (var participant in context.Request.Participants) { participant.Character.SetPosition(Vector3.zero); participant.Character.SetRotation(Vector3.zero); }
                proc.guideObject.amount.position = proc.guideObject.amount.rotation = Vector3.zero;
                proc.guideObject.amountOffset.position = proc.guideObject.amountOffset.rotation = Vector3.zero;
                proc.guideObject.amountTotal.position = proc.guideObject.amountTotal.rotation = Vector3.zero;
                foreach (var nativeEnding in ending) nativeEnding.Begin();
                foreach (var participant in context.Request.Participants.Where(p => p.IsPlayer)) participant.Character.visibleAll = false;
            }
        }
        public void PlayNativeEndingMotion(int femaleSlot, string exactNativeState)
        { if (!endingStarted || femaleSlot < default(int) || femaleSlot >= ending.Count) throw new InvalidOperationException("Native ending actor is not owned."); ending[femaleSlot].Play(exactNativeState); }
        void Attempt(Action action) { try { action(); } catch (Exception error) { cleanupErrors.Add(error); context?.TraceException?.Invoke(error); } }
        void ReleaseNativeBody()
        {
            if (bodyReleased || proc == null) return; bodyReleased = true;
            Attempt(() => proc.hand?.DetachAllItem()); Attempt(HandCtrl.ReleaseList);
            Attempt(() => { var list = (List<MotionIK>)AccessTools.Field(typeof(HSceneProc), "lstMotionIK").GetValue(proc);
                foreach (var ik in list) { Attempt(ik.Reset); Attempt(ik.Release); } list.Clear(); });
            Attempt(() => proc.dynamicCtrl?.InitDynamicBoneReferenceBone());
            Attempt(() => proc.yure?.Release()); Attempt(() => proc.yure?.ResetShape());
            Attempt(() => proc.yure1?.Release()); Attempt(() => proc.yure1?.ResetShape());
            Attempt(() => proc.yureMale?.Release()); Attempt(() => proc.yureMale?.ResetShape());
            var nativeParticleSlots = new[] { proc.particle, proc.particle1 };
            for (int slot = default(int); slot < nativeParticleSlots.Length; ++slot) { int captured = slot; Attempt(() => HParticleCtrl.ReleaseObject(captured)); }
            Attempt(HParticleCtrl.ReleaseDic);
            foreach (var parent in new[] { proc.parentObjectBaseFemale, proc.parentObjectBaseFemale1, proc.parentObjectBaseMale,
                proc.parentObjectFemale, proc.parentObjectFemale1, proc.parentObjectMale }) if (parent != null) Attempt(() => parent.ReleaseObject());
            Attempt(() => { var item = AccessTools.Field(typeof(HSceneProc), "item").GetValue(proc); item.GetType().GetMethod("ReleaseItem").Invoke(item, null); });
            // These are the native ResetScene resource releases. Its remaining
            // character relocation, normal-H save/unload and global audio stop
            // are deliberately owned by our actor/camera/session leases instead.
        }
        public void Dispose() => DisposeAsync(() => { }, error => context?.ReportFailure("Native engine cleanup: " + error.Message));
        public void DisposeAsync(Action completed, Action<Exception> failure)
        {
            if (disposed) { if (ReferenceEquals(active, this)) throw new InvalidOperationException("Native cleanup is already pending."); completed(); return; }
            disposed = true; cleaned = completed; cleanupFailed = failure;
            CancelDeadline();
            StopNativeObjects();
            DisposePoints();
            ReleaseNativeBody();
            foreach (var nativeEnding in ending) Attempt(nativeEnding.Dispose);
            Attempt(() => camera?.Dispose());
            if (map != null) Attempt(() => SetProperty(map, typeof(BaseMap), "mapRoot", null));
            // Scene stack removal is asynchronous. Keep exact Harmony/gate identity
            // until native OnDestroy has run; never unload another owner's scene.
            if (!loadingOuter && !loadingProc) StartUnload();
        }
        void StartUnload()
        {
            if (context?.Request.Owner != null) context.Request.Owner.StartCoroutine(UnloadOwned());
            else if (procHandle == default(int) && outerHandle == default(int)) ReleaseHooks();
            else { cleanupErrors.Add(new InvalidOperationException("Native scene cleanup needs the surviving owning host.")); CompleteCleanup(); }
        }
        IEnumerator UnloadOwned()
        {
            foreach (var name in new[] { "HProc", "H" })
            {
                var ownedHandle = name == "HProc" ? procHandle : outerHandle;
                var scene = OwnedScene(ownedHandle);
                if (!scene.IsValid() || !scene.isLoaded) continue;
                if (GameScene.NowSceneNames.FirstOrDefault() != name) {
                    cleanupErrors.Add(new InvalidOperationException("Owned native scene is not at the scene-stack boundary: " + name + "; its stopped scene and cleanup identity remain retained."));
                    RestoreBorrowedResources(); CompleteCleanup(); yield break;
                }
                yield return GameScene.UnloadAsync(false).ToCoroutine();
            }
            if (!nativeDestroyed && proc != null) cleanupErrors.Add(new InvalidOperationException("Native HProc destruction was not observed."));
            ReleaseHooks();
        }
        void ReleaseHooks()
        {
            RestoreBorrowedResources();
            Attempt(() => patches?.UnpatchSelf()); Attempt(() => gate?.Dispose());
            if (ReferenceEquals(active, this)) active = null; CompleteCleanup();
        }
        void RestoreBorrowedResources()
        {
            if (resourcesRestored) return; resourcesRestored = true;
            Attempt(() => actors?.Dispose());
            foreach (var body in bodyRestore) Attempt(body.Dispose);
            foreach (var restore in globalRestore) Attempt(restore);
            if (endingStarted) foreach (var participant in context.Request.Participants) Attempt(() => Presentation.PresentationModule.NormalizeNativeCompleted(participant.Character));
        }
        void CompleteCleanup()
        {
            var success = cleaned; var failure = cleanupFailed; cleaned = null; cleanupFailed = null;
            if (cleanupErrors.Count == default(int)) success?.Invoke(); else failure?.Invoke(new AggregateException("Native engine cleanup failed.", cleanupErrors));
        }
        [DataContract] sealed class EnginePolicy { [DataMember] public int version { get; set; } [DataMember] public float startupTimeoutSeconds { get; set; } }
    }
}
