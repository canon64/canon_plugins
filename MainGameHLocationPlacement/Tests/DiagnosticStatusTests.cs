using System;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace MainGameHLocationPlacement
{
    internal static class DiagnosticStatusTests
    {
        private sealed class StatusFixture
        {
            internal bool Expanded;
            internal string Label;
            [MethodImpl(MethodImplOptions.NoInlining)] internal void DrawHipHijackBody()
            { if (Expanded) Consume(false,"BodyIK診断ログ"); }
            [MethodImpl(MethodImplOptions.NoInlining)] private void Consume(bool enabled,string label) { Label=label; }
        }
        internal static void Run()
        {
            string path=Path.Combine(Program.DirectoryFor("diagnostic-setting"),"settings.cfg");
            var config=new ConfigFile(path,false);var setting=config.Bind("Logging","EnableLogs",false);
            Program.Assert(!setting.Value,"diagnostic default remains OFF until explicitly enabled");
            Program.Assert(DiagnosticLogSetting.Set(config,setting,true)&&setting.Value,"diagnostic setting enables immediately");
            var reopened=new ConfigFile(path,false);
            Program.Assert(reopened.Bind("Logging","EnableLogs",false).Value,"status ON persists across restart");
            Program.Assert(DiagnosticLogSetting.Set(config,setting,false)&&!setting.Value,"diagnostic setting disables immediately");
            reopened.Reload();Program.Assert(!reopened.Bind("Logging","EnableLogs",false).Value,"status OFF persists across restart");
            Program.Assert(!DiagnosticLogSetting.Set(config,setting,false),"unchanged status does not write config again");
            var harmony=new Harmony("test.hlocation.status");
            var plugin=Plugin.Instance;plugin.Diagnostics=false;plugin.CanShowStatus=true;plugin.DiagnosticSettingWrites=0;
            try
            {
                harmony.Patch(AccessTools.Method(typeof(StatusFixture),"DrawHipHijackBody"),transpiler:new HarmonyMethod(typeof(DiagnosticStatusUi),nameof(DiagnosticStatusUi.Insert)));
                var status=new StatusFixture();GUILayout.ToggleCalls=0;status.DrawHipHijackBody();
                Program.Assert(GUILayout.ToggleCalls==0,"collapsed native status does not draw diagnostic toggle");
                status.Expanded=true;GUILayout.NextToggle=true;status.DrawHipHijackBody();
                Program.Assert(plugin.Diagnostics&&plugin.DiagnosticSettingWrites==1&&status.Label=="BodyIK診断ログ","native status ON leaves original controls intact");
                GUILayout.NextToggle=false;status.DrawHipHijackBody();
                Program.Assert(!plugin.Diagnostics&&plugin.DiagnosticSettingWrites==2,"native status OFF stops diagnostics immediately");
                int collected=0;plugin.Diagnostic(()=>{collected++;return "event";});
                Program.Assert(collected==0,"OFF bypasses diagnostic message collection");
                plugin.CanShowStatus=false;int calls=GUILayout.ToggleCalls;status.DrawHipHijackBody();
                Program.Assert(calls==GUILayout.ToggleCalls,"expired gate does not display diagnostic settings");
                Program.Throws(()=>DiagnosticStatusUi.Insert(new[]{new CodeInstruction(OpCodes.Nop)}).ToArray(),"unknown native status refuses insertion");
            }
            finally { harmony.UnpatchSelf();plugin.Diagnostics=false;plugin.CanShowStatus=true;GUILayout.NextToggle=null; }
        }
    }
}
