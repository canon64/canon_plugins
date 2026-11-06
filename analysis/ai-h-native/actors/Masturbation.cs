using System.Collections.Generic;
using AIProject;
using Manager;
using UnityEngine;

public class Masturbation : ProcBase
{
	private string nextAnimation;

	private float timeChangeSpeed;

	private RandomTimer[] aTimer = new RandomTimer[2];

	private animParm animPar;

	private List<string> row = new List<string>();

	private float[][] Timers;

	private bool bAuto = true;

	private readonly List<int> notAutoID = new List<int> { 3, 13, 14 };

	private bool oldHit;

	private int nextPlay;

	public Masturbation(DeliveryMember _delivery)
		: base(_delivery)
	{
		animPar.heights = new float[1];
		aTimer[0] = new RandomTimer();
		aTimer[1] = new RandomTimer();
		Timers = new float[2][]
		{
			new float[2],
			new float[2]
		};
		string strAssetHAutoListFolder = Singleton<HSceneManager>.Instance.strAssetHAutoListFolder;
		string text = "masturbation";
		bool flag = false;
		if (!GlobalMethod.AssetFileExist(strAssetHAutoListFolder, text, string.Empty))
		{
			return;
		}
		ExcelData excelData = CommonLib.LoadAsset<ExcelData>(strAssetHAutoListFolder, text, clone: false, string.Empty);
		Singleton<HSceneManager>.Instance.hashUseAssetBundle.Add(strAssetHAutoListFolder);
		if (excelData == null)
		{
			return;
		}
		int num = 2;
		while (num < excelData.MaxCell)
		{
			row.Clear();
			foreach (string item in excelData.list[num++].list)
			{
				row.Add(item);
			}
			int num2 = 0;
			Timers[0] = new float[2]
			{
				float.Parse(row.GetElement(num2++)),
				float.Parse(row.GetElement(num2++))
			};
			Timers[1] = new float[2]
			{
				float.Parse(row.GetElement(num2++)),
				float.Parse(row.GetElement(num2++))
			};
			aTimer[0].Init(Timers[0][0], Timers[0][0]);
			aTimer[1].Init(Timers[1][1], Timers[1][1]);
		}
		CatID = 4;
	}

	public override bool SetStartMotion(bool _isIdle, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		AtariEffect.Stop();
		TimerReset();
		int id = _infoAnimList.id;
		bAuto = !notAutoID.Contains(id);
		if (_isIdle)
		{
			setPlay("Idle", _isFade: false);
			ctrlFlag.loopType = -1;
			voice.HouchiTime = 0f;
		}
		else
		{
			if (ctrlFlag.feel_f >= 0.7f)
			{
				setPlay("OLoop", _isFade: false);
				ctrlFlag.loopType = -1;
			}
			else if (ctrlFlag.feel_f >= 0.5f)
			{
				setPlay("SLoop", _isFade: false);
				ctrlFlag.loopType = -1;
			}
			else if (ctrlFlag.feel_f >= 0.25f)
			{
				setPlay("MLoop", _isFade: false);
				ctrlFlag.loopType = -1;
			}
			else
			{
				setPlay("WLoop", _isFade: false);
				ctrlFlag.loopType = -1;
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
		ctrlFlag.speed = 1f;
		ctrlFlag.loopType = -1;
		oldHit = false;
		nextPlay = 0;
		return true;
	}

	public override bool Proc(int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		if (chaFemales[0].objTop == null)
		{
			return false;
		}
		AnimatorStateInfo animatorStateInfo = chaFemales[0].getAnimatorStateInfo(0);
		bool animatorParamBool = chaFemales[0].getAnimatorParamBool("change");
		if (!bAuto)
		{
			Manual(animatorStateInfo, animatorParamBool);
		}
		else
		{
			Auto(animatorStateInfo, animatorParamBool);
		}
		ctrlMeta.Proc(animatorStateInfo);
		int id = ctrlFlag.nowAnimationInfo.id;
		if (id == 3 || id == 13 || id == 14)
		{
			bool flag = false;
			flag = animatorStateInfo.IsName("WLoop") || animatorStateInfo.IsName("MLoop") || animatorStateInfo.IsName("SLoop");
			flag &= Config.HData.FinishButton;
			sprite.categoryFinish.SetActive(flag, 3);
		}
		setAnimationParamater();
		return true;
	}

	private void Manual(AnimatorStateInfo ai, bool isChangeTrigger)
	{
		float num = UnityEngine.Input.GetAxis("Mouse ScrollWheel") * (float)((!sprite.IsSpriteOver()) ? 1 : 0);
		num = ((num < 0f) ? (0f - ctrlFlag.wheelActionCount) : ((!(num > 0f)) ? 0f : ctrlFlag.wheelActionCount));
		bool flag = false;
		int id = ctrlFlag.nowAnimationInfo.id;
		if ((id == 3 || id == 13 || id == 14) && (ai.IsName("WLoop") || ai.IsName("MLoop") || ai.IsName("SLoop")) && ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishBefore)
		{
			ctrlFlag.speed = 0f;
			ctrlFlag.loopType = -1;
			nextAnimation = "OLoop";
			chaFemales[0].setAnimatorParamBool("change", _bFlag: true);
			item.setAnimatorParamBool("change", _bFlag: true);
			setPlay("OLoop");
			ctrlFlag.feel_f = 0.7f;
			flag = true;
			oldHit = false;
			ctrlFlag.isGaugeHit = false;
		}
		if (!flag)
		{
			ctrlFlag.speed += num;
			ctrlFlag.speed = Mathf.Clamp(ctrlFlag.speed, 0f, 1f);
			ctrlFlag.nowSpeedStateFast = ctrlFlag.speed > 0.5f;
			if (ai.IsName("Idle"))
			{
				StartProcTrigger(num);
				StartProcManual(0);
				voice.HouchiTime += Time.unscaledDeltaTime;
			}
			else if (ai.IsName("WLoop"))
			{
				GotoNextLoop(0.25f, isChangeTrigger, "MLoop", ctrlFlag.nowAnimationInfo, 0);
			}
			else if (ai.IsName("MLoop"))
			{
				GotoNextLoop(0.5f, isChangeTrigger, "SLoop", ctrlFlag.nowAnimationInfo, 0);
			}
			else if (ai.IsName("SLoop"))
			{
				GotoNextLoop(0.7f, isChangeTrigger, "OLoop", ctrlFlag.nowAnimationInfo, 1);
			}
			else if (ai.IsName("OLoop"))
			{
				ctrlFlag.isGaugeHit = feelHit.isHit(ctrlFlag.nowAnimationInfo.nFeelHit, 2, ctrlFlag.speed);
				isAtariHit.Value = ctrlFlag.isGaugeHit;
				if (ctrlFlag.isGaugeHit)
				{
					float num2 = 0f;
					num2 = ctrlFlag.speedGuageRate * Time.deltaTime;
					num2 *= (float)((!ctrlFlag.stopFeelFemal) ? 1 : 0);
					if (!ProcBase.hSceneManager.bMerchant && ProcBase.hSceneManager.HSkil.ContainsValue(3))
					{
						num2 *= ctrlFlag.SkilChangeSpeed(3);
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
					if (!ctrlFlag.nowAnimationInfo.lstSystem.Contains(0) && (ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 1 || ctrlFlag.nowAnimationInfo.nShortBreahtPlay == 3))
					{
						ctrlFlag.voice.playShorts[0] = 0;
					}
					ctrlFlag.voice.dialog = false;
				}
				oldHit = ctrlFlag.isGaugeHit;
				if (ctrlFlag.selectAnimationListInfo == null && ctrlFlag.feel_f >= 1f)
				{
					setPlay("Orgasm");
					ctrlFlag.speed = 0f;
					ctrlFlag.loopType = -1;
					if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 3 && ctrlFlag.nowAnimationInfo.id == 13)
					{
						ctrlFlag.AddParam(18, 1);
					}
					else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 3 && ctrlFlag.nowAnimationInfo.id == 14)
					{
						ctrlFlag.AddParam(36, 1);
					}
					ctrlFlag.feel_f = 0f;
					ctrlFlag.numOrgasm = Mathf.Clamp(ctrlFlag.numOrgasm + 1, 0, 10);
					ctrlFlag.AddOrgasm();
					ctrlFlag.voice.oldFinish = 0;
					sprite.objMotionListPanel.SetActive(value: false);
					sprite.SetEnableCategoryMain(_enable: false);
					sprite.SetEnableHItem(_enable: false);
					ctrlFlag.nowOrgasm = true;
					ctrlFlag.isGaugeHit = false;
					isAtariHit.Value = ctrlFlag.isGaugeHit;
					ctrlFlag.rateNip = 1f;
					if (Config.HData.Gloss)
					{
						ctrlFlag.rateTuya = 1f;
					}
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
					}
					else if (sio)
					{
						particle.Play(0);
					}
				}
			}
			else if (ai.IsName("Orgasm"))
			{
				bool flag3 = (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.startVoice) && Singleton<Voice>.Instance.IsVoiceCheck(ctrlFlag.voice.voiceTrs[0]);
				if (ai.normalizedTime >= 1f && !flag3)
				{
					setPlay("Orgasm_A");
					ctrlFlag.speed = 0f;
					ctrlFlag.loopType = -1;
					ctrlFlag.nowOrgasm = false;
				}
			}
			else if (ai.IsName("Orgasm_A"))
			{
				StartProcTrigger(num);
				StartProcManual(1);
			}
		}
		if (isChangeTrigger && ai.IsName(nextAnimation))
		{
			chaFemales[0].setAnimatorParamBool("change", _bFlag: false);
		}
	}

	private void Auto(AnimatorStateInfo ai, bool isChangeTrigger)
	{
		bool flag = false;
		int id = ctrlFlag.nowAnimationInfo.id;
		if ((id == 3 || id == 13 || id == 14) && (ai.IsName("WLoop") || ai.IsName("MLoop") || ai.IsName("SLoop")) && ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishBefore)
		{
			ctrlFlag.speed = 1f;
			ctrlFlag.loopType = -1;
			nextAnimation = "OLoop";
			chaFemales[0].setAnimatorParamBool("change", _bFlag: true);
			item.setAnimatorParamBool("change", _bFlag: true);
			setPlay("OLoop");
			ctrlFlag.feel_f = 0.75f;
			flag = true;
		}
		if (!flag)
		{
			if (ai.IsName("Idle"))
			{
				StartProc(0);
			}
			else if (ai.IsName("WLoop"))
			{
				GotoNextLoop(0.25f, isChangeTrigger, "MLoop");
				ctrlFlag.speed = 1f;
				ctrlFlag.loopType = -1;
			}
			else if (ai.IsName("MLoop"))
			{
				GotoNextLoop(0.5f, isChangeTrigger, "SLoop");
				ctrlFlag.speed = 1f;
				ctrlFlag.loopType = -1;
			}
			else if (ai.IsName("SLoop"))
			{
				GotoNextLoop(0.75f, isChangeTrigger, "OLoop");
				ctrlFlag.speed = 1f;
				ctrlFlag.loopType = -1;
			}
			else if (ai.IsName("OLoop"))
			{
				float num = 0f;
				num = ctrlFlag.speedGuageRate * Time.deltaTime;
				num *= (float)((!ctrlFlag.stopFeelFemal) ? 1 : 0);
				if (!ProcBase.hSceneManager.bMerchant && ProcBase.hSceneManager.HSkil.ContainsValue(3))
				{
					num *= ctrlFlag.SkilChangeSpeed(3);
				}
				ctrlFlag.feel_f += num;
				ctrlFlag.feel_f = Mathf.Clamp01(ctrlFlag.feel_f);
				if (ctrlFlag.selectAnimationListInfo == null && ctrlFlag.feel_f >= 1f)
				{
					setPlay("Orgasm");
					ctrlFlag.speed = 1f;
					ctrlFlag.loopType = -1;
					if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 3 && ctrlFlag.nowAnimationInfo.id == 13)
					{
						ctrlFlag.AddParam(18, 1);
					}
					else if (ctrlFlag.nowAnimationInfo.ActionCtrl.Item1 == 3 && ctrlFlag.nowAnimationInfo.id == 14)
					{
						ctrlFlag.AddParam(36, 1);
					}
					ctrlFlag.feel_f = 0f;
					ctrlFlag.numOrgasm = Mathf.Clamp(ctrlFlag.numOrgasm + 1, 0, 10);
					ctrlFlag.AddOrgasm();
					ctrlFlag.voice.oldFinish = 0;
					sprite.objMotionListPanel.SetActive(value: false);
					sprite.SetEnableCategoryMain(_enable: false);
					sprite.SetEnableHItem(_enable: false);
					ctrlFlag.nowOrgasm = true;
					ctrlFlag.rateNip = 1f;
					if (Config.HData.Gloss)
					{
						ctrlFlag.rateTuya = 1f;
					}
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
					}
					else if (sio)
					{
						particle.Play(0);
					}
				}
			}
			else if (ai.IsName("Orgasm"))
			{
				if (ai.normalizedTime >= 1f)
				{
					setPlay("Orgasm_A");
					ctrlFlag.speed = 1f;
					ctrlFlag.loopType = -1;
					ctrlFlag.nowOrgasm = false;
				}
			}
			else if (ai.IsName("Orgasm_A"))
			{
				StartProc(1);
			}
		}
		if (isChangeTrigger && ai.IsName(nextAnimation))
		{
			chaFemales[0].setAnimatorParamBool("change", _bFlag: false);
		}
	}

	public override void setAnimationParamater()
	{
		animPar.speed = ((!bAuto) ? (ctrlFlag.speed + 1f) : ctrlFlag.speed);
		if (chaFemales[0].visibleAll && chaFemales[0].objTop != null)
		{
			animPar.heights[0] = chaFemales[0].GetShapeBodyValue(0);
			chaFemales[0].setAnimatorParamFloat("height", animPar.heights[0]);
			chaFemales[0].setAnimatorParamFloat("speed", animPar.speed);
		}
		if (item.GetItem() != null)
		{
			item.setAnimatorParamFloat("height", animPar.heights[0]);
			item.setAnimatorParamFloat("speed", animPar.speed);
		}
	}

	private void setPlay(string _playAnimation, bool _isFade = true)
	{
		chaFemales[0].setPlay(_playAnimation, 0);
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

	private bool StartProc(int _start)
	{
		if (!aTimer[_start].Check())
		{
			return false;
		}
		setPlay("WLoop");
		ctrlFlag.speed = 1f;
		ctrlFlag.loopType = 0;
		ctrlFlag.isNotCtrl = false;
		timeChangeSpeed = 0f;
		chaFemales[0].setAnimatorParamBool("change", _bFlag: false);
		item.setAnimatorParamBool("change", _bFlag: false);
		if (_start == 1)
		{
			voice.AfterFinish();
		}
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

	private bool StartProcManual(int _start)
	{
		if (nextPlay == 0)
		{
			return false;
		}
		if (nextPlay == 1)
		{
			nextPlay = 2;
			if (_start == 0)
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
		ctrlFlag.speed = 0f;
		ctrlFlag.loopType = 0;
		ctrlFlag.isNotCtrl = false;
		timeChangeSpeed = 0f;
		chaFemales[0].setAnimatorParamBool("change", _bFlag: false);
		item.setAnimatorParamBool("change", _bFlag: false);
		if (_start == 1)
		{
			voice.AfterFinish();
		}
		oldHit = false;
		return true;
	}

	private bool GotoNextLoop(float _range, bool _isChangeTrigger, string _nextAnimation)
	{
		float num = 0f;
		num = ctrlFlag.speedGuageRate * Time.deltaTime;
		num *= (float)((!ctrlFlag.stopFeelFemal) ? 1 : 0);
		if (!ProcBase.hSceneManager.bMerchant && ProcBase.hSceneManager.HSkil.ContainsValue(3))
		{
			num *= ctrlFlag.SkilChangeSpeed(3);
		}
		ctrlFlag.feel_f += num;
		ctrlFlag.feel_f = Mathf.Clamp01(ctrlFlag.feel_f);
		if (ctrlFlag.feel_f < _range || _isChangeTrigger)
		{
			return false;
		}
		ctrlFlag.speed = 1f;
		nextAnimation = _nextAnimation;
		chaFemales[0].setAnimatorParamBool("change", _bFlag: true);
		item.setAnimatorParamBool("change", _bFlag: true);
		setPlay(_nextAnimation);
		return true;
	}

	private bool GotoNextLoop(float _range, bool _isChangeTrigger, string _nextAnimation, HScene.AnimationListInfo _infoAnimList, int _loop)
	{
		ctrlFlag.isGaugeHit = feelHit.isHit(_infoAnimList.nFeelHit, _loop, ctrlFlag.speed);
		isAtariHit.Value = ctrlFlag.isGaugeHit;
		if (ctrlFlag.isGaugeHit)
		{
			float num = 0f;
			num = ctrlFlag.speedGuageRate * Time.deltaTime;
			num *= (float)((!ctrlFlag.stopFeelFemal) ? 1 : 0);
			if (!ProcBase.hSceneManager.bMerchant && ProcBase.hSceneManager.HSkil.ContainsValue(3))
			{
				num *= ctrlFlag.SkilChangeSpeed(3);
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
			if (!ctrlFlag.nowAnimationInfo.lstSystem.Contains(0) && (_infoAnimList.nShortBreahtPlay == 1 || _infoAnimList.nShortBreahtPlay == 3))
			{
				ctrlFlag.voice.playShorts[0] = 0;
			}
			ctrlFlag.voice.dialog = false;
		}
		oldHit = ctrlFlag.isGaugeHit;
		if (ctrlFlag.feel_f < _range || _isChangeTrigger)
		{
			return false;
		}
		ctrlFlag.speed = 0f;
		ctrlFlag.loopType = -1;
		nextAnimation = _nextAnimation;
		chaFemales[0].setAnimatorParamBool("change", _bFlag: true);
		item.setAnimatorParamBool("change", _bFlag: true);
		setPlay(_nextAnimation);
		oldHit = false;
		return true;
	}

	private void TimerReset()
	{
		aTimer[0] = new RandomTimer();
		aTimer[1] = new RandomTimer();
		aTimer[0].Init(Timers[0][0], Timers[0][0]);
		aTimer[1].Init(Timers[1][1], Timers[1][1]);
	}
}
