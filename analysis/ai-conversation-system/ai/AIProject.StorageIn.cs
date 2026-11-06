using AIProject.Definitions;
using AIProject.SaveData;
using BehaviorDesigner.Runtime.Tasks;
using IllusionUtility.GetUtility;
using Manager;
using UniRx;
using UnityEngine;

namespace AIProject;

[TaskCategory("")]
public class StorageIn : AgentAction
{
	[SerializeField]
	private State.Type _stateType;

	private Subject<Unit> _onStartChestAction = new Subject<Unit>();

	private Subject<Unit> _onStartAction = new Subject<Unit>();

	protected Subject<Unit> _onActionPlay = new Subject<Unit>();

	protected Subject<Unit> _onEndAction = new Subject<Unit>();

	private Subject<Unit> _onEndChestAction = new Subject<Unit>();

	private ChestAnimation _chestAnimation;

	public override void OnStart()
	{
		AgentActor agent = base.Agent;
		agent.EventKey = EventType.StorageIn;
		agent.CurrentPoint = agent.TargetInSightActionPoint;
		agent.SetActiveOnEquipedItem(active: false);
		agent.ChaControl.setAllLayerWeight(0f);
		agent.ElectNextPoint();
		AIProject.Definitions.Action.NameTable.TryGetValue(agent.EventKey, out var value);
		agent.ActionID = value.Item1;
		ActionPointInfo actionPointInfo = agent.TargetInSightActionPoint.GetActionPointInfo(agent);
		agent.Animation.ActionPointInfo = actionPointInfo;
		ActionPointInfo actionPointInfo2 = actionPointInfo;
		Transform t = agent.CurrentPoint.transform.FindLoop(actionPointInfo2.baseNullName)?.transform ?? agent.CurrentPoint.transform;
		GameObject gameObject = agent.CurrentPoint.transform.FindLoop(actionPointInfo2.recoveryNullName);
		agent.Animation.RecoveryPoint = gameObject?.transform;
		agent.PoseID = actionPointInfo2.poseID;
		PlayState playState = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[agent.ActionID][actionPointInfo2.poseID];
		agent.Animation.LoadEventKeyTable(agent.ActionID, actionPointInfo2.poseID);
		agent.LoadEventItems(playState);
		agent.LoadEventParticles(agent.ActionID, actionPointInfo2.poseID);
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
		Manager.Resources instance = Singleton<Manager.Resources>.Instance;
		agent.LoadActionFlag(actionPointInfo2.eventID, actionPointInfo2.poseID);
		agent.DeactivateNavMeshAgent();
		_onStartChestAction.Take(1).Subscribe(delegate
		{
			if (_chestAnimation != null)
			{
				_chestAnimation.PlayInAnimation();
			}
		});
		_onStartAction.Take(1).Subscribe(delegate
		{
			agent.Animation.PlayInAnimation(animInfo.inEnableBlend, animInfo.inBlendSec, animInfo.inFadeOutTime, animInfo.layer);
		});
		_onEndAction.Take(1).Subscribe(delegate
		{
			agent.Animation.StopAllAnimCoroutine();
			agent.Animation.PlayOutAnimation(animInfo.outEnableBlend, animInfo.outBlendSec, animInfo.layer);
		});
		if (animInfo.hasAction)
		{
			_onActionPlay.Take(1).Subscribe(delegate
			{
				agent.Animation.PlayActionAnimation(animInfo.layer);
			});
		}
		agent.CurrentPoint.SetSlot(agent);
		agent.SetStand(t, playState.MainStateInfo.InStateInfo.EnableFade, playState.MainStateInfo.InStateInfo.FadeSecond, playState.DirectionType);
		if (animInfo.isLoop)
		{
			base.Agent.SetCurrentSchedule(animInfo.isLoop, value.Item2, animInfo.loopMinTime, animInfo.loopMaxTime, animInfo.hasAction, useGameTime: false);
		}
		if (base.Agent.CurrentPoint != null)
		{
			_chestAnimation = base.Agent.CurrentPoint.GetComponent<ChestAnimation>();
		}
	}

	public override TaskStatus OnUpdate()
	{
		if (_chestAnimation != null && _chestAnimation.PlayingOutAnimation)
		{
			return TaskStatus.Running;
		}
		if (_onStartChestAction != null)
		{
			_onStartChestAction.OnNext(Unit.Default);
		}
		if (_chestAnimation != null && _chestAnimation.PlayingInAniamtion)
		{
			return TaskStatus.Running;
		}
		if (_onStartAction != null)
		{
			_onStartAction.OnNext(Unit.Default);
		}
		if (base.Agent.Animation.PlayingInAnimation)
		{
			return TaskStatus.Running;
		}
		if (base.Agent.Animation.AnimInfo.isLoop)
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

	private void Complete()
	{
		AgentActor agent = base.Agent;
		agent.UpdateStatus(agent.ActionID, agent.PoseID);
		agent.CauseSick();
		OnCompletedStateTask();
		base.Agent.ActivateNavMeshAgent();
		base.Agent.SetActiveOnEquipedItem(active: true);
		agent.Animation.EndStates();
		agent.ClearItems();
		agent.ClearParticles();
		agent.ResetActionFlag();
		agent.SetDefaultStateHousingItem();
		if (agent.CurrentPoint != null)
		{
			agent.CurrentPoint.ReleaseSlot(agent);
			agent.CurrentPoint = null;
		}
		agent.EventKey = (EventType)0;
		agent.PrevActionPoint = agent.TargetInSightActionPoint;
		agent.TargetInSightActionPoint = null;
	}

	private void OnCompletedStateTask()
	{
		if (_chestAnimation != null)
		{
			_chestAnimation.PlayOutAnimation();
		}
		AgentActor agent = base.Agent;
		foreach (StuffItem item in agent.AgentData.ItemList)
		{
			Singleton<Game>.Instance.WorldData.Environment.ItemListInStorage.AddItem(item);
		}
		agent.AgentData.ItemList.Clear();
	}

	public override void OnEnd()
	{
		base.OnEnd();
	}
}
