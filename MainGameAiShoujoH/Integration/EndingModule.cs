using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MainGameAiShoujoH.Contracts;
using MainGameAiShoujoH.Presentation;
using MainGameAiShoujoH.Progression;
using MainGameAiShoujoH.Results;
using UnityEngine;

namespace MainGameAiShoujoH.Integration
{
    public sealed class EndingModule : ISessionModule, ISessionFinalizer, ISessionEndingStatus, EndingCommandMachine.IHost
    {
        readonly string directory;
        readonly IMotionModule motion;
        readonly PresentationModule presentation;
        readonly ProgressionModule progression;
        readonly Func<bool> permitted;
        readonly NativeKksSessionRuntime nativeRuntime;
        NativeKksEndingTimeline nativeTimeline;
        SessionContext context; EndingCatalog catalog; EndingCommandMachine machine;
        bool disposed, finalizing; IDisposable music; NativeEndingCameraData cameraData; NativeEndingCameraLease cameraLease;
        public string Name => "ending-adv";
        public string EndingScenario { get; private set; }
        public bool EndingReceiveSaved { get; private set; }
        public bool EndingCompleted { get; private set; }
        public EndingModule(string directory, IMotionModule motion, PresentationModule presentation, ProgressionModule progression, Func<bool> permitted)
        { this.directory = directory; this.motion = motion; this.presentation = presentation; this.progression = progression; this.permitted = permitted; }
        internal EndingModule(string directory, IMotionModule motion, PresentationModule presentation, ProgressionModule progression, Func<bool> permitted, NativeKksSessionRuntime runtime)
            : this(directory, motion, presentation, progression, permitted) { nativeRuntime = runtime; }
        public string NativeBodyScenario => nativeTimeline?.Scenario;
        public int? NativeFinishAdv => nativeTimeline?.FinishAdv;
        public bool NativeBodyTimelineCompleted => nativeTimeline?.Completed == true;
        public void Prepare(SessionContext value)
        {
            context = value;
            cameraData = NativeEndingCameraData.Load(Path.Combine(directory, "camera.json"));
            if (value.Request.ReadNativeParameter == null || value.Request.Results?.Apply == null) throw new InvalidOperationException("終了ADVの実住人parameter/Receive接続がありません。");
            catalog = Motion.DedicatedMotionModule.ReadCatalog<EndingCatalog>(Path.Combine(directory, "catalog.json"));
            if (catalog?.scripts == null || catalog.scripts.Length != 91 || string.IsNullOrWhiteSpace(catalog.sourceSha256)) throw new InvalidDataException("終了ADVの91原本scriptがありません。");
            if (value.Request.KksPose != null)
            {
                if (!(motion is IKksEndingMotionModule)) throw new InvalidOperationException("KKS純正終了会話motionの実再生契約がありません。");
                foreach (var row in catalog.scripts.SelectMany(script => script.rows).Where(row => row.command == "Voice"))
                {
                    var arguments = new Queue<string>(row.args ?? throw new InvalidDataException("終了ADV Voice引数がありません。"));
                    int slot = int.Parse(arguments.Dequeue(), CultureInfo.InvariantCulture);
                    string bundle = arguments.Dequeue(), asset = arguments.Dequeue();
                    if (arguments.Any()) throw new InvalidDataException("終了ADV Voice引数が余っています。");
                    // Dialogue content remains AI's exact line; KKS Sexy moans are
                    // owned separately and never substitute unrelated dialogue.
                    if (slot < default(int)) throw new InvalidDataException("終了ADV Voice女性slotが不正です。");
                    if (slot < value.Request.Participants.Count(p => p.Role != "male")) presentation.ValidateEndingVoice(slot, bundle, asset);
                }
                nativeTimeline = new NativeKksEndingTimeline(Path.GetFullPath(Path.Combine(directory, "..", "HKks", "ending-native-policy.json")), value, nativeRuntime, (IKksEndingMotionModule)motion, presentation);
                return;
            }
            if (!(motion is IEndingMotionModule ending)) throw new InvalidOperationException("原本終了ADV motionの実再生契約がありません。");
            foreach (var row in catalog.scripts.SelectMany(script => script.rows).Where(row => row.command == "CharaMotion"))
            {
                if (row.args == null) throw new InvalidDataException("終了ADV CharaMotion引数がありません。");
                var arguments = new Queue<string>(row.args);
                var slot = int.Parse(arguments.Dequeue(), CultureInfo.InvariantCulture);
                var posture = int.Parse(arguments.Dequeue(), CultureInfo.InvariantCulture);
                var pose = int.Parse(arguments.Dequeue(), CultureInfo.InvariantCulture);
                if (arguments.Any()) throw new InvalidDataException("終了ADV CharaMotion引数が余っています。");
                ending.ValidateEndingMotion(slot, posture, pose);
            }
        }
        public void Execute(Command command) { }
        public void End(EndReason reason) { }
        public void BeginFinalization(EndReason reason, Action completed, Action<Exception> failed)
        {
            if (reason != EndReason.Completed) { completed(); return; }
            if (disposed || finalizing || !permitted()) throw new InvalidOperationException("終了ADVの世代・許可が失効しています。");
            if (context.Request.KksPose != null)
            {
                if (nativeTimeline == null || nativeRuntime == null) throw new InvalidOperationException("Native KKS ending timeline/runtime is missing");
                nativeTimeline.Select(nativeRuntime.FinishAdv, nativeRuntime.EndingVariables);
            }
            finalizing = true;
            music = context.Request.SuspendEndingMusic?.Invoke();
            var state = progression.CaptureEndingState(); var request = context.Request;
            var voice = request.VoiceContexts[0];
            cameraLease = new NativeEndingCameraLease(request, cameraData);
            if (!state.FemalePlayerOrgasmTotal.HasValue) throw new InvalidOperationException("女性主人公の終了結果が未接続です。");
            var selected = EndingSelection.Select(new EndingConditions {
                Event = (NativeHEvent)request.EventId, Merchant = voice.Merchant, Weakness = state.IsFaintness, Force = state.IsForce,
                SleepStart = state.SleepStart, SecondFemaleIsAgent = request.Participants.Any(p => p.Role == "female2" && !p.IsPlayer),
                HadControl = state.HadControlAction, PainSelected = state.PainSelected, ConstraintSelected = state.ConstraintSelected,
                PainAction = state.PainAction, ConstraintAction = state.ConstraintAction, InvitePattern = state.InvitePattern, Phase = voice.PersonalPhase,
                FemaleOrgasm = state.FemaleOrgasmTotal, MaleFinish = state.MaleOrgasmTotal, FemalePlayerFinish = state.FemalePlayerOrgasmTotal.Value,
                DirtySkill = state.Skill2, YobaiBareRate = state.YobaiBareRate }, UnityEngine.Random.Range(0, 100));
            EndingScenario = selected.CloseWithoutScenario ? "close-without-scenario" : selected.Scenario;
            if (selected.CloseWithoutScenario) { presentation.BeginEnding(); presentation.ApplyEndingExpression(0, "標準（目閉じ）", voice.Merchant ? default(int) : voice.Personality); Receive(completed); return; }
            var script = catalog.scripts.Single(s => s.personality == voice.Personality && s.name == selected.Scenario);
            var variables = new Dictionary<string, object> {
                ["H_StatsTable[1]"] = request.ReadNativeParameter(request.Participants[0].Identity, 1),
                ["H_DesireTable[1]"] = request.ReadNativeParameter(request.Participants[0].Identity, 101),
                ["isWeaknessH"] = selected.Weakness, ["JumpTag"] = selected.JumpTag, ["numOrgasmFemale"] = selected.FemaleOrgasm,
                ["ConditionMode"] = selected.ConditionMode, ["isPainAction"] = selected.PainAction, ["isConstraintAction"] = selected.ConstraintAction
            };
            if (voice.Merchant) variables["P_SpendMoney"] = request.ReadMerchantSpendTotal?.Invoke() ?? throw new InvalidOperationException("商人の原本累積取引実績の保存接続がありません。");
            presentation.BeginEnding();
            machine = new EndingCommandMachine(this, variables, UnityEngine.Random.Range);
            request.Reveal(() => {
                if (!Current()) return;
                machine.Start(script, () => { try { Receive(completed); } catch (Exception ex) { failed(ex); } }, failed);
            });
        }
        bool Current() => finalizing && !disposed && permitted() && context?.Request?.Owner != null;
        void Receive(Action completed)
        {
            if (!Current()) throw new InvalidOperationException("Receive前に終了ADVの許可が失効しました。");
            nativeTimeline?.ValidateFinish();
            if (machine != null)
            {
                foreach (var pair in new[] { new { Name = "H_StatsTable[1]", Tag = 1 }, new { Name = "H_DesireTable[1]", Tag = 101 } })
                {
                    float value = Convert.ToSingle(machine.Variables[pair.Name], CultureInfo.InvariantCulture);
                    var receipt = context.Request.Results.Apply(new ResultDelta { OperationId = "ai-h:" + context.Id.ToString("N") + ":adv-receive:" + pair.Name,
                        ParticipantId = context.Request.Participants[0].Identity, Parameter = pair.Tag, Assignment = true, AssignedValue = value });
                    if (receipt == null || !receipt.Applied || !receipt.Saved) throw new InvalidOperationException("終了ADV Receive保存失敗: " + receipt?.Error);
                }
            }
            // Native CloseADVProc resets H desire8 after Receive; Bath desire1 remains independent.
            foreach (var participant in context.Request.Participants.Where(p => !p.IsPlayer && p.Role != "male"))
            {
                var reset = context.Request.Results.Apply(new ResultDelta { OperationId = "ai-h:" + context.Id.ToString("N") + ":close:h-desire:" + participant.Identity,
                    ParticipantId = participant.Identity, Parameter = 108, Assignment = true, AssignedValue = 0 });
                if (reset == null || !reset.Applied || !reset.Saved) throw new InvalidOperationException("終了H欲求保存失敗: " + reset?.Error);
            }
            EndingReceiveSaved = true;
            presentation.EndEnding(); finalizing = false; var resume = music; music = null; resume?.Dispose(); EndingCompleted = true; completed();
        }
        public void Present(EndingCue cue, Action completed, Action<Exception> failed)
        {
            if (!Current()) { failed(new InvalidOperationException("終了ADVの表示世代が失効しました。")); return; }
            context.Request.Owner.StartCoroutine(PresentAfterPose(cue, completed, failed));
        }
        System.Collections.IEnumerator PresentAfterPose(EndingCue cue, Action completed, Action<Exception> failed)
        {
            Exception preparationError = null;
            try { if (nativeTimeline != null) nativeTimeline.AdvanceCaption(); else foreach (var movement in cue.Motions) PlayMotion(movement); } catch (Exception ex) { preparationError = ex; }
            if (preparationError != null) { failed(preparationError); yield break; }
            yield return new WaitForEndOfFrame();
            if (!Current()) { failed(new InvalidOperationException("終了ADVの表示世代が失効しました。")); yield break; }
            try
            {
                cameraLease?.Apply();
                if (nativeTimeline == null) foreach (var expression in cue.Expressions) presentation.ApplyEndingExpression(int.Parse(expression[0]), expression[1]);
                foreach (var voice in cue.Voices) presentation.PlayEndingVoice(int.Parse(voice[0]), voice[1], voice[2], () => { });
                presentation.PresentEndingText(cue.Speaker, cue.Text, () => { if (Current()) completed(); });
            }
            catch (Exception ex) { failed(ex); }
        }
        void PlayMotion(string[] args)
        {
            if (nativeTimeline != null) return; // AI body identifiers never select KKS states.
            if (!(motion is IEndingMotionModule ending)) throw new InvalidOperationException("原本終了ADV5motionの実再生契約がありません。");
            ending.PlayEndingMotion(int.Parse(args[0]), int.Parse(args[1]), int.Parse(args[2]));
        }
        public void ActorCommand(string command, string[] args)
        {
            if (!Current()) throw new InvalidOperationException("終了ADV actor操作の許可が失効しました。");
            if (nativeTimeline != null && command != "CharaVoiceStop") return; // Body/face/gaze follow the selected native script only.
            int slot = int.Parse(args[0]); var actor = context.Request.Participants.Where(p => p.Role != "male").ElementAt(slot).Character;
            switch (command)
            {
                case "CharaExpression": presentation.ApplyEndingExpression(slot, args[1]); break;
                case "CharaMotion": PlayMotion(args); break;
                case "CharaLookEyes": actor.ChangeLookEyesPtn(int.Parse(args[1])); break;
                case "CharaLookNeck": actor.ChangeLookNeckPtn(int.Parse(args[1]), args.Length > 2 ? float.Parse(args[2], CultureInfo.InvariantCulture) : 1); break;
                case "CharaVoiceStop": presentation.StopEndingVoice(slot); break;
                default: throw new NotSupportedException(command);
            }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; finalizing = false;
            machine?.Dispose(); machine = null; nativeTimeline?.Dispose(); nativeTimeline = null; context = null;
            try { presentation.EndEnding(); } finally { try { cameraLease?.Dispose(); cameraLease = null; } finally { var resume = music; music = null; resume?.Dispose(); } }
        }
    }
}
