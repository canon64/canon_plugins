using System;
using System.Collections.Generic;
using AIChara;
using AIProject;
using Manager;
using UnityEngine;

public class HMotionEyeNeckLesPlayer : MonoBehaviour
{
	[Serializable]
	public struct EyeNeck
	{
		[Label("アニメーション名")]
		public string anim;

		[Label("首コンフィグ無視")]
		public bool isConfigDisregardNeck;

		[Label("目コンフィグ無視")]
		public bool isConfigDisregardEye;

		[Label("目の開き")]
		public float openEye;

		[Label("口の開き")]
		public float openMouth;

		[Label("眉の形")]
		public int eyeBlow;

		[Label("目の形")]
		public int eye;

		[Label("口の形")]
		public int mouth;

		[RangeLabel("涙", 0f, 1f)]
		public float tear;

		[RangeLabel("頬赤", 0f, 1f)]
		public float cheek;

		[Label("瞬き")]
		public bool blink;

		public EyeNeckFace faceinfo;

		public void Init()
		{
			anim = string.Empty;
			isConfigDisregardNeck = false;
			isConfigDisregardEye = false;
			faceinfo.Init();
		}
	}

	[Serializable]
	public struct EyeNeckFace
	{
		[Label("首挙動")]
		public int Neckbehaviour;

		[Label("目挙動")]
		public int Eyebehaviour;

		[Label("首ターゲット")]
		public int targetNeck;

		[Label("首角度")]
		public Vector3[] NeckRot;

		[Label("頭角度")]
		public Vector3[] HeadRot;

		[Label("目ターゲット")]
		public int targetEye;

		[Label("視線角度")]
		public Vector3[] EyeRot;

		public void Init()
		{
			Neckbehaviour = 0;
			Eyebehaviour = 0;
			targetEye = 0;
			EyeRot = new Vector3[2]
			{
				Vector3.zero,
				Vector3.zero
			};
			targetNeck = 0;
			NeckRot = new Vector3[2]
			{
				Vector3.zero,
				Vector3.zero
			};
			HeadRot = new Vector3[2]
			{
				Vector3.zero,
				Vector3.zero
			};
		}
	}

	[Label("相手女顔オブジェクト名")]
	public string strFemaleHead = string.Empty;

	[Label("相手女性器オブジェクト名")]
	public string strFemaleGenital = string.Empty;

	[SerializeField]
	private List<EyeNeck> lstEyeNeck = new List<EyeNeck>();

	[DisabledGroup("女クラス")]
	[SerializeField]
	private ChaControl chaFemale;

	[DisabledGroup("自分性器オブジェクト")]
	[SerializeField]
	private GameObject objGenitalSelf;

	[DisabledGroup("女相手顔オブジェクト")]
	[SerializeField]
	private GameObject objFemale1Head;

	[DisabledGroup("女相手性器オブジェクト")]
	[SerializeField]
	private GameObject objFemale1Genital;

	private Transform LoopParent;

	private EyeNeck en;

	private EyeNeck eyeNeck;

	private EyeNeckFace enFace;

	private int ID;

	private AnimatorStateInfo ai;

	private Transform NeckTrs;

	private Transform HeadTrs;

	private Transform[] EyeTrs;

	private Quaternion BackUpNeck;

	private Quaternion BackUpHead;

	private Quaternion[] BackUpEye;

	private float ChangeTimeNeck;

	private int NeckType;

	private float ChangeTimeEye;

	private int EyeType;

	private bool[] bFaceInfo = new bool[2];

	private int nYuragiType;

	private string OldAnimName;

	private HScene hscene;

	private ExcelData excelData;

	private EyeNeck info;

	private List<string> row = new List<string>();

	private string abName = string.Empty;

	private string assetName = string.Empty;

	public bool NowEndADV;

	private Transform[] getChild;

	private float[] nowleapSpeed = new float[2];

	public bool Init(ChaControl _female, int id)
	{
		Release();
		ID = id;
		chaFemale = _female;
		NeckTrs = chaFemale.neckLookCtrl.neckLookScript.aBones[0].neckBone;
		HeadTrs = chaFemale.neckLookCtrl.neckLookScript.aBones[1].neckBone;
		EyeTrs = new Transform[2]
		{
			chaFemale.eyeLookCtrl.eyeLookScript.eyeObjs[0].eyeTransform,
			chaFemale.eyeLookCtrl.eyeLookScript.eyeObjs[1].eyeTransform
		};
		BackUpNeck = NeckTrs.localRotation;
		BackUpHead = HeadTrs.localRotation;
		BackUpEye = new Quaternion[2]
		{
			EyeTrs[0].localRotation,
			EyeTrs[1].localRotation
		};
		objGenitalSelf = null;
		if ((bool)chaFemale && (bool)chaFemale.objBodyBone)
		{
			LoopParent = chaFemale.objBodyBone.transform;
			if (strFemaleGenital != string.Empty)
			{
				objGenitalSelf = GetObjectName(LoopParent, strFemaleGenital);
			}
		}
		if (hscene == null)
		{
			hscene = Singleton<HSceneManager>.Instance.HSceneSet.GetComponentInChildren<HScene>();
		}
		return true;
	}

	public void Release()
	{
		lstEyeNeck.Clear();
	}

	private GameObject GetObjectName(Transform top, string name)
	{
		getChild = top.GetComponentsInChildren<Transform>();
		for (int i = 0; i < getChild.Length; i++)
		{
			if (!(getChild[i].name != name))
			{
				return getChild[i].gameObject;
			}
		}
		return null;
	}

	public bool Load(string _assetpath, string _file)
	{
		lstEyeNeck.Clear();
		ChangeTimeEye = 0f;
		nowleapSpeed[1] = 0f;
		ref Quaternion reference = ref BackUpEye[0];
		reference = EyeTrs[0].localRotation;
		ref Quaternion reference2 = ref BackUpEye[1];
		reference2 = EyeTrs[1].localRotation;
		ChangeTimeNeck = 0f;
		nowleapSpeed[0] = 0f;
		BackUpNeck = NeckTrs.localRotation;
		BackUpHead = HeadTrs.localRotation;
		if (_file == string.Empty)
		{
			return false;
		}
		List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(_assetpath);
		assetBundleNameListFromPath.Sort();
		abName = string.Empty;
		assetName = string.Empty;
		excelData = null;
		info.Init();
		Vector3 zero = Vector3.zero;
		for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
		{
			abName = assetBundleNameListFromPath[i];
			assetName = _file;
			bool flag = false;
			if (!GlobalMethod.AssetFileExist(abName, assetName, string.Empty))
			{
				continue;
			}
			excelData = CommonLib.LoadAsset<ExcelData>(abName, assetName, clone: false, string.Empty);
			Singleton<HSceneManager>.Instance.hashUseAssetBundle.Add(abName);
			if (excelData == null)
			{
				continue;
			}
			int num = 3;
			while (num < excelData.MaxCell)
			{
				row = excelData.list[num++].list;
				int num2 = 0;
				info = default(EyeNeck);
				info.Init();
				if (row.Count == 0)
				{
					continue;
				}
				string element = row.GetElement(num2++);
				if (!element.IsNullOrEmpty())
				{
					info.anim = element;
					info.isConfigDisregardNeck = row.GetElement(num2++) == "1";
					info.isConfigDisregardEye = row.GetElement(num2++) == "1";
					float result = 0f;
					if (!float.TryParse(row.GetElement(num2++), out result))
					{
						result = 0f;
					}
					info.openEye = result;
					float result2 = 0f;
					if (!float.TryParse(row.GetElement(num2++), out result2))
					{
						result2 = 0f;
					}
					info.openMouth = result2;
					int result3 = 0;
					if (!int.TryParse(row.GetElement(num2++), out result3))
					{
						result3 = 0;
					}
					info.eyeBlow = result3;
					int result4 = 0;
					if (!int.TryParse(row.GetElement(num2++), out result4))
					{
						result4 = 0;
					}
					info.eye = result4;
					int result5 = 0;
					if (!int.TryParse(row.GetElement(num2++), out result5))
					{
						result5 = 0;
					}
					info.mouth = result5;
					float result6 = 0f;
					if (!float.TryParse(row.GetElement(num2++), out result6))
					{
						result6 = 0f;
					}
					info.tear = result6;
					float result7 = 0f;
					if (!float.TryParse(row.GetElement(num2++), out result7))
					{
						result7 = 0f;
					}
					info.cheek = result7;
					info.blink = row.GetElement(num2++) == "1";
					info.faceinfo.Neckbehaviour = int.Parse(row.GetElement(num2++));
					info.faceinfo.Eyebehaviour = int.Parse(row.GetElement(num2++));
					info.faceinfo.targetNeck = int.Parse(row.GetElement(num2++));
					if (!float.TryParse(row.GetElement(num2++), out zero.x))
					{
						zero.x = 0f;
					}
					if (!float.TryParse(row.GetElement(num2++), out zero.y))
					{
						zero.y = 0f;
					}
					if (!float.TryParse(row.GetElement(num2++), out zero.z))
					{
						zero.z = 0f;
					}
					info.faceinfo.NeckRot[0] = zero;
					if (!float.TryParse(row.GetElement(num2++), out zero.x))
					{
						zero.x = 0f;
					}
					if (!float.TryParse(row.GetElement(num2++), out zero.y))
					{
						zero.y = 0f;
					}
					if (!float.TryParse(row.GetElement(num2++), out zero.z))
					{
						zero.z = 0f;
					}
					info.faceinfo.NeckRot[1] = zero;
					if (!float.TryParse(row.GetElement(num2++), out zero.x))
					{
						zero.x = 0f;
					}
					if (!float.TryParse(row.GetElement(num2++), out zero.y))
					{
						zero.y = 0f;
					}
					if (!float.TryParse(row.GetElement(num2++), out zero.z))
					{
						zero.z = 0f;
					}
					info.faceinfo.HeadRot[0] = zero;
					if (!float.TryParse(row.GetElement(num2++), out zero.x))
					{
						zero.x = 0f;
					}
					if (!float.TryParse(row.GetElement(num2++), out zero.y))
					{
						zero.y = 0f;
					}
					if (!float.TryParse(row.GetElement(num2++), out zero.z))
					{
						zero.z = 0f;
					}
					info.faceinfo.HeadRot[1] = zero;
					info.faceinfo.targetEye = int.Parse(row.GetElement(num2++));
					if (!float.TryParse(row.GetElement(num2++), out zero.x))
					{
						zero.x = 0f;
					}
					if (!float.TryParse(row.GetElement(num2++), out zero.y))
					{
						zero.y = 0f;
					}
					if (!float.TryParse(row.GetElement(num2++), out zero.z))
					{
						zero.z = 0f;
					}
					info.faceinfo.EyeRot[0] = zero;
					if (!float.TryParse(row.GetElement(num2++), out zero.x))
					{
						zero.x = 0f;
					}
					if (!float.TryParse(row.GetElement(num2++), out zero.y))
					{
						zero.y = 0f;
					}
					if (!float.TryParse(row.GetElement(num2++), out zero.z))
					{
						zero.z = 0f;
					}
					info.faceinfo.EyeRot[1] = zero;
					lstEyeNeck.Add(info);
				}
			}
		}
		return true;
	}

	public bool SetPartner(GameObject _objFemale1Bone)
	{
		objFemale1Head = null;
		objFemale1Genital = null;
		if ((bool)_objFemale1Bone)
		{
			LoopParent = _objFemale1Bone.transform;
			if (strFemaleHead != string.Empty)
			{
				objFemale1Head = GetObjectName(LoopParent, strFemaleHead);
			}
			if (strFemaleGenital != string.Empty)
			{
				objFemale1Genital = GetObjectName(LoopParent, strFemaleGenital);
			}
		}
		return true;
	}

	public bool Proc(AnimatorStateInfo _ai, int _main)
	{
		ai = _ai;
		for (int i = 0; i < lstEyeNeck.Count; i++)
		{
			en = lstEyeNeck[i];
			if (_ai.IsName(en.anim))
			{
				enFace = en.faceinfo;
				eyeNeck = en;
			}
		}
		chaFemale.ChangeEyesOpenMax(eyeNeck.openEye);
		FBSCtrlMouth mouthCtrl = chaFemale.mouthCtrl;
		if (mouthCtrl != null)
		{
			mouthCtrl.OpenMin = en.openMouth;
		}
		chaFemale.ChangeEyebrowPtn(en.eyeBlow);
		chaFemale.ChangeEyesPtn(en.eye);
		chaFemale.ChangeMouthPtn(eyeNeck.mouth);
		if (eyeNeck.mouth == 10 || eyeNeck.mouth == 13)
		{
			chaFemale.DisableShapeMouth(disable: true);
		}
		else
		{
			chaFemale.DisableShapeMouth(disable: false);
		}
		chaFemale.ChangeTearsRate(eyeNeck.tear);
		chaFemale.ChangeHohoAkaRate(eyeNeck.cheek);
		chaFemale.ChangeEyesBlinkFlag(eyeNeck.blink);
		SetNeckTarget((!en.isConfigDisregardNeck) ? enFace.targetNeck : enFace.targetNeck);
		SetEyesTarget((!en.isConfigDisregardEye) ? enFace.targetEye : enFace.targetEye);
		SetBehaviourEyes((!en.isConfigDisregardEye) ? enFace.Eyebehaviour : enFace.Eyebehaviour);
		SetBehaviourNeck((!en.isConfigDisregardNeck) ? enFace.Neckbehaviour : enFace.Neckbehaviour);
		if (OldAnimName != en.anim)
		{
			ChangeTimeEye = 0f;
			nowleapSpeed[1] = 0f;
			ref Quaternion reference = ref BackUpEye[0];
			reference = EyeTrs[0].localRotation;
			ref Quaternion reference2 = ref BackUpEye[1];
			reference2 = EyeTrs[1].localRotation;
			ChangeTimeNeck = 0f;
			nowleapSpeed[0] = 0f;
			BackUpNeck = NeckTrs.localRotation;
			BackUpHead = HeadTrs.localRotation;
			OldAnimName = en.anim;
		}
		return true;
	}

	private void LateUpdate()
	{
		EyeNeckCalc();
	}

	public void EyeNeckCalc()
	{
		if (chaFemale == null || NowEndADV)
		{
			return;
		}
		for (int i = 0; i < lstEyeNeck.Count; i++)
		{
			en = lstEyeNeck[i];
			if (!ai.IsName(en.anim))
			{
				continue;
			}
			enFace = en.faceinfo;
			if (enFace.targetNeck == 3)
			{
				if (Singleton<HSceneFlagCtrl>.Instance.motions[ID] < 0.5f)
				{
					if (nYuragiType != 0)
					{
						nYuragiType = 0;
						ChangeTimeNeck = 0f;
						nowleapSpeed[0] = 0f;
					}
				}
				else if (nYuragiType != 1)
				{
					nYuragiType = 1;
					ChangeTimeNeck = 0f;
					nowleapSpeed[0] = 0f;
				}
				NeckCalc(enFace.NeckRot[nYuragiType], enFace.HeadRot[nYuragiType]);
			}
			if (enFace.targetEye != 3)
			{
				break;
			}
			if (Singleton<HSceneFlagCtrl>.Instance.motions[ID] < 0.5f)
			{
				if (nYuragiType != 0)
				{
					nYuragiType = 0;
					ChangeTimeEye = 0f;
					nowleapSpeed[1] = 0f;
				}
			}
			else if (nYuragiType != 1)
			{
				nYuragiType = 1;
				ChangeTimeEye = 0f;
				nowleapSpeed[1] = 0f;
			}
			EyeCalc(enFace.EyeRot[nYuragiType]);
			break;
		}
	}

	private bool SetEyesTarget(int _tag)
	{
		switch (_tag)
		{
		case 1:
			chaFemale.ChangeLookEyesTarget(1, (!objFemale1Head) ? null : objFemale1Head.transform);
			break;
		case 2:
			chaFemale.ChangeLookEyesTarget(1, (!objFemale1Genital) ? null : objFemale1Genital.transform);
			break;
		case 4:
			chaFemale.ChangeLookEyesTarget(1, (!objGenitalSelf) ? null : objGenitalSelf.transform);
			break;
		default:
			chaFemale.ChangeLookEyesTarget(0);
			break;
		case 3:
			break;
		}
		if (EyeType != _tag)
		{
			ChangeTimeEye = 0f;
			nowleapSpeed[1] = 0f;
			ref Quaternion reference = ref BackUpEye[0];
			reference = EyeTrs[0].localRotation;
			ref Quaternion reference2 = ref BackUpEye[1];
			reference2 = EyeTrs[1].localRotation;
		}
		EyeType = _tag;
		return true;
	}

	private bool SetNeckTarget(int _tag)
	{
		switch (_tag)
		{
		case 1:
			chaFemale.ChangeLookNeckTarget(1, (!objFemale1Head) ? null : objFemale1Head.transform);
			break;
		case 2:
			chaFemale.ChangeLookNeckTarget(1, (!objFemale1Genital) ? null : objFemale1Genital.transform);
			break;
		case 4:
			chaFemale.ChangeLookNeckTarget(1, (!objGenitalSelf) ? null : objGenitalSelf.transform);
			break;
		default:
			chaFemale.ChangeLookNeckTarget(0);
			break;
		case 3:
			break;
		}
		if (NeckType != _tag)
		{
			ChangeTimeNeck = 0f;
			nowleapSpeed[0] = 0f;
			BackUpNeck = NeckTrs.localRotation;
			BackUpHead = HeadTrs.localRotation;
		}
		NeckType = _tag;
		return true;
	}

	private bool SetBehaviourEyes(int _behaviour)
	{
		switch (_behaviour)
		{
		case 1:
			chaFemale.ChangeLookEyesPtn(1);
			break;
		case 2:
			chaFemale.ChangeLookEyesPtn(2);
			break;
		case 3:
			chaFemale.ChangeLookEyesPtn(1);
			break;
		default:
			chaFemale.ChangeLookEyesPtn(0);
			break;
		}
		if (_behaviour == 3)
		{
			chaFemale.eyeLookCtrl.enabled = false;
		}
		else
		{
			chaFemale.eyeLookCtrl.enabled = true;
		}
		return true;
	}

	private bool SetBehaviourNeck(int _behaviour)
	{
		if (!chaFemale.neckLookCtrl.enabled && _behaviour != 3)
		{
			chaFemale.neckLookCtrl.neckLookScript.UpdateCall(0);
		}
		switch (_behaviour)
		{
		case 1:
			chaFemale.ChangeLookNeckPtn(1);
			break;
		case 2:
			chaFemale.ChangeLookNeckPtn(2);
			break;
		case 3:
			chaFemale.ChangeLookNeckPtn(1);
			break;
		default:
			chaFemale.ChangeLookNeckPtn(3);
			break;
		}
		if (_behaviour == 3)
		{
			chaFemale.neckLookCtrl.enabled = false;
		}
		else
		{
			chaFemale.neckLookCtrl.enabled = true;
		}
		return true;
	}

	private void NeckCalc(Vector3 targetNeckRot, Vector3 targetHeadRot)
	{
		float deltaTime = Time.deltaTime;
		ChangeTimeNeck = Mathf.Clamp(ChangeTimeNeck + deltaTime, 0f, chaFemale.neckLookCtrl.neckLookScript.changeTypeLeapTime);
		float num = Mathf.InverseLerp(0f, chaFemale.neckLookCtrl.neckLookScript.changeTypeLeapTime, ChangeTimeNeck);
		if (chaFemale.neckLookCtrl.neckLookScript.changeTypeLerpCurve != null)
		{
			num = chaFemale.neckLookCtrl.neckLookScript.changeTypeLerpCurve.Evaluate(num);
		}
		nowleapSpeed[0] = Mathf.Clamp01(chaFemale.neckLookCtrl.neckLookScript.neckTypeStates[3].leapSpeed * deltaTime);
		Quaternion b = Quaternion.Slerp(BackUpNeck, Quaternion.Euler(targetNeckRot), nowleapSpeed[0]);
		Quaternion b2 = Quaternion.Slerp(NeckTrs.localRotation, b, chaFemale.neckLookCtrl.neckLookScript.calcLerp);
		b2 = Quaternion.Slerp(BackUpNeck, b2, num);
		Quaternion quaternion = b2;
		NeckTrs.localRotation = quaternion;
		BackUpNeck = quaternion;
		b = Quaternion.Slerp(BackUpHead, Quaternion.Euler(targetHeadRot), nowleapSpeed[0]);
		b2 = Quaternion.Slerp(HeadTrs.localRotation, b, chaFemale.neckLookCtrl.neckLookScript.calcLerp);
		b2 = Quaternion.Slerp(BackUpHead, b2, num);
		quaternion = b2;
		HeadTrs.localRotation = quaternion;
		BackUpHead = quaternion;
		chaFemale.neckLookCtrl.neckLookScript.aBones[0].fixAngle = NeckTrs.localRotation;
		chaFemale.neckLookCtrl.neckLookScript.aBones[1].fixAngle = HeadTrs.localRotation;
	}

	private void EyeCalc(Vector3 targetEyeRot)
	{
		float deltaTime = Time.deltaTime;
		ChangeTimeEye = Mathf.Clamp(ChangeTimeEye + deltaTime, 0f, 1f);
		float t = Mathf.InverseLerp(0f, 1f, ChangeTimeEye);
		nowleapSpeed[1] = Mathf.Clamp01(chaFemale.eyeLookCtrl.eyeLookScript.eyeTypeStates[0].leapSpeed * deltaTime);
		Quaternion b = Quaternion.Slerp(BackUpEye[0], Quaternion.Euler(targetEyeRot), nowleapSpeed[1]);
		Quaternion b2 = Quaternion.Slerp(EyeTrs[0].localRotation, b, 1f);
		b2 = Quaternion.Slerp(BackUpEye[0], b2, t);
		ref Quaternion reference = ref BackUpEye[0];
		Quaternion quaternion = b2;
		EyeTrs[0].localRotation = quaternion;
		reference = quaternion;
		b = Quaternion.Slerp(BackUpEye[1], Quaternion.Euler(targetEyeRot), nowleapSpeed[1]);
		b2 = Quaternion.Slerp(EyeTrs[1].localRotation, b, 1f);
		b2 = Quaternion.Slerp(BackUpEye[1], b2, t);
		ref Quaternion reference2 = ref BackUpEye[1];
		quaternion = b2;
		EyeTrs[1].localRotation = quaternion;
		reference2 = quaternion;
		chaFemale.eyeLookCtrl.eyeLookScript.eyeObjs[0].angleH = EyeTrs[1].localRotation.eulerAngles.y;
		chaFemale.eyeLookCtrl.eyeLookScript.eyeObjs[0].angleV = EyeTrs[1].localRotation.eulerAngles.x;
		chaFemale.eyeLookCtrl.eyeLookScript.eyeObjs[1].angleH = EyeTrs[1].localRotation.eulerAngles.y;
		chaFemale.eyeLookCtrl.eyeLookScript.eyeObjs[1].angleV = EyeTrs[1].localRotation.eulerAngles.x;
	}
}
