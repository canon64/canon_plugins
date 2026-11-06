using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace MainGameHLocationPlacement
{
    internal sealed class RuntimeHooks : IDisposable
    {
        private readonly Harmony _harmony = new Harmony(Plugin.Guid + ".runtime");
        internal RuntimeHooks(RuntimeBridge bridge, Type scene)
        {
            var composer = bridge.Composer.GetType(); var hip = bridge.Hip.GetType();
            try
            {
                Patch(composer, "SaveCurrentToPreset", nameof(SavingCurrent), finalizer: nameof(SaveEnded));
                Patch(composer, "CommitPresetDraft", nameof(SavingDraft), finalizer: nameof(SaveEnded));
                Patch(composer, "SaveJsonAtomic", nameof(AtomicSaving), finalizer: nameof(AtomicSaved));
                Patch(composer, "SavePresets", postfix: nameof(RulesChanged));
                Patch(composer, "SaveMotionPresetMutation", postfix: nameof(RulesChanged));
                Patch(composer, "GetEffectiveMotionPositionOffset", nameof(MotionOffset));
                Patch(composer, "ApplyMovementPose", nameof(MovementEvaluationStarted), finalizer: nameof(MovementEvaluationEnded));
                Patch(composer, "FindMovementField", nameof(MovementRule));
                Patch(composer, "BeginMovementEdit", nameof(MovementEditorStarted));
                Patch(composer, "EndMovementEdit", nameof(MovementEditorEnded));
                Patch(composer, "ActivatePreset", nameof(ComposerLoading), postfix: nameof(ComposerLoaded));
                Patch(composer, "LoadPresetSlot", nameof(ComposerSlotLoading), postfix: nameof(ComposerLoaded));
                Patch(composer, "ApplyPresetEditorFixedSlot", nameof(FixedSlotLoading), postfix: nameof(FixedSlotLoaded));
                Patch(composer, "DeactivatePreset", postfix: nameof(ComposerDeactivated));
                Patch(composer, "RebuildActivePresetCache", postfix: nameof(CachesRebuilt));
                Patch(composer, "ResolveConfiguredParentTransform", postfix: nameof(ParentResolved));
                Patch(composer, "SyncDataFromRuntime", postfix: nameof(DataSynced));
                Patch(hip, "SaveCurrentPosePresetWithScreenshot", nameof(SavingHipNew), finalizer: nameof(SaveEnded));
                Patch(hip, "OverwritePosePresetById", nameof(SavingHipOverwrite), finalizer: nameof(SaveEnded));
                Patch(hip, "BuildCurrentPosePresetSnapshot", postfix: nameof(HipSnapshotBuilt));
                Patch(hip, "SavePosePresetIndex", transpiler: nameof(HipWriter));
                Patch(hip, "ApplyPosePreset", nameof(HipApplying), postfix: nameof(HipApplied), transpiler: nameof(HipFilter));
                Patch(scene, "ChangeAnimator", nameof(NativeMoving), finalizer: nameof(NativeMoved));
                Patch(scene, "SetLocalPosition", nameof(NativeMoving), finalizer: nameof(NativeMoved));
            }
            catch { Dispose(); throw; }
        }
        private void Patch(Type type, string name, string prefix = null, string postfix = null, string finalizer = null, string transpiler = null)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).Where(m => m.Name == name).ToArray();
            if (methods.Length != 1) throw new MissingMethodException(type.FullName, name);
            HarmonyMethod Hook(string method) => method == null ? null : new HarmonyMethod(typeof(RuntimeHooks), method) { priority = Priority.Last };
            _harmony.Patch(methods[0], Hook(prefix), Hook(postfix), Hook(transpiler), Hook(finalizer), null);
        }
        private static PlacementSession Current => PlacementSession.Current?.Valid == true ? PlacementSession.Current : null;
        private static bool SavingCurrent(object __0, string __1, int __2, ref SaveOperation __state)
        {
            var c = Current; if (c == null) return true;
            try { __state = c.StartComposerSave(__0, __2, __0 == null); return true; }
            catch (Exception ex) { R.Set(c.Bridge.Composer, "_presetSaveSucceeded", false); c.Report(ex); return false; }
        }
        private static bool SavingDraft(bool __0, string __1, ref SaveOperation __state)
        {
            var c = Current; if (c == null) return true;
            try { __state = c.StartComposerSave(R.Get(c.Bridge.Composer, "_presetSource"), 0, __1 != null); return true; }
            catch (Exception ex) { R.Set(c.Bridge.Composer, "_presetSaveSucceeded", false); R.Set(c.Bridge.Composer, "_presetSaveFailed", true); c.Report(ex); return false; }
        }
        private static bool SavingHipNew(ref SaveOperation __state)
        { var c = Current; if (c == null) return true; try { __state = c.StartHipSave(null); return true; } catch (Exception ex) { c.Report(ex); return false; } }
        private static bool SavingHipOverwrite(string __0, ref SaveOperation __state)
        { var c = Current; if (c == null) return true; try { __state = c.StartHipSave(__0); return true; } catch (Exception ex) { c.Report(ex); return false; } }
        private static void HipSnapshotBuilt(object __result) => Current?.HipSnapshotCreated(__result);
        private static Exception SaveEnded(SaveOperation __state, Exception __exception)
        {
            try { Current?.EndSave(__state, __exception); } catch (Exception ex) { Current?.Report(ex); return __exception ?? ex; }
            return __exception;
        }
        private static void AtomicSaving(string __0, string __1, ref OriginStore.Pending __state)
        { __state = Current?.PrepareSave(__0, __1, PresetDocuments.Composer); }
        private static Exception AtomicSaved(OriginStore.Pending __state, Exception __exception)
        {
            if (__state == null) return __exception;
            try { if (__exception == null) { __state.Commit(); Current?.SaveCompleted(); } }
            catch (Exception ex) { Current?.Report(ex); return __exception ?? ex; }
            finally { __state.Dispose(); }
            return __exception;
        }
        private static void ComposerLoading(object __0)
        { var c = Current; if (c == null) return; try { c.SelectComposer(__0); } catch (Exception ex) { c.ClearComposer(); c.Report(ex); } }
        private static void ComposerLoaded() => Current?.TraceComposerApplied();
        private static void ComposerSlotLoading(object __0, bool __1)
        { var c = Current; if (c == null) return; try { c.SelectComposer(__0, __1 ? "loop2Json" : "loop1Json"); } catch (Exception ex) { c.ClearComposer(); c.Report(ex); } }
        private static void FixedSlotLoading()
        {
            var c = Current; if (c == null) return;
            try
            {
                var state = R.Get(c.Bridge.Composer, "_presetEditorState");
                c.SelectComposer(R.Get(c.Bridge.Composer, "_presetDraft"), R.Get<int>(state, "FixedSlot") == 2 ? "loop2Json" : "loop1Json");
            }
            catch (Exception ex) { c.ClearComposer(); c.Report(ex); }
        }
        private static void FixedSlotLoaded() { var c = Current; if (c == null) return; try { R.Call(c.Bridge.Composer, "RebuildActivePresetCache"); } catch (Exception ex) { c.Report(ex); } }
        private static void ComposerDeactivated() => Current?.ClearComposer();
        private static void CachesRebuilt() { var c = Current; if (c == null) return; try { c.NormalizeCaches(); } catch (Exception ex) { c.ClearComposer(); c.Report(ex); } }
        private static void ParentResolved(object __0, ref Transform __result, ref string __2, ref string __3)
        {
            var c = Current; if (c == null) return;
            __result = c.ParentFor(__0, __result);
            // Original logical identity is retained even though the actual parent is private.
            __2 = R.Get<string>(__0, "parentKind"); __3 = R.Get<string>(__0, "parentRefId");
        }
        private static void DataSynced(object __0) => Current?.RestoreIdentity(__0);
        private static void RulesChanged() { var c = Current; if (c == null) return; try { c.BuildRuleProjection(); } catch (Exception ex) { c.Report(ex); } }
        private static bool MotionOffset(string __0, ref Vector3 __result)
        { var c = Current; if (c == null || !c.TryMotionOffset(__0, out var result)) return true; __result = result; return false; }
        private static void MovementEvaluationStarted() => Current?.BeginMovementEvaluation();
        private static Exception MovementEvaluationEnded(Exception __exception) { Current?.EndMovementEvaluation(); return __exception; }
        private static bool MovementRule(object __0, string __1, string __2, ref object __result)
        { var c = Current; if (c == null || !c.TryMovementRule(__0, __1, __2, out var result)) return true; __result = result; return false; }
        private static void MovementEditorStarted(object __0, int __1)
        { var c = Current; if (c == null) return; try { c.SelectComposer(__0, __1 == 2 ? "loop2Json" : "loop1Json"); } catch (Exception ex) { c.ClearComposer(); c.Report(ex); } }
        private static void MovementEditorEnded()
        { var c = Current; if (c == null) return; try { var preset = R.Get(c.Bridge.Composer, "_movementReturnPreset"); if (preset == null) c.ClearComposer(); else c.SelectComposer(preset); } catch (Exception ex) { c.ClearComposer(); c.Report(ex); } }
        private static void HipApplying(ref object __0)
        { var c = Current; if (c == null) return; try { __0 = c.PrepareHip(__0); } catch (Exception ex) { c.Report(ex); } }
        private static void HipApplied(bool __result, string __2) => Current?.TraceHipApplied(__result, __2);
        private static void NativeMoving(object __instance, MethodBase __originalMethod, ref Movement __state)
        { var c = Current; if (c == null) return; try { __state = c.StartNativeMove(__instance, c.DiagnosticsEnabled ? __originalMethod.Name : null); } catch (Exception ex) { c.Report(ex); } }
        private static Exception NativeMoved(object __instance, Movement __state, Exception __exception)
        { var c = Current; if (c != null) try { c.EndNativeMove(__instance, __state, __exception); } catch (Exception ex) { c.Report(ex); } return __exception; }
        internal static void WriteHipIndex(string path, string json, Encoding encoding)
        { var c = Current; if (c == null) File.WriteAllText(path, json, encoding); else c.WriteHip(path, json, encoding); }
        internal static bool SkipHipEntry(object state) => Current?.SkipHipPoseEntry(state) ?? R.Get<bool>(state, "ExternalOwner");
        internal static IEnumerable<CodeInstruction> HipWriter(IEnumerable<CodeInstruction> instructions)
        {
            int count = 0;
            var write = typeof(File).GetMethod(nameof(File.WriteAllText), new[] { typeof(string), typeof(string), typeof(Encoding) });
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(write)) { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(RuntimeHooks), nameof(WriteHipIndex)); count++; }
                yield return instruction;
            }
            if (count != 1) throw new InvalidOperationException("HipHijackの保存完了接続が未対応です。");
        }
        internal static IEnumerable<CodeInstruction> HipFilter(IEnumerable<CodeInstruction> instructions)
        {
            int count = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldfld && instruction.operand is FieldInfo field && field.Name == "ExternalOwner"
                    && field.DeclaringType.FullName == "MainGirlHipHijack.Runtime.BodyIkOperationPointState")
                { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(RuntimeHooks), nameof(SkipHipEntry)); count++; }
                yield return instruction;
            }
            if (count != 1) throw new InvalidOperationException("HipHijackの連動IK除外接続が未対応です。");
        }
        public void Dispose() => _harmony.UnpatchSelf();
    }
}
