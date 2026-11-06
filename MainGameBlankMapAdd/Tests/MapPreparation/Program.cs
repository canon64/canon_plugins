using System;
using System.Collections.Generic;
using System.Linq;
using MainGameBlankMapAdd;
using MainGameBlankMapAdd.Map;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static void Check(bool ok, string message)
    {
        checks++;
        if (!ok) throw new Exception(message);
    }
    private static void Main()
    {
        var visible = new Renderer { enabled = true };
        var hidden = new Renderer { enabled = false };
        var terrain = new Terrain { drawHeightmap = true, drawTreesAndFoliage = false };
        var light = new Light { enabled = true };
        var audio = new AudioSource();
        var root = new GameObject(visible, hidden, terrain, light, audio);
        var map = new BaseMap { no = 42, mapRoot = root };
        var settings = new PluginSettings { AddedMapNo = 42 };
        int starts = 0, stops = 0;
        var session = new MapSessionController(new SourceMapBlankifier(_ => { }), _ => starts++, () => stops++);
        session.Reserve(map);
        session.PrepareSourceMapVisibility(settings);
        Check(!visible.enabled && !terrain.drawHeightmap, "source scenery is hidden before H start");
        Check(starts == 0 && stops == 0, "preparation never starts scene processing");
        Check(light.enabled && !audio.mute, "preparation leaves lights and audio untouched");
        int reads = root.Reads;
        session.PrepareSourceMapVisibility(settings);
        Check(root.Reads == reads, "duplicate reserve does not traverse the same source again");
        session.Apply(map, settings);
        Check(starts == 1 && !visible.enabled && !terrain.drawHeightmap, "authorized runtime takes over hidden scenery");
        Check(!light.enabled && audio.mute, "full runtime starts only on apply");
        session.Stop();
        Check(visible.enabled && !hidden.enabled, "handoff preserves original renderer states");
        Check(terrain.drawHeightmap && !terrain.drawTreesAndFoliage, "handoff preserves terrain flags");
        Check(light.enabled && !audio.mute && stops == 1, "stop restores full runtime state");
        session.PrepareSourceMapVisibility(settings);
        session.CancelSourceMapPreparation();
        session.CancelSourceMapPreparation();
        Check(visible.enabled && !hidden.enabled, "cancel before H and repeated cleanup restore original state");
        session.Reserve(new BaseMap { no = 7, mapRoot = root });
        session.PrepareSourceMapVisibility(settings);
        Check(visible.enabled && root.Reads > 0, "ordinary map stays visible");
        session.Reserve(map);
        settings.BlankifySceneOnLoad = false;
        session.PrepareSourceMapVisibility(settings);
        Check(visible.enabled, "blankification disabled remains respected");
        settings.BlankifySceneOnLoad = true;
        settings.DisableRenderers = settings.DisableTerrains = false;
        session.PrepareSourceMapVisibility(settings);
        Check(visible.enabled && terrain.drawHeightmap, "individual visibility options remain respected");
        Console.WriteLine("PASS " + checks + " source-map preparation checks");
    }
}

// Unity boundaries only; production map preparation, handoff and restoration are linked above.
internal class BaseMap { public int no; public GameObject mapRoot; }
namespace MainGameBlankMapAdd
{
    internal class PluginSettings
    {
        public int AddedMapNo;
        public bool BlankifySceneOnLoad = true, DisableRenderers = true, DisableTerrains = true;
        public bool DisableLights = true, DisableParticles = true, DisableAudioSources = true, EnableVideoRoom = true;
    }
}
namespace UnityEngine
{
    internal class GameObject
    {
        private readonly object[] children;
        public int Reads;
        public string name = "map";
        public GameObject(params object[] children) { this.children = children; }
        public int GetInstanceID() => GetHashCode();
        public T[] GetComponentsInChildren<T>(bool _) { Reads++; return children.OfType<T>().ToArray(); }
    }
    internal class Renderer { public bool enabled; }
    internal class Terrain { public bool drawHeightmap, drawTreesAndFoliage; }
    internal class Light { public bool enabled; }
    internal class AudioSource { public bool mute; }
    internal class ParticleSystem { public Emission emission = new Emission(); }
    internal class Emission { public bool enabled; }
}
