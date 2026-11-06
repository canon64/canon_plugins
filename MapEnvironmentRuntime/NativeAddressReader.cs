using System;
using System.Globalization;
using System.Reflection;

namespace MapEnvironmentRuntime
{
    // Diagnostic identity only: reads the managed cached address, never dereferences native memory.
    internal static class NativeAddressReader
    {
        internal static string Read(object value, Type owner)
        {
            if (value == null) return "null";
            var field = owner.GetField("m_CachedPtr", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null) return "unavailable";
            try
            {
                var pointer = field.GetValue(value);
                return pointer is IntPtr address ? "0x" + address.ToInt64().ToString("X", CultureInfo.InvariantCulture) : "unavailable";
            }
            catch (Exception error) { return "unavailable:" + error.GetType().Name; }
        }
    }
}
