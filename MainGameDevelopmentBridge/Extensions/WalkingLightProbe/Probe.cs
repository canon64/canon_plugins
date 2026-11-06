using System;
using System.Collections.Generic;
using MainGameDevelopmentBridge;
using MainGameAiShoujoMode.Lighting;
using UnityEngine;

public sealed class WalkingLightProbe : IDevelopmentCommand
{
    public string Name => "walking-light-probe";
    NativeHLightingRenderer renderer;
    public object Execute(Dictionary<string, object> arguments, DevelopmentContext context)
    {
        Dispose();
        var camera = Camera.main;
        if (camera == null || camera.name != "Sandbox Camera") throw new InvalidOperationException("AI walking camera required");
        try
        {
            renderer = new NativeHLightingRenderer(camera.transform);
            renderer.Walking(camera);
            return new { applied = true, light = context.Reflection.Pack(renderer.OwnedLight) };
        }
        catch (Exception error) { return new { applied = false, error = error.ToString() }; }
    }
    public void Dispose() { renderer?.Dispose(); renderer = null; }
}
