using System;
using UnityEngine;

namespace MainGameAiShoujoH.Contracts
{
    public sealed class HLightState
    {
        public Color Color { get; set; }
        public float Intensity { get; set; }
        public Quaternion LocalRotation { get; set; }
        public HLightState Copy() => new HLightState { Color = Color, Intensity = Intensity, LocalRotation = LocalRotation };
    }
    public sealed class HCameraState
    {
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public float FieldOfView { get; set; }
    }
    // Only explicit UI/API operations call these delegates. No frame subscription is installed.
    public sealed class HSceneControls
    {
        public Func<HLightState> ReadLight { get; set; }
        public Action<HLightState> ApplyLight { get; set; }
        public Action ResetLight { get; set; }
        public Func<HCameraState> ReadCamera { get; set; }
        public Action<HCameraState> ApplyCamera { get; set; }
        public Action ResetCamera { get; set; }
    }
}
