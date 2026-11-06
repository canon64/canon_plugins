using BehaviorDesigner.Runtime.Tasks;

namespace AIProject;

[TaskCategory("")]
public class ReceiveTalk : AgentAction
{
	public override void OnStart()
	{
		base.OnStart();
	}

	public override TaskStatus OnUpdate()
	{
		if (base.Agent.CommandPartner is AgentActor)
		{
			AgentActor agentActor = base.Agent.CommandPartner as AgentActor;
			if (agentActor.LivesTalkSequence)
			{
				return TaskStatus.Running;
			}
			return TaskStatus.Success;
		}
		return TaskStatus.Failure;
	}
}
