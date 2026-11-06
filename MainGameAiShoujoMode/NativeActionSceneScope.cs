using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ActionGame;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MainGameAiShoujoMode
{
    // Own the real built-in Action scene. Native Player/UI/camera initialization is executed unchanged.
    // Mode-specific world, map and walking bindings must be ready before this scope is exposed to play.
    internal sealed class NativeActionSceneScope : IDisposable
    {
        private static NativeActionSceneScope _active;
        private readonly Harmony _patches;
        private readonly Action<ActionScene> _ready;
        private readonly Action<Exception> _failed;
        private AsyncOperation _load;
        private Scene _scene;
        private ActionScene _action;
        private bool _disposed, _notified, _begun, _unloading;
        internal ActionScene Action => _action;
        internal bool Released { get; private set; }
        internal event System.Action ReleaseCompleted;

        internal NativeActionSceneScope(Action<ActionScene> ready, Action<Exception> failed)
        {
            if (_active != null) throw new InvalidOperationException("A native Action scene is already owned.");
            if (!NativeActionWorldScope.Owns(Manager.Game.saveData)) throw new InvalidOperationException("Native Action requires an owned temporary world.");
            if (SceneManager.GetSceneByName("Action").IsValid()) throw new InvalidOperationException("The existing story scene cannot be adopted by this mode.");
            _ready = ready ?? throw new ArgumentNullException(nameof(ready));
            _failed = failed ?? throw new ArgumentNullException(nameof(failed));
            _patches = new Harmony("canon.MainGameAiShoujoMode.NativeActionScene");
            _active = this;
            try
            {
                _patches.Patch(AccessTools.Method(typeof(ActionScene), "Start"),
                    postfix: new HarmonyMethod(typeof(NativeActionSceneScope), nameof(AfterNativeStart)));
                _patches.Patch(AccessTools.Method(typeof(Cycle), "Change", new[] { typeof(Cycle.Type) }),
                    prefix: new HarmonyMethod(typeof(NativeActionSceneScope), nameof(BeforeCycleChange)));
                _patches.Patch(AccessTools.Method(typeof(ActionMap), "Reserve"),
                    prefix: new HarmonyMethod(typeof(NativeActionSceneScope), nameof(BeforeMapReserve)));
            }
            catch { _active = null; _patches.UnpatchSelf(); throw; }
        }
        internal void Begin()
        {
            if (_disposed || _begun) throw new InvalidOperationException("Native Action load is not available.");
            if (!NativeActionWorldScope.Owns(Manager.Game.saveData)) throw new InvalidOperationException("The temporary walking world is no longer owned.");
            _begun = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            try
            {
                _load = SceneManager.LoadSceneAsync("Action", LoadSceneMode.Additive);
                if (_load == null) throw new InvalidOperationException("The native Action scene did not start loading.");
                _load.completed += ignored => { if (_disposed) ReleaseScene(); };
            }
            catch { Dispose(); throw; }
        }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Action") return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _scene = scene;
            if (_disposed) { ReleaseScene(); return; }
            try
            {
                _action = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ActionScene>(true)).Single();
            }
            catch (Exception ex) { Fail(ex); }
        }
        private static void AfterNativeStart(ActionScene __instance, ref IEnumerator __result)
        {
            var owner = _active;
            if (owner != null && ReferenceEquals(owner._action, __instance))
                __result = owner.ObserveStart(__result);
        }
        private IEnumerator ObserveStart(IEnumerator native)
        {
            var pending = new Stack<IEnumerator>(); pending.Push(native);
            Exception failure = null;
            try
            {
                while (!_disposed && pending.Count > default(int))
                {
                    object step = null;
                    try
                    {
                        var iterator = pending.Peek();
                        if (!iterator.MoveNext()) { (pending.Pop() as IDisposable)?.Dispose(); continue; }
                        step = iterator.Current;
                        if (step is IEnumerator nested) { pending.Push(nested); continue; }
                    }
                    catch (Exception ex) { failure = ex; }
                    if (failure != null) break;
                    yield return step;
                }
            }
            finally { while (pending.Count > default(int)) (pending.Pop() as IDisposable)?.Dispose(); }
            if (_disposed) yield break;
            if (failure != null) { Fail(failure); yield break; }
            if (_action == null || _action.Player == null || !_action.Player.initialized)
            { Fail(new InvalidOperationException("Native player initialization did not complete.")); yield break; }
            if (_notified) yield break;
            _notified = true;
            try { _ready(_action); }
            catch (Exception ex) { Dispose(); _failed(ex); }
        }
        private static bool BeforeMapReserve(ActionMap __instance)
        {
            var owner = _active;
            return owner == null || owner._disposed || owner._action == null ||
                __instance != owner._action.Map || !NativeActionWorldScope.Owns(Manager.Game.saveData);
        }
        private static bool BeforeCycleChange(Cycle __instance)
        {
            var owner = _active;
            // Keep the native walking clock state initialized from the temporary WorldData;
            // do not start story transitions, tutorials, native NPC scheduling or autosaves.
            return owner == null || owner._disposed || owner._action == null ||
                __instance != owner._action.Cycle || !NativeActionWorldScope.Owns(Manager.Game.saveData);
        }
        private void Fail(Exception exception)
        {
            if (_disposed || _notified) return;
            _notified = true;
            Dispose();
            _failed(exception);
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (ReferenceEquals(_active, this)) _active = null;
            _action?.Cycle?.CancelMapMove();
            _patches.UnpatchSelf();
            if (_load != null && !_load.isDone) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            ReleaseScene();
        }
        private void ReleaseScene()
        {
            if (Released || _unloading) return;
            if (_scene.IsValid() && _scene.isLoaded)
            {
                Scene scene = _scene; _scene = default(Scene);
                _unloading = true;
                var unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null) { unload.completed += ignored => MarkReleased(); return; }
            }
            MarkReleased();
        }
        private void MarkReleased()
        {
            if (Released) return;
            Released = true;
            var completed = ReleaseCompleted; ReleaseCompleted = null;
            completed?.Invoke();
        }
    }
}
