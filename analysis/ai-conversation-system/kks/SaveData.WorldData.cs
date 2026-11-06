using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Illusion;
using MessagePack;
using UnityEngine;

namespace SaveData;

[MessagePackObject(true)]
public sealed class WorldData
{
	public class CallFileData
	{
		private string format;

		public string name { get; private set; }

		public string bundle { get; private set; }

		public string GetFileName(int i)
		{
			return string.Format(format, i);
		}

		public CallFileData(NickName.Param param, string format)
		{
			name = param.Name;
			bundle = param.Bundle;
			this.format = format;
		}
	}

	[MessagePackObject(true)]
	public class AreaState
	{
		public int state { get; set; }

		public int stateProgress { get; set; }

		public bool isTouch { get; set; }
	}

	[MessagePackObject(true)]
	public class MapTopic
	{
		public Dictionary<int, bool> alive { get; set; } = new Dictionary<int, bool>();


		public HashSet<int> used { get; set; } = new HashSet<int>();


		public bool isPrayed { get; set; }

		public bool isRefresh { get; set; } = true;

	}

	[IgnoreMember]
	public static Version Version { get; } = new Version(0, 0, 0);


	public Version version { get; set; } = Version;


	[IgnoreMember]
	public static int callDefaultID { get; set; }

	[IgnoreMember]
	public static string Path => UserData.Path + path;

	[IgnoreMember]
	private static string path { get; } = "save/game";


	[IgnoreMember]
	public static string Extension { get; } = ".dat";


	[IgnoreMember]
	public static string AutoSaveFileNameHeader { get; } = "a";


	[IgnoreMember]
	private static int AutoSaveFileSum { get; } = 10;


	[IgnoreMember]
	public static int KoikatsuPointMax { get; } = 99999;


	[IgnoreMember]
	public static string[] SaveFiles
	{
		get
		{
			if (Directory.Exists(Path))
			{
				return Directory.GetFiles(Path, "*" + Extension);
			}
			return new string[0];
		}
	}

	[IgnoreMember]
	public static Dictionary<string, List<NickName.Param>> nicNameDic => _nicNameDic ?? (_nicNameDic = LoadNickNameParam());

	[IgnoreMember]
	private static Dictionary<string, List<NickName.Param>> _nicNameDic { get; set; } = null;


	public int emblemID { get; set; }

	public int saveTiming { get; set; }

	public bool isOpening { get; set; } = true;


	public int week { get; set; } = 6;


	public int cycle { get; set; }

	public float timer { get; set; } = float.MinValue;


	public int area { get; set; } = -1;


	public string information { get; set; } = "";


	public AreaState seedState { get; set; } = new AreaState();


	public AreaState birdState { get; set; } = new AreaState();


	public int prayedResult { get; set; } = -1;


	public HashSet<int> tips { get; set; } = new HashSet<int>();


	public MapTopic mapTopic { get; set; } = new MapTopic();


	[IgnoreMember]
	public int totalKoikatsuPoint
	{
		get
		{
			return _totalKoikatsuPoint;
		}
		set
		{
			_totalKoikatsuPoint = Mathf.Clamp(value, 0, KoikatsuPointMax);
		}
	}

	public int _totalKoikatsuPoint { get; set; }

	[IgnoreMember]
	public Player player { get; set; } = new Player(isRandomize: false);


	[IgnoreMember]
	public List<Heroine> heroineList { get; set; } = new List<Heroine>();


	public Dictionary<int, HashSet<int>> clubContents { get; set; } = new Dictionary<int, HashSet<int>>();


	[IgnoreMember]
	public HashSet<int> guideSetPositionMaps => _guideSetPositionMaps;

	public HashSet<int> _guideSetPositionMaps { get; set; } = new HashSet<int>();


	public bool isLiveNumMax { get; set; }

	[IgnoreMember]
	public Dictionary<int, int> shopItemEffect => _shopItemEffect;

	public Dictionary<int, int> _shopItemEffect { get; set; } = new Dictionary<int, int>();


	public bool isSuiteRoomEvent { get; set; }

	public static byte[] GetBytes(WorldData saveData)
	{
		byte[] array = MessagePackSerializer.Serialize(saveData);
		using MemoryStream memoryStream = new MemoryStream();
		using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write(array.Length);
		binaryWriter.Write(array);
		byte[] bytes = Player.GetBytes(saveData.player);
		binaryWriter.Write(bytes.Length);
		binaryWriter.Write(bytes);
		int count = saveData.heroineList.Count;
		binaryWriter.Write(count);
		for (int i = 0; i < count; i++)
		{
			byte[] bytes2 = Heroine.GetBytes(saveData.heroineList[i]);
			binaryWriter.Write(bytes2.Length);
			binaryWriter.Write(bytes2);
		}
		return memoryStream.ToArray();
	}

	public static bool SetBytes(BinaryReader br, out WorldData saveData)
	{
		saveData = null;
		try
		{
			int count = br.ReadInt32();
			byte[] bytes = br.ReadBytes(count);
			saveData = MessagePackSerializer.Deserialize<WorldData>(bytes);
			int count2 = br.ReadInt32();
			byte[] data = br.ReadBytes(count2);
			Player.SetBytes(saveData.version, data, out var player);
			saveData.player = player;
			int num = br.ReadInt32();
			saveData.heroineList.Clear();
			for (int i = 0; i < num; i++)
			{
				int count3 = br.ReadInt32();
				byte[] data2 = br.ReadBytes(count3);
				Heroine.SetBytes(saveData.version, data2, out var heroine);
				saveData.heroineList.Add(heroine);
			}
			saveData.ComplementWithVersion();
		}
		catch (EndOfStreamException)
		{
			return false;
		}
		return true;
	}

	public static void InitializeCall()
	{
		callDefaultID = GetCallNameList(-1).Find((NickName.Param p) => p.Name == "ダーリン").ID;
	}

	private static Dictionary<string, List<NickName.Param>> LoadNickNameParam()
	{
		Dictionary<string, List<NickName.Param>> dic = new Dictionary<string, List<NickName.Param>>();
		CommonLib.GetAssetBundleNameListFromPath("etcetra/list/nickname/", subdirCheck: true).ForEach(delegate(string file)
		{
			NickName[] allAssets = AssetBundleManager.LoadAllAsset(file, typeof(NickName)).GetAllAssets<NickName>();
			foreach (NickName nickName in allAssets)
			{
				if (!dic.TryGetValue(nickName.name, out var value))
				{
					value = (dic[nickName.name] = new List<NickName.Param>());
				}
				value.AddRange(nickName.param.Where((NickName.Param param) => !param.Name.IsNullOrEmpty() && param.ID != -1));
			}
			AssetBundleManager.UnloadAssetBundle(file, isUnloadForceRefCount: false);
		});
		return dic;
	}

	public static NickName.Param GetCallName(CharaData charaData)
	{
		return GetCallName(charaData, charaData.callMyID);
	}

	public static NickName.Param GetCallName(CharaData charaData, int id)
	{
		return GetCallName((charaData is Player) ? (-1) : charaData.personality, id);
	}

	public static NickName.Param GetCallName(int personality, int id)
	{
		return GetCallNameList(personality).Find((NickName.Param p) => p.ID == id);
	}

	public static List<NickName.Param> GetCallNameList(CharaData charaData)
	{
		return GetCallNameList((charaData is Player) ? (-1) : charaData.personality);
	}

	public static List<NickName.Param> GetCallNameList(int personality)
	{
		return nicNameDic[(personality >= 0) ? $"c{personality:00}" : "player"];
	}

	public static void CallNormalize(CharaData charaData)
	{
		int callID = charaData.callID;
		if (callID != -1)
		{
			NickName.Param callName = GetCallName(charaData, callID);
			if (callName != null)
			{
				if (charaData.callName != callName.Name)
				{
					charaData.callName = callName.Name;
				}
				return;
			}
		}
		charaData.callID = -1;
		NickName.Param callName2 = GetCallName(charaData);
		if (charaData.callName != callName2.Name)
		{
			charaData.callName = callName2.Name;
		}
	}

	public static CallFileData FindCallFileData(CharaData charaData)
	{
		return FindCallFileData((charaData is Player) ? (-1) : charaData.personality, charaData.callMyID);
	}

	public static CallFileData FindCallFileData(int personality, int id)
	{
		List<NickName.Param> callNameList = GetCallNameList(personality);
		NickName.Param param = callNameList.Find((NickName.Param pp) => pp.ID == id);
		string text = "namelist_";
		if (param.isSpecial)
		{
			text += "sp_";
			id -= (from pp in callNameList
				where pp.isSpecial
				select pp.ID).Min();
		}
		return new CallFileData(param, text + personality.ToString("00") + "_{0:00}_" + id.ToString("000"));
	}

	public void Save(string fileName)
	{
		Save(UserData.Create(path), fileName);
	}

	public void Save(string path, string fileName)
	{
		Utils.File.OpenWrite(path + fileName, isAppend: false, delegate(FileStream f)
		{
			using BinaryWriter binaryWriter = new BinaryWriter(f);
			binaryWriter.Write(GetBytes(this));
		});
	}

	public static WorldData Load(string fileName)
	{
		return Load(Path + "/", fileName);
	}

	public static WorldData Load(string path, string fileName)
	{
		WorldData result = null;
		Utils.File.OpenRead(path + fileName, delegate(FileStream f)
		{
			using BinaryReader br = new BinaryReader(f);
			SetBytes(br, out result);
		});
		return result ?? new WorldData();
	}

	public void AutoSave(int saveTiming, int area)
	{
		int headerLen = AutoSaveFileNameHeader.Length;
		var array = (from x in SaveFiles.Select(delegate(string file)
			{
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(file);
				int result = 0;
				bool success = fileNameWithoutExtension.StartsWith(AutoSaveFileNameHeader) && int.TryParse(fileNameWithoutExtension.Remove(0, headerLen), out result);
				return new
				{
					fileName = fileNameWithoutExtension,
					success = success,
					file = file,
					num = result + 1
				};
			})
			where x.success
			orderby x.num descending
			select x).ToArray();
		List<string> list = new List<string>();
		var array2 = array;
		foreach (var anon in array2)
		{
			bool num = anon.num >= AutoSaveFileSum;
			string text = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(anon.file), $"{AutoSaveFileNameHeader}{anon.num:00}{Extension}");
			File.Move(anon.file, text);
			if (num)
			{
				list.Add(text);
			}
		}
		foreach (string item in list)
		{
			File.Delete(item);
		}
		int num2 = this.saveTiming;
		this.saveTiming = saveTiming;
		information = string.Empty;
		this.area = area;
		Save(AutoSaveFileNameHeader + "00" + Extension);
		this.saveTiming = num2;
	}

	public void ComplementWithVersion()
	{
		version = Version;
		if (tips == null)
		{
			tips = new HashSet<int>();
		}
		if (_guideSetPositionMaps == null)
		{
			_guideSetPositionMaps = new HashSet<int>();
		}
		if (_shopItemEffect == null)
		{
			_shopItemEffect = new Dictionary<int, int>();
		}
	}

	public static WorldData SimpleData(string fileName)
	{
		WorldData ret = null;
		Utils.File.OpenRead(Path + "/" + fileName, delegate(FileStream f)
		{
			using BinaryReader binaryReader = new BinaryReader(f);
			int count = binaryReader.ReadInt32();
			byte[] bytes = binaryReader.ReadBytes(count);
			ret = MessagePackSerializer.Deserialize<WorldData>(bytes);
		});
		return ret;
	}

	public static string GetTimeFormat(WorldData worldData)
	{
		if (worldData == null)
		{
			return "(--/--)";
		}
		return "()";
	}

	public static int LastWriteIndexUpdate()
	{
		var source = (from x in SaveFiles.Select(delegate(string file)
			{
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(file);
				int result;
				bool success = int.TryParse(fileNameWithoutExtension, out result);
				return new
				{
					fileName = fileNameWithoutExtension,
					time = File.GetLastWriteTime(file),
					no = result,
					success = success
				};
			})
			where x.success
			orderby x.time descending
			select x).ToArray();
		if (source.Any())
		{
			return source.First().no;
		}
		return -1;
	}

	public static string GetLastWriteTimeFormat(string fileName)
	{
		return File.GetLastWriteTime(Path + "/" + fileName).ToString("yyyy/MM/dd/HH:mm");
	}

	public void Delete(string fileName)
	{
		File.Delete(Path + "/" + fileName);
	}
}
