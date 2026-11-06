using BehaviorDesigner.Runtime.Tasks;

namespace AIProject;

[TaskCategory("")]
public class Heal : AgentAction
{
	public override TaskStatus OnUpdate()
	{
		base.Agent.AgentData.SickState.ID = -1;
		base.Agent.AgentData.SickState.UsedMedicine = false;
		return TaskStatus.Success;
	}
}
