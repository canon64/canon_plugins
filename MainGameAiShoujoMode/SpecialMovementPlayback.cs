using System;
using System.Collections;
using System.Linq;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    // Unity's native Animation/Animator advances the finite motion; completion is an event.
    public sealed class SpecialMovementPlayback : MonoBehaviour
    {
        private Animator _body;
        private RuntimeAnimatorController _controller;
        private AnimatorCullingMode _culling;
        private bool _events,_rootMotion;
        private float _speed;
        private Transform _actor,_parent;
        private Vector3 _position,_scale,_bodyPosition;
        private Quaternion _rotation,_bodyRotation;
        private Action<bool,Exception> _finished;
        private Coroutine _timeout;
        private AnimationClip _motion;
        private float _zero;
        private Action[] _restoreBones;
        public void Begin(Transform actor,Animator body,RuntimeAnimatorController controller,string state,float duration,
            Vector3 start,Quaternion rotation,SpecialMovementSegment[] segments,Parameters p,Action<bool,Exception> finished)
        {
            _actor=actor;_parent=actor.parent;_position=actor.localPosition;_rotation=actor.localRotation;_scale=actor.localScale;
            _body=body;_controller=body.runtimeAnimatorController;_culling=body.cullingMode;_events=body.fireEvents;_rootMotion=body.applyRootMotion;_speed=body.speed;
            _bodyPosition=body.transform.localPosition;_bodyRotation=body.transform.localRotation;_finished=finished;
            _zero=p.N("math.zero");
            _restoreBones=body.GetComponentsInChildren<Transform>(true).Select(bone=>
            {
                var bonePosition=bone.localPosition;var boneRotation=bone.localRotation;var boneScale=bone.localScale;
                return (Action)(()=>{if(bone!=null){bone.localPosition=bonePosition;bone.localRotation=boneRotation;bone.localScale=boneScale;}});
            }).ToArray();
            try
            {
                transform.SetPositionAndRotation(start,rotation);actor.SetParent(transform,false);actor.localPosition=Vector3.zero;actor.localRotation=Quaternion.identity;actor.localScale=_scale;
                body.runtimeAnimatorController=controller;body.cullingMode=AnimatorCullingMode.AlwaysAnimate;body.fireEvents=false;body.applyRootMotion=false;body.speed=p.N("traversal.speed");
                body.Rebind();int layer=p.I("math.zero");string full=body.GetLayerName(layer)+"."+state;
                if(!body.HasState(layer,Animator.StringToHash(full)))throw new InvalidOperationException("特殊移動stateがありません："+state);
                body.Play(full,layer,p.N("math.zero"));body.Update(p.N("math.zero"));
                float seconds=duration/p.N("traversal.speed");
                var routeKeys=SpecialMovementTimeline.Build(start,segments,seconds);
                _motion=new AnimationClip{legacy=true,name="SpecialMovementRoute",wrapMode=WrapMode.ClampForever};
                foreach(char axis in "xyz")
                {
                    Func<Vector3,float> value=v=>axis=='x'?v.x:axis=='y'?v.y:v.z;
                    var keys=routeKeys.Select(key=>new Keyframe(key.time,value(key.position))).ToList();
                    for(int index=p.I("math.zero");index<keys.Count;index+=p.I("math.one"))
                    {
                        var key=keys[index];
                        if(index>p.I("math.zero")){var before=keys[index-p.I("math.one")];key.inTangent=(key.value-before.value)/(key.time-before.time);}
                        if(index<keys.Count-p.I("math.one")){var after=keys[index+p.I("math.one")];key.outTangent=(after.value-key.value)/(after.time-key.time);}
                        keys[index]=key;
                    }
                    _motion.SetCurve("",typeof(Transform),"localPosition."+axis,new AnimationCurve(keys.ToArray()));
                }
                _motion.events=new[]{new AnimationEvent{time=seconds,functionName=nameof(OnSpecialMovementComplete)}};
                var animation=gameObject.AddComponent<Animation>();animation.cullingType=AnimationCullingType.AlwaysAnimate;animation.playAutomatically=false;animation.AddClip(_motion,_motion.name);animation.Play(_motion.name);
                _timeout=StartCoroutine(Deadline(seconds+p.N("traversal.deadlineGrace")));
            }
            catch(Exception ex){Finish(false,ex);}
        }
        public void OnSpecialMovementComplete(){Finish(true,null);}
        private IEnumerator Deadline(float seconds){yield return new WaitForSeconds(seconds);_timeout=null;Finish(false,new TimeoutException("特殊移動の終了通知を受信できませんでした。"));}
        private void Finish(bool success,Exception error)
        {
            var finished=_finished;if(finished==null)return;_finished=null;
            try
            {
            if(_timeout!=null){StopCoroutine(_timeout);_timeout=null;}
            var animation=GetComponent<Animation>();if(animation!=null)animation.Stop();
            if(_body!=null)
            {
                _body.runtimeAnimatorController=_controller;_body.cullingMode=_culling;_body.fireEvents=false;_body.applyRootMotion=_rootMotion;_body.speed=_speed;
                _body.transform.localPosition=_bodyPosition;_body.transform.localRotation=_bodyRotation;_body.Rebind();
                if(_restoreBones!=null)foreach(var restore in _restoreBones)restore();
                if(_controller!=null)_body.Update(_zero);
                _body.fireEvents=_events;
            }
            }
            catch(Exception ex){success=false;error=error??ex;}
            try{if(_actor!=null){_actor.SetParent(_parent,true);_actor.localScale=_scale;if(!success){_actor.localPosition=_position;_actor.localRotation=_rotation;}}}
            catch(Exception ex){success=false;error=error??ex;}
            try{finished(success,error);}finally{if(_motion!=null)Destroy(_motion);Destroy(gameObject);}
        }
        public void Cancel(){Finish(false,null);}
        private void OnDestroy(){if(_finished!=null)Finish(false,null);}
    }
}
