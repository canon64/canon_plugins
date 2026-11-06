using System;
using System.Collections.Generic;
using System.Reflection;

namespace MainGameAibuPoseClones
{
    internal static class PoseCloneList
    {
        private static readonly MethodInfo ShallowCopyMethod = typeof(object).GetMethod(
            "MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Dictionary<int, string> TargetNames = new Dictionary<int, string>
        {
            { 0, "立ち愛撫" }, { 1, "仰向け愛撫" }, { 7, "座り愛撫" }
        };

        internal static void AddMissing(List<HSceneProc.AnimationListInfo> entries,
            Action<HSceneProc.AnimationListInfo> added)
        {
            if (entries == null) return;
            // Clone only available originals; preserve map/progress restrictions.
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var original = entries[i];
                if (original == null || original.mode != HFlag.EMode.aibu
                    || !TargetNames.TryGetValue(original.id, out string name)
                    || !string.Equals(original.nameAnimation, name, StringComparison.Ordinal)) continue;
                if (entries.Exists(x => x != null && x.id == original.id && IsClone(x))) continue;
                var clone = (HSceneProc.AnimationListInfo)ShallowCopyMethod.Invoke(original, null);
                clone.nameAnimation = name + "【クローン】";
                entries.Insert(i + 1, clone);
                added?.Invoke(clone);
            }
        }

        internal static bool IsClone(HSceneProc.AnimationListInfo info)
        {
            return info != null && info.mode == HFlag.EMode.aibu
                && TargetNames.TryGetValue(info.id, out string name)
                && string.Equals(info.nameAnimation, name + "【クローン】", StringComparison.Ordinal);
        }
    }
}
