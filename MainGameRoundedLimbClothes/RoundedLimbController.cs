using System;
using System.Collections.Generic;
using System.Linq;
using ExtensibleSaveFormat;
using KKAPI;
using KKAPI.Chara;
using Sideloader.AutoResolver;
using UnityEngine;

namespace MainGameRoundedLimbClothes
{
    public sealed class RoundedLimbController : CharaCustomFunctionController
    {
        internal static readonly HashSet<RoundedLimbController> Live=new HashSet<RoundedLimbController>();
        SavedSettings saved=new SavedSettings();
        PositionSettings draft;
        int draftCoordinate=-1;
        SkinnedMeshRenderer renderer;
        BodyMeshAdapter adapter;
        Mesh owned;
        Material capMaterial;
        bool appliedArms,appliedLegs;
        PositionSettings appliedSettings;
        bool previewApplied=true;
        internal string Status {get;private set;}="専用の靴下・手袋を選んでください。";
        internal int ChangeDepth;
        internal int Coordinate=>ChaControl.fileStatus.coordinateType;
        internal bool Editing=>draft!=null;
        internal bool Allowed=>Plugin.Instance!=null&&Plugin.Instance.Allows(ChaControl);
        internal PositionSettings Settings
        {
            get
            {
                if(draft!=null&&draftCoordinate==Coordinate)return draft;
                return saved.Coordinates.TryGetValue(Coordinate,out var value)?value:new PositionSettings();
            }
        }
        protected override void OnReload(GameMode mode,bool maintainState)
        {
            Live.Add(this);Restore();draft=null;draftCoordinate=-1;
            if(!maintainState)
            {
                try{saved=Read(GetExtendedData())??new SavedSettings();}
                catch(Exception ex){saved=new SavedSettings();Fail(ex);}
            }
            Plugin.Instance?.ActorReloaded();
            Refresh();
        }
        protected override void OnCoordinateBeingLoaded(ChaFileCoordinate coordinate,bool maintainState)
        {
            draft=null;draftCoordinate=-1;
            if(!maintainState)
            {
                try
                {
                    var data=Read(GetCoordinateExtendedData(coordinate));
                    saved.Coordinates[Coordinate]=data!=null&&data.Coordinates.TryGetValue(0,out var value)?value:new PositionSettings();
                }
                catch(Exception ex){Fail(ex);}
            }
            Refresh();
        }
        protected override void OnCardBeingSaved(GameMode mode)=>SetExtendedData(Write(saved));
        protected override void OnCoordinateBeingSaved(ChaFileCoordinate coordinate)
        {
            var data=new SavedSettings();
            data.Coordinates[0]=saved.Coordinates.TryGetValue(Coordinate,out var value)?value.Copy():new PositionSettings();
            SetCoordinateExtendedData(coordinate,Write(data));
        }
        static SavedSettings Read(PluginData data)=>data!=null&&data.data.TryGetValue("settings",out var value)&&value is string json?SavedSettings.Decode(json):null;
        static PluginData Write(SavedSettings data)=>new PluginData{version=1,data=new Dictionary<string,object>{{"settings",SavedSettings.Encode(data)}}};
        internal void BeginEdit()
        {
            if(!Allowed)return;
            if(draft!=null&&draftCoordinate==Coordinate)return;
            draft=Settings.Copy();draftCoordinate=Coordinate;
        }
        internal void Preview(PositionSettings value)
        {
            if(!Allowed||value==null||!value.Valid)return;
            BeginEdit();draft=value.Copy();
            previewApplied=Refresh(); // Keep the requested value visible; retain the last good mesh on failure.
        }
        internal bool Commit()
        {
            if(!Allowed||draft==null||draftCoordinate!=Coordinate||!previewApplied)return false;
            saved.Coordinates[Coordinate]=draft.Copy();SetExtendedData(Write(saved));draft=null;draftCoordinate=-1;return true;
        }
        internal void CancelEdit()
        {
            if(draft==null)return;draft=null;draftCoordinate=-1;
            if(Allowed)Refresh();
        }
        internal bool Selected(int kind)
        {
            if(ChaControl==null||ChaControl.sex!=1)return false;
            int category=kind==4?109:111;
            int id=ChaControl.nowCoordinate.clothes.parts[kind].id;
            var info=UniversalAutoResolver.TryGetResolutionInfo((ChaListDefine.CategoryNo)category,id);
            return info!=null&&info.GUID==Plugin.ModGuid&&info.Slot==1;
        }
        internal bool Worn(int kind)=>Selected(kind)&&ChaControl.fileStatus.clothesState[kind]!=3;
        internal bool Refresh()
        {
            if(!Allowed)return false;
            if(ChangeDepth>0){Status="衣装・身体の読み込み完了待ちです。";return false;}
            try
            {
                bool arms=Worn(4),legs=Worn(6);
                if(!arms&&!legs){Restore();previewApplied=true;Status="専用の靴下・手袋を選び、着用状態にしてください。";return true;}
                var target=ChaControl.rendBody as SkinnedMeshRenderer;
                if(target==null)throw new InvalidOperationException("身体の読み込みが完了していません。");
                if(renderer!=target||adapter==null||target.sharedMesh!=owned&&target.sharedMesh!=adapter.Source)
                {
                    Restore();renderer=target;adapter=new BodyMeshAdapter(target.sharedMesh,target.bones);
                }
                var settings=Settings.Copy();
                if(owned!=null&&appliedArms==arms&&appliedLegs==legs&&Same(appliedSettings,settings))
                {SyncSkin();previewApplied=true;Status=(arms?"腕：適用中　":"")+(legs?"脚：適用中":"");return true;}
                var next=adapter.Generate(settings,arms,legs);
                Material nextCap=null;
                try
                {
                    var materials=target.sharedMaterials;
                    int slots=adapter.Source.subMeshCount;
                    if(materials.Length<slots)throw new InvalidOperationException("身体の材質が不足しています。");
                    nextCap=new Material(materials[0]){name="RoundedLimbClothes skin (character owned)"};
                    Unmask(nextCap);
                    var nextMaterials=materials.Take(slots).Concat(new[]{nextCap}).ToArray();
                    var oldMesh=owned;var oldCap=capMaterial;
                    var blendWeights=Enumerable.Range(0,adapter.Source.blendShapeCount).Select(target.GetBlendShapeWeight).ToArray();
                    target.sharedMaterials=nextMaterials;target.sharedMesh=next;
                    for(int shape=0;shape<blendWeights.Length;++shape)target.SetBlendShapeWeight(shape,blendWeights[shape]);
                    owned=next;capMaterial=nextCap;appliedArms=arms;appliedLegs=legs;appliedSettings=settings;
                    if(oldMesh!=null)Destroy(oldMesh);if(oldCap!=null)Destroy(oldCap);
                    previewApplied=true;Status=(arms?"腕：適用中　":"")+(legs?"脚：適用中":"");return true;
                }
                catch{Destroy(next);if(nextCap!=null)Destroy(nextCap);throw;}
            }
            catch(Exception ex){Fail(ex);return false;}
        }
        static bool Same(PositionSettings a,PositionSettings b)=>a!=null&&a.LeftArm==b.LeftArm&&a.RightArm==b.RightArm&&a.LeftLeg==b.LeftLeg&&a.RightLeg==b.RightLeg;
        internal void SyncSkin()
        {
            if(!Allowed||renderer==null||owned==null||renderer.sharedMesh!=owned||capMaterial==null)return;
            var materials=renderer.sharedMaterials;
            if(materials.Length==0||materials[0]==null)return;
            if(capMaterial.shader!=materials[0].shader)capMaterial.shader=materials[0].shader;
            capMaterial.CopyPropertiesFromMaterial(materials[0]);Unmask(capMaterial);
            int slot=adapter.Source.subMeshCount;
            if(materials.Length==slot)renderer.sharedMaterials=materials.Concat(new[]{capMaterial}).ToArray();
            else if(materials.Length>slot&&materials[slot]!=capMaterial){materials[slot]=capMaterial;renderer.sharedMaterials=materials;}
        }
        static void Unmask(Material material)
        {
            if(material.HasProperty("_AlphaMask"))material.SetTexture("_AlphaMask",null);
            if(material.HasProperty("_alpha_a"))material.SetFloat("_alpha_a",0);
            if(material.HasProperty("_alpha_b"))material.SetFloat("_alpha_b",0);
        }
        internal void StopRuntime()
        {draft=null;draftCoordinate=-1;ChangeDepth=0;Restore();}
        internal void Restore()
        {
            if(renderer!=null)
            {
                if(owned!=null&&renderer.sharedMesh==owned&&adapter!=null)
                {
                    var blendWeights=Enumerable.Range(0,adapter.Source.blendShapeCount).Select(renderer.GetBlendShapeWeight).ToArray();
                    renderer.sharedMesh=adapter.Source;
                    for(int shape=0;shape<blendWeights.Length;++shape)renderer.SetBlendShapeWeight(shape,blendWeights[shape]);
                }
                if(capMaterial!=null)renderer.sharedMaterials=renderer.sharedMaterials.Where(m=>m!=capMaterial).ToArray();
            }
            if(owned!=null)Destroy(owned);if(capMaterial!=null)Destroy(capMaterial);
            owned=null;capMaterial=null;adapter=null;renderer=null;appliedSettings=null;
        }
        void Fail(Exception ex)
        {
            previewApplied=false;Status="未適用："+ex.Message;
            if(Plugin.Instance!=null&&Plugin.Instance.Diagnostics)Plugin.Instance.Report(ex);
        }
        protected override void OnDestroy(){Live.Remove(this);StopRuntime();base.OnDestroy();}
    }
}
