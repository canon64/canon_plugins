using System;
using System.Collections.Generic;
using System.Reflection;

namespace MainGamePregnancyPlusBridge
{
    public sealed partial class Plugin
    {
        private readonly Dictionary<ChaControl, Tuple<object, object>> _sessionInflation = new Dictionary<ChaControl, Tuple<object, object>>();

        private bool CaptureInflation(ChaControl cha, object controller)
        {
            if (_sessionInflation.ContainsKey(cha)) return true;
            var field = _controllerType.GetField("infConfig", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var current = field?.GetValue(controller);
            var clone = current?.GetType().GetMethod("Clone", Type.EmptyTypes)?.Invoke(current, null);
            if (clone == null) { LogWarnOnce("snapshot-unavailable", "PregnancyPlus snapshot unavailable; refusing an unrestorable change"); return false; }
            _sessionInflation.Add(cha, Tuple.Create(controller, clone));
            return true;
        }

        private void EndGateSession()
        {
            _hasRuntimeInflationSizeOverride = false;
            ResetBellyPeakTracking();
            foreach (var pair in _sessionInflation)
            {
                if (pair.Key == null) continue;
                try
                {
                    var field = _controllerType.GetField("infConfig", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    field?.SetValue(pair.Value.Item1, pair.Value.Item2);
                    _meshInflateByDataMethod.Invoke(pair.Value.Item1, new object[] { pair.Value.Item2, "MainGamePregnancyPlusBridge-restore", null });
                }
                catch (Exception ex) { LogWarn("PregnancyPlus rollback failed: " + Unwrap(ex).Message); }
            }
            _sessionInflation.Clear();
            _bellyHSceneProc = null;
            _bellyFallbackSceneProc = null;
            _applyCharactersCache = null;
            _dirty = true;
        }
    }
}
