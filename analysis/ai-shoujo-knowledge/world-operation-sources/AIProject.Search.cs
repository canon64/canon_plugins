using System.Collections.Generic;
using AIProject.Definitions;
using AIProject.SaveData;
using BehaviorDesigner.Runtime.Tasks;
using Manager;

namespace AIProject;

[TaskCategory("")]
public class Search : AgentStateAction
{
	public override void OnStart()
	{
		base.Agent.EventKey = EventType.Search;
		base.OnStart();
	}

	protected override void OnCompletedStateTask()
	{
		AgentActor agent = base.Agent;
		int desireKey = Desire.GetDesireKey(Desire.Type.Hunt);
		agent.SetDesire(desireKey, 0f);
		Dictionary<int, AIProject.SaveData.Environment.SearchActionInfo> searchActionLockTable = agent.AgentData.SearchActionLockTable;
		if (!searchActionLockTable.TryGetValue(agent.CurrentPoint.RegisterID, out var value))
		{
			value = new AIProject.SaveData.Environment.SearchActionInfo();
		}
		value.Count++;
		searchActionLockTable[agent.CurrentPoint.RegisterID] = value;
		ActionPoint currentPoint = base.Agent.CurrentPoint;
		Resources instance = Singleton<Resources>.Instance;
		int pointID = (currentPoint.IDList.IsNullOrEmpty() ? currentPoint.ID : currentPoint.IDList.GetElement(0));
		Dictionary<int, ItemTableElement> itemTableInArea = instance.GameInfo.GetItemTableInArea(pointID);
		if (itemTableInArea == null)
		{
		}
		Actor.SearchInfo searchInfo = agent.RandomAddItem(itemTableInArea, containsNone: false);
		if (!searchInfo.IsSuccess)
		{
			return;
		}
		foreach (Actor.ItemSearchInfo item3 in searchInfo.ItemList)
		{
			StuffItem item = new StuffItem(item3.categoryID, item3.id, item3.count);
			agent.AgentData.ItemList.AddItem(item);
			StuffItemInfo item2 = Singleton<Resources>.Instance.GameInfo.GetItem(item3.categoryID, item3.id);
			MapUIContainer.AddItemLog(agent, item2, item3.count, isNotify: false);
		}
	}
}
