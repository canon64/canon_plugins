using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MainGameSubCameraDisplayProbe
{
    public sealed partial class Plugin
    {
        private const int SaveModeNormal = 0;
        private const int SaveModeBoneLink = 1;

        private void SaveCurrentPreset()
        {
            EnsureProbe();
            if (_cameraAnchorObject == null || _subCamera == null)
            {
                SetStatus("サブカメラ未生成");
                return;
            }

            string name = string.IsNullOrWhiteSpace(_presetNameBuffer)
                ? "Preset " + ((_settings.Presets?.Length ?? 0) + 1)
                : _presetNameBuffer.Trim();
            name = MakeUniquePresetName(name, null);

            SubCameraPreset preset = _settings.SelectedSaveMode == SaveModeBoneLink
                ? BuildBoneLinkedPreset(name)
                : BuildNormalPreset(name);
            if (preset == null)
            {
                SetStatus("プリセット保存失敗");
                return;
            }

            List<SubCameraPreset> presets = new List<SubCameraPreset>(_settings.Presets ?? new SubCameraPreset[0]);
            presets.Add(preset);
            _settings.Presets = presets.ToArray();
            SaveSettings();
            SetStatus("保存: " + name);
        }

        private SubCameraPreset BuildNormalPreset(string name)
        {
            Transform cameraTransform = _cameraAnchorObject.transform;
            return new SubCameraPreset
            {
                Name = name,
                UseBoneLink = false,
                SaveCameraPosition = true,
                CameraPosition = ToArray(cameraTransform.position),
                CameraRotation = ToArray(NormalizeEuler(cameraTransform.rotation.eulerAngles)),
                Fov = _settings.CameraFieldOfView,
                UsePoseOverrides = _settings.SaveCameraPoseOverrides,
                PoseOverrides = new CameraPoseOverride[0]
            };
        }

        private SubCameraPreset BuildBoneLinkedPreset(string name)
        {
            Transform bone = ResolveSelectedBone();
            if (bone == null || _cameraAnchorObject == null)
                return null;

            Transform cameraTransform = _cameraAnchorObject.transform;
            Vector3 lookAt = bone.position;
            bool saveCameraPosition = _settings.SaveBoneCameraPosition;
            Vector3 cameraPositionOffset = cameraTransform.position - bone.position;
            return new SubCameraPreset
            {
                Name = name,
                UseBoneLink = true,
                BoneTarget = _settings.SelectedBoneTarget,
                BoneName = bone.name,
                SaveCameraPosition = saveCameraPosition,
                CameraPosition = saveCameraPosition ? ToArray(cameraTransform.position) : null,
                CameraRotation = ToArray(NormalizeEuler(cameraTransform.rotation.eulerAngles)),
                CameraOffsetLocal = saveCameraPosition ? ToArray(cameraPositionOffset) : null,
                LookAtPosition = ToArray(lookAt),
                LookAtOffsetLocal = ToArray(bone.InverseTransformPoint(lookAt)),
                Fov = _settings.CameraFieldOfView,
                UsePoseOverrides = _settings.SaveCameraPoseOverrides,
                PoseOverrides = new CameraPoseOverride[0]
            };
        }

        private void LoadPreset(SubCameraPreset preset)
        {
            LoadPreset(preset, "manual");
        }

        private void LoadPreset(SubCameraPreset preset, string source)
        {
            if (preset == null)
                return;

            EnsureProbe();
            if (_cameraAnchorObject == null || _subCamera == null)
            {
                SetStatus("サブカメラ未生成");
                return;
            }

            SubCameraPreset resolved = ResolveCameraPresetForCurrentPose(preset);
            _activeCameraPresetName = preset.Name ?? string.Empty;
            _settings.CameraFieldOfView = Mathf.Clamp(resolved.Fov <= 0f ? _settings.CameraFieldOfView : resolved.Fov, 10f, 170f);
            _presetNameBuffer = preset.Name ?? string.Empty;
            StartCameraTransition(resolved);
            SaveSettings();
            SetStatus("呼び出し: " + (preset.Name ?? string.Empty));
            NotifyAutoCycleLoadPreset(preset, source);
        }

        private void ApplyNormalPreset(SubCameraPreset preset)
        {
            ClearBoneFollow();
            Transform cameraTransform = _cameraAnchorObject.transform;
            cameraTransform.position = ToVector3(preset.CameraPosition, cameraTransform.position);
            cameraTransform.rotation = Quaternion.Euler(ToVector3(preset.CameraRotation, cameraTransform.rotation.eulerAngles));
        }

        private void ActivateBoneFollow(SubCameraPreset preset)
        {
            _boneFollowActive = true;
            _activeBonePresetName = preset.Name ?? string.Empty;
            _activeBoneTarget = Mathf.Clamp(preset.BoneTarget, 0, BoneTargetLabels.Length - 1);
            _activeBoneName = preset.BoneName ?? string.Empty;
            _activeSaveCameraPosition = HasCameraPosition(preset);
            _activeLookAtOffsetLocal = ToVector3(preset.LookAtOffsetLocal, Vector3.zero);
            _activeCameraOffsetLocal = ToVector3(preset.CameraOffsetLocal, Vector3.zero);
            _activePresetFov = Mathf.Clamp(preset.Fov <= 0f ? _settings.CameraFieldOfView : preset.Fov, 10f, 170f);

            if (_activeSaveCameraPosition && _activeCameraOffsetLocal.sqrMagnitude <= 0.000001f)
            {
                Transform bone = ResolveActiveBone();
                if (bone != null)
                    _activeCameraOffsetLocal = ToVector3(preset.CameraPosition, _cameraAnchorObject.transform.position) - bone.position;
            }

            UpdateBoneFollow();
        }

        private void UpdateBoneFollow()
        {
            if (!_boneFollowActive || _cameraAnchorObject == null)
                return;

            Transform bone = ResolveActiveBone();
            if (bone == null)
                return;

            Transform cameraTransform = _cameraAnchorObject.transform;
            Vector3 lookAt = bone.TransformPoint(_activeLookAtOffsetLocal);
            if (_activeSaveCameraPosition)
                cameraTransform.position = bone.position + _activeCameraOffsetLocal;

            Vector3 forward = lookAt - cameraTransform.position;
            if (forward.sqrMagnitude > 0.000001f)
                cameraTransform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);

            _settings.CameraFieldOfView = _activePresetFov;
        }

        private void SyncActiveFovFromUi()
        {
            _settings.CameraFieldOfView = Mathf.Clamp(_settings.CameraFieldOfView, 10f, 170f);
            if (!_boneFollowActive)
                return;

            _activePresetFov = _settings.CameraFieldOfView;
            SaveActiveBonePresetOffsets();
        }

        private void CaptureActiveBoneCameraOffset()
        {
            if (!_boneFollowActive || !_activeSaveCameraPosition || _cameraAnchorObject == null)
                return;

            Transform bone = ResolveActiveBone();
            if (bone != null)
                _activeCameraOffsetLocal = _cameraAnchorObject.transform.position - bone.position;
        }

        private void SaveActiveBonePresetOffsets()
        {
            SubCameraPreset preset = FindActiveBonePreset();
            if (preset == null)
                return;

            Transform bone = ResolveActiveBone();
            preset.LookAtOffsetLocal = ToArray(_activeLookAtOffsetLocal);
            preset.LookAtPosition = bone != null
                ? ToArray(bone.TransformPoint(_activeLookAtOffsetLocal))
                : preset.LookAtPosition;
            preset.Fov = _settings.CameraFieldOfView;

            if (_activeSaveCameraPosition)
            {
                preset.SaveCameraPosition = true;
                preset.CameraOffsetLocal = ToArray(_activeCameraOffsetLocal);
                if (bone != null)
                    preset.CameraPosition = ToArray(bone.position + _activeCameraOffsetLocal);
            }

            SaveSettings();
        }

        private void OverwriteActivePreset()
        {
            EnsureProbe();
            if (_cameraAnchorObject == null || _subCamera == null)
            {
                SetStatus("サブカメラ未生成");
                return;
            }

            string name = (_presetNameBuffer ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                SetStatus("上書き名なし");
                return;
            }

            SubCameraPreset preset = FindPresetByName(name);
            if (preset == null)
            {
                SetStatus("上書き対象なし");
                return;
            }

            string normalizedName = preset.Name ?? name;
            if (preset.UsePoseOverrides && TryGetCurrentPoseInfo(out string poseKey, out string poseDisplayName))
            {
                CapturePoseOverrideToPreset(preset, poseKey, poseDisplayName);
                _activeCameraPresetName = normalizedName;
                SaveSettings();
                SetStatus("体位上書き保存: " + normalizedName + " / " + poseDisplayName);
                return;
            }

            SubCameraPreset next = preset.UseBoneLink
                ? BuildBoneLinkedPreset(normalizedName)
                : BuildNormalPreset(normalizedName);
            if (next == null)
            {
                SetStatus("上書き保存失敗");
                return;
            }

            preset.UseBoneLink = next.UseBoneLink;
            preset.BoneTarget = next.BoneTarget;
            preset.BoneName = next.BoneName;
            preset.SaveCameraPosition = next.SaveCameraPosition;
            preset.CameraPosition = next.CameraPosition;
            preset.CameraRotation = next.CameraRotation;
            preset.CameraOffsetLocal = next.CameraOffsetLocal;
            preset.LookAtPosition = next.LookAtPosition;
            preset.LookAtOffsetLocal = next.LookAtOffsetLocal;
            preset.Fov = next.Fov;
            preset.AutoCycleInclude = next.AutoCycleInclude;

            _activeCameraPresetName = normalizedName;
            SaveSettings();
            SetStatus("上書き保存: " + normalizedName);
        }

        private SubCameraPreset ResolveCameraPresetForCurrentPose(SubCameraPreset preset)
        {
            if (preset == null || !preset.UsePoseOverrides)
                return preset;

            if (!TryGetCurrentPoseInfo(out string key, out _))
                return preset;

            CameraPoseOverride pose = FindCameraPoseOverride(preset, key);
            if (pose == null)
                return preset;

            SubCameraPreset resolved = ClonePreset(preset);
            resolved.SaveCameraPosition = pose.SaveCameraPosition;
            resolved.CameraPosition = CloneArray(pose.CameraPosition, resolved.CameraPosition);
            resolved.CameraRotation = CloneArray(pose.CameraRotation, resolved.CameraRotation);
            resolved.CameraOffsetLocal = CloneArray(pose.CameraOffsetLocal, resolved.CameraOffsetLocal);
            resolved.LookAtPosition = CloneArray(pose.LookAtPosition, resolved.LookAtPosition);
            resolved.LookAtOffsetLocal = CloneArray(pose.LookAtOffsetLocal, resolved.LookAtOffsetLocal);
            resolved.Fov = pose.Fov > 0f ? pose.Fov : resolved.Fov;
            return resolved;
        }

        private void CapturePoseOverrideToPreset(SubCameraPreset preset, string key, string displayName)
        {
            if (preset == null || string.IsNullOrWhiteSpace(key))
                return;

            CameraPoseOverride pose = FindCameraPoseOverride(preset, key);
            if (pose == null)
            {
                List<CameraPoseOverride> list = new List<CameraPoseOverride>(preset.PoseOverrides ?? new CameraPoseOverride[0]);
                pose = new CameraPoseOverride { Key = key };
                list.Add(pose);
                preset.PoseOverrides = list.ToArray();
            }

            SubCameraPreset current = preset.UseBoneLink
                ? BuildBoneLinkedPreset(preset.Name ?? string.Empty)
                : BuildNormalPreset(preset.Name ?? string.Empty);
            if (current == null)
                return;

            pose.DisplayName = displayName ?? string.Empty;
            pose.SaveCameraPosition = current.SaveCameraPosition;
            pose.CameraPosition = CloneArray(current.CameraPosition, null);
            pose.CameraRotation = CloneArray(current.CameraRotation, null);
            pose.CameraOffsetLocal = CloneArray(current.CameraOffsetLocal, null);
            pose.LookAtPosition = CloneArray(current.LookAtPosition, null);
            pose.LookAtOffsetLocal = CloneArray(current.LookAtOffsetLocal, null);
            pose.Fov = current.Fov;
        }

        private CameraPoseOverride FindCameraPoseOverride(SubCameraPreset preset, string key)
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

        private static SubCameraPreset ClonePreset(SubCameraPreset preset)
        {
            if (preset == null)
                return null;

            return new SubCameraPreset
            {
                Name = preset.Name,
                UseBoneLink = preset.UseBoneLink,
                BoneTarget = preset.BoneTarget,
                BoneName = preset.BoneName,
                SaveCameraPosition = preset.SaveCameraPosition,
                CameraPosition = CloneArray(preset.CameraPosition, null),
                CameraRotation = CloneArray(preset.CameraRotation, null),
                CameraOffsetLocal = CloneArray(preset.CameraOffsetLocal, null),
                LookAtPosition = CloneArray(preset.LookAtPosition, null),
                LookAtOffsetLocal = CloneArray(preset.LookAtOffsetLocal, null),
                Fov = preset.Fov,
                AutoCycleInclude = preset.AutoCycleInclude,
                UsePoseOverrides = preset.UsePoseOverrides,
                PoseOverrides = preset.PoseOverrides
            };
        }

        private static float[] CloneArray(float[] values, float[] fallback)
        {
            float[] source = values ?? fallback;
            if (source == null)
                return null;

            float[] clone = new float[source.Length];
            Array.Copy(source, clone, source.Length);
            return clone;
        }

        private string MakeUniquePresetName(string baseName, SubCameraPreset self)
        {
            string seed = string.IsNullOrWhiteSpace(baseName) ? "Preset" : baseName.Trim();
            string candidate = seed;
            int suffix = 2;
            while (HasPresetNameConflict(candidate, self))
            {
                candidate = seed + " " + suffix.ToString();
                suffix++;
            }

            return candidate;
        }

        private bool HasPresetNameConflict(string name, SubCameraPreset self)
        {
            SubCameraPreset[] presets = _settings.Presets ?? new SubCameraPreset[0];
            for (int i = 0; i < presets.Length; i++)
            {
                SubCameraPreset preset = presets[i];
                if (preset == null || ReferenceEquals(preset, self))
                    continue;
                if (string.Equals(preset.Name ?? string.Empty, name, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private SubCameraPreset FindActiveBonePreset()
        {
            if (!_boneFollowActive)
                return null;

            SubCameraPreset[] presets = _settings.Presets ?? new SubCameraPreset[0];
            SubCameraPreset nameMatch = null;
            for (int i = 0; i < presets.Length; i++)
            {
                SubCameraPreset preset = presets[i];
                if (preset == null || !preset.UseBoneLink)
                    continue;

                bool sameName = string.Equals(preset.Name ?? string.Empty, _activeBonePresetName ?? string.Empty, StringComparison.Ordinal);
                if (!sameName)
                    continue;

                if (nameMatch == null)
                    nameMatch = preset;

                bool sameBone =
                    preset.BoneTarget == _activeBoneTarget
                    && string.Equals(preset.BoneName ?? string.Empty, _activeBoneName ?? string.Empty, StringComparison.Ordinal);
                if (sameBone)
                    return preset;
            }

            return nameMatch;
        }

        private bool IsActiveBonePreset(SubCameraPreset preset)
        {
            if (preset == null)
                return false;

            if (!_boneFollowActive || !preset.UseBoneLink)
                return string.Equals(preset.Name ?? string.Empty, _activeCameraPresetName ?? string.Empty, StringComparison.Ordinal);

            return string.Equals(preset.Name ?? string.Empty, _activeBonePresetName ?? string.Empty, StringComparison.Ordinal)
                && preset.BoneTarget == _activeBoneTarget
                && string.Equals(preset.BoneName ?? string.Empty, _activeBoneName ?? string.Empty, StringComparison.Ordinal);
        }

        private void ClearBoneFollow()
        {
            _boneFollowActive = false;
            _activeBonePresetName = string.Empty;
            _activeBoneTarget = -1;
            _activeBoneName = string.Empty;
            _activeSaveCameraPosition = false;
            _activeLookAtOffsetLocal = Vector3.zero;
            _activeCameraOffsetLocal = Vector3.zero;
            _activePresetFov = _settings != null ? _settings.CameraFieldOfView : 60f;
        }

        private void RemovePresetAt(int index)
        {
            SubCameraPreset[] source = _settings.Presets ?? new SubCameraPreset[0];
            if (index < 0 || index >= source.Length)
                return;

            string removed = source[index]?.Name ?? string.Empty;
            List<SubCameraPreset> next = new List<SubCameraPreset>(source);
            next.RemoveAt(index);
            _settings.Presets = next.ToArray();
            SaveSettings();
            SetStatus("削除: " + removed);
        }

        private string GetPresetDisplayName(SubCameraPreset preset, int index)
        {
            string name = string.IsNullOrWhiteSpace(preset?.Name) ? "Preset " + (index + 1) : preset.Name;
            if (preset == null)
                return name;

            string flags = string.Empty;
            if (preset.UseBoneLink && HasArray(preset.LookAtOffsetLocal))
                flags += "[注視]";
            if (HasCameraPosition(preset))
                flags += "[位置]";
            if (preset.Fov > 0f)
                flags += "[FOV]";
            if (preset.UsePoseOverrides)
                flags += "[体位]";

            return preset.UseBoneLink
                ? "ボーン" + flags + ": " + name
                : flags + name;
        }

        private static bool HasCameraPosition(SubCameraPreset preset)
        {
            return preset != null && preset.SaveCameraPosition && (HasArray(preset.CameraOffsetLocal) || HasArray(preset.CameraPosition));
        }

        private static bool HasArray(float[] values)
        {
            return values != null && values.Length >= 3;
        }

        private int CountPoseAwareCameraPresets()
        {
            SubCameraPreset[] presets = _settings.Presets ?? new SubCameraPreset[0];
            int count = 0;
            for (int i = 0; i < presets.Length; i++)
            {
                if (presets[i] != null && presets[i].UsePoseOverrides)
                    count++;
            }

            return count;
        }

        private Transform ResolveSelectedBone()
        {
            ChaControl female = ResolveMainFemale();
            Transform[] boneCache = GetFemaleBoneCache(female);
            return ResolveBoneTarget(boneCache, _settings.SelectedBoneTarget, null);
        }

        private Transform ResolveActiveBone()
        {
            ChaControl female = ResolveMainFemale();
            Transform[] boneCache = GetFemaleBoneCache(female);
            return ResolveBoneTarget(boneCache, _activeBoneTarget, _activeBoneName);
        }

        private static ChaControl ResolveMainFemale()
        {
            HSceneProc proc = UnityEngine.Object.FindObjectOfType<HSceneProc>();
            if (proc != null)
            {
                FieldInfo listField = typeof(HSceneProc).GetField("lstFemale", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                IList listObj = listField != null ? listField.GetValue(proc) as IList : null;
                if (listObj != null)
                {
                    for (int i = 0; i < listObj.Count; i++)
                    {
                        ChaControl cha = listObj[i] as ChaControl;
                        if (cha != null)
                            return cha;
                    }
                }
            }

            ChaControl[] all = UnityEngine.Object.FindObjectsOfType<ChaControl>();
            for (int i = 0; i < all.Length; i++)
            {
                ChaControl cha = all[i];
                if (cha != null && cha.sex == 1)
                    return cha;
            }

            return null;
        }

        private static Transform[] GetFemaleBoneCache(ChaControl female)
        {
            if (female == null)
                return null;

            Transform root = female.animBody != null ? female.animBody.transform : female.transform;
            return root != null ? root.GetComponentsInChildren<Transform>(true) : null;
        }

        private static Transform ResolveBoneTarget(Transform[] boneCache, int boneTarget, string preferredName)
        {
            if (boneCache == null)
                return null;

            if (!string.IsNullOrWhiteSpace(preferredName))
            {
                Transform exact = FindBoneByName(boneCache, preferredName);
                if (exact != null)
                    return exact;
            }

            int normalizedTarget = Mathf.Clamp(boneTarget, 0, BoneTargetNameCandidates.Length - 1);
            string[] candidates = BoneTargetNameCandidates[normalizedTarget];
            for (int i = 0; i < candidates.Length; i++)
            {
                Transform found = FindBoneByName(boneCache, candidates[i]);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static Transform FindBoneByName(Transform[] boneCache, string name)
        {
            if (boneCache == null || string.IsNullOrWhiteSpace(name))
                return null;

            for (int i = 0; i < boneCache.Length; i++)
            {
                Transform current = boneCache[i];
                if (current != null && string.Equals(current.name, name, StringComparison.Ordinal))
                    return current;
            }

            return null;
        }

        private void SetStatus(string text)
        {
            _statusMessage = text ?? string.Empty;
            LogInfo("preset: " + _statusMessage);
        }

        private bool TryLoadPresetByNameInternal(string presetName, out string reason)
        {
            reason = string.Empty;
            string normalized = (presetName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(normalized))
            {
                reason = "preset_name_empty";
                return false;
            }

            SubCameraPreset[] presets = _settings.Presets ?? new SubCameraPreset[0];
            for (int i = 0; i < presets.Length; i++)
            {
                SubCameraPreset preset = presets[i];
                if (preset == null || string.IsNullOrWhiteSpace(preset.Name))
                    continue;

                if (string.Equals(preset.Name.Trim(), normalized, StringComparison.Ordinal))
                {
                    LoadPreset(preset, "bridge");
                    reason = "ok";
                    return true;
                }
            }

            reason = "preset_not_found";
            return false;
        }

        public static bool TryLoadPresetByName(string presetName, out string reason)
        {
            reason = string.Empty;
            if (Instance == null)
            {
                reason = "instance_null";
                return false;
            }

            try
            {
                return Instance.TryLoadPresetByNameInternal(presetName, out reason);
            }
            catch (Exception ex)
            {
                reason = "exception:" + ex.Message;
                Instance.LogWarn("preset load failed: " + reason);
                return false;
            }
        }

        internal static bool TryGetRenderTextureInternalApi(out UnityEngine.RenderTexture renderTexture, out string reason)
        {
            renderTexture = null;
            reason = string.Empty;
            if (Instance == null)
            {
                reason = "instance_null";
                return false;
            }

            if (Instance._renderTexture == null)
            {
                reason = "render_texture_not_ready";
                return false;
            }

            renderTexture = Instance._renderTexture;
            reason = "ok";
            return true;
        }

        internal static bool TryGetPresetNamesInternalApi(out string[] names, out string reason)
        {
            names = new string[0];
            reason = string.Empty;
            if (Instance == null)
            {
                reason = "instance_null";
                return false;
            }

            SubCameraPreset[] presets = Instance._settings != null ? Instance._settings.Presets : null;
            if (presets == null || presets.Length == 0)
            {
                reason = "no_presets";
                return true;
            }

            List<string> result = new List<string>(presets.Length);
            for (int i = 0; i < presets.Length; i++)
            {
                SubCameraPreset preset = presets[i];
                if (preset == null || string.IsNullOrWhiteSpace(preset.Name))
                    continue;
                result.Add(preset.Name.Trim());
            }

            names = result.ToArray();
            reason = "ok";
            return true;
        }

        private static float[] ToArray(Vector3 value)
        {
            return new[] { Round2Static(value.x), Round2Static(value.y), Round2Static(value.z) };
        }

        private static Vector3 ToVector3(float[] values, Vector3 fallback)
        {
            if (values == null || values.Length < 3)
                return fallback;

            return new Vector3(values[0], values[1], values[2]);
        }

        private static Vector3 NormalizeEuler(Vector3 euler)
        {
            return new Vector3(NormalizeAngle(euler.x), NormalizeAngle(euler.y), NormalizeAngle(euler.z));
        }

        private static float NormalizeAngle(float angle)
        {
            float normalized = angle;
            while (normalized > 180f) normalized -= 360f;
            while (normalized < -180f) normalized += 360f;
            return normalized;
        }

        private static float Round2Static(float value)
        {
            return (float)Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }
    }
}
