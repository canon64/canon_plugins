using System;
using System.Linq;
using UnityEngine;
namespace MainGameSubCameraDisplayProbe {
 public sealed partial class Plugin {
  public object[] GetPlaybackCameraPresetState() => new object[] { _settings?.Presets != null && _settings.Presets.Any(p => p != null), _settings != null && _settings.AutoCycleEnabled, _activeCameraPresetName ?? "" };
  public bool GetPlaybackCameraGizmo() => _settings != null && _settings.CameraGizmoVisible;
  public bool CanOverwritePlaybackCameraPose() => SessionGate.Allowed && _cameraAnchorObject != null && _subCamera != null && FindPresetByName(_activeCameraPresetName) != null
   && (_actorLease != 0 ? ActorLeaseValid(_actorLease) : TryGetCurrentPoseInfo(out _, out _));
  public void OverwritePlaybackCameraPose() {
   if (!CanOverwritePlaybackCameraPose()) return;
   var preset = FindPresetByName(_activeCameraPresetName);
   if (!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("com.kks.maingame.cameracontrol", out var info)) { SetStatus("メインカメラ連携が利用できません"); return; }
   var data = info.Instance.GetType().GetMethod("CaptureMainCameraForSubCamera")?.Invoke(info.Instance, null) as object[];
   if (data == null || data.Length < 9) { SetStatus("メインカメラの状態を取得できません"); return; }
   BeginPlaybackCopyTrace(data);
   // Convert source geometry to the subcamera's fixed-position bone-look-at mode.
   bool boneLinked = (bool)data[3];
   var boneCamera = ReadCopiedBoneCamera(data);
   if (boneLinked && boneCamera == null) { SetStatus("保存失敗: メインのキャラ・ボーン情報を取得できません。両カメラDLLの更新が必要です"); return; }
   Vector3 cameraWorld = (Vector3)data[0];
   Vector3 lookWorld = (Vector3)data[8];
   Vector3 lookOffset = (Vector3)data[6];
   Vector3 cameraOffset = (Vector3)data[7];
   string boneName = (string)data[5];
   var captured = new SubCameraPreset {
    Name = preset.Name, BoneCamera = boneCamera, UseBoneLink = boneLinked,
    BoneTarget = (int)data[4], BoneName = boneName, SaveCameraPosition = true,
    CameraPosition = ToArray(cameraWorld), CameraRotation = ToArray((Vector3)data[1]), Fov = (float)data[2],
    LookAtOffsetLocal = ToArray(lookOffset), CameraOffsetLocal = ToArray(cameraOffset), LookAtPosition = ToArray(lookWorld)
   };
   if (!PrepareCopiedBinding(captured)) return;
   if (_actorLease != 0) {
    var previousActorShot = preset.ActorCamera;
    preset.ActorCamera = CaptureActorShot(captured);
    if (!SaveSettings()) { preset.ActorCamera = previousActorShot; SetStatus("保存失敗: 設定ファイルを書き込めません"); return; }
    TracePlaybackCopySaved(captured);
    ApplyExternalPreset(preset, "save");
    // A manual overwrite takes effect at once, including after the subcamera
    // position sliders changed the previous live actor shot.
    _externalPresetDuration = 0;
    ApplyCapturedPlaybackCamera(captured);
    SetStatus("メインカメラから呼出キャラのカメラ上書き: " + preset.Name);
    return;
   }
   if (!TryGetCurrentPoseInfo(out string key, out string label)) return;
   var previousOverrides = preset.PoseOverrides;
   bool previousUseOverrides = preset.UsePoseOverrides;
   var items = new System.Collections.Generic.List<CameraPoseOverride>(previousOverrides ?? new CameraPoseOverride[0]);
   var previousPose = FindCameraPoseOverride(preset, key);
   var pose = new CameraPoseOverride { Key = key };
   if (previousPose == null) items.Add(pose); else items[items.IndexOf(previousPose)] = pose;
   pose.DisplayName = label;
   pose.BoneCamera = boneCamera;
   pose.SaveCameraPosition = true;
   pose.CameraPosition = ToArray(cameraWorld);
   pose.CameraRotation = ToArray((Vector3)data[1]);
   pose.Fov = (float)data[2]; pose.UseBoneLink = boneLinked;
   pose.BoneTarget = (int)data[4]; pose.BoneName = boneName;
   pose.LookAtOffsetLocal = ToArray(lookOffset);
   pose.CameraOffsetLocal = ToArray(cameraOffset);
   pose.LookAtPosition = ToArray(lookWorld);
   preset.PoseOverrides = items.ToArray();
   preset.UsePoseOverrides = true;
   if (!SaveSettings()) { preset.PoseOverrides = previousOverrides; preset.UsePoseOverrides = previousUseOverrides; SetStatus("保存失敗: 設定ファイルを書き込めません"); return; }
   TracePlaybackCopySaved(captured);
   _transitionActive = false;
   _transitionDynamicTarget = false;
   _transitionPreset = null;
   if (boneLinked) ActivateBoneFollow(captured, false); else ClearBoneFollow();
   ApplyCapturedPlaybackCamera(captured);
   _lastPoseKey = key; _lastPoseDisplayName = label; _poseTrackingInitialized = true; _poseTrackingPausedByDisable = false;
   NotifyAutoCycleLoadPreset(preset, "manual");
   SetStatus("メインカメラから体位上書き: " + preset.Name + " / " + label);
  }
  private void ApplyCapturedPlaybackCamera(SubCameraPreset captured) {
   Vector3 position = ToVector3(captured.CameraPosition, Vector3.zero);
   Quaternion rotation = Quaternion.Euler(ToVector3(captured.CameraRotation, Vector3.zero));
   if (captured.UseBoneLink)
    ResolveSubBoneView(captured.BoneCamera, captured.BoneCamera.RuntimeBone,
     ToVector3(captured.LookAtOffsetLocal, Vector3.zero), position, rotation, out position, out rotation);
   _cameraAnchorObject.transform.SetPositionAndRotation(position, rotation);
   _subCamera.transform.localPosition = Vector3.zero;
   _subCamera.transform.localRotation = Quaternion.identity;
   _settings.CameraFieldOfView = captured.Fov;
   _subCamera.fieldOfView = captured.Fov;
   CaptureTransformsToSettings();
   TracePlaybackCopyApplied();
  }
  public void TogglePlaybackCameraGizmo() {
   if (_settings == null) return;
   _settings.CameraGizmoVisible = !_settings.CameraGizmoVisible;
   if (_settings.CameraGizmoVisible) EnsureCameraProbe();
   SyncGizmoState(); SaveSettings();
  }
  public void PlaybackCameraPresetCommand(int direction) {
   if (_settings == null) return;
   if (direction == 0) {
    _settings.AutoCycleEnabled = !_settings.AutoCycleEnabled;
    _autoCycleAwayUntil = 0;
    _autoCycleNextAt = UnityEngine.Time.unscaledTime;
    if (_settings.AutoCycleEnabled) EnsureCameraProbe();
    SaveSettings(); return;
   }
   var presets = _settings.Presets?.Where(p => p != null).ToArray();
   if (presets == null || presets.Length == 0) return;
   int index = Array.FindIndex(presets, p => p.Name == _activeCameraPresetName);
   int next = index < 0 ? (direction < 0 ? presets.Length - 1 : 0) : (index + (direction < 0 ? -1 : 1) + presets.Length) % presets.Length;
   LoadPreset(presets[next]);
  }
 }
}
