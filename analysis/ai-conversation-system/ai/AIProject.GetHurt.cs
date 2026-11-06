using System;
using AIProject.Definitions;
using BehaviorDesigner.Runtime.Tasks;
using Manager;
using UnityEngine;

namespace AIProject;

[TaskCategory("")]
public class GetHurt : AgentAction
{
	private int _layer = -1;

	private bool _inEnableFade;

	private float _inFadeSecond;

	public override void OnStart()
	{
		base.OnStart();
		AgentActor agent = base.Agent;
		if (agent.AgentData.SickState.ID != -1)
		{
			agent.StateType = State.Type.Immobility;
			agent.StopNavMeshAgent();
			agent.ChangeStaticNavMeshAgentAvoidance();
			PoseKeyPair standHurtID = Singleton<Manager.Resources>.Instance.AgentProfile.PoseIDTable.StandHurtID;
			PlayState playState = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[standHurtID.postureID][standHurtID.poseID];
			agent.Animation.LoadEventKeyTable(standHurtID.postureID, standHurtID.poseID);
			_layer = playState.Layer;
			_inEnableFade = playState.MainStateInfo.InStateInfo.EnableFade;
			_inFadeSecond = playState.MainStateInfo.InStateInfo.FadeSecond;
			agent.Animation.InitializeStates(playState);
			agent.Animation.PlayInAnimation(_inEnableFade, _inFadeSecond, playState.MainStateInfo.FadeOutTime, _layer);
		}
	}

	public override TaskStatus OnUpdate()
	{
		AgentActor agent = base.Agent;
		if (agent.AgentData.SickState.ID == -1)
		{
			return TaskStatus.Success;
		}
		if (agent.Animation.PlayingInAnimation)
		{
			return TaskStatus.Running;
		}
		agent.AgentData.SickState.ID = 4;
		int num = UnityEngine.Random.Range(0, (int)TimeSpan.FromDays(2.0).TotalHours);
		agent.AgentData.SickState.Duration = agent.AgentData.SickState.ElapsedTime + TimeSpan.FromDays(2.0) + TimeSpan.FromHours(num);
		return TaskStatus.Success;
	}

	public override void OnEnd()
	{
		base.Agent.StateType = State.Type.Normal;
		base.Agent.ChangeDynamicNavMeshAgentAvoidance();
	}
}
