using UnityEngine;
using Valve.VR;
using VRGIN.Controls;
using VRGIN.Core;

namespace MainGameClubLights
{
    public sealed partial class Plugin
    {
        // グラブ中のエントリ（null = 未グラブ）
        private LightEntry _vrGrabbedEntry;
        private Transform  _vrGrabbedController;
        private Vector3    _vrGrabOffset;

        private void UpdateVRLightGrab()
        {
            if (!VR.Active || VR.Mode == null) return;

            var right = VR.Mode.Right;
            var left  = VR.Mode.Left;

            if (_vrGrabbedEntry != null)
            {
                // グラブ中
                UpdateGrabbedLight();
                CheckGrabRelease(right, left);
            }
            else
            {
                // グラブ開始チェック（右→左の優先順）
                if (right != null && TryBeginGrab(right)) return;
                if (left  != null && TryBeginGrab(left))  return;
            }
        }

        private bool TryBeginGrab(Controller ctrl)
        {
            if (!ctrl.Input.GetPressDown(EVRButtonId.k_EButton_Grip)) return false;

            Transform ctrlTf = ((Component)ctrl).transform;
            float minDist    = float.MaxValue;
            LightEntry best  = null;

            foreach (var entry in _lightEntries)
            {
                if (entry.Go == null) continue;
                float dist = Vector3.Distance(ctrlTf.position, entry.Go.transform.position);
                if (dist < 0.3f && dist < minDist)
                {
                    minDist = dist;
                    best    = entry;
                }
            }

            if (best == null) return false;

            // グラブ開始 → 自由配置モードに切替
            if (best.Settings.FollowCamera)
                SetLightFollowCamera(best, false);

            _vrGrabbedEntry      = best;
            _vrGrabbedController = ctrlTf;
            _vrGrabOffset        = best.Go.transform.position - ctrlTf.position;
            _log.Info($"[VRGrab] start id={best.Settings.Id}");
            return true;
        }

        private void UpdateGrabbedLight()
        {
            if (_vrGrabbedEntry?.Go == null || _vrGrabbedController == null)
            {
                _vrGrabbedEntry = null;
                return;
            }
            _vrGrabbedEntry.Go.transform.position = _vrGrabbedController.position + _vrGrabOffset;
        }

        private void CheckGrabRelease(Controller right, Controller left)
        {
            bool released = false;
            if (right != null && right.Input.GetPressUp(EVRButtonId.k_EButton_Grip)) released = true;
            if (left  != null && left.Input.GetPressUp(EVRButtonId.k_EButton_Grip))  released = true;

            if (!released) return;

            if (_vrGrabbedEntry != null)
            {
                var pos = _vrGrabbedEntry.Go.transform.position;
                _vrGrabbedEntry.Settings.WorldPosX = pos.x;
                _vrGrabbedEntry.Settings.WorldPosY = pos.y;
                _vrGrabbedEntry.Settings.WorldPosZ = pos.z;
                _log.Info($"[VRGrab] end id={_vrGrabbedEntry.Settings.Id} pos={pos}");
            }
            _vrGrabbedEntry      = null;
            _vrGrabbedController = null;
        }
    }
}
