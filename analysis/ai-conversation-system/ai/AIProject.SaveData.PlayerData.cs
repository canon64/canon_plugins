using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ADV;
using AIProject.Definitions;
using MessagePack;
using UnityEngine;

namespace AIProject.SaveData;

[MessagePackObject(false)]
public class PlayerData : ICharacterInfo, IDiffComparer, IParams, ICommandData
{
	private CharaParams _param;

	[IgnoreMember]
	Params IParams.param
	{
		[CompilerGenerated]
		get
		{
			return param;
		}
	}

	[IgnoreMember]
	public CharaParams param => this.GetCache(ref _param, () => new CharaParams(this, "P"));

	[Key(0)]
	public System.Version Version { get; set; } = new System.Version();


	[Key(1)]
	public byte Sex { get; set; }

	[Key(2)]
	public string[] CharaFileNames { get; set; } = new string[2];


	[IgnoreMember]
	public string CharaFileName
	{
		get
		{
			if (Sex == 0)
			{
				return CharaFileNames[0];
			}
			return CharaFileNames[1];
		}
	}

	[Key(3)]
	public Vector3 Position { get; set; }

	[Key(4)]
	public Quaternion Rotation { get; set; }

	[Key(5)]
	public List<StuffItem> ItemList { get; set; } = new List<StuffItem>();


	[Key(6)]
	public StuffItem LastAcquiredItem { get; set; }

	[Key(7)]
	public float Wetness { get; set; }

	[Key(8)]
	public StuffItem EquipedGloveItem { get; set; } = new StuffItem
	{
		ID = -1
	};


	[Key(9)]
	public StuffItem EquipedShovelItem { get; set; } = new StuffItem
	{
		ID = -1
	};


	[Key(10)]
	public StuffItem EquipedPickelItem { get; set; } = new StuffItem
	{
		ID = -1
	};


	[Key(11)]
	public StuffItem EquipedNetItem { get; set; } = new StuffItem
	{
		ID = -1
	};


	[Key(12)]
	public StuffItem EquipedFishingItem { get; set; } = new StuffItem
	{
		ID = -1
	};


	[Key(13)]
	public StuffItem EquipedHeadItem { get; set; } = new StuffItem
	{
		ID = -1
	};


	[Key(14)]
	public StuffItem EquipedBackItem { get; set; } = new StuffItem
	{
		ID = -1
	};


	[Key(15)]
	public StuffItem EquipedNeckItem { get; set; } = new StuffItem
	{
		ID = -1
	};


	[Key(16)]
	public StuffItem EquipedLampItem { get; set; } = new StuffItem
	{
		ID = -1
	};


	[Key(17)]
	public int SpendMoney { get; set; }

	[Key(18)]
	public Skill FishingSkill { get; set; } = new Skill();


	[Key(19)]
	public int InventorySlotMax { get; set; } = 1;


	[Key(20)]
	public int AreaID { get; set; }

	[Key(21)]
	public int ChunkID { get; set; }

	[Key(22)]
	public HashSet<int> CraftPossibleTable { get; set; } = new HashSet<int>();


	[Key(23)]
	public HashSet<int> FirstCreatedItemTable { get; set; } = new HashSet<int>();


	[Key(24)]
	public bool IsOnbu { get; set; }

	[Key(25)]
	public int? PartnerID { get; set; }

	[Key(26)]
	public bool DateEatTrigger { get; set; }

	public PlayerData()
	{
	}

	public PlayerData(PlayerData source)
	{
		if (source == null)
		{
			return;
		}
		Version = source.Version;
		Sex = source.Sex;
		for (int i = 0; i < 2; i++)
		{
			CharaFileNames[i] = source.CharaFileNames[i];
		}
		Position = source.Position;
		Rotation = source.Rotation;
		ItemList.Clear();
		foreach (StuffItem item in source.ItemList)
		{
			ItemList.Add(new StuffItem(item));
		}
		if (source.LastAcquiredItem != null)
		{
			LastAcquiredItem = new StuffItem(source.LastAcquiredItem);
		}
		Wetness = source.Wetness;
		EquipedGloveItem = new StuffItem(source.EquipedGloveItem);
		EquipedShovelItem = new StuffItem(source.EquipedShovelItem);
		EquipedPickelItem = new StuffItem(source.EquipedPickelItem);
		EquipedNetItem = new StuffItem(source.EquipedNetItem);
		EquipedFishingItem = new StuffItem(source.EquipedFishingItem);
		EquipedHeadItem = new StuffItem(source.EquipedHeadItem);
		EquipedBackItem = new StuffItem(source.EquipedBackItem);
		EquipedNeckItem = new StuffItem(source.EquipedNeckItem);
		EquipedLampItem = new StuffItem(source.EquipedLampItem);
		SpendMoney = source.SpendMoney;
		FishingSkill = new Skill(source.FishingSkill);
		InventorySlotMax = source.InventorySlotMax;
		AreaID = source.AreaID;
		ChunkID = source.ChunkID;
		CraftPossibleTable.Clear();
		foreach (int item2 in source.CraftPossibleTable)
		{
			CraftPossibleTable.Add(item2);
		}
		FirstCreatedItemTable.Clear();
		foreach (int item3 in source.FirstCreatedItemTable)
		{
			FirstCreatedItemTable.Add(item3);
		}
		IsOnbu = source.IsOnbu;
		PartnerID = source.PartnerID;
		DateEatTrigger = source.DateEatTrigger;
	}

	public StuffItem EquipedSearchItem(int id)
	{
		switch (id)
		{
		case 0:
		case 1:
		case 2:
			return EquipedGloveItem;
		case 3:
			return EquipedShovelItem;
		case 4:
			return EquipedPickelItem;
		case 5:
			return EquipedNetItem;
		case 6:
			return EquipedFishingItem;
		default:
			return null;
		}
	}

	public void Copy(PlayerData source)
	{
		if (source == null)
		{
			return;
		}
		Version = source.Version;
		Sex = source.Sex;
		for (int i = 0; i < 2; i++)
		{
			CharaFileNames[i] = source.CharaFileNames[i];
		}
		Position = source.Position;
		Rotation = source.Rotation;
		ItemList.Clear();
		foreach (StuffItem item in source.ItemList)
		{
			ItemList.Add(new StuffItem(item));
		}
		if (source.LastAcquiredItem != null)
		{
			LastAcquiredItem = new StuffItem(source.LastAcquiredItem);
		}
		Wetness = source.Wetness;
		EquipedGloveItem = new StuffItem(source.EquipedGloveItem);
		EquipedShovelItem = new StuffItem(source.EquipedShovelItem);
		EquipedPickelItem = new StuffItem(source.EquipedPickelItem);
		EquipedNetItem = new StuffItem(source.EquipedNetItem);
		EquipedFishingItem = new StuffItem(source.EquipedFishingItem);
		EquipedHeadItem = new StuffItem(source.EquipedHeadItem);
		EquipedBackItem = new StuffItem(source.EquipedBackItem);
		EquipedNeckItem = new StuffItem(source.EquipedNeckItem);
		EquipedLampItem = new StuffItem(source.EquipedLampItem);
		SpendMoney = source.SpendMoney;
		FishingSkill = new Skill(source.FishingSkill);
		InventorySlotMax = source.InventorySlotMax;
		AreaID = source.AreaID;
		ChunkID = source.ChunkID;
		CraftPossibleTable.Clear();
		foreach (int item2 in source.CraftPossibleTable)
		{
			CraftPossibleTable.Add(item2);
		}
		FirstCreatedItemTable.Clear();
		foreach (int item3 in source.FirstCreatedItemTable)
		{
			FirstCreatedItemTable.Add(item3);
		}
		IsOnbu = source.IsOnbu;
		PartnerID = source.PartnerID;
		DateEatTrigger = source.DateEatTrigger;
	}

	public void ComplementDiff()
	{
		if (Version < new System.Version("0.0.1"))
		{
			IsOnbu = false;
			PartnerID = null;
		}
		Version = AIProject.Definitions.Version.PlayerDataVersion;
	}

	public void UpdateDiff()
	{
		ItemList.OrganizeItemList();
	}

	public IEnumerable<CommandData> CreateCommandData(string head)
	{
		List<CommandData> list = new List<CommandData>();
		string key = head + "Sex";
		list.Add(new CommandData(CommandData.Command.Int, key, () => (int)Sex));
		return list;
	}
}
