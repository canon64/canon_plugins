using BehaviorDesigner.Runtime.Tasks;

namespace AIProject;

[TaskCategory("")]
public class ElapsedDaysOfSick : AgentConditional
{
	public override TaskStatus OnUpdate()
	{
		if (base.Agent.AgentData.SickState.ElapsedTime > base.Agent.AgentData.SickState.Duration)
		{
			return TaskStatus.Success;
		}
		return TaskStatus.Failure;
	}
}
