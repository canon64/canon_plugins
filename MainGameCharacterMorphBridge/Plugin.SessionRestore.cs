using System;
using System.Collections.Generic;
using SessionGate = CanonPlugins.Local.SessionGate;
namespace MainGameCharacterMorphBridge
{
    public sealed partial class Plugin
    {
        private sealed class SessionMorphEntry
        {
            internal ChaControl Actor;
            internal MorphSnapshot Shape;
            internal ColorSnapshot Colors;
            internal MakeupSnapshot Makeup;
            internal HairSnapshot Hair;
            internal object Overlay;
        }
        private readonly List<SessionMorphEntry> _sessionMorphs=new List<SessionMorphEntry>();
        private bool _restoringSession;
        private void CaptureSessionMorph()
        {
            _sessionMorphs.Clear();
            ResolveOverlayApi(); ResolveGlowApi();
            foreach(var actor in SessionGate.Actors)
            {
                if(actor==null || actor.sex!=1 || actor.chaFile==null)continue;
                _sessionMorphs.Add(new SessionMorphEntry{
                    Actor=actor,Shape=SnapshotFrom(actor),Colors=ColorSnapshotFrom(actor.chaFile),
                    Makeup=MakeupSnapshotFrom(actor),
                    Hair=HairSnapshotFrom(actor.chaFile),Overlay=GetOverlayDataFromFile(actor.chaFile)});
            }
        }
        private void RestoreSessionMorph()
        {
            _suppressVisualRefreshReapply=true; _restoringSession=true;
            try {
                foreach(var saved in _sessionMorphs)
                {
                    var target=saved.Actor;if(target==null || saved.Shape==null)continue;
                    try {
                        ApplySnapshotBody(target,saved.Shape.Body); ApplySnapshotFace(target,saved.Shape.Face);
                        ApplyColorBlendBetween(target,saved.Colors,saved.Colors,saved.Makeup,saved.Makeup,0f,true);
                        if(saved.Hair!=null)ApplyHairIds(target,saved.Hair);
                        ApplyOverlayData(target,saved.Overlay);
                        target.UpdateShapeBodyValueFromCustomInfo();target.UpdateShapeFaceValueFromCustomInfo();
                    } catch(Exception ex){LogWarnAlways("session morph restore: "+ex.Message);}
                }
            } finally { _suppressVisualRefreshReapply=false;_restoringSession=false;_sessionMorphs.Clear(); }
        }
    }
}
