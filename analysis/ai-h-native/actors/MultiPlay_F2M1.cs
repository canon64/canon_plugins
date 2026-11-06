using System.Collections.Generic;
using AIProject.Definitions;
using Manager;
using UnityEngine;
using UnityEx;

public class MultiPlay_F2M1 : ProcBase
{
	private float[] timeMotions = new float[2];

	private bool[] enableMotions = new bool[2];

	private bool[] allowMotions = new bool[2] { true, true };

	private Vector2[] lerpMotions = new Vector2[2]
	{
		Vector2.zero,
		Vector2.zero
	};

	private float[] lerpTimes = new float[2];

	private List<HScene.AnimationListInfo> lstAnimation;

	private int nextPlay;

	private bool oldHit;

	private int finishMotion;

	private bool finishMorS;

	private animParm animPar;

	public MultiPlay_F2M1(DeliveryMember _delivery)
		: base(_delivery)
	{
		animPar.heights = new float[2];
		animPar.m = new float[2];
		CatID = 7;
	}

	public override bool Init(int _modeCtrl)
	{
		base.Init(_modeCtrl);
		return true;
	}

	public void SetAnimationList(List<HScene.AnimationListInfo> _list)
	{
		lstAnimation = _list;
	}

	public override bool SetStartMotion(bool _isIdle, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		AtariEffect.Stop();
		if (_isIdle)
		{
			if (_infoAnimList.nDownPtn != 0)
			{
				setPlay((!ctrlFlag.isFaintness) ? "Idle" : "D_Idle", _isFade: false);
			}
			else
			{
				setPlay("Idle", _isFade: false);
			}
			voice.HouchiTime = 0f;
			ctrlFlag.loopType = -1;
		}
		else
		{
			if (ctrlFlag.feel_f >= 0.7f)
			{
				if (_infoAnimList.nDownPtn != 0)
				{
					setPlay((!ctrlFlag.isFaintness) ? "OLoop" : "D_OLoop", _isFade: false);
				}
				else
				{
					setPlay("OLoop", _isFade: false);
				}
				ctrlFlag.loopType = -1;
			}
			else
			{
				if (_infoAnimList.nDownPtn != 0)
				{
					setPlay((!ctrlFlag.isFaintness) ? "WLoop" : "D_WLoop", _isFade: false);
					if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(0))
					{
						if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1 || ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 3)
						{
							voice.PlaySoundETC((!ctrlFlag.isFaintness) ? "WLoop" : "D_WLoop", 1, chaFemales[0], 0);
						}
						if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1)
						{
							voice.PlaySoundETC((!ctrlFlag.isFaintness) ? "WLoop" : "D_WLoop", 1, chaFemales[1], 1);
						}
					}
					else if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(1))
					{
						if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1 || ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 3)
						{
							voice.PlaySoundETC((!ctrlFlag.isFaintness) ? "WLoop" : "D_WLoop", 2, chaFemales[0], 0);
						}
						if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1)
						{
							voice.PlaySoundETC((!ctrlFlag.isFaintness) ? "WLoop" : "D_WLoop", 2, chaFemales[1], 1);
						}
					}
				}
				else
				{
					setPlay("WLoop", _isFade: false);
					if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(0))
					{
						if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1 || ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 3)
						{
							voice.PlaySoundETC("WLoop", 1, chaFemales[0], 0);
						}
						if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1)
						{
							voice.PlaySoundETC("WLoop", 2, chaFemales[1], 1);
						}
					}
					else if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(1))
					{
						if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1 || ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 3)
						{
							voice.PlaySoundETC("WLoop", 2, chaFemales[0], 0);
						}
						if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1)
						{
							voice.PlaySoundETC("WLoop", 2, chaFemales[1], 1);
						}
					}
				}
				ctrlFlag.loopType = 0;
			}
			if (_infoAnimList.lstSystem.Contains(4))
			{
				ctrlFlag.isPainActionParam = true;
			}
			ctrlFlag.speed = 0f;
			ctrlFlag.motions[0] = 0f;
			ctrlFlag.motions[1] = 0f;
			ctrlFlag.nowSpeedStateFast = false;
			auto.SetSpeed(0f);
		}
		nextPlay = 0;
		oldHit = false;
		isHeight1Parameter = chaFemales[0].IsParameterInAnimator("height1");
		return true;
	}

	public override bool Proc(int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		if (chaFemales[0].objTop == null)
		{
			return false;
		}
		FemaleAi = chaFemales[0].getAnimatorStateInfo(0);
		if (ctrlFlag.initiative == 0)
		{
			switch (_modeCtrl)
			{
			case 0:
				ManualAibu(FemaleAi, _infoAnimList);
				break;
			case 1:
			case 2:
				ManualHoushi(FemaleAi, _modeCtrl, _infoAnimList);
				break;
			case 3:
			case 4:
				ManualSonyu(FemaleAi, _modeCtrl, _infoAnimList);
				break;
			}
		}
		else
		{
			switch (_modeCtrl)
			{
			case 0:
				AutoAibu(FemaleAi, _infoAnimList);
				break;
			case 1:
			case 2:
				AutoHoushi(FemaleAi, _modeCtrl, _infoAnimList);
				break;
			case 3:
			case 4:
				AutoSonyu(FemaleAi, _modeCtrl, _infoAnimList);
				break;
			}
		}
		for (int i = 0; i < 2; i++)
		{
			if (enableMotions[i])
			{
				timeMotions[i] = Mathf.Clamp(timeMotions[i] + Time.deltaTime, 0f, lerpTimes[i]);
				float time = Mathf.Clamp01(timeMotions[i] / lerpTimes[i]);
				time = ctrlFlag.changeMotionCurve.Evaluate(time);
				ctrlFlag.motions[i] = Mathf.Lerp(lerpMotions[i].x, lerpMotions[i].y, time);
				if (time >= 1f)
				{
					enableMotions[i] = false;
				}
			}
		}
		SetFinishCategoryEnable(FemaleAi, _modeCtrl);
		ctrlMeta.Proc(FemaleAi);
		switch (_modeCtrl)
		{
		case 0:
			if (ctrlFlag.click != HSceneFlagCtrl.ClickKind.RecoverFaintness)
			{
				break;
			}
			if (FemaleAi.IsName("D_Idle") || FemaleAi.IsName("D_WLoop") || FemaleAi.IsName("D_SLoop") || FemaleAi.IsName("D_OLoop") || FemaleAi.IsName("D_Orgasm_A"))
			{
				setPlay("Orgasm_A");
				ctrlFlag.isFaintness = false;
				sprite.SetVisibleLeaveItToYou(_visible: true, _judgeLeaveItToYou: true);
				ctrlFlag.numOrgasm = 0;
				sprite.SetAnimationMenu();
				ctrlFlag.isGaugeHit = false;
				sprite.SetMotionListDraw(_active: false);
				ctrlFlag.nowOrgasm = false;
				Hitem.SetUse(6, use: false);
				if (FemaleAi.IsName("D_Orgasm_A") && voice.playAnimation != null)
				{
					voice.playAnimation.SetAllFlags(_play: true);
				}
			}
			else
			{
				ctrlFlag.click = HSceneFlagCtrl.ClickKind.None;
				Hitem.SetUse(6, use: false);
			}
			break;
		case 1:
		case 2:
			if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.RecoverFaintness)
			{
				if (FemaleAi.IsName("D_Idle") || FemaleAi.IsName("D_WLoop") || FemaleAi.IsName("D_SLoop") || FemaleAi.IsName("D_OLoop") || FemaleAi.IsName("D_Orgasm_OUT_A"))
				{
					ctrlFlag.selectAnimationListInfo = RecoverFaintnessAi();
					ctrlFlag.isFaintness = false;
					sprite.SetVisibleLeaveItToYou(_visible: true, _judgeLeaveItToYou: true);
					ctrlFlag.numOrgasm = 0;
					ctrlMeta.Clear();
					sprite.SetAnimationMenu();
					ctrlFlag.isGaugeHit = false;
					ctrlFlag.isGaugeHit_M = false;
					sprite.SetMotionListDraw(_active: false);
					ctrlFlag.nowOrgasm = false;
					Hitem.SetUse(6, use: false);
				}
				else
				{
					ctrlFlag.click = HSceneFlagCtrl.ClickKind.None;
					Hitem.SetUse(6, use: false);
				}
			}
			break;
		case 3:
		case 4:
			if (ctrlFlag.click != HSceneFlagCtrl.ClickKind.RecoverFaintness)
			{
				break;
			}
			if (FemaleAi.IsName("D_Idle") || FemaleAi.IsName("D_WLoop") || FemaleAi.IsName("D_SLoop") || FemaleAi.IsName("D_OLoop") || FemaleAi.IsName("D_Orgasm_IN_A") || FemaleAi.IsName("D_OrgasmM_OUT_A"))
			{
				if (_modeCtrl == 3)
				{
					setPlay("Orgasm_IN_A");
				}
				else
				{
					setPlay("OrgasmM_OUT_A");
				}
				ctrlMeta.Clear();
				ctrlFlag.isFaintness = false;
				ctrlFlag.numOrgasm = 0;
				sprite.SetFinishSelect(7, _modeCtrl);
				sprite.SetVisibleLeaveItToYou(_visible: true, _judgeLeaveItToYou: true);
				ctrlMeta.SetParameterFromState(0);
				sprite.SetAnimationMenu();
				ctrlFlag.isGaugeHit = false;
				sprite.SetMotionListDraw(_active: false);
				ctrlFlag.nowOrgasm = false;
				Hitem.SetUse(6, use: false);
				if ((FemaleAi.IsName("D_Orgasm_IN_A") || FemaleAi.IsName("D_OrgasmM_OUT_A")) && voice.playAnimation != null)
				{
					voice.playAnimation.SetAllFlags(_play: true);
				}
			}
			else
			{
				ctrlFlag.click = HSceneFlagCtrl.ClickKind.None;
				Hitem.SetUse(6, use: false);
			}
			break;
		}
		setAnimationParamater();
		return true;
	}

	private bool ManualAibu(AnimatorStateInfo _ai, HScene.AnimationListInfo _infoAnimList)
	{
		float num = UnityEngine.Input.GetAxis("Mouse ScrollWheel") * (float)((!sprite.IsSpriteOver()) ? 1 : 0);
		num = ((num < 0f) ? (0f - ctrlFlag.wheelActionCount) : ((!(num > 0f)) ? 0f : ctrlFlag.wheelActionCount));
		if (_ai.IsName("Idle"))
		{
			StartProcTrigger(num);
			StartAibuProc(_isReStart: false);
			voice.HouchiTime += Time.unscaledDeltaTime;
		}
		else if (_ai.IsName("WLoop"))
		{
			LoopAibuProc(0, 0, num, _infoAnimList);
		}
		else if (_ai.IsName("SLoop"))
		{
			LoopAibuProc(1, 0, num, _infoAnimList);
		}
		else if (_ai.IsName("OLoop"))
		{
			OLoopAibuProc(0, num, _infoAnimList);
		}
		else if (_ai.IsName("Orgasm"))
		{
			OrgasmAibuProc(0, _ai.normalizedTime);
		}
		else if (_ai.IsName("Orgasm_A"))
		{
			StartProcTrigger(num);
			StartAibuProc(_isReStart: true);
		}
		else if (_ai.IsName("D_Idle"))
		{
			FaintnessStartProcTrigger(num);
			FaintnessStartAibuProc(_start: true);
			voice.HouchiTime += Time.unscaledDeltaTime;
		}
		else if (_ai.IsName("D_WLoop"))
		{
			LoopAibuProc(0, 1, num, _infoAnimList);
		}
		else if (_ai.IsName("D_SLoop"))
		{
			LoopAibuProc(1, 1, num, _infoAnimList);
		}
		else if (_ai.IsName("D_OLoop"))
		{
			OLoopAibuProc(1, num, _infoAnimList);
		}
		else if (_ai.IsName("D_Orgasm"))
		{
			OrgasmAibuProc(1, _ai.normalizedTime);
		}
		else if (_ai.IsName("D_Orgasm_A"))
		{
			FaintnessStartProcTrigger(num);
			FaintnessStartAibuProc(_start: false);
		}
		return true;
	}

	private bool ManualHoushi(AnimatorStateInfo _ai, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		float num = UnityEngine.Input.GetAxis("Mouse ScrollWheel") * (float)((!sprite.IsSpriteOver()) ? 1 : 0);
		num = ((num < 0f) ? (0f - ctrlFlag.wheelActionCount) : ((!(num > 0f)) ? 0f : ctrlFlag.wheelActionCount));
		if (_ai.IsName("Idle"))
		{
			StartProcTrigger(num);
			StartHoushiProc(0, _restart: false);
			voice.HouchiTime += Time.unscaledDeltaTime;
		}
		else if (_ai.IsName("WLoop"))
		{
			LoopHoushiProc(0, 0, num, _infoAnimList);
		}
		else if (_ai.IsName("SLoop"))
		{
			LoopHoushiProc(1, 0, num, _infoAnimList);
		}
		else if (_ai.IsName("OLoop"))
		{
			OLoopHoushiProc(0, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("Orgasm_OUT"))
		{
			SetNextFinishAnimation(_ai.normalizedTime, "Orgasm_OUT_A");
		}
		else if (_ai.IsName("Orgasm_IN"))
		{
			SetAfterInsideFinishAnimation(0, _ai.normalizedTime);
		}
		else if (_ai.IsName("Drink_IN"))
		{
			SetNextFinishAnimation(_ai.normalizedTime, "Drink", _isSpriteSet: false, _isFade: false);
		}
		else if (_ai.IsName("Drink"))
		{
			SetNextFinishAnimation(_ai.normalizedTime, "Drink_A");
		}
		else if (_ai.IsName("Vomit_IN"))
		{
			SetNextFinishAnimation(_ai.normalizedTime, "Vomit", _isSpriteSet: false, _isFade: false);
		}
		else if (_ai.IsName("Vomit"))
		{
			SetNextFinishAnimation(_ai.normalizedTime, "Vomit_A");
		}
		else if (_ai.IsName("Orgasm_OUT_A"))
		{
			StartProcTrigger(num);
			StartHoushiProc(0, _restart: true);
		}
		else if (_ai.IsName("Drink_A"))
		{
			StartProcTrigger(num);
			StartHoushiProc(0, _restart: true);
		}
		else if (_ai.IsName("Vomit_A"))
		{
			StartProcTrigger(num);
			StartHoushiProc(0, _restart: true);
		}
		else if (_ai.IsName("D_Idle"))
		{
			StartProcTrigger(num);
			StartHoushiProc(1, _restart: false);
			voice.HouchiTime += Time.unscaledDeltaTime;
		}
		else if (_ai.IsName("D_WLoop"))
		{
			LoopHoushiProc(0, 1, num, _infoAnimList);
		}
		else if (_ai.IsName("D_SLoop"))
		{
			LoopHoushiProc(1, 1, num, _infoAnimList);
		}
		else if (_ai.IsName("D_OLoop"))
		{
			OLoopHoushiProc(1, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("D_Orgasm_OUT"))
		{
			SetNextFinishAnimation(_ai.normalizedTime, "D_Orgasm_OUT_A");
		}
		else if (_ai.IsName("D_Orgasm_OUT_A"))
		{
			StartProcTrigger(num);
			StartHoushiProc(1, _restart: true);
		}
		return true;
	}

	private bool ManualSonyu(AnimatorStateInfo _ai, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		float num = UnityEngine.Input.GetAxis("Mouse ScrollWheel") * (float)((!sprite.IsSpriteOver()) ? 1 : 0);
		num = ((num < 0f) ? (0f - ctrlFlag.wheelActionCount) : ((!(num > 0f)) ? 0f : ctrlFlag.wheelActionCount));
		if (_ai.IsName("Idle"))
		{
			StartProcTrigger(num);
			StartSonyuProc(_restart: false, 0);
			voice.HouchiTime += Time.unscaledDeltaTime;
		}
		else if (_ai.IsName("Insert"))
		{
			InsertProc(_ai.normalizedTime, 0);
		}
		else if (_ai.IsName("WLoop"))
		{
			LoopSonyuProc(0, 0, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("SLoop"))
		{
			LoopSonyuProc(1, 0, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("OLoop"))
		{
			OLoopSonyuProc(0, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("OrgasmF_IN"))
		{
			AfterTheNextWaitingAnimation(_ai.normalizedTime, 1f, 0, _modeCtrl, 0);
		}
		else if (_ai.IsName("OrgasmM_IN"))
		{
			FinishNextAnimationByMorS(_ai.normalizedTime, 1f, 0, _modeCtrl, _finishMorS: false);
		}
		else if (_ai.IsName("OrgasmS_IN"))
		{
			FinishNextAnimationByMorS(_ai.normalizedTime, 1f, 0, _modeCtrl, _finishMorS: true);
		}
		else if (_ai.IsName("Orgasm_IN_A"))
		{
			AfterTheInsideWaitingProc(0, num, _modeCtrl);
		}
		else if (_ai.IsName("Pull"))
		{
			PullProc(_ai.normalizedTime, 0);
		}
		else if (_ai.IsName("Drop"))
		{
			AfterTheNextWaitingAnimation(_ai.normalizedTime, 4f, 0, _modeCtrl, 2);
		}
		else if (_ai.IsName("OrgasmM_OUT"))
		{
			AfterTheNextWaitingAnimation(_ai.normalizedTime, 1f, 0, _modeCtrl, 2);
		}
		else if (_ai.IsName("OrgasmM_OUT_A"))
		{
			StartProcTrigger(num);
			StartSonyuProc(_restart: true, 0);
		}
		else if (_ai.IsName("D_Idle"))
		{
			StartProcTrigger(num);
			StartSonyuProc(_restart: false, 1);
			voice.HouchiTime += Time.unscaledDeltaTime;
		}
		else if (_ai.IsName("D_Insert"))
		{
			InsertProc(_ai.normalizedTime, 1);
		}
		else if (_ai.IsName("D_WLoop"))
		{
			LoopSonyuProc(0, 1, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("D_SLoop"))
		{
			LoopSonyuProc(1, 1, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("D_OLoop"))
		{
			OLoopSonyuProc(1, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("D_OrgasmF_IN"))
		{
			AfterTheNextWaitingAnimation(_ai.normalizedTime, 1f, 1, _modeCtrl, 0);
		}
		else if (_ai.IsName("D_OrgasmM_IN"))
		{
			FinishNextAnimationByMorS(_ai.normalizedTime, 1f, 1, _modeCtrl, _finishMorS: false);
		}
		else if (_ai.IsName("D_OrgasmS_IN"))
		{
			FinishNextAnimationByMorS(_ai.normalizedTime, 1f, 1, _modeCtrl, _finishMorS: true);
		}
		else if (_ai.IsName("D_Orgasm_IN_A"))
		{
			AfterTheInsideWaitingProc(1, num, _modeCtrl);
		}
		else if (_ai.IsName("D_Pull"))
		{
			PullProc(_ai.normalizedTime, 1);
		}
		else if (_ai.IsName("D_Drop"))
		{
			AfterTheNextWaitingAnimation(_ai.normalizedTime, 4f, 1, _modeCtrl, 2);
		}
		else if (_ai.IsName("D_OrgasmM_OUT"))
		{
			AfterTheNextWaitingAnimation(_ai.normalizedTime, 1f, 1, _modeCtrl, 2);
		}
		else if (_ai.IsName("D_OrgasmM_OUT_A"))
		{
			StartProcTrigger(num);
			StartSonyuProc(_restart: true, 1);
		}
		return true;
	}

	private bool AutoAibu(AnimatorStateInfo _ai, HScene.AnimationListInfo _infoAnimList)
	{
		float num = UnityEngine.Input.GetAxis("Mouse ScrollWheel") * (float)((!sprite.IsSpriteOver()) ? 1 : 0);
		num = ((num < 0f) ? (0f - ctrlFlag.wheelActionCount) : ((!(num > 0f)) ? 0f : ctrlFlag.wheelActionCount));
		if (_ai.IsName("Idle"))
		{
			AutoStartProcTrigger(_start: false);
			AutoStartAibuProc(_isReStart: false);
		}
		else if (_ai.IsName("WLoop"))
		{
			AutoLoopAibuProc(0, 0, num, _infoAnimList);
		}
		else if (_ai.IsName("SLoop"))
		{
			AutoLoopAibuProc(1, 0, num, _infoAnimList);
		}
		else if (_ai.IsName("OLoop"))
		{
			AutoOLoopAibuProc(0, num, _infoAnimList);
		}
		else if (_ai.IsName("Orgasm"))
		{
			OrgasmAibuProc(0, _ai.normalizedTime);
		}
		else if (_ai.IsName("Orgasm_A"))
		{
			AutoStartProcTrigger(_start: true);
			AutoStartAibuProc(_isReStart: true);
		}
		else if (_ai.IsName("D_Idle"))
		{
			FaintnessStartProcTrigger(num);
			FaintnessStartAibuProc(_start: true);
		}
		else if (_ai.IsName("D_WLoop"))
		{
			LoopAibuProc(0, 1, num, _infoAnimList);
		}
		else if (_ai.IsName("D_SLoop"))
		{
			LoopAibuProc(1, 1, num, _infoAnimList);
		}
		else if (_ai.IsName("D_OLoop"))
		{
			OLoopAibuProc(1, num, _infoAnimList);
		}
		else if (_ai.IsName("D_Orgasm"))
		{
			OrgasmAibuProc(1, _ai.normalizedTime);
		}
		else if (_ai.IsName("D_Orgasm_A"))
		{
			FaintnessStartProcTrigger(num);
			FaintnessStartAibuProc(_start: false);
		}
		return true;
	}

	private bool AutoHoushi(AnimatorStateInfo _ai, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		float num = UnityEngine.Input.GetAxis("Mouse ScrollWheel") * (float)((!sprite.IsSpriteOver()) ? 1 : 0);
		num = ((num < 0f) ? (0f - ctrlFlag.wheelActionCount) : ((!(num > 0f)) ? 0f : ctrlFlag.wheelActionCount));
		if (_ai.IsName("Idle"))
		{
			AutoStartProcTrigger(_start: false);
			AutoStartHoushiProc(0, _restart: false);
		}
		else if (_ai.IsName("WLoop"))
		{
			AutoLoopHoushiProc(0, 0, num, _infoAnimList);
		}
		else if (_ai.IsName("SLoop"))
		{
			AutoLoopHoushiProc(1, 0, num, _infoAnimList);
		}
		else if (_ai.IsName("OLoop"))
		{
			AutoOLoopHoushiProc(0, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("Orgasm_OUT"))
		{
			SetNextFinishAnimation(_ai.normalizedTime, "Orgasm_OUT_A");
		}
		else if (_ai.IsName("Orgasm_IN"))
		{
			SetAfterInsideFinishAnimation(0, _ai.normalizedTime);
		}
		else if (_ai.IsName("Drink_IN"))
		{
			SetNextFinishAnimation(_ai.normalizedTime, "Drink", _isSpriteSet: false, _isFade: false);
		}
		else if (_ai.IsName("Drink"))
		{
			SetNextFinishAnimation(_ai.normalizedTime, "Drink_A");
		}
		else if (_ai.IsName("Vomit_IN"))
		{
			SetNextFinishAnimation(_ai.normalizedTime, "Vomit", _isSpriteSet: false, _isFade: false);
		}
		else if (_ai.IsName("Vomit"))
		{
			SetNextFinishAnimation(_ai.normalizedTime, "Vomit_A");
		}
		else if (_ai.IsName("Orgasm_OUT_A"))
		{
			AutoStartProcTrigger(_start: true);
			AutoStartHoushiProc(0, _restart: true);
		}
		else if (_ai.IsName("Drink_A"))
		{
			AutoStartProcTrigger(_start: true);
			AutoStartHoushiProc(0, _restart: true);
		}
		else if (_ai.IsName("Vomit_A"))
		{
			AutoStartProcTrigger(_start: true);
			AutoStartHoushiProc(0, _restart: true);
		}
		else if (_ai.IsName("D_Idle"))
		{
			StartProcTrigger(num);
			StartHoushiProc(1, _restart: false);
		}
		else if (_ai.IsName("D_WLoop"))
		{
			LoopHoushiProc(0, 1, num, _infoAnimList);
		}
		else if (_ai.IsName("D_SLoop"))
		{
			LoopHoushiProc(1, 1, num, _infoAnimList);
		}
		else if (_ai.IsName("D_OLoop"))
		{
			OLoopHoushiProc(1, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("D_Orgasm_OUT"))
		{
			SetNextFinishAnimation(_ai.normalizedTime, "D_Orgasm_OUT_A");
		}
		else if (_ai.IsName("D_Orgasm_OUT_A"))
		{
			StartProcTrigger(num);
			StartHoushiProc(1, _restart: true);
		}
		return true;
	}

	private bool AutoSonyu(AnimatorStateInfo _ai, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		float num = UnityEngine.Input.GetAxis("Mouse ScrollWheel") * (float)((!sprite.IsSpriteOver()) ? 1 : 0);
		num = ((num < 0f) ? (0f - ctrlFlag.wheelActionCount) : ((!(num > 0f)) ? 0f : ctrlFlag.wheelActionCount));
		if (_ai.IsName("Idle"))
		{
			AutoStartProcTrigger(_start: false);
			AutoStartSonyuProc(_restart: false, 0, _modeCtrl);
		}
		else if (_ai.IsName("Insert"))
		{
			InsertProc(_ai.normalizedTime, 0);
		}
		else if (_ai.IsName("WLoop"))
		{
			AutoLoopSonyuProc(0, 0, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("SLoop"))
		{
			AutoLoopSonyuProc(1, 0, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("OLoop"))
		{
			AutoOLoopProc(0, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("OrgasmF_IN"))
		{
			AfterTheNextWaitingAnimation(_ai.normalizedTime, 1f, 0, _modeCtrl, 0);
		}
		else if (_ai.IsName("OrgasmM_IN"))
		{
			FinishNextAnimationByMorS(_ai.normalizedTime, 1f, 0, _modeCtrl, _finishMorS: false);
		}
		else if (_ai.IsName("OrgasmS_IN"))
		{
			FinishNextAnimationByMorS(_ai.normalizedTime, 1f, 0, _modeCtrl, _finishMorS: true);
		}
		else if (_ai.IsName("Orgasm_IN_A"))
		{
			AutoAfterTheInsideWaitingProc(0, num);
		}
		else if (_ai.IsName("Pull"))
		{
			PullProc(_ai.normalizedTime, 0);
		}
		else if (_ai.IsName("Drop"))
		{
			AfterTheNextWaitingAnimation(_ai.normalizedTime, 4f, 0, _modeCtrl, 2);
		}
		else if (_ai.IsName("OrgasmM_OUT"))
		{
			AfterTheNextWaitingAnimation(_ai.normalizedTime, 1f, 0, _modeCtrl, 2);
		}
		else if (_ai.IsName("OrgasmM_OUT_A"))
		{
			AutoStartProcTrigger(_start: true);
			AutoStartSonyuProc(_restart: true, 0, _modeCtrl);
		}
		else if (_ai.IsName("D_Idle"))
		{
			StartProcTrigger(num);
			StartSonyuProc(_restart: false, 1);
		}
		else if (_ai.IsName("D_Insert"))
		{
			InsertProc(_ai.normalizedTime, 1);
		}
		else if (_ai.IsName("D_WLoop"))
		{
			LoopSonyuProc(0, 1, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("D_SLoop"))
		{
			LoopSonyuProc(1, 1, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("D_OLoop"))
		{
			OLoopSonyuProc(1, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("D_OrgasmF_IN"))
		{
			AfterTheNextWaitingAnimation(_ai.normalizedTime, 1f, 1, _modeCtrl, 0);
		}
		else if (_ai.IsName("D_OrgasmM_IN"))
		{
			FinishNextAnimationByMorS(_ai.normalizedTime, 1f, 1, _modeCtrl, _finishMorS: false);
		}
		else if (_ai.IsName("D_OrgasmS_IN"))
		{
			FinishNextAnimationByMorS(_ai.normalizedTime, 1f, 1, _modeCtrl, _finishMorS: true);
		}
		else if (_ai.IsName("D_Orgasm_IN_A"))
		{
			AfterTheInsideWaitingProc(1, num, _modeCtrl);
		}
		else if (_ai.IsName("D_Pull"))
		{
			PullProc(_ai.normalizedTime, 1);
		}
		else if (_ai.IsName("D_Drop"))
		{
			AfterTheNextWaitingAnimation(_ai.normalizedTime, 4f, 1, _modeCtrl, 2);
		}
		else if (_ai.IsName("D_OrgasmM_OUT"))
		{
			AfterTheNextWaitingAnimation(_ai.normalizedTime, 1f, 1, _modeCtrl, 2);
		}
		else if (_ai.IsName("D_OrgasmM_OUT_A"))
		{
			StartProcTrigger(num);
			StartSonyuProc(_restart: true, 1);
		}
		else if (_ai.IsName("Vomit"))
		{
			AfterTheNextWaitingAnimation(_ai.normalizedTime, 3f, 0, _modeCtrl, (!finishMorS) ? 1 : 0);
		}
		else if (_ai.IsName("D_Vomit"))
		{
			AfterTheNextWaitingAnimation(_ai.normalizedTime, 3f, 1, _modeCtrl, (!finishMorS) ? 1 : 0);
		}
		return true;
	}

	public override void setAnimationParamater()
	{
		animPar.breast = chaFemales[0].GetShapeBodyValue(1);
		animPar.speed = ((ctrlFlag.loopType == 1) ? ctrlFlag.speed : (ctrlFlag.speed + 1f));
		animPar.m[0] = ctrlFlag.motions[0];
		animPar.m[1] = ctrlFlag.motions[1];
		for (int i = 0; i < chaFemales.Length; i++)
		{
			if (chaFemales[i].visibleAll && !(chaFemales[i].objBodyBone == null))
			{
				animPar.heights[i] = chaFemales[i].GetShapeBodyValue(0);
			}
		}
		for (int j = 0; j < chaFemales.Length; j++)
		{
			if (chaFemales[j].visibleAll && !(chaFemales[j].objTop == null))
			{
				chaFemales[j].setAnimatorParamFloat("height", animPar.heights[j]);
				chaFemales[j].setAnimatorParamFloat("speed", animPar.speed);
				chaFemales[j].setAnimatorParamFloat("motion", animPar.m[0]);
				chaFemales[j].setAnimatorParamFloat("motion1", animPar.m[1]);
				chaFemales[j].setAnimatorParamFloat("breast", animPar.breast);
				if (isHeight1Parameter)
				{
					chaFemales[j].setAnimatorParamFloat("height1", animPar.heights[j ^ 1]);
				}
			}
		}
		if (chaMales[0].objBodyBone != null)
		{
			chaMales[0].setAnimatorParamFloat("height", animPar.heights[0]);
			chaMales[0].setAnimatorParamFloat("speed", animPar.speed);
			chaMales[0].setAnimatorParamFloat("motion", animPar.m[0]);
			chaMales[0].setAnimatorParamFloat("breast", animPar.breast);
			if (isHeight1Parameter)
			{
				chaMales[0].setAnimatorParamFloat("height1", animPar.heights[1]);
			}
		}
		if (item.GetItem() != null)
		{
			item.setAnimatorParamFloat("height", animPar.heights[0]);
			item.setAnimatorParamFloat("speed", animPar.speed);
			item.setAnimatorParamFloat("motion", animPar.m[0]);
			if (isHeight1Parameter)
			{
				item.setAnimatorParamFloat("height1", animPar.heights[1]);
			}
		}
	}

	private void setPlay(string _playAnimation, bool _isFade = true)
	{
		chaFemales[0].setPlay(_playAnimation, 0);
		if (chaFemales[1].visibleAll && chaFemales[1].objTop != null)
		{
			chaFemales[1].setPlay(_playAnimation, 0);
		}
		if (chaMales[0].objTop != null)
		{
			chaMales[0].setPlay(_playAnimation, 0);
		}
		if (item != null)
		{
			item.setPlay(_playAnimation);
		}
		for (int i = 0; i < lstMotionIK.Count; i++)
		{
			lstMotionIK[i].Item3.Calc(_playAnimation);
		}
		if (item != null)
		{
			item.setPlay(_playAnimation);
		}
		if (_isFade)
		{
			fade.FadeStart(1f);
		}
	}

	private bool StartProcTrigger(float _wheel)
	{
		if (_wheel == 0f || nextPlay != 0)
		{
			return false;
		}
		if (ctrlFlag.voice.playStart > 4)
		{
			return false;
		}
		nextPlay = 1;
		return true;
	}

	private bool StartAibuProc(bool _isReStart)
	{
		if (nextPlay == 0)
		{
			return false;
		}
		if (nextPlay == 1)
		{
			nextPlay = 2;
			if (!_isReStart)
			{
				ctrlFlag.voice.playStart = 2;
			}
			else
			{
				ctrlFlag.voice.playStart = 3;
			}
			return false;
		}
		if (nextPlay == 2)
		{
			for (int i = 0; i < 2; i++)
			{
				if (voice.nowVoices[i].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[i].state == HVoiceCtrl.VoiceKind.startVoice)
				{
					return false;
				}
			}
			if (ctrlFlag.voice.playStart > 4)
			{
				return false;
			}
		}
		nextPlay = 0;
		setPlay("WLoop");
		if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(0))
		{
			if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1 || ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 3)
			{
				voice.PlaySoundETC("WLoop", 1, chaFemales[0], 0);
			}
			if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1)
			{
				voice.PlaySoundETC("WLoop", 2, chaFemales[1], 1);
			}
		}
		else if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(1))
		{
			if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1 || ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 3)
			{
				voice.PlaySoundETC("WLoop", 2, chaFemales[0], 0);
			}
			if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1)
			{
				voice.PlaySoundETC("WLoop", 2, chaFemales[1], 1);
			}
		}
		ctrlFlag.speed = 0f;
		ctrlFlag.loopType = 0;
		ctrlFlag.motions[0] = 0f;
		ctrlFlag.motions[1] = 0f;
		ctrlFlag.nowSpeedStateFast = false;
		for (int j = 0; j < 2; j++)
		{
			timeMotions[j] = 0f;
			timeChangeMotions[j] = Random.Range(ctrlFlag.changeAutoMotionTimeMin, ctrlFlag.changeAutoMotionTimeMax);
			timeChangeMotionDeltaTimes[j] = 0f;
		}
		ctrlFlag.isNotCtrl = false;
		oldHit = false;
		feelHit.InitTime();
		if (_isReStart)
		{
			voice.AfterFinish();
		}
		return true;
	}

	private bool FaintnessStartProcTrigger(float _wheel)
	{
		if (_wheel == 0f || nextPlay != 0)
		{
			return false;
		}
		for (int i = 0; i < 2; i++)
		{
			if (voice.nowVoices[i].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[i].state == HVoiceCtrl.VoiceKind.startVoice)
			{
				return false;
			}
		}
		if (ctrlFlag.voice.playStart > 4)
		{
			return false;
		}
		nextPlay = 1;
		return true;
	}

	private bool FaintnessStartAibuProc(bool _start)
	{
		if (nextPlay == 0)
		{
			return false;
		}
		if (nextPlay == 1)
		{
			if (_start)
			{
				nextPlay = 3;
			}
			else
			{
				nextPlay = 2;
				if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item2 == 0)
				{
					ctrlFlag.voice.playStart = 3;
				}
			}
			return false;
		}
		if (nextPlay == 2)
		{
			for (int i = 0; i < 2; i++)
			{
				if (voice.nowVoices[i].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[i].state == HVoiceCtrl.VoiceKind.startVoice)
				{
					return false;
				}
			}
			if (ctrlFlag.voice.playStart > 4)
			{
				return false;
			}
		}
		nextPlay = 0;
		setPlay("D_WLoop");
		if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(0))
		{
			if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1 || ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 3)
			{
				voice.PlaySoundETC("D_WLoop", 1, chaFemales[0], 0);
			}
			if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1)
			{
				voice.PlaySoundETC("D_WLoop", 2, chaFemales[1], 1);
			}
		}
		else if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(1))
		{
			if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1 || ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 3)
			{
				voice.PlaySoundETC("D_WLoop", 2, chaFemales[0], 0);
			}
			if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1)
			{
				voice.PlaySoundETC("D_WLoop", 2, chaFemales[1], 1);
			}
		}
		ctrlFlag.speed = 0f;
		ctrlFlag.loopType = 0;
		ctrlFlag.motions[0] = 0f;
		ctrlFlag.motions[1] = 0f;
		ctrlFlag.nowSpeedStateFast = false;
		ctrlFlag.isNotCtrl = false;
		oldHit = false;
		feelHit.InitTime();
		voice.AfterFinish();
		return true;
	}

	private bool LoopAibuProc(int _loop, int _state, float _wheel, HScene.AnimationListInfo _infoAnimList)
	{
		float num = 0f;
		if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishBefore)
		{
			setPlay((_state != 0) ? "D_OLoop" : "OLoop");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.nowSpeedStateFast = false;
			ctrlFlag.feel_f = 0.7f;
			oldHit = false;
			ctrlFlag.isGaugeHit = false;
		}
		else
		{
			ctrlFlag.speed += _wheel;
			if (_loop == 0)
			{
				ctrlFlag.nowSpeedStateFast = ctrlFlag.speed >= 0.5f;
			}
			else
			{
				ctrlFlag.nowSpeedStateFast = ctrlFlag.speed >= 1.5f;
			}
			if (_state == 0)
			{
				for (int i = 0; i < 2; i++)
				{
					if (!chaFemales[i].visibleAll || chaFemales[i].objBodyBone == null)
					{
						continue;
					}
					timeChangeMotionDeltaTimes[i] += Time.deltaTime;
					if (!(timeChangeMotions[i] <= timeChangeMotionDeltaTimes[i]) || enableMotions[i])
					{
						continue;
					}
					timeChangeMotions[i] = Random.Range(ctrlFlag.changeAutoMotionTimeMin, ctrlFlag.changeAutoMotionTimeMax);
					timeChangeMotionDeltaTimes[i] = 0f;
					enableMotions[i] = true;
					timeMotions[i] = 0f;
					float num2 = 0f;
					if (allowMotions[i])
					{
						num2 = 1f - ctrlFlag.motions[i];
						num2 = ((!(num2 <= ctrlFlag.changeMotionMinRate)) ? (ctrlFlag.motions[i] + Random.Range(ctrlFlag.changeMotionMinRate, num2)) : 1f);
						if (num2 >= 1f)
						{
							allowMotions[i] = false;
						}
					}
					else
					{
						num2 = ctrlFlag.motions[i];
						num2 = ((!(num2 <= ctrlFlag.changeMotionMinRate)) ? (ctrlFlag.motions[i] - Random.Range(ctrlFlag.changeMotionMinRate, num2)) : 0f);
						if (num2 <= 0f)
						{
							allowMotions[i] = true;
						}
					}
					ref Vector2 reference = ref lerpMotions[i];
					reference = new Vector2(ctrlFlag.motions[i], num2);
					lerpTimes[i] = Random.Range(ctrlFlag.changeMotionTimeMin, ctrlFlag.changeMotionTimeMax);
				}
			}
			if (_loop == 0)
			{
				if (ctrlFlag.speed > 1f && ctrlFlag.loopType == 0)
				{
					setPlay((_state != 0) ? "D_SLoop" : "SLoop");
					ctrlFlag.nowSpeedStateFast = false;
					feelHit.InitTime();
					ctrlFlag.loopType = 1;
				}
				ctrlFlag.speed = Mathf.Clamp(ctrlFlag.speed, 0f, 2f);
			}
			else
			{
				if (ctrlFlag.speed < 1f && ctrlFlag.loopType == 1)
				{
					setPlay((_state != 0) ? "D_WLoop" : "WLoop");
					ctrlFlag.nowSpeedStateFast = true;
					feelHit.InitTime();
					ctrlFlag.loopType = 0;
				}
				ctrlFlag.speed = Mathf.Clamp(ctrlFlag.speed, 0f, 2f);
			}
			ctrlFlag.isGaugeHit = feelHit.isHit(_infoAnimList.nFeelHit, _loop, (_loop != 0) ? (ctrlFlag.speed - 1f) : ctrlFlag.speed);
			isAtariHit.Value = ctrlFlag.isGaugeHit;
			if (ctrlFlag.isGaugeHit)
			{
				feelHit.ChangeHit(_infoAnimList.nFeelHit, _loop);
				num = Time.deltaTime * ctrlFlag.speedGuageRate;
				num *= (float)((!ctrlFlag.stopFeelFemal) ? 1 : 0);
				if (!ProcBase.hSceneManager.bMerchant)
				{
					if (ProcBase.hSceneManager.HSkil.ContainsValue(0) && ctrlFlag.nowAnimationInfo.lstSystem.Contains(2))
					{
						num *= ctrlFlag.SkilChangeSpeed(0);
					}
					else if (ProcBase.hSceneManager.HSkil.ContainsValue(15) && ctrlFlag.nowAnimationInfo.lstSystem.Contains(3))
					{
						num *= ctrlFlag.SkilChangeSpeed(15);
					}
					if (ProcBase.hSceneManager.HSkil.ContainsValue(3))
					{
						num *= ctrlFlag.SkilChangeSpeed(3);
					}
				}
				ctrlFlag.feel_f += num;
				ctrlFlag.feel_f = Mathf.Clamp01(ctrlFlag.feel_f);
			}
			if (ctrlFlag.isGaugeHit != oldHit && ctrlFlag.isGaugeHit)
			{
				if (randVoicePlays[0].Get() == 0)
				{
					ctrlFlag.voice.playVoices[0] = true;
				}
				else if (randVoicePlays[1].Get() == 0)
				{
					ctrlFlag.voice.playVoices[1] = true;
				}
				if (!ctrlFlag.nowAnimationInfo.lstSystem.Contains(0))
				{
					if (_infoAnimList.nShortBreahtPlay == 1 || _infoAnimList.nShortBreahtPlay == 3)
					{
						ctrlFlag.voice.playShorts[0] = 0;
					}
					if (_infoAnimList.nShortBreahtPlay == 1 || _infoAnimList.nShortBreahtPlay == 2)
					{
						ctrlFlag.voice.playShorts[1] = 0;
					}
				}
				ctrlFlag.voice.dialog = false;
			}
			oldHit = ctrlFlag.isGaugeHit;
			if (ctrlFlag.feel_f >= 0.7f)
			{
				setPlay((_state != 0) ? "D_OLoop" : "OLoop");
				ctrlFlag.speed = 0f;
				ctrlFlag.loopType = -1;
				ctrlFlag.nowSpeedStateFast = false;
				oldHit = false;
				feelHit.InitTime();
			}
		}
		return true;
	}

	private bool OLoopAibuProc(int _state, float _wheel, HScene.AnimationListInfo _infoAnimList)
	{
		float num = 0f;
		ctrlFlag.speed = Mathf.Clamp01(ctrlFlag.speed + _wheel);
		ctrlFlag.nowSpeedStateFast = ctrlFlag.speed >= 0.5f;
		feelHit.ChangeHit(_infoAnimList.nFeelHit, 2);
		ctrlFlag.isGaugeHit = feelHit.isHit(_infoAnimList.nFeelHit, 2, ctrlFlag.speed);
		isAtariHit.Value = ctrlFlag.isGaugeHit;
		if (ctrlFlag.isGaugeHit)
		{
			num = Time.deltaTime * ctrlFlag.speedGuageRate;
			num *= (float)((!ctrlFlag.stopFeelFemal) ? 1 : 0);
			if (!ProcBase.hSceneManager.bMerchant)
			{
				if (ProcBase.hSceneManager.HSkil.ContainsValue(0) && ctrlFlag.nowAnimationInfo.lstSystem.Contains(2))
				{
					num *= ctrlFlag.SkilChangeSpeed(0);
				}
				else if (ProcBase.hSceneManager.HSkil.ContainsValue(15) && ctrlFlag.nowAnimationInfo.lstSystem.Contains(3))
				{
					num *= ctrlFlag.SkilChangeSpeed(15);
				}
				if (ProcBase.hSceneManager.HSkil.ContainsValue(3))
				{
					num *= ctrlFlag.SkilChangeSpeed(3);
				}
			}
			ctrlFlag.feel_f += num;
			ctrlFlag.feel_f = Mathf.Clamp01(ctrlFlag.feel_f);
		}
		if (ctrlFlag.isGaugeHit != oldHit && ctrlFlag.isGaugeHit)
		{
			if (randVoicePlays[0].Get() == 0)
			{
				ctrlFlag.voice.playVoices[0] = true;
			}
			else if (randVoicePlays[1].Get() == 0)
			{
				ctrlFlag.voice.playVoices[1] = true;
			}
			if (!ctrlFlag.nowAnimationInfo.lstSystem.Contains(0))
			{
				if (_infoAnimList.nShortBreahtPlay == 1 || _infoAnimList.nShortBreahtPlay == 3)
				{
					ctrlFlag.voice.playShorts[0] = 0;
				}
				if (_infoAnimList.nShortBreahtPlay == 1 || _infoAnimList.nShortBreahtPlay == 2)
				{
					ctrlFlag.voice.playShorts[1] = 0;
				}
			}
			ctrlFlag.voice.dialog = false;
		}
		oldHit = ctrlFlag.isGaugeHit;
		if (ctrlFlag.selectAnimationListInfo == null && ctrlFlag.feel_f >= 1f)
		{
			setPlay((_state != 0) ? "D_Orgasm" : "Orgasm");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			if (_state != 0)
			{
				ctrlFlag.AddParam(15, 1);
			}
			ctrlFlag.feel_f = 0f;
			ctrlFlag.isGaugeHit = false;
			ctrlFlag.voice.oldFinish = 0;
			isAtariHit.Value = ctrlFlag.isGaugeHit;
			ctrlFlag.numOrgasm = Mathf.Clamp(ctrlFlag.numOrgasm + 1, 0, 10);
			ctrlFlag.AddOrgasm();
			if (!ctrlFlag.isPainAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(4))
			{
				ctrlFlag.isPainAction = true;
			}
			if (!ctrlFlag.isConstraintAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(5))
			{
				ctrlFlag.isConstraintAction = true;
			}
			ctrlFlag.voice.dialog = false;
			ctrlFlag.rateNip = 1f;
			if (Config.HData.Gloss)
			{
				ctrlFlag.rateTuya = 1f;
			}
			bool sio = Config.HData.Sio;
			bool urine = Config.HData.Urine;
			if (!ProcBase.hSceneManager.bMerchant)
			{
				bool flag = Hitem.Effect(5);
				if (sio)
				{
					particle.Play(0);
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone && ctrlFlag.nowAnimationInfo.nAnimListInfoID != 4)
					{
						particle.Play(4);
					}
				}
				else if (((ctrlFlag.numFaintness == 0 && ctrlFlag.numOrgasm >= ctrlFlag.gotoFaintnessCount) || flag) && ProcBase.hSceneManager.GetFlaverSkillLevel(2) >= 100)
				{
					particle.Play(0);
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone && ctrlFlag.nowAnimationInfo.nAnimListInfoID != 4)
					{
						particle.Play(4);
					}
				}
				if (urine || ProcBase.hSceneManager.Toilet >= 70f)
				{
					particle.Play(1);
					ctrlFlag.voice.urines[0] = true;
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone)
					{
						particle.Play(5);
						ctrlFlag.voice.urines[1] = true;
					}
					ctrlFlag.numUrine++;
					ProcBase.hSceneManager.Toilet = 30f;
					int desireKey = Desire.GetDesireKey(Desire.Type.Toilet);
					if (ProcBase.hSceneManager.Agent[0] != null)
					{
						ProcBase.hSceneManager.Agent[0].SetDesire(desireKey, ProcBase.hSceneManager.Toilet);
					}
					if (ProcBase.hSceneManager.Agent[1] != null)
					{
						ProcBase.hSceneManager.Agent[1].SetDesire(desireKey, ProcBase.hSceneManager.Toilet);
					}
				}
			}
			else
			{
				if (sio)
				{
					particle.Play(0);
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone && ctrlFlag.nowAnimationInfo.nAnimListInfoID != 4)
					{
						particle.Play(4);
					}
				}
				if (urine)
				{
					particle.Play(1);
					ctrlFlag.voice.urines[0] = true;
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone)
					{
						particle.Play(5);
						ctrlFlag.voice.urines[1] = true;
					}
					int desireKey2 = Desire.GetDesireKey(Desire.Type.Toilet);
					if (ProcBase.hSceneManager.Agent[0] != null)
					{
						ProcBase.hSceneManager.Agent[0].SetDesire(desireKey2, ProcBase.hSceneManager.Toilet);
					}
					if (ProcBase.hSceneManager.Agent[1] != null)
					{
						ProcBase.hSceneManager.Agent[1].SetDesire(desireKey2, ProcBase.hSceneManager.Toilet);
					}
				}
			}
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			ctrlFlag.nowOrgasm = true;
		}
		return true;
	}

	private bool GotoFaintnessAibu(int _state)
	{
		bool flag = Hitem.Effect(5);
		if (_state == 0 && (ctrlFlag.numOrgasm >= ctrlFlag.gotoFaintnessCount || flag))
		{
			setPlay("D_Orgasm_A");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			if (flag)
			{
				Hitem.SetUse(5, use: false);
			}
			if (!ProcBase.hSceneManager.bMerchant && ctrlFlag.numFaintness == 0)
			{
				ctrlFlag.AddParam(14, 1);
			}
			ctrlFlag.isFaintness = true;
			ctrlFlag.numFaintness = Mathf.Clamp(ctrlFlag.numFaintness + 1, 0, 999999);
			sprite.SetVisibleLeaveItToYou(_visible: false);
			if (ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.GyakuYobai)
			{
				sprite.categoryMainButton.interactable = true;
				sprite.hPointButton.interactable = true;
			}
			sprite.SetToggleLeaveItToYou(_on: false);
			if (ctrlFlag.initiative != 0)
			{
				ctrlFlag.initiative = 0;
				sprite.MainCategoryOfLeaveItToYou(_isLeaveItToYou: false);
			}
		}
		else
		{
			setPlay((_state != 0) ? "D_Orgasm_A" : "Orgasm_A");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
		}
		ctrlFlag.nowOrgasm = false;
		return true;
	}

	private bool OrgasmAibuProc(int _state, float _normalizedTime)
	{
		if (_normalizedTime < 1f)
		{
			return false;
		}
		GotoFaintnessAibu(_state);
		return true;
	}

	private bool StartHoushiProc(int _state, bool _restart)
	{
		if (nextPlay == 0)
		{
			return false;
		}
		if (nextPlay == 1)
		{
			nextPlay = 2;
			if (!_restart)
			{
				ctrlFlag.voice.playStart = 2;
			}
			else
			{
				ctrlFlag.voice.playStart = 3;
			}
			return false;
		}
		if (nextPlay == 2)
		{
			for (int i = 0; i < 2; i++)
			{
				if (voice.nowVoices[i].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[i].state == HVoiceCtrl.VoiceKind.startVoice)
				{
					return false;
				}
			}
			if (ctrlFlag.voice.playStart > 4)
			{
				return false;
			}
		}
		nextPlay = 0;
		setPlay((_state != 0) ? "D_WLoop" : "WLoop");
		if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(0))
		{
			if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1 || ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 3)
			{
				voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 1, chaFemales[0], 0);
			}
			if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1)
			{
				voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 2, chaFemales[1], 1);
			}
		}
		else if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(1))
		{
			if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1 || ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 3)
			{
				voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 2, chaFemales[0], 0);
			}
			if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1)
			{
				voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 2, chaFemales[1], 1);
			}
		}
		ctrlFlag.speed = 0f;
		ctrlFlag.loopType = 0;
		ctrlFlag.isNotCtrl = false;
		ctrlFlag.nowSpeedStateFast = false;
		oldHit = false;
		for (int j = 0; j < 2; j++)
		{
			ctrlFlag.motions[j] = 0f;
			timeMotions[j] = 0f;
			timeChangeMotions[j] = Random.Range(ctrlFlag.changeAutoMotionTimeMin, ctrlFlag.changeAutoMotionTimeMax);
			timeChangeMotionDeltaTimes[j] = 0f;
		}
		feelHit.InitTime();
		if (_restart)
		{
			ctrlMeta.Clear();
			voice.AfterFinish();
		}
		return true;
	}

	private bool LoopHoushiProc(int _loop, int _state, float _wheel, HScene.AnimationListInfo _infoAnimList)
	{
		if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishBefore)
		{
			setPlay((_state != 0) ? "D_OLoop" : "OLoop");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.nowSpeedStateFast = false;
			ctrlFlag.feel_m = 0.7f;
			oldHit = false;
			if (ctrlFlag.nowAnimationInfo.nAnimListInfoID == 4)
			{
				if (chaFemales[0].visibleAll && chaFemales[0].objBodyBone != null && (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.startVoice))
				{
					Singleton<Voice>.Instance.Stop(ctrlFlag.voice.voiceTrs[0]);
				}
				ctrlFlag.voice.dialog = false;
			}
			feelHit.InitTime();
			ctrlFlag.isGaugeHit = false;
		}
		else
		{
			ctrlFlag.speed += _wheel;
			if (_loop == 0)
			{
				ctrlFlag.nowSpeedStateFast = ctrlFlag.speed >= 0.5f;
			}
			else
			{
				ctrlFlag.nowSpeedStateFast = ctrlFlag.speed >= 1.5f;
			}
			for (int i = 0; i < 2; i++)
			{
				if (!chaFemales[i].visibleAll || chaFemales[i].objBodyBone == null)
				{
					continue;
				}
				timeChangeMotionDeltaTimes[i] += Time.deltaTime;
				if (!(timeChangeMotions[i] <= timeChangeMotionDeltaTimes[i]) || enableMotions[i])
				{
					continue;
				}
				timeChangeMotions[i] = Random.Range(ctrlFlag.changeAutoMotionTimeMin, ctrlFlag.changeAutoMotionTimeMax);
				timeChangeMotionDeltaTimes[0] = 0f;
				enableMotions[i] = true;
				timeMotions[i] = 0f;
				float num = 0f;
				if (allowMotions[i])
				{
					num = 1f - ctrlFlag.motions[i];
					num = ((!(num <= ctrlFlag.changeMotionMinRate)) ? (ctrlFlag.motions[i] + Random.Range(ctrlFlag.changeMotionMinRate, num)) : 1f);
					if (num >= 1f)
					{
						allowMotions[i] = false;
					}
				}
				else
				{
					num = ctrlFlag.motions[i];
					num = ((!(num <= ctrlFlag.changeMotionMinRate)) ? (ctrlFlag.motions[i] - Random.Range(ctrlFlag.changeMotionMinRate, num)) : 0f);
					if (num <= 0f)
					{
						allowMotions[i] = true;
					}
				}
				ref Vector2 reference = ref lerpMotions[i];
				reference = new Vector2(ctrlFlag.motions[i], num);
				lerpTimes[i] = Random.Range(ctrlFlag.changeMotionTimeMin, ctrlFlag.changeMotionTimeMax);
			}
			if (_loop == 0)
			{
				if (ctrlFlag.speed > 1f && ctrlFlag.loopType == 0)
				{
					setPlay((_state != 0) ? "D_SLoop" : "SLoop");
					ctrlFlag.nowSpeedStateFast = false;
					if (ctrlFlag.nowAnimationInfo.nAnimListInfoID == 4)
					{
						for (int j = 0; j < 2; j++)
						{
							if (chaFemales[j].visibleAll && !(chaFemales[j].objBodyBone == null) && (voice.nowVoices[j].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[j].state == HVoiceCtrl.VoiceKind.startVoice))
							{
								Singleton<Voice>.Instance.Stop(ctrlFlag.voice.voiceTrs[j]);
							}
						}
						ctrlFlag.voice.dialog = false;
					}
					feelHit.InitTime();
					ctrlFlag.loopType = 1;
				}
				ctrlFlag.speed = Mathf.Clamp(ctrlFlag.speed, 0f, 2f);
			}
			else
			{
				if (ctrlFlag.speed < 1f && ctrlFlag.loopType == 1)
				{
					setPlay((_state != 0) ? "D_WLoop" : "WLoop");
					ctrlFlag.nowSpeedStateFast = true;
					if (ctrlFlag.nowAnimationInfo.nAnimListInfoID == 4)
					{
						for (int k = 0; k < 2; k++)
						{
							if (chaFemales[k].visibleAll && !(chaFemales[k].objBodyBone == null) && (voice.nowVoices[k].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[k].state == HVoiceCtrl.VoiceKind.startVoice))
							{
								Singleton<Voice>.Instance.Stop(ctrlFlag.voice.voiceTrs[k]);
							}
						}
						ctrlFlag.voice.dialog = false;
					}
					feelHit.InitTime();
					ctrlFlag.loopType = 0;
				}
				ctrlFlag.speed = Mathf.Clamp(ctrlFlag.speed, 0f, 2f);
			}
			ctrlFlag.isGaugeHit = feelHit.isHit(_infoAnimList.nFeelHit, _loop, (_loop != 0) ? (ctrlFlag.speed - 1f) : ctrlFlag.speed);
			if (ctrlFlag.isGaugeHit)
			{
				feelHit.ChangeHit(_infoAnimList.nFeelHit, _loop);
			}
			ctrlFlag.isGaugeHit_M = ctrlFlag.isGaugeHit;
			float num2 = 0f;
			num2 = Time.deltaTime * ctrlFlag.speedGuageRate;
			num2 *= ((!ctrlFlag.isGaugeHit) ? 1f : 2f) * (float)((!ctrlFlag.stopFeelMale) ? 1 : 0);
			if (!ProcBase.hSceneManager.bMerchant && ProcBase.hSceneManager.HSkil.ContainsValue(18))
			{
				num2 *= ctrlFlag.SkilChangeSpeed(18);
			}
			ctrlFlag.feel_m += num2;
			ctrlFlag.feel_m = Mathf.Clamp01(ctrlFlag.feel_m);
			isAtariHit.Value = ctrlFlag.isGaugeHit_M;
			if (ctrlFlag.isGaugeHit != oldHit && ctrlFlag.isGaugeHit)
			{
				if (randVoicePlays[0].Get() == 0)
				{
					ctrlFlag.voice.playVoices[0] = true;
				}
				else if (randVoicePlays[1].Get() == 0)
				{
					ctrlFlag.voice.playVoices[1] = true;
				}
				ctrlFlag.voice.dialog = false;
			}
			oldHit = ctrlFlag.isGaugeHit;
			if (ctrlFlag.feel_m >= 0.7f)
			{
				setPlay((_state != 0) ? "D_OLoop" : "OLoop");
				ctrlFlag.speed = 0f;
				ctrlFlag.loopType = -1;
				ctrlFlag.nowSpeedStateFast = false;
				if (ctrlFlag.nowAnimationInfo.nAnimListInfoID == 4)
				{
					for (int l = 0; l < 2; l++)
					{
						if (!chaFemales[l].visibleAll && !(chaFemales[l].objBodyBone == null) && (voice.nowVoices[l].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[l].state == HVoiceCtrl.VoiceKind.startVoice))
						{
							Singleton<Voice>.Instance.Stop(ctrlFlag.voice.voiceTrs[l]);
						}
					}
					ctrlFlag.voice.dialog = false;
				}
				oldHit = false;
				feelHit.InitTime();
			}
		}
		return true;
	}

	private bool OLoopHoushiProc(int _state, float _wheel, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		ctrlFlag.speed = Mathf.Clamp01(ctrlFlag.speed + _wheel);
		ctrlFlag.nowSpeedStateFast = ctrlFlag.speed >= 0.5f;
		feelHit.ChangeHit(_infoAnimList.nFeelHit, 2);
		ctrlFlag.isGaugeHit = feelHit.isHit(_infoAnimList.nFeelHit, 2, ctrlFlag.speed);
		ctrlFlag.isGaugeHit_M = ctrlFlag.isGaugeHit;
		float num = 0f;
		num = Time.deltaTime * ctrlFlag.speedGuageRate;
		num *= ((!ctrlFlag.isGaugeHit) ? 1f : 2f) * (float)((!ctrlFlag.stopFeelMale) ? 1 : 0);
		if (!ProcBase.hSceneManager.bMerchant && ProcBase.hSceneManager.HSkil.ContainsValue(18))
		{
			num *= ctrlFlag.SkilChangeSpeed(18);
		}
		ctrlFlag.feel_m += num;
		ctrlFlag.feel_m = Mathf.Clamp01(ctrlFlag.feel_m);
		isAtariHit.Value = ctrlFlag.isGaugeHit_M;
		if (ctrlFlag.isGaugeHit != oldHit && ctrlFlag.isGaugeHit)
		{
			if (randVoicePlays[0].Get() == 0)
			{
				ctrlFlag.voice.playVoices[0] = true;
			}
			else if (randVoicePlays[1].Get() == 0)
			{
				ctrlFlag.voice.playVoices[1] = true;
			}
			ctrlFlag.voice.dialog = false;
		}
		oldHit = ctrlFlag.isGaugeHit;
		if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishOutSide)
		{
			setPlay((_state != 0) ? "D_Orgasm_OUT" : "Orgasm_OUT");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.feel_m = 0f;
			ctrlFlag.isGaugeHit = false;
			ctrlFlag.isGaugeHit_M = ctrlFlag.isGaugeHit;
			isAtariHit.Value = ctrlFlag.isGaugeHit_M;
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			ctrlFlag.numOutSide = Mathf.Clamp(ctrlFlag.numOutSide + 1, 0, 999999);
			ctrlFlag.isHoushiFinish = true;
			ctrlFlag.nowOrgasm = true;
			ctrlFlag.voice.oldFinish = 2;
			ctrlFlag.voice.dialog = false;
			if (!ctrlFlag.isPainAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(4))
			{
				ctrlFlag.isPainAction = true;
			}
			if (!ctrlFlag.isConstraintAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(5))
			{
				ctrlFlag.isConstraintAction = true;
			}
		}
		else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishDrink && _modeCtrl != 1)
		{
			setPlay("Orgasm_IN");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.feel_m = 0f;
			ctrlFlag.isGaugeHit = false;
			ctrlFlag.isGaugeHit_M = ctrlFlag.isGaugeHit;
			isAtariHit.Value = ctrlFlag.isGaugeHit_M;
			ctrlFlag.voice.dialog = false;
			finishMotion = 0;
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			ctrlFlag.isHoushiFinish = true;
			ctrlFlag.nowOrgasm = true;
			ctrlFlag.voice.oldFinish = 1;
		}
		else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishVomit && _modeCtrl != 1)
		{
			setPlay("Orgasm_IN");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.feel_m = 0f;
			ctrlFlag.isGaugeHit = false;
			ctrlFlag.isGaugeHit_M = ctrlFlag.isGaugeHit;
			isAtariHit.Value = ctrlFlag.isGaugeHit_M;
			ctrlFlag.voice.dialog = false;
			finishMotion = 1;
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			ctrlFlag.isHoushiFinish = true;
			ctrlFlag.nowOrgasm = true;
			ctrlFlag.voice.oldFinish = 1;
		}
		return true;
	}

	private bool SetNextFinishAnimation(float _normalizedTime, string _nextAnimation, bool _isSpriteSet = true, bool _isFade = true)
	{
		if (_normalizedTime < 1f)
		{
			return false;
		}
		setPlay(_nextAnimation, _isFade);
		ctrlFlag.speed = 0f;
		ctrlFlag.loopType = -1;
		if (_isSpriteSet)
		{
			ctrlFlag.nowOrgasm = false;
		}
		return true;
	}

	private bool SetAfterInsideFinishAnimation(int _state, float _normalizedTime)
	{
		if (_normalizedTime < 1f)
		{
			return false;
		}
		if (finishMotion == 0)
		{
			ctrlFlag.numDrink = Mathf.Clamp(ctrlFlag.numDrink + 1, 0, 999999);
			if (!ctrlFlag.isPainAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(4))
			{
				ctrlFlag.isPainAction = true;
			}
			if (!ctrlFlag.isConstraintAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(5))
			{
				ctrlFlag.isConstraintAction = true;
			}
			setPlay("Drink_IN");
		}
		else if (finishMotion == 1)
		{
			ctrlFlag.numVomit = Mathf.Clamp(ctrlFlag.numVomit + 1, 0, 999999);
			if (!ctrlFlag.isPainAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(4))
			{
				ctrlFlag.isPainAction = true;
			}
			if (!ctrlFlag.isConstraintAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(5))
			{
				ctrlFlag.isConstraintAction = true;
			}
			setPlay("Vomit_IN");
		}
		ctrlFlag.speed = 0f;
		ctrlFlag.loopType = -1;
		return true;
	}

	private bool StartSonyuProc(bool _restart, int _state)
	{
		if (nextPlay == 0)
		{
			return false;
		}
		if (nextPlay == 1)
		{
			nextPlay = 2;
			if (!_restart)
			{
				ctrlFlag.voice.playStart = 2;
			}
			else
			{
				ctrlFlag.voice.playStart = 3;
			}
			return false;
		}
		if (nextPlay == 2)
		{
			for (int i = 0; i < 2; i++)
			{
				if (voice.nowVoices[i].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[i].state == HVoiceCtrl.VoiceKind.startVoice)
				{
					return false;
				}
			}
			if (ctrlFlag.voice.playStart > 4)
			{
				return false;
			}
		}
		setPlay((_state != 0) ? "D_Insert" : "Insert");
		ctrlFlag.loopType = -1;
		nextPlay = 0;
		ctrlFlag.speed = 0f;
		ctrlFlag.motions[0] = 0f;
		ctrlFlag.motions[1] = 0f;
		ctrlFlag.isNotCtrl = false;
		ctrlFlag.nowSpeedStateFast = false;
		timeMotions[0] = 0f;
		timeMotions[1] = 0f;
		oldHit = false;
		feelHit.InitTime();
		if (_state == 0)
		{
			for (int j = 0; j < 2; j++)
			{
				timeChangeMotions[j] = Random.Range(ctrlFlag.changeAutoMotionTimeMin, ctrlFlag.changeAutoMotionTimeMax);
				timeChangeMotionDeltaTimes[j] = 0f;
			}
		}
		if (_restart)
		{
			ctrlMeta.Clear();
			voice.AfterFinish();
		}
		return true;
	}

	private bool InsertProc(float _normalizedTime, int _state)
	{
		if (_normalizedTime < 1f)
		{
			return false;
		}
		setPlay((_state != 0) ? "D_WLoop" : "WLoop");
		if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(0))
		{
			if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1 || ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 3)
			{
				voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 1, chaFemales[0], 0);
			}
			if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1)
			{
				voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 2, chaFemales[1], 1);
			}
		}
		else if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(1))
		{
			if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1 || ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 3)
			{
				voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 2, chaFemales[0], 0);
			}
			if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1)
			{
				voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 2, chaFemales[1], 1);
			}
		}
		ctrlFlag.speed = 0f;
		ctrlFlag.loopType = 0;
		return true;
	}

	private bool LoopSonyuProc(int _loop, int _state, float _wheel, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishInSide && ctrlFlag.feel_m >= 0.7f && _modeCtrl == 3)
		{
			string[] array = new string[2] { "OrgasmM_IN", "D_OrgasmM_IN" };
			setPlay(array[_state]);
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.feel_m = 0f;
			if (ctrlFlag.feel_f > 0.5f)
			{
				ctrlFlag.feel_f = 0.5f;
			}
			ctrlFlag.isInsert = true;
			ctrlFlag.isGaugeHit = false;
			isAtariHit.Value = ctrlFlag.isGaugeHit;
			ctrlFlag.numInside = Mathf.Clamp(ctrlFlag.numInside + 1, 0, 999999);
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			ctrlFlag.voice.oldFinish = 1;
			ctrlFlag.nowOrgasm = true;
			ctrlFlag.voice.dialog = false;
		}
		else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishOutSide && ctrlFlag.feel_m >= 0.7f && (_modeCtrl != 4 || (_modeCtrl == 4 && _state == 0)))
		{
			string[] array2 = new string[2] { "OrgasmM_OUT", "D_OrgasmM_OUT" };
			setPlay(array2[_state]);
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.feel_m = 0f;
			if (ctrlFlag.feel_f > 0.5f)
			{
				ctrlFlag.feel_f = 0.5f;
			}
			ctrlFlag.isGaugeHit = false;
			isAtariHit.Value = ctrlFlag.isGaugeHit;
			ctrlFlag.numOutSide = Mathf.Clamp(ctrlFlag.numOutSide + 1, 0, 999999);
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			ctrlFlag.voice.oldFinish = 2;
			ctrlFlag.nowOrgasm = true;
			ctrlFlag.voice.dialog = false;
			if (ctrlFlag.numOutSide < 4)
			{
				ProcBase.hSceneManager.Bath += 10f;
			}
		}
		else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishBefore)
		{
			string[] array3 = new string[2] { "OLoop", "D_OLoop" };
			setPlay(array3[_state]);
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.feel_f = 0.7f;
			if (ctrlFlag.feel_m <= 0.7f)
			{
				ctrlFlag.feel_m = 0.7f;
			}
			ctrlFlag.nowSpeedStateFast = false;
			oldHit = false;
			feelHit.InitTime();
			for (int i = 0; i < 2; i++)
			{
				if (chaFemales[i].visibleAll && chaFemales[i].objTop != null)
				{
					if (voice.nowVoices[i].state == HVoiceCtrl.VoiceKind.voice && ctrlFlag.nowAnimationInfo.nAnimListInfoID == 3)
					{
						Singleton<Voice>.Instance.Stop(ctrlFlag.voice.voiceTrs[i]);
					}
					else if (voice.nowVoices[i].state == HVoiceCtrl.VoiceKind.startVoice && ctrlFlag.nowAnimationInfo.nAnimListInfoID == 3)
					{
						Singleton<Voice>.Instance.Stop(ctrlFlag.voice.voiceTrs[i]);
					}
				}
			}
			ctrlFlag.isGaugeHit = false;
		}
		else
		{
			ctrlFlag.speed += _wheel;
			if (_loop == 0)
			{
				ctrlFlag.nowSpeedStateFast = ctrlFlag.speed >= 0.5f;
			}
			else
			{
				ctrlFlag.nowSpeedStateFast = ctrlFlag.speed >= 1.5f;
			}
			for (int j = 0; j < 2; j++)
			{
				if (!chaFemales[j].visibleAll || chaFemales[j].objBodyBone == null)
				{
					continue;
				}
				timeChangeMotionDeltaTimes[j] += Time.deltaTime;
				if (!(timeChangeMotions[j] <= timeChangeMotionDeltaTimes[j]) || enableMotions[j] || _state != 0)
				{
					continue;
				}
				timeChangeMotions[j] = Random.Range(ctrlFlag.changeAutoMotionTimeMin, ctrlFlag.changeAutoMotionTimeMax);
				timeChangeMotionDeltaTimes[j] = 0f;
				enableMotions[j] = true;
				timeMotions[j] = 0f;
				float num = 0f;
				if (allowMotions[j])
				{
					num = 1f - ctrlFlag.motions[j];
					num = ((!(num <= ctrlFlag.changeMotionMinRate)) ? (ctrlFlag.motions[j] + Random.Range(ctrlFlag.changeMotionMinRate, num)) : 1f);
					if (num >= 1f)
					{
						allowMotions[j] = false;
					}
				}
				else
				{
					num = ctrlFlag.motions[j];
					num = ((!(num <= ctrlFlag.changeMotionMinRate)) ? (ctrlFlag.motions[j] - Random.Range(ctrlFlag.changeMotionMinRate, num)) : 0f);
					if (num <= 0f)
					{
						allowMotions[j] = true;
					}
				}
				ref Vector2 reference = ref lerpMotions[j];
				reference = new Vector2(ctrlFlag.motions[j], num);
				lerpTimes[j] = Random.Range(ctrlFlag.changeMotionTimeMin, ctrlFlag.changeMotionTimeMax);
			}
			if (_loop == 0)
			{
				if (ctrlFlag.speed > 1f && ctrlFlag.loopType == 0)
				{
					setPlay((_state != 0) ? "D_SLoop" : "SLoop");
					ctrlFlag.nowSpeedStateFast = false;
					for (int k = 0; k < 2; k++)
					{
						if (chaFemales[k].visibleAll && !(chaFemales[k].objTop == null))
						{
							if (voice.nowVoices[k].state == HVoiceCtrl.VoiceKind.voice)
							{
								Singleton<Voice>.Instance.Stop(ctrlFlag.voice.voiceTrs[k]);
							}
							else if (voice.nowVoices[k].state == HVoiceCtrl.VoiceKind.startVoice)
							{
								Singleton<Voice>.Instance.Stop(ctrlFlag.voice.voiceTrs[k]);
							}
						}
					}
					feelHit.InitTime();
					ctrlFlag.loopType = 1;
				}
				ctrlFlag.speed = Mathf.Clamp(ctrlFlag.speed, 0f, 2f);
			}
			else
			{
				if (ctrlFlag.speed < 1f && ctrlFlag.loopType == 1)
				{
					setPlay((_state != 0) ? "D_WLoop" : "WLoop");
					ctrlFlag.nowSpeedStateFast = true;
					for (int l = 0; l < 2; l++)
					{
						if (chaFemales[l].visibleAll && !(chaFemales[l].objTop == null) && (voice.nowVoices[l].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[l].state == HVoiceCtrl.VoiceKind.startVoice) && ctrlFlag.nowAnimationInfo.nAnimListInfoID == 3 && ctrlFlag.nowAnimationInfo.id == 0)
						{
							Singleton<Voice>.Instance.Stop(ctrlFlag.voice.voiceTrs[l]);
						}
					}
					feelHit.InitTime();
					ctrlFlag.loopType = 0;
				}
				ctrlFlag.speed = Mathf.Clamp(ctrlFlag.speed, 0f, 2f);
			}
			float num2 = 0f;
			if (_state != 1 || _modeCtrl != 4)
			{
				num2 = Time.deltaTime * ctrlFlag.speedGuageRate;
				num2 *= (float)((!ctrlFlag.stopFeelMale) ? 1 : 0);
				if (!ProcBase.hSceneManager.bMerchant && ProcBase.hSceneManager.HSkil.ContainsValue(1))
				{
					num2 *= ctrlFlag.SkilChangeSpeed(1);
				}
				ctrlFlag.feel_m += num2;
				ctrlFlag.feel_m = Mathf.Clamp01(ctrlFlag.feel_m);
			}
			ctrlFlag.isGaugeHit = feelHit.isHit(_infoAnimList.nFeelHit, _loop, (_loop != 0) ? (ctrlFlag.speed - 1f) : ctrlFlag.speed);
			isAtariHit.Value = ctrlFlag.isGaugeHit;
			float num3 = 0f;
			if (ctrlFlag.isGaugeHit)
			{
				feelHit.ChangeHit(_infoAnimList.nFeelHit, _loop);
				num3 = Time.deltaTime * ctrlFlag.speedGuageRate;
				num3 *= (float)((!ctrlFlag.stopFeelFemal) ? 1 : 0);
				if (!ProcBase.hSceneManager.bMerchant)
				{
					if (ProcBase.hSceneManager.HSkil.ContainsValue(15) && ctrlFlag.nowAnimationInfo.lstSystem.Contains(3))
					{
						num3 *= ctrlFlag.SkilChangeSpeed(15);
					}
					if (ProcBase.hSceneManager.HSkil.ContainsValue(3))
					{
						num3 *= ctrlFlag.SkilChangeSpeed(3);
					}
				}
				ctrlFlag.feel_f += num3;
				ctrlFlag.feel_f = Mathf.Clamp01(ctrlFlag.feel_f);
			}
			if (ctrlFlag.isGaugeHit != oldHit && ctrlFlag.isGaugeHit)
			{
				if (randVoicePlays[0].Get() == 0)
				{
					ctrlFlag.voice.playVoices[0] = true;
				}
				else if (randVoicePlays[1].Get() == 0)
				{
					ctrlFlag.voice.playVoices[1] = true;
				}
				if (!ctrlFlag.nowAnimationInfo.lstSystem.Contains(0) && _modeCtrl != 4)
				{
					if (_infoAnimList.nShortBreahtPlay == 1 || _infoAnimList.nShortBreahtPlay == 3)
					{
						ctrlFlag.voice.playShorts[0] = 0;
					}
					if (_infoAnimList.nShortBreahtPlay == 1 || _infoAnimList.nShortBreahtPlay == 2)
					{
						ctrlFlag.voice.playShorts[1] = 0;
					}
				}
				ctrlFlag.voice.dialog = false;
			}
			oldHit = ctrlFlag.isGaugeHit;
			if (ctrlFlag.feel_f >= 0.7f)
			{
				setPlay((_state != 0) ? "D_OLoop" : "OLoop");
				ctrlFlag.speed = 0f;
				ctrlFlag.loopType = -1;
				ctrlFlag.nowSpeedStateFast = false;
				oldHit = false;
				feelHit.InitTime();
				for (int m = 0; m < 2; m++)
				{
					if (chaFemales[m].visibleAll && !(chaFemales[m].objTop == null) && (voice.nowVoices[m].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[m].state == HVoiceCtrl.VoiceKind.startVoice) && ctrlFlag.nowAnimationInfo.nAnimListInfoID == 3)
					{
						Singleton<Voice>.Instance.Stop(ctrlFlag.voice.voiceTrs[m]);
					}
				}
			}
		}
		return true;
	}

	private bool OLoopSonyuProc(int _state, float _wheel, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishBefore)
		{
			if (ctrlFlag.feel_m <= 0.7f)
			{
				ctrlFlag.feel_m = 0.7f;
				ctrlFlag.isGaugeHit = false;
			}
			return true;
		}
		float num = 0f;
		if (_state != 1 || _modeCtrl != 4)
		{
			num = Time.deltaTime * ctrlFlag.speedGuageRate;
			num *= (float)((!ctrlFlag.stopFeelMale) ? 1 : 0);
			if (!ProcBase.hSceneManager.bMerchant && ProcBase.hSceneManager.HSkil.ContainsValue(1))
			{
				num *= ctrlFlag.SkilChangeSpeed(1);
			}
			ctrlFlag.feel_m += num;
			ctrlFlag.feel_m = Mathf.Clamp01(ctrlFlag.feel_m);
		}
		ctrlFlag.speed = Mathf.Clamp01(ctrlFlag.speed + _wheel);
		ctrlFlag.nowSpeedStateFast = ctrlFlag.speed >= 0.5f;
		feelHit.ChangeHit(_infoAnimList.nFeelHit, 2);
		ctrlFlag.isGaugeHit = feelHit.isHit(_infoAnimList.nFeelHit, 2, ctrlFlag.speed);
		float num2 = 0f;
		if (ctrlFlag.isGaugeHit)
		{
			num2 = Time.deltaTime * ctrlFlag.speedGuageRate;
			num2 *= (float)((!ctrlFlag.stopFeelFemal) ? 1 : 0);
			if (!ProcBase.hSceneManager.bMerchant)
			{
				if (ProcBase.hSceneManager.HSkil.ContainsValue(15) && ctrlFlag.nowAnimationInfo.lstSystem.Contains(3))
				{
					num2 *= ctrlFlag.SkilChangeSpeed(15);
				}
				if (ProcBase.hSceneManager.HSkil.ContainsValue(3))
				{
					num2 *= ctrlFlag.SkilChangeSpeed(3);
				}
			}
			ctrlFlag.feel_f += num2;
			ctrlFlag.feel_f = Mathf.Clamp01(ctrlFlag.feel_f);
		}
		if (ctrlFlag.isGaugeHit != oldHit && ctrlFlag.isGaugeHit)
		{
			if (randVoicePlays[0].Get() == 0)
			{
				ctrlFlag.voice.playVoices[0] = true;
			}
			else if (randVoicePlays[1].Get() == 0)
			{
				ctrlFlag.voice.playVoices[1] = true;
			}
			if (!ctrlFlag.nowAnimationInfo.lstSystem.Contains(0) && _modeCtrl != 4)
			{
				if ((_infoAnimList.nShortBreahtPlay == 1 || _infoAnimList.nShortBreahtPlay == 3) && _infoAnimList.nAnimListInfoID != 3)
				{
					ctrlFlag.voice.playShorts[0] = 0;
				}
				if (_infoAnimList.nShortBreahtPlay == 1 || _infoAnimList.nShortBreahtPlay == 2)
				{
					ctrlFlag.voice.playShorts[1] = 0;
				}
			}
			ctrlFlag.voice.dialog = false;
		}
		oldHit = ctrlFlag.isGaugeHit;
		if (ctrlFlag.selectAnimationListInfo == null && ctrlFlag.feel_f >= 1f)
		{
			setPlay((_state != 0) ? "D_OrgasmF_IN" : "OrgasmF_IN");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.feel_f = 0f;
			ctrlFlag.isGaugeHit = false;
			isAtariHit.Value = ctrlFlag.isGaugeHit;
			ctrlFlag.numOrgasm = Mathf.Clamp(ctrlFlag.numOrgasm + 1, 0, 10);
			ctrlFlag.AddOrgasm();
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			ctrlFlag.voice.oldFinish = 0;
			ctrlFlag.nowOrgasm = true;
			ctrlFlag.voice.dialog = false;
			if (!ctrlFlag.isPainAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(4))
			{
				ctrlFlag.isPainAction = true;
			}
			if (!ctrlFlag.isConstraintAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(5))
			{
				ctrlFlag.isConstraintAction = true;
			}
			ctrlFlag.rateNip = 1f;
			if (Config.HData.Gloss)
			{
				ctrlFlag.rateTuya = 1f;
			}
			bool sio = Config.HData.Sio;
			bool urine = Config.HData.Urine;
			if (!ProcBase.hSceneManager.bMerchant)
			{
				bool flag = Hitem.Effect(5);
				if (sio)
				{
					particle.Play(0);
					if ((bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone)
					{
						particle.Play(4);
					}
				}
				else if (((ctrlFlag.numFaintness == 0 && ctrlFlag.numOrgasm >= ctrlFlag.gotoFaintnessCount) || flag) && ProcBase.hSceneManager.GetFlaverSkillLevel(2) >= 100)
				{
					particle.Play(0);
					if ((bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone)
					{
						particle.Play(4);
					}
				}
				if (urine || ProcBase.hSceneManager.Toilet >= 70f)
				{
					particle.Play(1);
					ctrlFlag.voice.urines[0] = true;
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone)
					{
						particle.Play(5);
						ctrlFlag.voice.urines[1] = true;
					}
					ctrlFlag.numUrine++;
					ProcBase.hSceneManager.Toilet = 30f;
					int desireKey = Desire.GetDesireKey(Desire.Type.Toilet);
					if (ProcBase.hSceneManager.Agent[0] != null)
					{
						ProcBase.hSceneManager.Agent[0].SetDesire(desireKey, ProcBase.hSceneManager.Toilet);
					}
					if (ProcBase.hSceneManager.Agent[1] != null)
					{
						ProcBase.hSceneManager.Agent[1].SetDesire(desireKey, ProcBase.hSceneManager.Toilet);
					}
				}
			}
			else
			{
				if (sio)
				{
					particle.Play(0);
					if ((bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone)
					{
						particle.Play(4);
					}
				}
				if (urine)
				{
					particle.Play(1);
					ctrlFlag.voice.urines[0] = true;
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone)
					{
						particle.Play(5);
						ctrlFlag.voice.urines[1] = true;
					}
					ctrlFlag.numUrine++;
					ProcBase.hSceneManager.Toilet = 30f;
					int desireKey2 = Desire.GetDesireKey(Desire.Type.Toilet);
					if (ProcBase.hSceneManager.Agent[0] != null)
					{
						ProcBase.hSceneManager.Agent[0].SetDesire(desireKey2, ProcBase.hSceneManager.Toilet);
					}
					if (ProcBase.hSceneManager.Agent[1] != null)
					{
						ProcBase.hSceneManager.Agent[1].SetDesire(desireKey2, ProcBase.hSceneManager.Toilet);
					}
				}
			}
		}
		else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishInSide && ctrlFlag.feel_m >= 0.7f && _modeCtrl == 3)
		{
			setPlay((_state != 0) ? "D_OrgasmM_IN" : "OrgasmM_IN");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.feel_m = 0f;
			if (ctrlFlag.feel_f > 0.5f)
			{
				ctrlFlag.feel_f = 0.5f;
			}
			ctrlFlag.isInsert = true;
			ctrlFlag.isGaugeHit = false;
			isAtariHit.Value = ctrlFlag.isGaugeHit;
			ctrlFlag.numInside = Mathf.Clamp(ctrlFlag.numInside + 1, 0, 999999);
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			ctrlFlag.voice.oldFinish = 1;
			ctrlFlag.nowOrgasm = true;
			if (!ctrlFlag.isPainAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(4))
			{
				ctrlFlag.isPainAction = true;
			}
			if (!ctrlFlag.isConstraintAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(5))
			{
				ctrlFlag.isConstraintAction = true;
			}
			ctrlFlag.voice.dialog = false;
		}
		else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishOutSide && ctrlFlag.feel_m >= 0.7f && (_modeCtrl != 4 || (_modeCtrl == 4 && _state == 0)))
		{
			setPlay((_state != 0) ? "D_OrgasmM_OUT" : "OrgasmM_OUT");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.feel_m = 0f;
			if (ctrlFlag.feel_f > 0.5f)
			{
				ctrlFlag.feel_f = 0.5f;
			}
			ctrlFlag.isGaugeHit = false;
			isAtariHit.Value = ctrlFlag.isGaugeHit;
			ctrlFlag.numOutSide = Mathf.Clamp(ctrlFlag.numOutSide + 1, 0, 999999);
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			ctrlFlag.voice.oldFinish = 2;
			ctrlFlag.nowOrgasm = true;
			if (!ctrlFlag.isPainAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(4))
			{
				ctrlFlag.isPainAction = true;
			}
			if (!ctrlFlag.isConstraintAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(5))
			{
				ctrlFlag.isConstraintAction = true;
			}
			ctrlFlag.voice.dialog = false;
		}
		else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishSame && ctrlFlag.feel_m >= 0.7f && (_modeCtrl != 4 || (_modeCtrl == 4 && _state == 0)))
		{
			setPlay((_state != 0) ? "D_OrgasmS_IN" : "OrgasmS_IN");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.feel_m = 0f;
			ctrlFlag.feel_f = 0f;
			ctrlFlag.isInsert = true;
			ctrlFlag.isGaugeHit = false;
			isAtariHit.Value = ctrlFlag.isGaugeHit;
			ctrlFlag.numOrgasm = Mathf.Clamp(ctrlFlag.numOrgasm + 1, 0, 10);
			ctrlFlag.AddOrgasm();
			ctrlFlag.numSameOrgasm = Mathf.Clamp(ctrlFlag.numSameOrgasm + 1, 0, 999999);
			ctrlFlag.nowOrgasm = true;
			if (!ctrlFlag.isPainAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(4))
			{
				ctrlFlag.isPainAction = true;
			}
			if (!ctrlFlag.isConstraintAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(5))
			{
				ctrlFlag.isConstraintAction = true;
			}
			ctrlFlag.voice.dialog = false;
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			if (_modeCtrl == 3)
			{
				ctrlFlag.numInside = Mathf.Clamp(ctrlFlag.numInside + 1, 0, 999999);
			}
			else
			{
				ctrlFlag.numOutSide = Mathf.Clamp(ctrlFlag.numOutSide + 1, 0, 999999);
			}
			ctrlFlag.voice.oldFinish = 3;
			bool urine2 = Config.HData.Urine;
			bool sio2 = Config.HData.Sio;
			if (!ProcBase.hSceneManager.bMerchant)
			{
				bool flag2 = Hitem.Effect(5);
				if (sio2)
				{
					particle.Play(0);
					if ((bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone)
					{
						particle.Play(4);
					}
				}
				else if (((ctrlFlag.numFaintness == 0 && ctrlFlag.numOrgasm >= ctrlFlag.gotoFaintnessCount) || flag2) && ProcBase.hSceneManager.GetFlaverSkillLevel(2) >= 100)
				{
					particle.Play(0);
					if ((bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone)
					{
						particle.Play(4);
					}
				}
				if (urine2 || ProcBase.hSceneManager.Toilet >= 70f)
				{
					particle.Play(1);
					ctrlFlag.voice.urines[0] = true;
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone)
					{
						particle.Play(5);
						ctrlFlag.voice.urines[1] = true;
					}
					ctrlFlag.numUrine++;
					ProcBase.hSceneManager.Toilet = 30f;
					int desireKey3 = Desire.GetDesireKey(Desire.Type.Toilet);
					if (ProcBase.hSceneManager.Agent[0] != null)
					{
						ProcBase.hSceneManager.Agent[0].SetDesire(desireKey3, ProcBase.hSceneManager.Toilet);
					}
					if (ProcBase.hSceneManager.Agent[1] != null)
					{
						ProcBase.hSceneManager.Agent[1].SetDesire(desireKey3, ProcBase.hSceneManager.Toilet);
					}
				}
			}
			else
			{
				if (sio2)
				{
					particle.Play(0);
					if ((bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone)
					{
						particle.Play(4);
					}
				}
				if (urine2)
				{
					particle.Play(1);
					ctrlFlag.voice.urines[0] = true;
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone)
					{
						particle.Play(5);
						ctrlFlag.voice.urines[1] = true;
					}
					ctrlFlag.numUrine++;
					ProcBase.hSceneManager.Toilet = 30f;
					int desireKey4 = Desire.GetDesireKey(Desire.Type.Toilet);
					if (ProcBase.hSceneManager.Agent[0] != null)
					{
						ProcBase.hSceneManager.Agent[0].SetDesire(desireKey4, ProcBase.hSceneManager.Toilet);
					}
					if (ProcBase.hSceneManager.Agent[1] != null)
					{
						ProcBase.hSceneManager.Agent[1].SetDesire(desireKey4, ProcBase.hSceneManager.Toilet);
					}
				}
			}
		}
		return true;
	}

	private bool AfterTheNextWaitingAnimation(float _normalizedTime, float _loopCount, int _state, int _modeCtrl, int _nextAfter)
	{
		if (_normalizedTime < _loopCount)
		{
			return false;
		}
		switch (_nextAfter)
		{
		case 0:
			GotoFaintnessSonyu(_state, _modeCtrl, (_modeCtrl != 3) ? 1 : 0);
			break;
		case 1:
			setPlay((_state != 0) ? "D_Orgasm_IN_A" : "Orgasm_IN_A");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			break;
		case 2:
			setPlay((_state != 0) ? "D_OrgasmM_OUT_A" : "OrgasmM_OUT_A");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.isInsert = false;
			break;
		}
		ctrlFlag.nowOrgasm = false;
		return true;
	}

	private bool GotoFaintnessSonyu(int _state, int _modeCtrl, int _nextAfter)
	{
		bool flag = Hitem.Effect(5);
		if (_state == 0 && (ctrlFlag.numOrgasm >= ctrlFlag.gotoFaintnessCount || flag))
		{
			setPlay((_nextAfter != 0) ? "D_OrgasmM_OUT_A" : "D_Orgasm_IN_A");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			if (flag)
			{
				Hitem.SetUse(5, use: false);
			}
			if (!ProcBase.hSceneManager.bMerchant && ctrlFlag.numFaintness == 0)
			{
				ctrlFlag.AddParam(14, 1);
			}
			ctrlFlag.isFaintness = true;
			ctrlFlag.numFaintness = Mathf.Clamp(ctrlFlag.numFaintness + 1, 0, 999999);
			sprite.SetVisibleLeaveItToYou(_visible: false);
			if (ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.GyakuYobai)
			{
				sprite.categoryMainButton.interactable = true;
				sprite.hPointButton.interactable = true;
			}
			ctrlMeta.SetParameterFromState(1);
			sprite.SetToggleLeaveItToYou(_on: false);
			if (ctrlFlag.initiative != 0)
			{
				ctrlFlag.initiative = 0;
				sprite.MainCategoryOfLeaveItToYou(_isLeaveItToYou: false);
			}
			sprite.SetAnimationMenu();
			sprite.SetFinishSelect(7, _modeCtrl);
		}
		else
		{
			setPlay((_state == 0) ? ((_nextAfter != 0) ? "OrgasmM_OUT_A" : "Orgasm_IN_A") : ((_nextAfter != 0) ? "D_OrgasmM_OUT_A" : "D_Orgasm_IN_A"));
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
		}
		return true;
	}

	private bool FinishNextAnimationByMorS(float _normalizedTime, float _loopCount, int _state, int _modeCtrl, bool _finishMorS)
	{
		AfterTheNextWaitingAnimation(_normalizedTime, _loopCount, _state, _modeCtrl, (!_finishMorS) ? 1 : 0);
		return true;
	}

	private bool AfterTheInsideWaitingProc(int _state, float _wheel, int _modeCtrl)
	{
		for (int i = 0; i < 2; i++)
		{
			if (voice.nowVoices[i].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[i].state == HVoiceCtrl.VoiceKind.startVoice)
			{
				return false;
			}
		}
		if (ctrlFlag.voice.dialog || ctrlFlag.voice.playStart > 4)
		{
			return false;
		}
		switch (nextPlay)
		{
		case 0:
			if (_wheel < 0f)
			{
				setPlay((_state != 0) ? "D_Pull" : "Pull");
				ctrlFlag.speed = 0f;
				ctrlFlag.loopType = -1;
				for (int l = 0; l < 2; l++)
				{
					ctrlFlag.motions[l] = 0f;
					timeMotions[l] = 0f;
				}
				sprite.objMotionListPanel.SetActive(value: false);
				sprite.SetEnableCategoryMain(_enable: false);
				sprite.SetEnableHItem(_enable: false);
				ctrlFlag.nowOrgasm = true;
				voice.AfterFinish();
				oldHit = false;
				feelHit.InitTime();
			}
			else if (_wheel > 0f)
			{
				ctrlFlag.voice.playStart = 3;
				nextPlay = 1;
			}
			break;
		case 1:
		{
			setPlay((_state != 0) ? "D_WLoop" : "WLoop");
			if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(0))
			{
				if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1 || ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 3)
				{
					voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 1, chaFemales[0], 0);
				}
				if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1)
				{
					voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 2, chaFemales[1], 1);
				}
			}
			else if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(1))
			{
				if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1 || ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 3)
				{
					voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 2, chaFemales[0], 0);
				}
				if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1)
				{
					voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 2, chaFemales[1], 1);
				}
			}
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = 0;
			ctrlFlag.nowSpeedStateFast = false;
			for (int j = 0; j < 2; j++)
			{
				ctrlFlag.motions[j] = 0f;
				timeMotions[j] = 0f;
			}
			if (_state == 0)
			{
				for (int k = 0; k < 2; k++)
				{
					timeChangeMotions[k] = Random.Range(ctrlFlag.changeAutoMotionTimeMin, ctrlFlag.changeAutoMotionTimeMax);
					timeChangeMotionDeltaTimes[k] = 0f;
				}
			}
			voice.AfterFinish();
			oldHit = false;
			feelHit.InitTime();
			nextPlay = 0;
			ctrlMeta.Clear();
			break;
		}
		}
		return true;
	}

	private bool PullProc(float _normalizedTime, int _state)
	{
		if (_normalizedTime < 1f)
		{
			return false;
		}
		if (ctrlFlag.isInsert)
		{
			setPlay((_state != 0) ? "D_Drop" : "Drop");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
		}
		else
		{
			setPlay((_state != 0) ? "D_OrgasmM_OUT_A" : "OrgasmM_OUT_A");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.nowOrgasm = false;
		}
		return true;
	}

	private void SetFinishCategoryEnable(AnimatorStateInfo _ai, int _modeCtrl)
	{
		bool flag = _ai.IsName("WLoop") || _ai.IsName("SLoop") || _ai.IsName("D_WLoop") || _ai.IsName("D_SLoop") || _ai.IsName("OLoop") || _ai.IsName("D_OLoop");
		bool flag2 = ctrlFlag.feel_m >= 0.7f && flag;
		bool flag3 = ((_modeCtrl == 3 || _modeCtrl == 4) ? ((ctrlFlag.feel_f < 0.7f || ctrlFlag.feel_m < 0.7f) && flag) : (ctrlFlag.feel_f < 0.7f || ctrlFlag.feel_m < 0.7f));
		flag3 &= Config.HData.FinishButton;
		sprite.categoryFinish.SetActive(flag3, 3);
		if (sprite.IsFinishVisible(0))
		{
			sprite.categoryFinish.SetActive(flag2 && _modeCtrl != 0, 0);
		}
		if (sprite.IsFinishVisible(1))
		{
			sprite.categoryFinish.SetActive(flag2 && ctrlFlag.feel_f >= 0.7f && _modeCtrl == 3, 1);
		}
		if (sprite.IsFinishVisible(2))
		{
			sprite.categoryFinish.SetActive(flag2 && _modeCtrl == 3, 2);
		}
		if (sprite.IsFinishVisible(4))
		{
			sprite.categoryFinish.SetActive(flag2 && _modeCtrl == 2, 4);
		}
		if (sprite.IsFinishVisible(5))
		{
			sprite.categoryFinish.SetActive(flag2 && _modeCtrl == 2, 5);
		}
	}

	private HScene.AnimationListInfo RecoverFaintnessAi()
	{
		List<ValueTuple<HSceneManager.HEvent, int, HScene.StartMotion>> lstStartAnimInfo = Singleton<Manager.Resources>.Instance.HSceneTable.lstStartAnimInfo;
		foreach (ValueTuple<HSceneManager.HEvent, int, HScene.StartMotion> item in lstStartAnimInfo)
		{
			if (item.Item1 != ProcBase.hSceneManager.EventKind || item.Item2 != ProcBase.hSceneManager.height || item.Item3.mode != 1)
			{
				continue;
			}
			return lstAnimation[item.Item3.id];
		}
		sbWarning.Clear();
		if (lstAnimation.Count == 0)
		{
			sbWarning.Append("RecoverFaintnessAi：失敗\n");
			Debug.LogWarning(sbWarning.ToString());
			return null;
		}
		sbWarning.Append("RecoverFaintnessAi：失敗\n").Append("回復後の体位を").Append(lstAnimation[0].nameAnimation)
			.Append("に設定");
		Debug.LogWarning(sbWarning.ToString());
		return lstAnimation[0];
	}

	private bool AutoStartProcTrigger(bool _start)
	{
		if (nextPlay != 0)
		{
			return false;
		}
		if (!_start)
		{
			if (!auto.IsStart())
			{
				return false;
			}
		}
		else if (!auto.IsReStart())
		{
			return false;
		}
		for (int i = 0; i < 2; i++)
		{
			if (voice.nowVoices[i].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[i].state == HVoiceCtrl.VoiceKind.startVoice)
			{
				return false;
			}
		}
		nextPlay = 1;
		return true;
	}

	private bool AutoStartAibuProc(bool _isReStart)
	{
		if (nextPlay == 0)
		{
			return false;
		}
		if (nextPlay == 1)
		{
			nextPlay = 2;
			if (!_isReStart)
			{
				ctrlFlag.voice.playStart = 2;
			}
			else
			{
				ctrlFlag.voice.playStart = 3;
			}
			return false;
		}
		if (nextPlay == 2)
		{
			for (int i = 0; i < 2; i++)
			{
				if (voice.nowVoices[i].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[i].state == HVoiceCtrl.VoiceKind.startVoice)
				{
					return false;
				}
			}
		}
		nextPlay = 0;
		if (!_isReStart || (_isReStart && !auto.IsChangeActionAtRestart()))
		{
			setPlay("WLoop");
			if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(0))
			{
				if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1 || ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 3)
				{
					voice.PlaySoundETC("WLoop", 1, chaFemales[0], 0);
				}
				if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1)
				{
					voice.PlaySoundETC("WLoop", 2, chaFemales[1], 1);
				}
			}
			else if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(1))
			{
				if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1 || ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 3)
				{
					voice.PlaySoundETC("WLoop", 2, chaFemales[0], 0);
				}
				if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1)
				{
					voice.PlaySoundETC("WLoop", 2, chaFemales[1], 1);
				}
			}
		}
		else
		{
			ctrlFlag.isAutoActionChange = true;
		}
		ctrlFlag.speed = 0f;
		ctrlFlag.loopType = 0;
		ctrlFlag.motions[0] = 0f;
		ctrlFlag.motions[1] = 0f;
		ctrlFlag.nowSpeedStateFast = false;
		oldHit = false;
		for (int j = 0; j < 2; j++)
		{
			timeMotions[j] = 0f;
			timeChangeMotions[j] = Random.Range(ctrlFlag.changeAutoMotionTimeMin, ctrlFlag.changeAutoMotionTimeMax);
			timeChangeMotionDeltaTimes[j] = 0f;
		}
		ctrlFlag.isNotCtrl = false;
		auto.Reset();
		feelHit.InitTime();
		if (_isReStart)
		{
			voice.AfterFinish();
		}
		return true;
	}

	private bool AutoLoopAibuProc(int _loop, int _state, float _wheel, HScene.AnimationListInfo _infoAnimList)
	{
		if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishBefore)
		{
			setPlay((_state != 0) ? "D_OLoop" : "OLoop");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.feel_f = 0.7f;
			ctrlFlag.nowSpeedStateFast = false;
			auto.SetSpeed(0f);
			oldHit = false;
			feelHit.InitTime();
			ctrlFlag.isGaugeHit = false;
		}
		else
		{
			if (_state == 0)
			{
				for (int i = 0; i < 2; i++)
				{
					if (!chaFemales[i].visibleAll || chaFemales[i].objBodyBone == null)
					{
						continue;
					}
					timeChangeMotionDeltaTimes[i] += Time.deltaTime;
					if (!(timeChangeMotions[i] <= timeChangeMotionDeltaTimes[i]) || enableMotions[i])
					{
						continue;
					}
					timeChangeMotions[i] = Random.Range(ctrlFlag.changeAutoMotionTimeMin, ctrlFlag.changeAutoMotionTimeMax);
					timeChangeMotionDeltaTimes[i] = 0f;
					enableMotions[i] = true;
					timeMotions[i] = 0f;
					float num = 0f;
					if (allowMotions[i])
					{
						num = 1f - ctrlFlag.motions[i];
						num = ((!(num <= ctrlFlag.changeMotionMinRate)) ? (ctrlFlag.motions[i] + Random.Range(ctrlFlag.changeMotionMinRate, num)) : 1f);
						if (num >= 1f)
						{
							allowMotions[i] = false;
						}
					}
					else
					{
						num = ctrlFlag.motions[i];
						num = ((!(num <= ctrlFlag.changeMotionMinRate)) ? (ctrlFlag.motions[i] - Random.Range(ctrlFlag.changeMotionMinRate, num)) : 0f);
						if (num <= 0f)
						{
							allowMotions[i] = true;
						}
					}
					ref Vector2 reference = ref lerpMotions[i];
					reference = new Vector2(ctrlFlag.motions[i], num);
					lerpTimes[i] = Random.Range(ctrlFlag.changeMotionTimeMin, ctrlFlag.changeMotionTimeMax);
				}
			}
			feelHit.ChangeHit(_infoAnimList.nFeelHit, _loop);
			Vector2 hitArea = feelHit.GetHitArea(_infoAnimList.nFeelHit, _loop);
			if (auto.ChangeLoopMotion(_loop == 1))
			{
				setPlay((_loop == 0) ? ((_state != 0) ? "D_SLoop" : "SLoop") : ((_state != 0) ? "D_WLoop" : "WLoop"));
				for (int j = 0; j < 2; j++)
				{
					if (voice.nowVoices[j].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[j].state == HVoiceCtrl.VoiceKind.startVoice)
					{
						Singleton<Voice>.Instance.Stop(ctrlFlag.voice.voiceTrs[j]);
					}
				}
				feelHit.InitTime();
			}
			else if (auto.AddSpeed(_wheel, _loop))
			{
				setPlay((_loop == 0) ? ((_state != 0) ? "D_SLoop" : "SLoop") : ((_state != 0) ? "D_WLoop" : "WLoop"));
				for (int k = 0; k < 2; k++)
				{
					if (voice.nowVoices[k].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[k].state == HVoiceCtrl.VoiceKind.startVoice)
					{
						Singleton<Voice>.Instance.Stop(ctrlFlag.voice.voiceTrs[k]);
					}
				}
				feelHit.InitTime();
			}
			if (_loop == 0)
			{
				ctrlFlag.speed = auto.GetSpeed(_loop: false);
				ctrlFlag.nowSpeedStateFast = ctrlFlag.speed >= 0.5f;
			}
			else
			{
				ctrlFlag.speed = auto.GetSpeed(_loop: true);
				ctrlFlag.nowSpeedStateFast = ctrlFlag.speed >= 0.5f;
			}
			ctrlFlag.isGaugeHit = GlobalMethod.RangeOn(ctrlFlag.speed, hitArea.x, hitArea.y);
			isAtariHit.Value = ctrlFlag.isGaugeHit;
			float num2 = 0f;
			if (_state == 1)
			{
				if (ctrlFlag.isGaugeHit)
				{
					num2 = Time.deltaTime * ctrlFlag.speedGuageRate;
					num2 *= (float)((!ctrlFlag.stopFeelFemal) ? 1 : 0);
					if (!ProcBase.hSceneManager.bMerchant)
					{
						if (ProcBase.hSceneManager.HSkil.ContainsValue(0) && ctrlFlag.nowAnimationInfo.lstSystem.Contains(2))
						{
							num2 *= ctrlFlag.SkilChangeSpeed(0);
						}
						else if (ProcBase.hSceneManager.HSkil.ContainsValue(15) && ctrlFlag.nowAnimationInfo.lstSystem.Contains(3))
						{
							num2 *= ctrlFlag.SkilChangeSpeed(15);
						}
						if (ProcBase.hSceneManager.HSkil.ContainsValue(3))
						{
							num2 *= ctrlFlag.SkilChangeSpeed(3);
						}
					}
					ctrlFlag.feel_f += num2;
					ctrlFlag.feel_f = Mathf.Clamp01(ctrlFlag.feel_f);
				}
			}
			else
			{
				num2 = Time.deltaTime * ctrlFlag.speedGuageRate;
				num2 *= ((!ctrlFlag.isGaugeHit) ? 0.3f : 1f) * (float)((!ctrlFlag.stopFeelFemal) ? 1 : 0);
				if (!ProcBase.hSceneManager.bMerchant)
				{
					if (ProcBase.hSceneManager.HSkil.ContainsValue(0) && ctrlFlag.nowAnimationInfo.lstSystem.Contains(2))
					{
						num2 *= ctrlFlag.SkilChangeSpeed(0);
					}
					else if (ProcBase.hSceneManager.HSkil.ContainsValue(15) && ctrlFlag.nowAnimationInfo.lstSystem.Contains(3))
					{
						num2 *= ctrlFlag.SkilChangeSpeed(15);
					}
					if (ProcBase.hSceneManager.HSkil.ContainsValue(3))
					{
						num2 *= ctrlFlag.SkilChangeSpeed(3);
					}
				}
				ctrlFlag.feel_f += num2;
				ctrlFlag.feel_f = Mathf.Clamp01(ctrlFlag.feel_f);
			}
			if (ctrlFlag.selectAnimationListInfo == null)
			{
				ctrlFlag.isAutoActionChange = auto.IsChangeActionAtLoop();
			}
			if (ctrlFlag.isGaugeHit != oldHit && ctrlFlag.isGaugeHit && !ctrlFlag.isAutoActionChange)
			{
				if (randVoicePlays[0].Get() == 0)
				{
					ctrlFlag.voice.playVoices[0] = true;
				}
				else if (randVoicePlays[1].Get() == 0)
				{
					ctrlFlag.voice.playVoices[1] = true;
				}
				if (!ctrlFlag.nowAnimationInfo.lstSystem.Contains(0))
				{
					ctrlFlag.voice.playShorts[0] = 0;
					ctrlFlag.voice.playShorts[1] = 0;
				}
			}
			oldHit = ctrlFlag.isGaugeHit;
			if (ctrlFlag.feel_f >= 0.7f)
			{
				setPlay((_state != 0) ? "D_OLoop" : "OLoop");
				ctrlFlag.speed = 0f;
				ctrlFlag.loopType = -1;
				ctrlFlag.nowSpeedStateFast = false;
				oldHit = false;
				auto.SetSpeed(0f);
				feelHit.InitTime();
			}
		}
		return true;
	}

	private bool AutoOLoopAibuProc(int _state, float _wheel, HScene.AnimationListInfo _infoAnimList)
	{
		feelHit.ChangeHit(_infoAnimList.nFeelHit, 2);
		Vector2 hitArea = feelHit.GetHitArea(_infoAnimList.nFeelHit, 2);
		auto.ChangeSpeed(_loop: false, hitArea);
		auto.AddSpeed(_wheel, 2);
		ctrlFlag.speed = auto.GetSpeed(_loop: false);
		ctrlFlag.nowSpeedStateFast = ctrlFlag.speed >= 0.5f;
		ctrlFlag.isGaugeHit = GlobalMethod.RangeOn(ctrlFlag.speed, hitArea.x, hitArea.y);
		isAtariHit.Value = ctrlFlag.isGaugeHit;
		float num = 0f;
		if (_state == 1)
		{
			if (ctrlFlag.isGaugeHit)
			{
				num = Time.deltaTime * ctrlFlag.speedGuageRate;
				num *= (float)((!ctrlFlag.stopFeelFemal) ? 1 : 0);
				if (!ProcBase.hSceneManager.bMerchant)
				{
					if (ProcBase.hSceneManager.HSkil.ContainsValue(15) && ctrlFlag.nowAnimationInfo.lstSystem.Contains(3))
					{
						num *= ctrlFlag.SkilChangeSpeed(15);
					}
					if (ProcBase.hSceneManager.HSkil.ContainsValue(3))
					{
						num *= ctrlFlag.SkilChangeSpeed(3);
					}
				}
				ctrlFlag.feel_f += num;
				ctrlFlag.feel_f = Mathf.Clamp01(ctrlFlag.feel_f);
			}
		}
		else
		{
			num = Time.deltaTime * ctrlFlag.speedGuageRate;
			num *= ((!ctrlFlag.isGaugeHit) ? 0.3f : 1f) * (float)((!ctrlFlag.stopFeelFemal) ? 1 : 0);
			if (!ProcBase.hSceneManager.bMerchant && ProcBase.hSceneManager.HSkil.ContainsValue(3))
			{
				num *= ctrlFlag.SkilChangeSpeed(3);
			}
			ctrlFlag.feel_f += num;
			ctrlFlag.feel_f = Mathf.Clamp01(ctrlFlag.feel_f);
		}
		if (ctrlFlag.isGaugeHit != oldHit && ctrlFlag.isGaugeHit && !ctrlFlag.isAutoActionChange)
		{
			if (randVoicePlays[0].Get() == 0)
			{
				ctrlFlag.voice.playVoices[0] = true;
			}
			else if (randVoicePlays[1].Get() == 0)
			{
				ctrlFlag.voice.playVoices[1] = true;
			}
			if (!ctrlFlag.nowAnimationInfo.lstSystem.Contains(0))
			{
				ctrlFlag.voice.playShorts[0] = 0;
				ctrlFlag.voice.playShorts[1] = 0;
			}
		}
		oldHit = ctrlFlag.isGaugeHit;
		if (ctrlFlag.selectAnimationListInfo == null && ctrlFlag.feel_f >= 1f)
		{
			setPlay((_state != 0) ? "D_Orgasm" : "Orgasm");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			if (_state != 0)
			{
				ctrlFlag.AddParam(15, 1);
			}
			ctrlFlag.feel_f = 0f;
			ctrlFlag.isGaugeHit = false;
			isAtariHit.Value = ctrlFlag.isGaugeHit;
			ctrlFlag.voice.oldFinish = 0;
			ctrlFlag.numOrgasm = Mathf.Clamp(ctrlFlag.numOrgasm + 1, 0, 10);
			ctrlFlag.AddOrgasm();
			if (!ctrlFlag.isPainAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(4))
			{
				ctrlFlag.isPainAction = true;
			}
			if (!ctrlFlag.isConstraintAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(5))
			{
				ctrlFlag.isConstraintAction = true;
			}
			ctrlFlag.rateNip = 1f;
			if (Config.HData.Gloss)
			{
				ctrlFlag.rateTuya = 1f;
			}
			bool sio = Config.HData.Sio;
			bool urine = Config.HData.Urine;
			if (!ProcBase.hSceneManager.bMerchant)
			{
				bool flag = Hitem.Effect(5);
				if (sio)
				{
					particle.Play(0);
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone && ctrlFlag.nowAnimationInfo.nAnimListInfoID != 4)
					{
						particle.Play(4);
					}
				}
				else if (((ctrlFlag.numFaintness == 0 && ctrlFlag.numOrgasm >= ctrlFlag.gotoFaintnessCount) || flag) && ProcBase.hSceneManager.GetFlaverSkillLevel(2) >= 100)
				{
					particle.Play(0);
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone && ctrlFlag.nowAnimationInfo.nAnimListInfoID != 4)
					{
						particle.Play(4);
					}
				}
				if (urine || ProcBase.hSceneManager.Toilet >= 70f)
				{
					particle.Play(1);
					ctrlFlag.voice.urines[0] = true;
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone)
					{
						particle.Play(5);
						ctrlFlag.voice.urines[1] = true;
					}
					ctrlFlag.numUrine++;
					ProcBase.hSceneManager.Toilet = 30f;
					int desireKey = Desire.GetDesireKey(Desire.Type.Toilet);
					if (ProcBase.hSceneManager.Agent[0] != null)
					{
						ProcBase.hSceneManager.Agent[0].SetDesire(desireKey, ProcBase.hSceneManager.Toilet);
					}
					if (ProcBase.hSceneManager.Agent[1] != null)
					{
						ProcBase.hSceneManager.Agent[1].SetDesire(desireKey, ProcBase.hSceneManager.Toilet);
					}
				}
			}
			else
			{
				if (sio)
				{
					particle.Play(0);
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone && ctrlFlag.nowAnimationInfo.nAnimListInfoID != 4)
					{
						particle.Play(4);
					}
				}
				if (urine)
				{
					particle.Play(1);
					ctrlFlag.voice.urines[0] = true;
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone)
					{
						particle.Play(5);
						ctrlFlag.voice.urines[1] = true;
					}
					int desireKey2 = Desire.GetDesireKey(Desire.Type.Toilet);
					if (ProcBase.hSceneManager.Agent[0] != null)
					{
						ProcBase.hSceneManager.Agent[0].SetDesire(desireKey2, ProcBase.hSceneManager.Toilet);
					}
					if (ProcBase.hSceneManager.Agent[1] != null)
					{
						ProcBase.hSceneManager.Agent[1].SetDesire(desireKey2, ProcBase.hSceneManager.Toilet);
					}
				}
			}
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			ctrlFlag.nowOrgasm = true;
		}
		return true;
	}

	private bool AutoStartHoushiProc(int _state, bool _restart)
	{
		if (nextPlay == 0)
		{
			return false;
		}
		if (nextPlay == 1)
		{
			nextPlay = 2;
			if (!_restart)
			{
				ctrlFlag.voice.playStart = 2;
			}
			else
			{
				ctrlFlag.voice.playStart = 3;
			}
			return false;
		}
		if (nextPlay == 2)
		{
			for (int i = 0; i < 2; i++)
			{
				if (voice.nowVoices[i].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[i].state == HVoiceCtrl.VoiceKind.startVoice)
				{
					return false;
				}
			}
		}
		if (!_restart || (_restart && !auto.IsChangeActionAtRestart()))
		{
			setPlay((_state != 0) ? "D_WLoop" : "WLoop");
			if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(0))
			{
				if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1 || ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 3)
				{
					voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 1, chaFemales[0], 0);
				}
				if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1)
				{
					voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 2, chaFemales[1], 1);
				}
			}
			else if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(1))
			{
				if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1 || ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 3)
				{
					voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 2, chaFemales[0], 0);
				}
				if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1)
				{
					voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 2, chaFemales[1], 1);
				}
			}
		}
		else
		{
			ctrlFlag.isAutoActionChange = true;
		}
		nextPlay = 0;
		ctrlFlag.speed = 0f;
		ctrlFlag.loopType = 0;
		ctrlFlag.isNotCtrl = false;
		ctrlFlag.nowSpeedStateFast = false;
		oldHit = false;
		for (int j = 0; j < 2; j++)
		{
			ctrlFlag.motions[j] = 0f;
			timeMotions[j] = 0f;
			timeChangeMotions[j] = Random.Range(ctrlFlag.changeAutoMotionTimeMin, ctrlFlag.changeAutoMotionTimeMax);
			timeChangeMotionDeltaTimes[j] = 0f;
		}
		feelHit.InitTime();
		if (_restart)
		{
			ctrlMeta.Clear();
			voice.AfterFinish();
		}
		auto.Reset();
		return true;
	}

	private bool AutoLoopHoushiProc(int _loop, int _state, float _wheel, HScene.AnimationListInfo _infoAnimList)
	{
		if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishBefore)
		{
			setPlay((_state != 0) ? "D_OLoop" : "OLoop");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.nowSpeedStateFast = false;
			ctrlFlag.feel_m = 0.7f;
			oldHit = false;
			feelHit.InitTime();
			ctrlFlag.isGaugeHit = false;
		}
		else
		{
			for (int i = 0; i < 2; i++)
			{
				if (!chaFemales[i].visibleAll || chaFemales[i].objBodyBone == null)
				{
					continue;
				}
				timeChangeMotionDeltaTimes[i] += Time.deltaTime;
				if (!(timeChangeMotions[i] <= timeChangeMotionDeltaTimes[i]) || enableMotions[i])
				{
					continue;
				}
				timeChangeMotions[i] = Random.Range(ctrlFlag.changeAutoMotionTimeMin, ctrlFlag.changeAutoMotionTimeMax);
				timeChangeMotionDeltaTimes[i] = 0f;
				enableMotions[i] = true;
				timeMotions[i] = 0f;
				float num = 0f;
				if (allowMotions[i])
				{
					num = 1f - ctrlFlag.motions[i];
					num = ((!(num <= ctrlFlag.changeMotionMinRate)) ? (ctrlFlag.motions[i] + Random.Range(ctrlFlag.changeMotionMinRate, num)) : 1f);
					if (num >= 1f)
					{
						allowMotions[i] = false;
					}
				}
				else
				{
					num = ctrlFlag.motions[i];
					num = ((!(num <= ctrlFlag.changeMotionMinRate)) ? (ctrlFlag.motions[i] - Random.Range(ctrlFlag.changeMotionMinRate, num)) : 0f);
					if (num <= 0f)
					{
						allowMotions[i] = true;
					}
				}
				ref Vector2 reference = ref lerpMotions[i];
				reference = new Vector2(ctrlFlag.motions[i], num);
				lerpTimes[i] = Random.Range(ctrlFlag.changeMotionTimeMin, ctrlFlag.changeMotionTimeMax);
			}
			string playAnimation = ((_loop == 0) ? ((_state != 0) ? "D_SLoop" : "SLoop") : ((_state != 0) ? "D_WLoop" : "WLoop"));
			if (auto.ChangeLoopMotion(_loop == 1))
			{
				setPlay(playAnimation);
				feelHit.InitTime();
			}
			else
			{
				auto.ChangeSpeed(_loop == 1, new Vector2(-1f, -1f));
				if (auto.AddSpeed(_wheel, _loop))
				{
					setPlay(playAnimation);
					feelHit.InitTime();
				}
			}
			if (_loop == 0)
			{
				ctrlFlag.speed = auto.GetSpeed(_loop: false);
				ctrlFlag.nowSpeedStateFast = ctrlFlag.speed >= 0.5f;
			}
			else
			{
				ctrlFlag.speed = auto.GetSpeed(_loop: true);
				ctrlFlag.nowSpeedStateFast = ctrlFlag.speed >= 0.5f;
			}
			feelHit.ChangeHit(_infoAnimList.nFeelHit, _loop);
			ctrlFlag.isGaugeHit = feelHit.isHit(_infoAnimList.nFeelHit, _loop, ctrlFlag.speed);
			ctrlFlag.isGaugeHit_M = ctrlFlag.isGaugeHit;
			isAtariHit.Value = ctrlFlag.isGaugeHit_M;
			float num2 = 0f;
			num2 = Time.deltaTime * ctrlFlag.speedGuageRate;
			num2 *= ((!ctrlFlag.isGaugeHit) ? 1f : 2f) * (float)((!ctrlFlag.stopFeelMale) ? 1 : 0);
			if (!ProcBase.hSceneManager.bMerchant && ProcBase.hSceneManager.HSkil.ContainsValue(18))
			{
				num2 *= ctrlFlag.SkilChangeSpeed(18);
			}
			ctrlFlag.feel_m += num2;
			ctrlFlag.feel_m = Mathf.Clamp01(ctrlFlag.feel_m);
			if (ctrlFlag.selectAnimationListInfo == null)
			{
				ctrlFlag.isAutoActionChange = auto.IsChangeActionAtLoop();
			}
			if (ctrlFlag.isGaugeHit != oldHit && ctrlFlag.isGaugeHit && !ctrlFlag.isAutoActionChange)
			{
				if (randVoicePlays[0].Get() == 0)
				{
					ctrlFlag.voice.playVoices[0] = true;
				}
				else if (randVoicePlays[1].Get() == 0)
				{
					ctrlFlag.voice.playVoices[1] = true;
				}
				ctrlFlag.voice.dialog = false;
			}
			oldHit = ctrlFlag.isGaugeHit;
			if (ctrlFlag.feel_m >= 0.7f)
			{
				setPlay((_state != 0) ? "D_OLoop" : "OLoop");
				ctrlFlag.speed = 0f;
				ctrlFlag.loopType = -1;
				ctrlFlag.nowSpeedStateFast = false;
				oldHit = false;
				feelHit.InitTime();
			}
		}
		return true;
	}

	private bool AutoOLoopHoushiProc(int _state, float _wheel, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		auto.ChangeSpeed(_loop: false, new Vector2(-1f, -1f));
		auto.AddSpeed(_wheel, 2);
		ctrlFlag.speed = auto.GetSpeed(_loop: false);
		feelHit.ChangeHit(_infoAnimList.nFeelHit, 2);
		ctrlFlag.isGaugeHit = feelHit.isHit(_infoAnimList.nFeelHit, 2, ctrlFlag.speed);
		ctrlFlag.isGaugeHit_M = ctrlFlag.isGaugeHit;
		float num = 0f;
		num = Time.deltaTime * ctrlFlag.speedGuageRate;
		num *= ((!ctrlFlag.isGaugeHit) ? 1f : 2f) * (float)((!ctrlFlag.stopFeelMale) ? 1 : 0);
		if (!ProcBase.hSceneManager.bMerchant && ProcBase.hSceneManager.HSkil.ContainsValue(18))
		{
			num *= ctrlFlag.SkilChangeSpeed(18);
		}
		ctrlFlag.feel_m += num;
		ctrlFlag.feel_m = Mathf.Clamp01(ctrlFlag.feel_m);
		isAtariHit.Value = ctrlFlag.isGaugeHit_M;
		if (ctrlFlag.isGaugeHit != oldHit && ctrlFlag.isGaugeHit && !ctrlFlag.isAutoActionChange)
		{
			if (randVoicePlays[0].Get() == 0)
			{
				ctrlFlag.voice.playVoices[0] = true;
			}
			else if (randVoicePlays[1].Get() == 0)
			{
				ctrlFlag.voice.playVoices[1] = true;
			}
			ctrlFlag.voice.dialog = false;
		}
		oldHit = ctrlFlag.isGaugeHit;
		if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishOutSide || (ctrlFlag.initiative == 2 && ctrlFlag.feel_m >= 1f))
		{
			setPlay((_state != 0) ? "D_Orgasm_OUT" : "Orgasm_OUT");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.feel_m = 0f;
			ctrlFlag.isGaugeHit = false;
			ctrlFlag.isGaugeHit_M = ctrlFlag.isGaugeHit;
			isAtariHit.Value = ctrlFlag.isGaugeHit_M;
			ctrlFlag.voice.dialog = false;
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			ctrlFlag.numOutSide = Mathf.Clamp(ctrlFlag.numOutSide + 1, 0, 999999);
			ctrlFlag.isHoushiFinish = true;
			ctrlFlag.nowOrgasm = true;
			if (!ctrlFlag.isPainAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(4))
			{
				ctrlFlag.isPainAction = true;
			}
			if (!ctrlFlag.isConstraintAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(5))
			{
				ctrlFlag.isConstraintAction = true;
			}
			ctrlFlag.voice.oldFinish = 2;
		}
		else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishDrink && _modeCtrl != 1)
		{
			setPlay("Orgasm_IN");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.feel_m = 0f;
			ctrlFlag.isGaugeHit = false;
			ctrlFlag.isGaugeHit_M = ctrlFlag.isGaugeHit;
			isAtariHit.Value = ctrlFlag.isGaugeHit_M;
			ctrlFlag.voice.dialog = false;
			finishMotion = 0;
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			ctrlFlag.isHoushiFinish = true;
			ctrlFlag.nowOrgasm = true;
			ctrlFlag.voice.oldFinish = 1;
		}
		else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishVomit && _modeCtrl != 1)
		{
			setPlay("Orgasm_IN");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.feel_m = 0f;
			ctrlFlag.isGaugeHit = false;
			ctrlFlag.isGaugeHit_M = ctrlFlag.isGaugeHit;
			isAtariHit.Value = ctrlFlag.isGaugeHit_M;
			ctrlFlag.voice.dialog = false;
			finishMotion = 1;
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			ctrlFlag.isHoushiFinish = true;
			ctrlFlag.nowOrgasm = true;
			ctrlFlag.voice.oldFinish = 1;
		}
		return true;
	}

	private bool AutoStartSonyuProc(bool _restart, int _state, int _modeCtrl)
	{
		if (nextPlay == 0)
		{
			return false;
		}
		if (nextPlay == 1)
		{
			nextPlay = 2;
			if (!_restart)
			{
				ctrlFlag.voice.playStart = 2;
			}
			else
			{
				ctrlFlag.voice.playStart = 3;
			}
			return false;
		}
		if (nextPlay == 2)
		{
			for (int i = 0; i < 2; i++)
			{
				if (voice.nowVoices[i].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[i].state == HVoiceCtrl.VoiceKind.startVoice)
				{
					return false;
				}
			}
		}
		nextPlay = 0;
		if (!_restart || (_restart && !auto.IsChangeActionAtRestart()))
		{
			if (_modeCtrl == 3)
			{
				setPlay((_state != 0) ? "D_Insert" : "Insert");
				ctrlFlag.speed = 0f;
				ctrlFlag.loopType = -1;
			}
			else
			{
				setPlay((_state != 0) ? "D_WLoop" : "WLoop");
				if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(0))
				{
					if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1 || ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 3)
					{
						voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 1, chaFemales[0], 0);
					}
					if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1)
					{
						voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 2, chaFemales[1], 1);
					}
				}
				else if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(1))
				{
					if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1 || ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 3)
					{
						voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 2, chaFemales[0], 0);
					}
					if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1)
					{
						voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 2, chaFemales[1], 1);
					}
				}
				ctrlFlag.speed = 0f;
				ctrlFlag.loopType = 0;
			}
		}
		else
		{
			ctrlFlag.isAutoActionChange = true;
		}
		ctrlFlag.speed = 0f;
		ctrlFlag.nowSpeedStateFast = false;
		ctrlFlag.motions[0] = 0f;
		ctrlFlag.motions[1] = 0f;
		timeMotions[0] = 0f;
		timeMotions[1] = 0f;
		oldHit = false;
		if (_state == 0)
		{
			for (int j = 0; j < 2; j++)
			{
				timeChangeMotions[j] = Random.Range(ctrlFlag.changeAutoMotionTimeMin, ctrlFlag.changeAutoMotionTimeMax);
				timeChangeMotionDeltaTimes[j] = 0f;
			}
		}
		ctrlFlag.isNotCtrl = false;
		auto.Reset();
		feelHit.InitTime();
		if (_restart)
		{
			ctrlMeta.Clear();
			voice.AfterFinish();
		}
		return true;
	}

	private bool AutoLoopSonyuProc(int _loop, int _state, float _wheel, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		float num = 0f;
		if (((ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishInSide && ctrlFlag.feel_m >= 0.7f) || (ctrlFlag.initiative == 2 && ctrlFlag.feel_m >= 1f)) && _modeCtrl == 3)
		{
			string[] array = new string[2] { "OrgasmM_IN", "D_OrgasmM_IN" };
			setPlay(array[_state]);
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.feel_m = 0f;
			if (ctrlFlag.feel_f > 0.5f)
			{
				ctrlFlag.feel_f = 0.5f;
			}
			ctrlFlag.isInsert = true;
			ctrlFlag.isGaugeHit = false;
			isAtariHit.Value = ctrlFlag.isGaugeHit;
			ctrlFlag.numInside = Mathf.Clamp(ctrlFlag.numInside + 1, 0, 999999);
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			ctrlFlag.voice.oldFinish = 1;
			ctrlFlag.nowOrgasm = true;
		}
		else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishOutSide && ctrlFlag.feel_m >= 0.7f && (_modeCtrl != 4 || (_modeCtrl == 4 && _state == 0)))
		{
			string[] array2 = new string[2] { "OrgasmM_OUT", "D_OrgasmM_OUT" };
			setPlay(array2[_state]);
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.feel_m = 0f;
			if (ctrlFlag.feel_f > 0.5f)
			{
				ctrlFlag.feel_f = 0.5f;
			}
			ctrlFlag.isGaugeHit = false;
			isAtariHit.Value = ctrlFlag.isGaugeHit;
			ctrlFlag.numOutSide = Mathf.Clamp(ctrlFlag.numOutSide + 1, 0, 999999);
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			ctrlFlag.voice.oldFinish = 2;
			ctrlFlag.isGaugeHit = false;
			ctrlFlag.nowOrgasm = true;
		}
		else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishBefore)
		{
			string[] array3 = new string[2] { "OLoop", "D_OLoop" };
			setPlay(array3[_state]);
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.nowSpeedStateFast = false;
			ctrlFlag.feel_f = 0.7f;
			if (ctrlFlag.feel_m <= 0.7f)
			{
				ctrlFlag.feel_m = 0.7f;
			}
			oldHit = false;
			feelHit.InitTime();
		}
		else
		{
			for (int i = 0; i < 2; i++)
			{
				if (!chaFemales[i].visibleAll || chaFemales[i].objBodyBone == null)
				{
					continue;
				}
				timeChangeMotionDeltaTimes[i] += Time.deltaTime;
				if (!(timeChangeMotions[i] <= timeChangeMotionDeltaTimes[i]) || enableMotions[i] || _state != 0)
				{
					continue;
				}
				timeChangeMotions[i] = Random.Range(ctrlFlag.changeAutoMotionTimeMin, ctrlFlag.changeAutoMotionTimeMax);
				timeChangeMotionDeltaTimes[i] = 0f;
				enableMotions[i] = true;
				timeMotions[i] = 0f;
				float num2 = 0f;
				if (allowMotions[i])
				{
					num2 = 1f - ctrlFlag.motions[i];
					num2 = ((!(num2 <= ctrlFlag.changeMotionMinRate)) ? (ctrlFlag.motions[i] + Random.Range(ctrlFlag.changeMotionMinRate, num2)) : 1f);
					if (num2 >= 1f)
					{
						allowMotions[i] = false;
					}
				}
				else
				{
					num2 = ctrlFlag.motions[i];
					num2 = ((!(num2 <= ctrlFlag.changeMotionMinRate)) ? (ctrlFlag.motions[i] - Random.Range(ctrlFlag.changeMotionMinRate, num2)) : 0f);
					if (num2 <= 0f)
					{
						allowMotions[i] = true;
					}
				}
				ref Vector2 reference = ref lerpMotions[i];
				reference = new Vector2(ctrlFlag.motions[i], num2);
				lerpTimes[i] = Random.Range(ctrlFlag.changeMotionTimeMin, ctrlFlag.changeMotionTimeMax);
			}
			feelHit.ChangeHit(_infoAnimList.nFeelHit, _loop);
			Vector2 hitArea = feelHit.GetHitArea(_infoAnimList.nFeelHit, _loop);
			if (auto.ChangeLoopMotion(_loop == 1))
			{
				setPlay((_loop == 0) ? ((_state != 0) ? "D_SLoop" : "SLoop") : ((_state != 0) ? "D_WLoop" : "WLoop"));
				for (int j = 0; j < 2; j++)
				{
					if (chaFemales[j].visibleAll && !(chaFemales[j].objTop == null) && (voice.nowVoices[j].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[j].state == HVoiceCtrl.VoiceKind.startVoice))
					{
						Singleton<Voice>.Instance.Stop(ctrlFlag.voice.voiceTrs[j]);
					}
				}
				feelHit.InitTime();
			}
			else
			{
				auto.ChangeSpeed(_loop == 1, hitArea);
				if (auto.AddSpeed(_wheel, _loop))
				{
					setPlay((_loop == 0) ? ((_state != 0) ? "D_SLoop" : "SLoop") : ((_state != 0) ? "D_WLoop" : "WLoop"));
					for (int k = 0; k < 2; k++)
					{
						if (chaFemales[k].visibleAll && !(chaFemales[k].objTop == null) && (voice.nowVoices[k].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[k].state == HVoiceCtrl.VoiceKind.startVoice))
						{
							Singleton<Voice>.Instance.Stop(ctrlFlag.voice.voiceTrs[k]);
						}
					}
					feelHit.InitTime();
				}
			}
			if (_loop == 0)
			{
				ctrlFlag.speed = auto.GetSpeed(_loop: false);
				ctrlFlag.nowSpeedStateFast = ctrlFlag.speed >= 0.5f;
			}
			else
			{
				ctrlFlag.speed = auto.GetSpeed(_loop: true);
				ctrlFlag.nowSpeedStateFast = ctrlFlag.speed >= 0.5f;
			}
			ctrlFlag.isGaugeHit = GlobalMethod.RangeOn(ctrlFlag.speed, hitArea.x, hitArea.y);
			if (_state == 1)
			{
				if (ctrlFlag.isGaugeHit)
				{
					num = Time.deltaTime * ctrlFlag.speedGuageRate;
					ctrlFlag.feel_f *= ((!ctrlFlag.stopFeelFemal) ? 1 : 0);
					if (!ProcBase.hSceneManager.bMerchant)
					{
						if (ProcBase.hSceneManager.HSkil.ContainsValue(15) && ctrlFlag.nowAnimationInfo.lstSystem.Contains(3))
						{
							num *= ctrlFlag.SkilChangeSpeed(15);
						}
						if (ProcBase.hSceneManager.HSkil.ContainsValue(3))
						{
							num *= ctrlFlag.SkilChangeSpeed(3);
						}
					}
					ctrlFlag.feel_f += num;
					ctrlFlag.feel_f = Mathf.Clamp01(ctrlFlag.feel_f);
				}
			}
			else
			{
				num = Time.deltaTime * ctrlFlag.speedGuageRate;
				num *= ((!ctrlFlag.isGaugeHit) ? 0.3f : 1f) * (float)((!ctrlFlag.stopFeelFemal) ? 1 : 0);
				if (!ProcBase.hSceneManager.bMerchant)
				{
					if (ProcBase.hSceneManager.HSkil.ContainsValue(15) && ctrlFlag.nowAnimationInfo.lstSystem.Contains(3))
					{
						num *= ctrlFlag.SkilChangeSpeed(15);
					}
					if (ProcBase.hSceneManager.HSkil.ContainsValue(3))
					{
						num *= ctrlFlag.SkilChangeSpeed(3);
					}
				}
				ctrlFlag.feel_f += num;
				ctrlFlag.feel_f = Mathf.Clamp01(ctrlFlag.feel_f);
			}
			float num3 = 0f;
			if (_state != 1 || _modeCtrl != 4)
			{
				num3 = Time.deltaTime * ctrlFlag.speedGuageRate;
				num3 *= (float)((!ctrlFlag.stopFeelMale) ? 1 : 0);
				if (!ProcBase.hSceneManager.bMerchant && ProcBase.hSceneManager.HSkil.ContainsValue(1))
				{
					num3 *= ctrlFlag.SkilChangeSpeed(1);
				}
				ctrlFlag.feel_m += num3;
				ctrlFlag.feel_m = Mathf.Clamp01(ctrlFlag.feel_m);
			}
			if (ctrlFlag.selectAnimationListInfo == null)
			{
				ctrlFlag.isAutoActionChange = auto.IsChangeActionAtLoop();
			}
			if (ctrlFlag.isGaugeHit != oldHit && ctrlFlag.isGaugeHit && !ctrlFlag.isAutoActionChange)
			{
				if (randVoicePlays[0].Get() == 0)
				{
					ctrlFlag.voice.playVoices[0] = true;
				}
				else if (randVoicePlays[1].Get() == 0)
				{
					ctrlFlag.voice.playVoices[1] = true;
				}
				if (!ctrlFlag.nowAnimationInfo.lstSystem.Contains(0) && _modeCtrl != 4)
				{
					ctrlFlag.voice.playShorts[0] = 0;
					ctrlFlag.voice.playShorts[1] = 0;
				}
			}
			oldHit = ctrlFlag.isGaugeHit;
			if (ctrlFlag.feel_f >= 0.7f)
			{
				setPlay((_state != 0) ? "D_OLoop" : "OLoop");
				ctrlFlag.speed = 0f;
				ctrlFlag.loopType = -1;
				ctrlFlag.nowSpeedStateFast = false;
				oldHit = false;
				feelHit.InitTime();
			}
		}
		return true;
	}

	private bool AutoOLoopProc(int _state, float _wheel, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		float num = 0f;
		if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishBefore)
		{
			if (ctrlFlag.feel_m <= 0.7f)
			{
				ctrlFlag.feel_m = 0.7f;
				ctrlFlag.isGaugeHit = false;
			}
			return true;
		}
		feelHit.ChangeHit(_infoAnimList.nFeelHit, 2);
		Vector2 hitArea = feelHit.GetHitArea(_infoAnimList.nFeelHit, 2);
		auto.ChangeSpeed(_loop: false, hitArea);
		auto.AddSpeed(_wheel, 2);
		ctrlFlag.speed = auto.GetSpeed(_loop: false);
		ctrlFlag.nowSpeedStateFast = ctrlFlag.speed >= 0.5f;
		ctrlFlag.isGaugeHit = GlobalMethod.RangeOn(ctrlFlag.speed, hitArea.x, hitArea.y);
		if (_state == 1)
		{
			if (ctrlFlag.isGaugeHit)
			{
				num = Time.deltaTime * ctrlFlag.speedGuageRate;
				num *= (float)((!ctrlFlag.stopFeelFemal) ? 1 : 0);
				if (!ProcBase.hSceneManager.bMerchant)
				{
					if (ProcBase.hSceneManager.HSkil.ContainsValue(15) && ctrlFlag.nowAnimationInfo.lstSystem.Contains(3))
					{
						num *= ctrlFlag.SkilChangeSpeed(15);
					}
					if (ProcBase.hSceneManager.HSkil.ContainsValue(3))
					{
						num *= ctrlFlag.SkilChangeSpeed(3);
					}
				}
				ctrlFlag.feel_f += num;
				ctrlFlag.feel_f = Mathf.Clamp01(ctrlFlag.feel_f);
			}
		}
		else
		{
			num = Time.deltaTime * ctrlFlag.speedGuageRate;
			num *= ((!ctrlFlag.isGaugeHit) ? 0.3f : 1f) * (float)((!ctrlFlag.stopFeelFemal) ? 1 : 0);
			if (!ProcBase.hSceneManager.bMerchant)
			{
				if (ProcBase.hSceneManager.HSkil.ContainsValue(15) && ctrlFlag.nowAnimationInfo.lstSystem.Contains(3))
				{
					num *= ctrlFlag.SkilChangeSpeed(15);
				}
				if (ProcBase.hSceneManager.HSkil.ContainsValue(3))
				{
					num *= ctrlFlag.SkilChangeSpeed(3);
				}
			}
			ctrlFlag.feel_f += num;
			ctrlFlag.feel_f = Mathf.Clamp01(ctrlFlag.feel_f);
		}
		float num2 = 0f;
		if (_state != 1 || _modeCtrl != 4)
		{
			num2 = Time.deltaTime * ctrlFlag.speedGuageRate;
			num2 *= (float)((!ctrlFlag.stopFeelMale) ? 1 : 0);
			if (!ProcBase.hSceneManager.bMerchant && ProcBase.hSceneManager.HSkil.ContainsValue(1))
			{
				num2 *= ctrlFlag.SkilChangeSpeed(1);
			}
			ctrlFlag.feel_m += num2;
			ctrlFlag.feel_m = Mathf.Clamp01(ctrlFlag.feel_m);
		}
		if (ctrlFlag.isGaugeHit != oldHit && ctrlFlag.isGaugeHit && !ctrlFlag.isAutoActionChange)
		{
			if (randVoicePlays[0].Get() == 0)
			{
				ctrlFlag.voice.playVoices[0] = true;
			}
			else if (randVoicePlays[1].Get() == 0)
			{
				ctrlFlag.voice.playVoices[1] = true;
			}
			if (!ctrlFlag.nowAnimationInfo.lstSystem.Contains(0) && _modeCtrl != 4)
			{
				ctrlFlag.voice.playShorts[0] = 0;
				ctrlFlag.voice.playShorts[1] = 0;
			}
		}
		oldHit = ctrlFlag.isGaugeHit;
		if (ctrlFlag.selectAnimationListInfo == null && ctrlFlag.feel_f >= 1f)
		{
			setPlay((_state != 0) ? "D_OrgasmF_IN" : "OrgasmF_IN");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.feel_f = 0f;
			ctrlFlag.isGaugeHit = false;
			isAtariHit.Value = ctrlFlag.isGaugeHit;
			ctrlFlag.numOrgasm = Mathf.Clamp(ctrlFlag.numOrgasm + 1, 0, 10);
			ctrlFlag.AddOrgasm();
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			ctrlFlag.voice.oldFinish = 0;
			ctrlFlag.nowOrgasm = true;
			if (!ctrlFlag.isPainAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(4))
			{
				ctrlFlag.isPainAction = true;
			}
			if (!ctrlFlag.isConstraintAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(5))
			{
				ctrlFlag.isConstraintAction = true;
			}
			ctrlFlag.rateNip = 1f;
			if (Config.HData.Gloss)
			{
				ctrlFlag.rateTuya = 1f;
			}
			bool sio = Config.HData.Sio;
			bool urine = Config.HData.Urine;
			if (!ProcBase.hSceneManager.bMerchant)
			{
				bool flag = Hitem.Effect(5);
				if (sio)
				{
					particle.Play(0);
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone && ctrlFlag.nowAnimationInfo.nAnimListInfoID != 4)
					{
						particle.Play(4);
					}
				}
				else if (((ctrlFlag.numFaintness == 0 && ctrlFlag.numOrgasm >= ctrlFlag.gotoFaintnessCount) || flag) && ProcBase.hSceneManager.GetFlaverSkillLevel(2) >= 100)
				{
					particle.Play(0);
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone && ctrlFlag.nowAnimationInfo.nAnimListInfoID != 4)
					{
						particle.Play(4);
					}
				}
				if (urine || ProcBase.hSceneManager.Toilet >= 70f)
				{
					particle.Play(1);
					ctrlFlag.voice.urines[0] = true;
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone)
					{
						particle.Play(5);
						ctrlFlag.voice.urines[1] = true;
					}
					ctrlFlag.numUrine++;
					ProcBase.hSceneManager.Toilet = 30f;
					int desireKey = Desire.GetDesireKey(Desire.Type.Toilet);
					if (ProcBase.hSceneManager.Agent[0] != null)
					{
						ProcBase.hSceneManager.Agent[0].SetDesire(desireKey, ProcBase.hSceneManager.Toilet);
					}
					if (ProcBase.hSceneManager.Agent[1] != null)
					{
						ProcBase.hSceneManager.Agent[1].SetDesire(desireKey, ProcBase.hSceneManager.Toilet);
					}
				}
			}
			else
			{
				if (sio)
				{
					particle.Play(0);
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone && ctrlFlag.nowAnimationInfo.nAnimListInfoID != 4)
					{
						particle.Play(4);
					}
				}
				if (urine)
				{
					particle.Play(1);
					ctrlFlag.voice.urines[0] = true;
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone)
					{
						particle.Play(5);
						ctrlFlag.voice.urines[1] = true;
					}
					int desireKey2 = Desire.GetDesireKey(Desire.Type.Toilet);
					if (ProcBase.hSceneManager.Agent[0] != null)
					{
						ProcBase.hSceneManager.Agent[0].SetDesire(desireKey2, ProcBase.hSceneManager.Toilet);
					}
					if (ProcBase.hSceneManager.Agent[1] != null)
					{
						ProcBase.hSceneManager.Agent[1].SetDesire(desireKey2, ProcBase.hSceneManager.Toilet);
					}
				}
			}
		}
		else if (((ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishInSide && ctrlFlag.feel_m >= 0.7f) || (ctrlFlag.initiative == 2 && ctrlFlag.feel_m >= 1f)) && _modeCtrl == 3)
		{
			setPlay((_state != 0) ? "D_OrgasmM_IN" : "OrgasmM_IN");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.feel_m = 0f;
			if (ctrlFlag.feel_f > 0.5f)
			{
				ctrlFlag.feel_f = 0.5f;
			}
			ctrlFlag.isInsert = true;
			ctrlFlag.isGaugeHit = false;
			isAtariHit.Value = ctrlFlag.isGaugeHit;
			ctrlFlag.numInside = Mathf.Clamp(ctrlFlag.numInside + 1, 0, 999999);
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			ctrlFlag.voice.oldFinish = 1;
			ctrlFlag.nowOrgasm = true;
			if (!ctrlFlag.isPainAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(4))
			{
				ctrlFlag.isPainAction = true;
			}
			if (!ctrlFlag.isConstraintAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(5))
			{
				ctrlFlag.isConstraintAction = true;
			}
		}
		else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishOutSide && ctrlFlag.feel_m >= 0.7f && (_modeCtrl != 4 || (_modeCtrl == 4 && _state == 0)))
		{
			setPlay((_state != 0) ? "D_OrgasmM_OUT" : "OrgasmM_OUT");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.feel_m = 0f;
			if (ctrlFlag.feel_f > 0.5f)
			{
				ctrlFlag.feel_f = 0.5f;
			}
			ctrlFlag.isGaugeHit = false;
			isAtariHit.Value = ctrlFlag.isGaugeHit;
			ctrlFlag.numOutSide = Mathf.Clamp(ctrlFlag.numOutSide + 1, 0, 999999);
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			ctrlFlag.voice.oldFinish = 2;
			ctrlFlag.nowOrgasm = true;
			if (!ctrlFlag.isPainAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(4))
			{
				ctrlFlag.isPainAction = true;
			}
			if (!ctrlFlag.isConstraintAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(5))
			{
				ctrlFlag.isConstraintAction = true;
			}
		}
		else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishSame && ctrlFlag.feel_m >= 0.7f && (_modeCtrl != 4 || (_modeCtrl == 4 && _state == 0)))
		{
			setPlay((_state != 0) ? "D_OrgasmS_IN" : "OrgasmS_IN");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.feel_m = 0f;
			ctrlFlag.feel_f = 0f;
			ctrlFlag.isInsert = true;
			ctrlFlag.isGaugeHit = false;
			isAtariHit.Value = ctrlFlag.isGaugeHit;
			ctrlFlag.numOrgasm = Mathf.Clamp(ctrlFlag.numOrgasm + 1, 0, 10);
			ctrlFlag.AddOrgasm();
			ctrlFlag.numSameOrgasm = Mathf.Clamp(ctrlFlag.numSameOrgasm + 1, 0, 999999);
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			ctrlFlag.nowOrgasm = true;
			if (!ctrlFlag.isPainAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(4))
			{
				ctrlFlag.isPainAction = true;
			}
			if (!ctrlFlag.isConstraintAction && ctrlFlag.nowAnimationInfo.lstSystem.Contains(5))
			{
				ctrlFlag.isConstraintAction = true;
			}
			if (_modeCtrl == 3)
			{
				ctrlFlag.numInside = Mathf.Clamp(ctrlFlag.numInside + 1, 0, 999999);
			}
			else
			{
				ctrlFlag.numOutSide = Mathf.Clamp(ctrlFlag.numOutSide + 1, 0, 999999);
			}
			ctrlFlag.voice.oldFinish = 3;
			bool sio2 = Config.HData.Sio;
			bool urine2 = Config.HData.Urine;
			if (!ProcBase.hSceneManager.bMerchant)
			{
				bool flag2 = Hitem.Effect(5);
				if (sio2)
				{
					particle.Play(0);
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone && ctrlFlag.nowAnimationInfo.nAnimListInfoID != 4)
					{
						particle.Play(4);
					}
				}
				else if (((ctrlFlag.numFaintness == 0 && ctrlFlag.numOrgasm >= ctrlFlag.gotoFaintnessCount) || flag2) && ProcBase.hSceneManager.GetFlaverSkillLevel(2) >= 100)
				{
					particle.Play(0);
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone && ctrlFlag.nowAnimationInfo.nAnimListInfoID != 4)
					{
						particle.Play(4);
					}
				}
				if (urine2 || ProcBase.hSceneManager.Toilet >= 70f)
				{
					particle.Play(1);
					ctrlFlag.voice.urines[0] = true;
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone)
					{
						particle.Play(5);
						ctrlFlag.voice.urines[1] = true;
					}
					ctrlFlag.numUrine++;
					ProcBase.hSceneManager.Toilet = 30f;
					int desireKey3 = Desire.GetDesireKey(Desire.Type.Toilet);
					if (ProcBase.hSceneManager.Agent[0] != null)
					{
						ProcBase.hSceneManager.Agent[0].SetDesire(desireKey3, ProcBase.hSceneManager.Toilet);
					}
					if (ProcBase.hSceneManager.Agent[1] != null)
					{
						ProcBase.hSceneManager.Agent[1].SetDesire(desireKey3, ProcBase.hSceneManager.Toilet);
					}
				}
			}
			else
			{
				if (sio2)
				{
					particle.Play(0);
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone && ctrlFlag.nowAnimationInfo.nAnimListInfoID != 4)
					{
						particle.Play(4);
					}
				}
				if (urine2)
				{
					particle.Play(1);
					ctrlFlag.voice.urines[0] = true;
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone)
					{
						particle.Play(5);
						ctrlFlag.voice.urines[1] = true;
					}
					int desireKey4 = Desire.GetDesireKey(Desire.Type.Toilet);
					if (ProcBase.hSceneManager.Agent[0] != null)
					{
						ProcBase.hSceneManager.Agent[0].SetDesire(desireKey4, ProcBase.hSceneManager.Toilet);
					}
					if (ProcBase.hSceneManager.Agent[1] != null)
					{
						ProcBase.hSceneManager.Agent[1].SetDesire(desireKey4, ProcBase.hSceneManager.Toilet);
					}
				}
			}
		}
		return true;
	}

	private bool AutoAfterTheInsideWaitingProc(int _state, float _wheel)
	{
		if (auto.IsPull(ctrlFlag.isInsert))
		{
			setPlay((_state != 0) ? "D_Pull" : "Pull");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.nowSpeedStateFast = false;
			ctrlFlag.motions[0] = 0f;
			ctrlFlag.motions[1] = 0f;
			timeMotions[0] = 0f;
			timeMotions[1] = 0f;
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			ctrlFlag.nowOrgasm = true;
			voice.AfterFinish();
			oldHit = false;
			feelHit.InitTime();
			auto.ReStartInit();
			return true;
		}
		if (!auto.IsReStart())
		{
			return false;
		}
		setPlay((_state != 0) ? "D_WLoop" : "WLoop");
		if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(0))
		{
			if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1 || ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 3)
			{
				voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 1, chaFemales[0], 0);
			}
			if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1)
			{
				voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 2, chaFemales[1], 1);
			}
		}
		else if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(1))
		{
			if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1 || ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 3)
			{
				voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 2, chaFemales[0], 0);
			}
			if (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1)
			{
				voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 2, chaFemales[1], 1);
			}
		}
		ctrlFlag.speed = 0f;
		ctrlFlag.loopType = 0;
		ctrlFlag.nowSpeedStateFast = false;
		ctrlFlag.motions[0] = 0f;
		ctrlFlag.motions[1] = 0f;
		timeMotions[0] = 0f;
		timeMotions[1] = 0f;
		oldHit = false;
		if (_state == 0)
		{
			for (int i = 0; i < 2; i++)
			{
				timeChangeMotions[i] = Random.Range(ctrlFlag.changeAutoMotionTimeMin, ctrlFlag.changeAutoMotionTimeMax);
				timeChangeMotionDeltaTimes[i] = 0f;
			}
		}
		voice.AfterFinish();
		auto.ReStartInit();
		auto.PullInit();
		ctrlMeta.Clear();
		return true;
	}
}
