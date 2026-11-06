using System.Reflection;
using RootMotion.FinalIK;
using UnityEngine;

namespace MainGirlHipHijack
{
    // Only dependencies outside the two production shoulder integration files are faked.
    public sealed partial class Plugin
    {
        public static Plugin Instance;
        private const int BIK_LS=4,BIK_RS=5,BIK_TOTAL=12;
        public bool isActiveAndEnabled=true;
        public int Disabled, Synced;
        public readonly List<string> Warnings=new();
        private readonly FemaleSession _femaleSession=new();
        private readonly BodyState[] _bikEff=Enumerable.Range(0,BIK_TOTAL).Select(_=>new BodyState()).ToArray();
        private sealed class FemaleSession { public FullBodyBipedIK FullBodyIk; public ChaControl Character; }
        private sealed class BodyState { public bool Running; }
        private sealed class ChainCameraLook { public void Restore() { } }
        private sealed class TestLogger { public void LogWarning(string s) => Instance.Warnings.Add(s); }
        private readonly TestLogger Logger=new();
        private bool IsPluginEnabled() => true;
        private void LogWarn(string s) => Warnings.Add(s);
        private void LogInfo(string s) { }
        private static void Destroy(UnityEngine.Object o) => UnityEngine.Object.Destroy(o);
        private static FullBodyBipedIK ResolveFbbik(ChaControl actor) => Instance._femaleSession.FullBodyIk;
        private void ApplyChainCameraLook(ChainShoulderHold hold) { Synced++; }
        private void StopGaugePoseBlend() { }
        private void ReleaseAllInputCapture() { }
        private float GetBodyIKWeight(int idx) => 1;
        private void DoDisableBodyIK(int idx,bool silent,bool deferDrag,bool preserveProxy,bool immediateShoulder=false)
        {
            Disabled++;
            _bikEff[idx].Running=false;
            ResetManualShoulder(idx);
            _femaleSession.FullBodyIk.solver.GetEffector((FullBodyBipedEffector)(idx-BIK_LS)).positionWeight=0;
        }
        public void Setup(FullBodyBipedIK ik)
        {
            Instance=this;
            _femaleSession.FullBodyIk=ik; _femaleSession.Character=new ChaControl();
            PublishShoulderRuntime();
        }
        public void Clear() => ClearShoulderRuntime();
        public void ManualStart(int side)
        {
            _bikEff[BIK_LS+side].Running=true;
            SetManualShoulderWeight(BIK_LS+side,1);
        }
        public void ManualRelease(int side) => BeginShoulderRelease(BIK_LS+side,false,false,false);
        public void ManualResume(int side) => CancelShoulderRelease(BIK_LS+side);
        public float ManualValue(int side) => _manualShoulders[side].Weight.Value;
        public bool ManualBound(int side) => _manualShoulders[side].Bound;
        public bool ManualRunning(int side) => _bikEff[BIK_LS+side].Running;
        public void StartChain()
        {
            var ik=_femaleSession.FullBodyIk;
            _chainShoulderHold=new ChainShoulderHold {
                Scene=new HSceneProc(), Female=_femaleSession.Character, Ik=ik,Session=1,
                WasEnabled=ik.enabled,SolverWeight=ik.solver.IKPositionWeight,
                Left=new HeldShoulder(ik.solver.leftShoulderEffector,"L",ik.solver.leftShoulderEffector.bone.position,Quaternion.identity),
                Right=new HeldShoulder(ik.solver.rightShoulderEffector,"R",ik.solver.rightShoulderEffector.bone.position,Quaternion.identity)
            };
            _chainShoulderHold.Blend.SetTarget(1);
        }
        public float ChainValue => _chainShoulderHold?.Blend.Value??0;
        public bool ChainAlive => _chainShoulderHold!=null;
        public void ChainTarget(float weight, Vector3 offset)
        {
            _chainShoulderHold.Left.Apply(offset,weight);
            _chainShoulderHold.Right.Apply(offset,weight);
        }
        public void ChainOffset(Vector3 offset) => ApplyChainShoulderTargets(offset);
        public void ResumeChain() { _chainShoulderHold.Releasing=false; _chainShoulderHold.Blend.SetTarget(1); }
        public void CancelChain() => CancelChainShoulderPull(_chainShoulderHold.Scene);
        public void Disable() => OnDisable();
        public void ReleaseChain() => ReleaseChainShoulderHold(false);
        public void QueueAfterChain(Action action) => QueueShoulderAfterChain(BIK_LS,action);
        public void Solve(Action body)
        {
            // Invoke the actual Harmony callbacks around a simulated original method.
            var before=typeof(ShoulderSolverPatches).GetMethod("Before",BindingFlags.Static|BindingFlags.NonPublic);
            var after=typeof(ShoulderSolverPatches).GetMethod("After",BindingFlags.Static|BindingFlags.NonPublic);
            object[] args={_femaleSession.FullBodyIk.solver,default(ShoulderSolveScope)};
            before.Invoke(null,args);
            Exception failure=null;
            try { body(); } catch(Exception ex) { failure=ex; }
            var result=(Exception)after.Invoke(null,new[]{failure,args[1]});
            if(result!=null) throw result;
        }
    }
}
