using System;
using System.Collections.Generic;

namespace MainGameAiShoujoH.Progression
{
    public enum ProcKind { Aibu, Houshi, Sonyu, Spanking, Masturbation, Peeping, Les, MultiPlay }
    public enum Finish { None, Before, Inside, Outside, Same, Drink, Vomit }
    public sealed class ProcDefinition
    {
        public ProcKind Kind;
        public string Name;
        public int MotionId, ActionMode, ModeControl, Initiative, VoiceMode, FeelHitCategory;
        public int Systems;
        public bool CanDown, Yobai;
        public float GaugeRate = .03f, WheelStep = .05f, SpankingDecrease = .005f, SpankingFeel = .05f;
        public int FaintnessCount = 3;
        public int FemaleInitiative, Proclivity = -1, Promiscuity, RefusalAction, Hentai, FaintnessLimit;
        public bool Sleep, MerchantMotion, NeedItem, HasMaleFile;
        public int[] Positions = Array.Empty<int>();
        public bool ManualMasturbation => MotionId == 3 || MotionId == 13 || MotionId == 14;
        public ProcKind Family => Kind == ProcKind.MultiPlay ? (ModeControl == 0 ? ProcKind.Aibu : ModeControl < 3 ? ProcKind.Houshi : ProcKind.Sonyu) : Kind;
        public bool Inside => Kind == ProcKind.MultiPlay ? ModeControl == 3 : (ActionMode == 2 && ModeControl == 0) || (ActionMode == 3 && ModeControl == 1);
        public bool RestrictedMale => Kind == ProcKind.MultiPlay ? ModeControl == 4 : ActionMode == 3 && ModeControl == 0;
    }
    public sealed class DriveSample
    {
        public float Delta, UnscaledDelta, Wheel, NormalizedTime;
        public bool Hit, UiBlocked, Voice0, Voice1, Dialog, PendingMotion, StopFemale, StopMale, Drug5;
        public bool AutoStart, AutoRestart, AutoPull, AutoChangeMotion;
        public float FemaleSkill = 1, MaleSkill = 1;
        public float AutoSpeed = float.NaN;
        public bool AutoLoopChange;
        public int PlayStart = -1;
        public Finish Finish;
    }
    /// <summary>Commands are effects, not direct writes to ordinary HSceneProc globals.</summary>
    public sealed class ProcEffect
    {
        public string Name, Value;
        public float Amount;
        public ProcEffect(string name, string value = null, float amount = 0) { Name = name; Value = value; Amount = amount; }
    }
    public interface IProgressionDriver
    {
        string State { get; }
        float Female { get; }
        float Male { get; }
        float Speed { get; }
        bool WaitingForStartVoice { get; }
        bool HasInsideResult { get; }
        bool CanFinish(Finish finish);
        float SecondsToBoundary(DriveSample sample);
        IReadOnlyList<ProcEffect> Advance(DriveSample sample);
        void SetManualSpeed(float value);
        void Stop();
    }
    public sealed class NativeProgression
    {
        private readonly ProcDefinition definition;
        private readonly List<ProcEffect> effects = new List<ProcEffect>();
        private int nextPlay;
        private bool oldHit, finishVomit, finishSame, insert, change, ended, spankingUpFeel;
        private float elapsed, spankingTime, spankingBackup;
        private IProgressionDriver driver;
        private string state;
        private float female, male, speed;
        public string State { get => driver?.State ?? state; private set => state = value; }
        public float Female { get => driver?.Female ?? female; private set => female = value; }
        public float Male { get => driver?.Male ?? male; private set => male = value; }
        public float Speed { get => driver?.Speed ?? speed; private set => speed = value; }
        public bool Faint { get; private set; }
        public int OrgasmCount { get; private set; }
        public int FaintnessCount { get; private set; }
        public ProcDefinition Definition => definition;
        public bool WaitingForStartVoice => driver?.WaitingForStartVoice ?? nextPlay != 0;
        public bool IsAutomatic => Automatic;
        public bool HasInsideResult => driver?.HasInsideResult ?? insert;
        public void AttachDriver(IProgressionDriver value) { if (driver != null || value == null) throw new InvalidOperationException("Progression driver ownership differs."); driver = value; }
        public bool CanFinish(Finish finish)
        {
            if (driver != null) return driver.CanFinish(finish);
            bool weakStrong = Plain == "WLoop" || Plain == "SLoop";
            bool loop = weakStrong || Plain == "OLoop";
            if (finish == Finish.Before)
                return definition.Kind == ProcKind.Masturbation ? definition.ManualMasturbation && (weakStrong || Plain == "MLoop") : definition.Family == ProcKind.Sonyu ? loop && (Female < .7f || Male < .7f) : weakStrong;
            if (definition.Kind != ProcKind.MultiPlay && definition.Initiative == 2) return false;
            if (definition.Family == ProcKind.Houshi)
                return Plain == "OLoop" && (finish == Finish.Outside || !Faint && (finish == Finish.Drink || finish == Finish.Vomit) && (definition.Kind == ProcKind.MultiPlay ? definition.ModeControl == 2 : definition.ModeControl == 1));
            if (definition.Family != ProcKind.Sonyu || !loop || Male < .7f) return false;
            if (finish == Finish.Inside) return definition.Inside;
            if (finish == Finish.Outside) return !(Faint && definition.RestrictedMale);
            return finish == Finish.Same && Plain == "OLoop" && definition.Inside && Female >= .7f && !(Faint && definition.RestrictedMale);
        }
        public float SecondsToBoundary(DriveSample s)
        {
            if (driver != null) return driver.SecondsToBoundary(s);
            if (ended) return float.PositiveInfinity;
            if (definition.Kind == ProcKind.Spanking && spankingUpFeel) return Math.Max(0, .3f - spankingTime);
            if (definition.Kind == ProcKind.Masturbation && Automatic && (Plain == "Idle" || Plain == "Orgasm_A")) return Math.Max(0, (Plain == "Idle" ? 5 : 15) - elapsed);
            if (Plain != "WLoop" && Plain != "MLoop" && Plain != "SLoop" && Plain != "OLoop") return float.PositiveInfinity;
            var family = definition.Family;
            float value = family == ProcKind.Houshi ? Male : Female;
            float target = Plain == "OLoop" ? 1 : .7f;
            float factor = family == ProcKind.Houshi ? (s.StopMale ? 0 : (s.Hit ? 2 : 1) * s.MaleSkill) : s.StopFemale ? 0 : (s.Hit ? 1 : Automatic ? definition.Kind == ProcKind.Masturbation ? 1 : .3f : 0) * s.FemaleSkill;
            if (definition.Kind == ProcKind.Masturbation) target = Plain == "WLoop" ? .25f : Plain == "MLoop" ? .5f : Plain == "SLoop" ? Automatic ? .75f : .7f : 1;
            // A hand-operated Houshi at full gauge has no automatic terminal transition.
            if (family == ProcKind.Houshi && Plain == "OLoop" && !(Automatic && definition.Initiative == 2)) return float.PositiveInfinity;
            if (Plain == "OLoop" && s.PendingMotion) return float.PositiveInfinity;
            float femaleBoundary = factor <= 0 || definition.GaugeRate <= 0 || target <= value ? float.PositiveInfinity : (target - value) / (definition.GaugeRate * factor);
            if (family == ProcKind.Sonyu && Automatic && definition.Initiative == 2 && definition.Inside && !s.StopMale && s.MaleSkill > 0 && Male < 1 && !(Faint && definition.RestrictedMale))
                return Math.Min(femaleBoundary, (1 - Male) / (definition.GaugeRate * s.MaleSkill));
            return femaleBoundary;
        }
        private bool Automatic => !Faint && (definition.Kind == ProcKind.Masturbation ? !definition.ManualMasturbation : definition.Initiative != 0);
        private string Plain => State.StartsWith("D_", StringComparison.Ordinal) ? State.Substring(2) : State;
        public NativeProgression(ProcDefinition definition, bool faint = false)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Faint = faint && definition.CanDown && definition.Kind != ProcKind.Masturbation;
            State = definition.Kind == ProcKind.Spanking ? (Faint ? "D_Orgasm_A" : "WIdle") : "Idle";
            if (Faint && definition.Kind != ProcKind.Spanking) State = "D_Idle";
            Speed = definition.Kind == ProcKind.Masturbation ? 1 : 0;
        }
        public IReadOnlyList<ProcEffect> Advance(DriveSample s)
        {
            if (driver != null) return driver.Advance(s);
            effects.Clear();
            if (ended) return effects;
            if (s == null || !Finite(s.Delta) || s.Delta < 0 || !Finite(s.Wheel)) throw new ArgumentException("Invalid native progression sample.");
            float wheel = s.UiBlocked ? 0 : Math.Sign(s.Wheel) * definition.WheelStep;
            var family = definition.Family;
            if (definition.Kind == ProcKind.Peeping) { Peeping(s); return effects; }
            if (definition.Kind == ProcKind.Spanking) { Spank(s, wheel); return effects; }
            if (definition.Kind == ProcKind.Masturbation) { Masturbate(s, wheel); return effects; }
            if (Plain == "Idle" || Plain.EndsWith("_A", StringComparison.Ordinal))
            {
                if (Plain == "Orgasm_IN_A") InsideWait(s, wheel);
                else Start(s, wheel);
                return effects;
            }
            if (Plain == "Insert") { if (s.NormalizedTime >= 1) Set("WLoop"); return effects; }
            if (Plain == "WLoop" || Plain == "SLoop" || Plain == "OLoop")
            {
                bool climax = Plain == "OLoop";
                if (s.Finish == Finish.Before)
                {
                    if (family == ProcKind.Sonyu) Male = Math.Max(Male, .7f);
                    if (family == ProcKind.Sonyu && climax) return effects;
                    if (family == ProcKind.Houshi) Male = .7f; else Female = .7f;
                    Set("OLoop"); Speed = 0; oldHit = false; Emit("hit-reset"); return effects;
                }
                // Native Sonyu evaluates male finish in W/S before gauge accumulation.
                if (!climax && family == ProcKind.Sonyu && TryMaleFinish(s, false)) return effects;
                if (Automatic && Finite(s.AutoSpeed))
                {
                    if (!climax && s.AutoLoopChange) { Set(Plain == "WLoop" ? "SLoop" : "WLoop"); Emit("hit-reset"); Emit("stop-loop-voice"); }
                    Speed = Clamp(s.AutoSpeed, 0, 1);
                }
                else
                {
                    Speed = Clamp(Speed + wheel, 0, climax ? 1 : 2);
                    if (!climax && Speed > 1 && Plain == "WLoop") { Set("SLoop"); Emit("hit-reset"); }
                    else if (!climax && Speed < 1 && Plain == "SLoop") { Set("WLoop"); Emit("hit-reset"); }
                }
                Accumulate(s, family);
                if (family == ProcKind.Sonyu && definition.ActionMode == 2)
                {
                    int award = definition.MotionId == 38 ? 20 : definition.MotionId == 44 ? 26 : definition.MotionId == 45 ? 38 : definition.MotionId == 46 ? 42 : definition.MotionId == 113 ? 46 : definition.MotionId == 110 ? 50 : -1;
                    if (award >= 0) Emit("action-result", null, award);
                }
                if (s.Hit && !oldHit && !s.AutoChangeMotion) { Emit("voice-hit"); if (family != ProcKind.Houshi) Emit("short", null, family == ProcKind.Les ? 1 : 0); }
                oldHit = s.Hit;
                if (!climax)
                {
                    if ((family == ProcKind.Houshi ? Male : Female) >= .7f) { Set("OLoop"); Speed = 0; oldHit = false; Emit("hit-reset"); }
                }
                else if (family == ProcKind.Houshi) HoushiFinish(s);
                else if (!s.PendingMotion && Female >= 1) FemaleFinish(family == ProcKind.Sonyu ? "OrgasmF_IN" : "Orgasm");
                else if (family == ProcKind.Sonyu) TryMaleFinish(s, true);
                return effects;
            }
            Terminal(s);
            return effects;
        }
        private void Accumulate(DriveSample s, ProcKind family)
        {
            float gain = s.Delta * definition.GaugeRate;
            if (family == ProcKind.Houshi) { if (!s.StopMale) Male = Clamp(Male + gain * (s.Hit ? 2 : 1) * s.MaleSkill, 0, 1); }
            else
            {
                float factor = s.Hit ? 1 : Automatic ? .3f : 0;
                // Les AutoLoop omits Skill0/3; AutoOLoop applies them.
                float skill = family == ProcKind.Les && Automatic && Plain != "OLoop" ? 1 : s.FemaleSkill;
                if (!s.StopFemale) Female = Clamp(Female + gain * factor * skill, 0, 1);
                if (family == ProcKind.Sonyu && !s.StopMale && !(Faint && definition.RestrictedMale)) Male = Clamp(Male + gain * s.MaleSkill, 0, 1);
            }
        }
        private void Start(DriveSample s, float wheel)
        {
            bool restart = Plain != "Idle";
            bool voices = s.Voice0 || s.Voice1;
            bool noStartVoice = (definition.Family == ProcKind.Aibu || definition.Family == ProcKind.Les) && (Automatic && !restart || Faint && !restart);
            if (nextPlay == 0)
            {
                if (voices || (!Automatic && s.PlayStart > 4)) return;
                if (Automatic ? !(restart ? s.AutoRestart : s.AutoStart) : wheel == 0) return;
                nextPlay = noStartVoice ? 3 : 2;
                if (!noStartVoice && !(definition.Yobai && definition.Family == ProcKind.Sonyu && (restart || Faint))) Emit("start-voice", null, restart ? 3 : 2);
                if (!noStartVoice) return;
            }
            if (voices || (!Automatic && s.PlayStart > 4)) return;
            if (restart && Automatic && s.AutoChangeMotion) { Emit("auto-motion"); nextPlay = 0; return; }
            bool useInsert = definition.Family == ProcKind.Sonyu && (definition.Inside || definition.Kind == ProcKind.MultiPlay && !Automatic);
            Set(useInsert ? "Insert" : "WLoop"); Speed = 0; oldHit = false; nextPlay = 0;
            Emit("hit-reset"); if (restart) Emit("after-finish");
            if (definition.Family == ProcKind.Houshi) Emit("short", null, 1);
        }
        private bool TryMaleFinish(DriveSample s, bool climax)
        {
            bool inside = (s.Finish == Finish.Inside && Male >= .7f || Automatic && definition.Initiative == 2 && Male >= 1) && definition.Inside;
            bool outside = s.Finish == Finish.Outside && Male >= .7f && !(Faint && definition.RestrictedMale);
            bool same = climax && s.Finish == Finish.Same && Male >= .7f && !(Faint && definition.RestrictedMale);
            if (!inside && !outside && !same) return false;
            Male = 0; Female = same ? 0 : Math.Min(Female, .5f);
            insert = inside || same; finishSame = same;
            Set(same ? "OrgasmS_IN" : inside ? "OrgasmM_IN" : "OrgasmM_OUT");
            if (same) { OrgasmCount = Math.Min(10, OrgasmCount + 1); Emit("count", "orgasm"); Emit("count", "same"); }
            Emit("count", inside || same && definition.Inside ? "inside" : "outside");
            Emit("old-finish", null, same ? 3 : inside ? 1 : 2); Emit("finish-ui", "disable");
            Speed = 0; return true;
        }
        private void HoushiFinish(DriveSample s)
        {
            bool outside = s.Finish == Finish.Outside || Automatic && definition.Initiative == 2 && Male >= 1;
            bool inner = s.Finish == Finish.Drink || s.Finish == Finish.Vomit;
            bool allowInner = definition.Kind == ProcKind.MultiPlay ? definition.ModeControl == 2 : definition.ModeControl != 0;
            if (!outside && !(inner && allowInner)) return;
            if (definition.ActionMode == 1)
            {
                int award = definition.MotionId == 25 ? 20 : definition.MotionId == 30 ? 40 : definition.MotionId == 104 ? 48 : -1;
                if (award >= 0) Emit("action-result", null, award);
            }
            Male = 0; Speed = 0; finishVomit = s.Finish == Finish.Vomit;
            if (outside) { Set("Orgasm_OUT"); Emit("count", "outside"); }
            else Set("Orgasm_IN", false);
            Emit("old-finish", null, outside ? 2 : 1); Emit("finish-ui", "disable");
        }
        private void FemaleFinish(string state)
        {
            Set(state); Female = 0; Speed = 0; OrgasmCount = Math.Min(10, OrgasmCount + 1);
            Emit("count", "orgasm"); Emit("old-finish", null, 0); Emit("finish-ui", "disable");
            if (Faint) Emit("action-result", null, 15);
            if (definition.Family == ProcKind.Aibu)
            {
                if (definition.ActionMode == 3 && definition.ModeControl == 3) Emit("action-result", null, 22);
                if (definition.ActionMode == 0 && definition.MotionId == 16) Emit("action-result", null, 24);
                if (definition.ActionMode == 0 && definition.MotionId == 100) Emit("action-result", null, 44);
            }
        }
        private void Terminal(DriveSample s)
        {
            if (Plain == "Drop" && s.NormalizedTime < 4 || Plain == "Vomit" && definition.Family == ProcKind.Sonyu && s.NormalizedTime < 3 || s.NormalizedTime < 1) return;
            switch (Plain)
            {
                case "Orgasm": case "OrgasmF_IN":
                    AfterFemale(s, definition.Family == ProcKind.Sonyu ? (definition.Inside ? "Orgasm_IN_A" : "OrgasmM_OUT_A") : "Orgasm_A"); break;
                case "Orgasm_OUT": Set("Orgasm_OUT_A"); break;
                case "Orgasm_IN": Set(finishVomit ? "Vomit_IN" : "Drink_IN", false); Emit("count", finishVomit ? "vomit" : "drink"); break;
                case "Drink_IN": Set("Drink", false); break;
                case "Vomit_IN": Set("Vomit", false); break;
                case "Drink": Set("Drink_A", false); break;
                case "Vomit": Set(definition.Family == ProcKind.Sonyu ? (finishSame ? "Orgasm_IN_A" : "OrgasmM_OUT_A") : "Vomit_A"); break;
                case "OrgasmM_IN":
                    if (definition.ActionMode == 3 && definition.ModeControl == 1 && definition.Kind != ProcKind.MultiPlay) Set("Vomit"); else Set("Orgasm_IN_A"); break;
                case "OrgasmS_IN": AfterFemale(s, "Orgasm_IN_A"); break;
                case "OrgasmM_OUT": Set("OrgasmM_OUT_A"); break;
                case "Pull": Set(insert ? "Drop" : "OrgasmM_OUT_A"); break;
                case "Drop": insert = false; Set("OrgasmM_OUT_A"); break;
            }
        }
        private void AfterFemale(DriveSample s, string after)
        {
            if (!Faint && (OrgasmCount >= definition.FaintnessCount || s.Drug5))
            {
                if (FaintnessCount == 0) Emit("action-result", null, 14);
                Faint = true; FaintnessCount++; definition.Initiative = 0; Emit("faint", null, s.Drug5 ? 1 : 0);
            }
            Set(after); Emit("finish-ui", "enable");
        }
        private void InsideWait(DriveSample s, float wheel)
        {
            if (Automatic)
            {
                if (s.AutoPull) { Set("Pull"); Emit("after-finish"); }
                else if (s.AutoRestart) { Set("WLoop"); Emit("after-finish"); }
                return;
            }
            if (s.Voice0 || s.Voice1 || s.Dialog || s.PlayStart > 4) return;
            if (nextPlay == 0 && wheel < 0) { Set("Pull"); Emit("after-finish"); }
            else if (nextPlay == 0 && wheel > 0) { nextPlay = 1; Emit("start-voice", null, 3); }
            else if (nextPlay == 1) { nextPlay = 0; Set("WLoop"); Emit("after-finish"); }
        }
        private void Masturbate(DriveSample s, float wheel)
        {
            bool auto = Automatic;
            if (Plain == "Idle" || Plain == "Orgasm_A")
            {
                if (auto) { elapsed += s.Delta; if (elapsed >= (Plain == "Idle" ? 5 : 15)) { elapsed = 0; Set("WLoop"); Speed = 1; Emit("after-finish"); } }
                else Start(s, wheel);
                return;
            }
            if (Plain == "Orgasm") { if (s.NormalizedTime >= 1 && (auto || !s.Voice0)) { Set("Orgasm_A"); elapsed = 0; } return; }
            Speed = auto ? 1 : Clamp(Speed + wheel, 0, 1);
            if (s.Finish == Finish.Before && definition.ManualMasturbation && Plain != "OLoop") { Female = .7f; Set("OLoop"); Speed = 0; return; }
            if (!s.StopFemale && (auto || s.Hit)) Female = Clamp(Female + s.Delta * definition.GaugeRate * s.FemaleSkill, 0, 1);
            if (change) { change = false; return; }
            string next = Plain == "WLoop" && Female >= .25f ? "MLoop" : Plain == "MLoop" && Female >= .5f ? "SLoop" : Plain == "SLoop" && Female >= (auto ? .75f : .7f) ? "OLoop" : null;
            if (next != null) { Set(next); Speed = auto ? 1 : 0; change = true; }
            else if (Plain == "OLoop" && !s.PendingMotion && Female >= 1)
            {
                FemaleFinish("Orgasm"); if (definition.MotionId == 13) Emit("action-result", null, 18); if (definition.MotionId == 14) Emit("action-result", null, 36);
            }
        }
        private void Spank(DriveSample s, float wheel)
        {
            if (Plain == "Orgasm") { if (s.NormalizedTime >= 1) { AfterFemale(s, Faint ? "Orgasm_A" : "WIdle"); Emit("after-finish"); } return; }
            if (spankingUpFeel && s.Delta > 0) SpankGain(s, s.Delta);
            if (Plain.EndsWith("Action", StringComparison.Ordinal))
            {
                if (s.NormalizedTime >= 1)
                {
                    if (Plain != "WAction" && !s.PendingMotion && Female >= 1) { FemaleFinish("Orgasm"); Emit("action-result", null, 15); }
                    else Set(Faint ? "Orgasm_A" : Female >= .5f ? "SIdle" : "WIdle");
                }
                return;
            }
            if (!s.StopFemale) Female = Math.Max(0, Female - definition.SpankingDecrease * s.Delta);
            if (Plain == "SIdle" && Female < .5f) Set("WIdle");
            if (wheel != 0 && !s.Voice0 && !s.Voice1)
            {
                spankingBackup = Female; spankingTime = 0; spankingUpFeel = true; SpankGain(s, 0); Set(Faint ? "Action" : Female >= .5f ? "SAction" : "WAction"); Emit("voice-hit"); Emit("short");
            }
        }
        private void SpankGain(DriveSample sample, float delta)
        {
            // Original Spnking122..140 has a one-window upFeel flag. Its per-render-call accumulation remains a native frame-rate-dependent limitation.
            spankingTime = Math.Min(.3f, spankingTime + delta);
            if (!sample.StopFemale) Female = Clamp(Female + (spankingBackup + definition.SpankingFeel * spankingTime / .3f) * sample.FemaleSkill, 0, 1);
            if (spankingTime >= .3f) spankingUpFeel = false;
        }
        private void Peeping(DriveSample s)
        {
            if (definition.MotionId != 10) return;
            float phase = s.NormalizedTime % 1;
            if (phase > .93f) Emit("peeping-fade", null, 1.5f);
            else if (elapsed <= .05f && phase > .05f) Emit("lower-clothes", null, 2);
            else if (elapsed <= .21f && phase > .21f) Emit("particle", null, 2);
            else if (elapsed <= .82f && phase > .82f) Emit("lower-clothes", null, 0);
            elapsed = phase;
        }
        public IReadOnlyList<ProcEffect> Recover()
        {
            if (driver != null) throw new InvalidOperationException("KKS native session has no AI faintness recovery branch.");
            effects.Clear();
            if (!Faint || !(Plain == "Idle" || Plain == "WLoop" || Plain == "SLoop" || Plain == "OLoop" || Plain.EndsWith("_A", StringComparison.Ordinal))) return effects;
            Faint = false; OrgasmCount = 0;
            Set(definition.Family == ProcKind.Sonyu ? definition.Inside ? "Orgasm_IN_A" : "OrgasmM_OUT_A" : definition.Kind == ProcKind.Spanking ? "WIdle" : definition.Family == ProcKind.Houshi ? "Orgasm_OUT_A" : "Orgasm_A");
            Emit("recover"); return effects;
        }
        public void Stop() { driver?.Stop(); ended = true; spankingUpFeel = false; nextPlay = 0; effects.Clear(); }
        public NativeProgression ChangeMotion(ProcDefinition nextDefinition)
        {
            if (driver != null) throw new InvalidOperationException("KKS native pose switching requires a prepared native pose driver.");
            var next = new NativeProgression(nextDefinition, Faint) { Female = Female, Male = Male, OrgasmCount = OrgasmCount, FaintnessCount = FaintnessCount };
            bool idle = Plain == "Idle" || Plain.EndsWith("_A", StringComparison.Ordinal);
            if (!idle)
            {
                if (nextDefinition.Kind == ProcKind.Spanking) next.State = next.Faint ? "D_Orgasm_A" : Female >= .5f ? "SIdle" : "WIdle";
                else if (nextDefinition.Kind == ProcKind.Masturbation) next.State = Female >= .7f ? "OLoop" : Female >= .5f ? "SLoop" : Female >= .25f ? "MLoop" : "WLoop";
                else next.State = (next.Faint ? "D_" : "") + ((nextDefinition.Family == ProcKind.Houshi ? Male : Female) >= .7f ? "OLoop" : "WLoop");
            }
            return next;
        }
        public void SetManualSpeed(float value)
        {
            if (driver != null) { driver.SetManualSpeed(value); return; }
            if (!Finite(value)) throw new ArgumentException("Non-finite native H speed.");
            Speed = Clamp(value, 0, Plain == "OLoop" || definition.Kind == ProcKind.Masturbation ? 1 : 2);
        }
        private void Set(string state, bool down = true) { State = down && Faint ? "D_" + state : state; Emit("state", State); }
        private void Emit(string name, string value = null, float amount = 0) => effects.Add(new ProcEffect(name, value, amount));
        private static float Clamp(float n, float lo, float hi) => Math.Max(lo, Math.Min(hi, n));
        private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
    }
}
