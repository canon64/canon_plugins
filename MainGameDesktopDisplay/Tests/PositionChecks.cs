using System;
using UnityEngine;
using VRGIN.Core;
namespace MainGameDesktopDisplay
{
    public sealed partial class Plugin
    {
        static void CheckPosition()
        {
            var p=VrSession();
            p._spawnOrigin=new Vector3(10,2,3);p._spawnRotation=new Quaternion{Yaw=90};
            p._spawn[0]=.2f;p._spawn[1]=-.1f;p._spawn[2]=.6f;p._spawnDirty=true;
            int saved=p._spawnX.Writes+p._spawnY.Writes+p._distance.Writes;
            p.ApplySpawnPosition();var position=p._root.transform.position;
            Check(Math.Abs(position.x-10.6f)<1e-5&&Math.Abs(position.y-1.9f)<1e-5&&Math.Abs(position.z-2.8f)<1e-5,"spawn offsets follow the captured view rotation and position");
            Check(saved==p._spawnX.Writes+p._spawnY.Writes+p._distance.Writes,"spawn preview does not save during drag");
            Event.current.type=EventType.MouseUp;Event.current.rawType=EventType.MouseUp;p.DrawSpawnPosition();
            Check(!p._spawnDirty&&p._spawnX.Value==.2f&&p._spawnY.Value==-.1f&&p._distance.Value==.6f,"spawn settings commit on release");
            saved=p._spawnX.Writes+p._spawnY.Writes+p._distance.Writes;p.DrawSpawnPosition();
            Check(saved==p._spawnX.Writes+p._spawnY.Writes+p._distance.Writes,"unchanged spawn settings are not saved repeatedly");
            p.InitializeSpawnPosition();Check(p._spawn[0]==.2f&&p._spawn[1]==-.1f&&p._spawn[2]==.6f,"spawn position restores from the existing config store");
            p._spawn[0]=-25;p._spawnCentre[0]=-25;p._spawnDirty=true;GUI.Focus="DesktopSpawnX";Event.current.type=EventType.KeyDown;Event.current.rawType=EventType.KeyDown;Event.current.keyCode=KeyCode.Return;p.DrawSpawnPosition();
            Check(p._spawnX.Value==-25&&Event.current.type==EventType.Used,"numeric spawn coordinates beyond the slider's initial range commit on Enter");
            var root=p._root;position=root.transform.position;p.StopSession();p.ApplySpawnPosition();
            Check((root.transform.position-position).sqrMagnitude==0,"inactive spawn preview cannot move the former display");
            GUI.Focus="";p.OnDestroy();VR.Active=false;
        }
    }
}
