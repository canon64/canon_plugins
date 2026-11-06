using System;
using CanonGate;
using UnityEngine;

internal static class HotelModeTests
{
    internal static void Run(Action<bool, string> check)
    {
        GateApi.SetRunning(true);
        var owner = new MonoBehaviour(); var h = new HSceneProc();
        var policy = new ExceptionPolicy(); policy.Load("", "", "", "hotel.client");
        bool started = false, stopped = false, otherStarted = false, otherStopped = false;
        using (var registration = GateApi.Register("hotel.client", () => started = true, () => stopped = true))
        using (var unrelated = GateApi.Register("not.hotel", () => otherStarted = true, () => otherStopped = true))
        {
            GateApi.BeginSession(GateApi.SessionKind.H, false, policy, h);
            check(!started && !otherStarted, "hotel permissions do not authorize unrelated ordinary H");
            GateApi.EndSession(GateApi.SessionKind.H);
            GateApi.BeginSession(GateApi.SessionKind.H, false, policy, h, hotelMode: true);
            check(!GateApi.Current.IsHotelMode && !GateApi.IsAllowed && !otherStarted, "hotel flag without an owning scope grants no permission");
            GateApi.EndSession(GateApi.SessionKind.H);
            using (var scope = GateApi.EnterHotelMode(owner))
            {
                scope.Bind(owner, h);
                check(scope.Owns(h) && !scope.Owns(new HSceneProc()), "hotel scope binds exact scene");
                GateApi.BeginSession(GateApi.SessionKind.H, false, policy, h, hotelMode: true);
                var permit = GateApi.Capture("hotel.client");
                check(started && permit.IsValid && GateApi.Current.IsHotelMode && !GateApi.Current.IsAiShoujoMode, "hotel origin and registered client permission");
                var otherPermit = GateApi.Capture("not.hotel");
                var legacyPermit = GateApi.Capture();
                check(otherStarted && GateApi.IsAllowed && otherPermit.IsValid && legacyPermit.IsValid, "owned hotel H grants all registered and legacy clients without a whitelist");
                scope.Dispose();
                check(stopped && otherStopped && !permit.IsValid && !otherPermit.IsValid && !legacyPermit.IsValid && GateApi.HotelMode == null && GateApi.Current == null, "hotel cleanup revokes every owned permission");
            }
            started = otherStarted = false;
            GateApi.BeginSession(GateApi.SessionKind.H, false, policy, h);
            check(!started && !otherStarted && !GateApi.IsAllowed, "hotel full permission does not leak into the next ordinary H");
            GateApi.EndSession(GateApi.SessionKind.H);
        }
        using (var allRooms = GateApi.Register("all.rooms.client", () => { }, () => { }))
        {
            foreach (string room in new[] { "201", "202", "203", "204", "205", "206" })
            {
                using (var scope = GateApi.EnterHotelMode(owner, room))
                {
                    scope.Bind(owner, h);
                    GateApi.BeginSession(GateApi.SessionKind.H, false, policy, h, hotelMode: true);
                    var roomPermit = GateApi.Capture("all.rooms.client");
                    check(GateApi.Current.HotelRoomId == room && roomPermit.IsValid && GateApi.IsAllowed, "all plugins authorized in hotel room " + room);
                    scope.Dispose();
                    check(!roomPermit.IsValid && GateApi.Current == null, "room permission ends on exit " + room);
                }
            }
        }
        using (var scope = GateApi.EnterHotelMode(owner, "201"))
        using (var endClient = GateApi.Register("ending.client", () => { }, () => { }))
        {
            scope.Bind(owner, h);
            GateApi.BeginSession(GateApi.SessionKind.H, false, policy, h, hotelMode: true);
            var validUntilEnd = GateApi.Capture("ending.client"); bool observed = false, laterObserver = false;
            Action<GateApi.SessionContext> first = context => { observed = context?.Scene == h && validUntilEnd.IsValid && GateApi.CanRun("ending.client"); throw new InvalidOperationException("injected final save failure"); };
            Action<GateApi.SessionContext> second = context => laterObserver = validUntilEnd.IsValid;
            GateApi.SessionEnding += first; GateApi.SessionEnding += second;
            try { GateApi.EndSession(GateApi.SessionKind.H); }
            finally { GateApi.SessionEnding -= first; GateApi.SessionEnding -= second; }
            check(observed && laterObserver && !validUntilEnd.IsValid && GateApi.Current == null, "pre-end save observers see valid permit; one exception cannot stop later observers or cleanup");
        }
        GateApi.SetRunning(false);
    }
}
