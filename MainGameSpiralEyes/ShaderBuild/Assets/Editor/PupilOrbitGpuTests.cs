using System;
using System.IO;
using MainGameSpiralEyes;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
public static class PupilOrbitGpuTests
{
 static PupilOrbitBindings _bindings;static Renderer[] _eyes;static Texture2D _source;static Material _original;static Camera _camera;static RenderTexture _target;
 static int _stage,_frames;static double _deadline;static readonly Vector2[,] Centers=new Vector2[2,2];
 static void Check(bool value,string reason){if(!value)throw new Exception("Pupil GPU: "+reason);}
 public static void Begin(Shader shader)
 {
  Check(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Direct3D11,"D3D11 required");
  _source=new Texture2D(64,64,TextureFormat.RGBA32,false,true){wrapMode=TextureWrapMode.Clamp};
  for(int y=0;y<64;y++)for(int x=0;x<64;x++)_source.SetPixel(x,y,x>=30&&x<34&&y>=30&&y<34?Color.white:Color.clear);_source.Apply();
  _original=new Material(Shader.Find("Unlit/Transparent"));_original.mainTexture=_source;
  _eyes=new Renderer[2];for(int side=0;side<2;side++){_eyes[side]=new GameObject("pupil test "+side).AddComponent<MeshRenderer>();_eyes[side].sharedMaterial=_original;}
  _bindings=new PupilOrbitBindings(shader);_bindings.Set(.1f,0);for(int side=0;side<2;side++)_bindings.Apply(_eyes[side],side);
  _camera=new GameObject("orbit test camera").AddComponent<Camera>();_camera.enabled=false;_target=new RenderTexture(16,16,16);_camera.targetTexture=_target;
  _deadline=EditorApplication.timeSinceStartup+60;Stage();EditorApplication.update+=Tick;
 }
 static CustomRenderTexture Texture(int side)
 {var block=new MaterialPropertyBlock();_eyes[side].GetPropertyBlock(block,0);return block.GetTexture("_MainTex") as CustomRenderTexture;}
 static void Stage()
 {
  _frames=0;for(int side=0;side<2;side++){var texture=Texture(side);Check(texture!=null,"real iris texture not bound");texture.material.SetVector("_Orbit",new Vector4(.1f,0,_stage==0?0:.25f,0));texture.Update();}
 }
 static Vector2 Center(CustomRenderTexture texture)
 {
  var previous=RenderTexture.active;RenderTexture.active=texture;var capture=new Texture2D(texture.width,texture.height,TextureFormat.RGBAFloat,false,true);
  try
  {
   capture.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);capture.Apply();var pixels=capture.GetPixels();float total=0,xsum=0,ysum=0;
   for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++){float weight=pixels[y*texture.width+x].a;total+=weight;xsum+=(x+.5f)/texture.width*weight;ysum+=(y+.5f)/texture.height*weight;}
   Check(total>1,"empty GPU result");return new Vector2(xsum/total,ysum/total);
  }
  finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(capture);}
 }
 static void Tick()
 {
  try
  {
   if(EditorApplication.timeSinceStartup>_deadline)throw new Exception("GPU test deadline");
   EditorApplication.QueuePlayerLoopUpdate();_camera.Render();if(++_frames<8)return;
   for(int side=0;side<2;side++)Centers[_stage,side]=Center(Texture(side));
   if(_stage++==0){Stage();return;}
   Check(Centers[0,0].x>.57f&&Centers[0,1].x>.57f,"inward peaks must alternate between the two eyes");
   Check(Centers[1,0].y<.43f&&Centers[1,1].y>.57f,"opposite orbital directions / quarter period");
   Check(Mathf.Abs(Centers[1,0].x-.5f)<.025f&&Mathf.Abs(Centers[1,1].x-.5f)<.025f,"pupil did not translate in a circle");
   Check(_original.mainTexture==_source&&_original.mainTextureOffset==Vector2.zero,"original eye material was changed");
   var first=Texture(0);_bindings.Apply(_eyes[0],0);Check(Texture(0)==first,"same eye recreated its GPU resources");
   _bindings.Unbind();for(int side=0;side<2;side++){var block=new MaterialPropertyBlock();_eyes[side].GetPropertyBlock(block,0);Check(block.isEmpty,"iris binding was not restored");}
   File.WriteAllText(@"J:\temp\pupil-orbit-gpu-pass.txt","PASS: real PupilOrbitBindings, D3D11 shader readback, opposite directions, alternating inward peaks, original material preservation, resource reuse and restoration.");
   Debug.Log("PUPIL_ORBIT_GPU_TESTS_OK");Finish(0);
  }
  catch(Exception ex){Debug.LogException(ex);Finish(1);}
 }
 static void Finish(int code)
 {
  EditorApplication.update-=Tick;_bindings?.Dispose();if(_eyes!=null)foreach(var eye in _eyes)if(eye!=null)UnityEngine.Object.DestroyImmediate(eye.gameObject);
  PupilOrbitBuild.RestoreCachePreferences();
  if(_source!=null)UnityEngine.Object.DestroyImmediate(_source);if(_original!=null)UnityEngine.Object.DestroyImmediate(_original);
  if(_camera!=null)UnityEngine.Object.DestroyImmediate(_camera.gameObject);if(_target!=null){_target.Release();UnityEngine.Object.DestroyImmediate(_target);}EditorApplication.Exit(code);
 }
}
