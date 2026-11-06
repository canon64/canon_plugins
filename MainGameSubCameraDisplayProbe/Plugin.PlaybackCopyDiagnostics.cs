using UnityEngine;

namespace MainGameSubCameraDisplayProbe
{
    public sealed partial class Plugin
    {
        private int _playbackCopyTraceId;
        private Vector3 _playbackCopyRequested;
        private Camera _playbackCopySource;
        private Transform _playbackCopyBone;
        private Vector3 _playbackCopyBoneOffset;
        private System.Action _playbackCopyNextFollowTrace;
        private Coroutine _playbackCopySampler;
        private string _playbackCopyRenderStage = "first-render";

        private void BeginPlaybackCopyTrace(object[] data)
        {
            CancelPlaybackCopyTrace();
            if (_settings == null || !_settings.LoggingEnabled) return;
            _playbackCopyTraceId++;
            _playbackCopyRequested = (Vector3)data[0];
            _playbackCopySource = data.Length == 13 ? data[12] as Camera : null;
            _playbackCopyBone = data.Length == 13 ? data[10] as Transform : null;
            _playbackCopyBoneOffset = (Vector3)data[6];
            _playbackCopyNextFollowTrace = () => TracePlaybackCopyPose("first-follow");
            LogInfo("[circle#" + _playbackCopyTraceId + "] capture requested=" + _playbackCopyRequested.ToString("F6")
                + " source=" + DescribeCopyCamera(_playbackCopySource)
                + " Camera.main=" + DescribeCopyCamera(Camera.main)
                + " bone=" + data[5] + " lookAt=" + ((Vector3)data[8]).ToString("F6") + " format=" + (data.Length > 9 ? data[9] : null)
                + " preset=" + _activeCameraPresetName + " actorLease=" + _actorLease);
        }

        private static string DescribeCopyCamera(Camera camera)
        {
            return camera == null ? "null" : camera.name + " pos=" + camera.transform.position.ToString("F6")
                + " rot=" + camera.transform.eulerAngles.ToString("F6") + " fov=" + camera.fieldOfView;
        }

        private void TracePlaybackCopyApplied()
        {
            if (_settings == null || !_settings.LoggingEnabled) return;
            TracePlaybackCopyPose("applied");
            Camera.onPostRender -= OnPlaybackCopyRendered;
            Camera.onPostRender += OnPlaybackCopyRendered;
            _playbackCopyRenderStage = "first-render";
            if (_playbackCopyBone != null)
                _playbackCopySampler = StartCoroutine(SamplePlaybackCopyAim(_playbackCopyTraceId));
        }

        // Explicitly requested, bounded diagnostic: six samples, two seconds apart.
        private System.Collections.IEnumerator SamplePlaybackCopyAim(int traceId)
        {
            for (int sample = 1; sample <= 6; sample++)
            {
                yield return new WaitForSecondsRealtime(2f);
                if (traceId != _playbackCopyTraceId || !SessionGate.Allowed || _settings == null
                    || !_settings.LoggingEnabled || _subCamera == null || _playbackCopyBone == null) break;
                string label = "sample" + sample;
                _playbackCopyNextFollowTrace = () =>
                {
                    TracePlaybackCopyPose(label + "-follow");
                    _playbackCopyRenderStage = label + "-render";
                    Camera.onPostRender -= OnPlaybackCopyRendered;
                    Camera.onPostRender += OnPlaybackCopyRendered;
                };
            }
            _playbackCopySampler = null;
        }

        private void TracePlaybackCopySaved(SubCameraPreset preset)
        {
            if (_settings == null || !_settings.LoggingEnabled) return;
            var shot = preset.BoneCamera;
            LogInfo("[circle#" + _playbackCopyTraceId + "] saved position="
                + ToVector3(preset.CameraPosition, Vector3.zero).ToString("F6")
                + " boneLocalLook=" + ToVector3(preset.LookAtOffsetLocal, Vector3.zero).ToString("F6")
                + " boneMode=" + preset.UseBoneLink + " actor=" + shot?.OwnerIndependentActor
                + " femaleSlot=" + shot?.OwnerFemaleSlot + " bonePath=" + shot?.BonePath);
        }

        private void OnPlaybackCopyRendered(Camera camera)
        {
            if (camera != _subCamera) return;
            Camera.onPostRender -= OnPlaybackCopyRendered;
            if (SessionGate.Allowed) TracePlaybackCopyPose(_playbackCopyRenderStage);
        }

        private void TracePlaybackCopyPose(string stage)
        {
            if (_settings == null || !_settings.LoggingEnabled || _subCamera == null) return;
            Transform targetBone = _actorLease != 0 ? _externalBone : _activeCopiedBoneCamera?.RuntimeBone;
            Vector3 targetOffset = _actorLease != 0 ? _externalLookOffset : _activeLookAtOffsetLocal;
            Vector3 sourceLook = _playbackCopyBone != null ? _playbackCopyBone.TransformPoint(_playbackCopyBoneOffset) : Vector3.zero;
            Vector3 targetLook = targetBone != null ? targetBone.TransformPoint(targetOffset) : Vector3.zero;
            Vector3 direction = targetLook - _subCamera.transform.position;
            Vector3 renderedForward = _subCamera.cameraToWorldMatrix.MultiplyVector(Vector3.back).normalized;
            LogInfo("[circle#" + _playbackCopyTraceId + "] " + stage
                + " frame=" + Time.frameCount + " time=" + Time.unscaledTime.ToString("F3")
                + " sub=" + DescribeCopyCamera(_subCamera) + " main=" + DescribeCopyCamera(_playbackCopySource)
                + " capturedGap=" + Vector3.Distance(_subCamera.transform.position, _playbackCopyRequested).ToString("F6")
                + " sourceBone=" + DescribeCopyBone(_playbackCopyBone)
                + " targetBone=" + DescribeCopyBone(targetBone)
                + " sameBone=" + (targetBone != null && targetBone == _playbackCopyBone)
                + " sourceLook=" + sourceLook.ToString("F6") + " targetLook=" + targetLook.ToString("F6")
                + " forward=" + _subCamera.transform.forward.ToString("F6")
                + " renderedForward=" + renderedForward.ToString("F6")
                + " boneRelativeForward=" + (targetBone != null
                    ? (Quaternion.Inverse(targetBone.rotation) * _subCamera.transform.forward).ToString("F6") : "n/a")
                + " aimErrorDegrees=" + (targetBone != null && direction.sqrMagnitude > .000001f
                    ? Vector3.Angle(_subCamera.transform.forward, direction).ToString("F6") : "n/a")
                + " renderedAimErrorDegrees=" + (targetBone != null && direction.sqrMagnitude > .000001f
                    ? Vector3.Angle(renderedForward, direction).ToString("F6") : "n/a")
                + " mainAimErrorDegrees=" + (_playbackCopySource != null && _playbackCopyBone != null
                    ? Vector3.Angle(_playbackCopySource.transform.forward, sourceLook - _playbackCopySource.transform.position).ToString("F6") : "n/a")
                + " local=" + _subCamera.transform.localPosition.ToString("F6")
                + " following=" + _boneFollowActive + " transition=" + _transitionActive
                + " autoCycle=" + _settings.AutoCycleEnabled + " preset=" + _activeCameraPresetName);
        }

        private void CancelPlaybackCopyTrace()
        {
            Camera.onPostRender -= OnPlaybackCopyRendered;
            _playbackCopyNextFollowTrace = null;
            if (_playbackCopySampler != null) { StopCoroutine(_playbackCopySampler); _playbackCopySampler = null; }
        }

        private static string DescribeCopyBone(Transform bone)
        {
            return bone == null ? "null" : bone.name + "#" + bone.GetInstanceID() + " pos=" + bone.position.ToString("F6");
        }

        // An operation arms this one-shot trace; it does no recurring pose sampling or logging.
        private void TraceNextPlaybackBoneFollow()
        {
            var trace = _playbackCopyNextFollowTrace;
            _playbackCopyNextFollowTrace = null;
            trace?.Invoke();
        }
    }
}
