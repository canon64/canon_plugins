using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using MainGameHLocationCoordinates;
using UnityEngine;

namespace MainGameHLocationPlacement
{
    internal static class Program
    {
        private static int _checks;
        internal static void Assert(bool condition,string name) { if(!condition)throw new Exception("FAIL: "+name);_checks++; }
        internal static void Throws(Action action,string name) { try{action();}catch{_checks++;return;}throw new Exception("FAIL no rejection: "+name); }
        internal static RigidPose Pose(float x,float y,float z,float yaw)=>new RigidPose(new Vector3(x,y,z),Quaternion.Euler(new Vector3(0,yaw,0)));
        internal static void Near(RigidPose value,RigidPose expected,string name)=>Assert((value.position-expected.position).magnitude<.001f&&Quaternion.Angle(value.rotation,expected.rotation)<.1f,name);
        internal static PlaceOrigin Origin(RigidPose pose)=>new PlaceOrigin{mapKey="bundle:test\nscene:room",mapName="room",placeId="place",poseKey="sonyu/4/S",utc="2026-11-01T00:00:00Z",femaleRoot=PoseNumbers.From(pose),mapRoot=PoseNumbers.From(RigidPose.Identity),
            parents=new System.Collections.Generic.List<ParentOrigin>{new ParentOrigin{objectId="root",kind="external",reference="char:root",world=PoseNumbers.From(pose)}}};
        private static string _root;
        internal static string DirectoryFor(string name) { string path=Path.Combine(_root,name+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);return path; }
        private static int Main()
        {
            try
            {
                string repo=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","..","..","..",".."));
                _root=Path.Combine(repo,".build","h-location-placement","tests");Directory.CreateDirectory(_root);
                string env=DirectoryFor("environment");
                AccessTools.Method(typeof(BepInEx.Paths),"SetExecutablePath").Invoke(null,new object[]{Path.Combine(env,"TestGame.exe"),Path.Combine(env,"BepInEx"),null,new string[0]});
                MathAndDocuments(); DiagnosticStatusTests.Run(); MapContextTests.Run(); SaveTests.Run(); HipOriginSaveTests.Run(); RuntimeTests.Run(); CoordinateDiagnosticTests.Run(); ContractTests.Run(repo);
                Console.WriteLine("PASS: "+_checks+" assertions; shared math, real JSON persistence, Harmony save/load/move fixtures, installed-DLL contracts");return 0;
            }
            catch(Exception ex){Console.Error.WriteLine(ex);Console.Error.WriteLine("Last operation: "+Plugin.Instance.Status);return 1;}
        }
        private static void MathAndDocuments()
        {
            var a=Pose(1,2,-3,30);var p=Pose(4,1,5,18);
            foreach(float yaw in new[]{0f,90f,180f})
            {
                var b=Pose(-11,3,7,yaw);bool active=true;
                var space=new CoordinateSpace(a,()=>{if(!active)throw new InvalidOperationException();});space.UpdateDestination(b);
                Near(space.Transform(p),RigidPose.Rebase(a,b,p),"shared projection "+yaw);Near(space.InverseTransform(space.Transform(p)),p,"inverse "+yaw);
                for(int i=0;i<100;i++){space.UpdateDestination(a);Near(space.Transform(p),p,"roundtrip source");space.UpdateDestination(b);Near(space.Transform(p),RigidPose.Rebase(a,b,p),"roundtrip target");}
                active=false;Throws(()=>space.Transform(p),"expired source");space.Close();Throws(()=>space.UpdateDestination(a),"closed source");
            }
            var numbers=PoseNumbers.From(p);Near(numbers.ToPose(),p,"explicit numerical pose");numbers.rotation=new float[4];Throws(()=>numbers.ToPose(),"zero quaternion");
            Assert(PresetDocuments.Canonical(PresetDocuments.Parse("{\"b\":1.0,\"a\":\"x\"}"))==PresetDocuments.Canonical(PresetDocuments.Parse("{\"a\":\"x\",\"b\":1}")),"stable JSON fingerprints");
            var node=PresetDocuments.Parse("{\"hasProxyPose\":true,\"proxyPosX\":1.23,\"path\":\"a\\\\b\"}");
            Assert(PresetDocuments.Canonical(node)==PresetDocuments.Canonical(PresetDocuments.Parse(PresetDocuments.ToJson(node))),"Hip element JSON preserves all numerical fields");
            var nullable=PresetDocuments.Parse("{\"followBonePath\":null,\"empty\":\"\",\"nested\":[null,{\"target\":null,\"enabled\":true}],\"number\":1.23}");
            string before=nullable.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
            string wire=PresetDocuments.ToJson(nullable);
            Assert(wire.Contains("\"followBonePath\":null"),"native nullable follow path stays JSON null");
            Assert(PresetDocuments.Canonical(nullable)==PresetDocuments.Canonical(PresetDocuments.Parse(wire)),"nested nulls preserve all values and fingerprints");
            Assert(nullable.ToString(System.Xml.Linq.SaveOptions.DisableFormatting)==before,"JSON output leaves original XML untouched");
            var nullRoot=PresetDocuments.Parse("null");
            Assert(PresetDocuments.ToJson(nullRoot)=="null","root null roundtrip");
        }
    }
}
