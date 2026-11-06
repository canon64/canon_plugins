using UnityEngine;

namespace MainGameVrDildo
{
    public sealed partial class Plugin
    {
        private Transform _composerParent;

        // Unity main thread. Detach remains available after gate invalidation for cleanup.
        public static bool TrySetComposerParent(Transform parent, bool moveToParent)
        {
            Plugin p = Instance;
            if (p == null || p._model == null) return false;
            if (parent != null && (!CanonPlugins.Shared.PluginSessionGate.Allowed ||
                p._settings == null || !p._settings.Enabled)) return false;
            Transform model = p._model.transform;
            if (parent == model || (parent != null && parent.IsChildOf(model))) return false;
            p.ReleaseGrab();
            model.SetParent(parent, true);
            if (parent != null && moveToParent) model.localPosition = Vector3.zero;
            p._composerParent = parent;
            p.RebuildPistonClip();
            p.ResetAibuMotion();
            return true;
        }

        public static bool TryGetComposerAttachment(out Transform model, out Transform parent)
        {
            Plugin p = Instance;
            model = p != null && p._model != null ? p._model.transform : null;
            parent = p != null ? p._composerParent : null;
            return model != null && parent != null && model.parent == parent;
        }

        private bool IsComposerDriven => _model != null && _composerParent != null &&
            _model.transform.parent == _composerParent;
    }
}
