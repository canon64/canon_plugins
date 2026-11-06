using System;
using System.Collections.Generic;
using MessagePack;

namespace AIChara;

[MessagePackObject(true)]
public class ChaFileGameInfo
{
	[MessagePackObject(true)]
	public class MinMaxInfo
	{
		public float lower { get; set; }

		public float upper { get; set; }

		public MinMaxInfo()
		{
			MemberInit();
		}

		public void MemberInit()
		{
			lower = 20f;
			upper = 80f;
		}

		public void Copy(MinMaxInfo src)
		{
			lower = src.lower;
			upper = src.upper;
		}
	}

	[IgnoreMember]
	public static readonly string BlockName = "GameInfo";

	public Version version { get; set; }

	public bool gameRegistration { get; set; }

	public MinMaxInfo tempBound { get; set; }

	public MinMaxInfo moodBound { get; set; }

	public Dictionary<int, int> flavorState { get; set; }

	public int totalFlavor { get; set; }

	public Dictionary<int, float> desireDefVal { get; set; }

	public Dictionary<int, float> desireBuffVal { get; set; }

	public int phase { get; set; }

	public Dictionary<int, int> normalSkill { get; set; }

	public Dictionary<int, int> hSkill { get; set; }

	public int favoritePlace { get; set; }

	public int lifestyle { get; set; }

	public int morality { get; set; }

	public int motivation { get; set; }

	public int immoral { get; set; }

	public bool isHAddTaii0 { get; set; }

	public bool isHAddTaii1 { get; set; }

	public ChaFileGameInfo()
	{
		MemberInit();
	}

	public void MemberInit()
	{
		version = ChaFileDefine.ChaFileGameInfoVersion;
		gameRegistration = false;
		tempBound = new MinMaxInfo();
		moodBound = new MinMaxInfo();
		flavorState = new Dictionary<int, int>();
		for (int i = 0; i < 8; i++)
		{
			flavorState[i] = 0;
		}
		totalFlavor = 0;
		desireDefVal = new Dictionary<int, float>();
		desireBuffVal = new Dictionary<int, float>();
		for (int j = 0; j < 16; j++)
		{
			desireDefVal[j] = 0f;
			desireBuffVal[j] = 0f;
		}
		phase = 0;
		normalSkill = new Dictionary<int, int>();
		hSkill = new Dictionary<int, int>();
		for (int k = 0; k < 5; k++)
		{
			normalSkill[k] = -1;
			hSkill[k] = -1;
		}
		favoritePlace = -1;
		lifestyle = -1;
		morality = 50;
		motivation = 0;
		immoral = 0;
		isHAddTaii0 = false;
		isHAddTaii1 = false;
	}

	public void Copy(ChaFileGameInfo src)
	{
		version = src.version;
		gameRegistration = src.gameRegistration;
		tempBound.Copy(src.tempBound);
		moodBound.Copy(src.moodBound);
		flavorState = new Dictionary<int, int>(src.flavorState);
		totalFlavor = src.totalFlavor;
		desireDefVal = new Dictionary<int, float>(src.desireDefVal);
		desireBuffVal = new Dictionary<int, float>(src.desireBuffVal);
		phase = src.phase;
		normalSkill = new Dictionary<int, int>(src.normalSkill);
		hSkill = new Dictionary<int, int>(src.hSkill);
		favoritePlace = src.favoritePlace;
		lifestyle = src.lifestyle;
		morality = src.morality;
		motivation = src.motivation;
		immoral = src.immoral;
		isHAddTaii0 = src.isHAddTaii0;
		isHAddTaii1 = src.isHAddTaii1;
	}

	public void ComplementWithVersion()
	{
		if (flavorState == null || flavorState.Count == 0)
		{
			flavorState = new Dictionary<int, int>();
			for (int i = 0; i < 8; i++)
			{
				flavorState[i] = 0;
			}
		}
		if (desireDefVal == null || desireDefVal.Count == 0)
		{
			desireDefVal = new Dictionary<int, float>();
			for (int j = 0; j < 16; j++)
			{
				desireDefVal[j] = 0f;
			}
		}
		if (desireBuffVal == null || desireBuffVal.Count == 0)
		{
			desireBuffVal = new Dictionary<int, float>();
			for (int k = 0; k < 16; k++)
			{
				desireBuffVal[k] = 0f;
			}
		}
		if (tempBound.lower == 0f && tempBound.upper == 0f)
		{
			tempBound.lower = 20f;
			tempBound.upper = 80f;
		}
		if (moodBound.lower == 0f && moodBound.upper == 0f)
		{
			moodBound.lower = 20f;
			moodBound.upper = 80f;
		}
		if (phase < 3)
		{
			lifestyle = -1;
		}
		if (normalSkill == null || normalSkill.Count == 0)
		{
			normalSkill = new Dictionary<int, int>();
			for (int l = 0; l < 5; l++)
			{
				normalSkill[l] = -1;
			}
		}
		if (hSkill == null || hSkill.Count == 0)
		{
			hSkill = new Dictionary<int, int>();
			for (int m = 0; m < 5; m++)
			{
				hSkill[m] = -1;
			}
		}
		version = ChaFileDefine.ChaFileGameInfoVersion;
	}
}
