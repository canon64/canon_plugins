using System.Collections.Generic;
using System.Text;
using AIChara;
using IllusionUtility.GetUtility;
using Manager;
using UnityEngine;

public class MetaballCtrl
{
	public struct ShootInfo
	{
		public bool isInside;

		public string nameAnimation;

		public float timeShoot;

		public float timeOld;
	}

	public class ShootCtrl
	{
		public List<ShootInfo> lstShoot = new List<ShootInfo>();

		public bool IsAnimation(bool _isInside, AnimatorStateInfo _stateInfo, ShootInfo info)
		{
			for (int i = 0; i < lstShoot.Count; i++)
			{
				info = lstShoot[i];
				if (!_stateInfo.IsName(info.nameAnimation))
				{
					info.timeOld = 0f;
					lstShoot[i] = info;
				}
				else if (!info.isInside || _isInside)
				{
					if (!(info.timeOld > info.timeShoot) && !(info.timeShoot >= _stateInfo.normalizedTime))
					{
						info.timeOld = _stateInfo.normalizedTime;
						lstShoot[i] = info;
						return true;
					}
					info.timeOld = _stateInfo.normalizedTime;
					lstShoot[i] = info;
				}
			}
			return false;
		}
	}

	public class MetaballParameterInfo
	{
		public Vector2 speedS;

		public Vector2 speedM;

		public Vector2 speedL;

		public Vector2 rotationS;

		public Vector2 rotationM;

		public Vector2 rotationL;

		public GameObject objShoot;

		public GameObject objParent;
	}

	public class MetaballParameter
	{
		public MetaballParameterInfo[] param = new MetaballParameterInfo[2];

		public MetaballParameter()
		{
			param[0] = new MetaballParameterInfo();
			param[1] = new MetaballParameterInfo();
		}
	}

	private List<ExcelData.Param> infolist = new List<ExcelData.Param>();

	private ShootInfo info = default(ShootInfo);

	private const int cMetaBallNum = 7;

	public MetaballShoot[] ctrlMetaball = new MetaballShoot[7];

	private UpdateMeta[] ctrlUpdate;

	private ShootCtrl[] actrlShoot = new ShootCtrl[7];

	private GameObject[] aobjBody = new GameObject[4];

	private MetaballParameter[] aParam = new MetaballParameter[7];

	private HParticleCtrl particle;

	public MetaballCtrl(GameObject _objBase, GameObject _objMale, GameObject _objFemale, ChaControl _customFemale)
	{
		if ((bool)_objBase)
		{
			ctrlUpdate = _objBase.GetComponentsInChildren<UpdateMeta>();
			UpdateMeta[] array = ctrlUpdate;
			foreach (UpdateMeta updateMeta in array)
			{
				foreach (MetaballShoot item in updateMeta.lstShoot)
				{
					if (item.comment.Contains("中出し1"))
					{
						ctrlMetaball[3] = item;
					}
					else if (item.comment.Contains("外出し1"))
					{
						ctrlMetaball[4] = item;
						ctrlMetaball[4].chaCustom = _customFemale;
					}
					else if (item.comment.Contains("外出し2"))
					{
						ctrlMetaball[5] = item;
						ctrlMetaball[5].chaCustom = _customFemale;
					}
					else if (item.comment.Contains("吐く2"))
					{
						ctrlMetaball[6] = item;
						ctrlMetaball[6].chaCustom = _customFemale;
					}
					else if (item.comment.Contains("中出し"))
					{
						ctrlMetaball[0] = item;
						ctrlMetaball[0].chaCustom = _customFemale;
					}
					else if (item.comment.Contains("外出し"))
					{
						ctrlMetaball[1] = item;
						ctrlMetaball[1].chaCustom = _customFemale;
					}
					else if (item.comment.Contains("吐く"))
					{
						ctrlMetaball[2] = item;
						ctrlMetaball[2].chaCustom = _customFemale;
					}
				}
			}
		}
		aobjBody[0] = _objMale;
		aobjBody[1] = _objFemale;
		for (int j = 0; j < 7; j++)
		{
			actrlShoot[j] = new ShootCtrl();
			aParam[j] = new MetaballParameter();
		}
	}

	public void SetParticle(HParticleCtrl _particle)
	{
		particle = _particle;
	}

	public bool Load(string _strAssetPath, string _nameFile, GameObject _objMale, GameObject _objMale1, GameObject _objFemale1 = null, ChaControl _customFemale1 = null)
	{
		aobjBody[0] = _objMale;
		aobjBody[2] = _objMale1;
		if (_objFemale1 != null)
		{
			aobjBody[3] = _objFemale1;
			if (ctrlMetaball[4] != null)
			{
				ctrlMetaball[4].chaCustom = _customFemale1;
			}
		}
		ShootCtrl[] array = actrlShoot;
		foreach (ShootCtrl shootCtrl in array)
		{
			shootCtrl.lstShoot.Clear();
		}
		string empty = string.Empty;
		empty = ((!_nameFile.ContainsAny("f2", "f1")) ? _nameFile.Remove(3, 2) : (_nameFile.Contains("ai3p") ? _nameFile.Remove(4, 3) : _nameFile.Remove(3, 3)));
		infolist = GlobalMethod.LoadExcelDataAlFindlFile(_strAssetPath, empty, 1, 1, 3, 1);
		if (infolist == null)
		{
			GlobalMethod.DebugLog("excel : [" + empty + "]は読み込めなかった\u3000警告無しでここに来たら[" + _strAssetPath + "]自体がない", 1);
			return false;
		}
		int count = infolist.Count;
		if (count < 3)
		{
			GlobalMethod.DebugLog("excel : [" + empty + "]は必ず3行必要", 1);
			return false;
		}
		StringBuilder stringBuilder = new StringBuilder();
		StringBuilder stringBuilder2 = new StringBuilder();
		StringBuilder stringBuilder3 = new StringBuilder();
		GameObject gameObject = null;
		string empty2 = string.Empty;
		for (int j = 0; j < count; j++)
		{
			int num = 0;
			if (ctrlMetaball[j] == null)
			{
				continue;
			}
			ctrlMetaball[j].isEnable = infolist[j].list[num++] == "1";
			if (!ctrlMetaball[j].isEnable)
			{
				continue;
			}
			int intTryParse = GlobalMethod.GetIntTryParse(infolist[j].list[num++]);
			ctrlMetaball[j].ShootAxis = ((!aobjBody[intTryParse]) ? null : aobjBody[intTryParse].transform.FindLoop(infolist[j].list[num++]));
			if (aobjBody[intTryParse] == null)
			{
				num++;
			}
			intTryParse = GlobalMethod.GetIntTryParse(infolist[j].list[num++]);
			gameObject = ((!aobjBody[intTryParse]) ? null : aobjBody[intTryParse].transform.FindLoop(infolist[j].list[num++]));
			ctrlMetaball[j].parentTransform = ((!gameObject) ? null : gameObject.transform);
			aParam[j].param[0].objParent = gameObject;
			if (aobjBody[intTryParse] == null)
			{
				num++;
			}
			intTryParse = GlobalMethod.GetIntTryParse(infolist[j].list[num++]);
			gameObject = ((!aobjBody[intTryParse]) ? null : aobjBody[intTryParse].transform.FindLoop(infolist[j].list[num++]));
			aParam[j].param[1].objParent = gameObject;
			if (aobjBody[intTryParse] == null)
			{
				num++;
			}
			stringBuilder.Clear();
			stringBuilder.Append(infolist[j].list[num++]);
			stringBuilder2.Clear();
			stringBuilder2.Append(infolist[j].list[num++]);
			stringBuilder3.Clear();
			stringBuilder3.Append(infolist[j].list[num++]);
			aParam[j].param[0].objShoot = LoadSiruObject(stringBuilder.ToString(), stringBuilder2.ToString(), stringBuilder3.ToString());
			ctrlMetaball[j].ShootObj = aParam[j].param[0].objShoot;
			AssetBundleManager.UnloadAssetBundle(stringBuilder.ToString(), isUnloadForceRefCount: false, stringBuilder3.ToString());
			stringBuilder.Clear();
			stringBuilder.Append(infolist[j].list[num++]);
			stringBuilder2.Clear();
			stringBuilder2.Append(infolist[j].list[num++]);
			stringBuilder3.Clear();
			stringBuilder3.Append(infolist[j].list[num++]);
			aParam[j].param[1].objShoot = LoadSiruObject(stringBuilder.ToString(), stringBuilder2.ToString(), stringBuilder3.ToString());
			AssetBundleManager.UnloadAssetBundle(stringBuilder.ToString(), isUnloadForceRefCount: false, stringBuilder3.ToString());
			aParam[j].param[0].speedS = new Vector2(float.Parse(infolist[j].list[num++]), float.Parse(infolist[j].list[num++]));
			aParam[j].param[0].rotationS = new Vector2(float.Parse(infolist[j].list[num++]), float.Parse(infolist[j].list[num++]));
			aParam[j].param[0].speedM = new Vector2(float.Parse(infolist[j].list[num++]), float.Parse(infolist[j].list[num++]));
			aParam[j].param[0].rotationM = new Vector2(float.Parse(infolist[j].list[num++]), float.Parse(infolist[j].list[num++]));
			aParam[j].param[0].speedL = new Vector2(float.Parse(infolist[j].list[num++]), float.Parse(infolist[j].list[num++]));
			aParam[j].param[0].rotationL = new Vector2(float.Parse(infolist[j].list[num++]), float.Parse(infolist[j].list[num++]));
			ctrlMetaball[j].speedSMin = aParam[j].param[0].speedS.x;
			ctrlMetaball[j].speedSMax = aParam[j].param[0].speedS.y;
			ctrlMetaball[j].randomRotationS = aParam[j].param[0].rotationS;
			ctrlMetaball[j].speedMMin = aParam[j].param[0].speedM.x;
			ctrlMetaball[j].speedMMax = aParam[j].param[0].speedM.y;
			ctrlMetaball[j].randomRotationM = aParam[j].param[0].rotationM;
			ctrlMetaball[j].speedLMin = aParam[j].param[0].speedL.x;
			ctrlMetaball[j].speedLMax = aParam[j].param[0].speedL.y;
			ctrlMetaball[j].randomRotationL = aParam[j].param[0].rotationL;
			aParam[j].param[1].speedS = new Vector2(float.Parse(infolist[j].list[num++]), float.Parse(infolist[j].list[num++]));
			aParam[j].param[1].rotationS = new Vector2(float.Parse(infolist[j].list[num++]), float.Parse(infolist[j].list[num++]));
			aParam[j].param[1].speedM = new Vector2(float.Parse(infolist[j].list[num++]), float.Parse(infolist[j].list[num++]));
			aParam[j].param[1].rotationM = new Vector2(float.Parse(infolist[j].list[num++]), float.Parse(infolist[j].list[num++]));
			aParam[j].param[1].speedL = new Vector2(float.Parse(infolist[j].list[num++]), float.Parse(infolist[j].list[num++]));
			aParam[j].param[1].rotationL = new Vector2(float.Parse(infolist[j].list[num++]), float.Parse(infolist[j].list[num++]));
			while (infolist[j].list.Count > num)
			{
				empty2 = infolist[j].list[num++];
				if (empty2 == string.Empty)
				{
					break;
				}
				info = default(ShootInfo);
				info.isInside = empty2 == "1";
				info.nameAnimation = infolist[j].list[num++];
				if (!float.TryParse(infolist[j].list[num++], out var result))
				{
					result = 0f;
				}
				info.timeShoot = result;
				info.timeOld = 0f;
				actrlShoot[j].lstShoot.Add(info);
			}
		}
		return true;
	}

	public bool SetParameterFromState(int _state)
	{
		if (!GlobalMethod.RangeOn(_state, 0, 1))
		{
			return false;
		}
		for (int i = 0; i < ctrlMetaball.Length; i++)
		{
			if (ctrlMetaball[i] != null)
			{
				ctrlMetaball[i].speedSMin = aParam[i].param[_state].speedS.x;
				ctrlMetaball[i].speedSMax = aParam[i].param[_state].speedS.y;
				ctrlMetaball[i].randomRotationS = aParam[i].param[_state].rotationS;
				ctrlMetaball[i].speedMMin = aParam[i].param[_state].speedM.x;
				ctrlMetaball[i].speedMMax = aParam[i].param[_state].speedM.y;
				ctrlMetaball[i].randomRotationM = aParam[i].param[_state].rotationM;
				ctrlMetaball[i].speedLMin = aParam[i].param[_state].speedL.x;
				ctrlMetaball[i].speedLMax = aParam[i].param[_state].speedL.y;
				ctrlMetaball[i].randomRotationL = aParam[i].param[_state].rotationL;
				ctrlMetaball[i].ShootObj = aParam[i].param[_state].objShoot;
				ctrlMetaball[i].parentTransform = ((!aParam[i].param[_state].objParent) ? null : aParam[i].param[_state].objParent.transform);
			}
		}
		return true;
	}

	public bool Proc(AnimatorStateInfo _stateInfo, bool _isInside = false)
	{
		if (Singleton<HSceneFlagCtrl>.Instance.semenType == 0)
		{
			return true;
		}
		for (int i = 0; i < actrlShoot.Length; i++)
		{
			if ((Singleton<HSceneFlagCtrl>.Instance.semenType == 2 && i != 1 && i != 4 && i != 5) || !actrlShoot[i].IsAnimation(_isInside, _stateInfo, info))
			{
				continue;
			}
			if (Singleton<HSceneFlagCtrl>.Instance.semenType == 2)
			{
				if (particle != null)
				{
					if (i == 1)
					{
						particle.Play(3);
						particle.Play(8);
					}
					if (i == 4)
					{
						particle.Play(7);
						particle.Play(9);
					}
					if (i == 5)
					{
						particle.Play(10);
					}
				}
			}
			else if (ctrlMetaball[i] != null)
			{
				ctrlMetaball[i].ShootMetaBallStart();
			}
		}
		return true;
	}

	public bool Clear()
	{
		UpdateMeta[] array = ctrlUpdate;
		foreach (UpdateMeta updateMeta in array)
		{
			updateMeta.MetaBallClear();
		}
		return true;
	}

	private GameObject LoadSiruObject(string _pathAsset, string _nameFile, string _manifest)
	{
		if (_nameFile == string.Empty)
		{
			return null;
		}
		GameObject result = CommonLib.LoadAsset<GameObject>(_pathAsset, _nameFile, clone: false, _manifest);
		Singleton<HSceneManager>.Instance.hashUseAssetBundle.Add(_pathAsset);
		return result;
	}
}
