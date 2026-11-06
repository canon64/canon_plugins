using System;
using System.Collections.Generic;
using CanonGate;
using MainGameSyringe;
using Spiral=MainGameSpiralEyes.PublicApi;
using SpiralPlugin=MainGameSpiralEyes.Plugin;
using Doll=MainGameDollMode.Plugin;
class Program
{
 const string Syringe="com.kks.maingame.syringe",Dildo="com.kks.maingame.vrdildo";
 static int checks;
 static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
 static void Main()
 {
  try{Tabs();Interaction();Skin();FadeSteps();PupilFollowKkutsTests.Run(Check);Apis();Lifecycle();VoiceTests.Run(Check);PrimaryActionTests.Run(Check);RefillMotionTests.Run(Check);Console.WriteLine("PASS: "+checks+" checks; tab ordering, primary action separation, sound lifecycle, skin, eyes, timed ownership, voice catalog/playback lifecycle and GateApi (Unity rendering/input/audio are boundary stubs).");}
  catch(Exception ex){Console.WriteLine("FAIL after "+checks+": "+ex.Message);Environment.ExitCode=1;}
 }
 static void Interaction()
 {
  Check(PrimaryActionPolicy.Decide(false,false,false,false)==SyringeAction.Spawn,"spawn action");
  Check(PrimaryActionPolicy.Decide(true,false,false,false)==SyringeAction.Insert,"insertion must not dose");
  Check(PrimaryActionPolicy.Decide(true,true,false,false)==SyringeAction.Inject,"separate dose action");
  Check(PrimaryActionPolicy.Decide(true,true,true,true)==SyringeAction.Wait,"busy dose permits no next action");
  Check(PrimaryActionPolicy.Decide(true,true,true,false)==SyringeAction.Withdraw,"withdraw must not refill");
  Check(PrimaryActionPolicy.Decide(true,false,true,false)==SyringeAction.Refill,"separate refill action");
  var dose=SyringeEffectAudio.Create(false);var refill=SyringeEffectAudio.Create(true);
  Check(dose.length==3&&refill.length==3&&dose.Samples.Length==144000&&refill.Samples.Length==144000,"three-second embedded effects");
  Check(dose.name=="Injection01"&&refill.name=="Refill02","effect assignments swapped");
  double energy=0,difference=0;bool bounded=true;
  for(int i=0;i<dose.Samples.Length;i++){float sample=dose.Samples[i];bounded&=!float.IsNaN(sample)&&Math.Abs(sample)<=1;energy+=sample*sample;difference+=Math.Abs(sample-refill.Samples[i]);}
  Check(bounded&&energy/dose.Samples.Length>0.000001&&difference>1,"effects invalid, silent, or identical");
  var p=new InjectionPolicy<string>();
  Check(!p.TriggerDown(Hand.Left),"trigger without grip");
  Check(p.Grab(Hand.Left)&&!p.Grab(Hand.Right),"two simultaneous hands");
  Check(!p.TriggerDown(Hand.Left),"held trigger activated on grab");p.TriggerUp(Hand.Left);
  Check(p.TriggerDown(Hand.Left)&&!p.TriggerDown(Hand.Left),"long press repeated");
  p.Commit("kept");p.ResetInput();
  Check(p.HeldBy==Hand.None&&p.IsInjected("kept"),"despawn must release input while retaining injected effect");
  Check(p.Grab(Hand.Left)&&p.TriggerDown(Hand.Left),"despawn/respawn left a stuck trigger");
  p.Release("kept");
  p.Drop();Check(p.Grab(Hand.Right),"right grip");Check(p.TriggerDown(Hand.Right),"right trigger");
  p.Commit("A");p.Commit("A");Check(p.ClearInjected().Count==1&&p.HeldBy==Hand.Right,"effect clear disturbed grip");
  p.Commit("B");p.Release("A");Check(p.IsInjected("B"),"wrong actor release");p.Release("B");Check(!p.IsInjected("B"),"actor release");
  Check(InjectionPolicy<string>.UniqueTarget(new[]{"A","A"},a=>true)=="A","same actor arm/thigh overlap");
  Check(InjectionPolicy<string>.UniqueTarget(new[]{"A","B"},a=>a=="A")==null,"ambiguous actor hit accepted");
  Check(InjectionPolicy<string>.UniqueTarget(new[]{"A"},a=>false)==null,"ineligible actor accepted");
  Check(InjectionPolicy<string>.UniqueTarget(new string[0],a=>true)==null,"empty space accepted");
  p.Clear();Check(p.HeldBy==Hand.None&&!p.IsInjected("B"),"session reset");
 }
 static void Apis()
 {
  GateApi.SetRunning(true);var registrations=new List<IDisposable>();
  foreach(var guid in new[]{Syringe,Dildo,SpiralPlugin.Guid,Doll.GUID,"unknown"})registrations.Add(GateApi.Register(guid,()=>{},()=>{}));
  var policy=new ExceptionPolicy();var actor=new ChaControl();var other=new ChaControl();
  var sp=new SpiralPlugin();SpiralPlugin.Instance=sp;sp._actors.Add(actor);sp._actors.Add(other);
  var doll=new Doll();Doll.Instance=doll;CanonPlugins.Local.SessionGate.Actors=new[]{actor};doll._originalHideHighlightByCha[actor.GetInstanceID()]=false;
  GateApi.BeginSession(GateApi.SessionKind.H,false,policy,new HSceneProc());
  Check(!Spiral.BeginOverlay(Syringe,actor)&&!Doll.IsDollModeActive(actor),"gate refusal");GateApi.EndSession(GateApi.SessionKind.H);
  GateApi.BeginSession(GateApi.SessionKind.H,true,policy,new HSceneProc());
  Check(!Spiral.BeginOverlay("unknown",actor),"unrecognized owner");Check(!Spiral.BeginOverlay(Syringe,new ChaControl()),"nonparticipant");
  Check(Doll.IsDollModeActive(actor)&&!Doll.IsDollModeActive(other),"actor-scoped doll query");
  actor.IdentityMatches=false;Check(!Doll.IsDollModeActive(actor),"stale identity");actor.IdentityMatches=true;
  actor.sex=0;Check(!Doll.IsDollModeActive(actor),"male doll eligible");actor.sex=1;
  int releases=0;Action<ChaControl> release=a=>{Check(ReferenceEquals(a,actor),"wrong release actor");releases++;};Doll.DollModeReleased+=release;
  doll.SetEnabled(false);doll.SetEnabled(false);Check(releases==1,"duplicate doll release");doll.SetEnabled(true);
  Check(Spiral.BeginOverlay(Syringe,actor)&&Spiral.BeginOverlay(Dildo,actor),"two owners begin");
  Check(sp.Independent(actor),"temporary syringe/dildo overlay must preserve the base heart on an independent layer");
  Check(Spiral.BeginOverlay(Syringe,actor),"duplicate owner begin");
  int allReleased=0;Action<ChaControl> all=a=>allReleased++;Spiral.OverlayReleased+=all;
  Spiral.EndOverlay(Syringe,actor);Check(sp.Forced(actor)&&sp._bindings.HasBinding(actor.rendEye[0])&&allReleased==0,"syringe removed dildo");
  Spiral.EndOverlay(Syringe,actor);Check(sp.Forced(actor),"duplicate release");
  Spiral.EndOverlay(Dildo,actor);Check(!sp.Forced(actor)&&sp._bindings.Count==0&&allReleased==1,"last-owner restore");
  sp.Manual.Add(actor);Check(Spiral.BeginOverlay(Syringe,actor),"manual + temporary begin");Check(!sp.Independent(actor),"manual spiral selection must not receive a duplicate centered layer");Spiral.EndOverlay(Syringe,actor);
  Check(sp._bindings.HasBinding(actor.rendEye[0]),"manual selection lost");sp.Manual.Clear();
  Check(Spiral.BeginOverlay(Syringe,other),"second actor begin");Spiral.EndOverlay(Syringe,actor);Check(sp.Forced(other),"different actor disturbed");Spiral.EndOverlay(Syringe,other);
  sp._bindings.Restore();sp._selectedActors.Clear();
  sp.FailBinding=true;Check(!Spiral.BeginOverlay(Syringe,actor)&&!sp.Forced(actor),"failed binding retained request");sp.FailBinding=false;
  sp.ThrowBinding=true;Check(!Spiral.BeginOverlay(Syringe,actor)&&!sp.Forced(actor),"partial failure retained request");sp.ThrowBinding=false;
  actor.rendEye[1]=null;Check(!Spiral.BeginOverlay(Syringe,actor)&&!sp.Forced(actor),"single-eye success");actor.rendEye[1]=new UnityEngine.Renderer();
  Check(Spiral.BeginFadingOverlay(Syringe,actor),"transparent begin");
  Check(actor.rendEye[0].sharedMaterials[0].GetFloat("_CanonSpiralOpacity")==0,"initial opacity not zero");
  for(int level=0;level<=100;level++)
  {
   Check(Spiral.SetOverlayOpacity(Syringe,actor,level),"opacity request failed");
   Check(Math.Abs(actor.rendEye[0].sharedMaterials[0].GetFloat("_CanonSpiralOpacity")-level/100f)<.00001f,"opacity division");
  }
  var beforeChange=typeof(SpiralPlugin).GetMethod("EyesChanging",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
  var afterChange=typeof(SpiralPlugin).GetMethod("EyesChanged",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
  Spiral.SetOverlayOpacity(Syringe,actor,37);
  doll.SetEnabled(false);
  Check(sp.Forced(actor)&&Spiral.SetOverlayOpacity(Syringe,actor,37),"doll release cancelled timed pupil overlay");
  Check(Math.Abs(actor.rendEye[0].sharedMaterials[0].GetFloat("_CanonSpiralOpacity")-.37f)<.00001f,"doll release reset opacity");
  // Simulate a material/property-block replacement outside the numbered-eye API.
  actor.rendEye[0].SetPropertyBlock(null,0);
  Check(Spiral.SetOverlayOpacity(Syringe,actor,38),"replacement lost timed owner");
  Check(ReferenceEquals(actor.rendEye[0].sharedMaterials[0].GetTexture("_CanonSpiral"),sp.OverlayTexture),"timed step failed to bind replacement eye");
  Spiral.SetOverlayOpacity(Syringe,actor,37);
  UnityEngine.Texture latestEye=null;
  foreach(int number in new[]{0,1,10,999})
  {
   beforeChange.Invoke(null,new object[]{actor});actor.EyeNumber=number;latestEye=new UnityEngine.Texture();
   foreach(var renderer in actor.rendEye){var block=new UnityEngine.MaterialPropertyBlock();block.SetTexture("_expression",latestEye);block.SetFloat("_exppower",.62f);block.SetFloat("unrelated",123);renderer.SetPropertyBlock(block,0);}
   afterChange.Invoke(null,new object[]{actor,null});
   Check(actor.EyeNumber==number&&ReferenceEquals(actor.rendEye[0].sharedMaterials[0].GetTexture("_CanonSpiral"),sp.OverlayTexture),"eye number lost spiral");
   Check(ReferenceEquals(actor.rendEye[0].Block().GetTexture("_expression"),latestEye),"native heart replaced");
   Check(Math.Abs(actor.rendEye[0].sharedMaterials[0].GetFloat("_CanonSpiralOpacity")-.37f)<.00001f&&actor.rendEye[0].Block().GetFloat("unrelated")==123,"eye change reset fade or base properties");
  }
  beforeChange.Invoke(null,new object[]{actor});beforeChange.Invoke(null,new object[]{actor});
  afterChange.Invoke(null,new object[]{actor,null});Check(!sp._bindings.HasBinding(actor.rendEye[0]),"nested eye change rebound before completion");
  var originalFailure=new Exception("original eye error");
  Check(ReferenceEquals(afterChange.Invoke(null,new object[]{actor,originalFailure}),originalFailure),"eye change swallowed original error");
  Check(Math.Abs(actor.rendEye[0].sharedMaterials[0].GetFloat("_CanonSpiralOpacity")-.37f)<.00001f,"nested eye change lost fade");
  Spiral.SetOverlayOpacity(Syringe,actor,-100);Check(actor.rendEye[0].sharedMaterials[0].GetFloat("_CanonSpiralOpacity")==0,"negative opacity not clamped");
  Spiral.SetOverlayOpacity(Syringe,actor,200);Check(actor.rendEye[0].sharedMaterials[0].GetFloat("_CanonSpiralOpacity")==1,"excess opacity not clamped");
  Check(Spiral.BeginOverlay(Dildo,actor),"second owner during fade");
  Check(actor.rendEye[0].sharedMaterials[0].GetFloat("_CanonSpiralOpacity")==1,"permanent owner dimmed");
  Spiral.SetOverlayOpacity(Syringe,actor,0);Check(actor.rendEye[0].sharedMaterials[0].GetFloat("_CanonSpiralOpacity")==1,"fading dimmed other owner");
  Spiral.EndOverlay(Dildo,actor);Check(actor.rendEye[0].sharedMaterials[0].GetFloat("_CanonSpiralOpacity")==0,"remaining zero-opacity request ignored");
  Spiral.EndOverlay(Syringe,actor);
  Check(ReferenceEquals(actor.rendEye[0].Block().GetTexture("_expression"),latestEye)&&actor.rendEye[0].Block().GetFloat("_exppower")==.62f,"release reverted to old eye number data");
  var removedActor=new ChaControl();sp._actors.Add(removedActor);
  Check(Spiral.BeginOverlay(Syringe,removedActor)&&Spiral.BeginOverlay(Dildo,removedActor),"owners before actor destruction");
  var destroying=typeof(SpiralPlugin).GetMethod("ActorDestroying",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
  destroying.Invoke(null,new object[]{removedActor});destroying.Invoke(null,new object[]{removedActor});
  Check(!sp._actors.Contains(removedActor)&&!sp.Forced(removedActor)&&!sp._bindings.HasBinding(removedActor.rendEye[0]),"destroyed actor retained ownership or bindings");
  Check(removedActor.rendEye[0].sharedMaterials[0].shader.name=="Koikano/main_eye","destroyed actor shader not restored");
  Check(!Spiral.BeginOverlay(Syringe,removedActor),"destroyed actor accepted a late request");
  Check(Spiral.BeginOverlay(Syringe,actor)&&Spiral.BeginOverlay(Dildo,actor),"owners before teardown");
  int ownersReleased=0;Action<string,ChaControl> owned=(o,a)=>ownersReleased++;Spiral.OwnerOverlayReleased+=owned;
  var old=GateApi.Capture(Syringe);GateApi.EndSession(GateApi.SessionKind.H);Check(!old.IsValid,"stale permit");sp.Stop();sp.Stop();Check(ownersReleased==2,"teardown owner notifications");
  Check(!Spiral.BeginOverlay(Syringe,actor),"late begin after stop");
  GateApi.BeginSession(GateApi.SessionKind.H,true,policy,new HSceneProc());Check(!sp.Forced(actor),"old owner reached new H");doll.Stop();
  GateApi.EndSession(GateApi.SessionKind.H);
  Doll.DollModeReleased-=release;Spiral.OverlayReleased-=all;Spiral.OwnerOverlayReleased-=owned;
  foreach(var registration in registrations)registration.Dispose();GateApi.SetRunning(false);
 }
 static void FadeSteps()
 {
  var steps=new List<OpacityStep>(OpacitySteps.Build(30,600));
  Check(steps.Count==200&&steps[99].Level==100&&steps[199].Level==0,"fade endpoints or division count");
  float incoming=0,outgoing=0;
  for(int i=0;i<100;i++){incoming+=steps[i].Delay;outgoing+=steps[i+100].Delay;Check(steps[i].Level==i+1&&steps[i+100].Level==99-i,"fade direction");}
  Check(Math.Abs(incoming-30)<.001&&Math.Abs(outgoing-600)<.001,"fade duration");
 }
 static void Skin()
 {
  AdvancedTests.Run(Check);
  var guard=new ReinsertionGuard();
  guard.Withdraw(.12f,.033f);
  Check(guard.CanInsert(.008f,.033f),"withdrawn needle cannot reinsert near the same body region");
  guard.Withdraw(.008f,.033f);
  Check(!guard.CanInsert(.02f,.033f),"needle reattached before leaving the puncture");
  Check(guard.CanInsert(.04f,.033f)&&guard.CanInsert(.008f,.033f),"local puncture clearance did not rearm insertion");
  guard.Withdraw(.008f,.033f);Check(guard.CanInsert(.008f,.033f,true),"explicit desktop insertion blocked");
  guard.Withdraw(.008f,.033f);guard.Reset();Check(guard.CanInsert(.008f,.033f),"new session inherited insertion block");
  var a=new System.Numerics.Vector3(-1,-1,0);var b=new System.Numerics.Vector3(1,-1,0);var c=new System.Numerics.Vector3(0,1,0);
  var origin=new System.Numerics.Vector3(0,0,-.025f);var direction=System.Numerics.Vector3.UnitZ;
  Check(SkinGeometry.RayTriangle(origin,direction,a,b,c,.05f,out float distance)&&Math.Abs(distance-.025f)<1e-6,"skin surface intersection");
  Check(!SkinGeometry.RayTriangle(origin,-direction,a,b,c,.05f,out distance),"backwards needle accepted");
  Check(!SkinGeometry.RayTriangle(origin,System.Numerics.Vector3.UnitX,a,b,c,.05f,out distance),"parallel needle accepted");
  Check(!SkinGeometry.RayTriangle(origin,direction,a,b,c,.01f,out distance),"distant skin accepted");
  Check(!SkinGeometry.RayTriangle(new System.Numerics.Vector3(3,3,-.025f),direction,a,b,c,.05f,out distance),"outside skin triangle accepted");
  var offset=SkinGeometry.InsertionDelta(origin,System.Numerics.Vector3.Zero,direction,.008f);
  Check(((origin+offset)-new System.Numerics.Vector3(0,0,.008f)).Length()<1e-6,"insertion depth");
  Check(Math.Abs(SkinGeometry.DistanceToSegment(new System.Numerics.Vector3(.05f,.5f,0),System.Numerics.Vector3.Zero,System.Numerics.Vector3.UnitY)-.05f)<1e-6,"arm segment distance");
  Check(Math.Abs(SkinGeometry.DistanceToSegment(System.Numerics.Vector3.UnitX,System.Numerics.Vector3.Zero,System.Numerics.Vector3.Zero)-1)<1e-6,"degenerate bone segment");
  Check(!SkinGeometry.ShouldRelease(.119f,.12f)&&SkinGeometry.ShouldRelease(.12f,.12f)&&SkinGeometry.ShouldRelease(.13f,.12f),"pull release threshold");
 }
 static void Tabs()
 {
  var sorted=new[]{"chain","afterimage","syringe","doll"};
  var result=MainGameUnifiedUi.TabOrdering.Arrange(sorted,x=>x,x=>x=="syringe"?"chain":x=="doll"?"syringe":null);
  Check(string.Join(",",result)=="chain,syringe,doll,afterimage","tab not next to chain with numeric-order collision");
  result=MainGameUnifiedUi.TabOrdering.Arrange(new[]{"afterimage","syringe","doll"},x=>x,x=>x=="syringe"?"chain":x=="doll"?"syringe":null);
  Check(string.Join(",",result)=="afterimage,syringe,doll","missing anchor hid tab");
  result=MainGameUnifiedUi.TabOrdering.Arrange(new[]{"a","b"},x=>x,x=>x=="a"?"b":"a");Check(result.Count==2,"cyclic anchor hid tabs");
 }
 static void Lifecycle()
 {
  GateApi.SetRunning(true);int starts=0,stops=0;var policy=new ExceptionPolicy();
  using(var client=new SessionLifecycle("syringe.lifecycle",()=>starts++,()=>stops++))
  {
   GateApi.BeginSession(GateApi.SessionKind.H,false,policy,new HSceneProc());Check(starts==0&&!client.Allowed,"normal H refusal");GateApi.EndSession(GateApi.SessionKind.H);
   GateApi.BeginSession(GateApi.SessionKind.H,true,policy,new HSceneProc());var old=GateApi.Capture("syringe.lifecycle");
   GateApi.BeginSession(GateApi.SessionKind.H,true,policy,new HSceneProc());Check(starts==1,"duplicate session start");
   GateApi.EndSession(GateApi.SessionKind.H);client.Stop();Check(stops==1&&!old.IsValid,"duplicate stop");
   GateApi.BeginSession(GateApi.SessionKind.Maker,true,policy);Check(starts==1,"Maker started H effects");GateApi.EndSession(GateApi.SessionKind.Maker);
   GateApi.BeginSession(GateApi.SessionKind.H,true,policy,new HSceneProc());client.Stop();client.Stop();Check(stops==2&&!client.Allowed,"local disable");GateApi.EndSession(GateApi.SessionKind.H);
  }
  int cleanup=0;GateApi.ReportError=ex=>{};
  using(var failing=new SessionLifecycle("syringe.failure",()=>{throw new Exception("partial start");},()=>cleanup++))
  {
   GateApi.BeginSession(GateApi.SessionKind.H,true,policy,new HSceneProc());Check(cleanup==1&&!failing.Allowed,"partial initialization");GateApi.EndSession(GateApi.SessionKind.H);Check(cleanup==1,"partial initialization duplicate cleanup");
  }
  GateApi.SetRunning(false);
 }
}
