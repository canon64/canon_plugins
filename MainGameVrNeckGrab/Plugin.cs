using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using CanonGate;
using CanonPlugins.Shared;
using HarmonyLib;
using MainGameNotifications;
using UnityEngine;
using Valve.VR;
using VRGIN.Controls;
using VRGIN.Core;

namespace MainGameVrNeckGrab
{
    [BepInPlugin(Guid, "MainGameVrNeckGrab", "1.0.2")]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    [BepInDependency(GateApi.PluginGuid, "2.4.0")]
    [BepInDependency(NotificationApi.PluginGuid)]
    [BepInDependency("KKS_MainGameVR")]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.kks.maingame.vrneckgrab";
        internal static Plugin Instance;
        internal sealed class ActorState
        {
            internal ChaControl Actor;
            internal int NativeIndex;
            internal float FrozenGauge;
            internal HandCtrl NativeHand;
            internal Transform Neck;
            internal GameObject Lifetime, Hand;
            internal NeckSlot Slot;
            internal readonly NeckState State = new NeckState();
            internal Coroutine Deadline;
            internal DollSnapshot Doll;
        }
        readonly List<ActorState> _actors = new List<ActorState>();
        readonly Controller.Lock[] _locks = new Controller.Lock[2];
        readonly NeckPressRouter _pressRouter = new NeckPressRouter();
        readonly SteamVR_Behaviour_Pose[] _tracking = new SteamVR_Behaviour_Pose[2];
        SteamVR_Action_Boolean _trigger;
        SteamVR_Events.Action _focus;
        IDisposable _registration;
        GateApi.Permit _permit;
        MonoBehaviour _scene;
        HFlag _flags;
        HVoiceCtrl _voice;
        HSprite _sprite;
        Harmony _harmony;
        bool _active, _focused = true;
        ulong _lastSession;
        int _generation;
        int _motionHash;
        ConfigEntry<bool> _enabled, _diagnostics;
        ConfigEntry<float> _radius, _handScale;
        internal bool Allowed => _active && isActiveAndEnabled && _enabled.Value && _permit.IsValid;

        void Awake()
        {
            Instance = this;
            _enabled = Config.Bind("General", "Enabled", true, "VRの首掴み。再有効化は次のH開始から適用。");
            _diagnostics = Config.Bind("Diagnostics", "Enabled", false, "首掴みの操作・遷移・失敗ログ。OFFでは診断文字列も生成しない。");
            _radius = Config.Bind("Contact", "NeckRadiusCm", 12f, new ConfigDescription("トリガー押下時に首を優先する距離（cm）。押下時だけ判定。", new AcceptableValueRange<float>(2, 25)));
            _handScale = Config.Bind("Appearance", "HandScale", 1f, new ConfigDescription("愛撫の手の倍率。次の掴みから適用。", new AcceptableValueRange<float>(.5f, 2f)));
            _enabled.SettingChanged += EnabledChanged;
            _registration = GateApi.Register(Guid, BeginSession, EndSession);
        }
        void EnabledChanged(object sender, EventArgs e) { if (!_enabled.Value) EndSession(); }
        void BeginSession()
        {
            var c = GateApi.Current;
            if (!_enabled.Value || !isActiveAndEnabled || c == null || c.Kind != GateApi.SessionKind.H || c.Scene == null || c.Id == _lastSession || !VR.Active) return;
            _lastSession = c.Id; _permit = GateApi.Capture(Guid);
            if (!_permit.IsValid) return;
            _scene = c.Scene;
            _flags = ReflectionAccess.Get<HFlag>(_scene, "flags");
            _voice = ReflectionAccess.Get<HVoiceCtrl>(_scene, "voice");
            _sprite = ReflectionAccess.Get<HSprite>(_scene, "sprite");
            try
            {
                if (_flags == null || _voice == null || _sprite == null || VR.Mode == null)
                    throw new InvalidOperationException("VR Hのflags/voice/sprite/controllerが未準備です。");
                var females = ReflectionAccess.Get(_scene, "lstFemale") as IList;
                if (females == null) throw new InvalidOperationException("Hの女性一覧を取得できません。");
                _active = true; ++_generation;
                _motionHash = Animator.StringToHash("motion");
                for (int i = 0; i < females.Count; ++i)
                {
                    var actor = females[i] as ChaControl;
                    if (actor == null || actor.sex != 1) continue;
                    var s = new ActorState { Actor = actor, NativeIndex = i, NativeHand = ReflectionAccess.Get<HandCtrl>(_scene, i == 0 ? "hand" : "hand1") };
                    s.Neck = ReflectionAccess.Bone(actor, "cf_j_neck");
                    if (s.Neck == null || s.NativeHand == null) throw new InvalidOperationException("首の骨または標準愛撫の手がありません。");
                    _actors.Add(s);
                    s.Lifetime = new GameObject("NeckGrab lifetime");
                    s.Lifetime.transform.SetParent(s.Neck, false);
                    s.Lifetime.AddComponent<NeckLifetime>().Initialize(this, s);
                    s.Slot = NeckSlot.Create(_sprite, _actors.Count - 1, () => RemoveNeckHand(s));
                }
                _harmony = new Harmony(Guid); InstallHooks(); ConnectInput();
                Trace(() => "session start actors=" + _actors.Count);
            }
            catch (Exception e) { EndSession(); Fail("開始失敗", e); }
        }
        internal ActorState FindActor(ChaControl actor) => _actors.Find(s => ReferenceEquals(s.Actor, actor));
        internal void ActorUnavailable(ActorState s)
        {
            if (!_active) return;
            RemoveNeckHand(s); s.Doll?.Restore(); s.Doll = null; s.State.Clear();
            HideAwakening(s);
        }
        void ConnectInput()
        {
            _trigger = SteamVR_Actions.legacy_emulate.Axis1_Press;
            if (_trigger == null) throw new InvalidOperationException("VRトリガー入力がありません。");
            foreach (var source in new[] { SteamVR_Input_Sources.LeftHand, SteamVR_Input_Sources.RightHand })
            {
                _trigger.AddOnStateDownListener(TriggerDown, source);
                _trigger.AddOnStateUpListener(TriggerUp, source);
                _trigger.AddOnActiveChangeListener(TriggerActive, source);
            }
            for (int i = 0; i < 2; ++i)
            {
                _tracking[i] = (i == 0 ? VR.Mode.Left : VR.Mode.Right)?.Tracking;
                if (_tracking[i] == null) continue;
                _tracking[i].onConnectedChangedEvent += Connected;
                _tracking[i].onTrackingChangedEvent += TrackingChanged;
            }
            _focused = true;
            _focus = SteamVR_Events.InputFocusAction(f => { _focused = f; if (!f) CancelHolds(); }); _focus.enabled = true;
        }
        static int Index(SteamVR_Input_Sources source) => source == SteamVR_Input_Sources.LeftHand ? 0 : source == SteamVR_Input_Sources.RightHand ? 1 : -1;
        void TriggerDown(SteamVR_Action_Boolean a, SteamVR_Input_Sources source)
        {
            RouteNeckPress(Index(source));
        }
        bool RouteNativeController(object nativeController)
        {
            if (VR.Mode == null || !(nativeController is Controller controller)) return false;
            return RouteNeckPress(controller == VR.Mode.Left ? 0 : controller == VR.Mode.Right ? 1 : -1);
        }
        bool NeckBlocksAutomaticKiss => _actors.Where((s, i) => s.NativeIndex == 0 &&
            (s.State.Owner >= 0 || _pressRouter.Actor(0) == i || _pressRouter.Actor(1) == i)).Any();
        bool RouteNeckPress(int i)
        {
            if (i < 0 || !Allowed || !_focused || VR.Mode == null) return false;
            var source = i == 0 ? SteamVR_Input_Sources.LeftHand : SteamVR_Input_Sources.RightHand;
            double stamp = _trigger.GetTimeLastChanged(source);
            if (_pressRouter.Captured(i, stamp)) return true;
            // A genuinely new Down recovers an Up that was missed, including presses outside
            // the neck radius. The native and Steam callbacks for the same edge share a stamp.
            foreach (var old in _actors.Where(s => s.State.Owner == i).ToArray()) ReleaseHold(old);
            var controller = i == 0 ? VR.Mode.Left : VR.Mode.Right;
            if (controller == null || !controller.IsTracking) return false;
            Vector3 point = controller.transform.position;
            double[] distances = _actors.Select(s => s.Neck != null && s.Actor != null
                ? (double)(point - s.Neck.position).sqrMagnitude : double.PositiveInfinity).ToArray();
            int nearest = NeckProximity.Closest(distances, _radius.Value / 100.0);
            return _pressRouter.Capture(i, nearest, () => BeginNeckHold(i, controller, _actors[nearest]), stamp);
        }
        void BeginNeckHold(int i, Controller controller, ActorState target)
        {
            if (target.State.Owner >= 0) return;
            if (OtherDollActive(target.Actor)) return;
            if (!VrItemFocus.TryAcquire(controller, out var focus))
            {
                Trace(() => "neck priority rejected hand=" + i + " owner=" + VrItemFocus.DescribeCurrent(controller));
                return;
            }
            _locks[i] = focus;
            bool hadHand = target.Hand != null;
            try
            {
                if (!target.State.Begin(i, Time.realtimeSinceStartup)) throw new InvalidOperationException("首は別の手が保持中です。");
                StopDialogue(target);
                if (target.NativeHand.IsKissAction()) target.NativeHand.ForceFinish(false);
                EnsureHand(target, i);
                target.Slot.SetPlaced(true);
                long ticket = target.State.Ticket; int generation = _generation;
                if (!target.State.Doll) target.Deadline = StartCoroutine(Deadline(target, ticket, generation));
                Trace(() => "hold begin hand=" + i + " actor=" + target.Actor.GetInstanceID());
            }
            catch (Exception e)
            {
                try { if (hadHand) ReleaseHold(target); else RemoveNeckHand(target); }
                finally { var remaining = _locks[i]; _locks[i] = null; remaining?.Release(); }
                Fail("掴み開始失敗", e);
            }
        }
        IEnumerator Deadline(ActorState s, long ticket, int generation)
        {
            yield return new WaitForSecondsRealtime((float)NeckState.HoldSeconds);
            s.Deadline = null;
            if (!Allowed || generation != _generation || s.Actor == null || !s.State.CanEnter(ticket, Time.realtimeSinceStartup) || OtherDollActive(s.Actor)) yield break;
            try
            {
                s.FrozenGauge = _flags.gaugeFemale;
                s.Doll = new DollSnapshot(s.Actor, _flags, LoadDollSettings(), ReportAppearance);
                s.Doll.Apply(); s.State.EnterDoll(); ShowAwakening(s);
                Trace(() => "doll enter actor=" + s.Actor.GetInstanceID());
            }
            catch (Exception e) { s.Doll?.Restore(); s.Doll = null; ReleaseHold(s); Fail("人形化失敗", e); }
        }
        void TriggerUp(SteamVR_Action_Boolean a, SteamVR_Input_Sources source) => CancelHand(Index(source));
        void TriggerActive(SteamVR_Action_Boolean a, SteamVR_Input_Sources source, bool active) { if (!active) CancelHand(Index(source)); }
        void Connected(SteamVR_Behaviour_Pose p, SteamVR_Input_Sources source, bool connected) { if (!connected) CancelHand(Index(source)); }
        void TrackingChanged(SteamVR_Behaviour_Pose p, SteamVR_Input_Sources source, ETrackingResult result) { if (result != ETrackingResult.Running_OK) CancelHand(Index(source)); }
        void CancelHand(int i) { _pressRouter.Release(i); foreach (var s in _actors.Where(s => i >= 0 && s.State.Owner == i).ToArray()) ReleaseHold(s); }
        void CancelHolds() { _pressRouter.Clear(); foreach (var s in _actors.ToArray()) ReleaseHold(s); }
        void ReleaseHold(ActorState s)
        {
            int i = s.State.Owner;
            s.State.End(i);
            if (s.Deadline != null) { StopCoroutine(s.Deadline); s.Deadline = null; }
            if (i < 0) return;
            var focus = _locks[i]; _locks[i] = null;
            try { Trace(() => "hold release hand=" + i); }
            finally { focus?.Release(); }
        }
        void RemoveNeckHand(ActorState s)
        {
            ReleaseHold(s); s.State.RemoveHand();
            if (s.Hand != null) { s.Hand.SetActive(false); Destroy(s.Hand); s.Hand = null; }
            s.Slot?.SetPlaced(false);
        }
        void Wake(ActorState s)
        {
            var clean = new CleanupBatch();
            // AddAwakening clears Owner, so release the recorded controller lock explicitly.
            foreach (int i in new[] { 0, 1 })
                if (_locks[i] != null && !_actors.Any(a => a.State.Owner == i))
                { var focus = _locks[i]; _locks[i] = null; clean.Run("操作権", () => focus.Release()); }
            var doll = s.Doll; s.Doll = null;
            clean.Run("人形復元", () => doll?.Restore()); clean.Run("覚醒表示", () => HideAwakening(s));
            ReportRestore(clean.Errors);
            Trace(() => "awake actor=" + s.Actor.GetInstanceID());
        }
        void EndSession()
        {
            _active = false; _permit = default; ++_generation;
            _pressRouter.Clear();
            var errors = new List<string>();
            Action<string, Action> clean = (name, action) => { try { action(); } catch (Exception e) { errors.Add(name + ": " + e.Message); } };
            clean("入力購読", DisconnectInput);
            foreach (var s in _actors.ToArray())
            {
                clean("掴み解除", () => ReleaseHold(s));
                clean("人形復元", () => s.Doll?.Restore()); s.Doll = null; s.State.Clear();
                clean("首の手", () => { if (s.Hand != null) { s.Hand.SetActive(false); Destroy(s.Hand); } });
                clean("首の寿命通知", () => { if (s.Lifetime != null) { s.Lifetime.SetActive(false); Destroy(s.Lifetime); } });
                clean("部位スロット", () => s.Slot?.Dispose());
            }
            clean("覚醒表示", DisposeAwakening);
            clean("パッチ", () => _harmony?.UnpatchSelf()); _harmony = null;
            _gaugeActor = null; _nativeDialogueActor = null;
            foreach (var ticket in _nativeVoiceTickets) ticket.Cancelled = true;
            _nativeVoiceTickets.Clear();
            StopAllCoroutines(); _actors.Clear(); _scene = null; _flags = null; _voice = null; _sprite = null;
            if (errors.Count > 0) ReportRestore(errors);
        }
        void DisconnectInput()
        {
            var clean = new CleanupBatch();
            var trigger = _trigger; _trigger = null;
            foreach (var source in new[] { SteamVR_Input_Sources.LeftHand, SteamVR_Input_Sources.RightHand })
            {
                clean.Run("押下購読", () => trigger?.RemoveOnStateDownListener(TriggerDown, source));
                clean.Run("解放購読", () => trigger?.RemoveOnStateUpListener(TriggerUp, source));
                clean.Run("入力状態購読", () => trigger?.RemoveOnActiveChangeListener(TriggerActive, source));
            }
            foreach (var pose in _tracking)
            {
                if (pose == null) continue;
                clean.Run("接続購読", () => pose.onConnectedChangedEvent -= Connected);
                clean.Run("追跡購読", () => pose.onTrackingChangedEvent -= TrackingChanged);
            }
            Array.Clear(_tracking, 0, 2);
            var inputFocus = _focus; _focus = null;
            clean.Run("フォーカス購読", () => { if (inputFocus != null) inputFocus.enabled = false; });
            for (int i = 0; i < 2; ++i) { var focus = _locks[i]; _locks[i] = null; clean.Run("操作権", () => focus?.Release()); }
            if (clean.Errors.Count > 0) throw new InvalidOperationException(string.Join("\n", clean.Errors));
        }
        internal void Trace(Func<string> message) { if (_diagnostics.Value) Logger.LogInfo(message()); }
        internal void Fail(string title, Exception error)
        {
            if (_diagnostics.Value) Logger.LogWarning(title + ": " + error);
            NotificationApi.Publish(Guid, "首掴み: " + title, error.Message, NotificationSeverity.Failure);
        }
        void ReportRestore(List<string> errors)
        {
            if (errors.Count > 0) NotificationApi.Publish(Guid, "首掴み: 一部復元失敗", "他の項目は復元処理を継続しました。\n" + string.Join("\n", errors), NotificationSeverity.Failure);
        }
        void ReportAppearance(string operation, List<string> errors)
        {
            if (errors.Count > 0) NotificationApi.Publish(Guid, "首掴み: " + operation, string.Join("\n", errors), NotificationSeverity.Failure);
        }
        void OnDisable() => EndSession();
        void OnDestroy()
        {
            try { _registration?.Dispose(); EndSession(); _pendingVoiceHarmony?.UnpatchSelf(); }
            finally { _enabled.SettingChanged -= EnabledChanged; if (Instance == this) Instance = null; }
        }
    }
    internal sealed class NeckLifetime : MonoBehaviour
    {
        Plugin _owner; Plugin.ActorState _actor;
        internal void Initialize(Plugin owner, Plugin.ActorState actor) { _owner = owner; _actor = actor; }
        void OnDisable() { if (_owner != null && _actor != null) _owner.ActorUnavailable(_actor); }
    }
}
