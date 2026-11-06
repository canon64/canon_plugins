using System.Collections.Generic;
using MainGameDevelopmentBridge;

namespace DevelopmentNotifications
{
    public sealed class Command : IDevelopmentCommand
    {
        public string Name => "notifications";
        public object Execute(Dictionary<string, object> arguments, DevelopmentContext context)
            => NotificationSnapshot.Read();
        public void Dispose() { } // No subscriptions, mutations, or generated objects.
    }
}
