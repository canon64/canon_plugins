using System;
using System.Collections.Generic;
using AIProject.Definitions;
using AIProject.SaveData;
using BehaviorDesigner.Runtime.Tasks;
using IllusionUtility.GetUtility;
using Manager;
using UniRx;
using UnityEngine;

namespace AIProject;

[TaskCategory("")]
public class Steal : AgentAction
{
	[SerializeField]
	private State.Type _stateType;

	private IDisposable _onActionPlayDisposable;

	private IDisposable _onEndActionDisposable;

	private IDisposable _onCompleteActionDisposable;

	private bool _existsFoods;

	public override void OnStart()
	{
		AgentActor agent = base.Agent;
		agent.EventKey = EventType.Cook;
		Manager.Resources instance = Singleton<Manager.Resources>.Instance;
		Dictionary<int, Dictionary<int, Dictionary<int, FoodParameterPacket>>> foodParameterTable = instance.GameInfo.FoodParameterTable;
		List<StuffItem> itemListInPantry = Singleton<Game>.Instance.Environment.ItemListInPantry;
		List<StuffItem> list = ListPool<StuffItem>.Get();
		foreach (StuffItem item in itemListInPantry)
		{
			if (foodParameterTable.TryGetValue(item.CategoryID, out var value) && value.TryGetValue(item.ID, out var _))
			{
				_existsFoods = true;
				list.Add(item);
			}
		}
		if (!_existsFoods)
		{
			ListPool<StuffItem>.Release(list);
			return;
		}
		StuffItem element = list.GetElement(UnityEngine.Random.Range(0, list.Count));
		ListPool<StuffItem>.Release(list);
		if (element == null)
		{
			_existsFoods = false;
			return;
		}
		agent.AgentData.CarryingItem = new StuffItem(element.CategoryID, element.ID, 1);
		agent.CurrentPoint = agent.TargetInSightActionPoint;
		agent.SetActiveOnEquipedItem(active: false);
		agent.ChaControl.setAllLayerWeight(0f);
		agent.ElectNextPoint();
		agent.CurrentPoint.SetActiveMapItemObjs(active: false);
		PoseKeyPair snitchFoodID = instance.AgentProfile.PoseIDTable.SnitchFoodID;
		agent.ActionID = snitchFoodID.postureID;
		agent.PoseID = snitchFoodID.poseID;
		Transform t = agent.CurrentPoint.transform.FindLoop(instance.DefinePack.MapDefines.StealPivotName)?.transform ?? agent.CurrentPoint.transform;
		agent.Animation.RecoveryPoint = null;
		PlayState playState = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[snitchFoodID.postureID][snitchFoodID.poseID];
		ActorAnimInfo animInfo = agent.Animation.LoadActionState(snitchFoodID.postureID, snitchFoodID.poseID, playState);
		agent.LoadActionFlag(snitchFoodID.postureID, snitchFoodID.poseID);
		agent.DeactivateNavMeshAgent();
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
		agent.CurrentPoint.SetSlot(agent);
		agent.SetStand(t, playState.MainStateInfo.InStateInfo.EnableFade, playState.MainStateInfo.InStateInfo.FadeSecond, playState.DirectionType);
		if (animInfo.isLoop)
		{
			agent.SetCurrentSchedule(animInfo.isLoop, "盗み食い", animInfo.loopMinTime, animInfo.loopMaxTime, animInfo.hasAction, useGameTime: false);
		}
	}

	public override TaskStatus OnUpdate()
	{
		if (!_existsFoods)
		{
			return TaskStatus.Success;
		}
		return base.Agent.AnimationAgent.OnUpdateActionState();
	}

	public override void OnEnd()
	{
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
		agent.SetActiveOnEquipedItem(active: true);
		agent.ClearItems();
		agent.ClearParticles();
	}

	private void Complete()
	{
		AgentActor agent = base.Agent;
		agent.UpdateStatus(agent.ActionID, agent.PoseID);
		agent.CauseSick();
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
			agent.CurrentPoint.CreateByproduct(agent.ActionID, agent.PoseID);
			agent.CurrentPoint.ReleaseSlot(agent);
			agent.CurrentPoint = null;
		}
		agent.EventKey = (EventType)0;
		agent.PrevActionPoint = agent.TargetInSightActionPoint;
		agent.TargetInSightActionPoint = null;
	}
}
