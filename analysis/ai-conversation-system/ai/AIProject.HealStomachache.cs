using BehaviorDesigner.Runtime.Tasks;

namespace AIProject;

[TaskCategory("")]
public class HealStomachache : AgentAction
{
	public override TaskStatus OnUpdate()
	{
		if (base.Agent.AgentData.SickState.ID == 1)
		{
			base.Agent.AgentData.SickState.ID = -1;
		}
		return TaskStatus.Success;
	}
}
