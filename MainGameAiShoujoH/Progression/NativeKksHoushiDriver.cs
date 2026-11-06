using System;
using System.Collections.Generic;
using MainGameAiShoujoH.Integration;
using UnityEngine;

namespace MainGameAiShoujoH.Progression
{
    // Saved HHoushi.Proc/LoopProc/AfterProc, manual two-actor path. Driven only
    // by commands, exact voice completions, clip boundaries and known deadlines.
    // Continuous native speed interpolation between those events and rely/held
    // frame-count input are not claimed to be bit-identical to HSceneProc.Update.
    internal sealed class NativeKksHoushiDriver : IProgressionDriver
    {
        readonly NativeKksSessionRuntime runtime;
        readonly List<ProcEffect> effects = new List<ProcEffect>();
        HFlag F => runtime.Flags;
        NativeKksProtocol P => runtime.Protocol;
        bool stopped, waiting, voiceFinished, inside;
        string pending;
        public string State { get; private set; } = "Idle";
        public float Female => F.gaugeFemale / P.gaugeMax;
        public float Male => F.gaugeMale / P.gaugeMax;
        public float Speed => F.speed;
        public bool WaitingForStartVoice => waiting;
        public bool HasInsideResult => inside;
        internal NativeKksHoushiDriver(NativeKksSessionRuntime runtime) { this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime)); }
        internal void VoiceFinished() { if (waiting) voiceFinished = true; }
        public bool CanFinish(Finish finish) => finish == Finish.Before ? State == "WLoop" || State == "SLoop" || State == "OLoop" :
            State == "OLoop" ? finish == Finish.Inside || finish == Finish.Outside : State == "Oral_Idle" && (finish == Finish.Drink || finish == Finish.Vomit);
        public float SecondsToBoundary(DriveSample sample)
        {
            if (stopped) return float.PositiveInfinity;
            float result = float.PositiveInfinity;
            if ((State == "WLoop" || State == "SLoop") && !F.lockGugeMale && F.rateClickGauge * F.feelMaleCheat > default(float))
                result = Math.Max(default(float), (P.orgasmBoundary - F.gaugeMale) / (F.rateClickGauge * F.feelMaleCheat));
            if ((State == "WLoop" || State == "SLoop") && F.timeNoClick > default(float) && F.timeSpeedUpStartCalc < F.timeSpeedUpStart)
                result = Math.Min(result, Math.Min(F.timeNoClick, F.timeSpeedUpStart - F.timeSpeedUpStartCalc));
            if (State == "Oral_Idle") result = Math.Min(result, Math.Max(default(float), F.timeWaitHoushi.timeIdle - F.timeWaitHoushi.timeIdleCalc));
            return result;
        }
        public IReadOnlyList<ProcEffect> Advance(DriveSample sample)
        {
            effects.Clear();
            if (stopped) return effects;
            F.lockGugeFemale = sample.StopFemale; F.lockGugeMale = sample.StopMale;
            float delta = sample.Delta;
            if (delta < default(float) || float.IsNaN(delta) || float.IsInfinity(delta)) throw new ArgumentOutOfRangeException(nameof(sample.Delta));
            bool loop = State == "WLoop" || State == "SLoop" || State == "OLoop";
            if (loop) F.MaleGaugeUp(delta * F.rateClickGauge);
            else if (State == "Idle" || State.EndsWith("_A", StringComparison.Ordinal)) F.FemaleGaugeUp(-delta * F.rateDecreaseGauge, false, false);
            if (State == "IN_Start" || State == "IN_Loop" || State == "M_OUT_Loop")
            {
                // Original FinishGaugeDown adds delta twice, then clamps.
                F.timeDecreaseGaugeCalc = Mathf.Clamp(F.timeDecreaseGaugeCalc + delta + delta, default(float), F.timeDecreaseGauge);
                F.gaugeMale = Mathf.Lerp(P.gaugeMax, default(float), F.timeDecreaseGaugeCalc / F.timeDecreaseGauge);
            }
            if (State == "WLoop" || State == "SLoop") IntegrateSpeed(delta);
            if (waiting)
            {
                if (voiceFinished)
                {
                    waiting = false; voiceFinished = false; F.voiceWait = false;
                    var next = pending; pending = null;
                    if (next == "M_OUT_Start") BeginFinish(false);
                    else if (next == "IN_Start") BeginFinish(true);
                    else Set(next);
                }
                return effects;
            }
            if (State == "Idle" || State == "OUT_A" || State == "Drink_A" || State == "Vomit_A" || State == "Stop_Idle")
            {
                if (sample.Wheel > default(float) || sample.AutoStart || sample.AutoRestart) Wait("WLoop", "200");
            }
            else if (State == "WLoop" || State == "SLoop")
            {
                if (sample.Finish == Finish.Before) Set("Stop_Idle");
                else if (sample.AutoLoopChange) Set(State == "WLoop" ? "SLoop" : "WLoop");
                else if (F.gaugeMale >= P.orgasmBoundary) { Set("OLoop"); F.speedCalc = default(float); F.speedUpClac = Vector2.zero; F.timeNoClick = default(float); Voice("203"); }
                else if (sample.Wheel != default(float)) F.SpeedUpClick(Math.Sign(sample.Wheel) * F.rateWheelSpeedUp, P.clipEnd);
            }
            else if (State == "OLoop")
            {
                if (sample.Finish == Finish.Before) Set("Stop_Idle");
                else if (sample.Finish == Finish.Outside) Wait("M_OUT_Start", "204");
                else if (sample.Finish == Finish.Inside) Wait("IN_Start", "205");
            }
            else if (State == "M_OUT_Start" && sample.NormalizedTime >= P.clipEnd) { Set("M_OUT_Loop"); F.timeDecreaseGaugeCalc = default(float); }
            else if (State == "M_OUT_Loop" && !sample.Voice0 && sample.NormalizedTime >= P.outsideLoopEnd) { Set("OUT_A"); F.gaugeMale = default(float); Voice("207"); }
            else if (State == "IN_Start" && sample.NormalizedTime >= P.clipEnd) Set("IN_Loop");
            else if (State == "IN_Loop" && !sample.Voice0) Set("Oral_Idle_IN");
            else if (State == "Oral_Idle_IN" && sample.NormalizedTime >= P.clipEnd) { Set("Oral_Idle"); F.gaugeMale = default(float); F.timeWaitHoushi.SetIdleTime(); }
            else if (State == "Oral_Idle")
            {
                F.timeWaitHoushi.timeIdleCalc += delta;
                var finish = sample.Finish;
                if (finish == Finish.None && F.timeWaitHoushi.timeIdleCalc >= F.timeWaitHoushi.timeIdle) finish = UnityEngine.Random.value < Mathf.InverseLerp(default(float), P.outsideLoopEnd, P.clipEnd) ? Finish.Drink : Finish.Vomit;
                if (finish == Finish.Drink) { F.AddHoushiDrink(); Set("Drink_IN"); Count("drink"); }
                else if (finish == Finish.Vomit) { F.AddHoushiVomit(); Set("Vomit_IN"); Count("vomit"); }
            }
            else if (State == "Drink_IN" && sample.NormalizedTime >= P.clipEnd) Set("Drink");
            else if (State == "Vomit_IN" && sample.NormalizedTime >= P.clipEnd) Set("Vomit");
            else if (State == "Drink" && sample.NormalizedTime >= P.clipEnd) { Set("Drink_A"); Voice("208"); }
            else if (State == "Vomit" && sample.NormalizedTime >= P.clipEnd) { Set("Vomit_A"); Voice("209"); }
            return effects;
        }
        void IntegrateSpeed(float delta)
        {
            F.timeNoClick = Mathf.Max(default(float), F.timeNoClick - delta);
            if (F.timeNoClick > default(float))
            {
                F.timeSpeedUpStartCalc = Mathf.Clamp(F.timeSpeedUpStartCalc + delta, default(float), F.timeSpeedUpStart);
                F.speedCalc = Mathf.Lerp(F.speedUpClac.x, F.speedUpClac.y, F.timeSpeedUpStartCalc / F.timeSpeedUpStart);
            }
            F.speed = F.speedHoushiCurve.Evaluate(F.speedCalc);
            F.voice.speedMotion = F.speedCalc > F.speedMaxBody * F.speedVoiceChangeSpeedRate;
        }
        void BeginFinish(bool isInside)
        {
            inside |= isInside;
            if (isInside) F.AddHoushiInside(); else { F.AddHoushiOutside(); if (F.nowAnimationInfo.isSplash) F.AddSplash(); }
            F.AddHandFinsh(); // This native definition is sysTaii=0, kindHoushi=0.
            Count(isInside ? "inside" : "outside"); Set(isInside ? "IN_Start" : "M_OUT_Start");
            if (isInside) F.timeDecreaseGaugeCalc = default(float); else Voice("206");
        }
        void Wait(string next, string request) { waiting = true; voiceFinished = false; F.voiceWait = true; pending = next; Voice(request, true); }
        void Set(string state) { State = state; if (state == "WLoop" || state == "SLoop") IntegrateSpeed(default(float)); effects.Add(new ProcEffect("state", state)); }
        void Voice(string request, bool wait = false) => effects.Add(new ProcEffect(wait ? "kks-wait-voice" : "kks-voice", request));
        void Count(string kind) => effects.Add(new ProcEffect("count", kind));
        public void SetManualSpeed(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < default(float) || value > P.clipEnd) throw new ArgumentOutOfRangeException(nameof(value));
            F.speedCalc = value; F.speed = F.speedHoushiCurve.Evaluate(value);
        }
        public void Stop() { stopped = true; waiting = false; effects.Clear(); }
    }
}
