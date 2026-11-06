using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using CanonGate;
using HarmonyLib;
using UnityEngine;
using MainGameUiInputCapture;
using MainGameTransformGizmo;
using MainGameUnifiedUi;
using Spiral=MainGameSpiralEyes.PublicApi;

namespace MainGameSyringe
{
 [BepInPlugin(Guid,"MainGameSyringe","1.5.7")]
 [BepInProcess("KoikatsuSunshine")]
 [BepInProcess("KoikatsuSunshine_VR")]
 [BepInDependency("canon.gate","2.3.0")]
 [BepInDependency("com.kks.maingame.spiraleyes","1.5.0")]
 [BepInDependency("com.kks.maingame.transformgizmo",BepInDependency.DependencyFlags.SoftDependency)]
 [BepInDependency("com.kks.maingame.uiinputcapture")]
 [BepInDependency("com.kks.maingame.unifiedui","0.2.0")]
 public sealed partial class Plugin:BaseUnityPlugin
 {
  public const string Guid="com.kks.maingame.syringe";
  SessionLifecycle _registration;GateApi.Permit _permit;bool _session,_focused=true,_vr;
  ConfigEntry<bool> _enabled,_diagnostics,_showGizmo;
  ConfigEntry<float> _gripRadius,_scale,_gizmoSize;
  ConfigEntry<float> _fadeIn,_fadeOut;
  ConfigEntry<Vector3> _gripPosition,_gripAngles;
  readonly InjectionPolicy<ChaControl> _policy=new InjectionPolicy<ChaControl>();
  readonly List<ChaControl> _actors=new List<ChaControl>();
  readonly List<BodySegment> _segments=new List<BodySegment>();
  readonly List<ActorLifetime> _lifetimes=new List<ActorLifetime>();
  GameObject _model;Transform _tip;TransformGizmo _gizmo;
  const string SyringeTabId="syringe";
  string _status="次の許可されたHで使用できます。";
  bool Allowed=>_session&&_permit.IsValid&&_enabled.Value&&isActiveAndEnabled;
  bool IsInjectionTarget(ChaControl actor)=>actor!=null&&actor.sex==1&&_actors.Contains(actor);
  sealed class BodySegment {public ChaControl Actor;public Transform A,B;public float Radius;public bool Thigh,Neck;public string Label=>Neck?"首":Thigh?"太もも":"腕";}

  void Awake()
  {
   _enabled=Config.Bind("General","Enabled",true,"注射器の有効／無効。OFFで注射器と注射由来の螺旋目を解除。");
   _diagnostics=Config.Bind("Diagnostics","Enabled",false,"操作・開始・終了時だけ診断ログを出す。");
   _showGizmo=Config.Bind("Desktop","ShowGizmo",false,"注射器の移動・回転ギズモを表示。OFFで確実に非表示にする。");
   _gizmoSize=Config.Bind("Desktop","GizmoSize",.2f,new ConfigDescription("注射器ギズモの表示倍率",new AcceptableValueRange<float>(.2f,1f)));
   _fadeIn=Config.Bind("Effect","FadeInSeconds",30f,new ConfigDescription("完全透明から完全不透明になる秒数。100段階。次の注射から反映。",new AcceptableValueRange<float>(10f,300f)));
   _fadeOut=Config.Bind("Effect","FadeOutSeconds",600f,new ConfigDescription("最大の濃さから完全透明になる秒数。100段階。次の注射から反映。",new AcceptableValueRange<float>(10f,3600f)));
   _gripRadius=Config.Bind("Interaction","GripRadius",.08f,new ConfigDescription("握れる距離（m）",new AcceptableValueRange<float>(.02f,.3f)));
   _scale=Config.Bind("Model","Scale",1f,new ConfigDescription("注射器の大きさ",new AcceptableValueRange<float>(.25f,3f)));
   _gripPosition=Config.Bind("VR","GripPosition",new Vector3(0,-.015f,.06f),"手に対する位置（m）");
   _gripAngles=Config.Bind("VR","GripRotation",Vector3.zero,"手に対する角度（度）。針先は注射器の前方。");
   BindInsertionSettings();
   BindContactSettings();
   BindDoseSettings();
   BindVoiceSettings();
   BindInjectionSoundSettings();
   Config.SettingChanged+=SettingsChanged;
   _registration=new SessionLifecycle(Guid,BeginSession,EndSession);
   UnifiedUiApi.RegisterTabAfter(SyringeTabId,"注射器",DrawSyringeTab,"vr-neck-chain",82);
  }
  void BeginSession()
  {
   var context=GateApi.Current;
   if(_session||context==null||context.Kind!=GateApi.SessionKind.H)return;
   _permit=GateApi.Capture(Guid);if(!_permit.IsValid)return;
   _session=true;
   try
   {
    _vr=IsVrActive();
    var participants=AccessTools.Field(context.Scene.GetType(),"lstFemale")?.GetValue(context.Scene) as IEnumerable;
    if(participants!=null)foreach(var value in participants)if(value is ChaControl actor&&actor!=null&&actor.sex==1&&!_actors.Contains(actor))_actors.Add(actor);
    foreach(var actor in _actors){CacheBody(actor);CacheSkin(actor);var life=actor.gameObject.AddComponent<ActorLifetime>();life.Actor=actor;life.Released=ActorDestroyed;_lifetimes.Add(life);}
    Spiral.OwnerOverlayReleased+=OverlayReleased;
    _status="基本プレイバックバーの「注射器」で呼び出してください。";
    Trace("H開始: females="+_actors.Count+" vr="+_vr);
   }
   catch(Exception ex){EndSession();Logger.LogWarning("注射器の開始失敗: "+ex.Message);throw;}
  }
  void Activate()
  {
   if(!Allowed||_model!=null)return;
   try
   {
    CreateModel();PlaceInFront();
    if(_vr)SubscribeVr();else SyncGizmoVisibility();
    _status=_vr?"グリップで握る → 腕・太もも・首に針先 → トリガー":"移動・回転で針先を腕・太もも・首に当て、注射。";
    PlayPreparedVoice();
   }
   catch(Exception ex){Deactivate();_status="注射器を生成できません: "+ex.Message;Logger.LogWarning(_status);}
  }
  void Deactivate()
  {
   CancelAllFades();
   Despawn();
   foreach(var actor in _policy.Clear())Spiral.EndOverlay(Guid,actor);
  }
  public bool IsSpawned()=>_model!=null;
  public void ToggleSpawn()
  {
   if(!Allowed||!InteractionFocused)return;
   if(_model==null)Activate();
   else{Despawn();_status="注射器を削除しました。再び「注射器」で呼び出せます。";}
  }
  void Despawn()
  {
   StopVoices();
   DetachInsertion(false);UnsubscribeVr();ReleaseGrab();_policy.ResetInput();DestroyModel();
   _probeInitialized=false;_reinsertion.Reset();_withdrawnBone=null;
  }
  void EndSession()
  {
   _session=false;_permit=default;
   Spiral.OwnerOverlayReleased-=OverlayReleased;
   Deactivate();
   _lastVoice.Clear();_preparedVoiceIndex=0;
   foreach(var life in _lifetimes)if(life!=null){life.Released=null;Destroy(life);}
   _lifetimes.Clear();_segments.Clear();_skinMeshes.Clear();_failedSkins.Clear();_actors.Clear();_status="次の許可されたHで使用できます。";
  }
  void SettingsChanged(object sender,SettingChangedEventArgs args)
  {
   _numericText.Clear();
   if(!_session)return;
   if(args.ChangedSetting==_maximumDoses||args.ChangedSetting==_maximumOrbit||args.ChangedSetting==_orbitSpeed)RefreshDoseSettings();
   if(args.ChangedSetting==_voiceEnabled&&!_voiceEnabled.Value)StopVoices();
   if(args.ChangedSetting==_injectionSoundEnabled&&!_injectionSoundEnabled.Value)StopInjectionSound();
   if(args.ChangedSetting==_injectionSoundVolume&&_injectionSoundSource!=null)_injectionSoundSource.volume=_injectionSoundVolume.Value;
   if(args.ChangedSetting==_voiceVolume)foreach(var playback in _voicePlaying.Values)if(playback.Source!=null)playback.Source.volume=_voiceVolume.Value;
   if(args.ChangedSetting==_enabled){if(!_enabled.Value)Deactivate();else _status="「注射器」で呼び出してください。";}
   if(_model!=null&&args.ChangedSetting==_scale)ApplyModelScale();
   if(args.ChangedSetting==_gripPosition||args.ChangedSetting==_gripAngles)UpdateHeldPose();
   if(args.ChangedSetting==_scale||args.ChangedSetting==_insertionDepth)RepositionInsertion();
   if(args.ChangedSetting==_pullDistance||args.ChangedSetting==_scale)ArmReturnBoundary();
   if(args.ChangedSetting==_armRadius||args.ChangedSetting==_thighRadius||args.ChangedSetting==_neckRadius)foreach(var segment in _segments)segment.Radius=segment.Neck?_neckRadius.Value:segment.Thigh?_thighRadius.Value:_armRadius.Value;
   if(_model!=null&&(args.ChangedSetting==_pusherStroke||args.ChangedSetting==_injectionSeconds||args.ChangedSetting==_remainingLiquid))RebuildInjectionAnimation();
   if(args.ChangedSetting==_showGizmo||args.ChangedSetting==_gizmoSize)SyncGizmoVisibility();
  }
  void ReleaseActor(ChaControl actor)
  {
   if(ReferenceEquals(actor,null))return;
   CancelFade(actor);
   if(ReferenceEquals(_insertedActor,actor))DetachInsertion(_heldController!=null);
   _policy.Release(actor);Spiral.EndOverlay(Guid,actor);
  }
  void OverlayReleased(string owner,ChaControl actor){if(owner==Guid){CancelFade(actor);_policy.Release(actor);}}
  void ActorDestroyed(ChaControl actor)
  {
   StopActorVoice(actor);
   ReleaseActor(actor);_actors.Remove(actor);_segments.RemoveAll(s=>ReferenceEquals(s.Actor,actor));
  }
  void CacheBody(ChaControl actor)
  {
   var bones=new Dictionary<string,Transform>(StringComparer.Ordinal);
   if(actor.objBodyBone==null){Logger.LogWarning("注射対象の身体骨格がありません。");return;}
   foreach(var bone in actor.objBodyBone.GetComponentsInChildren<Transform>(true))bones[bone.name]=bone;
   AddSegment(actor,bones,"cf_j_neck","cf_j_head",_neckRadius.Value);
   foreach(var side in new[]{"L","R"})
   {
    AddSegment(actor,bones,"cf_j_arm00_"+side,"cf_j_forearm01_"+side,_armRadius.Value);
    AddSegment(actor,bones,"cf_j_forearm01_"+side,"cf_j_hand_"+side,_armRadius.Value);
    AddSegment(actor,bones,"cf_j_thigh00_"+side,"cf_j_leg01_"+side,_thighRadius.Value);
   }
  }
  void AddSegment(ChaControl actor,Dictionary<string,Transform> bones,string a,string b,float radius)
  {
   if(bones.TryGetValue(a,out var first)&&bones.TryGetValue(b,out var second))_segments.Add(new BodySegment{Actor=actor,A=first,B=second,Radius=radius,Thigh=a.Contains("thigh"),Neck=a=="cf_j_neck"});
   else Logger.LogWarning("注射判定ボーンがありません: "+a+" / "+b);
  }
  void Inject()
  {
   if(!Allowed||!InteractionFocused||_model==null||_tip==null)return;
   if(Refilling){_status="薬液を補充中です。終了後に投与してください。";return;}
   var target=_insertedActor;
   if(_insertedBone==null||!IsInjectionTarget(target)){_status="先に針をH参加女性の腕・太もも・首へ刺して固定してください。";return;}
   if(_spent){_status="薬液が空です。「薬液を補充」を押してください。";return;}
   bool existing=_policy.IsInjected(target);
   if(!Spiral.PreparePupilOrbit(Guid,target)){_status="瞳の円運動を準備できません。GPU素材・gate許可を確認してください。";return;}
   if(!existing&&!Spiral.BeginFadingOverlay(Guid,target)){Spiral.EndPupilOrbit(Guid,target);_status="螺旋瞳を適用できません。gate許可・有効設定を確認してください。";return;}
   if(!StartFade(target)){if(!existing)Spiral.EndOverlay(Guid,target);_status="効果の追加に失敗しました。薬液は消費していません。";return;}
   _policy.Commit(target);PlayInjection();_status="投与成功 / 有効な注射 "+_doses[target].Count+" 回。この投与は "+_fadeIn.Value.ToString("0")+"秒で濃くなり、続く"+_fadeOut.Value.ToString("0")+"秒で消えます。";Trace("注射成功: actor="+target.GetInstanceID());
   PlayVoiceCue(VoiceCueEvent.Injected,target);
  }
  void PlaceInFront()
  {
   if(_model==null)return;DetachInsertion(false);ReleaseGrab();
   var camera=Camera.main;
   if(camera==null){_status="配置用カメラがありません。";return;}
   _model.transform.position=camera.transform.TransformPoint(_spawnOffset.Value);
   _model.transform.rotation=camera.transform.rotation;
  }
  void AttachGizmo()
  {
   if(_model==null)return;
   _gizmo=TransformGizmoApi.Attach(_model);
   if(_gizmo==null)throw new InvalidOperationException("移動ギズモがgateで許可されていません。");
   _gizmo.DragStateChanged+=GizmoDragChanged;
   _gizmo.SetSizeMultiplier(_gizmoSize.Value);_gizmo.SetMode(GizmoMode.Move);_gizmo.SetVisible(true);TransformGizmoApi.EnableCameraInputCaptureOnDrag(_gizmo);
  }
  void SyncGizmoVisibility()
  {
   if(_vr||!Allowed||_model==null||!_showGizmo.Value||_insertedBone!=null)
   {
    if(_gizmo!=null){_gizmo.SetVisible(false);TransformGizmoApi.DisableCameraInputCaptureOnDrag(_gizmo);}
    return;
   }
   try
   {
    if(_gizmo==null)AttachGizmo();
    else{_gizmo.SetSizeMultiplier(_gizmoSize.Value);_gizmo.SetVisible(true);TransformGizmoApi.EnableCameraInputCaptureOnDrag(_gizmo);}
   }
   catch(Exception ex){_status="ギズモを表示できません: "+ex.Message;Logger.LogWarning(_status);}
  }
  void DrawSyringeTab()
  {
   if(!_session||!_permit.IsValid||!InteractionFocused)return;
   _tabScroll=GUILayout.BeginScrollView(_tabScroll);
   GUILayout.BeginHorizontal();GUILayout.BeginVertical(GUILayout.Width(620),GUILayout.ExpandWidth(false));
   try{DrawSyringeContent();}finally{GUILayout.EndVertical();GUILayout.FlexibleSpace();GUILayout.EndHorizontal();GUILayout.EndScrollView();}
  }
  void DrawSyringeContent()
  {
   // The UnifiedUi/PlaybackBar host owns the content rectangle and pointer capture.
   bool enabled=GUILayout.Toggle(_enabled.Value,"注射器を有効にする");
   if(enabled!=_enabled.Value)_enabled.Value=enabled;
   bool diagnostics=GUILayout.Toggle(_diagnostics.Value,"操作ログを保存する（ON / OFF）");
   if(diagnostics!=_diagnostics.Value)_diagnostics.Value=diagnostics;
   if(_diagnostics.Value)UiLabel("ログ: "+DiagnosticLogPath);
   UiLabel(_status);
   if(!_enabled.Value)return;
   DrawContactSettings();
   DrawInjectionSoundSettings();
   DrawDoseSettings();
   DrawVoiceSettings();
   UiLabel("螺旋の濃さ: 0（完全透明）～100（完全不透明） / 各100段階");
   GUILayout.BeginHorizontal();GUILayout.Label("濃くなる秒数",GUILayout.Width(110));float fadeIn=GUILayout.HorizontalSlider(_fadeIn.Value,10,300);GUILayout.Label(fadeIn.ToString("0.##"),GUILayout.Width(45));GUILayout.EndHorizontal();if(fadeIn!=_fadeIn.Value)_fadeIn.Value=(float)Math.Round(fadeIn,2);
   GUILayout.BeginHorizontal();GUILayout.Label("消えていく秒数",GUILayout.Width(110));float fadeOut=GUILayout.HorizontalSlider(_fadeOut.Value,10,3600);GUILayout.Label(fadeOut.ToString("0.##"),GUILayout.Width(45));GUILayout.EndHorizontal();if(fadeOut!=_fadeOut.Value)_fadeOut.Value=(float)Math.Round(fadeOut,2);
   UiLabel(GetPrimaryActionHint());
   if(GUILayout.Button("注射器（基本ボタンと同じ操作）"))PrimaryAction();
   if(_model!=null&&GUILayout.Button("注射器を削除"))DeleteSyringe();
   if(_model!=null)
   {
    if(GUILayout.Button("手元に置き直す"))PlaceInFront();
    if(!_vr)
    {
     if(GUILayout.Button("注射"))Inject();
    }
    if(GUILayout.Button("薬液を補充"))Refill();
    if(GUILayout.Button("注射による螺旋目を解除")){CancelAllFades();foreach(var actor in _policy.ClearInjected())Spiral.EndOverlay(Guid,actor);}
   }
   DrawNumericSettings();
  }
  void OnApplicationFocus(bool focused){_focused=focused;if(!focused&&!IsVrActive())ReleaseGrab();}
  GUIStyle _wrappedLabel;
  void UiLabel(string text)
  {
   if(_wrappedLabel==null)_wrappedLabel=new GUIStyle(GUI.skin.label){wordWrap=true};
   GUILayout.Label(text,_wrappedLabel,GUILayout.Width(610));
  }
  void OnDisable(){_registration?.Stop();EndSession();}
  void OnDestroy(){UnifiedUiApi.UnregisterTab(SyringeTabId);Config.SettingChanged-=SettingsChanged;_registration?.Dispose();_registration=null;EndSession();}
  string DiagnosticLogPath=>System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Info.Location),"MainGameSyringe.log");
  void Trace(string message)
  {
   if(!_diagnostics.Value)return;
   Logger.LogInfo(message);
   try{System.IO.File.AppendAllText(DiagnosticLogPath,DateTime.Now.ToString("o")+" "+message+Environment.NewLine,System.Text.Encoding.UTF8);}
   catch(Exception ex){Logger.LogWarning("注射器ログ保存: "+ex.Message);}
  }
 }
 public sealed class ActorLifetime:MonoBehaviour
 {
  internal ChaControl Actor;internal Action<ChaControl> Released;
  void OnDestroy(){var callback=Released;Released=null;callback?.Invoke(Actor);}
 }
}
