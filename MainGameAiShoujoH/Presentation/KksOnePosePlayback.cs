using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using H;
using Illusion.Component.Correct;
using Illusion.Component.Correct.Process;
using MainGameAiShoujoH.Contracts;
using MainGameAiShoujoH.Motion;
using Manager;
using RootMotion.FinalIK;
using UnityEngine;

namespace MainGameAiShoujoH.Presentation
{
    // Explicit experiment: AI houshi:2 progression, KKS AnimationInfo_01 id 0.
    // Only native Animator/FinalIK drivers run continuously; this lease has no
    // Update, transform-copy loop, or independent game/session lifetime.
    internal sealed class KksOnePosePlayback : IDisposable
    {
        internal const string MotionId = "houshi:2";
        readonly SessionContext context;
        readonly Action<float> cycle;
        readonly List<Action> restore = new List<Action>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly Dictionary<string, Animator> animators = new Dictionary<string, Animator>();
        readonly Dictionary<string, MotionIK> contacts = new Dictionary<string, MotionIK>();
        readonly Dictionary<string, ChaControl> characters = new Dictionary<string, ChaControl>();
        readonly Dictionary<string, YureCtrl> physics = new Dictionary<string, YureCtrl>();
        AnimatorLayerCtrl layers;
        string state;
        float lastCycleTime = float.NegativeInfinity;
        int cycles;
        bool disposed, ending;

        internal KksOnePosePlayback(SessionContext context, IDictionary<string, ChaControl> actors, Action<float> cycle)
        {
            this.context = context; this.cycle = cycle;
            if (context.Request.MotionId != MotionId || actors.Count != 2 || !actors.ContainsKey("female") || !actors.ContainsKey("male"))
                throw new InvalidDataException("KKS one-pose trial requires houshi:2 and exactly female/male participants.");
            KksNativePoseDefinition.ValidateIdentity(context.Request.KksPose);
            try
            {
                foreach (var role in new[] { "female", "male" }) PrepareActor(role, actors[role]);
                // Native SetPartners retains self as slot0 and the partner as slot1.
                foreach (var contact in contacts.Values) contact.SetPartners(contacts.Values);
                InitializeNativeCorrections();
                ReapplyShape();
                var receiver = animators["female"].gameObject.AddComponent<KksOnePoseEventReceiver>(); owned.Add(receiver);
                receiver.Cycle = OnCycle;
                restore.Add(() => { if (receiver != null) receiver.Cycle = null; });
            }
            catch { Dispose(); throw; }
        }

        void PrepareActor(string role, ChaControl actor)
        {
            var animator = actor.animBody ?? throw new InvalidDataException("KKS trial actor Animator missing: " + role);
            animators.Add(role, animator);
            characters.Add(role, actor);
            var previous = animator.runtimeAnimatorController;
            var enabled = animator.enabled; var events = animator.fireEvents; var root = animator.applyRootMotion; var culling = animator.cullingMode; var speed = animator.speed;
            var parameters = new List<Action>();
            foreach (var parameter in animator.parameters)
            {
                int hash = parameter.nameHash;
                if (parameter.type == AnimatorControllerParameterType.Float) { float value = animator.GetFloat(hash); parameters.Add(() => animator.SetFloat(hash, value)); }
                else if (parameter.type == AnimatorControllerParameterType.Int) { int value = animator.GetInteger(hash); parameters.Add(() => animator.SetInteger(hash, value)); }
                else if (parameter.type == AnimatorControllerParameterType.Bool) { bool value = animator.GetBool(hash); parameters.Add(() => animator.SetBool(hash, value)); }
            }
            var layers = Enumerable.Range(0, animator.layerCount).Select(index => new { Index = index, Weight = animator.GetLayerWeight(index), State = animator.GetCurrentAnimatorStateInfo(index) }).ToArray();
            restore.Add(() =>
            {
                if (animator == null) return;
                animator.runtimeAnimatorController = previous;
                foreach (var parameter in parameters) parameter();
                foreach (var layer in layers) if (layer.Index < animator.layerCount)
                { animator.SetLayerWeight(layer.Index, layer.Weight); if (animator.HasState(layer.Index, layer.State.fullPathHash)) animator.Play(layer.State.fullPathHash, layer.Index, layer.State.normalizedTime); }
                animator.enabled = enabled; animator.fireEvents = events; animator.applyRootMotion = root; animator.cullingMode = culling; animator.speed = speed;
            });
            CaptureContacts(animator);
            var masks = (bool[,])actor.fileStatus.disableBustShapeMask.Clone();
            restore.Add(() => { if (actor != null) for (int side = 0; side < masks.GetLength(0); side++) for (int index = 0; index < masks.GetLength(1); index++) actor.DisableShapeBodyID(side, index, masks[side, index]); });
            foreach (var bone in actor.dictDynamicBoneBust.Values.Where(value => value != null))
            { var original = bone; bool active = bone.enabled; restore.Add(() => { if (original != null) original.enabled = active; }); }
            string sex = role == "female" ? "f" : "m";
            string bundle = "h/anim/" + role + "/01_01_00.unity3d";
            string basisName = "khh_" + sex + "_base", poseName = "khh_" + sex + "_00";
            bool released = false;
            Action releaseBundle = () => { if (released) return; released = true; AssetBundleManager.UnloadAssetBundle(bundle, isUnloadForceRefCount: true); };
            restore.Add(releaseBundle);
            var basis = CommonLib.LoadAsset<RuntimeAnimatorController>(bundle, basisName);
            var pose = CommonLib.LoadAsset<RuntimeAnimatorController>(bundle, poseName);
            if (basis == null || pose == null) throw new InvalidDataException("KKS trial controller missing: " + bundle + "/" + poseName);
            var merged = Illusion.Utils.Animator.SetupAnimatorOverrideController(basis, pose);
            if (merged == null) throw new InvalidDataException("KKS trial override construction failed: " + role);
            owned.Add(merged);
            // Session-local clips add one boundary notification without mutating
            // the shared native animation assets or installing a frame monitor.
            var controller = merged as AnimatorOverrideController ?? new AnimatorOverrideController(merged);
            if (!ReferenceEquals(controller, merged)) owned.Add(controller);
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(); controller.GetOverrides(overrides);
            var clones = new Dictionary<AnimationClip, AnimationClip>();
            for (int index = 0; index < overrides.Count; ++index)
            {
                var original = overrides[index].Value ?? overrides[index].Key;
                if (original == null) throw new InvalidDataException("KKS trial override clip missing: " + role);
                if (!clones.TryGetValue(original, out var clone))
                {
                    clone = UnityEngine.Object.Instantiate(original); owned.Add(clone); clones.Add(original, clone);
                    if (role == "female")
                    {
                        var notification = new AnimationEvent { functionName = nameof(KksOnePoseEventReceiver.OnKksOnePoseCycle), time = clone.length };
                        clone.events = clone.events.Concat(new[] { notification }).ToArray();
                    }
                }
                overrides[index] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[index].Key, clone);
            }
            controller.ApplyOverrides(overrides); animator.runtimeAnimatorController = controller;
            releaseBundle();
            animator.enabled = true; animator.fireEvents = true; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.speed = 1;
            foreach (var required in new[] { "Idle", "WLoop", "SLoop", "OLoop" })
                if (!animator.HasState(0, Animator.StringToHash(required))) throw new InvalidDataException("KKS trial required state missing: " + role + "/" + required);
            var ik = new MotionIK(actor); contacts.Add(role, ik);
            if (ik.frameCorrect == null || ik.ikCorrect == null) throw new InvalidDataException("KKS trial correction drivers missing: " + role);
            ik.frameCorrect.enabled = true; ik.ikCorrect.enabled = true;
            var data = GlobalMethod.LoadAllFolderInOneFile<TextAsset>("h/list/", poseName);
            if (data == null || !ik.LoadData(data)) throw new InvalidDataException("KKS trial native contact table missing: " + poseName);
            foreach (var required in new[] { "Idle", "WLoop" })
                if (ik.GetNowState(required) == null) throw new InvalidDataException("KKS native contact state missing: " + poseName + "/" + required);
        }

        void InitializeNativeCorrections()
        {
            // The compiled native loaders use this shared bundle-name list.
            // Borrow only during loading and leave other H sessions unchanged.
            var previous = HFlag.lstABName;
            try
            {
                HFlag.lstABName = CommonLib.GetAssetBundleNameListFromPath("h/list/"); HFlag.lstABName.Sort();
                layers = animators["female"].gameObject.AddComponent<AnimatorLayerCtrl>(); owned.Add(layers); layers.enabled = false;
                if (!layers.Init(animators["female"]) || !layers.Load("h/list/", "khh_f_00")) throw new InvalidDataException("KKS native animation layer initialization failed.");
                foreach (var role in characters.Keys)
                {
                    var control = new YureCtrl(); control.Init(characters[role]);
                    string file = role == "female" ? "yure_khh_00_00" : "yure_khh_m_00";
                    control.LoadAllExcel("h/list/", file); physics.Add(role, control);
                }
            }
            finally { HFlag.lstABName = previous; }
        }

        void CaptureContacts(Animator animator)
        {
            var solver = animator.GetComponent<FullBodyBipedIK>() ?? throw new InvalidDataException("KKS trial body IK missing");
            bool solverEnabled = solver.enabled; restore.Add(() => { if (solver != null) solver.enabled = solverEnabled; });
            foreach (var pair in MotionIK.IKTargetPair.GetPairs(solver.solver))
            {
                var effector = pair.effector; var bend = pair.bend;
                var target = effector.target; var position = effector.positionWeight; var rotation = effector.rotationWeight;
                var goal = bend.bendGoal; var weight = bend.weight;
                restore.Add(() => { effector.target = target; effector.positionWeight = position; effector.rotationWeight = rotation; bend.bendGoal = goal; bend.weight = weight; });
            }
            foreach (var data in animator.GetComponentsInChildren<BaseData>(true))
            {
                var value = data; var bone = data.bone; var position = data.pos; var rotation = data.rot;
                restore.Add(() => { if (value != null) { value.bone = bone; value.pos = position; value.rot = rotation; } });
            }
            foreach (var process in animator.GetComponentsInChildren<BaseProcess>(true))
            { var value = process; bool active = process.enabled; restore.Add(() => { if (value != null) value.enabled = active; }); }
            foreach (var correct in animator.GetComponentsInChildren<BaseCorrect>(true))
            {
                var owner = correct; bool active = correct.enabled; restore.Add(() => { if (owner != null) owner.enabled = active; });
                foreach (var info in correct.list)
                {
                    var value = info; var bone = info.bone; var position = info.pos; var rotation = info.rot; bool activeInfo = info.enabled;
                    restore.Add(() => { value.bone = bone; value.pos = position; value.rot = rotation; value.enabled = activeInfo; });
                }
            }
        }

        internal void Play(string stateName, float speed)
        {
            RequireCurrent();
            stateName = KksNativePoseDefinition.NativeState(stateName);
            foreach (var animator in animators.Values)
                if (!animator.HasState(0, Animator.StringToHash(stateName))) throw new InvalidDataException("KKS trial has no exact state: " + stateName);
            state = stateName; cycles = 0; lastCycleTime = float.NegativeInfinity;
            foreach (var role in animators.Keys)
            {
                var animator = animators[role];
                SetFloat(animator, "speed", speed, true);
                contacts[role].Calc(stateName);
                characters[role].syncPlay(stateName, 0, 0);
                physics[role].Proc(stateName);
            }
            layers.Proc(stateName);
        }
        internal void Parameter(string name, float value)
        { RequireCurrent(); foreach (var animator in animators.Values) SetFloat(animator, name == "breast" ? "Breast" : name, value, name == "speed"); }
        internal void ReapplyShape()
        {
            RequireCurrent();
            // HActionBase.SetDefaultAnimatorFloat uses the primary female's
            // height for both actors; selecting separate height clips breaks
            // the native pair's authored synchronization and contact geometry.
            float height = characters["female"].GetShapeBodyValue(0);
            float breast = characters["female"].GetShapeBodyValue(4);
            foreach (var role in animators.Keys)
            {
                SetFloat(animators[role], "height", height, true);
                SetFloat(animators[role], "Breast", breast, true);
                SetFloat(animators[role], "speedHand", 1, true);
                if (state != null) contacts[role].Calc(state);
            }
        }
        internal void BeginEnding()
        {
            if (disposed) throw new InvalidOperationException("KKS body lease has already ended.");
            ending = true;
            // Stop this H clock while the native ADV body lease takes control.
            // Keep the snapshots until that lease returns the owned native body.
            foreach (var animator in animators.Values) animator.speed = 0;
        }
        internal void PlayEndingIdle() { if (!ending) throw new InvalidOperationException("KKS ending body is not active."); Play("Idle", 1); }
        static void SetFloat(Animator animator, string name, float value, bool required)
        {
            if (animator.parameters.Any(p => p.name == name && p.type == AnimatorControllerParameterType.Float)) animator.SetFloat(name, value);
            else if (required) throw new InvalidDataException("KKS trial float parameter missing: " + name);
        }
        void OnCycle(AnimationEvent notification)
        {
            if (disposed || !context.IsCurrent() || context.CanInteract?.Invoke() != true || lastCycleTime == Time.time) return;
            if (!animators["female"].GetCurrentAnimatorStateInfo(0).IsName(state)) return;
            // Breast/additive clips also carry cloned events, but only the native
            // base layer's actual clips may advance the AI procedure clock.
            if (!animators["female"].GetCurrentAnimatorClipInfo(0).Any(info => info.clip == notification.animatorClipInfo.clip)) return;
            lastCycleTime = Time.time; cycle?.Invoke(++cycles);
        }
        void RequireCurrent() { if (disposed || !ending && !context.IsCurrent()) throw new InvalidOperationException("KKS trial session expired"); }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var action in Enumerable.Reverse(restore)) try { action(); } catch (Exception error) { context.ReportFailure("KKS trial restore: " + error.Message); }
            restore.Clear();
            foreach (var item in Enumerable.Reverse(owned)) if (item != null) UnityEngine.Object.Destroy(item);
            owned.Clear(); contacts.Clear(); animators.Clear(); characters.Clear();
        }
    }
    public sealed class KksOnePoseEventReceiver : MonoBehaviour
    {
        internal Action<AnimationEvent> Cycle;
        public void OnKksOnePoseCycle(AnimationEvent notification) { Cycle?.Invoke(notification); }
    }
}
