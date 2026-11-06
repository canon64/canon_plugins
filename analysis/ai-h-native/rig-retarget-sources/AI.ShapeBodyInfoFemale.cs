using System;
using System.Collections.Generic;
using IllusionUtility.SetUtility;
using Manager;
using UnityEngine;

public class ShapeBodyInfoFemale : ShapeInfoBase
{
	public enum DstName
	{
		cf_J_ArmElbo_low_s_L,
		cf_J_ArmElbo_low_s_R,
		cf_J_ArmLow01_s_L,
		cf_J_ArmLow01_s_R,
		cf_J_ArmLow02_s_L,
		cf_J_ArmLow02_s_R,
		cf_J_ArmUp01_s_L,
		cf_J_ArmUp01_s_R,
		cf_J_ArmUp02_s_L,
		cf_J_ArmUp02_s_R,
		cf_J_ArmUp03_s_L,
		cf_J_ArmUp03_s_R,
		cf_J_Hand_s_L,
		cf_J_Hand_s_R,
		cf_J_Hand_Wrist_s_L,
		cf_J_Hand_Wrist_s_R,
		cf_J_Head_s,
		cf_J_Kosi01_s,
		cf_J_Kosi02_s,
		cf_J_LegKnee_back_s_L,
		cf_J_LegKnee_back_s_R,
		cf_J_LegKnee_low_s_L,
		cf_J_LegKnee_low_s_R,
		cf_J_LegLow01_s_L,
		cf_J_LegLow01_s_R,
		cf_J_LegLow02_s_L,
		cf_J_LegLow02_s_R,
		cf_J_LegLow03_s_L,
		cf_J_LegLow03_s_R,
		cf_J_LegUp01_s_L,
		cf_J_LegUp01_s_R,
		cf_J_LegUp02_s_L,
		cf_J_LegUp02_s_R,
		cf_J_LegUp03_s_L,
		cf_J_LegUp03_s_R,
		cf_J_LegUpDam_s_L,
		cf_J_LegUpDam_s_R,
		cf_J_Mune_Nip01_s_L,
		cf_J_Mune_Nip01_s_R,
		cf_J_Mune_Nip02_s_L,
		cf_J_Mune_Nip02_s_R,
		cf_J_Mune_Nipacs01_L,
		cf_J_Mune_Nipacs01_R,
		cf_J_Mune00_d_L,
		cf_J_Mune00_d_R,
		cf_J_Mune00_s_L,
		cf_J_Mune00_s_R,
		cf_J_Mune00_t_L,
		cf_J_Mune00_t_R,
		cf_J_Mune01_s_L,
		cf_J_Mune01_s_R,
		cf_J_Mune01_t_L,
		cf_J_Mune01_t_R,
		cf_J_Mune02_s_L,
		cf_J_Mune02_s_R,
		cf_J_Mune02_t_L,
		cf_J_Mune02_t_R,
		cf_J_Mune03_s_L,
		cf_J_Mune03_s_R,
		cf_J_Mune04_s_L,
		cf_J_Mune04_s_R,
		cf_J_Neck_s,
		cf_J_Shoulder02_s_L,
		cf_J_Shoulder02_s_R,
		cf_J_Siri_s_L,
		cf_J_Siri_s_R,
		cf_J_sk_siri_dam,
		cf_J_sk_top,
		cf_J_Spine01_s,
		cf_J_Spine02_s,
		cf_J_Spine03_s,
		cf_N_height,
		cf_J_sk_00_00_dam,
		cf_J_sk_01_00_dam,
		cf_J_sk_02_00_dam,
		cf_J_sk_03_00_dam,
		cf_J_sk_04_00_dam,
		cf_J_sk_05_00_dam,
		cf_J_sk_06_00_dam,
		cf_J_sk_07_00_dam,
		cf_hit_Mune02_s_L,
		cf_hit_Mune02_s_R,
		cf_hit_Kosi02_s,
		cf_hit_LegUp01_s_L,
		cf_hit_LegUp01_s_R,
		cf_hit_Siri_s_L,
		cf_hit_Siri_s_R,
		cf_J_Legsk_root_L,
		cf_J_Legsk_root_R
	}

	public enum SrcName
	{
		cf_s_ArmElbo_low_s_L,
		cf_s_ArmElbo_low_s_R,
		cf_s_ArmElbo_up_s_L,
		cf_s_ArmElbo_up_s_R,
		cf_s_ArmLow01_h_L,
		cf_s_ArmLow01_h_R,
		cf_s_ArmLow01_s_L,
		cf_s_ArmLow01_s_R,
		cf_s_ArmLow02_h_L,
		cf_s_ArmLow02_h_R,
		cf_s_ArmLow02_s_L,
		cf_s_ArmLow02_s_R,
		cf_s_ArmUp01_h_L,
		cf_s_ArmUp01_h_R,
		cf_s_ArmUp01_s_L,
		cf_s_ArmUp01_s_R,
		cf_s_ArmUp01_s_tx_L,
		cf_s_ArmUp01_s_tx_R,
		cf_s_ArmUp02_h_L,
		cf_s_ArmUp02_h_R,
		cf_s_ArmUp02_s_L,
		cf_s_ArmUp02_s_R,
		cf_s_ArmUp03_h_L,
		cf_s_ArmUp03_h_R,
		cf_s_ArmUp03_s_L,
		cf_s_ArmUp03_s_R,
		cf_s_Hand_h_L,
		cf_s_Hand_h_R,
		cf_s_Hand_Wrist_s_L,
		cf_s_Hand_Wrist_s_R,
		cf_s_Head_h,
		cf_s_Head_s,
		cf_s_height,
		cf_s_Kosi01_h,
		cf_s_Kosi01_s,
		cf_s_Kosi01_s_sz,
		cf_s_Kosi02_h,
		cf_s_Kosi02_s,
		cf_s_Kosi02_s_sz,
		cf_s_LegKnee_back_s_L,
		cf_s_LegKnee_back_s_R,
		cf_s_LegKnee_h_L,
		cf_s_LegKnee_h_R,
		cf_s_LegKnee_low_s_L,
		cf_s_LegKnee_low_s_R,
		cf_s_LegKnee_up_s_L,
		cf_s_LegKnee_up_s_R,
		cf_s_LegLow01_h_L,
		cf_s_LegLow01_h_R,
		cf_s_LegLow01_s_L,
		cf_s_LegLow01_s_R,
		cf_s_LegLow02_h_L,
		cf_s_LegLow02_h_R,
		cf_s_LegLow02_s_L,
		cf_s_LegLow02_s_R,
		cf_s_LegLow03_s_L,
		cf_s_LegLow03_s_R,
		cf_s_LegUp01_blend_s_L,
		cf_s_LegUp01_blend_s_R,
		cf_s_LegUp01_blend_ss_L,
		cf_s_LegUp01_blend_ss_R,
		cf_s_LegUp01_h_L,
		cf_s_LegUp01_h_R,
		cf_s_LegUp01_s_L,
		cf_s_LegUp01_s_R,
		cf_s_LegUp02_blend_s_L,
		cf_s_LegUp02_blend_s_R,
		cf_s_LegUp02_h_L,
		cf_s_LegUp02_h_R,
		cf_s_LegUp02_s_L,
		cf_s_LegUp02_s_R,
		cf_s_LegUp03_blend_s_L,
		cf_s_LegUp03_blend_s_R,
		cf_s_LegUp03_h_L,
		cf_s_LegUp03_h_R,
		cf_s_LegUp03_s_L,
		cf_s_LegUp03_s_R,
		cf_s_LegUpDam_s_L,
		cf_s_LegUpDam_s_R,
		cf_s_Mune_Nip_dam_L,
		cf_s_Mune_Nip_dam_R,
		cf_s_Mune_Nip01_s_L,
		cf_s_Mune_Nip01_s_R,
		cf_s_Mune_Nip01_ss_L,
		cf_s_Mune_Nip01_ss_R,
		cf_s_Mune_Nip02_s_L,
		cf_s_Mune_Nip02_s_R,
		cf_s_Mune_Nipacs01_L,
		cf_s_Mune_Nipacs01_R,
		cf_s_Mune_Nipacs02_L,
		cf_s_Mune_Nipacs02_R,
		cf_s_Mune00_h_L,
		cf_s_Mune00_h_R,
		cf_s_Mune00_s_L,
		cf_s_Mune00_s_R,
		cf_s_Mune00_ss_02_L,
		cf_s_Mune00_ss_02_R,
		cf_s_Mune00_ss_02sz_L,
		cf_s_Mune00_ss_02sz_R,
		cf_s_Mune00_ss_03_L,
		cf_s_Mune00_ss_03_R,
		cf_s_Mune00_ss_03sz_L,
		cf_s_Mune00_ss_03sz_R,
		cf_s_Mune00_ss_ty_L,
		cf_s_Mune00_ss_ty_R,
		cf_s_Mune01_s_L,
		cf_s_Mune01_s_R,
		cf_s_Mune01_s_rx_L,
		cf_s_Mune01_s_rx_R,
		cf_s_Mune01_s_ry_L,
		cf_s_Mune01_s_ry_R,
		cf_s_Mune01_s_tx_L,
		cf_s_Mune01_s_tx_R,
		cf_s_Mune01_s_tz_L,
		cf_s_Mune01_s_tz_R,
		cf_s_Mune02_s_L,
		cf_s_Mune02_s_R,
		cf_s_Mune02_s_rx_L,
		cf_s_Mune02_s_rx_R,
		cf_s_Mune02_s_tz_L,
		cf_s_Mune02_s_tz_R,
		cf_s_Mune03_s_L,
		cf_s_Mune03_s_R,
		cf_s_Mune03_s_rx_L,
		cf_s_Mune03_s_rx_R,
		cf_s_Mune04_s_L,
		cf_s_Mune04_s_R,
		cf_s_Neck_h,
		cf_s_Neck_s,
		cf_s_Neck_s_sz,
		cf_s_Shoulder_h_L,
		cf_s_Shoulder_h_R,
		cf_s_Shoulder02_h_L,
		cf_s_Shoulder02_h_R,
		cf_s_Shoulder02_s_L,
		cf_s_Shoulder02_s_R,
		cf_s_Shoulder02_s_tx_L,
		cf_s_Shoulder02_s_tx_R,
		cf_s_Siri_kosi01_s_L,
		cf_s_Siri_kosi01_s_R,
		cf_s_Siri_kosi02_s_L,
		cf_s_Siri_kosi02_s_R,
		cf_s_Siri_legup01_s_L,
		cf_s_Siri_legup01_s_R,
		cf_s_Siri_s_L,
		cf_s_Siri_s_R,
		cf_s_Siri_s_ty_L,
		cf_s_Siri_s_ty_R,
		cf_s_sk_siri_dam,
		cf_s_sk_siri_ty_dam,
		cf_s_sk_top_h,
		cf_s_Spine01_h,
		cf_s_Spine01_s,
		cf_s_Spine01_s_sz,
		cf_s_Spine01_s_ty,
		cf_s_Spine02_h,
		cf_s_Spine02_s,
		cf_s_Spine02_s_sz,
		cf_s_Spine03_h,
		cf_s_Spine03_s,
		cf_s_Spine03_s_sz,
		cf_s_sk_00_sx01,
		cf_s_sk_00_sx02,
		cf_s_sk_00_sz01,
		cf_s_sk_00_sz02,
		cf_s_sk_01_sx01,
		cf_s_sk_01_sx02,
		cf_s_sk_01_sz01,
		cf_s_sk_01_sz02,
		cf_s_sk_02_sx01,
		cf_s_sk_02_sx02,
		cf_s_sk_02_sz01,
		cf_s_sk_02_sz02,
		cf_s_sk_03_sx01,
		cf_s_sk_03_sx02,
		cf_s_sk_03_sz01,
		cf_s_sk_03_sz02,
		cf_s_sk_04_sx01,
		cf_s_sk_04_sx02,
		cf_s_sk_04_sz01,
		cf_s_sk_04_sz02,
		cf_s_sk_05_sx01,
		cf_s_sk_05_sx02,
		cf_s_sk_05_sz01,
		cf_s_sk_05_sz02,
		cf_s_sk_06_sx01,
		cf_s_sk_06_sx02,
		cf_s_sk_06_sz01,
		cf_s_sk_06_sz02,
		cf_s_sk_07_sx01,
		cf_s_sk_07_sx02,
		cf_s_sk_07_sz01,
		cf_s_sk_07_sz02,
		cf_hit_Kosi02_Kosi01sx_a,
		cf_hit_Kosi02_Kosi01sz_a,
		cf_hit_Kosi02_Kosi02sx_a,
		cf_hit_Kosi02_Kosi02sz_a,
		cf_hit_LegUp01_Kosi02sz_a,
		cf_hit_LegUp01_Kosi02sx_a,
		cf_hit_Siri_Kosi02sz_a,
		cf_hit_Siri_Kosi02sx_a,
		cf_hit_Siri_size_a,
		cf_hit_Siri_rot_a,
		cf_hit_Mune_size_a,
		cf_hit_Siri_LegUp01,
		cf_hit_height,
		cf_s_legskroot_kosi02_sx_L,
		cf_s_legskroot_kosi02_sx_R,
		cf_s_legskroot_kosi02_sz,
		cf_s_legskroot_leg01_L,
		cf_s_legskroot_leg01_R
	}

	public const int UPDATE_MASK_BUST_L = 1;

	public const int UPDATE_MASK_BUST_R = 2;

	public const int UPDATE_MASK_NIP_L = 4;

	public const int UPDATE_MASK_NIP_R = 8;

	public const int UPDATE_MASK_ETC = 16;

	public const int UPDATE_MASK_ALL = 31;

	public int updateMask = 31;

	public override void InitShapeInfo(string manifest, string assetBundleAnmKey, string assetBundleCategory, string anmKeyInfoName, string cateInfoName, Transform trfObj)
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		DstName[] array = (DstName[])Enum.GetValues(typeof(DstName));
		DstName[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			DstName value = array2[i];
			dictionary[value.ToString()] = (int)value;
		}
		Dictionary<string, int> dictionary2 = new Dictionary<string, int>();
		SrcName[] array3 = (SrcName[])Enum.GetValues(typeof(SrcName));
		SrcName[] array4 = array3;
		for (int j = 0; j < array4.Length; j++)
		{
			SrcName value2 = array4[j];
			dictionary2[value2.ToString()] = (int)value2;
		}
		InitShapeInfoBase(manifest, assetBundleAnmKey, assetBundleCategory, anmKeyInfoName, cateInfoName, trfObj, dictionary, dictionary2, Singleton<Character>.Instance.AddLoadAssetBundle);
		base.InitEnd = true;
	}

	public override void ForceUpdate()
	{
		Update();
	}

	public override void Update()
	{
		if (base.InitEnd && dictSrc.Count != 0)
		{
			float num = dictSrc[193].vctPos.y + dictSrc[194].vctPos.y;
			float num2 = dictSrc[193].vctScl.y * dictSrc[194].vctScl.y;
			float x = dictSrc[194].vctScl.x;
			float z = dictSrc[194].vctPos.z;
			float num3 = dictSrc[195].vctPos.y + dictSrc[196].vctPos.y;
			float num4 = dictSrc[195].vctPos.z + dictSrc[196].vctPos.z;
			float num5 = dictSrc[195].vctScl.y * dictSrc[196].vctScl.y;
			float num6 = dictSrc[195].vctScl.x * dictSrc[196].vctScl.x;
			float num7 = dictSrc[197].vctPos.x + dictSrc[198].vctPos.x;
			float num8 = dictSrc[197].vctRot.z + dictSrc[198].vctRot.z;
			float x2 = dictSrc[197].vctScl.x * dictSrc[198].vctScl.x;
			float y = dictSrc[197].vctScl.y * dictSrc[198].vctScl.y;
			float num9 = dictSrc[199].vctScl.x * dictSrc[200].vctScl.x;
			float num10 = dictSrc[199].vctScl.y * dictSrc[200].vctScl.y;
			float x3 = dictSrc[200].vctPos.x;
			float num11 = dictSrc[199].vctPos.z + dictSrc[200].vctPos.z;
			float x4 = dictSrc[201].vctPos.x;
			float y2 = dictSrc[201].vctPos.y;
			float z2 = dictSrc[201].vctPos.z;
			float x5 = dictSrc[201].vctScl.x;
			float y3 = dictSrc[201].vctScl.y;
			float x6 = dictSrc[202].vctPos.x;
			float y4 = dictSrc[202].vctPos.y;
			float z3 = dictSrc[202].vctPos.z;
			float x7 = dictSrc[202].vctScl.x;
			float x8 = dictSrc[202].vctRot.x;
			float z4 = dictSrc[204].vctPos.z;
			float x9 = dictSrc[204].vctScl.x;
			if ((updateMask & 0x10) != 0)
			{
				dictDst[71].trfBone.SetLocalScale(dictSrc[32].vctScl.x, dictSrc[32].vctScl.y, dictSrc[32].vctScl.z);
				dictDst[67].trfBone.SetLocalScale(dictSrc[150].vctScl.x, 1f, dictSrc[150].vctScl.z);
				dictDst[72].trfBone.SetLocalPositionZ(dictSrc[161].vctPos.z + dictSrc[162].vctPos.z + dictSrc[163].vctPos.z + dictSrc[164].vctPos.z);
				dictDst[72].trfBone.SetLocalRotation(dictSrc[163].vctRot.x + dictSrc[164].vctRot.x, 0f, 0f);
				dictDst[73].trfBone.SetLocalPositionX(dictSrc[165].vctPos.x);
				dictDst[73].trfBone.SetLocalPositionZ(dictSrc[165].vctPos.z + dictSrc[166].vctPos.z + dictSrc[167].vctPos.z + dictSrc[168].vctPos.z);
				dictDst[73].trfBone.SetLocalRotation(dictSrc[166].vctRot.x + dictSrc[167].vctRot.x + dictSrc[168].vctRot.x, 0f, 0f);
				dictDst[74].trfBone.SetLocalPositionX(dictSrc[169].vctPos.x + dictSrc[170].vctPos.x);
				dictDst[74].trfBone.SetLocalPositionZ(dictSrc[169].vctPos.z + dictSrc[170].vctPos.z + dictSrc[171].vctPos.z + dictSrc[172].vctPos.z);
				dictDst[74].trfBone.SetLocalRotation(dictSrc[169].vctRot.x + dictSrc[170].vctRot.x, 0f, 0f);
				dictDst[75].trfBone.SetLocalPositionX(dictSrc[173].vctPos.x);
				dictDst[75].trfBone.SetLocalPositionZ(dictSrc[173].vctPos.z + dictSrc[174].vctPos.z + dictSrc[175].vctPos.z + dictSrc[176].vctPos.z);
				dictDst[75].trfBone.SetLocalRotation(dictSrc[174].vctRot.x + dictSrc[175].vctRot.x + dictSrc[176].vctRot.x, 0f, 0f);
				dictDst[76].trfBone.SetLocalPositionZ(dictSrc[177].vctPos.z + dictSrc[178].vctPos.z + dictSrc[179].vctPos.z + dictSrc[180].vctPos.z);
				dictDst[76].trfBone.SetLocalRotation(dictSrc[179].vctRot.x + dictSrc[180].vctRot.x, 0f, 0f);
				dictDst[77].trfBone.SetLocalPositionX(dictSrc[181].vctPos.x + dictSrc[182].vctPos.x);
				dictDst[77].trfBone.SetLocalPositionZ(dictSrc[181].vctPos.z + dictSrc[182].vctPos.z + dictSrc[183].vctPos.z + dictSrc[184].vctPos.z);
				dictDst[77].trfBone.SetLocalRotation(dictSrc[182].vctRot.x + dictSrc[183].vctRot.x + dictSrc[184].vctRot.x, 0f, 0f);
				dictDst[78].trfBone.SetLocalPositionX(dictSrc[185].vctPos.x + dictSrc[186].vctPos.x);
				dictDst[78].trfBone.SetLocalPositionZ(dictSrc[185].vctPos.z + dictSrc[186].vctPos.z + dictSrc[187].vctPos.z + dictSrc[188].vctPos.z);
				dictDst[78].trfBone.SetLocalRotation(dictSrc[185].vctRot.x + dictSrc[186].vctRot.x, 0f, 0f);
				dictDst[79].trfBone.SetLocalPositionX(dictSrc[189].vctPos.x + dictSrc[190].vctPos.x);
				dictDst[79].trfBone.SetLocalPositionZ(dictSrc[189].vctPos.z + dictSrc[190].vctPos.z + dictSrc[191].vctPos.z + dictSrc[192].vctPos.z);
				dictDst[79].trfBone.SetLocalRotation(dictSrc[190].vctRot.x + dictSrc[191].vctRot.x + dictSrc[192].vctRot.x, 0f, 0f);
				dictDst[66].trfBone.SetLocalPositionZ(dictSrc[149].vctPos.z + dictSrc[148].vctPos.z);
				dictDst[66].trfBone.SetLocalRotation(dictSrc[149].vctRot.x, 0f, 0f);
				dictDst[62].trfBone.SetLocalPositionX(dictSrc[136].vctPos.x);
				dictDst[62].trfBone.SetLocalScale(dictSrc[132].vctScl.x, dictSrc[134].vctScl.y * dictSrc[132].vctScl.z, dictSrc[134].vctScl.z * dictSrc[132].vctScl.y);
				dictDst[63].trfBone.SetLocalPositionX(dictSrc[137].vctPos.x);
				dictDst[63].trfBone.SetLocalScale(dictSrc[133].vctScl.x, dictSrc[135].vctScl.y * dictSrc[133].vctScl.z, dictSrc[135].vctScl.z * dictSrc[133].vctScl.y);
				dictDst[6].trfBone.SetLocalPositionX(dictSrc[16].vctPos.x);
				dictDst[6].trfBone.SetLocalPositionY(dictSrc[14].vctPos.y + dictSrc[16].vctPos.y);
				dictDst[6].trfBone.SetLocalRotation(0f, dictSrc[14].vctRot.y, dictSrc[14].vctRot.z + dictSrc[16].vctRot.z);
				dictDst[6].trfBone.SetLocalScale(1f, dictSrc[14].vctScl.y * dictSrc[12].vctScl.y, dictSrc[14].vctScl.z * dictSrc[12].vctScl.z);
				dictDst[7].trfBone.SetLocalPositionX(dictSrc[17].vctPos.x);
				dictDst[7].trfBone.SetLocalPositionY(dictSrc[15].vctPos.y + dictSrc[17].vctPos.y);
				dictDst[7].trfBone.SetLocalRotation(0f, dictSrc[15].vctRot.y, dictSrc[15].vctRot.z + dictSrc[17].vctRot.z);
				dictDst[7].trfBone.SetLocalScale(1f, dictSrc[15].vctScl.y * dictSrc[13].vctScl.y, dictSrc[15].vctScl.z * dictSrc[13].vctScl.z);
				dictDst[8].trfBone.SetLocalPositionY(dictSrc[20].vctPos.y);
				dictDst[8].trfBone.SetLocalScale(1f, dictSrc[20].vctScl.y * dictSrc[18].vctScl.y, dictSrc[20].vctScl.z * dictSrc[18].vctScl.z);
				dictDst[9].trfBone.SetLocalPositionY(dictSrc[21].vctPos.y);
				dictDst[9].trfBone.SetLocalScale(1f, dictSrc[21].vctScl.y * dictSrc[19].vctScl.y, dictSrc[21].vctScl.z * dictSrc[19].vctScl.z);
				dictDst[10].trfBone.SetLocalScale(1f, dictSrc[24].vctScl.y * dictSrc[22].vctScl.y, dictSrc[24].vctScl.z * dictSrc[22].vctScl.z);
				dictDst[11].trfBone.SetLocalScale(1f, dictSrc[25].vctScl.y * dictSrc[23].vctScl.y, dictSrc[25].vctScl.z * dictSrc[23].vctScl.z);
				dictDst[0].trfBone.SetLocalScale(1f, dictSrc[2].vctScl.y * dictSrc[0].vctScl.y, dictSrc[2].vctScl.z * dictSrc[0].vctScl.z);
				dictDst[1].trfBone.SetLocalScale(1f, dictSrc[3].vctScl.y * dictSrc[1].vctScl.y, dictSrc[3].vctScl.z * dictSrc[1].vctScl.z);
				dictDst[2].trfBone.SetLocalScale(1f, dictSrc[6].vctScl.y * dictSrc[4].vctScl.y, dictSrc[6].vctScl.z * dictSrc[4].vctScl.z);
				dictDst[3].trfBone.SetLocalScale(1f, dictSrc[7].vctScl.y * dictSrc[5].vctScl.y, dictSrc[7].vctScl.z * dictSrc[5].vctScl.z);
				dictDst[4].trfBone.SetLocalScale(1f, dictSrc[10].vctScl.y * dictSrc[8].vctScl.y, dictSrc[10].vctScl.z * dictSrc[8].vctScl.z);
				dictDst[5].trfBone.SetLocalScale(1f, dictSrc[11].vctScl.y * dictSrc[9].vctScl.y, dictSrc[11].vctScl.z * dictSrc[9].vctScl.z);
				dictDst[12].trfBone.SetLocalScale(dictSrc[26].vctScl.x, dictSrc[26].vctScl.y, dictSrc[26].vctScl.z);
				dictDst[13].trfBone.SetLocalScale(dictSrc[27].vctScl.x, dictSrc[27].vctScl.y, dictSrc[27].vctScl.z);
				dictDst[14].trfBone.SetLocalScale(1f, dictSrc[28].vctScl.y, dictSrc[28].vctScl.z);
				dictDst[15].trfBone.SetLocalScale(1f, dictSrc[29].vctScl.y, dictSrc[29].vctScl.z);
				dictDst[17].trfBone.SetLocalScale(dictSrc[33].vctScl.x * dictSrc[34].vctScl.x, 1f, dictSrc[33].vctScl.z * dictSrc[35].vctScl.z);
				dictDst[18].trfBone.SetLocalScale(dictSrc[36].vctScl.x * dictSrc[37].vctScl.x, 1f, dictSrc[36].vctScl.z * dictSrc[38].vctScl.z);
				dictDst[68].trfBone.SetLocalScale(dictSrc[151].vctScl.x * dictSrc[152].vctScl.x, 1f, dictSrc[151].vctScl.z * dictSrc[153].vctScl.z);
				dictDst[68].trfBone.SetLocalPositionY(dictSrc[154].vctPos.y);
				dictDst[68].trfBone.SetLocalPositionZ(dictSrc[152].vctPos.z + dictSrc[153].vctPos.z);
				dictDst[69].trfBone.SetLocalScale(dictSrc[155].vctScl.x * dictSrc[156].vctScl.x, 1f, dictSrc[155].vctScl.z * dictSrc[157].vctScl.z);
				dictDst[70].trfBone.SetLocalScale(dictSrc[158].vctScl.x * dictSrc[159].vctScl.x, 1f, dictSrc[158].vctScl.z * dictSrc[160].vctScl.z);
				dictDst[61].trfBone.SetLocalScale(dictSrc[127].vctScl.x * dictSrc[128].vctScl.x, 1f, dictSrc[127].vctScl.z * dictSrc[129].vctScl.z);
				dictDst[16].trfBone.SetLocalScale(dictSrc[30].vctScl.x * dictSrc[31].vctScl.x, dictSrc[30].vctScl.y * dictSrc[31].vctScl.y, dictSrc[30].vctScl.z * dictSrc[31].vctScl.z);
			}
			if ((updateMask & 1) != 0)
			{
				float x10 = dictSrc[203].vctPos.x;
				float y5 = dictSrc[203].vctPos.y;
				float z5 = dictSrc[203].vctPos.z;
				float x11 = dictSrc[203].vctScl.x;
				dictDst[47].trfBone.SetLocalPositionX(dictSrc[95].vctPos.x + dictSrc[99].vctPos.x);
				dictDst[47].trfBone.SetLocalPositionY(dictSrc[97].vctPos.y + dictSrc[103].vctPos.y);
				dictDst[47].trfBone.SetLocalPositionZ(dictSrc[97].vctPos.z + dictSrc[101].vctPos.z);
				dictDst[47].trfBone.SetLocalRotation(dictSrc[97].vctRot.x + dictSrc[101].vctRot.x + dictSrc[103].vctRot.x + dictSrc[93].vctRot.x, dictSrc[99].vctRot.y + dictSrc[103].vctRot.y, 0f);
				dictDst[47].trfBone.SetLocalScale(dictSrc[91].vctScl.x, dictSrc[91].vctScl.y, dictSrc[91].vctScl.z);
				dictDst[45].trfBone.SetLocalPositionX(dictSrc[93].vctPos.x);
				dictDst[45].trfBone.SetLocalPositionY(dictSrc[93].vctPos.y);
				dictDst[45].trfBone.SetLocalPositionZ(dictSrc[93].vctPos.z);
				dictDst[45].trfBone.SetLocalRotation(0f, 0f, dictSrc[93].vctRot.z);
				dictDst[45].trfBone.SetLocalScale(dictSrc[93].vctScl.x, dictSrc[93].vctScl.y, dictSrc[93].vctScl.z);
				dictDst[43].trfBone.SetLocalPositionX(dictSrc[111].vctPos.x + dictSrc[109].vctPos.x);
				dictDst[43].trfBone.SetLocalPositionZ(0.65f + dictSrc[107].vctPos.z + dictSrc[109].vctPos.z);
				dictDst[43].trfBone.SetLocalRotation(dictSrc[107].vctRot.x, dictSrc[109].vctRot.y, 0f);
				dictDst[49].trfBone.SetLocalPositionY(dictSrc[105].vctPos.y);
				dictDst[49].trfBone.SetLocalPositionZ(dictSrc[105].vctPos.z);
				dictDst[49].trfBone.SetLocalRotation(dictSrc[105].vctRot.x, dictSrc[105].vctRot.y, dictSrc[105].vctRot.z);
				dictDst[49].trfBone.SetLocalScale(dictSrc[105].vctScl.x, dictSrc[105].vctScl.y, dictSrc[105].vctScl.z);
				dictDst[51].trfBone.SetLocalPositionX(dictSrc[105].vctPos.x);
				dictDst[51].trfBone.SetLocalPositionZ(0.3f + dictSrc[113].vctPos.z);
				dictDst[51].trfBone.SetLocalRotation(dictSrc[117].vctRot.x, 0f, 0f);
				dictDst[53].trfBone.SetLocalPositionY(dictSrc[115].vctPos.y);
				dictDst[53].trfBone.SetLocalPositionZ(dictSrc[115].vctPos.z);
				dictDst[53].trfBone.SetLocalRotation(dictSrc[115].vctRot.x, 0f, 0f);
				dictDst[53].trfBone.SetLocalScale(dictSrc[115].vctScl.x, dictSrc[115].vctScl.y, dictSrc[115].vctScl.z);
				dictDst[55].trfBone.SetLocalPositionZ(0.3f + dictSrc[119].vctPos.z);
				dictDst[55].trfBone.SetLocalRotation(dictSrc[123].vctRot.x, 0f, 0f);
				dictDst[57].trfBone.SetLocalPositionZ(dictSrc[121].vctPos.z);
				dictDst[57].trfBone.SetLocalScale(dictSrc[121].vctScl.x, dictSrc[121].vctScl.y, dictSrc[121].vctScl.z);
				dictDst[59].trfBone.SetLocalPositionZ(dictSrc[125].vctPos.z);
				dictDst[59].trfBone.SetLocalScale(dictSrc[125].vctScl.x, dictSrc[125].vctScl.y, dictSrc[125].vctScl.z);
				dictDst[37].trfBone.SetLocalPositionZ(dictSrc[83].vctPos.z + dictSrc[81].vctPos.z);
				dictDst[37].trfBone.SetLocalScale(dictSrc[83].vctScl.x * dictSrc[81].vctScl.x, dictSrc[83].vctScl.y * dictSrc[81].vctScl.y, dictSrc[83].vctScl.z);
				dictDst[80].trfBone.SetLocalPositionX(x10);
				dictDst[80].trfBone.SetLocalPositionY(y5);
				dictDst[80].trfBone.SetLocalPositionZ(z5);
				dictDst[80].trfBone.SetLocalScale(x11, 1f, x11);
			}
			if ((updateMask & 4) != 0)
			{
				dictDst[39].trfBone.SetLocalPositionZ(dictSrc[85].vctPos.z);
				dictDst[39].trfBone.SetLocalScale(dictSrc[85].vctScl.x, dictSrc[85].vctScl.y, dictSrc[85].vctScl.z);
				dictDst[41].trfBone.SetLocalPositionZ(dictSrc[87].vctPos.z + dictSrc[89].vctPos.z);
				dictDst[41].trfBone.SetLocalScale(dictSrc[79].vctScl.x * dictSrc[89].vctScl.x, dictSrc[79].vctScl.y * dictSrc[89].vctScl.y, dictSrc[79].vctScl.z * dictSrc[89].vctScl.z);
			}
			if ((updateMask & 2) != 0)
			{
				float x12 = dictSrc[203].vctPos.x;
				float y6 = dictSrc[203].vctPos.y;
				float z6 = dictSrc[203].vctPos.z;
				float x13 = dictSrc[203].vctScl.x;
				dictDst[48].trfBone.SetLocalPositionX(dictSrc[96].vctPos.x + dictSrc[100].vctPos.x);
				dictDst[48].trfBone.SetLocalPositionY(dictSrc[98].vctPos.y + dictSrc[104].vctPos.y);
				dictDst[48].trfBone.SetLocalPositionZ(dictSrc[98].vctPos.z + dictSrc[102].vctPos.z);
				dictDst[48].trfBone.SetLocalRotation(dictSrc[98].vctRot.x + dictSrc[102].vctRot.x + dictSrc[104].vctRot.x + dictSrc[94].vctRot.x, dictSrc[100].vctRot.y + dictSrc[104].vctRot.y, 0f);
				dictDst[48].trfBone.SetLocalScale(dictSrc[92].vctScl.x, dictSrc[92].vctScl.y, dictSrc[92].vctScl.z);
				dictDst[46].trfBone.SetLocalPositionX(dictSrc[94].vctPos.x);
				dictDst[46].trfBone.SetLocalPositionY(dictSrc[94].vctPos.y);
				dictDst[46].trfBone.SetLocalPositionZ(dictSrc[94].vctPos.z);
				dictDst[46].trfBone.SetLocalRotation(0f, 0f, dictSrc[94].vctRot.z);
				dictDst[46].trfBone.SetLocalScale(dictSrc[94].vctScl.x, dictSrc[94].vctScl.y, dictSrc[94].vctScl.z);
				dictDst[44].trfBone.SetLocalPositionX(dictSrc[112].vctPos.x + dictSrc[110].vctPos.x);
				dictDst[44].trfBone.SetLocalPositionZ(0.65f + dictSrc[108].vctPos.z + dictSrc[110].vctPos.z);
				dictDst[44].trfBone.SetLocalRotation(dictSrc[108].vctRot.x, dictSrc[110].vctRot.y, 0f);
				dictDst[50].trfBone.SetLocalPositionY(dictSrc[106].vctPos.y);
				dictDst[50].trfBone.SetLocalPositionZ(dictSrc[106].vctPos.z);
				dictDst[50].trfBone.SetLocalRotation(dictSrc[106].vctRot.x, dictSrc[106].vctRot.y, dictSrc[106].vctRot.z);
				dictDst[50].trfBone.SetLocalScale(dictSrc[106].vctScl.x, dictSrc[106].vctScl.y, dictSrc[106].vctScl.z);
				dictDst[52].trfBone.SetLocalPositionX(dictSrc[106].vctPos.x);
				dictDst[52].trfBone.SetLocalPositionZ(0.3f + dictSrc[114].vctPos.z);
				dictDst[52].trfBone.SetLocalRotation(dictSrc[118].vctRot.x, 0f, 0f);
				dictDst[54].trfBone.SetLocalPositionY(dictSrc[116].vctPos.y);
				dictDst[54].trfBone.SetLocalPositionZ(dictSrc[116].vctPos.z);
				dictDst[54].trfBone.SetLocalRotation(dictSrc[116].vctRot.x, 0f, 0f);
				dictDst[54].trfBone.SetLocalScale(dictSrc[116].vctScl.x, dictSrc[116].vctScl.y, dictSrc[116].vctScl.z);
				dictDst[56].trfBone.SetLocalPositionZ(0.3f + dictSrc[120].vctPos.z);
				dictDst[56].trfBone.SetLocalRotation(dictSrc[124].vctRot.x, 0f, 0f);
				dictDst[58].trfBone.SetLocalPositionZ(dictSrc[122].vctPos.z);
				dictDst[58].trfBone.SetLocalScale(dictSrc[122].vctScl.x, dictSrc[122].vctScl.y, dictSrc[122].vctScl.z);
				dictDst[60].trfBone.SetLocalPositionZ(dictSrc[126].vctPos.z);
				dictDst[60].trfBone.SetLocalScale(dictSrc[126].vctScl.x, dictSrc[126].vctScl.y, dictSrc[126].vctScl.z);
				dictDst[38].trfBone.SetLocalPositionZ(dictSrc[84].vctPos.z + dictSrc[82].vctPos.z);
				dictDst[38].trfBone.SetLocalScale(dictSrc[84].vctScl.x * dictSrc[82].vctScl.x, dictSrc[84].vctScl.y * dictSrc[82].vctScl.y, dictSrc[84].vctScl.z);
				dictDst[81].trfBone.SetLocalPositionX(x12);
				dictDst[81].trfBone.SetLocalPositionY(y6);
				dictDst[81].trfBone.SetLocalPositionZ(z6);
				dictDst[81].trfBone.SetLocalScale(x13, 1f, x13);
			}
			if ((updateMask & 8) != 0)
			{
				dictDst[40].trfBone.SetLocalPositionZ(dictSrc[86].vctPos.z);
				dictDst[40].trfBone.SetLocalScale(dictSrc[86].vctScl.x, dictSrc[86].vctScl.y, dictSrc[86].vctScl.z);
				dictDst[42].trfBone.SetLocalPositionZ(dictSrc[88].vctPos.z + dictSrc[90].vctPos.z);
				dictDst[42].trfBone.SetLocalScale(dictSrc[80].vctScl.x * dictSrc[90].vctScl.x, dictSrc[80].vctScl.y * dictSrc[90].vctScl.y, dictSrc[80].vctScl.z * dictSrc[90].vctScl.z);
			}
			if ((updateMask & 0x10) != 0)
			{
				dictDst[35].trfBone.SetLocalScale(dictSrc[77].vctScl.x, 1f, dictSrc[77].vctScl.z);
				dictDst[36].trfBone.SetLocalScale(dictSrc[78].vctScl.x, 1f, dictSrc[78].vctScl.z);
				dictDst[29].trfBone.SetLocalPositionX(dictSrc[63].vctPos.x + dictSrc[57].vctPos.x);
				dictDst[29].trfBone.SetLocalPositionZ(dictSrc[63].vctPos.z);
				dictDst[29].trfBone.SetLocalRotation(0f, 0f, dictSrc[57].vctRot.z);
				dictDst[29].trfBone.SetLocalScale(dictSrc[61].vctScl.x * dictSrc[63].vctScl.x * dictSrc[57].vctScl.x, 1f, dictSrc[61].vctScl.z * dictSrc[63].vctScl.z * dictSrc[59].vctScl.z);
				dictDst[30].trfBone.SetLocalPositionX(dictSrc[64].vctPos.x + dictSrc[58].vctPos.x);
				dictDst[30].trfBone.SetLocalPositionZ(dictSrc[64].vctPos.z);
				dictDst[30].trfBone.SetLocalRotation(0f, 0f, dictSrc[58].vctRot.z);
				dictDst[30].trfBone.SetLocalScale(dictSrc[62].vctScl.x * dictSrc[64].vctScl.x * dictSrc[58].vctScl.x, 1f, dictSrc[62].vctScl.z * dictSrc[64].vctScl.z * dictSrc[60].vctScl.z);
				dictDst[31].trfBone.SetLocalScale(dictSrc[67].vctScl.x * dictSrc[69].vctScl.x * dictSrc[65].vctScl.x, 1f, dictSrc[67].vctScl.z * dictSrc[69].vctScl.z * dictSrc[65].vctScl.z);
				dictDst[32].trfBone.SetLocalScale(dictSrc[68].vctScl.x * dictSrc[70].vctScl.x * dictSrc[66].vctScl.x, 1f, dictSrc[68].vctScl.z * dictSrc[70].vctScl.z * dictSrc[66].vctScl.z);
				dictDst[33].trfBone.SetLocalScale(dictSrc[73].vctScl.x * dictSrc[75].vctScl.x * dictSrc[71].vctScl.x, 1f, dictSrc[73].vctScl.z * dictSrc[75].vctScl.z * dictSrc[71].vctScl.z);
				dictDst[34].trfBone.SetLocalScale(dictSrc[74].vctScl.x * dictSrc[76].vctScl.x * dictSrc[72].vctScl.x, 1f, dictSrc[74].vctScl.z * dictSrc[76].vctScl.z * dictSrc[72].vctScl.z);
				dictDst[19].trfBone.SetLocalPositionZ(dictSrc[39].vctPos.z);
				dictDst[19].trfBone.SetLocalScale(dictSrc[39].vctScl.x, 1f, dictSrc[39].vctScl.z);
				dictDst[20].trfBone.SetLocalPositionZ(dictSrc[40].vctPos.z);
				dictDst[20].trfBone.SetLocalScale(dictSrc[40].vctScl.x, 1f, dictSrc[40].vctScl.z);
				dictDst[21].trfBone.SetLocalPositionZ(dictSrc[43].vctPos.z);
				dictDst[21].trfBone.SetLocalScale(dictSrc[45].vctScl.x * dictSrc[43].vctScl.x * dictSrc[41].vctScl.x, 1f, dictSrc[45].vctScl.z * dictSrc[43].vctScl.z * dictSrc[41].vctScl.z);
				dictDst[22].trfBone.SetLocalPositionZ(dictSrc[44].vctPos.z);
				dictDst[22].trfBone.SetLocalScale(dictSrc[46].vctScl.x * dictSrc[44].vctScl.x * dictSrc[42].vctScl.x, 1f, dictSrc[46].vctScl.z * dictSrc[44].vctScl.z * dictSrc[42].vctScl.z);
				dictDst[23].trfBone.SetLocalRotation(dictSrc[49].vctRot.x, 0f, 0f);
				dictDst[23].trfBone.SetLocalScale(dictSrc[47].vctScl.x * dictSrc[49].vctScl.x, 1f, dictSrc[47].vctScl.z * dictSrc[49].vctScl.z);
				dictDst[24].trfBone.SetLocalRotation(dictSrc[50].vctRot.x, 0f, 0f);
				dictDst[24].trfBone.SetLocalScale(dictSrc[48].vctScl.x * dictSrc[50].vctScl.x, 1f, dictSrc[48].vctScl.z * dictSrc[50].vctScl.z);
				dictDst[25].trfBone.SetLocalScale(dictSrc[51].vctScl.x * dictSrc[53].vctScl.x, 1f, dictSrc[51].vctScl.z * dictSrc[53].vctScl.z);
				dictDst[26].trfBone.SetLocalScale(dictSrc[52].vctScl.x * dictSrc[54].vctScl.x, 1f, dictSrc[52].vctScl.z * dictSrc[54].vctScl.z);
				dictDst[27].trfBone.SetLocalPositionX(dictSrc[55].vctPos.x);
				dictDst[27].trfBone.SetLocalPositionZ(dictSrc[55].vctPos.z);
				dictDst[27].trfBone.SetLocalRotation(dictSrc[55].vctRot.x, 0f, dictSrc[55].vctRot.z);
				dictDst[27].trfBone.SetLocalScale(dictSrc[55].vctScl.x, 1f, dictSrc[55].vctScl.z);
				dictDst[28].trfBone.SetLocalPositionX(dictSrc[56].vctPos.x);
				dictDst[28].trfBone.SetLocalPositionZ(dictSrc[55].vctPos.z);
				dictDst[28].trfBone.SetLocalRotation(dictSrc[56].vctRot.x, 0f, dictSrc[56].vctRot.z);
				dictDst[28].trfBone.SetLocalScale(dictSrc[56].vctScl.x, 1f, dictSrc[56].vctScl.z);
				dictDst[64].trfBone.SetLocalPosition(dictSrc[144].vctPos.x, dictSrc[146].vctPos.y + dictSrc[144].vctPos.y, dictSrc[142].vctPos.z + dictSrc[144].vctPos.z);
				dictDst[64].trfBone.SetLocalRotation(dictSrc[146].vctRot.x, 0f, 0f);
				dictDst[64].trfBone.SetLocalScale(dictSrc[140].vctScl.x * dictSrc[142].vctScl.x * dictSrc[144].vctScl.x, dictSrc[144].vctScl.y, dictSrc[138].vctScl.z * dictSrc[140].vctScl.z * dictSrc[142].vctScl.z * dictSrc[144].vctScl.z);
				dictDst[65].trfBone.SetLocalPosition(dictSrc[145].vctPos.x, dictSrc[147].vctPos.y + dictSrc[145].vctPos.y, dictSrc[143].vctPos.z + dictSrc[145].vctPos.z);
				dictDst[65].trfBone.SetLocalRotation(dictSrc[147].vctRot.x, 0f, 0f);
				dictDst[65].trfBone.SetLocalScale(dictSrc[141].vctScl.x * dictSrc[143].vctScl.x * dictSrc[145].vctScl.x, dictSrc[145].vctScl.y, dictSrc[139].vctScl.z * dictSrc[141].vctScl.z * dictSrc[143].vctScl.z * dictSrc[145].vctScl.z);
				dictDst[82].trfBone.SetLocalPositionY(num + num3);
				dictDst[82].trfBone.SetLocalPositionZ(z + num4);
				dictDst[82].trfBone.SetLocalScale(x * num6 * dictSrc[205].vctScl.x, num2 * num5, x * num6);
				dictDst[83].trfBone.SetLocalPositionX(0f - num7);
				dictDst[83].trfBone.SetLocalPositionY(dictSrc[197].vctPos.y);
				dictDst[83].trfBone.SetLocalRotation(dictSrc[197].vctRot.x, 0f, 0f - num8);
				dictDst[83].trfBone.SetLocalScale(x2, y, 1f);
				dictDst[84].trfBone.SetLocalPositionX(num7);
				dictDst[84].trfBone.SetLocalPositionY(dictSrc[197].vctPos.y);
				dictDst[84].trfBone.SetLocalRotation(dictSrc[197].vctRot.x, 0f, num8);
				dictDst[84].trfBone.SetLocalScale(x2, y, 1f);
				dictDst[85].trfBone.SetLocalPositionX(0f - (x3 + x4 + x6));
				dictDst[85].trfBone.SetLocalPositionY(y2 + y4);
				dictDst[85].trfBone.SetLocalPositionZ(num11 + z2 + z3 + z4);
				dictDst[85].trfBone.SetLocalRotation(x8, 0f, 0f);
				dictDst[85].trfBone.SetLocalScale(num9 * x5 * x7 * x9 / dictSrc[205].vctScl.x, num10 * y3, num9 * x5 * x7);
				dictDst[86].trfBone.SetLocalPositionX(x3 + x4 + x6);
				dictDst[86].trfBone.SetLocalPositionY(y2 + y4);
				dictDst[86].trfBone.SetLocalPositionZ(num11 + z2 + z3 + z4);
				dictDst[86].trfBone.SetLocalRotation(x8, 0f, 0f);
				dictDst[86].trfBone.SetLocalScale(num9 * x5 * x7 * x9 / dictSrc[205].vctScl.x, num10 * y3, num9 * x5 * x7);
				dictDst[87].trfBone.SetLocalPositionX(dictSrc[206].vctPos.x + dictSrc[209].vctPos.x);
				dictDst[87].trfBone.SetLocalScale(dictSrc[206].vctScl.x * dictSrc[209].vctScl.x, 1f, dictSrc[208].vctScl.z * dictSrc[209].vctScl.z);
				dictDst[88].trfBone.SetLocalPositionX(dictSrc[207].vctPos.x + dictSrc[210].vctPos.x);
				dictDst[88].trfBone.SetLocalScale(dictSrc[207].vctScl.x * dictSrc[210].vctScl.x, 1f, dictSrc[208].vctScl.z * dictSrc[210].vctScl.z);
			}
		}
	}

	public override void UpdateAlways()
	{
		if (base.InitEnd)
		{
		}
	}
}
