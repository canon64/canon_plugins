using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace MainGameAiShoujoMode
{
    // The real Action scene is loaded additively to preserve the mode's title/return owner.
    // Extend the VRMOD's existing scene decision; do not add a second input or camera update loop.
    internal sealed class NativeActionVrSceneScope : IDisposable
    {
        private static NativeActionVrSceneScope _active;
        private readonly Harmony _patches;
        private readonly object _interpreter;
        private readonly MethodInfo _update;
        private readonly int _actionKind;
        private readonly Func<IDisposable> _acquireControls;
        private IDisposable _controls;
        private bool _enabled, _disposed, _ownedMotion;

        internal NativeActionVrSceneScope(ActionScene action)
            : this(action, FindType("VRGIN_OpenXR", "VRGIN.Core.VR"),
                FindType("KKS_MainGameVR", "KKS_VR.Interpreters.KoikatuInterpreter"),
                FindType("KKS_MainGameVR", "KKS_VR.Interpreters.ActionSceneInterpreter"),
                () => new NativeActionControlScope(FindType("VRGIN_OpenXR", "VRGIN.Core.VR"))) { }

        internal NativeActionVrSceneScope(ActionScene action, Type vrType, Type interpreterType)
            : this(action, vrType, interpreterType, null, null) { }

        internal NativeActionVrSceneScope(ActionScene action, Type vrType, Type interpreterType,
            Type walkingInterpreterType, Func<IDisposable> acquireControls)
        {
            if (_active != null) throw new InvalidOperationException("The mode already owns a VR scene binding.");
            if (action == null || action.Player == null || !action.Player.initialized)
                throw new InvalidOperationException("The real native player must be initialized before VR binding.");
            if (vrType == null || interpreterType == null || !(bool)vrType.GetProperty("Active").GetValue(null, null))
                throw new InvalidOperationException("The installed VRMOD is not active.");
            _interpreter = vrType.GetProperty("Interpreter").GetValue(null, null);
            if (!interpreterType.IsInstanceOfType(_interpreter)) throw new InvalidOperationException("The installed VR scene interpreter is not available.");
            var detect = AccessTools.Method(interpreterType, "DetectScene") ?? throw new MissingMethodException("VRMOD DetectScene");
            _update = AccessTools.Method(interpreterType, "UpdateScene") ?? throw new MissingMethodException("VRMOD UpdateScene");
            if (!detect.ReturnType.IsEnum || Enum.GetUnderlyingType(detect.ReturnType) != typeof(int))
                throw new InvalidOperationException("The installed VR scene decision has an unsupported signature.");
            _actionKind = Convert.ToInt32(Enum.Parse(detect.ReturnType, "ActionScene"));
            _acquireControls = acquireControls;
            _patches = new Harmony("canon.MainGameAiShoujoMode.NativeActionVrScene");
            _active = this;
            try
            {
                _patches.Patch(detect, transpiler: new HarmonyMethod(typeof(NativeActionVrSceneScope), nameof(ExtendSceneDecision)));
                if (walkingInterpreterType != null)
                    _patches.Patch(AccessTools.Method(walkingInterpreterType, "OnUpdate"),
                        transpiler: new HarmonyMethod(typeof(NativeActionVrSceneScope), nameof(ExtendWalkingFollow)));
            }
            catch { _active = null; _patches.UnpatchSelf(); throw; }
        }
        private static Type FindType(string assembly, string name) => AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(item => item.GetName().Name == assembly)?.GetType(name);

        // This replaces part of a decision the VRMOD already makes. Only an event-owned flag is read.
        // No Unity searches, extra frame callbacks, input subscriptions or rig pose writes are added.
        private static bool UsesNativeAction() => _active != null && _active._enabled && !_active._disposed;
        private static bool ShouldFollowWalking(bool nativeWalking) => nativeWalking || (UsesNativeAction() && _active._ownedMotion);
        private static IEnumerable<CodeInstruction> ExtendWalkingFollow(IEnumerable<CodeInstruction> instructions)
        {
            bool found = false;
            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (instruction.opcode != OpCodes.Ldfld || !(instruction.operand is FieldInfo field) || field.Name != "_Walking" || field.FieldType != typeof(bool)) continue;
                if (found) throw new InvalidOperationException("Native VR walking follow condition is ambiguous.");
                found = true;
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NativeActionVrSceneScope), nameof(ShouldFollowWalking)));
            }
            if (!found) throw new MissingMemberException("Native VR walking follow condition");
        }
        internal void SetWalkingMotion(bool moving) { if (!_disposed) _ownedMotion = moving; }
        private static IEnumerable<CodeInstruction> ExtendSceneDecision(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var original = instructions.ToList();
            if (!original.Any() || _active == null) throw new InvalidOperationException("The native VR decision cannot be extended.");
            Label normal = generator.DefineLabel(); original.First().labels.Add(normal);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NativeActionVrSceneScope), nameof(UsesNativeAction)));
            yield return new CodeInstruction(OpCodes.Brfalse, normal);
            yield return new CodeInstruction(OpCodes.Ldc_I4, _active._actionKind);
            yield return new CodeInstruction(OpCodes.Ret);
            foreach (var instruction in original) yield return instruction;
        }
        internal void Resume()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NativeActionVrSceneScope));
            if (_enabled) return;
            _enabled = true;
            try { _update.Invoke(_interpreter, null); _controls = _acquireControls?.Invoke(); }
            catch { _enabled = false; _controls?.Dispose(); _controls = null; _update.Invoke(_interpreter, null); throw; }
        }
        internal void Suspend()
        {
            if (_disposed || !_enabled) return;
            _controls?.Dispose(); _controls = null;
            _ownedMotion = false;
            _enabled = false;
            _update.Invoke(_interpreter, null);
        }
        internal void AlignToPlayer()
        {
            if (_disposed || !_enabled) return;
            var scene = AccessTools.Field(_interpreter.GetType(), "SceneInterpreter")?.GetValue(_interpreter);
            var align = scene == null ? null : AccessTools.Method(scene.GetType(), "MoveCameraToPlayer", new[] { typeof(bool), typeof(bool) });
            if (align == null) throw new MissingMethodException("Native VR walking camera alignment");
            align.Invoke(scene, new object[] { false, true });
        }
        public void Dispose()
        {
            if (_disposed) return;
            _controls?.Dispose(); _controls = null;
            _ownedMotion = false;
            _enabled = false; _disposed = true;
            _patches.UnpatchSelf();
            if (ReferenceEquals(_active, this)) _active = null;
            // The original decision performs native StopWalking and camera release on exit.
            _update.Invoke(_interpreter, null);
        }
    }
}
