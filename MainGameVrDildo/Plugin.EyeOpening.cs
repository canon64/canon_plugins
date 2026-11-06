using UnityEngine;

namespace MainGameVrDildo
{
    public sealed partial class Plugin
    {
        private ChaControl _eyeOpeningFemale;
        private float _originalEyesOpenMax, _originalEyesFixedRate;

        private void ApplySelectedEyeOpening(ChaControl actor)
        {
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed || !_cloneReactionPermit.IsValid ||
                actor == null || actor.eyesCtrl == null || actor.fileStatus == null) return;
            if (!_settings.EyeOpeningOverride || _selectedEyelidSlot < 0)
            { ReleaseSelectedEyeOpening(); return; }
            if (_eyeOpeningFemale != actor)
            {
                ReleaseSelectedEyeOpening();
                _eyeOpeningFemale = actor;
                _originalEyesOpenMax = actor.fileStatus.eyesOpenMax;
                _originalEyesFixedRate = actor.eyesCtrl.FixedRate;
            }
            var openings = _settings.EyelidOpeningSlots;
            float amount = openings != null && _selectedEyelidSlot < openings.Length
                ? Mathf.Clamp01(openings[_selectedEyelidSlot]) : 1f;
            actor.fileStatus.eyesOpenMax = amount;
            actor.eyesCtrl.OpenMax = amount;
            actor.eyesCtrl.FixedRate = amount;
        }

        private void ReleaseSelectedEyeOpening()
        {
            var actor = _eyeOpeningFemale; _eyeOpeningFemale = null;
            if (actor == null || actor.eyesCtrl == null || actor.fileStatus == null) return;
            actor.fileStatus.eyesOpenMax = _originalEyesOpenMax;
            actor.eyesCtrl.OpenMax = _originalEyesOpenMax;
            actor.eyesCtrl.FixedRate = _originalEyesFixedRate;
        }

        private static void SelectedEyeOpeningChanged(ChaControl __instance)
        {
            var p = Instance;
            if (p == null) return;
            if ((p.CloneClimaxFaceHeld && __instance == p._climaxFemale) || __instance == p._gaugeFaceFemale)
                p.ApplySelectedEyeOpening(__instance);
        }
    }
}
