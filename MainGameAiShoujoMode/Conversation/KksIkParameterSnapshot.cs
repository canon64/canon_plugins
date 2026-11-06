using System;
using System.Linq;
using System.Reflection;

namespace MainGameAiShoujoMode.Conversation
{
    internal static class KksIkParameterSnapshot
    {
        internal static Action Capture(object value)
        {
            if (value == null) return () => { };
            // Preserve motion parameters, never FinalIK's private solver caches.
            var fields = value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(field => !field.IsInitOnly && (field.FieldType.IsValueType ||
                    typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))).ToArray();
            var saved = fields.Select(field => field.GetValue(value)).ToArray();
            return () => { for (int index = default(int); index < fields.Length; ++index) fields[index].SetValue(value, saved[index]); };
        }
    }
}
