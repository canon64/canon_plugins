using BehaviorDesigner.Runtime.Tasks;
using IllusionUtility.GetUtility;
using Manager;
using UnityEngine;

namespace AIProject;

[TaskCategory("")]
public class Coma : AgentAction
{
	public override void OnStart()
	{
		base.OnStart();
		AgentActor agent = base.Agent;
		if (agent.CurrentPoint == null)
		{
			agent.CurrentPoint = agent.TargetInSightActionPoint;
		}
		agent.DeactivateNavMeshAgent();
		PoseKeyPair comaID = Singleton<Manager.Resources>.Instance.AgentProfile.PoseIDTable.ComaID;
		PlayState playState = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[comaID.postureID][comaID.poseID];
		agent.Animation.LoadEventKeyTable(comaID.postureID, comaID.poseID);
		agent.Animation.InitializeStates(playState);
		agent.LoadActionFlag(comaID.postureID, comaID.poseID);
		ActorAnimInfo actorAnimInfo = default(ActorAnimInfo);
		actorAnimInfo.layer = playState.Layer;
		actorAnimInfo.inEnableBlend = playState.MainStateInfo.InStateInfo.EnableFade;
		actorAnimInfo.inBlendSec = playState.MainStateInfo.InStateInfo.FadeSecond;
		actorAnimInfo.outEnableBlend = playState.MainStateInfo.OutStateInfo.EnableFade;
		actorAnimInfo.outBlendSec = playState.MainStateInfo.OutStateInfo.FadeSecond;
		actorAnimInfo.directionType = playState.DirectionType;
		actorAnimInfo.endEnableBlend = playState.EndEnableBlend;
		actorAnimInfo.endBlendSec = playState.EndBlendRate;
		actorAnimInfo.isLoop = playState.MainStateInfo.IsLoop;
		actorAnimInfo.loopMinTime = playState.MainStateInfo.LoopMin;
		actorAnimInfo.loopMaxTime = playState.MainStateInfo.LoopMax;
		actorAnimInfo.hasAction = playState.ActionInfo.hasAction;
		actorAnimInfo.loopStateName = playState.MainStateInfo.InStateInfo.StateInfos.GetElement(playState.MainStateInfo.InStateInfo.StateInfos.Length - 1).stateName;
		actorAnimInfo.randomCount = playState.ActionInfo.randomCount;
		actorAnimInfo.oldNormalizedTime = 0f;
		ActorAnimInfo actorAnimInfo2 = actorAnimInfo;
		agent.Animation.AnimInfo = actorAnimInfo2;
		ActorAnimInfo actorAnimInfo3 = actorAnimInfo2;
		agent.Animation.PlayInAnimation(actorAnimInfo3.inEnableBlend, actorAnimInfo3.inBlendSec, actorAnimInfo3.inFadeOutTime, actorAnimInfo3.layer);
		agent.CurrentPoint.TryGetAgentActionPointInfo(EventType.Sleep, out var outInfo);
		Transform t = agent.CurrentPoint.transform.FindLoop(outInfo.baseNullName)?.transform ?? agent.CurrentPoint.transform;
		GameObject gameObject = agent.CurrentPoint.transform.FindLoop(outInfo.recoveryNullName);
		agent.Animation.RecoveryPoint = gameObject?.transform;
		agent.CurrentPoint.SetSlot(agent);
		agent.SetStand(t, playState.MainStateInfo.InStateInfo.EnableFade, playState.MainStateInfo.InStateInfo.FadeSecond, playState.DirectionType);
	}

	public override TaskStatus OnUpdate()
	{
		if (base.Agent.Animation.PlayingInAnimation)
		{
			return TaskStatus.Running;
		}
		return TaskStatus.Success;
	}

	public override void OnEnd()
	{
		base.OnEnd();
	}
}
