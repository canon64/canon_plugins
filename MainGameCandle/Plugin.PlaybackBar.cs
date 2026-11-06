using System;
using CanonPlugins.Shared;
using MainGameTransformGizmo;
using UnityEngine;
using VRGIN.Core;

namespace MainGameCandle
{
    public sealed partial class Plugin
    {
        CandleModel _model;
        CandleLoop _loop;
        TransformGizmo _gizmo;
        readonly GadgetBarCycle _barCycle = new GadgetBarCycle();
        readonly CandleFlow _flow = new CandleFlow();
        public void PlaybackBarPrimaryAction()
        {
            if(!CanAct)
            {
                if(!_enabled.Value)_status="ロウソクが無効です。設定を有効にして次のHを開始してください。";
                else if(_active&&!_permit.IsValid)_status="このHの実行許可が失効しています。";
                else if(!_active&&!_status.StartsWith("開始失敗：",StringComparison.Ordinal))_status="許可されたHシーンの開始待ちです。";
                return;
            }
            if(!_focused){_status="ゲームまたはVRの入力フォーカスがないため、呼び出しを保留しました。";return;}
            try { _barCycle.Press(_model!=null,Recall,ShowGizmo,HideGizmo,_gizmo!=null&&_gizmo.IsVisible); }
            catch(Exception ex) { DeleteCandle(); _status="ロウソク操作失敗："+ex.Message; }
        }
        bool Recall()
        {
            var view=VR.Active&&VR.Camera!=null?VR.Camera.Head:Camera.main!=null?Camera.main.transform:null;
            if(view==null)throw new InvalidOperationException("視点カメラがありません");
            HideGizmo();ReleaseHand();ClearFlying();
            if(_model==null)_model=new CandleModel(_scene.transform);
            _model.SetWickLength(_wickHeight.Value*.001f);
            _model.Root.transform.SetPositionAndRotation(view.position+view.forward*.5f,Quaternion.identity);
            RefreshSurfaces();ApplyLight();SyncLoop();_status="傾けるほど蝋が多く垂れます。VR：近くでグリップして保持。";return true;
        }
        bool ShowGizmo()
        {
            ReleaseHand();
            if(_model==null)return false;
            if(_gizmo==null)_gizmo=TransformGizmoApi.Attach(_model.Root);
            if(_gizmo==null)throw new InvalidOperationException("共通ギズモを利用できません");
            _gizmo.enabled=true;
            if(!TransformGizmoApi.EnableCameraInputCaptureOnDrag(_gizmo))throw new InvalidOperationException("ギズモ入力保護を開始できません");
            TransformGizmoApi.SetSizeMultiplier(_gizmo,.35f);_gizmo.SetMode(GizmoMode.Rotate);_gizmo.SetVisible(true);
            _status="ギズモで傾けて滴下できます。次の左クリックでギズモを非表示にします。";return _gizmo.IsVisible;
        }
        void HideGizmo()
        {
            if(_gizmo==null)return;
            try{_gizmo.SetVisible(false);}finally{TransformGizmoApi.DisableCameraInputCaptureOnDrag(_gizmo);_gizmo.enabled=false;}
        }
        public void PlaybackBarDelete(){DeleteCandle();_status="ロウソクを削除しました。付着した蝋は残ります。";}
        public string GetPlaybackBarActionHint()=>"左クリック：初回だけ目の前へ呼び出し→以後ギズモ表示／非表示\n右クリック：ロウソク削除（付着した蝋は残す）\n初期値：傾き45度超から滴下、水平で平均約1.5滴/秒。傾けるほど頻度が上がり、135度以上で消火。\n蝋は肌だけに付着。設定で頻度・一粒の量・ばらつき・芯と炎の高さを調整できます。\n再点火：135度未満に戻し、保持中の手のトリガー。\n次の左クリック："+(_model==null?"呼び出し":_gizmo!=null&&_gizmo.IsVisible?"ギズモ非表示":"ギズモ表示")+"\n状態："+_status;
        void DeleteCandle()
        {
            Cleanup(ReleaseHand);Cleanup(HideGizmo);Cleanup(ClearFlying);_flow.Reset();
            if(_loop!=null){_loop.Owner=null;_loop.enabled=false;Destroy(_loop);}_loop=null;
            _gizmo=null;_barCycle.Reset();var model=_model;_model=null;if(model!=null)Cleanup(model.Dispose);
            Cleanup(ClearReactions);Cleanup(FinishRetiringLocks);Cleanup(ClearSurfaces);
        }
        void ApplyLight(){_model?.SetLit(CanAct&&_lit.Value&&_focused);}
        void SyncLoop()
        {
            bool run=CanAct&&_focused&&_lit.Value&&_model!=null;
            if(run&&_loop==null){_loop=_model.Root.AddComponent<CandleLoop>();_loop.Owner=this;}
            if(_loop!=null)_loop.enabled=run;
            if(!run)_flow.Reset();
        }
        internal void TickCandle(float dt)
        {
            if(!CanAct||!_focused||!_lit.Value||_model==null){ClearFlying();ApplyLight();SyncLoop();return;}
            // Explicit user-approved exception: only the visible, lit candle owns this loop.
            float angle=Vector3.Angle(_model.Root.transform.up,Vector3.up);
            if(angle>=CandleFlow.ExtinguishAngle)
            {
                _lit.Value=false;_status="135度以上で消火。135度未満に戻し、持っている手のトリガーで再点火できます。";return;
            }
            _flow.Advance(dt,CandleFlow.Rate(angle,_startAngle.Value,_flowMultiplier.Value*.25),volume=>SpawnDrop((float)volume),_variation.Value/100,_dropVolume.Value);
            try { AdvanceFlying(Mathf.Min(dt,.25f)); }
            catch(Exception ex){ClearFlying();if(_loop!=null)_loop.enabled=false;_status="滴下を停止しました："+ex.Message;}
        }
    }
    internal sealed class CandleLoop:MonoBehaviour
    {
        internal Plugin Owner;
        void Update(){Owner?.TickCandle(Time.deltaTime);}
    }
}
