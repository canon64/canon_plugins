using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MainGameHLocationCoordinates;
using UnityEngine;

namespace MainGameHLocationPlacement
{
    internal sealed class Plugin
    {
        public const string Guid = "test.production.hlocationplacement";
        internal static Plugin Instance = new Plugin();
        internal string Status;
        internal bool Diagnostics;
        internal bool CanShowStatus = true;
        internal int DiagnosticSettingWrites;
        internal readonly List<string> DiagnosticMessages = new List<string>();
        internal void SetDiagnostics(bool enabled) { Diagnostics = enabled; DiagnosticSettingWrites++; }
        internal void Message(string text) { Status = text; }
        internal void Diagnostic(Func<string> message) { if (Diagnostics) DiagnosticMessages.Add(message()); }
    }
    internal sealed class RuntimeBridge
    {
        internal readonly ComposerFixture Composer;
        internal readonly HipFixture Hip;
        internal readonly Transform Female = new GameObject("female").transform;
        internal RuntimeBridge(ComposerFixture composer, HipFixture hip) { Composer = composer; Hip = hip; }
        internal string[] IkKeys => Hip.Keys;
        internal IList ComposerPresets => Composer._presets;
        internal IList HipPresets => Hip._posePresets;
        internal string ComposerPath => Composer._presetsPath;
        internal string HipPath => Hip._posePresetIndexPath;
        internal bool MotionTarget(object preset,string id) => Composer.IsMotionTarget((ComposerPreset)preset,id);
        internal int MotionLevel => Composer._motionIntensityLevel;
        internal float MotionBlend => Composer.Motion;
        internal IDictionary Objects => Composer._runtimeObjects;
        internal Array States => Hip.States;
        internal bool Linked(int index) => IkEligibility.Linked(index, IkKeys, Hip._settings, Composer._activePreset?.ikLinks);
        internal bool Independent(int index, bool allowSavedFollow = false) => IkEligibility.Independent(States.GetValue(index), index, Linked(index), Hip._runtimeWrists, allowSavedFollow);
        internal SavedPreset ComposerSnapshot(object preset) => PresetDocuments.ComposerPreset(JsonUtility.ToJson(preset));
        internal SavedPreset HipSnapshot(object preset) => PresetDocuments.Collection(PresetDocuments.Hip, Hip.SingleJson((HipPreset)preset)).Single();
        internal object CloneHip(object preset) => new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<HipPreset>(JsonUtility.ToJson(preset));
        internal Transform ObjectTransform(string id) => Composer._runtimeObjects.TryGetValue(id, out var value) ? value.GameObject.transform : null;
        internal static RigidPose Pose(Transform t) => new RigidPose(t.position,t.rotation);
        internal static void Unit(Transform t) { for(var p=t;p!=null;p=p.parent)if((p.localScale-Vector3.one).sqrMagnitude>1e-8f)throw new InvalidOperationException("scale"); }
    }
    internal sealed partial class PlacementSession
    {
        internal static PlacementSession Current;
        internal readonly RuntimeBridge Bridge;
        private readonly Plugin _plugin = Plugin.Instance;
        private readonly OriginStore _store;
        private readonly object _scene;
        private string _placeId = "destination";
        private RigidPose? _destination;
        internal bool Valid = true;
        internal readonly PlaceOrigin Source;
        internal bool FailHipCapture;
        internal PlacementSession(RuntimeBridge bridge, OriginStore store, object scene, PlaceOrigin source)
        { Bridge=bridge;_store=store;_scene=scene;Source=source;Current=this;RefreshDestination(); }
        internal void Report(Exception ex) { _plugin.Message(ex.Message); }
        internal PlaceOrigin Capture()
        {
            return new PlaceOrigin { mapKey=Source.mapKey,mapName=Source.mapName,placeId=Source.placeId,placeName=Source.placeName,poseKey=Source.poseKey,utc=Source.utc,
                femaleRoot=PoseNumbers.From(RuntimeBridge.Pose(Bridge.Female)),mapRoot=Source.mapRoot,composerLinks=Source.composerLinks,
                parents=Source.parents.Select(p=>new ParentOrigin{objectId=p.objectId,kind=p.kind,reference=p.reference,
                    world=p.kind=="external"&&p.reference=="char:root"?PoseNumbers.From(RuntimeBridge.Pose(Bridge.Female)):p.world}).ToList() };
        }
        internal PlaceOrigin CaptureHip()
        {
            if (FailHipCapture) throw new InvalidOperationException("simulated missing Hip source");
            var source = Capture(); source.parents.Clear(); return source;
        }
        private void RefreshDestination() { RuntimeBridge.Unit(Bridge.Female);_destination=RuntimeBridge.Pose(Bridge.Female); }
        internal RigidPose Project(PlaceOrigin source,RigidPose value)
        { if(!Valid)throw new InvalidOperationException("expired");var space=new CoordinateSpace(source.femaleRoot.ToPose(),()=>{if(!Valid)throw new InvalidOperationException("expired");});space.UpdateDestination(_destination.Value);return space.Transform(value); }
        internal RigidPose ProjectMove(RigidPose before,RigidPose after,RigidPose value)
        { var space=new CoordinateSpace(before,()=>{if(!Valid)throw new InvalidOperationException("expired");});space.UpdateDestination(after);return space.Transform(value); }
        internal void Stop() { Valid=false;Current=null;ClearAnchors();_movements.Clear();_saves.Clear();_pendingNativeMove=null;_nativeDepth=0; }
    }
}
