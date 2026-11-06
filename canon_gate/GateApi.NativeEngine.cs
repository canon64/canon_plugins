using System;
using UnityEngine;

namespace CanonGate
{
    public static partial class GateApi
    {
        private static DedicatedNativeEngineLease _nativeEngine;

        // This is an engine ownership declaration, not an H permission grant.
        // The requester keeps only its already-decided AI activity permission.
        public static DedicatedNativeEngineLease AcquireDedicatedNativeEngine(string requesterGuid,
            MonoBehaviour owner, MonoBehaviour actor, ulong activityGeneration)
        {
            if (_nativeEngine != null) throw new InvalidOperationException("A dedicated native engine is already owned.");
            if (!_running || _ending || Current != null || _aiMode == null || _aiMode.Suspended
                || !ReferenceEquals(_aiMode.Owner, owner) || !ReferenceEquals(_aiMode.Actor, actor)
                || !Activities.TryGetValue(requesterGuid ?? string.Empty, out var registration)
                || registration.Kind != ActivityKind.AiShoujoMode || !registration.IsActive
                || registration.Id != activityGeneration || !ReferenceEquals(registration.Scene, owner))
                throw new InvalidOperationException("Native engine ownership requires the exact authorized AI owner, actor and activity generation.");
            return _nativeEngine = new DedicatedNativeEngineLease(registration, _aiMode, owner, actor);
        }

        // Exact identity remains recognizable during cleanup after permission
        // revocation; cleanup must not accidentally emit a normal H end event.
        public static bool IsDedicatedNativeEngine(MonoBehaviour scene) => scene != null
            && _nativeEngine != null && ReferenceEquals(_nativeEngine.Scene, scene);

        public sealed class DedicatedNativeEngineLease : IDisposable
        {
            readonly ActivityRegistration registration;
            readonly ActivityPermit permit;
            readonly AiShoujoModeScope scope;
            readonly MonoBehaviour owner, actor;
            bool disposed;
            public MonoBehaviour Scene { get; private set; }
            public bool IsValid => !disposed && ReferenceEquals(_nativeEngine, this) && permit.IsValid
                && ReferenceEquals(_aiMode, scope) && !scope.Suspended
                && ReferenceEquals(scope.Owner, owner) && ReferenceEquals(scope.Actor, actor)
                && ReferenceEquals(registration.Scene, owner) && Current == null;
            internal DedicatedNativeEngineLease(ActivityRegistration registration, AiShoujoModeScope scope,
                MonoBehaviour owner, MonoBehaviour actor)
            { this.registration = registration; permit = registration.Capture(); this.scope = scope; this.owner = owner; this.actor = actor; }
            public void Bind(HSceneProc scene)
            {
                if (!IsValid || scene == null || Scene != null) throw new InvalidOperationException("Native engine binding is stale, missing, or already assigned.");
                Scene = scene;
            }
            public void BindCleanupScene(HSceneProc scene)
            {
                if (disposed || !ReferenceEquals(_nativeEngine, this) || scene == null || Scene != null)
                    throw new InvalidOperationException("Native cleanup identity is missing or already assigned.");
                // This never makes IsValid true: late native scene completion
                // after cancellation can be torn down without ordinary H events.
                Scene = scene;
            }
            public void Dispose()
            {
                if (disposed) return; disposed = true; Scene = null;
                if (ReferenceEquals(_nativeEngine, this)) _nativeEngine = null;
            }
        }
    }
}
