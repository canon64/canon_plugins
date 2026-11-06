using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using CanonGate;
using UnityEngine;

namespace CanonPlugins.Local
{
    internal static class SessionGate
    {
        private static IDisposable _registration;
        private static GateApi.Permit _permit;
        private static BaseUnityPlugin _owner;
        private static Action _stop;
        private static bool _active;
        private static ulong _sessionId;
        private static readonly Dictionary<string, FieldInfo> Fields = new Dictionary<string, FieldInfo>();
        internal static MonoBehaviour Scene { get; private set; }
        internal static HSceneProc HScene => Scene as HSceneProc;
        internal static ChaControl[] Actors { get; private set; } = new ChaControl[0];
        internal static bool Allowed => _active && _permit.IsValid && _owner != null && _owner.enabled && Scene != null;
        internal static ulong Generation { get; private set; }
        internal static GateApi.Permit Capture() => Allowed ? _permit : default(GateApi.Permit);
        internal static object ReadSceneField(string name) => Scene != null && Fields.TryGetValue(name, out var field) ? field.GetValue(Scene) : null;
        internal static T ReadScene<T>(string name) where T : class => ReadSceneField(name) as T;

        internal static void Bind(BaseUnityPlugin owner, string guid, Action start, Action stop)
        {
            if (_registration != null) throw new InvalidOperationException("Composer gate already registered");
            _owner = owner;
            _stop = stop;
            owner.enabled = false;
            _registration = GateApi.Register(guid, () => {
                var context = GateApi.Current;
                if (context == null || context.Kind != GateApi.SessionKind.H || context.Scene == null) return;
                if (_active && _sessionId == context.Id) return;
                Stop();
                Scene = context.Scene;
                _sessionId = context.Id;
                _permit = GateApi.Capture(guid);
                Fields.Clear();
                for (Type type = Scene.GetType(); type != null; type = type.BaseType)
                    foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        if (!Fields.ContainsKey(field.Name)) Fields.Add(field.Name, field);
                var actors = new List<ChaControl>();
                if (ReadSceneField("lstFemale") is IList females)
                    foreach (var value in females) if (value is ChaControl actor && actor != null && !actors.Contains(actor)) actors.Add(actor);
                foreach (string name in new[] { "male", "male1" })
                {
                    var actor = ReadScene<ChaControl>(name);
                    if (actor != null && !actors.Contains(actor)) actors.Add(actor);
                }
                Actors = actors.ToArray();
                _active = true;
                Generation++;
                owner.enabled = true;
                try { start(); }
                catch { Stop(); throw; }
            }, Stop);
        }

        internal static void Stop()
        {
            bool cleanup = _active;
            _active = false;
            _permit = default(GateApi.Permit);
            Generation++;
            try { if (cleanup) _stop?.Invoke(); }
            finally
            {
                Scene = null;
                Actors = new ChaControl[0];
                Fields.Clear();
                if (_owner != null) _owner.enabled = false;
            }
        }

        internal static void Cleanup(params Action[] actions)
        {
            Exception first = null;
            foreach (var action in actions)
                try { action(); } catch (Exception ex) { if (first == null) first = ex; }
            if (first != null) throw first;
        }

        internal static void Dispose()
        {
            var registration = _registration;
            _registration = null;
            try { Stop(); }
            finally { registration?.Dispose(); _owner = null; _stop = null; }
        }
    }
}
