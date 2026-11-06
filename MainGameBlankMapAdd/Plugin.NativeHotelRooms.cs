using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private AiIslandEnvironment _nativeHotelRoomEnvironment;
        private SphericalHarmonicsL2 _nativeHotelRoomOldProbe;
        private static bool IsNativeHotelRoom(string scene) =>
            scene == "CanonPinkRoom201Map" || scene == "CanonReceptionSuiteMap";

        private static Transform NativeHotelRoomAnchor(string scene, GameObject root)
        {
            if (!IsNativeHotelRoom(scene)) return null;
            var group = root.transform.Find("MapObjectGroup");
            var anchor = group != null ? group.Find("yuka01") : null;
            if (anchor == null) throw new InvalidOperationException("Hotel map bed anchor is missing");
            return anchor;
        }

        private void CaptureNativeHotelRoomEnvironment(string scene)
        {
            ReleaseNativeHotelRoomEnvironment();
            if (!IsNativeHotelRoom(scene)) return;
            _nativeHotelRoomOldProbe = RenderSettings.ambientProbe;
            _nativeHotelRoomEnvironment = AiIslandEnvironment.Capture();
        }

        private static void ApplyNativeHotelRoomEnvironment(string scene, GameObject root)
        {
            if (!IsNativeHotelRoom(scene)) return;
            var info = root.GetComponent<SunLightInfo>();
            var values = info != null ? AccessTools.Field(typeof(SunLightInfo), "_infos")?.GetValue(info) as Array : null;
            var night = values?.Cast<object>().LastOrDefault();
            if (night == null) throw new InvalidOperationException("Hotel map night environment is missing");
            var ambient = (Color)AccessTools.Field(night.GetType(), "ambientSkyColor").GetValue(night);
            RenderSettings.skybox = null; RenderSettings.sun = (Light)AccessTools.Field(typeof(SunLightInfo), "_targetLight").GetValue(info);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = ambient;
            var probe = new SphericalHarmonicsL2(); probe.AddAmbientLight(ambient);
            RenderSettings.ambientProbe = probe; RenderSettings.fog = (bool)AccessTools.Field(night.GetType(), "fogUse").GetValue(night);
        }

        private void ReleaseNativeHotelRoomEnvironment()
        {
            if (_nativeHotelRoomEnvironment == null) return;
            _nativeHotelRoomEnvironment.Dispose(); RenderSettings.ambientProbe = _nativeHotelRoomOldProbe;
            _nativeHotelRoomEnvironment = null;
        }
    }
}
