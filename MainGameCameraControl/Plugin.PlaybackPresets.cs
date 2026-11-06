using SessionGate = CanonPlugins.Local.SessionGate;
using System.Linq;
namespace MainGameCameraControl {
 internal sealed partial class Plugin {
  public object[] CaptureMainCameraForSubCamera() {
   if(!SessionGate.Allowed)return null;
   RefreshCameraControllerCache();
   var camera = _ctrlCamera != null ? _ctrlCamera.thisCmaera : null;
   if (camera == null) return null;
   var female = ResolveMainFemale();
   var bone = _boneLinkActive && female != null ? ResolveActiveBoneTarget(female) : null;
   if (_boneLinkActive && bone == null) return null;
   var position = camera.transform.position;
   var lookAt = bone != null ? ResolveActiveLookAt(bone) : position + camera.transform.forward;
   // Version 2 transfers geometry and identity, not the main camera's follow implementation.
   return new object[] { position, camera.transform.eulerAngles, camera.fieldOfView,
    bone != null, _activeBoneLinkTarget, bone != null ? bone.name : "",
    bone != null ? bone.InverseTransformPoint(lookAt) : UnityEngine.Vector3.zero,
    bone != null ? position - bone.position : UnityEngine.Vector3.zero,
    lookAt, 2, bone, female, camera };
  }
  public bool CanOverwritePlaybackCameraPose() => FindPresetByName(_activeCameraPresetName) != null && TryGetCurrentPoseInfo(out _, out _);
  public void OverwritePlaybackCameraPose(){if(SessionGate.Allowed)CapturePoseOverrideForActivePreset();}
  public object[] GetPlaybackCameraPresetState() => new object[] { _presets != null && _presets.Any(p => p != null), _cfgAutoCameraCycleEnabled != null && _cfgAutoCameraCycleEnabled.Value, _activeCameraPresetName ?? "" };
  public void PlaybackCameraPresetCommand(int direction) {
   if(!SessionGate.Allowed)return;
   if (direction == 0) {
    if (_cfgAutoCameraCycleEnabled == null) return;
    _cfgAutoCameraCycleEnabled.Value = !_cfgAutoCameraCycleEnabled.Value;
    ResetAutoCameraCycleTimer(); SaveSettings(); return;
   }
   var presets = _presets.Where(p => p != null).ToList();
   if (presets.Count == 0) return;
   int index = presets.FindIndex(IsSelectedPreset);
   int next = index < 0 ? (direction < 0 ? presets.Count - 1 : 0) : (index + (direction < 0 ? -1 : 1) + presets.Count) % presets.Count;
   LoadPreset(presets[next]);
  }
 }
}
