using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MainGameCameraControl
{
    [DataContract]
    internal sealed class PluginSettings
    {
        [DataMember(Name = "UiVisible")]
        public bool UiVisible = true;

        [DataMember(Name = "DetailLogEnabled")]
        public bool DetailLogEnabled = false;

        [DataMember(Name = "VoiceFaceEventCameraTriggerEnabled")]
        public bool VoiceFaceEventCameraTriggerEnabled = true;

        [DataMember(Name = "BoneReleaseByRightClick")]
        public bool BoneReleaseByRightClick = true;

        [DataMember(Name = "SaveCameraPoseOverrides")]
        public bool SaveCameraPoseOverrides = false;

        [DataMember(Name = "PosePresetAutoApply")]
        public bool PosePresetAutoApply = false;

        [DataMember(Name = "DefaultFov")]
        public float DefaultFov = 35f;

        [DataMember(Name = "ApplyFov")]
        public bool ApplyFov = false;

        [DataMember(Name = "TransitionSeconds")]
        public float TransitionSeconds = 0.4f;

        [DataMember(Name = "TransitionEasing")]
        public int TransitionEasing = 3;

        [DataMember(Name = "WindowX")]
        public float WindowX = 20f;

        [DataMember(Name = "WindowY")]
        public float WindowY = 100f;

        [DataMember(Name = "SaveWithBoneLink")]
        public bool SaveWithBoneLink = false;

        [DataMember(Name = "SelectedBoneTarget")]
        public int SelectedBoneTarget = 0;

        [DataMember(Name = "SelectedSaveMode")]
        public int SelectedSaveMode = 0;

        [DataMember(Name = "SaveBoneCameraPosition")]
        public bool SaveBoneCameraPosition = false;

        [DataMember(Name = "GizmoVisible")]
        public bool GizmoVisible = true;

        [DataMember(Name = "GizmoSize")]
        public float GizmoSize = 0.35f;

        [DataMember(Name = "BeatFovZoomEnabled")]
        public bool BeatFovZoomEnabled = false;

        [DataMember(Name = "BeatFovZoomMultiplier")]
        public float BeatFovZoomMultiplier = 1.15f;

        [DataMember(Name = "Presets")]
        public List<CameraPreset> Presets = new List<CameraPreset>();
    }

#pragma warning disable 0649
    [DataContract]
    internal sealed class CameraPreset
    {
        [DataMember(Name = "Name")]
        public string Name;

        [DataMember(Name = "TargetPosition")]
        public float[] TargetPosition;

        [DataMember(Name = "CameraDirection")]
        public float[] CameraDirection;

        [DataMember(Name = "Rotation")]
        public float[] Rotation;

        [DataMember(Name = "Fov")]
        public float Fov;

        [DataMember(Name = "UseBoneLink")]
        public bool UseBoneLink;

        [DataMember(Name = "BoneTarget")]
        public int BoneTarget;

        [DataMember(Name = "BoneName")]
        public string BoneName;

        [DataMember(Name = "LookAtOffsetLocal")]
        public float[] LookAtOffsetLocal;

        [DataMember(Name = "UseKsPlugFpvLink")]
        public bool UseKsPlugFpvLink;

        [DataMember(Name = "SaveCameraPosition")]
        public bool SaveCameraPosition;

        [DataMember(Name = "CameraOffsetLocal")]
        public float[] CameraOffsetLocal;

        [DataMember(Name = "CameraOffsetWorld")]
        public float[] CameraOffsetWorld;

        [DataMember(Name = "RotationOffset")]
        public float[] RotationOffset;

        [DataMember(Name = "Position")]
        public float[] LegacyPosition;

        [DataMember(Name = "LookAt")]
        public float[] LegacyLookAt;

        [DataMember(Name = "Distance")]
        public float LegacyDistance;

        [DataMember(Name = "UsePoseOverrides")]
        public bool UsePoseOverrides;

        [DataMember(Name = "PoseOverrides")]
        public CameraPoseOverride[] PoseOverrides;
    }

    [DataContract]
    internal sealed class CameraPoseOverride
    {
        [DataMember(Name = "Key")]
        public string Key;

        [DataMember(Name = "DisplayName")]
        public string DisplayName;

        [DataMember(Name = "UseBoneLink")]
        public bool UseBoneLink;

        [DataMember(Name = "UseKsPlugFpvLink")]
        public bool UseKsPlugFpvLink;

        [DataMember(Name = "BoneTarget")]
        public int BoneTarget;

        [DataMember(Name = "BoneName")]
        public string BoneName;

        [DataMember(Name = "SaveCameraPosition")]
        public bool SaveCameraPosition;

        [DataMember(Name = "TargetPosition")]
        public float[] TargetPosition;

        [DataMember(Name = "CameraDirection")]
        public float[] CameraDirection;

        [DataMember(Name = "Rotation")]
        public float[] Rotation;

        [DataMember(Name = "LookAtOffsetLocal")]
        public float[] LookAtOffsetLocal;

        [DataMember(Name = "CameraOffsetLocal")]
        public float[] CameraOffsetLocal;

        [DataMember(Name = "CameraOffsetWorld")]
        public float[] CameraOffsetWorld;

        [DataMember(Name = "Fov")]
        public float Fov;
    }
#pragma warning restore 0649
}
