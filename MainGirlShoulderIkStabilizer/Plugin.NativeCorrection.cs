using System;
using HarmonyLib;
using SessionGate = CanonPlugins.Local.SessionGate;

namespace MainGirlShoulderIkStabilizer
{
    public sealed partial class ShoulderIkStabilizerPlugin
    {
        private NativeShoulderCorrection nativeCorrection;
        private void BindNativeCorrection()
        {
            nativeCorrection?.Dispose(); nativeCorrection = null;
            if (!session || !SessionGate.Allowed || diagnosticActor == null) return;
            var expression = diagnosticActor.expression ?? diagnosticActor.GetComponentInChildren<ExpressionBone>(true);
            if (expression != null && expression.dictBone != null && currentIk != null &&
                expression.dictBone.TryGetValue((int)ExpressionBone.BoneName.cf_j_arm00_L, out var upper) &&
                !ReferenceEquals(upper, currentIk.solver.GetLimbMapping(RootMotion.FinalIK.FullBodyBipedChain.LeftArm)?.bone1))
            {
                if (settings.ShoulderDiagnosticLog) WriteDiagnostic("NATIVE_CORRECTION_BIND ready=False reason=solver_bone_mismatch");
                return;
            }
            nativeCorrection = new NativeShoulderCorrection(expression);
            if (settings.ShoulderDiagnosticLog) WriteDiagnostic("NATIVE_CORRECTION_BIND ready=" + nativeCorrection.Ready);
        }
        private void ApplyNativeCorrection()
        {
            if (!session || !SessionGate.Allowed || !settings.Enabled || !settings.ShoulderRotationEnabled ||
                !settings.NativeAuxiliaryCorrection || !hipEnabled || nativeCorrection == null) return;
            RecordNativeOrder("native_aux_before");
            try
            {
                bool applied = nativeCorrection.Apply(MainGirlHipHijack.Plugin.IsArmIkRunning(0), MainGirlHipHijack.Plugin.IsArmIkRunning(1));
                if (NativeDiagnosticEnabled && nativeDiagnosticFrame == UnityEngine.Time.frameCount)
                    WriteDiagnostic("NATIVE_CORRECTION frame=" + UnityEngine.Time.frameCount + " nativePass=" + nativeDiagnosticPass +
                        " applied=" + applied + " changedBones=" + nativeCorrection.ChangedBones);
            }
            catch (Exception ex) { OnShoulderPoseFaulted(ex); }
            RecordNativeOrder("native_aux_after");
        }
        private static void NativeExpressionInitialized(ChaControl __instance)
        {
            var p = Instance;
            if (p != null && SessionGate.Allowed && p.session && ReferenceEquals(p.diagnosticActor, __instance))
            {
                try { p.BindNativeCorrection(); } catch (Exception ex) { p.OnShoulderPoseFaulted(ex); }
            }
        }
    }
}
