using System.Collections.Generic;
using System.IO;
using System.Text;
using IllusionUtility.GetUtility;
using Obi;
using UnityEngine;

public class ParentObjectCtrl : MonoBehaviour
{
	public class HitObjInfo
	{
		public string parent;

		public string path;

		public string asset;

		public string manifest;
	}

	public bool isInit;

	private bool isAllVisible = true;

	private Dictionary<string, GameObject> dicObject = new Dictionary<string, GameObject>();

	public bool isActive = true;

	private Dictionary<string, List<string>> dicObjectCtrl = new Dictionary<string, List<string>>();

	private ExcelData excel;

	private List<string> row = new List<string>();

	private List<GameObject> lstObjTouch = new List<GameObject>();

	private Dictionary<string, Dictionary<string, bool>> dicInfo = new Dictionary<string, Dictionary<string, bool>>();

	private Dictionary<string, ObiCollider[]> dicObiCol = new Dictionary<string, ObiCollider[]>();

	private Dictionary<string, GameObject> dicBodyObject = new Dictionary<string, GameObject>();

	private static List<HitObjInfo>[] lstSiruHitObject = new List<HitObjInfo>[2]
	{
		new List<HitObjInfo>(),
		new List<HitObjInfo>()
	};

	private static List<HitObjInfo>[] lstTouchHitObject = new List<HitObjInfo>[2]
	{
		new List<HitObjInfo>(),
		new List<HitObjInfo>()
	};

	private static ExcelData sExcel;

	private static List<string> sRow = new List<string>();

	public bool Init(string _strAssetFolderPath, string _file, GameObject _objBody, ChaControl _chaCtrl, HashSet<string> _hsAssetPath, bool _isAllVisible)
	{
		if (_objBody == null)
		{
			return false;
		}
		ReleaseObject();
		string text = GlobalMethod.LoadAllListText(_strAssetFolderPath, _file);
		if (text == "")
		{
			isInit = false;
			return false;
		}
		isAllVisible = _isAllVisible;
		GlobalMethod.GetListString(text, out var data);
		int length = data.GetLength(0);
		int length2 = data.GetLength(1);
		for (int i = 0; i < length; i++)
		{
			for (int j = 0; j < length2; j += 3)
			{
				string self = data[i, j];
				string text2 = data[i, j + 1];
				string text3 = data[i, j + 2];
				if (self.IsNullOrEmpty() && text2.IsNullOrEmpty() && text3.IsNullOrEmpty())
				{
					break;
				}
				GameObject gameObject = _objBody.transform.FindLoop(self);
				GameObject gameObject2 = CommonLib.LoadAsset<GameObject>(text2, text3, clone: true);
				AssetBundleManager.UnloadAssetBundle(text2, isUnloadForceRefCount: true);
				EliminateScale[] componentsInChildren = gameObject2.GetComponentsInChildren<EliminateScale>(includeInactive: true);
				for (int k = 0; k < componentsInChildren.Length; k++)
				{
					componentsInChildren[k].chaCtrl = _chaCtrl;
				}
				if (gameObject != null && gameObject2 != null)
				{
					gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
				}
				if (!dicObject.ContainsKey(text3))
				{
					dicObject.Add(text3, gameObject2);
					dicObiCol.Add(text3, gameObject2.GetComponentsInChildren<ObiCollider>());
				}
				else
				{
					Object.Destroy(dicObject[text3]);
					dicObject[text3] = gameObject2;
					dicObiCol[text3] = gameObject2.GetComponentsInChildren<ObiCollider>();
				}
			}
		}
		isInit = true;
		return true;
	}

	public static void LoadInit()
	{
		lstSiruHitObject = new List<HitObjInfo>[2]
		{
			new List<HitObjInfo>(),
			new List<HitObjInfo>()
		};
		lstTouchHitObject = new List<HitObjInfo>[2]
		{
			new List<HitObjInfo>(),
			new List<HitObjInfo>()
		};
		StringBuilder stringBuilder = new StringBuilder();
		List<string> lstABName = HFlag.lstABName;
		foreach (string item in lstABName)
		{
			stringBuilder.Clear();
			stringBuilder.Append("base_ab").Append(Path.GetFileNameWithoutExtension(item));
			if (!AssetBundleCheck.IsFile(item, stringBuilder.ToString()) || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(item, stringBuilder.ToString())))
			{
				continue;
			}
			sExcel = CommonLib.LoadAsset<ExcelData>(item, stringBuilder.ToString(), clone: false, "", check: false);
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
			if (sExcel == null)
			{
				continue;
			}
			int num = 1;
			while (num < sExcel.MaxCell)
			{
				sRow = sExcel.list[num++].list;
				int result = 0;
				if (!int.TryParse(sRow[0], out result))
				{
					break;
				}
				for (int i = 1; i < sRow.Count; i += 4)
				{
					SCheckObjInfo(lstSiruHitObject[result], sRow[i], sRow[i + 1], sRow[i + 2], sRow[i + 3]);
				}
			}
		}
		foreach (string item2 in lstABName)
		{
			stringBuilder.Clear();
			stringBuilder.Append("touchbase_ab").Append(Path.GetFileNameWithoutExtension(item2));
			if (!AssetBundleCheck.IsFile(item2, stringBuilder.ToString()) || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(item2, stringBuilder.ToString())))
			{
				continue;
			}
			sExcel = CommonLib.LoadAsset<ExcelData>(item2, stringBuilder.ToString(), clone: false, "", check: false);
			AssetBundleManager.UnloadAssetBundle(item2, isUnloadForceRefCount: true);
			if (sExcel == null)
			{
				continue;
			}
			int num2 = 1;
			while (num2 < sExcel.MaxCell)
			{
				sRow = sExcel.list[num2++].list;
				int result2 = 0;
				if (!int.TryParse(sRow[0], out result2))
				{
					break;
				}
				for (int j = 1; j < sRow.Count; j += 4)
				{
					SCheckObjInfo(lstTouchHitObject[result2], sRow[j], sRow[j + 1], sRow[j + 2], sRow[j + 3]);
				}
			}
		}
		sExcel = null;
		sRow = null;
	}

	public bool Init(GameObject _objBody, ChaControl _chaCtrl, int _sex, bool _isAllVisible)
	{
		if (_objBody == null)
		{
			return false;
		}
		ReleaseObject();
		List<string> lstABName = HFlag.lstABName;
		isAllVisible = _isAllVisible;
		if (!isAllVisible)
		{
			LoadBase(_objBody, _chaCtrl, _sex, lstABName);
		}
		else
		{
			LoadBase(_objBody, _chaCtrl, _sex, lstABName, 1);
		}
		isInit = true;
		return true;
	}

	private void LoadBase(GameObject _objBody, ChaControl _chaCtrl, int _sex, List<string> pathList, int mode = 0)
	{
		List<HitObjInfo> list = ((mode == 0) ? lstSiruHitObject[_sex] : lstTouchHitObject[_sex]);
		GameObject gameObject = null;
		for (int i = 0; i < list.Count; i++)
		{
			GameObject objParent = GetObjParent(list[i].parent);
			if (objParent == null || !AssetBundleCheck.IsFile(list[i].path, list[i].asset))
			{
				continue;
			}
			gameObject = CommonLib.LoadAsset<GameObject>(list[i].path, list[i].asset, clone: true, list[i].manifest);
			AssetBundleManager.UnloadAssetBundle(list[i].path, isUnloadForceRefCount: true, AssetBundleCheck.IsSimulation ? "" : list[i].manifest);
			if (!(gameObject == null))
			{
				EliminateScale[] componentsInChildren = gameObject.GetComponentsInChildren<EliminateScale>(includeInactive: true);
				for (int j = 0; j < componentsInChildren.Length; j++)
				{
					componentsInChildren[j].chaCtrl = _chaCtrl;
				}
				gameObject.transform.SetParent(objParent.transform, worldPositionStays: false);
				if (mode == 0)
				{
					dicObject.Add(list[i].asset, gameObject);
					dicObiCol.Add(list[i].asset, gameObject.GetComponentsInChildren<ObiCollider>());
				}
				else
				{
					lstObjTouch.Add(gameObject);
				}
			}
		}
	}

	private void CheckObjInfo(List<HitObjInfo> lstHitObject, string parent, string bundle, string file)
	{
		int num = -1;
		for (int i = 0; i < lstHitObject.Count; i++)
		{
			if (!(lstHitObject[i].asset != file))
			{
				num = i;
				break;
			}
		}
		if (num < 0)
		{
			lstHitObject.Add(new HitObjInfo
			{
				parent = parent,
				path = bundle,
				asset = file
			});
		}
		else
		{
			lstHitObject[num].path = bundle;
		}
	}

	private static void SCheckObjInfo(List<HitObjInfo> lstHitObject, string parent, string bundle, string file, string manifest)
	{
		int num = -1;
		for (int i = 0; i < lstHitObject.Count; i++)
		{
			if (!(lstHitObject[i].asset != file))
			{
				num = i;
				break;
			}
		}
		if (num < 0)
		{
			lstHitObject.Add(new HitObjInfo
			{
				parent = parent,
				path = bundle,
				asset = file,
				manifest = manifest
			});
		}
		else
		{
			lstHitObject[num].path = bundle;
			lstHitObject[num].manifest = manifest;
		}
	}

	private GameObject GetObjParent(Transform objTop, string name)
	{
		Transform[] componentsInChildren = objTop.GetComponentsInChildren<Transform>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			if (!(componentsInChildren[i].name != name))
			{
				return componentsInChildren[i].gameObject;
			}
		}
		return null;
	}

	private GameObject GetObjParent(string name)
	{
		if (!dicBodyObject.ContainsKey(name))
		{
			return null;
		}
		return dicBodyObject[name];
	}

	public bool LoadText(string _assetpath, string _file)
	{
		dicObjectCtrl.Clear();
		if (_file == "")
		{
			return false;
		}
		string text = GlobalMethod.LoadAllListText(_assetpath, _file);
		if (text.IsNullOrEmpty())
		{
			return false;
		}
		GlobalMethod.GetListString(text, out var data);
		int length = data.GetLength(0);
		int length2 = data.GetLength(1);
		for (int i = 0; i < length; i++)
		{
			int num = 0;
			string key = data[i, num++];
			if (!dicObjectCtrl.ContainsKey(key))
			{
				dicObjectCtrl.Add(key, new List<string>());
			}
			List<string> list = dicObjectCtrl[key];
			list.Clear();
			for (int j = num; j < length2; j++)
			{
				string text2 = data[i, j];
				if (text2.IsNullOrEmpty())
				{
					break;
				}
				list.Add(text2);
			}
		}
		return true;
	}

	public bool LoadExcel(string _file)
	{
		dicInfo = new Dictionary<string, Dictionary<string, bool>>();
		List<string> list = new List<string>();
		if (_file.IsNullOrEmpty())
		{
			return false;
		}
		List<string> lstABName = HFlag.lstABName;
		for (int i = 0; i < lstABName.Count; i++)
		{
			excel = null;
			if (!AssetBundleCheck.IsFile(lstABName[i], _file) || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(lstABName[i], _file)))
			{
				continue;
			}
			excel = CommonLib.LoadAsset<ExcelData>(lstABName[i], _file, clone: false, "", check: false);
			AssetBundleManager.UnloadAssetBundle(lstABName[i], isUnloadForceRefCount: true);
			if (excel == null)
			{
				continue;
			}
			int num = 0;
			int num2 = 0;
			row = excel.list[num++].list;
			for (num2 = 1; num2 < row.Count; num2++)
			{
				int index = num2;
				list.Add(row[index]);
			}
			while (num < excel.MaxCell)
			{
				num2 = 0;
				row = excel.list[num++].list;
				string key = row[num2++];
				if (!dicInfo.ContainsKey(key))
				{
					dicInfo.Add(key, new Dictionary<string, bool>());
				}
				else
				{
					dicInfo[key].Clear();
				}
				for (int j = 1; j < row.Count; j++)
				{
					dicInfo[key].Add(list[j - 1], row[num2++] == "1");
				}
			}
		}
		excel = null;
		row = null;
		return true;
	}

	public bool ReleaseObject()
	{
		foreach (KeyValuePair<string, GameObject> item in dicObject)
		{
			if (!(item.Value == null))
			{
				Object.Destroy(item.Value);
			}
		}
		foreach (GameObject item2 in lstObjTouch)
		{
			if (!(item2 == null))
			{
				Object.Destroy(item2);
			}
		}
		dicObject.Clear();
		dicObiCol.Clear();
		lstObjTouch.Clear();
		isInit = false;
		isActive = true;
		return true;
	}

	public bool Proc(string _nameNextAnimation)
	{
		Visible(isAllVisible);
		if (!dicObjectCtrl.ContainsKey(_nameNextAnimation))
		{
			return false;
		}
		isActive = !isAllVisible;
		foreach (string item in dicObjectCtrl[_nameNextAnimation])
		{
			dicObject[item].SetActive(!isAllVisible);
			SetColPhase(item);
		}
		return true;
	}

	public bool ProcObi(string _nameNextAnimation)
	{
		if (isAllVisible)
		{
			foreach (GameObject item in lstObjTouch)
			{
				if (!item.activeSelf)
				{
					item.SetActive(value: true);
				}
			}
		}
		else
		{
			VisibleObi(_visible: false);
			if (!dicInfo.ContainsKey(_nameNextAnimation))
			{
				return false;
			}
			foreach (KeyValuePair<string, bool> item2 in dicInfo[_nameNextAnimation])
			{
				dicObject[item2.Key].SetActive(item2.Value);
				SetColPhase(item2.Key);
			}
		}
		return true;
	}

	public GameObject GetObject(string _key)
	{
		dicObject.TryGetValue(_key, out var value);
		return value;
	}

	public Dictionary<string, GameObject> GetObjects()
	{
		return dicObject;
	}

	private bool Visible(bool _visible)
	{
		if (isActive == _visible)
		{
			return false;
		}
		foreach (KeyValuePair<string, GameObject> item in dicObject)
		{
			item.Value.SetActive(_visible);
			SetColPhase(item.Key);
		}
		isActive = _visible;
		return false;
	}

	private bool VisibleObi(bool _visible)
	{
		foreach (KeyValuePair<string, GameObject> item in dicObject)
		{
			item.Value.SetActive(_visible);
			SetColPhase(item.Key);
		}
		return false;
	}

	private void SetColPhase(string objName)
	{
		if (dicObiCol.Count == 0 || dicObject[objName] == null || !dicObiCol.ContainsKey(objName) || dicObiCol[objName] == null || dicObiCol[objName].Length == 0)
		{
			return;
		}
		bool activeSelf = dicObject[objName].activeSelf;
		ObiCollider[] array = dicObiCol[objName];
		foreach (ObiCollider obiCollider in array)
		{
			if (!(obiCollider == null))
			{
				obiCollider.Phase = ((!activeSelf) ? 1 : 0);
			}
		}
	}

	public void SetDicBodyObject(GameObject _objBody)
	{
		dicBodyObject.Clear();
		Transform[] componentsInChildren = _objBody.GetComponentsInChildren<Transform>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			if (!dicBodyObject.ContainsKey(componentsInChildren[i].name))
			{
				dicBodyObject.Add(componentsInChildren[i].name, componentsInChildren[i].gameObject);
			}
		}
	}

	public static void EndProc()
	{
		for (int i = 0; i < 2; i++)
		{
			lstSiruHitObject[i].Clear();
			lstTouchHitObject[i].Clear();
		}
		lstSiruHitObject = null;
		lstTouchHitObject = null;
	}
}
