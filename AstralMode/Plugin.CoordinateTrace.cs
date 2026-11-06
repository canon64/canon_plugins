using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace AstralMode
{
    public sealed partial class Plugin
    {
        private int _coordinateTraceSession;
        private readonly Dictionary<int, int> _coordinateTraceRewrites = new Dictionary<int, int>();

        // Event-only diagnostics, deliberately independent of Verbose Log.
        private void TraceCoordinates(string phase)
        {
            try
            {
                var log = new StringBuilder();
                log.Append("session=").Append(_coordinateTraceSession).Append(" phase=").Append(phase)
                    .Append(" traceVersion=20261029-ksplug-pov-7")
                    .Append(" state=").Append(_state).Append(" frame=").Append(Time.frameCount)
                    .Append(" pose=").Append(_hFlag?.nowAnimationInfo?.nameAnimation)
                    .Append(" requested=").Append(_hFlag?.selectAnimationListInfo?.nameAnimation)
                    .Append(" click=").Append(_hFlag != null ? _hFlag.click.ToString() : "null")
                    .Append(" center=").Append(TraceVector(_astralCharaCenter))
                    .Append(" offset=").Append(TraceVector(_astralCharaOffset)).AppendLine();
                log.Append("config hillPos=").Append(TraceVector(ReadVector(_cfgAstralHillPosX, _cfgAstralHillPosY, _cfgAstralHillPosZ)))
                    .Append(" benchPos=").Append(TraceVector(ReadVector(_cfgAstralBenchPosX, _cfgAstralBenchPosY, _cfgAstralBenchPosZ)))
                    .Append(" benchRot=").Append(TraceVector(ReadVector(_cfgAstralBenchRotX, _cfgAstralBenchRotY, _cfgAstralBenchRotZ))).AppendLine();
                var actors = CollectCurrentHSceneCharas();
                if (_hFlag != null && _hFlag.ctrlCamera != null)
                {
                    Vector3 cameraBasePosition = Vector3.zero;
                    Quaternion cameraBaseRotation = Quaternion.identity;
                    bool hasBase = _hFlag.ctrlCamera.GetWorldBase(ref cameraBasePosition, ref cameraBaseRotation);
                    var cameraData = _hFlag.ctrlCamera.GetCameraData();
                    log.Append("cameraBase valid=").Append(hasBase).Append(" world=").Append(TraceVector(cameraBasePosition))
                        .Append(" rot=").Append(TraceVector(cameraBaseRotation.eulerAngles))
                        .Append(" dataPos=").Append(TraceVector(cameraData.Pos))
                        .Append(" dataDir=").Append(TraceVector(cameraData.Dir))
                        .Append(" dataRot=").Append(TraceVector(cameraData.Rot)).AppendLine();
                    TraceTransform(log, "mainCamera", _hFlag.ctrlCamera.thisCmaera != null ? _hFlag.ctrlCamera.thisCmaera.transform : null);
                    TraceTransform(log, "cameraBaseTransform", _hFlag.ctrlCamera.transBase);
                }
                foreach (var actor in actors)
                {
                    if (actor == null) continue;
                    log.Append("actor=").Append(actor.name).Append('#').Append(actor.GetInstanceID())
                        .Append(" active=").Append(actor.gameObject.activeInHierarchy).AppendLine();
                    TraceTransform(log, "root", actor.transform);
                    TraceTransform(log, "objTop", actor.objTop != null ? actor.objTop.transform : null);
                    TraceTransform(log, "objAnim", actor.objAnim != null ? actor.objAnim.transform : null);
                    TraceTransform(log, "body", actor.objBodyBone != null ? actor.objBodyBone.transform : null);
                    var hips = actor.objBodyBone != null ? FindTraceHips(actor.objBodyBone.transform) : null;
                    TraceTransform(log, "hips", hips);
                    foreach (var snapshot in _charaSnapshots)
                        if (snapshot.Target == actor.transform)
                            log.Append(" savedRootPos=").Append(TraceVector(snapshot.WorldPos))
                                .Append(" savedRootRot=").Append(TraceVector(snapshot.WorldRot.eulerAngles)).AppendLine();
                    GameObject bench;
                    if (_astralFurnitureByName.TryGetValue("Bench", out bench) && bench != null)
                    {
                        log.Append(" rootInBench=").Append(TraceVector(bench.transform.InverseTransformPoint(actor.transform.position)));
                        if (hips != null) log.Append(" hipsInBench=").Append(TraceVector(bench.transform.InverseTransformPoint(hips.position)));
                        log.AppendLine();
                    }
                }
                TraceTransform(log, "scenery", _astralHillRoot != null ? _astralHillRoot.transform : null);
                TraceTransform(log, "hill", _astralHillGeometryRoot);
                foreach (var pair in _astralFurnitureByName)
                    TraceTransform(log, pair.Key, pair.Value != null ? pair.Value.transform : null);
                AppendCoordinateTrace(log.ToString());
            }
            catch (Exception ex) { Logger.LogWarning("Coordinate trace failed: " + ex.Message); }
        }

        private static Transform FindTraceHips(Transform root)
        {
            // Only called at transition events; no scene-wide search.
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "cf_j_hips" || t.name == "cm_j_hips") return t;
            return null;
        }

        private static string TraceVector(Vector3 value) => string.Format(CultureInfo.InvariantCulture,
            "({0:F5},{1:F5},{2:F5})", value.x, value.y, value.z);

        private static void TraceTransform(StringBuilder log, string label, Transform t)
        {
            if (t == null) { log.Append(label).AppendLine("=null"); return; }
            log.Append(label).Append(" path=").Append(TransformPath(t))
                .Append(" world=").Append(TraceVector(t.position)).Append(" local=").Append(TraceVector(t.localPosition))
                .Append(" worldRot=").Append(TraceVector(t.eulerAngles)).Append(" localRot=").Append(TraceVector(t.localEulerAngles))
                .Append(" scale=").Append(TraceVector(t.lossyScale)).AppendLine();
        }

        private void AppendCoordinateTrace(string message)
        {
            string directory = Path.GetDirectoryName(Info.Location);
            File.AppendAllText(Path.Combine(directory, "AstralCoordinates.log"),
                "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "] " + message + Environment.NewLine,
                Encoding.UTF8);
        }

        private void TraceSavedCoordinateRewrite(TransformSnapshot snapshot, string reason, Vector3 next)
        {
            int id = snapshot.Target.GetInstanceID();
            int serial;
            if (_coordinateTraceRewrites.TryGetValue(id, out serial) && serial == _poseChangeTriggerHits) return;
            _coordinateTraceRewrites[id] = _poseChangeTriggerHits;
            try
            {
                AppendCoordinateTrace("SAVED_ROOT_REWRITE session=" + _coordinateTraceSession + " frame=" + Time.frameCount
                    + " state=" + _state + " reason=" + reason + " actor=" + snapshot.Target.name
                    + " old=" + TraceVector(snapshot.WorldPos) + " next=" + TraceVector(next)
                    + " oldRot=" + TraceVector(snapshot.WorldRot.eulerAngles)
                    + " nextRot=" + TraceVector(snapshot.Target.eulerAngles));
            }
            catch (Exception ex) { Logger.LogWarning("Coordinate rewrite trace failed: " + ex.Message); }
        }
    }
}
