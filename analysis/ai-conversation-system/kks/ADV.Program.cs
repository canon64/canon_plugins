using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ActionGame;
using Cysharp.Threading.Tasks;
using Manager;
using SaveData;
using UnityEngine;

namespace ADV;

public static class Program
{
	public class Transfer
	{
		public int line { get; private set; }

		public ScenarioData.Param param { get; private set; }

		public Transfer(ScenarioData.Param param)
		{
			line = -1;
			this.param = param;
		}

		public static List<Transfer> NewList(bool multi = true, bool isSceneRegulate = false)
		{
			List<Transfer> list = new List<Transfer>();
			list.Add(Create(multi, Command.SceneFadeRegulate, isSceneRegulate.ToString()));
			return list;
		}

		public static Transfer Create(bool multi, Command command, params string[] args)
		{
			return new Transfer(new ScenarioData.Param(multi, command, args));
		}

		public static Transfer VAR(params string[] args)
		{
			return Create(multi: true, Command.VAR, args);
		}

		public static Transfer Open(params string[] args)
		{
			return Create(multi: false, Command.Open, args);
		}

		public static Transfer Close()
		{
			return Create(multi: false, Command.Close, (string[])null);
		}

		public static Transfer Text(params string[] args)
		{
			return Create(multi: false, Command.Text, args);
		}

		public static Transfer Voice(params string[] args)
		{
			return Create(multi: true, Command.Voice, args);
		}

		public static Transfer Motion(params string[] args)
		{
			return Create(multi: true, Command.Motion, args);
		}

		public static Transfer Expression(params string[] args)
		{
			return Create(multi: true, Command.Expression, args);
		}

		public static Transfer ExpressionIcon(params string[] args)
		{
			return Create(multi: true, Command.ExpressionIcon, args);
		}
	}

	public class OpenDataProc
	{
		public Action onLoad { get; set; }

		public Func<UniTask> onFadeIn { get; set; }

		public Func<UniTask> onFadeOut { get; set; }

		public FadeCanvas.Fade fadeType { get; set; }
	}

	public static string BASE_FOV { get; } = "23";


	public static bool isADVActionActive
	{
		get
		{
			bool result = false;
			ActionScene instance = SingletonInitializer<ActionScene>.instance;
			if (instance != null && instance.AdvScene != null)
			{
				result = instance.AdvScene.gameObject.activeSelf;
			}
			return result;
		}
	}

	public static bool isADVScene => Scene.NowSceneNames.Contains("ADV");

	public static bool isADVProcessing
	{
		get
		{
			if (!isADVActionActive)
			{
				return isADVScene;
			}
			return true;
		}
	}

	public static void SetParam(Player player, Heroine heroine, List<Transfer> list)
	{
		SetParam(player, list);
		SetParam(heroine, list);
	}

	public static void SetParam(SaveData.CharaData chara, List<Transfer> list)
	{
		chara?.SetADVParam(list);
	}

	public static string ScenarioBundle(string file)
	{
		return "adv/scenario/" + file + ".unity3d";
	}

	public static void SetNull(Transform transform, int version, string mapName, string nullName, BaseMap map)
	{
		if (GetNull(version, mapName, nullName, map, out var position, out var rotation))
		{
			transform.SetPositionAndRotation(position, rotation);
		}
	}

	public static bool GetNull(int version, string mapName, string nullName, BaseMap map, out Vector3 position, out Quaternion rotation)
	{
		bool result = false;
		position = Vector3.zero;
		rotation = Quaternion.identity;
		mapName = ((!(mapName == "デフォルト")) ? map.GetParam(mapName).AssetName.ToLower() : "default");
		string assetBundleName = string.Format("{0}{1:00}/{2}{3}", "map/advpos/", version, mapName, AssetBundleManager.Extension);
		GameObject asset = AssetBundleManager.LoadAsset(assetBundleName, nullName, typeof(GameObject)).GetAsset<GameObject>();
		if (asset != null)
		{
			Transform transform = asset.transform;
			position = transform.position;
			rotation = transform.rotation;
			result = true;
		}
		AssetBundleManager.UnloadAssetBundle(assetBundleName, isUnloadForceRefCount: false, null, unloadAllLoadedObjects: true);
		return result;
	}

	public static string FindADVBundleFilePath(int advNo, Heroine heroine)
	{
		string text = null;
		string bundlePath = "adv/scenario/" + heroine.ChaName + "/";
		foreach (string item in from x in CommonLib.GetAssetBundleNameListFromPath(bundlePath, subdirCheck: true).Select(delegate(string path)
			{
				int.TryParse(path.Replace(bundlePath, string.Empty).Replace(AssetBundleManager.Extension, string.Empty), out var result2);
				return (path: path, n: result2);
			})
			orderby x.n descending
			select x.path)
		{
			ScenarioData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(ScenarioData)).GetAllAssets<ScenarioData>();
			for (int i = 0; i < allAssets.Length; i++)
			{
				if (int.TryParse(allAssets[i].name, out var result) && result == advNo)
				{
					text = item;
					break;
				}
			}
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: false);
			if (text != null)
			{
				break;
			}
		}
		return text;
	}

	public static string FindADVBundleFilePath(string fileName, Heroine heroine)
	{
		string text = null;
		string bundlePath = "adv/scenario/" + heroine.ChaName + "/";
		foreach (string item in from x in CommonLib.GetAssetBundleNameListFromPath(bundlePath, subdirCheck: true).Select(delegate(string path)
			{
				int.TryParse(path.Replace(bundlePath, string.Empty).Replace(AssetBundleManager.Extension, string.Empty), out var result);
				return (path: path, n: result);
			})
			orderby x.n descending
			select x.path)
		{
			ScenarioData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(ScenarioData)).GetAllAssets<ScenarioData>();
			for (int i = 0; i < allAssets.Length; i++)
			{
				if (allAssets[i].name == fileName)
				{
					text = item;
					break;
				}
			}
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: false);
			if (text != null)
			{
				break;
			}
		}
		return text;
	}

	public static string FindADVBundleFilePath(string bundlePath, string fileName)
	{
		string text = null;
		bundlePath = "adv/scenario/" + bundlePath;
		foreach (string item in from x in CommonLib.GetAssetBundleNameListFromPath(bundlePath, subdirCheck: true).Select(delegate(string path)
			{
				int.TryParse(path.Replace(bundlePath, string.Empty).Replace(AssetBundleManager.Extension, string.Empty), out var result);
				return (path: path, n: result);
			})
			orderby x.n descending
			select x.path)
		{
			ScenarioData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(ScenarioData)).GetAllAssets<ScenarioData>();
			for (int i = 0; i < allAssets.Length; i++)
			{
				if (allAssets[i].name == fileName)
				{
					text = item;
					break;
				}
			}
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: false);
			if (text != null)
			{
				break;
			}
		}
		return text;
	}

	public static async UniTask Open(Data openData, CancellationToken token, OpenDataProc proc = null)
	{
		if (!SingletonInitializer<ActionScene>.initialized || !(SingletonInitializer<ActionScene>.instance.AdvScene != null))
		{
			await Open(null, openData, token, proc);
		}
		else
		{
			await Open(SingletonInitializer<ActionScene>.instance.AdvScene, openData, token, proc);
		}
	}

	public static async UniTask Open(ADVScene advScene, Data openData, CancellationToken token, OpenDataProc proc = null)
	{
		if (advScene != null)
		{
			Scene.commonSpace.GetOrAddComponent<CommandData>().data = openData;
			advScene.gameObject.SetActive(value: true);
			proc?.onLoad?.Invoke();
			return;
		}
		OpenADVData open = Scene.commonSpace.GetOrAddComponent<OpenADVData>();
		openData.bundleName = string.Empty;
		openData.assetName = string.Empty;
		open.data = openData;
		open.isLoad = false;
		open.isAsync = false;
		open.isFade = false;
		if (proc != null)
		{
			open.onLoad = proc.onLoad;
			open.onFadeIn = proc.onFadeIn;
			open.onFadeOut = proc.onFadeOut;
			open.fadeType = proc.fadeType;
		}
		await UniTask.WaitWhile(() => Scene.IsNowLoadingFade, PlayerLoopTiming.Update, token);
		await UniTask.WaitWhile(() => open != null, PlayerLoopTiming.Update, token);
		await UniTask.WaitWhile(() => isADVScene, PlayerLoopTiming.Update, token);
	}

	public static async UniTask ADVProcessingCheck(CancellationToken token)
	{
		if (isADVActionActive)
		{
			await UniTask.WaitWhile(() => isADVActionActive, PlayerLoopTiming.Update, token);
		}
		else
		{
			await UniTask.WaitWhile(() => isADVScene, PlayerLoopTiming.Update, token);
		}
	}

	public static async UniTask Wait(string addSceneName, CancellationToken token)
	{
		if (SingletonInitializer<Scene>.initialized)
		{
			GameObject commonSpace = Scene.commonSpace;
			await UniTask.WaitWhile(() => commonSpace.GetComponent<OpenADVData>() != null, PlayerLoopTiming.Update, token);
			await UniTask.WaitWhile(() => Scene.AddSceneName != addSceneName, PlayerLoopTiming.Update, token);
			await ADVProcessingCheck(token);
		}
	}

	public static void SetADVCharacter(int no, SaveData.CharaData charaData, TextScenario scenario, CharaData.MotionReserver motionReserver = null, bool hiPoly = true)
	{
		scenario.commandController.AddChara(no, charaData, charaData.chaCtrl, motionReserver, hiPoly);
	}
}
