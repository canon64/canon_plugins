using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace MainGameVrNeckGrab
{
    public sealed partial class Plugin
    {
        ChaControl _gaugeActor;
        static readonly FieldInfo GaugeField = AccessTools.Field(typeof(HFlag), "gaugeFemale");
        static readonly MethodInfo GaugeUp = AccessTools.Method(typeof(HFlag), "FemaleGaugeUp");
        void Patch(MethodBase method, string prefix = null, string postfix = null, string transpiler = null, string finalizer = null)
        {
            if (method == null) throw new MissingMethodException("NeckGrab required hook target is missing");
            Func<string, HarmonyMethod> h = name => name == null ? null : new HarmonyMethod(typeof(Plugin), name);
            _harmony.Patch(method, prefix: h(prefix), postfix: h(postfix), transpiler: h(transpiler), finalizer: h(finalizer), ilmanipulator: null);
        }
        void InstallHooks()
        {
            Patch(GaugeUp, nameof(FemaleGaugePrefix));
            // Instrument actual writes, not a frame callback that overwrites the result later.
            foreach (Type t in typeof(HFlag).Assembly.GetTypes().Where(t => typeof(HActionBase).IsAssignableFrom(t) || t == typeof(HandCtrl) || t == typeof(HSprite) || t == typeof(HFlag)))
                foreach (var m in t.GetMethods(AccessTools.allDeclared).Where(m => !m.IsAbstract && !m.ContainsGenericParameters && m != GaugeUp && m.GetMethodBody() != null))
                {
                    var il = PatchProcessor.GetOriginalInstructions(m);
                    bool writes = il.Any(x => x.opcode == OpCodes.Stfld && Equals(x.operand, GaugeField));
                    bool reads = il.Any(x => x.opcode == OpCodes.Ldfld && Equals(x.operand, GaugeField));
                    bool adds = il.Any(x => Equals(x.operand, GaugeUp));
                    if (writes || adds || reads) Patch(m, writes || adds ? nameof(GaugeContextPrefix) : null,
                        transpiler: writes || reads ? nameof(GaugeTranspiler) : null, finalizer: writes || adds ? nameof(GaugeContextFinalizer) : null);
                }
            foreach (string name in new[] { "VoiceProc", "VoiceLinkProc" })
                Patch(AccessTools.Method(typeof(HVoiceCtrl), name), nameof(NativeDialoguePrefix), finalizer: nameof(NativeDialogueFinalizer));
            Patch(AccessTools.Method(typeof(HVoiceCtrl), "PlayVoice"), nameof(NativePlayPrefix), finalizer: nameof(NativeDialogueFinalizer));
            InstallNativeLoaderHooks();
            foreach (string name in new[] { "ChangeEyebrowPtn", "ChangeEyesPtn", "ChangeMouthPtn", "ChangeEyesOpenMax", "ChangeMouthOpenMax", "ChangeMouthFixed", "ChangeHohoAkaRate", "HideEyeHighlight", "ChangeEyesBlinkFlag", "ChangeLookEyesPtn", "ChangeLookEyesTarget" })
                foreach (var m in typeof(ChaControl).GetMethods(AccessTools.all).Where(m => m.Name == name && m.DeclaringType == typeof(ChaControl)))
                    Patch(m, nameof(FaceWritePrefix));
            foreach (var m in typeof(ChaControl).GetMethods(AccessTools.allDeclared).Where(m => m.Name == "setAnimatorParamFloat" && m.GetParameters().Length == 2))
                Patch(m, nameof(MotionWritePrefix));
            Patch(AccessTools.Method(typeof(HandCtrl), "DetachAllItem"), postfix: nameof(DetachAllPostfix));
            InstallExternalVoiceHooks();
            InstallOtherDollHook();
            InstallNeckPriorityHooks();
        }
        static void GaugeContextPrefix(object __instance, object[] __args, out ChaControl __state)
        {
            var p = Instance; __state = p?._gaugeActor;
            if (p == null || !p.Allowed) return;
            // Only actual provenance is used. Shared scene-wide updates belong to its primary female.
            p._gaugeActor = __args.OfType<ChaControl>().FirstOrDefault() ?? ReflectionAccess.Actor(__instance) ?? p._actors.FirstOrDefault()?.Actor;
        }
        static Exception GaugeContextFinalizer(Exception __exception, ChaControl __state)
        { if (Instance != null) Instance._gaugeActor = __state; return __exception; }
        static IEnumerable<CodeInstruction> GaugeTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Stfld && Equals(instruction.operand, GaugeField))
                { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(Plugin), nameof(WriteGauge)); }
                else if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, GaugeField))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(Plugin), __originalMethod.DeclaringType == typeof(HSprite) ? nameof(ReadDisplayGauge) : nameof(ReadGauge));
                }
                yield return instruction;
            }
        }
        static void WriteGauge(HFlag flags, float value)
        {
            var p = Instance;
            if (p == null || !p.RouteGauge(flags, value - ReadGauge(flags))) flags.gaugeFemale = value;
        }
        static float ReadGauge(HFlag flags)
        {
            var p = Instance;
            if (p == null || !p.Allowed || !ReferenceEquals(p._flags, flags)) return flags.gaugeFemale;
            var s = p.FindActor(p._gaugeActor ?? p._actors.FirstOrDefault()?.Actor);
            return s?.State.Doll == true ? s.FrozenGauge : flags.gaugeFemale;
        }
        static float ReadDisplayGauge(HFlag flags)
        {
            var p = Instance;
            return p != null && p.Allowed && ReferenceEquals(p._flags, flags) && p._displayed?.State.Doll == true
                ? p._displayed.FrozenGauge : ReadGauge(flags);
        }
        static bool FemaleGaugePrefix(HFlag __instance, float _addPoint, bool _force)
        {
            var p = Instance;
            if (p == null || !p.Allowed || !ReferenceEquals(p._flags, __instance)) return true;
            var s = p.FindActor(p._gaugeActor ?? p._actors.FirstOrDefault()?.Actor);
            if (s == null || !s.State.Doll) return true;
            // Preserve the frozen value even for decay. Respect the native lock for positive gains.
            float effective = !__instance.lockGugeFemale || _force ? _addPoint * __instance.potion : 0;
            p.RouteGauge(__instance, effective);
            return false;
        }
        bool RouteGauge(HFlag flags, float amount)
        {
            if (!Allowed || !ReferenceEquals(flags, _flags)) return false;
            var s = FindActor(_gaugeActor ?? _actors.FirstOrDefault()?.Actor);
            if (s == null || !s.State.Doll) return false;
            if (s.State.AddAwakening(amount)) Wake(s); else ShowAwakening(s);
            return true;
        }
        static bool FaceWritePrefix(ChaControl __instance)
        {
            var p = Instance;
            return DollSnapshot.InternalWrite || p == null || !p.Allowed || p.FindActor(__instance)?.State.Doll != true;
        }
        static void MotionWritePrefix(ChaControl __instance, object __0, ref float __1)
        {
            var p = Instance;
            if (DollSnapshot.InternalWrite || p == null || !p.Allowed) return;
            if (!Equals(__0, "motion") && !Equals(__0, p._motionHash)) return;
            var s = p.FindActor(__instance);
            if (s?.State.Doll == true && s.Doll != null && s.Doll.TryMotion(out float value)) __1 = value;
        }
        static void DetachAllPostfix(HandCtrl __instance)
        {
            var p = Instance;
            if (p == null || !p.Allowed) return;
            foreach (var s in p._actors.Where(s => s.NativeHand == __instance).ToArray()) p.RemoveNeckHand(s);
        }
        static bool NativeDialoguePrefix(HVoiceCtrl __instance, ChaControl _female, int _main, ref bool __result, out ChaControl __state)
        {
            var p = Instance;
            __state = p?._nativeDialogueActor;
            if (p == null || !p.Allowed || !ReferenceEquals(__instance, p._voice)) return true;
            if (p.FindActor(_female)?.State.Muted != true) { p._nativeDialogueActor = _female; return true; }
            p.ClearNativeDialogue(_main); __result = false; return false;
        }
        static bool NativePlayPrefix(HVoiceCtrl __instance, ChaControl _female, int _voiceKind, ref bool __result, out ChaControl __state)
        {
            var p = Instance;
            __state = p?._nativeDialogueActor;
            // HVoiceCtrl uses face kind 0 for breath, 1 for spoken dialogue at this boundary.
            if (p == null || !p.Allowed || !ReferenceEquals(__instance, p._voice)) return true;
            p._nativeDialogueActor = _voiceKind == 1 ? _female : null;
            if (_voiceKind != 1 || p.FindActor(_female)?.State.Muted != true) return true;
            __result = false; return false;
        }
        void ClearNativeDialogue(int index)
        {
            if (_flags.voice.playVoices != null && index >= 0 && index < _flags.voice.playVoices.Length) _flags.voice.playVoices[index] = -1;
            if (_voice.nowVoices != null && index >= 0 && index < _voice.nowVoices.Length)
            {
                var v = _voice.nowVoices[index]; v.notOverWrite = false;
                if (v.link != null && (v.link.kind == HVoiceCtrl.VoiceKind.voice || v.link.kind == HVoiceCtrl.VoiceKind.startvoice)) v.link = new HVoiceCtrl.LinkInfo();
            }
        }
        void StopDialogue(ActorState s)
        {
            foreach (var pending in _nativeVoiceTickets) if (ReferenceEquals(pending.Actor, s.Actor)) pending.Cancelled = true;
            int index = s.NativeIndex;
            ClearNativeDialogue(index);
            if (index >= 0 && index < _voice.nowVoices.Length && _voice.nowVoices[index].state != HVoiceCtrl.VoiceKind.breath && _voice.nowVoices[index].state != HVoiceCtrl.VoiceKind.breathShort)
            {
                if (index < _flags.transVoiceMouth.Length && _flags.transVoiceMouth[index] != null) Manager.Voice.Stop(_flags.transVoiceMouth[index]);
                s.Actor.SetLipSync(null);
                _voice.nowVoices[index].state = HVoiceCtrl.VoiceKind.breath;
            }
            StopExternalDialogue(s.Actor);
        }
    }
}
