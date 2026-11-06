using System;
using System.IO;
using System.Linq;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    // One-shot selection from the H button. Furniture alignment is an explicitly permitted prototype.
    public static partial class AiModeHLocations
    {
        public sealed class Manifest { public int pointCount; public Point[] points; }
        public sealed class Rotation { public float x, y, z, w; }
        public sealed class Point
        {
            public string id, sourceName;
            public Vector3 sourcePosition;
            public Rotation sourceRotation;
            public int[] categories;
        }
        internal static AiModeHSession.HLocation Resolve(SandboxHost host, Transform target, Transform male = null, string characterId = null)
        {
            if (host?.Session == null || host.Island == null || target == null) throw new InvalidOperationException("会話場所と島が準備されていません。");
            var settings = host.Session.Settings;
            var file = settings.T("hSession.locations.path");
            if (!File.Exists(file)) throw new FileNotFoundException("H地点データが見つかりません。", file);
            var manifest = JsonCodec.FromJson<Manifest>(File.ReadAllText(file));
            if (manifest?.points == null || manifest.pointCount != manifest.points.Length || manifest.points.Any(point => point == null || string.IsNullOrEmpty(point.id) || point.sourceRotation == null)) throw new InvalidDataException("H地点データの形式が不正です。");
            Physics.SyncTransforms();
            if (!Ground(host, target.position, out var originFloor)) throw new InvalidOperationException("会話位置の足元を確認できません。");
            SelectionTrace trace = null;
            // The existing lazy logger also isolates diagnostic failures from H selection.
            if (Plugin.EventPositionTraceEnabled)
                Plugin.RecordEventPositions(() =>
                {
                    trace = new SelectionTrace { radius = settings.N("hSession.location.radius"),
                        verticalTolerance = settings.N("hSession.location.verticalTolerance"), originFloor = originFloor,
                        candidates = manifest.points.Select(point =>
                        {
                            var world = host.FromIslandSourcePosition(point.sourcePosition);
                            var femaleDelta = world - target.position;
                            var maleDelta = male == null ? Vector3.zero : world - male.position;
                            var distance = Vector3.ProjectOnPlane(femaleDelta, Vector3.up).magnitude;
                            return new CandidateTrace { id = point.id, sourceName = point.sourceName, categories = point.categories,
                                sourcePosition = point.sourcePosition, worldPosition = world, femaleToPoint = femaleDelta,
                                maleToPoint = maleDelta, maleAvailable = male != null, femaleHorizontalDistance = distance,
                                maleHorizontalDistance = Vector3.ProjectOnPlane(maleDelta, Vector3.up).magnitude,
                                result = point.categories == null || !point.categories.Any() ? "no_categories" :
                                    distance > settings.N("hSession.location.radius") ? "outside_radius" : "pending" };
                        }).OrderBy(row => row.femaleHorizontalDistance).ToArray() };
                    return Conversation.EventPositionSnapshot.Capture("h.candidates", host, male, target, characterId, selection: trace);
                });
            var candidates = manifest.points.Where(point => point.categories != null && point.categories.Any())
                .Select(point => new { point, position = host.FromIslandSourcePosition(point.sourcePosition) })
                .Where(item => Vector3.ProjectOnPlane(item.position - target.position, Vector3.up).magnitude <= settings.N("hSession.location.radius"))
                .OrderBy(item => Vector3.ProjectOnPlane(item.position - target.position, Vector3.up).sqrMagnitude);
            foreach (var item in candidates)
            {
                var row = trace?.candidates.FirstOrDefault(candidate => candidate.id == item.point.id);
                // Native pivots are on the floor even for seats; sample support separately, without moving that pivot.
                var floor = item.position;
                var groundFound = Ground(host, item.position, out var support);
                if (groundFound) floor = support;
                if (row != null) { row.groundFound = groundFound; row.supportWorld = floor; row.supportHeightDelta = floor.y - originFloor.y; }
                if (Mathf.Abs(floor.y - originFloor.y) > settings.N("hSession.location.verticalTolerance"))
                { if (row != null) row.result = "height_difference"; continue; }
                if (!Visible(host, target.position, item.position, row))
                { if (row != null) row.result = "line_of_sight_blocked"; continue; }
                var sourceRotation = new Quaternion(item.point.sourceRotation.x, item.point.sourceRotation.y, item.point.sourceRotation.z, item.point.sourceRotation.w);
                var forward = host.FromIslandSourcePosition(item.point.sourcePosition + sourceRotation * Vector3.forward) - item.position;
                var up = host.FromIslandSourcePosition(item.point.sourcePosition + sourceRotation * Vector3.up) - item.position;
                if (forward.sqrMagnitude <= settings.N("render.epsilon") || up.sqrMagnitude <= settings.N("render.epsilon"))
                { if (row != null) row.result = "invalid_rotation"; continue; }
                var selected = new AiModeHSession.HLocation
                {
                    SourceId = item.point.id, Position = item.position, Rotation = Quaternion.LookRotation(forward, up),
                    Categories = item.point.categories.Distinct().ToArray(), VerifiedGround = true
                };
                if (row != null)
                {
                    row.result = "selected";
                    foreach (var unvisited in trace.candidates.Where(candidate => candidate.result == "pending"))
                        unvisited.result = "not_tested_nearer_candidate_selected";
                }
                Plugin.RecordEventPositions(() => Conversation.EventPositionSnapshot.Capture("h.location_selection_result", host,
                    male, target, characterId, selected, trace));
                return selected;
            }
            // The user explicitly permits a rough floor/standing trial at the conversation position.
            var fallback = new AiModeHSession.HLocation
            {
                SourceId = "conversation-ground", Position = originFloor, Rotation = target.rotation,
                Categories = new[] { settings.I("hSession.category"), settings.I("hSession.location.standingCategory") }.Distinct().ToArray(),
                VerifiedGround = true
            };
            Plugin.RecordEventPositions(() => Conversation.EventPositionSnapshot.Capture("h.location_selection_result", host,
                male, target, characterId, fallback, trace));
            return fallback;
        }
        private static bool Ground(SandboxHost host, Vector3 point, out Vector3 floor)
        {
            var p = host.Session.Settings;
            var navigation = host.Session.Navigation;
            if (navigation != null && navigation.Status.ready && navigation.TryProject(point, out floor)) return true;
            float height = p.N("hSession.location.sightHeight");
            var hits = Physics.RaycastAll(point + Vector3.up * height, Vector3.down, height * p.N("math.two"), p.I("hSession.location.layerMask"), QueryTriggerInteraction.Ignore);
            foreach (var hit in hits.OrderBy(hit => hit.distance))
            {
                if (!IslandSolid(host, hit.collider) || hit.normal.y <= p.N("render.epsilon")) continue;
                if (Mathf.Abs(hit.point.y - point.y) > p.N("hSession.location.verticalTolerance")) continue;
                floor = hit.point; return true;
            }
            floor = point; return false;
        }
        private static bool Visible(SandboxHost host, Vector3 from, Vector3 to, CandidateTrace row = null)
        {
            var p = host.Session.Settings; float height = p.N("hSession.location.sightHeight");
            Vector3 start = from + Vector3.up * height, delta = to + Vector3.up * height - start;
            float distance = delta.magnitude;
            if (distance <= p.N("render.epsilon")) return true;
            var hits = Physics.RaycastAll(start, delta / distance, distance, p.I("hSession.location.layerMask"), QueryTriggerInteraction.Ignore);
            var blockers = hits.Where(hit => IslandSolid(host, hit.collider) && hit.distance < distance - p.N("hSession.location.supportRadius"));
            if (row != null)
            {
                var first = blockers.OrderBy(hit => hit.distance).FirstOrDefault();
                if (first.collider != null)
                { row.blockingCollider = first.collider.name; row.blockingHitWorld = first.point; }
            }
            return !blockers.Any();
        }
        private static bool IslandSolid(SandboxHost host, Collider collider) => collider != null
            && collider.gameObject.scene == host.Island.LoadedScene
            && !Physics.GetIgnoreLayerCollision(host.Session.Settings.I("math.zero"), collider.gameObject.layer);
    }
}
