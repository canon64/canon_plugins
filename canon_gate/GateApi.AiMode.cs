using System;
using System.Collections.Generic;
using UnityEngine;

namespace CanonGate
{
    public static partial class GateApi
    {
        private static AiShoujoModeScope _aiMode;
        public static AiShoujoModeScope AiShoujoMode => _aiMode;

        // The owning mode publishes lifecycle and actor changes; clients never search for them.
        public static AiShoujoModeScope EnterAiShoujoMode(MonoBehaviour owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (_aiMode != null) throw new InvalidOperationException("AI少女モードの所有者は既に登録されています。");
            _aiMode = new AiShoujoModeScope(owner);
            _aiMode.Resume();
            return _aiMode;
        }

        public sealed class AiShoujoModeScope : IDisposable
        {
            public MonoBehaviour Owner { get; }
            public MonoBehaviour Actor { get; private set; }
            public Func<Action> PauseActorMotion { get; set; }
            public Action AcquireActorMotion(MonoBehaviour actor)
            {
                if (_disposed || Suspended || !ReferenceEquals(Actor, actor)) return null;
                return PauseActorMotion?.Invoke();
            }
            public Action SuspendActorActivities(string requesterGuid, MonoBehaviour actor, params string[] serviceGuids)
            {
                if (_disposed || Suspended || !ReferenceEquals(actor, Actor) || Current != null
                    || !Activities.TryGetValue(requesterGuid, out var requester) || !requester.IsActive || requester.Kind != ActivityKind.AiShoujoMode || !ReferenceEquals(requester.Scene, Owner))
                    throw new InvalidOperationException("AI actor service lease requires the exact active owner, actor, and authorized requester.");
                var resume = new List<Action>();
                var requested = new HashSet<string>(serviceGuids ?? Array.Empty<string>(), StringComparer.Ordinal);
                if (requested.Contains(requesterGuid)) throw new InvalidOperationException("An AI actor service cannot suspend its requester.");
                try
                {
                    foreach (string guid in requested)
                    {
                        if (!Activities.TryGetValue(guid, out var service)) continue;
                        var release = service.PauseForActorLease(Owner); if (release != null) resume.Add(release);
                    }
                }
                catch { foreach (var release in resume) try { release(); } catch (Exception ex) { Report(ex); } throw; }
                bool restored = false;
                return () =>
                {
                    if (restored) return; restored = true;
                    if (_disposed || Suspended || !ReferenceEquals(_aiMode, this) || !ReferenceEquals(Actor, actor) || Current != null) return;
                    foreach (var release in resume) try { release(); } catch (Exception ex) { Report(ex); }
                };
            }
            internal bool Suspended { get; private set; } = true;
            private bool _disposed;
            internal AiShoujoModeScope(MonoBehaviour owner) { Owner = owner; }
            public void SetActor(MonoBehaviour actor)
            {
                if (_disposed || ReferenceEquals(Actor, actor)) return;
                bool suspended = Suspended;
                Suspend(); Actor = actor;
                if (!suspended) Resume();
            }
            public void Suspend()
            {
                if (_disposed || Suspended) return;
                Suspended = true;
                foreach (var activity in new List<ActivityRegistration>(Activities.Values))
                    if (activity.Kind == ActivityKind.AiShoujoMode) activity.Revoke();
                foreach (var activity in new List<ActivityRegistration>(Activities.Values))
                    if (activity.Kind == ActivityKind.AiShoujoMode) activity.End();
            }
            public void Resume()
            {
                if (_disposed || !Suspended || Current != null) return;
                Suspended = false;
                foreach (var activity in new List<ActivityRegistration>(Activities.Values))
                {
                    if (activity.Kind != ActivityKind.AiShoujoMode) continue;
                    try { activity.Begin(Owner); }
                    catch (Exception ex) { Report(ex); }
                }
            }
            public void Dispose()
            {
                if (_disposed) return;
                Suspend(); _disposed = true; Actor = null; PauseActorMotion = null;
                if (Current?.IsAiShoujoMode == true) EndSession(SessionKind.H);
                if (ReferenceEquals(_aiMode, this)) _aiMode = null;
            }
        }
    }
}
