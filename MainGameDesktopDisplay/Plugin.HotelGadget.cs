using System;
using System.Linq;
using CanonPlugins.Shared;
using UnityEngine;
namespace MainGameDesktopDisplay
{
    public sealed partial class Plugin
    {
        [Serializable] private sealed class HotelDisplay { public bool paused; }
        public bool HotelGadgetAllowed => CanAct;
        public void SetHotelGadgetVisible(bool visible)
        {
            if (!CanAct) throw new InvalidOperationException("ディスプレイはこのHでは許可されていません。");
            if (visible && _root == null && !Recall()) throw new InvalidOperationException("ディスプレイを生成できません。");
            else if (!visible) PlaybackBarDelete();
        }
        public string CaptureHotelGadget(Transform room)
        {
            var data = GadgetRoomSnapshot.Capture(room, _root == null ? null : _root.transform);
            data.settings = GadgetConfigSnapshot.Capture(Config); data.runtime = JsonUtility.ToJson(new HotelDisplay { paused = _paused });
            return data.Encode();
        }
        public void RestoreHotelGadget(Transform room, string json)
        {
            if (!CanAct) throw new InvalidOperationException("ディスプレイはこのHでは許可されていません。");
            var data = GadgetRoomSnapshot.Decode(json);
            if (data.visible && data.poses.Length != 1) throw new ArgumentException("ディスプレイの配置データが不正です。");
            var state = string.IsNullOrEmpty(data.runtime) ? null : JsonUtility.FromJson<HotelDisplay>(data.runtime);
            string beforeSettings = GadgetConfigSnapshot.Capture(Config);
            try
            {
                GadgetConfigSnapshot.Apply(Config, data.settings);
                if (!data.visible) { SetHotelGadgetVisible(false); return; }
                // Restore the actual producer and panel, including when a display already exists.
                // Normal monitor recall may load its assigned preset; an explicit room snapshot
                // already contains the selected width/clip and must take precedence over it.
                InitializeDisplayPresets(); RefreshMonitors(false, true);
                // A selected monitor may bind its dynamic Clip key for the first time above.
                GadgetConfigSnapshot.Apply(Config, data.settings); LoadMonitorClip();
                ReleaseDisplayGrab(); HideGizmo(); SetPointerActive(false);
                if (_root == null) CreateDisplay();
                if (!StartCapture()) throw new InvalidOperationException("保存したディスプレイを再開できません：" + _status);
                data.poses.Single().Apply(room, _root.transform);
            }
            catch
            {
                // Missing monitors are rejected before replacing the current producer. Preserve
                // the original configuration; the host also restores the full prior snapshot.
                GadgetConfigSnapshot.Apply(Config, beforeSettings);
                throw;
            }
            if (state != null) _paused = state.paused;
            if (_driver != null) _driver.enabled = !_paused; SyncDesktopMouse();
        }
        public bool VerifyHotelGadgetState(Transform room, string json)
        {
            var data = GadgetRoomSnapshot.Decode(json);
            if (!data.visible) return _root == null;
            var state = string.IsNullOrEmpty(data.runtime) ? null : JsonUtility.FromJson<HotelDisplay>(data.runtime);
            if (_root == null || _capture == null || _driver == null || _panelTransform == null || _bodyTransform == null ||
                _monitors.Length == 0 || _monitor < 0 || _monitor >= _monitors.Length) return false;
            if (!string.IsNullOrEmpty(_monitorDevice.Value) && MonitorKey(_monitors[_monitor].Device) != MonitorKey(_monitorDevice.Value)) return false;
            if (state != null && _paused != state.paused) return false;
            if (_driver.enabled == _paused) return false;
            var source = _capture.Source; var expected = _clip.Bounds;
            if (source.Left != expected.Left || source.Top != expected.Top || source.Right != expected.Right || source.Bottom != expected.Bottom) return false;
            double ratio = Math.Min(1d, (double)_resolution.Value / Math.Max(expected.Width, expected.Height));
            if (_capture.Width != Math.Max(1, (int)Math.Round(expected.Width * ratio)) || _capture.Height != Math.Max(1, (int)Math.Round(expected.Height * ratio))) return false;
            float width = _width.Value * _clip.Width, height = _width.Value / _screenAspect * _clip.Height;
            return Mathf.Abs(_panelTransform.localScale.x - width) < .0001f && Mathf.Abs(_panelTransform.localScale.y - height) < .0001f &&
                Mathf.Abs(_bodyTransform.localScale.x - width - .04f) < .0001f && Mathf.Abs(_bodyTransform.localScale.y - height - .04f) < .0001f;
        }
    }
}
