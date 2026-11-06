using System;
using System.Collections.Generic;

namespace MainGameVrDildo
{
    internal static class ExpressionSlotChoices
    {
        internal const int Count = 5;
        internal static int[] Normalize(int[] slots, int[] legacy)
        {
            var source = slots ?? legacy ?? new int[0];
            var result = new[] { -1, -1, -1, -1, -1 };
            for (int i = 0; i < Math.Min(Count, source.Length); i++) result[i] = Math.Max(-1, source[i]);
            return result;
        }
        internal static int Choose(int[] slots, bool random, int previous, int maximum, int fallback, Func<int, int> index)
        {
            if (maximum < 0) return -1;
            var choices = new List<int>();
            if (slots != null) for (int i = 0; i < Math.Min(Count, slots.Length); i++)
            {
                if (slots[i] < 0) continue;
                int value = Math.Min(maximum, slots[i]);
                if (!random) return value;
                if (!choices.Contains(value)) choices.Add(value);
            }
            if (choices.Count == 0) return Math.Min(maximum, Math.Max(0, fallback));
            if (choices.Count > 1) choices.Remove(previous);
            return choices[index(choices.Count)];
        }
    }
}
