using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIProject;

public class ItemData : ScriptableObject
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

		public int nameHash = -1;
	}

	public List<Param> param = new List<Param>();
}
