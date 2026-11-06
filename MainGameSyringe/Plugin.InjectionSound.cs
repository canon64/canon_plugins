using System;
using System.Collections;
using BepInEx.Configuration;
using UnityEngine;
namespace MainGameSyringe
{
 public sealed partial class Plugin
 {
  ConfigEntry<bool> _injectionSoundEnabled;
  ConfigEntry<float> _injectionSoundVolume;
  AudioSource _injectionSoundSource;AudioClip _injectionSoundClip;Coroutine _injectionSoundFinish;
  void BindInjectionSoundSettings()
  {
   _injectionSoundEnabled=Config.Bind("InjectionSound","Enabled",true,"注射は01、補充は02の効果音を押し子の動く3秒間に再生。台詞WAVとは独立。");
   _injectionSoundVolume=NumberSetting("InjectionSound","Volume",.5f,0,1,"投与の液体音の音量（0～1）");
  }
  void DrawInjectionSoundSettings()
  {
   GUILayout.Label("押し子の動作：注射・補充とも3秒（前後の動作は別）");
   bool sound=GUILayout.Toggle(_injectionSoundEnabled.Value,"注射・補充の効果音（01 / 02）");
   if(sound!=_injectionSoundEnabled.Value)_injectionSoundEnabled.Value=sound;
   EditNumber("投与の効果音量 0～1",_injectionSoundVolume);
  }
  void PlayInjectionSound()
  {PlaySyringeEffect(false);}
  void PlayRefillSound()
  {PlaySyringeEffect(true);}
  void PlaySyringeEffect(bool refill)
  {
   if(!Allowed||_model==null||!_injectionSoundEnabled.Value)return;
   try
   {
    StopInjectionSound();
    _injectionSoundClip=SyringeEffectAudio.Create(refill);
    _injectionSoundSource=_model.AddComponent<AudioSource>();
    _injectionSoundSource.playOnAwake=false;_injectionSoundSource.loop=false;_injectionSoundSource.dopplerLevel=0;
    _injectionSoundSource.spatialBlend=.65f;_injectionSoundSource.volume=_injectionSoundVolume.Value;
    _injectionSoundSource.clip=_injectionSoundClip;_injectionSoundSource.Play();
    _injectionSoundFinish=StartCoroutine(FinishInjectionSound(SyringeEffectAudio.MotionSeconds));
   }
   catch(Exception ex){StopInjectionSound();Logger.LogWarning("投与効果音: "+ex.Message);}
  }
  IEnumerator FinishInjectionSound(float seconds)
  {yield return new WaitForSecondsRealtime(seconds);_injectionSoundFinish=null;StopInjectionSound();}
  void StopInjectionSound()
  {
   if(_injectionSoundFinish!=null)StopCoroutine(_injectionSoundFinish);_injectionSoundFinish=null;
   if(_injectionSoundSource!=null){_injectionSoundSource.Stop();Destroy(_injectionSoundSource);}_injectionSoundSource=null;
   if(_injectionSoundClip!=null)Destroy(_injectionSoundClip);_injectionSoundClip=null;
  }
 }
}
