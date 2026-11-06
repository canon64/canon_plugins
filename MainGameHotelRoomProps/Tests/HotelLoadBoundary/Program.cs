using System;
using System.Collections;
using MainGameAiShoujoMode;
using UnityEngine;
using UnityEngine.SceneManagement;
internal static class Program
{
 static int count;
 static void Check(bool value,string name){if(!value)throw new Exception(name);count++;}
 static HotelFloorRuntime Create(){return new HotelFloorRuntime(new HotelFloorDefinition{id="lobby",bundle="lobby.unity3d",bundleName="native_lobby",scene="Lobby",elevatorObject="Lift"});}
 static AssetBundle Reset(bool bundleDone,bool sceneDone)
 {
  AssetBundle.Loaded.Clear();AssetBundle.FileLoads=0;SceneManager.Records.Clear();SceneManager.SceneLoads=0;SceneManager.LastUnload=null;SceneManager.FinishSceneImmediately=sceneDone;
  var bundle=new AssetBundle{name="native_lobby",Paths=new[]{"Assets/Lobby.unity"}};AssetBundle.Next=new AssetBundleCreateRequest{assetBundle=bundle,isDone=bundleDone};return bundle;
 }
 static void Main()
 {
  // No completed callbacks are dispatched before the enumerator resumes.
  var b=Reset(true,true);var f=Create();var e=f.Load("content");
  Check(!e.MoveNext(),"already finished bundle/scene must not need completion callbacks");
  Check(f.Scene.IsValid()&&!f.Roots[0].activeSelf,"real result applied and floor initially hidden");
  f.SetActive(true);Check(f.Roots[0].activeSelf,"loaded floor can activate");
  f.Dispose();SceneManager.LastUnload.CompleteLate();AssetBundle.Next.CompleteLate();SceneManager.LastScene.CompleteLate();
  Check(b.Unloads==1,"late notifications cannot unload twice or reattach assets");
  b=Reset(false,true);f=Create();e=f.Load("content");Check(e.MoveNext()&&e.Current is AssetBundleCreateRequest,"native pending bundle yields once");
  AssetBundle.Next.isDone=true;Check(!e.MoveNext()&&f.Scene.IsValid(),"completed native request works when callback dispatch lags");
  f.Dispose();SceneManager.LastUnload.CompleteLate();Check(b.Unloads==1,"deferred bundle ownership released");
  b=Reset(false,true);f=Create();e=f.Load("content");e.MoveNext();f.Dispose();AssetBundle.Next.CompleteLate();
  Check(b.Unloads==1&&SceneManager.SceneLoads==0,"cancelled pending bundle cannot start scene");
  b=Reset(true,false);f=Create();e=f.Load("content");Check(e.MoveNext()&&e.Current==SceneManager.LastScene,"pending scene yields once");
  SceneManager.LastScene.isDone=true;SceneManager.Records[0].Loaded=true;Check(!e.MoveNext()&&f.Scene.isLoaded,"scene readback independent of callback order");
  f.Dispose();SceneManager.LastUnload.CompleteLate();Check(b.Unloads==1,"scene-first completion safely released");
  b=Reset(true,true);b.name="map/scene/sideloader-renamed.unity3d";AssetBundle.Loaded.Add(b);f=Create();e=f.Load("content");
  Check(!e.MoveNext()&&AssetBundle.FileLoads==0,"borrow by scene identity after Sideloader renames bundle");
  f.Dispose();SceneManager.LastUnload.CompleteLate();Check(b.Unloads==0,"borrowed bundle remains owned by Sideloader");
  Console.WriteLine("HOTEL_LOAD_BOUNDARY_PASS checks="+count);
 }
}
