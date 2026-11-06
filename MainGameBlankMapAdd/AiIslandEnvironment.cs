using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MainGameBlankMapAdd
{
    // Capture the existing KKS environment before additive scene loading; never create a replacement sky.
    internal sealed class AiIslandEnvironment : IDisposable
    {
        private Material _sky;
        private Cubemap _oldReflection;
        private AmbientMode _ambientMode;
        private Color _ambient, _skyColor, _equator, _ground, _fogColor;
        private float _ambientIntensity, _reflection, _fogDensity, _fogStart, _fogEnd;
        private bool _fog;
        private FogMode _fogMode;
        private Light _sun;
        private DefaultReflectionMode _reflectionMode;
        internal static AiIslandEnvironment Capture() => new AiIslandEnvironment
        {
            _sky = RenderSettings.skybox, _sun = RenderSettings.sun, _ambientMode = RenderSettings.ambientMode,
            _ambient = RenderSettings.ambientLight, _skyColor = RenderSettings.ambientSkyColor,
            _equator = RenderSettings.ambientEquatorColor, _ground = RenderSettings.ambientGroundColor,
            _ambientIntensity = RenderSettings.ambientIntensity, _reflection = RenderSettings.reflectionIntensity,
            _reflectionMode = RenderSettings.defaultReflectionMode, _oldReflection = RenderSettings.customReflection, _fog = RenderSettings.fog,
            _fogMode = RenderSettings.fogMode, _fogColor = RenderSettings.fogColor, _fogDensity = RenderSettings.fogDensity,
            _fogStart = RenderSettings.fogStartDistance, _fogEnd = RenderSettings.fogEndDistance
        };
        internal void RestoreKksEnvironment()
        {
            RenderSettings.skybox = _sky; RenderSettings.sun = _sun;
            RenderSettings.ambientMode = _ambientMode; RenderSettings.ambientLight = _ambient;
            RenderSettings.ambientSkyColor = _skyColor; RenderSettings.ambientEquatorColor = _equator;
            RenderSettings.ambientGroundColor = _ground; RenderSettings.ambientIntensity = _ambientIntensity;
            RenderSettings.reflectionIntensity = _reflection; RenderSettings.defaultReflectionMode = _reflectionMode;
            RenderSettings.customReflection = _oldReflection;
            RenderSettings.fog = _fog; RenderSettings.fogMode = _fogMode; RenderSettings.fogColor = _fogColor;
            RenderSettings.fogDensity = _fogDensity; RenderSettings.fogStartDistance = _fogStart; RenderSettings.fogEndDistance = _fogEnd;
            DynamicGI.UpdateEnvironment();
        }
        public void Dispose() { RestoreKksEnvironment(); }
    }
}
