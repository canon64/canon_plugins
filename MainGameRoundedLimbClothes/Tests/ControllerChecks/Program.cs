using System;
using MainGameRoundedLimbClothes;
using Sideloader.AutoResolver;
using UnityEngine;

static class Program
{
    static int assertions;
    static void Check(bool condition,string why){assertions++;if(!condition)throw new Exception(why);}
    static void Main()
    {
        Console.WriteLine("START actual controller boundary checks (game / Unity boundary substituted)");
        var p=Plugin.Instance;var c=new RoundedLimbController();var body=c.ChaControl.rendBody;
        var original=body.sharedMesh;var material=body.sharedMaterials[0];material.SetTexture("_AlphaMask","existing clothing mask");material.SetFloat("_alpha_a",1);
        UniversalAutoResolver.Entries[100000123]=new ResolveInfo{GUID=Plugin.ModGuid,Slot=1};
        c.ChaControl.nowCoordinate.clothes.parts[4].id=100000123;
        c.Reload();Check(body.sharedMesh==original&&BodyMeshAdapter.Builds==0,"denied gate must not mutate");
        p.Permit=true;c.Refresh();Check(body.sharedMesh!=original,"resolved custom clothing applies");
        Check((string)material.Get("_AlphaMask")=="existing clothing mask"&&(float)material.Get("_alpha_a")==1,"original material not changed");
        Check(body.sharedMaterials.Length==2&&body.sharedMaterials[1].Get("_AlphaMask")==null&&(float)body.sharedMaterials[1].Get("_alpha_a")==0,"cap unmask only");
        int builds=BodyMeshAdapter.Builds;c.Refresh();Check(builds==BodyMeshAdapter.Builds,"unchanged events do not regenerate");
        c.BeginEdit();var setting=c.Settings.Copy();setting.Set(0,42);c.Preview(setting);
        Check(c.Settings.LeftArm==42&&c.Settings.RightArm==42&&body.sharedMesh.name.Contains("LA=42"),"linked preview");
        c.SaveCard();var saved=SavedSettings.Decode((string)c.Extended.data["settings"]);
        Check(saved.Coordinates.Count==0,"draft excluded from card save");
        c.CancelEdit();Check(c.Settings.LeftArm==50&&body.sharedMesh.name.Contains("LA=50"),"cancel restores committed mesh");
        c.BeginEdit();setting=c.Settings.Copy();setting.LinkArms=false;setting.Set(0,33);c.Preview(setting);c.Commit();
        c.SaveCard();saved=SavedSettings.Decode((string)c.Extended.data["settings"]);
        Check(saved.Coordinates[0].LeftArm==33&&saved.Coordinates[0].RightArm==50&&!saved.Coordinates[0].LinkArms,"independent committed positions");
        c.BeginEdit();setting=c.Settings.Copy();setting.Set(0,13);var last=body.sharedMesh;c.Preview(setting);
        Check(body.sharedMesh==last&&c.Settings.LeftArm==13,"geometry failure preserves last good renderer but never freezes slider value");
        Check(!c.Commit()&&c.Editing,"failed preview cannot commit or close the editor");
        Check(p.Reports==0,"diagnostics OFF does not report exceptions");c.CancelEdit();
        var coordinate=new ChaFileCoordinate();c.SaveCoordinate(coordinate);
        c.ChaControl.fileStatus.coordinateType=1;c.LoadCoordinate(coordinate);
        Check(c.Settings.LeftArm==33,"coordinate card import");
        c.BeginEdit();setting=c.Settings.Copy();setting.Set(0,61);c.Preview(setting);c.Commit();c.SaveCard();
        saved=SavedSettings.Decode((string)c.Extended.data["settings"]);
        Check(saved.Coordinates[0].LeftArm==33&&saved.Coordinates[1].LeftArm==61,"per-coordinate settings independent");
        c.Reload();Check(c.Settings.LeftArm==61,"card reload committed value");
        c.ChaControl.fileStatus.clothesState[4]=3;c.Refresh();Check(body.sharedMesh==original&&body.sharedMaterials.Length==1,"undress restores body/material slots");
        c.ChaControl.fileStatus.clothesState[4]=0;c.Refresh();Check(body.sharedMesh!=original,"redress applies");
        var external=new Mesh{name="another mod owns this"};body.sharedMesh=external;c.StopRuntime();
        Check(body.sharedMesh==external&&body.sharedMaterials.Length==1,"stop never overwrites another owner's mesh");
        body.sharedMesh=original;c.Refresh();p.Permit=false;c.StopRuntime();Check(body.sharedMesh==original,"cleanup works after permit revoked");
        c.Refresh();Check(body.sharedMesh==original,"no reapply after stop");
        p.Permit=true;c.ChaControl.nowCoordinate.clothes.parts[4].id=42;c.Refresh();Check(body.sharedMesh==original,"unrelated clothing ID never enables feature");
        c.Dispose();Check(!RoundedLimbController.Live.Contains(c),"controller destruction unregisters");
        var valid=new SavedSettings();valid.Coordinates[0]=new PositionSettings();
        var decoded=SavedSettings.Decode(SavedSettings.Encode(valid));Check(decoded.Coordinates[0].Valid,"roundtrip");
        foreach(string bad in new[]{"{}","{\"Version\":2,\"Coordinates\":[]}","{\"Version\":1,\"Coordinates\":[{\"Key\":0,\"Value\":{\"LeftArm\":101}}]}"})
        {bool rejected=false;try{SavedSettings.Decode(bad);}catch{rejected=true;}Check(rejected,"invalid data rejected");}
        Console.WriteLine("PASS controller assertions="+assertions);
    }
}
