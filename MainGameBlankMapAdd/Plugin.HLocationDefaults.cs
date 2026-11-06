using System;
using System.Collections;
using CanonPlugins.Shared;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private readonly HLocationDefaultRequest _hDefaultRequest=new HLocationDefaultRequest();
        private void SetHPlaceDefault(string id,bool enabled)
        {
            _hDefaultRequest.Cancel();++_roomRestoreGeneration;
            if(_hPlaceBusy||!HPlaceContext(out string key,out _)){TraceHPlace("default-save.skipped",()=>"id="+id+" busy="+_hPlaceBusy+" reason=busy-or-context-unavailable");return;}
            try{
                var document=_hLocations.WithDefault(key,id,enabled);
                SaveHLocationDocument(document);_hLocations=document;
                _hPlaceStatus=enabled?"次の入室・体位変更から、このH場所をデフォルト位置に使います。":"デフォルト指定を解除しました。";
            }catch(Exception ex){TraceHPlace("default-save.failed",()=>"id="+id+" reason="+ex);ReportPoseLocationFailure("デフォルトを保存できません："+ex.Message);}
        }
        private void QueueDefaultHPlace()
        {
            // Only lifecycle callbacks may capture the native start, before any automatic move.
            QueueRoomCustomizationRestore();
        }
    }
}
