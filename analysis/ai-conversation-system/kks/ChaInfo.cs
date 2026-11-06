using System;
using System.Collections.Generic;
using System.Linq;
using Illusion.Extensions;
using Manager;
using UnityEngine;
using WavInfoControl;

public class ChaInfo : ChaReference
{
	public enum DynamicBoneKind
	{
		BreastL,
		BreastR,
		HipL,
		HipR
	}

	private GameObject[] _objHair;

	private GameObject[] _objClothes;

	private GameObject[] _objParts;

	private GameObject[] _objAccessory;

	private GameObject[,] _objAcsMove;

	private GameObject _objHideNip;

	private GameObject _objHideKokan;

	private ListInfoBase[] _infoHair;

	private ListInfoBase[] _infoClothes;

	private ListInfoBase[] _infoParts;

	private ListInfoBase[] _infoAccessory;

	public ChaFileControl chaFile { get; protected set; }

	public ChaFileCustom fileCustom => chaFile.custom;

	public ChaFileBody fileBody => chaFile.custom.body;

	public ChaFileFace fileFace => chaFile.custom.face;

	public ChaFileHair fileHair => chaFile.custom.hair;

	public ChaFileParameter fileParam => chaFile.parameter;

	public ChaFileStatus fileStatus => chaFile.status;

	public ChaListControl lstCtrl => Character.chaListCtrl;

	public WavInfoData wavInfoData { get; set; }

	public EyeLookController eyeLookCtrl { get; protected set; }

	public EyeLookMaterialControll[] eyeLookMatCtrl { get; protected set; }

	public NeckLookControllerVer2 neckLookCtrl { get; protected set; }

	public FaceBlendShape fbsCtrl { get; protected set; }

	public FBSCtrlEyebrow eyebrowCtrl { get; protected set; }

	public FBSCtrlEyes eyesCtrl { get; protected set; }

	public FBSCtrlMouth mouthCtrl { get; protected set; }

	public Dictionary<DynamicBoneKind, DynamicBone_Ver02> dictDynamicBoneBust { get; protected set; }

	public ChaForegroundComponent foregroundCtrl { get; protected set; }

	public ExpressionBone expression { get; protected set; }

	public ChaHitControlComponent hitBodyCtrlCmp { get; protected set; }

	public ChaCustomHairComponent[] cusHairCmp { get; protected set; }

	public ChaClothesComponent[] cusClothesCmp { get; protected set; }

	public ChaClothesComponent[] cusClothesSubCmp { get; protected set; }

	public ChaAccessoryComponent[] cusAcsCmp { get; protected set; }

	public bool skipDestroy { get; set; }

	public int chaID { get; protected set; }

	public int loadNo { get; protected set; }

	public byte sex => chaFile.parameter.sex;

	public int exType => chaFile.parameter.exType;

	public bool hiPoly { get; protected set; }

	public bool hideMoz { get; set; }

	public bool loadEnd { get; protected set; }

	public bool visibleAll { get; set; }

	public bool updateShapeFace { get; set; }

	public bool updateShapeBody { get; set; }

	public bool updateShape
	{
		set
		{
			updateShapeFace = value;
			updateShapeBody = value;
		}
	}

	public bool resetDynamicBoneAll { get; set; }

	public bool reSetupDynamicBoneBust { get; set; }

	public bool updateBustSize { get; set; }

	public bool releaseCustomInputTexture { get; set; }

	public bool loadWithDefaultColorAndPtn { get; set; }

	public Renderer rendFace { get; protected set; }

	public Renderer[] rendEye { get; protected set; }

	public Renderer[] rendEyeW { get; protected set; }

	public Renderer rendEyelineUp { get; protected set; }

	public Renderer rendEyelineDown { get; protected set; }

	public Renderer rendEyebrow { get; protected set; }

	public Renderer rendNose { get; protected set; }

	public Renderer rendBody { get; protected set; }

	public Renderer[] rendBra { get; protected set; }

	public Renderer[] rendInner { get; protected set; }

	public Renderer rendSimpleBody { get; protected set; }

	public Renderer rendSimpleTongue { get; protected set; }

	public CustomTextureControl customTexCtrlFace { get; protected set; }

	public CustomTextureControl customTexCtrlBody { get; protected set; }

	public CustomTextureCreate ctCreateEyeL { get; protected set; }

	public CustomTextureCreate ctCreateEyeR { get; protected set; }

	public CustomTextureCreate ctCreateEyeW { get; protected set; }

	public Material customMatFace
	{
		get
		{
			if (customTexCtrlFace != null)
			{
				return customTexCtrlFace.matDraw;
			}
			return null;
		}
	}

	public Material customMatBody
	{
		get
		{
			if (customTexCtrlBody != null)
			{
				return customTexCtrlBody.matDraw;
			}
			return null;
		}
	}

	public CustomTextureCreate[,] ctCreateClothes { get; protected set; }

	public CustomTextureCreate[,] ctCreateClothesSub { get; protected set; }

	public Material[] matGag { get; set; }

	public GameObject objRoot { get; protected set; }

	public GameObject objTop { get; protected set; }

	public GameObject objAnim { get; protected set; }

	public GameObject objBodyBone { get; protected set; }

	public GameObject objBody { get; protected set; }

	public GameObject objHeadBone { get; protected set; }

	public GameObject objHead { get; protected set; }

	public GameObject[] objHair
	{
		get
		{
			return _objHair;
		}
		protected set
		{
			_objHair = value;
		}
	}

	public GameObject[] objClothes
	{
		get
		{
			return _objClothes;
		}
		protected set
		{
			_objClothes = value;
		}
	}

	public GameObject[] objParts
	{
		get
		{
			return _objParts;
		}
		protected set
		{
			_objParts = value;
		}
	}

	public GameObject[] objAccessory
	{
		get
		{
			return _objAccessory;
		}
		protected set
		{
			_objAccessory = value;
		}
	}

	public GameObject[,] objAcsMove
	{
		get
		{
			return _objAcsMove;
		}
		protected set
		{
			_objAcsMove = value;
		}
	}

	public GameObject objHideNip
	{
		get
		{
			return _objHideNip;
		}
		protected set
		{
			_objHideNip = value;
		}
	}

	public GameObject objHideKokan
	{
		get
		{
			return _objHideKokan;
		}
		protected set
		{
			_objHideKokan = value;
		}
	}

	public GameObject objTongueEx { get; protected set; }

	public GameObject objHitBody { get; protected set; }

	public GameObject objHitHead { get; protected set; }

	public Animator animBody { get; protected set; }

	public Animator animTongueEx { get; protected set; }

	public GameObject objEyesLookTargetP { get; protected set; }

	public GameObject objEyesLookTarget { get; protected set; }

	public GameObject objNeckLookTargetP { get; protected set; }

	public GameObject objNeckLookTarget { get; protected set; }

	public ListInfoBase infoHead { get; protected set; }

	public ListInfoBase[] infoHair
	{
		get
		{
			return _infoHair;
		}
		protected set
		{
			_infoHair = value;
		}
	}

	public ListInfoBase[] infoClothes
	{
		get
		{
			return _infoClothes;
		}
		protected set
		{
			_infoClothes = value;
		}
	}

	public ListInfoBase[] infoParts
	{
		get
		{
			return _infoParts;
		}
		protected set
		{
			_infoParts = value;
		}
	}

	public ListInfoBase[] infoAccessory
	{
		get
		{
			return _infoAccessory;
		}
		protected set
		{
			_infoAccessory = value;
		}
	}

	public int weakPoint => fileParam.weakPoint;

	public static string[] AttributeNames { get; } = new string[21]
	{
		"はらぺこ", "チョロイ", "読書好き", "音楽好き", "奥手", "フレンドリー", "綺麗好き", "神出鬼没", "一人好き", "アクティブ",
		"真面目", "情報通", "恋愛好き", "おしゃべり", "仲間意識", "のんびり", "頻尿", "女の子好き", "ビッチ", "むっつり",
		"寂しがり屋"
	};


	private static bool[] Attributes { get; } = new bool[AttributeNames.Length];


	public ChaCustomHairComponent GetCustomHairComponent(int parts)
	{
		if (cusHairCmp == null)
		{
			return null;
		}
		if (parts >= cusHairCmp.Length)
		{
			return null;
		}
		ChaCustomHairComponent chaCustomHairComponent = cusHairCmp[parts];
		if (null == chaCustomHairComponent)
		{
			return null;
		}
		return chaCustomHairComponent;
	}

	public ChaClothesComponent GetCustomClothesComponent(int parts)
	{
		if (cusClothesCmp == null)
		{
			return null;
		}
		if (parts >= cusClothesCmp.Length)
		{
			return null;
		}
		ChaClothesComponent chaClothesComponent = cusClothesCmp[parts];
		if (null == chaClothesComponent)
		{
			return null;
		}
		return chaClothesComponent;
	}

	public ChaClothesComponent GetCustomClothesSubComponent(int parts)
	{
		if (cusClothesSubCmp == null)
		{
			return null;
		}
		if (parts >= cusClothesSubCmp.Length)
		{
			return null;
		}
		ChaClothesComponent chaClothesComponent = cusClothesSubCmp[parts];
		if (null == chaClothesComponent)
		{
			return null;
		}
		return chaClothesComponent;
	}

	public ChaAccessoryComponent GetAccessoryComponent(int parts)
	{
		if (cusAcsCmp == null)
		{
			return null;
		}
		if (parts >= cusAcsCmp.Length)
		{
			return null;
		}
		ChaAccessoryComponent chaAccessoryComponent = cusAcsCmp[parts];
		if (null == chaAccessoryComponent)
		{
			return null;
		}
		return chaAccessoryComponent;
	}

	protected void MemberInitializeAll()
	{
		chaFile = null;
		wavInfoData = null;
		skipDestroy = false;
		chaID = 0;
		loadNo = -1;
		hiPoly = false;
		hideMoz = false;
		releaseCustomInputTexture = true;
		loadWithDefaultColorAndPtn = false;
		objRoot = null;
		customTexCtrlBody = null;
		ctCreateEyeL = null;
		ctCreateEyeR = null;
		ctCreateEyeW = null;
		MemberInitializeObject();
	}

	protected void MemberInitializeObject()
	{
		eyeLookCtrl = null;
		eyeLookMatCtrl = new EyeLookMaterialControll[2];
		neckLookCtrl = null;
		fbsCtrl = null;
		eyebrowCtrl = null;
		eyesCtrl = null;
		mouthCtrl = null;
		dictDynamicBoneBust = null;
		foregroundCtrl = null;
		expression = null;
		hitBodyCtrlCmp = null;
		cusHairCmp = new ChaCustomHairComponent[Enum.GetNames(typeof(ChaFileDefine.HairKind)).Length];
		cusClothesCmp = new ChaClothesComponent[Enum.GetNames(typeof(ChaFileDefine.ClothesKind)).Length];
		cusClothesSubCmp = new ChaClothesComponent[Enum.GetNames(typeof(ChaFileDefine.ClothesSubKind)).Length];
		cusAcsCmp = new ChaAccessoryComponent[20];
		customTexCtrlFace = null;
		ctCreateClothes = new CustomTextureCreate[Enum.GetNames(typeof(ChaFileDefine.ClothesKind)).Length, 3];
		ctCreateClothesSub = new CustomTextureCreate[Enum.GetNames(typeof(ChaFileDefine.ClothesSubKind)).Length, 3];
		matGag = new Material[3];
		for (int i = 0; i < 3; i++)
		{
			matGag[i] = null;
		}
		loadEnd = false;
		visibleAll = true;
		updateShapeFace = false;
		updateShapeBody = false;
		resetDynamicBoneAll = false;
		reSetupDynamicBoneBust = false;
		updateBustSize = false;
		rendFace = null;
		rendEye = new Renderer[2];
		rendEyeW = new Renderer[2];
		rendEyelineUp = null;
		rendEyelineDown = null;
		rendEyebrow = null;
		rendNose = null;
		rendBody = null;
		rendBra = new Renderer[2];
		rendInner = new Renderer[2];
		rendSimpleBody = null;
		rendSimpleTongue = null;
		objTop = null;
		objAnim = null;
		objBodyBone = null;
		objBody = null;
		objHeadBone = null;
		objHead = null;
		objHair = new GameObject[Enum.GetNames(typeof(ChaFileDefine.HairKind)).Length];
		objClothes = new GameObject[Enum.GetNames(typeof(ChaFileDefine.ClothesKind)).Length];
		objParts = new GameObject[Enum.GetNames(typeof(ChaFileDefine.ClothesSubKind)).Length];
		objAccessory = new GameObject[20];
		objAcsMove = new GameObject[20, 2];
		objHideNip = null;
		objHideKokan = null;
		objTongueEx = null;
		objHitHead = null;
		objHitBody = null;
		animBody = null;
		animTongueEx = null;
		objEyesLookTargetP = null;
		objEyesLookTarget = null;
		objNeckLookTargetP = null;
		objNeckLookTarget = null;
		infoHead = null;
		infoHair = new ListInfoBase[Enum.GetNames(typeof(ChaFileDefine.HairKind)).Length];
		infoClothes = new ListInfoBase[Enum.GetNames(typeof(ChaFileDefine.ClothesKind)).Length];
		infoParts = new ListInfoBase[Enum.GetNames(typeof(ChaFileDefine.ClothesSubKind)).Length];
		infoAccessory = new ListInfoBase[20];
	}

	protected void ReleaseInfoAll()
	{
		ReleaseInfoObject(init: false);
		if (customTexCtrlBody != null)
		{
			customTexCtrlBody.Release();
		}
		if (ctCreateEyeW != null)
		{
			ctCreateEyeW.Release();
			ctCreateEyeW = null;
		}
		if (ctCreateEyeL != null)
		{
			ctCreateEyeL.Release();
			ctCreateEyeL = null;
		}
		if (ctCreateEyeR != null)
		{
			ctCreateEyeR.Release();
			ctCreateEyeR = null;
		}
		if (Character.enableCharaLoadGCClear)
		{
			Resources.UnloadUnusedAssets();
		}
	}

	protected void ReleaseInfoObject(bool init = true)
	{
		if (matGag != null)
		{
			for (int i = 0; i < 3; i++)
			{
				if ((bool)matGag[i])
				{
					UnityEngine.Object.Destroy(matGag[i]);
					matGag[i] = null;
				}
			}
		}
		if (customTexCtrlFace != null)
		{
			customTexCtrlFace.Release();
		}
		if (ctCreateClothes != null)
		{
			for (int j = 0; j < ctCreateClothes.GetLength(0); j++)
			{
				for (int k = 0; k < 3; k++)
				{
					if (ctCreateClothes[j, k] != null)
					{
						ctCreateClothes[j, k].Release();
						ctCreateClothes[j, k] = null;
					}
				}
			}
		}
		if (ctCreateClothesSub != null)
		{
			for (int l = 0; l < ctCreateClothesSub.GetLength(0); l++)
			{
				for (int m = 0; m < 3; m++)
				{
					if (ctCreateClothesSub[l, m] != null)
					{
						ctCreateClothesSub[l, m].Release();
						ctCreateClothesSub[l, m] = null;
					}
				}
			}
		}
		if (false)
		{
			if ((bool)objTop)
			{
				objTop.SetActiveIfDifferent(active: false);
				objTop.name = "Delete_Reserve";
				UnityEngine.Object.Destroy(objTop);
			}
		}
		else
		{
			SafeDestroy(objTop);
		}
		objTop = null;
		ReleaseRefAll();
		if (init)
		{
			MemberInitializeObject();
		}
	}

	public void SafeDestroy(GameObject obj)
	{
		if ((bool)obj)
		{
			obj.SetActiveIfDifferent(active: false);
			obj.transform.SetParent(null);
			obj.name = "Delete_Reserve";
			UnityEngine.Object.Destroy(obj);
		}
	}

	public ChaFileParameter.Awnser GetAwnser()
	{
		return fileParam.awnser;
	}

	public bool[] GetAwnserArry()
	{
		return GetAwnserArry(chaFile);
	}

	public static bool[] GetAwnserArry(ChaFileControl _chafile)
	{
		return new bool[9]
		{
			_chafile.parameter.awnser.animal,
			_chafile.parameter.awnser.eat,
			_chafile.parameter.awnser.cook,
			_chafile.parameter.awnser.exercise,
			_chafile.parameter.awnser.study,
			_chafile.parameter.awnser.fashionable,
			_chafile.parameter.awnser.blackCoffee,
			_chafile.parameter.awnser.spicy,
			_chafile.parameter.awnser.sweet
		};
	}

	public ChaFileParameter.Denial GetDenial()
	{
		return fileParam.denial;
	}

	public bool[] GetDenialArry()
	{
		return GetDenialArry(chaFile);
	}

	public static bool[] GetDenialArry(ChaFileControl _chafile)
	{
		return new bool[5]
		{
			_chafile.parameter.denial.kiss,
			_chafile.parameter.denial.aibu,
			_chafile.parameter.denial.anal,
			_chafile.parameter.denial.massage,
			_chafile.parameter.denial.notCondom
		};
	}

	public ChaFileParameter.Interest GetInterest()
	{
		return chaFile.parameter.interest;
	}

	public bool IsInterest(int index)
	{
		return IsInterest(chaFile, index);
	}

	public static bool IsInterest(ChaFileControl chaFile, int index)
	{
		return chaFile.parameter.interest.answer.Contains(index);
	}

	public bool SetInterest(int index, bool isOn)
	{
		return SetInterest(chaFile, index, isOn);
	}

	public static bool SetInterest(ChaFileControl chaFile, int index, bool isOn)
	{
		int[] answer = chaFile.parameter.interest.answer;
		int None = ChaFileParameter.Interest.None;
		if (isOn)
		{
			if (IsInterest(chaFile, index))
			{
				return false;
			}
			if (answer.All((int x) => x != None))
			{
				return false;
			}
			for (int i = 0; i < answer.Length; i++)
			{
				if (answer[i] == None)
				{
					answer[i] = index;
					return true;
				}
			}
		}
		else
		{
			for (int j = 0; j < answer.Length; j++)
			{
				if (answer[j] == index)
				{
					answer[j] = None;
					return true;
				}
			}
		}
		return false;
	}

	public static bool IsAttributeType0(int index)
	{
		switch (index)
		{
		case 0:
		case 2:
		case 4:
		case 7:
		case 8:
		case 10:
		case 13:
		case 14:
		case 15:
		case 16:
		case 20:
			return true;
		default:
			return false;
		}
	}

	public static bool IsAttributeH(int index)
	{
		if ((uint)(index - 16) <= 4u)
		{
			return true;
		}
		return false;
	}

	public int GetAttributeType()
	{
		return GetAttributeType(chaFile);
	}

	public static int GetAttributeType(ChaFileControl chaFile)
	{
		return GetAttributeType(chaFile.parameter.attribute);
	}

	public static int GetAttributeType(ChaFileParameter.Attribute attribute)
	{
		bool[] attributes = GetAttributes(attribute);
		int num = 0;
		int num2 = 0;
		bool isAddH = Game.isAddH;
		for (int i = 0; i < attributes.Length; i++)
		{
			if (attributes[i] && (isAddH || !IsAttributeH(i)))
			{
				if (IsAttributeType0(i))
				{
					num++;
				}
				else
				{
					num2++;
				}
			}
		}
		if (num < num2)
		{
			return 1;
		}
		return 0;
	}

	public bool[] GetAttributes()
	{
		return GetAttributes(chaFile);
	}

	public static bool[] GetAttributes(ChaFileControl chaFile)
	{
		return GetAttributes(chaFile.parameter.attribute);
	}

	public static bool[] GetAttributes(ChaFileParameter.Attribute attribute)
	{
		for (int i = 0; i < Attributes.Length; i++)
		{
			Attributes[i] = GetAttribute(attribute, i);
		}
		return Attributes;
	}

	public bool GetAttribute(int index)
	{
		return GetAttribute(chaFile.parameter.attribute, index);
	}

	public static bool GetAttribute(ChaFileControl chaFile, int index)
	{
		return GetAttribute(chaFile.parameter.attribute, index);
	}

	public static bool GetAttribute(ChaFileParameter.Attribute attribute, int index)
	{
		return index switch
		{
			0 => attribute.harapeko, 
			1 => attribute.choroi, 
			2 => attribute.dokusyo, 
			3 => attribute.ongaku, 
			4 => attribute.okute, 
			5 => attribute.friendly, 
			6 => attribute.kireizuki, 
			7 => attribute.sinsyutu, 
			8 => attribute.hitori, 
			9 => attribute.active, 
			10 => attribute.majime, 
			11 => attribute.info, 
			12 => attribute.love, 
			13 => attribute.talk, 
			14 => attribute.nakama, 
			15 => attribute.nonbiri, 
			16 => attribute.hinnyo, 
			17 => attribute.likeGirls, 
			18 => attribute.bitch, 
			19 => attribute.mutturi, 
			20 => attribute.lonely, 
			_ => false, 
		};
	}

	public void SetAttribute(int index, bool flag)
	{
		SetAttribute(chaFile.parameter.attribute, index, flag);
	}

	public static void SetAttribute(ChaFileControl chaFile, int index, bool flag)
	{
		SetAttribute(chaFile.parameter.attribute, index, flag);
	}

	public static void SetAttribute(ChaFileParameter.Attribute attribute, int index, bool flag)
	{
		switch (index)
		{
		case 0:
			attribute.harapeko = flag;
			break;
		case 1:
			attribute.choroi = flag;
			break;
		case 2:
			attribute.dokusyo = flag;
			break;
		case 3:
			attribute.ongaku = flag;
			break;
		case 4:
			attribute.okute = flag;
			break;
		case 5:
			attribute.friendly = flag;
			break;
		case 6:
			attribute.kireizuki = flag;
			break;
		case 7:
			attribute.sinsyutu = flag;
			break;
		case 8:
			attribute.hitori = flag;
			break;
		case 9:
			attribute.active = flag;
			break;
		case 10:
			attribute.majime = flag;
			break;
		case 11:
			attribute.info = flag;
			break;
		case 12:
			attribute.love = flag;
			break;
		case 13:
			attribute.talk = flag;
			break;
		case 14:
			attribute.nakama = flag;
			break;
		case 15:
			attribute.nonbiri = flag;
			break;
		case 16:
			attribute.hinnyo = flag;
			break;
		case 17:
			attribute.likeGirls = flag;
			break;
		case 18:
			attribute.bitch = flag;
			break;
		case 19:
			attribute.mutturi = flag;
			break;
		case 20:
			attribute.lonely = flag;
			break;
		}
	}
}
