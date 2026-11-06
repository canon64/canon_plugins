using System.Collections.Generic;
using Manager;
using UnityEngine;

public class Houshi : ProcBase
{
	private float timeMotion;

	private bool enableMotion;

	private bool allowMotion = true;

	private Vector2 lerpMotion = Vector2.zero;

	private float lerpTime;

	private int finishMotion;

	private List<HScene.AnimationListInfo> lstAnimation;

	private int nextPlay;

	private bool oldHit;

	private animParm animPar;

	public Houshi(DeliveryMember _delivery)
		: base(_delivery)
	{
		animPar.heights = new float[1];
		animPar.m = new float[1];
		CatID = 1;
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
			setPlay((!ctrlFlag.isFaintness) ? "Idle" : "D_Idle", _isFade: false);
			ctrlFlag.loopType = -1;
			voice.HouchiTime = 0f;
		}
		else
		{
			if (ctrlFlag.feel_m >= 0.7f)
			{
				setPlay((!ctrlFlag.isFaintness) ? "OLoop" : "D_OLoop", _isFade: false);
				ctrlFlag.loopType = -1;
			}
			else
			{
				setPlay((!ctrlFlag.isFaintness) ? "WLoop" : "D_WLoop", _isFade: false);
				if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(0))
				{
					voice.PlaySoundETC((!ctrlFlag.isFaintness) ? "WLoop" : "D_WLoop", 1, chaFemales[0], 0);
				}
				else if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(1))
				{
					voice.PlaySoundETC((!ctrlFlag.isFaintness) ? "WLoop" : "D_WLoop", 2, chaFemales[0], 0);
				}
				ctrlFlag.loopType = 0;
			}
			ctrlFlag.speed = 0f;
			if (_infoAnimList.lstSystem.Contains(4))
			{
				ctrlFlag.isPainActionParam = true;
			}
			ctrlFlag.motions[0] = 0f;
			ctrlFlag.nowSpeedStateFast = false;
			auto.SetSpeed(0f);
		}
		ctrlMeta.SetParameterFromState(ctrlFlag.isFaintness ? 1 : 0);
		nextPlay = 0;
		oldHit = false;
		return true;
	}

	public override bool Proc(int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		if (chaMales[0].objTop == null || chaFemales[0].objTop == null)
		{
			return false;
		}
		FemaleAi = chaFemales[0].getAnimatorStateInfo(0);
		if (ctrlFlag.initiative == 0)
		{
			Manual(FemaleAi, _modeCtrl, _infoAnimList);
		}
		else
		{
			Auto(FemaleAi, _modeCtrl, _infoAnimList);
		}
		if (enableMotion)
		{
			timeMotion = Mathf.Clamp(timeMotion + Time.deltaTime, 0f, lerpTime);
			float time = Mathf.Clamp01(timeMotion / lerpTime);
			time = ctrlFlag.changeMotionCurve.Evaluate(time);
			ctrlFlag.motions[0] = Mathf.Lerp(lerpMotion.x, lerpMotion.y, time);
			if (time >= 1f)
			{
				enableMotion = false;
			}
		}
		ctrlMeta.Proc(FemaleAi);
		if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.RecoverFaintness)
		{
			if (FemaleAi.IsName("D_Idle") || FemaleAi.IsName("D_WLoop") || FemaleAi.IsName("D_SLoop") || FemaleAi.IsName("D_OLoop") || FemaleAi.IsName("D_Orgasm_OUT_A"))
			{
				ctrlFlag.selectAnimationListInfo = RecoverFaintnessAi();
				if (ctrlFlag.nowAnimationInfo == ctrlFlag.selectAnimationListInfo)
				{
					setPlay("Orgasm_OUT_A");
				}
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
		SetFinishCategoryEnable(FemaleAi.IsName("OLoop") || FemaleAi.IsName("D_OLoop"));
		bool flag = FemaleAi.IsName("WLoop") || FemaleAi.IsName("SLoop") || FemaleAi.IsName("D_WLoop") || FemaleAi.IsName("D_SLoop");
		flag &= Config.HData.FinishButton;
		sprite.categoryFinish.SetActive(flag, 3);
		setAnimationParamater();
		return true;
	}

	private bool Manual(AnimatorStateInfo _ai, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		float num = UnityEngine.Input.GetAxis("Mouse ScrollWheel") * (float)((!sprite.IsSpriteOver()) ? 1 : 0);
		num = ((num < 0f) ? (0f - ctrlFlag.wheelActionCount) : ((!(num > 0f)) ? 0f : ctrlFlag.wheelActionCount));
		if (_ai.IsName("Idle"))
		{
			StartProcTrigger(num);
			StartProc(0, _restart: false);
			voice.HouchiTime += Time.unscaledDeltaTime;
		}
		else if (_ai.IsName("WLoop"))
		{
			LoopProc(0, 0, num, _infoAnimList);
		}
		else if (_ai.IsName("SLoop"))
		{
			LoopProc(1, 0, num, _infoAnimList);
		}
		else if (_ai.IsName("OLoop"))
		{
			OLoopProc(0, num, _modeCtrl, _infoAnimList);
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
			StartProc(0, _restart: true);
		}
		else if (_ai.IsName("Drink_A"))
		{
			StartProcTrigger(num);
			StartProc(0, _restart: true);
		}
		else if (_ai.IsName("Vomit_A"))
		{
			StartProcTrigger(num);
			StartProc(0, _restart: true);
		}
		else if (_ai.IsName("D_Idle"))
		{
			StartProcTrigger(num);
			StartProc(1, _restart: false);
			voice.HouchiTime += Time.unscaledDeltaTime;
		}
		else if (_ai.IsName("D_WLoop"))
		{
			LoopProc(0, 1, num, _infoAnimList);
		}
		else if (_ai.IsName("D_SLoop"))
		{
			LoopProc(1, 1, num, _infoAnimList);
		}
		else if (_ai.IsName("D_OLoop"))
		{
			OLoopProc(1, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("D_Orgasm_OUT"))
		{
			SetNextFinishAnimation(_ai.normalizedTime, "D_Orgasm_OUT_A");
		}
		else if (_ai.IsName("D_Orgasm_OUT_A"))
		{
			StartProcTrigger(num);
			StartProc(1, _restart: true);
		}
		return true;
	}

	private bool Auto(AnimatorStateInfo _ai, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		float num = UnityEngine.Input.GetAxis("Mouse ScrollWheel") * (float)((!sprite.IsSpriteOver()) ? 1 : 0);
		num = ((num < 0f) ? (0f - ctrlFlag.wheelActionCount) : ((!(num > 0f)) ? 0f : ctrlFlag.wheelActionCount));
		if (_ai.IsName("Idle"))
		{
			AutoStartProcTrigger(_start: false);
			AutoStartProc(0, _restart: false);
		}
		else if (_ai.IsName("WLoop"))
		{
			AutoLoopProc(0, 0, num, _infoAnimList);
		}
		else if (_ai.IsName("SLoop"))
		{
			AutoLoopProc(1, 0, num, _infoAnimList);
		}
		else if (_ai.IsName("OLoop"))
		{
			AutoOLoopProc(0, num, _modeCtrl, _infoAnimList);
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
			AutoStartProc(0, _restart: true);
		}
		else if (_ai.IsName("Drink_A"))
		{
			AutoStartProcTrigger(_start: true);
			AutoStartProc(0, _restart: true);
		}
		else if (_ai.IsName("Vomit_A"))
		{
			AutoStartProcTrigger(_start: true);
			AutoStartProc(0, _restart: true);
		}
		else if (_ai.IsName("D_Idle"))
		{
			StartProcTrigger(num);
			StartProc(1, _restart: false);
		}
		else if (_ai.IsName("D_WLoop"))
		{
			LoopProc(0, 1, num, _infoAnimList);
		}
		else if (_ai.IsName("D_SLoop"))
		{
			LoopProc(1, 1, num, _infoAnimList);
		}
		else if (_ai.IsName("D_OLoop"))
		{
			OLoopProc(1, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("D_Orgasm_OUT"))
		{
			SetNextFinishAnimation(_ai.normalizedTime, "D_Orgasm_OUT_A");
		}
		else if (_ai.IsName("D_Orgasm_OUT_A"))
		{
			StartProcTrigger(num);
			StartProc(1, _restart: true);
		}
		return true;
	}

	public override void setAnimationParamater()
	{
		animPar.breast = chaFemales[0].GetShapeBodyValue(1);
		animPar.speed = ((ctrlFlag.loopType == 1) ? ctrlFlag.speed : (ctrlFlag.speed + 1f));
		animPar.m[0] = ctrlFlag.motions[0];
		if (chaFemales[0].visibleAll && chaFemales[0].objBodyBone != null)
		{
			animPar.heights[0] = chaFemales[0].GetShapeBodyValue(0);
			chaFemales[0].setAnimatorParamFloat("height", animPar.heights[0]);
			chaFemales[0].setAnimatorParamFloat("speed", animPar.speed);
			chaFemales[0].setAnimatorParamFloat("motion", animPar.m[0]);
			chaFemales[0].setAnimatorParamFloat("breast", animPar.breast);
		}
		if (chaMales[0].objBodyBone != null)
		{
			chaMales[0].setAnimatorParamFloat("height", animPar.heights[0]);
			chaMales[0].setAnimatorParamFloat("speed", animPar.speed);
			chaMales[0].setAnimatorParamFloat("motion", animPar.m[0]);
			chaMales[0].setAnimatorParamFloat("breast", animPar.breast);
		}
		if (item.GetItem() != null)
		{
			item.setAnimatorParamFloat("height", animPar.heights[0]);
			item.setAnimatorParamFloat("speed", animPar.speed);
			item.setAnimatorParamFloat("motion", animPar.m[0]);
		}
	}

	private void setPlay(string _playAnimation, bool _isFade = true)
	{
		chaFemales[0].setPlay(_playAnimation, 0);
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
		if (_isFade)
		{
			fade.FadeStart(1f);
		}
	}

	private bool LoopProc(int _loop, int _state, float _wheel, HScene.AnimationListInfo _infoAnimList)
	{
		float num = 0f;
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
			ctrlFlag.speed += _wheel;
			if (_loop == 0)
			{
				ctrlFlag.nowSpeedStateFast = ctrlFlag.speed >= 0.5f;
			}
			else
			{
				ctrlFlag.nowSpeedStateFast = ctrlFlag.speed >= 1.5f;
			}
			if (chaFemales[0].visibleAll && chaFemales[0].objBodyBone != null)
			{
				timeChangeMotionDeltaTimes[0] += Time.deltaTime;
				if (timeChangeMotions[0] <= timeChangeMotionDeltaTimes[0] && !enableMotion)
				{
					timeChangeMotions[0] = Random.Range(ctrlFlag.changeAutoMotionTimeMin, ctrlFlag.changeAutoMotionTimeMax);
					timeChangeMotionDeltaTimes[0] = 0f;
					enableMotion = true;
					timeMotion = 0f;
					float num2 = 0f;
					if (allowMotion)
					{
						num2 = 1f - ctrlFlag.motions[0];
						num2 = ((!(num2 <= ctrlFlag.changeMotionMinRate)) ? (ctrlFlag.motions[0] + Random.Range(ctrlFlag.changeMotionMinRate, num2)) : 1f);
						if (num2 >= 1f)
						{
							allowMotion = false;
						}
					}
					else
					{
						num2 = ctrlFlag.motions[0];
						num2 = ((!(num2 <= ctrlFlag.changeMotionMinRate)) ? (ctrlFlag.motions[0] - Random.Range(ctrlFlag.changeMotionMinRate, num2)) : 0f);
						if (num2 <= 0f)
						{
							allowMotion = true;
						}
					}
					lerpMotion = new Vector2(ctrlFlag.motions[0], num2);
					lerpTime = Random.Range(ctrlFlag.changeMotionTimeMin, ctrlFlag.changeMotionTimeMax);
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
			if (ctrlFlag.isGaugeHit)
			{
				feelHit.ChangeHit(_infoAnimList.nFeelHit, _loop);
			}
			ctrlFlag.isGaugeHit_M = ctrlFlag.isGaugeHit;
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

	private bool OLoopProc(int _state, float _wheel, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		float num = 0f;
		ctrlFlag.speed = Mathf.Clamp01(ctrlFlag.speed + _wheel);
		ctrlFlag.nowSpeedStateFast = ctrlFlag.speed >= 0.5f;
		feelHit.ChangeHit(_infoAnimList.nFeelHit, 2);
		ctrlFlag.isGaugeHit = feelHit.isHit(_infoAnimList.nFeelHit, 2, ctrlFlag.speed);
		ctrlFlag.isGaugeHit_M = ctrlFlag.isGaugeHit;
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
			if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 1 && ctrlFlag.nowAnimationInfo.id == 25)
			{
				ctrlFlag.AddParam(20, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 1 && ctrlFlag.nowAnimationInfo.id == 30)
			{
				ctrlFlag.AddParam(40, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 1 && ctrlFlag.nowAnimationInfo.id == 104)
			{
				ctrlFlag.AddParam(48, 1);
			}
			ctrlFlag.feel_m = 0f;
			ctrlFlag.isGaugeHit = false;
			ctrlFlag.isGaugeHit_M = ctrlFlag.isGaugeHit;
			isAtariHit.Value = ctrlFlag.isGaugeHit_M;
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			SetFinishCategoryEnable(_enable: false);
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
			ctrlFlag.voice.dialog = false;
		}
		else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishDrink && _modeCtrl != 0)
		{
			setPlay("Orgasm_IN");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 1 && ctrlFlag.nowAnimationInfo.id == 25)
			{
				ctrlFlag.AddParam(20, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 1 && ctrlFlag.nowAnimationInfo.id == 30)
			{
				ctrlFlag.AddParam(40, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 1 && ctrlFlag.nowAnimationInfo.id == 104)
			{
				ctrlFlag.AddParam(48, 1);
			}
			ctrlFlag.feel_m = 0f;
			ctrlFlag.isGaugeHit = false;
			ctrlFlag.isGaugeHit_M = ctrlFlag.isGaugeHit;
			isAtariHit.Value = ctrlFlag.isGaugeHit_M;
			ctrlFlag.voice.dialog = false;
			finishMotion = 0;
			SetFinishCategoryEnable(_enable: false);
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			ctrlFlag.isHoushiFinish = true;
			ctrlFlag.nowOrgasm = true;
			ctrlFlag.voice.oldFinish = 1;
		}
		else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishVomit && _modeCtrl != 0)
		{
			setPlay("Orgasm_IN");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 1 && ctrlFlag.nowAnimationInfo.id == 25)
			{
				ctrlFlag.AddParam(20, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 1 && ctrlFlag.nowAnimationInfo.id == 30)
			{
				ctrlFlag.AddParam(40, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 1 && ctrlFlag.nowAnimationInfo.id == 104)
			{
				ctrlFlag.AddParam(48, 1);
			}
			ctrlFlag.feel_m = 0f;
			finishMotion = 1;
			ctrlFlag.isGaugeHit = false;
			ctrlFlag.isGaugeHit_M = ctrlFlag.isGaugeHit;
			isAtariHit.Value = ctrlFlag.isGaugeHit_M;
			ctrlFlag.voice.dialog = false;
			SetFinishCategoryEnable(_enable: false);
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

	private bool StartProcTrigger(float _wheel)
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

	private bool StartProc(int _state, bool _restart)
	{
		if (nextPlay == 0)
		{
			return false;
		}
		if (nextPlay == 1)
		{
			nextPlay = 2;
			if (!_restart && _state != 1)
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
			voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 1, chaFemales[0], 0);
		}
		else if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(1))
		{
			voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 2, chaFemales[0], 0);
		}
		ctrlFlag.speed = 0f;
		ctrlFlag.loopType = 0;
		ctrlFlag.isNotCtrl = false;
		ctrlFlag.nowSpeedStateFast = false;
		oldHit = false;
		ctrlFlag.motions[0] = 0f;
		timeMotion = 0f;
		timeChangeMotions[0] = Random.Range(ctrlFlag.changeAutoMotionTimeMin, ctrlFlag.changeAutoMotionTimeMax);
		timeChangeMotionDeltaTimes[0] = 0f;
		feelHit.InitTime();
		if (_restart)
		{
			ctrlMeta.Clear();
			voice.AfterFinish();
		}
		ctrlFlag.voice.playShorts[0] = 1;
		return true;
	}

	private void SetFinishCategoryEnable(bool _enable)
	{
		if (sprite.IsFinishVisible(0))
		{
			sprite.categoryFinish.SetActive(_enable, 0);
		}
		if (sprite.IsFinishVisible(4))
		{
			sprite.categoryFinish.SetActive(_enable, 4);
		}
		if (sprite.IsFinishVisible(5))
		{
			sprite.categoryFinish.SetActive(_enable, 5);
		}
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

	private bool AutoStartProc(int _state, bool _restart)
	{
		if (nextPlay == 0)
		{
			return false;
		}
		if (nextPlay == 1)
		{
			nextPlay = 2;
			if (!_restart && _state != 1)
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
				voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 1, chaFemales[0], 0);
			}
			else if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(1))
			{
				voice.PlaySoundETC((_state != 0) ? "D_WLoop" : "WLoop", 2, chaFemales[0], 0);
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
		ctrlFlag.motions[0] = 0f;
		timeMotion = 0f;
		timeChangeMotions[0] = Random.Range(ctrlFlag.changeAutoMotionTimeMin, ctrlFlag.changeAutoMotionTimeMax);
		timeChangeMotionDeltaTimes[0] = 0f;
		feelHit.InitTime();
		if (_restart)
		{
			ctrlMeta.Clear();
			voice.AfterFinish();
		}
		ctrlFlag.voice.playShorts[0] = 1;
		auto.Reset();
		return true;
	}

	private bool AutoLoopProc(int _loop, int _state, float _wheel, HScene.AnimationListInfo _infoAnimList)
	{
		float num = 0f;
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
			if (chaFemales[0].visibleAll && chaFemales[0].objBodyBone != null)
			{
				timeChangeMotionDeltaTimes[0] += Time.deltaTime;
				if (timeChangeMotions[0] <= timeChangeMotionDeltaTimes[0] && !enableMotion)
				{
					timeChangeMotions[0] = Random.Range(ctrlFlag.changeAutoMotionTimeMin, ctrlFlag.changeAutoMotionTimeMax);
					timeChangeMotionDeltaTimes[0] = 0f;
					enableMotion = true;
					timeMotion = 0f;
					float num2 = 0f;
					if (allowMotion)
					{
						num2 = 1f - ctrlFlag.motions[0];
						num2 = ((!(num2 <= ctrlFlag.changeMotionMinRate)) ? (ctrlFlag.motions[0] + Random.Range(ctrlFlag.changeMotionMinRate, num2)) : 1f);
						if (num2 >= 1f)
						{
							allowMotion = false;
						}
					}
					else
					{
						num2 = ctrlFlag.motions[0];
						num2 = ((!(num2 <= ctrlFlag.changeMotionMinRate)) ? (ctrlFlag.motions[0] - Random.Range(ctrlFlag.changeMotionMinRate, num2)) : 0f);
						if (num2 <= 0f)
						{
							allowMotion = true;
						}
					}
					lerpMotion = new Vector2(ctrlFlag.motions[0], num2);
					lerpTime = Random.Range(ctrlFlag.changeMotionTimeMin, ctrlFlag.changeMotionTimeMax);
				}
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
			num = Time.deltaTime * ctrlFlag.speedGuageRate;
			num *= ((!ctrlFlag.isGaugeHit) ? 1f : 2f) * (float)((!ctrlFlag.stopFeelMale) ? 1 : 0);
			if (!ProcBase.hSceneManager.bMerchant && ProcBase.hSceneManager.HSkil.ContainsValue(18))
			{
				num *= ctrlFlag.SkilChangeSpeed(18);
			}
			ctrlFlag.feel_m += num;
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

	private bool AutoOLoopProc(int _state, float _wheel, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
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
			if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 1 && ctrlFlag.nowAnimationInfo.id == 25)
			{
				ctrlFlag.AddParam(20, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 1 && ctrlFlag.nowAnimationInfo.id == 30)
			{
				ctrlFlag.AddParam(40, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 1 && ctrlFlag.nowAnimationInfo.id == 104)
			{
				ctrlFlag.AddParam(48, 1);
			}
			ctrlFlag.feel_m = 0f;
			ctrlFlag.isGaugeHit = false;
			ctrlFlag.isGaugeHit_M = ctrlFlag.isGaugeHit;
			isAtariHit.Value = ctrlFlag.isGaugeHit_M;
			ctrlFlag.voice.dialog = false;
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			SetFinishCategoryEnable(_enable: false);
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
		else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishDrink && _modeCtrl != 0)
		{
			setPlay("Orgasm_IN");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 1 && ctrlFlag.nowAnimationInfo.id == 25)
			{
				ctrlFlag.AddParam(20, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 1 && ctrlFlag.nowAnimationInfo.id == 30)
			{
				ctrlFlag.AddParam(40, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 1 && ctrlFlag.nowAnimationInfo.id == 104)
			{
				ctrlFlag.AddParam(48, 1);
			}
			ctrlFlag.feel_m = 0f;
			ctrlFlag.isGaugeHit = false;
			ctrlFlag.isGaugeHit_M = ctrlFlag.isGaugeHit;
			isAtariHit.Value = ctrlFlag.isGaugeHit_M;
			ctrlFlag.voice.dialog = false;
			finishMotion = 0;
			SetFinishCategoryEnable(_enable: false);
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			ctrlFlag.isHoushiFinish = true;
			ctrlFlag.nowOrgasm = true;
			ctrlFlag.voice.oldFinish = 1;
		}
		else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishVomit && _modeCtrl != 0)
		{
			setPlay("Orgasm_IN");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 1 && ctrlFlag.nowAnimationInfo.id == 25)
			{
				ctrlFlag.AddParam(20, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 1 && ctrlFlag.nowAnimationInfo.id == 30)
			{
				ctrlFlag.AddParam(40, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 1 && ctrlFlag.nowAnimationInfo.id == 104)
			{
				ctrlFlag.AddParam(48, 1);
			}
			ctrlFlag.feel_m = 0f;
			ctrlFlag.isGaugeHit = false;
			ctrlFlag.isGaugeHit_M = ctrlFlag.isGaugeHit;
			isAtariHit.Value = ctrlFlag.isGaugeHit_M;
			ctrlFlag.voice.dialog = false;
			finishMotion = 1;
			SetFinishCategoryEnable(_enable: false);
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			ctrlFlag.isHoushiFinish = true;
			ctrlFlag.nowOrgasm = true;
			ctrlFlag.voice.oldFinish = 1;
		}
		return true;
	}

	private HScene.AnimationListInfo RecoverFaintnessAi()
	{
		for (int i = 0; i < lstAnimation.Count; i++)
		{
			if (lstAnimation[i].nPositons.Contains(ProcBase.hSceneManager.height))
			{
				return lstAnimation[i];
			}
		}
		return lstAnimation[0];
	}
}
