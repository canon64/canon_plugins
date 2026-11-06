using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace UnityEngine
{
    public class GameObject { public bool Destroyed; }
    public class AudioSource
    {
        public readonly GameObject gameObject = new GameObject();
        public bool Playing;
        public void Stop() { Playing = false; }
    }
}
public class ChaControl
{
    public int Face;
    public float Motion;
    public void SetLipSync(object source) { }
    [MethodImpl(MethodImplOptions.NoInlining)] public void ChangeEyesPtn(int pattern) { Face = pattern; }
    [MethodImpl(MethodImplOptions.NoInlining)] public void setAnimatorParamFloat(string parameter, float value) { Motion = value; }
}
public class HFlag
{
    public float gaugeFemale, gaugeMale, potion = 1;
    public bool lockGugeFemale;
    public readonly VoiceData voice = new VoiceData();
    public object[] transVoiceMouth = { new object(), new object() };
    public class VoiceData { public int[] playVoices = { -1, -1 }; }
    [MethodImpl(MethodImplOptions.NoInlining)] public void FemaleGaugeUp(float _addPoint, bool _force)
    { if (!lockGugeFemale || _force) gaugeFemale += _addPoint * potion; }
}
public class HActionBase { protected ChaControl female; }
public class HandCtrl
{
    public ChaControl female;
    public HFlag Flags;
    public HandCtrl(ChaControl actor, HFlag flags) { female = actor; Flags = flags; }
    [MethodImpl(MethodImplOptions.NoInlining)] public void Stimulate(float amount) { Flags.FemaleGaugeUp(amount, false); }
    [MethodImpl(MethodImplOptions.NoInlining)] public void DirectWrite(float amount) { Flags.gaugeFemale += amount; }
    [MethodImpl(MethodImplOptions.NoInlining)] public void DetachAllItem() { }
}
public class HSprite
{
    public HFlag flags;
    [MethodImpl(MethodImplOptions.NoInlining)] public float Display() => flags.gaugeFemale;
}
public class HVoiceCtrl
{
    public enum VoiceKind { breath, breathShort, startvoice, voice }
    public class LinkInfo { public VoiceKind kind; }
    public class Voice { public VoiceKind state; public bool notOverWrite; public LinkInfo link = new LinkInfo(); }
    public Voice[] nowVoices = { new Voice(), new Voice() };
    public int Calls;
    public UnityEngine.AudioSource Source;
    public Manager.Voice.Loader Loader;
    [MethodImpl(MethodImplOptions.NoInlining)] public bool VoiceProc(ChaControl _female, int _main)
    { ++Calls; Manager.Voice.Play(Loader); return true; }
    [MethodImpl(MethodImplOptions.NoInlining)] public bool VoiceLinkProc(ChaControl _female, int _main)
    { ++Calls; return true; }
    [MethodImpl(MethodImplOptions.NoInlining)] public bool PlayVoice(ChaControl _female, int _voiceKind)
    { ++Calls; return true; }
}
namespace Manager
{
    public static class Voice
    {
        public class Loader { }
        public static int Stops;
        [MethodImpl(MethodImplOptions.NoInlining)] public static void Play(Loader loader) { }
        [MethodImpl(MethodImplOptions.NoInlining)] public static void Play_Standby(UnityEngine.AudioSource audioSource, Loader loader) { audioSource.Playing = true; }
        public static void Stop(object target) { ++Stops; }
    }
}
namespace MainGameVoiceFaceEventBridge
{
    public class Plugin
    {
        public static Plugin Instance { get; } = new Plugin();
        public ExternalVoicePlayer _externalVoicePlayer { get; } = new ExternalVoicePlayer();
    }
    public class ExternalVoicePlayer
    {
        public ChaControl _dialogueFemale, _aiLipActor, _pendingFemale, _sequenceFemale;
        public bool _loadPending, _pendingReady;
        public int _loadGeneration, DialogueStops, QueueClears, Calls, CueCalls;
        public object _pendingLock = new object(), _pendingPcm, _pendingError;
        [MethodImpl(MethodImplOptions.NoInlining)] public bool Play(string path, ChaControl female) { ++Calls; return true; }
        [MethodImpl(MethodImplOptions.NoInlining)] public bool StartLoad(ChaControl female) { ++Calls; return true; }
        [MethodImpl(MethodImplOptions.NoInlining)] public bool RequestImpactCue(ChaControl female) { ++CueCalls; return true; }
        public void ClearQueuedItems() { _sequenceFemale = null; ++QueueClears; }
        public void StopInternal(string reason, bool writeLog) { _dialogueFemale = null; ++DialogueStops; }
    }
}
namespace MainGameVrNeckGrab
{
    internal static class ReflectionAccess
    {
        internal static object Get(object o, string n) => o == null ? null : AccessTools.Field(o.GetType(), n)?.GetValue(o) ?? AccessTools.Property(o.GetType(), n)?.GetValue(o, null);
        internal static ChaControl Actor(object o) => Get(o, "female") as ChaControl;
    }
    internal sealed class DollSnapshot
    {
        internal static bool InternalWrite;
        internal bool TryMotion(out float value) { value = .52f; return true; }
    }
    public sealed partial class Plugin
    {
        public const string Guid = "NeckGrab.Tests";
        internal static Plugin Instance;
        internal bool Allowed = true;
        internal readonly HFlag _flags = new HFlag();
        internal readonly HVoiceCtrl _voice = new HVoiceCtrl();
        readonly Harmony _harmony = new Harmony(Guid);
        internal readonly List<ActorState> _actors = new List<ActorState>();
        internal readonly object _scene = new object();
        internal readonly NeckPressRouter Press = new NeckPressRouter();
        internal Func<object, bool> NativePress;
        bool RouteNativeController(object controller) => NativePress?.Invoke(controller) == true;
        bool NeckBlocksAutomaticKiss => Press.Actor(0) == 0 || Press.Actor(1) == 0;
        internal int Wakes, Shows, Removed;
        int _motionHash = 123;
        ActorState _displayed;
        internal sealed class ActorState
        {
            internal ChaControl Actor = new ChaControl();
            internal int NativeIndex;
            internal float FrozenGauge;
            internal DollSnapshot Doll;
            internal HandCtrl NativeHand;
            internal readonly NeckState State = new NeckState();
        }
        internal ActorState FindActor(ChaControl actor) => _actors.Find(a => ReferenceEquals(a.Actor, actor));
        void Wake(ActorState s) { ++Wakes; _displayed = null; }
        void ShowAwakening(ActorState s) { ++Shows; _displayed = s; }
        void RemoveNeckHand(ActorState s) { s.State.RemoveHand(); ++Removed; }
        static void Destroy(UnityEngine.GameObject go) { go.Destroyed = true; }
        internal void Install() { Instance = this; InstallHooks(); }
        void InstallOtherDollHook() { }
        internal void Uninstall() { _harmony.UnpatchSelf(); _pendingVoiceHarmony?.UnpatchSelf(); Instance = null; }
        internal ActorState AddActor()
        {
            var s = new ActorState { NativeIndex = _actors.Count }; s.NativeHand = new HandCtrl(s.Actor, _flags); _actors.Add(s); return s;
        }
        internal void Silence(ActorState s) => StopDialogue(s);
    }
}
namespace KKS_VR.Caress
{
    internal sealed class NativeInput
    {
        internal bool Down;
        [MethodImpl(MethodImplOptions.NoInlining)] public bool GetPressDown(int button) => Down;
    }
    internal sealed class CaressController
    {
        readonly object _controller;
        internal readonly NativeInput Input = new NativeInput();
        internal int NativeClicks;
        internal CaressController(object controller) { _controller = controller; }
        [MethodImpl(MethodImplOptions.NoInlining)] private void HandleTrigger()
        { if (Input.GetPressDown(1)) ++NativeClicks; }
        internal void Tick() => HandleTrigger();
    }
    internal sealed class Tracker { public object Proc { get; set; } }
    internal sealed class VRMouth
    {
        internal readonly Tracker _aibuTracker = new Tracker();
        internal int Kisses;
        [MethodImpl(MethodImplOptions.NoInlining)] private void StartKiss() { ++Kisses; }
        internal void TryKiss() => StartKiss();
    }
}
