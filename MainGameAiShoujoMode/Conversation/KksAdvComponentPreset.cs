using System;
using System.Linq;
using System.Reflection;

namespace MainGameAiShoujoMode.Conversation
{
    internal static class KksAdvComponentPreset
    {
        internal static FieldInfo[] Fields(Type type) => type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(field => !field.IsInitOnly && !field.IsStatic).ToArray();
        internal static void Copy(object source, object target)
        {
            if (source == null || target == null) throw new ArgumentNullException(source == null ? nameof(source) : nameof(target));
            if (source.GetType() != target.GetType()) throw new ArgumentException("KKS effect preset types do not match.");
            foreach (var field in Fields(source.GetType())) field.SetValue(target, field.GetValue(source));
        }
    }
}
