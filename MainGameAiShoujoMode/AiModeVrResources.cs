using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using MapEnvironmentRuntime;

namespace MainGameAiShoujoMode
{
    // Capture once at entry. Desktop installations do not need to load any VR types.
    internal sealed class AiModeVrResources
    {
        internal StartupTrace Diagnostics { get; set; }
        internal Camera RenderCamera => _rig == null ? null : _rig.GetComponent<Camera>();
        internal Transform Origin => _origin;
        private readonly Component _rig;
        private readonly Transform _origin;
        private readonly object _mover;
        private readonly MethodInfo _copy, _move;
        private readonly HashSet<AudioListener> _listeners = new HashSet<AudioListener>();
        private readonly List<GameObject> _roots = new List<GameObject>();
        private Vector3 _position;
        private Quaternion _rotation;
        private Camera _view;
        private bool _viewOwned;
        internal IEnumerable<GameObject> Roots => _roots;
        internal bool Active => _rig != null;

        private static Type FindType(string assemblyName, string typeName)
        {
            return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly => assembly.GetName().Name == assemblyName)?.GetType(typeName);
        }
        internal AiModeVrResources() : this(FindType("VRGIN_OpenXR", "VRGIN.Core.VR"), FindType("KKS_MainGameVR", "KKS_VR.Camera.VRCameraMover")) { }
        internal AiModeVrResources(Type vr, Type moverType)
        {
            if (vr == null || !(bool)vr.GetProperty("Active").GetValue(null, null)) return;
            _rig = vr.GetProperty("Camera").GetValue(null, null) as Component;
            var gui = vr.GetProperty("GUI").GetValue(null, null) as Component;
            if (_rig == null || gui == null) throw new InvalidOperationException("VRのカメラ・UIが準備されていません。");
            _origin = _rig.GetType().GetProperty("Origin").GetValue(_rig, null) as Transform;
            _copy = _rig.GetType().GetMethod("Copy", new[] { typeof(Camera), typeof(bool), typeof(bool) });
            if (_origin == null || _copy == null || moverType == null) throw new InvalidOperationException("VRの視点接続APIが見つかりません。");
            _mover = moverType.GetProperty("Instance").GetValue(null, null);
            _move = moverType.GetMethod("MoveTo", new[] { typeof(Vector3), typeof(Quaternion), typeof(bool), typeof(bool) });
            if (_mover == null || _move == null) throw new InvalidOperationException("VRの視点移動APIが見つかりません。");
            Keep(_rig.transform.root.gameObject);
            Keep(gui.transform.root.gameObject);
        }
        private void Keep(GameObject root)
        {
            if (_roots.Contains(root)) return;
            _roots.Add(root);
            foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) _listeners.Add(listener);
        }
        internal bool Protect(AudioListener listener) => _listeners.Contains(listener);
        internal void CaptureNativeView()
        {
            if (_rig == null || _viewOwned) return;
            _position = _origin.position; _rotation = _origin.rotation; _viewOwned = true;
        }
        internal bool BindView(Camera view)
        {
            if (_rig == null) return false;
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (_viewOwned && _view == view) return true;
            if (!_viewOwned) { _position = _origin.position; _rotation = _origin.rotation; _viewOwned = true; }
            try
            {
                Diagnostics?.CameraCheckpoint("vr.copy.begin", view);
                _copy.Invoke(_rig, new object[] { view, true, false });
                Diagnostics?.Snapshot("vr.copy.end");
                Diagnostics?.Mark("vr.move.begin", () => "origin=" + _origin.position + " target=" + view.transform.position + " rotation=" + view.transform.rotation);
                _move.Invoke(_mover, new object[] { view.transform.position, view.transform.rotation, false, true });
                Diagnostics?.Mark("vr.move.end", () => "origin=" + _origin.position + " rotation=" + _origin.rotation);
                _view = view;
                return true;
            }
            catch (Exception ex) { Diagnostics?.Mark("vr.bind.failed", () => ex.ToString()); RestoreView(); throw; }
        }
        internal void RestoreView()
        {
            if (!_viewOwned) return;
            _viewOwned = false; _view = null;
            if (_origin != null) _origin.SetPositionAndRotation(_position, _rotation);
        }
    }
}
