using System;
using System.Collections;
using System.Collections.Generic;
using CanonPlugins.Shared;
using UnityEngine;

namespace MainGameVMDPlay
{
    public sealed partial class Plugin
    {
        internal static bool RoomAllowed => PluginSessionGate.Allowed;
        private bool _wasInAllPoseRoom;
        private int _roomEpoch;
        private readonly List<RoomBoundCoroutine> _roomRoutines = new List<RoomBoundCoroutine>();

        private Coroutine StartRoomCoroutine(IEnumerator routine)
        {
            return StartCoroutine(GuardRoomRoutine(routine));
        }

        private RoomBoundCoroutine GuardRoomRoutine(IEnumerator routine)
        {
            int epoch = _roomEpoch;
            var guarded = new RoomBoundCoroutine(routine, () => RoomAllowed && epoch == _roomEpoch);
            _roomRoutines.RemoveAll(item => item.IsCompleted);
            _roomRoutines.Add(guarded);
            return guarded;
        }

        private void LeaveAllPoseRoom()
        {
            if (!_wasInAllPoseRoom && _roomRoutines.Count == 0) return;
            _wasInAllPoseRoom = false;
            _roomEpoch++;
            _controlTransition.Invalidate();
            foreach (var routine in _roomRoutines)
                try { routine.Dispose(); } catch (Exception ex) { LogError("[RoomExit] coroutine: " + ex); }
            _roomRoutines.Clear();
            StopAllCoroutines();
            ControlTransition.Cleanup(ex => LogError("[RoomExit] " + ex), AbortAstral, DisposeIndependentActor,
                PlaybackUnloadAll, RestoreGameAudio, ReleaseCameraMotion, ReleaseActorCamera);
            _hSceneProc = null; _observedHScene = null;
            _hActor = null; _target.Actor = null; _target.Independent = false;
            _target.Generation++; _target.SceneGeneration++;
            _insideHScene = false;
            _nextScanTime = 0f;
            _actorCameraRequested = false;
            _actorGizmoRequested = false;
            _modelReloadAt = _modelResumeTime = -1f;
            SetState(PluginState.Idle);
        }
    }
}
