using System;
using AIProject.Definitions;
using AIProject.SaveData;
using BehaviorDesigner.Runtime.Tasks;
using IllusionUtility.GetUtility;
using Manager;
using UniRx;
using UnityEngine;

namespace AIProject;

[TaskCategory("")]
public class Eat : AgentAction
{
	private IDisposable _onActionPlayDisposable;

	private IDisposable _onEndActionDisposable;

	private IDisposable _onCompleteActionDisposable;

	public override void OnStart()
	{
		AgentActor agent = base.Agent;
		agent.EventKey = EventType.Eat;
		agent.CurrentPoint = agent.TargetInSightActionPoint;
		agent.SetActiveOnEquipedItem(active: false);
		agent.ChaControl.setAllLayerWeight(0f);
		agent.ElectNextPoint();
		agent.CurrentPoint.SetActiveMapItemObjs(active: false);
		StuffItem carryingItem = agent.AgentData.CarryingItem;
		ItemIDKeyPair[] canStandEatItems = Singleton<Manager.Resources>.Instance.AgentProfile.CanStandEatItems;
		bool flag = false;
		ItemIDKeyPair[] array = canStandEatItems;
		for (int i = 0; i < array.Length; i++)
		{
			ItemIDKeyPair itemIDKeyPair = array[i];
			if (carryingItem.CategoryID == itemIDKeyPair.categoryID && carryingItem.ID == itemIDKeyPair.itemID)
			{
				flag = true;
				break;
			}
		}
		ActionPointInfo outInfo;
		if (flag)
		{
			PoseKeyPair eatDeskID = Singleton<Manager.Resources>.Instance.AgentProfile.PoseIDTable.EatDeskID;
			PoseKeyPair eatChairID = Singleton<Manager.Resources>.Instance.AgentProfile.PoseIDTable.EatChairID;
			if (agent.TargetInSightActionPoint.FindAgentActionPointInfo(EventType.Eat, eatDeskID.poseID, out outInfo))
			{
				agent.Animation.ActionPointInfo = outInfo;
			}
			else if (agent.TargetInSightActionPoint.FindAgentActionPointInfo(EventType.Eat, eatChairID.poseID, out outInfo))
			{
				agent.Animation.ActionPointInfo = outInfo;
			}
		}
		else
		{
			PoseKeyPair eatDishID = Singleton<Manager.Resources>.Instance.AgentProfile.PoseIDTable.EatDishID;
			if (agent.TargetInSightActionPoint.FindAgentActionPointInfo(EventType.Eat, eatDishID.poseID, out outInfo))
			{
				agent.Animation.ActionPointInfo = outInfo;
			}
		}
		Transform t = agent.CurrentPoint.transform.FindLoop(outInfo.baseNullName)?.transform ?? agent.CurrentPoint.transform;
		GameObject gameObject = agent.CurrentPoint.transform.FindLoop(outInfo.recoveryNullName);
		agent.Animation.RecoveryPoint = gameObject?.transform;
		int eventID = outInfo.eventID;
		agent.ActionID = eventID;
		int num = eventID;
		eventID = outInfo.poseID;
		agent.PoseID = eventID;
		int num2 = eventID;
		PlayState playState = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[num][num2];
		ActorAnimInfo animInfo = agent.Animation.LoadActionState(num, num2, playState);
		if (Singleton<Manager.Resources>.Instance.Map.FoodEventItemList.TryGetValue(carryingItem.CategoryID, out var value) && value.TryGetValue(carryingItem.ID, out var value2) && Singleton<Manager.Resources>.Instance.Map.EventItemList.TryGetValue(value2, out var value3))
		{
			LocomotionProfile locomotionProfile = Singleton<Manager.Resources>.Instance.LocomotionProfile;
			string parentName = ((!flag) ? locomotionProfile.RootParentName : locomotionProfile.RightHandParentName);
			GameObject gameObject2 = agent.LoadEventItem(value2, parentName, isSync: false, value3);
			if (gameObject2 != null)
			{
				Renderer[] componentsInChildren = gameObject2.GetComponentsInChildren<Renderer>(includeInactive: true);
				Renderer[] array2 = componentsInChildren;
				foreach (Renderer renderer in array2)
				{
					renderer.enabled = true;
				}
			}
		}
		agent.LoadActionFlag(num, num2);
		agent.DeactivateNavMeshAgent();
		agent.Animation.StopAllAnimCoroutine();
		agent.Animation.PlayInAnimation(animInfo.inEnableBlend, animInfo.inBlendSec, playState.MainStateInfo.FadeOutTime, animInfo.layer);
		_onActionPlayDisposable = agent.AnimationAgent.OnEndActionAsObservable().Take(1).Subscribe(delegate
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
		agent.CurrentPoint.SetSlot(agent);
		agent.SetStand(t, playState.MainStateInfo.InStateInfo.EnableFade, playState.MainStateInfo.InStateInfo.FadeSecond, playState.DirectionType);
		if (animInfo.isLoop)
		{
			agent.SetCurrentSchedule(animInfo.isLoop, outInfo.actionName, animInfo.loopMinTime, animInfo.loopMaxTime, animInfo.hasAction, useGameTime: true);
		}
	}

	public override TaskStatus OnUpdate()
	{
		return base.Agent.AnimationAgent.OnUpdateActionState();
	}

	private void Complete()
	{
		AgentActor agent = base.Agent;
		agent.UpdateStatus(agent.ActionID, agent.PoseID);
		agent.CauseSick();
		int desireKey = Desire.GetDesireKey(Desire.Type.Eat);
		agent.SetDesire(desireKey, 0f);
		agent.ApplyFoodParameter(agent.AgentData.CarryingItem);
		agent.AgentData.CarryingItem = null;
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
			agent.CurrentPoint.ReleaseSlot(agent);
			agent.CurrentPoint = null;
		}
		agent.EventKey = (EventType)0;
		agent.PrevActionPoint = agent.TargetInSightActionPoint;
		agent.TargetInSightActionPoint = null;
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
		agent.ClearItems();
		agent.ClearParticles();
	}
}
