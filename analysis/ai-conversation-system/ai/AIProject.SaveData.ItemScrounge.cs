using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ADV;
using MessagePack;
using UnityEngine;

namespace AIProject.SaveData;

[MessagePackObject(false)]
public class ItemScrounge : ICommandData
{
	[Key(0)]
	public bool isEvent { get; private set; }

	[Key(1)]
	public float timer { get; private set; }

	[Key(2)]
	public List<StuffItem> ItemList { get; private set; } = new List<StuffItem>();


	[IgnoreMember]
	public bool isEnd
	{
		[CompilerGenerated]
		get
		{
			return timer >= (float)timeLimit;
		}
	}

	[IgnoreMember]
	private int timeLimit { get; } = 2400;


	[IgnoreMember]
	public int redZoneTime { get; } = 600;


	[IgnoreMember]
	public float remainingTime
	{
		[CompilerGenerated]
		get
		{
			return (float)timeLimit - timer;
		}
	}

	public ItemScrounge()
	{
	}

	public ItemScrounge(ItemScrounge other)
	{
		isEvent = other.isEvent;
		timer = other.timer;
		ItemList = other.ItemList.Select((StuffItem x) => new StuffItem(x)).ToList();
	}

	public void AddTimer(float add)
	{
		if (isEvent)
		{
			timer = Mathf.Min(timer + add, timeLimit);
		}
	}

	public void Finish()
	{
		timer = timeLimit;
	}

	public void Reset()
	{
		isEvent = false;
		timer = 0f;
		ItemList.Clear();
	}

	public void Set(IReadOnlyCollection<StuffItem> items)
	{
		isEvent = true;
		timer = 0f;
		ItemList.AddRange(items);
	}

	public IEnumerable<CommandData> CreateCommandData(string head)
	{
		List<CommandData> list = new List<CommandData>();
		list.Add(new CommandData(CommandData.Command.BOOL, head + string.Format(".{0}", "isEvent"), () => isEvent));
		list.Add(new CommandData(CommandData.Command.Int, head + string.Format(".{0}", "timer"), () => timer));
		string text = head + "ItemList";
		foreach (var item in ItemList.Select((StuffItem value, int index) => new { value, index }))
		{
			item.value.AddList(list, text + $"[{item.index}]");
		}
		return list;
	}
}
