using System.Numerics;
using RootMotion.FinalIK;
using U = UnityEngine;
using Hip = MainGirlHipHijack.Plugin;

static partial class Program
{
    static void ChainTargets()
    {
        var directions=new[]{Vector3.UnitX,-Vector3.UnitX,Vector3.UnitY,-Vector3.UnitY,
            Vector3.UnitZ,-Vector3.UnitZ,Vector3.Normalize(new Vector3(1,1,1))};
        foreach(bool hasTarget in new[]{false,true})
        foreach(float originalWeight in new[]{0f,.4f,1f})
        foreach(var stale in new[]{Vector3.Zero,new Vector3(20,-30,40)})
        foreach(var direction in directions)
        {
            var rig=new Rig(); var hip=new Hip(); hip.Setup(rig.Ik);
            var effectors=new[]{rig.Ik.solver.leftShoulderEffector,rig.Ik.solver.rightShoulderEffector};
            var starts=effectors.Select(e=>e.bone.position.n).ToArray();
            var targets=new U.Transform[2];
            for(int side=0;side<2;side++)
            {
                var e=effectors[side]; e.position=new U.Vector3(stale);
                e.positionWeight=originalWeight; e.rotationWeight=.3f;
                e.rotation=new U.Quaternion(Axis(Vector3.UnitZ,12));
                if(hasTarget) { targets[side]=new U.GameObject().transform; targets[side].position=new U.Vector3(stale); }
                e.target=targets[side];
            }
            rig.Ik.enabled=false; rig.Ik.solver.IKPositionWeight=.65f;
            hip.StartChain();
            var offset=direction*.5f;
            // Includes the zero-weight endpoints, both transition directions and a reversal.
            foreach(float t in new[]{0f,.01f,.25f,.5f,.75f,1f,.75f,.5f,.6f,.25f,.01f,0f})
            {
                if(hasTarget) foreach(var target in targets) target.position=new U.Vector3(stale+new Vector3(.1f,.2f,.3f));
                hip.ChainTarget(t,new U.Vector3(offset));
                for(int side=0;side<2;side++)
                {
                    var e=effectors[side];
                    float prior=(1-t)*originalWeight, total=prior+t;
                    var desired=starts[side]+offset;
                    var original=hasTarget?targets[side].position.n:stale;
                    var expected=total>0?(original*prior+desired*t)/total:desired;
                    Near(e.positionWeight,total,.000001f,"effective shoulder weight");
                    Near(Vector3.Distance(e.target.position.n,expected),0,.00002f,"contribution-weighted shoulder target");
                    if(originalWeight==0)
                        Near(Vector3.Distance(e.target.position.n,desired),0,.000001f,"inactive stale coordinates never enter target");
                }
            }
            hip.Clear();
            for(int side=0;side<2;side++)
            {
                var e=effectors[side];
                Check(ReferenceEquals(e.target,targets[side]),"original target restored after cleanup");
                Near(Vector3.Distance(e.position.n,stale),0,0,"original stored coordinate restored");
                Near(e.positionWeight,originalWeight,0,"original position weight restored");
                Near(e.rotationWeight,.3f,0,"original rotation weight restored");
                Near(QError(e.rotation.n,Axis(Vector3.UnitZ,12)),0,.001f,"original rotation restored");
            }
            Check(!rig.Ik.enabled,"original disabled IK restored");
            Near(rig.Ik.solver.IKPositionWeight,.65f,0,"original solver weight restored");
        }
        foreach(int fps in new[]{30,60,144}) foreach(int cleanup in new[]{0,1,2,3})
        {
            var rig=new Rig(); var hip=new Hip(); hip.Setup(rig.Ik);
            rig.Ik.enabled=false; rig.Ik.solver.IKPositionWeight=.7f;
            var e=rig.Ik.solver.leftShoulderEffector;
            e.position=new U.Vector3(100,-100,100);
            var start=e.bone.position.n;
            hip.StartChain(); hip.ChainOffset(new U.Vector3(2,0,0));
            Near(Vector3.Distance(e.target.position.n,start+Vector3.UnitX*.5f),0,.000001f,"production path caps pull at 50cm even at zero weight");
            for(int f=0;f<fps/3;f++) { Frame(fps); hip.Solve(()=>rig.Ik.solver.Read()); }
            hip.ReleaseChain(); Frame(fps); hip.Solve(()=>rig.Ik.solver.Read());
            float previous=hip.ChainValue;
            hip.ResumeChain(); Near(hip.ChainValue,previous,0,"reversal preserves transition weight");
            Frame(fps); hip.Solve(()=>rig.Ik.solver.Read());
            Check(hip.ChainValue>=previous,"reversal approaches held state");
            if(cleanup==0) hip.ReleaseChain();
            else if(cleanup==1) hip.CancelChain();
            else if(cleanup==2) hip.Disable();
            else hip.Clear();
            for(int f=0;f<fps;f++) { Frame(fps); hip.Solve(()=>rig.Ik.solver.Read()); }
            Check(!hip.ChainAlive,"slack, posture cancellation, disable and session cleanup release hold");
            Check(e.target==null&&!rig.Ik.enabled,"target and enabled state restored after lifecycle release");
            Near(e.positionWeight,0,0,"inactive shoulder restored after release");
            Near(rig.Ik.solver.IKPositionWeight,.7f,0,"solver weight restored after lifecycle release");
            hip.Clear();
        }
    }
}
