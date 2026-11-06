using System.Collections.Generic;
using ADV.Commands.Base;
using AIChara;
using AIProject;
using Illusion.Game.Elements.EasyLoader;
using Manager;
using UnityEngine;
using UnityEngine.AI;

namespace ADV;

public class CharaData
{
	public class MotionReserver
	{
		public IKMotion ikMotion { get; set; }

		public YureMotion yureMotion { get; set; }

		public MotionOverride motionOverride { get; set; }
	}

	public class Backup
	{
		private bool isRepair;

		public Transform transform { get; }

		private Vector3 position { get; }

		private Quaternion rotation { get; }

		private bool isNavmesh { get; }

		private NavMeshAgent navMeshAgent { get; }

		public Backup(TextScenario.ParamData data)
		{
			navMeshAgent = data.actor.NavMeshAgent;
			isNavmesh = navMeshAgent.enabled;
			transform = data.actor.Animation.Character.transform;
			position = transform.localPosition;
			rotation = transform.localRotation;
		}

		public void Set()
		{
			isRepair = true;
			navMeshAgent.enabled = false;
		}

		public void Repair()
		{
			if (isRepair)
			{
				if (transform != null)
				{
					transform.localPosition = position;
					transform.localRotation = rotation;
				}
				if (navMeshAgent != null)
				{
					navMeshAgent.enabled = isNavmesh;
				}
			}
		}
	}

	public class CharaItem
	{
		private Illusion.Game.Elements.EasyLoader.Motion motion;

		public GameObject item { get; private set; }

		public CharaItem()
		{
		}

		public CharaItem(GameObject item)
		{
			this.item = item;
		}

		public void Delete()
		{
			if (item != null)
			{
				Object.Destroy(item);
				item = null;
			}
		}

		public void LoadObject(string bundle, string asset, Transform root, bool worldPositionStays = false, string manifest = null)
		{
			Delete();
			GameObject asset2 = AssetBundleManager.LoadAsset(bundle, asset, typeof(GameObject), manifest).GetAsset<GameObject>();
			item = Object.Instantiate(asset2);
			AssetBundleManager.UnloadAssetBundle(bundle, isUnloadForceRefCount: false, manifest);
			item.name = asset2.name;
			item.transform.SetParent(root, worldPositionStays);
		}

		public void LoadAnimator(string bundle, string asset, string state)
		{
			Animator orAddComponent = item.GetOrAddComponent<Animator>();
			if (motion == null)
			{
				motion = new Illusion.Game.Elements.EasyLoader.Motion(bundle, asset, state);
			}
			if (motion.Setting(orAddComponent, bundle, asset, state, isCheck: true))
			{
				motion.Play(orAddComponent);
			}
		}
	}

	private ChaControl _chaCtrl;

	private IKMotion _ikMotion;

	private MotionOverride _motionOverride = new MotionOverride();

	private TextScenario scenario;

	public Dictionary<int, CharaItem> itemDic { get; private set; }

	public bool initialized { get; private set; }

	private bool isADVCreateChara { get; set; }

	private GameObject dataBaseRoot { get; set; }

	public Transform root
	{
		get
		{
			if (chaCtrl == null)
			{
				return null;
			}
			return chaCtrl.transform;
		}
	}

	public int voiceNo => data.voiceNo;

	public float voicePitch => data.voicePitch;

	public Transform voiceTrans
	{
		get
		{
			if (chaCtrl == null || !chaCtrl.loadEnd)
			{
				return null;
			}
			Transform trfHeadParent = chaCtrl.cmpBoneBody.targetEtc.trfHeadParent;
			if (trfHeadParent == null)
			{
				return null;
			}
			return trfHeadParent.transform;
		}
	}

	public TextScenario.ParamData data { get; private set; }

	public ChaControl chaCtrl => this.GetCacheObject(ref _chaCtrl, () => data.chaCtrl);

	public Transform transform
	{
		get
		{
			if (data.transform != null)
			{
				return data.transform;
			}
			if (chaCtrl == null)
			{
				return null;
			}
			return chaCtrl.transform;
		}
	}

	private MotionReserver motionReserver { get; set; }

	public IKMotion ikMotion => this.GetCache(ref _ikMotion, delegate
	{
		IKMotion iKMotion = new IKMotion();
		iKMotion.Create(chaCtrl, null);
		return iKMotion;
	});

	public YureMotion yureMotion => _yureMotion;

	private YureMotion _yureMotion { get; set; }

	public MotionOverride motionOverride => _motionOverride;

	public Backup backup { get; set; }

	public CharaData(TextScenario.ParamData data, TextScenario scenario)
	{
		this.data = data;
		this.scenario = scenario;
		itemDic = new Dictionary<int, CharaItem>();
		isADVCreateChara = true;
		initialized = true;
	}

	public CharaData(TextScenario.ParamData data, TextScenario scenario, MotionReserver motionReserver)
	{
		this.data = data;
		this.scenario = scenario;
		this.motionReserver = motionReserver;
		_chaCtrl = data.chaCtrl;
		isADVCreateChara = _chaCtrl == null;
		if (_chaCtrl != null)
		{
			Initialize();
			backup = new Backup(data);
		}
	}

	public void Initialize()
	{
		itemDic = new Dictionary<int, CharaItem>();
		if (motionReserver != null)
		{
			if (motionReserver.ikMotion != null)
			{
				_ikMotion = motionReserver.ikMotion;
			}
			if (motionReserver.yureMotion != null)
			{
				_yureMotion = motionReserver.yureMotion;
			}
			if (motionReserver.motionOverride != null)
			{
				_motionOverride = motionReserver.motionOverride;
			}
		}
		if (_yureMotion == null)
		{
			_yureMotion = new YureMotion();
			_yureMotion.Create(chaCtrl);
		}
		if (isADVCreateChara)
		{
			scenario.commandController.LoadingCharaList.Remove(this);
			chaCtrl.SetActiveTop(active: true);
			MotionIK motionIK = new MotionIK(chaCtrl);
			if (_ikMotion == null)
			{
				_ikMotion = new IKMotion();
				_ikMotion.Create(chaCtrl, motionIK);
			}
		}
		initialized = true;
	}

	public void MotionPlay(ADV.Commands.Base.Motion.Data motion, bool isCrossFade)
	{
		if (isCrossFade)
		{
			scenario.CrossFadeStart();
		}
		if (!motion.pair.HasValue)
		{
			if (_motionOverride.Setting(chaCtrl.animBody, motion.assetBundleName, motion.assetName, motion.overrideAssetBundleName, motion.overrideAssetName, motion.stateName, isCheck: true))
			{
				Info.Anime.Play play = scenario.info.anime.play;
				_motionOverride.isCrossFade = play.isCrossFade;
				_motionOverride.layers = motion.layerNo;
				_motionOverride.transitionDuration = play.transitionDuration;
				_motionOverride.normalizedTime = play.normalizedTime;
				_motionOverride.Play(chaCtrl.animBody);
			}
			IKMotion iKMotion = ikMotion;
			bool enabled = data.actor.Animation.enabled;
			iKMotion.use = !enabled;
			iKMotion.motionIK.enabled = enabled;
			iKMotion.Setting(chaCtrl, motion.ikAssetBundleName, motion.ikAssetName, motion.stateName, isCheck: false);
			_yureMotion.Setting(chaCtrl, motion.shakeAssetBundleName, motion.shakeAssetName, motion.stateName, isCheck: false);
		}
		else
		{
			int postureID = motion.pair.Value.postureID;
			int poseID = motion.pair.Value.poseID;
			Actor actor = data.actor;
			actor.ActionID = postureID;
			actor.PoseID = poseID;
			PlayState autoPlayState = GetAutoPlayState();
			if (autoPlayState != null)
			{
				actor.Animation.StopAllAnimCoroutine();
				ActorAnimInfo actorAnimInfo = actor.Animation.LoadActionState(postureID, poseID, autoPlayState);
				actor.Animation.PlayInAnimation(actorAnimInfo.inEnableBlend, actorAnimInfo.inBlendSec, autoPlayState.MainStateInfo.FadeOutTime, actorAnimInfo.layer);
			}
		}
	}

	public PlayState GetAutoPlayState()
	{
		Actor actor = data.actor;
		if (actor == null)
		{
			return null;
		}
		PlayState playState = null;
		Manager.Resources.AnimationTables animation = Singleton<Manager.Resources>.Instance.Animation;
		if (data.agentActor != null)
		{
			playState = GetPlayState(animation.AgentActionAnimTable);
		}
		else if (data.playerActor != null)
		{
			PlayerActor playerActor = data.playerActor;
			playState = GetPlayState(animation.PlayerActionAnimTable[playerActor.PlayerData.Sex]);
		}
		else
		{
			playState = GetPlayState(animation.MerchantOnlyActionAnimStateTable);
			if (playState == null)
			{
				playState = GetPlayState(animation.MerchantCommonActionAnimStateTable);
			}
			if (playState == null)
			{
				playState = GetPlayState(animation.AgentActionAnimTable);
			}
		}
		return playState;
	}

	public PlayState GetPlayState(Dictionary<int, Dictionary<int, PlayState>> stateTable)
	{
		Actor actor = data.actor;
		if (actor == null)
		{
			return null;
		}
		return GetPlayState(stateTable, actor.ActionID, actor.PoseID);
	}

	private static PlayState GetPlayState(IReadOnlyDictionary<int, Dictionary<int, PlayState>> stateTable, int postureID, int poseID)
	{
		if (stateTable.TryGetValue(postureID, out var value) && value.TryGetValue(poseID, out var value2))
		{
			return value2;
		}
		return null;
	}

	public void Release()
	{
		if (initialized)
		{
			foreach (KeyValuePair<int, CharaItem> item in itemDic)
			{
				item.Value.Delete();
			}
		}
		if (backup != null)
		{
			backup.Repair();
		}
	}
}
