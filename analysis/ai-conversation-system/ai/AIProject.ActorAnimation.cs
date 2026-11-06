using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AIChara;
using IllusionUtility.GetUtility;
using Manager;
using ReMotion;
using Sound;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEx;

namespace AIProject;

public abstract class ActorAnimation : MonoBehaviour
{
	public class AnimatorStateEvent
	{
		public float NormalizedTime { get; set; }

		public int EventID { get; set; }

		public bool Used { get; set; }
	}

	public class ExpressionKeyframeEvent
	{
		public float NormalizedTime { get; set; }

		public string Name { get; set; }

		public bool Used { get; set; }
	}

	public class AnimatorStateEvent_SE
	{
		public float NormalizedTime { get; set; }

		public int ClipID { get; set; } = -1;


		public int EventID { get; set; } = -1;


		public Transform Root { get; set; }

		public bool Used { get; set; }
	}

	public class AnimatorStateEvent_Particle
	{
		public float NormalizedTime { get; set; } = float.MaxValue;


		public int ParticleID { get; set; } = -1;


		public int EventID { get; set; } = -1;


		public string Root { get; set; }

		public bool Used { get; set; }
	}

	public class AnimatorStateEvent_OnceVoice
	{
		public float NormalizedTime { get; set; } = float.MaxValue;


		public int[] IDs { get; set; }

		public bool Used { get; set; }
	}

	[SerializeField]
	protected ActorLocomotion _character;

	public Animator Animator;

	private RuntimeAnimatorController _defaultAnimController;

	private PlayState.Info _maskState = default(PlayState.Info);

	protected UnityEx.ValueTuple<int, AudioSource, Transform, int> _loopActionVoice = new UnityEx.ValueTuple<int, AudioSource, Transform, int>(-1, null, null, -1);

	protected MotionIK _motionIK;

	protected string _preAnimatorName = string.Empty;

	protected AnimatorControllerParameter[] _parameters;

	protected List<ProceduralTargetParameter> _targets = new List<ProceduralTargetParameter>();

	private float _prevNormalizedTime;

	private int _loopCount;

	private bool _ignoreAnimEvent;

	private bool _ignoreExpressionEvent;

	private bool _ignoreVoiceEvent;

	private static readonly bool[] _defaultYure = new bool[7] { true, true, true, true, true, true, true };

	private const bool _defaultNip = true;

	private YureCtrl.BreastShapeInfo[] _shapeInfo = new YureCtrl.BreastShapeInfo[2];

	private string _animatorName = string.Empty;

	private int _nameHash;

	private bool _isFootStepActive = true;

	private FootStepInfo _prevFootStepInfo;

	private List<FootStepInfo> _footStepEvents = new List<FootStepInfo>();

	protected Dictionary<int, FootStepInfo[]> _footStepEventKeyTable = new Dictionary<int, FootStepInfo[]>();

	private List<AudioSource> _footStepAudioSources = new List<AudioSource>();

	protected static RaycastHit[] _raycastHits = new RaycastHit[3];

	private Queue<PlayState.Info> _recoveryStateFromTurn = new Queue<PlayState.Info>();

	private IEnumerator _inLocoAnimEnumerator;

	private IDisposable _inLocoAnimDisposable;

	private IEnumerator _inAnimEnumerator;

	private IDisposable _inAnimDisposable;

	private IEnumerator _outAnimEnumerator;

	private IDisposable _outAnimDisposable;

	private IEnumerator _actionAnimEnumerator;

	private IDisposable _actionAnimDisposable;

	private IEnumerator _turnAnimEnumerator;

	private IDisposable _turnAnimDisposable;

	private IEnumerator _onceActionAnimEnumerator;

	private IDisposable _onceActionAnimDisposable;

	public Actor Actor { get; set; }

	public ActorLocomotion Character
	{
		get
		{
			return _character;
		}
		set
		{
			_character = value;
		}
	}

	public bool EnabledPoser { get; set; }

	public Animator ArmAnimator { get; set; }

	public JointPoser Poser { get; protected set; }

	public bool IsLocomotionState { get; protected set; }

	public AssetBundleInfo AnimABInfo { get; set; }

	public Queue<PlayState.Info> InStates { get; } = new Queue<PlayState.Info>();


	public Queue<PlayState.Info> OutStates { get; } = new Queue<PlayState.Info>();


	public List<PlayState.PlayStateInfo> ActionStates { get; } = new List<PlayState.PlayStateInfo>();


	public Queue<PlayState.Info> ActionInStates { get; } = new Queue<PlayState.Info>();


	public Queue<PlayState.Info> ActionOutStates { get; } = new Queue<PlayState.Info>();


	public List<PlayState.Info> OnceActionStates { get; } = new List<PlayState.Info>();


	public ActionPointInfo ActionPointInfo { get; set; }

	public DateActionPointInfo DateActionPointInfo { get; set; }

	public bool RefsActAnimInfo { get; set; }

	public ActorAnimInfo AnimInfo { get; set; }

	public Transform RecoveryPoint { get; set; }

	public AudioSource OnceActionVoice { get; protected set; }

	public UnityEx.ValueTuple<int, AudioSource, Transform, int> LoopActionVoice => _loopActionVoice;

	public MotionIK MapIK
	{
		[CompilerGenerated]
		get
		{
			return _motionIK;
		}
	}

	public bool CanUseMapIK { get; set; } = true;


	public AnimatorControllerParameter[] Parameters
	{
		[CompilerGenerated]
		get
		{
			return _parameters;
		}
	}

	public string CurrentStateName { get; set; } = string.Empty;


	public List<ProceduralTargetParameter> Targets
	{
		[CompilerGenerated]
		get
		{
			return _targets;
		}
	}

	public Dictionary<int, List<AnimeEventInfo>> ItemEventKeyTable { get; set; }

	public Dictionary<int, List<AnimeEventInfo>> PartnerEventKeyTable { get; set; }

	public Dictionary<int, List<AnimeSEEventInfo>> SEEventKeyTable { get; set; }

	public Dictionary<int, List<AnimeParticleEventInfo>> ParticleEventKeyTable { get; set; }

	public Dictionary<int, List<AnimeOnceVoiceEventInfo>> OnceVoiceEventKeyTable { get; set; }

	public Dictionary<int, List<int>> LoopVoiceEventKeyTable { get; set; }

	public Dictionary<int, List<AnimeEventInfo>> ClothEventKeyTable { get; set; }

	public Dictionary<int, YureCtrl.Info> YureTable { get; set; }

	public List<AnimatorStateEvent> StateItemEvents { get; set; } = new List<AnimatorStateEvent>();


	public List<AnimatorStateEvent> StatePartnerEvents { get; set; } = new List<AnimatorStateEvent>();


	public List<AnimatorStateEvent_SE> StateSEEvents { get; set; } = new List<AnimatorStateEvent_SE>();


	public List<AnimatorStateEvent_Particle> StateParticleEvents { get; set; } = new List<AnimatorStateEvent_Particle>();


	public List<AnimatorStateEvent_OnceVoice> StateOnceVoiceEvents { get; set; } = new List<AnimatorStateEvent_OnceVoice>();


	public List<int> StateLoopVoiceEvents { get; set; } = new List<int>();


	public List<AnimatorStateEvent> StateClothEvents { get; set; } = new List<AnimatorStateEvent>();


	public List<ExpressionKeyframeEvent> ExpressionKeyframeList { get; set; } = new List<ExpressionKeyframeEvent>();


	public List<UnityEx.ValueTuple<int, GameObject>> Items { get; private set; } = new List<UnityEx.ValueTuple<int, GameObject>>();


	public List<UnityEx.ValueTuple<int, Renderer[]>> ItemRenderers { get; private set; } = new List<UnityEx.ValueTuple<int, Renderer[]>>();


	public Dictionary<int, ItemAnimInfo> ItemAnimatorTable { get; private set; } = new Dictionary<int, ItemAnimInfo>();


	public Dictionary<int, Dictionary<int, UnityEx.ValueTuple<AudioSource, FadePlayer>>> ActionLoopSETable { get; private set; } = new Dictionary<int, Dictionary<int, UnityEx.ValueTuple<AudioSource, FadePlayer>>>();


	public Dictionary<int, Dictionary<int, Tuple<GameObject, ParticleSystem, ParticleSystemRenderer>>> Particles { get; private set; } = new Dictionary<int, Dictionary<int, Tuple<GameObject, ParticleSystem, ParticleSystemRenderer>>>();


	public bool IsFootStepActive
	{
		get
		{
			return _isFootStepActive;
		}
		set
		{
			_prevFootStepInfo = null;
			_isFootStepActive = value;
		}
	}

	public bool PlayingInLocoAnimation => _inLocoAnimEnumerator != null;

	public bool PlayingInAnimation => _inAnimEnumerator != null;

	public bool PlayingOutAnimation => _outAnimEnumerator != null;

	public bool PlayingActAnimation => _actionAnimEnumerator != null;

	public bool PlayingTurnAnimation => _turnAnimEnumerator != null;

	public bool PlayingOnceActionAnimation => _onceActionAnimEnumerator != null;

	public abstract Vector3 GetPivotPoint();

	public float GetAngleFromForward(Vector3 worldDirection)
	{
		Vector3 vector = base.transform.InverseTransformDirection(worldDirection);
		return Mathf.Atan2(vector.x, vector.z) * 57.29578f;
	}

	public void SetDefaultAnimatorController(RuntimeAnimatorController rac)
	{
		_defaultAnimController = rac;
	}

	public void SetAnimatorController(RuntimeAnimatorController rac)
	{
		Animator.runtimeAnimatorController = rac;
		_parameters = Animator.parameters;
		if (_motionIK == null)
		{
			_motionIK = new MotionIK(Actor.ChaControl);
			_motionIK.MapIK = true;
		}
		if (!_ignoreAnimEvent)
		{
			_motionIK.SetMapIK(Animator.runtimeAnimatorController.name);
			_nameHash = 0;
		}
	}

	public void ResetDefaultAnimatorController()
	{
		SetAnimatorController(_defaultAnimController);
	}

	protected virtual void Start()
	{
		if (_character != null)
		{
			base.transform.position = _character.transform.position;
			base.transform.rotation = _character.transform.rotation;
		}
		for (int i = 0; i < _shapeInfo.Length; i++)
		{
			_shapeInfo[i].MemberInit();
		}
		(from _ in this.OnAnimatorMoveAsObservable().TakeUntilDestroy(base.gameObject)
			where base.isActiveAndEnabled
			select _).Subscribe(delegate
		{
		});
		(from _ in Observable.EveryUpdate().TakeUntilDestroy(base.gameObject)
			where base.isActiveAndEnabled
			select _).Subscribe(delegate
		{
			OnUpdate();
			Follow();
		});
	}

	protected virtual void OnDestroy()
	{
		StopAllAnimCoroutine();
		foreach (AudioSource footStepAudioSource in _footStepAudioSources)
		{
			if (!(footStepAudioSource == null) && !(footStepAudioSource.gameObject == null) && footStepAudioSource.isPlaying)
			{
				footStepAudioSource.Stop();
			}
		}
		StopLoopActionVoice();
		StopAllActionLoopSE();
		if (OnceActionVoice != null && OnceActionVoice.gameObject != null)
		{
			Actor actor = Actor;
			ActorLocomotion actorLocomotion = ((!(actor != null)) ? null : actor.Locomotor);
			Transform transform = ((!(actorLocomotion != null)) ? null : actorLocomotion.transform);
			if (transform != null && Singleton<Voice>.IsInstance())
			{
				Singleton<Voice>.Instance.Stop(transform);
			}
			else
			{
				UnityEngine.Object.Destroy(OnceActionVoice.gameObject);
			}
		}
	}

	protected void OnUpdate()
	{
		if (_targets != null)
		{
			AnimatorStateInfo currentAnimatorStateInfo = Animator.GetCurrentAnimatorStateInfo(0);
			bool flag = currentAnimatorStateInfo.IsName(CurrentStateName);
			bool flag2 = Animator.IsInTransition(0);
			if (flag && currentAnimatorStateInfo.normalizedTime < 0.9f && !flag2)
			{
				MatchTargetWeightMask weightMask = new MatchTargetWeightMask(Vector3.one, 0f);
				foreach (ProceduralTargetParameter target in _targets)
				{
					if (target.Target != null)
					{
						Animator.MatchTarget(target.Target.position, Quaternion.identity, AvatarTarget.Root, weightMask, target.Start, target.End);
					}
				}
			}
		}
		UpdateAnimationEvent();
	}

	protected void Follow()
	{
		base.transform.position = _character.transform.position;
		base.transform.rotation = _character.transform.rotation;
	}

	public float GetFloat(string parameterName)
	{
		return Animator.GetFloat(parameterName);
	}

	public float GetFloat(int parameterHash)
	{
		return Animator.GetFloat(parameterHash);
	}

	public void SetFloat(string parameterName, float value)
	{
		Animator.SetFloat(parameterName, value);
	}

	public void SetFloat(int parameterHash, float value)
	{
		Animator.SetFloat(parameterHash, value);
	}

	private void LoadStateEvents(Dictionary<int, List<AnimeEventInfo>> table, List<AnimatorStateEvent> list)
	{
		if (table == null || !table.TryGetValue(_nameHash, out var value) || value.IsNullOrEmpty())
		{
			return;
		}
		foreach (AnimeEventInfo item in value)
		{
			list.Add(new AnimatorStateEvent
			{
				NormalizedTime = item.normalizedTime,
				EventID = item.eventID
			});
		}
	}

	private void LoadStateSEEvents(Dictionary<int, List<AnimeSEEventInfo>> table, List<AnimatorStateEvent_SE> list)
	{
		if (table.IsNullOrEmpty() || !table.TryGetValue(_nameHash, out var value) || value.IsNullOrEmpty())
		{
			return;
		}
		foreach (AnimeSEEventInfo item in value)
		{
			list.Add(new AnimatorStateEvent_SE
			{
				NormalizedTime = item.NormalizedTime,
				ClipID = item.ClipID,
				EventID = item.EventID,
				Root = (Actor.ChaControl.transform.FindLoop(item.Root)?.transform ?? Actor.Locomotor.transform)
			});
		}
	}

	private void LoadStateParticleEvents(Dictionary<int, List<AnimeParticleEventInfo>> table, List<AnimatorStateEvent_Particle> list)
	{
		if (table.IsNullOrEmpty() || !table.TryGetValue(_nameHash, out var value) || value.IsNullOrEmpty())
		{
			return;
		}
		foreach (AnimeParticleEventInfo item in value)
		{
			list.Add(new AnimatorStateEvent_Particle
			{
				NormalizedTime = item.NormalizedTime,
				ParticleID = item.ParticleID,
				EventID = item.EventID,
				Root = item.Root,
				Used = false
			});
		}
	}

	private void LoadStateOnceVoiceEvents(Dictionary<int, List<AnimeOnceVoiceEventInfo>> table, List<AnimatorStateEvent_OnceVoice> list)
	{
		if (table.IsNullOrEmpty() || !table.TryGetValue(_nameHash, out var value) || value.IsNullOrEmpty())
		{
			return;
		}
		foreach (AnimeOnceVoiceEventInfo item in value)
		{
			list.Add(new AnimatorStateEvent_OnceVoice
			{
				NormalizedTime = item.NormalizedTime,
				IDs = item.EventIDs,
				Used = false
			});
		}
	}

	private void LoadStateLoopVoiceEvents(Dictionary<int, List<int>> table, List<int> list)
	{
		if (!table.IsNullOrEmpty() && table.TryGetValue(_nameHash, out var value) && !value.IsNullOrEmpty())
		{
			list.AddRange(value);
		}
	}

	public void BeginIgnoreEvent()
	{
		_ignoreAnimEvent = true;
	}

	public void EndIgnoreEvent()
	{
		_ignoreAnimEvent = false;
	}

	public void BeginIgnoreExpression()
	{
		_ignoreExpressionEvent = true;
	}

	public void EndIgnoreExpression()
	{
		_ignoreExpressionEvent = false;
	}

	public void BeginIgnoreVoice()
	{
		_ignoreVoiceEvent = true;
		StopLoopActionVoice();
	}

	public void EndIgnoreVoice()
	{
		_ignoreVoiceEvent = false;
		if (!PlayEventLoopVoice())
		{
			StopLoopActionVoice();
		}
	}

	public void UpdateAnimationEvent()
	{
		if (Animator == null || _ignoreAnimEvent)
		{
			return;
		}
		if (Animator.runtimeAnimatorController != null)
		{
			string text = Animator.runtimeAnimatorController.name;
			if (_animatorName != text)
			{
				_animatorName = text;
				LoadAnimatorYureInfo(text);
			}
		}
		AnimatorStateInfo currentAnimatorStateInfo = Animator.GetCurrentAnimatorStateInfo(0);
		if (_nameHash != currentAnimatorStateInfo.shortNameHash)
		{
			_nameHash = currentAnimatorStateInfo.shortNameHash;
			_prevNormalizedTime = 0f;
			_loopCount = 0;
			StateItemEvents.Clear();
			LoadStateEvents(ItemEventKeyTable, StateItemEvents);
			StatePartnerEvents.Clear();
			LoadStateEvents(PartnerEventKeyTable, StatePartnerEvents);
			StateSEEvents.Clear();
			StopAllActionLoopSE();
			LoadStateSEEvents(SEEventKeyTable, StateSEEvents);
			LoadFootStepEventKeyTable();
			LoadFootStepEvents();
			StateParticleEvents.Clear();
			LoadStateParticleEvents(ParticleEventKeyTable, StateParticleEvents);
			StateOnceVoiceEvents.Clear();
			LoadStateOnceVoiceEvents(OnceVoiceEventKeyTable, StateOnceVoiceEvents);
			if (!_ignoreExpressionEvent)
			{
				LoadStateExpression(_nameHash);
			}
			LoadYureInfo(_nameHash);
			StateLoopVoiceEvents.Clear();
			LoadStateLoopVoiceEvents(LoopVoiceEventKeyTable, StateLoopVoiceEvents);
			if (!PlayEventLoopVoice())
			{
				StopLoopActionVoice();
			}
			StateClothEvents.Clear();
			LoadStateEvents(ClothEventKeyTable, StateClothEvents);
			if (_motionIK != null && CanUseMapIK)
			{
				_motionIK.Calc(_nameHash);
			}
		}
		float num = currentAnimatorStateInfo.normalizedTime;
		bool loop = currentAnimatorStateInfo.loop;
		int num2 = (int)num;
		if (loop)
		{
			num = Mathf.Repeat(num, 1f);
		}
		bool isLoop = loop && (_loopCount < num2 || num < _prevNormalizedTime);
		UpdateItemEvent(num, isLoop);
		if (!_ignoreExpressionEvent)
		{
			UpdateExpressionEvent(num, isLoop);
		}
		UpdatePartnerEvent(num, isLoop);
		UpdateSEEvent(num, isLoop);
		UpdateFootStepEvent(currentAnimatorStateInfo, num, isLoop);
		UpdateParticleEvent(num, isLoop);
		if (!_ignoreVoiceEvent)
		{
			UpdateVoiceEvent(num, isLoop);
		}
		UpdateClothEvent(num, isLoop);
		_prevNormalizedTime = num;
		_loopCount = num2;
		if (_motionIK == null)
		{
			_motionIK = new MotionIK(Actor.ChaControl);
			_motionIK.MapIK = true;
			return;
		}
		if (Animator.runtimeAnimatorController != null && _preAnimatorName != Animator.runtimeAnimatorController.name)
		{
			_preAnimatorName = Animator.runtimeAnimatorController.name;
			_motionIK.SetMapIK(Animator.runtimeAnimatorController.name);
		}
		_motionIK.ChangeWeight(_nameHash, currentAnimatorStateInfo);
	}

	private void LoadAnimatorYureInfo(string animatorName)
	{
		if (Singleton<Manager.Resources>.Instance.Action.ActionYureTable.TryGetValue(animatorName, out var value))
		{
			YureTable = value;
			return;
		}
		ChaControl chaControl = Actor.ChaControl;
		ResetYure(chaControl);
	}

	private void LoadYureInfo(int stateHashName)
	{
		ChaControl chaControl = Actor.ChaControl;
		if (YureTable != null)
		{
			if (!YureTable.TryGetValue(stateHashName, out var value))
			{
				return;
			}
			bool[] aIsActive = value.aIsActive;
			for (int i = 0; i < aIsActive.Length; i++)
			{
				chaControl.playDynamicBoneBust(i, aIsActive[i]);
			}
			YureCtrl.BreastShapeInfo[] aBreastShape = value.aBreastShape;
			for (int j = 0; j < 2; j++)
			{
				YureCtrl.BreastShapeInfo breastShapeInfo = aBreastShape[j];
				YureCtrl.BreastShapeInfo breastShapeInfo2 = _shapeInfo[j];
				for (int k = 0; k < breastShapeInfo.breast.Length; k++)
				{
					bool flag = breastShapeInfo.breast[k];
					bool flag2 = breastShapeInfo2.breast[k];
					if (flag != flag2)
					{
						if (flag)
						{
							chaControl.DisableShapeBodyID((j != 0) ? 1 : 0, k, disable: false);
						}
						else
						{
							chaControl.DisableShapeBodyID((j != 0) ? 1 : 0, k, disable: true);
						}
					}
					_shapeInfo[j].breast[k] = flag;
				}
				if (breastShapeInfo.nip != breastShapeInfo2.nip)
				{
					if (breastShapeInfo.nip)
					{
						chaControl.DisableShapeBodyID((j != 0) ? 1 : 0, 7, disable: false);
					}
					else
					{
						chaControl.DisableShapeBodyID((j != 0) ? 1 : 0, 7, disable: false);
					}
					_shapeInfo[j].nip = breastShapeInfo.nip;
				}
			}
		}
		else
		{
			ResetYure(chaControl);
		}
	}

	private void ResetYure(ChaControl chara)
	{
		for (int i = 0; i < 4; i++)
		{
			chara.playDynamicBoneBust(i, play: true);
		}
		for (int j = 0; j < 2; j++)
		{
			YureCtrl.BreastShapeInfo breastShapeInfo = _shapeInfo[j];
			for (int k = 0; k < _defaultYure.Length; k++)
			{
				bool flag = _defaultYure[k];
				bool flag2 = breastShapeInfo.breast[k];
				if (flag != flag2)
				{
					if (flag)
					{
						chara.DisableShapeBodyID((j != 0) ? 1 : 0, k, disable: false);
					}
					else
					{
						chara.DisableShapeBodyID((j != 0) ? 1 : 0, k, disable: true);
					}
				}
				_shapeInfo[j].breast[k] = flag;
			}
			if (!breastShapeInfo.nip)
			{
				chara.DisableShapeBodyID((j != 0) ? 1 : 0, 7, disable: false);
				_shapeInfo[j].nip = true;
			}
		}
	}

	private void UpdateItemEvent(float normalizedTime, bool isLoop)
	{
		if (isLoop)
		{
			foreach (AnimatorStateEvent stateItemEvent in StateItemEvents)
			{
				stateItemEvent.Used = false;
			}
		}
		else
		{
			foreach (AnimatorStateEvent stateItemEvent2 in StateItemEvents)
			{
				if (stateItemEvent2.NormalizedTime > normalizedTime && stateItemEvent2.Used)
				{
					stateItemEvent2.Used = false;
				}
			}
		}
		foreach (AnimatorStateEvent stateItemEvent3 in StateItemEvents)
		{
			if (stateItemEvent3.NormalizedTime < normalizedTime && !stateItemEvent3.Used)
			{
				stateItemEvent3.Used = true;
				switch (stateItemEvent3.EventID)
				{
				case 0:
				{
					Renderer[] item4 = ItemRenderers.GetElement(0).Item2;
					SetEnableItemRenderers(item4, enable: true);
					break;
				}
				case 1:
				{
					Renderer[] item3 = ItemRenderers.GetElement(0).Item2;
					SetEnableItemRenderers(item3, enable: false);
					break;
				}
				case 2:
				{
					Renderer[] item2 = ItemRenderers.GetElement(1).Item2;
					SetEnableItemRenderers(item2, enable: true);
					break;
				}
				case 3:
				{
					Renderer[] item = ItemRenderers.GetElement(1).Item2;
					SetEnableItemRenderers(item, enable: false);
					break;
				}
				}
			}
		}
	}

	private void SetEnableItemRenderers(Renderer[] renderers, bool enable)
	{
		if (renderers.IsNullOrEmpty())
		{
			return;
		}
		foreach (Renderer renderer in renderers)
		{
			if (!(renderer == null) && renderer.enabled != enable)
			{
				renderer.enabled = enable;
			}
		}
	}

	private void UpdateClothEvent(float normalizedTime, bool isLoop)
	{
		AgentActor agentActor = Actor as AgentActor;
		if (agentActor == null)
		{
			return;
		}
		if (isLoop)
		{
			foreach (AnimatorStateEvent stateClothEvent in StateClothEvents)
			{
				stateClothEvent.Used = false;
			}
		}
		else
		{
			foreach (AnimatorStateEvent stateClothEvent2 in StateClothEvents)
			{
				if (stateClothEvent2.NormalizedTime > normalizedTime && stateClothEvent2.Used)
				{
					stateClothEvent2.Used = false;
				}
			}
		}
		foreach (AnimatorStateEvent stateClothEvent3 in StateClothEvents)
		{
			if (!(stateClothEvent3.NormalizedTime < normalizedTime) || stateClothEvent3.Used)
			{
				continue;
			}
			stateClothEvent3.Used = true;
			switch (stateClothEvent3.EventID)
			{
			case 0:
				agentActor.ChaControl.SetClothesState(0, 0);
				agentActor.ChaControl.SetClothesState(1, 0);
				agentActor.ChaControl.SetClothesState(2, 0);
				agentActor.ChaControl.SetClothesState(3, 0);
				break;
			case 1:
				agentActor.ChaControl.SetClothesState(0, 1);
				agentActor.ChaControl.SetClothesState(1, 1);
				agentActor.ChaControl.SetClothesState(2, 1);
				agentActor.ChaControl.SetClothesState(3, 1);
				break;
			case 2:
				agentActor.ChaControl.SetClothesState(0, 2);
				agentActor.ChaControl.SetClothesState(1, 2);
				agentActor.ChaControl.SetClothesState(2, 2);
				agentActor.ChaControl.SetClothesState(3, 2);
				break;
			case 3:
				agentActor.ChaControl.SetClothesState(0, 0);
				agentActor.ChaControl.SetClothesState(2, 0);
				break;
			case 4:
				agentActor.ChaControl.SetClothesState(0, 1);
				agentActor.ChaControl.SetClothesState(2, 1);
				break;
			case 5:
				agentActor.ChaControl.SetClothesState(0, 2);
				agentActor.ChaControl.SetClothesState(2, 2);
				break;
			case 6:
				agentActor.ChaControl.SetClothesState(1, 0);
				agentActor.ChaControl.SetClothesState(3, 0);
				agentActor.ChaControl.SetClothesState(5, 0);
				break;
			case 7:
				agentActor.ChaControl.SetClothesState(1, 1);
				agentActor.ChaControl.SetClothesState(3, 1);
				agentActor.ChaControl.SetClothesState(5, 1);
				break;
			case 8:
				agentActor.ChaControl.SetClothesState(1, 2);
				agentActor.ChaControl.SetClothesState(3, 2);
				agentActor.ChaControl.SetClothesState(5, 2);
				break;
			case -1:
				agentActor.ChaControl.ChangeNowCoordinate(reload: true);
				break;
			case 9:
			{
				string bathCoordinateFileName = agentActor.AgentData.BathCoordinateFileName;
				if (!bathCoordinateFileName.IsNullOrEmpty())
				{
					agentActor.ChaControl.ChangeNowCoordinate(agentActor.AgentData.BathCoordinateFileName, reload: true);
				}
				else
				{
					agentActor.ChaControl.ChangeNowCoordinate(Singleton<Manager.Resources>.Instance.BathDefaultCoord, reload: true);
				}
				break;
			}
			case 10:
			{
				string nowCoordinateFileName = agentActor.AgentData.NowCoordinateFileName;
				if (!nowCoordinateFileName.IsNullOrEmpty())
				{
					agentActor.ChaControl.ChangeNowCoordinate(nowCoordinateFileName, reload: true);
				}
				else
				{
					agentActor.ChaControl.ChangeNowCoordinate(reload: true);
				}
				break;
			}
			}
		}
	}

	protected virtual void UpdateExpressionEvent(float normalizedTime, bool isLoop)
	{
		if (isLoop)
		{
			foreach (ExpressionKeyframeEvent expressionKeyframe in ExpressionKeyframeList)
			{
				expressionKeyframe.Used = false;
			}
		}
		else
		{
			foreach (ExpressionKeyframeEvent expressionKeyframe2 in ExpressionKeyframeList)
			{
				if (expressionKeyframe2.NormalizedTime > normalizedTime && expressionKeyframe2.Used)
				{
					expressionKeyframe2.Used = false;
				}
			}
		}
		int iD = Actor.ID;
		foreach (ExpressionKeyframeEvent expressionKeyframe3 in ExpressionKeyframeList)
		{
			if (expressionKeyframe3.NormalizedTime < normalizedTime && !expressionKeyframe3.Used)
			{
				expressionKeyframe3.Used = true;
				Singleton<Game>.Instance.GetExpression(iD, expressionKeyframe3.Name)?.Change(Actor.ChaControl);
			}
		}
	}

	private void UpdatePartnerEvent(float normalizedTime, bool isLoop)
	{
		if (isLoop)
		{
			foreach (AnimatorStateEvent statePartnerEvent in StatePartnerEvents)
			{
				statePartnerEvent.Used = false;
			}
		}
		else
		{
			foreach (AnimatorStateEvent statePartnerEvent2 in StatePartnerEvents)
			{
				if (statePartnerEvent2.NormalizedTime < normalizedTime && statePartnerEvent2.Used)
				{
					statePartnerEvent2.Used = false;
				}
			}
		}
		foreach (AnimatorStateEvent statePartnerEvent3 in StatePartnerEvents)
		{
			if (statePartnerEvent3.NormalizedTime < normalizedTime && !statePartnerEvent3.Used)
			{
				statePartnerEvent3.Used = true;
				if (statePartnerEvent3.EventID == 0)
				{
				}
			}
		}
	}

	private void UpdateSEEvent(float normalizedTime, bool isLoop)
	{
		if (isLoop)
		{
			foreach (AnimatorStateEvent_SE stateSEEvent in StateSEEvents)
			{
				stateSEEvent.Used = false;
			}
		}
		else
		{
			foreach (AnimatorStateEvent_SE stateSEEvent2 in StateSEEvents)
			{
				if (normalizedTime < stateSEEvent2.NormalizedTime && stateSEEvent2.Used)
				{
					stateSEEvent2.Used = false;
				}
			}
		}
		foreach (AnimatorStateEvent_SE e in StateSEEvents)
		{
			if (!(e.NormalizedTime < normalizedTime) || e.Used)
			{
				continue;
			}
			e.Used = true;
			int clipID = e.ClipID;
			if (clipID == -1)
			{
				Actor.CurrentPoint?.GetComponent<DoorAnimation>()?.PlayMoveSE(open: true);
				continue;
			}
			int eventID = e.EventID;
			if (eventID < 0)
			{
				Observable.EveryLateUpdate().Take(1).Subscribe(delegate
				{
					if (!(e.Root == null) && Singleton<Map>.IsInstance() && Singleton<Manager.Resources>.IsInstance())
					{
						Transform transform = Singleton<Map>.Instance.Player?.CameraControl?.CameraComponent?.transform;
						if (!(transform == null))
						{
							SoundPack soundPack2 = Singleton<Manager.Resources>.Instance.SoundPack;
							if (soundPack2.TryGetActionSEData(e.ClipID, out var data2))
							{
								float num = data2.MaxDistance + soundPack2.Game3DInfo.MarginMaxDistance;
								if (!(num * num < (e.Root.position - transform.position).sqrMagnitude))
								{
									AudioSource audioSource = soundPack2.Play(data2, Manager.Sound.Type.GameSE3D);
									if (audioSource != null)
									{
										audioSource.Stop();
										audioSource.transform.position = e.Root.position;
										audioSource.transform.rotation = e.Root.rotation;
										audioSource.Play();
									}
								}
							}
						}
					}
				});
				continue;
			}
			int key = eventID / 2;
			int clipID2 = e.ClipID;
			Dictionary<int, UnityEx.ValueTuple<AudioSource, FadePlayer>> value3;
			UnityEx.ValueTuple<AudioSource, FadePlayer> value4;
			if (eventID % 2 == 0)
			{
				bool flag = false;
				flag = ActionLoopSETable.TryGetValue(key, out var value) && value != null;
				if (!flag)
				{
					value = (ActionLoopSETable[key] = new Dictionary<int, UnityEx.ValueTuple<AudioSource, FadePlayer>>());
				}
				if (flag)
				{
					UnityEx.ValueTuple<AudioSource, FadePlayer> value2 = default(UnityEx.ValueTuple<AudioSource, FadePlayer>);
					flag = value.TryGetValue(clipID2, out value2);
					if (flag)
					{
						flag = value2.Item1 != null && value2.Item1.isPlaying;
						if (!flag)
						{
							StopActionLoopSE(value2);
							value.Remove(clipID2);
						}
					}
				}
				if (flag)
				{
					continue;
				}
				if (e.Root == null || !Singleton<Map>.IsInstance() || !Singleton<Manager.Resources>.IsInstance())
				{
					break;
				}
				SoundPack soundPack = Singleton<Manager.Resources>.Instance.SoundPack;
				if (!soundPack.TryGetActionSEData(clipID2, out var data))
				{
					break;
				}
				AudioSource audio = soundPack.Play(data, Manager.Sound.Type.GameSE3D);
				if (audio != null)
				{
					FadePlayer component = audio.GetComponent<FadePlayer>();
					audio.Stop();
					Transform root = e.Root;
					audio.transform.position = root.position;
					audio.transform.rotation = root.rotation;
					audio.loop = true;
					audio.Play();
					value[clipID2] = new UnityEx.ValueTuple<AudioSource, FadePlayer>(audio, component);
					audio.LateUpdateAsObservable().TakeUntilDestroy(root).Subscribe(delegate
					{
						audio.transform.SetPositionAndRotation(root.position, root.rotation);
					});
				}
			}
			else if (ActionLoopSETable.TryGetValue(key, out value3) && value3.TryGetValue(clipID2, out value4))
			{
				StopActionLoopSE(value4);
				value3.Remove(clipID2);
			}
		}
	}

	private void UpdateParticleEvent(float normalizedTime, bool isLoop)
	{
		if (isLoop)
		{
			foreach (AnimatorStateEvent_Particle stateParticleEvent in StateParticleEvents)
			{
				stateParticleEvent.Used = false;
			}
		}
		else
		{
			foreach (AnimatorStateEvent_Particle stateParticleEvent2 in StateParticleEvents)
			{
				if (normalizedTime < stateParticleEvent2.NormalizedTime && stateParticleEvent2.Used)
				{
					stateParticleEvent2.Used = false;
				}
			}
		}
		foreach (AnimatorStateEvent_Particle stateParticleEvent3 in StateParticleEvents)
		{
			if (!(stateParticleEvent3.NormalizedTime <= normalizedTime) || stateParticleEvent3.Used)
			{
				continue;
			}
			stateParticleEvent3.Used = true;
			if (Particles.TryGetValue(stateParticleEvent3.EventID / 2, out var value) && value.TryGetValue(stateParticleEvent3.ParticleID, out var value2) && !(value2.Item2 == null))
			{
				ParticleSystem item = value2.Item2;
				bool flag = stateParticleEvent3.EventID % 2 == 0;
				if (item.isPlaying)
				{
					item.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
				}
				if (flag)
				{
					item.Play(withChildren: true);
				}
			}
		}
	}

	private void UpdateVoiceEvent(float normalizedTime, bool isLoop)
	{
		if (isLoop)
		{
			foreach (AnimatorStateEvent_OnceVoice stateOnceVoiceEvent in StateOnceVoiceEvents)
			{
				stateOnceVoiceEvent.Used = false;
			}
		}
		else
		{
			foreach (AnimatorStateEvent_OnceVoice stateOnceVoiceEvent2 in StateOnceVoiceEvents)
			{
				if (normalizedTime < stateOnceVoiceEvent2.NormalizedTime && stateOnceVoiceEvent2.Used)
				{
					stateOnceVoiceEvent2.Used = false;
				}
			}
		}
		foreach (AnimatorStateEvent_OnceVoice stateOnceVoiceEvent3 in StateOnceVoiceEvents)
		{
			if (stateOnceVoiceEvent3.NormalizedTime <= normalizedTime && !stateOnceVoiceEvent3.Used)
			{
				stateOnceVoiceEvent3.Used = true;
				if (!stateOnceVoiceEvent3.IDs.IsNullOrEmpty())
				{
					int element = stateOnceVoiceEvent3.IDs.GetElement(UnityEngine.Random.Range(0, stateOnceVoiceEvent3.IDs.Length));
					PlayEventOnceVoice(element);
				}
			}
		}
	}

	public void LoadEventVarious(int eventID, int poseID, PlayState info)
	{
		LoadEventKeyTable(eventID, poseID);
		Actor.LoadEventItems(info);
		Actor.LoadEventParticles(eventID, poseID);
	}

	public void LoadAnimatorIfNotEquals(PlayState info)
	{
		bool flag = false;
		PlayState.Info[] stateInfos = info.MainStateInfo.InStateInfo.StateInfos;
		for (int i = 0; i < stateInfos.Length; i++)
		{
			PlayState.Info info2 = stateInfos[i];
			int stateID = Animator.StringToHash(info2.stateName);
			if (!Animator.HasState(info2.layer, stateID))
			{
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			return;
		}
		AssetBundleInfo assetBundleInfo = info.MainStateInfo.AssetBundleInfo;
		if (assetBundleInfo.asset != Animator.runtimeAnimatorController.name)
		{
			RuntimeAnimatorController runtimeAnimatorController = AssetUtility.LoadAsset<RuntimeAnimatorController>(assetBundleInfo.assetbundle, assetBundleInfo.asset, string.Empty);
			if (runtimeAnimatorController != null)
			{
				SetAnimatorController(runtimeAnimatorController);
			}
		}
	}

	public ActorAnimInfo SetAnimInfo(PlayState info)
	{
		AnimInfo = new ActorAnimInfo
		{
			inEnableBlend = info.MainStateInfo.InStateInfo.EnableFade,
			inBlendSec = info.MainStateInfo.InStateInfo.FadeSecond,
			inFadeOutTime = info.MainStateInfo.FadeOutTime,
			outEnableBlend = info.MainStateInfo.OutStateInfo.EnableFade,
			outBlendSec = info.MainStateInfo.OutStateInfo.FadeSecond,
			directionType = info.DirectionType,
			endEnableBlend = info.EndEnableBlend,
			endBlendSec = info.EndBlendRate,
			isLoop = info.MainStateInfo.IsLoop,
			loopMinTime = info.MainStateInfo.LoopMin,
			loopMaxTime = info.MainStateInfo.LoopMax,
			hasAction = info.ActionInfo.hasAction,
			loopStateName = info.MainStateInfo.InStateInfo.StateInfos.GetElement(info.MainStateInfo.InStateInfo.StateInfos.Length - 1).stateName,
			randomCount = info.ActionInfo.randomCount,
			oldNormalizedTime = 0f,
			layer = info.MainStateInfo.InStateInfo.StateInfos[0].layer
		};
		return AnimInfo;
	}

	public ActorAnimInfo LoadActionState(int eventID, int poseID, PlayState info)
	{
		LoadEventVarious(eventID, poseID, info);
		InitializeStates(info);
		LoadAnimatorIfNotEquals(info);
		return SetAnimInfo(info);
	}

	public void EnableItems()
	{
		foreach (UnityEx.ValueTuple<int, GameObject> item in Items)
		{
			if (item.Item2 != null)
			{
				item.Item2.SetActive(value: true);
			}
		}
	}

	public void DisableItems()
	{
		foreach (UnityEx.ValueTuple<int, GameObject> item in Items)
		{
			if (item.Item2 != null)
			{
				item.Item2.SetActive(value: false);
			}
		}
	}

	public void ClearItems()
	{
		ItemRenderers.Clear();
		ItemAnimatorTable.Clear();
		foreach (UnityEx.ValueTuple<int, GameObject> item in Items)
		{
			if (item.Item2 != null)
			{
				UnityEngine.Object.Destroy(item.Item2);
			}
		}
		Items.Clear();
	}

	public void EnableParticleRenderer()
	{
		foreach (KeyValuePair<int, Dictionary<int, Tuple<GameObject, ParticleSystem, ParticleSystemRenderer>>> particle in Particles)
		{
			if (particle.Value == null)
			{
				continue;
			}
			foreach (KeyValuePair<int, Tuple<GameObject, ParticleSystem, ParticleSystemRenderer>> item3 in particle.Value)
			{
				ParticleSystem item = item3.Value.Item2;
				if (item != null && item.isPlaying)
				{
					item.Pause(withChildren: true);
				}
				ParticleSystemRenderer item2 = item3.Value.Item3;
				if (item2 != null && !item2.enabled)
				{
					item2.enabled = true;
				}
			}
		}
	}

	public void DisableParticleRenderer()
	{
		foreach (KeyValuePair<int, Dictionary<int, Tuple<GameObject, ParticleSystem, ParticleSystemRenderer>>> particle in Particles)
		{
			if (particle.Value == null)
			{
				continue;
			}
			foreach (KeyValuePair<int, Tuple<GameObject, ParticleSystem, ParticleSystemRenderer>> item3 in particle.Value)
			{
				ParticleSystem item = item3.Value.Item2;
				if (item != null && item.isPaused)
				{
					item.Play(withChildren: true);
				}
				ParticleSystemRenderer item2 = item3.Value.Item3;
				if (item2 != null && item2.enabled)
				{
					item2.enabled = false;
				}
			}
		}
	}

	public void ClearParticles()
	{
		if (Particles.IsNullOrEmpty())
		{
			return;
		}
		foreach (KeyValuePair<int, Dictionary<int, Tuple<GameObject, ParticleSystem, ParticleSystemRenderer>>> particle in Particles)
		{
			if (particle.Value.IsNullOrEmpty())
			{
				continue;
			}
			foreach (KeyValuePair<int, Tuple<GameObject, ParticleSystem, ParticleSystemRenderer>> item2 in particle.Value)
			{
				Tuple<GameObject, ParticleSystem, ParticleSystemRenderer> value = item2.Value;
				if (value != null)
				{
					GameObject item = value.Item1;
					if (!(item == null))
					{
						UnityEngine.Object.Destroy(item);
					}
				}
			}
		}
		Particles.Clear();
	}

	public virtual void StopLoopActionVoice()
	{
		if (!(_loopActionVoice.Item2 == null) && !(_loopActionVoice.Item2.gameObject == null))
		{
			if (_loopActionVoice.Item3 != null)
			{
				Singleton<Voice>.Instance.Stop(_loopActionVoice.Item4, _loopActionVoice.Item3);
			}
			else
			{
				UnityEngine.Object.Destroy(_loopActionVoice.Item2.gameObject);
			}
			_loopActionVoice.Item1 = -1;
			_loopActionVoice.Item2 = null;
			_loopActionVoice.Item3 = null;
			_loopActionVoice.Item4 = -1;
		}
	}

	private void StopActionLoopSE(UnityEx.ValueTuple<AudioSource, FadePlayer> pair)
	{
		if (pair.Item2 != null && pair.Item2.gameObject != null)
		{
			if (Singleton<Manager.Resources>.IsInstance())
			{
				pair.Item2.Stop(Singleton<Manager.Resources>.Instance.SoundPack.Game3DInfo.StopFadeTime);
			}
			else
			{
				pair.Item2.Stop(0.5f);
			}
		}
		else if (pair.Item1 != null && pair.Item1.gameObject != null)
		{
			if (Singleton<Manager.Sound>.IsInstance())
			{
				Singleton<Manager.Sound>.Instance.Stop(Manager.Sound.Type.GameSE3D, pair.Item2.transform);
			}
			else
			{
				UnityEngine.Object.Destroy(pair.Item1.gameObject);
			}
		}
	}

	private void StopAllActionLoopSE()
	{
		if (ActionLoopSETable.IsNullOrEmpty())
		{
			return;
		}
		foreach (KeyValuePair<int, Dictionary<int, UnityEx.ValueTuple<AudioSource, FadePlayer>>> item in ActionLoopSETable)
		{
			if (item.Value.IsNullOrEmpty())
			{
				continue;
			}
			foreach (KeyValuePair<int, UnityEx.ValueTuple<AudioSource, FadePlayer>> item2 in item.Value)
			{
				StopActionLoopSE(item2.Value);
			}
		}
		ActionLoopSETable.Clear();
	}

	private void LoadFootStepEvents()
	{
		_prevFootStepInfo = null;
		_footStepEvents.Clear();
		if (_footStepEventKeyTable != null && _footStepEventKeyTable.TryGetValue(_nameHash, out var value) && !value.IsNullOrEmpty())
		{
			FootStepInfo[] array = value;
			foreach (FootStepInfo info in array)
			{
				_footStepEvents.Add(new FootStepInfo(info));
			}
		}
	}

	private void UpdateFootStepEvent(AnimatorStateInfo stateInfo, float normalizedTime, bool isLoop)
	{
		if (!_isFootStepActive || _footStepEvents.IsNullOrEmpty())
		{
			return;
		}
		string forwardMove = Singleton<Manager.Resources>.Instance.DefinePack.AnimatorParameter.ForwardMove;
		float @float = GetFloat(forwardMove);
		FootStepInfo footStepInfo = null;
		foreach (FootStepInfo footStepEvent in _footStepEvents)
		{
			if (footStepEvent.Threshold.IsRange(@float))
			{
				footStepInfo = footStepEvent;
				break;
			}
		}
		if (footStepInfo == null || footStepInfo.Keys.IsNullOrEmpty())
		{
			return;
		}
		if (footStepInfo != _prevFootStepInfo)
		{
			for (int i = 0; i < footStepInfo.Keys.Length; i++)
			{
				footStepInfo.Keys[i].Execute = footStepInfo.Keys[i].Time < normalizedTime;
			}
			_prevFootStepInfo = footStepInfo;
		}
		else if (isLoop)
		{
			for (int j = 0; j < footStepInfo.Keys.Length; j++)
			{
				footStepInfo.Keys[j].Execute = false;
			}
		}
		else
		{
			for (int k = 0; k < footStepInfo.Keys.Length; k++)
			{
				FootStepInfo.Key key = footStepInfo.Keys[k];
				if (normalizedTime < key.Time && key.Execute)
				{
					footStepInfo.Keys[k].Execute = false;
				}
			}
		}
		bool flag = false;
		for (int l = 0; l < footStepInfo.Keys.Length; l++)
		{
			FootStepInfo.Key key2 = footStepInfo.Keys[l];
			if (key2.Time <= normalizedTime && !key2.Execute)
			{
				footStepInfo.Keys[l].Execute = true;
				flag = true;
			}
		}
		if (!flag)
		{
			return;
		}
		_footStepAudioSources.RemoveAll((AudioSource x) => x == null || x.gameObject == null || !x.isPlaying);
		Vector3 position = Actor.Position;
		Quaternion rotation = Actor.Rotation;
		Map instance = Singleton<Map>.Instance;
		ActorCameraControl cameraControl = instance.Player.CameraControl;
		float num = Vector3.Distance(position, cameraControl.transform.position);
		SoundPack.FootStepInfoGroup footStepInfo2 = Singleton<Manager.Resources>.Instance.SoundPack.FootStepInfo;
		if (!(num <= footStepInfo2.PlayEnableDistance))
		{
			return;
		}
		AudioSource audioSource = null;
		bool isBareFoot = Actor.ChaControl.IsBareFoot;
		byte sex = Actor.ChaControl.sex;
		Vector3 origin = position + Vector3.up * 15f;
		LayerMask sELayer = Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.SELayer;
		Ray ray = new Ray(origin, Vector3.down);
		int num2 = Physics.RaycastNonAlloc(ray, _raycastHits, 50f, sELayer);
		MapArea mapArea = null;
		Weather weather = instance.Simulator.Weather;
		SoundPack.PlayAreaType areaType = SoundPack.PlayAreaType.Normal;
		if (0 < num2)
		{
			float num3 = float.MaxValue;
			string self = null;
			for (int m = 0; m < num2; m++)
			{
				RaycastHit raycastHit = _raycastHits[m];
				GameObject gameObject = raycastHit.collider?.gameObject;
				if (!(gameObject == null))
				{
					float distance = raycastHit.distance;
					if (distance < num3)
					{
						self = gameObject.tag;
						num3 = distance;
					}
				}
			}
			if (!self.IsNullOrEmpty())
			{
				audioSource = Singleton<Manager.Resources>.Instance.SoundPack.PlayFootStep(sex, isBareFoot, self, weather, areaType);
			}
		}
		else if ((mapArea = Actor.MapArea) != null)
		{
			audioSource = Singleton<Manager.Resources>.Instance.SoundPack.PlayFootStep(sex, isBareFoot, instance.MapID, mapArea.AreaID, weather, areaType);
		}
		if (audioSource != null)
		{
			audioSource.Stop();
			audioSource.transform.SetPositionAndRotation(position, rotation);
			FadePlayer component = audioSource.GetComponent<FadePlayer>();
			if (component != null)
			{
				component.currentVolume = audioSource.volume;
			}
			audioSource.rolloffMode = footStepInfo2.RolloffMode;
			audioSource.minDistance = footStepInfo2.MinDistance;
			audioSource.maxDistance = footStepInfo2.MaxDistance;
			_footStepAudioSources.Add(audioSource);
			audioSource.Play();
		}
	}

	public void InitializeStates(PlayState info)
	{
		if (info == null)
		{
			InStates.Clear();
			OutStates.Clear();
			ActionStates.Clear();
			return;
		}
		InStates.Clear();
		if (!info.MainStateInfo.InStateInfo.StateInfos.IsNullOrEmpty())
		{
			PlayState.Info[] stateInfos = info.MainStateInfo.InStateInfo.StateInfos;
			foreach (PlayState.Info item in stateInfos)
			{
				InStates.Enqueue(item);
			}
		}
		OutStates.Clear();
		if (!info.MainStateInfo.OutStateInfo.StateInfos.IsNullOrEmpty())
		{
			PlayState.Info[] stateInfos2 = info.MainStateInfo.OutStateInfo.StateInfos;
			foreach (PlayState.Info item2 in stateInfos2)
			{
				OutStates.Enqueue(item2);
			}
		}
		ActionStates.Clear();
		if (!info.SubStateInfos.IsNullOrEmpty())
		{
			foreach (PlayState.PlayStateInfo subStateInfo in info.SubStateInfos)
			{
				ActionStates.Add(subStateInfo);
			}
		}
		AnimABInfo = info.MainStateInfo.AssetBundleInfo;
		_maskState = info.MaskStateInfo;
	}

	public void InitializeStates(PlayState.Info[] inStateInfos, PlayState.Info[] outStateInfos, AssetBundleInfo animABInfo)
	{
		InStates.Clear();
		if (!inStateInfos.IsNullOrEmpty())
		{
			foreach (PlayState.Info item in inStateInfos)
			{
				InStates.Enqueue(item);
			}
		}
		OutStates.Clear();
		if (!outStateInfos.IsNullOrEmpty())
		{
			foreach (PlayState.Info item2 in outStateInfos)
			{
				OutStates.Enqueue(item2);
			}
		}
		AnimABInfo = animABInfo;
		ActionStates.Clear();
	}

	public void EndStates()
	{
		ActorAnimInfo animInfo = AnimInfo;
		if (!animInfo.endEnableBlend)
		{
			CrossFadeScreen();
		}
		if (Animator.runtimeAnimatorController.name != _defaultAnimController.name)
		{
			ResetDefaultAnimatorController();
		}
		Actor.SetStand(RecoveryPoint, animInfo.endEnableBlend, animInfo.endBlendSec, animInfo.directionType);
		RecoveryPoint = null;
		RefsActAnimInfo = true;
	}

	public void StopAllAnimCoroutine()
	{
		StopInLocoAnimCoroutine();
		StopInAnimCoroutine();
		StopOutAnimCoroutine();
		StopActionAnimCoroutine();
		StopOnceActionAnimCoroutine();
		StopTurnAnimCoroutine();
	}

	public void PlayInLocoAnimation(bool enableFade, float fadeTime, int layer)
	{
		IEnumerator enumerator = (_inLocoAnimEnumerator = StartInLocoAnimation(enableFade, fadeTime, layer));
		_inLocoAnimDisposable = Observable.FromCoroutine(() => enumerator).Subscribe();
	}

	public void StopInLocoAnimCoroutine()
	{
		if (_inLocoAnimDisposable != null)
		{
			_inLocoAnimDisposable.Dispose();
			_inLocoAnimEnumerator = null;
		}
	}

	private IEnumerator StartInLocoAnimation(bool enableFade, float fadeTime, int layer)
	{
		Animator animator = Animator;
		if (!enableFade)
		{
			CrossFadeScreen();
		}
		for (int i = 1; i < animator.layerCount; i++)
		{
			animator.SetLayerWeight(i, 0f);
		}
		if (_maskState.layer > 0)
		{
			animator.SetLayerWeight(_maskState.layer, 1f);
			if (enableFade)
			{
				CrossFadeAnimation(_maskState.stateName, fadeTime, _maskState.layer, 0f);
			}
			else
			{
				PlayAnimation(_maskState.stateName, _maskState.layer, 0f);
			}
		}
		Queue<PlayState.Info> queue = InStates;
		while (queue.Count > 0)
		{
			PlayState.Info state = queue.Dequeue();
			if (enableFade)
			{
				CrossFadeAnimation(state.stateName, fadeTime, layer, 0f);
				CrossFadeItemAnimation(state.stateName, fadeTime, layer);
				CrossFadeHousingActionPointAnimation(state.stateName, fadeTime, layer);
				IConnectableObservable<long> waiter = Observable.Timer(TimeSpan.FromSeconds(fadeTime)).Publish();
				waiter.Connect();
				yield return waiter.ToYieldInstruction();
			}
			else
			{
				PlayAnimation(state.stateName, layer, 0f);
				PlayItemAnimation(state.stateName);
				PlayHousingActionPointAnimation(state.stateName);
				yield return null;
			}
			if (queue.Count == 0)
			{
				break;
			}
			CurrentStateName = state.stateName;
			yield return null;
			AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(state.layer);
			bool isInTransition = animator.IsInTransition(state.layer);
			while (isInTransition || (stateInfo.IsName(state.stateName) && stateInfo.normalizedTime < 1f))
			{
				stateInfo = animator.GetCurrentAnimatorStateInfo(state.layer);
				isInTransition = animator.IsInTransition(state.layer);
				yield return null;
			}
			CurrentStateName = string.Empty;
			yield return null;
		}
		CurrentStateName = string.Empty;
		_inLocoAnimEnumerator = null;
		yield return null;
	}

	public void PlayInAnimation(bool enableFade, float fadeTime, float fadeOutTime, int layer)
	{
		IEnumerator enumerator = (_inAnimEnumerator = StartInAnimation(enableFade, fadeTime, fadeOutTime, layer));
		_inAnimDisposable = Observable.FromCoroutine(() => enumerator).Subscribe();
	}

	public void StopInAnimCoroutine()
	{
		if (_inAnimDisposable != null)
		{
			_inAnimDisposable.Dispose();
			_inAnimEnumerator = null;
		}
	}

	private IEnumerator StartInAnimation(bool enableFade, float fadeTime, float fadeOutTime, int layer)
	{
		if (!enableFade)
		{
			CrossFadeScreen();
		}
		Queue<PlayState.Info> queue = InStates;
		Animator animator = Animator;
		while (queue.Count > 0)
		{
			PlayState.Info state = queue.Dequeue();
			yield return PlayState(animator, state, enableFade, fadeTime, layer, fadeOutTime);
		}
		yield return null;
		_inAnimEnumerator = null;
	}

	public void PlayOutAnimation(bool enableFade, float fadeTime, int layer)
	{
		IEnumerator enumerator = (_outAnimEnumerator = StartOutAnimation(enableFade, fadeTime, layer));
		_outAnimDisposable = Observable.FromCoroutine(() => enumerator).Subscribe();
	}

	public void StopOutAnimCoroutine()
	{
		if (_outAnimDisposable != null)
		{
			_outAnimDisposable.Dispose();
			_outAnimEnumerator = null;
		}
	}

	private IEnumerator StartOutAnimation(bool enableFade, float fadeTime, int layer)
	{
		Queue<PlayState.Info> queue = OutStates;
		Animator animator = Animator;
		while (queue.Count > 0)
		{
			PlayState.Info state = queue.Dequeue();
			yield return PlayState(animator, state, enableFade, fadeTime, layer, 1f);
		}
		yield return null;
		_outAnimEnumerator = null;
	}

	public void PlayActionAnimation(int layer)
	{
		IEnumerator enumerator = (_actionAnimEnumerator = StartActionAnimation(layer));
		_actionAnimDisposable = Observable.FromCoroutine(() => enumerator).Subscribe();
	}

	private IEnumerator StartActionAnimation(int layer)
	{
		PlayState.PlayStateInfo stateInfo = ActionStates[UnityEngine.Random.Range(0, ActionStates.Count)];
		Animator animator = Animator;
		AnimatorStateInfo prevState = animator.GetCurrentAnimatorStateInfo(layer);
		ActionInStates.Clear();
		if (!stateInfo.InStateInfo.StateInfos.IsNullOrEmpty())
		{
			PlayState.Info[] stateInfos = stateInfo.InStateInfo.StateInfos;
			foreach (PlayState.Info item in stateInfos)
			{
				ActionInStates.Enqueue(item);
			}
		}
		ActionOutStates.Clear();
		if (!stateInfo.OutStateInfo.StateInfos.IsNullOrEmpty())
		{
			PlayState.Info[] stateInfos2 = stateInfo.OutStateInfo.StateInfos;
			foreach (PlayState.Info item2 in stateInfos2)
			{
				ActionOutStates.Enqueue(item2);
			}
		}
		yield return StartActionInAnimation(stateInfo.InStateInfo.EnableFade, stateInfo.InStateInfo.FadeSecond, layer);
		float elapsedTime = 0f;
		int loopDuration = UnityEngine.Random.Range(stateInfo.LoopMin, stateInfo.LoopMax);
		while (true)
		{
			float num;
			elapsedTime = (num = elapsedTime + Time.deltaTime);
			if (!(num < (float)loopDuration))
			{
				break;
			}
			yield return null;
		}
		yield return StartActionOutAnimation(stateInfo.OutStateInfo.EnableFade, stateInfo.OutStateInfo.FadeSecond, layer);
		if (stateInfo.OutStateInfo.EnableFade)
		{
			CrossFadeAnimation(prevState.shortNameHash, stateInfo.OutStateInfo.FadeSecond, layer, 0f);
		}
		else
		{
			PlayAnimation(prevState.shortNameHash, layer, 0f);
		}
		yield return null;
		_actionAnimEnumerator = null;
	}

	private IEnumerator StartActionInAnimation(bool enableFade, float fadeTime, int layer)
	{
		Queue<PlayState.Info> queue = ActionInStates;
		Animator animator = Animator;
		while (queue.Count > 0)
		{
			PlayState.Info state = queue.Dequeue();
			yield return PlayState(animator, state, enableFade, fadeTime, layer, 1f);
		}
		yield return null;
	}

	private IEnumerator StartActionOutAnimation(bool enableFade, float fadeTime, int layer)
	{
		Queue<PlayState.Info> queue = ActionOutStates;
		Animator animator = Animator;
		while (queue.Count > 0)
		{
			PlayState.Info state = queue.Dequeue();
			yield return PlayState(animator, state, enableFade, fadeTime, layer, 1f);
		}
		yield return null;
	}

	public void StopActionAnimCoroutine()
	{
		if (_actionAnimDisposable != null)
		{
			_actionAnimDisposable.Dispose();
			_actionAnimEnumerator = null;
		}
	}

	public void PlayOnceActionAnimation(bool enableFade, float fadeTime, int layer)
	{
		IEnumerator enumerator = (_onceActionAnimEnumerator = StartOnceActionAnimation(enableFade, fadeTime, layer));
		_onceActionAnimDisposable = Observable.FromCoroutine(() => enumerator).Subscribe();
	}

	private IEnumerator StartOnceActionAnimation(bool enableFade, float fadeTime, int layer)
	{
		PlayState.Info state = OnceActionStates[UnityEngine.Random.Range(0, OnceActionStates.Count)];
		Animator animator = Animator;
		AnimatorStateInfo prevState = animator.GetCurrentAnimatorStateInfo(state.layer);
		if (enableFade)
		{
			CrossFadeAnimation(state.stateName, fadeTime, layer, 0f);
			IConnectableObservable<long> waiter = Observable.Timer(TimeSpan.FromSeconds(fadeTime)).Publish();
			waiter.Connect();
			yield return waiter.ToYieldInstruction();
		}
		else
		{
			CrossFadeScreen();
			PlayAnimation(state.stateName, state.layer, 0f);
			yield return null;
		}
		bool isInTransition = animator.IsInTransition(layer);
		AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
		while (isInTransition || (stateInfo.IsName(state.stateName) && stateInfo.normalizedTime < 1f))
		{
			isInTransition = animator.IsInTransition(layer);
			stateInfo = animator.GetCurrentAnimatorStateInfo(state.layer);
			yield return null;
		}
		animator.Play(prevState.shortNameHash, state.layer, 0f);
		yield return null;
		_onceActionAnimEnumerator = null;
	}

	public void StopOnceActionAnimCoroutine()
	{
		if (_onceActionAnimDisposable != null)
		{
			_onceActionAnimDisposable.Dispose();
			_onceActionAnimEnumerator = null;
		}
	}

	public void PlayTurnAnimation(Vector3 to, float fadeOutTime, PlayState.AnimStateInfo recoverStateInfo, bool isFast = false)
	{
		IEnumerator enumerator = (_turnAnimEnumerator = StartTurnAnimation(to, fadeOutTime, recoverStateInfo));
		_turnAnimDisposable = Observable.FromCoroutine(() => enumerator).Subscribe();
	}

	private IEnumerator StartTurnAnimation(Vector3 to, float fadeOutTime, PlayState.AnimStateInfo recoverStateInfo, bool isFast = false)
	{
		yield return Observable.EveryLateUpdate().Skip(1).Take(1)
			.ToYieldInstruction();
		float currentAngle = 0f - GetAngleFromForward(Actor.Controller.transform.forward);
		Vector3 from = Actor.Position;
		from.y = 0f;
		to.y = 0f;
		float toAngle = GetAngleFromForward(Vector3.Normalize(to - from));
		float angle = toAngle - currentAngle;
		Animator animator = Animator;
		string direction = Singleton<Manager.Resources>.Instance.DefinePack.AnimatorParameter.DirectionParameterName;
		float minAngle = Singleton<Manager.Resources>.Instance.AgentProfile.TurnMinAngle;
		Quaternion destRotation = Quaternion.LookRotation(to - from);
		if (Mathf.Abs(angle) > minAngle)
		{
			float inverse2 = Mathf.InverseLerp(-180f, 180f, angle);
			inverse2 = Mathf.Lerp(-1f, 1f, inverse2);
			float invAngle = Mathf.Clamp(inverse2, -1f, 1f);
			animator.SetFloat(direction, invAngle);
			DefinePack definePack = Singleton<Manager.Resources>.Instance.DefinePack;
			PlayState.AnimStateInfo turn = definePack.AnimatorState.TurnStateInfo;
			if (isFast)
			{
				animator.speed = 2f;
			}
			animator.CrossFadeInFixedTime(turn.StateInfos[0].stateName, turn.FadeSecond, turn.StateInfos[0].layer, 0f);
			string stateName = turn.StateInfos[0].stateName;
			IObservable<long> timer = Observable.Timer(TimeSpan.FromSeconds(0.10000000149011612));
			yield return timer.ToYieldInstruction();
			bool isInTransition2 = animator.IsInTransition(0);
			AnimatorStateInfo stateInfo3 = animator.GetCurrentAnimatorStateInfo(0);
			bool isTurn = stateInfo3.IsName(stateName);
			float normalizedTime = stateInfo3.normalizedTime;
			while (isInTransition2 || (isTurn && normalizedTime < fadeOutTime))
			{
				isInTransition2 = animator.IsInTransition(0);
				stateInfo3 = animator.GetCurrentAnimatorStateInfo(0);
				isTurn = stateInfo3.IsName(stateName);
				normalizedTime = stateInfo3.normalizedTime;
				yield return null;
			}
		}
		Quaternion startRotation = Actor.Rotation;
		ObservableEasing.Linear(0.3f).FrameTimeInterval().Subscribe(delegate(TimeInterval<float> x)
		{
			Quaternion rotation = Quaternion.Lerp(startRotation, destRotation, x.Value);
			Actor.Rotation = rotation;
		});
		animator.speed = 1f;
		if (recoverStateInfo != null && !recoverStateInfo.StateInfos.IsNullOrEmpty())
		{
			_recoveryStateFromTurn.Clear();
			PlayState.Info[] stateInfos = recoverStateInfo.StateInfos;
			foreach (PlayState.Info item in stateInfos)
			{
				_recoveryStateFromTurn.Enqueue(item);
			}
			float blendTime = recoverStateInfo.FadeSecond;
			while (_recoveryStateFromTurn.Count > 0)
			{
				PlayState.Info state = _recoveryStateFromTurn.Dequeue();
				if (recoverStateInfo.EnableFade)
				{
					CrossFadeAnimation(state.stateName, blendTime, state.layer, 0f);
					IConnectableObservable<long> waiter = Observable.Timer(TimeSpan.FromSeconds(blendTime)).Publish();
					waiter.Connect();
					yield return waiter.ToYieldInstruction();
				}
				else
				{
					PlayAnimation(state.stateName, state.layer, 0f);
					yield return null;
				}
				if (_recoveryStateFromTurn.Count == 0)
				{
					break;
				}
				yield return null;
				AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(state.layer);
				bool isInTransition = animator.IsInTransition(state.layer);
				while (isInTransition || (stateInfo.IsName(state.stateName) && stateInfo.normalizedTime < 1f))
				{
					stateInfo = animator.GetCurrentAnimatorStateInfo(state.layer);
					isInTransition = animator.IsInTransition(state.layer);
					yield return null;
				}
			}
		}
		animator.SetFloat(direction, 0f);
		_turnAnimEnumerator = null;
	}

	public void PlayTurnAnimation(float angle, float fadeOutTime, PlayState.AnimStateInfo recoverStateInfo)
	{
		_turnAnimEnumerator = StartTurnAnimation(angle, fadeOutTime, recoverStateInfo);
	}

	private IEnumerator StartTurnAnimation(float angle, float fadeOutTime, PlayState.AnimStateInfo recoverStateInfo)
	{
		float inverse2 = Mathf.InverseLerp(-180f, 180f, angle);
		inverse2 = Mathf.Lerp(-1f, 1f, angle);
		float invAngle = Mathf.Clamp(inverse2, -1f, 1f);
		string direction = Singleton<Manager.Resources>.Instance.DefinePack.AnimatorParameter.DirectionParameterName;
		Animator animator = Animator;
		animator.SetFloat(direction, invAngle);
		PlayState.AnimStateInfo turn = Singleton<Manager.Resources>.Instance.DefinePack.AnimatorState.TurnStateInfo;
		animator.CrossFadeInFixedTime(turn.StateInfos[0].stateName, turn.FadeSecond, turn.StateInfos[0].layer, 0f);
		string stateName = turn.StateInfos[0].stateName;
		IObservable<long> timer = Observable.Timer(TimeSpan.FromSeconds(turn.FadeSecond));
		yield return timer.ToYieldInstruction();
		bool isInTransition2 = animator.IsInTransition(0);
		AnimatorStateInfo stateInfo3 = animator.GetCurrentAnimatorStateInfo(0);
		bool isTurn = stateInfo3.IsName(stateName);
		float normalizedTime = stateInfo3.normalizedTime;
		while (isInTransition2 || (isTurn && normalizedTime < fadeOutTime))
		{
			isInTransition2 = animator.IsInTransition(0);
			stateInfo3 = animator.GetCurrentAnimatorStateInfo(0);
			isTurn = stateInfo3.IsName(stateName);
			normalizedTime = stateInfo3.normalizedTime;
			yield return null;
		}
		if (!recoverStateInfo.StateInfos.IsNullOrEmpty())
		{
			Queue<PlayState.Info> queue = QueuePool<AIProject.PlayState.Info>.Get();
			PlayState.Info[] stateInfos = recoverStateInfo.StateInfos;
			foreach (PlayState.Info item in stateInfos)
			{
				queue.Enqueue(item);
			}
			float blendTime = recoverStateInfo.FadeSecond;
			while (queue.Count > 0)
			{
				PlayState.Info state = queue.Dequeue();
				if (recoverStateInfo.EnableFade)
				{
					CrossFadeAnimation(state.stateName, blendTime, state.layer, 0f);
					IConnectableObservable<long> waiter = Observable.Timer(TimeSpan.FromSeconds(blendTime)).Publish();
					waiter.Connect();
					yield return waiter.ToYieldInstruction();
				}
				else
				{
					PlayAnimation(state.stateName, state.layer, 0f);
					yield return null;
				}
				if (queue.Count == 0)
				{
					break;
				}
				yield return null;
				stateInfo3 = animator.GetCurrentAnimatorStateInfo(state.layer);
				isInTransition2 = animator.IsInTransition(state.layer);
				while (isInTransition2 || (stateInfo3.IsName(state.stateName) && stateInfo3.normalizedTime < 1f))
				{
					stateInfo3 = animator.GetCurrentAnimatorStateInfo(state.layer);
					isInTransition2 = animator.IsInTransition(state.layer);
					yield return null;
				}
			}
			QueuePool<AIProject.PlayState.Info>.Release(queue);
		}
		animator.SetFloat(direction, 0f);
		_turnAnimEnumerator = null;
	}

	public void StopTurnAnimCoroutine()
	{
		if (_turnAnimDisposable != null)
		{
			_turnAnimDisposable.Dispose();
			_turnAnimEnumerator = null;
		}
	}

	public virtual void PlayAnimation(string stateName, int layer, float normalizedTime)
	{
		Animator animator = Animator;
		DefinePack definePack = Singleton<Manager.Resources>.Instance.DefinePack;
		string heightParameterName = definePack.AnimatorParameter.HeightParameterName;
		if (!_parameters.IsNullOrEmpty())
		{
			AnimatorControllerParameter[] parameters = _parameters;
			foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
			{
				if (animatorControllerParameter.name == heightParameterName && animatorControllerParameter.type == AnimatorControllerParameterType.Float)
				{
					float shapeBodyValue = Actor.ChaControl.GetShapeBodyValue(0);
					if (animator != null)
					{
						animator.SetFloat(heightParameterName, shapeBodyValue);
					}
				}
			}
		}
		PlayAnimation(animator, stateName, layer, normalizedTime);
	}

	public virtual void PlayAnimation(int stateNameHash, int layer, float normalizedTime)
	{
		Animator animator = Animator;
		DefinePack definePack = Singleton<Manager.Resources>.Instance.DefinePack;
		string heightParameterName = definePack.AnimatorParameter.HeightParameterName;
		AnimatorControllerParameter[] parameters = animator.parameters;
		foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
		{
			if (animatorControllerParameter.name == heightParameterName && animatorControllerParameter.type == AnimatorControllerParameterType.Float)
			{
				float shapeBodyValue = Actor.ChaControl.GetShapeBodyValue(0);
				if (animator != null)
				{
					animator.SetFloat(heightParameterName, shapeBodyValue);
				}
			}
		}
		if (animator != null)
		{
			animator.Play(stateNameHash, layer, normalizedTime);
		}
	}

	public void PlayAnimation(Animator animator, string stateName, int layer, float normalizedTime)
	{
		if (base.gameObject != null && Debug.isDebugBuild)
		{
			Debug.Log($"{base.gameObject.name} Play to {stateName}");
		}
		if (animator != null)
		{
			animator.Play(stateName, layer, normalizedTime);
		}
	}

	public virtual void CrossFadeAnimation(string stateName, float fadeTime, int layer, float fixedTimeOffset)
	{
		Animator animator = Animator;
		DefinePack definePack = Singleton<Manager.Resources>.Instance.DefinePack;
		string heightParameterName = definePack.AnimatorParameter.HeightParameterName;
		AnimatorControllerParameter[] parameters = _parameters;
		foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
		{
			if (animatorControllerParameter.name == heightParameterName && animatorControllerParameter.type == AnimatorControllerParameterType.Float)
			{
				float shapeBodyValue = Actor.ChaControl.GetShapeBodyValue(0);
				if (animator != null)
				{
					animator.SetFloat(heightParameterName, shapeBodyValue);
				}
			}
		}
		CrossFadeAnimation(animator, stateName, fadeTime, layer, fixedTimeOffset);
	}

	public virtual void CrossFadeAnimation(int stateNameHash, float fadeTime, int layer, float fixedTimeOffset)
	{
		Animator animator = Animator;
		DefinePack definePack = Singleton<Manager.Resources>.Instance.DefinePack;
		string heightParameterName = definePack.AnimatorParameter.HeightParameterName;
		AnimatorControllerParameter[] parameters = _parameters;
		foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
		{
			if (animatorControllerParameter.name == heightParameterName && animatorControllerParameter.type == AnimatorControllerParameterType.Float)
			{
				float shapeBodyValue = Actor.ChaControl.GetShapeBodyValue(0);
				if (animator != null)
				{
					animator.SetFloat(heightParameterName, shapeBodyValue);
				}
			}
		}
		if (animator != null)
		{
			animator.CrossFadeInFixedTime(stateNameHash, fadeTime, layer, fixedTimeOffset, 0f);
		}
	}

	public void CrossFadeAnimation(Animator animator, string stateName, float fadeTime, int layer, float fixedTimeOffset)
	{
		if (Debug.isDebugBuild)
		{
			Debug.Log($"{base.gameObject.name}: CrossFade to {stateName}");
		}
		if (animator != null)
		{
			animator.CrossFadeInFixedTime(stateName, fadeTime, layer, fixedTimeOffset, 0f);
		}
	}

	private IEnumerator PlayState(Animator animator, PlayState.Info state, bool enableFade, float fadeTime, int layer, float fadeOutTime)
	{
		string heightName = Singleton<Manager.Resources>.Instance.DefinePack.AnimatorParameter.HeightParameterName;
		if (enableFade)
		{
			CrossFadeAnimation(state.stateName, fadeTime, layer, 0f);
			CrossFadeItemAnimation(state.stateName, fadeTime, layer);
			CrossFadeHousingActionPointAnimation(state.stateName, fadeTime, layer);
			IConnectableObservable<long> waiter = Observable.Timer(TimeSpan.FromSeconds(fadeTime)).Publish();
			waiter.Connect();
			yield return waiter.ToYieldInstruction();
		}
		else
		{
			PlayAnimation(state.stateName, layer, 0f);
			PlayItemAnimation(state.stateName);
			PlayHousingActionPointAnimation(state.stateName);
			yield return null;
		}
		CurrentStateName = state.stateName;
		LoadMatchTargetInfo(state.stateName);
		yield return null;
		AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(state.layer);
		bool isInTransition = animator.IsInTransition(state.layer);
		while (isInTransition || (stateInfo.IsName(state.stateName) && stateInfo.normalizedTime < fadeOutTime))
		{
			if (animator == null)
			{
				yield break;
			}
			stateInfo = animator.GetCurrentAnimatorStateInfo(state.layer);
			isInTransition = animator.IsInTransition(state.layer);
			yield return null;
		}
		CurrentStateName = string.Empty;
		Targets.Clear();
		yield return null;
	}

	protected virtual void CrossFadeItemAnimation(string stateName, float fadeTime, int layer)
	{
		string heightParameterName = Singleton<Manager.Resources>.Instance.DefinePack.AnimatorParameter.HeightParameterName;
		foreach (KeyValuePair<int, ItemAnimInfo> item in ItemAnimatorTable)
		{
			Animator animator = item.Value.Animator;
			if (!item.Value.Sync)
			{
				continue;
			}
			AnimatorControllerParameter[] parameters = item.Value.Parameters;
			foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
			{
				if (animatorControllerParameter.name == heightParameterName && animatorControllerParameter.type == AnimatorControllerParameterType.Float)
				{
					float shapeBodyValue = Actor.ChaControl.GetShapeBodyValue(0);
					animator.SetFloat(heightParameterName, shapeBodyValue);
				}
			}
			CrossFadeAnimation(animator, stateName, fadeTime, layer, 0f);
		}
	}

	private void CrossFadeHousingActionPointAnimation(string stateName, float fadeTime, int layer)
	{
		if (Actor.CurrentPoint == null)
		{
			return;
		}
		HousingActionPointAnimation[] componentsInChildren = Actor.CurrentPoint.GetComponentsInChildren<HousingActionPointAnimation>();
		if (componentsInChildren.IsNullOrEmpty())
		{
			return;
		}
		HousingActionPointAnimation[] array = componentsInChildren;
		foreach (HousingActionPointAnimation housingActionPointAnimation in array)
		{
			if (!(housingActionPointAnimation.Animator == null))
			{
				CrossFadeAnimation(housingActionPointAnimation.Animator, stateName, fadeTime, layer, 0f);
			}
		}
	}

	protected virtual void PlayItemAnimation(string stateName)
	{
		string heightParameterName = Singleton<Manager.Resources>.Instance.DefinePack.AnimatorParameter.HeightParameterName;
		foreach (KeyValuePair<int, ItemAnimInfo> item in ItemAnimatorTable)
		{
			Animator animator = item.Value.Animator;
			if (!item.Value.Sync)
			{
				continue;
			}
			AnimatorControllerParameter[] parameters = item.Value.Parameters;
			foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
			{
				if (animatorControllerParameter.name == heightParameterName && animatorControllerParameter.type == AnimatorControllerParameterType.Float)
				{
					float shapeBodyValue = Actor.ChaControl.GetShapeBodyValue(0);
					animator.SetFloat(heightParameterName, shapeBodyValue);
				}
			}
			PlayAnimation(animator, stateName, 0, 0f);
		}
	}

	private void PlayHousingActionPointAnimation(string stateName)
	{
		if (Actor.CurrentPoint == null)
		{
			return;
		}
		HousingActionPointAnimation[] componentsInChildren = Actor.CurrentPoint.GetComponentsInChildren<HousingActionPointAnimation>();
		if (componentsInChildren.IsNullOrEmpty())
		{
			return;
		}
		HousingActionPointAnimation[] array = componentsInChildren;
		foreach (HousingActionPointAnimation housingActionPointAnimation in array)
		{
			if (!(housingActionPointAnimation.Animator == null))
			{
				PlayAnimation(housingActionPointAnimation.Animator, stateName, 0, 0f);
			}
		}
	}

	protected virtual void LoadMatchTargetInfo(string stateName)
	{
	}

	protected virtual void LoadStateLocomotionVoice(int stateHashName)
	{
	}

	protected virtual void LoadStateExpression(int stateHashName)
	{
		Manager.Resources.ActionTable action = Singleton<Manager.Resources>.Instance.Action;
		ExpressionKeyframeList.Clear();
		int iD = Actor.ID;
		if (Singleton<Manager.Resources>.Instance.Action.ActionExpressionKeyframeTable.TryGetValue(iD, out var value) && value.TryGetValue(stateHashName, out var value2))
		{
			foreach (ExpressionKeyframe item in value2)
			{
				ExpressionKeyframeList.Add(new ExpressionKeyframeEvent
				{
					NormalizedTime = item.normalizedTime,
					Name = item.expressionName
				});
			}
			return;
		}
		if (Singleton<Manager.Resources>.Instance.Action.ActionExpressionTable.TryGetValue(iD, out var value3) && value3.TryGetValue(stateHashName, out var value4))
		{
			Singleton<Game>.Instance.GetExpression(iD, value4)?.Change(Actor.ChaControl);
		}
	}

	public abstract void LoadEventKeyTable(int eventID, int poseID);

	public abstract void LoadSEEventKeyTable(int eventID, int poseID);

	public virtual void LoadAnimalEventKeyTable(int animalTypeID, int poseID)
	{
	}

	protected abstract void LoadFootStepEventKeyTable();

	public abstract void LoadParticleEventKeyTable(int eventID, int poseID);

	public virtual void LoadOnceVoiceEventKeyTable(int eventID, int poseID)
	{
	}

	public virtual void LoadLoopVoiceEventKeyTable(int eventID, int poseID)
	{
	}

	protected virtual void PlayEventOnceVoice(int voiceID)
	{
	}

	protected virtual bool PlayEventLoopVoice()
	{
		if (_ignoreVoiceEvent || _ignoreAnimEvent)
		{
			return false;
		}
		if (OnceActionVoice != null && OnceActionVoice.isPlaying)
		{
			return false;
		}
		if (StateLoopVoiceEvents.IsNullOrEmpty())
		{
			return false;
		}
		return true;
	}

	public void CrossFadeScreen(float time = -1f)
	{
		if (Actor.ChaControl.IsVisibleInCamera)
		{
			ActorCameraControl cameraControl = Singleton<Map>.Instance.Player.CameraControl;
			float num = Vector3.Distance(Actor.Position, cameraControl.transform.position);
			float crossFadeEnableDistance = Singleton<Manager.Resources>.Instance.LocomotionProfile.CrossFadeEnableDistance;
			if (num <= crossFadeEnableDistance)
			{
				cameraControl.CrossFade.FadeStart(time);
			}
		}
	}
}
