using System.Runtime.CompilerServices;

namespace AIProject;

public class StuffItemInfo
{
	private ItemData.Param param { get; set; }

	public int ID
	{
		[CompilerGenerated]
		get
		{
			return param.ID;
		}
	}

	public string Name
	{
		[CompilerGenerated]
		get
		{
			return param.Name;
		}
	}

	public int IconID
	{
		[CompilerGenerated]
		get
		{
			return param.IconID;
		}
	}

	public int CategoryID { get; private set; }

	public Rarelity Rarelity { get; set; }

	public int ReactionType { get; set; }

	public bool IsAvailableHeroine { get; set; }

	public ThresholdInt EnnuiAddition { get; set; }

	public ThresholdInt TasteAdditionNormal { get; set; }

	public ThresholdInt TasteAdditionEnnui { get; set; }

	public ItemEquipableState EquipableState { get; set; }

	public int Rate
	{
		[CompilerGenerated]
		get
		{
			return param.Rate;
		}
	}

	public Grade Grade
	{
		[CompilerGenerated]
		get
		{
			return (Grade)param.Grade;
		}
	}

	public string Explanation
	{
		[CompilerGenerated]
		get
		{
			return param.Explanation;
		}
	}

	public bool isTrash
	{
		[CompilerGenerated]
		get
		{
			return param.Rate >= 0;
		}
	}

	public int nameHash
	{
		[CompilerGenerated]
		get
		{
			return param.nameHash;
		}
	}

	public bool isNone { get; }

	public StuffItemInfo(int category, ItemData.Param param, bool isNone)
	{
		CategoryID = category;
		this.param = param;
		this.isNone = isNone;
		if (isNone)
		{
			Rarelity = (Rarelity)param.Grade;
		}
	}
}
