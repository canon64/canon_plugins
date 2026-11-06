using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AIProject.Animal;
using MessagePack;
using UnityEngine;

namespace AIProject.SaveData;

[MessagePackObject(false)]
public class WorldData
{
	[Key(12)]
	public Dictionary<int, bool> TutorialOpenStateTable = new Dictionary<int, bool>();

	[Key(0)]
	private Version Version { get; set; } = new Version();


	[Key(1)]
	public int WorldID { get; set; }

	[Key(2)]
	public bool FreeMode { get; set; }

	[IgnoreMember]
	public DateTime SaveTime { get; set; }

	[Key(4)]
	public string Name { get; set; }

	[Key(5)]
	public Environment Environment { get; set; } = new Environment();


	[Key(6)]
	public Dictionary<int, AgentData> AgentTable { get; set; } = new Dictionary<int, AgentData>();


	[Key(7)]
	public PlayerData PlayerData { get; set; } = new PlayerData();


	[Key(8)]
	public MerchantData MerchantData { get; set; } = new MerchantData();


	[Key(9)]
	public HousingData HousingData { get; set; } = new HousingData();


	[Key(10)]
	public bool Cleared { get; set; }

	[Key(11)]
	public string SaveTimeString { get; set; }

	[Key(13)]
	public Dictionary<AnimalTypes, Dictionary<int, WildAnimalData>> WildAnimalTable { get; set; } = new Dictionary<AnimalTypes, Dictionary<int, WildAnimalData>>();


	[Key(14)]
	public int MapID { get; set; }

	public void Copy(WorldData source)
	{
		WorldID = source.WorldID;
		FreeMode = source.FreeMode;
		SaveTime = source.SaveTime;
		Name = source.Name;
		Environment.Copy(source.Environment);
		foreach (KeyValuePair<int, AgentData> item in source.AgentTable)
		{
			if (!AgentTable.TryGetValue(item.Key, out var value))
			{
				AgentData agentData = new AgentData();
				AgentTable[item.Key] = agentData;
				value = agentData;
			}
			value.Copy(item.Value);
			value.param.Bind(item.Value.param.actor);
		}
		if (PlayerData == null)
		{
			PlayerData = new PlayerData();
		}
		PlayerData.Copy(source.PlayerData);
		PlayerData.param.Bind(source.PlayerData.param.actor);
		if (MerchantData == null)
		{
			MerchantData = new MerchantData();
		}
		MerchantData.Copy(source.MerchantData);
		MerchantData.param.Bind(source.MerchantData.param.actor);
		HousingData = new HousingData(source.HousingData);
		HousingData.CopyInstances(source.HousingData);
		Cleared = source.Cleared;
		SaveTimeString = source.SaveTimeString;
		if (!source.TutorialOpenStateTable.IsNullOrEmpty())
		{
			TutorialOpenStateTable = source.TutorialOpenStateTable.ToDictionary((KeyValuePair<int, bool> x) => x.Key, (KeyValuePair<int, bool> x) => x.Value);
		}
		else
		{
			TutorialOpenStateTable.Clear();
		}
		if (!source.WildAnimalTable.IsNullOrEmpty())
		{
			WildAnimalTable = source.WildAnimalTable.Where((KeyValuePair<AnimalTypes, Dictionary<int, WildAnimalData>> x) => !x.Value.IsNullOrEmpty()).ToDictionary((KeyValuePair<AnimalTypes, Dictionary<int, WildAnimalData>> x) => x.Key, (KeyValuePair<AnimalTypes, Dictionary<int, WildAnimalData>> x) => x.Value.Where((KeyValuePair<int, WildAnimalData> y) => y.Value != null).ToDictionary((KeyValuePair<int, WildAnimalData> y) => y.Key, (KeyValuePair<int, WildAnimalData> y) => new WildAnimalData(y.Value)));
		}
		else
		{
			WildAnimalTable.Clear();
		}
		MapID = source.MapID;
	}

	public void ComplementDiff()
	{
		SaveTime = ((!DateTime.TryParse(SaveTimeString, out var result)) ? DateTime.MinValue : result);
		PlayerData.ComplementDiff();
		foreach (KeyValuePair<int, AgentData> item in AgentTable)
		{
			item.Value.ComplementDiff();
		}
		MerchantData.ComplementDiff();
		if (HousingData != null)
		{
			HousingData.UpdateDiff();
		}
		else
		{
			HousingData = new HousingData();
		}
	}

	public void SaveFile(byte[] buffer)
	{
		using MemoryStream stream = new MemoryStream(buffer);
		SaveFile(stream);
	}

	public void SaveFile(string path)
	{
		using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write);
		SaveFile(stream);
	}

	public void SaveFile(Stream stream)
	{
		using BinaryWriter writer = new BinaryWriter(stream);
		SaveFile(writer);
	}

	public void SaveFile(BinaryWriter writer)
	{
		byte[] buffer = MessagePackSerializer.Serialize(this);
		writer.Write(buffer);
	}

	public static WorldData LoadFile(string fileName)
	{
		WorldData worldData = new WorldData();
		if (worldData.Load(fileName))
		{
			return worldData;
		}
		return null;
	}

	public bool Load(string fileName)
	{
		try
		{
			using FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
			if (fileStream.Length == 0)
			{
				Debug.LogError("空データ");
				return false;
			}
			return Load(fileStream);
		}
		catch (Exception ex)
		{
			if (ex is FileNotFoundException)
			{
				Debug.Log("セーブデータが見つからないので新規作成: " + fileName);
				return false;
			}
			Debug.LogException(ex);
			return false;
		}
	}

	public bool Load(Stream stream)
	{
		using BinaryReader reader = new BinaryReader(stream);
		return Load(reader);
	}

	public bool Load(BinaryReader reader)
	{
		try
		{
			byte[] array = reader.ReadBytes((int)reader.BaseStream.Length);
			if (array.IsNullOrEmpty())
			{
				return false;
			}
			WorldData worldData = MessagePackSerializer.Deserialize<WorldData>(array);
			worldData.CheckDiff();
			Copy(worldData);
			return true;
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
		return false;
	}

	private void CheckDiff()
	{
		Environment.UpdateDiff();
		foreach (KeyValuePair<int, AgentData> item in AgentTable)
		{
			item.Value.UpdateDiff();
		}
		if (PlayerData != null)
		{
			PlayerData.UpdateDiff();
		}
		else
		{
			PlayerData = new PlayerData();
		}
		if (MerchantData != null)
		{
			MerchantData.UpdateDiff();
		}
		else
		{
			MerchantData = new MerchantData();
		}
		if (HousingData != null)
		{
			HousingData.UpdateDiff();
		}
		else
		{
			HousingData = new HousingData();
		}
	}
}
