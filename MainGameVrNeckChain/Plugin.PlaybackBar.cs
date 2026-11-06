using System;
using CanonPlugins.Shared;
using UnityEngine;
using VRGIN.Core;

namespace MainGameVrNeckChain
{
    public sealed partial class Plugin
    {
        private readonly GadgetBarCycle _barCycle = new GadgetBarCycle();
        private bool _barGizmoRequested;
        public string GetPlaybackBarActionHint() =>
            "左クリック：初回だけ持ち手をカメラ前に呼び出し。その後はギズモ表示／非表示を切り替えます。\n右クリック：首輪と鎖を削除します。削除後は呼び出しから再開します。\n次の左クリック：" +
            (_chain == null ? "持ち手をカメラ前に呼び出し" : _desktopGizmo != null && _desktopGizmo.IsVisible ? "ギズモ非表示" : "ギズモ表示");
        public void PlaybackBarPrimaryAction()
        {
            if (!PluginSessionGate.Allowed || !Application.isFocused) return;
            _barCycle.Press(_chain != null, RecallFromPlaybackBar, ShowGizmoFromPlaybackBar,
                () => SetChainGizmosVisible(false), _desktopGizmo != null && _desktopGizmo.IsVisible);
        }
        public void PlaybackBarDelete() { ClearChain(); }
        private bool RecallFromPlaybackBar()
        {
            if (_chain == null) SpawnChain();
            if (_chain == null) return false;
            var view = VR.Active && VR.Camera != null ? VR.Camera.Head : Camera.main != null ? Camera.main.transform : null;
            if (view == null) throw new InvalidOperationException("配置用カメラがありません。");
            _barGizmoRequested = false;
            DetachDesktopGizmo(); DetachRootGizmo(); DetachCollarGizmo();
            ReleaseGrip(); ReleaseRootGrip(); ReleaseCollarGrip();
            SetChainGizmosVisible(false);
            _releasedTipPosition = view.position + view.forward * .5f;
            _releasedTipPinned = true;
            InitializeSimulation(); _customSimulation = true; _dynamicBone.enabled = false;
            SolveChain();
            return true;
        }
        private bool ShowGizmoFromPlaybackBar()
        {
            ReleaseGrip();
            _barGizmoRequested = true; _desktopGizmoEnabled = true;
            SetChainGizmosVisible(true);
            try
            {
                UpdateDesktopGizmo();
                if (_desktopGizmo == null) throw new InvalidOperationException("ギズモを表示できません。共通ギズモの許可を確認してください。");
                _desktopGizmo.SetVisible(true);
            }
            catch { _barGizmoRequested = false; DetachDesktopGizmo(); throw; }
            return true;
        }
    }
}
