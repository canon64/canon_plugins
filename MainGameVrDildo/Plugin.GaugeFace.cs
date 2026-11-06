using System.Collections.Generic;
using UnityEngine;

namespace MainGameVrDildo
{
    public sealed partial class Plugin
    {
        private float _motionGaugeCm;
        private ChaControl _gaugeFaceFemale;
        private int _gaugeFaceOriginalEyes, _gaugeFaceOriginalMouth;
        private int _gaugeFaceTonguePattern, _gaugeFaceEyePattern;
        private int _gaugeFaceEyelidShape = -1;
        private int _selectedEyelidSlot = -1;
        private void PreviewEyelidSlot(int slot)
        {
            var actor = CloneClimaxFaceHeld ? _climaxFemale : _gaugeFaceFemale;
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed || !_cloneReactionPermit.IsValid ||
                actor?.eyesCtrl == null || slot < 0 || slot >= _settings.EyelidShapeSlots.Length ||
                _settings.EyelidShapeSlots[slot] < 0 || actor.eyesCtrl.GetMaxPtn() <= 0) return;
            _selectedEyelidSlot = slot;
            int shape = Mathf.Clamp(_settings.EyelidShapeSlots[slot], 0, actor.eyesCtrl.GetMaxPtn() - 1);
            if (CloneClimaxFaceHeld) _climaxEyelidShape = shape;
            else _gaugeFaceEyelidShape = shape;
            actor.eyesCtrl.ChangePtn(shape, false);
            ApplySelectedEyeOpening(actor);
        }
        private int _gaugeFaceEyebrowPattern = -1, _gaugeFaceOriginalEyebrows;
        private int ChooseEyebrowPattern(ChaControl actor, int previous = -1)
        {
            var slots = _settings.EyebrowPatternSlots;
            if (slots == null || !System.Array.Exists(slots, value => value >= 0)) return -1;
            int maximum = (actor?.eyebrowCtrl?.GetMaxPtn() ?? 0) - 1;
            return ExpressionSlotChoices.Choose(slots, _settings.EyebrowPatternRandom,
                previous, maximum, 0, count => Random.Range(0, count));
        }
        private int ChooseGaugeEyePattern(int previous = -1) => ExpressionSlotChoices.Choose(_settings.EyePatternSlots,
            _settings.EyePatternRandom, previous, int.MaxValue, 0, count => Random.Range(0, count));
        private int ChooseEyelidShape(ChaControl actor, bool avoidPrevious = false)
        {
            int count = actor?.eyesCtrl?.GetMaxPtn() ?? 0;
            int previousSlot = _selectedEyelidSlot;
            _selectedEyelidSlot = -1;
            if (count <= 0) return -1;
            var slots = new List<int>();
            if (_settings.EyelidShapeSlots != null)
                for (int i = 0; i < System.Math.Min(5, _settings.EyelidShapeSlots.Length); i++)
                    if (_settings.EyelidShapeSlots[i] >= 0) slots.Add(i);
            if (slots.Count == 0) return -1;
            if (_settings.EyelidShapeRandom && avoidPrevious && slots.Count > 1) slots.Remove(previousSlot);
            _selectedEyelidSlot = slots[_settings.EyelidShapeRandom ? Random.Range(0, slots.Count) : 0];
            return Mathf.Clamp(_settings.EyelidShapeSlots[_selectedEyelidSlot], 0, count - 1);
        }

        private void RefreshSelectedEyeSettings(bool refreshMouth = false)
        {
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed || !_cloneReactionPermit.IsValid) return;
            if (CloneClimaxFaceHeld)
            {
                _climaxEyesPattern = ChooseGaugeEyePattern();
                _climaxEyelidShape = ChooseEyelidShape(_climaxFemale);
                int previousBrow = _climaxEyebrowPattern;
                _climaxEyebrowPattern = ChooseEyebrowPattern(_climaxFemale);
                if (_climaxEyebrowPattern >= 0) _climaxFemale.ChangeEyebrowPtn(_climaxEyebrowPattern, true);
                else if (previousBrow >= 0 && _climaxFemale.fileStatus.eyebrowPtn == previousBrow)
                    _climaxFemale.ChangeEyebrowPtn(_climaxOriginalEyebrowPattern, true);
                _climaxFemale.ChangeEyesPtn(_climaxEyesPattern, true);
                if (_climaxEyelidShape >= 0) _climaxFemale.eyesCtrl.ChangePtn(_climaxEyelidShape, false);
                ApplySelectedEyeOpening(_climaxFemale);
                if (refreshMouth) { _climaxMouthPattern = ChooseGaugeMouthPattern(-1); ApplyCloneClimaxMouth(); }
            }
            else if (_gaugeFaceFemale != null)
            {
                _gaugeFaceEyePattern = ChooseGaugeEyePattern();
                _gaugeFaceEyelidShape = ChooseEyelidShape(_gaugeFaceFemale);
                int previousBrow = _gaugeFaceEyebrowPattern;
                _gaugeFaceEyebrowPattern = ChooseEyebrowPattern(_gaugeFaceFemale);
                if (_gaugeFaceEyebrowPattern >= 0) _gaugeFaceFemale.ChangeEyebrowPtn(_gaugeFaceEyebrowPattern, true);
                else if (previousBrow >= 0 && _gaugeFaceFemale.fileStatus.eyebrowPtn == previousBrow)
                    _gaugeFaceFemale.ChangeEyebrowPtn(_gaugeFaceOriginalEyebrows, true);
                _gaugeFaceFemale.ChangeEyesPtn(_gaugeFaceEyePattern, true);
                if (_gaugeFaceEyelidShape >= 0) _gaugeFaceFemale.eyesCtrl.ChangePtn(_gaugeFaceEyelidShape, false);
                ApplySelectedEyeOpening(_gaugeFaceFemale);
                if (refreshMouth)
                {
                    _gaugeFaceTonguePattern = ChooseGaugeMouthPattern(-1);
                    _gaugeFaceFemale.ChangeMouthPtn(_gaugeFaceTonguePattern, true);
                }
            }
        }
        private float _nextGaugeMouthChangeAt;
        private int ChooseGaugeMouthPattern(int previous)
        {
            int maximum = (_female?.mouthCtrl?.GetMaxPtn() ?? 0) - 1;
            if (maximum < 0) return 0;
            return ExpressionSlotChoices.Choose(_settings.MouthPatternSlots, _settings.MouthPatternRandom,
                previous, maximum, 24, count => Random.Range(0, count));
        }

        private void UpdateGaugeFace()
        {
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed || !_cloneReactionPermit.IsValid)
            { ReleaseGaugeFace(); return; }
            if (CloneClimaxFaceHeld) return;
            bool active = _settings != null && _settings.Enabled && _settings.MotionGaugeEnabled &&
                _model != null && _female != null && _female.fileStatus != null &&
                _scene != null && _sceneFlags != null && _sceneFlags.mode == HFlag.EMode.aibu &&
                _selectedTarget == TargetKind.Vaginal &&
                _motionGaugeCm >= _settings.MotionGaugeFullCm * _settings.FaceStartGaugeFraction;
            if (!active)
            {
                ReleaseGaugeFace();
                return;
            }
            bool selectionChanged = false;
            string selectionReason = "timer";
            if (_gaugeFaceFemale != _female)
            {
                selectionChanged = true;
                selectionReason = "start";
                ReleaseGaugeFace();
                _gaugeFaceFemale = _female;
                _gaugeFaceOriginalEyes = _female.fileStatus.eyesPtn;
                _gaugeFaceOriginalMouth = _female.fileStatus.mouthPtn;
                _gaugeFaceOriginalEyebrows = _female.fileStatus.eyebrowPtn;
                _gaugeFaceEyePattern = ChooseGaugeEyePattern();
                _gaugeFaceEyelidShape = ChooseEyelidShape(_female);
                ApplySelectedEyeOpening(_female);
                _gaugeFaceEyebrowPattern = ChooseEyebrowPattern(_female);
                if (_gaugeFaceEyebrowPattern >= 0) _female.ChangeEyebrowPtn(_gaugeFaceEyebrowPattern, true);
                _gaugeFaceTonguePattern = ChooseGaugeMouthPattern(-1);
                _nextGaugeMouthChangeAt = Time.unscaledTime + _settings.MouthChangeSeconds;
            }
            if (Time.unscaledTime >= _nextGaugeMouthChangeAt)
            {
                selectionChanged = true;
                _gaugeFaceEyePattern = ChooseGaugeEyePattern(_gaugeFaceEyePattern);
                _gaugeFaceEyelidShape = ChooseEyelidShape(_female, true);
                _gaugeFaceEyebrowPattern = ChooseEyebrowPattern(_female, _gaugeFaceEyebrowPattern);
                if (_gaugeFaceEyebrowPattern >= 0) _female.ChangeEyebrowPtn(_gaugeFaceEyebrowPattern, true);
                _gaugeFaceTonguePattern = ChooseGaugeMouthPattern(_gaugeFaceTonguePattern);
                _nextGaugeMouthChangeAt = Time.unscaledTime + _settings.MouthChangeSeconds;
            }
            if (_female.fileStatus.eyesPtn != _gaugeFaceEyePattern)
                _female.ChangeEyesPtn(_gaugeFaceEyePattern, true);
            if (selectionChanged)
            {
                // The eye texture can stay unchanged. Do not depend on another
                // ChangeEyesPtn/ChangeFace call to commit a new shape or opening.
                if (_gaugeFaceEyelidShape >= 0) _female.eyesCtrl.ChangePtn(_gaugeFaceEyelidShape, false);
                ApplySelectedEyeOpening(_female);
            }
            if (_female.fileStatus.mouthPtn != _gaugeFaceTonguePattern)
                _female.ChangeMouthPtn(_gaugeFaceTonguePattern, true);
            if (selectionChanged) TraceVibeEvent("FACE_SELECTION", "reason=" + selectionReason + " " + DescribeGaugeFace());
        }

        private string DescribeGaugeFace() => "faceHeld=" + CloneClimaxFaceHeld
            + " faceOwned=" + (_gaugeFaceFemale != null)
            + " faceStart01=" + _settings.FaceStartGaugeFraction
            + " selectedEye=" + _gaugeFaceEyePattern + " selectedLid=" + _gaugeFaceEyelidShape
            + " selectedLidSlot=" + _selectedEyelidSlot + " selectedBrow=" + _gaugeFaceEyebrowPattern
            + " selectedMouth=" + _gaugeFaceTonguePattern
            + " actualEye=" + (_female?.fileStatus?.eyesPtn ?? -1)
            + " actualMouth=" + (_female?.fileStatus?.mouthPtn ?? -1)
            + " actualBrow=" + (_female?.fileStatus?.eyebrowPtn ?? -1)
            + " eyeOpen=" + (_female?.eyesCtrl?.FixedRate ?? -1f)
            + " nextFaceChange=" + _nextGaugeMouthChangeAt + " faceTime=" + Time.unscaledTime
            + " animation=" + _sceneFlags?.nowAnimStateName;

        private void ReleaseGaugeFace()
        {
            var actor = _gaugeFaceFemale;
            _gaugeFaceFemale = null;
            if (!CloneClimaxFaceHeld) ReleaseSelectedEyeOpening();
            if (actor != null && actor.fileStatus != null)
            {
                if (actor.fileStatus.eyesPtn == _gaugeFaceEyePattern)
                    actor.ChangeEyesPtn(_gaugeFaceOriginalEyes, true);
                if (_gaugeFaceEyebrowPattern >= 0 && actor.fileStatus.eyebrowPtn == _gaugeFaceEyebrowPattern)
                    actor.ChangeEyebrowPtn(_gaugeFaceOriginalEyebrows, true);
                if (actor.fileStatus.mouthPtn == _gaugeFaceTonguePattern)
                    actor.ChangeMouthPtn(_gaugeFaceOriginalMouth, true);
            }
        }
    }
}
