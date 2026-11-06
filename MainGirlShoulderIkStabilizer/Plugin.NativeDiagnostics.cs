using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;

namespace MainGirlShoulderIkStabilizer
{
    public sealed partial class ShoulderIkStabilizerPlugin
    {
        private ChaControl diagnosticActor;
        private NativeShoulderDiagnostic nativeDiagnostic;
        private Harmony nativeDiagnosticHarmony;
        private long nativeDiagnosticPass;
        private int nativeDiagnosticFrame = -1;
        private bool NativeDiagnosticEnabled => settings != null && settings.ShoulderDiagnosticLog && session && SessionGate.Allowed;

        private void RefreshNativeDiagnostics()
        {
            nativeDiagnostic = null; nativeDiagnosticPass = 0; nativeDiagnosticFrame = -1;
            if (!NativeDiagnosticEnabled || diagnosticActor == null)
            {
                nativeDiagnosticHarmony?.UnpatchSelf(); nativeDiagnosticHarmony = null;
                return;
            }
            var expression = diagnosticActor.expression;
            string source = expression == null ? "absent" : "actor_field";
            if (expression == null)
            {
                expression = diagnosticActor.GetComponentInChildren<ExpressionBone>(true);
                if (expression != null) source = "component";
            }
            var actualBones = new Dictionary<string,Transform>(StringComparer.Ordinal);
            var boneRoot = diagnosticActor.objBodyBone;
            if (boneRoot != null)
                foreach (var bone in boneRoot.GetComponentsInChildren<Transform>(true))
                    if (!actualBones.ContainsKey(bone.name)) actualBones.Add(bone.name, bone);
            nativeDiagnostic = new NativeShoulderDiagnostic(expression, () => NativeDiagnosticEnabled, WriteDiagnostic,
                settings.ShoulderDiagnosticLogInterval, actualBones);
            if (nativeDiagnosticHarmony == null)
            {
                nativeDiagnosticHarmony = new Harmony(Guid + ".native-diagnostics");
                nativeDiagnosticHarmony.Patch(AccessTools.Method(typeof(ExpressionBone), "LateUpdate"),
                    prefix: new HarmonyMethod(typeof(ShoulderIkStabilizerPlugin), nameof(BeforeNativeExpression)),
                    finalizer: new HarmonyMethod(typeof(ShoulderIkStabilizerPlugin), nameof(AfterNativeExpression)));
                nativeDiagnosticHarmony.Patch(AccessTools.Method(typeof(ChaControl), "LateUpdateForce"),
                    finalizer: new HarmonyMethod(typeof(ShoulderIkStabilizerPlugin), nameof(AfterNativeShape)));
                nativeDiagnosticHarmony.Patch(AccessTools.Method(typeof(ChaControl), "InitializeExpression"),
                    finalizer: new HarmonyMethod(typeof(ShoulderIkStabilizerPlugin), nameof(AfterExpressionInitialized)));
            }
            WriteDiagnostic("NATIVE_BIND expression=" + (expression == null ? "missing" : expression.GetInstanceID().ToString()) +
                " actor=" + diagnosticActor.GetInstanceID() + " expressionSource=" + source +
                " cachedBodyBones=" + actualBones.Count + " bodyBoneRoot=" + (boneRoot == null ? "missing" : boneRoot.name) + " observerOnly=true");
            try { ReportSkinBinding(actualBones); } catch (Exception ex) { OnShoulderPoseFaulted(ex); }
        }
        private void RecordNativeOrder(string stage)
        {
            if (!NativeDiagnosticEnabled || nativeDiagnostic == null) return;
            try
            {
                long token = nativeDiagnostic.Begin(stage);
                if (token != 0) { nativeDiagnosticPass = token; nativeDiagnosticFrame = Time.frameCount; }
                else if (nativeDiagnosticFrame == Time.frameCount) nativeDiagnostic.Capture(stage, nativeDiagnosticPass);
            }
            catch (Exception ex) { OnShoulderPoseFaulted(ex); }
        }
        private static void BeforeNativeExpression(ExpressionBone __instance, out long __state)
        {
            __state = 0;
            var p = Instance;
            if (p == null || !p.NativeDiagnosticEnabled || p.nativeDiagnostic == null || !p.nativeDiagnostic.Matches(__instance)) return;
            p.RecordNativeOrder("expression_before");
            if (p.nativeDiagnosticFrame == Time.frameCount) __state = p.nativeDiagnosticPass;
        }
        private static Exception AfterNativeExpression(ExpressionBone __instance, long __state, Exception __exception)
        {
            var p = Instance;
            if (p != null && p.NativeDiagnosticEnabled && p.nativeDiagnostic != null && p.nativeDiagnostic.Matches(__instance))
            {
                try { p.nativeDiagnostic.Capture("expression_after", __state); }
                catch (Exception ex) { p.OnShoulderPoseFaulted(ex); }
                if (__exception != null) p.WriteDiagnostic("NATIVE_FAILED stage=expression " + __exception);
            }
            return __exception;
        }
        private static Exception AfterNativeShape(ChaControl __instance, Exception __exception)
        {
            var p = Instance;
            if (p != null && p.NativeDiagnosticEnabled && ReferenceEquals(p.diagnosticActor, __instance))
            {
                p.RecordNativeOrder("shape_after");
                if (__exception != null) p.WriteDiagnostic("NATIVE_FAILED stage=shape " + __exception);
            }
            return __exception;
        }
        private static Exception AfterExpressionInitialized(ChaControl __instance, Exception __exception)
        {
            var p = Instance;
            if (p != null && p.NativeDiagnosticEnabled && ReferenceEquals(p.diagnosticActor, __instance))
            {
                try { p.RefreshNativeDiagnostics(); }
                catch (Exception ex) { p.OnShoulderPoseFaulted(ex); }
                if (__exception != null) p.WriteDiagnostic("NATIVE_FAILED stage=initialize_expression " + __exception);
            }
            return __exception;
        }
    }
}
