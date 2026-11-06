using System;
using System.Linq;
using UnityEngine;
using Valve.VR;

namespace MainGameDesktopDisplay
{
    // The native SteamVR dispatcher supplies physical A/B edges and stick changes.
    // A dedicated set avoids confusing B with the legacy stick-click alias.
    internal sealed class DesktopButtonInput : IDisposable
    {
        private SteamVR_ActionSet _set;
        private SteamVR_Action_Boolean _left, _right;
        private SteamVR_Action_Vector2 _scroll;
        private readonly Action<bool, bool> _button;
        private readonly Action<Vector2> _axis;
        internal DesktopButtonInput(Action<bool, bool> button, Action<Vector2> axis) { _button = button; _axis = axis; }
        internal void Connect()
        {
            if (_set != null) return;
            if (SteamVR_Input.actionSets == null || SteamVR_Input.actions == null) throw new InvalidOperationException("SteamVR入力が初期化されていません。");
            try
            {
                _set = SteamVR_ActionSet.Create<SteamVR_ActionSet>("/actions/desktopdisplay");
                SteamVR_Input.actionSets = Add(SteamVR_Input.actionSets, _set);
                SteamVR_Input.PreinitializeActionSetDictionaries();
                _left = SteamVR_Action.Create<SteamVR_Action_Boolean>("/actions/desktopdisplay/in/left_click");
                _right = SteamVR_Action.Create<SteamVR_Action_Boolean>("/actions/desktopdisplay/in/right_click");
                _scroll = SteamVR_Action.Create<SteamVR_Action_Vector2>("/actions/desktopdisplay/in/scroll");
                SteamVR_Input.actions = Add(SteamVR_Input.actions, _left, _right, _scroll);
                SteamVR_Input.actionsIn = Add<ISteamVR_Action_In>(SteamVR_Input.actionsIn, _left, _right, _scroll);
                SteamVR_Input.actionsBoolean = Add(SteamVR_Input.actionsBoolean, _left, _right);
                SteamVR_Input.actionsVector2 = Add(SteamVR_Input.actionsVector2, _scroll);
                SteamVR_Input.actionsNonPoseNonSkeletonIn = Add<ISteamVR_Action_In>(SteamVR_Input.actionsNonPoseNonSkeletonIn, _left, _right, _scroll);
                SteamVR_Input.PreinitializeActionDictionaries(); _set.FinishPreInitialize();
                _set.Initialize(true); _left.Initialize(true); _right.Initialize(true); _scroll.Initialize(true);
                if (_set.handle == 0 || _left.handle == 0 || _right.handle == 0 || _scroll.handle == 0)
                    throw new InvalidOperationException("ディスプレイ用A/B入力設定を配置してゲームを再起動してください。");
                var side = SteamVR_Input_Sources.RightHand;
                _left.AddOnStateDownListener(LeftDown, side); _left.AddOnStateUpListener(LeftUp, side);
                _right.AddOnStateDownListener(RightDown, side); _right.AddOnStateUpListener(RightUp, side);
                _left.AddOnActiveChangeListener(ActiveChanged, side); _right.AddOnActiveChangeListener(ActiveChanged, side);
                _scroll.AddOnChangeListener(Scroll, side);
            }
            catch { Dispose(); throw; }
        }
        internal void Active(bool active)
        { if (_set == null) return; if (active) _set.Activate(SteamVR_Input_Sources.RightHand, 100, false); else _set.Deactivate(SteamVR_Input_Sources.RightHand); }
        private void LeftDown(SteamVR_Action_Boolean a, SteamVR_Input_Sources s) { if (s == SteamVR_Input_Sources.RightHand) _button(false, true); }
        private void LeftUp(SteamVR_Action_Boolean a, SteamVR_Input_Sources s) { if (s == SteamVR_Input_Sources.RightHand) _button(false, false); }
        private void RightDown(SteamVR_Action_Boolean a, SteamVR_Input_Sources s) { if (s == SteamVR_Input_Sources.RightHand) _button(true, true); }
        private void RightUp(SteamVR_Action_Boolean a, SteamVR_Input_Sources s) { if (s == SteamVR_Input_Sources.RightHand) _button(true, false); }
        private void ActiveChanged(SteamVR_Action_Boolean a, SteamVR_Input_Sources s, bool active)
        { if (!active && s == SteamVR_Input_Sources.RightHand) { _button(false, false); _button(true, false); } }
        private void Scroll(SteamVR_Action_Vector2 a, SteamVR_Input_Sources s, Vector2 v, Vector2 d) { if (s == SteamVR_Input_Sources.RightHand) _axis(v); }
        private static T[] Add<T>(T[] values, params T[] items) => (values ?? new T[0]).Concat(items).ToArray();
        private static T[] Remove<T>(T[] values, params object[] items) => (values ?? new T[0]).Where(x => !items.Any(item => ReferenceEquals(x, item))).ToArray();
        public void Dispose()
        {
            if (_set == null) return;
            try
            {
                Active(false);
                var s = SteamVR_Input_Sources.RightHand;
                _left?.RemoveOnStateDownListener(LeftDown, s); _left?.RemoveOnStateUpListener(LeftUp, s);
                _right?.RemoveOnStateDownListener(RightDown, s); _right?.RemoveOnStateUpListener(RightUp, s);
                _left?.RemoveOnActiveChangeListener(ActiveChanged, s); _right?.RemoveOnActiveChangeListener(ActiveChanged, s);
                _scroll?.RemoveOnChangeListener(Scroll, s);
            }
            finally
            {
                SteamVR_Input.actionsNonPoseNonSkeletonIn = Remove(SteamVR_Input.actionsNonPoseNonSkeletonIn, _left, _right, _scroll);
                SteamVR_Input.actionsVector2 = Remove(SteamVR_Input.actionsVector2, _scroll);
                SteamVR_Input.actionsBoolean = Remove(SteamVR_Input.actionsBoolean, _left, _right);
                SteamVR_Input.actionsIn = Remove(SteamVR_Input.actionsIn, _left, _right, _scroll);
                SteamVR_Input.actions = Remove(SteamVR_Input.actions, _left, _right, _scroll);
                SteamVR_Input.actionSets = Remove(SteamVR_Input.actionSets, _set);
                SteamVR_Input.PreinitializeActionDictionaries(); SteamVR_Input.PreinitializeActionSetDictionaries();
                _set = null; _left = _right = null; _scroll = null;
            }
        }
    }
}
