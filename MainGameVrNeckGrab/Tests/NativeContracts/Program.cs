using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class Program
{
    static int checks;
    static void Check(bool ok, string reason) { ++checks; if (!ok) throw new Exception(reason); }
    static IEnumerable<TypeDefinition> Types(TypeDefinition t) => new[] { t }.Concat(t.NestedTypes.SelectMany(Types));
    static void Main(string[] args)
    {
        string root = args.Length > 0 ? args[0] : @"F:\kks";
        foreach (string mode in new[] { "KoikatsuSunshine_Data", "KoikatsuSunshine_VR_Data" })
        {
            using (var a = AssemblyDefinition.ReadAssembly(Path.Combine(root, mode, "Managed", "Assembly-CSharp.dll")))
            {
                Func<string, TypeDefinition> type = n => a.MainModule.Types.First(t => t.FullName == n);
                foreach (string name in new[] { "VoiceProc", "VoiceLinkProc", "PlayVoice" })
                {
                    var method = type("HVoiceCtrl").Methods.Single(m => m.Name == name);
                    Check(method.Parameters.Any(p => p.Name == "_female" && p.ParameterType.FullName == "ChaControl"), mode + " voice actor " + name);
                    Check(method.ReturnType.FullName == "System.Boolean", mode + " voice return " + name);
                }
                Check(type("HVoiceCtrl").Methods.Single(m => m.Name == "PlayVoice").Parameters.Any(p => p.Name == "_voiceKind"), mode + " voice kind");
                foreach (string field in new[] { "flags", "voice", "sprite", "hand", "hand1", "lstFemale" })
                    Check(type("HSceneProc").Fields.Any(f => f.Name == field), mode + " scene field " + field);
                Check(type("HandCtrl").Fields.Any(f => f.Name == "dicItem"), mode + " native hand catalog");
                Check(type("HandCtrl").Methods.Any(m => m.Name == "DetachAllItem"), mode + " hand detach");
                Check(type("HSprite").Fields.Any(f => f.Name == "commonAibuIcons"), mode + " caress slots");
                Check(type("HSprite").NestedTypes.Single(t => t.Name == "GaugeSprite").Fields.Any(f => f.Name == "imgBar"), mode + " native gauge image");
                foreach (var m in type("ChaControl").Methods.Where(m => m.Name == "setAnimatorParamFloat" && m.Parameters.Count == 2))
                    Check(m.Parameters[1].ParameterType.FullName == "System.Single" && new[] { "System.Int32", "System.String" }.Contains(m.Parameters[0].ParameterType.FullName), mode + " motion signature");
                Check(type("Manager.Voice").Methods.Single(m => m.Name == "Play_Standby").Parameters.Select(p => p.Name).SequenceEqual(new[] { "audioSource", "loader" }), mode + " async voice boundary");
                var writes = a.MainModule.Types.SelectMany(Types).SelectMany(t => t.Methods).Where(m => m.HasBody)
                    .Where(m => m.Body.Instructions.Any(i => i.OpCode.Code == Code.Stfld && i.Operand is FieldReference f && f.DeclaringType.FullName == "HFlag" && f.Name == "gaugeFemale")).ToArray();
                Console.WriteLine(mode + " native gauge writers: " + string.Join(", ", writes.Select(m => m.DeclaringType.FullName + "." + m.Name)));
                Check(writes.Length > 0, mode + " no gauge writer inventory");
            }
        }
        using (var vr = AssemblyDefinition.ReadAssembly(Path.Combine(root, @"BepInEx\plugins\KKS_VR\KKS_MainGameVR.dll")))
        {
            var caress = vr.MainModule.Types.Single(t => t.FullName == "KKS_VR.Caress.CaressController");
            var handle = caress.Methods.Single(m => m.Name == "HandleTrigger");
            var downCalls = handle.Body.Instructions.Where(i => i.Operand is MethodReference m && m.Name == "GetPressDown").ToArray();
            Check(downCalls.Length == 1 && ((MethodReference)downCalls[0].Operand).ReturnType.FullName == "System.Boolean", "native trigger routing IL changed");
            Check(caress.Fields.Any(f => f.Name == "_controller"), "native caress controller field missing");
            var mouth = vr.MainModule.Types.Single(t => t.FullName == "KKS_VR.Caress.VRMouth");
            Check(mouth.Methods.Any(m => m.Name == "StartKiss" && m.Parameters.Count == 0), "native automatic kiss hook missing");
        }
        string product = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\..\bin\Release\net472\MainGameVrNeckGrab.dll"));
        using (var a = AssemblyDefinition.ReadAssembly(product))
        {
            var all = a.MainModule.Types.SelectMany(Types).ToArray();
            Check(!all.SelectMany(t => t.Methods).Any(m => new[] { "Update", "LateUpdate", "FixedUpdate" }.Contains(m.Name)), "product adds frame callback");
            Check(!all.SelectMany(t => t.Methods).Any(m => new[] { "OnTriggerEnter", "OnTriggerExit" }.Contains(m.Name)), "neck still requires collision callbacks");
            Check(!all.SelectMany(t => t.Methods).Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Any(i => i.Operand is MethodReference m && (m.Name.Contains("onTransformChanged") || m.Name.Contains("onTransformChangedEvent"))), "product subscribes pose movement");
            Check(!a.MainModule.AssemblyReferences.Any(r => r.Name == "MainGameDollMode"), "product depends on old doll DLL");
            Check(!all.SelectMany(t => t.Methods).Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Any(i => i.Operand is MethodReference m && m.DeclaringType.FullName == "System.IO.File" && m.Name.StartsWith("Write")), "product writes saved doll settings");
        }
        Console.WriteLine("PASS " + checks + " shipped-DLL contracts / product lifecycle audit");
    }
}
