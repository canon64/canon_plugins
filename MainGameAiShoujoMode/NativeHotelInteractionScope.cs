using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace MainGameAiShoujoMode
{
    // Consume the installed VRMOD's F3 action at its button event boundary.
    // Sending a desktop key loses this action whenever another window has focus.
    internal sealed class NativeHotelInteractionScope : IDisposable
    {
        private static NativeHotelInteractionScope _active;
        private readonly Harmony _patches;
        private readonly Func<bool> _ownsInput;
        private readonly Action _interact;
        private readonly HashSet<object> _pressed = new HashSet<object>();
        private bool _disposed;
        internal NativeHotelInteractionScope(Func<bool> ownsInput, Action interact)
            : this(AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "KKS_MainGameVR")
                ?.GetType("KKS_VR.Controls.ButtonsSubtool"), ownsInput, interact) { }
        internal NativeHotelInteractionScope(Type buttons, Func<bool> ownsInput, Action interact)
        {
            if (_active != null) throw new InvalidOperationException("VRホテルの入力所有が重複しています。");
            if (buttons == null) throw new InvalidOperationException("本編VRMODのボタン入力が見つかりません。");
            _ownsInput = ownsInput ?? throw new ArgumentNullException(nameof(ownsInput));
            _interact = interact ?? throw new ArgumentNullException(nameof(interact));
            _patches = new Harmony("canon.MainGameAiShoujoMode.NativeHotelInteraction");
            _active = this;
            try
            {
                var down = AccessTools.Method(buttons, "ButtonDown") ?? throw new MissingMethodException("VR ButtonDown");
                var up = AccessTools.Method(buttons, "ButtonUp") ?? throw new MissingMethodException("VR ButtonUp");
                _patches.Patch(down, prefix: new HarmonyMethod(typeof(NativeHotelInteractionScope), nameof(Down)));
                _patches.Patch(up, prefix: new HarmonyMethod(typeof(NativeHotelInteractionScope), nameof(Up)));
                var rotate = AccessTools.Method(buttons, "Rotate") ?? throw new MissingMethodException("VR Rotate");
                _patches.Patch(rotate, transpiler: new HarmonyMethod(typeof(NativeHotelInteractionScope), nameof(KeepFeetOnTurn)));
            }
            catch { Dispose(); throw; }
        }
        private static bool IsInteract(object[] args) => args.Length > default(int)
            && string.Equals(Convert.ToString(args[default(int)]), "F3", StringComparison.Ordinal);
        private static bool RotationOnly(bool nativeValue)
            => nativeValue || (_active != null && !_active._disposed && _active._ownsInput());
        private static IEnumerable<CodeInstruction> KeepFeetOnTurn(IEnumerable<CodeInstruction> instructions)
        {
            bool found = false;
            foreach (var instruction in instructions)
            {
                if (instruction.operand is MethodInfo method && method.Name == "MovePlayerToCamera"
                    && method.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(bool) }))
                {
                    if (found) throw new InvalidOperationException("VR snap-turn position handoff is ambiguous.");
                    found = true;
                    // Preserve labels so a branch cannot bypass the argument filter.
                    var filter = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NativeHotelInteractionScope), nameof(RotationOnly)));
                    filter.labels.AddRange(instruction.labels); instruction.labels.Clear();
                    yield return filter;
                }
                yield return instruction;
            }
            if (!found) throw new MissingMethodException("VR snap-turn MovePlayerToCamera handoff");
        }
        private static bool Down(object __instance, object[] __args)
        {
            var owner = _active;
            if (owner == null || owner._disposed || !IsInteract(__args) || !owner._ownsInput()) return true;
            if (owner._pressed.Add(__instance)) owner._interact();
            return false;
        }
        private static bool Up(object __instance, object[] __args)
        {
            var owner = _active;
            if (owner == null || owner._disposed || !IsInteract(__args)) return true;
            return !owner._pressed.Remove(__instance);
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _pressed.Clear();
            _patches.UnpatchSelf();
            if (ReferenceEquals(_active, this)) _active = null;
        }
    }
}
