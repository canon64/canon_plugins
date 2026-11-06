using System;
using MainGameVrDildo;
using UnityEngine;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);
    }
    public struct Quaternion {public float marker;}
    public class Transform
    {
        public Vector3 position,forward=new(0,0,1);public Quaternion rotation;public Transform parent;
        public void SetPositionAndRotation(Vector3 p,Quaternion r){position=p;rotation=r;}
    }
    public class GameObject {public Transform transform=new();}
    public class Camera {public static Camera main=new();public Transform transform=new();}
    public static class Application {public static bool isFocused=true;}
}
namespace VRGIN.Core {public static class VR {public static bool Active;public static CameraState Camera;}public class CameraState {public Transform Head=new();}}
namespace CanonPlugins.Shared {public static class PluginSessionGate {public static bool Allowed=true;}}
namespace MainGameVrDildo
{
    public sealed partial class Plugin
    {
        internal class Settings {public bool Enabled=true;public float SpawnDistance=.35f;}
        private Settings _settings=new();
        public GameObject _model;
        public object _grabController;
        public int Spawns,Stops,Releases,Resets,DetachParents;
        public bool Running,GizmoAvailable=true;
        private bool _autoSpawnAttempted;
        private Gizmo _desktopGizmo;
        public bool Shown=>_desktopGizmo!=null&&_desktopGizmo.Visible;
        public bool Requested=>_barGizmoRequested;
        private sealed class Gizmo {public bool Visible;public void SetVisible(bool value){Visible=value;}}
        private void Spawn(){Spawns++;_model=new();}
        private void DetachDesktopGizmo(){_desktopGizmo=null;}
        private void ReleaseGrab(){Releases++;_grabController=null;}
        private void StopPiston(){Stops++;Running=false;}
        private void TrySetComposerParent(object value,bool keep){DetachParents++;_model.transform.parent=null;}
        private void ResetAibuMotion(){Resets++;}
        private void UpdateDesktopGizmo(){if(_grabController!=null){DetachDesktopGizmo();return;}if(GizmoAvailable)_desktopGizmo=new();}
        private void DeleteModel(){_model=null;_barControlsGizmo=false;_barGizmoRequested=false;DetachDesktopGizmo();}
    }
}
static class Program
{
    private static int checks;
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
    static void Main()
    {
        Camera.main.transform.position=new(10,20,30);
        var p=new Plugin();p.PlaybackBarPrimaryAction();
        Check(p.Spawns==1&&!p.Shown,"first button press spawns without gizmo");
        Check(Math.Abs(p._model.transform.position.z-30.35f)<.0001f,"first spawn is in front of camera");
        p._model.transform.position=new(1,2,3);p._model.transform.rotation=new(){marker=17};
        var parent=new Transform();p._model.transform.parent=parent;p.Running=true;
        int stops=p.Stops,releases=p.Releases,resets=p.Resets,parents=p.DetachParents;
        for(int i=0;i<30;i++)
        {
            p.PlaybackBarPrimaryAction();
            Check(p.Shown==(i%2==0),"show and hide alternate after first spawn");
            Check(p._model.transform.position.x==1&&p._model.transform.position.y==2&&p._model.transform.position.z==3&&p._model.transform.rotation.marker==17,"toggle preserves item pose");
            Check(p._model.transform.parent==parent&&p.Running,"toggle preserves parent and piston");
        }
        Check(p.Spawns==1&&p.Stops==stops&&p.Releases==releases&&p.Resets==resets&&p.DetachParents==parents,"toggles never invoke recall side effects");
        p._grabController=new object();var grip=p._grabController;p.PlaybackBarPrimaryAction();
        Check(p._grabController==grip&&p.Running&&!p.Shown,"held item keeps its grip and piston while gizmo is suppressed");
        p.PlaybackBarPrimaryAction();Check(!p.Requested&&p._grabController==grip,"held hide toggle only changes gizmo request");
        p._grabController=null;p.GizmoAvailable=false;
        try{p.PlaybackBarPrimaryAction();}catch(InvalidOperationException){}
        Check(!p.Requested&&p._model.transform.position.x==1&&p.Running,"gizmo failure cannot recall or stop item");
        CanonPlugins.Shared.PluginSessionGate.Allowed=false;p.PlaybackBarPrimaryAction();Check(!p.Requested,"denied gate rejects action");
        CanonPlugins.Shared.PluginSessionGate.Allowed=true;p.PlaybackBarDelete();p.GizmoAvailable=true;p.PlaybackBarPrimaryAction();
        Check(p.Spawns==2&&!p.Shown,"deleting resets next action to spawn");
        Console.WriteLine("PASS "+checks+" production playback button checks; native spawning/gizmo boundaries are doubles");
    }
}
