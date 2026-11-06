using System.Collections.Generic;
using System.Linq;
using Manager;
using UnityEngine;

public class HSonyu : HActionBase
{
	private bool isAuto;

	private float oldGaugeMale;

	private float oldGaugeFemale;

	private int rePlay;

	private int kindInsert;

	private int gotoInAfter;

	private bool isSW;

	private bool[] is70Voices = new bool[2];

	public HSonyu(HFlag.DeliveryMember _membar)
		: base(_membar)
	{
	}

	public override bool MotionChange(int _motion)
	{
		SetPlay("Idle");
		rePlay = 0;
		flags.isAnalPlay = false;
		sprite.HoushiInitRely();
		isAuto = false;
		Color color = sprite.sonyu.imgAuto.color;
		color.a = (isAuto ? 1f : 0f);
		sprite.sonyu.imgAuto.color = color;
		sprite.imageSpeedSlliderCover70.enabled = false;
		sprite.sonyu.imageCtrlPad.OnChangeValue(-1);
		isSW = false;
		flags.isCondom = sprite.sonyu.isCondom;
		if ((bool)male && male.chaFile != null)
		{
			male.chaFile.status.visibleGomu = flags.isCondom;
		}
		flags.voice.SetSonyuWaitTime(_isIdle: true);
		flags.voice.SetSonyuIdleTime();
		flags.isInsideFinish = false;
		flags.isInsideFinishCondom = false;
		flags.speedCalc = 0f;
		flags.isDenialvoiceWait = false;
		if (flags.selectAnimationListInfo == null)
		{
			flags.voice.isFemale70PercentageVoicePlay = false;
			flags.voice.isMale70PercentageVoicePlay = false;
			flags.voice.isAfterVoicePlay = false;
			is70Voices[0] = false;
			is70Voices[1] = false;
		}
		else if (flags.selectAnimationListInfo.mode != flags.nowAnimationInfo.mode)
		{
			flags.voice.isFemale70PercentageVoicePlay = false;
			flags.voice.isMale70PercentageVoicePlay = false;
			flags.voice.isAfterVoicePlay = false;
			is70Voices[0] = false;
			is70Voices[1] = false;
		}
		SetAnimatorFloat("speed", flags.speed);
		SetAnimatorFloat("speedHand", 1f + flags.speedItem);
		SetAnimatorFloat("Breast", female.GetShapeBodyValue(4), _isMale: false);
		SetDefaultAnimatorFloat();
		return true;
	}

	public override bool Proc()
	{
		if (flags.selectAnimationListInfo == null)
		{
			hand.Proc();
		}
		flags.WaitSpeedProcItem();
		if (!hand.IsAction())
		{
			SetMoveCameraFlag();
		}
		male.chaFile.status.visibleBodyAlways = !IsBodyTouch();
		AnimatorStateInfo animatorStateInfo = female.getAnimatorStateInfo(0);
		bool num = flags.gaugeFemale < 70f;
		bool flag = flags.gaugeMale < 70f;
		if (num && flags.voice.isFemale70PercentageVoicePlay)
		{
			flags.voice.isFemale70PercentageVoicePlay = false;
		}
		if (flag && flags.voice.isMale70PercentageVoicePlay)
		{
			flags.voice.isMale70PercentageVoicePlay = false;
		}
		if (!num && !flags.voice.isFemale70PercentageVoicePlay && flags.voice.IsSonyuIdleFlag() && !is70Voices[0])
		{
			flags.voice.SetSonyuWaitTime(_isIdle: false);
			flags.voice.SetSonyuIdleTime();
			is70Voices[0] = true;
		}
		else if (!flag && !flags.voice.isMale70PercentageVoicePlay && flags.voice.IsSonyuIdleFlag() && !is70Voices[1])
		{
			flags.voice.SetSonyuWaitTime(_isIdle: false);
			flags.voice.SetSonyuIdleTime();
			is70Voices[1] = true;
		}
		int num2 = (flags.nowAnimationInfo.isFemaleInitiative ? 38 : 0);
		if (animatorStateInfo.IsName("Idle") || animatorStateInfo.IsName("A_Idle"))
		{
			if (IsVoiceWait())
			{
				return false;
			}
			if (!hand.IsAction())
			{
				flags.FemaleGaugeUp((0f - Time.deltaTime) * flags.rateDecreaseGauge, _force: false, _isIdle: false);
			}
			flags.MaleGaugeUp((0f - Time.deltaTime) * flags.rateDecreaseGauge);
			switch (rePlay)
			{
			case 0:
				if (flags.isDenialvoiceWait && voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.breath)
				{
					if (!flags.isCondom)
					{
						flags.isCondom = true;
						male.chaFile.status.visibleGomu = true;
						sprite.SetCondom(_isOn: true);
					}
					flags.isDenialvoiceWait = false;
				}
				if (flags.click == HFlag.ClickKind.insert || flags.click == HFlag.ClickKind.insert_voice)
				{
					rePlay = 1;
					kindInsert = ((flags.click != HFlag.ClickKind.insert) ? 1 : 0);
					flags.voiceWait = true;
				}
				else if (flags.click == HFlag.ClickKind.insert_anal || flags.click == HFlag.ClickKind.insert_anal_voice)
				{
					rePlay = 2;
					kindInsert = ((flags.click != HFlag.ClickKind.insert_anal) ? 1 : 0);
					flags.voiceWait = true;
				}
				else if (!hand.IsAction() && voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.breath && flags.voice.IsSonyuIdleTime())
				{
					flags.voice.playVoices[0] = 300 + num2;
					if (!flags.voice.IsSonyuIdleFlag())
					{
						flags.voice.SetSonyuWaitTime(_isIdle: true);
						flags.voice.SetSonyuIdleTime();
					}
				}
				break;
			case 1:
				if (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.breath || (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.voice && !Voice.IsPlay(flags.transVoiceMouth[0])))
				{
					flags.voiceWait = false;
					SetPlay("Insert");
					sprite.SetSonyuActionButtonActive(_active: false);
					flags.isAnalPlay = false;
					if (!flags.isCondom)
					{
						flags.SetNameInsert();
					}
					flags.AddSonyuKokanPlay();
					if (kindInsert == 0)
					{
						flags.voice.playVoices[0] = 307 + num2;
					}
					else
					{
						flags.voice.playVoices[0] = 308 + num2;
					}
				}
				break;
			case 2:
				if (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.breath || (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.voice && !Voice.IsPlay(flags.transVoiceMouth[0])))
				{
					flags.voiceWait = false;
					SetPlay("A_Insert");
					sprite.SetSonyuActionButtonActive(_active: false);
					flags.isAnalPlay = true;
					flags.AddSonyuAnalPlay();
					if (kindInsert == 0)
					{
						flags.voice.playVoices[0] = 307 + num2;
					}
					else
					{
						flags.voice.playVoices[0] = 308 + num2;
					}
				}
				break;
			}
		}
		else if (animatorStateInfo.IsName("Insert") || animatorStateInfo.IsName("A_Insert"))
		{
			if (animatorStateInfo.normalizedTime > 1f)
			{
				SetPlay((!flags.isAnalPlay) ? "InsertIdle" : "A_InsertIdle");
				sprite.SetSonyuActionButtonActive(_active: true, 6);
				sprite.sonyu.imageCtrlPad.OnChangeValue(0);
				flags.finish = HFlag.FinishKind.none;
				flags.voice.SetSonyuIdleTime();
				if (!flags.isAnalPlay)
				{
					flags.SetInsertKokan();
				}
				else
				{
					flags.SetInsertAnal();
				}
			}
		}
		else if (animatorStateInfo.IsName("InsertIdle") || animatorStateInfo.IsName("A_InsertIdle"))
		{
			if (IsVoiceWait())
			{
				if (flags.voiceWait)
				{
					if (!flags.isAnalPlay)
					{
						flags.SetInsertKokanVoiceCondition();
					}
					else
					{
						flags.SetInsertAnalVoiceCondition();
					}
				}
				return false;
			}
			if (!hand.IsAction())
			{
				flags.FemaleGaugeUp((0f - Time.deltaTime) * flags.rateDecreaseGauge, _force: false, _isIdle: false);
			}
			flags.MaleGaugeUp((0f - Time.deltaTime) * flags.rateDecreaseGauge);
			if (flags.click == HFlag.ClickKind.speedup)
			{
				flags.SpeedUpClick(flags.rateSpeedUp, 1f);
				sprite.SetSonyuActionButtonActive(_active: false, 6);
				SetPlay((!flags.isAnalPlay) ? "WLoop" : "A_WLoop");
				flags.voice.SetSonyuIdleTime();
				if (!flags.isAnalPlay)
				{
					flags.SetInsertKokanVoiceCondition();
				}
				else
				{
					flags.SetInsertAnalVoiceCondition();
				}
				isSW = false;
			}
			else if (flags.click == HFlag.ClickKind.modeChange)
			{
				isAuto = !isAuto;
				Color color = sprite.sonyu.imgAuto.color;
				color.a = (isAuto ? 1f : 0f);
				sprite.sonyu.imgAuto.color = color;
				sprite.SetSonyuActionButtonActive(_active: false, 6);
				sprite.sonyu.imageCtrlPad.OnChangeValue(1);
				SetPlay((!flags.isAnalPlay) ? "WLoop" : "A_WLoop");
				flags.voice.SetSonyuIdleTime();
				if (!flags.isAnalPlay)
				{
					flags.SetInsertKokanVoiceCondition();
				}
				else
				{
					flags.SetInsertAnalVoiceCondition();
				}
				isSW = false;
			}
			else if (flags.click == HFlag.ClickKind.pull)
			{
				SetPlay((!flags.isAnalPlay) ? "Idle" : "A_Idle");
				sprite.SetSonyuStart();
				sprite.sonyu.imageCtrlPad.OnChangeValue(-1);
				flags.voice.SetSonyuIdleTime();
				if (!flags.isAnalPlay)
				{
					flags.SetInsertKokanVoiceCondition();
				}
				else
				{
					flags.SetInsertAnalVoiceCondition();
				}
				rePlay = 0;
				flags.voice.playVoices[0] = ((!flags.isAnalPlay) ? 303 : 306) + num2;
			}
			else if (!hand.IsAction() && voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.breath && flags.voice.IsSonyuIdleTime())
			{
				flags.voice.playVoices[0] = 309 + num2;
				if (!flags.voice.IsSonyuIdleFlag())
				{
					flags.voice.SetSonyuWaitTime(_isIdle: true);
					flags.voice.SetSonyuIdleTime();
				}
			}
		}
		else if (animatorStateInfo.IsName("WLoop") || animatorStateInfo.IsName("A_WLoop"))
		{
			LoopProc(_loop: false);
		}
		else if (animatorStateInfo.IsName("SLoop") || animatorStateInfo.IsName("A_SLoop"))
		{
			LoopProc(_loop: true);
		}
		else if (animatorStateInfo.IsName("OLoop") || animatorStateInfo.IsName("A_OLoop"))
		{
			if (!Voice.IsPlay(flags.transVoiceMouth[0]))
			{
				if (flags.finish == HFlag.FinishKind.inside)
				{
					if (!flags.isAnalPlay)
					{
						if (!flags.isCondom)
						{
							flags.AddSonyuInside();
						}
						else
						{
							flags.AddSonyuCondomInside();
						}
					}
					else if (!flags.isCondom)
					{
						flags.AddSonyuAnalInside();
					}
					else
					{
						flags.AddSonyuAnalCondomInside();
					}
					SetPlay((!flags.isAnalPlay) ? "M_IN_Start" : "A_M_IN_Start");
					if (flags.lstHeroine[0].hCount == 0 && !flags.isFreeH && (bool)sprite.objFirstHHelpBase && sprite.objFirstHHelpBase.activeSelf && !flags.isAnalPlay)
					{
						sprite.objFirstHHelpBase.SetActive(value: false);
					}
					flags.voice.playVoices[0] = 324 + num2;
					flags.isInsideFinish = true;
					flags.isInsideFinishCondom = flags.isCondom;
				}
				else if (flags.finish == HFlag.FinishKind.outside)
				{
					if (!flags.isAnalPlay)
					{
						flags.AddSonyuOutside();
					}
					else
					{
						flags.AddSonyuAnalOutside();
					}
					SetPlay((!flags.isAnalPlay) ? "M_OUT_Start" : "A_M_OUT_Start");
					if (flags.lstHeroine[0].hCount == 0 && !flags.isFreeH && (bool)sprite.objFirstHHelpBase && sprite.objFirstHHelpBase.activeSelf && !flags.isAnalPlay)
					{
						sprite.objFirstHHelpBase.SetActive(value: false);
					}
					flags.voice.playVoices[0] = 334 + num2;
				}
				else if (flags.finish == HFlag.FinishKind.orgW)
				{
					if (!flags.isAnalPlay)
					{
						flags.AddSonyuOrg();
					}
					else
					{
						flags.AddSonyuAnalOrg();
					}
					SetPlay((!flags.isAnalPlay) ? "WF_IN_Start" : "A_WF_IN_Start");
					if (flags.lstHeroine[0].hCount == 0 && !flags.isFreeH && (bool)sprite.objFirstHHelpBase && sprite.objFirstHHelpBase.activeSelf && !flags.isAnalPlay)
					{
						sprite.objFirstHHelpBase.SetActive(value: false);
					}
					gotoInAfter = 0;
					flags.voice.playVoices[0] = 325 + num2;
				}
				else if (flags.finish == HFlag.FinishKind.sameW)
				{
					if (!flags.isAnalPlay)
					{
						if (!flags.isCondom)
						{
							flags.AddSonyuSame();
							flags.AddSonyuInside();
						}
						else
						{
							flags.AddSonyuCondomSame();
							flags.AddSonyuCondomInside();
						}
						flags.AddSonyuOrg();
					}
					else
					{
						if (!flags.isCondom)
						{
							flags.AddSonyuAnalSame();
							flags.AddSonyuAnalInside();
						}
						else
						{
							flags.AddSonyuAnalCondomSame();
							flags.AddSonyuAnalCondomInside();
						}
						flags.AddSonyuAnalOrg();
					}
					SetPlay((!flags.isAnalPlay) ? "WS_IN_Start" : "A_WS_IN_Start");
					if (flags.lstHeroine[0].hCount == 0 && !flags.isFreeH && (bool)sprite.objFirstHHelpBase && sprite.objFirstHHelpBase.activeSelf && !flags.isAnalPlay)
					{
						sprite.objFirstHHelpBase.SetActive(value: false);
					}
					gotoInAfter = 0;
					flags.voice.playVoices[0] = 325 + num2;
					flags.isInsideFinish = true;
					flags.isInsideFinishCondom = flags.isCondom;
				}
				else if (flags.finish == HFlag.FinishKind.orgS)
				{
					if (!flags.isAnalPlay)
					{
						flags.AddSonyuOrg();
					}
					else
					{
						flags.AddSonyuAnalOrg();
					}
					SetPlay((!flags.isAnalPlay) ? "SF_IN_Start" : "A_SF_IN_Start");
					if (flags.lstHeroine[0].hCount == 0 && !flags.isFreeH && (bool)sprite.objFirstHHelpBase && sprite.objFirstHHelpBase.activeSelf && !flags.isAnalPlay)
					{
						sprite.objFirstHHelpBase.SetActive(value: false);
					}
					gotoInAfter = 0;
					flags.voice.playVoices[0] = 326 + num2;
				}
				else if (flags.finish == HFlag.FinishKind.sameS)
				{
					if (!flags.isAnalPlay)
					{
						if (!flags.isCondom)
						{
							flags.AddSonyuSame();
							flags.AddSonyuInside();
						}
						else
						{
							flags.AddSonyuCondomSame();
							flags.AddSonyuCondomInside();
						}
						flags.AddSonyuOrg();
					}
					else
					{
						if (!flags.isCondom)
						{
							flags.AddSonyuAnalSame();
							flags.AddSonyuAnalInside();
						}
						else
						{
							flags.AddSonyuAnalCondomSame();
							flags.AddSonyuAnalCondomInside();
						}
						flags.AddSonyuAnalOrg();
					}
					SetPlay((!flags.isAnalPlay) ? "SS_IN_Start" : "A_SS_IN_Start");
					if (flags.lstHeroine[0].hCount == 0 && !flags.isFreeH && (bool)sprite.objFirstHHelpBase && sprite.objFirstHHelpBase.activeSelf && !flags.isAnalPlay)
					{
						sprite.objFirstHHelpBase.SetActive(value: false);
					}
					gotoInAfter = 0;
					flags.voice.playVoices[0] = 326 + num2;
					flags.isInsideFinish = true;
					flags.isInsideFinishCondom = flags.isCondom;
				}
			}
		}
		else if (animatorStateInfo.IsName("WF_IN_Start") || animatorStateInfo.IsName("A_WF_IN_Start"))
		{
			if (animatorStateInfo.normalizedTime > 1f)
			{
				SetPlay((!flags.isAnalPlay) ? "WF_IN_Loop" : "A_WF_IN_Loop", _fade: false);
				flags.timeDecreaseGaugeCalc = 0f;
			}
		}
		else if (animatorStateInfo.IsName("WF_IN_Loop") || animatorStateInfo.IsName("A_WF_IN_Loop"))
		{
			FinishGaugeDown(1);
			if (!Voice.IsPlay(flags.transVoiceMouth[0]))
			{
				SetPlay((!flags.isAnalPlay) ? "WS_IN_A" : "A_WS_IN_A");
				flags.gaugeFemale = 0f;
			}
		}
		else if (animatorStateInfo.IsName("WS_IN_Start") || animatorStateInfo.IsName("A_WS_IN_Start"))
		{
			if (animatorStateInfo.normalizedTime > 1f)
			{
				SetPlay((!flags.isAnalPlay) ? "WS_IN_Loop" : "A_WS_IN_Loop", _fade: false);
				flags.timeDecreaseGaugeCalc = 0f;
			}
		}
		else if (animatorStateInfo.IsName("WS_IN_Loop") || animatorStateInfo.IsName("A_WS_IN_Loop"))
		{
			FinishGaugeDown();
			if (!Voice.IsPlay(flags.transVoiceMouth[0]))
			{
				SetPlay((!flags.isAnalPlay) ? "WS_IN_A" : "A_WS_IN_A");
				flags.gaugeMale = 0f;
				flags.gaugeFemale = 0f;
			}
		}
		else if (animatorStateInfo.IsName("SF_IN_Start") || animatorStateInfo.IsName("A_SF_IN_Start"))
		{
			if (animatorStateInfo.normalizedTime > 1f)
			{
				SetPlay((!flags.isAnalPlay) ? "SF_IN_Loop" : "A_SF_IN_Loop", _fade: false);
				flags.timeDecreaseGaugeCalc = 0f;
			}
		}
		else if (animatorStateInfo.IsName("SF_IN_Loop") || animatorStateInfo.IsName("A_SF_IN_Loop"))
		{
			FinishGaugeDown(1);
			if (!Voice.IsPlay(flags.transVoiceMouth[0]))
			{
				SetPlay((!flags.isAnalPlay) ? "SS_IN_A" : "A_SS_IN_A");
				flags.gaugeFemale = 0f;
			}
		}
		else if (animatorStateInfo.IsName("SS_IN_Start") || animatorStateInfo.IsName("A_SS_IN_Start"))
		{
			if (animatorStateInfo.normalizedTime > 1f)
			{
				SetPlay((!flags.isAnalPlay) ? "SS_IN_Loop" : "A_SS_IN_Loop", _fade: false);
				flags.timeDecreaseGaugeCalc = 0f;
			}
		}
		else if (animatorStateInfo.IsName("SS_IN_Loop") || animatorStateInfo.IsName("A_SS_IN_Loop"))
		{
			FinishGaugeDown();
			if (!Voice.IsPlay(flags.transVoiceMouth[0]))
			{
				SetPlay((!flags.isAnalPlay) ? "SS_IN_A" : "A_SS_IN_A");
				flags.gaugeMale = 0f;
				flags.gaugeFemale = 0f;
			}
		}
		else if (animatorStateInfo.IsName("M_IN_Start") || animatorStateInfo.IsName("A_M_IN_Start"))
		{
			if (animatorStateInfo.normalizedTime > 1f)
			{
				SetPlay((!flags.isAnalPlay) ? "M_IN_Loop" : "A_M_IN_Loop", _fade: false);
				flags.timeDecreaseGaugeCalc = 0f;
			}
		}
		else if (animatorStateInfo.IsName("M_IN_Loop") || animatorStateInfo.IsName("A_M_IN_Loop"))
		{
			FinishGaugeDown(0);
			if (!Voice.IsPlay(flags.transVoiceMouth[0]))
			{
				sprite.SetSonyuActionButtonActive(_active: true, 6);
				sprite.sonyu.imageCtrlPad.OnChangeValue(0);
				SetPlay((!flags.isAnalPlay) ? "IN_A" : "A_IN_A");
				flags.voice.SetSonyuWaitTime(_isIdle: false);
				flags.voice.SetSonyuIdleTime();
				flags.voice.gentles[0] = false;
				rePlay = 0;
				flags.gaugeMale = 0f;
				is70Voices[1] = false;
			}
		}
		else if (animatorStateInfo.IsName("M_OUT_Start") || animatorStateInfo.IsName("A_M_OUT_Start"))
		{
			if (animatorStateInfo.normalizedTime > 1f)
			{
				SetPlay((!flags.isAnalPlay) ? "M_OUT_Loop" : "A_M_OUT_Loop", _fade: false);
				flags.timeDecreaseGaugeCalc = 0f;
				male.chaFile.status.visibleGomu = false;
				sprite.SetCondom(_isOn: false);
			}
		}
		else if (animatorStateInfo.IsName("M_OUT_Loop") || animatorStateInfo.IsName("A_M_OUT_Loop"))
		{
			FinishGaugeDown(0);
			if (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.breath && animatorStateInfo.normalizedTime > 5f)
			{
				SetPlay((!flags.isAnalPlay) ? "OUT_A" : "A_OUT_A");
				sprite.SetSonyuStart();
				flags.timeWaitSonyu.SetIdleTime();
				flags.voice.SetSonyuWaitTime(_isIdle: false);
				flags.voice.SetSonyuIdleTime();
				flags.voice.gentles[0] = false;
				flags.timeWaitSonyu.SetIdleTime();
				flags.isInsideFinish = false;
				flags.isInsideFinishCondom = false;
				rePlay = 0;
				flags.gaugeMale = 0f;
				is70Voices[1] = false;
			}
		}
		else if (animatorStateInfo.IsName("WS_IN_A") || animatorStateInfo.IsName("A_WS_IN_A"))
		{
			switch (gotoInAfter)
			{
			case 0:
				if (animatorStateInfo.normalizedTime > 6f)
				{
					flags.voice.gentles[0] = false;
					flags.voice.SetSonyuWaitTime(_isIdle: false);
					flags.voice.SetSonyuIdleTime();
					if (flags.finish == HFlag.FinishKind.orgW)
					{
						flags.voice.playVoices[0] = 328 + num2;
					}
					else
					{
						flags.voice.playVoices[0] = 327 + num2;
						is70Voices[1] = false;
					}
					gotoInAfter = 1;
					is70Voices[0] = false;
				}
				break;
			case 1:
				if (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.breath)
				{
					sprite.SetSonyuActionButtonActive(_active: true, 6);
					sprite.sonyu.imageCtrlPad.OnChangeValue(0);
					SetPlay((!flags.isAnalPlay) ? "IN_A" : "A_IN_A");
					rePlay = 0;
					gotoInAfter = 0;
				}
				break;
			}
		}
		else if (animatorStateInfo.IsName("SS_IN_A") || animatorStateInfo.IsName("A_SS_IN_A"))
		{
			switch (gotoInAfter)
			{
			case 0:
				if (animatorStateInfo.normalizedTime > 6f)
				{
					flags.voice.gentles[0] = false;
					flags.voice.SetSonyuWaitTime(_isIdle: false);
					flags.voice.SetSonyuIdleTime();
					if (flags.finish == HFlag.FinishKind.orgS)
					{
						flags.voice.playVoices[0] = 330 + num2;
					}
					else
					{
						flags.voice.playVoices[0] = 329 + num2;
						is70Voices[1] = false;
					}
					gotoInAfter = 1;
					is70Voices[0] = false;
				}
				break;
			case 1:
				if (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.breath)
				{
					sprite.SetSonyuActionButtonActive(_active: true, 6);
					sprite.sonyu.imageCtrlPad.OnChangeValue(0);
					SetPlay((!flags.isAnalPlay) ? "IN_A" : "A_IN_A");
					rePlay = 0;
					gotoInAfter = 0;
				}
				break;
			}
		}
		else if (animatorStateInfo.IsName("IN_A") || animatorStateInfo.IsName("A_IN_A"))
		{
			if (IsVoiceWait())
			{
				return false;
			}
			if (!hand.IsAction())
			{
				flags.FemaleGaugeUp((0f - Time.deltaTime) * flags.rateDecreaseGauge, _force: false, _isIdle: false);
			}
			flags.MaleGaugeUp((0f - Time.deltaTime) * flags.rateDecreaseGauge);
			switch (rePlay)
			{
			case 0:
				if (flags.click == HFlag.ClickKind.speedup)
				{
					rePlay = 1;
					List<int> lstKosoKosoCategory = new List<int> { 1205 };
					flags.voice.playVoices[0] = (flags.nowAnimationInfo.lstCategory.Any((HSceneProc.Category c) => lstKosoKosoCategory.Contains(c.category)) ? 12 : 2);
					flags.voiceWait = true;
				}
				else if (flags.click == HFlag.ClickKind.modeChange)
				{
					isAuto = !isAuto;
					Color color2 = sprite.sonyu.imgAuto.color;
					color2.a = (isAuto ? 1f : 0f);
					sprite.sonyu.imgAuto.color = color2;
					sprite.sonyu.imageCtrlPad.OnChangeValue(1);
					rePlay = 1;
					List<int> lstKosoKosoCategory2 = new List<int> { 1205 };
					flags.voice.playVoices[0] = (flags.nowAnimationInfo.lstCategory.Any((HSceneProc.Category c) => lstKosoKosoCategory2.Contains(c.category)) ? 12 : 2);
					flags.voiceWait = true;
				}
				else if (flags.click == HFlag.ClickKind.pull)
				{
					sprite.SetSonyuActionButtonActive(_active: false);
					sprite.sonyu.imageCtrlPad.OnChangeValue(-1);
					SetPlay((!flags.isAnalPlay) ? "Pull" : "A_Pull");
					obi.Clear();
					flags.voice.playShorts[0] = ((!flags.isAnalPlay) ? 7 : 8);
					flags.voice.isShortsPlayTouchWeak[0] = false;
					flags.voice.isAfterVoicePlay = false;
				}
				else if (!hand.IsAction() && voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.breath && flags.voice.IsSonyuIdleTime() && !flags.voice.isAfterVoicePlay)
				{
					if (flags.finish == HFlag.FinishKind.inside || flags.finish == HFlag.FinishKind.outside)
					{
						flags.voice.playVoices[0] = 331 + num2;
					}
					else if (flags.finish == HFlag.FinishKind.sameW || flags.finish == HFlag.FinishKind.sameS)
					{
						flags.voice.playVoices[0] = 332 + num2;
					}
					else
					{
						flags.voice.playVoices[0] = 333 + num2;
					}
					if (!flags.voice.IsSonyuIdleFlag())
					{
						flags.voice.SetSonyuWaitTime(_isIdle: true);
						flags.voice.SetSonyuIdleTime();
					}
					flags.voice.isAfterVoicePlay = true;
				}
				break;
			case 1:
				if (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.breath || (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.voice && !Voice.IsPlay(flags.transVoiceMouth[0])))
				{
					flags.voiceWait = false;
					flags.voice.isAfterVoicePlay = false;
					if (!isAuto)
					{
						flags.SpeedUpClick(flags.rateSpeedUp, 1f);
					}
					sprite.SetSonyuActionButtonActive(_active: false, 6);
					SetPlay((!flags.isAnalPlay) ? "WLoop" : "A_WLoop");
					flags.finish = HFlag.FinishKind.none;
					flags.voice.SetSonyuIdleTime();
					obi.Clear();
					isSW = false;
					flags.voice.SetSonyuWaitTime(_isIdle: true);
					flags.voice.SetSonyuIdleTime();
				}
				break;
			}
		}
		else if (animatorStateInfo.IsName("OUT_A") || animatorStateInfo.IsName("A_OUT_A"))
		{
			if (IsVoiceWait())
			{
				return false;
			}
			if (!hand.IsAction())
			{
				flags.FemaleGaugeUp((0f - Time.deltaTime) * flags.rateDecreaseGauge, _force: false, _isIdle: false);
			}
			flags.MaleGaugeUp((0f - Time.deltaTime) * flags.rateDecreaseGauge);
			switch (rePlay)
			{
			case 0:
				if (flags.isDenialvoiceWait && voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.breath)
				{
					if (!flags.isCondom)
					{
						flags.isCondom = true;
						male.chaFile.status.visibleGomu = true;
						sprite.SetCondom(_isOn: true);
					}
					flags.isDenialvoiceWait = false;
				}
				if (flags.click == HFlag.ClickKind.insert || flags.click == HFlag.ClickKind.insert_voice)
				{
					rePlay = 1;
					kindInsert = ((flags.click != HFlag.ClickKind.insert) ? 1 : 0);
					flags.voiceWait = true;
				}
				else if (flags.click == HFlag.ClickKind.insert_anal || flags.click == HFlag.ClickKind.insert_anal_voice)
				{
					rePlay = 2;
					kindInsert = ((flags.click != HFlag.ClickKind.insert_anal) ? 1 : 0);
					flags.voiceWait = true;
				}
				else
				{
					if (hand.IsAction())
					{
						break;
					}
					if (flags.timeWaitSonyu.IsIdleTime())
					{
						SetPlay((!flags.isAnalPlay) ? "Idle" : "A_Idle");
						flags.voice.SetSonyuWaitTime(_isIdle: true);
						flags.voice.SetSonyuIdleTime();
						flags.voice.isAfterVoicePlay = false;
						flags.finish = HFlag.FinishKind.none;
						obi.Clear();
					}
					else if (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.breath && flags.voice.IsSonyuIdleTime() && !flags.voice.isAfterVoicePlay)
					{
						if (flags.finish == HFlag.FinishKind.outside)
						{
							flags.voice.playVoices[0] = 335 + num2;
						}
						if (!flags.voice.IsSonyuIdleFlag())
						{
							flags.voice.SetSonyuWaitTime(_isIdle: true);
							flags.voice.SetSonyuIdleTime();
						}
						flags.voice.isAfterVoicePlay = true;
					}
				}
				break;
			case 1:
				if (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.breath || (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.voice && !Voice.IsPlay(flags.transVoiceMouth[0])))
				{
					flags.voiceWait = false;
					flags.voice.isAfterVoicePlay = false;
					SetPlay("Insert");
					sprite.SetSonyuActionButtonActive(_active: false);
					obi.Clear();
					flags.finish = HFlag.FinishKind.none;
					flags.isAnalPlay = false;
					if (!flags.isCondom)
					{
						flags.SetNameInsert();
					}
					if (kindInsert == 0)
					{
						flags.voice.playVoices[0] = 307 + num2;
					}
					else
					{
						flags.voice.playVoices[0] = 308 + num2;
					}
					flags.voice.SetSonyuWaitTime(_isIdle: true);
					flags.voice.SetSonyuIdleTime();
				}
				break;
			case 2:
				if (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.breath || (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.voice && !Voice.IsPlay(flags.transVoiceMouth[0])))
				{
					flags.voiceWait = false;
					flags.voice.isAfterVoicePlay = false;
					SetPlay("A_Insert");
					sprite.SetSonyuActionButtonActive(_active: false);
					obi.Clear();
					flags.finish = HFlag.FinishKind.none;
					flags.isAnalPlay = true;
					if (kindInsert == 0)
					{
						flags.voice.playVoices[0] = 307 + num2;
					}
					else
					{
						flags.voice.playVoices[0] = 308 + num2;
					}
					flags.voice.SetSonyuWaitTime(_isIdle: true);
					flags.voice.SetSonyuIdleTime();
				}
				break;
			}
		}
		else if (animatorStateInfo.IsName("Pull") || animatorStateInfo.IsName("A_Pull"))
		{
			if (animatorStateInfo.normalizedTime > 1f)
			{
				if (!flags.isCondom && flags.isInsideFinish)
				{
					SetPlay((!flags.isAnalPlay) ? "Drop" : "A_Drop");
					if (!flags.isAnalPlay)
					{
						flags.AddSonyuTare();
					}
					else
					{
						flags.AddSonyuAnalTare();
					}
					flags.voice.playVoices[0] = 336 + num2;
				}
				else
				{
					SetPlay((!flags.isAnalPlay) ? "OUT_A" : "A_OUT_A");
					sprite.SetSonyuStart();
					sprite.sonyu.imageCtrlPad.OnChangeValue(-1);
					flags.voice.SetSonyuIdleTime();
					flags.timeWaitSonyu.SetIdleTime();
					rePlay = 0;
				}
			}
		}
		else if ((animatorStateInfo.IsName("Drop") || animatorStateInfo.IsName("A_Drop")) && !Voice.IsPlay(flags.transVoiceMouth[0]))
		{
			SetPlay((!flags.isAnalPlay) ? "OUT_A" : "A_OUT_A");
			sprite.SetSonyuStart();
			sprite.sonyu.imageCtrlPad.OnChangeValue(-1);
			flags.voice.SetSonyuIdleTime();
			flags.timeWaitSonyu.SetIdleTime();
			flags.isInsideFinish = false;
			rePlay = 0;
		}
		if (enableMotions[0])
		{
			timeMotionCalcs[0] = Mathf.Clamp(timeMotionCalcs[0] + Time.deltaTime, 0f, timeMotions[0]);
			float time = Mathf.Clamp01(timeMotionCalcs[0] / timeMotions[0]);
			time = flags.curveMotion.Evaluate(time);
			flags.motion = Mathf.Lerp(lerpMotions[0].x, lerpMotions[0].y, time);
			if (time >= 1f)
			{
				enableMotions[0] = false;
			}
		}
		SetAnimatorFloat("speed", flags.speed);
		SetAnimatorFloat("speedHand", 1f + flags.speedItem);
		SetAnimatorFloat("Breast", female.GetShapeBodyValue(4), _isMale: false);
		SetDefaultAnimatorFloat();
		return true;
	}

	public override bool LateProc()
	{
		hand.LateProc();
		return true;
	}

	private bool LoopProc(bool _loop)
	{
		int num = (flags.nowAnimationInfo.isFemaleInitiative ? 38 : 0);
		if (flags.finish == HFlag.FinishKind.none)
		{
			flags.WaitSpeedProc(isAuto, flags.speedSonyuCurve);
			flags.FemaleGaugeUp(Time.deltaTime * flags.rateClickGauge, _force: false, _isIdle: false);
			flags.MaleGaugeUp(Time.deltaTime * flags.rateClickGauge);
			if (flags.click == HFlag.ClickKind.speedup && !isAuto)
			{
				flags.SpeedUpClick(flags.rateSpeedUp, 1f);
			}
			if (isAuto && sprite.IsCursorOnPad())
			{
				float axis = Input.GetAxis("Mouse ScrollWheel");
				if (axis < 0f)
				{
					flags.SpeedUpClick(0f - flags.rateWheelSpeedUp, 1f);
				}
				else if (axis > 0f)
				{
					flags.SpeedUpClick(flags.rateWheelSpeedUp, 1f);
				}
			}
			if (flags.click == HFlag.ClickKind.actionChange)
			{
				sprite.SetSonyuActionButtonActive(_active: false);
				sprite.SetSonyuActionButtonActive(_active: true, 6);
				SetPlay((!flags.isAnalPlay) ? "InsertIdle" : "A_InsertIdle");
			}
			else if (MathfEx.RangeEqualOn(HFlag.ClickKind.inside, flags.click, HFlag.ClickKind.outside) || flags.gaugeFemale >= 100f)
			{
				if (MathfEx.RangeEqualOn(HFlag.ClickKind.inside, flags.click, HFlag.ClickKind.outside))
				{
					if (flags.click == HFlag.ClickKind.inside)
					{
						flags.finish = ((!(flags.gaugeFemale >= 70f)) ? HFlag.FinishKind.inside : ((!isSW) ? HFlag.FinishKind.sameW : HFlag.FinishKind.sameS));
					}
					else
					{
						flags.finish = HFlag.FinishKind.outside;
					}
					kindInsert = 0;
				}
				else
				{
					flags.finish = ((!(flags.gaugeMale < 70f) && !sprite.IsSonyuAutoFinish()) ? ((!isSW) ? HFlag.FinishKind.sameW : HFlag.FinishKind.sameS) : ((!isSW) ? HFlag.FinishKind.orgW : HFlag.FinishKind.orgS));
				}
				sprite.sonyu.imageCtrlPad.OnChangeValue(-1);
			}
			else if (flags.click == HFlag.ClickKind.motionchange)
			{
				SetPlay(flags.isAnalPlay ? (_loop ? "A_WLoop" : "A_SLoop") : (_loop ? "WLoop" : "SLoop"));
				isSW = !_loop;
			}
			else if (flags.click == HFlag.ClickKind.modeChange)
			{
				isAuto = !isAuto;
				Color color = sprite.sonyu.imgAuto.color;
				color.a = (isAuto ? 1f : 0f);
				sprite.sonyu.imgAuto.color = color;
				sprite.sonyu.imageCtrlPad.OnChangeValue(isAuto ? 1 : 0);
			}
			else if (flags.speedCalc == 0f && !isAuto)
			{
				sprite.SetSonyuActionButtonActive(_active: false);
				sprite.SetSonyuActionButtonActive(_active: true, 6);
				SetPlay((!flags.isAnalPlay) ? "InsertIdle" : "A_InsertIdle");
			}
			else if (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.breath && flags.voice.IsSonyuIdleTime())
			{
				if (flags.gaugeFemale >= 70f && !flags.voice.isFemale70PercentageVoicePlay)
				{
					flags.voice.playVoices[0] = ((!flags.voice.speedMotion) ? 314 : 315) + num;
					flags.voice.isFemale70PercentageVoicePlay = true;
					flags.voice.SetSonyuWaitTime(_isIdle: true);
					flags.voice.SetSonyuIdleTime();
				}
				else if (flags.gaugeMale >= 70f && !flags.voice.isMale70PercentageVoicePlay)
				{
					flags.voice.playVoices[0] = ((!flags.voice.speedMotion) ? 316 : 317) + num;
					flags.voice.isMale70PercentageVoicePlay = true;
					flags.voice.SetSonyuWaitTime(_isIdle: true);
					flags.voice.SetSonyuIdleTime();
				}
				else if (!flags.voice.isFemale70PercentageVoicePlay && !flags.voice.isMale70PercentageVoicePlay)
				{
					flags.voice.playVoices[0] = (_loop ? ((!flags.voice.speedMotion) ? 312 : 313) : ((!flags.voice.speedMotion) ? 310 : 311)) + num;
					if (!flags.voice.IsSonyuIdleFlag())
					{
						flags.voice.SetSonyuWaitTime(_isIdle: true);
						flags.voice.SetSonyuIdleTime();
					}
				}
			}
		}
		else if (voice.nowVoices[0].state != HVoiceCtrl.VoiceKind.voice)
		{
			SetPlay((!flags.isAnalPlay) ? "OLoop" : "A_OLoop");
			isAuto = false;
			flags.voiceWait = false;
			flags.speedCalc = 0f;
			flags.timeNoClick = 0f;
			flags.speedUpClac = Vector2.zero;
			flags.timeSpeedUpStartCalc = 0f;
			sprite.SetSonyuActionButtonActive(_active: false);
			Color color2 = sprite.sonyu.imgAuto.color;
			color2.a = (isAuto ? 1f : 0f);
			sprite.sonyu.imgAuto.color = color2;
			sprite.sonyu.imageCtrlPad.OnChangeValue(-1);
			oldGaugeMale = flags.gaugeMale;
			oldGaugeFemale = flags.gaugeFemale;
			if (flags.finish == HFlag.FinishKind.orgW || flags.finish == HFlag.FinishKind.orgS)
			{
				flags.voice.playVoices[0] = 318 + num;
			}
			else if (flags.finish == HFlag.FinishKind.sameS || flags.finish == HFlag.FinishKind.sameW)
			{
				flags.voice.playVoices[0] = 319 + num;
			}
			else if (kindInsert == 0)
			{
				if (flags.finish == HFlag.FinishKind.inside)
				{
					flags.voice.playVoices[0] = (flags.isCondom ? 323 : 320) + num;
				}
				else if (flags.finish == HFlag.FinishKind.outside)
				{
					flags.voice.playVoices[0] = 321 + num;
				}
			}
			else
			{
				flags.voice.playVoices[0] = 322 + num;
			}
			if (!flags.voice.IsSonyuIdleFlag())
			{
				flags.voice.SetSonyuWaitTime(_isIdle: true);
				flags.voice.SetSonyuIdleTime();
			}
		}
		timeAutoMotionCalcs[0] += Time.deltaTime;
		if (timeAutoMotions[0] <= timeAutoMotionCalcs[0] && !enableMotions[0])
		{
			timeAutoMotions[0] = Random.Range(flags.timeAutoMotionMin, flags.timeAutoMotionMax);
			timeAutoMotionCalcs[0] = 0f;
			enableMotions[0] = true;
			timeMotionCalcs[0] = 0f;
			float num2 = 0f;
			if (allowMotions[0])
			{
				num2 = 1f - flags.motion;
				num2 = ((!(num2 <= flags.rateMotionMin)) ? (flags.motion + Random.Range(flags.rateMotionMin, num2)) : 1f);
				if (num2 >= 1f)
				{
					allowMotions[0] = false;
				}
			}
			else
			{
				num2 = flags.motion;
				num2 = ((!(num2 <= flags.rateMotionMin)) ? (flags.motion - Random.Range(flags.rateMotionMin, num2)) : 0f);
				if (num2 <= 0f)
				{
					allowMotions[0] = true;
				}
			}
			lerpMotions[0] = new Vector2(flags.motion, num2);
			timeMotions[0] = Random.Range(flags.timeMotionMin, flags.timeMotionMax);
		}
		return true;
	}

	private void FinishGaugeDown(int _sex = -1)
	{
		flags.timeDecreaseGaugeCalc += Time.deltaTime;
		flags.timeDecreaseGaugeCalc = Mathf.Clamp(flags.timeDecreaseGaugeCalc + Time.deltaTime, 0f, flags.timeDecreaseGauge);
		float t = flags.timeDecreaseGaugeCalc / flags.timeDecreaseGauge;
		if (_sex == 0 || _sex == -1)
		{
			flags.gaugeMale = Mathf.Lerp(oldGaugeMale, 0f, t);
		}
		if (_sex == 1 || _sex == -1)
		{
			flags.gaugeFemale = Mathf.Lerp(oldGaugeFemale, 0f, t);
		}
	}
}
