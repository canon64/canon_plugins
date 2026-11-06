using System;
using UnityEngine;
using BepInEx.Configuration;
namespace MainGameSyringe
{
 public sealed partial class Plugin
 {
  GameObject _model;Transform _insertedBone;bool _spent,_focused=true;
  bool InteractionFocused=>_focused;
  internal bool _vr;
  string _status,_probeReport="test miss";
  ConfigEntry<float> _injectionSeconds=new ConfigEntry<float>(3);
  public int Spawns,Insertions,Doses,Withdrawals,Deletes;
  Animation _injectionAnimation=new Animation();
  Transform _pusher=new Transform(),_liquidPivot=new Transform();Vector3 _pusherRest;
  public bool Contact=true;
  void Activate(){Spawns++;_model=new GameObject("Syringe");_spent=false;}
  void TryInsert(bool force){if(Contact){Insertions++;_insertedBone=new Transform();}}
  void Inject(){Doses++;_spent=true;_injectionBusyUntil=Time.realtimeSinceStartup+_injectionSeconds.Value;PlayInjectionSound();}
  void DetachInsertion(bool held){Withdrawals++;_insertedBone=null;}
  void WithdrawNeedle()=>DetachInsertion(false);
  void Despawn(){Deletes++;CancelRefill();StopInjectionSound();_model=null;_insertedBone=null;}
  void Trace(string value){}
  public void SetupActionTests(){BindInjectionSoundSettings();}
  public AudioClip EffectClip=>_injectionSoundClip;
  public Coroutine EffectFinish=>_injectionSoundFinish;
  public void CompleteSound()=>_injectionSoundFinish.Routine.MoveNext();
  public Coroutine RefillFinish=>_refillFinish;
  public bool Empty=>_spent;
  public AnimationState RefillAnimation=>_injectionAnimation["Inject"];
  public void RequestRefill()=>Refill();
  public void CompleteRefill()=>_refillFinish.Routine.MoveNext();
  public void CancelTestRefill()=>CancelRefill();
  bool _testRefillActive,_testDrew;
  void BeginRefillVisuals(){_testRefillActive=true;_testDrew=false;}
  void KickRefillWaves(){}
  void CompleteRefillDrawing(){_testDrew=true;_spent=false;}
  void EndRefillVisuals(){if(!_testRefillActive)return;_spent=!_testDrew;_testRefillActive=false;}
 }
}
static class PrimaryActionTests
{
 public static void Run(Action<bool,string> check)
 {
  var p=new MainGameSyringe.Plugin();p.SetupActionTests();Time.realtimeSinceStartup=100;
  p.Active=false;p.PrimaryAction();check(p.Spawns==0,"gate refused primary action");p.Active=true;
  p.PrimaryAction();check(p.Spawns==1&&p.Insertions==0&&p.Doses==0,"spawn performed extra action");
  p.Contact=false;p.PrimaryAction();check(p.Insertions==0&&p.Doses==0,"miss advanced workflow");
  p.Contact=true;p.PrimaryAction();check(p.Insertions==1&&p.Doses==0&&p.EffectClip==null,"insert performed dose/audio");
  p.PrimaryAction();check(p.Doses==1&&p.Withdrawals==0&&p.EffectClip.length==3,"dose duration or combined withdrawal");
  check(p.EffectClip.name=="Injection01","dose did not use selected 01 sound");
  var clip=p.EffectClip;var finish=p.EffectFinish;
  p.PrimaryAction();check(p.Doses==1&&p.Withdrawals==0,"double click while busy");
  Time.realtimeSinceStartup=103;p.CompleteSound();check(clip.Destroyed&&p.EffectClip==null,"natural sound cleanup");
  p.PrimaryAction();check(p.Withdrawals==1&&p.RefillFinish==null,"withdraw combined with refill");
  p.PrimaryAction();check(p.RefillFinish!=null&&p.Empty&&p.Insertions==1,"refill completed instantly or inserted");
  var refill=p.RefillFinish;
  check(Math.Abs(((WaitForSeconds)refill.Routine.Current).Seconds-MainGameSyringe.SyringeRefillMotion.DrawStart)<.00001,"bottle approach did not precede drawing");
  p.CompleteRefill();
  check(p.EffectClip.name=="Refill02"&&p.EffectClip.length==3,"refill did not use selected 02 sound for three seconds");
  check(((WaitForSeconds)refill.Routine.Current).Seconds==3,"pusher drawing interval is not three seconds");
  p.PrimaryAction();p.RequestRefill();check(p.RefillFinish==refill&&p.Insertions==1&&p.Doses==1,"busy refill restarted or advanced action");
  p.CompleteRefill();check(!p.Empty&&p.RefillFinish==refill,"filled bottle withdrawal was skipped");
  p.CompleteRefill();check(!p.Empty&&p.RefillFinish==null,"refill did not finish after bottle withdrawal");
  p.PrimaryAction();check(p.Insertions==2&&p.Doses==1,"reinsertion combined with dose");
  p.PrimaryAction();check(p.Doses==2,"second dose missing");
  clip=p.EffectClip;finish=p.EffectFinish;p.DeleteSyringe();
  check(p.Deletes==1&&clip.Destroyed&&finish.Stopped&&p.EffectClip==null,"delete leaked injection sound");
  p.Active=false;p.PrimaryAction();check(p.Spawns==1,"stopped session spawned syringe");
  p.Active=true;p._vr=true;p.PrimaryAction();check(p.Spawns==2,"VR button did not spawn");
  p.PrimaryAction();check(p.Deletes==2&&p.Insertions==2,"VR button inserted instead of deleting");
  p.PrimaryAction();check(p.Spawns==3,"VR button did not respawn after deletion");
  p.RequestRefill();check(p.RefillFinish==null,"full syringe started refill");
  p._vr=false;Time.realtimeSinceStartup=106;p.PrimaryAction();p.PrimaryAction();
  Time.realtimeSinceStartup=109;p.PrimaryAction();p.PrimaryAction();refill=p.RefillFinish;
  p.DeleteSyringe();p.PrimaryAction();refill.Routine.MoveNext();
  check(refill.Stopped&&p.RefillFinish==null&&p.Empty==false,"deleted syringe refill affected replacement");
  p.PrimaryAction();p.PrimaryAction();Time.realtimeSinceStartup=112;p.PrimaryAction();p.PrimaryAction();
  refill=p.RefillFinish;p.CompleteRefill();clip=p.EffectClip;finish=p.EffectFinish;p.CancelTestRefill();refill.Routine.MoveNext();
  check(p.Empty&&p.RefillFinish==null&&refill.Stopped,"cancelled refill restored drug");
  check(clip.Destroyed&&finish.Stopped&&p.EffectClip==null,"cancelled refill leaked sound");
 }
}
