using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ActionGame;
using ActionGame.Chara;
using HarmonyLib;
using Illusion.Game;
using Manager;
using SaveData;
using UnityEngine;
using UnityEngine.Rendering;
using ksPlug.Character;
using ksPlug.Inputs;
using ksPlug.Menu;
using ksPlug.Shared;
using ksPlug.Story;
using ksPlug.Tools;

namespace ksPlug.HSession;

public class HCtrl : MonoBehaviour
{
	public class Menus
	{
		public BaseMenu invite;

		public BaseMenu posSel;

		public BaseMenu corner;

		public BaseMenu mapSel;

		public List<BaseMenu> guests = new List<BaseMenu>();
	}

	private const string cmpName = "HCtrl";

	public List<int> AvailableCategories = new List<int>();

	private float mainVoiceRate;

	private float secondVoiceRate;

	private AnimationListInfo _curInfo;

	private string _curState = string.Empty;

	public Menus HMenus = new Menus();

	public ChaControl FpvCustomCha;

	public int boyStartOrgasm;

	public Mover moverInCustomMove;

	public FPV.Source FpvSource = FPV.Source.Boy1;

	public bool UIisReduced;

	public List<GameObject> disabledUiGos = new List<GameObject>();

	private static List<HAnims.StartTyp> peepStartCtxList = new List<HAnims.StartTyp>
	{
		HAnims.StartTyp.Masturbate,
		HAnims.StartTyp.PeepShower,
		HAnims.StartTyp.PeepToilet,
		HAnims.StartTyp.Lesbian
	};

	private float chainTimer;

	private static float chainOpenTime = 3.75f;

	public List<HItem> HItems = new List<HItem>();

	private Dictionary<Renderer, ShadowCastingMode> inTouchHandledRnds = new Dictionary<Renderer, ShadowCastingMode>();

	public float inputDelay;

	public float userActTimer;

	public bool categoryChanged;

	private List<Mover> moversToReposition = new List<Mover>();

	private bool _cursorOverUI;

	private bool lockAibuOverUI;

	private float minEx_B;

	private float minEx_G;

	private float hTime;

	private float hStartTime;

	public Count.HInfo FreeHDefCount;

	public List<Item> lastAnimItemsFromMainOverride = new List<Item>();

	public HFlag HFlag { get; private set; }

	public HSceneProc HProc { get; private set; }

	public HSprite HSprite { get; private set; }

	public ChaCore MainBoy { get; private set; }

	public ChaCore MainGirl { get; private set; }

	public ChaCore SecondGirl { get; private set; }

	public bool HasSecondGirl => (Object)(object)SecondGirl != (Object)null;

	public List<HAnims.AnimInfo> HAnimInfos { get; private set; }

	public HAnims.AnimInfo CurAnim { get; private set; }

	public HAnims.State CurState { get; private set; }

	public HAnims.StartTyp StartTyp { get; private set; }

	public HAnims.Spot HSpot { get; private set; }

	public bool CursorOverUIS
	{
		get
		{
			if (!Core.dealer.CursorOverUIS)
			{
				return IsOverHMenusUIS();
			}
			return true;
		}
	}

	public bool IsTresspassH { get; private set; }

	public bool IsInInvitation
	{
		get
		{
			if ((Object)(object)HMenus.invite != (Object)null)
			{
				return HMenus.invite.IsBusy;
			}
			return false;
		}
	}

	public ChaControl MouseMovableCha { get; private set; }

	public FPV FpvCtrl { get; private set; }

	public Quaternion CamLightRot { get; private set; }

	public int MapID => Core.dealer.MapID;

	public bool HasResult
	{
		get
		{
			if (Core.dealer.InStory)
			{
				return !peepStartCtxList.Contains(StartTyp);
			}
			return false;
		}
	}

	public bool IsInit { get; private set; }

	public bool InPointMove { get; private set; }

	public bool InHandAction => IsTouch();

	public bool InKissMain => IsKiss();

	public bool IsFirstH { get; private set; }

	public bool IsDeflowered { get; private set; }

	public bool InChainOpen
	{
		get
		{
			if (chainTimer == 0f)
			{
				return CurState == HAnims.State.OrgOUT;
			}
			return true;
		}
	}

	public float ChainTimer => chainTimer;

	public bool UserHasPlay => userActTimer != 0f;

	public bool HasTresspassBut { get; private set; }

	public bool IsPeepingH
	{
		get
		{
			if (peepStartCtxList.Contains(StartTyp))
			{
				return !InvadedPeep;
			}
			return false;
		}
	}

	public bool InvadedPeep { get; private set; }

	public bool IsBoyOrg
	{
		get
		{
			if (CurState != HAnims.State.OrgM && CurState != HAnims.State.OrgBoth)
			{
				return CurState == HAnims.State.OrgOUT;
			}
			return true;
		}
	}

	public bool IsGirlOrg
	{
		get
		{
			if (CurState != HAnims.State.OrgF)
			{
				return CurState == HAnims.State.OrgBoth;
			}
			return true;
		}
	}

	public int BoyOrgCount { get; private set; }

	public bool InDealWithBoyVis => inTouchHandledRnds.Count != 0;

	public bool InLockAibuOverUI => lockAibuOverUI;

	public DefCamCtrl DefCamCtrl { get; private set; }

	public ParticlesCannon ParticlesCannon { get; private set; }

	public bool HasParticlesCannon => (Object)(object)ParticlesCannon != (Object)null;

	public static HCtrl Get
	{
		get
		{
			if (!Object.op_Implicit((Object)(object)Core.dealer.HCtrl))
			{
				return null;
			}
			return Core.dealer.HCtrl;
		}
	}

	public bool IsOverHMenusUIS()
	{
		if (!IsInit)
		{
			return false;
		}
		foreach (BaseMenu guest in HMenus.guests)
		{
			if (guest.IsCursorOver())
			{
				return true;
			}
		}
		if (!HMenus.invite.IsCursorOver() && !HMenus.mapSel.IsCursorOver() && !HMenus.posSel.IsCursorOver() && !HMenus.corner.IsCursorOver())
		{
			return HSprite.IsCursorOnPad();
		}
		return true;
	}

	public void SetHTimeAtStart(float frcTime = 0f, float frcStTime = 0f)
	{
		hTime = ((frcTime != 0f) ? frcTime : (Core.dealer.StoryHInf.hStartOnMap ? ((float)Settings.HPeriodDurStory * 60f * (Singleton<Cycle>.Instance.timer / (float)Cycle.TIME_LIMIT)) : 0f));
		hStartTime = ((frcStTime != 0f) ? frcTime : hTime);
	}

	public void GetCurrentTimeAndStart(ref float time, ref float startTime)
	{
		time = hTime;
		startTime = hStartTime;
	}

	private void Start()
	{
		FpvSource = ((Settings.FpvViewer == FPV.OptionSrc.Boy) ? FPV.Source.Boy1 : FPV.Source.Girl1);
		BoyOrgCount = 0;
		((MonoBehaviour)this).StartCoroutine(IE_Init());
	}

	private void Update()
	{
		if (!IsInit)
		{
			return;
		}
		bool flag = false;
		if (_curInfo != HFlag.nowAnimationInfo)
		{
			if (chainTimer != 0f)
			{
				chainTimer = 0f;
			}
			HManage.ResetMainCtrlBones();
			DefineCurAnim();
			flag = true;
			UpdateMoversOnChange(animChange: true);
		}
		if (HSpot != HAnims.GetSpotFromInt(HProc.categorys[0]) || categoryChanged)
		{
			HSpot = HAnims.GetSpotFromInt(HProc.categorys[0]);
			if (!flag)
			{
				if ((Object)(object)FpvCtrl != (Object)null)
				{
					((MonoBehaviour)this).StartCoroutine(IE_ResetFpv(onAnimChange: false));
				}
				UpdateMoversOnChange(animChange: false);
			}
			categoryChanged = false;
		}
		if (_curState != HFlag.nowAnimStateName)
		{
			bool num = CurState == HAnims.State.OrgM || CurState == HAnims.State.OrgBoth || CurState == HAnims.State.OrgOUT;
			bool flag2 = CurState == HAnims.State.OrgM || CurState == HAnims.State.OrgBoth || CurState == HAnims.State.OrgF;
			DefineCurState();
			if (CurState == HAnims.State.OrgOUT && !HFlag.nowAnimStateName.Contains("Start"))
			{
				((MonoBehaviour)this).StartCoroutine(IE_PlayerCumsOut());
			}
			if (CurState == HAnims.State.Insert && chainTimer != 0f)
			{
				chainTimer = 0f;
			}
			if (CurAnim.plugName.Contains("Mast") && CurState == HAnims.State.OrgF)
			{
				Count count = HFlag.count;
				count.aibuOrg++;
			}
			if (!num && (CurState == HAnims.State.OrgM || CurState == HAnims.State.OrgBoth || CurState == HAnims.State.OrgOUT))
			{
				BoyOrgCount++;
				DebugInfos.InConsole("Adding Orgasm to boy", "HCtrl");
			}
			if (!flag2 && (CurState == HAnims.State.OrgM || CurState == HAnims.State.OrgBoth || CurState == HAnims.State.OrgF))
			{
				if (CurState == HAnims.State.OrgF)
				{
					HFlag.lstHeroine[HManage.IsSecondMain() ? 1 : 0].lewdness = Mathf.Min(HFlag.lstHeroine[0].lewdness + 15, 100);
				}
				((MonoBehaviour)this).StartCoroutine(IE_ChainOpen());
			}
			if (((ChaInfo)MainBoy.Cha).sex == 1)
			{
				MainBoy.Cha.playDynamicBoneBust((DynamicBoneKind)0, true);
				MainBoy.Cha.playDynamicBoneBust((DynamicBoneKind)1, true);
				MainBoy.Cha.playDynamicBoneBust((DynamicBoneKind)2, true);
				MainBoy.Cha.playDynamicBoneBust((DynamicBoneKind)3, true);
			}
			if (!flag)
			{
				HManage.ResetMainCtrlBones();
				HManage.CheckAndSetMainSizeOffsets();
			}
		}
		if (inputDelay != 0f)
		{
			Helper.TimerToZero(ref inputDelay, freezeInOverlap: false);
		}
		if (chainTimer != 0f && !Core.dealer.InProcess)
		{
			Helper.TimerToZero(ref chainTimer);
		}
		if (userActTimer != 0f)
		{
			Helper.TimerToZero(ref userActTimer);
		}
		HAction.MainActionControl();
		HAction.OtherControl();
		HManage.ParseMoverFocus();
		if (!Object.op_Implicit((Object)(object)FpvCtrl))
		{
			Cam.bpInSight = ChaTools.SearchColDefInCamSight();
		}
		if (CursorOverUIS != _cursorOverUI)
		{
			if (CursorOverUIS && HFlag.isAibuSelect)
			{
				HFlag.isAibuSelect = false;
				lockAibuOverUI = true;
			}
			if (!CursorOverUIS && lockAibuOverUI)
			{
				HFlag.isAibuSelect = true;
				lockAibuOverUI = false;
			}
			_cursorOverUI = CursorOverUIS;
		}
		if (CursorOverUIS)
		{
			GlobalMethod.SetCameraMoveFlag(Object.FindObjectOfType<CameraControl_Ver2>(), false);
		}
	}

	private void LateUpdate()
	{
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		if (!IsInit)
		{
			return;
		}
		if ((Object)(object)HSprite.btnEnd != (Object)null)
		{
			((Component)HSprite.btnEnd).gameObject.SetActive(GetEndButtonState());
			if (UIisReduced && ((Component)HSprite.btnEnd).gameObject.activeSelf)
			{
				((Component)HSprite.btnEnd).gameObject.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
			}
		}
		if (HasTresspassBut && InvadedPeep && ((Behaviour)HSprite.btnTrespassing).isActiveAndEnabled)
		{
			((Component)HSprite.btnTrespassing).gameObject.SetActive(false);
			HasTresspassBut = false;
		}
		if (InHandAction && Core.dealer.BoyIsVisInH && Config.HData.VisibleBody)
		{
			HTouch.DealWithBoyVisInTouch(Settings.VisModInTch, ref inTouchHandledRnds);
		}
		if (!InHandAction && inTouchHandledRnds.Count != 0)
		{
			HTouch.RestoreShdwModFromDic(inTouchHandledRnds);
			inTouchHandledRnds.Clear();
		}
		if (UIisReduced)
		{
			if (CurAnim.ctx == HAnims.Context.Tease)
			{
				((Behaviour)HSprite.imageSpeedSlliderCover70).enabled = false;
			}
			if (CurAnim.ctx == HAnims.Context.Service)
			{
				((Component)HSprite.houshi.btAutoFinish).gameObject.SetActive(false);
			}
		}
		if (HFlag.gaugeMale < minEx_B)
		{
			HFlag.gaugeMale = minEx_B;
		}
		if (HFlag.gaugeFemale < minEx_G)
		{
			HFlag.gaugeFemale = minEx_G;
		}
		if (CurAnim.isSpSrv)
		{
			HFlag.gaugeFemale = HFlag.gaugeMale;
		}
		if (string.IsNullOrEmpty(CurAnim.animList.paramFemale.fileMotionNeck) && ((ChaInfo)MainGirl.Cha).neckLookCtrl.ptnNo != 3)
		{
			MainGirl.Cha.ChangeLookNeckPtn(3, 1f);
		}
		if (CurAnim.useTongueG1 && CurState == HAnims.State.Motion)
		{
			if (MainGirl.Cha.GetTongueState() != 1)
			{
				MainGirl.Cha.ChangeTongueState((byte)1);
			}
			if (MainGirl.Cha.GetMouthPtn() != 21)
			{
				MainGirl.Cha.ChangeMouthPtn(21, true);
			}
		}
		if (HasSecondGirl && string.IsNullOrEmpty(CurAnim.animList.paramFemale1.fileMotionNeck) && ((ChaInfo)SecondGirl.Cha).neckLookCtrl.ptnNo != 3)
		{
			SecondGirl.Cha.ChangeLookNeckPtn(3, 1f);
		}
		if (HasSecondGirl && CurAnim.useTongueG2 && CurState == HAnims.State.Motion && CurState == HAnims.State.Motion)
		{
			if (SecondGirl.Cha.GetTongueState() != 1)
			{
				SecondGirl.Cha.ChangeTongueState((byte)1);
			}
			if (SecondGirl.Cha.GetMouthPtn() != 21)
			{
				SecondGirl.Cha.ChangeMouthPtn(21, true);
			}
		}
		if (Core.dealer.BoyIsVisInH && CurAnim.isExtraAnim && ((ChaInfo)MainBoy.Cha).neckLookCtrl.ptnNo != 3)
		{
			((ChaInfo)MainBoy.Cha).neckLookCtrl.ptnNo = 3;
		}
		if (!Core.dealer.InChaSwap && Count.MatchHCounts(HFlag.count, MainGirl.HInfo.HCount, debgMsg: true))
		{
			if (HasSecondGirl)
			{
				Count.MatchHCounts(HFlag.count, SecondGirl.HInfo.HCount, debgMsg: true);
			}
			CalcMinExcitation();
		}
		if (CurAnim.ctx == HAnims.Context.Piston && CurState == HAnims.State.Insert && !HFlag.isCondom && !HFlag.lockGugeMale)
		{
			HFlag.gaugeMale = Mathf.Clamp(HFlag.gaugeMale + 3.5f * Time.deltaTime, 0f, 100f);
		}
		if ((CurAnim.ctx == HAnims.Context.Tease || CurAnim.ctx == HAnims.Context.Peep) && !HasParticlesCannon)
		{
			ChaTools.ColDefinition colDefinition = ChaTools.SearchColDefInCamSight();
			if ((Object)(object)colDefinition != (Object)null)
			{
				AI component = ((Component)colDefinition.cha).GetComponent<AI>();
				if ((Object)(object)component != (Object)null)
				{
					if (component.Typ == AI.Type.Guest && component.InAction && component.gAction.Typ != GuestAction.Type.Bound && component.gAction.Typ != GuestAction.Type.Tease)
					{
						HFlag.gaugeMale = Mathf.Clamp(HFlag.gaugeMale + Time.deltaTime * 5f, 0f, 100f);
					}
					if (component.Typ != AI.Type.Guest)
					{
						string plugName = CurAnim.plugName;
						if ((Object)(object)((Component)component.ChaCore).GetComponent<MainActionOver>() != (Object)null || plugName.Contains("Mast_") || plugName.Contains("Lez_") || plugName.Contains("Peep_"))
						{
							HFlag.gaugeMale = Mathf.Clamp(HFlag.gaugeMale + Time.deltaTime * 8f, 0f, 100f);
						}
					}
				}
			}
			if (HFlag.gaugeMale > 0f && (Keys.ShootSiru || (Keys.LmbHold && Keys.MmbClick && !InKissMain && !Core.dealer.InKissAI)))
			{
				ParticlesCannon = ParticlesCannon.Create(ParticlesCannon.ParticleTyp.Siru);
			}
			if (Keys.ShootWater)
			{
				ParticlesCannon = ParticlesCannon.Create(ParticlesCannon.ParticleTyp.Water);
			}
		}
		if (Settings.TimePassesInH)
		{
			hTime += Time.deltaTime;
			float num = (Core.dealer.InStory ? ((float)Settings.HPeriodDurStory) : ((float)Settings.HPeriodDurFreeH));
			if (hTime / 60f > num * ((Map.ambiance != Map.AmbTyp.Night) ? 1f : ((float)Settings.HNightDurFactor * 0.01f)))
			{
				((MonoBehaviour)this).StartCoroutine(IE_NextAmbiance());
				hTime = 0f;
				hStartTime = 0f;
			}
			if (Core.dealer.InStory)
			{
				Core.dealer.StoryHInf.hTime = hTime - hStartTime;
			}
		}
	}

	private IEnumerator IE_NextAmbiance()
	{
		yield return (object)new WaitUntil((Func<bool>)(() => !((BaseMap)Core.dealer.ActMap).isMapLoading && !Map.InChangeMap));
		Sound.Play((SystemSE)4);
		yield return (object)new WaitForSeconds(0.1f);
		Cam.DoFade(0.85f);
		Map.AmbTyp ambiance = Map.ambiance;
		(HMenus.mapSel as MenuMap).SetNextAmbiance();
		if (Core.dealer.InStory)
		{
			Core.dealer.StoryHInf.numOfAmbChange++;
			Core.dealer.StoryHInf.ambAtChange = Map.ambiance;
			if (ambiance == Map.AmbTyp.Night)
			{
				Core.dealer.StoryHInf.endDayByAmbChng = true;
			}
		}
	}

	private void OnDestroy()
	{
		if ((Object)(object)FpvCtrl != (Object)null)
		{
			Object.Destroy((Object)(object)FpvCtrl);
		}
		DebugInfos.InConsole("Destoyed", "HCtrl");
	}

	public void IncreaseMainGirlGauge(float incr, float max = 99f)
	{
		if (Get.CurAnim.isSpSrv)
		{
			if (!HFlag.lockGugeMale)
			{
				HFlag.gaugeMale = Mathf.Min(HFlag.gaugeMale + incr, max);
			}
		}
		else if (!HFlag.lockGugeFemale)
		{
			HFlag.gaugeFemale = Mathf.Min(HFlag.gaugeFemale + incr, max);
		}
	}

	private bool GetEndButtonState()
	{
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Invalid comparison between Unknown and I4
		if (Core.dealer.InProcess)
		{
			return false;
		}
		if (IsInInvitation)
		{
			return false;
		}
		if (CurAnim.ctx != HAnims.Context.Peep && IsFirstH && !IsDeflowered)
		{
			return false;
		}
		if (StartTyp != HAnims.StartTyp.Masturbate && !HFlag.isFreeH && CurAnim.plugName.Contains("Mast"))
		{
			return false;
		}
		if (Core.dealer.InStory && (int)CurAnim.animList.mode == 3 && StartTyp != HAnims.StartTyp.Masturbate)
		{
			return false;
		}
		return true;
	}

	public void CalcMinExcitation()
	{
		if (Settings.BoyExGain != 0 && Settings.BoyExMax != 0)
		{
			minEx_B = Mathf.Clamp((float)(Settings.BoyExGain * Count.MainBoyOrgasms()), 0f, (float)Settings.BoyExMax);
		}
		if (Settings.GirlExGain != 0 && Settings.GirlExMax != 0)
		{
			minEx_G = Mathf.Clamp((float)(Settings.GirlExGain * Count.GirlOrgasms(HFlag.count)), 0f, (float)Settings.GirlExMax);
		}
	}

	public bool IsTouch()
	{
		if (!HProc.hand.IsItemTouch())
		{
			return HProc.hand1.IsItemTouch();
		}
		return true;
	}

	public bool IsKiss()
	{
		if (!HProc.hand.IsKissAction())
		{
			return HProc.hand1.IsKissAction();
		}
		return true;
	}

	private void DefineCurAnim()
	{
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		if (HFlag.nowAnimationInfo == null)
		{
			return;
		}
		_curInfo = HFlag.nowAnimationInfo;
		foreach (HAnims.AnimInfo hAnimInfo in HAnimInfos)
		{
			string lastString = Helper.GetLastString(hAnimInfo.animList.paramFemale.path.assetpath, "/", rmvExt: true);
			if (hAnimInfo.animList.paramFemale.path.file.Contains("khr_"))
			{
				lastString = Helper.GetLastString(hAnimInfo.animList.pathFemaleBase.assetpath, "/", rmvExt: true);
			}
			if (!(lastString + "/" + _curInfo.paramFemale.path.file == hAnimInfo.asset))
			{
				continue;
			}
			CurAnim = hAnimInfo;
			DebugInfos.InConsole(" CurrentAnim is : ", "HCtrl.SetCurrentAnim");
			DebugInfos.AnimInfo(hAnimInfo);
			DebugInfos.InConsoleCompact(" CurrentSpot is : " + HSpot, "HCtrl.SetCurrentAnim");
			string text = ((object)((Component)MainGirl.Cha).transform.position/*cast due to .constrained prefix*/).ToString();
			Quaternion rotation = ((Component)MainGirl.Cha).transform.rotation;
			DebugInfos.InConsoleCompact(" Character pos : " + text + " rot :" + ((object)((Quaternion)(ref rotation)).eulerAngles/*cast due to .constrained prefix*/).ToString(), "HCtrl.SetCurrentAnim");
			AI component = ((Component)Core.dealer.HCtrl.MainGirl).GetComponent<AI>();
			if (CurAnim.isExtraAnim)
			{
				DebugInfos.InConsole(" This is an Additional animations, try to set IK: ", "HCtrl");
				IKS.SetExtraAnimIKS();
				if (!string.IsNullOrEmpty(CurAnim.cusDickTarg))
				{
					Core.dealer.HCtrl.HProc.lookDan.transLookAtNull = ChaTools.FindInCha(Core.dealer.HCtrl.MainGirl.Cha, CurAnim.cusDickTarg);
					Core.dealer.HCtrl.HProc.lookDan.lookAtCalcKind = 1;
					Core.dealer.HCtrl.HProc.lookDan.bTopStick = false;
				}
			}
			SizeOffsets.ReadAllAtChange();
			SetAnimOffsets();
			Binders.SetForGivenMainAnim(CurAnim);
			HManage.CheckAndSetMainSizeOffsets();
			if (CurAnim.propsOnG1 && (Object)(object)component != (Object)null)
			{
				component.LoadAnimItem(_curInfo.paramFemale.path.file);
			}
			if (lastAnimItemsFromMainOverride.Count == 0)
			{
				break;
			}
			foreach (Item item in lastAnimItemsFromMainOverride)
			{
				Object.Destroy((Object)(object)item.objItem);
			}
			lastAnimItemsFromMainOverride.Clear();
			break;
		}
		DebugInfos.DebugForward(MainGirl.Cha);
	}

	public void CheckBeforePoseChange()
	{
		if (CurAnim != null && CurAnim.isExtraAnim)
		{
			bool flag = CurAnim.animList.paramMale != null && CurAnim.animList.paramMale.path != null && !string.IsNullOrEmpty(CurAnim.animList.paramMale.path.file);
			bool num = CurAnim.animList.paramFemale1 != null && CurAnim.animList.paramFemale1.path != null && !string.IsNullOrEmpty(CurAnim.animList.paramFemale1.path.file);
			IKS.ForceIkDefaultGoals(MainGirl.Cha);
			if (flag)
			{
				IKS.ForceIkDefaultGoals(MainBoy.Cha);
			}
			if (num)
			{
				IKS.ForceIkDefaultGoals(SecondGirl.Cha);
			}
		}
		if (CurAnim != null)
		{
			SetAnimOffsets(-1f);
		}
		if (IsPeepingH)
		{
			((MonoBehaviour)this).StartCoroutine(IE_InvadePeep());
		}
		HItem.DetachAll();
		Binders.DeleteAllBinders();
		MainActionOver.DestroyOverride(MainGirl, restore: false);
	}

	private IEnumerator IE_InvadePeep()
	{
		HAnims.AnimInfo curAnim = CurAnim;
		yield return (object)new WaitUntil((Func<bool>)(() => CurAnim != curAnim));
		if (CurAnim.ctx != HAnims.Context.Peep)
		{
			if (StartTyp == HAnims.StartTyp.PeepShower)
			{
				MainBoy.Cha.SetClothesStateAll((byte)3);
			}
			InvadedPeep = true;
			Enums.ChainTyp typ = Enums.ChainTyp.None;
			if (CurAnim.ctx == HAnims.Context.Piston)
			{
				typ = (((CurAnim.plugName.Contains("Back") || CurAnim.plugName.Contains("Doggy")) && CurAnim.hasAnal) ? Enums.ChainTyp.Anal : Enums.ChainTyp.Vagina);
			}
			if (CurAnim.ctx == HAnims.Context.Piston || CurAnim.ctx == HAnims.Context.Service)
			{
				HFlag.gaugeMale = 60f;
				chainTimer = 3.75f;
			}
			((MonoBehaviour)this).StartCoroutine(MenuPose.IE_ChainAnims(typ, isQuickChain: false, waitNextAnim: false));
			Sound.Play((Setting)new SettingBGM
			{
				bundle = "sound/data/bgm/kks_bgm_03.unity3d",
				asset = "kks_bgm_03"
			});
			if ((Object)(object)Singleton<ActionScene>.Instance.Player.chaser != (Object)null && ((Base)Singleton<ActionScene>.Instance.Player.chaser).heroine != null)
			{
				MenuInvite.InvFromOtherComponent(((Base)Singleton<ActionScene>.Instance.Player.chaser).heroine);
			}
		}
	}

	private void SetAnimOffsets(float factor = 1f)
	{
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_019c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Unknown result type (might be due to invalid IL or missing references)
		//IL_0231: Unknown result type (might be due to invalid IL or missing references)
		//IL_0236: Unknown result type (might be due to invalid IL or missing references)
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_0275: Unknown result type (might be due to invalid IL or missing references)
		//IL_027a: Unknown result type (might be due to invalid IL or missing references)
		bool flag = CurAnim.animList.paramMale != null && CurAnim.animList.paramMale.path != null && !string.IsNullOrEmpty(CurAnim.animList.paramMale.path.file);
		bool flag2 = CurAnim.animList.paramFemale1 != null && CurAnim.animList.paramFemale1.path != null && !string.IsNullOrEmpty(CurAnim.animList.paramFemale1.path.file);
		if (CurAnim.zOffset != 0f || CurAnim.xOffset != 0f)
		{
			Transform transform = ((ChaInfo)MainGirl.Cha).objAnim.transform;
			transform.localPosition += new Vector3(CurAnim.xOffset * factor, 0f, CurAnim.zOffset * factor);
			if (flag)
			{
				Transform transform2 = ((ChaInfo)MainBoy.Cha).objAnim.transform;
				transform2.localPosition += new Vector3(CurAnim.xOffset * factor, 0f, CurAnim.zOffset * factor);
			}
			if (flag2)
			{
				Transform transform3 = ((ChaInfo)SecondGirl.Cha).objAnim.transform;
				transform3.localPosition += new Vector3(CurAnim.xOffset * factor, 0f, CurAnim.zOffset * factor);
			}
		}
		if (CurAnim.yRotOffset != 0f)
		{
			Transform transform4 = ((ChaInfo)MainGirl.Cha).objAnim.transform;
			transform4.localEulerAngles += new Vector3(0f, CurAnim.yRotOffset * factor, 0f);
			if (flag)
			{
				Transform transform5 = ((ChaInfo)MainBoy.Cha).objAnim.transform;
				transform5.localEulerAngles += new Vector3(0f, CurAnim.yRotOffset * factor, 0f);
			}
			if (flag2)
			{
				Transform transform6 = ((ChaInfo)SecondGirl.Cha).objAnim.transform;
				transform6.localEulerAngles += new Vector3(0f, CurAnim.yRotOffset * factor, 0f);
			}
		}
	}

	private void DefineCurState()
	{
		_curState = HFlag.nowAnimStateName;
		CurState = HAnims.GetState(_curState, CurAnim.ctx);
		if (CurAnim.isExtraAnim)
		{
			IKS.SetExtraAnimIKS();
		}
		if (HFlag.nowAnimStateName.Contains("InsertIdle") && !HFlag.isCondom && !HFlag.isAnalPlay)
		{
			Count.AddNamaToInfo(HManage.IsSecondMain() ? SecondGirl.HInfo : MainGirl.HInfo);
			DebugInfos.InConsole(" state is : " + _curState + " known as " + CurState.ToString() + " add Nama Insertion", "HCtrl");
		}
		if (IsFirstH && !IsDeflowered && HAnims.IsOrgasm(CurState) && !HFlag.isAnalPlay)
		{
			bool flag = MainGirl.Typ == ChaCore.Type.MainGirl;
			IsDeflowered = (flag ? true : false);
			if (!flag)
			{
				DebugInfos.InConsole("!! Cancel Deflower, " + ChaCore.Name(((ChaInfo)MainGirl.Cha).fileParam) + " is not the MainGirl", "HCtrl");
			}
		}
		if (!string.IsNullOrEmpty(CurAnim.cusDickTarg))
		{
			Core.dealer.HCtrl.HProc.lookDan.transLookAtNull = ChaTools.FindInCha(Core.dealer.HCtrl.MainGirl.Cha, CurAnim.cusDickTarg);
			Core.dealer.HCtrl.HProc.lookDan.lookAtCalcKind = 1;
			Core.dealer.HCtrl.HProc.lookDan.bTopStick = false;
		}
	}

	public void SetInPointMove(bool state)
	{
		if (state)
		{
			((ChaInfo)MainGirl.Cha).visibleAll = true;
			if (Core.dealer.BoyIsVisInH)
			{
				((ChaInfo)MainBoy.Cha).visibleAll = true;
			}
		}
		InPointMove = state;
	}

	public void ResetFpv(bool onPosChange = true)
	{
		if ((Object)(object)FpvCtrl != (Object)null)
		{
			((MonoBehaviour)this).StartCoroutine(IE_ResetFpv(onPosChange));
		}
	}

	public void ResetUIOnPoseChange()
	{
		if (UIisReduced)
		{
			((MonoBehaviour)this).StartCoroutine(IE_ResetUI(onAnimChange: true));
		}
	}

	public void DestroyFPV()
	{
		Object.Destroy((Object)(object)FpvCtrl);
	}

	private IEnumerator IE_AddFpvAtStart()
	{
		yield return (object)new WaitForEndOfFrame();
		CamLightRot = Quaternion.LookRotation(((Component)((CharaData)HFlag.lstHeroine[0]).chaCtrl).transform.position - Vector3.up * 0.2f - (((Component)HProc.lightCamera).transform.position + Vector3.up * 1.25f), Vector3.up);
		yield return (object)new WaitForSeconds(Time.deltaTime);
		FpvCtrl = FPV.Create();
	}

	public void AddFPV()
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		CamLightRot = ((Component)HProc.lightCamera).transform.rotation;
		FpvCtrl = FPV.Create();
	}

	private void UpdateMoversOnChange(bool animChange)
	{
		foreach (Mover chaMover in Core.dealer.ChaMovers)
		{
			if (chaMover.TestObstacle())
			{
				moversToReposition.Add(chaMover);
			}
			else if (!chaMover.TestContact(animChange))
			{
				moversToReposition.Add(chaMover);
			}
		}
		if (moversToReposition.Count != 0)
		{
			((MonoBehaviour)this).StartCoroutine(IE_ReposGuestList());
		}
	}

	public void Tresspass()
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Invalid comparison between Unknown and I4
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Invalid comparison between Unknown and I4
		HFlag.click = (ClickKind)30;
		HFlag.isHSceneEnd = true;
		HFlag.numEnd = 2;
		if ((int)HFlag.mode == 4)
		{
			HFlag.mode = (EMode)3;
		}
		if ((int)HFlag.mode == 3 && HFlag.isMasturbationFound)
		{
			HFlag.lstHeroine[0].lewdness = 100;
		}
		ActionScene instance = SingletonInitializer<ActionScene>.instance;
		if (Object.op_Implicit((Object)(object)instance))
		{
			instance.isPenetration = true;
		}
		if ((Object)(object)HFlag.shortcut != (Object)null)
		{
			((Behaviour)HFlag.shortcut).enabled = false;
		}
		if (Object.op_Implicit((Object)(object)instance))
		{
			instance.ShortcutKeyEnable(false);
		}
	}

	public void SetMouseMovableCha(ChaControl cha)
	{
		MouseMovableCha = cha;
	}

	public void RestoreVoiceRateAtStart()
	{
		for (int i = 0; i < HFlag.lstHeroine.Count; i++)
		{
			if (i == 0)
			{
				((CharaData)HFlag.lstHeroine[0]).parameter.voiceRate = mainVoiceRate;
			}
			if (i == 1)
			{
				((CharaData)HFlag.lstHeroine[1]).parameter.voiceRate = secondVoiceRate;
			}
		}
	}

	public void ChainOpenAtOutChain()
	{
		chainTimer = chainOpenTime + 1f;
	}

	private IEnumerator IE_ChainOpen()
	{
		HAnims.State entryState = CurState;
		DebugInfos.InConsoleCompact("IE_ChainOpen entry state is " + CurState);
		yield return (object)new WaitUntil((Func<bool>)(() => entryState != CurState && CurState != HAnims.State.OrgIdle && CurState != HAnims.State.InsertIdle));
		DebugInfos.InConsoleCompact("IE_ChainOpen next state is " + CurState);
		if (CurState == HAnims.State.Pull)
		{
			DebugInfos.InConsoleCompact("IE_ChainOpen launch chainTimer");
			chainTimer = chainOpenTime;
		}
	}

	private IEnumerator IE_PlayerCumsOut()
	{
		float num = 0.3f;
		AnimatorStateInfo currentAnimatorStateInfo = ((ChaInfo)MainBoy.Cha).animBody.GetCurrentAnimatorStateInfo(0);
		yield return (object)new WaitForSeconds(((AnimatorStateInfo)(ref currentAnimatorStateInfo)).length * num);
		if (CurAnim.isExtraAnim && CurAnim.hasSiruFlags)
		{
			ChaTools.AddSiruOnCha(MainGirl.Cha, CurAnim.siruFlags);
		}
		GuestAction[] array = Object.FindObjectsOfType<GuestAction>();
		for (int i = 0; i < array.Length; i++)
		{
			array[i].SetSiruOnCha();
		}
	}

	private IEnumerator IE_ReposGuestList()
	{
		yield return (object)new WaitForSeconds(Time.deltaTime);
		foreach (Mover item in moversToReposition)
		{
			WSpace.Contact contact = WSpace.SearchGuestPosAround();
			item.SetContact(contact, adaptAiPose: true);
			yield return (object)new WaitForSeconds(Time.deltaTime);
		}
		moversToReposition.Clear();
	}

	public IEnumerator IE_ResetFpv(bool onAnimChange)
	{
		Object.Destroy((Object)(object)FpvCtrl);
		if (onAnimChange)
		{
			HAnims.AnimInfo curAnim = CurAnim;
			yield return (object)new WaitUntil((Func<bool>)(() => curAnim != CurAnim));
		}
		else
		{
			yield return (object)new WaitForSeconds(Time.deltaTime);
		}
		FpvCtrl = FPV.Create();
	}

	private IEnumerator IE_ResetUI(bool onAnimChange)
	{
		HUI.Reduce(ref UIisReduced, ref disabledUiGos);
		if (onAnimChange)
		{
			AnimationListInfo curAnim = HFlag.nowAnimationInfo;
			yield return (object)new WaitUntil((Func<bool>)(() => curAnim != HFlag.nowAnimationInfo));
			Cam.DoFade(0.75f);
		}
		else
		{
			yield return (object)new WaitForSeconds(Time.deltaTime);
		}
		HUI.Reduce(ref UIisReduced, ref disabledUiGos);
	}

	public void GetAvailableCategories()
	{
		AvailableCategories = Traverse.Create((object)HProc).Field("useCategorys").GetValue() as List<int>;
		foreach (int availableCategory in AvailableCategories)
		{
			DebugInfos.InConsoleCompact("Map available cat " + availableCategory);
		}
	}

	private IEnumerator IE_Init()
	{
		yield return (object)new WaitUntil((Func<bool>)(() => (Object)(object)Singleton<HFlag>.Instance != (Object)null && !Scene.IsNowLoading));
		HFlag = Singleton<HFlag>.Instance;
		yield return (object)new WaitUntil((Func<bool>)(() => (Object)(object)Singleton<HSceneProc>.Instance != (Object)null));
		HProc = Singleton<HSceneProc>.Instance;
		yield return (object)new WaitUntil((Func<bool>)(() => (Object)(object)((CharaData)HFlag.lstHeroine[0]).chaCtrl != (Object)null && ((CharaData)HFlag.lstHeroine[0]).chaCtrl.GetActiveTop() && (Object)(object)((Component)((CharaData)HFlag.lstHeroine[0]).chaCtrl).GetComponent<ChaCore>() != (Object)null));
		HSprite = HProc.sprite;
		HAnimInfos = HAnims.CreateAnimInfoList();
		GuestAction.InitActionAnimList();
		yield return (object)new WaitUntil((Func<bool>)(() => (Object)(object)((CharaData)HFlag.player).chaCtrl != (Object)null && ((CharaData)HFlag.player).chaCtrl.GetActiveTop() && (Object)(object)((Component)((CharaData)HFlag.player).chaCtrl).GetComponent<ChaCore>() != (Object)null && (Object)(object)HFlag.ctrlCamera != (Object)null && (GlobalMethod.IsCameraMoveFlag(HFlag.ctrlCamera) || GlobalMethod.IsCameraActionFlag(HFlag.ctrlCamera))));
		bool moveToServ = Keys.LmbHold;
		bool moveToSex = Keys.RmbHold;
		bool moveToMast = Keys.MmbHold;
		GetAvailableCategories();
		if (Core.dealer.InStory)
		{
			GManage.ChaSnapshot andApplySnapshot = GManage.GetAndApplySnapshot((CharaData)(object)HFlag.player, ((CharaData)HFlag.player).chaCtrl);
			if (andApplySnapshot != null && andApplySnapshot.orgAtEcchiEnd != 0)
			{
				boyStartOrgasm = andApplySnapshot.orgAtEcchiEnd;
				CalcMinExcitation();
			}
		}
		DefCamCtrl = DefCamCtrl.Create();
		if (Settings.FpvAtStart && (int)Settings.ScFpvToggle.actKey != 0)
		{
			((MonoBehaviour)this).StartCoroutine(IE_AddFpvAtStart());
		}
		HasTresspassBut = (Object)(object)HSprite.btnTrespassing != (Object)null && ((Behaviour)HSprite.btnTrespassing).isActiveAndEnabled;
		if (HasTresspassBut)
		{
			DebugInfos.InConsole(" HSprite has trespassing button", "HCtrl");
		}
		if (Settings.ReduceHUI)
		{
			HUI.Reduce(ref UIisReduced, ref disabledUiGos);
		}
		yield return (object)new WaitUntil((Func<bool>)(() => HFlag.nowAnimationInfo != null));
		DefineCurAnim();
		StartTyp = HAnims.GetStartTypFromInfo(CurAnim);
		yield return (object)new WaitUntil((Func<bool>)(() => !Scene.IsNowLoadingFade));
		IsFirstH = Core.dealer.InStory && HFlag.lstHeroine[0].isVirgin;
		foreach (Heroine item in HFlag.lstHeroine)
		{
			if ((Object)(object)((CharaData)item).chaCtrl != (Object)null)
			{
				DebugInfos.InConsoleCompact(" Starting H with : " + ((ChaInfo)((CharaData)item).chaCtrl).fileParam.fullname, "HCtrl");
				Statistic.PreventMaxLewdness(item);
			}
		}
		for (int num = 0; num < HFlag.lstHeroine.Count; num++)
		{
			if (num == 0)
			{
				mainVoiceRate = ((CharaData)HFlag.lstHeroine[0]).parameter.voiceRate;
			}
			if (num == 1)
			{
				secondVoiceRate = ((CharaData)HFlag.lstHeroine[1]).parameter.voiceRate;
			}
		}
		DebugInfos.InConsoleCompact("!!! H Start Type is " + StartTyp.ToString() + (IsFirstH ? " ( First H )" : ""), "HCtrl");
		HManage.UnlockSpecialPositions();
		HMenus.invite = BaseMenu.Create(((Component)this).gameObject, BaseMenu.Type.Invitation);
		HMenus.posSel = BaseMenu.Create(((Component)this).gameObject, BaseMenu.Type.PoseSelector);
		HMenus.corner = BaseMenu.Create(((Component)this).gameObject, BaseMenu.Type.Corner);
		HMenus.mapSel = BaseMenu.Create(((Component)this).gameObject, BaseMenu.Type.MapSelector);
		yield return (object)new WaitForEndOfFrame();
		GroundPlane.Create(((CharaData)HFlag.lstHeroine[0]).chaCtrl);
		yield return (object)new WaitForSeconds(Time.deltaTime);
		if ((moveToServ || moveToMast || moveToSex) && CurAnim.ctx != HAnims.Context.Peep && CurAnim.ctx != HAnims.Context.Undefined)
		{
			AnimationListInfo current2 = HFlag.nowAnimationInfo;
			if (moveToSex && CurAnim.ctx != HAnims.Context.Piston)
			{
				MenuPose.ChangeStartAnim(HAnims.Context.Piston);
			}
			else if (moveToServ && CurAnim.ctx != HAnims.Context.Service)
			{
				MenuPose.ChangeStartAnim(HAnims.Context.Service);
			}
			else if (moveToMast && CurAnim.ctx != HAnims.Context.Peep)
			{
				MenuPose.ChangeStartAnim(HAnims.Context.Peep);
			}
			yield return (object)new WaitUntil((Func<bool>)(() => current2 != HFlag.nowAnimationInfo));
			DefineCurAnim();
			yield return (object)new WaitForSeconds(Time.deltaTime);
			DebugInfos.InConsole("Start Anim Has Changed", "HCtrl");
		}
		if (Core.dealer.MapID > 99)
		{
			Map.AddExtraCollider();
		}
		IsInit = true;
		if (Core.dealer.InStory && StartTyp == HAnims.StartTyp.Normal)
		{
			yield return (object)new WaitForSeconds(0.75f);
			Dictionary<float, ChaCore> dictionary = new Dictionary<float, ChaCore>();
			ChaCore[] array = Object.FindObjectsOfType<ChaCore>();
			foreach (ChaCore chaCore in array)
			{
				if ((Object.op_Implicit((Object)(object)chaCore.Npc) || Object.op_Implicit((Object)(object)chaCore.FixNpc)) && chaCore.npcInfo.map == MapID && !HFlag.lstHeroine.Contains(Object.op_Implicit((Object)(object)chaCore.FixNpc) ? ((Base)chaCore.FixNpc).heroine : ((Base)chaCore.Npc).heroine) && ChaCore.GetRelation(chaCore) > 0)
				{
					dictionary[chaCore.npcInfo.distance] = chaCore;
				}
			}
			foreach (KeyValuePair<float, ChaCore> item2 in dictionary.OrderBy((KeyValuePair<float, ChaCore> g) => g.Key))
			{
				(HMenus.invite as MenuInvite).GirlsAround.Add(item2.Value);
			}
		}
		if (!Core.dealer.InStory || !Core.dealer.ecchiEndByTresspass)
		{
			yield break;
		}
		IsTresspassH = true;
		yield return (object)new WaitForSeconds(0.5f);
		foreach (Heroine item3 in Core.dealer.HeroinesBeforeTrspass)
		{
			if (item3 != HFlag.lstHeroine[0] && (HFlag.lstHeroine.Count <= 1 || item3 != HFlag.lstHeroine[1]) && !Core.dealer.StoryHInf.girlsToMigrateInH.Contains(item3))
			{
				Core.dealer.StoryHInf.girlsToMigrateInH.Add(item3);
			}
		}
		Core.dealer.HeroinesBeforeTrspass.Clear();
		Core.dealer.ecchiEndByTresspass = false;
	}

	public static void SetMainGirlToHCtrl(ChaCore chaCore, int id)
	{
		HCtrl get = Get;
		if (!((Object)(object)get == (Object)null))
		{
			if (id == 0)
			{
				get.MainGirl = chaCore;
			}
			if (id == 1)
			{
				get.SecondGirl = chaCore;
			}
			DebugInfos.InConsole(" Defined Girl ChaCore " + id, "HCtrl");
		}
	}

	public static void SetMainBoyToHCtrl(ChaCore chaCore, int id)
	{
		HCtrl get = Get;
		if (!((Object)(object)get == (Object)null))
		{
			if (id == 0)
			{
				get.MainBoy = chaCore;
			}
			DebugInfos.InConsole(" Defined Boy ChaCore " + id, "HCtrl");
		}
	}

	public static HCtrl Create(GameObject root)
	{
		return root.AddComponent<HCtrl>();
	}
}
