using BehaviorDesigner.Runtime.Tasks;

namespace AIProject;

[TaskCategory("")]
public class RecoverPoint : AgentAction
{
	public override TaskStatus OnUpdate()
	{
		AgentActor agent = base.Agent;
		if (agent.CurrentPoint != null)
		{
			agent.CurrentPoint.SetActiveMapItemObjs(active: true);
			agent.CurrentPoint.ReleaseSlot(agent);
			agent.CurrentPoint = null;
		}
		agent.ActivateNavMeshAgent();
		agent.Animation.EndStates();
		agent.SetActiveOnEquipedItem(active: true);
		return TaskStatus.Success;
	}
}
