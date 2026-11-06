using System;
using System.Collections.Generic;
using System.Linq;
using MainGameUiInputCapture;
using UnityEngine;

namespace MainGameAiShoujoH.Integration
{
    // The original CameraControl_Ver2 owns input/evaluation. No frame wrapper.
    internal sealed class NativeKksCameraLease : IDisposable
    {
        readonly Camera camera;
        readonly Transform parent;
        readonly Vector3 position, scale;
        readonly Quaternion rotation;
        readonly float fov;
        readonly int cullingMask;
        readonly Component[] components;
        readonly List<Action> restore = new List<Action>();
        internal CameraControl_Ver2 Driver { get; }
        bool disposed;
        internal NativeKksCameraLease(Camera camera, CameraControl_Ver2 nativeSettings, Func<bool> canInteract)
        {
            this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
            if (nativeSettings == null) throw new ArgumentNullException(nameof(nativeSettings));
            parent = camera.transform.parent; position = camera.transform.position; rotation = camera.transform.rotation;
            scale = camera.transform.localScale; fov = camera.fieldOfView;
            cullingMask = camera.cullingMask;
            components = camera.GetComponents<Component>();
            foreach (var control in camera.GetComponents<BaseCameraControl_Ver2>())
            {
                var enabled = control.enabled; var data = control.GetCameraData(); control.enabled = false;
                restore.Add(() => { if (control != null) { try { control.SetCameraData(data); } finally { control.enabled = enabled; } } });
            }
            try
            {
                Driver = camera.gameObject.AddComponent<CameraControl_Ver2>(); Driver.enabled = false;
                // This copies an actual native Unity component's serialized settings,
                // not a managed DTO or an invented camera configuration.
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(nativeSettings), Driver);
                var setter = typeof(BaseCameraControl_Ver2).GetProperty(nameof(BaseCameraControl_Ver2.thisCmaera))?.GetSetMethod(true)
                    ?? throw new MissingMethodException("Native camera binding setter is missing.");
                setter.Invoke(Driver, new object[] { camera });
                Driver.NoCtrlCondition = () => disposed || !canInteract() || !Application.isFocused || UiInputCaptureApi.IsMouseInputBlocked();
                Driver.KeyCondition = () => !disposed && canInteract() && !UiInputCaptureApi.IsAnyActive() && GUIUtility.keyboardControl == default(int);
                Driver.enabled = true; // Native Start initializes its own input/physics state once.
            }
            catch { Dispose(); throw; }
        }
        internal void Suspend() { if (Driver != null) Driver.enabled = false; }
        internal void Resume() { if (!disposed && Driver != null) Driver.enabled = true; }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            var failures = new List<Exception>();
            Action<Action> attempt = action => { try { action(); } catch (Exception error) { failures.Add(error); } };
            attempt(() => { if (Driver != null) { Driver.enabled = false; Driver.NoCtrlCondition = () => true; UnityEngine.Object.Destroy(Driver); } });
            // Native Start adds these components. Remove only new components on
            // this exact borrowed camera, retaining every pre-existing collider.
            attempt(() => { if (camera != null) foreach (var component in camera.GetComponents<Component>().Except(components))
                if (component is CapsuleCollider || component is Rigidbody) UnityEngine.Object.Destroy(component); });
            attempt(() => { if (camera != null) { camera.transform.SetParent(parent, true); camera.transform.SetPositionAndRotation(position, rotation); camera.transform.localScale = scale; camera.fieldOfView = fov; camera.cullingMask = cullingMask; } });
            foreach (var action in Enumerable.Reverse(restore)) attempt(action);
            if (failures.Count != default(int)) throw new AggregateException("Native camera restoration failed.", failures);
        }
    }
}
