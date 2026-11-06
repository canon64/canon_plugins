using AIProject.Definitions;
using BehaviorDesigner.Runtime.Tasks;
using Manager;
using UnityEngine;

namespace AIProject;

[TaskCategory("")]
public class Commun : AgentAction
{
	private bool _updatedMotivation;

	public override void OnStart()
	{
		base.OnStart();
		AgentActor agent = base.Agent;
		agent.StateType = State.Type.Commun;
		agent.StopNavMeshAgent();
		agent.ChangeStaticNavMeshAgentAvoidance();
		PlayerActor player = Singleton<Manager.Map>.Instance.Player;
		Transform trfTarg = player.FovTargetPointTable[Actor.FovBodyPart.Head];
		agent.ChaControl.ChangeLookEyesTarget(1, trfTarg);
		agent.ChaControl.ChangeLookEyesPtn(1);
		agent.ChaControl.ChangeLookNeckTarget(1, trfTarg);
		agent.ChaControl.ChangeLookNeckPtn(1);
		agent.MotivationInEncounter = agent.AgentData.StatsTable[5];
		agent.UpdateMotivation = true;
		Debug.Log($"Encounter.Commun.OnStart UpdateMotivation=true proc: {Time.frameCount}");
	}

	public override TaskStatus OnUpdate()
	{
		AgentActor agent = base.Agent;
		if (agent.MotivationInEncounter <= 0f)
		{
			return TaskStatus.Success;
		}
		PlayerActor player = Singleton<Manager.Map>.Instance.Player;
		if (agent.ReleasableCommand && agent.IsFarPlayer)
		{
			return TaskStatus.Success;
		}
		return TaskStatus.Running;
	}

	public override void OnEnd()
	{
		base.OnEnd();
		AgentActor agent = base.Agent;
		agent.ChaControl.ChangeLookEyesPtn(0);
		agent.ChaControl.ChangeLookEyesTarget(0);
		agent.ChaControl.ChangeLookNeckPtn(3);
		agent.ChaControl.ChangeLookNeckTarget(0);
		agent.ChangeDynamicNavMeshAgentAvoidance();
		agent.UpdateMotivation = false;
	}

	public override void OnPause(bool paused)
	{
		AgentActor agent = base.Agent;
		if (paused)
		{
			_updatedMotivation = paused;
			agent.UpdateMotivation = false;
		}
		else
		{
			agent.UpdateMotivation = _updatedMotivation;
		}
	}
}
