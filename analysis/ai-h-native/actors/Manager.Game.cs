using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using ADV;
using AIChara;
using AIProject;
using AIProject.Definitions;
using AIProject.SaveData;
using AIProject.Scene;
using ConfigScene;
using UniRx;
using UnityEngine;
using UnityEx;

namespace Manager;

public sealed class Game : Singleton<Game>
{
	public class Expression
	{
		public class Pattern : ICloneable
		{
			public int Ptn { get; set; }

			public bool Blend { get; set; }

			public Pattern()
			{
				Initialize();
			}

			public Pattern(string arg, bool isThrow = false)
			{
				Initialize();
				if (arg.IsNullOrEmpty())
				{
					return;
				}
				string[] array = arg.Split(',');
				int num = 0;
				try
				{
					string element = array.GetElement(num++);
					if (element != null)
					{
						Ptn = int.Parse(element);
					}
					element = array.GetElement(num++);
					if (element != null)
					{
						Blend = bool.Parse(element);
					}
				}
				catch (Exception)
				{
					if (isThrow)
					{
						throw new Exception(string.Format("Expression Pattern:{0}", string.Join(",", array)));
					}
					Debug.LogError(string.Format("Expression Pattern:{0}", string.Join(", ", array)));
				}
			}

			public void Initialize()
			{
				Ptn = 0;
				Blend = true;
			}

			public object Clone()
			{
				return MemberwiseClone();
			}
		}

		protected bool _useEyebrow;

		protected bool _useEyes;

		protected bool _useMouth;

		protected bool _useEyebrowOpen;

		protected bool _useEyesOpen;

		protected bool _useMouthOpen;

		protected bool _useEyesLook;

		protected bool _useHohoAkaRate;

		protected bool _useHighlight;

		protected bool _useTearsLv;

		protected bool _useBlink;

		public bool IsChangeSkip { private get; set; }

		public Pattern Eyebrow { get; private set; }

		public Pattern Eyes { get; private set; }

		public Pattern Mouth { get; private set; }

		public float EyebrowOpen { get; private set; } = 1f;


		public float EyesOpen { get; private set; } = 1f;


		public float MouthOpen { get; private set; } = 1f;


		public int EyesLook { get; private set; }

		public float HohoAkaRate { get; private set; }

		public bool IsHighlight { get; private set; } = true;


		public float TearsRate { get; private set; }

		public bool IsBlink { get; private set; } = true;


		public Expression()
		{
		}

		public Expression(Expression other)
		{
			Copy(other, this);
		}

		public Expression(string[] args, ref int index)
		{
			Initialize(args, ref index);
		}

		public Expression(string[] args)
		{
			int index = 0;
			Initialize(args, ref index);
		}

		public virtual void Initialize(string[] args, ref int index, bool isThrow = false)
		{
			try
			{
				string element = args.GetElement(index++);
				_useEyebrow = !element.IsNullOrEmpty();
				if (_useEyebrow)
				{
					Eyebrow = new Pattern(element, isThrow: true);
				}
				string element2 = args.GetElement(index++);
				_useEyes = !element2.IsNullOrEmpty();
				if (_useEyes)
				{
					Eyes = new Pattern(element2, isThrow: true);
				}
				string element3 = args.GetElement(index++);
				_useMouth = !element3.IsNullOrEmpty();
				if (_useMouth)
				{
					Mouth = new Pattern(element3, isThrow: true);
				}
				string element4 = args.GetElement(index++);
				_useEyebrowOpen = !element4.IsNullOrEmpty();
				if (_useEyebrowOpen)
				{
					EyebrowOpen = float.Parse(element4);
				}
				string element5 = args.GetElement(index++);
				_useEyesOpen = !element5.IsNullOrEmpty();
				if (_useEyesOpen)
				{
					EyesOpen = float.Parse(element5);
				}
				string element6 = args.GetElement(index++);
				_useMouthOpen = !element6.IsNullOrEmpty();
				if (_useMouthOpen)
				{
					MouthOpen = float.Parse(element6);
				}
				string element7 = args.GetElement(index++);
				_useEyesLook = !element7.IsNullOrEmpty();
				if (_useEyesLook)
				{
					EyesLook = int.Parse(element7);
				}
				string element8 = args.GetElement(index++);
				_useHohoAkaRate = !element8.IsNullOrEmpty();
				if (_useHohoAkaRate)
				{
					HohoAkaRate = float.Parse(element8);
				}
				string element9 = args.GetElement(index++);
				_useHighlight = !element9.IsNullOrEmpty();
				if (_useHighlight)
				{
					IsHighlight = bool.Parse(element9);
				}
				string element10 = args.GetElement(index++);
				_useTearsLv = !element10.IsNullOrEmpty();
				if (_useTearsLv)
				{
					TearsRate = float.Parse(element10);
				}
				string element11 = args.GetElement(index++);
				_useBlink = !element11.IsNullOrEmpty();
				if (_useBlink)
				{
					IsBlink = bool.Parse(element11);
				}
			}
			catch (Exception)
			{
				if (isThrow)
				{
					throw new Exception(string.Format("Expression:{0}", string.Join(",", args)));
				}
				Debug.LogError(string.Format("Expression:{0}", string.Join(",", args)));
			}
		}

		public static void Copy(Expression source, Expression destination)
		{
			destination.Eyebrow = (source?.Eyebrow.Clone() as Pattern) ?? new Pattern();
			destination.Eyes = (source.Eyes?.Clone() as Pattern) ?? new Pattern();
			destination.Mouth = (source.Mouth?.Clone() as Pattern) ?? new Pattern();
			destination.EyebrowOpen = source.EyebrowOpen;
			destination.EyesOpen = source.EyesOpen;
			destination.MouthOpen = source.MouthOpen;
			destination.EyesLook = source.EyesLook;
			destination.HohoAkaRate = source.HohoAkaRate;
			destination.TearsRate = source.TearsRate;
			destination.IsBlink = source.IsBlink;
			destination.IsChangeSkip = source.IsChangeSkip;
			destination._useEyebrow = source._useEyebrow;
			destination._useEyes = source._useEyes;
			destination._useMouth = source._useMouth;
			destination._useEyebrowOpen = source._useEyebrowOpen;
			destination._useEyesOpen = source._useEyesOpen;
			destination._useMouthOpen = source._useMouthOpen;
			destination._useEyesLook = source._useEyesLook;
			destination._useHohoAkaRate = source._useHohoAkaRate;
			destination._useTearsLv = source._useTearsLv;
			destination._useBlink = source._useBlink;
		}

		public void Copy(Expression destination)
		{
			Copy(this, destination);
		}

		public void Change(ChaControl charInfo)
		{
			bool isChangeSkip = IsChangeSkip;
			if (!isChangeSkip || _useEyebrow)
			{
				charInfo.ChangeEyebrowPtn(Eyebrow.Ptn, Eyebrow.Blend);
			}
			if (!isChangeSkip || _useEyes)
			{
				charInfo.ChangeEyesPtn(Eyes.Ptn, Eyes.Blend);
			}
			if (!isChangeSkip || _useMouth)
			{
				charInfo.ChangeMouthPtn(Mouth.Ptn, Mouth.Blend);
			}
			if (!isChangeSkip || _useEyebrowOpen)
			{
				charInfo.ChangeEyebrowOpenMax(EyebrowOpen);
			}
			if (!isChangeSkip || _useEyesOpen)
			{
				charInfo.ChangeEyesOpenMax(EyesOpen);
			}
			if (!isChangeSkip || _useMouthOpen)
			{
				charInfo.ChangeMouthOpenMax(MouthOpen);
			}
			if ((!isChangeSkip || _useEyesLook) && EyesLook != -1)
			{
				charInfo.ChangeLookEyesPtn(EyesLook);
			}
			if (!isChangeSkip || _useHohoAkaRate)
			{
				charInfo.ChangeHohoAkaRate(HohoAkaRate);
			}
			if (!isChangeSkip || _useHighlight)
			{
				charInfo.HideEyeHighlight(!IsHighlight);
			}
			if (!isChangeSkip || _useTearsLv)
			{
				charInfo.ChangeTearsRate(TearsRate);
			}
			if (!IsChangeSkip || _useBlink)
			{
				charInfo.ChangeEyesBlinkFlag(_useBlink);
			}
		}
	}

	public struct CustomSceneInfo
	{
		public string previous;

		public byte type;

		public byte sex;

		public bool isFemale;

		public string fileName;
	}

	public static readonly System.Version Version = new System.Version(1, 0);

	public CustomSceneInfo customSceneInfo;

	private static bool? _isAdd01 = null;

	private static bool? _isAdd30 = null;

	private SaveData _bakData;

	public static bool isAdd01
	{
		get
		{
			bool? flag = _isAdd01;
			bool value;
			if (flag.HasValue)
			{
				value = flag.Value;
			}
			else
			{
				bool? flag2 = (_isAdd01 = AssetBundleCheck.IsManifest("add01"));
				value = flag2.Value;
			}
			return value;
		}
	}

	public static bool isAdd30
	{
		get
		{
			bool? flag = _isAdd30;
			bool value;
			if (flag.HasValue)
			{
				value = flag.Value;
			}
			else
			{
				bool? flag2 = (_isAdd30 = AssetBundleCheck.IsManifest("add30"));
				value = flag2.Value;
			}
			return value;
		}
	}

	public bool IsDebug { get; set; }

	public byte UploaderType { get; set; }

	public string ReserveSceneName { get; set; }

	public GlobalSaveData GlobalData { get; private set; }

	public SaveData Data { get; private set; }

	public WorldData WorldData { get; set; }

	public bool IsAuto { get; set; }

	public AIProject.SaveData.Environment Environment
	{
		[CompilerGenerated]
		get
		{
			return WorldData?.Environment;
		}
	}

	public static string PrevPlayerStateFromCharaCreate { get; set; }

	public static int PrevAccessDeviceID { get; set; }

	public List<UnityEx.ValueTuple<string, string>> AssetBundlePaths { get; set; } = new List<UnityEx.ValueTuple<string, string>>();


	public Dictionary<int, AssetBundleInfo> LoadingSpriteABList { get; private set; } = new Dictionary<int, AssetBundleInfo>();


	public ConfigWindow Config { get; set; }

	public ConfirmScene Dialog { get; set; }

	public ExitScene ExitScene { get; set; }

	public MapShortcutUI MapShortcutUI { get; set; }

	public static bool IsFirstGame
	{
		get
		{
			if (!Singleton<Game>.IsInstance())
			{
				return false;
			}
			return !Singleton<Game>.Instance.ExistsBackup();
		}
	}

	public static bool IsFreeMode
	{
		get
		{
			if (!Singleton<Game>.IsInstance())
			{
				return false;
			}
			Game game = Singleton<Game>.Instance;
			if (game.WorldData == null)
			{
				return false;
			}
			return game.WorldData.FreeMode;
		}
	}

	public Dictionary<int, Dictionary<string, Expression>> CharaExpTable { get; private set; } = new Dictionary<int, Dictionary<string, Expression>>();


	private Game()
	{
		ReserveSceneName = string.Empty;
	}

	public void SetCustomSceneInfo(string prev, byte type, byte sex, string fileName)
	{
		customSceneInfo.previous = prev;
		customSceneInfo.type = type;
		customSceneInfo.sex = sex;
		customSceneInfo.fileName = fileName;
	}

	public bool ExistsBackup()
	{
		if (_bakData == null || (WorldData == null && !IsAuto))
		{
			return false;
		}
		if (IsAuto)
		{
			return _bakData.AutoData != null;
		}
		return _bakData.WorldList.ContainsKey(WorldData.WorldID);
	}

	public bool ExistsBackup(int id)
	{
		if (_bakData == null)
		{
			return false;
		}
		if (IsAuto)
		{
			return _bakData.AutoData != null;
		}
		return _bakData.WorldList.ContainsKey(id);
	}

	protected override void Awake()
	{
		if (!CheckInstance())
		{
			return;
		}
		Debug.Log("ーーーーーーーーーーーリソースマネージャー読み込みーーーーーーーーーーー");
		string path = "scene/common/";
		string[] array = (from bundle in CommonLib.GetAssetBundleNameListFromPath(path)
			orderby bundle descending
			select bundle).ToArray();
		string[] array2 = array;
		foreach (string text in array2)
		{
			string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
			string text2 = ((!(fileNameWithoutExtension == "00")) ? $"add{fileNameWithoutExtension}" : "abdata");
			GameObject gameObject = null;
			GameObject[] allAssets = AssetBundleManager.LoadAllAsset(text, typeof(GameObject), text2).GetAllAssets<GameObject>();
			foreach (GameObject gameObject2 in allAssets)
			{
				if (gameObject2.name == "resrcmanager")
				{
					gameObject = UnityEngine.Object.Instantiate(gameObject2, base.transform);
					break;
				}
				AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(text, text2));
			}
			if (gameObject != null)
			{
				break;
			}
		}
		DefinePack definePack = Singleton<Resources>.Instance.DefinePack;
		List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.ExpList, subdirCheck: true);
		assetBundleNameListFromPath.Sort();
		foreach (string file2 in assetBundleNameListFromPath)
		{
			ExcelData[] allAssets2 = AssetBundleManager.LoadAllAsset(file2, typeof(ExcelData)).GetAllAssets<ExcelData>();
			foreach (ExcelData excelData in allAssets2)
			{
				int key = int.Parse(excelData.name.Replace("c", string.Empty));
				if (!CharaExpTable.TryGetValue(key, out var value))
				{
					Dictionary<string, Expression> dictionary = new Dictionary<string, Expression>();
					CharaExpTable[key] = dictionary;
					value = dictionary;
				}
				LoadExpExcelData(value, excelData);
				if (!AssetBundlePaths.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == file2))
				{
					AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(file2, string.Empty));
				}
			}
		}
		List<string> assetBundleNameListFromPath2 = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.LoadingSpriteList);
		assetBundleNameListFromPath2.Sort();
		foreach (string file in assetBundleNameListFromPath2)
		{
			LoadingImageData[] allAssets3 = AssetBundleManager.LoadAllAsset(file, typeof(LoadingImageData)).GetAllAssets<LoadingImageData>();
			foreach (LoadingImageData loadingImageData in allAssets3)
			{
				foreach (LoadingImageData.Param item in loadingImageData.param)
				{
					LoadingSpriteABList[item.ID] = new AssetBundleInfo(item.Name, item.Bundle, item.Asset, item.Manifest);
				}
				if (!AssetBundlePaths.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == file))
				{
					AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(file, string.Empty));
				}
			}
		}
		Observable.TimerFrame(2).Subscribe(delegate
		{
			foreach (UnityEx.ValueTuple<string, string> assetBundlePath in AssetBundlePaths)
			{
				AssetBundleManager.UnloadAssetBundle(assetBundlePath.Item1, isUnloadForceRefCount: true, assetBundlePath.Item2);
			}
		});
		TextScenario.LoadReadInfo();
	}

	private void OnApplicationQuit()
	{
		if (!IsDebug)
		{
			TextScenario.SaveReadInfo();
		}
	}

	public void LoadShortcut()
	{
		MapShortcutUI.ImageIndex = 0;
		MapShortcutUI.ClosedEvent = null;
		GameObject original = CommonLib.LoadAsset<GameObject>("scene/common/00.unity3d", Singleton<Resources>.Instance.DefinePack.SceneNames.MapShortcutScene, clone: false, string.Empty);
		UnityEngine.Object.Instantiate(original, base.transform, worldPositionStays: false);
	}

	public void LoadShortcut(int index, System.Action closedEvent = null)
	{
		MapShortcutUI.ImageIndex = index;
		MapShortcutUI.ClosedEvent = closedEvent;
		GameObject original = CommonLib.LoadAsset<GameObject>("scene/common/00.unity3d", Singleton<Resources>.Instance.DefinePack.SceneNames.MapShortcutScene, clone: false, string.Empty);
		UnityEngine.Object.Instantiate(original, base.transform, worldPositionStays: false);
	}

	public void LoadConfig()
	{
		GameObject original = CommonLib.LoadAsset<GameObject>("scene/common/00.unity3d", Singleton<Resources>.Instance.DefinePack.SceneNames.ConfigScene, clone: false, string.Empty);
		UnityEngine.Object.Instantiate(original, base.transform, worldPositionStays: false);
	}

	public void LoadDialog()
	{
		GameObject original = CommonLib.LoadAsset<GameObject>("scene/common/00.unity3d", Singleton<Resources>.Instance.DefinePack.SceneNames.DialogScene, clone: false, string.Empty);
		UnityEngine.Object.Instantiate(original, base.transform, worldPositionStays: false);
	}

	public void DestroyDialog()
	{
		UnityEngine.Object.Destroy(Dialog.gameObject);
		Dialog = null;
		GC.Collect();
		UnityEngine.Resources.UnloadUnusedAssets();
	}

	public void LoadExit()
	{
		GameObject original = CommonLib.LoadAsset<GameObject>("scene/common/00.unity3d", Singleton<Resources>.Instance.DefinePack.SceneNames.ExitScene, clone: false, string.Empty);
		UnityEngine.Object.Instantiate(original, base.transform, worldPositionStays: false);
	}

	public void SaveGlobalData()
	{
		if (GlobalData != null)
		{
			string globalSaveDataFile = AIProject.Definitions.Path.GlobalSaveDataFile;
			if (!Directory.Exists(AIProject.Definitions.Path.SaveDataDirectory))
			{
				Directory.CreateDirectory(AIProject.Definitions.Path.SaveDataDirectory);
			}
			GlobalData.SaveFile(globalSaveDataFile);
		}
	}

	public void LoadGlobalData()
	{
		string globalSaveDataFile = AIProject.Definitions.Path.GlobalSaveDataFile;
		GlobalData = GlobalSaveData.LoadFile(globalSaveDataFile);
		if (GlobalData == null)
		{
			GlobalData = new GlobalSaveData();
			SaveGlobalData();
		}
	}

	public void SaveProfile(string path)
	{
		if (!Directory.Exists(AIProject.Definitions.Path.SaveDataDirectory))
		{
			Directory.CreateDirectory(AIProject.Definitions.Path.SaveDataDirectory);
		}
		Data.SaveFile(path);
		if (_bakData == null)
		{
			_bakData = new SaveData();
			_bakData.Copy(Data);
		}
		else
		{
			path += ".bak";
			_bakData.SaveFile(path);
			_bakData.Copy(Data);
		}
	}

	public void LoadProfile(string path)
	{
		Data = SaveData.LoadFile(path);
		if (Data == null)
		{
			Data = new SaveData();
			return;
		}
		_bakData = new SaveData();
		_bakData.Copy(Data);
	}

	public static WorldData CreateInitData(int id, bool cleared = false)
	{
		WorldData worldData = new WorldData();
		worldData.WorldID = id;
		AIProject.SaveData.Environment environment = worldData.Environment;
		environment.Time = new AIProject.SaveData.Environment.SerializableDateTime(1, 1, 1, 10, 0, 0);
		environment.Weather = Weather.Cloud1;
		environment.Temperature = Temperature.Normal;
		if (cleared)
		{
			environment.TutorialProgress = 29;
			if (!environment.BasePointOpenState.TryGetValue(0, out var value))
			{
				Dictionary<int, bool> dictionary = new Dictionary<int, bool>();
				environment.BasePointOpenState[0] = dictionary;
				value = dictionary;
			}
			value[-1] = true;
		}
		worldData.PlayerData.InventorySlotMax = Singleton<Resources>.Instance.PlayerProfile.DefaultInventoryMax;
		int agentMax = Singleton<Resources>.Instance.DefinePack.MapDefines.AgentMax;
		int agentDefaultNum = Singleton<Resources>.Instance.DefinePack.MapDefines.AgentDefaultNum;
		for (int i = 0; i < agentMax; i++)
		{
			AgentData agentData = new AgentData();
			worldData.AgentTable[i] = agentData;
			AgentData agentData2 = agentData;
			agentData2.OpenState = i < 1;
			agentData2.PlayEnterScene = i < 1;
		}
		return worldData;
	}

	public static void LoadExpExcelData(Dictionary<string, Expression> dic, ExcelData excelData)
	{
		foreach (ExcelData.Param item in excelData.list)
		{
			if (!AIProject.CollectionExtensions.IsNullOrEmpty(item.list))
			{
				Expression expression = new Expression(item.list.Skip(1).ToArray());
				expression.IsChangeSkip = true;
				Expression value = expression;
				dic[item.list[0]] = value;
			}
		}
	}

	public static Expression GetExpression(Dictionary<string, Expression> dic, string key)
	{
		dic.TryGetValue(key, out var value);
		return value;
	}

	public Expression GetExpression(int personality, string key)
	{
		if (!CharaExpTable.TryGetValue(personality, out var value))
		{
			return null;
		}
		return GetExpression(value, key);
	}

	public void RemoveWorldData(int id)
	{
		bool flag = false;
		if (Data != null && !Data.WorldList.IsNullOrEmpty() && !Data.WorldList.IsNullOrEmpty())
		{
			flag |= Data.WorldList.Remove(id);
		}
		if (_bakData != null && !_bakData.WorldList.IsNullOrEmpty())
		{
			flag |= _bakData.WorldList.Remove(id);
		}
		if (flag)
		{
			SaveProfile(AIProject.Definitions.Path.WorldSaveDataFile);
		}
	}
}
