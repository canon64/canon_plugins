using System;
using System.Collections.Generic;
using System.Linq;
using Illusion;
using Localize.Translate;
using UnityEngine;

namespace Manager;

public class Character : Singleton<Character>
{
	public class RandamNameInfo
	{
		public List<string> lstLastNameH = new List<string>();

		public List<string> lstLastNameK = new List<string>();

		public List<string> lstFemaleFirstNameH = new List<string>();

		public List<string> lstFemaleFirstNameK = new List<string>();

		public List<string> lstMaleFirstNameH = new List<string>();

		public List<string> lstMaleFirstNameK = new List<string>();
	}

	private class Initializable : InitializeSolution.IInitializable
	{
		private bool initialized;

		private Character c { get; }

		bool InitializeSolution.IInitializable.initialized => initialized;

		public Initializable(Character c)
		{
			this.c = c;
		}

		void InitializeSolution.IInitializable.Initialize()
		{
			if (initialized)
			{
				return;
			}
			initialized = true;
			if (!Localize.Translate.Manager.isTranslate)
			{
				return;
			}
			Localize.Translate.Manager.SCENE_ID sceneID = Localize.Translate.Manager.SCENE_ID.CUSTOM_LIST;
			Dictionary<int, Dictionary<int, Data.Param>> translater = Localize.Translate.Manager.LoadScene(sceneID, null);
			Utils.Enum<ChaListDefine.CategoryNo>.Each(delegate(ChaListDefine.CategoryNo e)
			{
				foreach (KeyValuePair<int, ListInfoBase> item in chaListCtrl.GetCategoryInfo(e))
				{
					ListInfoBase value = item.Value;
					Data.Param param = translater.Get(value.Category).SafeGet(value.Id);
					if (param != null)
					{
						Dictionary<int, string> dictInfo = value.dictInfo;
						int key = 41;
						if (!param.text.IsNullOrEmpty() && dictInfo.ContainsKey(key))
						{
							dictInfo[key] = param.text;
						}
						if (!param.Bundle.IsNullOrEmpty() && !param.asset.IsNullOrEmpty())
						{
							int key2 = 67;
							int key3 = 68;
							if (dictInfo.ContainsKey(key2) && dictInfo.ContainsKey(key3))
							{
								dictInfo[key2] = param.Bundle;
								dictInfo[key3] = param.asset;
							}
						}
					}
				}
			});
			Localize.Translate.Manager.DisposeScene(sceneID, isForce: false);
		}
	}

	private int rampId = 1;

	private int rampHId;

	private int shaderType;

	private float shadowDepth = 0.26f;

	private float lineDepth = 1f;

	private float lineWidth = 0.307f;

	public static bool isMod { get; set; }

	public static ChaListControl chaListCtrl { get; } = new ChaListControl();


	public static ChaLoadingTask loading => _loading ?? (_loading = new ChaLoadingTask());

	public static ChaLoadingTask _loading { get; set; }

	public static string mainManifestName { get; } = "abdata";


	public static ChaFileControl editChara { get; set; }

	public static SortedDictionary<int, ChaControl> dictEntryChara { get; } = new SortedDictionary<int, ChaControl>();


	public static List<ChaControl> ChaControls => chaControls;

	private static List<ChaControl> chaControls { get; } = new List<ChaControl>();


	public static bool enableCorrectArmSize { get; set; }

	public static bool enableCorrectHandSize { get; set; }

	public static bool enableCharaLoadGCClear { get; set; } = true;


	public static bool enableCorrectHandTwoShotSize { get; set; } = false;


	public static Vector3 correctHandTwoShotSize { get; set; } = new Vector3(0.92f, 0.85f, 0.85f);


	public RandamNameInfo randamNameInfo { get; private set; }

	private static List<AssetBundleData> lstLoadAssetBundleInfo { get; } = new List<AssetBundleData>();


	private static bool loadAssetBundle { get; set; }

	public static void BeginLoadAssetBundle(bool _resources = false)
	{
		if (!loadAssetBundle)
		{
			lstLoadAssetBundleInfo.Clear();
			loadAssetBundle = true;
			if (_resources)
			{
				enableCharaLoadGCClear = false;
			}
		}
	}

	public static void AddLoadAssetBundle(string assetBundleName, string manifestName)
	{
		if (manifestName.IsNullOrEmpty())
		{
			manifestName = mainManifestName;
		}
		AssetBundleManifestData assetBundleManifestData = new AssetBundleManifestData();
		assetBundleManifestData.SetBundle(assetBundleName);
		assetBundleManifestData.manifest = manifestName;
		lstLoadAssetBundleInfo.Add(assetBundleManifestData);
	}

	public static AsyncOperation EndLoadAssetBundle(bool forceRemove = false)
	{
		if (!loadAssetBundle)
		{
			return null;
		}
		foreach (AssetBundleData item in lstLoadAssetBundleInfo)
		{
			AssetBundleManager.UnloadAssetBundle(item.bundle, isUnloadForceRefCount: true, null, forceRemove);
		}
		GC.Collect();
		lstLoadAssetBundleInfo.Clear();
		loadAssetBundle = false;
		return Resources.UnloadUnusedAssets();
	}

	public static ChaControl CreateFemale(GameObject parent, int id, ChaFileControl _chaFile = null, bool hiPoly = true)
	{
		return CreateChara(1, 0, parent, id, _chaFile, hiPoly);
	}

	public static ChaControl CreateMale(int _exType, GameObject parent, int id, ChaFileControl _chaFile = null, bool hiPoly = true)
	{
		return CreateChara(0, _exType, parent, id, _chaFile, hiPoly);
	}

	public static ChaControl CreateChara(byte _sex, int _exType, GameObject parent, int id, ChaFileControl _chaFile = null, bool hiPoly = true)
	{
		int num = 0;
		int num2 = 1;
		foreach (KeyValuePair<int, ChaControl> item in dictEntryChara)
		{
			if (item.Value.sex == _sex)
			{
				num2++;
			}
			if (num != item.Key)
			{
				break;
			}
			num++;
		}
		GameObject gameObject = new GameObject(((_sex == 0) ? "chaM_" : "chaF_") + num2.ToString("000"));
		if ((bool)parent)
		{
			gameObject.transform.SetParent(parent.transform, worldPositionStays: false);
		}
		ChaControl chaControl = gameObject.AddComponent<ChaControl>();
		if ((bool)chaControl)
		{
			chaControl.Initialize(_sex, _exType, hiPoly, gameObject, id, num, _chaFile);
		}
		dictEntryChara.Add(num, chaControl);
		chaControls.Add(chaControl);
		return chaControl;
	}

	public static bool IsChara(ChaControl cha)
	{
		return chaControls.Contains(cha);
	}

	public static bool DeleteChara(ChaControl cha, bool entryOnly = false)
	{
		foreach (KeyValuePair<int, ChaControl> item in dictEntryChara)
		{
			if (item.Value == cha)
			{
				if (!entryOnly)
				{
					item.Value.name = "Delete_Reserve";
					item.Value.transform.SetParent(null);
					UnityEngine.Object.Destroy(item.Value.gameObject);
				}
				dictEntryChara.Remove(item.Key);
				chaControls.Remove(cha);
				return true;
			}
		}
		return false;
	}

	public static void DeleteCharaAll()
	{
		foreach (KeyValuePair<int, ChaControl> item in dictEntryChara)
		{
			if ((bool)item.Value)
			{
				item.Value.name = "Delete_Reserve";
				item.Value.transform.SetParent(null);
				UnityEngine.Object.Destroy(item.Value.gameObject);
			}
		}
		dictEntryChara.Clear();
		chaControls.Clear();
	}

	public static List<ChaControl> GetCharaList(byte _sex)
	{
		return chaControls.Where((ChaControl c) => c.sex == _sex).ToList();
	}

	public static ChaControl GetChara(byte _sex, int _id)
	{
		try
		{
			return chaControls.Where((ChaControl s) => s.sex == _sex).First((ChaControl v) => v.chaID == _id);
		}
		catch (ArgumentNullException)
		{
		}
		catch (InvalidOperationException)
		{
		}
		return null;
	}

	public static ChaControl GetChara(int _id)
	{
		try
		{
			return chaControls.First((ChaControl v) => v.chaID == _id);
		}
		catch (ArgumentNullException)
		{
		}
		catch (InvalidOperationException)
		{
		}
		return null;
	}

	public static ChaControl GetCharaFromLoadNo(int _no)
	{
		try
		{
			return chaControls.First((ChaControl v) => v.loadNo == _no);
		}
		catch (ArgumentNullException)
		{
		}
		catch (InvalidOperationException)
		{
		}
		return null;
	}

	public static void ChangeRootParent(ChaControl cha, Transform trfNewParent)
	{
		if (null != cha)
		{
			cha.transform.SetParent(trfNewParent, worldPositionStays: false);
		}
	}

	protected new void Awake()
	{
		if (CheckInstance())
		{
			UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
			chaListCtrl.LoadListInfoAll();
			Localize.Translate.Manager.initializeSolution.Add(new Initializable(this));
			randamNameInfo = new RandamNameInfo();
			string text = "list/random_name/random_name.unity3d";
			if (Localize.Translate.Manager.isTranslate)
			{
				text = Localize.Translate.Manager.DefaultData.BundlePath + text;
			}
			List<ExcelData.Param> source = ChaListControl.LoadExcelData(text, "random_name", 2, 1);
			randamNameInfo.lstLastNameH = (from x in source
				select x.list[0] into x
				where x != "0"
				select x).ToList();
			randamNameInfo.lstLastNameK = (from x in source
				select x.list[1] into x
				where x != "0"
				select x).ToList();
			randamNameInfo.lstFemaleFirstNameH = (from x in source
				select x.list[2] into x
				where x != "0"
				select x).ToList();
			randamNameInfo.lstFemaleFirstNameK = (from x in source
				select x.list[3] into x
				where x != "0"
				select x).ToList();
			randamNameInfo.lstMaleFirstNameH = (from x in source
				select x.list[4] into x
				where x != "0"
				select x).ToList();
			randamNameInfo.lstMaleFirstNameK = (from x in source
				select x.list[5] into x
				where x != "0"
				select x).ToList();
		}
	}

	protected void Start()
	{
		rampId = Config.GraphicData.rampId;
		shaderType = Config.GraphicData.shaderType;
		shadowDepth = Config.GraphicData.shadowDepth;
		lineDepth = Config.GraphicData.lineDepth;
		lineWidth = Config.GraphicData.lineWidth;
		UpdateGlobalShader(ramp: true, shadowD: true, lineD: true, lineW: true, rampH: true, shaderT: true);
		enableCorrectHandSize = true;
		Localize.Translate.Manager.initializeSolution.Execute();
	}

	protected void Update()
	{
		bool ramp = rampId != Config.GraphicData.rampId;
		bool rampH = false;
		bool shaderT = shaderType != Config.GraphicData.shaderType;
		bool shadowD = shadowDepth != Config.GraphicData.shadowDepth;
		bool lineD = lineDepth != Config.GraphicData.lineDepth;
		bool lineW = lineWidth != Config.GraphicData.lineWidth;
		UpdateGlobalShader(ramp, shadowD, lineD, lineW, rampH, shaderT);
		foreach (ChaControl chaControl in chaControls)
		{
			chaControl.UpdateForce();
		}
	}

	protected void LateUpdate()
	{
		foreach (ChaControl chaControl in chaControls)
		{
			chaControl.LateUpdateForce();
		}
	}

	public void UpdateGlobalShader(bool ramp, bool shadowD, bool lineD, bool lineW, bool rampH, bool shaderT)
	{
		if (ramp)
		{
			rampId = Config.GraphicData.rampId;
			Texture2D texture2D = null;
			if (chaListCtrl.GetFilePath(ChaListDefine.CategoryNo.mt_ramp, rampId, ChaListDefine.KeyType.MainTexAB, ChaListDefine.KeyType.MainTex, out var bundle, out var asset))
			{
				if ("0" != bundle && "0" != asset)
				{
					texture2D = CommonLib.LoadAsset<Texture2D>(bundle, asset);
				}
				if ((bool)texture2D)
				{
					ChaShader.ChangeRampTexture(texture2D);
				}
			}
		}
		if (shaderT)
		{
			shaderType = Config.GraphicData.shaderType;
			ChaShader.ChangeShaderType(shaderType);
		}
		if (shadowD)
		{
			shadowDepth = Config.GraphicData.shadowDepth;
			ChaShader.ChangeAmbientShaodwColor(shadowDepth);
		}
		if (lineD)
		{
			lineDepth = Config.GraphicData.lineDepth;
			ChaShader.ChangeLineColor(lineDepth);
		}
		if (lineW)
		{
			lineWidth = Config.GraphicData.lineWidth;
			ChaShader.ChangeLineWidth(lineWidth);
		}
	}

	private void OnDestroy()
	{
		chaListCtrl.SaveItemID();
	}
}
