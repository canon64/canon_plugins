using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class AnimatorLayerCtrl : MonoBehaviour
{
	public bool isInit;

	[SerializeField]
	private Dictionary<string, List<int>> dicLayer = new Dictionary<string, List<int>>();

	[SerializeField]
	private Animator anim;

	private static List<int> lstOmitLayer = new List<int>();

	private static bool loadOmitList = false;

	public bool Init(Animator _animator)
	{
		anim = _animator;
		if (!loadOmitList)
		{
			LoadOmit();
		}
		return true;
	}

	public bool Load(string _strAssetFolderPath, string _file)
	{
		isInit = false;
		dicLayer.Clear();
		ResetAllLayer();
		if (!anim)
		{
			return false;
		}
		if (_file == null || _file == "")
		{
			return false;
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendFormat("al_{0}", _file);
		foreach (string item in HFlag.lstABName)
		{
			if (!AssetBundleCheck.IsFile(item, stringBuilder.ToString()) || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(item, stringBuilder.ToString())))
			{
				continue;
			}
			AnimationLayerData animationLayerData = CommonLib.LoadAsset<AnimationLayerData>(item, stringBuilder.ToString(), clone: false, "", check: false);
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
			if (animationLayerData == null)
			{
				continue;
			}
			foreach (AnimationLayerData.Param item2 in animationLayerData.param)
			{
				if (!dicLayer.ContainsKey(item2.anim))
				{
					dicLayer.Add(item2.anim, new List<int>());
				}
				List<int> list = dicLayer[item2.anim];
				list.Clear();
				list.AddRange(item2.layer);
			}
		}
		isInit = true;
		return true;
	}

	private static bool LoadOmit()
	{
		lstOmitLayer.Clear();
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("omitmask");
		foreach (string item in HFlag.lstABName)
		{
			if (!AssetBundleCheck.IsFile(item, stringBuilder.ToString()) || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(item, stringBuilder.ToString())))
			{
				continue;
			}
			OmitAnimationLayerData omitAnimationLayerData = CommonLib.LoadAsset<OmitAnimationLayerData>(item, stringBuilder.ToString(), clone: false, "", check: false);
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
			if (omitAnimationLayerData == null)
			{
				continue;
			}
			foreach (OmitAnimationLayerData.Param item2 in omitAnimationLayerData.param)
			{
				foreach (int item3 in item2.layer)
				{
					if (!lstOmitLayer.Contains(item3))
					{
						lstOmitLayer.Add(item3);
					}
				}
			}
		}
		loadOmitList = true;
		return true;
	}

	public static void EndProc()
	{
		lstOmitLayer.Clear();
		loadOmitList = false;
	}

	public bool ResetAllLayer()
	{
		if (!anim)
		{
			return false;
		}
		for (int i = 0; i < anim.layerCount; i++)
		{
			if (!lstOmitLayer.Contains(i))
			{
				anim.SetLayerWeight(i, 0f);
			}
		}
		return true;
	}

	public bool Proc(string _nameNextAnimation)
	{
		if (!isInit)
		{
			return false;
		}
		ResetAllLayer();
		if (!dicLayer.ContainsKey(_nameNextAnimation))
		{
			return false;
		}
		foreach (int item in dicLayer[_nameNextAnimation])
		{
			if (anim.layerCount > item)
			{
				anim.SetLayerWeight(item, 1f);
			}
		}
		return true;
	}
}
