using System;
using System.Collections.Generic;
using System.IO;
using MainGameSpiralEyes;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
public static class PupilFollowGpuTests
{
 static AssetBundle _bundle;static Shader _native,_shader;static Material _material;static Renderer _eye;
 static Camera _camera;static RenderTexture _target;static Texture2D _pupil,_spiral,_clear;static Mesh _mesh;
 static PupilFollowBindings _follow;static PupilOrbitBindings _orbit;static string _root;
 static int _checks,_stage,_frames;static double _deadline;static readonly List<string> Results=new List<string>();
 static readonly Vector2[] Scales={new Vector2(1,1),new Vector2(1.7f,.8f),new Vector2(-1.3f,1.8f),new Vector2(.85f,-1.4f)};
 static void Check(bool ok,string reason){_checks++;if(!ok)throw new Exception("PupilFollow GPU: "+reason);}
 static Texture2D Dot(float radius,Color color)
 {
  var texture=new Texture2D(256,256,TextureFormat.RGBAFloat,false,true){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
  var pixels=new Color[256*256];for(int y=0;y<256;y++)for(int x=0;x<256;x++){var p=new Vector2((x+.5f)/256-.5f,(y+.5f)/256-.5f);pixels[y*256+x]=p.magnitude<radius?color:Color.clear;}
  texture.SetPixels(pixels);texture.Apply();return texture;
 }
 static Color[] Capture()
 {
  _camera.Render();var previous=RenderTexture.active;RenderTexture.active=_target;
  var image=new Texture2D(256,256,TextureFormat.RGBAFloat,false,true);
  try{image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();return image.GetPixels();}
  finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);}
 }
 static Vector2 Center(Color[] pixels,Color[] baseline=null)
 {
  float total=0,xsum=0,ysum=0;
  for(int y=0;y<256;y++)for(int x=0;x<256;x++)
  {
   int i=y*256+x;float w=baseline==null?pixels[i].a:Mathf.Abs(pixels[i].r-baseline[i].r)+Mathf.Abs(pixels[i].g-baseline[i].g)+Mathf.Abs(pixels[i].b-baseline[i].b);
   total+=w;xsum+=(x+.5f)*w;ysum+=(y+.5f)*w;
  }
  Check(total>2,"empty render/readback: "+total);return new Vector2(xsum/total,ysum/total);
 }
 static float Difference(Color[] a,Color[] b)
 {float max=0;for(int i=0;i<a.Length;i++){max=Mathf.Max(max,Mathf.Abs(a[i].r-b[i].r),Mathf.Abs(a[i].g-b[i].g),Mathf.Abs(a[i].b-b[i].b),Mathf.Abs(a[i].a-b[i].a));}return max;}
 static Vector2 Spread(Color[] pixels,Vector2 center,Color[] baseline=null)
 {
  float total=0,xsum=0,ysum=0;
  for(int y=0;y<256;y++)for(int x=0;x<256;x++)
  {
   int i=y*256+x;float weight=baseline==null?pixels[i].a:Mathf.Abs(pixels[i].r-baseline[i].r)+Mathf.Abs(pixels[i].g-baseline[i].g)+Mathf.Abs(pixels[i].b-baseline[i].b);
   total+=weight;xsum+=Mathf.Pow(x+.5f-center.x,2)*weight;ysum+=Mathf.Pow(y+.5f-center.y,2)*weight;
  }
  return new Vector2(Mathf.Sqrt(xsum/total),Mathf.Sqrt(ysum/total));
 }
 static void NativeMapping(Vector2 scale,Vector2 offset,float rotation)
 {_material.SetTextureScale("_MainTex",scale);_material.SetTextureOffset("_MainTex",offset);_material.SetFloat("_rotation",rotation);}
 public static void Run()
 {
  try
  {
   Check(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Direct3D11,"D3D11 required");
   _root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
   _bundle=AssetBundle.LoadFromFile(Path.Combine(_root,"Resources/pupilfollow.bundle"));
   _shader=_bundle.LoadAsset<Shader>("assets/pupilfollow.shader");
   var referenceBundle=AssetBundle.LoadFromFile(Path.Combine(_root,"Tests/results/pupil-follow/native-reference.bundle"));
   _native=referenceBundle.LoadAsset<Shader>("assets/nativeeyereference.shader");
   Check(_shader!=null&&_native!=null&&_shader.isSupported&&_native.isSupported,"merged shaders unsupported");
   Check(_shader.passCount==_native.passCount+1,"native/overlay pass count");
   _pupil=Dot(.06f,new Color(0,1,0,1));_spiral=Dot(.18f,new Color(3,0,0,1));_clear=Dot(0,Color.clear);
   _material=new Material(_native);_material.shaderKeywords=new[]{"_ALPHAPREMULTIPLY_ON","_ISHIGHLIGHT_ON"};_material.SetTexture("_MainTex",_pupil);
   foreach(var name in new[]{"_expression","_overtex1","_overtex2","_overtex3"})_material.SetTexture(name,_clear);
   _material.SetColor("_Color",Color.white);_material.SetColor("_shadowcolor",Color.white);_material.SetFloat("_exppower",0);_material.SetFloat("_isHighLight",0);
   _material.SetTextureScale("_texcoord",new Vector2(.9f,.85f));_material.SetTextureOffset("_texcoord",new Vector2(.04f,.08f));
   _mesh=new Mesh();_mesh.vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(-1,1,0)};
   _mesh.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};
   _mesh.uv2=new[]{new Vector2(.2f,.1f),new Vector2(.7f,.1f),new Vector2(.7f,.9f),new Vector2(.2f,.9f)};
   _mesh.uv3=new[]{new Vector2(.4f,.3f),new Vector2(.8f,.3f),new Vector2(.8f,.6f),new Vector2(.4f,.6f)};
   _mesh.triangles=new[]{0,2,1,0,3,2};_mesh.RecalculateNormals();_mesh.RecalculateBounds();
   var eye=new GameObject("native eye GPU fixture");eye.AddComponent<MeshFilter>().sharedMesh=_mesh;_eye=eye.AddComponent<MeshRenderer>();_eye.sharedMaterial=_material;
   _camera=new GameObject("pupil follow verification camera").AddComponent<Camera>();_camera.enabled=false;_camera.transform.position=new Vector3(0,0,-2);
   _camera.orthographic=true;_camera.orthographicSize=1;_camera.clearFlags=CameraClearFlags.SolidColor;_camera.backgroundColor=Color.clear;_camera.allowHDR=true;_camera.renderingPath=RenderingPath.Forward;
   _target=new RenderTexture(256,256,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);_target.Create();_camera.targetTexture=_target;
   _follow=new PupilFollowBindings(_shader);
   NativeMapping(Vector2.one,Vector2.zero,0);var original=Capture();
   _follow.Apply(_eye,_spiral,Vector2.zero,0,0,false);Check(Difference(original,Capture())<.00001f,"zero-opacity changed the original compiled eye pass");
   Check(_eye.sharedMaterials.Length==1&&_eye.GetComponent<MeshFilter>().sharedMesh==_mesh,"added material or mesh");
   var heart=Dot(.12f,new Color(.7f,.2f,1,1));
   _material.SetTexture("_expression",heart);_material.SetFloat("_exppower",.7f);_material.SetTexture("_overtex1",heart);_material.SetFloat("_isHighLight",1);
   _follow.Restore();var withHeart=Capture();_follow.Apply(_eye,_spiral,Vector2.zero,0,0,false);
   Check(Difference(withHeart,Capture())<.00001f,"zero opacity changed heart/highlight rendering");
   Check(_material.GetTexture("_expression")==heart&&_material.GetTexture("_overtex1")==heart&&_material.GetFloat("_exppower")==.7f,"base textures or expression power changed");
   _material.SetTexture("_expression",_clear);_material.SetTexture("_overtex1",_clear);_material.SetFloat("_exppower",0);_material.SetFloat("_isHighLight",0);UnityEngine.Object.DestroyImmediate(heart);
   Results.Add("Original compiled native pass pixel equality with zero spiral opacity");
   for(int side=0;side<2;side++)for(int k=0;k<Scales.Length;k++)
   {
    var scale=Scales[k];NativeMapping(scale,new Vector2((1-scale.x)/2+.065f,(1-scale.y)/2-.045f),.037f*(k-1));
    // No Apply/rebinding between these writes and the image comparison.
    _follow.SetOpacity(_eye,0);var native=Capture();_follow.SetOpacity(_eye,1);var over=Capture();
    var nc=Center(native);var sc=Center(over,native);Check(Vector2.Distance(nc,sc)<1.5f,"native pupil/spiral center mismatch "+side+"/"+k+": "+nc+" vs "+sc);
    var ns=Spread(native,nc);var ss=Spread(over,sc,native);Check(Mathf.Abs(ns.x-ss.x)<1.1f&&Mathf.Abs(ns.y-ss.y)<1.1f,"spiral did not share pupil scale/rotation: "+ns+" vs "+ss);
    if(k==0)
    {
     _follow.SetOpacity(_eye,.3f);var faded=Capture();float full=0,part=0;
     for(int i=0;i<over.Length;i++){full+=Mathf.Abs(over[i].r-native[i].r);part+=Mathf.Abs(faded[i].r-native[i].r);}
     Check(Mathf.Abs(part/full-.3f)<.015f,"opacity changed UV or alpha multiplier");
     float max=0;foreach(var p in over)max=Mathf.Max(max,p.r);Check(max>2.8f,"HDR spiral emission was clamped");
    }
   }
   Results.Add("Live native UV0/pupil ST/rotation changes, anisotropic and signed scales, opacity and HDR");
   NativeMapping(Vector2.one,Vector2.zero,0);_follow.SetOpacity(_eye,0);var basePosition=Capture();
   _follow.Apply(_eye,_spiral,new Vector2(.3f,-.15f),1,0,false);var positioned=Center(Capture(),basePosition);var origin=Center(basePosition);
   Check(Mathf.Abs(positioned.x-origin.x-256*.1f/.9f)<1.5f&&Mathf.Abs(positioned.y-origin.y+256*.05f/.85f)<1.5f,"pupil-local doll position mapping");
   _follow.Restore();Check(_material.shader==_native&&_material.GetTexture("_MainTex")==_pupil,"native material/texture restoration");
   _follow.Apply(_eye,_spiral,Vector2.zero,1,0,false);
   var orbitBundle=AssetBundle.LoadFromFile(Path.Combine(_root,"Resources/pupilorbit.bundle"));
   _orbit=new PupilOrbitBindings(orbitBundle.LoadAsset<Shader>("assets/pupilorbit.shader"));_orbit.Set(.08f,0);_orbit.Apply(_eye,0);
   _deadline=EditorApplication.timeSinceStartup+60;_stage=0;PrepareStage();EditorApplication.update+=Tick;
  }
  catch(Exception e){Debug.LogException(e);Finish(1);}
 }
 static void PrepareStage()
 {
  _frames=0;int side=_stage/4;float phase=(_stage%4)*.25f;
  _orbit.Unbind();NativeMapping(new Vector2(side==0?1.4f:-1.4f,.8f),new Vector2(side==0?-.2f:1.2f,.1f),.025f);
  _orbit.Apply(_eye,side);var value=new Vector4(_stage%4==0?0:.08f,0,phase,0);
  var block=new MaterialPropertyBlock();_eye.GetPropertyBlock(block,0);
  foreach(var name in new[]{"_MainTex","_overtex1","_overtex2","_expression"})
  {var texture=block.GetTexture(name) as CustomRenderTexture;if(texture!=null){texture.material.SetVector("_Orbit",value);texture.Update();}}
  _follow.Apply(_eye,_spiral,Vector2.zero,0,side,false);_follow.SetOrbit(_eye,value);
 }
 static void Tick()
 {
  try
  {
   if(EditorApplication.timeSinceStartup>_deadline)throw new Exception("orbit GPU verification timed out");
   EditorApplication.QueuePlayerLoopUpdate();_camera.Render();if(++_frames<6)return;
   _follow.SetOpacity(_eye,0);var baseline=Capture();_follow.SetOpacity(_eye,1);var actual=Capture();
   var native=Center(baseline);var spiral=Center(actual,baseline);
   Check(Vector2.Distance(native,spiral)<1.5f,"orbit double movement/phase mismatch stage "+_stage+": "+native+" vs "+spiral);
   if(++_stage<8){PrepareStage();return;}
   var phase=_orbit.Orbit;_orbit.Unbind();_orbit.Apply(_eye,1);Check(_orbit.Orbit==phase,"rebind reset orbit phase");
   _orbit.Dispose();_orbit=null;_follow.SetOrbit(_eye,Vector4.zero);
   _material.SetFloat("_exppower",.7f);_follow.Apply(_eye,_spiral,Vector2.zero,1,0,true);Check(_material.GetFloat("_exppower")==0,"manual spiral duplicate not suppressed");
   _material.SetTextureOffset("_MainTex",new Vector2(.12f,.17f));_follow.Restore();_follow.Restore();
   Check(_material.shader==_native&&_material.GetFloat("_exppower")==.7f&&_material.GetTextureOffset("_MainTex")==new Vector2(.12f,.17f),"cleanup rolled back native writes");
   _follow.Apply(_eye,_spiral,Vector2.zero,1,0,false);_material.shader=Shader.Find("Unlit/Transparent");_follow.Restore();Check(_material.shader.name=="Unlit/Transparent","cleanup overwrote a later shader replacement");
   bool rejected=false;try{_follow.Apply(_eye,_spiral,Vector2.zero,1,0,false);}catch(InvalidOperationException){rejected=true;}Check(rejected,"unsupported shader accepted");
   Results.Add("Real orbit CRT readback: zero/nonzero radius, all quarter phases, signed left/right UVs, no phase reset");
   Results.Add("Position correction, manual expression suppression, preservation of later native writes, repeated cleanup and unsupported shader rejection");
   Directory.CreateDirectory(Path.Combine(_root,"Tests/results/pupil-follow"));
   File.WriteAllText(Path.Combine(_root,"Tests/results/pupil-follow/GPU-PASS.txt"),"Unity "+Application.unityVersion+"; "+SystemInfo.graphicsDeviceName+"; "+_checks+" assertions\n"+string.Join("\n",Results.ToArray())+"\nGame rendering not verified.\n");
   Debug.Log("PUPIL_FOLLOW_GPU_TESTS_OK assertions="+_checks);Finish(0);
  }
  catch(Exception e){Debug.LogException(e);Finish(1);}
 }
 static void Finish(int code)
 {
  EditorApplication.update-=Tick;_orbit?.Dispose();_follow?.Restore();
  if(_eye!=null)UnityEngine.Object.DestroyImmediate(_eye.gameObject);if(_camera!=null)UnityEngine.Object.DestroyImmediate(_camera.gameObject);
  foreach(var obj in new UnityEngine.Object[]{_material,_pupil,_spiral,_clear,_mesh,_target})if(obj!=null)UnityEngine.Object.DestroyImmediate(obj);
  EditorApplication.Exit(code);
 }
}
