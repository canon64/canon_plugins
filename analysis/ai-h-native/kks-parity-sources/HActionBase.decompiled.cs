using System.Collections.Generic;
using Manager;
using UnityEngine;

public class HActionBase
{
	protected ChaControl female;

	protected ChaControl female1;

	protected ChaControl male;

	protected ChaControl male1;

	protected HFlag flags;

	protected CrossFade fade;

	protected HSprite sprite;

	protected List<MotionIK> lstMotionIK;

	protected HandCtrl hand;

	protected HandCtrl hand1;

	protected YureCtrl yure;

	protected YureCtrl yure1;

	protected YureCtrl yureMale;

	protected ItemObject item;

	protected HitCollisionEnableCtrl hitcolFemale;

	protected HitCollisionEnableCtrl hitcolFemale1;

	protected HitCollisionEnableCtrl hitcolMale;

	protected ObiCtrl obi;

	protected ParentObjectCtrl parentObjectFemale;

	protected ParentObjectCtrl parentObjectFemale1;

	protected ParentObjectCtrl parentObjectMale;

	protected HVoiceCtrl voice;

	protected HSeCtrl se;

	protected HParticleCtrl particle;

	protected AnimatorLayerCtrl alCtrl;

	protected AnimatorLayerCtrl alCtrl1;

	protected float[] timeAutoMotions = new float[2];

	protected float[] timeAutoMotionCalcs = new float[2];

	protected float[] timeMotions = new float[2];

	protected float[] timeMotionCalcs = new float[2];

	protected bool[] enableMotions = new bool[2];

	protected bool[] allowMotions = new bool[2] { true, true };

	protected Vector2[] lerpMotions = new Vector2[2]
	{
		Vector2.zero,
		Vector2.zero
	};

	public HActionBase(HFlag.DeliveryMember _membar)
	{
		female = _membar.chaFemale;
		male = _membar.chaMale;
		male1 = _membar.chaMale1;
		flags = _membar.ctrlFlag;
		fade = _membar.fade;
		sprite = _membar.sprite;
		lstMotionIK = _membar.lstMotionIK;
		hand = _membar.hand;
		hand1 = _membar.hand1;
		yure = _membar.yure;
		item = _membar.item;
		hitcolFemale = _membar.hitcolFemale;
		hitcolMale = _membar.hitcolMale;
		obi = _membar.obi;
		parentObjectFemale = _membar.parentObjectFemale;
		parentObjectMale = _membar.parentObjectMale;
		voice = _membar.voice;
		se = _membar.se;
		particle = _membar.particle;
		female1 = _membar.chaFemale1;
		yure1 = _membar.yure1;
		yureMale = _membar.yureMale;
		parentObjectFemale1 = _membar.parentObjectFemale1;
		hitcolFemale1 = _membar.hitcolFemale1;
		alCtrl = _membar.alCtrl;
		alCtrl1 = _membar.alCtrl1;
	}

	public virtual bool Proc()
	{
		return true;
	}

	public virtual bool LateProc()
	{
		return true;
	}

	public virtual bool MotionChange(int _motion)
	{
		return true;
	}

	public bool SetPlay(string _nextAnimation, bool _fade = true)
	{
		flags.nowAnimStateName = _nextAnimation;
		if (_fade)
		{
			fade.FadeStart();
		}
		female.syncPlay(_nextAnimation, 0, 0f);
		if ((bool)female1 && female1.visibleAll)
		{
			female1.syncPlay(_nextAnimation, 0, 0f);
		}
		if (male.visibleAll)
		{
			male.syncPlay(_nextAnimation, 0, 0f);
		}
		if ((bool)male1 && male1.visibleAll)
		{
			male1.syncPlay(_nextAnimation, 0, 0f);
		}
		lstMotionIK.ForEach(delegate(MotionIK motionIK)
		{
			motionIK.Calc(_nextAnimation);
		});
		HSceneProc.FemaleParameter paramFemale = flags.nowAnimationInfo.paramFemale;
		HSceneProc.FemaleParameter paramFemale2 = flags.nowAnimationInfo.paramFemale1;
		yure.Proc(_nextAnimation, !paramFemale.isYure);
		if (yure1 != null)
		{
			yure1.Proc(_nextAnimation, !paramFemale2.isYure);
		}
		yureMale.Proc(_nextAnimation);
		hand.SetAnimation(_nextAnimation);
		hand1.SetAnimation(_nextAnimation);
		item.SetPlay(_nextAnimation, 0f);
		if (flags.testSiruKind == HFlag.TestSiruKind.メタボール)
		{
			hitcolFemale.SetPlay(_nextAnimation);
			hitcolFemale1.SetPlay(_nextAnimation);
			hitcolMale.SetPlay(_nextAnimation);
			parentObjectFemale.Proc(_nextAnimation);
			if ((bool)parentObjectFemale1)
			{
				parentObjectFemale1.Proc(_nextAnimation);
			}
			parentObjectMale.Proc(_nextAnimation);
		}
		else
		{
			hitcolFemale.SetPlayObi(_nextAnimation);
			hitcolFemale1.SetPlayObi(_nextAnimation);
			hitcolMale.SetPlayObi(_nextAnimation);
			parentObjectFemale.ProcObi(_nextAnimation);
			if ((bool)parentObjectFemale1)
			{
				parentObjectFemale1.ProcObi(_nextAnimation);
			}
			parentObjectMale.ProcObi(_nextAnimation);
		}
		alCtrl.Proc(_nextAnimation);
		alCtrl1.Proc(_nextAnimation);
		return true;
	}

	protected bool SetAnimatorFloat(string _param, float _value, bool _isMale = true, bool _isFemale1 = true)
	{
		int num = Animator.StringToHash(_param);
		female.setAnimatorParamFloat(num, _value);
		if ((bool)female1 && female1.visibleAll && _isFemale1)
		{
			female1.setAnimatorParamFloat(num, _value);
		}
		if (male.visibleAll && _isMale)
		{
			male.setAnimatorParamFloat(num, _value);
		}
		if ((bool)male1 && _isMale)
		{
			male1.setAnimatorParamFloat(num, _value);
		}
		item.SetAnimatorParamFloat(num, _value);
		return true;
	}

	protected bool SetAnimatorFloat(ChaControl _female, string _param, float _value)
	{
		_female.setAnimatorParamFloat(_param, _value);
		return true;
	}

	protected bool SetDefaultAnimatorFloat()
	{
		SetAnimatorFloat("motion", flags.motion);
		SetAnimatorFloat("height", female.GetShapeBodyValue(0), _isMale: true, _isFemale1: false);
		if ((bool)female1)
		{
			SetAnimatorFloat(female1, "height", female1.GetShapeBodyValue(0));
		}
		female.setAnimatorParamFloat(Animator.StringToHash("muneL_X"), flags.xy[0].x);
		female.setAnimatorParamFloat(Animator.StringToHash("muneL_Y"), flags.xy[0].y);
		female.setAnimatorParamFloat(Animator.StringToHash("muneR_X"), flags.xy[1].x);
		female.setAnimatorParamFloat(Animator.StringToHash("muneR_Y"), flags.xy[1].y);
		female.setAnimatorParamFloat(Animator.StringToHash("kokan_X"), flags.xy[2].x);
		female.setAnimatorParamFloat(Animator.StringToHash("kokan_Y"), flags.xy[2].y);
		female.setAnimatorParamFloat(Animator.StringToHash("anal_X"), flags.xy[3].x);
		female.setAnimatorParamFloat(Animator.StringToHash("anal_Y"), flags.xy[3].y);
		female.setAnimatorParamFloat(Animator.StringToHash("siriL_X"), flags.xy[4].x);
		female.setAnimatorParamFloat(Animator.StringToHash("siriL_Y"), flags.xy[4].y);
		female.setAnimatorParamFloat(Animator.StringToHash("siriR_X"), flags.xy[5].x);
		female.setAnimatorParamFloat(Animator.StringToHash("siriR_Y"), flags.xy[5].y);
		hand.SetItemAnimatorParam();
		return true;
	}

	protected bool IsBodyTouch()
	{
		return hand.GetUseItemNumber().Count != 0;
	}

	protected bool IsCheckVoicePlay(int _index)
	{
		if (voice.nowVoices[_index].state != HVoiceCtrl.VoiceKind.breath)
		{
			if (voice.nowVoices[_index].state == HVoiceCtrl.VoiceKind.voice)
			{
				return !Voice.IsPlay(flags.transVoiceMouth[_index]);
			}
			return false;
		}
		return true;
	}

	protected bool IsVoiceWait(bool _is3P = false)
	{
		if (flags.selectAnimationListInfo == null)
		{
			return false;
		}
		if (!_is3P)
		{
			if (IsCheckVoicePlay(0))
			{
				flags.voiceWait = false;
			}
		}
		else if (IsCheckVoicePlay(0) && IsCheckVoicePlay(1))
		{
			flags.voiceWait = false;
		}
		return true;
	}

	protected bool IsVoicePlay(int _index)
	{
		if (voice.nowVoices[_index].state == HVoiceCtrl.VoiceKind.voice)
		{
			return Voice.IsPlay(flags.transVoiceMouth[_index]);
		}
		return false;
	}

	protected void SetMoveCameraFlag()
	{
		if (!GlobalMethod.IsCameraMoveFlag(flags.ctrlCamera) && !Input.GetMouseButton(0))
		{
			GlobalMethod.SetCameraMoveFlag(flags.ctrlCamera, _bPlay: true);
		}
	}
}
