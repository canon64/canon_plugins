using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MainGameCandle
{
    public sealed partial class Plugin
    {
        readonly Dictionary<ChaControl,float> _nextReaction=new Dictionary<ChaControl,float>();
        readonly Dictionary<ChaControl,FaceReaction> _faces=new Dictionary<ChaControl,FaceReaction>();
        bool _bridgeResolved;
        Func<string,ChaControl,bool> _requestCue;
        Action<string,bool,bool> _cueEnabled;
        sealed class FaceReaction {internal int Eyes,Brows,Mouth;internal Coroutine Finish;}
        void ResolveBridge()
        {
            if(_bridgeResolved)return;_bridgeResolved=true;
            var type=Type.GetType("MainGameVoiceFaceEventBridge.PublicApi, MainGameVoiceFaceEventBridge",false);if(type==null)return;
            var request=type.GetMethod("TryRequestImpactCueForActor",new[]{typeof(string),typeof(ChaControl)});
            var enable=type.GetMethod("SetImpactCueEnabled",new[]{typeof(string),typeof(bool),typeof(bool)});
            if(request!=null&&enable!=null)
            {
                _requestCue=(Func<string,ChaControl,bool>)Delegate.CreateDelegate(typeof(Func<string,ChaControl,bool>),request);
                _cueEnabled=(Action<string,bool,bool>)Delegate.CreateDelegate(typeof(Action<string,bool,bool>),enable);
            }
        }
        void React(ChaControl actor)
        {
            if(!CanAct||!_focused||actor==null||(!_voice.Value&&!_expression.Value))return;
            float now=Time.unscaledTime;if(_nextReaction.TryGetValue(actor,out float next)&&now<next)return;
            _nextReaction[actor]=now+2;
            if(_voice.Value)
            {
                ResolveBridge();
                if(_requestCue!=null){_cueEnabled(Guid,true,false);_requestCue(Guid,actor);}
                else _status="蝋は付着しました。対象指定に対応した音声Bridgeが未導入です。";
            }
            if(_expression.Value)
            {
                RestoreFace(actor);
                var face=new FaceReaction{Eyes=actor.GetEyesPtn(),Brows=actor.GetEyebrowPtn(),Mouth=actor.GetMouthPtn()};_faces[actor]=face;
                actor.ChangeEyesPtn(1,true);actor.ChangeEyebrowPtn(1,true);actor.ChangeMouthPtn(2,true);
                face.Finish=StartCoroutine(FinishFace(actor,face));
            }
        }
        IEnumerator FinishFace(ChaControl actor,FaceReaction face)
        {
            yield return new WaitForSecondsRealtime(.8f);
            if(_faces.TryGetValue(actor,out var current)&&ReferenceEquals(current,face)){face.Finish=null;RestoreFace(actor);}
        }
        void RestoreFace(ChaControl actor)
        {
            if(!_faces.TryGetValue(actor,out var face))return;_faces.Remove(actor);
            if(face.Finish!=null)StopCoroutine(face.Finish);
            if(actor==null)return;
            // Restore only fields still owned by this reaction, never a later external expression.
            if(actor.GetEyesPtn()==1)actor.ChangeEyesPtn(face.Eyes,true);
            if(actor.GetEyebrowPtn()==1)actor.ChangeEyebrowPtn(face.Brows,true);
            if(actor.GetMouthPtn()==2)actor.ChangeMouthPtn(face.Mouth,true);
        }
        void ClearExpressions(){foreach(var actor in new List<ChaControl>(_faces.Keys))RestoreFace(actor);}
        void StopReactionVoice(){_cueEnabled?.Invoke(Guid,false,true);}
        void ClearReactions(){try{ClearExpressions();}finally{_nextReaction.Clear();StopReactionVoice();}}
    }
}
