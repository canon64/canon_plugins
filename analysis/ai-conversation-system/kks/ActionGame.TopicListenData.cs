using System;
using System.Collections.Generic;
using UnityEngine;

namespace ActionGame;

public class TopicListenData : ScriptableObject
{
	[Serializable]
	public class Param
	{
		public int no;

		public int category;

		public string bundle;

		public string file;

		public string text;

		public string pose = "";

		public string facialExpression = "";

		public int eyeLook = 1;

		public int neckLook;
	}

	public List<Param> param = new List<Param>();
}
