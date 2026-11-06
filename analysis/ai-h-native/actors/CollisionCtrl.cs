using System;
using System.Collections.Generic;
using AIChara;
using Manager;
using UnityEngine;

public class CollisionCtrl : MonoBehaviour
{
	[Serializable]
	public struct CollisionInfo
	{
		public string nameAnimation;

		public List<bool> lstIsActive;
	}

	public List<CollisionInfo> lstInfo = new List<CollisionInfo>();

	public List<GameObject> lstObj = new List<GameObject>();

	public ChaControl chaFemale;

	[DisabledGroup("表示")]
	public bool isActive = true;

	private void Update()
	{
		if ((bool)chaFemale)
		{
			Proc(chaFemale.getAnimatorStateInfo(0));
		}
	}

	public bool Init(ChaControl _female, GameObject _objHitHead, GameObject _objHitBody)
	{
		Release();
		chaFemale = _female;
		List<GameObject> headCollisionComponent = GetHeadCollisionComponent(_objHitHead);
		if (headCollisionComponent != null)
		{
			lstObj.AddRange(headCollisionComponent);
		}
		else
		{
			lstObj.Add(null);
		}
		if ((bool)_objHitBody)
		{
			HitCollision componentInChildren = _objHitBody.GetComponentInChildren<HitCollision>();
			if ((bool)componentInChildren)
			{
				lstObj.AddRange(componentInChildren.lstObj);
			}
		}
		return true;
	}

	private List<GameObject> GetHeadCollisionComponent(GameObject _objHitHead)
	{
		if (_objHitHead == null)
		{
			return null;
		}
		HitCollision componentInChildren = _objHitHead.GetComponentInChildren<HitCollision>();
		if (componentInChildren == null)
		{
			return null;
		}
		if (componentInChildren.lstObj.Count == 0)
		{
			return null;
		}
		return componentInChildren.lstObj;
	}

	public void Release()
	{
		lstObj.Clear();
		lstInfo = new List<CollisionInfo>();
		isActive = true;
	}

	public void LoadExcel(string _file)
	{
		if (!(_file == string.Empty) && !Singleton<Manager.Resources>.Instance.HSceneTable.DicLstCollisionInfo.TryGetValue(_file, out lstInfo))
		{
			lstInfo = new List<CollisionInfo>();
		}
	}

	private bool Proc(AnimatorStateInfo _ai)
	{
		for (int i = 0; i < lstInfo.Count; i++)
		{
			if (!_ai.IsName(lstInfo[i].nameAnimation.ToString()))
			{
				continue;
			}
			Visible(_visible: true);
			for (int j = 0; j < lstObj.Count; j++)
			{
				if (!(lstObj[j] == null) && lstObj[j].activeSelf != lstInfo[i].lstIsActive[j])
				{
					lstObj[j].SetActive(lstInfo[i].lstIsActive[j]);
				}
			}
			return true;
		}
		Visible(_visible: false);
		return false;
	}

	private void Visible(bool _visible)
	{
		if (isActive == _visible)
		{
			return;
		}
		for (int i = 0; i < lstObj.Count; i++)
		{
			if ((bool)lstObj[i])
			{
				lstObj[i].SetActive(_visible);
			}
		}
		isActive = _visible;
	}
}
