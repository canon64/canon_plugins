using System;
using System.Collections.Generic;

namespace MainGameObjectComposer
{
    internal static class PresetWriteTransaction
    {
        // Candidate is a complete independent clone. Failed writes cannot mutate the original.
        internal static bool Commit<T>(IList<T> entries, T original, T candidate, bool append, Func<bool> write,
            Action<T, T> adopt = null) where T : class
        {
            int index = append ? entries.Count : entries.IndexOf(original);
            if (index < 0) return false;
            if (append) entries.Add(candidate); else entries[index] = candidate;
            bool success = false;
            try
            {
                success = write();
                if (success && !append && adopt != null)
                {
                    // Retain existing references used by pending navigation/delete actions.
                    adopt(candidate, original);
                    entries[index] = original;
                }
                return success;
            }
            finally
            {
                if (!success)
                {
                    if (append) entries.RemoveAt(index); else entries[index] = original;
                }
            }
        }
    }
}
