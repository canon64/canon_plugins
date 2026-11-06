using System.Numerics;
using MainGirlShoulderIkStabilizer;
using M = MainGirlShoulderIkStabilizer.ShoulderPoseMath;
using U = UnityEngine;

static partial class Program
{
    static void ForwardShoulders()
    {
        foreach(float side in new[]{1f,-1f})
        foreach(float elevation in new[]{90f,120f,135f,150f})
        foreach(float yaw in new[]{55f,70f,90f})
        foreach(var torso in new[]{Quaternion.Identity,Axis(Vector3.UnitX,90),Axis(Vector3.UnitZ,115)})
        {
            var root=new Vector3(.12f*side,0,0);
            var direction=new Vector3(side*MathF.Sin(elevation*M.Rad)*MathF.Cos(yaw*M.Rad),
                -MathF.Cos(elevation*M.Rad),MathF.Sin(elevation*M.Rad)*MathF.Sin(yaw*M.Rad));
            var elbow=root+direction*.3f;
            Quaternion Goal(Vector3 hand,float maximum,out M.SwingTrace trace) =>
                M.DesiredSwing(Vector3.Zero,Vector3.Transform(root,torso),Vector3.Transform(elbow,torso),
                    Vector3.Transform(hand,torso),Vector3.Transform(Vector3.UnitY,torso),0,.6f,1.5f,.4f,1,false,
                    1,45,150,35,1,1,out _,out _,out trace,
                    Vector3.Transform(Vector3.UnitZ,torso),Vector3.Transform(root,torso),maximum);
            var bent=Goal(elbow-direction*.25f,30,out var trace);
            var unfolded=Goal(elbow+direction*.3f,30,out _);
            Near(QError(bent,unfolded),0,.003f,"hand distance cannot disable upper-arm forward tracking");
            foreach(var forearm in new[]{Vector3.UnitY,-Vector3.UnitY,Vector3.UnitZ,-Vector3.UnitZ})
            {
                var movedWrist=Goal(elbow+forearm*.3f,30,out _);
                Near(QError(bent,movedWrist),0,.003f,"same upper arm and forearm length: moving wrist does not change shoulder goal");
            }
            var after=Vector3.Transform(Vector3.Transform(root,torso),bent);
            Check(Vector3.Dot(after,Vector3.Transform(Vector3.UnitZ,torso))>0,"both shoulders turn anatomically forward");
            Check(M.Tilt(after,Vector3.Transform(Vector3.UnitY,torso))>0,"raised forward shoulder also lifts");
            Check(trace.ForwardGoal>0&&trace.MissingForward>0,"forward request is observable");
            var disabled=Goal(elbow-direction*.25f,0,out _);
            Near(Vector3.Dot(Vector3.Transform(Vector3.Transform(root,torso),disabled),Vector3.Transform(Vector3.UnitZ,torso)),0,.00001f,"zero maximum restores elevation-only behavior");
        }
        // Actual production rotator on a raised, bent, forward arm: preserve hands and wrists.
        foreach(float side in new[]{1f,-1f})
        {
            var rig=new Rig();int index=side>0?0:1;var arm=rig.Ik.solver.limbs[index];
            var forward=new Vector3(.25f*side,.7071068f,.6614378f);
            arm.bone1.rotation=new U.Quaternion(M.FromTo(arm.bone2.position.n-arm.bone1.position.n,forward));
            var hand=arm.bone3.position.n;var wrist=arm.bone3.rotation.n;
            using var rot=new ShoulderRotator(rig.Ik,new PluginSettings{ShoulderWeight=0},null);
            rot.SetState(true,index==0,index==1);
            for(int frame=0;frame<120;frame++){rot.Restore();Frame();rot.Apply(0,0);}
            var clavicle=arm.bone1.position.n-arm.parentBone.position.n;
            Check(clavicle.Y>.02f&&clavicle.Z>.005f,"production raises and protracts the shoulder");
            Check(Vector3.Distance(hand,arm.bone3.position.n)<.001f,"forward correction preserves solved hand within 1mm");
            Near(QError(wrist,arm.bone3.rotation.n),0,.002f,"forward correction preserves wrist rotation");
        }
        // No forward chase for lowered arms, behind-the-body arms or exactly overhead.
        foreach(var direction in new[]{new Vector3(0,-1,1),new Vector3(0,1,-1),Vector3.UnitY})
        {
            var root=new Vector3(.12f,0,0);var elbow=root+Vector3.Normalize(direction)*.3f;
            M.DesiredSwing(Vector3.Zero,root,elbow,root,Vector3.UnitY,0,.6f,0,0,1,false,
                1,45,150,35,1,1,out _,out _,out var trace,Vector3.UnitZ,root,30);
            Check(trace.ForwardGoal==0,"lowered/behind/vertical does not acquire forward shoulder correction");
        }
        // A depressed native shoulder must not lower the absolute upper-arm-driven goal.
        foreach(float nativeTilt in new[]{-12f,-5f,0f,8f,20f})
        {
            var neutralRoot=new Vector3(.12f,0,0);
            var root=Vector3.Transform(neutralRoot,Axis(Vector3.UnitZ,nativeTilt));
            var elbow=root+new Vector3(.15f,.212132f,.15f);
            foreach(var wrist in new[]{new Vector3(0,-.3f,0),new Vector3(0,.3f,0),new Vector3(.3f,0,0)})
            {
                M.DesiredSwing(Vector3.Zero,root,elbow,elbow+wrist,Vector3.UnitY,1.8f,.6f,1.5f,.4f,1,false,
                    1,45,150,35,.8f,1,out float upperAngle,out float lift,out var trace,Vector3.UnitZ,neutralRoot,30);
                Near(trace.TargetTilt,1.8f+lift,.001f,"absolute goal does not retain native shoulder depression");
                Near(trace.UpperModeBlend,1,0,"raised arms isolate wrist-based reach contribution");
            }
        }
    }
}
