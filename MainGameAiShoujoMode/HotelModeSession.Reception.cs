using System;
using System.Collections;
using System.Linq;
using MainGameAiShoujoMode.Conversation;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MainGameAiShoujoMode
{
    internal sealed partial class HotelModeSession
    {
        private KksConversationView _receptionView;
        private EventSystem _receptionEvents;
        private string _receptionContent;
        private Coroutine _receptionInvitation;
        private void OpenReception()
        {
            try
            {
                if (_receptionView == null) _receptionView = new KksConversationView(transform, _receptionContent, CloseReception);
                _receptionView.EditRequested = (name, text) => _authoring?.CaptureNative(_receptionView, name, text);
                if (EventSystem.current == null && _receptionEvents == null)
                {
                    var events = new GameObject("Hotel Reception UI Events", typeof(EventSystem), typeof(StandaloneInputModule));
                    events.transform.SetParent(transform, false); _receptionEvents = events.GetComponent<EventSystem>();
                }
                _receptionist.FocusPlayer();
                _walkingMenu = false; _receptionist.RestorePlacement();
                _receptionView.Show(_receptionist.Name, "受付メニュー");
                _receptionView.SetChoices(new[]
                {
                    new KksConversationView.Choice("受付嬢とH", InviteReceptionist),
                    new KksConversationView.Choice("閉じる", CloseReception)
                });
                _receptionMenu = true; _context.SetPanel(true); _walker.SetCursorReleased(true);
                CaptureReceptionInput();
            }
            catch (Exception ex) { CloseReception(); ReportRoomFailure(ex, "reception-ui"); }
        }
        private void InviteReceptionist()
        {
            if (!_receptionMenu || _receptionInvitation != null || _closing || _returnRequested
                || _loading || _changingFloor || _hSession?.Busy == true) return;
            _receptionView.SetChoices(Array.Empty<KksConversationView.Choice>());
            _receptionView.PresentLine(_receptionist.Name, "ではこちらへどうぞ");
            _receptionInvitation = StartCoroutine(EnterReceptionSuite());
        }
        private IEnumerator EnterReceptionSuite()
        {
            // One finite presentation delay; closing the conversation cancels the invitation.
            yield return new WaitForSecondsRealtime(_p.N("math.two"));
            _receptionInvitation = null;
            if (!_receptionMenu || _closing || _returnRequested || _floor.Definition.id != "lobby") yield break;
            var destination = _floors.Single(f => f.Definition.id == "reception-suite");
            CloseReception();
            TravelToFloor(destination, destination.Definition.arrival, destination.Definition.yaw,
                () => StartRoomSession("reception-suite"));
        }
        private void CancelReceptionInvitation()
        {
            if (_receptionInvitation == null) return;
            StopCoroutine(_receptionInvitation); _receptionInvitation = null;
        }
        private void CaptureReceptionInput()
        {
            AiModeNativeUiCapture.Apply(Plugin.Guid, new Rect(_p.N("math.zero"), _p.N("math.zero"), Screen.width, Screen.height));
        }
        private void CloseReception()
        {
            CancelReceptionInvitation();
            _receptionView?.Hide(); _receptionMenu = false;
            if (!_closing && _context != null) WalkingMenuChanged();
        }
        private void DisposeReception()
        {
            CancelReceptionInvitation();
            _receptionMenu = false; _receptionView?.Dispose(); _receptionView = null;
            if (_receptionEvents != null) Destroy(_receptionEvents.gameObject);
            _receptionEvents = null;
        }
    }
}
