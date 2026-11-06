using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using MainGameHLocationCoordinates;
using UnityEngine;

namespace MainGameHLocationPlacement
{
    internal sealed partial class PlacementSession
    {
        private sealed class OffsetProjection { internal Vector3 First, Second; internal bool Split; }
        private readonly Dictionary<string, OffsetProjection> _offsetRules = new Dictionary<string, OffsetProjection>();
        private readonly Dictionary<string, object> _movementRules = new Dictionary<string, object>();
        private int _movementEvaluationDepth;
        private static float Number(XElement node, string name) => float.TryParse(PresetDocuments.Text(node, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0f;
        private static Vector3 Vector(XElement node) => node == null ? Vector3.zero : new Vector3(Number(node, "x"), Number(node, "y"), Number(node, "z"));
        private static IEnumerable<string> Strings(object value, string field) => (R.Get(value, field) as IEnumerable)?.Cast<string>() ?? Enumerable.Empty<string>();
        private RigidPose RelativeParent(string id, string slot)
        {
            var baseline = _selection.Parts[_selection.BasePart];
            if (!_selection.Parts.TryGetValue(slot, out var part)) part = baseline;
            var first = baseline.source.parents.SingleOrDefault(p => p.objectId == id);
            var second = part.source.parents.SingleOrDefault(p => p.objectId == id);
            if (first == null || second == null) return RigidPose.Identity;
            return Project(baseline.source, first.world.ToPose()).Inverse() * Project(part.source, second.world.ToPose());
        }
        internal void BuildRuleProjection()
        {
            _offsetRules.Clear(); _movementRules.Clear();
            if (!Valid || _selection == null) return;
            object active = R.Get(Bridge.Composer, "_activePreset");
            if (active == null || !ReferenceEquals(active, _selection.Asset)) return;
            var parents = _selection.Parts[_selection.BasePart].source.parents;
            var top = new HashSet<string>(parents.Select(p => p.objectId), StringComparer.Ordinal);
            foreach (string json in Strings(active, "motionPositionRulesJson"))
            {
                var rule = PresetDocuments.Parse(json); string id = PresetDocuments.Text(rule, "objectId"); if (!top.Contains(id)) continue;
                _offsetRules[id + "/" + PresetDocuments.Text(rule, "level")] = new OffsetProjection {
                    First = RelativeParent(id, "loop1Json").rotation * Vector(rule.Element("l1Offset")),
                    Second = RelativeParent(id, "loop2Json").rotation * Vector(rule.Element("l2Offset")),
                    Split = PresetDocuments.Text(rule, "splitLoops") == "true" };
            }
            var rules = Strings(active, "movementRulesJson").Select(PresetDocuments.Parse).ToList();
            if (rules.Count == 0) return;
            var type = R.Method(Bridge.Composer.GetType(), "FindMovementField", active.GetType(), typeof(string), typeof(string)).ReturnType;
            var rawSlots = new[] { "loop1Json", "loop2Json" }.Select(slot => Strings(active, slot).Select(PresetDocuments.Parse).ToDictionary(n => PresetDocuments.Text(n, "id"))).ToArray();
            if (rawSlots[1].Count == 0) rawSlots[1] = rawSlots[0];
            foreach (string id in top)
            foreach (bool angle in new[] { false, true })
            {
                string[] fields = angle ? new[] { "rotX", "rotY", "rotZ" } : new[] { "px", "py", "pz" };
                var group = rules.Where(n => PresetDocuments.Text(n, "objectId") == id && fields.Contains(PresetDocuments.Text(n, "field")) && PresetDocuments.Text(n, "enabled") != "false").ToList();
                if (group.Count == 0 || !rawSlots[0].ContainsKey(id) || !rawSlots[1].ContainsKey(id)) continue;
                var projected = fields.Select(field => {
                    var rule = Activator.CreateInstance(type, true); R.Set(rule, "objectId", id); R.Set(rule, "field", field); R.Set(rule, "enabled", true); return rule;
                }).ToArray();
                for (int slot = 1; slot <= 2; slot++)
                foreach (bool full in new[] { false, true })
                {
                    string endpoint = "l" + slot + (full ? "Full" : "Zero");
                    Vector3 baseline = Vector(rawSlots[slot - 1][id].Element(angle ? "localEulerAngles" : "localPosition"));
                    var values = new[] { baseline.x, baseline.y, baseline.z };
                    for (int axis = 0; axis < 3; axis++)
                    {
                        var rule = group.LastOrDefault(n => PresetDocuments.Text(n, "field") == fields[axis]);
                        if (rule != null && PresetDocuments.Text(rule, endpoint + "Set") == "true") values[axis] = Number(rule, endpoint);
                    }
                    var relative = RelativeParent(id, slot == 1 ? "loop1Json" : "loop2Json");
                    Vector3 result = angle ? (relative.rotation * Quaternion.Euler(new Vector3(values[0], values[1], values[2]))).eulerAngles : relative.Point(new Vector3(values[0], values[1], values[2]));
                    float[] components = { result.x, result.y, result.z };
                    for (int axis = 0; axis < 3; axis++) { R.Set(projected[axis], endpoint + "Set", true); R.Set(projected[axis], endpoint, components[axis]); }
                }
                for (int axis = 0; axis < 3; axis++) _movementRules[id + "/" + fields[axis]] = projected[axis];
            }
        }
        internal bool TryMotionOffset(string id, out Vector3 result)
        {
            result = Vector3.zero;
            if (!Valid || _selection == null || _offsetRules.Count == 0 || !_offsetRules.TryGetValue(id + "/" + Bridge.MotionLevel, out var rule)) return false;
            var active = R.Get(Bridge.Composer, "_activePreset");
            if (!ReferenceEquals(active, _selection.Asset) || !Bridge.MotionTarget(active, id)) return false;
            bool blend = rule.Split && R.Get<bool>(active, "useLoopSplit") && _selection.Parts.ContainsKey("loop2Json");
            float t = Math.Max(0f, Math.Min(1f, Bridge.MotionBlend));
            result = blend ? new Vector3(rule.First.x + (rule.Second.x - rule.First.x) * t, rule.First.y + (rule.Second.y - rule.First.y) * t, rule.First.z + (rule.Second.z - rule.First.z) * t) : rule.First;
            return true;
        }
        internal void BeginMovementEvaluation() { _movementEvaluationDepth++; }
        internal void EndMovementEvaluation() { if (_movementEvaluationDepth > 0) _movementEvaluationDepth--; }
        internal bool TryMovementRule(object asset, string id, string field, out object result)
        {
            result = null;
            return Valid && _selection != null && _movementEvaluationDepth > 0 && ReferenceEquals(asset, _selection.Asset) && _movementRules.TryGetValue(id + "/" + field, out result);
        }
    }
}
