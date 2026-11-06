using System;
using System.Linq;
using Valve.VR;

namespace MainGameIPhoneCamera
{
    // Dedicated native SteamVR actions distinguish physical A/B from the stock stick-click alias.
    // The existing SteamVR input dispatcher samples these actions; this plugin adds no Update hook.
    internal sealed class PhoneButtonInput : IDisposable
    {
        private SteamVR_ActionSet _set;
        private SteamVR_Action_Boolean _confirm, _back;
        private readonly Action _onConfirm, _onBack;
        internal PhoneButtonInput(Action confirm, Action back) { _onConfirm = confirm; _onBack = back; }
        internal void Connect()
        {
            if (_set != null) return;
            if (SteamVR_Input.actionSets == null || SteamVR_Input.actions == null) throw new InvalidOperationException("SteamVR入力が初期化されていません");
            try
            {
                _set = SteamVR_ActionSet.Create<SteamVR_ActionSet>("/actions/iphone");
                SteamVR_Input.actionSets = Add(SteamVR_Input.actionSets, _set);
                SteamVR_Input.PreinitializeActionSetDictionaries();
                _confirm = SteamVR_Action.Create<SteamVR_Action_Boolean>("/actions/iphone/in/confirm");
                _back = SteamVR_Action.Create<SteamVR_Action_Boolean>("/actions/iphone/in/back");
                SteamVR_Input.actions = Add(SteamVR_Input.actions, _confirm, _back);
                SteamVR_Input.actionsIn = Add<ISteamVR_Action_In>(SteamVR_Input.actionsIn, _confirm, _back);
                SteamVR_Input.actionsBoolean = Add(SteamVR_Input.actionsBoolean, _confirm, _back);
                SteamVR_Input.actionsNonPoseNonSkeletonIn = Add<ISteamVR_Action_In>(SteamVR_Input.actionsNonPoseNonSkeletonIn, _confirm, _back);
                SteamVR_Input.PreinitializeActionDictionaries(); _set.FinishPreInitialize();
                _set.Initialize(true); _confirm.Initialize(true); _back.Initialize(true);
                if (_set.handle == 0 || _confirm.handle == 0 || _back.handle == 0)
                    throw new InvalidOperationException("iPhone専用A/B入力が未導入です。入力設定を配置後、ゲームを再起動してください。");
                _confirm.AddOnStateDownListener(ConfirmDown, SteamVR_Input_Sources.RightHand);
                _back.AddOnStateDownListener(BackDown, SteamVR_Input_Sources.RightHand);
            }
            catch { Dispose(); throw; }
        }
        internal void Active(bool active)
        {
            if (_set == null) return;
            if (active) _set.Activate(SteamVR_Input_Sources.RightHand, 100, false);
            else _set.Deactivate(SteamVR_Input_Sources.RightHand);
        }
        private void ConfirmDown(SteamVR_Action_Boolean action, SteamVR_Input_Sources source) { if (source == SteamVR_Input_Sources.RightHand) _onConfirm(); }
        private void BackDown(SteamVR_Action_Boolean action, SteamVR_Input_Sources source) { if (source == SteamVR_Input_Sources.RightHand) _onBack(); }
        private static T[] Add<T>(T[] values, params T[] additions) => (values ?? new T[0]).Concat(additions).ToArray();
        private static T[] Remove<T>(T[] values, object a, object b = null) => (values ?? new T[0]).Where(x => !ReferenceEquals(x, a) && !ReferenceEquals(x, b)).ToArray();
        public void Dispose()
        {
            if (_set == null) return;
            try
            {
                Active(false);
                _confirm?.RemoveOnStateDownListener(ConfirmDown, SteamVR_Input_Sources.RightHand);
                _back?.RemoveOnStateDownListener(BackDown, SteamVR_Input_Sources.RightHand);
            }
            finally
            {
                SteamVR_Input.actionsNonPoseNonSkeletonIn = Remove(SteamVR_Input.actionsNonPoseNonSkeletonIn, _confirm, _back);
                SteamVR_Input.actionsBoolean = Remove(SteamVR_Input.actionsBoolean, _confirm, _back);
                SteamVR_Input.actionsIn = Remove(SteamVR_Input.actionsIn, _confirm, _back);
                SteamVR_Input.actions = Remove(SteamVR_Input.actions, _confirm, _back);
                SteamVR_Input.actionSets = Remove(SteamVR_Input.actionSets, _set);
                SteamVR_Input.PreinitializeActionDictionaries(); SteamVR_Input.PreinitializeActionSetDictionaries();
                _confirm = _back = null; _set = null;
            }
        }
    }
}
