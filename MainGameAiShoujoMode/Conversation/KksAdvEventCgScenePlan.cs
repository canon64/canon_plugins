using System;
using System.Collections.Generic;

namespace MainGameAiShoujoMode.Conversation
{
    internal static class KksAdvEventCgScenePlan
    {
        internal sealed class Entry
        {
            internal int Slot;
            internal ADV.EventCG.Data.Scene.Chara Step;
            internal ADV.EventCG.Data.Scene.Chara.MotionAndItem PreviousIk, PreviousYure;
        }
        internal static IReadOnlyList<Entry> Create(IReadOnlyList<ADV.EventCG.Data.Scene> scenes, int index, IEnumerable<int> registeredSlots, int zero, int one)
        {
            var result = new List<Entry>();
            if (scenes == null || index < zero || index >= scenes.Count || scenes[index] == null) return result;
            var scene = scenes[index]; var previous = index > zero ? scenes[index - one] : null;
            foreach (var slot in registeredSlots)
            {
                var step = scene.FindGet(slot);
                if (step == null) continue;
                var prior = previous?.FindGet(slot)?.motionAndItem;
                result.Add(new Entry
                {
                    Slot = slot, Step = step,
                    PreviousIk = string.IsNullOrEmpty(step.motionAndItem.ik.bundle) ? prior : null,
                    PreviousYure = string.IsNullOrEmpty(step.motionAndItem.yure.bundle) ? prior : null
                });
            }
            return result;
        }
    }
}
