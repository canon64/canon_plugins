using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Illusion;
using UnityEngine;

namespace AIProject;

public class HarvestDataInfo
{
	private HarvestData.Param _data { get; }

	public Dictionary<int, List<HarvestData.Data>> table { get; }

	public int Time
	{
		[CompilerGenerated]
		get
		{
			return _data.time;
		}
	}

	public int nameHash
	{
		[CompilerGenerated]
		get
		{
			return _data.nameHash;
		}
	}

	public HarvestDataInfo(HarvestData.Param data)
	{
		_data = data;
		table = data.data.ToLookup((HarvestData.Data v) => v.group, (HarvestData.Data v) => v).ToDictionary((IGrouping<int, HarvestData.Data> v) => v.Key, Enumerable.ToList);
	}

	public IReadOnlyCollection<HarvestData.Data> Get()
	{
		List<HarvestData.Data> list = new List<HarvestData.Data>();
		foreach (KeyValuePair<int, List<HarvestData.Data>> item in table)
		{
			Dictionary<HarvestData.Data, int> dictionary = item.Value.ToDictionary((HarvestData.Data p) => p, (HarvestData.Data p) => p.percent);
			if (!dictionary.Any())
			{
				Debug.LogError($"収穫抽選失敗:{item.Key}");
				continue;
			}
			HarvestData.Data data = Illusion.Utils.ProbabilityCalclator.DetermineFromDict(dictionary);
			if (data.nameHash != -1)
			{
				list.Add(data);
			}
		}
		return list;
	}
}
