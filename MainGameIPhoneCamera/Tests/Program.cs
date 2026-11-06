using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using UnityEngine;
using Valve.VR;
using VRGIN.Core;

namespace MainGameIPhoneCamera
{
    public sealed partial class Plugin
    {
        private static int checks;
        private static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
        public static void Main(string[] args)
        {
            Console.WriteLine("[iPhoneTests] Executing product VR handlers and screen mesh with boundary doubles.");
            CheckPlaybackBar();
            CheckCameraAxes();
            var p=new Plugin();
            Check(p.GetVrView()==VR.Camera.Head,"spawn uses the tracked headset rather than the VR origin");
            var grip=SteamVR_Actions.legacy_emulate.Grip_Press;
            var trigger=SteamVR_Actions.legacy_emulate.Axis1_Press;
            var left=SteamVR_Input_Sources.LeftHand;var right=SteamVR_Input_Sources.RightHand;
            p.ConnectVrInput();
            Check(grip.Count==4&&trigger.Count==4,"per-hand trigger press/release subscriptions");
            trigger.Down(left);Check(p.Photos==0,"unheld trigger ignored");
            p.CanAct=false;grip.Down(left);Check(!p.IsHeld,"denied session cannot grab");p.CanAct=true;
            VR.Mode.Left.transform.position=new Vector3(1,0,0);
            grip.Down(left);Check(!p.IsHeld,"distant hand cannot grab");VR.Mode.Left.transform.position=default;
            VR.Mode.Left.Available=false;grip.Down(left);Check(!p.IsHeld,"foreign focus is respected");VR.Mode.Left.Available=true;
            grip.Down(left);Check(p.IsHeld&&p._root.transform.parent==VR.Mode.Left.transform,"left grip parents phone");
            var firstLock=VR.Mode.Left.LastLock;
            grip.Down(left);Check(VR.Mode.Left.LastLock!=firstLock&&!firstLock.IsValid&&!VR.Mode.Left.LastLock.IsInvalidating,"fresh grip recovers a missed release with a fresh native lock");firstLock=VR.Mode.Left.LastLock;
            trigger.Down(right);Check(p.Photos==0,"other hand cannot take photo");
            p._phone.Show(PhonePage.Viewer, DateTime.UtcNow);
            trigger.Down(right);Check(p.ViewerPan && p.Photos==0,"right trigger pans even when phone is held in left hand");
            trigger.Up(right);Check(!p.ViewerPan,"right trigger release ends pan");
            trigger.Down(left);Check(p.ViewerPan && p.Photos==0,"holding-hand trigger enters pan without shooting");
            trigger.Down(right); trigger.Up(left); Check(p.ViewerPan,"one trigger release does not cancel the other held trigger");
            trigger.Up(right); Check(!p.ViewerPan,"both trigger releases end pan");
            p._phone.Show(PhonePage.Camera, DateTime.UtcNow);
            trigger.Down(left);Check(p.Photos==1&&VR.Mode.Left.Input.Pulses==1,"held trigger captures and confirms");
            grip.Up(right);Check(p.IsHeld,"other grip release ignored");
            grip.Up(left);Check(!p.IsHeld&&firstLock.Releases==1&&p._root.transform.parent==p._scene.transform,"release detaches and releases focus");
            Check(VR.Mode.Left.Tracking.onConnectedChangedEvent==null&&VR.Mode.Left.Tracking.onTrackingChangedEvent==null,"pose listeners removed");
            p.ReleasePhone();Check(firstLock.Releases==1,"double cleanup safe");
            grip.Down(right);trigger.Down(right);Check(p.Photos==2,"right hand also captures");
            VR.Mode.Right.Tracking.Disconnect();Check(!p.IsHeld,"disconnect releases phone");
            grip.Down(left);VR.Mode.Left.Tracking.LoseTracking();Check(!p.IsHeld,"tracking loss releases phone");
            grip.Down(left);VR.Mode.Left.LastLock.IsValid=false;trigger.Down(left);Check(!p.IsHeld&&p.Photos==2,"revoked focus cannot shoot");
            grip.Down(left);p.CanAct=false;trigger.Down(left);Check(p.Photos==2,"revoked permit cannot shoot");
            p.DisconnectVrInput();Check(!p.IsHeld&&grip.Count==0&&trigger.Count==0,"stop removes listeners and hold");
            p.CanAct=true;grip.Down(left);trigger.Down(left);Check(!p.IsHeld&&p.Photos==2,"old edges inactive after stop");
            p.DisconnectVrInput();p.ConnectVrInput();Check(grip.Count==4&&trigger.Count==4,"next session subscribes once");p.DisconnectVrInput();
            foreach(int height in new[]{-100,480,481,1920,2436,99999})
            {var size=PhotoSize.FromHeight(height);Check(size.Width<size.Height&&size.Width%2==0&&size.Height%2==0&&size.Height>=480&&size.Height<=3840,"portrait dimensions "+height);}
            var defaultSize=PhotoSize.FromHeight(1920);Check(defaultSize.Width==886&&defaultSize.Height==1920,"default portrait PNG size");
            if (args.Length == 1 && args[0] == "--interaction-only")
            {
                Console.WriteLine("PASS " + checks + " assertions (product bar/VR handlers; boundary doubles, no game/VR execution; mesh tests excluded).");
                return;
            }
            Check(Directory.Exists(Path.GetDirectoryName(args[0])),"installed mesh fixture directory");
            var data=JsonDocument.Parse(File.ReadAllText(args[0])).RootElement;
            Vector3 V3(JsonElement v)=>new Vector3(v[0].GetSingle(),v[1].GetSingle(),v[2].GetSingle());
            var source=new Mesh{vertices=data.GetProperty("vertices").EnumerateArray().Select(V3).ToArray(),
                normals=data.GetProperty("normals").EnumerateArray().Select(V3).ToArray(),
                uv=data.GetProperty("uv").EnumerateArray().Select(v=>new Vector2(v[0].GetSingle(),v[1].GetSingle())).ToArray(),
                triangles=data.GetProperty("triangles").EnumerateArray().Select(v=>v.GetInt32()).ToArray()};
            var screen=PhoneScreen.Build(source);
            var bezel=PhoneScreen.Build(source,true);
            Check(bezel.triangles.Length>0&&bezel.vertices.All(v=>v.y<-.00345f),"old lock screen masked on the real glass only");
            Check(screen.triangles.Length>0,"screen generated from installed mesh");
            Check(screen.vertices.All(v=>v.y<-.00345f),"display in front of measured glass");
            Check(screen.vertices.All(v=>Math.Abs(v.x)>=.014f-1e-6f||v.z<=.0515f+1e-6f),"flat notch center retained");
            Check(screen.vertices.Any(v=>Math.Abs(v.x)>.0141f&&Math.Abs(v.x)<.0169f&&v.z>.0516f),"rounded notch corners expose the live screen");
            Check(screen.vertices.All(v=>Math.Abs(v.x)>=.017f-1e-6f||v.z<=.0545f+1e-6f),"rounded notch side tangent retained");
            Check(screen.uv.All(v=>v.x>=0&&v.x<=1&&v.y>=0&&v.y<=1),"screen UV range");
            for(int i=0;i<screen.triangles.Length;i+=3)
            {var a=screen.vertices[screen.triangles[i]];var b=screen.vertices[screen.triangles[i+1]];var c=screen.vertices[screen.triangles[i+2]];
                float cross=(b.z-a.z)*(c.x-a.x)-(b.x-a.x)*(c.z-a.z);Check(cross<=1e-8f,"front triangle winding");}
            Console.WriteLine("PASS "+checks+" assertions (boundary tests; no game/VR execution).");
        }
    }
}
