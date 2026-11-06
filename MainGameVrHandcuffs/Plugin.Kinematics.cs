using System;
using UnityEngine;
using Hip=MainGirlHipHijack.Plugin;

namespace MainGameVrHandcuffs
{
    internal static class HandcuffDistance
    {
        internal const float Reach=.05f;
        internal static Vector3 Correction(Vector3 heldEye,Vector3 freeEye)
        {
            var delta=freeEye-heldEye;float distance=delta.magnitude;
            return distance>Reach ? heldEye+delta*(Reach/distance)-freeEye : Vector3.zero;
        }
        internal static Vector3 Bend(Vector3 a,Vector3 b,Vector3 bend)
        {
            var delta=b-a;float span=delta.magnitude;
            var direction=span>.000001f?delta/span:Vector3.right;
            bend=Vector3.ProjectOnPlane(bend,direction);
            if (bend.sqrMagnitude<.000001f) bend=Vector3.Cross(direction,Vector3.up);
            if (bend.sqrMagnitude<.000001f) bend=Vector3.Cross(direction,Vector3.forward);
            bend.Normalize();
            return bend;
        }
        internal static Vector3 ChainPoint(Vector3 a,Vector3 b,Vector3 bend,float distance)
        {
            float span=(b-a).magnitude;
            bend=Bend(a,b,bend);
            float height=.5f*Mathf.Sqrt(Mathf.Max(0,Reach*Reach-span*span));
            var midpoint=(a+b)*.5f+bend*height;
            return distance<=Reach*.5f ? Vector3.Lerp(a,midpoint,distance/(Reach*.5f))
                : Vector3.Lerp(midpoint,b,(distance-Reach*.5f)/(Reach*.5f));
        }
    }

    public sealed partial class Plugin
    {
        private Vector3 _chainBend=Vector3.forward;
        private bool _constraintConnected;
        private void ConnectHeldConstraint()
        {
            if (_constraintConnected) return;
            Hip.RuntimeItemConstraintsEvaluating+=EvaluateHeldConstraint;
            _constraintConnected=true;
            EvaluateHeldConstraint();
        }
        private void DisconnectHeldConstraint()
        {
            if (!_constraintConnected) return;
            _constraintConnected=false;
            Hip.RuntimeItemConstraintsEvaluating-=EvaluateHeldConstraint;
        }
        private void EvaluateHeldConstraint()
        {
            int held=_state.Held;
            if (!CanAct || held<0 || _parts[held]==null || _chainEyes[0]==null || _chainEyes[1]==null) return;
            int free=1-held;
            _parts[free].position+=HandcuffDistance.Correction(_chainEyes[held].position,_chainEyes[free].position);
            var a=_chainEyes[0].position;var b=_chainEyes[1].position;
            // Direct geometry only: no speed, spring, damping, gravity, or accumulated time.
            _chainBend=HandcuffDistance.Bend(a,b,_chainBend);
            for (int i=0;i<3;i++)
            {
                float along=.012f+i*.013f;
                var p=HandcuffDistance.ChainPoint(a,b,_chainBend,along);
                var before=HandcuffDistance.ChainPoint(a,b,_chainBend,along-.001f);
                var after=HandcuffDistance.ChainPoint(a,b,_chainBend,along+.001f);
                var tangent=after-before;
                _parts[i+2].position=p;
                if (tangent.sqrMagnitude>.000001f)
                {
                    // Millimetre sample differences must become unit axes before
                    // crossing: otherwise LookRotation receives a near-zero forward
                    // vector and Unity falls back to identity, separating the links.
                    tangent.Normalize();
                    var normal=Vector3.Cross(_chainBend,tangent);
                    if (normal.sqrMagnitude>.000001f)
                    {
                        normal.Normalize();
                        _parts[i+2].rotation=Quaternion.LookRotation(Vector3.Cross(tangent,normal).normalized,normal);
                    }
                }
            }
        }
        private void FreezeParts()
        {
            Exception failure=null;
            for (int i=0;i<_parts.Length;i++)
            {
                var part=_parts[i];if (part==null) continue;
                try { if (_rig!=null) part.SetParent(_rig.transform,true); }
                catch (Exception ex) { if (failure==null) failure=ex; }
            }
            if (failure!=null) throw failure;
        }
    }
}
