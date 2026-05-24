using System;
using MainGameTransformGizmo;
using UnityEngine;
using Valve.VR;
using VRGIN.Controls;
using VRGIN.Core;

namespace MainGameClubLights
{
    // 全ライトをぶら下げる「フォルダ」（親オブジェクト）。
    // フォルダを動かすと子ライトが一括で追従する（Unity transform 階層）。
    public sealed partial class Plugin
    {
        private GameObject     _folderGo;
        private GameObject     _folderHandle;
        private Renderer       _folderHandleRenderer;
        private TransformGizmo _folderGizmo;

        // フォルダのVR掴み
        private bool            _folderVrGrabbed;
        private Controller      _folderVrController;
        private Controller.Lock _folderVrFocusLock = Controller.Lock.Invalid;
        private Vector3         _folderVrGrabOffset;

        internal Transform FolderTransform => _folderGo != null ? _folderGo.transform : null;

        private void EnsureFolder()
        {
            if (_folderGo != null) return;

            _folderGo = new GameObject("ClubLightsFolder");
            _folderGo.transform.position = new Vector3(_settings.FolderPosX, _settings.FolderPosY, _settings.FolderPosZ);
            _folderGo.transform.rotation = Quaternion.Euler(_settings.FolderRotX, _settings.FolderRotY, _settings.FolderRotZ);

            // 掴みハンドル（黄色い球マーカー。フォルダは実体が無いので位置を可視化）
            var handle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            handle.name = "FolderHandle";
            handle.transform.SetParent(_folderGo.transform, worldPositionStays: false);
            handle.transform.localPosition = Vector3.zero;
            handle.transform.localScale    = Vector3.one * Mathf.Max(0.02f, _settings.FolderHandleSize);
            var col = handle.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var mr = handle.GetComponent<Renderer>();
            if (mr != null)
            {
                mr.material = new Material(Shader.Find("Unlit/Color"));
                mr.material.color = new Color(1f, 0.85f, 0.2f, 1f);
            }
            handle.layer = 0;
            handle.SetActive(_settings.ShowFolderHandle);
            _folderHandle = handle;
            _folderHandleRenderer = mr;

            // 通常モード用ギズモ
            if (TransformGizmoApi.IsAvailable && TransformGizmoApi.TryAttach(_folderGo, out _folderGizmo) && _folderGizmo != null)
            {
                _folderGizmo.DragStateChanged += OnGizmoDragStateChanged;
                _folderGizmo.SetVisible(_settings.ShowFolderHandle);
            }
            _log.Info($"[Folder] created pos={_folderGo.transform.position} showHandle={_settings.ShowFolderHandle} gizmoNull={_folderGizmo == null}");
        }

        private void DestroyFolder()
        {
            StopFolderVrGrab();
            if (_folderGizmo != null)
            {
                _folderGizmo.DragStateChanged -= OnGizmoDragStateChanged;
                Destroy(_folderGizmo);
                _folderGizmo = null;
            }
            if (_folderGo != null)
                Destroy(_folderGo);
            _folderGo = null;
            _folderHandle = null;
            _folderHandleRenderer = null;
        }

        // 毎フレーム: ハンドル/ギズモ表示同期 ＋ フォルダ transform を設定へ書き戻し
        private void UpdateFolder()
        {
            if (_folderGo == null) return;

            if (_folderHandle != null)
            {
                if (_folderHandle.activeSelf != _settings.ShowFolderHandle)
                    _folderHandle.SetActive(_settings.ShowFolderHandle);
                _folderHandle.transform.localScale = Vector3.one * Mathf.Max(0.02f, _settings.FolderHandleSize);
            }
            if (_folderGizmo != null)
                _folderGizmo.SetVisible(_settings.ShowFolderHandle);

            Vector3 p = _folderGo.transform.position;
            Vector3 e = _folderGo.transform.rotation.eulerAngles;
            _settings.FolderPosX = p.x; _settings.FolderPosY = p.y; _settings.FolderPosZ = p.z;
            _settings.FolderRotX = e.x; _settings.FolderRotY = e.y; _settings.FolderRotZ = e.z;
        }

        internal void ResetFolderTransform()
        {
            if (_folderGo == null) return;
            _folderGo.transform.position = Vector3.zero;
            _folderGo.transform.rotation = Quaternion.identity;
            UpdateFolder();
            SaveSettingsNow("folder-reset");
        }

        // ── VR掴み（フォルダ） ───────────────────────────────────────────────

        private bool TryBeginFolderGrab(Controller ctrl)
        {
            if (!_settings.ShowFolderHandle) return false;
            if (_folderGo == null || ctrl == null || ctrl.Input == null) return false;
            if (!ctrl.Input.GetPressDown(EVRButtonId.k_EButton_Grip)) return false;

            Transform ctrlTf = ((Component)ctrl).transform;
            float dist = Vector3.Distance(ctrlTf.position, _folderGo.transform.position);
            if (dist > Mathf.Max(0.3f, _settings.FolderHandleSize)) return false;

            if (!ctrl.TryAcquireFocus(out Controller.Lock focusLock) || focusLock == null || !focusLock.IsValid)
            {
                _log.Warn($"[Folder][VRGrab] focus acquire failed controller={ctrl.name}");
                return false;
            }

            _folderVrGrabbed    = true;
            _folderVrController  = ctrl;
            _folderVrFocusLock   = focusLock;
            _folderVrGrabOffset  = _folderGo.transform.position - ctrlTf.position;
            _log.Info($"[Folder][VRGrab] start controller={ctrl.name}");
            return true;
        }

        private void UpdateFolderVrGrab()
        {
            if (!_folderVrGrabbed) return;
            if (_folderGo == null || _folderVrController == null || _folderVrController.Input == null)
            {
                StopFolderVrGrab();
                return;
            }
            if (!_folderVrController.Input.GetPress(EVRButtonId.k_EButton_Grip))
            {
                StopFolderVrGrab();
                return;
            }
            Transform ctrlTf = ((Component)_folderVrController).transform;
            _folderGo.transform.position = ctrlTf.position + _folderVrGrabOffset;
        }

        private void StopFolderVrGrab()
        {
            if (_folderVrGrabbed && _folderGo != null)
            {
                UpdateFolder();
                _log.Info($"[Folder][VRGrab] end pos={_folderGo.transform.position}");
            }
            if (_folderVrFocusLock != null && _folderVrFocusLock.IsValid)
            {
                try { _folderVrFocusLock.SafeRelease(); }
                catch (Exception ex) { _log.Warn("[Folder][VRGrab] focus release failed: " + ex.Message); }
            }
            _folderVrFocusLock  = Controller.Lock.Invalid;
            _folderVrGrabbed    = false;
            _folderVrController = null;
        }
    }
}
