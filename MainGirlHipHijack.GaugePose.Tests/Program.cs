using UnityEngine;
using Hip = MainGirlHipHijack.Plugin;
using Source = MainGameVrDildo.Plugin;

static class Program
{
    static int checks;
    internal static void Check(bool condition, string name) { checks++; if (!condition) throw new Exception(name); }
    static void Near(float a, float b, string name) => Check(Math.Abs(a - b) < .0001f, name + $": {a} != {b}");
    static (Hip, Source) Setup(bool running = true)
    {
        Hip.EndGaugePoseBlend();
        CanonPlugins.Local.SessionGate.Allowed = CanonPlugins.Shared.PluginSessionGate.Allowed = true;
        var actor = new ChaControl(); var hip = new Hip(actor, running); var source = new Source(actor);
        BepInEx.Bootstrap.Chainloader.PluginInfos["com.kks.maingame.vrdildo"] = new() { Instance = source };
        return (hip, source);
    }
    static void Main()
    {
        var (hip, source) = Setup();
        Check(Hip.BeginGaugePoseBlend("a", "b"), "saved endpoints start");
        Near(hip.X, 2, "0% position"); Near(hip.W, .2f, "0% weight");
        source.Sample(.5f); Near(hip.X, 6, "50% position"); Near(hip.W, .5f, "50% weight");
        Near(hip.Angle, 60, "50% quaternion rotation");
        int updates = hip.WeightWrites; source.Sample(.5f); Check(updates == hip.WeightWrites, "unchanged source sends no update");
        source.Sample(1); Near(hip.X, 10, "100% position"); Near(hip.W, .8f, "100% weight");
        source.Sample(.25f); Near(hip.X, 4, "descending position"); Near(hip.W, .35f, "descending weight");
        source.Sample(0); Near(hip.X, 2, "descending to zero");
        Hip.EndGaugePoseBlend(); Near(hip.X, 1, "restore pre-start position"); Near(hip.W, .4f, "restore pre-start weight");
        Check(hip.FollowRestored && hip.Running, "restore follow and running state");
        Check(hip.Saves == 0 && hip.SettingsUntouched, "blend does not write persisted settings");
        source.Sample(1); Near(hip.X, 1, "unsubscribe on stop");

        (hip, source) = Setup(false);
        Check(Hip.BeginGaugePoseBlend("", "b"), "unsaved animated start");
        Near(hip.W, 0, "uncontrolled start contributes zero");
        source.Sample(.5f); Near(hip.W, .4f, "animated to target weight"); Near(hip.X, 10, "target position used with partial IK influence");
        source.Sample(1); Near(hip.W, .8f, "unsaved to target full weight");
        source.End(); Check(!hip.Active && !hip.Running, "source stop restores disabled IK");
        Near(hip.W, .4f, "source stop restores runtime weight");
        Check(hip.SettingsUntouched, "unsaved path settings unchanged");

        (hip, source) = Setup();
        Check(Hip.BeginGaugePoseBlend("", "b"), "empty source initial start");
        source.End(); Check(!hip.Active, "source ends before first publication");
        source.Sample(.9f); Near(hip.X, 1, "old source callback rejected");
        Check(Hip.BeginGaugePoseBlend("a", "b"), "explicit restart"); Near(hip.X, 9.2f, "restart samples current gauge");
        Check(!Hip.SetGaugePoseBlendWeight(float.NaN), "NaN rejected");
        CanonPlugins.Shared.PluginSessionGate.Allowed = false;
        Check(!Hip.SetGaugePoseBlendWeight(.8f) && !hip.Active, "source permit revoked");
        Near(hip.X, 1, "restore after source permission loss");

        (hip, source) = Setup();
        CanonPlugins.Local.SessionGate.Allowed = false;
        Check(!Hip.BeginGaugePoseBlend("a", "b"), "consumer permission denies start");
        CanonPlugins.Local.SessionGate.Allowed = true;
        Check(Hip.BeginGaugePoseBlend("a", "b"), "consumer restarted");
        CanonPlugins.Local.SessionGate.Allowed = false;
        source.Sample(.6f); Check(!hip.Active, "consumer revoked notification cleanup"); Near(hip.X, 1, "revoked cleanup works");

        (hip, source) = Setup();
        hip.FailEnableIndex = 1;
        Check(!Hip.BeginGaugePoseBlend("a", "b"), "partial initialization failure reported");
        Check(!hip.Active && hip.Running && hip.OtherDisabled, "partial failure restores all points"); Near(hip.W, .4f, "partial failure weight restore");
        source.Sample(.5f); Near(hip.X, 1, "partial failure unsubscribed");
        Check(!Hip.BeginGaugePoseBlend("missing", "b"), "missing preset rejected");
        hip.WrongPosture = true; Check(!Hip.BeginGaugePoseBlend("a", "b"), "different posture rejected");

        (hip, source) = Setup();
        Check(Hip.BeginGaugePoseBlend("a", "b"), "invalid source lifecycle start");
        source.DisableGauge(); Check(!hip.Active, "gauge disabled ends blend");
        Console.WriteLine($"Gauge pose actual source/consumer boundary tests: PASS ({checks} assertions)");
    }
}

public sealed class ChaControl { }
public sealed class HFlag { public enum EMode { aibu, other } public EMode mode; }
namespace CanonPlugins.Local { public static class SessionGate { public static bool Allowed = true; } }
namespace CanonPlugins.Shared { public static class PluginSessionGate { public static bool Allowed = true; } }
namespace BepInEx.Bootstrap
{
    public sealed class Info { public object Instance; }
    public static class Chainloader { public static Dictionary<string, Info> PluginInfos = new(); }
}
namespace MainGameVrDildo
{
    public sealed partial class Plugin
    {
        internal static Plugin Instance;
        internal bool isActiveAndEnabled = true;
        internal Settings _settings = new(); internal object _model = new(); internal ChaControl _female;
        internal Scene _scene = new(); internal float _motionGaugeCm; internal Log Logger = new();
        internal sealed class Settings { public bool Enabled = true, MotionGaugeEnabled = true; public float MotionGaugeFullCm = 100; }
        internal sealed class Scene { public HFlag flags = new(); }
        internal sealed class Log { public void LogWarning(string s) => Console.WriteLine(s); }
        public Plugin(ChaControl actor) { Instance = this; _female = actor; }
        public void Sample(float value) { _motionGaugeCm = value * 100; NotifyGaugePoseChanged(); }
        public void End() => EndGaugePoseSource();
        public void DisableGauge() { _settings.MotionGaugeEnabled = false; NotifyGaugePoseChanged(); }
    }
}
namespace MainGirlHipHijack
{
    public sealed partial class Plugin
    {
        internal static Plugin Instance;
        const int BIK_TOTAL = 2, BIK_LS = 0, BIK_RS = 1;
        internal sealed class Settings { public string GaugePoseStartId = "", GaugePoseEndId = ""; public float[] Weights = { .4f, .4f }; }
        internal sealed class Female { public ChaControl Character; }
        internal sealed class Context { public int CurrentAnimationInfoId = 1, CurrentAnimationInfoMode = 0; public string CurrentAnimationInfoName = "aibu"; public bool HasCurrentAnimationInfo = true; }
        internal sealed class Point
        {
            public bool Running, PendingFollowRebind, PendingFollowHasPresetOffset, HasPostDragHold;
            public int PostDragHoldFrames;
            public Transform Proxy = new(), FollowBone, PendingFollowBone;
            public Vector3 FollowBonePositionOffset, PendingFollowPosOffset, PostDragHoldPos;
            public Quaternion FollowBoneRotationOffset, PendingFollowRotOffset, PostDragHoldRot;
        }
        private sealed class PosePresetEntryRuntime
        {
            public bool enabled, hasProxyPose = true, followIsMale;
            public float weight; public Vector3 proxyPosition; public Quaternion proxyRotation = Quaternion.identity;
        }
        private sealed class PosePresetRuntime
        {
            public string id, name; public int postureId = 1, postureMode;
            public PosePresetEntryRuntime[] entries = new PosePresetEntryRuntime[BIK_TOTAL];
            public bool hasFemaleHeadAdditive, hasFemaleHeadAngle;
            public Quaternion femaleHeadAdditiveOffset; public float femaleHeadAngleX, femaleHeadAngleY, femaleHeadAngleZ;
        }
        Settings _settings = new(); Female _femaleSession = new(); Context _hSceneSession = new();
        Point[] _bikEff = { new(), new() }; bool[] _bikWant = new bool[2]; float[] weights = { .4f, .4f };
        bool _bodyCtrlLinkEnabled, _vrGrabMode, _autoPosePendingApply, _autoPoseLoopReady, _abandonedByPostureChange, _pendingAbandonByPostureChange;
        int _autoPoseLoopCountSinceSwitch;
        Vector3 _vibeBodyApplied, _vibeLeftShoulderApplied, _vibeRightShoulderApplied;
        List<PosePresetRuntime> _posePresets = new(); Transform originalFollow = new();
        public int Saves, WeightWrites, FailEnableIndex = -1;
        public bool WrongPosture { set => _hSceneSession.CurrentAnimationInfoId = value ? 2 : 1; }
        public bool Active => _gaugePoseBlend != null;
        public float X => _bikEff[0].Proxy.position.x;
        public float W => weights[0];
        public float Angle => 2f * MathF.Acos(Math.Clamp(_bikEff[0].Proxy.rotation.Q.W, -1f, 1f)) * 180f / MathF.PI;
        public bool Running => _bikEff[0].Running;
        public bool OtherDisabled => !_bikEff[1].Running;
        public bool FollowRestored => _bikEff[0].FollowBone == originalFollow;
        public bool SettingsUntouched => _settings.Weights.All(w => w == .4f);
        public Plugin(ChaControl actor, bool running)
        {
            Instance = this; _femaleSession.Character = actor;
            _bikEff[0].Running = _bikWant[0] = running;
            _bikEff[0].Proxy.position = new(1, 0, 0); _bikEff[0].FollowBone = originalFollow;
            _posePresets.Add(Make("a", 2, .2f, 20)); _posePresets.Add(Make("b", 10, .8f, 100));
        }
        static PosePresetRuntime Make(string id, float x, float w, float angle)
        {
            var p = new PosePresetRuntime { id = id, name = id };
            for (int i = 0; i < BIK_TOTAL; i++) p.entries[i] = new() { enabled = true, weight = w, proxyPosition = new(x, 0, 0), proxyRotation = Quaternion.Euler(0, 0, angle) };
            return p;
        }
        bool IsPluginEnabled() => CanonPlugins.Local.SessionGate.Allowed;
        bool TryResolveRuntimeRefs() => true;
        bool IsChainShoulderHeld(int i) => false;
        void EnsurePosePresetsLoaded() { }
        void NormalizePosePreset(PosePresetRuntime p) { }
        void StopPoseTransitionIfRunning() { }
        void LogInfo(string s) { }
        void LogWarn(string s) => Console.WriteLine(s);
        void SaveSettings() => Saves++;
        PosePresetRuntime BuildCurrentPosePresetSnapshot(string name)
        {
            var p = Make("current", 1, weights[0], 0);
            for (int i = 0; i < BIK_TOTAL; i++) p.entries[i].enabled = _bikEff[i].Running;
            return p;
        }
        float GetBodyIKWeight(int i) => weights[i];
        void SetBodyIKWeight(int i, float w, bool saveSettings = true, bool updateSettings = true)
        { weights[i] = w; WeightWrites++; if (updateSettings) _settings.Weights[i] = w; if (saveSettings) Saves++; }
        void ClearBodyIKFollowBone(int i) { _bikEff[i].FollowBone = null; _bikEff[i].PendingFollowRebind = false; }
        void DoEnableBodyIK(int i) { if (i == FailEnableIndex) throw new Exception("injected initialization failure"); _bikEff[i].Running = true; }
        void DoDisableBodyIK(int i, bool silent = false, bool preserveProxy = false, bool immediateShoulder = false) => _bikEff[i].Running = false;
        Transform GetMalePosePresetRootTransform() => null;
        Transform GetPosePresetRootTransform() => null;
        bool TryResolvePosePresetFollowBinding(int i, Transform root, PosePresetEntryRuntime p, out Transform bone, out Vector3 offset, out Quaternion rot)
        { bone = null; offset = Vector3.zero; rot = Quaternion.identity; return false; }
        Quaternion GetFollowOffsetRotation(Transform bone) => bone.rotation;
        void ApplyConfiguredFemaleHandPoses() { }
    }
}
namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float a, float b, float c) { x = a; y = b; z = c; }
        public static Vector3 zero => new();
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => new(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
    }
    public struct Quaternion
    {
        internal System.Numerics.Quaternion Q;
        public static Quaternion identity => new() { Q = System.Numerics.Quaternion.Identity };
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => new() { Q = System.Numerics.Quaternion.Slerp(a.Q, b.Q, t) };
        public static Quaternion Euler(float x, float y, float z) => new() { Q = System.Numerics.Quaternion.CreateFromYawPitchRoll(y * MathF.PI / 180, x * MathF.PI / 180, z * MathF.PI / 180) };
        public static Quaternion operator *(Quaternion a, Quaternion b) => new() { Q = a.Q * b.Q };
        public static Vector3 operator *(Quaternion a, Vector3 b) { var v = System.Numerics.Vector3.Transform(new(b.x, b.y, b.z), a.Q); return new(v.X, v.Y, v.Z); }
    }
    public sealed class Transform { public Vector3 position; public Quaternion rotation = Quaternion.identity; public void SetPositionAndRotation(Vector3 p, Quaternion r) { position = p; rotation = r; } }
    public static class Mathf { public static float Clamp01(float v) => Math.Clamp(v, 0, 1); public static float Lerp(float a, float b, float t) => a + (b - a) * t; }
    public static class GUI { public static bool enabled = true; }
    public static class GUILayout
    {
        public static void Space(float x) { } public static void Label(string s) { }
        public static bool Button(string s) => false; public static bool Toggle(bool b, string s) => b;
        public static void BeginHorizontal() { } public static void EndHorizontal() { }
    }
}
