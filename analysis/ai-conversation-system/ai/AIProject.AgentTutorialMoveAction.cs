using System;
using AIProject.Definitions;
using BehaviorDesigner.Runtime.Tasks;
using IllusionUtility.GetUtility;
using Manager;
using UniRx;
using UnityEngine;

namespace AIProject;

public abstract class AgentTutorialMoveAction : AgentAction
{
	[SerializeField]
	private State.Type _stateType;

	private IDisposable _onActionPlayDisposable;

	private IDisposable _onEndActionDisposable;

	private IDisposable _onCompleteActionDisposable;

	protected PoseKeyPair _actionMotion = new PoseKeyPair
	{
		postureID = -1,
		poseID = -1
	};

	public override void OnStart()
	{
		base.OnStart();
		AgentActor agent = base.Agent;
		agent.CurrentPoint = agent.TargetInSightActionPoint;
		agent.SetActiveOnEquipedItem(active: false);
		agent.ChaControl.setAllLayerWeight(0f);
		agent.ElectNextPoint();
		agent.CurrentPoint.SetActiveMapItemObjs(active: false);
		ActionPointInfo actionPointInfo = agent.TargetInSightActionPoint.GetActionPointInfo(agent);
		agent.Animation.ActionPointInfo = actionPointInfo;
		ActionPointInfo actionPointInfo2 = actionPointInfo;
		Transform t = agent.CurrentPoint.transform.FindLoop(actionPointInfo2.baseNullName)?.transform ?? agent.CurrentPoint.transform;
		GameObject gameObject = agent.CurrentPoint.transform.FindLoop(actionPointInfo2.recoveryNullName);
		agent.Animation.RecoveryPoint = gameObject?.transform;
		int num = 0;
		int num2 = 0;
		if (_actionMotion.postureID < 0 || _actionMotion.poseID < 0)
		{
			num = actionPointInfo2.eventID;
			num2 = actionPointInfo2.poseID;
		}
		else
		{
			num = _actionMotion.postureID;
			num2 = _actionMotion.poseID;
		}
		agent.ActionID = num;
		agent.PoseID = num2;
		PlayState playState = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[num][num2];
		ActorAnimInfo animInfo = agent.Animation.LoadActionState(num, num2, playState);
		agent.LoadActionFlag(num, num2);
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
		agent.ActivateNavMeshAgent();
		agent.SetActiveOnEquipedItem(active: true);
		agent.ClearItems();
		agent.ClearParticles();
	}

	private void Complete()
	{
		AgentActor agent = base.Agent;
		agent.Animation.EndStates();
		agent.UpdateStatus(agent.ActionID, agent.PoseID);
		agent.CauseSick();
		OnCompletedStateTask();
		agent.ClearItems();
		agent.ClearParticles();
		agent.ResetActionFlag();
		agent.CurrentPoint.SetActiveMapItemObjs(active: true);
		agent.CurrentPoint.ReleaseSlot(agent);
		agent.CurrentPoint = null;
		agent.EventKey = (EventType)0;
		agent.PrevActionPoint = agent.TargetInSightActionPoint;
		agent.TargetInSightActionPoint = null;
	}

	protected virtual void OnCompletedStateTask()
	{
	}
}
