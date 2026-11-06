using System;
using System.Collections.Generic;
using UnityEngine;

public class CommunicationInfo : ScriptableObject
{
	[Serializable]
	public class Param
	{
		public int priority;

		public int conditions = -1;

		public int favor;

		public int lewdness;

		public int anger;

		public string assetbundle = "";

		public string file = "";

		public string text = "";

		public string pose = "";

		public string facialExpression = "";

		public int eyeLook = 1;

		public int neckLook;

		public int topic = -1;
	}

	public List<Param> param = new List<Param>();
}
