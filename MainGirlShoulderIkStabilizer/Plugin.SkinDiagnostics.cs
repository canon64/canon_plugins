using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MainGirlShoulderIkStabilizer
{
    public sealed partial class ShoulderIkStabilizerPlugin
    {
        // Once per diagnostic binding, not per solver pass or frame.
        private void ReportSkinBinding(Dictionary<string,Transform> bodyBones)
        {
            if (!NativeDiagnosticEnabled || diagnosticActor == null) return;
            foreach (var renderer in diagnosticActor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = renderer.sharedMesh;
                if (mesh == null) continue;
                var bones = renderer.bones;
                var selected = new HashSet<int>();
                for (int i = 0; i < bones.Length; i++)
                {
                    if (bones[i] == null) continue;
                    string name = bones[i].name;
                    if (name.StartsWith("cf_d_shoulder", StringComparison.Ordinal) || name.StartsWith("cf_j_shoulder", StringComparison.Ordinal) ||
                        name.StartsWith("cf_j_arm00", StringComparison.Ordinal) || name.StartsWith("cf_d_arm0", StringComparison.Ordinal) ||
                        name.StartsWith("cf_s_arm", StringComparison.Ordinal)) selected.Add(i);
                }
                if (selected.Count == 0) continue;
                var b = new StringBuilder("SKIN_BIND renderer=\"" + renderer.name + "\" mesh=\"" + mesh.name +
                    "\" vertices=" + mesh.vertexCount + " enabled=" + renderer.enabled + " active=" + renderer.gameObject.activeInHierarchy);
                var counts = new Dictionary<int,int>();
                try
                {
                    foreach (var weight in mesh.boneWeights)
                    {
                        CountSkinInfluence(counts, selected, weight.boneIndex0, weight.weight0);
                        CountSkinInfluence(counts, selected, weight.boneIndex1, weight.weight1);
                        CountSkinInfluence(counts, selected, weight.boneIndex2, weight.weight2);
                        CountSkinInfluence(counts, selected, weight.boneIndex3, weight.weight3);
                    }
                }
                catch (Exception ex) { b.Append(" weightsUnavailable=").Append(ex.GetType().Name); }
                foreach (int index in selected)
                {
                    b.Append("\nSKIN_BONE renderer=\"").Append(renderer.name).Append("\" index=").Append(index)
                        .Append(" bone=").Append(bones[index].name).Append(" weightedVertices=")
                        .Append(counts.TryGetValue(index, out int count) ? count.ToString() : "0_or_unavailable")
                        .Append(" bodyBoneMatch=").Append(bodyBones.TryGetValue(bones[index].name, out var bodyBone) && ReferenceEquals(bodyBone,bones[index]))
                        .Append(" parent=").Append(bones[index].parent == null ? "null" : bones[index].parent.name);
                }
                WriteDiagnostic(b.ToString());
            }
        }
        private static void CountSkinInfluence(Dictionary<int,int> counts, HashSet<int> selected, int index, float weight)
        {
            if (weight > 0f && selected.Contains(index)) counts[index] = counts.TryGetValue(index, out int count) ? count + 1 : 1;
        }
    }
}
