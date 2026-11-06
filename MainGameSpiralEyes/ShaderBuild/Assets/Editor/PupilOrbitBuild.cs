using System;
using System.IO;
using UnityEditor;
using UnityEngine;
public static class PupilOrbitBuild
{
 static bool _cacheSetup,_cacheHadFolder,_cacheHadEnabled,_cacheEnabled;static string _cacheFolder;
 static void ConfigureCache()
 {
  _cacheHadFolder=EditorPrefs.HasKey("GICacheFolder");_cacheFolder=EditorPrefs.GetString("GICacheFolder");
  _cacheHadEnabled=EditorPrefs.HasKey("GICacheEnableCustomPath");_cacheEnabled=EditorPrefs.GetBool("GICacheEnableCustomPath");_cacheSetup=true;
  EditorPrefs.SetBool("GICacheEnableCustomPath",true);EditorPrefs.SetString("GICacheFolder",@"J:\temp\unity-gicache");
  typeof(Lightmapping).GetMethod("UpdateCachePath",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic).Invoke(null,null);
 }
 public static void RestoreCachePreferences()
 {
  if(!_cacheSetup)return;_cacheSetup=false;
  if(_cacheHadFolder)EditorPrefs.SetString("GICacheFolder",_cacheFolder);else EditorPrefs.DeleteKey("GICacheFolder");
  if(_cacheHadEnabled)EditorPrefs.SetBool("GICacheEnableCustomPath",_cacheEnabled);else EditorPrefs.DeleteKey("GICacheEnableCustomPath");
 }
 public static void Build()
 {
  try
  {
   ConfigureCache();
   var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/PupilOrbit.shader");
   if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Pupil orbit shader compilation failed");
   string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Resources"));Directory.CreateDirectory(output);
   var result=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild{assetBundleName="pupilorbit.bundle",assetNames=new[]{"Assets/PupilOrbit.shader"}}},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,BuildTarget.StandaloneWindows64);
   if(result==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Pupil orbit bundle failed");
   Debug.Log("PUPIL_ORBIT_BUILD_OK");SyringeContactProductTests.Run();PupilOrbitGpuTests.Begin(shader);
  }
  catch(Exception e){RestoreCachePreferences();Debug.LogException(e);EditorApplication.Exit(1);}
 }
}
