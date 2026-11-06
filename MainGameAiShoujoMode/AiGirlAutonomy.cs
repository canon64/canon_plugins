using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MapEnvironmentRuntime;
using UnityEngine;
using UnityEngine.AI;

namespace MainGameAiShoujoMode
{
    public sealed class AiGirlArrival : MonoBehaviour
    {
        internal Action<Collider> Entered;
        internal Action<Collider> Exited;
        private void OnTriggerEnter(Collider other) { Entered?.Invoke(other); }
        private void OnTriggerExit(Collider other) { Exited?.Invoke(other); }
    }

    public sealed class AiGirlAutonomy : MonoBehaviour
    {
        private Parameters _settings;
        private NavigationWorld _world;
        private NavMeshAgent _agent;
        private CapsuleCollider _body;
        private GameObject _destination;
        private Action<string> _animate, _log;
        private readonly HashSet<string> _reservations = new HashSet<string>();
        private System.Random _random;
        private Coroutine _deadline, _rest;
        private bool _patrol, _calling;
        private int _failures, _repaths;
        private Vector3 _target;
        private Transform _caller;
        private bool _following;
        private SphereCollider _followRange;
        private AiGirlArrival _followEvents;
        private Action _segmentCompleted;
        private Action<Exception> _segmentFailed;
        public Func<Vector3,bool> SpecialRoute { get; set; }
        public event Action DestinationReached;
        public Func<Vector3,ISet<string>,System.Random,NavigationWaypoint> SpecialWaypoint { get; set; }
        public void WalkSegment(Vector3 target,Action completed,Action<Exception> failed)
        {
            _segmentCompleted=completed;_segmentFailed=failed;
            try{Move(target,"特殊通路へ移動中");}
            catch(Exception ex){_segmentCompleted=null;_segmentFailed=null;CancelJourney();failed(ex);}
        }
        public string State { get; private set; } = "停止";
        public string Error { get; private set; }
        public string DestinationId { get; private set; }
        public bool Moving => _agent != null && _agent.enabled && _agent.hasPath;
        private float N(string name) => _settings.N("navigation.actor." + name);
        private int I(string name) => _settings.I("navigation.actor." + name);
        public void Initialize(Parameters settings, NavigationWorld world, Action<string> animate, Action<string> log)
        {
            _settings = settings; _animate = animate; _log = log; SetNavigation(world);
            _random = new System.Random(I("seed"));
            var body = gameObject.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            _body = gameObject.AddComponent<CapsuleCollider>(); _body.isTrigger = true;
            _body.radius = N("radius"); _body.height = N("height");
            _body.center = Vector3.up * (N("height") / settings.N("math.two"));
            _agent = gameObject.AddComponent<NavMeshAgent>(); _agent.enabled = false;
            Configure(settings);
        }
        public void Configure(Parameters settings)
        {
            Stop(); _settings = settings;
            if (_agent == null) return;
            _agent.speed = N("speed"); _agent.angularSpeed = N("angularSpeed"); _agent.acceleration = N("acceleration");
            _agent.radius = N("radius"); _agent.height = N("height"); _agent.baseOffset = N("baseOffset");
            _agent.stoppingDistance = N("stoppingDistance"); _agent.autoTraverseOffMeshLink = false;
            _body.radius = N("radius"); _body.height = N("height"); _body.center = Vector3.up * N("height") / settings.N("math.two");
        }
        public void SetNavigation(NavigationWorld world)
        {
            Stop(); if (_world != null) _world.Rebuilding -= Stop;
            _world = world; if (_world != null) _world.Rebuilding += Stop;
        }
        public sealed class ConversationPause
        {
            internal bool patrol, calling;
            internal Transform caller;
        }
        public ConversationPause PauseForConversation()
        {
            var state = new ConversationPause { patrol = _patrol, calling = _calling, caller = _caller };
            Stop(); return state;
        }
        public void ResumeAfterConversation(ConversationPause state)
        {
            if (state == null) return;
            if (state.calling && state.caller != null) Call(state.caller);
            else if (state.patrol) Patrol();
            else Stop();
        }
        public void Patrol()
        {
            Stop(); _patrol = true; _failures = _settings.I("math.zero");
            TryOperation(NextDestination);
        }
        public void Call(Transform caller)
        {
            Stop(); _caller = caller; _calling = true; _repaths = _settings.I("math.zero");
            TryOperation(CallDestination);
        }
        public void Follow(Transform player)
        {
            Stop();
            if (player == null) throw new InvalidOperationException("同行する主人公がいません。");
            _following = true; _calling = true; _caller = player;
            if (_followRange == null)
            {
                _followRange = gameObject.AddComponent<SphereCollider>(); _followRange.isTrigger = true;
                _followEvents = gameObject.AddComponent<AiGirlArrival>();
                _followEvents.Exited = other =>
                {
                    if (!_following || _caller == null || other == null || !other.transform.IsChildOf(_caller) || Moving) return;
                    _calling = true; TryOperation(CallDestination);
                };
            }
            _followRange.radius = N("callRepathDistance"); _followRange.enabled = true;
            TryOperation(CallDestination);
        }
        public void Stop()
        {
            _following = false;
            if (_followRange != null) _followRange.enabled = false;
            _segmentCompleted=null;_segmentFailed=null;
            _patrol = false; _calling = false; _caller = null;
            CancelJourney();
            if (_rest != null) { StopCoroutine(_rest); _rest = null; }
            if (_agent != null) _agent.enabled = false;
            State = "停止";
        }
        internal void SuspendSpecialAnimation()
        {
            CancelJourney();
            // The Transform is moved by the finite passage animation until its exit is committed.
            if (_agent != null) _agent.enabled = false;
        }
        private void CancelJourney()
        {
            if (_deadline != null) { StopCoroutine(_deadline); _deadline = null; }
            if (_destination != null) { Destroy(_destination); _destination = null; }
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) { _agent.isStopped = true; _agent.ResetPath(); }
            _reservations.Clear(); DestinationId = null;
            _animate?.Invoke("Idle");
        }
        private void NextDestination()
        {
            if (!_patrol) return;
            if (_world == null) throw new InvalidOperationException("島の歩行経路が未準備です。");
            var waypoint=SpecialWaypoint?.Invoke(transform.position,_reservations,_random);
            if (waypoint==null && !_world.ChooseReachableWaypoint(transform.position, _reservations, _random, out waypoint))
                throw new InvalidOperationException("現在地から到達できる巡回地点がありません。");
            _reservations.Add(waypoint.Id); DestinationId = waypoint.Id;
            Move(waypoint.Position, "巡回中");
        }
        private void CallDestination()
        {
            if (_caller == null) throw new InvalidOperationException("呼び寄せ元がありません。");
            if (_world == null) throw new InvalidOperationException("島の歩行経路が未準備です。");
            var forward = Vector3.ProjectOnPlane(_caller.forward, Vector3.up).normalized;
            var desired = _caller.position + forward * N("callDistance");
            var ground = Physics.RaycastAll(desired, Vector3.down, _settings.N("character.groundProbeDistance"), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .Where(hit => !hit.transform.IsChildOf(transform)).OrderBy(hit => hit.distance).ToArray();
            if (!ground.Any() || !_world.TryProject(ground.First().point, out var point))
                throw new InvalidOperationException("主人公の近くに歩ける場所がありません。");
            Move(point, "呼び寄せ中");
        }
        private void Move(Vector3 point, string state)
        {
            if (_world == null || !_world.TryProject(transform.position, out var start)) throw new InvalidOperationException("女の子の足元に経路がありません。");
            if (Vector3.Distance(transform.position, start) > N("startProjectionTolerance")) throw new InvalidOperationException("足元の経路が離れています。歩ける地面へ配置してください。");
            if (!_world.TryCalculatePath(start, point, out var path))
            {
                if(_segmentCompleted==null&&SpecialRoute?.Invoke(point)==true)return;
                throw new InvalidOperationException("目的地まで経路がつながっていません。");
            }
            // A completed passage changes the Transform independently of the previous NavMesh location.
            // Rebind at the validated floor instead of keeping the entrance's internal agent position.
            _agent.enabled = false;
            _agent.agentTypeID = _world.AgentTypeId; _agent.areaMask = _world.AreaMask;
            transform.position = start;
            _agent.enabled = true;
            if (!_agent.isOnNavMesh || !_agent.Warp(start)) throw new InvalidOperationException("女の子を歩行経路へ接続できません。");
            _target = point;
            if (Vector3.Distance(transform.position, point) <= N("arrivalRadius")) { Arrived(); return; }
            _destination = new GameObject("AI Girl Destination Arrival");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_destination, gameObject.scene);
            _destination.transform.position = point + Vector3.up * N("height") / _settings.N("math.two");
            var trigger = _destination.AddComponent<SphereCollider>(); trigger.isTrigger = true; trigger.radius = N("arrivalRadius");
            _destination.AddComponent<AiGirlArrival>().Entered = other =>
            {
                if (other == _body && Mathf.Abs(transform.position.y - _target.y) <= N("arrivalHeightTolerance")) TryOperation(Arrived);
            };
            _agent.isStopped = false;
            if (!_agent.SetPath(path)) throw new InvalidOperationException("経路を女の子へ設定できません。");
            State = state; Error = null; _animate("Walk");
            float length = _settings.N("math.zero"); var corners = path.corners;
            for (int index = _settings.I("math.one"); index < corners.Length; index += _settings.I("math.one")) length += Vector3.Distance(corners[index - _settings.I("math.one")], corners[index]);
            _deadline = StartCoroutine(After(length / N("speed") * N("deadlineFactor") + N("deadlineGraceSeconds"), Deadline));
            Record();
        }
        private void Arrived()
        {
            if(_segmentCompleted!=null)
            {
                var completed=_segmentCompleted;_segmentCompleted=null;_segmentFailed=null;CancelJourney();DestinationReached?.Invoke();completed();return;
            }
            CancelJourney(); _failures = _settings.I("math.zero");
            if (_calling)
            {
                if (_caller != null && Vector3.ProjectOnPlane(_caller.position - transform.position, Vector3.up).magnitude > N("callRepathDistance") && (_following || _repaths < I("callMaxRepaths")))
                { _repaths += _settings.I("math.one"); CallDestination(); return; }
                _calling = false; State = _following ? "同行中・近くで待機" : "呼び寄せ完了・待機";
                if (_caller != null) { var direction = Vector3.ProjectOnPlane(_caller.position - transform.position, Vector3.up); if (direction.sqrMagnitude > _settings.N("render.epsilon")) transform.rotation = Quaternion.LookRotation(direction); }
            }
            else if (_patrol)
            {
                State = "休憩中";
                float duration = Mathf.Lerp(N("restMinSeconds"), N("restMaxSeconds"), (float)_random.NextDouble());
                _rest = StartCoroutine(After(duration, () => { _rest = null; TryOperation(NextDestination); }));
            }
            Record();
            DestinationReached?.Invoke();
        }
        private void Deadline()
        {
            _deadline = null;
            if (Vector3.Distance(transform.position, _target) <= N("arrivalRadius")) { TryOperation(Arrived); return; }
            if(_segmentFailed!=null)
            {
                var failed=_segmentFailed;_segmentCompleted=null;_segmentFailed=null;CancelJourney();failed(new TimeoutException("特殊通路入口まで到達できませんでした。"));return;
            }
            CancelJourney(); Error = "到着期限内に到達しませんでした。ドア・段差・経路障害を確認してください。";
            _failures += _settings.I("math.one");
            if (_patrol && _failures < I("maxFailures"))
            { State = "経路再選択待ち"; _rest = StartCoroutine(After(N("restMinSeconds"), () => { _rest = null; TryOperation(NextDestination); })); }
            else { _patrol = false; _calling = false; State = "経路失敗・停止"; }
            Record();
        }
        private IEnumerator After(float seconds, Action action) { yield return new WaitForSeconds(seconds); action(); }
        private void TryOperation(Action action)
        {
            try { action(); }
            catch (Exception ex) { Stop(); Error = ex.GetBaseException().Message; State = "経路エラー・停止"; Record(); }
        }
        private void Record() { _log?.Invoke("AI_GIRL_AUTONOMY state=" + State + " waypoint=" + DestinationId + " error=" + Error); }
        public void Release() { Stop(); _animate = null; if (_world != null) _world.Rebuilding -= Stop; _world = null; }
        private void OnDestroy() { Release(); }
    }
}
