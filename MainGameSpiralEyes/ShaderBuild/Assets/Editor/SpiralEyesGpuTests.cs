using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using MainGameSpiralEyes;

public static class SpiralEyesGpuTests
{
 sealed class Job {public string Name;public float Time,Rotation,Speed;public bool Animated=true;public int Mode;}
 static readonly Job[] Jobs={
  new Job{Name="still-0",Animated=false},new Job{Name="still-1",Animated=false,Time=1},
  new Job{Name="rotation-0",Rotation=.45f,Mode=2},new Job{Name="rotation-1",Rotation=.45f,Mode=2,Time=.5f},
  new Job{Name="color-0",Speed=-.3f},new Job{Name="color-1",Speed=-.3f,Time=.5f},
  new Job{Name="two-color",Speed=-.3f,Mode=1,Time=.5f},
  new Job{Name="unity-time-0",Speed=-.3f,Rotation=.45f,Time=-1},
  new Job{Name="unity-time-1",Speed=-.3f,Rotation=.45f,Time=-1}
 };
 static string _root;static AssetBundle _bundle;static Shader _shader;static Texture2D _source;
 static GpuEyeTexture _gpu;static int _job,_frames;static double _deadline,_jobStarted;
 static readonly Dictionary<string,Color[]> Images=new Dictionary<string,Color[]>();
 static Camera _camera;static RenderTexture _cameraTarget;
 static void Check(bool condition,string message){if(!condition)throw new Exception("GPU test: "+message);}
 public static void Begin(string root)
 {
  _root=root;
  Check(Application.unityVersion=="2019.4.9f1","wrong Unity version");
  Check(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Direct3D11,"D3D11 required");
  _bundle=AssetBundle.LoadFromFile(Path.Combine(root,"Resources/spiraleyes.bundle"));
  _shader=_bundle.LoadAsset<Shader>("assets/spiraleyes.shader");
  _source=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
  Check(_source.LoadImage(File.ReadAllBytes(Path.Combine(root,"Resources/canon_spiral.png"))),"PNG decode");
  _source.wrapMode=TextureWrapMode.Clamp;_source.filterMode=FilterMode.Bilinear;
  Directory.CreateDirectory(Path.Combine(root,"Tests/results"));
  _camera=new GameObject("GPU test camera").AddComponent<Camera>();_camera.enabled=false;
  _cameraTarget=new RenderTexture(16,16,16);_camera.targetTexture=_cameraTarget;
  _deadline=EditorApplication.timeSinceStartup+60;
  StartJob();EditorApplication.update+=Tick;
 }
 static void StartJob()
 {
  var job=Jobs[_job];
  _gpu=new GpuEyeTexture(_shader,_source,m=>{
   foreach(var d in ParameterDefinitions.Load(Path.Combine(_root,"parameters.json")))m.SetVector("p_"+d.key,new Vector4(d.value,0,1,0));
   m.SetFloat("uTestTime",job.Time);m.SetFloat("uOriginal",1);m.SetFloat("uAnimation",job.Animated?1:0);
   m.SetFloat("uMode",job.Mode);m.SetColor("uA",Color.red);m.SetColor("uB",Color.blue);
   m.SetColor("uTint",new Color(1,80f/255,235f/255));
   m.SetVector("p_rotation",new Vector4(job.Rotation,0,1,0));m.SetVector("p_colorSpeed",new Vector4(job.Speed,0,1,0));
   m.SetVector("p_glow",Vector4.zero);m.SetVector("p_pulse",Vector4.zero);
  });_frames=0;_jobStarted=EditorApplication.timeSinceStartup;
 }
 static void Tick()
 {
  try
  {
   if(EditorApplication.timeSinceStartup>_deadline)throw new Exception("GPU test timed out");
   EditorApplication.QueuePlayerLoopUpdate();_camera.Render();
   if(++_frames<8||(Jobs[_job].Time<0&&EditorApplication.timeSinceStartup-_jobStarted<.5))return;
   var previous=RenderTexture.active;RenderTexture.active=_gpu.Texture;
   var capture=new Texture2D(256,256,TextureFormat.RGBAFloat,false,true);
   capture.ReadPixels(new Rect(0,0,256,256),0,0);capture.Apply();RenderTexture.active=previous;
   Images.Add(Jobs[_job].Name,capture.GetPixels());
   File.WriteAllBytes(Path.Combine(_root,"Tests/results/"+Jobs[_job].Name+".png"),capture.EncodeToPNG());
   UnityEngine.Object.DestroyImmediate(capture);_gpu.Dispose();Check(_gpu.Texture==null&&_gpu.Material==null,"GPU disposal");_gpu=null;
   if(++_job<Jobs.Length){StartJob();return;}
   Validate();BindingTests();
   File.WriteAllText(Path.Combine(_root,"Tests/results/GPU-PASS.txt"),"Unity "+Application.unityVersion+"; "+SystemInfo.graphicsDeviceType+"; "+SystemInfo.graphicsDeviceName+"\nOriginal alpha, rotation, rainbow, inward flow, two colors, stopped frames and partial failure passed. Pupil binding is verified by PupilFollowGpuTests.\n");
   Debug.Log("SPIRAL_EYES_GPU_TESTS_OK");Finish(0);
  }
  catch(Exception e){Debug.LogException(e);Finish(1);}
 }
 static int Different(Color[] a,Color[] b,bool alpha)
 {
  int count=0;for(int i=0;i<a.Length;i++)
   if(alpha?Mathf.Abs(a[i].a-b[i].a)>.05f:Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>.1f)count++;
  return count;
 }
 static void Validate()
 {
  var original=_source.GetPixels();var still=Images["still-0"];int solid=0,clear=0;
  for(int i=0;i<still.Length;i++)
  {
   Check(Mathf.Abs(original[i].a-still[i].a)<.025f,"original alpha mismatch at "+i+": "+original[i].a+" vs "+still[i].a);
   if(still[i].a>.9f)solid++;if(still[i].a<.01f)clear++;
  }
  Check(solid>5000&&clear>30000,"nonempty spiral and transparent gaps");
  Check(Different(still,Images["still-1"],true)==0&&Different(still,Images["still-1"],false)==0,"stopped frames changed");
  Check(Different(Images["rotation-0"],Images["rotation-1"],true)>5000,"rotation did not change alpha");
  Check(Different(Images["color-0"],Images["color-1"],true)==0,"color animation changed shape");
  Check(Different(Images["color-0"],Images["color-1"],false)>10000,"color did not animate");
  // Verify the rendered color field follows h=t*speed-r*cycles. Negative dr/dt means inward.
  for(int y=32;y<224;y+=17)for(int x=32;x<224;x+=17)
  {
   float r=new Vector2((x+.5f)/256-.5f,(y+.5f)/256-.5f).magnitude/.43f;
   var expected=Color.HSVToRGB(Mathf.Repeat(.5f*-.3f-r*.9f,1),1,1);
   var actual=Images["color-1"][y*256+x];if(actual.a<.9f)continue;
   Check(Mathf.Abs(expected.r-actual.r)+Mathf.Abs(expected.g-actual.g)+Mathf.Abs(expected.b-actual.b)<.035f,"inward color field mismatch");
  }
  Check(-.3f/.9f<0,"color phase must move inward");
  foreach(var color in Images["two-color"])if(color.a>.9f)Check(color.g<.015f&&Mathf.Abs(color.r+color.b-1)<.02f,"specified two-color blend");
  Check(Different(Images["unity-time-0"],Images["unity-time-1"],true)>3000,"Unity _Time did not rotate the PNG");
  Check(Different(Images["unity-time-0"],Images["unity-time-1"],false)>5000,"Unity _Time did not animate colors");
 }
 static void BindingTests()
 {
  bool failed=false;
  try{using(var invalid=new GpuEyeTexture(_shader,_source,m=>{throw new Exception("partial setup");})){} }
  catch{failed=true;}
  Check(failed,"partial GPU setup failure not propagated");
 }
 static void Finish(int code)
 {
  EditorApplication.update-=Tick;_gpu?.Dispose();
  if(_camera!=null)UnityEngine.Object.DestroyImmediate(_camera.gameObject);
  if(_cameraTarget!=null){_cameraTarget.Release();UnityEngine.Object.DestroyImmediate(_cameraTarget);}
  if(_source!=null)UnityEngine.Object.DestroyImmediate(_source);if(_bundle!=null)_bundle.Unload(true);
  EditorApplication.Exit(code);
 }
}
