using System;
using CanonPlugins.ShoulderPose;
using HarmonyLib;
using RootMotion.FinalIK;
using SessionGate = CanonPlugins.Local.SessionGate;

namespace MainGirlHipHijack
{
    public sealed partial class Plugin
    {
        public const int ShoulderPoseApiVersion = 1;
        public static event Action<ChaControl, FullBodyBipedIK> ShoulderRuntimeChanged;
        public static event Action<ChaControl, FullBodyBipedIK> ShoulderPosePreparing;
        public static event Action<ChaControl, FullBodyBipedIK> ShoulderPoseSolved;
        public static event Action<ChaControl, FullBodyBipedIK, float, float> ShoulderPoseFinalizing;
        public static event Action<ChaControl, FullBodyBipedIK> ShoulderPoseCompleted;
        public static event Action<Exception> ShoulderPoseFaulted;
        private FullBodyBipedIK _shoulderRuntimeIk;
        private ChaControl _shoulderRuntimeActor;
        private readonly SolverPassState _shoulderPass = new SolverPassState();

        public static bool TryGetShoulderRuntime(out ChaControl actor, out FullBodyBipedIK ik)
        {
            var p = Instance;
            actor = p != null && SessionGate.Allowed ? p._shoulderRuntimeActor : null;
            ik = p != null && SessionGate.Allowed ? p._shoulderRuntimeIk : null;
            return actor != null && ik != null;
        }
        private static void ReportShoulderFault(Exception ex)
        {
            // The diagnostic subscriber owns the log switch; gameplay cleanup never depends on it.
            try { ShoulderPoseFaulted?.Invoke(ex); } catch { }
        }
        private void PublishShoulderRuntime()
        {
            var actor = _femaleSession.Character;
            var ik = _femaleSession.FullBodyIk;
            if (ReferenceEquals(actor, _shoulderRuntimeActor) && ReferenceEquals(ik, _shoulderRuntimeIk)) return;
            ClearShoulderRuntime();
            _shoulderRuntimeActor = actor; _shoulderRuntimeIk = ik;
            if (ik != null && ik.solver != null) ik.solver.OnPreRead += MarkShoulderSolved;
            try { ShoulderRuntimeChanged?.Invoke(actor, ik); } catch (Exception ex) { ReportShoulderFault(ex); }
        }
        private void ClearShoulderRuntime()
        {
            if (ReferenceEquals(_shoulderRuntimeActor, null) && ReferenceEquals(_shoulderRuntimeIk, null)) return;
            var previous = _shoulderRuntimeIk;
            _shoulderRuntimeActor = null; _shoulderRuntimeIk = null;
            _shoulderPass.Reset();
            if (previous != null && previous.solver != null) previous.solver.OnPreRead -= MarkShoulderSolved;
            try { ShoulderRuntimeChanged?.Invoke(null, null); } catch (Exception ex) { ReportShoulderFault(ex); }
        }
        private void MarkShoulderSolved() { if (SessionGate.Allowed) _shoulderPass.MarkSolved(); }
        private void PrepareShoulderPose()
        {
            try { ShoulderPosePreparing?.Invoke(_shoulderRuntimeActor, _shoulderRuntimeIk); }
            catch (Exception ex) { ReportShoulderFault(ex); }
        }
        private void FinishShoulderPose()
        {
            if (!SessionGate.Allowed || _shoulderRuntimeIk == null) return;
            try
            {
                ShoulderPoseSolved?.Invoke(_shoulderRuntimeActor, _shoulderRuntimeIk);
                if (!SessionGate.Allowed || _shoulderRuntimeIk == null) return;
                var solver = _shoulderRuntimeIk.solver;
                ShoulderPoseFinalizing?.Invoke(_shoulderRuntimeActor, _shoulderRuntimeIk,
                    UnityEngine.Mathf.Clamp01(solver.leftShoulderEffector.positionWeight),
                    UnityEngine.Mathf.Clamp01(solver.rightShoulderEffector.positionWeight));
                if (SessionGate.Allowed && _shoulderRuntimeIk != null)
                    ShoulderPoseCompleted?.Invoke(_shoulderRuntimeActor, _shoulderRuntimeIk);
            }
            catch (Exception ex) { ReportShoulderFault(ex); }
        }
        internal struct ShoulderSolveScope { internal Plugin Owner; internal int Token; }
        // Wrap the existing solver invocation, including nested solves, without driving the IK.
        internal static class ShoulderSolverPatches
        {
            [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyPatch(typeof(IKSolverFullBody), "StoreDefaultLocalState")]
            private static void BeforeStoreDefaults(IKSolverFullBody __instance)
            {
                var p = Instance;
                if (p != null && SessionGate.Allowed && p._shoulderRuntimeIk != null &&
                    ReferenceEquals(p._shoulderRuntimeIk.solver, __instance)) p.PrepareShoulderPose();
            }
            [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyPatch(typeof(IKSolver), "Update")]
            private static void Before(IKSolver __instance, ref ShoulderSolveScope __state)
            {
                var p = Instance;
                if (p == null || !SessionGate.Allowed || p._shoulderRuntimeIk == null ||
                    !ReferenceEquals(p._shoulderRuntimeIk.solver, __instance) ||
                    (ShoulderPosePreparing == null && ShoulderPoseSolved == null &&
                     ShoulderPoseFinalizing == null && ShoulderPoseCompleted == null)) return;
                bool outer = p._shoulderPass.IsOuter;
                __state.Owner = p; __state.Token = p._shoulderPass.Enter();
                if (outer) p.PrepareShoulderPose();
            }
            [HarmonyFinalizer, HarmonyPriority(Priority.Last), HarmonyPatch(typeof(IKSolver), "Update")]
            private static Exception After(Exception __exception, ShoulderSolveScope __state)
            {
                var p = __state.Owner;
                if (p != null)
                {
                    bool completed = p._shoulderPass.Exit(__state.Token, __exception == null);
                    if (__exception != null) ReportShoulderFault(__exception);
                    if (completed) p.FinishShoulderPose();
                }
                return __exception;
            }
        }
    }
}
