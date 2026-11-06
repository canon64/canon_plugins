using System;
using AIProject.Definitions;
using AIProject.SaveData;
using BehaviorDesigner.Runtime.Tasks;
using Manager;
using UniRx;
using UnityEngine;

namespace AIProject;

[TaskCategory("")]
public class DrinkThere : AgentAction
{
	[SerializeField]
	private State.Type _stateType;

	private StuffItem _targetItem;

	private IDisposable _onActionPlayDisposable;

	private IDisposable _onEndActionDisposable;

	private IDisposable _onCompleteActionDisposable;

	public override void OnStart()
	{
		AgentActor agent = base.Agent;
		agent.EventKey = EventType.Drink;
		_targetItem = agent.SelectDrinkItem();
		base.OnStart();
		agent.SetActiveOnEquipedItem(active: false);
		agent.ChaControl.setAllLayerWeight(0f);
		agent.ElectNextPoint();
		PoseKeyPair drinkStandID = Singleton<Manager.Resources>.Instance.AgentProfile.PoseIDTable.DrinkStandID;
		agent.ActionID = drinkStandID.postureID;
		agent.PoseID = drinkStandID.poseID;
		PlayState playState = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[drinkStandID.postureID][drinkStandID.poseID];
		ActorAnimInfo animInfo = agent.Animation.LoadActionState(drinkStandID.postureID, drinkStandID.poseID, playState);
		agent.LoadActionFlag(drinkStandID.postureID, drinkStandID.poseID);
		agent.StopNavMeshAgent();
		agent.ChangeStaticNavMeshAgentAvoidance();
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
		if (animInfo.isLoop)
		{
			agent.SetCurrentSchedule(animInfo.isLoop, "その場で飲む", animInfo.loopMinTime, animInfo.loopMaxTime, animInfo.hasAction, useGameTime: false);
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
		agent.ChangeDynamicNavMeshAgentAvoidance();
		agent.SetActiveOnEquipedItem(active: true);
		agent.ClearItems();
		agent.ClearParticles();
	}

	private void Complete()
	{
		AgentActor agent = base.Agent;
		int desireKey = Desire.GetDesireKey(Desire.Type.Drink);
		agent.SetDesire(desireKey, 0f);
		agent.Animation.EndStates();
		agent.UpdateStatus(agent.ActionID, agent.PoseID);
		agent.CauseSick();
		agent.ClearItems();
		agent.ClearParticles();
		agent.ResetActionFlag();
		agent.EventKey = (EventType)0;
		agent.ApplyDrinkParameter(_targetItem);
		_targetItem = null;
	}
}
