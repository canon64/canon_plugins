using System;
using System.Reflection;
using UnityEngine;

namespace MainGirlShoulderIkStabilizer
{
    // Reuse KKS's real shoulder/upper-arm deformation, with every other category excluded.
    internal sealed class NativeShoulderCorrection : IDisposable
    {
        private readonly ExpressionBone expression;
        private readonly FieldInfo[] switches;
        private readonly bool[] saved;
        private readonly BoneState[] bones = new BoneState[8];
        private bool applying;
        internal int ChangedBones { get; private set; }
        internal bool Ready => expression != null && expression.dictBone != null && expression.dictBone.Count > 0;
        private sealed class BoneState
        {
            internal Transform Bone;
            internal Vector3 BeforePosition, WrittenPosition;
            internal Quaternion BeforeRotation, WrittenRotation;
            internal bool Owned;
            internal void Capture()
            {
                if (Bone == null) return;
                BeforePosition = Bone.localPosition; BeforeRotation = Bone.localRotation; Owned = true;
            }
            internal bool Commit()
            {
                if (Bone == null || !Owned) return false;
                WrittenPosition = Bone.localPosition; WrittenRotation = Bone.localRotation;
                return Vector3.Distance(BeforePosition, WrittenPosition) > .000001f ||
                    Mathf.Abs(Quaternion.Dot(BeforeRotation, WrittenRotation)) < .999999f;
            }
            internal void Restore()
            {
                if (!Owned) return;
                Owned = false;
                if (Bone == null) return;
                if (Vector3.Distance(Bone.localPosition, WrittenPosition) < .000001f) Bone.localPosition = BeforePosition;
                if (Mathf.Abs(Quaternion.Dot(Bone.localRotation, WrittenRotation)) > .999999f) Bone.localRotation = BeforeRotation;
            }
        }
        internal NativeShoulderCorrection(ExpressionBone target)
        {
            expression = target;
            var list = new System.Collections.Generic.List<FieldInfo>();
            foreach (var field in typeof(ExpressionBone).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (field.FieldType == typeof(bool) && field.Name.StartsWith("enable", StringComparison.Ordinal)) list.Add(field);
            switches = list.ToArray(); saved = new bool[switches.Length];
            var names = new[] { ExpressionBone.BoneName.cf_d_shoulder02_L, ExpressionBone.BoneName.cf_d_arm01_L,
                ExpressionBone.BoneName.cf_d_arm02_L, ExpressionBone.BoneName.cf_d_arm03_L,
                ExpressionBone.BoneName.cf_d_shoulder02_R, ExpressionBone.BoneName.cf_d_arm01_R,
                ExpressionBone.BoneName.cf_d_arm02_R, ExpressionBone.BoneName.cf_d_arm03_R };
            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = null;
                if (Ready) expression.dictBone.TryGetValue((int)names[i], out bone);
                bones[i] = new BoneState { Bone = bone };
            }
        }
        internal bool Apply(bool left, bool right)
        {
            ChangedBones = 0;
            if (applying || !Ready || (!left && !right)) return false;
            applying = true;
            // Save all flags before altering any of them, including the global native switch.
            for (int i = 0; i < switches.Length; i++) saved[i] = (bool)switches[i].GetValue(expression);
            try
            {
                for (int i = 0; i < switches.Length; i++)
                {
                    string name = switches[i].Name;
                    bool selected = name == "enable" || left && (name == "enableShoulderL" || name == "enableUpperarmL") ||
                        right && (name == "enableShoulderR" || name == "enableUpperarmR");
                    switches[i].SetValue(expression, selected);
                }
                for (int i = 0; i < bones.Length; i++) if (i < 4 ? left : right) bones[i].Capture();
                expression.LateUpdate();
                foreach (var bone in bones) if (bone.Commit()) ChangedBones++;
                return true;
            }
            catch
            {
                foreach (var bone in bones) bone.Commit();
                Restore();
                throw;
            }
            finally
            {
                for (int i = 0; i < switches.Length; i++) switches[i].SetValue(expression, saved[i]);
                applying = false;
            }
        }
        internal void Restore() { foreach (var bone in bones) bone.Restore(); }
        public void Dispose() => Restore();
    }
}
