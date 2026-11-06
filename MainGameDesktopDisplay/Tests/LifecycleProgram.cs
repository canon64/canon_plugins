using System;
using CanonGate;
using MainGameTransformGizmo;
using UnityEngine;
namespace MainGameDesktopDisplay
{
    public sealed partial class Plugin
    {
        static int checks;
        static void Check(bool ok,string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); checks++; }
        public static int Main()
        {
            try
            {
                var p=new Plugin(); p.Awake();
                p.PlaybackBarPrimaryAction(); p.PlaybackBarDelete();
                Check(p._root==null && DesktopCapture.Captures==0,"no capture at startup or denied call");
                GateApi.Current=new() { Kind=GateApi.SessionKind.Maker,Id=1 }; GateApi.Allowed=true; GateApi.Generation=1;
                p.StartSession(); p.PlaybackBarPrimaryAction();
                Check(!p._active && p._root==null,"Maker cannot start H-only display");
                GateApi.Current.Kind=GateApi.SessionKind.H; p.StartSession();
                Check(p._active && p._root==null,"allowed H stays hidden until explicit call");
                Shader.Available=false; p.PlaybackBarPrimaryAction();
                Check(p._root==null && !p._cycle.NextShowsGizmo && DesktopCapture.Live==0,"partial creation failure cleans up without advancing cycle");
                Shader.Available=true; p.PlaybackBarPrimaryAction();
                var root=p._root; var texture=p._texture;
                Check(root!=null && DesktopCapture.Live==1 && texture.Uploads==1 && p._cycle.NextShowsGizmo,"explicit call creates screen and uploads first desktop frame");
                p.StartSession(); Check(p._root==root,"duplicate start preserves current display");
                p.PaintDesktop(); int captures=DesktopCapture.Captures;
                p.PaintDesktop(); Check(DesktopCapture.Captures==captures,"multiple eye/camera renders respect FPS cap");
                Time.realtimeSinceStartup=1; p.PaintDesktop(); Check(DesktopCapture.Captures==captures+1,"next due render produces desktop pixels");
                TransformGizmoApi.Capture=false; p.PlaybackBarPrimaryAction();
                Check(!p._gizmo.enabled && !p._gizmo.IsVisible && p._cycle.NextShowsGizmo,"failed gizmo capture keeps cycle and hides handle");
                TransformGizmoApi.Capture=true; p.PlaybackBarPrimaryAction();
                Check(p._gizmo.IsVisible && !p._cycle.NextShowsGizmo,"second action shows common gizmo");
                captures=DesktopCapture.Captures;
                p._root.transform.position=new Vector3(3,4,5);
                p.PlaybackBarPrimaryAction(); Check(p._root==root && !p._gizmo.IsVisible && p._cycle.NextShowsGizmo && p._root.transform.position.x==3 && DesktopCapture.Captures==captures,"third action hides without moving display or restarting capture");
                p.PlaybackBarPrimaryAction(); Check(p._gizmo.IsVisible && p._root.transform.position.x==3,"fourth action shows without moving display");
                p.OnApplicationFocus(false); Check(!p._gizmo.enabled,"focus loss releases gizmo capture");
                p.OnApplicationPause(true); Time.realtimeSinceStartup=2; captures=DesktopCapture.Captures; p.PaintDesktop();
                Check(p._paused && !p._driver.enabled && DesktopCapture.Captures==captures,"application pause stops pixel production");
                p._paused=false; p._driver.enabled=true;
                GateApi.Allowed=false; p.PaintDesktop(); p.PlaybackBarPrimaryAction();
                Check(DesktopCapture.Captures==captures && p._root==root,"revoked permit rejects both render and public action");
                p.StopSession(); p.StopSession(); p.PlaybackBarDelete();
                Check(p._root==null && p._texture==null && p._gizmo==null && DesktopCapture.Live==0,"repeated stop/delete after revocation releases every owned resource");
                GateApi.Generation=2; GateApi.Allowed=true; GateApi.Current.Id=2;
                p.PaintDesktop(); p.PlaybackBarPrimaryAction(); Check(p._root==null,"old local session cannot run under next generation");
                p.StartSession(); DesktopCapture.Fail=true; p.PlaybackBarPrimaryAction();
                Check(p._paused && p._capture==null && DesktopCapture.Live==0 && !p._driver.enabled && !p._cycle.NextShowsGizmo,"first capture failure stops producer and keeps failure status");
                DesktopCapture.Fail=false; p.StartCapture();
                Check(p._capture!=null && p._driver.enabled,"explicit retry recovers capture");
                DesktopCapture.Fail=true; Time.realtimeSinceStartup=5; p.PaintDesktop();
                Check(p._capture==null && p._paused && !p._driver.enabled && DesktopCapture.Live==0,"render capture failure releases native resources and stops retries");
                DesktopCapture.Fail=false; p.StartCapture(); p._enabled.Value=false;
                Check(!p._active && p._root==null && DesktopCapture.Live==0 && !p._enabled.Value,"config disable stops without rewriting enabled setting");
                p._enabled.Value=true; p.PlaybackBarPrimaryAction(); Check(p._root==null,"reenabling waits for next H");
                p.OnDisable(); p.OnDestroy();
                Check(DesktopCapture.Live==0 && GateApi.Start==null,"disable/destroy dispose registration and resources");
                CheckVr();
                CheckMouse();
                CheckClip();
                CheckPosition();
                CheckMonitorPresets();
                CheckHotelRestore();
                Console.WriteLine("Lifecycle boundary checks passed: " + checks + ". These doubles do not verify Unity rendering."); return 0;
            }
            catch(Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}
