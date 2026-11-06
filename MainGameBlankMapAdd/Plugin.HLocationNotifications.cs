using System;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        public static event Action<ChaControl, long, string, string> HLocationMoving;
        public static event Action<ChaControl, long, string, string> HLocationMoved;
        private static long _hLocationOperation;

        private long BeginHLocationNotification(string operation, string placeId)
        {
            if (HLocationMoving == null && HLocationMoved == null) return 0;
            long id = ++_hLocationOperation;
            NotifyHLocation(HLocationMoving, id, operation, placeId);
            return id;
        }

        private void EndHLocationNotification(long id, string operation, string placeId)
        {
            if (id != 0) NotifyHLocation(HLocationMoved, id, operation, placeId);
        }

        private void NotifyHLocation(Action<ChaControl, long, string, string> handlers, long id, string operation, string placeId)
        {
            if (handlers == null) return;
            foreach (Action<ChaControl, long, string, string> handler in handlers.GetInvocationList())
                try { handler(_femaleChara, id, operation, placeId); }
                catch { /* A diagnostic observer must not change placement success or rollback. */ }
        }
    }
}
