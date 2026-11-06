using System;
using System.IO;
using UnityEditor;
using UnityEngine;
public static class PupilFollowBuild
{
 public static void Compile()
 {
  try
  {
   var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/PupilFollow.shader");
   if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Pupil follow shader compilation failed");
   string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Resources"));
   var result=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild{assetBundleName="pupilfollow-donor.bundle",assetNames=new[]{"Assets/PupilFollow.shader"}},new AssetBundleBuild{assetBundleName="pupilfollow-reference-donor.bundle",assetNames=new[]{"Assets/NativeEyeReference.shader"}}},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,BuildTarget.StandaloneWindows64);
   if(result==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Pupil follow bundle failed");
   Debug.Log("PUPIL_FOLLOW_COMPILE_OK");EditorApplication.Exit(0);
  }
  catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
 }
 public static void Verify(){PupilFollowGpuTests.Run();}
}
