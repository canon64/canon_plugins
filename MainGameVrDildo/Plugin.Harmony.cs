using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace MainGameVrDildo
{
    public sealed partial class Plugin
    {
        private readonly Dictionary<Harmony, HashSet<MethodBase>> _ownedHarmonyTargets =
            new Dictionary<Harmony, HashSet<MethodBase>>();

        private void PatchOwned(Harmony harmony, MethodBase target, HarmonyMethod prefix = null,
            HarmonyMethod postfix = null, HarmonyMethod finalizer = null)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (!_ownedHarmonyTargets.TryGetValue(harmony, out var targets))
                _ownedHarmonyTargets.Add(harmony, targets = new HashSet<MethodBase>());
            // Track before Patch so a partially failed installation can still be removed.
            targets.Add(target);
            harmony.Patch(target, prefix: prefix, postfix: postfix, finalizer: finalizer);
        }

        private void RemoveOwnedHooks(ref Harmony harmony)
        {
            var owner = harmony;
            harmony = null;
            if (owner == null || !_ownedHarmonyTargets.TryGetValue(owner, out var targets)) return;
            _ownedHarmonyTargets.Remove(owner);
            foreach (var target in targets)
            {
                try
                {
                    // UnpatchSelf scans every patched method, including unrelated VR patches
                    // whose bodies may reference types absent from this game assembly.
                    owner.Unpatch(target, HarmonyPatchType.All, owner.Id);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning("Hook removal failed (" + owner.Id + ", " +
                        target.Name + "): " + ex);
                }
            }
        }
    }
}
