using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace MapEnvironmentRuntime
{
    public static class SeasonalClimate
    {
        public static float MonthValue(Parameters p, string prefix, int year, int month, int day, float hour)
        {
            float one = p.N("math.one"), months = p.N("calendar.months");
            float position = month - one + (day - one + hour / p.N("clock.hoursPerDay")) / CalendarClock.DaysInMonth(year, month, p) - p.N("climate.monthAnchor");
            float left = Mathf.Floor(position), blend = Mathf.SmoothStep(p.N("math.zero"), one, position - left);
            int a = (int)Mathf.Repeat(left, months) + p.I("math.one"), b = (int)Mathf.Repeat(left + one, months) + p.I("math.one");
            return Mathf.Lerp(p.N(prefix + a.ToString(CultureInfo.InvariantCulture)), p.N(prefix + b.ToString(CultureInfo.InvariantCulture)), blend);
        }
        public static float Sunrise(Parameters p, int year, int month, int day)
        {
            float radians = p.N("math.pi") / p.N("math.degreesHalfTurn");
            float declination = p.N("clock.declination") + p.N("calendar.axialTilt") * Mathf.Sin(p.N("math.tau") *
                (CalendarClock.DayOfYear(year, month, day, p) - p.N("calendar.equinoxDay")) / CalendarClock.DaysInYear(year, p));
            float cosine = Mathf.Clamp(-Mathf.Tan(p.N("clock.latitude") * radians) * Mathf.Tan(declination * radians), -p.N("math.one"), p.N("math.one"));
            float sunrise = p.N("clock.hoursPerDay") * (p.N("math.half") - Mathf.Acos(cosine) / p.N("math.tau"));
            return Mathf.Clamp(sunrise, p.N("math.zero"), p.N("temperature.peakHour") - p.N("render.epsilon"));
        }
    }

    public sealed class TemperatureModel
    {
        private sealed class Variation { public double start, days, previous, target; }
        private readonly List<Variation> _variation = new List<Variation>();
        private readonly Dictionary<int, float> _exponents = new Dictionary<int, float>();
        private string _signature;
        private double _cycleDays;
        private int _cachedYear, _cachedMonth, _cachedDay;
        public float Current { get; private set; }
        public float Minimum { get; private set; }
        public float Maximum { get; private set; }
        public void Configure(Parameters p)
        {
            string signature = string.Join(";", p.numbers.Where(item => item.key.StartsWith("temperature.") || item.key.StartsWith("calendar.") || item.key.StartsWith("clock.") || item.key.StartsWith("climate."))
                .Where(item => item.key != "clock.hours" && item.key != "calendar.year" && item.key != "calendar.month" && item.key != "calendar.day")
                .Select(item => item.key + "=" + item.value.ToString("R", CultureInfo.InvariantCulture)));
            if (_signature == signature) return;
            _exponents.Clear(); _variation.Clear(); _cycleDays = p.N("math.zero");
            for (int month = p.I("math.one"); month <= p.I("calendar.months"); month++)
            {
                string suffix = month.ToString(CultureInfo.InvariantCulture);
                float ratio = (p.N("temperature.mean." + suffix) - p.N("temperature.low." + suffix)) /
                    (p.N("temperature.high." + suffix) - p.N("temperature.low." + suffix));
                float low = p.N("temperature.powerMin"), high = p.N("temperature.powerMax");
                for (int iteration = p.I("math.zero"); iteration < p.I("temperature.solveIterations"); iteration++)
                {
                    float exponent = (low + high) * p.N("math.half"), average = p.N("math.zero");
                    for (int sample = p.I("math.zero"); sample < p.I("temperature.integrationSteps"); sample++)
                    {
                        float wave = (p.N("math.one") - Mathf.Cos(p.N("math.pi") * (sample + p.N("math.half")) / p.N("temperature.integrationSteps"))) * p.N("math.half");
                        average += Mathf.Pow(wave, exponent) / p.N("temperature.integrationSteps");
                    }
                    if (average > ratio) low = exponent; else high = exponent;
                }
                _exponents[month] = (low + high) * p.N("math.half");
            }
            var random = new System.Random(p.I("temperature.seed"));
            for (int index = p.I("math.zero"); index < p.I("temperature.variationSegments"); index++)
            {
                float amplitude = random.NextDouble() < p.N("temperature.extremeProbability") ? p.N("temperature.extremeAmplitude") : p.N("temperature.normalAmplitude");
                double target = (random.NextDouble() * p.N("math.two") - p.N("math.one")) * amplitude;
                double duration = p.N("temperature.changeMinDays") + random.NextDouble() * (p.N("temperature.changeMaxDays") - p.N("temperature.changeMinDays"));
                _variation.Add(new Variation { start = _cycleDays, days = duration, target = target }); _cycleDays += duration;
            }
            double previous = _variation.Last().target;
            foreach (var item in _variation) { item.previous = previous; previous = item.target; }
            _signature = signature; _cachedYear = p.I("math.zero");
        }
        private float Evaluate(Parameters p, int year, int month, int day, float hour)
        {
            float one = p.N("math.one"), half = p.N("math.half"), hours = p.N("clock.hoursPerDay");
            float low = SeasonalClimate.MonthValue(p, "temperature.low.", year, month, day, hour);
            float high = SeasonalClimate.MonthValue(p, "temperature.high.", year, month, day, hour);
            float position = month - one + (day - one + hour / hours) / CalendarClock.DaysInMonth(year, month, p) - p.N("climate.monthAnchor");
            float floor = Mathf.Floor(position), blend = Mathf.SmoothStep(p.N("math.zero"), one, position - floor);
            int a = (int)Mathf.Repeat(floor, p.N("calendar.months")) + p.I("math.one"), b = (int)Mathf.Repeat(floor + one, p.N("calendar.months")) + p.I("math.one");
            float exponent = Mathf.Lerp(_exponents[a], _exponents[b], blend);
            float sunrise = SeasonalClimate.Sunrise(p, year, month, day), peak = p.N("temperature.peakHour");
            float wave;
            if (hour >= sunrise && hour < peak) wave = (one - Mathf.Cos(p.N("math.pi") * (hour - sunrise) / (peak - sunrise))) * half;
            else wave = (one + Mathf.Cos(p.N("math.pi") * Mathf.Repeat(hour - peak, hours) / (hours - peak + sunrise))) * half;
            double absoluteDays = CalendarClock.AbsoluteSeconds(year, month, day, hour, p) / (hours * p.N("calendar.minutesPerHour") * p.N("clock.secondsPerMinute"));
            double cyclePosition = absoluteDays % _cycleDays;
            var change = _variation.Last(item => item.start <= cyclePosition);
            float u = (float)((cyclePosition - change.start) / change.days);
            float anomaly = Mathf.Lerp((float)change.previous, (float)change.target, Mathf.SmoothStep(p.N("math.zero"), one, u));
            return Mathf.Lerp(low, high, Mathf.Pow(Mathf.Clamp01(wave), exponent)) + anomaly;
        }
        public void Update(Parameters p, int year, int month, int day, float hour)
        {
            Current = Evaluate(p, year, month, day, hour);
            if (_cachedYear != year || _cachedMonth != month || _cachedDay != day)
            {
                Minimum = Current; Maximum = Current;
                for (int sample = p.I("math.zero"); sample <= p.I("temperature.dailySamples"); sample++)
                {
                    float value = Evaluate(p, year, month, day, p.N("clock.hoursPerDay") * sample / p.N("temperature.dailySamples"));
                    Minimum = Mathf.Min(Minimum, value); Maximum = Mathf.Max(Maximum, value);
                }
                _cachedYear = year; _cachedMonth = month; _cachedDay = day;
            }
            Minimum = Mathf.Min(Minimum, Current); Maximum = Mathf.Max(Maximum, Current);
        }
    }
}
