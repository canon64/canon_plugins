using System;
using System.Collections;
using System.Globalization;
using MainGameHLocationCoordinates;
using UnityEngine;

namespace MainGameHLocationPlacement
{
    internal sealed partial class PlacementSession
    {
        private long _diagnosticSequence;
        private static string Number(float n) => n.ToString("R", CultureInfo.InvariantCulture);
        private static string Vector(Vector3 p) => "(" + Number(p.x) + "," + Number(p.y) + "," + Number(p.z) + ")";
        private static string Rotation(Quaternion q) => "(" + Number(q.x) + "," + Number(q.y) + "," + Number(q.z) + "," + Number(q.w) + ")";
        internal static string PoseText(RigidPose p) => "pos_m=" + Vector(p.position) + " rot_xyzw=" + Rotation(p.rotation);
        private static string TransformText(Transform t) => t == null ? "none" : t.gameObject.name + " " + PoseText(RuntimeBridge.Pose(t));
        internal bool DiagnosticsEnabled => _plugin.Diagnostics;
        private static string SourceText(PlaceOrigin source) => source == null ? "unknown" :
            "map=" + source.mapKey.Replace("\n", "|") + " place=" + source.placeId + " pose=" + source.poseKey + " " + PoseText(source.femaleRoot.ToPose()) +
            " links=" + string.Join("|", source.composerLinks);
        internal void Trace(string stage, Func<string> details)
        {
            if (!_plugin.Diagnostics) return;
            try { _plugin.Diagnostic(() => "[Coordinates] event=" + (++_diagnosticSequence) + " stage=" + stage + " " + details()); }
            catch (Exception ex) { _plugin.Diagnostic(() => "[Coordinates] stage=" + stage + " diagnostic-unavailable=" + ex.GetType().Name + ":" + ex.Message); }
        }
        private string IkReason(int index, bool saved = false)
        {
            var state = Bridge.States.GetValue(index);
            if (state == null) return "state-missing";
            if (R.Get<bool>(state, "ExternalOwner")) return "external-owner";
            if (Bridge.Linked(index)) return "Composer-linked";
            if (!Bridge.Independent(index, saved)) return "bone-HMD-or-wrist-follow";
            if (!saved && !R.Get<bool>(state, "Running")) return "not-running";
            return "independent";
        }
        private void TraceState(string stage, string operation)
        {
            if (!_plugin.Diagnostics) return;
            try { TraceStateCore(stage, operation); }
            catch (Exception ex) { Trace(stage + ".unavailable", () => ex.GetType().Name + ":" + ex.Message); }
        }
        private void TraceStateCore(string stage, string operation)
        {
            if (!_plugin.Diagnostics) return;
            Trace(stage + ".context", () => "operation=" + operation + " " + SourceText(CaptureHip()) +
                " Composer=" + (_selection?.Snapshot.Name ?? "none") + " anchors=" + _anchors.Count);
            for (int i = 0; i < Bridge.States.Length; i++)
            {
                int index = i;
                Trace(stage + ".IK", () =>
                {
                    var state = Bridge.States.GetValue(index);
                    return "operation=" + operation + " IK=" + Bridge.IkKeys[index] + " reason=" + IkReason(index) +
                        (state == null ? "" : " running=" + R.Get<bool>(state, "Running") +
                        " proxy=" + TransformText(R.Get<Transform>(state, "Proxy")) + " follow=" + TransformText(R.Get<Transform>(state, "FollowBone")) +
                        " pendingFollow=" + TransformText(R.Get<Transform>(state, "PendingFollowBone")) + " hold=" + R.Get<bool>(state, "HasPostDragHold"));
                });
            }
            foreach (DictionaryEntry item in Bridge.Objects)
            {
                var current = item;
                Trace(stage + ".object", () =>
                {
                    var data = R.Get(current.Value, "Data");
                    var t = R.Get<GameObject>(current.Value, "GameObject")?.transform;
                    return "operation=" + operation + " id=" + current.Key + " parentKind=" + R.Get<string>(data, "parentKind") +
                        " parentId=" + R.Get<string>(data, "parentRefId") + " world=" + TransformText(t) +
                        (t == null ? "" : " local_pos_m=" + Vector(t.localPosition) + " local_rot_xyzw=" + Rotation(t.localRotation) + " parent=" + TransformText(t.parent));
                });
            }
        }
        private void TraceHipPart(SavedPreset preset, SavedPart part, object entry, PartOrigin origin, string reason)
        {
            Trace("Hip.load", () => "preset=" + preset.Name + " id=" + preset.Identity + " pose=" + preset.PoseKey +
                " part=" + part.Name + " fingerprint=" + part.Fingerprint + " reason=" + reason +
                " originMatch=" + _store.DescribeMatch(Bridge.HipPath, preset, part) +
                " source={" + SourceText((origin ?? _store.Match(Bridge.HipPath, preset, part))?.source) + "} destination={" + PoseText(_destination.Value) + "}" +
                (entry == null ? "" : " enabled=" + R.Get<bool>(entry, "enabled") + " weight=" + Number(R.Get<float>(entry, "weight")) +
                " saved_pos_m=" + Vector(R.Get<Vector3>(entry, "proxyPosition")) + " saved_rot_xyzw=" + Rotation(R.Get<Quaternion>(entry, "proxyRotation")) +
                " followPath=" + R.Get<string>(entry, "followBonePath")));
        }
        internal void TraceHipApplied(bool applied, string reason)
        {
            if (!_plugin.Diagnostics) return;
            Trace("Hip.applied", () => "success=" + applied + " reason=" + reason + " phase=transition-start");
            TraceState("Hip.applied", reason);
        }
        internal void TraceComposerApplied() => TraceState("Composer.applied", "load");
    }
}
