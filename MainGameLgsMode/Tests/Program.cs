using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using MainGameLgsMode;
using UnityEngine;
using UnityEngine.SceneManagement;
using BepInEx.Bootstrap;
using KKAPI.MainGame;

internal static class Program
{
    private static int _checks;
    private static void Check(bool condition, string description)
    { if (!condition) throw new Exception(description); ++_checks; }
    private sealed class Sink : ICommandMailbox
    {
        public readonly List<int> Modes = new List<int>();
        public int Last => Modes.Last();
        public void Write(int mode, int lifetimeSeconds) { Modes.Add(mode); }
    }
    private static Event Key(EventType type, KeyCode key = KeyCode.M, EventModifiers modifiers = 0)
        => new Event { type = type, keyCode = key, modifiers = modifiers };
    private static void Press(ManualKeyInput input, KeyCode key = KeyCode.M, EventModifiers modifiers = 0)
    { input.Handle(Key(EventType.KeyDown, key, modifiers)); input.Handle(Key(EventType.KeyUp, key)); }
    private static int Main()
    {
        try { Controller(); Keyboard(); Events(); MailboxAndSettings(); Console.WriteLine("PASS: " + _checks + " checks (real Harmony hooks, simulated game events)"); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static void Controller()
    {
        var sink = new Sink(); int logs = 0;
        var settings = new ModeSettings();
        using (var controller = new ModeController(settings, sink, _ => ++logs, _ => ++logs))
        {
            Check(sink.Last == 0, "startup cancels stale requests without sending a mode");
            controller.Enter(GameSituation.Walking); Check(sink.Last == 2, "walking sends M2");
            int count = sink.Modes.Count; controller.Enter(GameSituation.Walking);
            Check(count == sink.Modes.Count, "duplicate walking callbacks do not override manual changes");
            controller.CycleManual(); Check(sink.Last == 3, "M cycles from requested M2 to M3");
            controller.CycleManual(); Check(sink.Last == 1, "manual cycle wraps to M1");
            controller.Enter(GameSituation.FreeH); Check(sink.Last == 1, "new Free H restores auto M1");
            controller.CycleManual(); Check(sink.Last == 2, "manual overrides Free H until next transition");
            controller.Configure(new ModeSettings { ManualCycleKey = "F8" }); Check(sink.Last == 2, "rebinding preserves manual selection");
            controller.Enter(GameSituation.Walking); Check(sink.Last == 2, "walking return restores automatic M2");
            controller.Enter(GameSituation.Other); Check(sink.Last == 0, "other screens hold the physical bank");
            controller.Report(new IOException()); Check(logs == 0, "logs OFF suppresses all diagnostic paths");
            controller.Configure(new ModeSettings { EnableLogs = true }); controller.CycleManual();
            Check(logs > 0, "logs can be enabled without rebuild");
            controller.Configure(new ModeSettings { Enabled = false }); count = sink.Modes.Count;
            controller.CycleManual(); Check(sink.Last == 0 && sink.Modes.Count == count, "disabled bridge sends no manual command");
        }
        Check(sink.Last == 0, "disposal cancels unread command without changing physical bank");
    }
    private static void Keyboard()
    {
        var settings = new ModeSettings(); var sink = new Sink();
        using (var controller = new ModeController(settings, sink))
        {
            var input = new ManualKeyInput(() => settings, controller);
            input.Handle(Key(EventType.KeyDown)); Check(sink.Last == 1, "default M sends M1 on first press");
            int count = sink.Modes.Count; input.Handle(Key(EventType.KeyDown));
            Check(count == sink.Modes.Count, "held key does not repeat");
            input.Handle(Key(EventType.KeyUp)); Press(input); Check(sink.Last == 2, "new physical press advances one bank");
            GUIUtility.keyboardControl = 4; count = sink.Modes.Count; Press(input); GUIUtility.keyboardControl = 0;
            Check(sink.Modes.Count == count, "IMGUI text focus suppresses cycle");
            var selected = new GameObject(); selected.Components[typeof(TMPro.TMP_InputField)] = new TMPro.TMP_InputField();
            UnityEngine.EventSystems.EventSystem.current = new UnityEngine.EventSystems.EventSystem { currentSelectedGameObject = selected };
            Press(input); Check(sink.Modes.Count == count, "TMP text input suppresses cycle");
            selected.Components.Clear(); selected.Components[typeof(UnityEngine.UI.InputField)] = new UnityEngine.UI.InputField();
            Press(input); Check(sink.Modes.Count == count, "UGUI text input suppresses cycle");
            UnityEngine.EventSystems.EventSystem.current = null;
            settings = new ModeSettings { ManualCycleKey = "Ctrl+F8" }; controller.Configure(settings); count = sink.Modes.Count;
            Press(input); Press(input, KeyCode.F8); Check(sink.Modes.Count == count, "old key and unmodified replacement no longer match");
            Press(input, KeyCode.F8, EventModifiers.Control); Check(sink.Last == 3, "editable Ctrl+F8 binding works");
            count = sink.Modes.Count; Press(input, KeyCode.F8, EventModifiers.Control | EventModifiers.Alt);
            Check(sink.Modes.Count == count, "extra modifiers do not trigger");
            Application.isFocused = false; Press(input, KeyCode.F8, EventModifiers.Control); Application.isFocused = true;
            Check(sink.Modes.Count == count, "background game ignores key");
            settings = new ModeSettings { ManualCycleEnabled = false }; count = sink.Modes.Count; Press(input);
            Check(sink.Modes.Count == count, "manual switching can be disabled independently");
            Check(ManualHotkey.Matches("M", KeyCode.M, EventModifiers.CapsLock), "CapsLock does not break M binding");
            bool rejected = false; try { new ModeSettings { ManualCycleKey = "not-a-key" }.Validate(); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "invalid key is rejected before active configuration changes");
        }
    }
    private static void Drain(IEnumerator iterator)
    { while (iterator.MoveNext()) if (iterator.Current is IEnumerator nested) Drain(nested); }
    private static void Events()
    {
        const string aiGuid = "com.kks.maingame.aishoujomode";
        var plugin = new MainGameAiShoujoMode.Plugin();
        Chainloader.PluginInfos[aiGuid] = new PluginInfo { Instance = plugin };
        var sink = new Sink();
        using (var controller = new ModeController(new ModeSettings(), sink))
        using (var events = new GameEvents(controller))
        {
            events.Connect();
            var action = new ActionScene(); action.gameObject.scene = new Scene { handle = 1 };
            Drain(action.Start()); Check(sink.Last == 2, "real Action.Start coroutine hook sends walking M2 after completion");
            var h = new HSceneProc(); h.flags.isFreeH = true; h.Start(); GameAPI.Begin();
            Check(sink.Last == 1, "real H.Start hook and API event recognize Free H");
            action.Player.SetActive(true); Check(sink.Last == 1, "native player activation during H cannot force walking");
            GameAPI.End(); Check(sink.Last == 2, "normal game H end returns to walking M2");
            plugin._hotel = new MainGameAiShoujoMode.HotelModeSession();
            h.Start(); GameAPI.Begin(); var context = new MainGameAiShoujoMode.HotelWalkingContext();
            context.WalkingReady = true; GameAPI.End();
            Check(sink.Last == 2, "hotel walking-before-EndH ordering retains M2");
            h.Start(); GameAPI.Begin(); GameAPI.End(); context.WalkingReady = true;
            Check(sink.Last == 2, "hotel walking-after-EndH ordering sends M2");
            var native = new MainGameAiShoujoMode.NativeActionWalkingSession();
            controller.Enter(GameSituation.Other); native.Ready = true; Check(sink.Last == 2, "owned native walking readiness sends M2");
            h.Start(); GameAPI.Begin(); GameAPI.End(); native.Resume(); Check(sink.Last == 2, "owned native walking resume sends M2");
            plugin._hotel = null; var owner = new MainGameAiShoujoMode.AiModeSession(); plugin._mode = owner;
            var scope = new CanonGate.GateApi.AiShoujoModeScope { Owner = owner }; CanonGate.GateApi.AiShoujoMode = scope;
            controller.Enter(GameSituation.Other); scope.Resume(); Check(sink.Last == 2, "non-VR AI walking resume recognized");
            owner._host.Session.Apply(new MapEnvironmentRuntime.Parameters { Mode = "free" }, false);
            Check(sink.Last == 0, "AI flying camera holds bank and clears walking situation");
            owner._host.Session.Apply(new MapEnvironmentRuntime.Parameters { Mode = "walk" }, false);
            Check(sink.Last == 2, "switching AI camera back to walking sends M2");
            var unrelated = new MapEnvironmentRuntime.SandboxSession(); controller.CycleManual();
            unrelated.Apply(new MapEnvironmentRuntime.Parameters(), false); Check(sink.Last == 3, "unrelated sandbox cannot override manual mode");
            h.Start(); GameAPI.Begin(); scope.Resume(); GameAPI.End(); Check(sink.Last == 2, "AI resume before EndH retains M2");
            var vr = new VRHScene(); vr.flags.isFreeH = true; vr.Start(); GameAPI.Begin(); Check(sink.Last == 1, "VR H scene captured through real hook");
            GameAPI.End(); plugin._mode = null; CanonGate.GateApi.AiShoujoMode = null;
            scope.Dispose(); Check(sink.Last == 0, "AI mode exit resets situation for the next walking entry");
            h.flags.isFreeH = false; h.Start(); GameAPI.Begin(); Check(sink.Last == 0, "story H does not receive Free H M1");
            GameAPI.End(); SceneManager.Unload(action.gameObject.scene); Check(sink.Last == 0, "leaving native walking cancels pending mode request");
        }
        int after = sink.Modes.Count; GameAPI.Begin(); GameAPI.End(); contextAfterDispose();
        Check(sink.Modes.Count == after, "unsubscribe and unpatch stop all callbacks");
        Chainloader.PluginInfos.Clear();
    }
    private static void contextAfterDispose() { new MainGameAiShoujoMode.HotelWalkingContext().WalkingReady = true; }
    private static void MailboxAndSettings()
    {
        string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "results", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        var mailbox = new CommandMailbox(root); mailbox.Write(2, 15);
        string first = File.ReadAllText(mailbox.CommandPath);
        Check(Regex.IsMatch(first, "^return \\{version=1,token=\"[0-9a-f]{32}\",mode=2,expires=\"[0-9]{14}\"\\}\\n$"), "mailbox is a complete Lua return record");
        mailbox.Write(1, 15); Check(File.ReadAllText(mailbox.CommandPath) != first, "atomic replacement publishes new command and identity");
        Check(!Directory.GetFiles(Path.GetDirectoryName(mailbox.CommandPath), "*.tmp").Any(), "successful publication leaves no temporary file");
        using (var store = new SettingsStore(root))
        {
            Check(store.Current.ManualCycleKey == "M" && store.Current.FreeHMode == 1 && store.Current.WalkingMode == 2, "first-run JSON defaults match request");
            using (var changed = new AutoResetEvent(false))
            {
                store.Changed += _ => changed.Set();
                File.WriteAllText(Path.Combine(root, "settings.json"), "{\"ManualCycleKey\":\"F9\"}");
                Check(changed.WaitOne(3000) && store.Current.ManualCycleKey == "F9" && store.Current.WalkingMode == 2, "JSON hot reload changes key while preserving missing-field defaults");
                while (changed.WaitOne(0)) { }
                using (var failed = new AutoResetEvent(false))
                {
                    store.Error += _ => failed.Set();
                    File.WriteAllText(Path.Combine(root, "settings.json"), "{\"ManualCycleKey\":\"bogus\"}");
                    Check(failed.WaitOne(3000) && store.Current.ManualCycleKey == "F9", "invalid JSON settings retain last valid binding");
                }
            }
        }
    }
}
