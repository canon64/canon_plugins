using System;
using System.Collections.Generic;
using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;

namespace MainGameDollMode
{
    public partial class Plugin
    {
        /// <summary>Raised when a previously queried doll ceases to be a doll. Cleanup is legal after gate revocation.</summary>
        public static event Action<ChaControl> DollModeReleased;
        private readonly HashSet<ChaControl> _externalDolls = new HashSet<ChaControl>();

        public static bool IsDollModeActive(ChaControl actor)
        {
            var p = Instance;
            if (p == null || !p.IsActiveDoll(actor)) return false;
            p._externalDolls.Add(actor);
            return true;
        }

        private bool IsActiveDoll(ChaControl actor)
        {
            if (!IsDollModeEnabled() || !_dollModeApplied || actor == null || !IsTargetCharacter(actor)) return false;
            bool participant = false;
            foreach (var candidate in SessionGate.Actors) participant |= ReferenceEquals(candidate, actor);
            return participant && _originalHideHighlightByCha.ContainsKey(actor.GetInstanceID()) && SavedActorMatches(actor);
        }

        private void ReleaseExternalDolls(bool all)
        {
            foreach (var actor in new List<ChaControl>(_externalDolls))
            {
                if (!all && IsActiveDoll(actor)) continue;
                _externalDolls.Remove(actor);
                var handlers = DollModeReleased;
                if (handlers == null) continue;
                foreach (Action<ChaControl> handler in handlers.GetInvocationList())
                    try { handler(actor); } catch (Exception ex) { LogWarn("DollModeReleased: " + ex.Message); }
            }
        }
    }
}
