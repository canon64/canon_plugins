using System;

namespace MainGameCameraLookControl
{
    // The game rewrites these values with each pose. Copy the current values for
    // this call; never turn a previous override into the next call's baseline.
    internal sealed class NativeParameterLease : IDisposable
    {
        private readonly NeckTypeStateVer2 _state;
        private readonly NeckLimitParameter[] _original;
        private readonly float _horizontal, _vertical, _speed;
        private bool _disposed;
        internal readonly NeckLimitParameter[] Effective;

        internal NativeParameterLease(NeckTypeStateVer2 state, LookPolicy policy, float pitch, float yaw,
            NeckLimitParameter[] scratch)
        {
            _state = state;
            _original = state.aParam;
            _horizontal = state.hAngleLimit; _vertical = state.vAngleLimit; _speed = state.leapSpeed;
            Effective = scratch;
            for (int i = 0; i < _original.Length; i++)
            {
                NeckLimitParameter source = _original[i];
                NeckLimitParameter result = scratch[i];
                BonePolicy bone = policy.Bone(i, _original.Length);
                result.name = source.name;
                result.upBendingAngle = LookPolicy.Upper(source.upBendingAngle, bone.Up);
                result.downBendingAngle = LookPolicy.Lower(source.downBendingAngle, bone.Down);
                result.minBendingAngle = LookPolicy.Upper(source.minBendingAngle, bone.Left);
                result.maxBendingAngle = LookPolicy.Lower(source.maxBendingAngle, bone.Right);
            }
            state.aParam = scratch;
            state.hAngleLimit = policy.HorizontalField(_horizontal, yaw);
            state.vAngleLimit = policy.VerticalField(_vertical, pitch);
            state.leapSpeed = _speed * policy.Speed;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _state.aParam = _original;
            _state.hAngleLimit = _horizontal;
            _state.vAngleLimit = _vertical;
            _state.leapSpeed = _speed;
        }
    }
}
