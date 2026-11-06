using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MainGameObjectComposer;
using UnityEngine;

internal static class PresetEditorTests
{
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true, IgnoreReadOnlyProperties = true };
    private static string Encode<T>(T value) => JsonSerializer.Serialize(value, Json);
    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(Encode(value), Json);
    private static ManagedObjectData Read(string value) => JsonSerializer.Deserialize<ManagedObjectData>(value, Json);
    private sealed class Pose
    {
        public string id = "body";
        public Vector3 localPosition;
        public float radius;
        public float phaseContinuityOffsetTurns;
        public Vector3 worldPosition;
    }

    internal static void Run(Action<bool, string> check)
    {
        var view = new PresetEditorState();
        view.Open();
        check(view.EditSlot == 1 && view.FixedSlot == 1 && !view.Preview && view.Blend(.6f) == 0, "editor opens at fixed L1");
        view.Select(2); view.Select(0);
        check(view.EditSlot == 0 && view.FixedSlot == 2 && view.Blend(.3f) == 1, "both-edit keeps the previously displayed L2");
        view.StartPreview();
        check(view.Preview && view.Blend(.3f) == .3f && view.EditSlot == 0, "preview changes blend without changing edit destination");
        view.StopPreview();
        check(!view.Preview && view.FixedSlot == 2 && view.Blend(.3f) == 1, "stopping preview returns to the previous fixed endpoint");
        view.StartPreview(); view.Select(1);
        check(!view.Preview && view.Blend(.9f) == 0, "selecting an endpoint cannot leave blended placement editing active");
        view.Open();
        check(view.EditSlot == 1 && !view.Preview, "discard and next preset reset preview state");

        var before = new Pose { localPosition = new Vector3(1, 2, 3), radius = 2 };
        var after = new Pose { localPosition = new Vector3(2, 2, 3), radius = 3, worldPosition = new Vector3(999, 999, 999) };
        var l1 = Clone(before);
        var l2 = new Pose { localPosition = new Vector3(5, 2, 3), radius = 4 };
        PresetEditFields.Apply(before, after, l1, false);
        check(l1.localPosition.x == 2 && l2.localPosition.x == 5, "L1-only edits do not touch L2");
        l1 = Clone(before);
        PresetEditFields.Apply(before, after, l1, true); PresetEditFields.Apply(before, after, l2, true);
        check(l2.localPosition.x - l1.localPosition.x == 4 && l2.radius - l1.radius == 2, "both-edit preserves endpoint differences");
        check(l1.worldPosition.x == 0 && l2.worldPosition.x == 0, "derived coordinate pairs are not mistaken for user edits");

        var source = new PresetEntry {
            key = "aibu/1/O", displayName = "original", animationName = "仰向け愛撫【クローン】",
            useLoopSplit = true, autoLoadEligible = false, applyToOther = false,
            ikLinks = new List<string> { "body\tbody" },
            movementEnabled = true, movementVersion = 1, movementFullMultiplier = 1.5f,
            movementTargetIds = new List<string> { "body" }, movementRulesJson = new List<string> { "movement-rule" },
            motionIntensityRulesJson = new List<string> { "intensity-rule" }, motionPositionRulesJson = new List<string> { "position-rule" },
            betterPenetrationTargetConfigured = true, betterPenetrationTargetEnabled = true, betterPenetrationVaginaTarget = .1f,
            loop1Json = new List<string> { Encode(new ManagedObjectData { parentKind = "root", localPosition = new Vector3(3, .5f, -64) }) },
            loop2Json = new List<string> { Encode(new ManagedObjectData { parentKind = "root", localPosition = new Vector3(4, .8f, -64) }) }
        };
        string original = Encode(source);
        var draft = Clone(source);
        var candidate = Clone(draft);
        var root = new Transform { position = new Vector3(2, 0, -64), rotation = Quaternion.Euler(new Vector3(0, 90, 0)) };
        var composer = new Plugin(root);
        PresetEndpointCoordinates.Capture(candidate, data => data.Select(raw => {
            var obj = Read(raw); composer.Capture(obj); return Encode(obj);
        }).ToList());
        var savedL1 = Read(candidate.loop1Json[0]); var savedL2 = Read(candidate.loop2Json[0]);
        check(savedL1.worldPosition.x == 3 && savedL2.worldPosition.x == 4, "save recalculates each authored endpoint instead of the displayed blend");
        check(savedL1.femaleRootPose != null && savedL2.femaleRootPose != null
            && Math.Abs(savedL1.femaleRootPose.z - savedL2.femaleRootPose.z) > .9f, "one save adds distinct female-root coordinates to both legacy endpoints");
        composer.Resolve(savedL1, true); composer.Resolve(savedL2, true);
        check(savedL1.parentRefId == "char:root" && savedL2.parentRefId == "char:root", "saved endpoints reload in female-root mode");
        composer.Resolve(savedL1, false); composer.Resolve(savedL2, false);
        check(savedL1.localPosition.x == 3 && savedL2.localPosition.x == 4, "saved endpoints reload in world mode without collapsing their difference");

        var entries = new List<PresetEntry> { source };
        string disk = null;
        bool success = PresetWriteTransaction.Commit(entries, source, candidate, false, () => { disk = Encode(entries); return true; });
        var loaded = JsonSerializer.Deserialize<List<PresetEntry>>(disk, Json)[0];
        check(success && entries.Count == 1 && loaded.loop1Json[0] != loaded.loop2Json[0], "one transaction writes a complete independently reloadable preset");
        check(loaded.ikLinks.SequenceEqual(source.ikLinks) && loaded.movementRulesJson.SequenceEqual(source.movementRulesJson)
            && loaded.motionIntensityRulesJson.SequenceEqual(source.motionIntensityRulesJson)
            && loaded.motionPositionRulesJson.SequenceEqual(source.motionPositionRulesJson)
            && loaded.betterPenetrationTargetEnabled && !loaded.applyToOther && !loaded.autoLoadEligible, "whole-preset save retains IK links, motion rules and application conditions");
        check(Encode(source) == original && Encode(draft) == original, "persisting a candidate never mutates the original or editing draft");

        entries = new List<PresetEntry> { source };
        success = PresetWriteTransaction.Commit(entries, source, candidate, false, () => false);
        check(!success && ReferenceEquals(entries[0], source) && Encode(source) == original, "failed save restores the original preset and leaves the draft available");
        try { PresetWriteTransaction.Commit(entries, source, candidate, false, () => throw new Exception("disk failure")); }
        catch (Exception) { }
        check(ReferenceEquals(entries[0], source), "writer exceptions also roll back the preset list");
        var alias = Clone(candidate); alias.displayName = "another name";
        success = PresetWriteTransaction.Commit(entries, source, alias, true, () => { disk = Encode(entries); return true; });
        check(success && entries.Count == 2 && ReferenceEquals(entries[0], source) && Encode(source) == original,
            "save-as appends a full copy without overwriting the original");
        check(alias.loop1Json.SequenceEqual(candidate.loop1Json) && alias.loop2Json.SequenceEqual(candidate.loop2Json)
            && alias.ikLinks.SequenceEqual(candidate.ikLinks) && alias.movementRulesJson.SequenceEqual(candidate.movementRulesJson), "save-as retains both endpoints and all linked settings");
        int count = entries.Count;
        success = PresetWriteTransaction.Commit(entries, source, Clone(alias), true, () => false);
        check(!success && entries.Count == count, "failed save-as leaves no phantom preset");
        success = PresetWriteTransaction.Commit(entries, new PresetEntry(), candidate, false, () => throw new Exception("must not run"));
        check(!success && entries.Count == count, "stale selection cannot overwrite a different preset");
        var unchanged = Clone(source);
        int calls = 0;
        try { PresetEndpointCoordinates.Capture(unchanged, data => ++calls == 2 ? throw new Exception("bad endpoint") : new List<string> { "converted" }); }
        catch (Exception) { }
        check(Encode(unchanged) == original, "coordinate conversion failure does not partially replace endpoint data");
        var identitySource = Clone(source);
        var identityCandidate = Clone(candidate);
        entries = new List<PresetEntry> { identitySource };
        success = PresetWriteTransaction.Commit(entries, identitySource, identityCandidate, false, () => true,
            (saved, target) => { target.loop1Json = saved.loop1Json; target.loop2Json = saved.loop2Json; });
        check(success && ReferenceEquals(entries[0], identitySource) && entries[0].loop1Json.SequenceEqual(candidate.loop1Json),
            "successful save retains source identity for pending delete/navigation actions");
    }
}
