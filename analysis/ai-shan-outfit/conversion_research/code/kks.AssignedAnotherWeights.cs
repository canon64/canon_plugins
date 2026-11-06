using System.Collections.Generic;
using System.Linq;
using ChaLoad;
using IllusionUtility.GetUtility;
using UnityEngine;

public class AssignedAnotherWeights
{
	public Dictionary<string, GameObject> dictBone { get; private set; }

	public AssignedAnotherWeights()
	{
		dictBone = new Dictionary<string, GameObject>();
	}

	public void Release()
	{
		dictBone.Clear();
	}

	public void CreateBoneList(GameObject obj, string name)
	{
		dictBone.Clear();
		CreateBoneListLoop(obj, name);
	}

	public void CreateBoneList(ChaPreparationBodyBone.BoneInfo[] boneInfos)
	{
		dictBone = boneInfos.ToDictionary((ChaPreparationBodyBone.BoneInfo v) => v.name, (ChaPreparationBodyBone.BoneInfo v) => v.gameObject);
	}

	public void CreateBoneListLoop(GameObject obj, string name)
	{
		if ((obj.name == name || name.IsNullOrEmpty()) && !dictBone.ContainsKey(obj.name))
		{
			dictBone[obj.name] = obj;
		}
		for (int i = 0; i < obj.transform.childCount; i++)
		{
			CreateBoneListLoop(obj.transform.GetChild(i).gameObject, name);
		}
	}

	public void CreateBoneList(GameObject obj, string assetBundleName, string assetName)
	{
		dictBone.Clear();
		if (!AssetBundleCheck.IsFile(assetBundleName, assetName))
		{
			_ = "読み込みエラー\r\nassetBundleName：" + assetBundleName + "\tassetName：" + assetName;
			return;
		}
		AssetBundleLoadAssetOperation assetBundleLoadAssetOperation = AssetBundleManager.LoadAsset(assetBundleName, assetName, typeof(TextAsset));
		if (assetBundleLoadAssetOperation.IsEmpty())
		{
			_ = "読み込みエラー\r\nassetName：" + assetName;
			return;
		}
		YS_Assist.GetListString(assetBundleLoadAssetOperation.GetAsset<TextAsset>().text, out var data);
		GameObject gameObject = null;
		int length = data.GetLength(0);
		int length2 = data.GetLength(1);
		if (length != 0 && length2 != 0)
		{
			for (int i = 0; i < length; i++)
			{
				gameObject = obj.transform.FindLoop(data[i, 0]);
				if ((bool)gameObject)
				{
					dictBone[data[i, 0]] = gameObject;
				}
			}
		}
		AssetBundleManager.UnloadAssetBundle(assetBundleName, isUnloadForceRefCount: true);
	}

	public void CreateBoneList(GameObject obj, string[] bonename)
	{
		dictBone.Clear();
		GameObject gameObject = null;
		for (int i = 0; i < bonename.Length; i++)
		{
			gameObject = obj.transform.FindLoop(bonename[i]);
			if ((bool)gameObject)
			{
				dictBone[bonename[i]] = gameObject;
			}
		}
	}

	public void AssignedWeights(GameObject obj, string delTopName, Transform rootBone = null)
	{
		if (dictBone != null && dictBone.Count != 0 && !(null == obj))
		{
			AssignedWeightsLoop(obj.transform, rootBone);
			GameObject gameObject = obj.transform.FindLoop(delTopName);
			if ((bool)gameObject)
			{
				gameObject.transform.SetParent(null);
				Object.Destroy(gameObject);
			}
		}
	}

	private void AssignedWeightsLoop(Transform t, Transform rootBone = null)
	{
		SkinnedMeshRenderer component = t.GetComponent<SkinnedMeshRenderer>();
		if ((bool)component)
		{
			int num = component.bones.Length;
			Transform[] array = new Transform[num];
			GameObject value = null;
			for (int i = 0; i < num; i++)
			{
				if (dictBone.TryGetValue(component.bones[i].name, out value))
				{
					array[i] = value.transform;
				}
			}
			component.bones = array;
			Cloth component2 = component.gameObject.GetComponent<Cloth>();
			if ((bool)rootBone && null == component2)
			{
				component.rootBone = rootBone;
			}
			else if ((bool)component.rootBone && dictBone.TryGetValue(component.rootBone.name, out value))
			{
				component.rootBone = value.transform;
			}
		}
		foreach (Transform item in t.gameObject.transform)
		{
			AssignedWeightsLoop(item, rootBone);
		}
	}

	public void AssignedWeightsAndSetBounds(GameObject obj, string delTopName, Bounds bounds, Transform rootBone = null)
	{
		if (dictBone != null && dictBone.Count != 0 && !(null == obj))
		{
			AssignedWeightsAndSetBoundsLoop(obj.transform, bounds, rootBone);
			GameObject gameObject = obj.transform.FindLoop(delTopName);
			if ((bool)gameObject)
			{
				gameObject.transform.SetParent(null);
				Object.Destroy(gameObject);
			}
		}
	}

	private void AssignedWeightsAndSetBoundsLoop(Transform t, Bounds bounds, Transform rootBone = null)
	{
		SkinnedMeshRenderer component = t.GetComponent<SkinnedMeshRenderer>();
		if ((bool)component)
		{
			int num = component.bones.Length;
			Transform[] bones = component.bones;
			GameObject value = null;
			for (int i = 0; i < num; i++)
			{
				if (dictBone.TryGetValue(component.bones[i].name, out value))
				{
					bones[i] = value.transform;
				}
			}
			component.bones = bones;
			component.localBounds = bounds;
			Cloth component2 = component.gameObject.GetComponent<Cloth>();
			if (rootBone != null && null == component2)
			{
				component.rootBone = rootBone;
			}
			else if (component.rootBone != null && dictBone.TryGetValue(component.rootBone.name, out value))
			{
				component.rootBone = value.transform;
			}
		}
		foreach (Transform item in t)
		{
			AssignedWeightsAndSetBoundsLoop(item, bounds, rootBone);
		}
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.2.0.10050' (yours is '8.2.0.7535-95108c96')
