using System;
using System.IO;
using System.Linq;
using MainGameHLocationCoordinates;
using UnityEngine;

namespace MainGameHLocationPlacement
{
    internal sealed class TestEnvironment : IDisposable
    {
        internal readonly string Root;
        internal readonly OriginStore Store;
        internal readonly ComposerFixture Composer;
        internal readonly HipFixture Hip;
        internal readonly RuntimeBridge Bridge;
        internal readonly NativeScene Scene=new NativeScene();
        internal readonly PlacementSession Session;
        private readonly RuntimeHooks _hooks;
        internal TestEnvironment(string name)
        {
            Root=Program.DirectoryFor(name);Store=new OriginStore(Path.Combine(Root,"Origins"));
            Composer=new ComposerFixture{_presetsPath=Path.Combine(Root,"Composer.json")};Hip=new HipFixture{_posePresetIndexPath=Path.Combine(Root,"Hip.json")};
            Bridge=new RuntimeBridge(Composer,Hip);Composer.NativeParent=Bridge.Female;
            Session=new PlacementSession(Bridge,Store,Scene,Program.Origin(RigidPose.Identity));
            _hooks=new RuntimeHooks(Bridge,typeof(NativeScene));
        }
        public void Dispose(){Session.Stop();_hooks.Dispose();}
    }
    internal static class SaveTests
    {
        internal static void Run()
        {
            using(var env=new TestEnvironment("save"))
            {
                var a=Program.Pose(3,4,5,40);env.Bridge.Female.SetPositionAndRotation(a.position,a.rotation);
                env.Composer.NextData.localPosition=new Vector3(.2f,.7f,-.5f);env.Composer.SaveCurrentToPreset(null,"kijoui",0);
                var preset=env.Composer._presets.Single();var snapshot=env.Bridge.ComposerSnapshot(preset);
                var first=env.Store.Match(env.Bridge.ComposerPath,snapshot,snapshot.Parts[0]);
                Program.Assert(first!=null,"new Composer save recorded automatically");Program.Near(first.source.femaleRoot.ToPose(),a,"new source pose");
                var reopened=new OriginStore(Path.Combine(env.Root,"Origins"));var persisted=reopened.Match(env.Bridge.ComposerPath,snapshot,snapshot.Parts[0]);
                Program.Near(persisted.source.femaleRoot.ToPose(),a,"actual production serializer roundtrip");Program.Near(persisted.source.parents.Single().world.ToPose(),a,"parent pose persisted");
                Program.Assert(persisted.savedElements.Length==1&&persisted.source.mapKey.Contains("room"),"map and per-object payload persisted");
                var b=Program.Pose(-6,1,9,130);env.Bridge.Female.SetPositionAndRotation(b.position,b.rotation);env.Composer.NextData.localPosition=new Vector3(.4f,.8f,0);
                env.Composer.SaveCurrentToPreset(preset,null,1);snapshot=env.Bridge.ComposerSnapshot(preset);
                Program.Near(env.Store.Match(env.Bridge.ComposerPath,snapshot,snapshot.Parts[0]).source.femaleRoot.ToPose(),b,"L1 overwrite updates L1 source");
                Program.Near(env.Store.Match(env.Bridge.ComposerPath,snapshot,snapshot.Parts[1]).source.femaleRoot.ToPose(),a,"L1 overwrite retains L2 source");
                preset.displayName="renamed";env.Composer.SavePresets();snapshot=env.Bridge.ComposerSnapshot(preset);
                Program.Near(env.Store.Match(env.Bridge.ComposerPath,snapshot,snapshot.Parts[1]).source.femaleRoot.ToPose(),a,"rename does not replace source");
                env.Composer.SaveCurrentToPreset(preset,null,2);snapshot=env.Bridge.ComposerSnapshot(preset);
                Program.Near(env.Store.Match(env.Bridge.ComposerPath,snapshot,snapshot.Parts[0]).source.femaleRoot.ToPose(),b,"renamed L2 save keeps L1 source");
                Program.Near(env.Store.Match(env.Bridge.ComposerPath,snapshot,snapshot.Parts[1]).source.femaleRoot.ToPose(),b,"L2 overwrite updates L2 source");
                string before=File.ReadAllText(env.Bridge.ComposerPath);env.Composer.FailWrite=true;env.Composer.NextData.localPosition=new Vector3(77,2,9);env.Composer.SaveCurrentToPreset(preset,null,1);
                Program.Assert(!env.Composer._presetSaveSucceeded&&File.ReadAllText(env.Bridge.ComposerPath)==before,"failed body save leaves file unchanged");
                var disk=PresetDocuments.Collection(PresetDocuments.Composer,before).Single();
                Program.Assert(env.Store.Match(env.Bridge.ComposerPath,disk,disk.Parts[0])!=null,"failed body save retains previous metadata");
                Program.Assert(env.Store.Match(env.Bridge.ComposerPath,env.Bridge.ComposerSnapshot(preset),env.Bridge.ComposerSnapshot(preset).Parts[0])==null,"changed failed candidate cannot use stale source");
                Program.Assert(Directory.GetFiles(Path.Combine(env.Root,"Origins"),"*.pending-*").Length==0,"failed save aborts pending metadata");
                env.Composer.FailWrite=false;env.Composer._presetSource=preset;env.Composer._presetDraft=new ComposerPreset{displayName="renamed",loop1Json=preset.loop1Json,loop2Json=preset.loop2Json};
                Program.Assert(env.Composer.CommitPresetDraft(true,null),"editor overwrite succeeds");
                Program.Assert(env.Composer.CommitPresetDraft(true,"new-editor"),"editor new-save succeeds");
                var saved=env.Bridge.ComposerSnapshot(env.Composer._presets.Last());Program.Assert(env.Store.Match(env.Bridge.ComposerPath,saved,saved.Parts[0])!=null,"editor new-save records provenance");
                env.Hip.SaveCurrentPosePresetWithScreenshot("hands");var hp=env.Hip._posePresets.Single();var hs=env.Bridge.HipSnapshot(hp);
                Program.Assert(hs.Parts.All(p=>env.Store.Match(env.Bridge.HipPath,hs,p)!=null),"Hip new save records each IK");
                env.Bridge.Female.SetPositionAndRotation(a.position,a.rotation);env.Hip.NextPreset=new HipPreset();env.Hip.OverwritePosePresetById(hp.id);hp=env.Hip._posePresets.Single();hs=env.Bridge.HipSnapshot(hp);
                Program.Near(env.Store.Match(env.Bridge.HipPath,hs,hs.Parts[0]).source.femaleRoot.ToPose(),a,"Hip overwrite updates source even identical coordinates");
                string hipBefore=File.ReadAllText(env.Bridge.HipPath);File.SetAttributes(env.Bridge.HipPath,FileAttributes.ReadOnly);
                try{env.Hip.NextPreset=new HipPreset();env.Hip.NextPreset.entries[0].proxyPosition=new Vector3(88,0,0);env.Hip.OverwritePosePresetById(hp.id);}
                finally{File.SetAttributes(env.Bridge.HipPath,FileAttributes.Normal);}
                Program.Assert(File.ReadAllText(env.Bridge.HipPath)==hipBefore,"swallowed Hip write failure cannot commit metadata");
                var hipDisk=PresetDocuments.Collection(PresetDocuments.Hip,hipBefore).Single();Program.Assert(env.Store.Match(env.Bridge.HipPath,hipDisk,hipDisk.Parts[0])!=null,"Hip failure retains original source");
                int count=Directory.GetFiles(Path.Combine(env.Root,"Origins"),"*.json").Length;env.Session.Stop();env.Hip.SaveCurrentPosePresetWithScreenshot("after-stop");
                Program.Assert(Directory.GetFiles(Path.Combine(env.Root,"Origins"),"*.json").Length==count,"session invalidation stops save metadata");
            }
            StoreFailureTests();
        }
        private static void StoreFailureTests()
        {
            string root=Program.DirectoryFor("store-fail");string document=Path.Combine(root,"preset.json");var source=Program.Origin(Program.Pose(7,8,9,80));
            var preset=PresetDocuments.ComposerPreset(JsonUtility.ToJson(new ComposerPreset{loop1Json=new System.Collections.Generic.List<string>{JsonUtility.ToJson(new Data())}}));
            var store=new OriginStore(Path.Combine(root,"origins"));
            using(var pending=store.Prepare(document,preset,source,new[]{"loop1Json"}))pending.Commit();
            string recordPath=Directory.GetFiles(Path.Combine(root,"origins"),"*.json").Single();
            using(var pending=store.Prepare(document,preset,Program.Origin(Program.Pose(30,0,0,180)),new[]{"loop1Json"}))
            {
                File.Move(recordPath,recordPath+".kept");Directory.CreateDirectory(recordPath);
                Program.Throws(()=>pending.Commit(),"metadata commit failure");
            }
            Program.Assert(store.Match(document,preset,preset.Parts[0])==null,"commit failure blocks matching stale record even identical content");
            Directory.Delete(recordPath);File.Move(recordPath+".kept",recordPath);
            var reopened=new OriginStore(Path.Combine(root,"origins"));Program.Assert(reopened.Match(document,preset,preset.Parts[0])==null,"pending journal blocks stale record after restart");
            using(var pending=reopened.Prepare(document,preset,source,new[]{"loop1Json"}))pending.Commit();
            Program.Assert(reopened.Match(document,preset,preset.Parts[0])!=null,"successful resave repairs pending provenance");
            var reread=new OriginStore(Path.Combine(root,"origins"));Program.Assert(reread.Match(document,preset,preset.Parts[0])!=null,"repair remains valid after restart");
        }
    }
}
