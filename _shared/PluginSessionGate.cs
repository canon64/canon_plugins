using System;
using BepInEx;
using CanonGate;
using System.Reflection;

namespace CanonPlugins.Shared
{
    // Each plugin registers once. Only canon_gate decides the room at session start.
    internal static class PluginSessionGate
    {
        private static IDisposable _registration;
        private static GateApi.Permit _permit;
        private static GateApi.ActivityRegistration _activity;
        private static GateApi.ActivityPermit _activityPermit;
        internal static ChaControl AiActor { get; private set; }
        private static BaseUnityPlugin _owner;
        private static bool _starting;
        internal static bool Allowed => (_permit.IsValid || _activityPermit.IsValid) && _owner != null && (_starting || _owner.enabled);
        internal static HSceneProc HScene { get; private set; }
        internal static UnityEngine.MonoBehaviour Scene { get; private set; }
        internal static GateApi.SessionKind Kind { get; private set; }
        internal static GateApi.Permit Capture() => Allowed ? _permit : default;
        private static readonly System.Collections.Generic.Dictionary<string, FieldInfo> SceneFields = new System.Collections.Generic.Dictionary<string, FieldInfo>();
        internal static T ReadScene<T>(string name) where T : class => Scene != null && SceneFields.TryGetValue(name, out var field) ? field?.GetValue(Scene) as T : null;

        internal static void Bind(BaseUnityPlugin owner, string guid, Action start, Action stop)
        {
            if (_registration != null) throw new InvalidOperationException("Session already registered: " + guid);
            _owner = owner;
            owner.enabled = false;
            _activity = GateApi.RegisterActivity(guid, GateApi.ActivityKind.AiShoujoMode,
                () =>
                {
                    HScene = GateApi.Current?.HScene;
                    AiActor = GateApi.Current == null ? GateApi.AiShoujoMode?.Actor as ChaControl : null;
                    Scene = GateApi.Current?.Scene ?? GateApi.AiShoujoMode?.Owner;
                    SceneFields.Clear();
                    if (Scene != null)
                        foreach (var field in Scene.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                            SceneFields[field.Name] = field;
                    Kind = GateApi.Current?.Kind ?? GateApi.SessionKind.None;
                    _permit = GateApi.Capture(guid);
                    _activityPermit = _activity.Capture();
                    _starting = true;
                    try
                    {
                        start?.Invoke();
                        if (owner != null) owner.enabled = true;
                    }
                    finally { _starting = false; }
                },
                () =>
                {
                    _permit = default;
                    _activityPermit = default;
                    try { stop?.Invoke(); }
                    finally
                    {
                        HScene = null;
                        AiActor = null;
                        Scene = null;
                        SceneFields.Clear();
                        Kind = GateApi.SessionKind.None;
                        if (owner != null) owner.enabled = false;
                    }
                });
            _registration = _activity;
        }

        internal static void Dispose()
        {
            var registration = _registration;
            _registration = null;
            registration?.Dispose();
            _permit = default;
            HScene = null;
            Scene = null;
            SceneFields.Clear();
            Kind = GateApi.SessionKind.None;
            _owner = null;
        }
    }
}
