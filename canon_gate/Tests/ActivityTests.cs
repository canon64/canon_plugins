using System;
using CanonGate;

internal static class ActivityTests
{
    internal static void Run(Action<bool, string> check)
    {
        int starts = 0, stops = 0, reads = 0;
        bool allow = false;
        GateApi.SetRunning(true);
        GateApi.AllowActivity = (guid, kind) => { ++reads; return allow; };
        using (var activity = GateApi.RegisterActivity("activity.test", GateApi.ActivityKind.Studio,
            () => ++starts, () => ++stops))
        {
            check(GateApi.IsRegistered("activity.test"), "activity participates in warning registration audit");
            bool duplicate = false;
            try { GateApi.Register("activity.test", () => { }, () => { }); }
            catch (InvalidOperationException) { duplicate = true; }
            check(duplicate, "activity and H registration share duplicate protection");
            activity.Begin(); allow = true; activity.Begin();
            check(reads == 1 && starts == 0, "denied activity is fixed until explicit end");
            activity.End(); activity.Begin(); activity.Begin();
            check(reads == 2 && starts == 1 && activity.IsActive, "explicit next activity event applies policy once");
            var oldPermit = activity.Capture();
            allow = false;
            check(activity.IsActive && reads == 2, "activity retains decision without policy polling");
            activity.End(); activity.End();
            check(stops == 1 && !activity.IsActive, "activity stop is idempotent");
            check(!oldPermit.IsValid, "ended activity invalidates captured asynchronous work");
            var policy = new ExceptionPolicy(); policy.Load("", "activity.test");
            GateApi.BeginSession(GateApi.SessionKind.H, false, policy);
            check(starts == 2 && activity.IsActive, "same registration accepts authorized H session");
            check(!oldPermit.IsValid, "new activity/H cannot revive an old activity permit");
            GateApi.SetRunning(false);
            check(stops == 2 && !activity.IsActive, "gate disable revokes and stops activity H work");
            GateApi.SetRunning(true); allow = true; activity.Begin();
            GateApi.SetRunning(false);
            check(stops == 3 && !activity.IsActive, "gate disable also stops independent activity work");
            GateApi.SetRunning(true); activity.Begin();
            GateApi.BeginSession(GateApi.SessionKind.H, false, new ExceptionPolicy());
            activity.Begin();
            check(stops == 4 && !activity.IsActive, "preview permit cannot cross or bypass a denied H session");
            GateApi.EndSession(GateApi.SessionKind.H);
            GateApi.SetRunning(false);
        }
        GateApi.SetRunning(true);
        GateApi.ActivityRegistration second = null;
        bool peerRevoked = false;
        using (var first = GateApi.RegisterActivity("activity.peer.first", GateApi.ActivityKind.Preview,
            () => { }, () => { peerRevoked = !second.IsActive; }))
        using (second = GateApi.RegisterActivity("activity.peer.second", GateApi.ActivityKind.Preview,
            () => { }, () => { }))
        {
            GateApi.BeginSession(GateApi.SessionKind.H, true, new ExceptionPolicy());
            GateApi.EndSession(GateApi.SessionKind.H);
            check(peerRevoked, "all activity permissions are revoked before the first stop callback");
        }
        GateApi.SetRunning(false);
        GateApi.AllowActivity = null;
        GateApi.SetRunning(true);
        starts = stops = reads = 0;
        GateApi.AllowActivity = (guid, kind) => { ++reads; return false; };
        using (var service = GateApi.RegisterActivity(GateApi.NotificationServiceGuid, GateApi.ActivityKind.StartupService,
            () => ++starts, () => ++stops, includeSessions: false))
        {
            service.Begin(); var permit = service.Capture();
            check(starts == 1 && reads == 0 && GateApi.IsNotificationServiceActive(GateApi.NotificationServiceGuid), "notification infrastructure starts without modifying user exception lists");
            check(!GateApi.IsNotificationServiceActive("ordinary.plugin"), "notification infrastructure grants no unrelated input permissions");
            foreach (var kind in new[] { GateApi.SessionKind.H, GateApi.SessionKind.Maker })
            {
                GateApi.BeginSession(kind, false, new ExceptionPolicy());
                check(service.IsActive && permit.IsValid && starts == 1 && stops == 0, "notification process lifetime survives denied " + kind + " entry");
                GateApi.EndSession(kind);
                check(service.IsActive && permit.IsValid, "notification process lifetime survives " + kind + " exit to title/Studio");
            }
            GateApi.SetRunning(false);
            check(!service.IsActive && !permit.IsValid && stops == 1, "gate shutdown revokes notification input and stops process service");
            service.Dispose();
            check(!GateApi.IsNotificationServiceActive(GateApi.NotificationServiceGuid) && stops == 1, "notification disposal is idempotent");
        }
        GateApi.SetRunning(true);
        using (var bridge = GateApi.RegisterActivity(GateApi.DevelopmentBridgeServiceGuid, GateApi.ActivityKind.StartupService,
            () => { }, () => { }, includeSessions: false))
        {
            bridge.Begin(); var permit = bridge.Capture();
            GateApi.BeginSession(GateApi.SessionKind.H, false, new ExceptionPolicy());
            GateApi.EndSession(GateApi.SessionKind.H);
            check(permit.IsValid && bridge.IsActive, "development bridge remains connected after room exit");
            check(!GateApi.IsNotificationServiceActive(GateApi.DevelopmentBridgeServiceGuid), "development bridge receives no notification input privilege");
        }
        GateApi.AllowActivity = null;
    }
}
