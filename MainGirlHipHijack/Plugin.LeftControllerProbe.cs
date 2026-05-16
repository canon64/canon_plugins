using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using VRGIN.Controls;
using VRGIN.Core;

namespace MainGirlHipHijack
{
    public sealed partial class Plugin
    {
        private const float LeftControllerProbeIntervalSeconds = 0.2f;
        private const float LeftControllerProbeRadius = 0.10f;

        private float _nextLeftControllerProbeTime;

        private void TickLeftControllerProbe()
        {
            if (_settings == null || !_settings.DetailLogEnabled)
                return;
            if (!_bodyCtrlLinkEnabled)
                return;
            if (!VR.Active || VR.Mode == null || VR.Mode.Left == null)
                return;
            if (Time.unscaledTime < _nextLeftControllerProbeTime)
                return;

            _nextLeftControllerProbeTime = Time.unscaledTime + LeftControllerProbeIntervalSeconds;

            Controller leftController = VR.Mode.Left;
            Transform left = ((Component)leftController).transform;
            if (left == null)
                return;

            Vector3 leftPos = left.position;
            LogInfo("[LeftProbe] left"
                + " path=" + BuildTransformPath(left)
                + " id=" + left.GetInstanceID()
                + " active=" + BuildActiveState(left.gameObject)
                + " pos=" + Vec3(leftPos)
                + " comps=" + DescribeComponents(left.gameObject));

            LogNeckLookProbe(leftPos);
            LogLeftControllerNearbyTransforms(leftPos);
        }

        private void LogNeckLookProbe(Vector3 leftPos)
        {
            ChaControl femaleCha = _runtime.TargetFemaleCha;
            if (femaleCha == null || femaleCha.neckLookCtrl == null)
            {
                LogInfo("[LeftProbe] neck unavailable"
                    + " female=" + (femaleCha != null)
                    + " neckLook=" + (femaleCha != null && femaleCha.neckLookCtrl != null));
                return;
            }

            Transform target = femaleCha.neckLookCtrl.target;
            int ptnNo = femaleCha.neckLookCtrl.ptnNo;
            if (target == null)
            {
                LogInfo("[LeftProbe] neck target=null ptnNo=" + ptnNo);
                return;
            }

            float distance = Vector3.Distance(leftPos, target.position);
            LogInfo("[LeftProbe] neck"
                + " dLeft=" + distance.ToString("F4")
                + " ptnNo=" + ptnNo
                + " path=" + BuildTransformPath(target)
                + " id=" + target.GetInstanceID()
                + " active=" + BuildActiveState(target.gameObject)
                + " pos=" + Vec3(target.position)
                + " comps=" + DescribeComponents(target.gameObject));
        }

        private void LogLeftControllerNearbyTransforms(Vector3 leftPos)
        {
            Transform[] allTransforms = FindObjectsOfType<Transform>();
            List<TransformDistance> matches = new List<TransformDistance>();

            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform t = allTransforms[i];
                if (t == null)
                    continue;

                float distance = Vector3.Distance(leftPos, t.position);
                if (distance > LeftControllerProbeRadius)
                    continue;

                matches.Add(new TransformDistance(t, distance));
            }

            matches.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            LogInfo("[LeftProbe] near summary"
                + " radius=" + LeftControllerProbeRadius.ToString("F3")
                + " count=" + matches.Count);

            for (int i = 0; i < matches.Count; i++)
            {
                Transform t = matches[i].Transform;
                if (t == null)
                    continue;

                LogInfo("[LeftProbe] near"
                    + " d=" + matches[i].Distance.ToString("F4")
                    + " path=" + BuildTransformPath(t)
                    + " id=" + t.GetInstanceID()
                    + " active=" + BuildActiveState(t.gameObject)
                    + " layer=" + t.gameObject.layer
                    + " pos=" + Vec3(t.position)
                    + " comps=" + DescribeComponents(t.gameObject));
            }
        }

        private static string BuildTransformPath(Transform t)
        {
            if (t == null)
                return "(null)";

            List<string> parts = new List<string>();
            Transform current = t;
            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            parts.Reverse();
            return "/" + string.Join("/", parts.ToArray());
        }

        private static string DescribeComponents(GameObject go)
        {
            if (go == null)
                return "(null)";

            Component[] components = go.GetComponents<Component>();
            if (components == null || components.Length == 0)
                return "-";

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < components.Length; i++)
            {
                if (i > 0)
                    sb.Append(",");

                Component component = components[i];
                if (component == null)
                {
                    sb.Append("(missing)");
                    continue;
                }

                Type type = component.GetType();
                string assemblyName = type.Assembly != null ? type.Assembly.GetName().Name : "?";
                sb.Append(type.FullName);
                sb.Append("@");
                sb.Append(assemblyName);
            }

            return sb.ToString();
        }

        private static string BuildActiveState(GameObject go)
        {
            if (go == null)
                return "(null)";

            return (go.activeSelf ? "self1" : "self0") + "/" + (go.activeInHierarchy ? "hier1" : "hier0");
        }

        private readonly struct TransformDistance
        {
            public readonly Transform Transform;
            public readonly float Distance;

            public TransformDistance(Transform transform, float distance)
            {
                Transform = transform;
                Distance = distance;
            }
        }
    }
}
