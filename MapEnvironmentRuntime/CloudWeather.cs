using System;
using System.Collections.Generic;
using System.Linq;

namespace MapEnvironmentRuntime
{
    public sealed class CloudWeather
    {
        private sealed class Segment { public double start,hold; public string type,previous; }
        private readonly List<Segment> _segments=new List<Segment>();
        private string _signature;
        private Parameters _parameters;
        private double _duration;
        public string Current { get; private set; }
        public string Next { get; private set; }
        public float Coverage { get; private set; }
        public float Density { get; private set; }
        public void Evaluate(double seconds,Parameters p)
        {
            string signature=_parameters==p ? _signature : string.Join(";",p.numbers.Where(n=>n.key.StartsWith("weather.")).Select(n=>n.key+"="+n.value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            _parameters=p;
            if(signature!=_signature)
            {
                _segments.Clear(); _duration=p.N("math.zero"); var random=new Random(p.I("weather.seed"));
                var types=new[]{"clear","partly","cloudy"}; double total=types.Sum(t=>p.N("weather."+t+".weight"));
                for(int index=p.I("math.zero");index<p.I("wind.segments");index++)
                {
                    double pick=random.NextDouble()*total; string type=types.Last();
                    foreach(string candidate in types) { pick-=p.N("weather."+candidate+".weight"); if(pick<=p.N("math.zero")) {type=candidate;break;} }
                    double hold=p.N("weather.holdMin")+random.NextDouble()*(p.N("weather.holdMax")-p.N("weather.holdMin"));
                    _segments.Add(new Segment{start=_duration,hold=hold,type=type}); _duration+=hold+p.N("weather.transitionSeconds");
                }
                string previous=_segments.Last().type;
                foreach(var segment in _segments) { segment.previous=previous; previous=segment.type; }
                _signature=signature;
            }
            double scale=p.N("clock.dayMinutes")/(p.N("clock.hoursPerDay")*p.N("calendar.minutesPerHour"));
            double position=seconds*scale; position-=Math.Floor(position/_duration)*_duration;
            var selected=_segments.Last(s=>s.start<=position);
            float blend=UnityEngine.Mathf.SmoothStep(p.N("math.zero"),p.N("math.one"),UnityEngine.Mathf.Clamp01((float)((position-selected.start-selected.hold)/p.N("weather.transitionSeconds"))));
            Current=blend<p.N("math.one") ? selected.previous : selected.type; Next=selected.type;
            Coverage=UnityEngine.Mathf.Lerp(p.N("weather."+selected.previous+".coverage"),p.N("weather."+selected.type+".coverage"),blend)*p.N("cloud.coverage")/p.N("weather.coverageReference");
            Density=UnityEngine.Mathf.Lerp(p.N("weather."+selected.previous+".density"),p.N("weather."+selected.type+".density"),blend)*p.N("cloud.density")/p.N("weather.densityReference");
        }
    }
}
