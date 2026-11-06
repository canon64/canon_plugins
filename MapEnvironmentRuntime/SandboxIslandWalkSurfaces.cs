using System;
using UnityEngine;
using UnityEngine.AI;

namespace MapEnvironmentRuntime
{
    // Called by existing player input only. No autonomous Update, search loop, or collider replacement.
    public sealed class SandboxIslandWalkSurfaces : IDisposable
    {
        private readonly CharacterController _body;
        private readonly Func<Parameters> _settings;
        private NavigationWorld _world;
        private GameObject _foot;
        private NavMeshAgent _agent;
        private bool _airborne, _walking, _enabled;
        private Parameters P => _settings();
        private float EyeHeight => _body.height - _body.radius;
        private Vector3 Feet => _body.transform.position - Vector3.up * EyeHeight;
        public bool Enabled => _world != null && _enabled;
        public bool RequiresPhysicalBody => !Enabled || !Grounded;
        public bool Grounded => Enabled && !_airborne && _agent != null && _agent.enabled && _agent.isOnNavMesh;
        public string State { get; private set; }
        public int MoveFrame { get; private set; }
        public bool CommandActive { get; private set; }
        public Vector3 RequestedDelta { get; private set; }
        public Vector3 GuidedDelta { get; private set; }
        public Vector3 ActualDelta { get; private set; }
        public string Constraint { get; private set; }
        public CollisionFlags LastCollisionFlags { get; private set; }
        public SandboxIslandWalkSurfaces(CharacterController body, Func<Parameters> settings)
        {
            _body = body; _settings = settings;
        }
        public void SetNavigation(NavigationWorld world)
        {
            if (_world != null) { _world.Rebuilding -= BeforeRebuild; _world.Rebuilt -= AfterRebuild; }
            ReleaseAgent(); _world = world;
            _enabled = _world != null && bool.Parse(P.T("movement.navigation.enabled"));
            if (_world == null) { State = "No navigation world"; return; }
            _world.Rebuilding += BeforeRebuild; _world.Rebuilt += AfterRebuild;
            if (_walking && Enabled && _world.Status.ready) Bind(true);
        }
        public void Configure(bool walking)
        {
            bool changed = _walking != walking; _walking = walking;
            _enabled = _world != null && bool.Parse(P.T("movement.navigation.enabled"));
            if (!walking || !Enabled) { ReleaseAgent(); _airborne = false; return; }
            if (_agent != null)
            {
                _agent.height = _body.height; _agent.radius = _body.radius;
                _agent.obstacleAvoidanceType = (ObstacleAvoidanceType)Enum.Parse(typeof(ObstacleAvoidanceType), P.T("movement.navigation.obstacleAvoidance"));
            }
            else if (_world.Status.ready && (changed || !_airborne)) Bind(true);
        }
        public void Relocate()
        {
            ReleaseAgent(); _airborne = false;
            if (_walking && Enabled && _world.Status.ready) Bind(true);
        }
        private void BeforeRebuild() { ReleaseAgent(); State = "Navigation rebuild"; }
        private void AfterRebuild() { if (_walking && Enabled) Bind(true); }
        private bool Project(Vector3 point, bool entry, out Vector3 projected)
        {
            projected = point;
            if (_world == null || !_world.Status.ready) return false;
            float radius = entry ? P.N("movement.navigation.entryMaxVertical") : P.N("navigation.projectRadius");
            var filter = new NavMeshQueryFilter { agentTypeID = _world.AgentTypeId, areaMask = _world.AreaMask };
            if (!NavMesh.SamplePosition(point, out var hit, radius, filter)) return false;
            var horizontal = Vector3.ProjectOnPlane(hit.position - point, Vector3.up);
            if (horizontal.magnitude > P.N("navigation.projectRadius")) return false;
            if (Mathf.Abs(hit.position.y - point.y) > (entry ? P.N("movement.navigation.entryMaxVertical") : P.N("navigation.maxVerticalProjection"))) return false;
            projected = hit.position; return true;
        }
        private bool Bind(bool entry)
        {
            ReleaseAgent();
            if (!Project(Feet, entry, out var floor))
            {
                State = "No walk surface at player feet";
                Debug.LogWarning("SANDBOX_PLAYER_NAVIGATION_UNBOUND feet=" + Feet);
                return false;
            }
            if (entry)
            {
                bool enabled = _body.enabled; _body.enabled = false;
                _body.transform.position = floor + Vector3.up * EyeHeight; _body.enabled = enabled;
            }
            _foot = new GameObject("AI Player Navigation Foot");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_foot, _body.gameObject.scene);
            _foot.transform.position = floor;
            _agent = _foot.AddComponent<NavMeshAgent>(); _agent.enabled = false;
            _agent.agentTypeID = _world.AgentTypeId; _agent.areaMask = _world.AreaMask;
            _agent.height = _body.height; _agent.radius = _body.radius;
            _agent.baseOffset = P.N("math.zero"); _agent.speed = P.N("math.zero");
            _agent.updatePosition = true; _agent.updateRotation = false; _agent.updateUpAxis = false;
            _agent.autoTraverseOffMeshLink = false; _agent.autoRepath = true;
            _agent.obstacleAvoidanceType = (ObstacleAvoidanceType)Enum.Parse(typeof(ObstacleAvoidanceType), P.T("movement.navigation.obstacleAvoidance"));
            _agent.enabled = true;
            if (!_agent.Warp(floor)) { ReleaseAgent(); State = "Player navigation warp failed"; return false; }
            _airborne = false; _body.enabled = false; State = "Grounded on original walk surface";
            Debug.Log("SANDBOX_PLAYER_NAVIGATION_BOUND feet=" + floor + " agentType=" + _world.AgentTypeId);
            return true;
        }
        public void BeginJump()
        {
            _airborne = true; _body.enabled = true;
            if (_agent != null) _agent.enabled = false;
            State = "Physical jump";
        }
        public CollisionFlags Move(Vector3 velocity, float deltaTime, bool rising)
        {
            Vector3 before = _body.transform.position;
            var requested = Enabled && !_airborne ? Vector3.ProjectOnPlane(velocity, Vector3.up) * deltaTime : velocity * deltaTime;
            CommandActive = requested.sqrMagnitude > P.N("render.epsilon");
            if (!Enabled)
            {
                var flags = _body.Move(velocity * deltaTime);
                if (CommandActive) Record(before, requested, requested, flags, "Physical movement");
                return flags;
            }
            if (!_world.Status.ready)
            {
                if (CommandActive) Record(before, requested, Vector3.zero, UnityEngine.CollisionFlags.None, "Navigation rebuilding");
                return UnityEngine.CollisionFlags.None;
            }
            if (!_airborne)
            {
                if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                {
                    var fallback = _body.Move(velocity * deltaTime);
                    if (CommandActive) Record(before, requested, velocity * deltaTime, fallback, "Physical movement outside navigation");
                    return fallback;
                }
                var horizontal = Vector3.ProjectOnPlane(velocity, Vector3.up);
                if (!CommandActive) return UnityEngine.CollisionFlags.Below;
                Vector3 origin = _agent.nextPosition;
                _agent.Move(horizontal * deltaTime);
                Vector3 target = _world.ConstrainMovement(origin, _agent.nextPosition, _body.radius, _body.height, out bool boundary);
                if (boundary) _agent.nextPosition = target;
                Vector3 desired = _agent.nextPosition + Vector3.up * EyeHeight;
                var guided = desired - before;
                _body.transform.position = desired;
                var flags = UnityEngine.CollisionFlags.Below;
                Record(before, requested, guided, flags, "Navigation walk");
                return flags;
            }
            Vector3 previous = Feet;
            var collisions = _body.Move(velocity * deltaTime);
            Vector3 current = Feet;
            if (!rising && Project(current, false, out var landing)
                && previous.y >= landing.y - P.N("movement.navigation.landTolerance")
                && current.y <= landing.y + P.N("movement.navigation.landTolerance"))
            {
                _body.Move(Vector3.up * (landing.y - current.y));
                Bind(false);
            }
            if (CommandActive) Record(before, requested, requested, collisions, "Physical jump");
            return collisions;
        }
        public bool WillMove(Vector3 velocity)
        {
            var motion = Enabled && !_airborne ? Vector3.ProjectOnPlane(velocity, Vector3.up) : velocity;
            return motion.sqrMagnitude > P.N("render.epsilon");
        }
        private void Record(Vector3 before, Vector3 requested, Vector3 guided, CollisionFlags flags, string phase)
        {
            MoveFrame = Time.frameCount; RequestedDelta = requested; GuidedDelta = guided;
            ActualDelta = _body.transform.position - before; LastCollisionFlags = flags;
            float requestedLength = Vector3.ProjectOnPlane(requested, Vector3.up).magnitude;
            float guidedLength = Vector3.ProjectOnPlane(guided, Vector3.up).magnitude;
            float actualLength = Vector3.ProjectOnPlane(ActualDelta, Vector3.up).magnitude;
            Constraint = phase;
            if (requestedLength > P.N("render.epsilon") && guidedLength <= P.N("render.epsilon")) Constraint = "Navigation boundary";
            else if (guidedLength > P.N("render.epsilon") && actualLength <= P.N("render.epsilon")) Constraint = "Physical collider";
            else if (guidedLength + P.N("render.epsilon") < requestedLength) Constraint = "Navigation constrained movement";
            else if (actualLength + P.N("render.epsilon") < guidedLength) Constraint = "Physical collider constrained movement";
        }
        private void ReleaseAgent()
        {
            if (_agent != null) _agent.enabled = false;
            _agent = null;
            if (_foot != null) UnityEngine.Object.DestroyImmediate(_foot);
            _foot = null;
        }
        public void Dispose()
        {
            if (_world != null) { _world.Rebuilding -= BeforeRebuild; _world.Rebuilt -= AfterRebuild; }
            _world = null; ReleaseAgent();
        }
    }
}






