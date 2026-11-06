using System;
using System.Collections;
using System.IO;
using System.Reflection;
using CanonGate;
using MainGameDesktopDisplay;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class DesktopSmokeRunner : MonoBehaviour
{
    [Serializable] class Card { public string device; public int x,y; }
    [Serializable] class Result
    {
        public bool passed;
        public string unityVersion,graphicsDevice,device,sourceImage,renderedImage,secondFrame,error;
        public int width,height,checks;
        public float liveChange,sourceRenderError;
        public string scope="Real product sources and Unity rendering; KKS host/gate/gizmo integration is not exercised.";
    }
    string output;
    Plugin plugin;
    Camera camera;
    RenderTexture target;
    Result result = new Result();
    bool finished;
    static readonly BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    T Read<T>(string name) => (T)typeof(Plugin).GetField(name,Fields).GetValue(plugin);
    void Check(bool condition,string label)
    {
        if(!condition) throw new Exception(label);
        result.checks++;
        File.AppendAllText(Path.Combine(output,"checks.txt"),"PASS " + label + "\n");
    }
    public void Begin(string folder) { output=folder; StartCoroutine(RunSafe()); }
    IEnumerator RunSafe()
    {
        var run=Run();
        while(true)
        {
            object next;
            try { if(!run.MoveNext()) break; next=run.Current; }
            catch(Exception error) { result.error=error.ToString(); Finish(); yield break; }
            yield return next;
        }
        Finish();
    }
    IEnumerator Run()
    {
        result.unityVersion=Application.unityVersion; result.graphicsDevice=SystemInfo.graphicsDeviceName;
        Check(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null,"real graphics device");
        Card card=JsonUtility.FromJson<Card>(File.ReadAllText(Path.Combine(output,"card.json")));
        result.device=card.device;
        var cameraObject=new GameObject("SmokeCamera");
        camera=cameraObject.AddComponent<Camera>(); cameraObject.tag="MainCamera";
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.065f,.075f,.1f);
        camera.fieldOfView=34; camera.nearClipPlane=.05f; camera.farClipPlane=20;
        target=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32);
        Check(target.Create(),"render target creation"); camera.targetTexture=target;
        var host=new GameObject("RealProductSourceHost"); plugin=host.AddComponent<Plugin>();
        GateApi.Current=new GateApi.SessionContext { Kind=GateApi.SessionKind.H,Scene=this,Id=1 };
        GateApi.Start();
        Read<BepInEx.Configuration.ConfigEntry<string>>("_monitorDevice").Value=card.device;
        plugin.PlaybackBarPrimaryAction();
        var root=Read<GameObject>("_root");
        Check(root!=null,"actual PlaybackBarPrimaryAction created product screen");
        Check(Read<DesktopCapture>("_capture")!=null,"actual product opened native desktop capture");
        var panel=Read<GameObject>("_panel");
        Texture2D texture=Read<Texture2D>("_texture");
        Check(texture!=null && texture.format==TextureFormat.BGRA32,"actual product BGRA texture");
        var monitors=DesktopCapture.Enumerate(); DesktopCapture.Monitor monitor=null;
        foreach(var m in monitors) if(m.Device==card.device) monitor=m;
        Check(monitor!=null,"requested physical monitor selected");
        result.width=texture.width;result.height=texture.height;
        float u=(card.x-monitor.Rect.Left)/(float)monitor.Rect.Width;
        float v=1-(card.y-monitor.Rect.Top)/(float)monitor.Rect.Height;
        yield return new WaitForSecondsRealtime(.4f);
        camera.Render();
        var a=texture.GetPixelBilinear(u,v);
        Check(Difference(a,new Color(32/255f,180/255f,90/255f))<.09f,"phase A pixels originate from physical desktop window");
        result.sourceImage=Path.Combine(output,"desktop-source-a.png");
        File.WriteAllBytes(result.sourceImage,texture.EncodeToPNG());
        result.renderedImage=Path.Combine(output,"unity-display-a.png");
        var frameA=ReadCamera();
        File.WriteAllBytes(result.renderedImage,frameA.EncodeToPNG());
        Vector3 world=panel.transform.TransformPoint(new Vector3(u-.5f,v-.5f,0));
        var pixel=camera.WorldToScreenPoint(world);
        var drawn=frameA.GetPixel(Mathf.RoundToInt(pixel.x),Mathf.RoundToInt(pixel.y));
        result.sourceRenderError=Difference(a,drawn);
        Check(result.sourceRenderError<.12f,"physical desktop marker is actually rasterized on the 3D mesh");
        File.WriteAllText(Path.Combine(output,"phase-b"),"change physical window");
        yield return new WaitForSecondsRealtime(.7f);
        camera.Render();
        var b=texture.GetPixelBilinear(u,v);
        result.liveChange=Difference(a,b);
        Check(Difference(b,new Color(240/255f,110/255f,30/255f))<.09f && result.liveChange>.3f,"OnWillRenderObject updated live desktop from green A to orange B");
        result.secondFrame=Path.Combine(output,"unity-display-b.png");
        var frameB=ReadCamera();
        File.WriteAllBytes(result.secondFrame,frameB.EncodeToPNG());
        drawn=frameB.GetPixel(Mathf.RoundToInt(pixel.x),Mathf.RoundToInt(pixel.y));
        Check(Difference(b,drawn)<.12f,"changed desktop pixels reach the rendered screen");
        plugin.PlaybackBarDelete(); yield return null;
        Check(Read<GameObject>("_root")==null && Read<Texture2D>("_texture")==null && Read<DesktopCapture>("_capture")==null,"actual delete releases mesh, texture and native capture");
        var blank=ReadCamera(); var cleared=blank.GetPixel(Mathf.RoundToInt(pixel.x),Mathf.RoundToInt(pixel.y));
        Check(Difference(cleared,camera.backgroundColor)<.08f,"deleted display leaves no rendered surface");
        plugin.PlaybackBarPrimaryAction(); yield return new WaitForSecondsRealtime(.2f); camera.Render();
        Check(Read<Texture2D>("_texture")!=null,"actual display can be recreated after delete");
        GateApi.Current=null; GateApi.Stop(); yield return null;
        Check(Read<GameObject>("_root")==null && Read<DesktopCapture>("_capture")==null,"host session end stops product rendering and capture");
        Destroy(frameA);Destroy(frameB);Destroy(blank);
        result.passed=true;
    }
    Texture2D ReadCamera()
    {
        camera.Render(); var previous=RenderTexture.active; RenderTexture.active=target;
        var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply(); RenderTexture.active=previous; return image;
    }
    static float Difference(Color a,Color b) => Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Max(Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b)));
    void Finish()
    {
        if(finished) return; finished=true;
        if(plugin!=null) plugin.PlaybackBarDelete();
        if(camera!=null) camera.targetTexture=null;
        if(target!=null) { target.Release(); Destroy(target); }
        File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(result,true));
        File.WriteAllText(Path.Combine(output,"finished"),result.passed ? "PASS" : "FAIL");
    }
}
