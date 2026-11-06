using AIProject.Definitions;
using Manager;
using UnityEngine;

public class Spnking : ProcBase
{
	private bool upFeel;

	private float backupFeel;

	private float timeFeelUp;

	private animParm animPar;

	public Spnking(DeliveryMember _delivery)
		: base(_delivery)
	{
		animPar.heights = new float[1];
		CatID = 3;
	}

	public override bool SetStartMotion(bool _isIdle, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		AtariEffect.Stop();
		if (_isIdle)
		{
			setPlay((!ctrlFlag.isFaintness) ? "WIdle" : "D_Orgasm_A", _isFade: false);
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			voice.HouchiTime = 0f;
		}
		else
		{
			if (ctrlFlag.isFaintness)
			{
				setPlay("D_Orgasm_A", _isFade: false);
			}
			else
			{
				setPlay((!(ctrlFlag.feel_f >= 0.5f)) ? "WIdle" : "SIdle", _isFade: false);
			}
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			voice.HouchiTime = 0f;
			ctrlFlag.motions[0] = 0f;
			ctrlFlag.motions[1] = 0f;
			if (_infoAnimList.lstSystem.Contains(4))
			{
				ctrlFlag.isPainActionParam = true;
			}
		}
		isHeight1Parameter = chaFemales[0].IsParameterInAnimator("height1");
		return true;
	}

	public override bool Proc(int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		float num = 0f;
		if (chaMales[0].objTop == null || chaFemales[0].objTop == null)
		{
			return false;
		}
		FemaleAi = chaFemales[0].getAnimatorStateInfo(0);
		if (FemaleAi.IsName("WIdle"))
		{
			SpankingProc(0);
			voice.HouchiTime += Time.unscaledDeltaTime;
		}
		else if (FemaleAi.IsName("WAction"))
		{
			ActionProc(FemaleAi.normalizedTime, 0, _infoAnimList);
		}
		if (FemaleAi.IsName("SIdle"))
		{
			SpankingProc(1);
			voice.HouchiTime += Time.unscaledDeltaTime;
		}
		else if (FemaleAi.IsName("SAction"))
		{
			ActionProc(FemaleAi.normalizedTime, 1, _infoAnimList);
		}
		else if (FemaleAi.IsName("Orgasm"))
		{
			AfterWaitingAnimation(FemaleAi.normalizedTime, 0);
		}
		else if (FemaleAi.IsName("D_Idle"))
		{
			SpankingProc(2);
			voice.HouchiTime += Time.unscaledDeltaTime;
		}
		else if (FemaleAi.IsName("D_Action"))
		{
			ActionProc(FemaleAi.normalizedTime, 2, _infoAnimList);
		}
		else if (FemaleAi.IsName("D_Orgasm"))
		{
			AfterWaitingAnimation(FemaleAi.normalizedTime, 1);
		}
		else if (FemaleAi.IsName("D_Orgasm_A"))
		{
			SpankingProc(2);
		}
		if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.RecoverFaintness)
		{
			if (FemaleAi.IsName("D_Orgasm_A"))
			{
				setPlay("WIdle");
				voice.HouchiTime = 0f;
				ctrlFlag.isFaintness = false;
				sprite.SetVisibleLeaveItToYou(_visible: true, _judgeLeaveItToYou: true);
				ctrlFlag.numOrgasm = 0;
				sprite.SetAnimationMenu();
				sprite.SetMotionListDraw(_active: false);
				Hitem.SetUse(6, use: false);
			}
			else
			{
				ctrlFlag.click = HSceneFlagCtrl.ClickKind.None;
				Hitem.SetUse(6, use: false);
			}
		}
		if (upFeel)
		{
			timeFeelUp = Mathf.Clamp(timeFeelUp + Time.deltaTime, 0f, 0.3f);
			float num2 = Mathf.Clamp01(timeFeelUp / 0.3f);
			if (!ctrlFlag.stopFeelFemal)
			{
				num = Mathf.Lerp(backupFeel, backupFeel + ctrlFlag.feelSpnking, num2);
				if (!ProcBase.hSceneManager.bMerchant && ProcBase.hSceneManager.HSkil.ContainsValue(3))
				{
					num *= ctrlFlag.SkilChangeSpeed(3);
				}
				ctrlFlag.feel_f += num;
				ctrlFlag.feel_f = Mathf.Clamp01(ctrlFlag.feel_f);
			}
			if (num2 >= 1f)
			{
				upFeel = false;
			}
		}
		ctrlMeta.Proc(FemaleAi);
		setAnimationParamater();
		return true;
	}

	public override void setAnimationParamater()
	{
		if (chaFemales[0].visibleAll && chaFemales[0].objTop == null)
		{
			animPar.heights[0] = chaFemales[0].GetShapeBodyValue(0);
			chaFemales[0].setAnimatorParamFloat("height", animPar.heights[0]);
		}
		if (chaMales[0].objTop != null)
		{
			chaMales[0].setAnimatorParamFloat("height", animPar.heights[0]);
		}
		if (item.GetItem() != null)
		{
			item.setAnimatorParamFloat("height", animPar.heights[0]);
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

	private bool SpankingProc(int _state)
	{
		if (!ctrlFlag.stopFeelFemal)
		{
			ctrlFlag.feel_f = Mathf.Clamp01(ctrlFlag.feel_f - ctrlFlag.guageDecreaseRate * Time.deltaTime);
		}
		if (_state == 1 && ctrlFlag.feel_f < 0.5f)
		{
			setPlay("WIdle");
			voice.HouchiTime = 0f;
			return true;
		}
		float num = Singleton<Manager.Input>.Instance.ScrollValue() * (float)((!sprite.IsSpriteOver()) ? 1 : 0);
		if (num == 0f)
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
		setPlay(_state switch
		{
			0 => "WAction", 
			1 => "SAction", 
			_ => "D_Action", 
		}, _isFade: false);
		upFeel = true;
		timeFeelUp = 0f;
		backupFeel = ctrlFlag.feel_f;
		ctrlFlag.isNotCtrl = false;
		if (randVoicePlays[0].Get() == 0)
		{
			ctrlFlag.voice.playVoices[0] = true;
		}
		else if (randVoicePlays[1].Get() == 0)
		{
			ctrlFlag.voice.playVoices[1] = true;
		}
		ctrlFlag.voice.playShorts[0] = 0;
		return true;
	}

	private bool ActionProc(float _normalizedTime, int _state, HScene.AnimationListInfo _infoAnimList)
	{
		if (_normalizedTime < 1f)
		{
			return false;
		}
		if (_state == 0)
		{
			setPlay((!((double)ctrlFlag.feel_f >= 0.5)) ? "WIdle" : "SIdle", _isFade: false);
			voice.HouchiTime = 0f;
		}
		else if (ctrlFlag.selectAnimationListInfo == null && ctrlFlag.feel_f >= 1f)
		{
			setPlay((_state != 1) ? "D_Orgasm" : "Orgasm");
			ctrlFlag.feel_f = 0f;
			ctrlFlag.numOrgasm = Mathf.Clamp(ctrlFlag.numOrgasm + 1, 0, 10);
			ctrlFlag.AddOrgasm();
			sprite.objMotionListPanel.SetActive(value: false);
			sprite.SetEnableCategoryMain(_enable: false);
			sprite.SetEnableHItem(_enable: false);
			ctrlFlag.voice.oldFinish = 0;
			ctrlFlag.nowOrgasm = true;
			if (_state != 0)
			{
				ctrlFlag.AddParam(15, 1);
			}
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
		else
		{
			setPlay((_state != 1) ? "D_Orgasm_A" : "SIdle", _isFade: false);
		}
		return true;
	}

	private bool AfterWaitingAnimation(float _normalizedTime, int _state)
	{
		if (_normalizedTime < 1f)
		{
			return false;
		}
		bool flag = Hitem.Effect(5);
		if (_state == 0 && (ctrlFlag.numOrgasm >= ctrlFlag.gotoFaintnessCount || flag))
		{
			setPlay("D_Orgasm_A");
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
			sprite.SetAnimationMenu();
		}
		else
		{
			setPlay((_state != 0) ? "D_Orgasm_A" : "WIdle", _isFade: false);
			voice.HouchiTime = 0f;
		}
		ctrlFlag.nowOrgasm = false;
		voice.AfterFinish();
		return true;
	}
}
