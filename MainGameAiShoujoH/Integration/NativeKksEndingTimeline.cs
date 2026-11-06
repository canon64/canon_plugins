using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using MainGameAiShoujoH.Contracts;
using MainGameAiShoujoH.Presentation;
using UnityEngine;

namespace MainGameAiShoujoH.Integration
{
    // Uses the same gotoADV and Program.FindADVBundleFilePath lookup as HScene.
    // Script body commands are not selected from the AI posture/pose numbers.
    internal sealed class NativeKksEndingTimeline : EndingCommandMachine.IHost, IDisposable
    {
        [DataContract] sealed class Policy
        {
            [DataMember] public int version { get; set; }
            [DataMember] public int directBundleFinishMinimum { get; set; }
            [DataMember] public int directBundleFinishMaximum { get; set; }
            [DataMember] public string personality { get; set; }
            [DataMember] public string sourceSha256 { get; set; }
        }
        sealed class Script { internal string Bundle, Name; internal EndingScript Body; }
        readonly Dictionary<int, Script> scripts = new Dictionary<int, Script>();
        readonly SessionContext context;
        readonly IKksEndingMotionModule motion;
        readonly PresentationModule presentation;
        EndingCommandMachine machine;
        Action resume;
        Exception failure;
        bool started, completed, disposed;
        int checkpoint, bodyVersion, checkpointBodyVersion;
        internal string Scenario { get; private set; }
        internal int? FinishAdv { get; private set; }
        internal bool Completed => completed && failure == null;
        internal NativeKksEndingTimeline(string policyPath, SessionContext context, NativeKksSessionRuntime runtime, IKksEndingMotionModule motion, PresentationModule presentation)
        {
            this.context = context; this.motion = motion; this.presentation = presentation;
            var policy = Motion.DedicatedMotionModule.ReadCatalog<Policy>(policyPath);
            if (policy == null || policy.version != 1 || policy.personality != KksSexyVoicePlayback.Personality || string.IsNullOrWhiteSpace(policy.sourceSha256)) throw new InvalidDataException("Native KKS ending lookup policy is missing");
            if (runtime?.Heroine == null || runtime.Heroine.ChaName != policy.personality) throw new InvalidOperationException("Native c00 ending lookup requires the shared transient c00 Heroine");
            var info = new Dictionary<int, GotoADVData.Param>();
            var bundles = CommonLib.GetAssetBundleNameListFromPath("h/list/"); bundles.Sort();
            foreach (var bundle in bundles)
            {
                if (!AssetBundleCheck.IsFile(bundle, "gotoADV") || !HFlag.BundleCheck(bundle, "gotoADV")) continue;
                try
                {
                    var table = CommonLib.LoadAsset<GotoADVData>(bundle, "gotoADV");
                    if (table == null) continue;
                    foreach (var row in table.param) info[row.finishADV] = new GotoADVData.Param { finishADV = row.finishADV, bundle = row.bundle, adv = row.adv };
                }
                finally { AssetBundleManager.UnloadAssetBundle(bundle, isUnloadForceRefCount: false); }
            }
            if (info.Count == default(int)) throw new InvalidDataException("Installed KKS gotoADV table is missing");
            foreach (var pair in info)
            {
                var row = pair.Value;
                string bundle = row.finishADV >= policy.directBundleFinishMinimum && row.finishADV <= policy.directBundleFinishMaximum ? row.bundle : ADV.Program.FindADVBundleFilePath(row.adv, runtime.Heroine);
                if (string.IsNullOrWhiteSpace(bundle) || string.IsNullOrWhiteSpace(row.adv)) throw new InvalidDataException("Native KKS ending script locator is missing for finishADV " + row.finishADV);
                try
                {
                    var source = CommonLib.LoadAsset<ADV.ScenarioData>(bundle, row.adv);
                    if (source == null || source.list == null) throw new InvalidDataException("Native KKS ending scenario missing: " + bundle + "/" + row.adv);
                    var rows = source.list.Select(p => new EndingRow { hash = p.Hash, version = p.Version, multi = p.Multi, command = p.Command.ToString(), args = (p.Args ?? new string[0]).ToArray() }).ToArray();
                    scripts.Add(pair.Key, new Script { Bundle = bundle, Name = row.adv, Body = new EndingScript { name = row.adv, sourceBundle = bundle, rows = rows } });
                }
                finally { AssetBundleManager.UnloadAssetBundle(bundle, isUnloadForceRefCount: false); }
            }
        }
        internal void Select(int? finishAdv, IReadOnlyDictionary<string, object> variables)
        {
            if (disposed || machine != null || !finishAdv.HasValue) throw new InvalidOperationException("Native finishADV has not been computed or ending selection already occurred");
            if (!scripts.TryGetValue(finishAdv.Value, out var selected)) throw new InvalidDataException("Native gotoADV has no exact finishADV " + finishAdv.Value);
            // Unknown control/body commands remain errors. Only presentation and
            // native save commands are omitted: AI content/Receive owns those.
            var inert = new HashSet<string>(new[] { "Voice", "CharaVoiceStop", "ParameterFile", "SceneFade", "SceneFadeRegulate", "BGM", "SE" }, StringComparer.Ordinal);
            var rows = selected.Body.rows.Where(row => !inert.Contains(row.command)).Select(row => new EndingRow {
                hash = row.hash, version = row.version, multi = row.multi, args = row.args,
                // Native Motion/Expression queue an update for the immediately
                // following Text. This body-only adapter applies it before that
                // same checkpoint, without executing the native text/audio.
                command = row.command == "Motion" ? "CharaMotion" : row.command == "Expression" ? "CharaExpression" : row.command
            }).ToArray();
            selectedBody = new EndingScript { name = selected.Name, sourceBundle = selected.Bundle, rows = rows };
            FinishAdv = finishAdv; Scenario = selected.Bundle + "/" + selected.Name;
            machine = new EndingCommandMachine(this, variables == null ? new Dictionary<string, object>() : variables.ToDictionary(p => p.Key, p => p.Value), UnityEngine.Random.Range);
        }
        EndingScript selectedBody;
        internal void AdvanceCaption()
        {
            if (disposed || machine == null) throw new InvalidOperationException("Native ending body timeline was not selected");
            if (failure != null) throw failure;
            if (completed) return; // Extra AI captions retain the last exact native pose.
            if (!started) { started = true; machine.Start(selectedBody, () => completed = true, error => failure = error); }
            else { var next = resume ?? throw new InvalidOperationException("Native ending body checkpoint is unavailable"); resume = null; next(); }
            if (failure != null) throw failure;
        }
        internal void ValidateFinish()
        {
            if (!started) throw new InvalidOperationException("Native ending body timeline had no corresponding AI caption checkpoint");
            if (!completed && resume != null) { var next = resume; resume = null; next(); }
            if (failure != null) throw failure;
            if (!completed) throw new InvalidOperationException("AI captions ended before native body checkpoints were consumed; checkpoint=" + checkpoint);
        }
        public void Present(EndingCue cue, Action done, Action<Exception> failed)
        {
            if (resume != null) throw new InvalidOperationException("Native ending body checkpoint overlaps");
            // Native dialogue-only replies do not invent another body phase.
            // Keep every actual body/face/gaze change in source order.
            if (checkpoint != default(int) && bodyVersion == checkpointBodyVersion) { done(); return; }
            ++checkpoint; checkpointBodyVersion = bodyVersion; resume = done;
        }
        public void ActorCommand(string command, string[] args)
        {
            if (args == null || args.Length == default(int)) throw new InvalidDataException("Native ending actor arguments are missing");
            int slot = int.Parse(args[0], CultureInfo.InvariantCulture);
            var actors = context.Request.Participants.Where(p => p.Role != "male").ToArray();
            if (slot < default(int) || slot >= actors.Length) throw new NotSupportedException("Native ending actor slot has no dedicated female owner: " + slot);
            var actor = actors[slot].Character;
            switch (command)
            {
                case "CharaMotion":
                    if (args.Length < 2 || args.Skip(2).Any(value => !string.IsNullOrEmpty(value))) throw new NotSupportedException("Native ending motion requires an explicit bundle/IK/override contract: " + string.Join("|", args));
                    motion.PlayNativeEndingMotion(slot, args[1]); break;
                case "CharaExpression": presentation.ApplyNativeEndingExpression(slot, args[1]); break;
                case "CharaLookEyes": actor.ChangeLookEyesPtn(int.Parse(args[1], CultureInfo.InvariantCulture)); break;
                case "CharaLookNeck": actor.ChangeLookNeckPtn(int.Parse(args[1], CultureInfo.InvariantCulture), args.Length > 2 ? float.Parse(args[2], CultureInfo.InvariantCulture) : 1); break;
                default: throw new NotSupportedException("Native ending body command is not connected: " + command);
            }
            ++bodyVersion;
        }
        public void Dispose() { disposed = true; resume = null; machine?.Dispose(); machine = null; }
    }
}
