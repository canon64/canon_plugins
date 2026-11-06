using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MainGameAiShoujoH.Contracts;
using MainGameAiShoujoH.Presentation;
using MainGameAiShoujoH.Results;

namespace MainGameAiShoujoH.Progression
{
    public sealed class ProgressionModule : ISessionModule, IMotionSelectionTransition
    {
        private readonly IMotionModule motion;
        private readonly PresentationModule presentation;
        private readonly ResultModule results;
        private readonly Integration.NativeKksSessionRuntime nativeRuntime;
        private NativeKksHoushiDriver nativeDriver;
        private bool NativeOwner => nativeRuntime?.Engine != null;
        private SessionContext context;
        private NativeProgression engine;
        private DriveSample sample;
        private UnityEngine.Coroutine deadline;
        private float lastTime;
        private bool running, selectionSuspended;
        private bool CanDrive => running && !selectionSuspended && context != null && context.IsCurrent() && context.CanInteract != null && context.CanInteract();
        private DedicatedControlPanel panel;
        private NativeFeelHit hit;
        private NativeAutoDriver auto;
        private string lastAutoState;
        private int totalOrgasm, totalFaintness, totalMaleOrgasm;
        private readonly HashSet<ProcKind> selectedFamilies = new HashSet<ProcKind>();
        private readonly HashSet<string> counts = new HashSet<string>(StringComparer.Ordinal);
        private bool operated, pain, constraint, hadAnal;
        private int selectedSystems, totalFemalePlayerOrgasm;
        private bool advancing, advanceQueued;
        private float queuedDelta;
        public string Name => "progression";
        public NativeProgression Engine => engine;
        public float ManualSpeed => nativeRuntime != null ? nativeRuntime.Flags.speedCalc : engine.Speed;
        public float ManualSpeedMaximum => nativeRuntime != null ? nativeRuntime.Protocol.clipEnd : engine.State.Contains("OLoop") || engine.Definition.Kind == ProcKind.Masturbation ? 1 : 2;
        public string CurrentMotionId => motion.SelectedMotionId;
        private string[] availableMotions = Array.Empty<string>();
        public string[] AvailableMotions => availableMotions;
        private void RefreshMotionChoices()
        {
            if (nativeRuntime != null) { availableMotions = motion is Integration.NativeKksOwnedScene owned ? owned.AvailableMotions : motion is Motion.DedicatedMotionModule kks ? kks.AvailableMotionIds : Array.Empty<string>(); return; }
            availableMotions = motion is Motion.DedicatedMotionModule native
                ? native.AvailableMotionIds.Where(id => NativeMotionEligibility.Check(NativeProcCatalog.Get(id), SelectionContext(), false) && presentation.ItemsReady(NativeProcCatalog.Get(id))).ToArray() : Array.Empty<string>();
        }
        private NativeMotionSelectionContext SelectionContext()
        {
            var player = context.Request.Participants.FirstOrDefault(p => p.IsPlayer);
            return new NativeMotionSelectionContext { PlayerSex = player == null ? (context.Request.SecondFemaleIsPlayer ? 1 : 0) : player.Character.sex,
                Futanari = context.Request.Settings.Futanari, SecondFemale = context.Request.Participants.Count(p => p.Role != "male") >= 2,
                Merchant = context.Request.VoiceContexts[0].Merchant, Force = context.Request.VoiceContexts[0].Force,
                Yobai = context.Request.VoiceContexts[0].Yobai, Faint = engine.Faint, Initiative = engine.Definition.Initiative,
                Place = context.Request.NativePlace, Unlock0 = context.Request.Settings.HentaiUnlock0, Unlock1 = context.Request.Settings.HentaiUnlock1,
                Skills = context.Request.VoiceContexts[0].Skills, HasItem = context.Request.HasNativeHItem };
        }
        public event Action<ProcEffect> Effect;
        public ProgressionModule(IMotionModule motion, PresentationModule presentation, ResultModule results)
        { this.motion = motion ?? throw new ArgumentNullException(nameof(motion)); this.presentation = presentation ?? throw new ArgumentNullException(nameof(presentation)); this.results = results ?? throw new ArgumentNullException(nameof(results)); }
        internal ProgressionModule(IMotionModule motion, PresentationModule presentation, ResultModule results, Integration.NativeKksSessionRuntime runtime) : this(motion, presentation, results) { nativeRuntime = runtime; }
        public void Prepare(SessionContext value)
        {
            context = value ?? throw new ArgumentNullException(nameof(value));
            engine = new NativeProgression(NativeProcCatalog.Get(context.Request.MotionId));
            if (NativeOwner) engine.AttachDriver(new NativeKksOwnerDriver(nativeRuntime));
            else if (nativeRuntime != null) { nativeDriver = new NativeKksHoushiDriver(nativeRuntime); engine.AttachDriver(nativeDriver); }
            engine.Definition.Initiative = context.Request.EventId == (int)NativeHEvent.FromFemale ? 1 : context.Request.EventId == (int)NativeHEvent.GyakuYobai ? 2 : 0;
            if (nativeDriver != null) engine.Definition.Initiative = default(int);
            SelectedAwards();
            var settings = context.Request.Settings ?? throw new InvalidOperationException("Native dedicated H settings are missing.");
            settings.Validate();
            sample = new DriveSample { StopFemale = settings.StopFemale, StopMale = settings.StopMale, Drug5 = settings.Drug5,
                FemaleSkill = settings.FemaleSkillMultiplier, MaleSkill = settings.MaleSkillMultiplier };
            foreach (var voice in context.Request.VoiceContexts) voice.Force = settings.Force;
            hit = new NativeFeelHit(UnityEngine.Random.Range, UnityEngine.Random.Range);
            auto = new NativeAutoDriver(UnityEngine.Random.Range, UnityEngine.Random.Range); lastAutoState = engine.State;
            lastTime = UnityEngine.Time.time;
            running = true;
            if (!NativeOwner) presentation.VoiceCompleted += OnVoiceComplete;
            if (motion is Motion.DedicatedMotionModule nativeMotion) { nativeMotion.AnimationDriven += OnAnimation; nativeMotion.SeCue += OnSoundEffect; }
            if (!NativeOwner) motion.Execute(new Command { Name = "state", Value = engine.State });
            if (NativeOwner) { RefreshMotionChoices(); return; } // Native HSprite owns the actual KKS controls.
            var ui = new UnityEngine.GameObject("AI H controls " + context.Id.ToString("N"));
            RefreshMotionChoices();
            panel = ui.AddComponent<DedicatedControlPanel>(); panel.Initialize(this, context, presentation);
            Reschedule();
        }
        private void OnAnimation(float cycle) { if (CanDrive) Execute(new Command { Name = "animation", Amount = cycle }); }
        private void OnSoundEffect(int cue)
        { if (!CanDrive) return; try { presentation.PlaySoundEffect(cue, motion.SelectedMotionId, engine.State); } catch (Exception ex) { context.ReportFailure("AI H SE: " + ex.Message); } }
        private void OnVoiceComplete(int slot)
        {
            if (!CanDrive) return;
            sample.PlayStart = -1;
            Integrate(); Advance(0); Reschedule();
        }
        public void Execute(Command command)
        {
            if (context == null || !context.IsCurrent() || command == null) return;
            bool motionPreparation = context.IsMotionPreparation != null && context.IsMotionPreparation();
            if (!CanDrive && !(motionPreparation && (command.Name == "motion" || command.Name == "auto-motion"))) return;
            if (NativeOwner)
            {
                if (command.Name == "motion") { ValidateMotionSelection(command.Value, false); return; }
                if (command.Name == "lock-female") { nativeRuntime.Flags.lockGugeFemale = command.Amount != default(float); return; }
                if (command.Name == "lock-male") { nativeRuntime.Flags.lockGugeMale = command.Amount != default(float); return; }
                nativeRuntime.Engine.DispatchNativeCommand(command); operated = true; return;
            }
            if (CanDrive) Integrate();
            switch (command.Name)
            {
                // Explicit development sample only; production has no clock/frame subscription.
                case "sample": Advance(command.Amount); break;
                case "animation": sample.NormalizedTime = command.Amount; Advance(0); break;
                case "wheel": sample.Wheel = command.Amount; Advance(0); sample.Wheel = 0; break;
                case "speed": engine.SetManualSpeed(command.Amount); Advance(0); break;
                case "loop-change": sample.AutoLoopChange = true; try { Advance(default(float)); } finally { sample.AutoLoopChange = false; } break;
                case "finish":
                    var finish = (Finish)Enum.Parse(typeof(Finish), command.Value, true);
                    if (!engine.CanFinish(finish)) throw new InvalidOperationException("原本の終了選択条件を満たしていません。");
                    sample.Finish = finish; try { Advance(0); } finally { sample.Finish = Finish.None; } break;
                case "hit": sample.Hit = command.Amount != 0; break;
                case "input-blocked": sample.UiBlocked = command.Amount != 0; break;
                case "recover": Apply(engine.Recover()); break;
                case "initiative":
                    if (nativeDriver != null && command.Amount != default(float)) throw new NotSupportedException("KKS rely/held frame-count controls are not connected to this event-driven native session.");
                    int initiative = checked((int)command.Amount);
                    if (initiative < 0 || initiative > 2) throw new ArgumentOutOfRangeException(nameof(command.Amount));
                    if (initiative != 0)
                    {
                        if (!(motion is Motion.DedicatedMotionModule available) || !available.AvailableMotionIds.Any(id => NativeMotionEligibility.Check(NativeProcCatalog.Get(id), SelectionContext(), true) && presentation.ItemsReady(NativeProcCatalog.Get(id))))
                            throw new InvalidOperationException("原本の自動主導条件（技能13・状態・場所・役割・所持品）を満たす準備済み体位がありません。");
                        engine.Definition.Initiative = initiative; SelectAutomaticMotion();
                    }
                    else engine.Definition.Initiative = 0;
                    break;
                case "lock-female": sample.StopFemale = command.Amount != 0; break;
                case "lock-male": sample.StopMale = command.Amount != 0; break;
                case "auto-start": sample.AutoStart = true; Advance(0); sample.AutoStart = false; break;
                case "auto-restart": sample.AutoRestart = true; Advance(0); sample.AutoRestart = false; break;
                case "auto-pull": sample.AutoPull = true; Advance(0); sample.AutoPull = false; break;
                case "motion": SelectMotion(command.Value); break;
                case "auto-motion": SelectMotion(command.Value, true); break;
            }
            RefreshMotionChoices(); Reschedule();
        }
        private void SelectMotion(string id, bool automatic = false)
        {
            id = NativeProcCatalog.CanonicalId(id);
            ValidateMotionSelection(id, automatic);
            if (context.IsMotionPreparation == null || !context.IsMotionPreparation())
            {
                if (context.RequestMotionChange == null) throw new InvalidOperationException("Motion loading transition is unavailable.");
                context.RequestMotionChange(id, automatic); return;
            }
            var definition = NativeProcCatalog.Get(id);
            definition.Initiative = engine.Definition.Initiative;
            motion.Select(id); presentation.SelectItems(definition); var next = engine.ChangeMotion(definition); engine.Stop(); engine = next;
            sample.NormalizedTime = 0; sample.PendingMotion = false; sample.AutoChangeMotion = false;
            sample.Finish = Finish.None; sample.PlayStart = -1;
            auto.Reset(); lastAutoState = engine.State; SelectedAwards();
            motion.Execute(new Command { Name = "state", Value = engine.State });
            // Voice/breath and item state resume once after the loading transition returns to Ready.
        }
        public void ValidateMotionSelection(string id, bool automatic)
        {
            if (NativeOwner) { nativeRuntime.Engine.ValidateMotionSelection(id, automatic); return; }
            if (nativeDriver != null)
            {
                if (automatic || id != KksOnePosePlayback.MotionId) throw new InvalidOperationException("The requested native KKS pose driver is not prepared.");
                if (id == motion.SelectedMotionId) throw new InvalidOperationException("The native KKS pose is already selected.");
                return;
            }
            Integration.NativeMerchantAccess.Validate(context.Request, id);
            var definition = NativeProcCatalog.Get(id);
            if (!(motion is Motion.DedicatedMotionModule available) || !available.AvailableMotionIds.Any(known => { var d = NativeProcCatalog.Get(known); return d.ActionMode == definition.ActionMode && d.MotionId == definition.MotionId; })
                || !NativeMotionEligibility.Check(definition, SelectionContext(), automatic) || !presentation.ItemsReady(definition))
                throw new InvalidOperationException("原本の体位選択条件・参加者・準備済み資産を満たしていません: " + id);
        }
        public void SuspendSelection()
        {
            selectionSuspended = true;
            if (deadline != null && context?.Request.Owner != null) context.Request.Owner.StopCoroutine(deadline);
            deadline = null; lastTime = UnityEngine.Time.time;
            presentation.SuspendSelectionAudio();
        }
        public void ResumeSelection()
        {
            selectionSuspended = false; lastTime = UnityEngine.Time.time;
            if (!CanDrive) return;
            if (NativeOwner) { RefreshMotionChoices(); return; }
            presentation.EnterState(engine.State, engine.Definition.ActionMode, engine.Definition.ModeControl, engine.Definition.Initiative != default(int));
            RefreshMotionChoices(); Reschedule();
        }
        private void SelectAutomaticMotion()
        {
            if (!(motion is Motion.DedicatedMotionModule native)) throw new InvalidOperationException("Native automatic motion requires the verified motion catalog.");
            bool male = context.Request.Participants.Any(p => p.Role == "male");
            var candidates = new List<string>();
            var weights = new List<float>();
            foreach (string id in native.AvailableMotionIds)
            {
                var d = NativeProcCatalog.Get(id);
                if (!NativeMotionEligibility.Check(d, SelectionContext(), true) || !presentation.ItemsReady(d)) continue;
                if (!d.Positions.Contains(context.Request.NativePlace)) continue;
                if (male ? d.Kind == ProcKind.Les : d.Kind != ProcKind.Les) continue;
                int initiative = engine.Definition.Initiative;
                if (initiative == 1 ? d.FemaleInitiative != 1 && d.FemaleInitiative != 2 : initiative != 2 || d.FemaleInitiative != 2) continue;
                candidates.Add(id);
                // Native hAuto field13 preference addition10, plus base weight10 (HAutoCtrl.GetAnimation).
                weights.Add(10 + (context.Request.VoiceContexts[0].Skills.Contains(d.Proclivity) ? 10 : 0));
            }
            if (candidates.Count == 0) throw new InvalidOperationException("No verified native automatic motion satisfies place, roles and initiative.");
            float choice = UnityEngine.Random.value * weights.Sum();
            for (int i = 0; i < candidates.Count; i++)
            { choice -= weights[i]; if (choice <= 0 || i == candidates.Count - 1) { SelectMotion(candidates[i], true); return; } }
        }
        private void Award(int mode, int pattern)
        {
            var actor = context.Request.Participants.First(p => p.Role == "female");
            results.AddNativeAward(actor.Identity, mode, pattern, () => UnityEngine.Random.value);
        }
        private void SelectedAwards()
        {
            var d = engine.Definition; selectedFamilies.Add(d.Kind);
            selectedSystems |= d.Systems;
            if ((d.ActionMode == 0 || d.ActionMode == 2) && (selectedSystems & 8) != 0) hadAnal = true;
            if ((d.Systems & 1) != 0) Award(1, 6);
            int special = d.ActionMode == 3 && d.MotionId == 13 ? 17 : d.ActionMode == 2 && d.MotionId == 38 ? 19 : d.ActionMode == 0 && d.MotionId == 16 ? 23 : d.ActionMode == 2 && d.MotionId == 44 ? 25 : d.ActionMode == 3 && d.MotionId == 14 ? 35 : d.ActionMode == 2 && d.MotionId == 45 ? 37 : d.ActionMode == 1 && d.MotionId == 30 ? 39 : d.ActionMode == 2 && d.MotionId == 46 ? 41 : d.ActionMode == 0 && d.MotionId == 100 ? 43 : d.ActionMode == 2 && d.MotionId == 113 ? 45 : d.ActionMode == 1 && d.MotionId == 104 ? 47 : d.ActionMode == 2 && d.MotionId == 110 ? 49 : -1;
            if (special >= 0) Award(1, special);
            if (d.ActionMode == 3 && d.ModeControl == 3) Award(1, 21);
        }
        private void Integrate()
        {
            if (!CanDrive) return;
            float now = UnityEngine.Time.time;
            float delta = Math.Max(0, now - lastTime); lastTime = now;
            if (delta > 0) Advance(delta);
        }
        private void Reschedule()
        {
            if (NativeOwner) return;
            if (deadline != null && context?.Request.Owner != null) context.Request.Owner.StopCoroutine(deadline);
            deadline = null;
            if (!CanDrive) return;
            float seconds = engine.SecondsToBoundary(sample);
            if (nativeDriver != null)
            {
                if (!float.IsInfinity(seconds) && seconds > default(float)) deadline = context.Request.Owner.StartCoroutine(WaitBoundary(seconds));
                return;
            }
            int loop = CurrentLoop();
            if (loop >= 0 && (sample.Hit || engine.State.Contains("OLoop") || engine.Definition.Initiative != 0)) seconds = Math.Min(seconds, hit.Remaining(loop));
            hit.Range(loop, out float low, out float high);
            seconds = Math.Min(seconds, auto.SecondsToBoundary(engine, low, high));
            if (!float.IsInfinity(seconds)) deadline = context.Request.Owner.StartCoroutine(WaitBoundary(Math.Max(.001f, seconds)));
        }
        private IEnumerator WaitBoundary(float seconds)
        {
            yield return new UnityEngine.WaitForSeconds(seconds);
            deadline = null;
            if (!CanDrive) yield break;
            Integrate(); Reschedule();
        }
        private void Advance(float delta)
        {
            if (advancing) { queuedDelta += delta; advanceQueued = true; return; }
            advancing = true;
            try
            {
                do
                {
                    advanceQueued = false;
                    AdvanceCore(delta);
                    delta = queuedDelta; queuedDelta = 0;
                } while (advanceQueued && CanDrive);
            }
            finally { advancing = false; }
        }
        private void AdvanceCore(float delta)
        {
            sample.Delta = delta; sample.UnscaledDelta = delta;
            if (nativeDriver != null)
            {
                sample.Voice0 = presentation.IsDialoguePlaying(default(int));
                Apply(engine.Advance(sample).ToArray());
                motion.SetSpeed(engine.Speed);
                return;
            }
            sample.FemaleSkill = context.Request.Settings.FemaleSkillMultiplier;
            sample.MaleSkill = context.Request.Settings.MaleSkillMultiplier;
            NativeSkillRates.Apply(engine.Definition, context.Request.VoiceContexts[0].Skills, context.Request.VoiceContexts[0].Merchant,
                ResultModule.SkillMultiplier, ref sample.FemaleSkill, ref sample.MaleSkill);
            sample.Voice0 = presentation.IsDialoguePlaying(0); sample.Voice1 = presentation.IsDialoguePlaying(1);
            sample.Dialog = presentation.Dialog;
            int loop = CurrentLoop();
            hit.Range(loop, out float low, out float high);
            if (engine.Definition.Family == ProcKind.Houshi) low = high = -1;
            auto.Advance(engine, sample, low, high);
            if (loop >= 0 && (sample.Hit || engine.State.Contains("OLoop") || engine.Definition.Initiative != 0)) hit.Advance(loop, delta);
            bool autoLoopMotion = sample.AutoChangeMotion && (engine.State == "WLoop" || engine.State == "SLoop");
            Apply(engine.Advance(sample).ToArray());
            if (autoLoopMotion) SelectAutomaticMotion();
            if (delta > 0 && engine.IsAutomatic && engine.Definition.Initiative == 2 && engine.Definition.Family == ProcKind.Sonyu && engine.Definition.Inside && engine.Male >= 1)
                Advance(0);
            if (lastAutoState != engine.State) { auto.EnterState(lastAutoState, engine.State, engine.Definition.Family); lastAutoState = engine.State; RefreshMotionChoices(); }
            loop = CurrentLoop();
            float power = engine.Speed - (loop == 1 && !engine.IsAutomatic ? 1 : 0);
            sample.Hit = loop >= 0 && hit.Contains(engine.Definition.FeelHitCategory, loop, power);
            // speed is a native blend parameter; W/O use +1 and S uses the 1..2 range.
            float playbackSpeed = engine.State.Contains("SLoop") ? Math.Max(.001f, engine.Speed) : engine.Speed + 1;
            motion.SetSpeed(playbackSpeed); presentation.SetItemSpeed(playbackSpeed);
        }
        private int CurrentLoop() => engine.State.Contains("OLoop") ? 2 : engine.State.Contains("SLoop") ? 1 : engine.State.Contains("WLoop") || engine.State.Contains("MLoop") ? 0 : -1;
        private void Apply(System.Collections.Generic.IReadOnlyList<ProcEffect> effects)
        {
            foreach (var voice in context.Request.VoiceContexts)
            { voice.Mode = engine.Definition.VoiceMode; voice.MotionId = engine.Definition.MotionId; voice.Faint = engine.Faint; voice.Initiative = engine.Definition.Initiative; voice.GaugeHit = sample.Hit; }
            foreach (var effect in effects)
            {
                if (effect.Name == "kks-voice" || effect.Name == "kks-wait-voice")
                {
                    bool wait = effect.Name == "kks-wait-voice";
                    presentation.PlayKksRequest(effect.Value, engine.State, wait ? (Action)(() => { if (CanDrive) { nativeDriver.VoiceFinished(); OnVoiceComplete(default(int)); } }) : null);
                    Effect?.Invoke(effect); continue;
                }
                if (effect.Name == "state") { sample.NormalizedTime = 0; motion.Execute(new Command { Name = "state", Value = effect.Value }); presentation.EnterState(effect.Value, engine.Definition.ActionMode, engine.Definition.ModeControl, engine.Definition.Initiative != 0); if (effect.Value.Contains("Loop") || effect.Value.Contains("Insert") || effect.Value.Contains("Action")) { operated = true; } }
                else if (effect.Name == "start-voice")
                {
                    sample.PlayStart = 5;
                    foreach (var voice in context.Request.VoiceContexts)
                    { voice.Mode = engine.Definition.VoiceMode; voice.MotionId = engine.Definition.MotionId; voice.Faint = engine.Faint; voice.Initiative = engine.Definition.Initiative; }
                    presentation.PlayStart((int)effect.Amount, () => { if (context != null && context.IsCurrent()) { sample.PlayStart = -1; OnVoiceComplete(0); } });
                }
                else if (effect.Name == "stop-loop-voice")
                {
                    var d = engine.Definition;
                    if (d.Kind == ProcKind.MultiPlay && d.Family == ProcKind.Aibu) presentation.StopAtLoopChange(2, false, true);
                    else if (d.Kind == ProcKind.MultiPlay && d.Family == ProcKind.Sonyu) presentation.StopAtLoopChange(2, true, false);
                    else if (d.Family == ProcKind.Les) presentation.StopAtLoopChange(2, true, true);
                    else if (d.Family == ProcKind.Aibu) presentation.StopAtLoopChange(1, true, true);
                    else if (d.Family == ProcKind.Sonyu && d.ActionMode == 3 && d.ModeControl == 0) presentation.StopAtLoopChange(1, true, false);
                    // Houshi and regular Sonyu do not stop dialogue at loop change in the native Proc.
                }
                else if (effect.Name == "particle") presentation.PlayParticle((int)effect.Amount);
                else if (effect.Name == "lower-clothes") presentation.SetLowerClothes(0, (int)effect.Amount);
                else if (effect.Name == "auto-motion") SelectAutomaticMotion();
                else if (effect.Name == "hit-reset") hit.ResetTimers();
                else if (effect.Name == "short") presentation.PlayShort(engine.Definition.ActionMode, engine.Definition.ModeControl, (int)effect.Amount);
                else if (effect.Name == "voice-hit") presentation.PlayOrdinary(engine.State, true, engine.Definition.ActionMode, engine.Definition.Initiative != 0);
                else if (effect.Name == "old-finish") foreach (var voice in context.Request.VoiceContexts) voice.OldFinish = (int)effect.Amount;
                else if (effect.Name == "after-finish") foreach (var voice in context.Request.VoiceContexts) voice.FinishCount++;
                else if (effect.Name == "count" && effect.Value == "orgasm")
                {
                    totalOrgasm = Math.Min(999999, totalOrgasm + 1);
                   
                    if (engine.Definition.Family == ProcKind.Les && context.Request.Participants.Any(p => p.IsPlayer && p.Character.sex == 1)
                        && (engine.IsAutomatic || !context.Request.Settings.Futanari)) totalFemalePlayerOrgasm = Math.Min(10, totalFemalePlayerOrgasm + 1);
                    if (totalOrgasm == engine.Definition.FaintnessCount)
                        foreach (var actor in context.Request.Participants)
                            if (actor.Role == "female") results.AddNativeAward(actor.Identity, 0, context.Request.VoiceContexts[0].Force ? 4 : 6, () => UnityEngine.Random.value);
                }
                if (effect.Name == "count")
                {
                    counts.Add(effect.Value); if (effect.Value == "inside" || effect.Value == "outside" || effect.Value == "drink" || effect.Value == "vomit") totalMaleOrgasm++;
                    var family = engine.Definition.Family;
                    bool nativeAction = effect.Value == "orgasm" && family != ProcKind.Masturbation && family != ProcKind.Peeping
                        || (effect.Value == "inside" || effect.Value == "outside") && family == ProcKind.Sonyu
                        || family == ProcKind.Houshi && (effect.Value == "outside" || effect.Value == "drink" || effect.Value == "vomit");
                    if (nativeAction) { if ((engine.Definition.Systems & 16) != 0) pain = true; if ((engine.Definition.Systems & 32) != 0) constraint = true; }
                }
                if (effect.Name == "faint") totalFaintness++;
                else if (effect.Name == "action-result")
                    foreach (var actor in context.Request.Participants)
                        if (actor.Role == "female") results.AddNativeAward(actor.Identity, 1, (int)effect.Amount, () => UnityEngine.Random.value);
                Effect?.Invoke(effect);
            }
        }
        public NativeEndingState CaptureEndingState()
        {
            if (context == null || engine == null) throw new InvalidOperationException("Native H ending state is unavailable.");
            var settings = context.Request.Settings;
            return new NativeEndingState { IsFaintness = engine.Faint, IsForce = context.Request.VoiceContexts[0].Force,
                FemaleOrgasmTotal = totalOrgasm, MaleOrgasmTotal = totalMaleOrgasm,
                FemalePlayerOrgasmTotal = totalFemalePlayerOrgasm,
                HadControlAction = operated, Pain = pain, Constraint = constraint,
                PainSelected = (selectedSystems & 16) != 0, ConstraintSelected = (selectedSystems & 32) != 0 || settings.Constraint,
                PainAction = pain, ConstraintAction = constraint, SleepStart = settings.SleepStart,
                HadAibu = selectedFamilies.Contains(ProcKind.Aibu), HadHoushi = selectedFamilies.Contains(ProcKind.Houshi),
                HadSonyu = selectedFamilies.Contains(ProcKind.Sonyu), HadAnal = hadAnal,
                YobaiBareRate = settings.YobaiBareRate, InvitePattern = settings.InvitePattern, Skill2 = context.Request.VoiceContexts[0].Skill2,
                Evidence = "NativeProgression count/state observations; dedicated persisted Settings. Les player climax count follows explicit IsPlayer/sex and manual futanari exclusion; Anal follows cumulative JudgeSelect bit8 on Aibu/Sonyu selection." };
        }
        private void FinalAwards()
        {
            var voice = context.Request.VoiceContexts[0];
            if (voice.Merchant) return;
            string participant = context.Request.Participants.First(p => p.Role == "female").Identity;
            void Skill(int id) { if (voice.Skills.Contains(id)) results.AddNativeSkill(participant, id); }
            if (totalOrgasm < engine.Definition.FaintnessCount) { Award(0, voice.Force ? 5 : 3); if (voice.Force) Skill(9); }
            if (counts.Contains("outside")) { Award(1, 11); Skill(8); }
            if (counts.Contains("inside")) { Award(1, 12); Skill(5); }
            if (counts.Contains("same")) { Award(1, 13); Skill(2); }
            if (selectedFamilies.Contains(ProcKind.Aibu)) Award(1, 0);
            if (selectedFamilies.Contains(ProcKind.Houshi)) { Award(1, 1); Skill(4); }
            if (selectedFamilies.Contains(ProcKind.Sonyu)) Award(1, 2);
            if (selectedFamilies.Contains(ProcKind.Les)) { Award(1, 7); Skill(12); }
            if (pain) Award(1, 3);
            if (voice.Force) Award(1, 5);
            if (totalFaintness > 0) { Award(1, 14); Skill(20); }
            if (totalOrgasm == 0) Skill(21);
            if (!operated) Award(1, 16);
        }
        public void End(EndReason reason)
        {
            try { if (running && reason == EndReason.Completed) {
                if (NativeOwner) {
                    totalOrgasm = nativeRuntime.Flags.GetOrgCount(); totalMaleOrgasm = nativeRuntime.Flags.GetInsideAndOutsideCount();
                    selectedFamilies.Clear();
                    foreach (var mode in nativeRuntime.SelectedModes) {
                        if (mode == HFlag.EMode.aibu) selectedFamilies.Add(ProcKind.Aibu);
                        if (mode == HFlag.EMode.houshi) selectedFamilies.Add(ProcKind.Houshi);
                        if (mode == HFlag.EMode.sonyu) selectedFamilies.Add(ProcKind.Sonyu);
                    }
                    var nativeCount = nativeRuntime.Flags.count;
                    hadAnal = nativeCount.isInsertAnal;
                    if (nativeCount.houshiOutside + nativeCount.sonyuOutside + nativeCount.sonyuAnalOutside > default(int)) counts.Add("outside");
                    if (nativeCount.houshiInside + nativeCount.sonyuInside + nativeCount.sonyuCondomInside + nativeCount.sonyuAnalInside + nativeCount.sonyuAnalCondomInside > default(int)) counts.Add("inside");
                    if (nativeCount.sonyuSame + nativeCount.sonyuCondomSame + nativeCount.sonyuAnalSame + nativeCount.sonyuAnalCondomSame > default(int)) counts.Add("same");
                }
                nativeRuntime?.CaptureEnding(); results.CaptureFinalAwards(FinalAwards);
            } }
            finally
            {
                running = false; Reschedule(); engine?.Stop(); presentation.VoiceCompleted -= OnVoiceComplete;
                if (motion is Motion.DedicatedMotionModule nativeMotion) { nativeMotion.AnimationDriven -= OnAnimation; nativeMotion.SeCue -= OnSoundEffect; }
                if (panel != null) { try { panel.Release(); } finally { UnityEngine.Object.Destroy(panel.gameObject); panel = null; } }
            }
        }
        public void Dispose() { End(EndReason.Cancelled); context = null; }
    }
}
