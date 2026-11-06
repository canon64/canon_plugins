using System;
using System.Collections.Generic;
using AIProject.Definitions;
using BehaviorDesigner.Runtime.Tasks;
using Manager;
using UniRx;
using UnityEngine;

namespace AIProject;

[TaskCategory("")]
public class PlayGameThere : AgentAction
{
	[SerializeField]
	private State.Type _stateType;

	[SerializeField]
	private bool _unchangeParamState;

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
		List<PoseKeyPair> list = ListPool<PoseKeyPair>.Get();
		AgentProfile.PoseIDCollection poseIDTable = Singleton<Manager.Resources>.Instance.AgentProfile.PoseIDTable;
		list.AddRange(poseIDTable.PlayGameStandIDList);
		Weather weather = Singleton<Manager.Map>.Instance.Simulator.Weather;
		if (agent.AreaType == MapArea.AreaType.Normal)
		{
			list.AddRange(Singleton<Manager.Resources>.Instance.AgentProfile.PoseIDTable.PlayGameStandOutdoorIDList);
			if (weather == Weather.Cloud1 || weather == Weather.Cloud2)
			{
				list.Add(Singleton<Manager.Resources>.Instance.AgentProfile.PoseIDTable.ClearPoseID);
			}
		}
		PoseKeyPair element = list.GetElement(UnityEngine.Random.Range(0, list.Count));
		ListPool<PoseKeyPair>.Release(list);
		agent.ActionID = element.postureID;
		agent.ActionID = element.poseID;
		PlayState playState = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[element.postureID][element.poseID];
		agent.Animation.RecoveryPoint = null;
		ActorAnimInfo animInfo = agent.Animation.LoadActionState(element.postureID, element.poseID, playState);
		agent.LoadActionFlag(element.postureID, element.poseID);
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
			agent.SetCurrentSchedule(animInfo.isLoop, "その場で遊ぶ", animInfo.loopMinTime, animInfo.loopMaxTime, animInfo.hasAction, useGameTime: false);
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
		if (!agent.Animation.AnimInfo.outEnableBlend)
		{
			agent.Animation.CrossFadeScreen();
		}
		agent.Animation.RefsActAnimInfo = true;
		if (!_unchangeParamState)
		{
			agent.UpdateStatus(agent.ActionID, agent.PoseID);
			int desireKey = Desire.GetDesireKey(Desire.Type.Game);
			agent.SetDesire(desireKey, 0f);
			agent.CauseSick();
		}
		agent.ClearItems();
		agent.ClearParticles();
		agent.ResetActionFlag();
		agent.EventKey = (EventType)0;
		agent.Animation.EndStates();
	}
}
