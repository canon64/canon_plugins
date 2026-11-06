using BehaviorDesigner.Runtime.Tasks;

namespace AIProject;

[TaskCategory("")]
public class IsHurt : AgentConditional
{
	public override TaskStatus OnUpdate()
	{
		if (base.Agent.AgentData.SickState.ID == 4)
		{
			return TaskStatus.Success;
		}
		return TaskStatus.Failure;
	}
}
