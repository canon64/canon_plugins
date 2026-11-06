using System;
using System.Runtime.Serialization;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode.Conversation
{
    // Collected synchronously at explicit event boundaries, before pausing or relocating actors.
    [DataContract] internal sealed class EventPositionSnapshot
    {
        [DataMember] public string utc, phase, characterId, scene;
        [DataMember] public Point player, view, girl, selectedPoint;
        [DataMember] public string selectedPointId;
        [DataMember] public int[] selectedCategories;
        [DataMember] public Relation maleToFemale, maleToSelectedPoint, femaleToSelectedPoint;
        [DataMember] public AiModeHLocations.SelectionTrace selection;
        [DataMember] public FurniturePoint[] placedFurniture;
        [DataContract] internal sealed class FurniturePoint
        {
            [DataMember] public string instanceId, name, slotId, slotStatus;
            [DataMember] public string selectionStatus = "not_in_h_location_manifest";
            [DataMember] public Point root, slot;
            [DataMember] public Relation maleToSlot, femaleToSlot, selectedPointToSlot;
        }
        [DataContract] internal sealed class Point
        {
            [DataMember] public Vector3 world, islandSource;
            [DataMember] public Vector3 worldEuler;
            [DataMember] public bool rotationAvailable;
            [DataMember] public string objectName;
            internal static Point Capture(SandboxHost host, Transform target)
            {
                if (target == null) return null;
                var point = Capture(host, target.position);
                point.worldEuler = target.eulerAngles; point.objectName = target.name; point.rotationAvailable = true;
                return point;
            }
            internal static Point Capture(SandboxHost host, Vector3 position)
                => new Point { world = position, islandSource = host.ToIslandSourcePosition(position) };
        }
        [DataContract] internal sealed class Relation
        {
            [DataMember] public Vector3 worldDelta;
            [DataMember] public float distance, horizontalDistance, heightDelta;
            internal static Relation Between(Point from, Point to)
            {
                if (from == null || to == null) return null;
                var delta = to.world - from.world;
                return new Relation { worldDelta = delta, distance = delta.magnitude,
                    horizontalDistance = Vector3.ProjectOnPlane(delta, Vector3.up).magnitude, heightDelta = delta.y };
            }
        }
        internal static string Capture(string phase, SandboxHost host, Transform player, Transform girl,
            string characterId, AiModeHSession.HLocation selected = null, AiModeHLocations.SelectionTrace selection = null,
            FurniturePoint[] furniture = null)
        {
            var snapshot = new EventPositionSnapshot
            {
                utc = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                phase = phase, characterId = characterId, scene = host.gameObject.scene.name,
                player = Point.Capture(host, player), girl = Point.Capture(host, girl),
                view = Point.Capture(host, host.Walker?.ViewCamera?.transform),
                selectedPoint = selected == null ? null : Point.Capture(host, selected.Position),
                selectedPointId = selected?.SourceId, selectedCategories = selected?.Categories, selection = selection,
                placedFurniture = furniture
            };
            snapshot.maleToFemale = Relation.Between(snapshot.player, snapshot.girl);
            snapshot.maleToSelectedPoint = Relation.Between(snapshot.player, snapshot.selectedPoint);
            snapshot.femaleToSelectedPoint = Relation.Between(snapshot.girl, snapshot.selectedPoint);
            if (furniture != null)
                foreach (var item in furniture)
                {
                    item.maleToSlot = Relation.Between(snapshot.player, item.slot);
                    item.femaleToSlot = Relation.Between(snapshot.girl, item.slot);
                    item.selectedPointToSlot = Relation.Between(snapshot.selectedPoint, item.slot);
                }
            return JsonCodec.ToJson(snapshot);
        }
    }
}
