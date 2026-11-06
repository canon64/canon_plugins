using System;
using UnityEngine;

namespace MapEnvironmentRuntime
{
    public sealed class CloudLayerState
    {
        public string type;
        public float weight, baseMinimum, baseMaximum, maximumThickness, lifetime, phase;
        public double generation;
        public Vector3 wind;
    }
    public sealed class CloudField
    {
        public double Seconds { get; private set; }
        public CloudLayerState[] Layers { get; private set; }
        private Vector4[] _bounds, _shape, _pattern, _origins, _winds, _times, _seeds;
        private Parameters _settings;
        private double _travel;
        public float[] Weights { get; private set; }
        private float MonthWeight(Parameters p, string type, EnvironmentWorld world)
        {
            if (p.T("cloud.mode") == "type" || p.T("cloud.debug") == "single") return type == p.T("cloud.fixedType") ? p.N("math.one") : p.N("math.zero");
            if (p.T("cloud.mode") == "season") return p.N("cloud.season." + p.T("cloud.fixedSeason") + "." + type);
            float one=p.N("math.one"), months=p.N("calendar.months");
            float position=world.Month-one+(world.Day-one+world.Hour/p.N("clock.hoursPerDay"))/CalendarClock.DaysInMonth(world.Year,world.Month,p)-p.N("climate.monthAnchor");
            float left=Mathf.Floor(position), blend=Mathf.SmoothStep(p.N("math.zero"),one,position-left);
            return Mathf.Lerp(SeasonWeight(p,type,(int)Mathf.Repeat(left,months)+p.I("math.one")),SeasonWeight(p,type,(int)Mathf.Repeat(left+one,months)+p.I("math.one")),blend);
        }
        private static float SeasonWeight(Parameters p,string type,int month)
        {
            string season=month<=p.I("math.two") || month==p.I("calendar.months") ? "winter" : month<=p.I("math.two")*p.I("math.two")+p.I("math.one") ? "spring" : month<=p.I("math.two")*p.I("math.two")*p.I("math.two") ? "summer" : "autumn";
            return p.N("cloud.season."+season+"."+type);
        }
        private static float SeasonGrowth(Parameters p,string prefix,int month)
        {
            string season=month<=p.I("math.two") || month==p.I("calendar.months") ? "Winter" : month<=p.I("math.two")*p.I("math.two")+p.I("math.one") ? "Spring" : month<=p.I("math.two")*p.I("math.two")*p.I("math.two") ? "Summer" : "Autumn";
            return p.N(prefix+"heightSeason"+season);
        }
        public void Update(EnvironmentWorld world, double travel)
        {
            var p=world.Settings;
            if (_settings!=p)
            {
                _settings=p; int count=p.I("cloud.typeCount");
                _bounds=new Vector4[count]; _shape=new Vector4[count]; _pattern=new Vector4[count]; _origins=new Vector4[count];
                _winds=new Vector4[count]; _times=new Vector4[count]; _seeds=new Vector4[count]; Weights=new float[count]; Layers=new CloudLayerState[count];
                for(int i=p.I("math.zero");i<count;i++) Layers[i]=new CloudLayerState{type=p.cloudTypes[i]};
            }
            Seconds=CalendarClock.AbsoluteSeconds(world.Year,world.Month,world.Day,world.Hour,p)*p.N("cloud.timeScale"); _travel=travel;
            float total=p.N("math.zero");
            for(int i=p.I("math.zero");i<Weights.Length;i++) { Weights[i]=MonthWeight(p,p.cloudTypes[i],world); total+=Weights[i]; }
            if(total<=p.N("math.zero")) throw new InvalidOperationException("Cloud seasonal weights have no positive total");
            for(int i=p.I("math.zero");i<Weights.Length;i++)
            {
                string key="cloud.type."+p.cloudTypes[i]+".";
                Weights[i]/=total;
                float baseLow=p.N(key+"baseMin"),baseHigh=p.N(key+"baseMax");
                if(p.cloudTypes[i]=="cumulus" || p.cloudTypes[i]=="stratocumulus") { baseHigh+=p.N("cloud.base")-baseLow; baseLow=p.N("cloud.base"); }
                float growth=p.N(key+"growth");
                if(p.cloudTypes[i]=="cumulus")
                {
                    float position=world.Month-p.N("math.one")+(world.Day-p.N("math.one")+world.Hour/p.N("clock.hoursPerDay"))/CalendarClock.DaysInMonth(world.Year,world.Month,p)-p.N("climate.monthAnchor");
                    float left=Mathf.Floor(position),blend=Mathf.SmoothStep(p.N("math.zero"),p.N("math.one"),position-left);
                    float seasonal=p.T("cloud.mode")=="season" ? p.N(key+"heightSeason"+char.ToUpperInvariant(p.T("cloud.fixedSeason")[p.I("math.zero")])+p.T("cloud.fixedSeason").Substring(p.I("math.one"))) :
                        Mathf.Lerp(SeasonGrowth(p,key,(int)Mathf.Repeat(left,p.N("calendar.months"))+p.I("math.one")),SeasonGrowth(p,key,(int)Mathf.Repeat(left+p.N("math.one"),p.N("calendar.months"))+p.I("math.one")),blend);
                    growth*=seasonal*world.CloudDevelopment;
                }
                _bounds[i]=new Vector4(baseLow+world.GroundHeight,baseHigh+world.GroundHeight,p.N(key+"heightMin")*growth,p.N(key+"heightMax")*growth);
                _shape[i]=new Vector4(p.N(key+"widthMin"),p.N(key+"widthMax"),p.N(key+"cell"),p.N(key+"density"));
                _pattern[i]=new Vector4(p.N(key+"kind"),p.N(key+"anisotropy"),p.N(key+"edge"),p.N(key+"frequency"));
                double cycles=Seconds/p.N(key+"lifetime");
                _times[i]=new Vector4((float)(cycles-Math.Floor(cycles)),(float)(Math.Floor(cycles)%p.N("cloud.seedWrap")),Weights[i],p.N(key+"seed"));
                var direction=Quaternion.AngleAxis(p.N(key+"windAngle"),Vector3.up)*new Vector3(p.N("cloud.windX"),p.N("math.zero"),p.N("cloud.windZ"))*p.N(key+"windScale");
                var velocity=Quaternion.AngleAxis(p.N(key+"windAngle"),Vector3.up)*world.WindVelocity*p.N(key+"windScale");
                _winds[i]=new Vector4(velocity.x,velocity.z,direction.x,direction.z);
                _seeds[i]=new Vector4(direction.x,direction.z,p.N(key+"seed"),p.N(key+"lifetime"));
                Layers[i].weight=Weights[i]; Layers[i].baseMinimum=baseLow; Layers[i].baseMaximum=baseHigh;
                Layers[i].maximumThickness=_bounds[i].w; Layers[i].lifetime=p.N(key+"lifetime"); Layers[i].phase=_times[i].x; Layers[i].generation=Math.Floor(cycles); Layers[i].wind=velocity;
            }
        }
        public void Bind(Material material,Vector3 observer,EnvironmentWorld world)
        {
            var p=world.Settings;
            for(int i=p.I("math.zero");i<_origins.Length;i++)
            {
                double cell=_shape[i].z;
                double travel=p.T("cloud.debug")=="single" ? p.N("math.zero") : _travel;
                double x=(double)observer.x-travel*_seeds[i].x,z=(double)observer.z-travel*_seeds[i].y;
                double cx=Math.Floor(x/cell),cz=Math.Floor(z/cell);
                _origins[i]=new Vector4((float)(x-cx*cell),(float)(z-cz*cell),(float)(cx%p.N("cloud.seedWrap")),(float)(cz%p.N("cloud.seedWrap")));
            }
            material.SetVectorArray("_LayerBounds",_bounds); material.SetVectorArray("_LayerShape",_shape); material.SetVectorArray("_LayerPattern",_pattern);
            material.SetVectorArray("_LayerOrigins",_origins); material.SetVectorArray("_LayerWind",_winds); material.SetVectorArray("_LayerTime",_times);
            material.SetVector("_CloudCamera",observer); material.SetFloat("_Coverage",world.CloudCoverage); material.SetFloat("_Density",world.CloudDensity);
            material.SetVector("_SunDirection",world.SunDirection); material.SetVector("_NightAmbient",world.NightAmbient);
            material.SetFloat("_SummerFactor",world.SummerCloudFactor);
            material.SetFloat("_LayoutSeed",p.N("cloud.layoutSeed"));
            material.SetFloat("_SingleCloud",p.T("cloud.debug")=="single" ? p.N("math.one") : p.N("math.zero"));
            string prefix="cloud.type.cumulus.";
            foreach(string key in new[]{"towerChance","towerMin","towerMax","lobes","lobeSpread","lobeRadius","lobeHeight","lobeVertical","baseFade","lowerRadius","mainHeight","sheetDensity","fibres","waveScale","waveStrength"})
                material.SetFloat("_"+char.ToUpperInvariant(key[p.I("math.zero")])+key.Substring(p.I("math.one")),p.N(prefix+key));
        }
    }
}
