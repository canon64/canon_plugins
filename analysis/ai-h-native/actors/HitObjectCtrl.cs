using System.Collections;
using System.Collections.Generic;
using AIChara;
using Manager;
using UnityEngine;

public class HitObjectCtrl
{
	public struct CollisionInfo
	{
		public string nameAnimation;

		public List<bool> lstIsActive;
	}

	public bool isInit;

	public bool isActive = true;

	public int id = -1;

	public Transform Place;

	private List<string> atariName = new List<string>();

	private List<GameObject> lstObject = new List<GameObject>();

	private List<CollisionInfo> lstInfo = new List<CollisionInfo>();

	private AnimatorStateInfo ai;

	private Dictionary<int, Dictionary<string, GameObject>> tmpDic = new Dictionary<int, Dictionary<string, GameObject>>();

	private Dictionary<string, GameObject> tmpLst = new Dictionary<string, GameObject>();

	private string pathAsset;

	private static List<string> lstHitObject;

	private Transform[] getChild;

	public IEnumerator HitObjInit(int Sex, GameObject _objBody, ChaControl _custom)
	{
		if (_objBody == null)
		{
			yield break;
		}
		ReleaseObject();
		while (!Singleton<Manager.Resources>.Instance.HSceneTable.endHLoad)
		{
			yield return null;
		}
		if (!Singleton<Manager.Resources>.Instance.HSceneTable.lstHitObject.ContainsKey(Sex))
		{
			yield break;
		}
		if (lstHitObject == null)
		{
			lstHitObject = new List<string>();
		}
		lstHitObject = Singleton<Manager.Resources>.Instance.HSceneTable.lstHitObject[Sex];
		int nX = lstHitObject.Count;
		GameObject obj = null;
		for (int i = 0; i < nX; i += 3)
		{
			GameObject objParent = GetObjParent(_objBody.transform, lstHitObject[i]);
			if (Singleton<Manager.Resources>.Instance.HSceneTable.DicHitObject.TryGetValue(Sex, out tmpDic) && tmpDic.TryGetValue(id, out tmpLst) && tmpLst.TryGetValue(lstHitObject[i + 2], out obj))
			{
				EliminateScale[] scale = obj.GetComponentsInChildren<EliminateScale>(includeInactive: true);
				EliminateScale[] array = scale;
				foreach (EliminateScale eliminateScale in array)
				{
					eliminateScale.custom = _custom;
				}
				if (objParent != null && obj != null)
				{
					obj.transform.SetParent(objParent.transform, worldPositionStays: false);
					obj.transform.localPosition = Vector3.zero;
					obj.transform.localRotation = Quaternion.identity;
				}
				lstObject.Add(obj);
			}
		}
		isInit = true;
	}

	private GameObject GetObjParent(Transform objTop, string name)
	{
		getChild = objTop.GetComponentsInChildren<Transform>();
		for (int i = 0; i < getChild.Length; i++)
		{
			if (!(getChild[i].name != name))
			{
				return getChild[i].gameObject;
			}
		}
		return null;
	}

	public void HitObjLoadExcel(string _file)
	{
		lstInfo = new List<CollisionInfo>();
		atariName = new List<string>();
		if (!(_file == string.Empty))
		{
			if (!Singleton<Manager.Resources>.Instance.HSceneTable.DicLstHitObjInfo.TryGetValue(_file, out lstInfo))
			{
				lstInfo = new List<CollisionInfo>();
			}
			if (!Singleton<Manager.Resources>.Instance.HSceneTable.HitObjAtariName.TryGetValue(_file, out atariName))
			{
				atariName = new List<string>();
			}
		}
	}

	public bool setActiveObject(bool val)
	{
		for (int i = 0; i < lstObject.Count; i++)
		{
			if (!(lstObject[i] == null) && lstObject[i].activeSelf != val)
			{
				lstObject[i].SetActive(val);
			}
		}
		isActive = val;
		return true;
	}

	public bool ReleaseObject()
	{
		for (int i = 0; i < lstObject.Count; i++)
		{
			if (!(lstObject[i] == null))
			{
				lstObject[i].SetActive(value: false);
				lstObject[i].transform.SetParent(Place, worldPositionStays: false);
			}
		}
		isInit = false;
		isActive = true;
		return true;
	}

	public bool Proc(Animator _anim)
	{
		if (_anim == null || _anim.runtimeAnimatorController == null)
		{
			Visible(_visible: false);
			return false;
		}
		ai = _anim.GetCurrentAnimatorStateInfo(0);
		for (int i = 0; i < lstInfo.Count; i++)
		{
			if (!ai.IsName(lstInfo[i].nameAnimation.ToString()))
			{
				continue;
			}
			isActive = true;
			foreach (GameObject item in lstObject)
			{
				for (int j = 0; j < atariName.Count; j++)
				{
					if (!(item.name != atariName[j]) && item.activeSelf != lstInfo[i].lstIsActive[j])
					{
						item.SetActive(lstInfo[i].lstIsActive[j]);
					}
				}
			}
			return true;
		}
		Visible(_visible: false);
		return false;
	}

	private bool Visible(bool _visible)
	{
		if (isActive == _visible)
		{
			return false;
		}
		for (int i = 0; i < lstObject.Count; i++)
		{
			lstObject[i].SetActive(_visible);
		}
		isActive = _visible;
		return false;
	}
}
