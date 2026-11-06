using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIProject;

public class ItemData_System : ScriptableObject
{
	[Serializable]
	public class Param
	{
		public int ID = -1;

		public int IconID = -1;

		public string Name = string.Empty;

		public int Grade;

		public int Rate;

		public string Explanation = string.Empty;
	}

	public List<Param> param = new List<Param>();

	public static StuffItemInfo Convert(int category, Param param)
	{
		return new StuffItemInfo(category, new ItemData.Param
		{
			ID = param.ID,
			IconID = param.IconID,
			Name = param.Name,
			Grade = param.Grade,
			Rate = param.Rate,
			Explanation = param.Explanation
		}, isNone: true);
	}
}
