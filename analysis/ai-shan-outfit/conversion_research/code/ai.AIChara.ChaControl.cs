#define OUTPUT_LOG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CharaUtils;
using FBSAssist;
using Illusion.Extensions;
using IllusionUtility.GetUtility;
using IllusionUtility.SetUtility;
using Manager;
using OutputLogControl;
using RootMotion.FinalIK;
using UnityEngine;

namespace AIChara;

public class ChaControl : ChaInfo
{
	public class MannequinBackInfo
	{
		public byte[] custom;

		public int eyesPtn;

		public float eyesOpen;

		public int mouthPtn;

		public float mouthOpen;

		public bool mouthFixed;

		public int neckLook;

		public byte[] eyesInfo;

		public bool mannequin;

		public void Backup(ChaControl chaCtrl)
		{
			custom = chaCtrl.chaFile.GetCustomBytes();
			eyesPtn = chaCtrl.fileStatus.eyesPtn;
			eyesOpen = chaCtrl.fileStatus.eyesOpenMax;
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
			chaCtrl.ChangeEyesOpenMax(eyesOpen);
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

	public enum BodyTexKind
	{
		inpBase,
		inpPaint01,
		inpPaint02,
		inpSunburn
	}

	public enum FaceTexKind
	{
		inpBase,
		inpEyeshadow,
		inpPaint01,
		inpPaint02,
		inpCheek,
		inpLip,
		inpMole
	}

	private MannequinBackInfo mannequinBackInfo = new MannequinBackInfo();

	private bool confSon = true;

	private bool confBody = true;

	private bool drawSimple;

	private List<bool> lstActive = new List<bool>();

	private AssignedAnotherWeights aaWeightsHead;

	private AssignedAnotherWeights aaWeightsBody;

	public bool IsVisibleInCamera
	{
		get
		{
			if (null != base.cmpBody && base.cmpBody.isVisible)
			{
				return true;
			}
			if (null != base.cmpFace && base.cmpFace.isVisible)
			{
				return true;
			}
			if (base.cmpHair != null)
			{
				for (int i = 0; i < base.cmpHair.Length; i++)
				{
					if (!(null == base.cmpHair[i]) && base.cmpHair[i].isVisible)
					{
						return true;
					}
				}
			}
			if (base.cmpClothes != null)
			{
				for (int j = 0; j < base.cmpClothes.Length; j++)
				{
					if (!(null == base.cmpClothes[j]) && base.cmpClothes[j].isVisible)
					{
						return true;
					}
				}
			}
			if (base.cmpAccessory != null)
			{
				for (int k = 0; k < base.cmpAccessory.Length; k++)
				{
					if (!(null == base.cmpAccessory[k]) && base.cmpAccessory[k].isVisible)
					{
						return true;
					}
				}
			}
			if (base.cmpExtraAccessory != null)
			{
				for (int l = 0; l < base.cmpExtraAccessory.Length; l++)
				{
					if (!(null == base.cmpExtraAccessory[l]) && base.cmpExtraAccessory[l].isVisible)
					{
						return true;
					}
				}
			}
			return false;
		}
	}

	public bool[] hideHairAcs { get; private set; }

	private Texture texBodyAlphaMask { get; set; }

	private Texture texBraAlphaMask { get; set; }

	private Texture texInnerTBAlphaMask { get; set; }

	private Texture texInnerBAlphaMask { get; set; }

	private Texture texPanstAlphaMask { get; set; }

	private Texture texBodyBAlphaMask { get; set; }

	private int underMaskReflectionType { get; set; } = -1;


	private bool underMaskBreakDisable { get; set; }

	public bool hideInnerBWithBot { get; set; }

	public BustNormal bustNormal { get; private set; }

	private byte[] siruNewLv { get; set; }

	public float siriAkaRate => base.fileStatus.siriAkaRate;

	public float wetRate
	{
		get
		{
			return base.fileStatus.wetRate;
		}
		set
		{
			float num = Mathf.Clamp(value, 0f, 1f);
			if (base.fileStatus.wetRate != num)
			{
				base.updateWet = true;
			}
			base.fileStatus.wetRate = num;
		}
	}

	public float skinGlossRate
	{
		get
		{
			return base.fileStatus.skinTuyaRate;
		}
		set
		{
			float num = Mathf.Clamp(value, 0f, 1f);
			if (base.fileStatus.skinTuyaRate != num)
			{
				base.fileStatus.skinTuyaRate = num;
				ChangeBodyGlossPower();
				ChangeFaceGlossPower();
			}
		}
	}

	public ChaFileCoordinate nowCoordinate { get; private set; }

	public bool notInnerT { get; private set; }

	public bool notBot { get; private set; }

	public bool notInnerB { get; private set; }

	public Dictionary<int, Dictionary<byte, string>> dictStateType { get; private set; }

	public bool IsBareFoot
	{
		get
		{
			if (IsClothes(7))
			{
				return 0 != base.fileStatus.clothesState[7];
			}
			return true;
		}
	}

	private int ShapeBodyNum { get; set; }

	public ShapeInfoBase sibBody { get; set; }

	private bool changeShapeBodyMask { get; set; }

	public BustSoft bustSoft { get; private set; }

	public BustGravity bustGravity { get; private set; }

	public bool[] updateCMBodyTex { get; private set; }

	public bool[] updateCMBodyColor { get; private set; }

	public bool[] updateCMBodyGloss { get; private set; }

	public bool[] updateCMBodyLayout { get; private set; }

	private int ShapeFaceNum { get; set; }

	private ShapeInfoBase sibFace { get; set; }

	public bool[] updateCMFaceTex { get; private set; }

	public bool[] updateCMFaceColor { get; private set; }

	public bool[] updateCMFaceGloss { get; private set; }

	public bool[] updateCMFaceLayout { get; private set; }

	public AudioSource asVoice { get; private set; }

	private AudioAssist fbsaaVoice { get; set; }

	public float tearsRate => base.fileStatus.tearsRate;

	public float hohoAkaRate => base.fileStatus.hohoAkaRate;

	public ShapeInfoBase sibHand { get; set; }

	private bool updateAlphaMask { get; set; }

	private bool updateAlphaMask2 { get; set; }

	public void Initialize(byte _sex, GameObject _objRoot, int _id, int _no, ChaFileControl _chaFile = null)
	{
		if (_chaFile != null && _sex != _chaFile.parameter.sex)
		{
			Debug.LogWarning("作成する性別とファイルの性別が食い違っている");
		}
		MemberInitializeAll();
		InitializeControlLoadAll();
		InitializeControlFaceAll();
		InitializeControlBodyAll();
		InitializeControlCoordinateAll();
		InitializeControlAccessoryAll();
		InitializeControlCustomBodyAll();
		InitializeControlCustomFaceAll();
		InitializeControlCustomHairAll();
		InitializeControlHandAll();
		base.objRoot = _objRoot;
		base.chaID = _id;
		base.loadNo = _no;
		base.hideMoz = false;
		base.lstCtrl = Singleton<Character>.Instance.chaListCtrl;
		if (_chaFile == null)
		{
			base.chaFile = new ChaFileControl();
			LoadPreset(_sex, string.Empty);
		}
		else
		{
			base.chaFile = _chaFile;
		}
		base.chaFile.parameter.sex = _sex;
		InitBaseCustomTextureBody();
		ChangeNowCoordinate();
		if (_sex == 0)
		{
			base.chaFile.custom.body.shapeValueBody[0] = 0.75f;
		}
		if (_sex == 1)
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
		ReleaseControlAccessoryAll();
		ReleaseControlCustomBodyAll();
		ReleaseControlCustomFaceAll();
		ReleaseControlCustomHairAll();
		ReleaseControlHandAll();
		ReleaseInfoAll();
	}

	public void ReleaseObject()
	{
		ReleaseControlLoadObject();
		ReleaseControlFaceObject();
		ReleaseControlBodyObject();
		ReleaseControlCoordinateObject();
		ReleaseControlAccessoryObject();
		ReleaseControlCustomBodyObject();
		ReleaseControlCustomFaceObject();
		ReleaseControlCustomHairObject();
		ReleaseControlHandObject();
		ReleaseInfoObject();
		if (Singleton<Character>.Instance.enableCharaLoadGCClear)
		{
			UnityEngine.Resources.UnloadUnusedAssets();
			OutputLog.Log("ReleaseObject", unityLog: false, "UnloadUnusedAssets");
			GC.Collect();
		}
	}

	public void LoadPreset(int _sex, string presetName = "")
	{
		string empty = string.Empty;
		empty = ((!presetName.IsNullOrEmpty()) ? presetName : ((_sex != 0) ? "ill_Default_Female" : "ill_Default_Male"));
		Dictionary<int, ListInfoBase> dictionary = null;
		dictionary = ((_sex != 0) ? base.lstCtrl.GetCategoryInfo(ChaListDefine.CategoryNo.cha_sample_f) : base.lstCtrl.GetCategoryInfo(ChaListDefine.CategoryNo.cha_sample_m));
		foreach (KeyValuePair<int, ListInfoBase> item in dictionary)
		{
			if (item.Value.GetInfo(ChaListDefine.KeyType.MainData) == empty)
			{
				base.chaFile.LoadFromAssetBundle(item.Value.GetInfo(ChaListDefine.KeyType.MainAB), empty);
				break;
			}
		}
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
		for (int i = 0; i < num2; i++)
		{
			ChaFileControl chaFileControl = new ChaFileControl();
			if (chaFileControl.LoadCharaFile(list[i], 1, noLoadPng: true) && chaFileControl.parameter.sex != 0)
			{
				list2.Add(chaFileControl);
			}
		}
		return list2.ToArray();
	}

	public void SetActiveTop(bool active)
	{
		if (null != base.objTop)
		{
			base.objTop.SetActiveIfDifferent(active);
		}
	}

	public bool GetActiveTop()
	{
		if (null != base.objTop)
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
		return (!(null == base.objTop)) ? base.objTop.transform.localPosition : Vector3.zero;
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

	public void SetRotation(Quaternion rot)
	{
		if (null != base.objTop)
		{
			base.objTop.transform.localRotation = rot;
		}
	}

	public Vector3 GetRotation()
	{
		return (!(null == base.objTop)) ? base.objTop.transform.localRotation.eulerAngles : Vector3.zero;
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
				string assetBundleName = ChaABDefine.PresetAssetBundle(base.sex);
				string assetName = ChaABDefine.PresetAsset(base.sex);
				base.chaFile.LoadMannequinFile(assetBundleName, assetName, face: true, body: true, hair: true, parameter: false, coordinate: false);
				Reload(noChangeClothes: true);
				ChangeEyesPtn(0, blend: false);
				ChangeEyesOpenMax(0f);
				ChangeMouthPtn(0, blend: false);
				base.fileStatus.mouthFixed = true;
				ChangeMouthOpenMax(0f);
				base.neckLookCtrl.neckLookScript.skipCalc = true;
				base.neckLookCtrl.ForceLateUpdate();
				base.eyeLookCtrl.ForceLateUpdate();
				base.resetDynamicBoneAll = true;
				LateUpdateForce();
			}
		}
		else if (mannequinBackInfo.mannequin)
		{
			mannequinBackInfo.Restore(this);
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
		if (Singleton<Character>.IsInstance())
		{
			Singleton<Character>.Instance.DeleteChara(this, entryOnly: true);
		}
		ReleaseAll();
	}

	public void UpdateForce()
	{
		if (base.loadEnd)
		{
			UpdateBlendShapeVoice();
			UpdateSiru();
			if (base.updateWet)
			{
				UpdateWet();
				base.updateWet = false;
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
		if (null != base.cmpBoneBody && null != base.cmpBoneBody.targetEtc.trfAnaCorrect && null != base.cmpBoneBody.targetAccessory.acs_Ana)
		{
			base.cmpBoneBody.targetAccessory.acs_Ana.localScale = new Vector3(1f / base.cmpBoneBody.targetEtc.trfAnaCorrect.localScale.x, 1f / base.cmpBoneBody.targetEtc.trfAnaCorrect.localScale.y, 1f / base.cmpBoneBody.targetEtc.trfAnaCorrect.localScale.z);
		}
		if (base.reSetupDynamicBoneBust)
		{
			ReSetupDynamicBoneBust();
			UpdateBustSoftnessAndGravity();
			base.reSetupDynamicBoneBust = false;
		}
		if (base.updateBustSize && base.sex == 1)
		{
			int num = 1;
			float rate = 1f;
			if (0.5f > base.chaFile.custom.body.shapeValueBody[num])
			{
				rate = Mathf.InverseLerp(0f, 0.5f, base.chaFile.custom.body.shapeValueBody[num]);
			}
			if (bustNormal != null)
			{
				bustNormal.Blend(rate);
			}
			base.updateBustSize = false;
		}
		if (!IsVisibleInCamera)
		{
			if (null != base.cmpBoneBody)
			{
				for (int i = 0; i < base.enableDynamicBoneBustAndHip.Length; i++)
				{
					base.cmpBoneBody.EnableDynamicBonesBustAndHip(enable: false, i);
				}
			}
			if (base.cmpHair != null)
			{
				for (int j = 0; j < base.cmpHair.Length; j++)
				{
					if (null != base.cmpHair[j])
					{
						base.cmpHair[j].EnableDynamicBonesHair(enable: false);
					}
				}
			}
			if (base.cmpClothes != null)
			{
				for (int k = 0; k < base.cmpClothes.Length; k++)
				{
					if (null != base.cmpClothes[k])
					{
						base.cmpClothes[k].EnableDynamicBones(enable: false);
					}
				}
			}
			if (base.cmpAccessory != null)
			{
				for (int l = 0; l < base.cmpAccessory.Length; l++)
				{
					if (null != base.cmpAccessory[l])
					{
						base.cmpAccessory[l].EnableDynamicBones(enable: false);
					}
				}
			}
			if (base.cmpExtraAccessory == null)
			{
				return;
			}
			for (int m = 0; m < base.cmpExtraAccessory.Length; m++)
			{
				if (null != base.cmpExtraAccessory[m])
				{
					base.cmpExtraAccessory[m].EnableDynamicBones(enable: false);
				}
			}
			return;
		}
		if (null != base.cmpBoneBody)
		{
			for (int n = 0; n < base.enableDynamicBoneBustAndHip.Length; n++)
			{
				base.cmpBoneBody.EnableDynamicBonesBustAndHip(base.cmpBody.isVisible && base.enableDynamicBoneBustAndHip[n], n);
			}
		}
		if (base.cmpHair != null)
		{
			for (int num2 = 0; num2 < base.cmpHair.Length; num2++)
			{
				if (null != base.cmpHair[num2])
				{
					base.cmpHair[num2].EnableDynamicBonesHair(base.cmpHair[num2].isVisible, base.fileHair.parts[num2]);
				}
			}
		}
		if (base.cmpClothes != null)
		{
			for (int num3 = 0; num3 < base.cmpClothes.Length; num3++)
			{
				if (null != base.cmpClothes[num3])
				{
					base.cmpClothes[num3].EnableDynamicBones(base.fileStatus.clothesState[num3] == 0 && base.cmpClothes[num3].isVisible);
				}
			}
		}
		if (base.cmpAccessory != null)
		{
			for (int num4 = 0; num4 < base.cmpAccessory.Length; num4++)
			{
				if (null != base.cmpAccessory[num4])
				{
					base.cmpAccessory[num4].EnableDynamicBones(!nowCoordinate.accessory.parts[num4].noShake && base.cmpAccessory[num4].isVisible);
				}
			}
		}
		if (base.cmpExtraAccessory == null)
		{
			return;
		}
		for (int num5 = 0; num5 < base.cmpExtraAccessory.Length; num5++)
		{
			if (null != base.cmpExtraAccessory[num5])
			{
				base.cmpExtraAccessory[num5].EnableDynamicBones(base.cmpExtraAccessory[num5].isVisible);
			}
		}
	}

	protected void InitializeControlAccessoryAll()
	{
		InitializeControlAccessoryObject();
	}

	protected void InitializeControlAccessoryObject()
	{
		hideHairAcs = new bool[20];
	}

	protected void ReleaseControlAccessoryAll()
	{
		ReleaseControlAccessoryObject(init: false);
	}

	protected void ReleaseControlAccessoryObject(bool init = true)
	{
		if (init)
		{
			InitializeControlAccessoryObject();
		}
	}

	public bool IsAccessory(int slotNo)
	{
		if (!MathfEx.RangeEqualOn(0, slotNo, 19))
		{
			return false;
		}
		return !(null == base.objAccessory[slotNo]);
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

	public string GetAccessoryDefaultParentStr(int type, int id)
	{
		int num = Enum.GetNames(typeof(ChaListDefine.CategoryNo)).Length;
		if (!MathfEx.RangeEqualOn(0, type, num - 1))
		{
			return string.Empty;
		}
		ListInfoBase value = null;
		Dictionary<int, ListInfoBase> categoryInfo = base.lstCtrl.GetCategoryInfo((ChaListDefine.CategoryNo)type);
		if (!categoryInfo.TryGetValue(id, out value))
		{
			return string.Empty;
		}
		return value.GetInfo(ChaListDefine.KeyType.Parent);
	}

	public string GetAccessoryDefaultParentStr(int slotNo)
	{
		GameObject gameObject = base.objAccessory[slotNo];
		if (null == gameObject)
		{
			return string.Empty;
		}
		ListInfoComponent component = gameObject.GetComponent<ListInfoComponent>();
		return component.data.GetInfo(ChaListDefine.KeyType.Parent);
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
		ListInfoComponent component = gameObject.GetComponent<ListInfoComponent>();
		ListInfoBase data = component.data;
		if ("0" == data.GetInfo(ChaListDefine.KeyType.Parent))
		{
			return false;
		}
		try
		{
			Transform accessoryParentTransform = GetAccessoryParentTransform(parentStr);
			if (null == accessoryParentTransform)
			{
				Debug.LogWarning("親が見付からない");
				return false;
			}
			gameObject.transform.SetParent(accessoryParentTransform, worldPositionStays: false);
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
		Transform transform = base.trfAcsMove[slotNo, correctNo];
		if (null == transform)
		{
			return false;
		}
		ChaFileAccessory accessory = nowCoordinate.accessory;
		if (((uint)flags & (true ? 1u : 0u)) != 0)
		{
			float value2 = float.Parse((((!add) ? 0f : accessory.parts[slotNo].addMove[correctNo, 0].x) + value).ToString("f1"));
			accessory.parts[slotNo].addMove[correctNo, 0].x = Mathf.Clamp(value2, -100f, 100f);
		}
		if (((uint)flags & 2u) != 0)
		{
			float value3 = float.Parse((((!add) ? 0f : accessory.parts[slotNo].addMove[correctNo, 0].y) + value).ToString("f1"));
			accessory.parts[slotNo].addMove[correctNo, 0].y = Mathf.Clamp(value3, -100f, 100f);
		}
		if (((uint)flags & 4u) != 0)
		{
			float value4 = float.Parse((((!add) ? 0f : accessory.parts[slotNo].addMove[correctNo, 0].z) + value).ToString("f1"));
			accessory.parts[slotNo].addMove[correctNo, 0].z = Mathf.Clamp(value4, -100f, 100f);
		}
		transform.localPosition = new Vector3(accessory.parts[slotNo].addMove[correctNo, 0].x * 0.1f, accessory.parts[slotNo].addMove[correctNo, 0].y * 0.1f, accessory.parts[slotNo].addMove[correctNo, 0].z * 0.1f);
		return true;
	}

	public bool SetAccessoryRot(int slotNo, int correctNo, float value, bool add, int flags = 7)
	{
		if (!MathfEx.RangeEqualOn(0, slotNo, 19))
		{
			return false;
		}
		Transform transform = base.trfAcsMove[slotNo, correctNo];
		if (null == transform)
		{
			return false;
		}
		ChaFileAccessory accessory = nowCoordinate.accessory;
		if (((uint)flags & (true ? 1u : 0u)) != 0)
		{
			float t = (int)(((!add) ? 0f : accessory.parts[slotNo].addMove[correctNo, 1].x) + value);
			accessory.parts[slotNo].addMove[correctNo, 1].x = Mathf.Repeat(t, 360f);
		}
		if (((uint)flags & 2u) != 0)
		{
			float t2 = (int)(((!add) ? 0f : accessory.parts[slotNo].addMove[correctNo, 1].y) + value);
			accessory.parts[slotNo].addMove[correctNo, 1].y = Mathf.Repeat(t2, 360f);
		}
		if (((uint)flags & 4u) != 0)
		{
			float t3 = (int)(((!add) ? 0f : accessory.parts[slotNo].addMove[correctNo, 1].z) + value);
			accessory.parts[slotNo].addMove[correctNo, 1].z = Mathf.Repeat(t3, 360f);
		}
		transform.localEulerAngles = new Vector3(accessory.parts[slotNo].addMove[correctNo, 1].x, accessory.parts[slotNo].addMove[correctNo, 1].y, accessory.parts[slotNo].addMove[correctNo, 1].z);
		return true;
	}

	public bool SetAccessoryScl(int slotNo, int correctNo, float value, bool add, int flags = 7)
	{
		if (!MathfEx.RangeEqualOn(0, slotNo, 19))
		{
			return false;
		}
		Transform transform = base.trfAcsMove[slotNo, correctNo];
		if (null == transform)
		{
			return false;
		}
		ChaFileAccessory accessory = nowCoordinate.accessory;
		if (((uint)flags & (true ? 1u : 0u)) != 0)
		{
			float value2 = float.Parse((((!add) ? 0f : accessory.parts[slotNo].addMove[correctNo, 2].x) + value).ToString("f2"));
			accessory.parts[slotNo].addMove[correctNo, 2].x = Mathf.Clamp(value2, 0.01f, 100f);
		}
		if (((uint)flags & 2u) != 0)
		{
			float value3 = float.Parse((((!add) ? 0f : accessory.parts[slotNo].addMove[correctNo, 2].y) + value).ToString("f2"));
			accessory.parts[slotNo].addMove[correctNo, 2].y = Mathf.Clamp(value3, 0.01f, 100f);
		}
		if (((uint)flags & 4u) != 0)
		{
			float value4 = float.Parse((((!add) ? 0f : accessory.parts[slotNo].addMove[correctNo, 2].z) + value).ToString("f2"));
			accessory.parts[slotNo].addMove[correctNo, 2].z = Mathf.Clamp(value4, 0.01f, 100f);
		}
		transform.localScale = new Vector3(accessory.parts[slotNo].addMove[correctNo, 2].x, accessory.parts[slotNo].addMove[correctNo, 2].y, accessory.parts[slotNo].addMove[correctNo, 2].z);
		return true;
	}

	public bool ResetAccessoryMove(int slotNo, int correctNo, int type = 7)
	{
		bool flag = true;
		if (((uint)type & (true ? 1u : 0u)) != 0)
		{
			flag &= SetAccessoryPos(slotNo, correctNo, 0f, add: false);
		}
		if (((uint)type & 2u) != 0)
		{
			flag &= SetAccessoryRot(slotNo, correctNo, 0f, add: false);
		}
		if (((uint)type & 4u) != 0)
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
		for (int i = 0; i < 2; i++)
		{
			Transform transform = base.trfAcsMove[slotNo, i];
			if (!(null == transform))
			{
				transform.localPosition = new Vector3(accessory.parts[slotNo].addMove[i, 0].x * 0.1f, accessory.parts[slotNo].addMove[i, 0].y * 0.1f, accessory.parts[slotNo].addMove[i, 0].z * 0.1f);
				transform.localEulerAngles = new Vector3(accessory.parts[slotNo].addMove[i, 1].x, accessory.parts[slotNo].addMove[i, 1].y, accessory.parts[slotNo].addMove[i, 1].z);
				transform.localScale = new Vector3(accessory.parts[slotNo].addMove[i, 2].x, accessory.parts[slotNo].addMove[i, 2].y, accessory.parts[slotNo].addMove[i, 2].z);
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
		CmpAccessory cmpAccessory = base.cmpAccessory[slotNo];
		ChaFileAccessory.PartsInfo partsInfo = nowCoordinate.accessory.parts[slotNo];
		if (null == cmpAccessory)
		{
			return false;
		}
		if (cmpAccessory.rendNormal != null)
		{
			Renderer[] rendNormal = cmpAccessory.rendNormal;
			foreach (Renderer renderer in rendNormal)
			{
				if (cmpAccessory.useColor01)
				{
					renderer.material.SetColor(ChaShader.Color, partsInfo.colorInfo[0].color);
					renderer.material.SetFloat(ChaShader.ClothesGloss1, partsInfo.colorInfo[0].glossPower);
					renderer.material.SetFloat(ChaShader.Metallic, partsInfo.colorInfo[0].metallicPower);
				}
				if (cmpAccessory.useColor02)
				{
					renderer.material.SetColor(ChaShader.Color2, partsInfo.colorInfo[1].color);
					renderer.material.SetFloat(ChaShader.ClothesGloss2, partsInfo.colorInfo[1].glossPower);
					renderer.material.SetFloat(ChaShader.Metallic2, partsInfo.colorInfo[1].metallicPower);
				}
				if (cmpAccessory.useColor03)
				{
					renderer.material.SetColor(ChaShader.Color3, partsInfo.colorInfo[2].color);
					renderer.material.SetFloat(ChaShader.ClothesGloss3, partsInfo.colorInfo[2].glossPower);
					renderer.material.SetFloat(ChaShader.Metallic3, partsInfo.colorInfo[2].metallicPower);
				}
			}
		}
		if (cmpAccessory.rendAlpha != null)
		{
			Renderer[] rendAlpha = cmpAccessory.rendAlpha;
			foreach (Renderer renderer2 in rendAlpha)
			{
				renderer2.material.SetColor(ChaShader.Color, partsInfo.colorInfo[3].color);
				renderer2.material.SetFloat(ChaShader.ClothesGloss4, partsInfo.colorInfo[3].glossPower);
				renderer2.material.SetFloat(ChaShader.Metallic4, partsInfo.colorInfo[3].metallicPower);
				renderer2.gameObject.SetActiveIfDifferent(partsInfo.colorInfo[3].color.a != 0f);
			}
		}
		return true;
	}

	public bool GetAccessoryDefaultColor(ref Color color, ref float gloss, ref float metallic, int slotNo, int no)
	{
		if (!MathfEx.RangeEqualOn(0, slotNo, 19))
		{
			return false;
		}
		CmpAccessory cmpAccessory = base.cmpAccessory[slotNo];
		if (null == cmpAccessory)
		{
			return false;
		}
		if (no == 0 && cmpAccessory.useColor01)
		{
			color = cmpAccessory.defColor01;
			gloss = cmpAccessory.defGlossPower01;
			metallic = cmpAccessory.defMetallicPower01;
			return true;
		}
		if (no == 1 && cmpAccessory.useColor02)
		{
			color = cmpAccessory.defColor02;
			gloss = cmpAccessory.defGlossPower02;
			metallic = cmpAccessory.defMetallicPower02;
			return true;
		}
		if (no == 2 && cmpAccessory.useColor03)
		{
			color = cmpAccessory.defColor03;
			gloss = cmpAccessory.defGlossPower03;
			metallic = cmpAccessory.defMetallicPower03;
			return true;
		}
		if (no == 3 && cmpAccessory.rendAlpha != null && cmpAccessory.rendAlpha.Length != 0)
		{
			color = cmpAccessory.defColor04;
			gloss = cmpAccessory.defGlossPower04;
			metallic = cmpAccessory.defMetallicPower04;
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
		CmpAccessory cmpAccessory = base.cmpAccessory[slotNo];
		if (!(null == cmpAccessory))
		{
			if (cmpAccessory.useColor01)
			{
				nowCoordinate.accessory.parts[slotNo].colorInfo[0].color = cmpAccessory.defColor01;
				nowCoordinate.accessory.parts[slotNo].colorInfo[0].glossPower = cmpAccessory.defGlossPower01;
				nowCoordinate.accessory.parts[slotNo].colorInfo[0].metallicPower = cmpAccessory.defMetallicPower01;
			}
			if (cmpAccessory.useColor02)
			{
				nowCoordinate.accessory.parts[slotNo].colorInfo[1].color = cmpAccessory.defColor02;
				nowCoordinate.accessory.parts[slotNo].colorInfo[1].glossPower = cmpAccessory.defGlossPower02;
				nowCoordinate.accessory.parts[slotNo].colorInfo[1].metallicPower = cmpAccessory.defMetallicPower02;
			}
			if (cmpAccessory.useColor03)
			{
				nowCoordinate.accessory.parts[slotNo].colorInfo[2].color = cmpAccessory.defColor03;
				nowCoordinate.accessory.parts[slotNo].colorInfo[2].glossPower = cmpAccessory.defGlossPower03;
				nowCoordinate.accessory.parts[slotNo].colorInfo[2].metallicPower = cmpAccessory.defMetallicPower03;
			}
			if (cmpAccessory.rendAlpha != null && cmpAccessory.rendAlpha.Length != 0)
			{
				nowCoordinate.accessory.parts[slotNo].colorInfo[3].color = cmpAccessory.defColor04;
				nowCoordinate.accessory.parts[slotNo].colorInfo[3].glossPower = cmpAccessory.defGlossPower04;
				nowCoordinate.accessory.parts[slotNo].colorInfo[3].metallicPower = cmpAccessory.defMetallicPower04;
			}
		}
	}

	public void ResetDynamicBoneAccessories(bool includeInactive = false)
	{
		if (base.cmpAccessory == null)
		{
			return;
		}
		for (int i = 0; i < base.cmpAccessory.Length; i++)
		{
			if (!(null == base.cmpAccessory[i]))
			{
				base.cmpAccessory[i].ResetDynamicBones(includeInactive);
			}
		}
	}

	public bool ChangeHairTypeAccessoryColor(int slotNo)
	{
		if (!MathfEx.RangeEqualOn(0, slotNo, 19))
		{
			return false;
		}
		CmpAccessory cmpAccessory = base.cmpAccessory[slotNo];
		ChaFileAccessory.PartsInfo partsInfo = nowCoordinate.accessory.parts[slotNo];
		if (null == cmpAccessory)
		{
			return false;
		}
		if (cmpAccessory.rendNormal != null)
		{
			Renderer[] rendNormal = cmpAccessory.rendNormal;
			foreach (Renderer renderer in rendNormal)
			{
				if (cmpAccessory.useColor01)
				{
					renderer.material.SetColor(ChaShader.HairMainColor, partsInfo.colorInfo[0].color);
				}
				if (cmpAccessory.useColor02)
				{
					renderer.material.SetColor(ChaShader.HairTopColor, partsInfo.colorInfo[1].color);
				}
				if (cmpAccessory.useColor03)
				{
					renderer.material.SetColor(ChaShader.HairUnderColor, partsInfo.colorInfo[2].color);
				}
				renderer.material.SetColor(ChaShader.Specular, partsInfo.colorInfo[3].color);
				renderer.material.SetFloat(ChaShader.Smoothness, partsInfo.colorInfo[0].smoothnessPower);
				renderer.material.SetFloat(ChaShader.Metallic, partsInfo.colorInfo[0].metallicPower);
			}
		}
		return true;
	}

	public void ChangeSettingHairTypeAccessoryShaderAll()
	{
		for (int i = 0; i < 20; i++)
		{
			ChangeSettingHairTypeAccessoryShader(i);
		}
	}

	public void ChangeSettingHairTypeAccessoryShader(int slotNo)
	{
		if (!MathfEx.RangeEqualOn(0, slotNo, 19))
		{
			return;
		}
		CmpAccessory cmpAccessory = base.cmpAccessory[slotNo];
		if (null == cmpAccessory)
		{
			return;
		}
		ChaFileAccessory.PartsInfo partsInfo = nowCoordinate.accessory.parts[slotNo];
		if (!cmpAccessory.typeHair)
		{
			return;
		}
		ChaFileHair hair = base.chaFile.custom.hair;
		Shader shader = ((hair.shaderType != 0) ? Singleton<Character>.Instance.shaderCutout : Singleton<Character>.Instance.shaderDithering);
		int num = 0;
		if (base.infoAccessory[slotNo] == null)
		{
			return;
		}
		string info = base.infoAccessory[slotNo].GetInfo(ChaListDefine.KeyType.TexManifest);
		string info2 = base.infoAccessory[slotNo].GetInfo(ChaListDefine.KeyType.TexAB);
		string info3 = base.infoAccessory[slotNo].GetInfo((hair.shaderType != 0) ? ChaListDefine.KeyType.TexC : ChaListDefine.KeyType.TexD);
		Texture2D value = CommonLib.LoadAsset<Texture2D>(info2, info3, clone: false, info);
		Singleton<Character>.Instance.AddLoadAssetBundle(info2, info);
		for (int i = 0; i < cmpAccessory.rendNormal.Length; i++)
		{
			for (int j = 0; j < cmpAccessory.rendNormal[i].materials.Length; j++)
			{
				num = cmpAccessory.rendNormal[i].materials[j].renderQueue;
				cmpAccessory.rendNormal[i].materials[j].shader = shader;
				cmpAccessory.rendNormal[i].materials[j].SetTexture(ChaShader.MainTex, value);
				cmpAccessory.rendNormal[i].materials[j].renderQueue = num;
			}
		}
	}

	public void ChangeExtraAccessory(ChaControlDefine.ExtraAccessoryParts parts, int id)
	{
		StartCoroutine(ChangeExtraAccessory(parts, id, asyncFlags: false));
	}

	public IEnumerator ChangeExtraAccessory(ChaControlDefine.ExtraAccessoryParts parts, int id, bool asyncFlags = true)
	{
		if (!MathfEx.RangeEqualOn(0, (int)parts, Enum.GetNames(typeof(ChaControlDefine.ExtraAccessoryParts)).Length - 1))
		{
			yield break;
		}
		if (id == -1)
		{
			if (null != base.objExtraAccessory[(int)parts])
			{
				base.cmpExtraAccessory[(int)parts] = null;
				SafeDestroy(base.objExtraAccessory[(int)parts]);
				base.objExtraAccessory[(int)parts] = null;
			}
			yield break;
		}
		if (asyncFlags)
		{
			yield return null;
		}
		if (null != base.objExtraAccessory[(int)parts])
		{
			base.cmpExtraAccessory[(int)parts] = null;
			SafeDestroy(base.objExtraAccessory[(int)parts]);
			base.objExtraAccessory[(int)parts] = null;
		}
		if (!Singleton<Manager.Resources>.Instance.GameInfo.AccessoryItem.TryGetValue(10, out var dic) || !dic.TryGetValue(id, out var item))
		{
			yield break;
		}
		string manifestName = item.Manifest;
		string assetBundleName = item.AssetBundle;
		string assetName = item.Asset;
		GameObject newObj = null;
		if (asyncFlags)
		{
			yield return StartCoroutine(Load_Coroutine(assetBundleName, assetName, delegate(GameObject x)
			{
				newObj = x;
			}, isClone: true, manifestName));
		}
		else
		{
			newObj = CommonLib.LoadAsset<GameObject>(assetBundleName, assetName, clone: true, manifestName);
		}
		Singleton<Character>.Instance.AddLoadAssetBundle(assetBundleName, manifestName);
		if (null == newObj)
		{
			Debug.LogError("マップアクセサリの読込みに失敗");
			yield break;
		}
		newObj.name = ChaControlDefine.extraAcsNames[(int)parts];
		string parentName = item.ParentName;
		Transform trfParent = GetAccessoryParentTransform(parentName);
		newObj.transform.SetParent(trfParent, worldPositionStays: false);
		DynamicBoneCollider[] dbc = base.objBodyBone.GetComponentsInChildren<DynamicBoneCollider>(includeInactive: true);
		Dictionary<string, GameObject> dictBone = aaWeightsBody.dictBone;
		DynamicBone[] db = newObj.GetComponentsInChildren<DynamicBone>(includeInactive: true);
		GameObject obj = null;
		DynamicBone[] array = db;
		foreach (DynamicBone dynamicBone in array)
		{
			if (null != dynamicBone.m_Root && dictBone.TryGetValue(dynamicBone.m_Root.name, out obj))
			{
				dynamicBone.m_Root = obj.transform;
			}
			if (dynamicBone.m_Exclusions != null && dynamicBone.m_Exclusions.Count != 0)
			{
				for (int j = 0; j < dynamicBone.m_Exclusions.Count; j++)
				{
					if (!(null == dynamicBone.m_Exclusions[j]) && dictBone.TryGetValue(dynamicBone.m_Exclusions[j].name, out obj))
					{
						dynamicBone.m_Exclusions[j] = obj.transform;
					}
				}
			}
			if (dynamicBone.m_notRolls != null && dynamicBone.m_notRolls.Count != 0)
			{
				for (int k = 0; k < dynamicBone.m_notRolls.Count; k++)
				{
					if (!(null == dynamicBone.m_notRolls[k]) && dictBone.TryGetValue(dynamicBone.m_notRolls[k].name, out obj))
					{
						dynamicBone.m_notRolls[k] = obj.transform;
					}
				}
			}
			if (dynamicBone.m_Colliders != null)
			{
				dynamicBone.m_Colliders.Clear();
				dynamicBone.m_Colliders.AddRange(dbc);
			}
		}
		Transform trfRootBone = base.cmpBoneBody.targetEtc.trfRoot;
		switch (item.Weight)
		{
		case 1:
			aaWeightsBody.AssignedWeightsAndSetBounds(newObj, "cf_J_Root", ChaControlDefine.bounds, trfRootBone);
			break;
		case 2:
			aaWeightsHead.AssignedWeights(newObj, "cf_J_FaceRoot", trfRootBone);
			break;
		}
		base.objExtraAccessory[(int)parts] = newObj;
		if (null != base.objExtraAccessory[(int)parts])
		{
			base.cmpExtraAccessory[(int)parts] = base.objExtraAccessory[(int)parts].GetComponent<CmpAccessory>();
			if (null != base.cmpExtraAccessory[(int)parts])
			{
				base.cmpExtraAccessory[(int)parts].InitDynamicBones();
			}
		}
	}

	public void ResetDynamicBoneExtraAccessories(bool includeInactive = false)
	{
		if (base.cmpExtraAccessory == null)
		{
			return;
		}
		for (int i = 0; i < base.cmpExtraAccessory.Length; i++)
		{
			if (!(null == base.cmpExtraAccessory[i]))
			{
				base.cmpExtraAccessory[i].ResetDynamicBones(includeInactive);
			}
		}
	}

	public bool ChangeExtraAccessoryColor(ChaControlDefine.ExtraAccessoryParts parts, params Color[] color)
	{
		if (color == null)
		{
			return false;
		}
		if (!MathfEx.RangeEqualOn(0, (int)parts, Enum.GetNames(typeof(ChaControlDefine.ExtraAccessoryParts)).Length - 1))
		{
			return false;
		}
		CmpAccessory cmpAccessory = base.cmpExtraAccessory[(int)parts];
		if (null == cmpAccessory)
		{
			return false;
		}
		if (cmpAccessory.rendNormal != null)
		{
			Renderer[] rendNormal = cmpAccessory.rendNormal;
			foreach (Renderer renderer in rendNormal)
			{
				if (cmpAccessory.useColor01 && 1 <= color.Length)
				{
					renderer.material.SetColor(ChaShader.Color, color[0]);
				}
				if (cmpAccessory.useColor02 && 2 <= color.Length)
				{
					renderer.material.SetColor(ChaShader.Color2, color[1]);
				}
				if (cmpAccessory.useColor03 && 3 <= color.Length)
				{
					renderer.material.SetColor(ChaShader.Color3, color[2]);
				}
			}
		}
		if (cmpAccessory.rendAlpha != null && 4 <= color.Length)
		{
			Renderer[] rendAlpha = cmpAccessory.rendAlpha;
			foreach (Renderer renderer2 in rendAlpha)
			{
				renderer2.material.SetColor(ChaShader.Color, color[3]);
			}
		}
		return true;
	}

	public bool GetExtraAccessoryDefaultColor(ref Color color, ChaControlDefine.ExtraAccessoryParts parts, int no)
	{
		if (!MathfEx.RangeEqualOn(0, (int)parts, Enum.GetNames(typeof(ChaControlDefine.ExtraAccessoryParts)).Length - 1))
		{
			return false;
		}
		CmpAccessory cmpAccessory = base.cmpExtraAccessory[(int)parts];
		if (null == cmpAccessory)
		{
			return false;
		}
		if (no == 0 && cmpAccessory.useColor01)
		{
			color = cmpAccessory.defColor01;
			return true;
		}
		if (no == 1 && cmpAccessory.useColor02)
		{
			color = cmpAccessory.defColor02;
			return true;
		}
		if (no == 2 && cmpAccessory.useColor03)
		{
			color = cmpAccessory.defColor03;
			return true;
		}
		if (no == 3 && cmpAccessory.rendAlpha != null && cmpAccessory.rendAlpha.Length != 0)
		{
			color = cmpAccessory.defColor04;
			return true;
		}
		return false;
	}

	public void ShowExtraAccessory(ChaControlDefine.ExtraAccessoryParts parts, bool show)
	{
		base.showExtraAccessory[(int)parts] = show;
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

	public bool IsParameterInAnimator(string strParameter)
	{
		if (base.animBody == null || base.animBody.runtimeAnimatorController == null)
		{
			return false;
		}
		return Array.FindIndex(base.animBody.parameters, (AnimatorControllerParameter p) => p.name == strParameter) != -1;
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
		texBodyAlphaMask = null;
		texBraAlphaMask = null;
		texInnerTBAlphaMask = null;
		texInnerBAlphaMask = null;
		texPanstAlphaMask = null;
		texBodyBAlphaMask = null;
		hideInnerBWithBot = false;
		bustNormal = null;
	}

	protected void ReleaseControlBodyAll()
	{
		ReleaseControlBodyObject(init: false);
	}

	protected void ReleaseControlBodyObject(bool init = true)
	{
		if (base.sex == 1)
		{
			if (bustNormal != null)
			{
				bustNormal.Release();
			}
			bustNormal = null;
		}
		for (int i = 0; i < siruNewLv.Length; i++)
		{
			siruNewLv[i] = 0;
		}
		OutputLog.Log("ReleaseControlBodyObject", unityLog: false, "UnloadUnusedAssets");
		UnityEngine.Resources.UnloadUnusedAssets();
		if (init)
		{
			InitializeControlBodyObject();
		}
	}

	public void ResetDynamicBoneBustAndHip(bool includeInactive = false)
	{
		if (null != base.cmpBoneBody)
		{
			base.cmpBoneBody.ResetDynamicBonesBustAndHip(includeInactive);
		}
	}

	public void ResetDynamicBoneAll(bool includeInactive = false)
	{
		ResetDynamicBoneHair(includeInactive);
		ResetDynamicBoneBustAndHip(includeInactive);
		ResetDynamicBoneClothes(includeInactive);
		ResetDynamicBoneAccessories(includeInactive);
	}

	public DynamicBone_Ver02 GetDynamicBoneBustAndHip(ChaControlDefine.DynamicBoneKind area)
	{
		if (null == base.cmpBoneBody)
		{
			return null;
		}
		return base.cmpBoneBody.GetDynamicBoneBustAndHip(area);
	}

	public bool ReSetupDynamicBoneBust(int cate = 0)
	{
		DynamicBone_Ver02 dynamicBone_Ver = null;
		if (cate == 0)
		{
			dynamicBone_Ver = GetDynamicBoneBustAndHip(ChaControlDefine.DynamicBoneKind.BreastL);
			if (null != dynamicBone_Ver)
			{
				dynamicBone_Ver.ResetPosition();
			}
			dynamicBone_Ver = GetDynamicBoneBustAndHip(ChaControlDefine.DynamicBoneKind.BreastR);
			if (null != dynamicBone_Ver)
			{
				dynamicBone_Ver.ResetPosition();
			}
		}
		else
		{
			dynamicBone_Ver = GetDynamicBoneBustAndHip(ChaControlDefine.DynamicBoneKind.HipL);
			if (null != dynamicBone_Ver)
			{
				dynamicBone_Ver.ResetPosition();
			}
			dynamicBone_Ver = GetDynamicBoneBustAndHip(ChaControlDefine.DynamicBoneKind.HipR);
			if (null != dynamicBone_Ver)
			{
				dynamicBone_Ver.ResetPosition();
			}
		}
		return true;
	}

	public void playDynamicBoneBust(int area, bool play)
	{
		if (area < base.enableDynamicBoneBustAndHip.Length)
		{
			base.enableDynamicBoneBustAndHip[area] = base.sex != 0 && play;
		}
	}

	public void playDynamicBoneBust(ChaControlDefine.DynamicBoneKind area, bool play)
	{
		playDynamicBoneBust((int)area, play);
	}

	public bool ChangeNipRate(float rate)
	{
		base.chaFile.status.nipStandRate = rate;
		changeShapeBodyMask = true;
		base.updateShapeBody = true;
		return true;
	}

	public void ChangeBustInert(bool h)
	{
		if (base.sex == 1)
		{
			float inert = 0.8f;
			if (h)
			{
				float value = base.fileBody.bustSoftness * base.fileBody.shapeValueBody[1] + 0.01f;
				value = Mathf.Clamp(value, 0f, 1f);
				inert = Mathf.Lerp(0.8f, 0.4f, value);
			}
			DynamicBone_Ver02 dynamicBone_Ver = null;
			dynamicBone_Ver = GetDynamicBoneBustAndHip(ChaControlDefine.DynamicBoneKind.BreastL);
			if (null != dynamicBone_Ver)
			{
				dynamicBone_Ver.setSoftParamsEx(0, -1, inert);
				dynamicBone_Ver.ResetPosition();
			}
			dynamicBone_Ver = GetDynamicBoneBustAndHip(ChaControlDefine.DynamicBoneKind.BreastR);
			if (null != dynamicBone_Ver)
			{
				dynamicBone_Ver.setSoftParamsEx(0, -1, inert);
				dynamicBone_Ver.ResetPosition();
			}
		}
	}

	public void SetSiruFlag(ChaFileDefine.SiruParts parts, byte lv)
	{
		siruNewLv[(int)parts] = lv;
	}

	public byte GetSiruFlag(ChaFileDefine.SiruParts parts)
	{
		return base.chaFile.status.siruLv[(int)parts];
	}

	private bool UpdateSiru(bool forceChange = false)
	{
		if (base.sex == 0)
		{
			return false;
		}
		float[] array = new float[3] { 0f, 0.5f, 1f };
		if (!(null == base.customMatFace))
		{
			int num = 0;
			if (forceChange || base.fileStatus.siruLv[num] != siruNewLv[num])
			{
				base.fileStatus.siruLv[num] = siruNewLv[num];
				base.customMatFace.SetFloat(ChaShader.siruFace, array[base.fileStatus.siruLv[num]]);
			}
		}
		ChaFileDefine.SiruParts[] array2 = new ChaFileDefine.SiruParts[4]
		{
			ChaFileDefine.SiruParts.SiruFrontTop,
			ChaFileDefine.SiruParts.SiruFrontBot,
			ChaFileDefine.SiruParts.SiruBackTop,
			ChaFileDefine.SiruParts.SiruBackBot
		};
		List<string> list = new List<string>();
		bool flag = false;
		for (int i = 0; i < array2.Length; i++)
		{
			if (forceChange || base.fileStatus.siruLv[(int)array2[i]] != siruNewLv[(int)array2[i]])
			{
				flag = true;
			}
			if (siruNewLv[(int)array2[i]] != 0)
			{
				string item = array2[i].ToString() + siruNewLv[(int)array2[i]].ToString("00");
				list.Add(item);
			}
			base.fileStatus.siruLv[(int)array2[i]] = siruNewLv[(int)array2[i]];
		}
		if (flag)
		{
			byte[] array3 = new byte[4]
			{
				base.fileStatus.siruLv[(int)array2[0]],
				base.fileStatus.siruLv[(int)array2[1]],
				base.fileStatus.siruLv[(int)array2[2]],
				base.fileStatus.siruLv[(int)array2[3]]
			};
			if (null != base.cmpBody.targetCustom.rendBody && 1 < base.cmpBody.targetCustom.rendBody.materials.Length)
			{
				base.cmpBody.targetCustom.rendBody.materials[1].SetFloat(ChaShader.siruFrontTop, array[array3[0]]);
				base.cmpBody.targetCustom.rendBody.materials[1].SetFloat(ChaShader.siruFrontBot, array[array3[1]]);
				base.cmpBody.targetCustom.rendBody.materials[1].SetFloat(ChaShader.siruBackTop, array[array3[2]]);
				base.cmpBody.targetCustom.rendBody.materials[1].SetFloat(ChaShader.siruBackBot, array[array3[3]]);
			}
			SetBodyBaseMaterial();
			int[] array4 = new int[5] { 0, 1, 2, 3, 5 };
			for (int j = 0; j < array4.Length; j++)
			{
				UpdateClothesSiru(j, array[array3[0]], array[array3[1]], array[array3[2]], array[array3[3]]);
			}
		}
		return true;
	}

	public void ChangeSiriAkaRate(float value)
	{
		base.fileStatus.siriAkaRate = Mathf.Clamp(value, 0f, 1f);
		if (null != base.customMatBody)
		{
			base.customMatBody.SetFloat(ChaShader.SiriAka, base.fileStatus.siriAkaRate);
		}
	}

	public void UpdateWet()
	{
		float value = base.fileStatus.wetRate;
		for (int i = 0; i < base.cmpHair.Length; i++)
		{
			if (null == base.cmpHair[i])
			{
				continue;
			}
			Renderer[] rendHair = base.cmpHair[i].rendHair;
			foreach (Renderer renderer in rendHair)
			{
				Material[] materials = renderer.materials;
				foreach (Material material in materials)
				{
					if (material.HasProperty(ChaShader.wetRate))
					{
						material.SetFloat(ChaShader.wetRate, value);
					}
				}
			}
			Renderer[] rendAccessory = base.cmpHair[i].rendAccessory;
			foreach (Renderer renderer2 in rendAccessory)
			{
				Material[] materials2 = renderer2.materials;
				foreach (Material material2 in materials2)
				{
					if (material2.HasProperty(ChaShader.wetRate))
					{
						material2.SetFloat(ChaShader.wetRate, value);
					}
				}
			}
		}
		if ((bool)base.customMatFace)
		{
			base.customMatFace.SetFloat(ChaShader.wetRate, value);
		}
		if ((bool)base.customMatBody)
		{
			base.customMatBody.SetFloat(ChaShader.wetRate, value);
		}
		for (int n = 0; n < base.cmpClothes.Length; n++)
		{
			if (null == base.cmpClothes[n])
			{
				continue;
			}
			Renderer[] rendNormal = base.cmpClothes[n].rendNormal01;
			foreach (Renderer renderer3 in rendNormal)
			{
				Material[] materials3 = renderer3.materials;
				foreach (Material material3 in materials3)
				{
					if (material3.HasProperty(ChaShader.wetRate))
					{
						material3.SetFloat(ChaShader.wetRate, value);
					}
				}
			}
			Renderer[] rendNormal2 = base.cmpClothes[n].rendNormal02;
			foreach (Renderer renderer4 in rendNormal2)
			{
				Material[] materials4 = renderer4.materials;
				foreach (Material material4 in materials4)
				{
					if (material4.HasProperty(ChaShader.wetRate))
					{
						material4.SetFloat(ChaShader.wetRate, value);
					}
				}
			}
			Renderer[] rendNormal3 = base.cmpClothes[n].rendNormal03;
			foreach (Renderer renderer5 in rendNormal3)
			{
				Material[] materials5 = renderer5.materials;
				foreach (Material material5 in materials5)
				{
					if (material5.HasProperty(ChaShader.wetRate))
					{
						material5.SetFloat(ChaShader.wetRate, value);
					}
				}
			}
		}
	}

	public void ChangeAlphaMask(params byte[] state)
	{
		if ((bool)base.customMatBody)
		{
			if (base.customMatBody.HasProperty(ChaShader.alpha_a))
			{
				base.customMatBody.SetFloat(ChaShader.alpha_a, (int)state[0]);
			}
			if (base.customMatBody.HasProperty(ChaShader.alpha_b))
			{
				base.customMatBody.SetFloat(ChaShader.alpha_b, (int)state[1]);
			}
		}
		if (base.rendBra == null)
		{
			return;
		}
		for (int i = 0; i < 1; i++)
		{
			if (!base.rendBra[i])
			{
				continue;
			}
			Material material = base.rendBra[i].material;
			if ((bool)material)
			{
				if (material.HasProperty(ChaShader.alpha_a))
				{
					material.SetFloat(ChaShader.alpha_a, (int)state[0]);
				}
				if (material.HasProperty(ChaShader.alpha_b))
				{
					material.SetFloat(ChaShader.alpha_b, (int)state[1]);
				}
			}
		}
	}

	public void ChangeAlphaMaskEx()
	{
		float value = ((nowCoordinate.clothes.parts[0].breakRate != 0f) ? 1f : 0f);
		if (null != base.customMatBody && base.customMatBody.HasProperty(ChaShader.alpha_c))
		{
			base.customMatBody.SetFloat(ChaShader.alpha_c, value);
		}
		if (base.rendBra == null)
		{
			return;
		}
		for (int i = 0; i < 1; i++)
		{
			if ((bool)base.rendBra[i])
			{
				Material material = base.rendBra[i].material;
				if (null != material && material.HasProperty(ChaShader.alpha_c))
				{
					material.SetFloat(ChaShader.alpha_c, value);
				}
			}
		}
	}

	public void ChangeAlphaMask2()
	{
		float value = 0f;
		if (underMaskReflectionType == 0)
		{
			if ((!underMaskBreakDisable || nowCoordinate.clothes.parts[0].breakRate == 0f) && base.fileStatus.clothesState[1] == 0)
			{
				value = 1f;
			}
		}
		else if (underMaskReflectionType == 1 && (!underMaskBreakDisable || nowCoordinate.clothes.parts[1].breakRate == 0f) && base.fileStatus.clothesState[1] == 0 && !notBot)
		{
			value = 1f;
		}
		if (null != base.customMatBody && base.customMatBody.HasProperty(ChaShader.alpha_d))
		{
			base.customMatBody.SetFloat(ChaShader.alpha_d, value);
		}
		if (null != base.rendInnerTB && null != base.rendInnerTB.material && base.rendInnerTB.material.HasProperty(ChaShader.alpha_d))
		{
			base.rendInnerTB.material.SetFloat(ChaShader.alpha_d, value);
		}
		if (null != base.rendInnerB && null != base.rendInnerB.material && base.rendInnerB.material.HasProperty(ChaShader.alpha_d))
		{
			base.rendInnerB.material.SetFloat(ChaShader.alpha_d, value);
		}
		if (null != base.rendPanst && null != base.rendPanst.material && base.rendPanst.material.HasProperty(ChaShader.alpha_d))
		{
			base.rendPanst.material.SetFloat(ChaShader.alpha_d, value);
		}
	}

	public void ChangeSimpleBodyDraw(bool drawSimple)
	{
	}

	public void ChangeSimpleBodyColor(Color color)
	{
		if (null == base.cmpSimpleBody || base.fileStatus.simpleColor == color)
		{
			return;
		}
		base.fileStatus.simpleColor = color;
		if ((bool)base.cmpSimpleBody.targetCustom.rendBody)
		{
			Material material = base.cmpSimpleBody.targetCustom.rendBody.material;
			if ((bool)material)
			{
				material.SetColor(ChaShader.Color, color);
			}
		}
		if ((bool)base.cmpSimpleBody.targetEtc.rendTongue)
		{
			Material material2 = base.cmpSimpleBody.targetEtc.rendTongue.material;
			if ((bool)material2)
			{
				material2.SetColor(ChaShader.Color, color);
			}
		}
	}

	private void UpdateVisible()
	{
		confSon = true;
		confBody = true;
		drawSimple = false;
		if (Singleton<Config>.IsInstance() && Singleton<HSceneManager>.IsInstance() && HSceneManager.isHScene)
		{
			confSon = (base.sex != 0 && (base.sex != 1 || !base.isPlayer)) || Config.HData.Son;
			confBody = (base.sex != 0 && (base.sex != 1 || !base.isPlayer)) || Config.HData.Visible;
			drawSimple = (base.sex == 0 || (base.sex == 1 && base.isPlayer)) && Config.GraphicData.SimpleBody;
			base.fileStatus.visibleSimple = drawSimple;
			ChangeSimpleBodyColor(Config.GraphicData.SilhouetteColor);
		}
		if ((bool)base.cmpBody)
		{
			if ((bool)base.cmpBody.targetEtc.objTongue)
			{
				lstActive.Clear();
				lstActive.Add(base.visibleAll);
				lstActive.Add(base.fileStatus.tongueState == 1);
				lstActive.Add(confBody);
				lstActive.Add(!drawSimple);
				lstActive.Add(base.fileStatus.visibleHeadAlways);
				lstActive.Add(base.fileStatus.visibleBodyAlways);
				YS_Assist.SetActiveControl(base.cmpBody.targetEtc.objTongue, lstActive);
			}
			if ((bool)base.cmpBody.targetEtc.objMNPB)
			{
				YS_Assist.SetActiveControl(base.cmpBody.targetEtc.objMNPB, !base.hideMoz);
			}
			if ((bool)base.cmpBody.targetEtc.objBody)
			{
				lstActive.Clear();
				lstActive.Add(base.visibleAll);
				lstActive.Add(confBody);
				lstActive.Add(!drawSimple);
				lstActive.Add(base.fileStatus.visibleBodyAlways);
				YS_Assist.SetActiveControl(base.cmpBody.targetEtc.objBody, lstActive);
			}
			if ((bool)base.cmpBody.targetEtc.objDanTop)
			{
				lstActive.Clear();
				lstActive.Add(base.visibleAll);
				bool flag = false;
				if (notBot && base.fileStatus.clothesState[0] == 0)
				{
					flag = true;
				}
				lstActive.Add(drawSimple || ((!IsClothesStateKind(1) || base.fileStatus.clothesState[1] != 0) && !flag) || base.fileStatus.visibleSon);
				lstActive.Add(confSon);
				lstActive.Add(base.fileStatus.visibleSonAlways);
				YS_Assist.SetActiveControl(base.cmpBody.targetEtc.objDanTop, lstActive);
			}
		}
		if ((bool)base.cmpSimpleBody)
		{
			if ((bool)base.cmpSimpleBody.targetEtc.objTongue)
			{
				lstActive.Clear();
				lstActive.Add(base.visibleAll);
				lstActive.Add(base.fileStatus.tongueState == 1);
				lstActive.Add(confBody);
				lstActive.Add(drawSimple);
				lstActive.Add(base.fileStatus.visibleHeadAlways);
				lstActive.Add(base.fileStatus.visibleBodyAlways);
				YS_Assist.SetActiveControl(base.cmpSimpleBody.targetEtc.objTongue, lstActive);
			}
			if ((bool)base.cmpSimpleBody.targetEtc.objMNPB)
			{
				YS_Assist.SetActiveControl(base.cmpSimpleBody.targetEtc.objMNPB, !base.hideMoz);
			}
			if ((bool)base.cmpSimpleBody.targetEtc.objBody)
			{
				lstActive.Clear();
				lstActive.Add(base.visibleAll);
				lstActive.Add(confBody);
				lstActive.Add(drawSimple);
				lstActive.Add(base.fileStatus.visibleBodyAlways);
				YS_Assist.SetActiveControl(base.cmpSimpleBody.targetEtc.objBody, lstActive);
			}
			if ((bool)base.cmpSimpleBody.targetEtc.objDanTop)
			{
				lstActive.Clear();
				lstActive.Add(base.visibleAll);
				lstActive.Add(drawSimple && base.fileStatus.visibleSon);
				lstActive.Add(confSon);
				lstActive.Add(base.fileStatus.visibleSonAlways);
				YS_Assist.SetActiveControl(base.cmpSimpleBody.targetEtc.objDanTop, lstActive);
			}
		}
		if ((bool)base.cmpFace)
		{
			lstActive.Clear();
			lstActive.Add(base.visibleAll);
			lstActive.Add(confBody);
			lstActive.Add(!drawSimple);
			lstActive.Add(base.fileStatus.visibleHeadAlways);
			lstActive.Add(base.fileStatus.visibleBodyAlways);
			YS_Assist.SetActiveControl(base.objHead, lstActive);
			if ((bool)base.cmpFace.targetEtc.objTongue)
			{
				YS_Assist.SetActiveControl(base.cmpFace.targetEtc.objTongue, base.fileStatus.tongueState == 0);
			}
		}
		if ((bool)base.cmpClothes[0])
		{
			lstActive.Clear();
			lstActive.Add(base.visibleAll);
			lstActive.Add(confBody);
			lstActive.Add(!drawSimple);
			lstActive.Add(2 != base.fileStatus.clothesState[0]);
			lstActive.Add(base.fileStatus.visibleBodyAlways);
			YS_Assist.SetActiveControl(base.objClothes[0], lstActive);
		}
		bool flag2 = false;
		bool flag3 = false;
		if ((bool)base.cmpClothes[0])
		{
			if (YS_Assist.SetActiveControl(base.cmpClothes[0].objTopDef, base.fileStatus.clothesState[0] == 0))
			{
				flag2 = true;
			}
			if (YS_Assist.SetActiveControl(base.cmpClothes[0].objTopHalf, base.fileStatus.clothesState[0] == 1))
			{
				flag2 = true;
			}
			lstActive.Clear();
			lstActive.Add(base.fileStatus.clothesState[1] == 0);
			lstActive.Add(base.fileStatus.clothesState[0] != 2);
			if (YS_Assist.SetActiveControl(base.cmpClothes[0].objBotDef, lstActive))
			{
				flag3 = true;
			}
			lstActive.Clear();
			lstActive.Add((base.fileStatus.clothesState[1] != 0) ? true : false);
			lstActive.Add(base.fileStatus.clothesState[0] != 2);
			if (YS_Assist.SetActiveControl(base.cmpClothes[0].objBotHalf, lstActive))
			{
				flag3 = true;
			}
		}
		DrawOption(ChaFileDefine.ClothesKind.top);
		if (flag2 || updateAlphaMask)
		{
			byte b = base.fileStatus.clothesState[0];
			byte[,] array = new byte[3, 2]
			{
				{ 1, 1 },
				{ 0, 1 },
				{ 0, 0 }
			};
			ChangeAlphaMask(array[b, 0], array[b, 1]);
			updateAlphaMask = false;
		}
		bool flag4 = false;
		if ((bool)base.cmpClothes[1])
		{
			lstActive.Clear();
			lstActive.Add(base.visibleAll);
			lstActive.Add(!notBot);
			lstActive.Add(confBody);
			lstActive.Add(!drawSimple);
			lstActive.Add(2 != base.fileStatus.clothesState[1]);
			lstActive.Add(base.fileStatus.visibleBodyAlways);
			if (YS_Assist.SetActiveControl(base.objClothes[1], lstActive))
			{
				flag4 = true;
			}
		}
		if ((bool)base.cmpClothes[1])
		{
			if (YS_Assist.SetActiveControl(base.cmpClothes[1].objBotDef, base.fileStatus.clothesState[1] == 0))
			{
				flag4 = true;
			}
			if (YS_Assist.SetActiveControl(base.cmpClothes[1].objBotHalf, base.fileStatus.clothesState[1] == 1))
			{
				flag4 = true;
			}
			lstActive.Clear();
			lstActive.Add(base.fileStatus.clothesState[0] == 0);
			lstActive.Add(base.fileStatus.clothesState[1] != 2);
			YS_Assist.SetActiveControl(base.cmpClothes[1].objTopDef, lstActive);
			lstActive.Clear();
			lstActive.Add((base.fileStatus.clothesState[0] != 0) ? true : false);
			lstActive.Add(base.fileStatus.clothesState[1] != 2);
			YS_Assist.SetActiveControl(base.cmpClothes[1].objTopHalf, lstActive);
		}
		if (flag3 || flag4 || updateAlphaMask2)
		{
			ChangeAlphaMask2();
			updateAlphaMask2 = false;
		}
		DrawOption(ChaFileDefine.ClothesKind.bot);
		if ((bool)base.cmpClothes[2])
		{
			lstActive.Clear();
			lstActive.Add(base.visibleAll);
			lstActive.Add(confBody);
			lstActive.Add(!drawSimple);
			lstActive.Add(!notInnerT);
			lstActive.Add(2 != base.fileStatus.clothesState[2]);
			lstActive.Add(base.fileStatus.visibleBodyAlways);
			YS_Assist.SetActiveControl(base.objClothes[2], lstActive);
		}
		if ((bool)base.cmpClothes[2])
		{
			YS_Assist.SetActiveControl(base.cmpClothes[2].objTopDef, base.fileStatus.clothesState[2] == 0);
			YS_Assist.SetActiveControl(base.cmpClothes[2].objTopHalf, base.fileStatus.clothesState[2] == 1);
			lstActive.Clear();
			lstActive.Add(base.fileStatus.clothesState[3] == 0);
			lstActive.Add(base.fileStatus.clothesState[2] != 2);
			YS_Assist.SetActiveControl(base.cmpClothes[2].objBotDef, lstActive);
			lstActive.Clear();
			lstActive.Add((base.fileStatus.clothesState[3] != 0) ? true : false);
			lstActive.Add(base.fileStatus.clothesState[2] != 2);
			YS_Assist.SetActiveControl(base.cmpClothes[2].objBotHalf, lstActive);
		}
		DrawOption(ChaFileDefine.ClothesKind.inner_t);
		bool item = true;
		if ((bool)base.cmpClothes[3])
		{
			lstActive.Clear();
			lstActive.Add(base.visibleAll);
			lstActive.Add(item);
			lstActive.Add(!notInnerB);
			lstActive.Add(confBody);
			lstActive.Add(!drawSimple);
			lstActive.Add(2 != base.fileStatus.clothesState[3]);
			lstActive.Add(base.fileStatus.visibleBodyAlways);
			YS_Assist.SetActiveControl(base.objClothes[3], lstActive);
		}
		if ((bool)base.cmpClothes[3])
		{
			YS_Assist.SetActiveControl(base.cmpClothes[3].objBotDef, base.fileStatus.clothesState[3] == 0);
			YS_Assist.SetActiveControl(base.cmpClothes[3].objBotHalf, base.fileStatus.clothesState[3] == 1);
		}
		DrawOption(ChaFileDefine.ClothesKind.inner_b);
		if ((bool)base.cmpClothes[4])
		{
			lstActive.Clear();
			lstActive.Add(base.visibleAll);
			lstActive.Add(base.fileStatus.clothesState[4] == 0);
			lstActive.Add(confBody);
			lstActive.Add(!drawSimple);
			lstActive.Add(base.fileStatus.visibleBodyAlways);
			YS_Assist.SetActiveControl(base.objClothes[4], lstActive);
		}
		DrawOption(ChaFileDefine.ClothesKind.gloves);
		if ((bool)base.cmpClothes[5])
		{
			lstActive.Clear();
			lstActive.Add(base.visibleAll);
			lstActive.Add(base.fileStatus.clothesState[5] != 2);
			lstActive.Add(confBody);
			lstActive.Add(!drawSimple);
			lstActive.Add(base.fileStatus.visibleBodyAlways);
			YS_Assist.SetActiveControl(base.objClothes[5], lstActive);
		}
		if ((bool)base.cmpClothes[5])
		{
			YS_Assist.SetActiveControl(base.cmpClothes[5].objBotDef, base.fileStatus.clothesState[5] == 0);
			YS_Assist.SetActiveControl(base.cmpClothes[5].objBotHalf, base.fileStatus.clothesState[5] == 1);
		}
		DrawOption(ChaFileDefine.ClothesKind.panst);
		if ((bool)base.cmpClothes[6])
		{
			lstActive.Clear();
			lstActive.Add(base.visibleAll);
			lstActive.Add(base.fileStatus.clothesState[6] == 0);
			lstActive.Add(confBody);
			lstActive.Add(!drawSimple);
			lstActive.Add(base.fileStatus.visibleBodyAlways);
			YS_Assist.SetActiveControl(base.objClothes[6], lstActive);
		}
		DrawOption(ChaFileDefine.ClothesKind.socks);
		if ((bool)base.cmpClothes[7])
		{
			lstActive.Clear();
			lstActive.Add(base.visibleAll);
			lstActive.Add(base.fileStatus.clothesState[7] == 0);
			lstActive.Add(confBody);
			lstActive.Add(!drawSimple);
			lstActive.Add(base.fileStatus.visibleBodyAlways);
			YS_Assist.SetActiveControl(base.objClothes[7], lstActive);
		}
		DrawOption(ChaFileDefine.ClothesKind.shoes);
		for (int i = 0; i < base.objAccessory.Length; i++)
		{
			if (!(null == base.objAccessory[i]))
			{
				bool flag5 = false;
				if (!base.fileStatus.visibleHeadAlways && nowCoordinate.accessory.parts[i].partsOfHead)
				{
					flag5 = true;
				}
				if (!base.fileStatus.visibleBodyAlways || !confBody)
				{
					flag5 = true;
				}
				lstActive.Clear();
				lstActive.Add(base.visibleAll);
				lstActive.Add(base.fileStatus.showAccessory[i]);
				lstActive.Add(!drawSimple);
				lstActive.Add(!flag5);
				YS_Assist.SetActiveControl(base.objAccessory[i], lstActive);
			}
		}
		for (int j = 0; j < base.objExtraAccessory.Length; j++)
		{
			if (!(null == base.objExtraAccessory[j]))
			{
				lstActive.Clear();
				lstActive.Add(base.visibleAll);
				lstActive.Add(base.showExtraAccessory[j]);
				lstActive.Add(!drawSimple);
				YS_Assist.SetActiveControl(base.objExtraAccessory[j], lstActive);
			}
		}
		GameObject[] array2 = base.objHair;
		foreach (GameObject obj in array2)
		{
			lstActive.Clear();
			lstActive.Add(base.visibleAll);
			lstActive.Add(confBody);
			lstActive.Add(!drawSimple);
			lstActive.Add(base.fileStatus.visibleHeadAlways);
			lstActive.Add(base.fileStatus.visibleBodyAlways);
			YS_Assist.SetActiveControl(obj, lstActive);
		}
	}

	public void DrawOption(ChaFileDefine.ClothesKind kind)
	{
		CmpClothes cmpClothes = base.cmpClothes[(int)kind];
		if (null == cmpClothes)
		{
			return;
		}
		if (cmpClothes.objOpt01 != null)
		{
			GameObject[] objOpt = cmpClothes.objOpt01;
			foreach (GameObject obj in objOpt)
			{
				YS_Assist.SetActiveControl(obj, !nowCoordinate.clothes.parts[(int)kind].hideOpt[0]);
			}
		}
		if (cmpClothes.objOpt02 != null)
		{
			GameObject[] objOpt2 = cmpClothes.objOpt02;
			foreach (GameObject obj2 in objOpt2)
			{
				YS_Assist.SetActiveControl(obj2, !nowCoordinate.clothes.parts[(int)kind].hideOpt[1]);
			}
		}
	}

	public bool AssignCoordinate(string path)
	{
		string path2 = ChaFileControl.ConvertCoordinateFilePath(path, base.sex);
		ChaFileCoordinate chaFileCoordinate = new ChaFileCoordinate();
		if (!chaFileCoordinate.LoadFile(path2))
		{
			return false;
		}
		return AssignCoordinate(chaFileCoordinate);
	}

	public bool AssignCoordinate(ChaFileCoordinate srcCoorde)
	{
		byte[] data = srcCoorde.SaveBytes();
		return base.chaFile.coordinate.LoadBytes(data, srcCoorde.loadVersion);
	}

	public bool AssignCoordinate()
	{
		byte[] data = nowCoordinate.SaveBytes();
		return base.chaFile.coordinate.LoadBytes(data, nowCoordinate.loadVersion);
	}

	public bool ChangeNowCoordinate(bool reload = false, bool forceChange = true)
	{
		return ChangeNowCoordinate(base.chaFile.coordinate, reload, forceChange);
	}

	public bool ChangeNowCoordinate(string path, bool reload = false, bool forceChange = true)
	{
		string path2 = ChaFileControl.ConvertCoordinateFilePath(path, base.sex);
		ChaFileCoordinate chaFileCoordinate = new ChaFileCoordinate();
		if (!chaFileCoordinate.LoadFile(path2))
		{
			return false;
		}
		return ChangeNowCoordinate(chaFileCoordinate, reload, forceChange);
	}

	public bool ChangeNowCoordinate(ChaFileCoordinate srcCoorde, bool reload = false, bool forceChange = true)
	{
		byte[] data = srcCoorde.SaveBytes();
		if (nowCoordinate.LoadBytes(data, srcCoorde.loadVersion))
		{
			if (reload)
			{
				return Reload(noChangeClothes: false, noChangeHead: true, noChangeHair: true, noChangeBody: true, forceChange);
			}
			return true;
		}
		return false;
	}

	protected void InitializeControlCoordinateAll()
	{
		nowCoordinate = new ChaFileCoordinate();
		InitializeControlCoordinateObject();
	}

	protected void InitializeControlCoordinateObject()
	{
		notInnerT = false;
		notBot = false;
		notInnerB = false;
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

	protected void ReleaseBaseCustomTextureClothes(int parts, bool createTex = true)
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
			if (base.ctCreateClothesGloss[parts, i] != null)
			{
				if (createTex)
				{
					base.ctCreateClothesGloss[parts, i].Release();
				}
				else
				{
					base.ctCreateClothesGloss[parts, i].ReleaseCreateMaterial();
				}
				base.ctCreateClothesGloss[parts, i] = null;
			}
		}
	}

	protected bool InitBaseCustomTextureClothes(int parts)
	{
		if (base.infoClothes == null)
		{
			return false;
		}
		string empty = string.Empty;
		string empty2 = string.Empty;
		string empty3 = string.Empty;
		string empty4 = string.Empty;
		string empty5 = string.Empty;
		string empty6 = string.Empty;
		ListInfoBase listInfoBase = base.infoClothes[parts];
		string info = listInfoBase.GetInfo(ChaListDefine.KeyType.MainManifest);
		string info2 = listInfoBase.GetInfo(ChaListDefine.KeyType.MainAB);
		empty = listInfoBase.GetInfo(ChaListDefine.KeyType.MainTex);
		empty2 = listInfoBase.GetInfo(ChaListDefine.KeyType.MainTex02);
		empty3 = listInfoBase.GetInfo(ChaListDefine.KeyType.MainTex03);
		empty4 = listInfoBase.GetInfo(ChaListDefine.KeyType.ColorMaskTex);
		empty5 = listInfoBase.GetInfo(ChaListDefine.KeyType.ColorMask02Tex);
		empty6 = listInfoBase.GetInfo(ChaListDefine.KeyType.ColorMask03Tex);
		Texture2D texture2D = null;
		if ("0" == empty)
		{
			return false;
		}
		texture2D = CommonLib.LoadAsset<Texture2D>(info2, empty, clone: false, info);
		if (null == texture2D)
		{
			return false;
		}
		Texture2D texture2D2 = null;
		if ("0" == empty4)
		{
			UnityEngine.Resources.UnloadAsset(texture2D);
			return false;
		}
		texture2D2 = CommonLib.LoadAsset<Texture2D>(info2, empty4, clone: false, info);
		if (null == texture2D2)
		{
			UnityEngine.Resources.UnloadAsset(texture2D);
			return false;
		}
		Texture2D texture2D3 = null;
		if ("0" != empty2)
		{
			texture2D3 = CommonLib.LoadAsset<Texture2D>(info2, empty2, clone: false, info);
		}
		Texture2D texture2D4 = null;
		if ("0" != empty5)
		{
			texture2D4 = CommonLib.LoadAsset<Texture2D>(info2, empty5, clone: false, info);
		}
		Texture2D texture2D5 = null;
		if ("0" != empty3)
		{
			texture2D5 = CommonLib.LoadAsset<Texture2D>(info2, empty3, clone: false, info);
		}
		Texture2D texture2D6 = null;
		if ("0" != empty6)
		{
			texture2D6 = CommonLib.LoadAsset<Texture2D>(info2, empty6, clone: false, info);
		}
		Texture2D[] array = new Texture2D[3] { texture2D, texture2D3, texture2D5 };
		Texture2D[] array2 = new Texture2D[3] { texture2D2, texture2D4, texture2D6 };
		for (int i = 0; i < 3; i++)
		{
			base.ctCreateClothes[parts, i] = null;
			base.ctCreateClothesGloss[parts, i] = null;
			CustomTextureCreate customTextureCreate = null;
			int num = 0;
			int num2 = 0;
			if (null != array[i])
			{
				customTextureCreate = new CustomTextureCreate(base.objRoot.transform);
				num = array[i].width;
				num2 = array[i].height;
				customTextureCreate.Initialize("abdata", "chara/mm_base.unity3d", "create_clothes", num, num2);
				customTextureCreate.SetMainTexture(array[i]);
				customTextureCreate.SetTexture(ChaShader.ColorMask, array2[i]);
				base.ctCreateClothes[parts, i] = customTextureCreate;
			}
			customTextureCreate = null;
			if (null != array[i])
			{
				customTextureCreate = new CustomTextureCreate(base.objRoot.transform);
				customTextureCreate.Initialize("abdata", "chara/mm_base.unity3d", "create_clothes detail", num, num2);
				RenderTexture active = RenderTexture.active;
				RenderTexture renderTexture = new RenderTexture(num, num2, 0, RenderTextureFormat.ARGB32);
				bool sRGBWrite = GL.sRGBWrite;
				GL.sRGBWrite = true;
				Graphics.SetRenderTarget(renderTexture);
				GL.Clear(clearDepth: false, clearColor: true, new Color(0f, 0f, 0f, 1f));
				Graphics.SetRenderTarget(null);
				GL.sRGBWrite = sRGBWrite;
				Texture2D texture2D7 = new Texture2D(num, num2, TextureFormat.ARGB32, mipChain: false);
				RenderTexture.active = renderTexture;
				texture2D7.ReadPixels(new Rect(0f, 0f, num, num2), 0, 0);
				texture2D7.Apply();
				RenderTexture.active = active;
				UnityEngine.Object.Destroy(renderTexture);
				customTextureCreate.SetMainTexture(texture2D7);
				customTextureCreate.SetTexture(ChaShader.ColorMask, array2[i]);
				base.ctCreateClothesGloss[parts, i] = customTextureCreate;
			}
		}
		return true;
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
		case ChaFileDefine.ClothesKind.shoes:
			dictStateType.Remove(7);
			AddClothesStateKindSub(7, byte.Parse(stateType));
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
			if (b4 != 0)
			{
				int num3 = 0;
				if (base.cmpClothes != null && null != base.cmpClothes[num3])
				{
					GameObject objBotDef3 = base.cmpClothes[num3].objBotDef;
					if (null != objBotDef3)
					{
						switch (b3)
						{
						case 0:
							b4 = 0;
							break;
						case 1:
							if (b4 == 2)
							{
								b4 = 1;
							}
							break;
						}
					}
				}
			}
			if (b3 != 0)
			{
				int num4 = 1;
				if (base.cmpClothes != null && (bool)base.cmpClothes[num4])
				{
					GameObject objTopDef = base.cmpClothes[num4].objTopDef;
					if (null != objTopDef)
					{
						switch (b4)
						{
						case 0:
							b3 = 0;
							break;
						case 1:
							if (b3 == 2)
							{
								b3 = 1;
							}
							break;
						}
					}
				}
			}
			AddClothesStateKindSub(0, b3);
			AddClothesStateKindSub(1, b4);
			if (clothesKind2 == ChaFileDefine.ClothesKind.top)
			{
				AddClothesStateKind(2, string.Empty);
			}
			break;
		}
		case ChaFileDefine.ClothesKind.inner_t:
		case ChaFileDefine.ClothesKind.inner_b:
		{
			dictStateType.Remove(2);
			dictStateType.Remove(3);
			byte b = 3;
			if (!notInnerT)
			{
				ListInfoBase listInfoBase = base.infoClothes[2];
				if (listInfoBase != null)
				{
					b = byte.Parse(listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
				}
			}
			byte b2 = 3;
			if (!notInnerT || !notInnerB)
			{
				ListInfoBase listInfoBase2 = base.infoClothes[3];
				if (listInfoBase2 != null)
				{
					b2 = byte.Parse(listInfoBase2.GetInfo(ChaListDefine.KeyType.StateType));
				}
			}
			if (b2 != 0)
			{
				int num = 2;
				if (base.cmpClothes != null && null != base.cmpClothes[num])
				{
					GameObject objBotDef = base.cmpClothes[num].objBotDef;
					if (null != objBotDef)
					{
						switch (b)
						{
						case 0:
							b2 = 0;
							break;
						case 1:
							if (b2 == 2)
							{
								b2 = 1;
							}
							break;
						}
					}
				}
			}
			if (b != 0 && b != 2)
			{
				int num2 = 3;
				if (base.cmpClothes != null && null != base.cmpClothes[num2])
				{
					GameObject objBotDef2 = base.cmpClothes[num2].objBotDef;
					if (null != objBotDef2)
					{
						switch (b2)
						{
						case 0:
							b = 0;
							break;
						case 1:
							if (b == 2)
							{
								b = 1;
							}
							break;
						}
					}
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
		if (!MathfEx.RangeEqualOn(0, type, 1))
		{
			return false;
		}
		Dictionary<byte, string> dictionary = new Dictionary<byte, string>();
		if (type == 0)
		{
			dictionary[0] = "着衣";
			dictionary[1] = "半脱";
			dictionary[2] = "脱衣";
		}
		else
		{
			dictionary[0] = "着衣";
			dictionary[2] = "脱衣";
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
		case ChaFileDefine.ClothesKind.shoes:
			dictStateType.Remove(7);
			break;
		case ChaFileDefine.ClothesKind.top:
		case ChaFileDefine.ClothesKind.bot:
			AddClothesStateKind(0, string.Empty);
			break;
		case ChaFileDefine.ClothesKind.inner_t:
		case ChaFileDefine.ClothesKind.inner_b:
			AddClothesStateKind(2, string.Empty);
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
				state = (byte)((state + 1) % 3);
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
				state = (byte)((state + 2) % 3);
			}
			while (b2 != state);
		}
		switch (clothesKind)
		{
		case 0:
			if (notBot)
			{
				if (base.fileStatus.clothesState[clothesKind] == 2)
				{
					base.fileStatus.clothesState[1] = 2;
				}
				else if (base.fileStatus.clothesState[1] == 2)
				{
					base.fileStatus.clothesState[1] = state;
				}
			}
			break;
		case 1:
			if (notBot)
			{
				if (base.fileStatus.clothesState[clothesKind] == 2)
				{
					base.fileStatus.clothesState[0] = 2;
				}
				else if (base.fileStatus.clothesState[0] == 2)
				{
					base.fileStatus.clothesState[0] = state;
				}
			}
			break;
		case 2:
			if (notInnerB)
			{
				if (base.fileStatus.clothesState[clothesKind] == 2)
				{
					base.fileStatus.clothesState[3] = 2;
				}
				else if (base.fileStatus.clothesState[3] == 2)
				{
					base.fileStatus.clothesState[3] = state;
				}
			}
			break;
		case 3:
			if (notInnerB)
			{
				if (base.fileStatus.clothesState[clothesKind] == 2)
				{
					base.fileStatus.clothesState[2] = 2;
				}
				else if (base.fileStatus.clothesState[2] == 2)
				{
					base.fileStatus.clothesState[2] = state;
				}
			}
			break;
		}
	}

	public void SetClothesStatePrev(int clothesKind)
	{
		byte b = base.fileStatus.clothesState[clothesKind];
		b = (byte)((b + 2) % 3);
		SetClothesState(clothesKind, b, next: false);
	}

	public void SetClothesStateNext(int clothesKind)
	{
		byte b = base.fileStatus.clothesState[clothesKind];
		b = (byte)((b + 1) % 3);
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

	public int GetNowClothesType()
	{
		int num = 0;
		int num2 = 1;
		int num3 = 2;
		int num4 = 3;
		int num5 = ((!IsClothesStateKind(num)) ? 2 : base.fileStatus.clothesState[num]);
		int num6 = ((!IsClothesStateKind(num2)) ? 2 : base.fileStatus.clothesState[num2]);
		int num7 = ((!IsClothesStateKind(num3)) ? 2 : base.fileStatus.clothesState[num3]);
		int num8 = ((!IsClothesStateKind(num4)) ? 2 : base.fileStatus.clothesState[num4]);
		bool flag = true;
		bool flag2 = true;
		if (base.infoClothes[num3] != null)
		{
			flag = base.infoClothes[num3].Kind == 1;
		}
		if (base.infoClothes[num4] != null)
		{
			flag2 = base.infoClothes[num4].Kind == 1;
		}
		if (notInnerB)
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
				return flag2 ? 1 : 2;
			}
			return 3;
		}
		if (num6 == 0)
		{
			if (num7 == 0)
			{
				return flag ? 1 : 2;
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
				return flag2 ? 1 : 2;
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
			if (IsClothes(num) && ((i != 0 && i != 2) || !("1" != base.infoClothes[num].GetInfo(ChaListDefine.KeyType.Coordinate))) && "1" == base.infoClothes[num].GetInfo(ChaListDefine.KeyType.KokanHide) && base.fileStatus.clothesState[array2[i]] == 0)
			{
				result = true;
				break;
			}
		}
		return result;
	}

	public bool ChangeCustomClothes(int kind, bool updateColor, bool updateTex01, bool updateTex02, bool updateTex03)
	{
		CustomTextureCreate[] array = new CustomTextureCreate[3]
		{
			base.ctCreateClothes[kind, 0],
			base.ctCreateClothes[kind, 1],
			base.ctCreateClothes[kind, 2]
		};
		CustomTextureCreate[] array2 = new CustomTextureCreate[3]
		{
			base.ctCreateClothesGloss[kind, 0],
			base.ctCreateClothesGloss[kind, 1],
			base.ctCreateClothesGloss[kind, 2]
		};
		if (array[0] == null)
		{
			return false;
		}
		CmpClothes customClothesComponent = GetCustomClothesComponent(kind);
		if (null == customClothesComponent)
		{
			return false;
		}
		ChaFileClothes.PartsInfo partsInfo = nowCoordinate.clothes.parts[kind];
		if (!updateColor && !updateTex01 && !updateTex02 && !updateTex03)
		{
			return false;
		}
		bool result = true;
		int[] array3 = new int[3]
		{
			ChaShader.PatternMask1,
			ChaShader.PatternMask2,
			ChaShader.PatternMask3
		};
		bool[] array4 = new bool[3] { updateTex01, updateTex02, updateTex03 };
		for (int i = 0; i < 3; i++)
		{
			if (!array4[i])
			{
				continue;
			}
			Texture2D texture2D = null;
			ListInfoBase listInfo = base.lstCtrl.GetListInfo(ChaListDefine.CategoryNo.st_pattern, partsInfo.colorInfo[i].pattern);
			if (listInfo != null)
			{
				string info = listInfo.GetInfo(ChaListDefine.KeyType.MainTexAB);
				string info2 = listInfo.GetInfo(ChaListDefine.KeyType.MainTex);
				if ("0" != info && "0" != info2)
				{
					texture2D = CommonLib.LoadAsset<Texture2D>(info, info2, clone: false, string.Empty);
					Singleton<Character>.Instance.AddLoadAssetBundle(info, "abdata");
				}
			}
			if (null != texture2D)
			{
				CustomTextureCreate[] array5 = array;
				for (int j = 0; j < array5.Length; j++)
				{
					array5[j]?.SetTexture(array3[i], texture2D);
				}
			}
			else
			{
				CustomTextureCreate[] array6 = array;
				for (int k = 0; k < array6.Length; k++)
				{
					array6[k]?.SetTexture(array3[i], null);
				}
			}
		}
		if (updateColor)
		{
			int[] array7 = new int[3]
			{
				ChaShader.Color,
				ChaShader.Color2,
				ChaShader.Color3
			};
			int[] array8 = new int[3]
			{
				ChaShader.Color1_2,
				ChaShader.Color2_2,
				ChaShader.Color3_2
			};
			int[] array9 = new int[3]
			{
				ChaShader.patternuv1,
				ChaShader.patternuv2,
				ChaShader.patternuv3
			};
			int[] array10 = new int[3]
			{
				ChaShader.patternuv1Rotator,
				ChaShader.patternuv2Rotator,
				ChaShader.patternuv3Rotator
			};
			int[] array11 = new int[3]
			{
				ChaShader.ClothesGloss1,
				ChaShader.ClothesGloss2,
				ChaShader.ClothesGloss3
			};
			int[] array12 = new int[3]
			{
				ChaShader.Metallic,
				ChaShader.Metallic2,
				ChaShader.Metallic3
			};
			bool[] array13 = new bool[3] { customClothesComponent.useColorA01, customClothesComponent.useColorA02, customClothesComponent.useColorA03 };
			Vector4 value = default(Vector4);
			for (int l = 0; l < array.Length; l++)
			{
				if (array[l] == null)
				{
					continue;
				}
				for (int m = 0; m < 3; m++)
				{
					array[l].SetVector4(ChaShader.uvScalePattern, customClothesComponent.uvScalePattern);
					if (!array13[m] && partsInfo.colorInfo[m].baseColor.a != 1f)
					{
						partsInfo.colorInfo[m].baseColor = new Color(partsInfo.colorInfo[m].baseColor.r, partsInfo.colorInfo[m].baseColor.g, partsInfo.colorInfo[m].baseColor.b, 1f);
					}
					array[l].SetColor(array7[m], partsInfo.colorInfo[m].baseColor);
					array[l].SetColor(array8[m], partsInfo.colorInfo[m].patternColor);
					value.x = Mathf.Lerp(20f, 1f, partsInfo.colorInfo[m].layout.x);
					value.y = Mathf.Lerp(20f, 1f, partsInfo.colorInfo[m].layout.y);
					value.z = Mathf.Lerp(-1f, 1f, partsInfo.colorInfo[m].layout.z);
					value.w = Mathf.Lerp(-1f, 1f, partsInfo.colorInfo[m].layout.w);
					array[l].SetVector4(array9[m], value);
					float value2 = Mathf.Lerp(-1f, 1f, partsInfo.colorInfo[m].rotation);
					array[l].SetFloat(array10[m], value2);
				}
			}
			for (int n = 0; n < array2.Length; n++)
			{
				if (array2[n] != null)
				{
					for (int num = 0; num < 3; num++)
					{
						array2[n].SetFloat(array11[num], partsInfo.colorInfo[num].glossPower);
						array2[n].SetFloat(array12[num], partsInfo.colorInfo[num].metallicPower);
					}
				}
			}
		}
		for (int num2 = 0; num2 < array.Length; num2++)
		{
			if (array[num2] != null)
			{
				array[num2].SetColor(ChaShader.Color4, customClothesComponent.defMainColor04);
			}
		}
		for (int num3 = 0; num3 < array2.Length; num3++)
		{
			if (array2[num3] != null)
			{
				array2[num3].SetFloat(ChaShader.ClothesGloss4, customClothesComponent.defGloss04);
				array2[num3].SetFloat(ChaShader.Metallic4, customClothesComponent.defMetallic04);
			}
		}
		bool[] array14 = new bool[3]
		{
			customClothesComponent.rendNormal01 != null && 0 != customClothesComponent.rendNormal01.Length,
			customClothesComponent.rendNormal02 != null && 0 != customClothesComponent.rendNormal02.Length,
			customClothesComponent.rendNormal03 != null && 0 != customClothesComponent.rendNormal03.Length
		};
		Renderer[][] array15 = new Renderer[3][] { customClothesComponent.rendNormal01, customClothesComponent.rendNormal02, customClothesComponent.rendNormal03 };
		for (int num4 = 0; num4 < 3; num4++)
		{
			if (array14[num4] && array[num4] != null)
			{
				Texture texture = array[num4].RebuildTextureAndSetMaterial();
				Texture texture2 = null;
				if (array2[num4] != null)
				{
					texture2 = array2[num4].RebuildTextureAndSetMaterial();
				}
				if (null != texture)
				{
					if (!array14[num4])
					{
						continue;
					}
					for (int num5 = 0; num5 < array15[num4].Length; num5++)
					{
						if (null != array15[num4][num5])
						{
							array15[num4][num5].material.SetTexture(ChaShader.MainTex, texture);
							if (null != texture2)
							{
								array15[num4][num5].material.SetTexture(ChaShader.DetailMainTex, texture2);
							}
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
			else
			{
				result = false;
			}
		}
		return result;
	}

	public void ChangeBreakClothes(int kind)
	{
		CmpClothes customClothesComponent = GetCustomClothesComponent(kind);
		if (null == customClothesComponent || !customClothesComponent.useBreak)
		{
			return;
		}
		ChaFileClothes.PartsInfo partsInfo = nowCoordinate.clothes.parts[kind];
		bool[] array = new bool[3]
		{
			customClothesComponent.rendNormal01 != null && 0 != customClothesComponent.rendNormal01.Length,
			customClothesComponent.rendNormal02 != null && 0 != customClothesComponent.rendNormal02.Length,
			customClothesComponent.rendNormal03 != null && 0 != customClothesComponent.rendNormal03.Length
		};
		Renderer[][] array2 = new Renderer[3][] { customClothesComponent.rendNormal01, customClothesComponent.rendNormal02, customClothesComponent.rendNormal03 };
		for (int i = 0; i < 3; i++)
		{
			if (!array[i] || !array[i])
			{
				continue;
			}
			for (int j = 0; j < array2[i].Length; j++)
			{
				if (null != array2[i][j])
				{
					array2[i][j].material.SetFloat(ChaShader.ClothesBreak, 1f - partsInfo.breakRate);
				}
			}
		}
		ChangeAlphaMaskEx();
		ChangeAlphaMask2();
	}

	public bool UpdateClothesSiru(int kind, float frontTop, float frontBot, float downTop, float downBot)
	{
		if (base.sex == 0)
		{
			return false;
		}
		if (null == base.cmpClothes[kind])
		{
			return false;
		}
		if (!base.cmpClothes[kind].rendNormal01.IsNullOrEmpty())
		{
			Renderer[] rendNormal = base.cmpClothes[kind].rendNormal01;
			foreach (Renderer renderer in rendNormal)
			{
				if (!(null == renderer))
				{
					renderer.material.SetFloat(ChaShader.siruFrontTop, frontTop);
					renderer.material.SetFloat(ChaShader.siruFrontBot, frontBot);
					renderer.material.SetFloat(ChaShader.siruBackTop, downTop);
					renderer.material.SetFloat(ChaShader.siruBackBot, downBot);
				}
			}
		}
		if (!base.cmpClothes[kind].rendNormal02.IsNullOrEmpty())
		{
			Renderer[] rendNormal2 = base.cmpClothes[kind].rendNormal02;
			foreach (Renderer renderer2 in rendNormal2)
			{
				if (!(null == renderer2))
				{
					renderer2.material.SetFloat(ChaShader.siruFrontTop, frontTop);
					renderer2.material.SetFloat(ChaShader.siruFrontBot, frontBot);
					renderer2.material.SetFloat(ChaShader.siruBackTop, downTop);
					renderer2.material.SetFloat(ChaShader.siruBackBot, downBot);
				}
			}
		}
		if (!base.cmpClothes[kind].rendNormal03.IsNullOrEmpty())
		{
			Renderer[] rendNormal3 = base.cmpClothes[kind].rendNormal03;
			foreach (Renderer renderer3 in rendNormal3)
			{
				if (!(null == renderer3))
				{
					renderer3.material.SetFloat(ChaShader.siruFrontTop, frontTop);
					renderer3.material.SetFloat(ChaShader.siruFrontBot, frontBot);
					renderer3.material.SetFloat(ChaShader.siruBackTop, downTop);
					renderer3.material.SetFloat(ChaShader.siruBackBot, downBot);
				}
			}
		}
		return true;
	}

	public ChaFileClothes.PartsInfo.ColorInfo GetClothesDefaultSetting(int kind, int no)
	{
		ChaFileClothes.PartsInfo.ColorInfo colorInfo = new ChaFileClothes.PartsInfo.ColorInfo();
		CmpClothes customClothesComponent = GetCustomClothesComponent(kind);
		if (null != customClothesComponent)
		{
			switch (no)
			{
			case 0:
			{
				colorInfo.baseColor = customClothesComponent.defMainColor01;
				colorInfo.patternColor = customClothesComponent.defPatternColor01;
				colorInfo.pattern = customClothesComponent.defPtnIndex01;
				colorInfo.glossPower = customClothesComponent.defGloss01;
				colorInfo.metallicPower = customClothesComponent.defMetallic01;
				Vector4 layout3 = default(Vector4);
				layout3.x = Mathf.InverseLerp(20f, 1f, customClothesComponent.defLayout01.x);
				layout3.y = Mathf.InverseLerp(20f, 1f, customClothesComponent.defLayout01.y);
				layout3.z = Mathf.InverseLerp(-1f, 1f, customClothesComponent.defLayout01.z);
				layout3.w = Mathf.InverseLerp(-1f, 1f, customClothesComponent.defLayout01.w);
				colorInfo.layout = layout3;
				colorInfo.rotation = Mathf.InverseLerp(-1f, 1f, customClothesComponent.defRotation01);
				break;
			}
			case 1:
			{
				colorInfo.baseColor = customClothesComponent.defMainColor02;
				colorInfo.patternColor = customClothesComponent.defPatternColor02;
				colorInfo.pattern = customClothesComponent.defPtnIndex02;
				colorInfo.glossPower = customClothesComponent.defGloss02;
				colorInfo.metallicPower = customClothesComponent.defMetallic02;
				Vector4 layout2 = default(Vector4);
				layout2.x = Mathf.InverseLerp(20f, 1f, customClothesComponent.defLayout02.x);
				layout2.y = Mathf.InverseLerp(20f, 1f, customClothesComponent.defLayout02.y);
				layout2.z = Mathf.InverseLerp(-1f, 1f, customClothesComponent.defLayout02.z);
				layout2.w = Mathf.InverseLerp(-1f, 1f, customClothesComponent.defLayout02.w);
				colorInfo.layout = layout2;
				colorInfo.rotation = Mathf.InverseLerp(-1f, 1f, customClothesComponent.defRotation02);
				break;
			}
			case 2:
			{
				colorInfo.baseColor = customClothesComponent.defMainColor03;
				colorInfo.patternColor = customClothesComponent.defPatternColor03;
				colorInfo.pattern = customClothesComponent.defPtnIndex03;
				colorInfo.glossPower = customClothesComponent.defGloss03;
				colorInfo.metallicPower = customClothesComponent.defMetallic03;
				Vector4 layout = default(Vector4);
				layout.x = Mathf.InverseLerp(20f, 1f, customClothesComponent.defLayout03.x);
				layout.y = Mathf.InverseLerp(20f, 1f, customClothesComponent.defLayout03.y);
				layout.z = Mathf.InverseLerp(-1f, 1f, customClothesComponent.defLayout03.z);
				layout.w = Mathf.InverseLerp(-1f, 1f, customClothesComponent.defLayout03.w);
				colorInfo.layout = layout;
				colorInfo.rotation = Mathf.InverseLerp(-1f, 1f, customClothesComponent.defRotation03);
				break;
			}
			}
		}
		return colorInfo;
	}

	public void SetClothesDefaultSetting(int kind)
	{
		CmpClothes customClothesComponent = GetCustomClothesComponent(kind);
		if (null != customClothesComponent)
		{
			nowCoordinate.clothes.parts[kind].colorInfo[0].baseColor = customClothesComponent.defMainColor01;
			nowCoordinate.clothes.parts[kind].colorInfo[1].baseColor = customClothesComponent.defMainColor02;
			nowCoordinate.clothes.parts[kind].colorInfo[2].baseColor = customClothesComponent.defMainColor03;
			nowCoordinate.clothes.parts[kind].colorInfo[0].patternColor = customClothesComponent.defPatternColor01;
			nowCoordinate.clothes.parts[kind].colorInfo[1].patternColor = customClothesComponent.defPatternColor02;
			nowCoordinate.clothes.parts[kind].colorInfo[2].patternColor = customClothesComponent.defPatternColor03;
			nowCoordinate.clothes.parts[kind].colorInfo[0].pattern = customClothesComponent.defPtnIndex01;
			nowCoordinate.clothes.parts[kind].colorInfo[1].pattern = customClothesComponent.defPtnIndex02;
			nowCoordinate.clothes.parts[kind].colorInfo[2].pattern = customClothesComponent.defPtnIndex03;
			nowCoordinate.clothes.parts[kind].colorInfo[0].glossPower = customClothesComponent.defGloss01;
			nowCoordinate.clothes.parts[kind].colorInfo[1].glossPower = customClothesComponent.defGloss02;
			nowCoordinate.clothes.parts[kind].colorInfo[2].glossPower = customClothesComponent.defGloss03;
			nowCoordinate.clothes.parts[kind].colorInfo[0].metallicPower = customClothesComponent.defMetallic01;
			nowCoordinate.clothes.parts[kind].colorInfo[1].metallicPower = customClothesComponent.defMetallic02;
			nowCoordinate.clothes.parts[kind].colorInfo[2].metallicPower = customClothesComponent.defMetallic03;
			Vector4 layout = default(Vector4);
			layout.x = Mathf.InverseLerp(20f, 1f, customClothesComponent.defLayout01.x);
			layout.y = Mathf.InverseLerp(20f, 1f, customClothesComponent.defLayout01.y);
			layout.z = Mathf.InverseLerp(-1f, 1f, customClothesComponent.defLayout01.z);
			layout.w = Mathf.InverseLerp(-1f, 1f, customClothesComponent.defLayout01.w);
			nowCoordinate.clothes.parts[kind].colorInfo[0].layout = layout;
			layout.x = Mathf.InverseLerp(20f, 1f, customClothesComponent.defLayout02.x);
			layout.y = Mathf.InverseLerp(20f, 1f, customClothesComponent.defLayout02.y);
			layout.z = Mathf.InverseLerp(-1f, 1f, customClothesComponent.defLayout02.z);
			layout.w = Mathf.InverseLerp(-1f, 1f, customClothesComponent.defLayout02.w);
			nowCoordinate.clothes.parts[kind].colorInfo[1].layout = layout;
			layout.x = Mathf.InverseLerp(20f, 1f, customClothesComponent.defLayout03.x);
			layout.y = Mathf.InverseLerp(20f, 1f, customClothesComponent.defLayout03.y);
			layout.z = Mathf.InverseLerp(-1f, 1f, customClothesComponent.defLayout03.z);
			layout.w = Mathf.InverseLerp(-1f, 1f, customClothesComponent.defLayout03.w);
			nowCoordinate.clothes.parts[kind].colorInfo[2].layout = layout;
			nowCoordinate.clothes.parts[kind].colorInfo[0].rotation = Mathf.InverseLerp(-1f, 1f, customClothesComponent.defRotation01);
			nowCoordinate.clothes.parts[kind].colorInfo[1].rotation = Mathf.InverseLerp(-1f, 1f, customClothesComponent.defRotation02);
			nowCoordinate.clothes.parts[kind].colorInfo[2].rotation = Mathf.InverseLerp(-1f, 1f, customClothesComponent.defRotation03);
		}
	}

	public void ResetDynamicBoneClothes(bool includeInactive = false)
	{
		if (base.cmpClothes == null)
		{
			return;
		}
		for (int i = 0; i < base.cmpClothes.Length; i++)
		{
			if (!(null == base.cmpClothes[i]))
			{
				base.cmpClothes[i].ResetDynamicBones(includeInactive);
			}
		}
	}

	protected void InitializeControlCustomBodyAll()
	{
		ShapeBodyNum = ChaFileDefine.cf_bodyshapename.Length;
		InitializeControlCustomBodyObject();
	}

	protected void InitializeControlCustomBodyObject()
	{
		sibBody = new ShapeBodyInfoFemale();
		changeShapeBodyMask = false;
		bustSoft = new BustSoft(this);
		bustGravity = new BustGravity(this);
		int num = Enum.GetNames(typeof(BodyTexKind)).Length;
		updateCMBodyTex = new bool[num];
		updateCMBodyColor = new bool[num];
		updateCMBodyGloss = new bool[num];
		updateCMBodyLayout = new bool[num];
	}

	protected void ReleaseControlCustomBodyAll()
	{
		ReleaseControlCustomBodyObject();
	}

	protected void ReleaseControlCustomBodyObject(bool init = true)
	{
		if (sibBody != null)
		{
			sibBody.ReleaseShapeInfo();
		}
		if (init)
		{
			InitializeControlCustomBodyObject();
		}
	}

	public void AddUpdateCMBodyTexFlags(bool inpBase, bool inpPaint01, bool inpPaint02, bool inpSunburn)
	{
		if (inpBase)
		{
			updateCMBodyTex[0] = inpBase;
		}
		if (inpPaint01)
		{
			updateCMBodyTex[1] = inpPaint01;
		}
		if (inpPaint02)
		{
			updateCMBodyTex[2] = inpPaint02;
		}
		if (inpSunburn)
		{
			updateCMBodyTex[3] = inpSunburn;
		}
	}

	public void AddUpdateCMBodyColorFlags(bool inpBase, bool inpPaint01, bool inpPaint02, bool inpSunburn)
	{
		if (inpBase)
		{
			updateCMBodyColor[0] = inpBase;
		}
		if (inpPaint01)
		{
			updateCMBodyColor[1] = inpPaint01;
		}
		if (inpPaint02)
		{
			updateCMBodyColor[2] = inpPaint02;
		}
		if (inpSunburn)
		{
			updateCMBodyColor[3] = inpSunburn;
		}
	}

	public void AddUpdateCMBodyGlossFlags(bool inpPaint01, bool inpPaint02)
	{
		if (inpPaint01)
		{
			updateCMBodyGloss[1] = inpPaint01;
		}
		if (inpPaint02)
		{
			updateCMBodyGloss[2] = inpPaint02;
		}
	}

	public void AddUpdateCMBodyLayoutFlags(bool inpPaint01, bool inpPaint02)
	{
		if (inpPaint01)
		{
			updateCMBodyLayout[1] = inpPaint01;
		}
		if (inpPaint02)
		{
			updateCMBodyLayout[2] = inpPaint02;
		}
	}

	protected bool InitBaseCustomTextureBody()
	{
		if (base.customTexCtrlBody != null)
		{
			base.customTexCtrlBody.Release();
			base.customTexCtrlBody = null;
		}
		string drawMatName = ChaABDefine.BodyMaterialAsset(base.sex);
		base.customTexCtrlBody = new CustomTextureControl(2, "abdata", "chara/mm_base.unity3d", drawMatName, base.objRoot.transform);
		base.customTexCtrlBody.Initialize(0, "abdata", "chara/mm_base.unity3d", "create_skin_body", 4096, 4096);
		base.customTexCtrlBody.Initialize(1, "abdata", "chara/mm_base.unity3d", "create_skin detail_body", 4096, 4096);
		return true;
	}

	public bool CreateBodyTexture()
	{
		bool flag = false;
		bool flag2 = false;
		CustomTextureCreate customTextureCreate = base.customTexCtrlBody.createCustomTex[0];
		CustomTextureCreate customTextureCreate2 = base.customTexCtrlBody.createCustomTex[1];
		if (updateCMBodyTex[0])
		{
			if (SetCreateTexture(customTextureCreate, main: true, (base.sex != 0) ? ChaListDefine.CategoryNo.ft_skin_b : ChaListDefine.CategoryNo.mt_skin_b, base.fileBody.skinId, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.MainTex, -1))
			{
				flag = true;
			}
			Texture2D texture2D = CommonLib.LoadAsset<Texture2D>("chara/etc.unity3d", "black4096", clone: false, "abdata");
			Singleton<Character>.Instance.AddLoadAssetBundle("chara/etc.unity3d", "abdata");
			if (null != texture2D)
			{
				customTextureCreate2.SetMainTexture(texture2D);
				flag2 = true;
			}
			ChangeTexture(base.customTexCtrlBody.matDraw, (base.sex != 0) ? ChaListDefine.CategoryNo.ft_detail_b : ChaListDefine.CategoryNo.mt_detail_b, base.fileBody.detailId, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.OcclusionMapTex, ChaShader.SkinOcclusionMapTex, string.Empty);
			ChangeTexture(base.customTexCtrlBody.matDraw, (base.sex != 0) ? ChaListDefine.CategoryNo.ft_detail_b : ChaListDefine.CategoryNo.mt_detail_b, base.fileBody.detailId, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.NormalMapTex, ChaShader.SkinDetailTex, string.Empty);
			updateCMBodyTex[0] = false;
		}
		if (updateCMBodyColor[0])
		{
			customTextureCreate.SetColor(ChaShader.SkinColor, base.fileBody.skinColor);
			flag = true;
			updateCMBodyColor[0] = false;
		}
		if (updateCMBodyTex[3])
		{
			if (SetCreateTexture(customTextureCreate, main: false, (base.sex != 0) ? ChaListDefine.CategoryNo.ft_sunburn : ChaListDefine.CategoryNo.mt_sunburn, base.fileBody.sunburnId, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.AddTex, ChaShader.SunburnTex))
			{
				flag = true;
			}
			updateCMBodyTex[3] = false;
		}
		if (updateCMBodyColor[3])
		{
			customTextureCreate.SetColor(ChaShader.SunburnColor, base.fileBody.sunburnColor);
			updateCMBodyColor[3] = false;
			flag = true;
		}
		int[] array = new int[2] { 1, 2 };
		int[] array2 = new int[2]
		{
			ChaShader.Paint01Tex,
			ChaShader.Paint02Tex
		};
		int[] array3 = new int[2]
		{
			ChaShader.Paint01Color,
			ChaShader.Paint02Color
		};
		int[] array4 = new int[2]
		{
			ChaShader.Paint01Gloass,
			ChaShader.Paint02Gloass
		};
		int[] array5 = new int[2]
		{
			ChaShader.Paint01Metallic,
			ChaShader.Paint02Metallic
		};
		int[] array6 = new int[2]
		{
			ChaShader.Paint01Layout,
			ChaShader.Paint02Layout
		};
		int[] array7 = new int[2]
		{
			ChaShader.Paint01Rot,
			ChaShader.Paint02Rot
		};
		Vector4 value = default(Vector4);
		for (int i = 0; i < 2; i++)
		{
			if (updateCMBodyTex[array[i]])
			{
				if (SetCreateTexture(customTextureCreate, main: false, ChaListDefine.CategoryNo.st_paint, base.fileBody.paintInfo[i].id, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.AddTex, array2[i]))
				{
					flag = true;
				}
				if (SetCreateTexture(customTextureCreate2, main: false, ChaListDefine.CategoryNo.st_paint, base.fileBody.paintInfo[i].id, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.GlossTex, array2[i]))
				{
					flag2 = true;
				}
				updateCMBodyTex[array[i]] = false;
			}
			if (updateCMBodyColor[array[i]])
			{
				customTextureCreate.SetColor(array3[i], base.fileBody.paintInfo[i].color);
				updateCMBodyColor[array[i]] = false;
				flag = true;
			}
			if (updateCMBodyGloss[array[i]])
			{
				customTextureCreate2.SetFloat(array4[i], base.fileBody.paintInfo[i].glossPower);
				customTextureCreate2.SetFloat(array5[i], base.fileBody.paintInfo[i].metallicPower);
				updateCMBodyGloss[array[i]] = false;
				flag2 = true;
			}
			ListInfoBase listInfo = base.lstCtrl.GetListInfo(ChaListDefine.CategoryNo.bodypaint_layout, base.fileBody.paintInfo[i].layoutId);
			if (listInfo != null)
			{
				float a = listInfo.GetInfoFloat(ChaListDefine.KeyType.CenterX) + listInfo.GetInfoFloat(ChaListDefine.KeyType.MoveX);
				float b = listInfo.GetInfoFloat(ChaListDefine.KeyType.CenterX) - listInfo.GetInfoFloat(ChaListDefine.KeyType.MoveX);
				float a2 = listInfo.GetInfoFloat(ChaListDefine.KeyType.CenterY) + listInfo.GetInfoFloat(ChaListDefine.KeyType.MoveY);
				float b2 = listInfo.GetInfoFloat(ChaListDefine.KeyType.CenterY) - listInfo.GetInfoFloat(ChaListDefine.KeyType.MoveY);
				float a3 = listInfo.GetInfoFloat(ChaListDefine.KeyType.CenterScale) + listInfo.GetInfoFloat(ChaListDefine.KeyType.AddScale);
				float b3 = listInfo.GetInfoFloat(ChaListDefine.KeyType.CenterScale) - listInfo.GetInfoFloat(ChaListDefine.KeyType.AddScale);
				value.x = Mathf.Lerp(a3, b3, base.fileBody.paintInfo[i].layout.x);
				value.y = Mathf.Lerp(a3, b3, base.fileBody.paintInfo[i].layout.y);
				value.z = Mathf.Lerp(a, b, base.fileBody.paintInfo[i].layout.z);
				value.w = Mathf.Lerp(a2, b2, base.fileBody.paintInfo[i].layout.w);
				float value2 = Mathf.Lerp(1f, -1f, base.fileBody.paintInfo[i].rotation);
				customTextureCreate.SetVector4(array6[i], value);
				customTextureCreate.SetFloat(array7[i], value2);
				customTextureCreate2.SetVector4(array6[i], value);
				customTextureCreate2.SetFloat(array7[i], value2);
				updateCMBodyLayout[array[i]] = false;
				flag = true;
				flag2 = true;
			}
		}
		if (flag)
		{
			base.customTexCtrlBody.SetNewCreateTexture(0, ChaShader.SkinTex);
		}
		if (flag2)
		{
			base.customTexCtrlBody.SetNewCreateTexture(1, ChaShader.SkinCreateDetailTex);
		}
		if (base.releaseCustomInputTexture)
		{
			ReleaseBodyCustomTexture();
		}
		return true;
	}

	public bool ChangeBodyDetailPower()
	{
		base.customTexCtrlBody.matDraw.SetFloat(ChaShader.SkinDetailPower, base.fileBody.detailPower);
		return true;
	}

	public bool ChangeBodyGlossPower()
	{
		float value = Mathf.Lerp(0f, 0.8f, base.fileBody.skinGlossPower) + 0.2f * base.fileStatus.skinTuyaRate;
		base.customTexCtrlBody.matDraw.SetFloat(ChaShader.Gloss, value);
		return true;
	}

	public bool ChangeBodyMetallicPower()
	{
		base.customTexCtrlBody.matDraw.SetFloat(ChaShader.Metallic, base.fileBody.skinMetallicPower);
		return true;
	}

	public bool ChangeNipKind()
	{
		ChangeTexture(base.customTexCtrlBody.matDraw, ChaListDefine.CategoryNo.st_nip, base.fileBody.nipId, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.AddTex, ChaShader.NipTex, string.Empty);
		return true;
	}

	public bool ChangeNipColor()
	{
		base.customTexCtrlBody.matDraw.SetColor(ChaShader.NipColor, base.fileBody.nipColor);
		return true;
	}

	public bool ChangeNipGloss()
	{
		base.customTexCtrlBody.matDraw.SetFloat(ChaShader.NipGloss, base.fileBody.nipGlossPower);
		return true;
	}

	public bool ChangeNipScale()
	{
		base.customTexCtrlBody.matDraw.SetFloat(ChaShader.NipScale, base.fileBody.areolaSize);
		return true;
	}

	public bool ChangeUnderHairKind()
	{
		ChangeTexture(base.customTexCtrlBody.matDraw, ChaListDefine.CategoryNo.st_underhair, base.fileBody.underhairId, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.AddTex, ChaShader.UnderhairTex, string.Empty);
		return true;
	}

	public bool ChangeUnderHairColor()
	{
		base.customTexCtrlBody.matDraw.SetColor(ChaShader.UnderhairColor, base.fileBody.underhairColor);
		return true;
	}

	public bool ChangeNailColor()
	{
		base.customTexCtrlBody.matDraw.SetColor(ChaShader.NailColor, base.fileBody.nailColor);
		return true;
	}

	public bool ChangeNailGloss()
	{
		base.customTexCtrlBody.matDraw.SetFloat(ChaShader.NailGloss, base.fileBody.nailGlossPower);
		return true;
	}

	public bool SetBodyBaseMaterial()
	{
		if (null == base.customMatBody)
		{
			return false;
		}
		if (null == base.cmpBody)
		{
			return false;
		}
		Renderer rendBody = base.cmpBody.targetCustom.rendBody;
		if (null == rendBody)
		{
			return false;
		}
		return SetBaseMaterial(rendBody, base.customMatBody);
	}

	public bool ReleaseBodyCustomTexture()
	{
		if (base.customTexCtrlBody == null)
		{
			return false;
		}
		CustomTextureCreate customTextureCreate = base.customTexCtrlBody.createCustomTex[0];
		CustomTextureCreate customTextureCreate2 = base.customTexCtrlBody.createCustomTex[1];
		customTextureCreate.SetTexture(ChaShader.MainTex, null);
		customTextureCreate.SetTexture(ChaShader.SunburnTex, null);
		customTextureCreate.SetTexture(ChaShader.Paint01Tex, null);
		customTextureCreate.SetTexture(ChaShader.Paint02Tex, null);
		customTextureCreate2.SetTexture(ChaShader.MainTex, null);
		customTextureCreate2.SetTexture(ChaShader.SunburnTex, null);
		customTextureCreate2.SetTexture(ChaShader.Paint01Tex, null);
		customTextureCreate2.SetTexture(ChaShader.Paint02Tex, null);
		OutputLog.Log("ReleaseBodyCustomTexture", unityLog: false, "UnloadUnusedAssets");
		UnityEngine.Resources.UnloadUnusedAssets();
		return true;
	}

	public void ChangeCustomBodyWithoutCustomTexture()
	{
		ChangeBodyGlossPower();
		ChangeBodyMetallicPower();
		ChangeBodyDetailPower();
		ChangeNipKind();
		ChangeNipColor();
		ChangeNipGloss();
		ChangeNipScale();
		ChangeUnderHairKind();
		ChangeUnderHairColor();
		ChangeNailColor();
		ChangeNailGloss();
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
		sibBody.InitShapeInfo("abdata", "list/customshape.unity3d", "list/customshape.unity3d", "cf_anmShapeBody", "cf_custombody", trfBone);
		float[] array = new float[base.fileBody.shapeValueBody.Length];
		base.chaFile.custom.body.shapeValueBody.CopyTo(array, 0);
		array[32] = Mathf.Lerp(0f, 0.8f, array[32]) + 0.2f * base.fileStatus.nipStandRate;
		if (base.sex == 0 || base.isPlayer)
		{
			array[0] = 0.75f;
		}
		for (int i = 0; i < ShapeBodyNum; i++)
		{
			sibBody.ChangeValue(i, array[i]);
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
		float value2 = (base.fileBody.shapeValueBody[index] = value);
		if (index == 32)
		{
			value2 = Mathf.Lerp(0f, 0.8f, value) + 0.2f * base.fileStatus.nipStandRate;
		}
		if (index == 0 && (base.sex == 0 || base.isPlayer))
		{
			value2 = 0.75f;
		}
		if (sibBody != null && sibBody.InitEnd)
		{
			sibBody.ChangeValue(index, value2);
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
		float[] array = new float[base.fileBody.shapeValueBody.Length];
		base.fileBody.shapeValueBody.CopyTo(array, 0);
		array[32] = Mathf.Lerp(0f, 0.8f, array[32]) + 0.2f * base.fileStatus.nipStandRate;
		if (base.sex == 0 || base.isPlayer)
		{
			array[0] = 0.75f;
		}
		for (int i = 0; i < base.fileBody.shapeValueBody.Length; i++)
		{
			sibBody.ChangeValue(i, array[i]);
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
		return base.fileBody.shapeValueBody[index];
	}

	public void UpdateShapeBody()
	{
		if (sibBody == null || !sibBody.InitEnd || !(sibBody is ShapeBodyInfoFemale shapeBodyInfoFemale))
		{
			return;
		}
		shapeBodyInfoFemale.updateMask = 31;
		sibBody.Update();
		if (!changeShapeBodyMask)
		{
			return;
		}
		float[] array = new float[ChaFileDefine.cf_BustShapeMaskID.Length];
		int num = 0;
		int[] array2 = new int[2] { 1, 2 };
		float[] array3 = new float[8] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f };
		for (int i = 0; i < 2; i++)
		{
			for (int j = 0; j < ChaFileDefine.cf_BustShapeMaskID.Length; j++)
			{
				num = ChaFileDefine.cf_BustShapeMaskID[j];
				array[j] = ((!base.fileStatus.disableBustShapeMask[i, j]) ? base.fileBody.shapeValueBody[num] : array3[j]);
			}
			int num2 = 7;
			num = ChaFileDefine.cf_BustShapeMaskID[num2];
			array[num2] = ((!base.fileStatus.disableBustShapeMask[i, num2]) ? (Mathf.Lerp(0f, 0.8f, base.fileBody.shapeValueBody[num]) + 0.2f * base.fileStatus.nipStandRate) : 0.5f);
			for (int k = 0; k < ChaFileDefine.cf_BustShapeMaskID.Length; k++)
			{
				sibBody.ChangeValue(ChaFileDefine.cf_BustShapeMaskID[k], array[k]);
			}
			shapeBodyInfoFemale.updateMask = array2[i];
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
			sibBody.ForceUpdate();
		}
	}

	public void DisableShapeBodyID(int LR, int id, bool disable)
	{
		if (sibBody != null && sibBody.InitEnd && id < ChaFileDefine.cf_BustShapeMaskID.Length)
		{
			changeShapeBodyMask = true;
			base.updateShapeBody = true;
			switch (LR)
			{
			case 0:
				base.fileStatus.disableBustShapeMask[0, id] = disable;
				break;
			case 1:
				base.fileStatus.disableBustShapeMask[1, id] = disable;
				break;
			default:
				base.fileStatus.disableBustShapeMask[0, id] = disable;
				base.fileStatus.disableBustShapeMask[1, id] = disable;
				break;
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
		switch (LR)
		{
		case 0:
		{
			for (int k = 0; k < ChaFileDefine.cf_ShapeMaskBust.Length; k++)
			{
				base.fileStatus.disableBustShapeMask[0, ChaFileDefine.cf_ShapeMaskBust[k]] = disable;
			}
			break;
		}
		case 1:
		{
			for (int l = 0; l < ChaFileDefine.cf_ShapeMaskBust.Length; l++)
			{
				base.fileStatus.disableBustShapeMask[1, ChaFileDefine.cf_ShapeMaskBust[l]] = disable;
			}
			break;
		}
		default:
		{
			for (int i = 0; i < 2; i++)
			{
				for (int j = 0; j < ChaFileDefine.cf_ShapeMaskBust.Length; j++)
				{
					base.fileStatus.disableBustShapeMask[i, ChaFileDefine.cf_ShapeMaskBust[j]] = disable;
				}
			}
			break;
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
		switch (LR)
		{
		case 0:
		{
			for (int k = 0; k < ChaFileDefine.cf_ShapeMaskNip.Length; k++)
			{
				base.fileStatus.disableBustShapeMask[0, ChaFileDefine.cf_ShapeMaskNip[k]] = disable;
			}
			break;
		}
		case 1:
		{
			for (int l = 0; l < ChaFileDefine.cf_ShapeMaskNip.Length; l++)
			{
				base.fileStatus.disableBustShapeMask[1, ChaFileDefine.cf_ShapeMaskNip[l]] = disable;
			}
			break;
		}
		default:
		{
			for (int i = 0; i < 2; i++)
			{
				for (int j = 0; j < ChaFileDefine.cf_ShapeMaskNip.Length; j++)
				{
					base.fileStatus.disableBustShapeMask[i, ChaFileDefine.cf_ShapeMaskNip[j]] = disable;
				}
			}
			break;
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

	protected void InitializeControlCustomFaceAll()
	{
		ShapeFaceNum = ChaFileDefine.cf_headshapename.Length;
		InitializeControlCustomFaceObject();
	}

	protected void InitializeControlCustomFaceObject()
	{
		sibFace = new ShapeHeadInfoFemale();
		int num = Enum.GetNames(typeof(FaceTexKind)).Length;
		updateCMFaceTex = new bool[num];
		updateCMFaceColor = new bool[num];
		updateCMFaceGloss = new bool[num];
		updateCMFaceLayout = new bool[num];
	}

	protected void ReleaseControlCustomFaceAll()
	{
		ReleaseControlCustomFaceObject();
	}

	protected void ReleaseControlCustomFaceObject(bool init = true)
	{
		if (sibFace != null)
		{
			sibFace.ReleaseShapeInfo();
		}
		if (init)
		{
			InitializeControlCustomFaceObject();
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

	private bool SetCreateTexture(CustomTextureCreate ctc, bool main, ChaListDefine.CategoryNo type, int id, ChaListDefine.KeyType manifestKey, ChaListDefine.KeyType assetBundleKey, ChaListDefine.KeyType assetKey, int propertyID)
	{
		ListInfoBase listInfo = base.lstCtrl.GetListInfo(type, id);
		if (listInfo != null)
		{
			string text = listInfo.GetInfo(manifestKey);
			if ("0" == text)
			{
				text = string.Empty;
			}
			string info = listInfo.GetInfo(assetBundleKey);
			string info2 = listInfo.GetInfo(assetKey);
			Texture2D texture2D = null;
			if ("0" != info && "0" != info2)
			{
				texture2D = CommonLib.LoadAsset<Texture2D>(info, info2, clone: false, text);
				Singleton<Character>.Instance.AddLoadAssetBundle(info, text);
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
		return false;
	}

	private void ChangeTexture(Renderer rend, ChaListDefine.CategoryNo type, int id, ChaListDefine.KeyType manifestKey, ChaListDefine.KeyType assetBundleKey, ChaListDefine.KeyType assetKey, int propertyID, string addStr = "")
	{
		if (null == rend)
		{
			OutputLog.Warning("レンダラーが見つからない。", unityLog: true, "ChaCustom");
		}
		else
		{
			ChangeTexture(rend.material, type, id, manifestKey, assetBundleKey, assetKey, propertyID, addStr);
		}
	}

	private void ChangeTexture(Material mat, ChaListDefine.CategoryNo type, int id, ChaListDefine.KeyType manifestKey, ChaListDefine.KeyType assetBundleKey, ChaListDefine.KeyType assetKey, int propertyID, string addStr = "")
	{
		if (null == mat)
		{
			OutputLog.Warning("マテリアルが見つからない。", unityLog: true, "ChaCustom");
			return;
		}
		Texture2D texture = GetTexture(type, id, manifestKey, assetBundleKey, assetKey, addStr);
		mat.SetTexture(propertyID, texture);
	}

	private Texture2D GetTexture(ChaListDefine.CategoryNo type, int id, ChaListDefine.KeyType manifestKey, ChaListDefine.KeyType assetBundleKey, ChaListDefine.KeyType assetKey, string addStr = "")
	{
		ListInfoBase listInfo = base.lstCtrl.GetListInfo(type, id);
		if (listInfo != null)
		{
			string text = listInfo.GetInfo(manifestKey);
			if ("0" == text)
			{
				text = string.Empty;
			}
			string info = listInfo.GetInfo(assetBundleKey);
			string info2 = listInfo.GetInfo(assetKey);
			Texture2D texture2D = null;
			if ("0" != info && "0" != info2)
			{
				if (!addStr.IsNullOrEmpty())
				{
					string assetName = info2 + addStr;
					texture2D = CommonLib.LoadAsset<Texture2D>(info, assetName, clone: false, text);
				}
				if (null == texture2D)
				{
					texture2D = CommonLib.LoadAsset<Texture2D>(info, info2, clone: false, text);
				}
				Singleton<Character>.Instance.AddLoadAssetBundle(info, text);
			}
			return texture2D;
		}
		OutputLog.Warning("テクスチャの情報が見つからない。", unityLog: true, "ChaCustom");
		return null;
	}

	public void AddUpdateCMFaceTexFlags(bool inpBase, bool inpEyeshadow, bool inpPaint01, bool inpPaint02, bool inpCheek, bool inpLip, bool inpMole)
	{
		if (inpBase)
		{
			updateCMFaceTex[0] = inpBase;
		}
		if (inpEyeshadow)
		{
			updateCMFaceTex[1] = inpEyeshadow;
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
		if (inpLip)
		{
			updateCMFaceTex[5] = inpLip;
		}
		if (inpMole)
		{
			updateCMFaceTex[6] = inpMole;
		}
	}

	public void AddUpdateCMFaceColorFlags(bool inpBase, bool inpEyeshadow, bool inpPaint01, bool inpPaint02, bool inpCheek, bool inpLip, bool inpMole)
	{
		if (inpBase)
		{
			updateCMFaceColor[0] = inpBase;
		}
		if (inpEyeshadow)
		{
			updateCMFaceColor[1] = inpEyeshadow;
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
		if (inpLip)
		{
			updateCMFaceColor[5] = inpLip;
		}
		if (inpMole)
		{
			updateCMFaceColor[6] = inpMole;
		}
	}

	public void AddUpdateCMFaceGlossFlags(bool inpEyeshadow, bool inpPaint01, bool inpPaint02, bool inpCheek, bool inpLip)
	{
		if (inpEyeshadow)
		{
			updateCMFaceGloss[1] = inpEyeshadow;
		}
		if (inpPaint01)
		{
			updateCMFaceGloss[2] = inpPaint01;
		}
		if (inpPaint02)
		{
			updateCMFaceGloss[3] = inpPaint02;
		}
		if (inpCheek)
		{
			updateCMFaceGloss[4] = inpCheek;
		}
		if (inpLip)
		{
			updateCMFaceGloss[5] = inpLip;
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

	private bool InitBaseCustomTextureFace(string drawManifest, string drawAssetBundleName, string drawAssetName)
	{
		if (base.customTexCtrlFace != null)
		{
			base.customTexCtrlFace.Release();
			base.customTexCtrlFace = null;
		}
		base.customTexCtrlFace = new CustomTextureControl(2, drawManifest, drawAssetBundleName, drawAssetName, base.objRoot.transform);
		base.customTexCtrlFace.Initialize(0, "abdata", "chara/mm_base.unity3d", "create_skin_face", 2048, 2048);
		base.customTexCtrlFace.Initialize(1, "abdata", "chara/mm_base.unity3d", "create_skin detail_face", 2048, 2048);
		return true;
	}

	public bool CreateFaceTexture()
	{
		ChaFileFace.MakeupInfo makeup = base.fileFace.makeup;
		bool flag = false;
		bool flag2 = false;
		CustomTextureCreate customTextureCreate = base.customTexCtrlFace.createCustomTex[0];
		CustomTextureCreate customTextureCreate2 = base.customTexCtrlFace.createCustomTex[1];
		if (updateCMFaceTex[0])
		{
			if (SetCreateTexture(customTextureCreate, main: true, (base.sex != 0) ? ChaListDefine.CategoryNo.ft_skin_f : ChaListDefine.CategoryNo.mt_skin_f, base.fileFace.skinId, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.MainTex, -1))
			{
				flag = true;
			}
			Texture2D texture2D = CommonLib.LoadAsset<Texture2D>("chara/etc.unity3d", "black2048", clone: false, "abdata");
			Singleton<Character>.Instance.AddLoadAssetBundle("chara/etc.unity3d", "abdata");
			if (null != texture2D)
			{
				customTextureCreate2.SetMainTexture(texture2D);
				flag2 = true;
			}
			ChangeTexture(base.customTexCtrlFace.matDraw, (base.sex != 0) ? ChaListDefine.CategoryNo.ft_skin_f : ChaListDefine.CategoryNo.mt_skin_f, base.fileFace.skinId, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.OcclusionMapTex, ChaShader.SkinOcclusionMapTex, string.Empty);
			ChangeTexture(base.customTexCtrlFace.matDraw, (base.sex != 0) ? ChaListDefine.CategoryNo.ft_skin_f : ChaListDefine.CategoryNo.mt_skin_f, base.fileFace.skinId, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.NormalMapTex, ChaShader.SkinNormalMapTex, string.Empty);
			updateCMFaceTex[0] = false;
		}
		if (updateCMFaceColor[0])
		{
			customTextureCreate.SetColor(ChaShader.SkinColor, base.fileBody.skinColor);
			flag = true;
			updateCMFaceColor[0] = false;
		}
		if (updateCMFaceTex[1])
		{
			if (SetCreateTexture(customTextureCreate, main: false, ChaListDefine.CategoryNo.st_eyeshadow, makeup.eyeshadowId, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.AddTex, ChaShader.EyeshadowTex))
			{
				flag = true;
			}
			if (SetCreateTexture(customTextureCreate2, main: false, ChaListDefine.CategoryNo.st_eyeshadow, makeup.eyeshadowId, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.GlossTex, ChaShader.EyeshadowTex))
			{
				flag2 = true;
			}
			updateCMFaceTex[1] = false;
		}
		if (updateCMFaceColor[1])
		{
			customTextureCreate.SetColor(ChaShader.EyeshadowColor, makeup.eyeshadowColor);
			flag = true;
			updateCMFaceColor[1] = false;
		}
		if (updateCMFaceGloss[1])
		{
			customTextureCreate2.SetFloat(ChaShader.EyeshadowGloss, makeup.eyeshadowGloss);
			flag2 = true;
			updateCMFaceGloss[1] = false;
		}
		int[] array = new int[2] { 2, 3 };
		int[] array2 = new int[2]
		{
			ChaShader.Paint01Tex,
			ChaShader.Paint02Tex
		};
		int[] array3 = new int[2]
		{
			ChaShader.Paint01Color,
			ChaShader.Paint02Color
		};
		int[] array4 = new int[2]
		{
			ChaShader.Paint01Gloass,
			ChaShader.Paint02Gloass
		};
		int[] array5 = new int[2]
		{
			ChaShader.Paint01Metallic,
			ChaShader.Paint02Metallic
		};
		int[] array6 = new int[2]
		{
			ChaShader.Paint01Layout,
			ChaShader.Paint02Layout
		};
		int[] array7 = new int[2]
		{
			ChaShader.Paint01Rot,
			ChaShader.Paint02Rot
		};
		for (int i = 0; i < 2; i++)
		{
			if (updateCMFaceTex[array[i]])
			{
				if (SetCreateTexture(customTextureCreate, main: false, ChaListDefine.CategoryNo.st_paint, makeup.paintInfo[i].id, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.AddTex, array2[i]))
				{
					flag = true;
				}
				if (SetCreateTexture(customTextureCreate2, main: false, ChaListDefine.CategoryNo.st_paint, makeup.paintInfo[i].id, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.GlossTex, array2[i]))
				{
					flag2 = true;
				}
				updateCMFaceTex[array[i]] = false;
			}
			if (updateCMFaceColor[array[i]])
			{
				customTextureCreate.SetColor(array3[i], makeup.paintInfo[i].color);
				updateCMFaceColor[array[i]] = false;
				flag = true;
			}
			if (updateCMFaceGloss[array[i]])
			{
				customTextureCreate2.SetFloat(array4[i], makeup.paintInfo[i].glossPower);
				customTextureCreate2.SetFloat(array5[i], makeup.paintInfo[i].metallicPower);
				updateCMFaceGloss[array[i]] = false;
				flag2 = true;
			}
			if (updateCMFaceLayout[array[i]])
			{
				Vector4 zero = Vector4.zero;
				zero.x = Mathf.Lerp(10f, 1f, makeup.paintInfo[i].layout.x);
				zero.y = Mathf.Lerp(10f, 1f, makeup.paintInfo[i].layout.y);
				zero.z = Mathf.Lerp(0.28f, -0.3f, makeup.paintInfo[i].layout.z);
				zero.w = Mathf.Lerp(0.28f, -0.3f, makeup.paintInfo[i].layout.w);
				float value = Mathf.Lerp(1f, -1f, makeup.paintInfo[i].rotation);
				customTextureCreate.SetVector4(array6[i], zero);
				customTextureCreate.SetFloat(array7[i], value);
				customTextureCreate2.SetVector4(array6[i], zero);
				customTextureCreate2.SetFloat(array7[i], value);
				updateCMFaceLayout[array[i]] = false;
				flag = true;
				flag2 = true;
			}
		}
		if (updateCMFaceTex[4])
		{
			if (SetCreateTexture(customTextureCreate, main: false, ChaListDefine.CategoryNo.st_cheek, makeup.cheekId, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.AddTex, ChaShader.CheekTex))
			{
				flag = true;
			}
			if (SetCreateTexture(customTextureCreate2, main: false, ChaListDefine.CategoryNo.st_cheek, makeup.cheekId, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.GlossTex, ChaShader.CheekTex))
			{
				flag2 = true;
			}
			updateCMFaceTex[4] = false;
		}
		if (updateCMFaceColor[4])
		{
			customTextureCreate.SetColor(ChaShader.CheekColor, makeup.cheekColor);
			updateCMFaceColor[4] = false;
			flag = true;
		}
		if (updateCMFaceGloss[4])
		{
			customTextureCreate2.SetFloat(ChaShader.CheekGloss, makeup.cheekGloss);
			updateCMFaceGloss[4] = false;
			flag2 = true;
		}
		if (updateCMFaceTex[5])
		{
			if (SetCreateTexture(customTextureCreate, main: false, ChaListDefine.CategoryNo.st_lip, makeup.lipId, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.AddTex, ChaShader.LipTex))
			{
				flag = true;
			}
			if (SetCreateTexture(customTextureCreate2, main: false, ChaListDefine.CategoryNo.st_lip, makeup.lipId, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.GlossTex, ChaShader.LipTex))
			{
				flag2 = true;
			}
			updateCMFaceTex[5] = false;
		}
		if (updateCMFaceColor[5])
		{
			customTextureCreate.SetColor(ChaShader.LipColor, makeup.lipColor);
			updateCMFaceColor[5] = false;
			flag = true;
		}
		if (updateCMFaceGloss[5])
		{
			customTextureCreate2.SetFloat(ChaShader.LipGloss, makeup.lipGloss);
			updateCMFaceGloss[5] = false;
			flag2 = true;
		}
		if (updateCMFaceTex[6])
		{
			if (SetCreateTexture(customTextureCreate, main: false, ChaListDefine.CategoryNo.st_mole, base.fileFace.moleId, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.AddTex, ChaShader.MoleTex))
			{
				flag = true;
			}
			updateCMFaceTex[6] = false;
		}
		if (updateCMFaceColor[6])
		{
			customTextureCreate.SetColor(ChaShader.MoleColor, base.fileFace.moleColor);
			updateCMFaceColor[6] = false;
			flag = true;
		}
		if (updateCMFaceLayout[6])
		{
			Vector4 vector = customTextureCreate.GetVector4(ChaShader.MoleLayout);
			vector.x = Mathf.Lerp(5f, 1f, base.fileFace.moleLayout.x);
			vector.y = Mathf.Lerp(5f, 1f, base.fileFace.moleLayout.y);
			vector.z = Mathf.Lerp(0.3f, -0.3f, base.fileFace.moleLayout.z);
			vector.w = Mathf.Lerp(0.3f, -0.3f, base.fileFace.moleLayout.w);
			customTextureCreate.SetVector4(ChaShader.MoleLayout, vector);
			updateCMFaceLayout[6] = false;
			flag = true;
		}
		if (flag)
		{
			base.customTexCtrlFace.SetNewCreateTexture(0, ChaShader.SkinTex);
		}
		if (flag2)
		{
			base.customTexCtrlFace.SetNewCreateTexture(1, ChaShader.SkinCreateDetailTex);
		}
		if (base.releaseCustomInputTexture)
		{
			ReleaseFaceCustomTexture();
		}
		return true;
	}

	public bool ChangeFaceGlossPower()
	{
		float value = Mathf.Lerp(0f, 0.8f, base.fileBody.skinGlossPower) + 0.2f * base.fileStatus.skinTuyaRate;
		base.customTexCtrlFace.matDraw.SetFloat(ChaShader.Gloss, value);
		return true;
	}

	public bool ChangeFaceMetallicPower()
	{
		base.customTexCtrlFace.matDraw.SetFloat(ChaShader.Metallic, base.fileBody.skinMetallicPower);
		return true;
	}

	public bool ChangeFaceDetailKind()
	{
		ChangeTexture(base.customTexCtrlFace.matDraw, (base.sex != 0) ? ChaListDefine.CategoryNo.ft_detail_f : ChaListDefine.CategoryNo.mt_detail_f, base.fileFace.detailId, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.AddTex, ChaShader.SkinDetailTex, string.Empty);
		return true;
	}

	public bool ChangeFaceDetailPower()
	{
		base.customTexCtrlFace.matDraw.SetFloat(ChaShader.SkinDetailPower, base.fileFace.detailPower);
		return true;
	}

	public bool ChangeEyebrowKind()
	{
		ChangeTexture(base.customTexCtrlFace.matDraw, ChaListDefine.CategoryNo.st_eyebrow, base.fileFace.eyebrowId, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.AddTex, ChaShader.EyebrowTex, string.Empty);
		return true;
	}

	public bool ChangeEyebrowColor()
	{
		base.customTexCtrlFace.matDraw.SetColor(ChaShader.EyebrowColor, base.fileFace.eyebrowColor);
		return true;
	}

	public bool ChangeEyebrowLayout()
	{
		Vector4 vector = base.customTexCtrlFace.matDraw.GetVector(ChaShader.EyebrowLayout);
		vector.x = Mathf.Lerp(-0.2f, 0.2f, base.fileFace.eyebrowLayout.x);
		vector.y = Mathf.Lerp(0.16f, 0f, base.fileFace.eyebrowLayout.y);
		vector.z = Mathf.Lerp(2f, 0.5f, base.fileFace.eyebrowLayout.z);
		vector.w = Mathf.Lerp(2f, 0.5f, base.fileFace.eyebrowLayout.w);
		base.customTexCtrlFace.matDraw.SetVector(ChaShader.EyebrowLayout, vector);
		return true;
	}

	public bool ChangeEyebrowTilt()
	{
		float value = Mathf.Lerp(-0.15f, 0.15f, base.fileFace.eyebrowTilt);
		base.customTexCtrlFace.matDraw.SetFloat(ChaShader.EyebrowTilt, value);
		return true;
	}

	public bool ChangeWhiteEyesColor(int lr)
	{
		if (null == base.cmpFace)
		{
			return false;
		}
		Renderer[] rendEyes = base.cmpFace.targetCustom.rendEyes;
		if (rendEyes == null)
		{
			return false;
		}
		for (int i = 0; i < 2; i++)
		{
			if ((lr == 2 || lr == i) && !(null == rendEyes[i]) && !(null == rendEyes[i].material))
			{
				rendEyes[i].material.SetColor(ChaShader.EyesWhiteColor, base.fileFace.pupil[i].whiteColor);
			}
		}
		return true;
	}

	public bool ChangeEyesKind(int lr)
	{
		if (null == base.cmpFace)
		{
			return false;
		}
		Renderer[] rendEyes = base.cmpFace.targetCustom.rendEyes;
		if (rendEyes == null)
		{
			return false;
		}
		for (int i = 0; i < 2; i++)
		{
			if ((lr == 2 || lr == i) && !(null == rendEyes[i]) && !(null == rendEyes[i].material))
			{
				ChangeTexture(rendEyes[i], ChaListDefine.CategoryNo.st_eye, base.fileFace.pupil[i].pupilId, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.AddTex, ChaShader.PupilTex, string.Empty);
			}
		}
		return true;
	}

	public bool ChangeEyesWH(int lr)
	{
		if (null == base.cmpFace)
		{
			return false;
		}
		Renderer[] rendEyes = base.cmpFace.targetCustom.rendEyes;
		if (rendEyes == null)
		{
			return false;
		}
		for (int i = 0; i < 2; i++)
		{
			if ((lr == 2 || lr == i) && !(null == rendEyes[i]) && !(null == rendEyes[i].material))
			{
				Vector4 vector = rendEyes[i].material.GetVector(ChaShader.PupilLayout);
				vector.x = Mathf.Lerp(2f, 0.5f, base.fileFace.pupil[i].pupilW);
				vector.y = Mathf.Lerp(2f, 0.5f, base.fileFace.pupil[i].pupilH);
				rendEyes[i].material.SetVector(ChaShader.PupilLayout, vector);
			}
		}
		return true;
	}

	public bool ChangeEyesColor(int lr)
	{
		if (null == base.cmpFace)
		{
			return false;
		}
		Renderer[] rendEyes = base.cmpFace.targetCustom.rendEyes;
		if (rendEyes == null)
		{
			return false;
		}
		for (int i = 0; i < 2; i++)
		{
			if ((lr == 2 || lr == i) && !(null == rendEyes[i]) && !(null == rendEyes[i].material))
			{
				rendEyes[i].material.SetColor(ChaShader.PupilColor, base.fileFace.pupil[i].pupilColor);
			}
		}
		return true;
	}

	public bool ChangeEyesEmission(int lr)
	{
		if (null == base.cmpFace)
		{
			return false;
		}
		Renderer[] rendEyes = base.cmpFace.targetCustom.rendEyes;
		if (rendEyes == null)
		{
			return false;
		}
		for (int i = 0; i < 2; i++)
		{
			if ((lr == 2 || lr == i) && !(null == rendEyes[i]) && !(null == rendEyes[i].material))
			{
				rendEyes[i].material.SetFloat(ChaShader.PupilEmission, base.fileFace.pupil[i].pupilEmission);
			}
		}
		return true;
	}

	public bool ChangeBlackEyesKind(int lr)
	{
		if (null == base.cmpFace)
		{
			return false;
		}
		Renderer[] rendEyes = base.cmpFace.targetCustom.rendEyes;
		if (rendEyes == null)
		{
			return false;
		}
		for (int i = 0; i < 2; i++)
		{
			if ((lr == 2 || lr == i) && !(null == rendEyes[i]) && !(null == rendEyes[i].material))
			{
				ChangeTexture(rendEyes[i], ChaListDefine.CategoryNo.st_eyeblack, base.fileFace.pupil[i].blackId, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.AddTex, ChaShader.PupilBlackTex, string.Empty);
			}
		}
		return true;
	}

	public bool ChangeBlackEyesColor(int lr)
	{
		if (null == base.cmpFace)
		{
			return false;
		}
		Renderer[] rendEyes = base.cmpFace.targetCustom.rendEyes;
		if (rendEyes == null)
		{
			return false;
		}
		for (int i = 0; i < 2; i++)
		{
			if ((lr == 2 || lr == i) && !(null == rendEyes[i]) && !(null == rendEyes[i].material))
			{
				rendEyes[i].material.SetColor(ChaShader.PupilBlackColor, base.fileFace.pupil[i].blackColor);
			}
		}
		return true;
	}

	public bool ChangeBlackEyesWH(int lr)
	{
		if (null == base.cmpFace)
		{
			return false;
		}
		Renderer[] rendEyes = base.cmpFace.targetCustom.rendEyes;
		if (rendEyes == null)
		{
			return false;
		}
		for (int i = 0; i < 2; i++)
		{
			if ((lr == 2 || lr == i) && !(null == rendEyes[i]) && !(null == rendEyes[i].material))
			{
				Vector4 vector = rendEyes[i].material.GetVector(ChaShader.PupilBlackLayout);
				vector.x = Mathf.Lerp(4f, 0.4f, base.fileFace.pupil[i].blackW);
				vector.y = Mathf.Lerp(4f, 0.4f, base.fileFace.pupil[i].blackH);
				rendEyes[i].material.SetVector(ChaShader.PupilBlackLayout, vector);
			}
		}
		return true;
	}

	public bool ChangeEyesBasePosY()
	{
		if (null == base.cmpFace)
		{
			return false;
		}
		Renderer[] rendEyes = base.cmpFace.targetCustom.rendEyes;
		if (rendEyes == null)
		{
			return false;
		}
		for (int i = 0; i < 2; i++)
		{
			if (!(null == rendEyes[i]) && !(null == rendEyes[i].material))
			{
				Vector4 vector = rendEyes[i].material.GetVector(ChaShader.PupilLayout);
				vector.w = Mathf.Lerp(0.5f, -0.5f, base.fileFace.pupilY);
				rendEyes[i].material.SetVector(ChaShader.PupilLayout, vector);
			}
		}
		return true;
	}

	public bool ChangeEyesHighlightKind()
	{
		if (null == base.cmpFace)
		{
			return false;
		}
		Renderer[] rendEyes = base.cmpFace.targetCustom.rendEyes;
		if (rendEyes == null)
		{
			return false;
		}
		for (int i = 0; i < 2; i++)
		{
			if (!(null == rendEyes[i]) && !(null == rendEyes[i].material))
			{
				ChangeTexture(rendEyes[i], ChaListDefine.CategoryNo.st_eye_hl, base.fileFace.hlId, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.AddTex, ChaShader.EyesHighlightTex, string.Empty);
			}
		}
		return true;
	}

	public bool ChangeEyesHighlightColor()
	{
		if (null == base.cmpFace)
		{
			return false;
		}
		Renderer[] rendEyes = base.cmpFace.targetCustom.rendEyes;
		if (rendEyes == null)
		{
			return false;
		}
		for (int i = 0; i < 2; i++)
		{
			if (!(null == rendEyes[i]) && !(null == rendEyes[i].material))
			{
				rendEyes[i].material.SetColor(ChaShader.EyesHighlightColor, base.fileFace.hlColor);
			}
		}
		return true;
	}

	public bool ChangeEyesHighlighLayout()
	{
		if (null == base.cmpFace)
		{
			return false;
		}
		Renderer[] rendEyes = base.cmpFace.targetCustom.rendEyes;
		if (rendEyes == null)
		{
			return false;
		}
		Vector4 value = default(Vector4);
		value.x = Mathf.Lerp(1.8f, 0.2f, base.fileFace.hlLayout.x);
		value.y = Mathf.Lerp(1.8f, 0.2f, base.fileFace.hlLayout.y);
		value.z = Mathf.Lerp(-0.3f, 0.3f, base.fileFace.hlLayout.z);
		value.w = Mathf.Lerp(-0.3f, 0.3f, base.fileFace.hlLayout.w);
		for (int i = 0; i < 2; i++)
		{
			if (!(null == rendEyes[i]) && !(null == rendEyes[i].material))
			{
				rendEyes[i].material.SetVector(ChaShader.EyesHighlightLayout, value);
			}
		}
		return true;
	}

	public bool ChangeEyesHighlighTilt()
	{
		if (null == base.cmpFace)
		{
			return false;
		}
		Renderer[] rendEyes = base.cmpFace.targetCustom.rendEyes;
		if (rendEyes == null)
		{
			return false;
		}
		float value = Mathf.Lerp(-1f, 1f, base.fileFace.hlTilt);
		for (int i = 0; i < 2; i++)
		{
			if (!(null == rendEyes[i]) && !(null == rendEyes[i].material))
			{
				rendEyes[i].material.SetFloat(ChaShader.EyesHighlightTilt, value);
			}
		}
		return true;
	}

	public bool ChangeEyesShadowRange()
	{
		if (null == base.cmpFace)
		{
			return false;
		}
		Renderer rendShadow = base.cmpFace.targetCustom.rendShadow;
		if (null == rendShadow)
		{
			return false;
		}
		if (null == rendShadow.material)
		{
			return false;
		}
		float value = Mathf.Lerp(0.1f, 0.9f, base.fileFace.whiteShadowScale);
		rendShadow.material.SetFloat(ChaShader.EyesShadowRange, value);
		return true;
	}

	public bool ChangeEyelashesKind()
	{
		if (null == base.cmpFace)
		{
			return false;
		}
		Renderer rendEyelashes = base.cmpFace.targetCustom.rendEyelashes;
		if (null == rendEyelashes)
		{
			return false;
		}
		if (null == rendEyelashes.material)
		{
			return false;
		}
		ChangeTexture(rendEyelashes, ChaListDefine.CategoryNo.st_eyelash, base.fileFace.eyelashesId, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.AddTex, ChaShader.EyelashesTex, string.Empty);
		return true;
	}

	public bool ChangeEyelashesColor()
	{
		if (null == base.cmpFace)
		{
			return false;
		}
		Renderer rendEyelashes = base.cmpFace.targetCustom.rendEyelashes;
		if (null == rendEyelashes)
		{
			return false;
		}
		if (null == rendEyelashes.material)
		{
			return false;
		}
		rendEyelashes.material.SetColor(ChaShader.EyelashesColor, base.fileFace.eyelashesColor);
		return true;
	}

	public bool ChangeBeardKind()
	{
		ChangeTexture(base.customTexCtrlFace.matDraw, ChaListDefine.CategoryNo.mt_beard, base.fileFace.beardId, ChaListDefine.KeyType.MainManifest, ChaListDefine.KeyType.MainAB, ChaListDefine.KeyType.AddTex, ChaShader.BeardTex, string.Empty);
		return true;
	}

	public bool ChangeBeardColor()
	{
		base.customTexCtrlFace.matDraw.SetColor(ChaShader.BeardColor, base.fileFace.beardColor);
		return true;
	}

	public bool SetFaceBaseMaterial()
	{
		if (null == base.customMatFace)
		{
			return false;
		}
		if (null == base.cmpFace)
		{
			return false;
		}
		Renderer rendHead = base.cmpFace.targetCustom.rendHead;
		if (null == rendHead)
		{
			return false;
		}
		return SetBaseMaterial(rendHead, base.customMatFace);
	}

	public bool ReleaseFaceCustomTexture()
	{
		if (base.customTexCtrlFace == null)
		{
			return false;
		}
		CustomTextureCreate customTextureCreate = base.customTexCtrlFace.createCustomTex[0];
		CustomTextureCreate customTextureCreate2 = base.customTexCtrlFace.createCustomTex[1];
		customTextureCreate.SetTexture(ChaShader.MainTex, null);
		customTextureCreate.SetTexture(ChaShader.EyeshadowTex, null);
		customTextureCreate.SetTexture(ChaShader.Paint01Tex, null);
		customTextureCreate.SetTexture(ChaShader.Paint02Tex, null);
		customTextureCreate.SetTexture(ChaShader.CheekTex, null);
		customTextureCreate.SetTexture(ChaShader.LipTex, null);
		customTextureCreate.SetTexture(ChaShader.MoleTex, null);
		customTextureCreate2.SetTexture(ChaShader.MainTex, null);
		customTextureCreate2.SetTexture(ChaShader.EyeshadowTex, null);
		customTextureCreate2.SetTexture(ChaShader.Paint01Tex, null);
		customTextureCreate2.SetTexture(ChaShader.Paint02Tex, null);
		customTextureCreate2.SetTexture(ChaShader.CheekTex, null);
		customTextureCreate2.SetTexture(ChaShader.LipTex, null);
		OutputLog.Log("ReleaseBodyCustomTexture", unityLog: false, "UnloadUnusedAssets");
		UnityEngine.Resources.UnloadUnusedAssets();
		return true;
	}

	public void ChangeCustomFaceWithoutCustomTexture()
	{
		ChangeFaceGlossPower();
		ChangeFaceMetallicPower();
		ChangeFaceDetailKind();
		ChangeFaceDetailPower();
		ChangeEyebrowKind();
		ChangeEyebrowColor();
		ChangeEyebrowLayout();
		ChangeEyebrowTilt();
		ChangeWhiteEyesColor(2);
		ChangeEyesKind(2);
		ChangeEyesWH(2);
		ChangeEyesColor(2);
		ChangeEyesEmission(2);
		ChangeBlackEyesKind(2);
		ChangeBlackEyesColor(2);
		ChangeBlackEyesWH(2);
		ChangeEyesBasePosY();
		ChangeEyesHighlightKind();
		ChangeEyesHighlightColor();
		ChangeEyesHighlighLayout();
		ChangeEyesHighlighTilt();
		ChangeEyesShadowRange();
		ChangeEyelashesKind();
		ChangeEyelashesColor();
		if (base.sex == 0)
		{
			ChangeBeardKind();
			ChangeBeardColor();
		}
	}

	public bool InitShapeFace(Transform trfBone, string manifest, string assetBundleAnmShapeFace, string assetAnmShapeFace)
	{
		if (sibFace == null)
		{
			return false;
		}
		if (null == trfBone)
		{
			return false;
		}
		string cateInfoName = ChaABDefine.ShapeHeadListAsset(base.sex);
		sibFace.InitShapeInfo(manifest, assetBundleAnmShapeFace, "list/customshape.unity3d", assetAnmShapeFace, cateInfoName, trfBone);
		for (int i = 0; i < ShapeFaceNum; i++)
		{
			sibFace.ChangeValue(i, base.fileFace.shapeValueFace[i]);
		}
		base.updateShapeFace = true;
		return true;
	}

	public void ReleaseShapeFace()
	{
		if (sibFace != null)
		{
			sibFace.ReleaseShapeInfo();
		}
	}

	public bool SetShapeFaceValue(int index, float value)
	{
		if (index >= ShapeFaceNum)
		{
			return false;
		}
		base.fileFace.shapeValueFace[index] = value;
		if (sibFace != null && sibFace.InitEnd)
		{
			sibFace.ChangeValue(index, value);
		}
		base.updateShapeFace = true;
		return true;
	}

	public bool UpdateShapeFaceValueFromCustomInfo()
	{
		if (sibFace == null || !sibFace.InitEnd)
		{
			return false;
		}
		for (int i = 0; i < base.fileFace.shapeValueFace.Length; i++)
		{
			sibFace.ChangeValue(i, base.fileFace.shapeValueFace[i]);
		}
		base.updateShapeFace = true;
		return true;
	}

	public float GetShapeFaceValue(int index)
	{
		if (index >= ShapeFaceNum)
		{
			return 0f;
		}
		return base.fileFace.shapeValueFace[index];
	}

	public void UpdateShapeFace()
	{
		if (sibFace == null || !sibFace.InitEnd)
		{
			return;
		}
		if (base.fileStatus.disableMouthShapeMask)
		{
			for (int i = 0; i < ChaFileDefine.cf_MouthShapeMaskID.Length; i++)
			{
				sibFace.ChangeValue(ChaFileDefine.cf_MouthShapeMaskID[i], ChaFileDefine.cf_MouthShapeDefault[i]);
			}
		}
		else
		{
			int[] cf_MouthShapeMaskID = ChaFileDefine.cf_MouthShapeMaskID;
			foreach (int num in cf_MouthShapeMaskID)
			{
				sibFace.ChangeValue(num, base.fileFace.shapeValueFace[num]);
			}
		}
		sibFace.Update();
	}

	public void DisableShapeMouth(bool disable)
	{
		base.updateShapeFace = true;
		base.fileStatus.disableMouthShapeMask = disable;
	}

	protected void InitializeControlCustomHairAll()
	{
		InitializeControlCustomHairObject();
	}

	protected void InitializeControlCustomHairObject()
	{
	}

	protected void ReleaseControlCustomHairAll()
	{
		ReleaseControlCustomHairObject();
	}

	protected void ReleaseControlCustomHairObject(bool init = true)
	{
		if (init)
		{
			InitializeControlCustomHairObject();
		}
	}

	public void ChangeSettingHairShader()
	{
		ChaFileHair hair = base.chaFile.custom.hair;
		Shader shader = ((hair.shaderType != 0) ? Singleton<Character>.Instance.shaderCutout : Singleton<Character>.Instance.shaderDithering);
		int num = 0;
		for (int i = 0; i < base.cmpHair.Length; i++)
		{
			if (null == base.cmpHair[i])
			{
				continue;
			}
			CmpHair customHairComponent = GetCustomHairComponent(i);
			if (null == customHairComponent || customHairComponent.rendHair == null || customHairComponent.rendHair.Length == 0 || base.infoHair == null || base.infoHair[i] == null)
			{
				continue;
			}
			string info = base.infoHair[i].GetInfo(ChaListDefine.KeyType.TexManifest);
			string info2 = base.infoHair[i].GetInfo(ChaListDefine.KeyType.TexAB);
			string info3 = base.infoHair[i].GetInfo((hair.shaderType != 0) ? ChaListDefine.KeyType.TexC : ChaListDefine.KeyType.TexD);
			int value = ((base.infoHair[i].GetInfoInt(ChaListDefine.KeyType.RingOff) == 1) ? 1 : 0);
			Texture2D value2 = CommonLib.LoadAsset<Texture2D>(info2, info3, clone: false, info);
			Singleton<Character>.Instance.AddLoadAssetBundle(info2, info);
			for (int j = 0; j < customHairComponent.rendHair.Length; j++)
			{
				for (int k = 0; k < customHairComponent.rendHair[j].materials.Length; k++)
				{
					num = customHairComponent.rendHair[j].materials[k].renderQueue;
					customHairComponent.rendHair[j].materials[k].shader = shader;
					customHairComponent.rendHair[j].materials[k].SetTexture(ChaShader.MainTex, value2);
					if (customHairComponent.rendHair[j].materials[k].HasProperty(ChaShader.HairRingoff))
					{
						customHairComponent.rendHair[j].materials[k].SetInt(ChaShader.HairRingoff, value);
					}
					customHairComponent.rendHair[j].materials[k].renderQueue = num;
				}
			}
		}
	}

	public void ChangeSettingHairColor(int parts, bool _main, bool _top, bool _under)
	{
		CmpHair customHairComponent = GetCustomHairComponent(parts);
		if (null == customHairComponent || customHairComponent.rendHair == null || customHairComponent.rendHair.Length == 0)
		{
			return;
		}
		ChaFileHair hair = base.chaFile.custom.hair;
		for (int i = 0; i < customHairComponent.rendHair.Length; i++)
		{
			if (_main)
			{
				if (1f > hair.parts[parts].baseColor.a)
				{
					hair.parts[parts].baseColor = new Color(hair.parts[parts].baseColor.r, hair.parts[parts].baseColor.g, hair.parts[parts].baseColor.b, 1f);
				}
				for (int j = 0; j < customHairComponent.rendHair[i].materials.Length; j++)
				{
					customHairComponent.rendHair[i].materials[j].SetColor(ChaShader.HairMainColor, hair.parts[parts].baseColor);
				}
			}
			if (customHairComponent.useTopColor && _top)
			{
				if (1f > hair.parts[parts].topColor.a)
				{
					hair.parts[parts].topColor = new Color(hair.parts[parts].topColor.r, hair.parts[parts].topColor.g, hair.parts[parts].topColor.b, 1f);
				}
				for (int k = 0; k < customHairComponent.rendHair[i].materials.Length; k++)
				{
					customHairComponent.rendHair[i].materials[k].SetColor(ChaShader.HairTopColor, hair.parts[parts].topColor);
				}
			}
			if (customHairComponent.useUnderColor && _under)
			{
				if (1f > hair.parts[parts].underColor.a)
				{
					hair.parts[parts].underColor = new Color(hair.parts[parts].underColor.r, hair.parts[parts].underColor.g, hair.parts[parts].underColor.b, 1f);
				}
				for (int l = 0; l < customHairComponent.rendHair[i].materials.Length; l++)
				{
					customHairComponent.rendHair[i].materials[l].SetColor(ChaShader.HairUnderColor, hair.parts[parts].underColor);
				}
			}
		}
	}

	public void CreateHairColor(int parts, Color color)
	{
		ChaFileHair hair = base.chaFile.custom.hair;
		hair.parts[parts].baseColor = new Color(color.r, color.g, color.b, 1f);
		CreateHairColor(color, out var topColor, out var underColor, out var specular);
		hair.parts[parts].topColor = topColor;
		hair.parts[parts].underColor = underColor;
		hair.parts[parts].specular = specular;
	}

	public void CreateHairColor(Color baseColor, out Color topColor, out Color underColor, out Color specular)
	{
		Color.RGBToHSV(baseColor, out var H, out var S, out var V);
		topColor = Color.HSVToRGB(H, S, Mathf.Max(V - 0.15f, 0f));
		underColor = Color.HSVToRGB(H, Mathf.Max(S - 0.1f, 0f), Mathf.Min(V + 0.44f, 1f));
		specular = Color.HSVToRGB(H, S, Mathf.Min(V + 0.17f, 1f));
	}

	public void ChangeSettingHairSpecular(int parts)
	{
		CmpHair customHairComponent = GetCustomHairComponent(parts);
		if (null == customHairComponent || customHairComponent.rendHair == null || customHairComponent.rendHair.Length == 0)
		{
			return;
		}
		ChaFileHair hair = base.chaFile.custom.hair;
		for (int i = 0; i < customHairComponent.rendHair.Length; i++)
		{
			if (1f > hair.parts[parts].specular.a)
			{
				hair.parts[parts].specular = new Color(hair.parts[parts].specular.r, hair.parts[parts].specular.g, hair.parts[parts].specular.b, 1f);
			}
			for (int j = 0; j < customHairComponent.rendHair[i].materials.Length; j++)
			{
				customHairComponent.rendHair[i].materials[j].SetColor(ChaShader.Specular, hair.parts[parts].specular);
			}
		}
	}

	public void ChangeSettingHairMetallic(int parts)
	{
		CmpHair customHairComponent = GetCustomHairComponent(parts);
		if (null == customHairComponent || customHairComponent.rendHair == null || customHairComponent.rendHair.Length == 0)
		{
			return;
		}
		ChaFileHair hair = base.chaFile.custom.hair;
		for (int i = 0; i < customHairComponent.rendHair.Length; i++)
		{
			for (int j = 0; j < customHairComponent.rendHair[i].materials.Length; j++)
			{
				customHairComponent.rendHair[i].materials[j].SetFloat(ChaShader.Metallic, hair.parts[parts].metallic);
			}
		}
	}

	public void ChangeSettingHairSmoothness(int parts)
	{
		CmpHair customHairComponent = GetCustomHairComponent(parts);
		if (null == customHairComponent || customHairComponent.rendHair == null || customHairComponent.rendHair.Length == 0)
		{
			return;
		}
		ChaFileHair hair = base.chaFile.custom.hair;
		for (int i = 0; i < customHairComponent.rendHair.Length; i++)
		{
			for (int j = 0; j < customHairComponent.rendHair[i].materials.Length; j++)
			{
				customHairComponent.rendHair[i].materials[j].SetFloat(ChaShader.Smoothness, hair.parts[parts].smoothness);
			}
		}
	}

	public int GetHairAcsColorNum(int parts)
	{
		CmpHair customHairComponent = GetCustomHairComponent(parts);
		if (null == customHairComponent || customHairComponent.acsDefColor == null || customHairComponent.acsDefColor.Length == 0)
		{
			return 0;
		}
		return customHairComponent.acsDefColor.Length;
	}

	public void SetHairAcsDefaultColorParameterOnly(int parts)
	{
		ChaFileHair hair = base.chaFile.custom.hair;
		CmpHair customHairComponent = GetCustomHairComponent(parts);
		if (null == customHairComponent)
		{
			return;
		}
		int num = customHairComponent.acsDefColor.Length;
		for (int i = 0; i < num; i++)
		{
			if (customHairComponent.acsDefColor != null)
			{
				hair.parts[parts].acsColorInfo[i].color = customHairComponent.acsDefColor[i];
			}
		}
	}

	public void ChangeSettingHairAcsColor(int parts)
	{
		if (base.cmpHair == null || null == base.cmpHair[parts])
		{
			return;
		}
		int hairAcsColorNum = GetHairAcsColorNum(parts);
		if (hairAcsColorNum == 0)
		{
			return;
		}
		CmpHair customHairComponent = GetCustomHairComponent(parts);
		if (null == customHairComponent)
		{
			return;
		}
		int[] array = new int[3]
		{
			ChaShader.Color,
			ChaShader.Color2,
			ChaShader.Color3
		};
		bool[] array2 = new bool[3]
		{
			base.cmpHair[parts].useAcsColor01,
			base.cmpHair[parts].useAcsColor02,
			base.cmpHair[parts].useAcsColor03
		};
		for (int i = 0; i < customHairComponent.rendAccessory.Length; i++)
		{
			for (int j = 0; j < hairAcsColorNum; j++)
			{
				if (array2[j])
				{
					if (1f > base.fileHair.parts[parts].acsColorInfo[j].color.a)
					{
						base.fileHair.parts[parts].acsColorInfo[j].color = new Color(base.fileHair.parts[parts].acsColorInfo[j].color.r, base.fileHair.parts[parts].acsColorInfo[j].color.g, base.fileHair.parts[parts].acsColorInfo[j].color.b, 1f);
					}
					Material[] materials = customHairComponent.rendAccessory[i].materials;
					foreach (Material material in materials)
					{
						material.SetColor(array[j], base.fileHair.parts[parts].acsColorInfo[j].color);
					}
				}
			}
		}
	}

	public void ChangeSettingHairCorrectPos(int parts, int idx)
	{
		if (base.cmpHair != null && base.cmpHair.Length > parts && !(null == base.cmpHair[parts]) && base.cmpHair[parts].boneInfo != null && base.cmpHair[parts].boneInfo.Length > idx)
		{
			ChaFileHair hair = base.chaFile.custom.hair;
			if (hair.parts[parts].dictBundle.TryGetValue(idx, out var value))
			{
				base.cmpHair[parts].boneInfo[idx].moveRate = value.moveRate;
			}
		}
	}

	public void ChangeSettingHairCorrectPosAll(int parts)
	{
		if (base.cmpHair == null || base.cmpHair.Length <= parts || null == base.cmpHair[parts])
		{
			return;
		}
		ChaFileHair hair = base.chaFile.custom.hair;
		if (base.cmpHair[parts].boneInfo == null || base.cmpHair[parts].boneInfo.Length != hair.parts[parts].dictBundle.Count)
		{
			return;
		}
		for (int i = 0; i < base.cmpHair[parts].boneInfo.Length; i++)
		{
			if (hair.parts[parts].dictBundle.TryGetValue(i, out var value))
			{
				base.cmpHair[parts].boneInfo[i].moveRate = value.moveRate;
			}
		}
	}

	public bool SetHairCorrectPosValue(int parts, int idx, Vector3 val, int _flag)
	{
		if (base.cmpHair == null || base.cmpHair.Length <= parts || null == base.cmpHair[parts])
		{
			return false;
		}
		if (base.cmpHair[parts].boneInfo == null || base.cmpHair[parts].boneInfo.Length <= idx)
		{
			return false;
		}
		ChaFileHair hair = base.chaFile.custom.hair;
		if (!hair.parts[parts].dictBundle.TryGetValue(idx, out var value))
		{
			return false;
		}
		val = base.cmpHair[parts].boneInfo[idx].trfCorrect.parent.InverseTransformPoint(val.x, val.y, val.z);
		Vector3 moveRate = value.moveRate;
		if (((uint)_flag & (true ? 1u : 0u)) != 0)
		{
			moveRate.x = Mathf.InverseLerp(base.cmpHair[parts].boneInfo[idx].posMin.x, base.cmpHair[parts].boneInfo[idx].posMax.x, val.x);
		}
		if (((uint)_flag & 2u) != 0)
		{
			moveRate.y = Mathf.InverseLerp(base.cmpHair[parts].boneInfo[idx].posMin.y, base.cmpHair[parts].boneInfo[idx].posMax.y, val.y);
		}
		if (((uint)_flag & 4u) != 0)
		{
			moveRate.z = Mathf.InverseLerp(base.cmpHair[parts].boneInfo[idx].posMin.z, base.cmpHair[parts].boneInfo[idx].posMax.z, val.z);
		}
		value.moveRate = moveRate;
		return true;
	}

	public bool GetDefaultHairCorrectPosRate(int parts, int idx, out Vector3 v)
	{
		v = Vector3.zero;
		if (base.cmpHair == null || base.cmpHair.Length <= parts || null == base.cmpHair[parts])
		{
			return false;
		}
		if (base.cmpHair[parts].boneInfo == null || base.cmpHair[parts].boneInfo.Length <= idx)
		{
			return false;
		}
		ChaFileHair hair = base.chaFile.custom.hair;
		if (!hair.parts[parts].dictBundle.TryGetValue(idx, out var _))
		{
			return false;
		}
		v.x = Mathf.InverseLerp(base.cmpHair[parts].boneInfo[idx].posMin.x, base.cmpHair[parts].boneInfo[idx].posMax.x, base.cmpHair[parts].boneInfo[idx].basePos.x);
		v.y = Mathf.InverseLerp(base.cmpHair[parts].boneInfo[idx].posMin.y, base.cmpHair[parts].boneInfo[idx].posMax.y, base.cmpHair[parts].boneInfo[idx].basePos.y);
		v.z = Mathf.InverseLerp(base.cmpHair[parts].boneInfo[idx].posMin.z, base.cmpHair[parts].boneInfo[idx].posMax.z, base.cmpHair[parts].boneInfo[idx].basePos.z);
		return true;
	}

	public void SetDefaultHairCorrectPosRate(int parts, int idx)
	{
		if (GetDefaultHairCorrectPosRate(parts, idx, out var v))
		{
			ChaFileHair hair = base.chaFile.custom.hair;
			if (hair.parts[parts].dictBundle.TryGetValue(idx, out var value))
			{
				value.moveRate = v;
			}
		}
	}

	public void SetDefaultHairCorrectPosRateAll(int parts)
	{
		if (base.cmpHair == null || base.cmpHair.Length <= parts || null == base.cmpHair[parts])
		{
			return;
		}
		ChaFileHair hair = base.chaFile.custom.hair;
		if (base.cmpHair[parts].boneInfo == null || base.cmpHair[parts].boneInfo.Length != hair.parts[parts].dictBundle.Count)
		{
			return;
		}
		for (int i = 0; i < base.cmpHair[parts].boneInfo.Length; i++)
		{
			if (hair.parts[parts].dictBundle.TryGetValue(i, out var value) && GetDefaultHairCorrectPosRate(parts, i, out var v))
			{
				value.moveRate = v;
			}
		}
	}

	public void ChangeSettingHairCorrectRot(int parts, int idx)
	{
		if (base.cmpHair != null && base.cmpHair.Length > parts && !(null == base.cmpHair[parts]) && base.cmpHair[parts].boneInfo != null && base.cmpHair[parts].boneInfo.Length > idx)
		{
			ChaFileHair hair = base.chaFile.custom.hair;
			if (hair.parts[parts].dictBundle.TryGetValue(idx, out var value))
			{
				base.cmpHair[parts].boneInfo[idx].rotRate = value.rotRate;
			}
		}
	}

	public void ChangeSettingHairCorrectRotAll(int parts)
	{
		if (base.cmpHair == null || base.cmpHair.Length <= parts || null == base.cmpHair[parts])
		{
			return;
		}
		ChaFileHair hair = base.chaFile.custom.hair;
		if (base.cmpHair[parts].boneInfo == null || base.cmpHair[parts].boneInfo.Length != hair.parts[parts].dictBundle.Count)
		{
			return;
		}
		for (int i = 0; i < base.cmpHair[parts].boneInfo.Length; i++)
		{
			if (hair.parts[parts].dictBundle.TryGetValue(i, out var value))
			{
				base.cmpHair[parts].boneInfo[i].rotRate = value.rotRate;
			}
		}
	}

	public bool SetHairCorrectRotValue(int parts, int idx, Vector3 val, int _flag)
	{
		if (base.cmpHair == null || base.cmpHair.Length <= parts || null == base.cmpHair[parts])
		{
			return false;
		}
		if (base.cmpHair[parts].boneInfo == null || base.cmpHair[parts].boneInfo.Length <= idx)
		{
			return false;
		}
		ChaFileHair hair = base.chaFile.custom.hair;
		if (!hair.parts[parts].dictBundle.TryGetValue(idx, out var value))
		{
			return false;
		}
		val.x = ((!(val.x > 180f)) ? val.x : (val.x - 360f));
		val.y = ((!(val.y > 180f)) ? val.y : (val.y - 360f));
		val.z = ((!(val.z > 180f)) ? val.z : (val.z - 360f));
		Vector3 rotRate = value.rotRate;
		if (((uint)_flag & (true ? 1u : 0u)) != 0)
		{
			rotRate.x = Mathf.InverseLerp(base.cmpHair[parts].boneInfo[idx].rotMin.x, base.cmpHair[parts].boneInfo[idx].rotMax.x, val.x);
		}
		if (((uint)_flag & 2u) != 0)
		{
			rotRate.y = Mathf.InverseLerp(base.cmpHair[parts].boneInfo[idx].rotMin.y, base.cmpHair[parts].boneInfo[idx].rotMax.y, val.y);
		}
		if (((uint)_flag & 4u) != 0)
		{
			rotRate.z = Mathf.InverseLerp(base.cmpHair[parts].boneInfo[idx].rotMin.z, base.cmpHair[parts].boneInfo[idx].rotMax.z, val.z);
		}
		value.rotRate = rotRate;
		return true;
	}

	public bool GetDefaultHairCorrectRotRate(int parts, int idx, out Vector3 v)
	{
		v = Vector3.zero;
		if (base.cmpHair == null || base.cmpHair.Length <= parts || null == base.cmpHair[parts])
		{
			return false;
		}
		if (base.cmpHair[parts].boneInfo == null || base.cmpHair[parts].boneInfo.Length <= idx)
		{
			return false;
		}
		ChaFileHair hair = base.chaFile.custom.hair;
		if (!hair.parts[parts].dictBundle.TryGetValue(idx, out var _))
		{
			return false;
		}
		v.x = Mathf.InverseLerp(base.cmpHair[parts].boneInfo[idx].rotMin.x, base.cmpHair[parts].boneInfo[idx].rotMax.x, base.cmpHair[parts].boneInfo[idx].baseRot.x);
		v.y = Mathf.InverseLerp(base.cmpHair[parts].boneInfo[idx].rotMin.y, base.cmpHair[parts].boneInfo[idx].rotMax.y, base.cmpHair[parts].boneInfo[idx].baseRot.y);
		v.z = Mathf.InverseLerp(base.cmpHair[parts].boneInfo[idx].rotMin.z, base.cmpHair[parts].boneInfo[idx].rotMax.z, base.cmpHair[parts].boneInfo[idx].baseRot.z);
		return true;
	}

	public void SetDefaultHairCorrectRotRate(int parts, int idx)
	{
		if (GetDefaultHairCorrectRotRate(parts, idx, out var v))
		{
			ChaFileHair hair = base.chaFile.custom.hair;
			if (hair.parts[parts].dictBundle.TryGetValue(idx, out var value))
			{
				value.rotRate = v;
			}
		}
	}

	public void SetDefaultHairCorrectRotRateAll(int parts)
	{
		if (base.cmpHair == null || base.cmpHair.Length <= parts || null == base.cmpHair[parts])
		{
			return;
		}
		ChaFileHair hair = base.chaFile.custom.hair;
		if (base.cmpHair[parts].boneInfo == null || base.cmpHair[parts].boneInfo.Length != hair.parts[parts].dictBundle.Count)
		{
			return;
		}
		for (int i = 0; i < base.cmpHair[parts].boneInfo.Length; i++)
		{
			if (hair.parts[parts].dictBundle.TryGetValue(i, out var value) && GetDefaultHairCorrectRotRate(parts, i, out var v))
			{
				value.rotRate = v;
			}
		}
	}

	public void ResetDynamicBoneHair(bool includeInactive = false)
	{
		if (base.cmpHair == null)
		{
			return;
		}
		for (int i = 0; i < base.cmpHair.Length; i++)
		{
			if (!(null == base.cmpHair[i]))
			{
				base.cmpHair[i].ResetDynamicBonesHair(includeInactive);
			}
		}
	}

	public void ChangeSettingHairMeshType(int parts)
	{
		CmpHair customHairComponent = GetCustomHairComponent(parts);
		if (null == customHairComponent || customHairComponent.rendHair == null || customHairComponent.rendHair.Length == 0 || !customHairComponent.useMesh)
		{
			return;
		}
		ChaFileHair hair = base.chaFile.custom.hair;
		Texture2D value = null;
		ListInfoBase listInfo = base.lstCtrl.GetListInfo(ChaListDefine.CategoryNo.st_hairmeshptn, hair.parts[parts].meshType);
		if (listInfo != null)
		{
			string info = listInfo.GetInfo(ChaListDefine.KeyType.MainAB);
			string info2 = listInfo.GetInfo(ChaListDefine.KeyType.MainTex);
			if ("0" != info && "0" != info2)
			{
				value = CommonLib.LoadAsset<Texture2D>(info, info2, clone: false, string.Empty);
				Singleton<Character>.Instance.AddLoadAssetBundle(info, "abdata");
			}
		}
		for (int i = 0; i < customHairComponent.rendHair.Length; i++)
		{
			for (int j = 0; j < customHairComponent.rendHair[i].materials.Length; j++)
			{
				customHairComponent.rendHair[i].materials[j].SetTexture(ChaShader.HairMeshColorMask, value);
			}
		}
	}

	public void ChangeSettingHairMeshColor(int parts)
	{
		CmpHair customHairComponent = GetCustomHairComponent(parts);
		if (null == customHairComponent || customHairComponent.rendHair == null || customHairComponent.rendHair.Length == 0 || !customHairComponent.useMesh)
		{
			return;
		}
		ChaFileHair hair = base.chaFile.custom.hair;
		Color value = new Color(hair.parts[parts].meshColor.r, hair.parts[parts].meshColor.g, hair.parts[parts].meshColor.b, 1f);
		for (int i = 0; i < customHairComponent.rendHair.Length; i++)
		{
			for (int j = 0; j < customHairComponent.rendHair[i].materials.Length; j++)
			{
				customHairComponent.rendHair[i].materials[j].SetColor(ChaShader.HairMeshColor, value);
			}
		}
	}

	public void ChangeSettingHairMeshLayout(int parts)
	{
		CmpHair customHairComponent = GetCustomHairComponent(parts);
		if (null == customHairComponent || customHairComponent.rendHair == null || customHairComponent.rendHair.Length == 0 || !customHairComponent.useMesh)
		{
			return;
		}
		ChaFileHair hair = base.chaFile.custom.hair;
		Vector2 value = new Vector2(Mathf.Lerp(0.5f, 1f, hair.parts[parts].meshLayout.x), Mathf.Lerp(0.5f, 1f, hair.parts[parts].meshLayout.y));
		Vector2 value2 = new Vector2(Mathf.Lerp(0.5f, 1.5f, hair.parts[parts].meshLayout.z), Mathf.Lerp(0.5f, 1.5f, hair.parts[parts].meshLayout.w));
		for (int i = 0; i < customHairComponent.rendHair.Length; i++)
		{
			for (int j = 0; j < customHairComponent.rendHair[i].materials.Length; j++)
			{
				customHairComponent.rendHair[i].materials[j].SetTextureScale(ChaShader.HairMeshColorMask, value);
				customHairComponent.rendHair[i].materials[j].SetTextureOffset(ChaShader.HairMeshColorMask, value2);
			}
		}
	}

	protected void InitializeControlFaceAll()
	{
		fbsaaVoice = new AudioAssist();
		InitializeControlFaceObject();
	}

	protected void InitializeControlFaceObject()
	{
		asVoice = null;
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

	public void ChangeLookEyesTarget(int targetType, Transform trfTarg = null, float rate = 0.5f, float rotDeg = 0f, float range = 1f, float dis = 2f)
	{
		if (null == base.eyeLookCtrl)
		{
			Debug.LogError("視線の制御スクリプトが見つかりません");
			return;
		}
		if (targetType == -1)
		{
			targetType = base.fileStatus.eyesTargetType;
		}
		else
		{
			base.fileStatus.eyesTargetType = targetType;
		}
		base.eyeLookCtrl.target = null;
		if (null != trfTarg)
		{
			base.eyeLookCtrl.target = trfTarg;
			return;
		}
		if (targetType == 0)
		{
			if (null != Camera.main)
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
			case 9:
				rotDeg = 0f;
				range = 1f;
				break;
			}
			base.objEyesLookTargetP.transform.SetLocalPosition(0f, 0.7f, 0f);
			base.eyeLookCtrl.target = base.objEyesLookTarget.transform;
			float y = Mathf.Lerp(0f, range, (targetType != 9) ? rate : 0f);
			base.eyeLookCtrl.target.SetLocalPosition(0f, y, dis);
			base.objEyesLookTargetP.transform.localEulerAngles = new Vector3(0f, 0f, 360f - rotDeg);
		}
		base.fileStatus.eyesTargetAngle = rotDeg;
		base.fileStatus.eyesTargetRange = range;
		base.fileStatus.eyesTargetRate = rate;
	}

	public void ChangeLookEyesPtn(int ptn)
	{
		if (null == base.eyeLookCtrl)
		{
			Debug.LogError("視線の制御スクリプトが見つかりません");
			return;
		}
		EyeLookController eyeLookController = base.eyeLookCtrl;
		base.fileStatus.eyesLookPtn = ptn;
		eyeLookController.ptnNo = ptn;
	}

	public int GetLookEyesPtn()
	{
		return base.fileStatus.eyesLookPtn;
	}

	public float GetLookEyesRate()
	{
		return base.fileStatus.eyesTargetRate;
	}

	public void ChangeLookNeckTarget(int targetType, Transform trfTarg = null, float rate = 0.5f, float rotDeg = 0f, float range = 1f, float dis = 0.8f)
	{
		if (null == base.neckLookCtrl)
		{
			Debug.LogError("首の制御スクリプトが見つかりません");
			return;
		}
		if (targetType == -1)
		{
			targetType = base.fileStatus.neckTargetType;
		}
		else
		{
			base.fileStatus.neckTargetType = targetType;
		}
		base.neckLookCtrl.target = null;
		if (null != trfTarg)
		{
			base.neckLookCtrl.target = trfTarg;
			return;
		}
		if (targetType == 0)
		{
			if (null != Camera.main)
			{
				base.neckLookCtrl.target = Camera.main.transform;
			}
		}
		else if (null != base.objNeckLookTarget && null != base.objNeckLookTargetP)
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
			base.objNeckLookTargetP.transform.SetLocalPosition(0f, 2.7f, 0f);
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
		if (null == base.neckLookCtrl)
		{
			Debug.LogError("首の制御スクリプトが見つかりません");
			return;
		}
		NeckLookControllerVer2 neckLookControllerVer = base.neckLookCtrl;
		base.fileStatus.neckLookPtn = ptn;
		neckLookControllerVer.ptnNo = ptn;
		base.neckLookCtrl.rate = rate;
	}

	public int GetLookNeckPtn()
	{
		return base.fileStatus.neckLookPtn;
	}

	public float GetLookNeckRate()
	{
		return base.fileStatus.neckTargetRate;
	}

	public void HideEyeHighlight(bool hide)
	{
		if (null == base.cmpFace)
		{
			return;
		}
		Renderer[] rendEyes = base.cmpFace.targetCustom.rendEyes;
		base.fileStatus.hideEyesHighlight = hide;
		float value = ((!hide) ? 1f : 0f);
		if (rendEyes == null)
		{
			return;
		}
		Renderer[] array = rendEyes;
		foreach (Renderer renderer in array)
		{
			if (null == renderer)
			{
				Debug.LogError("瞳のハイライトのレンダラーが取得できていません");
				continue;
			}
			Material material = renderer.material;
			if (null != material)
			{
				material.SetFloat(ChaShader.EyesHighlightOnOff, value);
			}
			else
			{
				Debug.LogError("瞳のハイライトのマテリアルが取得できません");
			}
		}
	}

	public void ChangeTearsRate(float value)
	{
		base.fileStatus.tearsRate = Mathf.Clamp(value, 0f, 1f);
		if (null != base.cmpFace && null != base.cmpFace.targetEtc.rendTears)
		{
			base.cmpFace.targetEtc.rendTears.material.SetFloat(ChaShader.tearsRate, base.fileStatus.tearsRate);
		}
	}

	public int GetEyesPtnNum()
	{
		if (base.eyesCtrl == null)
		{
			Debug.LogError("目の制御スクリプトが見つかりません");
			return 0;
		}
		return base.eyesCtrl.GetMaxPtn();
	}

	public void ChangeEyesPtn(int ptn, bool blend = true)
	{
		if (base.eyesCtrl == null)
		{
			Debug.LogError("目の制御スクリプトが見つかりません");
			return;
		}
		base.fileStatus.eyesPtn = ptn;
		base.eyesCtrl.ChangePtn(ptn, blend);
	}

	public int GetEyesPtn()
	{
		return base.fileStatus.eyesPtn;
	}

	public void ChangeEyesOpenMax(float maxValue)
	{
		if (base.eyesCtrl == null)
		{
			Debug.LogError("目の制御スクリプトが見つかりません");
			return;
		}
		float num = Mathf.Clamp(maxValue, 0f, 1f);
		base.fileStatus.eyesOpenMax = num;
		base.eyesCtrl.OpenMax = num;
		if (!base.fileStatus.eyesBlink)
		{
			base.eyesCtrl.SetOpenRateForce(num);
		}
	}

	public float GetEyesOpenMax()
	{
		return base.fileStatus.eyesOpenMax;
	}

	public void ChangeEyebrowPtn(int ptn, bool blend = true)
	{
		if (base.eyebrowCtrl == null)
		{
			Debug.LogError("眉の制御スクリプトが見つかりません");
			return;
		}
		base.fileStatus.eyebrowPtn = ptn;
		base.eyebrowCtrl.ChangePtn(ptn, blend);
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
		if (null == base.fbsCtrl)
		{
			Debug.LogError("表情スクリプトが見つかりません");
			return;
		}
		if (base.fbsCtrl.BlinkCtrl == null)
		{
			Debug.LogError("瞬きの制御スクリプトが見つかりません");
			return;
		}
		base.fileStatus.eyesBlink = blink;
		base.fbsCtrl.BlinkCtrl.SetFixedFlags((!blink) ? ((byte)1) : ((byte)0));
		if (!blink)
		{
			base.eyesCtrl.SetOpenRateForce(1f);
			base.eyebrowCtrl.SetOpenRateForce(1f);
		}
	}

	public bool GetEyesBlinkFlag()
	{
		return base.fileStatus.eyesBlink;
	}

	public void ChangeMouthPtn(int ptn, bool blend = true)
	{
		if (base.mouthCtrl == null)
		{
			Debug.LogError("口の制御スクリプトが見つかりません");
			return;
		}
		base.fileStatus.mouthPtn = ptn;
		base.mouthCtrl.ChangePtn(ptn, blend);
		ChangeTongueState((byte)((ptn == 10 || ptn == 13) ? 1u : 0u));
		bool useFlags = true;
		if (9 <= ptn && ptn <= 16)
		{
			useFlags = false;
		}
		base.mouthCtrl.UseAdjustWidthScale(useFlags);
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

	public void ChangeMouthOpenMin(float minValue)
	{
		if (base.mouthCtrl != null)
		{
			float num = Mathf.Clamp(minValue, 0f, 1f);
			base.fileStatus.mouthOpenMin = num;
			base.mouthCtrl.OpenMin = num;
			if (base.fileStatus.mouthFixed)
			{
				base.mouthCtrl.FixedRate = num;
			}
		}
	}

	public float GetMouthOpenMin()
	{
		return base.fileStatus.mouthOpenMin;
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

	public bool SetVoiceTransform(Transform trfVoice)
	{
		if (null == trfVoice)
		{
			asVoice = null;
			return false;
		}
		asVoice = trfVoice.GetComponent<AudioSource>();
		if (null == asVoice)
		{
			return false;
		}
		return true;
	}

	private void UpdateBlendShapeVoice()
	{
		float voiceVaule = 0f;
		float correct = 3f;
		if (null != asVoice && asVoice.isPlaying)
		{
			voiceVaule = fbsaaVoice.GetAudioWaveValue(asVoice, correct);
			if (null != base.cmpBoneBody && null != base.cmpBoneBody.targetEtc.trfHeadParent && null != asVoice.transform)
			{
				asVoice.transform.position = base.cmpBoneBody.targetEtc.trfHeadParent.position;
			}
		}
		if (null != base.fbsCtrl)
		{
			base.fbsCtrl.SetVoiceVaule(voiceVaule);
		}
		if (!base.fileStatus.mouthAdjustWidth || !(null != base.objHeadBone))
		{
			return;
		}
		Transform trfMouthAdjustWidth = base.cmpBoneHead.targetEtc.trfMouthAdjustWidth;
		if (null != trfMouthAdjustWidth)
		{
			float x = 1f;
			if (base.mouthCtrl != null)
			{
				x = base.mouthCtrl.GetAdjustWidthScale();
			}
			trfMouthAdjustWidth.SetLocalScaleX(x);
		}
	}

	public void ChangeHohoAkaRate(float value)
	{
		base.fileStatus.hohoAkaRate = Mathf.Clamp(value, 0f, 1f);
		if (null != base.customMatFace)
		{
			base.customMatFace.SetFloat(ChaShader.HohoAka, base.fileStatus.hohoAkaRate);
		}
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
		sibHand.InitShapeInfo("abdata", "list/customshape.unity3d", "list/customshape.unity3d", "cf_anmShapeHand", "cf_customhand", trfBone);
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

	public int GetShapeIndexHandCount()
	{
		return sibHand.GetKeyCount();
	}

	public int GetShapeHandIndex(int lr, int no)
	{
		if (2 <= no)
		{
			Debug.LogWarning("手のパターンは２つの合成まで");
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
		if (idx01 != -1)
		{
			base.fileStatus.shapeHandPtn[lr, 0] = idx01;
		}
		if (idx02 != -1)
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
		updateAlphaMask2 = true;
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
		StartCoroutine(LoadAsync(reflectStatus: false, asyncFlags: false));
		return true;
	}

	public IEnumerator LoadAsync(bool reflectStatus = false, bool asyncFlags = true)
	{
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
		if (null != base.objRoot)
		{
			base.objTop.transform.SetParent(base.objRoot.transform, worldPositionStays: false);
		}
		if (asyncFlags)
		{
			SetActiveTop(active: false);
		}
		AddUpdateCMBodyTexFlags(inpBase: true, inpPaint01: true, inpPaint02: true, inpSunburn: true);
		AddUpdateCMBodyColorFlags(inpBase: true, inpPaint01: true, inpPaint02: true, inpSunburn: true);
		AddUpdateCMBodyGlossFlags(inpPaint01: true, inpPaint02: true);
		AddUpdateCMBodyLayoutFlags(inpPaint01: true, inpPaint02: true);
		CreateBodyTexture();
		if (asyncFlags)
		{
			yield return StartCoroutine(Load_Coroutine("chara/oo_base.unity3d", "p_cf_anim", delegate(GameObject x)
			{
				base.objAnim = x;
			}, isClone: true, "abdata"));
		}
		else
		{
			base.objAnim = CommonLib.LoadAsset<GameObject>("chara/oo_base.unity3d", "p_cf_anim", clone: true, "abdata");
		}
		Singleton<Character>.Instance.AddLoadAssetBundle("chara/oo_base.unity3d", "abdata");
		if (null != base.objAnim)
		{
			base.cmpBoneBody = base.objAnim.GetComponent<CmpBoneBody>();
			if (null != base.cmpBoneBody)
			{
				base.cmpBoneBody.InitDynamicBonesBustAndHip();
			}
			base.animBody = base.objAnim.GetComponent<Animator>();
			base.objAnim.transform.SetParent(base.objTop.transform, worldPositionStays: false);
			if (base.sex == 0)
			{
				for (int i = 0; i < base.enableDynamicBoneBustAndHip.Length; i++)
				{
					base.enableDynamicBoneBustAndHip[i] = false;
				}
			}
			base.objBodyBone = base.objAnim.transform.FindLoop("cf_J_Root");
			if (null != base.objBodyBone)
			{
				aaWeightsBody.CreateBoneList(base.objBodyBone, string.Empty);
				NeckLookControllerVer2[] componentsInChildren = base.objBodyBone.GetComponentsInChildren<NeckLookControllerVer2>(includeInactive: true);
				if (componentsInChildren.Length != 0)
				{
					base.neckLookCtrl = componentsInChildren[0];
				}
				if (null != base.neckLookCtrl)
				{
					ChangeLookNeckTarget(base.fileStatus.neckTargetType);
					ChangeLookNeckPtn(0);
				}
				InitShapeBody(base.objBodyBone.transform);
				InitShapeHand(base.objBodyBone.transform);
				base.objNeckLookTargetP = new GameObject("N_NeckLookTargetP");
				Transform trfNeckLookTarget = base.cmpBoneBody.targetEtc.trfNeckLookTarget;
				if (null != base.objNeckLookTargetP && null != trfNeckLookTarget)
				{
					base.objNeckLookTargetP.transform.SetParent(trfNeckLookTarget, worldPositionStays: false);
					base.objNeckLookTarget = new GameObject("N_NeckLookTarget");
					if (null != base.objNeckLookTarget)
					{
						base.objNeckLookTarget.transform.SetParent(base.objNeckLookTargetP.transform, worldPositionStays: false);
					}
				}
				base.objEyesLookTargetP = new GameObject("N_EyesLookTargetP");
				trfNeckLookTarget = base.cmpBoneBody.targetEtc.trfHeadParent;
				if (null != base.objEyesLookTargetP && null != trfNeckLookTarget)
				{
					base.objEyesLookTargetP.transform.SetParent(trfNeckLookTarget, worldPositionStays: false);
					base.objEyesLookTarget = new GameObject("N_EyesLookTarget");
					if (null != base.objEyesLookTarget)
					{
						base.objEyesLookTarget.transform.SetParent(base.objEyesLookTargetP.transform, worldPositionStays: false);
					}
				}
				if (base.sex == 0)
				{
					base.cmpBoneBody.InactiveBustDynamicBoneCollider();
				}
			}
			base.fullBodyIK = base.objAnim.GetComponent<FullBodyBipedIK>();
			base.fullBodyIK.solver.Initiate(base.fullBodyIK.transform);
			if (asyncFlags)
			{
				yield return StartCoroutine(Load_Coroutine("chara/oo_base.unity3d", "p_cf_head_bone", delegate(GameObject x)
				{
					base.objHeadBone = x;
				}, isClone: true, "abdata"));
			}
			else
			{
				base.objHeadBone = CommonLib.LoadAsset<GameObject>("chara/oo_base.unity3d", "p_cf_head_bone", clone: true, "abdata");
			}
			Singleton<Character>.Instance.AddLoadAssetBundle("chara/oo_base.unity3d", "abdata");
			if (null != base.objHeadBone)
			{
				base.cmpBoneHead = base.objHeadBone.GetComponent<CmpBoneHead>();
				base.objHeadBone.transform.SetParent(base.cmpBoneBody.targetEtc.trfHeadParent, worldPositionStays: false);
				EyeLookController[] componentsInChildren2 = base.objHeadBone.GetComponentsInChildren<EyeLookController>(includeInactive: true);
				if (componentsInChildren2.Length != 0)
				{
					base.eyeLookCtrl = componentsInChildren2[0];
				}
				if (null != base.eyeLookCtrl)
				{
					EyeLookCalc component = base.eyeLookCtrl.GetComponent<EyeLookCalc>();
					if (null != component)
					{
						ChangeLookEyesTarget(base.fileStatus.eyesTargetType);
						ChangeLookEyesPtn(0);
					}
				}
				aaWeightsHead.CreateBoneList(base.objHeadBone, string.Empty);
				string bodyAsset = ChaABDefine.BodyAsset(base.sex);
				if (asyncFlags)
				{
					yield return StartCoroutine(Load_Coroutine("chara/oo_base.unity3d", bodyAsset, delegate(GameObject x)
					{
						base.objBody = x;
					}, isClone: true, "abdata"));
				}
				else
				{
					base.objBody = CommonLib.LoadAsset<GameObject>("chara/oo_base.unity3d", bodyAsset, clone: true, "abdata");
				}
				Singleton<Character>.Instance.AddLoadAssetBundle("chara/oo_base.unity3d", "abdata");
				if (null != base.objBody)
				{
					base.cmpBody = base.objBody.GetComponent<CmpBody>();
					SetBodyBaseMaterial();
					base.objBody.transform.SetParent(base.objTop.transform, worldPositionStays: false);
					aaWeightsBody.AssignedWeightsAndSetBounds(base.objBody, "cf_J_Root", ChaControlDefine.bounds, base.cmpBoneBody.targetEtc.trfRoot);
					if (base.sex == 1)
					{
						bustNormal = new BustNormal();
						bustNormal.Init(base.objBody, "chara/oo_base.unity3d", "p_cf_body_00_Nml", "abdata");
					}
					ChangeCustomBodyWithoutCustomTexture();
				}
				string simpleBodyAsset = ChaABDefine.SilhouetteAsset(base.sex);
				if (asyncFlags)
				{
					yield return StartCoroutine(Load_Coroutine("chara/oo_base.unity3d", simpleBodyAsset, delegate(GameObject x)
					{
						base.objSimpleBody = x;
					}, isClone: true, "abdata"));
				}
				else
				{
					base.objSimpleBody = CommonLib.LoadAsset<GameObject>("chara/oo_base.unity3d", simpleBodyAsset, clone: true, "abdata");
				}
				Singleton<Character>.Instance.AddLoadAssetBundle("chara/oo_base.unity3d", "abdata");
				if (null != base.objSimpleBody)
				{
					base.cmpSimpleBody = base.objSimpleBody.GetComponent<CmpBody>();
					Color color = ((base.sex != 0) ? new Color(230f, 115f, 115f) : new Color(115f, 175f, 230f));
					if (Singleton<Config>.IsInstance() && Config.GraphicData != null)
					{
						color = Config.GraphicData.SilhouetteColor;
					}
					ChangeSimpleBodyColor(color);
					base.objSimpleBody.transform.SetParent(base.objTop.transform, worldPositionStays: false);
					aaWeightsBody.AssignedWeightsAndSetBounds(base.objSimpleBody, "cf_J_Root", ChaControlDefine.bounds, base.cmpBoneBody.targetEtc.trfRoot);
				}
				InitializeAccessoryParent();
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
				if (asyncFlags)
				{
					yield return StartCoroutine(ChangeHairAllAsync(forceChange: true));
				}
				else
				{
					ChangeHairAll(forceChange: true);
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
				UpdateSiru(forceChange: true);
				base.updateWet = true;
				ChangeHohoAkaRate(base.fileStatus.hohoAkaRate);
				if (reflectStatus)
				{
					base.chaFile.SetStatusBytes(status);
				}
				if (Singleton<Character>.Instance.enableCharaLoadGCClear)
				{
					OutputLog.Log("LoadAsync", unityLog: false, "UnloadUnusedAssets");
					UnityEngine.Resources.UnloadUnusedAssets();
					GC.Collect();
				}
				base.loadEnd = true;
			}
			else
			{
				OutputLog.Error("assetBundleName：chara/oo_base.unity3d  AssetName：p_cf_head_bone\u3000の読み込みに失敗", unityLog: true, "CharaLoad");
			}
		}
		else
		{
			OutputLog.Error("assetBundleName：chara/oo_base.unity3d  AssetName：p_cf_anim\u3000の読み込みに失敗", unityLog: true, "CharaLoad");
		}
	}

	public bool Reload(bool noChangeClothes = false, bool noChangeHead = false, bool noChangeHair = false, bool noChangeBody = false, bool forceChange = true)
	{
		StartCoroutine(ReloadAsync(noChangeClothes, noChangeHead, noChangeHair, noChangeBody, forceChange, asyncFlags: false));
		return true;
	}

	public IEnumerator ReloadAsync(bool noChangeClothes = false, bool noChangeHead = false, bool noChangeHair = false, bool noChangeBody = false, bool forceChange = true, bool asyncFlags = true)
	{
		if (asyncFlags)
		{
			SetActiveTop(active: false);
		}
		if (!noChangeBody)
		{
			AddUpdateCMBodyTexFlags(inpBase: true, inpPaint01: true, inpPaint02: true, inpSunburn: true);
			AddUpdateCMBodyGlossFlags(inpPaint01: true, inpPaint02: true);
			AddUpdateCMBodyColorFlags(inpBase: true, inpPaint01: true, inpPaint02: true, inpSunburn: true);
			AddUpdateCMBodyLayoutFlags(inpPaint01: true, inpPaint02: true);
			CreateBodyTexture();
			ChangeCustomBodyWithoutCustomTexture();
			if (noChangeHead)
			{
				AddUpdateCMFaceTexFlags(inpBase: true, inpEyeshadow: true, inpPaint01: true, inpPaint02: true, inpCheek: true, inpLip: true, inpMole: true);
				AddUpdateCMFaceGlossFlags(inpEyeshadow: true, inpPaint01: true, inpPaint02: true, inpCheek: true, inpLip: true);
				AddUpdateCMFaceColorFlags(inpBase: true, inpEyeshadow: true, inpPaint01: true, inpPaint02: true, inpCheek: true, inpLip: true, inpMole: true);
				AddUpdateCMFaceLayoutFlags(inpPaint01: true, inpPaint02: true, inpMole: true);
				CreateFaceTexture();
				SetFaceBaseMaterial();
				ChangeCustomFaceWithoutCustomTexture();
			}
		}
		if (!noChangeHead)
		{
			if (asyncFlags)
			{
				yield return StartCoroutine(ChangeHeadAsync(forceChange));
			}
			else
			{
				ChangeHead(forceChange);
			}
		}
		if (!noChangeHair)
		{
			if (asyncFlags)
			{
				yield return StartCoroutine(ChangeHairAllAsync(forceChange));
			}
			else
			{
				ChangeHairAll(forceChange);
			}
		}
		if (!noChangeClothes)
		{
			underMaskReflectionType = -1;
			underMaskBreakDisable = false;
			if (asyncFlags)
			{
				yield return StartCoroutine(ChangeClothesAsync(forceChange));
			}
			else
			{
				ChangeClothes(forceChange);
			}
			if (asyncFlags)
			{
				yield return StartCoroutine(ChangeAccessoryAsync(forceChange));
			}
			else
			{
				ChangeAccessory(forceChange);
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
		UpdateClothesStateAll();
		if (Singleton<Character>.Instance.enableCharaLoadGCClear)
		{
			OutputLog.Log("ReloadAsync", unityLog: false, "UnloadUnusedAssets");
			UnityEngine.Resources.UnloadUnusedAssets();
			GC.Collect();
		}
		if (asyncFlags)
		{
			yield return null;
		}
	}

	public void ChangeHead(bool forceChange = false)
	{
		StartCoroutine(ChangeHeadAsync(base.fileFace.headId, forceChange, asyncFlags: false));
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
		if (_headId == -1 || (!forceChange && null != base.objHead && _headId == base.fileFace.headId))
		{
			yield break;
		}
		string objName = "ct_head";
		if (null != base.objHead)
		{
			SafeDestroy(base.objHead);
			base.objHead = null;
			base.infoHead = null;
			base.cmpFace = null;
			ReleaseShapeFace();
			if (Singleton<Character>.Instance.customLoadGCClear)
			{
				UnityEngine.Resources.UnloadUnusedAssets();
				GC.Collect();
			}
		}
		int category = ((base.sex != 0) ? 210 : 110);
		int defId = ((base.sex != 0) ? 0 : 0);
		if (asyncFlags)
		{
			IEnumerator cor = LoadCharaFbxDataAsync(delegate(GameObject o)
			{
				base.objHead = o;
			}, category, _headId, objName, copyDynamicBone: false, 0, null, defId);
			yield return StartCoroutine(cor);
		}
		else
		{
			base.objHead = LoadCharaFbxData(category, _headId, objName, copyDynamicBone: false, 0, null, defId);
		}
		if (null != base.objHead)
		{
			base.cmpFace = base.objHead.GetComponent<CmpFace>();
			CommonLib.CopySameNameTransform(base.objHeadBone.transform, base.objHead.transform);
			base.objHead.transform.SetParent(base.objHeadBone.transform, worldPositionStays: false);
			aaWeightsHead.AssignedWeightsAndSetBounds(base.objHead, "cf_J_FaceRoot", ChaControlDefine.bounds);
			if (asyncFlags)
			{
				yield return null;
			}
			ListInfoComponent libComponent = base.objHead.GetComponent<ListInfoComponent>();
			ListInfoBase listInfoBase = (base.infoHead = libComponent.data);
			ListInfoBase lib = listInfoBase;
			base.fileFace.headId = lib.Id;
			Dictionary<int, ListInfoBase> dictSkin = base.lstCtrl.GetCategoryInfo((base.sex != 0) ? ChaListDefine.CategoryNo.ft_skin_f : ChaListDefine.CategoryNo.mt_skin_f);
			List<int> lstSkin = (from x in dictSkin
				where x.Value.GetInfoInt(ChaListDefine.KeyType.HeadID) == base.fileFace.headId
				select x.Key).ToList();
			if (!lstSkin.Contains(base.fileFace.skinId))
			{
				base.fileFace.skinId = lstSkin[0];
			}
			string customManifest = lib.GetInfo(ChaListDefine.KeyType.MainManifest);
			string customAssetBundle = lib.GetInfo(ChaListDefine.KeyType.MainAB);
			string customAsset = lib.GetInfo(ChaListDefine.KeyType.MatData);
			InitBaseCustomTextureFace(customManifest, customAssetBundle, customAsset);
			AddUpdateCMFaceTexFlags(inpBase: true, inpEyeshadow: true, inpPaint01: true, inpPaint02: true, inpCheek: true, inpLip: true, inpMole: true);
			AddUpdateCMFaceGlossFlags(inpEyeshadow: true, inpPaint01: true, inpPaint02: true, inpCheek: true, inpLip: true);
			AddUpdateCMFaceColorFlags(inpBase: true, inpEyeshadow: true, inpPaint01: true, inpPaint02: true, inpCheek: true, inpLip: true, inpMole: true);
			AddUpdateCMFaceLayoutFlags(inpPaint01: true, inpPaint02: true, inpMole: true);
			CreateFaceTexture();
			SetFaceBaseMaterial();
			UpdateSiru(forceChange: true);
			base.updateWet = true;
			ChangeHohoAkaRate(base.fileStatus.hohoAkaRate);
			InitShapeFace(base.objHeadBone.transform, lib.GetInfo(ChaListDefine.KeyType.MainManifest), lib.GetInfo(ChaListDefine.KeyType.MainAB), lib.GetInfo(ChaListDefine.KeyType.ShapeAnime));
			ChangeCustomFaceWithoutCustomTexture();
			HideEyeHighlight(hide: false);
			base.fbsCtrl = base.objHead.GetComponent<FaceBlendShape>();
			if (null != base.fbsCtrl)
			{
				base.eyebrowCtrl = base.fbsCtrl.EyebrowCtrl;
				base.eyesCtrl = base.fbsCtrl.EyesCtrl;
				base.mouthCtrl = base.fbsCtrl.MouthCtrl;
				ChangeEyesBlinkFlag(base.fileStatus.eyesBlink);
				ChangeEyebrowPtn(base.fileStatus.eyebrowPtn);
				ChangeEyebrowOpenMax(base.fileStatus.eyebrowOpenMax);
				ChangeEyesPtn(base.fileStatus.eyesPtn);
				ChangeEyesOpenMax(base.fileStatus.eyesOpenMax);
				ChangeMouthPtn(base.fileStatus.mouthPtn);
				ChangeMouthOpenMax(base.fileStatus.mouthOpenMax);
				ChangeMouthOpenMin(base.fileStatus.mouthOpenMin);
			}
		}
		if (asyncFlags)
		{
			yield return null;
		}
	}

	public void ChangeHairAll(bool forceChange = false)
	{
		int[] array = (int[])Enum.GetValues(typeof(ChaFileDefine.HairKind));
		int[] array2 = array;
		foreach (int num in array2)
		{
			StartCoroutine(ChangeHairAsync(num, base.fileHair.parts[num].id, forceChange, asyncFlags: false));
		}
	}

	public IEnumerator ChangeHairAllAsync(bool forceChange = false)
	{
		int[] hairKind = (int[])Enum.GetValues(typeof(ChaFileDefine.HairKind));
		int[] array = hairKind;
		foreach (int i in array)
		{
			yield return StartCoroutine(ChangeHairAsync(i, base.fileHair.parts[i].id, forceChange));
		}
	}

	public bool ChangeHair(int kind, bool forceChange = false)
	{
		StartCoroutine(ChangeHairAsync(kind, base.fileHair.parts[kind].id, forceChange, asyncFlags: false));
		return true;
	}

	public void ChangeHair(int kind, int id, bool forceChange = false)
	{
		StartCoroutine(ChangeHairAsync(kind, id, forceChange, asyncFlags: false));
	}

	public IEnumerator ChangeHairAsync(int kind, int id, bool forceChange = false, bool asyncFlags = true)
	{
		ChaListDefine.CategoryNo[] fbxType = new ChaListDefine.CategoryNo[4]
		{
			ChaListDefine.CategoryNo.so_hair_b,
			ChaListDefine.CategoryNo.so_hair_f,
			ChaListDefine.CategoryNo.so_hair_s,
			ChaListDefine.CategoryNo.so_hair_o
		};
		string[] objName = new string[4] { "ct_hairB", "ct_hairF", "ct_hairS", "ct_hairO" };
		int[,] defId = new int[2, 4]
		{
			{ 0, 2, 0, 0 },
			{ 0, 1, 0, 0 }
		};
		if (!forceChange && null != base.objHair[kind] && id == base.fileHair.parts[kind].id)
		{
			yield break;
		}
		if (null != base.objHair[kind])
		{
			SafeDestroy(base.objHair[kind]);
			base.objHair[kind] = null;
			base.infoHair[kind] = null;
			base.cmpHair[kind] = null;
			if (id != base.fileHair.parts[kind].bundleId)
			{
				base.fileHair.parts[kind].dictBundle.Clear();
				base.fileHair.parts[kind].bundleId = -1;
			}
			if (Singleton<Character>.Instance.customLoadGCClear)
			{
				UnityEngine.Resources.UnloadUnusedAssets();
				GC.Collect();
			}
		}
		Transform trfHairParent = base.cmpBoneHead.targetEtc.trfHairParent;
		if (asyncFlags)
		{
			IEnumerator cor = LoadCharaFbxDataAsync(delegate(GameObject o)
			{
				base.objHair[kind] = o;
			}, (int)fbxType[kind], id, objName[kind], copyDynamicBone: false, 0, trfHairParent, defId[base.sex, kind]);
			yield return StartCoroutine(cor);
		}
		else
		{
			base.objHair[kind] = LoadCharaFbxData((int)fbxType[kind], id, objName[kind], copyDynamicBone: false, 0, trfHairParent, defId[base.sex, kind]);
		}
		if (null != base.objHair[kind])
		{
			ListInfoComponent component = base.objHair[kind].GetComponent<ListInfoComponent>();
			ListInfoBase listInfoBase = (base.infoHair[kind] = component.data);
			base.fileHair.parts[kind].id = listInfoBase.Id;
			if (kind == 0)
			{
				base.fileHair.kind = listInfoBase.Kind;
			}
			base.cmpHair[kind] = base.objHair[kind].GetComponent<CmpHair>();
			if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy" && null == base.cmpHair[kind])
			{
				Debug.LogWarning("髪情報を保持するコンポーネントがありません");
			}
			if (null != base.cmpHair[kind] && (id != base.fileHair.parts[kind].bundleId || base.fileHair.parts[kind].dictBundle.Count != base.cmpHair[kind].boneInfo.Length))
			{
				base.fileHair.parts[kind].dictBundle.Clear();
				for (int i = 0; i < base.cmpHair[kind].boneInfo.Length; i++)
				{
					base.fileHair.parts[kind].dictBundle[i] = new ChaFileHair.PartsInfo.BundleInfo();
					SetDefaultHairCorrectPosRate(kind, i);
					SetDefaultHairCorrectRotRate(kind, i);
				}
			}
			base.fileHair.parts[kind].bundleId = id;
			ChangeSettingHairColor(kind, _main: true, _top: true, _under: true);
			ChangeSettingHairSpecular(kind);
			ChangeSettingHairMetallic(kind);
			ChangeSettingHairSmoothness(kind);
			ChangeSettingHairAcsColor(kind);
			ChangeSettingHairCorrectPosAll(kind);
			ChangeSettingHairCorrectRotAll(kind);
			ChangeSettingHairShader();
			if (Game.isAdd30)
			{
				ChangeSettingHairMeshType(kind);
				ChangeSettingHairMeshColor(kind);
				ChangeSettingHairMeshLayout(kind);
			}
		}
		base.updateWet = true;
		if (asyncFlags)
		{
			yield return null;
		}
	}

	public void ChangeClothes(bool forceChange = false)
	{
		int[] array = (int[])Enum.GetValues(typeof(ChaFileDefine.ClothesKind));
		int[] array2 = array;
		foreach (int num in array2)
		{
			StartCoroutine(ChangeClothesAsync(num, nowCoordinate.clothes.parts[num].id, forceChange, asyncFlags: false));
		}
	}

	public void ChangeClothes(int kind, int id, bool forceChange = false)
	{
		StartCoroutine(ChangeClothesAsync(kind, id, forceChange, asyncFlags: false));
	}

	public IEnumerator ChangeClothesAsync(bool forceChange = false)
	{
		int[] clothesKind = (int[])Enum.GetValues(typeof(ChaFileDefine.ClothesKind));
		int[] array = clothesKind;
		foreach (int i in array)
		{
			yield return StartCoroutine(ChangeClothesAsync(i, nowCoordinate.clothes.parts[i].id, forceChange));
		}
	}

	public IEnumerator ChangeClothesAsync(int kind, int id, bool forceChange = false, bool asyncFlags = true)
	{
		if (asyncFlags)
		{
			switch (kind)
			{
			case 0:
				yield return StartCoroutine(ChangeClothesTopAsync(id, forceChange));
				updateAlphaMask = true;
				updateAlphaMask2 = true;
				break;
			case 1:
				yield return StartCoroutine(ChangeClothesBotAsync(id, forceChange));
				updateAlphaMask2 = true;
				break;
			case 2:
				yield return StartCoroutine(ChangeClothesInnerTAsync(id, forceChange));
				break;
			case 3:
				yield return StartCoroutine(ChangeClothesInnerBAsync(id, forceChange));
				break;
			case 4:
				yield return StartCoroutine(ChangeClothesGlovesAsync(id, forceChange));
				break;
			case 5:
				yield return StartCoroutine(ChangeClothesPanstAsync(id, forceChange));
				break;
			case 6:
				yield return StartCoroutine(ChangeClothesSocksAsync(id, forceChange));
				break;
			case 7:
				yield return StartCoroutine(ChangeClothesShoesAsync(id, forceChange));
				break;
			}
		}
		else
		{
			switch (kind)
			{
			case 0:
				ChangeClothesTop(id, forceChange);
				updateAlphaMask = true;
				updateAlphaMask2 = true;
				break;
			case 1:
				ChangeClothesBot(id, forceChange);
				updateAlphaMask2 = true;
				break;
			case 2:
				ChangeClothesInnerT(id, forceChange);
				break;
			case 3:
				ChangeClothesInnerB(id, forceChange);
				break;
			case 4:
				ChangeClothesGloves(id, forceChange);
				break;
			case 5:
				ChangeClothesPanst(id, forceChange);
				break;
			case 6:
				ChangeClothesSocks(id, forceChange);
				break;
			case 7:
				ChangeClothesShoes(id, forceChange);
				break;
			}
		}
	}

	public void ChangeClothesTop(int id, bool forceChange = false)
	{
		StartCoroutine(ChangeClothesTopAsync(id, forceChange, asyncFlags: false));
	}

	public IEnumerator ChangeClothesTopAsync(int id, bool forceChange = false, bool asyncFlags = true)
	{
		updateAlphaMask = true;
		updateAlphaMask2 = true;
		bool load = true;
		bool release = true;
		int kindNo = 0;
		string objName = "ct_clothesTop";
		int nowId = ((base.infoClothes[kindNo] != null) ? base.infoClothes[kindNo].Id : (-1));
		if (!forceChange && null != base.objClothes[kindNo] && id == nowId)
		{
			load = false;
			release = false;
		}
		ReleaseBaseCustomTextureClothes(kindNo);
		if (release && null != base.objClothes[kindNo])
		{
			SafeDestroy(base.objClothes[kindNo]);
			base.objClothes[kindNo] = null;
			base.infoClothes[kindNo] = null;
			base.cmpClothes[kindNo] = null;
			notInnerT = false;
			notBot = false;
			RemoveClothesStateKind(kindNo);
			ReleaseAlphaMaskTexture(0);
			ReleaseAlphaMaskTexture(1);
			if (underMaskReflectionType != 1)
			{
				ReleaseAlphaMaskTexture(2);
				ReleaseAlphaMaskTexture(3);
				ReleaseAlphaMaskTexture(4);
				ReleaseAlphaMaskTexture(5);
				underMaskReflectionType = -1;
			}
			if (Singleton<Character>.Instance.customLoadGCClear)
			{
				UnityEngine.Resources.UnloadUnusedAssets();
				GC.Collect();
			}
		}
		if (load)
		{
			if (asyncFlags)
			{
				yield return null;
			}
			if (asyncFlags)
			{
				IEnumerator cor = LoadCharaFbxDataAsync(delegate(GameObject o)
				{
					base.objClothes[kindNo] = o;
				}, (base.sex != 0) ? 240 : 140, id, objName, copyDynamicBone: true, 1, base.objTop.transform, (base.sex != 0) ? 0 : 0);
				yield return StartCoroutine(cor);
			}
			else
			{
				base.objClothes[kindNo] = LoadCharaFbxData((base.sex != 0) ? 240 : 140, id, objName, copyDynamicBone: true, 1, base.objTop.transform, (base.sex != 0) ? 0 : 0);
			}
			if (asyncFlags)
			{
				yield return null;
			}
			if (null != base.objClothes[kindNo])
			{
				ListInfoComponent component = base.objClothes[kindNo].GetComponent<ListInfoComponent>();
				ListInfoBase listInfoBase = (base.infoClothes[kindNo] = component.data);
				base.cmpClothes[kindNo] = base.objClothes[kindNo].GetComponent<CmpClothes>();
				if (null != base.cmpClothes[kindNo])
				{
					base.cmpClothes[kindNo].InitDynamicBones();
				}
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy" && null == base.cmpClothes[kindNo])
				{
					Debug.LogWarning("服情報を保持するコンポーネントがありません");
				}
				nowCoordinate.clothes.parts[kindNo].id = listInfoBase.Id;
				notInnerT = listInfoBase.GetInfoInt(ChaListDefine.KeyType.NotBra) == 1;
				notBot = listInfoBase.GetInfoInt(ChaListDefine.KeyType.Coordinate) == 1;
				AddClothesStateKind(kindNo, listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
				LoadAlphaMaskTexture(listInfoBase.GetInfo(ChaListDefine.KeyType.OverBodyMaskAB), listInfoBase.GetInfo(ChaListDefine.KeyType.OverBodyMask), 0);
				if ((bool)base.customMatBody)
				{
					base.customMatBody.SetTexture(ChaShader.AlphaMask, texBodyAlphaMask);
				}
				LoadAlphaMaskTexture(listInfoBase.GetInfo(ChaListDefine.KeyType.OverBraMaskAB), listInfoBase.GetInfo(ChaListDefine.KeyType.OverBraMask), 1);
				if (base.rendBra != null && (bool)base.rendBra[0])
				{
					base.rendBra[0].material.SetTexture(ChaShader.AlphaMask, texBraAlphaMask);
				}
				if (LoadAlphaMaskTexture(listInfoBase.GetInfo(ChaListDefine.KeyType.OverBodyBMaskAB), listInfoBase.GetInfo(ChaListDefine.KeyType.OverBodyBMask), 5))
				{
					if ((bool)base.customMatBody)
					{
						base.customMatBody.SetTexture(ChaShader.AlphaMask2, texBodyBAlphaMask);
					}
					LoadAlphaMaskTexture(listInfoBase.GetInfo(ChaListDefine.KeyType.OverInnerTBMaskAB), listInfoBase.GetInfo(ChaListDefine.KeyType.OverInnerTBMask), 2);
					if (null != base.rendInnerTB)
					{
						base.rendInnerTB.material.SetTexture(ChaShader.AlphaMask2, texInnerTBAlphaMask);
					}
					LoadAlphaMaskTexture(listInfoBase.GetInfo(ChaListDefine.KeyType.OverInnerBMaskAB), listInfoBase.GetInfo(ChaListDefine.KeyType.OverInnerBMask), 3);
					if (null != base.rendInnerB)
					{
						base.rendInnerB.material.SetTexture(ChaShader.AlphaMask2, texInnerBAlphaMask);
					}
					LoadAlphaMaskTexture(listInfoBase.GetInfo(ChaListDefine.KeyType.OverPanstMaskAB), listInfoBase.GetInfo(ChaListDefine.KeyType.OverPanstMask), 4);
					if (null != base.rendPanst)
					{
						base.rendPanst.material.SetTexture(ChaShader.AlphaMask2, texPanstAlphaMask);
					}
					underMaskReflectionType = 0;
					underMaskBreakDisable = "1" == listInfoBase.GetInfo(ChaListDefine.KeyType.BreakDisableMask);
				}
			}
		}
		if (null != base.objClothes[kindNo])
		{
			InitBaseCustomTextureClothes(kindNo);
			if (base.loadWithDefaultColorAndPtn)
			{
				SetClothesDefaultSetting(kindNo);
				for (int i = 0; i < 3; i++)
				{
					nowCoordinate.clothes.parts[kindNo].colorInfo[i].pattern = 0;
				}
			}
			ChangeCustomClothes(kindNo, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true);
			if (base.releaseCustomInputTexture)
			{
				ReleaseBaseCustomTextureClothes(kindNo, createTex: false);
			}
			ChangeBreakClothes(kindNo);
		}
		UpdateSiru(forceChange: true);
		base.updateWet = true;
		if ("0" == base.infoClothes[kindNo].GetInfo(ChaListDefine.KeyType.Coordinate))
		{
			int botKind = 1;
			if (null == base.objClothes[botKind])
			{
				if (asyncFlags)
				{
					yield return StartCoroutine(ChangeClothesBotAsync(nowCoordinate.clothes.parts[botKind].id));
				}
				else
				{
					ChangeClothesBot(nowCoordinate.clothes.parts[botKind].id);
				}
			}
		}
		else
		{
			if (!IsClothes(1))
			{
				yield break;
			}
			ListInfoBase listInfoBase2 = base.infoClothes[1];
			if (listInfoBase2 != null && "0" != listInfoBase2.GetInfo(ChaListDefine.KeyType.OverBodyBMaskAB) && LoadAlphaMaskTexture(listInfoBase2.GetInfo(ChaListDefine.KeyType.OverBodyBMaskAB), listInfoBase2.GetInfo(ChaListDefine.KeyType.OverBodyBMask), 5))
			{
				if ((bool)base.customMatBody)
				{
					base.customMatBody.SetTexture(ChaShader.AlphaMask2, texBodyBAlphaMask);
				}
				LoadAlphaMaskTexture(listInfoBase2.GetInfo(ChaListDefine.KeyType.OverInnerTBMaskAB), listInfoBase2.GetInfo(ChaListDefine.KeyType.OverInnerTBMask), 2);
				if (null != base.rendInnerTB)
				{
					base.rendInnerTB.material.SetTexture(ChaShader.AlphaMask2, texInnerTBAlphaMask);
				}
				LoadAlphaMaskTexture(listInfoBase2.GetInfo(ChaListDefine.KeyType.OverInnerBMaskAB), listInfoBase2.GetInfo(ChaListDefine.KeyType.OverInnerBMask), 3);
				if (null != base.rendInnerB)
				{
					base.rendInnerB.material.SetTexture(ChaShader.AlphaMask2, texInnerBAlphaMask);
				}
				LoadAlphaMaskTexture(listInfoBase2.GetInfo(ChaListDefine.KeyType.OverPanstMaskAB), listInfoBase2.GetInfo(ChaListDefine.KeyType.OverPanstMask), 4);
				if (null != base.rendPanst)
				{
					base.rendPanst.material.SetTexture(ChaShader.AlphaMask2, texPanstAlphaMask);
				}
				underMaskReflectionType = 1;
				underMaskBreakDisable = "1" == listInfoBase2.GetInfo(ChaListDefine.KeyType.BreakDisableMask);
			}
		}
	}

	public void ChangeClothesBot(int id, bool forceChange = false)
	{
		StartCoroutine(ChangeClothesBotAsync(id, forceChange, asyncFlags: false));
	}

	public IEnumerator ChangeClothesBotAsync(int id, bool forceChange = false, bool asyncFlags = true)
	{
		updateAlphaMask2 = true;
		bool load = true;
		bool release = true;
		int kindNo = 1;
		string objName = "ct_clothesBot";
		int nowId = ((base.infoClothes[kindNo] != null) ? base.infoClothes[kindNo].Id : (-1));
		if (!forceChange && null != base.objClothes[kindNo] && id == nowId)
		{
			load = false;
			release = false;
		}
		ChaListDefine.CategoryNo cateTop = ((base.sex != 0) ? ChaListDefine.CategoryNo.fo_top : ChaListDefine.CategoryNo.mo_top);
		Dictionary<int, ListInfoBase> dictFbx = base.lstCtrl.GetCategoryInfo(cateTop);
		if (dictFbx.Count != 0)
		{
			ListInfoBase value = null;
			if (dictFbx.TryGetValue(nowCoordinate.clothes.parts[0].id, out value) && "1" == value.GetInfo(ChaListDefine.KeyType.Coordinate))
			{
				load = false;
				release = true;
			}
		}
		ReleaseBaseCustomTextureClothes(kindNo);
		if (release && null != base.objClothes[kindNo])
		{
			SafeDestroy(base.objClothes[kindNo]);
			base.objClothes[kindNo] = null;
			base.infoClothes[kindNo] = null;
			base.cmpClothes[kindNo] = null;
			if (underMaskReflectionType != 0)
			{
				ReleaseAlphaMaskTexture(2);
				ReleaseAlphaMaskTexture(3);
				ReleaseAlphaMaskTexture(4);
				ReleaseAlphaMaskTexture(5);
				underMaskReflectionType = -1;
			}
			RemoveClothesStateKind(kindNo);
			if (Singleton<Character>.Instance.customLoadGCClear)
			{
				UnityEngine.Resources.UnloadUnusedAssets();
				GC.Collect();
			}
		}
		if (load)
		{
			if (asyncFlags)
			{
				yield return null;
			}
			if (asyncFlags)
			{
				IEnumerator cor = LoadCharaFbxDataAsync(delegate(GameObject o)
				{
					base.objClothes[kindNo] = o;
				}, (base.sex != 0) ? 241 : 141, id, objName, copyDynamicBone: true, 1, base.objTop.transform, (base.sex != 0) ? 0 : 0);
				yield return StartCoroutine(cor);
			}
			else
			{
				base.objClothes[kindNo] = LoadCharaFbxData((base.sex != 0) ? 241 : 141, id, objName, copyDynamicBone: true, 1, base.objTop.transform, (base.sex != 0) ? 0 : 0);
			}
			if (asyncFlags)
			{
				yield return null;
			}
			if (null != base.objClothes[kindNo])
			{
				ListInfoComponent component = base.objClothes[kindNo].GetComponent<ListInfoComponent>();
				ListInfoBase listInfoBase = (base.infoClothes[kindNo] = component.data);
				base.cmpClothes[kindNo] = base.objClothes[kindNo].GetComponent<CmpClothes>();
				if (null != base.cmpClothes[kindNo])
				{
					base.cmpClothes[kindNo].InitDynamicBones();
				}
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy" && null == base.cmpClothes[kindNo])
				{
					Debug.LogWarning("服情報を保持するコンポーネントがありません");
				}
				nowCoordinate.clothes.parts[kindNo].id = listInfoBase.Id;
				AddClothesStateKind(kindNo, listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
				if (LoadAlphaMaskTexture(listInfoBase.GetInfo(ChaListDefine.KeyType.OverBodyBMaskAB), listInfoBase.GetInfo(ChaListDefine.KeyType.OverBodyBMask), 5))
				{
					if ((bool)base.customMatBody)
					{
						base.customMatBody.SetTexture(ChaShader.AlphaMask2, texBodyBAlphaMask);
					}
					LoadAlphaMaskTexture(listInfoBase.GetInfo(ChaListDefine.KeyType.OverInnerTBMaskAB), listInfoBase.GetInfo(ChaListDefine.KeyType.OverInnerTBMask), 2);
					if (null != base.rendInnerTB)
					{
						base.rendInnerTB.material.SetTexture(ChaShader.AlphaMask2, texInnerTBAlphaMask);
					}
					LoadAlphaMaskTexture(listInfoBase.GetInfo(ChaListDefine.KeyType.OverInnerBMaskAB), listInfoBase.GetInfo(ChaListDefine.KeyType.OverInnerBMask), 3);
					if (null != base.rendInnerB)
					{
						base.rendInnerB.material.SetTexture(ChaShader.AlphaMask2, texInnerBAlphaMask);
					}
					LoadAlphaMaskTexture(listInfoBase.GetInfo(ChaListDefine.KeyType.OverPanstMaskAB), listInfoBase.GetInfo(ChaListDefine.KeyType.OverPanstMask), 4);
					if (null != base.rendPanst)
					{
						base.rendPanst.material.SetTexture(ChaShader.AlphaMask2, texPanstAlphaMask);
					}
					underMaskReflectionType = 1;
					underMaskBreakDisable = "1" == listInfoBase.GetInfo(ChaListDefine.KeyType.BreakDisableMask);
				}
			}
		}
		if (null != base.objClothes[kindNo])
		{
			InitBaseCustomTextureClothes(kindNo);
			if (base.loadWithDefaultColorAndPtn)
			{
				SetClothesDefaultSetting(kindNo);
				for (int i = 0; i < 3; i++)
				{
					nowCoordinate.clothes.parts[kindNo].colorInfo[i].pattern = 0;
				}
			}
			ChangeCustomClothes(kindNo, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true);
			if (base.releaseCustomInputTexture)
			{
				ReleaseBaseCustomTextureClothes(kindNo, createTex: false);
			}
			ChangeBreakClothes(kindNo);
		}
		UpdateSiru(forceChange: true);
		base.updateWet = true;
	}

	public void ChangeClothesInnerT(int id, bool forceChange = false)
	{
		StartCoroutine(ChangeClothesInnerTAsync(id, forceChange, asyncFlags: false));
	}

	public IEnumerator ChangeClothesInnerTAsync(int id, bool forceChange = false, bool asyncFlags = true)
	{
		bool load = true;
		bool release = true;
		int kindNo = 2;
		string objName = "ct_inner_t";
		int nowId = ((base.infoClothes[kindNo] != null) ? base.infoClothes[kindNo].Id : (-1));
		if (!forceChange && null != base.objClothes[kindNo] && id == nowId)
		{
			load = false;
			release = false;
		}
		ReleaseBaseCustomTextureClothes(kindNo);
		if (release && null != base.objClothes[kindNo])
		{
			SafeDestroy(base.objClothes[kindNo]);
			base.objClothes[kindNo] = null;
			base.infoClothes[kindNo] = null;
			base.cmpClothes[kindNo] = null;
			ReleaseRefObject(7uL);
			notInnerB = false;
			RemoveClothesStateKind(kindNo);
			base.rendBra = new Renderer[2];
			base.rendInnerTB = null;
			if (Singleton<Character>.Instance.customLoadGCClear)
			{
				UnityEngine.Resources.UnloadUnusedAssets();
				GC.Collect();
			}
		}
		if (load)
		{
			if (asyncFlags)
			{
				yield return null;
			}
			if (asyncFlags)
			{
				IEnumerator cor = LoadCharaFbxDataAsync(delegate(GameObject o)
				{
					base.objClothes[kindNo] = o;
				}, 242, id, objName, copyDynamicBone: false, 1, base.objTop.transform, 0);
				yield return StartCoroutine(cor);
			}
			else
			{
				base.objClothes[kindNo] = LoadCharaFbxData(242, id, objName, copyDynamicBone: false, 1, base.objTop.transform, 0);
			}
			if (asyncFlags)
			{
				yield return null;
			}
			if (null != base.objClothes[kindNo])
			{
				ListInfoComponent component = base.objClothes[kindNo].GetComponent<ListInfoComponent>();
				ListInfoBase listInfoBase = (base.infoClothes[kindNo] = component.data);
				base.cmpClothes[kindNo] = base.objClothes[kindNo].GetComponent<CmpClothes>();
				if (null != base.cmpClothes[kindNo])
				{
					base.cmpClothes[kindNo].InitDynamicBones();
				}
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy" && null == base.cmpClothes[kindNo])
				{
					Debug.LogWarning("服情報を保持するコンポーネントがありません");
				}
				notInnerB = (("1" == listInfoBase.GetInfo(ChaListDefine.KeyType.Coordinate)) ? true : false);
				nowCoordinate.clothes.parts[kindNo].id = listInfoBase.Id;
				CreateReferenceInfo(7uL, base.objClothes[kindNo]);
				AddClothesStateKind(kindNo, listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
				GameObject referenceInfo = GetReferenceInfo(RefObjKey.mask_braA);
				if (null != referenceInfo)
				{
					base.rendBra[0] = referenceInfo.GetComponent<Renderer>();
				}
				if (base.rendBra != null && (bool)base.rendBra[0])
				{
					base.rendBra[0].material.SetTexture(ChaShader.AlphaMask, texBraAlphaMask);
				}
				byte b = base.fileStatus.clothesState[0];
				byte[,] array = new byte[3, 2]
				{
					{ 1, 1 },
					{ 0, 1 },
					{ 0, 0 }
				};
				ChangeAlphaMask(array[b, 0], array[b, 1]);
				ChangeAlphaMaskEx();
				GameObject referenceInfo2 = GetReferenceInfo(RefObjKey.mask_innerTB);
				if (null != referenceInfo2)
				{
					base.rendInnerTB = referenceInfo2.GetComponent<Renderer>();
				}
				if (null != base.rendInnerTB)
				{
					base.rendInnerTB.material.SetTexture(ChaShader.AlphaMask2, texInnerTBAlphaMask);
				}
				ChangeAlphaMask2();
			}
		}
		if (null != base.objClothes[kindNo])
		{
			InitBaseCustomTextureClothes(kindNo);
			if (base.loadWithDefaultColorAndPtn)
			{
				SetClothesDefaultSetting(kindNo);
				for (int i = 0; i < 3; i++)
				{
					nowCoordinate.clothes.parts[kindNo].colorInfo[i].pattern = 0;
				}
			}
			ChangeCustomClothes(kindNo, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true);
			if (base.releaseCustomInputTexture)
			{
				ReleaseBaseCustomTextureClothes(kindNo, createTex: false);
			}
			ChangeBreakClothes(kindNo);
		}
		UpdateSiru(forceChange: true);
		base.updateWet = true;
		if (!("0" == base.infoClothes[kindNo].GetInfo(ChaListDefine.KeyType.Coordinate)))
		{
			yield break;
		}
		int kindInnerB = 3;
		if (null == base.objClothes[kindInnerB])
		{
			if (asyncFlags)
			{
				yield return StartCoroutine(ChangeClothesInnerBAsync(nowCoordinate.clothes.parts[kindInnerB].id));
			}
			else
			{
				ChangeClothesInnerB(nowCoordinate.clothes.parts[kindInnerB].id);
			}
		}
	}

	public void ChangeClothesInnerB(int id, bool forceChange = false)
	{
		StartCoroutine(ChangeClothesInnerBAsync(id, forceChange, asyncFlags: false));
	}

	public IEnumerator ChangeClothesInnerBAsync(int id, bool forceChange = false, bool asyncFlags = true)
	{
		bool load = true;
		bool release = true;
		int kindNo = 3;
		string objName = "ct_inner_b";
		int nowId = ((base.infoClothes[kindNo] != null) ? base.infoClothes[kindNo].Id : (-1));
		if (!forceChange && null != base.objClothes[kindNo] && id == nowId)
		{
			load = false;
			release = false;
		}
		Dictionary<int, ListInfoBase> dictFbx = base.lstCtrl.GetCategoryInfo(ChaListDefine.CategoryNo.fo_inner_t);
		if (dictFbx.Count != 0)
		{
			ListInfoBase value = null;
			if (dictFbx.TryGetValue(nowCoordinate.clothes.parts[2].id, out value) && "1" == value.GetInfo(ChaListDefine.KeyType.Coordinate))
			{
				load = false;
				release = true;
			}
		}
		ReleaseBaseCustomTextureClothes(kindNo);
		if (release && null != base.objClothes[kindNo])
		{
			SafeDestroy(base.objClothes[kindNo]);
			base.objClothes[kindNo] = null;
			base.infoClothes[kindNo] = null;
			base.cmpClothes[kindNo] = null;
			ReleaseRefObject(8uL);
			RemoveClothesStateKind(kindNo);
			base.rendInnerB = null;
			if (Singleton<Character>.Instance.customLoadGCClear)
			{
				UnityEngine.Resources.UnloadUnusedAssets();
				GC.Collect();
			}
		}
		if (load)
		{
			if (asyncFlags)
			{
				yield return null;
			}
			if (asyncFlags)
			{
				IEnumerator cor = LoadCharaFbxDataAsync(delegate(GameObject o)
				{
					base.objClothes[kindNo] = o;
				}, 243, id, objName, copyDynamicBone: false, 1, base.objTop.transform, 0);
				yield return StartCoroutine(cor);
			}
			else
			{
				base.objClothes[kindNo] = LoadCharaFbxData(243, id, objName, copyDynamicBone: false, 1, base.objTop.transform, 0);
			}
			if (asyncFlags)
			{
				yield return null;
			}
			if (null != base.objClothes[kindNo])
			{
				ListInfoComponent component = base.objClothes[kindNo].GetComponent<ListInfoComponent>();
				ListInfoBase listInfoBase = (base.infoClothes[kindNo] = component.data);
				base.cmpClothes[kindNo] = base.objClothes[kindNo].GetComponent<CmpClothes>();
				if (null != base.cmpClothes[kindNo])
				{
					base.cmpClothes[kindNo].InitDynamicBones();
				}
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy" && null == base.cmpClothes[kindNo])
				{
					Debug.LogWarning("服情報を保持するコンポーネントがありません");
				}
				nowCoordinate.clothes.parts[kindNo].id = listInfoBase.Id;
				CreateReferenceInfo(8uL, base.objClothes[kindNo]);
				AddClothesStateKind(kindNo, listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
				GameObject referenceInfo = GetReferenceInfo(RefObjKey.mask_innerB);
				if (null != referenceInfo)
				{
					base.rendInnerB = referenceInfo.GetComponent<Renderer>();
				}
				if (null != base.rendInnerB)
				{
					base.rendInnerB.material.SetTexture(ChaShader.AlphaMask2, texInnerBAlphaMask);
				}
				ChangeAlphaMask2();
			}
		}
		if (null != base.objClothes[kindNo])
		{
			InitBaseCustomTextureClothes(kindNo);
			if (base.loadWithDefaultColorAndPtn)
			{
				SetClothesDefaultSetting(kindNo);
				for (int i = 0; i < 3; i++)
				{
					nowCoordinate.clothes.parts[kindNo].colorInfo[i].pattern = 0;
				}
			}
			ChangeCustomClothes(kindNo, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true);
			if (base.releaseCustomInputTexture)
			{
				ReleaseBaseCustomTextureClothes(kindNo, createTex: false);
			}
			ChangeBreakClothes(kindNo);
		}
		UpdateSiru(forceChange: true);
		base.updateWet = true;
	}

	public void ChangeClothesGloves(int id, bool forceChange = false)
	{
		StartCoroutine(ChangeClothesGlovesAsync(id, forceChange, asyncFlags: false));
	}

	public IEnumerator ChangeClothesGlovesAsync(int id, bool forceChange = false, bool asyncFlags = true)
	{
		bool load = true;
		bool release = true;
		int kindNo = 4;
		string objName = "ct_gloves";
		int nowId = ((base.infoClothes[kindNo] != null) ? base.infoClothes[kindNo].Id : (-1));
		if (!forceChange && null != base.objClothes[kindNo] && id == nowId)
		{
			load = false;
			release = false;
		}
		ReleaseBaseCustomTextureClothes(kindNo);
		if (release && null != base.objClothes[kindNo])
		{
			SafeDestroy(base.objClothes[kindNo]);
			base.objClothes[kindNo] = null;
			base.infoClothes[kindNo] = null;
			base.cmpClothes[kindNo] = null;
			RemoveClothesStateKind(kindNo);
			if (Singleton<Character>.Instance.customLoadGCClear)
			{
				UnityEngine.Resources.UnloadUnusedAssets();
				GC.Collect();
			}
		}
		if (load)
		{
			if (asyncFlags)
			{
				yield return null;
			}
			if (asyncFlags)
			{
				IEnumerator cor = LoadCharaFbxDataAsync(delegate(GameObject o)
				{
					base.objClothes[kindNo] = o;
				}, (base.sex != 0) ? 244 : 144, id, objName, copyDynamicBone: false, 1, base.objTop.transform, (base.sex != 0) ? 0 : 0);
				yield return StartCoroutine(cor);
			}
			else
			{
				base.objClothes[kindNo] = LoadCharaFbxData((base.sex != 0) ? 244 : 144, id, objName, copyDynamicBone: false, 1, base.objTop.transform, (base.sex != 0) ? 0 : 0);
			}
			if (asyncFlags)
			{
				yield return null;
			}
			if (null != base.objClothes[kindNo])
			{
				ListInfoComponent component = base.objClothes[kindNo].GetComponent<ListInfoComponent>();
				ListInfoBase listInfoBase = (base.infoClothes[kindNo] = component.data);
				base.cmpClothes[kindNo] = base.objClothes[kindNo].GetComponent<CmpClothes>();
				if (null != base.cmpClothes[kindNo])
				{
					base.cmpClothes[kindNo].InitDynamicBones();
				}
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy" && null == base.cmpClothes[kindNo])
				{
					Debug.LogWarning("服情報を保持するコンポーネントがありません");
				}
				nowCoordinate.clothes.parts[kindNo].id = listInfoBase.Id;
				AddClothesStateKind(kindNo, listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
			}
		}
		if (!(null != base.objClothes[kindNo]))
		{
			yield break;
		}
		InitBaseCustomTextureClothes(kindNo);
		if (base.loadWithDefaultColorAndPtn)
		{
			SetClothesDefaultSetting(kindNo);
			for (int i = 0; i < 3; i++)
			{
				nowCoordinate.clothes.parts[kindNo].colorInfo[i].pattern = 0;
			}
		}
		ChangeCustomClothes(kindNo, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true);
		if (base.releaseCustomInputTexture)
		{
			ReleaseBaseCustomTextureClothes(kindNo, createTex: false);
		}
		ChangeBreakClothes(kindNo);
	}

	public void ChangeClothesPanst(int id, bool forceChange = false)
	{
		StartCoroutine(ChangeClothesPanstAsync(id, forceChange, asyncFlags: false));
	}

	public IEnumerator ChangeClothesPanstAsync(int id, bool forceChange = false, bool asyncFlags = true)
	{
		bool load = true;
		bool release = true;
		int kindNo = 5;
		string objName = "ct_panst";
		int nowId = ((base.infoClothes[kindNo] != null) ? base.infoClothes[kindNo].Id : (-1));
		if (!forceChange && null != base.objClothes[kindNo] && id == nowId)
		{
			load = false;
			release = false;
		}
		ReleaseBaseCustomTextureClothes(kindNo);
		if (release && null != base.objClothes[kindNo])
		{
			SafeDestroy(base.objClothes[kindNo]);
			base.objClothes[kindNo] = null;
			base.infoClothes[kindNo] = null;
			base.cmpClothes[kindNo] = null;
			ReleaseRefObject(10uL);
			RemoveClothesStateKind(kindNo);
			base.rendPanst = null;
			if (Singleton<Character>.Instance.customLoadGCClear)
			{
				UnityEngine.Resources.UnloadUnusedAssets();
				GC.Collect();
			}
		}
		if (load)
		{
			if (asyncFlags)
			{
				yield return null;
			}
			if (asyncFlags)
			{
				IEnumerator cor = LoadCharaFbxDataAsync(delegate(GameObject o)
				{
					base.objClothes[kindNo] = o;
				}, 245, id, objName, copyDynamicBone: false, 1, base.objTop.transform, 0);
				yield return StartCoroutine(cor);
			}
			else
			{
				base.objClothes[kindNo] = LoadCharaFbxData(245, id, objName, copyDynamicBone: false, 1, base.objTop.transform, 0);
			}
			if (asyncFlags)
			{
				yield return null;
			}
			if (null != base.objClothes[kindNo])
			{
				ListInfoComponent component = base.objClothes[kindNo].GetComponent<ListInfoComponent>();
				ListInfoBase listInfoBase = (base.infoClothes[kindNo] = component.data);
				base.cmpClothes[kindNo] = base.objClothes[kindNo].GetComponent<CmpClothes>();
				if (null != base.cmpClothes[kindNo])
				{
					base.cmpClothes[kindNo].InitDynamicBones();
				}
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy" && null == base.cmpClothes[kindNo])
				{
					Debug.LogWarning("服情報を保持するコンポーネントがありません");
				}
				nowCoordinate.clothes.parts[kindNo].id = listInfoBase.Id;
				CreateReferenceInfo(10uL, base.objClothes[kindNo]);
				AddClothesStateKind(kindNo, listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
				GameObject referenceInfo = GetReferenceInfo(RefObjKey.mask_panst);
				if (null != referenceInfo)
				{
					base.rendPanst = referenceInfo.GetComponent<Renderer>();
				}
				if (null != base.rendPanst)
				{
					base.rendPanst.material.SetTexture(ChaShader.AlphaMask2, texPanstAlphaMask);
				}
				ChangeAlphaMask2();
			}
		}
		if (null != base.objClothes[kindNo])
		{
			InitBaseCustomTextureClothes(kindNo);
			if (base.loadWithDefaultColorAndPtn)
			{
				SetClothesDefaultSetting(kindNo);
				for (int i = 0; i < 3; i++)
				{
					nowCoordinate.clothes.parts[kindNo].colorInfo[i].pattern = 0;
				}
			}
			ChangeCustomClothes(kindNo, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true);
			if (base.releaseCustomInputTexture)
			{
				ReleaseBaseCustomTextureClothes(kindNo, createTex: false);
			}
			ChangeBreakClothes(kindNo);
		}
		UpdateSiru(forceChange: true);
		base.updateWet = true;
	}

	public void ChangeClothesSocks(int id, bool forceChange = false)
	{
		StartCoroutine(ChangeClothesSocksAsync(id, forceChange, asyncFlags: false));
	}

	public IEnumerator ChangeClothesSocksAsync(int id, bool forceChange = false, bool asyncFlags = true)
	{
		bool load = true;
		bool release = true;
		int kindNo = 6;
		string objName = "ct_socks";
		int nowId = ((base.infoClothes[kindNo] != null) ? base.infoClothes[kindNo].Id : (-1));
		if (!forceChange && null != base.objClothes[kindNo] && id == nowId)
		{
			load = false;
			release = false;
		}
		ReleaseBaseCustomTextureClothes(kindNo);
		if (release && null != base.objClothes[kindNo])
		{
			SafeDestroy(base.objClothes[kindNo]);
			base.objClothes[kindNo] = null;
			base.infoClothes[kindNo] = null;
			base.cmpClothes[kindNo] = null;
			RemoveClothesStateKind(kindNo);
			if (Singleton<Character>.Instance.customLoadGCClear)
			{
				UnityEngine.Resources.UnloadUnusedAssets();
				GC.Collect();
			}
		}
		if (load)
		{
			if (asyncFlags)
			{
				yield return null;
			}
			if (asyncFlags)
			{
				IEnumerator cor = LoadCharaFbxDataAsync(delegate(GameObject o)
				{
					base.objClothes[kindNo] = o;
				}, 246, id, objName, copyDynamicBone: false, 1, base.objTop.transform, 0);
				yield return StartCoroutine(cor);
			}
			else
			{
				base.objClothes[kindNo] = LoadCharaFbxData(246, id, objName, copyDynamicBone: false, 1, base.objTop.transform, 0);
			}
			if (asyncFlags)
			{
				yield return null;
			}
			if (null != base.objClothes[kindNo])
			{
				ListInfoComponent component = base.objClothes[kindNo].GetComponent<ListInfoComponent>();
				ListInfoBase listInfoBase = (base.infoClothes[kindNo] = component.data);
				base.cmpClothes[kindNo] = base.objClothes[kindNo].GetComponent<CmpClothes>();
				if (null != base.cmpClothes[kindNo])
				{
					base.cmpClothes[kindNo].InitDynamicBones();
				}
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy" && null == base.cmpClothes[kindNo])
				{
					Debug.LogWarning("服情報を保持するコンポーネントがありません");
				}
				nowCoordinate.clothes.parts[kindNo].id = listInfoBase.Id;
				AddClothesStateKind(kindNo, listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
			}
		}
		if (!(null != base.objClothes[kindNo]))
		{
			yield break;
		}
		InitBaseCustomTextureClothes(kindNo);
		if (base.loadWithDefaultColorAndPtn)
		{
			SetClothesDefaultSetting(kindNo);
			for (int i = 0; i < 3; i++)
			{
				nowCoordinate.clothes.parts[kindNo].colorInfo[i].pattern = 0;
			}
		}
		ChangeCustomClothes(kindNo, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true);
		if (base.releaseCustomInputTexture)
		{
			ReleaseBaseCustomTextureClothes(kindNo, createTex: false);
		}
		ChangeBreakClothes(kindNo);
	}

	public void ChangeClothesShoes(int id, bool forceChange = false)
	{
		StartCoroutine(ChangeClothesShoesAsync(id, forceChange, asyncFlags: false));
	}

	public IEnumerator ChangeClothesShoesAsync(int id, bool forceChange = false, bool asyncFlags = true)
	{
		bool load = true;
		bool release = true;
		int kindNo = 7;
		string objName = "ct_shoes";
		int nowId = ((base.infoClothes[kindNo] != null) ? base.infoClothes[kindNo].Id : (-1));
		if (!forceChange && null != base.objClothes[kindNo] && id == nowId)
		{
			load = false;
			release = false;
		}
		ReleaseBaseCustomTextureClothes(kindNo);
		if (release && null != base.objClothes[kindNo])
		{
			SafeDestroy(base.objClothes[kindNo]);
			base.objClothes[kindNo] = null;
			base.infoClothes[kindNo] = null;
			base.cmpClothes[kindNo] = null;
			RemoveClothesStateKind(kindNo);
			if (Singleton<Character>.Instance.customLoadGCClear)
			{
				UnityEngine.Resources.UnloadUnusedAssets();
				GC.Collect();
			}
		}
		if (load)
		{
			if (asyncFlags)
			{
				yield return null;
			}
			if (asyncFlags)
			{
				IEnumerator cor = LoadCharaFbxDataAsync(delegate(GameObject o)
				{
					base.objClothes[kindNo] = o;
				}, (base.sex != 0) ? 247 : 147, id, objName, copyDynamicBone: false, 1, base.objTop.transform, (base.sex != 0) ? 0 : 0);
				yield return StartCoroutine(cor);
			}
			else
			{
				base.objClothes[kindNo] = LoadCharaFbxData((base.sex != 0) ? 247 : 147, id, objName, copyDynamicBone: false, 1, base.objTop.transform, (base.sex != 0) ? 0 : 0);
			}
			if (asyncFlags)
			{
				yield return null;
			}
			if (null != base.objClothes[kindNo])
			{
				ListInfoComponent component = base.objClothes[kindNo].GetComponent<ListInfoComponent>();
				ListInfoBase listInfoBase = (base.infoClothes[kindNo] = component.data);
				base.cmpClothes[kindNo] = base.objClothes[kindNo].GetComponent<CmpClothes>();
				if (null != base.cmpClothes[kindNo])
				{
					base.cmpClothes[kindNo].InitDynamicBones();
				}
				if (listInfoBase.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy" && null == base.cmpClothes[kindNo])
				{
					Debug.LogWarning("服情報を保持するコンポーネントがありません");
				}
				nowCoordinate.clothes.parts[kindNo].id = listInfoBase.Id;
				AddClothesStateKind(kindNo, listInfoBase.GetInfo(ChaListDefine.KeyType.StateType));
			}
		}
		if (!(null != base.objClothes[kindNo]))
		{
			yield break;
		}
		InitBaseCustomTextureClothes(kindNo);
		if (base.loadWithDefaultColorAndPtn)
		{
			SetClothesDefaultSetting(kindNo);
			for (int i = 0; i < 3; i++)
			{
				nowCoordinate.clothes.parts[kindNo].colorInfo[i].pattern = 0;
			}
		}
		ChangeCustomClothes(kindNo, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true);
		if (base.releaseCustomInputTexture)
		{
			ReleaseBaseCustomTextureClothes(kindNo, createTex: false);
		}
		ChangeBreakClothes(kindNo);
	}

	public void ChangeAccessory(bool forceChange = false)
	{
		for (int i = 0; i < 20; i++)
		{
			StartCoroutine(ChangeAccessoryAsync(i, nowCoordinate.accessory.parts[i].type, nowCoordinate.accessory.parts[i].id, nowCoordinate.accessory.parts[i].parentKey, forceChange, asyncFlags: false));
		}
	}

	public void ChangeAccessory(int slotNo, int type, int id, string parentKey, bool forceChange = false)
	{
		StartCoroutine(ChangeAccessoryAsync(slotNo, type, id, parentKey, forceChange, asyncFlags: false));
	}

	public IEnumerator ChangeAccessoryAsync(bool forceChange = false)
	{
		for (int i = 0; i < 20; i++)
		{
			yield return StartCoroutine(ChangeAccessoryAsync(i, nowCoordinate.accessory.parts[i].type, nowCoordinate.accessory.parts[i].id, nowCoordinate.accessory.parts[i].parentKey, forceChange));
		}
	}

	public IEnumerator ChangeAccessoryAsync(int slotNo, int type, int id, string parentKey, bool forceChange = false, bool asyncFlags = true)
	{
		if (!MathfEx.RangeEqualOn(0, slotNo, 19))
		{
			yield break;
		}
		ListInfoBase lib = null;
		bool load = true;
		bool release = true;
		if (type == 350 || !MathfEx.RangeEqualOn(351, type, 363))
		{
			release = true;
			load = false;
		}
		else
		{
			if (id == -1)
			{
				release = false;
				load = false;
			}
			int num = ((base.infoAccessory[slotNo] != null) ? base.infoAccessory[slotNo].Category : (-1));
			int num2 = ((base.infoAccessory[slotNo] != null) ? base.infoAccessory[slotNo].Id : (-1));
			if (!forceChange && null != base.objAccessory[slotNo] && type == num && id == num2)
			{
				load = false;
				release = false;
			}
			if (id != -1)
			{
				Dictionary<int, ListInfoBase> categoryInfo = base.lstCtrl.GetCategoryInfo((ChaListDefine.CategoryNo)type);
				if (categoryInfo == null)
				{
					release = true;
					load = false;
				}
				else if (!categoryInfo.TryGetValue(id, out lib))
				{
					release = true;
					load = false;
				}
			}
		}
		if (release)
		{
			if (!load)
			{
				nowCoordinate.accessory.parts[slotNo].MemberInit();
				nowCoordinate.accessory.parts[slotNo].type = 350;
			}
			if (null != base.objAccessory[slotNo])
			{
				SafeDestroy(base.objAccessory[slotNo]);
				base.objAccessory[slotNo] = null;
				base.infoAccessory[slotNo] = null;
				base.cmpAccessory[slotNo] = null;
				for (int i = 0; i < 2; i++)
				{
					base.trfAcsMove[slotNo, i] = null;
				}
			}
			if (Singleton<Character>.Instance.customLoadGCClear)
			{
				UnityEngine.Resources.UnloadUnusedAssets();
				GC.Collect();
			}
		}
		if (load)
		{
			if (asyncFlags)
			{
				yield return null;
			}
			byte weight = 0;
			Transform trfParent = null;
			if ("null" == lib.GetInfo(ChaListDefine.KeyType.Parent))
			{
				weight = 2;
				trfParent = base.objTop.transform;
			}
			if (asyncFlags)
			{
				IEnumerator cor = LoadCharaFbxDataAsync(delegate(GameObject o)
				{
					base.objAccessory[slotNo] = o;
				}, type, id, "ca_slot" + slotNo.ToString("00"), copyDynamicBone: false, weight, trfParent, -1);
				yield return StartCoroutine(cor);
			}
			else
			{
				base.objAccessory[slotNo] = LoadCharaFbxData(type, id, "ca_slot" + slotNo.ToString("00"), copyDynamicBone: false, weight, trfParent, -1);
			}
			if (null != base.objAccessory[slotNo])
			{
				ListInfoComponent component = base.objAccessory[slotNo].GetComponent<ListInfoComponent>();
				lib = (base.infoAccessory[slotNo] = component.data);
				base.cmpAccessory[slotNo] = base.objAccessory[slotNo].GetComponent<CmpAccessory>();
				if (null != base.cmpAccessory[slotNo])
				{
					base.cmpAccessory[slotNo].InitDynamicBones();
				}
				if (lib.GetInfo(ChaListDefine.KeyType.MainData) != "p_dummy" && null == base.cmpAccessory[slotNo])
				{
					OutputLog.Warning("アクセサリ情報を保持するコンポーネントがありません", unityLog: true, "CharaLoad");
				}
				nowCoordinate.accessory.parts[slotNo].type = type;
				nowCoordinate.accessory.parts[slotNo].id = lib.Id;
				if (base.cmpAccessory != null && null != base.cmpAccessory[slotNo])
				{
					base.trfAcsMove[slotNo, 0] = base.cmpAccessory[slotNo].trfMove01;
					base.trfAcsMove[slotNo, 1] = base.cmpAccessory[slotNo].trfMove02;
				}
			}
		}
		if (!(null != base.objAccessory[slotNo]))
		{
			yield break;
		}
		if (string.Empty == parentKey)
		{
			parentKey = lib.GetInfo(ChaListDefine.KeyType.Parent);
		}
		ChangeAccessoryParent(slotNo, parentKey);
		UpdateAccessoryMoveFromInfo(slotNo);
		nowCoordinate.accessory.parts[slotNo].partsOfHead = ChaAccessoryDefine.CheckPartsOfHead(parentKey);
		if (base.cmpAccessory != null && base.cmpAccessory[slotNo].typeHair)
		{
			ChangeSettingHairTypeAccessoryShader(slotNo);
			ChangeHairTypeAccessoryColor(slotNo);
			yield break;
		}
		if (base.loadWithDefaultColorAndPtn)
		{
			SetAccessoryDefaultColor(slotNo);
		}
		ChangeAccessoryColor(slotNo);
	}

	private GameObject LoadCharaFbxData(int category, int id, string createName, bool copyDynamicBone, byte copyWeights, Transform trfParent, int defaultId, bool worldPositionStays = false)
	{
		GameObject actObj = null;
		StartCoroutine(LoadCharaFbxDataAsync(delegate(GameObject o)
		{
			actObj = o;
		}, category, id, createName, copyDynamicBone, copyWeights, trfParent, defaultId, AsyncFlags: false, worldPositionStays));
		return actObj;
	}

	private IEnumerator LoadCharaFbxDataAsync(Action<GameObject> actObj, int category, int id, string createName, bool copyDynamicBone, byte copyWeights, Transform trfParent, int defaultId, bool AsyncFlags = true, bool worldPositionStays = false)
	{
		Dictionary<int, ListInfoBase> work2 = null;
		work2 = base.lstCtrl.GetCategoryInfo((ChaListDefine.CategoryNo)category);
		if (work2.Count == 0)
		{
			OutputLog.Warning(createName + "：リストデータが無い", unityLog: false, "CharaLoad");
			actObj(null);
			yield break;
		}
		ListInfoBase lib = null;
		if (!work2.TryGetValue(id, out lib))
		{
			if (defaultId == -1)
			{
				actObj(null);
				yield break;
			}
			if (id != defaultId)
			{
				work2.TryGetValue(defaultId, out lib);
			}
			if (lib == null)
			{
				lib = work2.First().Value;
			}
		}
		string assetName = string.Empty;
		if (base.sex == 0)
		{
			string info = lib.GetInfo(ChaListDefine.KeyType.MainData02);
			if ("0" != info)
			{
				assetName = info;
			}
		}
		if (string.Empty == assetName)
		{
			assetName = lib.GetInfo(ChaListDefine.KeyType.MainData);
		}
		if (string.Empty == assetName)
		{
			OutputLog.Warning(createName + "：リストにデータの指定がない", unityLog: false, "CharaLoad");
			yield break;
		}
		OutputLog.Log(createName + "：" + assetName, unityLog: false, "CharaLoad");
		string manifestName = lib.GetInfo(ChaListDefine.KeyType.MainManifest);
		string assetBundleName = lib.GetInfo(ChaListDefine.KeyType.MainAB);
		GameObject newObj = null;
		if (AsyncFlags)
		{
			yield return StartCoroutine(Load_Coroutine(assetBundleName, assetName, delegate(GameObject x)
			{
				newObj = x;
			}, isClone: true, manifestName));
		}
		else
		{
			newObj = CommonLib.LoadAsset<GameObject>(assetBundleName, assetName, clone: true, manifestName);
		}
		Singleton<Character>.Instance.AddLoadAssetBundle(assetBundleName, manifestName);
		if (null == newObj)
		{
			OutputLog.Error(assetName + "：データの読み込みに失敗", unityLog: false, "CharaLoad");
			actObj(null);
			yield break;
		}
		newObj.name = createName;
		if (null != trfParent)
		{
			newObj.transform.SetParent(trfParent, worldPositionStays);
		}
		DynamicBoneCollider[] dbc = base.objBodyBone.GetComponentsInChildren<DynamicBoneCollider>(includeInactive: true);
		Dictionary<string, GameObject> dictBone = aaWeightsBody.dictBone;
		DynamicBone[] db = newObj.GetComponentsInChildren<DynamicBone>(includeInactive: true);
		GameObject obj = null;
		DynamicBone[] array = db;
		foreach (DynamicBone dynamicBone in array)
		{
			if (copyDynamicBone)
			{
				if (null != dynamicBone.m_Root && dictBone.TryGetValue(dynamicBone.m_Root.name, out obj))
				{
					dynamicBone.m_Root = obj.transform;
				}
				if (dynamicBone.m_Exclusions != null && dynamicBone.m_Exclusions.Count != 0)
				{
					for (int j = 0; j < dynamicBone.m_Exclusions.Count; j++)
					{
						if (!(null == dynamicBone.m_Exclusions[j]) && dictBone.TryGetValue(dynamicBone.m_Exclusions[j].name, out obj))
						{
							dynamicBone.m_Exclusions[j] = obj.transform;
						}
					}
				}
				if (dynamicBone.m_notRolls != null && dynamicBone.m_notRolls.Count != 0)
				{
					for (int k = 0; k < dynamicBone.m_notRolls.Count; k++)
					{
						if (!(null == dynamicBone.m_notRolls[k]) && dictBone.TryGetValue(dynamicBone.m_notRolls[k].name, out obj))
						{
							dynamicBone.m_notRolls[k] = obj.transform;
						}
					}
				}
			}
			if (dynamicBone.m_Colliders != null)
			{
				dynamicBone.m_Colliders.Clear();
				dynamicBone.m_Colliders.AddRange(dbc);
			}
		}
		Transform trfRootBone = base.cmpBoneBody.targetEtc.trfRoot;
		int copyWeightsEx = copyWeights;
		if (copyWeightsEx == 0)
		{
			copyWeightsEx = lib.GetInfoInt(ChaListDefine.KeyType.Weights);
		}
		switch (copyWeightsEx)
		{
		case 1:
			aaWeightsBody.AssignedWeightsAndSetBounds(newObj, "cf_J_Root", ChaControlDefine.bounds, trfRootBone);
			break;
		case 2:
			aaWeightsHead.AssignedWeightsAndSetBounds(newObj, "cf_J_FaceRoot", ChaControlDefine.bounds, trfRootBone);
			break;
		}
		ListInfoComponent libComponent = newObj.AddComponent<ListInfoComponent>();
		libComponent.data = lib;
		actObj(newObj);
	}

	public void LoadHitObject()
	{
		ReleaseHitObject();
		string assetBundleName = "chara/oo_base.unity3d";
		string assetName = "p_cf_body_00_hit";
		base.objHitBody = CommonLib.LoadAsset<GameObject>(assetBundleName, assetName, clone: true, "abdata");
		Singleton<Character>.Instance.AddLoadAssetBundle(assetBundleName, "abdata");
		if (null != base.objHitBody)
		{
			base.objHitBody.transform.SetParent(base.objTop.transform, worldPositionStays: false);
			aaWeightsBody.AssignedWeights(base.objHitBody, "cf_J_Root");
			SkinnedCollisionHelper[] componentsInChildren = base.objHitBody.GetComponentsInChildren<SkinnedCollisionHelper>(includeInactive: true);
			SkinnedCollisionHelper[] array = componentsInChildren;
			foreach (SkinnedCollisionHelper skinnedCollisionHelper in array)
			{
				skinnedCollisionHelper.Init();
			}
		}
		if (base.sex == 0 || !(null != base.objHead))
		{
			return;
		}
		ListInfoComponent component = base.objHead.GetComponent<ListInfoComponent>();
		string manifestName = component.data.dictInfo[14];
		assetBundleName = component.data.dictInfo[15];
		assetName = component.data.dictInfo[16] + "_hit";
		base.objHitHead = CommonLib.LoadAsset<GameObject>(assetBundleName, assetName, clone: true, manifestName);
		Singleton<Character>.Instance.AddLoadAssetBundle(assetBundleName, manifestName);
		if (null != base.objHitHead)
		{
			base.objHitHead.transform.SetParent(base.objTop.transform, worldPositionStays: false);
			aaWeightsHead.AssignedWeights(base.objHitHead, "cf_J_FaceRoot");
			SkinnedCollisionHelper[] componentsInChildren2 = base.objHitHead.GetComponentsInChildren<SkinnedCollisionHelper>(includeInactive: true);
			SkinnedCollisionHelper[] array2 = componentsInChildren2;
			foreach (SkinnedCollisionHelper skinnedCollisionHelper2 in array2)
			{
				skinnedCollisionHelper2.Init();
			}
		}
	}

	public void ReleaseHitObject()
	{
		if (null != base.objHitBody)
		{
			SkinnedCollisionHelper[] componentsInChildren = base.objHitBody.GetComponentsInChildren<SkinnedCollisionHelper>(includeInactive: true);
			SkinnedCollisionHelper[] array = componentsInChildren;
			foreach (SkinnedCollisionHelper skinnedCollisionHelper in array)
			{
				skinnedCollisionHelper.Release();
			}
			SafeDestroy(base.objHitBody);
			base.objHitBody = null;
		}
		if (null != base.objHitHead)
		{
			SkinnedCollisionHelper[] componentsInChildren2 = base.objHitHead.GetComponentsInChildren<SkinnedCollisionHelper>(includeInactive: true);
			SkinnedCollisionHelper[] array2 = componentsInChildren2;
			foreach (SkinnedCollisionHelper skinnedCollisionHelper2 in array2)
			{
				skinnedCollisionHelper2.Release();
			}
			SafeDestroy(base.objHitHead);
			base.objHitHead = null;
		}
	}

	public bool LoadAlphaMaskTexture(string assetBundleName, string assetName, byte type)
	{
		if ("0" == assetBundleName || "0" == assetName)
		{
			return false;
		}
		Texture texture = CommonLib.LoadAsset<Texture>(assetBundleName, assetName, clone: false, string.Empty);
		if (null == texture)
		{
			return false;
		}
		Singleton<Character>.Instance.AddLoadAssetBundle(assetBundleName, string.Empty);
		switch (type)
		{
		case 0:
			texBodyAlphaMask = texture;
			break;
		case 1:
			texBraAlphaMask = texture;
			break;
		case 2:
			texInnerTBAlphaMask = texture;
			break;
		case 3:
			texInnerBAlphaMask = texture;
			break;
		case 4:
			texPanstAlphaMask = texture;
			break;
		case 5:
			texBodyBAlphaMask = texture;
			break;
		}
		return true;
	}

	public bool ReleaseAlphaMaskTexture(byte type)
	{
		switch (type)
		{
		case 0:
			if (null != base.customMatBody)
			{
				base.customMatBody.SetTexture(ChaShader.AlphaMask, null);
			}
			texBodyAlphaMask = null;
			break;
		case 1:
			if (base.rendBra != null && null != base.rendBra[0])
			{
				base.rendBra[0].material.SetTexture(ChaShader.AlphaMask, null);
			}
			texBraAlphaMask = null;
			break;
		case 2:
			if (null != base.rendInnerTB)
			{
				base.rendInnerTB.material.SetTexture(ChaShader.AlphaMask2, null);
			}
			texInnerTBAlphaMask = null;
			break;
		case 3:
			if (null != base.rendInnerB)
			{
				base.rendInnerB.material.SetTexture(ChaShader.AlphaMask2, null);
			}
			texInnerBAlphaMask = null;
			break;
		case 4:
			if (null != base.rendPanst)
			{
				base.rendPanst.material.SetTexture(ChaShader.AlphaMask2, null);
			}
			texPanstAlphaMask = null;
			break;
		case 5:
			if (null != base.customMatBody)
			{
				base.customMatBody.SetTexture(ChaShader.AlphaMask2, null);
			}
			texBodyBAlphaMask = null;
			break;
		}
		return true;
	}

	public bool InitializeExpression(int sex, bool _enable = true)
	{
		string text = "list/expression.unity3d";
		string text2 = ((sex != 0) ? "cf_expression" : "cm_expression");
		if (!AssetBundleCheck.IsFile(text, text2))
		{
			string message = "読み込みエラー\r\nassetBundleName：" + text + "\tassetName：" + text2;
			Debug.LogWarning(message);
			return false;
		}
		base.expression = base.objRoot.AddComponent<Expression>();
		base.expression.LoadSetting(text, text2);
		int[] array = new int[26]
		{
			0, 0, 4, 0, 0, 0, 0, 1, 1, 5,
			1, 1, 1, 1, 6, 6, 6, 2, 2, 6,
			7, 7, 7, 3, 3, 7
		};
		for (int i = 0; i < base.expression.info.Length; i++)
		{
			base.expression.info[i].categoryNo = array[i];
		}
		base.expression.SetCharaTransform(base.objRoot.transform);
		base.expression.Initialize();
		base.expression.enable = _enable;
		return true;
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.2.0.10050' (yours is '8.2.0.7535-95108c96')
