using System;
using System.Collections.Generic;
using System.Reflection;
using CanonGate;
using MainGameHLocationCoordinates;
using UnityEngine;

namespace MainGameHLocationPlacement
{
    internal sealed partial class PlacementSession : IDisposable
    {
        internal static PlacementSession Current;
        internal readonly RuntimeBridge Bridge;
        private readonly Plugin _plugin;
        private readonly GateApi.Permit _permit;
        private readonly MonoBehaviour _scene;
        private readonly OriginStore _store;
        private readonly Dictionary<PlaceOrigin, CoordinateSpace> _spaces = new Dictionary<PlaceOrigin, CoordinateSpace>();
        private readonly EventInfo _movingEvent, _movedEvent;
        private readonly Action<ChaControl, long, string, string> _onMoving, _onMoved;
        private RuntimeHooks _hooks;
        private bool _disposed;
        private string _placeId;
        private RigidPose? _destination;
        internal bool Valid => !_disposed && _permit.IsValid && _scene != null;
        internal PlacementSession(string directory, Plugin plugin)
        {
            _plugin = plugin; _permit = GateApi.Capture(Plugin.Guid); _scene = GateApi.Current.Scene;
            Bridge = new RuntimeBridge(); _store = new OriginStore(directory);
            _onMoving = Moving; _onMoved = Moved;
            _movingEvent = Bridge.Map.GetType().GetEvent("HLocationMoving", BindingFlags.Public | BindingFlags.Static);
            _movedEvent = Bridge.Map.GetType().GetEvent("HLocationMoved", BindingFlags.Public | BindingFlags.Static);
            if (_movingEvent?.EventHandlerType != _onMoving.GetType() || _movedEvent?.EventHandlerType != _onMoved.GetType()) throw new MissingMemberException("H場所の移動通知が未対応です。");
            try
            {
                _hooks = new RuntimeHooks(Bridge, _scene.GetType());
                _movingEvent.AddEventHandler(null, _onMoving); _movedEvent.AddEventHandler(null, _onMoved);
                Current = this;
            }
            catch { Dispose(); throw; }
        }
        internal void Report(Exception exception)
        { _plugin.Message(exception.Message); _plugin.Diagnostic(() => exception.ToString()); }
        internal PlaceOrigin Capture() { if (!Valid) throw new InvalidOperationException("Hセッションが終了しています。"); return Bridge.Capture(); }
        internal PlaceOrigin CaptureHip() { if (!Valid) throw new InvalidOperationException("Hセッションが終了しています。"); return Bridge.Capture(false); }
        private void RefreshDestination()
        {
            RuntimeBridge.Unit(Bridge.Female);
            _destination = RuntimeBridge.Pose(Bridge.Female);
            foreach (var space in _spaces.Values) space.Close();
            _spaces.Clear();
        }
        internal RigidPose Project(PlaceOrigin origin, RigidPose pose)
        {
            if (!Valid) throw new InvalidOperationException("Hセッションが終了しています。");
            if (!_destination.HasValue) RefreshDestination();
            if (!_spaces.TryGetValue(origin, out var space))
            {
                space = CoordinatesApi.Register(origin.femaleRoot.ToPose());
                space.UpdateDestination(_destination.Value, reason: "load", placeId: _placeId ?? "native");
                _spaces.Add(origin, space);
            }
            return space.Transform(pose);
        }
        internal RigidPose ProjectMove(RigidPose before, RigidPose after, RigidPose pose)
        {
            var space = CoordinatesApi.Register(before);
            try { space.UpdateDestination(after, reason: "move", placeId: _placeId ?? "native"); return space.Transform(pose); }
            finally { space.Close(); }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (ReferenceEquals(Current, this)) Current = null;
            try
            {
                _movingEvent?.RemoveEventHandler(null, _onMoving); _movedEvent?.RemoveEventHandler(null, _onMoved);
                ClearAnchors();
            }
            finally
            {
                _hooks?.Dispose(); _saves.Clear(); _movements.Clear(); _pendingNativeMove = null; _nativeDepth = 0;
                foreach (var space in _spaces.Values) space.Close(); _spaces.Clear(); _selection = null;
            }
        }
    }
}
