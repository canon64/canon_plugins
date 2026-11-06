using System;
using UnityEngine;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksAdvNativeActorBackend : KksAdvActorHost.IBackend
    {
        private readonly KksAdvActorRegistry _actors;
        private readonly Func<string, Transform> _findTarget;
        internal KksAdvNativeActorBackend(KksAdvActorRegistry actors, Func<string, Transform> findTarget)
        { _actors = actors ?? throw new ArgumentNullException(nameof(actors)); _findTarget = findTarget ?? throw new ArgumentNullException(nameof(findTarget)); }
        public void LookEyes(int actor, int pattern) => _actors.Resolve(actor).Data.chaCtrl.ChangeLookEyesPtn(pattern);
        public void LookNeck(int actor, int pattern, float rate) => _actors.Resolve(actor).Data.chaCtrl.ChangeLookNeckPtn(pattern, rate);
        public void LookEyesTarget(KksAdvActorHost.LookTarget target) => _actors.Resolve(target.Actor).Data.chaCtrl.ChangeLookEyesTarget(target.Type,
            string.IsNullOrEmpty(target.Name) ? null : _findTarget(target.Name), target.Rate, target.Rotation, target.Range, target.Distance);
        public void LookNeckTarget(KksAdvActorHost.LookTarget target) => _actors.Resolve(target.Actor).Data.chaCtrl.ChangeLookNeckTarget(target.Type,
            string.IsNullOrEmpty(target.Name) ? null : _findTarget(target.Name), target.Rate, target.Rotation, target.Range, target.Distance);
        public void DefaultMotion(int actor) => _actors.Resolve(actor).Presentation.ApplyNativeDefaultMotion();
    }
}
