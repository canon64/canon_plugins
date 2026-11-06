using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MainGameDevelopmentBridge
{
    // Read only; called on the bridge's existing Unity main-thread dispatcher.
    internal static class NotificationSnapshot
    {
        public static object Read()
        {
            var api = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("MainGameNotifications.NotificationApi", false))
                .FirstOrDefault(t => t != null);
            if (api == null) return Result(false, "plugin-not-loaded", new object[0]);
            if (!(bool)api.GetProperty("IsAvailable").GetValue(null, null))
                return Result(false, "service-not-initialized", new object[0]);
            var snapshot = (IEnumerable)api.GetMethod("GetNotifications", Type.EmptyTypes).Invoke(null, null);
            var items = snapshot.Cast<object>().Select(item =>
            {
                var type = item.GetType();
                Func<string, object> get = name => type.GetProperty(name).GetValue(item, null);
                return (object)new Dictionary<string, object> {
                    { "ownerGuid", get("OwnerGuid") }, { "id", get("Id") },
                    { "title", get("Title") }, { "body", get("Body") },
                    { "severity", get("Severity").ToString() },
                    { "updatedUtc", ((DateTime)get("UpdatedUtc")).ToString("O", CultureInfo.InvariantCulture) }
                };
            }).ToArray();
            return Result(true, null, items);
        }
        private static object Result(bool available, string reason, object[] items)
            => new Dictionary<string, object> { { "available", available }, { "reason", reason },
                { "count", items.Length }, { "notifications", items } };
    }
}
