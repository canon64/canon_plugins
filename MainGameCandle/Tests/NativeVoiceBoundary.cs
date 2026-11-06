using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
public class ChaFileParameter{public int personality;}
public class TestNativeHScene:UnityEngine.MonoBehaviour
{
    public HFlag flags;public HVoiceCtrl voice;public List<ChaControl> lstFemale;
}
public class NativeBreathInfo{public string pathAsset,nameFile,word;}
public class HVoiceCtrl
{
    public Dictionary<int,Dictionary<int,Dictionary<int,NativeBreathInfo>>>[] dicVoiceIntos;
    public Dictionary<int,Dictionary<int,NativeBreathInfo>>[] dicShortBreathIntos;
    public Dictionary<int,Dictionary<int,Dictionary<int,NativeBreathInfo>>>[] dicBreathVoiceIntos;
}
public class AssetBundleData
{
    readonly string _key;
    public static readonly Dictionary<string,Task<UnityEngine.AudioClip>> Assets=new Dictionary<string,Task<UnityEngine.AudioClip>>();
    public AssetBundleData(string bundle,string asset){_key=bundle+"|"+asset;}
    public async Task<T> GetAssetAsync<T>() where T:UnityEngine.Object=>(T)(UnityEngine.Object)(await Assets[_key]);
}
public static class AssetBundleManager{public static void UnloadAssetBundle(string bundle,bool force){}}
public class ShortBreathData:UnityEngine.Object
{
    public List<Row> param=new List<Row>();public class Row{public NativeBreathInfo[] infos;}
}
public class BreathAllData:UnityEngine.Object
{
    public List<Mode> param=new List<Mode>();public class Mode{public List<Row> data=new List<Row>();}public class Row{public NativeBreathInfo[] info;}
}
public class VoiceAllData:UnityEngine.Object
{
    public List<Mode> param=new List<Mode>();public class Mode{public List<Row> data=new List<Row>();}public class Row{public NativeBreathInfo[] info;}
}
public static class CommonLib
{
    public static readonly Dictionary<string,UnityEngine.Object> Assets=new Dictionary<string,UnityEngine.Object>();
    public static List<string> GetAssetBundleNameListFromPath(string path,bool sub)=>Assets.Keys.Select(k=>k.Split('|')[0]).Distinct().ToList();
    public static T LoadAsset<T>(string bundle,string asset,bool clone,string manifest,bool check) where T:UnityEngine.Object=>Assets.TryGetValue(bundle+"|"+asset,out var value)?(T)value:null;
}
public static class AssetBundleCheck{public static bool IsFile(string bundle,string asset)=>CommonLib.Assets.ContainsKey(bundle+"|"+asset);}
namespace UnityEngine{public class AudioClip:Object{public float length;}}
