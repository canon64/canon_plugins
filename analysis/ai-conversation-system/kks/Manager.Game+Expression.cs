using System;
using System.Collections.Generic;
using System.Linq;
using ADV;
using ActionGame;
using SaveData;
using UniRx;
using UniRx.Triggers;
using UnityEngine;

namespace Manager;

public sealed class Game : SingletonInitializer<Game>
{
	public class AnimePack
	{
		public RuntimeAnimatorController ac { get; private set; }

		public MotionIKData ikData { get; private set; }

		public string defaultName { get; private set; }

		public AnimePack(RuntimeAnimatorController ac, MotionIKData ikData, string defaultName)
		{
			this.ac = ac;
			this.ikData = ikData;
			this.defaultName = defaultName;
		}

		public void SetDefalut(ChaControl chaCtrl)
		{
			SetDefalut(chaCtrl, out var _);
		}

		public void SetDefalut(ChaControl chaCtrl, out MotionIK motionIK)
		{
			chaCtrl.animBody.runtimeAnimatorController = ac;
			chaCtrl.animBody.Play(defaultName);
			motionIK = new MotionIK(chaCtrl, ikData.Copy());
			motionIK.Calc(defaultName);
		}
	}

	public class Expression
	{
		public class Pattern : ICloneable
		{
			public int ptn;

			public bool blend;

			public void Initialize()
			{
				ptn = 0;
				blend = true;
			}

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
					array.SafeProc(num++, delegate(string s)
					{
						ptn = int.Parse(s);
					});
					array.SafeProc(num++, delegate(string s)
					{
						blend = bool.Parse(s);
					});
				}
				catch (Exception)
				{
					if (isThrow)
					{
						throw new Exception("Expression Pattern:" + string.Join(",", array));
					}
				}
			}

			public object Clone()
			{
				return MemberwiseClone();
			}
		}

		public Pattern eyebrow = new Pattern();

		public Pattern eyes = new Pattern();

		public Pattern mouth = new Pattern();

		public float eyebrowOpen = 1f;

		public float eyesOpen = 1f;

		public float mouthOpen = 1f;

		public int eyesLook;

		public float hohoAkaRate;

		public bool isHighlight = true;

		public int tearsLv;

		public bool isBlink = true;

		protected bool useEyebrow;

		protected bool useEyes;

		protected bool useMouth;

		protected bool useEyebrowOpen;

		protected bool useEyesOpen;

		protected bool useMouthOpen;

		protected bool useEyesLook;

		protected bool useHohoAkaRate;

		protected bool useIsHighlight;

		protected bool useTearsLv;

		protected bool useIsBlink;

		public bool isChangeSkip { private get; set; }

		public static void Copy(Expression src, Expression dest)
		{
			if (!src.eyebrow.SafeProc(delegate(Pattern p)
			{
				dest.eyebrow = (Pattern)p.Clone();
			}))
			{
				dest.eyebrow = new Pattern();
			}
			if (!src.eyes.SafeProc(delegate(Pattern p)
			{
				dest.eyes = (Pattern)p.Clone();
			}))
			{
				dest.eyes = new Pattern();
			}
			if (!src.mouth.SafeProc(delegate(Pattern p)
			{
				dest.mouth = (Pattern)p.Clone();
			}))
			{
				dest.mouth = new Pattern();
			}
			dest.eyebrowOpen = src.eyebrowOpen;
			dest.eyesOpen = src.eyesOpen;
			dest.mouthOpen = src.mouthOpen;
			dest.eyesLook = src.eyesLook;
			dest.hohoAkaRate = src.hohoAkaRate;
			dest.isHighlight = src.isHighlight;
			dest.tearsLv = src.tearsLv;
			dest.isBlink = src.isBlink;
			dest.isChangeSkip = src.isChangeSkip;
			dest.useEyebrow = src.useEyebrow;
			dest.useEyes = src.useEyes;
			dest.useMouth = src.useMouth;
			dest.useEyebrowOpen = src.useEyebrowOpen;
			dest.useEyesOpen = src.useEyesOpen;
			dest.useMouthOpen = src.useMouthOpen;
			dest.useEyesLook = src.useEyesLook;
			dest.useHohoAkaRate = src.useHohoAkaRate;
			dest.useIsHighlight = src.useIsHighlight;
			dest.useTearsLv = src.useTearsLv;
			dest.useIsBlink = src.useIsBlink;
		}

		public Expression()
		{
		}

		public Expression(Expression other)
		{
			Copy(other, this);
		}

		public Expression(string[] args, ref int cnt)
		{
			Initialize(args, ref cnt);
		}

		public Expression(string[] args)
		{
			int cnt = 0;
			Initialize(args, ref cnt);
		}

		public virtual void Initialize(string[] args, ref int cnt, bool isThrow = false)
		{
			try
			{
				useEyebrow = args.SafeProc(cnt++, delegate(string s)
				{
					eyebrow = new Pattern(s, isThrow: true);
				});
				useEyes = args.SafeProc(cnt++, delegate(string s)
				{
					eyes = new Pattern(s, isThrow: true);
				});
				useMouth = args.SafeProc(cnt++, delegate(string s)
				{
					mouth = new Pattern(s, isThrow: true);
				});
				useEyebrowOpen = args.SafeProc(cnt++, delegate(string s)
				{
					eyebrowOpen = float.Parse(s);
				});
				useEyesOpen = args.SafeProc(cnt++, delegate(string s)
				{
					eyesOpen = float.Parse(s);
				});
				useMouthOpen = args.SafeProc(cnt++, delegate(string s)
				{
					mouthOpen = float.Parse(s);
				});
				useEyesLook = args.SafeProc(cnt++, delegate(string s)
				{
					eyesLook = int.Parse(s);
				});
				useHohoAkaRate = args.SafeProc(cnt++, delegate(string s)
				{
					hohoAkaRate = float.Parse(s);
				});
				useIsHighlight = args.SafeProc(cnt++, delegate(string s)
				{
					isHighlight = bool.Parse(s);
				});
				useTearsLv = args.SafeProc(cnt++, delegate(string s)
				{
					tearsLv = int.Parse(s);
				});
				useIsBlink = args.SafeProc(cnt++, delegate(string s)
				{
					isBlink = bool.Parse(s);
				});
			}
			catch (Exception)
			{
				if (isThrow)
				{
					throw new Exception("Expression:" + string.Join(",", args));
				}
			}
		}

		public void Change(ChaControl chaCtrl)
		{
			if (!isChangeSkip || useEyebrow)
			{
				chaCtrl.ChangeEyebrowPtn(eyebrow.ptn, eyebrow.blend);
			}
			if (!isChangeSkip || useEyes)
			{
				chaCtrl.ChangeEyesPtn(eyes.ptn, eyes.blend);
			}
			if (!isChangeSkip || useMouth)
			{
				chaCtrl.ChangeMouthPtn(mouth.ptn, mouth.blend);
				int ptn = mouth.ptn;
				bool disable = (uint)(ptn - 21) <= 1u;
				chaCtrl.DisableShapeMouth(disable);
			}
			if (!isChangeSkip || useEyebrowOpen)
			{
				chaCtrl.ChangeEyebrowOpenMax(eyebrowOpen);
			}
			if (!isChangeSkip || useEyesOpen)
			{
				chaCtrl.ChangeEyesOpenMax(eyesOpen);
			}
			if (!isChangeSkip || useMouthOpen)
			{
				chaCtrl.ChangeMouthOpenMax(mouthOpen);
			}
			if ((!isChangeSkip || useEyesLook) && eyesLook != -1)
			{
				chaCtrl.ChangeLookEyesPtn(eyesLook);
			}
			if (!isChangeSkip || useHohoAkaRate)
			{
				chaCtrl.ChangeHohoAkaRate(hohoAkaRate);
			}
			if (!isChangeSkip || useIsHighlight)
			{
				chaCtrl.HideEyeHighlight(!isHighlight);
			}
			if (!isChangeSkip || useTearsLv)
			{
				chaCtrl.chaFile.status.tearsLv = (byte)tearsLv;
			}
			if (!isChangeSkip || useIsBlink)
			{
				chaCtrl.ChangeEyesBlinkFlag(isBlink);
			}
		}

		public void Copy(Expression dest)
		{
			Copy(this, dest);
		}
	}

	public Dictionary<int, Dictionary<string, Expression>> expCharaDic = new Dictionary<int, Dictionary<string, Expression>>();

	public static bool isAddH => _isAddH ?? (_isAddH = AssetBundleCheck.IsManifest("add01")).Value;

	private static bool? _isAddH { get; set; } = null;


	public static bool isAdd70 => _isAdd70 ?? (_isAdd70 = AssetBundleCheck.IsManifest("add70")).Value;

	private static bool? _isAdd70 { get; set; } = null;


	public static string SaveFileName { get; set; } = "";


	public static Dictionary<string, int> ClassRoomNameIndexPair { get; } = new Dictionary<string, int>
	{
		{ "ホテル", 0 },
		{ "民宿", 1 },
		{ "合宿所", 2 }
	};


	public static string ClubNameMan => "研修旅行";

	public static Dictionary<int, ClubInfo.Param> ClubInfos => clubInfoDic;

	private static Dictionary<int, ClubInfo.Param> clubInfoDic { get; } = new Dictionary<int, ClubInfo.Param>();


	public static bool IsLoadCheck => !((IReadOnlyCollection<string>)(object)WorldData.SaveFiles).IsNullOrEmpty();

	public static GlobalData globalData { get; private set; }

	public RankingData rankSaveData { get; } = new RankingData();


	public static bool isAutoSavePossible { get; set; }

	public static WorldData saveData { get; private set; }

	public static Player Player => saveData.player;

	public static List<Heroine> HeroineList => saveData.heroineList;

	public CameraEffector cameraEffector
	{
		get
		{
			Camera camera = nowCamera;
			if (camera == null)
			{
				return null;
			}
			if (isCameraChanged)
			{
				_cameraEffector = camera.GetComponent<CameraEffector>();
			}
			return _cameraEffector;
		}
	}

	private CameraEffector _cameraEffector { get; set; }

	public Camera nowCamera
	{
		get
		{
			if (_nowCamera != Camera.main)
			{
				_nowCamera = Camera.main;
				isCameraChanged = true;
			}
			return _nowCamera;
		}
	}

	private Camera _nowCamera { get; set; }

	private bool isCameraChanged { get; set; }

	public AnimePack advAnimePack { get; private set; }

	public Dictionary<string, List<ScenarioData.Param>> scenarioParameterDic { get; private set; }

	public static byte CharaDataToSex(SaveData.CharaData charaData)
	{
		return (!(charaData is Player)) ? ((byte)1) : ((byte)0);
	}

	public static int SchoolYear(int schoolClass)
	{
		switch (schoolClass)
		{
		case 0:
			return 1;
		case 1:
		case 2:
			return 2;
		case 3:
			return 3;
		default:
			return 0;
		}
	}

	public static ClubInfo.Param GetClubInfo(SaveData.CharaData charaData)
	{
		clubInfoDic.TryGetValue(charaData.clubActivities, out var value);
		return value;
	}

	public static void LoadFromTextAsset(Player player)
	{
		if (!player.charFileInitialized)
		{
			player.charFileInitialized = true;
			player.charFile.LoadFromAssetBundle("custom/presets_m_00.unity3d", "ill_Default_Male");
			player.ChaFileUpdate();
		}
	}

	public static void LoadFromTextAsset(int fixID, Heroine heroine, TextAsset ta)
	{
		if (!heroine.charFileInitialized)
		{
			heroine.charFileInitialized = true;
			heroine.charFile.LoadFromTextAsset(ta);
			heroine.ChaFileUpdate();
			heroine.fixCharaID = fixID;
		}
	}

	public static Dictionary<int, TextAsset> GetFixCharaTextAsset()
	{
		Dictionary<int, TextAsset> ret = new Dictionary<int, TextAsset>();
		List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath("action/fixchara/", subdirCheck: true);
		assetBundleNameListFromPath.Sort();
		assetBundleNameListFromPath.ForEach(delegate(string file)
		{
			TextAsset[] allAssets = AssetBundleManager.LoadAllAsset(file, typeof(TextAsset)).GetAllAssets<TextAsset>();
			foreach (TextAsset textAsset in allAssets)
			{
				if (int.TryParse(textAsset.name.Replace("c", string.Empty), out var result))
				{
					ret[result] = textAsset;
				}
			}
			AssetBundleManager.UnloadAssetBundle(file, isUnloadForceRefCount: false);
		});
		return ret;
	}

	public static List<Heroine> CreateFixCharaList(params int[] removeFixCharaIDs)
	{
		List<Heroine> list = (from p in GetFixCharaTextAsset()
			where !removeFixCharaIDs.Contains(p.Key)
			select p).Select(delegate(KeyValuePair<int, TextAsset> p)
		{
			Heroine heroine = new Heroine(isRandomize: false);
			LoadFromTextAsset(p.Key, heroine, p.Value);
			return heroine;
		}).ToList();
		list.Sort((Heroine p1, Heroine p2) => p2.fixCharaID.CompareTo(p1.fixCharaID));
		return list;
	}

	public static bool IsSameAccommodation(SaveData.CharaData charaData)
	{
		return Player.schoolClass == charaData.schoolClass;
	}

	public static bool IsRegulate(bool isAddSceneCheck)
	{
		bool flag = false;
		if (SingletonInitializer<Scene>.initialized)
		{
			flag |= Scene.IsNowLoadingFade;
			if (isAddSceneCheck)
			{
				flag |= !Scene.AddSceneName.IsNullOrEmpty();
				flag |= Scene.IsOverlap;
			}
		}
		ActionScene actionScene = SingletonInitializer<ActionScene>.instance;
		if (actionScene != null && actionScene.Map != null)
		{
			flag |= actionScene.Map.isMapLoading;
		}
		return flag;
	}

	public static void Save()
	{
		Save(SaveFileName);
	}

	public static void Save(string fileName)
	{
		saveData.Save(fileName);
	}

	public static void Load()
	{
		Load(SaveFileName);
	}

	public static void Load(string fileName)
	{
		saveData = WorldData.Load(fileName);
	}

	public static void Delete(string fileName)
	{
		saveData.Delete(fileName);
	}

	public static void AutoSave(int saveTiming, int area)
	{
		if (isAutoSavePossible)
		{
			saveData.AutoSave(saveTiming, area);
			InformationUI.Set("オートセーブしました", InformationUI.Mode.Normal);
		}
	}

	public static void NewGame()
	{
		saveData = new WorldData();
		saveData.player.Randomize();
		SaveFileName = string.Empty;
		isAutoSavePossible = false;
		globalData.tutorialHash.Remove(10);
	}

	public void ParameterCorrectValues()
	{
		int min = 0;
		int max = 100;
		foreach (Heroine heroine in saveData.heroineList)
		{
			int max2 = SingletonInitializer<ActionScene>.instance.stepUpDic[Mathf.Clamp((heroine.fixCharaID < 0) ? heroine.relationNPC : heroine.relation, 0, 2)];
			heroine.favor = Mathf.Clamp(heroine.favor, min, max2);
			heroine.lewdness = Mathf.Clamp(heroine.lewdness, min, max);
		}
	}

	protected override void Initialize()
	{
		globalData = new GlobalData();
		WorldData.InitializeCall();
		globalData = GlobalData.Load();
		List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath("action/list/clubinfo/", subdirCheck: true);
		assetBundleNameListFromPath.Sort();
		assetBundleNameListFromPath.ForEach(delegate(string file)
		{
			ClubInfo[] allAssets3 = AssetBundleManager.LoadAllAsset(file, typeof(ClubInfo)).GetAllAssets<ClubInfo>();
			for (int k = 0; k < allAssets3.Length; k++)
			{
				foreach (ClubInfo.Param item in allAssets3[k].param)
				{
					clubInfoDic[item.ID] = item;
				}
			}
			AssetBundleManager.UnloadAssetBundle(file, isUnloadForceRefCount: false);
		});
		List<string> assetBundleNameListFromPath2 = CommonLib.GetAssetBundleNameListFromPath("etcetra/list/exp/", subdirCheck: true);
		assetBundleNameListFromPath2.Sort();
		assetBundleNameListFromPath2.ForEach(delegate(string file)
		{
			ExcelData[] allAssets2 = AssetBundleManager.LoadAllAsset(file, typeof(ExcelData)).GetAllAssets<ExcelData>();
			foreach (ExcelData excelData in allAssets2)
			{
				int key = int.Parse(excelData.name.Replace("c", string.Empty));
				if (!expCharaDic.TryGetValue(key, out var value))
				{
					value = new Dictionary<string, Expression>();
				}
				LoadExpExcelData(value, excelData);
				expCharaDic[key] = value;
			}
			AssetBundleManager.UnloadAssetBundle(file, isUnloadForceRefCount: false);
		});
		string assetName = "cf_adv_00_00";
		RuntimeAnimatorController asset = AssetBundleManager.LoadAsset("adv/motion/controller/adv/00.unity3d", assetName, typeof(RuntimeAnimatorController)).GetAsset<RuntimeAnimatorController>();
		MotionIKData ikData = new MotionIKData(AssetBundleManager.LoadAsset("adv/motion/iklist/00.unity3d", assetName, typeof(TextAsset)).GetAsset<TextAsset>());
		advAnimePack = new AnimePack(asset, ikData, "Idle_00_00");
		scenarioParameterDic = new Dictionary<string, List<ScenarioData.Param>>();
		List<string> assetBundleNameListFromPath3 = CommonLib.GetAssetBundleNameListFromPath("adv/scenario/common_param/", subdirCheck: true);
		assetBundleNameListFromPath3.Sort();
		assetBundleNameListFromPath3.ForEach(delegate(string file)
		{
			ScenarioData[] allAssets = AssetBundleManager.LoadAllAsset(file, typeof(ScenarioData)).GetAllAssets<ScenarioData>();
			foreach (ScenarioData scenarioData in allAssets)
			{
				scenarioParameterDic[scenarioData.name] = scenarioData.list;
			}
			AssetBundleManager.UnloadAssetBundle(file, isUnloadForceRefCount: false);
		});
		NewGame();
		(from _ in this.UpdateAsObservable()
			where saveData != null
			where saveData.player != null
			select _).Subscribe(delegate
		{
			float num = saveData.player.playTimeCalc + Time.unscaledDeltaTime;
			saveData.player.playTimeCalc = num % 1f;
			uint src = saveData.player.playTime;
			RankingData.ValueCheck(ref src, src + (uint)num);
			saveData.player.playTime = src;
		});
		this.OnDestroyAsObservable().Subscribe(delegate
		{
			AlreadyReadInfo.Save();
			globalData.Save();
		});
	}

	public static void LoadExpExcelData(Dictionary<string, Expression> dic, ExcelData excelData)
	{
		foreach (ExcelData.Param item in excelData.list)
		{
			if (item.list.Any())
			{
				Expression expression = new Expression(item.list.Skip(1).ToArray());
				expression.isChangeSkip = true;
				dic[item.list[0]] = expression;
			}
		}
	}

	public static Expression GetExpression(Dictionary<string, Expression> dic, string key)
	{
		dic.TryGetValue(key, out var value);
		return value;
	}

	public static Dictionary<string, Expression> ExpCharaListLoadData(int personality)
	{
		Dictionary<string, Expression> dic = new Dictionary<string, Expression>();
		CommonLib.GetAssetBundleNameListFromPath("etcetra/list/exp/", subdirCheck: true).ForEach(delegate(string file)
		{
			ExcelData[] allAssets = AssetBundleManager.LoadAllAsset(file, typeof(ExcelData)).GetAllAssets<ExcelData>();
			foreach (ExcelData excelData in allAssets)
			{
				if (int.Parse(excelData.name.Replace("c", string.Empty)) == personality)
				{
					LoadExpExcelData(dic, excelData);
				}
			}
			AssetBundleManager.UnloadAssetBundle(file, isUnloadForceRefCount: false);
		});
		return dic;
	}

	public Expression GetExpression(int personality, string key)
	{
		if (!expCharaDic.TryGetValue(personality, out var value))
		{
			return null;
		}
		return GetExpression(value, key);
	}
}
