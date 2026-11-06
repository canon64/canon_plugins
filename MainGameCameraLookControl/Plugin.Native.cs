using SessionGate = CanonPlugins.Local.SessionGate;
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MainGameCameraLookControl
{
    public sealed partial class Plugin
    {
        [ThreadStatic] private static NeckLookControllerVer2 _callingController;
        private static readonly FieldInfo InitField = AccessTools.Field(typeof(NeckLookCalcVer2), "initEnd");
        private static readonly FieldInfo BrokenField = AccessTools.Field(typeof(NeckTypeStateVer2), "isLimitBreakBackup");
        private readonly CameraTargets _cameras = new CameraTargets();
        private readonly Dictionary<NeckLookCalcVer2, CalcRuntime> _runtimes = new Dictionary<NeckLookCalcVer2, CalcRuntime>();
        private readonly List<Evaluation> _evaluations = new List<Evaluation>();
        private readonly List<NeckLookCalcVer2> _deadRuntimes = new List<NeckLookCalcVer2>();
        private LatestState _latest;

        private sealed class LatestState
        {
            internal NeckLookControllerVer2 Controller;
            internal ChaControl Female;
            internal int Pattern;
            internal float NativeNeckUp, EffectiveNeckUp, NativeHeadUp, EffectiveHeadUp;
            internal float NativeField, EffectiveField, Pitch, Yaw;
            internal bool Broken;
        }

        private sealed class CalcRuntime
        {
            internal ChaControl Female;
            internal bool OwnerResolved;
            internal NeckObjectVer2[] Bones;
            internal Transform[] Transforms;
            internal RollLayer Roll;
            internal float[] Rolls;
            internal int RollFrame = -1;
            internal int LastActiveFrame = -2;
            internal readonly Stack<NeckLimitParameter[]> Scratch = new Stack<NeckLimitParameter[]>();

            internal void BindBones(NeckObjectVer2[] bones)
            {
                bool same = Bones == bones && Transforms != null && Transforms.Length == bones.Length;
                if (same)
                    for (int i = 0; i < bones.Length; i++) same &= Transforms[i] == bones[i]?.neckBone;
                if (same) return;
                Roll?.Remove();
                Bones = bones;
                Transforms = new Transform[bones.Length];
                for (int i = 0; i < bones.Length; i++) Transforms[i] = bones[i].neckBone;
                Roll = new RollLayer(Transforms);
                Rolls = new float[bones.Length];
                RollFrame = -1;
            }

            internal NeckLimitParameter[] Rent(int count)
            {
                while (Scratch.Count > 0)
                {
                    var result = Scratch.Pop();
                    if (result.Length == count) return result;
                }
                var rows = new NeckLimitParameter[count];
                for (int i = 0; i < count; i++) rows[i] = new NeckLimitParameter();
                return rows;
            }
        }

        private sealed class Evaluation
        {
            internal NeckLookCalcVer2 Calc;
            internal NeckLookControllerVer2 Controller;
            internal CalcRuntime Runtime;
            internal LookPolicy Policy;
            internal NativeParameterLease Lease;
            internal NeckTypeStateVer2 State;
            internal float Yaw;
            internal float EffectiveSpeed;
        }

        private void PatchNativeMethods()
        {
            if (InitField == null || BrokenField == null) throw new MissingFieldException("Native neck state fields changed");
            MethodInfo controller = AccessTools.Method(typeof(NeckLookControllerVer2), "LateUpdate", Type.EmptyTypes);
            MethodInfo calc = AccessTools.Method(typeof(NeckLookCalcVer2), "NeckUpdateCalc", new[] { typeof(Vector3), typeof(int), typeof(bool) });
            MethodInfo angles = AccessTools.Method(typeof(NeckLookCalcVer2), "GetAngleToTarget", new[] { typeof(Vector3), typeof(NeckObjectVer2), typeof(float) });
            if (controller == null || calc == null || angles == null) throw new MissingMethodException("Native camera-look method signatures changed");
            _harmony.Patch(controller, prefix: new HarmonyMethod(typeof(Plugin), nameof(ControllerPrefix)),
                finalizer: new HarmonyMethod(typeof(Plugin), nameof(ControllerFinalizer)));
            _harmony.Patch(calc, prefix: new HarmonyMethod(typeof(Plugin), nameof(CalculatorPrefix)) { priority = Priority.Last },
                postfix: new HarmonyMethod(typeof(Plugin), nameof(CalculatorPostfix)) { priority = Priority.Last },
                finalizer: new HarmonyMethod(typeof(Plugin), nameof(CalculatorFinalizer)) { priority = Priority.Last });
            _harmony.Patch(angles, null, new HarmonyMethod(typeof(Plugin), nameof(AnglesPostfix)) { priority = Priority.Last });
        }

        private static void ControllerPrefix(NeckLookControllerVer2 __instance, out NeckLookControllerVer2 __state)
        {
            __state = _callingController;
            _callingController = __instance;
            if (_instance != null && __instance.neckLookScript != null &&
                _instance._runtimes.TryGetValue(__instance.neckLookScript, out CalcRuntime runtime))
                runtime.Roll?.Remove();
        }

        private static Exception ControllerFinalizer(Exception __exception, NeckLookControllerVer2 __state)
        {
            _callingController = __state;
            return __exception;
        }

        private CalcRuntime GetRuntime(NeckLookCalcVer2 calc, NeckLookControllerVer2 controller)
        {
            if (!_runtimes.TryGetValue(calc, out CalcRuntime runtime))
            {
                runtime = new CalcRuntime();
                _runtimes.Add(calc, runtime);
            }
            if (!runtime.OwnerResolved)
            {
                runtime.OwnerResolved=true;
                runtime.Female = controller.GetComponentInParent<ChaControl>();
                if (runtime.Female == null)
                    foreach (ChaControl candidate in SessionGate.Actors)
                        if (candidate.neckLookCtrl == controller) { runtime.Female = candidate; break; }
            }
            return runtime;
        }

        private static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis)
        {
            from -= Vector3.Project(from, axis);
            to -= Vector3.Project(to, axis);
            return Vector3.Angle(from, to) * (Vector3.Dot(axis, Vector3.Cross(from, to)) < 0 ? -1f : 1f);
        }

        private Evaluation BeginEvaluation(NeckLookCalcVer2 calc, Vector3 target, int pattern, bool backup)
        {
            // A recursive ForceLateUpdate already sees the outer lease. Do not
            // add the same range extension to that lease a second time.
            for (int i = _evaluations.Count - 1; i >= 0; i--)
                if (_evaluations[i].Calc == calc) return null;
            NeckLookControllerVer2 controller = _callingController;
            if (!SessionGate.Allowed || !isActiveAndEnabled || !_settings.Enabled.Value || controller == null ||
                controller.neckLookScript != calc || backup || !calc.isEnabled ||
                (!calc.skipCalc && Time.deltaTime == 0f) || !(bool)InitField.GetValue(calc) ||
                pattern < 0 || calc.neckTypeStates == null || pattern >= calc.neckTypeStates.Length ||
                !_cameras.IsCamera(controller.target)) return null;
            NeckTypeStateVer2 state = calc.neckTypeStates[pattern];
            if (state == null || state.lookType != NECK_LOOK_TYPE_VER2.TARGET || state.aParam == null ||
                calc.aBones == null || calc.aBones.Length == 0 || state.aParam.Length != calc.aBones.Length ||
                calc.boneCalcAngle == null || calc.transformAim == null) return null;
            for (int i = 0; i < calc.aBones.Length; i++)
                if (state.aParam[i] == null || calc.aBones[i]?.neckBone == null) return null;
            CalcRuntime runtime = GetRuntime(calc, controller);
            if (runtime.Female == null || runtime.Female.sex != 1) return null;
            runtime.BindBones(calc.aBones);
            if (runtime.LastActiveFrame < Time.frameCount - 1)
            {
                Array.Clear(runtime.Rolls, 0, runtime.Rolls.Length);
                runtime.RollFrame = -1;
            }
            runtime.LastActiveFrame = Time.frameCount;
            var evaluation = new Evaluation
            {
                Calc = calc, Controller = controller, Runtime = runtime, Policy = _policy, State = state
            };
            Vector3 direction = target - calc.boneCalcAngle.position;
            float pitch = SignedAngle(calc.boneCalcAngle.forward, direction, calc.boneCalcAngle.right);
            float yaw = SignedAngle(calc.boneCalcAngle.forward, direction, calc.boneCalcAngle.up);
            evaluation.Yaw = _policy.Yaw(yaw);
            float nativeField = state.vAngleLimit;
            float nativeNeckUp = state.aParam[0].upBendingAngle;
            float nativeHeadUp = state.aParam[state.aParam.Length - 1].upBendingAngle;
            evaluation.Lease = new NativeParameterLease(state, _policy, pitch, yaw, runtime.Rent(state.aParam.Length));
            try
            {
                evaluation.EffectiveSpeed = state.leapSpeed;
                _evaluations.Add(evaluation);
                bool changed = _latest == null || _latest.Controller != controller || _latest.Pattern != pattern;
                if (_latest == null) _latest = new LatestState();
                _latest.Controller = controller; _latest.Female = runtime.Female; _latest.Pattern = pattern;
                _latest.NativeNeckUp = -nativeNeckUp; _latest.NativeHeadUp = -nativeHeadUp;
                _latest.EffectiveNeckUp = -state.aParam[0].upBendingAngle;
                _latest.EffectiveHeadUp = -state.aParam[state.aParam.Length - 1].upBendingAngle;
                _latest.NativeField = nativeField; _latest.EffectiveField = state.vAngleLimit;
                if (changed && _settings.Diagnostics.Value)
                    Logger.LogInfo("Camera look target=" + runtime.Female.name + " pattern=" + pattern + " camera=" + controller.target.name);
                return evaluation;
            }
            catch
            {
                evaluation.Lease.Dispose();
                runtime.Scratch.Push(evaluation.Lease.Effective);
                _evaluations.Remove(evaluation);
                throw;
            }
        }

        private static void CalculatorPrefix(NeckLookCalcVer2 __instance, Vector3 target, int ptnNo,
            bool _isUseBackUpPos, out Evaluation __state)
        {
            __state = null;
            if (_instance == null) return;
            try { __state = _instance.BeginEvaluation(__instance, target, ptnNo, _isUseBackUpPos); }
            catch (Exception ex) { _instance.Warn("Camera-look setup failed: " + ex.Message); }
        }

        private static void AnglesPostfix(NeckLookCalcVer2 __instance, ref Vector2 __result)
        {
            if (_instance == null) return;
            for (int i = _instance._evaluations.Count - 1; i >= 0; i--)
            {
                Evaluation evaluation = _instance._evaluations[i];
                if (evaluation.Calc != __instance) continue;
                __result = new Vector2(evaluation.Policy.Pitch(__result.x), evaluation.Policy.Yaw(__result.y));
                evaluation.Yaw = __result.y;
                if (_instance._latest != null)
                {
                    _instance._latest.Pitch = __result.x;
                    _instance._latest.Yaw = __result.y;
                }
                return;
            }
        }

        private static void CalculatorPostfix(Evaluation __state)
        {
            if (__state == null || _instance == null) return;
            try
            {
                bool broken = (bool)BrokenField.GetValue(__state.State);
                _instance._latest.Broken = broken;
                CalcRuntime runtime = __state.Runtime;
                if (runtime.RollFrame != Time.frameCount)
                {
                    runtime.RollFrame = Time.frameCount;
                    float blend = __state.Calc.skipCalc ? 1f : Mathf.Clamp01(Time.deltaTime * __state.EffectiveSpeed);
                    for (int i = 0; i < runtime.Rolls.Length; i++)
                        runtime.Rolls[i] = Mathf.Lerp(runtime.Rolls[i], broken ? 0f :
                            LookPolicy.Roll(__state.Policy.Bone(i, runtime.Rolls.Length), __state.Yaw), blend);
                }
                runtime.Roll.Apply(runtime.Rolls);
            }
            catch (Exception ex) { _instance.Warn("Camera-look roll update failed: " + ex.Message); }
        }

        private static Exception CalculatorFinalizer(Exception __exception, Evaluation __state)
        {
            if (__state != null)
            {
                __state.Lease.Dispose();
                __state.Runtime.Scratch.Push(__state.Lease.Effective);
                _instance?._evaluations.Remove(__state);
            }
            return __exception;
        }

        private void ClearRollLayers()
        {
            foreach (CalcRuntime runtime in _runtimes.Values)
            {
                runtime.Roll?.Remove();
                if (runtime.Rolls != null) Array.Clear(runtime.Rolls, 0, runtime.Rolls.Length);
                runtime.RollFrame = -1;
            }
        }

        private void CleanupRuntimes()
        {
            _deadRuntimes.Clear();
            foreach (var item in _runtimes)
                if (item.Key == null) { item.Value.Roll?.Remove(); _deadRuntimes.Add(item.Key); }
            foreach (var calc in _deadRuntimes) _runtimes.Remove(calc);
        }
    }
}
