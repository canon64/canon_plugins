using System.Collections;
using Manager;
using SaveData;
using UnityEngine;
using UnityEngine.Events;

public class HPeeping : HActionBase
{
	public UnityAction<Heroine> OnChange;

	private float oldFrame;

	private PeepingFrameCtrl frameCtrl = new PeepingFrameCtrl();

	private HFlag.TimeWait timeVoice = new HFlag.TimeWait();

	public HPeeping(HFlag.DeliveryMember _membar)
		: base(_membar)
	{
		frameCtrl.Init(_membar.chaFemale, _membar.sprite, _membar.particle);
		timeVoice.MemberInit();
	}

	public void SetMapObject(GameObject _objMap)
	{
		frameCtrl.SetMapObj(_objMap);
	}

	public override bool MotionChange(int _motion)
	{
		flags.timeMasturbation.SetIdleTime();
		sprite.imageSpeedSlliderCover70.enabled = false;
		oldFrame = 0f;
		frameCtrl.Load(_motion);
		timeVoice.SetIdleTime();
		SetDefaultAnimatorFloat();
		return true;
	}

	public override bool Proc()
	{
		AnimatorStateInfo animatorStateInfo = female.getAnimatorStateInfo(0);
		frameCtrl.Proc(animatorStateInfo);
		float num = animatorStateInfo.normalizedTime % 1f;
		if (flags.nowAnimationInfo.numCtrl == 0)
		{
			if (!sprite.isFade)
			{
				HSprite.FadeKindProc fadeKindProc = sprite.GetFadeKindProc();
				if (fadeKindProc != HSprite.FadeKindProc.OutEnd)
				{
					SetMoveCameraFlag();
					if (animatorStateInfo.IsName("In"))
					{
						if (animatorStateInfo.normalizedTime >= 1f)
						{
							SetPlay("Loop", _fade: false);
							flags.voice.playVoices[0] = 501;
						}
					}
					else if (animatorStateInfo.IsName("Loop") && animatorStateInfo.normalizedTime >= 2f)
					{
						SetPlay("Out", _fade: false);
					}
				}
				else if (fadeKindProc == HSprite.FadeKindProc.OutEnd)
				{
					GlobalMethod.SetCameraMoveFlag(flags.ctrlCamera, _bPlay: false);
					if (Singleton<GameCursor>.Instance != null)
					{
						Singleton<GameCursor>.Instance.UnLockCursor();
					}
					if (Voice.IsPlay(flags.transVoiceMouth[0]))
					{
						Voice.Stop(flags.transVoiceMouth[0]);
					}
					female.animBody.speed = 0f;
					item.SetAnimationSpeed(0f);
					ConfirmDialog.Status status = ConfirmDialog.status;
					status.Title = "覗きシーン再確認";
					status.Sentence = "今の光景を思い出す";
					status.Yes = delegate
					{
						GlobalMethod.SetCameraMoveFlag(flags.ctrlCamera, _bPlay: true);
						SetPlay("In", _fade: false);
						sprite.FadeState(HSprite.FadeKind.In, 0.5f);
						female.animBody.speed = 1f;
						item.SetAnimationSpeed(1f);
						voice.StartCoroutine(InitOldMemberCoroutine());
					};
					status.No = delegate
					{
						sprite.OnClickHSceneEnd();
					};
					ConfirmDialog.Load();
				}
			}
		}
		else
		{
			SetMoveCameraFlag();
			if (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.breath && timeVoice.IsIdleTime())
			{
				flags.voice.playVoices[0] = 500;
			}
		}
		oldFrame = num;
		SetDefaultAnimatorFloat();
		return true;
	}

	private void SetRePlay(AnimatorStateInfo _ai, float _normalizetime, bool _isFade = true)
	{
		female.syncPlay(_ai.shortNameHash, 0, _normalizetime);
		item.SyncPlay(_ai.shortNameHash, _normalizetime);
		if (_isFade)
		{
			fade.FadeStart(1f);
		}
	}

	private IEnumerator InitOldMemberCoroutine()
	{
		yield return new WaitForEndOfFrame();
		se.InitOldMember(1);
		oldFrame = 0f;
	}
}
