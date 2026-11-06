using System.Collections.Generic;
using System.Linq;
using Manager;
using UnityEngine;
using UnityEngine.UI;

namespace ConfigScene;

public class GraphicSetting : BaseSetting
{
	[Header("描画レベルスライダー")]
	[SerializeField]
	private Slider qualitySlider;

	[Header("描画レベルスライダー数字")]
	[SerializeField]
	private Text[] qualityNumberText;

	[Header("数字の色")]
	[SerializeField]
	private Color[] qualitySelectColor;

	[Header("セルフシャドウ")]
	[SerializeField]
	private Toggle selfShadowToggle;

	[Header("被写界深度")]
	[SerializeField]
	private Toggle depthOfFieldToggle;

	[Header("ブルーム")]
	[SerializeField]
	private Toggle bloomToggle;

	[Header("大気表現")]
	[SerializeField]
	private Toggle atmosphereToggle;

	[Header("SSAO")]
	[SerializeField]
	private Toggle ssaoToggle;

	[Header("ビグネット")]
	[SerializeField]
	private Toggle vignetteToggle;

	[Header("SSR")]
	[SerializeField]
	private Toggle ssrToggle;

	[Header("雨の描画")]
	[SerializeField]
	private Toggle rainToggle;

	[Header("キャラ解像度")]
	[SerializeField]
	private Toggle[] charaLevalToggles;

	[Header("マップ解像度")]
	[SerializeField]
	private Toggle[] mapLevalToggles;

	[Header("フェイスライト")]
	[SerializeField]
	private Toggle[] faceLightToggles;

	[Header("環境ライト")]
	[SerializeField]
	private Toggle[] ambientToggles;

	[Header("遮蔽")]
	[SerializeField]
	private Toggle[] shieldToggles;

	[Header("背景色")]
	[SerializeField]
	private UI_SampleColor backGroundCololr;

	[Header("登場人数制限")]
	[SerializeField]
	private Slider charaMaxNumSlider;

	[Header("登場制限")]
	[SerializeField]
	private Toggle Entry0Toggle;

	[SerializeField]
	private Toggle Entry1Toggle;

	[SerializeField]
	private Toggle Entry2Toggle;

	[SerializeField]
	private Toggle Entry3Toggle;

	private bool ChangeSlider = true;

	private bool[][] effectEnables = new bool[4][]
	{
		new bool[8] { true, false, false, false, false, false, false, false },
		new bool[8] { true, false, true, true, true, true, false, false },
		new bool[8] { true, true, true, true, true, true, true, false },
		new bool[8] { true, true, true, true, true, true, true, true }
	};

	private Dictionary<int, List<bool>> easySettingInfo = new Dictionary<int, List<bool>>();

	public override void Init()
	{
		for (int i = 0; i < 4; i++)
		{
			easySettingInfo.Add(i + 1, effectEnables[i].ToList());
		}
		GraphicSystem data = Config.GraphicData;
		LinkToggle(selfShadowToggle, delegate(bool isOn)
		{
			int qualityLevel = QualitySettings.GetQualityLevel() / 2 * 2 + ((!isOn) ? 1 : 0);
			QualitySettings.SetQualityLevel(qualityLevel);
			data.SelfShadow = isOn;
			SetEasySlider();
		});
		LinkToggle(depthOfFieldToggle, delegate(bool isOn)
		{
			data.DepthOfField = isOn;
			SetEasySlider();
		});
		LinkToggle(bloomToggle, delegate(bool isOn)
		{
			data.Bloom = isOn;
			SetEasySlider();
		});
		LinkToggle(atmosphereToggle, delegate(bool isOn)
		{
			data.Atmospheric = isOn;
			SetEasySlider();
		});
		LinkToggle(ssaoToggle, delegate(bool isOn)
		{
			data.SSAO = isOn;
			SetEasySlider();
		});
		LinkToggle(vignetteToggle, delegate(bool isOn)
		{
			data.Vignette = isOn;
			SetEasySlider();
		});
		LinkToggle(ssrToggle, delegate(bool isOn)
		{
			data.SSR = isOn;
			SetEasySlider();
		});
		LinkToggle(rainToggle, delegate(bool isOn)
		{
			data.Rain = isOn;
			SetEasySlider();
		});
		qualitySlider.onValueChanged.AddListener(delegate(float value)
		{
			SetNumberColor((int)value);
			if (!ChangeSlider)
			{
				ChangeSlider = true;
			}
			else
			{
				switch ((int)value)
				{
				case 1:
					data.SelfShadow = true;
					data.DepthOfField = false;
					data.Bloom = false;
					data.Atmospheric = false;
					data.SSAO = false;
					data.Vignette = false;
					data.SSR = false;
					data.Rain = false;
					break;
				case 2:
					data.SelfShadow = true;
					data.DepthOfField = false;
					data.Bloom = true;
					data.Atmospheric = true;
					data.SSAO = true;
					data.Vignette = true;
					data.SSR = false;
					data.Rain = false;
					break;
				case 3:
					data.SelfShadow = true;
					data.DepthOfField = true;
					data.Bloom = true;
					data.Atmospheric = true;
					data.SSAO = true;
					data.Vignette = true;
					data.SSR = true;
					data.Rain = false;
					break;
				case 4:
					data.SelfShadow = true;
					data.DepthOfField = true;
					data.Bloom = true;
					data.Atmospheric = true;
					data.SSAO = true;
					data.Vignette = true;
					data.SSR = true;
					data.Rain = true;
					break;
				}
				UIPresenter();
			}
		});
		LinkToggleArray(charaLevalToggles, delegate(int num)
		{
			data.CharaGraphicQuality = (byte)num;
		});
		LinkToggleArray(mapLevalToggles, delegate(int num)
		{
			data.MapGraphicQuality = (byte)num;
		});
		LinkToggleArray(faceLightToggles, delegate(int num)
		{
			data.FaceLight = num == 0;
		});
		LinkToggleArray(ambientToggles, delegate(int num)
		{
			data.AmbientLight = num == 0;
		});
		LinkToggleArray(shieldToggles, delegate(int num)
		{
			data.Shield = num == 0;
		});
		backGroundCololr.actUpdateColor = delegate(Color c)
		{
			data.BackColor = c;
		};
		LinkSlider(charaMaxNumSlider, delegate(float value)
		{
			data.MaxCharaNum = (int)value;
		});
		LinkToggle(Entry0Toggle, delegate(bool isOn)
		{
			data.CharasEntry[0] = isOn;
		});
		LinkToggle(Entry1Toggle, delegate(bool isOn)
		{
			data.CharasEntry[1] = isOn;
		});
		LinkToggle(Entry2Toggle, delegate(bool isOn)
		{
			data.CharasEntry[2] = isOn;
		});
		LinkToggle(Entry3Toggle, delegate(bool isOn)
		{
			data.CharasEntry[3] = isOn;
		});
		ChangeSlider = true;
	}

	protected override void ValueToUI()
	{
		GraphicSystem data = Config.GraphicData;
		selfShadowToggle.isOn = data.SelfShadow;
		depthOfFieldToggle.isOn = data.DepthOfField;
		bloomToggle.isOn = data.Bloom;
		atmosphereToggle.isOn = data.Atmospheric;
		ssaoToggle.isOn = data.SSAO;
		vignetteToggle.isOn = data.Vignette;
		ssrToggle.isOn = data.SSR;
		rainToggle.isOn = data.Rain;
		SetEasySlider();
		SetToggleUIArray(charaLevalToggles, delegate(Toggle tgl, int index)
		{
			tgl.isOn = index == data.CharaGraphicQuality;
		});
		SetToggleUIArray(mapLevalToggles, delegate(Toggle tgl, int index)
		{
			tgl.isOn = index == data.MapGraphicQuality;
		});
		SetToggleUIArray(faceLightToggles, delegate(Toggle tgl, int index)
		{
			tgl.isOn = ((index != 0) ? (!data.FaceLight) : data.FaceLight);
		});
		SetToggleUIArray(ambientToggles, delegate(Toggle tgl, int index)
		{
			tgl.isOn = ((index != 0) ? (!data.AmbientLight) : data.AmbientLight);
		});
		SetToggleUIArray(shieldToggles, delegate(Toggle tgl, int index)
		{
			tgl.isOn = ((index != 0) ? (!data.Shield) : data.Shield);
		});
		backGroundCololr.SetColor(data.BackColor);
		charaMaxNumSlider.value = data.MaxCharaNum;
		Entry0Toggle.isOn = data.CharasEntry[0];
		Entry1Toggle.isOn = data.CharasEntry[1];
		Entry2Toggle.isOn = data.CharasEntry[2];
		Entry3Toggle.isOn = data.CharasEntry[3];
	}

	private void SetEasySlider()
	{
		GraphicSystem graphicData = Config.GraphicData;
		bool[] array = new bool[8] { graphicData.SelfShadow, graphicData.DepthOfField, graphicData.Bloom, graphicData.Atmospheric, graphicData.SSAO, graphicData.Vignette, graphicData.SSR, graphicData.Rain };
		List<bool> list = new List<bool>();
		int num = array.Length;
		foreach (KeyValuePair<int, List<bool>> item in easySettingInfo)
		{
			int num2 = 0;
			for (int i = 0; i < num; i++)
			{
				if (item.Value[i] && array[i] == item.Value[i])
				{
					num2++;
				}
			}
			list.Add(num2 == item.Value.Count((bool b) => b));
		}
		int num3 = list.FindLastIndex((bool v) => v) + 1;
		if (qualitySlider.value != (float)num3)
		{
			ChangeSlider = false;
			qualitySlider.value = num3;
		}
	}

	private void SetNumberColor(int _value)
	{
		for (int i = 0; i < qualityNumberText.Length; i++)
		{
			Text text = qualityNumberText[i];
			text.color = ((_value != i + 1) ? qualitySelectColor[1] : qualitySelectColor[0]);
		}
	}

	public void EntryInteractable(bool _interactable)
	{
		Entry0Toggle.interactable = _interactable;
		Entry1Toggle.interactable = _interactable;
		Entry2Toggle.interactable = _interactable;
		Entry3Toggle.interactable = _interactable;
	}
}
