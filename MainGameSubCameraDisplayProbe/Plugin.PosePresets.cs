using UnityEngine;

namespace MainGameSubCameraDisplayProbe
{
    public sealed partial class Plugin
    {
        private string _lastPoseKey = string.Empty;
        private string _lastPoseDisplayName = string.Empty;
        private bool _poseTrackingInitialized;

        private bool TryGetCurrentPoseInfo(out string key, out string displayName)
        {
            key = string.Empty;
            displayName = string.Empty;
            try
            {
                HSceneProc proc = UnityEngine.Object.FindObjectOfType<HSceneProc>();
                if (proc == null || proc.flags == null || proc.flags.nowAnimationInfo == null)
                    return false;

                HSceneProc.AnimationListInfo info = proc.flags.nowAnimationInfo;
                key = info.mode.ToString() + ":" + info.id.ToString();
                displayName = string.IsNullOrWhiteSpace(info.nameAnimation) ? key : info.nameAnimation;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void UpdatePosePresetTracking()
        {
            if (_settings == null)
                return;

            bool hasPose = TryGetCurrentPoseInfo(out string key, out string displayName);
            if (!hasPose)
            {
                _lastPoseKey = string.Empty;
                _lastPoseDisplayName = string.Empty;
                _poseTrackingInitialized = false;
                return;
            }

            if (!_settings.PosePresetAutoApply)
            {
                _lastPoseKey = key;
                _lastPoseDisplayName = displayName;
                _poseTrackingInitialized = true;
                return;
            }

            if (!_poseTrackingInitialized)
            {
                bool probeReady = _subCamera != null && _cameraAnchorObject != null
                    && _displayObject != null && _displayAnchorObject != null;
                if (!probeReady)
                    return;

                _lastPoseKey = key;
                _lastPoseDisplayName = displayName;
                _poseTrackingInitialized = true;
                ApplyTaggedPoseOverrides(key);
                return;
            }

            if (string.Equals(key, _lastPoseKey, System.StringComparison.Ordinal))
                return;

            _lastPoseKey = key;
            _lastPoseDisplayName = displayName;
            ApplyTaggedPoseOverrides(key);
        }

        private void ApplyTaggedPoseOverrides(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            if (!string.IsNullOrWhiteSpace(_activeCameraPresetName))
            {
                SubCameraPreset preset = FindPresetByName(_activeCameraPresetName);
                if (preset != null && preset.UsePoseOverrides && FindCameraPoseOverride(preset, key) != null)
                {
                    LoadPreset(preset, "pose-auto-apply");
                    LogInfo("camera pose override applied key=" + key + " preset=" + _activeCameraPresetName);
                }
            }

            if (_displayObject != null && _displayAnchorObject != null && !string.IsNullOrWhiteSpace(_activeDisplayPresetName))
            {
                DisplayPreset preset = FindDisplayPresetByName(_activeDisplayPresetName);
                if (preset != null && preset.UsePoseOverrides && FindDisplayPoseOverride(preset, key) != null)
                {
                    LoadDisplayPreset(preset);
                    LogInfo("display pose override applied key=" + key + " preset=" + _activeDisplayPresetName);
                }
            }
        }
    }
}
