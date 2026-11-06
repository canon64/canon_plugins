using System;
using HarmonyLib;
using UnityEngine;

namespace MainGameHandySync
{
    internal sealed partial class Plugin
    {
        private IDisposable _rhythmSubscription;
        private readonly RhythmStrokeSource _rhythmSource = new RhythmStrokeSource();
        private string _rhythmMotionState;
        private HampTarget? _lastRhythmTarget;
        private bool UsingRhythmBpm => _rhythmSource.TryGetBpm(_cfgBpmStroke.Value, RhythmDivisor, out _);
        private bool UseHamp => _cfgHampEnabled.Value || _cfgBpmStroke.Value;

        private void SetupRhythmStroke()
        {
            _cfgLinked.SettingChanged += (_, __) => ApplyLinkedSetting();
            _cfgEnabled.SettingChanged += (_, __) => ApplyLinkedSetting();
            Config.SettingChanged += (_, args) =>
            {
                string section = args.ChangedSetting.Definition.Section;
                if (section.StartsWith("HAMP", StringComparison.Ordinal)) RefreshRhythmStroke();
            };
            _rhythmHooks = new Harmony(GUID + ".rhythm");
            _rhythmHooks.PatchAll(typeof(HandyMotionStatePatch));
        }

        private void ConnectRhythmStroke()
        {
            DisconnectRhythmStroke();
            _rhythmMotionState = _hFlag?.nowAnimStateName;
            // Optional one-time resolution, matching the existing prop integration.
            var api = Type.GetType("MainGameRhythmSync.MotionSyncApi, MainGameRhythmSync", false);
            var subscribe = api?.GetMethod("Subscribe", new[] { typeof(Action<bool, double, double>) });
            if (subscribe == null) return;
            try
            {
                _rhythmSubscription = (IDisposable)subscribe.Invoke(null,
                    new object[] { new Action<bool, double, double>(ReceiveRhythmStroke) });
            }
            catch (Exception ex)
            {
                if (ShouldLogError()) SimpleFileLogger.Warn("[hamp] RhythmSync接続失敗: " + ex.Message);
            }
        }

        private void DisconnectRhythmStroke()
        {
            _rhythmSubscription?.Dispose(); _rhythmSubscription = null;
            _rhythmSource.Reset(); _lastRhythmTarget = null;
            _rhythmMotionState = null;
        }

        private void ReceiveRhythmStroke(bool active, double phase, double rate)
        {
            if (!SessionGate.Allowed) return;
            // MotionSync rate is cycles per Unity scaled second; Handy uses wall time.
            if (!_rhythmSource.Receive(active, rate, Time.timeScale)) return;
            if (_cfgBpmStroke.Value) RefreshRhythmStroke();
        }

        private void ApplyLinkedSetting()
        {
            _externalMotionStopped = !_cfgLinked.Value;
            if (!SessionGate.Allowed) return;
            if (!_cfgEnabled.Value || !_cfgLinked.Value) ForceHampStopInternal();
            else
            {
                _handyClient.BeginSession();
                PrepareHampRestartInternal();
                RefreshRhythmStroke();
            }
        }

        private void RefreshRhythmStroke()
        {
            _lastRhythmTarget = null;
            if (!SessionGate.Allowed || !_cfgEnabled.Value || !_cfgLinked.Value || _externalMotionStopped ||
                !InHScene || _hFlag == null || !_handyClient.UsesV3) return;
            if (UseHamp)
            {
                if (_strokeCoroutine != null) { StopCoroutine(_strokeCoroutine); _strokeCoroutine = null; }
                TickHampBpm(true);
            }
            else StopHampIfRunning("mode-change");
        }

        internal void MotionStateChanged(string state)
        {
            if (!SessionGate.Allowed) return;
            if (_rhythmMotionState == state) return;
            _rhythmMotionState = state;
            if (UsingRhythmBpm) RefreshRhythmStroke();
        }

        internal int RhythmDivisor => HampTarget.NormalizeDivisor(_cfgRhythmDivisor.Value);
        internal void CycleRhythmDivisor() { _cfgRhythmDivisor.Value = HampTarget.NextDivisor(RhythmDivisor); }
    }

    [HarmonyPatch(typeof(HActionBase), "SetPlay", new[] { typeof(string), typeof(bool) })]
    internal static class HandyMotionStatePatch
    {
        [HarmonyPostfix]
        private static void Postfix([HarmonyArgument(0)] string state) { Plugin.Instance?.MotionStateChanged(state); }
    }
}
