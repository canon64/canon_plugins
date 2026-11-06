using System;
using System.Collections.Generic;
namespace MainGameHHandPoseProbe
{
    public sealed partial class Plugin
    {
        private readonly Dictionary<Tuple<ChaControl,int>, Action> _handRestore = new Dictionary<Tuple<ChaControl,int>, Action>();
        private void RememberHand(ChaControl target,int hand)
        {
            if(target==null)return;
            var key=Tuple.Create(target,hand);
            if(_handRestore.ContainsKey(key))return;
            int pattern=target.GetShapeHandIndex(hand,0); bool active=target.GetEnableShapeHand(hand);
            _handRestore.Add(key,()=>{if(target!=null){target.SetShapeHandValue(hand,pattern,0,0f);target.SetEnableShapeHand(hand,active);}});
        }
        private void RestoreSessionHands()
        {
            foreach(var restore in _handRestore.Values)try{restore();}catch(Exception ex){Logger.LogWarning(ex.Message);}
            _handRestore.Clear();
        }
    }
}
