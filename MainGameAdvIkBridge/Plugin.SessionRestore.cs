using System;
using System.Collections.Generic;
using System.Reflection;
using SessionGate = CanonPlugins.Local.SessionGate;
namespace MainGameAdvIkBridge
{
    public sealed partial class Plugin
    {
        private readonly Dictionary<Tuple<object, PropertyInfo>, object> _sessionProperties = new Dictionary<Tuple<object, PropertyInfo>, object>();
        private void RememberSessionProperty(object target, PropertyInfo property, object value)
        {
            var key = Tuple.Create(target, property);
            if (!_sessionProperties.ContainsKey(key)) _sessionProperties.Add(key, value);
        }
        private void RestoreSessionProperties()
        {
            foreach (var pair in _sessionProperties)
                try { pair.Key.Item2.SetValue(pair.Key.Item1, pair.Value, null); }
                catch (Exception ex) { Logger.LogWarning("ADVik session restore: " + ex.Message); }
            _sessionProperties.Clear();
        }
    }
}
