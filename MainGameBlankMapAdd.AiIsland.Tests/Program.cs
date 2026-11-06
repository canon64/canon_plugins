// Boundary tests for production camera ownership, translation scaling and restoration; no Unity runtime simulation.
using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 zero => new Vector3(0,0,0);
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);
    }
    public enum CameraClearFlags { Skybox, Color }
    [Flags] public enum DepthTextureMode { None=0, Depth=1, DepthNormals=2 }
    public class Camera { public float farClipPlane; public CameraClearFlags clearFlags; public DepthTextureMode depthTextureMode; public bool useOcclusionCulling = true; }
    public static class Mathf { public static float Clamp(float x,float min,float max)=>Math.Clamp(x,min,max); }
    public static class QualitySettings { public static float shadowDistance=150; }
    public enum KeyCode { LeftArrow,RightArrow,UpArrow,DownArrow,PageUp,PageDown,R,Keypad5 }
    public static class Input { public static HashSet<KeyCode> Held=new HashSet<KeyCode>(); public static bool GetKey(KeyCode code)=>Held.Contains(code); public static bool GetMouseButton(int button)=>false;public static float GetAxis(string name)=>0; }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Method)] public class HarmonyPrefix:Attribute{}
    [AttributeUsage(AttributeTargets.Method)] public class HarmonyPostfix:Attribute{}
    [AttributeUsage(AttributeTargets.Method)] public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string name){}}
}
public class BaseCameraControl_Ver2
{
    public struct CameraData { public UnityEngine.Vector3 Pos; }
    public bool isLimitPos,isLimitDir,isLlimitRot;
    public float moveSpeed;
    public UnityEngine.Camera thisCmaera;
    private CameraData data=new CameraData();
    public CameraData GetCameraData()=>data;
    public void SetCameraData(CameraData value){data=value;}
}
public class CameraControl_Ver2:BaseCameraControl_Ver2{}
namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private static Plugin _aiIslandOwner;
        private bool _aiIslandActive;
        private AiIslandCameraScope _aiIslandCamera;
        private class Setting { public float Value; }
        private Setting _aiMoveMultiplier=new Setting{Value=10}, _aiFarClip=new Setting{Value=3000};
        private Setting _aiShadowDistance=new Setting{Value=40};
        private static int checks;
        private static void ApplySettings(CameraControl_Ver2 camera)
        {
            var owner=_aiIslandOwner;
            if(owner._aiIslandActive && owner._aiIslandCamera.Owns(camera))
                owner._aiIslandCamera.Apply(owner._aiMoveMultiplier.Value,owner._aiFarClip.Value,owner._aiShadowDistance.Value);
        }
        private static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
        public static int Main()
        {
            ManifestTests.Run();
            var camera=new CameraControl_Ver2{isLimitPos=true,isLimitDir=true,isLlimitRot=true,moveSpeed=.05f,
                thisCmaera=new UnityEngine.Camera{farClipPlane=350,clearFlags=UnityEngine.CameraClearFlags.Color,depthTextureMode=UnityEngine.DepthTextureMode.DepthNormals}};
            var owner=new Plugin{_aiIslandActive=true};
            _aiIslandOwner=owner;
            owner._aiIslandCamera=new AiIslandCameraScope(camera);
            ApplySettings(camera);
            Check(!camera.isLimitPos&&!camera.isLimitDir&&!camera.isLlimitRot,"all free-camera limits disabled");
            Check(!camera.thisCmaera.useOcclusionCulling,"invalid source-scene baked occlusion disabled");
            Check(UnityEngine.QualitySettings.shadowDistance==40,"island shadow draw distance applied");
            Check(camera.moveSpeed==.5f&&camera.thisCmaera.farClipPlane==3000,"speed multiplier and draw distance applied");
            ApplySettings(camera);
            Check(camera.moveSpeed==.5f,"repeated updates do not compound speed");
            Check(camera.thisCmaera.depthTextureMode==(UnityEngine.DepthTextureMode.Depth|UnityEngine.DepthTextureMode.DepthNormals),"original depth mode preserved for water");
            var other=new CameraControl_Ver2{isLimitPos=true,moveSpeed=.07f};
            ApplySettings(other);
            Check(other.isLimitPos&&other.moveSpeed==.07f,"other cameras untouched");
            owner._aiMoveMultiplier.Value=1;
            ApplySettings(camera);
            Check(camera.moveSpeed==.05f,"normal multiplier retains original mouse camera speed");
            UnityEngine.Input.Held.Add(UnityEngine.KeyCode.UpArrow);
            AiIslandKeyPrefix(camera,out var normalState);
            var normalMove=camera.GetCameraData();normalMove.Pos=new UnityEngine.Vector3(0,0,.1f);camera.SetCameraData(normalMove);
            AiIslandKeyPostfix(camera,normalState);
            Check(Math.Abs(camera.GetCameraData().Pos.z-.1f)<.0001f,"normal multiplier retains original keyboard movement");
            owner._aiMoveMultiplier.Value=10;ApplySettings(camera);
            UnityEngine.Input.Held.Add(UnityEngine.KeyCode.UpArrow);
            var initial=camera.GetCameraData();initial.Pos=new UnityEngine.Vector3(2,4,6);camera.SetCameraData(initial);
            AiIslandKeyPrefix(camera,out var state);
            var moved=camera.GetCameraData();moved.Pos+=new UnityEngine.Vector3(0,0,.1f);camera.SetCameraData(moved);
            AiIslandKeyPostfix(camera,state);
            Check(Math.Abs(camera.GetCameraData().Pos.z-7)<.0001f,"keyboard translation scales original controller delta");
            AiIslandKeyPrefix(camera,out state); // Original key handler did not move, e.g. blocked by UI.
            AiIslandKeyPostfix(camera,state);
            Check(Math.Abs(camera.GetCameraData().Pos.z-7)<.0001f,"UI-suppressed input stays stationary");
            UnityEngine.Input.Held.Add(UnityEngine.KeyCode.R);
            AiIslandKeyPrefix(camera,out state);
            Check(!state.Active,"reset key is not multiplied");
            owner._aiIslandCamera.Dispose();
            Check(camera.isLimitPos&&camera.isLimitDir&&camera.isLlimitRot&&camera.moveSpeed==.05f,"original movement settings restored");
            Check(camera.GetCameraData().Pos.x==0&&camera.GetCameraData().Pos.y==0&&camera.GetCameraData().Pos.z==0,"original camera pose restored after travelling across the island");
            Check(camera.thisCmaera.farClipPlane==350&&camera.thisCmaera.clearFlags==UnityEngine.CameraClearFlags.Color&&
                camera.thisCmaera.depthTextureMode==UnityEngine.DepthTextureMode.DepthNormals,"original rendering settings restored");
            Check(camera.thisCmaera.useOcclusionCulling,"original occlusion setting restored");
            Check(UnityEngine.QualitySettings.shadowDistance==150,"original shadow draw distance restored");
            owner._aiIslandCamera.Dispose();
            Check(camera.moveSpeed==.05f,"restoration is idempotent");
            owner._aiIslandActive=false;
            ApplySettings(camera);
            Check(camera.isLimitPos&&camera.moveSpeed==.05f,"inactive island does not override restored camera");
            Console.WriteLine("TOTAL "+checks+" PASSED");
            return 0;
        }
    }
}
