using System;
using System.Collections.Generic;
using AIChara;
using UnityEngine;

public class DeliveryMember
{
	public HSceneFlagCtrl ctrlFlag;

	public ChaControl[] chaFemales;

	public ChaControl[] chaMales;

	public CrossFade fade;

	public MetaballCtrl ctrlMeta;

	public HSceneSprite sprite;

	public HItemCtrl item;

	public FeelHit feelHit;

	public HAutoCtrl auto;

	public HVoiceCtrl voice;

	public HParticleCtrl particle;

	public ParticleSystem AtariEffect;

	public ParticleSystem FeelHitEffect3D;

	public HSeCtrl se;

	public List<Tuple<int, int, MotionIK>> lstMotionIK;
}
