using System;
using System.Collections;
using System.Linq;
using MainGameHLocationCoordinates;
using UnityEngine;

namespace MainGameHLocationPlacement
{
    internal sealed partial class PlacementSession
    {
        internal bool SkipHipPoseEntry(object state)
        {
            if (R.Get<bool>(state, "ExternalOwner")) return true;
            if (!Valid) return false;
            var states = Bridge.States;
            for (int i = 0; i < states.Length; i++) if (ReferenceEquals(states.GetValue(i), state)) return Bridge.Linked(i);
            return false;
        }
        internal object PrepareHip(object preset)
        {
            if (!Valid || preset == null) return preset;
            RefreshDestination();
            var snapshot = Bridge.HipSnapshot(preset);
            Trace("Hip.load.begin", () => "preset=" + snapshot.Name + " id=" + snapshot.Identity + " pose=" + snapshot.PoseKey + " " + PoseText(_destination.Value));
            var clone = Bridge.CloneHip(preset);
            var entries = (Array)R.Get(clone, "entries");
            int converted = 0, independent = 0;
            foreach (var part in snapshot.Parts)
            {
                int index = int.Parse(part.Name.Substring(3), System.Globalization.CultureInfo.InvariantCulture);
                if (index >= entries.Length || index >= Bridge.States.Length) { TraceHipPart(snapshot, part, null, null, "index-missing"); continue; }
                object entry = entries.GetValue(index);
                if (!Bridge.Independent(index, true)) { TraceHipPart(snapshot, part, entry, null, _plugin.Diagnostics ? IkReason(index, true) : null); continue; }
                if (entry == null || !R.Get<bool>(entry, "hasProxyPose") || R.Get<bool>(entry, "hasFollowBone") || R.Get<bool>(entry, "followIsHmd"))
                { TraceHipPart(snapshot, part, entry, null, "no-world-pose-or-saved-follow"); continue; }
                independent++;
                var origin = _store.Match(Bridge.HipPath, snapshot, part);
                if (origin == null) { TraceHipPart(snapshot, part, entry, null, "origin-missing-or-content-mismatch"); continue; }
                TraceHipPart(snapshot, part, entry, origin, "convert-independent");
                var original = new RigidPose(R.Get<Vector3>(entry, "proxyPosition"), R.Get<Quaternion>(entry, "proxyRotation"));
                var placed = Project(origin.source, original);
                R.Set(entry, "proxyPosition", placed.position); R.Set(entry, "proxyRotation", placed.rotation);
                converted++;
                if (_plugin.Diagnostics)
                {
                    string key = Bridge.IkKeys[index];
                    Trace("Hip.load.result", () => "preset=" + snapshot.Name + " IK=" + key + " before={" + PoseText(original) + "} result={" + PoseText(placed) + "}");
                }
            }
            if (independent > converted) _plugin.Message("HipHijack「" + snapshot.Name + "」は保存元未登録のIKあり・該当IKは補正なし。");
            else if (converted > 0) _plugin.Message("HipHijack「" + snapshot.Name + "」の個別IKを現在のH場所へ変換しました。");
            Trace("Hip.load.end", () => "preset=" + snapshot.Name + " converted=" + converted + " independent=" + independent);
            return clone;
        }
    }
}
