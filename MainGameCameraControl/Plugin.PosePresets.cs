using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MainGameCameraControl
{
    internal sealed partial class Plugin
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

        private CameraPreset FindPresetByName(string name)
        {
            string normalized = (name ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(normalized))
                return null;

            return _presets.FirstOrDefault(x =>
                x != null
                && !string.IsNullOrWhiteSpace(x.Name)
                && string.Equals(x.Name.Trim(), normalized, StringComparison.Ordinal));
        }

        private static CameraPoseOverride FindCameraPoseOverride(CameraPreset preset, string key)
        {
            if (preset == null || string.IsNullOrWhiteSpace(key))
                return null;

            CameraPoseOverride[] overrides = preset.PoseOverrides ?? new CameraPoseOverride[0];
            for (int i = 0; i < overrides.Length; i++)
            {
                CameraPoseOverride entry = overrides[i];
                if (entry != null && string.Equals(entry.Key, key, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        private static float[] CloneFloatArray(float[] values)
        {
            if (values == null)
                return null;

            float[] copy = new float[values.Length];
            Array.Copy(values, copy, values.Length);
            return copy;
        }

        private static CameraPreset ClonePreset(CameraPreset preset)
        {
            if (preset == null)
                return null;

            return new CameraPreset
            {
                Name = preset.Name,
                TargetPosition = CloneFloatArray(preset.TargetPosition),
                CameraDirection = CloneFloatArray(preset.CameraDirection),
                Rotation = CloneFloatArray(preset.Rotation),
                Fov = preset.Fov,
                UseBoneLink = preset.UseBoneLink,
                BoneTarget = preset.BoneTarget,
                BoneName = preset.BoneName,
                LookAtOffsetLocal = CloneFloatArray(preset.LookAtOffsetLocal),
                UseKsPlugFpvLink = preset.UseKsPlugFpvLink,
                SaveCameraPosition = preset.SaveCameraPosition,
                CameraOffsetLocal = CloneFloatArray(preset.CameraOffsetLocal),
                CameraOffsetWorld = CloneFloatArray(preset.CameraOffsetWorld),
                RotationOffset = CloneFloatArray(preset.RotationOffset),
                LegacyPosition = CloneFloatArray(preset.LegacyPosition),
                LegacyLookAt = CloneFloatArray(preset.LegacyLookAt),
                LegacyDistance = preset.LegacyDistance,
                UsePoseOverrides = preset.UsePoseOverrides,
                PoseOverrides = preset.PoseOverrides
            };
        }

        // 呼び出し時、現在の体位に一致するオーバーライドがあればその値で上書きしたクローンを返す（Name は保持）
        private CameraPreset ResolveCameraPresetForCurrentPose(CameraPreset preset)
        {
            if (preset == null || !preset.UsePoseOverrides)
                return preset;

            if (!TryGetCurrentPoseInfo(out string key, out _))
                return preset;

            CameraPoseOverride pose = FindCameraPoseOverride(preset, key);
            if (pose == null)
                return preset;

            CameraPreset resolved = ClonePreset(preset);
            resolved.UseBoneLink = pose.UseBoneLink;
            resolved.UseKsPlugFpvLink = pose.UseKsPlugFpvLink;
            resolved.BoneTarget = pose.BoneTarget;
            resolved.BoneName = pose.BoneName;
            resolved.SaveCameraPosition = pose.SaveCameraPosition;
            resolved.TargetPosition = CloneFloatArray(pose.TargetPosition) ?? resolved.TargetPosition;
            resolved.CameraDirection = CloneFloatArray(pose.CameraDirection) ?? resolved.CameraDirection;
            resolved.Rotation = CloneFloatArray(pose.Rotation) ?? resolved.Rotation;
            resolved.LookAtOffsetLocal = CloneFloatArray(pose.LookAtOffsetLocal) ?? resolved.LookAtOffsetLocal;
            resolved.CameraOffsetLocal = CloneFloatArray(pose.CameraOffsetLocal) ?? resolved.CameraOffsetLocal;
            resolved.CameraOffsetWorld = CloneFloatArray(pose.CameraOffsetWorld) ?? resolved.CameraOffsetWorld;
            resolved.Fov = pose.Fov > 0f ? pose.Fov : resolved.Fov;
            LogInfo("pose override load preset=" + (preset.Name ?? string.Empty)
                + " key=" + key
                + " display=" + (pose.DisplayName ?? string.Empty)
                + " fov=" + resolved.Fov.ToString("0.##"));
            return resolved;
        }

        // 現在のカメラ状態を、指定プリセットの現在体位オーバーライドとして記録する
        private bool CapturePoseOverrideToPreset(CameraPreset preset, string key, string displayName)
        {
            if (preset == null || string.IsNullOrWhiteSpace(key))
                return false;

            CameraPreset snapshot = BuildCurrentPresetSnapshot(preset.Name ?? string.Empty);
            if (snapshot == null)
                return false;

            CameraPoseOverride pose = FindCameraPoseOverride(preset, key);
            if (pose == null)
            {
                List<CameraPoseOverride> list = new List<CameraPoseOverride>(preset.PoseOverrides ?? new CameraPoseOverride[0]);
                pose = new CameraPoseOverride { Key = key };
                list.Add(pose);
                preset.PoseOverrides = list.ToArray();
            }

            pose.DisplayName = displayName ?? string.Empty;
            pose.UseBoneLink = snapshot.UseBoneLink;
            pose.UseKsPlugFpvLink = snapshot.UseKsPlugFpvLink;
            pose.BoneTarget = snapshot.BoneTarget;
            pose.BoneName = snapshot.BoneName;
            pose.SaveCameraPosition = snapshot.SaveCameraPosition;
            pose.TargetPosition = CloneFloatArray(snapshot.TargetPosition);
            pose.CameraDirection = CloneFloatArray(snapshot.CameraDirection);
            pose.Rotation = CloneFloatArray(snapshot.Rotation);
            pose.LookAtOffsetLocal = CloneFloatArray(snapshot.LookAtOffsetLocal);
            pose.CameraOffsetLocal = CloneFloatArray(snapshot.CameraOffsetLocal);
            pose.CameraOffsetWorld = CloneFloatArray(snapshot.CameraOffsetWorld);
            pose.Fov = snapshot.Fov;
            LogInfo("pose override save preset=" + (preset.Name ?? string.Empty)
                + " key=" + key
                + " display=" + pose.DisplayName
                + " bone=" + pose.UseBoneLink + ":" + pose.BoneTarget
                + " fov=" + pose.Fov.ToString("0.##"));
            return true;
        }

        // 現在の保存モードに従って現在カメラのスナップショット CameraPreset を作る（保存ロジックの再利用）
        private CameraPreset BuildCurrentPresetSnapshot(string name)
        {
            RefreshCameraControllerCache();
            if (!_hasCameraController || _ctrlCamera == null)
                return null;

            BaseCameraControl_Ver2.CameraData data = _ctrlCamera.GetCameraData();
            data.Fov = ResolveLiveCameraFov(data.Fov);

            switch (_selectedSaveMode)
            {
                case SaveModeBoneLink:
                    return BuildBoneLinkedPreset(name, data);
                case SaveModeKsPlugFpv:
                    return BuildKsPlugFpvPreset(name, data);
                default:
                    return new CameraPreset
                    {
                        Name = name,
                        TargetPosition = ToArray(data.Pos),
                        CameraDirection = ToArray(data.Dir),
                        Rotation = ToArray(data.Rot),
                        Fov = data.Fov
                    };
            }
        }

        // UI「体位を上書き」: アクティブプリセットの現在体位オーバーライドを保存
        private void CapturePoseOverrideForActivePreset()
        {
            CameraPreset preset = FindPresetByName(_activeCameraPresetName);
            if (preset == null)
            {
                SetStatus("体位保存: アクティブなプリセットが無い");
                return;
            }

            if (!TryGetCurrentPoseInfo(out string key, out string displayName))
            {
                SetStatus("体位保存: 体位を取得できない");
                return;
            }

            preset.UsePoseOverrides = true;
            if (!CapturePoseOverrideToPreset(preset, key, displayName))
            {
                SetStatus("体位保存に失敗");
                return;
            }

            CommitPresets("pose-override-save");
            SetStatus("体位保存: " + (preset.Name ?? string.Empty) + " / " + displayName);
        }

        private void UpdatePosePresetTracking()
        {
            bool hasPose = TryGetCurrentPoseInfo(out string key, out string displayName);
            if (!hasPose)
            {
                _lastPoseKey = string.Empty;
                _lastPoseDisplayName = string.Empty;
                _poseTrackingInitialized = false;
                return;
            }

            if (!_posePresetAutoApply)
            {
                _lastPoseKey = key;
                _lastPoseDisplayName = displayName;
                _poseTrackingInitialized = true;
                return;
            }

            if (!_poseTrackingInitialized)
            {
                RefreshCameraControllerCache();
                if (!_hasCameraController || _ctrlCamera == null)
                    return;

                _lastPoseKey = key;
                _lastPoseDisplayName = displayName;
                _poseTrackingInitialized = true;
                ApplyActivePosePreset(key);
                return;
            }

            if (string.Equals(key, _lastPoseKey, StringComparison.Ordinal))
                return;

            _lastPoseKey = key;
            _lastPoseDisplayName = displayName;
            ApplyActivePosePreset(key);
        }

        private void ApplyActivePosePreset(string key)
        {
            if (string.IsNullOrWhiteSpace(_activeCameraPresetName))
                return;

            CameraPreset preset = FindPresetByName(_activeCameraPresetName);
            if (preset == null || !preset.UsePoseOverrides)
                return;

            if (FindCameraPoseOverride(preset, key) == null)
                return;

            LoadPreset(preset);
            LogInfo("pose auto-apply key=" + key + " preset=" + _activeCameraPresetName);
        }
    }
}
