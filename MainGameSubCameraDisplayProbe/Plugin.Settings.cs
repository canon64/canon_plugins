using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using UnityEngine;

namespace MainGameSubCameraDisplayProbe
{
    [DataContract]
    internal sealed class ProbeSettings
    {
        [DataMember(Name = "Enabled")]
        public bool Enabled = true;
        [DataMember(Name = "UiVisible")]
        public bool UiVisible = false;
        [DataMember(Name = "ToggleUiKey")]
        public KeyCode ToggleUiKey = KeyCode.F8;
        [DataMember(Name = "AutoCreateOnStart")]
        public bool AutoCreateOnStart = true;

        [DataMember(Name = "WindowX")]
        public float WindowX = 20f;
        [DataMember(Name = "WindowY")]
        public float WindowY = 20f;
        [DataMember(Name = "WindowWidth")]
        public float WindowWidth = 560f;
        [DataMember(Name = "WindowHeight")]
        public float WindowHeight = 520f;

        [DataMember(Name = "RenderWidth")]
        public int RenderWidth = 1024;
        [DataMember(Name = "RenderHeight")]
        public int RenderHeight = 1024;
        [DataMember(Name = "RenderFilterMode")]
        public string RenderFilterMode = "Point";
        [DataMember(Name = "UseDisplayOverlayCamera")]
        public bool UseDisplayOverlayCamera = true;
        [DataMember(Name = "DisplayLayer")]
        public int DisplayLayer = 30;
        [DataMember(Name = "CameraFieldOfView")]
        public float CameraFieldOfView = 60f;
        [DataMember(Name = "CameraNearClip")]
        public float CameraNearClip = 0.03f;
        [DataMember(Name = "CameraFarClip")]
        public float CameraFarClip = 500f;
        [DataMember(Name = "PresetName")]
        public string PresetName = "SubCamera";
        [DataMember(Name = "SelectedSaveMode")]
        public int SelectedSaveMode = 0;
        [DataMember(Name = "SelectedBoneTarget")]
        public int SelectedBoneTarget = 0;
        [DataMember(Name = "SaveBoneCameraPosition")]
        public bool SaveBoneCameraPosition = false;
        [DataMember(Name = "Presets")]
        public SubCameraPreset[] Presets = new SubCameraPreset[0];
        [DataMember(Name = "DisplayPresetName")]
        public string DisplayPresetName = "Display";
        [DataMember(Name = "DisplayPresets")]
        public DisplayPreset[] DisplayPresets = new DisplayPreset[0];

        [DataMember(Name = "SpawnDistance")]
        public float SpawnDistance = 0.3f;
        [DataMember(Name = "CameraPosX")]
        public float CameraPosX = 0f;
        [DataMember(Name = "CameraPosY")]
        public float CameraPosY = 1.2f;
        [DataMember(Name = "CameraPosZ")]
        public float CameraPosZ = 0.8f;
        [DataMember(Name = "CameraRotX")]
        public float CameraRotX = 10f;
        [DataMember(Name = "CameraRotY")]
        public float CameraRotY = 180f;
        [DataMember(Name = "CameraRotZ")]
        public float CameraRotZ = 0f;

        [DataMember(Name = "DisplayPosX")]
        public float DisplayPosX = 0f;
        [DataMember(Name = "DisplayPosY")]
        public float DisplayPosY = 1.0f;
        [DataMember(Name = "DisplayPosZ")]
        public float DisplayPosZ = 1.5f;
        [DataMember(Name = "DisplayRotX")]
        public float DisplayRotX = 0f;
        [DataMember(Name = "DisplayRotY")]
        public float DisplayRotY = 180f;
        [DataMember(Name = "DisplayRotZ")]
        public float DisplayRotZ = 0f;
        [DataMember(Name = "DisplayWidth")]
        public float DisplayWidth = 1.0f;
        [DataMember(Name = "DisplayHeight")]
        public float DisplayHeight = 1.0f;
        [DataMember(Name = "DisplayAspectInitialized")]
        public bool DisplayAspectInitialized = true;

        [DataMember(Name = "GizmoSizeMultiplier")]
        public float GizmoSizeMultiplier = 1f;
        [DataMember(Name = "CameraGizmoVisible")]
        public bool CameraGizmoVisible = true;
        [DataMember(Name = "DisplayGizmoVisible")]
        public bool DisplayGizmoVisible = true;

        [DataMember(Name = "EnableVrGrip")]
        public bool EnableVrGrip = true;
        [DataMember(Name = "VrGripStartDistance")]
        public float VrGripStartDistance = 0.25f;
        [DataMember(Name = "VrFovStickEnabled")]
        public bool VrFovStickEnabled = true;
        [DataMember(Name = "VrFovStickSpeed")]
        public float VrFovStickSpeed = 30f;
        [DataMember(Name = "VrFovStickDeadzone")]
        public float VrFovStickDeadzone = 0.2f;
        [DataMember(Name = "GripMovesCamera")]
        public bool GripMovesCamera = true;
        [DataMember(Name = "GripMovesDisplay")]
        public bool GripMovesDisplay = false;
        [DataMember(Name = "ShowHandPreviewWhileGrabbingCamera")]
        public bool ShowHandPreviewWhileGrabbingCamera = true;
        [DataMember(Name = "PhotoCaptureOnTrigger")]
        public bool PhotoCaptureOnTrigger = true;
        [DataMember(Name = "PhotoOutputFolder")]
        public string PhotoOutputFolder = "image";
    }

    [DataContract]
    internal sealed class SubCameraPreset
    {
        [DataMember(Name = "Name")]
        public string Name;
        [DataMember(Name = "UseBoneLink")]
        public bool UseBoneLink;
        [DataMember(Name = "BoneTarget")]
        public int BoneTarget;
        [DataMember(Name = "BoneName")]
        public string BoneName;
        [DataMember(Name = "SaveCameraPosition")]
        public bool SaveCameraPosition;
        [DataMember(Name = "CameraPosition")]
        public float[] CameraPosition;
        [DataMember(Name = "CameraRotation")]
        public float[] CameraRotation;
        [DataMember(Name = "CameraOffsetLocal")]
        public float[] CameraOffsetLocal;
        [DataMember(Name = "LookAtPosition")]
        public float[] LookAtPosition;
        [DataMember(Name = "LookAtOffsetLocal")]
        public float[] LookAtOffsetLocal;
        [DataMember(Name = "Fov")]
        public float Fov;
    }

    [DataContract]
    internal sealed class DisplayPreset
    {
        [DataMember(Name = "Name")]
        public string Name;
        [DataMember(Name = "Position")]
        public float[] Position;
        [DataMember(Name = "Rotation")]
        public float[] Rotation;
        [DataMember(Name = "Width")]
        public float Width;
        [DataMember(Name = "Height")]
        public float Height;
    }

    internal static class SettingsStore
    {
        internal static ProbeSettings LoadOrCreate(string path, Action<string> logInfo, Action<string> logWarn)
        {
            try
            {
                if (!File.Exists(path))
                {
                    ProbeSettings created = Normalize(new ProbeSettings());
                    Save(path, created, logWarn);
                    logInfo?.Invoke("settings created: " + path);
                    return created;
                }

                string json = File.ReadAllText(path, Encoding.UTF8);
                ProbeSettings loaded = Deserialize(json) ?? new ProbeSettings();
                bool saveLoaded = false;
                if (json.IndexOf("\"DisplayAspectInitialized\"", StringComparison.Ordinal) < 0)
                {
                    ApplyDefaultDisplayAspect(loaded);
                    loaded.DisplayAspectInitialized = true;
                    saveLoaded = true;
                }
                if (json.IndexOf("\"ShowHandPreviewWhileGrabbingCamera\"", StringComparison.Ordinal) < 0)
                {
                    loaded.ShowHandPreviewWhileGrabbingCamera = true;
                    saveLoaded = true;
                }
                if (json.IndexOf("\"PhotoCaptureOnTrigger\"", StringComparison.Ordinal) < 0)
                {
                    loaded.PhotoCaptureOnTrigger = true;
                    saveLoaded = true;
                }
                if (json.IndexOf("\"PhotoOutputFolder\"", StringComparison.Ordinal) < 0)
                {
                    loaded.PhotoOutputFolder = "image";
                    saveLoaded = true;
                }
                if (json.IndexOf("\"VrFovStickEnabled\"", StringComparison.Ordinal) < 0)
                {
                    loaded.VrFovStickEnabled = true;
                    saveLoaded = true;
                }
                if (json.IndexOf("\"VrFovStickSpeed\"", StringComparison.Ordinal) < 0)
                {
                    loaded.VrFovStickSpeed = 30f;
                    saveLoaded = true;
                }
                if (json.IndexOf("\"VrFovStickDeadzone\"", StringComparison.Ordinal) < 0)
                {
                    loaded.VrFovStickDeadzone = 0.2f;
                    saveLoaded = true;
                }
                if (saveLoaded)
                {
                    Save(path, loaded, logWarn);
                }
                return Normalize(loaded);
            }
            catch (Exception ex)
            {
                logWarn?.Invoke("settings load failed: " + ex.Message);
                return Normalize(new ProbeSettings());
            }
        }

        internal static void Save(string path, ProbeSettings settings, Action<string> logWarn)
        {
            try
            {
                ProbeSettings normalized = Normalize(settings ?? new ProbeSettings());
                string json = Serialize(normalized);
                File.WriteAllText(path, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                logWarn?.Invoke("settings save failed: " + ex.Message);
            }
        }

        private static ProbeSettings Normalize(ProbeSettings settings)
        {
            if (settings == null)
                settings = new ProbeSettings();

            settings.WindowWidth = Mathf.Max(420f, Round2(settings.WindowWidth));
            settings.WindowHeight = Mathf.Max(320f, Round2(settings.WindowHeight));
            settings.WindowX = Round2(settings.WindowX);
            settings.WindowY = Round2(settings.WindowY);
            settings.RenderWidth = Mathf.Clamp(settings.RenderWidth, 64, 4096);
            settings.RenderHeight = Mathf.Clamp(settings.RenderHeight, 64, 4096);
            if (string.IsNullOrWhiteSpace(settings.RenderFilterMode))
                settings.RenderFilterMode = "Point";
            settings.DisplayLayer = Mathf.Clamp(settings.DisplayLayer, 0, 30);
            settings.CameraFieldOfView = Mathf.Clamp(Round2(settings.CameraFieldOfView), 10f, 170f);
            settings.CameraNearClip = Mathf.Max(0.01f, Round2(settings.CameraNearClip));
            settings.CameraFarClip = Mathf.Max(settings.CameraNearClip + 1f, Round2(settings.CameraFarClip));
            if (string.IsNullOrWhiteSpace(settings.PresetName))
                settings.PresetName = "SubCamera";
            settings.SelectedSaveMode = Mathf.Clamp(settings.SelectedSaveMode, 0, 1);
            settings.SelectedBoneTarget = Mathf.Clamp(settings.SelectedBoneTarget, 0, 2);
            settings.Presets = NormalizePresets(settings.Presets);
            if (string.IsNullOrWhiteSpace(settings.DisplayPresetName))
                settings.DisplayPresetName = "Display";
            settings.DisplayPresets = NormalizeDisplayPresets(settings.DisplayPresets);
            settings.SpawnDistance = Mathf.Clamp(Round2(settings.SpawnDistance), 0.1f, 8f);
            settings.CameraPosX = Round2(settings.CameraPosX);
            settings.CameraPosY = Round2(settings.CameraPosY);
            settings.CameraPosZ = Round2(settings.CameraPosZ);
            settings.CameraRotX = Round2(settings.CameraRotX);
            settings.CameraRotY = Round2(settings.CameraRotY);
            settings.CameraRotZ = Round2(settings.CameraRotZ);
            settings.DisplayPosX = Round2(settings.DisplayPosX);
            settings.DisplayPosY = Round2(settings.DisplayPosY);
            settings.DisplayPosZ = Round2(settings.DisplayPosZ);
            settings.DisplayRotX = Round2(settings.DisplayRotX);
            settings.DisplayRotY = Round2(settings.DisplayRotY);
            settings.DisplayRotZ = Round2(settings.DisplayRotZ);
            settings.DisplayWidth = Mathf.Max(0.1f, Round2(settings.DisplayWidth));
            settings.DisplayHeight = Mathf.Max(0.1f, Round2(settings.DisplayHeight));
            settings.GizmoSizeMultiplier = Mathf.Clamp(Round2(settings.GizmoSizeMultiplier), 0.2f, 4f);
            settings.VrGripStartDistance = Mathf.Clamp(Round2(settings.VrGripStartDistance), 0.05f, 1.5f);
            settings.VrFovStickSpeed = Mathf.Clamp(Round2(settings.VrFovStickSpeed), 1f, 120f);
            settings.VrFovStickDeadzone = Mathf.Clamp(Round2(settings.VrFovStickDeadzone), 0f, 0.95f);
            if (string.IsNullOrWhiteSpace(settings.PhotoOutputFolder))
                settings.PhotoOutputFolder = "image";
            settings.PhotoOutputFolder = settings.PhotoOutputFolder.Trim();
            return settings;
        }

        private static SubCameraPreset[] NormalizePresets(SubCameraPreset[] presets)
        {
            if (presets == null || presets.Length == 0)
                return new SubCameraPreset[0];

            for (int i = 0; i < presets.Length; i++)
            {
                SubCameraPreset preset = presets[i];
                if (preset == null)
                    continue;

                if (string.IsNullOrWhiteSpace(preset.Name))
                    preset.Name = "Preset " + (i + 1);
                preset.BoneTarget = Mathf.Clamp(preset.BoneTarget, 0, 2);
                preset.CameraPosition = RoundArray(preset.CameraPosition);
                preset.CameraRotation = RoundArray(preset.CameraRotation);
                preset.CameraOffsetLocal = RoundArray(preset.CameraOffsetLocal);
                preset.LookAtPosition = RoundArray(preset.LookAtPosition);
                preset.LookAtOffsetLocal = RoundArray(preset.LookAtOffsetLocal);
                preset.Fov = Mathf.Clamp(Round2(preset.Fov <= 0f ? 60f : preset.Fov), 10f, 170f);
            }

            return presets;
        }

        private static DisplayPreset[] NormalizeDisplayPresets(DisplayPreset[] presets)
        {
            if (presets == null || presets.Length == 0)
                return new DisplayPreset[0];

            for (int i = 0; i < presets.Length; i++)
            {
                DisplayPreset preset = presets[i];
                if (preset == null)
                    continue;

                if (string.IsNullOrWhiteSpace(preset.Name))
                    preset.Name = "Display " + (i + 1);
                preset.Position = RoundArray(preset.Position);
                preset.Rotation = RoundArray(preset.Rotation);
                preset.Width = Mathf.Max(0.1f, Round2(preset.Width));
                preset.Height = Mathf.Max(0.1f, Round2(preset.Height));
            }

            return presets;
        }

        private static float[] RoundArray(float[] values)
        {
            if (values == null)
                return null;

            for (int i = 0; i < values.Length; i++)
                values[i] = Round2(values[i]);
            return values;
        }

        private static float Round2(float value)
        {
            return (float)Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private static string Serialize(ProbeSettings settings)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(ProbeSettings));
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.WriteObject(stream, settings);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static ProbeSettings Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(ProbeSettings));
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return serializer.ReadObject(stream) as ProbeSettings;
            }
        }

        private static void ApplyDefaultDisplayAspect(ProbeSettings settings)
        {
            if (settings == null)
                return;

            int width = Mathf.Max(1, settings.RenderWidth);
            int height = Mathf.Max(1, settings.RenderHeight);
            float aspectHeight = settings.DisplayWidth * (height / (float)width);
            settings.DisplayHeight = Round2(Mathf.Max(0.1f, aspectHeight));
        }
    }
}
