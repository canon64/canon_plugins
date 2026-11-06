using UnityEngine;

namespace MainGameVrNeckChain
{
    public sealed partial class Plugin
    {
        private ChainVisualDriver _chainVisualDriver;
        private bool _chainVisualPending;
        private int _lastChainVisualFrame = -1;
        private System.Action<bool, bool> _chainCollisionResolver;

        // Runs after HScene, FBBIK, the shoulder hold and the camera-look update.
        // Neither normal LateUpdate nor the post-IK notification integrates rope.
        internal void UpdateFinalChainVisual()
        {
            if (!isActiveAndEnabled || !_chainVisualPending || _chain == null || _collar == null ||
                _scene == null || !_scene.isActiveAndEnabled || _female == null ||
                _neck == null || _collarBone == null || _lastChainVisualFrame == Time.frameCount) return;
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed) return;
            _chainVisualPending = false;
            _lastChainVisualFrame = Time.frameCount;
            UpdateCollarPlacement();
            UpdateBodyColliders();
            UpdateColliderPreviews();
            if (_customSimulation)
            {
                if (_dynamicBone != null) _dynamicBone.enabled = false;
                SolveChain();
            }
            else if (_rootAttached)
            {
                _chain.transform.position = _collar.transform.TransformPoint(_attachedRootLocal) -
                    _chain.transform.TransformVector(_chainRootLocalOffset);
            }
        }
    }

    [DefaultExecutionOrder(32000)]
    public sealed class ChainVisualDriver : MonoBehaviour
    {
        internal Plugin Owner;
        private void LateUpdate()
        {
            if (Owner != null) Owner.UpdateFinalChainVisual();
        }
    }
}
