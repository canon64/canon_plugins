using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Valve.VR;
using N = System.Numerics.Vector3;

namespace MainGameMagicPaint
{
    public sealed partial class Plugin
    {
        DiagnosticTrace _trace;
        bool _traceNextSample, _traceHeader;
        Dictionary<PaintActor, long[]> _traceStrokes;
        bool TraceEnabled => _logging != null && _logging.Value;
        string TracePath => Path.Combine(SaveDirectory,"Diagnostics","magicpaint.log");
        void SyncTrace()
        {
            if (TraceEnabled)
            {
                if (_trace == null) _trace = new DiagnosticTrace(TracePath);
                _trace.SetEnabled(true);
            }
            else { foreach(var actor in _actors){if(actor.Face!=null)actor.Face.RasterTrace=null;if(actor.Body!=null)actor.Body.RasterTrace=null;} _trace?.Dispose(); _trace = null; _traceStrokes = null; _traceNextSample = _traceHeader = false; }
        }
        void TraceWrite(string category, Func<string> message)
        {
            if (!TraceEnabled) return;
            try
            {
                SyncTrace();
                if(!_traceHeader)
                {
                    _traceHeader=true;
                    _trace.Write("BUILD",()=>"assembly="+GetType().Assembly.Location+" writeUtc="+File.GetLastWriteTimeUtc(GetType().Assembly.Location).ToString("O")+" diagnostic=current-skin-pose-20261031");
                }
                _trace.Write(category,message);
            }
            catch (Exception ex) { _status = "診断ログ書込失敗："+ex.Message; }
        }
        void TraceInput(string action, SteamVR_Input_Sources source, bool accepted)
        {
            if (!TraceEnabled) return;
            TraceWrite("INPUT",()=>"action="+action+" source="+source+" accepted="+accepted+
                " held="+_vrPaint.Held+" heldSource="+_handSource+" focused="+_focused+
                " tracking="+(_hand!=null&&_hand.IsTracking)+" lock="+(_focus!=null&&_focus.IsValid)+
                " trigger="+_trigger+" contact="+_contact+" guide="+_sprayReady+" menu="+_stick.Menu+" capturePending="+_inspectionPending+" status="+_status);
            if (action == "TRIGGER_DOWN") _traceNextSample = accepted;
        }
        void TraceContact(int end,string reason,Vector3 raw,float limit,SurfaceHit hit)
        {
            if (!TraceEnabled) return;
            var renderer=hit.Probe?.Renderer;
            string signature=reason+":"+(renderer!=null?renderer.GetInstanceID():0);
            try
            {
                SyncTrace(); _trace.Transition(end==0?"NIB_CONTACT":"ERASER_CONTACT",signature,()=>
                    "result="+reason+" mesh="+(renderer!=null?renderer.name:"none")+" distanceMm="+(renderer!=null?(hit.Distance*1000).ToString("F3",CultureInfo.InvariantCulture):"NA")+
                    " limitMm="+(limit*1000).ToString("F3",CultureInfo.InvariantCulture)+" raw="+raw+" contact="+hit.Point+" normal="+hit.Normal+" trigger="+_trigger);
            }
            catch(Exception ex){_status="診断ログ書込失敗："+ex.Message;}
        }
        void TraceSpray(string reason,SurfaceHit hit,float radius)
        {
            if(!TraceEnabled)return;
            var probe=hit.Probe;
            try
            {
                SyncTrace();_trace.Transition("SPRAY_GATE",reason+":"+(probe?.Renderer!=null?probe.Renderer.GetInstanceID():0),()=>
                    "result="+reason+" mesh="+(probe?.Renderer!=null?probe.Renderer.name:"none")+" distanceMm="+(hit.Distance*1000).ToString("F3",CultureInfo.InvariantCulture)+" radiusMm="+(radius*1000)+" held="+_vrPaint.Held+" trigger="+_trigger+" guide="+_sprayReady);
            }
            catch(Exception ex){_status="診断ログ書込失敗："+ex.Message;}
        }
        void TraceSample()
        {
            if(!TraceEnabled||!_traceNextSample)return;_traceNextSample=false;
            TraceGeometry("TRIGGER_SAMPLE");
        }
        void TraceGeometry(string reason)
        {
            if(!TraceEnabled||_model==null)return;
            Vector3 displayed=_model.Tip.position;
            Vector3 raw=displayed-(_model.Visual.position-_model.Root.transform.position);
            TraceWrite("GEOMETRY",()=>reason+" rawTip="+raw+" displayedTip="+displayed+" eraser="+_model.Eraser.position+
                " snapMm="+_snap.Value+" detachMm="+_detach.Value+" markerWidthMm="+_markerWidth.Value+" transparency="+_transparency.Value);
            foreach(var actor in _actors)
            {
                if(actor.Actor==null)continue;
                foreach(bool face in new[]{true,false})
                {
                    var renderer=face?actor.Actor.rendFace:actor.Actor.rendBody;
                    var probe=actor.Probes.Find(p=>p.Renderer==renderer);
                    if(renderer==null||probe?.Index==null){TraceWrite("MESH",()=>"part="+(face?"face":"body")+" no cached geometry");continue;}
                    var label=face?"face":"body";
                    TraceWrite("MESH",()=>
                    {
                        var text=new StringBuilder();var transform=renderer.transform;
                        text.Append("part=").Append(label).Append(" actor=").Append(actor.Actor.chaFile.parameter.fullname).Append(" mesh=").Append(renderer.name)
                            .Append(" frame=").Append(probe.SampleFrame).Append(" enabled=").Append(renderer.enabled).Append(" forceOff=").Append(renderer.forceRenderingOff)
                            .Append(" position=").Append(transform.position).Append(" lossyScale=").Append(transform.lossyScale)
                            .Append(" vertices=").Append(probe.Vertices.Length).Append(" triangles=").Append(probe.Triangles.Length/3).Append(" uv=").Append(probe.Uv.Length);
                        N min=new N(float.PositiveInfinity),max=new N(float.NegativeInfinity);
                        foreach(var vertex in probe.NumericVertices){min=N.Min(min,vertex);max=N.Max(max,vertex);}
                        text.Append(" cachedWorldMin=").Append(min).Append(" cachedWorldMax=").Append(max);
                        if(probe.Index.Near(MeshProbe.Num(raw),1000,out int triangle,out N closest,false))
                        {
                            text.Append(" nearestMm=").Append((N.Distance(closest,MeshProbe.Num(raw))*1000).ToString("F3",CultureInfo.InvariantCulture)).Append(" triangle=").Append(triangle).Append(" closest=").Append(closest);
                            N a=probe.NumericVertices[probe.Triangles[triangle]],b=probe.NumericVertices[probe.Triangles[triangle+1]],c=probe.NumericVertices[probe.Triangles[triangle+2]];
                            N normal=N.Normalize(N.Cross(b-a,c-a));text.Append(" signedDepthMm=").Append((N.Dot(MeshProbe.Num(raw)-closest,normal)*1000).ToString("F3",CultureInfo.InvariantCulture));
                        }
                        // Compare coordinate conventions without rebaking or changing actual paint geometry.
                        text.Append(" transformMatrixNearestMm=").Append(NearestUsingMatrix(probe,transform.localToWorldMatrix,raw).ToString("F3",CultureInfo.InvariantCulture));
                        text.Append(" scaleFreeNearestMm=").Append(NearestUsingMatrix(probe,Matrix4x4.TRS(transform.position,transform.rotation,Vector3.one),raw).ToString("F3",CultureInfo.InvariantCulture));
                        return text.ToString();
                    });
                }
            }
        }
        static float NearestUsingMatrix(MeshProbe probe,Matrix4x4 matrix,Vector3 point)
        {
            var vertices=new N[probe.LocalVertices.Count];for(int i=0;i<vertices.Length;i++)vertices[i]=MeshProbe.Num(matrix.MultiplyPoint3x4(probe.LocalVertices[i]));
            N raw=MeshProbe.Num(point);float best=float.PositiveInfinity;
            for(int i=0;i<probe.Triangles.Length;i+=3)
            {
                N closest=SurfaceMath.Closest(raw,vertices[probe.Triangles[i]],vertices[probe.Triangles[i+1]],vertices[probe.Triangles[i+2]]);
                best=Math.Min(best,N.DistanceSquared(raw,closest));
            }
            return (float)Math.Sqrt(best)*1000;
        }
        void TraceStrokeBegin(PaintActor actor)
        {
            if(!TraceEnabled)return;
            if(actor.Face!=null&&actor.Face.RasterTrace==null)actor.Face.RasterTrace=new BrushStats();
            if(actor.Body!=null&&actor.Body.RasterTrace==null)actor.Body.RasterTrace=new BrushStats();
            if(_traceStrokes==null)_traceStrokes=new Dictionary<PaintActor,long[]>();
            if(_traceStrokes.ContainsKey(actor))return;
            _traceStrokes.Add(actor,new[]{actor.FaceBuffer?.Revision??0,actor.BodyBuffer?.Revision??0});
            TraceWrite("STROKE_BEGIN",()=>"actor="+actor.Actor.chaFile.parameter.fullname+" tool="+_tool+" contact="+_contact+" guide="+_sprayReady+" held="+_vrPaint.Held+" trigger="+_trigger);
        }
        void TraceStrokeEnd(PaintActor actor)
        {
            if(!TraceEnabled||_traceStrokes==null||!_traceStrokes.TryGetValue(actor,out var before))return;
            TraceWrite("STROKE_END",()=>"actor="+actor.Actor.chaFile.parameter.fullname+" faceWrites="+((actor.FaceBuffer?.Revision??0)-before[0])+
                " bodyWrites="+((actor.BodyBuffer?.Revision??0)-before[1])+" faceInkPixels="+(actor.FaceBuffer?.InkPixels??0)+" bodyInkPixels="+(actor.BodyBuffer?.InkPixels??0)+
                " faceBindings="+(actor.Face?.AssignedBindings??0)+" bodyBindings="+(actor.Body?.AssignedBindings??0));
            if(actor.Face?.RasterTrace!=null)TraceWrite("RASTER_FACE",()=>actor.Face.RasterTrace.ToString());
            if(actor.Body?.RasterTrace!=null)TraceWrite("RASTER_BODY",()=>actor.Body.RasterTrace.ToString());
            if(actor.Face!=null)actor.Face.RasterTrace=null;if(actor.Body!=null)actor.Body.RasterTrace=null;
            _traceStrokes.Remove(actor);
        }
    }
}
