using System;
using System.Collections.Generic;
using System.Linq;
using AIProject.SaveData;
using Manager;

namespace AIProject;

public static class StuffItemExtensions
{
	public static StuffItem FindItem(this List<StuffItem> self, StuffItem item)
	{
		return (item != null) ? self.Find((StuffItem p) => p.CategoryID == item.CategoryID && p.ID == item.ID) : null;
	}

	public static StuffItem[] FindItems(this IEnumerable<StuffItem> self, StuffItem item)
	{
		return (item != null) ? self.Where((StuffItem node) => item.CategoryID == node.CategoryID && item.ID == node.ID).ToArray() : new StuffItem[0];
	}

	public static bool AddItem(this ICollection<StuffItem> self, StuffItem item)
	{
		return self.AddItem(item, item.Count);
	}

	public static bool AddItem(this ICollection<StuffItem> self, StuffItem item, int count)
	{
		item.LatestDateTime = DateTime.Now;
		int itemSlotMax = Singleton<Resources>.Instance.DefinePack.MapDefines.ItemSlotMax;
		foreach (StuffItem item2 in from x in self.FindItems(item)
			orderby x.Count descending
			select x)
		{
			int num = itemSlotMax - item2.Count;
			if (num != 0)
			{
				int num2 = count - num;
				if (num2 > 0)
				{
					item2.Count = itemSlotMax;
					count = num2;
				}
				else
				{
					item2.Count += count;
					count = 0;
				}
				item2.LatestDateTime = item.LatestDateTime;
				if (count <= 0)
				{
					break;
				}
			}
		}
		bool result = false;
		while (count > 0)
		{
			result = true;
			int num3 = count - itemSlotMax;
			if (num3 > 0)
			{
				item.Count = itemSlotMax;
				self.Add(new StuffItem(item));
				count = num3;
				continue;
			}
			item.Count = count;
			self.Add(item);
			count = 0;
			break;
		}
		return result;
	}

	public static bool AddItem(this ICollection<StuffItem> self, StuffItem item, int count, int maxSlot)
	{
		item.LatestDateTime = DateTime.Now;
		int itemSlotMax = Singleton<Resources>.Instance.DefinePack.MapDefines.ItemSlotMax;
		foreach (StuffItem item2 in from x in self.FindItems(item)
			orderby x.Count descending
			select x)
		{
			int num = itemSlotMax - item2.Count;
			if (num != 0)
			{
				int num2 = count - num;
				if (num2 > 0)
				{
					item2.Count = itemSlotMax;
					count = num2;
				}
				else
				{
					item2.Count += count;
					count = 0;
				}
				item2.LatestDateTime = item.LatestDateTime;
				if (count <= 0)
				{
					break;
				}
			}
		}
		bool result = false;
		while (count > 0 && self.Count < maxSlot)
		{
			result = true;
			int num3 = count - itemSlotMax;
			if (num3 > 0)
			{
				item.Count = itemSlotMax;
				self.Add(new StuffItem(item));
				count = num3;
				continue;
			}
			item.Count = count;
			self.Add(item);
			count = 0;
			break;
		}
		return result;
	}

	public static bool RemoveItem(this List<StuffItem> self, StuffItem item)
	{
		int count = item.Count;
		return self.RemoveItem(item, ref count);
	}

	public static bool RemoveItem(this List<StuffItem> self, StuffItem item, ref int count)
	{
		bool flag = false;
		foreach (StuffItem item2 in from x in self.FindItems(item)
			orderby x.Count
			select x)
		{
			int num = count - item2.Count;
			if (num >= 0)
			{
				item2.Count = 0;
				count = num;
				flag = true;
				continue;
			}
			item2.Count -= count;
			count = 0;
			break;
		}
		if (flag)
		{
			self.RemoveAll((StuffItem x) => x.Count <= 0);
		}
		return flag;
	}

	public static bool CanAddItem(this IReadOnlyCollection<StuffItem> self, int capacity, StuffItem item)
	{
		return self.CanAddItem(capacity, item, item.Count);
	}

	public static bool CanAddItem(this IReadOnlyCollection<StuffItem> self, int capacity, StuffItem item, out int possible)
	{
		return self.CanAddItem(capacity, item, item.Count, out possible);
	}

	public static bool CanAddItem(this IReadOnlyCollection<StuffItem> self, int capacity, StuffItem item, int count)
	{
		int possible;
		return self.CanAddItem(capacity, item, count, out possible);
	}

	public static bool CanAddItem(this IReadOnlyCollection<StuffItem> self, int capacity, StuffItem item, int count, out int possible)
	{
		int ItemSlotMax = Singleton<Resources>.Instance.DefinePack.MapDefines.ItemSlotMax;
		int num = self.FindItems(item).Sum((StuffItem x) => ItemSlotMax - x.Count);
		int num2 = count - num;
		if (num2 <= 0)
		{
			num2 = 0;
		}
		int num3 = num2 / ItemSlotMax + ((num2 % ItemSlotMax > 0) ? 1 : 0);
		int num4 = capacity - self.Count - num3;
		possible = num;
		if (num4 > 0)
		{
			possible += ItemSlotMax * num4;
		}
		if (num4 >= 0 && num2 > 0)
		{
			possible += ItemSlotMax - num2;
		}
		return num4 >= 0 || (possible > 0 && possible >= count);
	}
}
