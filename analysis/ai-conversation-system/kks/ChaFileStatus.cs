using System;
using MessagePack;
using UnityEngine;

[MessagePackObject(true)]
public class ChaFileStatus
{
	[IgnoreMember]
	public static readonly string BlockName = "Status";

	public Version version { get; set; }

	public int coordinateType { get; set; }

	public int backCoordinateType { get; set; }

	public byte[] clothesState { get; set; }

	public byte shoesType { get; set; }

	public bool[] showAccessory { get; set; }

	public int eyebrowPtn { get; set; }

	public float eyebrowOpenMax { get; set; }

	public int eyesPtn { get; set; }

	public float eyesOpenMax { get; set; }

	public bool eyesBlink { get; set; }

	public bool eyesYure { get; set; }

	public int mouthPtn { get; set; }

	public float mouthOpenMax { get; set; }

	public bool mouthFixed { get; set; }

	public bool mouthAdjustWidth { get; set; }

	public byte tongueState { get; set; }

	public int eyesLookPtn { get; set; }

	public int eyesTargetType { get; set; }

	public float eyesTargetAngle { get; set; }

	public float eyesTargetRange { get; set; }

	public float eyesTargetRate { get; set; }

	public int neckLookPtn { get; set; }

	public int neckTargetType { get; set; }

	public float neckTargetAngle { get; set; }

	public float neckTargetRange { get; set; }

	public float neckTargetRate { get; set; }

	public bool disableMouthShapeMask { get; set; }

	public bool[,] disableBustShapeMask { get; set; }

	public float nipStandRate { get; set; }

	public float skinTuyaRate { get; set; }

	public float hohoAkaRate { get; set; }

	public byte tearsLv { get; set; }

	public bool hideEyesHighlight { get; set; }

	public byte[] siruLv { get; set; }

	public bool visibleSon { get; set; }

	public bool visibleSonAlways { get; set; }

	public bool visibleHeadAlways { get; set; }

	public bool visibleBodyAlways { get; set; }

	public bool visibleSimple { get; set; }

	public bool visibleGomu { get; set; }

	public Color simpleColor { get; set; }

	public bool[] enableShapeHand { get; set; }

	public int[,] shapeHandPtn { get; set; }

	public float[] shapeHandBlendValue { get; set; }

	public ChaFileStatus()
	{
		MemberInit();
	}

	public void MemberInit()
	{
		version = ChaFileDefine.ChaFileStatusVersion;
		coordinateType = 0;
		backCoordinateType = 0;
		clothesState = new byte[Enum.GetValues(typeof(ChaFileDefine.ClothesKind)).Length];
		shoesType = 1;
		showAccessory = new bool[20];
		for (int i = 0; i < showAccessory.Length; i++)
		{
			showAccessory[i] = true;
		}
		eyebrowPtn = 0;
		eyebrowOpenMax = 1f;
		eyesPtn = 0;
		eyesOpenMax = 1f;
		eyesBlink = true;
		eyesYure = false;
		mouthPtn = 0;
		mouthOpenMax = 1f;
		mouthFixed = false;
		mouthAdjustWidth = true;
		tongueState = 0;
		eyesLookPtn = 0;
		eyesTargetType = 0;
		eyesTargetAngle = 0f;
		eyesTargetRange = 1f;
		eyesTargetRate = 1f;
		neckLookPtn = 0;
		neckTargetType = 0;
		neckTargetAngle = 0f;
		neckTargetRange = 1f;
		neckTargetRate = 1f;
		disableMouthShapeMask = false;
		disableBustShapeMask = new bool[2, ChaFileDefine.cf_BustShapeMaskID.Length];
		nipStandRate = 0f;
		skinTuyaRate = 0f;
		hohoAkaRate = 0f;
		tearsLv = 0;
		hideEyesHighlight = false;
		siruLv = new byte[Enum.GetValues(typeof(ChaFileDefine.SiruParts)).Length];
		visibleSon = false;
		visibleSonAlways = true;
		visibleHeadAlways = true;
		visibleBodyAlways = true;
		visibleSimple = false;
		visibleGomu = false;
		simpleColor = new Color(0.188f, 0.286f, 0.8f, 0.5f);
		enableShapeHand = new bool[2];
		shapeHandPtn = new int[2, 2];
		shapeHandBlendValue = new float[2];
	}

	public void Copy(ChaFileStatus src)
	{
		version = src.version;
		coordinateType = src.coordinateType;
		backCoordinateType = src.backCoordinateType;
		Array.Copy(src.clothesState, clothesState, clothesState.Length);
		Array.Copy(src.showAccessory, showAccessory, showAccessory.Length);
		eyebrowPtn = src.eyebrowPtn;
		eyebrowOpenMax = src.eyebrowOpenMax;
		eyesPtn = src.eyesPtn;
		eyesOpenMax = src.eyesOpenMax;
		eyesBlink = src.eyesBlink;
		eyesYure = src.eyesYure;
		mouthPtn = src.mouthPtn;
		mouthOpenMax = src.mouthOpenMax;
		mouthFixed = src.mouthFixed;
		mouthAdjustWidth = src.mouthAdjustWidth;
		tongueState = src.tongueState;
		eyesLookPtn = src.eyesLookPtn;
		eyesTargetType = src.eyesTargetType;
		eyesTargetAngle = src.eyesTargetAngle;
		eyesTargetRange = src.eyesTargetRange;
		eyesTargetRate = src.eyesTargetRate;
		neckLookPtn = src.neckLookPtn;
		neckTargetType = src.neckTargetType;
		neckTargetAngle = src.neckTargetAngle;
		neckTargetRange = src.neckTargetRange;
		neckTargetRate = src.neckTargetRate;
		disableMouthShapeMask = src.disableMouthShapeMask;
		Array.Copy(src.disableBustShapeMask, disableBustShapeMask, ChaFileDefine.cf_BustShapeMaskID.Length * 2);
		nipStandRate = src.nipStandRate;
		skinTuyaRate = src.skinTuyaRate;
		hohoAkaRate = src.hohoAkaRate;
		tearsLv = src.tearsLv;
		hideEyesHighlight = src.hideEyesHighlight;
		Array.Copy(src.siruLv, siruLv, siruLv.Length);
		visibleSon = src.visibleSon;
		visibleSonAlways = src.visibleSonAlways;
		visibleHeadAlways = src.visibleHeadAlways;
		visibleBodyAlways = src.visibleBodyAlways;
		visibleSimple = src.visibleSimple;
		visibleGomu = src.visibleGomu;
		simpleColor = src.simpleColor;
		Array.Copy(src.enableShapeHand, enableShapeHand, enableShapeHand.Length);
		Array.Copy(src.shapeHandPtn, shapeHandPtn, 4);
		Array.Copy(src.shapeHandBlendValue, shapeHandBlendValue, shapeHandBlendValue.Length);
		ComplementWithVersion();
	}

	public void ComplementWithVersion()
	{
		if (enableShapeHand == null)
		{
			enableShapeHand = new bool[2];
			for (int i = 0; i < 2; i++)
			{
				enableShapeHand[i] = false;
			}
			shapeHandPtn = new int[2, 2];
			for (int j = 0; j < 2; j++)
			{
				for (int k = 0; k < 2; k++)
				{
					shapeHandPtn[j, k] = 0;
				}
			}
			shapeHandBlendValue = new float[2];
			for (int l = 0; l < 2; l++)
			{
				shapeHandBlendValue[l] = 0f;
			}
		}
		shoesType = 1;
		version = ChaFileDefine.ChaFileStatusVersion;
	}
}
