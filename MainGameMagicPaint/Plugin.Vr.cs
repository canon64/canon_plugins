using System;
using UnityEngine;
using Valve.VR;
using VRGIN.Controls;
using VRGIN.Core;
namespace MainGameMagicPaint
{
    public sealed partial class Plugin
    {
        SteamVR_Action_Boolean _gripAction, _triggerAction;
        SteamVR_Action_Vector2 _axisAction;
        PaintButtons _buttons;
        SteamVR_Events.Action _focusEvent;
        Controller _hand;
        Controller.Lock _focus;
        SteamVR_Behaviour_Pose _pose;
        SteamVR_Input_Sources _handSource;
        HeldPaintLoop _loop;
        bool _focused = true;
        static readonly SteamVR_Input_Sources[] Hands = { SteamVR_Input_Sources.LeftHand, SteamVR_Input_Sources.RightHand };
        void ConnectVr()
        {
            if (!VR.Active) return;
            _gripAction = SteamVR_Actions.legacy_emulate.Grip_Press;
            _triggerAction = SteamVR_Actions.legacy_emulate.Axis1_Press;
            foreach (var hand in Hands)
            {
                _gripAction.AddOnStateDownListener(GripDown, hand); _gripAction.AddOnStateUpListener(GripUp, hand);
                _gripAction.AddOnActiveChangeListener(GripActive, hand);
                _triggerAction.AddOnStateDownListener(TriggerDown, hand); _triggerAction.AddOnStateUpListener(TriggerUp, hand);
            }
            _buttons = new PaintButtons(s => { if (HandAllowed(s) && !_stick.Menu) ToggleSettings(); }, s => { if (HandAllowed(s)) ClearDrawing(); }, MenuDown, MenuUp);
            _buttons.Connect();
            _axisAction = _buttons.Axis;
            foreach (var hand in Hands) _axisAction.AddOnChangeListener(Axis, hand);
            _focusEvent = SteamVR_Events.InputFocusAction(focused => { _focused = focused; if (!focused) ReleaseHand(); }); _focusEvent.enabled = true;
        }
        void DisconnectVr()
        {
            ReleaseHand();
            if (_focusEvent != null) _focusEvent.enabled = false; _focusEvent = null;
            try { _buttons?.Dispose(); } finally { _buttons = null; }
            foreach (var hand in Hands)
            {
                if (_gripAction != null) { _gripAction.RemoveOnStateDownListener(GripDown, hand); _gripAction.RemoveOnStateUpListener(GripUp, hand); _gripAction.RemoveOnActiveChangeListener(GripActive, hand); }
                if (_triggerAction != null) { _triggerAction.RemoveOnStateDownListener(TriggerDown, hand); _triggerAction.RemoveOnStateUpListener(TriggerUp, hand); }
                _axisAction?.RemoveOnChangeListener(Axis, hand);
            }
            _gripAction = _triggerAction = null; _axisAction = null;
        }
        bool HandAllowed(SteamVR_Input_Sources s)
        {
            if (!CanAct || !_focused || _hand == null || s != _handSource) return false;
            if (_focus == null || !_focus.IsValid || !_hand.IsTracking) { ReleaseHand(); return false; }
            return true;
        }
        void GripDown(SteamVR_Action_Boolean a, SteamVR_Input_Sources s)
        {
            if (!CanAct || !_focused || _model == null || VR.Mode == null) return;
            if (_hand != null) { if (s != _handSource) return; ReleaseHand(); }
            var hand = s == SteamVR_Input_Sources.LeftHand ? VR.Mode.Left : VR.Mode.Right;
            if (hand == null || !hand.IsTracking || Vector3.Distance(hand.transform.position, _model.Root.transform.position) > .12f) return;
            if (!CanonPlugins.Shared.VrItemFocus.TryAcquire(hand, out var focus)) return;
            _hand = hand; _focus = focus; _handSource = s;
            try
            {
                HideGizmo(); SetDesktop(false); _model.Root.transform.SetParent(hand.transform, true);
                _pose = hand.Tracking;
                if (_pose != null) { _pose.onConnectedChangedEvent += Connected; _pose.onTrackingChangedEvent += Tracking; }
                _vrPaint.Grip(); _settingsVisible = false; ResetContacts(); _stick.Reset(); StopPaintPress(); _focused = true; _buttons.Activate(s, true);
                _loop = _model.Root.AddComponent<HeldPaintLoop>(); _loop.Owner = this;
                if (TraceEnabled) { _trace?.ResetTransitions(); TraceInput("GRIP",s,true); }
                _status = "A：設定表示／設定中は上下：項目・左右：調整／通常左右：UNDO・REDO／B：全削除／押し込み：道具";
            }
            catch (Exception ex) { ReleaseHand(); Fail("保持失敗", ex); }
        }
        void GripUp(SteamVR_Action_Boolean a, SteamVR_Input_Sources s) { if (_hand != null && s == _handSource) ReleaseHand(); }
        void GripActive(SteamVR_Action_Boolean a, SteamVR_Input_Sources s, bool active) { if (!active && _hand != null && s == _handSource) ReleaseHand(); }
        void TriggerDown(SteamVR_Action_Boolean a, SteamVR_Input_Sources s)
        {
            bool accepted = HandAllowed(s) && !_stick.Menu && !_inspectionPending;
            if (accepted) _trigger = _vrPaint.Press();
            InspectionInput(accepted); TraceInput("TRIGGER_DOWN",s,accepted);
        }
        void TriggerUp(SteamVR_Action_Boolean a, SteamVR_Input_Sources s)
        {
            if (_hand == null || s != _handSource) return;
            TraceInput("TRIGGER_UP",s,true); if(TraceEnabled) TraceGeometry("TRIGGER_UP");
            StopPaintPress(); EndStroke(); RequestInspectionCapture("トリガー解放");
        }
        void Axis(SteamVR_Action_Vector2 a, SteamVR_Input_Sources s, Vector2 axis, Vector2 delta)
        {
            if (!HandAllowed(s)) return;
            if (Mathf.Abs(axis.x) < .25f && Mathf.Abs(axis.y) < .25f) SaveConfig();
            if (_stick.Menu) { _stick.X = axis.x; _stick.Y = axis.y; return; }
            int edge = _stick.Edge(axis.x, axis.y);
            if (_settingsVisible)
            {
                if (Math.Abs(edge) == 2) SelectSetting(edge > 0 ? -1 : 1);
                else if (Math.Abs(edge) == 1 && _setting == PaintSetting.Color) ChangeColor(edge > 0 ? 1 : -1);
            }
            else if (edge == -1) Undo(false); else if (edge == 1) Undo(true);
        }
        void MenuDown(SteamVR_Input_Sources s)
        {
            if (!HandAllowed(s)) return; StopPaintPress(); EndStroke(); ResetContacts(); _stick.Reset(); _stick.Menu = true;
            Vector2 axis = _axisAction.GetAxis(s); _stick.X = axis.x; _stick.Y = axis.y;
        }
        void MenuUp(SteamVR_Input_Sources s)
        {
            if (!HandAllowed(s) || !_stick.Menu) return; int selected = _stick.MenuSelection; _stick.BlockUntilNeutral();
            if (selected >= 0) ChangeTool((PaintTool)selected);
            SaveConfig();
        }
        void Connected(SteamVR_Behaviour_Pose pose, SteamVR_Input_Sources source, bool connected) { if (!connected) ReleaseHand(); }
        void Tracking(SteamVR_Behaviour_Pose pose, SteamVR_Input_Sources source, ETrackingResult state) { if (state != ETrackingResult.Running_OK) ReleaseHand(); }
        void ReleaseHand()
        {
            bool wasHeld = _hand != null;
            var focus = _focus; var source = _handSource;
            _focus = null; _hand = null; _handSource = SteamVR_Input_Sources.Any;
            try
            {
                if (wasHeld) { TraceInput("RELEASE",source,true); StopPaintPress(); EndStroke(); RequestInspectionCapture("グリップ解放・保持終了"); }
            }
            finally
            {
                _vrPaint.Release(); _trigger = false; _settingsVisible = false;
                try
                {
                    if (_loop != null) { _loop.Owner = null; _loop.enabled = false; Destroy(_loop); _loop = null; }
                    var pose = _pose; _pose = null;
                    if (pose != null) { pose.onConnectedChangedEvent -= Connected; pose.onTrackingChangedEvent -= Tracking; }
                    _buttons?.Activate(source, false); ResetContacts(); _stick.Reset();
                    if (_model != null) _model.Root.transform.SetParent(_scene != null ? _scene.transform : null, true);
                    _hud?.Hide(); SaveConfig();
                }
                finally { if (focus != null && focus.IsValid) focus.SafeRelease(); }
            }
        }
        internal void HeldTick()
        {
            if (!CanAct || !_focused || _hand == null || _model == null) { ReleaseHand(); return; }
            if (!HandAllowed(_handSource) || _model == null) return;
            try
            {
                if (_settingsVisible && !_stick.Menu && _stick.Horizontal && _setting != PaintSetting.Color && Mathf.Abs(_stick.X) > .25f)
                    AdjustSetting(Mathf.Sign(_stick.X) * Mathf.InverseLerp(.25f, 1, Mathf.Abs(_stick.X)) * Time.unscaledDeltaTime);
                if (!_inspectionPending) TickTool(); TraceSample(); UpdateHud();
            }
            catch (Exception ex) { ReleaseHand(); Fail("描画操作失敗", ex); }
        }
    }
    // Created only on successful grip; destroyed on every release/disable/focus-loss path.
    // User-approved exception: contact/spray guide sampling while holding this drawing tool only.
    [DefaultExecutionOrder(32000)]
    public sealed class HeldPaintLoop : MonoBehaviour
    {
        internal Plugin Owner;
        void LateUpdate() { if (Owner != null) Owner.HeldTick(); }
    }
}
