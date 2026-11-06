using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChaLoad;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using FBSAssist;
using Illusion.Extensions;
using Illusion.Game.Array;
using IllusionUtility.GetUtility;
using IllusionUtility.SetUtility;
using Localize.Translate;
using Manager;
using Obi;
using UnityEngine;

public class ChaControl : ChaInfo
{
	public class MannequinBackInfo
	{
		public int coordinateType;

		public byte[] custom;

		public int eyesPtn;

		public int mouthPtn;

		public float mouthOpen;

		public bool mouthFixed;

		public int neckLook;

		public byte[] eyesInfo;

		public bool mannequin;

		public void Backup(ChaControl chaCtrl)
		{
			coordinateType = chaCtrl.fileStatus.coordinateType;
			custom = chaCtrl.chaFile.GetCustomBytes();
			eyesPtn = chaCtrl.fileStatus.eyesPtn;
			mouthPtn = chaCtrl.fileStatus.mouthPtn;
			mouthOpen = chaCtrl.fileStatus.mouthOpenMax;
			mouthFixed = chaCtrl.fileStatus.mouthFixed;
			neckLook = chaCtrl.fileStatus.neckLookPtn;
		}

		public void Restore(ChaControl chaCtrl)
		{
			chaCtrl.chaFile.SetCustomBytes(custom, ChaFileDefine.ChaFileCustomVersion);
			chaCtrl.Reload(noChangeClothes: true);
			chaCtrl.ChangeEyesPtn(eyesPtn, blend: false);
			chaCtrl.ChangeMouthPtn(mouthPtn, blend: false);
			chaCtrl.fileStatus.mouthFixed = mouthFixed;
			chaCtrl.ChangeMouthOpenMax(mouthOpen);
			chaCtrl.ChangeLookNeckPtn(neckLook);
			chaCtrl.neckLookCtrl.ForceLateUpdate();
			chaCtrl.eyeLookCtrl.ForceLateUpdate();
			chaCtrl.neckLookCtrl.neckLookScript.skipCalc = false;
			chaCtrl.resetDynamicBoneAll = true;
			chaCtrl.LateUpdateForce();
		}
	}

	public enum BustNormalKind
	{
		NmlBody,
		NmlTop,
		NmlBra,
		NmlTopPA
	}

	public enum FaceTexKind
	{
		inpBase,
		inpSub,
		inpPaint01,
		inpPaint02,
		inpCheek,
		inpLipLine,
		inpMole
	}

	public enum BodyTexKind
	{
		inpBase,
		inpSub,
		inpPaint01,
		inpPaint02,
		inpSunburn,
		inpNail
	}

	private MannequinBackInfo mannequinBackInfo = new MannequinBackInfo();

	private const string deleteHeadBoneName = "cf_J_N_FaceRoot";

	private const string deleteBodyBoneName = "cf_j_root";

	private readonly Bounds bounds = new Bounds(new Vector3(0f, -0.2f, 0f), new Vector3(2f, 2f, 2f));

	private AssignedAnotherWeights aaWeightsHead;

	private AssignedAnotherWeights aaWeightsBody;

	private Texture texBodyAlphaMask { get; set; }

	private Texture texBraAlphaMask { get; set; }

	public Texture texInnerAlphaMask { get; set; }

	public bool hideShortsBot { get; set; }

	public Dictionary<BustNormalKind, BustNormal> dictBustNormal { get; private set; }

	private byte[] siruNewLv { get; set; }

	public bool enableExpression
	{
		get
		{
			if (!(null == base.expression))
			{
				return base.expression.enable;
			}
			return false;
		}
		set
		{
			if (null != base.expression)
			{
				base.expression.enable = value;
			}
		}
	}

	public ChaFileCoordinate nowCoordinate { get; private set; }

	public bool notBra { get; private set; }

	public bool notBot { get; private set; }

	public bool notShorts { get; private set; }

	public bool[] hideHairAcs { get; private set; }

	public bool isChangeOfClothesRandom { get; private set; }

	public Dictionary<int, Dictionary<byte, string>> dictStateType { get; private set; }

	private static byte[,] RandomChangeState { get; } = new byte[6, 9]
	{
		{ 0, 0, 0, 0, 0, 0, 0, 0, 0 },
		{ 3, 3, 3, 3, 3, 3, 3, 3, 3 },
		{ 3, 3, 0, 0, 3, 3, 3, 3, 3 },
		{ 3, 0, 0, 0, 0, 0, 0, 3, 3 },
		{ 3, 0, 3, 0, 0, 0, 0, 3, 3 },
		{ 3, 3, 3, 0, 3, 0, 0, 3, 3 }
	};

	private int ShapeFaceNum { get; set; }

	private int ShapeBodyNum { get; set; }

	private ShapeInfoBase sibFace { get; set; }

	public ShapeInfoBase sibBody { get; set; }

	private bool changeShapeBodyMask { get; set; }

	public BustSoft bustSoft { get; private set; }

	public BustGravity bustGravity { get; private set; }

	public bool[] updateCMFaceTex { get; private set; }

	public bool[] updateCMFaceColor { get; private set; }

	public bool[] updateCMFaceLayout { get; private set; }

	public bool[] updateCMBodyTex { get; private set; }

	public bool[] updateCMBodyColor { get; private set; }

	public bool[] updateCMBodyLayout { get; private set; }

	public AudioSource asVoice { get; private set; }

	private AudioAssist fbsaaVoice { get; set; }

	private Texture texHairGloss { get; set; }

	public byte tearsLv
	{
		get
		{
			return base.fileStatus.tearsLv;
		}
		set
		{
			base.fileStatus.tearsLv = value;
		}
	}

	public ShapeInfoBase sibHand { get; set; }

	private bool updateAlphaMask { get; set; }

	public void Initialize(byte _sex, int _exType, bool _hiPoly, GameObject _objRoot, int _id, int _no, ChaFileControl _chaFile = null)
	{
		if (_chaFile != null)
		{
			_ = _chaFile.parameter.sex;
			_ = _chaFile.parameter.exType;
		}
		MemberInitializeAll();
		InitializeControlLoadAll();
		InitializeControlFaceAll();
		InitializeControlBodyAll();
		InitializeControlCoordinateAll();
		InitializeControlCustomAll();
		InitializeControlHandAll();
		base.objRoot = _objRoot;
		base.chaID = _id;
		base.loadNo = _no;
		base.hiPoly = _hiPoly;
		base.hideMoz = true;
		if (_chaFile == null)
		{
			base.chaFile = new ChaFileControl();
			LoadPreset(_sex, _exType);
		}
		else
		{
			base.chaFile = _chaFile;
		}
		base.chaFile.parameter.sex = _sex;
		base.chaFile.parameter.exType = ((base.chaFile.parameter.sex == 0) ? _exType : 0);
		InitBaseCustomTextureBody();
		InitBaseCustomTextureEtc();
		if (_chaFile == null)
		{
			ChangeCoordinateType(ChaFileDefine.CoordinateType.Plain);
		}
		else
		{
			ChangeCoordinateType(changeBackCoordinateType: false);
		}
		if (_sex == 0)
		{
			base.chaFile.custom.body.shapeValueBody[0] = ((base.exType == 0) ? 0.6f : 1f);
		}
		if (1 == _sex)
		{
			base.chaFile.status.visibleSonAlways = false;
		}
		else
		{
			base.chaFile.status.visibleSon = false;
		}
	}

	public void ReleaseAll()
	{
		ReleaseControlLoadAll();
		ReleaseControlFaceAll();
		ReleaseControlBodyAll();
		ReleaseControlCoordinateAll();
		ReleaseControlCustomAll();
		ReleaseControlHandAll();
		ReleaseInfoAll();
	}

	public void ReleaseObject()
	{
		ReleaseControlLoadObject();
		ReleaseControlFaceObject();
		ReleaseControlBodyObject();
		ReleaseControlCoordinateObject();
		ReleaseControlCustomObject();
		ReleaseControlHandObject();
		ReleaseInfoObject();
		if (Character.enableCharaLoadGCClear)
		{
			Resources.UnloadUnusedAssets();
			GC.Collect();
		}
	}

	public void LoadPreset(int _sex, int _exType, string presetName = "")
	{
		string text = "";
		text = ((base.sex == 0 && 1 == _exType) ? $"ill_Default_Male_Ex" : ((!presetName.IsNullOrEmpty()) ? presetName : ((_sex == 0) ? "ill_Default_Male" : "ill_Default_Female")));
		ListInfoBase listInfoBase = base.lstCtrl.FindInfo((_exType != 0) ? ChaListDefine.CategoryNo.cha_sample_m_ex : ((_sex == 0) ? ChaListDefine.CategoryNo.cha_sample_m : ChaListDefine.CategoryNo.cha_sample_f), ChaListDefine.KeyType.MainData, text);
		if (listInfoBase != null)
		{
			string path = listInfoBase.GetInfo(ChaListDefine.KeyType.MainAB);
			Localize.Translate.Manager.DefaultData.GetBundlePath(ref path);
			base.chaFile.LoadFromAssetBundle(path, text);
		}
	}

	public void LoadPresetDemo(int type)
	{
		string assetBundleName = "custom/presets_demo.unity3d";
		string assetName = "ill_Demo_Shop01";
		base.chaFile.LoadFromAssetBundle(assetBundleName, assetName);
	}

	public static ChaFileControl[] GetRandomFemaleCard(int num)
	{
		FolderAssist folderAssist = new FolderAssist();
		string[] searchPattern = new string[1] { "*.png" };
		string folder = UserData.Path + "chara/female/";
		folderAssist.CreateFolderInfoEx(folder, searchPattern);
		List<string> list = (from n in folderAssist.lstFile.Shuffle()
			select n.FullPath).ToList();
		int num2 = Mathf.Min(list.Count, num);
		if (num2 == 0)
		{
			return null;
		}
		List<ChaFileControl> list2 = new List<ChaFileControl>();
		for (int num3 = 0; num3 < num2; num3++)
		{
			ChaFileControl chaFileControl = new ChaFileControl();
			if (chaFileControl.LoadCharaFile(list[num3], 1, noLoadPng: true) && chaFileControl.parameter.sex != 0)
			{
				list2.Add(chaFileControl);
			}
		}
		return list2.ToArray();
	}

	public void SetActiveTop(bool active)
	{
		if ((bool)base.objTop)
		{
			base.objTop.SetActiveIfDifferent(active);
		}
	}

	public bool GetActiveTop()
	{
		if ((bool)base.objTop)
		{
			return base.objTop.activeSelf;
		}
		return false;
	}

	public void SetPosition(float x, float y, float z)
	{
		if (null != base.objTop)
		{
			base.objTop.transform.localPosition = new Vector3(x, y, z);
		}
	}

	public void SetPosition(Vector3 pos)
	{
		if (null != base.objTop)
		{
			base.objTop.transform.localPosition = pos;
		}
	}

	public Vector3 GetPosition()
	{
		if (!(null == base.objTop))
		{
			return base.objTop.transform.localPosition;
		}
		return Vector3.zero;
	}

	public void SetRotation(float x, float y, float z)
	{
		if (null != base.objTop)
		{
			base.objTop.transform.localRotation = Quaternion.Euler(x, y, z);
		}
	}

	public void SetRotation(Vector3 rot)
	{
		if (null != base.objTop)
		{
			base.objTop.transform.localRotation = Quaternion.Euler(rot);
		}
	}

	public Vector3 GetRotation()
	{
		if (!(null == base.objTop))
		{
			return base.objTop.transform.localRotation.eulerAngles;
		}
		return Vector3.zero;
	}

	public void SetTransform(Transform trf)
	{
		if (null != base.objTop)
		{
			base.objTop.transform.localPosition = trf.localPosition;
			base.objTop.transform.localRotation = trf.localRotation;
			base.objTop.transform.localScale = trf.localScale;
		}
	}

	public void ChangeSettingMannequin(bool mannequin)
	{
		if (mannequin)
		{
			if (!mannequinBackInfo.mannequin)
			{
				mannequinBackInfo.mannequin = true;
				mannequinBackInfo.Backup(this);
				string assetBundleName = "chara/oo_base.unity3d";
				string assetName = "mannequin";
				base.chaFile.LoadFileLimited(assetBundleName, assetName, face: true, body: true, hair: true, parameter: false, coordinate: false);
				Reload(noChangeClothes: true);
				ChangeEyesPtn(21, blend: false);
				ChangeMouthPtn(0, blend: false);
				base.fileStatus.mouthFixed = true;
				ChangeMouthOpenMax(0f);
				ChangeLookNeckPtn(3);
				base.neckLookCtrl.neckLookScript.skipCalc = true;
				base.neckLookCtrl.ForceLateUpdate();
				base.eyeLookCtrl.ForceLateUpdate();
				base.resetDynamicBoneAll = true;
				LateUpdateForce();
				GameObject referenceInfo = GetReferenceInfo(RefObjKey.ObjEyebrow);
				if ((bool)referenceInfo)
				{
					referenceInfo.SetActiveIfDifferent(active: false);
				}
				GameObject referenceInfo2 = GetReferenceInfo(RefObjKey.ObjNoseline);
				if ((bool)referenceInfo2)
				{
					referenceInfo2.SetActiveIfDifferent(active: false);
				}
				GameObject referenceInfo3 = GetReferenceInfo(RefObjKey.N_FaceSpecial);
				if ((bool)referenceInfo3)
				{
					referenceInfo3.SetActiveIfDifferent(active: false);
				}
			}
		}
		else if (mannequinBackInfo.mannequin)
		{
			mannequinBackInfo.Restore(this);
			GameObject referenceInfo4 = GetReferenceInfo(RefObjKey.ObjEyebrow);
			if ((bool)referenceInfo4)
			{
				referenceInfo4.SetActiveIfDifferent(active: true);
			}
			GameObject referenceInfo5 = GetReferenceInfo(RefObjKey.ObjNoseline);
			if ((bool)referenceInfo5)
			{
				referenceInfo5.SetActiveIfDifferent(active: true);
			}
			GameObject referenceInfo6 = GetReferenceInfo(RefObjKey.N_FaceSpecial);
			if ((bool)referenceInfo6)
			{
				referenceInfo6.SetActiveIfDifferent(active: true);
			}
			mannequinBackInfo.mannequin = false;
		}
	}

	public void RestoreMannequinHair()
	{
		ChaFileControl chaFileControl = new ChaFileControl();
		chaFileControl.SetCustomBytes(mannequinBackInfo.custom, ChaFileDefine.ChaFileCustomVersion);
		base.fileCustom.hair = chaFileControl.custom.hair;
		Reload(noChangeClothes: true, noChangeHead: true, noChangeHair: false, noChangeBody: true);
	}

	public void OnDestroy()
	{
		Character.DeleteChara(this, entryOnly: true);
		if (!base.skipDestroy)
		{
			ReleaseAll();
		}
	}

	public void UpdateForce()
	{
		if (base.loadEnd)
		{
			UpdateBlendShapeVoice();
			if (base.hiPoly)
			{
				UpdateSiru();
			}
		}
	}

	public void LateUpdateForce()
	{
		if (!base.loadEnd)
		{
			return;
		}
		UpdateVisible();
		if (base.resetDynamicBoneAll)
		{
			ResetDynamicBoneAll();
			base.resetDynamicBoneAll = false;
		}
		if (base.updateShapeBody)
		{
			UpdateShapeBody();
			base.updateShapeBody = false;
		}
		if (base.updateShapeFace)
		{
			UpdateShapeFace();
			base.updateShapeFace = false;
		}
		UpdateAlwaysShapeBody();
		UpdateAlwaysShapeHand();
		GameObject referenceInfo = GetReferenceInfo(RefObjKey.S_ANA);
		GameObject referenceInfo2 = GetReferenceInfo(RefObjKey.a_n_ana);
		if (null != referenceInfo && null != referenceInfo2)
		{
			Vector3 localScale = referenceInfo.transform.localScale;
			referenceInfo2.transform.localScale = new Vector3(1f / localScale.x, 1f / localScale.y, 1f / localScale.z);
		}
		if (base.hiPoly && 1 == base.sex)
		{
			float x = (Character.enableCorrectArmSize ? 0.92f : 1f);
			float y = (Character.enableCorrectHandSize ? 0.9f : 1f);
			float z = (Character.enableCorrectHandSize ? 0.9f : 1f);
			float x2 = 1f;
			if (Character.enableCorrectHandTwoShotSize)
			{
				x2 = Character.correctHandTwoShotSize.x;
				y = Character.correctHandTwoShotSize.y;
				z = Character.correctHandTwoShotSize.z;
			}
			GameObject referenceInfo3 = GetReferenceInfo(RefObjKey.CORRECT_ARM_L);
			if (referenceInfo3 != null)
			{
				referenceInfo3.transform.localScale = new Vector3(x, 1f, 1f);
			}
			GameObject referenceInfo4 = GetReferenceInfo(RefObjKey.CORRECT_ARM_R);
			if (referenceInfo4 != null)
			{
				referenceInfo4.transform.localScale = new Vector3(x, 1f, 1f);
			}
			GameObject referenceInfo5 = GetReferenceInfo(RefObjKey.CORRECT_HAND_L);
			if (referenceInfo5 != null)
			{
				referenceInfo5.transform.localScale = new Vector3(x2, y, z);
			}
			GameObject referenceInfo6 = GetReferenceInfo(RefObjKey.CORRECT_HAND_R);
			if (referenceInfo6 != null)
			{
				referenceInfo6.transform.localScale = new Vector3(x2, y, z);
			}
		}
		if (base.reSetupDynamicBoneBust)
		{
			ReSetupDynamicBoneBust();
			UpdateBustSoftnessAndGravity();
			base.reSetupDynamicBoneBust = false;
		}
		if (base.updateBustSize)
		{
			int num = 4;
			float rate = 1f;
			if (0.5f > base.chaFile.custom.body.shapeValueBody[num])
			{
				rate = Mathf.InverseLerp(0f, 0.5f, base.chaFile.custom.body.shapeValueBody[num]);
			}
			foreach (KeyValuePair<BustNormalKind, BustNormal> item in dictBustNormal)
			{
				item.Value.Blend(rate);
			}
			base.updateBustSize = false;
		}
		if (!base.hiPoly)
		{
			CalcNip(RefObjKey.a_n_nip_L);
			CalcNip(RefObjKey.a_n_nip_R);
		}
		Transform referenceTransform = GetReferenceTransform(RefObjKey.CORRECT_TONGUE_TOP);
		if (referenceTransform != null)
		{
			referenceTransform.localScale = Vector3.one;
		}
		if (200 == base.fileFace.headId || 201 == base.fileFace.headId)
		{
			if (21 == base.fileStatus.mouthPtn)
			{
				Transform referenceTransform2 = GetReferenceTransform(RefObjKey.CORRECT_TONGUE_TOP);
				if (referenceTransform2 != null)
				{
					Vector3 localPosition = referenceTransform2.localPosition;
					referenceTransform2.localPosition = new Vector3(localPosition.x, localPosition.y + 0.007f, localPosition.z);
					referenceTransform2.localScale = new Vector3(0.92f, 1f, 1.05f);
				}
			}
			else if (22 == base.fileStatus.mouthPtn)
			{
				Transform referenceTransform3 = GetReferenceTransform(RefObjKey.CORRECT_TONGUE_TOP);
				if (referenceTransform3 != null)
				{
					Vector3 localPosition2 = referenceTransform3.localPosition;
					referenceTransform3.localPosition = new Vector3(localPosition2.x, localPosition2.y + 0.007f, localPosition2.z);
					referenceTransform3.localScale = new Vector3(0.92f, 1f, 1.05f);
				}
				Transform referenceTransform4 = GetReferenceTransform(RefObjKey.CORRECT_MOUTH_TARGET);
				if (referenceTransform4 != null)
				{
					Vector3 localPosition3 = referenceTransform4.localPosition;
					referenceTransform4.localPosition = new Vector3(localPosition3.x, localPosition3.y + 0.007f, localPosition3.z);
				}
				Transform referenceTransform5 = GetReferenceTransform(RefObjKey.CORRECT_MOUTH_TARGET02);
				if (referenceTransform5 != null)
				{
					Vector3 localPosition4 = referenceTransform5.localPosition;
					referenceTransform5.localPosition = new Vector3(localPosition4.x, localPosition4.y + 0.007f, localPosition4.z);
				}
			}
		}
		if (base.fileBody.typeBone == 1)
		{
			Transform referenceTransform6 = GetReferenceTransform(RefObjKey.CORRECT_TONGUE_TOP);
			if (referenceTransform6 != null && sibBody is ShapeBodyInfoFemale shapeBodyInfoFemale)
			{
				Vector3 vctScl = shapeBodyInfoFemale.correctValue[2].vctScl;
				vctScl += Vector3.one;
				referenceTransform6.localScale = new Vector3(1f / vctScl.x, 1f / vctScl.y, 1f / vctScl.z);
			}
		}
		SetForegroundEyesAndEyebrow();
		void CalcNip(RefObjKey key)
		{
			GameObject referenceInfo7 = GetReferenceInfo(key);
			if (!(null == referenceInfo7))
			{
				Vector3 lossyScale = referenceInfo7.transform.parent.lossyScale;
				Vector3 one = Vector3.one;
				if (lossyScale.x != 0f)
				{
					one.x = 1f / lossyScale.x;
				}
				if (lossyScale.y != 0f)
				{
					one.y = 1f / lossyScale.y;
				}
				if (lossyScale.z != 0f)
				{
					one.z = 1f / lossyScale.z;
				}
				referenceInfo7.transform.localScale = one;
			}
		}
	}

	public RuntimeAnimatorController LoadAnimation(string assetBundleName, string assetName, string manifestName = "")
	{
		if (null == base.animBody)
		{
			return null;
		}
		RuntimeAnimatorController runtimeAnimatorController = CommonLib.LoadAsset<RuntimeAnimatorController>(assetBundleName, assetName, clone: false, manifestName);
		if (null == runtimeAnimatorController)
		{
			return null;
		}
		base.animBody.runtimeAnimatorController = runtimeAnimatorController;
		AssetBundleManager.UnloadAssetBundle(assetBundleName, isUnloadForceRefCount: true);
		return runtimeAnimatorController;
	}

	public void AnimPlay(string stateName)
	{
		if (!(null == base.animBody))
		{
			base.animBody.Play(stateName);
		}
	}

	public AnimatorStateInfo getAnimatorStateInfo(int _nLayer)
	{
		if (null == base.animBody || null == base.animBody.runtimeAnimatorController)
		{
			return default(AnimatorStateInfo);
		}
		return base.animBody.GetCurrentAnimatorStateInfo(_nLayer);
	}

	public bool syncPlay(AnimatorStateInfo _syncState, int _nLayer)
	{
		if (null == base.animBody)
		{
			return false;
		}
		base.animBody.Play(_syncState.shortNameHash, _nLayer, _syncState.normalizedTime);
		return true;
	}

	public bool syncPlay(int _nameHash, int _nLayer, float _fnormalizedTime)
	{
		if (null == base.animBody)
		{
			return false;
		}
		base.animBody.Play(_nameHash, _nLayer, _fnormalizedTime);
		return true;
	}

	public bool syncPlay(string _strameHash, int _nLayer, float _fnormalizedTime)
	{
		if (null == base.animBody)
		{
			return false;
		}
		base.animBody.Play(_strameHash, _nLayer, _fnormalizedTime);
		return true;
	}

	public bool setLayerWeight(float _fWeight, int _nLayer)
	{
		if (null == base.animBody)
		{
			return false;
		}
		base.animBody.SetLayerWeight(_nLayer, _fWeight);
		return true;
	}

	public bool setAllLayerWeight(float _fWeight)
	{
		if (null == base.animBody)
		{
			return false;
		}
		for (int i = 1; i < base.animBody.layerCount; i++)
		{
			base.animBody.SetLayerWeight(i, _fWeight);
		}
		return true;
	}

	public float getLayerWeight(int _nLayer)
	{
		if (null == base.animBody)
		{
			return 0f;
		}
		return base.animBody.GetLayerWeight(_nLayer);
	}

	public bool setPlay(string _strAnmName, int _nLayer)
	{
		if (null == base.animBody)
		{
			return false;
		}
		base.animBody.Play(_strAnmName, _nLayer);
		return true;
	}

	public void setAnimatorParamTrigger(string _strAnmName)
	{
		if (!(null == base.animBody))
		{
			base.animBody.SetTrigger(_strAnmName);
		}
	}

	public void setAnimatorParamResetTrigger(string _strAnmName)
	{
		if (!(null == base.animBody))
		{
			base.animBody.ResetTrigger(_strAnmName);
		}
	}

	public void setAnimatorParamBool(string _strAnmName, bool _bFlag)
	{
		if (!(null == base.animBody))
		{
			base.animBody.SetBool(_strAnmName, _bFlag);
		}
	}

	public bool getAnimatorParamBool(string _strAnmName)
	{
		if (null == base.animBody)
		{
			return false;
		}
		return base.animBody.GetBool(_strAnmName);
	}

	public void setAnimatorParamFloat(string _strAnmName, float _fValue)
	{
		if (base.animBody != null)
		{
			base.animBody.SetFloat(_strAnmName, _fValue);
		}
	}

	public void setAnimatorParamFloat(int _strAnmNameID, float _fValue)
	{
		if (base.animBody != null)
		{
			base.animBody.SetFloat(_strAnmNameID, _fValue);
		}
	}

	public void setAnimPtnCrossFade(string _strAnmName, float _fBlendTime, int _nLayer, float _fCrossStateTime)
	{
		if (!(null == base.animBody))
		{
			base.animBody.CrossFade(_strAnmName, _fBlendTime, _nLayer, _fCrossStateTime);
		}
	}

	public bool isBlend(int _nLayer)
	{
		if (null == base.animBody)
		{
			return false;
		}
		return base.animBody.IsInTransition(_nLayer);
	}

	public bool IsNextHash(int _nLayer, string _nameHash)
	{
		if (null == base.animBody)
		{
			return false;
		}
		return base.animBody.GetNextAnimatorStateInfo(_nLayer).IsName(_nameHash);
	}

	protected void InitializeControlBodyAll()
	{
		siruNewLv = new byte[Enum.GetNames(typeof(ChaFileDefine.SiruParts)).Length];
		for (int i = 0; i < siruNewLv.Length; i++)
		{
			siruNewLv[i] = 0;
		}
		InitializeControlBodyObject();
	}

	protected void InitializeControlBodyObject()
	{
		dictBustNormal = new Dictionary<BustNormalKind, BustNormal>();
		texBodyAlphaMask = null;
		texBraAlphaMask = null;
		texInnerAlphaMask = null;
		hideShortsBot = false;
	}

	protected void ReleaseControlBodyAll()
	{
		ReleaseControlBodyObject(init: false);
	}

	protected void ReleaseControlBodyObject(bool init = true)
	{
		if (dictBustNormal != null)
		{
			foreach (KeyValuePair<BustNormalKind, BustNormal> item in dictBustNormal)
			{
				item.Value.Release();
			}
			dictBustNormal.Clear();
		}
		for (int i = 0; i < siruNewLv.Length; i++)
		{
			siruNewLv[i] = 0;
		}
		if (Character.enableCharaLoadGCClear)
		{
			Resources.UnloadUnusedAssets();
		}
		if (init)
		{
			InitializeControlBodyObject();
		}
	}

	public int GetHeightCategory()
	{
		float shapeBodyValue = GetShapeBodyValue(0);
		if (0.25f > shapeBodyValue)
		{
			return 0;
		}
		if (0.6f < shapeBodyValue)
		{
			return 2;
		}
		return 1;
	}

	public int GetWaistCategory()
	{
		float num = (GetShapeBodyValue(ChaFileDefine.cf_CategoryWaist[0]) + GetShapeBodyValue(ChaFileDefine.cf_CategoryWaist[1]) + GetShapeBodyValue(ChaFileDefine.cf_CategoryWaist[2])) / 3f;
		if (0.35f > num)
		{
			return 0;
		}
		if (0.65f < num)
		{
			return 2;
		}
		return 1;
	}

	public int GetBustCategory()
	{
		float shapeBodyValue = GetShapeBodyValue(4);
		if (0.4f > shapeBodyValue)
		{
			return 0;
		}
		if (0.7f < shapeBodyValue)
		{
			return 2;
		}
		return 1;
	}

	public void ResetDynamicBoneAll()
	{
		DynamicBone[] componentsInChildren = base.objTop.GetComponentsInChildren<DynamicBone>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].ResetParticlesPosition();
		}
	}

	public DynamicBone_Ver02 getDynamicBoneBust(DynamicBoneKind _eArea)
	{
		DynamicBone_Ver02 value = null;
		base.dictDynamicBoneBust.TryGetValue(_eArea, out value);
		return value;
	}

	public bool InitDynamicBoneBust()
	{
		foreach (KeyValuePair<DynamicBoneKind, DynamicBone_Ver02> item in base.dictDynamicBoneBust)
		{
			if (null != item.Value)
			{
				item.Value.ResetParticlesPosition();
			}
		}
		return true;
	}

	public bool ReSetupDynamicBoneBust(int _nArea = 0)
	{
		DynamicBone_Ver02 value = null;
		if (_nArea == 0)
		{
			if (base.dictDynamicBoneBust.TryGetValue(DynamicBoneKind.BreastL, out value))
			{
				value.ResetPosition();
			}
			if (base.dictDynamicBoneBust.TryGetValue(DynamicBoneKind.BreastR, out value))
			{
				value.ResetPosition();
			}
		}
		else
		{
			if (base.dictDynamicBoneBust.TryGetValue(DynamicBoneKind.HipL, out value))
			{
				value.ResetPosition();
			}
			if (base.dictDynamicBoneBust.TryGetValue(DynamicBoneKind.HipR, out value))
			{
				value.ResetPosition();
			}
		}
		return true;
	}

	public bool playDynamicBoneBust(int _nArea, bool _bPlay)
	{
		DynamicBone_Ver02 value = null;
		if (_nArea == 0)
		{
			if (base.dictDynamicBoneBust.TryGetValue(DynamicBoneKind.BreastL, out value))
			{
				value.enabled = _bPlay;
			}
			if (base.dictDynamicBoneBust.TryGetValue(DynamicBoneKind.BreastR, out value))
			{
				value.enabled = _bPlay;
			}
		}
		else
		{
			if (base.dictDynamicBoneBust.TryGetValue(DynamicBoneKind.HipL, out value))
			{
				value.enabled = _bPlay;
			}
			if (base.dictDynamicBoneBust.TryGetValue(DynamicBoneKind.HipR, out value))
			{
				value.enabled = _bPlay;
			}
		}
		return true;
	}

	public bool playDynamicBoneBust(DynamicBoneKind _eArea, bool _bPlay)
	{
		DynamicBone_Ver02 value = null;
		if (base.dictDynamicBoneBust.TryGetValue(_eArea, out value))
		{
			value.enabled = _bPlay;
		}
		return true;
	}

	public bool ChangeNipRate(float rate)
	{
		base.chaFile.status.nipStandRate = rate;
		changeShapeBodyMask = true;
		base.updateShapeBody = true;
		return true;
	}

	public void ChangeHitBustBlendShapeValue(byte typeSex)
	{
		if (null != base.hitBodyCtrlCmp)
		{
			base.hitBodyCtrlCmp.ChangeBustBlendShapeValue((typeSex == 0) ? 90f : 0f);
		}
	}

	public void EnableExpressionIndex(int indexNo, bool enable)
	{
		if (null != base.expression)
		{
			base.expression.EnableIndex((ExpressionBone.ExpressionIndex)indexNo, enable);
		}
	}

	public void EnableExpressionCategory(int categoryNo, bool enable)
	{
		if (null != base.expression)
		{
			base.expression.EnableCategory((ExpressionBone.ExpressionCategory)categoryNo, enable);
		}
	}

	public void SetSiruFlags(ChaFileDefine.SiruParts parts, byte lv)
	{
		siruNewLv[(int)parts] = lv;
	}

	public byte GetSiruFlags(ChaFileDefine.SiruParts parts)
	{
		return base.chaFile.status.siruLv[(int)parts];
	}

	private bool UpdateSiru(bool forceChange = false)
	{
		if (!base.hiPoly)
		{
			return true;
		}
		if (base.customMatFace != null)
		{
			int num = 0;
			if (base.fileStatus.siruLv[num] != siruNewLv[num] || forceChange)
			{
				base.fileStatus.siruLv[num] = siruNewLv[num];
				base.customMatFace.SetFloat(ChaShader._liquidface, (int)base.fileStatus.siruLv[num]);
			}
		}
		if (base.customMatBody != null)
		{
			int[] bodySiruParts = ChaFileDefine.bodySiruParts;
			bool flag = false;
			for (int i = 0; i < bodySiruParts.Length; i++)
			{
				if (forceChange || base.fileStatus.siruLv[bodySiruParts[i]] != siruNewLv[bodySiruParts[i]])
				{
					flag = true;
				}
				base.fileStatus.siruLv[bodySiruParts[i]] = siruNewLv[bodySiruParts[i]];
			}
			if (flag)
			{
				float num2 = (int)base.fileStatus.siruLv[bodySiruParts[0]];
				float num3 = (int)base.fileStatus.siruLv[bodySiruParts[1]];
				float num4 = (int)base.fileStatus.siruLv[bodySiruParts[2]];
				float num5 = (int)base.fileStatus.siruLv[bodySiruParts[3]];
				base.customMatBody.SetFloat(ChaShader._liquidftop, num2);
				base.customMatBody.SetFloat(ChaShader._liquidfbot, num3);
				base.customMatBody.SetFloat(ChaShader._liquidbtop, num4);
				base.customMatBody.SetFloat(ChaShader._liquidbbot, num5);
				int[] clothesSiruParts = ChaFileDefine.clothesSiruParts;
				for (int j = 0; j < clothesSiruParts.Length; j++)
				{
					UpdateClothesSiru(clothesSiruParts[j], num2, num3, num4, num5);
				}
			}
		}
		return true;
	}

	public void ChangeAlphaMask(byte state0, byte state1)
	{
		if ((bool)base.customMatBody)
		{
			base.customMatBody.SetFloat(ChaShader._alpha_a, (int)state0);
			base.customMatBody.SetFloat(ChaShader._alpha_b, (int)state1);
		}
		if (base.rendBra != null)
		{
			for (int i = 0; i < 2; i++)
			{
				if (base.rendBra[i] != null)
				{
					Material material = base.rendBra[i].material;
					if (material != null)
					{
						material.SetFloat(ChaShader._alpha_a, (int)state0);
						material.SetFloat(ChaShader._alpha_b, (int)state1);
					}
				}
			}
		}
		if (base.rendInner == null)
		{
			return;
		}
		for (int j = 0; j < 2; j++)
		{
			if (base.rendInner[j] != null)
			{
				Material material2 = base.rendInner[j].material;
				if (material2 != null)
				{
					material2.SetFloat(ChaShader._alpha_a, (int)state0);
					material2.SetFloat(ChaShader._alpha_b, (int)state1);
				}
			}
		}
	}

	public void ChangeSimpleBodyDraw(bool drawSimple)
	{
		base.fileStatus.visibleSimple = drawSimple;
	}

	public void ChangeSimpleBodyColor(Color color)
	{
		base.fileStatus.simpleColor = color;
		if ((bool)base.rendSimpleBody)
		{
			Material material = base.rendSimpleBody.material;
			if ((bool)material)
			{
				material.SetColor(ChaShader._Color, color);
			}
		}
		if ((bool)base.rendSimpleTongue)
		{
			Material material2 = base.rendSimpleTongue.material;
			if ((bool)material2)
			{
				material2.SetColor(ChaShader._Color, color);
			}
		}
	}

	private void UpdateVisible()
	{
		bool value = true;
		bool flag = true;
		bool flag2 = false;
		if (Scene.NowSceneNames.Any((string s) => s == "H"))
		{
			value = base.sex != 0 || Manager.Config.HData.VisibleSon;
			flag = base.sex != 0 || Manager.Config.HData.VisibleBody;
			flag2 = base.sex == 0 && base.fileStatus.visibleSimple;
		}
		Bool8 @bool = default(Bool8);
		GameObject referenceInfo = GetReferenceInfo(RefObjKey.S_TongueB);
		@bool[0] = base.visibleAll;
		@bool[1] = ((1 == base.fileStatus.tongueState) ? true : false);
		@bool[2] = flag;
		@bool[3] = !flag2;
		@bool[4] = base.fileStatus.visibleHeadAlways;
		@bool[5] = base.fileStatus.visibleBodyAlways;
		if (YS_Assist.SetActiveControl(referenceInfo, @bool.Any(6)))
		{
			base.updateShape = true;
		}
		if (YS_Assist.SetActiveControl(GetReferenceInfo(RefObjKey.S_MOZ_ALL), !base.hideMoz))
		{
			base.updateShape = true;
		}
		GameObject referenceInfo2 = GetReferenceInfo(RefObjKey.ObjBody);
		@bool[0] = base.visibleAll;
		@bool[1] = flag;
		@bool[2] = !flag2;
		@bool[3] = base.fileStatus.visibleBodyAlways;
		if (YS_Assist.SetActiveControl(referenceInfo2, @bool.Any(4)))
		{
			base.updateShape = true;
		}
		bool flag3 = IsClothesStateKind(1) && base.fileStatus.clothesState[1] == 0;
		bool flag4 = IsClothesStateKind(3) && base.fileStatus.clothesState[3] == 0;
		bool value2 = flag2 || !(flag3 || flag4) || base.fileStatus.visibleSon;
		if (base.exType == 1)
		{
			value2 = !IsClothesStateKind(0) || base.fileStatus.clothesState[0] != 0 || base.fileStatus.visibleSon;
		}
		GameObject referenceInfo3 = GetReferenceInfo(RefObjKey.S_Son);
		@bool[0] = base.visibleAll;
		@bool[1] = value2;
		@bool[2] = value;
		@bool[3] = base.fileStatus.visibleSonAlways;
		if (YS_Assist.SetActiveControl(referenceInfo3, @bool.Any(4)))
		{
			base.updateShape = true;
		}
		if (YS_Assist.SetActiveControl(GetReferenceInfo(RefObjKey.S_GOMU), base.fileStatus.visibleGomu))
		{
			base.updateShape = true;
		}
		GameObject referenceInfo4 = GetReferenceInfo(RefObjKey.S_SimpleTop);
		@bool[0] = base.visibleAll;
		@bool[1] = flag;
		@bool[2] = base.fileStatus.visibleBodyAlways;
		@bool[3] = flag2;
		if (YS_Assist.SetActiveControl(referenceInfo4, @bool.Any(4)))
		{
			base.updateShape = true;
		}
		@bool[0] = base.visibleAll;
		@bool[1] = flag;
		@bool[2] = !flag2;
		@bool[3] = base.fileStatus.visibleHeadAlways;
		@bool[4] = base.fileStatus.visibleBodyAlways;
		if (YS_Assist.SetActiveControl(base.objHead, @bool.Any(5)))
		{
			base.updateShape = true;
		}
		if (YS_Assist.SetActiveControl(GetReferenceInfo(RefObjKey.S_TongueF), base.fileStatus.tongueState == 0))
		{
			base.updateShape = true;
		}
		GameObject referenceInfo5 = GetReferenceInfo(RefObjKey.S_TEARS_01);
		if (referenceInfo5 != null && referenceInfo5.SetActiveIfDifferent(ChaFileDefine.tearsFlags[base.fileStatus.tearsLv, 0]))
		{
			base.updateShape = true;
		}
		referenceInfo5 = GetReferenceInfo(RefObjKey.S_TEARS_02);
		if (referenceInfo5 != null && referenceInfo5.SetActiveIfDifferent(ChaFileDefine.tearsFlags[base.fileStatus.tearsLv, 1]))
		{
			base.updateShape = true;
		}
		referenceInfo5 = GetReferenceInfo(RefObjKey.S_TEARS_03);
		if (referenceInfo5 != null && referenceInfo5.SetActiveIfDifferent(ChaFileDefine.tearsFlags[base.fileStatus.tearsLv, 2]))
		{
			base.updateShape = true;
		}
		@bool[0] = base.visibleAll;
		@bool[1] = ((2 == base.fileStatus.tongueState) ? true : false);
		@bool[2] = flag;
		@bool[3] = !flag2;
		@bool[4] = base.fileStatus.visibleHeadAlways;
		@bool[5] = base.fileStatus.visibleBodyAlways;
		if (YS_Assist.SetActiveControl(base.objTongueEx, @bool.Any(6)))
		{
			base.updateShape = true;
		}
		bool flag5 = CheckHideHair();
		@bool[0] = base.visibleAll;
		@bool[1] = flag;
		@bool[2] = !flag5;
		@bool[3] = !flag2;
		@bool[4] = base.fileStatus.visibleHeadAlways;
		@bool[5] = base.fileStatus.visibleBodyAlways;
		GameObject[] array = base.objHair;
		for (int num = 0; num < array.Length; num++)
		{
			if (YS_Assist.SetActiveControl(array[num], @bool.Any(6)))
			{
				base.updateShape = true;
			}
		}
		@bool[0] = base.visibleAll;
		@bool[1] = flag;
		@bool[2] = !flag2;
		@bool[3] = 3 != base.fileStatus.clothesState[0];
		@bool[4] = base.fileStatus.visibleBodyAlways;
		if (YS_Assist.SetActiveControl(base.objClothes[0], @bool.Any(5)))
		{
			base.updateShape = true;
		}
		bool flag6 = false;
		bool flag7 = base.fileStatus.clothesState[0] == 0;
		if (YS_Assist.SetActiveControl(GetReferenceInfo(RefObjKey.S_CTOP_T_DEF), flag7))
		{
			base.updateShape = true;
			flag6 = true;
		}
		bool flag8 = 1 == base.fileStatus.clothesState[0];
		if (YS_Assist.SetActiveControl(GetReferenceInfo(RefObjKey.S_CTOP_T_NUGE), flag8))
		{
			base.updateShape = true;
			flag6 = true;
		}
		@bool[0] = base.fileStatus.clothesState[1] == 0;
		@bool[1] = 3 != base.fileStatus.clothesState[0];
		if (YS_Assist.SetActiveControl(GetReferenceInfo(RefObjKey.S_CTOP_B_DEF), @bool.Any(2)))
		{
			base.updateShape = true;
		}
		@bool[0] = base.fileStatus.clothesState[1] != 0;
		@bool[1] = 3 != base.fileStatus.clothesState[0];
		if (YS_Assist.SetActiveControl(GetReferenceInfo(RefObjKey.S_CTOP_B_NUGE), @bool.Any(2)))
		{
			base.updateShape = true;
		}
		DrawOption(ChaFileDefine.ClothesKind.top);
		DrawSode();
		int num2 = 0;
		ListInfoBase listInfoBase = base.infoClothes[0];
		if (listInfoBase != null)
		{
			num2 = listInfoBase.Kind;
		}
		for (int num3 = 0; num3 < ChaFileDefine.topPartsKindId.Length; num3++)
		{
			@bool[0] = base.visibleAll;
			@bool[1] = 3 != base.fileStatus.clothesState[0];
			@bool[2] = flag;
			@bool[3] = !flag2;
			@bool[4] = base.fileStatus.visibleBodyAlways;
			if (YS_Assist.SetActiveControl(base.objParts[ChaFileDefine.topPartsKindId[num3]], @bool.Any(5)))
			{
				base.updateShape = true;
			}
			if (YS_Assist.SetActiveControl(GetReferenceInfo(ChaFileDefine.topPartsKey[num3, 0]), base.fileStatus.clothesState[0] == 0))
			{
				base.updateShape = true;
				if (num3 == 0)
				{
					flag6 = true;
				}
				else if (1 == num3 && 2 == num2)
				{
					flag6 = true;
				}
			}
			if (YS_Assist.SetActiveControl(GetReferenceInfo(ChaFileDefine.topPartsKey[num3, 1]), 1 == base.fileStatus.clothesState[0]))
			{
				base.updateShape = true;
				if (num3 == 0)
				{
					flag6 = true;
				}
				else if (1 == num3 && 2 == num2)
				{
					flag6 = true;
				}
			}
		}
		if (flag6 || updateAlphaMask)
		{
			byte b = base.fileStatus.clothesState[0];
			ChangeAlphaMask(ChaFileDefine.alphaState[b, 0], ChaFileDefine.alphaState[b, 1]);
			updateAlphaMask = false;
		}
		@bool[0] = base.visibleAll;
		@bool[1] = !notBot;
		@bool[2] = flag;
		@bool[3] = !flag2;
		@bool[4] = 3 != base.fileStatus.clothesState[1];
		@bool[5] = base.fileStatus.visibleBodyAlways;
		if (YS_Assist.SetActiveControl(base.objClothes[1], @bool.Any(6)))
		{
			base.updateShape = true;
		}
		bool flag9 = base.fileStatus.clothesState[1] == 0;
		if (YS_Assist.SetActiveControl(GetReferenceInfo(RefObjKey.S_CBOT_B_DEF), flag9))
		{
			base.updateShape = true;
		}
		bool flag10 = 1 == base.fileStatus.clothesState[1];
		if (YS_Assist.SetActiveControl(GetReferenceInfo(RefObjKey.S_CBOT_B_NUGE), flag10))
		{
			base.updateShape = true;
		}
		@bool[0] = base.fileStatus.clothesState[0] == 0;
		@bool[1] = 2 != base.fileStatus.clothesState[1];
		if (YS_Assist.SetActiveControl(GetReferenceInfo(RefObjKey.S_CBOT_T_DEF), @bool.Any(2)))
		{
			base.updateShape = true;
		}
		@bool[0] = base.fileStatus.clothesState[0] != 0;
		@bool[1] = 2 != base.fileStatus.clothesState[1];
		if (YS_Assist.SetActiveControl(GetReferenceInfo(RefObjKey.S_CBOT_T_NUGE), @bool.Any(2)))
		{
			base.updateShape = true;
		}
		DrawOption(ChaFileDefine.ClothesKind.bot);
		@bool[0] = base.visibleAll;
		@bool[1] = flag;
		@bool[2] = !flag2;
		@bool[3] = !notBra;
		@bool[4] = 3 != base.fileStatus.clothesState[2];
		@bool[5] = base.fileStatus.visibleBodyAlways;
		if (YS_Assist.SetActiveControl(base.objClothes[2], @bool.Any(6)))
		{
			base.updateShape = true;
		}
		bool flag11 = base.fileStatus.clothesState[2] == 0;
		if (YS_Assist.SetActiveControl(GetReferenceInfo(RefObjKey.S_UWT_T_DEF), flag11))
		{
			base.updateShape = true;
		}
		bool flag12 = 1 == base.fileStatus.clothesState[2];
		if (YS_Assist.SetActiveControl(GetReferenceInfo(RefObjKey.S_UWT_T_NUGE), flag12))
		{
			base.updateShape = true;
		}
		@bool[0] = base.fileStatus.clothesState[3] == 0;
		@bool[1] = 3 != base.fileStatus.clothesState[2];
		if (YS_Assist.SetActiveControl(GetReferenceInfo(RefObjKey.S_UWT_B_DEF), @bool.Any(2)))
		{
			base.updateShape = true;
		}
		@bool[0] = base.fileStatus.clothesState[3] != 0;
		@bool[1] = 3 != base.fileStatus.clothesState[2];
		if (YS_Assist.SetActiveControl(GetReferenceInfo(RefObjKey.S_UWT_B_NUGE), @bool.Any(2)))
		{
			base.updateShape = true;
		}
		DrawOption(ChaFileDefine.ClothesKind.bra);
		if (base.objHideNip != null)
		{
			bool active = false;
			if (notBra && base.visibleAll && !flag2 && base.fileStatus.visibleBodyAlways && base.fileStatus.clothesState[0] != 0)
			{
				active = true;
			}
			base.objHideNip.SetActiveIfDifferent(active);
		}
		bool value3 = true;
		if (hideShortsBot && (bool)base.objClothes[1] && base.objClothes[1].activeSelf && base.fileStatus.clothesState[1] == 0)
		{
			value3 = false;
		}
		@bool[0] = base.visibleAll;
		@bool[1] = value3;
		@bool[2] = !notShorts;
		@bool[3] = flag;
		@bool[4] = !flag2;
		@bool[5] = 3 != base.fileStatus.clothesState[3];
		@bool[6] = base.fileStatus.visibleBodyAlways;
		if (YS_Assist.SetActiveControl(base.objClothes[3], @bool.Any(7)))
		{
			base.updateShape = true;
		}
		bool flag13 = base.fileStatus.clothesState[3] == 0;
		if (YS_Assist.SetActiveControl(GetReferenceInfo(RefObjKey.S_UWB_B_DEF), flag13))
		{
			base.updateShape = true;
		}
		bool flag14 = 1 == base.fileStatus.clothesState[3];
		if (YS_Assist.SetActiveControl(GetReferenceInfo(RefObjKey.S_UWB_B_NUGE), flag14))
		{
			base.updateShape = true;
		}
		bool flag15 = 2 == base.fileStatus.clothesState[3];
		if (YS_Assist.SetActiveControl(GetReferenceInfo(RefObjKey.S_UWB_B_NUGE2), flag15))
		{
			base.updateShape = true;
		}
		DrawOption(ChaFileDefine.ClothesKind.shorts);
		if (base.objHideKokan != null)
		{
			bool active2 = false;
			if (notShorts && notBra && base.visibleAll && !flag2 && base.fileStatus.visibleBodyAlways)
			{
				active2 = true;
			}
			base.objHideKokan.SetActiveIfDifferent(active2);
		}
		@bool[0] = base.visibleAll;
		@bool[1] = base.fileStatus.clothesState[4] == 0;
		@bool[2] = flag;
		@bool[3] = !flag2;
		@bool[4] = base.fileStatus.visibleBodyAlways;
		if (YS_Assist.SetActiveControl(base.objClothes[4], @bool.Any(5)))
		{
			base.updateShape = true;
		}
		DrawOption(ChaFileDefine.ClothesKind.gloves);
		byte b2 = base.fileStatus.clothesState[5];
		@bool[0] = base.visibleAll;
		@bool[1] = 3 != b2 && 2 != b2;
		@bool[2] = flag;
		@bool[3] = !flag2;
		@bool[4] = base.fileStatus.visibleBodyAlways;
		if (YS_Assist.SetActiveControl(base.objClothes[5], @bool.Any(5)))
		{
			base.updateShape = true;
		}
		if (YS_Assist.SetActiveControl(GetReferenceInfo(RefObjKey.S_PANST_DEF), base.fileStatus.clothesState[5] == 0))
		{
			base.updateShape = true;
		}
		if (YS_Assist.SetActiveControl(GetReferenceInfo(RefObjKey.S_PANST_NUGE), 1 == base.fileStatus.clothesState[5]))
		{
			base.updateShape = true;
		}
		DrawOption(ChaFileDefine.ClothesKind.panst);
		@bool[0] = base.visibleAll;
		@bool[1] = base.fileStatus.clothesState[6] == 0;
		@bool[2] = flag;
		@bool[3] = !flag2;
		@bool[4] = base.fileStatus.visibleBodyAlways;
		if (YS_Assist.SetActiveControl(base.objClothes[6], @bool.Any(5)))
		{
			base.updateShape = true;
		}
		DrawOption(ChaFileDefine.ClothesKind.socks);
		@bool[0] = base.visibleAll;
		@bool[1] = base.fileStatus.shoesType == 0;
		@bool[2] = base.fileStatus.clothesState[7] == 0;
		@bool[3] = flag;
		@bool[4] = !flag2;
		@bool[5] = base.fileStatus.visibleBodyAlways;
		if (YS_Assist.SetActiveControl(base.objClothes[7], @bool.Any(6)))
		{
			base.updateShape = true;
		}
		DrawOption(ChaFileDefine.ClothesKind.shoes_inner);
		@bool[0] = base.visibleAll;
		@bool[1] = 1 == base.fileStatus.shoesType;
		@bool[2] = base.fileStatus.clothesState[8] == 0;
		@bool[3] = flag;
		@bool[4] = !flag2;
		@bool[5] = base.fileStatus.visibleBodyAlways;
		if (YS_Assist.SetActiveControl(base.objClothes[8], @bool.Any(6)))
		{
			base.updateShape = true;
		}
		DrawOption(ChaFileDefine.ClothesKind.shoes_outer);
		for (int num4 = 0; num4 < base.objAccessory.Length; num4++)
		{
			bool flag16 = false;
			if (!base.fileStatus.visibleHeadAlways && nowCoordinate.accessory.parts[num4].partsOfHead)
			{
				flag16 = true;
			}
			if (!base.fileStatus.visibleBodyAlways || !flag)
			{
				flag16 = true;
			}
			@bool[0] = base.visibleAll;
			@bool[1] = base.fileStatus.showAccessory[num4];
			@bool[2] = !flag2;
			@bool[3] = !flag16;
			if (YS_Assist.SetActiveControl(base.objAccessory[num4], @bool.Any(4)))
			{
				base.updateShape = true;
			}
		}
		GameObject referenceInfo6 = GetReferenceInfo(RefObjKey.DB_SKIRT_BOT);
		GameObject referenceInfo7 = GetReferenceInfo(RefObjKey.DB_SKIRT_TOP);
		GameObject referenceInfo8 = GetReferenceInfo(RefObjKey.DB_SKIRT_TOPA);
		GameObject referenceInfo9 = GetReferenceInfo(RefObjKey.DB_SKIRT_TOPB);
		bool active3 = !referenceInfo6;
		if (referenceInfo6 != null && 3 == base.fileStatus.clothesState[1])
		{
			active3 = true;
		}
		if (referenceInfo7 != null)
		{
			referenceInfo7.SetActiveIfDifferent(active3);
		}
		if (referenceInfo8 != null)
		{
			referenceInfo8.SetActiveIfDifferent(active3);
		}
		if (referenceInfo9 != null)
		{
			referenceInfo9.SetActiveIfDifferent(active3);
		}
	}

	public void DrawOption(ChaFileDefine.ClothesKind kind)
	{
		ChaClothesComponent chaClothesComponent = base.cusClothesCmp[(int)kind];
		if (null == chaClothesComponent)
		{
			return;
		}
		bool flag = !nowCoordinate.clothes.parts[(int)kind].hideOpt[0];
		bool flag2 = !nowCoordinate.clothes.parts[(int)kind].hideOpt[1];
		if (ChaFileDefine.ClothesKind.bra == kind)
		{
			flag = !nowCoordinate.clothes.hideBraOpt[0];
			flag2 = !nowCoordinate.clothes.hideBraOpt[1];
		}
		if (ChaFileDefine.ClothesKind.shorts == kind)
		{
			flag = !nowCoordinate.clothes.hideShortsOpt[0];
			flag2 = !nowCoordinate.clothes.hideShortsOpt[1];
		}
		if (chaClothesComponent.objOpt01 != null)
		{
			GameObject[] objOpt = chaClothesComponent.objOpt01;
			for (int i = 0; i < objOpt.Length; i++)
			{
				YS_Assist.SetActiveControl(objOpt[i], flag);
			}
		}
		if (chaClothesComponent.objOpt02 != null)
		{
			GameObject[] objOpt = chaClothesComponent.objOpt02;
			for (int i = 0; i < objOpt.Length; i++)
			{
				YS_Assist.SetActiveControl(objOpt[i], flag2);
			}
		}
	}

	public void DrawSode()
	{
		int num = 0;
		ChaClothesComponent chaClothesComponent = base.cusClothesCmp[num];
		int sleevesType = nowCoordinate.clothes.parts[num].sleevesType;
		if (null == chaClothesComponent)
		{
			return;
		}
		if (chaClothesComponent.objSleeves01 != null)
		{
			GameObject[] objSleeves = chaClothesComponent.objSleeves01;
			for (int i = 0; i < objSleeves.Length; i++)
			{
				YS_Assist.SetActiveControl(objSleeves[i], sleevesType == 0);
			}
		}
		if (chaClothesComponent.objSleeves02 != null)
		{
			GameObject[] objSleeves = chaClothesComponent.objSleeves02;
			for (int i = 0; i < objSleeves.Length; i++)
			{
				YS_Assist.SetActiveControl(objSleeves[i], 1 == sleevesType);
			}
		}
		if (chaClothesComponent.objSleeves03 != null)
		{
			GameObject[] objSleeves = chaClothesComponent.objSleeves03;
			for (int i = 0; i < objSleeves.Length; i++)
			{
				YS_Assist.SetActiveControl(objSleeves[i], 2 == sleevesType);
			}
		}
	}

	protected void InitializeControlCoordinateAll()
	{
		nowCoordinate = new ChaFileCoordinate();
		InitializeControlCoordinateObject();
	}

	protected void InitializeControlCoordinateObject()
	{
		notBra = false;
		notBot = false;
		notShorts = false;
		hideHairAcs = new bool[20];
		isChangeOfClothesRandom = false;
		dictStateType = new Dictionary<int, Dictionary<byte, string>>();
	}

	protected void ReleaseControlCoordinateAll()
	{
		ReleaseControlCoordinateObject(init: false);
	}

	protected void ReleaseControlCoordinateObject(bool init = true)
	{
		if (init)
		{
			InitializeControlCoordinateObject();
		}
	}

	public int GetNowClothesType()
	{
		int num = 0;
		int num2 = 1;
		int num3 = 2;
		int num4 = 3;
		int num5 = (IsClothesStateKind(num) ? base.fileStatus.clothesState[num] : 3);
		int num6 = (IsClothesStateKind(num2) ? base.fileStatus.clothesState[num2] : 3);
		int num7 = (IsClothesStateKind(num3) ? base.fileStatus.clothesState[num3] : 3);
		int num8 = (IsClothesStateKind(num4) ? base.fileStatus.clothesState[num4] : 3);
		bool flag = true;
		bool flag2 = true;
		if (base.infoClothes[num3] != null)
		{
			flag = ((1 == base.infoClothes[num3].Kind) ? true : false);
		}
		if (base.infoClothes[num4] != null)
		{
			flag2 = ((1 == base.infoClothes[num4].Kind) ? true : false);
		}
		if (notShorts)
		{
			num8 = num7;
			flag2 = flag;
		}
		if (num5 == 0)
		{
			if (num6 == 0)
			{
				return 0;
			}
			if (num8 == 0)
			{
				if (!flag2)
				{
					return 2;
				}
				return 1;
			}
			return 3;
		}
		if (num6 == 0)
		{
			if (num7 == 0)
			{
				if (!flag)
				{
					return 2;
				}
				return 1;
			}
			return 3;
		}
		if (num7 == 0)
		{
			if (num8 == 0)
			{
				if (flag)
				{
					return 1;
				}
				if (!flag2)
				{
					return 2;
				}
				return 1;
			}
			return 3;
		}
		return 3;
	}

	public bool IsKokanHide()
	{
		bool result = false;
		int[] array = new int[4] { 0, 1, 2, 3 };
		int[] array2 = new int[4] { 1, 1, 3, 3 };
		for (int i = 0; i < array.Length; i++)
		{
			int num = array[i];
			if (IsClothes(num) && ((i != 0 && 2 != i) || !("2" != base.infoClothes[num].GetInfo(ChaListDefine.KeyType.Coordinate))) && "1" == base.infoClothes[num].GetInfo(ChaListDefine.KeyType.KokanHide) && base.fileStatus.clothesState[array2[i]] == 0)
			{
				result = true;
				break;
			}
		}
		return result;
	}

	public bool AssignCoordinate(ChaFileDefine.CoordinateType type, string path)
	{
		ChaFileCoordinate chaFileCoordinate = new ChaFileCoordinate();
		string path2 = ChaFileControl.ConvertCoordinateFilePath(path);
		if (!chaFileCoordinate.LoadFile(path2))
		{
			return false;
		}
		return base.chaFile.AssignCoordinate(type, chaFileCoordinate);
	}

	public bool AssignCoordinate(ChaFileDefine.CoordinateType type, ChaFileCoordinate srcCoorde)
	{
		return base.chaFile.AssignCoordinate(type, srcCoorde);
	}

	public bool AssignCoordinate(ChaFileDefine.CoordinateType type)
	{
		return base.chaFile.AssignCoordinate(type, nowCoordinate);
	}

	public bool SetNowCoordinate(string path)
	{
		ChaFileCoordinate chaFileCoordinate = new ChaFileCoordinate();
		string path2 = ChaFileControl.ConvertCoordinateFilePath(path);
		if (!chaFileCoordinate.LoadFile(path2))
		{
			return false;
		}
		return SetNowCoordinate(chaFileCoordinate);
	}

	public bool SetNowCoordinate(ChaFileCoordinate srcCoorde)
	{
		byte[] data = srcCoorde.SaveBytes();
		return nowCoordinate.LoadBytes(data, srcCoorde.loadVersion);
	}

	public bool ChangeCoordinateType(bool changeBackCoordinateType = true)
	{
		return ChangeCoordinateType((ChaFileDefine.CoordinateType)base.fileStatus.coordinateType, changeBackCoordinateType);
	}

	public bool ChangeCoordinateType(ChaFileDefine.CoordinateType type, bool changeBackCoordinateType = true)
	{
		if (base.chaFile.coordinate.Length <= (int)type)
		{
			return false;
		}
		byte[] data = base.chaFile.coordinate[(int)type].SaveBytes();
		if (nowCoordinate.LoadBytes(data, ChaFileDefine.ChaFileCoordinateVersion))
		{
			if (changeBackCoordinateType)
			{
				base.fileStatus.backCoordinateType = base.fileStatus.coordinateType;
			}
			base.fileStatus.coordinateType = (int)type;
			return true;
		}
		return false;
	}

	public bool ChangeCoordinateTypeAndReload(bool changeBackCoordinateType = true)
	{
		if (ChangeCoordinateType(changeBackCoordinateType))
		{
			Reload(noChangeClothes: false, noChangeHead: true, noChangeHair: true, noChangeBody: true);
			return true;
		}
		return false;
	}

	public bool ChangeCoordinateTypeAndReload(ChaFileDefine.CoordinateType type, bool changeBackCoordinateType = true)
	{
		if (ChangeCoordinateType(type, changeBackCoordinateType))
		{
			Reload(noChangeClothes: false, noChangeHead: true, noChangeHair: true, noChangeBody: true);
			return true;
		}
		return false;
	}

	public bool AddClothesStateKind(int clothesKind, string stateType)
	{
		ChaFileDefine.ClothesKind clothesKind2 = (ChaFileDefine.ClothesKind)Enum.ToObject(typeof(ChaFileDefine.ClothesKind), clothesKind);
		switch (clothesKind2)
		{
		case ChaFileDefine.ClothesKind.gloves:
			dictStateType.Remove(4);
			AddClothesStateKindSub(4, byte.Parse(stateType));
			break;
		case ChaFileDefine.ClothesKind.panst:
			dictStateType.Remove(5);
			AddClothesStateKindSub(5, byte.Parse(stateType));
			break;
		case ChaFileDefine.ClothesKind.socks:
			dictStateType.Remove(6);
			AddClothesStateKindSub(6, byte.Parse(stateType));
			break;
		case ChaFileDefine.ClothesKind.shoes_inner:
			dictStateType.Remove(7);
			AddClothesStateKindSub(7, byte.Parse(stateType));
			break;
		case ChaFileDefine.ClothesKind.shoes_outer:
			dictStateType.Remove(8);
			AddClothesStateKindSub(8, byte.Parse(stateType));
			break;
		case ChaFileDefine.ClothesKind.top:
		case ChaFileDefine.ClothesKind.bot:
		{
			dictStateType.Remove(0);
			dictStateType.Remove(1);
			ListInfoBase listInfoBase3 = base.infoClothes[0];
			byte b3 = 3;
			if (listInfoBase3 != null)
			{
				b3 = byte.Parse(listInfoBase3.GetInfo(ChaListDefine.KeyType.StateType));
			}
			byte b4 = 3;
			ListInfoBase listInfoBase4 = base.infoClothes[1];
			if (listInfoBase4 != null)
			{
				b4 = byte.Parse(listInfoBase4.GetInfo(ChaListDefine.KeyType.StateType));
			}
			if (b4 != 0 && (bool)GetReferenceInfo(RefObjKey.S_CTOP_B_DEF))
			{
				if (b3 == 0)
				{
					b4 = 0;
				}
				else if (1 == b3 && 2 == b4)
				{
					b4 = 1;
				}
			}
			if (b3 != 0 && (bool)GetReferenceInfo(RefObjKey.S_CBOT_T_DEF))
			{
				if (b4 == 0)
				{
					b3 = 0;
				}
				else if (1 == b4 && 2 == b3)
				{
					b3 = 1;
				}
			}
			AddClothesStateKindSub(0, b3);
			AddClothesStateKindSub(1, b4);
			if (clothesKind2 == ChaFileDefine.ClothesKind.top)
			{
				AddClothesStateKind(2, "");
			}
			break;
		}
		case ChaFileDefine.ClothesKind.bra:
		case ChaFileDefine.ClothesKind.shorts:
		{
			dictStateType.Remove(2);
			dictStateType.Remove(3);
			byte b = 3;
			if (!notBra)
			{
				ListInfoBase listInfoBase = base.infoClothes[2];
				if (listInfoBase != null)
				{
					b = byte.Parse(listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
				}
			}
			byte b2 = 3;
			if (!notBra || !notShorts)
			{
				ListInfoBase listInfoBase2 = base.infoClothes[3];
				if (listInfoBase2 != null)
				{
					b2 = byte.Parse(listInfoBase2.GetInfo(ChaListDefine.KeyType.StateType));
				}
			}
			if (b2 != 0 && 2 != b2 && (bool)GetReferenceInfo(RefObjKey.S_UWT_B_DEF))
			{
				if (b == 0)
				{
					b2 = 0;
				}
				else if (1 == b && 3 == b2)
				{
					b2 = 1;
				}
			}
			if (b != 0 && 2 != b && (bool)GetReferenceInfo(RefObjKey.S_UWB_T_DEF))
			{
				if (b2 == 0)
				{
					b = 0;
				}
				else if (1 == b2 && 3 == b)
				{
					b = 1;
				}
			}
			AddClothesStateKindSub(2, b);
			AddClothesStateKindSub(3, b2);
			break;
		}
		}
		return true;
	}

	private bool AddClothesStateKindSub(int clothesKind, byte type)
	{
		if (!MathfEx.RangeEqualOn(0, type, 2))
		{
			return false;
		}
		Dictionary<byte, string> dictionary = new Dictionary<byte, string>();
		if (type == 0)
		{
			dictionary[0] = "着衣";
			dictionary[1] = "半脱";
			dictionary[3] = "脱衣";
		}
		else if (1 == type)
		{
			dictionary[0] = "着衣";
			dictionary[3] = "脱衣";
		}
		else
		{
			dictionary[0] = "着衣";
			dictionary[1] = "半脱１";
			dictionary[2] = "半脱２";
			dictionary[3] = "脱衣";
		}
		dictStateType[clothesKind] = dictionary;
		return true;
	}

	public void RemoveClothesStateKind(int clothesKind)
	{
		switch ((ChaFileDefine.ClothesKind)Enum.ToObject(typeof(ChaFileDefine.ClothesKind), clothesKind))
		{
		case ChaFileDefine.ClothesKind.gloves:
			dictStateType.Remove(4);
			break;
		case ChaFileDefine.ClothesKind.panst:
			dictStateType.Remove(5);
			break;
		case ChaFileDefine.ClothesKind.socks:
			dictStateType.Remove(6);
			break;
		case ChaFileDefine.ClothesKind.shoes_inner:
			dictStateType.Remove(7);
			break;
		case ChaFileDefine.ClothesKind.shoes_outer:
			dictStateType.Remove(8);
			break;
		case ChaFileDefine.ClothesKind.top:
		case ChaFileDefine.ClothesKind.bot:
			AddClothesStateKind(0, "");
			break;
		case ChaFileDefine.ClothesKind.bra:
		case ChaFileDefine.ClothesKind.shorts:
			AddClothesStateKind(2, "");
			break;
		}
	}

	public bool IsClothes(int clothesKind)
	{
		if (!IsClothesStateKind(clothesKind))
		{
			return false;
		}
		if (null == base.objClothes[clothesKind])
		{
			return false;
		}
		if (base.infoClothes[clothesKind] == null)
		{
			return false;
		}
		return true;
	}

	public bool IsClothesStateKind(int clothesKind)
	{
		return dictStateType.ContainsKey(clothesKind);
	}

	public bool IsShoesStateKind()
	{
		int clothesKind = ((base.fileStatus.shoesType == 0) ? 7 : 8);
		return IsClothesStateKind(clothesKind);
	}

	public Dictionary<byte, string> GetClothesStateKind(int clothesKind)
	{
		Dictionary<byte, string> value = null;
		dictStateType.TryGetValue(clothesKind, out value);
		return value;
	}

	public bool IsClothesStateType(int clothesKind, byte stateType)
	{
		Dictionary<byte, string> value = null;
		dictStateType.TryGetValue(clothesKind, out value);
		return value?.ContainsKey(stateType) ?? false;
	}

	public bool IsAccessory(int slotNo)
	{
		if (!MathfEx.RangeEqualOn(0, slotNo, 19))
		{
			return false;
		}
		if (!(null == base.objAccessory[slotNo]))
		{
			return true;
		}
		return false;
	}

	public void SetClothesState(int clothesKind, byte state, bool next = true)
	{
		if (next)
		{
			byte b = base.fileStatus.clothesState[clothesKind];
			do
			{
				if (!IsClothesStateKind(clothesKind))
				{
					base.fileStatus.clothesState[clothesKind] = state;
					break;
				}
				if (IsClothesStateType(clothesKind, state))
				{
					base.fileStatus.clothesState[clothesKind] = state;
					break;
				}
				state = (byte)((state + 1) % 4);
			}
			while (b != state);
		}
		else
		{
			byte b2 = base.fileStatus.clothesState[clothesKind];
			do
			{
				if (!IsClothesStateKind(clothesKind))
				{
					base.fileStatus.clothesState[clothesKind] = state;
					break;
				}
				if (IsClothesStateType(clothesKind, state))
				{
					base.fileStatus.clothesState[clothesKind] = state;
					break;
				}
				state = (byte)((state + 3) % 4);
			}
			while (b2 != state);
		}
		switch (clothesKind)
		{
		case 0:
			if (notBot)
			{
				if (3 == base.fileStatus.clothesState[clothesKind])
				{
					base.fileStatus.clothesState[1] = 3;
				}
				else if (3 == base.fileStatus.clothesState[1])
				{
					base.fileStatus.clothesState[1] = state;
				}
			}
			break;
		case 1:
			if (notBot)
			{
				if (3 == base.fileStatus.clothesState[clothesKind])
				{
					base.fileStatus.clothesState[0] = 3;
				}
				else if (3 == base.fileStatus.clothesState[0])
				{
					base.fileStatus.clothesState[0] = state;
				}
			}
			break;
		case 2:
			if (notShorts)
			{
				if (3 == base.fileStatus.clothesState[clothesKind])
				{
					base.fileStatus.clothesState[3] = 3;
				}
				else if (3 == base.fileStatus.clothesState[3])
				{
					base.fileStatus.clothesState[3] = state;
				}
			}
			break;
		case 3:
			if (notShorts)
			{
				if (3 == base.fileStatus.clothesState[clothesKind])
				{
					base.fileStatus.clothesState[2] = 3;
				}
				else if (3 == base.fileStatus.clothesState[2])
				{
					base.fileStatus.clothesState[2] = state;
				}
			}
			break;
		case 7:
			if (state == 0)
			{
				if (IsClothesStateKind(8))
				{
					base.fileStatus.clothesState[8] = state;
				}
			}
			else
			{
				base.fileStatus.clothesState[8] = state;
			}
			break;
		case 8:
			if (state == 0)
			{
				if (IsClothesStateKind(7))
				{
					base.fileStatus.clothesState[7] = state;
				}
			}
			else
			{
				base.fileStatus.clothesState[7] = state;
			}
			break;
		case 4:
		case 5:
		case 6:
			break;
		}
	}

	public void SetClothesStatePrev(int clothesKind)
	{
		byte b = base.fileStatus.clothesState[clothesKind];
		b = (byte)((b + 3) % 4);
		SetClothesState(clothesKind, b, next: false);
	}

	public void SetClothesStateNext(int clothesKind)
	{
		byte b = base.fileStatus.clothesState[clothesKind];
		b = (byte)((b + 1) % 4);
		SetClothesState(clothesKind, b);
	}

	public void SetClothesStateAll(byte state)
	{
		int num = Enum.GetNames(typeof(ChaFileDefine.ClothesKind)).Length;
		for (int i = 0; i < num; i++)
		{
			SetClothesState(i, state);
		}
	}

	public void UpdateClothesStateAll()
	{
		int num = Enum.GetNames(typeof(ChaFileDefine.ClothesKind)).Length;
		for (int i = 0; i < num; i++)
		{
			SetClothesState(i, base.fileStatus.clothesState[i]);
		}
	}

	public void RandomChangeOfClothesLowPoly(int h)
	{
		if (!isChangeOfClothesRandom)
		{
			isChangeOfClothesRandom = true;
			int num = (int)((float)h * 0.5f);
			int[] obj = new int[6] { 10, 0, 50, 50, 50, 50 };
			obj[1] = 5 + num;
			int randomIndex = ChaRandom.GetRandomIndex(obj);
			int num2 = Enum.GetNames(typeof(ChaFileDefine.ClothesKind)).Length;
			for (int i = 0; i < num2; i++)
			{
				SetClothesState(i, RandomChangeState[randomIndex, i]);
			}
		}
	}

	public void RandomChangeOfClothesLowPolyEnd()
	{
		SetClothesStateAll(0);
		isChangeOfClothesRandom = false;
	}

	public void ChangeToiletStateLowPoly()
	{
		SetClothesState(1, 3);
		SetClothesState(3, 3);
		SetClothesState(5, 3);
	}

	public void SetAccessoryState(int slotNo, bool show)
	{
		if (base.fileStatus.showAccessory.Length > slotNo)
		{
			base.fileStatus.showAccessory[slotNo] = show;
		}
	}

	public void SetAccessoryStateAll(bool show)
	{
		for (int i = 0; i < base.fileStatus.showAccessory.Length; i++)
		{
			base.fileStatus.showAccessory[i] = show;
		}
	}

	public void SetAccessoryStateCategory(int cateNo, bool show)
	{
		if (cateNo != 0 && 1 != cateNo)
		{
			return;
		}
		for (int i = 0; i < base.fileStatus.showAccessory.Length; i++)
		{
			if (nowCoordinate.accessory.parts[i].hideCategory == cateNo)
			{
				base.fileStatus.showAccessory[i] = show;
			}
		}
	}

	public int GetAccessoryCategoryCount(int cateNo)
	{
		if (cateNo != 0 && 1 != cateNo)
		{
			return -1;
		}
		int num = 0;
		for (int i = 0; i < base.fileStatus.showAccessory.Length; i++)
		{
			if (nowCoordinate.accessory.parts[i].hideCategory == cateNo)
			{
				num++;
			}
		}
		return num;
	}

	public string GetAccessoryDefaultParentStr(int slotNo)
	{
		ListInfoBase listInfoBase = base.infoAccessory[slotNo];
		if (listInfoBase != null)
		{
			return listInfoBase.GetString(ChaListDefine.KeyType.Parent);
		}
		GameObject gameObject = base.objAccessory[slotNo];
		if (null == gameObject)
		{
			return "";
		}
		return gameObject.GetComponent<ListInfoComponent>().data.GetInfo(ChaListDefine.KeyType.Parent);
	}

	public bool ChangeAccessoryParent(int slotNo, string parentStr)
	{
		if (!MathfEx.RangeEqualOn(0, slotNo, 19))
		{
			return false;
		}
		GameObject gameObject = base.objAccessory[slotNo];
		if (null == gameObject)
		{
			return false;
		}
		if ("none" == parentStr)
		{
			gameObject.transform.SetParent(null, worldPositionStays: false);
			return true;
		}
		ListInfoBase listInfoBase = base.infoAccessory[slotNo];
		if (listInfoBase != null)
		{
			listInfoBase = gameObject.GetComponent<ListInfoComponent>().data;
		}
		if ("0" == listInfoBase.GetInfo(ChaListDefine.KeyType.Parent))
		{
			return false;
		}
		try
		{
			RefObjKey key = (RefObjKey)Enum.Parse(typeof(RefObjKey), parentStr);
			GameObject referenceInfo = GetReferenceInfo(key);
			if (null == referenceInfo)
			{
				return false;
			}
			gameObject.transform.SetParent(referenceInfo.transform, worldPositionStays: false);
			nowCoordinate.accessory.parts[slotNo].parentKey = parentStr;
			nowCoordinate.accessory.parts[slotNo].partsOfHead = ChaAccessoryDefine.CheckPartsOfHead(parentStr);
		}
		catch (ArgumentException)
		{
			return false;
		}
		return true;
	}

	public bool SetAccessoryPos(int slotNo, int correctNo, float value, bool add, int flags = 7)
	{
		if (!MathfEx.RangeEqualOn(0, slotNo, 19))
		{
			return false;
		}
		GameObject gameObject = base.objAcsMove[slotNo, correctNo];
		if (null == gameObject)
		{
			return false;
		}
		ChaFileAccessory accessory = nowCoordinate.accessory;
		if ((flags & 1) != 0)
		{
			float value2 = float.Parse(((add ? accessory.parts[slotNo].addMove[correctNo, 0].x : 0f) + value).ToString("f1"));
			accessory.parts[slotNo].addMove[correctNo, 0].x = Mathf.Clamp(value2, -100f, 100f);
		}
		if ((flags & 2) != 0)
		{
			float value3 = float.Parse(((add ? accessory.parts[slotNo].addMove[correctNo, 0].y : 0f) + value).ToString("f1"));
			accessory.parts[slotNo].addMove[correctNo, 0].y = Mathf.Clamp(value3, -100f, 100f);
		}
		if ((flags & 4) != 0)
		{
			float value4 = float.Parse(((add ? accessory.parts[slotNo].addMove[correctNo, 0].z : 0f) + value).ToString("f1"));
			accessory.parts[slotNo].addMove[correctNo, 0].z = Mathf.Clamp(value4, -100f, 100f);
		}
		ListInfoBase info = base.lstCtrl.GetInfo((ChaListDefine.CategoryNo)accessory.parts[slotNo].type, accessory.parts[slotNo].id);
		if (1 == info.GetInfoInt(ChaListDefine.KeyType.HideHair))
		{
			gameObject.transform.localPosition = new Vector3(0f, 0f, 0f);
		}
		else
		{
			gameObject.transform.localPosition = new Vector3(accessory.parts[slotNo].addMove[correctNo, 0].x * 0.01f, accessory.parts[slotNo].addMove[correctNo, 0].y * 0.01f, accessory.parts[slotNo].addMove[correctNo, 0].z * 0.01f);
		}
		return true;
	}

	public bool SetAccessoryRot(int slotNo, int correctNo, float value, bool add, int flags = 7)
	{
		if (!MathfEx.RangeEqualOn(0, slotNo, 19))
		{
			return false;
		}
		GameObject gameObject = base.objAcsMove[slotNo, correctNo];
		if (null == gameObject)
		{
			return false;
		}
		ChaFileAccessory accessory = nowCoordinate.accessory;
		if ((flags & 1) != 0)
		{
			float t = (int)((add ? accessory.parts[slotNo].addMove[correctNo, 1].x : 0f) + value);
			accessory.parts[slotNo].addMove[correctNo, 1].x = Mathf.Repeat(t, 360f);
		}
		if ((flags & 2) != 0)
		{
			float t2 = (int)((add ? accessory.parts[slotNo].addMove[correctNo, 1].y : 0f) + value);
			accessory.parts[slotNo].addMove[correctNo, 1].y = Mathf.Repeat(t2, 360f);
		}
		if ((flags & 4) != 0)
		{
			float t3 = (int)((add ? accessory.parts[slotNo].addMove[correctNo, 1].z : 0f) + value);
			accessory.parts[slotNo].addMove[correctNo, 1].z = Mathf.Repeat(t3, 360f);
		}
		ListInfoBase info = base.lstCtrl.GetInfo((ChaListDefine.CategoryNo)accessory.parts[slotNo].type, accessory.parts[slotNo].id);
		if (1 == info.GetInfoInt(ChaListDefine.KeyType.HideHair))
		{
			gameObject.transform.localEulerAngles = new Vector3(0f, 0f, 0f);
		}
		else
		{
			gameObject.transform.localEulerAngles = new Vector3(accessory.parts[slotNo].addMove[correctNo, 1].x, accessory.parts[slotNo].addMove[correctNo, 1].y, accessory.parts[slotNo].addMove[correctNo, 1].z);
		}
		return true;
	}

	public bool SetAccessoryScl(int slotNo, int correctNo, float value, bool add, int flags = 7)
	{
		if (!MathfEx.RangeEqualOn(0, slotNo, 19))
		{
			return false;
		}
		GameObject gameObject = base.objAcsMove[slotNo, correctNo];
		if (null == gameObject)
		{
			return false;
		}
		ChaFileAccessory accessory = nowCoordinate.accessory;
		if ((flags & 1) != 0)
		{
			float value2 = float.Parse(((add ? accessory.parts[slotNo].addMove[correctNo, 2].x : 0f) + value).ToString("f2"));
			accessory.parts[slotNo].addMove[correctNo, 2].x = Mathf.Clamp(value2, 0.01f, 100f);
		}
		if ((flags & 2) != 0)
		{
			float value3 = float.Parse(((add ? accessory.parts[slotNo].addMove[correctNo, 2].y : 0f) + value).ToString("f2"));
			accessory.parts[slotNo].addMove[correctNo, 2].y = Mathf.Clamp(value3, 0.01f, 100f);
		}
		if ((flags & 4) != 0)
		{
			float value4 = float.Parse(((add ? accessory.parts[slotNo].addMove[correctNo, 2].z : 0f) + value).ToString("f2"));
			accessory.parts[slotNo].addMove[correctNo, 2].z = Mathf.Clamp(value4, 0.01f, 100f);
		}
		ListInfoBase info = base.lstCtrl.GetInfo((ChaListDefine.CategoryNo)accessory.parts[slotNo].type, accessory.parts[slotNo].id);
		if (1 == info.GetInfoInt(ChaListDefine.KeyType.HideHair))
		{
			gameObject.transform.localScale = new Vector3(1f, 1f, 1f);
		}
		else
		{
			gameObject.transform.localScale = new Vector3(accessory.parts[slotNo].addMove[correctNo, 2].x, accessory.parts[slotNo].addMove[correctNo, 2].y, accessory.parts[slotNo].addMove[correctNo, 2].z);
		}
		return true;
	}

	public bool ResetAccessoryMove(int slotNo, int correctNo, int type = 7)
	{
		bool flag = true;
		if ((type & 1) != 0)
		{
			flag &= SetAccessoryPos(slotNo, correctNo, 0f, add: false);
		}
		if ((type & 2) != 0)
		{
			flag &= SetAccessoryRot(slotNo, correctNo, 0f, add: false);
		}
		if ((type & 4) != 0)
		{
			flag &= SetAccessoryScl(slotNo, correctNo, 1f, add: false);
		}
		return flag;
	}

	public bool UpdateAccessoryMoveFromInfo(int slotNo)
	{
		if (!MathfEx.RangeEqualOn(0, slotNo, 19))
		{
			return false;
		}
		ChaFileAccessory accessory = nowCoordinate.accessory;
		ListInfoBase info = base.lstCtrl.GetInfo((ChaListDefine.CategoryNo)accessory.parts[slotNo].type, accessory.parts[slotNo].id);
		if (1 == info.GetInfoInt(ChaListDefine.KeyType.HideHair))
		{
			for (int i = 0; i < 2; i++)
			{
				GameObject gameObject = base.objAcsMove[slotNo, i];
				if (!(null == gameObject))
				{
					gameObject.transform.localPosition = new Vector3(0f, 0f, 0f);
					gameObject.transform.localEulerAngles = new Vector3(0f, 0f, 0f);
					gameObject.transform.localScale = new Vector3(1f, 1f, 1f);
				}
			}
		}
		else
		{
			for (int j = 0; j < 2; j++)
			{
				GameObject gameObject2 = base.objAcsMove[slotNo, j];
				if (!(null == gameObject2))
				{
					gameObject2.transform.localPosition = new Vector3(accessory.parts[slotNo].addMove[j, 0].x * 0.01f, accessory.parts[slotNo].addMove[j, 0].y * 0.01f, accessory.parts[slotNo].addMove[j, 0].z * 0.01f);
					gameObject2.transform.localEulerAngles = new Vector3(accessory.parts[slotNo].addMove[j, 1].x, accessory.parts[slotNo].addMove[j, 1].y, accessory.parts[slotNo].addMove[j, 1].z);
					gameObject2.transform.localScale = new Vector3(accessory.parts[slotNo].addMove[j, 2].x, accessory.parts[slotNo].addMove[j, 2].y, accessory.parts[slotNo].addMove[j, 2].z);
				}
			}
		}
		return true;
	}

	public bool UpdateAccessoryMoveAllFromInfo()
	{
		for (int i = 0; i < 20; i++)
		{
			UpdateAccessoryMoveFromInfo(i);
		}
		return true;
	}

	public bool ChangeAccessoryColor(int slotNo)
	{
		if (!MathfEx.RangeEqualOn(0, slotNo, 19))
		{
			return false;
		}
		ChaAccessoryComponent chaAccessoryComponent = base.cusAcsCmp[slotNo];
		ChaFileAccessory.PartsInfo partsInfo = nowCoordinate.accessory.parts[slotNo];
		if (null == chaAccessoryComponent)
		{
			return false;
		}
		if (chaAccessoryComponent.rendNormal != null)
		{
			Renderer[] rendNormal = chaAccessoryComponent.rendNormal;
			foreach (Renderer renderer in rendNormal)
			{
				if (chaAccessoryComponent.useColor01)
				{
					renderer.material.SetColor(ChaShader._Color, partsInfo.color[0]);
				}
				if (chaAccessoryComponent.useColor02)
				{
					renderer.material.SetColor(ChaShader._Color2, partsInfo.color[1]);
				}
				if (chaAccessoryComponent.useColor03)
				{
					renderer.material.SetColor(ChaShader._Color3, partsInfo.color[2]);
				}
			}
		}
		if (chaAccessoryComponent.rendAlpha != null)
		{
			Renderer[] rendNormal = chaAccessoryComponent.rendAlpha;
			foreach (Renderer obj in rendNormal)
			{
				obj.material.SetColor(ChaShader._Color4, partsInfo.color[3]);
				obj.gameObject.SetActiveIfDifferent((0f != partsInfo.color[3].a) ? true : false);
			}
		}
		if (chaAccessoryComponent.rendHair != null)
		{
			Color startColor = base.fileHair.parts[0].startColor;
			Renderer[] rendNormal = chaAccessoryComponent.rendHair;
			foreach (Renderer obj2 in rendNormal)
			{
				obj2.material.SetColor(ChaShader._Color, startColor);
				obj2.material.SetColor(ChaShader._Color2, startColor);
				obj2.material.SetColor(ChaShader._Color3, startColor);
			}
		}
		return true;
	}

	public bool GetAccessoryDefaultColor(ref Color color, int slotNo, int no)
	{
		if (!MathfEx.RangeEqualOn(0, slotNo, 19))
		{
			return false;
		}
		ChaAccessoryComponent chaAccessoryComponent = base.cusAcsCmp[slotNo];
		if (null == chaAccessoryComponent)
		{
			return false;
		}
		if (no == 0 && chaAccessoryComponent.useColor01)
		{
			color = chaAccessoryComponent.defColor01;
			return true;
		}
		if (1 == no && chaAccessoryComponent.useColor02)
		{
			color = chaAccessoryComponent.defColor02;
			return true;
		}
		if (2 == no && chaAccessoryComponent.useColor03)
		{
			color = chaAccessoryComponent.defColor03;
			return true;
		}
		if (3 == no && chaAccessoryComponent.rendAlpha != null && chaAccessoryComponent.rendAlpha.Length != 0)
		{
			color = chaAccessoryComponent.defColor04;
			return true;
		}
		return false;
	}

	public void SetAccessoryDefaultColor(int slotNo)
	{
		if (!MathfEx.RangeEqualOn(0, slotNo, 19))
		{
			return;
		}
		ChaAccessoryComponent chaAccessoryComponent = base.cusAcsCmp[slotNo];
		if (!(null == chaAccessoryComponent))
		{
			if (chaAccessoryComponent.useColor01)
			{
				nowCoordinate.accessory.parts[slotNo].color[0] = chaAccessoryComponent.defColor01;
			}
			if (chaAccessoryComponent.useColor02)
			{
				nowCoordinate.accessory.parts[slotNo].color[1] = chaAccessoryComponent.defColor02;
			}
			if (chaAccessoryComponent.useColor03)
			{
				nowCoordinate.accessory.parts[slotNo].color[2] = chaAccessoryComponent.defColor03;
			}
			if (chaAccessoryComponent.rendAlpha != null && chaAccessoryComponent.rendAlpha.Length != 0)
			{
				nowCoordinate.accessory.parts[slotNo].color[3] = chaAccessoryComponent.defColor04;
			}
		}
	}

	public void ChangeShakeAccessory(int slotNo)
	{
		if ((bool)base.objAccessory[slotNo])
		{
			DynamicBone[] componentsInChildren = base.objAccessory[slotNo].GetComponentsInChildren<DynamicBone>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].enabled = !nowCoordinate.accessory.parts[slotNo].noShake;
			}
		}
	}

	public void SetHideHairAccessory()
	{
		for (int i = 0; i < hideHairAcs.Length; i++)
		{
			hideHairAcs[i] = false;
		}
		for (int j = 0; j < base.infoAccessory.Length; j++)
		{
			if (base.infoAccessory[j] != null && base.infoAccessory[j].GetInfoInt(ChaListDefine.KeyType.HideHair) != 0)
			{
				hideHairAcs[j] = true;
			}
		}
	}

	public bool CheckHideHair()
	{
		for (int i = 0; i < hideHairAcs.Length; i++)
		{
			if (hideHairAcs[i] && base.fileStatus.showAccessory[i])
			{
				return true;
			}
		}
		return false;
	}

	public bool ChangeCustomClothes(bool main, int kind, bool updateColor, bool updateTex01, bool updateTex02, bool updateTex03, bool updateTex04)
	{
		CustomTextureCreate[] array = new CustomTextureCreate[3]
		{
			main ? base.ctCreateClothes[kind, 0] : base.ctCreateClothesSub[kind, 0],
			main ? base.ctCreateClothes[kind, 1] : base.ctCreateClothesSub[kind, 1],
			main ? base.ctCreateClothes[kind, 2] : base.ctCreateClothesSub[kind, 2]
		};
		if (array[0] == null)
		{
			return false;
		}
		ChaClothesComponent chaClothesComponent = (main ? GetCustomClothesComponent(kind) : GetCustomClothesSubComponent(kind));
		if (null == chaClothesComponent)
		{
			return false;
		}
		ChaFileClothes.PartsInfo partsInfo = (main ? nowCoordinate.clothes.parts[kind] : nowCoordinate.clothes.parts[0]);
		if (main)
		{
			if (!updateColor && !updateTex01 && !updateTex02 && !updateTex03)
			{
				return false;
			}
		}
		else if (!updateColor && !updateTex01 && !updateTex02 && !updateTex03 && !updateTex04)
		{
			return false;
		}
		bool result = true;
		int[] array2 = new int[4]
		{
			ChaShader._PatternMask1,
			ChaShader._PatternMask2,
			ChaShader._PatternMask3,
			ChaShader._PatternMask1
		};
		bool[] array3 = new bool[4] { updateTex01, updateTex02, updateTex03, updateTex04 };
		int num = ((!main && 2 == kind) ? 4 : 3);
		for (int i = 0; i < num; i++)
		{
			if (!array3[i])
			{
				continue;
			}
			Texture2D tex = null;
			base.lstCtrl.GetFilePath(ChaListDefine.CategoryNo.mt_pattern, partsInfo.colorInfo[i].pattern, ChaListDefine.KeyType.MainTexAB, ChaListDefine.KeyType.MainTex, out var bundle, out var asset);
			if ("0" != bundle && "0" != asset)
			{
				if (!base.hiPoly)
				{
					asset += "_low";
				}
				tex = CommonLib.LoadAsset<Texture2D>(bundle, asset);
				Character.AddLoadAssetBundle(bundle, "");
			}
			CustomTextureCreate[] array4 = array;
			for (int j = 0; j < array4.Length; j++)
			{
				array4[j]?.SetTexture(array2[i], tex);
			}
		}
		if (updateColor)
		{
			CustomTextureCreate[] array4 = array;
			foreach (CustomTextureCreate customTextureCreate in array4)
			{
				if (customTextureCreate != null)
				{
					if (!main && 2 == kind)
					{
						customTextureCreate.SetColor(ChaShader._Color, partsInfo.colorInfo[3].baseColor);
						customTextureCreate.SetColor(ChaShader._Color1_2, partsInfo.colorInfo[3].patternColor);
						customTextureCreate.SetFloat(ChaShader._PatternScale1u, partsInfo.colorInfo[3].tiling.x);
						customTextureCreate.SetFloat(ChaShader._PatternScale1v, partsInfo.colorInfo[3].tiling.y);
						customTextureCreate.SetFloat(ChaShader._PatternOffset1u, partsInfo.colorInfo[3].offset.x);
						customTextureCreate.SetFloat(ChaShader._PatternOffset1v, partsInfo.colorInfo[3].offset.y);
						customTextureCreate.SetFloat(ChaShader._PatternRotator1, partsInfo.colorInfo[3].rotate);
					}
					else
					{
						customTextureCreate.SetColor(ChaShader._Color, partsInfo.colorInfo[0].baseColor);
						customTextureCreate.SetColor(ChaShader._Color1_2, partsInfo.colorInfo[0].patternColor);
						customTextureCreate.SetFloat(ChaShader._PatternScale1u, partsInfo.colorInfo[0].tiling.x);
						customTextureCreate.SetFloat(ChaShader._PatternScale1v, partsInfo.colorInfo[0].tiling.y);
						customTextureCreate.SetFloat(ChaShader._PatternOffset1u, partsInfo.colorInfo[0].offset.x);
						customTextureCreate.SetFloat(ChaShader._PatternOffset1v, partsInfo.colorInfo[0].offset.y);
						customTextureCreate.SetFloat(ChaShader._PatternRotator1, partsInfo.colorInfo[0].rotate);
					}
					customTextureCreate.SetColor(ChaShader._Color2, partsInfo.colorInfo[1].baseColor);
					customTextureCreate.SetColor(ChaShader._Color2_2, partsInfo.colorInfo[1].patternColor);
					customTextureCreate.SetFloat(ChaShader._PatternScale2u, partsInfo.colorInfo[1].tiling.x);
					customTextureCreate.SetFloat(ChaShader._PatternScale2v, partsInfo.colorInfo[1].tiling.y);
					customTextureCreate.SetColor(ChaShader._Color3, partsInfo.colorInfo[2].baseColor);
					customTextureCreate.SetColor(ChaShader._Color3_2, partsInfo.colorInfo[2].patternColor);
					customTextureCreate.SetFloat(ChaShader._PatternScale3u, partsInfo.colorInfo[2].tiling.x);
					customTextureCreate.SetFloat(ChaShader._PatternScale3v, partsInfo.colorInfo[2].tiling.y);
					customTextureCreate.SetFloat(ChaShader._PatternOffset2u, partsInfo.colorInfo[1].offset.x);
					customTextureCreate.SetFloat(ChaShader._PatternOffset2v, partsInfo.colorInfo[1].offset.y);
					customTextureCreate.SetFloat(ChaShader._PatternRotator2, partsInfo.colorInfo[1].rotate);
					customTextureCreate.SetFloat(ChaShader._PatternOffset3u, partsInfo.colorInfo[2].offset.x);
					customTextureCreate.SetFloat(ChaShader._PatternOffset3v, partsInfo.colorInfo[2].offset.y);
					customTextureCreate.SetFloat(ChaShader._PatternRotator3, partsInfo.colorInfo[2].rotate);
				}
			}
		}
		bool flag = chaClothesComponent.rendNormal01 != null && chaClothesComponent.rendNormal01.Length != 0;
		bool flag2 = chaClothesComponent.rendAlpha01 != null && chaClothesComponent.rendAlpha01.Length != 0;
		if (flag || flag2)
		{
			Texture texture = array[0].RebuildTextureAndSetMaterial();
			if (null != texture)
			{
				if (flag)
				{
					for (int k = 0; k < chaClothesComponent.rendNormal01.Length; k++)
					{
						if ((bool)chaClothesComponent.rendNormal01[k])
						{
							chaClothesComponent.rendNormal01[k].material.SetTexture(ChaShader._MainTex, texture);
						}
						else
						{
							result = false;
						}
					}
				}
				if (flag2)
				{
					for (int l = 0; l < chaClothesComponent.rendAlpha01.Length; l++)
					{
						if ((bool)chaClothesComponent.rendAlpha01[l])
						{
							chaClothesComponent.rendAlpha01[l].material.SetTexture(ChaShader._MainTex, texture);
						}
						else
						{
							result = false;
						}
					}
				}
			}
			else
			{
				result = false;
			}
		}
		else
		{
			result = false;
		}
		if (chaClothesComponent.rendNormal02 != null && chaClothesComponent.rendNormal02.Length != 0 && array[1] != null)
		{
			Texture texture2 = array[1].RebuildTextureAndSetMaterial();
			if (null != texture2)
			{
				for (int m = 0; m < chaClothesComponent.rendNormal02.Length; m++)
				{
					if ((bool)chaClothesComponent.rendNormal02[m])
					{
						chaClothesComponent.rendNormal02[m].material.SetTexture(ChaShader._MainTex, texture2);
					}
					else
					{
						result = false;
					}
				}
			}
			else
			{
				result = false;
			}
		}
		if (chaClothesComponent.rendNormal03 != null && chaClothesComponent.rendNormal03.Length != 0 && array[2] != null)
		{
			Texture texture3 = array[2].RebuildTextureAndSetMaterial();
			if (null != texture3)
			{
				for (int n = 0; n < chaClothesComponent.rendNormal03.Length; n++)
				{
					if ((bool)chaClothesComponent.rendNormal03[n])
					{
						chaClothesComponent.rendNormal03[n].material.SetTexture(ChaShader._MainTex, texture3);
					}
					else
					{
						result = false;
					}
				}
			}
			else
			{
				result = false;
			}
		}
		if (null != chaClothesComponent.rendAccessory && array[0] != null)
		{
			Texture texture4 = array[0].RebuildTextureAndSetMaterial();
			if (null != texture4)
			{
				if ((bool)chaClothesComponent.rendAccessory)
				{
					chaClothesComponent.rendAccessory.material.SetTexture(ChaShader._MainTex, texture4);
				}
				else
				{
					result = false;
				}
			}
			else
			{
				result = false;
			}
		}
		return result;
	}

	public bool UpdateClothesSiru(int kind, float frontTop, float frontBot, float downTop, float downBot)
	{
		int num = 0;
		if (kind == 0)
		{
			ListInfoBase info = base.lstCtrl.GetInfo(ChaListDefine.CategoryNo.co_top, nowCoordinate.clothes.parts[0].id);
			if (info != null)
			{
				num = info.Kind;
			}
		}
		ChaClothesComponent[] array = null;
		if (1 == num)
		{
			array = new ChaClothesComponent[1] { GetCustomClothesSubComponent(0) };
			if (null == array[0])
			{
				return false;
			}
		}
		else if (2 == num)
		{
			array = new ChaClothesComponent[2]
			{
				GetCustomClothesSubComponent(0),
				GetCustomClothesSubComponent(1)
			};
			if (null == array[0] && null == array[1])
			{
				return false;
			}
		}
		else
		{
			array = new ChaClothesComponent[1] { GetCustomClothesComponent(kind) };
			if (null == array[0])
			{
				return false;
			}
		}
		ChaClothesComponent[] array2 = array;
		foreach (ChaClothesComponent chaClothesComponent in array2)
		{
			if (null == chaClothesComponent)
			{
				continue;
			}
			Renderer[] rendNormal;
			if (chaClothesComponent.rendNormal01 != null && chaClothesComponent.rendNormal01.Length != 0)
			{
				rendNormal = chaClothesComponent.rendNormal01;
				foreach (Renderer renderer in rendNormal)
				{
					if (!(null == renderer))
					{
						renderer.material.SetFloat(ChaShader._liquidftop, frontTop);
						renderer.material.SetFloat(ChaShader._liquidfbot, frontBot);
						renderer.material.SetFloat(ChaShader._liquidbtop, downTop);
						renderer.material.SetFloat(ChaShader._liquidbbot, downBot);
					}
				}
			}
			if (chaClothesComponent.rendNormal02 != null && chaClothesComponent.rendNormal02.Length != 0)
			{
				rendNormal = chaClothesComponent.rendNormal02;
				foreach (Renderer renderer2 in rendNormal)
				{
					if (!(null == renderer2))
					{
						renderer2.material.SetFloat(ChaShader._liquidftop, frontTop);
						renderer2.material.SetFloat(ChaShader._liquidfbot, frontBot);
						renderer2.material.SetFloat(ChaShader._liquidbtop, downTop);
						renderer2.material.SetFloat(ChaShader._liquidbbot, downBot);
					}
				}
			}
			if (chaClothesComponent.rendNormal03 == null || chaClothesComponent.rendNormal03.Length == 0)
			{
				continue;
			}
			rendNormal = chaClothesComponent.rendNormal03;
			foreach (Renderer renderer3 in rendNormal)
			{
				if (!(null == renderer3))
				{
					renderer3.material.SetFloat(ChaShader._liquidftop, frontTop);
					renderer3.material.SetFloat(ChaShader._liquidfbot, frontBot);
					renderer3.material.SetFloat(ChaShader._liquidbtop, downTop);
					renderer3.material.SetFloat(ChaShader._liquidbbot, downBot);
				}
			}
		}
		return true;
	}

	public Color GetClothesDefaultColor(int kind, int no)
	{
		int num = 0;
		if (kind == 0)
		{
			ListInfoBase info = base.lstCtrl.GetInfo(ChaListDefine.CategoryNo.co_top, nowCoordinate.clothes.parts[0].id);
			if (info != null)
			{
				num = info.Kind;
			}
		}
		if (1 == num || 2 == num)
		{
			return ChaFileDefine.defClothesSubColor[no];
		}
		ChaClothesComponent customClothesComponent = GetCustomClothesComponent(kind);
		if (null != customClothesComponent)
		{
			if (no == 0)
			{
				return customClothesComponent.defMainColor01;
			}
			if (1 == no)
			{
				return customClothesComponent.defMainColor02;
			}
			if (2 == no)
			{
				return customClothesComponent.defMainColor03;
			}
		}
		return Color.white;
	}

	public void SetClothesDefaultColor(int kind)
	{
		int num = 0;
		if (kind == 0)
		{
			ListInfoBase info = base.lstCtrl.GetInfo(ChaListDefine.CategoryNo.co_top, nowCoordinate.clothes.parts[0].id);
			if (info != null)
			{
				num = info.Kind;
			}
		}
		if (1 == num || 2 == num)
		{
			for (int i = 0; i < 4; i++)
			{
				nowCoordinate.clothes.parts[kind].colorInfo[i].baseColor = ChaFileDefine.defClothesSubColor[i];
			}
			return;
		}
		ChaClothesComponent customClothesComponent = GetCustomClothesComponent(kind);
		if (null != customClothesComponent)
		{
			nowCoordinate.clothes.parts[kind].colorInfo[0].baseColor = customClothesComponent.defMainColor01;
			nowCoordinate.clothes.parts[kind].colorInfo[1].baseColor = customClothesComponent.defMainColor02;
			nowCoordinate.clothes.parts[kind].colorInfo[2].baseColor = customClothesComponent.defMainColor03;
		}
	}

	public bool IsEmblem(int kind, int arrNo)
	{
		ChaClothesComponent[] array = null;
		int num = 0;
		if (kind == 0)
		{
			ListInfoBase info = base.lstCtrl.GetInfo(ChaListDefine.CategoryNo.co_top, nowCoordinate.clothes.parts[0].id);
			if (info != null)
			{
				num = info.Kind;
			}
		}
		if (1 != num && 2 != num)
		{
			array = new ChaClothesComponent[1] { GetCustomClothesComponent(kind) };
		}
		else
		{
			array = new ChaClothesComponent[Enum.GetNames(typeof(ChaFileDefine.ClothesSubKind)).Length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = GetCustomClothesSubComponent(i);
			}
		}
		return IsEmblem(array, arrNo);
	}

	public bool IsEmblem(ChaClothesComponent[] ccc, int arrNo)
	{
		if (ccc == null || ccc.Length == 0)
		{
			return false;
		}
		foreach (ChaClothesComponent chaClothesComponent in ccc)
		{
			if (null == chaClothesComponent)
			{
				continue;
			}
			if (arrNo == 0)
			{
				if (null == chaClothesComponent.rendEmblem01 && (chaClothesComponent.exRendEmblem01 == null || chaClothesComponent.exRendEmblem01.Length == 0))
				{
					continue;
				}
			}
			else if (1 == arrNo && (chaClothesComponent.exRendEmblem02 == null || chaClothesComponent.exRendEmblem02.Length == 0))
			{
				continue;
			}
			return true;
		}
		return false;
	}

	public bool ChangeCustomEmblem(int kind, int arrNo)
	{
		ChaClothesComponent[] array = null;
		int num = 0;
		if (kind == 0)
		{
			ListInfoBase info = base.lstCtrl.GetInfo(ChaListDefine.CategoryNo.co_top, nowCoordinate.clothes.parts[0].id);
			if (info != null)
			{
				num = info.Kind;
			}
		}
		if (1 != num && 2 != num)
		{
			array = new ChaClothesComponent[1] { GetCustomClothesComponent(kind) };
		}
		else
		{
			array = new ChaClothesComponent[Enum.GetNames(typeof(ChaFileDefine.ClothesSubKind)).Length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = GetCustomClothesSubComponent(i);
			}
		}
		if (!IsEmblem(array, arrNo))
		{
			return false;
		}
		ChaFileClothes.PartsInfo partsInfo = nowCoordinate.clothes.parts[kind];
		Texture2D value = null;
		base.lstCtrl.GetFilePath(ChaListDefine.CategoryNo.mt_emblem, (arrNo == 0) ? partsInfo.emblemeId : partsInfo.emblemeId2, ChaListDefine.KeyType.MainTexAB, ChaListDefine.KeyType.MainTex, out var bundle, out var asset);
		if ("0" != bundle && "0" != asset)
		{
			if (!base.hiPoly)
			{
				asset += "_low";
			}
			value = CommonLib.LoadAsset<Texture2D>(bundle, asset);
			Character.AddLoadAssetBundle(bundle, "");
		}
		ChaClothesComponent[] array2 = array;
		foreach (ChaClothesComponent chaClothesComponent in array2)
		{
			if (null == chaClothesComponent)
			{
				continue;
			}
			if (arrNo == 0)
			{
				if (null != chaClothesComponent.rendEmblem01)
				{
					chaClothesComponent.rendEmblem01.material.SetTexture(ChaShader._MainTex, value);
				}
				if (null != chaClothesComponent.rendEmblem02)
				{
					chaClothesComponent.rendEmblem02.material.SetTexture(ChaShader._MainTex, value);
				}
				if (chaClothesComponent.exRendEmblem01 != null)
				{
					Renderer[] exRendEmblem = chaClothesComponent.exRendEmblem01;
					for (int k = 0; k < exRendEmblem.Length; k++)
					{
						exRendEmblem[k].material.SetTexture(ChaShader._MainTex, value);
					}
				}
			}
			else if (1 == arrNo && chaClothesComponent.exRendEmblem02 != null)
			{
				Renderer[] exRendEmblem = chaClothesComponent.exRendEmblem02;
				for (int k = 0; k < exRendEmblem.Length; k++)
				{
					exRendEmblem[k].material.SetTexture(ChaShader._MainTex, value);
				}
			}
		}
		return true;
	}

	protected void InitializeControlCustomAll()
	{
		ShapeFaceNum = ChaFileDefine.cf_headshapename.Length;
		ShapeBodyNum = ChaFileDefine.cf_bodyshapename.Length;
		InitializeControlCustomObject();
	}

	protected void InitializeControlCustomObject()
	{
		sibFace = new ShapeHeadInfoFemale();
		sibBody = new ShapeBodyInfoFemale();
		changeShapeBodyMask = false;
		bustSoft = new BustSoft(this);
		bustGravity = new BustGravity(this);
		updateCMFaceTex = new bool[Enum.GetNames(typeof(FaceTexKind)).Length];
		updateCMFaceColor = new bool[Enum.GetNames(typeof(FaceTexKind)).Length];
		updateCMFaceLayout = new bool[Enum.GetNames(typeof(FaceTexKind)).Length];
		updateCMBodyTex = new bool[Enum.GetNames(typeof(BodyTexKind)).Length];
		updateCMBodyColor = new bool[Enum.GetNames(typeof(BodyTexKind)).Length];
		updateCMBodyLayout = new bool[Enum.GetNames(typeof(BodyTexKind)).Length];
	}

	protected void ReleaseControlCustomAll()
	{
		ReleaseControlCustomObject();
	}

	protected void ReleaseControlCustomObject(bool init = true)
	{
		if (sibFace != null)
		{
			sibFace.ReleaseShapeInfo();
		}
		if (sibBody != null)
		{
			sibBody.ReleaseShapeInfo();
		}
		if (init)
		{
			InitializeControlCustomObject();
		}
	}

	private bool SetBaseMaterial(Renderer rend, Material mat)
	{
		if (null == mat || null == rend)
		{
			return false;
		}
		int num = rend.materials.Length;
		Material[] array = null;
		if (num == 0)
		{
			array = new Material[1] { mat };
		}
		else
		{
			array = new Material[num];
			for (int i = 0; i < num; i++)
			{
				array[i] = rend.materials[i];
			}
			Material material = array[0];
			array[0] = mat;
			if (material != mat)
			{
				UnityEngine.Object.Destroy(material);
			}
		}
		rend.materials = array;
		return true;
	}

	private bool SetCreateTexture(CustomTextureCreate ctc, bool main, ChaListDefine.CategoryNo type, int id, ChaListDefine.KeyType assetBundleKey, ChaListDefine.KeyType assetKey, int propertyID)
	{
		if (!base.lstCtrl.GetFilePath(type, id, assetBundleKey, assetKey, out var bundle, out var asset))
		{
			return false;
		}
		Texture2D texture2D = null;
		if ("0" != bundle && "0" != asset)
		{
			if (!base.hiPoly)
			{
				asset += "_low";
			}
			texture2D = CommonLib.LoadAsset<Texture2D>(bundle, asset);
			Character.AddLoadAssetBundle(bundle, "");
		}
		if (main)
		{
			ctc.SetMainTexture(texture2D);
		}
		else
		{
			ctc.SetTexture(propertyID, texture2D);
		}
		return true;
	}

	private void ChangeTexture(Renderer rend, ChaListDefine.CategoryNo type, int id, ChaListDefine.KeyType assetBundleKey, ChaListDefine.KeyType assetKey, int propertyID, string addStr = "")
	{
		if (!(null == rend))
		{
			ChangeTexture(rend.material, type, id, assetBundleKey, assetKey, propertyID, addStr);
		}
	}

	private void ChangeTexture(Material mat, ChaListDefine.CategoryNo type, int id, ChaListDefine.KeyType assetBundleKey, ChaListDefine.KeyType assetKey, int propertyID, string addStr = "")
	{
		if (!(null == mat))
		{
			Texture2D texture = GetTexture(type, id, assetBundleKey, assetKey, addStr);
			mat.SetTexture(propertyID, texture);
		}
	}

	private Texture2D GetTexture(ChaListDefine.CategoryNo type, int id, ChaListDefine.KeyType assetBundleKey, ChaListDefine.KeyType assetKey, string addStr = "")
	{
		ListInfoBase listInfo = base.lstCtrl.GetListInfo(type, id);
		if (listInfo != null)
		{
			string info = listInfo.GetInfo(assetBundleKey);
			string text = listInfo.GetInfo(assetKey);
			Texture2D texture2D = null;
			if ("0" != info && "0" != text)
			{
				if (!addStr.IsNullOrEmpty())
				{
					string text2 = text + addStr;
					if (!base.hiPoly)
					{
						text2 += "_low";
					}
					texture2D = CommonLib.LoadAsset<Texture2D>(info, text2);
				}
				if (null == texture2D)
				{
					if (!base.hiPoly)
					{
						text += "_low";
					}
					texture2D = CommonLib.LoadAsset<Texture2D>(info, text);
				}
				Character.AddLoadAssetBundle(info, "");
			}
			return texture2D;
		}
		return null;
	}

	public void AddUpdateCMFaceTexFlags(bool inpBase, bool inpSub, bool inpPaint01, bool inpPaint02, bool inpCheek, bool inpLipLine, bool inpMole)
	{
		if (inpBase)
		{
			updateCMFaceTex[0] = inpBase;
		}
		if (inpSub)
		{
			updateCMFaceTex[1] = inpSub;
		}
		if (inpPaint01)
		{
			updateCMFaceTex[2] = inpPaint01;
		}
		if (inpPaint02)
		{
			updateCMFaceTex[3] = inpPaint02;
		}
		if (inpCheek)
		{
			updateCMFaceTex[4] = inpCheek;
		}
		if (inpLipLine)
		{
			updateCMFaceTex[5] = inpLipLine;
		}
		if (inpMole)
		{
			updateCMFaceTex[6] = inpMole;
		}
	}

	public void AddUpdateCMFaceColorFlags(bool inpBase, bool inpSub, bool inpPaint01, bool inpPaint02, bool inpCheek, bool inpLipLine, bool inpMole)
	{
		if (inpBase)
		{
			updateCMFaceColor[0] = inpBase;
		}
		if (inpSub)
		{
			updateCMFaceColor[1] = inpSub;
		}
		if (inpPaint01)
		{
			updateCMFaceColor[2] = inpPaint01;
		}
		if (inpPaint02)
		{
			updateCMFaceColor[3] = inpPaint02;
		}
		if (inpCheek)
		{
			updateCMFaceColor[4] = inpCheek;
		}
		if (inpLipLine)
		{
			updateCMFaceColor[5] = inpLipLine;
		}
		if (inpMole)
		{
			updateCMFaceColor[6] = inpMole;
		}
	}

	public void AddUpdateCMFaceLayoutFlags(bool inpPaint01, bool inpPaint02, bool inpMole)
	{
		if (inpPaint01)
		{
			updateCMFaceLayout[2] = inpPaint01;
		}
		if (inpPaint02)
		{
			updateCMFaceLayout[3] = inpPaint02;
		}
		if (inpMole)
		{
			updateCMFaceLayout[6] = inpMole;
		}
	}

	public bool CreateFaceTexture()
	{
		ChaFileMakeup chaFileMakeup = (nowCoordinate.enableMakeup ? nowCoordinate.makeup : base.fileFace.baseMakeup);
		bool flag = false;
		int num = ((base.sex == 0 && 1 == base.exType) ? 1 : 0);
		if (updateCMFaceTex[0])
		{
			if ((bool)base.objHead)
			{
				string empty = string.Empty;
				string empty2 = string.Empty;
				string empty3 = string.Empty;
				if (0 == 0)
				{
					ListInfoBase listInfoBase = base.infoHead;
					empty = listInfoBase.GetInfo(ChaListDefine.KeyType.MainTexAB);
					empty2 = listInfoBase.GetInfo(ChaListDefine.KeyType.MainTex);
					empty3 = listInfoBase.GetInfo(ChaListDefine.KeyType.MainManifest);
				}
				if ("0" != empty2 && !base.hiPoly)
				{
					empty2 += "_low";
				}
				Texture2D mainTexture = CommonLib.LoadAsset<Texture2D>(empty, empty2, clone: false, empty3);
				Character.AddLoadAssetBundle(empty, "");
				base.customTexCtrlFace.SetMainTexture(mainTexture);
				flag = true;
			}
			updateCMFaceTex[0] = false;
		}
		if (updateCMFaceColor[0])
		{
			base.customTexCtrlFace.SetColor(ChaShader._Color, base.fileBody.skinMainColor);
			flag = true;
			updateCMFaceColor[0] = false;
		}
		if (updateCMFaceTex[1])
		{
			if ((bool)base.objHead)
			{
				string empty4 = string.Empty;
				string empty5 = string.Empty;
				string empty6 = string.Empty;
				if (0 == 0)
				{
					ListInfoBase listInfoBase2 = base.infoHead;
					empty4 = listInfoBase2.GetInfo(ChaListDefine.KeyType.ColorMaskAB);
					empty5 = listInfoBase2.GetInfo(ChaListDefine.KeyType.ColorMaskTex);
					empty6 = listInfoBase2.GetInfo(ChaListDefine.KeyType.MainManifest);
				}
				if ("0" != empty5 && !base.hiPoly)
				{
					empty5 += "_low";
				}
				Texture2D tex = CommonLib.LoadAsset<Texture2D>(empty4, empty5, clone: false, empty6);
				Character.AddLoadAssetBundle(empty4, "");
				base.customTexCtrlFace.SetTexture(ChaShader._ColorMask, tex);
				flag = true;
			}
			updateCMFaceTex[1] = false;
		}
		if (updateCMFaceColor[1])
		{
			base.customTexCtrlFace.SetColor(ChaShader._Color2, base.fileBody.skinSubColor);
			flag = true;
			updateCMFaceColor[1] = false;
		}
		if (num == 0)
		{
			if (updateCMFaceTex[2])
			{
				if (SetCreateTexture(base.customTexCtrlFace, main: false, ChaListDefine.CategoryNo.mt_face_paint, chaFileMakeup.paintId[0], ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.PaintTex, ChaShader._Texture3))
				{
					flag = true;
				}
				updateCMFaceTex[2] = false;
			}
			if (updateCMFaceColor[2])
			{
				base.customTexCtrlFace.SetColor(ChaShader._Color3, chaFileMakeup.paintColor[0]);
				updateCMFaceColor[2] = false;
				flag = true;
			}
			if (updateCMFaceLayout[2])
			{
				Vector4 zero = Vector4.zero;
				zero.x = Mathf.Lerp(0.25f, -0.25f, chaFileMakeup.paintLayout[0].x);
				zero.y = Mathf.Lerp(0.3f, -0.3f, chaFileMakeup.paintLayout[0].y);
				zero.z = Mathf.Lerp(1f, -1f, chaFileMakeup.paintLayout[0].z);
				zero.w = Mathf.Lerp(-8f, 0.7f, chaFileMakeup.paintLayout[0].w);
				base.customTexCtrlFace.SetVector4(ChaShader._paint1, zero);
				updateCMFaceLayout[2] = false;
				flag = true;
			}
			if (updateCMFaceTex[3])
			{
				if (SetCreateTexture(base.customTexCtrlFace, main: false, ChaListDefine.CategoryNo.mt_face_paint, chaFileMakeup.paintId[1], ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.PaintTex, ChaShader._Texture7))
				{
					flag = true;
				}
				updateCMFaceTex[3] = false;
			}
			if (updateCMFaceColor[3])
			{
				base.customTexCtrlFace.SetColor(ChaShader._Color7, chaFileMakeup.paintColor[1]);
				updateCMFaceColor[3] = false;
				flag = true;
			}
			if (updateCMFaceLayout[3])
			{
				Vector4 zero2 = Vector4.zero;
				zero2.x = Mathf.Lerp(0.25f, -0.25f, chaFileMakeup.paintLayout[1].x);
				zero2.y = Mathf.Lerp(0.3f, -0.3f, chaFileMakeup.paintLayout[1].y);
				zero2.z = Mathf.Lerp(1f, -1f, chaFileMakeup.paintLayout[1].z);
				zero2.w = Mathf.Lerp(-8f, 0.7f, chaFileMakeup.paintLayout[1].w);
				base.customTexCtrlFace.SetVector4(ChaShader._paint2, zero2);
				updateCMFaceLayout[3] = false;
				flag = true;
			}
			if (updateCMFaceTex[4])
			{
				if (SetCreateTexture(base.customTexCtrlFace, main: false, ChaListDefine.CategoryNo.mt_cheek, chaFileMakeup.cheekId, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.CheekTex, ChaShader._Texture4))
				{
					flag = true;
				}
				updateCMFaceTex[4] = false;
			}
			if (updateCMFaceColor[4])
			{
				base.customTexCtrlFace.SetColor(ChaShader._Color4, chaFileMakeup.cheekColor);
				updateCMFaceColor[4] = false;
				flag = true;
			}
			if (updateCMFaceTex[5])
			{
				if (SetCreateTexture(base.customTexCtrlFace, main: false, ChaListDefine.CategoryNo.mt_lipline, base.fileFace.lipLineId, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.LiplineTex, ChaShader._Texture5))
				{
					flag = true;
				}
				updateCMFaceTex[5] = false;
			}
			if (updateCMFaceColor[5])
			{
				base.customTexCtrlFace.SetColor(ChaShader._Color5, base.fileFace.lipLineColor);
				updateCMFaceColor[5] = false;
				flag = true;
			}
			if (updateCMFaceTex[6])
			{
				if (SetCreateTexture(base.customTexCtrlFace, main: false, ChaListDefine.CategoryNo.mt_mole, base.fileFace.moleId, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.MoleTex, ChaShader._Texture6))
				{
					flag = true;
				}
				updateCMFaceTex[6] = false;
			}
			if (updateCMFaceColor[6])
			{
				base.customTexCtrlFace.SetColor(ChaShader._Color6, base.fileFace.moleColor);
				updateCMFaceColor[6] = false;
				flag = true;
			}
			if (updateCMFaceLayout[6])
			{
				Vector4 zero3 = Vector4.zero;
				zero3.x = Mathf.Lerp(0.25f, -0.25f, base.fileFace.moleLayout.x);
				zero3.y = Mathf.Lerp(0.3f, -0.3f, base.fileFace.moleLayout.y);
				zero3.w = Mathf.Lerp(0f, 0.7f, base.fileFace.moleLayout.w);
				base.customTexCtrlFace.SetVector4(ChaShader._hokuro, zero3);
				updateCMFaceLayout[6] = false;
				flag = true;
			}
		}
		if (flag)
		{
			base.customTexCtrlFace.SetNewCreateTexture();
		}
		if (base.releaseCustomInputTexture)
		{
			ReleaseFaceCustomTexture();
		}
		return true;
	}

	public bool ChangeSettingWhiteOfEye(bool updateTex, bool updateColor)
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		if (base.ctCreateEyeW == null)
		{
			return false;
		}
		if (!updateTex && !updateColor)
		{
			return false;
		}
		bool result = true;
		if (updateTex && !SetCreateTexture(base.ctCreateEyeW, main: true, ChaListDefine.CategoryNo.mt_eye_white, base.fileFace.whiteId, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.EyeWhiteTex, ChaShader._MainTex))
		{
			result = false;
		}
		if (updateColor)
		{
			base.ctCreateEyeW.SetColor(ChaShader._Color, base.fileFace.whiteBaseColor);
			base.ctCreateEyeW.SetColor(ChaShader._Color2, base.fileFace.whiteSubColor);
		}
		Texture texture = base.ctCreateEyeW.RebuildTextureAndSetMaterial();
		if (null != texture && base.rendEyeW != null && 2 == base.rendEyeW.Length)
		{
			if (null != base.rendEyeW[0] && null != base.rendEyeW[1])
			{
				base.rendEyeW[0].material.SetTexture(ChaShader._MainTex, texture);
				base.rendEyeW[1].material = base.rendEyeW[0].material;
			}
			else
			{
				result = false;
			}
		}
		else
		{
			result = false;
		}
		if (base.releaseCustomInputTexture)
		{
			base.ctCreateEyeW.SetMainTexture(null);
			base.ctCreateEyeW.SetTexture(ChaShader._MainTex, null);
		}
		return result;
	}

	public bool ChangeSettingEyeL(bool updateBaseTex, bool updateMaskTex, bool updateColorAndOffset)
	{
		return ChangeSettingEye(0, updateBaseTex, updateMaskTex, updateColorAndOffset);
	}

	public bool ChangeSettingEyeR(bool updateBaseTex, bool updateMaskTex, bool updateColorAndOffset)
	{
		return ChangeSettingEye(1, updateBaseTex, updateMaskTex, updateColorAndOffset);
	}

	public bool ChangeSettingEye(bool updateBaseTex, bool updateMaskTex, bool updateColorAndOffset)
	{
		return ChangeSettingEye(2, updateBaseTex, updateMaskTex, updateColorAndOffset);
	}

	public bool ChangeSettingEye(byte lr, bool updateBaseTex, bool updateMaskTex, bool updateColorAndOffset)
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		bool result = true;
		bool[] array = new bool[2]
		{
			(1 != lr) ? true : false,
			(lr != 0) ? true : false
		};
		CustomTextureCreate[] array2 = new CustomTextureCreate[2] { base.ctCreateEyeL, base.ctCreateEyeR };
		for (int i = 0; i < 2; i++)
		{
			if (array[i])
			{
				if (array2[i] == null)
				{
					return false;
				}
				if (null == base.rendEye[i])
				{
					return false;
				}
				if (!updateBaseTex && !updateMaskTex && !updateColorAndOffset)
				{
					return false;
				}
				if (updateBaseTex && !SetCreateTexture(array2[i], main: true, ChaListDefine.CategoryNo.mt_eye, base.fileFace.pupil[i].id, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.EyeTex, ChaShader._MainTex))
				{
					result = false;
				}
				if (updateMaskTex && !SetCreateTexture(array2[i], main: false, ChaListDefine.CategoryNo.mt_eye_gradation, base.fileFace.pupil[i].gradMaskId, ChaListDefine.KeyType.ColorMaskAB, ChaListDefine.KeyType.ColorMaskTex, ChaShader._ColorMask))
				{
					result = false;
				}
				if (updateColorAndOffset)
				{
					array2[i].SetColor(ChaShader._Color, base.fileFace.pupil[i].baseColor);
					array2[i].SetColor(ChaShader._Color2, base.fileFace.pupil[i].subColor);
					array2[i].SetFloat(ChaShader._Blend, base.fileFace.pupil[i].gradBlend);
					float y = Mathf.Lerp(-0.5f, 0.5f, base.fileFace.pupil[i].gradOffsetY);
					float w = Mathf.Lerp(-1f, 1f, base.fileFace.pupil[i].gradScale);
					array2[i].SetVector4(ChaShader._grad, new Vector4(0f, y, 0f, w));
				}
				Texture texture = array2[i].RebuildTextureAndSetMaterial();
				if (null != texture)
				{
					base.rendEye[i].material.SetTexture(ChaShader._MainTex, texture);
				}
				if (base.releaseCustomInputTexture)
				{
					array2[i].SetMainTexture(null);
					array2[i].SetTexture(ChaShader._MainTex, null);
					array2[i].SetTexture(ChaShader._ColorMask, null);
				}
			}
		}
		return result;
	}

	public bool ChangeSettingEyeHLUpPosX()
	{
		float eyeTexHLUpOffsetX = Mathf.Lerp(0.2f, -0.2f, base.fileFace.hlUpX);
		for (int i = 0; i < base.fileFace.pupil.Length; i++)
		{
			if (!(null == base.eyeLookMatCtrl[i]))
			{
				base.eyeLookMatCtrl[i].SetEyeTexHLUpOffsetX(eyeTexHLUpOffsetX);
			}
		}
		return true;
	}

	public bool ChangeSettingEyeHLUpPosY()
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		float eyeTexHLUpOffsetY = Mathf.Lerp(0.1f, -0.1f, base.fileFace.hlUpY);
		for (int i = 0; i < base.fileFace.pupil.Length; i++)
		{
			if (!(null == base.eyeLookMatCtrl[i]))
			{
				base.eyeLookMatCtrl[i].SetEyeTexHLUpOffsetY(eyeTexHLUpOffsetY);
			}
		}
		return true;
	}

	public bool ChangeSettingEyeHLDownPosX()
	{
		float eyeTexHLDownOffsetX = Mathf.Lerp(0.2f, -0.2f, base.fileFace.hlDownX);
		for (int i = 0; i < base.fileFace.pupil.Length; i++)
		{
			if (!(null == base.eyeLookMatCtrl[i]))
			{
				base.eyeLookMatCtrl[i].SetEyeTexHLDownOffsetX(eyeTexHLDownOffsetX);
			}
		}
		return true;
	}

	public bool ChangeSettingEyeHLDownPosY()
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		float eyeTexHLDownOffsetY = Mathf.Lerp(0.1f, -0.1f, base.fileFace.hlDownY);
		for (int i = 0; i < base.fileFace.pupil.Length; i++)
		{
			if (!(null == base.eyeLookMatCtrl[i]))
			{
				base.eyeLookMatCtrl[i].SetEyeTexHLDownOffsetY(eyeTexHLDownOffsetY);
			}
		}
		return true;
	}

	public bool ChangeSettingEyePosX()
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		float eyeTexOffsetX = Mathf.Lerp(0.2f, -0.6f, base.fileFace.pupilX);
		for (int i = 0; i < base.fileFace.pupil.Length; i++)
		{
			if (!(null == base.eyeLookMatCtrl[i]))
			{
				base.eyeLookMatCtrl[i].SetEyeTexOffsetX(eyeTexOffsetX);
			}
		}
		return true;
	}

	public bool ChangeSettingEyePosY()
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		float eyeTexOffsetY = Mathf.Lerp(-0.5f, 0.5f, base.fileFace.pupilY);
		for (int i = 0; i < base.fileFace.pupil.Length; i++)
		{
			if (!(null == base.eyeLookMatCtrl[i]))
			{
				base.eyeLookMatCtrl[i].SetEyeTexOffsetY(eyeTexOffsetY);
			}
		}
		return true;
	}

	public bool ChangeSettingEyeScaleWidth()
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		float eyeTexScaleX = Mathf.Lerp(1.8f, -0.2f, base.fileFace.pupilWidth);
		for (int i = 0; i < base.fileFace.pupil.Length; i++)
		{
			if (!(null == base.eyeLookMatCtrl[i]))
			{
				base.eyeLookMatCtrl[i].SetEyeTexScaleX(eyeTexScaleX);
			}
		}
		return true;
	}

	public bool ChangeSettingEyeScaleHeight()
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		float eyeTexScaleY = Mathf.Lerp(1.8f, -0.2f, base.fileFace.pupilHeight);
		for (int i = 0; i < base.fileFace.pupil.Length; i++)
		{
			if (!(null == base.eyeLookMatCtrl[i]))
			{
				base.eyeLookMatCtrl[i].SetEyeTexScaleY(eyeTexScaleY);
			}
		}
		return true;
	}

	public bool ChangeSettingEyeTilt()
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		if (base.rendEye == null)
		{
			return false;
		}
		int num = 33;
		float[] array = new float[2]
		{
			Mathf.Lerp(0.02f, -0.02f, base.fileFace.shapeValueFace[num]),
			Mathf.Lerp(-0.02f, 0.02f, base.fileFace.shapeValueFace[num])
		};
		for (int i = 0; i < base.rendEye.Length; i++)
		{
			if (!(null == base.rendEye[i]))
			{
				base.rendEye[i].material.SetFloat(ChaShader._rotation, array[i]);
			}
		}
		return true;
	}

	public bool ChangeSettingEyeHiUp()
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		if (base.rendEye == null)
		{
			return false;
		}
		for (int i = 0; i < 2; i++)
		{
			if (!(null == base.rendEye[i]))
			{
				ChangeTexture(base.rendEye[i], ChaListDefine.CategoryNo.mt_eye_hi_up, base.fileFace.hlUpId, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.EyeHiUpTex, ChaShader._overtex1);
			}
		}
		return true;
	}

	public bool ChangeSettingEyeHiUpColor()
	{
		if (base.rendEye == null)
		{
			return false;
		}
		for (int i = 0; i < 2; i++)
		{
			if (!(null == base.rendEye[i]))
			{
				base.rendEye[i].material.SetColor(ChaShader._overcolor1, base.fileFace.hlUpColor);
			}
		}
		return true;
	}

	public bool ChangeSettingEyeHiDown()
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		if (base.rendEye == null)
		{
			return false;
		}
		for (int i = 0; i < 2; i++)
		{
			if (!(null == base.rendEye[i]))
			{
				ChangeTexture(base.rendEye[i], ChaListDefine.CategoryNo.mt_eye_hi_down, base.fileFace.hlDownId, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.EyeHiDownTex, ChaShader._overtex2);
			}
		}
		return true;
	}

	public bool ChangeSettingEyeHiDownColor()
	{
		if (base.rendEye == null)
		{
			return false;
		}
		for (int i = 0; i < 2; i++)
		{
			if (!(null == base.rendEye[i]))
			{
				base.rendEye[i].material.SetColor(ChaShader._overcolor2, base.fileFace.hlDownColor);
			}
		}
		return true;
	}

	public bool ChangeSettingEyelineUp()
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		if (null == base.rendEyelineUp)
		{
			return false;
		}
		if (2 > base.rendEyelineUp.materials.Length)
		{
			return false;
		}
		string addStr = "";
		if (base.fileFace.headId != 0)
		{
			addStr = $"_{base.fileFace.headId}";
		}
		ChangeTexture(base.rendEyelineUp.materials[0], ChaListDefine.CategoryNo.mt_eyeline_up, base.fileFace.eyelineUpId, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.EyelineUpTex, ChaShader._MainTex, addStr);
		ChangeTexture(base.rendEyelineUp.materials[1], ChaListDefine.CategoryNo.mt_eyeline_up, base.fileFace.eyelineUpId, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.EyelineShadowTex, ChaShader._MainTex, addStr);
		return true;
	}

	public bool ChangeSettingEyelineColor()
	{
		if (null != base.rendEyelineUp)
		{
			base.rendEyelineUp.materials[0].SetColor(ChaShader._Color, base.fileFace.eyelineColor);
		}
		if (null != base.rendEyelineDown)
		{
			base.rendEyelineDown.material.SetColor(ChaShader._Color, base.fileFace.eyelineColor);
		}
		return true;
	}

	public bool UpdateEyelineShadowColor()
	{
		if (null == base.rendEyelineUp)
		{
			return false;
		}
		if (2 > base.rendEyelineUp.materials.Length)
		{
			return false;
		}
		base.rendEyelineUp.materials[1].SetColor(ChaShader._Color, base.fileBody.skinMainColor);
		return true;
	}

	public bool ChangeSettingEyelineDown()
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		if (null == base.rendEyelineDown)
		{
			return false;
		}
		string addStr = "";
		if (base.fileFace.headId != 0)
		{
			addStr = $"_{base.fileFace.headId}";
		}
		ChangeTexture(base.rendEyelineDown, ChaListDefine.CategoryNo.mt_eyeline_down, base.fileFace.eyelineDownId, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.EyelineDownTex, ChaShader._MainTex, addStr);
		return true;
	}

	public bool ChangeSettingEyebrow()
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		ChangeTexture(base.rendEyebrow, ChaListDefine.CategoryNo.mt_eyebrow, base.fileFace.eyebrowId, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.EyebrowTex, ChaShader._MainTex);
		return true;
	}

	public bool ChangeSettingEyebrowColor()
	{
		if (null == base.rendEyebrow)
		{
			return false;
		}
		base.rendEyebrow.material.SetColor(ChaShader._Color, base.fileFace.eyebrowColor);
		return true;
	}

	public bool ChangeSettingNose()
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		ChangeTexture(base.rendNose, ChaListDefine.CategoryNo.mt_nose, base.fileFace.noseId, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.NoseTex, ChaShader._MainTex);
		return true;
	}

	public bool VisibleDoubleTooth()
	{
		GameObject referenceInfo = GetReferenceInfo(RefObjKey.ObjDoubleTooth);
		if ((bool)referenceInfo)
		{
			referenceInfo.SetActiveIfDifferent(base.fileFace.doubleTooth);
		}
		return true;
	}

	public bool ChangeSettingFaceDetail()
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		if (null == base.customTexCtrlFace.matDraw)
		{
			return false;
		}
		ChangeTexture(base.customTexCtrlFace.matDraw, ChaListDefine.CategoryNo.mt_face_detail, base.fileFace.detailId, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.NormallMapDetail, ChaShader._NormalMapDetail);
		ChangeTexture(base.customTexCtrlFace.matDraw, ChaListDefine.CategoryNo.mt_face_detail, base.fileFace.detailId, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.LineMask, ChaShader._LineMask);
		return true;
	}

	public bool ChangeSettingFaceDetailPower()
	{
		if (null == base.customTexCtrlFace.matDraw)
		{
			return false;
		}
		base.customTexCtrlFace.matDraw.SetFloat(ChaShader._DetailNormalMapScale, base.fileFace.detailPower);
		return true;
	}

	public bool ChangeSettingEyeShadow()
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		ChaFileMakeup chaFileMakeup = (nowCoordinate.enableMakeup ? nowCoordinate.makeup : base.fileFace.baseMakeup);
		if (null == base.customTexCtrlFace.matDraw)
		{
			return false;
		}
		ChangeTexture(base.customTexCtrlFace.matDraw, ChaListDefine.CategoryNo.mt_eyeshadow, chaFileMakeup.eyeshadowId, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.EyeshadowTex, ChaShader._overtex3);
		return true;
	}

	public bool ChangeSettingEyeShadowColor()
	{
		ChaFileMakeup chaFileMakeup = (nowCoordinate.enableMakeup ? nowCoordinate.makeup : base.fileFace.baseMakeup);
		if (base.customTexCtrlFace == null || null == base.customTexCtrlFace.matDraw)
		{
			return false;
		}
		if (IsGagEyes())
		{
			Color eyeshadowColor = chaFileMakeup.eyeshadowColor;
			eyeshadowColor.a = 0f;
			base.customTexCtrlFace.matDraw.SetColor(ChaShader._overcolor3, eyeshadowColor);
		}
		else
		{
			base.customTexCtrlFace.matDraw.SetColor(ChaShader._overcolor3, chaFileMakeup.eyeshadowColor);
		}
		return true;
	}

	public bool ChangeSettingLip()
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		ChaFileMakeup chaFileMakeup = (nowCoordinate.enableMakeup ? nowCoordinate.makeup : base.fileFace.baseMakeup);
		if (null == base.customTexCtrlFace.matDraw)
		{
			return false;
		}
		ChangeTexture(base.customTexCtrlFace.matDraw, ChaListDefine.CategoryNo.mt_lip, chaFileMakeup.lipId, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.LipTex, ChaShader._overtex1);
		return true;
	}

	public bool ChangeSettingLipColor()
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		ChaFileMakeup chaFileMakeup = (nowCoordinate.enableMakeup ? nowCoordinate.makeup : base.fileFace.baseMakeup);
		if (null == base.customTexCtrlFace.matDraw)
		{
			return false;
		}
		base.customTexCtrlFace.matDraw.SetColor(ChaShader._overcolor1, chaFileMakeup.lipColor);
		return true;
	}

	public bool ChangeSettingCheekGlossPower()
	{
		if (null == base.customTexCtrlFace.matDraw)
		{
			return false;
		}
		base.customTexCtrlFace.matDraw.SetFloat(ChaShader._SpecularPower, base.fileFace.cheekGlossPower);
		return true;
	}

	public bool ChangeSettingLipGlossPower()
	{
		if (null == base.customTexCtrlFace.matDraw)
		{
			return false;
		}
		base.customTexCtrlFace.matDraw.SetFloat(ChaShader._SpecularPowerNail, base.fileFace.lipGlossPower);
		return true;
	}

	public bool SetFaceBaseMaterial()
	{
		if (null == base.customMatFace)
		{
			return false;
		}
		return SetBaseMaterial(base.rendFace, base.customMatFace);
	}

	public bool ReleaseFaceCustomTexture()
	{
		if (base.customTexCtrlFace == null)
		{
			return false;
		}
		base.customTexCtrlFace.SetTexture(ChaShader._MainTex, null);
		base.customTexCtrlFace.SetTexture(ChaShader._ColorMask, null);
		base.customTexCtrlFace.SetTexture(ChaShader._Texture3, null);
		base.customTexCtrlFace.SetTexture(ChaShader._Texture4, null);
		base.customTexCtrlFace.SetTexture(ChaShader._Texture5, null);
		base.customTexCtrlFace.SetTexture(ChaShader._Texture6, null);
		base.customTexCtrlFace.SetTexture(ChaShader._Texture7, null);
		if (Character.enableCharaLoadGCClear)
		{
			Resources.UnloadUnusedAssets();
		}
		return true;
	}

	public void ChangeCustomFaceWithoutCustomTexture()
	{
		ChangeSettingEyebrow();
		ChangeSettingEyebrowColor();
		ChangeSettingEyeHiUp();
		ChangeSettingEyeHiUpColor();
		ChangeSettingEyeHiDown();
		ChangeSettingEyeHiDownColor();
		ChangeSettingEyeHLUpPosX();
		ChangeSettingEyeHLUpPosY();
		ChangeSettingEyeHLDownPosX();
		ChangeSettingEyeHLDownPosY();
		ChangeSettingEyePosX();
		ChangeSettingEyePosY();
		ChangeSettingEyeScaleWidth();
		ChangeSettingEyeScaleHeight();
		ChangeSettingEyeTilt();
		ChangeSettingNose();
		ChangeSettingEyelineUp();
		ChangeSettingEyelineDown();
		ChangeSettingEyelineColor();
		UpdateEyelineShadowColor();
		VisibleDoubleTooth();
		ChangeSettingFaceDetail();
		ChangeSettingFaceDetailPower();
		ChangeSettingLip();
		ChangeSettingLipColor();
		ChangeSettingEyeShadow();
		ChangeSettingEyeShadowColor();
		ChangeSettingCheekGlossPower();
		ChangeSettingLipGlossPower();
	}

	public void ChangeSettingHairColor(int parts, bool c00, bool c01, bool c02)
	{
		ChaCustomHairComponent customHairComponent = GetCustomHairComponent(parts);
		if (null == customHairComponent || customHairComponent.rendHair == null || customHairComponent.rendHair.Length == 0)
		{
			return;
		}
		ChaFileHair hair = base.chaFile.custom.hair;
		for (int i = 0; i < customHairComponent.rendHair.Length; i++)
		{
			if (c00)
			{
				if (1f > hair.parts[parts].baseColor.a)
				{
					hair.parts[parts].baseColor = new Color(hair.parts[parts].baseColor.r, hair.parts[parts].baseColor.g, hair.parts[parts].baseColor.b, 1f);
				}
				customHairComponent.rendHair[i].material.SetColor(ChaShader._Color, hair.parts[parts].baseColor);
			}
			if (c01)
			{
				if (1f > hair.parts[parts].startColor.a)
				{
					hair.parts[parts].startColor = new Color(hair.parts[parts].startColor.r, hair.parts[parts].startColor.g, hair.parts[parts].startColor.b, 1f);
				}
				customHairComponent.rendHair[i].material.SetColor(ChaShader._Color2, hair.parts[parts].startColor);
			}
			if (c02)
			{
				if (1f > hair.parts[parts].endColor.a)
				{
					hair.parts[parts].endColor = new Color(hair.parts[parts].endColor.r, hair.parts[parts].endColor.g, hair.parts[parts].endColor.b, 1f);
				}
				customHairComponent.rendHair[i].material.SetColor(ChaShader._Color3, hair.parts[parts].endColor);
			}
		}
	}

	public void ChangeSettingHairOutlineColor(int parts)
	{
		ChaCustomHairComponent customHairComponent = GetCustomHairComponent(parts);
		if (!(null == customHairComponent) && customHairComponent.rendHair != null && customHairComponent.rendHair.Length != 0)
		{
			ChaFileHair hair = base.chaFile.custom.hair;
			for (int i = 0; i < customHairComponent.rendHair.Length; i++)
			{
				customHairComponent.rendHair[i].material.SetColor(ChaShader._LineColor, hair.parts[parts].outlineColor);
			}
		}
	}

	public void ChangeSettingHairGlossColor(int parts)
	{
		ChaCustomHairComponent customHairComponent = GetCustomHairComponent(parts);
		if (!(null == customHairComponent) && customHairComponent.rendHair != null && customHairComponent.rendHair.Length != 0)
		{
			ChaFileHair hair = base.chaFile.custom.hair;
			for (int i = 0; i < customHairComponent.rendHair.Length; i++)
			{
				customHairComponent.rendHair[i].material.SetColor(ChaShader._GlossColor, hair.parts[parts].glossColor);
			}
		}
	}

	public int GetHairAcsColorNum(int parts)
	{
		ChaCustomHairComponent customHairComponent = GetCustomHairComponent(parts);
		if (null == customHairComponent || customHairComponent.acsDefColor == null || customHairComponent.acsDefColor.Length == 0)
		{
			return 0;
		}
		return customHairComponent.acsDefColor.Length;
	}

	public void SetAcsDefaultColorParameterOnly(int parts)
	{
		ChaFileHair hair = base.chaFile.custom.hair;
		ChaCustomHairComponent customHairComponent = GetCustomHairComponent(parts);
		if (!(null == customHairComponent))
		{
			int num = customHairComponent.acsDefColor.Length;
			for (int i = 0; i < num; i++)
			{
				hair.parts[parts].acsColor[i] = customHairComponent.acsDefColor[i];
			}
		}
	}

	public void ChangeSettingHairAcsColor(int parts)
	{
		int hairAcsColorNum = GetHairAcsColorNum(parts);
		if (hairAcsColorNum == 0)
		{
			return;
		}
		ChaCustomHairComponent customHairComponent = GetCustomHairComponent(parts);
		if (null == customHairComponent)
		{
			return;
		}
		int[] array = new int[3]
		{
			ChaShader._Color,
			ChaShader._Color2,
			ChaShader._Color3
		};
		ChaFileHair hair = base.chaFile.custom.hair;
		for (int i = 0; i < customHairComponent.rendAccessory.Length; i++)
		{
			for (int j = 0; j < hairAcsColorNum; j++)
			{
				if (1f > hair.parts[parts].acsColor[j].a)
				{
					hair.parts[parts].acsColor[j] = new Color(hair.parts[parts].acsColor[j].r, hair.parts[parts].acsColor[j].g, hair.parts[parts].acsColor[j].b, 1f);
				}
				customHairComponent.rendAccessory[i].material.SetColor(array[j], hair.parts[parts].acsColor[j]);
			}
		}
	}

	public void ChangeSettingHairLength(int parts)
	{
		ChaCustomHairComponent customHairComponent = GetCustomHairComponent(parts);
		if (!(null == customHairComponent))
		{
			ChaFileHair hair = base.chaFile.custom.hair;
			customHairComponent.lengthRate = hair.parts[parts].length;
		}
	}

	public bool ChangeSettingHairFrontLength()
	{
		int num = 1;
		ChaCustomHairComponent customHairComponent = GetCustomHairComponent(num);
		if (null == customHairComponent)
		{
			return false;
		}
		ChaFileHair hair = base.chaFile.custom.hair;
		customHairComponent.lengthRate = hair.parts[num].length;
		return true;
	}

	public void LoadHairGlossMask()
	{
		ChaFileHair hair = base.chaFile.custom.hair;
		texHairGloss = GetTexture(ChaListDefine.CategoryNo.mt_hairgloss, hair.glossId, ChaListDefine.KeyType.MainTexAB, ChaListDefine.KeyType.MainTex);
	}

	public bool ChangeSettingHairGlossMaskAll()
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		int[] array = (int[])Enum.GetValues(typeof(ChaFileDefine.HairKind));
		foreach (int parts in array)
		{
			ChangeSettingHairGlossMask(parts);
		}
		return true;
	}

	public void ChangeSettingHairGlossMask(int parts)
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return;
		}
		ChaCustomHairComponent customHairComponent = GetCustomHairComponent(parts);
		if (null == customHairComponent || null == customHairComponent || customHairComponent.rendHair == null || customHairComponent.rendHair.Length == 0)
		{
			return;
		}
		for (int i = 0; i < customHairComponent.rendHair.Length; i++)
		{
			if (!(null == customHairComponent.rendHair[i]))
			{
				customHairComponent.rendHair[i].material.SetTexture(ChaShader._HairGloss, texHairGloss);
			}
		}
	}

	public void ChangeShakeHair(int parts)
	{
		if ((bool)base.objHair[parts])
		{
			DynamicBone[] componentsInChildren = base.objHair[parts].GetComponentsInChildren<DynamicBone>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].enabled = !base.fileHair.parts[parts].noShake;
			}
		}
	}

	public void AddUpdateCMBodyTexFlags(bool inpBase, bool inpSub, bool inpPaint01, bool inpPaint02, bool inpSunburn)
	{
		if (inpBase)
		{
			updateCMBodyTex[0] = inpBase;
		}
		if (inpSub)
		{
			updateCMBodyTex[1] = inpSub;
		}
		if (inpPaint01)
		{
			updateCMBodyTex[2] = inpPaint01;
		}
		if (inpPaint02)
		{
			updateCMBodyTex[3] = inpPaint02;
		}
		if (inpSunburn)
		{
			updateCMBodyTex[4] = inpSunburn;
		}
	}

	public void AddUpdateCMBodyColorFlags(bool inpBase, bool inpSub, bool inpPaint01, bool inpPaint02, bool inpSunburn, bool inpNail)
	{
		if (inpBase)
		{
			updateCMBodyColor[0] = inpBase;
		}
		if (inpSub)
		{
			updateCMBodyColor[1] = inpSub;
		}
		if (inpPaint01)
		{
			updateCMBodyColor[2] = inpPaint01;
		}
		if (inpPaint02)
		{
			updateCMBodyColor[3] = inpPaint02;
		}
		if (inpSunburn)
		{
			updateCMBodyColor[4] = inpSunburn;
		}
		if (inpNail)
		{
			updateCMBodyColor[5] = inpNail;
		}
	}

	public void AddUpdateCMBodyLayoutFlags(bool inpPaint01, bool inpPaint02)
	{
		if (inpPaint01)
		{
			updateCMBodyLayout[2] = inpPaint01;
		}
		if (inpPaint02)
		{
			updateCMBodyLayout[3] = inpPaint02;
		}
	}

	public bool CreateBodyTexture()
	{
		ChaFileBody body = base.chaFile.custom.body;
		bool flag = false;
		int num = ((base.sex == 0 && 1 == base.exType) ? 1 : 0);
		if (updateCMBodyTex[0])
		{
			string manifestName = "abdata";
			string text = "cf_body_00_t";
			if (!base.hiPoly)
			{
				text += "_low";
			}
			Texture2D mainTexture = CommonLib.LoadAsset<Texture2D>("chara/oo_base.unity3d", text, clone: false, manifestName);
			Character.AddLoadAssetBundle("chara/oo_base.unity3d", manifestName);
			base.customTexCtrlBody.SetMainTexture(mainTexture);
			flag = true;
			updateCMBodyTex[0] = false;
		}
		if (updateCMBodyColor[0])
		{
			base.customTexCtrlBody.SetColor(ChaShader._Color, body.skinMainColor);
			flag = true;
			updateCMBodyColor[0] = false;
		}
		if (updateCMBodyTex[1])
		{
			string manifestName2 = "abdata";
			string text2 = ((base.sex == 0) ? "cm_body_00_mc" : "cf_body_00_mc");
			if (!base.hiPoly)
			{
				text2 += "_low";
			}
			Texture2D tex = CommonLib.LoadAsset<Texture2D>("chara/oo_base.unity3d", text2, clone: false, manifestName2);
			Character.AddLoadAssetBundle("chara/oo_base.unity3d", manifestName2);
			base.customTexCtrlBody.SetTexture(ChaShader._ColorMask, tex);
			flag = true;
			updateCMBodyTex[1] = false;
		}
		if (updateCMBodyColor[1])
		{
			base.customTexCtrlBody.SetColor(ChaShader._Color2, body.skinSubColor);
			flag = true;
			updateCMBodyColor[1] = false;
		}
		if (num == 0)
		{
			if (updateCMBodyTex[2])
			{
				if (SetCreateTexture(base.customTexCtrlBody, main: false, ChaListDefine.CategoryNo.mt_body_paint, body.paintId[0], ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.PaintTex, ChaShader._Texture3))
				{
					flag = true;
				}
				updateCMBodyTex[2] = false;
			}
			if (updateCMBodyColor[2])
			{
				base.customTexCtrlBody.SetColor(ChaShader._Color3, body.paintColor[0]);
				updateCMBodyColor[2] = false;
				flag = true;
			}
			if (updateCMBodyLayout[2])
			{
				ListInfoBase listInfo = base.lstCtrl.GetListInfo(ChaListDefine.CategoryNo.bodypaint_layout, body.paintLayoutId[0]);
				if (listInfo != null)
				{
					float b = listInfo.GetInfoFloat(ChaListDefine.KeyType.CenterX) - listInfo.GetInfoFloat(ChaListDefine.KeyType.MoveX);
					float a = listInfo.GetInfoFloat(ChaListDefine.KeyType.CenterX) + listInfo.GetInfoFloat(ChaListDefine.KeyType.MoveX);
					float b2 = listInfo.GetInfoFloat(ChaListDefine.KeyType.CenterY) - listInfo.GetInfoFloat(ChaListDefine.KeyType.MoveY);
					float a2 = listInfo.GetInfoFloat(ChaListDefine.KeyType.CenterY) + listInfo.GetInfoFloat(ChaListDefine.KeyType.MoveY);
					float a3 = listInfo.GetInfoFloat(ChaListDefine.KeyType.CenterScale) - listInfo.GetInfoFloat(ChaListDefine.KeyType.AddScale);
					float b3 = listInfo.GetInfoFloat(ChaListDefine.KeyType.CenterScale) + listInfo.GetInfoFloat(ChaListDefine.KeyType.AddScale);
					Vector4 value = default(Vector4);
					value.x = Mathf.Lerp(a, b, body.paintLayout[0].x);
					value.y = Mathf.Lerp(a2, b2, body.paintLayout[0].y);
					value.z = Mathf.Lerp(-1f, 1f, body.paintLayout[0].z);
					value.w = Mathf.Lerp(a3, b3, body.paintLayout[0].w);
					base.customTexCtrlBody.SetVector4(ChaShader._paint1, value);
					updateCMBodyLayout[2] = false;
					flag = true;
				}
			}
			if (updateCMBodyTex[3])
			{
				if (SetCreateTexture(base.customTexCtrlBody, main: false, ChaListDefine.CategoryNo.mt_body_paint, body.paintId[1], ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.PaintTex, ChaShader._Texture6))
				{
					flag = true;
				}
				updateCMBodyTex[3] = false;
			}
			if (updateCMBodyColor[3])
			{
				base.customTexCtrlBody.SetColor(ChaShader._Color6, body.paintColor[1]);
				updateCMBodyColor[3] = false;
				flag = true;
			}
			if (updateCMBodyLayout[3])
			{
				ListInfoBase listInfo2 = base.lstCtrl.GetListInfo(ChaListDefine.CategoryNo.bodypaint_layout, body.paintLayoutId[1]);
				if (listInfo2 != null)
				{
					float b4 = listInfo2.GetInfoFloat(ChaListDefine.KeyType.CenterX) - listInfo2.GetInfoFloat(ChaListDefine.KeyType.MoveX);
					float a4 = listInfo2.GetInfoFloat(ChaListDefine.KeyType.CenterX) + listInfo2.GetInfoFloat(ChaListDefine.KeyType.MoveX);
					float b5 = listInfo2.GetInfoFloat(ChaListDefine.KeyType.CenterY) - listInfo2.GetInfoFloat(ChaListDefine.KeyType.MoveY);
					float a5 = listInfo2.GetInfoFloat(ChaListDefine.KeyType.CenterY) + listInfo2.GetInfoFloat(ChaListDefine.KeyType.MoveY);
					float a6 = listInfo2.GetInfoFloat(ChaListDefine.KeyType.CenterScale) - listInfo2.GetInfoFloat(ChaListDefine.KeyType.AddScale);
					float b6 = listInfo2.GetInfoFloat(ChaListDefine.KeyType.CenterScale) + listInfo2.GetInfoFloat(ChaListDefine.KeyType.AddScale);
					Vector4 value2 = default(Vector4);
					value2.x = Mathf.Lerp(a4, b4, body.paintLayout[1].x);
					value2.y = Mathf.Lerp(a5, b5, body.paintLayout[1].y);
					value2.z = Mathf.Lerp(-1f, 1f, body.paintLayout[1].z);
					value2.w = Mathf.Lerp(a6, b6, body.paintLayout[1].w);
					base.customTexCtrlBody.SetVector4(ChaShader._paint2, value2);
					updateCMBodyLayout[3] = false;
					flag = true;
				}
			}
			if (updateCMBodyTex[4])
			{
				if (SetCreateTexture(base.customTexCtrlBody, main: false, ChaListDefine.CategoryNo.mt_sunburn, body.sunburnId, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.SunburnTex, ChaShader._Texture4))
				{
					flag = true;
				}
				updateCMBodyTex[4] = false;
			}
			if (updateCMBodyColor[4])
			{
				base.customTexCtrlBody.SetColor(ChaShader._Color4, body.sunburnColor);
				updateCMBodyColor[4] = false;
				flag = true;
			}
			if (updateCMBodyColor[5])
			{
				base.customTexCtrlBody.SetColor(ChaShader._Color5, body.nailColor);
				updateCMBodyColor[5] = false;
				flag = true;
			}
		}
		if (flag)
		{
			base.customTexCtrlBody.SetNewCreateTexture();
		}
		if (base.releaseCustomInputTexture)
		{
			ReleaseBodyCustomTexture();
		}
		return true;
	}

	public bool ChangeSettingNip()
	{
		ChaFileBody body = base.chaFile.custom.body;
		ChangeTexture(base.customTexCtrlBody.matDraw, ChaListDefine.CategoryNo.mt_nip, body.nipId, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.NipTex, ChaShader._overtex1);
		return true;
	}

	public bool ChangeSettingAreolaSize()
	{
		ChaFileBody body = base.chaFile.custom.body;
		if (null == base.customTexCtrlBody.matDraw)
		{
			return false;
		}
		base.customTexCtrlBody.matDraw.SetFloat(ChaShader._nipsize, body.areolaSize);
		return true;
	}

	public bool ChangeSettingNipColor()
	{
		ChaFileBody body = base.chaFile.custom.body;
		if (null == base.customTexCtrlBody.matDraw)
		{
			return false;
		}
		base.customTexCtrlBody.matDraw.SetColor(ChaShader._overcolor1, body.nipColor);
		return true;
	}

	public bool ChangeSettingNipGlossPower()
	{
		ChaFileBody body = base.chaFile.custom.body;
		if (null == base.customTexCtrlBody.matDraw)
		{
			return false;
		}
		base.customTexCtrlBody.matDraw.SetFloat(ChaShader._nip_specular, body.nipGlossPower);
		return true;
	}

	public bool ChangeSettingUnderhair()
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		ChaFileBody body = base.chaFile.custom.body;
		if (null == base.customTexCtrlBody.matDraw)
		{
			return false;
		}
		ChangeTexture(base.customTexCtrlBody.matDraw, ChaListDefine.CategoryNo.mt_underhair, body.underhairId, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.UnderhairTex, ChaShader._overtex2);
		return true;
	}

	public bool ChangeSettingUnderhairColor()
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		ChaFileBody body = base.chaFile.custom.body;
		if (null == base.customTexCtrlBody.matDraw)
		{
			return false;
		}
		base.customTexCtrlBody.matDraw.SetColor(ChaShader._overcolor2, body.underhairColor);
		return true;
	}

	public bool VisibleAddBodyLine()
	{
		ChaFileBody body = base.chaFile.custom.body;
		if (null == base.customTexCtrlBody.matDraw)
		{
			return false;
		}
		base.customTexCtrlBody.matDraw.SetFloat(ChaShader._linetexon, body.drawAddLine ? 1f : 0f);
		return true;
	}

	public bool ChangeSettingBodyDetail()
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		ChaFileBody body = base.chaFile.custom.body;
		if (null == base.customTexCtrlBody.matDraw)
		{
			return false;
		}
		ChangeTexture(base.customTexCtrlBody.matDraw, ChaListDefine.CategoryNo.mt_body_detail, body.detailId, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.NormallMapDetail, ChaShader._NormalMapDetail);
		ChangeTexture(base.customTexCtrlBody.matDraw, ChaListDefine.CategoryNo.mt_body_detail, body.detailId, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.LineMask, ChaShader._LineMask);
		return true;
	}

	public bool ChangeSettingBodyDetailPower()
	{
		ChaFileBody body = base.chaFile.custom.body;
		if (null == base.customTexCtrlBody.matDraw)
		{
			return false;
		}
		base.customTexCtrlBody.matDraw.SetFloat(ChaShader._DetailNormalMapScale, body.detailPower);
		return true;
	}

	public bool ChangeSettingSkinGlossPower()
	{
		ChaFileBody body = base.chaFile.custom.body;
		if (null == base.customTexCtrlBody.matDraw)
		{
			return false;
		}
		float value = Mathf.Lerp(body.skinGlossPower, 1f, base.chaFile.status.skinTuyaRate);
		base.customTexCtrlBody.matDraw.SetFloat(ChaShader._SpecularPower, value);
		return true;
	}

	public bool ChangeSettingNailGlossPower()
	{
		ChaFileBody body = base.chaFile.custom.body;
		if (null == base.customTexCtrlBody.matDraw)
		{
			return false;
		}
		base.customTexCtrlBody.matDraw.SetFloat(ChaShader._SpecularPowerNail, body.nailGlossPower);
		return true;
	}

	public Vector4 GetLayoutInfo(int id)
	{
		Vector4 zero = Vector4.zero;
		ListInfoBase listInfo = base.lstCtrl.GetListInfo(ChaListDefine.CategoryNo.bodypaint_layout, id);
		if (listInfo != null)
		{
			float a = listInfo.GetInfoFloat(ChaListDefine.KeyType.CenterX) - listInfo.GetInfoFloat(ChaListDefine.KeyType.MoveX);
			float b = listInfo.GetInfoFloat(ChaListDefine.KeyType.CenterX) + listInfo.GetInfoFloat(ChaListDefine.KeyType.MoveX);
			float a2 = listInfo.GetInfoFloat(ChaListDefine.KeyType.CenterY) - listInfo.GetInfoFloat(ChaListDefine.KeyType.MoveY);
			float b2 = listInfo.GetInfoFloat(ChaListDefine.KeyType.CenterY) + listInfo.GetInfoFloat(ChaListDefine.KeyType.MoveY);
			float a3 = listInfo.GetInfoFloat(ChaListDefine.KeyType.CenterScale) - listInfo.GetInfoFloat(ChaListDefine.KeyType.AddScale);
			float b3 = listInfo.GetInfoFloat(ChaListDefine.KeyType.CenterScale) + listInfo.GetInfoFloat(ChaListDefine.KeyType.AddScale);
			float infoFloat = listInfo.GetInfoFloat(ChaListDefine.KeyType.PosX);
			float infoFloat2 = listInfo.GetInfoFloat(ChaListDefine.KeyType.PosY);
			float infoFloat3 = listInfo.GetInfoFloat(ChaListDefine.KeyType.Rot);
			float infoFloat4 = listInfo.GetInfoFloat(ChaListDefine.KeyType.Scale);
			zero.x = Mathf.InverseLerp(a, b, infoFloat);
			zero.y = Mathf.InverseLerp(a2, b2, infoFloat2);
			zero.z = Mathf.InverseLerp(-1f, 1f, infoFloat3);
			zero.w = Mathf.InverseLerp(a3, b3, infoFloat4);
		}
		return zero;
	}

	public bool SetBodyBaseMaterial()
	{
		if (null == base.customMatBody)
		{
			return false;
		}
		return SetBaseMaterial(base.rendBody, base.customMatBody);
	}

	public bool ReleaseBodyCustomTexture()
	{
		if (base.customTexCtrlBody == null)
		{
			return false;
		}
		base.customTexCtrlBody.SetTexture(ChaShader._MainTex, null);
		base.customTexCtrlBody.SetTexture(ChaShader._ColorMask, null);
		base.customTexCtrlBody.SetTexture(ChaShader._Texture3, null);
		base.customTexCtrlBody.SetTexture(ChaShader._Texture4, null);
		base.customTexCtrlBody.SetTexture(ChaShader._Texture6, null);
		if (Character.enableCharaLoadGCClear)
		{
			Resources.UnloadUnusedAssets();
		}
		return true;
	}

	public void ChangeCustomBodyWithoutCustomTexture()
	{
		ChangeSettingNip();
		ChangeSettingAreolaSize();
		ChangeSettingNipColor();
		ChangeSettingUnderhair();
		ChangeSettingUnderhairColor();
		VisibleAddBodyLine();
		ChangeSettingBodyDetail();
		ChangeSettingBodyDetailPower();
		ChangeSettingSkinGlossPower();
		ChangeSettingNailGlossPower();
		ChangeSettingNipGlossPower();
	}

	public bool InitShapeFace(Transform trfBone, string assetBundleAnmShapeFace, string assetAnmShapeFace, string manifest)
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		if (sibFace == null)
		{
			return false;
		}
		if (null == trfBone)
		{
			return false;
		}
		string cateInfoName = "cf_customhead";
		sibFace.InitShapeInfo(manifest, assetBundleAnmShapeFace, "list/customshape.unity3d", assetAnmShapeFace, cateInfoName, trfBone, base.sex);
		for (int i = 0; i < ShapeFaceNum; i++)
		{
			sibFace.ChangeValue(i, base.chaFile.custom.face.shapeValueFace[i]);
		}
		base.updateShapeFace = true;
		return true;
	}

	public void ReleaseShapeFace()
	{
		if ((base.sex != 0 || 1 != base.exType) && sibFace != null)
		{
			sibFace.ReleaseShapeInfo();
		}
	}

	public bool SetShapeFaceValue(int index, float value)
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		if (index >= ShapeFaceNum)
		{
			return false;
		}
		base.chaFile.custom.face.shapeValueFace[index] = value;
		if (sibFace != null && sibFace.InitEnd)
		{
			sibFace.ChangeValue(index, value);
		}
		if (index == 33)
		{
			ChangeSettingEyeTilt();
		}
		base.updateShapeFace = true;
		return true;
	}

	public bool UpdateShapeFaceValueFromCustomInfo()
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return true;
		}
		if (sibFace == null || !sibFace.InitEnd)
		{
			return false;
		}
		for (int i = 0; i < base.chaFile.custom.face.shapeValueFace.Length; i++)
		{
			sibFace.ChangeValue(i, base.chaFile.custom.face.shapeValueFace[i]);
		}
		base.updateShapeFace = true;
		return true;
	}

	public float GetShapeFaceValue(int index)
	{
		if (base.sex == 0 && 1 == base.exType)
		{
			return 0f;
		}
		if (index >= ShapeFaceNum)
		{
			return 0f;
		}
		return base.chaFile.custom.face.shapeValueFace[index];
	}

	public void UpdateShapeFace()
	{
		if ((base.sex == 0 && 1 == base.exType) || sibFace == null || !sibFace.InitEnd)
		{
			return;
		}
		if (base.chaFile.status.disableMouthShapeMask)
		{
			int num = 0;
			if (200 == base.fileFace.headId)
			{
				num = 1;
			}
			else if (201 == base.fileFace.headId)
			{
				num = 2;
			}
			for (int i = 0; i < ChaFileDefine.cf_MouthShapeMaskID.Length; i++)
			{
				sibFace.ChangeValue(ChaFileDefine.cf_MouthShapeMaskID[i], ChaFileDefine.cf_MouthShapeDefault[num, i]);
			}
		}
		else
		{
			int[] cf_MouthShapeMaskID = ChaFileDefine.cf_MouthShapeMaskID;
			foreach (int num2 in cf_MouthShapeMaskID)
			{
				sibFace.ChangeValue(num2, base.chaFile.custom.face.shapeValueFace[num2]);
			}
		}
		sibFace.typeBone = base.fileCustom.body.typeBone;
		if (sibBody != null)
		{
			ShapeBodyInfoFemale shapeBodyInfoFemale = sibBody as ShapeBodyInfoFemale;
			ShapeHeadInfoFemale shapeHeadInfoFemale = sibFace as ShapeHeadInfoFemale;
			if (shapeBodyInfoFemale != null)
			{
				shapeHeadInfoFemale.headCorrectValue = 1f / (1f + shapeBodyInfoFemale.correctValue[2].vctScl.y);
				float num3 = ((sibFace.typeBone == 0) ? 1.2f : 1.08f);
				GameObject referenceInfo = GetReferenceInfo(RefObjKey.CORRECT_HEAD_DBCOL);
				if ((bool)referenceInfo)
				{
					referenceInfo.transform.localScale = new Vector3(num3, num3, num3);
				}
			}
		}
		sibFace.Update();
	}

	public void DisableShapeMouth(bool disable)
	{
		base.updateShapeFace = true;
		base.chaFile.status.disableMouthShapeMask = disable;
	}

	public bool InitShapeBody(Transform trfBone)
	{
		if (sibBody == null)
		{
			return false;
		}
		if (null == trfBone)
		{
			return false;
		}
		string anmKeyInfoName = "cf_anmShapeBody";
		string cateInfoName = "cf_custombody";
		sibBody.InitShapeInfo("abdata", "chara/oo_base.unity3d", "list/customshape.unity3d", anmKeyInfoName, cateInfoName, trfBone, base.sex);
		float[] array = base.chaFile.custom.body.shapeValueBody.ToArray();
		if (!base.hiPoly)
		{
			array = array.Select(delegate(float val)
			{
				float num2 = 0f;
				return (val < 0.5f) ? Mathf.Lerp(0.2f, 0.5f, Mathf.InverseLerp(0f, 0.5f, val)) : Mathf.Lerp(0.5f, 0.8f, Mathf.InverseLerp(0.5f, 1f, val));
			}).ToArray();
			array[0] = base.chaFile.custom.body.shapeValueBody[0];
		}
		array[13] = Mathf.Lerp(array[13], 1f, base.chaFile.status.nipStandRate);
		for (int num = 0; num < ShapeBodyNum; num++)
		{
			sibBody.ChangeValue(num, array[num]);
		}
		base.updateShapeBody = true;
		base.updateBustSize = true;
		base.reSetupDynamicBoneBust = true;
		return true;
	}

	public void ReleaseShapeBody()
	{
		if (sibBody != null)
		{
			sibBody.ReleaseShapeInfo();
		}
	}

	public bool SetShapeBodyValue(int index, float value)
	{
		if (index >= ShapeBodyNum)
		{
			return false;
		}
		if (base.sex == 0 && index == 0)
		{
			value = ((base.exType == 0) ? 0.6f : 1f);
		}
		float num = (base.chaFile.custom.body.shapeValueBody[index] = value);
		if (!base.hiPoly && base.sex != 0)
		{
			float num2 = 0f;
			num2 = ((!(num < 0.5f)) ? Mathf.Lerp(0.5f, 0.8f, Mathf.InverseLerp(0.5f, 1f, num)) : Mathf.Lerp(0.2f, 0.5f, Mathf.InverseLerp(0f, 0.5f, num)));
			num = num2;
			if (index == 0)
			{
				num = value;
			}
		}
		if (index == 13)
		{
			num = Mathf.Lerp(value, 1f, base.chaFile.status.nipStandRate);
		}
		if (sibBody != null && sibBody.InitEnd)
		{
			sibBody.ChangeValue(index, num);
		}
		base.updateShapeBody = true;
		base.updateBustSize = true;
		base.reSetupDynamicBoneBust = true;
		return true;
	}

	public bool UpdateShapeBodyValueFromCustomInfo()
	{
		if (sibBody == null || !sibBody.InitEnd)
		{
			return false;
		}
		float[] array = base.chaFile.custom.body.shapeValueBody.ToArray();
		if (!base.hiPoly)
		{
			array = array.Select(delegate(float val)
			{
				float num2 = 0f;
				return (val < 0.5f) ? Mathf.Lerp(0.2f, 0.5f, Mathf.InverseLerp(0f, 0.5f, val)) : Mathf.Lerp(0.5f, 0.8f, Mathf.InverseLerp(0.5f, 1f, val));
			}).ToArray();
			array[0] = base.chaFile.custom.body.shapeValueBody[0];
		}
		array[13] = Mathf.Lerp(array[13], 1f, base.chaFile.status.nipStandRate);
		for (int num = 0; num < base.chaFile.custom.body.shapeValueBody.Length; num++)
		{
			sibBody.ChangeValue(num, array[num]);
		}
		if (base.sex == 0)
		{
			sibBody.ChangeValue(0, (base.exType == 0) ? 0.6f : 1f);
		}
		base.updateShapeBody = true;
		base.updateBustSize = true;
		base.reSetupDynamicBoneBust = true;
		return true;
	}

	public float GetShapeBodyValue(int index)
	{
		if (index >= ShapeBodyNum)
		{
			return 0f;
		}
		return base.chaFile.custom.body.shapeValueBody[index];
	}

	public void UpdateShapeBody()
	{
		if (sibBody == null || !sibBody.InitEnd || !(sibBody is ShapeBodyInfoFemale shapeBodyInfoFemale))
		{
			return;
		}
		shapeBodyInfoFemale.updateMask = 7;
		sibBody.typeBone = base.fileCustom.body.typeBone;
		sibBody.Update();
		if (!changeShapeBodyMask)
		{
			return;
		}
		Float16 @float = default(Float16);
		int num = 0;
		for (int i = 0; i < 2; i++)
		{
			for (int j = 0; j < ChaFileDefine.cf_BustShapeMaskID.Length; j++)
			{
				num = ChaFileDefine.cf_BustShapeMaskID[j];
				@float[j] = (base.chaFile.status.disableBustShapeMask[i, j] ? ChaFileDefine.cf_BustShapeDefaultValue[j] : base.chaFile.custom.body.shapeValueBody[num]);
			}
			int cf_ShapeMaskNipStand = ChaFileDefine.cf_ShapeMaskNipStand;
			num = ChaFileDefine.cf_BustShapeMaskID[cf_ShapeMaskNipStand];
			@float[cf_ShapeMaskNipStand] = (base.chaFile.status.disableBustShapeMask[i, cf_ShapeMaskNipStand] ? 0.5f : Mathf.Lerp(base.chaFile.custom.body.shapeValueBody[num], 1f, base.chaFile.status.nipStandRate));
			for (int k = 0; k < ChaFileDefine.cf_BustShapeMaskID.Length; k++)
			{
				sibBody.ChangeValue(ChaFileDefine.cf_BustShapeMaskID[k], @float[k]);
			}
			shapeBodyInfoFemale.updateMask = ((i == 0) ? 1 : 2);
			sibBody.Update();
		}
		changeShapeBodyMask = false;
	}

	public void UpdateAlwaysShapeBody()
	{
		if (sibBody != null && sibBody.InitEnd)
		{
			sibBody.UpdateAlways();
		}
	}

	public void UpdateShapeBodyCalcForce()
	{
		if (sibBody != null && sibBody.InitEnd)
		{
			sibBody.typeBone = base.fileCustom.body.typeBone;
			sibBody.ForceUpdate();
		}
	}

	public void DisableShapeBodyID(int LR, int id, bool disable)
	{
		if (sibBody != null && sibBody.InitEnd && id < ChaFileDefine.cf_BustShapeMaskID.Length)
		{
			changeShapeBodyMask = true;
			base.updateShapeBody = true;
			if (LR == 0)
			{
				base.chaFile.status.disableBustShapeMask[0, id] = disable;
			}
			else if (1 == LR)
			{
				base.chaFile.status.disableBustShapeMask[1, id] = disable;
			}
			else
			{
				base.chaFile.status.disableBustShapeMask[0, id] = disable;
				base.chaFile.status.disableBustShapeMask[1, id] = disable;
			}
			base.reSetupDynamicBoneBust = true;
		}
	}

	public void DisableShapeBust(int LR, bool disable)
	{
		if (sibBody == null || !sibBody.InitEnd)
		{
			return;
		}
		changeShapeBodyMask = true;
		base.updateShapeBody = true;
		if (LR == 0)
		{
			for (int i = 0; i < ChaFileDefine.cf_ShapeMaskBust.Length; i++)
			{
				base.chaFile.status.disableBustShapeMask[0, ChaFileDefine.cf_ShapeMaskBust[i]] = disable;
			}
		}
		else if (1 == LR)
		{
			for (int j = 0; j < ChaFileDefine.cf_ShapeMaskBust.Length; j++)
			{
				base.chaFile.status.disableBustShapeMask[1, ChaFileDefine.cf_ShapeMaskBust[j]] = disable;
			}
		}
		else
		{
			for (int k = 0; k < 2; k++)
			{
				for (int l = 0; l < ChaFileDefine.cf_ShapeMaskBust.Length; l++)
				{
					base.chaFile.status.disableBustShapeMask[k, ChaFileDefine.cf_ShapeMaskBust[l]] = disable;
				}
			}
		}
		base.reSetupDynamicBoneBust = true;
	}

	public void DisableShapeNip(int LR, bool disable)
	{
		if (sibBody == null || !sibBody.InitEnd)
		{
			return;
		}
		changeShapeBodyMask = true;
		base.updateShapeBody = true;
		if (LR == 0)
		{
			for (int i = 0; i < ChaFileDefine.cf_ShapeMaskNip.Length; i++)
			{
				base.chaFile.status.disableBustShapeMask[0, ChaFileDefine.cf_ShapeMaskNip[i]] = disable;
			}
		}
		else if (1 == LR)
		{
			for (int j = 0; j < ChaFileDefine.cf_ShapeMaskNip.Length; j++)
			{
				base.chaFile.status.disableBustShapeMask[1, ChaFileDefine.cf_ShapeMaskNip[j]] = disable;
			}
		}
		else
		{
			for (int k = 0; k < 2; k++)
			{
				for (int l = 0; l < ChaFileDefine.cf_ShapeMaskNip.Length; l++)
				{
					base.chaFile.status.disableBustShapeMask[k, ChaFileDefine.cf_ShapeMaskNip[l]] = disable;
				}
			}
		}
		base.reSetupDynamicBoneBust = true;
	}

	public void UpdateBustSoftnessAndGravity()
	{
		UpdateBustSoftness();
		UpdateBustGravity();
	}

	public void ChangeBustSoftness(float soft)
	{
		if (bustSoft != null)
		{
			bustSoft.Change(soft, default(int));
			base.reSetupDynamicBoneBust = true;
		}
	}

	public bool UpdateBustSoftness()
	{
		if (bustSoft != null)
		{
			bustSoft.ReCalc(default(int));
			base.reSetupDynamicBoneBust = true;
			return true;
		}
		return false;
	}

	public void ChangeBustGravity(float gravity)
	{
		if (bustGravity != null)
		{
			bustGravity.Change(gravity, default(int));
			base.reSetupDynamicBoneBust = true;
		}
	}

	public bool UpdateBustGravity()
	{
		if (bustGravity != null)
		{
			bustGravity.ReCalc(default(int));
			base.reSetupDynamicBoneBust = true;
			return true;
		}
		return false;
	}

	public void LoadNoAsync(bool reflectStatus = false)
	{
		bool flag = ((base.sex != 0) ? true : false);
		string mainManifestName = Character.mainManifestName;
		byte[] statusBytes = null;
		if (reflectStatus)
		{
			statusBytes = base.chaFile.GetStatusBytes();
		}
		ReleaseObject();
		base.objTop = new GameObject("BodyTop");
		if ((bool)base.objRoot)
		{
			base.objTop.transform.SetParent(base.objRoot.transform, worldPositionStays: false);
		}
		AddUpdateCMBodyTexFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpSunburn: true);
		AddUpdateCMBodyColorFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpSunburn: true, inpNail: true);
		AddUpdateCMBodyLayoutFlags(inpPaint01: true, inpPaint02: true);
		CreateBodyTexture();
		LoadGagMaterial();
		string text = "chara/oo_base.unity3d";
		string text2 = (base.hiPoly ? "p_cf_body_bone" : "p_cf_body_bone_low");
		base.objAnim = CommonLib.LoadAsset<GameObject>(text, text2, clone: true, mainManifestName);
		Character.AddLoadAssetBundle(text, mainManifestName);
		if (base.objAnim != null)
		{
			base.objAnim.transform.SetParent(base.objTop.transform, worldPositionStays: false);
			ChaPreparationBodyBone component = base.objAnim.GetComponent<ChaPreparationBodyBone>();
			base.animBody = component.Animator;
			base.dictDynamicBoneBust = new Dictionary<DynamicBoneKind, DynamicBone_Ver02>();
			DynamicBone_Ver02[] dynamicBones = component.DynamicBones;
			for (int i = 0; i < dynamicBones.Length; i++)
			{
				if (!(dynamicBones[i] == null))
				{
					base.dictDynamicBoneBust[(DynamicBoneKind)i] = dynamicBones[i];
					if ((uint)(i - 2) <= 1u && base.sex == 0)
					{
						dynamicBones[i].enabled = false;
					}
				}
			}
			base.neckLookCtrl = component.NeckLookController;
			if (base.neckLookCtrl != null)
			{
				ChangeLookNeckTarget(base.fileStatus.neckTargetType);
			}
			ChangeLookNeckPtn(0);
			base.objBodyBone = component.ObjBoneRoot;
			aaWeightsBody.CreateBoneList(component.BoneInfos);
			CreateReferenceInfo(1uL, component.BoneInfos);
			if (!base.hiPoly)
			{
				GetReferenceTransform(RefObjKey.a_n_back_L).SafeProc(delegate(Transform t)
				{
					t.SetLocalRotationY(180f);
				});
				GetReferenceTransform(RefObjKey.a_n_back_R).SafeProc(delegate(Transform t)
				{
					t.SetLocalRotationY(180f);
				});
				GetReferenceTransform(RefObjKey.a_n_bust).SafeProc(delegate(Transform t)
				{
					t.SetLocalPositionY(0.14f);
				});
				GetReferenceTransform(RefObjKey.a_n_bust_f).SafeProc(delegate(Transform t)
				{
					t.SetLocalPositionZ(0.04f);
					t.SetLocalRotationX(-35f);
				});
				GetReferenceTransform(RefObjKey.a_n_ind_L).SafeProc(delegate(Transform t)
				{
					t.SetLocalPosition(-0.0162f, -0.0024f, 0.0006f);
					t.SetLocalRotation(0f, 0f, 0f);
				});
				GetReferenceTransform(RefObjKey.a_n_ind_R).SafeProc(delegate(Transform t)
				{
					t.SetLocalPosition(0.0162f, 0.0024f, -0.0006f);
					t.SetLocalRotation(180f, 0f, 0f);
				});
				GetReferenceTransform(RefObjKey.a_n_mid_L).SafeProc(delegate(Transform t)
				{
					t.SetLocalPosition(-0.016f, -0.0028f, 0f);
				});
				GetReferenceTransform(RefObjKey.a_n_mid_R).SafeProc(delegate(Transform t)
				{
					t.SetLocalPosition(0.016f, 0.0028f, 0f);
					t.SetLocalRotation(180f, 0f, 0f);
				});
				GetReferenceTransform(RefObjKey.a_n_ring_L).SafeProc(delegate(Transform t)
				{
					t.SetLocalPosition(-0.018f, -0.0018f, -0.0005f);
				});
				GetReferenceTransform(RefObjKey.a_n_ring_R).SafeProc(delegate(Transform t)
				{
					t.SetLocalPosition(0.018f, 0.0018f, 0.0005f);
					t.SetLocalRotation(180f, 0f, 0f);
				});
				GetReferenceTransform(RefObjKey.a_n_nip_L).SafeProc(delegate(Transform t)
				{
					t.SetLocalPosition(0f, 0f, 0.05f);
					t.SetLocalRotation(0f, 0f, 0f);
				});
				GetReferenceTransform(RefObjKey.a_n_nip_R).SafeProc(delegate(Transform t)
				{
					t.SetLocalPosition(0f, 0f, 0.05f);
					t.SetLocalRotation(0f, 0f, 0f);
				});
			}
			InitShapeBody(base.objBodyBone.transform);
			InitShapeHand(base.objBodyBone.transform);
			base.objNeckLookTargetP = new GameObject("N_NeckLookTargetP");
			Transform referenceTransform = GetReferenceTransform(RefObjKey.NECK_LOOK_TARGET);
			if (base.objNeckLookTargetP != null && referenceTransform != null)
			{
				base.objNeckLookTargetP.transform.SetParent(referenceTransform, worldPositionStays: false);
				base.objNeckLookTarget = new GameObject("N_NeckLookTarget");
				if (base.objNeckLookTarget != null)
				{
					base.objNeckLookTarget.transform.SetParent(base.objNeckLookTargetP.transform, worldPositionStays: false);
				}
			}
			base.objEyesLookTargetP = new GameObject("N_EyesLookTargetP");
			referenceTransform = GetReferenceTransform(RefObjKey.HeadParent);
			if (base.objEyesLookTargetP != null && referenceTransform != null)
			{
				base.objEyesLookTargetP.transform.SetParent(referenceTransform, worldPositionStays: false);
				base.objEyesLookTarget = new GameObject("N_EyesLookTarget");
				if (base.objEyesLookTarget != null)
				{
					base.objEyesLookTarget.transform.SetParent(base.objEyesLookTargetP.transform, worldPositionStays: false);
				}
			}
			string text3 = "chara/oo_base.unity3d";
			string text4 = "p_cf_head_bone";
			base.objHeadBone = CommonLib.LoadAsset<GameObject>(text3, text4, clone: true, mainManifestName);
			Character.AddLoadAssetBundle(text3, mainManifestName);
			if (base.objHeadBone != null)
			{
				GameObject referenceInfo = GetReferenceInfo(RefObjKey.HeadParent);
				base.objHeadBone.transform.SetParent(referenceInfo.transform, worldPositionStays: false);
				ChaPreparationHeadBone component2 = base.objHeadBone.GetComponent<ChaPreparationHeadBone>();
				base.eyeLookCtrl = component2.EyeLookController;
				if (component2.EyeLookCalc != null)
				{
					ChangeLookEyesTarget(base.fileStatus.eyesTargetType);
					ChangeLookEyesPtn(0);
				}
				aaWeightsHead.CreateBoneList(component2.BoneInfos);
				CreateReferenceInfo(2uL, component2.BoneInfos);
				string assetBundleName = "chara/oo_base.unity3d";
				string assetName = ((!base.hiPoly) ? (flag ? "p_cf_body_00_low" : "p_cm_body_00_low") : (flag ? "p_cf_body_00" : "p_cm_body_00"));
				base.objBody = CommonLib.LoadAsset<GameObject>(assetBundleName, assetName, clone: true, mainManifestName);
				Character.AddLoadAssetBundle(assetBundleName, mainManifestName);
				if (base.objBody != null)
				{
					CreateReferenceInfo(3uL, base.objBody);
					GetReferenceInfo(RefObjKey.ObjBody).SafeProc(delegate(GameObject obj)
					{
						base.rendBody = obj.GetComponent<Renderer>();
					});
					GetReferenceInfo(RefObjKey.S_SimpleBody).SafeProc(delegate(GameObject obj)
					{
						base.rendSimpleBody = obj.GetComponent<Renderer>();
					});
					GetReferenceInfo(RefObjKey.S_SimpleTongue).SafeProc(delegate(GameObject obj)
					{
						base.rendSimpleTongue = obj.GetComponent<Renderer>();
					});
					SetBodyBaseMaterial();
					base.objBody.transform.SetParent(base.objTop.transform, worldPositionStays: false);
					Transform referenceTransform2 = GetReferenceTransform(RefObjKey.A_ROOTBONE);
					aaWeightsBody.AssignedWeightsAndSetBounds(base.objBody, "cf_j_root", bounds, referenceTransform2);
					if (1 == base.sex && base.hiPoly)
					{
						assetBundleName = "chara/oo_base.unity3d";
						assetName = "p_cf_body_00_Nml";
						BustNormal value = null;
						if (dictBustNormal.TryGetValue(BustNormalKind.NmlBody, out value))
						{
							value.Release();
						}
						value = new BustNormal();
						value.Init(base.objBody, assetBundleName, assetName, "");
						dictBustNormal[BustNormalKind.NmlBody] = value;
					}
					ChangeCustomBodyWithoutCustomTexture();
				}
				if (base.hiPoly)
				{
					string assetBundleName2 = "chara/oo_base.unity3d";
					string assetName2 = "p_tang2";
					base.objTongueEx = CommonLib.LoadAsset<GameObject>(assetBundleName2, assetName2, clone: true, mainManifestName);
					Character.AddLoadAssetBundle(assetBundleName2, mainManifestName);
					if (base.objTongueEx != null)
					{
						GetReferenceTransform(RefObjKey.HeadParent);
						base.objTongueEx.transform.SetParent(base.transform, worldPositionStays: false);
						base.animTongueEx = base.objTongueEx.GetComponent<Animator>();
					}
				}
				if (!Game.isAddH)
				{
					if (1 == base.sex)
					{
						string assetBundleName3 = "chara/oo_base.unity3d";
						string assetName3 = "p_cf_hide_nip";
						base.objHideNip = CommonLib.LoadAsset<GameObject>(assetBundleName3, assetName3, clone: true, mainManifestName);
						Character.AddLoadAssetBundle(assetBundleName3, mainManifestName);
						if ((bool)base.objHideNip)
						{
							base.objHideNip.transform.SetParent(base.objBodyBone.transform, worldPositionStays: false);
							GameObject referenceInfo2 = GetReferenceInfo(RefObjKey.A_ROOTBONE);
							Transform rootBone = (referenceInfo2 ? referenceInfo2.transform : null);
							aaWeightsBody.AssignedWeightsAndSetBounds(base.objHideNip, "cf_j_root", bounds, rootBone);
						}
					}
					if (1 == base.sex)
					{
						string assetBundleName4 = "chara/oo_base.unity3d";
						string assetName4 = "p_cf_hide_kokan";
						base.objHideKokan = CommonLib.LoadAsset<GameObject>(assetBundleName4, assetName4, clone: true, mainManifestName);
						Character.AddLoadAssetBundle(assetBundleName4, mainManifestName);
						if ((bool)base.objHideKokan)
						{
							base.objHideKokan.transform.SetParent(base.objBodyBone.transform, worldPositionStays: false);
							GameObject referenceInfo3 = GetReferenceInfo(RefObjKey.A_ROOTBONE);
							Transform rootBone2 = (referenceInfo3 ? referenceInfo3.transform : null);
							aaWeightsBody.AssignedWeightsAndSetBounds(base.objHideKokan, "cf_j_root", bounds, rootBone2);
						}
					}
				}
				if (reflectStatus)
				{
					base.chaFile.SetStatusBytes(statusBytes);
				}
				ChangeHead(forceChange: true, update: false);
				LoadHairGlossMask();
				ChangeHair(forceChange: true);
				ChangeClothes(forceChange: true, update: false);
				ChangeAccessory(forceChange: true, update: false);
				SetHideHairAccessory();
				base.updateBustSize = true;
				base.reSetupDynamicBoneBust = true;
				UpdateSiru(forceChange: true);
				ChangeHohoAkaRate(base.fileStatus.hohoAkaRate);
				if (Character.enableCharaLoadGCClear)
				{
					Resources.UnloadUnusedAssets();
					GC.Collect();
				}
				base.loadEnd = true;
				return;
			}
			using Utf16ValueStringBuilder utf16ValueStringBuilder = ZString.CreateStringBuilder();
			utf16ValueStringBuilder.AppendFormat("assetBundleName：{0}  AssetName：{1}\u3000の読み込みに失敗", text3, text4);
			return;
		}
		using Utf16ValueStringBuilder utf16ValueStringBuilder2 = ZString.CreateStringBuilder();
		utf16ValueStringBuilder2.AppendFormat("assetBundleName：{0}  AssetName：{1}\u3000の読み込みに失敗", text, text2);
	}

	public void ReloadNoAsync(bool noChangeClothes = false, bool noChangeHead = false, bool noChangeHair = false, bool noChangeBody = false)
	{
		if (!noChangeBody)
		{
			AddUpdateCMBodyTexFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpSunburn: true);
			AddUpdateCMBodyColorFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpSunburn: true, inpNail: true);
			AddUpdateCMBodyLayoutFlags(inpPaint01: true, inpPaint02: true);
			CreateBodyTexture();
			if (noChangeHead)
			{
				AddUpdateCMFaceTexFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpCheek: true, inpLipLine: true, inpMole: true);
				AddUpdateCMFaceColorFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpCheek: true, inpLipLine: true, inpMole: true);
				AddUpdateCMFaceLayoutFlags(inpPaint01: true, inpPaint02: true, inpMole: true);
				CreateFaceTexture();
				ChangeCustomFaceWithoutCustomTexture();
			}
		}
		if (!noChangeHead)
		{
			ChangeHead(forceChange: true);
		}
		if (!noChangeHair)
		{
			LoadHairGlossMask();
			ChangeHair(forceChange: true);
		}
		if (!noChangeClothes)
		{
			ChangeClothes(forceChange: true);
			ChangeAccessory(forceChange: true);
		}
		UpdateShapeBodyValueFromCustomInfo();
		UpdateShapeFaceValueFromCustomInfo();
		base.updateBustSize = true;
		base.reSetupDynamicBoneBust = true;
		if (!noChangeBody)
		{
			ChangeCustomBodyWithoutCustomTexture();
		}
		UpdateClothesStateAll();
		if (Character.enableCharaLoadGCClear)
		{
			Resources.UnloadUnusedAssets();
			GC.Collect();
		}
	}

	public void ChangeHeadNoAsync(int _headId, bool forceChange = false, bool update = true)
	{
		if (-1 == _headId || (!forceChange && null != base.objHead && _headId == base.fileFace.headId))
		{
			return;
		}
		string createName = "ct_head";
		if ((bool)base.objHead)
		{
			SafeDestroy(base.objHead);
			base.objHead = null;
			base.infoHead = null;
			ReleaseRefObject(4uL);
			ReleaseShapeFace();
			base.foregroundCtrl = null;
			base.rendFace = null;
			base.rendEye = new Renderer[2];
			base.rendEyeW = new Renderer[2];
			base.rendEyelineUp = null;
			base.rendEyelineDown = null;
			base.rendEyebrow = null;
			base.rendNose = null;
		}
		base.objHead = LoadCharaFbxData(delegate(ListInfoBase lib)
		{
			base.infoHead = lib;
		}, _hiPoly: true, (base.sex == 0 && 1 == base.exType) ? 501 : 100, _headId, createName, copyDynamicBone: false, 0, null, 0);
		if (!base.objHead)
		{
			return;
		}
		CommonLib.CopySameNameTransform(base.objHeadBone.transform, base.objHead.transform);
		base.objHead.transform.SetParent(base.objHeadBone.transform, worldPositionStays: false);
		GameObject referenceInfo = GetReferenceInfo(RefObjKey.A_ROOTBONE);
		Transform rootBone = (referenceInfo ? referenceInfo.transform : null);
		aaWeightsHead.AssignedWeightsAndSetBounds(base.objHead, "cf_J_N_FaceRoot", bounds, rootBone);
		ListInfoBase listInfoBase = base.infoHead;
		base.foregroundCtrl = base.objHead.GetComponent<ChaForegroundComponent>();
		base.fileFace.headId = listInfoBase.Id;
		CreateReferenceInfo(4uL, base.objHead);
		GameObject referenceInfo2 = GetReferenceInfo(RefObjKey.ObjEyeL);
		if (referenceInfo2 != null)
		{
			base.eyeLookMatCtrl[0] = referenceInfo2.GetComponent<EyeLookMaterialControll>();
			if (base.eyeLookMatCtrl[0] != null)
			{
				base.eyeLookMatCtrl[0].script = base.eyeLookCtrl.eyeLookScript;
				base.eyeLookMatCtrl[0].ReSetupMaterial();
			}
		}
		GameObject referenceInfo3 = GetReferenceInfo(RefObjKey.ObjEyeR);
		if (referenceInfo3 != null)
		{
			base.eyeLookMatCtrl[1] = referenceInfo3.GetComponent<EyeLookMaterialControll>();
			if (base.eyeLookMatCtrl[1] != null)
			{
				base.eyeLookMatCtrl[1].script = base.eyeLookCtrl.eyeLookScript;
				base.eyeLookMatCtrl[1].ReSetupMaterial();
			}
		}
		GetReferenceInfo(RefObjKey.ObjFace).SafeProc(delegate(GameObject refObj)
		{
			base.rendFace = refObj.GetComponent<Renderer>();
		});
		base.rendEye = new Renderer[2];
		GetReferenceInfo(RefObjKey.ObjEyeL).SafeProc(delegate(GameObject refObj)
		{
			base.rendEye[0] = refObj.GetComponent<Renderer>();
		});
		GetReferenceInfo(RefObjKey.ObjEyeR).SafeProc(delegate(GameObject refObj)
		{
			base.rendEye[1] = refObj.GetComponent<Renderer>();
		});
		base.rendEyeW = new Renderer[2];
		GetReferenceInfo(RefObjKey.ObjEyeWL).SafeProc(delegate(GameObject refObj)
		{
			base.rendEyeW[0] = refObj.GetComponent<Renderer>();
		});
		GetReferenceInfo(RefObjKey.ObjEyeWR).SafeProc(delegate(GameObject refObj)
		{
			base.rendEyeW[1] = refObj.GetComponent<Renderer>();
		});
		GetReferenceInfo(RefObjKey.ObjEyeline).SafeProc(delegate(GameObject refObj)
		{
			base.rendEyelineUp = refObj.GetComponent<Renderer>();
		});
		GetReferenceInfo(RefObjKey.ObjEyelineLow).SafeProc(delegate(GameObject refObj)
		{
			base.rendEyelineDown = refObj.GetComponent<Renderer>();
		});
		GetReferenceInfo(RefObjKey.ObjEyebrow).SafeProc(delegate(GameObject refObj)
		{
			base.rendEyebrow = refObj.GetComponent<Renderer>();
		});
		GetReferenceInfo(RefObjKey.ObjNoseline).SafeProc(delegate(GameObject refObj)
		{
			base.rendNose = refObj.GetComponent<Renderer>();
		});
		string info = listInfoBase.GetInfo(ChaListDefine.KeyType.MatManifest);
		string info2 = listInfoBase.GetInfo(ChaListDefine.KeyType.MatAB);
		string info3 = listInfoBase.GetInfo(ChaListDefine.KeyType.MatData);
		InitBaseCustomTextureFace(info2, info3, info);
		AddUpdateCMFaceTexFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpCheek: true, inpLipLine: true, inpMole: true);
		AddUpdateCMFaceColorFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpCheek: true, inpLipLine: true, inpMole: true);
		AddUpdateCMFaceLayoutFlags(inpPaint01: true, inpPaint02: true, inpMole: true);
		CreateFaceTexture();
		SetFaceBaseMaterial();
		ChangeSettingWhiteOfEye(updateTex: true, updateColor: true);
		ChangeSettingEye(2, updateBaseTex: true, updateMaskTex: true, updateColorAndOffset: true);
		if (update)
		{
			UpdateSiru(forceChange: true);
			ChangeHohoAkaRate(base.fileStatus.hohoAkaRate);
		}
		InitShapeFace(base.objHeadBone.transform, listInfoBase.GetInfo(ChaListDefine.KeyType.MainAB), listInfoBase.GetInfo(ChaListDefine.KeyType.ShapeAnime), listInfoBase.GetInfo(ChaListDefine.KeyType.MainManifest));
		ChangeCustomFaceWithoutCustomTexture();
		HideEyeHighlight(base.fileStatus.hideEyesHighlight);
		base.fbsCtrl = base.objHead.GetComponent<FaceBlendShape>();
		if (null != base.fbsCtrl)
		{
			base.eyebrowCtrl = base.fbsCtrl.EyebrowCtrl;
			base.eyesCtrl = base.fbsCtrl.EyesCtrl;
			base.mouthCtrl = base.fbsCtrl.MouthCtrl;
			ChangeEyebrowPtn(base.fileStatus.eyebrowPtn);
			ChangeEyebrowOpenMax(base.fileStatus.eyebrowOpenMax);
			ChangeEyesPtn(base.fileStatus.eyesPtn);
			ChangeEyesOpenMax(base.fileStatus.eyesOpenMax);
			ChangeMouthPtn(base.fileStatus.mouthPtn);
			ChangeMouthOpenMax(base.fileStatus.mouthOpenMax);
			base.fbsCtrl.EyeLookController = base.eyeLookCtrl;
			ChangeEyesBlinkFlag(base.fileStatus.eyesBlink);
		}
	}

	public void ChangeHairNoAsync(int kind, int id, bool forceChange = false)
	{
		if (base.sex == 0 && 1 == base.exType && kind >= 1)
		{
			id = 0;
		}
		if (!forceChange && null != base.objHair[kind] && id == base.fileHair.parts[kind].id)
		{
			return;
		}
		if ((bool)base.objHair[kind])
		{
			SafeDestroy(base.objHair[kind]);
			base.objHair[kind] = null;
			base.infoHair[kind] = null;
			base.cusHairCmp[kind] = null;
		}
		Transform referenceTransform = GetReferenceTransform(RefObjKey.HairParent);
		base.objHair[kind] = LoadCharaFbxData(delegate(ListInfoBase lib)
		{
			base.infoHair[kind] = lib;
		}, base.hiPoly, (base.sex == 0 && 1 == base.exType && kind == 0) ? 502 : ChaFileDefine.hairFbxType[kind], id, ChaFileDefine.hairObjName[kind], copyDynamicBone: false, 0, referenceTransform, ChaFileDefine.hairDefID[kind]);
		if (!(base.objHair[kind] != null))
		{
			return;
		}
		ListInfoBase listInfoBase = base.infoHair[kind];
		base.fileHair.parts[kind].id = listInfoBase.Id;
		if (kind == 0)
		{
			base.fileHair.kind = listInfoBase.Kind;
		}
		base.cusHairCmp[kind] = base.objHair[kind].GetComponent<ChaCustomHairComponent>();
		if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy")
		{
			_ = null == base.cusHairCmp[kind];
		}
		ChangeSettingHairGlossMask(kind);
		ChangeSettingHairColor(kind, c00: true, c01: true, c02: true);
		ChangeSettingHairOutlineColor(kind);
		ChangeSettingHairGlossColor(kind);
		ChangeSettingHairLength(kind);
		ChangeSettingHairAcsColor(kind);
		if (!base.hiPoly && kind == 1)
		{
			DynamicBone[] componentsInChildren = base.objHair[kind].GetComponentsInChildren<DynamicBone>(includeInactive: true);
			for (int num = 0; num < componentsInChildren.Length; num++)
			{
				componentsInChildren[num].enabled = false;
			}
		}
		else
		{
			ChangeShakeHair(kind);
		}
	}

	public void ChangeClothesNoAsync(int kind, int id, int subId01, int subId02, int subId03, bool forceChange = false, bool update = true)
	{
		switch (kind)
		{
		case 0:
			ChangeClothesTop(id, subId01, subId02, subId03, forceChange, update);
			break;
		case 1:
			if (base.sex != 0 || 1 != base.exType)
			{
				ChangeClothesBot(id, forceChange, update);
			}
			break;
		case 2:
			if (base.sex != 0 || 1 != base.exType)
			{
				ChangeClothesBra(id, forceChange, update);
			}
			break;
		case 3:
			if (base.sex != 0 || 1 != base.exType)
			{
				ChangeClothesShorts(id, forceChange, update);
			}
			break;
		case 4:
			if (base.sex != 0 || 1 != base.exType)
			{
				ChangeClothesGloves(id, forceChange);
			}
			break;
		case 5:
			if (base.sex != 0 || 1 != base.exType)
			{
				ChangeClothesPanst(id, forceChange, update);
			}
			break;
		case 6:
			if (base.sex != 0 || 1 != base.exType)
			{
				ChangeClothesSocks(id, forceChange);
			}
			break;
		case 7:
			ChangeClothesShoes(0, id, forceChange);
			break;
		case 8:
			ChangeClothesShoes(1, id, forceChange);
			break;
		}
	}

	public void ChangeClothesTopNoAsync(int id, int subId01, int subId02, int subId03, bool forceChange = false, bool update = true)
	{
		updateAlphaMask = true;
		bool flag = true;
		bool flag2 = true;
		int kindNo = 0;
		string createName = "ct_clothesTop";
		int num = ((base.infoClothes[kindNo] == null) ? (-1) : base.infoClothes[kindNo].Id);
		if (!forceChange && null != base.objClothes[kindNo] && id == num)
		{
			flag = false;
			flag2 = false;
		}
		ReleaseBaseCustomTextureClothes(main: true, kindNo);
		if (flag2 && (bool)base.objClothes[kindNo])
		{
			SafeDestroy(base.objClothes[kindNo]);
			base.objClothes[kindNo] = null;
			BustNormal value = null;
			if (dictBustNormal.TryGetValue(BustNormalKind.NmlTop, out value))
			{
				value.Release();
			}
			base.infoClothes[kindNo] = null;
			base.cusClothesCmp[kindNo] = null;
			ReleaseRefObject(5uL);
			notBra = false;
			notBot = false;
			RemoveClothesStateKind(kindNo);
			if (null != texBodyAlphaMask && (bool)base.customMatBody)
			{
				base.customMatBody.SetTexture(ChaShader._AlphaMask, null);
			}
			texBodyAlphaMask = null;
			texBraAlphaMask = null;
		}
		if (flag)
		{
			base.objClothes[kindNo] = LoadCharaFbxData(delegate(ListInfoBase lib)
			{
				base.infoClothes[kindNo] = lib;
			}, base.hiPoly, (base.sex == 0 && 1 == base.exType) ? 503 : 105, id, createName, copyDynamicBone: true, 1, base.objTop.transform, 0);
			if (base.objClothes[kindNo] != null)
			{
				ListInfoBase listInfoBase = base.infoClothes[kindNo];
				base.cusClothesCmp[kindNo] = base.objClothes[kindNo].GetComponent<ChaClothesComponent>();
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy")
				{
					_ = null == base.cusClothesCmp[kindNo];
				}
				nowCoordinate.clothes.parts[kindNo].id = listInfoBase.Id;
				CreateReferenceInfo(5uL, base.objClothes[kindNo]);
				notBra = ((1 == listInfoBase.GetInfoInt(ChaListDefine.KeyType.NotBra)) ? true : false);
				notBot = ((2 == listInfoBase.GetInfoInt(ChaListDefine.KeyType.Coordinate)) ? true : false);
				AddClothesStateKind(kindNo, listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
				LoadAlphaMaskTexture(listInfoBase.GetInfo(ChaListDefine.KeyType.OverBodyMaskAB), listInfoBase.GetInfo(ChaListDefine.KeyType.OverBodyMask), 0);
				if ((bool)base.customMatBody)
				{
					base.customMatBody.SetTexture(ChaShader._AlphaMask, texBodyAlphaMask);
				}
				LoadAlphaMaskTexture(listInfoBase.GetInfo(ChaListDefine.KeyType.OverBraMaskAB), listInfoBase.GetInfo(ChaListDefine.KeyType.OverBraMask), 1);
				if (base.rendBra != null)
				{
					ListInfoBase listInfoBase2 = base.infoClothes[2];
					if (listInfoBase2 != null)
					{
						int num2 = ((2 == listInfoBase2.GetInfoInt(ChaListDefine.KeyType.Coordinate)) ? 1 : 0);
						if (listInfoBase2.dictInfo.TryGetValue(105, out var value2))
						{
							num2 = (("1" == value2) ? 1 : 0);
						}
						if (base.rendBra[0] != null)
						{
							base.rendBra[0].material.SetTexture(ChaShader._AlphaMask, texBraAlphaMask);
							base.rendBra[0].material.SetTextureOffset(ChaShader._AlphaMask, ChaListDefine.braOffset[num2]);
							base.rendBra[0].material.SetTextureScale(ChaShader._AlphaMask, ChaListDefine.braTiling[num2]);
						}
						if (base.rendBra[1] != null)
						{
							base.rendBra[1].material.SetTexture(ChaShader._AlphaMask, texBraAlphaMask);
							base.rendBra[1].material.SetTextureOffset(ChaShader._AlphaMask, ChaListDefine.braOffset[num2]);
							base.rendBra[1].material.SetTextureScale(ChaShader._AlphaMask, ChaListDefine.braTiling[num2]);
						}
					}
				}
				string info = listInfoBase.GetInfo(ChaListDefine.KeyType.NormalData);
				if ("0" != info)
				{
					BustNormal value3 = null;
					if (!dictBustNormal.TryGetValue(BustNormalKind.NmlTop, out value3))
					{
						value3 = new BustNormal();
					}
					value3.Init(base.objClothes[kindNo], listInfoBase.GetInfo(ChaListDefine.KeyType.MainAB), info, listInfoBase.GetInfo(ChaListDefine.KeyType.MainManifest));
					dictBustNormal[BustNormalKind.NmlTop] = value3;
				}
			}
		}
		if (base.objClothes[kindNo] != null)
		{
			InitBaseCustomTextureClothes(main: true, kindNo);
			if (base.loadWithDefaultColorAndPtn)
			{
				SetClothesDefaultColor(kindNo);
				for (int num3 = 0; num3 < 4; num3++)
				{
					nowCoordinate.clothes.parts[kindNo].colorInfo[num3].pattern = 0;
				}
			}
			ChangeCustomClothes(main: true, kindNo, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true, updateTex04: false);
			for (int num4 = 0; num4 < 2; num4++)
			{
				ChangeCustomEmblem(kindNo, num4);
			}
			if (base.releaseCustomInputTexture)
			{
				ReleaseBaseCustomTextureClothes(main: true, kindNo, createTex: false);
			}
			bool forceChange2 = flag || forceChange;
			if (base.sex != 0 || 1 != base.exType)
			{
				ChangeClothesTopParts(0, subId01, forceChange2);
				ChangeClothesTopParts(1, subId02, forceChange2);
				ChangeClothesTopParts(2, subId03, forceChange2);
			}
		}
		if (update)
		{
			UpdateSiru(forceChange: true);
		}
		if ("0" == base.infoClothes[kindNo].GetInfo(ChaListDefine.KeyType.Coordinate))
		{
			int num5 = 1;
			if (null == base.objClothes[num5])
			{
				ChangeClothesBot(nowCoordinate.clothes.parts[num5].id, forceChange: false, update);
			}
		}
	}

	public void ChangeClothesTopPartsNoAsync(int kind, int id, bool forceChange = false)
	{
		bool flag = true;
		bool flag2 = true;
		int num = ((base.infoParts[kind] == null) ? (-1) : base.infoParts[kind].Id);
		if (!forceChange && null != base.objParts[kind] && id == num)
		{
			flag = false;
			flag2 = false;
		}
		int num2 = 0;
		ListInfoBase listInfoBase = base.infoClothes[0];
		if (listInfoBase != null)
		{
			num2 = listInfoBase.Kind;
		}
		bool flag3 = 1 != num2 && 2 != num2;
		if (flag3 || null == base.objClothes[0])
		{
			flag = false;
			flag2 = true;
			nowCoordinate.clothes.subPartsId[kind] = 0;
		}
		ReleaseBaseCustomTextureClothes(main: false, kind);
		if (flag2 && base.objParts[kind] != null)
		{
			SafeDestroy(base.objParts[kind]);
			base.objParts[kind] = null;
			BustNormal value = null;
			if (dictBustNormal.TryGetValue(BustNormalKind.NmlTopPA, out value))
			{
				value.Release();
			}
			base.infoParts[kind] = null;
			base.cusClothesSubCmp[kind] = null;
			if ((1 == num2 && kind == 0) || (2 == num2 && kind != 2))
			{
				notBra = false;
			}
			ReleaseRefObject(13uL + (ulong)kind);
			if (!flag3)
			{
				if (null != texBodyAlphaMask && kind == 0)
				{
					if ((bool)base.customMatBody)
					{
						base.customMatBody.SetTexture(ChaShader._AlphaMask, null);
					}
					texBodyAlphaMask = null;
				}
				if (null != texBraAlphaMask && kind == 0)
				{
					if (base.rendBra != null)
					{
						Renderer[] array = base.rendBra;
						foreach (Renderer renderer in array)
						{
							if (null != renderer)
							{
								renderer.material.SetTexture(ChaShader._AlphaMask, null);
							}
						}
					}
					texBraAlphaMask = null;
				}
			}
			if (kind == 0)
			{
				base.rendInner = new Renderer[2];
			}
			if (1 == kind && null != texInnerAlphaMask)
			{
				if (base.rendInner != null)
				{
					Renderer[] array = base.rendInner;
					foreach (Renderer renderer2 in array)
					{
						if (null != renderer2)
						{
							renderer2.material.SetTexture(ChaShader._AlphaMask, null);
						}
					}
				}
				texInnerAlphaMask = null;
			}
		}
		if (flag)
		{
			int category = ((1 == num2) ? 200 : 210) + kind;
			int num3 = ((1 != num2) ? 1 : 0);
			base.objParts[kind] = LoadCharaFbxData(delegate(ListInfoBase lib)
			{
				base.infoParts[kind] = lib;
			}, base.hiPoly, category, id, ChaFileDefine.topPartsNames[kind], copyDynamicBone: true, 1, base.objClothes[0].transform, ChaFileDefine.topPartsDefaultId[num3, kind]);
			if (base.objParts[kind] != null)
			{
				ListInfoBase listInfoBase2 = base.infoParts[kind];
				base.cusClothesSubCmp[kind] = base.objParts[kind].GetComponent<ChaClothesComponent>();
				if (listInfoBase2.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy")
				{
					_ = null == base.cusClothesSubCmp[kind];
				}
				nowCoordinate.clothes.subPartsId[kind] = listInfoBase2.Id;
				if (2 == num2)
				{
					if (kind == 0)
					{
						if (7 == listInfoBase2.Id)
						{
							base.cusClothesSubCmp[kind].useColorN02 = true;
							base.cusClothesSubCmp[kind].useColorN03 = false;
						}
						else if (9 == listInfoBase2.Id)
						{
							base.cusClothesSubCmp[kind].useColorN03 = true;
						}
					}
					else if (1 == kind)
					{
						if (11 == listInfoBase2.Id)
						{
							base.cusClothesSubCmp[kind].useColorN01 = false;
							base.cusClothesSubCmp[kind].useColorN02 = true;
							base.cusClothesSubCmp[kind].useColorN03 = false;
						}
						else if (12 == listInfoBase2.Id)
						{
							base.cusClothesSubCmp[kind].useColorN01 = false;
							base.cusClothesSubCmp[kind].useColorN02 = true;
							base.cusClothesSubCmp[kind].useColorN03 = false;
						}
						else if (9 == listInfoBase2.Id)
						{
							base.cusClothesSubCmp[kind].useColorN01 = false;
							base.cusClothesSubCmp[kind].useColorN02 = true;
							base.cusClothesSubCmp[kind].useColorN03 = false;
						}
					}
				}
				CreateReferenceInfo(13uL + (ulong)kind, base.objParts[kind]);
				string info = listInfoBase2.GetInfo(ChaListDefine.KeyType.NormalData);
				if (1 == num2 && kind == 0)
				{
					notBra = ((1 == listInfoBase2.GetInfoInt(ChaListDefine.KeyType.NotBra)) ? true : false);
				}
				if (2 == num2)
				{
					ListInfoBase listInfoBase3 = base.infoParts[0];
					ListInfoBase listInfoBase4 = base.infoParts[1];
					notBra = false;
					if (listInfoBase3 != null && 1 == listInfoBase3.GetInfoInt(ChaListDefine.KeyType.NotBra))
					{
						notBra = true;
					}
					if (listInfoBase4 != null && 1 == listInfoBase4.GetInfoInt(ChaListDefine.KeyType.NotBra))
					{
						notBra = true;
					}
				}
				if ("0" != info)
				{
					BustNormal value2 = null;
					if (!dictBustNormal.TryGetValue(BustNormalKind.NmlTopPA, out value2))
					{
						value2 = new BustNormal();
					}
					value2.Init(base.objParts[kind], listInfoBase2.GetInfo(ChaListDefine.KeyType.MainAB), info, listInfoBase2.GetInfo(ChaListDefine.KeyType.MainManifest));
					dictBustNormal[BustNormalKind.NmlTopPA] = value2;
				}
				if (kind == 0)
				{
					GameObject referenceInfo = GetReferenceInfo(RefObjKey.ObjInnerDef);
					if (null != referenceInfo)
					{
						base.rendInner[0] = referenceInfo.GetComponent<Renderer>();
					}
					referenceInfo = GetReferenceInfo(RefObjKey.ObjInnerNuge);
					if (null != referenceInfo)
					{
						base.rendInner[1] = referenceInfo.GetComponent<Renderer>();
					}
					if (base.rendInner != null)
					{
						if ((bool)base.rendInner[0])
						{
							base.rendInner[0].material.SetTexture(ChaShader._AlphaMask, texInnerAlphaMask);
						}
						if ((bool)base.rendInner[1])
						{
							base.rendInner[1].material.SetTexture(ChaShader._AlphaMask, texInnerAlphaMask);
						}
					}
					LoadAlphaMaskTexture(listInfoBase2.GetInfo(ChaListDefine.KeyType.OverBodyMaskAB), listInfoBase2.GetInfo(ChaListDefine.KeyType.OverBodyMask), 0);
					if (base.customMatBody != null)
					{
						base.customMatBody.SetTexture(ChaShader._AlphaMask, texBodyAlphaMask);
					}
					LoadAlphaMaskTexture(listInfoBase2.GetInfo(ChaListDefine.KeyType.OverBraMaskAB), listInfoBase2.GetInfo(ChaListDefine.KeyType.OverBraMask), 1);
					if (base.rendBra != null)
					{
						ListInfoBase listInfoBase5 = base.infoClothes[2];
						if (listInfoBase5 != null)
						{
							int num4 = ((2 == listInfoBase5.GetInfoInt(ChaListDefine.KeyType.Coordinate)) ? 1 : 0);
							if (listInfoBase5.dictInfo.TryGetValue(105, out var value3))
							{
								num4 = (("1" == value3) ? 1 : 0);
							}
							if ((bool)base.rendBra[0])
							{
								base.rendBra[0].material.SetTexture(ChaShader._AlphaMask, texBraAlphaMask);
								base.rendBra[0].material.SetTextureOffset(ChaShader._AlphaMask, ChaListDefine.braOffset[num4]);
								base.rendBra[0].material.SetTextureScale(ChaShader._AlphaMask, ChaListDefine.braTiling[num4]);
							}
							if ((bool)base.rendBra[1])
							{
								base.rendBra[1].material.SetTexture(ChaShader._AlphaMask, texBraAlphaMask);
								base.rendBra[1].material.SetTextureOffset(ChaShader._AlphaMask, ChaListDefine.braOffset[num4]);
								base.rendBra[1].material.SetTextureScale(ChaShader._AlphaMask, ChaListDefine.braTiling[num4]);
							}
						}
					}
				}
				else if (1 == kind)
				{
					LoadAlphaMaskTexture(listInfoBase2.GetInfo(ChaListDefine.KeyType.OverInnerMaskAB), listInfoBase2.GetInfo(ChaListDefine.KeyType.OverInnerMask), 2);
					if (base.rendInner != null)
					{
						if ((bool)base.rendInner[0])
						{
							base.rendInner[0].material.SetTexture(ChaShader._AlphaMask, texInnerAlphaMask);
						}
						if ((bool)base.rendInner[1])
						{
							base.rendInner[1].material.SetTexture(ChaShader._AlphaMask, texInnerAlphaMask);
						}
					}
					byte b = base.fileStatus.clothesState[0];
					ChangeAlphaMask(ChaFileDefine.alphaState[b, 0], ChaFileDefine.alphaState[b, 1]);
				}
			}
		}
		if (!(base.objParts[kind] != null))
		{
			return;
		}
		InitBaseCustomTextureClothes(main: false, kind);
		if (base.loadWithDefaultColorAndPtn)
		{
			SetClothesDefaultColor(0);
			for (int num5 = 0; num5 < 4; num5++)
			{
				nowCoordinate.clothes.parts[0].colorInfo[num5].pattern = 0;
			}
		}
		ChangeCustomClothes(main: false, kind, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true, updateTex04: true);
		for (int num6 = 0; num6 < 2; num6++)
		{
			ChangeCustomEmblem(0, num6);
		}
		if (base.releaseCustomInputTexture)
		{
			ReleaseBaseCustomTextureClothes(main: true, kind, createTex: false);
		}
	}

	public void ChangeClothesBotNoAsync(int id, bool forceChange = false, bool update = true)
	{
		bool flag = true;
		bool flag2 = true;
		int kindNo = 1;
		string createName = "ct_clothesBot";
		hideShortsBot = false;
		int num = ((base.infoClothes[kindNo] == null) ? (-1) : base.infoClothes[kindNo].Id);
		if (!forceChange && null != base.objClothes[kindNo] && id == num)
		{
			flag = false;
			flag2 = false;
		}
		if (base.lstCtrl.CountInfo(ChaListDefine.CategoryNo.co_top) != 0)
		{
			ListInfoBase info = base.lstCtrl.GetInfo(ChaListDefine.CategoryNo.co_top, nowCoordinate.clothes.parts[0].id);
			if (info != null && "2" == info.GetInfo(ChaListDefine.KeyType.Coordinate))
			{
				flag = false;
				flag2 = true;
			}
		}
		ReleaseBaseCustomTextureClothes(main: true, kindNo);
		if (flag2 && (bool)base.objClothes[kindNo])
		{
			SafeDestroy(base.objClothes[kindNo]);
			base.objClothes[kindNo] = null;
			base.infoClothes[kindNo] = null;
			base.cusClothesCmp[kindNo] = null;
			ReleaseRefObject(6uL);
			RemoveClothesStateKind(kindNo);
		}
		if (flag)
		{
			base.objClothes[kindNo] = LoadCharaFbxData(delegate(ListInfoBase lib)
			{
				base.infoClothes[kindNo] = lib;
			}, base.hiPoly, 106, id, createName, copyDynamicBone: true, 1, base.objTop.transform, 0);
			if (base.objClothes[kindNo] != null)
			{
				ListInfoBase listInfoBase = base.infoClothes[kindNo];
				base.cusClothesCmp[kindNo] = base.objClothes[kindNo].GetComponent<ChaClothesComponent>();
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy")
				{
					_ = null == base.cusClothesCmp[kindNo];
				}
				if (listInfoBase.dictInfo.TryGetValue(106, out var value))
				{
					if (value == "1")
					{
						hideShortsBot = true;
					}
				}
				else if (ChaListDefine.botHideIds.Contains(id))
				{
					hideShortsBot = true;
				}
				nowCoordinate.clothes.parts[kindNo].id = listInfoBase.Id;
				CreateReferenceInfo(6uL, base.objClothes[kindNo]);
				AddClothesStateKind(kindNo, listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
			}
		}
		if (base.objClothes[kindNo] != null)
		{
			InitBaseCustomTextureClothes(main: true, kindNo);
			if (base.loadWithDefaultColorAndPtn)
			{
				SetClothesDefaultColor(kindNo);
				for (int num2 = 0; num2 < 4; num2++)
				{
					nowCoordinate.clothes.parts[kindNo].colorInfo[num2].pattern = 0;
				}
			}
			ChangeCustomClothes(main: true, kindNo, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true, updateTex04: false);
			for (int num3 = 0; num3 < 2; num3++)
			{
				ChangeCustomEmblem(kindNo, num3);
			}
			if (base.releaseCustomInputTexture)
			{
				ReleaseBaseCustomTextureClothes(main: true, kindNo, createTex: false);
			}
		}
		if (update)
		{
			UpdateSiru(forceChange: true);
		}
	}

	public void ChangeClothesBraNoAsync(int id, bool forceChange = false, bool update = true)
	{
		bool flag = true;
		bool flag2 = true;
		int kindNo = 2;
		string createName = "ct_bra";
		int num = ((base.infoClothes[kindNo] == null) ? (-1) : base.infoClothes[kindNo].Id);
		if (!forceChange && null != base.objClothes[kindNo] && id == num)
		{
			flag = false;
			flag2 = false;
		}
		ReleaseBaseCustomTextureClothes(main: true, kindNo);
		if (flag2 && (bool)base.objClothes[kindNo])
		{
			SafeDestroy(base.objClothes[kindNo]);
			base.objClothes[kindNo] = null;
			BustNormal value = null;
			if (dictBustNormal.TryGetValue(BustNormalKind.NmlBra, out value))
			{
				value.Release();
			}
			base.infoClothes[kindNo] = null;
			base.cusClothesCmp[kindNo] = null;
			ReleaseRefObject(7uL);
			notShorts = false;
			RemoveClothesStateKind(kindNo);
			base.rendBra = new Renderer[2];
		}
		if (flag)
		{
			base.objClothes[kindNo] = LoadCharaFbxData(delegate(ListInfoBase lib)
			{
				base.infoClothes[kindNo] = lib;
			}, base.hiPoly, 107, id, createName, copyDynamicBone: false, 1, base.objTop.transform, (base.sex == 0) ? ChaFileDefine.DefClothesMBraID : ChaFileDefine.DefClothesFBraID);
			if (base.objClothes[kindNo] != null)
			{
				ListInfoBase listInfoBase = base.infoClothes[kindNo];
				base.cusClothesCmp[kindNo] = base.objClothes[kindNo].GetComponent<ChaClothesComponent>();
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy")
				{
					_ = null == base.cusClothesCmp[kindNo];
				}
				notShorts = (("2" == listInfoBase.GetInfo(ChaListDefine.KeyType.Coordinate)) ? true : false);
				nowCoordinate.clothes.parts[kindNo].id = listInfoBase.Id;
				CreateReferenceInfo(7uL, base.objClothes[kindNo]);
				AddClothesStateKind(kindNo, listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
				GameObject referenceInfo = GetReferenceInfo(RefObjKey.ObjBraDef);
				if (null != referenceInfo)
				{
					base.rendBra[0] = referenceInfo.GetComponent<Renderer>();
				}
				referenceInfo = GetReferenceInfo(RefObjKey.ObjBraNuge);
				if (null != referenceInfo)
				{
					base.rendBra[1] = referenceInfo.GetComponent<Renderer>();
				}
				if (base.rendBra != null)
				{
					int num2 = ((2 == listInfoBase.GetInfoInt(ChaListDefine.KeyType.Coordinate)) ? 1 : 0);
					if (listInfoBase.dictInfo.TryGetValue(105, out var value2))
					{
						num2 = (("1" == value2) ? 1 : 0);
					}
					if (base.rendBra[0] != null)
					{
						base.rendBra[0].material.SetTexture(ChaShader._AlphaMask, texBraAlphaMask);
						base.rendBra[0].material.SetTextureOffset(ChaShader._AlphaMask, ChaListDefine.braOffset[num2]);
						base.rendBra[0].material.SetTextureScale(ChaShader._AlphaMask, ChaListDefine.braTiling[num2]);
					}
					if (base.rendBra[1] != null)
					{
						base.rendBra[1].material.SetTexture(ChaShader._AlphaMask, texBraAlphaMask);
						base.rendBra[1].material.SetTextureOffset(ChaShader._AlphaMask, ChaListDefine.braOffset[num2]);
						base.rendBra[1].material.SetTextureScale(ChaShader._AlphaMask, ChaListDefine.braTiling[num2]);
					}
				}
				byte b = base.fileStatus.clothesState[0];
				ChangeAlphaMask(ChaFileDefine.alphaState[b, 0], ChaFileDefine.alphaState[b, 1]);
				string info = listInfoBase.GetInfo(ChaListDefine.KeyType.NormalData);
				if ("0" != info)
				{
					BustNormal value3 = null;
					if (!dictBustNormal.TryGetValue(BustNormalKind.NmlBra, out value3))
					{
						value3 = new BustNormal();
					}
					value3.Init(base.objClothes[kindNo], listInfoBase.GetInfo(ChaListDefine.KeyType.MainAB), info, listInfoBase.GetInfo(ChaListDefine.KeyType.MainManifest));
					dictBustNormal[BustNormalKind.NmlBra] = value3;
				}
			}
		}
		if (base.objClothes[kindNo] != null)
		{
			InitBaseCustomTextureClothes(main: true, kindNo);
			if (base.loadWithDefaultColorAndPtn)
			{
				SetClothesDefaultColor(kindNo);
				for (int num3 = 0; num3 < 4; num3++)
				{
					nowCoordinate.clothes.parts[kindNo].colorInfo[num3].pattern = 0;
				}
			}
			ChangeCustomClothes(main: true, kindNo, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true, updateTex04: false);
			for (int num4 = 0; num4 < 2; num4++)
			{
				ChangeCustomEmblem(kindNo, num4);
			}
			if (base.releaseCustomInputTexture)
			{
				ReleaseBaseCustomTextureClothes(main: true, kindNo, createTex: false);
			}
		}
		if (update)
		{
			UpdateSiru(forceChange: true);
		}
		if ("0" == base.infoClothes[kindNo].GetInfo(ChaListDefine.KeyType.Coordinate))
		{
			int num5 = 3;
			if (null == base.objClothes[num5])
			{
				ChangeClothesShorts(nowCoordinate.clothes.parts[num5].id, forceChange: false, update);
			}
		}
	}

	public void ChangeClothesShortsNoAsync(int id, bool forceChange = false, bool update = true)
	{
		bool flag = true;
		bool flag2 = true;
		int kindNo = 3;
		string createName = "ct_shorts";
		int num = ((base.infoClothes[kindNo] == null) ? (-1) : base.infoClothes[kindNo].Id);
		if (!forceChange && null != base.objClothes[kindNo] && id == num)
		{
			flag = false;
			flag2 = false;
		}
		if (base.lstCtrl.CountInfo(ChaListDefine.CategoryNo.co_bra) != 0)
		{
			ListInfoBase info = base.lstCtrl.GetInfo(ChaListDefine.CategoryNo.co_bra, nowCoordinate.clothes.parts[2].id);
			if (info != null && "2" == info.GetInfo(ChaListDefine.KeyType.Coordinate))
			{
				flag = false;
				flag2 = true;
			}
		}
		ReleaseBaseCustomTextureClothes(main: true, kindNo);
		if (flag2 && (bool)base.objClothes[kindNo])
		{
			SafeDestroy(base.objClothes[kindNo]);
			base.objClothes[kindNo] = null;
			base.infoClothes[kindNo] = null;
			base.cusClothesCmp[kindNo] = null;
			ReleaseRefObject(8uL);
			RemoveClothesStateKind(kindNo);
		}
		if (flag)
		{
			base.objClothes[kindNo] = LoadCharaFbxData(delegate(ListInfoBase lib)
			{
				base.infoClothes[kindNo] = lib;
			}, base.hiPoly, 108, id, createName, copyDynamicBone: false, 1, base.objTop.transform, (base.sex == 0) ? ChaFileDefine.DefClothesMShortsID : ChaFileDefine.DefClothesFShortsID);
			if (base.objClothes[kindNo] != null)
			{
				ListInfoBase listInfoBase = base.infoClothes[kindNo];
				base.cusClothesCmp[kindNo] = base.objClothes[kindNo].GetComponent<ChaClothesComponent>();
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy")
				{
					_ = null == base.cusClothesCmp[kindNo];
				}
				if (listInfoBase.Id == 17)
				{
					for (int num2 = 0; num2 < base.cusClothesCmp[kindNo].rendNormal01.Length; num2++)
					{
						base.cusClothesCmp[kindNo].rendNormal01[num2].material.renderQueue = 3030;
					}
				}
				nowCoordinate.clothes.parts[kindNo].id = listInfoBase.Id;
				CreateReferenceInfo(8uL, base.objClothes[kindNo]);
				AddClothesStateKind(kindNo, listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
			}
		}
		if (base.objClothes[kindNo] != null)
		{
			InitBaseCustomTextureClothes(main: true, kindNo);
			if (base.loadWithDefaultColorAndPtn)
			{
				SetClothesDefaultColor(kindNo);
				for (int num3 = 0; num3 < 4; num3++)
				{
					nowCoordinate.clothes.parts[kindNo].colorInfo[num3].pattern = 0;
				}
			}
			ChangeCustomClothes(main: true, kindNo, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true, updateTex04: false);
			for (int num4 = 0; num4 < 2; num4++)
			{
				ChangeCustomEmblem(kindNo, num4);
			}
			if (base.releaseCustomInputTexture)
			{
				ReleaseBaseCustomTextureClothes(main: true, kindNo, createTex: false);
			}
		}
		if (update)
		{
			UpdateSiru(forceChange: true);
		}
	}

	public void ChangeClothesGlovesNoAsync(int id, bool forceChange = false)
	{
		bool flag = true;
		bool flag2 = true;
		int kindNo = 4;
		string createName = "ct_gloves";
		int num = ((base.infoClothes[kindNo] == null) ? (-1) : base.infoClothes[kindNo].Id);
		if (!forceChange && null != base.objClothes[kindNo] && id == num)
		{
			flag = false;
			flag2 = false;
		}
		ReleaseBaseCustomTextureClothes(main: true, kindNo);
		if (flag2 && (bool)base.objClothes[kindNo])
		{
			SafeDestroy(base.objClothes[kindNo]);
			base.objClothes[kindNo] = null;
			base.infoClothes[kindNo] = null;
			base.cusClothesCmp[kindNo] = null;
			RemoveClothesStateKind(kindNo);
		}
		if (flag)
		{
			base.objClothes[kindNo] = LoadCharaFbxData(delegate(ListInfoBase lib)
			{
				base.infoClothes[kindNo] = lib;
			}, base.hiPoly, 109, id, createName, copyDynamicBone: false, 1, base.objTop.transform, 0);
			if (base.objClothes[kindNo] != null)
			{
				ListInfoBase listInfoBase = base.infoClothes[kindNo];
				base.cusClothesCmp[kindNo] = base.objClothes[kindNo].GetComponent<ChaClothesComponent>();
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy")
				{
					_ = null == base.cusClothesCmp[kindNo];
				}
				nowCoordinate.clothes.parts[kindNo].id = listInfoBase.Id;
				AddClothesStateKind(kindNo, listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
			}
		}
		if (!(base.objClothes[kindNo] != null))
		{
			return;
		}
		InitBaseCustomTextureClothes(main: true, kindNo);
		if (base.loadWithDefaultColorAndPtn)
		{
			SetClothesDefaultColor(kindNo);
			for (int num2 = 0; num2 < 4; num2++)
			{
				nowCoordinate.clothes.parts[kindNo].colorInfo[num2].pattern = 0;
			}
		}
		ChangeCustomClothes(main: true, kindNo, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true, updateTex04: false);
		for (int num3 = 0; num3 < 2; num3++)
		{
			ChangeCustomEmblem(kindNo, num3);
		}
		if (base.releaseCustomInputTexture)
		{
			ReleaseBaseCustomTextureClothes(main: true, kindNo, createTex: false);
		}
	}

	public void ChangeClothesPanstNoAsync(int id, bool forceChange = false, bool update = true)
	{
		bool flag = true;
		bool flag2 = true;
		int kindNo = 5;
		string createName = "ct_panst";
		int num = ((base.infoClothes[kindNo] == null) ? (-1) : base.infoClothes[kindNo].Id);
		if (!forceChange && null != base.objClothes[kindNo] && id == num)
		{
			flag = false;
			flag2 = false;
		}
		ReleaseBaseCustomTextureClothes(main: true, kindNo);
		if (flag2 && (bool)base.objClothes[kindNo])
		{
			SafeDestroy(base.objClothes[kindNo]);
			base.objClothes[kindNo] = null;
			base.infoClothes[kindNo] = null;
			base.cusClothesCmp[kindNo] = null;
			ReleaseRefObject(10uL);
			RemoveClothesStateKind(kindNo);
		}
		if (flag)
		{
			base.objClothes[kindNo] = LoadCharaFbxData(delegate(ListInfoBase lib)
			{
				base.infoClothes[kindNo] = lib;
			}, base.hiPoly, 110, id, createName, copyDynamicBone: false, 1, base.objTop.transform, 0);
			if (base.objClothes[kindNo] != null)
			{
				ListInfoBase listInfoBase = base.infoClothes[kindNo];
				base.cusClothesCmp[kindNo] = base.objClothes[kindNo].GetComponent<ChaClothesComponent>();
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy")
				{
					_ = null == base.cusClothesCmp[kindNo];
				}
				if (listInfoBase.Id == 11)
				{
					for (int num2 = 0; num2 < base.cusClothesCmp[kindNo].rendNormal01.Length; num2++)
					{
						base.cusClothesCmp[kindNo].rendNormal01[num2].material.renderQueue = 3040;
					}
				}
				nowCoordinate.clothes.parts[kindNo].id = listInfoBase.Id;
				CreateReferenceInfo(10uL, base.objClothes[kindNo]);
				AddClothesStateKind(kindNo, listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
			}
		}
		if (base.objClothes[kindNo] != null)
		{
			InitBaseCustomTextureClothes(main: true, kindNo);
			if (base.loadWithDefaultColorAndPtn)
			{
				SetClothesDefaultColor(kindNo);
				for (int num3 = 0; num3 < 4; num3++)
				{
					nowCoordinate.clothes.parts[kindNo].colorInfo[num3].pattern = 0;
				}
			}
			ChangeCustomClothes(main: true, kindNo, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true, updateTex04: false);
			for (int num4 = 0; num4 < 2; num4++)
			{
				ChangeCustomEmblem(kindNo, num4);
			}
			if (base.releaseCustomInputTexture)
			{
				ReleaseBaseCustomTextureClothes(main: true, kindNo, createTex: false);
			}
		}
		if (update)
		{
			UpdateSiru(forceChange: true);
		}
	}

	public void ChangeClothesSocksNoAsync(int id, bool forceChange = false)
	{
		bool flag = true;
		bool flag2 = true;
		int kindNo = 6;
		string createName = "ct_socks";
		int num = ((base.infoClothes[kindNo] == null) ? (-1) : base.infoClothes[kindNo].Id);
		if (!forceChange && null != base.objClothes[kindNo] && id == num)
		{
			flag = false;
			flag2 = false;
		}
		ReleaseBaseCustomTextureClothes(main: true, kindNo);
		if (flag2 && (bool)base.objClothes[kindNo])
		{
			SafeDestroy(base.objClothes[kindNo]);
			base.objClothes[kindNo] = null;
			base.infoClothes[kindNo] = null;
			base.cusClothesCmp[kindNo] = null;
			RemoveClothesStateKind(kindNo);
		}
		if (flag)
		{
			base.objClothes[kindNo] = LoadCharaFbxData(delegate(ListInfoBase lib)
			{
				base.infoClothes[kindNo] = lib;
			}, base.hiPoly, 111, id, createName, copyDynamicBone: false, 1, base.objTop.transform, 0);
			if (base.objClothes[kindNo] != null)
			{
				ListInfoBase listInfoBase = base.infoClothes[kindNo];
				base.cusClothesCmp[kindNo] = base.objClothes[kindNo].GetComponent<ChaClothesComponent>();
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy")
				{
					_ = null == base.cusClothesCmp[kindNo];
				}
				if (listInfoBase.Id == 21)
				{
					for (int num2 = 0; num2 < base.cusClothesCmp[kindNo].rendNormal01.Length; num2++)
					{
						base.cusClothesCmp[kindNo].rendNormal01[num2].material.renderQueue = 3050;
					}
				}
				nowCoordinate.clothes.parts[kindNo].id = listInfoBase.Id;
				AddClothesStateKind(kindNo, listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
			}
		}
		if (!(base.objClothes[kindNo] != null))
		{
			return;
		}
		InitBaseCustomTextureClothes(main: true, kindNo);
		if (base.loadWithDefaultColorAndPtn)
		{
			SetClothesDefaultColor(kindNo);
			for (int num3 = 0; num3 < 4; num3++)
			{
				nowCoordinate.clothes.parts[kindNo].colorInfo[num3].pattern = 0;
			}
		}
		ChangeCustomClothes(main: true, kindNo, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true, updateTex04: false);
		for (int num4 = 0; num4 < 2; num4++)
		{
			ChangeCustomEmblem(kindNo, num4);
		}
		if (base.releaseCustomInputTexture)
		{
			ReleaseBaseCustomTextureClothes(main: true, kindNo, createTex: false);
		}
	}

	public void ChangeClothesShoesNoAsync(int type, int id, bool forceChange = false)
	{
		bool flag = true;
		bool flag2 = true;
		int kindNo = 0;
		string text = "";
		int num = 0;
		if (type == 0)
		{
			kindNo = 7;
			text = "ct_shoes_inner";
			num = 0;
		}
		else
		{
			kindNo = 8;
			text = "ct_shoes_outer";
			num = 0;
		}
		int num2 = ((base.infoClothes[kindNo] == null) ? (-1) : base.infoClothes[kindNo].Id);
		if (!forceChange && null != base.objClothes[kindNo] && id == num2)
		{
			flag = false;
			flag2 = false;
		}
		ReleaseBaseCustomTextureClothes(main: true, kindNo);
		if (flag2 && (bool)base.objClothes[kindNo])
		{
			SafeDestroy(base.objClothes[kindNo]);
			base.objClothes[kindNo] = null;
			base.infoClothes[kindNo] = null;
			base.cusClothesCmp[kindNo] = null;
			RemoveClothesStateKind(kindNo);
		}
		if (flag)
		{
			base.objClothes[kindNo] = LoadCharaFbxData(delegate(ListInfoBase lib)
			{
				base.infoClothes[kindNo] = lib;
			}, base.hiPoly, (base.sex == 0 && 1 == base.exType) ? 504 : 112, id, text, copyDynamicBone: false, 1, base.objTop.transform, num);
			if (base.objClothes[kindNo] != null)
			{
				ListInfoBase listInfoBase = base.infoClothes[kindNo];
				base.cusClothesCmp[kindNo] = base.objClothes[kindNo].GetComponent<ChaClothesComponent>();
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy")
				{
					_ = null == base.cusClothesCmp[kindNo];
				}
				nowCoordinate.clothes.parts[kindNo].id = listInfoBase.Id;
				AddClothesStateKind(kindNo, listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
			}
		}
		if (!(base.objClothes[kindNo] != null))
		{
			return;
		}
		InitBaseCustomTextureClothes(main: true, kindNo);
		if (base.loadWithDefaultColorAndPtn)
		{
			SetClothesDefaultColor(kindNo);
			for (int num3 = 0; num3 < 4; num3++)
			{
				nowCoordinate.clothes.parts[kindNo].colorInfo[num3].pattern = 0;
			}
		}
		ChangeCustomClothes(main: true, kindNo, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true, updateTex04: false);
		for (int num4 = 0; num4 < 2; num4++)
		{
			ChangeCustomEmblem(kindNo, num4);
		}
		if (base.releaseCustomInputTexture)
		{
			ReleaseBaseCustomTextureClothes(main: true, kindNo, createTex: false);
		}
	}

	public void ChangeAccessoryNoAsync(int slotNo, int type, int id, string parentKey, bool forceChange = false, bool update = true)
	{
		if (!MathfEx.RangeEqualOn(0, slotNo, 19))
		{
			return;
		}
		ListInfoBase listInfoBase = null;
		bool flag = true;
		bool flag2 = true;
		if ((!Game.isAddH) ? (120 == type || !MathfEx.RangeEqualOn(121, type, 129)) : (120 == type || !MathfEx.RangeEqualOn(121, type, 130)))
		{
			flag2 = true;
			flag = false;
		}
		else
		{
			if (-1 == id)
			{
				flag2 = false;
				flag = false;
			}
			int num = ((base.infoAccessory[slotNo] == null) ? (-1) : base.infoAccessory[slotNo].Category);
			int num2 = ((base.infoAccessory[slotNo] == null) ? (-1) : base.infoAccessory[slotNo].Id);
			if (!forceChange && null != base.objAccessory[slotNo] && type == num && id == num2)
			{
				flag = false;
				flag2 = false;
			}
			if (-1 != id)
			{
				if (!base.lstCtrl.ContainsCategoryInfo((ChaListDefine.CategoryNo)type))
				{
					flag2 = true;
					flag = false;
				}
				else
				{
					listInfoBase = base.lstCtrl.GetInfo((ChaListDefine.CategoryNo)type, id);
					if (listInfoBase == null)
					{
						flag2 = true;
						flag = false;
					}
					else if (!base.hiPoly)
					{
						bool flag3 = true;
						if (123 == type && 1 == listInfoBase.Kind)
						{
							flag3 = false;
						}
						if (122 == type && 1 == listInfoBase.GetInfoInt(ChaListDefine.KeyType.HideHair))
						{
							flag3 = false;
						}
						if (Manager.Config.EtcData.loadHeadAccessory && 122 == type && 1 == listInfoBase.Kind)
						{
							flag3 = false;
						}
						if (Manager.Config.EtcData.loadAllAccessory)
						{
							flag3 = false;
						}
						if (flag3)
						{
							flag2 = true;
							flag = false;
						}
					}
				}
			}
		}
		if (flag2)
		{
			if (!flag)
			{
				nowCoordinate.accessory.parts[slotNo].MemberInit();
				nowCoordinate.accessory.parts[slotNo].type = 120;
			}
			if ((bool)base.objAccessory[slotNo])
			{
				SafeDestroy(base.objAccessory[slotNo]);
				base.objAccessory[slotNo] = null;
				base.infoAccessory[slotNo] = null;
				base.cusAcsCmp[slotNo] = null;
				for (int i = 0; i < 2; i++)
				{
					base.objAcsMove[slotNo, i] = null;
				}
			}
		}
		if (flag)
		{
			byte copyWeights = 0;
			Transform trfParent = null;
			if ("null" == listInfoBase.GetInfo(ChaListDefine.KeyType.Parent))
			{
				copyWeights = 2;
				trfParent = base.objTop.transform;
			}
			base.objAccessory[slotNo] = LoadCharaFbxData(delegate(ListInfoBase l)
			{
				base.infoAccessory[slotNo] = l;
			}, _hiPoly: true, type, id, "ca_slot" + slotNo.ToString("00"), copyDynamicBone: false, copyWeights, trfParent, -1);
			if ((bool)base.objAccessory[slotNo])
			{
				listInfoBase = base.infoAccessory[slotNo];
				base.cusAcsCmp[slotNo] = base.objAccessory[slotNo].GetComponent<ChaAccessoryComponent>();
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy")
				{
					_ = null == base.cusAcsCmp[slotNo];
				}
				nowCoordinate.accessory.parts[slotNo].type = type;
				nowCoordinate.accessory.parts[slotNo].id = listInfoBase.Id;
				base.objAcsMove[slotNo, 0] = base.objAccessory[slotNo].transform.FindLoop("N_move");
				base.objAcsMove[slotNo, 1] = base.objAccessory[slotNo].transform.FindLoop("N_move2");
			}
		}
		if (base.objAccessory[slotNo] != null)
		{
			if (base.loadWithDefaultColorAndPtn)
			{
				SetAccessoryDefaultColor(slotNo);
			}
			ChangeAccessoryColor(slotNo);
			if ("" == parentKey)
			{
				parentKey = listInfoBase.GetInfo(ChaListDefine.KeyType.Parent);
			}
			ChangeAccessoryParent(slotNo, parentKey);
			UpdateAccessoryMoveFromInfo(slotNo);
			nowCoordinate.accessory.parts[slotNo].partsOfHead = ChaAccessoryDefine.CheckPartsOfHead(parentKey);
			if (!base.hiPoly && !Manager.Config.EtcData.loadAllAccessory)
			{
				DynamicBone[] componentsInChildren = base.objAccessory[slotNo].GetComponentsInChildren<DynamicBone>(includeInactive: true);
				for (int num3 = 0; num3 < componentsInChildren.Length; num3++)
				{
					componentsInChildren[num3].enabled = false;
				}
			}
			ChangeShakeAccessory(slotNo);
		}
		if (update)
		{
			SetHideHairAccessory();
		}
	}

	private void LoadCharaFbxDataNoAsync(Action<GameObject> actObj, Action<ListInfoBase> actListInfo, bool _hiPoly, int category, int id, string createName, bool copyDynamicBone, byte copyWeights, Transform trfParent, int defaultId, bool worldPositionStays = false)
	{
		if (base.lstCtrl.CountInfo((ChaListDefine.CategoryNo)category) == 0)
		{
			actObj(null);
			return;
		}
		ListInfoBase info = base.lstCtrl.GetInfo((ChaListDefine.CategoryNo)category, id);
		if (info == null)
		{
			if (-1 == defaultId)
			{
				actObj(null);
				return;
			}
			if (id != defaultId)
			{
				info = base.lstCtrl.GetInfo((ChaListDefine.CategoryNo)category, defaultId);
			}
			if (info == null)
			{
				info = base.lstCtrl.GetInfo((ChaListDefine.CategoryNo)category, 0);
				if (info == null)
				{
					actObj(null);
					return;
				}
			}
		}
		else if (category == 105 || category == 107)
		{
			int infoInt = info.GetInfoInt(ChaListDefine.KeyType.Sex);
			bool flag = false;
			if (base.sex == 0 && 3 == infoInt)
			{
				flag = true;
			}
			else if (1 == base.sex && 2 == infoInt)
			{
				flag = true;
			}
			if (flag)
			{
				if (id != defaultId)
				{
					info = base.lstCtrl.GetInfo((ChaListDefine.CategoryNo)category, defaultId);
				}
				if (info == null)
				{
					info = base.lstCtrl.GetInfo((ChaListDefine.CategoryNo)category, 0);
					if (info == null)
					{
						actObj(null);
						return;
					}
				}
			}
		}
		if (category == 501)
		{
			info = base.lstCtrl.GetInfo(ChaListDefine.CategoryNo.ex_bo_head, 0);
			if (info == null)
			{
				actObj(null);
				return;
			}
		}
		string text = info.GetInfo(ChaListDefine.KeyType.MainData);
		if ("" == text)
		{
			return;
		}
		if (!_hiPoly)
		{
			text += "_low";
		}
		string info2 = info.GetInfo(ChaListDefine.KeyType.MainManifest);
		string info3 = info.GetInfo(ChaListDefine.KeyType.MainAB);
		GameObject gameObject = null;
		gameObject = CommonLib.LoadAsset<GameObject>(info3, text, clone: true, info2);
		Character.AddLoadAssetBundle(info3, info2);
		if (null == gameObject)
		{
			actObj(null);
			return;
		}
		gameObject.name = createName;
		if ((bool)trfParent)
		{
			gameObject.transform.SetParent(trfParent, worldPositionStays);
		}
		DynamicBone[] componentsInChildren = gameObject.GetComponentsInChildren<DynamicBone>(includeInactive: true);
		if (!((IReadOnlyCollection<DynamicBone>)(object)componentsInChildren).IsNullOrEmpty())
		{
			DynamicBoneCollider[] componentsInChildren2 = base.objBodyBone.GetComponentsInChildren<DynamicBoneCollider>(includeInactive: true);
			Dictionary<string, GameObject> dictBone = aaWeightsBody.dictBone;
			DynamicBone[] array = componentsInChildren;
			foreach (DynamicBone dynamicBone in array)
			{
				if (copyDynamicBone)
				{
					if ((bool)dynamicBone.m_Root)
					{
						foreach (KeyValuePair<string, GameObject> item in dictBone)
						{
							if (!(item.Key != dynamicBone.m_Root.name))
							{
								dynamicBone.m_Root = item.Value.transform;
								break;
							}
						}
					}
					if (dynamicBone.m_Exclusions != null && dynamicBone.m_Exclusions.Count != 0)
					{
						for (int j = 0; j < dynamicBone.m_Exclusions.Count; j++)
						{
							if (null == dynamicBone.m_Exclusions[j])
							{
								continue;
							}
							foreach (KeyValuePair<string, GameObject> item2 in dictBone)
							{
								if (!(item2.Key != dynamicBone.m_Exclusions[j].name))
								{
									dynamicBone.m_Exclusions[j] = item2.Value.transform;
									break;
								}
							}
						}
					}
					if (dynamicBone.m_notRolls != null && dynamicBone.m_notRolls.Count != 0)
					{
						for (int k = 0; k < dynamicBone.m_notRolls.Count; k++)
						{
							if (null == dynamicBone.m_notRolls[k])
							{
								continue;
							}
							foreach (KeyValuePair<string, GameObject> item3 in dictBone)
							{
								if (!(item3.Key != dynamicBone.m_notRolls[k].name))
								{
									dynamicBone.m_notRolls[k] = item3.Value.transform;
									break;
								}
							}
						}
					}
				}
				if (dynamicBone.m_Colliders != null)
				{
					dynamicBone.m_Colliders.Clear();
					for (int l = 0; l < componentsInChildren2.Length; l++)
					{
						dynamicBone.m_Colliders.Add(componentsInChildren2[l]);
					}
				}
			}
		}
		GameObject referenceInfo = GetReferenceInfo(RefObjKey.A_ROOTBONE);
		Transform rootBone = (referenceInfo ? referenceInfo.transform : null);
		if (1 == copyWeights)
		{
			aaWeightsBody.AssignedWeightsAndSetBounds(gameObject, "cf_j_root", bounds, rootBone);
		}
		else if (2 == copyWeights)
		{
			aaWeightsHead.AssignedWeightsAndSetBounds(gameObject, "cf_J_N_FaceRoot", bounds, rootBone);
		}
		gameObject.AddComponent<ListInfoComponent>().data = info;
		actObj(gameObject);
		actListInfo(info);
	}

	protected void InitializeControlFaceAll()
	{
		fbsaaVoice = new AudioAssist();
		InitializeControlFaceObject();
	}

	protected void InitializeControlFaceObject()
	{
		asVoice = null;
		texHairGloss = null;
	}

	protected void ReleaseControlFaceAll()
	{
		ReleaseControlFaceObject(init: false);
	}

	protected void ReleaseControlFaceObject(bool init = true)
	{
		if (init)
		{
			InitializeControlFaceObject();
		}
	}

	public void HideEyeHighlight(bool hide)
	{
		base.fileStatus.hideEyesHighlight = hide;
		float value = (hide ? 0f : 1f);
		if (base.rendEye == null)
		{
			return;
		}
		Renderer[] array = base.rendEye;
		foreach (Renderer renderer in array)
		{
			if (!(null == renderer))
			{
				Material material = renderer.material;
				if (null != material)
				{
					material.SetFloat(ChaShader._isHighLight, value);
				}
			}
		}
	}

	public void ChangeEyesShaking(bool enable)
	{
		base.fileStatus.eyesYure = enable;
		if (base.eyeLookMatCtrl != null)
		{
			EyeLookMaterialControll[] array = base.eyeLookMatCtrl;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].ChangeShaking(enable);
			}
		}
	}

	public bool GetEyesShaking()
	{
		return base.fileStatus.eyesYure;
	}

	public int GetEyesPtnNum()
	{
		return base.lstCtrl.CountInfo(ChaListDefine.CategoryNo.cha_eyeset);
	}

	public void ChangeEyesPtn(int ptn, bool blend = true)
	{
		if (base.eyesCtrl == null)
		{
			return;
		}
		base.fileStatus.eyesPtn = ptn;
		ListInfoBase info = base.lstCtrl.GetInfo(ChaListDefine.CategoryNo.cha_eyeset, ptn);
		if (info == null)
		{
			base.fileStatus.eyesPtn = (ptn = 0);
			info = base.lstCtrl.GetInfo(ChaListDefine.CategoryNo.cha_eyeset, ptn);
		}
		if (info == null)
		{
			return;
		}
		GameObject referenceInfo = GetReferenceInfo(RefObjKey.N_EyeBase);
		if ((bool)referenceInfo)
		{
			referenceInfo.SetActiveIfDifferent(1 == info.GetInfoInt(ChaListDefine.KeyType.EyeBase));
		}
		GameObject referenceInfo2 = GetReferenceInfo(RefObjKey.N_Hitomi);
		if ((bool)referenceInfo2)
		{
			referenceInfo2.SetActiveIfDifferent(info.GetInfoInt(ChaListDefine.KeyType.EyeHitomi) != 0);
		}
		GameObject[] array = new GameObject[3]
		{
			GetReferenceInfo(RefObjKey.N_Gag00),
			GetReferenceInfo(RefObjKey.N_Gag01),
			GetReferenceInfo(RefObjKey.N_Gag02)
		};
		int infoInt = info.GetInfoInt(ChaListDefine.KeyType.EyeObjNo);
		for (int i = 0; i < array.Length; i++)
		{
			if ((bool)array[i])
			{
				array[i].SetActiveIfDifferent(i + 1 == infoInt);
			}
		}
		if (base.rendEye != null && 2 == base.rendEye.Length)
		{
			Texture value = null;
			string info2 = info.GetInfo(ChaListDefine.KeyType.EpsTexAB);
			string text = info.GetInfo(ChaListDefine.KeyType.EpsTex);
			float value2 = 0f;
			if ("0" != info2 && "0" != text)
			{
				if (!base.hiPoly)
				{
					text += "_low";
				}
				value = CommonLib.LoadAsset<Texture2D>(info2, text);
				value2 = 1f;
			}
			Renderer[] array2 = base.rendEye;
			foreach (Renderer renderer in array2)
			{
				if (!(null == renderer))
				{
					renderer.material.SetTexture(ChaShader._expression, value);
					renderer.material.SetFloat(ChaShader._exppower, value2);
				}
			}
		}
		if (array != null && array.Length != 0 && infoInt != 0)
		{
			Texture tex = null;
			string info3 = info.GetInfo(ChaListDefine.KeyType.MainTexAB);
			string text2 = info.GetInfo(ChaListDefine.KeyType.MainTex);
			if ("0" != info3 && "0" != text2)
			{
				if (!base.hiPoly)
				{
					text2 += "_low";
				}
				tex = CommonLib.LoadAsset<Texture2D>(info3, text2);
			}
			string[] array3 = info.GetInfo(ChaListDefine.KeyType.TileAnim).Split('/');
			Vector4 zero = Vector4.zero;
			if (3 <= array3.Length)
			{
				float.TryParse(array3[0], out zero.x);
				float.TryParse(array3[1], out zero.y);
				float.TryParse(array3[2], out zero.z);
			}
			float infoFloat = info.GetInfoFloat(ChaListDefine.KeyType.SizeSpeed);
			float infoFloat2 = info.GetInfoFloat(ChaListDefine.KeyType.SizeWidth);
			float infoFloat3 = info.GetInfoFloat(ChaListDefine.KeyType.AngleSpeed);
			float infoFloat4 = info.GetInfoFloat(ChaListDefine.KeyType.Yurayura);
			ChangeGagEyesMaterial(infoInt - 1, tex, zero, infoFloat, infoFloat2, infoFloat3, infoFloat4);
		}
		SetForegroundEyesAndEyebrow();
		ChangeSettingEyeShadowColor();
		base.eyesCtrl.ChangePtn(info.GetInfoInt(ChaListDefine.KeyType.EyesPtn), blend);
	}

	public int GetEyesPtn()
	{
		return base.fileStatus.eyesPtn;
	}

	public void ChangeGagEyesMaterial(int no, Texture tex, Vector4 v4TileAnim, float sizeSpeed, float sizeWidth, float angleSpeed, float yurayura)
	{
		GameObject gameObject = null;
		switch (no)
		{
		case 0:
			gameObject = GetReferenceInfo(RefObjKey.N_Gag00);
			break;
		case 1:
			gameObject = GetReferenceInfo(RefObjKey.N_Gag01);
			break;
		case 2:
			gameObject = GetReferenceInfo(RefObjKey.N_Gag02);
			break;
		}
		if (null == gameObject)
		{
			return;
		}
		SkinnedMeshRenderer component = gameObject.GetComponent<SkinnedMeshRenderer>();
		if (!(null == component))
		{
			base.matGag[no].SetTexture(ChaShader._MainTex, null);
			base.matGag[no].SetTexture(ChaShader._MainTex, tex);
			base.matGag[no].SetVector(ChaShader._TileAnimation, v4TileAnim);
			base.matGag[no].SetFloat(ChaShader._SizeSpeed, sizeSpeed);
			base.matGag[no].SetFloat(ChaShader._SizeWidth, sizeWidth);
			base.matGag[no].SetFloat(ChaShader._angleSpeed, angleSpeed);
			base.matGag[no].SetFloat(ChaShader._yurayura, yurayura);
			component.material = base.matGag[no];
			if (Character.enableCharaLoadGCClear)
			{
				Resources.UnloadUnusedAssets();
			}
		}
	}

	public void ChangeEyesOpenMax(float maxValue)
	{
		if (base.eyesCtrl != null)
		{
			float num = Mathf.Clamp(maxValue, 0f, 1f);
			base.fileStatus.eyesOpenMax = num;
			base.eyesCtrl.OpenMax = Mathf.Clamp(maxValue, 0f, 0.92f);
			if (!base.fileStatus.eyesBlink)
			{
				base.eyesCtrl.SetOpenRateForce(num);
			}
		}
	}

	public float GetEyesOpenMax()
	{
		return base.fileStatus.eyesOpenMax;
	}

	public void ChangeEyebrowPtn(int ptn, bool blend = true)
	{
		if (base.eyebrowCtrl != null)
		{
			base.fileStatus.eyebrowPtn = ptn;
			base.eyebrowCtrl.ChangePtn(ptn, blend);
		}
	}

	public int GetEyebrowPtn()
	{
		return base.fileStatus.eyebrowPtn;
	}

	public void ChangeEyebrowOpenMax(float maxValue)
	{
		if (base.eyebrowCtrl != null)
		{
			float num = Mathf.Clamp(maxValue, 0f, 1f);
			base.fileStatus.eyebrowOpenMax = num;
			base.eyebrowCtrl.OpenMax = num;
			if (!base.fileStatus.eyesBlink)
			{
				base.eyebrowCtrl.SetOpenRateForce(1f);
			}
		}
	}

	public float GetEyebrowOpenMax()
	{
		return base.fileStatus.eyebrowOpenMax;
	}

	public void ChangeEyesBlinkFlag(bool blink)
	{
		if (!(null == base.fbsCtrl) && base.fbsCtrl.BlinkCtrl != null)
		{
			base.fileStatus.eyesBlink = blink;
			base.fbsCtrl.BlinkCtrl.SetFixedFlags((!blink) ? ((byte)1) : ((byte)0));
			if (!blink)
			{
				base.eyesCtrl.SetOpenRateForce(1f);
				base.eyebrowCtrl.SetOpenRateForce(1f);
			}
		}
	}

	public bool GetEyesBlinkFlag()
	{
		return base.fileStatus.eyesBlink;
	}

	public void ChangeMouthPtn(int ptn, bool blend = true)
	{
		if (base.mouthCtrl != null)
		{
			base.fileStatus.mouthPtn = ptn;
			base.mouthCtrl.ChangePtn(ptn, blend);
			ChangeTongueState((ptn == 21) ? ((byte)1) : ((byte)0));
			base.mouthCtrl.UseAdjustWidthScale(ptn != 21 && ptn != 22);
		}
	}

	public int GetMouthPtn()
	{
		return base.fileStatus.mouthPtn;
	}

	public void ChangeMouthOpenMax(float maxValue)
	{
		if (base.mouthCtrl != null)
		{
			float num = Mathf.Clamp(maxValue, 0f, 1f);
			base.fileStatus.mouthOpenMax = num;
			base.mouthCtrl.OpenMax = num;
			if (base.fileStatus.mouthFixed)
			{
				base.mouthCtrl.FixedRate = num;
			}
		}
	}

	public float GetMouthOpenMax()
	{
		return base.fileStatus.mouthOpenMax;
	}

	public void ChangeMouthFixed(bool fix)
	{
		if (base.mouthCtrl != null)
		{
			base.fileStatus.mouthFixed = fix;
			if (fix)
			{
				base.mouthCtrl.FixedRate = base.fileStatus.mouthOpenMax;
			}
			else
			{
				base.mouthCtrl.FixedRate = -1f;
			}
		}
	}

	public bool GetMouthFixed()
	{
		return base.fileStatus.mouthFixed;
	}

	public void ChangeTongueState(byte state)
	{
		base.fileStatus.tongueState = state;
	}

	public byte GetTongueState()
	{
		return base.fileStatus.tongueState;
	}

	public bool SetLipSync(AudioSource audio)
	{
		asVoice = audio;
		return audio != null;
	}

	private void UpdateBlendShapeVoice()
	{
		float voiceVaule = 0f;
		float correct = 3f;
		if ((bool)asVoice && asVoice.isPlaying)
		{
			voiceVaule = ((base.wavInfoData == null) ? fbsaaVoice.GetAudioWaveValue(asVoice, correct) : base.wavInfoData.GetValue(asVoice.time));
		}
		if ((bool)base.fbsCtrl)
		{
			base.fbsCtrl.SetVoiceVaule(voiceVaule);
		}
		if (!base.fileStatus.mouthAdjustWidth || !base.objHeadBone)
		{
			return;
		}
		GameObject referenceInfo = GetReferenceInfo(RefObjKey.F_ADJUSTWIDTHSCALE);
		if (null != referenceInfo)
		{
			float x = 1f;
			if (base.mouthCtrl != null)
			{
				x = base.mouthCtrl.GetAdjustWidthScale();
			}
			referenceInfo.transform.SetLocalScaleX(x);
		}
	}

	public void ChangeLookEyesTarget(int targetType, Transform trfTarg = null, float rate = 0.5f, float rotDeg = 0f, float range = 1f, float dis = 2f)
	{
		if (null == base.eyeLookCtrl)
		{
			return;
		}
		if (-1 == targetType)
		{
			targetType = base.fileStatus.eyesTargetType;
		}
		else
		{
			base.fileStatus.eyesTargetType = targetType;
		}
		base.eyeLookCtrl.target = null;
		if ((bool)trfTarg)
		{
			base.eyeLookCtrl.target = trfTarg;
			return;
		}
		if (targetType == 0)
		{
			if ((bool)Camera.main)
			{
				base.eyeLookCtrl.target = Camera.main.transform;
			}
		}
		else if ((bool)base.objEyesLookTarget && (bool)base.objEyesLookTargetP)
		{
			switch (targetType)
			{
			case 1:
				rotDeg = 0f;
				range = 1f;
				break;
			case 2:
				rotDeg = 45f;
				range = 1f;
				break;
			case 3:
				rotDeg = 90f;
				range = 1f;
				break;
			case 4:
				rotDeg = 135f;
				range = 1f;
				break;
			case 5:
				rotDeg = 180f;
				range = 1f;
				break;
			case 6:
				rotDeg = 225f;
				range = 1f;
				break;
			case 7:
				rotDeg = 270f;
				range = 1f;
				break;
			case 8:
				rotDeg = 315f;
				range = 1f;
				break;
			}
			base.objEyesLookTargetP.transform.SetLocalPosition(0f, 0.07f, 0f);
			base.eyeLookCtrl.target = base.objEyesLookTarget.transform;
			float y = Mathf.Lerp(0f, range, rate);
			base.eyeLookCtrl.target.SetLocalPosition(0f, y, dis);
			base.objEyesLookTargetP.transform.localEulerAngles = new Vector3(0f, 0f, 360f - rotDeg);
		}
		base.fileStatus.eyesTargetAngle = rotDeg;
		base.fileStatus.eyesTargetRange = range;
		base.fileStatus.eyesTargetRate = rate;
	}

	public void ChangeLookEyesPtn(int ptn)
	{
		if (!(null == base.eyeLookCtrl))
		{
			EyeLookController eyeLookController = base.eyeLookCtrl;
			int ptnNo = (base.fileStatus.eyesLookPtn = ptn);
			eyeLookController.ptnNo = ptnNo;
		}
	}

	public int GetLookEyesPtn()
	{
		return base.fileStatus.eyesLookPtn;
	}

	public void ChangeLookNeckTarget(int targetType, Transform trfTarg = null, float rate = 0.5f, float rotDeg = 0f, float range = 1f, float dis = 0.8f)
	{
		if (null == base.neckLookCtrl)
		{
			return;
		}
		if (-1 == targetType)
		{
			targetType = base.fileStatus.neckTargetType;
		}
		else
		{
			base.fileStatus.neckTargetType = targetType;
		}
		base.neckLookCtrl.target = null;
		if ((bool)trfTarg)
		{
			base.neckLookCtrl.target = trfTarg;
			return;
		}
		if (targetType == 0)
		{
			if ((bool)Camera.main)
			{
				base.neckLookCtrl.target = Camera.main.transform;
			}
		}
		else if ((bool)base.objNeckLookTarget && (bool)base.objNeckLookTargetP)
		{
			switch (targetType)
			{
			case 1:
				rotDeg = 0f;
				range = 1f;
				break;
			case 2:
				rotDeg = 45f;
				range = 1f;
				break;
			case 3:
				rotDeg = 90f;
				range = 1f;
				break;
			case 4:
				rotDeg = 135f;
				range = 1f;
				break;
			case 5:
				rotDeg = 180f;
				range = 1f;
				break;
			case 6:
				rotDeg = 225f;
				range = 1f;
				break;
			case 7:
				rotDeg = 270f;
				range = 1f;
				break;
			case 8:
				rotDeg = 315f;
				range = 1f;
				break;
			}
			base.objNeckLookTargetP.transform.SetLocalPosition(0f, 0.27f, 0f);
			base.neckLookCtrl.target = base.objNeckLookTarget.transform;
			float y = Mathf.Lerp(0f, range, rate);
			base.neckLookCtrl.target.SetLocalPosition(0f, y, dis);
			base.objNeckLookTargetP.transform.localEulerAngles = new Vector3(0f, 0f, 360f - rotDeg);
		}
		base.fileStatus.neckTargetAngle = rotDeg;
		base.fileStatus.neckTargetRange = range;
		base.fileStatus.neckTargetRate = rate;
	}

	public void ChangeLookNeckPtn(int ptn, float rate = 1f)
	{
		if (!(null == base.neckLookCtrl))
		{
			NeckLookControllerVer2 neckLookControllerVer = base.neckLookCtrl;
			int ptnNo = (base.fileStatus.neckLookPtn = ptn);
			neckLookControllerVer.ptnNo = ptnNo;
			base.neckLookCtrl.rate = rate;
		}
	}

	public int GetLookNeckPtn()
	{
		return base.fileStatus.neckLookPtn;
	}

	public void SetForegroundEyesAndEyebrow()
	{
		bool eyes = true;
		if (Manager.Config.EtcData != null)
		{
			eyes = Manager.Config.EtcData.ForegroundEyes;
		}
		if (1 == base.fileFace.foregroundEyes)
		{
			eyes = false;
		}
		else if (2 == base.fileFace.foregroundEyes)
		{
			eyes = true;
		}
		bool eyebrow = true;
		if (Manager.Config.EtcData != null)
		{
			eyebrow = Manager.Config.EtcData.ForegroundEyebrow;
		}
		if (1 == base.fileFace.foregroundEyebrow)
		{
			eyebrow = false;
		}
		else if (2 == base.fileFace.foregroundEyebrow)
		{
			eyebrow = true;
		}
		if (null != base.foregroundCtrl)
		{
			base.foregroundCtrl.SetForeground(eyes, eyebrow);
		}
	}

	public void ChangeHohoAkaRate(float value)
	{
		base.fileStatus.hohoAkaRate = value;
		if ((bool)base.customMatFace)
		{
			Color color = base.customMatFace.GetColor(ChaShader._overcolor2);
			color.a = Mathf.Lerp(0f, 0.2f, base.fileStatus.hohoAkaRate);
			if (Manager.Config.EtcData != null && !Manager.Config.EtcData.hohoAka)
			{
				color.a = 0f;
			}
			base.customMatFace.SetColor(ChaShader._overcolor2, color);
		}
	}

	public bool IsGagEyes()
	{
		int eyesPtn = GetEyesPtn();
		ListInfoBase info = base.lstCtrl.GetInfo(ChaListDefine.CategoryNo.cha_eyeset, eyesPtn);
		if (info == null)
		{
			return false;
		}
		if (info.GetInfoInt(ChaListDefine.KeyType.EyeObjNo) == 0)
		{
			return false;
		}
		return true;
	}

	protected void InitializeControlHandAll()
	{
		InitializeControlHandObject();
	}

	protected void InitializeControlHandObject()
	{
		sibHand = new ShapeHandInfo();
	}

	protected void ReleaseControlHandAll()
	{
		ReleaseControlHandObject();
	}

	protected void ReleaseControlHandObject(bool init = true)
	{
		if (sibHand != null)
		{
			sibHand.ReleaseShapeInfo();
		}
		if (init)
		{
			InitializeControlHandObject();
		}
	}

	public bool InitShapeHand(Transform trfBone)
	{
		if (sibHand == null)
		{
			return false;
		}
		if (null == trfBone)
		{
			return false;
		}
		string anmKeyInfoName = "cf_anmShapeHand";
		string cateInfoName = "cf_customhand";
		sibHand.InitShapeInfo("abdata", "chara/oo_hand.unity3d", "chara/oo_hand.unity3d", anmKeyInfoName, cateInfoName, trfBone, base.sex);
		for (int i = 0; i < 2; i++)
		{
			SetShapeHandValue(i, 0, 0, 0f);
		}
		return true;
	}

	public void ReleaseShapeHand()
	{
		if (sibHand != null)
		{
			sibHand.ReleaseShapeInfo();
		}
	}

	public bool GetEnableShapeHand(int lr)
	{
		return base.fileStatus.enableShapeHand[lr];
	}

	public void SetEnableShapeHand(int lr, bool enable)
	{
		base.fileStatus.enableShapeHand[lr] = enable;
	}

	public int GetShapeIndexCount()
	{
		return sibHand.GetKeyCount();
	}

	public int GetShapeHandIndex(int lr, int no)
	{
		if (2 <= no)
		{
			return 0;
		}
		return base.fileStatus.shapeHandPtn[lr, no];
	}

	public float GetShapeHandBlendValue(int lr)
	{
		return base.fileStatus.shapeHandBlendValue[lr];
	}

	public bool SetShapeHandValue(int lr, int idx01, int idx02, float blend)
	{
		base.fileStatus.shapeHandPtn[lr, 0] = idx01;
		base.fileStatus.shapeHandPtn[lr, 1] = idx02;
		base.fileStatus.shapeHandBlendValue[lr] = blend;
		if (sibHand != null && sibHand.InitEnd)
		{
			sibHand.ChangeValue(lr, idx01, idx02, blend);
		}
		return true;
	}

	public bool SetShapeHandIndex(int lr, int idx01 = -1, int idx02 = -1)
	{
		if (-1 != idx01)
		{
			base.fileStatus.shapeHandPtn[lr, 0] = idx01;
		}
		if (-1 != idx02)
		{
			base.fileStatus.shapeHandPtn[lr, 1] = idx02;
		}
		if (sibHand != null && sibHand.InitEnd)
		{
			sibHand.ChangeValue(lr, base.fileStatus.shapeHandPtn[lr, 0], base.fileStatus.shapeHandPtn[lr, 1], base.fileStatus.shapeHandBlendValue[lr]);
		}
		return true;
	}

	public bool SetShapeHandBlend(int lr, float blend)
	{
		base.fileStatus.shapeHandBlendValue[lr] = blend;
		if (sibHand != null && sibHand.InitEnd)
		{
			sibHand.ChangeValue(lr, base.fileStatus.shapeHandPtn[lr, 0], base.fileStatus.shapeHandPtn[lr, 1], base.fileStatus.shapeHandBlendValue[lr]);
		}
		return true;
	}

	public void UpdateAlwaysShapeHand()
	{
		if (sibHand != null && sibHand.InitEnd)
		{
			ShapeHandInfo shapeHandInfo = sibHand as ShapeHandInfo;
			shapeHandInfo.updateMask = 0;
			if (base.fileStatus.enableShapeHand[0])
			{
				shapeHandInfo.updateMask |= 1;
			}
			if (base.fileStatus.enableShapeHand[1])
			{
				shapeHandInfo.updateMask |= 2;
			}
			sibHand.UpdateAlways();
		}
	}

	protected void InitializeControlLoadAll()
	{
		InitializeControlLoadObject();
	}

	protected void InitializeControlLoadObject()
	{
		aaWeightsHead = new AssignedAnotherWeights();
		aaWeightsBody = new AssignedAnotherWeights();
		updateAlphaMask = true;
	}

	protected void ReleaseControlLoadAll()
	{
		ReleaseControlLoadObject(init: false);
	}

	protected void ReleaseControlLoadObject(bool init = true)
	{
		if (aaWeightsHead != null)
		{
			aaWeightsHead.Release();
		}
		if (aaWeightsBody != null)
		{
			aaWeightsBody.Release();
		}
		if (init)
		{
			InitializeControlLoadObject();
		}
	}

	public bool Load(bool reflectStatus = false)
	{
		LoadNoAsync(reflectStatus);
		return true;
	}

	public IEnumerator LoadAsync(bool reflectStatus = false, bool asyncFlags = true)
	{
		if (!asyncFlags)
		{
			LoadNoAsync(reflectStatus);
			yield break;
		}
		bool sexFemale = ((base.sex != 0) ? true : false);
		string mainManifestName = Character.mainManifestName;
		byte[] status = null;
		if (reflectStatus)
		{
			status = base.chaFile.GetStatusBytes();
		}
		ReleaseObject();
		if (asyncFlags)
		{
			yield return null;
		}
		base.objTop = new GameObject("BodyTop");
		if ((bool)base.objRoot)
		{
			base.objTop.transform.SetParent(base.objRoot.transform, worldPositionStays: false);
		}
		if (asyncFlags)
		{
			SetActiveTop(active: false);
		}
		AddUpdateCMBodyTexFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpSunburn: true);
		AddUpdateCMBodyColorFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpSunburn: true, inpNail: true);
		AddUpdateCMBodyLayoutFlags(inpPaint01: true, inpPaint02: true);
		CreateBodyTexture();
		LoadGagMaterial();
		string assetBundleName = "chara/oo_base.unity3d";
		string assetName = (base.hiPoly ? "p_cf_body_bone" : "p_cf_body_bone_low");
		if (asyncFlags)
		{
			yield return StartCoroutine(Load_Coroutine(assetBundleName, assetName, delegate(GameObject x)
			{
				base.objAnim = x;
			}, xxx: true, mainManifestName));
		}
		else
		{
			base.objAnim = CommonLib.LoadAsset<GameObject>(assetBundleName, assetName, clone: true, mainManifestName);
		}
		Character.AddLoadAssetBundle(assetBundleName, mainManifestName);
		if (!base.objAnim)
		{
			yield break;
		}
		base.animBody = base.objAnim.GetComponent<Animator>();
		base.objAnim.transform.SetParent(base.objTop.transform, worldPositionStays: false);
		base.dictDynamicBoneBust = new Dictionary<DynamicBoneKind, DynamicBone_Ver02>();
		DynamicBone_Ver02[] componentsInChildren = base.objAnim.GetComponentsInChildren<DynamicBone_Ver02>(includeInactive: true);
		foreach (DynamicBone_Ver02 dynamicBone_Ver in componentsInChildren)
		{
			if (dynamicBone_Ver.Comment == "左胸")
			{
				base.dictDynamicBoneBust[DynamicBoneKind.BreastL] = dynamicBone_Ver;
			}
			else if (dynamicBone_Ver.Comment == "右胸")
			{
				base.dictDynamicBoneBust[DynamicBoneKind.BreastR] = dynamicBone_Ver;
			}
			else if (dynamicBone_Ver.Comment == "左尻")
			{
				base.dictDynamicBoneBust[DynamicBoneKind.HipL] = dynamicBone_Ver;
				if (base.sex == 0)
				{
					dynamicBone_Ver.enabled = false;
				}
			}
			else if (dynamicBone_Ver.Comment == "右尻")
			{
				base.dictDynamicBoneBust[DynamicBoneKind.HipR] = dynamicBone_Ver;
				if (base.sex == 0)
				{
					dynamicBone_Ver.enabled = false;
				}
			}
		}
		base.objBodyBone = base.objAnim.transform.FindLoop("cf_j_root");
		if ((bool)base.objBodyBone)
		{
			aaWeightsBody.CreateBoneList(base.objBodyBone, "");
			CreateReferenceInfo(1uL, base.objBodyBone);
			if (!base.hiPoly)
			{
				GameObject referenceInfo = GetReferenceInfo(RefObjKey.a_n_back_L);
				if ((bool)referenceInfo)
				{
					referenceInfo.transform.SetLocalRotationY(180f);
				}
				referenceInfo = GetReferenceInfo(RefObjKey.a_n_back_R);
				if ((bool)referenceInfo)
				{
					referenceInfo.transform.SetLocalRotationY(180f);
				}
				referenceInfo = GetReferenceInfo(RefObjKey.a_n_bust);
				if ((bool)referenceInfo)
				{
					referenceInfo.transform.SetLocalPositionY(0.14f);
				}
				referenceInfo = GetReferenceInfo(RefObjKey.a_n_bust_f);
				if ((bool)referenceInfo)
				{
					referenceInfo.transform.SetLocalPositionZ(0.04f);
					referenceInfo.transform.SetLocalRotationX(-35f);
				}
				referenceInfo = GetReferenceInfo(RefObjKey.a_n_ind_L);
				if ((bool)referenceInfo)
				{
					referenceInfo.transform.SetLocalPosition(-0.0162f, -0.0024f, 0.0006f);
					referenceInfo.transform.SetLocalRotation(0f, 0f, 0f);
				}
				referenceInfo = GetReferenceInfo(RefObjKey.a_n_ind_R);
				if ((bool)referenceInfo)
				{
					referenceInfo.transform.SetLocalPosition(0.0162f, 0.0024f, -0.0006f);
					referenceInfo.transform.SetLocalRotation(180f, 0f, 0f);
				}
				referenceInfo = GetReferenceInfo(RefObjKey.a_n_mid_L);
				if ((bool)referenceInfo)
				{
					referenceInfo.transform.SetLocalPosition(-0.016f, -0.0028f, 0f);
				}
				referenceInfo = GetReferenceInfo(RefObjKey.a_n_mid_R);
				if ((bool)referenceInfo)
				{
					referenceInfo.transform.SetLocalPosition(0.016f, 0.0028f, 0f);
					referenceInfo.transform.SetLocalRotation(180f, 0f, 0f);
				}
				referenceInfo = GetReferenceInfo(RefObjKey.a_n_ring_L);
				if ((bool)referenceInfo)
				{
					referenceInfo.transform.SetLocalPosition(-0.018f, -0.0018f, -0.0005f);
				}
				referenceInfo = GetReferenceInfo(RefObjKey.a_n_ring_R);
				if ((bool)referenceInfo)
				{
					referenceInfo.transform.SetLocalPosition(0.018f, 0.0018f, 0.0005f);
					referenceInfo.transform.SetLocalRotation(180f, 0f, 0f);
				}
				referenceInfo = GetReferenceInfo(RefObjKey.a_n_nip_L);
				if ((bool)referenceInfo)
				{
					referenceInfo.transform.SetLocalPosition(0f, 0f, 0.05f);
					referenceInfo.transform.SetLocalRotation(0f, 0f, 0f);
				}
				referenceInfo = GetReferenceInfo(RefObjKey.a_n_nip_R);
				if ((bool)referenceInfo)
				{
					referenceInfo.transform.SetLocalPosition(0f, 0f, 0.05f);
					referenceInfo.transform.SetLocalRotation(0f, 0f, 0f);
				}
			}
			NeckLookControllerVer2[] componentsInChildren2 = base.objBodyBone.GetComponentsInChildren<NeckLookControllerVer2>(includeInactive: true);
			if (componentsInChildren2.Length != 0)
			{
				base.neckLookCtrl = componentsInChildren2[0];
			}
			if ((bool)base.neckLookCtrl)
			{
				ChangeLookNeckTarget(base.fileStatus.neckTargetType);
				ChangeLookNeckPtn(0);
			}
			InitShapeBody(base.objBodyBone.transform);
			InitShapeHand(base.objBodyBone.transform);
			base.objNeckLookTargetP = new GameObject("N_NeckLookTargetP");
			GameObject referenceInfo2 = GetReferenceInfo(RefObjKey.NECK_LOOK_TARGET);
			if ((bool)base.objNeckLookTargetP && (bool)referenceInfo2)
			{
				base.objNeckLookTargetP.transform.SetParent(referenceInfo2.transform, worldPositionStays: false);
				base.objNeckLookTarget = new GameObject("N_NeckLookTarget");
				if ((bool)base.objNeckLookTarget)
				{
					base.objNeckLookTarget.transform.SetParent(base.objNeckLookTargetP.transform, worldPositionStays: false);
				}
			}
			base.objEyesLookTargetP = new GameObject("N_EyesLookTargetP");
			referenceInfo2 = GetReferenceInfo(RefObjKey.HeadParent);
			if ((bool)base.objEyesLookTargetP && (bool)referenceInfo2)
			{
				base.objEyesLookTargetP.transform.SetParent(referenceInfo2.transform, worldPositionStays: false);
				base.objEyesLookTarget = new GameObject("N_EyesLookTarget");
				if ((bool)base.objEyesLookTarget)
				{
					base.objEyesLookTarget.transform.SetParent(base.objEyesLookTargetP.transform, worldPositionStays: false);
				}
			}
		}
		assetBundleName = "chara/oo_base.unity3d";
		string assetName2 = "p_cf_head_bone";
		if (asyncFlags)
		{
			yield return StartCoroutine(Load_Coroutine(assetBundleName, assetName2, delegate(GameObject x)
			{
				base.objHeadBone = x;
			}, xxx: true, mainManifestName));
		}
		else
		{
			base.objHeadBone = CommonLib.LoadAsset<GameObject>(assetBundleName, assetName2, clone: true, mainManifestName);
		}
		Character.AddLoadAssetBundle(assetBundleName, mainManifestName);
		if (!base.objHeadBone)
		{
			yield break;
		}
		GameObject referenceInfo3 = GetReferenceInfo(RefObjKey.HeadParent);
		base.objHeadBone.transform.SetParent(referenceInfo3.transform, worldPositionStays: false);
		EyeLookController[] componentsInChildren3 = base.objHeadBone.GetComponentsInChildren<EyeLookController>(includeInactive: true);
		if (componentsInChildren3.Length != 0)
		{
			base.eyeLookCtrl = componentsInChildren3[0];
		}
		if ((bool)base.eyeLookCtrl && (bool)base.eyeLookCtrl.GetComponent<EyeLookCalc>())
		{
			ChangeLookEyesTarget(base.fileStatus.eyesTargetType);
			ChangeLookEyesPtn(0);
		}
		aaWeightsHead.CreateBoneList(base.objHeadBone, "");
		CreateReferenceInfo(2uL, base.objHeadBone);
		assetBundleName = null;
		string asset;
		string manifestName;
		if (base.fileParam.exType == 0)
		{
			assetBundleName = "chara/oo_base.unity3d";
			string assetName3 = ((!base.hiPoly) ? (sexFemale ? "p_cf_body_00_low" : "p_cm_body_00_low") : (sexFemale ? "p_cf_body_00" : "p_cm_body_00"));
			if (asyncFlags)
			{
				yield return StartCoroutine(Load_Coroutine(assetBundleName, assetName3, delegate(GameObject x)
				{
					base.objBody = x;
				}, xxx: true, mainManifestName));
			}
			else
			{
				base.objBody = CommonLib.LoadAsset<GameObject>(assetBundleName, assetName3, clone: true, mainManifestName);
			}
			Character.AddLoadAssetBundle(assetBundleName, mainManifestName);
			if ((bool)base.objBody)
			{
				CreateReferenceInfo(3uL, base.objBody);
				GameObject referenceInfo4 = GetReferenceInfo(RefObjKey.ObjBody);
				if ((bool)referenceInfo4)
				{
					base.rendBody = referenceInfo4.GetComponent<Renderer>();
				}
				GameObject referenceInfo5 = GetReferenceInfo(RefObjKey.S_SimpleBody);
				if ((bool)referenceInfo5)
				{
					base.rendSimpleBody = referenceInfo5.GetComponent<Renderer>();
				}
				GameObject referenceInfo6 = GetReferenceInfo(RefObjKey.S_SimpleTongue);
				if ((bool)referenceInfo6)
				{
					base.rendSimpleTongue = referenceInfo6.GetComponent<Renderer>();
				}
				SetBodyBaseMaterial();
				base.objBody.transform.SetParent(base.objTop.transform, worldPositionStays: false);
				GameObject referenceInfo7 = GetReferenceInfo(RefObjKey.A_ROOTBONE);
				Transform rootBone = (referenceInfo7 ? referenceInfo7.transform : null);
				aaWeightsBody.AssignedWeightsAndSetBounds(base.objBody, "cf_j_root", bounds, rootBone);
				if (1 == base.sex && base.hiPoly)
				{
					assetBundleName = "chara/oo_base.unity3d";
					assetName3 = "p_cf_body_00_Nml";
					BustNormal value = null;
					if (dictBustNormal.TryGetValue(BustNormalKind.NmlBody, out value))
					{
						value.Release();
					}
					value = new BustNormal();
					value.Init(base.objBody, assetBundleName, assetName3, "");
					dictBustNormal[BustNormalKind.NmlBody] = value;
				}
				ChangeCustomBodyWithoutCustomTexture();
			}
		}
		else if (base.lstCtrl.GetFilePath(ChaListDefine.CategoryNo.ex_bo_body, 0, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.MainData, ChaListDefine.KeyType.MainManifest, out assetBundleName, out asset, out manifestName))
		{
			if (asyncFlags)
			{
				yield return StartCoroutine(Load_Coroutine(assetBundleName, asset, delegate(GameObject x)
				{
					base.objBody = x;
				}, xxx: true, manifestName));
			}
			else
			{
				base.objBody = CommonLib.LoadAsset<GameObject>(assetBundleName, asset, clone: true, manifestName);
			}
			Character.AddLoadAssetBundle(assetBundleName, manifestName);
			if ((bool)base.objBody)
			{
				CreateReferenceInfo(3uL, base.objBody);
				GameObject referenceInfo8 = GetReferenceInfo(RefObjKey.ObjBody);
				if ((bool)referenceInfo8)
				{
					base.rendBody = referenceInfo8.GetComponent<Renderer>();
				}
				GameObject referenceInfo9 = GetReferenceInfo(RefObjKey.S_SimpleBody);
				if ((bool)referenceInfo9)
				{
					base.rendSimpleBody = referenceInfo9.GetComponent<Renderer>();
				}
				GameObject referenceInfo10 = GetReferenceInfo(RefObjKey.S_SimpleTongue);
				if ((bool)referenceInfo10)
				{
					base.rendSimpleTongue = referenceInfo10.GetComponent<Renderer>();
				}
				SetBodyBaseMaterial();
				base.objBody.transform.SetParent(base.objTop.transform, worldPositionStays: false);
				GameObject referenceInfo11 = GetReferenceInfo(RefObjKey.A_ROOTBONE);
				Transform rootBone2 = (referenceInfo11 ? referenceInfo11.transform : null);
				aaWeightsBody.AssignedWeightsAndSetBounds(base.objBody, "cf_j_root", bounds, rootBone2);
				ChangeCustomBodyWithoutCustomTexture();
			}
		}
		if (base.hiPoly)
		{
			manifestName = "chara/oo_base.unity3d";
			string assetName4 = "p_tang2";
			if (asyncFlags)
			{
				yield return StartCoroutine(Load_Coroutine(manifestName, assetName4, delegate(GameObject x)
				{
					base.objTongueEx = x;
				}, xxx: true, mainManifestName));
			}
			else
			{
				base.objTongueEx = CommonLib.LoadAsset<GameObject>(manifestName, assetName4, clone: true, mainManifestName);
			}
			Character.AddLoadAssetBundle(manifestName, mainManifestName);
			if ((bool)base.objTongueEx)
			{
				GameObject referenceInfo12 = GetReferenceInfo(RefObjKey.HeadParent);
				base.objTongueEx.transform.SetParent(referenceInfo12.transform, worldPositionStays: false);
				base.animTongueEx = base.objTongueEx.GetComponent<Animator>();
			}
		}
		if (1 == base.sex)
		{
			manifestName = "chara/oo_base.unity3d";
			string assetName5 = "p_cf_hide_nip";
			if (asyncFlags)
			{
				yield return StartCoroutine(Load_Coroutine(manifestName, assetName5, delegate(GameObject x)
				{
					base.objHideNip = x;
				}, xxx: true, mainManifestName));
			}
			else
			{
				base.objHideNip = CommonLib.LoadAsset<GameObject>(manifestName, assetName5, clone: true, mainManifestName);
			}
			Character.AddLoadAssetBundle(manifestName, mainManifestName);
			if ((bool)base.objHideNip)
			{
				base.objHideNip.transform.SetParent(base.objBodyBone.transform, worldPositionStays: false);
				GameObject referenceInfo13 = GetReferenceInfo(RefObjKey.A_ROOTBONE);
				Transform rootBone3 = (referenceInfo13 ? referenceInfo13.transform : null);
				aaWeightsBody.AssignedWeightsAndSetBounds(base.objHideNip, "cf_j_root", bounds, rootBone3);
			}
		}
		if (1 == base.sex)
		{
			manifestName = "chara/oo_base.unity3d";
			string assetName6 = "p_cf_hide_kokan";
			if (asyncFlags)
			{
				yield return StartCoroutine(Load_Coroutine(manifestName, assetName6, delegate(GameObject x)
				{
					base.objHideKokan = x;
				}, xxx: true, mainManifestName));
			}
			else
			{
				base.objHideKokan = CommonLib.LoadAsset<GameObject>(manifestName, assetName6, clone: true, mainManifestName);
			}
			Character.AddLoadAssetBundle(manifestName, mainManifestName);
			if ((bool)base.objHideKokan)
			{
				base.objHideKokan.transform.SetParent(base.objBodyBone.transform, worldPositionStays: false);
				GameObject referenceInfo14 = GetReferenceInfo(RefObjKey.A_ROOTBONE);
				Transform rootBone4 = (referenceInfo14 ? referenceInfo14.transform : null);
				aaWeightsBody.AssignedWeightsAndSetBounds(base.objHideKokan, "cf_j_root", bounds, rootBone4);
			}
		}
		if (asyncFlags)
		{
			yield return null;
		}
		if (asyncFlags)
		{
			yield return StartCoroutine(ChangeHeadAsync(forceChange: true));
		}
		else
		{
			ChangeHead(forceChange: true);
		}
		LoadHairGlossMask();
		if (asyncFlags)
		{
			yield return StartCoroutine(ChangeHairAsync(forceChange: true));
		}
		else
		{
			ChangeHair(forceChange: true);
		}
		if (asyncFlags)
		{
			yield return StartCoroutine(ChangeClothesAsync(forceChange: true));
		}
		else
		{
			ChangeClothes(forceChange: true);
		}
		if (asyncFlags)
		{
			yield return StartCoroutine(ChangeAccessoryAsync(forceChange: true));
		}
		else
		{
			ChangeAccessory(forceChange: true);
		}
		base.updateBustSize = true;
		base.reSetupDynamicBoneBust = true;
		if (base.hiPoly)
		{
			UpdateSiru(forceChange: true);
		}
		ChangeHohoAkaRate(base.fileStatus.hohoAkaRate);
		if (reflectStatus)
		{
			base.chaFile.SetStatusBytes(status);
		}
		if (Character.enableCharaLoadGCClear)
		{
			Resources.UnloadUnusedAssets();
			GC.Collect();
		}
		base.loadEnd = true;
	}

	public bool Reload(bool noChangeClothes = false, bool noChangeHead = false, bool noChangeHair = false, bool noChangeBody = false)
	{
		ReloadNoAsync(noChangeClothes, noChangeHead, noChangeHair, noChangeBody);
		return true;
	}

	public IEnumerator ReloadAsync(bool noChangeClothes = false, bool noChangeHead = false, bool noChangeHair = false, bool noChangeBody = false, bool asyncFlags = true)
	{
		if (asyncFlags)
		{
			SetActiveTop(active: false);
		}
		if (!noChangeBody)
		{
			AddUpdateCMBodyTexFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpSunburn: true);
			AddUpdateCMBodyColorFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpSunburn: true, inpNail: true);
			AddUpdateCMBodyLayoutFlags(inpPaint01: true, inpPaint02: true);
			CreateBodyTexture();
			if (noChangeHead)
			{
				AddUpdateCMFaceTexFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpCheek: true, inpLipLine: true, inpMole: true);
				AddUpdateCMFaceColorFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpCheek: true, inpLipLine: true, inpMole: true);
				AddUpdateCMFaceLayoutFlags(inpPaint01: true, inpPaint02: true, inpMole: true);
				CreateFaceTexture();
				ChangeCustomFaceWithoutCustomTexture();
			}
		}
		if (!noChangeHead)
		{
			if (asyncFlags)
			{
				yield return StartCoroutine(ChangeHeadAsync(forceChange: true));
			}
			else
			{
				ChangeHead(forceChange: true);
			}
		}
		if (!noChangeHair)
		{
			LoadHairGlossMask();
			if (asyncFlags)
			{
				yield return StartCoroutine(ChangeHairAsync(forceChange: true));
			}
			else
			{
				ChangeHair(forceChange: true);
			}
		}
		if (!noChangeClothes)
		{
			if (asyncFlags)
			{
				yield return StartCoroutine(ChangeClothesAsync(forceChange: true));
			}
			else
			{
				ChangeClothes(forceChange: true);
			}
			if (asyncFlags)
			{
				yield return StartCoroutine(ChangeAccessoryAsync(forceChange: true));
			}
			else
			{
				ChangeAccessory(forceChange: true);
			}
		}
		if (asyncFlags)
		{
			yield return null;
		}
		UpdateShapeBodyValueFromCustomInfo();
		UpdateShapeFaceValueFromCustomInfo();
		base.updateBustSize = true;
		base.reSetupDynamicBoneBust = true;
		if (!noChangeBody)
		{
			ChangeCustomBodyWithoutCustomTexture();
		}
		UpdateClothesStateAll();
		if (Character.enableCharaLoadGCClear)
		{
			Resources.UnloadUnusedAssets();
			GC.Collect();
		}
		if (asyncFlags)
		{
			yield return null;
		}
	}

	public void ChangeHead(bool forceChange = false, bool update = true)
	{
		ChangeHeadNoAsync(base.fileFace.headId, forceChange, update);
	}

	public void ChangeHead(int _headId, bool forceChange = false)
	{
		StartCoroutine(ChangeHeadAsync(_headId, forceChange, asyncFlags: false));
	}

	public IEnumerator ChangeHeadAsync(bool forceChange = false)
	{
		yield return StartCoroutine(ChangeHeadAsync(base.fileFace.headId, forceChange));
	}

	public IEnumerator ChangeHeadAsync(int _headId, bool forceChange = false, bool asyncFlags = true)
	{
		if (-1 == _headId || (!forceChange && null != base.objHead && _headId == base.fileFace.headId))
		{
			yield break;
		}
		string createName = "ct_head";
		if ((bool)base.objHead)
		{
			SafeDestroy(base.objHead);
			base.objHead = null;
			base.infoHead = null;
			ReleaseRefObject(4uL);
			ReleaseShapeFace();
			base.foregroundCtrl = null;
			base.rendFace = null;
			base.rendEye = new Renderer[2];
			base.rendEyeW = new Renderer[2];
			base.rendEyelineUp = null;
			base.rendEyelineDown = null;
			base.rendEyebrow = null;
			base.rendNose = null;
		}
		if (asyncFlags)
		{
			IEnumerator routine = LoadCharaFbxDataAsync(delegate(GameObject o)
			{
				base.objHead = o;
			}, delegate(ListInfoBase lib)
			{
				base.infoHead = lib;
			}, _hiPoly: true, (base.sex == 0 && 1 == base.exType) ? 501 : 100, _headId, createName, copyDynamicBone: false, 0, null, 0);
			yield return StartCoroutine(routine);
		}
		else
		{
			base.objHead = LoadCharaFbxData(delegate(ListInfoBase lib)
			{
				base.infoHead = lib;
			}, _hiPoly: true, (base.sex == 0 && 1 == base.exType) ? 501 : 100, _headId, createName, copyDynamicBone: false, 0, null, 0);
		}
		if ((bool)base.objHead)
		{
			CommonLib.CopySameNameTransform(base.objHeadBone.transform, base.objHead.transform);
			base.objHead.transform.SetParent(base.objHeadBone.transform, worldPositionStays: false);
			GameObject referenceInfo = GetReferenceInfo(RefObjKey.A_ROOTBONE);
			Transform rootBone = (referenceInfo ? referenceInfo.transform : null);
			aaWeightsHead.AssignedWeightsAndSetBounds(base.objHead, "cf_J_N_FaceRoot", bounds, rootBone);
			if (asyncFlags)
			{
				yield return null;
			}
			ListInfoBase listInfoBase = base.infoHead;
			base.foregroundCtrl = base.objHead.GetComponent<ChaForegroundComponent>();
			base.fileFace.headId = listInfoBase.Id;
			CreateReferenceInfo(4uL, base.objHead);
			GameObject referenceInfo2 = GetReferenceInfo(RefObjKey.ObjEyeL);
			if ((bool)referenceInfo2)
			{
				base.eyeLookMatCtrl[0] = referenceInfo2.GetComponent<EyeLookMaterialControll>();
				if ((bool)base.eyeLookMatCtrl[0])
				{
					base.eyeLookMatCtrl[0].script = base.eyeLookCtrl.eyeLookScript;
					base.eyeLookMatCtrl[0].ReSetupMaterial();
				}
			}
			GameObject referenceInfo3 = GetReferenceInfo(RefObjKey.ObjEyeR);
			if ((bool)referenceInfo3)
			{
				base.eyeLookMatCtrl[1] = referenceInfo3.GetComponent<EyeLookMaterialControll>();
				if ((bool)base.eyeLookMatCtrl[1])
				{
					base.eyeLookMatCtrl[1].script = base.eyeLookCtrl.eyeLookScript;
					base.eyeLookMatCtrl[1].ReSetupMaterial();
				}
			}
			GameObject referenceInfo4 = GetReferenceInfo(RefObjKey.ObjFace);
			if (null != referenceInfo4)
			{
				base.rendFace = referenceInfo4.GetComponent<Renderer>();
			}
			base.rendEye = new Renderer[2];
			referenceInfo4 = GetReferenceInfo(RefObjKey.ObjEyeL);
			if (null != referenceInfo4)
			{
				base.rendEye[0] = referenceInfo4.GetComponent<Renderer>();
			}
			referenceInfo4 = GetReferenceInfo(RefObjKey.ObjEyeR);
			if (null != referenceInfo4)
			{
				base.rendEye[1] = referenceInfo4.GetComponent<Renderer>();
			}
			base.rendEyeW = new Renderer[2];
			referenceInfo4 = GetReferenceInfo(RefObjKey.ObjEyeWL);
			if (null != referenceInfo4)
			{
				base.rendEyeW[0] = referenceInfo4.GetComponent<Renderer>();
			}
			referenceInfo4 = GetReferenceInfo(RefObjKey.ObjEyeWR);
			if (null != referenceInfo4)
			{
				base.rendEyeW[1] = referenceInfo4.GetComponent<Renderer>();
			}
			referenceInfo4 = GetReferenceInfo(RefObjKey.ObjEyeline);
			if (null != referenceInfo4)
			{
				base.rendEyelineUp = referenceInfo4.GetComponent<Renderer>();
			}
			referenceInfo4 = GetReferenceInfo(RefObjKey.ObjEyelineLow);
			if (null != referenceInfo4)
			{
				base.rendEyelineDown = referenceInfo4.GetComponent<Renderer>();
			}
			referenceInfo4 = GetReferenceInfo(RefObjKey.ObjEyebrow);
			if (null != referenceInfo4)
			{
				base.rendEyebrow = referenceInfo4.GetComponent<Renderer>();
			}
			referenceInfo4 = GetReferenceInfo(RefObjKey.ObjNoseline);
			if (null != referenceInfo4)
			{
				base.rendNose = referenceInfo4.GetComponent<Renderer>();
			}
			string info = listInfoBase.GetInfo(ChaListDefine.KeyType.MatManifest);
			string info2 = listInfoBase.GetInfo(ChaListDefine.KeyType.MatAB);
			string info3 = listInfoBase.GetInfo(ChaListDefine.KeyType.MatData);
			InitBaseCustomTextureFace(info2, info3, info);
			AddUpdateCMFaceTexFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpCheek: true, inpLipLine: true, inpMole: true);
			AddUpdateCMFaceColorFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpCheek: true, inpLipLine: true, inpMole: true);
			AddUpdateCMFaceLayoutFlags(inpPaint01: true, inpPaint02: true, inpMole: true);
			CreateFaceTexture();
			SetFaceBaseMaterial();
			ChangeSettingWhiteOfEye(updateTex: true, updateColor: true);
			ChangeSettingEye(2, updateBaseTex: true, updateMaskTex: true, updateColorAndOffset: true);
			if (base.hiPoly)
			{
				UpdateSiru(forceChange: true);
			}
			ChangeHohoAkaRate(base.fileStatus.hohoAkaRate);
			InitShapeFace(base.objHeadBone.transform, listInfoBase.GetInfo(ChaListDefine.KeyType.MainAB), listInfoBase.GetInfo(ChaListDefine.KeyType.ShapeAnime), listInfoBase.GetInfo(ChaListDefine.KeyType.MainManifest));
			ChangeCustomFaceWithoutCustomTexture();
			HideEyeHighlight(hide: false);
			base.fbsCtrl = base.objHead.GetComponent<FaceBlendShape>();
			if (null != base.fbsCtrl)
			{
				base.eyebrowCtrl = base.fbsCtrl.EyebrowCtrl;
				base.eyesCtrl = base.fbsCtrl.EyesCtrl;
				base.mouthCtrl = base.fbsCtrl.MouthCtrl;
				ChangeEyebrowPtn(base.fileStatus.eyebrowPtn);
				ChangeEyebrowOpenMax(base.fileStatus.eyebrowOpenMax);
				ChangeEyesPtn(base.fileStatus.eyesPtn);
				ChangeEyesOpenMax(base.fileStatus.eyesOpenMax);
				ChangeMouthPtn(base.fileStatus.mouthPtn);
				ChangeMouthOpenMax(base.fileStatus.mouthOpenMax);
				base.fbsCtrl.EyeLookController = base.eyeLookCtrl;
				ChangeEyesBlinkFlag(base.fileStatus.eyesBlink);
			}
		}
		if (asyncFlags)
		{
			yield return null;
		}
	}

	public bool ChangeHairFront(bool forceChange = false)
	{
		int num = 1;
		StartCoroutine(ChangeHairAsync(num, base.fileHair.parts[num].id, forceChange, asyncFlags: false));
		return true;
	}

	public bool ChangeHairBack(bool forceChange = false)
	{
		int num = 0;
		StartCoroutine(ChangeHairAsync(num, base.fileHair.parts[num].id, forceChange, asyncFlags: false));
		return true;
	}

	public bool ChangeHairSide(bool forceChange = false)
	{
		int num = 2;
		StartCoroutine(ChangeHairAsync(num, base.fileHair.parts[num].id, forceChange, asyncFlags: false));
		return true;
	}

	public bool ChangeHairOption(bool forceChange = false)
	{
		int num = 3;
		StartCoroutine(ChangeHairAsync(num, base.fileHair.parts[num].id, forceChange, asyncFlags: false));
		return true;
	}

	public void ChangeHair(bool forceChange = false)
	{
		int[] hairKinds = ChaFileDefine.hairKinds;
		foreach (int num in hairKinds)
		{
			ChangeHairNoAsync(num, base.fileHair.parts[num].id, forceChange);
		}
	}

	public void ChangeHair(int kind, int id, bool forceChange = false)
	{
		StartCoroutine(ChangeHairAsync(kind, id, forceChange, asyncFlags: false));
	}

	public IEnumerator ChangeHairAsync(bool forceChange = false)
	{
		int[] array = (int[])Enum.GetValues(typeof(ChaFileDefine.HairKind));
		int[] array2 = array;
		foreach (int num in array2)
		{
			yield return StartCoroutine(ChangeHairAsync(num, base.fileHair.parts[num].id, forceChange));
		}
	}

	public IEnumerator ChangeHairAsync(int kind, int id, bool forceChange = false, bool asyncFlags = true)
	{
		ChaListDefine.CategoryNo[] array = new ChaListDefine.CategoryNo[5]
		{
			ChaListDefine.CategoryNo.bo_hair_b,
			ChaListDefine.CategoryNo.bo_hair_f,
			ChaListDefine.CategoryNo.bo_hair_s,
			ChaListDefine.CategoryNo.bo_hair_o,
			ChaListDefine.CategoryNo.bo_hair_o
		};
		string[] array2 = new string[5] { "ct_hairB", "ct_hairF", "ct_hairS", "ct_hairO_01", "ct_hairO_02" };
		int[] array3 = new int[5] { 0, 1, 0, 0, 0 };
		if (base.sex == 0 && 1 == base.exType && kind >= 1)
		{
			id = 0;
		}
		if (!forceChange && null != base.objHair[kind] && id == base.fileHair.parts[kind].id)
		{
			yield break;
		}
		if ((bool)base.objHair[kind])
		{
			SafeDestroy(base.objHair[kind]);
			base.objHair[kind] = null;
			base.infoHair[kind] = null;
			base.cusHairCmp[kind] = null;
		}
		GameObject referenceInfo = GetReferenceInfo(RefObjKey.HairParent);
		if (asyncFlags)
		{
			IEnumerator routine = LoadCharaFbxDataAsync(delegate(GameObject o)
			{
				base.objHair[kind] = o;
			}, delegate(ListInfoBase lib)
			{
				base.infoHair[kind] = lib;
			}, base.hiPoly, (int)((base.sex == 0 && 1 == base.exType && kind == 0) ? ChaListDefine.CategoryNo.ex_bo_hair : array[kind]), id, array2[kind], copyDynamicBone: false, 0, referenceInfo.transform, array3[kind]);
			yield return StartCoroutine(routine);
		}
		else
		{
			base.objHair[kind] = LoadCharaFbxData(delegate(ListInfoBase lib)
			{
				base.infoHair[kind] = lib;
			}, base.hiPoly, (int)((base.sex == 0 && 1 == base.exType && kind == 0) ? ChaListDefine.CategoryNo.ex_bo_hair : array[kind]), id, array2[kind], copyDynamicBone: false, 0, referenceInfo.transform, array3[kind]);
		}
		if ((bool)base.objHair[kind])
		{
			ListInfoBase listInfoBase = base.infoHair[kind];
			base.fileHair.parts[kind].id = listInfoBase.Id;
			if (kind == 0)
			{
				base.fileHair.kind = listInfoBase.Kind;
			}
			base.cusHairCmp[kind] = base.objHair[kind].GetComponent<ChaCustomHairComponent>();
			if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy")
			{
				_ = null == base.cusHairCmp[kind];
			}
			ChangeSettingHairGlossMask(kind);
			ChangeSettingHairColor(kind, c00: true, c01: true, c02: true);
			ChangeSettingHairOutlineColor(kind);
			ChangeSettingHairGlossColor(kind);
			ChangeSettingHairLength(kind);
			ChangeSettingHairAcsColor(kind);
			if (!base.hiPoly && kind == 1)
			{
				DynamicBone[] componentsInChildren = base.objHair[kind].GetComponentsInChildren<DynamicBone>(includeInactive: true);
				for (int num = 0; num < componentsInChildren.Length; num++)
				{
					componentsInChildren[num].enabled = false;
				}
			}
			else
			{
				ChangeShakeHair(kind);
			}
		}
		if (asyncFlags)
		{
			yield return null;
		}
	}

	public void ChangeClothes(bool forceChange = false, bool update = true)
	{
		int[] clothesKinds = ChaFileDefine.clothesKinds;
		foreach (int num in clothesKinds)
		{
			ChangeClothesNoAsync(num, nowCoordinate.clothes.parts[num].id, nowCoordinate.clothes.subPartsId[0], nowCoordinate.clothes.subPartsId[1], nowCoordinate.clothes.subPartsId[2], forceChange, update);
		}
	}

	public void ChangeClothes(int kind, int id, int subId01, int subId02, int subId03, bool forceChange = false)
	{
		StartCoroutine(ChangeClothesAsync(kind, id, subId01, subId02, subId03, forceChange, asyncFlags: false));
	}

	public IEnumerator ChangeClothesAsync(bool forceChange = false)
	{
		int[] array = (int[])Enum.GetValues(typeof(ChaFileDefine.ClothesKind));
		int[] array2 = array;
		foreach (int num in array2)
		{
			yield return StartCoroutine(ChangeClothesAsync(num, nowCoordinate.clothes.parts[num].id, nowCoordinate.clothes.subPartsId[0], nowCoordinate.clothes.subPartsId[1], nowCoordinate.clothes.subPartsId[2], forceChange));
		}
	}

	public IEnumerator ChangeClothesAsync(int kind, int id, int subId01, int subId02, int subId03, bool forceChange = false, bool asyncFlags = true)
	{
		if (asyncFlags)
		{
			switch (kind)
			{
			case 0:
				yield return StartCoroutine(ChangeClothesTopAsync(id, subId01, subId02, subId03, forceChange));
				updateAlphaMask = true;
				break;
			case 1:
				if (base.sex != 0 || 1 != base.exType)
				{
					yield return StartCoroutine(ChangeClothesBotAsync(id, forceChange));
				}
				break;
			case 2:
				if (base.sex != 0 || 1 != base.exType)
				{
					yield return StartCoroutine(ChangeClothesBraAsync(id, forceChange));
				}
				break;
			case 3:
				if (base.sex != 0 || 1 != base.exType)
				{
					yield return StartCoroutine(ChangeClothesShortsAsync(id, forceChange));
				}
				break;
			case 4:
				if (base.sex != 0 || 1 != base.exType)
				{
					yield return StartCoroutine(ChangeClothesGlovesAsync(id, forceChange));
				}
				break;
			case 5:
				if (base.sex != 0 || 1 != base.exType)
				{
					yield return StartCoroutine(ChangeClothesPanstAsync(id, forceChange));
				}
				break;
			case 6:
				if (base.sex != 0 || 1 != base.exType)
				{
					yield return StartCoroutine(ChangeClothesSocksAsync(id, forceChange));
				}
				break;
			case 7:
				yield return StartCoroutine(ChangeClothesShoesAsync(0, id, forceChange));
				break;
			case 8:
				yield return StartCoroutine(ChangeClothesShoesAsync(1, id, forceChange));
				break;
			}
			yield break;
		}
		switch (kind)
		{
		case 0:
			ChangeClothesTop(id, subId01, subId02, subId03, forceChange);
			updateAlphaMask = true;
			break;
		case 1:
			if (base.sex != 0 || 1 != base.exType)
			{
				ChangeClothesBot(id, forceChange);
			}
			break;
		case 2:
			if (base.sex != 0 || 1 != base.exType)
			{
				ChangeClothesBra(id, forceChange);
			}
			break;
		case 3:
			if (base.sex != 0 || 1 != base.exType)
			{
				ChangeClothesShorts(id, forceChange);
			}
			break;
		case 4:
			if (base.sex != 0 || 1 != base.exType)
			{
				ChangeClothesGloves(id, forceChange);
			}
			break;
		case 5:
			if (base.sex != 0 || 1 != base.exType)
			{
				ChangeClothesPanst(id, forceChange);
			}
			break;
		case 6:
			if (base.sex != 0 || 1 != base.exType)
			{
				ChangeClothesSocks(id, forceChange);
			}
			break;
		case 7:
			ChangeClothesShoes(0, id, forceChange);
			break;
		case 8:
			ChangeClothesShoes(1, id, forceChange);
			break;
		}
	}

	public void ChangeClothesTop(int id, int subId01, int subId02, int subId03, bool forceChange = false, bool update = true)
	{
		ChangeClothesTopNoAsync(id, subId01, subId02, subId03, forceChange, update);
	}

	public IEnumerator ChangeClothesTopAsync(int id, int subId01, int subId02, int subId03, bool forceChange = false, bool asyncFlags = true)
	{
		updateAlphaMask = true;
		bool load = true;
		bool flag = true;
		int kindNo = 0;
		string objName = "ct_clothesTop";
		int num = ((base.infoClothes[kindNo] == null) ? (-1) : base.infoClothes[kindNo].Id);
		if (!forceChange && null != base.objClothes[kindNo] && id == num)
		{
			load = false;
			flag = false;
		}
		ReleaseBaseCustomTextureClothes(main: true, kindNo);
		if (flag && (bool)base.objClothes[kindNo])
		{
			SafeDestroy(base.objClothes[kindNo]);
			base.objClothes[kindNo] = null;
			BustNormal value = null;
			if (dictBustNormal.TryGetValue(BustNormalKind.NmlTop, out value))
			{
				value.Release();
			}
			base.infoClothes[kindNo] = null;
			base.cusClothesCmp[kindNo] = null;
			ReleaseRefObject(5uL);
			notBra = false;
			notBot = false;
			RemoveClothesStateKind(kindNo);
			if (null != texBodyAlphaMask && (bool)base.customMatBody)
			{
				base.customMatBody.SetTexture(ChaShader._AlphaMask, null);
			}
			texBodyAlphaMask = null;
			texBraAlphaMask = null;
		}
		if (load)
		{
			if (asyncFlags)
			{
				yield return null;
			}
			if (asyncFlags)
			{
				IEnumerator routine = LoadCharaFbxDataAsync(delegate(GameObject o)
				{
					base.objClothes[kindNo] = o;
				}, delegate(ListInfoBase lib)
				{
					base.infoClothes[kindNo] = lib;
				}, base.hiPoly, (base.sex == 0 && 1 == base.exType) ? 503 : 105, id, objName, copyDynamicBone: true, 1, base.objTop.transform, 0);
				yield return StartCoroutine(routine);
			}
			else
			{
				base.objClothes[kindNo] = LoadCharaFbxData(delegate(ListInfoBase lib)
				{
					base.infoClothes[kindNo] = lib;
				}, base.hiPoly, (base.sex == 0 && 1 == base.exType) ? 503 : 105, id, objName, copyDynamicBone: true, 1, base.objTop.transform, 0);
			}
			if (asyncFlags)
			{
				yield return null;
			}
			if ((bool)base.objClothes[kindNo])
			{
				ListInfoBase listInfoBase = base.infoClothes[kindNo];
				base.cusClothesCmp[kindNo] = base.objClothes[kindNo].GetComponent<ChaClothesComponent>();
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy")
				{
					_ = null == base.cusClothesCmp[kindNo];
				}
				nowCoordinate.clothes.parts[kindNo].id = listInfoBase.Id;
				CreateReferenceInfo(5uL, base.objClothes[kindNo]);
				notBra = ((1 == listInfoBase.GetInfoInt(ChaListDefine.KeyType.NotBra)) ? true : false);
				notBot = ((2 == listInfoBase.GetInfoInt(ChaListDefine.KeyType.Coordinate)) ? true : false);
				AddClothesStateKind(kindNo, listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
				LoadAlphaMaskTexture(listInfoBase.GetInfo(ChaListDefine.KeyType.OverBodyMaskAB), listInfoBase.GetInfo(ChaListDefine.KeyType.OverBodyMask), 0);
				if ((bool)base.customMatBody)
				{
					base.customMatBody.SetTexture(ChaShader._AlphaMask, texBodyAlphaMask);
				}
				LoadAlphaMaskTexture(listInfoBase.GetInfo(ChaListDefine.KeyType.OverBraMaskAB), listInfoBase.GetInfo(ChaListDefine.KeyType.OverBraMask), 1);
				if (base.rendBra != null)
				{
					Vector2[] array = new Vector2[2]
					{
						new Vector2(0f, 0f),
						new Vector2(0f, -1f)
					};
					Vector2[] array2 = new Vector2[2]
					{
						new Vector2(1f, 1f),
						new Vector2(1f, 2f)
					};
					ListInfoBase listInfoBase2 = base.infoClothes[2];
					if (listInfoBase2 != null)
					{
						int num2 = ((2 == listInfoBase2.GetInfoInt(ChaListDefine.KeyType.Coordinate)) ? 1 : 0);
						if (listInfoBase2.dictInfo.TryGetValue(105, out var value2))
						{
							num2 = (("1" == value2) ? 1 : 0);
						}
						if ((bool)base.rendBra[0])
						{
							base.rendBra[0].material.SetTexture(ChaShader._AlphaMask, texBraAlphaMask);
							base.rendBra[0].material.SetTextureOffset(ChaShader._AlphaMask, array[num2]);
							base.rendBra[0].material.SetTextureScale(ChaShader._AlphaMask, array2[num2]);
						}
						if ((bool)base.rendBra[1])
						{
							base.rendBra[1].material.SetTexture(ChaShader._AlphaMask, texBraAlphaMask);
							base.rendBra[1].material.SetTextureOffset(ChaShader._AlphaMask, array[num2]);
							base.rendBra[1].material.SetTextureScale(ChaShader._AlphaMask, array2[num2]);
						}
					}
				}
				string info = listInfoBase.GetInfo(ChaListDefine.KeyType.NormalData);
				if ("0" != info)
				{
					BustNormal value3 = null;
					if (!dictBustNormal.TryGetValue(BustNormalKind.NmlTop, out value3))
					{
						value3 = new BustNormal();
					}
					value3.Init(base.objClothes[kindNo], listInfoBase.GetInfo(ChaListDefine.KeyType.MainAB), info, listInfoBase.GetInfo(ChaListDefine.KeyType.MainManifest));
					dictBustNormal[BustNormalKind.NmlTop] = value3;
				}
			}
		}
		if ((bool)base.objClothes[kindNo])
		{
			InitBaseCustomTextureClothes(main: true, kindNo);
			if (base.loadWithDefaultColorAndPtn)
			{
				SetClothesDefaultColor(kindNo);
				for (int num3 = 0; num3 < 4; num3++)
				{
					nowCoordinate.clothes.parts[kindNo].colorInfo[num3].pattern = 0;
				}
			}
			ChangeCustomClothes(main: true, kindNo, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true, updateTex04: false);
			for (int num4 = 0; num4 < 2; num4++)
			{
				ChangeCustomEmblem(kindNo, num4);
			}
			if (base.releaseCustomInputTexture)
			{
				ReleaseBaseCustomTextureClothes(main: true, kindNo, createTex: false);
			}
			int[] partsKindNo = new int[3] { 0, 1, 2 };
			int[] partsID = new int[3] { subId01, subId02, subId03 };
			int partsFig = Enum.GetNames(typeof(ChaFileDefine.ClothesSubKind)).Length;
			bool forceChange2 = load || forceChange;
			if (base.sex != 0 || 1 != base.exType)
			{
				if (asyncFlags)
				{
					int i = 0;
					while (i < partsFig)
					{
						yield return StartCoroutine(ChangeClothesTopPartsAsync(partsKindNo[i], partsID[i], forceChange));
						int num5 = i + 1;
						i = num5;
					}
				}
				else
				{
					for (int num6 = 0; num6 < partsFig; num6++)
					{
						ChangeClothesTopParts(partsKindNo[num6], partsID[num6], forceChange2);
					}
				}
			}
		}
		if (base.hiPoly)
		{
			UpdateSiru(forceChange: true);
		}
		if (!("0" == base.infoClothes[kindNo].GetInfo(ChaListDefine.KeyType.Coordinate)))
		{
			yield break;
		}
		int num7 = 1;
		if (null == base.objClothes[num7])
		{
			if (asyncFlags)
			{
				yield return StartCoroutine(ChangeClothesBotAsync(nowCoordinate.clothes.parts[num7].id));
			}
			else
			{
				ChangeClothesBot(nowCoordinate.clothes.parts[num7].id);
			}
		}
	}

	public void ChangeClothesTopParts(int kind, int id, bool forceChange = false)
	{
		ChangeClothesTopPartsNoAsync(kind, id, forceChange);
	}

	public IEnumerator ChangeClothesTopPartsAsync(int kind, int id, bool forceChange = false, bool asyncFlags = true)
	{
		bool flag = true;
		bool flag2 = true;
		int num = ((base.infoParts[kind] == null) ? (-1) : base.infoParts[kind].Id);
		if (!forceChange && null != base.objParts[kind] && id == num)
		{
			flag = false;
			flag2 = false;
		}
		int topType = 0;
		ListInfoBase listInfoBase = base.infoClothes[0];
		if (listInfoBase != null)
		{
			topType = listInfoBase.Kind;
		}
		bool flag3 = 1 != topType && 2 != topType;
		if (flag3 || null == base.objClothes[0])
		{
			flag = false;
			flag2 = true;
			nowCoordinate.clothes.subPartsId[kind] = 0;
		}
		ReleaseBaseCustomTextureClothes(main: false, kind);
		if (flag2 && (bool)base.objParts[kind])
		{
			SafeDestroy(base.objParts[kind]);
			base.objParts[kind] = null;
			BustNormal value = null;
			if (dictBustNormal.TryGetValue(BustNormalKind.NmlTopPA, out value))
			{
				value.Release();
			}
			base.infoParts[kind] = null;
			base.cusClothesSubCmp[kind] = null;
			if ((1 == topType && kind == 0) || (2 == topType && kind != 2))
			{
				notBra = false;
			}
			ReleaseRefObject(13uL + (ulong)kind);
			if (!flag3)
			{
				if (null != texBodyAlphaMask && kind == 0)
				{
					if ((bool)base.customMatBody)
					{
						base.customMatBody.SetTexture(ChaShader._AlphaMask, null);
					}
					texBodyAlphaMask = null;
				}
				if (null != texBraAlphaMask && kind == 0)
				{
					if (base.rendBra != null)
					{
						Renderer[] array = base.rendBra;
						foreach (Renderer renderer in array)
						{
							if (null != renderer)
							{
								renderer.material.SetTexture(ChaShader._AlphaMask, null);
							}
						}
					}
					texBraAlphaMask = null;
				}
			}
			if (kind == 0)
			{
				base.rendInner = new Renderer[2];
			}
			if (1 == kind && null != texInnerAlphaMask)
			{
				if (base.rendInner != null)
				{
					Renderer[] array = base.rendInner;
					foreach (Renderer renderer2 in array)
					{
						if (null != renderer2)
						{
							renderer2.material.SetTexture(ChaShader._AlphaMask, null);
						}
					}
				}
				texInnerAlphaMask = null;
			}
		}
		if (flag)
		{
			string[] goName = new string[3] { "ct_top_parts_A", "ct_top_parts_B", "ct_top_parts_C" };
			int cateNo = ((1 == topType) ? 200 : 210) + kind;
			if (asyncFlags)
			{
				yield return null;
			}
			int[,] array2 = new int[2, 3]
			{
				{ 0, 0, 1 },
				{ 0, 1, 1 }
			};
			int num2 = ((1 != topType) ? 1 : 0);
			if (asyncFlags)
			{
				IEnumerator routine = LoadCharaFbxDataAsync(delegate(GameObject o)
				{
					base.objParts[kind] = o;
				}, delegate(ListInfoBase lib)
				{
					base.infoParts[kind] = lib;
				}, base.hiPoly, cateNo, id, goName[kind], copyDynamicBone: true, 1, base.objClothes[0].transform, array2[num2, kind]);
				yield return StartCoroutine(routine);
			}
			else
			{
				base.objParts[kind] = LoadCharaFbxData(delegate(ListInfoBase lib)
				{
					base.infoParts[kind] = lib;
				}, base.hiPoly, cateNo, id, goName[kind], copyDynamicBone: true, 1, base.objClothes[0].transform, array2[num2, kind]);
			}
			if (asyncFlags)
			{
				yield return null;
			}
			if ((bool)base.objParts[kind])
			{
				ListInfoBase listInfoBase2 = base.infoParts[kind];
				base.cusClothesSubCmp[kind] = base.objParts[kind].GetComponent<ChaClothesComponent>();
				if (listInfoBase2.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy")
				{
					_ = null == base.cusClothesSubCmp[kind];
				}
				nowCoordinate.clothes.subPartsId[kind] = listInfoBase2.Id;
				if (2 == topType)
				{
					if (kind == 0)
					{
						if (7 == listInfoBase2.Id)
						{
							base.cusClothesSubCmp[kind].useColorN02 = true;
							base.cusClothesSubCmp[kind].useColorN03 = false;
						}
						else if (9 == listInfoBase2.Id)
						{
							base.cusClothesSubCmp[kind].useColorN03 = true;
						}
					}
					else if (1 == kind)
					{
						if (11 == listInfoBase2.Id)
						{
							base.cusClothesSubCmp[kind].useColorN01 = false;
							base.cusClothesSubCmp[kind].useColorN02 = true;
							base.cusClothesSubCmp[kind].useColorN03 = false;
						}
						else if (12 == listInfoBase2.Id)
						{
							base.cusClothesSubCmp[kind].useColorN01 = false;
							base.cusClothesSubCmp[kind].useColorN02 = true;
							base.cusClothesSubCmp[kind].useColorN03 = false;
						}
						else if (9 == listInfoBase2.Id)
						{
							base.cusClothesSubCmp[kind].useColorN01 = false;
							base.cusClothesSubCmp[kind].useColorN02 = true;
							base.cusClothesSubCmp[kind].useColorN03 = false;
						}
					}
				}
				CreateReferenceInfo(13uL + (ulong)kind, base.objParts[kind]);
				string info = listInfoBase2.GetInfo(ChaListDefine.KeyType.NormalData);
				if (1 == topType && kind == 0)
				{
					notBra = ((1 == listInfoBase2.GetInfoInt(ChaListDefine.KeyType.NotBra)) ? true : false);
				}
				if (2 == topType)
				{
					ListInfoBase listInfoBase3 = base.infoParts[0];
					ListInfoBase listInfoBase4 = base.infoParts[1];
					notBra = false;
					if (listInfoBase3 != null && 1 == listInfoBase3.GetInfoInt(ChaListDefine.KeyType.NotBra))
					{
						notBra = true;
					}
					if (listInfoBase4 != null && 1 == listInfoBase4.GetInfoInt(ChaListDefine.KeyType.NotBra))
					{
						notBra = true;
					}
				}
				if ("0" != info)
				{
					BustNormal value2 = null;
					if (!dictBustNormal.TryGetValue(BustNormalKind.NmlTopPA, out value2))
					{
						value2 = new BustNormal();
					}
					value2.Init(base.objParts[kind], listInfoBase2.GetInfo(ChaListDefine.KeyType.MainAB), info, listInfoBase2.GetInfo(ChaListDefine.KeyType.MainManifest));
					dictBustNormal[BustNormalKind.NmlTopPA] = value2;
				}
				if (kind == 0)
				{
					GameObject referenceInfo = GetReferenceInfo(RefObjKey.ObjInnerDef);
					if (null != referenceInfo)
					{
						base.rendInner[0] = referenceInfo.GetComponent<Renderer>();
					}
					referenceInfo = GetReferenceInfo(RefObjKey.ObjInnerNuge);
					if (null != referenceInfo)
					{
						base.rendInner[1] = referenceInfo.GetComponent<Renderer>();
					}
					if (base.rendInner != null)
					{
						if ((bool)base.rendInner[0])
						{
							base.rendInner[0].material.SetTexture(ChaShader._AlphaMask, texInnerAlphaMask);
						}
						if ((bool)base.rendInner[1])
						{
							base.rendInner[1].material.SetTexture(ChaShader._AlphaMask, texInnerAlphaMask);
						}
					}
					LoadAlphaMaskTexture(listInfoBase2.GetInfo(ChaListDefine.KeyType.OverBodyMaskAB), listInfoBase2.GetInfo(ChaListDefine.KeyType.OverBodyMask), 0);
					if ((bool)base.customMatBody)
					{
						base.customMatBody.SetTexture(ChaShader._AlphaMask, texBodyAlphaMask);
					}
					LoadAlphaMaskTexture(listInfoBase2.GetInfo(ChaListDefine.KeyType.OverBraMaskAB), listInfoBase2.GetInfo(ChaListDefine.KeyType.OverBraMask), 1);
					if (base.rendBra != null)
					{
						Vector2[] array3 = new Vector2[2]
						{
							new Vector2(0f, 0f),
							new Vector2(0f, -1f)
						};
						Vector2[] array4 = new Vector2[2]
						{
							new Vector2(1f, 1f),
							new Vector2(1f, 2f)
						};
						ListInfoBase listInfoBase5 = base.infoClothes[2];
						if (listInfoBase5 != null)
						{
							int num3 = ((2 == listInfoBase5.GetInfoInt(ChaListDefine.KeyType.Coordinate)) ? 1 : 0);
							if (listInfoBase5.dictInfo.TryGetValue(105, out var value3))
							{
								num3 = (("1" == value3) ? 1 : 0);
							}
							if ((bool)base.rendBra[0])
							{
								base.rendBra[0].material.SetTexture(ChaShader._AlphaMask, texBraAlphaMask);
								base.rendBra[0].material.SetTextureOffset(ChaShader._AlphaMask, array3[num3]);
								base.rendBra[0].material.SetTextureScale(ChaShader._AlphaMask, array4[num3]);
							}
							if ((bool)base.rendBra[1])
							{
								base.rendBra[1].material.SetTexture(ChaShader._AlphaMask, texBraAlphaMask);
								base.rendBra[1].material.SetTextureOffset(ChaShader._AlphaMask, array3[num3]);
								base.rendBra[1].material.SetTextureScale(ChaShader._AlphaMask, array4[num3]);
							}
						}
					}
				}
				else if (1 == kind)
				{
					LoadAlphaMaskTexture(listInfoBase2.GetInfo(ChaListDefine.KeyType.OverInnerMaskAB), listInfoBase2.GetInfo(ChaListDefine.KeyType.OverInnerMask), 2);
					if (base.rendInner != null)
					{
						if ((bool)base.rendInner[0])
						{
							base.rendInner[0].material.SetTexture(ChaShader._AlphaMask, texInnerAlphaMask);
						}
						if ((bool)base.rendInner[1])
						{
							base.rendInner[1].material.SetTexture(ChaShader._AlphaMask, texInnerAlphaMask);
						}
					}
					byte b = base.fileStatus.clothesState[0];
					ChangeAlphaMask(ChaFileDefine.alphaState[b, 0], ChaFileDefine.alphaState[b, 1]);
				}
			}
		}
		if (!base.objParts[kind])
		{
			yield break;
		}
		InitBaseCustomTextureClothes(main: false, kind);
		if (base.loadWithDefaultColorAndPtn)
		{
			SetClothesDefaultColor(0);
			for (int num4 = 0; num4 < 4; num4++)
			{
				nowCoordinate.clothes.parts[0].colorInfo[num4].pattern = 0;
			}
		}
		ChangeCustomClothes(main: false, kind, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true, updateTex04: true);
		for (int num5 = 0; num5 < 2; num5++)
		{
			ChangeCustomEmblem(0, num5);
		}
		if (base.releaseCustomInputTexture)
		{
			ReleaseBaseCustomTextureClothes(main: true, kind, createTex: false);
		}
	}

	public void ChangeClothesBot(int id, bool forceChange = false, bool update = true)
	{
		ChangeClothesBotNoAsync(id, forceChange, update);
	}

	public IEnumerator ChangeClothesBotAsync(int id, bool forceChange = false, bool asyncFlags = true)
	{
		bool flag = true;
		bool flag2 = true;
		int kindNo = 1;
		string objName = "ct_clothesBot";
		hideShortsBot = false;
		int num = ((base.infoClothes[kindNo] == null) ? (-1) : base.infoClothes[kindNo].Id);
		if (!forceChange && null != base.objClothes[kindNo] && id == num)
		{
			flag = false;
			flag2 = false;
		}
		if (base.lstCtrl.CountInfo(ChaListDefine.CategoryNo.co_top) != 0)
		{
			ListInfoBase info = base.lstCtrl.GetInfo(ChaListDefine.CategoryNo.co_top, nowCoordinate.clothes.parts[0].id);
			if (info != null && "2" == info.GetInfo(ChaListDefine.KeyType.Coordinate))
			{
				flag = false;
				flag2 = true;
			}
		}
		ReleaseBaseCustomTextureClothes(main: true, kindNo);
		if (flag2 && (bool)base.objClothes[kindNo])
		{
			SafeDestroy(base.objClothes[kindNo]);
			base.objClothes[kindNo] = null;
			base.infoClothes[kindNo] = null;
			base.cusClothesCmp[kindNo] = null;
			ReleaseRefObject(6uL);
			RemoveClothesStateKind(kindNo);
		}
		if (flag)
		{
			if (asyncFlags)
			{
				yield return null;
			}
			if (asyncFlags)
			{
				IEnumerator routine = LoadCharaFbxDataAsync(delegate(GameObject o)
				{
					base.objClothes[kindNo] = o;
				}, delegate(ListInfoBase lib)
				{
					base.infoClothes[kindNo] = lib;
				}, base.hiPoly, 106, id, objName, copyDynamicBone: true, 1, base.objTop.transform, 0);
				yield return StartCoroutine(routine);
			}
			else
			{
				base.objClothes[kindNo] = LoadCharaFbxData(delegate(ListInfoBase lib)
				{
					base.infoClothes[kindNo] = lib;
				}, base.hiPoly, 106, id, objName, copyDynamicBone: true, 1, base.objTop.transform, 0);
			}
			if (asyncFlags)
			{
				yield return null;
			}
			if ((bool)base.objClothes[kindNo])
			{
				ListInfoBase listInfoBase = base.infoClothes[kindNo];
				base.cusClothesCmp[kindNo] = base.objClothes[kindNo].GetComponent<ChaClothesComponent>();
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy")
				{
					_ = null == base.cusClothesCmp[kindNo];
				}
				if (listInfoBase.dictInfo.TryGetValue(106, out var value))
				{
					if (value == "1")
					{
						hideShortsBot = true;
					}
				}
				else if (new int[13]
				{
					2, 3, 9, 16, 22, 25, 28, 29, 30, 32,
					34, 35, 45
				}.Contains(id))
				{
					hideShortsBot = true;
				}
				nowCoordinate.clothes.parts[kindNo].id = listInfoBase.Id;
				CreateReferenceInfo(6uL, base.objClothes[kindNo]);
				AddClothesStateKind(kindNo, listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
			}
		}
		if ((bool)base.objClothes[kindNo])
		{
			InitBaseCustomTextureClothes(main: true, kindNo);
			if (base.loadWithDefaultColorAndPtn)
			{
				SetClothesDefaultColor(kindNo);
				for (int num2 = 0; num2 < 4; num2++)
				{
					nowCoordinate.clothes.parts[kindNo].colorInfo[num2].pattern = 0;
				}
			}
			ChangeCustomClothes(main: true, kindNo, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true, updateTex04: false);
			for (int num3 = 0; num3 < 2; num3++)
			{
				ChangeCustomEmblem(kindNo, num3);
			}
			if (base.releaseCustomInputTexture)
			{
				ReleaseBaseCustomTextureClothes(main: true, kindNo, createTex: false);
			}
		}
		if (base.hiPoly)
		{
			UpdateSiru(forceChange: true);
		}
	}

	public void ChangeClothesBra(int id, bool forceChange = false, bool update = true)
	{
		ChangeClothesBraNoAsync(id, forceChange, update);
	}

	public IEnumerator ChangeClothesBraAsync(int id, bool forceChange = false, bool asyncFlags = true)
	{
		bool flag = true;
		bool flag2 = true;
		int kindNo = 2;
		string objName = "ct_bra";
		int num = ((base.infoClothes[kindNo] == null) ? (-1) : base.infoClothes[kindNo].Id);
		if (!forceChange && null != base.objClothes[kindNo] && id == num)
		{
			flag = false;
			flag2 = false;
		}
		ReleaseBaseCustomTextureClothes(main: true, kindNo);
		if (flag2 && (bool)base.objClothes[kindNo])
		{
			SafeDestroy(base.objClothes[kindNo]);
			base.objClothes[kindNo] = null;
			BustNormal value = null;
			if (dictBustNormal.TryGetValue(BustNormalKind.NmlBra, out value))
			{
				value.Release();
			}
			base.infoClothes[kindNo] = null;
			base.cusClothesCmp[kindNo] = null;
			ReleaseRefObject(7uL);
			notShorts = false;
			RemoveClothesStateKind(kindNo);
			base.rendBra = new Renderer[2];
		}
		if (flag)
		{
			if (asyncFlags)
			{
				yield return null;
			}
			if (asyncFlags)
			{
				IEnumerator routine = LoadCharaFbxDataAsync(delegate(GameObject o)
				{
					base.objClothes[kindNo] = o;
				}, delegate(ListInfoBase lib)
				{
					base.infoClothes[kindNo] = lib;
				}, base.hiPoly, 107, id, objName, copyDynamicBone: false, 1, base.objTop.transform, (base.sex == 0) ? ChaFileDefine.DefClothesMBraID : ChaFileDefine.DefClothesFBraID);
				yield return StartCoroutine(routine);
			}
			else
			{
				base.objClothes[kindNo] = LoadCharaFbxData(delegate(ListInfoBase lib)
				{
					base.infoClothes[kindNo] = lib;
				}, base.hiPoly, 107, id, objName, copyDynamicBone: false, 1, base.objTop.transform, (base.sex == 0) ? ChaFileDefine.DefClothesMBraID : ChaFileDefine.DefClothesFBraID);
			}
			if (asyncFlags)
			{
				yield return null;
			}
			if ((bool)base.objClothes[kindNo])
			{
				ListInfoBase listInfoBase = base.infoClothes[kindNo];
				base.cusClothesCmp[kindNo] = base.objClothes[kindNo].GetComponent<ChaClothesComponent>();
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy")
				{
					_ = null == base.cusClothesCmp[kindNo];
				}
				notShorts = (("2" == listInfoBase.GetInfo(ChaListDefine.KeyType.Coordinate)) ? true : false);
				nowCoordinate.clothes.parts[kindNo].id = listInfoBase.Id;
				CreateReferenceInfo(7uL, base.objClothes[kindNo]);
				AddClothesStateKind(kindNo, listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
				GameObject referenceInfo = GetReferenceInfo(RefObjKey.ObjBraDef);
				if (null != referenceInfo)
				{
					base.rendBra[0] = referenceInfo.GetComponent<Renderer>();
				}
				referenceInfo = GetReferenceInfo(RefObjKey.ObjBraNuge);
				if (null != referenceInfo)
				{
					base.rendBra[1] = referenceInfo.GetComponent<Renderer>();
				}
				if (base.rendBra != null)
				{
					Vector2[] array = new Vector2[2]
					{
						new Vector2(0f, 0f),
						new Vector2(0f, -1f)
					};
					Vector2[] array2 = new Vector2[2]
					{
						new Vector2(1f, 1f),
						new Vector2(1f, 2f)
					};
					int num2 = ((2 == listInfoBase.GetInfoInt(ChaListDefine.KeyType.Coordinate)) ? 1 : 0);
					if (listInfoBase.dictInfo.TryGetValue(105, out var value2))
					{
						num2 = (("1" == value2) ? 1 : 0);
					}
					if ((bool)base.rendBra[0])
					{
						base.rendBra[0].material.SetTexture(ChaShader._AlphaMask, texBraAlphaMask);
						base.rendBra[0].material.SetTextureOffset(ChaShader._AlphaMask, array[num2]);
						base.rendBra[0].material.SetTextureScale(ChaShader._AlphaMask, array2[num2]);
					}
					if ((bool)base.rendBra[1])
					{
						base.rendBra[1].material.SetTexture(ChaShader._AlphaMask, texBraAlphaMask);
						base.rendBra[1].material.SetTextureOffset(ChaShader._AlphaMask, array[num2]);
						base.rendBra[1].material.SetTextureScale(ChaShader._AlphaMask, array2[num2]);
					}
				}
				byte b = base.fileStatus.clothesState[0];
				ChangeAlphaMask(ChaFileDefine.alphaState[b, 0], ChaFileDefine.alphaState[b, 1]);
				string info = listInfoBase.GetInfo(ChaListDefine.KeyType.NormalData);
				if ("0" != info)
				{
					BustNormal value3 = null;
					if (!dictBustNormal.TryGetValue(BustNormalKind.NmlBra, out value3))
					{
						value3 = new BustNormal();
					}
					value3.Init(base.objClothes[kindNo], listInfoBase.GetInfo(ChaListDefine.KeyType.MainAB), info, listInfoBase.GetInfo(ChaListDefine.KeyType.MainManifest));
					dictBustNormal[BustNormalKind.NmlBra] = value3;
				}
			}
		}
		if ((bool)base.objClothes[kindNo])
		{
			InitBaseCustomTextureClothes(main: true, kindNo);
			if (base.loadWithDefaultColorAndPtn)
			{
				SetClothesDefaultColor(kindNo);
				for (int num3 = 0; num3 < 4; num3++)
				{
					nowCoordinate.clothes.parts[kindNo].colorInfo[num3].pattern = 0;
				}
			}
			ChangeCustomClothes(main: true, kindNo, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true, updateTex04: false);
			for (int num4 = 0; num4 < 2; num4++)
			{
				ChangeCustomEmblem(kindNo, num4);
			}
			if (base.releaseCustomInputTexture)
			{
				ReleaseBaseCustomTextureClothes(main: true, kindNo, createTex: false);
			}
		}
		if (base.hiPoly)
		{
			UpdateSiru(forceChange: true);
		}
		if (!("0" == base.infoClothes[kindNo].GetInfo(ChaListDefine.KeyType.Coordinate)))
		{
			yield break;
		}
		int num5 = 3;
		if (null == base.objClothes[num5])
		{
			if (asyncFlags)
			{
				yield return StartCoroutine(ChangeClothesShortsAsync(nowCoordinate.clothes.parts[num5].id));
			}
			else
			{
				ChangeClothesShorts(nowCoordinate.clothes.parts[num5].id);
			}
		}
	}

	public void ChangeClothesShorts(int id, bool forceChange = false, bool update = true)
	{
		ChangeClothesShortsNoAsync(id, forceChange, update);
	}

	public IEnumerator ChangeClothesShortsAsync(int id, bool forceChange = false, bool asyncFlags = true)
	{
		bool flag = true;
		bool flag2 = true;
		int kindNo = 3;
		string objName = "ct_shorts";
		int num = ((base.infoClothes[kindNo] == null) ? (-1) : base.infoClothes[kindNo].Id);
		if (!forceChange && null != base.objClothes[kindNo] && id == num)
		{
			flag = false;
			flag2 = false;
		}
		if (base.lstCtrl.CountInfo(ChaListDefine.CategoryNo.co_bra) != 0)
		{
			ListInfoBase info = base.lstCtrl.GetInfo(ChaListDefine.CategoryNo.co_bra, nowCoordinate.clothes.parts[2].id);
			if (info != null && "2" == info.GetInfo(ChaListDefine.KeyType.Coordinate))
			{
				flag = false;
				flag2 = true;
			}
		}
		ReleaseBaseCustomTextureClothes(main: true, kindNo);
		if (flag2 && (bool)base.objClothes[kindNo])
		{
			SafeDestroy(base.objClothes[kindNo]);
			base.objClothes[kindNo] = null;
			base.infoClothes[kindNo] = null;
			base.cusClothesCmp[kindNo] = null;
			ReleaseRefObject(8uL);
			RemoveClothesStateKind(kindNo);
		}
		if (flag)
		{
			if (asyncFlags)
			{
				yield return null;
			}
			if (asyncFlags)
			{
				IEnumerator routine = LoadCharaFbxDataAsync(delegate(GameObject o)
				{
					base.objClothes[kindNo] = o;
				}, delegate(ListInfoBase lib)
				{
					base.infoClothes[kindNo] = lib;
				}, base.hiPoly, 108, id, objName, copyDynamicBone: false, 1, base.objTop.transform, (base.sex == 0) ? ChaFileDefine.DefClothesMShortsID : ChaFileDefine.DefClothesFShortsID);
				yield return StartCoroutine(routine);
			}
			else
			{
				base.objClothes[kindNo] = LoadCharaFbxData(delegate(ListInfoBase lib)
				{
					base.infoClothes[kindNo] = lib;
				}, base.hiPoly, 108, id, objName, copyDynamicBone: false, 1, base.objTop.transform, (base.sex == 0) ? ChaFileDefine.DefClothesMShortsID : ChaFileDefine.DefClothesFShortsID);
			}
			if (asyncFlags)
			{
				yield return null;
			}
			if ((bool)base.objClothes[kindNo])
			{
				ListInfoBase listInfoBase = base.infoClothes[kindNo];
				base.cusClothesCmp[kindNo] = base.objClothes[kindNo].GetComponent<ChaClothesComponent>();
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy")
				{
					_ = null == base.cusClothesCmp[kindNo];
				}
				if (listInfoBase.Id == 17)
				{
					for (int num2 = 0; num2 < base.cusClothesCmp[kindNo].rendNormal01.Length; num2++)
					{
						base.cusClothesCmp[kindNo].rendNormal01[num2].material.renderQueue = 3030;
					}
				}
				nowCoordinate.clothes.parts[kindNo].id = listInfoBase.Id;
				CreateReferenceInfo(8uL, base.objClothes[kindNo]);
				AddClothesStateKind(kindNo, listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
			}
		}
		if ((bool)base.objClothes[kindNo])
		{
			InitBaseCustomTextureClothes(main: true, kindNo);
			if (base.loadWithDefaultColorAndPtn)
			{
				SetClothesDefaultColor(kindNo);
				for (int num3 = 0; num3 < 4; num3++)
				{
					nowCoordinate.clothes.parts[kindNo].colorInfo[num3].pattern = 0;
				}
			}
			ChangeCustomClothes(main: true, kindNo, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true, updateTex04: false);
			for (int num4 = 0; num4 < 2; num4++)
			{
				ChangeCustomEmblem(kindNo, num4);
			}
			if (base.releaseCustomInputTexture)
			{
				ReleaseBaseCustomTextureClothes(main: true, kindNo, createTex: false);
			}
		}
		if (base.hiPoly)
		{
			UpdateSiru(forceChange: true);
		}
	}

	public void ChangeClothesGloves(int id, bool forceChange = false)
	{
		ChangeClothesGlovesNoAsync(id, forceChange);
	}

	public IEnumerator ChangeClothesGlovesAsync(int id, bool forceChange = false, bool asyncFlags = true)
	{
		bool flag = true;
		bool flag2 = true;
		int kindNo = 4;
		string objName = "ct_gloves";
		int num = ((base.infoClothes[kindNo] == null) ? (-1) : base.infoClothes[kindNo].Id);
		if (!forceChange && null != base.objClothes[kindNo] && id == num)
		{
			flag = false;
			flag2 = false;
		}
		ReleaseBaseCustomTextureClothes(main: true, kindNo);
		if (flag2 && (bool)base.objClothes[kindNo])
		{
			SafeDestroy(base.objClothes[kindNo]);
			base.objClothes[kindNo] = null;
			base.infoClothes[kindNo] = null;
			base.cusClothesCmp[kindNo] = null;
			RemoveClothesStateKind(kindNo);
		}
		if (flag)
		{
			if (asyncFlags)
			{
				yield return null;
			}
			if (asyncFlags)
			{
				IEnumerator routine = LoadCharaFbxDataAsync(delegate(GameObject o)
				{
					base.objClothes[kindNo] = o;
				}, delegate(ListInfoBase lib)
				{
					base.infoClothes[kindNo] = lib;
				}, base.hiPoly, 109, id, objName, copyDynamicBone: false, 1, base.objTop.transform, 0);
				yield return StartCoroutine(routine);
			}
			else
			{
				base.objClothes[kindNo] = LoadCharaFbxData(delegate(ListInfoBase lib)
				{
					base.infoClothes[kindNo] = lib;
				}, base.hiPoly, 109, id, objName, copyDynamicBone: false, 1, base.objTop.transform, 0);
			}
			if (asyncFlags)
			{
				yield return null;
			}
			if ((bool)base.objClothes[kindNo])
			{
				ListInfoBase listInfoBase = base.infoClothes[kindNo];
				base.cusClothesCmp[kindNo] = base.objClothes[kindNo].GetComponent<ChaClothesComponent>();
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy")
				{
					_ = null == base.cusClothesCmp[kindNo];
				}
				nowCoordinate.clothes.parts[kindNo].id = listInfoBase.Id;
				AddClothesStateKind(kindNo, listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
			}
		}
		if (!base.objClothes[kindNo])
		{
			yield break;
		}
		InitBaseCustomTextureClothes(main: true, kindNo);
		if (base.loadWithDefaultColorAndPtn)
		{
			SetClothesDefaultColor(kindNo);
			for (int num2 = 0; num2 < 4; num2++)
			{
				nowCoordinate.clothes.parts[kindNo].colorInfo[num2].pattern = 0;
			}
		}
		ChangeCustomClothes(main: true, kindNo, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true, updateTex04: false);
		for (int num3 = 0; num3 < 2; num3++)
		{
			ChangeCustomEmblem(kindNo, num3);
		}
		if (base.releaseCustomInputTexture)
		{
			ReleaseBaseCustomTextureClothes(main: true, kindNo, createTex: false);
		}
	}

	public void ChangeClothesPanst(int id, bool forceChange = false, bool update = true)
	{
		ChangeClothesPanstNoAsync(id, forceChange, update);
	}

	public IEnumerator ChangeClothesPanstAsync(int id, bool forceChange = false, bool asyncFlags = true)
	{
		bool flag = true;
		bool flag2 = true;
		int kindNo = 5;
		string objName = "ct_panst";
		int num = ((base.infoClothes[kindNo] == null) ? (-1) : base.infoClothes[kindNo].Id);
		if (!forceChange && null != base.objClothes[kindNo] && id == num)
		{
			flag = false;
			flag2 = false;
		}
		ReleaseBaseCustomTextureClothes(main: true, kindNo);
		if (flag2 && (bool)base.objClothes[kindNo])
		{
			SafeDestroy(base.objClothes[kindNo]);
			base.objClothes[kindNo] = null;
			base.infoClothes[kindNo] = null;
			base.cusClothesCmp[kindNo] = null;
			ReleaseRefObject(10uL);
			RemoveClothesStateKind(kindNo);
		}
		if (flag)
		{
			if (asyncFlags)
			{
				yield return null;
			}
			if (asyncFlags)
			{
				IEnumerator routine = LoadCharaFbxDataAsync(delegate(GameObject o)
				{
					base.objClothes[kindNo] = o;
				}, delegate(ListInfoBase lib)
				{
					base.infoClothes[kindNo] = lib;
				}, base.hiPoly, 110, id, objName, copyDynamicBone: false, 1, base.objTop.transform, 0);
				yield return StartCoroutine(routine);
			}
			else
			{
				base.objClothes[kindNo] = LoadCharaFbxData(delegate(ListInfoBase lib)
				{
					base.infoClothes[kindNo] = lib;
				}, base.hiPoly, 110, id, objName, copyDynamicBone: false, 1, base.objTop.transform, 0);
			}
			if (asyncFlags)
			{
				yield return null;
			}
			if ((bool)base.objClothes[kindNo])
			{
				ListInfoBase listInfoBase = base.infoClothes[kindNo];
				base.cusClothesCmp[kindNo] = base.objClothes[kindNo].GetComponent<ChaClothesComponent>();
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy")
				{
					_ = null == base.cusClothesCmp[kindNo];
				}
				if (listInfoBase.Id == 11)
				{
					for (int num2 = 0; num2 < base.cusClothesCmp[kindNo].rendNormal01.Length; num2++)
					{
						base.cusClothesCmp[kindNo].rendNormal01[num2].material.renderQueue = 3040;
					}
				}
				nowCoordinate.clothes.parts[kindNo].id = listInfoBase.Id;
				CreateReferenceInfo(10uL, base.objClothes[kindNo]);
				AddClothesStateKind(kindNo, listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
			}
		}
		if ((bool)base.objClothes[kindNo])
		{
			InitBaseCustomTextureClothes(main: true, kindNo);
			if (base.loadWithDefaultColorAndPtn)
			{
				SetClothesDefaultColor(kindNo);
				for (int num3 = 0; num3 < 4; num3++)
				{
					nowCoordinate.clothes.parts[kindNo].colorInfo[num3].pattern = 0;
				}
			}
			ChangeCustomClothes(main: true, kindNo, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true, updateTex04: false);
			for (int num4 = 0; num4 < 2; num4++)
			{
				ChangeCustomEmblem(kindNo, num4);
			}
			if (base.releaseCustomInputTexture)
			{
				ReleaseBaseCustomTextureClothes(main: true, kindNo, createTex: false);
			}
		}
		if (base.hiPoly)
		{
			UpdateSiru(forceChange: true);
		}
	}

	public void ChangeClothesSocks(int id, bool forceChange = false)
	{
		ChangeClothesSocksNoAsync(id, forceChange);
	}

	public IEnumerator ChangeClothesSocksAsync(int id, bool forceChange = false, bool asyncFlags = true)
	{
		bool flag = true;
		bool flag2 = true;
		int kindNo = 6;
		string objName = "ct_socks";
		int num = ((base.infoClothes[kindNo] == null) ? (-1) : base.infoClothes[kindNo].Id);
		if (!forceChange && null != base.objClothes[kindNo] && id == num)
		{
			flag = false;
			flag2 = false;
		}
		ReleaseBaseCustomTextureClothes(main: true, kindNo);
		if (flag2 && (bool)base.objClothes[kindNo])
		{
			SafeDestroy(base.objClothes[kindNo]);
			base.objClothes[kindNo] = null;
			base.infoClothes[kindNo] = null;
			base.cusClothesCmp[kindNo] = null;
			RemoveClothesStateKind(kindNo);
		}
		if (flag)
		{
			if (asyncFlags)
			{
				yield return null;
			}
			if (asyncFlags)
			{
				IEnumerator routine = LoadCharaFbxDataAsync(delegate(GameObject o)
				{
					base.objClothes[kindNo] = o;
				}, delegate(ListInfoBase lib)
				{
					base.infoClothes[kindNo] = lib;
				}, base.hiPoly, 111, id, objName, copyDynamicBone: false, 1, base.objTop.transform, 0);
				yield return StartCoroutine(routine);
			}
			else
			{
				base.objClothes[kindNo] = LoadCharaFbxData(delegate(ListInfoBase lib)
				{
					base.infoClothes[kindNo] = lib;
				}, base.hiPoly, 111, id, objName, copyDynamicBone: false, 1, base.objTop.transform, 0);
			}
			if (asyncFlags)
			{
				yield return null;
			}
			if ((bool)base.objClothes[kindNo])
			{
				ListInfoBase listInfoBase = base.infoClothes[kindNo];
				base.cusClothesCmp[kindNo] = base.objClothes[kindNo].GetComponent<ChaClothesComponent>();
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy")
				{
					_ = null == base.cusClothesCmp[kindNo];
				}
				if (listInfoBase.Id == 21)
				{
					for (int num2 = 0; num2 < base.cusClothesCmp[kindNo].rendNormal01.Length; num2++)
					{
						base.cusClothesCmp[kindNo].rendNormal01[num2].material.renderQueue = 3050;
					}
				}
				nowCoordinate.clothes.parts[kindNo].id = listInfoBase.Id;
				AddClothesStateKind(kindNo, listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
			}
		}
		if (!base.objClothes[kindNo])
		{
			yield break;
		}
		InitBaseCustomTextureClothes(main: true, kindNo);
		if (base.loadWithDefaultColorAndPtn)
		{
			SetClothesDefaultColor(kindNo);
			for (int num3 = 0; num3 < 4; num3++)
			{
				nowCoordinate.clothes.parts[kindNo].colorInfo[num3].pattern = 0;
			}
		}
		ChangeCustomClothes(main: true, kindNo, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true, updateTex04: false);
		for (int num4 = 0; num4 < 2; num4++)
		{
			ChangeCustomEmblem(kindNo, num4);
		}
		if (base.releaseCustomInputTexture)
		{
			ReleaseBaseCustomTextureClothes(main: true, kindNo, createTex: false);
		}
	}

	public void ChangeClothesShoes(int type, int id, bool forceChange = false)
	{
		ChangeClothesShoesNoAsync(type, id, forceChange);
	}

	public IEnumerator ChangeClothesShoesAsync(int type, int id, bool forceChange = false, bool asyncFlags = true)
	{
		bool flag = true;
		bool flag2 = true;
		int kindNo = 0;
		string objName;
		int defID;
		if (type == 0)
		{
			kindNo = 7;
			objName = "ct_shoes_inner";
			defID = 0;
		}
		else
		{
			kindNo = 8;
			objName = "ct_shoes_outer";
			defID = 0;
		}
		int num = ((base.infoClothes[kindNo] == null) ? (-1) : base.infoClothes[kindNo].Id);
		if (!forceChange && null != base.objClothes[kindNo] && id == num)
		{
			flag = false;
			flag2 = false;
		}
		ReleaseBaseCustomTextureClothes(main: true, kindNo);
		if (flag2 && (bool)base.objClothes[kindNo])
		{
			SafeDestroy(base.objClothes[kindNo]);
			base.objClothes[kindNo] = null;
			base.infoClothes[kindNo] = null;
			base.cusClothesCmp[kindNo] = null;
			RemoveClothesStateKind(kindNo);
		}
		if (flag)
		{
			if (asyncFlags)
			{
				yield return null;
			}
			if (asyncFlags)
			{
				IEnumerator routine = LoadCharaFbxDataAsync(delegate(GameObject o)
				{
					base.objClothes[kindNo] = o;
				}, delegate(ListInfoBase lib)
				{
					base.infoClothes[kindNo] = lib;
				}, base.hiPoly, (base.sex == 0 && 1 == base.exType) ? 504 : 112, id, objName, copyDynamicBone: false, 1, base.objTop.transform, defID);
				yield return StartCoroutine(routine);
			}
			else
			{
				base.objClothes[kindNo] = LoadCharaFbxData(delegate(ListInfoBase lib)
				{
					base.infoClothes[kindNo] = lib;
				}, base.hiPoly, (base.sex == 0 && 1 == base.exType) ? 504 : 112, id, objName, copyDynamicBone: false, 1, base.objTop.transform, defID);
			}
			if (asyncFlags)
			{
				yield return null;
			}
			if ((bool)base.objClothes[kindNo])
			{
				ListInfoBase listInfoBase = base.infoClothes[kindNo];
				base.cusClothesCmp[kindNo] = base.objClothes[kindNo].GetComponent<ChaClothesComponent>();
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy")
				{
					_ = null == base.cusClothesCmp[kindNo];
				}
				nowCoordinate.clothes.parts[kindNo].id = listInfoBase.Id;
				AddClothesStateKind(kindNo, listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
			}
		}
		if (!base.objClothes[kindNo])
		{
			yield break;
		}
		InitBaseCustomTextureClothes(main: true, kindNo);
		if (base.loadWithDefaultColorAndPtn)
		{
			SetClothesDefaultColor(kindNo);
			for (int num2 = 0; num2 < 4; num2++)
			{
				nowCoordinate.clothes.parts[kindNo].colorInfo[num2].pattern = 0;
			}
		}
		ChangeCustomClothes(main: true, kindNo, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true, updateTex04: false);
		for (int num3 = 0; num3 < 2; num3++)
		{
			ChangeCustomEmblem(kindNo, num3);
		}
		if (base.releaseCustomInputTexture)
		{
			ReleaseBaseCustomTextureClothes(main: true, kindNo, createTex: false);
		}
	}

	public void ChangeAccessory(bool forceChange = false, bool update = true)
	{
		for (int i = 0; i < 20; i++)
		{
			ChangeAccessoryNoAsync(i, nowCoordinate.accessory.parts[i].type, nowCoordinate.accessory.parts[i].id, nowCoordinate.accessory.parts[i].parentKey, forceChange, update);
		}
	}

	public void ChangeAccessory(int slotNo, int type, int id, string parentKey, bool forceChange = false)
	{
		StartCoroutine(ChangeAccessoryAsync(slotNo, type, id, parentKey, forceChange, asyncFlags: false));
	}

	public IEnumerator ChangeAccessoryAsync(bool forceChange = false)
	{
		int i = 0;
		while (i < 20)
		{
			yield return StartCoroutine(ChangeAccessoryAsync(i, nowCoordinate.accessory.parts[i].type, nowCoordinate.accessory.parts[i].id, nowCoordinate.accessory.parts[i].parentKey, forceChange));
			int num = i + 1;
			i = num;
		}
	}

	public IEnumerator ChangeAccessoryAsync(int slotNo, int type, int id, string parentKey, bool forceChange = false, bool asyncFlags = true)
	{
		if (!MathfEx.RangeEqualOn(0, slotNo, 19))
		{
			yield break;
		}
		ListInfoBase lib = null;
		bool flag = true;
		bool flag2 = true;
		if ((!Game.isAddH) ? (120 == type || !MathfEx.RangeEqualOn(121, type, 129)) : (120 == type || !MathfEx.RangeEqualOn(121, type, 130)))
		{
			flag2 = true;
			flag = false;
		}
		else
		{
			if (-1 == id)
			{
				flag2 = false;
				flag = false;
			}
			int num = ((base.infoAccessory[slotNo] == null) ? (-1) : base.infoAccessory[slotNo].Category);
			int num2 = ((base.infoAccessory[slotNo] == null) ? (-1) : base.infoAccessory[slotNo].Id);
			if (!forceChange && null != base.objAccessory[slotNo] && type == num && id == num2)
			{
				flag = false;
				flag2 = false;
			}
			if (-1 != id)
			{
				if (!base.lstCtrl.ContainsCategoryInfo((ChaListDefine.CategoryNo)type))
				{
					flag2 = true;
					flag = false;
				}
				else
				{
					lib = base.lstCtrl.GetInfo((ChaListDefine.CategoryNo)type, id);
					if (lib == null)
					{
						flag2 = true;
						flag = false;
					}
					else if (!base.hiPoly)
					{
						bool flag3 = true;
						if (123 == type && 1 == lib.Kind)
						{
							flag3 = false;
						}
						if (122 == type && 1 == lib.GetInfoInt(ChaListDefine.KeyType.HideHair))
						{
							flag3 = false;
						}
						if (Manager.Config.EtcData.loadHeadAccessory && 122 == type && 1 == lib.Kind)
						{
							flag3 = false;
						}
						if (Manager.Config.EtcData.loadAllAccessory)
						{
							flag3 = false;
						}
						if (flag3)
						{
							flag2 = true;
							flag = false;
						}
					}
				}
			}
		}
		if (flag2)
		{
			if (!flag)
			{
				nowCoordinate.accessory.parts[slotNo].MemberInit();
				nowCoordinate.accessory.parts[slotNo].type = 120;
			}
			if ((bool)base.objAccessory[slotNo])
			{
				SafeDestroy(base.objAccessory[slotNo]);
				base.objAccessory[slotNo] = null;
				base.infoAccessory[slotNo] = null;
				base.cusAcsCmp[slotNo] = null;
				for (int i = 0; i < 2; i++)
				{
					base.objAcsMove[slotNo, i] = null;
				}
			}
		}
		if (flag)
		{
			if (asyncFlags)
			{
				yield return null;
			}
			byte copyWeights = 0;
			Transform trfParent = null;
			if ("null" == lib.GetInfo(ChaListDefine.KeyType.Parent))
			{
				copyWeights = 2;
				trfParent = base.objTop.transform;
			}
			if (asyncFlags)
			{
				IEnumerator routine = LoadCharaFbxDataAsync(delegate(GameObject o)
				{
					base.objAccessory[slotNo] = o;
				}, delegate(ListInfoBase li)
				{
					base.infoAccessory[slotNo] = li;
				}, _hiPoly: true, type, id, "ca_slot" + slotNo.ToString("00"), copyDynamicBone: false, copyWeights, trfParent, -1);
				yield return StartCoroutine(routine);
			}
			else
			{
				base.objAccessory[slotNo] = LoadCharaFbxData(delegate(ListInfoBase l)
				{
					base.infoAccessory[slotNo] = l;
				}, _hiPoly: true, type, id, "ca_slot" + slotNo.ToString("00"), copyDynamicBone: false, copyWeights, trfParent, -1);
			}
			if ((bool)base.objAccessory[slotNo])
			{
				lib = base.infoAccessory[slotNo];
				base.cusAcsCmp[slotNo] = base.objAccessory[slotNo].GetComponent<ChaAccessoryComponent>();
				if (lib.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy")
				{
					_ = null == base.cusAcsCmp[slotNo];
				}
				nowCoordinate.accessory.parts[slotNo].type = type;
				nowCoordinate.accessory.parts[slotNo].id = lib.Id;
				base.objAcsMove[slotNo, 0] = base.objAccessory[slotNo].transform.FindLoop("N_move");
				base.objAcsMove[slotNo, 1] = base.objAccessory[slotNo].transform.FindLoop("N_move2");
			}
		}
		if ((bool)base.objAccessory[slotNo])
		{
			if (base.loadWithDefaultColorAndPtn)
			{
				SetAccessoryDefaultColor(slotNo);
			}
			ChangeAccessoryColor(slotNo);
			if ("" == parentKey)
			{
				parentKey = lib.GetInfo(ChaListDefine.KeyType.Parent);
			}
			ChangeAccessoryParent(slotNo, parentKey);
			UpdateAccessoryMoveFromInfo(slotNo);
			nowCoordinate.accessory.parts[slotNo].partsOfHead = ChaAccessoryDefine.CheckPartsOfHead(parentKey);
			if (!base.hiPoly && !Manager.Config.EtcData.loadAllAccessory)
			{
				DynamicBone[] componentsInChildren = base.objAccessory[slotNo].GetComponentsInChildren<DynamicBone>(includeInactive: true);
				for (int num3 = 0; num3 < componentsInChildren.Length; num3++)
				{
					componentsInChildren[num3].enabled = false;
				}
			}
			ChangeShakeAccessory(slotNo);
		}
		SetHideHairAccessory();
	}

	private GameObject LoadCharaFbxData(Action<ListInfoBase> actListInfo, bool _hiPoly, int category, int id, string createName, bool copyDynamicBone, byte copyWeights, Transform trfParent, int defaultId, bool worldPositionStays = false)
	{
		GameObject actObj = null;
		LoadCharaFbxDataNoAsync(delegate(GameObject o)
		{
			actObj = o;
		}, actListInfo, _hiPoly, category, id, createName, copyDynamicBone, copyWeights, trfParent, defaultId, worldPositionStays);
		return actObj;
	}

	private IEnumerator LoadCharaFbxDataAsync(Action<GameObject> actObj, Action<ListInfoBase> actListInfo, bool _hiPoly, int category, int id, string createName, bool copyDynamicBone, byte copyWeights, Transform trfParent, int defaultId, bool AsyncFlags = true, bool worldPositionStays = false)
	{
		if (base.lstCtrl.CountInfo((ChaListDefine.CategoryNo)category) == 0)
		{
			actObj(null);
			yield break;
		}
		ListInfoBase lib = base.lstCtrl.GetInfo((ChaListDefine.CategoryNo)category, id);
		if (lib == null)
		{
			if (-1 == defaultId)
			{
				actObj(null);
				yield break;
			}
			if (id != defaultId)
			{
				lib = base.lstCtrl.GetInfo((ChaListDefine.CategoryNo)category, defaultId);
			}
			if (lib == null)
			{
				lib = base.lstCtrl.GetInfo((ChaListDefine.CategoryNo)category, 0);
				if (lib == null)
				{
					actObj(null);
					yield break;
				}
			}
		}
		else if (category == 105 || category == 107)
		{
			int infoInt = lib.GetInfoInt(ChaListDefine.KeyType.Sex);
			bool flag = false;
			if (base.sex == 0 && 3 == infoInt)
			{
				flag = true;
			}
			else if (1 == base.sex && 2 == infoInt)
			{
				flag = true;
			}
			if (flag)
			{
				if (id != defaultId)
				{
					lib = base.lstCtrl.GetInfo((ChaListDefine.CategoryNo)category, defaultId);
				}
				if (lib == null)
				{
					lib = base.lstCtrl.GetInfo((ChaListDefine.CategoryNo)category, 0);
					if (lib == null)
					{
						actObj(null);
						yield break;
					}
				}
			}
		}
		if (category == 501)
		{
			lib = base.lstCtrl.GetInfo(ChaListDefine.CategoryNo.ex_bo_head, 0);
			if (lib == null)
			{
				actObj(null);
				yield break;
			}
		}
		string text = lib.GetInfo(ChaListDefine.KeyType.MainData);
		if ("" == text)
		{
			yield break;
		}
		if (!_hiPoly)
		{
			text += "_low";
		}
		string manifestName = lib.GetInfo(ChaListDefine.KeyType.MainManifest);
		string assetBundleName = lib.GetInfo(ChaListDefine.KeyType.MainAB);
		GameObject newObj = null;
		if (AsyncFlags)
		{
			yield return StartCoroutine(Load_Coroutine(assetBundleName, text, delegate(GameObject x)
			{
				newObj = x;
			}, xxx: true, manifestName));
		}
		else
		{
			newObj = CommonLib.LoadAsset<GameObject>(assetBundleName, text, clone: true, manifestName);
		}
		Character.AddLoadAssetBundle(assetBundleName, manifestName);
		if (null == newObj)
		{
			actObj(null);
			yield break;
		}
		newObj.name = createName;
		if ((bool)trfParent)
		{
			newObj.transform.SetParent(trfParent, worldPositionStays);
		}
		DynamicBoneCollider[] componentsInChildren = base.objBodyBone.GetComponentsInChildren<DynamicBoneCollider>(includeInactive: true);
		Dictionary<string, GameObject> dictBone = aaWeightsBody.dictBone;
		DynamicBone[] componentsInChildren2 = newObj.GetComponentsInChildren<DynamicBone>(includeInactive: true);
		foreach (DynamicBone dynamicBone in componentsInChildren2)
		{
			if (copyDynamicBone)
			{
				if ((bool)dynamicBone.m_Root)
				{
					foreach (KeyValuePair<string, GameObject> item in dictBone)
					{
						if (!(item.Key != dynamicBone.m_Root.name))
						{
							dynamicBone.m_Root = item.Value.transform;
							break;
						}
					}
				}
				if (dynamicBone.m_Exclusions != null && dynamicBone.m_Exclusions.Count != 0)
				{
					for (int num2 = 0; num2 < dynamicBone.m_Exclusions.Count; num2++)
					{
						if (null == dynamicBone.m_Exclusions[num2])
						{
							continue;
						}
						foreach (KeyValuePair<string, GameObject> item2 in dictBone)
						{
							if (!(item2.Key != dynamicBone.m_Exclusions[num2].name))
							{
								dynamicBone.m_Exclusions[num2] = item2.Value.transform;
								break;
							}
						}
					}
				}
				if (dynamicBone.m_notRolls != null && dynamicBone.m_notRolls.Count != 0)
				{
					for (int num3 = 0; num3 < dynamicBone.m_notRolls.Count; num3++)
					{
						if (null == dynamicBone.m_notRolls[num3])
						{
							continue;
						}
						foreach (KeyValuePair<string, GameObject> item3 in dictBone)
						{
							if (!(item3.Key != dynamicBone.m_notRolls[num3].name))
							{
								dynamicBone.m_notRolls[num3] = item3.Value.transform;
								break;
							}
						}
					}
				}
			}
			if (dynamicBone.m_Colliders != null)
			{
				dynamicBone.m_Colliders.Clear();
				for (int num4 = 0; num4 < componentsInChildren.Length; num4++)
				{
					dynamicBone.m_Colliders.Add(componentsInChildren[num4]);
				}
			}
		}
		GameObject referenceInfo = GetReferenceInfo(RefObjKey.A_ROOTBONE);
		Transform rootBone = (referenceInfo ? referenceInfo.transform : null);
		if (1 == copyWeights)
		{
			aaWeightsBody.AssignedWeightsAndSetBounds(newObj, "cf_j_root", bounds, rootBone);
		}
		else if (2 == copyWeights)
		{
			aaWeightsHead.AssignedWeightsAndSetBounds(newObj, "cf_J_N_FaceRoot", bounds, rootBone);
		}
		newObj.AddComponent<ListInfoComponent>().data = lib;
		actObj(newObj);
		actListInfo(lib);
	}

	protected bool InitBaseCustomTextureBody()
	{
		if (base.customTexCtrlBody != null)
		{
			base.customTexCtrlBody.Release();
			base.customTexCtrlBody = null;
		}
		string text = "abdata";
		string text2 = "chara/mm_base.unity3d";
		string drawMatName = ((base.sex == 0) ? "cm_m_body" : "cf_m_body");
		string createMatName = "cf_m_body_create";
		if (base.sex == 0 && 1 == base.exType)
		{
			text = "add54";
			text2 = "chara/54/chara_mat_00.unity3d";
			drawMatName = "cm_m_ex_body_00";
			createMatName = "cm_m_ex_body_00_create";
		}
		int num = (base.hiPoly ? 2048 : 512);
		base.customTexCtrlBody = new CustomTextureControl(base.objRoot.transform);
		base.customTexCtrlBody.Initialize(text2, drawMatName, text, text2, createMatName, text, num, num);
		return true;
	}

	protected bool InitBaseCustomTextureFace(string drawAssetBundleName, string drawAssetName, string drawManifest)
	{
		if (base.customTexCtrlFace != null)
		{
			base.customTexCtrlFace.Release();
			base.customTexCtrlFace = null;
		}
		string createMatManifest = "abdata";
		string createMatABName = "chara/mm_base.unity3d";
		string createMatName = "cf_m_face_create";
		if (base.sex == 0 && 1 == base.exType)
		{
			drawManifest = "add54";
			createMatManifest = "add54";
			createMatABName = "chara/54/chara_mat_00.unity3d";
			drawAssetName = "cm_m_ex_face_00";
			createMatName = "cm_m_ex_face_00_create";
		}
		int num = (base.hiPoly ? 1024 : 512);
		base.customTexCtrlFace = new CustomTextureControl(base.objRoot.transform);
		base.customTexCtrlFace.Initialize(drawAssetBundleName, drawAssetName, drawManifest, createMatABName, createMatName, createMatManifest, num, num);
		return true;
	}

	protected bool InitBaseCustomTextureEtc()
	{
		string text = "";
		string text2 = "";
		int num = (base.hiPoly ? 512 : 256);
		if (base.ctCreateEyeW != null)
		{
			base.ctCreateEyeW.Release();
			base.ctCreateEyeW = null;
		}
		text = "chara/mm_base.unity3d";
		text2 = "cf_m_eyewhite_create";
		base.ctCreateEyeW = new CustomTextureCreate(base.objRoot.transform);
		base.ctCreateEyeW.Initialize(text, text2, "", num, num);
		if (base.ctCreateEyeL != null)
		{
			base.ctCreateEyeL.Release();
			base.ctCreateEyeL = null;
		}
		text = "chara/mm_base.unity3d";
		text2 = "cf_m_eye_create";
		base.ctCreateEyeL = new CustomTextureCreate(base.objRoot.transform);
		base.ctCreateEyeL.Initialize(text, text2, "", num, num);
		if (base.ctCreateEyeR != null)
		{
			base.ctCreateEyeR.Release();
			base.ctCreateEyeR = null;
		}
		text = "chara/mm_base.unity3d";
		text2 = "cf_m_eye_create";
		base.ctCreateEyeR = new CustomTextureCreate(base.objRoot.transform);
		base.ctCreateEyeR.Initialize(text, text2, "", num, num);
		return true;
	}

	protected void ReleaseBaseCustomTextureClothes(bool main, int parts, bool createTex = true)
	{
		if (main)
		{
			for (int i = 0; i < 3; i++)
			{
				if (base.ctCreateClothes[parts, i] != null)
				{
					if (createTex)
					{
						base.ctCreateClothes[parts, i].Release();
					}
					else
					{
						base.ctCreateClothes[parts, i].ReleaseCreateMaterial();
					}
					base.ctCreateClothes[parts, i] = null;
				}
			}
			return;
		}
		for (int j = 0; j < 3; j++)
		{
			if (base.ctCreateClothesSub[parts, j] != null)
			{
				if (createTex)
				{
					base.ctCreateClothesSub[parts, j].Release();
				}
				else
				{
					base.ctCreateClothesSub[parts, j].ReleaseCreateMaterial();
				}
				base.ctCreateClothesSub[parts, j] = null;
			}
		}
	}

	protected bool InitBaseCustomTextureClothes(bool main, int parts)
	{
		string text = "";
		string text2 = "";
		string text3 = "";
		string text4 = "";
		string text5 = "";
		string text6 = "";
		string text7 = "";
		string text8 = "";
		string text9 = "";
		string text10 = "";
		string text11 = "";
		string text12 = "";
		string text13 = "";
		string text14 = "";
		string text15 = "";
		string text16 = "";
		string text17 = "";
		string text18 = "";
		if (main && base.infoClothes == null)
		{
			return false;
		}
		if (!main && base.infoParts == null)
		{
			return false;
		}
		ListInfoBase listInfoBase = (main ? base.infoClothes[parts] : base.infoParts[parts]);
		text = listInfoBase.GetInfo(ChaListDefine.KeyType.MainManifest);
		text2 = listInfoBase.GetInfo(ChaListDefine.KeyType.MainTexAB);
		if ("0" == text2)
		{
			text2 = listInfoBase.GetInfo(ChaListDefine.KeyType.MainAB);
		}
		text3 = listInfoBase.GetInfo(ChaListDefine.KeyType.MainTex);
		text4 = listInfoBase.GetInfo(ChaListDefine.KeyType.MainManifest);
		text5 = listInfoBase.GetInfo(ChaListDefine.KeyType.MainTex02AB);
		if ("0" == text5)
		{
			text5 = listInfoBase.GetInfo(ChaListDefine.KeyType.MainAB);
		}
		text6 = listInfoBase.GetInfo(ChaListDefine.KeyType.MainTex02);
		text7 = listInfoBase.GetInfo(ChaListDefine.KeyType.MainManifest);
		text8 = listInfoBase.GetInfo(ChaListDefine.KeyType.MainTex03AB);
		if ("0" == text8)
		{
			text8 = listInfoBase.GetInfo(ChaListDefine.KeyType.MainAB);
		}
		text9 = listInfoBase.GetInfo(ChaListDefine.KeyType.MainTex03);
		text10 = listInfoBase.GetInfo(ChaListDefine.KeyType.MainManifest);
		text11 = listInfoBase.GetInfo(ChaListDefine.KeyType.ColorMaskAB);
		if ("0" == text11)
		{
			text11 = listInfoBase.GetInfo(ChaListDefine.KeyType.MainAB);
		}
		text12 = listInfoBase.GetInfo(ChaListDefine.KeyType.ColorMaskTex);
		text13 = listInfoBase.GetInfo(ChaListDefine.KeyType.MainManifest);
		text14 = listInfoBase.GetInfo(ChaListDefine.KeyType.ColorMask02AB);
		if ("0" == text14)
		{
			text14 = listInfoBase.GetInfo(ChaListDefine.KeyType.MainAB);
		}
		text15 = listInfoBase.GetInfo(ChaListDefine.KeyType.ColorMask02Tex);
		text16 = listInfoBase.GetInfo(ChaListDefine.KeyType.MainManifest);
		text17 = listInfoBase.GetInfo(ChaListDefine.KeyType.ColorMask03AB);
		if ("0" == text17)
		{
			text17 = listInfoBase.GetInfo(ChaListDefine.KeyType.MainAB);
		}
		text18 = listInfoBase.GetInfo(ChaListDefine.KeyType.ColorMask03Tex);
		Texture2D texture2D = null;
		if ("0" == text2 || "0" == text3)
		{
			return false;
		}
		if (!base.hiPoly)
		{
			text3 += "_low";
		}
		texture2D = CommonLib.LoadAsset<Texture2D>(text2, text3, clone: false, text);
		if (null == texture2D)
		{
			return false;
		}
		Texture2D texture2D2 = null;
		if ("0" == text11 || "0" == text12)
		{
			Resources.UnloadAsset(texture2D);
			return false;
		}
		if (!base.hiPoly)
		{
			text12 += "_low";
		}
		texture2D2 = CommonLib.LoadAsset<Texture2D>(text11, text12, clone: false, text10);
		if (null == texture2D2)
		{
			Resources.UnloadAsset(texture2D);
			return false;
		}
		Texture2D texture2D3 = null;
		if ("0" != text5 && "0" != text6)
		{
			if (!base.hiPoly)
			{
				text6 += "_low";
			}
			texture2D3 = CommonLib.LoadAsset<Texture2D>(text5, text6, clone: false, text4);
		}
		Texture2D texture2D4 = null;
		if ("0" != text14 && "0" != text15)
		{
			if (!base.hiPoly)
			{
				text15 += "_low";
			}
			texture2D4 = CommonLib.LoadAsset<Texture2D>(text14, text15, clone: false, text13);
		}
		Texture2D texture2D5 = null;
		if ("0" != text8 && "0" != text9)
		{
			texture2D5 = CommonLib.LoadAsset<Texture2D>(text8, text9, clone: false, text7);
		}
		Texture2D texture2D6 = null;
		if ("0" != text17 && "0" != text18)
		{
			texture2D6 = CommonLib.LoadAsset<Texture2D>(text17, text18, clone: false, text16);
		}
		string createMatABName = "chara/mm_base.unity3d";
		string createMatName = "cf_m_clothesN_create";
		for (int i = 0; i < 3; i++)
		{
			CustomTextureCreate customTextureCreate = null;
			Texture2D texture2D7 = null;
			Texture2D texture2D8 = null;
			if (i == 0)
			{
				texture2D7 = texture2D;
				texture2D8 = texture2D2;
			}
			else if (1 == i)
			{
				texture2D7 = texture2D3;
				texture2D8 = texture2D4;
			}
			else
			{
				texture2D7 = texture2D5;
				texture2D8 = texture2D6;
			}
			if (null != texture2D7)
			{
				customTextureCreate = new CustomTextureCreate(base.objRoot.transform);
				int width = texture2D7.width;
				int height = texture2D7.height;
				customTextureCreate.Initialize(createMatABName, createMatName, "", width, height);
				customTextureCreate.SetMainTexture(texture2D7);
				customTextureCreate.SetTexture(ChaShader._ColorMask, texture2D8);
			}
			if (main)
			{
				base.ctCreateClothes[parts, i] = customTextureCreate;
			}
			else
			{
				base.ctCreateClothesSub[parts, i] = customTextureCreate;
			}
		}
		return true;
	}

	public bool LoadGagMaterial()
	{
		string assetBundleName = "chara/mm_base.unity3d";
		base.matGag[0] = CommonLib.LoadAsset<Material>(assetBundleName, "cf_m_gageye_00", clone: true);
		base.matGag[1] = CommonLib.LoadAsset<Material>(assetBundleName, "cf_m_gageye_01", clone: true);
		base.matGag[2] = CommonLib.LoadAsset<Material>(assetBundleName, "cf_m_gageye_02", clone: true);
		Character.AddLoadAssetBundle(assetBundleName, "");
		return true;
	}

	public bool LoadAlphaMaskTexture(string assetBundleName, string assetName, byte type)
	{
		if ("0" == assetBundleName || "0" == assetName)
		{
			return false;
		}
		if (!base.hiPoly)
		{
			assetName += "_low";
		}
		string manifestName = "abdata";
		if (base.exType == 1)
		{
			manifestName = "add54";
		}
		Texture texture = CommonLib.LoadAsset<Texture>(assetBundleName, assetName, clone: false, manifestName);
		if (null == texture)
		{
			return false;
		}
		Character.AddLoadAssetBundle(assetBundleName, manifestName);
		if (type == 0)
		{
			texBodyAlphaMask = texture;
		}
		else if (1 == type)
		{
			texBraAlphaMask = texture;
		}
		else if (2 == type)
		{
			texInnerAlphaMask = texture;
		}
		return true;
	}

	private void MozInit(params GameObject[] objMoz)
	{
		Texture[] array = new Texture[4];
		string assetBundleName = "chara/etc.unity3d";
		string mainManifestName = Character.mainManifestName;
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = CommonLib.LoadAsset<Texture>(assetBundleName, "cf_t_mnpb_00_" + i.ToString("00"), clone: false, mainManifestName);
		}
		Character.AddLoadAssetBundle(assetBundleName, mainManifestName);
		foreach (GameObject gameObject in objMoz)
		{
			if (!(null == gameObject))
			{
				gameObject.AddComponent<ChangePtnAnime>().Init(array);
			}
		}
	}

	public bool InitializeExpression(bool _enable = true)
	{
		base.expression = base.objRoot.AddComponent<ExpressionBone>();
		base.expression.trfBoneRoot = base.objRoot.transform;
		base.expression.Initialize();
		base.expression.enable = _enable;
		return true;
	}

	public void LoadHitObject(int siru = 0)
	{
		ReleaseHitObject();
		string assetBundleName = "chara/oo_base.unity3d";
		string assetName = ((siru == 0) ? "p_cf_body_00_hit" : ((base.sex != 0) ? "p_cf_body_00_hit_low" : "p_cm_body_00_hit_low"));
		base.objHitBody = CommonLib.LoadAsset<GameObject>(assetBundleName, assetName, clone: true);
		Character.AddLoadAssetBundle(assetBundleName, "");
		SkinnedCollisionHelper[] componentsInChildren2;
		if ((bool)base.objHitBody)
		{
			base.objHitBody.transform.SetParent(base.objTop.transform, worldPositionStays: false);
			aaWeightsBody.AssignedWeights(base.objHitBody, "cf_j_root");
			ObiCollider[] componentsInChildren = base.objHitBody.GetComponentsInChildren<ObiCollider>(includeInactive: true);
			if (!((IReadOnlyCollection<ObiCollider>)(object)componentsInChildren).IsNullOrEmpty())
			{
				ObiCollider[] array = componentsInChildren;
				for (int i = 0; i < array.Length; i++)
				{
					array[i].RemoveCollider();
				}
			}
			componentsInChildren2 = base.objHitBody.GetComponentsInChildren<SkinnedCollisionHelper>(includeInactive: true);
			for (int i = 0; i < componentsInChildren2.Length; i++)
			{
				componentsInChildren2[i].Init();
			}
			base.hitBodyCtrlCmp = base.objHitBody.GetComponent<ChaHitControlComponent>();
			if (base.sex == 0 && null != base.hitBodyCtrlCmp)
			{
				base.hitBodyCtrlCmp.ChangeBustBlendShapeValue(90f);
			}
		}
		if (siru != 0 || base.sex == 0 || base.infoHead == null)
		{
			return;
		}
		string manifestName = base.infoHead.dictInfo[32];
		assetBundleName = base.infoHead.dictInfo[30];
		assetName = base.infoHead.dictInfo[31] + "_hit";
		base.objHitHead = CommonLib.LoadAsset<GameObject>(assetBundleName, assetName, clone: true, manifestName);
		Character.AddLoadAssetBundle(assetBundleName, manifestName);
		if (!base.objHitHead)
		{
			return;
		}
		base.objHitHead.transform.SetParent(base.objTop.transform, worldPositionStays: false);
		aaWeightsHead.AssignedWeights(base.objHitHead, "cf_J_N_FaceRoot");
		ObiCollider[] componentsInChildren3 = base.objHitHead.GetComponentsInChildren<ObiCollider>(includeInactive: true);
		if (!((IReadOnlyCollection<ObiCollider>)(object)componentsInChildren3).IsNullOrEmpty())
		{
			ObiCollider[] array = componentsInChildren3;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].RemoveCollider();
			}
		}
		componentsInChildren2 = base.objHitHead.GetComponentsInChildren<SkinnedCollisionHelper>(includeInactive: true);
		for (int i = 0; i < componentsInChildren2.Length; i++)
		{
			componentsInChildren2[i].Init();
		}
	}

	public void ReleaseHitObject()
	{
		if ((bool)base.objHitBody)
		{
			SkinnedCollisionHelper[] componentsInChildren = base.objHitBody.GetComponentsInChildren<SkinnedCollisionHelper>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].Release();
			}
			SafeDestroy(base.objHitBody);
			base.objHitBody = null;
		}
		if ((bool)base.objHitHead)
		{
			SkinnedCollisionHelper[] componentsInChildren = base.objHitHead.GetComponentsInChildren<SkinnedCollisionHelper>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].Release();
			}
			SafeDestroy(base.objHitHead);
			base.objHitHead = null;
		}
		base.hitBodyCtrlCmp = null;
	}

	public static IEnumerator Load_Coroutine<T>(string assetBundleName, string assetName, Action<T> action, bool xxx, string mainManifestName) where T : UnityEngine.Object
	{
		T ret = null;
		UniTask.Void(async delegate
		{
			T asset = (await AssetBundleManager.LoadAssetAsync(assetBundleName, assetName, typeof(T), mainManifestName)).GetAsset<T>();
			ret = UnityEngine.Object.Instantiate(asset);
			ret.name = asset.name;
			action(ret);
		});
		yield return new WaitWhile(() => ret == null);
	}
}
