using System.Numerics;
using CanonPlugins.ShoulderPose;
using MainGirlShoulderIkStabilizer;
using RootMotion.FinalIK;
using M = MainGirlShoulderIkStabilizer.ShoulderPoseMath;
using U = UnityEngine;
using Hip = MainGirlHipHijack.Plugin;

static partial class Program
{
    static int checks;
    static float maxHandError;
    static void Check(bool value,string message) { checks++; if(!value) throw new Exception(message); }
    static void Near(float a,float b,float tolerance,string message) => Check(Math.Abs(a-b)<=tolerance,$"{message}: {a} vs {b}");
    static Quaternion Axis(Vector3 axis,float degrees) => Quaternion.CreateFromAxisAngle(axis,degrees*M.Rad);
    static float QError(Quaternion a,Quaternion b) => M.RotationAngle(Quaternion.Inverse(a)*b);
    static void Frame(int fps=60) { U.Time.frameCount++; U.Time.unscaledDeltaTime=1f/fps; U.Time.unscaledTime+=1f/fps; }
    static void Main()
    {
        Run("upper-arm elevation, symmetry, torso coordinates, baseline, limits",Geometry);
        Run("raised forward arms: protraction, bent elbows, mirrored/rotated torso and hand preservation",ForwardShoulders);
        Run("reachable two-bone solve, stable elbow and bone lengths",ReachAndBend);
        Run("30/60/144 fps transitions and reverse retargeting",Transitions);
        Run("nested/skipped/failed solver generations",Passes);
        Run("production rotator: hands, wrists, repeated frozen poses, disposal",RuntimeGeometry);
        Run("production ownership: animation writes and quaternion rounding",Ownership);
        Run("production rotator: side ownership, constraints, live configuration and bend inputs",RuntimeTransitions);
        Run("production Hip integration: notification order, nested/skipped/failed solves, cleanup",NotificationIntegration);
        Run("diagnostics: actual forward angles, four stages, limits, throttling and file output",Diagnostics);
        Run("diagnostic startup: first record, initialization failure and OFF isolation",StartupDiagnostics);
        Run("native diagnostics: auxiliary bones, native inputs, stage order, immediate OFF and missing references",NativeDiagnostics);
        Run("native correction wrapper: selected categories, preserved settings, ownership and exception rollback",NativeCorrection);
        Console.WriteLine($"PASS: {checks} assertions; maximum accepted hand error={maxHandError*1000:F6} mm (headless rigid hierarchy)");
    }
    static void Run(string name,Action test) { test(); Console.WriteLine("PASS "+name); }
    static Quaternion Desired(Vector3 root,Vector3 elbow,Vector3 hand,Vector3 up,float neutral=0,float reach=0,float blend=1,float elevation=1)
        => M.DesiredSwing(Vector3.Zero,root,elbow,hand,up,neutral,.6f,reach,.4f,1,false,elevation,45,150,35,blend,1,out _,out _);
    static void Geometry()
    {
        var root=new Vector3(.12f,0,0);
        foreach(float angle in new[]{0,44.9f,45,60,90,120,150,180})
        {
            var upper=new Vector3(MathF.Sin(angle*M.Rad),-MathF.Cos(angle*M.Rad),0)*.3f;
            var elbow=root+upper;
            var q=Desired(root,elbow,elbow+Vector3.UnitZ*.3f,Vector3.UnitY);
            var folded=Desired(root,elbow,elbow-upper*.95f+Vector3.UnitZ*.01f,Vector3.UnitY);
            Near(QError(q,folded),0,.001f,"bending elbow cannot disable elevation");
            Near(M.Tilt(Vector3.Transform(root,q),Vector3.UnitY),M.Elevation(angle,45,150,35),.001f,"elevation curve");
            var mirror=Desired(-root,new Vector3(-elbow.X,elbow.Y,elbow.Z),new Vector3(-elbow.X,elbow.Y,.3f),Vector3.UnitY);
            Near(M.Tilt(Vector3.Transform(-root,mirror),Vector3.UnitY),M.Tilt(Vector3.Transform(root,q),Vector3.UnitY),.001f,"left/right mirror");
            foreach(var torso in new[]{Axis(Vector3.UnitX,90),Axis(Vector3.UnitZ,115),Quaternion.CreateFromYawPitchRoll(.4f,1.1f,.9f)})
            {
                var turned=Desired(Vector3.Transform(root,torso),Vector3.Transform(elbow,torso),Vector3.Transform(elbow+Vector3.UnitZ*.3f,torso),Vector3.Transform(Vector3.UnitY,torso));
                Near(QError(turned,torso*q*Quaternion.Inverse(torso)),0,.01f,"torso rotation covariance");
            }
        }
        var lifted=Vector3.Transform(root,Axis(Vector3.UnitZ,20));
        var qLift=Desired(lifted,lifted+new Vector3(.15f,.2598076f,0),lifted+Vector3.UnitY*.2f,Vector3.UnitY);
        Near(M.RotationAngle(qLift),15,.001f,"subtract already raised shoulder");
        var down=Desired(root,root-Vector3.UnitY*.3f,root-Vector3.UnitY*.6f,Vector3.UnitY,reach:1,elevation:0);
        Check(M.Tilt(Vector3.Transform(root,down),Vector3.UnitY)<0,"legacy downward reach is preserved");
        var full=Desired(root,root+Vector3.UnitY*.3f,root+Vector3.UnitY*.1f,Vector3.UnitY);
        Near(M.RotationAngle(M.LimitAngle(full,12)),12,.001f,"maximum rotation");
        Near(M.RotationAngle(Desired(root,root+Vector3.UnitY*.3f,root,Vector3.UnitY,blend:.4f)),14,.001f,"maximum blend");
        Near(M.RotationAngle(M.FromTo(Vector3.UnitX,Vector3.Transform(Vector3.UnitX,Axis(Vector3.UnitZ,.015f)))),.015f,.001f,"small rotations retain precision");
    }
    static void ReachAndBend()
    {
        foreach(float upper in new[]{.18f,.3f,.5f}) foreach(float lower in new[]{.16f,.3f,.45f})
        foreach(float d in new[]{Math.Abs(upper-lower)+.001f,(upper+lower)*.8f,upper+lower-.000001f})
        {
            if(d<=Math.Abs(upper-lower)||d>upper+lower) continue;
            var hand=new Vector3(d,0,0);
            Check(M.SolveElbow(Vector3.Zero,hand,new Vector3(.1f,.1f,0),Vector3.UnitY,upper,lower,out var elbow,out var bend),"reachable solve");
            Near(elbow.Length(),upper,.00001f,"upper length");
            Near(Vector3.Distance(elbow,hand),lower,.00001f,"forearm length");
            Check(Vector3.Dot(bend,Vector3.UnitY)>.999f,"bend plane kept");
            Check(M.SolveElbow(Vector3.Zero,hand,new Vector3(.1f,-.00000001f,0),Vector3.UnitY,upper,lower,out _,out bend),"straight fallback");
            Check(Vector3.Dot(bend,Vector3.UnitY)>.99f,"no straight-arm elbow flip");
        }
        var root=new Vector3(.12f,0,0); var target=root-Vector3.UnitY*.59f;
        var delta=Axis(Vector3.UnitZ,35);
        float fraction=M.ReachFraction(Vector3.Zero,root,target,.3f,.3f,delta);
        Check(fraction>0&&fraction<1,"reach cap activates");
        var capped=Vector3.Transform(root,Quaternion.Slerp(Quaternion.Identity,delta,fraction));
        Check(M.Reachable(capped,target,.3f,.3f),"limited result reachable");
        Check(!M.Reachable(Vector3.Transform(root,Quaternion.Slerp(Quaternion.Identity,delta,Math.Min(1,fraction+.001f))),target,.3f,.3f),"boundary located");
    }
    static void Transitions()
    {
        var outputs=new List<float>();
        foreach(int fps in new[]{30,60,144})
        {
            var current=Quaternion.Identity;
            for(int f=0;f<fps;f++)
            {
                var target=Axis(Vector3.UnitZ,f<fps/2?35:-20);
                var next=M.SmoothRotation(current,target,1f/fps,.15f,120);
                Check(QError(current,next)<=120f/fps+.001f,"angular speed cap including reversal");
                current=next;
            }
            outputs.Add(M.RotationAngle(current));
            var w=new WeightTransition();w.SetTarget(1);
            for(int f=0;f<fps;f++) w.Step(1f/fps,.15f);
            Near(w.Value,1,0,"weight settled");
            w.SetTarget(0);w.Step(.05f,.15f);float before=w.Value;
            w.SetTarget(1);Near(w.Value,before,0,"reversal starts at current output");
            w.Step(0,.15f);Near(w.Value,before,0,"nested update cannot advance time");
            w.SetTarget(0);w.Step(.01f,.05f);
            Check(w.Value>=0&&w.Value<=before,"duration change retains output range");
            before=w.Value;w.Step(0,.9f);Near(w.Value,before,0,"duration changes restart from current output");
        }
        Check(outputs.Max()-outputs.Min()<.2f,"frame rate convergence");
    }
    static void Passes()
    {
        var pass=new SolverPassState();var outer=pass.Enter();pass.MarkSolved();var inner=pass.Enter();pass.MarkSolved();
        Check(!pass.Exit(inner,true),"inner not finalized");Check(pass.Exit(outer,true),"outer finalized once");
        var skipped=pass.Enter();Check(!pass.Exit(skipped,true),"VMD skipped solve not finalized");
        outer=pass.Enter();pass.MarkSolved();inner=pass.Enter();
        Check(!pass.Exit(inner,false)&&!pass.Exit(outer,true),"failure cancels transaction");
        outer=pass.Enter();pass.MarkSolved();pass.Reset();Check(!pass.Exit(outer,true),"stale generation ignored");
        Check(pass.IsOuter,"clear resets nesting");
    }
    internal sealed class Rig
    {
        public readonly FullBodyBipedIK Ik=new FullBodyBipedIK { gameObject=new U.GameObject() };
        public readonly U.Transform Chest;
        public Rig(float upper=.3f,float lower=.28f,Quaternion? orientation=null)
        {
            var spine=Bone(null,new Vector3(0,0,0));Chest=Bone(spine,new Vector3(0,.3f,0));
            Ik.references.root=spine;
            Ik.solver.spineMapping.spineBones=new[]{spine,Chest};
            for(int i=0;i<2;i++)
            {
                float side=i==0?1:-1;
                var m=Ik.solver.limbs[i];
                m.parentBone=Bone(Chest,new Vector3(.05f*side,.02f,0));
                m.bone1=Bone(m.parentBone,new Vector3(.12f*side,0,0));
                m.bone2=Bone(m.bone1,new Vector3(upper*.5f*side,upper*.8660254f,0));
                m.bone3=Bone(m.bone2,new Vector3(-lower*.7f*side,-lower*.3f,lower*MathF.Sqrt(.42f)));
                m.Map.localSwingAxis=new U.Vector3(side,0,0);
                Ik.solver.hands[i].bone=m.bone3;
                var shoulder=Ik.solver.GetEffector((FullBodyBipedEffector)i);
                shoulder.bone=m.bone1;shoulder.position=m.bone1.position;shoulder.positionWeight=0;
            }
            spine.rotation=new U.Quaternion(orientation??Quaternion.Identity);
        }
        static U.Transform Bone(U.Transform parent,Vector3 offset) => new U.Transform { parent=parent,localPosition=new U.Vector3(offset) };
    }
    static void RuntimeGeometry()
    {
        foreach(int fps in new[]{30,60,144}) foreach(float scale in new[]{.7f,1,1.5f})
        foreach(var orientation in new[]{Quaternion.Identity,Axis(Vector3.UnitX,90),Axis(Vector3.UnitZ,110)})
        {
            var rig=new Rig(.3f*scale,.28f*scale,orientation);
            var cfg=new PluginSettings{ShoulderWeight=0,ShoulderRightWeight=0,MaxSolverBlend=1};
            using var rotator=new ShoulderRotator(rig.Ik,cfg,null);
            rotator.SetState(true,true,true);
            var hands=rig.Ik.solver.limbs.Select(m=>m.bone3.position.n).ToArray();
            var wrists=rig.Ik.solver.limbs.Select(m=>m.bone3.rotation.n).ToArray();
            for(int frame=0;frame<fps*2;frame++)
            {
                rotator.Restore();Frame(fps);rotator.Apply(0,0);
                for(int i=0;i<2;i++)
                {
                    var m=rig.Ik.solver.limbs[i];float error=Vector3.Distance(m.bone3.position.n,hands[i]);
                    maxHandError=Math.Max(maxHandError,error);
                    Check(error<.001f,"production hand position preserved without animation rewriting bones");
                    Near(U.Vector3.Distance(m.bone1.position,m.bone2.position),.3f*scale,.00001f,"production upper length");
                    Near(U.Vector3.Distance(m.bone2.position,m.bone3.position),.28f*scale,.00001f,"production forearm length");
                    Near(QError(m.bone3.rotation.n,wrists[i]),0,.001f,"production wrist rotation");
                }
            }
            float correction=M.RotationAngle(rig.Ik.solver.limbs[0].parentBone.localRotation.n);
            Check(correction>10,"bent arm actually raises clavicle");
            rotator.Restore();Near(M.RotationAngle(rig.Ik.solver.limbs[0].parentBone.localRotation.n),0,.001f,"frozen pose restored");
            rotator.SetState(false,false,false);
            for(int f=0;f<fps;f++){rotator.Restore();Frame(fps);rotator.Apply(0,0);}
            Check(!rotator.NeedsProcessing,"OFF reaches zero and stops callbacks");
            rotator.Dispose();Check(rig.Ik.solver.SubscriptionCount==0,"dispose releases solver event handlers");
        }
        // Raw effector target/rotation weights must never be applied for a second time.
        var outputs=new List<Vector3>();
        foreach(float rawWeight in new[]{0,.5f,1})
        {
            var r=new Rig();r.Ik.solver.hands[0].target=new U.GameObject().transform;
            r.Ik.solver.hands[0].target.position=new U.Vector3(20,-20,20);
            r.Ik.solver.hands[0].rotationWeight=rawWeight;
            using var rot=new ShoulderRotator(r.Ik,new PluginSettings{ShoulderWeight=0},null);rot.SetState(true,true,false);
            for(int f=0;f<60;f++){rot.Restore();Frame();rot.Apply(0,0);}
            outputs.Add(r.Ik.solver.limbs[0].bone3.position.n);
        }
        Near(Vector3.Distance(outputs[0],outputs[2]),0,.000001f,"raw target weights do not reapply");
    }
    static void Ownership()
    {
        var bone=new U.Transform();var owned=new OwnedBoneRotation();
        for(int i=0;i<10000;i++)
        {
            bone.localRotation=U.Quaternion.identity;owned.Capture(bone);
            bone.localRotation=new U.Quaternion(Quaternion.CreateFromYawPitchRoll(i*.014f,i*.019f,i*.023f));owned.Commit();owned.Restore();
            Near(M.RotationAngle(bone.localRotation.n),0,.001f,"own rounded quaternion restores exactly");
        }
        owned.Capture(bone);bone.localRotation=new U.Quaternion(Axis(Vector3.UnitX,20));owned.Commit();
        var animation=Axis(Vector3.UnitY,31);bone.localRotation=new U.Quaternion(animation);owned.Restore();
        Near(QError(bone.localRotation.n,animation),0,.001f,"external animation wins");
    }
    static void RuntimeTransitions()
    {
        var rig=new Rig();
        var cfg=new PluginSettings { ShoulderWeight=0,ShoulderRightWeight=0,IndependentShoulders=true,ElevationRightMaxLift=10,MaxSolverBlend=1 };
        using var rot=new ShoulderRotator(rig.Ik,cfg,null);
        rot.SetState(true,true,true);
        Quaternion previous=Quaternion.Identity;
        for(int f=0;f<180;f++)
        {
            if(f==25) cfg.TransitionSeconds=.5f;
            if(f==35) cfg.TransitionSeconds=.05f;
            if(f==90) rot.SetState(false,false,false);
            rot.Restore();Frame();rot.Apply(f>=40&&f<70?1:0,0);
            var now=rig.Ik.solver.limbs[0].parentBone.localRotation.n;
            Check(QError(previous,now)<=cfg.MaxAngularSpeed/60+.002f,"constraint/config/OFF angular velocity bound");
            previous=now;
            if(f==39)
            {
                float left=M.RotationAngle(now),right=M.RotationAngle(rig.Ik.solver.limbs[1].parentBone.localRotation.n);
                Check(left>right*2,"independent right maximum lift");
            }
        }
        Check(!rot.NeedsProcessing,"OFF residual finished");
        rot.SetState(true,true,false);
        var component=new U.Component{gameObject=rig.Ik.gameObject};
        Check(rot.Owns(component,0)&&!rot.Owns(component,1),"AdvIK suppression only selected side");
        Check(!rot.Owns(new U.Component{gameObject=new U.GameObject()},0),"other character AdvIK unaffected");
        rig.Ik.solver.Reinitialize();
        Check(rig.Ik.solver.SubscriptionCount==2,"reinitialize has no duplicate subscription");
        foreach(float bendWeight in new[]{0,.5f,1})
        {
            // Simulate three already solved elbow planes. The production correction must
            // preserve each result without access to (or reapplication of) the bend goal.
            var r=new Rig();var m=r.Ik.solver.limbs[0];
            Vector3 hand=m.bone3.position.n,root=m.bone1.position.n;
            Vector3 axis=Vector3.Normalize(hand-root);
            var turn=Axis(axis,bendWeight*60);
            Vector3 elbow=root+Vector3.Transform(m.bone2.position.n-root,turn);
            m.bone1.rotation=new U.Quaternion(turn)*m.bone1.rotation;
            m.bone2.rotation=new U.Quaternion(M.FromTo(m.bone3.position.n-m.bone2.position.n,hand-m.bone2.position.n))*m.bone2.rotation;
            var wrist=m.bone3.rotation.n;
            using var correction=new ShoulderRotator(r.Ik,new PluginSettings{ShoulderWeight=0},null);correction.SetState(true,true,false);
            for(int f=0;f<60;f++){correction.Restore();Frame();correction.Apply(0,0);}
            Check(Vector3.Distance(m.bone3.position.n,hand)<.001f,"bend weight hand result preserved");
            Near(QError(m.bone3.rotation.n,wrist),0,.001f,"bend weight wrist result preserved");
            var direction=Vector3.Normalize(hand-m.bone1.position.n);
            Check(Vector3.Dot(M.Project(m.bone2.position.n-m.bone1.position.n,direction),M.Project(elbow-m.bone1.position.n,direction))>0,"solved elbow side is retained");
        }
    }
#if LEGACY_SHOULDER_CHAIN
    static void Integration()
    {
        var rig=new Rig();var hip=new Hip();hip.Setup(rig.Ik);
        int prepares=0,finals=0;
        var stages=new List<string>();
        Action<ChaControl,FullBodyBipedIK> prepare=(_,__)=>{prepares++;stages.Add("prepare");};
        Action<ChaControl,FullBodyBipedIK> solved=(_,__)=>stages.Add("solved");
        Action<ChaControl,FullBodyBipedIK> completed=(_,__)=>stages.Add("final");
        Action<ChaControl,FullBodyBipedIK,float,float> final=(_,__,___,____)=>{finals++;stages.Add("auto");};
        Hip.ShoulderPosePreparing+=prepare;Hip.ShoulderPoseFinalizing+=final;
        Hip.ShoulderPoseSolved+=solved;Hip.ShoulderPoseCompleted+=completed;
        try
        {
            hip.ManualStart(0);Frame();hip.Solve(()=>{rig.Ik.solver.Read();hip.Solve(()=>rig.Ik.solver.Read());});
            Check(prepares==1&&finals==1,"nested actual callbacks finalize once");
            Check(stages.SequenceEqual(new[]{"prepare","solved","auto","final"}),"diagnostic observers run in outer-solve phase order once");
            var reference=new WeightTransition();reference.SetTarget(1);reference.Step(1f/60,.15f);
            Near(hip.ManualValue(0),reference.Value,.000001f,"nested transition advances once");
            float before=hip.ManualValue(0);Frame();hip.Solve(()=>{});
            Near(hip.ManualValue(0),before,0,"skipped solve does not advance");Check(finals==1,"skipped solve has no final pose");
            for(int f=0;f<15;f++){Frame();hip.Solve(()=>rig.Ik.solver.Read());}
            hip.ManualRelease(0);Check(hip.ManualRunning(0)&&hip.Disabled==0,"release retains references before fade");
            Frame();hip.Solve(()=>rig.Ik.solver.Read());before=hip.ManualValue(0);
            hip.ManualResume(0);Near(hip.ManualValue(0),before,0,"release reversal has no weight jump");
            hip.ManualRelease(0);
            for(int f=0;f<15;f++){Frame();hip.Solve(()=>rig.Ik.solver.Read());}
            Check(!hip.ManualBound(0)&&!hip.ManualRunning(0)&&hip.Disabled==1,"completed fade releases once");
            var oldTarget=new U.GameObject().transform;oldTarget.position=rig.Ik.solver.leftShoulderEffector.bone.position;
            rig.Ik.solver.leftShoulderEffector.target=oldTarget;rig.Ik.solver.leftShoulderEffector.positionWeight=.4f;
            hip.StartChain();
            for(int f=0;f<15;f++){Frame();hip.Solve(()=>rig.Ik.solver.Read());}
            Near(hip.ChainValue,1,0,"chain fade in complete");
            bool pending=false;hip.QueueAfterChain(()=>pending=true);Check(!pending&&hip.ChainAlive,"chain queues operation until release");
            for(int f=0;f<15;f++){Frame();hip.Solve(()=>rig.Ik.solver.Read());}
            Check(pending&&!hip.ChainAlive,"chain release applies pending operation");
            Check(ReferenceEquals(oldTarget,rig.Ik.solver.leftShoulderEffector.target),"original target restored");
            Near(rig.Ik.solver.leftShoulderEffector.positionWeight,.4f,0,"original shoulder weight restored");
            hip.StartChain();hip.ManualStart(1);Frame();hip.Solve(()=>rig.Ik.solver.Read());
            hip.ManualRelease(1);hip.Clear();
            Check(!hip.ChainAlive&&!hip.ManualBound(1),"session end cancels holds/transitions");
            Check(!Hip.TryGetShoulderRuntime(out _,out _)&&rig.Ik.solver.SubscriptionCount==0,"context clear unhooks and nulls refs");
            hip.Setup(rig.Ik);Check(Hip.TryGetShoulderRuntime(out _,out _),"next session reconnects");
            hip.ManualStart(0);Frame();hip.Solve(()=>rig.Ik.solver.Read());
            CanonPlugins.Local.SessionGate.Allowed=false;
            before=hip.ManualValue(0);int oldFinals=finals;
            Frame();hip.Solve(()=>rig.Ik.solver.Read());
            Near(hip.ManualValue(0),before,0,"revoked permit does not advance transitions");
            Check(finals==oldFinals,"revoked permit cannot finalize");
            hip.Clear();Check(!hip.ManualBound(0)&&rig.Ik.solver.SubscriptionCount==0,"cleanup works after permit revoked");
            CanonPlugins.Local.SessionGate.Allowed=true;
            Check(hip.Warnings.Count==0,"no integration warning");
        }
        finally { hip.Clear();CanonPlugins.Local.SessionGate.Allowed=true;Hip.ShoulderPosePreparing-=prepare;Hip.ShoulderPoseFinalizing-=final;Hip.ShoulderPoseSolved-=solved;Hip.ShoulderPoseCompleted-=completed; }
    }
#endif
    static void NotificationIntegration()
    {
        var rig=new Rig();var hip=new Hip();hip.Setup(rig.Ik);
        int prepares=0,finals=0,faults=0;
        var stages=new List<string>();
        Action<ChaControl,FullBodyBipedIK> prepare=(_,__)=>{prepares++;stages.Add("prepare");};
        Action<ChaControl,FullBodyBipedIK> solved=(_,__)=>stages.Add("solved");
        Action<ChaControl,FullBodyBipedIK> completed=(_,__)=>stages.Add("final");
        Action<ChaControl,FullBodyBipedIK,float,float> final=(_,__,___,____)=>{finals++;stages.Add("auto");};
        Action<Exception> fault=_=>faults++;
        Hip.ShoulderPosePreparing+=prepare;Hip.ShoulderPoseSolved+=solved;
        Hip.ShoulderPoseFinalizing+=final;Hip.ShoulderPoseCompleted+=completed;Hip.ShoulderPoseFaulted+=fault;
        try
        {
            Frame();hip.Solve(()=>{rig.Ik.solver.Read();hip.Solve(()=>rig.Ik.solver.Read());});
            Check(prepares==1&&finals==1,"nested actual callbacks finalize once");
            Check(stages.SequenceEqual(new[]{"prepare","solved","auto","final"}),"outer-solve diagnostic phase order");
            Frame();hip.Solve(()=>{});Check(finals==1,"skipped solve has no final pose");
            var expected=new InvalidOperationException("original solver failure");
            try {hip.Solve(()=>{rig.Ik.solver.Read();throw expected;});Check(false,"original exception expected");}
            catch(InvalidOperationException ex){Check(ReferenceEquals(ex,expected),"original solver exception preserved");}
            Check(faults==1&&finals==1,"failed solve is diagnosed without final pose");
            rig.Ik.solver.leftShoulderEffector.positionWeight=.4f;
            hip.Solve(()=>rig.Ik.solver.Read());
            Near(rig.Ik.solver.leftShoulderEffector.positionWeight,.4f,0,"hooks do not change IK weights");
            hip.Clear();hip.Clear();
            Check(!Hip.TryGetShoulderRuntime(out _,out _)&&rig.Ik.solver.SubscriptionCount==0,"double clear unhooks and clears references");
            hip.Setup(rig.Ik);Check(Hip.TryGetShoulderRuntime(out _,out _),"next session reconnects");
            CanonPlugins.Local.SessionGate.Allowed=false;
            int oldFinals=finals;hip.Solve(()=>rig.Ik.solver.Read());
            Check(finals==oldFinals,"revoked permit cannot finalize");
            hip.Clear();Check(rig.Ik.solver.SubscriptionCount==0,"cleanup works after permit revoked");
        }
        finally
        {
            hip.Clear();CanonPlugins.Local.SessionGate.Allowed=true;
            Hip.ShoulderPosePreparing-=prepare;Hip.ShoulderPoseSolved-=solved;
            Hip.ShoulderPoseFinalizing-=final;Hip.ShoulderPoseCompleted-=completed;Hip.ShoulderPoseFaulted-=fault;
        }
    }
    static void StartupDiagnostics()
    {
        var lines=new List<string>();var expected=new MissingMethodException("missing shoulder runtime API");
        bool entered=false;
        try
        {
            DiagnosticStartup.Run(()=>{
                entered=true;Check(lines.Count==1&&lines[0]=="BOOT stage=awake_begin","record precedes dependency initialization");
                throw expected;
            },()=>true,lines.Add);
            Check(false,"initialization error must propagate");
        }
        catch(MissingMethodException ex){Check(ReferenceEquals(ex,expected),"original initialization failure preserved");}
        Check(entered&&lines.Count==2&&lines[1].Contains("BOOT_FAILED")&&lines[1].Contains(expected.Message),"failure includes stage and original error");
        lines.Clear();
        try {DiagnosticStartup.Run(()=>throw expected,()=>false,lines.Add);}catch(MissingMethodException){}
        Check(lines.Count==0,"OFF startup/failure does not invoke diagnostic writer");
        DiagnosticStartup.Run(()=>entered=true,()=>true,lines.Add);
        Check(lines.Count==1,"successful startup writes initial evidence");
    }
    static void Diagnostics()
    {
        var rig=new Rig();var lines=new List<string>();
        var cfg=new PluginSettings{ShoulderDiagnosticLog=true,ShoulderDiagnosticLogInterval=.2f,ShoulderWeight=0};
        using var rot=new ShoulderRotator(rig.Ik,cfg,lines.Add);
        rot.SetState(true,true,true);
        var arm=rig.Ik.solver.limbs[0];
        // Raised upper arm directed forward: yaw 90 degrees, elevation 120 degrees.
        var forward=new Vector3(0,.5f,.8660254f);
        arm.bone1.rotation=new U.Quaternion(M.FromTo(arm.bone2.position.n-arm.bone1.position.n,forward));
        var originalCulture=System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture=new System.Globalization.CultureInfo("fr-FR");
            Frame();rot.CaptureDiagnosticSolvedPose();
            var handBefore=arm.bone3.position.n;
            rot.Apply(0,0);rot.CompleteDiagnosticPose();
            string text=string.Join("\n",lines);
            Check(text.Contains("upperElevation=120.0000")||text.Contains("upperElevation=119.9999"),"actual raised forward upper-arm angle logged");
            Check(text.Contains("upperYaw=90.0000"),"forward direction distinct from upward angle");
            Check(lines.Any(s=>s.StartsWith("CALC ")&&s.Contains("side=L")&&s.Contains("forwardGoal=")&&!s.Contains("forwardGoal=0.0000")),"raised forward upper arm produces a forward shoulder goal");
            foreach(string stage in new[]{"solved","held","auto","final"})Check(text.Contains("stage="+stage),"stage logged: "+stage);
            foreach(string bone in new[]{"clavicle","upper","fore","hand"})Check(text.Contains("bone="+bone+" localEuler="),"real bone rotations logged: "+bone);
            Check(text.Contains("reachBlend=0.0000")&&text.Contains("effectiveWeight=")&&text.Contains("goalReach="),"calculation limiting factors logged");
            Check(text.Contains("handErrorMm=")&&text.Contains("FINAL_DELTA"),"hand errors and later overwrite delta logged");
            Check(Vector3.Distance(arm.bone3.position.n,handBefore)<.001f,"diagnostics preserve hand result");
            Check(rot.DescribeDiagnosticBinding().Contains("clavicle=\"")&&rot.DescribeDiagnosticBinding().Contains("forwardLocal="),"binding paths and axes available");
            int count=lines.Count;rot.Restore();rot.CaptureDiagnosticSolvedPose();rot.Apply(0,0);rot.CompleteDiagnosticPose();
            Check(lines.Count==count,"same-time IK callback emits no extra sample");
            U.Time.unscaledTime+=.3f;Frame();rot.Restore();rot.CaptureDiagnosticSolvedPose();rot.Apply(1,0);rot.CompleteDiagnosticPose();
            Check(lines.Any(s=>s.Contains("constraint=1.0000")&&s.Contains("effectiveWeight=0.0000")),"shoulder constraint is visible even during fading");
            cfg.ShoulderDiagnosticLog=false;count=lines.Count;U.Time.unscaledTime+=1;Frame();rot.Restore();rot.Apply(0,0);rot.CompleteDiagnosticPose();
            Check(lines.Count==count,"diagnostic OFF emits nothing");
            cfg.ShoulderDiagnosticLog=true;U.Time.unscaledTime+=1;Frame();rot.CaptureDiagnosticSolvedPose();
            count=lines.Count;cfg.ShoulderDiagnosticLog=false;
            rot.Apply(0,0);rot.CompleteDiagnosticPose();
            Check(lines.Count==count,"turning OFF between solve and completion suppresses all pending diagnostics");
            cfg.ShoulderDiagnosticLog=true;rot.CancelDiagnosticPose();Frame();rot.CaptureDiagnosticSolvedPose();
            rot.Apply(0,0);rot.CompleteDiagnosticPose();
            Check(lines.Count>count,"turning ON resumes diagnostics without replacing the DLL");
        }
        finally {System.Globalization.CultureInfo.CurrentCulture=originalCulture;}
        var path=Path.Combine(AppContext.BaseDirectory,"diagnostic-test",Guid.NewGuid().ToString("N"),"ShoulderPoseDiagnostic.log");
        string failure=null;var writer=new PoseDiagnosticWriter(path,s=>failure=s,maxBytes:128);
        writer.Write("ACTUAL-TEST "+new string('x',256));writer.Write("NEXT-RECORD");
        Check(File.ReadAllText(path).Contains("NEXT-RECORD"),"dedicated file has real emitted record");
        Check(!File.Exists(path+".previous.log")&&File.ReadAllText(path).Contains("SEGMENT reason=size_limit"),"bounded log marks truncation without copying logs");
        Check(failure==null,"file writer succeeded independently of relay");
    }
}
