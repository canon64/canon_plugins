using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace MapEnvironmentRuntime
{
    [RequireComponent(typeof(Camera))]
    public sealed class SkyTemporalFilter : MonoBehaviour
    {
        public EnvironmentWorld World { get; private set; }
        public int AccumulatedFrames { get; private set; }
        public bool HistoryValid => _valid;
        public string ResetReason { get; private set; }
        public string Error { get; private set; }
        public float CpuMilliseconds { get; private set; }
        public bool ShadowActive => _shadow != null && World != null && World.Sun.cookie == _shadow;
        public int CloudWidth => _currentColor == null ? default(int) : _currentColor.width;
        public int CloudHeight => _currentColor == null ? default(int) : _currentColor.height;
        public double GpuFrameMilliseconds { get; private set; }
        public bool GpuFrameTimingAvailable { get; private set; }
        private FrameTiming[] _frameTiming;
        private Camera _camera;
        private Material _material;
        private RenderTexture _currentColor,_currentData,_historyColor,_historyData,_resolvedColor,_resolvedData,_shadow;
        private RenderBuffer[] _targets;
        private DepthTextureMode _oldDepth;
        private Matrix4x4 _previousProjection;
        private Vector3 _previousPosition;
        private Quaternion _previousRotation;
        private double _previousSeconds;
        private Parameters _previousSettings;
        private bool _valid;
        private float _shadowAt;
        public CloudDiagnostics ExportDiagnostics(string directory)
        {
            if(_currentColor==null) throw new InvalidOperationException("Cloud buffers are not ready");
            var result=new CloudDiagnostics{cloudWidth=CloudWidth,cloudHeight=CloudHeight,historyValid=_valid,resetReason=ResetReason,shadowActive=ShadowActive};
            result.cloud=Export(_historyColor,Path.Combine(directory,"cloud-color.png"),false,out float low,out float high);
            result.cloudTransmittanceMinimum=low;result.cloudTransmittanceMaximum=high;
            if(_shadow!=null)
            {
                result.shadow=Export(_shadow,Path.Combine(directory,"cloud-shadow.png"),true,out low,out high);
                result.shadowMinimum=low;result.shadowMaximum=high;
            }
            return result;
        }
        private string Export(RenderTexture source,string path,bool opaque,out float minimum,out float maximum)
        {
            var previous=RenderTexture.active;Texture2D texture=null;var p=World.Settings;
            try
            {
                RenderTexture.active=source;texture=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false,true);
                texture.ReadPixels(new Rect(p.N("math.zero"),p.N("math.zero"),source.width,source.height),p.I("math.zero"),p.I("math.zero"));texture.Apply();
                var pixels=texture.GetPixels();minimum=p.N("math.one");maximum=p.N("math.zero");
                for(int index=p.I("math.zero");index<pixels.Length;index++)
                {
                    float value=opaque ? pixels[index].r : pixels[index].a;minimum=Mathf.Min(minimum,value);maximum=Mathf.Max(maximum,value);
                    if(opaque) pixels[index].a=p.N("math.one");
                }
                texture.SetPixels(pixels);texture.Apply();Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,ImageConversion.EncodeToPNG(texture));return path;
            }
            finally {RenderTexture.active=previous;if(texture!=null) Destroy(texture);}
        }

        public void Initialize(EnvironmentWorld world,Material source)
        {
            World=world; _camera=GetComponent<Camera>(); _oldDepth=_camera.depthTextureMode;
            _camera.depthTextureMode|=DepthTextureMode.Depth;
            _material=new Material(source); _targets=new RenderBuffer[world.Settings.I("math.two")];
            _frameTiming=new FrameTiming[world.Settings.I("math.one")];
            if(!_material.shader.isSupported || _material.GetTexture("_NoiseTex")==null || _material.GetTexture("_DetailTex")==null)
                throw new InvalidOperationException("Shared cloud shader/noise textures are unavailable");
            UnityEngine.Debug.Log("CLOUD_RENDERER_ATTACHED "+_camera.name);
        }
        private RenderTexture Buffer(int width,int height,string name,RenderTextureFormat format)
        {
            var result=new RenderTexture(width,height,World.Settings.I("math.zero"),format,RenderTextureReadWrite.Linear)
            {name=name,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            result.Create();
            var previous=RenderTexture.active; RenderTexture.active=result;
            float zero=World.Settings.N("math.zero"); GL.Clear(false,true,new Color(zero,zero,zero,zero)); RenderTexture.active=previous;
            return result;
        }
        private void DrawPair(RenderTexture color,RenderTexture data,int pass)
        {
            var p=World.Settings; int zero=p.I("math.zero"),one=p.I("math.one");
            _targets[zero]=color.colorBuffer; _targets[one]=data.colorBuffer;
            var previous=RenderTexture.active; bool srgb=GL.sRGBWrite;
            GL.sRGBWrite=false; Graphics.SetRenderTarget(_targets,color.depthBuffer);
            GL.PushMatrix(); GL.LoadOrtho();
            try
            {
                if(!_material.SetPass(pass)) throw new InvalidOperationException("Cloud shader pass unavailable: "+pass);
                GL.Begin(GL.QUADS);
                GL.TexCoord2(zero,zero);GL.Vertex3(zero,zero,zero);
                GL.TexCoord2(one,zero);GL.Vertex3(one,zero,zero);
                GL.TexCoord2(one,one);GL.Vertex3(one,one,zero);
                GL.TexCoord2(zero,one);GL.Vertex3(zero,one,zero);
                GL.End();
            }
            finally { GL.PopMatrix();GL.sRGBWrite=srgb;RenderTexture.active=previous; }
        }
        private void Configure(RenderTexture source)
        {
            var p=World.Settings;
            if(_previousSettings!=p)
            {
                foreach(var item in p.numbers) if(!string.IsNullOrEmpty(item.shader)) _material.SetFloat(item.shader,item.value);
                _material.SetFloat("_NoiseResolution",p.N("cloud.noise.baseResolution"));
                _material.SetFloat("_DetailResolution",p.N("cloud.noise.detailResolution"));
            }
            float scale=p.N(p.T("cloud.quality")=="low" ? "cloud.quality.lowScale" : p.T("cloud.quality")=="high" ? "cloud.quality.highScale" : "cloud.renderScale");
            int width=Mathf.Max(p.I("math.one"),Mathf.RoundToInt(source.width*scale));
            int height=Mathf.Max(p.I("math.one"),Mathf.RoundToInt(source.height*scale));
            if(_currentColor==null || _currentColor.width!=width || _currentColor.height!=height)
            {
                ReleaseClouds();
                _currentColor=Buffer(width,height,"Cloud current radiance",RenderTextureFormat.ARGBHalf);
                _currentData=Buffer(width,height,"Cloud current distance and wind",RenderTextureFormat.ARGBHalf);
                _historyColor=Buffer(width,height,"Cloud history radiance",RenderTextureFormat.ARGBHalf);
                _historyData=Buffer(width,height,"Cloud history distance",RenderTextureFormat.ARGBHalf);
                _resolvedColor=Buffer(width,height,"Cloud resolved radiance",RenderTextureFormat.ARGBHalf);
                _resolvedData=Buffer(width,height,"Cloud resolved distance",RenderTextureFormat.ARGBHalf);
                ResetReason="resolution";
            }
            _material.SetFloat("_CloudSteps",p.N(p.T("cloud.quality")=="low" ? "cloud.quality.lowSteps" : p.T("cloud.quality")=="high" ? "cloud.quality.highSteps" : "cloud.steps"));
            float z=p.N("math.zero"),o=p.N("math.one");
            _material.SetVector("_RayBottomLeft",_camera.ViewportPointToRay(new Vector3(z,z,z)).direction);
            _material.SetVector("_RayBottomRight",_camera.ViewportPointToRay(new Vector3(o,z,z)).direction);
            _material.SetVector("_RayTopLeft",_camera.ViewportPointToRay(new Vector3(z,o,z)).direction);
            _material.SetVector("_RayTopRight",_camera.ViewportPointToRay(new Vector3(o,o,z)).direction);
            _material.SetVector("_CameraForward",transform.forward);
            _material.SetTexture("_MainTex",source);
            _material.SetVector("_MainTex_TexelSize",new Vector4(o/source.width,o/source.height,source.width,source.height));
            World.Clouds.Bind(_material,transform.position,World);
        }
        private void OnRenderImage(RenderTexture source,RenderTexture destination)
        {
            if(World==null || _material==null || World.Settings.T("cloud.debug")=="passthrough")
            { _valid=false; ResetReason="passthrough";Graphics.Blit(source,destination);return; }
            var timer=Stopwatch.StartNew();
            try
            {
                Configure(source); var p=World.Settings; float zero=p.N("math.zero");
                double seconds=World.Clouds.Seconds;
                string reset=!_valid ? "initial" : _previousSettings!=p ? "settings" :
                    Vector3.Distance(_previousPosition,transform.position)>p.N("cloud.temporal.resetDistance") ? "camera-position" :
                    Quaternion.Angle(_previousRotation,transform.rotation)>p.N("cloud.temporal.resetAngle") ? "camera-rotation" :
                    Math.Abs(seconds-_previousSeconds)>p.N("cloud.temporal.resetSeconds") ? "time-jump" : null;
                bool temporal=p.N("cloud.temporal.enabled")>zero && p.T("cloud.debug")!="static";
                if(reset!=null) { _valid=false;ResetReason=reset; }
                _material.SetFloat("_SampleSpread",temporal && _valid && World.Playing ? p.N("cloud.sampling.spread") : zero);
                _material.SetFloat("_SamplePhase",temporal && _valid && World.Playing ? Time.frameCount%p.I("cloud.sampling.phaseCycle") : p.N("cloud.debug.frozenPhase"));
                _material.SetFloat("_HistoryValid",temporal && _valid ? p.N("math.one") : zero);
                _material.SetFloat("_HistoryWeight",temporal ? p.N("cloud.temporal.historyWeight") : zero);
                _material.SetFloat("_DeltaCloudSeconds",_valid ? (float)(seconds-_previousSeconds) : zero);
                _material.SetMatrix("_PreviousViewProjection",_previousProjection);
                DrawPair(_currentColor,_currentData,p.I("cloud.pass.trace"));
                _material.SetTexture("_CloudCurrent",_currentColor);_material.SetTexture("_CloudData",_currentData);
                _material.SetTexture("_HistoryTex",_historyColor);_material.SetTexture("_HistoryData",_historyData);
                DrawPair(_resolvedColor,_resolvedData,p.I("cloud.pass.resolve"));
                _material.SetTexture("_CloudResolved",_resolvedColor);_material.SetTexture("_ResolvedData",_resolvedData);
                Graphics.Blit(source,destination,_material,p.I("cloud.pass.composite"));
                var temporary=_historyColor;_historyColor=_resolvedColor;_resolvedColor=temporary;
                temporary=_historyData;_historyData=_resolvedData;_resolvedData=temporary;
                _previousProjection=_camera.projectionMatrix*_camera.worldToCameraMatrix;
                _previousPosition=transform.position;_previousRotation=transform.rotation;
                _previousSeconds=seconds;_previousSettings=p;
                AccumulatedFrames=_valid ? AccumulatedFrames+p.I("math.one") : p.I("math.one"); _valid=true;Error=null;
                UpdateShadow();
                FrameTimingManager.CaptureFrameTimings();
                if(FrameTimingManager.GetLatestTimings(checked((uint)p.I("math.one")),_frameTiming)>checked((uint)p.I("math.zero")))
                { GpuFrameMilliseconds=_frameTiming[p.I("math.zero")].gpuFrameTime;GpuFrameTimingAvailable=GpuFrameMilliseconds>p.N("math.zero"); }
            }
            catch(Exception ex)
            {
                if(Error!=ex.Message) UnityEngine.Debug.LogError("CLOUD_RENDER_ERROR "+ex);
                Error=ex.Message;_valid=false;ResetReason="error";Graphics.Blit(source,destination);
            }
            finally { CpuMilliseconds=(float)(timer.Elapsed.TotalSeconds*World.Settings.N("units.millisecondsPerSecond")); }
        }
        private void UpdateShadow()
        {
            var p=World.Settings; float zero=p.N("math.zero");
            if(p.N("cloud.shadow.enabled")<=zero || World.SunDirection.y<=zero)
            { World.Sun.cookie=null;return; }
            if(Time.unscaledTime<_shadowAt) return;
            _shadowAt=Time.unscaledTime+p.N("cloud.shadow.interval");
            int resolution=p.I("cloud.shadow.resolution");
            if(_shadow==null || _shadow.width!=resolution)
            { Release(ref _shadow);_shadow=Buffer(resolution,resolution,"Cloud directional light cookie",RenderTextureFormat.ARGB32); }
            var light=World.Sun; light.transform.position=transform.position;
            _material.SetVector("_ShadowCentre",light.transform.position);
            _material.SetVector("_ShadowRight",light.transform.right);_material.SetVector("_ShadowUp",light.transform.up);
            Graphics.Blit(null,_shadow,_material,p.I("cloud.pass.shadow"));
            light.cookieSize=p.N("cloud.shadow.size");light.cookie=_shadow;
        }
        private void Release(ref RenderTexture buffer)
        { if(buffer!=null) { buffer.Release();Destroy(buffer);buffer=null; } }
        private void ReleaseClouds()
        {
            _valid=false;
            Release(ref _currentColor);Release(ref _currentData);Release(ref _historyColor);Release(ref _historyData);Release(ref _resolvedColor);Release(ref _resolvedData);
        }
        private void OnDisable() { ReleaseClouds();if(World!=null && World.Sun!=null) World.Sun.cookie=null;Release(ref _shadow); }
        private void OnDestroy()
        {
            OnDisable();if(_camera!=null) _camera.depthTextureMode=_oldDepth;
            if(_material!=null) Destroy(_material);
        }
    }
    public sealed class CloudDiagnostics
    {
        public string cloud,shadow,resetReason;
        public int cloudWidth,cloudHeight;
        public bool historyValid,shadowActive;
        public float cloudTransmittanceMinimum,cloudTransmittanceMaximum,shadowMinimum,shadowMaximum;
    }
}
