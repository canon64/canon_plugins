using BehaviorDesigner.Runtime.Tasks;

namespace AIProject;

[TaskCategory("")]
public class GetStomachache : AgentAction
{
	public override TaskStatus OnUpdate()
	{
		base.Agent.AgentData.SickState.ID = 1;
		return TaskStatus.Success;
	}
}
