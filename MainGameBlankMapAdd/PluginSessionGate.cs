using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using CanonGate;
using UnityEngine;

namespace CanonPlugins.Shared
{
    // BlankMapAdd provides the room identity that canon_gate inspects before grants.
    // Keep its bootstrap enabled; only the scene runtime is controlled by the permit.
    internal static class PluginSessionGate
    {
        private static IDisposable _registration;
        private static GateApi.Permit _permit;
        private static BaseUnityPlugin _owner;
        private static readonly Dictionary<string, FieldInfo> SceneFields = new Dictionary<string, FieldInfo>();
        internal static bool Allowed => _permit.IsValid && _owner != null && _owner.isActiveAndEnabled;
        internal static HSceneProc HScene { get; private set; }
        internal static MonoBehaviour Scene { get; private set; }
        internal static GateApi.SessionKind Kind { get; private set; }
        internal static GateApi.Permit Capture() => Allowed ? _permit : default;
        internal static T ReadScene<T>(string name) where T : class =>
            Scene != null && SceneFields.TryGetValue(name, out var field) ? field?.GetValue(Scene) as T : null;

        internal static void Bind(BaseUnityPlugin owner, string guid, Action start, Action stop)
        {
            if (_registration != null) throw new InvalidOperationException("Already registered: " + guid);
            _owner = owner;
            _registration = GateApi.Register(guid,
                () =>
                {
                    var context = GateApi.Current;
                    if (context == null || context.Kind != GateApi.SessionKind.H || !owner.isActiveAndEnabled) return;
                    HScene = context.HScene;
                    Scene = context.Scene;
                    Kind = context.Kind;
                    SceneFields.Clear();
                    if (Scene != null)
                        foreach (var field in Scene.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                            SceneFields[field.Name] = field;
                    _permit = GateApi.Capture(guid);
                    start();
                },
                () =>
                {
                    _permit = default;
                    try { stop(); }
                    finally { ClearScene(); }
                });
        }

        private static void ClearScene()
        {
            HScene = null;
            Scene = null;
            SceneFields.Clear();
            Kind = GateApi.SessionKind.None;
        }

        internal static void Dispose()
        {
            var registration = _registration;
            _registration = null;
            try { registration?.Dispose(); }
            finally { _permit = default; _owner = null; ClearScene(); }
        }
    }
}
