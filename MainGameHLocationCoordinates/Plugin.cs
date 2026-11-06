using System;
using System.Threading;
using BepInEx;
using CanonGate;

namespace MainGameHLocationCoordinates
{
    [BepInPlugin(Guid, "MainGameHLocationCoordinates", "0.1.0")]
    [BepInDependency(GateApi.PluginGuid, "2.4.0")]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.kks.maingame.hlocationcoordinates";
        private IDisposable _registration;
        private void Awake()
        {
            CoordinatesApi.Initialize(Thread.CurrentThread.ManagedThreadId);
            _registration = GateApi.Register(Guid, CoordinatesApi.Begin, CoordinatesApi.End);
        }
        private void OnDisable() => CoordinatesApi.End();
        private void OnDestroy() { _registration?.Dispose(); CoordinatesApi.End(); }
    }

    public static class CoordinatesApi
    {
        private static int _thread;
        private static ulong _generation;
        private static GateApi.Permit _permit;
        private static bool _active;
        internal static void Initialize(int thread) { _thread = thread; }
        internal static void Begin()
        {
            _generation++;
            _active = GateApi.Current?.Kind == GateApi.SessionKind.H;
            _permit = GateApi.Capture(Plugin.Guid);
        }
        internal static void End() { _active = false; _generation++; _permit = default(GateApi.Permit); }
        private static void Check(ulong generation)
        {
            if (_thread != Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException("Unity main thread required");
            if (!_active || !_permit.IsValid || _generation != generation) throw new InvalidOperationException("H session expired or not permitted");
        }
        public static CoordinateSpace Register(RigidPose source)
        {
            ulong generation = _generation;
            Check(generation);
            return new CoordinateSpace(source, () => Check(generation));
        }
    }
}
