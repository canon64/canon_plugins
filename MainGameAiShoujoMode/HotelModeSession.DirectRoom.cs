using System;
using System.Collections;
using System.Linq;

namespace MainGameAiShoujoMode
{
    internal sealed partial class HotelModeSession
    {
        private HotelFloorRuntime _hRoomFloor;
        private bool _directRoomOpening;
        private HotelFloorRuntime RoomFloor => _hRoomFloor ?? _floor;
        private void OpenDirectRoom(HotelFloorRuntime room)
        {
            if (_closing || _returnRequested || _loading || _changingFloor || _hSession?.Busy == true || _directRoomOpening) return;
            BeginRoomEntryTrace(room.Definition.room.id);
            _directRoomOpening = true; _loading = true; _context.WalkingReady = false;
            try
            {
                _fade.Cover(() => { TraceRoom("loading-cover-complete"); StartCoroutine(EnterDirectRoomAfterCover(room)); });
            }
            catch (Exception ex)
            {
                ReportRoomFailure(ex, "direct-entry");
                if (_hSession?.Busy != true && _directRoomOpening) RoomSessionEnded();
            }
        }
        private IEnumerator EnterDirectRoomAfterCover(HotelFloorRuntime room)
        {
            var steps = new System.Collections.Generic.Stack<IEnumerator>();
            steps.Push(EnterDirectRoomCore(room));
            try
            {
                while (steps.Count > default(int))
                {
                    bool next; object current = null;
                    try { next = steps.Peek().MoveNext(); if (next) current = steps.Peek().Current; }
                    catch (Exception ex)
                    {
                        ReportRoomFailure(ex, "direct-entry");
                        if (_hSession?.Busy != true && _directRoomOpening) RoomSessionEnded();
                        yield break;
                    }
                    if (!next) { (steps.Pop() as IDisposable)?.Dispose(); continue; }
                    if (current is IEnumerator nested) steps.Push(nested); else yield return current;
                }
            }
            finally { while (steps.Count > default(int)) (steps.Pop() as IDisposable)?.Dispose(); }
        }
        private IEnumerator EnterDirectRoomCore(HotelFloorRuntime room)
        {
            // StartCoroutine runs immediately up to this yield. Let the covered frame render
            // before card/state IO, scene activation or native-session preparation can block.
            // A next-frame yield also works while the VR compositor pauses eye rendering.
            yield return null;
            if (_closing || !_directRoomOpening) yield break;
            if (_returnRequested) { _loading = false; BeginReturn(); yield break; }
                TraceRoom("loading-frame-ready");
                var definition = room.Definition.room;
                if (_roomCards.Registered(definition.cardSlot) == null)
                    throw new InvalidOperationException(definition.label + "で使う女性カードをTABメニューから登録してください。");
                TraceRoom("registered-card-checked");
                yield return EnsureFloorLoaded(room);
                if (_closing || _returnRequested || !_directRoomOpening) yield break;
                _nativeWalking?.Suspend(); SuspendRoomBehaviour(_walker); _walker.SetCursorReleased(true);
                _hRoomFloor = room;
                _floor.SetActive(false); room.SetActive(true);
                _sessionRoomRoot = room.Roots.Single(root => root.name == definition.rootObject).transform;
                _roomObjects?.Dispose();
                _roomObjects = new HotelRoomObjectLayout(this, _sessionRoomRoot, definition.id, _p, RefreshRoomPortalPositions);
                TraceRoom("room-objects-ready");
                _loading = false;
                StartRoomSessionAfterLoading(definition.id);
                if (_hSession?.Busy != true && _directRoomOpening) RoomSessionEnded();
        }
        private void RestoreDirectRoom()
        {
            if (_hRoomFloor != null)
            {
                _hRoomFloor.SetActive(false); _hRoomFloor = null;
                _roomObjects?.Dispose(); _roomObjects = null;
            }
            _directRoomOpening = false;
        }
        private void RevealDirectRoom()
        {
            if (_directRoomOpening)
            {
                _directRoomOpening = false;
                _fade.Reveal(() => { _loading = false; if (_returnRequested) BeginReturn(); });
            }
        }
    }
}
