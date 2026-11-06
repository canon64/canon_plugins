using System;
using System.Globalization;
using System.Linq;
using MainGameHLocationCoordinates;
using UnityEngine;

namespace MainGameHLocationPlacement
{
    internal static class CoordinateDiagnosticTests
    {
        internal static void Run()
        {
            var plugin=Plugin.Instance;
            plugin.DiagnosticMessages.Clear();plugin.Diagnostics=false;
            try
            {
                using(var e=new TestEnvironment("coordinate-trace"))
                {
                    int collected=0;e.Session.Trace("OFF",()=>{collected++;return "unused";});
                    Program.Assert(collected==0&&plugin.DiagnosticMessages.Count==0,"OFF skips trace collection and output");
                    var a=Program.Pose(2,0,-64,30);var b=Program.Pose(10,1,20,120);var p=Program.Pose(2,.3f,-63.8f,45);
                    e.Bridge.Female.SetPositionAndRotation(a.position,a.rotation);
                    foreach(var entry in e.Hip.NextPreset.entries){entry.proxyPosition=p.position;entry.proxyRotation=p.rotation;}
                    e.Hip.SaveCurrentPosePresetWithScreenshot("trace");
                    var preset=e.Hip._posePresets.Single();
                    plugin.Diagnostics=true;
                    e.Hip.NextPreset=new HipPreset();
                    e.Hip.SaveCurrentPosePresetWithScreenshot("trace-save");
                    var data=new Data();var go=new GameObject("RotationObject_test");go.transform.SetParent(e.Bridge.Female,false);
                    e.Composer._runtimeObjects[data.id]=new RuntimeObject{Data=data,GameObject=go};
                    e.Composer.NextData=data;e.Composer.SaveCurrentToPreset(null,"diagnostic-object",0);
                    e.Composer.ActivatePreset(e.Composer._presets.Single());
                    var parent=e.Composer.ResolveConfiguredParentTransform(data,data.id,out _,out _);go.transform.SetParent(parent,false);
                    e.Hip._settings.FollowComposerEnabled[0]=true;e.Hip._settings.FollowComposerObjectIds[0]="hook";
                    e.Bridge.Female.SetPositionAndRotation(b.position,b.rotation);
                    e.Hip.ApplyPosePreset(preset,true,"manual");
                    var logs=plugin.DiagnosticMessages;
                    Program.Assert(logs.Any(s=>s.Contains("stage=save.prepare")&&s.Contains("source={map=")),"save source map and pose traced");
                    Program.Assert(logs.Any(s=>s.Contains("stage=save.part")&&s.Contains("saved=")),"saved IK coordinates and fingerprint traced");
                    Program.Assert(logs.Any(s=>s.Contains("stage=save.committed")),"successful origin commit traced");
                    Program.Assert(logs.Any(s=>s.Contains("reason=Composer-linked")),"linked IK exclusion explained");
                    Program.Assert(logs.Any(s=>s.Contains("stage=Hip.load.result")&&s.Contains("rot_xyzw=")),"load transformation logs exact quaternion");
                    Program.Assert(logs.Any(s=>s.Contains("stage=Hip.applied")&&s.Contains("transition-start")),"native application result and phase traced");
                    e.Hip.ApplyPosePreset(new HipPreset(),true,"unknown");
                    Program.Assert(logs.Any(s=>s.Contains("reason=origin-missing-or-content-mismatch")),"unknown source is explicit in load trace");
                    e.Hip.States[1].Proxy.SetPositionAndRotation(p.position,p.rotation);
                    e.Hip.States[1].HasPostDragHold=true;e.Hip.States[1].PostDragHoldPos=p.position;e.Hip.States[1].PostDragHoldRot=p.rotation;
                    e.Scene.Move=()=>e.Bridge.Female.SetPositionAndRotation(a.position,a.rotation);e.Scene.ChangeAnimator();
                    Program.Assert(logs.Any(s=>s.Contains("stage=move.frames")&&s.Contains("ChangeAnimator")&&s.Contains("distance_m=")),"native operation and movement delta traced");
                    Program.Assert(logs.Any(s=>s.Contains("stage=move.IK.result")&&s.Contains("calculated={")&&s.Contains("actual={")),"calculated and actual IK target recorded separately");
                    Program.Assert(logs.Any(s=>s.Contains("stage=move.hold")),"hold transformation traced");
                    Program.Assert(logs.Any(s=>s.Contains("stage=move.transition")),"transition endpoints traced");
                    Program.Assert(logs.Any(s=>s.Contains("stage=Composer.anchor")&&s.Contains("savedParent={")),"Composer anchor source and result traced");
                    Program.Assert(logs.Any(s=>s.Contains("stage=move.after.object")&&s.Contains("local_rot_xyzw=")&&s.Contains("parent=")),"every Composer object world local and actual parent traced");
                    Program.Assert(!logs.Any(s=>s.Contains("diagnostic-unavailable")),"diagnostic paths collect complete native fixture data");
                    int count=logs.Count;plugin.Diagnostics=false;
                    e.Scene.Move=()=>e.Bridge.Female.SetPositionAndRotation(b.position,b.rotation);e.Scene.SetLocalPosition();
                    Program.Assert(logs.Count==count,"OFF stops all later coordinate events");
                    Program.Near(RuntimeBridge.Pose(e.Hip.States[1].Proxy),p,"diagnostics do not change roundtrip movement");
                    plugin.Diagnostics=true;e.Session.FailHipCapture=true;e.Session.TraceHipApplied(true,"diagnostic-failure");
                    Program.Assert(logs.Any(s=>s.Contains("diagnostic-unavailable")),"diagnostic capture failure cannot abort application");
                }
                var culture=CultureInfo.CurrentCulture;
                try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("fr-FR");Program.Assert(PlacementSession.PoseText(Program.Pose(1.25f,0,0,0)).Contains("1.25"),"coordinate precision is culture independent");}
                finally{CultureInfo.CurrentCulture=culture;}
            }
            finally{plugin.Diagnostics=false;plugin.DiagnosticMessages.Clear();}
        }
    }
}
