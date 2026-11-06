using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using MainGameLogRelay;
using RootMotion.FinalIK;
using Hip = MainGirlHipHijack.Plugin;
using SessionGate = CanonPlugins.Local.SessionGate;

namespace MainGirlShoulderIkStabilizer
{
    public sealed partial class ShoulderIkStabilizerPlugin
    {
        private PoseDiagnosticWriter diagnosticWriter;
        private bool diagnosticIdentityWritten;
        private bool advShoulderHooked;
        private void OnShoulderPoseFaulted(Exception ex)
        {
            if (settings != null && settings.ShoulderDiagnosticLog) WriteDiagnostic("POSE_FAILED " + ex);
        }

        private void WriteDiagnostic(string text)
        {
            if (settings == null || !settings.ShoulderDiagnosticLog) return;
            if (diagnosticWriter == null)
                diagnosticWriter = new PoseDiagnosticWriter(Path.Combine(Path.GetDirectoryName(Info.Location), "log", "ShoulderPoseDiagnostic.log"), s => Logger.LogError(s));
            if (!diagnosticIdentityWritten)
            {
                diagnosticIdentityWritten = true;
                using (var process = Process.GetCurrentProcess())
                    diagnosticWriter.Write("IDENTITY version=" + Version + " pid=" + process.Id +
                        " process=" + process.ProcessName + " dll=\"" + Info.Location + "\" mvid=" + typeof(ShoulderIkStabilizerPlugin).Module.ModuleVersionId);
                diagnosticWriter.Write("SCHEMA 3 units=degrees,metres frame=UnityFrame sample=outerSolve pipeline=HipShoulderApi1 " +
                    "stages=solved,held,auto,final side=L/R upperElevation=down0/horizontal90/up180 " +
                    "yaw=outward0/forward90/behind-90 localEuler=UnityRawXYZ chestQ=quaternionXYZW " +
                    "fileIndependentOfRelay=true");
            }
            diagnosticWriter.Write(text);
        }
        private void ReportDiagnosticState(string reason)
        {
            if (settings == null || !settings.ShoulderDiagnosticLog) return;
            // Keep dependency/type resolution failures outside the initial file writer.
            try { ReportRuntimeDiagnosticState(reason); }
            catch (Exception ex) { WriteDiagnostic("STATE_FAILED reason=" + reason + " " + ex); }
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void ReportRuntimeDiagnosticState(string reason)
        {
            WriteDiagnostic("HIP_IDENTITY version=" + Hip.Version + " dll=\"" + typeof(Hip).Assembly.Location +
                "\" mvid=" + typeof(Hip).Module.ModuleVersionId + " shoulderApi=" + Hip.ShoulderPoseApiVersion);
            WriteDiagnostic(string.Format(CultureInfo.InvariantCulture,
                "STATE reason={0} gate={1} session={2} runtime={3} enabled={4} rotation={5} hipEnabled={6} armL={7} armR={8} subscribed={9} advHook={10} " +
                "independent={11} reachWeight={12:F3}/{13:F3} reachOffset={14:F3}/{15:F3} elevationWeight={16:F3}/{17:F3} start={18:F2}/{19:F2} full={20:F2}/{21:F2} maxLift={22:F2}/{23:F2} maxDelta={24:F2} maxBlend={25:F3} transition={26:F3} speed={27:F2}",
                reason, SessionGate.Allowed, session, currentIk == null ? 0 : currentIk.GetInstanceID(),
                settings.Enabled, settings.ShoulderRotationEnabled, hipEnabled, Hip.IsArmIkRunning(0), Hip.IsArmIkRunning(1), poseSubscribed, advShoulderHooked,
                settings.IndependentShoulders, settings.ShoulderWeight, settings.ShoulderRightWeight, settings.ShoulderOffset, settings.ShoulderRightOffset,
                settings.ElevationWeight, settings.ElevationRightWeight, settings.ElevationStartAngle, settings.ElevationRightStartAngle,
                settings.ElevationFullAngle, settings.ElevationRightFullAngle, settings.ElevationMaxLift, settings.ElevationRightMaxLift,
                settings.MaxShoulderDeltaAngleDeg, settings.MaxSolverBlend, settings.TransitionSeconds, settings.MaxAngularSpeed));
            WriteDiagnostic("ROUTE " + LogRelayApi.GetOwnerSummary(Guid));
            if (rotator != null) WriteDiagnostic(rotator.DescribeDiagnosticBinding());
        }
        private void CaptureSolvedPose(ChaControl actor, FullBodyBipedIK ik)
        {
            if (SessionGate.Allowed && ReferenceEquals(currentIk, ik)) rotator?.CaptureDiagnosticSolvedPose();
            if (SessionGate.Allowed && ReferenceEquals(currentIk, ik)) RecordNativeOrder("ik_solved");
        }
        private void CaptureCompletedPose(ChaControl actor, FullBodyBipedIK ik)
        {
            if (SessionGate.Allowed && ReferenceEquals(currentIk, ik)) rotator?.CompleteDiagnosticPose();
            if (SessionGate.Allowed && ReferenceEquals(currentIk, ik)) RecordNativeOrder("ik_completed");
        }
    }
}
