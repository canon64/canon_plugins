using System;
using BepInEx.Configuration;
using CanonPlugins.Shared;
using UnityEngine;
using VRGIN.Controls;
using VRGIN.Core;

namespace MainGameDesktopDisplay
{
    public sealed partial class Plugin
    {
        private ConfigEntry<bool> _mouseEnabled;
        private DesktopButtonInput _mouseButtons;
        private IDesktopMouse _desktopMouse = new WindowsDesktopMouse();
        private Controller _mouseHand;
        private Controller.Lock _mouseLock;
        private bool _mouseLeft, _mouseRight, _mouseReady;
        private bool _mouseInputActive, _mouseTracked;
        private Valve.VR.SteamVR_Behaviour_Pose _mousePose;
        private GameObject _mouseRay;
        private LineRenderer _mouseLine;
        private DesktopPointerDriver _pointerDriver;
        private Transform _mouseAim;
        private bool _pointerFaulted;
        private Vector3 _lineStart, _lineHit;
        private bool _lineSet;
        private Material _mouseRayMaterial;
        private int _scrollX, _scrollY;
        private DesktopCapture.Bounds _mouseBounds;
        private bool CanMouse => CanAct && _grabVrFocused && _mouseEnabled.Value && _mouseReady && !_paused && !_pointerFaulted && _grabHand == null && _root != null && _mouseHand != null && _mouseHand.IsTracking && _mouseTracked;

        private void ConnectDesktopMouse()
        {
            if (!VR.Active || _mouseButtons != null || VR.Mode == null) return;
            _mouseHand = VR.Mode.Right;
            _mouseAim = _mouseHand != null ? _mouseHand.transform : null;
            _mouseTracked = _mouseHand != null && _mouseHand.IsTracking;
            _mousePose = _mouseHand?.Tracking;
            if (_mousePose != null)
            {
                _mousePose.onConnectedChangedEvent += MouseHandConnected;
                _mousePose.onTrackingChangedEvent += MouseHandTracking;
            }
            _mouseButtons = new DesktopButtonInput(DesktopButton, DesktopScroll);
            try { _mouseButtons.Connect(); }
            catch (Exception ex) { _mouseButtons.Dispose(); _mouseButtons = null; _status = "PCマウス入力：" + ex.Message; }
        }
        private void SyncDesktopMouse()
        {
            bool eligible = CanMouse && _mouseButtons != null;
            if (_pointerDriver != null && _pointerDriver.enabled != eligible) _pointerDriver.enabled = eligible;
            if (!eligible) SetPointerActive(false);
        }
        private void SetPointerActive(bool active)
        {
            if (!active && (_mouseInputActive || _mouseLock != null || _mouseLeft || _mouseRight)) ReleaseDesktopMouse();
            if (_mouseInputActive != active)
            {
                _mouseInputActive = active;
                try { _mouseButtons?.Active(active); }
                catch (Exception ex) { _mouseInputActive = false; _status = "PCマウス入力切替：" + ex.Message; }
                if (_mouseRay != null) _mouseRay.SetActive(_mouseInputActive);
            }
        }
        private void TickDesktopPointer()
        {
            if (!CanMouse) { SyncDesktopMouse(); return; }
            if (!TryAimDesktop(out int x, out int y, out Vector3 start, out Vector3 hit)) { SetPointerActive(false); return; }
            try
            {
                // Do not reflect/acquire repeatedly while another native tool owns this hand.
                if ((_mouseLock == null || !_mouseLock.IsValid) && !_mouseHand.CanAcquireFocus()) { SetPointerActive(false); return; }
                if (!AcquireMouseFocus()) { SetPointerActive(false); return; }
                SetPointerActive(true);
                if (!_mouseInputActive) return;
                if (!_lineSet || (start - _lineStart).sqrMagnitude > 0 || (hit - _lineHit).sqrMagnitude > 0)
                {
                    _mouseLine.SetPosition(0, start); _mouseLine.SetPosition(1, hit);
                    _lineStart = start; _lineHit = hit; _lineSet = true;
                }
                _desktopMouse.Move(x, y);
            }
            catch (Exception ex) { PointerFailed(ex); }
        }
        private void PointerFailed(Exception ex)
        {
            _pointerFaulted = true; SyncDesktopMouse();
            _status = "PCマウス入力停止：" + ex.Message;
        }
        private void CreateMouseRay()
        {
            if (_mouseRay != null || _mouseHand == null || _mouseButtons == null) return;
            _mouseRay = new GameObject("DesktopPointerRay");
            _mouseRay.transform.SetParent(_mouseHand.transform, false);
            var line = _mouseLine = _mouseRay.AddComponent<LineRenderer>();
            line.useWorldSpace = true; line.positionCount = 2;
            line.startWidth = line.endWidth = .002f;
            _mouseRay.SetActive(false);
            _mouseRayMaterial = new Material(Shader.Find("Unlit/Color")) { color = new Color(0, 1, 1) };
            line.sharedMaterial = _mouseRayMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            _pointerDriver = _root.AddComponent<DesktopPointerDriver>();
            _pointerDriver.enabled = false;
            _pointerDriver.Tick = TickDesktopPointer;
            _pointerDriver.Stop = StopDesktopPointer;
            SyncDesktopMouse();
        }
        private void StopDesktopPointer() => SetPointerActive(false);
        private void DestroyMouseRay()
        {
            SetPointerActive(false);
            if (_pointerDriver != null) { _pointerDriver.enabled = false; _pointerDriver.Tick = null; _pointerDriver.Stop = null; }
            if (_mouseRay != null) { _mouseRay.SetActive(false); Destroy(_mouseRay); }
            if (_mouseRayMaterial != null) Destroy(_mouseRayMaterial);
            _mouseRay = null; _mouseRayMaterial = null; _mouseLine = null; _pointerDriver = null;
            _lineSet = false;
        }
        private void MouseHandConnected(Valve.VR.SteamVR_Behaviour_Pose pose, Valve.VR.SteamVR_Input_Sources source, bool connected)
        { if (pose == _mousePose) { _mouseTracked = connected; if (!connected) ReleaseDesktopMouse(); SyncDesktopMouse(); } }
        private void MouseHandTracking(Valve.VR.SteamVR_Behaviour_Pose pose, Valve.VR.SteamVR_Input_Sources source, Valve.VR.ETrackingResult tracking)
        { if (pose == _mousePose) { _mouseTracked = tracking == Valve.VR.ETrackingResult.Running_OK; if (!_mouseTracked) ReleaseDesktopMouse(); SyncDesktopMouse(); } }
        private void DisconnectDesktopMouse()
        {
            ReleaseDesktopMouse();
            if (_mousePose != null)
            {
                _mousePose.onConnectedChangedEvent -= MouseHandConnected;
                _mousePose.onTrackingChangedEvent -= MouseHandTracking;
            }
            _mousePose = null; DestroyMouseRay(); _mouseInputActive = false;
            try { CleanupGrab(() => _mouseButtons?.Dispose()); }
            finally { _mouseButtons = null; _mouseHand = null; _mouseAim = null; }
        }
        private void MouseSettingChanged(object sender, EventArgs args)
        { _pointerFaulted = false; if (!_mouseEnabled.Value) ReleaseDesktopMouse(); SyncDesktopMouse(); }

        private bool AimDesktop(out int x, out int y)
            => TryAimDesktop(out x, out y, out _, out _);
        private bool TryAimDesktop(out int x, out int y, out Vector3 start, out Vector3 hit)
        {
            x = y = 0;
            start = hit = default;
            if (!CanMouse || _panelTransform == null || _mouseAim == null) return false;
            start = _mouseAim.position;
            Vector3 forward = _mouseAim.forward;
            Vector3 origin = _panelTransform.InverseTransformPoint(start);
            Vector3 direction = _panelTransform.InverseTransformPoint(start + forward) - origin;
            if (origin.z >= 0 || direction.z <= .00001f) return false;
            float distance = -origin.z / direction.z;
            if (distance < 0 || distance > .5f) return false;
            Vector3 point = origin + direction * distance;
            if (Mathf.Abs(point.x) > .5f || Mathf.Abs(point.y) > .5f) return false;
            hit = start + forward * distance;
            MapDesktopPoint(_mouseBounds, point.x + .5f, .5f - point.y, out x, out y);
            return true;
        }
        internal static void MapDesktopPoint(DesktopCapture.Bounds bounds, float u, float v, out int x, out int y)
        {
            x = bounds.Left + (int)Math.Round(Mathf.Clamp(u, 0, 1) * Math.Max(0, bounds.Width - 1));
            y = bounds.Top + (int)Math.Round(Mathf.Clamp(v, 0, 1) * Math.Max(0, bounds.Height - 1));
        }
        private bool AcquireMouseFocus()
        {
            if (_mouseLock != null && _mouseLock.IsValid && !_mouseLock.IsInvalidating) return true;
            _mouseLock = null;
            return VrItemFocus.TryAcquire(_mouseHand, out _mouseLock);
        }
        private void DesktopButton(bool right, bool down)
        {
            bool pressed = right ? _mouseRight : _mouseLeft;
            if (down && pressed) return;
            if (!down && !pressed) return;
            try
            {
                if (down)
                {
                    if (!_mouseInputActive || !AimDesktop(out int x, out int y) || !AcquireMouseFocus()) { SetPointerActive(false); return; }
                    _desktopMouse.Move(x, y);
                    _desktopMouse.Button(right, true);
                    if (right) _mouseRight = true; else _mouseLeft = true;
                }
                else
                {
                    // Always send Up, including when the ray has just left the board.
                    if (_mouseLock != null && _mouseLock.IsValid && AimDesktop(out int x, out int y)) _desktopMouse.Move(x, y);
                    _desktopMouse.Button(right, false);
                    if (right) _mouseRight = false; else _mouseLeft = false;
                }
            }
            catch (Exception ex) { PointerFailed(ex); }
        }
        private void DesktopScroll(Vector2 axis)
        {
            if (Mathf.Abs(axis.x) < .25f) _scrollX = 0;
            if (Mathf.Abs(axis.y) < .25f) _scrollY = 0;
            if (!CanMouse || !_mouseInputActive) { ReleaseDesktopMouse(); return; }
            int x = axis.x >= .55f ? 1 : axis.x <= -.55f ? -1 : 0;
            int y = axis.y >= .55f ? 1 : axis.y <= -.55f ? -1 : 0;
            int horizontal = x != _scrollX ? x : 0, vertical = y != _scrollY ? y : 0;
            if (horizontal == 0 && vertical == 0)
            { return; }
            try
            {
                if (!AimDesktop(out int px, out int py) || !AcquireMouseFocus()) { SetPointerActive(false); return; }
                _desktopMouse.Move(px, py); _desktopMouse.Scroll(horizontal, vertical);
                if (x != 0) _scrollX = x; if (y != 0) _scrollY = y;
            }
            catch (Exception ex) { PointerFailed(ex); }
        }
        private void ReleaseMouseFocus()
        {
            var focus = _mouseLock; _mouseLock = null;
            if (focus != null && focus.IsValid) CleanupGrab(() => focus.Release());
        }
        private void ReleaseDesktopMouse()
        {
            _mouseLeft = _mouseRight = false; _scrollX = _scrollY = 0;
            try { _desktopMouse.ReleaseAll(); }
            catch (Exception ex) { _status = "PCマウス解放：" + ex.Message; }
            finally { ReleaseMouseFocus(); }
        }
    }
    public sealed class DesktopPointerDriver : MonoBehaviour
    {
        internal Action Tick, Stop;
        private void LateUpdate() { Tick?.Invoke(); }
        private void OnDisable() { Stop?.Invoke(); }
    }
}
