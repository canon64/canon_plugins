using System;
using System.Threading;

namespace MainGameNotifications
{
    /// <summary>Unity main-thread API. Add a hard BepInEx dependency on PluginGuid.</summary>
    public static class NotificationApi
    {
        public const string PluginGuid = "com.kks.maingame.notifications";
        private static NotificationStore _store;
        private static int _thread;
        public static bool IsAvailable => _store != null;
        public static string Publish(string ownerGuid, string title, string body, NotificationSeverity severity, string notificationId = null)
            => Store.Publish(ownerGuid, title, body, severity, notificationId);
        public static bool Dismiss(string ownerGuid, string notificationId) => Store.Dismiss(ownerGuid, notificationId);
        public static int ClearOwner(string ownerGuid, bool includeFailures = false) => Store.ClearOwner(ownerGuid, includeFailures);
        public static Notification[] GetNotifications() => Store.Snapshot();
        internal static void Initialize(NotificationStore store) { _thread = Thread.CurrentThread.ManagedThreadId; _store = store; }
        internal static void Shutdown() { _store = null; _thread = 0; }
        private static NotificationStore Store
        {
            get
            {
                if (_store == null) throw new InvalidOperationException("共通通知プラグインが初期化されていません。必須依存を確認してください。");
                if (Thread.CurrentThread.ManagedThreadId != _thread) throw new InvalidOperationException("共通通知 API は Unity メインスレッドで呼び出してください。");
                return _store;
            }
        }
    }
}
