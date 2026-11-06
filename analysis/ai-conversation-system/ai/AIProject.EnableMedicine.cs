using BehaviorDesigner.Runtime.Tasks;

namespace AIProject;

[TaskCategory("")]
public class EnableMedicine : AgentConditional
{
	public override TaskStatus OnUpdate()
	{
		if (base.Agent.AgentData.SickState.Enabled)
		{
			return TaskStatus.Success;
		}
		return TaskStatus.Failure;
	}
}
