using System;
using System.Collections;
using System.Collections.Generic;
using AIChara;
using AIProject;
using AIProject.Scene;
using AIProject.UI;
using ConfigScene;
using Illusion.Component.UI.ColorPicker;
using IllusionUtility.GetUtility;
using Manager;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEx;

public class HSceneSprite : Singleton<HSceneSprite>
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

	public Toggle objGaugeLockF;

	public Toggle objGaugeLockM;

	public Image imageMGauge;

	public Image imageFGauge;

	public Color colorMGauge = Color.white;

	public Color colorFGauge = Color.white;

	private Color colorMGaugeDef;

	private Color colorFGaugeDef;

	[SerializeField]
	private ParticleSystem AtariEffect;

	public GameObject buttonEnd;

	public HSceneSpriteFinishCategory categoryFinish;

	public HSceneSpriteClothCondition objCloth;

	public HSceneSpriteAccessoryCondition objAccessory;

	public HSceneSpriteCoordinatesCard objClothCard;

	public HSceneSpriteChaChoice charaChoice;

	public HsceneSpriteTaiiCategory categoryMain;

	private RotationScroll CategoryScroll;

	[SerializeField]
	private ScrollCylinder MotionScroll;

	public Toggle tglLeaveItToYou;

	public GameObject objMotionListPanel;

	public GameObject objMotionListInstanceButton;

	public GameObject objMotionList;

	public float HpointSearchRange;

	public GameObject objHItem;

	public HSceneSpriteHitem HItemCtrl;

	public GameObject objLightCategory;

	public HSceneSpriteLightCategory categoryLightDir;

	public PickerRect colorPicker;

	public AnimationCurve fadeAnimation;

	public HScene.AnimationListInfo StartAnimInfo;

	public RawImage imageFade;

	public float timeFadeBase;

	public bool isFade;

	[SerializeField]
	private ParticleSystem FeelHitEffect3D;

	[SerializeField]
	private Vector3 FeelHitEffect3DOffSet;

	public HSceneSpriteCategories categories;

	public float fadeTime = 1f;

	private HSceneFlagCtrl ctrlFlag;

	private HScene hScene;

	private FadeKind kindFade;

	private FadeKindProc kindFadeProc;

	private float timeFade;

	private float timeFadeTime;

	private bool isLeaveItToYou;

	private ChaControl[] chaFemales;

	public bool usePoint;

	private List<HScene.AnimationListInfo>[] lstAnimInfo;

	public Button categoryMainButton;

	public Button hPointButton;

	[SerializeField]
	private GameObject[] MenuCategory = new GameObject[2];

	[SerializeField]
	private GameObject MenuCategorySub;

	[SerializeField]
	private GameObject GuageBase;

	private List<UnityEx.ValueTuple<HSceneManager.HEvent, int, HScene.StartMotion>> startList;

	private List<int> lstFinishVisible = new List<int>();

	private HScene.LightInfo[] infoMapLight = new HScene.LightInfo[2]
	{
		new HScene.LightInfo(),
		new HScene.LightInfo()
	};

	public Animator anim_f;

	public Animator anim_m;

	public int endFade = -1;

	[SerializeField]
	private CanvasGroup UIGroup;

	private HSceneManager hSceneManager;

	[SerializeField]
	private int PlayerSex;

	private bool[] canMainCategory = new bool[6];

	private float nowFadeTime;

	private HSceneNodePool hSceneMotionPool = new HSceneNodePool();

	private List<ScrollCylinderNode> hSceneScrollNodes = new List<ScrollCylinderNode>();

	private HPointCtrl hPointCtrl;

	[SerializeField]
	private Text MotionListLabel;

	[SerializeField]
	private GameObject SelectArea;

	private Image[] motionListImages;

	private Toggle motionListToggle;

	private IDisposable beforeChoice;

	public bool ChangeStart;

	private bool AllEquip = true;

	[SerializeField]
	private GameObject HelpBaseConfig;

	[SerializeField]
	private GameObject HelpBase;

	[SerializeField]
	private Text HelpTxt;

	private const string HelpTextDef = "エッチを開始する";

	private string[][] GuidTiming = new string[4][]
	{
		new string[2] { "Idle", "D_Idle" },
		new string[6] { "WLoop", "SLoop", "OLoop", "D_WLoop", "D_SLoop", "D_OLoop" },
		new string[2] { "Orgasm_IN_A", "D_Orgasm_IN_A" },
		new string[7] { "Orgasm_A", "Orgasm_OUT_A", "Drink_A", "Vomit_A", "OrgasmM_OUT_A", "D_Orgasm_A", "D_OrgasmM_OUT_A" }
	};

	private string[][] GuidTimingEtc = new string[4][]
	{
		new string[1] { "Idle" },
		new string[4] { "WLoop", "SLoop", "MLoop", "OLoop" },
		new string[0],
		new string[1] { "Orgasm_A" }
	};

	public readonly List<int> NonTokushuCheckIDs = new List<int> { 3, 13, 14 };

	private const string CheckmarkName = "Checkmark";

	private const string nowSelectName = "NowSelect";

	public IEnumerator Init()
	{
		SetHelpActive(_active: false);
		AtariEffect.Stop();
		FeelHitEffect3D.Stop();
		ctrlFlag = Singleton<HSceneFlagCtrl>.Instance;
		hScene = ctrlFlag.GetComponent<HScene>();
		hSceneManager = Singleton<HSceneManager>.Instance;
		MotionScroll.BlankSet(objMotionListInstanceButton.GetComponent<ScrollCylinderNode>());
		hSceneMotionPool.CreatePool(objMotionListInstanceButton, objMotionList.transform, 50);
		yield return null;
		colorMGaugeDef = imageMGauge.color;
		colorFGaugeDef = imageFGauge.color;
		categoryFinish.Init();
		charaChoice.Init();
		objCloth.Init();
		objAccessory.Init();
		objClothCard.Init();
		categoryMain.Init();
		CategoryScroll = categoryMain.GetHScroll();
		yield return HItemCtrl.Init();
		if (hSceneManager.Player != null)
		{
			PlayerSex = hSceneManager.Player.ChaControl.sex;
			if (PlayerSex == 1 && hSceneManager.bFutanari)
			{
				PlayerSex = 0;
			}
		}
		colorPicker.updateColorAction += delegate(Color color)
		{
			OnValueLightColorChanged(color);
		};
		endFade = -1;
		nowFadeTime = 0f;
		hScene.AtariEffect = AtariEffect;
		hScene.FeelHitEffect3D = FeelHitEffect3D;
		hScene.FeelHitEffect3DOffSet = FeelHitEffect3DOffSet;
		if (!hSceneManager.bMerchant)
		{
			if (!hSceneManager.IsHousingHEnter)
			{
				if (hSceneManager.EventKind == HSceneManager.HEvent.Normal)
				{
					yield return MapUIContainer.DrawOnceTutorialUI(5);
					PopupCommands(hSceneManager.isForce);
				}
				else
				{
					ctrlFlag.HBeforeCamera.gameObject.SetActive(value: false);
					if (PlayerSex == 0)
					{
						hScene.SetStartAnimationInfo(hSceneManager.EventKind);
					}
					else if (PlayerSex == 1)
					{
						hScene.SetStartAnimationInfo(hSceneManager.EventKind, 4);
					}
					hSceneManager.Player.CameraControl.HCamera = ctrlFlag.cameraCtrl;
					CommandProc();
					hSceneManager.Player.CameraControl.enabled = false;
					hSceneManager.Player.Controller.enabled = false;
					hSceneManager.Player.Animation.enabled = false;
					while (hScene.ctrlVoice.nowVoices[0].state == HVoiceCtrl.VoiceKind.startVoice)
					{
						yield return null;
					}
					Singleton<HSceneFlagCtrl>.Instance.BeforeHWait = false;
					nowFadeTime = 0f;
					if (hSceneManager.Player.CameraControl.Mode != CameraMode.H)
					{
						Vector3 position = hSceneManager.females[0].Position;
						GlobalMethod.setCameraBase(Singleton<HSceneFlagCtrl>.Instance.cameraCtrl, position, hSceneManager.females[0].Rotation.eulerAngles);
						hSceneManager.Player.CameraControl.Mode = CameraMode.H;
						ctrlFlag.HBeforeCamera.gameObject.SetActive(value: false);
					}
				}
			}
			else
			{
				ctrlFlag.HBeforeCamera.gameObject.SetActive(value: false);
				hScene.SetStartAnimationInfo(hSceneManager.EventKind);
				hSceneManager.Player.CameraControl.HCamera = ctrlFlag.cameraCtrl;
				CommandProc();
				hSceneManager.Player.CameraControl.enabled = false;
				hSceneManager.Player.Controller.enabled = false;
				hSceneManager.Player.Animation.enabled = false;
				while (hScene.ctrlVoice.nowVoices[0].state == HVoiceCtrl.VoiceKind.startVoice)
				{
					yield return null;
				}
				Singleton<HSceneFlagCtrl>.Instance.BeforeHWait = false;
				nowFadeTime = 0f;
				if (hSceneManager.Player.CameraControl.Mode != CameraMode.H)
				{
					Vector3 position2 = hSceneManager.females[0].Position;
					GlobalMethod.setCameraBase(Singleton<HSceneFlagCtrl>.Instance.cameraCtrl, position2, hSceneManager.females[0].Rotation.eulerAngles);
					hSceneManager.Player.CameraControl.Mode = CameraMode.H;
					ctrlFlag.HBeforeCamera.gameObject.SetActive(value: false);
				}
			}
		}
		else
		{
			ctrlFlag.HBeforeCamera.gameObject.SetActive(value: false);
			hScene.SetStartAnimationInfoM(hSceneManager.EventKind);
			hSceneManager.Player.CameraControl.HCamera = ctrlFlag.cameraCtrl;
			CommandProc();
			hSceneManager.Player.CameraControl.enabled = false;
			hSceneManager.Player.Controller.enabled = false;
			hSceneManager.Player.Animation.enabled = false;
			while (hScene.ctrlVoice.nowVoices[0].state == HVoiceCtrl.VoiceKind.startVoice)
			{
				yield return null;
			}
			Singleton<HSceneFlagCtrl>.Instance.BeforeHWait = false;
			nowFadeTime = 0f;
			if (hSceneManager.Player.CameraControl.Mode != CameraMode.H)
			{
				Vector3 position3 = hSceneManager.females[0].Position;
				GlobalMethod.setCameraBase(Singleton<HSceneFlagCtrl>.Instance.cameraCtrl, position3, hSceneManager.females[0].Rotation.eulerAngles);
				hSceneManager.Player.CameraControl.Mode = CameraMode.H;
				ctrlFlag.HBeforeCamera.gameObject.SetActive(value: false);
			}
		}
		hPointCtrl = Singleton<HPointCtrl>.Instance;
	}

	private void Update()
	{
		if (chaFemales == null)
		{
			return;
		}
		for (int i = 0; i < MenuCategory.Length; i++)
		{
			MenuCategory[i].SetActive(Config.HData.MenuIcon);
		}
		MenuCategorySub.SetActive(Config.HData.MenuIcon);
		GuageBase.SetActive(Config.HData.FeelingGauge);
		HelpBaseActive(Config.HData.ActionGuide);
		if (imageFGauge != null)
		{
			imageFGauge.fillAmount = ctrlFlag.feel_f;
			if (ctrlFlag.feel_f >= 0.7f)
			{
				imageFGauge.color = colorFGauge;
			}
			else
			{
				imageFGauge.color = colorFGaugeDef;
			}
		}
		if (imageMGauge != null)
		{
			imageMGauge.fillAmount = ctrlFlag.feel_m;
			if (ctrlFlag.feel_m >= 0.7f)
			{
				imageMGauge.color = colorMGauge;
			}
			else
			{
				imageMGauge.color = colorMGaugeDef;
			}
		}
		FadeProc();
		int item = ctrlFlag.nowAnimationInfo.ActionCtrl.Item1;
		int item2 = ctrlFlag.nowAnimationInfo.ActionCtrl.Item2;
		if (anim_f != null && anim_f.isActiveAndEnabled)
		{
			AnimatorStateInfo currentAnimatorStateInfo = anim_f.GetCurrentAnimatorStateInfo(0);
			if (ctrlFlag.isGaugeHit && !anim_f.IsInTransition(0) && currentAnimatorStateInfo.IsName("idle") && item != 1 && (item != 7 || (item2 != 1 && item2 != 2)))
			{
				anim_f.CrossFade("hit", 0.3f);
			}
			if (!ctrlFlag.isGaugeHit && !anim_f.IsInTransition(0) && currentAnimatorStateInfo.IsName("hit"))
			{
				anim_f.CrossFade("idle", 0.3f);
			}
		}
		if ((bool)anim_m && anim_m.isActiveAndEnabled)
		{
			AnimatorStateInfo currentAnimatorStateInfo2 = anim_m.GetCurrentAnimatorStateInfo(0);
			if (ctrlFlag.isGaugeHit_M && !anim_m.IsInTransition(0) && currentAnimatorStateInfo2.IsName("idle") && (item == 1 || (item == 7 && (item2 == 1 || item2 == 2))))
			{
				anim_m.CrossFade("hit", 0.3f);
			}
			if (!ctrlFlag.isGaugeHit_M && !anim_m.IsInTransition(0) && currentAnimatorStateInfo2.IsName("hit"))
			{
				anim_m.CrossFade("idle", 0.3f);
			}
		}
		UIFade();
		if (objHItem.GetComponent<HSceneSpriteHitem>().Effect(6))
		{
			ctrlFlag.click = HSceneFlagCtrl.ClickKind.RecoverFaintness;
		}
		if (SelectArea != null)
		{
			for (int j = 0; j < hSceneScrollNodes.Count; j++)
			{
				if (hSceneScrollNodes[j] == null)
				{
					continue;
				}
				motionListImages = hSceneScrollNodes[j].GetComponentsInChildren<Image>();
				if (j == MotionScroll.GetTarget().Item1)
				{
					motionListToggle = hSceneScrollNodes[j].GetComponent<Toggle>();
					if (!motionListToggle.isOn)
					{
						for (int k = 0; k < motionListImages.Length; k++)
						{
							if (motionListImages[k].name != "NowSelect")
							{
								motionListImages[k].enabled = false;
							}
							else
							{
								motionListImages[k].enabled = true;
							}
						}
						continue;
					}
					for (int l = 0; l < motionListImages.Length; l++)
					{
						if (motionListImages[l].name != "Checkmark")
						{
							motionListImages[l].enabled = false;
						}
						else
						{
							motionListImages[l].enabled = true;
						}
					}
					continue;
				}
				for (int m = 0; m < motionListImages.Length; m++)
				{
					if (motionListImages[m].name == "NowSelect")
					{
						motionListImages[m].enabled = false;
					}
					else if (motionListImages[m] == hSceneScrollNodes[j].BG)
					{
						motionListImages[m].enabled = true;
					}
				}
			}
		}
		if (!ChangeStart)
		{
			foreach (ScrollCylinderNode hSceneScrollNode in hSceneScrollNodes)
			{
				hSceneScrollNode.text.raycastTarget = true;
			}
		}
		categories.Changebuttonactive(!ctrlFlag.nowOrgasm);
		if (ctrlFlag.nowOrgasm)
		{
			if (hPointCtrl.IsMarker)
			{
				MarkerObjSet();
			}
			if (HItemCtrl.ConfirmPanel.activeSelf)
			{
				HItemCtrl.ConfirmPanel.SetActive(value: false);
			}
			if (objLightCategory.activeSelf)
			{
				objLightCategory.SetActive(value: false);
			}
		}
	}

	private void LateUpdate()
	{
	}

	private void OnValidate()
	{
	}

	public void SetLightInfo(HScene.LightInfo[] _info)
	{
		for (int i = 0; i < 2; i++)
		{
			int num = i;
			if (_info[num] == null)
			{
				infoMapLight[num] = null;
				continue;
			}
			infoMapLight[num].objCharaLight = _info[num].objCharaLight;
			infoMapLight[num].light = _info[num].light;
			infoMapLight[num].initRot = _info[num].initRot;
			infoMapLight[num].initIntensity = _info[num].initIntensity;
			infoMapLight[num].initColor = _info[num].initColor;
		}
		for (int j = 0; j < 2; j++)
		{
			int num2 = j;
			if (_info[num2] != null && _info[num2].light.isActiveAndEnabled)
			{
				categoryLightDir.SetValue(infoMapLight[num2].initRot.eulerAngles.x / 360f, 0);
				categoryLightDir.SetValue(infoMapLight[num2].initRot.eulerAngles.y / 360f, 1);
				categoryLightDir.SetValue(infoMapLight[num2].initIntensity, 2);
			}
		}
	}

	public bool IsSpriteOver()
	{
		EventSystem current = EventSystem.current;
		if (current == null)
		{
			return false;
		}
		return current.IsPointerOverGameObject();
	}

	public void setAnimationList(List<HScene.AnimationListInfo>[] _lstAnimInfo)
	{
		lstAnimInfo = _lstAnimInfo;
	}

	public void Setting(ChaControl[] _females)
	{
		chaFemales = _females;
		if (Singleton<HSceneManager>.Instance.EventKind == HSceneManager.HEvent.GyakuYobai)
		{
			categoryMainButton.interactable = false;
			hPointButton.interactable = false;
		}
		RefleshAutoButtom();
		categoryFinish.SetActive(_active: false);
		SetAnimationMenu();
	}

	public void RefleshAutoButtom()
	{
		int num = 0;
		for (int i = 0; i < lstAnimInfo.Length; i++)
		{
			for (int j = 0; j < lstAnimInfo[i].Count; j++)
			{
				if (CheckAutoMotionLimit(lstAnimInfo[i][j]))
				{
					num++;
				}
			}
		}
		if (PlayerSex == -1)
		{
			categoryMain.SetActive(_active: true);
		}
		isLeaveItToYou = num != 0;
		isLeaveItToYou &= hSceneManager.EventKind == HSceneManager.HEvent.FromFemale || hSceneManager.EventKind == HSceneManager.HEvent.GyakuYobai;
		SetVisibleLeaveItToYou(isLeaveItToYou);
	}

	public void SetFinishSelect(int _mode, int _ctrl, int infomode = -1, int infoctrl = -1)
	{
		switch (_mode)
		{
		case 0:
			categoryFinish.SetActive(_active: false);
			lstFinishVisible.Clear();
			break;
		case 1:
			categoryFinish.SetActive(_active: false);
			lstFinishVisible.Clear();
			if (ctrlFlag.initiative != 2)
			{
				categoryFinish.lstButton[0].GetComponentInChildren<Text>().text = "体にかける";
				lstFinishVisible.Add(0);
				if (_ctrl == 1 && !ctrlFlag.isFaintness)
				{
					lstFinishVisible.Add(4);
					lstFinishVisible.Add(5);
				}
			}
			break;
		case 2:
			categoryFinish.SetActive(_active: false);
			lstFinishVisible.Clear();
			if (ctrlFlag.initiative != 2)
			{
				categoryFinish.lstButton[0].GetComponentInChildren<Text>().text = "外に出す";
				lstFinishVisible.Add(0);
				lstFinishVisible.Add(1);
				if ((infomode == 2 && infoctrl == 0) || (infomode == 3 && infoctrl == 1))
				{
					lstFinishVisible.Add(2);
				}
				else if (infomode == 3 && _ctrl == 0 && ctrlFlag.isFaintness)
				{
					categoryFinish.SetActive(_active: false);
					lstFinishVisible.Clear();
				}
			}
			break;
		case 3:
			categoryFinish.SetActive(_active: false);
			lstFinishVisible.Clear();
			break;
		case 4:
			categoryFinish.SetActive(_active: false);
			lstFinishVisible.Clear();
			break;
		case 5:
			categoryFinish.SetActive(_active: false);
			lstFinishVisible.Clear();
			break;
		case 6:
			categoryFinish.SetActive(_active: false);
			lstFinishVisible.Clear();
			break;
		case 7:
			categoryFinish.SetActive(_active: false);
			lstFinishVisible.Clear();
			switch (_ctrl)
			{
			case 1:
			case 2:
				categoryFinish.lstButton[0].GetComponentInChildren<Text>().text = "体にかける";
				lstFinishVisible.Add(0);
				if (_ctrl == 2 && !ctrlFlag.isFaintness)
				{
					lstFinishVisible.Add(4);
					lstFinishVisible.Add(5);
				}
				break;
			case 3:
			case 4:
				categoryFinish.lstButton[0].GetComponentInChildren<Text>().text = "外に出す";
				lstFinishVisible.Add(0);
				lstFinishVisible.Add(1);
				switch (_ctrl)
				{
				case 3:
					lstFinishVisible.Add(2);
					break;
				case 4:
					if (ctrlFlag.isFaintness)
					{
						categoryFinish.SetActive(_active: false);
						lstFinishVisible.Clear();
					}
					break;
				}
				break;
			}
			break;
		}
	}

	public bool IsFinishVisible(int _num)
	{
		return lstFinishVisible.Contains(_num);
	}

	public void SetVisibleLeaveItToYou(bool _visible, bool _judgeLeaveItToYou = false)
	{
		if (_judgeLeaveItToYou)
		{
			_visible = isLeaveItToYou;
		}
		tglLeaveItToYou.gameObject.SetActive(_visible);
	}

	public void SetToggleLeaveItToYou(bool _on)
	{
		tglLeaveItToYou.isOn = _on;
	}

	public void SetEnableCategoryMain(bool _enable)
	{
		categoryMain.SetEnable(_enable);
		categoryMain.gameObject.SetActive(_enable);
		tglLeaveItToYou.interactable = _enable;
		if (!_enable)
		{
			categories.MainCategoryActive[3] = false;
		}
	}

	public void SetEnableHItem(bool _enable)
	{
		objHItem.SetActive(_enable);
		if (_enable)
		{
			return;
		}
		categories.SubCategoryActive[0] = false;
		HSceneSpriteHitem component = objHItem.GetComponent<HSceneSpriteHitem>();
		List<ScrollCylinderNode> list = component.hSceneScroll.GetList();
		if (!component.hSceneScroll.enabled)
		{
			component.hSceneScroll.enabled = true;
		}
		for (int i = 0; i < list.Count; i++)
		{
			if (!(list[i] == null))
			{
				Toggle component2 = list[i].GetComponent<Toggle>();
				if (!(component2 == null) && component2.isOn)
				{
					component2.isOn = false;
				}
			}
		}
	}

	public void MainCategoryOfLeaveItToYou(bool _isLeaveItToYou)
	{
		if (!_isLeaveItToYou)
		{
			SetAnimationMenu();
			return;
		}
		bool flag = true;
		for (int i = 0; i < lstAnimInfo.Length; i++)
		{
			int num = 0;
			for (int j = 0; j < lstAnimInfo[i].Count; j++)
			{
				if (chaFemales[1] != null)
				{
					if (i < 4)
					{
						continue;
					}
					if (PlayerSex == 0)
					{
						if (i != 5)
						{
							continue;
						}
					}
					else if (i != 4)
					{
						continue;
					}
				}
				else if (i > 3)
				{
					continue;
				}
				if (ctrlFlag.initiative == 1)
				{
					if (lstAnimInfo[i][j].nInitiativeFemale != 1 && (!flag || lstAnimInfo[i][j].nInitiativeFemale != 2))
					{
						continue;
					}
				}
				else if (ctrlFlag.initiative != 2 || lstAnimInfo[i][j].nInitiativeFemale != 2)
				{
					continue;
				}
				num++;
			}
			if (PlayerSex != -1)
			{
				categoryMain.SetActive(num != 0, i);
			}
		}
		if (PlayerSex == -1)
		{
			categoryMain.SetActive(_active: true);
		}
		CategoryScroll.ListNodeSet();
		Observable.NextFrame().Subscribe(delegate
		{
			CategoryScroll.SetTarget(ctrlFlag.nowAnimationInfo.ActionCtrl.Item1);
		});
	}

	public bool IsEnableLeaveItToYou()
	{
		if (!tglLeaveItToYou.interactable || !tglLeaveItToYou.gameObject.activeSelf)
		{
			return false;
		}
		return true;
	}

	public void OnChangePlaySelect(GameObject objClick)
	{
		Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_L);
		if (null != objClick)
		{
			HAnimationInfoComponent component = objClick.GetComponent<HAnimationInfoComponent>();
			if (null != component)
			{
				ctrlFlag.selectAnimationListInfo = component.info;
			}
			objClick.GetComponent<Toggle>().isOn = true;
			ChangeStart = true;
		}
	}

	private void MotionListView(int taii)
	{
		if (!Singleton<Scene>.Instance.IsNowLoading && !Singleton<Scene>.Instance.IsNowLoadingFade && !isFade && hSceneManager.HSceneUISet.activeSelf && endFade != 0)
		{
			SetMotionListDraw(_active: true, taii);
		}
	}

	public void OnClickMotion(int _motion)
	{
		if (!Singleton<Scene>.Instance.IsNowLoading && !Singleton<Scene>.Instance.IsNowLoadingFade && !isFade && hSceneManager.HSceneUISet.activeSelf)
		{
			HRotationScrollNode component = categoryMain.GetHScroll().GetTarget().Item2.GetComponent<HRotationScrollNode>();
			if (!(component == null) && component.id == _motion && endFade != 0)
			{
				SetMotionListDraw(_active: true, _motion);
			}
		}
	}

	public void SetMotionListDraw(bool _active, int _motion = -1)
	{
		objMotionListPanel.SetActive(_active);
		ctrlFlag.categoryMotionList = _motion;
		if (_active)
		{
			LoadMotionList(ctrlFlag.categoryMotionList);
			switch (_motion)
			{
			case 0:
				MotionListLabel.text = "愛撫";
				break;
			case 1:
				MotionListLabel.text = "奉仕";
				break;
			case 2:
				MotionListLabel.text = "挿入";
				break;
			case 3:
				MotionListLabel.text = "特殊";
				break;
			case 4:
				MotionListLabel.text = "レズ";
				break;
			case 5:
				MotionListLabel.text = "複数";
				break;
			}
		}
		else if (categoryMain.gameObject.activeSelf)
		{
			categoryMain.gameObject.SetActive(value: false);
		}
	}

	public void OnClickMainCategories(int _menu)
	{
		if (Singleton<Scene>.Instance.IsNowLoading || Singleton<Scene>.Instance.IsNowLoadingFade || isFade || !hSceneManager.HSceneUISet.activeSelf || endFade == 0 || HItemCtrl.ConfirmPanel.activeSelf)
		{
			return;
		}
		GameObject[] array = new GameObject[4] { objCloth.gameObject, objAccessory.gameObject, objClothCard.gameObject, categoryMain.gameObject };
		if (array[_menu].activeSelf)
		{
			array[_menu].SetActive(value: false);
			if (_menu == 3)
			{
				SetMotionListDraw(_active: false);
			}
			charaChoice.CloseChoice();
			charaChoice.gameObject.SetActive(value: false);
			charaChoice.SetMaleSelectBtn(setVal: false);
			categories.MainCategoryActive[_menu] = false;
			if (_menu == 2)
			{
				objClothCard.CloseSort();
			}
		}
		else
		{
			if (_menu != 3)
			{
				array[_menu].SetActive(value: true);
				categories.MainCategoryActive[_menu] = true;
			}
			else
			{
				ScrollCylinderNode[] componentsInChildren = CategoryScroll.Contents.GetComponentsInChildren<ScrollCylinderNode>();
				if (componentsInChildren.Length > 0)
				{
					SetEnableCategoryMain(_enable: true);
					categories.MainCategoryActive[_menu] = true;
					Observable.NextFrame().Subscribe(delegate
					{
						CategoryScroll.ListNodeSet(null, targetInit: false);
						Observable.NextFrame().Subscribe(delegate
						{
							CategoryScroll.SetTarget(ctrlFlag.nowAnimationInfo.ActionCtrl.Item1);
							MotionListView(ctrlFlag.nowAnimationInfo.ActionCtrl.Item1);
						});
					});
				}
			}
			for (int i = 0; i < array.Length; i++)
			{
				if (i != _menu)
				{
					array[i].SetActive(value: false);
					categories.MainCategoryActive[i] = false;
				}
			}
			if (hPointCtrl.IsMarker)
			{
				MarkerObjSet();
			}
			categories.MainCategoryActive[categories.MainCategoryActive.Length - 1] = false;
			if (_menu != 3)
			{
				SetMotionListDraw(_active: false);
			}
			else
			{
				SetAnimationMenu();
			}
			charaChoice.SetMaleSelectBtn(_menu == 1);
			switch (_menu)
			{
			case 0:
				objCloth.SetClothCharacter();
				break;
			case 1:
				objAccessory.SetAccessoryCharacter();
				break;
			}
			charaChoice.gameObject.SetActive(_menu < 3);
		}
		GameObject[] array2 = new GameObject[2] { objHItem, objLightCategory };
		for (int j = 0; j < array2.Length; j++)
		{
			array2[j].SetActive(value: false);
		}
		HItemCtrl.ConfirmPanel.SetActive(value: false);
		for (int k = 0; k < categories.SubCategoryActive.Length; k++)
		{
			categories.SubCategoryActive[k] = false;
		}
	}

	public void OnClickSubCategories(int _menu)
	{
		if (Singleton<Scene>.Instance.IsNowLoading || Singleton<Scene>.Instance.IsNowLoadingFade || isFade || !hSceneManager.HSceneUISet.activeSelf || endFade == 0 || HItemCtrl.ConfirmPanel.activeSelf)
		{
			return;
		}
		GameObject[] array = new GameObject[4] { objCloth.gameObject, objAccessory.gameObject, objClothCard.gameObject, categoryMain.gameObject };
		for (int i = 0; i < array.Length; i++)
		{
			array[i].SetActive(value: false);
		}
		objClothCard.CloseSort();
		SetMotionListDraw(_active: false);
		for (int j = 0; j < categories.MainCategoryActive.Length; j++)
		{
			categories.MainCategoryActive[j] = false;
		}
		if (hPointCtrl.IsMarker)
		{
			MarkerObjSet();
		}
		GameObject[] array2 = new GameObject[2] { objHItem, objLightCategory };
		if (array2[_menu].activeSelf)
		{
			array2[_menu].SetActive(value: false);
			categories.SubCategoryActive[(_menu != 0) ? 2 : _menu] = false;
		}
		else
		{
			array2[_menu].SetActive(value: true);
			categories.SubCategoryActive[(_menu != 0) ? 2 : _menu] = true;
			if (_menu == 0)
			{
				objHItem.GetComponent<HSceneSpriteHitem>().SetVisible(visible: true);
			}
			else
			{
				for (int k = 0; k < infoMapLight.Length; k++)
				{
					if (infoMapLight[k] != null && infoMapLight[k].light.isActiveAndEnabled)
					{
						colorPicker.SetColor(infoMapLight[k].light.color);
						break;
					}
				}
			}
			for (int l = 0; l < array2.Length; l++)
			{
				if (l != _menu)
				{
					array2[l].SetActive(value: false);
				}
			}
			for (int m = 0; m < categories.SubCategoryActive.Length; m++)
			{
				if (m != ((_menu != 0) ? 2 : _menu))
				{
					categories.SubCategoryActive[m] = false;
				}
			}
		}
		charaChoice.CloseChoice();
		charaChoice.gameObject.SetActive(value: false);
	}

	public void OnClickFinishBefore()
	{
		if (!Singleton<Scene>.Instance.IsNowLoading && !Singleton<Scene>.Instance.IsNowLoadingFade && !isFade && hSceneManager.HSceneUISet.activeSelf)
		{
			ctrlFlag.click = HSceneFlagCtrl.ClickKind.FinishBefore;
		}
	}

	public void OnClickFinishInSide()
	{
		if (!Singleton<Scene>.Instance.IsNowLoading && !Singleton<Scene>.Instance.IsNowLoadingFade && !isFade && hSceneManager.HSceneUISet.activeSelf)
		{
			ctrlFlag.click = HSceneFlagCtrl.ClickKind.FinishInSide;
		}
	}

	public void OnClickFinishOutSide()
	{
		if (!Singleton<Scene>.Instance.IsNowLoading && !Singleton<Scene>.Instance.IsNowLoadingFade && !isFade && hSceneManager.HSceneUISet.activeSelf)
		{
			ctrlFlag.click = HSceneFlagCtrl.ClickKind.FinishOutSide;
		}
	}

	public void OnClickFinishSame()
	{
		if (!Singleton<Scene>.Instance.IsNowLoading && !Singleton<Scene>.Instance.IsNowLoadingFade && !isFade && hSceneManager.HSceneUISet.activeSelf)
		{
			ctrlFlag.click = HSceneFlagCtrl.ClickKind.FinishSame;
		}
	}

	public void OnClickFinishDrink()
	{
		if (!Singleton<Scene>.Instance.IsNowLoading && !Singleton<Scene>.Instance.IsNowLoadingFade && !isFade && hSceneManager.HSceneUISet.activeSelf)
		{
			ctrlFlag.click = HSceneFlagCtrl.ClickKind.FinishDrink;
		}
	}

	public void OnClickFinishVomit()
	{
		if (!Singleton<Scene>.Instance.IsNowLoading && !Singleton<Scene>.Instance.IsNowLoadingFade && !isFade && hSceneManager.HSceneUISet.activeSelf)
		{
			ctrlFlag.click = HSceneFlagCtrl.ClickKind.FinishVomit;
		}
	}

	public void OnClickSpanking()
	{
		if (!Singleton<Scene>.Instance.IsNowLoading && !Singleton<Scene>.Instance.IsNowLoadingFade && !isFade && hSceneManager.HSceneUISet.activeSelf)
		{
			ctrlFlag.click = HSceneFlagCtrl.ClickKind.Spnking;
		}
	}

	public void OnClickSceneEnd()
	{
		if (!Singleton<Scene>.Instance.IsNowLoading && !Singleton<Scene>.Instance.IsNowLoadingFade && !isFade && hSceneManager.HSceneUISet.activeSelf && endFade != 0)
		{
			Singleton<GameCursor>.Instance.SetCursorLock(setLockFlag: false);
			ConfirmScene.Sentence = "Hシーンを終了しますか";
			ConfirmScene.OnClickedYes = delegate
			{
				ctrlFlag.click = HSceneFlagCtrl.ClickKind.SceneEnd;
			};
			ConfirmScene.OnClickedNo = delegate
			{
			};
			Singleton<Game>.Instance.LoadDialog();
		}
	}

	public void OnClickStopFeel(int _sex)
	{
		if ((!Singleton<Scene>.IsInstance() || (!Singleton<Scene>.Instance.IsNowLoading && !Singleton<Scene>.Instance.IsNowLoadingFade)) && !isFade && hSceneManager.HSceneUISet.activeSelf)
		{
			if (_sex == 0)
			{
				ctrlFlag.stopFeelMale = !ctrlFlag.stopFeelMale;
			}
			else
			{
				ctrlFlag.stopFeelFemal = !ctrlFlag.stopFeelFemal;
			}
		}
	}

	public void OnClickMovePoint()
	{
		if (endFade != 0 && !HItemCtrl.ConfirmPanel.activeSelf)
		{
			MarkerObjSet();
		}
	}

	public void OnClickLeave()
	{
		if (!Singleton<Scene>.Instance.IsNowLoading && !Singleton<Scene>.Instance.IsNowLoadingFade && !isFade && hSceneManager.HSceneUISet.activeSelf && IsEnableLeaveItToYou() && UnityEngine.Input.GetMouseButtonUp(0))
		{
			ctrlFlag.click = HSceneFlagCtrl.ClickKind.LeaveItToYou;
			SetMotionListDraw(_active: false);
			categoryMain.gameObject.SetActive(value: false);
			categories.MainCategoryActive[3] = false;
		}
	}

	public void OnValueLightColorChanged(Color color)
	{
		if (Singleton<Scene>.Instance.IsNowLoading || Singleton<Scene>.Instance.IsNowLoadingFade || isFade || !hSceneManager.HSceneUISet.activeSelf)
		{
			return;
		}
		for (int i = 0; i < 2; i++)
		{
			if (infoMapLight[i] != null)
			{
				infoMapLight[i].light.color = color;
			}
		}
		GlobalMethod.setCameraMoveFlag(ctrlFlag.cameraCtrl, _bPlay: false);
	}

	public void OnValueLightDireChanged()
	{
		if (Singleton<Scene>.Instance.IsNowLoading || Singleton<Scene>.Instance.IsNowLoadingFade || isFade || !hSceneManager.HSceneUISet.activeSelf)
		{
			return;
		}
		float x = categoryLightDir.GetValue(0) * 360f;
		float y = categoryLightDir.GetValue(1) * 360f;
		for (int i = 0; i < 2; i++)
		{
			if (infoMapLight[i] != null)
			{
				infoMapLight[i].objCharaLight.transform.localRotation = Quaternion.Euler(x, y, 0f);
			}
		}
		GlobalMethod.setCameraMoveFlag(ctrlFlag.cameraCtrl, _bPlay: false);
	}

	public void OnValuePowerChanged()
	{
		if (Singleton<Scene>.Instance.IsNowLoading || Singleton<Scene>.Instance.IsNowLoadingFade || isFade || !hSceneManager.HSceneUISet.activeSelf)
		{
			return;
		}
		for (int i = 0; i < 2; i++)
		{
			if (infoMapLight[i] != null)
			{
				infoMapLight[i].light.intensity = Mathf.Lerp(infoMapLight[i].minIntensity, infoMapLight[i].maxIntensity, categoryLightDir.GetValue(2));
			}
		}
		GlobalMethod.setCameraMoveFlag(ctrlFlag.cameraCtrl, _bPlay: false);
	}

	public void ReSetLight()
	{
		ReSetLightDir();
		ReSetLightColor();
		ReSetLightPower();
	}

	public void ReSetLightDir()
	{
		for (int i = 0; i < 2; i++)
		{
			if (infoMapLight[i] != null)
			{
				infoMapLight[i].objCharaLight.transform.localRotation = infoMapLight[i].initRot;
			}
		}
	}

	public void ReSetLightPower()
	{
		for (int i = 0; i < 2; i++)
		{
			if (infoMapLight[i] != null)
			{
				infoMapLight[i].light.intensity = Mathf.Lerp(infoMapLight[i].minIntensity, infoMapLight[i].maxIntensity, infoMapLight[i].initIntensity);
			}
		}
	}

	public void ReSetLightColor()
	{
		for (int i = 0; i < 2; i++)
		{
			if (infoMapLight[i] != null)
			{
				infoMapLight[i].light.color = infoMapLight[i].initColor;
			}
		}
	}

	public void OnClickConfig()
	{
		bool flag = Singleton<Game>.Instance.Config != null;
		flag |= Singleton<Game>.Instance.Dialog != null;
		flag |= Singleton<Game>.Instance.ExitScene != null;
		if (flag | (Singleton<Game>.Instance.MapShortcutUI != null))
		{
			return;
		}
		GameObject[] array = new GameObject[4] { objCloth.gameObject, objAccessory.gameObject, objClothCard.gameObject, categoryMain.gameObject };
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].activeSelf)
			{
				array[i].SetActive(value: false);
				categories.MainCategoryActive[i] = false;
			}
		}
		SetMotionListDraw(_active: false);
		charaChoice.CloseChoice();
		charaChoice.gameObject.SetActive(value: false);
		objClothCard.CloseSort();
		if (hPointCtrl.IsMarker)
		{
			hPointCtrl.MarkerObjDel();
			categories.MainCategoryActive[categories.MainCategoryActive.Length - 1] = false;
		}
		GameObject[] array2 = new GameObject[2] { objHItem, objLightCategory };
		for (int j = 0; j < array2.Length; j++)
		{
			array2[j].SetActive(value: false);
		}
		categories.SubCategoryActive[0] = false;
		categories.SubCategoryActive[2] = false;
		SetEnableHItem(_enable: false);
		HItemCtrl.ConfirmPanel.SetActive(value: false);
		categories.AllForceClose(1);
		ConfigWindow.UnLoadAction = delegate
		{
		};
		ConfigWindow.TitleChangeAction = delegate
		{
			ctrlFlag.click = HSceneFlagCtrl.ClickKind.SceneEnd;
			hScene.NowStateIsEnd = true;
			hScene.ConfigEnd();
			ConfigWindow.UnLoadAction = null;
			Singleton<Game>.Instance.Dialog.TimeScale = 1f;
		};
		Singleton<Game>.Instance.LoadConfig();
	}

	public void FadeState(FadeKind _kind, float _timeFade = -1f)
	{
		isFade = true;
		timeFadeTime = 0f;
		if (_timeFade < 0f)
		{
			timeFade = ((_kind == FadeKind.OutIn) ? (timeFadeBase * 2f) : timeFadeBase);
		}
		else
		{
			timeFade = ((_kind == FadeKind.OutIn) ? (_timeFade * 2f) : _timeFade);
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
				break;
			case FadeKind.OutIn:
				kindFadeProc = FadeKindProc.OutInEnd;
				break;
			}
		}
		return true;
	}

	public bool LoadMotionList(int _motion)
	{
		List<GameObject> list = hSceneMotionPool.GetList();
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i].gameObject.activeSelf)
			{
				list[i].gameObject.SetActive(value: false);
			}
		}
		hSceneScrollNodes.Clear();
		if (_motion < 0 || lstAnimInfo.Length <= _motion)
		{
			return true;
		}
		PointerClickTrigger pointerClickTrigger = null;
		UITrigger.TriggerEvent triggerEvent = null;
		for (int j = 0; j < lstAnimInfo[_motion].Count; j++)
		{
			if ((hSceneManager.bMerchant && hSceneManager.MerchantLimit == 1 && ((PlayerSex == 0 && _motion != 1) || (PlayerSex == 1 && _motion != 4))) || !CheckMotionLimit(lstAnimInfo[_motion][j]))
			{
				continue;
			}
			GameObject objClone = hSceneMotionPool.Get(hSceneScrollNodes.Count);
			HAnimationInfoComponent component = objClone.GetComponent<HAnimationInfoComponent>();
			component.info = lstAnimInfo[_motion][j];
			objClone.transform.SetParent(objMotionList.transform, worldPositionStays: false);
			GameObject gameObject = objClone.transform.FindLoop("Label");
			if ((bool)gameObject)
			{
				Text component2 = gameObject.GetComponent<Text>();
				component2.text = component.info.nameAnimation;
				if (component2.text != ctrlFlag.nowAnimationInfo.nameAnimation)
				{
					objClone.GetComponent<Toggle>().isOn = false;
				}
				else
				{
					objClone.GetComponent<Toggle>().isOn = true;
				}
			}
			pointerClickTrigger = null;
			triggerEvent = new UITrigger.TriggerEvent();
			int no = hSceneScrollNodes.Count;
			pointerClickTrigger = objClone.GetComponent<PointerClickTrigger>();
			if (pointerClickTrigger == null)
			{
				pointerClickTrigger = objClone.AddComponent<PointerClickTrigger>();
			}
			pointerClickTrigger.Triggers.Clear();
			pointerClickTrigger.Triggers.Add(triggerEvent);
			triggerEvent.AddListener(delegate
			{
				if (no != objMotionList.transform.parent.GetComponent<ScrollCylinder>().GetTarget().Item1)
				{
					objMotionList.transform.parent.GetComponent<ScrollCylinder>().SetTarget(objClone.GetComponent<ScrollCylinderNode>());
				}
				else
				{
					OnChangePlaySelect(objClone);
					if (ChangeStart)
					{
						foreach (ScrollCylinderNode hSceneScrollNode in hSceneScrollNodes)
						{
							hSceneScrollNode.text.raycastTarget = false;
						}
					}
				}
			});
			if (lstAnimInfo[_motion][j] == ctrlFlag.nowAnimationInfo)
			{
				objClone.GetComponent<Toggle>().isOn = true;
			}
			hSceneScrollNodes.Add(objClone.GetComponent<ScrollCylinderNode>());
		}
		Observable.NextFrame().Subscribe(delegate
		{
			MotionScroll.ListNodeSet(hSceneScrollNodes);
		});
		return true;
	}

	public bool SetAnimationMenu()
	{
		int[] array = new int[lstAnimInfo.Length];
		for (int i = 0; i < lstAnimInfo.Length; i++)
		{
			if (hSceneManager.bMerchant && hSceneManager.MerchantLimit != 2)
			{
				if (hSceneManager.MerchantLimit != 1)
				{
					break;
				}
				if (((hSceneManager.Player.ChaControl.sex == 0 || hSceneManager.bFutanari) && i != 1) || (hSceneManager.Player.ChaControl.sex == 1 && !hSceneManager.bFutanari && i != 4))
				{
					continue;
				}
			}
			for (int j = 0; j < lstAnimInfo[i].Count; j++)
			{
				if (CheckMotionLimit(lstAnimInfo[i][j]))
				{
					array[i]++;
				}
			}
		}
		for (int k = 0; k < array.Length; k++)
		{
			canMainCategory[k] = array[k] != 0;
			categoryMain.SetActive(canMainCategory[k], k);
		}
		if (PlayerSex == -1)
		{
			categoryMain.SetActive(_active: true);
		}
		return true;
	}

	private void UIFade()
	{
		if (endFade == 0)
		{
			nowFadeTime += Time.unscaledDeltaTime;
			UIGroup.alpha = Mathf.Lerp(0f, 1f, nowFadeTime / fadeTime);
			if (UIGroup.alpha >= 1f)
			{
				endFade = 1;
				UIGroup.blocksRaycasts = true;
			}
		}
	}

	public void PopupCommands(bool isForce)
	{
		hScene.ctrlVoice.HBeforeHouchiTime = 0f;
		Singleton<Manager.Input>.Instance.ReserveState(Manager.Input.ValidType.UI);
		Singleton<Manager.Input>.Instance.SetupState();
		startList = Singleton<Manager.Resources>.Instance.HSceneTable.lstStartAnimInfo;
		MapUIContainer.CommandList.CancelEvent = null;
		if (hSceneManager.Player.ChaControl.sex == 0 || (hSceneManager.Player.ChaControl.sex == 1 && ctrlFlag.bFutanari))
		{
			ChaControl[] females = hScene.GetFemales();
			if (females[1] == null)
			{
				CommandRefresh(isForce);
			}
			else if (StartListContain(startList, hSceneManager.height, HSceneManager.HEvent.Normal, 5))
			{
				CommCommandList.CommandInfo[] options = new CommCommandList.CommandInfo[1]
				{
					new CommCommandList.CommandInfo("リードする", null, delegate
					{
						ctrlFlag.AddParam(8, 1);
						hScene.SetStartAnimationInfo(HSceneManager.HEvent.Normal, 5);
						CommandProc();
						Singleton<HSceneFlagCtrl>.Instance.BeforeHWait = false;
					})
				};
				MapUIContainer.RefreshCommands(0, options);
			}
			MapUIContainer.SetActiveCommandList(active: true, "どう始める？");
		}
		else if (StartListContain(startList, hSceneManager.height, HSceneManager.HEvent.Normal, 4))
		{
			CommCommandList.CommandInfo[] options2 = new CommCommandList.CommandInfo[1]
			{
				new CommCommandList.CommandInfo("リードする", null, delegate
				{
					ctrlFlag.AddParam(7, 1);
					hScene.SetStartAnimationInfo(HSceneManager.HEvent.Normal, 4);
					CommandProc();
					Singleton<HSceneFlagCtrl>.Instance.BeforeHWait = false;
				})
			};
			MapUIContainer.RefreshCommands(0, options2);
			MapUIContainer.SetActiveCommandList(active: true, "どう始める？");
		}
	}

	private void CommandRefresh(bool isForce)
	{
		List<CommCommandList.CommandInfo> list = new List<CommCommandList.CommandInfo>();
		bool[] array;
		CommCommandList.CommandInfo[] array2;
		if (!isForce)
		{
			array = new bool[3]
			{
				StartListContain(startList, hSceneManager.height, HSceneManager.HEvent.Normal, 0),
				StartListContain(startList, hSceneManager.height, HSceneManager.HEvent.Normal, 1),
				StartListContain(startList, hSceneManager.height, HSceneManager.HEvent.Normal, 2)
			};
			array2 = new CommCommandList.CommandInfo[3]
			{
				new CommCommandList.CommandInfo("リードする", null, delegate
				{
					ctrlFlag.AddParam(0, 0);
					hScene.SetStartAnimationInfo(HSceneManager.HEvent.Normal, 0);
					CommandProc();
					Singleton<HSceneFlagCtrl>.Instance.BeforeHWait = false;
				}),
				new CommCommandList.CommandInfo("してほしい", null, delegate
				{
					ctrlFlag.AddParam(1, 0);
					hScene.SetStartAnimationInfo(HSceneManager.HEvent.Normal, 1);
					CommandProc();
					Singleton<HSceneFlagCtrl>.Instance.BeforeHWait = false;
				}),
				new CommCommandList.CommandInfo("いきなり挿入する", null, delegate
				{
					ctrlFlag.AddParam(2, 0);
					hScene.SetStartAnimationInfo(HSceneManager.HEvent.Normal, 2);
					CommandProc();
					Singleton<HSceneFlagCtrl>.Instance.BeforeHWait = false;
				})
			};
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i])
				{
					list.Add(array2[i]);
				}
			}
			if (list.Count != 0)
			{
				array2 = new CommCommandList.CommandInfo[list.Count];
				for (int j = 0; j < array2.Length; j++)
				{
					array2[j] = list[j];
				}
				MapUIContainer.RefreshCommands(0, array2);
			}
			return;
		}
		array = new bool[2]
		{
			StartListContain(startList, hSceneManager.height, HSceneManager.HEvent.Normal, 1),
			StartListContain(startList, hSceneManager.height, HSceneManager.HEvent.Normal, 2)
		};
		array2 = new CommCommandList.CommandInfo[2]
		{
			new CommCommandList.CommandInfo("奉仕させる", null, delegate
			{
				hScene.SetStartAnimationInfo(HSceneManager.HEvent.Normal, 1);
				CommandProc();
				Singleton<HSceneFlagCtrl>.Instance.BeforeHWait = false;
			}),
			new CommCommandList.CommandInfo("挿入する", null, delegate
			{
				hScene.SetStartAnimationInfo(HSceneManager.HEvent.Normal, 2);
				CommandProc();
				Singleton<HSceneFlagCtrl>.Instance.BeforeHWait = false;
			})
		};
		for (int k = 0; k < array.Length; k++)
		{
			if (array[k])
			{
				list.Add(array2[k]);
			}
		}
		if (list.Count != 0)
		{
			array2 = new CommCommandList.CommandInfo[list.Count];
			for (int l = 0; l < array2.Length; l++)
			{
				array2[l] = list[l];
			}
			MapUIContainer.RefreshCommands(0, array2);
		}
	}

	private void CommandProc()
	{
		MapUIContainer.SetActiveCommandList(active: false);
		beforeChoice = MapUIContainer.CommandList.OnCompletedStopAsObservable().Take(1).Subscribe(delegate
		{
			MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.None);
			hSceneManager.Player.CameraControl.enabled = false;
			hSceneManager.Player.Controller.enabled = false;
			hSceneManager.Player.Animation.enabled = false;
			hSceneManager.Player.CameraControl.EnabledInput = false;
		});
		StartAnimInfo = hScene.StartAnimInfo;
		bool isForceResetCamera = true;
		hScene.setCameraLoad(StartAnimInfo, isForceResetCamera);
	}

	public void MarkerObjSet()
	{
		if (Singleton<Scene>.Instance.IsNowLoading || Singleton<Scene>.Instance.IsNowLoadingFade || isFade || !hSceneManager.HSceneUISet.activeSelf)
		{
			return;
		}
		if (!hPointCtrl.IsMarker)
		{
			Actor actor = hSceneManager.females[0];
			int areaID = actor.AreaID;
			hPointCtrl.MarkerObjSet(ctrlFlag.nowHPoint.transform.position, Singleton<Map>.Instance.MapID, areaID);
			categories.MainCategoryActive[categories.MainCategoryActive.Length - 1] = true;
			GameObject[] array = new GameObject[4] { objCloth.gameObject, objAccessory.gameObject, objClothCard.gameObject, categoryMain.gameObject };
			for (int i = 0; i < array.Length; i++)
			{
				array[i].SetActive(value: false);
				if (i == 3)
				{
					SetMotionListDraw(_active: false);
				}
				charaChoice.CloseChoice();
				charaChoice.gameObject.SetActive(value: false);
				categories.MainCategoryActive[i] = false;
			}
			objClothCard.CloseSort();
			GameObject[] array2 = new GameObject[2] { objHItem, objLightCategory };
			for (int j = 0; j < array2.Length; j++)
			{
				array2[j].SetActive(value: false);
			}
			for (int k = 0; k < categories.SubCategoryActive.Length; k++)
			{
				categories.SubCategoryActive[k] = false;
			}
		}
		else
		{
			hPointCtrl.MarkerObjDel();
			categories.MainCategoryActive[categories.MainCategoryActive.Length - 1] = false;
		}
	}

	private bool CheckMotionLimit(HScene.AnimationListInfo lstAnimInfo)
	{
		if (PlayerSex == 0 || (PlayerSex == 1 && ctrlFlag.bFutanari))
		{
			if (hSceneManager.females[1] == null)
			{
				if (lstAnimInfo.nPromiscuity == 1)
				{
					return false;
				}
			}
			else if (lstAnimInfo.nPromiscuity != 1)
			{
				return false;
			}
			if (lstAnimInfo.nPromiscuity == 2)
			{
				return false;
			}
			if (lstAnimInfo.ActionCtrl.Item1 == 3 && !NonTokushuCheckIDs.Contains(lstAnimInfo.id) && lstAnimInfo.fileMale == string.Empty)
			{
				return false;
			}
		}
		else if (PlayerSex == 1 && lstAnimInfo.nPromiscuity < 2)
		{
			return false;
		}
		if (!hSceneManager.bMerchant)
		{
			if (lstAnimInfo.nHentai == 1 && !hSceneManager.isHAddTaii[0])
			{
				return false;
			}
			if (lstAnimInfo.nHentai == 2 && !hSceneManager.isHAddTaii[1])
			{
				return false;
			}
		}
		if (hSceneManager.bMerchant)
		{
			if (!lstAnimInfo.bMerchantMotion)
			{
				return false;
			}
			if (lstAnimInfo.nIyaAction == 2)
			{
				return false;
			}
		}
		else if (hSceneManager.EventKind != HSceneManager.HEvent.Yobai)
		{
			if (hSceneManager.isForce)
			{
				if (lstAnimInfo.nIyaAction == 0)
				{
					return false;
				}
			}
			else if (lstAnimInfo.nIyaAction == 2)
			{
				return false;
			}
		}
		else if (!lstAnimInfo.bSleep)
		{
			return false;
		}
		if (!usePoint)
		{
			int item = hSceneManager.hitHmesh.Item3;
			if (item != -1 && !lstAnimInfo.nPositons.Contains(item))
			{
				return false;
			}
			if (item == -1 && !lstAnimInfo.nPositons.Contains(0) && !lstAnimInfo.nPositons.Contains(1))
			{
				return false;
			}
		}
		else if (ctrlFlag.HPointID == 0)
		{
			if (!lstAnimInfo.nPositons.Contains(hPointCtrl.InitNull.Item3))
			{
				return false;
			}
		}
		else
		{
			HPoint.NotMotionInfo[] notMotion = hPointCtrl.lstMarker[ctrlFlag.HPointID].Item2.notMotion;
			for (int i = 0; i < notMotion.Length; i++)
			{
				HPoint.NotMotionInfo notMotionInfo = notMotion[i];
				if (notMotionInfo.motionID.Contains(lstAnimInfo.id))
				{
					return false;
				}
			}
			if (!HPointCtrl.DicTupleContainsValue(hPointCtrl.lstMarker[ctrlFlag.HPointID].Item2._nPlace, lstAnimInfo.nPositons, 0))
			{
				return false;
			}
		}
		if (lstAnimInfo.isNeedItem && !hSceneManager.CheckHadItem(lstAnimInfo.ActionCtrl.Item1, lstAnimInfo.id))
		{
			return false;
		}
		if (lstAnimInfo.nDownPtn == 0 && ctrlFlag.isFaintness)
		{
			return false;
		}
		if (lstAnimInfo.nFaintnessLimit == 1 && !ctrlFlag.isFaintness)
		{
			return false;
		}
		switch (ctrlFlag.initiative)
		{
		case 0:
			if (lstAnimInfo.nInitiativeFemale != 0)
			{
				return false;
			}
			break;
		case 1:
			if (lstAnimInfo.nInitiativeFemale == 0)
			{
				return false;
			}
			break;
		case 2:
			if (lstAnimInfo.nInitiativeFemale != 2)
			{
				return false;
			}
			break;
		}
		return true;
	}

	private bool CheckAutoMotionLimit(HScene.AnimationListInfo lstAnimInfo)
	{
		if (!hSceneManager.bMerchant)
		{
			if (hSceneManager.EventKind == HSceneManager.HEvent.Yobai)
			{
				return false;
			}
			if (hSceneManager.isForce)
			{
				return false;
			}
		}
		if (ctrlFlag.isFaintness)
		{
			return false;
		}
		if (!hSceneManager.HSkil.ContainsValue(13))
		{
			return false;
		}
		if (PlayerSex == 0 || (PlayerSex == 1 && ctrlFlag.bFutanari))
		{
			if (hSceneManager.females[1] == null)
			{
				if (lstAnimInfo.nPromiscuity == 1)
				{
					return false;
				}
			}
			else if (lstAnimInfo.nPromiscuity != 1)
			{
				return false;
			}
			if (lstAnimInfo.nPromiscuity == 2)
			{
				return false;
			}
			if (lstAnimInfo.ActionCtrl.Item1 == 3 && !NonTokushuCheckIDs.Contains(lstAnimInfo.id) && lstAnimInfo.fileMale == string.Empty)
			{
				return false;
			}
		}
		else if (PlayerSex == 1 && lstAnimInfo.nPromiscuity < 2)
		{
			return false;
		}
		if (!hSceneManager.bMerchant)
		{
			if (lstAnimInfo.nHentai == 1 && !hSceneManager.isHAddTaii[0])
			{
				return false;
			}
			if (lstAnimInfo.nHentai == 2 && !hSceneManager.isHAddTaii[1])
			{
				return false;
			}
		}
		if (hSceneManager.bMerchant && !lstAnimInfo.bMerchantMotion)
		{
			return false;
		}
		if (!usePoint)
		{
			int item = hSceneManager.hitHmesh.Item3;
			if (item != -1 && !lstAnimInfo.nPositons.Contains(item))
			{
				return false;
			}
			if (item == -1 && !lstAnimInfo.nPositons.Contains(0) && !lstAnimInfo.nPositons.Contains(1))
			{
				return false;
			}
		}
		else if (ctrlFlag.HPointID == 0)
		{
			if (!lstAnimInfo.nPositons.Contains(hPointCtrl.InitNull.Item3))
			{
				return false;
			}
		}
		else
		{
			HPoint.NotMotionInfo[] notMotion = hPointCtrl.lstMarker[ctrlFlag.HPointID].Item2.notMotion;
			for (int i = 0; i < notMotion.Length; i++)
			{
				HPoint.NotMotionInfo notMotionInfo = notMotion[i];
				if (notMotionInfo.motionID.Contains(lstAnimInfo.id))
				{
					return false;
				}
			}
			if (!HPointCtrl.DicTupleContainsValue(hPointCtrl.lstMarker[ctrlFlag.HPointID].Item2._nPlace, lstAnimInfo.nPositons, 0))
			{
				return false;
			}
		}
		if (lstAnimInfo.isNeedItem && !hSceneManager.CheckHadItem(lstAnimInfo.ActionCtrl.Item1, lstAnimInfo.id))
		{
			return false;
		}
		if (lstAnimInfo.nFaintnessLimit == 1)
		{
			return false;
		}
		switch (ctrlFlag.initiative)
		{
		case 0:
			if (lstAnimInfo.nInitiativeFemale == 0)
			{
				return false;
			}
			break;
		case 1:
			if (lstAnimInfo.nInitiativeFemale == 0)
			{
				return false;
			}
			break;
		case 2:
			if (lstAnimInfo.nInitiativeFemale != 2)
			{
				return false;
			}
			break;
		}
		return true;
	}

	private bool StartListContain(List<UnityEx.ValueTuple<HSceneManager.HEvent, int, HScene.StartMotion>> _startList, HSceneManager.HEvent target)
	{
		foreach (UnityEx.ValueTuple<HSceneManager.HEvent, int, HScene.StartMotion> _start in _startList)
		{
			if (_start.Item1 == target)
			{
				return true;
			}
		}
		return false;
	}

	private bool StartListContain(List<UnityEx.ValueTuple<HSceneManager.HEvent, int, HScene.StartMotion>> _startList, int target, HSceneManager.HEvent hEvent = HSceneManager.HEvent.Normal, int category = -1)
	{
		foreach (UnityEx.ValueTuple<HSceneManager.HEvent, int, HScene.StartMotion> _start in _startList)
		{
			if (_start.Item1 == hEvent && _start.Item2 == target)
			{
				if (category < 0)
				{
					return true;
				}
				if (_start.Item3.mode == category)
				{
					return true;
				}
			}
		}
		return false;
	}

	private bool StartListContain(List<UnityEx.ValueTuple<HSceneManager.HEvent, int, HScene.StartMotion>> _startList, HScene.StartMotion target)
	{
		foreach (UnityEx.ValueTuple<HSceneManager.HEvent, int, HScene.StartMotion> _start in _startList)
		{
			if (_start.Item3 == target)
			{
				return true;
			}
		}
		return false;
	}

	public void ChangeStateAllEquip()
	{
		objAccessory.ChangeStateAllEquip(AllEquip);
		AllEquip ^= true;
	}

	private void OnDisable()
	{
		UIGroup.blocksRaycasts = false;
		UIGroup.alpha = 0f;
		HItemCtrl.EndProc();
		endFade = -1;
		usePoint = false;
		objGaugeLockF.isOn = false;
		objGaugeLockM.isOn = false;
		categoryFinish.SetActive(_active: true);
		categoryFinish.gameObject.SetActive(value: true);
		objAccessory.EndProc();
		charaChoice.EndProc();
		categoryMain.EndProc();
		hPointCtrl.EndProc();
		objCloth.gameObject.SetActive(value: false);
		objAccessory.gameObject.SetActive(value: false);
		objClothCard.gameObject.SetActive(value: false);
		categoryMain.gameObject.SetActive(value: false);
		objHItem.SetActive(value: false);
		objLightCategory.SetActive(value: false);
		objMotionListPanel.SetActive(value: false);
		charaChoice.CloseChoice();
		charaChoice.gameObject.SetActive(value: false);
		HelpBase.SetActive(value: false);
		ReSetHelpText();
		for (int i = 0; i < categories.MainCategoryActive.Length; i++)
		{
			categories.MainCategoryActive[i] = false;
		}
		isLeaveItToYou = false;
		SetMotionListDraw(_active: false);
		SetEnableCategoryMain(_enable: false);
		SetEnableHItem(_enable: false);
		CategoryScroll.ListClear();
		MotionScroll.GetList().Clear();
		MotionScroll.ClearBlank();
		objClothCard.EndProc();
		categoryMainButton.interactable = true;
		hPointButton.interactable = true;
		List<GameObject> list = hSceneMotionPool.GetList();
		foreach (GameObject item in list)
		{
			UnityEngine.Object.Destroy(item);
		}
		list.Clear();
		chaFemales = null;
		if (beforeChoice != null)
		{
			beforeChoice.Dispose();
		}
		beforeChoice = null;
		if (base.enabled)
		{
			base.enabled = false;
		}
	}

	private void SetHelpActive(bool _active)
	{
		if (HelpBase.activeSelf != _active)
		{
			HelpBase.SetActive(_active);
		}
	}

	private void SetHelpText(string _text)
	{
		if (!(HelpTxt == null))
		{
			HelpTxt.text = _text;
		}
	}

	private void ReSetHelpText()
	{
		if (!(HelpTxt == null))
		{
			HelpTxt.text = "エッチを開始する";
		}
	}

	public void GuidProc(AnimatorStateInfo ai)
	{
		bool flag = false;
		int id = ctrlFlag.nowAnimationInfo.id;
		flag = ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 3 && (id == 3 || id == 13 || id == 14);
		if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 3 && ctrlFlag.nowAnimationInfo.ActionCtrl.Item2 == 5 && !NonTokushuCheckIDs.Contains(ctrlFlag.nowAnimationInfo.id))
		{
			SetHelpActive(_active: false);
			return;
		}
		int num = -1;
		if (!flag)
		{
			for (int i = 0; i < GuidTiming.Length; i++)
			{
				for (int j = 0; j < GuidTiming[i].Length; j++)
				{
					if (ai.IsName(GuidTiming[i][j]))
					{
						num = i;
						break;
					}
				}
				if (num >= 0)
				{
					break;
				}
			}
		}
		else
		{
			for (int k = 0; k < GuidTimingEtc.Length; k++)
			{
				for (int l = 0; l < GuidTimingEtc[k].Length; l++)
				{
					if (ai.IsName(GuidTimingEtc[k][l]))
					{
						num = k;
						break;
					}
				}
				if (num >= 0)
				{
					break;
				}
			}
		}
		if (num < 0)
		{
			SetHelpActive(_active: false);
			return;
		}
		if ((num != 1 && hScene.ctrlVoice.nowVoices[0].state == HVoiceCtrl.VoiceKind.voice) || hScene.ctrlVoice.nowVoices[0].state == HVoiceCtrl.VoiceKind.startVoice || hScene.ctrlVoice.nowVoices[1].state == HVoiceCtrl.VoiceKind.voice || hScene.ctrlVoice.nowVoices[1].state == HVoiceCtrl.VoiceKind.startVoice || ctrlFlag.voice.playStart > 4)
		{
			SetHelpActive(_active: false);
			return;
		}
		if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && hScene.GetProcBase() is Sonyu { nowInsert: not false })
		{
			SetHelpActive(_active: false);
			return;
		}
		if (hScene.NowChangeAnim)
		{
			SetHelpActive(_active: false);
			return;
		}
		if (num != 1 && ctrlFlag.initiative != 0)
		{
			SetHelpActive(_active: false);
			return;
		}
		switch (num)
		{
		case 0:
			ReSetHelpText();
			SetHelpActive(_active: true);
			break;
		case 1:
			SetHelpText("上で速くなる\n下で遅くなる");
			SetHelpActive(_active: true);
			break;
		case 2:
			SetHelpText("上で続ける\n下で抜く");
			SetHelpActive(_active: true);
			break;
		case 3:
			SetHelpText("続ける");
			SetHelpActive(_active: true);
			break;
		default:
			SetHelpActive(_active: false);
			break;
		}
	}

	public void HelpBaseActive(bool _active)
	{
		if (!(HelpBaseConfig == null) && HelpBaseConfig.activeSelf != _active)
		{
			HelpBaseConfig.SetActive(_active);
		}
	}

	public bool GetHelpActive()
	{
		if (HelpBaseConfig == null)
		{
			return false;
		}
		return HelpBaseConfig.activeSelf;
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
	}
}
