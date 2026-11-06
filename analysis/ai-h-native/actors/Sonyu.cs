using AIProject.Definitions;
using Manager;
using UnityEngine;

public class Sonyu : ProcBase
{
	private float timeMotion;

	private bool enableMotion;

	private bool allowMotion = true;

	private Vector2 lerpMotion = Vector2.zero;

	private float lerpTime;

	private int nextPlay;

	private bool oldHit;

	private bool finishMorS;

	private animParm animPar;

	public bool nowInsert;

	public Sonyu(DeliveryMember _delivery)
		: base(_delivery)
	{
		animPar.heights = new float[1];
		animPar.m = new float[1];
		CatID = 2;
	}

	public override bool SetStartMotion(bool _isIdle, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		AtariEffect.Stop();
		if (_isIdle)
		{
			bool flag = false;
			flag = ((ctrlFlag.selectAnimationListInfo == null) ? (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 3 && _modeCtrl == 0) : (ctrlFlag.selectAnimationListInfo.ActionCtrl.Item1 == 3 && _modeCtrl == 0));
			if (_modeCtrl == 1 || flag)
			{
				setPlay((!ctrlFlag.isFaintness) ? "Idle" : "D_OrgasmM_OUT_A", _fade: false);
			}
			else
			{
				setPlay((!ctrlFlag.isFaintness) ? "Idle" : "D_Idle", _fade: false);
			}
			voice.HouchiTime = 0f;
			ctrlFlag.loopType = -1;
			if (ctrlFlag.initiative == 0)
			{
			}
			nowInsert = false;
		}
		else
		{
			if (ctrlFlag.feel_f >= 0.7f)
			{
				setPlay((!ctrlFlag.isFaintness) ? "OLoop" : "D_OLoop", _fade: false);
				ctrlFlag.loopType = -1;
			}
			else
			{
				setPlay((!ctrlFlag.isFaintness) ? "WLoop" : "D_WLoop", _fade: false);
				ctrlFlag.loopType = 0;
			}
			ctrlFlag.speed = 0f;
			ctrlFlag.motions[0] = 0f;
			ctrlFlag.nowSpeedStateFast = false;
			auto.SetSpeed(0f);
			if (_infoAnimList.lstSystem.Contains(4))
			{
				ctrlFlag.isPainActionParam = true;
			}
		}
		ctrlMeta.SetParameterFromState(ctrlFlag.isFaintness ? 1 : 0);
		nextPlay = 0;
		oldHit = false;
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
		ctrlMeta.Proc(FemaleAi, ctrlFlag.isInsert);
		if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.RecoverFaintness)
		{
			if (FemaleAi.IsName("D_Idle") || FemaleAi.IsName("D_WLoop") || FemaleAi.IsName("D_SLoop") || FemaleAi.IsName("D_OLoop") || FemaleAi.IsName("D_Orgasm_IN_A") || FemaleAi.IsName("D_OrgasmM_OUT_A"))
			{
				if ((ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && _modeCtrl == 0) || (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 3 && _modeCtrl == 1))
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
				int item = ctrlFlag.nowAnimationInfo.ActionCtrl.Item1;
				int item2 = ctrlFlag.nowAnimationInfo.ActionCtrl.Item2;
				sprite.SetFinishSelect(2, _modeCtrl, item, item2);
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
		}
		SetFinishCategoryEnable(FemaleAi);
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
			StartProc(_restart: false, 0, _modeCtrl);
			voice.HouchiTime += Time.unscaledDeltaTime;
		}
		else if (_ai.IsName("Insert"))
		{
			InsertProc(_ai.normalizedTime, 0);
		}
		else if (_ai.IsName("WLoop"))
		{
			LoopProc(0, 0, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("SLoop"))
		{
			LoopProc(1, 0, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("OLoop"))
		{
			OLoopProc(0, num, _modeCtrl, _infoAnimList);
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
			AfterTheInsideWaitingProc(0, num);
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
			StartProc(_restart: true, 0, _modeCtrl);
		}
		else if (_ai.IsName("D_Idle"))
		{
			StartProcTrigger(num);
			StartProc(_restart: false, 1, _modeCtrl);
			voice.HouchiTime += Time.unscaledDeltaTime;
		}
		else if (_ai.IsName("D_Insert"))
		{
			InsertProc(_ai.normalizedTime, 1);
		}
		else if (_ai.IsName("D_WLoop"))
		{
			LoopProc(0, 1, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("D_SLoop"))
		{
			LoopProc(1, 1, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("D_OLoop"))
		{
			OLoopProc(1, num, _modeCtrl, _infoAnimList);
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
			AfterTheInsideWaitingProc(1, num);
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
			StartProc(_restart: true, 1, _modeCtrl);
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

	private bool Auto(AnimatorStateInfo _ai, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		float num = UnityEngine.Input.GetAxis("Mouse ScrollWheel") * (float)((!sprite.IsSpriteOver()) ? 1 : 0);
		num = ((num < 0f) ? (0f - ctrlFlag.wheelActionCount) : ((!(num > 0f)) ? 0f : ctrlFlag.wheelActionCount));
		if (_ai.IsName("Idle"))
		{
			AutoStartProcTrigger(_start: false);
			AutoStartProc(_restart: false, 0, _modeCtrl);
		}
		else if (_ai.IsName("Insert"))
		{
			InsertProc(_ai.normalizedTime, 0);
		}
		else if (_ai.IsName("WLoop"))
		{
			AutoLoopProc(0, 0, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("SLoop"))
		{
			AutoLoopProc(1, 0, num, _modeCtrl, _infoAnimList);
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
			AutoAfterTheInsideWaitingProc(0, num, _modeCtrl);
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
			AutoStartProc(_restart: true, 0, _modeCtrl);
		}
		else if (_ai.IsName("D_Idle"))
		{
			StartProcTrigger(num);
			StartProc(_restart: false, 1, _modeCtrl);
		}
		else if (_ai.IsName("D_Insert"))
		{
			InsertProc(_ai.normalizedTime, 1);
		}
		else if (_ai.IsName("D_WLoop"))
		{
			LoopProc(0, 1, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("D_SLoop"))
		{
			LoopProc(1, 1, num, _modeCtrl, _infoAnimList);
		}
		else if (_ai.IsName("D_OLoop"))
		{
			OLoopProc(1, num, _modeCtrl, _infoAnimList);
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
			AfterTheInsideWaitingProc(1, num);
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
			StartProc(_restart: true, 1, _modeCtrl);
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
		if (chaFemales[0].visibleAll && chaFemales[0].objTop != null)
		{
			animPar.heights[0] = chaFemales[0].GetShapeBodyValue(0);
			chaFemales[0].setAnimatorParamFloat("height", animPar.heights[0]);
			chaFemales[0].setAnimatorParamFloat("speed", animPar.speed);
			chaFemales[0].setAnimatorParamFloat("motion", animPar.m[0]);
			chaFemales[0].setAnimatorParamFloat("breast", animPar.breast);
		}
		if (chaMales[0].objTop != null)
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

	private void setPlay(string _playAnimation, bool _fade = true)
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
		if (_fade)
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

	private bool StartProc(bool _restart, int _state, int _modeCtrl)
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
				if (ProcBase.hSceneManager.EventKind != HSceneManager.HEvent.Yobai || _state != 1)
				{
					ctrlFlag.voice.playStart = 2;
				}
			}
			else if (ProcBase.hSceneManager.EventKind != HSceneManager.HEvent.Yobai)
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
		if ((ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && _modeCtrl == 0) || (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 3 && _modeCtrl == 1))
		{
			setPlay((_state != 0) ? "D_Insert" : "Insert");
			ctrlFlag.loopType = -1;
			nowInsert = true;
		}
		else
		{
			setPlay((_state != 0) ? "D_WLoop" : "WLoop");
			ctrlFlag.loopType = 0;
			if (ctrlFlag.nowAnimationInfo.lstSystem.Contains(4))
			{
				ctrlFlag.isPainActionParam = true;
			}
		}
		nextPlay = 0;
		ctrlFlag.speed = 0f;
		ctrlFlag.motions[0] = 0f;
		ctrlFlag.isNotCtrl = false;
		ctrlFlag.nowSpeedStateFast = false;
		timeMotion = 0f;
		oldHit = false;
		feelHit.InitTime();
		if (_state == 0)
		{
			timeChangeMotions[0] = Random.Range(ctrlFlag.changeAutoMotionTimeMin, ctrlFlag.changeAutoMotionTimeMax);
			timeChangeMotionDeltaTimes[0] = 0f;
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
		ctrlFlag.speed = 0f;
		ctrlFlag.loopType = 0;
		nowInsert = false;
		return true;
	}

	private bool LoopProc(int _loop, int _state, float _wheel, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishInSide && ctrlFlag.feel_m >= 0.7f && ((ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && _modeCtrl == 0) || (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 3 && _modeCtrl == 1)))
		{
			string[] array = new string[2] { "OrgasmM_IN", "D_OrgasmM_IN" };
			setPlay(array[_state]);
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			if (_state != 0)
			{
				ctrlFlag.AddParam(15, 1);
			}
			if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 38)
			{
				ctrlFlag.AddParam(20, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 44)
			{
				ctrlFlag.AddParam(26, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 45)
			{
				ctrlFlag.AddParam(38, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 46)
			{
				ctrlFlag.AddParam(42, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 113)
			{
				ctrlFlag.AddParam(46, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 110)
			{
				ctrlFlag.AddParam(50, 1);
			}
			if (ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.Bath && ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.MapBath && ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.Tachi)
			{
				ctrlFlag.AddParam(28, 1);
			}
			if (ctrlFlag.isToilet)
			{
				ctrlFlag.AddParam(30, 1);
			}
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
		else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishOutSide && ctrlFlag.feel_m >= 0.7f && (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 != 3 || _modeCtrl != 0 || _state == 0))
		{
			string[] array2 = new string[2] { "OrgasmM_OUT", "D_OrgasmM_OUT" };
			setPlay(array2[_state]);
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			if (_state != 0)
			{
				ctrlFlag.AddParam(15, 1);
			}
			if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 38)
			{
				ctrlFlag.AddParam(20, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 44)
			{
				ctrlFlag.AddParam(26, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 45)
			{
				ctrlFlag.AddParam(38, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 46)
			{
				ctrlFlag.AddParam(42, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 113)
			{
				ctrlFlag.AddParam(46, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 110)
			{
				ctrlFlag.AddParam(50, 1);
			}
			if (ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.Bath && ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.MapBath && ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.Tachi)
			{
				ctrlFlag.AddParam(28, 1);
			}
			if (ctrlFlag.isToilet)
			{
				ctrlFlag.AddParam(30, 1);
			}
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
			if (chaFemales[0].visibleAll && chaFemales[0].objTop != null && (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.startVoice) && ctrlFlag.nowAnimationInfo.nAnimListInfoID == 3)
			{
				Singleton<Voice>.Instance.Stop(ctrlFlag.voice.voiceTrs[0]);
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
			if (chaFemales[0].visibleAll && chaFemales[0].objBodyBone != null)
			{
				timeChangeMotionDeltaTimes[0] += Time.deltaTime;
				if (timeChangeMotions[0] <= timeChangeMotionDeltaTimes[0] && !enableMotion && _state == 0)
				{
					timeChangeMotions[0] = Random.Range(ctrlFlag.changeAutoMotionTimeMin, ctrlFlag.changeAutoMotionTimeMax);
					timeChangeMotionDeltaTimes[0] = 0f;
					enableMotion = true;
					timeMotion = 0f;
					float num = 0f;
					if (allowMotion)
					{
						num = 1f - ctrlFlag.motions[0];
						num = ((!(num <= ctrlFlag.changeMotionMinRate)) ? (ctrlFlag.motions[0] + Random.Range(ctrlFlag.changeMotionMinRate, num)) : 1f);
						if (num >= 1f)
						{
							allowMotion = false;
						}
					}
					else
					{
						num = ctrlFlag.motions[0];
						num = ((!(num <= ctrlFlag.changeMotionMinRate)) ? (ctrlFlag.motions[0] - Random.Range(ctrlFlag.changeMotionMinRate, num)) : 0f);
						if (num <= 0f)
						{
							allowMotion = true;
						}
					}
					lerpMotion = new Vector2(ctrlFlag.motions[0], num);
					lerpTime = Random.Range(ctrlFlag.changeMotionTimeMin, ctrlFlag.changeMotionTimeMax);
				}
			}
			if (_loop == 0)
			{
				if (ctrlFlag.speed > 1f && ctrlFlag.loopType == 0)
				{
					setPlay((_state != 0) ? "D_SLoop" : "SLoop");
					ctrlFlag.nowSpeedStateFast = false;
					if (chaFemales[0].visibleAll && chaFemales[0].objTop != null && (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.startVoice) && ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 3 && ctrlFlag.nowAnimationInfo.ActionCtrl.Item2 == 0)
					{
						Singleton<Voice>.Instance.Stop(ctrlFlag.voice.voiceTrs[0]);
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
					if (chaFemales[0].visibleAll && chaFemales[0].objTop != null && (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.startVoice) && ctrlFlag.nowAnimationInfo.nAnimListInfoID == 3 && ctrlFlag.nowAnimationInfo.id == 0)
					{
						Singleton<Voice>.Instance.Stop(ctrlFlag.voice.voiceTrs[0]);
					}
					feelHit.InitTime();
					ctrlFlag.loopType = 0;
				}
				ctrlFlag.speed = Mathf.Clamp(ctrlFlag.speed, 0f, 2f);
			}
			float num2 = 0f;
			if (_state != 1 || ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 != 3 || _modeCtrl != 0)
			{
				num2 = Time.deltaTime * ctrlFlag.speedGuageRate;
				num2 *= (float)((!ctrlFlag.stopFeelMale) ? 1 : 0);
				if (!ProcBase.hSceneManager.bMerchant && ProcBase.hSceneManager.HSkil.ContainsValue(1) && ctrlFlag.nowAnimationInfo.lstSystem.Contains(1))
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
				if (!ctrlFlag.nowAnimationInfo.lstSystem.Contains(0) && (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 != 3 || _modeCtrl != 0))
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
				if (chaFemales[0].visibleAll && chaFemales[0].objTop != null && (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.startVoice) && ctrlFlag.nowAnimationInfo.nAnimListInfoID == 3)
				{
					Singleton<Voice>.Instance.Stop(ctrlFlag.voice.voiceTrs[0]);
				}
			}
		}
		return true;
	}

	private bool OLoopProc(int _state, float _wheel, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
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
		bool flag = ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 3 && _modeCtrl == 0;
		if (_state != 1 || !flag)
		{
			num = Time.deltaTime * ctrlFlag.speedGuageRate;
			num *= (float)((!ctrlFlag.stopFeelMale) ? 1 : 0);
			if (!ProcBase.hSceneManager.bMerchant && ProcBase.hSceneManager.HSkil.ContainsValue(1) && ctrlFlag.nowAnimationInfo.lstSystem.Contains(1))
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
		isAtariHit.Value = ctrlFlag.isGaugeHit;
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
			if (!ctrlFlag.nowAnimationInfo.lstSystem.Contains(0) && !flag)
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
			if (_state != 0)
			{
				ctrlFlag.AddParam(15, 1);
			}
			if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 38)
			{
				ctrlFlag.AddParam(20, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 44)
			{
				ctrlFlag.AddParam(26, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 45)
			{
				ctrlFlag.AddParam(38, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 46)
			{
				ctrlFlag.AddParam(42, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 113)
			{
				ctrlFlag.AddParam(46, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 110)
			{
				ctrlFlag.AddParam(50, 1);
			}
			if (ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.Bath && ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.MapBath && ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.Tachi)
			{
				ctrlFlag.AddParam(28, 1);
			}
			if (ctrlFlag.isToilet)
			{
				ctrlFlag.AddParam(30, 1);
			}
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
			ctrlFlag.voice.dialog = false;
			ctrlFlag.rateNip = 1f;
			if (Config.HData.Gloss)
			{
				ctrlFlag.rateTuya = 1f;
			}
			bool urine = Config.HData.Urine;
			bool sio = Config.HData.Sio;
			if (!ProcBase.hSceneManager.bMerchant)
			{
				bool flag2 = Hitem.Effect(5);
				if (sio)
				{
					particle.Play(0);
				}
				else if (((ctrlFlag.numFaintness == 0 && ctrlFlag.numOrgasm >= ctrlFlag.gotoFaintnessCount) || flag2) && ProcBase.hSceneManager.GetFlaverSkillLevel(2) >= 100)
				{
					particle.Play(0);
				}
				if (urine || ProcBase.hSceneManager.Toilet >= 70f)
				{
					particle.Play(1);
					ctrlFlag.numUrine++;
					ctrlFlag.voice.urines[0] = true;
					ProcBase.hSceneManager.Toilet = 30f;
					int desireKey = Desire.GetDesireKey(Desire.Type.Toilet);
					ProcBase.hSceneManager.Agent[0].SetDesire(desireKey, ProcBase.hSceneManager.Toilet);
				}
			}
			else
			{
				if (sio)
				{
					particle.Play(0);
				}
				if (urine)
				{
					particle.Play(1);
					ctrlFlag.numUrine++;
					ctrlFlag.voice.urines[0] = true;
				}
			}
		}
		else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishInSide && ctrlFlag.feel_m >= 0.7f && ((ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && _modeCtrl == 0) || (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 3 && _modeCtrl == 1)))
		{
			setPlay((_state != 0) ? "D_OrgasmM_IN" : "OrgasmM_IN");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			if (_state != 0)
			{
				ctrlFlag.AddParam(15, 1);
			}
			if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 38)
			{
				ctrlFlag.AddParam(20, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 44)
			{
				ctrlFlag.AddParam(26, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 45)
			{
				ctrlFlag.AddParam(38, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 46)
			{
				ctrlFlag.AddParam(42, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 113)
			{
				ctrlFlag.AddParam(46, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 110)
			{
				ctrlFlag.AddParam(50, 1);
			}
			if (ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.Bath && ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.MapBath && ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.Tachi)
			{
				ctrlFlag.AddParam(28, 1);
			}
			if (ctrlFlag.isToilet)
			{
				ctrlFlag.AddParam(30, 1);
			}
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
		else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishOutSide && ctrlFlag.feel_m >= 0.7f && (!flag || (flag && _state == 0)))
		{
			setPlay((_state != 0) ? "D_OrgasmM_OUT" : "OrgasmM_OUT");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			if (_state != 0)
			{
				ctrlFlag.AddParam(15, 1);
			}
			if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 38)
			{
				ctrlFlag.AddParam(20, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 44)
			{
				ctrlFlag.AddParam(26, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 45)
			{
				ctrlFlag.AddParam(38, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 46)
			{
				ctrlFlag.AddParam(42, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 113)
			{
				ctrlFlag.AddParam(46, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 110)
			{
				ctrlFlag.AddParam(50, 1);
			}
			if (ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.Bath && ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.MapBath && ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.Tachi)
			{
				ctrlFlag.AddParam(28, 1);
			}
			if (ctrlFlag.isToilet)
			{
				ctrlFlag.AddParam(30, 1);
			}
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
		else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishSame && ctrlFlag.feel_m >= 0.7f && (!flag || (flag && _state == 0)))
		{
			setPlay((_state != 0) ? "D_OrgasmS_IN" : "OrgasmS_IN");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			if (_state != 0)
			{
				ctrlFlag.AddParam(15, 1);
			}
			if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 38)
			{
				ctrlFlag.AddParam(20, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 44)
			{
				ctrlFlag.AddParam(26, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 45)
			{
				ctrlFlag.AddParam(38, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 46)
			{
				ctrlFlag.AddParam(42, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 113)
			{
				ctrlFlag.AddParam(46, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 110)
			{
				ctrlFlag.AddParam(50, 1);
			}
			if (ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.Bath && ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.MapBath && ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.Tachi)
			{
				ctrlFlag.AddParam(28, 1);
			}
			if (ctrlFlag.isToilet)
			{
				ctrlFlag.AddParam(30, 1);
			}
			ctrlFlag.feel_m = 0f;
			ctrlFlag.feel_f = 0f;
			ctrlFlag.isInsert = true;
			ctrlFlag.isGaugeHit = false;
			isAtariHit.Value = ctrlFlag.isGaugeHit;
			ctrlFlag.numOrgasm = Mathf.Clamp(ctrlFlag.numOrgasm + 1, 0, 10);
			ctrlFlag.AddOrgasm();
			ctrlFlag.numSameOrgasm = Mathf.Clamp(ctrlFlag.numSameOrgasm + 1, 0, 999999);
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
			ctrlFlag.nowOrgasm = true;
			if ((ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && _modeCtrl == 0) || (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 3 && _modeCtrl == 1))
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
				bool flag3 = Hitem.Effect(5);
				if (sio2)
				{
					particle.Play(0);
				}
				else if (((ctrlFlag.numFaintness == 0 && ctrlFlag.numOrgasm >= ctrlFlag.gotoFaintnessCount) || flag3) && ProcBase.hSceneManager.GetFlaverSkillLevel(2) >= 100)
				{
					particle.Play(0);
				}
				if (urine2 || ProcBase.hSceneManager.Toilet >= 70f)
				{
					particle.Play(1);
					ctrlFlag.numUrine++;
					ctrlFlag.voice.urines[0] = true;
					ProcBase.hSceneManager.Toilet = 0f;
					int desireKey2 = Desire.GetDesireKey(Desire.Type.Toilet);
					ProcBase.hSceneManager.Agent[0].SetDesire(desireKey2, ProcBase.hSceneManager.Toilet);
				}
			}
			else
			{
				if (sio2)
				{
					particle.Play(0);
				}
				if (urine2)
				{
					particle.Play(1);
					ctrlFlag.numUrine++;
					ctrlFlag.voice.urines[0] = true;
				}
			}
		}
		return true;
	}

	private bool GotoFaintness(int _state, int _modeCtrl, int _nextAfter)
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
			bool sio = Config.HData.Sio;
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
			int item = ctrlFlag.nowAnimationInfo.ActionCtrl.Item1;
			int item2 = ctrlFlag.nowAnimationInfo.ActionCtrl.Item2;
			sprite.SetFinishSelect(2, _modeCtrl, item, item2);
		}
		else
		{
			setPlay((_state == 0) ? ((_nextAfter != 0) ? "OrgasmM_OUT_A" : "Orgasm_IN_A") : ((_nextAfter != 0) ? "D_OrgasmM_OUT_A" : "D_Orgasm_IN_A"));
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
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
			GotoFaintness(_state, _modeCtrl, ((ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 != 2 || _modeCtrl != 0) && (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 != 3 || _modeCtrl != 1)) ? 1 : 0);
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

	private bool FinishNextAnimationByMorS(float _normalizedTime, float _loopCount, int _state, int _modeCtrl, bool _finishMorS)
	{
		if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 3 && _modeCtrl == 1)
		{
			if (_normalizedTime < _loopCount)
			{
				return false;
			}
			finishMorS = _finishMorS;
			setPlay((_state != 0) ? "D_Vomit" : "Vomit");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
		}
		else
		{
			AfterTheNextWaitingAnimation(_normalizedTime, _loopCount, _state, _modeCtrl, (!_finishMorS) ? 1 : 0);
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

	private bool AfterTheInsideWaitingProc(int _state, float _wheel)
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
				ctrlFlag.motions[0] = 0f;
				timeMotion = 0f;
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
			setPlay((_state != 0) ? "D_WLoop" : "WLoop");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = 0;
			ctrlFlag.nowSpeedStateFast = false;
			ctrlFlag.motions[0] = 0f;
			timeMotion = 0f;
			if (_state == 0)
			{
				timeChangeMotions[0] = Random.Range(ctrlFlag.changeAutoMotionTimeMin, ctrlFlag.changeAutoMotionTimeMax);
				timeChangeMotionDeltaTimes[0] = 0f;
			}
			voice.AfterFinish();
			oldHit = false;
			feelHit.InitTime();
			nextPlay = 0;
			ctrlMeta.Clear();
			break;
		}
		return true;
	}

	private void SetFinishCategoryEnable(AnimatorStateInfo _ai)
	{
		bool flag = _ai.IsName("WLoop") || _ai.IsName("SLoop") || _ai.IsName("D_WLoop") || _ai.IsName("D_SLoop") || _ai.IsName("OLoop") || _ai.IsName("D_OLoop");
		bool flag2 = ctrlFlag.feel_m >= 0.7f && flag;
		bool flag3 = (ctrlFlag.feel_f < 0.7f || ctrlFlag.feel_m < 0.7f) && flag;
		flag3 &= Config.HData.FinishButton;
		sprite.categoryFinish.SetActive(flag3, 3);
		if (sprite.IsFinishVisible(0))
		{
			sprite.categoryFinish.SetActive(flag2, 0);
		}
		if (sprite.IsFinishVisible(1))
		{
			sprite.categoryFinish.SetActive(flag2 && ctrlFlag.feel_f >= 0.7f, 1);
		}
		if (sprite.IsFinishVisible(2))
		{
			sprite.categoryFinish.SetActive(flag2, 2);
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

	private bool AutoStartProc(bool _restart, int _state, int _modeCtrl)
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
				if (_state != 1)
				{
					ctrlFlag.voice.playStart = 2;
				}
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
			if ((ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && _modeCtrl == 0) || (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 3 && _modeCtrl == 1))
			{
				setPlay((_state != 0) ? "D_Insert" : "Insert");
				ctrlFlag.speed = 0f;
				ctrlFlag.loopType = -1;
				nowInsert = true;
			}
			else
			{
				setPlay((_state != 0) ? "D_WLoop" : "WLoop");
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
		timeMotion = 0f;
		oldHit = false;
		if (_state == 0)
		{
			timeChangeMotions[0] = Random.Range(ctrlFlag.changeAutoMotionTimeMin, ctrlFlag.changeAutoMotionTimeMax);
			timeChangeMotionDeltaTimes[0] = 0f;
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

	private bool AutoLoopProc(int _loop, int _state, float _wheel, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		if (((ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishInSide && ctrlFlag.feel_m >= 0.7f) || (ctrlFlag.initiative == 2 && ctrlFlag.feel_m >= 1f)) && ((ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && _modeCtrl == 0) || (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 3 && _modeCtrl == 1)))
		{
			string[] array = new string[2] { "OrgasmM_IN", "D_OrgasmM_IN" };
			setPlay(array[_state]);
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			if (_state != 0)
			{
				ctrlFlag.AddParam(15, 1);
			}
			if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 38)
			{
				ctrlFlag.AddParam(20, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 44)
			{
				ctrlFlag.AddParam(26, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 45)
			{
				ctrlFlag.AddParam(38, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 46)
			{
				ctrlFlag.AddParam(42, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 113)
			{
				ctrlFlag.AddParam(46, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 110)
			{
				ctrlFlag.AddParam(50, 1);
			}
			if (ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.Bath && ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.MapBath && ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.Tachi)
			{
				ctrlFlag.AddParam(28, 1);
			}
			if (ctrlFlag.isToilet)
			{
				ctrlFlag.AddParam(30, 1);
			}
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
		else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishOutSide && ctrlFlag.feel_m >= 0.7f && (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 != 3 || _modeCtrl != 0 || _state == 0))
		{
			string[] array2 = new string[2] { "OrgasmM_OUT", "D_OrgasmM_OUT" };
			setPlay(array2[_state]);
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			if (_state != 0)
			{
				ctrlFlag.AddParam(15, 1);
			}
			if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 38)
			{
				ctrlFlag.AddParam(20, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 44)
			{
				ctrlFlag.AddParam(26, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 45)
			{
				ctrlFlag.AddParam(38, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 46)
			{
				ctrlFlag.AddParam(42, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 113)
			{
				ctrlFlag.AddParam(46, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 110)
			{
				ctrlFlag.AddParam(50, 1);
			}
			if (ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.Bath && ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.MapBath && ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.Tachi)
			{
				ctrlFlag.AddParam(28, 1);
			}
			if (ctrlFlag.isToilet)
			{
				ctrlFlag.AddParam(30, 1);
			}
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
			ctrlFlag.isGaugeHit = false;
		}
		else
		{
			if (chaFemales[0].visibleAll && chaFemales[0].objBodyBone != null)
			{
				timeChangeMotionDeltaTimes[0] += Time.deltaTime;
				if (timeChangeMotions[0] <= timeChangeMotionDeltaTimes[0] && !enableMotion && _state == 0)
				{
					timeChangeMotions[0] = Random.Range(ctrlFlag.changeAutoMotionTimeMin, ctrlFlag.changeAutoMotionTimeMax);
					timeChangeMotionDeltaTimes[0] = 0f;
					enableMotion = true;
					timeMotion = 0f;
					float num = 0f;
					if (allowMotion)
					{
						num = 1f - ctrlFlag.motions[0];
						num = ((!(num <= ctrlFlag.changeMotionMinRate)) ? (ctrlFlag.motions[0] + Random.Range(ctrlFlag.changeMotionMinRate, num)) : 1f);
						if (num >= 1f)
						{
							allowMotion = false;
						}
					}
					else
					{
						num = ctrlFlag.motions[0];
						num = ((!(num <= ctrlFlag.changeMotionMinRate)) ? (ctrlFlag.motions[0] - Random.Range(ctrlFlag.changeMotionMinRate, num)) : 0f);
						if (num <= 0f)
						{
							allowMotion = true;
						}
					}
					lerpMotion = new Vector2(ctrlFlag.motions[0], num);
					lerpTime = Random.Range(ctrlFlag.changeMotionTimeMin, ctrlFlag.changeMotionTimeMax);
				}
			}
			feelHit.ChangeHit(_infoAnimList.nFeelHit, _loop);
			Vector2 hitArea = feelHit.GetHitArea(_infoAnimList.nFeelHit, _loop);
			if (auto.ChangeLoopMotion(_loop == 1))
			{
				setPlay((_loop == 0) ? ((_state != 0) ? "D_SLoop" : "SLoop") : ((_state != 0) ? "D_WLoop" : "WLoop"));
				if (chaFemales[0].visibleAll && chaFemales[0].objTop != null && (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.startVoice) && ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 3 && ctrlFlag.nowAnimationInfo.ActionCtrl.Item2 == 0)
				{
					Singleton<Voice>.Instance.Stop(ctrlFlag.voice.voiceTrs[0]);
				}
				feelHit.InitTime();
			}
			else
			{
				auto.ChangeSpeed(_loop == 1, hitArea);
				if (auto.AddSpeed(_wheel, _loop))
				{
					setPlay((_loop == 0) ? ((_state != 0) ? "D_SLoop" : "SLoop") : ((_state != 0) ? "D_WLoop" : "WLoop"));
					if (chaFemales[0].visibleAll && chaFemales[0].objTop != null && (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.startVoice) && ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 3 && ctrlFlag.nowAnimationInfo.ActionCtrl.Item2 == 0)
					{
						Singleton<Voice>.Instance.Stop(ctrlFlag.voice.voiceTrs[0]);
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
			float num2 = 0f;
			if (_state == 1)
			{
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
			}
			else
			{
				num2 = Time.deltaTime * ctrlFlag.speedGuageRate;
				num2 *= ((!ctrlFlag.isGaugeHit) ? 0.3f : 1f) * (float)((!ctrlFlag.stopFeelFemal) ? 1 : 0);
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
			float num3 = 0f;
			if (_state != 1 || ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 != 3 || _modeCtrl != 0)
			{
				num3 = Time.deltaTime * ctrlFlag.speedGuageRate;
				num3 *= (float)((!ctrlFlag.stopFeelMale) ? 1 : 0);
				if (!ProcBase.hSceneManager.bMerchant && ProcBase.hSceneManager.HSkil.ContainsValue(1) && ctrlFlag.nowAnimationInfo.lstSystem.Contains(1))
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
				if (!ctrlFlag.nowAnimationInfo.lstSystem.Contains(0) && (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 != 3 || _modeCtrl != 0))
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
		if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishBefore)
		{
			if (ctrlFlag.feel_m <= 0.7f)
			{
				ctrlFlag.isGaugeHit = false;
				ctrlFlag.feel_m = 0.7f;
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
		if (_state != 1 || ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 != 3 || _modeCtrl != 0)
		{
			num2 = Time.deltaTime * ctrlFlag.speedGuageRate;
			num2 *= (float)((!ctrlFlag.stopFeelMale) ? 1 : 0);
			if (!ProcBase.hSceneManager.bMerchant && ProcBase.hSceneManager.HSkil.ContainsValue(1) && ctrlFlag.nowAnimationInfo.lstSystem.Contains(1))
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
			if (!ctrlFlag.nowAnimationInfo.lstSystem.Contains(0) && (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 != 3 || _modeCtrl != 0))
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
			if (_state != 0)
			{
				ctrlFlag.AddParam(15, 1);
			}
			if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 38)
			{
				ctrlFlag.AddParam(20, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 44)
			{
				ctrlFlag.AddParam(26, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 45)
			{
				ctrlFlag.AddParam(38, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 46)
			{
				ctrlFlag.AddParam(42, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 113)
			{
				ctrlFlag.AddParam(46, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 110)
			{
				ctrlFlag.AddParam(50, 1);
			}
			if (ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.Bath && ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.MapBath && ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.Tachi)
			{
				ctrlFlag.AddParam(28, 1);
			}
			if (ctrlFlag.isToilet)
			{
				ctrlFlag.AddParam(30, 1);
			}
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
				}
				else if (((ctrlFlag.numFaintness == 0 && ctrlFlag.numOrgasm >= ctrlFlag.gotoFaintnessCount) || flag) && ProcBase.hSceneManager.GetFlaverSkillLevel(2) >= 100)
				{
					particle.Play(0);
				}
				if (urine || ProcBase.hSceneManager.Toilet >= 70f)
				{
					particle.Play(1);
					ctrlFlag.numUrine++;
					ctrlFlag.voice.urines[0] = true;
					ProcBase.hSceneManager.Toilet = 30f;
					int desireKey = Desire.GetDesireKey(Desire.Type.Toilet);
					ProcBase.hSceneManager.Agent[0].SetDesire(desireKey, ProcBase.hSceneManager.Toilet);
				}
			}
			else
			{
				if (sio)
				{
					particle.Play(0);
				}
				if (urine)
				{
					particle.Play(1);
					ctrlFlag.numUrine++;
					ctrlFlag.voice.urines[0] = true;
				}
			}
		}
		else if (((ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishInSide && ctrlFlag.feel_m >= 0.7f) || (ctrlFlag.initiative == 2 && ctrlFlag.feel_m >= 1f)) && ((ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && _modeCtrl == 0) || (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 3 && _modeCtrl == 1)))
		{
			setPlay((_state != 0) ? "D_OrgasmM_IN" : "OrgasmM_IN");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			if (_state != 0)
			{
				ctrlFlag.AddParam(15, 1);
			}
			if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 38)
			{
				ctrlFlag.AddParam(20, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 44)
			{
				ctrlFlag.AddParam(26, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 45)
			{
				ctrlFlag.AddParam(38, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 46)
			{
				ctrlFlag.AddParam(42, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 113)
			{
				ctrlFlag.AddParam(46, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 110)
			{
				ctrlFlag.AddParam(50, 1);
			}
			if (ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.Bath && ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.MapBath && ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.Tachi)
			{
				ctrlFlag.AddParam(28, 1);
			}
			if (ctrlFlag.isToilet)
			{
				ctrlFlag.AddParam(30, 1);
			}
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
		else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishOutSide && ctrlFlag.feel_m >= 0.7f && (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 != 3 || _modeCtrl != 0 || _state == 0))
		{
			setPlay((_state != 0) ? "D_OrgasmM_OUT" : "OrgasmM_OUT");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			if (_state != 0)
			{
				ctrlFlag.AddParam(15, 1);
			}
			if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 38)
			{
				ctrlFlag.AddParam(20, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 44)
			{
				ctrlFlag.AddParam(26, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 45)
			{
				ctrlFlag.AddParam(38, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 46)
			{
				ctrlFlag.AddParam(42, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 113)
			{
				ctrlFlag.AddParam(46, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 110)
			{
				ctrlFlag.AddParam(50, 1);
			}
			if (ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.Bath && ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.MapBath && ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.Tachi)
			{
				ctrlFlag.AddParam(28, 1);
			}
			if (ctrlFlag.isToilet)
			{
				ctrlFlag.AddParam(30, 1);
			}
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
		else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishSame && ctrlFlag.feel_m >= 0.7f && (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 != 3 || _modeCtrl != 0 || _state == 0))
		{
			setPlay((_state != 0) ? "D_OrgasmS_IN" : "OrgasmS_IN");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			if (_state != 0)
			{
				ctrlFlag.AddParam(15, 1);
			}
			if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 38)
			{
				ctrlFlag.AddParam(20, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 44)
			{
				ctrlFlag.AddParam(26, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 45)
			{
				ctrlFlag.AddParam(38, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 46)
			{
				ctrlFlag.AddParam(42, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 113)
			{
				ctrlFlag.AddParam(46, 1);
			}
			else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && ctrlFlag.nowAnimationInfo.id == 110)
			{
				ctrlFlag.AddParam(50, 1);
			}
			if (ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.Bath && ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.MapBath && ProcBase.hSceneManager.EventKind == HSceneManager.HEvent.Tachi)
			{
				ctrlFlag.AddParam(28, 1);
			}
			if (ctrlFlag.isToilet)
			{
				ctrlFlag.AddParam(30, 1);
			}
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
			if ((ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 2 && _modeCtrl == 0) || (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 3 && _modeCtrl == 1))
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
				}
				else if (((ctrlFlag.numFaintness == 0 && ctrlFlag.numOrgasm >= ctrlFlag.gotoFaintnessCount) || flag2) && ProcBase.hSceneManager.GetFlaverSkillLevel(2) >= 100)
				{
					particle.Play(0);
				}
				if (urine2 || ProcBase.hSceneManager.Toilet >= 70f)
				{
					particle.Play(1);
					ctrlFlag.numUrine++;
					ctrlFlag.voice.urines[0] = true;
					ProcBase.hSceneManager.Toilet = 30f;
					int desireKey2 = Desire.GetDesireKey(Desire.Type.Toilet);
					ProcBase.hSceneManager.Agent[0].SetDesire(desireKey2, ProcBase.hSceneManager.Toilet);
				}
			}
			else
			{
				if (sio2)
				{
					particle.Play(0);
				}
				if (urine2)
				{
					particle.Play(1);
					ctrlFlag.numUrine++;
					ctrlFlag.voice.urines[0] = true;
				}
			}
		}
		return true;
	}

	private bool AutoAfterTheInsideWaitingProc(int _state, float _wheel, int _modeCtrl)
	{
		if (auto.IsPull(ctrlFlag.isInsert))
		{
			setPlay((_state != 0) ? "D_Pull" : "Pull");
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			ctrlFlag.nowSpeedStateFast = false;
			ctrlFlag.motions[0] = 0f;
			timeMotion = 0f;
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
		ctrlFlag.speed = 0f;
		ctrlFlag.loopType = 0;
		ctrlFlag.nowSpeedStateFast = false;
		ctrlFlag.motions[0] = 0f;
		timeMotion = 0f;
		oldHit = false;
		if (_state == 0)
		{
			timeChangeMotions[0] = Random.Range(ctrlFlag.changeAutoMotionTimeMin, ctrlFlag.changeAutoMotionTimeMax);
			timeChangeMotionDeltaTimes[0] = 0f;
		}
		voice.AfterFinish();
		auto.ReStartInit();
		auto.PullInit();
		ctrlMeta.Clear();
		return true;
	}
}
