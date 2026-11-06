using System;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Xml.Linq;
using Mono.Cecil;

static class Program
{
    static void Check(bool valid,string message){if(!valid)throw new Exception(message);}
    static System.Collections.Generic.IEnumerable<TypeDefinition> Types(ModuleDefinition module)=>module.Types.SelectMany(Types);
    static System.Collections.Generic.IEnumerable<TypeDefinition> Types(TypeDefinition type)=>new[]{type}.Concat(type.NestedTypes.SelectMany(Types));
    static void Main(string[] args)
    {
        string root=args.Length==0?".":args[0];
        using var resolver=new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(@"F:\kks\BepInEx\core");resolver.AddSearchDirectory(@"F:\kks\KoikatsuSunshine_Data\Managed");
        using var plugin=ModuleDefinition.ReadModule(Path.Combine(root,"bin/Release/net472/MainGameRoundedLimbClothes.dll"),new ReaderParameters{AssemblyResolver=resolver});
        using var game=ModuleDefinition.ReadModule(@"F:\kks\KoikatsuSunshine_Data\Managed\Assembly-CSharp.dll");
        var all=Types(plugin).ToArray();
        Check(!all.SelectMany(t=>t.Methods).Any(m=>new[]{"Update","LateUpdate","FixedUpdate"}.Contains(m.Name)),"frame callback added");
        var entry=all.Single(t=>t.FullName=="MainGameRoundedLimbClothes.Plugin");
        var references=all.SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
        foreach(string method in new[]{"Register","Capture"})Check(references.Any(m=>m.DeclaringType.FullName=="CanonGate.GateApi"&&m.Name==method),"gate contract missing");
        foreach(string method in new[]{"SetInputBlockRects","ClearInputBlockRects","SetIdleCursorUnlock","Sync"})Check(references.Any(m=>m.DeclaringType.FullName=="MainGameUiInputCapture.UiInputCaptureApi"&&m.Name==method),"input contract missing "+method);
        var dependencies=entry.CustomAttributes.Where(a=>a.AttributeType.Name=="BepInDependency").Select(a=>(string)a.ConstructorArguments[0].Value).ToArray();
        foreach(string id in new[]{"canon.gate","marco.kkapi","com.bepis.bepinex.sideloader","com.bepis.bepinex.extendedsave","com.kks.maingame.uiinputcapture","com.kks.maingame.unifiedui"})Check(dependencies.Contains(id),"dependency missing "+id);
        var gameControl=game.Types.Single(t=>t.Name=="ChaControl");
        var makerSelect=game.Types.Single(t=>t.FullName=="ChaCustom.CvsClothes").Methods.Single(m=>m.Name=="UpdateSelectClothes");
        Check(makerSelect.Parameters.Select(p=>p.ParameterType.FullName).SequenceEqual(new[]{"System.String","UnityEngine.Sprite","System.Int32"}),"Maker selection hook signature differs");
        var makerHook=entry.Methods.Single(m=>m.Name=="MakerClothesSelected");
        Check(makerHook.Body.Instructions.Any(i=>i.Operand is MethodReference mr&&mr.Name=="OpenMakerEditor"),"same-ID Maker selection must reopen editor");
        Check(!all.Any(t=>t.Name=="StandardBodies"),"obsolete stock body hash whitelist still compiled");
        using(var selector=ModuleDefinition.ReadModule(@"F:\kks\BepInEx\plugins\KKS_Plugins\KKS_UncensorSelector.dll"))
        {
            var controller=Types(selector).Single(t=>t.FullName=="KK_Plugins.UncensorSelector/UncensorSelectorController");
            foreach(string method in new[]{"ReloadCharacterUncensor","ReloadCharacterBody","UpdateSkinColor","UpdateSkin"})Check(controller.Methods.Any(m=>m.Name==method),"installed Uncensor hook unavailable "+method);
        }
        foreach(string name in new[]{"ChangeClothesGlovesNoAsync","ChangeClothesSocksNoAsync","ChangeClothesGlovesAsync","ChangeClothesSocksAsync","SetClothesState","ChangeCoordinateType","Reload","ReloadNoAsync","ReloadAsync","Load","LoadNoAsync","LoadAsync","CreateBodyTexture","ChangeCustomBodyWithoutCustomTexture","ChangeAlphaMask","SetBodyBaseMaterial","VisibleAddBodyLine","ChangeSettingBodyDetail","ChangeSettingBodyDetailPower"})
        {var methods=gameControl.Methods.Where(m=>m.Name==name).ToArray();Check(methods.Length>0,"event hook name unavailable "+name);Console.WriteLine("HOOK "+name+" overloads="+methods.Length);}
        foreach(var file in new[]{("canon_gate","CanonGate.GateApi"), ("MainGameUnifiedUi","MainGameUnifiedUi.UnifiedUiApi"),("MainGameUiInputCapture","MainGameUiInputCapture.UiInputCaptureApi")})
        {
            string installed=Path.Combine(@"F:\kks\BepInEx\plugins\canon_plugins",file.Item1,file.Item1+".dll");
            using var dependency=ModuleDefinition.ReadModule(installed);
            var target=dependency.Types.Single(t=>t.FullName==file.Item2);
            foreach(var used in references.Where(m=>m.DeclaringType.FullName==file.Item2))
                Check(target.Methods.Any(m=>m.Name==used.Name&&m.Parameters.Count==used.Parameters.Count),"installed dependency API unavailable "+used.FullName);
            var installedVersion=dependency.Types.SelectMany(t=>t.CustomAttributes).First(a=>a.AttributeType.Name=="BepInPlugin").ConstructorArguments[2].Value.ToString();
            if(file.Item1=="canon_gate")Check(Version.Parse(installedVersion)>=Version.Parse("2.4.0"),"installed canon_gate too old");
            Console.WriteLine("INSTALLED API "+file.Item1+" "+installedVersion);
        }
        string zip=Directory.GetFiles(Path.Combine(root,"artifacts"),"*.zipmod").Single();
        using var archive=ZipFile.OpenRead(zip);
        using var manifest=archive.GetEntry("manifest.xml").Open();
        var xml=XDocument.Load(manifest);
        Check(xml.Root.Element("game").Value=="Koikatsu Sunshine","Sideloader game filter must match its accepted spelling");
        Console.WriteLine("PASS compiled runtime contracts and native event targets; no game execution");
    }
}
