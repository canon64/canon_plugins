using BehaviorDesigner.Runtime.Tasks;

namespace AIProject;

[TaskCategory("")]
public class CanCauseHurt : AgentConditional
{
	public override TaskStatus OnUpdate()
	{
		if (base.Agent.CanCauseHurt)
		{
			return TaskStatus.Success;
		}
		return TaskStatus.Failure;
	}
}
