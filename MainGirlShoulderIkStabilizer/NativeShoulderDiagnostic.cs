using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace MainGirlShoulderIkStabilizer
{
    // Observes the real ExpressionBone dictionary. Never writes a transform or drives IK.
    internal sealed class NativeShoulderDiagnostic
    {
        private readonly ExpressionBone expression;
        private readonly Func<bool> enabled;
        private readonly Action<string> write;
        private readonly float interval;
        private readonly Dictionary<string, Transform> bodyBones;
        private float nextSample;
        private long sequence;
        private static readonly string[] Switches = { "enableShoulderL", "enableShoulderR", "enableUpperarmL", "enableUpperarmR" };
        private static readonly FieldInfo[] SwitchFields = Array.ConvertAll(Switches,
            name => typeof(ExpressionBone).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic));
        private static readonly ExpressionBone.BoneName[] Bones = {
            ExpressionBone.BoneName.cf_j_spine03,
            ExpressionBone.BoneName.cf_j_arm00_L, ExpressionBone.BoneName.cf_d_shoulder02_L,
            ExpressionBone.BoneName.cf_d_arm01_L, ExpressionBone.BoneName.cf_d_arm02_L,
            ExpressionBone.BoneName.cf_d_arm03_L, ExpressionBone.BoneName.n_goal_forearm_L,
            ExpressionBone.BoneName.cf_j_forearm01_L, ExpressionBone.BoneName.cf_j_hand_L,
            ExpressionBone.BoneName.cf_j_arm00_R, ExpressionBone.BoneName.cf_d_shoulder02_R,
            ExpressionBone.BoneName.cf_d_arm01_R, ExpressionBone.BoneName.cf_d_arm02_R,
            ExpressionBone.BoneName.cf_d_arm03_R, ExpressionBone.BoneName.n_goal_forearm_R,
            ExpressionBone.BoneName.cf_j_forearm01_R, ExpressionBone.BoneName.cf_j_hand_R };

        internal NativeShoulderDiagnostic(ExpressionBone target, Func<bool> isEnabled, Action<string> output, float seconds,
            Dictionary<string, Transform> actualBones = null)
        { expression = target; enabled = isEnabled; write = output; interval = seconds; bodyBones = actualBones; }
        internal bool Matches(ExpressionBone value) => ReferenceEquals(expression, value);
        internal long Begin(string stage)
        {
            if (!enabled() || Time.unscaledTime < nextSample) return 0;
            nextSample = Time.unscaledTime + interval;
            long token = ++sequence;
            Capture(stage, token);
            return token;
        }
        internal void Capture(string stage, long token)
        {
            if (token == 0 || !enabled()) return;
            string prefix = "frame=" + Time.frameCount + " nativePass=" + token + " stage=" + stage;
            var b = new StringBuilder("NATIVE " + prefix + " expressionPresent=" + (expression != null) +
                " expressionEnabled=" + (expression != null && expression.enable));
            for (int i = 0; expression != null && i < SwitchFields.Length; i++)
                b.Append(' ').Append(Switches[i]).Append('=').Append(SwitchFields[i]?.GetValue(expression) ?? "missing_field");
            foreach (var name in Bones)
            {
                var bone = Get(name);
                b.Append("\nAUX ").Append(prefix).Append(" bone=").Append(name);
                if (bone == null) { b.Append(" missing=true"); continue; }
                b.Append(" parent=").Append(bone.parent == null ? "null" : bone.parent.name)
                    .Append(" localPos=").Append(V(bone.localPosition)).Append(" localQ=").Append(Q(bone.localRotation))
                    .Append(" localScale=").Append(V(bone.localScale)).Append(" worldPos=").Append(V(bone.position))
                    .Append(" worldQ=").Append(Q(bone.rotation));
            }
            if (bodyBones != null)
                foreach (var entry in bodyBones)
                {
                    if (!entry.Key.StartsWith("cf_s_arm", StringComparison.Ordinal) && !entry.Key.StartsWith("cf_d_shoulder_", StringComparison.Ordinal)) continue;
                    var bone = entry.Value;
                    if (bone == null) continue;
                    b.Append("\nSKIN_AUX ").Append(prefix).Append(" bone=").Append(bone.name)
                        .Append(" parent=").Append(bone.parent == null ? "null" : bone.parent.name)
                        .Append(" localPos=").Append(V(bone.localPosition)).Append(" localQ=").Append(Q(bone.localRotation))
                        .Append(" localScale=").Append(V(bone.localScale)).Append(" worldPos=").Append(V(bone.position))
                        .Append(" worldQ=").Append(Q(bone.rotation));
                }
            Clavicle(b, prefix, "L", ExpressionBone.BoneName.cf_j_arm00_L);
            Clavicle(b, prefix, "R", ExpressionBone.BoneName.cf_j_arm00_R);
            Input(b, prefix, "L", ExpressionBone.BoneName.n_goal_forearm_L, ExpressionBone.BoneName.cf_j_forearm01_L);
            Input(b, prefix, "R", ExpressionBone.BoneName.n_goal_forearm_R, ExpressionBone.BoneName.cf_j_forearm01_R);
            write(b.ToString());
        }
        private Transform Get(ExpressionBone.BoneName name)
        {
            if (expression != null && expression.dictBone != null && expression.dictBone.TryGetValue((int)name, out var value)) return value;
            return bodyBones != null && bodyBones.TryGetValue(name.ToString(), out var bone) ? bone : null;
        }
        private void Clavicle(StringBuilder b, string prefix, string side, ExpressionBone.BoneName upperName)
        {
            var upper = Get(upperName); var bone = upper == null ? null : upper.parent;
            b.Append("\nNATIVE_CLAVICLE ").Append(prefix).Append(" side=").Append(side);
            if (bone == null) { b.Append(" missing=true"); return; }
            b.Append(" bone=").Append(bone.name).Append(" localPos=").Append(V(bone.localPosition))
                .Append(" localQ=").Append(Q(bone.localRotation)).Append(" worldPos=").Append(V(bone.position))
                .Append(" worldQ=").Append(Q(bone.rotation));
        }
        private void Input(StringBuilder b, string prefix, string side, ExpressionBone.BoneName goalName, ExpressionBone.BoneName elbowName)
        {
            var goal = Get(goalName); var elbow = Get(elbowName);
            b.Append("\nNATIVE_INPUT ").Append(prefix).Append(" side=").Append(side);
            if (goal == null || elbow == null) { b.Append(" missing=true"); return; }
            var local = goal.InverseTransformPoint(elbow.position);
            float amount = Mathf.Max(0f, .3f - Mathf.Abs(local.y));
            b.Append(" elbowInGoal=").Append(V(local)).Append(" nativeShoulderBendDeg=").Append(F(amount * 250f))
                .Append(" nativeShoulderLift=").Append(F(amount * .2f));
        }
        private static string F(float value) => value.ToString("F5", CultureInfo.InvariantCulture);
        private static string V(Vector3 value) => "(" + F(value.x) + "," + F(value.y) + "," + F(value.z) + ")";
        private static string Q(Quaternion value) => "(" + F(value.x) + "," + F(value.y) + "," + F(value.z) + "," + F(value.w) + ")";
    }
}
