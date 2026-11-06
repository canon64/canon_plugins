using System;
using System.IO;
using UnityEditor;
using UnityEngine;
public static class SpiralEyesBuild
{
 static float Smooth(float a,float b,float x){float t=Mathf.InverseLerp(a,b,x);return t*t*(3-2*t);}
 public static void Verify(){SpiralEyesGpuTests.Begin(Path.GetFullPath(Path.Combine(Application.dataPath,"../..")));}
 public static void Build()
 {
  try
  {
   string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
   var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/SpiralEyes.shader");
   if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Spiral shader compilation failed");
   var output=Path.Combine(root,"Resources");Directory.CreateDirectory(output);
   var manifest=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild{assetBundleName="spiraleyes.bundle",assetNames=new[]{"Assets/SpiralEyes.shader"}}},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,BuildTarget.StandaloneWindows64);
   if(manifest==null||ShaderUtil.ShaderHasError(shader))throw new Exception("AssetBundle shader compilation failed");
   Debug.Log("SPIRAL_EYES_BUILD_OK");SpiralEyesGpuTests.Begin(root);
  }
  catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
 }
}
