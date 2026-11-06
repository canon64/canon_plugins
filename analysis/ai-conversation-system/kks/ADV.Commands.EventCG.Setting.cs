using System;
using System.Linq;
using ADV.EventCG;
using Illusion.Extensions;
using UnityEngine;

namespace ADV.Commands.EventCG;

public class Setting : CommandBase
{
	private ADV.EventCG.Data data;

	public override string[] ArgsLabel => new string[2] { "Bundle", "Asset" };

	public override string[] ArgsDefault => new string[2]
	{
		string.Empty,
		string.Empty
	};

	public override void Do()
	{
		base.Do();
		Common.Release(base.scenario);
		int num = 0;
		string bundle = args[num++];
		string asset = args[num++];
		Action action = delegate
		{
			GameObject asset2 = AssetBundleManager.LoadAsset(bundle, asset, typeof(GameObject)).GetAsset<GameObject>();
			data = UnityEngine.Object.Instantiate(asset2, base.scenario.commandController.EventCGRoot, worldPositionStays: false).GetComponent<ADV.EventCG.Data>();
			data.name = asset2.name;
			AssetBundleManager.UnloadAssetBundle(bundle, isUnloadForceRefCount: false);
		};
		if (!bundle.IsNullOrEmpty())
		{
			action();
		}
		else
		{
			foreach (string item in from x in CommonLib.GetAssetBundleNameListFromPath("adv/eventcg/", subdirCheck: true)
				orderby x descending
				select x)
			{
				if (asset.Check(ignoreCase: true, AssetBundleCheck.GetAllAssetName(item, _WithExtension: false)) != -1)
				{
					bundle = item;
					action();
					break;
				}
			}
		}
		base.scenario.commandController.useCorrectCamera = false;
		data.camRoot = base.scenario.AdvCamera.transform;
		CommandController commandController = base.scenario.commandController;
		data.SetChaRoot(commandController.Character, commandController.Characters);
		data.Next(0, commandController.Characters);
	}
}
