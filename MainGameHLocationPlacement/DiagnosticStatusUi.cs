using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace MainGameHLocationPlacement
{
    internal static class DiagnosticStatusUi
    {
        // Injected inside the existing expanded status section, before BodyIK diagnostics.
        // The native window/tab continues to own input capture and its layout.
        internal static IEnumerable<CodeInstruction> Insert(IEnumerable<CodeInstruction> instructions)
        {
            int count = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldstr && (string)instruction.operand == "BodyIK診断ログ")
                {
                    var draw = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DiagnosticStatusUi), nameof(Draw)));
                    draw.labels.AddRange(instruction.labels); instruction.labels.Clear();
                    draw.blocks.AddRange(instruction.blocks); instruction.blocks.Clear();
                    yield return draw; count++;
                }
                yield return instruction;
            }
            if (count != 1) throw new InvalidOperationException("HipHijackステータスの診断設定欄に接続できません。");
        }
        internal static void Draw()
        {
            var plugin = Plugin.Instance;
            if (plugin == null || !plugin.CanShowStatus) return;
            bool next = GUILayout.Toggle(plugin.Diagnostics, new GUIContent("H場所配置の診断ログ", "保存・場所移動・IK補正の診断を出力します。変更は即時反映し、設定に保存します。"));
            if (next != plugin.Diagnostics) plugin.SetDiagnostics(next);
        }
    }
}
