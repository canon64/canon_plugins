using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Manager;
using MessagePack;
using UnityEngine;

public class ChaFileControl : ChaFile
{
	private static bool saveBytesMode;

	public bool skipRangeCheck;

	public void ChangeSaveBytesMode(bool bytes)
	{
		saveBytesMode = bytes;
	}

	public ChaFilePreview GetPreviewInfo()
	{
		return new ChaFilePreview
		{
			sex = parameter.sex,
			lastname = parameter.lastname,
			firstname = parameter.firstname,
			nickname = parameter.nickname,
			personality = parameter.personality,
			bloodType = parameter.bloodType,
			birthMonth = parameter.birthMonth,
			birthDay = parameter.birthDay,
			clubActivities = parameter.clubActivities,
			voicePitch = parameter.voicePitch
		};
	}

	public static byte CheckDataRangeFace(ChaFile chafile, List<string> checkInfo = null)
	{
		ChaListControl chaListCtrl = Character.chaListCtrl;
		ChaFileFace face = chafile.custom.face;
		bool flag = false;
		for (int i = 0; i < face.shapeValueFace.Length; i++)
		{
			if (!MathfEx.RangeEqualOn(0f, face.shapeValueFace[i], 1f))
			{
				checkInfo?.Add($"【{ChaFileDefine.cf_headshapename[i]}】値:{face.shapeValueFace[i]}");
				flag = true;
				face.shapeValueFace[i] = Mathf.Clamp(face.shapeValueFace[i], 0f, 1f);
			}
		}
		if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.bo_head, face.headId))
		{
			checkInfo?.Add($"【頭の種類】値:{face.headId}");
			flag = true;
			face.headId = 0;
		}
		if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.mt_face_detail, face.detailId))
		{
			checkInfo?.Add($"【彫りの種類】値:{face.detailId}");
			flag = true;
			face.detailId = 0;
		}
		if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.mt_eyebrow, face.eyebrowId))
		{
			checkInfo?.Add($"【眉毛の種類】値:{face.eyebrowId}");
			flag = true;
			face.eyebrowId = 0;
		}
		if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.mt_nose, face.noseId))
		{
			checkInfo?.Add($"【鼻の種類】値:{face.noseId}");
			flag = true;
			face.noseId = 0;
		}
		for (int j = 0; j < 2; j++)
		{
			if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.mt_eye, face.pupil[j].id))
			{
				checkInfo?.Add($"【瞳の種類】値:{face.pupil[j].id}");
				flag = true;
				face.pupil[j].id = 0;
			}
			if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.mt_eye_gradation, face.pupil[j].gradMaskId))
			{
				checkInfo?.Add($"【瞳のグラデマスク種類】値:{face.pupil[j].gradMaskId}");
				flag = true;
				face.pupil[j].gradMaskId = 0;
			}
		}
		if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.mt_eye_hi_up, face.hlUpId))
		{
			checkInfo?.Add($"【ハイライト上種類】値:{face.hlUpId}");
			flag = true;
			face.hlUpId = 0;
		}
		if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.mt_eye_hi_down, face.hlDownId))
		{
			checkInfo?.Add($"【ハイライト下種類】値:{face.hlDownId}");
			flag = true;
			face.hlDownId = 0;
		}
		if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.mt_eye_white, face.whiteId))
		{
			checkInfo?.Add($"【白目種類】値:{face.whiteId}");
			flag = true;
			face.whiteId = 0;
		}
		if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.mt_eyeline_up, face.eyelineUpId))
		{
			checkInfo?.Add($"【アイライン上種類】値:{face.eyelineUpId}");
			flag = true;
			face.eyelineUpId = 0;
		}
		if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.mt_eyeline_down, face.eyelineDownId))
		{
			checkInfo?.Add($"【アイライン下種類】値:{face.eyelineDownId}");
			flag = true;
			face.eyelineDownId = 0;
		}
		if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.mt_mole, face.moleId))
		{
			checkInfo?.Add($"【ホクロの種類】値:{face.moleId}");
			flag = true;
			face.moleId = 0;
		}
		if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.mt_lipline, face.lipLineId))
		{
			checkInfo?.Add($"【リップラインの種類】値:{face.lipLineId}");
			flag = true;
			face.lipLineId = 0;
		}
		if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.mt_eyeshadow, face.baseMakeup.eyeshadowId))
		{
			checkInfo?.Add($"【アイシャドウ種類】値:{face.baseMakeup.eyeshadowId}");
			flag = true;
			face.baseMakeup.eyeshadowId = 0;
		}
		if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.mt_cheek, face.baseMakeup.cheekId))
		{
			checkInfo?.Add($"【チークの種類】値:{face.baseMakeup.cheekId}");
			flag = true;
			face.baseMakeup.cheekId = 0;
		}
		if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.mt_lip, face.baseMakeup.lipId))
		{
			checkInfo?.Add($"【リップの種類】値:{face.baseMakeup.lipId}");
			flag = true;
			face.baseMakeup.lipId = 0;
		}
		for (int k = 0; k < 2; k++)
		{
			if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.mt_face_paint, face.baseMakeup.paintId[k]))
			{
				checkInfo?.Add($"【ペイント種類】値:{face.baseMakeup.paintId[k]}");
				flag = true;
				face.baseMakeup.paintId[k] = 0;
			}
		}
		return (byte)(flag ? 1u : 0u);
	}

	public static byte CheckDataRangeBody(ChaFile chafile, List<string> checkInfo = null)
	{
		ChaListControl chaListCtrl = Character.chaListCtrl;
		ChaFileBody body = chafile.custom.body;
		bool flag = false;
		for (int i = 0; i < body.shapeValueBody.Length; i++)
		{
			if (!MathfEx.RangeEqualOn(0f, body.shapeValueBody[i], 1f))
			{
				checkInfo?.Add($"【{ChaFileDefine.cf_bodyshapename[i]}】値:{body.shapeValueBody[i]}");
				flag = true;
				body.shapeValueBody[i] = Mathf.Clamp(body.shapeValueBody[i], 0f, 1f);
			}
		}
		if (!MathfEx.RangeEqualOn(0f, body.areolaSize, 1f))
		{
			checkInfo?.Add($"【乳輪のサイズ】値:{body.areolaSize}");
			flag = true;
			body.areolaSize = Mathf.Clamp(body.areolaSize, 0f, 1f);
		}
		if (!MathfEx.RangeEqualOn(0f, body.bustSoftness, 1f))
		{
			checkInfo?.Add($"【胸の柔らかさ】値:{body.bustSoftness}");
			flag = true;
			body.bustSoftness = Mathf.Clamp(body.bustSoftness, 0f, 1f);
		}
		if (!MathfEx.RangeEqualOn(0f, body.bustWeight, 1f))
		{
			checkInfo?.Add($"【胸の重さ】値:{body.bustWeight}");
			flag = true;
			body.bustWeight = Mathf.Clamp(body.bustWeight, 0f, 1f);
		}
		if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.mt_body_detail, body.detailId))
		{
			checkInfo?.Add($"【筋肉表現】値:{body.detailId}");
			flag = true;
			body.detailId = 0;
		}
		for (int j = 0; j < 2; j++)
		{
			if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.mt_body_paint, body.paintId[j]))
			{
				checkInfo?.Add($"【ペイントの種類】値:{body.paintId[j]}");
				flag = true;
				body.paintId[j] = 0;
			}
		}
		if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.mt_sunburn, body.sunburnId))
		{
			checkInfo?.Add($"【日焼けの種類】値:{body.sunburnId}");
			flag = true;
			body.sunburnId = 0;
		}
		if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.mt_nip, body.nipId))
		{
			checkInfo?.Add($"【乳首の種類】値:{body.nipId}");
			flag = true;
			body.nipId = 0;
		}
		if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.mt_underhair, body.underhairId))
		{
			checkInfo?.Add($"【アンダーヘア種類】値:{body.underhairId}");
			flag = true;
			body.underhairId = 0;
		}
		return (byte)(flag ? 2u : 0u);
	}

	public static byte CheckDataRangeHair(ChaFile chafile, List<string> checkInfo = null)
	{
		ChaListControl chaListCtrl = Character.chaListCtrl;
		ChaFileHair hair = chafile.custom.hair;
		bool flag = false;
		if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.bo_hair_b, hair.parts[0].id))
		{
			checkInfo?.Add($"【後ろ髪】値:{hair.parts[0].id}");
			flag = true;
			hair.parts[0].id = 0;
		}
		if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.bo_hair_f, hair.parts[1].id))
		{
			checkInfo?.Add($"【前髪】値:{hair.parts[1].id}");
			flag = true;
			hair.parts[1].id = 1;
		}
		if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.bo_hair_s, hair.parts[2].id))
		{
			checkInfo?.Add($"【横髪】値:{hair.parts[2].id}");
			flag = true;
			hair.parts[2].id = 0;
		}
		if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.bo_hair_o, hair.parts[3].id))
		{
			checkInfo?.Add($"【エクステ】値:{hair.parts[3].id}");
			flag = true;
			hair.parts[3].id = 0;
		}
		if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.mt_hairgloss, hair.glossId))
		{
			checkInfo?.Add($"【グロスID】値:{hair.glossId}");
			flag = true;
			hair.glossId = 0;
		}
		return (byte)(flag ? 4u : 0u);
	}

	public static byte CheckDataRangeCoordinate(ChaFile chafile, List<string> checkInfo = null)
	{
		ChaListControl chaListCtrl = Character.chaListCtrl;
		ChaFileCoordinate[] array = chafile.coordinate;
		bool flag = false;
		string[] array2 = new string[9] { "トップス", "ボトムス", "インナー(上)", "インナー(下)", "手袋", "パンスト", "靴下", "内履き", "外履き" };
		ChaListDefine.CategoryNo[] array3 = new ChaListDefine.CategoryNo[9]
		{
			ChaListDefine.CategoryNo.co_top,
			ChaListDefine.CategoryNo.co_bot,
			ChaListDefine.CategoryNo.co_bra,
			ChaListDefine.CategoryNo.co_shorts,
			ChaListDefine.CategoryNo.co_gloves,
			ChaListDefine.CategoryNo.co_panst,
			ChaListDefine.CategoryNo.co_socks,
			ChaListDefine.CategoryNo.co_shoes,
			ChaListDefine.CategoryNo.co_shoes
		};
		int[] array4 = new int[9] { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
		int[] array5 = new int[9]
		{
			0,
			0,
			(chafile.parameter.sex == 0) ? ChaFileDefine.DefClothesMBraID : ChaFileDefine.DefClothesFBraID,
			(chafile.parameter.sex == 0) ? ChaFileDefine.DefClothesMShortsID : ChaFileDefine.DefClothesFShortsID,
			0,
			0,
			0,
			0,
			0
		};
		string[,] array6 = new string[2, 3]
		{
			{ "セーラーA", "セーラーB", "セーラーC" },
			{ "ジャケットA", "ジャケットB", "ジャケットC" }
		};
		ChaListDefine.CategoryNo[,] array7 = new ChaListDefine.CategoryNo[2, 3]
		{
			{
				ChaListDefine.CategoryNo.cpo_sailor_a,
				ChaListDefine.CategoryNo.cpo_sailor_b,
				ChaListDefine.CategoryNo.cpo_sailor_c
			},
			{
				ChaListDefine.CategoryNo.cpo_jacket_a,
				ChaListDefine.CategoryNo.cpo_jacket_b,
				ChaListDefine.CategoryNo.cpo_jacket_c
			}
		};
		int[] array8 = new int[3] { 0, 1, 2 };
		int[,] array9 = new int[2, 3]
		{
			{ 0, 0, 1 },
			{ 0, 1, 1 }
		};
		for (int i = 0; i < array.Length; i++)
		{
			ChaFileClothes clothes = array[i].clothes;
			for (int j = 0; j < array4.Length; j++)
			{
				if (!chaListCtrl.ContainsInfo(array3[j], clothes.parts[array4[j]].id))
				{
					checkInfo?.Add($"【{array2[j]}】値:{clothes.parts[array4[j]].id}");
					flag = true;
					clothes.parts[array4[j]].id = array5[j];
				}
				if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.mt_emblem, clothes.parts[array4[j]].emblemeId))
				{
					checkInfo?.Add($"【エンブレム】値:{clothes.parts[array4[j]].emblemeId}");
					flag = true;
					clothes.parts[array4[j]].emblemeId = 0;
				}
				if (!chaListCtrl.ContainsInfo(ChaListDefine.CategoryNo.mt_emblem, clothes.parts[array4[j]].emblemeId2))
				{
					checkInfo?.Add($"【エンブレム】値:{clothes.parts[array4[j]].emblemeId2}");
					flag = true;
					clothes.parts[array4[j]].emblemeId2 = 0;
				}
			}
			int infoKind = chaListCtrl.GetInfoKind(ChaListDefine.CategoryNo.co_top, clothes.parts[0].id);
			if (1 == infoKind || 2 == infoKind)
			{
				int num = ((1 != infoKind) ? 1 : 0);
				for (int k = 0; k < array8.Length; k++)
				{
					if (!chaListCtrl.ContainsInfo(array7[num, k], clothes.subPartsId[array8[k]]))
					{
						checkInfo?.Add($"【{array6[num, k]}】値:{clothes.subPartsId[array8[k]]}");
						flag = true;
						clothes.subPartsId[array8[k]] = array9[num, k];
					}
				}
			}
			ChaFileAccessory accessory = array[i].accessory;
			for (int l = 0; l < accessory.parts.Length; l++)
			{
				int type = accessory.parts[l].type;
				_ = accessory.parts[l].id;
				if (chaListCtrl.ContainsCategoryInfo((ChaListDefine.CategoryNo)type) && !chaListCtrl.ContainsInfo((ChaListDefine.CategoryNo)type, accessory.parts[l].id))
				{
					checkInfo?.Add($"【{ChaAccessoryDefine.GetAccessoryTypeName((ChaListDefine.CategoryNo)type)}】値:{accessory.parts[l].id}");
					flag = true;
					accessory.parts[l].MemberInit();
				}
			}
		}
		return (byte)(flag ? 8u : 0u);
	}

	public static byte CheckDataRangeParameter(ChaFile chafile, List<string> checkInfo = null)
	{
		ChaFileParameter chaFileParameter = chafile.parameter;
		bool flag = false;
		if (chaFileParameter.sex == 0)
		{
			return 0;
		}
		if (!Voice.infoTable.ContainsKey(chaFileParameter.personality))
		{
			checkInfo?.Add($"【性格】値:{chaFileParameter.personality}");
			flag = true;
			chaFileParameter.personality = 0;
		}
		if (!Game.ClubInfos.ContainsKey(chaFileParameter.clubActivities))
		{
			checkInfo?.Add($"【部活】値:{chaFileParameter.clubActivities}");
			flag = true;
			chaFileParameter.clubActivities = 3;
		}
		return (byte)(flag ? 16u : 0u);
	}

	public static bool CheckDataRange(ChaFile chafile, bool chk_custom, bool chk_clothes, bool chk_parameter, List<string> checkInfo = null)
	{
		byte b = 0;
		if (chk_custom)
		{
			b |= CheckDataRangeFace(chafile, checkInfo);
			b |= CheckDataRangeBody(chafile, checkInfo);
			b |= CheckDataRangeHair(chafile, checkInfo);
		}
		if (chk_clothes)
		{
			b |= CheckDataRangeCoordinate(chafile, checkInfo);
		}
		if (chk_parameter)
		{
			b |= CheckDataRangeParameter(chafile, checkInfo);
		}
		if (b > 0)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendFormat("【{0}】 範囲外！", chafile.parameter.fullname);
			if ((b & 1) > 0)
			{
				stringBuilder.AppendLine();
				stringBuilder.Append("顔");
			}
			if ((b & 2) > 0)
			{
				stringBuilder.AppendLine();
				stringBuilder.Append("体");
			}
			if ((b & 4) > 0)
			{
				stringBuilder.AppendLine();
				stringBuilder.Append("髪");
			}
			if ((b & 8) > 0)
			{
				stringBuilder.AppendLine();
				stringBuilder.Append("服");
			}
			if ((b & 0x10) > 0)
			{
				stringBuilder.AppendLine();
				stringBuilder.Append("パラメーター");
			}
		}
		return b > 0;
	}

	public bool SaveCharaFile(string filename, byte sex = byte.MaxValue, bool newFile = false)
	{
		string path = ConvertCharaFilePath(filename, sex, newFile);
		string directoryName = Path.GetDirectoryName(path);
		if (!Directory.Exists(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		base.charaFileName = Path.GetFileName(path);
		string userID = about.userID;
		string dataID = about.dataID;
		if (about.userID != GameSystem.UserUUID)
		{
			about.dataID = YS_Assist.CreateUUID();
		}
		else if (!File.Exists(path))
		{
			about.dataID = YS_Assist.CreateUUID();
		}
		about.userID = GameSystem.UserUUID;
		using FileStream st = new FileStream(path, FileMode.Create, FileAccess.Write);
		bool result = SaveCharaFile(st, savePng: true);
		about.userID = userID;
		about.dataID = dataID;
		return result;
	}

	public bool SaveCharaFile(Stream st, bool savePng)
	{
		using BinaryWriter bw = new BinaryWriter(st);
		return SaveCharaFile(bw, savePng);
	}

	public bool SaveCharaFile(BinaryWriter bw, bool savePng)
	{
		return SaveFile(bw, savePng);
	}

	public bool LoadCharaFile(string filename, byte sex = byte.MaxValue, bool noLoadPng = false, bool noLoadStatus = true)
	{
		base.charaFileName = Path.GetFileName(filename);
		using FileStream st = new FileStream(ConvertCharaFilePath(filename, sex), FileMode.Open, FileAccess.Read);
		return LoadCharaFile(st, noLoadPng, noLoadStatus);
	}

	public bool LoadCharaFile(Stream st, bool noLoadPng = false, bool noLoadStatus = true)
	{
		using BinaryReader br = new BinaryReader(st);
		return LoadCharaFile(br, noLoadPng, noLoadStatus);
	}

	public bool LoadCharaFile(BinaryReader br, bool noLoadPng = false, bool noLoadStatus = true)
	{
		bool result = LoadFile(br, noLoadPng, noLoadStatus);
		if (!skipRangeCheck)
		{
			Character.isMod = CheckDataRange(this, chk_custom: true, chk_clothes: true, chk_parameter: true);
		}
		return result;
	}

	public bool LoadCharaFileKoikatsu(string filename, byte sex = byte.MaxValue, bool noLoadPng = false, bool noLoadStatus = true)
	{
		base.charaFileName = Path.GetFileName(filename);
		using FileStream st = new FileStream(ConvertCharaFilePath(filename, sex), FileMode.Open, FileAccess.Read);
		return LoadCharaFileKoikatsu(st, noLoadPng, noLoadStatus);
	}

	public bool LoadCharaFileKoikatsu(Stream st, bool noLoadPng = false, bool noLoadStatus = true)
	{
		using BinaryReader br = new BinaryReader(st);
		return LoadCharaFileKoikatsu(br, noLoadPng, noLoadStatus);
	}

	public bool LoadCharaFileKoikatsu(BinaryReader br, bool noLoadPng = false, bool noLoadStatus = true)
	{
		bool result = LoadFileKoikatsu(br, noLoadPng, noLoadStatus);
		if (!skipRangeCheck)
		{
			Character.isMod = CheckDataRange(this, chk_custom: true, chk_clothes: true, chk_parameter: true);
		}
		return result;
	}

	public bool LoadFileLimited(string filename, byte sex = byte.MaxValue, bool face = true, bool body = true, bool hair = true, bool parameter = true, bool coordinate = true, bool extension = true)
	{
		string path = ConvertCharaFilePath(filename, sex);
		ChaFileControl chaFileControl = new ChaFileControl();
		if (chaFileControl.LoadFile(path))
		{
			if (!skipRangeCheck)
			{
				Character.isMod = CheckDataRange(chaFileControl, chk_custom: true, chk_clothes: true, chk_parameter: true);
			}
			byte[] array = null;
			if (face)
			{
				array = MessagePackSerializer.Serialize(chaFileControl.custom.face);
				custom.face = MessagePackSerializer.Deserialize<ChaFileFace>(array);
			}
			if (body)
			{
				array = MessagePackSerializer.Serialize(chaFileControl.custom.body);
				custom.body = MessagePackSerializer.Deserialize<ChaFileBody>(array);
			}
			if (hair)
			{
				array = MessagePackSerializer.Serialize(chaFileControl.custom.hair);
				custom.hair = MessagePackSerializer.Deserialize<ChaFileHair>(array);
			}
			if (parameter)
			{
				base.parameter.Copy(chaFileControl.parameter);
			}
			if (coordinate)
			{
				CopyCoordinate(chaFileControl.coordinate);
			}
			if (face && body && hair && parameter && coordinate)
			{
				about.userID = chaFileControl.about.userID;
				about.dataID = chaFileControl.about.dataID;
			}
			pngData = chaFileControl.pngData;
			facePngData = chaFileControl.facePngData;
		}
		return false;
	}

	public bool LoadFileLimited(string assetBundleName, string assetName, bool face = true, bool body = true, bool hair = true, bool parameter = true, bool coordinate = true)
	{
		ChaFileControl chaFileControl = new ChaFileControl();
		TextAsset textAsset = CommonLib.LoadAsset<TextAsset>(assetBundleName, assetName);
		if (null == textAsset)
		{
			return false;
		}
		if (chaFileControl.LoadFromTextAsset(textAsset, noSetPNG: true))
		{
			byte[] array = null;
			if (face)
			{
				array = MessagePackSerializer.Serialize(chaFileControl.custom.face);
				custom.face = MessagePackSerializer.Deserialize<ChaFileFace>(array);
			}
			if (body)
			{
				array = MessagePackSerializer.Serialize(chaFileControl.custom.body);
				custom.body = MessagePackSerializer.Deserialize<ChaFileBody>(array);
			}
			if (hair)
			{
				array = MessagePackSerializer.Serialize(chaFileControl.custom.hair);
				custom.hair = MessagePackSerializer.Deserialize<ChaFileHair>(array);
			}
			if (parameter)
			{
				base.parameter.Copy(chaFileControl.parameter);
			}
			if (coordinate)
			{
				CopyCoordinate(chaFileControl.coordinate);
			}
		}
		return false;
	}

	public bool LoadFromAssetBundle(string assetBundleName, string assetName, bool noSetPNG = false, bool noLoadStatus = true)
	{
		TextAsset textAsset = CommonLib.LoadAsset<TextAsset>(assetBundleName, assetName);
		if (null == textAsset)
		{
			return false;
		}
		bool result = LoadFromTextAsset(textAsset, noSetPNG, noLoadStatus);
		AssetBundleManager.UnloadAssetBundle(assetBundleName, isUnloadForceRefCount: true, null, unloadAllLoadedObjects: true);
		return result;
	}

	public bool LoadFromTextAsset(TextAsset ta, bool noSetPNG = false, bool noLoadStatus = true)
	{
		if (null == ta)
		{
			return false;
		}
		using MemoryStream memoryStream = new MemoryStream();
		memoryStream.Write(ta.bytes, 0, ta.bytes.Length);
		memoryStream.Seek(0L, SeekOrigin.Begin);
		return LoadCharaFile(memoryStream, noSetPNG, noLoadStatus);
	}

	public bool LoadFromBytes(byte[] bytes, bool noSetPNG = false, bool noLoadStatus = true)
	{
		using MemoryStream memoryStream = new MemoryStream();
		memoryStream.Write(bytes, 0, bytes.Length);
		memoryStream.Seek(0L, SeekOrigin.Begin);
		bool result = LoadCharaFile(memoryStream, noSetPNG, noLoadStatus);
		if (!skipRangeCheck)
		{
			Character.isMod = CheckDataRange(this, chk_custom: true, chk_clothes: true, chk_parameter: true);
		}
		return result;
	}

	public void SaveFace(string path)
	{
		if (custom != null)
		{
			custom.SaveFace(path);
		}
	}

	public void LoadFace(string path)
	{
		if (custom != null)
		{
			custom.LoadFace(path);
		}
	}

	public void LoadFacePreset()
	{
		ListInfoBase listInfo = Character.chaListCtrl.GetListInfo(ChaListDefine.CategoryNo.face_preset, custom.face.headId);
		string info = listInfo.GetInfo(ChaListDefine.KeyType.MainManifest);
		string info2 = listInfo.GetInfo(ChaListDefine.KeyType.MainAB);
		string info3 = listInfo.GetInfo(ChaListDefine.KeyType.MainData);
		TextAsset textAsset = CommonLib.LoadAsset<TextAsset>(info2, info3, clone: false, info);
		if (!(null == textAsset))
		{
			custom.LoadFace(textAsset.bytes);
			AssetBundleManager.UnloadAssetBundle(info2, isUnloadForceRefCount: true);
		}
	}

	public string ConvertCharaFilePath(string path, byte _sex, bool newFile = false)
	{
		byte b = ((byte.MaxValue == _sex) ? parameter.sex : _sex);
		string text = "";
		string text2 = "";
		if (path != "")
		{
			text = Path.GetDirectoryName(path);
			text2 = Path.GetFileName(path);
		}
		text = ((!(text == "")) ? (text + "/") : (UserData.Path + ((b == 0) ? "chara/male/" : "chara/female/")));
		if (text2 == "")
		{
			text2 = ((!newFile && !(base.charaFileName == "")) ? base.charaFileName : ("KoiKatsuSun_" + ((b == 0) ? "M" : "F") + "_" + DateTime.Now.ToString("yyyyMMddHHmmssfff")));
		}
		if (saveBytesMode)
		{
			return text + Path.GetFileNameWithoutExtension(text2) + ".bytes";
		}
		if (Path.GetExtension(text2).IsNullOrEmpty())
		{
			return text + Path.GetFileNameWithoutExtension(text2) + ".png";
		}
		return text + text2;
	}

	public static string ConvertCoordinateFilePath(string path)
	{
		string text = "";
		string text2 = "";
		if (path != "")
		{
			text = Path.GetDirectoryName(path);
			text2 = Path.GetFileName(path);
		}
		text = ((!(text == "")) ? (text + "/") : (UserData.Path + "coordinate/"));
		if (text2 == "")
		{
			text2 = "KKCoorde_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
		}
		if (saveBytesMode)
		{
			return text + Path.GetFileNameWithoutExtension(text2) + ".bytes";
		}
		if (Path.GetExtension(text2).IsNullOrEmpty())
		{
			return text + Path.GetFileNameWithoutExtension(text2) + ".png";
		}
		return text + text2;
	}
}
