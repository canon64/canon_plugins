using System.Collections;
using AIProject.Scene;
using UnityEngine;

public class Peeping : ProcBase
{
	private float oldFrame;

	private animParm animPar;

	public Peeping(DeliveryMember _delivery)
		: base(_delivery)
	{
		animPar.heights = new float[1];
		CatID = 5;
	}

	public override bool SetStartMotion(bool _isIdle, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		AtariEffect.Stop();
		ctrlFlag.isNotCtrl = false;
		oldFrame = 0f;
		return true;
	}

	public override bool Proc(int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		if (chaFemales[0].objTop == null)
		{
			return false;
		}
		FemaleAi = chaFemales[0].getAnimatorStateInfo(0);
		float num = FemaleAi.normalizedTime % 1f;
		if (ctrlFlag.nowAnimationInfo.id == 10 && !sprite.isFade)
		{
			HSceneSprite.FadeKindProc fadeKindProc = sprite.GetFadeKindProc();
			if (fadeKindProc != HSceneSprite.FadeKindProc.OutEnd)
			{
				if (num > 0.93f)
				{
					sprite.FadeState(HSceneSprite.FadeKind.Out, 1.5f);
				}
				if (oldFrame <= 0.05f && 0.05f < num)
				{
					GlobalMethod.SetAllClothState(chaFemales[0], _isUpper: false, 2, _isForce: true);
				}
				else if (oldFrame <= 0.21f && 0.21f < num)
				{
					particle.Play(2);
				}
				else if (oldFrame <= 0.82f && 0.82f < num)
				{
					GlobalMethod.SetAllClothState(chaFemales[0], _isUpper: false, 0, _isForce: true);
				}
			}
			else if (fadeKindProc == HSceneSprite.FadeKindProc.OutEnd)
			{
				GlobalMethod.setCameraMoveFlag(ctrlFlag.cameraCtrl, _bPlay: false);
				chaFemales[0].animBody.speed = 0f;
				ConfirmScene.Sentence = string.Empty;
				ConfirmScene.OnClickedYes = delegate
				{
					ctrlFlag.click = HSceneFlagCtrl.ClickKind.PeepingRestart;
				};
				ConfirmScene.OnClickedNo = delegate
				{
					ctrlFlag.click = HSceneFlagCtrl.ClickKind.SceneEnd;
				};
				if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.PeepingRestart)
				{
					setRePlay(FemaleAi, 0f, _isFade: false);
					sprite.FadeState(HSceneSprite.FadeKind.In, 0.5f);
					chaFemales[0].animBody.speed = 1f;
					voice.StartCoroutine(InitOldMemberCoroutine());
				}
			}
		}
		oldFrame = num;
		ctrlMeta.Proc(FemaleAi);
		setAnimationParamater();
		return true;
	}

	public override void setAnimationParamater()
	{
		if (chaFemales[0].visibleAll && chaFemales[0].objTop != null)
		{
			animPar.heights[0] = chaFemales[0].GetShapeBodyValue(0);
			chaFemales[0].setAnimatorParamFloat("height", animPar.heights[0]);
		}
		if (item.GetItem() != null)
		{
			item.setAnimatorParamFloat("height", animPar.heights[0]);
		}
	}

	private void setRePlay(AnimatorStateInfo _ai, float _normalizetime, bool _isFade = true)
	{
		chaFemales[0].syncPlay(_ai.shortNameHash, 0, _normalizetime);
		if (item != null)
		{
			item.syncPlay(_ai.shortNameHash, _normalizetime);
		}
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
		yield return null;
	}
}
