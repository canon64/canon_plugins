using System;
using System.Collections.Generic;
namespace MapEnvironmentRuntime {}
namespace UnityEngine
{
 public class Object {public string name;}
 public class Component:Object {public T[] GetComponents<T>() where T:class {return this is Transform t ? Array.ConvertAll(t.Colliders,c=>c as T) : new T[0];}}
 public class Transform:Component {public Collider[] Colliders=new Collider[0];}
 public class Collider:Component {public bool enabled=true;}
 public class GameObject:Object
 {
  public bool activeSelf=true;public Transform[] Transforms;public Collider[] Colliders;
  public T[] GetComponentsInChildren<T>(bool inactive) where T:class {if(typeof(T)==typeof(Transform))return Transforms as T[];return Colliders as T[];}
  public void SetActive(bool active){activeSelf=active;}
 }
 public class AsyncOperation
 {
  public bool isDone;private Action<AsyncOperation> _completed;
  public event Action<AsyncOperation> completed {add{_completed+=value;}remove{_completed-=value;}}
  public void CompleteLate(){isDone=true;_completed?.Invoke(this);}
 }
 public class AssetBundleCreateRequest:AsyncOperation {public AssetBundle assetBundle;}
 public class AssetBundle:Object
 {
  public static List<AssetBundle> Loaded=new List<AssetBundle>();public static AssetBundleCreateRequest Next;
  public static int FileLoads;public int Unloads;public string[] Paths;
  public static IEnumerable<AssetBundle> GetAllLoadedAssetBundles(){return Loaded.ToArray();}
  public static AssetBundleCreateRequest LoadFromFileAsync(string path){FileLoads++;Loaded.Add(Next.assetBundle);return Next;}
  public string[] GetAllScenePaths(){return Paths;}
  public void Unload(bool all){Unloads++;Loaded.Remove(this);}
 }
 public struct Vector3 {public float x,y,z;public static float Distance(Vector3 a,Vector3 b){return default;}}
 public struct Color {}
 public class Material:Object {}
 public class Light:Object {}
 public enum FogMode {Linear}
 public enum QueryTriggerInteraction {Ignore}
 public struct RaycastHit {public Collider collider;}
 public static class Physics {public const int DefaultRaycastLayers=0;public static bool Raycast(Vector3 a,Vector3 b,out RaycastHit hit,float d,int mask,QueryTriggerInteraction q){hit=default;return false;}}
 public static class RenderSettings
 {
  public static Material skybox;public static Light sun;public static Rendering.AmbientMode ambientMode;
  public static Color ambientSkyColor,ambientEquatorColor,ambientGroundColor,ambientLight,fogColor;
  public static bool fog;public static FogMode fogMode;public static float fogDensity,fogStartDistance,fogEndDistance,reflectionIntensity,ambientIntensity;
 }
}
namespace UnityEngine.Rendering {public enum AmbientMode {Flat}}
namespace UnityEngine.SceneManagement
{
 using UnityEngine;
 public enum LoadSceneMode {Additive}
 public class Record {public bool Loaded;public string Path;public GameObject Root;}
 public struct Scene
 {
  public Record Data;public string path=>Data?.Path;public bool isLoaded=>Data!=null&&Data.Loaded;
  public bool IsValid(){return Data!=null;}
  public GameObject[] GetRootGameObjects(){return new[]{Data.Root};}
 }
 public static class SceneManager
 {
  public static List<Record> Records=new List<Record>();public static bool FinishSceneImmediately;public static AsyncOperation LastScene,LastUnload;public static int SceneLoads;
  public static int sceneCount=>Records.Count;public static Scene GetSceneAt(int i){return new Scene{Data=Records[i]};}
  public static bool SetActiveScene(Scene s){return true;}
  public static AsyncOperation LoadSceneAsync(string path,LoadSceneMode mode)
  {
   SceneLoads++;var collider=new Collider{name="Lift"};var root=new GameObject{Colliders=new[]{collider},Transforms=new[]{new Transform{name="Lift",Colliders=new[]{collider}}}};
   var record=new Record{Path=path,Loaded=FinishSceneImmediately,Root=root};Records.Add(record);
   LastScene=new AsyncOperation{isDone=FinishSceneImmediately};LastScene.completed+=op=>record.Loaded=true;return LastScene;
  }
  public static AsyncOperation UnloadSceneAsync(Scene s){s.Data.Loaded=false;LastUnload=new AsyncOperation();return LastUnload;}
 }
}
