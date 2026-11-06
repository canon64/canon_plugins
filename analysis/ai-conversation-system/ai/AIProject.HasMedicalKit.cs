using AIProject.SaveData;
using BehaviorDesigner.Runtime.Tasks;
using Manager;

namespace AIProject;

[TaskCategory("")]
public class HasMedicalKit : AgentConditional
{
	public override TaskStatus OnUpdate()
	{
		ItemIDKeyPair gauzeID = Singleton<Resources>.Instance.CommonDefine.ItemIDDefine.GauzeID;
		foreach (StuffItem item in base.Agent.AgentData.ItemList)
		{
			if (item.CategoryID == gauzeID.categoryID && item.ID == gauzeID.itemID)
			{
				return TaskStatus.Success;
			}
		}
		return TaskStatus.Failure;
	}
}
