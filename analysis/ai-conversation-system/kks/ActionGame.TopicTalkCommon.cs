using System;
using System.Collections.Generic;
using UnityEngine;

namespace ActionGame;

public class TopicTalkCommon : ScriptableObject
{
	[Serializable]
	public class Param
	{
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
