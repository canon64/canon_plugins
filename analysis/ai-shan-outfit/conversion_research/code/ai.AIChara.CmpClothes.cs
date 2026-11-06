using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AIChara;

[DisallowMultipleComponent]
public class CmpClothes : CmpBase
{
	[Header("破れフラグ")]
	public bool useBreak;

	[Header("通常パーツ")]
	public Renderer[] rendNormal01;

	public Renderer[] rendNormal02;

	public Renderer[] rendNormal03;

	public bool useColorN01;

	public bool useColorN02;

	public bool useColorN03;

	public bool useColorA01;

	public bool useColorA02;

	public bool useColorA03;

	[Header("着衣・半脱のまとめ")]
	public GameObject objTopDef;

	public GameObject objTopHalf;

	public GameObject objBotDef;

	public GameObject objBotHalf;

	[Header("オプションパーツ")]
	public GameObject[] objOpt01;

	public GameObject[] objOpt02;

	[Header("柄サイズ調整(固定)")]
	public Vector4 uvScalePattern = new Vector4(1f, 1f, 0f, 0f);

	[Header("基本初期設定")]
	public Color defMainColor01 = Color.white;

	public Color defMainColor02 = Color.white;

	public Color defMainColor03 = Color.white;

	public int defPtnIndex01;

	public int defPtnIndex02;

	public int defPtnIndex03;

	public Color defPatternColor01 = Color.white;

	public Color defPatternColor02 = Color.white;

	public Color defPatternColor03 = Color.white;

	[Range(0f, 1f)]
	public float defGloss01;

	[Range(0f, 1f)]
	public float defGloss02;

	[Range(0f, 1f)]
	public float defGloss03;

	[Range(0f, 1f)]
	public float defMetallic01;

	[Range(0f, 1f)]
	public float defMetallic02;

	[Range(0f, 1f)]
	public float defMetallic03;

	public Vector4 defLayout01 = new Vector4(10f, 10f, 0f, 0f);

	public Vector4 defLayout02 = new Vector4(10f, 10f, 0f, 0f);

	public Vector4 defLayout03 = new Vector4(10f, 10f, 0f, 0f);

	[Range(-1f, 1f)]
	public float defRotation01;

	[Range(-1f, 1f)]
	public float defRotation02;

	[Range(-1f, 1f)]
	public float defRotation03;

	[Space]
	[Header("４色目(固定)")]
	public Color defMainColor04 = Color.white;

	[Range(0f, 1f)]
	public float defGloss04;

	[Range(0f, 1f)]
	public float defMetallic04;

	[Space]
	[Button("SetDefault", "初期色を設定", new object[] { })]
	public int setdefault;

	public CmpClothes()
		: base(_baseDB: true)
	{
	}

	public void SetDefault()
	{
		Material material = null;
		if (rendNormal01 != null && rendNormal01.Length != 0)
		{
			material = rendNormal01[0].sharedMaterial;
		}
		if (!(null != material))
		{
			return;
		}
		if (useColorN01 || useColorA01)
		{
			if (material.HasProperty("_Color"))
			{
				defMainColor01 = material.GetColor("_Color");
			}
			if (material.HasProperty("_Color1_2"))
			{
				defPatternColor01 = material.GetColor("_Color1_2");
			}
			if (material.HasProperty("_Glossiness"))
			{
				defGloss01 = material.GetFloat("_Glossiness");
			}
			if (material.HasProperty("_Metallic"))
			{
				defMetallic01 = material.GetFloat("_Metallic");
			}
			if (material.HasProperty("_patternuv1"))
			{
				defLayout01 = material.GetVector("_patternuv1");
			}
			if (material.HasProperty("_patternuv1Rotator"))
			{
				defRotation01 = material.GetFloat("_patternuv1Rotator");
			}
		}
		if (useColorN02 || useColorA02)
		{
			if (material.HasProperty("_Color2"))
			{
				defMainColor02 = material.GetColor("_Color2");
			}
			if (material.HasProperty("_Color2_2"))
			{
				defPatternColor01 = material.GetColor("_Color2_2");
			}
			if (material.HasProperty("_Glossiness2"))
			{
				defGloss02 = material.GetFloat("_Glossiness2");
			}
			if (material.HasProperty("_Metallic2"))
			{
				defMetallic02 = material.GetFloat("_Metallic2");
			}
			if (material.HasProperty("_patternuv2"))
			{
				defLayout02 = material.GetVector("_patternuv2");
			}
			if (material.HasProperty("_patternuv2Rotator"))
			{
				defRotation02 = material.GetFloat("_patternuv2Rotator");
			}
		}
		if (useColorN03 || useColorA03)
		{
			if (material.HasProperty("_Color3"))
			{
				defMainColor03 = material.GetColor("_Color3");
			}
			if (material.HasProperty("_Color3_2"))
			{
				defPatternColor01 = material.GetColor("_Color3_2");
			}
			if (material.HasProperty("_Glossiness3"))
			{
				defGloss03 = material.GetFloat("_Glossiness3");
			}
			if (material.HasProperty("_Metallic3"))
			{
				defMetallic03 = material.GetFloat("_Metallic3");
			}
			if (material.HasProperty("_patternuv3"))
			{
				defLayout03 = material.GetVector("_patternuv3");
			}
			if (material.HasProperty("_patternuv3Rotator"))
			{
				defRotation03 = material.GetFloat("_patternuv3Rotator");
			}
		}
		if (material.HasProperty("_UVScalePattern"))
		{
			uvScalePattern = material.GetVector("_UVScalePattern");
		}
		if (material.HasProperty("_Color4"))
		{
			defMainColor04 = material.GetColor("_Color4");
		}
		if (material.HasProperty("_Glossiness4"))
		{
			defGloss04 = material.GetFloat("_Glossiness4");
		}
		if (material.HasProperty("_Metallic4"))
		{
			defMetallic04 = material.GetFloat("_Metallic4");
		}
	}

	public override void SetReferenceObject()
	{
		FindAssist findAssist = new FindAssist();
		findAssist.Initialize(base.transform);
		objTopDef = findAssist.GetObjectFromName("n_top_a");
		objTopHalf = findAssist.GetObjectFromName("n_top_b");
		objBotDef = findAssist.GetObjectFromName("n_bot_a");
		objBotHalf = findAssist.GetObjectFromName("n_bot_b");
		objOpt01 = (from x in findAssist.dictObjName
			where x.Key.StartsWith("op1")
			select x.Value).ToArray();
		objOpt02 = (from x in findAssist.dictObjName
			where x.Key.StartsWith("op2")
			select x.Value).ToArray();
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.2.0.10050' (yours is '8.2.0.7535-95108c96')
