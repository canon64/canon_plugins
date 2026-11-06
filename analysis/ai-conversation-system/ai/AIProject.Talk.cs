using System.Collections.Generic;
using AIChara;
using AIProject.Definitions;
using BehaviorDesigner.Runtime.Tasks;
using Manager;
using UnityEngine;

namespace AIProject;

[TaskCategory("")]
public class Talk : AgentAction
{
	private int _relationShip = 50;

	private Actor _partner;

	public override void OnStart()
	{
		base.OnStart();
		AgentActor agent = base.Agent;
		Actor actor = (_partner = agent.CommandPartner);
		if (actor == null)
		{
			return;
		}
		agent.SetActiveOnEquipedItem(active: false);
		actor.SetActiveOnEquipedItem(active: false);
		agent.StopNavMeshAgent();
		actor.StopNavMeshAgent();
		agent.ChangeStaticNavMeshAgentAvoidance();
		actor.ChangeStaticNavMeshAgentAvoidance();
		agent.DisableActionFlag();
		if (actor is AgentActor)
		{
			(actor as AgentActor).DisableActionFlag();
		}
		agent.RuntimeDesire = Desire.Type.Lonely;
		int listenID = 0;
		Dictionary<int, int> dictionary = null;
		if (actor is AgentActor)
		{
			AgentActor agentActor = _partner as AgentActor;
			int iD = base.Agent.ID;
			if (!agentActor.AgentData.FriendlyRelationShipTable.TryGetValue(iD, out _relationShip))
			{
				int num = 50;
				agentActor.AgentData.FriendlyRelationShipTable[iD] = num;
				_relationShip = num;
			}
			dictionary = Singleton<Manager.Resources>.Instance.Animation.TalkListenerRelationTable;
		}
		else
		{
			int iD2 = actor.ID;
			if (!agent.AgentData.FriendlyRelationShipTable.TryGetValue(iD2, out _relationShip))
			{
				int num = 50;
				agent.AgentData.FriendlyRelationShipTable[iD2] = num;
				_relationShip = num;
			}
			dictionary = Singleton<Manager.Resources>.Instance.Animation.MerchantListenerRelationTable;
		}
		List<KeyValuePair<int, int>> list = ListPool<KeyValuePair<int, int>>.Get();
		foreach (KeyValuePair<int, int> item in dictionary)
		{
			list.Add(item);
		}
		list.Sort((KeyValuePair<int, int> v1, KeyValuePair<int, int> v2) => v1.Value - v2.Value);
		for (int i = 0; i < list.Count; i++)
		{
			KeyValuePair<int, int> keyValuePair = list[i];
			if (_relationShip <= keyValuePair.Value)
			{
				listenID = keyValuePair.Key;
				break;
			}
		}
		ListPool<KeyValuePair<int, int>>.Release(list);
		agent.StartTalkSequence(actor, agent.ChaControl.fileParam.personality, listenID);
	}

	public override TaskStatus OnUpdate()
	{
		if (_partner == null)
		{
			return TaskStatus.Failure;
		}
		if (base.Agent.LivesTalkSequence)
		{
			return TaskStatus.Running;
		}
		OnComplete();
		return TaskStatus.Success;
	}

	public override void OnEnd()
	{
		base.Agent.StopTalkSequence();
		base.Agent.ChangeDynamicNavMeshAgentAvoidance();
		if (_partner != null)
		{
			_partner.ChangeDynamicNavMeshAgentAvoidance();
		}
		_partner = null;
	}

	private void OnComplete()
	{
		AgentActor agent = base.Agent;
		int desireKey = Desire.GetDesireKey(agent.RuntimeDesire);
		if (desireKey != -1)
		{
			agent.SetDesire(desireKey, 0f);
		}
		agent.RuntimeDesire = Desire.Type.None;
		if (agent.CommandPartner is AgentActor)
		{
			int value = _relationShip + Random.Range(-10, 10);
			agent.AgentData.FriendlyRelationShipTable[agent.CommandPartner.ID] = Mathf.Clamp(value, 0, 100);
			agent.ApplySituationResultParameter(23);
			AgentActor agentActor = agent.CommandPartner as AgentActor;
			ChaFileGameInfo fileGameInfo = agent.ChaControl.fileGameInfo;
			if (agent.ChaControl.fileGameInfo.normalSkill.ContainsValue(39))
			{
				agentActor.AddStatus(3, -20f);
			}
			int num = fileGameInfo.flavorState[4];
			int num2 = fileGameInfo.flavorState[7];
			int desireKey2 = Desire.GetDesireKey(Desire.Type.H);
			float? desire = agent.GetDesire(desireKey2);
			StatusProfile statusProfile = Singleton<Manager.Resources>.Instance.StatusProfile;
			bool flag = num >= statusProfile.LesbianBorder && num2 >= statusProfile.GirlsActionBorder && desire.HasValue && desire.GetValueOrDefault() >= statusProfile.LesbianBorderDesire;
			int lesbianBorder = Singleton<Manager.Resources>.Instance.StatusProfile.LesbianBorder;
			if (flag)
			{
				agent.Partner = _partner;
				agentActor.Partner = agent;
				agent.ChangeBehavior(Desire.ActionType.GotoLesbianSpot);
				agentActor.BehaviorResources.ChangeMode(Desire.ActionType.GotoLesbianSpotFollow);
			}
			else
			{
				agent.ChangeBehavior(Desire.ActionType.Normal);
				agentActor.ChangeBehavior(Desire.ActionType.Normal);
			}
		}
		else if (agent.CommandPartner is MerchantActor)
		{
			Dictionary<Merchant.ActionType, int> resultAddFriendlyRelationShipTable = Singleton<Manager.Resources>.Instance.MerchantProfile.ResultAddFriendlyRelationShipTable;
			if (!resultAddFriendlyRelationShipTable.TryGetValue(Merchant.ActionType.TalkWithAgent, out var value2))
			{
				value2 = 0;
			}
			int value3 = _relationShip + value2;
			agent.AgentData.FriendlyRelationShipTable[agent.CommandPartner.ID] = Mathf.Clamp(value3, 0, 100);
			agent.ApplySituationResultParameter(25);
			MerchantActor merchantActor = agent.CommandPartner as MerchantActor;
			StatusProfile statusProfile2 = Singleton<Manager.Resources>.Instance.StatusProfile;
			int desireKey3 = Desire.GetDesireKey(Desire.Type.H);
			float? desire2 = agent.GetDesire(desireKey3);
			ChaFileGameInfo fileGameInfo2 = agent.ChaControl.fileGameInfo;
			int num3 = fileGameInfo2.flavorState[4];
			int num4 = fileGameInfo2.flavorState[7];
			if (statusProfile2.LesbianBorder <= num3 && statusProfile2.GirlsActionBorder <= num4 && desire2.HasValue && statusProfile2.LesbianBorderDesire <= desire2.GetValueOrDefault())
			{
				agent.Partner = _partner;
				_partner.Partner = agent;
				agent.ChangeBehavior(Desire.ActionType.GotoLesbianSpot);
				merchantActor.ChangeBehavior(Merchant.ActionType.GotoLesbianSpotFollow);
			}
			else
			{
				agent.ChangeBehavior(Desire.ActionType.Normal);
				merchantActor.ChangeBehavior(merchantActor.LastNormalMode);
			}
		}
		int desireKey4 = Desire.GetDesireKey(Desire.Type.Lonely);
		agent.SetDesire(desireKey4, 0f);
		agent.TargetInSightActor = null;
		agent.CommandPartner = null;
	}
}
