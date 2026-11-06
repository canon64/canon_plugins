using System;
using System.Reflection;
using UnityEngine;

namespace MainGameCameraLookControl
{
    internal sealed class CameraTargets
    {
        private Type _vrType;
        private PropertyInfo _vrActive, _vrCamera;
        private float _nextTypeCheck;
        private int _frame = -1;
        private Transform _head, _steamHead;

        private static object Read(object instance, string name)
        {
            if (instance == null) return null;
            Type type = instance.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            return type.GetProperty(name, flags)?.GetValue(instance, null) ?? type.GetField(name, flags)?.GetValue(instance);
        }

        internal void Prepare(){

                    _vrType = Type.GetType("VRGIN.Core.VR, VRGIN_OpenXR", false);
                    if (_vrType != null)
                    {
                        _vrActive = _vrType.GetProperty("Active", BindingFlags.Static | BindingFlags.Public);
                        _vrCamera = _vrType.GetProperty("Camera", BindingFlags.Static | BindingFlags.Public);
                    }
                }

        internal bool IsCamera(Transform target)
        {
            if (target == null) return false;
            // An explicitly selected capture camera also counts; do not rely on
            // ptnNo or neckTargetType (the game uses those for partners as well).
            if (target.GetComponent<Camera>() != null) return true;
            if (_frame != Time.frameCount)
            {
                _frame = Time.frameCount;
                _head = _steamHead = null;

                try
                {
                    if (_vrActive?.GetValue(null, null) is bool active && active)
                    {
                        object vrCamera = _vrCamera?.GetValue(null, null);
                        _head = Read(vrCamera, "Head") as Transform;
                        _steamHead = Read(Read(vrCamera, "SteamCam"), "head") as Transform;
                    }
                }
                catch { _head = _steamHead = null; }
            }
            return target == _head || target == _steamHead;
        }
    }
}
