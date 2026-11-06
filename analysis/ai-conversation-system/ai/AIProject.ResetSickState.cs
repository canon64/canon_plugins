using AIProject.SaveData;
using BehaviorDesigner.Runtime.Tasks;

namespace AIProject;

[TaskCategory("")]
public class ResetSickState : AgentAction
{
	public override TaskStatus OnUpdate()
	{
		AgentActor agent = base.Agent;
		Sickness sickState = agent.AgentData.SickState;
		if (sickState.ID == 0)
		{
			agent.AgentData.ColdLockInfo.Lock = true;
			if (sickState.Enabled)
			{
				agent.SetStatus(0, 50f);
			}
		}
		sickState.ID = -1;
		return TaskStatus.Success;
	}
}
