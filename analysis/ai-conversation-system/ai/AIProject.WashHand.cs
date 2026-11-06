using System;
using BehaviorDesigner.Runtime.Tasks;
using IllusionUtility.GetUtility;
using Manager;
using UniRx;
using UnityEngine;

namespace AIProject;

[TaskCategory("")]
public class WashHand : AgentAction
{
	private IDisposable _onActionPlayDisposable;

	private IDisposable _onEndActionDisposable;

	private IDisposable _onCompleteActionDisposable;

	public override void OnStart()
	{
		AgentActor agent = base.Agent;
		agent.EventKey = EventType.Wash;
		agent.CurrentPoint = agent.TargetInSightActionPoint;
		agent.SetActiveOnEquipedItem(active: false);
		agent.ChaControl.setAllLayerWeight(0f);
		agent.ElectNextPoint();
		Manager.Resources instance = Singleton<Manager.Resources>.Instance;
		PoseKeyPair handWash = instance.AgentProfile.PoseIDTable.HandWash;
		ActionPointInfo actionPointInfo = agent.TargetInSightActionPoint.GetActionPointInfo(agent);
		agent.Animation.ActionPointInfo = actionPointInfo;
		ActionPointInfo actionPointInfo2 = actionPointInfo;
		Transform t = agent.CurrentPoint.transform.FindLoop(actionPointInfo2.baseNullName)?.transform ?? agent.CurrentPoint.transform;
		GameObject gameObject = agent.CurrentPoint.transform.FindLoop(actionPointInfo2.recoveryNullName);
		agent.Animation.RecoveryPoint = gameObject?.transform;
		agent.ActionID = handWash.postureID;
		agent.PoseID = handWash.poseID;
		PlayState playState = instance.Animation.AgentActionAnimTable[agent.ActionID][agent.PoseID];
		agent.Animation.LoadEventKeyTable(agent.ActionID, agent.PoseID);
		agent.LoadEventItems(playState);
		agent.LoadEventParticles(agent.ActionID, agent.PoseID);
		agent.Animation.InitializeStates(playState);
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
			layer = playState.MainStateInfo.InStateInfo.StateInfos[0].layer
		};
		agent.Animation.AnimInfo = actorAnimInfo;
		ActorAnimInfo animInfo = actorAnimInfo;
		agent.LoadActionFlag(agent.ActionID, agent.PoseID);
		agent.DeactivateNavMeshAgent();
		agent.Animation.StopAllAnimCoroutine();
		agent.Animation.PlayInAnimation(animInfo.inEnableBlend, animInfo.inBlendSec, playState.MainStateInfo.FadeOutTime, animInfo.layer);
		_onEndActionDisposable = agent.AnimationAgent.OnEndActionAsObservable().Take(1).Subscribe(delegate
		{
			agent.Animation.PlayOutAnimation(animInfo.outEnableBlend, animInfo.outBlendSec, animInfo.layer);
		});
		if (animInfo.hasAction)
		{
			_onActionPlayDisposable = agent.AnimationAgent.OnActionPlayAsObservable().Subscribe(delegate
			{
				agent.Animation.PlayActionAnimation(animInfo.layer);
			});
		}
		_onCompleteActionDisposable = agent.AnimationAgent.OnCompleteActionAsObservable().Subscribe(delegate
		{
			Complete();
		});
		agent.CurrentPoint.SetSlot(agent);
		agent.SetStand(t, playState.MainStateInfo.InStateInfo.EnableFade, playState.MainStateInfo.InStateInfo.FadeSecond, playState.DirectionType);
		if (animInfo.isLoop)
		{
			agent.SetCurrentSchedule(animInfo.isLoop, actionPointInfo2.actionName, animInfo.loopMinTime, animInfo.loopMaxTime, animInfo.hasAction, useGameTime: false);
		}
	}

	public override TaskStatus OnUpdate()
	{
		return base.Agent.AnimationAgent.OnUpdateActionState();
	}

	public override void OnEnd()
	{
		base.OnEnd();
		if (_onEndActionDisposable != null)
		{
			_onEndActionDisposable.Dispose();
		}
		if (_onActionPlayDisposable != null)
		{
			_onActionPlayDisposable.Dispose();
		}
		if (_onCompleteActionDisposable != null)
		{
			_onCompleteActionDisposable.Dispose();
		}
		AgentActor agent = base.Agent;
	}

	protected virtual void Complete()
	{
		AgentActor agent = base.Agent;
		agent.UpdateStatus(agent.ActionID, agent.PoseID);
		agent.CauseSick();
		agent.ActivateNavMeshAgent();
		agent.SetActiveOnEquipedItem(active: true);
		agent.Animation.EndStates();
		agent.ClearItems();
		agent.ClearParticles();
		agent.ResetActionFlag();
		agent.SetDefaultStateHousingItem();
		if (agent.CurrentPoint != null)
		{
			agent.CurrentPoint.SetActiveMapItemObjs(active: true);
			agent.CurrentPoint.CreateByproduct(agent.ActionID, agent.PoseID);
			agent.CurrentPoint.ReleaseSlot(agent);
			agent.CurrentPoint = null;
		}
		agent.EventKey = (EventType)0;
		agent.PrevActionPoint = agent.TargetInSightActionPoint;
		agent.TargetInSightActionPoint = null;
	}
}
