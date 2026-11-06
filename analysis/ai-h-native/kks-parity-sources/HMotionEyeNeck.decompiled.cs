using System;
using System.Collections.Generic;
using Illusion.CustomAttributes;
using IllusionUtility.GetUtility;
using Sirenix.OdinInspector;
using UnityEngine;

public class HMotionEyeNeck : SerializedMonoBehaviour
{
	[Serializable]
	public class EyeNeckRange
	{
		[Label("上上限")]
		public float up = -60f;

		[Label("下上限")]
		public float down = 60f;

		[Label("左上限")]
		public float left = -60f;

		[Label("右上限")]
		public float right = 60f;
	}

	[Serializable]
	public class EyeNeckPtnInfo
	{
		[Label("ID")]
		public int id;

		[Label("挙動")]
		public int behaviour;

		[Label("首タゲ")]
		public int targetNeck;

		[Label("目タゲ")]
		public int targetEye;

		[Label("首向き幅")]
		public float rateNeck;

		[Label("目向き幅")]
		public float rateEye;
	}

	[SerializeField]
	protected Dictionary<int, EyeNeckPtnInfo> DicInfo = new Dictionary<int, EyeNeckPtnInfo>();

	protected static Dictionary<int, EyeNeckPtnInfo> dicInfo = new Dictionary<int, EyeNeckPtnInfo>();

	protected ChaControl chara;

	protected ChaControl partner;

	[Label("フラグ類")]
	public HFlag flags;

	[Label("相手顔オブジェクト名")]
	public string namePartnerHead = "";

	[Label("相手性器オブジェクト名")]
	public string namePartnerGenital = "";

	[Label("自分性器オブジェクト名")]
	public string nameGenital = "";

	[Label("相手女顔オブジェクト名")]
	public string namePartnerHead1 = "";

	[Label("相手女性器オブジェクト名")]
	public string namePartnerGenital1 = "";

	[DisabledGroup("男顔オブジェクト")]
	[SerializeField]
	protected GameObject objPartnerHead;

	[DisabledGroup("男性器オブジェクト")]
	[SerializeField]
	protected GameObject objPartnerKokan;

	[DisabledGroup("自分股間オブジェクト")]
	[SerializeField]
	protected GameObject objKokan;

	[DisabledGroup("女顔オブジェクト")]
	[SerializeField]
	protected GameObject objPartner1Head;

	[DisabledGroup("女性器オブジェクト")]
	[SerializeField]
	protected GameObject objPartner1Kokan;

	[DisabledGroup("相手体メッシュオブジェクト")]
	[SerializeField]
	protected GameObject objPartnerBody;

	[DisabledGroup("相手単色体メッシュオブジェクト")]
	[SerializeField]
	protected GameObject objPartnerSilhouetteBody;

	public static bool LoadEyeNeckPtn()
	{
		dicInfo = new Dictionary<int, EyeNeckPtnInfo>();
		string text = "eyeneckbase";
		foreach (string item in HFlag.lstABName)
		{
			if (!AssetBundleCheck.IsFile(item, text) || (!AssetBundleCheck.IsSimulation && !HFlag.BundleCheck(item, text)))
			{
				continue;
			}
			EyeNeckBaseData eyeNeckBaseData = CommonLib.LoadAsset<EyeNeckBaseData>(item, text, clone: false, "", check: false);
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
			if (eyeNeckBaseData == null)
			{
				continue;
			}
			foreach (EyeNeckBaseData.Param item2 in eyeNeckBaseData.param)
			{
				if (!dicInfo.ContainsKey(item2.id))
				{
					dicInfo.Add(item2.id, new EyeNeckPtnInfo());
				}
				EyeNeckPtnInfo eyeNeckPtnInfo = dicInfo[item2.id];
				eyeNeckPtnInfo.id = item2.id;
				eyeNeckPtnInfo.behaviour = item2.behaviour;
				eyeNeckPtnInfo.targetNeck = item2.targetNeck;
				eyeNeckPtnInfo.targetEye = item2.targetEye;
				eyeNeckPtnInfo.rateNeck = item2.rateNeck;
				eyeNeckPtnInfo.rateEye = item2.rateEye;
			}
		}
		return true;
	}

	public static void EndProc()
	{
		dicInfo.Clear();
		dicInfo = null;
	}

	public bool SetPartner(GameObject _objPartnerBone)
	{
		objPartnerHead = null;
		objPartnerKokan = null;
		if ((bool)_objPartnerBone)
		{
			Dictionary<string, GameObject> dictionary = ObjFind(_objPartnerBone);
			if (namePartnerHead != "" && dictionary.ContainsKey(namePartnerHead))
			{
				objPartnerHead = dictionary[namePartnerHead];
			}
			if (namePartnerGenital != "" && dictionary.ContainsKey(namePartnerGenital))
			{
				objPartnerKokan = dictionary[namePartnerGenital];
			}
		}
		return true;
	}

	public bool SetFemalePartner(GameObject _objPartnerBone)
	{
		objPartner1Head = null;
		objPartner1Kokan = null;
		if ((bool)_objPartnerBone)
		{
			ObjFind(_objPartnerBone);
			if (namePartnerHead1 != "")
			{
				objPartner1Head = _objPartnerBone.transform.FindLoop(namePartnerHead1);
			}
			if (namePartnerGenital1 != "")
			{
				objPartner1Kokan = _objPartnerBone.transform.FindLoop(namePartnerGenital1);
			}
		}
		return true;
	}

	private Dictionary<string, GameObject> ObjFind(GameObject root)
	{
		Dictionary<string, GameObject> dictionary = new Dictionary<string, GameObject>();
		Transform[] componentsInChildren = root.GetComponentsInChildren<Transform>();
		foreach (Transform transform in componentsInChildren)
		{
			if (!dictionary.ContainsKey(transform.name))
			{
				dictionary.Add(transform.name, transform.gameObject);
			}
		}
		return dictionary;
	}

	protected bool SetEyeNeckPtn(int _id, GameObject _objCamera, bool _isConfigEyeDisregard, bool _isConfigNeckDisregard)
	{
		if (_id >= 1000)
		{
			switch (_id)
			{
			case 1000:
				chara.ChangeLookEyesPtn(5);
				SetEyesTarget(0, 0.5f, _objCamera, _isConfigDisregard: true);
				break;
			case 1001:
				chara.ChangeLookNeckPtn(5);
				SetNeckTarget(0, 0.5f, _objCamera, _isConfigDisregard: true);
				break;
			case 1002:
				chara.ChangeLookEyesPtn(5);
				chara.ChangeLookNeckPtn(5);
				SetNeckTarget(0, 0.5f, _objCamera, _isConfigDisregard: true);
				SetEyesTarget(0, 0.5f, _objCamera, _isConfigDisregard: true);
				break;
			}
			return true;
		}
		if (!dicInfo.ContainsKey(_id))
		{
			return false;
		}
		EyeNeckPtnInfo eyeNeckPtnInfo = dicInfo[_id];
		SetBehaviour(eyeNeckPtnInfo.behaviour);
		SetNeckTarget(eyeNeckPtnInfo.targetNeck, eyeNeckPtnInfo.rateNeck, _objCamera, _isConfigNeckDisregard);
		SetEyesTarget(eyeNeckPtnInfo.targetEye, eyeNeckPtnInfo.rateNeck, _objCamera, _isConfigEyeDisregard);
		return true;
	}

	private bool SetBehaviour(int _behaviour)
	{
		switch (_behaviour)
		{
		case 0:
			chara.ChangeLookEyesPtn(4);
			chara.ChangeLookNeckPtn(3);
			break;
		case 1:
			chara.ChangeLookEyesPtn(5);
			chara.ChangeLookNeckPtn(3);
			break;
		case 2:
			chara.ChangeLookEyesPtn(5);
			chara.ChangeLookNeckPtn(5);
			break;
		case 3:
			chara.ChangeLookEyesPtn(7);
			chara.ChangeLookNeckPtn(3);
			break;
		case 4:
			chara.ChangeLookEyesPtn(7);
			chara.ChangeLookNeckPtn(6);
			break;
		case 5:
			chara.ChangeLookEyesPtn(7);
			chara.ChangeLookNeckPtn(5);
			break;
		case 6:
			chara.ChangeLookEyesPtn(5);
			chara.ChangeLookNeckPtn(6);
			break;
		case 7:
			chara.ChangeLookEyesPtn(4);
			chara.ChangeLookNeckPtn(5);
			break;
		case 8:
			chara.ChangeLookEyesPtn(4);
			chara.ChangeLookNeckPtn(6);
			break;
		case 9:
			chara.ChangeLookEyesPtn(5);
			chara.ChangeLookNeckPtn(0);
			break;
		case 10:
			chara.ChangeLookEyesPtn(7);
			chara.ChangeLookNeckPtn(0);
			break;
		case 11:
			chara.ChangeLookEyesPtn(4);
			chara.ChangeLookNeckPtn(0);
			break;
		default:
			chara.ChangeLookEyesPtn(4);
			chara.ChangeLookNeckPtn(3);
			break;
		}
		return true;
	}

	private bool SetNeckTarget(int _tag, float _rate, GameObject _objCamera, bool _isConfigDisregard)
	{
		switch (_tag)
		{
		case 0:
			chara.ChangeLookNeckTarget(0, _objCamera ? _objCamera.transform : null);
			break;
		case 1:
			chara.ChangeLookNeckTarget(0, (flags.mode != HFlag.EMode.aibu) ? (objPartnerHead ? objPartnerHead.transform : (_objCamera ? _objCamera.transform : null)) : (_objCamera ? _objCamera.transform : null));
			break;
		case 2:
			chara.ChangeLookNeckTarget(0, (flags.mode != HFlag.EMode.aibu) ? (objPartnerKokan ? objPartnerKokan.transform : (_objCamera ? _objCamera.transform : null)) : (_objCamera ? _objCamera.transform : null));
			break;
		case 3:
			chara.ChangeLookNeckTarget(0, objKokan ? objKokan.transform : (_objCamera ? _objCamera.transform : null));
			break;
		case 4:
			chara.ChangeLookNeckTarget(1, null, _rate);
			break;
		case 5:
			chara.ChangeLookNeckTarget(2, null, _rate);
			break;
		case 6:
			chara.ChangeLookNeckTarget(3, null, _rate);
			break;
		case 7:
			chara.ChangeLookNeckTarget(4, null, _rate);
			break;
		case 8:
			chara.ChangeLookNeckTarget(5, null, _rate);
			break;
		case 9:
			chara.ChangeLookNeckTarget(6, null, _rate);
			break;
		case 10:
			chara.ChangeLookNeckTarget(7, null, _rate);
			break;
		case 11:
			chara.ChangeLookNeckTarget(8, null, _rate);
			break;
		case 12:
			chara.ChangeLookNeckTarget(0, (flags.mode != HFlag.EMode.aibu) ? (objPartner1Head ? objPartner1Head.transform : (_objCamera ? _objCamera.transform : null)) : (_objCamera ? _objCamera.transform : null));
			break;
		case 13:
			chara.ChangeLookNeckTarget(0, (flags.mode != HFlag.EMode.aibu) ? (objPartner1Kokan ? objPartner1Kokan.transform : (_objCamera ? _objCamera.transform : null)) : (_objCamera ? _objCamera.transform : null));
			break;
		default:
			chara.ChangeLookNeckTarget(0, _objCamera ? _objCamera.transform : null);
			break;
		}
		return true;
	}

	private bool SetEyesTarget(int _tag, float _rate, GameObject _objCamera, bool _isConfigDisregard)
	{
		switch (_tag)
		{
		case 0:
			chara.ChangeLookEyesTarget(0, _objCamera ? _objCamera.transform : null);
			break;
		case 1:
			if (flags.mode == HFlag.EMode.aibu)
			{
				chara.ChangeLookEyesTarget(0, _objCamera ? _objCamera.transform : null);
			}
			else
			{
				chara.ChangeLookEyesTarget(0, objPartnerHead ? objPartnerHead.transform : (_objCamera ? _objCamera.transform : null));
			}
			break;
		case 2:
			if (flags.mode == HFlag.EMode.aibu)
			{
				chara.ChangeLookEyesTarget(0, _objCamera ? _objCamera.transform : null);
			}
			else
			{
				chara.ChangeLookEyesTarget(0, objPartnerKokan ? objPartnerKokan.transform : (_objCamera ? _objCamera.transform : null));
			}
			break;
		case 3:
			chara.ChangeLookEyesTarget(0, objKokan ? objKokan.transform : (_objCamera ? _objCamera.transform : null));
			break;
		case 4:
			chara.ChangeLookEyesTarget(1, null, _rate);
			break;
		case 5:
			chara.ChangeLookEyesTarget(2, null, _rate);
			break;
		case 6:
			chara.ChangeLookEyesTarget(3, null, _rate);
			break;
		case 7:
			chara.ChangeLookEyesTarget(4, null, _rate);
			break;
		case 8:
			chara.ChangeLookEyesTarget(5, null, _rate);
			break;
		case 9:
			chara.ChangeLookEyesTarget(6, null, _rate);
			break;
		case 10:
			chara.ChangeLookEyesTarget(7, null, _rate);
			break;
		case 11:
			chara.ChangeLookEyesTarget(8, null, _rate);
			break;
		case 12:
			chara.ChangeLookEyesTarget(0, objPartner1Head ? objPartner1Head.transform : (_objCamera ? _objCamera.transform : null));
			break;
		case 13:
			chara.ChangeLookEyesTarget(0, objPartner1Kokan ? objPartner1Kokan.transform : (_objCamera ? _objCamera.transform : null));
			break;
		default:
			chara.ChangeLookEyesTarget(0, _objCamera ? _objCamera.transform : null);
			break;
		}
		return true;
	}
}
