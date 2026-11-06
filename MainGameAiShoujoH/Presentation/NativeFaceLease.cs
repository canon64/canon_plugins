using System;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MainGameAiShoujoH.Presentation
{
    public sealed class NativeFaceRequest
    {
        public int Eyes, Brows, Mouth;
        public float? BrowOpen;
        public float EyeOpen, MouthMin, MouthMax, Tear, Cheek;
        public bool Highlight, Blink;
    }
    #pragma warning disable CS0649
    [DataContract] internal sealed class FaceTarget { [DataMember] public string renderer, mesh; [DataMember] public int vertexCount; [DataMember] public string[] channelNames; }
    [DataContract] internal sealed class FaceFrame { [DataMember] public string role, renderer, mesh; [DataMember] public int vertexCount, pattern, endpoint; [DataMember] public int[] indices; [DataMember] public float[] delta; }
    [DataContract] internal sealed class FaceData { [DataMember] public int version; [DataMember] public string sourceRoot, targetRoot; [DataMember] public FaceTarget[] targets; [DataMember] public FaceFrame[] frames; }
    #pragma warning restore CS0649
    /// <summary>Session-local added native endpoints; original assets and card pattern IDs are never changed.</summary>
    internal sealed class NativeFaceLease : IDisposable
    {
        private sealed class TargetSnapshot { internal SkinnedMeshRenderer Renderer; internal Mesh Original, Clone; }
        private sealed class ControllerSnapshot
        {
            internal FBSBase Controller;
            internal FBSTargetInfo.CloseOpen[][] Patterns;
            internal float Min, Max, Fixed;
            internal int Offset, Count;
        }
        private readonly ChaControl actor;
        private readonly List<TargetSnapshot> targets = new List<TargetSnapshot>();
        private readonly Dictionary<string, ControllerSnapshot> controllers = new Dictionary<string, ControllerSnapshot>(StringComparer.Ordinal);
        private readonly bool blink, highlightHidden, mouthDisabled;
        private readonly float cheek;
        private bool disposed;
        internal NativeFaceLease(ChaControl actor, string directory)
        {
            this.actor = actor;
            blink = actor.fileStatus.eyesBlink; highlightHidden = actor.fileStatus.hideEyesHighlight; cheek = actor.fileStatus.hohoAkaRate; mouthDisabled = actor.fileStatus.disableMouthShapeMask;
            var face = actor.objHead == null ? null : actor.objHead.GetComponent<FaceBlendShape>();
            if (face == null) throw new InvalidOperationException("Native FBS actor head is unavailable.");
            var data = MainGameAiShoujoH.Motion.DedicatedMotionModule.ReadCatalog<FaceData>(Path.Combine(directory, "female00-retarget.json"));
            if (data?.version != 1 || data.frames == null || data.targets == null) throw new InvalidDataException("Native FBS transfer data is unavailable.");
            try
            {
                var roles = new Dictionary<string, FBSBase> { { "EyebrowCtrl", face.EyebrowCtrl }, { "EyesCtrl", face.EyesCtrl }, { "MouthCtrl", face.MouthCtrl } };
                foreach (var role in roles)
                {
                    var control = role.Value;
                    if (control?.FBSTarget == null || control.FBSTarget.Length == 0) throw new InvalidOperationException("Native FBS target role is unavailable: " + role.Key);
                    int originalCount = control.FBSTarget[0].PtnSet.Length;
                    int count = data.frames.Where(f => f.role == role.Key).Max(f => f.pattern) + 1;
                    var snapshot = new ControllerSnapshot { Controller = control, Patterns = control.FBSTarget.Select(t => t.PtnSet).ToArray(), Min = control.OpenMin, Max = control.OpenMax, Fixed = control.FixedRate, Offset = originalCount, Count = count };
                    controllers.Add(role.Key, snapshot);
                    foreach (var target in control.FBSTarget)
                    {
                        if (target.PtnSet.Length != originalCount || target.ObjTarget == null) throw new InvalidDataException("Native FBS pattern target count is inconsistent.");
                        var renderer = target.ObjTarget.GetComponent<SkinnedMeshRenderer>();
                        var definition = data.targets.SingleOrDefault(t => t.renderer == target.ObjTarget.name) ?? throw new InvalidDataException("Unsupported FBS renderer: " + target.ObjTarget.name);
                        if (renderer == null || renderer.sharedMesh == null) throw new InvalidDataException("FBS renderer/mesh is missing: " + target.ObjTarget.name);
                        if (renderer.sharedMesh.vertexCount != definition.vertexCount)
                        {
                            if (role.Key != "EyesCtrl" || (target.ObjTarget.name != "cf_Ohitomi_L" && target.ObjTarget.name != "cf_Ohitomi_R"))
                                throw new InvalidDataException("FBS target vertex layout differs: " + target.ObjTarget.name + " expected=" + definition.vertexCount + " actual=" + renderer.sharedMesh.vertexCount);
                            // The live card's alternate iris topology is not the standard 107-vertex iris.
                            // Keep its existing KKS expression pair and native Close/Open blink driver; never apply foreign vertex deltas.
                            int current = actor.fileStatus.eyesPtn;
                            if (current < default(int) || current >= target.PtnSet.Length) throw new InvalidDataException("Current KKS iris expression is unavailable.");
                            var original = target.PtnSet[current];
                            if (original.Close >= renderer.sharedMesh.blendShapeCount || original.Open >= renderer.sharedMesh.blendShapeCount)
                                throw new InvalidDataException("Current KKS iris expression exceeds its live mesh.");
                            var compatible = new FBSTargetInfo.CloseOpen[originalCount + count]; Array.Copy(target.PtnSet, compatible, originalCount);
                            for (int pattern = default(int); pattern < count; pattern++) compatible[originalCount + pattern] = new FBSTargetInfo.CloseOpen { Close = original.Close, Open = original.Open };
                            target.PtnSet = compatible;
                            continue;
                        }
                        var stored = targets.FirstOrDefault(t => t.Renderer == renderer);
                        if (stored == null)
                        {
                            var mesh = renderer.sharedMesh;
                            FaceChannelLayout.Validate(target.ObjTarget.name, definition.channelNames, Enumerable.Range(default(int), mesh.blendShapeCount).Select(mesh.GetBlendShapeName).ToArray());
                            stored = new TargetSnapshot { Renderer = renderer, Original = mesh, Clone = UnityEngine.Object.Instantiate(mesh) }; stored.Clone.name = mesh.name + " AI H session";
                            targets.Add(stored); renderer.sharedMesh = stored.Clone;
                        }
                        var patterns = new FBSTargetInfo.CloseOpen[originalCount + count]; Array.Copy(target.PtnSet, patterns, originalCount);
                        for (int pattern = 0; pattern < count; pattern++)
                        {
                            var pair = new FBSTargetInfo.CloseOpen();
                            for (int endpoint = 0; endpoint < 2; endpoint++)
                            {
                                var frame = data.frames.Single(f => f.role == role.Key && f.renderer == target.ObjTarget.name && f.pattern == pattern && f.endpoint == endpoint);
                                if (frame.indices.Length * 3 != frame.delta.Length) throw new InvalidDataException("Invalid sparse FBS frame.");
                                var delta = new Vector3[definition.vertexCount];
                                for (int i = 0; i < frame.indices.Length; i++)
                                {
                                    int v = frame.indices[i]; if (v < 0 || v >= delta.Length) throw new InvalidDataException("FBS sparse vertex exceeds target mesh.");
                                    delta[v] = new Vector3(frame.delta[i * 3], frame.delta[i * 3 + 1], frame.delta[i * 3 + 2]);
                                }
                                int index = stored.Clone.blendShapeCount;
                                stored.Clone.AddBlendShapeFrame("AIH_" + role.Key + "_" + pattern + "_" + endpoint, 100, delta, null, null);
                                if (endpoint == 0) pair.Close = index; else pair.Open = index;
                            }
                            patterns[originalCount + pattern] = pair;
                        }
                        target.PtnSet = patterns;
                    }
                }
            }
            catch { Dispose(); throw; }
        }
        internal void Apply(NativeFaceRequest request)
        {
            if (disposed) throw new ObjectDisposedException(nameof(NativeFaceLease));
            Set("EyebrowCtrl", request.Brows); Set("EyesCtrl", request.Eyes); Set("MouthCtrl", request.Mouth);
            if (request.BrowOpen.HasValue) controllers["EyebrowCtrl"].Controller.OpenMax = request.BrowOpen.Value;
            controllers["EyesCtrl"].Controller.OpenMax = request.EyeOpen;
            controllers["MouthCtrl"].Controller.OpenMin = request.MouthMin; controllers["MouthCtrl"].Controller.OpenMax = request.MouthMax;
            actor.ChangeEyesBlinkFlag(request.Blink); actor.HideEyeHighlight(!request.Highlight); actor.ChangeHohoAkaRate(request.Cheek); actor.DisableShapeMouth(request.Mouth == 10 || request.Mouth == 13);
        }
        private void Set(string role, int pattern)
        {
            var controller = controllers[role];
            if (pattern < 0 || pattern >= controller.Count) throw new InvalidDataException("Native FBS source pattern is out of range: " + role + ":" + pattern);
            controller.Controller.ChangePtn(controller.Offset + pattern, true);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            var errors = new List<Exception>();
            void Restore(Action restore) { try { restore(); } catch (Exception ex) { errors.Add(ex); } }
            foreach (var controller in controllers.Values)
            {
                Restore(() => controller.Controller.ChangePtn(0, false));
                for (int i = 0; i < controller.Patterns.Length; i++)
                { int index = i; Restore(() => controller.Controller.FBSTarget[index].PtnSet = controller.Patterns[index]); }
                Restore(() => controller.Controller.OpenMin = controller.Min);
                Restore(() => controller.Controller.OpenMax = controller.Max);
                Restore(() => controller.Controller.FixedRate = controller.Fixed);
            }
            foreach (var target in targets)
            {
                Restore(() => { if (target.Renderer != null) target.Renderer.sharedMesh = target.Original; });
                Restore(() => { if (target.Clone != null) UnityEngine.Object.Destroy(target.Clone); });
            }
            if (actor != null)
            {
                Restore(() => actor.ChangeEyesBlinkFlag(blink)); Restore(() => actor.HideEyeHighlight(highlightHidden));
                Restore(() => actor.ChangeHohoAkaRate(cheek)); Restore(() => actor.DisableShapeMouth(mouthDisabled));
            }
            targets.Clear(); controllers.Clear();
            if (errors.Count != 0) throw new AggregateException("AI face lease restoration failures.", errors);
        }
    }
}
