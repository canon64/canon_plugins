using System;
using UnityEngine;
using Valve.VR;
using VRGIN.Core;
namespace MainGameDesktopDisplay
{
    public sealed partial class Plugin
    {
        static void CheckMonitorPresets()
        {
            Check(MonitorKey(@"\\.\DISPLAY1")==MonitorKey(@"\.\DISPLAY1")&&MonitorKey("DISPLAY1")=="DISPLAY1","monitor lookup accepts old escaped names and stores a stable identifier");
            foreach(var side in new[]{SteamVR_Input_Sources.LeftHand,SteamVR_Input_Sources.RightHand})
            {
                var p=VrSession();var hand=side==SteamVR_Input_Sources.LeftHand?VR.Mode.Left:VR.Mode.Right;
                var other=side==SteamVR_Input_Sources.LeftHand?SteamVR_Input_Sources.RightHand:SteamVR_Input_Sources.LeftHand;
                var axis=SteamVR_Actions.legacy_emulate.Axis0_2D;var grip=SteamVR_Actions.legacy_emulate.Grip_Press;
                p._clipDraft[0]=12;p._clipDirty=true;p.CommitClipDraft();
                hand.transform.position=p._panelTransform.position;grip.Down(side);var focus=p._grabLock;var position=p._root.transform.position;float width=p._width.Value;
                axis.Move(other,1,0);Check(p._monitor==0,"other hand cannot change held monitor");
                axis.Move(side,1,.1f);Check(p._monitor==1&&p._monitorDevice.Value=="secondary"&&p._grabHand==hand&&p._grabLock==focus,"holding stick right switches and saves monitor without releasing grip: "+side);
                Check((position-p._root.transform.position).sqrMagnitude==0&&p._width.Value==width,"horizontal monitor switch preserves pose and width");
                int writes=p._monitorDevice.Writes,created=DesktopCapture.Created;axis.Move(side,.9f,.05f);
                Check(p._monitor==1&&p._monitorDevice.Writes==writes&&DesktopCapture.Created==created,"held horizontal deflection/jitter cannot repeat switch or capture recreation");
                p._clipDraft[1]=25;p._clipDirty=true;p.CommitClipDraft();
                axis.Move(side,0,0);axis.Move(side,1,0);Check(p._monitor==0&&p._clipDraft[0]==12&&p._clipDraft[1]==0,"right wraps and restores previous monitor crop");
                axis.Move(side,0,0);axis.Move(side,-1,0);Check(p._monitor==1&&p._clipDraft[1]==25,"left wraps and restores independent crop");
                p._paused=true;p._driver.enabled=false;axis.Move(side,0,0);axis.Move(side,-1,0);
                Check(p._paused&&!p._driver.enabled&&p._grabHand==hand,"monitor switch preserves pause and holder");
                grip.Up(side);p.CompletePendingGrabLocks();p.PlaybackBarDelete();p.Recall();
                Check(p._monitor==0&&p._clipDraft[0]==12,"deletion and recall restore last monitor and crop");
                p.SelectMonitor(1);p._presetName="縦画面クリップ";p.SaveDisplayPreset();string encoded=p._presetsEntry.Value;
                Check(!encoded.Contains("\\")&&p._displayPresets.Count==1,"named clipping preset uses config-safe persistent data");
                p.SelectMonitor(0);p._width.Value=2;p.LoadDisplayPreset(0);
                Check(p._monitor==1&&p._clipDraft[1]==25&&p._width.Value==width&&p._monitorDevice.Value=="secondary","preset restores its source monitor, four edges and screen width");
                p.InitializeDisplayPresets();Check(p._displayPresets.Count==1&&p._lastPreset.Value=="縦画面クリップ","preset collection and last selection restore from config");
                p._displayPresets[0].Monitor="unplugged";int current=p._monitor;p.LoadDisplayPreset(0);
                Check(p._monitor==current&&p._presetsEntry.Value==encoded,"missing preset monitor preserves current display and saved data");
                p._presetsEntry.Value="damaged";p.InitializeDisplayPresets();p.SaveDisplayPreset();
                Check(!p._presetsReadable&&p._presetsEntry.Value=="damaged","malformed preset data is never silently overwritten");
                p.StopSession();p.OnDestroy();VR.Active=false;
            }
            CheckMonitorPresetAssignments();
        }
        static void CheckMonitorPresetAssignments()
        {
            var p=VrSession();
            p._clipDraft[0]=3;p._clipDirty=true;p.CommitClipDraft();p._width.Value=.6f;p._presetName="主画面A";p.SaveDisplayPreset();
            p._clipDraft[0]=11;p._clipDirty=true;p.CommitClipDraft();p._width.Value=.9f;p._presetName="主画面B";p.SaveDisplayPreset();
            int created=DesktopCapture.Created;p.AssignMonitorPreset(0,0);
            Check(p._clipDraft[0]==3&&p._width.Value==.6f&&DesktopCapture.Created==created+1,"assigning the current monitor's preset applies its crop and width once");
            p.SelectMonitor(1);p._clipDraft[1]=7;p._clipDirty=true;p.CommitClipDraft();p._width.Value=1.1f;p._presetName="副画面";p.SaveDisplayPreset();
            var hand=VR.Mode.Right;hand.transform.position=p._panelTransform.position;
            SteamVR_Actions.legacy_emulate.Grip_Press.Down(SteamVR_Input_Sources.RightHand);
            var held=p._grabHand;var focus=p._grabLock;var position=p._root.transform.position;
            var axis=SteamVR_Actions.legacy_emulate.Axis0_2D;created=DesktopCapture.Created;
            axis.Move(SteamVR_Input_Sources.RightHand,1,0);
            Check(p._monitor==0&&p._clipDraft[0]==3&&p._width.Value==.6f&&DesktopCapture.Created==created+1,"held stick switch automatically applies the target monitor's assigned preset with one recreation");
            Check(p._grabHand==held&&p._grabLock==focus&&(position-p._root.transform.position).sqrMagnitude==0,"assigned preset application preserves the native holder and world pose");
            axis.Move(SteamVR_Input_Sources.RightHand,0,0);axis.Move(SteamVR_Input_Sources.RightHand,-1,0);
            Check(p._monitor==1&&p._clipDraft[1]==7&&p._width.Value==1.1f,"switching back automatically restores the other monitor's independent assigned preset");
            created=DesktopCapture.Created;p.AssignMonitorPreset(0,1);
            Check(p._monitor==1&&DesktopCapture.Created==created,"assigning another monitor does not change or recapture the active display");
            p.SelectMonitor(0);Check(p._clipDraft[0]==11&&p._width.Value==.9f,"changing a monitor's assigned preset changes its next automatic appearance");
            p.SelectMonitor(1);p.InitializeDisplayPresets();p.RefreshMonitors();p.SelectMonitor(0);
            Check(p._monitorPresetEntries[0].Value==p._presetIds[1]&&p._clipDraft[0]==11,"per-monitor preset assignment survives config rebind and monitor enumeration");
            p.PlaybackBarDelete();p.Recall();Check(p._monitor==0&&p._clipDraft[0]==11&&p._width.Value==.9f,"recalling a deleted display applies the saved assigned preset immediately");
            string old=p._monitorPresetEntries[0].Value;p.AssignMonitorPreset(0,2);
            Check(p._monitorPresetEntries[0].Value==old,"a preset belonging to another monitor cannot be assigned accidentally");
            p.AssignMonitorPreset(0,-1);Check(p._clipDraft[0]==11,"clearing assignment restores the saved manual crop without erasing it");
            p._monitorPresetEntries[0].Value="missing-id";p.RefreshMonitors();
            Check(p._clipDraft[0]==11&&p._monitorPresetEntries[0].Value=="missing-id","missing assigned preset keeps the assignment and uses saved manual crop");
            p.StopSession();p.OnDestroy();VR.Active=false;
        }
    }
}
