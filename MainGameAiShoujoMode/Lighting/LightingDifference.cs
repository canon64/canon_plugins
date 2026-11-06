using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace MainGameAiShoujoMode.Lighting
{
    [DataContract] internal sealed class LightingDifference
    {
        [DataMember] public string key, walking, h;
        internal static LightingDifference[] Compare(LightingSnapshot walking, LightingSnapshot h)
        {
            var before = Flatten(walking); var after = Flatten(h);
            return before.Keys.Union(after.Keys).OrderBy(key => key, StringComparer.Ordinal)
                .Select(key => new LightingDifference { key = key, walking = before.TryGetValue(key, out var left) ? left : null, h = after.TryGetValue(key, out var right) ? right : null })
                .Where(value => value.walking != value.h).ToArray();
        }
        private static Dictionary<string, string> Flatten(LightingSnapshot snapshot)
        {
            var output = new Dictionary<string, string>(StringComparer.Ordinal);
            output["time"] = LightingJson.Write(new[] { snapshot.hour }); output["weather"] = snapshot.weather;
            output["date"] = string.Join("/", new[] { snapshot.year, snapshot.month, snapshot.day });
            foreach (var value in snapshot.environment ?? Array.Empty<LightingValue>()) output["environment/" + value.name] = LightingJson.Write(value);
            foreach (var light in snapshot.lights ?? Array.Empty<LightingLight>()) output["light/" + light.path] = LightingJson.Write(light);
            foreach (var camera in snapshot.cameras ?? Array.Empty<LightingCamera>())
            {
                foreach (var value in camera.settings) output["camera/" + camera.role + "/" + value.name] = LightingJson.Write(value);
                foreach (var effect in camera.components) output["camera/" + camera.role + "/effect/" + effect.type + "/" + effect.path] = LightingJson.Write(effect);
            }
            foreach (var actor in snapshot.characters ?? Array.Empty<LightingRenderer>()) output["character/" + actor.path] = LightingJson.Write(actor.settings);
            return output;
        }
    }
}
