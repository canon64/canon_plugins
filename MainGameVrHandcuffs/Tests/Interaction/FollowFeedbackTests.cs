using UnityEngine;
using VRGIN.Core;

static partial class Program
{
    static void TestWristFollowFeedback()
    {
        foreach(int hand in new[]{0,1})
        {
            var hip=NewWristTest();var controller=hand==0?VR.Mode.Left:VR.Mode.Right;
            controller.transform.position=hip.Effectors[hand].bone.position;
            hip.WristStep(hand,"Grip");var state=hip._bikEff[hand];
            state.VRMarkerGo=new GameObject();state.VRMarkerRend=new Renderer();state.Gizmo=new();
            var visibleCandidate=new Transform();hip.NearestFollow=visibleCandidate;
            hip.RefreshWristFeedback(hand);
            Check(state.CandidateBone==visibleCandidate,"held wrist supplies candidate for existing bone marker and follow line");
            Check(state.VRMarkerGo.Active && state.Gizmo.GrabState==1 && state.VRMarkerRend.sharedMaterial.color.r==.8f && state.VRMarkerRend.sharedMaterial.color.b==1f,"candidate turns held marker purple and highlights gizmo");
            hip.NearestFollow=null;hip.RefreshWristFeedback(hand);
            Check(state.CandidateBone==null && state.Gizmo.GrabState==2 && state.VRMarkerRend.sharedMaterial.color.r==1 && state.VRMarkerRend.sharedMaterial.color.g==1 && state.VRMarkerRend.sharedMaterial.color.b==0,"leaving candidate range clears preview and restores yellow");
            hip.NearestFollow=visibleCandidate;hip.RefreshWristFeedback(hand);
            hip.NearestFollow=new Transform();int queries=hip.NearestFollowQueries;
            hip.WristStep(hand,"Trigger");
            Check(state.FollowBone==visibleCandidate && hip.NearestFollowQueries==queries,"trigger binds displayed candidate rather than silently selecting a different bone");
            hip.RefreshWristFeedback(hand);
            Check(state.CandidateBone==null && state.Gizmo.GrabState==2 && hip.NearestFollowQueries==queries,"confirmed follow clears candidate and does not perform preview search");
            hip.WristStep(hand,up:"Grip");hip.RefreshWristFeedback(hand);
            Check(state.CandidateBone==null && !state.VRMarkerGo.Active && hip.NearestFollowQueries==queries,"release clears preview and stops candidate queries");
            state.ExternalOwner=true;hip.RefreshWristFeedback(hand);
            Check(state.CandidateBone==null && hip.NearestFollowQueries==queries,"external wrist ownership never generates ordinary follow preview");
        }
    }
}
