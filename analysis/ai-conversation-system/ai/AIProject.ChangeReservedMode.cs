using BehaviorDesigner.Runtime.Tasks;

namespace AIProject;

[TaskCategory("")]
public class ChangeReservedMode : AgentAction
{
	public override TaskStatus OnUpdate()
	{
		base.Agent.ChangeReservedBehavior();
		return TaskStatus.Success;
	}
}
