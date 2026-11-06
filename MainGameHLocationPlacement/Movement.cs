using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MainGameHLocationCoordinates;
using UnityEngine;

namespace MainGameHLocationPlacement
{
    internal sealed class IkBeforeMove
    {
        internal int Index;
        internal object State;
        internal Transform Proxy;
        internal RigidPose World;
        internal RigidPose? Hold;
    }
    internal sealed class TransitionBeforeMove
    {
        internal object Point;
        internal RigidPose Start, Target;
    }
    internal sealed class Movement
    {
        internal RigidPose Before;
        internal readonly List<IkBeforeMove> Ik = new List<IkBeforeMove>();
        internal readonly List<TransitionBeforeMove> Transitions = new List<TransitionBeforeMove>();
        internal object TransitionList;
        internal string DiagnosticOperation;
    }
    internal sealed partial class PlacementSession
    {
        private readonly Dictionary<long, Movement> _movements = new Dictionary<long, Movement>();
        private int _nativeDepth;
        private Movement _pendingNativeMove;
        internal Movement StartNativeMove(object scene, string reason = "native")
        {
            if (!Valid || !ReferenceEquals(scene, _scene)) return null;
            _nativeDepth++;
            if (_nativeDepth != 1 || _movements.Count != 0) return null;
            try { return _pendingNativeMove = SnapshotMovement(reason); }
            catch { _nativeDepth--; throw; }
        }
        internal void EndNativeMove(object scene, Movement before, Exception exception)
        {
            if (!ReferenceEquals(scene, _scene)) return;
            if (_nativeDepth > 0) _nativeDepth--;
            if (before != null && ReferenceEquals(before, _pendingNativeMove))
            {
                _pendingNativeMove = null;
                if (exception == null && Valid && _movements.Count == 0) ApplyMovement(before);
            }
        }
        private void Moving(ChaControl female, long operation, string reason, string place)
        {
            if (!Valid || female == null) return;
            try
            {
                if (female.transform != Bridge.Female) return;
                // A map move may be requested inside ChangeAnimator's postfix/finalizer.
                // Finish the native delta first; EndNativeMove must not apply both deltas again.
                if (_pendingNativeMove != null)
                {
                    var native = _pendingNativeMove; _pendingNativeMove = null;
                    ApplyMovement(native);
                }
                _movements[operation] = SnapshotMovement(_plugin.Diagnostics ? "map:" + operation + ":" + reason + ":" + place : null);
            }
            catch (Exception ex) { Report(ex); }
        }
        private void Moved(ChaControl female, long operation, string reason, string place)
        {
            if (!Valid || female == null) return;
            try
            {
                if (female.transform != Bridge.Female) return;
                _placeId = string.IsNullOrEmpty(place) ? "native" : place;
                if (_movements.TryGetValue(operation, out var before)) ApplyMovement(before);
                else { Trace("move.unpaired", () => "operation=" + operation + " reason=" + reason + " place=" + place); RefreshDestination(); UpdateAnchors(); }
            }
            catch (Exception ex) { Report(ex); }
            finally { _movements.Remove(operation); }
        }
        private Movement SnapshotMovement(string reason)
        {
            var result = new Movement { Before = RuntimeBridge.Pose(Bridge.Female), TransitionList = R.Get(Bridge.Hip, "_activeTransitionPoints") };
            if (_plugin.Diagnostics)
            {
                result.DiagnosticOperation = reason + ":event-" + (_diagnosticSequence + 1);
                TraceState("move.before", result.DiagnosticOperation);
            }
            var states = Bridge.States;
            for (int i = 0; i < states.Length; i++)
            {
                object state = states.GetValue(i);
                if (!Bridge.Independent(i) || !R.Get<bool>(state, "Running")) continue;
                var proxy = R.Get<Transform>(state, "Proxy");
                if (proxy == null) continue;
                result.Ik.Add(new IkBeforeMove { Index = i, State = state, Proxy = proxy, World = RuntimeBridge.Pose(proxy),
                    Hold = R.Get<bool>(state, "HasPostDragHold") ? new RigidPose(R.Get<Vector3>(state, "PostDragHoldPos"), R.Get<Quaternion>(state, "PostDragHoldRot")) : (RigidPose?)null });
            }
            if (result.TransitionList is IEnumerable points)
            foreach (object point in points)
            {
                int index = R.Get<int>(point, "Index");
                if (index < 0 || index >= states.Length || !Bridge.Independent(index) || R.Get<bool>(point, "UseFollowLocalTransition") || !R.Get<bool>(point, "HasPose")) continue;
                result.Transitions.Add(new TransitionBeforeMove { Point = point,
                    Start = new RigidPose(R.Get<Vector3>(point, "StartPos"), R.Get<Quaternion>(point, "StartRot")),
                    Target = new RigidPose(R.Get<Vector3>(point, "TargetPos"), R.Get<Quaternion>(point, "TargetRot")) });
            }
            return result;
        }
        private void ApplyMovement(Movement before)
        {
            RefreshDestination(); UpdateAnchors();
            RigidPose after = _destination.Value;
            Trace("move.frames", () => "operation=" + before.DiagnosticOperation + " before={" + PoseText(before.Before) + "} destination={" + PoseText(after) +
                "} delta={" + PoseText(after * before.Before.Inverse()) + "} distance_m=" + Number((after.position-before.Before.position).magnitude) +
                " angle_deg=" + Number(Quaternion.Angle(before.Before.rotation,after.rotation)) + " IK=" + before.Ik.Count + " transitions=" + before.Transitions.Count);
            if ((before.Before.position - after.position).sqrMagnitude > 1e-10f || Quaternion.Angle(before.Before.rotation, after.rotation) > .001f)
            {
                // A deliberate place move is not a follow-distance escape. Let the native guard
                // establish its next baseline without changing the linked IK pose or weight.
                foreach (object state in Bridge.States)
                    if (state != null && R.Get<bool>(state, "Running") && !R.Get<bool>(state, "ExternalOwner") && R.Get(state, "FollowBone") != null)
                        R.Set(state, "HasFollowDistanceBaseline", false);
            }
            foreach (var item in before.Ik)
            {
                if (!Bridge.Independent(item.Index) || !ReferenceEquals(Bridge.States.GetValue(item.Index), item.State)
                    || !R.Get<bool>(item.State, "Running") || item.Proxy == null || R.Get<Transform>(item.State, "Proxy") != item.Proxy)
                { Trace("move.IK.skipped", () => "operation=" + before.DiagnosticOperation + " IK=" + Bridge.IkKeys[item.Index] + " reason=state-changed-or-" + IkReason(item.Index)); continue; }
                var placed = ProjectMove(before.Before, after, item.World);
                item.Proxy.SetPositionAndRotation(placed.position, placed.rotation);
                Trace("move.IK.result", () => "operation=" + before.DiagnosticOperation + " IK=" + Bridge.IkKeys[item.Index] +
                    " before={" + PoseText(item.World) + "} calculated={" + PoseText(placed) + "} actual={" + PoseText(RuntimeBridge.Pose(item.Proxy)) + "}");
                if (item.Hold.HasValue && R.Get<bool>(item.State, "HasPostDragHold"))
                {
                    var hold = ProjectMove(before.Before, after, item.Hold.Value);
                    R.Set(item.State, "PostDragHoldPos", hold.position); R.Set(item.State, "PostDragHoldRot", hold.rotation);
                    Trace("move.hold", () => "operation=" + before.DiagnosticOperation + " IK=" + Bridge.IkKeys[item.Index] + " before={" + PoseText(item.Hold.Value) + "} result={" + PoseText(hold) + "}");
                }
            }
            if (ReferenceEquals(before.TransitionList, R.Get(Bridge.Hip, "_activeTransitionPoints")))
            foreach (var item in before.Transitions)
            {
                int index = R.Get<int>(item.Point, "Index");
                if (!Bridge.Independent(index)) continue;
                var start = ProjectMove(before.Before, after, item.Start); var target = ProjectMove(before.Before, after, item.Target);
                R.Set(item.Point, "StartPos", start.position); R.Set(item.Point, "StartRot", start.rotation);
                R.Set(item.Point, "TargetPos", target.position); R.Set(item.Point, "TargetRot", target.rotation);
                Trace("move.transition", () => "operation=" + before.DiagnosticOperation + " IK=" + Bridge.IkKeys[index] +
                    " startBefore={" + PoseText(item.Start) + "} startAfter={" + PoseText(start) + "} targetBefore={" + PoseText(item.Target) + "} targetAfter={" + PoseText(target) + "}");
            }
            if (_plugin.Diagnostics) _plugin.Diagnostic(() => "H location move: IK=" + before.Ik.Count + " transitions=" + before.Transitions.Count + " place=" + _placeId);
            TraceState("move.after", before.DiagnosticOperation);
        }
    }
}
