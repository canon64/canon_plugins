using System;
using UnityEngine;

namespace MainGameGlow
{
    /// <summary>
    /// 外部プラグインから MainGameGlow を利用するための公開API。
    /// リフレクション経由で型解決される前提（直接DLL参照不要）。
    /// </summary>
    public static class MainGameGlowApi
    {
        /// <summary>MainGameGlowが内部で保持しているグロー出力RTを取得。</summary>
        public static bool TryGetGlowRenderTexture(out RenderTexture rt, out string reason)
        {
            rt = null;
            reason = string.Empty;
            try
            {
                return Plugin.TryGetGlowRenderTextureInternalApi(out rt, out reason);
            }
            catch (Exception ex)
            {
                reason = "exception:" + ex.Message;
                return false;
            }
        }

        /// <summary>MainGameGlowの有効/無効を取得。</summary>
        public static bool TryGetEnabled(out bool enabled)
        {
            return Plugin.TryGetEnabledInternalApi(out enabled);
        }

        /// <summary>MainGameGlowの有効/無効を設定。</summary>
        public static bool TrySetEnabled(bool enabled)
        {
            return Plugin.TrySetEnabledInternalApi(enabled);
        }

        /// <summary>現在のグロー設定値（Threshold/Strength/BlurPercent/Tint/OverlayAlpha）を取得。</summary>
        public static bool TryGetGlowParameters(
            out float threshold, out float strength, out float blurPercent,
            out float tintR, out float tintG, out float tintB, out float tintA, out float overlayAlpha,
            out string reason)
        {
            threshold = 0f; strength = 0f; blurPercent = 0f;
            tintR = 1f; tintG = 1f; tintB = 1f; tintA = 1f; overlayAlpha = 1f;
            reason = string.Empty;
            try
            {
                return Plugin.TryGetGlowParametersInternalApi(
                    out threshold, out strength, out blurPercent,
                    out tintR, out tintG, out tintB, out tintA, out overlayAlpha, out reason);
            }
            catch (Exception ex)
            {
                reason = "exception:" + ex.Message;
                return false;
            }
        }
    }
}
