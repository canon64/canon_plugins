using System;
using System.Collections.Generic;
using AIChara;
using Manager;
using UnityEngine;

public class YureCtrlMale : MonoBehaviour
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

		public int nMale;
	}

	public List<Info> lstInfo = new List<Info>();

	public ChaControl chaMale;

	public int MaleID;

	public bool isInit;

	[Tooltip("動いているかの確認用")]
	public bool[] aIsActive = new bool[4] { true, true, true, true };

	[Tooltip("動いているかの確認用")]
	public BreastShapeInfo[] aBreastShape = new BreastShapeInfo[2];

	private bool[] aYureEnableActive = new bool[4] { true, true, true, true };

	private BreastShapeInfo[] aBreastShapeEnable = new BreastShapeInfo[2];

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
		if (isInit && (bool)chaMale)
		{
			Proc(chaMale.getAnimatorStateInfo(0));
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
		if ((bool)chaMale)
		{
			for (int k = 0; k < aIsActive.Length; k++)
			{
				chaMale.playDynamicBoneBust((ChaControlDefine.DynamicBoneKind)k, play: true);
			}
			for (int l = 0; l < ChaFileDefine.cf_BustShapeMaskID.Length; l++)
			{
				int id = l;
				chaMale.DisableShapeBodyID(2, id, disable: false);
			}
		}
		Dictionary<int, List<Info>> value = null;
		List<Info> value2 = null;
		Singleton<Manager.Resources>.Instance.HSceneTable.DicDicYureMale.TryGetValue(category, out value);
		value?.TryGetValue(_motionId, out value2);
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
				if (_ai.IsName(lstInfo[i].nameAnimation) && lstInfo[i].nMale == MaleID)
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
				chaMale.playDynamicBoneBust((ChaControlDefine.DynamicBoneKind)i, _aIsActive[i]);
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
							chaMale.DisableShapeBodyID(lR, num, disable: false);
						}
						else
						{
							chaMale.DisableShapeBodyID(lR, num, disable: true);
						}
					}
				}
				breastShapeInfo2.breast = breastShapeInfo.breast;
			}
			if (breastShapeInfo.nip != breastShapeInfo2.nip)
			{
				if (breastShapeInfo.nip)
				{
					chaMale.DisableShapeBodyID(lR, 7, disable: false);
				}
				else
				{
					chaMale.DisableShapeBodyID(lR, 7, disable: true);
				}
				breastShapeInfo2.nip = breastShapeInfo.nip;
			}
			aBreastShape[i] = breastShapeInfo2;
		}
	}

	public void ResetShape()
	{
		if (!(chaMale == null))
		{
			for (int i = 0; i < ChaFileDefine.cf_BustShapeMaskID.Length; i++)
			{
				int id = i;
				chaMale.DisableShapeBodyID(2, id, disable: false);
			}
			for (int j = 0; j < aIsActive.Length; j++)
			{
				aIsActive[j] = true;
				chaMale.playDynamicBoneBust((ChaControlDefine.DynamicBoneKind)j, play: true);
			}
		}
	}
}
