using System;
using MainGameAiShoujoMode.Life;
using UnityEngine;
using System.Linq;
using MapEnvironmentRuntime;

namespace MainGameAiShoujoMode
{
    // State-change subscription only. Navigation owns travel and arrival events.
    internal sealed class AiLifeActorBridge : IDisposable
    {
        private readonly AiLifeController _life;
        private readonly AiAnimationTrial _actor;
        private readonly Func<Transform> _player;
        private readonly Action<string> _log;
        private readonly Func<double> _clock;
        private readonly NavigationWorld _navigation;
        private AiLifeActivityMotion _motion;
        private IDisposable _furnitureMotion;
        internal Furniture.AiFurnitureSession Furniture { get; set; }
        private string _running;
        private string _generation;
        private bool _arrived;
        private double _clockAt;
        private bool _synchronizing;
        private bool _applying, _disposed, _resumePending;
        private string _appliedAction;
        private readonly ChaControl _identity;
        internal AiLifeActorBridge(AiLifeController life, AiAnimationTrial actor, Func<Transform> player, Func<double> clock, Action<string> log, SandboxHost host, Furniture.AiFurnitureSession furniture=null)
        {
            _navigation = host.Session.Navigation;Furniture=furniture;
            _life = life; _actor = actor; _player = player; _clock = clock; _clockAt = clock(); _log = log; _identity = actor.CurrentActor;
            var state = life.State;
            if (state.hasPosition && actor.Root != null && !actor.PodSequenceActive)
                actor.Root.SetPositionAndRotation(new Vector3(state.x, state.y, state.z), Quaternion.Euler(Vector3.up * state.yaw));
            life.Changed += Apply; actor.LifeActionReady += Ready; actor.LifeDestinationReached += SavePosition; Apply(state);
            if (_navigation != null) { _navigation.Rebuilding += NavigationRebuilding; _navigation.Rebuilt += Ready; }
        }
        private void Ready() { CancelRunning(); _appliedAction = null; Apply(_life.State); }
        private void NavigationRebuilding() { CancelRunning(); _appliedAction = null; }
        private void CancelRunning(bool interrupted=true)
        {
            var wasRunning = _running != null; _running = null; _generation = null; _arrived = false;
            if (wasRunning) _appliedAction = null;
            _motion?.Dispose(); _motion = null;var furniture=_furnitureMotion;_furnitureMotion=null;furniture?.Dispose();
            if(interrupted&&furniture!=null&&_life.State.activity?.furnitureTarget!=null)_life.CancelActivity("ベッド利用を中断しました。",_life.State.activity.id);
            if (wasRunning && ReferenceEquals(_identity, _actor.CurrentActor)) _actor.StopSpecialSegment();
        }
        private bool Current(string id, string generation) => !_disposed && _running == id && _generation == generation && !_life.Conversing && !_life.InH
            && _actor != null && _actor.isActiveAndEnabled && !_actor.LifeMotionOccupied && ReferenceEquals(_identity, _actor.CurrentActor) && _life.State.activity?.id == id;
        private void BeginActivity(AiLifeActivity activity)
        {
            if (_running == activity.id) return;
            CancelRunning(); _running = activity.id; _generation = Guid.NewGuid().ToString("N");
            if (activity.kind != "motion" && _navigation?.Status.ready != true) throw new InvalidOperationException("生活行動の歩行経路が準備されていません。");
            var settings = _life.Activities;
            if(activity.furnitureTarget!=null)
            {
                if(Furniture==null)throw new InvalidOperationException("家具の利用機能が準備されていません。");
                var bedId=activity.id;var bedGeneration=_generation;
                _furnitureMotion=Furniture.BeginBed(_life,activity,()=>FinishActivity(bedId,bedGeneration),error=>FailActivity(bedId,bedGeneration,error));return;
            }
            if (activity.kind == "motion")
            {
                var here = _actor.Root.position;
                _life.ActivityDestination(activity.id, "motion:" + activity.animationActionId, here.x, here.y, here.z);
                activity = _life.State.activity;
                _motion = new AiLifeActivityMotion(_actor, settings, activity);
                _arrived = true;
                _life.ActivityArrived(activity.id);
                var requestId = activity.id; var requestGeneration = _generation;
                _motion.Begin(activity, () => FinishActivity(requestId, requestGeneration), error => FailActivity(requestId, requestGeneration, error));
                return;
            }
            if (!activity.hasDestination)
            {
                var origin = _actor.Root.position;
                var point = _navigation.Waypoints.Where(v => Vector3.Distance(origin, v.Position) >= settings.minimumTravel
                    && Vector3.Distance(origin, v.Position) <= settings.maximumTravel)
                    .OrderBy(v => (v.Position - origin).sqrMagnitude)
                    .FirstOrDefault(v => _navigation.TryCalculatePath(origin, v.Position, out _));
                if (point == null) throw new InvalidOperationException("近くに移動できる仮の生活地点がありません。歩ける広い場所で頼んでください。");
                _life.ActivityDestination(activity.id, point.Id, point.Position.x, point.Position.y, point.Position.z);
                activity = _life.State.activity;
            }
            var destination = new Vector3(activity.x, activity.y, activity.z);
            if (!_navigation.TryProject(destination, out var projected) || !_navigation.TryCalculatePath(_actor.Root.position, projected, out _))
                throw new InvalidOperationException("保存した生活地点へ到達できません。行動を中止して、もう一度頼んでください。");
            var id = activity.id;
            var generation = _generation;
            _motion = new AiLifeActivityMotion(_actor, settings, activity);
            _actor.ApplyLifeAction("idle", null);
            _actor.WalkSpecialSegment(projected, () =>
            {
                if (!Current(id, generation) || _arrived) return;
                _arrived = true;
                try { _life.ActivityArrived(id); _motion.Begin(_life.State.activity, () => FinishActivity(id, generation), error => FailActivity(id, generation, error)); }
                catch (Exception error) { FailActivity(id, generation, error); }
            }, error => FailActivity(id, generation, error));
        }
        private void FinishActivity(string id, string generation)
        {
            if (!Current(id, generation)) return;
            CancelRunning(false); _appliedAction = null;
            try { if (!_life.CompleteActivity(id)) _life.CancelActivity("生活行動は中断されました。消費・完了効果は反映していません。", id); }
            catch (Exception error) { _life.CancelActivity("生活行動を確定できませんでした：" + error.GetBaseException().Message, id); }
            Apply(_life.State);
        }
        private void FailActivity(string id, string generation, Exception error)
        {
            if (!Current(id, generation)) return;
            CancelRunning(); _appliedAction = null;
            _resumePending = _applying;
            _life.CancelActivity("生活行動を実行できませんでした：" + error.GetBaseException().Message, id);
        }
        internal void SynchronizeClock()
        {
            if (_synchronizing || _disposed) return;
            _synchronizing = true;
            try
            {
                var now = _clock(); var delta = now - _clockAt; _clockAt = now;
                if (delta > default(double)) _life.Advance(delta);
            }
            finally { _synchronizing = false; }
        }
        private void SavePosition()
        {
            if (_actor.Root == null || !ReferenceEquals(_identity, _actor.CurrentActor)) return;
            SynchronizeClock();
            var position = _actor.Root.position;
            _life.SavePosition(position.x, position.y, position.z, _actor.Root.eulerAngles.y);
        }
        private void Apply(AiResidentState state)
        {
            if (_disposed || _applying) return;
            if (_actor.PodSequenceActive || _actor.LifeMotionOccupied || _life.Conversing || _life.InH) { CancelRunning(); _appliedAction = null; return; }
            _applying = true;
            bool resumeAfterFailure = false;
            try
            {
                var action = _life.DesiredAction;
                if (state.activity != null && action == state.activity.kind)
                { BeginActivity(state.activity); return; }
                CancelRunning();
                // Needs do not repeatedly launch/consume an unrequested trial action.
                if ((action == "eat" || action == "rest") && state.action == "normal" && state.lastActivityResult != null) action = "normal";
                if (_appliedAction == action) return;
                _actor.ApplyLifeAction(action, _player()); _appliedAction = action;
                string blocked = null;
                if (action == "eat") blocked = "空腹です。話しかけて食べ物を選ぶと、仮の地点で食事を実行できます。";
                if (action == "rest") blocked = "休息が必要です。話しかけて休息を頼むと、仮の地点で実行できます。回復量は未指定です。";
                if (action == "work") blocked = "作業地点が未指定です。作業要求を保存して待機しています。";
                if (action == "treatment") blocked = "病気のため待機しています。話しかけて治療できます。";
                _life.Block(blocked);
                _log?.Invoke("AI_LIFE_ACTION resident=" + state.id + " action=" + action + " reason=" + blocked);
            }
            catch (Exception error)
            {
                var id = _running; CancelRunning();
                if (id != null) _life.CancelActivity(error.GetBaseException().Message, id);
                else _life.Block(error.GetBaseException().Message);
                resumeAfterFailure = id != null;
            }
            finally
            {
                _applying = false;
                if (_resumePending) { _resumePending = false; Apply(_life.State); }
            }
            if (resumeAfterFailure) Apply(_life.State);
        }
        public void Dispose()
        {
            if (_disposed) return;
            _life.Changed -= Apply;
            CancelRunning();
            if (_navigation != null) { _navigation.Rebuilding -= NavigationRebuilding; _navigation.Rebuilt -= Ready; }
            try { SynchronizeClock(); } catch (Exception error) { _log?.Invoke("AI_LIFE_CLOCK_SAVE_ERROR " + error); }
            _disposed = true;
            _actor.LifeActionReady -= Ready; _actor.LifeDestinationReached -= SavePosition;
            try { SavePosition(); } catch (Exception error) { _log?.Invoke("AI_LIFE_POSITION_SAVE_ERROR " + error); }
        }
    }
}
