using System;
using System.Collections;
using System.IO;
using System.Linq;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    public sealed class PodCharacterAnimation : MonoBehaviour
    {
        public sealed class ClipInfo { public string name,source; public float duration; }
        public sealed class Report { public string controller; public ClipInfo[] clips; public bool nativeSamplingPassed; }
        private Animator _body;
        private Transform _stage,_hips;
        private Vector3 _previousLocalPosition;
        private Quaternion _previousLocalRotation;
        private RuntimeAnimatorController _previousController;
        private AssetBundle _bundle;
        private AnimatorCullingMode _previousCulling;
        private bool _previousEvents,_previousRootMotion;
        private float _previousSpeed;
        private Parameters _settings;
        private Action _completed;
        private Action<Exception> _failed;
        private string _inState,_outState;
        private bool _playing,_out,_finishing;
        private Coroutine _timeout,_finish;
        public bool Playing => _playing;
        public void Begin(Animator body,Transform stage,string content,Parameters settings,Action completed,Action<Exception> failed)
        {
            if(_playing)throw new InvalidOperationException("カプセル登場アニメは再生中です。");
            if(body==null||body.gameObject!=gameObject)throw new InvalidOperationException("演技コンポーネントは女性Animatorと同じ物体へ追加してください。");
            _settings=settings;_completed=completed;_failed=failed;
            try
            {
                var report=JsonCodec.FromJson<Report>(File.ReadAllText(Path.Combine(content,"pod-animation-application.json")));
                if(report?.clips==null||!report.nativeSamplingPassed)throw new InvalidDataException("カプセル演技のnative変換検証が未完了です。");
                _bundle=AssetBundle.LoadFromFile(Path.Combine(content,settings.T("podAnimation.bundle")));
                if(_bundle==null)throw new InvalidDataException("カプセル演技bundleを読めません。");
                var controller=_bundle.LoadAsset<RuntimeAnimatorController>(report.controller);
                if(controller==null)throw new InvalidDataException("カプセル演技controllerを読めません。");
                string sequence=settings.T("podAnimation.sequence");_inState=sequence+"_M";_outState=sequence+"_out_M";
                var enter=report.clips.Single(clip=>clip.name==_inState);var exit=report.clips.Single(clip=>clip.name==_outState);
                if(settings.N("animation.speed")<=settings.N("math.zero"))throw new InvalidDataException("animation.speedは出現演技中に正である必要があります。");
                if(stage==null||!body.transform.IsChildOf(stage))throw new InvalidOperationException("女性の所有ルートが一致しません。");
                var hips=body.GetComponentsInChildren<Transform>(true).Single(bone=>bone.name==settings.T("animation.hips.target"));
                _body=body;_stage=stage;_hips=hips;
                _previousLocalPosition=body.transform.localPosition;_previousLocalRotation=body.transform.localRotation;
                _previousController=body.runtimeAnimatorController;_previousCulling=body.cullingMode;_previousEvents=body.fireEvents;_previousRootMotion=body.applyRootMotion;_previousSpeed=body.speed;
                body.runtimeAnimatorController=controller;body.fireEvents=true;body.applyRootMotion=false;body.cullingMode=AnimatorCullingMode.AlwaysAnimate;body.speed=settings.N("animation.speed");body.Rebind();
                _playing=true;_out=false;_finishing=false;Play(_inState);
                _timeout=StartCoroutine(Deadline((enter.duration+exit.duration)/body.speed+settings.N("podAnimation.deadlineGraceSeconds")));
            }
            catch(Exception ex){Cancel();failed?.Invoke(ex);}
        }
        private void Play(string state)
        {
            int layer=_settings.I("math.zero");string full=_body.GetLayerName(layer)+"."+state;
            if(!_body.HasState(layer,Animator.StringToHash(full)))throw new InvalidDataException("カプセル演技stateがありません："+full);
            _body.Play(full,layer,_settings.N("math.zero"));
        }
        public void OnPodClipComplete(string state)
        {
            if(!_playing)return;
            var failure=_failed;
            try
            {
                if(!_out&&state==_inState){_out=true;Play(_outState);return;}
                if(_out&&state==_outState&&!_finishing){_finishing=true;_finish=StartCoroutine(FinishAfterFrame());}
            }
            catch(Exception ex){Cancel();failure?.Invoke(ex);}
        }
        private IEnumerator FinishAfterFrame()
        {
            yield return new WaitForEndOfFrame();_finish=null;
            var done=_completed;var failure=_failed;var stage=_stage;var hips=_hips;var body=_body;
            try
            {
                Vector3 endHips=hips.position;Quaternion endRotation=body.transform.rotation;
                Cancel();
                body.Rebind();int layer=_settings.I("math.zero");string idle=body.GetLayerName(layer)+".Idle";
                if(body.HasState(layer,Animator.StringToHash(idle)))body.Play(idle,layer,_settings.N("math.zero"));
                body.Update(_settings.N("math.zero"));
                stage.rotation=endRotation*Quaternion.Inverse(body.transform.rotation)*stage.rotation;
                stage.position+=endHips-hips.position;
                done?.Invoke();
            }
            catch(Exception ex){Cancel();failure?.Invoke(ex);}
        }
        private IEnumerator Deadline(float duration)
        {
            yield return new WaitForSeconds(duration);_timeout=null;
            if(!_playing)yield break;
            var failure=_failed;Cancel();failure?.Invoke(new InvalidOperationException("カプセル演技の終了イベントを受信できませんでした。"));
        }
        public void Cancel()
        {
            _playing=false;
            if(_finish!=null){StopCoroutine(_finish);_finish=null;}
            if(_timeout!=null){StopCoroutine(_timeout);_timeout=null;}
            if(_body!=null){_body.runtimeAnimatorController=_previousController;_body.fireEvents=_previousEvents;_body.applyRootMotion=_previousRootMotion;_body.cullingMode=_previousCulling;_body.speed=_previousSpeed;_body.transform.localPosition=_previousLocalPosition;_body.transform.localRotation=_previousLocalRotation;}
            _body=null;_completed=null;_failed=null;
            if(_bundle!=null){_bundle.Unload(true);_bundle=null;}
        }
        private void OnDestroy(){Cancel();}
    }
}
