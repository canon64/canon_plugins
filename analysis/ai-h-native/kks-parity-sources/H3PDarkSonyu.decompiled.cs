using Manager;
using UnityEngine;

public class H3PDarkSonyu : HActionBase
{
	private bool isAuto;

	private float oldGaugeMale;

	private float oldGaugeFemale;

	private int rePlay;

	private int kindInsert;

	private int gotoInAfter;

	private bool isSW;

	private bool[] is70Voices = new bool[2];

	public H3PDarkSonyu(HFlag.DeliveryMember _membar)
		: base(_membar)
	{
	}

	public override bool MotionChange(int _motion)
	{
		SetPlay("Idle");
		rePlay = 0;
		flags.isAnalPlay = false;
		sprite.Houshi3PDarkInitRely();
		isAuto = false;
		Color color = sprite.sonyu3PDark.imgAuto.color;
		color.a = (isAuto ? 1f : 0f);
		sprite.sonyu3PDark.imgAuto.color = color;
		sprite.imageSpeedSlliderCover70.enabled = false;
		sprite.sonyu3PDark.imageCtrlPad.OnChangeValue(-1);
		isSW = false;
		flags.isCondom = sprite.sonyu3PDark.isCondom;
		if ((bool)male && male.chaFile != null)
		{
			male.chaFile.status.visibleGomu = flags.isCondom;
		}
		flags.voice.SetSonyuWaitTime(_isIdle: true);
		flags.voice.SetSonyuIdleTime();
		flags.isInsideFinish = false;
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
		return true;
	}

	public override bool Proc()
	{
		if (flags.selectAnimationListInfo == null)
		{
			hand.Proc();
		}
		SetMoveCameraFlag();
		AnimatorStateInfo animatorStateInfo = female.getAnimatorStateInfo(0);
		if (flags.gaugeFemale < 70f && flags.voice.isFemale70PercentageVoicePlay)
		{
			flags.voice.isFemale70PercentageVoicePlay = false;
		}
		if (flags.gaugeMale < 70f && flags.voice.isMale70PercentageVoicePlay)
		{
			flags.voice.isMale70PercentageVoicePlay = false;
		}
		if (flags.gaugeFemale >= 70f && !flags.voice.isFemale70PercentageVoicePlay && flags.voice.IsSonyuIdleFlag() && !is70Voices[0])
		{
			flags.voice.SetSonyuWaitTime(_isIdle: false);
			flags.voice.SetSonyuIdleTime();
			is70Voices[0] = true;
		}
		else if (flags.gaugeMale >= 70f && !flags.voice.isMale70PercentageVoicePlay && flags.voice.IsSonyuIdleFlag() && !is70Voices[1])
		{
			flags.voice.SetSonyuWaitTime(_isIdle: false);
			flags.voice.SetSonyuIdleTime();
			is70Voices[1] = true;
		}
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
						sprite.SetCondom3PDark(_isOn: true);
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
					flags.voice.playVoices[0] = 1000;
				}
				break;
			case 1:
				if (IsCheckVoicePlay(0))
				{
					flags.voiceWait = false;
					SetPlay("Insert");
					sprite.SetSonyu3PDarkActionButtonActive(_active: false);
					flags.isAnalPlay = false;
					if (!flags.isCondom)
					{
						flags.SetNameInsert();
					}
					flags.AddSonyuKokanPlay();
					if (kindInsert == 0)
					{
						flags.voice.playVoices[0] = 1007;
					}
					else
					{
						flags.voice.playVoices[0] = 1008;
					}
				}
				break;
			case 2:
				if (IsCheckVoicePlay(0))
				{
					flags.voiceWait = false;
					SetPlay("A_Insert");
					sprite.SetSonyu3PDarkActionButtonActive(_active: false);
					flags.isAnalPlay = true;
					flags.AddSonyuAnalPlay();
					if (kindInsert == 0)
					{
						flags.voice.playVoices[0] = 1007;
					}
					else
					{
						flags.voice.playVoices[0] = 1008;
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
				sprite.SetSonyu3PDarkActionButtonActive(_active: true, 6);
				sprite.sonyu3PDark.imageCtrlPad.OnChangeValue(0);
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
				if (flags.click == HFlag.ClickKind.actionChange && flags.nowAnimationInfo.id == 2)
				{
					SetPlay((!flags.isAnalPlay) ? "Idle" : "A_Idle");
				}
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
				sprite.SetSonyu3PDarkActionButtonActive(_active: false, 6);
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
				Color color = sprite.sonyu3PDark.imgAuto.color;
				color.a = (isAuto ? 1f : 0f);
				sprite.sonyu3PDark.imgAuto.color = color;
				sprite.SetSonyu3PDarkActionButtonActive(_active: false, 6);
				sprite.sonyu3PDark.imageCtrlPad.OnChangeValue(1);
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
				sprite.SetSonyu3PDarkStart();
				sprite.sonyu3PDark.imageCtrlPad.OnChangeValue(-1);
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
				flags.voice.playVoices[0] = 1003;
			}
			else if (!hand.IsAction() && voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.breath && flags.voice.IsSonyuIdleTime())
			{
				flags.voice.playVoices[0] = 1009;
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
					flags.voice.playVoices[0] = 1024;
					flags.isInsideFinish = true;
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
					flags.voice.playVoices[0] = 1034;
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
					gotoInAfter = 0;
					flags.voice.playVoices[0] = 1025;
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
					gotoInAfter = 0;
					flags.voice.playVoices[0] = 1025;
					flags.isInsideFinish = true;
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
					gotoInAfter = 0;
					flags.voice.playVoices[0] = 1026;
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
					gotoInAfter = 0;
					flags.voice.playVoices[0] = 1026;
					flags.isInsideFinish = true;
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
				sprite.SetSonyu3PDarkActionButtonActive(_active: true, 6);
				sprite.sonyu3PDark.imageCtrlPad.OnChangeValue(0);
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
				sprite.SetCondom3PDark(_isOn: false);
			}
		}
		else if (animatorStateInfo.IsName("M_OUT_Loop") || animatorStateInfo.IsName("A_M_OUT_Loop"))
		{
			FinishGaugeDown(0);
			if (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.breath && animatorStateInfo.normalizedTime > 5f)
			{
				SetPlay((!flags.isAnalPlay) ? "OUT_A" : "A_OUT_A");
				sprite.SetSonyu3PDarkStart();
				flags.timeWaitSonyu.SetIdleTime();
				flags.voice.SetSonyuWaitTime(_isIdle: false);
				flags.voice.SetSonyuIdleTime();
				flags.voice.gentles[0] = false;
				flags.timeWaitSonyu.SetIdleTime();
				flags.isInsideFinish = false;
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
					flags.voice.SetSonyuWaitTime(_isIdle: false);
					flags.voice.SetSonyuIdleTime();
					flags.voice.gentles[0] = false;
					if (flags.finish == HFlag.FinishKind.orgW)
					{
						flags.voice.playVoices[0] = 1028;
					}
					else
					{
						flags.voice.playVoices[0] = 1027;
						is70Voices[1] = false;
					}
					gotoInAfter = 1;
					is70Voices[0] = false;
				}
				break;
			case 1:
				if (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.breath)
				{
					sprite.SetSonyu3PDarkActionButtonActive(_active: true, 6);
					sprite.sonyu3PDark.imageCtrlPad.OnChangeValue(0);
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
					flags.voice.SetSonyuWaitTime(_isIdle: false);
					flags.voice.SetSonyuIdleTime();
					flags.voice.gentles[0] = false;
					if (flags.finish == HFlag.FinishKind.orgS)
					{
						flags.voice.playVoices[0] = 1030;
					}
					else
					{
						flags.voice.playVoices[0] = 1029;
						is70Voices[1] = false;
					}
					gotoInAfter = 1;
					is70Voices[0] = false;
				}
				break;
			case 1:
				if (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.breath)
				{
					sprite.SetSonyu3PDarkActionButtonActive(_active: true, 6);
					sprite.sonyu3PDark.imageCtrlPad.OnChangeValue(0);
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
					flags.voice.playVoices[0] = 26;
					flags.voiceWait = true;
				}
				else if (flags.click == HFlag.ClickKind.modeChange)
				{
					isAuto = !isAuto;
					Color color2 = sprite.sonyu3PDark.imgAuto.color;
					color2.a = (isAuto ? 1f : 0f);
					sprite.sonyu3PDark.imgAuto.color = color2;
					sprite.sonyu3PDark.imageCtrlPad.OnChangeValue(1);
					rePlay = 1;
					flags.voice.playVoices[0] = 26;
					flags.voiceWait = true;
				}
				else if (flags.click == HFlag.ClickKind.pull)
				{
					sprite.SetSonyu3PDarkActionButtonActive(_active: false);
					sprite.sonyu3PDark.imageCtrlPad.OnChangeValue(-1);
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
						flags.voice.playVoices[0] = 1031;
					}
					else if (flags.finish == HFlag.FinishKind.sameW || flags.finish == HFlag.FinishKind.sameS)
					{
						flags.voice.playVoices[0] = 1032;
					}
					else
					{
						flags.voice.playVoices[0] = 1033;
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
				if (IsCheckVoicePlay(0))
				{
					flags.voiceWait = false;
					flags.voice.isAfterVoicePlay = false;
					if (!isAuto)
					{
						flags.SpeedUpClick(flags.rateSpeedUp, 1f);
					}
					sprite.SetSonyu3PDarkActionButtonActive(_active: false, 6);
					SetPlay((!flags.isAnalPlay) ? "WLoop" : "A_WLoop");
					flags.voice.SetSonyuIdleTime();
					obi.Clear();
					flags.finish = HFlag.FinishKind.none;
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
						sprite.SetCondom3PDark(_isOn: true);
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
						flags.finish = HFlag.FinishKind.none;
						flags.voice.isAfterVoicePlay = false;
						obi.Clear();
					}
					else if (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.breath && flags.voice.IsSonyuIdleTime() && !flags.voice.isAfterVoicePlay)
					{
						if (flags.finish == HFlag.FinishKind.outside)
						{
							flags.voice.playVoices[0] = 1035;
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
				if (IsCheckVoicePlay(0))
				{
					flags.voiceWait = false;
					flags.voice.isAfterVoicePlay = false;
					SetPlay("Insert");
					sprite.SetSonyu3PDarkActionButtonActive(_active: false);
					obi.Clear();
					flags.isAnalPlay = false;
					flags.finish = HFlag.FinishKind.none;
					if (!flags.isCondom)
					{
						flags.SetNameInsert();
					}
					if (kindInsert == 0)
					{
						flags.voice.playVoices[0] = 1007;
					}
					else
					{
						flags.voice.playVoices[0] = 1008;
					}
					flags.voice.SetSonyuWaitTime(_isIdle: true);
					flags.voice.SetSonyuIdleTime();
				}
				break;
			case 2:
				if (IsCheckVoicePlay(0))
				{
					flags.voiceWait = false;
					flags.voice.isAfterVoicePlay = false;
					SetPlay("A_Insert");
					sprite.SetSonyu3PDarkActionButtonActive(_active: false);
					obi.Clear();
					flags.isAnalPlay = true;
					flags.finish = HFlag.FinishKind.none;
					if (kindInsert == 0)
					{
						flags.voice.playVoices[0] = 1007;
					}
					else
					{
						flags.voice.playVoices[0] = 1008;
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
					flags.voice.playVoices[0] = 1036;
				}
				else
				{
					SetPlay((!flags.isAnalPlay) ? "OUT_A" : "A_OUT_A");
					sprite.SetSonyu3PDarkStart();
					sprite.sonyu3PDark.imageCtrlPad.OnChangeValue(-1);
					flags.voice.SetSonyuIdleTime();
					flags.timeWaitSonyu.SetIdleTime();
					rePlay = 0;
				}
			}
		}
		else if ((animatorStateInfo.IsName("Drop") || animatorStateInfo.IsName("A_Drop")) && !Voice.IsPlay(flags.transVoiceMouth[0]))
		{
			SetPlay((!flags.isAnalPlay) ? "OUT_A" : "A_OUT_A");
			sprite.SetSonyu3PDarkStart();
			sprite.sonyu3PDark.imageCtrlPad.OnChangeValue(-1);
			flags.voice.SetSonyuIdleTime();
			flags.timeWaitSonyu.SetIdleTime();
			flags.isInsideFinish = false;
			rePlay = 0;
		}
		flags.motion = CalcFluctuation(0, flags.motion);
		flags.motion1 = CalcFluctuation(1, flags.motion1);
		SetAnimatorFloat("speed", flags.speed);
		SetDefaultAnimatorFloat();
		SetAnimatorFloat(male1, "motion1", flags.motion1);
		return true;
	}

	public override bool LateProc()
	{
		hand.LateProc();
		return true;
	}

	private bool LoopProc(bool _loop)
	{
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
				sprite.SetSonyu3PDarkActionButtonActive(_active: false);
				sprite.SetSonyu3PDarkActionButtonActive(_active: true, 6);
				if (flags.nowAnimationInfo.id == 2)
				{
					SetPlay((!flags.isAnalPlay) ? "Idle" : "A_Idle");
				}
				else
				{
					SetPlay((!flags.isAnalPlay) ? "InsertIdle" : "A_InsertIdle");
				}
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
					flags.finish = ((!(flags.gaugeMale < 70f) && !sprite.IsSonyu3PDarkAutoFinish()) ? ((!isSW) ? HFlag.FinishKind.sameW : HFlag.FinishKind.sameS) : ((!isSW) ? HFlag.FinishKind.orgW : HFlag.FinishKind.orgS));
				}
				sprite.sonyu3PDark.imageCtrlPad.OnChangeValue(-1);
			}
			else if (flags.click == HFlag.ClickKind.motionchange)
			{
				SetPlay(flags.isAnalPlay ? (_loop ? "A_WLoop" : "A_SLoop") : (_loop ? "WLoop" : "SLoop"));
				isSW = !_loop;
			}
			else if (flags.click == HFlag.ClickKind.modeChange)
			{
				isAuto = !isAuto;
				Color color = sprite.sonyu3PDark.imgAuto.color;
				color.a = (isAuto ? 1f : 0f);
				sprite.sonyu3PDark.imgAuto.color = color;
				sprite.sonyu3PDark.imageCtrlPad.OnChangeValue(isAuto ? 1 : 0);
			}
			else if (flags.speedCalc == 0f && !isAuto)
			{
				sprite.SetSonyu3PDarkActionButtonActive(_active: false);
				sprite.SetSonyu3PDarkActionButtonActive(_active: true, 6);
				SetPlay((!flags.isAnalPlay) ? "InsertIdle" : "A_InsertIdle");
			}
			else if (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.breath && flags.voice.IsSonyuIdleTime())
			{
				if (flags.gaugeFemale >= 70f && !flags.voice.isFemale70PercentageVoicePlay)
				{
					flags.voice.playVoices[0] = ((!flags.voice.speedMotion) ? 1014 : 1015);
					flags.voice.isFemale70PercentageVoicePlay = true;
					flags.voice.SetSonyuWaitTime(_isIdle: true);
					flags.voice.SetSonyuIdleTime();
				}
				else if (flags.gaugeMale >= 70f && !flags.voice.isMale70PercentageVoicePlay)
				{
					flags.voice.playVoices[0] = ((!flags.voice.speedMotion) ? 1016 : 1017);
					flags.voice.isMale70PercentageVoicePlay = true;
					flags.voice.SetSonyuWaitTime(_isIdle: true);
					flags.voice.SetSonyuIdleTime();
				}
				else if (!flags.voice.isFemale70PercentageVoicePlay && !flags.voice.isMale70PercentageVoicePlay)
				{
					flags.voice.playVoices[0] = (_loop ? ((!flags.voice.speedMotion) ? 1012 : 1013) : ((!flags.voice.speedMotion) ? 1010 : 1011));
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
			sprite.SetSonyu3PDarkActionButtonActive(_active: false);
			Color color2 = sprite.sonyu3PDark.imgAuto.color;
			color2.a = (isAuto ? 1f : 0f);
			sprite.sonyu3PDark.imgAuto.color = color2;
			sprite.sonyu3PDark.imageCtrlPad.OnChangeValue(-1);
			oldGaugeMale = flags.gaugeMale;
			oldGaugeFemale = flags.gaugeFemale;
			if (flags.finish == HFlag.FinishKind.orgW || flags.finish == HFlag.FinishKind.orgS)
			{
				flags.voice.playVoices[0] = 1018;
			}
			else if (flags.finish == HFlag.FinishKind.sameS || flags.finish == HFlag.FinishKind.sameW)
			{
				flags.voice.playVoices[0] = 1019;
			}
			else if (kindInsert == 0)
			{
				if (flags.finish == HFlag.FinishKind.inside)
				{
					flags.voice.playVoices[0] = (flags.isCondom ? 1023 : 1020);
				}
				else if (flags.finish == HFlag.FinishKind.outside)
				{
					flags.voice.playVoices[0] = 1021;
				}
			}
			else
			{
				flags.voice.playVoices[0] = 1022;
			}
		}
		SetFluctuation(0, flags.motion);
		SetFluctuation(1, flags.motion1);
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

	private void SetFluctuation(int _num, float _motion)
	{
		timeAutoMotionCalcs[_num] += Time.deltaTime;
		if (timeAutoMotions[_num] > timeAutoMotionCalcs[_num] || enableMotions[_num])
		{
			return;
		}
		timeAutoMotions[_num] = Random.Range(flags.timeAutoMotionMin, flags.timeAutoMotionMax);
		timeAutoMotionCalcs[_num] = 0f;
		enableMotions[_num] = true;
		timeMotionCalcs[_num] = 0f;
		float num = 0f;
		if (allowMotions[_num])
		{
			num = 1f - _motion;
			num = ((!(num <= flags.rateMotionMin)) ? (_motion + Random.Range(flags.rateMotionMin, num)) : 1f);
			if (num >= 1f)
			{
				allowMotions[_num] = false;
			}
		}
		else
		{
			num = _motion;
			num = ((!(num <= flags.rateMotionMin)) ? (_motion - Random.Range(flags.rateMotionMin, num)) : 0f);
			if (num <= 0f)
			{
				allowMotions[_num] = true;
			}
		}
		lerpMotions[_num] = new Vector2(_motion, num);
		timeMotions[_num] = Random.Range(flags.timeMotionMin, flags.timeMotionMax);
	}

	private float CalcFluctuation(int _num, float _motion)
	{
		if (!enableMotions[_num])
		{
			return _motion;
		}
		timeMotionCalcs[_num] = Mathf.Clamp(timeMotionCalcs[_num] + Time.deltaTime, 0f, timeMotions[_num]);
		float time = Mathf.Clamp01(timeMotionCalcs[_num] / timeMotions[_num]);
		time = flags.curveMotion.Evaluate(time);
		float result = Mathf.Lerp(lerpMotions[_num].x, lerpMotions[_num].y, time);
		if (time >= 1f)
		{
			enableMotions[_num] = false;
		}
		return result;
	}
}
