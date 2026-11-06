using System.Collections.Generic;
using System.Text;
using AIChara;
using AIProject;
using Manager;
using UnityEngine;

public class DynamicBoneReferenceCtrl
{
	private struct RateInfo
	{
		public Vector3 sizeS;

		public Vector3 sizeM;

		public Vector3 sizeL;

		public Vector3 Calc(float _rate)
		{
			return (!(_rate >= 0.5f)) ? Vector3.Lerp(sizeS, sizeM, Mathf.InverseLerp(0f, 0.5f, _rate)) : Vector3.Lerp(sizeM, sizeL, Mathf.InverseLerp(0.5f, 1f, _rate));
		}
	}

	private struct Reference
	{
		public RateInfo position;

		public RateInfo rotation;

		public RateInfo scale;
	}

	private bool isInit;

	private ChaControl chaFemale;

	private DynamicBone_Ver02.BonePtn[] bonePtns = new DynamicBone_Ver02.BonePtn[2];

	private List<Transform>[] lstsTrans = new List<Transform>[2];

	private List<string> row = new List<string>();

	private StringBuilder sbAssetName = new StringBuilder();

	private List<Reference>[] lstsRef = new List<Reference>[2];

	public bool Init(ChaControl _female)
	{
		chaFemale = _female;
		if (chaFemale == null)
		{
			return false;
		}
		DynamicBone_Ver02[] array = new DynamicBone_Ver02[2]
		{
			chaFemale.GetDynamicBoneBustAndHip(ChaControlDefine.DynamicBoneKind.BreastL),
			chaFemale.GetDynamicBoneBustAndHip(ChaControlDefine.DynamicBoneKind.BreastR)
		};
		for (int i = 0; i < 2; i++)
		{
			lstsRef[i] = new List<Reference>();
			bonePtns[i] = new DynamicBone_Ver02.BonePtn();
			lstsTrans[i] = new List<Transform>();
			if (array[i] != null && array[i].Patterns.Count > 0)
			{
				bonePtns[i] = array[i].Patterns[0];
				for (int j = 1; j < bonePtns[i].Params.Count; j++)
				{
					lstsTrans[i].Add(bonePtns[i].Params[j].RefTransform);
				}
			}
		}
		return true;
	}

	public bool Load(string _assetpath, string _file)
	{
		List<bool>[] array = new List<bool>[2];
		List<bool>[] array2 = new List<bool>[2];
		isInit = false;
		for (int i = 0; i < 2; i++)
		{
			InitDynamicBoneReferenceBone(bonePtns[i], lstsTrans[i]);
			if (lstsRef[i] != null)
			{
				lstsRef[i].Clear();
			}
			array[i] = new List<bool>();
			array[i].Add(item: false);
			array2[i] = new List<bool>();
			array2[i].Add(item: false);
		}
		if (_file == string.Empty)
		{
			return false;
		}
		sbAssetName.Clear();
		sbAssetName.Append(_file.Remove(3, 2));
		List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(_assetpath);
		Reference item = default(Reference);
		for (int j = 0; j < assetBundleNameListFromPath.Count; j++)
		{
			bool flag = false;
			if (!GlobalMethod.AssetFileExist(assetBundleNameListFromPath[j], sbAssetName.ToString(), string.Empty))
			{
				return false;
			}
			ExcelData excelData = CommonLib.LoadAsset<ExcelData>(assetBundleNameListFromPath[j], sbAssetName.ToString(), clone: false, string.Empty);
			Singleton<HSceneManager>.Instance.hashUseAssetBundle.Add(assetBundleNameListFromPath[j]);
			if (excelData == null)
			{
				return false;
			}
			int num = 3;
			while (num < excelData.MaxCell)
			{
				int num2 = num - 3;
				row = excelData.list[num++].list;
				int num3 = 2;
				item.position.sizeS = new Vector3(float.Parse(row.GetElement(num3++)), float.Parse(row.GetElement(num3++)), float.Parse(row.GetElement(num3++)));
				item.position.sizeM = new Vector3(float.Parse(row.GetElement(num3++)), float.Parse(row.GetElement(num3++)), float.Parse(row.GetElement(num3++)));
				item.position.sizeL = new Vector3(float.Parse(row.GetElement(num3++)), float.Parse(row.GetElement(num3++)), float.Parse(row.GetElement(num3++)));
				item.rotation.sizeS = new Vector3(float.Parse(row.GetElement(num3++)), float.Parse(row.GetElement(num3++)), float.Parse(row.GetElement(num3++)));
				item.rotation.sizeM = new Vector3(float.Parse(row.GetElement(num3++)), float.Parse(row.GetElement(num3++)), float.Parse(row.GetElement(num3++)));
				item.rotation.sizeL = new Vector3(float.Parse(row.GetElement(num3++)), float.Parse(row.GetElement(num3++)), float.Parse(row.GetElement(num3++)));
				item.scale.sizeS = new Vector3(float.Parse(row.GetElement(num3++)), float.Parse(row.GetElement(num3++)), float.Parse(row.GetElement(num3++)));
				item.scale.sizeM = new Vector3(float.Parse(row.GetElement(num3++)), float.Parse(row.GetElement(num3++)), float.Parse(row.GetElement(num3++)));
				item.scale.sizeL = new Vector3(float.Parse(row.GetElement(num3++)), float.Parse(row.GetElement(num3++)), float.Parse(row.GetElement(num3++)));
				array[num2 / 4].Add(row.GetElement(num3++) == "1");
				array2[num2 / 4].Add(row.GetElement(num3++) == "1");
				lstsRef[num2 / 4].Add(item);
			}
		}
		for (int k = 0; k < 2; k++)
		{
			SetDynamicBoneRotationCalc(bonePtns[k], array[k]);
			SetDynamicBoneMoveLimitFlag(bonePtns[k], array2[k]);
		}
		isInit = true;
		return true;
	}

	public bool Proc()
	{
		if (!isInit)
		{
			return false;
		}
		float shapeBodyValue = chaFemale.GetShapeBodyValue(1);
		for (int i = 0; i < 2; i++)
		{
			if (lstsTrans[i].Count != lstsRef[i].Count)
			{
				continue;
			}
			for (int j = 0; j < lstsRef[i].Count; j++)
			{
				if (!(lstsTrans[i][j] == null))
				{
					lstsTrans[i][j].localPosition = lstsRef[i][j].position.Calc(shapeBodyValue);
					lstsTrans[i][j].localRotation = Quaternion.Euler(lstsRef[i][j].rotation.Calc(shapeBodyValue));
					lstsTrans[i][j].localScale = lstsRef[i][j].scale.Calc(shapeBodyValue);
				}
			}
		}
		return true;
	}

	public bool InitDynamicBoneReferenceBone()
	{
		if (!isInit)
		{
			return false;
		}
		for (int i = 0; i < 2; i++)
		{
			InitDynamicBoneReferenceBone(bonePtns[i], lstsTrans[i]);
		}
		return true;
	}

	private bool InitDynamicBoneReferenceBone(DynamicBone_Ver02.BonePtn _ptn, List<Transform> _lstTrans)
	{
		if (_ptn == null || _lstTrans == null)
		{
			return false;
		}
		List<bool> list = new List<bool>();
		list.Add(item: false);
		list.Add(item: true);
		list.Add(item: false);
		list.Add(item: true);
		list.Add(item: false);
		List<bool> lstBool = list;
		SetDynamicBoneRotationCalc(_ptn, lstBool);
		list = new List<bool>();
		list.Add(item: false);
		list.Add(item: false);
		list.Add(item: false);
		list.Add(item: false);
		list.Add(item: false);
		List<bool> lstBool2 = list;
		SetDynamicBoneMoveLimitFlag(_ptn, lstBool2);
		foreach (Transform _lstTran in _lstTrans)
		{
			if (!(_lstTran == null))
			{
				_lstTran.localPosition = Vector3.zero;
				_lstTran.localRotation = Quaternion.identity;
				_lstTran.localScale = Vector3.one;
			}
		}
		return true;
	}

	private bool SetDynamicBoneRotationCalc(DynamicBone_Ver02.BonePtn _ptn, List<bool> _lstBool)
	{
		if (_ptn == null || _lstBool == null)
		{
			return false;
		}
		if (_lstBool.Count != _ptn.Params.Count)
		{
			return false;
		}
		for (int i = 0; i < _ptn.Params.Count; i++)
		{
			_ptn.Params[i].IsRotationCalc = _lstBool[i];
		}
		for (int j = 0; j < _ptn.ParticlePtns.Count && j < _lstBool.Count; j++)
		{
			_ptn.ParticlePtns[j].IsRotationCalc = _lstBool[j];
		}
		return true;
	}

	private bool SetDynamicBoneMoveLimitFlag(DynamicBone_Ver02.BonePtn _ptn, List<bool> _lstBool)
	{
		if (_ptn == null || _lstBool == null)
		{
			return false;
		}
		if (_lstBool.Count != _ptn.Params.Count)
		{
			return false;
		}
		for (int i = 0; i < _ptn.Params.Count; i++)
		{
			_ptn.Params[i].IsMoveLimit = _lstBool[i];
		}
		for (int j = 0; j < _ptn.ParticlePtns.Count && j < _lstBool.Count; j++)
		{
			_ptn.ParticlePtns[j].IsMoveLimit = _lstBool[j];
		}
		return true;
	}
}
