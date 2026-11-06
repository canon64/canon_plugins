using System;
using System.Collections.Generic;
using ADV;
using MessagePack;

namespace AIProject.SaveData;

[MessagePackObject(false)]
public class StuffItem : ICommandData
{
	[Key(0)]
	public int CategoryID { get; set; }

	[Key(1)]
	public int ID { get; set; }

	[Key(2)]
	public int Count { get; set; }

	[Key(3)]
	public DateTime LatestDateTime { get; set; }

	public StuffItem()
	{
	}

	public StuffItem(int category, int id, int count)
	{
		CategoryID = category;
		ID = id;
		Count = count;
	}

	public StuffItem(StuffItem source)
	{
		CategoryID = source.CategoryID;
		ID = source.ID;
		Count = source.Count;
		LatestDateTime = source.LatestDateTime;
	}

	public IEnumerable<CommandData> CreateCommandData(string head)
	{
		return new CommandData[4]
		{
			new CommandData(CommandData.Command.Int, head + string.Format(".{0}", "CategoryID"), () => CategoryID),
			new CommandData(CommandData.Command.Int, head + string.Format(".{0}", "ID"), () => ID),
			new CommandData(CommandData.Command.Int, head + string.Format(".{0}", "Count"), () => Count),
			new CommandData(CommandData.Command.String, head + string.Format(".{0}", "LatestDateTime"), () => LatestDateTime.ToString())
		};
	}

	public static int RemoveStorages(StuffItem item, IReadOnlyCollection<List<StuffItem>> items)
	{
		return RemoveStorages(item, item.Count, items);
	}

	public static int RemoveStorages(StuffItem item, int count, IReadOnlyCollection<List<StuffItem>> items)
	{
		foreach (List<StuffItem> item2 in items)
		{
			item2.RemoveItem(item, ref count);
			if (count <= 0)
			{
				break;
			}
		}
		return count;
	}

	public static StuffItem CreateSystemItem(int id, int category = 0, int count = 1)
	{
		return new StuffItem(category, id, count);
	}

	public bool MatchItem(ItemIDKeyPair key)
	{
		return CategoryID == key.categoryID && ID == key.itemID;
	}
}
