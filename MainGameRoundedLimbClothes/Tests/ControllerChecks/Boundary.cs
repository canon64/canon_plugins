using System;
using System.Collections.Generic;
using System.Linq;
namespace UnityEngine
{
    public class Object {public string name;public bool Destroyed;public static void Destroy(Object o){if(o!=null)o.Destroyed=true;}}
#if !REAL_ADAPTER
    public class Mesh:Object {public int subMeshCount=1;public int blendShapeCount;}
#endif
    public class Shader:Object {}
    public class Material:Object
    {
        public Shader shader;
        readonly Dictionary<string,object> values=new Dictionary<string,object>();
        public Material(Material source){CopyPropertiesFromMaterial(source);}
        public Material(){shader=new Shader();}
        public bool HasProperty(string key)=>true;
        public void CopyPropertiesFromMaterial(Material source){values.Clear();foreach(var p in source.values)values[p.Key]=p.Value;shader=source.shader;}
        public void SetTexture(string key,object value)=>values[key]=value;
        public void SetFloat(string key,float value)=>values[key]=value;
        public object Get(string key)=>values.TryGetValue(key,out var v)?v:null;
    }
#if !REAL_ADAPTER
    public class Transform:Object {}
#endif
    public class SkinnedMeshRenderer:Object
    {
        Material[] materials={new Material()};
        public Material[] sharedMaterials{get=>(Material[])materials.Clone();set=>materials=(Material[])value.Clone();}
        public Transform[] bones=Array.Empty<Transform>();
        public Mesh sharedMesh=new Mesh{name="stock"};
        public float GetBlendShapeWeight(int index)=>0;
        public void SetBlendShapeWeight(int index,float weight){}
    }
}
namespace ExtensibleSaveFormat
{public class PluginData{public int version;public Dictionary<string,object> data=new Dictionary<string,object>();}}
namespace KKAPI{public enum GameMode{MainGame,Maker}}
public class ChaListDefine{public enum CategoryNo{}}
public sealed class ClothesPart{public int id;}
public sealed class ClothesData{public ClothesPart[] parts=Enumerable.Range(0,9).Select(_=>new ClothesPart()).ToArray();}
public class ChaFileCoordinate{public ClothesData clothes=new ClothesData();public ExtensibleSaveFormat.PluginData Extended;}
public sealed class Status{public int coordinateType;public byte[] clothesState=new byte[9];}
public class ChaControl:UnityEngine.Object
{
    public sealed class FileData{public sealed class Parameter{public string fullname="fixture character";}public Parameter parameter=new Parameter();}
    public FileData chaFile=new FileData();
    public int sex=1;public Status fileStatus=new Status();public ChaFileCoordinate nowCoordinate=new ChaFileCoordinate();
    public UnityEngine.SkinnedMeshRenderer rendBody=new UnityEngine.SkinnedMeshRenderer();
}
namespace Sideloader.AutoResolver
{
    public sealed class ResolveInfo{public string GUID;public int Slot;}
    public static class UniversalAutoResolver
    {
        public static readonly Dictionary<int,ResolveInfo> Entries=new Dictionary<int,ResolveInfo>();
        public static ResolveInfo TryGetResolutionInfo(ChaListDefine.CategoryNo category,int localSlot)=>Entries.TryGetValue(localSlot,out var value)?value:null;
    }
}
namespace KKAPI.Chara
{
    public abstract class CharaCustomFunctionController:UnityEngine.Object
    {
        public ChaControl ChaControl=new ChaControl();
        public ExtensibleSaveFormat.PluginData Extended;
        public ExtensibleSaveFormat.PluginData GetExtendedData()=>Extended;
        public void SetExtendedData(ExtensibleSaveFormat.PluginData value)=>Extended=value;
        public ExtensibleSaveFormat.PluginData GetCoordinateExtendedData(ChaFileCoordinate coordinate)=>coordinate.Extended;
        public void SetCoordinateExtendedData(ChaFileCoordinate coordinate,ExtensibleSaveFormat.PluginData value)=>coordinate.Extended=value;
        protected virtual void OnReload(KKAPI.GameMode mode,bool maintainState){}
        protected virtual void OnCoordinateBeingLoaded(ChaFileCoordinate coordinate,bool maintainState){}
        protected abstract void OnCardBeingSaved(KKAPI.GameMode mode);
        protected virtual void OnCoordinateBeingSaved(ChaFileCoordinate coordinate){}
        protected virtual void OnDestroy(){}
        public void Reload(bool maintain=false)=>OnReload(KKAPI.GameMode.Maker,maintain);
        public void SaveCard()=>OnCardBeingSaved(KKAPI.GameMode.Maker);
        public void LoadCoordinate(ChaFileCoordinate coordinate)=>OnCoordinateBeingLoaded(coordinate,false);
        public void SaveCoordinate(ChaFileCoordinate coordinate)=>OnCoordinateBeingSaved(coordinate);
        public void Dispose()=>OnDestroy();
    }
}
namespace MainGameRoundedLimbClothes
{
    public sealed partial class Plugin
    {
        public const string ModGuid="codex.local.kks.rounded-limb-clothes";
        public static Plugin Instance=new Plugin();public bool Permit,Diagnostics;public int Reports;
        public bool Allows(ChaControl cha)=>Permit;
        public void ActorReloaded(){}
        public void Report(Exception ex){Reports++;}
    }
#if !REAL_ADAPTER
    internal sealed class BodyMeshAdapter
    {
        public static int Builds;
        internal readonly UnityEngine.Mesh Source;
        internal BodyMeshAdapter(UnityEngine.Mesh source,UnityEngine.Transform[] bones)
        {if(source.name=="unsupported")throw new InvalidOperationException("unsupported");Source=source;}
        internal UnityEngine.Mesh Generate(PositionSettings settings,bool arms,bool legs)
        {
            if(settings.LeftArm==13)throw new InvalidOperationException("deliberate geometry rejection");
            Builds++;return new UnityEngine.Mesh{name=$"arms={arms};legs={legs};LA={settings.LeftArm};LL={settings.LeftLeg}"};
        }
    }
#endif
}
