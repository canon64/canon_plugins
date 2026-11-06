using System;
using CanonPlugins.Shared;
using UnityEngine;
using VRGIN.Core;

namespace MainGameVrDildo
{
    public sealed partial class Plugin
    {
        private bool _barControlsGizmo, _barGizmoRequested;
        public string GetPlaybackBarActionHint() =>
            "左クリック：初回だけカメラ前に呼び出し。その後はギズモ表示／非表示だけを切り替えます。\n右クリック：削除。削除後は呼び出しから再開します。\n次の左クリック：" +
            (_model == null ? "カメラ前に呼び出し" : _barGizmoRequested ? "ギズモ非表示" : "ギズモ表示");
        public void PlaybackBarPrimaryAction()
        {
            if (!PluginSessionGate.Allowed || !Application.isFocused || _settings == null || !_settings.Enabled) return;
            if (_model == null) RecallFromPlaybackBar();
            else if (_barGizmoRequested) { _barControlsGizmo = true; _barGizmoRequested = false; DetachDesktopGizmo(); }
            else ShowGizmoFromPlaybackBar();
        }
        public void PlaybackBarDelete() { _autoSpawnAttempted = true; DeleteModel(); }
        private bool RecallFromPlaybackBar()
        {
            _autoSpawnAttempted = true;
            if (_model == null) Spawn();
            if (_model == null) return false;
            var view = VR.Active && VR.Camera != null ? VR.Camera.Head : Camera.main != null ? Camera.main.transform : null;
            if (view == null) throw new InvalidOperationException("配置用カメラがありません。");
            _barControlsGizmo = true; _barGizmoRequested = false;
            DetachDesktopGizmo(); ReleaseGrab(); StopPiston();
            TrySetComposerParent(null, false);
            _model.transform.SetPositionAndRotation(view.position + view.forward * _settings.SpawnDistance, view.rotation);
            ResetAibuMotion();
            return true;
        }
        private bool ShowGizmoFromPlaybackBar()
        {
            _barControlsGizmo = true; _barGizmoRequested = true;
            try
            {
                UpdateDesktopGizmo();
                if (_grabController == null)
                {
                    if (_desktopGizmo == null) throw new InvalidOperationException("ギズモを表示できません。共通ギズモの許可を確認してください。");
                    _desktopGizmo.SetVisible(true);
                }
            }
            catch { _barGizmoRequested = false; DetachDesktopGizmo(); throw; }
            return true;
        }
    }
}
