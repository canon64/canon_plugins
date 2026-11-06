using System;
using UnityEngine;

namespace MapEnvironmentRuntime
{
    // The environment UI can retain its existing view API while movement belongs to the native game.
    public interface INativeWalkingDriver : IDisposable
    {
        Camera ViewCamera { get; }
        bool Crouching { get; }
        Vector3 FeetLocalOffset { get; }
        WalkingStatus Snapshot();
        void Configure(Parameters settings);
        void PlaceAtIslandStart(Vector3 eyePosition, float heading);
        void WarpFeet(Vector3 feet, Quaternion heading);
        void SetViewPose(Vector3 eyePosition, Quaternion heading, float fieldOfView);
        void SetInputBlocked(bool blocked);
        Action PrepareTraversal(Transform motion);
    }
    public interface IWalkingInputNotifications
    {
        event Action InputBlockChanged;
    }
}
