using System;
using UnityEngine;

namespace MainGameIPhoneCamera
{
    // Compose masks and UI once in mono. The handheld surface is a single opaque mesh,
    // so its movement and stereo eye renders never change Canvas/stencil state.
    internal sealed class PhoneDisplay : IDisposable
    {
        internal Transform Root => _root.transform;
        internal RenderTexture Texture { get; private set; }
        private GameObject _root;
        private Camera _camera;

        internal PhoneDisplay()
        {
            try
            {
                _root = new GameObject("IPhoneDisplayStage");
                // Pixel-sized UI stays far outside gameplay camera frusta. It is not
                // parented to the moving phone, nor assigned a global/reserved layer.
                _root.transform.position = new Vector3(10000, 10000, 10000);
                Texture = new RenderTexture(560, 1166, 24, RenderTextureFormat.ARGB32)
                { name = "IPhoneComposedDisplay", antiAliasing = 1 };
                if (!Texture.Create()) throw new InvalidOperationException("iPhone UI描画面を作成できません");
                var cameraObject = new GameObject("IPhoneDisplayCamera", typeof(Camera));
                cameraObject.transform.SetParent(Root, false);
                cameraObject.transform.localPosition = new Vector3(0, 0, -10);
                _camera = cameraObject.GetComponent<Camera>();
                _camera.stereoTargetEye = StereoTargetEyeMask.None;
                _camera.orthographic = true;
                _camera.orthographicSize = 583;
                _camera.aspect = 560f / 1166;
                _camera.nearClipPlane = 1;
                _camera.farClipPlane = 20;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = Color.black;
                _camera.depth = -49; // Rear camera (-50), composed UI, then player cameras.
                _camera.targetTexture = Texture;
                _camera.useOcclusionCulling = false;
                _camera.allowHDR = false;
                _camera.allowMSAA = false;
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (_camera != null) { _camera.enabled = false; _camera.targetTexture = null; }
            if (_root != null) { _root.SetActive(false); UnityEngine.Object.Destroy(_root); }
            _root = null; _camera = null;
            if (Texture != null) { Texture.Release(); UnityEngine.Object.Destroy(Texture); Texture = null; }
        }
    }
}
