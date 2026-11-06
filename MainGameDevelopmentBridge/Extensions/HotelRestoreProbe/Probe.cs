using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MainGameDevelopmentBridge;
using UnityEngine;

public sealed class HotelRestoreProbe : IDevelopmentCommand
{
    public string Name => "hotel-restore-probe";
    static HotelRestoreProbe active;
    Harmony patch;
    object setting;
    EventHandler changed;
    readonly List<object> records = new List<object>();
    bool Enabled => Get(setting, "Value") is bool value && value;
    static object Get(object obj, string name)
    {
        if (obj == null) return null;
        for (var type = obj as Type ?? obj.GetType(); type != null; type = type.BaseType)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var field = type.GetField(name, flags);
            if (field != null) return field.GetValue(obj is Type ? null : obj);
            var property = type.GetProperty(name, flags);
            if (property != null) return property.GetValue(obj is Type ? null : obj, null);
        }
        return null;
    }
    static object Plugin => Get(ReflectionCommands.ResolveType("MainGameAiShoujoMode.Plugin"), "_instance");
    static void HipVerified(object __instance, object[] __args, bool __result)
    {
        var owner = active;
        if (owner == null || !owner.Enabled) return;
        try
        {
            var capture = AccessTools.Method(__instance.GetType(), "CaptureHotelGadget");
            owner.records.Add(new { stage = "HipVerify", semanticVerified = __result, saved = __args.Last(),
                actual = capture.Invoke(__instance, new[] { __args.First() }) });
        }
        catch (Exception ex) { owner.records.Add(new { stage = "HipVerify", error = ex.GetBaseException().Message }); }
    }
    static void Compared(object[] __args, bool __result)
    {
        var owner = active;
        if (owner == null || !owner.Enabled || !((string)__args.First()).Contains("headAndHands")) return;
        owner.records.Add(new { stage = "HipSnapshotCompare", equivalent = __result, saved = __args.First(), actual = __args.Skip(1).First(),
            flags = __args.Skip(2).ToArray() });
    }
    static object Snapshot()
    {
        var hotel = Get(Plugin, "_hotel");
        var mapType = ReflectionCommands.ResolveType("MainGameBlankMapAdd.Plugin");
        var owner = Get(mapType, "_roomFeatureOwner");
        var hsession = Get(hotel, "_hSession");
        var proc = Get(hsession, "_proc");
        var nativeMap = Get(proc, "map") as Component;
        var connectedMap = Get(owner, "_hotelSourceMap") as Component;
        var notices = (IEnumerable)AccessTools.Method(ReflectionCommands.ResolveType("MainGameNotifications.NotificationApi"), "GetNotifications").Invoke(null, null);
        return new { roomReady = Get(hsession, "RoomReady"), nativeMap = nativeMap == null ? null : nativeMap.name,
            connectedMap = connectedMap == null ? null : connectedMap.name, sameMap = nativeMap != null && nativeMap == connectedMap,
            mapKey = Get(owner, "_hPlaceKey"), poseLocationsReady = Get(owner, "_hotelPoseLocationsReady"),
            restore = Get(Get(hotel, "_roomRestore"), "Status"),
            notices = notices.Cast<object>().Select(n => new { owner = Get(n, "OwnerGuid"), id = Get(n, "Id"), title = Get(n, "Title"),
                body = Get(n, "Body"), severity = Get(n, "Severity")?.ToString() }).ToArray() };
    }
    public object Execute(Dictionary<string, object> args, DevelopmentContext context)
    {
        string action = ReflectionCommands.Text(args, "action", "result");
        if (action == "arm")
        {
            Dispose(); records.Clear(); setting = Get(Plugin, "_hotelDiagnostics");
            if (!Enabled) throw new InvalidOperationException("HotelMode DiagnosticsEnabled must be enabled to arm restoration tracing.");
            active = this;
            changed = (s,e) => { if (!Enabled) Dispose(); };
            setting.GetType().GetEvent("SettingChanged").AddEventHandler(setting, changed);
            patch = new Harmony("canon.development.hotel-restore-probe");
            patch.Patch(AccessTools.Method(ReflectionCommands.ResolveType("MainGirlHipHijack.Plugin"), "VerifyHotelGadgetState"),
                postfix: new HarmonyMethod(typeof(HotelRestoreProbe), nameof(HipVerified)));
            patch.Patch(AccessTools.Method(ReflectionCommands.ResolveType("MainGameAiShoujoMode.HotelRoomGadgets"), "EquivalentGadget"),
                postfix: new HarmonyMethod(typeof(HotelRestoreProbe), nameof(Compared)));
        }
        if (action == "stop") Dispose();
        return new { armed = patch != null, records = records.ToArray(), current = Snapshot() };
    }
    public void Dispose()
    {
        patch?.UnpatchSelf(); patch = null;
        if (changed != null) setting.GetType().GetEvent("SettingChanged").RemoveEventHandler(setting, changed);
        changed = null;
        if (ReferenceEquals(active, this)) active = null;
    }
}
