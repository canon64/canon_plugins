using System;
using AnimationLoader;
using MainGameSamabakeMotion;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;

internal static class Program
{
    private static int count;
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); count++; }
    private static int Main()
    {
        bool allowed = false;
        int logs = 0;
        var runtime = new NativeIntegration(() => allowed, () => false, _ => logs++);
        var donor = new RuntimeAnimatorController();
        var female = new ChaControl { sex = 1, visibleAll = true };
        var male = new ChaControl { sex = 0 };
        female.animBody.runtimeAnimatorController = donor;
        male.animBody.runtimeAnimatorController = donor;
        male.animBody.enabled = false;
        male.animBody.cullingMode = AnimatorCullingMode.CullCompletely;
        var solver = new RootMotion.FinalIK.FullBodyBipedIK();
        var disabledSolver = new Illusion.Component.Correct.FrameCorrect { enabled = false };
        var bone = new Transform { localPosition = new Vector3 { x = 4 } };
        female.animBody.Components.AddRange(new object[] { solver, disabledSolver, bone });
        var scene = new HSceneProc { male = male };
        scene.lstFemale.Add(female);
        var pose = new HSceneProc.AnimationListInfo { id = 0 };
        var foreign = new HSceneProc.AnimationListInfo { id = 0 };
        SwapAnim.SwapAnimationMapping[pose] = new SwapAnimationInfo { Guid = ZipmodPose.Guid, StudioId = ZipmodPose.StudioId,
            ControllerFemale = ZipmodPose.FemaleController, ControllerMale = ZipmodPose.MaleController,
            PathFemale = ZipmodPose.FemalePath, PathMale = ZipmodPose.MalePath };
        AssetBundleManager.Controllers[ZipmodPose.FemaleController] = new RuntimeAnimatorController();
        AssetBundleManager.Controllers[ZipmodPose.MaleController] = new RuntimeAnimatorController();
        runtime.BeginSession(scene);
        runtime.CaptureBase(scene, pose);
        runtime.AfterChange(scene, pose);
        Check(!runtime.Owns(female), "Denied gate must not apply");
        allowed = true;
        runtime.BeginSession(scene);
        runtime.CaptureBase(scene, foreign);
        runtime.AfterChange(scene, foreign);
        Check(!runtime.Owns(female), "Same donor ID must not claim native pose");
        var originalHand = scene.hand.nowMES;
        Select(runtime, scene, pose, donor);
        Check(runtime.Owns(female) && runtime.Owns(male), "Own both imported actors");
        Check(!solver.enabled && !disabledSolver.enabled, "Disable conflicting correction only");
        Check(male.visibleAll, "Male visible during imported kiss");
        Check(male.animBody.enabled && male.animBody.cullingMode == AnimatorCullingMode.AlwaysAnimate, "Enable male playback independently of donor");
        Check(male.animBody.PlayedState == "Idle", "Male starts in native Idle");
        // Reproduce HActionBase's visibility guard: the native call skips this male.
        male.visibleAll = false;
        int playCount = male.animBody.PlayCount;
        runtime.AfterPlay("K_Touch");
        Check(male.animBody.PlayedState == "K_Touch" && male.animBody.PlayCount == playCount + 1, "Native state event reaches hidden male");
        runtime.AfterPlay("K_Loop");
        Check(male.animBody.PlayedState == "K_Loop", "Male enters kiss loop");
        playCount = male.animBody.PlayCount;
        runtime.AfterPlay("unsupported");
        Check(male.animBody.PlayCount == playCount, "Never invent a missing male state");
        Check(scene.hand.nowMES.isTouchAreas[0] && !scene.hand.nowMES.isTouchAreas[1], "Kiss-only touch areas");
        runtime.AfterPlay("Orgasm_Start");
        Check(male.animBody.PlayedState == "Orgasm_Start", "Male follows native orgasm event");
        Check(!scene.hand.nowMES.isTouchAreas[0], "Disable touch during climax");
        bone.localPosition = new Vector3 { x = 9 };
        allowed = false;
        runtime.EndSession();
        Check(female.animBody.runtimeAnimatorController == donor && male.animBody.runtimeAnimatorController == donor, "Stop after permit revocation restores real donor");
        Check(solver.enabled && !disabledSolver.enabled, "Restore original solver enable states");
        Check(!male.visibleAll && bone.localPosition.x == 4, "Restore visibility and bone TRS");
        Check(!male.animBody.enabled && male.animBody.cullingMode == AnimatorCullingMode.CullCompletely, "Restore donor Animator enable/culling on stop");
        Check(scene.hand.nowMES == originalHand, "Restore hand state");
        runtime.EndSession();
        Check(!runtime.Owns(female), "Double stop harmless");
        allowed = true;
        runtime.BeginSession(scene);
        AssetBundleManager.Controllers.Remove(ZipmodPose.MaleController);
        Select(runtime, scene, pose, donor);
        Check(female.animBody.runtimeAnimatorController == donor && male.animBody.runtimeAnimatorController == donor, "Missing male bundle restores both AL overrides");
        AssetBundleManager.Controllers[ZipmodPose.MaleController] = new RuntimeAnimatorController { Missing = true };
        Select(runtime, scene, pose, donor);
        Check(!runtime.Owns(female) && solver.enabled && !male.visibleAll, "Partial apply failure restores first actor");
        Check(!male.animBody.enabled && male.animBody.cullingMode == AnimatorCullingMode.CullCompletely, "Partial failure restores male playback flags");
        AssetBundleManager.Controllers[ZipmodPose.MaleController] = new RuntimeAnimatorController();
        Select(runtime, scene, pose, donor);
        runtime.BeforeChange();
        Check(female.animBody.runtimeAnimatorController == donor && !runtime.Owns(female), "Switch-away restores before native change");
        runtime.EndSession();
        runtime.BeginSession(scene);
        runtime.AfterChange(scene, pose);
        Check(!runtime.Owns(female), "Old capture cannot apply in next H");
        Check(logs == 0, "Diagnostics OFF does not format/output failure logs");
        runtime.EndSession();
        MultiActorTests();
        ContactMathTests();
        Console.WriteLine("PASS helper lifecycle: " + count + " checks (Unity stubs; not game-runtime evidence)");
        return 0;
    }
    private static void MultiActorTests()
    {
        var definitions = new MotionDefinitions { version=2, poses=new[] { new PlaybackPose {
            guid="canon.samabake.native.mff",studioId=192615300,mode=7,name="サマバケ・test",states=new[]{"Idle","WLoop"},
            actors=new[] {
                new ActorDefinition {slot="female0",partner="male0",path="anim_imports/canon/samabake/test/f0",controller="test_f0",correctiveStates=new[]{"WLoop"}},
                new ActorDefinition {slot="female1",partner="male0",path="anim_imports/canon/samabake/test/f1",controller="test_f1"},
                new ActorDefinition {slot="male0",partner="female0",path="anim_imports/canon/samabake/test/m0",controller="test_m0"}}}}};
        string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"catalog-test.json");
        Action save=()=>{using(var stream=File.Create(file)) new DataContractJsonSerializer(typeof(MotionDefinitions)).WriteObject(stream,definitions);};
        save();
        ZipmodPose.Load(file);
        var info = new SwapAnimationInfo {Guid=definitions.poses[0].guid,StudioId=192615300,Mode=7,
            PathFemale=definitions.poses[0].actors[0].path,ControllerFemale="test_f0",
            PathFemale1=definitions.poses[0].actors[1].path,ControllerFemale1="test_f1",
            PathMale=definitions.poses[0].actors[2].path,ControllerMale="test_m0"};
        var entry = new HSceneProc.AnimationListInfo();
        SwapAnim.SwapAnimationMapping[entry]=info;
        var donor = new RuntimeAnimatorController();
        var scene = new HSceneProc {male=new ChaControl {sex=0}};
        scene.lstFemale.Add(new ChaControl {sex=1}); scene.lstFemale.Add(new ChaControl {sex=1});
        foreach(var actor in new[]{scene.lstFemale[0],scene.lstFemale[1],scene.male}) actor.animBody.runtimeAnimatorController=donor;
        scene.lstFemale[0].animBody.Layers=2;
        foreach(var definition in definitions.poses[0].actors) AssetBundleManager.Controllers[definition.controller]=new RuntimeAnimatorController();
        var runtime = new NativeIntegration(()=>true,()=>false,_=>{});
        runtime.BeginSession(scene); runtime.CaptureBase(scene,entry); runtime.AfterChange(scene,entry);
        Check(runtime.Owns(scene.lstFemale[0])&&runtime.Owns(scene.lstFemale[1])&&runtime.Owns(scene.male),"Three-actor pose applies each own slot");
        runtime.AfterPlay("WLoop");
        Check(scene.lstFemale[0].animBody.LayerWeights[1]==1,"Corrective layer enabled only on declared state");
        runtime.AfterPlay("Idle");
        Check(scene.lstFemale[0].animBody.LayerWeights[1]==0,"Corrective layer disabled on next state event");
        runtime.EndSession();
        Check(scene.lstFemale[1].animBody.runtimeAnimatorController==donor,"Second female restored at H end");
        ContactLifecycleTests(runtime,scene,entry,definitions,save);
        scene.lstFemale.RemoveAt(1);
        runtime.BeginSession(scene); runtime.CaptureBase(scene,entry); runtime.AfterChange(scene,entry);
        Check(!runtime.Owns(scene.male)&&!runtime.Owns(scene.lstFemale[0]),"Missing third actor never applies a partial pose");
        var before=definitions.poses[0].actors[0].path;
        definitions.poses[0].actors[0].path="../outside";save();
        bool rejected=false;try {MotionDefinitions.Read(file);} catch(InvalidDataException){rejected=true;}
        Check(rejected,"Catalog rejects unsafe package reference");
        definitions.poses[0].actors[0].path=before;
        definitions.poses[0].actors[1].slot="male1";save();rejected=false;
        try {MotionDefinitions.Read(file);} catch(InvalidDataException){rejected=true;}
        Check(rejected,"Catalog rejects unsupported second male slot");
        File.Delete(file);
        runtime.EndSession();
    }
    private sealed class FakeContact : IContactRig
    {
        public int Applied, Restored;
        public bool Configured, Fail;
        public float Height { get; set; }
        public void Configure(System.Collections.Generic.IDictionary<string,IContactRig> rigs) { Configured=rigs.Count==3; }
        public void Apply(string state) { if(!Configured) throw new Exception("Contact group incomplete"); Applied++;if(Fail)throw new Exception("Contact failure"); }
        public void Restore() { Restored++; }
    }
    private static void ContactLifecycleTests(NativeIntegration runtime,HSceneProc scene,HSceneProc.AnimationListInfo entry,MotionDefinitions definitions,Action save)
    {
        var pose=definitions.poses[0];
        foreach(var actor in pose.actors) actor.contact=new ContactDefinition {
            height=new HeightCalibration{bone="cf_n_height",nativeRest=1,sourceScales=new[]{.84f,.92f,1f}},
            partners=pose.actors.Select(a=>a.slot).ToArray(),
            states=pose.states.Select(s=>new ContactState{name=s,parts=Enumerable.Range(0,4).Select(i=>new ContactPart{slot=actor.slot}).ToArray(),frames=new ContactFrame[0]}).ToArray()};
        save();ZipmodPose.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"catalog-test.json"));
        var rigs=new System.Collections.Generic.Dictionary<ChaControl,FakeContact>();
        NativeContactRig.Factory=(actor,def)=>rigs[actor]=new FakeContact{Height=actor.sex==0?.8f:.2f};
        runtime.BeginSession(scene);runtime.CaptureBase(scene,entry);runtime.AfterChange(scene,entry);
        Check(rigs.Count==3&&rigs.Values.All(r=>r.Configured&&r.Applied==1),"All actors configure before contact application");
        Check(scene.male.animBody.Floats[Animator.StringToHash("height1")]==.8f&&scene.lstFemale[1].animBody.Floats[Animator.StringToHash("partner_height")]==.8f,"Own and partner physical heights reach every controller");
        Check(scene.male.animBody.Floats[Animator.StringToHash("speed")]==scene.flags.speed,"Non-caress speed initializes with native speed rather than speedBody");
        runtime.AfterPlay("WLoop");Check(rigs.Values.All(r=>r.Applied==2),"State event reapplies source contact constraints");
        runtime.ShapeChanged(scene.lstFemale[1]);Check(rigs.Values.All(r=>r.Applied==3),"Body-change event refreshes partner constraints");
        runtime.EndSession();Check(rigs.Values.All(r=>r.Restored==1),"Session end restores every contact owner once");
        rigs.Clear();NativeContactRig.Factory=(actor,def)=>rigs[actor]=new FakeContact{Height=.5f,Fail=actor==scene.lstFemale[1]};
        runtime.BeginSession(scene);runtime.CaptureBase(scene,entry);runtime.AfterChange(scene,entry);
        Check(!runtime.Owns(scene.male)&&rigs.Values.All(r=>r.Restored==1),"Partial contact failure rolls back the full group");
        runtime.EndSession();NativeContactRig.Factory=null;
        foreach(var actor in pose.actors)actor.contact=null;
        save();ZipmodPose.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"catalog-test.json"));
    }
    private static void ContactMathTests()
    {
        var map=new HeightCalibration{nativeRest=1,sourceScales=new[]{.84f,.92f,1f}};
        Check(Math.Abs(map.Map(.84f))<1e-5&&Math.Abs(map.Map(.92f)-.5f)<1e-5&&map.Map(1)==1,"Physical source S/M/L endpoint inversion");
        Check(Math.Abs(map.Map(.88f)-.25f)<1e-5&&Math.Abs(map.Map(.96f)-.75f)<1e-5,"Physical source height interpolation");
        Check(map.Map(.7f)==0&&map.Map(1.2f)==1,"Height outside authored range clamps without extrapolation");
        Check(ContactShape.Lerp(-2,3,8,.5f)==3&&ContactShape.Lerp(-2,3,8,.25f)==.5f,"Retain nonzero authored medium correction");
        Check(Math.Abs(ContactShape.Angle(359,0,1,.25f)-359.5f)<1e-5,"Euler correction uses shortest angular interpolation");
    }
    private static void Select(NativeIntegration runtime, HSceneProc scene, HSceneProc.AnimationListInfo pose, RuntimeAnimatorController donor)
    {
        runtime.BeforeChange();
        runtime.CaptureBase(scene, pose);
        scene.lstFemale[0].animBody.runtimeAnimatorController = new AnimatorOverrideController { runtimeAnimatorController = donor };
        scene.male.animBody.runtimeAnimatorController = new AnimatorOverrideController { runtimeAnimatorController = donor };
        runtime.AfterChange(scene, pose);
    }
}
