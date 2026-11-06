using System.Collections;
using FreeH;
using SaveData;
using Object = UnityEngine.Object;
using GameScene = Manager.Scene;

namespace MainGameAiShoujoMode
{
    public sealed partial class AiModeHSession
    {
        private static void AfterOuterStart(ref IEnumerator __result)
        {
            var owner = _active;
            if (owner == null || !owner.Busy) return;
            var heroine = owner._heroine;
            var player = owner._player;
            // HScene creates its return data after requesting scene unload. Keep this
            // cleanup attached to that native enumerator even if Finish runs first.
            __result = new SessionCompletionEnumerator(__result, () => RemoveOwnedReturnData(heroine, player));
        }

        private static void RemoveOwnedReturnData(Heroine heroine, Player player)
        {
            if (GameScene.commonSpace == null || heroine == null) return;
            foreach (var back in GameScene.commonSpace.GetComponents<FreeHBackData>())
                if (ReferenceEquals(back.heroine, heroine) && ReferenceEquals(back.player, player))
                    // This is a data-only component; deferred destruction would leave
                    // it readable by a new FreeHScene during this same frame.
                    Object.DestroyImmediate(back);
        }
    }
}
