using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ADV;
using ActionGame;
using ActionGame.Chara;
using Illusion;
using Illusion.Elements.Reference;
using Manager;
using MessagePack;
using UnityEngine;

namespace SaveData;

[MessagePackObject(true)]
public abstract class CharaData
{
	public abstract class Params
	{
		public class Data : Pointer<object>
		{
			public enum Command
			{
				None,
				Replace,
				Int,
				String,
				BOOL
			}

			public Command command { get; private set; }

			public string key { get; private set; }

			public static Command Cast(object o)
			{
				if (o is string)
				{
					return Command.String;
				}
				if (o is int)
				{
					return Command.Int;
				}
				if (o is bool)
				{
					return Command.BOOL;
				}
				return Command.None;
			}

			public static Command Cast(Type type)
			{
				if (type == typeof(string))
				{
					return Command.String;
				}
				if (type == typeof(int))
				{
					return Command.Int;
				}
				if (type == typeof(bool))
				{
					return Command.BOOL;
				}
				return Command.None;
			}

			public Data(Command command, string key, Func<object> get, Action<object> set = null)
				: base(get, set)
			{
				this.command = command;
				this.key = key;
			}
		}

		protected List<Data> _datas;

		public List<Data> datas => _datas;

		public CharaData chara { get; private set; }

		public void CreateCommand(List<Program.Transfer> list)
		{
			int num = -1;
			foreach (Data data in datas)
			{
				num++;
				switch (data.command)
				{
				case Data.Command.Replace:
					list.Add(Program.Transfer.Create(true, Command.Replace, data.key, (string)data.value));
					break;
				case Data.Command.Int:
					list.Add(Program.Transfer.Create(true, Command.VAR, "int", data.key, data.value.ToString()));
					break;
				case Data.Command.String:
					list.Add(Program.Transfer.Create(true, Command.VAR, "string", data.key, data.value.ToString()));
					break;
				case Data.Command.BOOL:
					list.Add(Program.Transfer.Create(true, Command.VAR, "bool", data.key, data.value.ToString()));
					break;
				}
			}
		}

		public IEnumerator<Data> GetEnumerator()
		{
			foreach (Data data in datas)
			{
				yield return data;
			}
		}

		public Params(CharaData chara, string header)
		{
			this.chara = chara;
			_datas = new List<Data>();
			_datas.AddRange(from info in Utils.Type.GetPublicFields(chara.GetType())
				select new
				{
					info = info,
					command = Data.Cast(info.FieldType)
				} into p
				where p.command != Data.Command.None
				select new Data(p.command, header + p.info.Name, () => p.info.GetValue(chara), delegate(object o)
				{
					p.info.SetValue(chara, Convert.ChangeType(o, p.info.FieldType));
				}));
			_datas.AddRange((from info in Utils.Type.GetPublicProperties(chara.GetType())
				select new
				{
					info = info,
					command = Data.Cast(info.PropertyType)
				} into p
				where p.command != Data.Command.None
				select p).Select(p =>
			{
				Func<object> get = null;
				Action<object> set = null;
				if (p.info.CanRead)
				{
					get = () => p.info.GetValue(chara, null);
				}
				if (p.info.CanWrite)
				{
					set = delegate(object o)
					{
						p.info.SetValue(chara, o);
					};
				}
				return new Data(p.command, header + p.info.Name, get, set);
			}));
		}
	}

	public enum NameType
	{
		Last,
		First,
		Full,
		Nick
	}

	[MessagePackObject(true)]
	public class MoveData
	{
		public bool isAlive { get; set; }

		public int mapNo { get; set; }

		public Vector3 position { get; set; }

		public Vector3 angle { get; set; }

		public int waitPointID { get; set; } = int.MinValue;


		public int paramIndex { get; set; } = int.MinValue;


		public int motionIndex { get; set; } = int.MinValue;


		public int actionNo { get; set; } = -1;


		public float actionTimer { get; set; }

		public ActionControl.ResultInfo resultInfo { get; set; }

		public bool isArrival { get; set; }

		private void Reset()
		{
			isAlive = false;
			mapNo = -1;
			position = Vector3.zero;
			angle = Vector3.zero;
			waitPointID = int.MinValue;
			paramIndex = int.MinValue;
			motionIndex = int.MinValue;
			actionNo = -1;
			actionTimer = 0f;
			resultInfo = null;
			isArrival = false;
		}

		public void Set(Base chara)
		{
			Reset();
			isAlive = chara != null;
			if (!isAlive)
			{
				return;
			}
			mapNo = chara.mapNo;
			position = chara.position;
			angle = chara.eulerAngles;
			Base.WaitPointData wpData = chara.wpData;
			if (wpData != null)
			{
				waitPointID = wpData.wp.ID;
				paramIndex = wpData.paramIndex;
				motionIndex = wpData.motionIndex;
			}
			NPC nPC = chara as NPC;
			if (nPC != null)
			{
				actionNo = nPC.AI.actionNo;
				actionTimer = nPC.AI.actionTimer;
				ActionControl.ResultInfo result = nPC.AI.result;
				if (result != null)
				{
					waitPointID = ((result.point != null) ? result.point.ID : int.MinValue);
				}
				resultInfo = result;
			}
			isArrival = chara.isArrival;
		}
	}

	[IgnoreMember]
	public static string[] Names { get; } = new string[4] { "姓", "名", "名前", "あだ名" };


	[IgnoreMember]
	public abstract int voiceNo { get; }

	[IgnoreMember]
	public GameObject root { get; private set; }

	[IgnoreMember]
	public Base charaBase { get; private set; }

	[IgnoreMember]
	public ChaControl chaCtrl { get; private set; }

	[IgnoreMember]
	public Transform transform
	{
		get
		{
			if (!(root == null))
			{
				return root.transform;
			}
			return null;
		}
	}

	[IgnoreMember]
	public bool isNickNameNormal => !WorldData.GetCallName(this).isSpecial;

	[IgnoreMember]
	public int callMyID
	{
		get
		{
			int callDefaultID = callID;
			if (callDefaultID == -1)
			{
				if (SingletonInitializer<Game>.initialized)
				{
					callDefaultID = Game.Player.callID;
				}
				if (callDefaultID == -1)
				{
					callDefaultID = WorldData.callDefaultID;
				}
			}
			return callDefaultID;
		}
	}

	[IgnoreMember]
	public string Name => parameter.fullname;

	[IgnoreMember]
	public string lastname => parameter.lastname;

	[IgnoreMember]
	public string firstname => parameter.firstname;

	[IgnoreMember]
	public int personality => parameter.personality;

	[IgnoreMember]
	public int bloodType => parameter.bloodType;

	[IgnoreMember]
	public int birthMonth => parameter.birthMonth;

	[IgnoreMember]
	public int birthDay => parameter.birthDay;

	[IgnoreMember]
	public int clubActivities => parameter.clubActivities;

	[IgnoreMember]
	public string nickname => parameter.nickname;

	[IgnoreMember]
	public float voicePitch => parameter.voicePitch;

	[IgnoreMember]
	public int weakPoint => parameter.weakPoint;

	[IgnoreMember]
	public ChaFileParameter.Denial denial => parameter.denial;

	[IgnoreMember]
	public bool charFileInitialized { get; set; }

	[IgnoreMember]
	public ChaFileControl charFile { get; private set; }

	[IgnoreMember]
	public ChaFileParameter parameter => charFile.parameter;

	public int callID { get; set; } = -1;


	public string callName { get; set; } = string.Empty;


	public int schoolClass { get; set; } = -1;


	public int schoolClassIndex { get; set; } = -1;


	public NameType nameType { get; set; } = NameType.First;


	public MoveData moveData { get; set; } = new MoveData();


	public abstract void SetADVParam(List<Program.Transfer> list);

	public abstract void SetADVParam(TextScenario scenario, string key, object o);

	public abstract void SetADVParam(TextScenario scenario);

	public void SetRoot(GameObject root)
	{
		this.root = root;
		if (root == null)
		{
			charaBase = null;
			chaCtrl = null;
			return;
		}
		Base component = root.GetComponent<Base>();
		if (component != null)
		{
			charaBase = component;
			chaCtrl = charaBase.chaCtrl;
		}
		else
		{
			chaCtrl = root.GetComponent<ChaControl>();
		}
	}

	public string GetCallName()
	{
		return WorldData.FindCallFileData(this).name;
	}

	public void SetCharFile(ChaFileControl charFile)
	{
		this.charFile = charFile;
		ChaFileUpdate();
	}

	public abstract void ChaFileUpdate();

	public abstract void Initialize();

	private void Create(bool isRandomize)
	{
		Initialize();
		if (isRandomize)
		{
			Randomize();
		}
	}

	public CharaData(ChaFileControl charFile, bool isRandomize)
	{
		SetCharFile(charFile);
		Create(isRandomize);
	}

	public CharaData(bool isRandomize)
	{
		SetCharFile(new ChaFileControl());
		Create(isRandomize);
	}

	public abstract void Randomize();
}
