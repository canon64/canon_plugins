using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace AstralMode
{
    // このファイルに時間進行の差し替えだけを隔離する。ksPlugの設定値は一切変更しない。
    internal static class KsPlugTimePatch
    {
        internal const string PatchId = "com.kks.maingame.astralmode.ksplugtime";
        private static Harmony _harmony;
        private static Func<bool> _shouldSuppress;

        internal static void Install(Func<bool> shouldSuppress, Action<string> warn)
        {
            if (_harmony != null) return;
            _shouldSuppress = shouldSuppress;
            _harmony = new Harmony(PatchId);
            foreach (string name in new[] { "ksPlug.HSession.HCtrl", "ksPlugVR.HSession.HCtrl" })
            {
                var type = AccessTools.TypeByName(name);
                if (type == null) continue;
                try
                {
                    var target = AccessTools.DeclaredMethod(type, "LateUpdate", Type.EmptyTypes);
                    if (target == null) throw new MissingMethodException(name, "LateUpdate");
                    _harmony.Patch(target, transpiler: new HarmonyMethod(typeof(KsPlugTimePatch), nameof(Transpile)));
                }
                catch (Exception ex)
                {
                    // 他のパッチは継続する。旧式の設定上書きには戻さない。
                    warn?.Invoke("ksPlug time patch skipped: " + name + ": " + ex.Message);
                }
            }
        }

        internal static void Remove()
        {
            _shouldSuppress = null;
            try { _harmony?.UnpatchSelf(); }
            finally { _harmony = null; }
        }

        internal static bool AllowTimeProgress(bool ksPlugEnabled)
        {
            return ksPlugEnabled && !(_shouldSuppress?.Invoke() ?? false);
        }

        internal static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var code = new List<CodeInstruction>();
            foreach (var instruction in instructions) code.Add(new CodeInstruction(instruction));
            var type = __originalMethod.DeclaringType;
            if (type == null || (type.FullName != "ksPlug.HSession.HCtrl" && type.FullName != "ksPlugVR.HSession.HCtrl")
                || __originalMethod.Name != "LateUpdate")
                throw new InvalidOperationException("Unexpected ksPlug time patch target");

            string settingsType = type.Namespace.Substring(0, type.Namespace.IndexOf('.')) + ".Settings";
            int match = -1;
            for (int i = 0; i < code.Count; i++)
            {
                if (code[i].opcode != OpCodes.Ldsfld || !(code[i].operand is FieldInfo field)
                    || field.Name != "TimePassesInH" || field.FieldType != typeof(bool)
                    || field.DeclaringType.FullName != settingsType) continue;
                if (match >= 0) throw new InvalidOperationException("Multiple ksPlug time conditions; patch refused");
                match = i;
            }
            if (match < 0 || match + 1 >= code.Count
                || (code[match + 1].opcode != OpCodes.Brfalse && code[match + 1].opcode != OpCodes.Brfalse_S))
                throw new InvalidOperationException("ksPlug time condition changed; patch refused");

            // ldsfldのboolを受け取り、時間進行の分岐だけでアストラル状態を反映する。
            // 元のラベル・例外境界を持つ命令はそのまま維持する。
            code.Insert(match + 1, new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(KsPlugTimePatch), nameof(AllowTimeProgress))));
            return code;
        }
    }
}
