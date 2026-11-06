using BehaviorDesigner.Runtime.Tasks;

namespace AIProject;

[TaskCategory("")]
public class HasSick : AgentConditional
{
	public override TaskStatus OnUpdate()
	{
		if (base.Agent.AgentData.SickState.ID == -1)
		{
			return TaskStatus.Failure;
		}
		return TaskStatus.Success;
	}
}
