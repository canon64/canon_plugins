using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using CanonGate;
using HarmonyLib;
using UnityEngine;

namespace MainGameSpiralEyes
{
 [BepInPlugin(Guid,"MainGameSpiralEyes","1.6.2")]
 [BepInDependency("canon.gate","2.3.0")]
 [BepInProcess("KoikatsuSunshine")]
 [BepInProcess("KoikatsuSunshine_VR")]
 public sealed partial class Plugin : BaseUnityPlugin
 {
  public const string Guid="com.kks.maingame.spiraleyes";
  internal static Plugin Instance;
  sealed class Parameter {public Definition Def;public ConfigEntry<float> Value,Amplitude,Frequency,Phase;public ConfigEntry<int> Wave;}
  readonly Dictionary<string,Parameter> _parameters=new Dictionary<string,Parameter>(StringComparer.Ordinal);
  readonly List<ChaControl> _actors=new List<ChaControl>();
  readonly HashSet<ChaControl> _selectedActors=new HashSet<ChaControl>();
  readonly Dictionary<ChaControl,KeyValuePair<int,bool>> _selectionCache=new Dictionary<ChaControl,KeyValuePair<int,bool>>();
  PupilFollowBindings _followBindings;AssetBundle _followBundle,_followUtsBundle;
  HSessionClient _session;AssetBundle _bundle;Shader _shader;Harmony _harmony;
  GpuEyeTexture _left,_right;Texture2D _drawing,_staticDrawing;
  ConfigEntry<bool> _enabled,_animation,_original,_mirror,_diagnostics;
  ConfigEntry<int> _mode,_hand;ConfigEntry<Color> _colorA,_colorB,_tint;
  int _mainThread;bool _fallbackWarned;
  bool Allowed=>_session!=null&&_session.Allowed;
  string Folder=>Path.GetDirectoryName(Info.Location);

  void Awake()
  {
   Instance=this;_mainThread=System.Threading.Thread.CurrentThread.ManagedThreadId;
   _enabled=Config.Bind("General","Enabled",true,"許可されたHで追加の「canon 虹色螺旋目」を選択した場合だけ動かす。通常の目は変更しない。");
   _animation=Config.Bind("General","AnimationEnabled",true,"回転と色変化をGPUで動かす。OFFでは指定色の静止画。");
   _original=Config.Bind("General","UseOriginalDrawing",true,"最初に描いた3.5周のPNGを使用。falseのみ旧方式。周間隔・線幅・変形は旧方式専用。");
   _diagnostics=Config.Bind("Diagnostics","Enabled",false,"登録・H許可・人数・目素材数と、適用失敗時の詳細素材診断をイベント時だけログへ記録。");
   _tint=Config.Bind("色","OriginalDrawingTint",new Color(1,80f/255,235f/255),"アニメーションOFF・失敗時の静止画の色。");
   _mirror=Config.Bind("左右","MirrorLeft",false,"左目を鏡映する。");
   _hand=Config.Bind("形","Handedness",1,new ConfigDescription("旧方式の巻く向き。",new AcceptableValueList<int>(-1,1)));
   _mode=Config.Bind("色","ColorMode",0,new ConfigDescription("0=虹色、1=指定2色、2=単色",new AcceptableValueRange<int>(0,2)));
   _colorA=Config.Bind("色","ColorA",new Color(1,.1f,.2f),"アニメーションの指定色A");
   _colorB=Config.Bind("色","ColorB",new Color(.1f,.3f,1),"アニメーションの指定色B");
   var originalKeys=new HashSet<string>{"rotation","colorSpeed","colorCycles","hue","saturation","brightness","strength","glow","pulse","pulseSpeed"};
   foreach(var d in ParameterDefinitions.Load(Path.Combine(Folder,"parameters.json")))
   {
    string label=d.label+(originalKeys.Contains(d.key)?"":"（UseOriginalDrawing=falseの旧方式専用）");
    var p=new Parameter{Def=d};
    p.Value=Config.Bind(d.group,d.key,d.value,new ConfigDescription(label,new AcceptableValueRange<float>(d.min,d.max)));
    p.Amplitude=Config.Bind("変調."+d.key,"Amplitude",0f,"周期変化の振幅。0で停止。"+label);
    p.Frequency=Config.Bind("変調."+d.key,"Frequency",1f,new ConfigDescription("周期変化の回数/秒",new AcceptableValueRange<float>(0,20)));
    p.Phase=Config.Bind("変調."+d.key,"Phase",0f,"周期変化の位相（1で1周期）");
    p.Wave=Config.Bind("変調."+d.key,"Wave",0,new ConfigDescription("0=正弦、1=三角、2=のこぎり、3=滑らかな不規則波",new AcceptableValueRange<int>(0,3)));
    _parameters.Add(d.key,p);
   }
   _session=new HSessionClient(Guid,()=>isActiveAndEnabled,StartSession,StopSession);
   _harmony=new Harmony(Guid);
   Config.SettingChanged+=SettingsChanged;Trace("registered");
  }
  void StartSession(MonoBehaviour scene)
  {
   InstallEyeHooks();
   _fallbackWarned=false;
   var list=scene==null?null:AccessTools.Field(scene.GetType(),"lstFemale")?.GetValue(scene) as IEnumerable;
   if(list!=null)foreach(object item in list)if(item is ChaControl actor&&actor!=null&&actor.sex==1)_actors.Add(actor);
   Trace("gate permitted H; females="+_actors.Count);
   if(_actors.Count==0)Logger.LogWarning("SpiralEyes: permitted H has no female participants; no eyes changed.");
   if(_enabled.Value)EnableEffects();
  }
  void SettingsChanged(object sender,SettingChangedEventArgs args)
  {
   if(!Allowed)return;
   if(args.ChangedSetting==_diagnostics)return;
   if(args.ChangedSetting==_enabled||args.ChangedSetting==_animation)
   {if(!_enabled.Value)ClearForcedOverlays();ReleaseEffects();if(_enabled.Value)EnableEffects();return;}
   if(!_enabled.Value)return;
   if(_left!=null){WriteParameters(_left.Material,-1);WriteParameters(_right.Material,1);}
   else if(_staticDrawing!=null&&args.ChangedSetting==_tint)
   {CreateStaticDrawing();foreach(var actor in _actors)BindActor(actor);}
  }
  void LoadDrawing()
  {
   _drawing=new Texture2D(2,2,TextureFormat.RGBA32,false,true){name="canon original spiral",wrapMode=TextureWrapMode.Clamp};
   if(!ImageConversion.LoadImage(_drawing,File.ReadAllBytes(Path.Combine(Folder,"Resources","canon_spiral.png"))))
    throw new InvalidDataException("Original spiral PNG could not be loaded");
  }
  void EnableEffects()
  {
   if(!Allowed||!_enabled.Value||_actors.Count==0)return;
   foreach(var actor in _actors)
    try{BindActor(actor);}catch(Exception ex){Logger.LogWarning("SpiralEyes pupil-follow binding failed: "+ex.Message);}
   Trace("selected females="+_selectedActors.Count+"; eye material bindings="+(_followBindings?.Count??0));
  }
  void EnsureTextures()
  {
   if(_followBindings==null)
   {
    try
    {
     _followBundle=AssetBundle.LoadFromFile(Path.Combine(Folder,"Resources","pupilfollow.bundle"));
     var followShader=_followBundle?.LoadAsset<Shader>("assets/pupilfollow.shader");
     _followUtsBundle=AssetBundle.LoadFromFile(Path.Combine(Folder,"Resources","pupilfollow-kkutseye.bundle"));
     var utsShader=_followUtsBundle?.LoadAsset<Shader>("assets/kkutseye.shader");
     if(utsShader==null)throw new InvalidOperationException("KKUTSeye用の瞳追従素材がありません。");
     _followBindings=new PupilFollowBindings(followShader,utsShader);
    }
    catch{if(_followBundle!=null)_followBundle.Unload(false);if(_followUtsBundle!=null)_followUtsBundle.Unload(false);_followBundle=_followUtsBundle=null;throw;}
   }
   if(_drawing!=null)return;
   try
   {
    LoadDrawing();
    if(_animation.Value)
    {
     try
     {
      if(_bundle==null)_bundle=AssetBundle.LoadFromFile(Path.Combine(Folder,"Resources","spiraleyes.bundle"));
      if(_shader==null&&_bundle!=null)_shader=_bundle.LoadAsset<Shader>("assets/spiraleyes.shader");
      _left=new GpuEyeTexture(_shader,_drawing,m=>WriteParameters(m,-1));
      _right=new GpuEyeTexture(_shader,_drawing,m=>WriteParameters(m,1));
     }
     catch(Exception ex)
     {
      _left?.Dispose();_right?.Dispose();_left=_right=null;
      if(!_fallbackWarned){_fallbackWarned=true;Logger.LogWarning("SpiralEyes: animation unavailable; displaying original PNG. "+ex.Message);}
     }
    }
    if(_left==null)CreateStaticDrawing();
    Trace("textures ready; GPU="+(_left!=null));
   }
   catch{ReleaseEffects();throw;}
  }
  void CreateStaticDrawing()
  {
   if(_staticDrawing==null)_staticDrawing=new Texture2D(_drawing.width,_drawing.height,TextureFormat.RGBA32,false,true){wrapMode=TextureWrapMode.Clamp};
   var pixels=_drawing.GetPixels32();var tint=(Color32)_tint.Value;
   for(int i=0;i<pixels.Length;i++){pixels[i].r=tint.r;pixels[i].g=tint.g;pixels[i].b=tint.b;}
   _staticDrawing.SetPixels32(pixels);_staticDrawing.Apply(false);
  }
  void WriteParameters(Material material,int side)
  {
   foreach(var pair in _parameters){var p=pair.Value;material.SetVector("p_"+pair.Key,new Vector4(p.Value.Value,p.Amplitude.Value,p.Frequency.Value,p.Phase.Value));material.SetFloat("w_"+pair.Key,p.Wave.Value);}
   material.SetFloat("uOriginal",_original.Value?1:0);material.SetFloat("uAnimation",1);
   material.SetFloat("uSide",side);material.SetFloat("uMirror",_mirror.Value?1:0);material.SetFloat("uHand",_hand.Value);material.SetFloat("uMode",_mode.Value);
   material.SetColor("uA",_colorA.Value);material.SetColor("uB",_colorB.Value);material.SetColor("uTint",_tint.Value);
  }
  void BindActor(ChaControl actor)
  {
   if(!Allowed||!_enabled.Value||actor==null||actor.rendEye==null)return;
   if(!HasForcedOverlay(actor)&&!IsSpiralSelected(actor))
   {
    if(_selectedActors.Remove(actor))
    {
     UnbindOrbit(actor);foreach(var renderer in actor.rendEye)_followBindings?.Restore(renderer);
     if(_selectedActors.Count==0)ReleaseEffects();
    }
    return;
   }
   EnsureTextures();_selectedActors.Add(actor);
   bool selected=IsSpiralSelected(actor);
   try
   {
    for(int side=0;side<actor.rendEye.Length;side++)
    {
     var renderer=actor.rendEye[side];var texture=_left!=null?(Texture)(side==0?_left.Texture:_right.Texture):_staticDrawing;
     _followBindings.Apply(renderer,texture,HasDollLayer(actor)?DollLayerPosition(actor):Vector2.zero,EffectiveOpacity(actor),side,selected);
    }
    BindOrbit(actor);
   }
   catch(Exception ex)
   {
    LogBindingFailureOnce(actor,ex);
    UnbindOrbit(actor);foreach(var renderer in actor.rendEye)_followBindings?.Restore(renderer);
    _selectedActors.Remove(actor);throw;
   }
  }
  bool IsSpiralSelected(ChaControl actor)
  {
   int id=actor.fileStatus.eyesPtn;
   if(_selectionCache.TryGetValue(actor,out var cached)&&cached.Key==id)return cached.Value;
   var info=actor.lstCtrl.GetInfo(ChaListDefine.CategoryNo.cha_eyeset,id);
   bool selected=info!=null&&SpiralSelection.Matches(info.GetInfo(ChaListDefine.KeyType.EpsTexAB),info.GetInfo(ChaListDefine.KeyType.EpsTex));
   _selectionCache[actor]=new KeyValuePair<int,bool>(id,selected);
   return selected;
  }
  void ReleaseEffects()
  {
   UnbindAllOrbits();
   _followBindings?.Restore();_followBindings=null;_selectedActors.Clear();_left?.Dispose();_right?.Dispose();_left=_right=null;
   if(_followBundle!=null)_followBundle.Unload(false);_followBundle=null;
   if(_followUtsBundle!=null)_followUtsBundle.Unload(false);_followUtsBundle=null;
   if(_drawing!=null)Destroy(_drawing);if(_staticDrawing!=null)Destroy(_staticDrawing);
   _drawing=_staticDrawing=null;
   if(_bundle!=null)_bundle.Unload(false);_bundle=null;_shader=null;
  }
  void StopSession(){RemoveEyeHooks();ClearForcedOverlays();ReleaseEffects();_actors.Clear();_selectionCache.Clear();_bindingFailuresLogged.Clear();Trace("H stopped; all eye bindings restored");}
  void OnDisable(){_session?.Stop();}
  void OnDestroy()
  {
   if(Config!=null)Config.SettingChanged-=SettingsChanged;
   _session?.Dispose();ReleaseEffects();_harmony?.UnpatchSelf();if(Instance==this)Instance=null;
  }
  void Trace(string message){if(_diagnostics?.Value==true)Logger.LogInfo("SpiralEyes: "+message);}
  public static bool SetParameter(string key,float value)
  {
   var p=Instance;if(p==null)return false;
   if(System.Threading.Thread.CurrentThread.ManagedThreadId!=p._mainThread)throw new InvalidOperationException("Use the Unity main thread");
   if(!p.Allowed||float.IsNaN(value)||float.IsInfinity(value)||!p._parameters.TryGetValue(key,out var parameter))return false;
   parameter.Value.Value=Mathf.Clamp(value,parameter.Def.min,parameter.Def.max);return true;
  }
 }
}
