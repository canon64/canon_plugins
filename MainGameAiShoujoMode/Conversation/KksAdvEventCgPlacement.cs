using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksAdvEventCgPlacement
    {
        internal sealed class Lease : IDisposable
        {
            internal Transform Anchor { get; private set; }
            internal Lease(Transform anchor) { Anchor = anchor; }
            public void Dispose()
            {
                var anchor = Anchor; Anchor = null;
                if (anchor != null) { anchor.gameObject.SetActive(false); UnityEngine.Object.Destroy(anchor.gameObject); }
            }
        }
        private readonly KksAdvActorRegistry _actors;
        private readonly KksConversationRuntimeSettings _settings;
        private readonly HashSet<string> _relative;
        internal KksAdvEventCgPlacement(KksAdvActorRegistry actors, KksConversationRuntimeSettings settings)
        {
            _actors = actors ?? throw new ArgumentNullException(nameof(actors)); _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _relative = new HashSet<string>(settings.advEventCgActorRelativeAssets ?? throw new ArgumentException("EventCG placement definitions are missing."), StringComparer.OrdinalIgnoreCase);
        }
        internal Lease Place(string bundle, string asset, GameObject prefab)
        {
            if (!_relative.Contains(bundle + ":" + asset)) throw new NotSupportedException("EventCG needs an explicit island placement: " + bundle + "/" + asset);
            var data = prefab.GetComponent<ADV.EventCG.Data>();
            if (data == null || data.mapName != _settings.advEventCgActorRelativeMap || prefab.transform.localScale != Vector3.one
                || prefab.transform.localPosition != Vector3.zero || prefab.transform.localRotation != Quaternion.identity)
                throw new NotSupportedException("EventCG reference frame differs from the audited island placement: " + asset);
            var points = prefab.transform.Cast<Transform>().ToArray();
            var references = points.Where(ADV.EventCG.Data.IsCharaPos).ToArray();
            if (references.Length != _settings.one || points.Length != _settings.one + _settings.one
                || points[_settings.zero].name != ADV.EventCG.Data.ParentNameCamera || !references.All(point => point.localScale == Vector3.one)
                || data.scenes.Any(scene => scene.charas.Any(chara => chara.no != _settings.zero || chara.motionAndItem.items.Any() || chara.motionAndItem.removes.Any())))
                throw new NotSupportedException("EventCG requires world geometry or additional participant placement: " + asset);
            if (!_actors.RegisteredTargets().TryGetValue(_settings.zero, out var heroine) || !(heroine.Data is SaveData.Heroine))
                throw new InvalidOperationException("EventCG has no registered heroine reference slot.");
            var target = heroine.Data.transform != null ? heroine.Data.transform : heroine.Data.chaCtrl.transform;
            var reference = references[_settings.zero];
            var sourcePosition = reference.position; var sourceForward = reference.forward;
            var targetPosition = target.position; var targetForward = target.forward;
            var frame = KksEventCgReferenceFrame.Align(new KksEventCgReferenceFrame.Pose
            { X = sourcePosition.x, Y = sourcePosition.y, Z = sourcePosition.z, ForwardX = sourceForward.x, ForwardZ = sourceForward.z },
                new KksEventCgReferenceFrame.Pose
                { X = targetPosition.x, Y = targetPosition.y, Z = targetPosition.z, ForwardX = targetForward.x, ForwardZ = targetForward.z });
            var root = new GameObject("AI mode EventCG placement " + asset);
            try
            {
                // World unit scale avoids inheriting the imported map's scale or skew.
                root.transform.SetPositionAndRotation(new Vector3((float)frame.Origin.X, (float)frame.Origin.Y, (float)frame.Origin.Z),
                    Quaternion.LookRotation(new Vector3((float)frame.Sin, _settings.zero, (float)frame.Cos), Vector3.up));
                return new Lease(root.transform);
            }
            catch { UnityEngine.Object.Destroy(root); throw; }
        }
    }
}
