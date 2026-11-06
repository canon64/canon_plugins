using System;
using System.Collections.Generic;
using AIChara;
using Manager;
using UniRx;
using UnityEngine;

public class YureCtrl : MonoBehaviour
{
	[Serializable]
	public struct BreastShapeInfo
	{
		public bool[] breast;

		public bool nip;

		public void MemberInit()
		{
			breast = new bool[7] { true, true, true, true, true, true, true };
			nip = true;
		}
	}

	[Serializable]
	public class Info
	{
		public string nameAnimation = string.Empty;

		public bool[] aIsActive = new bool[4];

		public BreastShapeInfo[] aBreastShape = new BreastShapeInfo[2];

		public int nFemale;
	}

	[Serializable]
	public class YureCtrlMapH
	{
		public Dictionary<int, List<Info>> lstInfo = new Dictionary<int, List<Info>>();

		public Dictionary<int, ChaControl> chaFemale = new Dictionary<int, ChaControl>();

		[HideInInspector]
		public int femaleID;

		public Dictionary<int, bool> isInit = new Dictionary<int, bool>();

		public Dictionary<int, bool[]> aIsActive = new Dictionary<int, bool[]>();

		public Dictionary<int, BreastShapeInfo[]> aBreastShape = new Dictionary<int, BreastShapeInfo[]>();

		private Dictionary<int, bool[]> aYureEnableActive = new Dictionary<int, bool[]>();

		private Dictionary<int, BreastShapeInfo[]> aBreastShapeEnable = new Dictionary<int, BreastShapeInfo[]>();

		public int AddChaInit(ChaControl female)
		{
			int num = aBreastShapeEnable.Count;
			if (chaFemale.ContainsKey(num))
			{
				int i;
				for (i = 0; chaFemale.ContainsKey(i); i++)
				{
				}
				num = i;
			}
			chaFemale.Add(num, female);
			aBreastShapeEnable.Add(num, new BreastShapeInfo[2]);
			aBreastShape.Add(num, new BreastShapeInfo[2]);
			for (int j = 0; j < 2; j++)
			{
				aBreastShape[num][j].MemberInit();
				aBreastShapeEnable[num][j].MemberInit();
			}
			aYureEnableActive.Add(num, new bool[4] { true, true, true, true });
			return num;
		}

		public void RemoveChaInit(int ID)
		{
			if (lstInfo.ContainsKey(ID))
			{
				lstInfo[ID] = null;
				lstInfo.Remove(ID);
			}
			if (chaFemale.ContainsKey(ID))
			{
				chaFemale[ID] = null;
				chaFemale.Remove(ID);
			}
			if (isInit.ContainsKey(ID))
			{
				isInit.Remove(ID);
			}
			if (aIsActive.ContainsKey(ID))
			{
				aIsActive[ID] = null;
				aIsActive.Remove(ID);
			}
			if (aBreastShape.ContainsKey(ID))
			{
				aBreastShape[ID] = null;
				aBreastShape.Remove(ID);
			}
			if (aYureEnableActive.ContainsKey(ID))
			{
				aYureEnableActive[ID] = null;
				aYureEnableActive.Remove(ID);
			}
			if (aBreastShapeEnable.ContainsKey(ID))
			{
				aBreastShapeEnable[ID] = null;
				aBreastShapeEnable.Remove(ID);
			}
		}

		public bool AddLoadInfo(int _motionId, int category, int _addID)
		{
			if (!isInit.ContainsKey(_addID))
			{
				isInit.Add(_addID, value: false);
			}
			else
			{
				isInit[_addID] = false;
			}
			if (!lstInfo.ContainsKey(_addID))
			{
				lstInfo.Add(_addID, new List<Info>());
			}
			else
			{
				lstInfo[_addID] = new List<Info>();
			}
			if (!aIsActive.ContainsKey(_addID))
			{
				aIsActive.Add(_addID, new bool[4]);
			}
			for (int i = 0; i < aIsActive[_addID].Length; i++)
			{
				aIsActive[_addID][i] = true;
			}
			if (!aBreastShape.ContainsKey(_addID))
			{
				aBreastShape.Add(_addID, new BreastShapeInfo[2]);
			}
			for (int j = 0; j < 2; j++)
			{
				aBreastShape[_addID][j].MemberInit();
			}
			if ((bool)chaFemale[_addID])
			{
				chaFemale[_addID].playDynamicBoneBust(0, play: true);
				chaFemale[_addID].playDynamicBoneBust(1, play: true);
				Observable.EveryEndOfFrame().Take(1).Subscribe(delegate
				{
					DynamicBone_Ver02 dynamicBoneBustAndHip = chaFemale[_addID].GetDynamicBoneBustAndHip(ChaControlDefine.DynamicBoneKind.BreastL);
					dynamicBoneBustAndHip.enabled = false;
					dynamicBoneBustAndHip = chaFemale[_addID].GetDynamicBoneBustAndHip(ChaControlDefine.DynamicBoneKind.BreastR);
					dynamicBoneBustAndHip.enabled = false;
				});
				for (int k = 0; k < ChaFileDefine.cf_BustShapeMaskID.Length; k++)
				{
					int id = k;
					chaFemale[_addID].DisableShapeBodyID(2, id, disable: false);
				}
			}
			Dictionary<int, List<Info>> value = null;
			Singleton<Manager.Resources>.Instance.HSceneTable.DicDicYure.TryGetValue(category, out value);
			if (value != null)
			{
				List<Info> value2 = new List<Info>();
				value.TryGetValue(_motionId, out value2);
				lstInfo[_addID] = value2;
			}
			isInit[_addID] = true;
			return true;
		}

		public bool Proc(AnimatorStateInfo _ai, int _id)
		{
			if (!isInit[_id])
			{
				return false;
			}
			Info info = null;
			if (lstInfo != null && lstInfo[_id] != null)
			{
				for (int i = 0; i < lstInfo[_id].Count; i++)
				{
					if (_ai.IsName(lstInfo[_id][i].nameAnimation) && lstInfo[_id][i].nFemale == 0)
					{
						info = lstInfo[_id][i];
						break;
					}
				}
			}
			if (info != null)
			{
				Active(info.aIsActive, _id);
				Shape(info.aBreastShape, _id);
				return true;
			}
			Active(aYureEnableActive[_id], _id);
			Shape(aBreastShapeEnable[_id], _id);
			return false;
		}

		private void Active(bool[] _aIsActive, int _id)
		{
			for (int i = 0; i < aIsActive[_id].Length; i++)
			{
				if (aIsActive[_id][i] != _aIsActive[i])
				{
					chaFemale[_id].playDynamicBoneBust((ChaControlDefine.DynamicBoneKind)i, _aIsActive[i]);
					aIsActive[_id][i] = _aIsActive[i];
				}
			}
		}

		private void Shape(BreastShapeInfo[] _shapeInfo, int _id)
		{
			for (int i = 0; i < 2; i++)
			{
				int lR = i;
				BreastShapeInfo breastShapeInfo = _shapeInfo[i];
				BreastShapeInfo breastShapeInfo2 = aBreastShape[_id][i];
				if (breastShapeInfo.breast != breastShapeInfo2.breast)
				{
					for (int j = 0; j < ChaFileDefine.cf_BustShapeMaskID.Length - 1; j++)
					{
						int num = j;
						if (breastShapeInfo.breast[num] != breastShapeInfo2.breast[num])
						{
							if (breastShapeInfo.breast[num])
							{
								chaFemale[_id].DisableShapeBodyID(lR, num, disable: false);
							}
							else
							{
								chaFemale[_id].DisableShapeBodyID(lR, num, disable: true);
							}
						}
					}
					breastShapeInfo2.breast = breastShapeInfo.breast;
				}
				if (breastShapeInfo.nip != breastShapeInfo2.nip)
				{
					if (breastShapeInfo.nip)
					{
						chaFemale[_id].DisableShapeBodyID(lR, 7, disable: false);
					}
					else
					{
						chaFemale[_id].DisableShapeBodyID(lR, 7, disable: true);
					}
					breastShapeInfo2.nip = breastShapeInfo.nip;
				}
				aBreastShape[_id][i] = breastShapeInfo2;
			}
		}

		public void ResetShape(int _id)
		{
			if (chaFemale == null)
			{
				return;
			}
			for (int i = 0; i < ChaFileDefine.cf_BustShapeMaskID.Length; i++)
			{
				int id = i;
				if (chaFemale.ContainsKey(_id))
				{
					chaFemale[_id].DisableShapeBodyID(2, id, disable: false);
				}
			}
			if (!aIsActive.ContainsKey(_id))
			{
				return;
			}
			for (int j = 0; j < aIsActive[_id].Length; j++)
			{
				aIsActive[_id][j] = true;
				if (chaFemale.ContainsKey(_id))
				{
					chaFemale[_id].playDynamicBoneBust((ChaControlDefine.DynamicBoneKind)j, play: true);
				}
			}
		}
	}

	public List<Info> lstInfo = new List<Info>();

	public ChaControl chaFemale;

	public int femaleID;

	public bool isInit;

	[Tooltip("動いているかの確認用")]
	public bool[] aIsActive = new bool[4] { true, true, true, true };

	[Tooltip("動いているかの確認用")]
	public BreastShapeInfo[] aBreastShape = new BreastShapeInfo[2];

	private bool[] aYureEnableActive = new bool[4] { true, true, true, true };

	private BreastShapeInfo[] aBreastShapeEnable = new BreastShapeInfo[2];

	public YureCtrlMapH CtrlMapH = new YureCtrlMapH();

	private void Start()
	{
		for (int i = 0; i < 2; i++)
		{
			aBreastShape[i].MemberInit();
			aBreastShapeEnable[i].MemberInit();
		}
	}

	private void LateUpdate()
	{
		if (isInit && (bool)chaFemale)
		{
			Proc(chaFemale.getAnimatorStateInfo(0));
		}
	}

	public bool Release()
	{
		isInit = false;
		if (lstInfo != null)
		{
			lstInfo.Clear();
		}
		return true;
	}

	public bool Load(int _motionId, int category)
	{
		isInit = false;
		for (int i = 0; i < aIsActive.Length; i++)
		{
			aIsActive[i] = true;
		}
		for (int j = 0; j < 2; j++)
		{
			aBreastShape[j].MemberInit();
		}
		if ((bool)chaFemale)
		{
			for (int k = 0; k < aIsActive.Length; k++)
			{
				chaFemale.playDynamicBoneBust((ChaControlDefine.DynamicBoneKind)k, play: true);
			}
			Observable.EveryEndOfFrame().Take(1).Subscribe(delegate
			{
				DynamicBone_Ver02 dynamicBoneBustAndHip = chaFemale.GetDynamicBoneBustAndHip(ChaControlDefine.DynamicBoneKind.BreastL);
				dynamicBoneBustAndHip.enabled = false;
				dynamicBoneBustAndHip = chaFemale.GetDynamicBoneBustAndHip(ChaControlDefine.DynamicBoneKind.BreastR);
				dynamicBoneBustAndHip.enabled = false;
				dynamicBoneBustAndHip = chaFemale.GetDynamicBoneBustAndHip(ChaControlDefine.DynamicBoneKind.HipL);
				dynamicBoneBustAndHip.enabled = false;
				dynamicBoneBustAndHip = chaFemale.GetDynamicBoneBustAndHip(ChaControlDefine.DynamicBoneKind.HipR);
				dynamicBoneBustAndHip.enabled = false;
			});
			for (int l = 0; l < ChaFileDefine.cf_BustShapeMaskID.Length; l++)
			{
				int id = l;
				chaFemale.DisableShapeBodyID(2, id, disable: false);
			}
		}
		Dictionary<int, List<Info>> value = null;
		List<Info> value2 = null;
		if (Singleton<Manager.Resources>.Instance.HSceneTable.DicDicYure.TryGetValue(category, out value))
		{
			value.TryGetValue(_motionId, out value2);
		}
		if (value2 != null)
		{
			lstInfo = new List<Info>(value2);
		}
		else
		{
			lstInfo = new List<Info>();
		}
		isInit = true;
		return true;
	}

	public bool Proc(AnimatorStateInfo _ai)
	{
		if (!isInit)
		{
			return false;
		}
		Info info = null;
		if (lstInfo != null)
		{
			for (int i = 0; i < lstInfo.Count; i++)
			{
				if (_ai.IsName(lstInfo[i].nameAnimation) && lstInfo[i].nFemale == femaleID)
				{
					info = lstInfo[i];
					break;
				}
			}
		}
		if (info != null)
		{
			Active(info.aIsActive);
			Shape(info.aBreastShape);
			return true;
		}
		Active(aYureEnableActive);
		Shape(aBreastShapeEnable);
		return false;
	}

	private void Active(bool[] _aIsActive)
	{
		for (int i = 0; i < aIsActive.Length; i++)
		{
			if (aIsActive[i] != _aIsActive[i])
			{
				chaFemale.playDynamicBoneBust((ChaControlDefine.DynamicBoneKind)i, _aIsActive[i]);
				aIsActive[i] = _aIsActive[i];
			}
		}
	}

	private void Shape(BreastShapeInfo[] _shapeInfo)
	{
		for (int i = 0; i < 2; i++)
		{
			int lR = i;
			BreastShapeInfo breastShapeInfo = _shapeInfo[i];
			BreastShapeInfo breastShapeInfo2 = aBreastShape[i];
			if (breastShapeInfo.breast != breastShapeInfo2.breast)
			{
				for (int j = 0; j < ChaFileDefine.cf_BustShapeMaskID.Length - 1; j++)
				{
					int num = j;
					if (breastShapeInfo.breast[num] != breastShapeInfo2.breast[num])
					{
						if (breastShapeInfo.breast[num])
						{
							chaFemale.DisableShapeBodyID(lR, num, disable: false);
						}
						else
						{
							chaFemale.DisableShapeBodyID(lR, num, disable: true);
						}
					}
				}
				breastShapeInfo2.breast = breastShapeInfo.breast;
			}
			if (breastShapeInfo.nip != breastShapeInfo2.nip)
			{
				if (breastShapeInfo.nip)
				{
					chaFemale.DisableShapeBodyID(lR, 7, disable: false);
				}
				else
				{
					chaFemale.DisableShapeBodyID(lR, 7, disable: true);
				}
				breastShapeInfo2.nip = breastShapeInfo.nip;
			}
			aBreastShape[i] = breastShapeInfo2;
		}
	}

	public void ResetShape()
	{
		if (!(chaFemale == null))
		{
			for (int i = 0; i < ChaFileDefine.cf_BustShapeMaskID.Length; i++)
			{
				int id = i;
				chaFemale.DisableShapeBodyID(2, id, disable: false);
			}
			for (int j = 0; j < aIsActive.Length; j++)
			{
				aIsActive[j] = true;
				chaFemale.playDynamicBoneBust((ChaControlDefine.DynamicBoneKind)j, play: true);
			}
		}
	}
}
