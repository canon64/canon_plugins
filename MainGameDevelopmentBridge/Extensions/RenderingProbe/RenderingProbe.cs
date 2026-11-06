using System;
using System.Collections.Generic;
using System.Linq;
using MainGameDevelopmentBridge;
using UnityEngine;

public sealed class RenderingProbe : IDevelopmentCommand
{
    public string Name => "rendering-state";
    private static object Rgb(Color value) => new { value.r, value.g, value.b, value.a };
    public object Execute(Dictionary<string, object> arguments, DevelopmentContext context)
    {
        return new
        {
            colorSpace = QualitySettings.activeColorSpace.ToString(),
            graphicsDevice = SystemInfo.graphicsDeviceName,
            environment = new { ambientMode = RenderSettings.ambientMode.ToString(), ambientLight = Rgb(RenderSettings.ambientLight),
                ambientSky = Rgb(RenderSettings.ambientSkyColor), ambientEquator = Rgb(RenderSettings.ambientEquatorColor), ambientGround = Rgb(RenderSettings.ambientGroundColor),
                RenderSettings.ambientIntensity, RenderSettings.reflectionIntensity, RenderSettings.fog, fogColor = Rgb(RenderSettings.fogColor),
                skybox = RenderSettings.skybox != null ? RenderSettings.skybox.name : null },
            cameras = Resources.FindObjectsOfTypeAll<Camera>().Where(camera => camera.gameObject.scene.IsValid()).Select(camera => new
            {
                reference = context.Reflection.Pack(camera), camera.name, camera.enabled, active = camera.gameObject.activeInHierarchy,
                camera.allowHDR, camera.allowMSAA, camera.cullingMask, camera.depth, camera.fieldOfView,
                target = camera.targetTexture != null ? camera.targetTexture.name : null,
                effects = camera.GetComponents<Behaviour>().Where(component => component != null).Select(component => new
                { type = component.GetType().FullName, component.enabled, reference = context.Reflection.Pack(component) }).ToArray()
            }).ToArray(),
            lights = Resources.FindObjectsOfTypeAll<Light>().Where(light => light.gameObject.scene.IsValid()).Select(light => new
            { reference = context.Reflection.Pack(light), light.name, light.enabled, active = light.gameObject.activeInHierarchy,
                type = light.type.ToString(), light.intensity, color = Rgb(light.color), light.range }).ToArray()
        };
    }
    public void Dispose() { }
}
