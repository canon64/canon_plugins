using System;
using BepInEx;
using CanonGate;

namespace CanonPlugins.Shared
{
    // Compiled into each owner: registration and cached lifecycle only, no scene polling.
    internal static class PluginActivityGate
    {
        private static GateApi.ActivityRegistration _registration;
        private static BaseUnityPlugin _owner;
        private static bool _starting;
        internal static bool Allowed => _registration != null && _registration.IsActive && _owner != null && (_starting || _owner.enabled);
        internal static UnityEngine.MonoBehaviour Scene => _registration?.Scene;
        internal static GateApi.ActivityPermit Capture() => Allowed ? _registration.Capture() : default;
        internal static void Bind(BaseUnityPlugin owner, string guid, GateApi.ActivityKind kind, Action start, Action stop, bool keepBootstrapEnabled = false)
        {
            _owner = owner;
            if (!keepBootstrapEnabled) owner.enabled = false;
            _registration = GateApi.RegisterActivity(guid, kind,
                () => { _starting = true; try { start(); if (owner != null) owner.enabled = true; } finally { _starting = false; } },
                () => { try { stop(); } finally { if (owner != null && !keepBootstrapEnabled) owner.enabled = false; } });
        }
        internal static void Begin(UnityEngine.MonoBehaviour scene = null) => _registration?.Begin(scene);
        internal static void End() => _registration?.End();
        internal static void Dispose()
        {
            _registration?.Dispose(); _registration = null;
            if (_owner != null) _owner.enabled = false;
            _owner = null;
        }
    }
}
