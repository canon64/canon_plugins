using System;
using System.Collections.Generic;
using System.Text;
using AIChara;
using Manager;
using UniRx;
using UnityEngine;

public class ProcBase
{
	protected struct animParm
	{
		public float[] heights;

		public float breast;

		public float[] m;

		public float speed;
	}

	protected HSceneFlagCtrl ctrlFlag;

	protected ChaControl[] chaFemales;

	protected ChaControl[] chaMales;

	protected CrossFade fade;

	protected MetaballCtrl ctrlMeta;

	protected HSceneSprite sprite;

	protected HItemCtrl item;

	protected FeelHit feelHit;

	protected HAutoCtrl auto;

	protected HVoiceCtrl voice;

	protected HParticleCtrl particle;

	protected HSeCtrl se;

	protected float[] timeChangeMotions = new float[2];

	protected float[] timeChangeMotionDeltaTimes = new float[2];

	protected ShuffleRand[] randVoicePlays = new ShuffleRand[2];

	protected bool isHeight1Parameter;

	protected List<Tuple<int, int, MotionIK>> lstMotionIK = new List<Tuple<int, int, MotionIK>>();

	protected ParticleSystem AtariEffect;

	protected ParticleSystem FeelHitEffect3D;

	public static bool endInit;

	protected BoolReactiveProperty isAtariHit = new BoolReactiveProperty(initialValue: false);

	private bool isAtariHitOld;

	protected static HSceneManager hSceneManager;

	protected int CatID = -1;

	protected StringBuilder sbWarning = new StringBuilder();

	protected HSceneSpriteHitem Hitem;

	protected AnimatorStateInfo FemaleAi;

	public ProcBase(DeliveryMember _delivery)
	{
		ctrlFlag = _delivery.ctrlFlag;
		chaMales = _delivery.chaMales;
		chaFemales = _delivery.chaFemales;
		fade = _delivery.fade;
		ctrlMeta = _delivery.ctrlMeta;
		sprite = _delivery.sprite;
		item = _delivery.item;
		feelHit = _delivery.feelHit;
		auto = _delivery.auto;
		voice = _delivery.voice;
		particle = _delivery.particle;
		se = _delivery.se;
		lstMotionIK = _delivery.lstMotionIK;
		AtariEffect = _delivery.AtariEffect;
		FeelHitEffect3D = _delivery.FeelHitEffect3D;
		Hitem = sprite.objHItem.GetComponent<HSceneSpriteHitem>();
		if (hSceneManager == null)
		{
			hSceneManager = Singleton<HSceneManager>.Instance;
		}
		for (int i = 0; i < 2; i++)
		{
			randVoicePlays[i] = new ShuffleRand();
			randVoicePlays[i].Init((i != 0) ? 2 : 3);
		}
		isAtariHit.Where((bool x) => isAtariHitOld != x && CatID != 1).Subscribe(delegate(bool x)
		{
			if ((CatID != 7 || Singleton<HSceneFlagCtrl>.Instance.nowAnimationInfo.ActionCtrl.Item2 != 1) && Singleton<HSceneFlagCtrl>.Instance.nowAnimationInfo.ActionCtrl.Item2 != 2)
			{
				isAtariHitOld = x;
				if (x)
				{
					AtariEffect.Play();
					if (Singleton<HSceneManager>.Instance.isParticle)
					{
						FeelHitEffect3D.Play();
					}
				}
				else
				{
					AtariEffect.Stop();
					FeelHitEffect3D.Stop();
				}
			}
		});
	}

	public virtual bool Init(int _modeCtrl)
	{
		endInit = true;
		ctrlFlag.lstSyncAnimLayers[0, 0].Clear();
		ctrlFlag.lstSyncAnimLayers[0, 1].Clear();
		ctrlFlag.lstSyncAnimLayers[1, 0].Clear();
		ctrlFlag.lstSyncAnimLayers[1, 1].Clear();
		Singleton<GameCursor>.Instance.setCursor(GameCursor.CursorKind.None, Vector2.zero);
		return true;
	}

	public virtual bool SetStartMotion(bool _isIdle, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		return true;
	}

	public virtual bool Proc(int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
	{
		return true;
	}

	public virtual void setAnimationParamater()
	{
	}

	public virtual bool ResetMotionSpeed()
	{
		auto.SetSpeed(ctrlFlag.speed);
		return true;
	}
}
