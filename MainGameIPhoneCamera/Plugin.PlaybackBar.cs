using System;
using CanonPlugins.Shared;
using MainGameTransformGizmo;
using UnityEngine;

namespace MainGameIPhoneCamera
{
    public sealed partial class Plugin
    {
        private readonly GadgetBarCycle _barCycle = new GadgetBarCycle();
        private TransformGizmo _phoneGizmo;

        public void PlaybackBarPrimaryAction()
        {
            if (!CanAct || !Application.isFocused) return;
            _barCycle.Press(_root != null, RecallFromPlaybackBar, ShowGizmoFromPlaybackBar, HidePhoneGizmo, _phoneGizmo != null && _phoneGizmo.IsVisible);
        }

        private bool RecallFromPlaybackBar()
        {
            SpawnPhone();
            return _root != null;
        }

        private bool ShowGizmoFromPlaybackBar()
        {
            if (_root == null) return false;
            ReleasePhone();
            try
            {
                if (!TransformGizmoApi.IsAvailable)
                    throw new InvalidOperationException("共通ギズモが利用できません。");
                if (_phoneGizmo == null) _phoneGizmo = TransformGizmoApi.Attach(_root);
                if (_phoneGizmo == null)
                    throw new InvalidOperationException("共通ギズモの許可を確認してください。");
                _phoneGizmo.enabled = true;
                if (!TransformGizmoApi.EnableCameraInputCaptureOnDrag(_phoneGizmo))
                    throw new InvalidOperationException("ギズモの入力保護を開始できません。");
                TransformGizmoApi.SetSizeMultiplier(_phoneGizmo, .35f);
                _phoneGizmo.SetMode(GizmoMode.Move);
                _phoneGizmo.SetVisible(true);
                if (!_phoneGizmo.IsVisible)
                    throw new InvalidOperationException("ギズモを表示できません。");
                _status = "ギズモ表示：iPhoneを移動できます。次の左クリックでギズモを非表示にします。";
                return true;
            }
            catch (Exception ex)
            {
                HidePhoneGizmo();
                _status = "ギズモ表示失敗：" + ex.Message;
                return false;
            }
        }

        private void HidePhoneGizmo()
        {
            if (_phoneGizmo == null) return;
            try { _phoneGizmo.SetVisible(false); }
            finally
            {
                TransformGizmoApi.DisableCameraInputCaptureOnDrag(_phoneGizmo);
                _phoneGizmo.enabled = false;
            }
        }

        private void ClearPhoneGizmo()
        {
            try { HidePhoneGizmo(); }
            finally { _phoneGizmo = null; _barCycle.Reset(); }
        }

        public void PlaybackBarDelete()
        {
            DestroyPhone();
            _status = "非表示：表示ボタンで目の前に出せます。";
        }

        public string GetPlaybackBarActionHint() =>
            "左クリック：初回だけiPhoneを目の前に呼び出し。その後はギズモ表示／非表示を切り替えます。\n右クリック：削除。削除後は呼び出しから再開します。\n次の左クリック：" +
            (_root == null ? "目の前に呼び出し" : _phoneGizmo != null && _phoneGizmo.IsVisible ? "ギズモ非表示" : "ギズモ表示");
    }
}
