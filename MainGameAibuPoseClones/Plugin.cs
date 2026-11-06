using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace MainGameAibuPoseClones
{
    [BepInDependency("canon.gate")]
    [BepInPlugin("com.kks.maingame.aibuposeclones", "MainGameAibuPoseClones", "0.1.0")]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private static readonly FieldInfo UseListField = AccessTools.Field(typeof(HSceneProc), "lstUseAnimInfo");
        private static readonly FieldInfo FemaleListField = AccessTools.Field(typeof(HSceneProc), "lstFemale");
        private static readonly FieldInfo MaleField = AccessTools.Field(typeof(HSceneProc), "male");
        private static readonly FieldInfo ActionFlagsField = AccessTools.Field(typeof(HActionBase), "flags");
        private static readonly FieldInfo ItemDictionaryField = AccessTools.Field(typeof(HandCtrl), "dicItem");

        private static Plugin _instance;
        private Harmony _harmony;
        private BepInEx.Configuration.ConfigEntry<bool> _logEnabled;
        private bool LogEnabled => _logEnabled != null && _logEnabled.Value;
        private void LogInfo(string message) { if (LogEnabled) Logger.LogInfo(message); }
        private CanonPlugins.Shared.GateScene _observedScene;
        private readonly List<HSceneProc.AnimationListInfo> _sessionClones = new List<HSceneProc.AnimationListInfo>();
        private string _lastMotionSignature;
        private float _nextMotionSample;

        private HandCtrl _hiddenHandOwner;
        private readonly Dictionary<Renderer, bool> _hiddenHandRenderers = new Dictionary<Renderer, bool>();
        private bool _wasClone;
        private float _orgasmAfterStarted = -1f;
        private bool _loggedNoHandMeshes;
        private static HSceneProc.AnimationListInfo _changingToPose;

        private void Awake()
        {
            _instance = this;
            _logEnabled = Config.Bind("Logging", "LogEnabled", false,
                "モーション・クローンの診断ログ。OFFではAnimatorの診断走査も停止。変更は即時反映。");
            _harmony = new Harmony("com.kks.maingame.aibuposeclones");
            _harmony.PatchAll(typeof(Plugin));
        
            var vr = Type.GetType("VRHScene, Assembly-CSharp", false);
            if (vr != null)
            {
                var change = AccessTools.Method(vr, "ChangeAnimator");
                if (change != null) _harmony.Patch(change, prefix: new HarmonyMethod(typeof(Plugin), nameof(TrackSelectedPose)), postfix: new HarmonyMethod(typeof(Plugin), nameof(FinishSelectedPose)));
                var list = AccessTools.Method(vr, "CreateListAnimationFileName");
                if (list != null) _harmony.Patch(list, postfix: new HarmonyMethod(typeof(Plugin), nameof(AddCloneEntries)));
            }
            CanonPlugins.Shared.PluginSessionGate.Bind(this, Info.Metadata.GUID,
                () => {
                    if (CanonPlugins.Shared.PluginSessionGate.Scene != null) _observedScene = new CanonPlugins.Shared.GateScene(CanonPlugins.Shared.PluginSessionGate.Scene);
                    TraceHotelClone("clone.gate-start.before", _observedScene?.Source);
                    if (_observedScene != null) AddCloneEntries(_observedScene.Source);
                    TraceHotelClone("clone.gate-start.after", _observedScene?.Source);
                }, EndGateSession);
        }

        private void OnDestroy()
        {
            CanonPlugins.Shared.PluginSessionGate.Dispose();
            RestoreHandRenderers();
            _harmony?.UnpatchSelf();
            if (ReferenceEquals(_instance, this)) _instance = null;
        }

        [HarmonyPatch(typeof(HSceneProc), "ChangeAnimator")]
        [HarmonyPrefix]
        private static void TrackSelectedPose([HarmonyArgument(0)] HSceneProc.AnimationListInfo _nextAinmInfo)
        {
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed) return;
            // MotionChange calls SetPlay before nowAnimationInfo is updated.
            _changingToPose = _nextAinmInfo;
        }

        [HarmonyPatch(typeof(HSceneProc), "ChangeAnimator")]
        [HarmonyPostfix]
        private static void FinishSelectedPose(MonoBehaviour __instance)
        {
            try
            {
                if (!CanonPlugins.Shared.PluginSessionGate.Allowed) return;
                var flags = AccessTools.Field(__instance.GetType(), "flags")?.GetValue(__instance) as HFlag;
                var pose = flags?.nowAnimationInfo;
                if (!IsClone(pose) || pose.id != 1) return;
                var actions = AccessTools.Field(__instance.GetType(), "lstProc")?.GetValue(__instance)
                    as List<HActionBase>;
                int mode = (int)HFlag.EMode.aibu;
                if (actions == null || actions.Count <= mode || !(actions[mode] is HAibu)) return;
                actions[mode].SetPlay("A_Idle");
                _instance?.LogInfo("[GroinPose] ChangeAnimator completed; forced A_Idle for selected clone");
            }
            finally
            {
                _changingToPose = null;
            }
        }


        [HarmonyPatch(typeof(HActionBase), "SetPlay", new[] { typeof(string), typeof(bool) })]
        [HarmonyPrefix]
        private static void KeepSupineCloneGroinMotion(HActionBase __instance, ref string _nextAnimation)
        {
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed) return;
            if (!(__instance is HAibu)) return;
            var flags = ActionFlagsField?.GetValue(__instance) as HFlag;
            var pose = _changingToPose ?? flags?.nowAnimationInfo;
            if (pose == null || pose.id != 1 || !IsClone(pose)) return;
            string current = flags.nowAnimStateName ?? string.Empty;
            if (current.StartsWith("Orgasm", StringComparison.Ordinal) ||
                current.StartsWith("After", StringComparison.Ordinal)) return;
            switch (_nextAnimation)
            {
                case "Idle":
                case "A_Touch":
                case "K_Touch":
                case "M_Touch":
                case "S_Touch":
                case "K_Loop":
                case "M_Idle":
                case "S_Idle":
                    _nextAnimation = "A_Idle";
                    break;
            }
        }

        [HarmonyPatch(typeof(HAibu), "Proc")]
        [HarmonyPostfix]
        private static void RestoreGroinPoseAfterOrgasm(HAibu __instance)
        {
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed) return;
            var plugin = _instance;
            if (plugin == null) return;
            var flags = ActionFlagsField?.GetValue(__instance) as HFlag;
            if (!IsClone(flags?.nowAnimationInfo) || flags.nowAnimationInfo.id != 1)
            {
                plugin._orgasmAfterStarted = -1f;
                return;
            }
            if (flags.nowAnimStateName != "Orgasm_A")
            {
                plugin._orgasmAfterStarted = -1f;
                return;
            }
            if (plugin._orgasmAfterStarted < 0f) plugin._orgasmAfterStarted = Time.unscaledTime;
            if (Time.unscaledTime - plugin._orgasmAfterStarted < 0.5f ||
                flags.voice.playVoices[0] != -1 || Manager.Voice.IsPlay(flags.transVoiceMouth[0])) return;
            var scene = plugin._observedScene;
            var female = scene?.lstFemale;
            if (female == null || female.Count == 0 || female[0] == null ||
                !female[0].getAnimatorStateInfo(0).IsName("Orgasm_A") ||
                female[0].getAnimatorStateInfo(0).normalizedTime < 1f) return;
            __instance.SetPlay("A_Idle");
            plugin.LogInfo("[GroinPose] orgasm finished; restored A_Idle");
            plugin._orgasmAfterStarted = -1f;
        }

        [HarmonyPatch(typeof(HActionBase), "SetPlay", new[] { typeof(string), typeof(bool) })]
        [HarmonyPostfix]
        private static void LogMotionPlay(HActionBase __instance, string _nextAnimation)
        {
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed) return;
            var plugin = _instance;
            if (plugin == null) return;
            var flags = ActionFlagsField?.GetValue(__instance) as HFlag;
            var pose = flags?.nowAnimationInfo;
            if (pose == null) return;
            plugin.LogInfo("[HMotionPlay] mode=" + pose.mode + " id=" + pose.id
                + " pose=" + pose.nameAnimation + " state=" + _nextAnimation);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextMotionSample) return;
            _nextMotionSample = Time.unscaledTime + 0.1f;

            if (_observedScene == null || _observedScene.flags == null
                || _observedScene.flags.nowAnimationInfo == null)
            {
                RestoreHandRenderers();
                _wasClone = false;
                return;
            }

            bool clone = IsClone(_observedScene.flags.nowAnimationInfo)
                && _observedScene.flags.nowAnimationInfo.id == 1;
            if (!clone)
            {
                if (_wasClone) LogInfo("[GroinPose] clone exited; hand renderers restored");
                RestoreHandRenderers();
            }
            else
            {
                HideCloneHandRenderers(_observedScene.hand);
                if (!_wasClone) LogInfo("[GroinPose] clone active; A_Idle fixed, hand hidden");
            }
            _wasClone = clone;

            if (!LogEnabled) { _lastMotionSignature = null; return; }

            try
            {
                HFlag flags = _observedScene.flags;
                HSceneProc.AnimationListInfo pose = flags.nowAnimationInfo;
                var females = _observedScene.lstFemale;
                var male = _observedScene.Read<ChaControl>("male");
                string femaleClips = females != null && females.Count > 0 ? DescribeAnimator(females[0]) : "<none>";
                string maleClips = DescribeAnimator(male);
                string state = flags.nowAnimStateName ?? "<none>";
                string signature = pose.mode + "/" + pose.id + "/" + pose.nameAnimation
                    + "/" + state + "/" + femaleClips + "/" + maleClips;
                if (signature == _lastMotionSignature) return;
                _lastMotionSignature = signature;
                LogInfo("[HMotion] mode=" + pose.mode + " id=" + pose.id
                    + " pose=" + pose.nameAnimation + " state=" + state
                    + " click=" + flags.click + " speed=" + flags.speed.ToString("F3")
                    + " motion=" + flags.motion.ToString("F3")
                    + " female=" + femaleClips + " male=" + maleClips);
            }
            catch (Exception ex)
            {
                // A transient animator teardown should not interrupt H gameplay.
                Logger.LogWarning("[HMotion] sample failed: " + ex.GetType().Name + ": " + ex.Message);
                _nextMotionSample = Time.unscaledTime + 2f;
            }
        }

        private void HideCloneHandRenderers(HandCtrl owner)
        {
            if (owner == null) return;
            if (_hiddenHandOwner != null && _hiddenHandOwner != owner) RestoreHandRenderers();
            _hiddenHandOwner = owner;
            var items = ItemDictionaryField?.GetValue(owner) as Dictionary<int, HandCtrl.AibuItem>;
            if (items == null) return;
            int matched = 0;
            foreach (var pair in items)
            {
                var item = pair.Value;
                // isMaterialGet marks the game-owned hand mesh; leave other love items visible.
                if (item == null || item.mHand == null) continue;
                matched++;
                HideRenderer(item.renderBody);
                HideRenderer(item.meshRenderBody);
                HideRenderer(item.renderSilhouette);
                HideRenderer(item.meshRenderSilhouette);
            }
            if (matched == 0 && !_loggedNoHandMeshes)
            {
                Logger.LogWarning("[GroinPose] no hand meshes identified in HandCtrl");
                _loggedNoHandMeshes = true;
            }
        }

        private void HideRenderer(Renderer renderer)
        {
            if (renderer == null) return;
            if (!_hiddenHandRenderers.ContainsKey(renderer))
            {
                _hiddenHandRenderers.Add(renderer, renderer.enabled);
                LogInfo("[GroinPose] hide hand renderer " + renderer.name);
            }
            renderer.enabled = false;
        }

        private void RestoreHandRenderers()
        {
            foreach (var pair in _hiddenHandRenderers)
            {
                if (pair.Key != null)
                {
                    pair.Key.enabled = pair.Value;
                    LogInfo("[GroinPose] restore hand renderer " + pair.Key.name);
                }
            }
            _hiddenHandRenderers.Clear();
            _hiddenHandOwner = null;
            _loggedNoHandMeshes = false;
        }

        private static string DescribeAnimator(ChaControl character)
        {
            Animator animator = character != null ? character.animBody : null;
            if (animator == null) return "<none>";
            var sb = new StringBuilder();
            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                AnimatorClipInfo[] current = animator.GetCurrentAnimatorClipInfo(layer);
                AnimatorClipInfo[] next = animator.IsInTransition(layer)
                    ? animator.GetNextAnimatorClipInfo(layer) : null;
                if ((current == null || current.Length == 0) && (next == null || next.Length == 0)) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append('L').Append(layer).Append('=');
                AppendClips(sb, current);
                if (next != null && next.Length > 0)
                {
                    sb.Append("->");
                    AppendClips(sb, next);
                }
            }
            return sb.Length == 0 ? "<no-clips>" : sb.ToString();
        }

        private static void AppendClips(StringBuilder sb, AnimatorClipInfo[] clips)
        {
            if (clips == null || clips.Length == 0) { sb.Append("<none>"); return; }
            for (int i = 0; i < clips.Length; i++)
            {
                if (i > 0) sb.Append('+');
                sb.Append(clips[i].clip != null ? clips[i].clip.name : "<null>");
            }
        }

        [HarmonyPatch(typeof(HSceneProc), "CreateListAnimationFileName")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void AddCloneEntries(MonoBehaviour __instance)
        {
            TraceHotelClone("clone.catalog-hook.enter", __instance);
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed) return;
            var lists = AccessTools.Field(__instance.GetType(), "lstUseAnimInfo")?.GetValue(__instance) as List<HSceneProc.AnimationListInfo>[];
            if (lists == null || lists.Length <= (int)HFlag.EMode.aibu) return;
            var aibu = lists[(int)HFlag.EMode.aibu];
            EnsureCloneEntries(aibu);
            TraceHotelClone("clone.catalog-hook.after", __instance);
        }

        [HarmonyPatch(typeof(HSprite), "LoadMotionList")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static void PrepareDisplayedCloneEntries(
            [HarmonyArgument(0)] List<HSceneProc.AnimationListInfo> entries)
        {
            TraceHotelClone("clone.ui-list.enter", entries);
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed) return;
            // StartH callbacks can replace the list after our start callback.
            // Modify the exact list consumed by the UI, after those replacements.
            EnsureCloneEntries(entries);
        }

        public void PrepareHotelRoomPoseCatalog(HSceneProc scene)
        {
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed || scene == null || !ReferenceEquals(_observedScene?.Source, scene))
                throw new InvalidOperationException("クローン体位の許可セッションと復元先シーンが一致しません。");
            AddCloneEntries(scene);
        }

        private static void EnsureCloneEntries(List<HSceneProc.AnimationListInfo> aibu)
        {
            TraceHotelClone("clone.insert.before", aibu);
            PoseCloneList.AddMissing(aibu, clone => _instance?._sessionClones.Add(clone));
            TraceHotelClone("clone.insert.after", aibu);
        }
        private static void TraceHotelClone(string stage, object source)
        {
            if (_instance == null || !Chainloader.PluginInfos.TryGetValue("com.kks.maingame.aishoujomode", out var hotel) || hotel.Instance == null) return;
            if (!hotel.Instance.Config.TryGetEntry<bool>("HotelMode", "DiagnosticsEnabled", out var enabled) || !enabled.Value) return;
            try
            {
                var trace = hotel.Instance.GetType().GetMethod("TraceHotelPoseCatalog", BindingFlags.Public | BindingFlags.Instance);
                if (trace == null) throw new MissingMethodException("ホテル体位診断APIがありません。");
                trace.Invoke(hotel.Instance, new[] { (object)stage, source, CanonPlugins.Shared.PluginSessionGate.Allowed });
            }
            catch (Exception ex) { _instance.Logger.LogWarning("HOTEL_POSE_CATALOG_DIAGNOSTIC_FAILED " + ex.GetBaseException().Message); }
        }

        private static bool IsClone(HSceneProc.AnimationListInfo info)
        {
            return PoseCloneList.IsClone(info);
        }

        private void EndGateSession()
        {
            RestoreHandRenderers();
            var lists = _observedScene?.Read<List<HSceneProc.AnimationListInfo>[]>("lstUseAnimInfo");
            if (lists != null)
                foreach (var list in lists) list?.RemoveAll(item => _sessionClones.Contains(item));
            _sessionClones.Clear();
            _observedScene = null;
            _changingToPose = null;
            _wasClone = false;
            _orgasmAfterStarted = -1f;
        }

        [HarmonyPatch(typeof(HSprite), "OnChangePlaySelect")]
        [HarmonyPrefix]
        private static bool SelectSameAnimationVariant(HSprite __instance, GameObject objClick)
        {
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed) return true;
            if (objClick == null || __instance?.flags?.nowAnimationInfo == null) return true;
            var toggle = objClick.GetComponent<Toggle>();
            if (toggle != null && !toggle.interactable) return true;
            var component = objClick.GetComponent<HSprite.AnimationInfoComponent>();
            var selected = component?.info;
            var current = __instance.flags.nowAnimationInfo;
            if (selected == null || selected.mode != current.mode || selected.id != current.id
                || ReferenceEquals(selected, current) || (!IsClone(selected) && !IsClone(current))) return true;

            // The stock click handler returns early when mode/id are equal.
            // Queue the normal position-change action so switching between the
            // original entry and its clone still selects the clicked entry.
            __instance.flags.selectAnimationListInfo = selected;
            __instance.flags.voiceWait = true;
            __instance.flags.click = HFlag.ClickKind.actionChange;
            return false;
        }
    }
}
