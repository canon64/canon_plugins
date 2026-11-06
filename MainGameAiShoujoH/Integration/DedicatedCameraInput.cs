using System;
using System.Collections.Generic;
using System.Linq;
using MainGameAiShoujoH.Contracts;
using MainGameUiInputCapture;
using UnityEngine;

namespace MainGameAiShoujoH.Integration
{
    // Uses the game's existing camera input/evaluation loop. No new Update/LateUpdate.
    internal sealed class DedicatedCameraInput : ISessionModule, IMotionSelectionTransition
    {
        readonly List<Action> restore = new List<Action>();
        SessionContext context;
        NativeDedicatedCameraInput driver;
        Camera camera;
        HSceneControls controls;
        Vector3 beforePosition;
        Quaternion beforeRotation;
        float beforeFov;
        bool disposed;
        public string Name => "camera-input";
        public void Prepare(SessionContext value)
        {
            context = value;
            // Desktop pointer input must never take ownership of the native VR view.
            if (System.Diagnostics.Process.GetCurrentProcess().ProcessName.EndsWith("_VR", StringComparison.OrdinalIgnoreCase)) return;
            camera = value.Request.Camera ?? throw new InvalidOperationException("専用Hの描画カメラがありません。");
            beforePosition = camera.transform.position; beforeRotation = camera.transform.rotation; beforeFov = camera.fieldOfView;
            foreach (var previous in camera.GetComponents<BaseCameraControl_Ver2>().Where(c => !(c is NativeDedicatedCameraInput)))
            {
                var enabled = previous.enabled; var data = previous.GetCameraData();
                previous.enabled = false;
                restore.Add(() => { if (previous != null) { try { previous.SetCameraData(data); } finally { previous.enabled = enabled; } } });
            }
            driver = camera.gameObject.AddComponent<NativeDedicatedCameraInput>();
            driver.Initialize(camera, () => !disposed && context.CanInteract() && Application.isFocused);
            controls = value.Request.SceneControls;
            if (controls != null)
            {
                value.Request.SceneControls = new HSceneControls {
                    ReadCamera = controls.ReadCamera,
                    ApplyCamera = state => { controls.ApplyCamera(state); SynchronizePose(); },
                    ResetCamera = () => { controls.ResetCamera(); SynchronizePose(); },
                    ReadLight = controls.ReadLight, ApplyLight = controls.ApplyLight, ResetLight = controls.ResetLight
                };
            }
        }
        void SynchronizePose()
        {
            if (driver == null || camera == null) return;
            var actors = context.Request.Participants.Where(p => p.Character != null).Select(p => p.Character.transform.position).ToArray();
            var pivot = actors.Length == default(int) ? camera.transform.position + camera.transform.forward : actors.Aggregate(Vector3.zero, (a, b) => a + b) / actors.Length;
            driver.AdoptPose(camera, pivot);
        }
        public void ValidateMotionSelection(string id, bool automatic) { }
        public void SuspendSelection() { if (driver != null) driver.enabled = false; }
        public void ResumeSelection()
        {
            if (driver == null || disposed || !context.CanInteract()) return;
            SynchronizePose(); driver.enabled = true;
        }
        public void Execute(Command command) { }
        public void End(EndReason reason) { SuspendSelection(); }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (driver != null) { driver.Release(); UnityEngine.Object.Destroy(driver); }
            if (context != null && controls != null) context.Request.SceneControls = controls;
            foreach (var action in Enumerable.Reverse(restore)) try { action(); } catch (Exception error) { context.ReportFailure("カメラ入力復元: " + error.Message); }
            restore.Clear();
            if (camera != null) { camera.transform.SetPositionAndRotation(beforePosition, beforeRotation); camera.fieldOfView = beforeFov; }
        }
    }

    internal sealed class NativeDedicatedCameraInput : BaseCameraControl_Ver2
    {
        // Only initialize native bookkeeping here; inherited native LateUpdate owns input.
        private new void Start() { }
        internal void Initialize(Camera camera, Func<bool> allowed)
        {
            enabled = false; thisCmaera = camera; isInit = true;
            isDrags = new bool[CONFIG_SIZE];
            viewCollider = gameObject.AddComponent<CapsuleCollider>();
            // Base CameraUpdate writes its view-volume bookkeeping; it has no physics role here.
            viewCollider.enabled = false;
            NoCtrlCondition = () => !allowed() || UiInputCaptureApi.IsMouseInputBlocked();
            // H uses the wheel for native progression. Native right-drag still
            // performs vertical translation/dolly zoom through InputMouseProc.
            ZoomCondition = () => false;
            KeyCondition = () => allowed() && !UiInputCaptureApi.IsAnyActive() && GUIUtility.keyboardControl == default(int);
        }
        internal void AdoptPose(Camera camera, Vector3 pivot)
        {
            float distance = Mathf.Max(camera.nearClipPlane, Vector3.Dot(pivot - camera.transform.position, camera.transform.forward));
            transBase = null; targetObj = null;
            CamDat = new CameraData { Pos = camera.transform.position + camera.transform.forward * distance, Dir = Vector3.back * distance, Rot = camera.transform.eulerAngles, Fov = camera.fieldOfView };
            CamReset.Copy(CamDat, camera.transform.rotation);
            SetCameraData(CamDat);
        }
        internal void Release()
        {
            enabled = false;
            NoCtrlCondition = () => true; ZoomCondition = () => false; KeyCondition = () => false;
            if (viewCollider != null) UnityEngine.Object.Destroy(viewCollider);
        }
    }
}
