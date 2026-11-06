using System;
using System.Collections.Generic;
using IllusionUtility.GetUtility;
using UnityEngine;

public abstract class ShapeInfoBase
{
	public class CategoryInfo
	{
		public int id;

		public string name = "";

		public bool[][] use = new bool[3][];

		public bool[] getflag = new bool[3];

		public void Initialize()
		{
			for (int i = 0; i < 3; i++)
			{
				use[i] = new bool[3];
				getflag[i] = false;
			}
		}
	}

	public class BoneInfo
	{
		public Transform trfBone;

		public Vector3 vctPos = Vector3.zero;

		public Vector3 vctRot = Vector3.zero;

		public Vector3 vctScl = Vector3.one;
	}

	private Dictionary<int, List<CategoryInfo>> dictCategory;

	protected Dictionary<int, BoneInfo> dictDst;

	protected Dictionary<int, BoneInfo> dictSrc;

	private AnimationKeyInfo anmKeyInfo = new AnimationKeyInfo();

	public bool InitEnd { get; protected set; }

	public int typeBone { get; set; }

	public int sex { get; set; }

	public int GetKeyCount()
	{
		if (anmKeyInfo != null)
		{
			return anmKeyInfo.GetKeyCount();
		}
		return 0;
	}

	public ShapeInfoBase()
	{
		InitEnd = false;
		dictCategory = new Dictionary<int, List<CategoryInfo>>();
		dictDst = new Dictionary<int, BoneInfo>();
		dictSrc = new Dictionary<int, BoneInfo>();
		typeBone = 0;
	}

	public abstract void InitShapeInfo(string manifest, string assetBundleAnmKey, string assetBundleCategory, string anmKeyInfoName, string cateInfoName, Transform trfObj, int _sex);

	protected void InitShapeInfoBase(string manifest, string assetBundleAnmKey, string assetBundleCategory, string anmKeyInfoName, string cateInfoName, Transform trfObj, Dictionary<string, int> dictEnumDst, Dictionary<string, int> dictEnumSrc, Action<string, string> funcAssetBundleEntry = null)
	{
		anmKeyInfo.LoadInfo(manifest, assetBundleAnmKey, anmKeyInfoName, funcAssetBundleEntry);
		LoadCategoryInfoList(assetBundleCategory, cateInfoName, dictEnumSrc);
		GetDstBoneInfo(trfObj, dictEnumDst);
		GetSrcBoneInfo();
	}

	public void ReleaseShapeInfo()
	{
		InitEnd = false;
		dictCategory.Clear();
		dictDst.Clear();
		dictSrc.Clear();
	}

	private bool LoadCategoryInfoList(string assetBundleName, string assetName, Dictionary<string, int> dictEnumSrc)
	{
		if (!AssetBundleCheck.IsFile(assetBundleName, assetName))
		{
			_ = "読み込みエラー\r\nassetBundleName：" + assetBundleName + "\tassetName：" + assetName;
			return false;
		}
		AssetBundleLoadAssetOperation assetBundleLoadAssetOperation = AssetBundleManager.LoadAsset(assetBundleName, assetName, typeof(TextAsset));
		if (assetBundleLoadAssetOperation == null)
		{
			_ = "読み込みエラー\r\nassetName：" + assetName;
			return false;
		}
		TextAsset asset = assetBundleLoadAssetOperation.GetAsset<TextAsset>();
		if (null == asset)
		{
			return false;
		}
		YS_Assist.GetListString(asset.text, out var data);
		int length = data.GetLength(0);
		int length2 = data.GetLength(1);
		dictCategory.Clear();
		if (length != 0 && length2 != 0)
		{
			int num = 0;
			for (int i = 0; i < length; i++)
			{
				CategoryInfo categoryInfo = new CategoryInfo();
				categoryInfo.Initialize();
				num = int.Parse(data[i, 0]);
				categoryInfo.name = data[i, 1];
				int value = 0;
				if (!dictEnumSrc.TryGetValue(categoryInfo.name, out value))
				{
					_ = "SrcBone【" + categoryInfo.name + "】のIDが見つかりません";
					continue;
				}
				categoryInfo.id = value;
				categoryInfo.use[0][0] = !(data[i, 2] == "0");
				categoryInfo.use[0][1] = !(data[i, 3] == "0");
				categoryInfo.use[0][2] = !(data[i, 4] == "0");
				if (categoryInfo.use[0][0] || categoryInfo.use[0][1] || categoryInfo.use[0][2])
				{
					categoryInfo.getflag[0] = true;
				}
				categoryInfo.use[1][0] = !(data[i, 5] == "0");
				categoryInfo.use[1][1] = !(data[i, 6] == "0");
				categoryInfo.use[1][2] = !(data[i, 7] == "0");
				if (categoryInfo.use[1][0] || categoryInfo.use[1][1] || categoryInfo.use[1][2])
				{
					categoryInfo.getflag[1] = true;
				}
				categoryInfo.use[2][0] = !(data[i, 8] == "0");
				categoryInfo.use[2][1] = !(data[i, 9] == "0");
				categoryInfo.use[2][2] = !(data[i, 10] == "0");
				if (categoryInfo.use[2][0] || categoryInfo.use[2][1] || categoryInfo.use[2][2])
				{
					categoryInfo.getflag[2] = true;
				}
				List<CategoryInfo> value2 = null;
				if (!dictCategory.TryGetValue(num, out value2))
				{
					value2 = new List<CategoryInfo>();
					dictCategory[num] = value2;
				}
				value2.Add(categoryInfo);
			}
		}
		AssetBundleManager.UnloadAssetBundle(assetBundleName, isUnloadForceRefCount: true);
		return true;
	}

	private bool GetDstBoneInfo(Transform trfBone, Dictionary<string, int> dictEnumDst)
	{
		dictDst.Clear();
		foreach (KeyValuePair<string, int> item in dictEnumDst)
		{
			GameObject gameObject = trfBone.FindLoop(item.Key);
			if (null != gameObject)
			{
				BoneInfo boneInfo = new BoneInfo();
				boneInfo.trfBone = gameObject.transform;
				dictDst[item.Value] = boneInfo;
			}
		}
		return true;
	}

	private void GetSrcBoneInfo()
	{
		dictSrc.Clear();
		foreach (KeyValuePair<int, List<CategoryInfo>> item in dictCategory)
		{
			int count = item.Value.Count;
			for (int i = 0; i < count; i++)
			{
				BoneInfo value = null;
				if (!dictSrc.TryGetValue(item.Value[i].id, out value))
				{
					value = new BoneInfo();
					dictSrc[item.Value[i].id] = value;
				}
			}
		}
	}

	public bool ChangeValue(int category, float value)
	{
		if (anmKeyInfo == null)
		{
			return false;
		}
		if (!dictCategory.ContainsKey(category))
		{
			return false;
		}
		int count = dictCategory[category].Count;
		string text = "";
		int num = 0;
		for (int i = 0; i < count; i++)
		{
			List<CategoryInfo> list = dictCategory[category];
			BoneInfo value2 = null;
			num = list[i].id;
			text = list[i].name;
			if (dictSrc.TryGetValue(num, out value2))
			{
				Vector3 pos = Vector3.zero;
				Vector3 rot = Vector3.zero;
				Vector3 scale = Vector3.zero;
				anmKeyInfo.GetInfo(text, value, ref pos, ref rot, ref scale, list[i].getflag);
				if (list[i].use[0][0])
				{
					value2.vctPos.x = pos.x;
				}
				if (list[i].use[0][1])
				{
					value2.vctPos.y = pos.y;
				}
				if (list[i].use[0][2])
				{
					value2.vctPos.z = pos.z;
				}
				if (list[i].use[1][0])
				{
					value2.vctRot.x = rot.x;
				}
				if (list[i].use[1][1])
				{
					value2.vctRot.y = rot.y;
				}
				if (list[i].use[1][2])
				{
					value2.vctRot.z = rot.z;
				}
				if (list[i].use[2][0])
				{
					value2.vctScl.x = scale.x;
				}
				if (list[i].use[2][1])
				{
					value2.vctScl.y = scale.y;
				}
				if (list[i].use[2][2])
				{
					value2.vctScl.z = scale.z;
				}
			}
		}
		return true;
	}

	public bool ChangeValue(int category, int key01, int key02, float blend)
	{
		if (anmKeyInfo == null)
		{
			return false;
		}
		if (!dictCategory.ContainsKey(category))
		{
			return false;
		}
		foreach (CategoryInfo item in dictCategory[category])
		{
			if (dictSrc.TryGetValue(item.id, out var value))
			{
				Vector3 pos = Vector3.zero;
				Vector3 rot = Vector3.zero;
				Vector3 scale = Vector3.zero;
				if (!anmKeyInfo.GetInfo(item.name, key01, ref pos, ref rot, ref scale, item.getflag))
				{
					return false;
				}
				Vector3 pos2 = Vector3.zero;
				Vector3 rot2 = Vector3.zero;
				Vector3 scale2 = Vector3.zero;
				if (!anmKeyInfo.GetInfo(item.name, key02, ref pos2, ref rot2, ref scale2, item.getflag))
				{
					return false;
				}
				Vector3 vector = Vector3.Lerp(pos, pos2, blend);
				if (item.use[0][0])
				{
					value.vctPos.x = vector.x;
				}
				if (item.use[0][1])
				{
					value.vctPos.y = vector.y;
				}
				if (item.use[0][2])
				{
					value.vctPos.z = vector.z;
				}
				vector.x = Mathf.LerpAngle(rot.x, rot2.x, blend);
				vector.y = Mathf.LerpAngle(rot.y, rot2.y, blend);
				vector.z = Mathf.LerpAngle(rot.z, rot2.z, blend);
				if (item.use[1][0])
				{
					value.vctRot.x = vector.x;
				}
				if (item.use[1][1])
				{
					value.vctRot.y = vector.y;
				}
				if (item.use[1][2])
				{
					value.vctRot.z = vector.z;
				}
				vector = Vector3.Lerp(scale, scale2, blend);
				if (item.use[2][0])
				{
					value.vctScl.x = vector.x;
				}
				if (item.use[2][1])
				{
					value.vctScl.y = vector.y;
				}
				if (item.use[2][2])
				{
					value.vctScl.z = vector.z;
				}
			}
		}
		return true;
	}

	public abstract void ForceUpdate();

	public abstract void Update();

	public abstract void UpdateAlways();
}
