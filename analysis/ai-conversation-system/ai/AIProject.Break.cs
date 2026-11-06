using AIProject.Definitions;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace AIProject;

[TaskCategory("")]
public class Break : AgentStateAction
{
	public override void OnStart()
	{
		base.Agent.EventKey = EventType.Break;
		base.OnStart();
	}

	protected override void OnCompletedStateTask()
	{
		if (!_unchangeParamState)
		{
			AgentActor agent = base.Agent;
			int desireKey = Desire.GetDesireKey(Desire.Type.Break);
			agent.SetDesire(desireKey, 0f);
			if (Random.Range(0, 20) < 1)
			{
				agent.AgentData.SickState.ID = -1;
			}
		}
	}
}
