using System;
using System.Linq;
using Valve.VR;
namespace MainGameMagicPaint
{
    // Real A/B/thumbstick clicks; the installed legacy Axis0_Press also aliases B and cannot be used.
    internal sealed class PaintButtons : IDisposable
    {
        SteamVR_ActionSet _set;
        SteamVR_Action_Boolean _setting, _clear, _menu;
        internal SteamVR_Action_Vector2 Axis { get; private set; }
        readonly Action<SteamVR_Input_Sources> _onSetting, _onClear, _onMenuDown, _onMenuUp;
        static readonly SteamVR_Input_Sources[] Hands = { SteamVR_Input_Sources.LeftHand, SteamVR_Input_Sources.RightHand };
        internal PaintButtons(Action<SteamVR_Input_Sources> setting, Action<SteamVR_Input_Sources> clear, Action<SteamVR_Input_Sources> down, Action<SteamVR_Input_Sources> up)
        { _onSetting = setting; _onClear = clear; _onMenuDown = down; _onMenuUp = up; }
        internal void Connect()
        {
            if (_set != null) return;
            try
            {
                _set = SteamVR_ActionSet.Create<SteamVR_ActionSet>("/actions/magicpaint");
                SteamVR_Input.actionSets = Add(SteamVR_Input.actionSets, _set); SteamVR_Input.PreinitializeActionSetDictionaries();
                _setting = SteamVR_Action.Create<SteamVR_Action_Boolean>("/actions/magicpaint/in/setting");
                _clear = SteamVR_Action.Create<SteamVR_Action_Boolean>("/actions/magicpaint/in/clear");
                _menu = SteamVR_Action.Create<SteamVR_Action_Boolean>("/actions/magicpaint/in/menu");
                Axis = SteamVR_Action.Create<SteamVR_Action_Vector2>("/actions/magicpaint/in/stick");
                SteamVR_Input.actions = Add<SteamVR_Action>(SteamVR_Input.actions, _setting, _clear, _menu, Axis);
                SteamVR_Input.actionsIn = Add<ISteamVR_Action_In>(SteamVR_Input.actionsIn, _setting, _clear, _menu, Axis);
                SteamVR_Input.actionsBoolean = Add(SteamVR_Input.actionsBoolean, _setting, _clear, _menu);
                SteamVR_Input.actionsVector2 = Add(SteamVR_Input.actionsVector2, Axis);
                SteamVR_Input.actionsNonPoseNonSkeletonIn = Add<ISteamVR_Action_In>(SteamVR_Input.actionsNonPoseNonSkeletonIn, _setting, _clear, _menu, Axis);
                SteamVR_Input.PreinitializeActionDictionaries(); _set.FinishPreInitialize();
                _set.Initialize(true); _setting.Initialize(true); _clear.Initialize(true); _menu.Initialize(true); Axis.Initialize(true);
                if (_set.handle == 0 || _setting.handle == 0 || _clear.handle == 0 || _menu.handle == 0 || Axis.handle == 0) throw new InvalidOperationException("マジック用SteamVR入力設定を配置し、ゲームを再起動してください。");
                foreach (var hand in Hands)
                {
                    _setting.AddOnStateDownListener(Setting, hand); _clear.AddOnStateDownListener(Clear, hand);
                    _menu.AddOnStateDownListener(MenuDown, hand); _menu.AddOnStateUpListener(MenuUp, hand);
                }
            }
            catch { Dispose(); throw; }
        }
        internal void Activate(SteamVR_Input_Sources source, bool active)
        { if (_set == null) return; if (active) _set.Activate(source, 110, false); else _set.Deactivate(source); }
        void Setting(SteamVR_Action_Boolean a, SteamVR_Input_Sources s) => _onSetting(s);
        void Clear(SteamVR_Action_Boolean a, SteamVR_Input_Sources s) => _onClear(s);
        void MenuDown(SteamVR_Action_Boolean a, SteamVR_Input_Sources s) => _onMenuDown(s);
        void MenuUp(SteamVR_Action_Boolean a, SteamVR_Input_Sources s) => _onMenuUp(s);
        static T[] Add<T>(T[] a, params T[] b) => (a ?? new T[0]).Concat(b).ToArray();
        T[] Remove<T>(T[] a) => (a ?? new T[0]).Where(x => !ReferenceEquals(x, _setting) && !ReferenceEquals(x, _clear) && !ReferenceEquals(x, _menu) && !ReferenceEquals(x, Axis) && !ReferenceEquals(x, _set)).ToArray();
        public void Dispose()
        {
            if (_set == null) return;
            try
            {
                foreach (var hand in Hands)
                {
                    Activate(hand, false); _setting?.RemoveOnStateDownListener(Setting, hand); _clear?.RemoveOnStateDownListener(Clear, hand);
                    _menu?.RemoveOnStateDownListener(MenuDown, hand); _menu?.RemoveOnStateUpListener(MenuUp, hand);
                }
            }
            finally
            {
                SteamVR_Input.actions = Remove(SteamVR_Input.actions); SteamVR_Input.actionsIn = Remove(SteamVR_Input.actionsIn);
                SteamVR_Input.actionsBoolean = Remove(SteamVR_Input.actionsBoolean); SteamVR_Input.actionsNonPoseNonSkeletonIn = Remove(SteamVR_Input.actionsNonPoseNonSkeletonIn);
                SteamVR_Input.actionsVector2 = Remove(SteamVR_Input.actionsVector2);
                SteamVR_Input.actionSets = Remove(SteamVR_Input.actionSets); SteamVR_Input.PreinitializeActionDictionaries(); SteamVR_Input.PreinitializeActionSetDictionaries();
                _set = null; _setting = _clear = _menu = null; Axis = null;
            }
        }
    }
}
