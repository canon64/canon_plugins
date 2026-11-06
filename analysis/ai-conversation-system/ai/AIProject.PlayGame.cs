using System.Collections.Generic;
using AIProject.Definitions;
using AIProject.SaveData;
using BehaviorDesigner.Runtime.Tasks;
using Manager;
using UnityEngine;

namespace AIProject;

[TaskCategory("")]
public class PlayGame : AgentStateAction
{
	public override void OnStart()
	{
		base.Agent.EventKey = EventType.Play;
		base.OnStart();
	}

	protected override void OnCompletedStateTask()
	{
		AgentActor agent = base.Agent;
		int desireKey = Desire.GetDesireKey(Desire.Type.Game);
		agent.SetDesire(desireKey, 0f);
		PoseKeyPair wateringID = Singleton<Manager.Resources>.Instance.AgentProfile.PoseIDTable.WateringID;
		if (agent.ActionID != wateringID.postureID || agent.PoseID != wateringID.poseID)
		{
			return;
		}
		float? num = null;
		FarmPoint farmPoint = null;
		FarmPoint[] farmPoints = Singleton<Manager.Map>.Instance.PointAgent.FarmPoints;
		foreach (FarmPoint farmPoint2 in farmPoints)
		{
			float num2 = Vector3.Distance(agent.Position, farmPoint2.Position);
			if (num.HasValue)
			{
				if (num2 < num.Value)
				{
					num = num2;
					farmPoint = farmPoint2;
				}
			}
			else
			{
				num = num2;
				farmPoint = farmPoint2;
			}
		}
		foreach (KeyValuePair<int, FarmPoint> item in Singleton<Manager.Map>.Instance.PointAgent.RuntimeFarmPointTable)
		{
			if (item.Value.Kind != 0)
			{
				continue;
			}
			float num3 = Vector3.Distance(agent.Position, item.Value.Position);
			if (num.HasValue)
			{
				if (num3 < num.Value)
				{
					num = num3;
					farmPoint = item.Value;
				}
			}
			else
			{
				num = num3;
				farmPoint = item.Value;
			}
		}
		if (!(farmPoint != null) || !Singleton<Game>.Instance.Environment.FarmlandTable.TryGetValue(farmPoint.RegisterID, out var value))
		{
			return;
		}
		foreach (AIProject.SaveData.Environment.PlantInfo item2 in value)
		{
			item2?.AddTimer(60f);
		}
		if (!agent.ChaControl.fileGameInfo.normalSkill.ContainsValue(12))
		{
			return;
		}
		foreach (AIProject.SaveData.Environment.PlantInfo item3 in value)
		{
			item3?.AddTimer(20f);
		}
	}
}
