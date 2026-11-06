using System;
using System.Reflection;
using CanonGate;
using MainGameBlankMapAdd;
using UnityEngine;
using UnityEngine.SceneManagement;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using KKAPI.MainGame;
using KKAPI.Maker;
using GatePlugin = CanonGate.Plugin;

internal static class Program
{
    private static int _checks;
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        ++_checks;
        Console.WriteLine("PASS " + name);
    }
    private static void Throws<T>(Action action, string name) where T : Exception
    {
        try { action(); } catch (T) { Check(true, name); return; }
        throw new Exception("Expected " + typeof(T).Name + ": " + name);
    }
    private static void Call(GatePlugin plugin, string name) =>
        typeof(GatePlugin).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Invoke(plugin, name == "HSceneDestroyed" ? new object[] { null } : null);
    private sealed class Client
    {
        internal int Starts, Stops;
        internal readonly string Guid;
        internal readonly IDisposable Registration;
        internal Client(string guid)
        {
            Guid = guid;
            Registration = GateApi.Register(guid, () => ++Starts, () => ++Stops);
        }
    }

    private static void Main()
    {
        Console.WriteLine("canon_gate once-per-session tests");
        var normal = new Client("normal.plugin");
        var maker = new Client("maker.plugin");
        var everywhere = new Client("utility.plugin");
        Check(!GateApi.CanRun(normal.Guid) && !GateApi.Capture().IsValid, "registration before gate startup stays inactive");
        Throws<InvalidOperationException>(() => GateApi.Register(normal.Guid, () => { }, () => { }), "duplicate GUID rejected");
        Throws<ArgumentException>(() => GateApi.Register(" ", () => { }, () => { }), "empty GUID rejected");
        Throws<ArgumentNullException>(() => GateApi.Register("null.callback", null, () => { }), "null start rejected");
        var gate = new GatePlugin();
        Call(gate, "Awake");
        Check(typeof(GatePlugin).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance) == null
            && typeof(GatePlugin).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance) == null
            && typeof(GatePlugin).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance) == null,
            "no Unity frame callbacks exist");
        Check(BlankRoomState.Reads == 0 && MakerAPI.Reads == 0 && UnityEngine.Object.Searches == 0, "startup does not inspect scenes or room state");
        var makerSetting = (ConfigEntry<string>)gate.Config.Entries["MakerAllowedPluginGuids"];
        var everywhereSetting = (ConfigEntry<string>)gate.Config.Entries["EverywhereAllowedPluginGuids"];
        Check(makerSetting.Value == "" && everywhereSetting.Value == "", "default exception lists empty");
        Check(!GateApi.CanRun("unknown") && !GateApi.CanRun(null), "query does not register unknown GUID");
        using (GateApi.Register("unknown", () => { }, () => { })) { }
        GameAPI.Begin();
        Check(!GateApi.IsAllowed && normal.Starts == 0 && BlankRoomState.Reads == 0, "missing provider denies H without retries or client startup");
        GameAPI.End();
        makerSetting.Value = " maker.plugin ,maker.plugin; another.maker";
        everywhereSetting.Value = "utility.plugin";
        var provider = new MainGameBlankMapAdd.Plugin();
        Chainloader.PluginInfos.Add("com.kks.maingameblankmapadd", new BepInEx.PluginInfo { Instance = provider });
        BlankRoomState.Inside = false;
        GameAPI.Begin();
        Check(BlankRoomState.Reads == 1, "normal H entry reads room exactly once");
        Check(normal.Starts == 0 && maker.Starts == 0 && everywhere.Starts == 1, "normal H starts only explicit everywhere exception");
        var oldUtilityPermit = GateApi.Capture(everywhere.Guid);
        Check(oldUtilityPermit.IsValid && !GateApi.IsAllowed, "individual exception does not open strict room result");
        GameAPI.Begin();
        Check(BlankRoomState.Reads == 1 && everywhere.Starts == 1, "duplicate start does not inspect or restart");
        var late = new Client("late.plugin");
        everywhereSetting.Value = "utility.plugin,normal.plugin,late.plugin";
        GateApi.ReloadExceptions();
        BlankRoomState.Set(true);
        SceneManager.Load(new Scene { handle = 1 });
        SceneManager.Unload(new Scene { handle = 1 });
        Check(BlankRoomState.Reads == 1 && !GateApi.CanRun(normal.Guid) && !GateApi.CanRun(late.Guid), "map, scene, config, reload, late registration do not rejudge active H");
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000000; ++i)
        {
            Time.frameCount++;
            if (!GateApi.CanRun(everywhere.Guid) || GateApi.IsAllowed || !oldUtilityPermit.IsValid)
                throw new Exception("cached decision changed");
        }
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Check(BlankRoomState.Reads == 1 && MakerAPI.Reads == 0 && UnityEngine.Object.Searches == 0 && allocated == 0,
            "1,000,000 successive frames: no inspections, scene searches, or query allocations");
        GameAPI.End();
        Check(BlankRoomState.Reads == 1 && everywhere.Stops == 1 && normal.Stops == 0, "end stops only started clients without inspecting room");
        Check(!oldUtilityPermit.IsValid && !GateApi.CanRun(everywhere.Guid), "end revokes cached results and permits");
        GameAPI.End();
        Call(gate, "HSceneDestroyed");
        Check(everywhere.Stops == 1, "duplicate end and destruction stop once");
        BlankRoomState.Inside = false;
        GameAPI.Begin();
        Check(BlankRoomState.Reads == 2 && normal.Starts == 1 && late.Starts == 1, "next H applies staged config and late registration");
        Check(!oldUtilityPermit.IsValid, "new H cannot revive old permit");
        var disposedPermit = GateApi.Capture(late.Guid);
        late.Registration.Dispose();
        late.Registration.Dispose();
        Check(late.Stops == 1 && !disposedPermit.IsValid && !GateApi.CanRun(late.Guid), "registration disposal stops once and invalidates permit");
        GameAPI.End();
        everywhereSetting.Value = "utility.plugin";
        BlankRoomState.Inside = true;
        GameAPI.Begin();
        Check(BlankRoomState.Reads == 3 && GateApi.IsAllowed && GateApi.CanRun(normal.Guid), "AllPoseRoom H reads once and permits default client");
        var strictPermit = GateApi.Capture();
        Check(strictPermit.IsValid, "strict permit valid during allowed H");
        BlankRoomState.Set(false);
        provider.isActiveAndEnabled = false;
        everywhereSetting.Value = "";
        GameAPI.Begin();
        Check(BlankRoomState.Reads == 3 && GateApi.IsAllowed && strictPermit.IsValid, "H keeps initial decision after map/config/provider changes");
        GameAPI.End();
        Check(!strictPermit.IsValid, "strict permit revoked at end");
        GameAPI.Begin();
        Check(!GateApi.CanRun(normal.Guid) && BlankRoomState.Reads == 3, "disabled provider denies H without reading stale state");
        GameAPI.End();
        provider.isActiveAndEnabled = true;
        BlankRoomState.ThrowOnRead = true;
        GameAPI.Begin();
        Check(BlankRoomState.Reads == 4 && !GateApi.CanRun(normal.Guid), "failed room read denies session");
        BlankRoomState.ThrowOnRead = false;
        BlankRoomState.Set(true);
        GameAPI.Begin();
        Check(BlankRoomState.Reads == 4 && !GateApi.CanRun(normal.Guid), "failed session never retries");
        Call(gate, "HSceneDestroyed");
        GameAPI.End();
        Check(BlankRoomState.Reads == 4, "destruction fallback does not inspect");
        int makerStarts = maker.Starts;
        MakerAPI.Enter();
        Check(maker.Starts == makerStarts + 1 && GateApi.CanRun(maker.Guid) && !GateApi.CanRun(normal.Guid), "maker entry applies only maker exception");
        var makerPermit = GateApi.Capture(maker.Guid);
        MakerAPI.Enter();
        Check(maker.Starts == makerStarts + 1 && BlankRoomState.Reads == 4 && MakerAPI.Reads == 0, "duplicate maker start performs no inspection or restart");
        makerSetting.Value = "";
        Check(GateApi.CanRun(maker.Guid), "maker config changes deferred until next maker");
        MakerAPI.Exit();
        Check(!makerPermit.IsValid && !GateApi.CanRun(maker.Guid), "maker exit stops and revokes");
        MakerAPI.Enter();
        Check(!GateApi.CanRun(maker.Guid) && maker.Starts == makerStarts + 1, "next maker applies removed exception");
        MakerAPI.Exit();
        everywhereSetting.Value = "maker.plugin";
        MakerAPI.Enter();
        Check(GateApi.CanRun(maker.Guid), "everywhere exception also permits maker context");
        MakerAPI.Exit();
        everywhereSetting.Value = "";
        int partialStops = 0, siblingStarts = 0;
        using (GateApi.Register("broken.start", () => { throw new Exception("partial startup"); }, () => ++partialStops))
        using (GateApi.Register("healthy.start", () => ++siblingStarts, () => { }))
        {
            GameAPI.Begin();
            Check(partialStops == 1 && siblingStarts == 1 && !GateApi.CanRun("broken.start"), "start failure cleans up partial work and does not block another client");
            GameAPI.End();
            Check(partialStops == 1, "failed start cleanup not repeated at session end");
        }
        int brokenStops = 0, healthyStops = 0;
        using (GateApi.Register("broken.stop", () => { }, () => { ++brokenStops; throw new Exception("cleanup failure"); }))
        using (GateApi.Register("healthy.stop", () => { }, () => ++healthyStops))
        {
            GameAPI.Begin();
            var disabledPermit = GateApi.Capture(normal.Guid);
            gate.isActiveAndEnabled = false;
            Call(gate, "OnDisable");
            Check(brokenStops == 1 && healthyStops == 1, "cleanup failure cannot block another client's stop");
            Check(!disabledPermit.IsValid && !GateApi.CanRun(normal.Guid), "gate disable revokes session");
            int readsBefore = BlankRoomState.Reads;
            int startsBefore = normal.Starts;
            gate.isActiveAndEnabled = true;
            Call(gate, "OnEnable");
            GameAPI.Begin();
            Check(BlankRoomState.Reads == readsBefore && normal.Starts == startsBefore && !GateApi.CanRun(normal.Guid), "reenable waits for next H; duplicate start cannot reopen current H");
            GameAPI.End();
        }
        GameAPI.Begin();
        Check(GateApi.CanRun(normal.Guid), "next real H works after reenable");
        var destroyedPermit = GateApi.Capture(normal.Guid);
        Call(gate, "OnDestroy");
        int finalReads = BlankRoomState.Reads;
        GameAPI.End(); GameAPI.Begin(); MakerAPI.Enter();
        Check(!destroyedPermit.IsValid && !GateApi.CanRun(normal.Guid) && BlankRoomState.Reads == finalReads,
            "destroyed gate unsubscribes and stays closed");
        normal.Registration.Dispose(); maker.Registration.Dispose(); everywhere.Registration.Dispose();
        Console.WriteLine($"{_checks} checks passed (stubbed lifecycle; live game not tested).");
        WarningTests.Run();
        ActivityTests.Run(Check);
        AiModeTests.Run(Check);
        HotelModeTests.Run(Check);
    }
}
