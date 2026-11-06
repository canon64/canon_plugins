using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;
using MainGameVoiceFaceEventBridge;

namespace MainGameSyringe
{
 public sealed partial class Plugin
 {
  ConfigEntry<bool> _voiceEnabled,_voiceSubtitles,_preparedVoiceEnabled,_insertedVoiceEnabled,_injectedVoiceEnabled;
  ConfigEntry<float> _voiceVolume;
  int _preparedVoiceIndex;
  string _voiceStatus="各Audioフォルダのmanifest.jsonを操作時に読み込みます。";
  readonly Dictionary<VoiceCueEvent,string> _lastVoice=new Dictionary<VoiceCueEvent,string>();
  readonly Dictionary<ChaControl,VoicePlayback> _voicePlaying=new Dictionary<ChaControl,VoicePlayback>();
  MethodInfo _subtitleShow;Type _subtitleBackend;
  sealed class VoicePlayback {public GameObject Object;public AudioSource Source;public AudioClip Clip;public Coroutine Finish;public IDisposable DildoVoicePriority,GameVoiceGuard;public bool LipSyncBound,MouthFixed;}
  Func<Action,IDisposable> _beginSyringeDialogue;
  bool _syringeGuardResolved;
  IDisposable BeginGameVoiceGuard()
  {
   if(!_syringeGuardResolved)
   {
    var api=Type.GetType("MainGameVoiceFaceEventBridge.PublicApi, MainGameVoiceFaceEventBridge",false);
    var method=api?.GetMethod("BeginSyringeDialogue",BindingFlags.Public|BindingFlags.Static);
    if(method!=null)_beginSyringeDialogue=(Func<Action,IDisposable>)Delegate.CreateDelegate(typeof(Func<Action,IDisposable>),method);
    _syringeGuardResolved=true;
   }
   return _beginSyringeDialogue?.Invoke(StopVoices);
  }
  Func<IDisposable> _acquireDildoVoicePriority;
  Func<bool> _interruptBridgeVoices;
  bool _bridgeInterruptResolved;
  void InterruptOtherVoices()
  {
   if(!_bridgeInterruptResolved)
   {
    var api=Type.GetType("MainGameVoiceFaceEventBridge.PublicApi, MainGameVoiceFaceEventBridge",false);
    var method=api?.GetMethod("InterruptAllVoicesForSyringe",BindingFlags.Public|BindingFlags.Static);
    if(method!=null)_interruptBridgeVoices=(Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>),method);
    _bridgeInterruptResolved=true;
   }
   // Native voices must still stop when the optional external-voice bridge is unavailable.
   if(_interruptBridgeVoices==null||!_interruptBridgeVoices())Manager.Voice.StopAll(false);
  }
  IDisposable AcquireDildoVoicePriority()
  {
   if(_acquireDildoVoicePriority==null)
   {
    var type=Type.GetType("MainGameVrDildo.Plugin, MainGameVrDildo",false);
    if(type==null)return null;
    var method=type.GetMethod("AcquireSyringeVoicePriority",BindingFlags.Public|BindingFlags.Static);
    if(method==null){Logger.LogWarning("ディルド音声の抑止APIがありません。MainGameVrDildoを更新してください。");return null;}
    _acquireDildoVoicePriority=(Func<IDisposable>)Delegate.CreateDelegate(typeof(Func<IDisposable>),method);
   }
   return _acquireDildoVoicePriority();
  }
  void BindVoiceSettings()
  {
   _voiceEnabled=Config.Bind("Voice","Enabled",true,"用意・刺入・投与時のユーザー作成WAVを再生する。");
   _preparedVoiceEnabled=Config.Bind("Voice","PreparedEnabled",true,"用意時の台詞を再生する。台詞全体の有効設定もONのときに再生する。");
   _insertedVoiceEnabled=Config.Bind("Voice","InsertedEnabled",true,"刺入時の台詞を再生する。台詞全体の有効設定もONのときに再生する。");
   _injectedVoiceEnabled=Config.Bind("Voice","InjectedEnabled",true,"投与時の台詞を再生する。台詞全体の有効設定もONのときに再生する。");
   _voiceSubtitles=Config.Bind("Voice","Subtitles",true,"対応する台詞を既存字幕APIへ渡す。");
   _voiceVolume=NumberSetting("Voice","Volume",1,0,1,"注射器の台詞音量（0～1）");
  }
  void PlayPreparedVoice()
  {
   if(_actors.Count==0)return;
   _preparedVoiceIndex=Math.Max(0,Math.Min(_preparedVoiceIndex,_actors.Count-1));
   PlayVoiceCue(VoiceCueEvent.Prepared,_actors[_preparedVoiceIndex]);
  }
  bool IsVoiceCueEnabled(VoiceCueEvent cue)
  {
   if(!_voiceEnabled.Value)return false;
   switch(cue)
   {
    case VoiceCueEvent.Prepared:return _preparedVoiceEnabled.Value;
    case VoiceCueEvent.Inserted:return _insertedVoiceEnabled.Value;
    case VoiceCueEvent.Injected:return _injectedVoiceEnabled.Value;
    default:return false;
   }
  }
  void PlayVoiceCue(VoiceCueEvent cue,ChaControl actor)
  {
   if(!Allowed||!IsVoiceCueEnabled(cue)||!IsInjectionTarget(actor))return;
   try
   {
    string directory=Path.Combine(Path.GetDirectoryName(Info.Location),"Audio",cue.ToString());
    string manifest=Path.Combine(directory,"manifest.json");
    if(!File.Exists(manifest)){_voiceStatus=cue+": manifest.jsonがありません。";return;}
    var entries=VoiceCueCatalog.Parse(File.ReadAllText(manifest),directory);
    entries.RemoveAll(entry=>!File.Exists(entry.FilePath));
    _lastVoice.TryGetValue(cue,out var last);
    var entry=VoiceCueCatalog.Pick(entries,last,count=>UnityEngine.Random.Range(0,count));
    if(entry==null){_voiceStatus=cue+": 再生可能なWAVがありません。";return;}
    if(!WavFileLoader.TryLoadPcm(entry.FilePath,out var pcm,out var error))throw new InvalidDataException(error);
    // Revalidate before touching the actor; no worker, deferred search, or input polling.
    if(!Allowed||!IsInjectionTarget(actor))return;
    StopVoices();
    var playback=new VoicePlayback();_voicePlaying.Add(actor,playback);
    playback.Clip=WavFileLoader.CreateClip(pcm);
    playback.Object=new GameObject("Syringe dialogue");
    playback.Object.transform.SetParent(actor.objHeadBone!=null?actor.objHeadBone.transform:actor.transform,false);
    playback.Source=playback.Object.AddComponent<AudioSource>();
    playback.Source.playOnAwake=false;playback.Source.loop=false;playback.Source.dopplerLevel=0;
    playback.Source.spatialBlend=.8f;playback.Source.volume=_voiceVolume.Value;
    playback.Source.clip=playback.Clip;
    InterruptOtherVoices();
    playback.GameVoiceGuard=BeginGameVoiceGuard();
    playback.DildoVoicePriority=AcquireDildoVoicePriority();
    playback.MouthFixed=actor.GetMouthFixed();
    playback.LipSyncBound=true;
    actor.SetLipSync(playback.Source);
    actor.ChangeMouthFixed(false);
    playback.Source.Play();
    _lastVoice[cue]=entry.FilePath;
    _voiceStatus=cue+": "+Path.GetFileName(entry.FilePath)+" / "+entry.Dialogue;
    ShowVoiceSubtitle(entry.Dialogue,playback.Clip.length);
    playback.Finish=StartCoroutine(FinishVoice(actor,playback,playback.Clip.length));
   }
   catch(Exception ex){StopActorVoice(actor);_voiceStatus=cue+": 音声を再生できません: "+ex.Message;Logger.LogWarning(_voiceStatus);}
  }
  IEnumerator FinishVoice(ChaControl actor,VoicePlayback playback,float seconds)
  {
   yield return new WaitForSecondsRealtime(seconds);
   if(_voicePlaying.TryGetValue(actor,out var current)&&ReferenceEquals(current,playback))
   {playback.Finish=null;StopActorVoice(actor);}
  }
  void StopActorVoice(ChaControl actor)
  {
   if(!_voicePlaying.TryGetValue(actor,out var playback))return;
   _voicePlaying.Remove(actor);
   try
   {
    if(playback.Finish!=null)StopCoroutine(playback.Finish);
    if(playback.Source!=null)playback.Source.Stop();
    if(playback.LipSyncBound&&actor!=null)
    {
     try{actor.SetLipSync(null);}
     finally{actor.ChangeMouthFixed(playback.MouthFixed);}
    }
    if(playback.Object!=null)Destroy(playback.Object);
    if(playback.Clip!=null)Destroy(playback.Clip);
   }
   finally
   {
    try{playback.DildoVoicePriority?.Dispose();playback.DildoVoicePriority=null;}
    finally{playback.GameVoiceGuard?.Dispose();playback.GameVoiceGuard=null;}
   }
  }
  void StopVoices()
  {
   foreach(var actor in new List<ChaControl>(_voicePlaying.Keys))StopActorVoice(actor);
  }
  void ShowVoiceSubtitle(string text,float seconds)
  {
   if(!_voiceSubtitles.Value||string.IsNullOrWhiteSpace(text)||!Allowed)return;
   try
   {
    if(_subtitleShow==null)
    {
     var api=Type.GetType("MainGameSubtitleCore.SubtitleApi, MainGameSubtitleCore",false);
     _subtitleBackend=Type.GetType("MainGameSubtitleCore.SubtitleBackend, MainGameSubtitleCore",false);
     if(api==null||_subtitleBackend==null)return;
     _subtitleShow=api.GetMethod("TryShow",new[]{typeof(string),_subtitleBackend,typeof(string),typeof(float)});
    }
    _subtitleShow?.Invoke(null,new object[]{"<color=#FF7ACDFF>"+text+"</color>",Enum.Parse(_subtitleBackend,"Auto"),"StackFemale",Mathf.Max(.1f,seconds)});
   }
   catch(Exception ex){Logger.LogWarning("注射器字幕: "+ex.Message);}
  }
  void DrawVoiceSettings()
  {
   bool enabled=GUILayout.Toggle(_voiceEnabled.Value,"用意・刺入・投与の台詞を再生");if(enabled!=_voiceEnabled.Value)_voiceEnabled.Value=enabled;
   bool prepared=GUILayout.Toggle(_preparedVoiceEnabled.Value,"用意時の台詞を再生");if(prepared!=_preparedVoiceEnabled.Value)_preparedVoiceEnabled.Value=prepared;
   bool inserted=GUILayout.Toggle(_insertedVoiceEnabled.Value,"刺入時の台詞を再生");if(inserted!=_insertedVoiceEnabled.Value)_insertedVoiceEnabled.Value=inserted;
   bool injected=GUILayout.Toggle(_injectedVoiceEnabled.Value,"投与時の台詞を再生");if(injected!=_injectedVoiceEnabled.Value)_injectedVoiceEnabled.Value=injected;
   bool subtitles=GUILayout.Toggle(_voiceSubtitles.Value,"台詞の字幕を表示");if(subtitles!=_voiceSubtitles.Value)_voiceSubtitles.Value=subtitles;
   EditNumber("台詞の音量 0～1",_voiceVolume);
   if(_actors.Count>0)
   {
    _preparedVoiceIndex=Math.Max(0,Math.Min(_preparedVoiceIndex,_actors.Count-1));
    if(GUILayout.Button("用意時の話者: 女性"+(_preparedVoiceIndex+1)+" / 切替"))_preparedVoiceIndex=(_preparedVoiceIndex+1)%_actors.Count;
   }
   UiLabel(_voiceStatus);
  }
 }
}
