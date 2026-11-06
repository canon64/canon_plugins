using System.Collections.Generic;
using AIProject.Definitions;
using AIProject.SaveData;
using AIProject.UI;
using BehaviorDesigner.Runtime.Tasks;
using Manager;

namespace AIProject;

[TaskCategory("")]
public class Cook : AgentStateAction
{
	private bool _continueCookSeq;

	private CraftUI.CreateItem _createItem;

	public override void OnStart()
	{
		base.Agent.EventKey = EventType.Cook;
		_continueCookSeq = CalcTargetRecipe();
		if (_continueCookSeq)
		{
			base.OnStart();
		}
	}

	public override TaskStatus OnUpdate()
	{
		if (!_continueCookSeq)
		{
			int desireKey = Desire.GetDesireKey(Desire.Type.Cook);
			base.Agent.SetDesire(desireKey, 0f);
			return TaskStatus.Success;
		}
		return base.OnUpdate();
	}

	protected override void OnCompletedStateTask()
	{
		AgentActor agent = base.Agent;
		int desireKey = Desire.GetDesireKey(Desire.Type.Cook);
		agent.SetDesire(desireKey, 0f);
		if (_createItem != null)
		{
			StuffItemInfo item = Singleton<Resources>.Instance.GameInfo.GetItem(_createItem.item.CategoryID, _createItem.item.ID);
			MapUIContainer.AddItemLog(agent, item, _createItem.info.CreateSum, isNotify: false);
			_createItem = null;
		}
	}

	private bool CalcTargetRecipe()
	{
		AgentActor agent = base.Agent;
		List<StuffItem> itemList = agent.AgentData.ItemList;
		_createItem = CraftUI.CreateCheck(Singleton<Resources>.Instance.GameInfo.recipe.cookTable, (IReadOnlyCollection<IReadOnlyCollection<StuffItem>>)(object)new List<StuffItem>[2]
		{
			itemList,
			Singleton<Game>.Instance.Environment.ItemListInPantry
		});
		int num = agent.ChaControl.fileGameInfo.flavorState[0];
		bool chef = agent.ChaControl.fileGameInfo.normalSkill.ContainsValue(0);
		return CraftUI.CreateCooking(_createItem, itemList, num, chef);
	}
}
