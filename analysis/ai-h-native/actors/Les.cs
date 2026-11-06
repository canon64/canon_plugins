using AIProject.Definitions;
using Manager;
using UnityEngine;

public class Les : ProcBase
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

	private int nextPlay;

	private bool oldHit;

	private animParm animPar;

	public Les(DeliveryMember _delivery)
		: base(_delivery)
	{
		animPar.heights = new float[2];
		animPar.m = new float[2];
		CatID = 6;
	}

	public override bool Init(int _modeCtrl)
	{
		base.Init(_modeCtrl);
		return true;
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
			if (ctrlFlag.initiative != 0)
			{
			}
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
						voice.PlaySoundETC((!ctrlFlag.isFaintness) ? "WLoop" : "D_WLoop", 1, chaFemales[0], 0);
					}
					else if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(1))
					{
						voice.PlaySoundETC((!ctrlFlag.isFaintness) ? "WLoop" : "D_WLoop", 2, chaFemales[0], 0);
					}
				}
				else
				{
					setPlay("WLoop", _isFade: false);
					if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(0))
					{
						voice.PlaySoundETC("WLoop", 1, chaFemales[0], 0);
					}
					else if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(1))
					{
						voice.PlaySoundETC("WLoop", 2, chaFemales[0], 0);
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
			Manual(FemaleAi, _infoAnimList);
		}
		else
		{
			Auto(FemaleAi, _infoAnimList);
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
		bool flag = FemaleAi.IsName("WLoop") || FemaleAi.IsName("SLoop") || FemaleAi.IsName("D_WLoop") || FemaleAi.IsName("D_SLoop");
		flag &= Config.HData.FinishButton;
		sprite.categoryFinish.SetActive(flag, 3);
		ctrlMeta.Proc(FemaleAi);
		if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.RecoverFaintness)
		{
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
		}
		setAnimationParamater();
		return true;
	}

	private bool Manual(AnimatorStateInfo _ai, HScene.AnimationListInfo _infoAnimList)
	{
		float num = UnityEngine.Input.GetAxis("Mouse ScrollWheel") * (float)((!sprite.IsSpriteOver()) ? 1 : 0);
		num = ((num < 0f) ? (0f - ctrlFlag.wheelActionCount) : ((!(num > 0f)) ? 0f : ctrlFlag.wheelActionCount));
		if (_ai.IsName("Idle"))
		{
			StartProcTrigger(num);
			StartProc(_isReStart: false);
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
			OLoopProc(0, num, _infoAnimList);
		}
		else if (_ai.IsName("Orgasm"))
		{
			OrgasmProc(0, _ai.normalizedTime);
		}
		else if (_ai.IsName("Orgasm_A"))
		{
			StartProcTrigger(num);
			StartProc(_isReStart: true);
		}
		else if (_ai.IsName("D_Idle"))
		{
			FaintnessStartProcTrigger(num, _start: true, _infoAnimList.nDownPtn != 0);
			FaintnessStartProc(_start: true);
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
			OLoopProc(1, num, _infoAnimList);
		}
		else if (_ai.IsName("D_Orgasm"))
		{
			OrgasmProc(1, _ai.normalizedTime);
		}
		else if (_ai.IsName("D_Orgasm_A"))
		{
			FaintnessStartProcTrigger(num, _start: false, _infoAnimList.nDownPtn != 0);
			FaintnessStartProc(_start: false);
		}
		return true;
	}

	private bool Auto(AnimatorStateInfo _ai, HScene.AnimationListInfo _infoAnimList)
	{
		float num = UnityEngine.Input.GetAxis("Mouse ScrollWheel") * (float)((!sprite.IsSpriteOver()) ? 1 : 0);
		num = ((num < 0f) ? (0f - ctrlFlag.wheelActionCount) : ((!(num > 0f)) ? 0f : ctrlFlag.wheelActionCount));
		if (_ai.IsName("Idle"))
		{
			AutoStartProcTrigger(_start: false);
			AutoStartProc(_isReStart: false);
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
			AutoOLoopProc(0, num, _infoAnimList);
		}
		else if (_ai.IsName("Orgasm"))
		{
			OrgasmProc(0, _ai.normalizedTime);
		}
		else if (_ai.IsName("Orgasm_A"))
		{
			AutoStartProcTrigger(_start: true);
			AutoStartProc(_isReStart: true);
		}
		else if (_ai.IsName("D_Idle"))
		{
			FaintnessStartProcTrigger(num, _start: true, _infoAnimList.nDownPtn != 0);
			FaintnessStartProc(_start: true);
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
			OLoopProc(1, num, _infoAnimList);
		}
		else if (_ai.IsName("D_Orgasm"))
		{
			OrgasmProc(1, _ai.normalizedTime);
		}
		else if (_ai.IsName("D_Orgasm_A"))
		{
			FaintnessStartProcTrigger(num, _start: false, _infoAnimList.nDownPtn != 0);
			FaintnessStartProc(_start: false);
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
			if (chaFemales[1].visibleAll && !(chaFemales[i].objBodyBone == null))
			{
				if (!chaFemales[i].isPlayer)
				{
					animPar.heights[i] = chaFemales[i].GetShapeBodyValue(0);
				}
				else
				{
					animPar.heights[i] = 0.75f;
				}
			}
		}
		for (int j = 0; j < chaFemales.Length; j++)
		{
			if (chaFemales[j].visibleAll && !(chaFemales[j].objTop == null))
			{
				chaFemales[j].setAnimatorParamFloat("height", animPar.heights[j]);
				chaFemales[j].setAnimatorParamFloat("speed", animPar.speed);
				chaFemales[j].setAnimatorParamFloat("motion", animPar.m[j]);
				chaFemales[j].setAnimatorParamFloat("breast", animPar.breast);
				if (isHeight1Parameter)
				{
					chaFemales[j].setAnimatorParamFloat("height1", animPar.heights[j ^ 1]);
				}
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

	private bool StartProc(bool _isReStart)
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
			voice.PlaySoundETC("WLoop", 1, chaFemales[0], 0);
		}
		else if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(1))
		{
			voice.PlaySoundETC("WLoop", 2, chaFemales[0], 0);
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

	private bool FaintnessStartProcTrigger(float _wheel, bool _start, bool canFaintNess)
	{
		if (_wheel == 0f || nextPlay != 0)
		{
			return false;
		}
		if (!_start && !canFaintNess)
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

	private bool FaintnessStartProc(bool _start)
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
			voice.PlaySoundETC("D_WLoop", 1, chaFemales[0], 0);
		}
		else if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(1))
		{
			voice.PlaySoundETC("D_WLoop", 2, chaFemales[0], 0);
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

	private bool LoopProc(int _loop, int _state, float _wheel, HScene.AnimationListInfo _infoAnimList)
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
				num += Time.deltaTime * ctrlFlag.speedGuageRate;
				num *= (float)((!ctrlFlag.stopFeelFemal) ? 1 : 0);
				if (!ProcBase.hSceneManager.bMerchant)
				{
					if (ProcBase.hSceneManager.HSkil.ContainsValue(0) && ctrlFlag.nowAnimationInfo.lstSystem.Contains(2))
					{
						num *= ctrlFlag.SkilChangeSpeed(0);
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
						ctrlFlag.voice.playShorts[0] = 1;
					}
					if (_infoAnimList.nShortBreahtPlay == 1 || _infoAnimList.nShortBreahtPlay == 2)
					{
						ctrlFlag.voice.playShorts[1] = 0;
						ctrlFlag.voice.playShorts[1] = 1;
					}
				}
				ctrlFlag.voice.dialog = false;
				if (ctrlFlag.voice.playVoices[1] && ProcBase.hSceneManager.Agent[1] == null)
				{
					ctrlFlag.voice.dialog = true;
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

	private bool OLoopProc(int _state, float _wheel, HScene.AnimationListInfo _infoAnimList)
	{
		float num = 0f;
		ctrlFlag.speed = Mathf.Clamp01(ctrlFlag.speed + _wheel);
		ctrlFlag.nowSpeedStateFast = ctrlFlag.speed >= 0.5f;
		feelHit.ChangeHit(_infoAnimList.nFeelHit, 2);
		ctrlFlag.isGaugeHit = feelHit.isHit(_infoAnimList.nFeelHit, 2, ctrlFlag.speed);
		isAtariHit.Value = ctrlFlag.isGaugeHit;
		if (ctrlFlag.isGaugeHit)
		{
			num += Time.deltaTime * ctrlFlag.speedGuageRate;
			num *= (float)((!ctrlFlag.stopFeelFemal) ? 1 : 0);
			if (!ProcBase.hSceneManager.bMerchant)
			{
				if (ProcBase.hSceneManager.HSkil.ContainsValue(0) && ctrlFlag.nowAnimationInfo.lstSystem.Contains(2))
				{
					num *= ctrlFlag.SkilChangeSpeed(0);
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
					ctrlFlag.voice.playShorts[0] = 1;
				}
				if (_infoAnimList.nShortBreahtPlay == 1 || _infoAnimList.nShortBreahtPlay == 2)
				{
					ctrlFlag.voice.playShorts[1] = 0;
					ctrlFlag.voice.playShorts[1] = 1;
				}
			}
			ctrlFlag.voice.dialog = false;
			if (ctrlFlag.voice.playVoices[1] && ProcBase.hSceneManager.Agent[1] == null)
			{
				ctrlFlag.voice.dialog = true;
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
			if (ProcBase.hSceneManager.Player.ChaControl.sex == 1 && !ProcBase.hSceneManager.bFutanari)
			{
				ctrlFlag.numOrgasmFemalePlayer = Mathf.Clamp(ctrlFlag.numOrgasmFemalePlayer + 1, 0, 10);
			}
			ctrlFlag.nowOrgasm = true;
			ctrlFlag.voice.dialog = false;
			ctrlFlag.rateNip = 1f;
			if (Config.HData.Gloss)
			{
				ctrlFlag.rateTuya = 1f;
			}
			bool sio = Config.HData.Sio;
			if (!ProcBase.hSceneManager.bMerchant)
			{
				bool flag = Hitem.Effect(5);
				if (sio)
				{
					particle.Play(0);
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone && ctrlFlag.nowAnimationInfo.id == 0)
					{
						particle.Play(4);
					}
				}
				else if (((ctrlFlag.numFaintness == 0 && ctrlFlag.numOrgasm >= ctrlFlag.gotoFaintnessCount) || flag) && ProcBase.hSceneManager.GetFlaverSkillLevel(2) >= 100)
				{
					particle.Play(0);
				}
			}
			else if (sio)
			{
				particle.Play(0);
				if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone && ctrlFlag.nowAnimationInfo.id == 0)
				{
					particle.Play(4);
				}
			}
			bool urine = Config.HData.Urine;
			if (!ProcBase.hSceneManager.bMerchant)
			{
				if (urine || ProcBase.hSceneManager.Toilet >= 70f)
				{
					particle.Play(1);
					ctrlFlag.voice.urines[0] = true;
					if (urine && chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone && ctrlFlag.nowAnimationInfo.id == 0)
					{
						particle.Play(5);
						ctrlFlag.voice.urines[1] = true;
					}
					ProcBase.hSceneManager.Toilet = 30f;
					int desireKey = Desire.GetDesireKey(Desire.Type.Toilet);
					ProcBase.hSceneManager.Agent[0].SetDesire(desireKey, ProcBase.hSceneManager.Toilet);
					ctrlFlag.numUrine++;
				}
			}
			else if (urine)
			{
				particle.Play(1);
				ctrlFlag.voice.urines[0] = true;
				if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone)
				{
					particle.Play(5);
					ctrlFlag.voice.urines[1] = true;
				}
			}
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
		}
		return true;
	}

	private bool GotoFaintness(int _state)
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

	private bool OrgasmProc(int _state, float _normalizedTime)
	{
		if (_normalizedTime < 1f)
		{
			return false;
		}
		GotoFaintness(_state);
		return true;
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

	private bool AutoStartProc(bool _isReStart)
	{
		if (nextPlay == 0)
		{
			return false;
		}
		if (nextPlay == 1)
		{
			if (!_isReStart)
			{
				nextPlay = 3;
			}
			else
			{
				nextPlay = 2;
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
		setPlay("WLoop");
		if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(0))
		{
			voice.PlaySoundETC("WLoop", 1, chaFemales[0], 0);
		}
		else if (ctrlFlag.nowAnimationInfo.hasVoiceCategory.Contains(1))
		{
			voice.PlaySoundETC("WLoop", 2, chaFemales[0], 0);
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

	private bool AutoLoopProc(int _loop, int _state, float _wheel, HScene.AnimationListInfo _infoAnimList)
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
					if (chaFemales[j].visibleAll && !(chaFemales[j].objBodyBone == null) && (voice.nowVoices[j].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[j].state == HVoiceCtrl.VoiceKind.startVoice))
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
						if (chaFemales[k].visibleAll && !(chaFemales[k].objBodyBone == null) && (voice.nowVoices[k].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[k].state == HVoiceCtrl.VoiceKind.startVoice))
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
					ctrlFlag.voice.playShorts[0] = 1;
					ctrlFlag.voice.playShorts[1] = 0;
					ctrlFlag.voice.playShorts[1] = 1;
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

	private bool AutoOLoopProc(int _state, float _wheel, HScene.AnimationListInfo _infoAnimList)
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
					if (ProcBase.hSceneManager.HSkil.ContainsValue(0) && ctrlFlag.nowAnimationInfo.lstSystem.Contains(2))
					{
						num *= ctrlFlag.SkilChangeSpeed(0);
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
				if (ProcBase.hSceneManager.HSkil.ContainsValue(0) && ctrlFlag.nowAnimationInfo.lstSystem.Contains(2))
				{
					num *= ctrlFlag.SkilChangeSpeed(0);
				}
				if (ProcBase.hSceneManager.HSkil.ContainsValue(3))
				{
					num *= ctrlFlag.SkilChangeSpeed(3);
				}
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
				ctrlFlag.voice.playShorts[0] = 1;
				ctrlFlag.voice.playShorts[1] = 0;
				ctrlFlag.voice.playShorts[1] = 1;
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
			if (ProcBase.hSceneManager.Player.ChaControl.sex == 1)
			{
				ctrlFlag.numOrgasmFemalePlayer = Mathf.Clamp(ctrlFlag.numOrgasmFemalePlayer + 1, 0, 10);
			}
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
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone)
					{
						particle.Play(4);
					}
				}
				else if (((ctrlFlag.numFaintness == 0 && ctrlFlag.numOrgasm >= ctrlFlag.gotoFaintnessCount) || flag) && ProcBase.hSceneManager.GetFlaverSkillLevel(2) >= 100)
				{
					particle.Play(0);
				}
				if (urine || ProcBase.hSceneManager.Toilet >= 70f)
				{
					particle.Play(1);
					ctrlFlag.voice.urines[0] = true;
					if (urine && chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone)
					{
						particle.Play(5);
						ctrlFlag.voice.urines[1] = true;
					}
					ctrlFlag.numUrine++;
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
					if (chaFemales[1].visibleAll && (bool)chaFemales[1] && (bool)chaFemales[1].objBodyBone)
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
				}
			}
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
		}
		return true;
	}
}
