using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    // Suspend only the competing cursor feature, once at entry; preserve its saved configuration.
    internal sealed class AiModeCursorScope : IDisposable
    {
        private readonly object _feature;
        private readonly FieldInfo _enabled;
        private readonly bool _previous;
        private readonly Action<string> _log;
        private readonly Func<bool> _diagnostics;
        private bool Diagnostics => _diagnostics == null || _diagnostics();
        private readonly Harmony _idleGate;
        private readonly MethodInfo _idleUnlock;
        private static Action<object> _firstIdleRequest;
        private bool _disposed;
        internal AiModeCursorScope(Action<string> log, Func<bool> diagnostics = null)
        {
            _log = log; _diagnostics = diagnostics;
            try
            {
            var captureType = AccessTools.TypeByName("MainGameUiInputCapture.UiInputCaptureRuntime");
            _idleUnlock = captureType == null ? null : AccessTools.Method(captureType, "ApplyIdleCursorUnlock");
            if (_idleUnlock == null) throw new InvalidOperationException("共有UIの待機カーソル解除経路を取得できません。AI少女モードのカーソル所有権を開始できません。");
            _idleGate = new Harmony(Plugin.Guid + ".idle-cursor-scope");
            _firstIdleRequest = !Diagnostics ? null : (Action<object>)(runtime =>
            {
                if (!Diagnostics) return;
                var owners = AccessTools.Field(runtime.GetType(), "_idleUnlockOwners")?.GetValue(runtime) as IEnumerable;
                log("AI_MODE_CURSOR_IDLE_SUPPRESSED owners=" + (owners == null ? "unavailable" : string.Join(",", owners.Cast<object>().Select(value => value.ToString()))) + "; explicit UI capture and camera release remain available");
            });
            // Ignore only idle release requests. Rect/drag capture and the mode's Tab/UI release remain intact.
            _idleGate.Patch(_idleUnlock, prefix: new HarmonyMethod(typeof(AiModeCursorScope), nameof(SkipIdleCursorUnlock)));
            if (Diagnostics) log("AI_MODE_CURSOR_SCOPE shared idle-only release suspended; pointer capture unchanged");
            if (Diagnostics) LogConfigurationManager(log);
            var type = AccessTools.TypeByName("RuntimeUnityEditor.Core.CursorUnlocker");
            if (type == null) { if (Diagnostics) log("AI_MODE_CURSOR_SCOPE RuntimeUnityEditor not installed"); return; }
            var parent = type.BaseType;
            var instance = parent?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            _feature = instance?.GetValue(null, null);
            _enabled = parent?.GetField("_enabled", BindingFlags.NonPublic | BindingFlags.Instance);
            if (_feature == null || _enabled == null) throw new InvalidOperationException("RuntimeUnityEditorのカーソル解除機能を停止できません。対応するInstance/_enabledがありません。");
            _previous = (bool)_enabled.GetValue(_feature);
            _enabled.SetValue(_feature, false);
            if (Diagnostics) log("AI_MODE_CURSOR_SCOPE suspended RuntimeUnityEditor.CursorUnlocker; previous=" + _previous + "; configuration unchanged");
            }
            catch
            {
                try { Dispose(); }
                catch (Exception cleanup) { if (Diagnostics) log("AI_MODE_CURSOR_SCOPE_CONSTRUCTOR_CLEANUP_ERROR " + cleanup); }
                throw;
            }
        }
        private static bool SkipIdleCursorUnlock(object __instance)
        {
            var report = _firstIdleRequest;
            if (report != null) { _firstIdleRequest = null; report(__instance); }
            return false;
        }
        private static void LogConfigurationManager(Action<string> log)
        {
            var type = AccessTools.TypeByName("ConfigurationManager.ConfigurationManager");
            if (type == null) return;
            var displayed = AccessTools.Property(type, "DisplayingWindow");
            var hotkey = AccessTools.Field(type, "OverrideHotkey");
            foreach (var manager in Resources.FindObjectsOfTypeAll(type))
                log("AI_MODE_CURSOR_CONFIGURATION_MANAGER assembly=" + type.Assembly.Location + " displayed=" + displayed?.GetValue(manager, null) + " overrideHotkey=" + hotkey?.GetValue(manager));
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _firstIdleRequest = null;
            try { _idleGate?.Unpatch(_idleUnlock, HarmonyPatchType.Prefix, _idleGate.Id); }
            finally
            {
                if (_feature != null && _enabled != null)
                {
                    if (!(bool)_enabled.GetValue(_feature)) _enabled.SetValue(_feature, _previous);
                    if (Diagnostics) _log("AI_MODE_CURSOR_SCOPE restored RuntimeUnityEditor.CursorUnlocker");
                }
                if (Diagnostics) _log("AI_MODE_CURSOR_SCOPE shared idle-only release restored");
            }
        }
    }
}
