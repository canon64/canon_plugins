using System;
using System.Collections.Generic;
using System.Reflection;
using ActionGame;
using HarmonyLib;
using MainGameAiShoujoMode;
using MainGameDevelopmentBridge;
using UnityEngine;

// Explicit diagnostic-only bridge hotfix; no callbacks or polling outside H boundaries.
public sealed class HotelEntryMapFix : IDevelopmentCommand
{
    public string Name => "hotel-entry-map-fix";
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type Session => ReflectionCommands.ResolveType("MainGameAiShoujoMode.AiModeHSession");
    static object owner;
    static HSessionBorrowedMapScope scope;
    Harmony patches;
    static object Field(object value, string name) => (value as Type ?? value.GetType()).GetField(name, Flags).GetValue(value is Type ? null : value);
    static void Set(object value, string name, object data) => value.GetType().GetField(name, Flags).SetValue(value, data);
    static void Before(HSceneProc __instance)
    {
        var active = Field(Session, "_active");
        if (active == null || !(bool)Session.GetProperty("Busy").GetValue(active, null)) return;
        var proc = Field(active, "_proc") as HSceneProc;
        if (proc != null && proc != __instance) return;
        if (Field(active, "_map") != null) return;
        var host = Field(active, "_host");
        if (host == null || host.GetType().Name != "HotelModeSession") return;
        var walking = Field(host, "_nativeWalking");
        var action = walking?.GetType().GetProperty("Action", Flags)?.GetValue(walking, null) as ActionScene;
        if (action == null) return;
        if (scope != null) throw new InvalidOperationException("Previous borrowed H map has not been released.");
        var p = Field(active, "_p");
        var number = (int)p.GetType().GetMethod("I").Invoke(p, new object[] { "hSession.mapNo" });
        scope = new HSessionBorrowedMapScope(action.Map, (GameObject)Field(active, "_island"), number);
        owner = active;
        Set(active, "_map", action.Map);
    }
    static void Finish(object __instance)
    {
        if (!ReferenceEquals(owner, __instance) || scope == null) return;
        scope.Dispose(); scope = null; owner = null;
        Set(__instance, "_map", null);
    }
    public object Execute(Dictionary<string, object> arguments, DevelopmentContext context)
    {
        if (patches != null) return new { installed = true };
        patches = new Harmony("canon.development.hotel-entry-borrowed-map");
        patches.Patch(AccessTools.Method(Session, "BeforeHStart"), prefix: new HarmonyMethod(typeof(HotelEntryMapFix), nameof(Before)));
        patches.Patch(AccessTools.Method(Session, "Finish"), prefix: new HarmonyMethod(typeof(HotelEntryMapFix), nameof(Finish)));
        return new { installed = true, persistentPluginReplaced = false };
    }
    public void Dispose()
    {
        if (scope != null) throw new InvalidOperationException("Do not unload while the diagnostic H map is borrowed.");
        patches?.UnpatchSelf(); patches = null;
    }
}
