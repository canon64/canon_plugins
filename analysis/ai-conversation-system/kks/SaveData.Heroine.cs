using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ADV;
using ActionData;
using Illusion.Extensions;
using Manager;
using MessagePack;
using UnityEngine;

namespace SaveData;

[MessagePackObject(true)]
public class Heroine : CharaData
{
	public new class Params : CharaData.Params
	{
		private Heroine heroine => base.chara as Heroine;

		public Params(Heroine heroine)
			: base(heroine, "H_")
		{
			string text = "H_HExperience";
			_datas.AddRange(new List<Data>
			{
				new Data(Data.Command.Replace, "H", () => string.Empty),
				new Data(Data.Command.Replace, "H" + CharaData.Names[0], () => heroine.lastname),
				new Data(Data.Command.Replace, "H" + CharaData.Names[1], () => heroine.firstname),
				new Data(Data.Command.Replace, "H" + CharaData.Names[2], () => heroine.Name),
				new Data(Data.Command.Replace, "H" + CharaData.Names[3], () => heroine.nickname),
				new Data(Data.Command.Replace, "P" + CharaData.Names[3], () => heroine.callName),
				new Data(Data.Command.String, text, () => heroine.HExperience.ToString()),
				new Data(Data.Command.Int, text + "Index", () => (int)heroine.HExperience)
			});
		}
	}

	public enum HExperienceKind
	{
		初めて,
		不慣れ,
		慣れ,
		淫乱
	}

	public const string ParamHead = "H";

	public const string ParamHeadName = "H_";

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
	public int FixCharaIDOrPersonality
	{
		get
		{
			if (fixCharaID == 0)
			{
				return base.personality;
			}
			return fixCharaID;
		}
	}

	[IgnoreMember]
	public string ChaName => "c" + FixCharaIDOrPersonality.MinusThroughToString("00");

	[IgnoreMember]
	public string ChaVoice => "c" + voiceNo.MinusThroughToString("00");

	[IgnoreMember]
	public override int voiceNo
	{
		get
		{
			if (!cachedVoiceNo.HasValue)
			{
				if (Voice.infoTable.TryGetValue(FixCharaIDOrPersonality, out var value))
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

	[IgnoreMember]
	public int relation
	{
		get
		{
			if (isGirlfriend)
			{
				if (favor < SingletonInitializer<ActionScene>.instance.stepUpDic[2])
				{
					return 2;
				}
				return 3;
			}
			if (isFriend)
			{
				return 1;
			}
			return 0;
		}
	}

	[IgnoreMember]
	public string RelationLabel => relationLabels[relation];

	[IgnoreMember]
	public static string[] relationLabels { get; } = new string[4] { "知り合い", "友達", "恋人", "親愛" };


	public int relationNPC { get; set; }

	[IgnoreMember]
	public string RelationNPCLabel => relationNPCLabels[relationNPC];

	[IgnoreMember]
	public static string[] relationNPCLabels { get; } = new string[4] { "お客さん", "友達", "親友", "恋人" };


	[IgnoreMember]
	public bool isNickNameEvent
	{
		get
		{
			if (!talkEvent.Contains(54))
			{
				return talkEvent.Contains(55);
			}
			return true;
		}
	}

	public int favor { get; set; }

	public int lewdness { get; set; }

	public int hCount { get; set; }

	public bool isFriend { get; set; }

	public bool isGirlfriend { get; set; }

	public int fixCharaID { get; set; }

	public int myRoomCount { get; set; }

	public Dictionary<string, float> motionSpeed { get; set; }

	public DesireInfo desireInfo { get; set; }

	public HashSet<int> talkEvent { get; set; } = new HashSet<int>();


	public byte[] talkTemper { get; set; } = new byte[39];


	public bool confessed { get; set; }

	public Queue<int> topicListen { get; set; } = new Queue<int>();


	public HashSet<int> topicTalked { get; set; } = new HashSet<int>();


	public int tipsCount { get; set; } = 1;


	public HashSet<int> topicDrop { get; set; } = new HashSet<int>();


	public int menstruationStartDay { get; set; }

	[IgnoreMember]
	private static byte menstruationMonth { get; } = 15;


	public byte menstruationDay { get; set; }

	[IgnoreMember]
	public byte MenstruationDay => (byte)((menstruationDay + menstruationStartDay) % menstruationMonth);

	public float[] hAreaExps { get; set; } = new float[6];


	public float[] massageExps { get; set; } = new float[2];


	public bool isVirgin { get; set; } = true;


	public bool isAnalVirgin { get; set; } = true;


	public float countKokanH { get; set; }

	public float countAnalH { get; set; }

	public float houshiExp { get; set; }

	public float hExp { get; set; }

	public bool isKiss { get; set; }

	public int countNamaInsert { get; set; }

	public int countHoushi { get; set; }

	public bool isDayH { get; set; }

	public bool isFirstGirlfriend { get; set; }

	public int visitDesire { get; set; }

	public int talkTimeMax { get; set; } = 4;


	public int talkTime { get; set; } = 4;


	public bool isTalkPoint { get; set; }

	public bool isLunch { get; set; }

	public HashSet<int> talkIntro { get; set; } = new HashSet<int>();


	public HashSet<int> talkCommand { get; set; } = new HashSet<int>();


	public int coordinates { get; set; }

	public bool isDresses { get; set; }

	[IgnoreMember]
	public int NowCoordinate
	{
		get
		{
			if (!isDresses)
			{
				if (!(base.chaCtrl == null))
				{
					return base.chaCtrl.fileStatus.coordinateType;
				}
				return coordinates;
			}
			return coordinates;
		}
	}

	[IgnoreMember]
	public int StatusCoordinate
	{
		get
		{
			if (!(base.chaCtrl == null))
			{
				return base.chaCtrl.fileStatus.coordinateType;
			}
			return coordinates;
		}
	}

	[IgnoreMember]
	public HExperienceKind HExperience
	{
		get
		{
			lewdness = Mathf.Clamp(lewdness, 0, 100);
			if (hCount >= 1)
			{
				float[] array = new float[hAreaExps.Length - 1];
				Array.Copy(hAreaExps, 1, array, 0, hAreaExps.Length - 1);
				bool flag = true;
				for (int i = 0; i < array.Length; i++)
				{
					flag = i switch
					{
						1 => flag & (countKokanH + array[i] >= 100f), 
						2 => flag & (countAnalH + array[i] >= 100f), 
						_ => flag & (array[i] >= 100f), 
					};
				}
				if (!flag)
				{
					return HExperienceKind.不慣れ;
				}
				if (lewdness != 100)
				{
					return HExperienceKind.慣れ;
				}
				return HExperienceKind.淫乱;
			}
			return HExperienceKind.初めて;
		}
	}

	public Heroine()
		: this(isRandomize: false)
	{
	}

	public Heroine(bool isRandomize)
		: base(isRandomize)
	{
	}

	public Heroine(ChaFileControl charFile, bool isRandomize)
		: base(charFile, isRandomize)
	{
	}

	public static byte[] GetBytes(Heroine heroine)
	{
		byte[] array = MessagePackSerializer.Serialize(heroine);
		using MemoryStream memoryStream = new MemoryStream();
		using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write(array.Length);
		binaryWriter.Write(array);
		heroine.charFile.SaveCharaFile(binaryWriter, savePng: true);
		return memoryStream.ToArray();
	}

	public static bool SetBytes(Version version, byte[] data, out Heroine heroine)
	{
		heroine = null;
		using MemoryStream input = new MemoryStream(data);
		using BinaryReader binaryReader = new BinaryReader(input);
		try
		{
			int count = binaryReader.ReadInt32();
			byte[] bytes = binaryReader.ReadBytes(count);
			heroine = MessagePackSerializer.Deserialize<Heroine>(bytes);
			heroine.charFile.LoadCharaFile(binaryReader, noLoadPng: false, noLoadStatus: false);
			heroine.charFileInitialized = true;
			heroine.ComplementWithVersion();
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
		list.Add(Program.Transfer.Create(multi: true, Command.HeroineCallNameChange, (string[])null));
	}

	public override void SetADVParam(TextScenario scenario, string key, object o)
	{
		CharaData.Params.Data data = param.datas.FirstOrDefault((CharaData.Params.Data p) => p.key == key);
		if (data == null)
		{
			return;
		}
		ValData valData = new ValData(ValData.Convert(o, data.value.GetType()));
		data.value = valData.o;
		scenario.Vars[data.key] = valData;
		if (key == "H_callID")
		{
			CallNameChange(scenario);
		}
		foreach (CharaData.Params.Data item in param.datas.Where((CharaData.Params.Data p) => p.command == CharaData.Params.Data.Command.Replace))
		{
			scenario.Replaces[item.key] = (string)item.value;
		}
	}

	private void AfterVarsUpdate(TextScenario scenario, string key, Func<object> func, Type type)
	{
		CharaData.Params.Data data = param.datas.FirstOrDefault((CharaData.Params.Data p) => p.key == key);
		data.value = func();
		scenario.Vars[data.key] = new ValData(ValData.Convert(data.value, type));
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

	public void CallNameChange(TextScenario scenario)
	{
		int num = base.callMyID;
		if (num != -1)
		{
			WorldData.CallFileData callFileData = WorldData.FindCallFileData(base.personality, num);
			scenario.Vars["callBundle"] = new ValData(callFileData.bundle);
			for (int i = 0; i < 3; i++)
			{
				scenario.Vars["callName" + i] = new ValData(callFileData.GetFileName(i));
			}
			string key = "H_callName";
			base.callName = callFileData.name;
			scenario.Vars[key] = new ValData(base.callName);
			scenario.Replaces["P" + CharaData.Names[3]] = base.callName;
		}
	}

	public override void Initialize()
	{
		int[] source = new int[6] { 7, 8, 9, 10, 11, 12 };
		for (int i = 0; i < talkTemper.Length; i++)
		{
			talkTemper[i] = (byte)UnityEngine.Random.Range(0, source.Contains(i - 1) ? 2 : 3);
		}
	}

	public override void ChaFileUpdate()
	{
		base.charFile.parameter.sex = 1;
		if (Singleton<GameAssist>.IsInstance())
		{
			motionSpeed = Singleton<GameAssist>.Instance.GetMotionSpeed(base.personality);
		}
	}

	public override void Randomize()
	{
		menstruationStartDay = UnityEngine.Random.Range(0, menstruationMonth);
	}

	public int GetShuffleEventNo(params int[] events)
	{
		int[] array = events.Except(talkEvent).ToArray();
		return (array.Any() ? array : events).Shuffle().First();
	}

	public void PopInit()
	{
		talkTimeMax = 4 + Mathf.Clamp(relation, 0, 2);
		talkTime = talkTimeMax;
		isTalkPoint = false;
		isLunch = false;
		talkIntro.Clear();
		talkCommand.Clear();
		tipsCount = 1;
		if (topicDrop == null)
		{
			topicDrop = new HashSet<int>();
		}
		else
		{
			topicDrop.Clear();
		}
	}

	public void NextCoordinate()
	{
		if (!isDresses)
		{
			isDresses = true;
		}
	}

	public void AddMenstruationsDay()
	{
		menstruationDay = (byte)((menstruationDay + 1) % menstruationMonth);
	}

	public void SetMenstruationsDay(int _menstruationDay)
	{
		menstruationDay = (byte)(_menstruationDay % menstruationMonth);
	}

	public void AddHCount()
	{
		hCount = Mathf.Min(hCount + 1, 2147483646);
	}

	public void ComplementWithVersion()
	{
		version = Version;
		if (talkEvent == null)
		{
			talkEvent = new HashSet<int>();
		}
		if (talkTemper == null)
		{
			talkTemper = new byte[39];
		}
	}
}
