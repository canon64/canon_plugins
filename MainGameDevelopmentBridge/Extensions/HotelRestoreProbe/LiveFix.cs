using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MainGameDevelopmentBridge;
using UnityEngine;

public sealed class HotelRestoreLiveFix : IDevelopmentCommand
{
    public string Name => "hotel-restore-fix";
    Harmony patch;
    static object Get(object value, string field) => value == null ? null : AccessTools.Field(value.GetType(), field)?.GetValue(value);
    static object Hotel
    {
        get
        {
            var type = ReflectionCommands.ResolveType("MainGameAiShoujoMode.Plugin");
            return Get(AccessTools.Field(type, "_instance").GetValue(null), "_hotel");
        }
    }
    static bool IsHotel => Hotel != null;
    static void BindMap(object __instance)
    {
        if (!(Get(__instance, "_hotelPlaybackSession") is bool hotel) || !hotel || Get(__instance, "_hotelSourceMap") != null) return;
        var proc = Get(Get(Hotel, "_hSession"), "_proc");
        var map = Get(proc, "map");
        if (map != null) AccessTools.Field(__instance.GetType(), "_hotelSourceMap").SetValue(__instance, map);
    }
    static void Prepare()
    {
        var type = ReflectionCommands.ResolveType("MainGameBlankMapAdd.Plugin");
        var owner = AccessTools.Field(type, "_roomFeatureOwner").GetValue(null);
        if (owner != null && Get(owner, "_hotelPlaybackSession") is bool hotel && hotel)
            AccessTools.Method(type, "BindOwnedMapContext").Invoke(owner, null);
    }
    static bool Compare(object[] __args, ref bool __result)
    {
        if (!IsHotel || !((string)__args.First()).Contains("headAndHands") || !((bool)__args.Last())) return true;
        var type = ReflectionCommands.ResolveType("MainGameAiShoujoMode.HotelGadgetComparison");
        Func<double, double, bool> close = (a,b) => new Vector3((float)a, default(float), default(float)) == new Vector3((float)b, default(float), default(float));
        __result = (bool)AccessTools.Method(type, "Equivalent").Invoke(null,
            new object[] { __args.First(), __args.Skip(1).First(), close, false, true, true });
        return false;
    }
    public object Execute(Dictionary<string, object> args, DevelopmentContext context)
    {
        if (patch == null)
        {
            patch = new Harmony("canon.development.hotel-restore-live-fix");
            var type = ReflectionCommands.ResolveType("MainGameBlankMapAdd.Plugin");
            patch.Patch(AccessTools.Method(type, "BindOwnedMapContext"), postfix: new HarmonyMethod(typeof(HotelRestoreLiveFix), nameof(BindMap)));
            patch.Patch(AccessTools.Method(type, "PrepareHotelPoseLocations"), prefix: new HarmonyMethod(typeof(HotelRestoreLiveFix), nameof(Prepare)));
            patch.Patch(AccessTools.Method(ReflectionCommands.ResolveType("MainGameAiShoujoMode.HotelRoomGadgets"), "EquivalentGadget"),
                prefix: new HarmonyMethod(typeof(HotelRestoreLiveFix), nameof(Compare)));
        }
        return new { installed = true, scope = "current process hotel borrowed-map binding and semantically verified Hip placement only" };
    }
    public void Dispose() { patch?.UnpatchSelf(); patch = null; }
}
