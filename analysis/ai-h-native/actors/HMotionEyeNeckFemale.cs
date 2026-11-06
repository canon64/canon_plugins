using System;
using System.Collections.Generic;
using AIChara;
using AIProject;
using Manager;
using UnityEngine;

public class HMotionEyeNeckFemale : MonoBehaviour
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

		public bool ExistFaceInfoIya;

		public EyeNeckFace[] faceinfo;

		public void Init()
		{
			anim = string.Empty;
			isConfigDisregardNeck = false;
			isConfigDisregardEye = false;
			ExistFaceInfoIya = false;
			faceinfo = new EyeNeckFace[2];
			faceinfo[0].Init();
			faceinfo[1].Init();
		}
	}

	[Serializable]
	public struct EyeNeckFace
	{
		[Label("セリフ無視(首)")]
		public bool isDisregardVoiceNeck;

		[Label("セリフ無視(目)")]
		public bool isDisregardVoiceEye;

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
			isDisregardVoiceNeck = false;
			isDisregardVoiceEye = false;
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

	[Label("相手男顔オブジェクト名")]
	public string strMaleHead = string.Empty;

	[Label("相手男性器オブジェクト名")]
	public string strMaleGenital = string.Empty;

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

	[DisabledGroup("男1顔オブジェクト")]
	[SerializeField]
	private GameObject objMale1Head;

	[DisabledGroup("男1性器オブジェクト")]
	[SerializeField]
	private GameObject objMale1Genital;

	[DisabledGroup("男2顔オブジェクト")]
	[SerializeField]
	private GameObject objMale2Head;

	[DisabledGroup("男2性器オブジェクト")]
	[SerializeField]
	private GameObject objMale2Genital;

	[DisabledGroup("女相手顔オブジェクト")]
	[SerializeField]
	private GameObject objFemale1Head;

	[DisabledGroup("女相手性器オブジェクト")]
	[SerializeField]
	private GameObject objFemale1Genital;

	private Transform LoopParent;

	private EyeNeck en;

	private EyeNeckFace enFace;

	private int ID;

	private AnimatorStateInfo ai;

	private Transform NeckTrs;

	private Transform HeadTrs;

	private Transform[] EyeTrs;

	private Quaternion BackUpNeck;

	private Quaternion BackUpHead;

	private Quaternion[] BackUpEye;

	private HVoiceCtrl.FaceInfo faceInfo;

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
				if (element.IsNullOrEmpty())
				{
					continue;
				}
				info.anim = element;
				info.isConfigDisregardNeck = row.GetElement(num2++) == "1";
				info.isConfigDisregardEye = row.GetElement(num2++) == "1";
				element = row.GetElement(num2++);
				info.faceinfo[0].isDisregardVoiceNeck = !element.IsNullOrEmpty() && element == "1";
				element = row.GetElement(num2++);
				info.faceinfo[0].isDisregardVoiceEye = !element.IsNullOrEmpty() && element == "1";
				info.faceinfo[0].Neckbehaviour = int.Parse(row.GetElement(num2++));
				info.faceinfo[0].Eyebehaviour = int.Parse(row.GetElement(num2++));
				info.faceinfo[0].targetNeck = int.Parse(row.GetElement(num2++));
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
				info.faceinfo[0].NeckRot[0] = zero;
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
				info.faceinfo[0].NeckRot[1] = zero;
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
				info.faceinfo[0].HeadRot[0] = zero;
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
				info.faceinfo[0].HeadRot[1] = zero;
				info.faceinfo[0].targetEye = int.Parse(row.GetElement(num2++));
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
				info.faceinfo[0].EyeRot[0] = zero;
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
				info.faceinfo[0].EyeRot[1] = zero;
				if (num2 >= row.Count)
				{
					lstEyeNeck.Add(info);
					continue;
				}
				element = row.GetElement(num2++);
				if (element.IsNullOrEmpty())
				{
					lstEyeNeck.Add(info);
					continue;
				}
				info.ExistFaceInfoIya = true;
				info.faceinfo[1].isDisregardVoiceNeck = !element.IsNullOrEmpty() && element == "1";
				element = row.GetElement(num2++);
				info.faceinfo[1].isDisregardVoiceEye = !element.IsNullOrEmpty() && element == "1";
				info.faceinfo[1].Neckbehaviour = int.Parse(row.GetElement(num2++));
				info.faceinfo[1].Eyebehaviour = int.Parse(row.GetElement(num2++));
				info.faceinfo[1].targetNeck = int.Parse(row.GetElement(num2++));
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
				info.faceinfo[1].NeckRot[0] = zero;
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
				info.faceinfo[1].NeckRot[1] = zero;
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
				info.faceinfo[1].HeadRot[0] = zero;
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
				info.faceinfo[1].HeadRot[1] = zero;
				info.faceinfo[1].targetEye = int.Parse(row.GetElement(num2++));
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
				info.faceinfo[1].EyeRot[0] = zero;
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
				info.faceinfo[1].EyeRot[1] = zero;
				lstEyeNeck.Add(info);
			}
		}
		return true;
	}

	public bool SetPartner(GameObject _objMale1Bone, GameObject _objMale2Bone, GameObject _objFemale1Bone)
	{
		objMale1Head = null;
		objMale2Head = null;
		objFemale1Head = null;
		objMale1Genital = null;
		objMale2Genital = null;
		objFemale1Genital = null;
		if ((bool)_objMale1Bone)
		{
			LoopParent = _objMale1Bone.transform;
			if (strMaleHead != string.Empty)
			{
				objMale1Head = GetObjectName(LoopParent, strMaleHead);
			}
			if (strMaleGenital != string.Empty)
			{
				objMale1Genital = GetObjectName(LoopParent, strMaleGenital);
			}
		}
		if ((bool)_objMale2Bone)
		{
			LoopParent = _objMale2Bone.transform;
			if (strMaleHead != string.Empty)
			{
				objMale2Head = GetObjectName(LoopParent, strMaleHead);
			}
			if (strMaleGenital != string.Empty)
			{
				objMale2Genital = GetObjectName(LoopParent, strMaleGenital);
			}
		}
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

	public bool Proc(AnimatorStateInfo _ai, HVoiceCtrl.FaceInfo _faceVoice, int _main)
	{
		bool flag = ((_main != 0) ? Config.HData.EyeDir1 : Config.HData.EyeDir0);
		bool flag2 = ((_main != 0) ? Config.HData.NeckDir1 : Config.HData.NeckDir0);
		ai = _ai;
		faceInfo = _faceVoice;
		for (int i = 0; i < lstEyeNeck.Count; i++)
		{
			if (_ai.IsName(lstEyeNeck[i].anim))
			{
				en = lstEyeNeck[i];
				if (en.ExistFaceInfoIya)
				{
					enFace = en.faceinfo[Singleton<HSceneManager>.Instance.isForce ? 1 : 0];
				}
				else
				{
					enFace = en.faceinfo[0];
				}
			}
		}
		if (hscene.ctrlVoice.nowVoices[ID].state == HVoiceCtrl.VoiceKind.startVoice || hscene.ctrlVoice.nowVoices[ID].state == HVoiceCtrl.VoiceKind.voice)
		{
			if (!enFace.isDisregardVoiceEye)
			{
				bFaceInfo[0] = true;
				SetEyesTarget((!flag) ? (_faceVoice?.targetEyeLine ?? 0) : 0);
				SetBehaviourEyes(flag ? 1 : (_faceVoice?.behaviorEyeLine ?? 0));
			}
			else
			{
				SetEyesTarget(en.isConfigDisregardEye ? enFace.targetEye : ((!flag) ? enFace.targetEye : 0));
				SetBehaviourEyes(en.isConfigDisregardEye ? enFace.Eyebehaviour : (flag ? 1 : enFace.Eyebehaviour));
			}
			if (!enFace.isDisregardVoiceNeck)
			{
				bFaceInfo[1] = true;
				SetNeckTarget((!flag2) ? (_faceVoice?.targetNeckLine ?? 0) : 0);
				SetBehaviourNeck(flag2 ? 1 : (_faceVoice?.behaviorNeckLine ?? 0));
			}
			else
			{
				SetNeckTarget(en.isConfigDisregardNeck ? enFace.targetNeck : ((!flag2) ? enFace.targetNeck : 0));
				SetBehaviourNeck(en.isConfigDisregardNeck ? enFace.Neckbehaviour : (flag2 ? 1 : enFace.Neckbehaviour));
			}
		}
		else
		{
			SetNeckTarget(en.isConfigDisregardNeck ? enFace.targetNeck : ((!flag2) ? enFace.targetNeck : 0));
			SetEyesTarget(en.isConfigDisregardEye ? enFace.targetEye : ((!flag) ? enFace.targetEye : 0));
			SetBehaviourEyes(en.isConfigDisregardEye ? enFace.Eyebehaviour : (flag ? 1 : enFace.Eyebehaviour));
			SetBehaviourNeck(en.isConfigDisregardNeck ? enFace.Neckbehaviour : (flag2 ? 1 : enFace.Neckbehaviour));
			bFaceInfo[0] = false;
			bFaceInfo[1] = false;
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
			}
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
		bool flag = ((ID != 0) ? Config.HData.NeckDir1 : Config.HData.NeckDir0);
		bool flag2 = ((ID != 0) ? Config.HData.EyeDir1 : Config.HData.EyeDir0);
		if (!bFaceInfo[0] || !bFaceInfo[1])
		{
			for (int i = 0; i < lstEyeNeck.Count; i++)
			{
				if (!ai.IsName(lstEyeNeck[i].anim))
				{
					continue;
				}
				en = lstEyeNeck[i];
				if (en.ExistFaceInfoIya)
				{
					enFace = en.faceinfo[Singleton<HSceneManager>.Instance.isForce ? 1 : 0];
				}
				else
				{
					enFace = en.faceinfo[0];
				}
				if (!flag)
				{
					if (!bFaceInfo[1])
					{
						if (enFace.targetNeck == 7)
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
					}
					else if (faceInfo != null && faceInfo.targetNeckLine == 7)
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
						NeckCalc(faceInfo.NeckRot[nYuragiType], faceInfo.HeadRot[nYuragiType]);
					}
				}
				if (flag2)
				{
					break;
				}
				if (!bFaceInfo[0])
				{
					if (enFace.targetEye != 7)
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
				}
				else
				{
					if (faceInfo == null || faceInfo.targetEyeLine != 7)
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
					EyeCalc(faceInfo.EyeRot[nYuragiType]);
				}
				break;
			}
		}
		else
		{
			if (faceInfo == null)
			{
				return;
			}
			if (!flag && faceInfo.targetNeckLine == 7)
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
				NeckCalc(faceInfo.NeckRot[nYuragiType], faceInfo.HeadRot[nYuragiType]);
			}
			if (flag2 || faceInfo.targetEyeLine != 7)
			{
				return;
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
			EyeCalc(faceInfo.EyeRot[nYuragiType]);
		}
	}

	private bool SetEyesTarget(int _tag)
	{
		switch (_tag)
		{
		case 1:
			chaFemale.ChangeLookEyesTarget(1, (!objMale1Head) ? null : objMale1Head.transform);
			break;
		case 2:
			chaFemale.ChangeLookEyesTarget(1, (!objMale1Genital) ? null : objMale1Genital.transform);
			break;
		case 3:
			chaFemale.ChangeLookEyesTarget(1, (!objMale2Head) ? null : objMale2Head.transform);
			break;
		case 4:
			chaFemale.ChangeLookEyesTarget(1, (!objMale2Genital) ? null : objMale2Genital.transform);
			break;
		case 5:
			chaFemale.ChangeLookEyesTarget(1, (!objFemale1Head) ? null : objFemale1Head.transform);
			break;
		case 6:
			chaFemale.ChangeLookEyesTarget(1, (!objFemale1Genital) ? null : objFemale1Genital.transform);
			break;
		case 8:
			chaFemale.ChangeLookEyesTarget(1, (!objGenitalSelf) ? null : objGenitalSelf.transform);
			break;
		default:
			chaFemale.ChangeLookEyesTarget(0);
			break;
		case 7:
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
			chaFemale.ChangeLookNeckTarget(1, (!objMale1Head) ? null : objMale1Head.transform);
			break;
		case 2:
			chaFemale.ChangeLookNeckTarget(1, (!objMale1Genital) ? null : objMale1Genital.transform);
			break;
		case 3:
			chaFemale.ChangeLookNeckTarget(1, (!objMale2Head) ? null : objMale2Head.transform);
			break;
		case 4:
			chaFemale.ChangeLookNeckTarget(1, (!objMale2Genital) ? null : objMale2Genital.transform);
			break;
		case 5:
			chaFemale.ChangeLookNeckTarget(1, (!objFemale1Head) ? null : objFemale1Head.transform);
			break;
		case 6:
			chaFemale.ChangeLookNeckTarget(1, (!objFemale1Genital) ? null : objFemale1Genital.transform);
			break;
		case 8:
			chaFemale.ChangeLookNeckTarget(1, (!objGenitalSelf) ? null : objGenitalSelf.transform);
			break;
		default:
			chaFemale.ChangeLookNeckTarget(0);
			break;
		case 7:
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
		float y = EyeTrs[1].localRotation.eulerAngles.y;
		float x = EyeTrs[1].localRotation.eulerAngles.x;
		y = ((!(y > 180f)) ? y : (y - 360f));
		x = ((!(x > 180f)) ? x : (x - 360f));
		chaFemale.eyeLookCtrl.eyeLookScript.eyeObjs[0].angleH = y;
		chaFemale.eyeLookCtrl.eyeLookScript.eyeObjs[0].angleV = x;
		chaFemale.eyeLookCtrl.eyeLookScript.eyeObjs[1].angleH = y;
		chaFemale.eyeLookCtrl.eyeLookScript.eyeObjs[1].angleV = x;
	}
}
