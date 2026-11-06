using System;
using System.Linq;
using MainGameAiShoujoH.Contracts;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    // Captured after the loading cover; explicit operations only, restored even after permit loss.
    internal sealed class DedicatedSceneControlLease : IDisposable
    {
        private readonly Camera camera;
        private readonly Light light;
        private readonly Func<bool> permitted;
        private readonly HCameraState cameraBefore;
        private readonly HLightState lightBefore;
        private bool disposed;
        internal HSceneControls Controls { get; }
        internal DedicatedSceneControlLease(Camera camera, Light light, Func<bool> permitted)
        {
            this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
            this.light = light; this.permitted = permitted;
            cameraBefore = CaptureCamera(); lightBefore = CaptureLight();
            Controls = new HSceneControls {
                ReadCamera = () => { Require(); return CaptureCamera(); },
                ApplyCamera = value => { Require(); SetCamera(value); },
                ResetCamera = () => { Require(); SetCamera(cameraBefore); },
                ReadLight = () => { Require(); return CaptureLight(); },
                ApplyLight = value => { Require(); SetLight(value); },
                ResetLight = () => { Require(); SetLight(lightBefore); }
            };
        }
        private void Require()
        { if (disposed || !permitted()) throw new InvalidOperationException("専用H画面操作の世代/許可が失効しました。"); }
        internal void FrameParticipants(ChaControl[] actors, float two, float radiansPerDegree)
        {
            Require();
            var renderers = actors.SelectMany(actor => actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.sharedMesh != null).ToArray();
            if (renderers.Length == default(int)) throw new InvalidOperationException("専用Hの参加者描画範囲がありません。");
            var bounds = renderers.First().bounds;
            foreach (var renderer in renderers.Skip((int)Vector3.one.x)) bounds.Encapsulate(renderer.bounds);
            var direction = Vector3.ProjectOnPlane(bounds.center - camera.transform.position, Vector3.up);
            if (direction.sqrMagnitude <= Mathf.Epsilon) direction = actors.First().transform.forward;
            direction.Normalize();
            var side = Vector3.Cross(Vector3.up, direction);
            HCameraState selected = null;
            int obstructionCount = int.MaxValue;
            foreach (var horizontalDirection in new[] { direction, -direction, side, -side })
            {
                var rotation = Quaternion.LookRotation(horizontalDirection + Vector3.down, Vector3.up);
                var candidate = FitCamera(bounds, rotation, two, radiansPerDegree);
                var ray = candidate.Position - bounds.center;
                int count = Physics.RaycastAll(bounds.center, ray.normalized, ray.magnitude)
                    .Count(hit => !hit.collider.isTrigger && !actors.Any(actor => hit.collider.transform.IsChildOf(actor.transform)));
                if (count >= obstructionCount) continue;
                selected = candidate; obstructionCount = count;
                if (count == default(int)) break;
            }
            SetCamera(selected ?? throw new InvalidOperationException("専用Hの開始視点を計算できません。"));
        }
        private HCameraState FitCamera(Bounds bounds, Quaternion rotation, float two, float radiansPerDegree)
        {
            float vertical = Mathf.Tan(camera.fieldOfView * radiansPerDegree / two);
            float horizontal = vertical * camera.aspect;
            float distance = camera.nearClipPlane;
            var inverse = Quaternion.Inverse(rotation);
            foreach (float x in new[] { -Vector3.one.x, Vector3.one.x })
                foreach (float y in new[] { -Vector3.one.y, Vector3.one.y })
                    foreach (float z in new[] { -Vector3.one.z, Vector3.one.z })
                    {
                        var corner = inverse * Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                        distance = Mathf.Max(distance, Mathf.Max(Mathf.Abs(corner.x) / horizontal, Mathf.Abs(corner.y) / vertical) - corner.z);
                    }
            distance += camera.nearClipPlane * two;
            return new HCameraState { Position = bounds.center - rotation * Vector3.forward * distance, Rotation = rotation, FieldOfView = camera.fieldOfView };
        }
        private HCameraState CaptureCamera() => new HCameraState { Position = camera.transform.position, Rotation = camera.transform.rotation, FieldOfView = camera.fieldOfView };
        private HLightState CaptureLight() => light == null ? null : new HLightState { Color = light.color, Intensity = light.intensity, LocalRotation = light.transform.localRotation };
        private static void Finite(params float[] values)
        { foreach (var value in values) if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(values)); }
        private void SetCamera(HCameraState value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            Finite(value.Position.x, value.Position.y, value.Position.z, value.Rotation.x, value.Rotation.y, value.Rotation.z, value.Rotation.w, value.FieldOfView);
            if (value.FieldOfView <= default(float)) throw new ArgumentOutOfRangeException(nameof(value.FieldOfView));
            camera.transform.SetPositionAndRotation(value.Position, value.Rotation); camera.fieldOfView = value.FieldOfView;
        }
        private void SetLight(HLightState value)
        {
            if (light == null) { if (value != null) throw new InvalidOperationException("AI所有の照明は無効です。"); return; }
            if (value == null) throw new ArgumentNullException(nameof(value));
            Finite(value.Color.r, value.Color.g, value.Color.b, value.Color.a, value.Intensity, value.LocalRotation.x, value.LocalRotation.y, value.LocalRotation.z, value.LocalRotation.w);
            if (value.Intensity < default(float)) throw new ArgumentOutOfRangeException(nameof(value.Intensity));
            light.color = value.Color; light.intensity = value.Intensity; light.transform.localRotation = value.LocalRotation;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            try { if (camera != null) SetCamera(cameraBefore); }
            finally { if (light != null) SetLight(lightBefore); }
        }
    }
}
