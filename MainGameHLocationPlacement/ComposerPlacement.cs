using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MainGameHLocationCoordinates;
using UnityEngine;

namespace MainGameHLocationPlacement
{
    internal sealed partial class PlacementSession
    {
        private sealed class Selection
        {
            internal object Asset;
            internal SavedPreset Snapshot;
            internal string BasePart;
            internal readonly Dictionary<string, PartOrigin> Parts = new Dictionary<string, PartOrigin>();
        }
        private sealed class Anchor
        {
            internal Transform Transform, Original;
            internal ParentOrigin Parent;
            internal PlaceOrigin Source;
        }
        private Selection _selection;
        private readonly Dictionary<string, Anchor> _anchors = new Dictionary<string, Anchor>();
        private bool _normalizing;
        internal void SelectComposer(object asset, string basePart = "loop1Json")
        {
            if (!Valid || asset == null) return;
            ClearAnchors(); _selection = null;
            RefreshDestination();
            object original = ReferenceEquals(asset, R.Get(Bridge.Composer, "_presetDraft")) ? R.Get(Bridge.Composer, "_presetSource") ?? asset : asset;
            var snapshot = Bridge.ComposerSnapshot(original);
            var selection = new Selection { Asset = asset, Snapshot = snapshot, BasePart = basePart };
            foreach (var part in snapshot.Parts)
            {
                var match = _store.Match(Bridge.ComposerPath, snapshot, part);
                Trace("Composer.select", () => "preset=" + snapshot.Name + " pose=" + snapshot.PoseKey + " part=" + part.Name + " fingerprint=" + part.Fingerprint +
                    " reason=" + (match == null ? "origin-missing-or-content-mismatch" : "origin-matched") + " source={" + SourceText(match?.source) + "} destination={" + PoseText(_destination.Value) + "}");
                if (match != null) selection.Parts.Add(part.Name, match);
            }
            if (!selection.Parts.ContainsKey(basePart)) { _plugin.Message("Composer「" + snapshot.Name + "」は保存元未登録・補正なし。正しい場所で保存してください。"); return; }
            if (basePart == "loop1Json" && R.Get<bool>(asset, "useLoopSplit") && snapshot.Parts.Any(p => p.Name == "loop2Json") && !selection.Parts.ContainsKey("loop2Json"))
            { _plugin.Message("ComposerのL2は保存元未登録・補正なし。L2も正しい場所で保存してください。"); return; }
            foreach (var part in snapshot.Parts)
            foreach (string json in part.Elements)
            {
                var data = PresetDocuments.Parse(json);
                string kind = PresetDocuments.Text(data, "parentKind"), reference = PresetDocuments.Text(data, "parentRefId");
                if (kind != "root" && kind != "managed" && !(kind == "external" && reference == "char:root"))
                    throw new InvalidOperationException("Composerの外部ボーン親は補正対象外です。");
                int.TryParse(PresetDocuments.Text(data, "motionMode"), out int mode);
                bool IsFalse(string key) => string.Equals(PresetDocuments.Text(data, key), "false", StringComparison.OrdinalIgnoreCase);
                if ((mode == 1 || PresetDocuments.Text(data, "autoRotate") == "true") && IsFalse("autoRotateLocalSpace") || mode == 2 && IsFalse("angleLocalSpace") || mode == 3 && IsFalse("pistonLocalSpace"))
                    throw new InvalidOperationException("ワールド軸モーションは補正対象外です。");
                if (kind != "managed" && selection.Parts.TryGetValue(part.Name, out var frame) && !frame.source.parents.Any(p => p.objectId == PresetDocuments.Text(data, "id") && p.kind == kind && (p.reference ?? "") == reference))
                    throw new InvalidOperationException("Composerの親の保存元情報が欠落しています。");
            }
            _selection = selection;
            _plugin.Message("Composer「" + snapshot.Name + "」を現在のH場所へ配置しました。");
        }
        internal Transform ParentFor(object data, Transform original)
        {
            if (!Valid || _selection == null || R.Get<string>(data, "parentKind") == "managed") return original;
            string id = R.Get<string>(data, "id");
            if (_anchors.TryGetValue(id, out var existing) && existing.Transform != null)
            {
                if (existing.Parent.kind != R.Get<string>(data, "parentKind") || (existing.Parent.reference ?? "") != (R.Get<string>(data, "parentRefId") ?? ""))
                {
                    while (existing.Transform.childCount > 0) existing.Transform.GetChild(0).SetParent(existing.Original != null ? existing.Original : null, true);
                    UnityEngine.Object.Destroy(existing.Transform.gameObject); _anchors.Remove(id); return original;
                }
                if (original != existing.Transform) existing.Original = original;
                return existing.Transform;
            }
            var part = _selection.Parts[_selection.BasePart];
            var parent = part.source.parents.SingleOrDefault(p => p.objectId == id);
            if (parent == null) return original; // A newly created object is authored in the current place.
            if (parent.kind != R.Get<string>(data, "parentKind") || (parent.reference ?? "") != (R.Get<string>(data, "parentRefId") ?? "")) return original;
            RuntimeBridge.Unit(original);
            var go = new GameObject("__HLocationPlacement_" + id) { hideFlags = HideFlags.HideAndDontSave };
            var anchor = new Anchor { Transform = go.transform, Original = original, Parent = parent, Source = part.source };
            anchor.Transform.SetParent(Bridge.Female, false); anchor.Transform.localScale = Vector3.one;
            var placed = Project(anchor.Source, anchor.Parent.world.ToPose());
            anchor.Transform.SetPositionAndRotation(placed.position, placed.rotation);
            _anchors[id] = anchor;
            Trace("Composer.anchor", () => "preset=" + _selection.Snapshot.Name + " id=" + id + " source={" + SourceText(anchor.Source) +
                "} savedParent={" + PoseText(anchor.Parent.world.ToPose()) + "} result={" + PoseText(placed) + "} actual={" + PoseText(RuntimeBridge.Pose(anchor.Transform)) + "}");
            return anchor.Transform;
        }
        internal void RestoreIdentity(object runtime)
        {
            if (runtime == null || _anchors.Count == 0) return;
            var data = R.Get(runtime, "Data");
            string id = R.Get<string>(data, "id");
            if (!_anchors.TryGetValue(id, out var anchor)) return;
            var go = R.Get<GameObject>(runtime, "GameObject");
            if (go == null || go.transform.parent != anchor.Transform) return;
            R.Set(data, "parentKind", anchor.Parent.kind); R.Set(data, "parentRefId", anchor.Parent.reference); R.Set(data, "parentId", null);
        }
        private void UpdateAnchors()
        {
            foreach (var anchor in _anchors.Values)
            {
                if (anchor.Transform == null) continue;
                var pose = Project(anchor.Source, anchor.Parent.world.ToPose());
                anchor.Transform.SetPositionAndRotation(pose.position, pose.rotation);
                Trace("Composer.anchor.move", () => "preset=" + _selection?.Snapshot.Name + " source={" + SourceText(anchor.Source) +
                    "} savedParent={" + PoseText(anchor.Parent.world.ToPose()) + "} destination={" + PoseText(_destination.Value) +
                    "} result={" + PoseText(pose) + "} actual={" + PoseText(RuntimeBridge.Pose(anchor.Transform)) + "}");
            }
        }
        internal void ClearComposer()
        { ClearAnchors(); _selection = null; }
        private void ClearAnchors()
        {
            foreach (var anchor in _anchors.Values)
            {
                if (anchor.Transform == null) continue;
                while (anchor.Transform.childCount > 0) anchor.Transform.GetChild(0).SetParent(anchor.Original != null ? anchor.Original : null, true);
                UnityEngine.Object.Destroy(anchor.Transform.gameObject);
            }
            _anchors.Clear();
        }
        internal void NormalizeCaches()
        {
            if (!Valid || _normalizing) return;
            object active = R.Get(Bridge.Composer, "_activePreset");
            if (active == null) return;
            if (_selection == null || !ReferenceEquals(_selection.Asset, active)) SelectComposer(active);
            if (_selection == null) return;
            _normalizing = true;
            try
            {
                var basePart = _selection.Parts[_selection.BasePart];
                foreach (var pair in new[] { new[] { "loop1Json", "_activeL1ById" }, new[] { "loop2Json", "_activeL2ById" } })
                {
                    if (!_selection.Parts.TryGetValue(pair[0], out var endpoint)) continue;
                    var cache = R.Get(Bridge.Composer, pair[1]) as IDictionary;
                    if (cache == null) continue;
                    foreach (var parent in endpoint.source.parents)
                    {
                        var reference = basePart.source.parents.SingleOrDefault(p => p.objectId == parent.objectId);
                        if (reference == null || !cache.Contains(parent.objectId)) continue;
                        var relative = Project(basePart.source, reference.world.ToPose()).Inverse() * Project(endpoint.source, parent.world.ToPose());
                        object data = cache[parent.objectId];
                        var local = new RigidPose(R.Get<Vector3>(data, "localPosition"), Quaternion.Euler(R.Get<Vector3>(data, "localEulerAngles")));
                        var converted = relative * local;
                        R.Set(data, "localPosition", converted.position); R.Set(data, "localEulerAngles", converted.rotation.eulerAngles);
                        if (R.Get<int>(data, "motionMode") == 3) R.Set(data, "pistonAxis", relative.rotation * R.Get<Vector3>(data, "pistonAxis"));
                    }
                }
            }
            finally { _normalizing = false; }
            BuildRuleProjection();
        }
        private PlaceOrigin ComposerSaveSource(SavedPart part, PlaceOrigin current)
        {
            if (_selection == null || !_selection.Parts.TryGetValue(part.Name, out var previous) || previous.fingerprint != part.Fingerprint) return current;
            // Unchanged authored endpoints can have different parent frames. Rebase each endpoint's parent,
            // never sample a moving child or falsely claim both slots had the displayed slot's parent.
            return new PlaceOrigin { mapKey = current.mapKey, mapName = current.mapName, placeId = current.placeId, placeName = current.placeName,
                poseKey = current.poseKey, utc = current.utc, femaleRoot = current.femaleRoot, mapRoot = current.mapRoot, composerLinks = current.composerLinks,
                parents = previous.source.parents.Select(p => new ParentOrigin { objectId = p.objectId, kind = p.kind, reference = p.reference,
                    world = PoseNumbers.From(Project(previous.source, p.world.ToPose())) }).ToList() };
        }
        private void RefreshComposerAfterSave()
        {
            var active = R.Get(Bridge.Composer, "_activePreset");
            if (active == null) return;
            var snapshot = Bridge.ComposerSnapshot(active);
            if (_selection == null) SelectComposer(active);
            if (_selection == null) return;
            _selection.Asset = active; _selection.Snapshot = snapshot;
            foreach (var part in snapshot.Parts)
            {
                var origin = _store.Match(Bridge.ComposerPath, snapshot, part);
                if (origin != null) _selection.Parts[part.Name] = origin;
            }
            if (!_selection.Parts.TryGetValue(_selection.BasePart, out var baseline)) return;
            foreach (var pair in _anchors)
            {
                var parent = baseline.source.parents.SingleOrDefault(p => p.objectId == pair.Key);
                if (parent != null) { pair.Value.Source = baseline.source; pair.Value.Parent = parent; }
            }
            foreach (DictionaryEntry item in Bridge.Objects)
            {
                var data = R.Get(item.Value, "Data"); if (R.Get<string>(data, "parentKind") == "managed") continue;
                var transform = R.Get<GameObject>(item.Value, "GameObject")?.transform;
                if (transform?.parent == null) continue;
                var parent = ParentFor(data, transform.parent);
                if (parent != transform.parent) transform.SetParent(parent, false);
            }
            // Native cache rebuilding decodes authored endpoint data; it does not restart rotation time.
            R.Call(Bridge.Composer, "RebuildActivePresetCache");
        }
    }
}
