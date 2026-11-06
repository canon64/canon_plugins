using System;
using AIProject.Definitions;
using BehaviorDesigner.Runtime.Tasks;
using IllusionUtility.GetUtility;
using Manager;
using UniRx;
using UnityEngine;

namespace AIProject;

[TaskCategory("")]
public class Sleep : AgentAction
{
	private IDisposable _onActionPlayDisposable;

	private IDisposable _onEndActionDisposable;

	private IDisposable _onCompleteActionDisposable;

	public override void OnStart()
	{
		AgentActor agent = base.Agent;
		agent.EventKey = EventType.Sleep;
		agent.CurrentPoint = agent.TargetInSightActionPoint;
		agent.SetActiveOnEquipedItem(active: false);
		agent.ChaControl.setAllLayerWeight(0f);
		agent.ElectNextPoint();
		if (agent.CurrentPoint == null)
		{
			return;
		}
		agent.CurrentPoint.SetActiveMapItemObjs(active: false);
		ActionPointInfo actionPointInfo = agent.TargetInSightActionPoint.GetActionPointInfo(agent);
		agent.Animation.ActionPointInfo = actionPointInfo;
		ActionPointInfo actionPointInfo2 = actionPointInfo;
		Transform t = agent.CurrentPoint.transform.FindLoop(actionPointInfo2.baseNullName)?.transform ?? agent.CurrentPoint.transform;
		GameObject gameObject = agent.CurrentPoint.transform.FindLoop(actionPointInfo2.recoveryNullName);
		agent.Animation.RecoveryPoint = gameObject?.transform;
		int eventID = actionPointInfo2.eventID;
		agent.ActionID = eventID;
		int num = eventID;
		eventID = actionPointInfo2.poseID;
		agent.PoseID = eventID;
		int num2 = eventID;
		PlayState playState = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[num][num2];
		if (AIProject.Definitions.Action.NameTable.TryGetValue(agent.EventKey, out var value))
		{
			int item = value.Item1;
			if (item != agent.ActionID)
			{
				return;
			}
		}
		ActorAnimInfo animInfo = agent.Animation.LoadActionState(num, num2, playState);
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
			agent.SetCurrentSchedule(animInfo.isLoop, actionPointInfo2.actionName, animInfo.loopMinTime, animInfo.loopMaxTime, animInfo.hasAction, useGameTime: true);
			if (agent.AgentData.ScheduleEnabled)
			{
				Actor.BehaviorSchedule schedule = agent.Schedule;
				schedule.enabled = agent.AgentData.ScheduleEnabled;
				schedule.elapsedTime = agent.AgentData.ScheduleElapsedTime;
				schedule.duration = agent.AgentData.ScheduleDuration;
				agent.Schedule = schedule;
				agent.AgentData.ScheduleEnabled = false;
			}
		}
		MapUIContainer.AddSystemLog($"{MapUIContainer.CharaNameColor(agent)}が寝ました", isNotify: true);
	}

	public override TaskStatus OnUpdate()
	{
		AgentActor agent = base.Agent;
		if (AIProject.Definitions.Action.NameTable.TryGetValue(agent.EventKey, out var value))
		{
			int item = value.Item1;
			if (item != agent.ActionID)
			{
				agent.ChangeBehavior(Desire.ActionType.Normal);
				return TaskStatus.Failure;
			}
		}
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
		agent.ClearItems();
		agent.ClearParticles();
	}

	private void Complete()
	{
		AgentActor agent = base.Agent;
		agent.UpdateStatus(agent.ActionID, agent.PoseID);
		int desireKey = Desire.GetDesireKey(Desire.Type.Sleep);
		agent.SetDesire(desireKey, 0f);
		OnCompletedStateTask();
		int num = UnityEngine.Random.Range(0, 100);
		if (num < 50)
		{
			agent.ChangeBehavior(Desire.ActionType.EndTaskSecondSleep);
			return;
		}
		agent.ActivateNavMeshAgent();
		agent.SetActiveOnEquipedItem(active: true);
		agent.ClearItems();
		agent.ClearParticles();
		agent.Animation.EndStates();
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
		agent.AgentData.YobaiTrigger = false;
	}

	protected void OnCompletedStateTask()
	{
		AgentActor agent = base.Agent;
		agent.SetStatus(0, 50f);
		agent.SetDefaultImmoral();
		agent.HealSickBySleep();
	}
}
