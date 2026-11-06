using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ADV;
using Manager;
using MessagePack;
using UnityEngine;

namespace SaveData;

[MessagePackObject(true)]
public class Player : CharaData
{
	public new class Params : CharaData.Params
	{
		private Player player => base.chara as Player;

		public Params(Player player)
			: base(player, "P_")
		{
			_datas.AddRange(new List<Data>
			{
				new Data(Data.Command.Replace, "P", () => string.Empty),
				new Data(Data.Command.Replace, "P" + CharaData.Names[0], () => player.lastname),
				new Data(Data.Command.Replace, "P" + CharaData.Names[1], () => player.firstname),
				new Data(Data.Command.Replace, "P" + CharaData.Names[2], () => player.Name)
			});
		}
	}

	[MessagePackObject(true)]
	public class TopicState
	{
		public int num { get; set; }

		public DateTime time { get; set; }

		[IgnoreMember]
		private static int MaxNum { get; } = 99;


		public TopicState()
		{
		}

		public TopicState(int num)
		{
			Add(num);
		}

		public bool IsAdd(int count)
		{
			return num + count <= MaxNum;
		}

		public bool IsSub(int count)
		{
			return num - Mathf.Abs(count) >= 0;
		}

		public bool Add(int count = 1)
		{
			bool result;
			if (count > 0)
			{
				result = IsAdd(1);
				num = Mathf.Min(num + count, MaxNum);
				time = DateTime.Now;
			}
			else
			{
				result = IsSub(1);
				num = Mathf.Max(num + count, 0);
			}
			return result;
		}

		public bool Sub(int count = 1)
		{
			return Add(-count);
		}
	}

	public const string ParamHead = "P";

	public const string ParamHeadName = "P_";

	public const int ActionCountDef = 5;

	private Params _param;

	private int? cachedVoiceNo;

	[IgnoreMember]
	public static Version Version { get; } = new Version(0, 0, 0);


	public Version version { get; set; } = Version;


	[IgnoreMember]
	public Params param
	{
		get
		{
			if (_param == null)
			{
				return _param = new Params(this);
			}
			return _param;
		}
	}

	[IgnoreMember]
	public override int voiceNo
	{
		get
		{
			if (!cachedVoiceNo.HasValue)
			{
				if (Voice.infoTable.TryGetValue(-100, out var value))
				{
					cachedVoiceNo = value.No;
				}
				else
				{
					cachedVoiceNo = base.personality;
				}
			}
			return cachedVoiceNo.Value;
		}
	}

	public int intellect { get; set; }

	public int physical { get; set; }

	public int hentai { get; set; }

	public int actionCount { get; set; } = 5;


	public int girlfriendedCnt { get; set; }

	public float playTimeCalc { get; set; }

	public uint playTime { get; set; }

	public int hcount { get; set; }

	public int hPeopleCount { get; set; }

	public int orgCount { get; set; }

	public int changeClothesType { get; set; }

	public Dictionary<int, TopicState> topicStock { get; set; } = new Dictionary<int, TopicState>();


	public int whreLiveSelection { get; set; }

	[IgnoreMember]
	public int koikatsuPoint
	{
		get
		{
			return _koikatsuPoint;
		}
		set
		{
			_koikatsuPoint = Mathf.Clamp(value, 0, WorldData.KoikatsuPointMax);
		}
	}

	public int _koikatsuPoint { get; set; }

	public Dictionary<int, int> buyNumTable { get; set; } = new Dictionary<int, int>();


	[IgnoreMember]
	public bool isSecretBeachOpen
	{
		get
		{
			return buyNumTable.ContainsKey(3);
		}
		set
		{
			if (value)
			{
				buyNumTable[3] = 1;
			}
			else
			{
				buyNumTable.Remove(3);
			}
		}
	}

	public bool isSecretBeachInvasion { get; set; }

	[IgnoreMember]
	public bool isSuiteRoomOpen
	{
		get
		{
			return buyNumTable.ContainsKey(5);
		}
		set
		{
			if (value)
			{
				buyNumTable[5] = 1;
			}
			else
			{
				buyNumTable.Remove(5);
			}
		}
	}

	public bool isSuiteRoomInvasion { get; set; }

	public Player()
		: this(isRandomize: false)
	{
	}

	public Player(bool isRandomize)
		: base(isRandomize)
	{
	}

	public Player(ChaFileControl charFile, bool isRandomize)
		: base(charFile, isRandomize)
	{
	}

	public static byte[] GetBytes(Player player)
	{
		byte[] array = MessagePackSerializer.Serialize(player);
		using MemoryStream memoryStream = new MemoryStream();
		using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write(array.Length);
		binaryWriter.Write(array);
		player.charFile.SaveCharaFile(binaryWriter, savePng: true);
		return memoryStream.ToArray();
	}

	public static bool SetBytes(Version version, byte[] data, out Player player)
	{
		player = null;
		using MemoryStream input = new MemoryStream(data);
		using BinaryReader binaryReader = new BinaryReader(input);
		try
		{
			int count = binaryReader.ReadInt32();
			byte[] bytes = binaryReader.ReadBytes(count);
			player = MessagePackSerializer.Deserialize<Player>(bytes);
			player.charFile.LoadCharaFile(binaryReader);
			player.charFileInitialized = true;
			player.ComplementWithVersion();
		}
		catch (EndOfStreamException)
		{
			return false;
		}
		return true;
	}

	public override void SetADVParam(List<Program.Transfer> list)
	{
		param.CreateCommand(list);
	}

	public override void SetADVParam(TextScenario scenario, string key, object o)
	{
		CharaData.Params.Data data = param.datas.FirstOrDefault((CharaData.Params.Data p) => p.key == key);
		if (data != null)
		{
			ValData valData = new ValData(ValData.Convert(o, data.value.GetType()));
			data.value = valData.o;
			scenario.Vars[data.key] = valData;
		}
	}

	public override void SetADVParam(TextScenario scenario)
	{
		foreach (CharaData.Params.Data item in param.datas.Where((CharaData.Params.Data p) => p.command == CharaData.Params.Data.Command.Int || p.command == CharaData.Params.Data.Command.BOOL || p.command == CharaData.Params.Data.Command.String))
		{
			scenario.Vars[item.key] = new ValData(ValData.Convert(item.value, item.value.GetType()));
		}
		foreach (CharaData.Params.Data item2 in param.datas.Where((CharaData.Params.Data p) => p.command == CharaData.Params.Data.Command.Replace))
		{
			scenario.Replaces[item2.key] = (string)item2.value;
		}
	}

	public override void Initialize()
	{
		base.schoolClass = 0;
		base.callID = WorldData.callDefaultID;
	}

	public override void ChaFileUpdate()
	{
		base.charFile.parameter.sex = 0;
	}

	public override void Randomize()
	{
	}

	public void AddHCount()
	{
		hcount = Mathf.Min(10000, hcount + 1);
	}

	public void AddOrgCount(int _org)
	{
		orgCount = Mathf.Min(10000, orgCount + _org);
	}

	public void AddHPeopleCount()
	{
		hPeopleCount = Mathf.Min(10000, hPeopleCount + 1);
	}

	public void ComplementWithVersion()
	{
		version = Version;
		if (buyNumTable == null)
		{
			buyNumTable = new Dictionary<int, int>();
		}
	}
}
