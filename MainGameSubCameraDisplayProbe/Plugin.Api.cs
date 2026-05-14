using System;
using System.Collections.Generic;

namespace MainGameSubCameraDisplayProbe
{
    public static class MainGameSubCameraDisplayProbeApi
    {
        public static bool TryLoadPresetByName(string presetName, out string reason)
        {
            return Plugin.TryLoadPresetByName(presetName, out reason);
        }

        public static bool TryGetPresetNames(out string[] names, out string reason)
        {
            names = new string[0];
            reason = string.Empty;
            try
            {
                if (!Plugin.TryGetPresetNamesInternalApi(out string[] result, out string innerReason))
                {
                    reason = innerReason;
                    return false;
                }

                names = result ?? new string[0];
                reason = "ok";
                return true;
            }
            catch (Exception ex)
            {
                reason = "exception:" + ex.Message;
                return false;
            }
        }

        public static bool TryGetRenderTexture(out UnityEngine.RenderTexture renderTexture, out string reason)
        {
            renderTexture = null;
            reason = string.Empty;
            try
            {
                return Plugin.TryGetRenderTextureInternalApi(out renderTexture, out reason);
            }
            catch (Exception ex)
            {
                reason = "exception:" + ex.Message;
                return false;
            }
        }

        public static bool TryGetUiVisible(out bool visible)
        {
            return Plugin.TryGetUiVisible(out visible);
        }

        public static bool TrySetUiVisible(bool visible)
        {
            return Plugin.TrySetUiVisible(visible);
        }
    }
}
