using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine { public sealed class Transform{} public sealed class WaitForEndOfFrame{} public sealed class Camera{} }
namespace CanonGate
{
    public static class GateApi
    {
        public static int Generation=1;public static bool Allowed=true;
        public struct Permit{internal int Generation;public bool IsValid=>GateApi.Allowed&&Generation==GateApi.Generation;}
    }
}
namespace CanonPlugins.Shared
{
    public static class PluginSessionGate { public static CanonGate.GateApi.Permit Capture()=>new CanonGate.GateApi.Permit{Generation=CanonGate.GateApi.Generation}; }
}
namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        internal HLocationDocument _hLocations;
        internal HLocationPreset _hPlaceDraft;
        internal bool _hPlaceBusy,_sessionStarted=true,Available=true,OriginFailure;
        internal string _hPlaceStatus,HPlacePath,Key="map-a";
        private long _roomRestoreGeneration;
        internal bool _hotelRoomPlacementOwned;
        internal Transform Root=new Transform();
        internal MapWarpDocument _mapWarpDocument=new MapWarpDocument();
        private string _boundKey;private Transform _boundRoot;
        internal sealed class CameraFixture{public Camera thisCmaera=new Camera();}
        internal CameraFixture HPlaceCamera=new CameraFixture();
        internal readonly List<IEnumerator> Pending=new List<IEnumerator>();
        internal readonly List<string> Moves=new List<string>();
        internal readonly List<string> Events=new List<string>();
        internal int PoseRestoreRequests;
        private bool HPlaceContext(out string key,out Transform root){key=Key;root=Root;return Available;}
        private void BindHPlaceContext(string key,Transform root){if(_boundKey!=key||_boundRoot!=root){_hDefaultRequest.Reset();_boundKey=key;_boundRoot=root;}}
        private void PrepareMapWarpNavigation(string key,Transform root){
            if(OriginFailure)return;
            var next=MapWarpOrigin.CreateIfMissing(_mapWarpDocument,key,()=>new MapWarpPoint{X=1,Y=2,Z=3,Scale=1,Placement=new float[6]});
            if(next!=null)_mapWarpDocument=next;
        }
        private void StartCoroutine(IEnumerator work){if(work.MoveNext())Pending.Add(work);}
        private void RecallHPlace(string id,bool camera,bool automatic){Events.Add("before");Moves.Add(id);Events.Add("after");}
        internal void Ready()=>QueueDefaultHPlace();
        private void SaveHLocationDocument(HLocationDocument document)=>HLocationPresetStore.Save(HPlacePath,document);
        private void TraceHPlace(string stage,Func<string> details) { }
        private string HTraceText(string value)=>value;
        private string HPlaceRecord(HLocationPreset value)=>value?.Id;
        private string HPlaceActual(Transform root)=>"";
        private void QueueRoomCustomizationRestore() { PoseRestoreRequests++; }
        private void ReportPoseLocationFailure(string message) { _hPlaceStatus=message; }
        internal void SetDefault(string id,bool enabled)=>SetHPlaceDefault(id,enabled);
        internal void Manual()=>_hDefaultRequest.Cancel();
        internal void Stop(){_sessionStarted=false;_hDefaultRequest.Reset();}
        internal void Settle(){var work=Pending.ToArray();Pending.Clear();foreach(var routine in work){if(routine.MoveNext())throw new Exception("Unexpected polling coroutine");}}
    }
}
