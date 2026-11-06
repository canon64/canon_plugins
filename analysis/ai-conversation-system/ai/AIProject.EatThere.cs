using System;
using AIProject.Definitions;
using AIProject.SaveData;
using BehaviorDesigner.Runtime.Tasks;
using Manager;
using UniRx;
using UnityEngine;

namespace AIProject;

[TaskCategory("")]
public class EatThere : AgentAction
{
	[SerializeField]
	private State.Type _stateType;

	private IDisposable _onActionPlayDisposable;

	private IDisposable _onEndActionDisposable;

	private IDisposable _onCompleteActionDisposable;

	public override void OnStart()
	{
		AgentActor agent = base.Agent;
		agent.EventKey = EventType.Play;
		base.OnStart();
		agent.SetActiveOnEquipedItem(active: false);
		agent.ChaControl.setAllLayerWeight(0f);
		agent.ElectNextPoint();
		PoseKeyPair eatStandID = Singleton<Manager.Resources>.Instance.AgentProfile.PoseIDTable.EatStandID;
		agent.ActionID = eatStandID.postureID;
		agent.PoseID = eatStandID.poseID;
		PlayState playState = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[eatStandID.postureID][eatStandID.poseID];
		ActorAnimInfo animInfo = agent.Animation.LoadActionState(eatStandID.postureID, eatStandID.poseID, playState);
		StuffItem carryingItem = agent.AgentData.CarryingItem;
		if (Singleton<Manager.Resources>.Instance.Map.FoodEventItemList.TryGetValue(carryingItem.CategoryID, out var value) && value.TryGetValue(carryingItem.ID, out var value2) && Singleton<Manager.Resources>.Instance.Map.EventItemList.TryGetValue(value2, out var value3))
		{
			string rightHandParentName = Singleton<Manager.Resources>.Instance.LocomotionProfile.RightHandParentName;
			GameObject gameObject = agent.LoadEventItem(value2, rightHandParentName, isSync: false, value3);
			if (gameObject != null)
			{
				Renderer[] componentsInChildren = gameObject.GetComponentsInChildren<Renderer>(includeInactive: true);
				Renderer[] array = componentsInChildren;
				foreach (Renderer renderer in array)
				{
					renderer.enabled = true;
				}
			}
		}
		agent.LoadActionFlag(eatStandID.postureID, eatStandID.poseID);
		agent.StopNavMeshAgent();
		agent.ChangeStaticNavMeshAgentAvoidance();
		agent.Animation.StopAllAnimCoroutine();
		agent.Animation.PlayInAnimation(animInfo.inEnableBlend, animInfo.inBlendSec, playState.MainStateInfo.FadeOutTime, animInfo.layer);
		_onEndActionDisposable = agent.AnimationAgent.OnEndActionAsObservable().Take(1).Subscribe(delegate
		{
			agent.Animation.StopAllAnimCoroutine();
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
			agent.SetCurrentSchedule(animInfo.isLoop, "その場で食べる", animInfo.loopMinTime, animInfo.loopMaxTime, animInfo.hasAction, useGameTime: false);
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
		int desireKey = Desire.GetDesireKey(Desire.Type.Eat);
		agent.SetDesire(desireKey, 0f);
		if (!agent.Animation.AnimInfo.outEnableBlend)
		{
			agent.Animation.CrossFadeScreen();
		}
		agent.Animation.RefsActAnimInfo = true;
		agent.UpdateStatus(agent.ActionID, agent.PoseID);
		agent.CauseSick();
		agent.ClearItems();
		agent.ClearParticles();
		agent.ResetActionFlag();
		agent.EventKey = (EventType)0;
		agent.ApplyFoodParameter(agent.AgentData.CarryingItem);
		agent.AgentData.CarryingItem = null;
	}
}
