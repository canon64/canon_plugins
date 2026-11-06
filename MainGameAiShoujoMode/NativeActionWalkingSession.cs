using System;
using System.Collections;
using System.Collections.Generic;
using ActionGame;
using MapEnvironmentRuntime;
using SaveData;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    // One owner for temporary world, actual Action scene, imported walking floor and VRMOD context.
    internal sealed class NativeActionWalkingSession : IDisposable
    {
        private NativeActionWorldScope _world;
        private NativeActionSceneScope _scene;
        private NativeActionVrSceneScope _vr;
        private NativeActionMapScope _map;
        private NativeActionWalkingDriver _driver;
        private NativeActionUiScope _ui;
        internal IEnumerable<Transform> DoorExclusions { get; set; }
        private bool _disposed;
        internal ActionScene Action { get; private set; }
        internal Exception Error { get; private set; }
        internal bool PlayerReady => !_disposed && Action != null;
        internal bool Ready { get; private set; }
        internal bool Released => _world == null && (_scene == null || _scene.Released);
        internal SandboxFreeCamera CreateWalker(IWalkingSession session, Camera renderCamera, Transform origin)
        {
            if (!PlayerReady || _driver != null) throw new InvalidOperationException("Native walking adapter is not available.");
            _driver = new NativeActionWalkingDriver(this, renderCamera, origin, session.Settings);
            var walker = Action.Player.gameObject.AddComponent<SandboxFreeCamera>();
            try { walker.InitializeNative(session, _driver); return walker; }
            catch { UnityEngine.Object.Destroy(walker); _driver.Dispose(); throw; }
        }
        internal IEnumerator AttachMap(GameObject root, Parameters settings)
        {
            if (_driver == null) throw new InvalidOperationException("Native walking adapter has not been created.");
            yield return AttachMap(root, settings, _driver.Arrival, _driver.Heading);
        }
        internal void AlignView() => _vr?.AlignToPlayer();
        internal void SetWalkingMotion(bool moving) => _vr?.SetWalkingMotion(moving);
        internal IEnumerator ReplaceMap(GameObject root, Parameters settings, Vector3 feet, Quaternion rotation)
        {
            if (_disposed || !Ready) throw new InvalidOperationException("The native walking map cannot be replaced now.");
            Suspend();
            Ready = false;
            _map?.Dispose(); _map = null;
            yield return AttachMap(root, settings, feet, rotation);
        }
        internal void BeginTraversal() => _vr?.Suspend();
        internal void EndTraversal()
        {
            if (_disposed || !Ready) return;
            _vr.Resume(); _driver?.MapReady(); AlignView();
        }

        internal NativeActionWalkingSession(Parameters settings, ChaFileControl savedPlayer = null)
        {
            try
            {
                var card = savedPlayer ?? Manager.Game.saveData?.player?.charFile;
                string path = settings.T("player.card.path");
                if (savedPlayer == null && !string.IsNullOrWhiteSpace(path))
                {
                    card = new ChaFileControl();
                    if (!card.LoadCharaFile(path)) throw new InvalidOperationException("主人公カードを読み込めません。");
                }
                if (card == null) throw new InvalidOperationException("本編歩行に使用する主人公カードがありません。");
                if (card.parameter.sex == settings.I("math.one"))
                {
                    // Native Action locomotion owns its existing hidden male controller; the mode avatar retains the selected female card.
                    var backing = Manager.Game.saveData?.player?.charFile;
                    if (backing == null || backing.parameter.sex != settings.I("math.zero"))
                    {
                        var defaults = new WorldData(); defaults.player.Randomize(); backing = defaults.player.charFile;
                    }
                    card = backing;
                }
                if (card.parameter.sex != settings.I("math.zero")) throw new InvalidOperationException("本編歩行の内部コントローラーに使用できる主人公カードがありません。");
                var temporary = new WorldData
                {
                    player = new SaveData.Player(HotelRoomAppearanceRuntime.CloneCard(card), false),
                    cycle = (int)Cycle.Type.Daytime,
                    isOpening = false
                };
                _world = new NativeActionWorldScope(temporary);
                _scene = new NativeActionSceneScope(NativeReady, NativeFailed);
                _scene.ReleaseCompleted += RestoreWorld;
                _scene.Begin();
            }
            catch { Dispose(); throw; }
        }
        private void NativeReady(ActionScene action)
        {
            if (_disposed) return;
            try
            {
                _vr = new NativeActionVrSceneScope(action);
                _ui = new NativeActionUiScope(action);
                Action = action;
            }
            catch (Exception exception) { NativeFailed(exception); }
        }
        private void NativeFailed(Exception exception)
        {
            if (_disposed) return;
            Error = exception;
            Dispose();
        }
        internal IEnumerator AttachMap(GameObject root, Parameters settings, Vector3 feet, Quaternion rotation)
        {
            if (!PlayerReady || Ready || _map != null) throw new InvalidOperationException("The native walking player is not ready for a map.");
            _map = new NativeActionMapScope(Action);
            yield return _map.Attach(root, settings, feet, rotation, DoorExclusions);
            if (_disposed) yield break;
            _vr.Resume();
            Ready = true;
            _driver?.MapReady();
            AlignView();
        }
        internal void Suspend()
        {
            if (_disposed || !Ready) return;
            _vr.Suspend();
            Action.Player.SetActive(false);
        }
        internal void Resume()
        {
            if (_disposed || !Ready) return;
            Action.Player.SetActive(true);
            _vr.Resume();
            _driver?.MapReady();
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; Ready = false;
            try { _vr?.Dispose(); }
            finally
            {
                _vr = null;
                _ui?.Dispose(); _ui = null;
                _driver?.Dispose(); _driver = null;
                try { _map?.Dispose(); }
                finally
                {
                    _map = null; Action = null;
                    if (_scene != null) _scene.Dispose();
                    if (_scene == null || _scene.Released) RestoreWorld();
                }
            }
        }
        private void RestoreWorld()
        {
            var world = _world; _world = null;
            world?.Dispose();
        }
    }
}
