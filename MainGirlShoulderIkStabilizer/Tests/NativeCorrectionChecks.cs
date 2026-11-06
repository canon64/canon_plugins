using System.Reflection;
using MainGirlShoulderIkStabilizer;
using U=UnityEngine;
using M=MainGirlShoulderIkStabilizer.ShoulderPoseMath;

static partial class Program
{
    static void NativeCorrection()
    {
        var expression=new ExpressionBone{enable=false};
        foreach(ExpressionBone.BoneName name in Enum.GetValues<ExpressionBone.BoneName>())expression.Bones[(int)name]=new U.Transform();
        bool Flag(string name)=>(bool)typeof(ExpressionBone).GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(expression);
        var left=expression.Bones[(int)ExpressionBone.BoneName.cf_d_shoulder02_L];
        var right=expression.Bones[(int)ExpressionBone.BoneName.cf_d_shoulder02_R];
        int calls=0;
        expression.NativeCall=()=>{
            calls++;Check(Flag("enable")&&Flag("enableShoulderL")&&Flag("enableUpperarmL"),"native call receives active left categories");
            Check(!Flag("enableShoulderR")&&!Flag("enableUpperarmR")&&!Flag("enableBust")&&!Flag("enableElboL"),"native call excludes all unrelated categories");
            left.localPosition=new U.Vector3(0,.02f,0);
            left.localRotation=new U.Quaternion(Axis(System.Numerics.Vector3.UnitZ,25));
        };
        using var correction=new NativeShoulderCorrection(expression);
        Check(correction.Ready&&correction.Apply(true,false)&&calls==1&&correction.ChangedBones==1,"production wrapper calls native method once and observes changed helper");
        Check(!Flag("enable")&&Flag("enableShoulderR")&&Flag("enableBust")&&Flag("enableElboL"),"all original native switches restored after call");
        Near(right.localPosition.y,0,0,"opposite helper untouched");
        correction.Restore();Near(left.localPosition.y,0,0,"position restored before next IK/end");
        Near(M.RotationAngle(left.localRotation.n),0,.001f,"rotation restored before next IK/end");
        correction.Apply(true,false);left.localPosition=new U.Vector3(0,.05f,0);
        left.localRotation=new U.Quaternion(Axis(System.Numerics.Vector3.UnitZ,40));correction.Restore();
        Near(left.localPosition.y,.05f,0,"animation's later position is preserved");
        Near(M.RotationAngle(left.localRotation.n),40,.001f,"animation's later rotation is preserved");
        var originalPos=left.localPosition;var originalRot=left.localRotation;
        expression.NativeCall=()=>{left.localPosition=new U.Vector3(0,.2f,0);throw new InvalidOperationException("native failure");};
        try{correction.Apply(true,false);Check(false,"native exception expected");}catch(InvalidOperationException){}
        Near(U.Vector3.Distance(left.localPosition,originalPos),0,.000001f,"failed native call rolls back helper changes");
        Near(M.RotationAngle(left.localRotation.n),M.RotationAngle(originalRot.n),.001f,"failed native call preserves rotation");
        Check(!Flag("enable")&&Flag("enableBust"),"native exception restores original switches");
        Check(!correction.Apply(false,false),"inactive arms do not invoke native correction");
        using var missing=new NativeShoulderCorrection(null);Check(!missing.Ready&&!missing.Apply(true,true),"missing native component is an explicit unavailable state");
    }
}
