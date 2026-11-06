using System;
using System.Collections.Generic;
using UnityEngine;
using VRGIN.Core;
namespace MainGameMagicPaint
{
    public sealed partial class Plugin
    {
        readonly List<MeshProbe> _skinCandidates = new List<MeshProbe>();
        readonly VrPaintState _vrPaint = new VrPaintState();
        readonly ContactLatch _nibContact = new ContactLatch(), _rubberContact = new ContactLatch();
        bool _sprayReady;
        int _markerEnd;
        SurfaceAnchor _previousAnchor;
        Func<Ray,Vector3?> _projectSprayGuide;
        void StopPaintPress() { _trigger = false; _vrPaint.StopPress(); }
        void ResetContacts()
        {
            _contact = _sprayReady = false; _markerEnd = 0;
            _nibContact.Set(false); _rubberContact.Set(false);
            _skinCandidates.Clear();
            _hud?.Guide(false, Vector3.zero, Vector3.up, 0, Color.white);
        }
        void PrepareCandidates(Bounds volume)
        {
            _skinCandidates.Clear();
            foreach(var actor in _actors)
            {
                if(actor.Actor == null) continue;
                foreach(var probe in actor.Probes)
                {
                    var renderer = probe.Renderer;
                    if(!actor.IsSkin(probe) || !MeshProbe.Participates(renderer) || !volume.Intersects(probe.QueryBounds)) continue;
                    _skinCandidates.Add(probe);
                }
            }
        }
        void PrepareRayCandidates(Ray ray,float distance,float spread)
        {
            var volume = new Bounds(ray.origin,Vector3.one * .002f);volume.Encapsulate(ray.GetPoint(distance));volume.Expand(spread*2+.002f);PrepareCandidates(volume);
        }
        bool FindRay(Ray ray,float limit,out SurfaceHit hit,bool bind=true,bool reuseCandidates=false)
        {
            hit=default;
            if(!reuseCandidates)PrepareRayCandidates(ray,limit,0);
            float best=limit;bool found=false;
            foreach(var probe in _skinCandidates)
                if(probe.Ray(ray,best,out var candidate)){best=candidate.Distance;hit=candidate;found=true;}
            if(!found){_status="対象なし";return false;}
            var owner=hit.Probe.Owner;if(owner==null)return false;
            if(bind)hit.Surface=owner.Surface(hit.Probe);
            _target=_actors.IndexOf(owner);return true;
        }
        bool FindContact(Vector3 raw,ContactLatch latch,out SurfaceHit hit)
        {
            hit=default;float attach=_snap.Value*.001f,detach=Mathf.Max(_snap.Value,_detach.Value)*.001f;
            float best=latch.Limit(attach,detach);bool found=false;
            foreach(var probe in _skinCandidates)
                if(probe.Near(raw,best,out var candidate)){best=candidate.Distance;hit=candidate;found=true;}
            if(!found){latch.Set(false);TraceContact(latch==_nibContact?0:1,"NO_TRIANGLE_WITHIN_LIMIT",raw,best,hit);return false;}
            TraceContact(latch==_nibContact?0:1,"ATTACHED",raw,latch.Limit(attach,detach),hit);latch.Set(true);_target=_actors.IndexOf(hit.Probe.Owner);return true;
        }
        void TickTool()
        {
            if(VR.Active && (!_vrPaint.Held || _hand==null)){ResetContacts();EndStroke();return;}
            if(_stick.Menu){ResetContacts();return;}
            _model.Visual.localPosition=Vector3.zero;
            if(_tool==PaintTool.Marker)
            {
                _sprayReady=false;
                float margin=Mathf.Max(_snap.Value,_detach.Value)*.001f+.015f;
                var volume=new Bounds(_model.Tip.position,Vector3.zero);volume.Encapsulate(_model.Eraser.position);volume.Expand(margin*2+.002f);PrepareCandidates(volume);
                bool front=FindContact(_model.Tip.position,_nibContact,out var nib);
                bool back=FindContact(_model.Eraser.position,_rubberContact,out var rubber);
                bool erase=back&&(!front||rubber.Distance<nib.Distance);
                if(!front&&!back){_contact=false;_markerEnd=0;EndStroke();_status="肌に近づけてください";_hud?.Guide(false,Vector3.zero,Vector3.up,0,Color.white);return;}
                var contact=erase?rubber:nib;int end=erase?2:1;
                if(_markerEnd!=0&&_markerEnd!=end)EndStroke();_markerEnd=end;
                Vector3 raw=erase?_model.Eraser.position:_model.Tip.position;
                _model.Visual.position+=Vector3.ClampMagnitude(contact.Point-raw,Mathf.Max(_snap.Value,_detach.Value)*.001f);
                _contact=true;_target=_actors.IndexOf(contact.Probe.Owner);
                _status=erase?"後端の消しゴム：肌に接地":"ペン先：肌に接地";
                _hud?.Guide(false,Vector3.zero,Vector3.up,0,Color.white);
                if(_trigger && (!VR.Active || _vrPaint.CanPaint(_contact)))
                {contact.Surface=contact.Probe.Owner.Surface(contact.Probe);Marker(contact,erase);}
                else _previousSurface=null;
            }
            else SprayFrom(new Ray(_model.Tip.position,_model.Visual.forward),Time.unscaledDeltaTime,_trigger);
            if(Time.unscaledTime-_lastFlush>=1f/30f)
            {foreach(var actor in _strokeActors)actor.Flush();_lastFlush=Time.unscaledTime;}
        }
        void BeginStroke(SkinSurface surface)
        {
            var actor=surface?.Probe.Owner;if(actor==null)return;
            _strokeActor=actor;_strokeActors.Add(actor);_stroke=true;
            ObserveInspectionActor(actor); TraceStrokeBegin(actor);
        }
        uint PackedColor(){Color32 c=InkColor;return PaintBuffer.Pack(c.r,c.g,c.b,255);}
        void Marker(SurfaceHit hit,bool erase)
        {
            if(VR.Active && !_vrPaint.CanPaint(_contact))return;
            BeginStroke(hit.Surface);if(!_stroke)return;
            float radius=(erase?_eraserWidth.Value:_markerWidth.Value)*.0005f;
            float alpha=erase?.8f:1-_transparency.Value/100f;
            int count=1;
            if(_previousSurface==hit.Surface && _previousAnchor.Resolve(hit.Probe.NumericVertices,out var movedPrevious))
            {
                _previousPoint=MeshProbe.Vec(movedPrevious);
                float distance=Vector3.Distance(_previousPoint,hit.Point);
                if(distance<=.04f)count=Mathf.Clamp(Mathf.CeilToInt(distance/Mathf.Max(radius*.5f,.00025f)),1,100);
            }
            for(int i=1;i<=count;i++)
            {
                Vector3 point=count==1?hit.Point:Vector3.Lerp(_previousPoint,hit.Point,(float)i/count);
                hit.Surface.Stamp(point,hit.Normal,radius,PackedColor(),alpha,erase,.15f);
            }
            _previousPoint=hit.Point;_previousSurface=hit.Surface;
            _previousAnchor=SurfaceAnchor.Capture(hit.Probe.NumericVertices,hit.Probe.Triangles,hit.Triangle,MeshProbe.Num(hit.Point));
        }
        Vector3? ProjectSprayGuide(Ray ray)
        {
            if(!FindRay(ray,.30f,out var projected,false,true))return null;
            return projected.Point+projected.Normal*.0008f;
        }
        void SprayFrom(Ray axis,float delta,bool painting)
        {
            _sprayReady=false;_contact=false;_previousSurface=null;
            if(VR.Active && (!_vrPaint.Held || _hand==null)){_hud?.Guide(false,Vector3.zero,Vector3.up,0,InkColor);EndStroke();return;}
            PrepareRayCandidates(axis,2f,_sprayWidth.Value*.0015f);
            if(!FindRay(axis,2f,out var center,false,true)){TraceSpray("NO_VISIBLE_SKIN",default,0);EndStroke();_hud?.Guide(false,Vector3.zero,Vector3.up,0,InkColor);return;}
            if(center.Distance>.30f||center.Distance<.001f){TraceSpray("OUT_OF_RANGE",center,0);EndStroke();_status="噴射距離外";_hud?.Guide(false,Vector3.zero,Vector3.up,0,InkColor);return;}
            float radius=_sprayWidth.Value*.0005f*center.Distance/.1f;
            _status=(EraseSpray?"消去可能・":"噴射可能・")+(center.Distance*100).ToString("0")+"cm";
            string centerStatus=_status;int centerTarget=_target;
            if(_projectSprayGuide==null)_projectSprayGuide=ProjectSprayGuide;
            _sprayReady=_hud!=null&&_hud.ProjectGuide(axis,center,radius,InkColor,_projectSprayGuide);
            _status=centerStatus;_target=centerTarget;
            TraceSpray(_sprayReady?"GUIDE_READY":"GUIDE_NOT_VISIBLE",center,radius);
            if(!_sprayReady){EndStroke();return;}
            if(!painting || (VR.Active&&!_vrPaint.CanPaint(_sprayReady)))return;
            _sprayBudget+=_flow.Value*Mathf.Min(delta,.05f);int count=Mathf.Min(150,Mathf.FloorToInt(_sprayBudget));_sprayBudget-=count;
            Vector3 right=Vector3.Cross(axis.direction,Mathf.Abs(axis.direction.y)<.9f?Vector3.up:Vector3.right).normalized;
            Vector3 up=Vector3.Cross(right,axis.direction).normalized;
            uint color=PackedColor();
            for(int i=0;i<count;i++)
            {
                float angle=(float)_random.NextDouble()*Mathf.PI*2,q=Mathf.Sqrt((float)_random.NextDouble());
                float edge=_feather.Value<=0?1:1-Mathf.SmoothStep(0,1,Mathf.Clamp01((q-(1-_feather.Value/100))/(_feather.Value/100)));
                Vector3 aimed=center.Point+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*(radius*q);
                if(!FindRay(new Ray(axis.origin,aimed-axis.origin),.30f,out var hit,true,true))continue;
                float variation=_variation.Value/100f;
                float sizeFactor=1+((float)_random.NextDouble()*2-1)*variation*.8f;
                float alphaFactor=1+((float)_random.NextDouble()*2-1)*variation*.8f;
                float grain=_grain.Value*.0005f*Mathf.Max(.15f,hit.Distance/.1f)*sizeFactor;
                BeginStroke(hit.Surface);hit.Surface.Stamp(hit.Point,hit.Normal,grain,color,Mathf.Clamp01((1-_transparency.Value/100f)*.45f*alphaFactor*edge),EraseSpray,.6f);
            }
            _status=centerStatus;_target=centerTarget;
        }
        static readonly PaintSetting[] MarkerSettings = { PaintSetting.Color, PaintSetting.Width, PaintSetting.Transparency };
        static readonly PaintSetting[] SpraySettings = { PaintSetting.Color, PaintSetting.Width, PaintSetting.Grain, PaintSetting.Transparency, PaintSetting.Flow, PaintSetting.Feather, PaintSetting.Variation };
        bool _settingsVisible;
        void ToggleSettings()
        {
            _settingsVisible = !_settingsVisible; _stick.BlockUntilNeutral();
            if (!_settingsVisible) SaveConfig();
        }
        void SelectSetting(int direction)
        {
            var choices = _tool == PaintTool.Marker ? MarkerSettings : SpraySettings;
            _setting = choices[(Array.IndexOf(choices, _setting) + direction + choices.Length) % choices.Length];
        }
        void ChangeColor(int direction)
        {
            int count = _tool == PaintTool.Spray ? 11 : 10; _colorIndex = (_colorIndex + direction + count) % count;
            _preferredColor.Value = _colorIndex;
            _model?.SetColor(InkColor); _gaugeUntil = Time.unscaledTime + 2;
        }
        BepInEx.Configuration.ConfigEntry<float> SettingValue()
        {
            switch (_setting)
            {
                case PaintSetting.Width: return _tool == PaintTool.Marker ? _markerWidth : _sprayWidth;
                case PaintSetting.Grain: return _grain; case PaintSetting.Transparency: return _transparency;
                case PaintSetting.Flow: return _flow; case PaintSetting.Feather: return _feather; case PaintSetting.Variation: return _variation;
                default: return null;
            }
        }
        void AdjustSetting(float seconds)
        {
            var entry = SettingValue(); if (entry == null) return;
            float rate = _setting == PaintSetting.Width ? (_tool == PaintTool.Marker ? 5 : 30) : _setting == PaintSetting.Grain ? 1 : _setting == PaintSetting.Flow ? 500 : 25;
            entry.Value += seconds * rate; _gaugeUntil = Time.unscaledTime + 2;
        }
    }
}
