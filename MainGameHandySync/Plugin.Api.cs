namespace MainGameHandySync
{
    /// <summary>
    /// 外部プラグインから動作の開始/停止を切り替えるための公開API。
    /// リフレクション経由で型解決される前提（DLL参照は不要）。
    ///
    /// 動作方式とは独立した「連動」設定。OFF時は直ちに送信世代を失効し、
    /// 機器へ停止を要求する。BPM倍率はBPM連動中だけ適用する。
    /// </summary>
    public static class MainGameHandySyncApi
    {
        public static bool IsAvailable => Plugin.Instance != null;

        /// <summary>現在の有効/無効。取得できなければ false を返す。</summary>
        public static bool TryGetEnabled(out bool enabled)
        {
            enabled = false;
            Plugin instance = Plugin.Instance;
            if (instance == null)
                return false;

            return instance.TryGetHampEnabledInternal(out enabled);
        }

        /// <summary>有効/無効を設定する。</summary>
        public static bool TrySetEnabled(bool enabled)
        {
            Plugin instance = Plugin.Instance;
            return instance != null && instance.TrySetHampEnabledInternal(enabled);
        }
        public static int GetBpmDivisor() => Plugin.Instance?.RhythmDivisor ?? 1;
        public static void CycleBpmDivisor() { Plugin.Instance?.CycleRhythmDivisor(); }
    }

    internal sealed partial class Plugin
    {
        /// <summary>
        /// 外部から止められている間は true。
        /// この機器には動作系統が2つある（HAMPの自走往復と、LMのストローク送出）。
        /// 両方をこのフラグとクライアントの送信世代で止める。
        /// </summary>
        internal bool _externalMotionStopped;

        internal bool TryGetHampEnabledInternal(out bool enabled)
        {
            enabled = false;
            if (_cfgLinked == null)
                return false;

            enabled = _cfgLinked.Value && !_externalMotionStopped;
            return true;
        }

        internal bool TrySetHampEnabledInternal(bool enabled)
        {
            if (enabled && !SessionGate.Allowed) return false;

            if (_cfgLinked == null)
                return false;

            if (_cfgLinked.Value != enabled) _cfgLinked.Value = enabled;
            else ApplyLinkedSetting();
            return true;
        }

        /// <summary>クールダウンを無視して即座に停止させる。</summary>
        private void ForceHampStopInternal()
        {
            _hampNextSwitchTime = 0f;
            _hampRunning = false;
            _hampLastVelocity = _hampLastMin = _hampLastMax = -1f;
            _gaugeSmoothed = -1f;

            if (_handyClient != null)
            {
                if (_strokeCoroutine != null) { StopCoroutine(_strokeCoroutine); _strokeCoroutine = null; }
                _handyClient.EndSession();
            }
        }

        /// <summary>次のフレームで開始処理が確実に走る状態へ戻す。</summary>
        private void PrepareHampRestartInternal()
        {
            _hampNextSwitchTime = 0f;
            _hampRunning = false;
            _hampLastVelocity = _hampLastMin = _hampLastMax = -1f;
            _gaugeSmoothed = -1f;
        }
    }
}
