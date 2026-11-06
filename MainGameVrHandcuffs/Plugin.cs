using System;
using BepInEx;
using CanonGate;
using UnityEngine;
using VRGIN.Core;
using MainGameTransformGizmo;
using CanonPlugins.Shared;

namespace MainGameVrHandcuffs
{
    [BepInPlugin(Guid,"MainGameVrHandcuffs","0.1.1")]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    [BepInDependency(GateApi.PluginGuid)]
    [BepInDependency(MainGirlHipHijack.Plugin.Guid)]
    [BepInDependency(MainGameTransformGizmo.Plugin.GUID)]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string Guid="com.kks.maingame.vrhandcuffs";
        private IDisposable _registration;
        private GateApi.Permit _permit;
        private bool _active;
        private ulong _lastSession;
        private GameObject _rig;
        private readonly Transform[] _parts=new Transform[5];
        private readonly Transform[] _chainEyes=new Transform[2];
        private readonly Transform[] _wristTargets=new Transform[2];
        private readonly MainGirlHipHijack.Plugin.RuntimeWristBinding[] _bindings=new MainGirlHipHijack.Plugin.RuntimeWristBinding[2];
        private readonly HandcuffState _state=new HandcuffState();
        private readonly GadgetBarCycle _cycle=new GadgetBarCycle();
        private TransformGizmo _gizmo;
        private bool CanAct => _active && isActiveAndEnabled && _permit.IsValid;
        public string LastResult { get; private set; }

        private void Awake() { _registration=GateApi.Register(Guid,StartSession,StopSession); }
        private void StartSession()
        {
            var context=GateApi.Current;
            if (!isActiveAndEnabled || context==null || context.Kind!=GateApi.SessionKind.H
                || context.Scene==null || context.Id==_lastSession) return;
            _lastSession=context.Id; _permit=GateApi.Capture(Guid);
            if (!_permit.IsValid) return;
            _active=true;
            try { ConnectInput(); } catch { StopSession(); throw; }
        }
        private void StopSession()
        {
            _active=false; _permit=default(GateApi.Permit);
            try { DisconnectInput(); } finally { DeleteRig(); }
        }
        private void OnDisable() { StopSession(); }
        private void OnDestroy()
        {
            try { _registration?.Dispose(); }
            finally { _registration=null; StopSession(); }
        }
        private void OnApplicationFocus(bool focused) { if (!focused && !VR.Active) ReleaseGrab(); }

        public string GetPlaybackBarActionHint() =>
            "左クリック：初回だけ手錠を視点前に呼び出し。その後はギズモ表示／非表示を切り替え。\n右クリック：削除・手首IKの元の状態を復元。\nVR：AまたはBをグリップで掴み、手首の5cm以内でトリガーを押すと装着。掴んだ手のBでその側を外す。離すと手首ごと空間固定。"+
            (string.IsNullOrEmpty(LastResult)?"":"\n"+LastResult);
        public void PlaybackBarPrimaryAction()
        {
            if (!CanAct || !Application.isFocused) return;
            _cycle.Press(_rig!=null,Recall,ShowGizmo,DetachGizmo,_gizmo!=null&&_gizmo.IsVisible);
        }
        public void PlaybackBarDelete() { DeleteRig(); }
        private bool Recall()
        {
            var view=VR.Active && VR.Camera!=null ? VR.Camera.Head : Camera.main!=null ? Camera.main.transform : null;
            if (view==null) { LastResult="配置用の視点がありません。"; return false; }
            // Explicit recall ends old wrist bindings rather than teleporting the character.
            DeleteRig();
            try
            {
                CreateRig();
                _rig.transform.SetPositionAndRotation(view.position+view.forward*.45f,view.rotation);
                _chainBend=_rig.transform.forward;
                LastResult="グリップ：掴む／トリガー：掴んでいる側を手首へ装着";
                return true;
            }
            catch { DeleteRig(); throw; }
        }
        private bool ShowGizmo()
        {
            if (_rig==null || !CanAct) return false;
            ReleaseGrab(); DetachGizmo();
            _gizmo=TransformGizmoApi.Attach(_rig);
            if (_gizmo==null) return false;
            TransformGizmoApi.EnableCameraInputCaptureOnDrag(_gizmo);
            _gizmo.SetMode(GizmoMode.Move); _gizmo.SetVisible(true);
            _gizmo.ModeChanged+=GizmoModeChanged;
            return true;
        }
        private void GizmoModeChanged(GizmoMode mode) { if (mode==GizmoMode.Scale) _gizmo?.SetMode(GizmoMode.Move); }
        private void DetachGizmo()
        {
            if (_gizmo!=null)
            {
                _gizmo.ModeChanged-=GizmoModeChanged;
                TransformGizmoApi.DisableCameraInputCaptureOnDrag(_gizmo);
                if (_rig!=null) TransformGizmoApi.Detach(_rig);
            }
            _gizmo=null;
        }
        private void DeleteRig()
        {
            Exception failure=null;
            try { ReleaseGrab(); } catch (Exception ex) { failure=ex; }
            try { FinishPendingLocks(); } catch (Exception ex) { if (failure==null) failure=ex; }
            try { DetachGizmo(); } catch (Exception ex) { if (failure==null) failure=ex; }
            try { DestroyAttachmentAudio(); } catch (Exception ex) { if (failure==null) failure=ex; }
            for (int i=0;i<2;i++)
            {
                var binding=_bindings[i]; _bindings[i]=null;
                try
                {
                    if (binding!=null) { binding.Invalidated-=BindingInvalidated; binding.Dispose(); }
                }
                catch (Exception ex) { if (failure==null) failure=ex; }
                finally { _wristTargets[i]=null; }
            }
            if (_rig!=null) { _rig.SetActive(false); Destroy(_rig); }
            _rig=null;
            for (int i=0;i<_parts.Length;i++) _parts[i]=null;
            _chainEyes[0]=_chainEyes[1]=null;
            DestroyModelResources(); _state.Reset(); _cycle.Reset();
            if (failure!=null) throw failure;
        }
        private void BindingInvalidated() { DeleteRig(); }
    }
}
