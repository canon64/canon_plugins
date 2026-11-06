using System;
using System.Linq;
using System.Threading;
using MainGameNotifications;

internal static class Program
{
    private static int _checks;
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); ++_checks; Console.WriteLine("PASS " + label); }
    private static void Main()
    {
        var store = new NotificationStore();
        int changed = 0; store.Changed += () => ++changed;
        Check(!NotificationApi.IsAvailable, "API unavailable before initialization");
        bool rejected = false;
        try { NotificationApi.Publish("owner", "test", "", NotificationSeverity.Information); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "missing infrastructure is explicit");
        NotificationApi.Initialize(store);
        Check(NotificationApi.IsAvailable, "API available after initialization");
        var id = NotificationApi.Publish("hotel", "一部保存に失敗", "衣装: 取得例外。位置: 保存済み", NotificationSeverity.Failure, "save/201");
        Check(id == "save/201" && NotificationApi.GetNotifications().Length == 1, "partial failure is one grouped notification");
        NotificationApi.Publish("hotel", "一部保存に失敗", "衣装: 取得例外。位置: 保存済み", NotificationSeverity.Failure, "save/201");
        Check(changed == 1, "identical operation update is deduplicated");
        NotificationApi.Publish("hotel", "全項目の保存に失敗", "読み取り専用", NotificationSeverity.Failure, "save/201");
        Check(NotificationApi.GetNotifications().Single().Body == "読み取り専用", "same owner/id updates failure reason");
        NotificationApi.Publish("other", "別プラグイン", "", NotificationSeverity.Warning, "save/201");
        Check(NotificationApi.GetNotifications().Length == 2, "owner separates identical IDs");
        Check(!NotificationApi.Dismiss("wrong-owner", "save/201"), "owner cannot dismiss another owner's notification");
        Check(NotificationApi.ClearOwner("hotel") == 0, "normal cleanup preserves failures");
        for (int i = 0; i < 100; ++i) NotificationApi.Publish("hotel", "保存完了", "全て保存済み", NotificationSeverity.Information, "save/201");
        Check(NotificationApi.GetNotifications().Count(n => n.OwnerGuid == "hotel") == 2, "later success is deduplicated without erasing earlier failure");
        Check(NotificationApi.GetNotifications().Any(n => n.Severity == NotificationSeverity.Failure), "success does not silently erase unacknowledged failure");
        Check(NotificationApi.ClearOwner("hotel") == 1, "cleanup removes later information only");
        Check(NotificationApi.ClearOwner("hotel", true) == 1, "explicit failure cleanup is available");
        rejected = false;
        var thread = new Thread(() => { try { NotificationApi.GetNotifications(); } catch (InvalidOperationException) { rejected = true; } });
        thread.Start(); thread.Join(); Check(rejected, "API rejects worker thread Unity notification calls");
        NotificationApi.Dismiss("other", "save/201");
        Check(NotificationApi.GetNotifications().Length == 0, "explicit acknowledgement removes notification");
        NotificationApi.Shutdown(); Check(!NotificationApi.IsAvailable, "shutdown releases public API");
        Console.WriteLine("PASS " + _checks + " notification behavior checks");
    }
}
