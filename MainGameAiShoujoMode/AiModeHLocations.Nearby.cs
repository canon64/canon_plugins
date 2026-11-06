using System;
using System.IO;
using System.Linq;
using MainGameAiShoujoH.Contracts;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    public static partial class AiModeHLocations
    {
        internal static HPointChoice[] Nearby(SandboxHost host, Vector3 origin)
        {
            if (host?.Session == null || host.Island == null) throw new InvalidOperationException("島のHポイントを取得できません。");
            var manifest = JsonCodec.FromJson<Manifest>(File.ReadAllText(host.Session.Settings.T("hSession.locations.path")));
            if (manifest?.points == null || manifest.pointCount != manifest.points.Length)
                throw new InvalidDataException("Hポイント一覧が不正です。");
            return manifest.points.Where(point => point != null && point.sourceRotation != null && point.categories?.Any() == true)
                .Select(point => {
                    var position = host.FromIslandSourcePosition(point.sourcePosition);
                    var rotation = new Quaternion(point.sourceRotation.x, point.sourceRotation.y, point.sourceRotation.z, point.sourceRotation.w);
                    var forward = host.FromIslandSourcePosition(point.sourcePosition + rotation * Vector3.forward) - position;
                    var up = host.FromIslandSourcePosition(point.sourcePosition + rotation * Vector3.up) - position;
                    return new HPointChoice { Id = point.id, Position = position,
                        Rotation = Quaternion.LookRotation(forward, up), Categories = point.categories.Distinct().ToArray() };
                }).Where(point => HPointRange.Contains(origin, point.Position))
                .OrderBy(point => (point.Position - origin).sqrMagnitude)
                .Select((point, index) => { point.Name = "Hポイント " + (index + host.Session.Settings.I("math.one")).ToString("00"); return point; }).ToArray();
        }
    }
}
