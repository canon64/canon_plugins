using System;
using System.Collections;
using UnityEngine;

namespace MainGameAiShoujoMode.Conversation
{
    // One engine wait per deadline, not a coroutine that resumes each frame to inspect state.
    internal sealed class KksUnityDeadlineScheduler
    {
        private sealed class Lease : IDisposable
        {
            internal MonoBehaviour Host;
            internal Coroutine Routine;
            internal bool Ended;
            public void Dispose()
            {
                if (Ended) return; Ended = true;
                var routine = Routine; Routine = null;
                if (routine != null && Host != null) Host.StopCoroutine(routine);
            }
        }
        private readonly MonoBehaviour _host;
        internal KksUnityDeadlineScheduler(MonoBehaviour host) { _host = host ?? throw new ArgumentNullException(nameof(host)); }
        internal double Now => Time.time;
        internal IDisposable Schedule(double seconds, Action completed)
        {
            var duration = (float)seconds;
            if (float.IsNaN(duration) || float.IsInfinity(duration)) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (_host == null) throw new InvalidOperationException("KKS ADV deadline owner disappeared.");
            if (!_host.gameObject.activeInHierarchy) throw new InvalidOperationException("KKS ADV deadline owner is inactive.");
            var lease = new Lease { Host = _host };
            var routine = _host.StartCoroutine(Wait(lease, duration, completed));
            if (routine == null && !lease.Ended) throw new InvalidOperationException("KKS ADV deadline could not be scheduled.");
            if (lease.Ended) { if (routine != null && _host != null) _host.StopCoroutine(routine); }
            else lease.Routine = routine;
            return lease;
        }
        private static IEnumerator Wait(Lease lease, float seconds, Action completed)
        {
            yield return new WaitForSeconds(seconds);
            if (lease.Ended) yield break;
            lease.Routine = null; lease.Ended = true;
            completed?.Invoke();
        }
    }
}
