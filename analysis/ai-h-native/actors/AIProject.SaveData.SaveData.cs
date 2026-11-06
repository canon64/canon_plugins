using System;
using System.Collections.Generic;
using System.IO;
using MessagePack;
using UnityEngine;

namespace AIProject.SaveData;

[MessagePackObject(false)]
public class SaveData
{
	[Key(0)]
	public WorldData AutoData { get; set; }

	[Key(1)]
	public Dictionary<int, WorldData> WorldList { get; set; } = new Dictionary<int, WorldData>();


	public void SaveFile(byte[] buffer)
	{
		using MemoryStream stream = new MemoryStream(buffer);
		SaveFile(stream);
	}

	public void SaveFile(string path)
	{
		using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write);
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

	public static SaveData LoadFile(string fileName)
	{
		SaveData saveData = new SaveData();
		if (saveData.Load(fileName))
		{
			saveData.ComplementDiff();
			return saveData;
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
				Debug.Log($"セーブデータが見つからないので新規作成: {fileName}");
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
			SaveData source = MessagePackSerializer.Deserialize<SaveData>(array);
			Copy(source);
			return true;
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
		return false;
	}

	public void Copy(SaveData source)
	{
		if (source.AutoData != null)
		{
			AutoData = new WorldData();
			AutoData.Copy(source.AutoData);
		}
		foreach (KeyValuePair<int, WorldData> world in source.WorldList)
		{
			WorldData worldData = new WorldData();
			worldData.Copy(world.Value);
			WorldList[world.Key] = worldData;
		}
	}

	public void ComplementDiff()
	{
		AutoData?.ComplementDiff();
		foreach (KeyValuePair<int, WorldData> world in WorldList)
		{
			if (world.Value != null)
			{
				world.Value.ComplementDiff();
			}
		}
	}
}
