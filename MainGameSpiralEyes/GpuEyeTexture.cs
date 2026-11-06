using System;
using UnityEngine;
namespace MainGameSpiralEyes
{
 public sealed class GpuEyeTexture : IDisposable
 {
  public CustomRenderTexture Texture {get;private set;}
  public Material Material {get;private set;}
  public GpuEyeTexture(Shader shader,Texture original,Action<Material> configure)
  {
   try
   {
    if(shader==null||!shader.isSupported)throw new InvalidOperationException("Unsupported SpiralEyes shader");
    Material=new Material(shader){name="canon SpiralEyes GPU"};Material.SetTexture("_OriginalDrawing",original);configure(Material);
    Texture=new CustomRenderTexture(256,256,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear)
    {name="canon SpiralEyes GPU",material=Material,updateMode=CustomRenderTextureUpdateMode.Realtime,
     initializationMode=CustomRenderTextureUpdateMode.OnLoad,initializationColor=Color.clear,
     doubleBuffered=false,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
    if(!Texture.Create())throw new InvalidOperationException("SpiralEyes render texture creation failed");
    Texture.Initialize();Texture.Update();
   }
   catch{Dispose();throw;}
  }
  public void Dispose()
  {
   if(Texture!=null){Texture.Release();Release(Texture);Texture=null;}
   if(Material!=null){Release(Material);Material=null;}
  }
  static void Release(UnityEngine.Object value)
  {if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
 }
}
