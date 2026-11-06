using System;
using System.IO;
using MainGameSyringe;
namespace Manager
{
 public static class Voice {public static int Stops;public static void StopAll(bool fade){if(fade)throw new Exception("voice interruption must be immediate");Stops++;}}
}
namespace BepInEx.Configuration
{
 public class ConfigEntry<T> {public T Value;public ConfigEntry(T value){Value=value;}}
}
namespace MainGameSyringe
{
 public sealed partial class Plugin:UnityEngine.MonoBehaviour
 {
  public class TestInfo {public string Location;}
  public class TestConfig {public BepInEx.Configuration.ConfigEntry<T> Bind<T>(string group,string key,T value,string description)=>new BepInEx.Configuration.ConfigEntry<T>(value);}
  public TestInfo Info=new TestInfo();public TestConfig Config=new TestConfig();
  public MainGameSpiralEyes.TestLogger Logger=new MainGameSpiralEyes.TestLogger();
  readonly System.Collections.Generic.List<ChaControl> _actors=new System.Collections.Generic.List<ChaControl>();
  public bool Active=true;bool Allowed=>Active;
  bool IsInjectionTarget(ChaControl a)=>a!=null&&a.sex==1&&_actors.Contains(a);
  BepInEx.Configuration.ConfigEntry<float> NumberSetting(string a,string b,float value,float min,float max,string text)=>new BepInEx.Configuration.ConfigEntry<float>(value);
  void EditNumber(string text,BepInEx.Configuration.ConfigEntry<float> value){}
  void UiLabel(string text){}
  public void InitVoice(string root,params ChaControl[] actors){Info.Location=Path.Combine(root,"MainGameSyringe.dll");_actors.AddRange(actors);BindVoiceSettings();}
  public void Cue(VoiceCueEvent cue,ChaControl actor)=>PlayVoiceCue(cue,actor);
  public void Prepared()=>PlayPreparedVoice();
  public void VoiceSettings(bool enabled,int stages){_voiceEnabled.Value=enabled;_preparedVoiceEnabled.Value=(stages&1)!=0;_insertedVoiceEnabled.Value=(stages&2)!=0;_injectedVoiceEnabled.Value=(stages&4)!=0;}
  public void StopAll()=>StopVoices();
  public int PlayingCount=>_voicePlaying.Count;
  public UnityEngine.AudioClip Current(ChaControl a)=>_voicePlaying[a].Clip;
  public UnityEngine.Coroutine Completion(ChaControl a)=>_voicePlaying[a].Finish;
  public void PriorityProvider(Func<IDisposable> provider){_acquireDildoVoicePriority=provider;}
  public void InterruptProvider(Func<bool> provider){_interruptBridgeVoices=provider;_bridgeInterruptResolved=true;}
  public void GuardProvider(Func<Action,IDisposable> provider){_beginSyringeDialogue=provider;_syringeGuardResolved=true;}
 }
}
static class VoiceTests
{
 sealed class PriorityLease:IDisposable
 {
  Action _release;public PriorityLease(Action release){_release=release;}
  public void Dispose(){var release=_release;_release=null;release?.Invoke();}
 }
 public static void Run(Action<bool,string> check)
 {
  string root=Path.Combine(@"J:\temp","syringe-voice-tests-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(root);
  try
  {
   var list=VoiceCueCatalog.Parse("{\"items\":[{\"file\":\"a.wav\",\"text\":\"hello\"}]}",root);
   check(list.Count==1&&list[0].Dialogue=="hello","GUI manifest mapping");
   list=VoiceCueCatalog.Parse("[{\"id\":7,\"dialogue\":\"seven\"}]",root);
   check(list[0].Id=="7"&&list[0].Dialogue=="seven"&&Path.GetFileName(list[0].FilePath)=="7.wav","id/dialogue mapping");
   foreach(string invalid in new[]{"null","{\"id\":\"bad\"}","{\"file\":\"../x.wav\"}","{\"file\":\"x.mp3\"}","[{\"id\":1},{\"id\":1}]"})
   {bool failed=false;try{VoiceCueCatalog.Parse(invalid,root);}catch{failed=true;}check(failed,"invalid voice catalog accepted: "+invalid);}
   var picks=VoiceCueCatalog.Parse("[{\"file\":\"a.wav\"},{\"file\":\"b.wav\"}]",root);
   check(VoiceCueCatalog.Pick(picks,picks[0].FilePath,n=>0)==picks[1],"repeat avoidance");
   foreach(VoiceCueEvent cue in Enum.GetValues(typeof(VoiceCueEvent)))
   {
    string dir=Path.Combine(root,"Audio",cue.ToString());Directory.CreateDirectory(dir);
    File.WriteAllText(Path.Combine(dir,"manifest.json"),"{\"items\":[{\"file\":\"test.wav\",\"text\":\"test\"}]}");
    using(var w=new BinaryWriter(File.Create(Path.Combine(dir,"test.wav"))))
    {
     w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(40);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
     w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(8000);w.Write(16000);w.Write((short)2);w.Write((short)16);
     w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(4);w.Write((short)0);w.Write((short)16384);
    }
   }
   var a=new ChaControl{MouthFixed=true};var b=new ChaControl();var male=new ChaControl{sex=0};var p=new Plugin();p.InitVoice(root,a,b,male);
   int priorityCount=0;
   int interrupts=0;
   int guards=0;Action externalSpeech=null;
   p.GuardProvider(callback=>{guards++;externalSpeech=callback;return new PriorityLease(()=>guards--);});
   p.InterruptProvider(()=>{interrupts++;return true;});
   p.PriorityProvider(()=>{priorityCount++;return new PriorityLease(()=>priorityCount--);});
   p.Active=false;p.Prepared();check(p.PlayingCount==0&&interrupts==0,"voice or interruption before gate");p.Active=true;
   p.Cue(VoiceCueEvent.Inserted,new ChaControl());p.Cue(VoiceCueEvent.Inserted,male);check(p.PlayingCount==0,"wrong actor voice");
   p.Prepared();check(p.PlayingCount==1,"prepared voice missing");var old=p.Current(a);var completion=p.Completion(a);
   check(a.LipSource!=null&&a.LipSource.clip==old&&!a.MouthFixed,"prepared voice did not bind lip sync and release mouth");
   check(priorityCount==1,"prepared voice failed to suppress dildo audio");
   check(guards==1,"prepared voice did not block native voices");
   check(interrupts==1,"prepared voice did not interrupt all other voices");
   float[] samples=new float[2];old.Reader(samples);check(Math.Abs(samples[1]-.5f)<.0001,"real WAV decoder");
   p.Cue(VoiceCueEvent.Inserted,a);check(old.Destroyed&&completion.Stopped&&p.PlayingCount==1,"insertion did not replace prepared cue");
   check(a.LipSource!=null&&a.LipSource.clip==p.Current(a)&&!a.MouthFixed,"inserted voice lip sync missing");
   check(priorityCount==1,"replacement leaked or released its suppression lease");
   completion.Routine.MoveNext();check(p.PlayingCount==1,"old completion removed new voice");
   check(priorityCount==1,"stale completion released new voice priority");
   var otherClip=p.Current(a);var otherCompletion=p.Completion(a);
   p.Cue(VoiceCueEvent.Injected,b);check(p.PlayingCount==1&&otherClip.Destroyed&&otherCompletion.Stopped,"new syringe dialogue did not interrupt other actor");
   check(a.LipSource==null&&a.MouthFixed&&b.LipSource!=null&&!b.MouthFixed,"speaker switch did not restore old mouth and bind injected voice");
   check(priorityCount==1&&interrupts==3,"replacement voice did not interrupt all voices exactly once");
   otherCompletion.Routine.MoveNext();check(p.PlayingCount==1,"old actor completion stopped replacement voice");
   var finishedClip=p.Current(b);p.Completion(b).Routine.MoveNext();check(p.PlayingCount==0&&finishedClip.Destroyed,"voice natural completion leak");
   check(priorityCount==0,"natural completion retained suppression");
   check(guards==0,"natural completion retained native voice guard");
   check(b.LipSource==null&&!b.MouthFixed,"natural completion did not restore mouth");
   p.Prepared();var cutClip=p.Current(a);var cutCompletion=p.Completion(a);externalSpeech();
   check(cutClip.Destroyed&&cutCompletion.Stopped&&p.PlayingCount==0&&guards==0&&priorityCount==0,
    "external speech did not stop syringe and release both guards");
   check(a.LipSource==null&&a.MouthFixed,"interruption retained lip sync or lost fixed mouth");
   p.InterruptProvider(()=>false);var nativeStops=Manager.Voice.Stops;p.Prepared();
   check(Manager.Voice.Stops==nativeStops+1,"missing bridge did not stop native voices");
   var clip=p.Current(a);p.Active=false;p.StopAll();p.StopAll();check(clip.Destroyed&&p.PlayingCount==0,"gate cleanup/double stop");
   check(priorityCount==0,"gate cleanup leaked suppression");
   check(a.LipSource==null&&a.MouthFixed,"gate cleanup retained lip sync or lost mouth state");
   p.Active=true;
   for(int mask=0;mask<8;mask++)
   foreach(bool enabled in new[]{false,true})
   foreach(VoiceCueEvent cue in Enum.GetValues(typeof(VoiceCueEvent)))
   {
    p.VoiceSettings(enabled,mask);nativeStops=Manager.Voice.Stops;
    p.Cue(cue,a);
    bool expected=enabled&&(mask&(1<<(int)cue))!=0;
    check(p.PlayingCount==(expected?1:0),"stage voice setting failed: "+enabled+"/"+mask+"/"+cue);
    check(Manager.Voice.Stops==nativeStops+(expected?1:0),"disabled stage interrupted other voices: "+enabled+"/"+mask+"/"+cue);
    check(priorityCount==(expected?1:0)&&guards==(expected?1:0),"stage voice suppression mismatch: "+enabled+"/"+mask+"/"+cue);
    p.StopAll();
   }
   p.VoiceSettings(true,7);
   p.Active=true;p.PriorityProvider(()=>{throw new InvalidOperationException("priority failure");});
   p.Cue(VoiceCueEvent.Injected,a);check(p.PlayingCount==0&&priorityCount==0,"failed playback setup retained a priority lease");
  }
  finally{Directory.Delete(root,true);}
 }
}

