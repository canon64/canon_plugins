using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    public sealed class SpecialMovementSegment { public float start,end; public Vector3 position; }
    public sealed class SpecialMovementRotation { public float x,y,z,w; }
    public sealed class SpecialMovementPoint
    {
        public int id,linkedId;
        public string kind,label,femaleState,maleState,commandName;
        public Vector3 start;
        public Vector3 command;
        public SpecialMovementRotation rotation;
        public SpecialMovementSegment[] femaleSegments,maleSegments;
        public float femaleDuration,maleDuration;
    }
    public sealed class SpecialMovementManifest
    {
        public string femaleController,maleController;
        public bool nativeSamplingPassed;
        public SpecialMovementPoint[] points;
        public void Validate(Parameters p)
        {
            if(!nativeSamplingPassed || points==null || !points.Any() || string.IsNullOrEmpty(femaleController) || string.IsNullOrEmpty(maleController)) throw new InvalidDataException("特殊移動の変換検証が未完了です。");
            var lookup=points.ToDictionary(point=>point.id);
            foreach(var point in points)
            {
                if(string.IsNullOrEmpty(point.commandName)||!Finite(point.command))throw new InvalidDataException("特殊通路の操作基準点がありません："+point.id);
                if(point.rotation==null||string.IsNullOrWhiteSpace(point.femaleState)||string.IsNullOrWhiteSpace(point.maleState)||!Finite(point.start)||new[]{point.rotation.x,point.rotation.y,point.rotation.z,point.rotation.w,point.femaleDuration,point.maleDuration}.Any(value=>float.IsNaN(value)||float.IsInfinity(value)))throw new InvalidDataException("特殊通路の座標・演技情報が不正です："+point.id);
                if(!lookup.TryGetValue(point.linkedId,out var pair) || pair.linkedId!=point.id || pair.id==point.id) throw new InvalidDataException("特殊通路の対が不正です："+point.id);
                foreach(var segments in new[]{point.femaleSegments,point.maleSegments})
                {
                    if(segments==null || !segments.Any()) throw new InvalidDataException("特殊通路の出口がありません："+point.id);
                    float end=p.N("math.zero");
                    foreach(var segment in segments)
                    {
                        if(float.IsNaN(segment.start)||float.IsNaN(segment.end)||!Finite(segment.position)||segment.start<end||segment.end<=segment.start||segment.end>p.N("math.one")) throw new InvalidDataException("特殊通路の移動時間が不正です："+point.id);
                        end=segment.end;
                    }
                }
                if(point.femaleDuration<=p.N("math.zero")||point.maleDuration<=p.N("math.zero"))throw new InvalidDataException("特殊通路の演技時間が不正です："+point.id);
            }
        }
        private static bool Finite(Vector3 value)=>!new[]{value.x,value.y,value.z}.Any(axis=>float.IsNaN(axis)||float.IsInfinity(axis));
    }
    public sealed class SpecialMovementEdge
    {
        public string from,to;
        public float cost;
        public int? passage;
    }
    public static class SpecialMovementSelection
    {
        // Original Move points use CommandType.AllAround. The animation pivot is not the command target.
        public static SpecialMovementPoint Choose(IEnumerable<SpecialMovementPoint> points,Vector3 player,float reach,Func<SpecialMovementPoint,Vector3> command)
            =>points.Where(point=>Vector3.Distance(player,command(point))<=reach).OrderBy(point=>Vector3.Distance(player,command(point))).FirstOrDefault();
    }
    // Pure operation-time shortest path. No Unity scene searches or frame callbacks.
    public static class SpecialMovementRoutes
    {
        public static SpecialMovementEdge[] Find(IEnumerable<SpecialMovementEdge> edges,string start,string end)
        {
            var remaining=edges.ToArray();var distance=new Dictionary<string,float>{{start,default(float)}};
            var previous=new Dictionary<string,SpecialMovementEdge>();var visited=new HashSet<string>();
            while(true)
            {
                var candidates=distance.Where(item=>!visited.Contains(item.Key)).OrderBy(item=>item.Value).ToArray();
                if(!candidates.Any())return null;
                var current=candidates.First();if(current.Key==end)break;visited.Add(current.Key);
                foreach(var edge in remaining.Where(edge=>edge.from==current.Key))
                {
                    if(float.IsNaN(edge.cost)||float.IsInfinity(edge.cost)||edge.cost<default(float))throw new InvalidDataException("特殊通路の経路費用が不正です。");
                    float next=current.Value+edge.cost;
                    if(distance.TryGetValue(edge.to,out float known)&&known<=next)continue;
                    distance[edge.to]=next;previous[edge.to]=edge;
                }
            }
            var result=new List<SpecialMovementEdge>();string cursor=end;
            while(cursor!=start){var edge=previous[cursor];result.Add(edge);cursor=edge.from;}
            result.Reverse();return result.ToArray();
        }
    }
}
