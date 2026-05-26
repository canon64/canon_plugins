using UnityEngine;

namespace MainGameVoiceFaceEventBridge
{
    /// <summary>
    /// 他プラグインから呼ぶための公開エントリポイント。
    /// (本体 <see cref="Plugin"/> は internal sealed のため、ここを薄いラッパとして公開する)
    /// </summary>
    public static class PublicApi
    {
        /// <summary>
        /// 現在再生中の外部ボイス (SBV2 等) に対して、
        /// attack→hold→release 形のエンベロープで音量を一時的に持ち上げる。
        /// 再生していない / プラグイン未起動の場合は false。
        /// </summary>
        /// <param name="peakMultiplier">ピーク倍率 (1.0 = 等倍, 1.5 = +50%)</param>
        /// <param name="attackMs">立ち上がり時間 (ミリ秒)</param>
        /// <param name="holdMs">ピーク維持時間 (ミリ秒)</param>
        /// <param name="releaseMs">減衰時間 (ミリ秒)</param>
        public static bool TryRequestTransientVolumeBoost(float peakMultiplier, int attackMs, int holdMs, int releaseMs)
        {
            return TryRequestTransientVolumeBoost(peakMultiplier, attackMs, holdMs, releaseMs, 0, EasingShape.CosineInOut);
        }

        /// <summary>
        /// 拡張版: ブースト直後に silenceMs ミリ秒の無音を挿入し、AudioSource を一時 Pause する。
        /// 無音中は再生時刻が止まる (UnPause で再開)。easing で In/Out カーブを指定可能。
        /// </summary>
        public static bool TryRequestTransientVolumeBoost(
            float peakMultiplier,
            int attackMs,
            int holdMs,
            int releaseMs,
            int silenceMs,
            EasingShape easing)
        {
            return TryRequestTransientVolumeBoost(
                peakMultiplier,
                attackMs,
                holdMs,
                releaseMs,
                silenceMs,
                80,
                80,
                easing);
        }

        public static bool TryRequestTransientVolumeBoost(
            float peakMultiplier,
            int attackMs,
            int holdMs,
            int releaseMs,
            int silenceMs,
            int silenceFadeOutMs,
            int silenceFadeInMs,
            EasingShape easing)
        {
            Plugin inst = Plugin.Instance;
            if (inst == null) return false;
            return inst.RequestExternalVolumeBoost(
                peakMultiplier,
                attackMs * 0.001f,
                holdMs * 0.001f,
                releaseMs * 0.001f,
                Mathf.Max(0, silenceMs) * 0.001f,
                Mathf.Max(0, silenceFadeOutMs) * 0.001f,
                Mathf.Max(0, silenceFadeInMs) * 0.001f,
                easing);
        }

        public enum EasingShape
        {
            /// <summary>線形 (旧挙動)</summary>
            Linear = 0,
            /// <summary>三角関数によるS字 (InOut)。出も入りも滑らか。</summary>
            CosineInOut = 1,
        }
    }
}
