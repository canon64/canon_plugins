"""Verify production map calibration/lazy generation against full-map engine stubs."""
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / '_diagnostics/perf-reduction-20261027/map-sampling'
STUBS = r'''
using System;
namespace UnityEngine {
 public class Object {public static int Destroyed;public static void Destroy(Object o){if(o!=null)Destroyed++;}}
 public enum TextureFormat{ARGB32}public enum TextureWrapMode{Clamp}public enum FilterMode{Bilinear}
 public struct Color32 {public byte r,g,b,a;public Color32(byte x,byte y,byte z,byte w){r=x;g=y;b=z;a=w;}}
 public struct Color {public float r,g,b,a;public Color(float x,float y,float z,float w){r=x;g=y;b=z;a=w;}}
 public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}public static Vector2 zero=>default;}
 public class Texture2D:Object {
  public static int NativeMode,DimensionReads;int _width,_height;public int width{get{DimensionReads++;return _width;}}public int height{get{DimensionReads++;return _height;}}
  public TextureWrapMode wrapMode;public FilterMode filterMode;Color32[] pixels;
  public Texture2D(int w,int h,TextureFormat f,bool m){_width=w;_height=h;pixels=new Color32[w*h];}
  public void SetPixels32(Color32[] p){pixels=(Color32[])p.Clone();}
  public Color GetPixelBilinear(float u,float v){
   int width=_width,height=_height;
   if(NativeMode==5)throw new InvalidOperationException("native error");
   if(NativeMode==4)return new Color(float.NaN,0,0,1);
   u=Math.Clamp(u,0,1);v=Math.Clamp(v,0,1);
   float x=NativeMode==1?u*(width-1):u*width-(NativeMode==0?.5f:0);
   float y=NativeMode==1?v*(height-1):v*height-(NativeMode==0?.5f:0);
   int ix=(int)MathF.Floor(x),iy=(int)MathF.Floor(y);float fx=x-ix,fy=y-iy;
   var a=pixels[Math.Clamp(iy,0,height-1)*width+Math.Clamp(ix,0,width-1)];
   var b=pixels[Math.Clamp(iy,0,height-1)*width+Math.Clamp(ix+1,0,width-1)];
   var c=pixels[Math.Clamp(iy+1,0,height-1)*width+Math.Clamp(ix,0,width-1)];
   var d=pixels[Math.Clamp(iy+1,0,height-1)*width+Math.Clamp(ix+1,0,width-1)];
   float R(byte p,byte q,byte r,byte s){float p0=p/255f,q0=q/255f,r0=r/255f,s0=s/255f;return (p0*(1-fx)+q0*fx)*(1-fy)+(r0*(1-fx)+s0*fx)*fy;}
   float red=R(a.r,b.r,c.r,d.r),green=R(a.g,b.g,c.g,d.g);
   if(NativeMode==3){red*=red;green*=green;}
   return new Color(red,green,0,1);
  }
 }
 public static class Time {public static float time;}
 public static class Mathf {
  public static float Clamp01(float f)=>Math.Clamp(f,0,1);public static int Clamp(int x,int a,int b)=>Math.Clamp(x,a,b);
  public static float Clamp(float x,float a,float b)=>Math.Clamp(x,a,b);public static int FloorToInt(float f)=>(int)MathF.Floor(f);
  public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);public static float Max(float a,float b)=>Math.Max(a,b);
  public static float Abs(float a)=>Math.Abs(a);public static float Sqrt(float a)=>MathF.Sqrt(a);public static float Exp(float a)=>MathF.Exp(a);
  public static float Atan2(float a,float b)=>MathF.Atan2(a,b);public static float Cos(float a)=>MathF.Cos(a);public static float Sin(float a)=>MathF.Sin(a);
  // This stand-in exercises lazy/full equivalence; it is not Unity's Perlin implementation.
  public static float PerlinNoise(float x,float y)=>(MathF.Sin(x*1.7f+y*.9f)+1)*.5f;
 }
}
namespace MainGameDisplacementMap {
 class Entry<T>{public T Value;public Entry(T v){Value=v;}}
 class Log {public void Write(string a,string b){}}
 public sealed partial class Plugin:UnityEngine.Object {
  Entry<int> _cfgMapResolution=new Entry<int>(192),_cfgMapWaveCount=new Entry<int>(3);
  Entry<float> _cfgTimeScale=new Entry<float>(1),_cfgMapFrequency=new Entry<float>(6.5f),_cfgMapPropagationSpeed=new Entry<float>(1.6f),
   _cfgMapDamping=new Entry<float>(1.4f),_cfgMapNoise=new Entry<float>(.15f),_cfgMapScrollX=new Entry<float>(0),_cfgMapScrollY=new Entry<float>(.2f);
  Entry<bool> _cfgVerboseLog=new Entry<bool>(false);Log _fileLog;
  public int TestGenerated;
  internal void Configure(int res,int waves,float noise){_cfgMapResolution.Value=res;_cfgMapWaveCount.Value=waves;_cfgMapNoise.Value=noise;EnsureDisplacementMap();}
  internal void Generate(float t,bool forceNative=false){if(forceNative){_useManagedMapSampling=false;_managedMapSamplingChecked=true;}RegenerateDisplacementMap(t);}
  internal UnityEngine.Vector2 Sample(float u,float v,float sx,float sy)=>SampleDisplacementMap(new UnityEngine.Vector2(u,v),sx,sy);
  internal bool Managed=>_useManagedMapSampling;internal int Mode=>_mapSamplingMode;
  internal void Release()=>ReleaseDisplacementMap();
 }
}
'''
TESTS = r'''
using System;using MainGameDisplacementMap;using UnityEngine;using Object=UnityEngine.Object;
class Program {
 static int checks;static float worst;
 static void Check(bool x,string s){checks++;if(!x)throw new Exception(s);}
 static void Near(Vector2 a,Vector2 b){float e=Math.Max(Math.Abs(a.x-b.x),Math.Abs(a.y-b.y));worst=Math.Max(worst,e);Check(e<=MapSampling.Tolerance,"lazy/native displacement mismatch "+e);}
 static void Main(){
  int generated=0,full=0;
  for(int mode=0;mode<3;mode++){
   Texture2D.NativeMode=mode;int destroyed=Object.Destroyed;
   Check(MapSampling.TryCalibrate(out int selected,out float error)&&selected==mode&&error<=MapSampling.Tolerance,"runtime coordinate convention selected");
   Check(Object.Destroyed==destroyed+1,"calibration texture released");
   foreach(int resolution in new[]{32,192,512})foreach(int waves in new[]{1,3,8})foreach(float noise in new[]{0f,.15f}){
    var a=new Plugin();var b=new Plugin();a.Configure(resolution,waves,noise);b.Configure(resolution,waves,noise);
    a.Generate(0);b.Generate(0,true);Check(a.Managed&&a.Mode==mode,"actual map validates after calibration");
    foreach(float time in new[]{.1f,1.7f,30f}){
     a.TestGenerated=0;b.TestGenerated=0;a.Generate(time);b.Generate(time,true);
     for(int y=0;y<=36;y++)for(int x=0;x<=64;x++){
      float u=x/64f,v=y/36f,sx=time*.017f,sy=time*.03f;
      int reads=Texture2D.DimensionReads;var actual=a.Sample(u,v,sx,sy);
      Check(Texture2D.DimensionReads==reads,"managed vertex sampling does not read engine texture dimensions");
      Near(actual,b.Sample(u,v,sx,sy));
     }
     Check(a.TestGenerated<=b.TestGenerated,"lazy generation never exceeds full map");
     if(mode==1&&resolution==192&&waves==3&&noise==.15f&&time==.1f){generated=a.TestGenerated;full=b.TestGenerated;}
    }
    for(int i=0;i<100;i++)a.Sample(.5f,.5f,0,0);
    long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++)a.Sample(.5f,.5f,0,0);
    Check(GC.GetAllocatedBytesForCurrentThread()==before,"steady map reads do not allocate");
    a.Configure(64,3,.15f);b.Configure(64,3,.15f);a.Generate(2);b.Generate(2,true);
    Check(a.Managed,"resolution replacement recalibrates and updates cached dimensions");
    Near(a.Sample(.47f,.79f,0,.1f),b.Sample(.47f,.79f,0,.1f));
    a.Release();a.Configure(resolution,3,.15f);a.Generate(0);Check(a.Managed,"release/recreate calibrates again");b.Release();a.Release();
   }
  }
  for(int mode=3;mode<=5;mode++){
   Texture2D.NativeMode=mode;int destroyed=Object.Destroyed;
   Check(!MapSampling.TryCalibrate(out int selected,out float error)&&selected==-1,"unknown/non-finite/throwing native sampler rejected");
   Check(Object.Destroyed==destroyed+1,"failed calibration releases texture");
   if(mode==3){var a=new Plugin();a.Configure(32,3,.15f);a.Generate(0);Check(!a.Managed,"unknown native convention retains native fallback");a.Release();}
  }
  Console.WriteLine($"MAP_SAMPLING_OK checks={checks}; max displacement error={worst:R}; representative regenerated pixels {full}->{generated}; 3 coordinate conventions + fallback; engine stubs, not live Unity");
 }
}
'''


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    source = (ROOT / 'MainGameDisplacementMap/Plugin.Displacement.cs').read_text(encoding='utf-8-sig')
    source = source.replace('int pixelIndex = y * _dispMapWidth + x;', 'TestGenerated++; int pixelIndex = y * _dispMapWidth + x;')
    for name, value in {'Plugin.Displacement.cs': source, 'Stubs.cs': STUBS, 'Program.cs': TESTS,
                        'MapSampling.cs': (ROOT / 'MainGameDisplacementMap/MapSampling.cs').read_text(encoding='utf-8-sig')}.items():
        (OUT / name).write_text(value, encoding='utf-8')
    (OUT / 'Checks.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><NoWarn>CS0649</NoWarn></PropertyGroup></Project>', encoding='utf-8')
    subprocess.run(['dotnet', 'run', '--project', str(OUT / 'Checks.csproj'), '-c', 'Release'], cwd=ROOT, check=True)


if __name__ == '__main__':
    main()
