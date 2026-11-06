using UnityEngine;
using CanonPlugins.Shared;
using Hip=MainGirlHipHijack.Plugin;
using Cuffs=MainGameVrHandcuffs.Plugin;

static partial class Program
{
    static void TestHotelRestore()
    {
        Hip.Instance=new Hip();
        CanonPlugins.Local.SessionGate.Allowed=true;
        var room=new Transform {position=new Vector3(3,2,-4),rotation=Quaternion.Euler(0,123,0)};
        var p=new Cuffs();
        // Older saved body offset disagrees with the required fixed mount.
        // Saved wrist targets still describe the user's desired fixed hands.
        var values=Enumerable.Range(0,8).Select(i=>new GameObject().transform).ToArray();
        for(int i=0;i<values.Length;i++) values[i].SetPositionAndRotation(room.TransformPoint(new Vector3(.03f*i,.2f,.4f)),room.rotation);
        var saved=GadgetRoomSnapshot.Capture(room,values);
        saved.runtime=UnityEngine.JsonUtility.ToJson(new CuffRuntime {wrists=new[]{0,1},bend=Vector3.forward});
        var json=saved.Encode();
        Hip.Instance.Effectors[0].bone.position=new Vector3(90,80,70);
        p.RestoreHotelGadget(room,json);
        Check(p.VerifyHotelGadgetPlacement(room,json),"bound cuffs restore saved targets and fixed mount despite new animation wrists");
        for(int i=0;i<2;i++)
        {
            var expected=room.TransformPoint(saved.poses[6+i].position);
            Check(Vector3.Distance(Hip.Instance.Effectors[i].target.position,expected)<.00001f,"restored IK target retains saved room position");
            MainGameVrHandcuffs.HandcuffMountPose.Calculate(i,i,expected,room.rotation,out var mount,out var rotation);
            Check(Vector3.Distance(p.Part(i).position,mount)<.00001f,"cuff aligned to fixed mount around saved target");
        }
        p.Part(3).position+=new Vector3(.02f,0,0);
        Check(!p.VerifyHotelGadgetPlacement(room,json),"actual displaced chain link fails validation");
        p.RestoreHotelGadget(room,json);
        Check(p.VerifyHotelGadgetPlacement(room,json),"restore repairs displaced chain link and rebinds hands");
        p.Delete();
        Check(!p.VerifyHotelGadgetPlacement(room,json),"lost wrist ownership fails validation");
        saved.runtime=UnityEngine.JsonUtility.ToJson(new CuffRuntime {wrists=new[]{-1,-1},bend=Vector3.forward});
        json=saved.Encode();
        p.RestoreHotelGadget(room,json);
        Check(p.VerifyHotelGadgetPlacement(room,json),"unbound cuffs retain all saved body poses without canonical mount override");
        saved.visible=false;saved.poses=Array.Empty<GadgetRoomPose>();
        json=saved.Encode();p.RestoreHotelGadget(room,json);
        Check(p.VerifyHotelGadgetPlacement(room,json),"saved hidden cuffs delete live object");
    }
    public class CuffRuntime {public int[] wrists;public Vector3 bend;}
}

namespace UnityEngine
{
    public static class JsonUtility
    {
        static readonly System.Text.Json.JsonSerializerOptions Options=new() {IncludeFields=true,IgnoreReadOnlyProperties=true};
        public static string ToJson(object value)=>System.Text.Json.JsonSerializer.Serialize(value,value.GetType(),Options);
        public static T FromJson<T>(string value)=>System.Text.Json.JsonSerializer.Deserialize<T>(value,Options);
    }
}
namespace MainGameVrHandcuffs
{
    public sealed partial class Plugin
    {
        void CreateRig()
        {
            _rig=new();
            for(int i=0;i<_parts.Length;i++) { _parts[i]=new GameObject().transform;_parts[i].SetParent(_rig.transform,false); }
            for(int i=0;i<2;i++) { _wristTargets[i]=new GameObject().transform;_wristTargets[i].SetParent(_parts[i],false);_chainEyes[i]=new GameObject().transform;_chainEyes[i].SetParent(_parts[i],false); }
        }
        void DeleteRig() { Delete();_rig=null; }
    }
}
namespace BepInEx.Configuration
{
    public class ConfigDefinition
    {
        public string Section,Key;
        public ConfigDefinition(string section,string key) {Section=section;Key=key;}
    }
    public class ConfigEntryBase
    {
        public string GetSerializedValue()=>"";
        public void SetSerializedValue(string value) { }
    }
    public class ConfigFile:Dictionary<ConfigDefinition,ConfigEntryBase> {public bool SaveOnConfigSet;}
}
