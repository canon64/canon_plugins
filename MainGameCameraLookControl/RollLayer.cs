using UnityEngine;

namespace MainGameCameraLookControl
{
    internal sealed class RollLayer
    {
        private readonly Transform[] _bones;
        private readonly Quaternion[] _nativeLocal, _nativeWorld, _appliedLocal;
        private bool _applied;

        internal RollLayer(Transform[] bones)
        {
            _bones = bones;
            _nativeLocal = new Quaternion[bones.Length];
            _nativeWorld = new Quaternion[bones.Length];
            _appliedLocal = new Quaternion[bones.Length];
        }

        internal void Remove()
        {
            if (!_applied) return;
            _applied = false;
            for (int i = 0; i < _bones.Length; i++)
                if (_bones[i] != null && Quaternion.Angle(_bones[i].localRotation, _appliedLocal[i]) < 0.01f)
                    _bones[i].localRotation = _nativeLocal[i];
        }

        internal void Apply(float[] rolls)
        {
            Remove();
            bool any = false;
            for (int i = 0; i < rolls.Length; i++) any |= Mathf.Abs(rolls[i]) > 0.0001f;
            if (!any) return;
            for (int i = 0; i < _bones.Length; i++)
            {
                if (_bones[i] == null) return;
                _nativeLocal[i] = _bones[i].localRotation;
                _nativeWorld[i] = _bones[i].rotation;
            }
            // Native order is neck -> head. Apply from each native world pose,
            // compensating the parent's roll so the head's forward remains aimed.
            float inherited = 0f;
            for (int i = 0; i < _bones.Length; i++)
            {
                inherited += rolls[i];
                _bones[i].rotation = _nativeWorld[i] * Quaternion.AngleAxis(inherited, Vector3.forward);
                _appliedLocal[i] = _bones[i].localRotation;
            }
            _applied = true;
        }
    }
}
