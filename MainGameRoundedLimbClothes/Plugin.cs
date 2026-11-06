using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Bootstrap;
using CanonGate;
using HarmonyLib;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Maker.UI;
using MainGameUnifiedUi;
using UnityEngine;
using ChaCustom;

namespace MainGameRoundedLimbClothes
{
    [BepInPlugin(Guid,"MainGameRoundedLimbClothes","0.1.1")]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    [BepInDependency("canon.gate","2.4.0")]
    [BepInDependency("marco.kkapi")]
    [BepInDependency("com.bepis.bepinex.sideloader")]
    [BepInDependency("com.bepis.bepinex.extendedsave")]
    [BepInDependency("com.kks.maingame.unifiedui")]
    [BepInDependency("com.kks.maingame.uiinputcapture")]
    [BepInDependency("com.deathweasel.bepinex.uncensorselector",BepInDependency.DependencyFlags.SoftDependency)]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string Guid="canon.kks.roundedlimbclothes";
        public const string ModGuid="codex.local.kks.rounded-limb-clothes";
        const string TabId="rounded-limb-clothes";
        internal static Plugin Instance;
        IDisposable registration;
        GateApi.Permit permit;
        MonoBehaviour scene;
        bool running;
        int generation;
        ConfigEntry<bool> diagnostics;
        Harmony harmony;
        readonly HashSet<ChaControl> actors=new HashSet<ChaControl>();
        internal bool Diagnostics=>diagnostics!=null&&diagnostics.Value;
        internal bool Allowed=>running&&permit.IsValid;
        internal bool Allows(ChaControl cha)=>Allowed&&cha!=null&&actors.Contains(cha);
        void Awake()
        {
            Instance=this;
            diagnostics=Config.Bind("Diagnostics","Enabled",false,"衣装・メッシュ処理の例外をイベント時に記録する。OFFでは診断収集もしない。");
            CharacterApi.RegisterExtraBehaviour<RoundedLimbController>(Guid);
            MakerAPI.RegisterCustomSubCategories+=RegisterMakerControls;
            CharacterApi.CharacterReloaded+=CharacterReloaded;
            registration=GateApi.Register(Guid,StartSession,StopSession);
            harmony=new Harmony(Guid);InstallHooks();
        }
        void StartSession()
        {
            if(running||!isActiveAndEnabled)return;
            var context=GateApi.Current;
            if(context==null||(context.Kind!=GateApi.SessionKind.H&&context.Kind!=GateApi.SessionKind.Maker))return;
            permit=GateApi.Capture(Guid);if(!permit.IsValid)return;
            scene=context.Scene;running=true;++generation;
            try
            {
                ActorReloaded();
                if(context.Kind==GateApi.SessionKind.H)
                {
                    UnifiedUiApi.RegisterTab(TabId,"丸い手足",DrawTab,85);
                    UnifiedUiApi.RegisterViewportTab(TabId,null,leave=>{CancelEditing();ReleaseInput();return true;});
                }
            }
            catch{StopSession();throw;}
        }
        void StopSession()
        {
            running=false;permit=default;scene=null;++generation;
            makerWindow=false;CancelEditing();ReleaseInput();UnifiedUiApi.UnregisterTab(TabId);
            foreach(var c in RoundedLimbController.Live.ToArray())if(c!=null)c.StopRuntime();
            actors.Clear();uiActors=Array.Empty<RoundedLimbController>();
        }
        void CharacterReloaded(object sender,CharaReloadEventArgs args)=>ActorReloaded();
        internal void ActorReloaded()
        {
            if(!Allowed)return;
            var next=new HashSet<ChaControl>();
            if(GateApi.Current.Kind==GateApi.SessionKind.Maker)
            {
                var cha=MakerAPI.GetCharacterControl();if(cha!=null)next.Add(cha);
            }
            else if(scene!=null)
            {
                foreach(string name in new[]{"lstFemale","lstMale","male","male1"})
                {
                    var field=AccessTools.Field(scene.GetType(),name);
                    object value=field?.GetValue(scene);
                    if(value is ChaControl cha)next.Add(cha);
                    else if(value is IEnumerable list)foreach(object item in list)if(item is ChaControl actor)next.Add(actor);
                }
            }
            foreach(var old in actors.Except(next).ToArray())old.GetComponent<RoundedLimbController>()?.StopRuntime();
            actors.Clear();foreach(var cha in next)actors.Add(cha);
            uiActors=RoundedLimbController.Live.Where(c=>c!=null&&actors.Contains(c.ChaControl)&&c.ChaControl.sex==1).OrderBy(c=>c.ChaControl.GetInstanceID()).ToArray();
            foreach(var c in uiActors)c.Refresh();
            if(editing!=null)ResetFields();
        }
        void RegisterMakerControls(object sender,RegisterSubCategoriesEvent args)
        {
            foreach(var category in new[]{MakerConstants.Clothes.Gloves,MakerConstants.Clothes.Socks})
            {
                var button=args.AddControl(new MakerButton("丸い手足：位置を調整",category,this));
                button.OnClick.AddListener(()=>
                {
                    if(!Allowed)return;
                    var cha=MakerAPI.GetCharacterControl();var c=cha?.GetComponent<RoundedLimbController>();
                    if(c==null||!Allows(cha))return;
                    OpenMakerEditor(c);
                });
            }
        }
        internal void Report(Exception ex){if(Diagnostics)Logger.LogWarning(ex.ToString());}
        bool applicationFocused=true;
        void OnApplicationFocus(bool focused)
        {
            applicationFocused=focused;
            if(!focused){editing?.CancelEdit();pendingUi=null;ReleaseInput();}
            else if(makerWindow&&editing!=null&&editing.Allowed){editing.BeginEdit();ResetFields();RegisterWindowInput();}
        }
        void OnDisable(){StopSession();}
        void OnDestroy()
        {
            registration?.Dispose();registration=null;StopSession();
            harmony?.UnpatchSelf();MakerAPI.RegisterCustomSubCategories-=RegisterMakerControls;CharacterApi.CharacterReloaded-=CharacterReloaded;
            if(Instance==this)Instance=null;
        }

        sealed class ChangeToken
        {
            internal RoundedLimbController Controller;internal int Generation;internal bool Async,Finished,Entered,ReloadBody;
        }
        void InstallHooks()
        {
            var names=new HashSet<string>{"ChangeClothesGlovesNoAsync","ChangeClothesSocksNoAsync","ChangeClothesGlovesAsync","ChangeClothesSocksAsync",
                "SetClothesState","ChangeCoordinateType","Reload","ReloadNoAsync","ReloadAsync","Load","LoadNoAsync","LoadAsync","CreateBodyTexture","ChangeCustomBodyWithoutCustomTexture","ChangeAlphaMask",
                "SetBodyBaseMaterial","VisibleAddBodyLine","ChangeSettingBodyDetail","ChangeSettingBodyDetailPower","ChangeSettingSkinGlossPower"};
            foreach(var method in typeof(ChaControl).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
            {
                if(!names.Contains(method.Name))continue;
                bool async=typeof(IEnumerator).IsAssignableFrom(method.ReturnType);
                harmony.Patch(method,prefix:new HarmonyMethod(typeof(Plugin),nameof(BeforeChange)),
                    postfix:async?new HarmonyMethod(typeof(Plugin),nameof(AfterAsyncChange)):null,
                    finalizer:new HarmonyMethod(typeof(Plugin),nameof(AfterChange)));
            }
            harmony.Patch(AccessTools.Method(typeof(CvsClothes),"UpdateSelectClothes",new[]{typeof(string),typeof(Sprite),typeof(int)}),
                postfix:new HarmonyMethod(typeof(Plugin),nameof(MakerClothesSelected)){priority=Priority.Last});
            var external=Chainloader.PluginInfos.TryGetValue("com.deathweasel.bepinex.uncensorselector",out var uncensor)
                ? uncensor.Instance.GetType().Assembly.GetType("KK_Plugins.UncensorSelector+UncensorSelectorController") : null;
            if(external!=null)foreach(string name in new[]{"ReloadCharacterUncensor","ReloadCharacterBody","UpdateSkinColor","UpdateSkin"})
            {
                var method=AccessTools.Method(external,name);if(method==null)continue;
                bool async=typeof(IEnumerator).IsAssignableFrom(method.ReturnType);
                harmony.Patch(method,prefix:new HarmonyMethod(typeof(Plugin),nameof(BeforeExternalChange)),
                    postfix:async?new HarmonyMethod(typeof(Plugin),nameof(AfterAsyncChange)):null,
                    finalizer:new HarmonyMethod(typeof(Plugin),nameof(AfterChange)){priority=Priority.Last});
            }
        }
        static void MakerClothesSelected(CvsClothes __instance)
        {
            var p=Instance;if(p==null||!p.Allowed||GateApi.Current?.Kind!=GateApi.SessionKind.Maker)return;
            int kind=(int)__instance.typeClothes;if(kind!=4&&kind!=6)return;
            var cha=MakerAPI.GetCharacterControl();if(!p.Allows(cha))return;
            var c=cha.GetComponent<RoundedLimbController>();if(c==null)return;
            c.Refresh();
            if(c.Selected(kind))p.OpenMakerEditor(c); // Also runs for same-ID re-selection, where the game returns early.
        }
        void OpenMakerEditor(RoundedLimbController c)
        {
            BeginEditing(c);makerWindow=true;RegisterWindowInput();
        }
        static void BeforeExternalChange(CharaCustomFunctionController __instance,MethodBase __originalMethod,out ChangeToken __state)
            =>BeforeChange(__instance.ChaControl,__originalMethod,out __state);
        static void BeforeChange(ChaControl __instance,MethodBase __originalMethod,out ChangeToken __state)
        {
            __state=null;var p=Instance;if(p==null||!p.Allows(__instance))return;
            var c=__instance.GetComponent<RoundedLimbController>();if(c==null)return;
            __state=new ChangeToken{Controller=c,Generation=p.generation,Async=typeof(IEnumerator).IsAssignableFrom(((MethodInfo)__originalMethod).ReturnType),
                ReloadBody=__originalMethod.Name.StartsWith("Reload",StringComparison.Ordinal)||__originalMethod.Name=="Load"||__originalMethod.Name=="LoadAsync"||__originalMethod.Name=="LoadNoAsync"};
            if(!__state.Async)EnterChange(__state);
        }
        static void EnterChange(ChangeToken token)
        {
            var p=Instance;if(token.Entered||p==null||token.Controller==null||token.Generation!=p.generation||!p.Allows(token.Controller.ChaControl))return;
            token.Entered=true;
            if(token.ReloadBody)token.Controller.Restore();
            ++token.Controller.ChangeDepth;
        }
        static void AfterAsyncChange(ref IEnumerator __result,ChangeToken __state)
        {if(__state!=null)__result=Wrap(__result,__state);}
        static IEnumerator Wrap(IEnumerator original,ChangeToken token)
        {
            bool complete=false;
            EnterChange(token); // Iterator creation alone must not leave the controller permanently busy.
            try{if(original!=null)while(original.MoveNext())yield return original.Current;complete=true;}
            finally{try{(original as IDisposable)?.Dispose();}finally{FinishChange(token,complete);}}
        }
        static Exception AfterChange(Exception __exception,ChangeToken __state)
        {
            if(__state!=null&&(!__state.Async||__exception!=null))FinishChange(__state,__exception==null);
            return __exception;
        }
        static void FinishChange(ChangeToken token,bool successful)
        {
            if(token.Finished)return;token.Finished=true;
            var p=Instance;var c=token.Controller;
            if(p==null||c==null||!token.Entered||token.Generation!=p.generation)return;
            c.ChangeDepth=Math.Max(0,c.ChangeDepth-1);
            if(successful&&c.ChangeDepth==0&&p.Allows(c.ChaControl))c.Refresh();
        }
    }
}
