using System;
using System.Collections.Generic;
using MapEnvironmentRuntime;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace MainGameAiShoujoMode
{
    internal sealed class NativeActionWalkingDriver : INativeWalkingDriver
    {
        private readonly NativeActionWalkingSession _owner;
        private readonly ActionScene _action;
        private readonly Dictionary<Renderer, bool> _body = new Dictionary<Renderer, bool>();
        private readonly WalkingStatus _status = new WalkingStatus();
        private Parameters _settings;
        private readonly Transform _origin;
        private Action _restoreTraversal;
        private IDisposable _movementSubscription;
        private bool _blocked = true, _disposed, _moving;
        internal Vector3 Arrival { get; private set; }
        internal Quaternion Heading { get; private set; } = Quaternion.identity;
        public Camera ViewCamera { get; }
        public bool Crouching => _action.Player.move.agentSpeeder.mode.ToString() == "Crouch";
        public Vector3 FeetLocalOffset => Vector3.zero;

        internal NativeActionWalkingDriver(NativeActionWalkingSession owner, Camera view, Transform origin, Parameters settings)
        {
            _owner = owner; _action = owner.Action; ViewCamera = view;
            _origin = origin;
            if (view == null || _action?.Player?.chaCtrl == null) throw new InvalidOperationException("The native walking player or VR view is unavailable.");
            _settings = settings;
            foreach (var renderer in _action.Player.chaCtrl.GetComponentsInChildren<Renderer>(true)) _body[renderer] = renderer.enabled;
            HideNativeBody();
            ApplyInput();
            _movementSubscription = _action.Player.move.animParamSpeedEvent.Subscribe(new MovementObserver(this));
        }
        private sealed class MovementObserver : IObserver<float>
        {
            private readonly NativeActionWalkingDriver _driver;
            internal MovementObserver(NativeActionWalkingDriver driver) { _driver = driver; }
            public void OnNext(float speed)
            {
                if (_driver._disposed) return;
                _driver._moving = speed > _driver._settings.N("math.zero");
                _driver.PublishMotion();
            }
            public void OnError(Exception error) { OnNext(_driver._settings.N("math.zero")); }
            public void OnCompleted() { OnNext(_driver._settings.N("math.zero")); }
        }
        private void PublishMotion() => _owner.SetWalkingMotion(!_disposed && !_blocked && _owner.Ready && _moving);
        internal void HideNativeBody()
        {
            // The existing mode avatar remains the visible/card-controlled male; native Player owns movement.
            foreach (var renderer in _body.Keys) if (renderer != null) renderer.enabled = false;
        }
        public void Configure(Parameters settings) { _settings = settings; }
        public void PlaceAtIslandStart(Vector3 eyePosition, float heading)
        {
            var feet = eyePosition + Vector3.up * (_settings.N("movement.bodyRadius") - _settings.N("movement.bodyHeight"));
            WarpFeet(feet, Quaternion.Euler(_settings.N("math.zero"), heading + _settings.N("spawn.yaw"), _settings.N("math.zero")));
        }
        public void WarpFeet(Vector3 feet, Quaternion heading)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NativeActionWalkingDriver));
            Arrival = feet; Heading = heading;
            if (!_owner.Ready) return;
            if (!NavMesh.SamplePosition(feet, out var floor, _settings.N("movement.bodyHeight"), NavMesh.AllAreas) || !_action.Player.agent.Warp(floor.position))
                throw new InvalidOperationException("The destination is outside the native walking floor.");
            _action.Player.transform.rotation = heading;
            _owner.AlignView();
        }
        public void SetViewPose(Vector3 eyePosition, Quaternion heading, float fieldOfView)
        {
            // Keep HMD projection/tracking native; move the player's feet by the requested view displacement.
            WarpFeet(_action.Player.transform.position + eyePosition - ViewCamera.transform.position, heading);
        }
        public Action PrepareTraversal(Transform motion)
        {
            if (_disposed || _origin == null || motion == null || _restoreTraversal != null)
                throw new InvalidOperationException("Native traversal view ownership is unavailable.");
            Transform parent = _origin.parent;
            Scene scene = _origin.gameObject.scene;
            _owner.BeginTraversal();
            try { _origin.SetParent(motion, true); }
            catch { _owner.EndTraversal(); throw; }
            bool restored = false;
            _restoreTraversal = () =>
            {
                if (restored) return;
                restored = true; _restoreTraversal = null;
                if (_origin != null)
                {
                    _origin.SetParent(parent, true);
                    if (parent == null)
                    {
                        if (scene.name == "DontDestroyOnLoad") UnityEngine.Object.DontDestroyOnLoad(_origin.gameObject);
                        else if (scene.IsValid() && scene.isLoaded) SceneManager.MoveGameObjectToScene(_origin.gameObject, scene);
                    }
                }
                _owner.EndTraversal();
            };
            return _restoreTraversal;
        }
        public void SetInputBlocked(bool blocked)
        {
            if (_disposed) return;
            _blocked = blocked;
            ApplyInput();
        }
        internal void MapReady()
        {
            HideNativeBody();
            ApplyInput();
        }
        private void ApplyInput()
        {
            if (_action.Player == null) return;
            bool blocked = _blocked || !_owner.Ready;
            _action.SetCursorLock(!blocked);
            // Native Mover.Base observes its GameObject even when the component is disabled.
            // Set its own stop flag so that subscription cannot continue movement or clear the agent stop.
            _action.Player.move.isStop = blocked;
            _action.Player.move.enabled = !blocked;
            var agent = _action.Player.agent;
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = blocked;
            PublishMotion();
        }
        public WalkingStatus Snapshot()
        {
            var agent = _action.Player.agent;
            _status.grounded = _status.physicalGrounded = agent.enabled && agent.isOnNavMesh;
            _status.crouching = Crouching;
            _status.cursorReleased = _status.cursorReleaseRequested = _blocked;
            _status.cameraPosition = ViewCamera.transform.position;
            _status.height = agent.height; _status.radius = agent.radius;
            _status.navigationState = "native NavMesh";
            return _status;
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _movementSubscription?.Dispose(); _movementSubscription = null;
            _owner.SetWalkingMotion(false);
            _restoreTraversal?.Invoke();
            if (_action != null && _action.Player != null)
            {
                _action.SetCursorLock(false);
                _action.Player.move.isStop = true;
                _action.Player.move.enabled = false;
            }
            foreach (var pair in _body) if (pair.Key != null) pair.Key.enabled = pair.Value;
            _body.Clear();
        }
    }
}
