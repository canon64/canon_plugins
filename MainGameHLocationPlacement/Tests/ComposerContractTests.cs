using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace MainGameHLocationPlacement
{
    internal static class ComposerContractTests
    {
        private sealed class UpdatedComposer : ComposerFixture
        { public string UnrelatedHotelSetting; public void NewEditorFeature() { } }
        private sealed class WrongField : ComposerFixture
        { public new string _presetSaveSucceeded; }
        private sealed class WrongMethod : ComposerFixture
        { public new string SavePresets() => "changed"; }
        private sealed class ExtraOverload : ComposerFixture
        { public void LoadPresetSlot(string unrelated) { } }
        private sealed class MissingComposer { }
        private static void Check(Type type) => ComposerContract.Check(type, typeof(ComposerPreset), typeof(Data), typeof(RuntimeObject), typeof(MovementRule), typeof(ChaControl));
        internal static void Run()
        {
            Check(typeof(ComposerFixture)); Program.Assert(true,"original Composer connection accepted");
            Check(typeof(UpdatedComposer)); Program.Assert(true,"unrelated Composer feature additions remain compatible");
            Program.Throws(()=>Check(typeof(WrongField)),"changed save-result type is rejected");
            Program.Throws(()=>Check(typeof(WrongMethod)),"changed save method signature is rejected");
            Program.Throws(()=>Check(typeof(ExtraOverload)),"ambiguous Harmony patch target is rejected");
            Program.Throws(()=>Check(typeof(MissingComposer)),"missing placement members are rejected");
        }
        internal static void InstalledVariants(string repo)
        {
            string installed="F:/kks/BepInEx/plugins/canon_plugins/MainGameObjectComposer/MainGameObjectComposer.dll";
            string directory=Program.DirectoryFor("composer-updates");
            string updated=Path.Combine(directory,"ComposerUpdated.dll"), broken=Path.Combine(directory,"ComposerBroken.dll");
            using(var module=ModuleDefinition.ReadModule(installed))
            {
                var plugin=module.GetType("MainGameObjectComposer.Plugin");
                plugin.Fields.Add(new FieldDefinition("UnrelatedFutureFeature",FieldAttributes.Public,module.TypeSystem.Boolean));
                module.Write(updated);
                plugin.Fields.Single(f=>f.Name=="_presetSaveSucceeded").FieldType=module.TypeSystem.String;
                module.Write(broken);
            }
            Program.Assert(!File.ReadAllBytes(installed).SequenceEqual(File.ReadAllBytes(updated)),"future Composer fixture has different DLL contents");
            string probe=Path.Combine(repo,"MainGameHLocationPlacement","Tests","ConnectionProbe","bin","Release","net472","ConnectionProbe.exe");
            Probe(probe,updated,true);
            Probe(probe,broken,false);
        }
        private static void Probe(string probe,string dll,bool accepted)
        {
            var start=new ProcessStartInfo(probe,"\""+dll+"\"") { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
            using(var process=Process.Start(start))
            {
                var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
                if(!process.WaitForExit(15000)) { process.Kill();throw new Exception("Composer contract probe timed out"); }
                string report=output.Result+errors.Result;
                Program.Assert(accepted ? process.ExitCode==0 : process.ExitCode!=0&&report.Contains("_presetSaveSucceeded"),"actual modified Composer DLL contract accepted="+accepted+" "+report);
            }
        }
    }
}
