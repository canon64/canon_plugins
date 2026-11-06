using System;
using System.Collections.Generic;
using System.IO;
using IllusionUtility.SetUtility;
using Manager;
using UnityEngine;

public class ShapeBodyInfoFemale : ShapeInfoBase
{
	public enum DstName
	{
		cf_n_height,
		cf_s_hand_L,
		cf_s_hand_R,
		cf_s_head,
		cf_s_neck,
		cf_s_spine03,
		cf_s_shoulder02_L,
		cf_s_shoulder02_R,
		cf_s_arm01_L,
		cf_s_arm01_R,
		cf_s_arm02_L,
		cf_s_arm02_R,
		cf_s_arm03_L,
		cf_s_arm03_R,
		cf_s_forearm01_L,
		cf_s_forearm01_R,
		cf_s_forearm02_L,
		cf_s_forearm02_R,
		cf_s_wrist_L,
		cf_s_wrist_R,
		cf_s_spine02,
		cf_s_spine01,
		cf_s_waist01,
		cf_s_waist02,
		cf_s_siri_L,
		cf_s_siri_R,
		cf_s_thigh01_L,
		cf_s_thigh01_R,
		cf_s_thigh02_L,
		cf_s_thigh02_R,
		cf_s_thigh03_L,
		cf_s_thigh03_R,
		cf_s_leg01_L,
		cf_s_leg01_R,
		cf_s_leg02_L,
		cf_s_leg02_R,
		cf_s_leg03_L,
		cf_s_leg03_R,
		cf_d_kokan,
		cf_s_bust00_L,
		cf_d_bust01_L,
		cf_d_bust02_L,
		cf_d_bust03_L,
		cf_s_bust01_L,
		cf_s_bust02_L,
		cf_s_bust03_L,
		cf_hit_bust02_L,
		cf_d_bnip01_L,
		cf_s_bnip01_L,
		cf_s_bnip025_L,
		cf_s_bnip015_L,
		cf_s_bnip02_L,
		cf_s_bnipacc_L,
		cf_s_bust00_R,
		cf_d_bust01_R,
		cf_d_bust02_R,
		cf_d_bust03_R,
		cf_s_bust01_R,
		cf_s_bust02_R,
		cf_s_bust03_R,
		cf_hit_bust02_R,
		cf_d_bnip01_R,
		cf_s_bnip01_R,
		cf_s_bnip025_R,
		cf_s_bnip015_R,
		cf_s_bnip02_R,
		cf_s_bnipacc_R,
		cf_hit_siri_L,
		cf_hit_siri_R,
		cf_hit_waist_L,
		cf_hit_berry,
		cf_hit_spine02_L,
		cf_hit_shoulder_L,
		cf_hit_shoulder_R,
		cf_hit_arm_L,
		cf_hit_arm_R,
		cf_hit_spine01,
		cf_d_sk_top,
		cf_d_sk_00_00,
		cf_d_sk_01_00,
		cf_d_sk_02_00,
		cf_d_sk_03_00,
		cf_d_sk_04_00,
		cf_d_sk_05_00,
		cf_d_sk_06_00,
		cf_d_sk_07_00
	}

	public enum SrcName
	{
		cf_a_height,
		cf_a_height_aid,
		cf_a_head,
		cf_a_neck,
		cf_a_spine03,
		cf_a_shoulder,
		cf_a_shoulder_L_aid03,
		cf_a_shoulder_R_aid03,
		cf_a_arm_L_aid03,
		cf_a_arm_R_aid03,
		cf_a_arm02,
		cf_a_arm03_blend01,
		cf_a_arm03_blend02,
		cf_a_farm01,
		cf_a_farm02_blend01,
		cf_a_farm02_blend03,
		cf_a_farm03,
		cf_a_spine02,
		cf_a_spine02_aid_berry,
		cf_a_spine01,
		cf_a_berry,
		cf_a_waist01,
		cf_a_waist02,
		cf_a_siri,
		cf_a_thigh01_L,
		cf_a_thigh01_L_aid,
		cf_a_thigh01_R,
		cf_a_thigh01_R_aid,
		cf_a_thigh02_L_blend01,
		cf_a_thigh02_L_blend03,
		cf_a_thigh02_R_blend01,
		cf_a_thigh02_R_blend03,
		cf_a_thigh03_L,
		cf_a_thigh03_R,
		cf_a_leg01_L,
		cf_a_leg01_R,
		cf_a_leg02_L,
		cf_a_leg02_R,
		cf_a_leg03,
		cf_a_dan,
		cf_a_bust_ty,
		cf_a_bust00_aid03_sz,
		cf_a_bust00_aid02_sz,
		cf_a_bust_L_ry,
		cf_a_bust_rx,
		cf_a_bust01_size,
		cf_a_bust_L_tx,
		cf_a_bust02_size,
		cf_a_bust_tz,
		cf_a_bust03_size,
		cf_a_bust01_shape1,
		cf_a_bust02_shape1,
		cf_a_bust03_shape1,
		cf_a_hit_bust_shape1,
		cf_a_hit_bust_shape2,
		cf_a_bnip01,
		cf_a_bnip01_size,
		cf_a_d_bnip01_size,
		cf_a_bnip02_size,
		cf_a_bnip015_size,
		cf_a_bnip02,
		cf_a_bnipacc_stand,
		cf_a_bnipacc_size,
		cf_a_bust_R_ry,
		cf_a_bust_R_tx,
		cf_a_hit_siri_shape1,
		cf_a_hit_siri_shape2,
		cf_a_hit_siri_shape3,
		cf_a_hit_siri_shape4,
		cf_a_hit_siri_shape5,
		cf_a_hit_siri_shape6,
		cf_a_hit_waist_shape1,
		cf_a_hit_waist_shape2,
		cf_a_hit_waist_shape3,
		cf_a_hit_spinety_shape,
		cf_a_hit_waist_shape4,
		cf_a_hit_waist_shape5,
		cf_a_hit_berry_shape,
		cf_a_hit_berry_shape2,
		cf_a_hit_berry_shape3,
		cf_a_hit_spine02_shape1,
		cf_a_hit_spine02_shape2,
		cf_a_hit_spine02_shape3,
		cf_a_hit_shoulder_shape1,
		cf_a_hit_shoulder_shape2,
		cf_a_hit_shoulder_shape3,
		cf_a_hit_arm_shape2,
		cf_a_hit_arm_shape3,
		cf_a_hit_arm_shape4,
		cf_a_hit_spine01_shape1,
		cf_a_hit_spine01_shape2,
		cf_a_sk_00_00,
		cf_a_sk_00_01,
		cf_a_sk_berry,
		cf_a_sk_thigh01_sz,
		cf_a_sk_01_00,
		cf_a_sk_01_01,
		cf_a_sk_thigh01_sx,
		cf_a_sk_02_00,
		cf_a_sk_02_01,
		cf_a_sk_siri,
		cf_a_sk_03_00,
		cf_a_sk_03_01,
		cf_a_sk_04_00,
		cf_a_sk_04_01,
		cf_a_sk_05_00,
		cf_a_sk_05_01,
		cf_a_sk_06_00,
		cf_a_sk_06_01,
		cf_a_sk_07_00,
		cf_a_sk_07_01
	}

	public enum CorrectKeyName
	{
		cf_s_waist02,
		cf_s_neck,
		cf_s_head,
		cf_s_spine01,
		cf_s_spine02,
		cf_s_spine03,
		cf_s_thigh01_L,
		cf_s_thigh01_R,
		cf_s_thigh02_L,
		cf_s_thigh02_R,
		cf_s_thigh03_L,
		cf_s_thigh03_R,
		cf_s_leg01_L,
		cf_s_leg01_R,
		cf_s_shoulder02_L,
		cf_s_shoulder02_R,
		cf_s_arm01_L,
		cf_s_arm01_R,
		cf_s_arm02_L,
		cf_s_arm02_R,
		cf_s_arm03_L,
		cf_s_arm03_R,
		cf_s_forearm01_L,
		cf_s_forearm01_R,
		cf_s_forearm02_L,
		cf_s_forearm02_R,
		cf_s_wrist_L,
		cf_s_wrist_R,
		cf_s_siri_L,
		cf_s_siri_R,
		cf_d_shoulder_L,
		cf_d_shoulder_R
	}

	public const int UPDATE_MASK_BUST_L = 1;

	public const int UPDATE_MASK_BUST_R = 2;

	public const int UPDATE_MASK_ETC = 4;

	public const int UPDATE_MASK_ALL = 7;

	public int updateMask = 7;

	public BoneInfo[] correctValue;

	public Transform[] fixCorrectBone = new Transform[2];

	public float correctHeadSize = 1f;

	public float correctNeckSize = 1f;

	public float deformedHeight = 1f;

	public float deformedHeightAid = 1f;

	public float deformedHeadSize = 1f;

	public float deformedNeckSize = 1f;

	public float deformedArmLength = 1f;

	public float deformedLegLength = 1f;

	public float deformedFootSize = 1f;

	public float deformedStandPos;

	public override void InitShapeInfo(string manifest, string assetBundleAnmKey, string assetBundleCategory, string anmKeyInfoName, string cateInfoName, Transform trfObj, int _sex)
	{
		base.sex = _sex;
		if (base.sex == 0)
		{
			correctHeadSize = 0.91f;
			correctNeckSize = 0.91f;
		}
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		DstName[] array = (DstName[])Enum.GetValues(typeof(DstName));
		for (int i = 0; i < array.Length; i++)
		{
			DstName value = array[i];
			dictionary[value.ToString()] = (int)value;
		}
		Dictionary<string, int> dictionary2 = new Dictionary<string, int>();
		SrcName[] array2 = (SrcName[])Enum.GetValues(typeof(SrcName));
		for (int i = 0; i < array2.Length; i++)
		{
			SrcName value2 = array2[i];
			dictionary2[value2.ToString()] = (int)value2;
		}
		FindAssist findAssist = new FindAssist();
		findAssist.Initialize(trfObj);
		GameObject objectFromName = findAssist.GetObjectFromName("cf_d_shoulder_L");
		if ((bool)objectFromName)
		{
			fixCorrectBone[0] = objectFromName.transform;
		}
		objectFromName = findAssist.GetObjectFromName("cf_d_shoulder_R");
		if ((bool)objectFromName)
		{
			fixCorrectBone[1] = objectFromName.transform;
		}
		InitShapeInfoBase(manifest, assetBundleAnmKey, assetBundleCategory, anmKeyInfoName, cateInfoName, trfObj, dictionary, dictionary2, Character.AddLoadAssetBundle);
		int num = Enum.GetNames(typeof(CorrectKeyName)).Length;
		correctValue = new BoneInfo[num];
		for (int j = 0; j < num; j++)
		{
			correctValue[j] = new BoneInfo();
			correctValue[j].vctScl = Vector3.zero;
		}
		LoadCorrectInfo();
		base.InitEnd = true;
	}

	public void SaveCorrectInfo()
	{
		using FileStream output = new FileStream(Application.dataPath + "/Illusion/assetbundle/chara/list/shapecorrect/shapecorrect.bytes", FileMode.Create, FileAccess.Write);
		using BinaryWriter binaryWriter = new BinaryWriter(output);
		int num = correctValue.Length;
		binaryWriter.Write(num);
		for (int i = 0; i < num; i++)
		{
			binaryWriter.Write(correctValue[i].vctPos.x);
			binaryWriter.Write(correctValue[i].vctPos.y);
			binaryWriter.Write(correctValue[i].vctPos.z);
			binaryWriter.Write(correctValue[i].vctRot.x);
			binaryWriter.Write(correctValue[i].vctRot.y);
			binaryWriter.Write(correctValue[i].vctRot.z);
			binaryWriter.Write(correctValue[i].vctScl.x);
			binaryWriter.Write(correctValue[i].vctScl.y);
			binaryWriter.Write(correctValue[i].vctScl.z);
		}
	}

	public void LoadCorrectInfo()
	{
		string assetBundleName = "list/shapecorrect/shapecorrect.unity3d";
		string assetName = "shapecorrect";
		TextAsset textAsset = CommonLib.LoadAsset<TextAsset>(assetBundleName, assetName);
		if (null != textAsset)
		{
			using MemoryStream memoryStream = new MemoryStream();
			memoryStream.Write(textAsset.bytes, 0, textAsset.bytes.Length);
			memoryStream.Seek(0L, SeekOrigin.Begin);
			using BinaryReader binaryReader = new BinaryReader(memoryStream);
			int num = binaryReader.ReadInt32();
			for (int i = 0; i < num; i++)
			{
				correctValue[i].vctPos.x = binaryReader.ReadSingle();
				correctValue[i].vctPos.y = binaryReader.ReadSingle();
				correctValue[i].vctPos.z = binaryReader.ReadSingle();
				correctValue[i].vctRot.x = binaryReader.ReadSingle();
				correctValue[i].vctRot.y = binaryReader.ReadSingle();
				correctValue[i].vctRot.z = binaryReader.ReadSingle();
				correctValue[i].vctScl.x = binaryReader.ReadSingle();
				correctValue[i].vctScl.y = binaryReader.ReadSingle();
				correctValue[i].vctScl.z = binaryReader.ReadSingle();
			}
		}
		AssetBundleManager.UnloadAssetBundle(assetBundleName, isUnloadForceRefCount: true);
	}

	public override void ForceUpdate()
	{
		Update();
	}

	public override void Update()
	{
		if (!base.InitEnd || dictSrc.Count == 0)
		{
			return;
		}
		BoneInfo value = null;
		Vector3 vector = dictSrc[0].vctScl * deformedHeight;
		Vector3 vector2 = dictSrc[1].vctScl * deformedHeightAid;
		if ((updateMask & 4) != 0)
		{
			if (dictDst.TryGetValue(0, out value))
			{
				value.trfBone.SetLocalScale(vector.x, vector.y, vector.z);
			}
			if (dictDst.TryGetValue(1, out value))
			{
				value.trfBone.SetLocalScale(vector2.x, vector2.y, vector2.z);
			}
			if (dictDst.TryGetValue(2, out value))
			{
				value.trfBone.SetLocalScale(vector2.x, vector2.y, vector2.z);
			}
			if (dictDst.TryGetValue(3, out value))
			{
				BoneInfo boneInfo = correctValue[2];
				float num = ((base.typeBone == 0) ? 0f : boneInfo.vctPos.y);
				float num2 = ((base.typeBone == 0) ? 0f : boneInfo.vctScl.x);
				float num3 = ((base.typeBone == 0) ? 0f : boneInfo.vctScl.y);
				float num4 = ((base.typeBone == 0) ? 0f : boneInfo.vctScl.z);
				value.trfBone.SetLocalPositionY(dictSrc[2].vctPos.y + num);
				value.trfBone.SetLocalScale(dictSrc[2].vctScl.x * vector2.x * correctHeadSize * deformedHeadSize + num2, dictSrc[2].vctScl.y * vector2.y * correctHeadSize * deformedHeadSize + num3, dictSrc[2].vctScl.z * vector2.z * correctHeadSize * deformedHeadSize + num4);
			}
			if (dictDst.TryGetValue(4, out value))
			{
				BoneInfo boneInfo2 = correctValue[1];
				float y = ((base.typeBone == 0) ? 0f : boneInfo2.vctPos.y);
				float x = ((base.typeBone == 0) ? 0f : boneInfo2.vctRot.x);
				float num5 = ((base.typeBone == 0) ? 0f : boneInfo2.vctScl.x);
				float num6 = ((base.typeBone == 0) ? 0f : boneInfo2.vctScl.z);
				value.trfBone.SetLocalPositionY(y);
				value.trfBone.SetLocalPositionZ(dictSrc[3].vctPos.z);
				value.trfBone.SetLocalRotation(x, 0f, 0f);
				value.trfBone.SetLocalScale(dictSrc[3].vctScl.x * vector2.x * correctNeckSize * deformedNeckSize + num5, 1f, dictSrc[3].vctScl.z * vector2.z * correctNeckSize * deformedNeckSize + num6);
			}
			if (dictDst.TryGetValue(5, out value))
			{
				BoneInfo boneInfo3 = correctValue[5];
				float num7 = ((base.typeBone == 0) ? 0f : boneInfo3.vctPos.z);
				float x2 = ((base.typeBone == 0) ? 0f : boneInfo3.vctRot.x);
				float num8 = ((base.typeBone == 0) ? 0f : boneInfo3.vctScl.x);
				float num9 = ((base.typeBone == 0) ? 0f : boneInfo3.vctScl.z);
				value.trfBone.SetLocalPositionZ(dictSrc[4].vctPos.z + num7);
				value.trfBone.SetLocalRotation(x2, 0f, 0f);
				value.trfBone.SetLocalScale(dictSrc[4].vctScl.x * vector2.x + num8, 1f, dictSrc[4].vctScl.z * vector2.z + num9);
			}
			if (dictDst.TryGetValue(6, out value))
			{
				BoneInfo boneInfo4 = correctValue[14];
				float num10 = ((base.typeBone == 0) ? 0f : boneInfo4.vctPos.x);
				float num11 = ((base.typeBone == 0) ? 0f : boneInfo4.vctPos.y);
				float z = ((base.typeBone == 0) ? 0f : boneInfo4.vctPos.z);
				float num12 = ((base.typeBone == 0) ? 0f : boneInfo4.vctScl.y);
				float num13 = ((base.typeBone == 0) ? 0f : boneInfo4.vctScl.z);
				value.trfBone.SetLocalPositionX(dictSrc[6].vctPos.x + dictSrc[5].vctPos.x + num10);
				value.trfBone.SetLocalPositionY(dictSrc[5].vctPos.y + num11);
				value.trfBone.SetLocalPositionZ(z);
				value.trfBone.SetLocalScale(dictSrc[5].vctScl.x, dictSrc[5].vctScl.y * vector2.y + num12, dictSrc[5].vctScl.z * dictSrc[6].vctScl.z * vector2.z + num13);
			}
			if (dictDst.TryGetValue(7, out value))
			{
				BoneInfo boneInfo5 = correctValue[15];
				float num14 = ((base.typeBone == 0) ? 0f : boneInfo5.vctPos.x);
				float num15 = ((base.typeBone == 0) ? 0f : boneInfo5.vctPos.y);
				float z2 = ((base.typeBone == 0) ? 0f : boneInfo5.vctPos.z);
				float num16 = ((base.typeBone == 0) ? 0f : boneInfo5.vctScl.y);
				float num17 = ((base.typeBone == 0) ? 0f : boneInfo5.vctScl.z);
				value.trfBone.SetLocalPositionX(dictSrc[7].vctPos.x - dictSrc[5].vctPos.x + num14);
				value.trfBone.SetLocalPositionY(dictSrc[5].vctPos.y + num15);
				value.trfBone.SetLocalPositionZ(z2);
				value.trfBone.SetLocalScale(dictSrc[5].vctScl.x, dictSrc[5].vctScl.y * vector2.y + num16, dictSrc[5].vctScl.z * dictSrc[7].vctScl.z * vector2.z + num17);
			}
			if (dictDst.TryGetValue(8, out value))
			{
				BoneInfo boneInfo6 = correctValue[16];
				float num18 = ((base.typeBone == 0) ? 0f : boneInfo6.vctPos.y);
				float num19 = ((base.typeBone == 0) ? 0f : boneInfo6.vctScl.x);
				float num20 = ((base.typeBone == 0) ? 0f : boneInfo6.vctScl.y);
				float num21 = ((base.typeBone == 0) ? 0f : boneInfo6.vctScl.z);
				value.trfBone.SetLocalPositionX(dictSrc[8].vctPos.x);
				value.trfBone.SetLocalPositionY(dictSrc[5].vctPos.y + num18);
				value.trfBone.SetLocalScale(1f + num19, dictSrc[5].vctScl.y * vector2.y + num20, dictSrc[5].vctScl.z * dictSrc[8].vctScl.z * vector2.z + num21);
			}
			if (dictDst.TryGetValue(9, out value))
			{
				BoneInfo boneInfo7 = correctValue[17];
				float num22 = ((base.typeBone == 0) ? 0f : boneInfo7.vctPos.y);
				float num23 = ((base.typeBone == 0) ? 0f : boneInfo7.vctScl.x);
				float num24 = ((base.typeBone == 0) ? 0f : boneInfo7.vctScl.y);
				float num25 = ((base.typeBone == 0) ? 0f : boneInfo7.vctScl.z);
				value.trfBone.SetLocalPositionX(dictSrc[9].vctPos.x);
				value.trfBone.SetLocalPositionY(dictSrc[5].vctPos.y + num22);
				value.trfBone.SetLocalScale(1f + num23, dictSrc[5].vctScl.y * vector2.y + num24, dictSrc[5].vctScl.z * dictSrc[9].vctScl.z * vector2.z + num25);
			}
			if (dictDst.TryGetValue(10, out value))
			{
				BoneInfo boneInfo8 = correctValue[18];
				float num26 = ((base.typeBone == 0) ? 0f : boneInfo8.vctPos.y);
				float num27 = ((base.typeBone == 0) ? 0f : boneInfo8.vctPos.z);
				float num28 = ((base.typeBone == 0) ? 0f : boneInfo8.vctScl.y);
				float num29 = ((base.typeBone == 0) ? 0f : boneInfo8.vctScl.z);
				value.trfBone.SetLocalPositionY(dictSrc[10].vctPos.y + num26);
				value.trfBone.SetLocalPositionZ(dictSrc[10].vctPos.z + num27);
				value.trfBone.SetLocalScale(1f, dictSrc[10].vctScl.y * vector2.y + num28, dictSrc[10].vctScl.z * vector2.z + num29);
			}
			if (dictDst.TryGetValue(11, out value))
			{
				BoneInfo boneInfo9 = correctValue[19];
				float num30 = ((base.typeBone == 0) ? 0f : boneInfo9.vctPos.y);
				float num31 = ((base.typeBone == 0) ? 0f : boneInfo9.vctPos.z);
				float num32 = ((base.typeBone == 0) ? 0f : boneInfo9.vctScl.y);
				float num33 = ((base.typeBone == 0) ? 0f : boneInfo9.vctScl.z);
				value.trfBone.SetLocalPositionY(dictSrc[10].vctPos.y + num30);
				value.trfBone.SetLocalPositionZ(dictSrc[10].vctPos.z + num31);
				value.trfBone.SetLocalScale(1f, dictSrc[10].vctScl.y * vector2.y + num32, dictSrc[10].vctScl.z * vector2.z + num33);
			}
			if (dictDst.TryGetValue(12, out value))
			{
				BoneInfo boneInfo10 = correctValue[20];
				float y2 = ((base.typeBone == 0) ? 0f : boneInfo10.vctPos.y);
				float num34 = ((base.typeBone == 0) ? 0f : boneInfo10.vctScl.y);
				float num35 = ((base.typeBone == 0) ? 0f : boneInfo10.vctScl.z);
				value.trfBone.SetLocalPositionY(y2);
				value.trfBone.SetLocalScale(1f, dictSrc[11].vctScl.y * dictSrc[12].vctScl.y * vector2.y + num34, dictSrc[11].vctScl.z * dictSrc[12].vctScl.z * vector2.z + num35);
			}
			if (dictDst.TryGetValue(13, out value))
			{
				BoneInfo boneInfo11 = correctValue[21];
				float y3 = ((base.typeBone == 0) ? 0f : boneInfo11.vctPos.y);
				float num36 = ((base.typeBone == 0) ? 0f : boneInfo11.vctScl.y);
				float num37 = ((base.typeBone == 0) ? 0f : boneInfo11.vctScl.z);
				value.trfBone.SetLocalPositionY(y3);
				value.trfBone.SetLocalScale(1f, dictSrc[11].vctScl.y * dictSrc[12].vctScl.y * vector2.y + num36, dictSrc[11].vctScl.z * dictSrc[12].vctScl.z * vector2.z + num37);
			}
			if (dictDst.TryGetValue(14, out value))
			{
				float y4 = ((base.typeBone == 0) ? 0f : correctValue[22].vctPos.y);
				value.trfBone.SetLocalPositionY(y4);
				value.trfBone.SetLocalScale(1f, dictSrc[13].vctScl.y * vector2.y, dictSrc[13].vctScl.z * vector2.z);
			}
			if (dictDst.TryGetValue(15, out value))
			{
				float y5 = ((base.typeBone == 0) ? 0f : correctValue[23].vctPos.y);
				value.trfBone.SetLocalPositionY(y5);
				value.trfBone.SetLocalScale(1f, dictSrc[13].vctScl.y * vector2.y, dictSrc[13].vctScl.z * vector2.z);
			}
			if (dictDst.TryGetValue(16, out value))
			{
				BoneInfo boneInfo12 = correctValue[24];
				float y6 = ((base.typeBone == 0) ? 0f : boneInfo12.vctRot.y);
				float z3 = ((base.typeBone == 0) ? 0f : boneInfo12.vctRot.z);
				float num38 = ((base.typeBone == 0) ? 0f : boneInfo12.vctScl.y);
				float num39 = ((base.typeBone == 0) ? 0f : boneInfo12.vctScl.z);
				value.trfBone.SetLocalRotation(0f, y6, z3);
				value.trfBone.SetLocalScale(1f, dictSrc[14].vctScl.y * dictSrc[15].vctScl.y * vector2.y + num38, dictSrc[14].vctScl.z * dictSrc[15].vctScl.z * vector2.z + num39);
			}
			if (dictDst.TryGetValue(17, out value))
			{
				BoneInfo boneInfo13 = correctValue[25];
				float y7 = ((base.typeBone == 0) ? 0f : boneInfo13.vctRot.y);
				float z4 = ((base.typeBone == 0) ? 0f : boneInfo13.vctRot.z);
				float num40 = ((base.typeBone == 0) ? 0f : boneInfo13.vctScl.y);
				float num41 = ((base.typeBone == 0) ? 0f : boneInfo13.vctScl.z);
				value.trfBone.SetLocalRotation(0f, y7, z4);
				value.trfBone.SetLocalScale(1f, dictSrc[14].vctScl.y * dictSrc[15].vctScl.y * vector2.y + num40, dictSrc[14].vctScl.z * dictSrc[15].vctScl.z * vector2.z + num41);
			}
			if (dictDst.TryGetValue(18, out value))
			{
				BoneInfo boneInfo14 = correctValue[26];
				float num42 = ((base.typeBone == 0) ? 0f : boneInfo14.vctScl.x);
				float num43 = ((base.typeBone == 0) ? 0f : boneInfo14.vctScl.y);
				value.trfBone.SetLocalScale(1f + num42, dictSrc[16].vctScl.y * vector2.y + num43, dictSrc[16].vctScl.z * vector2.z);
			}
			if (dictDst.TryGetValue(19, out value))
			{
				BoneInfo boneInfo15 = correctValue[27];
				float num44 = ((base.typeBone == 0) ? 0f : boneInfo15.vctScl.x);
				float num45 = ((base.typeBone == 0) ? 0f : boneInfo15.vctScl.y);
				value.trfBone.SetLocalScale(1f + num44, dictSrc[16].vctScl.y * vector2.y + num45, dictSrc[16].vctScl.z * vector2.z);
			}
			if (dictDst.TryGetValue(20, out value))
			{
				BoneInfo boneInfo16 = correctValue[4];
				float num46 = ((base.typeBone == 0) ? 0f : boneInfo16.vctPos.z);
				float num47 = ((base.typeBone == 0) ? 0f : boneInfo16.vctScl.x);
				float num48 = ((base.typeBone == 0) ? 0f : boneInfo16.vctScl.z);
				value.trfBone.SetLocalPositionZ(dictSrc[17].vctPos.z + dictSrc[18].vctPos.z + num46);
				value.trfBone.SetLocalScale(dictSrc[17].vctScl.x * vector2.x + num47, 1f, dictSrc[17].vctScl.z * vector2.z * dictSrc[18].vctScl.z + num48);
			}
			if (dictDst.TryGetValue(21, out value))
			{
				BoneInfo boneInfo17 = correctValue[3];
				float num49 = ((base.typeBone == 0) ? 0f : boneInfo17.vctPos.z);
				float num50 = ((base.typeBone == 0) ? 0f : boneInfo17.vctScl.x);
				float num51 = ((base.typeBone == 0) ? 0f : boneInfo17.vctScl.z);
				value.trfBone.SetLocalPositionY(dictSrc[19].vctPos.y + dictSrc[20].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[19].vctPos.z + dictSrc[20].vctPos.z + num49);
				value.trfBone.SetLocalRotation(dictSrc[20].vctRot.x, 0f, 0f);
				value.trfBone.SetLocalScale(dictSrc[19].vctScl.x * vector2.x * dictSrc[20].vctScl.x + num50, dictSrc[20].vctScl.y, dictSrc[19].vctScl.z * vector2.z * dictSrc[20].vctScl.z + num51);
			}
			if (dictDst.TryGetValue(22, out value))
			{
				value.trfBone.SetLocalPositionY(dictSrc[20].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[21].vctPos.z + dictSrc[20].vctPos.z);
				value.trfBone.SetLocalRotation(dictSrc[20].vctRot.x, 0f, 0f);
				value.trfBone.SetLocalScale(dictSrc[21].vctScl.x * vector2.x * dictSrc[20].vctScl.x, dictSrc[20].vctScl.y, dictSrc[21].vctScl.z * vector2.z * dictSrc[20].vctScl.z);
			}
			if (dictDst.TryGetValue(23, out value))
			{
				float num52 = ((base.typeBone == 0) ? 0f : correctValue[0].vctScl.x);
				value.trfBone.SetLocalPositionZ(dictSrc[22].vctPos.z);
				value.trfBone.SetLocalScale(dictSrc[22].vctScl.x * vector2.x + num52, 1f, dictSrc[22].vctScl.z * vector2.z);
			}
			if (dictDst.TryGetValue(24, out value))
			{
				BoneInfo boneInfo18 = correctValue[28];
				float x3 = ((base.typeBone == 0) ? 0f : boneInfo18.vctPos.x);
				float num53 = ((base.typeBone == 0) ? 0f : boneInfo18.vctRot.x);
				float z5 = ((base.typeBone == 0) ? 0f : boneInfo18.vctRot.z);
				float num54 = ((base.typeBone == 0) ? 0f : boneInfo18.vctScl.x);
				value.trfBone.SetLocalPositionX(x3);
				value.trfBone.SetLocalPositionY(dictSrc[23].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[23].vctPos.z + dictSrc[22].vctPos.z * 0.3f);
				value.trfBone.SetLocalRotation(dictSrc[23].vctRot.x + num53, 0f, z5);
				value.trfBone.SetLocalScale(dictSrc[23].vctScl.x + (-1f + dictSrc[22].vctScl.x) * 0.5f + num54, dictSrc[23].vctScl.y, dictSrc[23].vctScl.z + (-1f + dictSrc[22].vctScl.z) * 0.5f + (-1f + dictSrc[21].vctScl.z) * 0.5f);
			}
			if (dictDst.TryGetValue(25, out value))
			{
				BoneInfo boneInfo19 = correctValue[29];
				float x4 = ((base.typeBone == 0) ? 0f : boneInfo19.vctPos.x);
				float num55 = ((base.typeBone == 0) ? 0f : boneInfo19.vctRot.x);
				float z6 = ((base.typeBone == 0) ? 0f : boneInfo19.vctRot.z);
				float num56 = ((base.typeBone == 0) ? 0f : boneInfo19.vctScl.x);
				value.trfBone.SetLocalPositionX(x4);
				value.trfBone.SetLocalPositionY(dictSrc[23].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[23].vctPos.z + dictSrc[22].vctPos.z * 0.3f);
				value.trfBone.SetLocalRotation(dictSrc[23].vctRot.x + num55, 0f, z6);
				value.trfBone.SetLocalScale(dictSrc[23].vctScl.x + (-1f + dictSrc[22].vctScl.x) * 0.5f + num56, dictSrc[23].vctScl.y, dictSrc[23].vctScl.z + (-1f + dictSrc[22].vctScl.z) * 0.5f + (-1f + dictSrc[21].vctScl.z) * 0.5f);
			}
			if (dictDst.TryGetValue(26, out value))
			{
				BoneInfo boneInfo20 = correctValue[6];
				float num57 = ((base.typeBone == 0) ? 0f : boneInfo20.vctPos.x);
				float num58 = ((base.typeBone == 0) ? 0f : boneInfo20.vctPos.z);
				float num59 = ((base.typeBone == 0) ? 0f : boneInfo20.vctRot.x);
				float y8 = ((base.typeBone == 0) ? 0f : boneInfo20.vctRot.y);
				float num60 = ((base.typeBone == 0) ? 0f : boneInfo20.vctRot.z);
				float num61 = ((base.typeBone == 0) ? 0f : boneInfo20.vctScl.x);
				float num62 = ((base.typeBone == 0) ? 0f : boneInfo20.vctScl.z);
				value.trfBone.SetLocalPositionX(dictSrc[24].vctPos.x + dictSrc[25].vctPos.x + num57);
				value.trfBone.SetLocalPositionZ(dictSrc[24].vctPos.z + num58);
				value.trfBone.SetLocalRotation(dictSrc[24].vctRot.x + num59, y8, dictSrc[24].vctRot.z + num60);
				value.trfBone.SetLocalScale(dictSrc[24].vctScl.x * dictSrc[25].vctScl.x * vector2.x + num61, 1f, dictSrc[24].vctScl.z * dictSrc[25].vctScl.z * vector2.z + num62);
			}
			if (dictDst.TryGetValue(27, out value))
			{
				BoneInfo boneInfo21 = correctValue[7];
				float num63 = ((base.typeBone == 0) ? 0f : boneInfo21.vctPos.x);
				float num64 = ((base.typeBone == 0) ? 0f : boneInfo21.vctPos.z);
				float num65 = ((base.typeBone == 0) ? 0f : boneInfo21.vctRot.x);
				float y9 = ((base.typeBone == 0) ? 0f : boneInfo21.vctRot.y);
				float num66 = ((base.typeBone == 0) ? 0f : boneInfo21.vctRot.z);
				float num67 = ((base.typeBone == 0) ? 0f : boneInfo21.vctScl.x);
				float num68 = ((base.typeBone == 0) ? 0f : boneInfo21.vctScl.z);
				value.trfBone.SetLocalPositionX(dictSrc[26].vctPos.x + dictSrc[27].vctPos.x + num63);
				value.trfBone.SetLocalPositionZ(dictSrc[26].vctPos.z + num64);
				value.trfBone.SetLocalRotation(dictSrc[26].vctRot.x + num65, y9, dictSrc[26].vctRot.z + num66);
				value.trfBone.SetLocalScale(dictSrc[26].vctScl.x * dictSrc[27].vctScl.x * vector2.x + num67, 1f, dictSrc[26].vctScl.z * dictSrc[27].vctScl.z * vector2.z + num68);
			}
			if (dictDst.TryGetValue(28, out value))
			{
				BoneInfo boneInfo22 = correctValue[8];
				float num69 = ((base.typeBone == 0) ? 0f : boneInfo22.vctPos.x);
				float num70 = ((base.typeBone == 0) ? 0f : boneInfo22.vctScl.x);
				float num71 = ((base.typeBone == 0) ? 0f : boneInfo22.vctScl.z);
				value.trfBone.SetLocalPositionX(dictSrc[28].vctPos.x + dictSrc[29].vctPos.x + num69);
				value.trfBone.SetLocalPositionZ(dictSrc[28].vctPos.z + dictSrc[29].vctPos.z);
				value.trfBone.SetLocalScale(dictSrc[28].vctScl.x * dictSrc[29].vctScl.x * vector2.x + num70, 1f, dictSrc[28].vctScl.z * dictSrc[29].vctScl.z * vector2.z + num71);
			}
			if (dictDst.TryGetValue(29, out value))
			{
				BoneInfo boneInfo23 = correctValue[9];
				float num72 = ((base.typeBone == 0) ? 0f : boneInfo23.vctPos.x);
				float num73 = ((base.typeBone == 0) ? 0f : boneInfo23.vctScl.x);
				float num74 = ((base.typeBone == 0) ? 0f : boneInfo23.vctScl.z);
				value.trfBone.SetLocalPositionX(dictSrc[30].vctPos.x + dictSrc[31].vctPos.x + num72);
				value.trfBone.SetLocalPositionZ(dictSrc[30].vctPos.z + dictSrc[31].vctPos.z);
				value.trfBone.SetLocalScale(dictSrc[30].vctScl.x * dictSrc[31].vctScl.x * vector2.x + num73, 1f, dictSrc[30].vctScl.z * dictSrc[31].vctScl.z * vector2.z + num74);
			}
			if (dictDst.TryGetValue(30, out value))
			{
				BoneInfo boneInfo24 = correctValue[10];
				float num75 = ((base.typeBone == 0) ? 0f : boneInfo24.vctPos.z);
				float num76 = ((base.typeBone == 0) ? 0f : boneInfo24.vctScl.x);
				value.trfBone.SetLocalPositionX(dictSrc[32].vctPos.x);
				value.trfBone.SetLocalPositionZ(dictSrc[32].vctPos.z + num75);
				value.trfBone.SetLocalRotation(dictSrc[32].vctRot.x, 0f, dictSrc[32].vctRot.z);
				value.trfBone.SetLocalScale(dictSrc[32].vctScl.x * vector2.x + num76, 1f, dictSrc[32].vctScl.z * vector2.z);
			}
			if (dictDst.TryGetValue(31, out value))
			{
				BoneInfo boneInfo25 = correctValue[11];
				float num77 = ((base.typeBone == 0) ? 0f : boneInfo25.vctPos.z);
				float num78 = ((base.typeBone == 0) ? 0f : boneInfo25.vctScl.x);
				value.trfBone.SetLocalPositionX(dictSrc[33].vctPos.x);
				value.trfBone.SetLocalPositionZ(dictSrc[33].vctPos.z + num77);
				value.trfBone.SetLocalRotation(dictSrc[33].vctRot.x, 0f, dictSrc[33].vctRot.z);
				value.trfBone.SetLocalScale(dictSrc[33].vctScl.x * vector2.x + num78, 1f, dictSrc[33].vctScl.z * vector2.z);
			}
			if (dictDst.TryGetValue(32, out value))
			{
				BoneInfo boneInfo26 = correctValue[12];
				float num79 = ((base.typeBone == 0) ? 0f : boneInfo26.vctScl.x);
				float num80 = ((base.typeBone == 0) ? 0f : boneInfo26.vctScl.z);
				value.trfBone.SetLocalPositionX(dictSrc[34].vctPos.x);
				value.trfBone.SetLocalPositionZ(dictSrc[34].vctPos.z);
				value.trfBone.SetLocalScale(dictSrc[34].vctScl.x * vector2.x + num79, 1f, dictSrc[34].vctScl.z * vector2.z + num80);
			}
			if (dictDst.TryGetValue(33, out value))
			{
				BoneInfo boneInfo27 = correctValue[13];
				float num81 = ((base.typeBone == 0) ? 0f : boneInfo27.vctScl.x);
				float num82 = ((base.typeBone == 0) ? 0f : boneInfo27.vctScl.z);
				value.trfBone.SetLocalPositionX(dictSrc[35].vctPos.x);
				value.trfBone.SetLocalPositionZ(dictSrc[35].vctPos.z);
				value.trfBone.SetLocalScale(dictSrc[35].vctScl.x * vector2.x + num81, 1f, dictSrc[35].vctScl.z * vector2.z + num82);
			}
			if (dictDst.TryGetValue(34, out value))
			{
				value.trfBone.SetLocalPositionY(dictSrc[36].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[36].vctPos.z);
				value.trfBone.SetLocalScale(dictSrc[36].vctScl.x * vector2.x, 1f, dictSrc[36].vctScl.z * vector2.z);
			}
			if (dictDst.TryGetValue(35, out value))
			{
				value.trfBone.SetLocalPositionY(dictSrc[37].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[37].vctPos.z);
				value.trfBone.SetLocalScale(dictSrc[37].vctScl.x * vector2.x, 1f, dictSrc[37].vctScl.z * vector2.z);
			}
			if (dictDst.TryGetValue(36, out value))
			{
				value.trfBone.SetLocalScale(dictSrc[38].vctScl.x, 1f, dictSrc[38].vctScl.z);
			}
			if (dictDst.TryGetValue(37, out value))
			{
				value.trfBone.SetLocalScale(dictSrc[38].vctScl.x, 1f, dictSrc[38].vctScl.z);
			}
			if (dictDst.TryGetValue(67, out value))
			{
				value.trfBone.SetLocalPositionX(dictSrc[65].vctPos.x + dictSrc[66].vctPos.x + dictSrc[67].vctPos.x + dictSrc[68].vctPos.x + dictSrc[69].vctPos.x + dictSrc[70].vctPos.x);
				value.trfBone.SetLocalPositionY(dictSrc[67].vctPos.y + dictSrc[68].vctPos.y + dictSrc[70].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[65].vctPos.z + dictSrc[67].vctPos.z + dictSrc[70].vctPos.z);
				value.trfBone.SetLocalScale(dictSrc[65].vctScl.x * dictSrc[67].vctScl.x * dictSrc[68].vctScl.x * vector2.x * dictSrc[70].vctScl.x, dictSrc[65].vctScl.y * dictSrc[67].vctScl.y * dictSrc[68].vctScl.y * vector2.y * dictSrc[70].vctScl.x, dictSrc[65].vctScl.z * dictSrc[67].vctScl.z * dictSrc[68].vctScl.z * vector2.z * dictSrc[70].vctScl.x);
			}
			if (dictDst.TryGetValue(68, out value))
			{
				value.trfBone.SetLocalPositionX(0f - dictSrc[65].vctPos.x - dictSrc[66].vctPos.x - dictSrc[67].vctPos.x - dictSrc[68].vctPos.x - dictSrc[69].vctPos.x - dictSrc[70].vctPos.x);
				value.trfBone.SetLocalPositionY(dictSrc[67].vctPos.y + dictSrc[68].vctPos.y + dictSrc[70].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[65].vctPos.z + dictSrc[67].vctPos.z + dictSrc[70].vctPos.z);
				value.trfBone.SetLocalScale(dictSrc[65].vctScl.x * dictSrc[67].vctScl.x * dictSrc[68].vctScl.x * vector2.x * dictSrc[70].vctScl.x, dictSrc[65].vctScl.y * dictSrc[67].vctScl.y * dictSrc[68].vctScl.y * vector2.y * dictSrc[70].vctScl.x, dictSrc[65].vctScl.z * dictSrc[67].vctScl.z * dictSrc[68].vctScl.z * vector2.z * dictSrc[70].vctScl.x);
			}
			if (dictDst.TryGetValue(69, out value))
			{
				value.trfBone.SetLocalPositionY(dictSrc[71].vctPos.y + dictSrc[72].vctPos.y + dictSrc[73].vctPos.y + dictSrc[74].vctPos.y + dictSrc[75].vctPos.y + dictSrc[76].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[72].vctPos.z + dictSrc[73].vctPos.z);
				value.trfBone.SetLocalScale(dictSrc[71].vctScl.x * dictSrc[72].vctScl.x * vector2.x * dictSrc[73].vctScl.x * dictSrc[76].vctScl.x * dictSrc[75].vctScl.x, dictSrc[71].vctScl.x * dictSrc[72].vctScl.x * vector2.y * dictSrc[73].vctScl.y * dictSrc[76].vctScl.y * dictSrc[75].vctScl.y, dictSrc[71].vctScl.x * dictSrc[72].vctScl.x * vector2.z * dictSrc[73].vctScl.z * dictSrc[76].vctScl.z * dictSrc[75].vctScl.z);
			}
			if (dictDst.TryGetValue(70, out value))
			{
				value.trfBone.SetLocalPositionX(dictSrc[77].vctPos.x);
				value.trfBone.SetLocalPositionY(dictSrc[77].vctPos.y + dictSrc[74].vctPos.y * 2f + dictSrc[78].vctPos.y + dictSrc[79].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[77].vctPos.z + dictSrc[78].vctPos.z + dictSrc[79].vctPos.z);
				value.trfBone.SetLocalScale(dictSrc[77].vctScl.x * dictSrc[78].vctScl.x * vector2.x * dictSrc[79].vctScl.x, dictSrc[77].vctScl.y * dictSrc[78].vctScl.y * vector2.y * dictSrc[79].vctScl.y, dictSrc[77].vctScl.z * dictSrc[78].vctScl.z * vector2.z * dictSrc[79].vctScl.z);
			}
			if (dictDst.TryGetValue(71, out value))
			{
				value.trfBone.SetLocalPositionY(dictSrc[80].vctPos.y + dictSrc[81].vctPos.y + dictSrc[82].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[82].vctPos.z);
				value.trfBone.SetLocalScale(dictSrc[81].vctScl.x * dictSrc[82].vctScl.x * vector2.x, dictSrc[81].vctScl.y * dictSrc[82].vctScl.y * vector2.y, dictSrc[81].vctScl.z * dictSrc[82].vctScl.z * vector2.z);
			}
			if (dictDst.TryGetValue(72, out value))
			{
				value.trfBone.SetLocalPositionX(dictSrc[83].vctPos.x + dictSrc[84].vctPos.x + dictSrc[85].vctPos.x);
				value.trfBone.SetLocalPositionY(dictSrc[83].vctPos.y + dictSrc[84].vctPos.y + dictSrc[85].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[83].vctPos.z + dictSrc[84].vctPos.z);
				value.trfBone.SetLocalScale(dictSrc[83].vctScl.x * dictSrc[84].vctScl.x * dictSrc[85].vctScl.x * vector2.x, dictSrc[83].vctScl.y * dictSrc[84].vctScl.y * dictSrc[85].vctScl.y * vector2.y, dictSrc[83].vctScl.z * dictSrc[84].vctScl.z * dictSrc[85].vctScl.z * vector2.z);
			}
			if (dictDst.TryGetValue(73, out value))
			{
				value.trfBone.SetLocalPositionX(0f - dictSrc[83].vctPos.x - dictSrc[84].vctPos.x - dictSrc[85].vctPos.x);
				value.trfBone.SetLocalPositionY(dictSrc[83].vctPos.y + dictSrc[84].vctPos.y + dictSrc[85].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[83].vctPos.z + dictSrc[84].vctPos.z);
				value.trfBone.SetLocalScale(dictSrc[83].vctScl.x * dictSrc[84].vctScl.x * dictSrc[85].vctScl.x * vector2.x, dictSrc[83].vctScl.y * dictSrc[84].vctScl.y * dictSrc[85].vctScl.y * vector2.y, dictSrc[83].vctScl.z * dictSrc[84].vctScl.z * dictSrc[85].vctScl.z * vector2.z);
			}
			if (dictDst.TryGetValue(74, out value))
			{
				value.trfBone.SetLocalScale(1f, dictSrc[86].vctScl.y * dictSrc[87].vctScl.y * dictSrc[88].vctScl.y * vector2.x, dictSrc[86].vctScl.z * dictSrc[87].vctScl.z * dictSrc[88].vctScl.z * vector2.x);
			}
			if (dictDst.TryGetValue(75, out value))
			{
				value.trfBone.SetLocalScale(1f, dictSrc[86].vctScl.y * dictSrc[87].vctScl.y * dictSrc[88].vctScl.y * vector2.x, dictSrc[86].vctScl.z * dictSrc[87].vctScl.z * dictSrc[88].vctScl.z * vector2.x);
			}
			if (dictDst.TryGetValue(76, out value))
			{
				value.trfBone.SetLocalPositionZ(dictSrc[89].vctPos.z + dictSrc[90].vctPos.z);
				value.trfBone.SetLocalScale(dictSrc[89].vctScl.x * dictSrc[90].vctScl.x * vector2.x, dictSrc[89].vctScl.y * dictSrc[90].vctScl.y * vector2.y, dictSrc[89].vctScl.z * dictSrc[90].vctScl.z * vector2.z);
			}
			float num83 = ((180f < dictSrc[94].vctRot.x) ? (dictSrc[94].vctRot.x - 360f) : dictSrc[94].vctRot.x);
			float num84 = ((180f < dictSrc[97].vctRot.x) ? (dictSrc[97].vctRot.x - 360f) : dictSrc[97].vctRot.x);
			if (dictDst.TryGetValue(77, out value))
			{
				value.trfBone.SetLocalScale(vector2.x, vector2.y, vector2.z);
			}
			if (dictDst.TryGetValue(78, out value))
			{
				value.trfBone.SetLocalPositionX(dictSrc[91].vctPos.x + dictSrc[92].vctPos.x);
				value.trfBone.SetLocalPositionZ(dictSrc[91].vctPos.z + dictSrc[92].vctPos.z + dictSrc[93].vctPos.z);
				value.trfBone.SetLocalRotation(dictSrc[91].vctRot.x + dictSrc[92].vctRot.x + dictSrc[92].vctRot.z + num83, dictSrc[91].vctRot.y, dictSrc[91].vctRot.z);
			}
			if (dictDst.TryGetValue(79, out value))
			{
				value.trfBone.SetLocalPositionX(dictSrc[95].vctPos.x + dictSrc[96].vctPos.x - dictSrc[93].vctPos.x * 0.5f);
				value.trfBone.SetLocalPositionZ(dictSrc[95].vctPos.z + dictSrc[96].vctPos.z + dictSrc[93].vctPos.z);
				value.trfBone.SetLocalRotation(dictSrc[95].vctRot.x + dictSrc[96].vctRot.x + dictSrc[96].vctRot.z + num83 * 0.6f + num84 * 0.6f, dictSrc[95].vctRot.y, dictSrc[95].vctRot.z);
			}
			if (dictDst.TryGetValue(80, out value))
			{
				value.trfBone.SetLocalPositionX(dictSrc[98].vctPos.x + dictSrc[99].vctPos.x + dictSrc[100].vctPos.x * 0.5f - dictSrc[93].vctPos.x);
				value.trfBone.SetLocalPositionZ(dictSrc[98].vctPos.z + dictSrc[99].vctPos.z);
				value.trfBone.SetLocalRotation(dictSrc[98].vctRot.x + dictSrc[99].vctRot.x + dictSrc[99].vctRot.z + num84, dictSrc[98].vctRot.y, dictSrc[98].vctRot.z);
			}
			if (dictDst.TryGetValue(81, out value))
			{
				value.trfBone.SetLocalPositionX(dictSrc[101].vctPos.x + dictSrc[102].vctPos.x + dictSrc[100].vctPos.x);
				value.trfBone.SetLocalPositionZ(dictSrc[101].vctPos.z + dictSrc[102].vctPos.z + dictSrc[100].vctPos.z);
				value.trfBone.SetLocalRotation(dictSrc[101].vctRot.x + dictSrc[102].vctRot.x + dictSrc[102].vctRot.z + num83 * 0.6f + num84 * 0.6f + dictSrc[100].vctRot.x, dictSrc[101].vctRot.y, dictSrc[101].vctRot.z);
			}
			if (dictDst.TryGetValue(82, out value))
			{
				value.trfBone.SetLocalPositionX(dictSrc[103].vctPos.x + dictSrc[104].vctPos.x);
				value.trfBone.SetLocalPositionZ(dictSrc[103].vctPos.z + dictSrc[104].vctPos.z + dictSrc[100].vctPos.z);
				value.trfBone.SetLocalRotation(dictSrc[103].vctRot.x + dictSrc[104].vctRot.x + dictSrc[104].vctRot.z + num83 + dictSrc[100].vctRot.x, dictSrc[103].vctRot.y, dictSrc[103].vctRot.z);
			}
			if (dictDst.TryGetValue(83, out value))
			{
				value.trfBone.SetLocalPositionX(dictSrc[105].vctPos.x + dictSrc[106].vctPos.x - dictSrc[100].vctPos.x);
				value.trfBone.SetLocalPositionZ(dictSrc[105].vctPos.z + dictSrc[106].vctPos.z + dictSrc[100].vctPos.z);
				value.trfBone.SetLocalRotation(dictSrc[105].vctRot.x + dictSrc[106].vctRot.x + dictSrc[106].vctRot.z + num83 * 0.6f + num84 * 0.6f + dictSrc[100].vctRot.x, dictSrc[105].vctRot.y, dictSrc[105].vctRot.z);
			}
			if (dictDst.TryGetValue(84, out value))
			{
				value.trfBone.SetLocalPositionX(dictSrc[107].vctPos.x + dictSrc[108].vctPos.x - dictSrc[100].vctPos.x * 0.5f + dictSrc[93].vctPos.x);
				value.trfBone.SetLocalPositionZ(dictSrc[107].vctPos.z + dictSrc[108].vctPos.z);
				value.trfBone.SetLocalRotation(dictSrc[107].vctRot.x + dictSrc[108].vctRot.x + dictSrc[108].vctRot.z + num84, dictSrc[107].vctRot.y, dictSrc[107].vctRot.z);
			}
			if (dictDst.TryGetValue(85, out value))
			{
				value.trfBone.SetLocalPositionX(dictSrc[109].vctPos.x + dictSrc[110].vctPos.x + dictSrc[93].vctPos.x * 0.5f);
				value.trfBone.SetLocalPositionZ(dictSrc[109].vctPos.z + dictSrc[110].vctPos.z + dictSrc[93].vctPos.z);
				value.trfBone.SetLocalRotation(dictSrc[109].vctRot.x + dictSrc[110].vctRot.x + dictSrc[110].vctRot.z + num83 * 0.6f + num84 * 0.6f, dictSrc[109].vctRot.y, dictSrc[109].vctRot.z);
			}
		}
		if ((updateMask & 1) != 0)
		{
			if (dictDst.TryGetValue(39, out value))
			{
				value.trfBone.SetLocalPositionY(dictSrc[40].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[41].vctPos.z + dictSrc[42].vctPos.z + dictSrc[43].vctPos.z + dictSrc[40].vctPos.z + dictSrc[44].vctPos.z);
				value.trfBone.SetLocalRotation(dictSrc[44].vctRot.x, 0f, 0f);
			}
			if (dictDst.TryGetValue(40, out value))
			{
				value.trfBone.SetLocalPositionX(dictSrc[46].vctPos.x);
				value.trfBone.SetLocalPositionY(dictSrc[45].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[45].vctPos.z);
				value.trfBone.SetLocalRotation(dictSrc[45].vctRot.x, dictSrc[43].vctRot.y + dictSrc[45].vctRot.y, 0f);
				value.trfBone.SetLocalScale(dictSrc[45].vctScl.x, dictSrc[45].vctScl.y, dictSrc[45].vctScl.z);
			}
			if (dictDst.TryGetValue(41, out value))
			{
				value.trfBone.SetLocalPositionY(dictSrc[47].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[47].vctPos.z + dictSrc[48].vctPos.z);
				value.trfBone.SetLocalRotation(dictSrc[47].vctRot.x, 0f, 0f);
				value.trfBone.SetLocalScale(dictSrc[47].vctScl.x, dictSrc[47].vctScl.y, dictSrc[47].vctScl.z);
			}
			if (dictDst.TryGetValue(42, out value))
			{
				value.trfBone.SetLocalPositionY(dictSrc[49].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[49].vctPos.z + dictSrc[48].vctPos.z / 2f);
				value.trfBone.SetLocalRotation(dictSrc[49].vctRot.x, 0f, 0f);
				value.trfBone.SetLocalScale(dictSrc[49].vctScl.x, dictSrc[49].vctScl.y, dictSrc[49].vctScl.z);
			}
			if (dictDst.TryGetValue(43, out value))
			{
				value.trfBone.SetLocalPositionX(dictSrc[50].vctPos.x);
				value.trfBone.SetLocalPositionY(dictSrc[50].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[50].vctPos.z);
				value.trfBone.SetLocalRotation(dictSrc[50].vctRot.x, dictSrc[50].vctRot.y, 0f);
				value.trfBone.SetLocalScale(dictSrc[50].vctScl.x, dictSrc[50].vctScl.y, dictSrc[48].vctScl.z);
			}
			if (dictDst.TryGetValue(44, out value))
			{
				value.trfBone.SetLocalPositionX(dictSrc[51].vctPos.x);
				value.trfBone.SetLocalPositionY(dictSrc[51].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[51].vctPos.z);
				value.trfBone.SetLocalRotation(dictSrc[51].vctRot.x, dictSrc[51].vctRot.y, 0f);
				value.trfBone.SetLocalScale(dictSrc[51].vctScl.x * dictSrc[48].vctScl.x, dictSrc[51].vctScl.y * dictSrc[48].vctScl.y, dictSrc[51].vctScl.z);
			}
			if (dictDst.TryGetValue(45, out value))
			{
				value.trfBone.SetLocalPositionY(dictSrc[52].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[52].vctPos.z);
				value.trfBone.SetLocalRotation(dictSrc[52].vctRot.x, 0f, 0f);
				value.trfBone.SetLocalScale(dictSrc[52].vctScl.x * dictSrc[48].vctScl.x, dictSrc[52].vctScl.y * dictSrc[48].vctScl.y, 1f);
			}
			if (dictDst.TryGetValue(46, out value))
			{
				value.trfBone.SetLocalPositionX(dictSrc[53].vctPos.x);
				value.trfBone.SetLocalPositionY(dictSrc[53].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[53].vctPos.z + dictSrc[54].vctPos.z);
				value.trfBone.SetLocalScale(dictSrc[53].vctScl.x * dictSrc[54].vctScl.x, dictSrc[53].vctScl.y * dictSrc[54].vctScl.x, dictSrc[53].vctScl.z * dictSrc[54].vctScl.x);
			}
			if (dictDst.TryGetValue(47, out value))
			{
				value.trfBone.SetLocalPositionZ(dictSrc[55].vctPos.z + dictSrc[56].vctPos.z);
				value.trfBone.SetLocalScale(dictSrc[57].vctScl.x, dictSrc[57].vctScl.x, dictSrc[57].vctScl.z);
			}
			if (dictDst.TryGetValue(48, out value))
			{
				value.trfBone.SetLocalPositionZ((0f - (dictSrc[55].vctPos.z - 0.01f)) * 1.2f);
				value.trfBone.SetLocalScale(dictSrc[55].vctScl.x * dictSrc[56].vctScl.x, dictSrc[55].vctScl.y * dictSrc[56].vctScl.y, dictSrc[55].vctScl.z * dictSrc[56].vctScl.z);
			}
			if (dictDst.TryGetValue(49, out value))
			{
				value.trfBone.SetLocalScale(1f / dictSrc[55].vctScl.x * (dictSrc[58].vctScl.x * 1.2f), 1f / dictSrc[55].vctScl.y * (dictSrc[58].vctScl.y * 1.2f), 1f / dictSrc[55].vctScl.z);
			}
			if (dictDst.TryGetValue(50, out value))
			{
				value.trfBone.SetLocalPositionZ(0.0025f + dictSrc[59].vctPos.z - (dictSrc[55].vctPos.z - 0.01f) * 1f);
				value.trfBone.SetLocalScale(0.1f + dictSrc[59].vctScl.y * dictSrc[59].vctScl.x, 0.1f + dictSrc[59].vctScl.y * dictSrc[59].vctScl.x, 0.1f + dictSrc[59].vctScl.z * dictSrc[59].vctScl.x);
			}
			if (dictDst.TryGetValue(51, out value))
			{
				value.trfBone.SetLocalPositionZ(0.004f + dictSrc[60].vctPos.z + dictSrc[55].vctPos.x + dictSrc[58].vctPos.z);
				value.trfBone.SetLocalScale(dictSrc[60].vctScl.x * dictSrc[58].vctScl.x, dictSrc[60].vctScl.y * dictSrc[58].vctScl.y, dictSrc[60].vctScl.z * dictSrc[58].vctScl.z);
			}
			if (dictDst.TryGetValue(52, out value))
			{
				value.trfBone.SetLocalPositionZ(dictSrc[61].vctPos.z);
				value.trfBone.SetLocalScale(1f * dictSrc[61].vctScl.x / dictSrc[60].vctScl.x / dictSrc[58].vctScl.x * dictSrc[62].vctScl.x, 1f * dictSrc[61].vctScl.y / dictSrc[60].vctScl.y / dictSrc[58].vctScl.y * dictSrc[62].vctScl.y, 1f * dictSrc[61].vctScl.z / dictSrc[60].vctScl.z / dictSrc[58].vctScl.z * dictSrc[62].vctScl.z);
			}
		}
		if ((updateMask & 2) != 0)
		{
			if (dictDst.TryGetValue(53, out value))
			{
				value.trfBone.SetLocalPositionY(dictSrc[40].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[41].vctPos.z + dictSrc[42].vctPos.z + dictSrc[43].vctPos.z + dictSrc[40].vctPos.z + dictSrc[44].vctPos.z);
				value.trfBone.SetLocalRotation(dictSrc[44].vctRot.x, 0f, 0f);
			}
			if (dictDst.TryGetValue(54, out value))
			{
				value.trfBone.SetLocalPositionX(dictSrc[64].vctPos.x);
				value.trfBone.SetLocalPositionY(dictSrc[45].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[45].vctPos.z);
				value.trfBone.SetLocalRotation(dictSrc[45].vctRot.x, dictSrc[63].vctRot.y - dictSrc[45].vctRot.y, 0f);
				value.trfBone.SetLocalScale(dictSrc[45].vctScl.x, dictSrc[45].vctScl.y, dictSrc[45].vctScl.z);
			}
			if (dictDst.TryGetValue(55, out value))
			{
				value.trfBone.SetLocalPositionY(dictSrc[47].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[47].vctPos.z + dictSrc[48].vctPos.z);
				value.trfBone.SetLocalRotation(dictSrc[47].vctRot.x, 0f, 0f);
				value.trfBone.SetLocalScale(dictSrc[47].vctScl.x, dictSrc[47].vctScl.y, dictSrc[47].vctScl.z);
			}
			if (dictDst.TryGetValue(56, out value))
			{
				value.trfBone.SetLocalPositionY(dictSrc[49].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[49].vctPos.z + dictSrc[48].vctPos.z / 2f);
				value.trfBone.SetLocalRotation(dictSrc[49].vctRot.x, 0f, 0f);
				value.trfBone.SetLocalScale(dictSrc[49].vctScl.x, dictSrc[49].vctScl.y, dictSrc[49].vctScl.z);
			}
			if (dictDst.TryGetValue(57, out value))
			{
				value.trfBone.SetLocalPositionX(0f - dictSrc[50].vctPos.x);
				value.trfBone.SetLocalPositionY(dictSrc[50].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[50].vctPos.z);
				value.trfBone.SetLocalRotation(dictSrc[50].vctRot.x, 0f - dictSrc[50].vctRot.y, 0f);
				value.trfBone.SetLocalScale(dictSrc[50].vctScl.x, dictSrc[50].vctScl.y, dictSrc[48].vctScl.z);
			}
			if (dictDst.TryGetValue(58, out value))
			{
				value.trfBone.SetLocalPositionX(0f - dictSrc[51].vctPos.x);
				value.trfBone.SetLocalPositionY(dictSrc[51].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[51].vctPos.z);
				value.trfBone.SetLocalRotation(dictSrc[51].vctRot.x, 0f - dictSrc[51].vctRot.y, 0f);
				value.trfBone.SetLocalScale(dictSrc[51].vctScl.x * dictSrc[48].vctScl.x, dictSrc[51].vctScl.y * dictSrc[48].vctScl.y, dictSrc[51].vctScl.z);
			}
			if (dictDst.TryGetValue(59, out value))
			{
				value.trfBone.SetLocalPositionY(dictSrc[52].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[52].vctPos.z);
				value.trfBone.SetLocalRotation(dictSrc[52].vctRot.x, 0f, 0f);
				value.trfBone.SetLocalScale(dictSrc[52].vctScl.x * dictSrc[48].vctScl.x, dictSrc[52].vctScl.y * dictSrc[48].vctScl.y, 1f);
			}
			if (dictDst.TryGetValue(60, out value))
			{
				value.trfBone.SetLocalPositionX(0f - dictSrc[53].vctPos.x);
				value.trfBone.SetLocalPositionY(dictSrc[53].vctPos.y);
				value.trfBone.SetLocalPositionZ(dictSrc[53].vctPos.z + dictSrc[54].vctPos.z);
				value.trfBone.SetLocalScale(dictSrc[53].vctScl.x * dictSrc[54].vctScl.x, dictSrc[53].vctScl.y * dictSrc[54].vctScl.x, dictSrc[53].vctScl.z * dictSrc[54].vctScl.x);
			}
			if (dictDst.TryGetValue(61, out value))
			{
				value.trfBone.SetLocalPositionZ(dictSrc[55].vctPos.z + dictSrc[56].vctPos.z);
				value.trfBone.SetLocalScale(dictSrc[57].vctScl.x, dictSrc[57].vctScl.x, dictSrc[57].vctScl.z);
			}
			if (dictDst.TryGetValue(62, out value))
			{
				value.trfBone.SetLocalPositionZ((0f - (dictSrc[55].vctPos.z - 0.01f)) * 1.2f);
				value.trfBone.SetLocalScale(dictSrc[55].vctScl.x * dictSrc[56].vctScl.x, dictSrc[55].vctScl.y * dictSrc[56].vctScl.y, dictSrc[55].vctScl.z * dictSrc[56].vctScl.z);
			}
			if (dictDst.TryGetValue(63, out value))
			{
				value.trfBone.SetLocalScale(1f / dictSrc[55].vctScl.x * (dictSrc[58].vctScl.x * 1.2f), 1f / dictSrc[55].vctScl.y * (dictSrc[58].vctScl.y * 1.2f), 1f / dictSrc[55].vctScl.z);
			}
			if (dictDst.TryGetValue(64, out value))
			{
				value.trfBone.SetLocalPositionZ(0.0025f + dictSrc[59].vctPos.z - (dictSrc[55].vctPos.z - 0.01f) * 1f);
				value.trfBone.SetLocalScale(0.1f + dictSrc[59].vctScl.y * dictSrc[59].vctScl.x, 0.1f + dictSrc[59].vctScl.y * dictSrc[59].vctScl.x, 0.1f + dictSrc[59].vctScl.z * dictSrc[59].vctScl.x);
			}
			if (dictDst.TryGetValue(65, out value))
			{
				value.trfBone.SetLocalPositionZ(0.004f + dictSrc[60].vctPos.z + dictSrc[55].vctPos.x + dictSrc[58].vctPos.z);
				value.trfBone.SetLocalScale(dictSrc[60].vctScl.x * dictSrc[58].vctScl.x, dictSrc[60].vctScl.y * dictSrc[58].vctScl.y, dictSrc[60].vctScl.z * dictSrc[58].vctScl.z);
			}
			if (dictDst.TryGetValue(66, out value))
			{
				value.trfBone.SetLocalPositionZ(dictSrc[61].vctPos.z);
				value.trfBone.SetLocalScale(1f * dictSrc[61].vctScl.x / dictSrc[60].vctScl.x / dictSrc[58].vctScl.x * dictSrc[62].vctScl.x, 1f * dictSrc[61].vctScl.y / dictSrc[60].vctScl.y / dictSrc[58].vctScl.y * dictSrc[62].vctScl.y, 1f * dictSrc[61].vctScl.z / dictSrc[60].vctScl.z / dictSrc[58].vctScl.z * dictSrc[62].vctScl.z);
			}
		}
	}

	public override void UpdateAlways()
	{
		if (!base.InitEnd || dictSrc.Count == 0)
		{
			return;
		}
		BoneInfo value = null;
		if (dictDst.TryGetValue(38, out value))
		{
			value.trfBone.SetLocalPositionZ(dictSrc[39].vctPos.z);
		}
		if (fixCorrectBone != null && 2 == fixCorrectBone.Length)
		{
			if ((bool)fixCorrectBone[0])
			{
				float x = ((base.typeBone == 0) ? (-0.01563369f) : correctValue[30].vctPos.x);
				fixCorrectBone[0].SetLocalPositionX(x);
			}
			if ((bool)fixCorrectBone[1])
			{
				float x2 = ((base.typeBone == 0) ? 0.01560147f : correctValue[31].vctPos.x);
				fixCorrectBone[1].SetLocalPositionX(x2);
			}
		}
	}
}
