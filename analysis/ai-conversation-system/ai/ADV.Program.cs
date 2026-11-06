using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Manager;
using UnityEngine;

namespace ADV;

public static class Program
{
	[Serializable]
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

		public Func<IEnumerator> onFadeIn { get; set; }

		public Func<IEnumerator> onFadeOut { get; set; }

		public Scene.Data.FadeType fadeType { get; set; }
	}

	public const string BASE_FOV = "23";

	public const string AdvScenarioPath = "scenario/";

	public static bool isADVActionActive => true;

	public static bool isADVScene => Singleton<Scene>.Instance.NowSceneNames.Contains("ADV");

	public static bool isADVProcessing => isADVActionActive || isADVScene;

	public static string ScenarioBundle(string file)
	{
		return "scenario/" + file + ".unity3d";
	}

	public static string FindADVBundleFilePath(string path, string asset = null)
	{
		ScenarioData data;
		return FindADVBundleFilePath(path, asset, out data);
	}

	public static string FindADVBundleFilePath(string path, string asset, out ScenarioData data)
	{
		data = null;
		string text = null;
		foreach (string item in from bundle in CommonLib.GetAssetBundleNameListFromPath($"adv/scenario/{path}/ ", subdirCheck: true)
			orderby bundle descending
			select bundle)
		{
			if (asset == null)
			{
				text = item;
			}
			else
			{
				ScenarioData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(ScenarioData)).GetAllAssets<ScenarioData>();
				foreach (ScenarioData scenarioData in allAssets)
				{
					if (scenarioData.name == asset)
					{
						text = item;
						data = scenarioData;
						break;
					}
				}
				AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: false);
			}
			if (text != null)
			{
				break;
			}
		}
		return text;
	}

	public static string FindADVBundleFilePath(int id, int category, string asset = null)
	{
		ScenarioData data;
		return FindADVBundleFilePath(id, category, asset, out data);
	}

	public static string FindADVBundleFilePath(int id, int category, string asset, out ScenarioData data)
	{
		data = null;
		string text = null;
		string text2 = $"{category:00}";
		foreach (string item in from bundle in CommonLib.GetAssetBundleNameListFromPath($"adv/scenario/c{id:00}/", subdirCheck: true)
			orderby bundle descending
			select bundle)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(item);
			if (fileNameWithoutExtension != text2)
			{
				continue;
			}
			if (asset == null)
			{
				text = item;
			}
			else
			{
				ScenarioData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(ScenarioData)).GetAllAssets<ScenarioData>();
				foreach (ScenarioData scenarioData in allAssets)
				{
					if (scenarioData.name == asset)
					{
						text = item;
						data = scenarioData;
						break;
					}
				}
				AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: false);
			}
			if (text == null)
			{
				continue;
			}
			break;
		}
		return text;
	}

	public static string FindMessageADVBundleFilePath(string path, string asset = null)
	{
		ScenarioData data;
		return FindMessageADVBundleFilePath(path, asset, out data);
	}

	public static string FindMessageADVBundleFilePath(string path, string asset, out ScenarioData data)
	{
		data = null;
		string text = null;
		foreach (string item in from bundle in CommonLib.GetAssetBundleNameListFromPath($"adv/message/{path}/", subdirCheck: true)
			orderby bundle descending
			select bundle)
		{
			if (asset == null)
			{
				text = item;
			}
			else
			{
				ScenarioData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(ScenarioData)).GetAllAssets<ScenarioData>();
				foreach (ScenarioData scenarioData in allAssets)
				{
					if (scenarioData.name == asset)
					{
						text = item;
						data = scenarioData;
						break;
					}
				}
				AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: false);
			}
			if (text != null)
			{
				break;
			}
		}
		return text;
	}

	public static IEnumerator Open(IData scene, IData openData, OpenDataProc proc = null)
	{
		if (scene == null)
		{
		}
		yield break;
	}

	public unsafe static IEnumerator ADVProcessingCheck()
	{
		if (isADVActionActive)
		{
			yield return new WaitWhile(new Func<bool>(null, (nint)(delegate*<bool>)(&Program.get_isADVActionActive)));
		}
		else
		{
			yield return new WaitWhile(new Func<bool>(null, (nint)(delegate*<bool>)(&Program.get_isADVScene)));
		}
	}

	public static IEnumerator Wait(string addSceneName)
	{
		if (Singleton<Scene>.IsInstance())
		{
			yield return new WaitWhile(() => Singleton<Scene>.Instance.AddSceneName != addSceneName);
			yield return ADVProcessingCheck();
		}
	}
}
