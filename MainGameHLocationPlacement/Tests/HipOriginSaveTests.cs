using System;
using System.IO;
using System.Linq;
using MainGameHLocationCoordinates;
using UnityEngine;

namespace MainGameHLocationPlacement
{
    internal static class HipOriginSaveTests
    {
        internal static void Run()
        {
            using(var e=new TestEnvironment("hip-real-save-order"))
            {
                var before=Program.Pose(1,0,2,0);var actual=Program.Pose(-7,2,11,90);
                e.Bridge.Female.SetPositionAndRotation(before.position,before.rotation);
                e.Hip.BeforeSnapshot=()=>e.Bridge.Female.SetPositionAndRotation(actual.position,actual.rotation);
                e.Hip.SaveCurrentPosePresetWithScreenshot("new");
                var first=e.Hip._posePresets.Single();var saved=e.Bridge.HipSnapshot(first);
                var reopened=new OriginStore(Path.Combine(e.Root,"Origins"));
                foreach(var part in saved.Parts)
                {
                    var origin=reopened.Match(e.Bridge.HipPath,saved,part);
                    Program.Assert(origin!=null,"new Hip save persists each IK origin");
                    Program.Near(origin.source.femaleRoot.ToPose(),actual,"Hip origin captured after native runtime preparation");
                    Program.Assert(origin.source.mapKey==e.Session.Source.mapKey&&origin.source.placeId==e.Session.Source.placeId&&origin.source.poseKey=="1/4","Hip origin preserves map, H place, and its own pose key");
                    Program.Assert(origin.source.parents.Count==0,"Hip save does not depend on Composer parent graphs");
                }
                // Native EnsurePosePresetsLoaded runs inside overwrite, after our prefix.
                e.Hip._posePresets.Clear();e.Hip.PrepareRuntime=()=>{if(e.Hip._posePresets.Count==0)e.Hip._posePresets.Add(first);};
                var overwritten=Program.Pose(14,3,-4,180);
                e.Hip.BeforeSnapshot=()=>e.Bridge.Female.SetPositionAndRotation(overwritten.position,overwritten.rotation);
                e.Hip.NextPreset=new HipPreset();e.Hip.OverwritePosePresetById(first.id);
                saved=e.Bridge.HipSnapshot(e.Hip._posePresets.Single());
                foreach(var part in saved.Parts)Program.Near(e.Store.Match(e.Bridge.HipPath,saved,part).source.femaleRoot.ToPose(),overwritten,"lazy-loaded Hip overwrite records the new source");
                // Index normalization/reordering must not attach the new source to another preset.
                e.Hip.NextPreset=new HipPreset();string newId=e.Hip.NextPreset.id;
                e.Hip.BeforeWrite=()=>e.Hip._posePresets.Reverse();
                e.Hip.SaveCurrentPosePresetWithScreenshot("reordered");
                var newSaved=e.Bridge.HipSnapshot(e.Hip._posePresets.Single(p=>p.id==newId));
                Program.Assert(newSaved.Parts.All(p=>e.Store.Match(e.Bridge.HipPath,newSaved,p)!=null),"Hip source follows stable ID when list order changes");
                string disk=File.ReadAllText(e.Bridge.HipPath);
                e.Session.FailHipCapture=true;e.Hip.BeforeWrite=null;e.Hip.NextPreset=new HipPreset();
                e.Hip.OverwritePosePresetById(first.id);
                Program.Assert(File.ReadAllText(e.Bridge.HipPath)==disk,"missing source blocks body write rather than silently saving without origin");
                var previous=PresetDocuments.Collection(PresetDocuments.Hip,disk).Single(p=>p.Identity==first.id);
                Program.Assert(previous.Parts.All(p=>e.Store.Match(e.Bridge.HipPath,previous,p)!=null),"failed source capture retains previous provenance");
                e.Session.FailHipCapture=false;
            }
        }
    }
}
