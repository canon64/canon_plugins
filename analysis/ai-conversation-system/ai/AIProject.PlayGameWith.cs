using AIProject.Definitions;
using BehaviorDesigner.Runtime.Tasks;
using IllusionUtility.GetUtility;
using Manager;
using UniRx;
using UnityEngine;

namespace AIProject;

[TaskCategory("")]
public class PlayGameWith : AgentAction
{
	protected Subject<Unit> _onActionPlay = new Subject<Unit>();

	protected Subject<Unit> _onEndAction = new Subject<Unit>();

	public override void OnStart()
	{
		AgentActor agent = base.Agent;
		agent.EventKey = EventType.Play;
		agent.CurrentPoint = agent.TargetInSightActionPoint;
		agent.SetActiveOnEquipedItem(active: false);
		agent.ChaControl.setAllLayerWeight(0f);
		agent.ElectNextPoint();
		agent.CurrentPoint.SetActiveMapItemObjs(active: false);
		agent.CurrentPoint.TryGetAgentDateActionPointInfo(EventType.Play, out var outInfo);
		Transform t = agent.CurrentPoint.transform.FindLoop(outInfo.baseNullNameA)?.transform ?? agent.CurrentPoint.transform;
		Transform t2 = agent.CurrentPoint.transform.FindLoop(outInfo.baseNullNameB)?.transform ?? agent.CurrentPoint.transform;
		GameObject gameObject = agent.CurrentPoint.transform.FindLoop(outInfo.recoveryNullNameA);
		agent.Animation.RecoveryPoint = gameObject?.transform;
		GameObject gameObject2 = agent.CurrentPoint.transform.FindLoop(outInfo.recoveryNullNameB);
		agent.Animation.RecoveryPoint = gameObject2?.transform;
		int eventID = outInfo.eventID;
		agent.ActionID = eventID;
		int num = eventID;
		eventID = outInfo.poseIDA;
		agent.PoseID = eventID;
		int num2 = eventID;
		PlayState playState = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[num][num2];
		agent.Animation.LoadEventKeyTable(num, num2);
		agent.LoadEventItems(playState);
		agent.LoadEventParticles(num, num2);
		agent.Animation.InitializeStates(playState);
		AgentActor partner = agent.Partner as AgentActor;
		partner.BehaviorResources.ChangeMode(Desire.ActionType.Idle);
		partner.Mode = Desire.ActionType.Idle;
		partner.ActionID = num;
		eventID = outInfo.poseIDB;
		partner.PoseID = eventID;
		int num3 = eventID;
		partner.Animation.LoadEventKeyTable(num, num3);
		PlayState playState2 = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[num][num3];
		partner.LoadEventItems(playState2);
		partner.LoadEventParticles(num, num3);
		partner.Animation.InitializeStates(playState2);
		ActorAnimInfo actorAnimInfo = new ActorAnimInfo
		{
			inEnableBlend = playState.MainStateInfo.InStateInfo.EnableFade,
			inBlendSec = playState.MainStateInfo.InStateInfo.FadeSecond,
			inFadeOutTime = playState.MainStateInfo.FadeOutTime,
			outEnableBlend = playState.MainStateInfo.OutStateInfo.EnableFade,
			outBlendSec = playState.MainStateInfo.OutStateInfo.FadeSecond,
			directionType = playState.DirectionType,
			endEnableBlend = playState.EndEnableBlend,
			endBlendSec = playState.EndBlendRate,
			isLoop = playState.MainStateInfo.IsLoop,
			loopMinTime = playState.MainStateInfo.LoopMin,
			loopMaxTime = playState.MainStateInfo.LoopMax,
			hasAction = playState.ActionInfo.hasAction,
			loopStateName = playState.MainStateInfo.InStateInfo.StateInfos.GetElement(playState.MainStateInfo.InStateInfo.StateInfos.Length - 1).stateName,
			randomCount = playState.ActionInfo.randomCount,
			oldNormalizedTime = 0f,
			layer = playState.MainStateInfo.InStateInfo.StateInfos[0].layer
		};
		agent.Animation.AnimInfo = actorAnimInfo;
		ActorAnimInfo animInfo = actorAnimInfo;
		actorAnimInfo = default(ActorAnimInfo);
		actorAnimInfo.layer = playState2.Layer;
		actorAnimInfo.inEnableBlend = playState2.MainStateInfo.InStateInfo.EnableFade;
		actorAnimInfo.inBlendSec = playState2.MainStateInfo.InStateInfo.FadeSecond;
		actorAnimInfo.outEnableBlend = playState2.MainStateInfo.OutStateInfo.EnableFade;
		actorAnimInfo.outBlendSec = playState2.MainStateInfo.OutStateInfo.FadeSecond;
		actorAnimInfo.directionType = playState.DirectionType;
		actorAnimInfo.isLoop = playState.MainStateInfo.IsLoop;
		actorAnimInfo.endEnableBlend = playState.EndEnableBlend;
		actorAnimInfo.endBlendSec = playState.EndBlendRate;
		ActorAnimInfo actorAnimInfo2 = actorAnimInfo;
		partner.Animation.AnimInfo = actorAnimInfo2;
		ActorAnimInfo actorAnimInfo3 = actorAnimInfo2;
		agent.DisableActionFlag();
		partner.DisableActionFlag();
		agent.DeactivateNavMeshAgent();
		agent.IsKinematic = true;
		partner.SetActiveOnEquipedItem(active: false);
		partner.ChaControl.setAllLayerWeight(0f);
		partner.DeactivateNavMeshAgent();
		partner.IsKinematic = true;
		agent.CurrentPoint.SetSlot(agent);
		agent.Animation.StopAllAnimCoroutine();
		agent.Animation.PlayInAnimation(animInfo.inEnableBlend, animInfo.inBlendSec, playState.MainStateInfo.FadeOutTime, animInfo.layer);
		agent.SetStand(t, playState.MainStateInfo.InStateInfo.EnableFade, playState.MainStateInfo.InStateInfo.FadeSecond, animInfo.layer);
		partner.Animation.PlayInAnimation(actorAnimInfo3.inEnableBlend, actorAnimInfo3.inBlendSec, playState2.MainStateInfo.FadeOutTime, actorAnimInfo3.layer);
		partner.SetStand(t2, playState2.MainStateInfo.InStateInfo.EnableFade, playState2.MainStateInfo.InStateInfo.FadeSecond, actorAnimInfo3.layer);
		_onEndAction.Take(1).Subscribe(delegate
		{
			agent.Animation.PlayOutAnimation(animInfo.outEnableBlend, animInfo.outBlendSec, animInfo.layer);
			partner.Animation.PlayOutAnimation(animInfo.outEnableBlend, animInfo.outBlendSec, animInfo.layer);
		});
		if (animInfo.hasAction)
		{
			_onActionPlay.Subscribe(delegate
			{
				agent.Animation.PlayActionAnimation(animInfo.layer);
				partner.Animation.PlayActionAnimation(animInfo.layer);
			});
		}
		if (animInfo.isLoop)
		{
			agent.SetCurrentSchedule(animInfo.isLoop, outInfo.actionName, animInfo.loopMinTime, animInfo.loopMaxTime, animInfo.hasAction, useGameTime: false);
		}
	}

	public override TaskStatus OnUpdate()
	{
		if (base.Agent.Animation.PlayingInAnimation)
		{
			return TaskStatus.Running;
		}
		ActorAnimInfo animInfo = base.Agent.Animation.AnimInfo;
		if (animInfo.isLoop)
		{
			if (base.Agent.Animation.PlayingActAnimation)
			{
				return TaskStatus.Running;
			}
			if (!base.Agent.Schedule.enabled)
			{
				if (_onEndAction != null)
				{
					_onEndAction.OnNext(Unit.Default);
				}
				if (base.Agent.Animation.PlayingOutAnimation)
				{
					return TaskStatus.Running;
				}
				Complete();
				return TaskStatus.Success;
			}
			AnimatorStateInfo currentAnimatorStateInfo = base.Agent.Animation.Animator.GetCurrentAnimatorStateInfo(0);
			if (currentAnimatorStateInfo.IsName(animInfo.loopStateName))
			{
				float num = currentAnimatorStateInfo.normalizedTime - animInfo.oldNormalizedTime;
				if (num > 1f)
				{
					animInfo.oldNormalizedTime = currentAnimatorStateInfo.normalizedTime;
					if (Random.Range(0, animInfo.randomCount) == 0)
					{
						if (_onActionPlay != null)
						{
							_onActionPlay.OnNext(Unit.Default);
						}
						animInfo.oldNormalizedTime = 0f;
					}
				}
			}
			return TaskStatus.Running;
		}
		if (_onEndAction != null)
		{
			_onEndAction.OnNext(Unit.Default);
		}
		if (base.Agent.Animation.PlayingOutAnimation)
		{
			return TaskStatus.Running;
		}
		Complete();
		return TaskStatus.Success;
	}

	public override void OnEnd()
	{
		base.OnEnd();
		base.Agent.ActivateNavMeshAgent();
		base.Agent.SetActiveOnEquipedItem(active: true);
	}

	private void Complete()
	{
		AgentActor agent = base.Agent;
		AgentActor agentActor = agent.Partner as AgentActor;
		ActorAnimInfo animInfo = agent.Animation.AnimInfo;
		if (!animInfo.outEnableBlend)
		{
			agent.Animation.CrossFadeScreen();
		}
		agent.SetStand(agent.Animation.RecoveryPoint, animInfo.endEnableBlend, animInfo.endBlendSec, animInfo.directionType);
		agent.Animation.RefsActAnimInfo = true;
		agentActor.SetStand(agentActor.Animation.RecoveryPoint, animInfo.endEnableBlend, animInfo.endBlendSec, animInfo.directionType);
		agentActor.Animation.RefsActAnimInfo = true;
		agent.UpdateStatus(agent.ActionID, agent.PoseID);
		agentActor.UpdateStatus(agentActor.ActionID, agentActor.PoseID);
		agent.CauseSick();
		agent.ApplySituationResultParameter(28);
		agentActor.ApplySituationResultParameter(28);
		int desireKey = Desire.GetDesireKey(Desire.Type.Game);
		agent.SetDesire(desireKey, 0f);
		agentActor.SetDesire(desireKey, 0f);
		desireKey = Desire.GetDesireKey(Desire.Type.Lonely);
		agent.SetDesire(desireKey, 0f);
		agentActor.SetDesire(desireKey, 0f);
		agent.ClearItems();
		agent.ClearParticles();
		agent.ResetActionFlag();
		agentActor.ResetActionFlag();
		agent.SetDefaultStateHousingItem();
		agent.CurrentPoint.SetActiveMapItemObjs(active: true);
		agent.Partner = null;
		agentActor.Partner = null;
		agentActor.ActivateNavMeshAgent();
		agentActor.SetActiveOnEquipedItem(active: true);
		ActorAnimInfo animInfo2 = agentActor.Animation.AnimInfo;
		agentActor.BehaviorResources.ChangeMode(Desire.ActionType.Normal);
		agentActor.Mode = Desire.ActionType.Normal;
		agent.TargetInSightActor = null;
		agent.CurrentPoint.ReleaseSlot(base.Agent);
		agent.CurrentPoint = null;
		agent.EventKey = (EventType)0;
		agent.PrevActionPoint = base.Agent.TargetInSightActionPoint;
		agent.TargetInSightActionPoint = null;
	}
}
