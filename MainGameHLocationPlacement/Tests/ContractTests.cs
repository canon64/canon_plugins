using System;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace MainGameHLocationPlacement
{
    internal static class ContractTests
    {
        private static MethodDefinition Method(TypeDefinition t,string name)=>t.Methods.Single(m=>m.Name==name);
        private static void Signature(TypeDefinition t,string name,string result,params string[] args)
        {var method=Method(t,name);Program.Assert(method.ReturnType.FullName==result&&method.Parameters.Select(p=>p.ParameterType.FullName).SequenceEqual(args),"installed exact signature "+t.FullName+"."+name);}
        private static System.Collections.Generic.IEnumerable<TypeDefinition> All(TypeDefinition type)
        {yield return type;foreach(var child in type.NestedTypes)foreach(var nested in All(child))yield return nested;}
        internal static void Run(string repo)
        {
            ComposerContractTests.Run();
            ComposerContractTests.InstalledVariants(repo);
            string hipIndex="F:/kks/BepInEx/plugins/canon_plugins/MainGirlHipHijack/pose_presets/index.json";
            if(File.Exists(hipIndex))
            {
                var presets=PresetDocuments.Collection(PresetDocuments.Hip,File.ReadAllText(hipIndex));
                Program.Assert(presets.Count>0,"installed Hip corpus is parsed read-only");
                foreach(var part in presets.SelectMany(p=>p.Parts))
                    Program.Assert(PresetDocuments.Hash(PresetDocuments.Canonical(PresetDocuments.Parse(part.Elements.Single())))==part.Fingerprint,"installed Hip IK JSON roundtrip preserves fingerprint");
            }
            string product=Path.Combine(repo,"MainGameHLocationPlacement","bin","Release","net472","MainGameHLocationPlacement.dll");
            using(var own=ModuleDefinition.ReadModule(product))
            {
                foreach(string name in new[]{"MainGameObjectComposer","MainGirlHipHijack","MainGameBlankMapAdd"})
                {
                    string file="F:/kks/BepInEx/plugins/canon_plugins/"+name+"/"+name+".dll";
                    string key=name=="MainGameObjectComposer"?"Composer":name=="MainGirlHipHijack"?"Hip":"Map";
                    Program.Assert(!own.GetType("MainGameHLocationPlacement.Compatibility").Methods.Any(m=>m.Name=="Check"||m.Name=="SupportsHash"||m.Name=="HashFile"),"runtime has no DLL hash allowlist");
                    using(var module=ModuleDefinition.ReadModule(file))
                    {
                        var type=module.GetType(name+".Plugin");
                        if(key=="Composer")
                        {
                            string preset="MainGameObjectComposer.PresetEntry",data="MainGameObjectComposer.ManagedObjectData";
                            Signature(type,"SaveCurrentToPreset","System.Void",preset,"System.String","System.Int32");Signature(type,"CommitPresetDraft","System.Boolean","System.Boolean","System.String");
                            Signature(type,"SaveJsonAtomic","System.Void","System.String","System.String","System.Boolean");Signature(type,"ActivatePreset","System.Void",preset);
                            Signature(type,"LoadPresetSlot","System.Void",preset,"System.Boolean");Signature(type,"ResolveConfiguredParentTransform","UnityEngine.Transform",data,"System.String","System.String&","System.String&");
                            foreach(string method in new[]{"ApplyPresetEditorFixedSlot","DeactivatePreset","RebuildActivePresetCache","DrawMainBody"})Signature(type,method,"System.Void");
                            Signature(type,"SaveMotionPresetMutation","System.Void");Signature(type,"GetEffectiveMotionPositionOffset","UnityEngine.Vector3","System.String");
                            Signature(type,"FindMovementField","MainGameObjectComposer.MovementFieldRule",preset,"System.String","System.String");
                            Signature(type,"ApplyMovementPose","System.Void","MainGameObjectComposer.RuntimeObjectRef",data,data,"System.Single");
                            Signature(type,"BeginMovementEdit","System.Void",preset,"System.Int32","System.Boolean");Signature(type,"EndMovementEdit","System.Void","System.Boolean");
                            foreach(string field in new[]{"_activeL1ById","_activeL2ById","_presetSource","_presetDraft","_presetSaveSucceeded","_presetSaveFailed","_presetEditorState","_runtimeObjects","_presetsPath"})
                                Program.Assert(type.Fields.Any(f=>f.Name==field),"Composer field "+field);
                        }
                        if(key=="Hip")
                        {
                            Signature(type,"SaveCurrentPosePresetWithScreenshot","System.Void","System.String");Signature(type,"OverwritePosePresetById","System.Void","System.String");Signature(type,"SavePosePresetIndex","System.Void");
                            Signature(type,"BuildCurrentPosePresetSnapshot","MainGirlHipHijack.Plugin/PosePresetRuntime","System.String");
                            var female=type.Properties.Single(p=>p.Name=="_femaleSession");
                            var femaleType=module.GetType(female.PropertyType.FullName);
                            Program.Assert(femaleType==null||femaleType.Fields.Any(f=>f.Name=="Character")||femaleType.Properties.Any(p=>p.Name=="Character"),"Hip character comes from its own session");
                            Signature(type,"ApplyPosePreset","System.Boolean","MainGirlHipHijack.Plugin/PosePresetRuntime","System.Boolean","System.String");
                            Signature(type,"DrawPosePresetSection","System.Void");
                            Signature(type,"DrawHipHijackBody","System.Void");
                            Program.Assert(Method(type,"DrawHipHijackBody").Body.Instructions.Count(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Ldstr&&(string)i.Operand=="BodyIK診断ログ")==1,"actual Hip status diagnostic insertion point");
                            var apply=Method(type,"ApplyPosePreset");Program.Assert(apply.Body.Instructions.Count(i=>i.Operand is FieldReference f&&f.Name=="ExternalOwner")==1,"actual linked-IK filter insertion point");
                            var write=Method(type,"SavePosePresetIndex");Program.Assert(write.Body.Instructions.Count(i=>i.Operand is MethodReference m&&m.DeclaringType.FullName=="System.IO.File"&&m.Name=="WriteAllText"&&m.Parameters.Count==3)==1,"actual successful Hip write insertion point");
                            foreach(string method in new[]{"BuildPosePresetFileData","SerializePosePresetFileData","DeserializePosePresetIndex","GetEffectorKeys"})Program.Assert(Method(type,method)!=null,"Hip runtime serializer "+method);
                            foreach(string field in new[]{"_bikEff","_settings","_activeTransitionPoints","_runtimeWrists","_posePresets","_posePresetIndexPath"})
                                Program.Assert(type.Fields.Any(f=>f.Name==field)||type.Properties.Any(p=>p.Name==field),"Hip member "+field);
                        }
                        if(key=="Map")
                        {
                            foreach(string evt in new[]{"HLocationMoving","HLocationMoved"})
                            {
                                var notification=type.Events.Single(e=>e.Name==evt);
                                Program.Assert(notification.AddMethod.IsStatic&&notification.RemoveMethod.IsStatic&&notification.EventType.FullName=="System.Action`4<ChaControl,System.Int64,System.String,System.String>","typed static map event "+evt);
                            }
                            foreach(string field in new[]{"_sessionStarted","_femaleChara","_roomOverlayLoading","_roomOverlaySceneName","_roomOverlayBundleName","_roomOverlayDisplayName","_roomOverlayPositionRoot","_vamHotelPivot","_hPlaceRoot","_hPlaceKey","_hPlaceActive","_hPlaceDraft"})Program.Assert(type.Fields.Any(f=>f.Name==field),"map context field "+field);
                            Program.Assert(type.Properties.Any(p=>p.Name=="LastReservedMap"&&p.PropertyType.FullName=="BaseMap"&&p.GetMethod!=null),"read-only reserved map source");
                            foreach(string field in new[]{"_roomOverlaySceneName","_roomOverlayBundleName","_roomOverlayDisplayName","_hPlaceKey"})Program.Assert(type.Fields.Single(f=>f.Name==field).FieldType.FullName=="System.String","map string field "+field);
                            foreach(string field in new[]{"_roomOverlayLoading","_sessionStarted"})Program.Assert(type.Fields.Single(f=>f.Name==field).FieldType.FullName=="System.Boolean","map bool field "+field);
                            foreach(string field in new[]{"_roomOverlayPositionRoot","_vamHotelPivot"})Program.Assert(type.Fields.Single(f=>f.Name==field).FieldType.FullName=="UnityEngine.GameObject","map root field "+field);
                            Program.Assert(type.Fields.Single(f=>f.Name=="_femaleChara").FieldType.FullName=="ChaControl"&&type.Fields.Single(f=>f.Name=="_hPlaceRoot").FieldType.FullName=="UnityEngine.Transform","map actor and bound root types");
                            foreach(string field in new[]{"_hPlaceActive","_hPlaceDraft"})foreach(string member in new[]{"Id","Name","MapKey"})
                                Program.Assert(module.GetType(type.Fields.Single(f=>f.Name==field).FieldType.FullName).Fields.Single(f=>f.Name==member).FieldType.FullName=="System.String","map place identity "+field+"."+member);
                        }
                    }
                }
                Program.Assert(!own.Types.SelectMany(All).SelectMany(t=>t.Methods).Any(m=>new[]{"Update","LateUpdate","FixedUpdate","OnGUI"}.Contains(m.Name)),"no added frame/GUI monitor");
                Program.Assert(!own.Types.SelectMany(All).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference m&&m.Name=="RegisterTab"),"production has no trial tab registration");
                Program.Assert(!own.Types.Any(t=>t.Name=="IoIsolation"||t.Name=="TrialController"),"no production trial storage routing");
            }
            string composerSource=File.ReadAllText(Path.Combine(repo,"MainGameHLocationPlacement","RuntimeBridge.cs"));
            Program.Assert(composerSource.Contains("ComposerContract.Check(Composer.GetType())")&&!composerSource.Contains("Compatibility.Check(Composer,"),"Composer startup uses connection contract without DLL pin");
            using(var game=ModuleDefinition.ReadModule("F:/kks/KoikatsuSunshine_Data/Managed/Assembly-CSharp.dll"))
            {var scene=game.GetType("HSceneProc");Program.Assert(Method(scene,"ChangeAnimator")!=null&&Method(scene,"SetLocalPosition")!=null,"actual native H pose hooks");Program.Assert(scene.Fields.Any(f=>f.Name=="nowHpointData"&&f.FieldType.FullName=="System.String"),"actual native H-place identity");}
        }
    }
}
