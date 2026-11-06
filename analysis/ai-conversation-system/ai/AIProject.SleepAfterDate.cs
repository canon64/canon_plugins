using AIProject.Definitions;
using BehaviorDesigner.Runtime.Tasks;
using UniRx;

namespace AIProject;

[TaskCategory("")]
public class SleepAfterDate : AgentAction
{
	private Subject<Unit> _endAction = new Subject<Unit>();

	private bool _missing;

	public override void OnStart()
	{
		base.OnStart();
		if (base.Agent.CurrentPoint == null)
		{
			base.Agent.CurrentPoint = base.Agent.TargetInSightActionPoint;
		}
		_missing = base.Agent.CurrentPoint == null;
	}

	public override TaskStatus OnUpdate()
	{
		if (_missing)
		{
			Complete();
			return TaskStatus.Success;
		}
		ActorAnimation animation = base.Agent.Animation;
		if (animation.PlayingActAnimation)
		{
			return TaskStatus.Running;
		}
		if (!base.Agent.Schedule.enabled)
		{
			if (_endAction != null)
			{
				_endAction.OnNext(Unit.Default);
			}
			if (animation.PlayingOutAnimation)
			{
				return TaskStatus.Running;
			}
			Complete();
			return TaskStatus.Success;
		}
		return TaskStatus.Running;
	}

	public override void OnEnd()
	{
		base.OnEnd();
	}

	private void Complete()
	{
		AgentActor agent = base.Agent;
		agent.UpdateStatus(agent.ActionID, agent.PoseID);
		int desireKey = Desire.GetDesireKey(Desire.Type.Sleep);
		agent.SetDesire(desireKey, 0f);
		agent.SetStatus(0, 50f);
		agent.HealSickBySleep();
		base.Agent.ActivateNavMeshAgent();
		base.Agent.SetActiveOnEquipedItem(active: true);
		agent.Animation.EndStates();
		agent.ClearItems();
		agent.ClearParticles();
		if (agent.CurrentPoint != null)
		{
			agent.CurrentPoint.SetActiveMapItemObjs(active: true);
			agent.CurrentPoint.ReleaseSlot(agent);
			agent.CurrentPoint = null;
		}
		agent.EventKey = (EventType)0;
		agent.PrevActionPoint = agent.TargetInSightActionPoint;
		agent.TargetInSightActionPoint = null;
	}
}
