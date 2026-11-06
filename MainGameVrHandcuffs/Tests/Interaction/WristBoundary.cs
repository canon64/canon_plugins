using UnityEngine;
using VRGIN.Controls;

public sealed class WristInput
{
    public readonly HashSet<string> Down = new(), Up = new(), Held = new();
    public void Clear() { Down.Clear(); Up.Clear(); }
}

namespace CanonPlugins.Shared
{
    internal static class PluginInput
    {
        internal static bool VrDown(string id, object input, string fallback) => ((WristInput)input).Down.Contains(fallback);
        internal static bool VrUp(string id, object input, string fallback) => !((WristInput)input).Held.Contains(fallback);
    }
}

namespace MainGirlHipHijack
{
    public sealed partial class Plugin
    {
        internal readonly Runtime.VrInteractionState _vrInteractionState = new();
        internal bool _vrGrabMode = true, _bodyCtrlLinkEnabled;
        int _vrLeftGrabIdx => _vrInteractionState.LeftGrabIndex;
        int _vrRightGrabIdx => _vrInteractionState.RightGrabIndex;
        Controller.Lock _vrGrabLockLeft { get=>_vrInteractionState.LeftGrabLock;set=>_vrInteractionState.LeftGrabLock=value; }
        Controller.Lock _vrGrabLockRight { get=>_vrInteractionState.RightGrabLock;set=>_vrInteractionState.RightGrabLock=value; }
        internal sealed class TestSettings
        {
            public float VRGrabDistance = .1f;
            public float VRElbowExtension = .2f;
            public bool[] Enabled = new bool[13];
            public float[] Weights = Enumerable.Range(0,13).Select(i=>i==0?0f:i==1?.6f:1f).ToArray();
        }
        internal TestSettings _settings = new();
        internal bool EnableFails, EnableThrows;
        internal int FollowRebinds;
        internal int SavedSettings;
        internal readonly RootMotion.FinalIK.IKConstraintBend[] Bends=Enumerable.Range(0,13).Select(_=>new RootMotion.FinalIK.IKConstraintBend()).ToArray();
        RootMotion.FinalIK.IKConstraintBend BIK_GetBendConstraint(int i)=>Bends[i];
        void SaveSettings() { SavedSettings++; }
        void SetBodyIKWeight(int i,float weight,bool saveSettings,bool updateSettings) { _bikWeight[i]=weight;Bends[i].weight=weight; }
        internal Transform NearestFollow;
        internal int NearestFollowQueries;
        static void Destroy(object value) { }
        void RecomputeGizmoDraggingState() { }
        void DoEnableBodyIK(int i)
        {
            if (EnableThrows) throw new Exception("injected enable failure");
            if (EnableFails) return;
            var s=_bikEff[i];s.Running=true;s.ProxyGo=new GameObject();s.Proxy=s.ProxyGo.transform;
            var bone=i==BIK_LE||i==BIK_RE?Bends[i].bone2:Effectors[i].bone;
            s.Proxy.SetPositionAndRotation(bone.position,bone.rotation);
            if(i==BIK_LE||i==BIK_RE){Bends[i].bendGoal=s.Proxy;Bends[i].weight=_bikWeight[i];}
            else {Effectors[i].target=s.Proxy;Effectors[i].positionWeight=Effectors[i].rotationWeight=_bikWeight[i];}
            _femaleSession.FullBodyIk.enabled=true;
        }
        void ClearBodyIKFollowBone(int i) { _bikEff[i].FollowBone=null;_bikEff[i].CandidateBone=null; }
        void ResetBodyIKPartToAnimationPose(int i,bool stopPoseTransition,bool logSnapshot)
        {
            ReleaseVRElbowForPart(i);ReleaseVRWristForPart(i);ClearBodyIKFollowBone(i);_bikWant[i]=false;_bikEff[i].Running=false;_bikEff[i].VrElbowFromWrist=false;
            Bends[i].weight=0;Bends[i].bendGoal=null;
            Effectors[i].positionWeight=Effectors[i].rotationWeight=0;Effectors[i].target=null;
        }
        Transform FindNearestBone(int i,Vector3 p) { NearestFollowQueries++;return NearestFollow; }
        bool TrySetNearestFollowBone(int i)
        { _bikEff[i].FollowBone=_bikEff[i].CandidateBone;_bikEff[i].HasPostDragHold=true;return _bikEff[i].FollowBone!=null; }
        void ArmBodyIKPostDragHold(int i,string reason) { _bikEff[i].HasPostDragHold=true; }
        void QueueBodyIKFollowRebind(int i,Transform bone,bool preset,Vector3 p,Quaternion q,string reason) { FollowRebinds++; }
        void ReleaseVRGrab(ref int index,int hand) { ReleaseVRWrist(ref index,hand); }
        public int Nearest(Vector3 position)=>FindNearestProxy(position);
        public void WristStep(int hand,string down=null,string up=null)
        {
            var controller=hand==0?VRGIN.Core.VR.Mode.Left:VRGIN.Core.VR.Mode.Right;
            var input=(WristInput)controller.Input;input.Clear();
            if(down!=null){input.Down.Add(down);input.Held.Add(down);}if(up!=null){input.Up.Add(up);input.Held.Remove(up);}
            ProcessVRWristResetInput();
            if(hand==0)ProcessVRWristGrab(controller,ref _vrInteractionState.LeftGrabIndex,0);
            else ProcessVRWristGrab(controller,ref _vrInteractionState.RightGrabIndex,1);
            input.Clear();
        }
        public int HeldWrist(int hand)=>hand==0?_vrLeftGrabIdx:_vrRightGrabIdx;
        public bool AwaitingGripUp(int hand)=>_vrInteractionState.WristResetUntilGripUp[hand];
        public void StopWristMode() { ReleaseBothVRGrabs();_vrGrabMode=false; }
        public void ConnectWristFocus()=>ConnectVRWristFocus();
        public void LoseWristFocus()=>_vrWristFocus.Handler(false);
        public void ElbowExtension(float meters)=>SetVRElbowExtension(meters);
        public int AimingElbow(int hand)=>_vrInteractionState.ElbowAims[hand].Index;
        public void DisableArmPart(int index)=>ResetBodyIKPartToAnimationPose(index,false,false);
        public void RefreshWristFeedback(int index)=>RefreshVRWristFollowCandidate(index);
    }
}
