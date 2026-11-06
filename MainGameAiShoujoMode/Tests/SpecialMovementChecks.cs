using System;
using System.IO;
using System.Linq;
using MainGameAiShoujoMode;
using MapEnvironmentRuntime;

internal static class SpecialMovementChecks
{
    internal static void Run(Parameters p,string content)
    {
        var manifest=JsonCodec.FromJson<SpecialMovementManifest>(File.ReadAllText(Path.Combine(content,p.T("traversal.manifest"))));manifest.Validate(p);
        var copy=JsonCodec.FromJson<SpecialMovementManifest>(JsonCodec.ToJson(manifest));copy.Validate(p);
        var selectionPoint=new SpecialMovementPoint{command=UnityEngine.Vector3.zero,start=UnityEngine.Vector3.right*p.N("math.two")*p.N("math.two")};
        if(SpecialMovementSelection.Choose(new[]{selectionPoint},UnityEngine.Vector3.zero,p.N("math.one"),point=>point.command)!=selectionPoint)throw new Exception("Selection must use the command anchor even when the animation pivot is outside reach");
        if(SpecialMovementSelection.Choose(new[]{selectionPoint},UnityEngine.Vector3.right*p.N("math.two"),p.N("math.one"),point=>point.command)!=null)throw new Exception("Out-of-reach command anchor selected");
        Console.WriteLine("SPECIAL_MOVEMENT_COMMAND_ANCHOR_ALL_AROUND_PASS");
        if(copy.points.Length!=manifest.points.Length||copy.points.Where((point,index)=>point.id!=manifest.points[index].id||point.femaleSegments.Length!=manifest.points[index].femaleSegments.Length).Any())throw new Exception("Special movement JSON round trip changed points");
        Console.WriteLine("SPECIAL_MOVEMENT_MANIFEST_PASS endpoints="+manifest.points.Length);
        CheckRelease();
        foreach(var endpoint in manifest.points)
            foreach(bool male in new[]{false,true})
            {
                var segments=male?endpoint.maleSegments:endpoint.femaleSegments;
                var seconds=(male?endpoint.maleDuration:endpoint.femaleDuration)/p.N("traversal.speed");
                var keys=SpecialMovementTimeline.Build(endpoint.start,segments,seconds);
                if(keys.First().time!=p.N("math.zero")||keys.Last().time!=seconds||!keys.Last().position.Equals(segments.Last().position))throw new Exception("Passage endpoints changed");
                foreach(var pair in keys.Zip(keys.Skip(p.I("math.one")),(a,b)=>new[]{a,b}))
                {
                    var a=pair.First();var b=pair.Last();
                    if(b.time<=a.time)throw new Exception("Duplicate or reversed passage keys");
                    var velocity=(b.position-a.position)/(b.time-a.time);
                    if(new[]{velocity.x,velocity.y,velocity.z}.Any(axis=>float.IsNaN(axis)||float.IsInfinity(axis)))throw new Exception("Non-finite passage tangent");
                }
            }
        var contiguous=new[]{new SpecialMovementSegment{start=p.N("math.zero"),end=p.N("math.half"),position=UnityEngine.Vector3.right},
            new SpecialMovementSegment{start=p.N("math.half"),end=p.N("math.one"),position=UnityEngine.Vector3.up}};
        var boundary=SpecialMovementTimeline.Build(UnityEngine.Vector3.zero,contiguous,p.N("math.one"));
        if(boundary.Select(key=>key.time).Distinct().Count()!=boundary.Length)throw new Exception("Shared segment boundaries were not merged");
        ExpectInvalid(()=>SpecialMovementTimeline.Build(UnityEngine.Vector3.zero,contiguous,float.NaN),"invalid traversal duration");
        Console.WriteLine("SPECIAL_MOVEMENT_ALL_ENDPOINT_TIMELINES_PASS variants="+manifest.points.Length*p.I("math.two"));
        var edges=new[]{new SpecialMovementEdge{from="a",to="b",cost=p.N("math.one")},new SpecialMovementEdge{from="b",to="c",cost=p.N("math.one"),passage=manifest.points.First().id},new SpecialMovementEdge{from="a",to="c",cost=p.N("math.two")+p.N("math.one")},new SpecialMovementEdge{from="b",to="a",cost=p.N("math.one")}};
        var route=SpecialMovementRoutes.Find(edges,"a","c");
        if(route==null||route.Length!=p.I("math.two")||!route.Last().passage.HasValue)throw new Exception("Special route did not select shorter passage path");
        if(SpecialMovementRoutes.Find(edges,"c","a")!=null)throw new Exception("Disconnected special route was accepted");
        if(SpecialMovementRoutes.Find(edges,"a","a").Any())throw new Exception("Already arrived route must be empty");
        var point=copy.points.First();point.linkedId=point.id;
        ExpectInvalid(()=>copy.Validate(p),"reciprocal partner");
        copy=JsonCodec.FromJson<SpecialMovementManifest>(JsonCodec.ToJson(manifest));copy.points.First().femaleSegments.First().end=p.N("math.zero");
        ExpectInvalid(()=>copy.Validate(p),"invalid timing");
        copy=JsonCodec.FromJson<SpecialMovementManifest>(JsonCodec.ToJson(manifest));copy.points.First().femaleDuration=float.NaN;
        ExpectInvalid(()=>copy.Validate(p),"non-finite duration");
        edges.First().cost=-p.N("math.one");ExpectInvalid(()=>SpecialMovementRoutes.Find(edges,"a","c"),"negative route cost");
        Console.WriteLine("SPECIAL_MOVEMENT_ROUTE_TESTS_PASS");
    }
    private static void ExpectInvalid(Action action,string reason)
    {
        try{action();}catch(InvalidDataException){return;}throw new Exception("Accepted "+reason);
    }
    private static void CheckRelease()
    {
        bool occupied=true,ready=false;int restores=default(int);
        var release=new ExternalMotionRelease(()=>true,()=>occupied=false,()=>
        { if(occupied)throw new Exception("Motion occupancy was not cleared before resume");restores++; },()=>ready=true);
        release.Release();release.Release();
        if(occupied||!ready||restores!=Math.Sign(int.MaxValue))throw new Exception("Release did not unlock exactly once");
        occupied=true;ready=false;
        release=new ExternalMotionRelease(()=>true,()=>occupied=false,()=>throw new InvalidOperationException("Simulated exit rebind failure"),()=>ready=true);
        try{release.Release();throw new Exception("Restore failure hidden");}catch(InvalidOperationException){}
        if(occupied||!ready)throw new Exception("Restore failure left the girl unapproachable");
        occupied=true;
        release=new ExternalMotionRelease(()=>false,()=>occupied=false,()=>throw new Exception("Old actor resumed"),()=>throw new Exception("Old actor notified"));
        release.Release();if(!occupied)throw new Exception("Old release changed a replacement actor");
        Console.WriteLine("SPECIAL_MOVEMENT_RELEASE_PASS cancel restoreFailure duplicate actorChange");
    }
}
