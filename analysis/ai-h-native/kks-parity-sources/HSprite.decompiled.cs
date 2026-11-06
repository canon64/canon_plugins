using System;
using System.Collections.Generic;
using System.Linq;
using ChaCustom;
using H;
using HSceneUtility;
using Illusion.Component.UI;
using Illusion.CustomAttributes;
using Illusion.Game;
using IllusionUtility.GetUtility;
using Manager;
using SaveData;
using SceneAssist;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class HSprite : MonoBehaviour
{
	public enum FadeKind
	{
		Out,
		In,
		OutIn
	}

	public enum FadeKindProc
	{
		None,
		Out,
		OutEnd,
		In,
		InEnd,
		OutIn,
		OutInEnd
	}

	public class AnimationInfoComponent : MonoBehaviour
	{
		public HSceneProc.AnimationListInfo info;
	}

	[Serializable]
	public class SpriteBase
	{
		[Label("ID")]
		public int id;

		[Label("親オブジェクト")]
		public GameObject mainObj;
	}

	[Serializable]
	public class AibuSprite : SpriteBase
	{
	}

	[Serializable]
	public class HoushiSprite : SpriteBase
	{
		[Label("アクションボタン(吹き出し)")]
		public HSceneSpriteCategory categoryActionButton;

		[Label("オートフィニッシュ")]
		public Button btAutoFinish;

		public ButtonSpriteChangeCtrl btAutoFinishSpriteCtl;

		[Label("おまかせ状態")]
		public Image imgAuto;

		[Label("パッドImage")]
		public Image imagePad;

		[Label("おまかせ")]
		public Toggle tglRely;

		[Label("パッドImageCtrl")]
		public SpriteChangeCtrl imageCtrlPad;
	}

	[Serializable]
	public class SonyuSprite : SpriteBase
	{
		[Label("アクションボタン(吹き出し)")]
		public HSceneSpriteCategory categoryActionButton;

		[Label("オートフィニッシュ")]
		public Button btAutoFinish;

		public ButtonSpriteChangeCtrl btAutoFinishSpriteCtl;

		[Label("おまかせ状態")]
		public Image imgAuto;

		[Label("パッドImage")]
		public Image imagePad;

		[Label("コンドームボタン")]
		public Button buttonCondom;

		[Label("コンドームのスプライト変更")]
		public ButtonSpriteChangeCtrl buttonCondomChange;

		[Label("コンドームの状態")]
		public bool isCondom;

		[Label("パッドImageCtrl")]
		public SpriteChangeCtrl imageCtrlPad;
	}

	[Serializable]
	public class MasturbationSprite : SpriteBase
	{
	}

	[Serializable]
	public class PeepingSprite : SpriteBase
	{
	}

	[Serializable]
	public class Houshi3PSprite : SpriteBase
	{
		[Label("アクションボタン(吹き出し)")]
		public HSceneSpriteCategory categoryActionButton;

		[Label("オートフィニッシュ")]
		public Button btAutoFinish;

		public ButtonSpriteChangeCtrl btAutoFinishSpriteCtl;

		[Label("おまかせ状態")]
		public Image imgAuto;

		[Label("パッドImage")]
		public Image imagePad;

		[Label("おまかせ")]
		public Toggle tglRely;

		[Label("パッドImageCtrl")]
		public SpriteChangeCtrl imageCtrlPad;
	}

	[Serializable]
	public class Sonyu3PSprite : SpriteBase
	{
		[Label("アクションボタン(吹き出し)")]
		public HSceneSpriteCategory categoryActionButton;

		[Label("オートフィニッシュ")]
		public Button btAutoFinish;

		public ButtonSpriteChangeCtrl btAutoFinishSpriteCtl;

		[Label("おまかせ状態")]
		public Image imgAuto;

		[Label("パッドImage")]
		public Image imagePad;

		[Label("コンドームボタン")]
		public Button buttonCondom;

		[Label("コンドームのスプライト変更")]
		public ButtonSpriteChangeCtrl buttonCondomChange;

		[Label("コンドームの状態")]
		public bool isCondom;

		[Label("パッドImageCtrl")]
		public SpriteChangeCtrl imageCtrlPad;
	}

	[Serializable]
	public class Houshi3PDarkSprite : SpriteBase
	{
		[Label("アクションボタン(吹き出し)")]
		public HSceneSpriteCategory categoryActionButton;

		[Label("オートフィニッシュ")]
		public Button btAutoFinish;

		public ButtonSpriteChangeCtrl btAutoFinishSpriteCtl;

		[Label("おまかせ状態")]
		public Image imgAuto;

		[Label("パッドImage")]
		public Image imagePad;

		[Label("おまかせ")]
		public Toggle tglRely;

		[Label("パッドImageCtrl")]
		public SpriteChangeCtrl imageCtrlPad;
	}

	[Serializable]
	public class Sonyu3PDarkSprite : SpriteBase
	{
		[Label("アクションボタン(吹き出し)")]
		public HSceneSpriteCategory categoryActionButton;

		[Label("オートフィニッシュ")]
		public Button btAutoFinish;

		public ButtonSpriteChangeCtrl btAutoFinishSpriteCtl;

		[Label("おまかせ状態")]
		public Image imgAuto;

		[Label("パッドImage")]
		public Image imagePad;

		[Label("コンドームボタン")]
		public Button buttonCondom;

		[Label("コンドームのスプライト変更")]
		public ButtonSpriteChangeCtrl buttonCondomChange;

		[Label("コンドームの状態")]
		public bool isCondom;

		[Label("パッドImageCtrl")]
		public SpriteChangeCtrl imageCtrlPad;
	}

	[Serializable]
	public class GaugeSprite
	{
		[Label("スライダー")]
		public Image imgBar;

		[Label("ロックToggle")]
		public Toggle tglLock;
	}

	[Serializable]
	public class Axis
	{
		public HSceneGuideObject guideObj;

		public HSceneSpriteObjectCategory categoryAxis;

		public Slider sliderSpeed;

		public Slider sliderScale;

		public Toggle tglDraw;

		public Image imgDisable;
	}

	[Serializable]
	public class LightSlider
	{
		public Slider r;

		public Slider g;

		public Slider b;

		public Slider a;

		public Slider x;

		public Slider y;
	}

	[Serializable]
	public class MoveAxizInteractive
	{
		public List<Toggle> lstTgl = new List<Toggle>();

		public List<Button> lstBtn = new List<Button>();

		public List<Slider> lstSld = new List<Slider>();

		public void SetInteractive(bool _interactive)
		{
			lstTgl.ForEach(delegate(Toggle t)
			{
				t.interactable = _interactive;
			});
			lstBtn.ForEach(delegate(Button b)
			{
				b.interactable = _interactive;
			});
			lstSld.ForEach(delegate(Slider s)
			{
				s.interactable = _interactive;
			});
		}
	}

	[Serializable]
	public class MultipleFemaleSubGroup
	{
		public List<GameObject> lstGroup = new List<GameObject>();
	}

	[Serializable]
	public class FemaleDressButtonCategory
	{
		public HSceneSpriteCategory dress;

		public HSceneSpriteCategory dressAll;

		public HSceneSpriteCategory accessory;

		public HSceneSpriteCategory accessoryAll;

		public HSceneSpriteCategory coordinate;
	}

	private enum VoicePlayShuffleKind
	{
		ActionChange,
		PoseChange
	}

	private delegate bool AutoFinishDelegate(bool _force = false);

	private delegate bool SpriteStateProcDelegate();

	[Label("フラグ管理スクリプト")]
	public HFlag flags;

	[Label("スピードスライダー親オブジェクト")]
	public GameObject objSpeed;

	[Label("スピードスライダー愛撫下地オブジェクト")]
	public GameObject objSpeedAibuBase;

	[Label("スピードスライダーバーimage")]
	public Image imageSpeed;

	[Label("HandCtrlスクリプト")]
	public HandCtrl hand;

	public AibuSprite aibu;

	public HoushiSprite houshi;

	public SonyuSprite sonyu;

	public MasturbationSprite masturbation;

	public PeepingSprite peeping;

	public Houshi3PSprite houshi3P;

	public Sonyu3PSprite sonyu3P;

	public HoushiSprite houshi3PDark;

	public SonyuSprite sonyu3PDark;

	public RawImage[] commonAibuIcons = new RawImage[3];

	public GaugeSprite[] gauge = new GaugeSprite[2];

	[Label("メインメニューHSceneSpriteCategory")]
	public HSceneSpriteCategory menuMain;

	public ButtonSpriteChangeCtrl[] menuMainBtChanges;

	[Label("アクションメニューHSceneSpriteCategory")]
	public HSceneSpriteCategory menuAction;

	public ButtonSpriteChangeCtrl[] menuActionBtChanges;

	[Label("サブメニューHSceneSpriteCategory")]
	public HSceneSpriteObjectCategory menuSub;

	[Label("アクションサブメニューHSceneSpriteCategory")]
	public HSceneSpriteSubMenuCategory menuActionSub;

	public GameObject objMenuSubMaleTop;

	public GameObject objMenuSubMale;

	public GameObject objMenuSubFemale;

	public GameObject[] objMenuSubMaleSubs = new GameObject[3];

	public GameObject[] objMenuSubFemaleSubs = new GameObject[3];

	[Label("服装HSceneSpriteCategory")]
	public HSceneSpriteCategoryParent categoryDress;

	public HSceneSpriteCategoryParent categoryDressMale;

	[Label("服HSceneSpriteCategory")]
	public HSceneSpriteCategory categoryCloth;

	[Label("服まとめHSceneSpriteCategory")]
	public HSceneSpriteCategory categoryClothAll;

	[Label("アクセサリーHSceneSpriteCategory")]
	public HSceneSpriteCategory categoryAccessory;

	[Label("アクセサリーまとめHSceneSpriteCategory")]
	public HSceneSpriteCategory categoryAccessoryAll;

	public SelectUI[] selectUIPads;

	[Label("奉仕行為Toggleカテゴリー")]
	public HSceneSpriteToggleCategory categoryToggleHoushi;

	[Label("モーションリストNodeオブジェクト")]
	public GameObject objMotionListNode;

	[Label("Hシーン終了ボタン")]
	public Button btnEnd;

	[Label("愛撫時の7割スライダーcover")]
	public Image imageSpeedSlliderCover70;

	public Axis axis;

	public clothesFileControl clothCusutomCtrl;

	public bool clothCusutomOpen;

	public LightSlider lightSlider = new LightSlider();

	[Label("愛撫アイコンの親オブジェクト")]
	public GameObject objCommonAibuIcon;

	[Label("愛撫アイコンカテゴリー")]
	public HSceneSpriteObjectCategory categoryAibuIcon;

	public MoveAxizInteractive moveAxizInteractive = new MoveAxizInteractive();

	[Label("初HHelp親オブジェクト")]
	public GameObject objFirstHHelpBase;

	[Label("ヘルプ自動消去")]
	public HSpriteAutoDisable autoDisableFirstHHelp;

	[Label("生理周期カテゴリー")]
	public HSceneSpriteObjectCategory categoryMenstruation;

	[Label("慣れカテゴリー")]
	public HSceneSpriteObjectCategory categoryExperience;

	[Label("顔RawImage")]
	public RawImage rawFace;

	[Label("MainMenuの親オブジェクト")]
	public GameObject objBaseMainMenu;

	[Label("MainMenuOpenButtonObject")]
	public GameObject objMainMenuOpen;

	[Label("AibuItemIconOpenオブジェクト")]
	public GameObject objAibuItemIconOpen;

	public List<HSceneSpriteCategoryParent> lstMultipleFemaleGroup = new List<HSceneSpriteCategoryParent>();

	public List<MultipleFemaleSubGroup> lstMultipleFemaleSubGroup = new List<MultipleFemaleSubGroup>();

	public List<FemaleDressButtonCategory> lstMultipleFemaleDressButton = new List<FemaleDressButtonCategory>();

	public GameObject MultipleMaleTop;

	public HSceneSpriteCategoryParent MultipleMenuSubMale;

	public GameObject[] MultipleMenuSubMaleSubs = new GameObject[3];

	[Header("サブメニューのOn/Off")]
	public ButtonSpriteChangeCtrl[] BtSpriteSub;

	public ButtonSpriteChangeCtrl[] BtSpriteMaleSub;

	public ButtonSpriteChangeCtrl[] BtSpriteMaleCoodeSub;

	public ButtonSpriteChangeCtrl[] BtSpriteFemaleSub;

	public ButtonSpriteChangeCtrl[] BtSpriteFemaleCoodeSub;

	public ButtonSpriteChangeCtrl[] BtSpriteMultiSub;

	public ButtonSpriteChangeCtrl[] BtSpriteMultiMaleSub;

	public ButtonSpriteChangeCtrl[] BtSpriteMultiMaleCoodeSub;

	public ButtonSpriteChangeCtrl[] BtSpriteMultiFemaleSub;

	public ButtonSpriteChangeCtrl[] BtSpriteMultiFemaleCoodeSub;

	public ButtonSpriteChangeCtrl[] BtSpriteMultiFemaleSub1;

	public ButtonSpriteChangeCtrl[] BtSpriteMultiFemaleCoodeSub1;

	[Label("イキそうボタン親")]
	public GameObject objImmediatelyFinish;

	[Label("イキそうボタン女")]
	public Button btnImmediatelyFinishFemale;

	[Label("イキそうボタン男")]
	public Button btnImmediatelyFinishMale;

	[Label("乱入ボタン")]
	public Button btnTrespassing;

	[Label("乱入ボタンImage変更スクリプト")]
	public SpriteTransitionChangeCtrl helpSpriteTrespassing;

	[Label("乱入ボタンText変更スクリプト")]
	public TextChangeCtrl helpTextTrespassing;

	[Label("乱入ボタンヘルプ自動消去")]
	public HSpriteAutoDisable autoDisableTrespassingHelp;

	public UnityAction<int> localMoveInitAction;

	[Header("フェード")]
	[Label("フェード用RawImage")]
	public RawImage imageFade;

	[Label("基本フェード時間")]
	public float timeFadeBase;

	[Label("フェード処理をしてるか")]
	public bool isFade;

	[Label("フェードアニメーション")]
	public AnimationCurve fadeAnimation;

	private List<ChaControl> females = new List<ChaControl>();

	private ChaControl male;

	private Heroine heroine;

	private AutoRely rely;

	private List<int> lstCategory;

	private List<HSceneProc.AnimationListInfo>[] lstUseAnimInfo = new List<HSceneProc.AnimationListInfo>[8];

	private List<HSceneProc.AnimationListInfo>[] lstHoushiAnimInfo = new List<HSceneProc.AnimationListInfo>[3];

	private List<HSceneProc.AnimationListInfo>[] lst3PHoushiAnimInfo = new List<HSceneProc.AnimationListInfo>[3];

	[SerializeField]
	private List<int> lstHoushiPose = new List<int>();

	private FadeKind kindFade;

	private FadeKindProc kindFadeProc;

	private float timeFade;

	private float timeFadeTime;

	private ReactiveProperty<Heroine> resultPeepingNode = new ReactiveProperty<Heroine>();

	private HSceneProc.LightData lightData;

	private ShuffleRand[] voicePlayShuffle = new ShuffleRand[5];

	protected HVoiceCtrl voice;

	private void Start()
	{
		menuActionSub.SetAction(OnMouseDownSlider);
		axis.guideObj.SetScale();
		for (int i = 0; i < lstMultipleFemaleGroup.Count; i++)
		{
			HSceneSpriteCategoryParent hSceneSpriteCategoryParent = lstMultipleFemaleGroup[i];
			int female = i;
			int count = hSceneSpriteCategoryParent.GetCount();
			for (int j = 0; j < count; j++)
			{
				int num = j;
				hSceneSpriteCategoryParent.SetAction(j, delegate
				{
					OnFemaleDressSubMenu(female, num);
				});
			}
		}
		for (int num2 = 0; num2 < voicePlayShuffle.Length; num2++)
		{
			voicePlayShuffle[num2] = new ShuffleRand();
			voicePlayShuffle[num2].Init(2);
		}
	}

	private void Update()
	{
		if (flags.mode != HFlag.EMode.none)
		{
			if ((bool)imageSpeed)
			{
				if (flags.mode == HFlag.EMode.aibu)
				{
					imageSpeed.fillAmount = Mathf.InverseLerp(0f, flags.speedMaxAibuBody, flags.speed);
				}
				else
				{
					imageSpeed.fillAmount = flags.speedCalc;
				}
			}
			(new SpriteStateProcDelegate[10] { AibuProc, HoushiProc, SonyuProc, MasturbationProc, PeepingProc, LesbianProc, Houshi3PProc, Sonyu3PProc, Houshi3PDarkProc, Sonyu3PDarkProc })[(int)flags.mode]();
		}
		if (flags.lstHeroine.Count != 0 && flags.lstHeroine[0].hCount == 0)
		{
			bool flag = flags.count.sonyuOrg != 0 || flags.count.sonyuOutside + flags.count.sonyuInside + flags.count.sonyuCondomInside != 0;
			if (flags.mode == HFlag.EMode.masturbation || flags.mode == HFlag.EMode.peeping || flags.mode == HFlag.EMode.lesbian || flags.isFreeH || flags.isDebug)
			{
				flag = true;
			}
			if (btnEnd.gameObject.activeSelf != flag)
			{
				btnEnd.gameObject.SetActive(flag);
			}
		}
		if ((bool)gauge[0].imgBar)
		{
			gauge[0].imgBar.fillAmount = Mathf.InverseLerp(0f, 100f, flags.gaugeFemale);
		}
		if ((bool)gauge[1].imgBar)
		{
			gauge[1].imgBar.fillAmount = Mathf.InverseLerp(0f, 100f, flags.gaugeMale);
		}
		for (int i = 0; i < females.Count; i++)
		{
			int num = 0;
			for (int j = 0; j < 7; j++)
			{
				if (females[i].IsClothesStateKind(j))
				{
					num++;
				}
			}
			if (females[i].IsShoesStateKind())
			{
				num++;
			}
			if (females.Count == 1)
			{
				categoryDress.SetActiveWithParent(num != 0, 0);
			}
			else if (females.Count > 1)
			{
				lstMultipleFemaleGroup[i].SetActiveWithParent(num != 0, 0);
			}
			int num2 = 0;
			for (int k = 0; k < 20; k++)
			{
				if (females[i].IsAccessory(k))
				{
					num2++;
				}
			}
			if (females.Count == 1)
			{
				categoryDress.SetActiveWithParent(num2 != 0, 1);
			}
			else if (females.Count > 1)
			{
				lstMultipleFemaleGroup[i].SetActiveWithParent(num2 != 0, 1);
			}
		}
		if (male != null)
		{
			int num3 = 0;
			for (int l = 0; l < 20; l++)
			{
				if (male.IsAccessory(l))
				{
					num3++;
				}
			}
			if (females.Count == 1)
			{
				categoryDressMale.SetActiveWithParent(num3 != 0, 1);
			}
			else if (females.Count > 1)
			{
				MultipleMenuSubMale.SetActiveWithParent(num3 != 0, 1);
			}
		}
		if ((bool)objImmediatelyFinish && objImmediatelyFinish.activeSelf != Manager.Config.HData.immediatelyFinish)
		{
			objImmediatelyFinish.SetActive(Manager.Config.HData.immediatelyFinish);
		}
		FadeProc();
	}

	public bool IsCursorOnPad()
	{
		return selectUIPads.Any((SelectUI s) => s.isSelect);
	}

	public void CloseAllMenu()
	{
		for (int i = 0; i < menuSub.GetCount(); i++)
		{
			if (menuSub.GetActive(i))
			{
				menuSub.SetActive(_active: false, i);
			}
		}
		for (int j = 0; j < menuMainBtChanges.Length; j++)
		{
			menuMainBtChanges[j].OnChangeValue(_isOn: false);
		}
		objMenuSubMale.SetActive(value: false);
		objMenuSubFemale.SetActive(value: false);
		GameObject[] array = objMenuSubMaleSubs;
		for (int k = 0; k < array.Length; k++)
		{
			array[k].SetActive(value: false);
		}
		array = objMenuSubFemaleSubs;
		for (int k = 0; k < array.Length; k++)
		{
			array[k].SetActive(value: false);
		}
		foreach (HSceneSpriteCategoryParent item in lstMultipleFemaleGroup)
		{
			item.gameObject.SetActive(value: false);
		}
		MultipleMenuSubMale.gameObject.SetActive(value: false);
		foreach (MultipleFemaleSubGroup item2 in lstMultipleFemaleSubGroup)
		{
			foreach (GameObject item3 in item2.lstGroup)
			{
				item3.SetActive(value: false);
			}
		}
		array = MultipleMenuSubMaleSubs;
		for (int k = 0; k < array.Length; k++)
		{
			array[k].SetActive(value: false);
		}
	}

	public void CloseMotionSubMenu()
	{
		menuSub.SetActive(_active: false, 0);
		menuSub.SetActive(_active: false, 1);
		menuSub.SetActive(_active: false, 2);
		menuSub.SetActive(_active: false, 3);
		menuSub.SetActive(_active: false, 4);
		for (int i = 0; i < menuMainBtChanges.Length; i++)
		{
			menuMainBtChanges[i].OnChangeValue(_isOn: false);
		}
	}

	public void ForceCloseAllMenu()
	{
		for (int i = 0; i < menuSub.GetCount(); i++)
		{
			menuSub.SetActive(_active: false);
		}
		for (int j = 0; j < menuMainBtChanges.Length; j++)
		{
			menuMainBtChanges[j].OnChangeValue(_isOn: false);
		}
		objMenuSubMale.SetActive(value: false);
		objMenuSubFemale.SetActive(value: false);
		GameObject[] array = objMenuSubMaleSubs;
		for (int k = 0; k < array.Length; k++)
		{
			array[k].SetActive(value: false);
		}
		array = objMenuSubFemaleSubs;
		for (int k = 0; k < array.Length; k++)
		{
			array[k].SetActive(value: false);
		}
		foreach (HSceneSpriteCategoryParent item in lstMultipleFemaleGroup)
		{
			item.gameObject.SetActive(value: false);
		}
		MultipleMenuSubMale.gameObject.SetActive(value: false);
		foreach (MultipleFemaleSubGroup item2 in lstMultipleFemaleSubGroup)
		{
			foreach (GameObject item3 in item2.lstGroup)
			{
				item3.SetActive(value: false);
			}
		}
		array = MultipleMenuSubMaleSubs;
		for (int k = 0; k < array.Length; k++)
		{
			array[k].SetActive(value: false);
		}
	}

	public void OnSpeedUpClick()
	{
		if (!IsSpriteAciotn())
		{
			return;
		}
		if (Input.GetMouseButtonDown(0))
		{
			flags.click = HFlag.ClickKind.speedup;
			AnimatorStateInfo animatorStateInfo = females[0].getAnimatorStateInfo(0);
			if ((flags.mode == HFlag.EMode.houshi || flags.mode == HFlag.EMode.houshi3P || flags.mode == HFlag.EMode.houshi3PMMF) && animatorStateInfo.IsName("Idle"))
			{
				flags.voiceWait = true;
			}
		}
		if (Input.GetMouseButtonDown(1))
		{
			flags.click = HFlag.ClickKind.motionchange;
		}
		if (Input.GetMouseButtonDown(2))
		{
			flags.click = HFlag.ClickKind.modeChange;
		}
	}

	public void OnInsertClick()
	{
		if (!Input.GetMouseButtonUp(0) || !IsSpriteAciotn())
		{
			return;
		}
		int num = 0;
		int num2 = ((flags.mode == HFlag.EMode.houshi3P || flags.mode == HFlag.EMode.sonyu3P) ? (flags.nowAnimationInfo.id % 2) : 0);
		num = ((flags.mode == HFlag.EMode.sonyu3P) ? (flags.nowAnimationInfo.isFemaleInitiative ? 538 : 500) : (flags.nowAnimationInfo.isFemaleInitiative ? 38 : 0));
		if (flags.mode != HFlag.EMode.sonyu3PMMF)
		{
			if (flags.isInsertOK[num2] || flags.isDebug)
			{
				flags.click = HFlag.ClickKind.insert;
				flags.voice.playVoices[num2] = 301 + num;
				Utils.Sound.Play(SystemSE.sel);
			}
			else if (flags.isCondom)
			{
				flags.click = HFlag.ClickKind.insert;
				flags.voice.playVoices[num2] = 301 + num;
				Utils.Sound.Play(SystemSE.sel);
			}
			else
			{
				flags.AddNotCondomPlay();
				flags.voice.playVoices[num2] = 302 + num;
				flags.voice.SetSonyuIdleTime();
				flags.isDenialvoiceWait = true;
			}
		}
		else
		{
			flags.click = HFlag.ClickKind.insert;
			flags.voice.playVoices[num2] = 1001;
			Utils.Sound.Play(SystemSE.sel);
		}
		if (flags.mode == HFlag.EMode.houshi3P || flags.mode == HFlag.EMode.sonyu3P)
		{
			int num3 = num2 ^ 1;
			if (voice.nowVoices[num3].state == HVoiceCtrl.VoiceKind.voice && Voice.IsPlay(flags.transVoiceMouth[num3]))
			{
				Voice.Stop(flags.transVoiceMouth[num3]);
			}
		}
	}

	public void OnInsertNoVoiceClick()
	{
		if (!Input.GetMouseButtonUp(0) || !IsSpriteAciotn())
		{
			return;
		}
		int num = ((flags.mode == HFlag.EMode.houshi3P || flags.mode == HFlag.EMode.sonyu3P) ? (flags.nowAnimationInfo.id % 2) : 0);
		if (flags.mode != HFlag.EMode.sonyu3PMMF)
		{
			if (flags.isInsertOK[num] || flags.isDebug)
			{
				flags.click = HFlag.ClickKind.insert_voice;
				Utils.Sound.Play(SystemSE.sel);
				return;
			}
			if (flags.isCondom)
			{
				flags.click = HFlag.ClickKind.insert_voice;
				Utils.Sound.Play(SystemSE.sel);
				return;
			}
			flags.AddNotCondomPlay();
			int num2 = 0;
			num2 = ((flags.mode == HFlag.EMode.sonyu3P) ? (flags.nowAnimationInfo.isFemaleInitiative ? 538 : 500) : (flags.nowAnimationInfo.isFemaleInitiative ? 38 : 0));
			flags.voice.playVoices[num] = 302 + num2;
			flags.voice.SetSonyuIdleTime();
			flags.isDenialvoiceWait = true;
			if (flags.mode == HFlag.EMode.houshi3P || flags.mode == HFlag.EMode.sonyu3P)
			{
				int num3 = num ^ 1;
				if (voice.nowVoices[num3].state == HVoiceCtrl.VoiceKind.voice && Voice.IsPlay(flags.transVoiceMouth[num3]))
				{
					Voice.Stop(flags.transVoiceMouth[num3]);
				}
			}
		}
		else
		{
			flags.click = HFlag.ClickKind.insert_voice;
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnInsertAnalClick()
	{
		if (!Input.GetMouseButtonUp(0) || !IsSpriteAciotn())
		{
			return;
		}
		int num = ((flags.mode == HFlag.EMode.houshi3P || flags.mode == HFlag.EMode.sonyu3P) ? (flags.nowAnimationInfo.id % 2) : 0);
		int num2 = 0;
		num2 = ((flags.mode == HFlag.EMode.sonyu3P) ? (flags.nowAnimationInfo.isFemaleInitiative ? 538 : 500) : (flags.nowAnimationInfo.isFemaleInitiative ? 38 : 0));
		if (flags.isAnalInsertOK || flags.isDebug)
		{
			flags.click = HFlag.ClickKind.insert_anal;
			flags.voice.playVoices[num] = 304 + num2;
			Utils.Sound.Play(SystemSE.sel);
		}
		else
		{
			flags.AddNotAnalPlay();
			flags.voice.playVoices[num] = 305 + num2;
			flags.voice.SetSonyuIdleTime();
		}
		if (flags.mode == HFlag.EMode.houshi3P || flags.mode == HFlag.EMode.sonyu3P)
		{
			int num3 = num ^ 1;
			if (voice.nowVoices[num3].state == HVoiceCtrl.VoiceKind.voice && Voice.IsPlay(flags.transVoiceMouth[num3]))
			{
				Voice.Stop(flags.transVoiceMouth[num3]);
			}
		}
	}

	public void OnInsertAnalNoVoiceClick()
	{
		if (!Input.GetMouseButtonUp(0) || !IsSpriteAciotn())
		{
			return;
		}
		if (flags.isAnalInsertOK || flags.isDebug)
		{
			flags.click = HFlag.ClickKind.insert_anal_voice;
			Utils.Sound.Play(SystemSE.sel);
			return;
		}
		flags.AddNotAnalPlay();
		int num = ((flags.mode == HFlag.EMode.houshi3P || flags.mode == HFlag.EMode.sonyu3P) ? (flags.nowAnimationInfo.id % 2) : 0);
		int num2 = 0;
		num2 = ((flags.mode == HFlag.EMode.sonyu3P) ? (flags.nowAnimationInfo.isFemaleInitiative ? 538 : 500) : (flags.nowAnimationInfo.isFemaleInitiative ? 38 : 0));
		flags.voice.playVoices[num] = 305 + num2;
		flags.voice.SetSonyuIdleTime();
		if (flags.mode == HFlag.EMode.houshi3P || flags.mode == HFlag.EMode.sonyu3P)
		{
			int num3 = num ^ 1;
			if (voice.nowVoices[num3].state == HVoiceCtrl.VoiceKind.voice && Voice.IsPlay(flags.transVoiceMouth[num3]))
			{
				Voice.Stop(flags.transVoiceMouth[num3]);
			}
		}
	}

	public void OnPullClick()
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			flags.click = HFlag.ClickKind.pull;
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnAutoFinish()
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			switch (flags.mode)
			{
			case HFlag.EMode.houshi:
				houshi.btAutoFinishSpriteCtl.OnChangeValue(!houshi.btAutoFinishSpriteCtl.now);
				break;
			case HFlag.EMode.sonyu:
				sonyu.btAutoFinishSpriteCtl.OnChangeValue(!sonyu.btAutoFinishSpriteCtl.now);
				break;
			case HFlag.EMode.houshi3P:
				houshi3P.btAutoFinishSpriteCtl.OnChangeValue(!houshi3P.btAutoFinishSpriteCtl.now);
				break;
			case HFlag.EMode.sonyu3P:
				sonyu3P.btAutoFinishSpriteCtl.OnChangeValue(!sonyu3P.btAutoFinishSpriteCtl.now);
				break;
			case HFlag.EMode.houshi3PMMF:
				houshi3PDark.btAutoFinishSpriteCtl.OnChangeValue(!houshi3PDark.btAutoFinishSpriteCtl.now);
				break;
			case HFlag.EMode.sonyu3PMMF:
				sonyu3PDark.btAutoFinishSpriteCtl.OnChangeValue(!sonyu3PDark.btAutoFinishSpriteCtl.now);
				break;
			}
			AutoFinishDelegate[] array = new AutoFinishDelegate[10] { null, SetHoushiAutoFinish, null, null, null, null, SetHoushi3PAutoFinish, null, SetHoushi3PDarkAutoFinish, null };
			if (array[(int)flags.mode] != null)
			{
				array[(int)flags.mode]();
			}
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnCondomClick()
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			CondomClick();
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void CondomClick()
	{
		if (flags.mode == HFlag.EMode.sonyu)
		{
			SetCondom(!sonyu.isCondom);
			flags.isCondom = sonyu.isCondom;
		}
		else if (flags.mode == HFlag.EMode.sonyu3P)
		{
			SetCondom3P(!sonyu3P.isCondom);
			flags.isCondom = sonyu3P.isCondom;
		}
		if ((bool)male && male.chaFile != null)
		{
			male.chaFile.status.visibleGomu = flags.isCondom;
		}
	}

	public void OnInsideClick()
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			flags.voiceWait = true;
			flags.click = HFlag.ClickKind.inside;
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnOutsideClick()
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			flags.voiceWait = true;
			flags.click = HFlag.ClickKind.outside;
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnFastMouseDown()
	{
		if (Input.GetMouseButtonDown(0) && IsSpriteAciotn())
		{
			flags.speedHoushi = 1;
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnSlowMouseDown()
	{
		if (Input.GetMouseButtonDown(0) && IsSpriteAciotn())
		{
			flags.speedHoushi = 2;
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnSpeedMouseUp()
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			flags.speedHoushi = 0;
		}
	}

	public void OnRelyClick(bool _isON)
	{
		if (IsSpriteAciotn())
		{
			flags.rely = _isON;
			if (!_isON)
			{
				rely.InitTimer();
			}
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnDrinkClick()
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			flags.click = HFlag.ClickKind.drink;
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnRePlayClick()
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			flags.click = HFlag.ClickKind.again;
			flags.voiceWait = true;
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnVomitClick()
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			flags.click = HFlag.ClickKind.vomit;
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnFemaleGaugeLock(bool _isON)
	{
		if (IsSpriteAciotn())
		{
			flags.lockGugeFemale = _isON;
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnFemaleGaugeLockOnGauge()
	{
		gauge[0].tglLock.isOn = !gauge[0].tglLock.isOn;
	}

	public void OnMaleGaugeLock(bool _isON)
	{
		if (IsSpriteAciotn())
		{
			flags.lockGugeMale = _isON;
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnMaleGaugeLockOnGauge()
	{
		gauge[1].tglLock.isOn = !gauge[1].tglLock.isOn;
	}

	public void OnClickPeepingRestart()
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			flags.click = HFlag.ClickKind.peeping_restart;
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnMainMenu(int _kind)
	{
		if (!Input.GetMouseButtonUp(0) || !IsSpriteAciotn())
		{
			return;
		}
		if (_kind < 2)
		{
			for (int i = 0; i < menuMainBtChanges.Length; i++)
			{
				menuMainBtChanges[i].OnChangeValue(i == _kind && !menuMainBtChanges[_kind].now);
			}
		}
		else if (_kind == 3)
		{
			for (int j = 0; j < menuMainBtChanges.Length; j++)
			{
				menuMainBtChanges[j].OnChangeValue(j == 2 && !menuMainBtChanges[2].now);
			}
		}
		bool active = flags.mode == HFlag.EMode.aibu || flags.nowAnimationInfo.paramMale.path.file != "";
		bool flag = flags.mode != HFlag.EMode.lesbian && flags.mode != HFlag.EMode.houshi3P && flags.mode != HFlag.EMode.sonyu3P;
		int num = ((_kind == 0 && (flags.mode == HFlag.EMode.lesbian || flags.mode == HFlag.EMode.houshi3P || flags.mode == HFlag.EMode.sonyu3P)) ? 4 : _kind);
		if (!menuSub.IsEmpty(num))
		{
			if (menuSub.GetActive(num))
			{
				menuSub.SetActive(_active: false);
			}
			else
			{
				menuSub.SetActiveToggle(num);
				if (flag)
				{
					objMenuSubMaleTop.SetActive(active);
				}
				else
				{
					MultipleMaleTop.SetActive(active);
				}
				MainMenuProc(num);
				Utils.Sound.Play(SystemSE.sel);
			}
		}
		else
		{
			MainMenuProc(num);
		}
		objMenuSubMale.SetActive(value: false);
		objMenuSubFemale.SetActive(value: false);
		GameObject[] array = objMenuSubMaleSubs;
		for (int k = 0; k < array.Length; k++)
		{
			array[k].SetActive(value: false);
		}
		array = objMenuSubFemaleSubs;
		for (int k = 0; k < array.Length; k++)
		{
			array[k].SetActive(value: false);
		}
		foreach (HSceneSpriteCategoryParent item in lstMultipleFemaleGroup)
		{
			item.gameObject.SetActive(value: false);
		}
		MultipleMenuSubMale.gameObject.SetActive(value: false);
		foreach (MultipleFemaleSubGroup item2 in lstMultipleFemaleSubGroup)
		{
			foreach (GameObject item3 in item2.lstGroup)
			{
				item3.SetActive(value: false);
			}
		}
		array = MultipleMenuSubMaleSubs;
		for (int k = 0; k < array.Length; k++)
		{
			array[k].SetActive(value: false);
		}
		ButtonSpriteChangeCtrl[] btSpriteSub = BtSpriteSub;
		for (int k = 0; k < btSpriteSub.Length; k++)
		{
			btSpriteSub[k].OnChangeValue(_isOn: false);
		}
		btSpriteSub = BtSpriteMultiSub;
		for (int k = 0; k < btSpriteSub.Length; k++)
		{
			btSpriteSub[k].OnChangeValue(_isOn: false);
		}
	}

	public void OnActionMenu(int _kind)
	{
		if (!Input.GetMouseButtonUp(0) || !IsSpriteAciotn())
		{
			return;
		}
		if (_kind != 3)
		{
			for (int i = 0; i < menuActionBtChanges.Length; i++)
			{
				menuActionBtChanges[i].OnChangeValue(i == _kind && !menuActionBtChanges[i].now);
			}
			CreateMotionList(_kind);
			return;
		}
		for (int j = 0; j < menuActionBtChanges.Length; j++)
		{
			if (menuActionBtChanges[j].now)
			{
				menuActionBtChanges[j].OnChangeValue(_isOn: false);
			}
		}
		autoDisableTrespassingHelp.ForceFlag();
		if (!autoDisableTrespassingHelp.gameObject.activeSelf)
		{
			RectTransform rectTransform = btnTrespassing.transform.parent.gameObject.transform as RectTransform;
			RectTransform obj = menuActionSub.GetObject(7).transform as RectTransform;
			Vector2 anchoredPosition = obj.anchoredPosition;
			anchoredPosition.y = rectTransform.anchoredPosition.y;
			obj.anchoredPosition = anchoredPosition;
			menuActionSub.SetActiveToggle(7);
			Utils.Sound.Play(SystemSE.sel);
		}
		else
		{
			menuActionSub.SetActive(_active: false, 7);
		}
	}

	public void OnActionHoushiMenuHand(bool _isON)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			OnActionHoushiMenu(_isON, 0);
		}
	}

	public void OnActionHoushiMenuMouth(bool _isON)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			OnActionHoushiMenu(_isON, 1);
		}
	}

	public void OnActionHoushiMenuBreast(bool _isON)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			OnActionHoushiMenu(_isON, 2);
		}
	}

	public void OnActionHoushiMenu(bool _isON, int _kind)
	{
		if (!menuActionSub.GetActive(_kind + 1) && _isON)
		{
			for (int i = 1; i < 4; i++)
			{
				if (!menuActionSub.IsEmpty(i))
				{
					menuActionSub.SetActive(_kind + 1 == i, i);
				}
			}
			LoadMotionList(lstHoushiAnimInfo[_kind], menuActionSub.GetPutParent(_kind + 1));
			GameObject obj = menuActionSub.GetObject(4);
			GameObject gameObject = categoryToggleHoushi.GetObject(_kind);
			RectTransform rectTransform = obj.transform as RectTransform;
			RectTransform rectTransform2 = gameObject.transform as RectTransform;
			gameObject = menuActionSub.GetObject(_kind + 1);
			RectTransform obj2 = gameObject.transform as RectTransform;
			Vector2 anchoredPosition = obj2.anchoredPosition;
			anchoredPosition.y = rectTransform2.anchoredPosition.y + rectTransform.anchoredPosition.y;
			obj2.anchoredPosition = anchoredPosition;
			Utils.Sound.Play(SystemSE.sel);
		}
		else
		{
			menuActionSub.SetActive(_active: false, _kind + 1);
		}
	}

	public void OnSubMenu(int _kind)
	{
		if (!Input.GetMouseButtonUp(0) || !IsSpriteAciotn())
		{
			return;
		}
		Animator component = objMenuSubFemaleSubs[_kind].GetComponent<Animator>();
		if (objMenuSubFemaleSubs[_kind].activeSelf)
		{
			component.SetBool("on", value: false);
			component.GetBehaviour<MenuStateMachineBehaviour>().obj = objMenuSubFemaleSubs[_kind];
			for (int i = 0; i < BtSpriteFemaleSub.Length; i++)
			{
				BtSpriteFemaleSub[i].OnChangeValue(_isOn: false);
			}
			for (int j = 0; j < BtSpriteFemaleCoodeSub.Length; j++)
			{
				BtSpriteFemaleCoodeSub[j].OnChangeValue(_isOn: false);
			}
		}
		else
		{
			objMenuSubFemaleSubs[_kind].SetActive(value: true);
			for (int k = 0; k < objMenuSubFemaleSubs.Length; k++)
			{
				if (k != _kind)
				{
					objMenuSubFemaleSubs[k].SetActive(value: false);
				}
			}
			component.SetBool("on", value: true);
			SubMenuProc(_kind);
			Utils.Sound.Play(SystemSE.sel);
			for (int l = 0; l < BtSpriteFemaleSub.Length; l++)
			{
				BtSpriteFemaleSub[l].OnChangeValue(l == _kind);
			}
			int coordinateType = females[0].fileStatus.coordinateType;
			for (int m = 0; m < BtSpriteFemaleCoodeSub.Length; m++)
			{
				BtSpriteFemaleCoodeSub[m].OnChangeValue(m == coordinateType);
			}
		}
		for (int n = 0; n < BtSpriteMaleSub.Length; n++)
		{
			BtSpriteMaleSub[n].OnChangeValue(_isOn: false);
		}
		for (int num = 0; num < BtSpriteMaleCoodeSub.Length; num++)
		{
			BtSpriteMaleCoodeSub[num].OnChangeValue(_isOn: false);
		}
	}

	public void OnSubMenuMale(int _kind)
	{
		if (!Input.GetMouseButtonUp(0) || !IsSpriteAciotn())
		{
			return;
		}
		Animator component = objMenuSubMaleSubs[_kind].GetComponent<Animator>();
		if (objMenuSubMaleSubs[_kind].activeSelf)
		{
			component.SetBool("on", value: false);
			component.GetBehaviour<MenuStateMachineBehaviour>().obj = objMenuSubMaleSubs[_kind];
			for (int i = 0; i < BtSpriteMaleSub.Length; i++)
			{
				BtSpriteMaleSub[i].OnChangeValue(_isOn: false);
			}
			for (int j = 0; j < BtSpriteMaleCoodeSub.Length; j++)
			{
				BtSpriteMaleCoodeSub[j].OnChangeValue(_isOn: false);
			}
		}
		else
		{
			objMenuSubMaleSubs[_kind].SetActive(value: true);
			for (int k = 0; k < objMenuSubMaleSubs.Length; k++)
			{
				if (k != _kind)
				{
					objMenuSubMaleSubs[k].SetActive(value: false);
				}
			}
			component.SetBool("on", value: true);
			Utils.Sound.Play(SystemSE.sel);
			for (int l = 0; l < BtSpriteMaleSub.Length; l++)
			{
				BtSpriteMaleSub[l].OnChangeValue(l == _kind);
			}
			int coordinateType = male.fileStatus.coordinateType;
			for (int m = 0; m < BtSpriteMaleCoodeSub.Length; m++)
			{
				BtSpriteMaleCoodeSub[m].OnChangeValue(m == coordinateType);
			}
		}
		for (int n = 0; n < BtSpriteFemaleSub.Length; n++)
		{
			BtSpriteFemaleSub[n].OnChangeValue(_isOn: false);
		}
		for (int num = 0; num < BtSpriteFemaleCoodeSub.Length; num++)
		{
			BtSpriteFemaleCoodeSub[num].OnChangeValue(_isOn: false);
		}
	}

	public void OnSubMenuMultiMale(int _kind)
	{
		if (!Input.GetMouseButtonUp(0) || !IsSpriteAciotn())
		{
			return;
		}
		Animator component = MultipleMenuSubMaleSubs[_kind].GetComponent<Animator>();
		if (MultipleMenuSubMaleSubs[_kind].activeSelf)
		{
			component.SetBool("on", value: false);
			component.GetBehaviour<MenuStateMachineBehaviour>().obj = MultipleMenuSubMaleSubs[_kind];
			for (int i = 0; i < BtSpriteMultiMaleSub.Length; i++)
			{
				BtSpriteMultiMaleSub[i].OnChangeValue(_isOn: false);
			}
			for (int j = 0; j < BtSpriteMultiMaleCoodeSub.Length; j++)
			{
				BtSpriteMultiMaleCoodeSub[j].OnChangeValue(_isOn: false);
			}
			return;
		}
		MultipleMenuSubMaleSubs[_kind].SetActive(value: true);
		for (int k = 0; k < MultipleMenuSubMaleSubs.Length; k++)
		{
			if (k != _kind)
			{
				MultipleMenuSubMaleSubs[k].SetActive(value: false);
			}
		}
		component.SetBool("on", value: true);
		Utils.Sound.Play(SystemSE.sel);
		for (int l = 0; l < BtSpriteMultiMaleSub.Length; l++)
		{
			BtSpriteMultiMaleSub[l].OnChangeValue(l == _kind);
		}
		int coordinateType = male.fileStatus.coordinateType;
		for (int m = 0; m < BtSpriteMultiMaleCoodeSub.Length; m++)
		{
			BtSpriteMultiMaleCoodeSub[m].OnChangeValue(m == coordinateType);
		}
	}

	public void OnClickCloth(int _cloth)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			int clothesStateNext = _cloth;
			if (_cloth == 7 && !females[0].IsClothes(_cloth))
			{
				clothesStateNext = 8;
			}
			females[0].SetClothesStateNext(clothesStateNext);
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnClickClothMale(int _cloth)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			if (_cloth == 0)
			{
				Manager.Config.HData.IsMaleClothes = !Manager.Config.HData.IsMaleClothes;
			}
			else
			{
				Manager.Config.HData.IsMaleShoes = !Manager.Config.HData.IsMaleShoes;
			}
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnClickAllCloth(int _cloth)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			females[0].SetClothesStateAll((byte)_cloth);
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnClickAccessory(int _accessory)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			females[0].chaFile.status.showAccessory[_accessory] = !females[0].chaFile.status.showAccessory[_accessory];
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnClickAllAccessory(bool _accessory)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			females[0].SetAccessoryStateAll(_accessory);
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnClickAllAccessoryGroup1(bool _accessory)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			females[0].SetAccessoryStateCategory(0, _accessory);
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnClickAllAccessoryGroup2(bool _accessory)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			females[0].SetAccessoryStateCategory(1, _accessory);
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnClickMaleAccessoryGroup1(bool _accessory)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			Manager.Config.HData.IsMaleAccessoriesMain = _accessory;
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnClickMaleAccessoryGroup2(bool _accessory)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			Manager.Config.HData.IsMaleAccessoriesSub = _accessory;
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnClickCoordinateChange(int _coordinate)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			females[0].ChangeCoordinateTypeAndReload((ChaFileDefine.CoordinateType)(0 + _coordinate));
			Utils.Sound.Play(SystemSE.sel);
			for (int i = 0; i < BtSpriteFemaleCoodeSub.Length; i++)
			{
				BtSpriteFemaleCoodeSub[i].OnChangeValue(i == _coordinate);
			}
		}
	}

	public void OnClickMaleCoordinateChange(int _coordinate)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			male.ChangeCoordinateTypeAndReload((ChaFileDefine.CoordinateType)(0 + _coordinate));
			Utils.Sound.Play(SystemSE.sel);
			for (int i = 0; i < BtSpriteMaleCoodeSub.Length; i++)
			{
				BtSpriteMaleCoodeSub[i].OnChangeValue(i == _coordinate);
			}
		}
	}

	public void OnClothChange(int _female)
	{
		if (!Input.GetMouseButtonUp(0) || !IsSpriteAciotn())
		{
			return;
		}
		clothCusutomCtrl.chaCtrl = females[_female];
		clothCusutomCtrl.gameObject.SetActive(value: true);
		Utils.Sound.Play(SystemSE.window_o);
		objMenuSubMale.SetActive(value: false);
		objMenuSubFemale.SetActive(value: false);
		GameObject[] array = objMenuSubMaleSubs;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].SetActive(value: false);
		}
		array = objMenuSubFemaleSubs;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].SetActive(value: false);
		}
		foreach (HSceneSpriteCategoryParent item in lstMultipleFemaleGroup)
		{
			item.gameObject.SetActive(value: false);
		}
		MultipleMenuSubMale.gameObject.SetActive(value: false);
		foreach (MultipleFemaleSubGroup item2 in lstMultipleFemaleSubGroup)
		{
			foreach (GameObject item3 in item2.lstGroup)
			{
				item3.SetActive(value: false);
			}
		}
		array = MultipleMenuSubMaleSubs;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].SetActive(value: false);
		}
		clothCusutomOpen = true;
		(from _ in Observable.EveryUpdate().TakeUntilDestroy(this).TakeWhile((long _) => clothCusutomOpen)
			where !clothCusutomCtrl.gameObject.activeSelf
			select _).Subscribe(delegate
		{
			ButtonSpriteChangeCtrl[] btSpriteSub = BtSpriteSub;
			for (int j = 0; j < btSpriteSub.Length; j++)
			{
				btSpriteSub[j].OnChangeValue(_isOn: false);
			}
			btSpriteSub = BtSpriteMultiSub;
			for (int j = 0; j < btSpriteSub.Length; j++)
			{
				btSpriteSub[j].OnChangeValue(_isOn: false);
			}
			clothCusutomOpen = false;
		});
	}

	public void OnClothCharaClick(int kind)
	{
		if (!Input.GetMouseButtonUp(0) || !IsSpriteAciotn())
		{
			return;
		}
		ButtonSpriteChangeCtrl[] btSpriteSub;
		if (kind == 0)
		{
			Animator component = objMenuSubMale.GetComponent<Animator>();
			if (objMenuSubMale.activeSelf)
			{
				component.SetBool("on", value: false);
				component.GetBehaviour<MenuStateMachineBehaviour>().obj = objMenuSubMale;
				btSpriteSub = BtSpriteSub;
				for (int i = 0; i < btSpriteSub.Length; i++)
				{
					btSpriteSub[i].OnChangeValue(_isOn: false);
				}
			}
			else
			{
				objMenuSubMale.SetActive(value: true);
				objMenuSubFemale.SetActive(value: false);
				component.SetBool("on", value: true);
				Utils.Sound.Play(SystemSE.sel);
				BtSpriteSub[0].OnChangeValue(_isOn: true);
				BtSpriteSub[1].OnChangeValue(_isOn: false);
			}
		}
		else
		{
			Animator component2 = objMenuSubFemale.GetComponent<Animator>();
			if (objMenuSubFemale.activeSelf)
			{
				component2.SetBool("on", value: false);
				component2.GetBehaviour<MenuStateMachineBehaviour>().obj = objMenuSubFemale;
				btSpriteSub = BtSpriteSub;
				for (int i = 0; i < btSpriteSub.Length; i++)
				{
					btSpriteSub[i].OnChangeValue(_isOn: false);
				}
			}
			else
			{
				objMenuSubMale.SetActive(value: false);
				objMenuSubFemale.SetActive(value: true);
				component2.SetBool("on", value: true);
				Utils.Sound.Play(SystemSE.sel);
				BtSpriteSub[0].OnChangeValue(_isOn: false);
				BtSpriteSub[1].OnChangeValue(_isOn: true);
			}
		}
		GameObject[] array = objMenuSubMaleSubs;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].SetActive(value: false);
		}
		array = objMenuSubFemaleSubs;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].SetActive(value: false);
		}
		btSpriteSub = BtSpriteMaleSub;
		for (int i = 0; i < btSpriteSub.Length; i++)
		{
			btSpriteSub[i].OnChangeValue(_isOn: false);
		}
		btSpriteSub = BtSpriteMaleCoodeSub;
		for (int i = 0; i < btSpriteSub.Length; i++)
		{
			btSpriteSub[i].OnChangeValue(_isOn: false);
		}
		btSpriteSub = BtSpriteFemaleSub;
		for (int i = 0; i < btSpriteSub.Length; i++)
		{
			btSpriteSub[i].OnChangeValue(_isOn: false);
		}
		btSpriteSub = BtSpriteFemaleCoodeSub;
		for (int i = 0; i < btSpriteSub.Length; i++)
		{
			btSpriteSub[i].OnChangeValue(_isOn: false);
		}
	}

	public void OnFemaleClick(int _female)
	{
		if (!Input.GetMouseButtonUp(0) || !IsSpriteAciotn())
		{
			return;
		}
		Animator component = lstMultipleFemaleGroup[_female].GetComponent<Animator>();
		ButtonSpriteChangeCtrl[] btSpriteMultiSub;
		if (lstMultipleFemaleGroup[_female].gameObject.activeSelf)
		{
			component.SetBool("on", value: false);
			component.GetBehaviour<MenuStateMachineBehaviour>().obj = lstMultipleFemaleGroup[_female].gameObject;
			btSpriteMultiSub = BtSpriteMultiSub;
			for (int i = 0; i < btSpriteMultiSub.Length; i++)
			{
				btSpriteMultiSub[i].OnChangeValue(_isOn: false);
			}
		}
		else
		{
			for (int j = 0; j < lstMultipleFemaleGroup.Count; j++)
			{
				lstMultipleFemaleGroup[j].gameObject.SetActive(j == _female);
			}
			component.SetBool("on", value: true);
			Utils.Sound.Play(SystemSE.sel);
			BtSpriteMultiSub[0].OnChangeValue(_isOn: false);
			BtSpriteMultiSub[1].OnChangeValue(_female == 0);
			BtSpriteMultiSub[2].OnChangeValue(_female == 1);
		}
		MultipleMenuSubMale.gameObject.SetActive(value: false);
		foreach (MultipleFemaleSubGroup item in lstMultipleFemaleSubGroup)
		{
			foreach (GameObject item2 in item.lstGroup)
			{
				item2.SetActive(value: false);
			}
		}
		GameObject[] multipleMenuSubMaleSubs = MultipleMenuSubMaleSubs;
		for (int i = 0; i < multipleMenuSubMaleSubs.Length; i++)
		{
			multipleMenuSubMaleSubs[i].SetActive(value: false);
		}
		btSpriteMultiSub = BtSpriteMultiMaleSub;
		for (int i = 0; i < btSpriteMultiSub.Length; i++)
		{
			btSpriteMultiSub[i].OnChangeValue(_isOn: false);
		}
		btSpriteMultiSub = BtSpriteMultiMaleCoodeSub;
		for (int i = 0; i < btSpriteMultiSub.Length; i++)
		{
			btSpriteMultiSub[i].OnChangeValue(_isOn: false);
		}
		btSpriteMultiSub = BtSpriteMultiFemaleSub;
		for (int i = 0; i < btSpriteMultiSub.Length; i++)
		{
			btSpriteMultiSub[i].OnChangeValue(_isOn: false);
		}
		btSpriteMultiSub = BtSpriteMultiFemaleCoodeSub;
		for (int i = 0; i < btSpriteMultiSub.Length; i++)
		{
			btSpriteMultiSub[i].OnChangeValue(_isOn: false);
		}
		btSpriteMultiSub = BtSpriteMultiFemaleSub1;
		for (int i = 0; i < btSpriteMultiSub.Length; i++)
		{
			btSpriteMultiSub[i].OnChangeValue(_isOn: false);
		}
		btSpriteMultiSub = BtSpriteMultiFemaleCoodeSub1;
		for (int i = 0; i < btSpriteMultiSub.Length; i++)
		{
			btSpriteMultiSub[i].OnChangeValue(_isOn: false);
		}
	}

	public void OnMaleClick()
	{
		if (!Input.GetMouseButtonUp(0) || !IsSpriteAciotn())
		{
			return;
		}
		Animator component = MultipleMenuSubMale.GetComponent<Animator>();
		ButtonSpriteChangeCtrl[] btSpriteMultiSub;
		if (MultipleMenuSubMale.gameObject.activeSelf)
		{
			component.SetBool("on", value: false);
			component.GetBehaviour<MenuStateMachineBehaviour>().obj = MultipleMenuSubMale.gameObject;
			btSpriteMultiSub = BtSpriteMultiSub;
			for (int i = 0; i < btSpriteMultiSub.Length; i++)
			{
				btSpriteMultiSub[i].OnChangeValue(_isOn: false);
			}
		}
		else
		{
			MultipleMenuSubMale.gameObject.SetActive(value: true);
			component.SetBool("on", value: true);
			Utils.Sound.Play(SystemSE.sel);
			BtSpriteMultiSub[0].OnChangeValue(_isOn: true);
			BtSpriteMultiSub[1].OnChangeValue(_isOn: false);
			BtSpriteMultiSub[2].OnChangeValue(_isOn: false);
		}
		for (int j = 0; j < lstMultipleFemaleGroup.Count; j++)
		{
			lstMultipleFemaleGroup[j].gameObject.SetActive(value: false);
		}
		foreach (MultipleFemaleSubGroup item in lstMultipleFemaleSubGroup)
		{
			foreach (GameObject item2 in item.lstGroup)
			{
				item2.SetActive(value: false);
			}
		}
		GameObject[] multipleMenuSubMaleSubs = MultipleMenuSubMaleSubs;
		for (int i = 0; i < multipleMenuSubMaleSubs.Length; i++)
		{
			multipleMenuSubMaleSubs[i].SetActive(value: false);
		}
		btSpriteMultiSub = BtSpriteMultiMaleSub;
		for (int i = 0; i < btSpriteMultiSub.Length; i++)
		{
			btSpriteMultiSub[i].OnChangeValue(_isOn: false);
		}
		btSpriteMultiSub = BtSpriteMultiMaleCoodeSub;
		for (int i = 0; i < btSpriteMultiSub.Length; i++)
		{
			btSpriteMultiSub[i].OnChangeValue(_isOn: false);
		}
		btSpriteMultiSub = BtSpriteMultiFemaleSub;
		for (int i = 0; i < btSpriteMultiSub.Length; i++)
		{
			btSpriteMultiSub[i].OnChangeValue(_isOn: false);
		}
		btSpriteMultiSub = BtSpriteMultiFemaleCoodeSub;
		for (int i = 0; i < btSpriteMultiSub.Length; i++)
		{
			btSpriteMultiSub[i].OnChangeValue(_isOn: false);
		}
		btSpriteMultiSub = BtSpriteMultiFemaleSub1;
		for (int i = 0; i < btSpriteMultiSub.Length; i++)
		{
			btSpriteMultiSub[i].OnChangeValue(_isOn: false);
		}
		btSpriteMultiSub = BtSpriteMultiFemaleCoodeSub1;
		for (int i = 0; i < btSpriteMultiSub.Length; i++)
		{
			btSpriteMultiSub[i].OnChangeValue(_isOn: false);
		}
	}

	public void OnFemaleDressSubMenu(int _female, int _kind)
	{
		if (!Input.GetMouseButtonUp(0) || !IsSpriteAciotn())
		{
			return;
		}
		Animator component = lstMultipleFemaleSubGroup[_female].lstGroup[_kind].GetComponent<Animator>();
		if (lstMultipleFemaleSubGroup[_female].lstGroup[_kind].activeSelf)
		{
			component.SetBool("on", value: false);
			component.GetBehaviour<MenuStateMachineBehaviour>().obj = lstMultipleFemaleSubGroup[_female].lstGroup[_kind];
			for (int i = 0; i < BtSpriteMultiFemaleSub.Length; i++)
			{
				BtSpriteMultiFemaleSub[i].OnChangeValue(_isOn: false);
			}
			for (int j = 0; j < BtSpriteMultiFemaleCoodeSub.Length; j++)
			{
				BtSpriteMultiFemaleCoodeSub[j].OnChangeValue(_isOn: false);
			}
			for (int k = 0; k < BtSpriteMultiFemaleSub1.Length; k++)
			{
				BtSpriteMultiFemaleSub1[k].OnChangeValue(_isOn: false);
			}
			for (int l = 0; l < BtSpriteMultiFemaleCoodeSub1.Length; l++)
			{
				BtSpriteMultiFemaleCoodeSub1[l].OnChangeValue(_isOn: false);
			}
		}
		else
		{
			for (int m = 0; m < lstMultipleFemaleSubGroup[_female].lstGroup.Count; m++)
			{
				lstMultipleFemaleSubGroup[_female].lstGroup[m].SetActive(m == _kind);
			}
			component.SetBool("on", value: true);
			FemaleDressSubMenuProc(_female, _kind);
			Utils.Sound.Play(SystemSE.sel);
			int coordinateType = females[_female].fileStatus.coordinateType;
			if (_female == 0)
			{
				for (int n = 0; n < BtSpriteMultiFemaleSub.Length; n++)
				{
					BtSpriteMultiFemaleSub[n].OnChangeValue(n == _kind);
				}
				for (int num = 0; num < BtSpriteMultiFemaleCoodeSub.Length; num++)
				{
					BtSpriteMultiFemaleCoodeSub[num].OnChangeValue(num == coordinateType);
				}
				for (int num2 = 0; num2 < BtSpriteMultiFemaleSub1.Length; num2++)
				{
					BtSpriteMultiFemaleSub1[num2].OnChangeValue(_isOn: false);
				}
				for (int num3 = 0; num3 < BtSpriteMultiFemaleCoodeSub1.Length; num3++)
				{
					BtSpriteMultiFemaleCoodeSub1[num3].OnChangeValue(_isOn: false);
				}
			}
			else
			{
				for (int num4 = 0; num4 < BtSpriteMultiFemaleSub1.Length; num4++)
				{
					BtSpriteMultiFemaleSub1[num4].OnChangeValue(num4 == _kind);
				}
				for (int num5 = 0; num5 < BtSpriteMultiFemaleCoodeSub1.Length; num5++)
				{
					BtSpriteMultiFemaleCoodeSub1[num5].OnChangeValue(num5 == coordinateType);
				}
				for (int num6 = 0; num6 < BtSpriteMultiFemaleSub.Length; num6++)
				{
					BtSpriteMultiFemaleSub[num6].OnChangeValue(_isOn: false);
				}
				for (int num7 = 0; num7 < BtSpriteMultiFemaleCoodeSub.Length; num7++)
				{
					BtSpriteMultiFemaleCoodeSub[num7].OnChangeValue(_isOn: false);
				}
			}
		}
		for (int num8 = 0; num8 < BtSpriteMultiMaleSub.Length; num8++)
		{
			BtSpriteMultiMaleSub[num8].OnChangeValue(_isOn: false);
		}
		for (int num9 = 0; num9 < BtSpriteMultiMaleCoodeSub.Length; num9++)
		{
			BtSpriteMultiMaleCoodeSub[num9].OnChangeValue(_isOn: false);
		}
	}

	public void OnClickCloth(int _female, int _cloth)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			int clothesStateNext = _cloth;
			if (_cloth == 7 && !females[_female].IsClothes(_cloth))
			{
				clothesStateNext = 8;
			}
			females[_female].SetClothesStateNext(clothesStateNext);
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnClickAllCloth(int _female, int _cloth)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			females[_female].SetClothesStateAll((byte)_cloth);
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnClickAccessory(int _female, int _accessory)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			females[_female].chaFile.status.showAccessory[_accessory] = !females[_female].chaFile.status.showAccessory[_accessory];
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnClickAllAccessory(int _female, bool _accessory)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			females[_female].SetAccessoryStateAll(_accessory);
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnClickAllAccessoryGroup1(int _female, bool _accessory)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			females[_female].SetAccessoryStateCategory(0, _accessory);
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnClickAllAccessoryGroup2(int _female, bool _accessory)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			females[_female].SetAccessoryStateCategory(1, _accessory);
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnClickCoordinateChange(int _female, int _coordinate)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			females[_female].ChangeCoordinateTypeAndReload((ChaFileDefine.CoordinateType)(0 + _coordinate));
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnClickHSceneEnd()
	{
		if (!Scene.IsNowLoadingFade && !(Scene.NowSceneNames[0] != "HProc") && Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			Utils.Sound.Play(SystemSE.cancel);
			flags.click = HFlag.ClickKind.end;
			flags.isHSceneEnd = true;
			flags.numEnd = 0;
			if (flags.shortcut != null)
			{
				flags.shortcut.enabled = false;
			}
			ActionScene instance = SingletonInitializer<ActionScene>.instance;
			if ((bool)instance)
			{
				instance.ShortcutKeyEnable(isOn: false);
			}
		}
	}

	public void OnClickTrespassing()
	{
		if (Scene.IsNowLoadingFade || Scene.NowSceneNames[0] != "HProc" || !Input.GetMouseButtonUp(0) || !IsSpriteAciotn())
		{
			return;
		}
		Utils.Sound.Play(SystemSE.sel);
		ConfirmDialog.Status status = ConfirmDialog.status;
		status.Title = "Hシーン確認";
		if (flags.mode == HFlag.EMode.masturbation)
		{
			status.Sentence = "このままエッチに入りますか？";
		}
		else if (flags.mode == HFlag.EMode.lesbian)
		{
			status.Sentence = "3Pに入りますか？";
		}
		else
		{
			status.Sentence = "3Pに入りますか？";
		}
		status.Yes = delegate
		{
			flags.click = HFlag.ClickKind.end;
			flags.isHSceneEnd = true;
			flags.numEnd = 2;
			if (flags.mode == HFlag.EMode.masturbation && flags.isMasturbationFound)
			{
				flags.lstHeroine[0].lewdness = 100;
			}
			ActionScene instance = SingletonInitializer<ActionScene>.instance;
			if ((bool)instance)
			{
				instance.isPenetration = true;
			}
			if (flags.shortcut != null)
			{
				flags.shortcut.enabled = false;
			}
			if ((bool)instance)
			{
				instance.ShortcutKeyEnable(isOn: false);
			}
		};
		status.No = delegate
		{
		};
		ConfirmDialog.Load();
	}

	public void OnClickConfig()
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			Utils.Sound.Play(SystemSE.ok_s);
			ConfigScene.Load();
		}
	}

	public void OnValueRChanged(float _value)
	{
		if (Input.GetMouseButton(0) && IsSpriteAciotn())
		{
			Color color = lightData.light.color;
			color.r = _value;
			lightData.light.color = color;
		}
	}

	public void OnValueGChanged(float _value)
	{
		if (Input.GetMouseButton(0) && IsSpriteAciotn())
		{
			Color color = lightData.light.color;
			color.g = _value;
			lightData.light.color = color;
		}
	}

	public void OnValueBChanged(float _value)
	{
		if (Input.GetMouseButton(0) && IsSpriteAciotn())
		{
			Color color = lightData.light.color;
			color.b = _value;
			lightData.light.color = color;
		}
	}

	public void OnClickLightColorInit(int _num)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			Color color = lightData.light.color;
			switch (_num)
			{
			case 0:
				lightSlider.r.value = lightData.initColor.r;
				color.r = lightData.initColor.r;
				break;
			case 1:
				lightSlider.g.value = lightData.initColor.g;
				color.g = lightData.initColor.g;
				break;
			case 2:
				lightSlider.b.value = lightData.initColor.b;
				color.b = lightData.initColor.b;
				break;
			case 3:
				lightData.light.intensity = lightData.initIntensity;
				lightSlider.a.value = lightData.initIntensity;
				color.a = lightData.initIntensity;
				break;
			}
			lightData.light.color = color;
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void LightColorAllInit()
	{
		lightData.light.color = lightData.initColor;
		lightData.light.intensity = lightData.initIntensity;
	}

	public void OnValueVerticalChanged(float _value)
	{
		if (Input.GetMouseButton(0) && IsSpriteAciotn())
		{
			lightData.calcRot.x = _value;
			float x = _value * 360f + lightData.initRot.x;
			float y = lightData.calcRot.y * 360f + lightData.initRot.y;
			lightData.light.transform.localRotation = Quaternion.Euler(x, y, 0f);
		}
	}

	public void OnValueHorizonChanged(float _value)
	{
		if (Input.GetMouseButton(0) && IsSpriteAciotn())
		{
			lightData.calcRot.y = _value;
			float x = lightData.calcRot.x * 360f + lightData.initRot.x;
			float y = _value * 360f + lightData.initRot.y;
			lightData.light.transform.localRotation = Quaternion.Euler(x, y, 0f);
		}
	}

	public void OnValuePowerChanged(float _value)
	{
		if (Input.GetMouseButton(0) && IsSpriteAciotn())
		{
			lightData.light.intensity = _value;
		}
	}

	public void OnClickLightDirInit(int _num)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			if (_num == 0)
			{
				lightData.calcRot.x = 0f;
				lightData.light.transform.localRotation = Quaternion.Euler(lightData.initRot.x, lightSlider.y.value * 360f, 0f);
				lightSlider.x.value = 0f;
			}
			else
			{
				lightData.calcRot.y = 0f;
				lightData.light.transform.localRotation = Quaternion.Euler(lightSlider.x.value * 360f, lightData.initRot.y, 0f);
				lightSlider.y.value = 0f;
			}
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void LightDirInit()
	{
		lightData.calcRot = Vector2.zero;
		lightData.light.transform.localRotation = Quaternion.Euler(lightData.initRot.x, lightData.initRot.y, 0f);
	}

	public void OnClickMoveAxisDraw(bool _isOn)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			axis.guideObj.gameObject.SetActive(_isOn);
			axis.imgDisable.gameObject.SetActive(!_isOn);
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void MoveAxisDraw(bool _isOn)
	{
		axis.guideObj.gameObject.SetActive(_isOn);
		axis.imgDisable.gameObject.SetActive(!_isOn);
	}

	public void OnClickChangeMoveAxis(int _change)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn() && moveAxizInteractive.lstTgl[_change].interactable)
		{
			axis.guideObj.SetMode(_change);
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void ChangeMoveAxis(int _change)
	{
		axis.guideObj.SetMode(_change);
	}

	public void OnClickInitMoveAxis(int _init)
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			localMoveInitAction(_init);
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnValueMoveAxisSpeedChange(float _value)
	{
		if (Input.GetMouseButton(0) && IsSpriteAciotn())
		{
			axis.guideObj.speedMove = _value;
		}
	}

	public void OnClickMoveAxisInitSpeed()
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			axis.sliderSpeed.value = 1f;
			axis.guideObj.speedMove = 1f;
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnValueMoveAxisScaleChange(float _value)
	{
		if (Input.GetMouseButton(0) && IsSpriteAciotn())
		{
			axis.guideObj.scaleAxis = _value;
			axis.guideObj.SetScale();
		}
	}

	public void OnClickMoveAxisInitScale()
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			axis.sliderScale.value = 1.5f;
			axis.guideObj.scaleAxis = 1.5f;
			axis.guideObj.SetScale();
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnMouseDownSlider()
	{
		GlobalMethod.SetCameraMoveFlag(flags.ctrlCamera, _bPlay: false);
	}

	public void OnAibuIconOpenAndClose(bool _open)
	{
		categoryAibuIcon.SetActive(_open);
		objAibuItemIconOpen.SetActive(!_open);
		Utils.Sound.Play(SystemSE.sel);
	}

	public void OnMainMenuOpenAndClose(bool _close)
	{
		objBaseMainMenu.SetActive(!_close);
		objMainMenuOpen.SetActive(_close);
		if (_close)
		{
			CloseAllMenu();
		}
		Utils.Sound.Play(SystemSE.sel);
	}

	public void OnClickHelp()
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			if ((bool)autoDisableFirstHHelp)
			{
				autoDisableFirstHHelp.FadeStart();
			}
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnDetachItemClick(int _num)
	{
		if (!Input.GetMouseButtonUp(0))
		{
			return;
		}
		if (_num != 3)
		{
			int num = hand.DetachItemByUseItem(_num);
			if (num != -1)
			{
				flags.click = (HFlag.ClickKind)(num + 22);
			}
			Utils.Sound.Play(SystemSE.sel);
		}
		else
		{
			hand.DetachAllItem();
			flags.click = HFlag.ClickKind.de_muneL;
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnImmediatelyFinishMale()
	{
		if (Input.GetMouseButtonUp(0))
		{
			bool flag = false;
			if (flags.mode == HFlag.EMode.houshi || flags.mode == HFlag.EMode.houshi3P || flags.mode == HFlag.EMode.houshi3PMMF)
			{
				flag = true;
			}
			else if (flags.mode == HFlag.EMode.sonyu || flags.mode == HFlag.EMode.sonyu3P || flags.mode == HFlag.EMode.sonyu3PMMF)
			{
				flag = true;
			}
			if (flags.gaugeMale < 70f && flag && !flags.lockGugeMale)
			{
				flags.gaugeMale = 70f;
			}
			Utils.Sound.Play(SystemSE.sel);
			flags.isUseImmediatelyFinishButton = true;
		}
	}

	public void OnImmediatelyFinishFemale()
	{
		if (Input.GetMouseButtonUp(0))
		{
			bool flag = false;
			flag = flags.mode != HFlag.EMode.sonyu || true;
			if (flags.gaugeFemale < 70f && flag && !flags.lockGugeFemale)
			{
				flags.gaugeFemale = 70f;
			}
			Utils.Sound.Play(SystemSE.sel);
			flags.isUseImmediatelyFinishButton = true;
		}
	}

	public void OnChangeMotionClick()
	{
		if (Input.GetMouseButtonUp(0) && IsSpriteAciotn())
		{
			flags.click = HFlag.ClickKind.motionchange;
		}
	}

	public void OnOrgWClick()
	{
		if (Input.GetMouseButtonUp(0))
		{
			flags.click = HFlag.ClickKind.orgW;
			if (flags.lstHeroine[0].hCount == 0 && !flags.isFreeH && (bool)objFirstHHelpBase && objFirstHHelpBase.activeSelf && !flags.isAnalPlay)
			{
				objFirstHHelpBase.SetActive(value: false);
			}
		}
	}

	public void OnSameWClick()
	{
		if (Input.GetMouseButtonUp(0))
		{
			flags.click = HFlag.ClickKind.sameW;
			if (flags.lstHeroine[0].hCount == 0 && !flags.isFreeH && (bool)objFirstHHelpBase && objFirstHHelpBase.activeSelf && !flags.isAnalPlay)
			{
				objFirstHHelpBase.SetActive(value: false);
			}
		}
	}

	public void OnOrgSClick()
	{
		if (Input.GetMouseButtonUp(0))
		{
			flags.click = HFlag.ClickKind.orgS;
			if (flags.lstHeroine[0].hCount == 0 && !flags.isFreeH && (bool)objFirstHHelpBase && objFirstHHelpBase.activeSelf && !flags.isAnalPlay)
			{
				objFirstHHelpBase.SetActive(value: false);
			}
		}
	}

	public void OnSameSClick()
	{
		if (Input.GetMouseButtonUp(0))
		{
			flags.click = HFlag.ClickKind.sameS;
			if (flags.lstHeroine[0].hCount == 0 && !flags.isFreeH && (bool)objFirstHHelpBase && objFirstHHelpBase.activeSelf && !flags.isAnalPlay)
			{
				objFirstHHelpBase.SetActive(value: false);
			}
		}
	}

	public void OnIdleClick()
	{
		if (Input.GetMouseButtonUp(0))
		{
			flags.click = HFlag.ClickKind.Idle;
		}
	}

	public void OnStopIdleClick()
	{
		if (Input.GetMouseButtonUp(0))
		{
			flags.click = HFlag.ClickKind.stop_Idle;
		}
	}

	public void OnLoopClick()
	{
		if (Input.GetMouseButtonUp(0))
		{
			flags.click = HFlag.ClickKind.insert;
		}
	}

	public void OnOLoopClick()
	{
		if (Input.GetMouseButtonUp(0))
		{
			flags.click = HFlag.ClickKind.OLoop;
		}
	}

	public void OnFrontDislikesClick()
	{
		if (Input.GetMouseButtonUp(0))
		{
			flags.click = HFlag.ClickKind.Front_Dislikes;
		}
	}

	public void OnBackDislikesClick()
	{
		if (Input.GetMouseButtonUp(0))
		{
			flags.click = HFlag.ClickKind.Back_Dislikes;
		}
	}

	public void NickNamePlay()
	{
		WorldData.CallFileData callFileData = WorldData.FindCallFileData(flags.lstHeroine[0].personality, 0);
		Utils.Voice.Setting s = new Utils.Voice.Setting
		{
			no = flags.lstHeroine[0].voiceNo,
			assetBundleName = callFileData.bundle,
			assetName = callFileData.GetFileName(2),
			pitch = flags.lstHeroine[0].voicePitch,
			voiceTrans = flags.transVoiceMouth[0]
		};
		females[0].SetLipSync(Utils.Voice.OncePlayChara(s));
	}

	public void Init(List<ChaControl> _female, Heroine _heroine, ChaControl _male, AutoRely _rely, List<HSceneProc.AnimationListInfo>[] _lstAnimation, List<int> _category, HVoiceCtrl _voice)
	{
		females = _female;
		heroine = _heroine;
		male = _male;
		rely = _rely;
		lstUseAnimInfo = _lstAnimation;
		voice = _voice;
		lstCategory = _category;
		bool flag = _category.Any((int c) => MathfEx.IsRange(3000, c, 3999, isEqual: true));
		int[] array = new int[3];
		for (int num = 0; num < 3; num++)
		{
			array[num] = lstUseAnimInfo[num].Count;
			bool active = array[num] >= 1;
			menuAction.SetActive(active, num);
		}
		if (flag)
		{
			menuAction.SetActive(_active: false, 0);
			for (int num2 = 1; num2 < 3; num2++)
			{
				array[num2] = lstUseAnimInfo[num2 + 5].Count;
				bool active2 = array[num2] >= 1;
				menuAction.SetActive(active2, num2);
			}
		}
		ActionScene instance = SingletonInitializer<ActionScene>.instance;
		bool flag2 = array.Count((int c) => c >= 1) >= 2;
		flag2 |= instance != null && !instance.isPenetration && flags.mode != HFlag.EMode.peeping && !flags.isFreeH;
		menuAction.gameObject.SetActive(flag2);
		lstHoushiAnimInfo[0] = new List<HSceneProc.AnimationListInfo>();
		lstHoushiAnimInfo[1] = new List<HSceneProc.AnimationListInfo>();
		lstHoushiAnimInfo[2] = new List<HSceneProc.AnimationListInfo>();
		int i;
		int num3;
		for (i = 0; i < lstHoushiAnimInfo.Length; i = num3)
		{
			lstHoushiAnimInfo[i] = lstUseAnimInfo[1].Where((HSceneProc.AnimationListInfo l) => l.kindHoushi == i).ToList();
			num3 = i + 1;
		}
		lst3PHoushiAnimInfo[0] = new List<HSceneProc.AnimationListInfo>();
		lst3PHoushiAnimInfo[1] = new List<HSceneProc.AnimationListInfo>();
		lst3PHoushiAnimInfo[2] = new List<HSceneProc.AnimationListInfo>();
		HashSet<int> hashSet = new HashSet<int>();
		foreach (HSceneProc.AnimationListInfo item in lstUseAnimInfo[1])
		{
			hashSet.Add(item.posture);
		}
		lstHoushiPose = new List<int>(hashSet);
		if (lstHoushiPose.Count != 0)
		{
			flags.poseHoushi = lstHoushiPose[0];
		}
		array = new int[3];
		for (int num4 = 0; num4 < 3; num4++)
		{
			array[num4] = lstHoushiAnimInfo[num4].Count;
			bool active3 = array[num4] >= 1;
			menuActionSub.SetActive(active3, num4 + 1);
			categoryToggleHoushi.SetActive(active3, num4);
		}
		SetHoushiStart();
		SetSonyuStart();
		clothCusutomCtrl.chaCtrl = females[0];
		categoryMenstruation.SetActiveToggle((HFlag.GetMenstruation(heroine.MenstruationDay) != HFlag.MenstruationType.安全日) ? 1 : 0);
		categoryExperience.SetActiveToggle((int)heroine.HExperience);
		Texture2D texture2D = new Texture2D(240, 320);
		texture2D.LoadImage(heroine.charFile.facePngData);
		rawFace.texture = texture2D;
		menuActionSub.SetActive(_active: false);
		ButtonSpriteChangeCtrl[] array2 = menuActionBtChanges;
		for (num3 = 0; num3 < array2.Length; num3++)
		{
			array2[num3].OnChangeValue(_isOn: false);
		}
	}

	public void ChangetaiiMovePoint(List<int> _category)
	{
		lstCategory = _category;
		SetHoushiStart();
		SetSonyuStart();
		clothCusutomCtrl.chaCtrl = females[0];
		categoryMenstruation.SetActiveToggle((HFlag.GetMenstruation(heroine.MenstruationDay) != HFlag.MenstruationType.安全日) ? 1 : 0);
		categoryExperience.SetActiveToggle((int)heroine.HExperience);
		Texture2D texture2D = new Texture2D(240, 320);
		texture2D.LoadImage(heroine.charFile.facePngData);
		rawFace.texture = texture2D;
		for (int i = 0; i < menuActionSub.lstObj.Count; i++)
		{
			if (i == 4 || i == 7 || !menuActionSub.GetActive(i))
			{
				continue;
			}
			GameObject putParent = menuActionSub.GetPutParent(i);
			for (int j = 0; j < putParent.transform.childCount; j++)
			{
				Transform child = putParent.transform.GetChild(j);
				GameObject gameObject = child.FindLoop("NowPos");
				if (gameObject == null)
				{
					continue;
				}
				bool flag = false;
				foreach (HSceneProc.Category item in child.GetComponent<AnimationInfoComponent>().info.lstCategory)
				{
					if (lstCategory.Contains(item.category))
					{
						flag = true;
						break;
					}
				}
				gameObject.SetActive(!flag);
			}
			break;
		}
	}

	public void InitHeroine(List<Heroine> _heroines)
	{
		if (flags.mode == HFlag.EMode.houshi3P || flags.mode == HFlag.EMode.sonyu3P)
		{
			int index = flags.nowAnimationInfo.id % 2;
			heroine = _heroines[index];
			clothCusutomCtrl.chaCtrl = females[index];
			categoryMenstruation.SetActiveToggle((HFlag.GetMenstruation(heroine.MenstruationDay) != HFlag.MenstruationType.安全日) ? 1 : 0);
			categoryExperience.SetActiveToggle((int)heroine.HExperience);
			Texture2D texture2D = new Texture2D(240, 320);
			texture2D.LoadImage(heroine.charFile.facePngData);
			if ((bool)rawFace.texture)
			{
				UnityEngine.Object.Destroy(rawFace.texture);
			}
			rawFace.texture = texture2D;
		}
	}

	public void InitPointMenuAndHelp(List<int> _category, bool _isFirst = true)
	{
		menuMain.SetEnable(!_category.Any((int c) => c >= 2000) && ((flags.mode != HFlag.EMode.masturbation && flags.mode != HFlag.EMode.lesbian) || flags.isFreeH), 2);
		if ((bool)objFirstHHelpBase && _isFirst)
		{
			bool active = heroine.hCount == 0 && !flags.isFreeH && !_category.Any((int c) => c >= 2000) && flags.mode != HFlag.EMode.masturbation && flags.mode != HFlag.EMode.lesbian && flags.mode != HFlag.EMode.sonyu3P && flags.mode != HFlag.EMode.houshi3P;
			objFirstHHelpBase.SetActive(active);
		}
		categoryExperience.gameObject.SetActive(flags.mode != HFlag.EMode.lesbian);
		if (!_isFirst)
		{
			return;
		}
		ActionScene instance = SingletonInitializer<ActionScene>.instance;
		bool flag = (bool)instance && !instance.isPenetration && flags.mode != HFlag.EMode.peeping && !flags.isFreeH;
		if (flag)
		{
			if (flags.mode == HFlag.EMode.masturbation)
			{
				if (flags.lstHeroine[0].isVirgin)
				{
					flag = false;
				}
			}
			else if (flags.mode == HFlag.EMode.lesbian)
			{
				if (flags.lstHeroine[0].HExperience < Heroine.HExperienceKind.慣れ || flags.lstHeroine[1].HExperience < Heroine.HExperienceKind.慣れ)
				{
					flag = false;
				}
			}
			else if ((flags.mode == HFlag.EMode.aibu || flags.mode == HFlag.EMode.houshi || flags.mode == HFlag.EMode.sonyu) && (_category.Any((int c) => c == 12 || c >= 1000) || flags.newHeroine == null || flags.lstHeroine[0].HExperience < Heroine.HExperienceKind.慣れ))
			{
				flag = false;
			}
		}
		if ((bool)btnTrespassing)
		{
			btnTrespassing.gameObject.SetActive(flag);
		}
	}

	public void InitSpriteButton()
	{
		for (int i = 0; i < lstMultipleFemaleDressButton.Count; i++)
		{
			HSceneSpriteCategory dress = lstMultipleFemaleDressButton[i].dress;
			int female = i;
			int count = dress.GetCount();
			for (int j = 0; j < count; j++)
			{
				int cloth = j;
				dress.SetAction(j, delegate
				{
					OnClickCloth(female, cloth);
				});
			}
			dress = lstMultipleFemaleDressButton[i].dressAll;
			count = dress.GetCount();
			for (int num = 0; num < count; num++)
			{
				int num2 = num;
				int[] cloths = new int[3] { 0, 1, 3 };
				dress.SetAction(num, delegate
				{
					OnClickAllCloth(female, cloths[num2]);
				});
			}
			dress = lstMultipleFemaleDressButton[i].accessory;
			count = dress.GetCount();
			for (int num3 = 0; num3 < count; num3++)
			{
				int cloth2 = num3;
				dress.SetAction(num3, delegate
				{
					OnClickAccessory(female, cloth2);
				});
			}
			dress = lstMultipleFemaleDressButton[i].accessoryAll;
			dress.SetAction(0, delegate
			{
				OnClickAllAccessory(female, _accessory: true);
			});
			dress.SetAction(1, delegate
			{
				OnClickAllAccessory(female, _accessory: false);
			});
			dress.SetAction(2, delegate
			{
				OnClickAllAccessoryGroup1(female, _accessory: true);
			});
			dress.SetAction(3, delegate
			{
				OnClickAllAccessoryGroup1(female, _accessory: false);
			});
			dress.SetAction(4, delegate
			{
				OnClickAllAccessoryGroup2(female, _accessory: true);
			});
			dress.SetAction(5, delegate
			{
				OnClickAllAccessoryGroup2(female, _accessory: false);
			});
			dress = lstMultipleFemaleDressButton[i].coordinate;
			count = dress.GetCount();
			for (int num4 = 0; num4 < count; num4++)
			{
				int cloth3 = num4;
				dress.SetAction(num4, delegate
				{
					OnClickCoordinateChange(female, cloth3);
				});
			}
		}
	}

	public void InitTrespassingHelp()
	{
		if (btnTrespassing.gameObject.activeSelf)
		{
			int num = -1;
			if (flags.mode == HFlag.EMode.masturbation)
			{
				num = 0;
			}
			else if (flags.mode == HFlag.EMode.lesbian)
			{
				num = 1;
			}
			else if (btnTrespassing.gameObject.activeSelf)
			{
				num = 2;
			}
			if (num != -1)
			{
				helpSpriteTrespassing.OnChangeValue(num);
				helpTextTrespassing.OnChangeValue(num);
				RectTransform rectTransform = btnTrespassing.transform.parent.gameObject.transform as RectTransform;
				RectTransform obj = menuActionSub.GetObject(7).transform as RectTransform;
				Vector2 anchoredPosition = obj.anchoredPosition;
				anchoredPosition.y = rectTransform.anchoredPosition.y;
				obj.anchoredPosition = anchoredPosition;
				autoDisableTrespassingHelp.FadeStart();
			}
		}
	}

	public void SetLightData(HSceneProc.LightData _lightdata)
	{
		lightData = _lightdata;
		if (lightData != null)
		{
			lightSlider.r.value = lightData.initColor.r;
			lightSlider.g.value = lightData.initColor.g;
			lightSlider.b.value = lightData.initColor.b;
			lightSlider.a.value = lightData.initIntensity;
			lightSlider.x.value = 0f;
			lightSlider.y.value = 0f;
		}
	}

	public Heroine GetPeepingFemale()
	{
		return resultPeepingNode.Value;
	}

	public void MainSpriteChange(HSceneProc.AnimationListInfo _info)
	{
		SpriteBase[] array = new SpriteBase[10] { aibu, houshi, sonyu, masturbation, peeping, null, houshi3P, sonyu3P, houshi3PDark, sonyu3PDark };
		foreach (SpriteBase spriteBase in array)
		{
			if (spriteBase != null && (bool)spriteBase.mainObj && ((_info.mode != HFlag.EMode.houshi && _info.mode != HFlag.EMode.sonyu) || (spriteBase.id != 8 && spriteBase.id != 9)))
			{
				spriteBase.mainObj.SetActive(spriteBase.id == (int)_info.mode);
			}
		}
		bool flag = _info.mode != HFlag.EMode.peeping && _info.mode != HFlag.EMode.masturbation && _info.mode != HFlag.EMode.lesbian;
		objSpeed.SetActive(flag);
		if (!flag)
		{
			ActionScene instance = SingletonInitializer<ActionScene>.instance;
			bool flag2 = instance != null && !instance.isPenetration && flags.mode != HFlag.EMode.peeping && !flags.isFreeH;
			menuAction.gameObject.SetActive(flag2 || flag);
		}
		else
		{
			menuAction.gameObject.SetActive(flag);
		}
		objSpeedAibuBase.SetActive(_info.mode == HFlag.EMode.aibu);
		RectTransform obj = objSpeed.transform as RectTransform;
		Vector2 anchoredPosition = obj.anchoredPosition;
		anchoredPosition.x = ((_info.mode == HFlag.EMode.aibu) ? 210 : 0);
		obj.anchoredPosition = anchoredPosition;
		(new SpriteStateProcDelegate[10] { SetAibuStart, SetHoushiStart, SetSonyuStart, SetMasturbationStart, SetPeepingStart, SetLesbianStart, SetHoushi3PStart, SetSonyu3PStart, SetHoushi3PDarkStart, SetSonyu3PDarkStart })[(int)_info.mode]();
	}

	private void MainMenuProc(int _kind)
	{
		if (_kind == 2)
		{
			flags.click = HFlag.ClickKind.pointmove;
		}
	}

	private void CreateMotionList(int _kind)
	{
		bool flag = flags.mode == HFlag.EMode.houshi3P || flags.mode == HFlag.EMode.sonyu3P;
		bool flag2 = flags.mode == HFlag.EMode.houshi3PMMF || flags.mode == HFlag.EMode.sonyu3PMMF;
		if (_kind != 1)
		{
			int num = ((_kind != 0) ? 5 : 0);
			if (!menuActionSub.GetActive(num))
			{
				menuActionSub.SetActiveToggle(num);
				LoadMotionList(lstUseAnimInfo[(_kind != 0) ? (flag ? 7 : (flag2 ? 9 : 2)) : 0], menuActionSub.GetPutParent(num));
				RectTransform rectTransform = menuAction.GetObject(_kind).transform as RectTransform;
				RectTransform obj = menuActionSub.GetObject(num).transform as RectTransform;
				Vector2 anchoredPosition = obj.anchoredPosition;
				anchoredPosition.y = rectTransform.anchoredPosition.y;
				obj.anchoredPosition = anchoredPosition;
				Utils.Sound.Play(SystemSE.sel);
			}
			else
			{
				menuActionSub.SetActive(_active: false, num);
			}
		}
		else if (flag || flag2)
		{
			if (!menuActionSub.GetActive(6))
			{
				menuActionSub.SetActiveToggle(6);
				LoadMotionList(lstUseAnimInfo[flag ? 6 : 8], menuActionSub.GetPutParent(6));
				RectTransform rectTransform2 = menuAction.GetObject(_kind).transform as RectTransform;
				RectTransform obj2 = menuActionSub.GetObject(6).transform as RectTransform;
				Vector2 anchoredPosition2 = obj2.anchoredPosition;
				anchoredPosition2.y = rectTransform2.anchoredPosition.y;
				obj2.anchoredPosition = anchoredPosition2;
				Utils.Sound.Play(SystemSE.sel);
			}
			else
			{
				menuActionSub.SetActive(_active: false, 6);
			}
		}
		else if (!menuActionSub.GetActive(4))
		{
			menuActionSub.SetActiveToggle(4);
			RectTransform rectTransform3 = menuAction.GetObject(_kind).transform as RectTransform;
			RectTransform obj3 = menuActionSub.GetObject(4).transform as RectTransform;
			Vector2 anchoredPosition3 = obj3.anchoredPosition;
			anchoredPosition3.y = rectTransform3.anchoredPosition.y;
			obj3.anchoredPosition = anchoredPosition3;
			categoryToggleHoushi.SetToggleAll(_isON: false);
			Utils.Sound.Play(SystemSE.sel);
		}
		else
		{
			for (int i = 1; i < 6; i++)
			{
				menuActionSub.SetActive(_active: false, i);
			}
		}
	}

	private void SubMenuProc(int _kind)
	{
		(new Action[3] { ClothProc, AccessoryProc, CoordinateProc })[_kind]();
	}

	private void ClothProc()
	{
		for (int i = 0; i < 7; i++)
		{
			bool active = females[0].IsClothesStateKind(i);
			categoryCloth.SetActive(active, i);
		}
		categoryCloth.SetActive(females[0].IsShoesStateKind(), 7);
	}

	private void AccessoryProc()
	{
		for (int i = 0; i < 20; i++)
		{
			bool flag = females[0].IsAccessory(i);
			categoryAccessory.SetActive(flag, i);
			if (flag)
			{
				ListInfoComponent component = females[0].objAccessory[i].GetComponent<ListInfoComponent>();
				GameObject gameObject = categoryAccessory.GetObject(i);
				TextMeshProUGUI textMeshProUGUI = null;
				if ((bool)gameObject)
				{
					textMeshProUGUI = gameObject.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
				}
				if ((bool)component && (bool)textMeshProUGUI)
				{
					textMeshProUGUI.text = component.data.Name;
				}
			}
		}
	}

	private void LightProc()
	{
	}

	private void MoveProc()
	{
	}

	private void CoordinateProc()
	{
	}

	private bool LoadMotionList(List<HSceneProc.AnimationListInfo> _lstAnimInfo, GameObject _objParent)
	{
		for (int i = 0; i < _objParent.transform.childCount; i++)
		{
			UnityEngine.Object.Destroy(_objParent.transform.GetChild(i).gameObject);
		}
		ToggleGroup component = _objParent.GetComponent<ToggleGroup>();
		if (_lstAnimInfo == null)
		{
			return true;
		}
		bool flag = false;
		for (int j = 0; j < _lstAnimInfo.Count; j++)
		{
			GameObject objClone = UnityEngine.Object.Instantiate(objMotionListNode);
			AnimationInfoComponent animationInfoComponent = objClone.AddComponent<AnimationInfoComponent>();
			animationInfoComponent.info = _lstAnimInfo[j];
			objClone.transform.SetParent(_objParent.transform, worldPositionStays: false);
			GameObject gameObject = objClone.transform.FindLoop("TextMeshPro Text");
			if ((bool)gameObject)
			{
				gameObject.GetComponent<TextMeshProUGUI>().text = animationInfoComponent.info.nameAnimation;
			}
			Toggle component2 = objClone.GetComponent<Toggle>();
			if ((bool)component2 && (bool)component)
			{
				component2.group = component;
				component2.enabled = false;
				component2.enabled = true;
			}
			GameObject gameObject2 = objClone.transform.FindLoop("New");
			if ((bool)gameObject2)
			{
				Dictionary<int, HashSet<int>> playHList = Game.globalData.playHList;
				if (!playHList.TryGetValue((int)animationInfoComponent.info.mode, out var value))
				{
					value = (playHList[(int)animationInfoComponent.info.mode] = new HashSet<int>());
				}
				gameObject2.SetActive(!value.Contains(animationInfoComponent.info.id));
			}
			GameObject gameObject3 = objClone.transform.FindLoop("NowPos");
			if ((bool)gameObject3)
			{
				bool flag2 = false;
				foreach (HSceneProc.Category item in _lstAnimInfo[j].lstCategory)
				{
					if (lstCategory.Contains(item.category))
					{
						flag2 = true;
						break;
					}
				}
				gameObject3.SetActive(!flag2);
			}
			objClone.GetComponent<PointerAction>().listClickAction.Add(delegate
			{
				OnChangePlaySelect(objClone);
			});
			objClone.GetComponent<PointerAction>().listDownAction.Add(delegate
			{
				OnMouseDownSlider();
			});
			objClone.SetActive(value: true);
			if (_lstAnimInfo[j] == flags.nowAnimationInfo)
			{
				component2.isOn = true;
				flag = true;
			}
		}
		if (!flag)
		{
			for (int num = 0; num < _objParent.transform.childCount; num++)
			{
				SpriteClickChangeCtrl componentInChildren = _objParent.transform.GetChild(num).GetComponentInChildren<SpriteClickChangeCtrl>();
				if (componentInChildren != null)
				{
					componentInChildren.OnChangeValueEnable(_isOn: false);
				}
			}
		}
		return true;
	}

	public void OnChangePlaySelect(GameObject objClick)
	{
		if (null == objClick)
		{
			return;
		}
		Toggle component = objClick.GetComponent<Toggle>();
		if ((bool)component && !component.interactable)
		{
			return;
		}
		AnimationInfoComponent component2 = objClick.GetComponent<AnimationInfoComponent>();
		if (null == component2)
		{
			GlobalMethod.DebugLog("モーションボタンにモーションクラスが入っていない", 1);
		}
		else
		{
			if (component2.info.mode == flags.nowAnimationInfo.mode && component2.info.id == flags.nowAnimationInfo.id)
			{
				return;
			}
			GameObject gameObject = objClick.transform.FindLoop("New");
			if ((bool)gameObject && gameObject.activeSelf)
			{
				gameObject.SetActive(value: false);
			}
			SpriteClickChangeCtrl componentInChildren = objClick.GetComponentInChildren<SpriteClickChangeCtrl>();
			if (componentInChildren != null && !componentInChildren.nowEnable)
			{
				componentInChildren.OnChangeValueEnable(_isOn: true);
			}
			flags.selectAnimationListInfo = component2.info;
			flags.voiceWait = true;
			if (flags.mode != HFlag.EMode.masturbation && flags.mode != HFlag.EMode.lesbian)
			{
				if (flags.mode == flags.selectAnimationListInfo.mode)
				{
					if ((flags.mode == HFlag.EMode.houshi && flags.selectAnimationListInfo.mode == HFlag.EMode.houshi) || (flags.mode == HFlag.EMode.houshi3P && flags.selectAnimationListInfo.mode == HFlag.EMode.houshi3P) || (flags.mode == HFlag.EMode.houshi3PMMF && flags.selectAnimationListInfo.mode == HFlag.EMode.houshi3PMMF))
					{
						flags.click = HFlag.ClickKind.insert;
					}
					else
					{
						flags.click = HFlag.ClickKind.actionChange;
					}
					int num = ((component2.info.mode == HFlag.EMode.houshi3P || component2.info.mode == HFlag.EMode.sonyu3P) ? voicePlayShuffle[1].Get() : 0);
					if (component2.info.mode == HFlag.EMode.houshi3PMMF || component2.info.mode == HFlag.EMode.sonyu3PMMF)
					{
						flags.voice.playVoices[0] = 29;
					}
					else if (component2.info.mode != HFlag.EMode.houshi3P && component2.info.mode != HFlag.EMode.sonyu3P)
					{
						flags.voice.playVoices[num] = ((component2.info.mode == HFlag.EMode.aibu) ? 6 : ((component2.info.mode == HFlag.EMode.houshi || component2.info.mode == HFlag.EMode.houshi3PMMF) ? 7 : 8));
					}
					else if (component2.info.mode != HFlag.EMode.none)
					{
						if (component2.info.id % 2 == flags.nowAnimationInfo.id % 2)
						{
							flags.voice.playVoices[num] = ((component2.info.mode == HFlag.EMode.houshi3P) ? 17 : 18);
						}
						else
						{
							flags.voice.playVoices[num] = ((component2.info.mode == HFlag.EMode.houshi3P) ? 19 : 20);
						}
						int num2 = num ^ 1;
						if (voice.nowVoices[num2].state == HVoiceCtrl.VoiceKind.voice && Voice.IsPlay(flags.transVoiceMouth[num2]))
						{
							Voice.Stop(flags.transVoiceMouth[num2]);
						}
					}
				}
				else
				{
					flags.click = HFlag.ClickKind.actionChange;
					int[] array = new int[10] { 3, 4, 5, -1, -1, -1, 15, 16, 27, 28 };
					int num3 = ((component2.info.mode == HFlag.EMode.houshi3P || component2.info.mode == HFlag.EMode.sonyu3P) ? voicePlayShuffle[0].Get() : 0);
					if (component2.info.mode != HFlag.EMode.houshi3P && component2.info.mode != HFlag.EMode.sonyu3P)
					{
						flags.voice.playVoices[num3] = array[(int)component2.info.mode];
					}
					else if (component2.info.mode != HFlag.EMode.none)
					{
						if (component2.info.id % 2 == flags.nowAnimationInfo.id % 2)
						{
							if (component2.info.mode != HFlag.EMode.houshi3P)
							{
								flags.voice.playVoices[num3] = 16;
							}
							else
							{
								flags.voice.playVoices[num3] = ((component2.info.kindHoushi == 1) ? 15 : 24);
							}
						}
						else if (component2.info.mode != HFlag.EMode.houshi3P)
						{
							flags.voice.playVoices[num3] = 23;
						}
						else
						{
							flags.voice.playVoices[num3] = ((component2.info.kindHoushi == 1) ? 22 : 25);
						}
						int num4 = num3 ^ 1;
						if (voice.nowVoices[num4].state == HVoiceCtrl.VoiceKind.voice && Voice.IsPlay(flags.transVoiceMouth[num4]))
						{
							Voice.Stop(flags.transVoiceMouth[num4]);
						}
					}
				}
			}
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	public void OnChangeHoushiPose(GameObject objClick, int _pose)
	{
		if (null != objClick)
		{
			flags.poseHoushi = _pose;
		}
	}

	public bool IsSpriteAciotn()
	{
		if (Scene.IsNowLoading || Scene.IsNowLoadingFade || (Scene.AddSceneName != string.Empty && Scene.AddSceneName != "HProc"))
		{
			return false;
		}
		return true;
	}

	private bool SetSubMenuEnable(bool _enable)
	{
		for (int i = 0; i < menuActionSub.GetCount(); i++)
		{
			if (i == 4 || i == 7)
			{
				continue;
			}
			GameObject putParent = menuActionSub.GetPutParent(i);
			for (int j = 0; j < putParent.transform.childCount; j++)
			{
				Toggle component = putParent.transform.GetChild(j).GetComponent<Toggle>();
				if ((bool)component)
				{
					component.interactable = _enable;
				}
			}
		}
		return true;
	}

	public void SetHelpSprite()
	{
		if ((bool)autoDisableFirstHHelp)
		{
			autoDisableFirstHHelp.FadeStart();
		}
	}

	private void FemaleDressSubMenuProc(int _female, int _kind)
	{
		(new Action<int>[3] { FemaleDressSubMenuClothProc, FemaleDressSubMenuAccessoryProc, FemaleDressSubMenuCoordinateProc })[_kind](_female);
	}

	private void FemaleDressSubMenuClothProc(int _female)
	{
		for (int i = 0; i < 7; i++)
		{
			bool active = females[_female].IsClothesStateKind(i);
			lstMultipleFemaleDressButton[_female].dress.SetActive(active, i);
		}
		lstMultipleFemaleDressButton[_female].dress.SetActive(females[_female].IsShoesStateKind(), 7);
	}

	private void FemaleDressSubMenuAccessoryProc(int _female)
	{
		for (int i = 0; i < 20; i++)
		{
			bool flag = females[_female].IsAccessory(i);
			lstMultipleFemaleDressButton[_female].accessory.SetActive(flag, i);
			if (flag)
			{
				ListInfoComponent component = females[_female].objAccessory[i].GetComponent<ListInfoComponent>();
				GameObject gameObject = lstMultipleFemaleDressButton[_female].accessory.GetObject(i);
				TextMeshProUGUI textMeshProUGUI = null;
				if ((bool)gameObject)
				{
					textMeshProUGUI = gameObject.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
				}
				if ((bool)component && (bool)textMeshProUGUI)
				{
					textMeshProUGUI.text = component.data.Name;
				}
			}
		}
	}

	private void FemaleDressSubMenuCoordinateProc(int _female)
	{
	}

	public void FadeState(FadeKind _kind, float _timeFade = -1f)
	{
		isFade = true;
		timeFadeTime = 0f;
		imageFade.enabled = true;
		if (_timeFade < 0f)
		{
			timeFade = ((_kind != FadeKind.OutIn) ? timeFadeBase : (timeFadeBase * 2f));
		}
		else
		{
			timeFade = ((_kind != FadeKind.OutIn) ? _timeFade : (_timeFade * 2f));
		}
		kindFade = _kind;
		switch (kindFade)
		{
		case FadeKind.Out:
			kindFadeProc = FadeKindProc.Out;
			break;
		case FadeKind.In:
			kindFadeProc = FadeKindProc.In;
			break;
		case FadeKind.OutIn:
			kindFadeProc = FadeKindProc.OutIn;
			break;
		}
	}

	public FadeKindProc GetFadeKindProc()
	{
		return kindFadeProc;
	}

	private bool FadeProc()
	{
		if (!imageFade)
		{
			return false;
		}
		if (!isFade)
		{
			return false;
		}
		timeFadeTime += Time.deltaTime;
		Color color = imageFade.color;
		float time = Mathf.Clamp01(timeFadeTime / timeFade);
		time = fadeAnimation.Evaluate(time);
		switch (kindFade)
		{
		case FadeKind.Out:
			color.a = time;
			break;
		case FadeKind.In:
			color.a = 1f - time;
			break;
		case FadeKind.OutIn:
			color.a = Mathf.Sin((float)Math.PI / 180f * Mathf.Lerp(0f, 180f, time));
			break;
		}
		imageFade.color = color;
		if (time >= 1f)
		{
			isFade = false;
			switch (kindFade)
			{
			case FadeKind.Out:
				kindFadeProc = FadeKindProc.OutEnd;
				break;
			case FadeKind.In:
				kindFadeProc = FadeKindProc.InEnd;
				imageFade.enabled = false;
				break;
			case FadeKind.OutIn:
				kindFadeProc = FadeKindProc.OutInEnd;
				imageFade.enabled = false;
				break;
			}
		}
		return true;
	}

	public bool SetHoushi3PDarkStart()
	{
		houshi3PDark.categoryActionButton.SetActive(_active: false);
		objCommonAibuIcon.SetActive(value: false);
		if ((bool)btnImmediatelyFinishFemale)
		{
			btnImmediatelyFinishFemale.gameObject.SetActive(value: false);
		}
		if ((bool)btnImmediatelyFinishMale)
		{
			btnImmediatelyFinishMale.gameObject.SetActive(value: true);
		}
		return true;
	}

	public bool SetHoushi3PDarkActionButtonActive(bool _active, int _array = -1)
	{
		houshi3PDark.categoryActionButton.SetActive(_active, _array);
		return true;
	}

	public bool SetHoushi3PDarkAutoFinish(bool _force = false)
	{
		if (!females[0].getAnimatorStateInfo(0).IsName("OLoop") && !_force)
		{
			return false;
		}
		int num = ((flags.selectAnimationListInfo != null) ? flags.selectAnimationListInfo.numCtrl : flags.nowAnimationInfo.numCtrl);
		houshi3PDark.categoryActionButton.SetActive(num == 1 && !houshi3PDark.btAutoFinishSpriteCtl.now, 7);
		houshi3PDark.categoryActionButton.SetActive(num != 1 || houshi3PDark.btAutoFinishSpriteCtl.now, 8);
		return true;
	}

	public bool IsHoushi3PDarkAutoFinish()
	{
		return houshi3PDark.btAutoFinishSpriteCtl.now;
	}

	public void Houshi3PDarkInitRely()
	{
		houshi3PDark.tglRely.isOn = false;
	}

	public bool Houshi3PDarkProc()
	{
		bool flag = false;
		if (females.Count > 0 && (bool)females[0])
		{
			AnimatorStateInfo animatorStateInfo = females[0].getAnimatorStateInfo(0);
			flag = (animatorStateInfo.IsName("Idle") || animatorStateInfo.IsName("WLoop") || animatorStateInfo.IsName("SLoop") || animatorStateInfo.IsName("OLoop") || animatorStateInfo.IsName("OUT_A") || animatorStateInfo.IsName("Drink_A") || animatorStateInfo.IsName("Vomit_A")) && !flags.voiceWait;
			SetSubMenuEnable(flag);
			btnEnd.interactable = flag;
			houshi3PDark.btAutoFinish.interactable = !flags.voiceWait;
			houshi3PDark.categoryActionButton.SetEnable(!flags.voiceWait);
			if ((bool)btnTrespassing)
			{
				btnTrespassing.interactable = flag;
			}
			menuMain.SetEnable(flag, 2);
			houshi3PDark.btAutoFinish.gameObject.SetActive(flags.nowAnimationInfo.numCtrl == 1);
			bool flag2 = animatorStateInfo.IsName("Idle") || animatorStateInfo.IsName("WLoop") || animatorStateInfo.IsName("SLoop") || animatorStateInfo.IsName("OLoop");
			Color color = houshi3PDark.imagePad.color;
			color.a = (flag2 ? 1f : 0.3f);
			houshi3PDark.imagePad.color = color;
		}
		if ((bool)btnImmediatelyFinishFemale)
		{
			btnImmediatelyFinishMale.interactable = flag;
		}
		return true;
	}

	public bool SetSonyu3PDarkStart()
	{
		sonyu3PDark.categoryActionButton.SetActive(_active: false);
		sonyu3PDark.categoryActionButton.SetActive(_active: true, 14);
		sonyu3PDark.categoryActionButton.SetActive(_active: true, 15);
		objCommonAibuIcon.SetActive(value: false);
		if ((bool)btnImmediatelyFinishFemale)
		{
			btnImmediatelyFinishFemale.gameObject.SetActive(value: true);
		}
		if ((bool)btnImmediatelyFinishMale)
		{
			btnImmediatelyFinishMale.gameObject.SetActive(value: true);
		}
		return true;
	}

	public bool SetSonyu3PDarkActionButtonActive(bool _active, int _array = -1)
	{
		sonyu3PDark.categoryActionButton.SetActive(_active, (_array == -1) ? _array : (_array + 14));
		return true;
	}

	public bool IsSonyu3PDarkAutoFinish()
	{
		return sonyu3PDark.btAutoFinishSpriteCtl.now;
	}

	public bool SetCondom3PDark(bool _isOn)
	{
		sonyu3PDark.isCondom = _isOn;
		sonyu3PDark.buttonCondomChange.OnChangeValue(_isOn);
		return true;
	}

	public bool Sonyu3PDarkProc()
	{
		bool flag = false;
		if (females.Count > 0 && (bool)females[0])
		{
			AnimatorStateInfo animatorStateInfo = females[0].getAnimatorStateInfo(0);
			flag = (animatorStateInfo.IsName("Idle") || animatorStateInfo.IsName("InsertIdle") || animatorStateInfo.IsName("WLoop") || animatorStateInfo.IsName("SLoop") || animatorStateInfo.IsName("IN_A") || animatorStateInfo.IsName("OUT_A") || animatorStateInfo.IsName("A_Idle") || animatorStateInfo.IsName("A_InsertIdle") || animatorStateInfo.IsName("A_WLoop") || animatorStateInfo.IsName("A_SLoop") || animatorStateInfo.IsName("A_IN_A") || animatorStateInfo.IsName("A_OUT_A")) && !flags.voiceWait;
			SetSubMenuEnable(flag);
			btnEnd.interactable = flag;
			sonyu3PDark.categoryActionButton.SetEnable(!flags.voiceWait);
			sonyu3PDark.btAutoFinish.interactable = !flags.voiceWait;
			if ((bool)btnTrespassing)
			{
				btnTrespassing.interactable = flag;
			}
			menuMain.SetEnable(flag, 2);
			sonyu3PDark.buttonCondom.interactable = (animatorStateInfo.IsName("Idle") || animatorStateInfo.IsName("OUT_A") || animatorStateInfo.IsName("A_Idle") || animatorStateInfo.IsName("A_OUT_A")) && !flags.voiceWait;
			bool flag2 = ((animatorStateInfo.IsName("WLoop") || animatorStateInfo.IsName("SLoop") || animatorStateInfo.IsName("A_WLoop") || animatorStateInfo.IsName("A_SLoop")) && flags.gaugeMale >= 70f) || flags.isDebug;
			sonyu3PDark.categoryActionButton.SetActive(flag2 && !sonyu3PDark.btAutoFinishSpriteCtl.now, 18);
			sonyu3PDark.categoryActionButton.SetActive(flag2 && sonyu3PDark.btAutoFinishSpriteCtl.now, 19);
			bool flag3 = animatorStateInfo.IsName("InsertIdle") || animatorStateInfo.IsName("WLoop") || animatorStateInfo.IsName("SLoop") || animatorStateInfo.IsName("IN_A") || animatorStateInfo.IsName("A_InsertIdle") || animatorStateInfo.IsName("A_WLoop") || animatorStateInfo.IsName("A_SLoop") || animatorStateInfo.IsName("A_IN_A");
			Color color = sonyu3PDark.imagePad.color;
			color.a = (flag3 ? 1f : 0.3f);
			sonyu3PDark.imagePad.color = color;
		}
		if ((bool)btnImmediatelyFinishFemale)
		{
			btnImmediatelyFinishFemale.interactable = flag;
		}
		if ((bool)btnImmediatelyFinishMale)
		{
			btnImmediatelyFinishMale.interactable = flag;
		}
		return true;
	}

	public bool SetHoushi3PStart()
	{
		houshi3P.categoryActionButton.SetActive(_active: false);
		objCommonAibuIcon.SetActive(value: false);
		if ((bool)btnImmediatelyFinishFemale)
		{
			btnImmediatelyFinishFemale.gameObject.SetActive(value: false);
		}
		if ((bool)btnImmediatelyFinishMale)
		{
			btnImmediatelyFinishMale.gameObject.SetActive(value: true);
		}
		return true;
	}

	public bool SetHoushi3PActionButtonActive(bool _active, int _array = -1)
	{
		houshi3P.categoryActionButton.SetActive(_active, _array);
		return true;
	}

	public bool SetHoushi3PAutoFinish(bool _force = false)
	{
		if (!females[0].getAnimatorStateInfo(0).IsName("OLoop") && !_force)
		{
			return false;
		}
		int num = ((flags.selectAnimationListInfo != null) ? flags.selectAnimationListInfo.numCtrl : flags.nowAnimationInfo.numCtrl);
		houshi3P.categoryActionButton.SetActive((num == 1 || num == 3) && !houshi3P.btAutoFinishSpriteCtl.now, 0);
		houshi3P.categoryActionButton.SetActive((num != 1 && num != 3) || houshi3P.btAutoFinishSpriteCtl.now, 1);
		return true;
	}

	public bool Is3PHoushiAutoFinish()
	{
		return houshi3P.btAutoFinishSpriteCtl.now;
	}

	public void Houshi3PInitRely()
	{
		houshi3P.tglRely.isOn = false;
	}

	public bool Houshi3PProc()
	{
		bool flag = false;
		if (females.Count > 0 && (bool)females[0])
		{
			AnimatorStateInfo animatorStateInfo = females[0].getAnimatorStateInfo(0);
			flag = (animatorStateInfo.IsName("Idle") || animatorStateInfo.IsName("WLoop") || animatorStateInfo.IsName("SLoop") || animatorStateInfo.IsName("OLoop") || animatorStateInfo.IsName("OUT_A") || animatorStateInfo.IsName("Drink_A") || animatorStateInfo.IsName("Vomit_A")) && !flags.voiceWait;
			SetSubMenuEnable(flag);
			btnEnd.interactable = flag;
			houshi3P.btAutoFinish.interactable = !flags.voiceWait;
			houshi3P.categoryActionButton.SetEnable(!flags.voiceWait);
			menuMain.SetEnable(flag, 2);
			houshi3P.btAutoFinish.gameObject.SetActive(flags.nowAnimationInfo.numCtrl == 1 || flags.nowAnimationInfo.numCtrl == 3);
			bool flag2 = animatorStateInfo.IsName("Idle") || animatorStateInfo.IsName("WLoop") || animatorStateInfo.IsName("SLoop") || animatorStateInfo.IsName("OLoop");
			Color color = houshi3P.imagePad.color;
			color.a = (flag2 ? 1f : 0.3f);
			houshi3P.imagePad.color = color;
		}
		if ((bool)btnImmediatelyFinishFemale)
		{
			btnImmediatelyFinishMale.interactable = flag;
		}
		return true;
	}

	public bool SetSonyu3PStart()
	{
		sonyu3P.categoryActionButton.SetActive(_active: false);
		int num = ((flags.selectAnimationListInfo == null) ? (flags.nowAnimationInfo.isFemaleInitiative ? 1 : 0) : (flags.selectAnimationListInfo.isFemaleInitiative ? 1 : 0)) * 7;
		sonyu3P.categoryActionButton.SetActive(_active: true, num);
		sonyu3P.categoryActionButton.SetActive(_active: true, 1 + num);
		bool active = ((flags.selectAnimationListInfo != null) ? flags.selectAnimationListInfo.paramFemale.isAnal : flags.nowAnimationInfo.paramFemale.isAnal);
		sonyu3P.categoryActionButton.SetActive(active, 2 + num);
		sonyu3P.categoryActionButton.SetActive(active, 3 + num);
		objCommonAibuIcon.SetActive(value: false);
		if ((bool)btnImmediatelyFinishFemale)
		{
			btnImmediatelyFinishFemale.gameObject.SetActive(value: true);
		}
		if ((bool)btnImmediatelyFinishMale)
		{
			btnImmediatelyFinishMale.gameObject.SetActive(value: true);
		}
		return true;
	}

	public bool SetSonyu3PActionButtonActive(bool _active, int _array = -1)
	{
		int num = ((flags.selectAnimationListInfo == null) ? (flags.nowAnimationInfo.isFemaleInitiative ? 1 : 0) : (flags.selectAnimationListInfo.isFemaleInitiative ? 1 : 0)) * 7;
		sonyu3P.categoryActionButton.SetActive(_active, (_array == -1) ? _array : (_array + num));
		return true;
	}

	public bool IsSonyu3PAutoFinish()
	{
		return sonyu3P.btAutoFinishSpriteCtl.now;
	}

	public bool SetCondom3P(bool _isOn)
	{
		sonyu3P.isCondom = _isOn;
		sonyu3P.buttonCondomChange.OnChangeValue(_isOn);
		return true;
	}

	public bool Sonyu3PProc()
	{
		bool flag = false;
		if (females.Count > 0 && (bool)females[0])
		{
			AnimatorStateInfo animatorStateInfo = females[0].getAnimatorStateInfo(0);
			flag = (animatorStateInfo.IsName("Idle") || animatorStateInfo.IsName("InsertIdle") || animatorStateInfo.IsName("WLoop") || animatorStateInfo.IsName("SLoop") || animatorStateInfo.IsName("IN_A") || animatorStateInfo.IsName("OUT_A") || animatorStateInfo.IsName("A_Idle") || animatorStateInfo.IsName("A_InsertIdle") || animatorStateInfo.IsName("A_WLoop") || animatorStateInfo.IsName("A_SLoop") || animatorStateInfo.IsName("A_IN_A") || animatorStateInfo.IsName("A_OUT_A")) && !flags.voiceWait;
			SetSubMenuEnable(flag);
			btnEnd.interactable = flag;
			sonyu3P.categoryActionButton.SetEnable(!flags.voiceWait);
			sonyu3P.btAutoFinish.interactable = !flags.voiceWait;
			menuMain.SetEnable(flag, 2);
			sonyu3P.buttonCondom.interactable = (animatorStateInfo.IsName("Idle") || animatorStateInfo.IsName("OUT_A") || animatorStateInfo.IsName("A_Idle") || animatorStateInfo.IsName("A_OUT_A")) && !flags.voiceWait;
			int num = ((flags.selectAnimationListInfo == null) ? (flags.nowAnimationInfo.isFemaleInitiative ? 1 : 0) : (flags.selectAnimationListInfo.isFemaleInitiative ? 1 : 0)) * 7;
			bool flag2 = ((animatorStateInfo.IsName("WLoop") || animatorStateInfo.IsName("SLoop") || animatorStateInfo.IsName("A_WLoop") || animatorStateInfo.IsName("A_SLoop")) && flags.gaugeMale >= 70f) || flags.isDebug;
			sonyu3P.categoryActionButton.SetActive(flag2 && !sonyu3P.btAutoFinishSpriteCtl.now, 4 + num);
			sonyu3P.categoryActionButton.SetActive(flag2 && sonyu3P.btAutoFinishSpriteCtl.now, 5 + num);
			bool flag3 = animatorStateInfo.IsName("InsertIdle") || animatorStateInfo.IsName("WLoop") || animatorStateInfo.IsName("SLoop") || animatorStateInfo.IsName("IN_A") || animatorStateInfo.IsName("A_InsertIdle") || animatorStateInfo.IsName("A_WLoop") || animatorStateInfo.IsName("A_SLoop") || animatorStateInfo.IsName("A_IN_A");
			Color color = sonyu3P.imagePad.color;
			color.a = (flag3 ? 1f : 0.3f);
			sonyu3P.imagePad.color = color;
		}
		if ((bool)btnImmediatelyFinishFemale)
		{
			btnImmediatelyFinishFemale.interactable = flag;
		}
		if ((bool)btnImmediatelyFinishMale)
		{
			btnImmediatelyFinishMale.interactable = flag;
		}
		return true;
	}

	public bool SetAibuStart()
	{
		objCommonAibuIcon.SetActive(value: true);
		if ((bool)btnImmediatelyFinishFemale)
		{
			btnImmediatelyFinishFemale.gameObject.SetActive(value: true);
		}
		if ((bool)btnImmediatelyFinishMale)
		{
			btnImmediatelyFinishMale.gameObject.SetActive(value: false);
		}
		return true;
	}

	public bool SetAibuActionButtonActive(bool _active, int _array = -1)
	{
		return true;
	}

	public bool SetAibuAutoFinish(bool _force = false)
	{
		return true;
	}

	public bool AibuProc()
	{
		bool flag = false;
		if (females.Count > 0 && (bool)females[0])
		{
			AnimatorStateInfo animatorStateInfo = females[0].getAnimatorStateInfo(0);
			flag = (animatorStateInfo.IsName("Idle") || animatorStateInfo.IsName("M_Idle") || animatorStateInfo.IsName("S_Idle") || animatorStateInfo.IsName("A_Idle") || animatorStateInfo.IsName("Orgasm_A")) && !flags.voiceWait;
			SetSubMenuEnable(flag);
			menuMain.SetEnable(flag, 2);
			btnEnd.interactable = flag;
			if ((bool)btnTrespassing)
			{
				btnTrespassing.interactable = flag;
			}
		}
		if ((bool)btnImmediatelyFinishFemale)
		{
			btnImmediatelyFinishFemale.interactable = flag;
		}
		return true;
	}

	public bool SetHoushiStart()
	{
		houshi.categoryActionButton.SetActive(_active: false);
		objCommonAibuIcon.SetActive(value: true);
		if ((bool)btnImmediatelyFinishFemale)
		{
			btnImmediatelyFinishFemale.gameObject.SetActive(value: false);
		}
		if ((bool)btnImmediatelyFinishMale)
		{
			btnImmediatelyFinishMale.gameObject.SetActive(value: true);
		}
		return true;
	}

	public bool SetHoushiActionButtonActive(bool _active, int _array = -1)
	{
		houshi.categoryActionButton.SetActive(_active, _array);
		return true;
	}

	public bool SetHoushiAutoFinish(bool _force = false)
	{
		if (!females[0].getAnimatorStateInfo(0).IsName("OLoop") && !_force)
		{
			return false;
		}
		int num = ((flags.selectAnimationListInfo != null) ? flags.selectAnimationListInfo.numCtrl : flags.nowAnimationInfo.numCtrl);
		houshi.categoryActionButton.SetActive(num == 1 && !houshi.btAutoFinishSpriteCtl.now, 0);
		houshi.categoryActionButton.SetActive(num != 1 || houshi.btAutoFinishSpriteCtl.now, 1);
		return true;
	}

	public bool IsHoushiAutoFinish()
	{
		return houshi.btAutoFinishSpriteCtl.now;
	}

	public void HoushiInitRely()
	{
		houshi.tglRely.isOn = false;
	}

	public bool HoushiProc()
	{
		bool flag = false;
		if (females.Count > 0 && (bool)females[0])
		{
			AnimatorStateInfo animatorStateInfo = females[0].getAnimatorStateInfo(0);
			flag = (animatorStateInfo.IsName("Idle") || animatorStateInfo.IsName("WLoop") || animatorStateInfo.IsName("SLoop") || animatorStateInfo.IsName("OLoop") || animatorStateInfo.IsName("OUT_A") || animatorStateInfo.IsName("Drink_A") || animatorStateInfo.IsName("Vomit_A")) && !flags.voiceWait;
			SetSubMenuEnable(flag);
			btnEnd.interactable = flag;
			houshi.btAutoFinish.interactable = !flags.voiceWait;
			houshi.categoryActionButton.SetEnable(!flags.voiceWait);
			if ((bool)btnTrespassing)
			{
				btnTrespassing.interactable = flag;
			}
			menuMain.SetEnable(flag, 2);
			houshi.btAutoFinish.gameObject.SetActive(flags.nowAnimationInfo.numCtrl == 1);
			bool flag2 = animatorStateInfo.IsName("Idle") || animatorStateInfo.IsName("WLoop") || animatorStateInfo.IsName("SLoop") || animatorStateInfo.IsName("OLoop");
			Color color = houshi.imagePad.color;
			color.a = (flag2 ? 1f : 0.3f);
			houshi.imagePad.color = color;
		}
		if ((bool)btnImmediatelyFinishFemale)
		{
			btnImmediatelyFinishMale.interactable = flag;
		}
		return true;
	}

	public bool SetLesbianStart()
	{
		objCommonAibuIcon.SetActive(value: false);
		if ((bool)btnImmediatelyFinishFemale)
		{
			btnImmediatelyFinishFemale.gameObject.SetActive(flags.isFreeH);
		}
		if ((bool)btnImmediatelyFinishMale)
		{
			btnImmediatelyFinishMale.gameObject.SetActive(value: false);
		}
		return true;
	}

	public bool LesbianProc()
	{
		bool flag = false;
		if (females.Count > 0 && (bool)females[0])
		{
			if (flags.isFreeH)
			{
				AnimatorStateInfo animatorStateInfo = females[0].getAnimatorStateInfo(0);
				flag = (animatorStateInfo.IsName("WLoop") || animatorStateInfo.IsName("MLoop") || animatorStateInfo.IsName("SLoop") || animatorStateInfo.IsName("OLoop") || animatorStateInfo.IsName("Orgasm_B")) && !flags.voiceWait;
				menuMain.SetEnable(flag, 2);
			}
			btnEnd.interactable = true;
			if ((bool)btnTrespassing)
			{
				btnTrespassing.interactable = true;
			}
		}
		if ((bool)btnImmediatelyFinishFemale)
		{
			btnImmediatelyFinishFemale.interactable = flag;
		}
		return true;
	}

	public bool SetMasturbationStart()
	{
		objCommonAibuIcon.SetActive(value: false);
		if ((bool)btnImmediatelyFinishFemale)
		{
			btnImmediatelyFinishFemale.gameObject.SetActive(flags.isFreeH);
		}
		if ((bool)btnImmediatelyFinishMale)
		{
			btnImmediatelyFinishMale.gameObject.SetActive(value: false);
		}
		return true;
	}

	public bool MasturbationProc()
	{
		bool flag = false;
		if (females.Count > 0 && (bool)females[0])
		{
			if (flags.isFreeH)
			{
				AnimatorStateInfo animatorStateInfo = females[0].getAnimatorStateInfo(0);
				flag = (animatorStateInfo.IsName("WLoop") || animatorStateInfo.IsName("MLoop") || animatorStateInfo.IsName("SLoop") || animatorStateInfo.IsName("OLoop") || animatorStateInfo.IsName("Orgasm_B")) && !flags.voiceWait;
				menuMain.SetEnable(flag, 2);
			}
			btnEnd.interactable = true;
			if ((bool)btnTrespassing)
			{
				btnTrespassing.interactable = true;
			}
		}
		if ((bool)btnImmediatelyFinishFemale)
		{
			btnImmediatelyFinishFemale.interactable = flag;
		}
		return true;
	}

	public bool SetPeepingStart()
	{
		objCommonAibuIcon.SetActive(value: false);
		if ((bool)btnImmediatelyFinishFemale)
		{
			btnImmediatelyFinishFemale.gameObject.SetActive(value: false);
		}
		if ((bool)btnImmediatelyFinishMale)
		{
			btnImmediatelyFinishMale.gameObject.SetActive(value: false);
		}
		return true;
	}

	public bool PeepingProc()
	{
		if (females.Count > 0 && (bool)females[0])
		{
			SetSubMenuEnable(_enable: true);
			btnEnd.interactable = true;
		}
		return true;
	}

	public bool SetSonyuStart()
	{
		sonyu.categoryActionButton.SetActive(_active: false);
		int num = ((flags.selectAnimationListInfo == null) ? (flags.nowAnimationInfo.isFemaleInitiative ? 1 : 0) : (flags.selectAnimationListInfo.isFemaleInitiative ? 1 : 0)) * 7;
		sonyu.categoryActionButton.SetActive(_active: true, num);
		sonyu.categoryActionButton.SetActive(_active: true, 1 + num);
		bool active = ((flags.selectAnimationListInfo != null) ? flags.selectAnimationListInfo.paramFemale.isAnal : flags.nowAnimationInfo.paramFemale.isAnal);
		sonyu.categoryActionButton.SetActive(active, 2 + num);
		sonyu.categoryActionButton.SetActive(active, 3 + num);
		objCommonAibuIcon.SetActive(value: true);
		if ((bool)btnImmediatelyFinishFemale)
		{
			btnImmediatelyFinishFemale.gameObject.SetActive(value: true);
		}
		if ((bool)btnImmediatelyFinishMale)
		{
			btnImmediatelyFinishMale.gameObject.SetActive(value: true);
		}
		return true;
	}

	public bool SetSonyuActionButtonActive(bool _active, int _array = -1)
	{
		int num = ((flags.selectAnimationListInfo == null) ? (flags.nowAnimationInfo.isFemaleInitiative ? 1 : 0) : (flags.selectAnimationListInfo.isFemaleInitiative ? 1 : 0)) * 7;
		sonyu.categoryActionButton.SetActive(_active, (_array == -1) ? _array : (_array + num));
		return true;
	}

	public bool IsSonyuAutoFinish()
	{
		return sonyu.btAutoFinishSpriteCtl.now;
	}

	public bool SetCondom(bool _isOn)
	{
		sonyu.isCondom = _isOn;
		sonyu.buttonCondomChange.OnChangeValue(_isOn);
		return true;
	}

	public bool SonyuProc()
	{
		bool flag = false;
		if (females.Count > 0 && (bool)females[0])
		{
			AnimatorStateInfo animatorStateInfo = females[0].getAnimatorStateInfo(0);
			flag = (animatorStateInfo.IsName("Idle") || animatorStateInfo.IsName("InsertIdle") || animatorStateInfo.IsName("WLoop") || animatorStateInfo.IsName("SLoop") || animatorStateInfo.IsName("IN_A") || animatorStateInfo.IsName("OUT_A") || animatorStateInfo.IsName("A_Idle") || animatorStateInfo.IsName("A_InsertIdle") || animatorStateInfo.IsName("A_WLoop") || animatorStateInfo.IsName("A_SLoop") || animatorStateInfo.IsName("A_IN_A") || animatorStateInfo.IsName("A_OUT_A")) && !flags.voiceWait;
			SetSubMenuEnable(flag);
			btnEnd.interactable = flag;
			sonyu.categoryActionButton.SetEnable(!flags.voiceWait);
			sonyu.btAutoFinish.interactable = !flags.voiceWait;
			if ((bool)btnTrespassing)
			{
				btnTrespassing.interactable = flag;
			}
			menuMain.SetEnable(flag, 2);
			sonyu.buttonCondom.interactable = (animatorStateInfo.IsName("Idle") || animatorStateInfo.IsName("OUT_A") || animatorStateInfo.IsName("A_Idle") || animatorStateInfo.IsName("A_OUT_A")) && !flags.voiceWait;
			int num = ((flags.selectAnimationListInfo == null) ? (flags.nowAnimationInfo.isFemaleInitiative ? 1 : 0) : (flags.selectAnimationListInfo.isFemaleInitiative ? 1 : 0)) * 7;
			bool flag2 = (animatorStateInfo.IsName("WLoop") || animatorStateInfo.IsName("SLoop") || animatorStateInfo.IsName("A_WLoop") || animatorStateInfo.IsName("A_SLoop")) && flags.gaugeMale >= 70f;
			sonyu.categoryActionButton.SetActive(flag2 && !sonyu.btAutoFinishSpriteCtl.now, 4 + num);
			sonyu.categoryActionButton.SetActive(flag2 && sonyu.btAutoFinishSpriteCtl.now, 5 + num);
			bool flag3 = animatorStateInfo.IsName("InsertIdle") || animatorStateInfo.IsName("WLoop") || animatorStateInfo.IsName("SLoop") || animatorStateInfo.IsName("IN_A") || animatorStateInfo.IsName("A_InsertIdle") || animatorStateInfo.IsName("A_WLoop") || animatorStateInfo.IsName("A_SLoop") || animatorStateInfo.IsName("A_IN_A");
			Color color = sonyu.imagePad.color;
			color.a = (flag3 ? 1f : 0.3f);
			sonyu.imagePad.color = color;
		}
		if ((bool)btnImmediatelyFinishFemale)
		{
			btnImmediatelyFinishFemale.interactable = flag;
		}
		if ((bool)btnImmediatelyFinishMale)
		{
			btnImmediatelyFinishMale.interactable = flag;
		}
		return true;
	}
}
