using System;
using System.Collections.Generic;
using IllusionUtility.GetUtility;
using UnityEngine;

public abstract class ShapeInfoBase
{
	public class CategoryInfo
	{
		public int id;

		public string name = string.Empty;

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

	public ShapeInfoBase()
	{
		InitEnd = false;
		dictCategory = new Dictionary<int, List<CategoryInfo>>();
		dictDst = new Dictionary<int, BoneInfo>();
		dictSrc = new Dictionary<int, BoneInfo>();
	}

	public int GetKeyCount()
	{
		return (anmKeyInfo != null) ? anmKeyInfo.GetKeyCount() : 0;
	}

	public abstract void InitShapeInfo(string manifest, string assetBundleAnmKey, string assetBundleCategory, string anmKeyInfoName, string cateInfoName, Transform trfObj);

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
			string message = "読み込みエラー\r\nassetBundleName：" + assetBundleName + "\tassetName：" + assetName;
			Debug.LogError(message);
			return false;
		}
		AssetBundleLoadAssetOperation assetBundleLoadAssetOperation = AssetBundleManager.LoadAsset(assetBundleName, assetName, typeof(TextAsset));
		if (assetBundleLoadAssetOperation == null)
		{
			string message2 = "読み込みエラー\r\nassetName：" + assetName;
			Debug.LogError(message2);
			return false;
		}
		TextAsset asset = assetBundleLoadAssetOperation.GetAsset<TextAsset>();
		if (null == asset)
		{
			Debug.LogError("ありえない");
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
					string message3 = "SrcBone【" + categoryInfo.name + "】のIDが見つかりません";
					Debug.LogWarning(message3);
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
		if (!dictCategory.TryGetValue(category, out var value2))
		{
			return false;
		}
		int count = value2.Count;
		string empty = string.Empty;
		int num = 0;
		for (int i = 0; i < count; i++)
		{
			BoneInfo value3 = null;
			num = value2[i].id;
			empty = value2[i].name;
			if (dictSrc.TryGetValue(num, out value3))
			{
				Vector3[] value4 = new Vector3[3];
				for (int j = 0; j < 3; j++)
				{
					ref Vector3 reference = ref value4[j];
					reference = Vector3.zero;
				}
				bool[] array = new bool[3];
				for (int k = 0; k < 3; k++)
				{
					array[k] = value2[i].getflag[k];
				}
				anmKeyInfo.GetInfo(empty, value, ref value4, array);
				if (value2[i].use[0][0])
				{
					value3.vctPos.x = value4[0].x;
				}
				if (value2[i].use[0][1])
				{
					value3.vctPos.y = value4[0].y;
				}
				if (value2[i].use[0][2])
				{
					value3.vctPos.z = value4[0].z;
				}
				if (value2[i].use[1][0])
				{
					value3.vctRot.x = value4[1].x;
				}
				if (value2[i].use[1][1])
				{
					value3.vctRot.y = value4[1].y;
				}
				if (value2[i].use[1][2])
				{
					value3.vctRot.z = value4[1].z;
				}
				if (value2[i].use[2][0])
				{
					value3.vctScl.x = value4[2].x;
				}
				if (value2[i].use[2][1])
				{
					value3.vctScl.y = value4[2].y;
				}
				if (value2[i].use[2][2])
				{
					value3.vctScl.z = value4[2].z;
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
		if (!dictCategory.TryGetValue(category, out var value))
		{
			return false;
		}
		int count = value.Count;
		string empty = string.Empty;
		int num = 0;
		for (int i = 0; i < count; i++)
		{
			BoneInfo value2 = null;
			num = value[i].id;
			empty = value[i].name;
			if (dictSrc.TryGetValue(num, out value2))
			{
				Vector3[] value3 = new Vector3[3];
				for (int j = 0; j < 3; j++)
				{
					ref Vector3 reference = ref value3[j];
					reference = Vector3.zero;
				}
				Vector3[] value4 = new Vector3[3];
				for (int k = 0; k < 3; k++)
				{
					ref Vector3 reference2 = ref value4[k];
					reference2 = Vector3.zero;
				}
				bool[] array = new bool[3];
				for (int l = 0; l < 3; l++)
				{
					array[l] = value[i].getflag[l];
				}
				if (!anmKeyInfo.GetInfo(empty, key01, ref value3, array))
				{
					return false;
				}
				if (!anmKeyInfo.GetInfo(empty, key02, ref value4, array))
				{
					return false;
				}
				Vector3 vector = Vector3.Lerp(value3[0], value4[0], blend);
				if (value[i].use[0][0])
				{
					value2.vctPos.x = vector.x;
				}
				if (value[i].use[0][1])
				{
					value2.vctPos.y = vector.y;
				}
				if (value[i].use[0][2])
				{
					value2.vctPos.z = vector.z;
				}
				vector.x = Mathf.LerpAngle(value3[1].x, value4[1].x, blend);
				vector.y = Mathf.LerpAngle(value3[1].y, value4[1].y, blend);
				vector.z = Mathf.LerpAngle(value3[1].z, value4[1].z, blend);
				if (value[i].use[1][0])
				{
					value2.vctRot.x = vector.x;
				}
				if (value[i].use[1][1])
				{
					value2.vctRot.y = vector.y;
				}
				if (value[i].use[1][2])
				{
					value2.vctRot.z = vector.z;
				}
				vector = Vector3.Lerp(value3[2], value4[2], blend);
				if (value[i].use[2][0])
				{
					value2.vctScl.x = vector.x;
				}
				if (value[i].use[2][1])
				{
					value2.vctScl.y = vector.y;
				}
				if (value[i].use[2][2])
				{
					value2.vctScl.z = vector.z;
				}
			}
		}
		return true;
	}

	public abstract void ForceUpdate();

	public abstract void Update();

	public abstract void UpdateAlways();
}
