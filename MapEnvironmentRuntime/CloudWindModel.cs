using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace MapEnvironmentRuntime
{
    public sealed class CloudWindModel
    {
        private sealed class Segment
        {
            public double start, duration, transition, previous, target, integral;
            public string state;
        }
        private readonly List<Segment> _segments = new List<Segment>();
        private string _signature;
        private double _duration, _integral;
        public string State { get; private set; }
        public Vector3 Velocity { get; private set; }
        public double Travel { get; private set; }
        public void Configure(Parameters p)
        {
            string signature = string.Join(";", p.numbers.Where(item => item.key.StartsWith("wind.") || item.key == "clock.secondsPerMinute" || item.key == "calendar.minutesPerHour")
                .Select(item => item.key + "=" + item.value.ToString("R", CultureInfo.InvariantCulture)));
            if (signature == _signature) return;
            _segments.Clear(); _duration = p.N("math.zero"); _integral = p.N("math.zero");
            var random = new System.Random(p.I("wind.seed"));
            double secondsPerHour = p.N("calendar.minutesPerHour") * p.N("clock.secondsPerMinute");
            double total = p.N("wind.normalWeight") + p.N("wind.calmWeight") + p.N("wind.strongWeight");
            double calmWeight = p.N("wind.calmWeight"), strongWeight = p.N("wind.strongWeight");
            double holdMin = p.N("wind.holdMinHours"), holdSpan = p.N("wind.holdMaxHours") - holdMin;
            double transition = p.N("wind.transitionHours") * secondsPerHour;
            var minimum = new Dictionary<string, double>(); var maximum = new Dictionary<string, double>();
            foreach (string state in new[] { "calm", "strong", "normal" }) { minimum[state] = p.N("wind." + state + "Min"); maximum[state] = p.N("wind." + state + "Max"); }
            for (int index = p.I("math.zero"); index < p.I("wind.segments"); index++)
            {
                double choice = random.NextDouble() * total;
                string state = choice < calmWeight ? "calm" : choice < calmWeight + strongWeight ? "strong" : "normal";
                double target = minimum[state] + random.NextDouble() * (maximum[state] - minimum[state]);
                double duration = (holdMin + random.NextDouble() * holdSpan) * secondsPerHour;
                _segments.Add(new Segment { start = _duration, duration = duration, transition = transition, target = target, state = state });
                _duration += duration;
            }
            double previous = _segments.Last().target;
            foreach (var segment in _segments)
            {
                segment.previous = previous; segment.integral = _integral;
                _integral += (previous + segment.target) * p.N("math.half") * segment.transition + segment.target * (segment.duration - segment.transition);
                previous = segment.target;
            }
            _signature = signature;
        }
        public Vector3 Evaluate(double seconds, Parameters p)
        {
            double cycle = Math.Floor(seconds / _duration), position = seconds - cycle * _duration;
            var segment = _segments.Last(item => item.start <= position);
            double elapsed = position - segment.start;
            double u = Math.Min(p.N("math.one"), elapsed / segment.transition);
            double blend = u * u * (p.N("noise.smoothA") - p.N("noise.smoothB") * u);
            double speed = segment.previous + (segment.target - segment.previous) * blend;
            double area = segment.previous * Math.Min(elapsed, segment.transition)
                + (segment.target - segment.previous) * segment.transition * (u * u * u - p.N("math.half") * u * u * u * u)
                + segment.target * Math.Max(p.N("math.zero"), elapsed - segment.transition);
            double travel = cycle * _integral + segment.integral + area;
            Travel = travel;
            State = segment.state;
            Velocity = new Vector3((float)(p.N("cloud.windX") * speed), p.N("math.zero"), (float)(p.N("cloud.windZ") * speed));
            return new Vector3((float)(travel * p.N("cloud.windX") % p.N("cloud.advectionWrap")),
                (float)(seconds * p.N("cloud.evolution") % p.N("cloud.evolutionWrap")),
                (float)(travel * p.N("cloud.windZ") % p.N("cloud.advectionWrap")));
        }
    }
}
