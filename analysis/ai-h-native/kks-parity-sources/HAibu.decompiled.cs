using System.Collections.Generic;
using Illusion.Game;
using Manager;
using SaveData;
using UniRx;
using UniRx.Triggers;
using UnityEngine;

public class HAibu : HActionBase
{
	private int backIdle = -1;

	private HandCtrl.AibuColliderKind oldTouchKind;

	private int kindDislikes;

	private int oldObj = -1;

	private GlobalMethod.ShuffleRand randSio = new GlobalMethod.ShuffleRand();

	public HAibu(HFlag.DeliveryMember _membar)
		: base(_membar)
	{
		List<int> list = new List<int>();
		if (!flags.isFreeH)
		{
			bool sioRateItem = flags.GetSioRateItem();
			bool flag = flags.lstHeroine[0].HExperience == Heroine.HExperienceKind.淫乱;
			for (int i = 0; i < 3; i++)
			{
				list.Add(1);
			}
			if (flag)
			{
				for (int j = 0; j < 2; j++)
				{
					list.Add(1);
				}
			}
			if (sioRateItem)
			{
				for (int k = 0; k < 2; k++)
				{
					list.Add(1);
				}
			}
			int num = 10 - list.Count;
			for (int l = 0; l < num; l++)
			{
				list.Add(0);
			}
		}
		else
		{
			list = new List<int> { 1, 1, 1, 0, 0 };
		}
		randSio.Init(list);
	}

	public override bool MotionChange(int _motion)
	{
		SetPlay("Idle");
		flags.isCondom = false;
		sprite.HoushiInitRely();
		sprite.imageSpeedSlliderCover70.enabled = false;
		flags.isInsideFinish = false;
		flags.isInsideFinishCondom = false;
		flags.speed = 0f;
		flags.voice.SetAibuIdleTime();
		if (flags.selectAnimationListInfo == null)
		{
			flags.voice.isFemale70PercentageVoicePlay = false;
			flags.voice.isMale70PercentageVoicePlay = false;
			flags.voice.isAfterVoicePlay = false;
		}
		else if (flags.selectAnimationListInfo.mode != flags.nowAnimationInfo.mode)
		{
			flags.voice.isFemale70PercentageVoicePlay = false;
			flags.voice.isMale70PercentageVoicePlay = false;
			flags.voice.isAfterVoicePlay = false;
		}
		SetAnimatorFloat("speedBody", 1f + flags.speed);
		SetAnimatorFloat("speedHand", 1f + flags.speedItem);
		SetAnimatorFloat("Breast", female.GetShapeBodyValue(4));
		flags.motion = Mathf.Clamp01(Mathf.InverseLerp(0f, flags.speedMaxAibuBody * 0.7f, flags.speed));
		SetDefaultAnimatorFloat();
		return true;
	}

	public override bool Proc()
	{
		if (flags.selectAnimationListInfo == null)
		{
			hand.Proc();
		}
		flags.WaitSpeedProcAibu();
		AnimatorStateInfo animatorStateInfo = female.getAnimatorStateInfo(0);
		if (flags.gaugeFemale < 70f && flags.voice.isFemale70PercentageVoicePlay)
		{
			flags.voice.isFemale70PercentageVoicePlay = false;
		}
		if (!flags.voice.isFemale70PercentageVoicePlay && flags.gaugeFemale >= 70f && !animatorStateInfo.IsName("K_Touch") && !animatorStateInfo.IsName("K_Loop") && flags.voice.playVoices[0] != -1 && voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.breath)
		{
			flags.voice.playVoices[0] = 141;
			flags.voice.isFemale70PercentageVoicePlay = true;
		}
		if (animatorStateInfo.IsName("Idle"))
		{
			if (!hand.IsAction())
			{
				SetMoveCameraFlag();
			}
			if (IsVoiceWait())
			{
				return false;
			}
			flags.FemaleGaugeUp((0f - Time.deltaTime) * flags.rateDecreaseGauge, _force: false, _isIdle: false);
			if (flags.click == HFlag.ClickKind.mouth)
			{
				SetPlay("K_Touch");
				backIdle = 0;
				sprite.imageSpeedSlliderCover70.enabled = false;
			}
			else if (flags.click == HFlag.ClickKind.muneL || flags.click == HFlag.ClickKind.muneR)
			{
				SetPlay("M_Touch");
				SetSpriteDislikesis70((HandCtrl.AibuColliderKind)(2 + (flags.click - 16)));
			}
			else if (flags.click == HFlag.ClickKind.kokan)
			{
				SetPlay("A_Touch");
				SetSpriteDislikesis70(HandCtrl.AibuColliderKind.kokan);
			}
			else if (flags.click == HFlag.ClickKind.siriL || flags.click == HFlag.ClickKind.siriR || flags.click == HFlag.ClickKind.anal)
			{
				SetPlay("S_Touch");
				SetSpriteDislikesis70((HandCtrl.AibuColliderKind)(5 + (flags.click - 19)));
			}
			else if (flags.click == HFlag.ClickKind.anal_dislikes || flags.click == HFlag.ClickKind.massage_mune_dislikes || flags.click == HFlag.ClickKind.massage_kokan_dislikes)
			{
				SetMassageAndAnalDislikes();
			}
			else if (!hand.IsAction() && voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.breath && flags.speed <= 0.0001f && flags.voice.IsAibuIdleTime())
			{
				flags.voice.playVoices[0] = 100;
			}
		}
		else if (animatorStateInfo.IsName("M_Touch"))
		{
			if (animatorStateInfo.normalizedTime > 1f)
			{
				if (backIdle == -1)
				{
					SetPlay("M_Idle");
					SetIdleForItem(0);
				}
				else
				{
					GotoDislikes();
				}
			}
		}
		else if (animatorStateInfo.IsName("M_Idle"))
		{
			if (!hand.IsAction())
			{
				SetMoveCameraFlag();
			}
			if (IsVoiceWait())
			{
				return false;
			}
			if (flags.click == HFlag.ClickKind.orgW || flags.gaugeFemale >= 100f)
			{
				hand.ForceFinish();
				DetachItem();
				SetPlay("Orgasm_Start");
				flags.AddAibuOrg();
				flags.voice.playVoices[0] = 142;
				return true;
			}
			if (flags.click == HFlag.ClickKind.mouth)
			{
				SetPlay("K_Touch");
				backIdle = 1;
				sprite.imageSpeedSlliderCover70.enabled = false;
			}
			else if (flags.click == HFlag.ClickKind.muneL || flags.click == HFlag.ClickKind.muneR)
			{
				SetPlay("M_Touch");
				SetSpriteDislikesis70((HandCtrl.AibuColliderKind)(2 + (flags.click - 16)));
			}
			else if (flags.click == HFlag.ClickKind.kokan)
			{
				SetPlay("A_Touch");
				SetSpriteDislikesis70(HandCtrl.AibuColliderKind.kokan);
			}
			else if (flags.click == HFlag.ClickKind.siriL || flags.click == HFlag.ClickKind.siriR || flags.click == HFlag.ClickKind.anal)
			{
				SetPlay("S_Touch");
				SetSpriteDislikesis70((HandCtrl.AibuColliderKind)(5 + (flags.click - 19)));
			}
			else if (MathfEx.IsRange(HFlag.ClickKind.de_muneL, flags.click, HFlag.ClickKind.de_siriR, isEqual: true))
			{
				SetIdleForItem(0);
			}
			else if (flags.click == HFlag.ClickKind.anal_dislikes || flags.click == HFlag.ClickKind.massage_mune_dislikes || flags.click == HFlag.ClickKind.massage_kokan_dislikes)
			{
				SetMassageAndAnalDislikes();
			}
			else if (hand.IsAction())
			{
				JudgeDislikesis70(1);
			}
			if (!hand.IsAction())
			{
				flags.FemaleGaugeUp((0f - Time.deltaTime) * flags.rateDecreaseGauge, _force: false, _isIdle: false);
			}
		}
		else if (animatorStateInfo.IsName("A_Touch"))
		{
			if (animatorStateInfo.normalizedTime > 1f)
			{
				if (backIdle == -1)
				{
					SetPlay("A_Idle");
					SetIdleForItem(1);
				}
				else
				{
					GotoDislikes();
				}
			}
		}
		else if (animatorStateInfo.IsName("A_Idle"))
		{
			if (!hand.IsAction())
			{
				SetMoveCameraFlag();
			}
			if (IsVoiceWait())
			{
				return false;
			}
			if (flags.click == HFlag.ClickKind.orgW || flags.gaugeFemale >= 100f)
			{
				hand.ForceFinish();
				DetachItem();
				SetPlay("Orgasm_Start");
				flags.AddAibuOrg();
				flags.voice.playVoices[0] = 142;
				return true;
			}
			if (flags.click == HFlag.ClickKind.mouth)
			{
				SetPlay("K_Touch");
				backIdle = 2;
				sprite.imageSpeedSlliderCover70.enabled = false;
			}
			else if (flags.click == HFlag.ClickKind.kokan)
			{
				SetPlay("A_Touch");
				SetSpriteDislikesis70(HandCtrl.AibuColliderKind.kokan);
			}
			else if (flags.click == HFlag.ClickKind.muneL || flags.click == HFlag.ClickKind.muneR)
			{
				SetPlay("M_Touch");
				SetSpriteDislikesis70((HandCtrl.AibuColliderKind)(2 + (flags.click - 16)));
			}
			else if (flags.click == HFlag.ClickKind.siriL || flags.click == HFlag.ClickKind.siriR || flags.click == HFlag.ClickKind.anal)
			{
				SetPlay("S_Touch");
				SetSpriteDislikesis70((HandCtrl.AibuColliderKind)(5 + (flags.click - 19)));
			}
			else if (MathfEx.IsRange(HFlag.ClickKind.de_muneL, flags.click, HFlag.ClickKind.de_siriR, isEqual: true))
			{
				SetIdleForItem(1);
			}
			else if (flags.click == HFlag.ClickKind.anal_dislikes || flags.click == HFlag.ClickKind.massage_mune_dislikes || flags.click == HFlag.ClickKind.massage_kokan_dislikes)
			{
				SetMassageAndAnalDislikes();
			}
			else if (hand.IsAction())
			{
				JudgeDislikesis70(2);
			}
			if (!hand.IsAction())
			{
				flags.FemaleGaugeUp((0f - Time.deltaTime) * flags.rateDecreaseGauge, _force: false, _isIdle: false);
			}
		}
		else if (animatorStateInfo.IsName("S_Touch"))
		{
			if (animatorStateInfo.normalizedTime > 1f)
			{
				if (backIdle == -1)
				{
					SetPlay("S_Idle");
					SetIdleForItem(2);
				}
				else
				{
					GotoDislikes();
				}
			}
		}
		else if (animatorStateInfo.IsName("S_Idle"))
		{
			if (!hand.IsAction())
			{
				SetMoveCameraFlag();
			}
			if (IsVoiceWait())
			{
				return false;
			}
			if (flags.click == HFlag.ClickKind.orgW || flags.gaugeFemale >= 100f)
			{
				hand.ForceFinish();
				DetachItem();
				SetPlay("Orgasm_Start");
				flags.AddAibuOrg();
				flags.voice.playVoices[0] = 142;
				return true;
			}
			if (flags.click == HFlag.ClickKind.mouth)
			{
				SetPlay("K_Touch");
				backIdle = 3;
				sprite.imageSpeedSlliderCover70.enabled = false;
			}
			else if (flags.click == HFlag.ClickKind.muneL || flags.click == HFlag.ClickKind.muneR)
			{
				SetPlay("M_Touch");
				SetSpriteDislikesis70((HandCtrl.AibuColliderKind)(2 + (flags.click - 16)));
			}
			else if (flags.click == HFlag.ClickKind.kokan)
			{
				SetPlay("A_Touch");
				SetSpriteDislikesis70(HandCtrl.AibuColliderKind.kokan);
			}
			else if (flags.click == HFlag.ClickKind.siriL || flags.click == HFlag.ClickKind.siriR || flags.click == HFlag.ClickKind.anal)
			{
				SetPlay("S_Touch");
				SetSpriteDislikesis70((HandCtrl.AibuColliderKind)(5 + (flags.click - 19)));
			}
			else if (MathfEx.IsRange(HFlag.ClickKind.de_muneL, flags.click, HFlag.ClickKind.de_siriR, isEqual: true))
			{
				SetIdleForItem(2);
			}
			else if (flags.click == HFlag.ClickKind.anal_dislikes || flags.click == HFlag.ClickKind.massage_mune_dislikes || flags.click == HFlag.ClickKind.massage_kokan_dislikes)
			{
				SetMassageAndAnalDislikes();
			}
			else if (hand.IsAction())
			{
				JudgeDislikesis70(3);
			}
			if (!hand.IsAction())
			{
				flags.FemaleGaugeUp((0f - Time.deltaTime) * flags.rateDecreaseGauge, _force: false, _isIdle: false);
			}
		}
		else if (animatorStateInfo.IsName("K_Touch"))
		{
			if (animatorStateInfo.normalizedTime > 1f)
			{
				SetPlay("K_Loop", _fade: false);
			}
		}
		else if (animatorStateInfo.IsName("K_Loop"))
		{
			if (flags.click == HFlag.ClickKind.orgW || flags.gaugeFemale >= 100f)
			{
				hand.ForceFinish();
				DetachItem();
				backIdle = -1;
				SetPlay("Orgasm_Start");
				flags.AddAibuOrg();
				flags.voice.playVoices[0] = 142;
			}
			else if (hand.action == HandCtrl.HandAction.none)
			{
				flags.voice.playVoices[0] = 102;
				string[] array = new string[4] { "Idle", "M_Idle", "A_Idle", "S_Idle" };
				SetPlay(array[backIdle]);
				backIdle = -1;
				flags.timeNoClick = 0f;
				flags.speed = 0f;
			}
		}
		else if (animatorStateInfo.IsName("Front_Dislikes"))
		{
			SetMoveCameraFlag();
			string[] array2 = new string[4] { "Idle", "M_Idle", "A_Idle", "S_Idle" };
			if (!Voice.IsPlay(flags.transVoiceMouth[0]))
			{
				if (backIdle != 0)
				{
					int num = SetIdleForItem(backIdle - 1, _setplay: false);
					if (num != -1)
					{
						backIdle = num;
					}
				}
				hand.SetShapeON(HandCtrl.AibuColliderKind.muneL);
				hand.SetShapeON(HandCtrl.AibuColliderKind.muneR);
				SetPlay(array2[backIdle]);
				backIdle = -1;
				kindDislikes = -1;
				flags.timeSpeedUpStartCalc = flags.timeSpeedUpStartAibu;
			}
		}
		else if (animatorStateInfo.IsName("Back_Dislikes"))
		{
			SetMoveCameraFlag();
			string[] array3 = new string[4] { "Idle", "M_Idle", "A_Idle", "S_Idle" };
			if (!Voice.IsPlay(flags.transVoiceMouth[0]))
			{
				if (backIdle != 0)
				{
					int num2 = SetIdleForItem(backIdle - 1, _setplay: false);
					if (num2 != -1)
					{
						backIdle = num2;
					}
				}
				SetPlay(array3[backIdle]);
				backIdle = -1;
				kindDislikes = -1;
				flags.timeSpeedUpStartCalc = flags.timeSpeedUpStartAibu;
			}
		}
		else if (animatorStateInfo.IsName("Orgasm_Start"))
		{
			SetMoveCameraFlag();
			if (animatorStateInfo.normalizedTime > 1f)
			{
				flags.timeDecreaseGaugeCalc = 0f;
				SetPlay("Orgasm_Loop");
				if (randSio.Get() == 1)
				{
					particle.Play(2);
					Utils.Sound.Setting setting = new Utils.Sound.Setting();
					setting.loader.type = Manager.Sound.Type.GameSE3D;
					setting.loader.bundle = "sound/data/se/h/01.unity3d";
					setting.loader.asset = "hse_ks_siofuki";
					AudioSource audioSource = Utils.Sound.Play(setting);
					GameObject referenceInfo = female.GetReferenceInfo(ChaReference.RefObjKey.a_n_kokan);
					Vector3 pos = (referenceInfo ? referenceInfo.transform.position : Vector3.zero);
					Quaternion rot = (referenceInfo ? referenceInfo.transform.rotation : Quaternion.identity);
					Transform trans = ((audioSource == null) ? null : audioSource.transform);
					if (trans != null)
					{
						trans.UpdateAsObservable().Subscribe(delegate
						{
							if (trans != null)
							{
								trans.SetPositionAndRotation(pos, rot);
							}
						});
					}
				}
			}
		}
		else if (animatorStateInfo.IsName("Orgasm_Loop"))
		{
			SetMoveCameraFlag();
			flags.timeDecreaseGaugeCalc += Time.deltaTime;
			flags.timeDecreaseGaugeCalc = Mathf.Clamp(flags.timeDecreaseGaugeCalc + Time.deltaTime, 0f, flags.timeDecreaseGauge);
			float t = flags.timeDecreaseGaugeCalc / flags.timeDecreaseGauge;
			flags.gaugeFemale = Mathf.Lerp(100f, 0f, t);
			if (!Voice.IsPlay(flags.transVoiceMouth[0]))
			{
				flags.gaugeFemale = 0f;
				SetPlay("Orgasm_A");
				flags.voice.gentles[0] = false;
				flags.voice.SetAibuIdleTime();
				flags.voice.isFemale70PercentageVoicePlay = false;
				flags.voice.playVoices[0] = 143;
				flags.voice.isAfterVoicePlay = true;
			}
		}
		else if (animatorStateInfo.IsName("Orgasm_A"))
		{
			SetMoveCameraFlag();
			if (IsVoiceWait())
			{
				return false;
			}
			if (flags.click == HFlag.ClickKind.mouth)
			{
				SetPlay("K_Touch");
				backIdle = 0;
				flags.voice.SetAibuIdleTime();
				flags.voice.isAfterVoicePlay = false;
				sprite.imageSpeedSlliderCover70.enabled = false;
			}
			else if (flags.click == HFlag.ClickKind.muneL || flags.click == HFlag.ClickKind.muneR)
			{
				SetPlay("M_Touch");
				flags.voice.SetAibuIdleTime();
				flags.voice.isAfterVoicePlay = false;
				SetSpriteDislikesis70((HandCtrl.AibuColliderKind)(2 + (flags.click - 16)));
			}
			else if (flags.click == HFlag.ClickKind.kokan)
			{
				SetPlay("A_Touch");
				flags.voice.SetAibuIdleTime();
				flags.voice.isAfterVoicePlay = false;
				SetSpriteDislikesis70(HandCtrl.AibuColliderKind.kokan);
			}
			else if (flags.click == HFlag.ClickKind.siriL || flags.click == HFlag.ClickKind.siriR || flags.click == HFlag.ClickKind.anal)
			{
				SetPlay("S_Touch");
				flags.voice.SetAibuIdleTime();
				flags.voice.isAfterVoicePlay = false;
				SetSpriteDislikesis70((HandCtrl.AibuColliderKind)(5 + (flags.click - 19)));
			}
			else if (flags.click == HFlag.ClickKind.anal_dislikes || flags.click == HFlag.ClickKind.massage_mune_dislikes || flags.click == HFlag.ClickKind.massage_kokan_dislikes)
			{
				SetMassageAndAnalDislikes();
			}
		}
		SetAnimatorFloat("speedBody", 1f + flags.speed);
		SetAnimatorFloat("speedHand", 1f + flags.speedItem);
		SetAnimatorFloat("Breast", female.GetShapeBodyValue(4));
		flags.motion = Mathf.Clamp01(Mathf.InverseLerp(0f, flags.speedMaxAibuBody * 0.7f, flags.speed));
		SetDefaultAnimatorFloat();
		return true;
	}

	public override bool LateProc()
	{
		if (flags.click == HFlag.ClickKind.none && flags.selectAnimationListInfo != null)
		{
			return false;
		}
		hand.LateProc();
		return true;
	}

	private void DetachItem(int _isFront = -1)
	{
		for (int i = 0; i < 3; i++)
		{
			if (hand.IsFrontTouch(i) == _isFront || _isFront == -1)
			{
				hand.DetachItemByUseItem(i);
			}
		}
	}

	private int SetIdleForItem(int _area, bool _setplay = true)
	{
		int result = -1;
		if ((new bool[3]
		{
			!hand.IsUseAreaItemActive(0) && !hand.IsUseAreaItemActive(1),
			!hand.IsUseAreaItemActive(2),
			!hand.IsUseAreaItemActive(3) && !hand.IsUseAreaItemActive(4) && !hand.IsUseAreaItemActive(5)
		})[_area])
		{
			List<int> useItemNumber = hand.GetUseItemNumber();
			int count = useItemNumber.Count;
			if (count == 0)
			{
				if (_setplay)
				{
					SetPlay("Idle");
				}
				result = 0;
			}
			else
			{
				HandCtrl.AibuColliderKind useItemStickArea = hand.GetUseItemStickArea((count == 1) ? useItemNumber[0] : hand.GetTouchHistoryAt(1));
				bool[] array = new bool[3]
				{
					useItemStickArea == HandCtrl.AibuColliderKind.kokan,
					useItemStickArea == HandCtrl.AibuColliderKind.muneL || useItemStickArea == HandCtrl.AibuColliderKind.muneR,
					useItemStickArea == HandCtrl.AibuColliderKind.kokan
				};
				string[] array2 = new string[3] { "A_Idle", "M_Idle", "A_Idle" };
				string[] array3 = new string[3] { "S_Idle", "S_Idle", "M_Idle" };
				int[] array4 = new int[3] { 2, 1, 2 };
				int[] array5 = new int[3] { 3, 3, 1 };
				if (array[_area])
				{
					if (_setplay)
					{
						SetPlay(array2[_area]);
					}
					result = array4[_area];
				}
				else if (useItemStickArea != HandCtrl.AibuColliderKind.none)
				{
					if (_setplay)
					{
						SetPlay(array3[_area]);
					}
					result = array5[_area];
				}
			}
		}
		return result;
	}

	private void GotoDislikes()
	{
		hand.ForceFinish();
		if (kindDislikes == 0)
		{
			HSceneProc.FemaleParameter paramFemale = flags.nowAnimationInfo.paramFemale;
			bool flag = oldTouchKind < HandCtrl.AibuColliderKind.anal && (oldTouchKind != HandCtrl.AibuColliderKind.kokan || paramFemale.lstFrontAndBehind[0] == 0);
			DetachItem((!flag) ? 1 : 0);
			SetPlay(flag ? "Front_Dislikes" : "Back_Dislikes");
			if (oldTouchKind == HandCtrl.AibuColliderKind.muneL || oldTouchKind == HandCtrl.AibuColliderKind.muneR)
			{
				flags.voice.playVoices[0] = ((oldObj != 0) ? 108 : 104);
			}
			else if (oldTouchKind == HandCtrl.AibuColliderKind.kokan)
			{
				flags.voice.playVoices[0] = 105;
			}
			else if (oldTouchKind == HandCtrl.AibuColliderKind.anal)
			{
				flags.voice.playVoices[0] = 106;
			}
			else if (oldTouchKind == HandCtrl.AibuColliderKind.siriL || oldTouchKind == HandCtrl.AibuColliderKind.siriR)
			{
				flags.voice.playVoices[0] = 107;
			}
		}
		else
		{
			DetachItem((kindDislikes == 1) ? 1 : 0);
			SetPlay((kindDislikes != 1) ? "Front_Dislikes" : "Back_Dislikes");
			flags.voice.playVoices[0] = ((kindDislikes != 1) ? 110 : 109);
		}
	}

	private void SetDislikes(int _backIdle)
	{
		backIdle = _backIdle;
		int useAreaItemActive = hand.GetUseAreaItemActive();
		oldTouchKind = hand.GetUseItemStickArea(useAreaItemActive);
		oldObj = hand.GetUseItemStickObjectID(useAreaItemActive);
		if (oldTouchKind != HandCtrl.AibuColliderKind.none)
		{
			string[] array = new string[7] { "M_Touch", "M_Touch", "M_Touch", "A_Touch", "S_Touch", "S_Touch", "S_Touch" };
			SetPlay(array[(int)(oldTouchKind - 1)]);
			int[] array2 = new int[7] { -1, 0, 1, 2, 3, 4, 5 };
			flags.voice.playShorts[0] = array2[(int)(oldTouchKind - 1)];
			flags.voice.isShortsPlayTouchWeak[0] = false;
		}
	}

	private bool StartDislikes(int _backIdle)
	{
		int useAreaItemActive = hand.GetUseAreaItemActive();
		HandCtrl.AibuColliderKind useItemStickArea = hand.GetUseItemStickArea(useAreaItemActive);
		if (useItemStickArea == HandCtrl.AibuColliderKind.none)
		{
			return false;
		}
		int num = (int)(useItemStickArea - 1);
		int num2 = (new int[7] { 0, 1, 1, 2, 3, 4, 4 })[num];
		if (num2 == 1 && hand.GetUseItemStickObjectID(useAreaItemActive) != 0)
		{
			num2 = 5;
		}
		if (hand.IsAction())
		{
			if (num2 == 2 && flags.lstHeroine[0].hAreaExps[num2] + flags.lstHeroine[0].countKokanH == 0f)
			{
				kindDislikes = 0;
				SetDislikes(_backIdle);
				flags.AddNotPowerful();
			}
			else if (num2 == 3 && flags.lstHeroine[0].hAreaExps[num2] + flags.lstHeroine[0].countAnalH == 0f)
			{
				kindDislikes = 0;
				SetDislikes(_backIdle);
				flags.AddNotPowerful();
			}
			else if (flags.lstHeroine[0].hAreaExps[num2] == 0f)
			{
				kindDislikes = 0;
				SetDislikes(_backIdle);
				flags.AddNotPowerful();
			}
		}
		return true;
	}

	private void SetMassageAndAnalDislikes()
	{
		kindDislikes = ((flags.click == HFlag.ClickKind.anal_dislikes) ? 1 : 2);
		backIdle = 1;
		SetPlay((flags.click == HFlag.ClickKind.anal_dislikes) ? "S_Touch" : ((flags.click == HFlag.ClickKind.massage_mune_dislikes) ? "M_Touch" : "A_Touch"));
	}

	private bool JudgeDislikesis70(int _backIdle)
	{
		int useAreaItemActive = hand.GetUseAreaItemActive();
		HandCtrl.AibuColliderKind useItemStickArea = hand.GetUseItemStickArea(useAreaItemActive);
		if (useItemStickArea == HandCtrl.AibuColliderKind.none)
		{
			return false;
		}
		if (flags.speed >= flags.speedMaxAibuBody * 0.7f && !flags.lstHeroine[0].denial.aibu && GetAreaExperience(useItemStickArea) == 0)
		{
			StartDislikes(_backIdle);
		}
		return true;
	}

	private int GetAreaExperience(HandCtrl.AibuColliderKind _touchArea)
	{
		if (_touchArea == HandCtrl.AibuColliderKind.none)
		{
			return 0;
		}
		int num = (int)(_touchArea - 1);
		int[] obj = new int[7] { 0, 1, 1, 2, 3, 4, 4 };
		int useAreaItemActive = hand.GetUseAreaItemActive();
		int num2 = obj[num];
		if (hand.GetUseItemStickObjectID(useAreaItemActive) != 0 && num2 == 1)
		{
			num2 = 5;
		}
		switch (num2)
		{
		case 2:
		{
			float num3 = flags.lstHeroine[0].hAreaExps[num2] + flags.lstHeroine[0].countKokanH;
			if (num3 != 0f)
			{
				if (!(num3 >= 100f))
				{
					return 1;
				}
				return 2;
			}
			return 0;
		}
		case 3:
		{
			float num4 = flags.lstHeroine[0].hAreaExps[num2] + flags.lstHeroine[0].countAnalH;
			if (num4 != 0f)
			{
				if (!(num4 >= 100f))
				{
					return 1;
				}
				return 2;
			}
			return 0;
		}
		default:
			if (flags.lstHeroine[0].hAreaExps[num2] != 0f)
			{
				if (!(flags.lstHeroine[0].hAreaExps[num2] >= 100f))
				{
					return 1;
				}
				return 2;
			}
			return 0;
		}
	}

	private bool SetSpriteDislikesis70(HandCtrl.AibuColliderKind _touchArea)
	{
		sprite.imageSpeedSlliderCover70.enabled = !flags.lstHeroine[0].denial.aibu && GetAreaExperience(_touchArea) == 0;
		return true;
	}
}
