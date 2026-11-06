using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using CanonPlugins.Shared;
using MainGameBlankMapAdd.Compatibility;
using BepInEx.Bootstrap;
using System.Reflection;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private HLocationDocument _hLocations;
        private HLocationPreset _hPlaceActive, _hPlaceDraft;
        private HPlaceSnapshot _hPlaceOriginal, _hPlaceEditBefore, _hPlacePendingBefore;
        private Transform _hPlaceRoot;
        private string _hPlaceKey, _hPlaceStatus;
        private bool _hPlaceBusy;
        private int _hPlaceGeneration, _hPlacePoseDepth;
        private readonly Dictionary<string,Texture2D> _hPlaceImages=new Dictionary<string,Texture2D>();
        private string HPlacePath => Path.Combine(_pluginDir,"HLocationPresets.json");
        private string HPlaceImageDir => Path.Combine(_pluginDir,"HLocationPresets");

        private sealed class HMemberSnapshot { internal Transform Root;internal Vector3 Position;internal Quaternion Rotation; }
        private sealed class HCameraSnapshot { internal Vector3 Position, Focus;internal Quaternion Rotation;internal float Fov; }
        private sealed class HPlaceSnapshot { internal List<HMemberSnapshot> Members;internal HCameraSnapshot Camera;internal HLocationPreset Active; }
        private static Vector3 HV(float[] value)=>new Vector3(value[0],value[1],value[2]);
        private static float[] HA(Vector3 value)=>new[]{value.x,value.y,value.z};
        private static bool IsFiniteMapVector(Vector3 value)=>!float.IsNaN(value.x)&&!float.IsInfinity(value.x)&&!float.IsNaN(value.y)&&!float.IsInfinity(value.y)&&!float.IsNaN(value.z)&&!float.IsInfinity(value.z);

        private void LoadHLocations()
        {
            _hLocations=null;_hPlaceStatus=null;
            TraceHPlace("load.begin",()=>"path="+HPlacePath+" exists="+File.Exists(HPlacePath)+" legacy="+MapWarpPath);
            try{_hLocations=HLocationPresetStore.Load(HPlacePath,MapWarpPath);TraceHPlaceDocument("load.parsed",_hLocations);}
            catch(Exception ex){TraceHPlace("load.failed",()=>"path="+HPlacePath+" reason="+HTraceText(ex.ToString()));ReportPoseLocationFailure("H場所を読み込めません。保存ファイルは保持しています："+ex.Message);}
        }
        private bool HPlaceContext(out string key,out Transform root)
        {
            return TryMapWarpContext(out key,out root,out _) && _hLocations!=null && PluginSessionGate.Capture().IsValid;
        }
        private CameraControl_Ver2 HPlaceCamera => PluginSessionGate.HScene?.flags?.ctrlCamera;
        private bool AdoptHPlaceCamera()
        {
            try{
                if(!Chainloader.PluginInfos.TryGetValue("com.kks.maingame.cameracontrol",out var info)||info.Instance==null)return true;
                var adopt=info.Instance.GetType().GetMethod("TryAdoptHLocationCamera",BindingFlags.Public|BindingFlags.Static);
                return adopt==null || (bool)adopt.Invoke(null,null);
            }catch(Exception){return false;}
        }
        private List<Transform> HPlaceActors()
        {
            var actors=new List<Transform>();
            var females=PluginSessionGate.ReadScene<List<ChaControl>>("lstFemale");
            if(females!=null)foreach(var actor in females)if(actor!=null)actors.Add(actor.transform);
            foreach(string name in new[]{"male","male1"}){
                var actor=PluginSessionGate.ReadScene<ChaControl>(name);if(actor!=null)actors.Add(actor.transform);
            }
            return actors.Distinct().Where(t=>t!=null).ToList();
        }
        private HCameraSnapshot ReadHPlaceCamera()
        {
            var controller=HPlaceCamera;
            if(controller==null||controller.thisCmaera==null)throw new InvalidOperationException("カメラを取得できません。");
            var data=controller.GetCameraData();
            return new HCameraSnapshot{Position=controller.thisCmaera.transform.position,Rotation=controller.thisCmaera.transform.rotation,
                Focus=controller.transBase!=null?controller.transBase.TransformPoint(data.Pos):data.Pos,Fov=controller.thisCmaera.fieldOfView};
        }
        private void WriteHPlaceCamera(HCameraSnapshot snapshot)
        {
            var controller=HPlaceCamera;if(controller==null||snapshot==null)return;
            var data=controller.GetCameraData();
            data.Pos=controller.transBase!=null?controller.transBase.InverseTransformPoint(snapshot.Focus):snapshot.Focus;
            data.Rot=(controller.transBase!=null?Quaternion.Inverse(controller.transBase.rotation)*snapshot.Rotation:snapshot.Rotation).eulerAngles;
            data.Dir=Quaternion.Inverse(snapshot.Rotation)*(snapshot.Position-snapshot.Focus);data.Fov=snapshot.Fov;
            controller.SetCameraData(data);
            AdoptHPlaceCamera();
        }
        private void SyncHPlaceCameraBase()
        {
            if(_femaleChara!=null&&_femaleChara.objTop!=null)HPlaceCamera?.SetWorldBase(_femaleChara.objTop.transform);
        }
        private HPlaceSnapshot SnapshotHPlace()=>new HPlaceSnapshot{Members=HPlaceActors().Select(t=>new HMemberSnapshot{Root=t,Position=t.position,Rotation=t.rotation}).ToList(),Camera=ReadHPlaceCamera(),Active=_hPlaceActive?.Clone()};
        private void RestoreHPlace(HPlaceSnapshot snapshot)
        {
            if(snapshot==null)return;
            TraceHPlace("rollback.begin",()=>HPlaceActual(_hPlaceRoot));
            string placeId=snapshot.Active?.Id??_hPlaceActive?.Id??"";
            long notification=BeginHLocationNotification("restore",placeId);
            try{foreach(var member in snapshot.Members)if(member.Root!=null)member.Root.SetPositionAndRotation(member.Position,member.Rotation);}
            finally{EndHLocationNotification(notification,"restore",placeId);}
            SyncHPlaceCameraBase();WriteHPlaceCamera(snapshot.Camera);_hPlaceActive=snapshot.Active?.Clone();
            TraceHPlace("rollback.applied",()=>HPlaceActual(_hPlaceRoot));
        }
        private void StopHLocations(bool restore = true, bool resetBaseline = true)
        {
            ++_roomRestoreGeneration;
            EndCategoryGizmo();
            _hDefaultRequest.Reset();
            ReleaseHPlaceSliderCapture();
            ++_hPlaceGeneration;
            _hPlaceUiAction=null;
            // Cleanup must also run after the gate permit has expired.
            if (restore) RestoreHPlace(_hPlaceOriginal??_hPlacePendingBefore??_hPlaceEditBefore);
            if (resetBaseline) { _poseInitialPlacement=null; _poseInitialRoot=null; _poseInitialKey=null; }
            _hPlaceOriginal=null;_hPlaceEditBefore=null;_hPlacePendingBefore=null;_hPlaceDraft=null;_hPlaceActive=null;_hPlaceRoot=null;_hPlaceKey=null;_hPlaceBusy=false;
            foreach(var image in _hPlaceImages.Values)if(image!=null)Destroy(image);
            _hPlaceImages.Clear();_hPlacePoseDepth=0;
        }
        private void BindHPlaceContext(string key,Transform root)
        {
            if(_hPlaceRoot==root&&_hPlaceKey==key)return;
            StopHLocations(false, _poseInitialRoot != root || _poseInitialKey != key);_hPlaceRoot=root;_hPlaceKey=key;
            // Recall and editing can bind the map before its preset list is drawn.
            ReloadHPlaceThumbnails();
        }
        private void SaveHPlaceCamera(HLocationPreset preset,Transform root,HCameraSnapshot camera)
        {
            preset.CameraPosition=HA(root.InverseTransformPoint(camera.Position));
            preset.CameraFocus=HA(root.InverseTransformPoint(camera.Focus));
            preset.CameraRotation=HA((Quaternion.Inverse(root.rotation)*camera.Rotation).eulerAngles);preset.Fov=camera.Fov;
        }
        private HCameraSnapshot PresetHPlaceCamera(HLocationPreset preset,Transform root)=>new HCameraSnapshot{
            Position=root.TransformPoint(HV(preset.CameraPosition)),Focus=root.TransformPoint(HV(preset.CameraFocus)),
            Rotation=root.rotation*Quaternion.Euler(HV(preset.CameraRotation)),Fov=preset.Fov};
        private void ApplyHPlace(HLocationPreset preset,Transform root,bool restoreCamera,string operation="edit")
        {
            TraceHPlace("apply.begin",()=>"operation="+operation+" "+HPlaceRecord(preset)+HPlaceActual(root));
            try {
            if(_femaleChara==null)throw new InvalidOperationException("主役のキャラがいません。");
            var currentCamera=restoreCamera?null:ReadHPlaceCamera();
            Vector3 origin=_femaleChara.transform.position;
            Quaternion delta=root.rotation*Quaternion.Euler(HV(preset.Rotation))*Quaternion.Inverse(_femaleChara.transform.rotation);
            Vector3 target=root.TransformPoint(HV(preset.Position));
            var members=HPlaceActors().Select(t=>new HMemberSnapshot{Root=t,Position=t.position,Rotation=t.rotation}).ToList();
            if(members.Count==0)throw new InvalidOperationException("H参加キャラを取得できません。");
            long notification=BeginHLocationNotification(operation,preset.Id);
            try{foreach(var member in members)member.Root.SetPositionAndRotation(HLocationGeometry.MoveMember(member.Position,origin,target,delta),delta*member.Rotation);}
            finally{EndHLocationNotification(notification,operation,preset.Id);}
            SyncHPlaceCameraBase();
            if(restoreCamera&&!preset.Legacy)WriteHPlaceCamera(PresetHPlaceCamera(preset,root));
            else if(currentCamera!=null)WriteHPlaceCamera(currentCamera);
            TraceHPlace("apply.applied",()=>"operation="+operation+" "+HPlaceRecord(preset)+" targetError="+Vector3.Distance(_femaleChara.transform.position,target).ToString("R",System.Globalization.CultureInfo.InvariantCulture)+HPlaceActual(root));
            } catch(Exception ex) {TraceHPlace("apply.failed",()=>"operation="+operation+" "+HPlaceRecord(preset)+" reason="+HTraceText(ex.ToString()));throw;}
        }
        private void BeginHPlaceCapture(HLocationPreset source=null,bool legacy=false,bool useCurrentCamera=false)
        {
            _hDefaultRequest.Cancel();++_roomRestoreGeneration;
            if(_hPlaceBusy||!HPlaceContext(out string key,out Transform root))return;
            if(source!=null&&!HLocationMapIdentity.Same(source.MapKey,key))return;
            if(!AdoptHPlaceCamera()){_hPlaceStatus="別の機能がカメラを使用中です。終了してから保存してください。";return;}
            BindHPlaceContext(key,root);_hPlaceBusy=true;
            StartCoroutine(GuardHPlaceCapture(CaptureHPlace(key,root,source?.Clone(),legacy,_hPlaceGeneration,useCurrentCamera)));
        }
        private IEnumerator CaptureHPlace(string key,Transform root,HLocationPreset source,bool legacy,int generation,bool useCurrentCamera=false)
        {
            var permit=PluginSessionGate.Capture();HPlaceSnapshot before=null;HLocationPreset preset=null;bool success=false;
            try
            {
                before=SnapshotHPlace();_hPlacePendingBefore=before;
                var document=_hLocations.Clone();
                if(source==null){
                    Vector3 direction=Vector3.ProjectOnPlane(before.Camera.Position-before.Camera.Focus,Vector3.up);
                    Vector3 front=Vector3.ProjectOnPlane(_femaleChara.objTop!=null?_femaleChara.objTop.transform.forward:_femaleChara.transform.forward,Vector3.up);
                    if(front.sqrMagnitude<.000001f)front=Vector3.ProjectOnPlane(_femaleChara.transform.forward,Vector3.up);
                    Quaternion rotation=direction.sqrMagnitude>.000001f&&front.sqrMagnitude>.000001f
                        ? Quaternion.FromToRotation(front,direction)*_femaleChara.transform.rotation : _femaleChara.transform.rotation;
                    preset=new HLocationPreset{Id=Guid.NewGuid().ToString("N"),MapKey=key,Name=document.AllocateName(key),
                        Position=HA(root.InverseTransformPoint(before.Camera.Focus)),Center=HA(root.InverseTransformPoint(before.Camera.Focus)),
                        Rotation=HA((Quaternion.Inverse(root.rotation)*rotation).eulerAngles)};
                    SaveHPlaceCamera(preset,root,before.Camera);
                }else{
                    preset=source;preset.Name=(preset.Name??"").Trim();
                    if(legacy){preset.Rotation=HA((Quaternion.Inverse(root.rotation)*_femaleChara.transform.rotation).eulerAngles);SaveHPlaceCamera(preset,root,before.Camera);}
                    else if(useCurrentCamera)SaveHPlaceCamera(preset,root,before.Camera);
                }
                preset.Legacy=false;preset.Validate();
                ApplyHPlace(preset,root,true,"save");
                // One bounded settle point, after animation and before the offscreen scene render.
                yield return new WaitForEndOfFrame();
                if(!permit.IsValid||generation!=_hPlaceGeneration||!HPlaceContext(out string current,out Transform currentRoot)||current!=key||currentRoot!=root)yield break;
                // Reapply the captured world camera once if native animation updated its base.
                WriteHPlaceCamera(PresetHPlaceCamera(preset,root));
                byte[] png=CaptureHPlacePng();
                int index=document.Presets.FindIndex(p=>p.Id==preset.Id&&HLocationMapIdentity.Same(p.MapKey,key));
                if(index<0)document.Presets.Add(preset);else document.Presets[index]=preset;
                SaveHLocationImage(document,preset,png);
                _hLocations=document;_hPlaceOriginal=_hPlaceOriginal??_hPlaceEditBefore??before;_hPlaceActive=preset.Clone();_hPlaceActive.MapKey=key;
                _hPlaceDraft=null;_hPlaceEditBefore=null;ReloadHPlaceThumbnails();
                _hPlaceStatus="「"+preset.Name+"」を保存しました。";success=true;
            }
            finally
            {
                try{if(!success&&generation==_hPlaceGeneration)RestoreHPlace(before);}
                finally{if(generation==_hPlaceGeneration){_hPlaceBusy=false;_hPlacePendingBefore=null;}}
            }
        }
        // MoveNext exceptions must be handled outside the iterator's yield-containing try block.
        private IEnumerator GuardHPlaceCapture(IEnumerator work)
        {
            try{
                while(true){object next;try{if(!work.MoveNext())yield break;next=work.Current;}catch(Exception ex){TraceHPlace("capture.failed",()=>"reason="+ex);_hPlaceStatus="H場所を保存できません："+ex.Message;yield break;}yield return next;}
            }finally{(work as IDisposable)?.Dispose();}
        }
        private void RecallHPlace(string id,bool restoreCamera=true,bool automatic=false)
        {
            if(!automatic){_hDefaultRequest.Cancel();++_roomRestoreGeneration;}
            if(_hPlaceBusy||_hPlaceDraft!=null||!HPlaceContext(out string key,out Transform root)){TraceHPlace("recall.skipped",()=>"id="+id+" busy="+_hPlaceBusy+" draft="+(_hPlaceDraft!=null)+" reason=not-ready");return;}
            if(!AdoptHPlaceCamera()){TraceHPlace("recall.skipped",()=>"id="+id+" reason=camera-owned");_hPlaceStatus="別の機能がカメラを使用中です。";return;}
            BindHPlaceContext(key,root);
            var preset=_hLocations.Presets.SingleOrDefault(p=>p.Id==id&&HLocationMapIdentity.Same(p.MapKey,key));if(preset==null){TraceHPlace("recall.skipped",()=>"id="+id+" map="+HTraceText(key)+" reason=preset-missing");return;}
            if(preset.Legacy){BeginHPlaceCapture(preset,true);return;}
            HPlaceSnapshot before=null;
            try{before=SnapshotHPlace();ApplyHPlace(preset,root,restoreCamera,automatic?"default":"recall");_hPlaceOriginal=_hPlaceOriginal??before;_hPlaceActive=preset.Clone();_hPlaceActive.MapKey=key;_hPlaceStatus="「"+preset.Name+"」を呼び出しました。";}
            catch(Exception ex){TraceHPlace("recall.failed",()=>"id="+id+" reason="+ex);RestoreHPlace(before);_hPlaceStatus="呼び出せません："+ex.Message;}
        }
        private byte[] CaptureHPlacePng()
        {
            var camera=HPlaceCamera?.thisCmaera;if(camera==null)throw new InvalidOperationException("撮影カメラがありません。");
            var canvases=UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c=>c.enabled).ToArray();
            var target=camera.targetTexture;var active=RenderTexture.active;float aspect=camera.aspect;
            RenderTexture render=null;Texture2D pixels=null;
            try{
                foreach(var canvas in canvases)canvas.enabled=false;
                render=RenderTexture.GetTemporary(Mathf.Max(1,Screen.width),Mathf.Max(1,Screen.height),24,RenderTextureFormat.ARGB32);
                camera.targetTexture=render;camera.aspect=(float)render.width/render.height;camera.Render();RenderTexture.active=render;
                pixels=new Texture2D(render.width,render.height,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,render.width,render.height),0,0);pixels.Apply();return pixels.EncodeToPNG();
            }finally{
                camera.targetTexture=target;camera.aspect=aspect;RenderTexture.active=active;
                foreach(var canvas in canvases)if(canvas!=null)canvas.enabled=true;
                if(pixels!=null)Destroy(pixels);if(render!=null)RenderTexture.ReleaseTemporary(render);
            }
        }
        private void ReapplyHPlaceAfterPose()
        {
            if (ApplyCategoryLocation(false) != PoseLocationResult.NotHandled) return;
            // Individual positions must never become the shared fallback when recall is disabled/unavailable.
            if (_hPlaceDraft == null && _hPlaceActive?.Id?.StartsWith("pose-", StringComparison.Ordinal) == true) return;
            if((_hPlaceActive==null&&_hPlaceDraft==null)||_hPlaceBusy||!HPlaceContext(out string key,out Transform root)||root!=_hPlaceRoot||key!=_hPlaceKey)return;
            try{var camera=ReadHPlaceCamera();ApplyHPlace(_hPlaceDraft??_hPlaceActive,root,false,"posture-reapply");WriteHPlaceCamera(camera);}
            catch(Exception ex){_hPlaceStatus="体位変更後の配置に失敗しました："+ex.Message;}
        }
        [HarmonyPrefix,HarmonyPatch(typeof(HSceneProc),"ChangeAnimator")]
        private static void HPlacePosePrefix(HSceneProc __instance){var p=PluginCompatibilityResolver.ResolveLoadedPlugin();if(p!=null&&p._sessionStarted&&ReferenceEquals(__instance,PluginSessionGate.HScene)){if(p._categoryEditing)p.CloseHPlaceEdit();++p._hPlacePoseDepth;}}
        [HarmonyFinalizer,HarmonyPatch(typeof(HSceneProc),"ChangeAnimator")]
        private static void HPlacePoseFinalizer(HSceneProc __instance,Exception __exception){var p=PluginCompatibilityResolver.ResolveLoadedPlugin();if(p==null||!ReferenceEquals(__instance,PluginSessionGate.HScene))return;if(p._hPlacePoseDepth>0)--p._hPlacePoseDepth;if(p._hPlacePoseDepth==0&&__exception==null)p.ReapplyHPlaceAfterPose();}
        [HarmonyPostfix,HarmonyPatch(typeof(HSceneProc),"SetLocalPosition")]
        private static void HPlacePositionPostfix(HSceneProc __instance){var p=PluginCompatibilityResolver.ResolveLoadedPlugin();if(p!=null&&p._hPlacePoseDepth==0&&ReferenceEquals(__instance,PluginSessionGate.HScene))p.ReapplyHPlaceAfterPose();}
    }
}
