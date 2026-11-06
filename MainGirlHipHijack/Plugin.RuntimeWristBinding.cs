using System;
using MainGirlHipHijack.Runtime;
using RootMotion.FinalIK;
using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;

namespace MainGirlHipHijack
{
    public sealed partial class Plugin
    {
        private readonly RuntimeWristBinding[] _runtimeWrists = new RuntimeWristBinding[2];
        private bool _runtimeWristSolverWasEnabled;
        private event Action _runtimeItemConstraintsEvaluating;
        public static event Action RuntimeItemConstraintsEvaluating
        {
            add { if (Instance!=null) Instance._runtimeItemConstraintsEvaluating+=value; }
            remove { if (Instance!=null) Instance._runtimeItemConstraintsEvaluating-=value; }
        }

        /// <summary>A session-only, exclusive wrist target. Does not save configuration.</summary>
        public sealed class RuntimeWristBinding : IDisposable
        {
            private Plugin _plugin;
            internal readonly int Index;
            internal readonly BodyIkOperationPointState Previous;
            internal readonly bool Wanted;
            internal readonly float Weight;
            internal readonly IKEffector Effector;
            internal readonly Transform Target;
            internal readonly Vector3 Position;
            internal readonly Quaternion Rotation;
            internal readonly float PositionWeight, RotationWeight;
            public string Owner { get; }
            public bool IsValid => _plugin != null;
            public event Action Invalidated;
            internal RuntimeWristBinding(Plugin plugin, int index, string owner, IKEffector effector)
            {
                _plugin=plugin; Index=index; Owner=owner; Effector=effector;
                Previous=plugin._bikEff[index]; Wanted=plugin._bikWant[index]; Weight=plugin._bikWeight[index];
                Target=effector.target; Position=effector.position; Rotation=effector.rotation;
                PositionWeight=effector.positionWeight; RotationWeight=effector.rotationWeight;
            }
            public void Dispose() { var p=_plugin; if (p!=null) p.ReleaseRuntimeWrist(this, false); }
            /// <summary>Aim the elbow using this exclusively owned wrist as the pivot.</summary>
            public bool TryBeginElbowAim(bool rightController)
            {
                var p=_plugin;
                return p!=null && p.TryBeginRuntimeWristElbowAim(this,rightController?1:0);
            }
            public void EndElbowAim() { _plugin?.EndRuntimeWristElbowAim(this); }
            internal void MarkReleased(bool notify)
            {
                _plugin=null;
                var callback=Invalidated; Invalidated=null;
                if (notify) callback?.Invoke();
            }
        }

        public static bool TryGetRuntimeWrist(string key, out ChaControl female, out Transform wrist)
        {
            female=null; wrist=null;
            var p=Instance;
            int index=IndexOfEffectorKey(key);
            if (!SessionGate.Allowed || p==null || !p.IsPluginEnabled() || index<0 || index>1) return false;
            var eff=p.BIK_GetEffector(index);
            if (eff==null || eff.bone==null || p._femaleSession.Character==null) return false;
            female=p._femaleSession.Character; wrist=eff.bone;
            return true;
        }

        public static bool TryGetRuntimeForearm(string key, out Transform forearm)
        {
            forearm=null;
            if (!TryGetRuntimeWrist(key,out _,out var wrist)) return false;
            var refs=Instance._femaleSession.FullBodyIk.references;
            if (refs!=null) forearm=IndexOfEffectorKey(key)==0?refs.leftForearm:refs.rightForearm;
            if (forearm==null) forearm=wrist.parent;
            return forearm!=null;
        }

        public static bool TryBindRuntimeWrist(string owner, ChaControl female, string key,
            Transform target, out RuntimeWristBinding binding)
        {
            binding=null;
            var p=Instance; int index=IndexOfEffectorKey(key);
            if (!SessionGate.Allowed || p==null || !p.IsPluginEnabled() || string.IsNullOrEmpty(owner)
                || target==null || index<0 || index>1 || female==null || female!=p._femaleSession.Character
                || p._runtimeWrists[index]!=null || p._pendingAbandonByPostureChange || p._poseTransitionCoroutine!=null) return false;
            var eff=p.BIK_GetEffector(index);
            if (eff==null || eff.bone==null || p._bikEff[index].GizmoDragging) return false;
            var lease=new RuntimeWristBinding(p,index,owner,eff);
            try
            {
                if (p._runtimeWrists[0]==null && p._runtimeWrists[1]==null)
                    p._runtimeWristSolverWasEnabled=p._femaleSession.FullBodyIk.enabled;
                p._runtimeWrists[index]=lease;
                p._bikEff[index]=new BodyIkOperationPointState {
                    Running=true, Proxy=target, ExternalOwner=true,
                    HasOriginalEffectorTarget=true, OriginalEffectorTarget=eff.target
                };
                p._bikWant[index]=true; p._bikWeight[index]=1f;
                // Trigger attachment is an explicit new IK operation after a posture reset.
                p._abandonedByPostureChange=false;
                eff.target=target; eff.positionWeight=1f; eff.rotationWeight=1f;
                p._femaleSession.FullBodyIk.enabled=true;
                lease.Previous.Gizmo?.SetVisible(false);
                p.NotifyArmIkRunningChanged(index,true);
                binding=lease;
                return true;
            }
            catch { p.ReleaseRuntimeWrist(lease,false); throw; }
        }

        private void ReleaseRuntimeWrist(RuntimeWristBinding lease, bool notify)
        {
            int index=lease.Index;
            if (!ReferenceEquals(_runtimeWrists[index],lease)) return;
            Exception elbowFailure=null;
            try { EndRuntimeWristElbowAim(lease); } catch(Exception ex) { elbowFailure=ex; }
            _runtimeWrists[index]=null;
            _bikEff[index]=lease.Previous; _bikWant[index]=lease.Wanted; _bikWeight[index]=lease.Weight;
            var eff=lease.Effector;
            eff.target=lease.Target; eff.position=lease.Position; eff.rotation=lease.Rotation;
            eff.positionWeight=lease.PositionWeight; eff.rotationWeight=lease.RotationWeight;
            if (_runtimeWrists[0]==null && _runtimeWrists[1]==null && !_runtimeWristSolverWasEnabled
                && _femaleSession.FullBodyIk!=null)
            {
                bool running=false;
                foreach (var state in _bikEff) if (state.Running) { running=true; break; }
                if (!running) _femaleSession.FullBodyIk.enabled=false;
            }
            try
            {
                lease.Previous.Gizmo?.SetVisible(IsGizmoVisible(index));
                NotifyArmIkRunningChanged(index,lease.Previous.Running);
            }
            finally { lease.MarkReleased(notify); }
            if (elbowFailure!=null) throw elbowFailure;
        }

        private void ReleaseRuntimeWrists()
        {
            Exception failure=null;
            for (int i=0;i<2;i++)
            {
                try { if (_runtimeWrists[i]!=null) ReleaseRuntimeWrist(_runtimeWrists[i],true); }
                catch (Exception ex) { if (failure==null) failure=ex; }
            }
            if (failure!=null) throw failure;
        }
    }
}
