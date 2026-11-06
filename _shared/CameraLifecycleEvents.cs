using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CanonPlugins.Shared
{
    // Rendering announces a camera once. Subsequent renders do not enumerate cameras
    // or reapply settings. Object lifetime and canvas resize messages invalidate users.
    internal sealed class CameraLifecycleEvents : IDisposable
    {
        private readonly Action _changed;
        private readonly List<CameraLifetimeNotice> _notices = new List<CameraLifetimeNotice>();
        private readonly HashSet<Camera> _known = new HashSet<Camera>();
        private GameObject _screenRoot;
        private bool _disposed;

        internal CameraLifecycleEvents(Action changed)
        {
            _changed = changed;
            Camera.onPreCull += OnCameraRendering;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            _screenRoot = new GameObject("CameraLifecycleScreen", typeof(RectTransform), typeof(Canvas));
            _screenRoot.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(_screenRoot);
            _screenRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _screenRoot.AddComponent<ScreenSizeNotice>().Changed = Notify;
            foreach (var camera in Camera.allCameras) Remember(camera);
        }

        private bool Remember(Camera camera)
        {
            if (camera == null || !_known.Add(camera)) return false;
            var notice = camera.gameObject.AddComponent<CameraLifetimeNotice>();
            notice.Changed = Notify;
            _notices.Add(notice);
            return true;
        }

        private void OnCameraRendering(Camera camera)
        {
            if (Remember(camera)) Notify();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Notify(); }
        private void OnSceneUnloaded(Scene scene) { Notify(); }
        private void Notify() { if (!_disposed) _changed(); }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Camera.onPreCull -= OnCameraRendering;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            foreach (var notice in _notices)
            {
                if (notice == null) continue;
                notice.Changed = null;
                UnityEngine.Object.Destroy(notice);
            }
            _notices.Clear();
            _known.Clear();
            if (_screenRoot != null) UnityEngine.Object.Destroy(_screenRoot);
            _screenRoot = null;
        }
    }

    internal sealed class CameraLifetimeNotice : MonoBehaviour
    {
        internal Action Changed;
        private void OnEnable() { Changed?.Invoke(); }
        private void OnDisable() { Changed?.Invoke(); }
        private void OnDestroy() { Changed?.Invoke(); }
    }

    internal sealed class ScreenSizeNotice : MonoBehaviour
    {
        internal Action Changed;
        private void OnRectTransformDimensionsChange() { Changed?.Invoke(); }
    }
}
