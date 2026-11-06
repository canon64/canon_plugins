using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using AIChara;
using AIProject.Animal;
using AIProject.Definitions;
using AIProject.SaveData;
using AIProject.Scene;
using ConfigScene;
using Illusion.Extensions;
using IllusionUtility.GetUtility;
using Manager;
using ReMotion;
using Sirenix.OdinInspector;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.AI;
using UnityEx;

namespace AIProject;

[DisallowMultipleComponent]
public abstract class Actor : SerializedMonoBehaviour, IVisible
{
	public struct BehaviorSchedule
	{
		public bool enabled;

		public string name;

		public float duration;

		public float elapsedTime;

		public bool useGameTime;

		public bool progress;

		public BehaviorSchedule(bool enabled_, string name_, float duration_)
		{
			enabled = enabled_;
			name = name_;
			duration = duration_;
			elapsedTime = 0f;
			useGameTime = false;
			progress = true;
		}
	}

	public struct InputInfo
	{
		public Vector3 move;

		public Vector3 lookPos;

		public void Init()
		{
			move = Vector3.zero;
			lookPos = Vector3.zero;
		}
	}

	public enum FovBodyPart
	{
		Body,
		Head,
		Foot
	}

	public enum BodyPart
	{
		Body,
		Bust,
		Head,
		LeftFoot,
		RightFoot
	}

	public class SearchInfo
	{
		public bool IsSuccess { get; set; }

		public List<ItemSearchInfo> ItemList { get; set; } = new List<ItemSearchInfo>();

	}

	public struct ItemSearchInfo
	{
		public string name;

		public int categoryID;

		public int id;

		public int count;
	}

	public class ItemScaleInfo
	{
		public GameObject TargetItem { get; set; }

		public int ScaleMode { get; set; }

		public float SThreshold { get; set; }

		public float MThreshold { get; set; }

		public float LThreshold { get; set; }

		public float Evaluate(float t)
		{
			t = Mathf.Clamp01(t);
			if (t < 0.5f)
			{
				float t2 = Mathf.InverseLerp(0f, 0.5f, t);
				return Mathf.Lerp(SThreshold, MThreshold, t2);
			}
			float t3 = Mathf.InverseLerp(0.5f, 1f, t);
			return Mathf.Lerp(MThreshold, LThreshold, t3);
		}
	}

	public int ID;

	[SerializeField]
	protected Rigidbody _rigidbody;

	[SerializeField]
	protected NavMeshAgent _navMeshAgent;

	[SerializeField]
	protected NavMeshObstacle _navMeshObstacle;

	[SerializeField]
	private float _accelerationTime = 0.2f;

	[SerializeField]
	private float _maxVerticalVelocityOnGround = 3f;

	[SerializeField]
	private float _slopeStartAngle = 50f;

	[SerializeField]
	private float _slopeEndAngle = 85f;

	private ReadOnlyDictionary<FovBodyPart, Transform> _fovBodyPartReadOnlyTable;

	[SerializeField]
	protected Dictionary<FovBodyPart, Transform> _fovTargetPointTable = new Dictionary<FovBodyPart, Transform>();

	private Transform[] _fovTargets;

	private ReadOnlyDictionary<BodyPart, Transform> _chaBodyPartsReadonlyDic;

	[SerializeField]
	protected Dictionary<BodyPart, Transform> _chaBodyParts = new Dictionary<BodyPart, Transform>();

	protected Transform[] _chaFovTargets;

	private LocomotionProfile _locomotionProfile;

	[SerializeField]
	[HideInEditorMode]
	[DisableInPlayMode]
	private MapArea _mapArea;

	protected IntReactiveProperty _mapAreaID;

	private ObservableStateMachineTrigger _observableStateMachine;

	private int? _instanceID;

	protected UnityEx.ValueTuple<int, int> _scheduleThreshold = default(UnityEx.ValueTuple<int, int>);

	protected BehaviorSchedule _schedule = default(BehaviorSchedule);

	protected UnityEx.ValueTuple<bool, TimeSpan> _gestureTimeLimit = new UnityEx.ValueTuple<bool, TimeSpan>(i1: false, TimeSpan.MaxValue);

	protected int _originAvoidancePriority = -1;

	private IEnumerator _talkEnumerator;

	private IDisposable _talkDisposable;

	private IEnumerator _withAnimalEnumerator;

	private IDisposable _withAnimalDisposable;

	private IDisposable _standDisposable;

	protected List<ItemScaleInfo> _scaleCtrlInfos = new List<ItemScaleInfo>();

	public EventType EventKey { get; set; }

	public ChaControl ChaControl { get; set; }

	public bool IsVisible { get; set; } = true;


	public int ActionID { get; set; }

	public int PoseID { get; set; }

	public abstract string CharaName { get; }

	public ItemCache EquipedItem { get; protected set; }

	public bool IsKinematic
	{
		get
		{
			return _rigidbody.isKinematic;
		}
		set
		{
			_rigidbody.isKinematic = value;
		}
	}

	public Rigidbody Rigidbody
	{
		[CompilerGenerated]
		get
		{
			return _rigidbody;
		}
	}

	public NavMeshAgent NavMeshAgent
	{
		[CompilerGenerated]
		get
		{
			return _navMeshAgent;
		}
	}

	public NavMeshObstacle NavMeshObstacle
	{
		[CompilerGenerated]
		get
		{
			return _navMeshObstacle;
		}
	}

	public float AccelerationTime
	{
		[CompilerGenerated]
		get
		{
			return _accelerationTime;
		}
	}

	public float MaxVerticalVelocityOnGround
	{
		[CompilerGenerated]
		get
		{
			return _maxVerticalVelocityOnGround;
		}
	}

	public InputInfo StateInfo { get; set; }

	public ReadOnlyDictionary<FovBodyPart, Transform> FovTargetPointTable => _fovBodyPartReadOnlyTable ?? (_fovBodyPartReadOnlyTable = new ReadOnlyDictionary<FovBodyPart, Transform>(_fovTargetPointTable));

	public Transform[] FovTargetPoints
	{
		[CompilerGenerated]
		get
		{
			return _fovTargets ?? (_fovTargets = _fovTargetPointTable.Values.ToArray());
		}
	}

	public ReadOnlyDictionary<BodyPart, Transform> ChaBodyParts => _chaBodyPartsReadonlyDic ?? (_chaBodyPartsReadonlyDic = new ReadOnlyDictionary<BodyPart, Transform>(_chaBodyParts));

	public Transform[] ChaFovTargets
	{
		get
		{
			if (_chaFovTargets.IsNullOrEmpty())
			{
				_chaFovTargets = _chaBodyParts.Values.ToArray();
			}
			return _chaFovTargets;
		}
	}

	public LocomotionProfile LocomotionProfile
	{
		get
		{
			if (_locomotionProfile == null)
			{
				_locomotionProfile = Singleton<Manager.Resources>.Instance.LocomotionProfile;
			}
			return _locomotionProfile;
		}
	}

	public bool Visible { get; set; } = true;


	public Vector3 Normal { get; set; }

	public float ForwardMLP { get; set; }

	public abstract ActorController Controller { get; }

	public abstract ICharacterInfo TiedInfo { get; }

	public abstract ActorAnimation Animation { get; }

	public int MapID { get; set; }

	public int ChunkID { get; set; }

	public int AreaID { get; set; }

	public MapArea MapArea
	{
		get
		{
			return _mapArea;
		}
		set
		{
			_mapArea = value;
		}
	}

	public MapArea.AreaType AreaType { get; set; }

	public virtual bool IsNeutralCommand => true;

	public virtual Desire.ActionType Mode { get; set; }

	public bool IsInit { get; private set; }

	public abstract ActorLocomotion Locomotor { get; }

	public Actor Partner { get; set; }

	public Vector3 Position
	{
		get
		{
			if (Locomotor != null)
			{
				return Locomotor.transform.position;
			}
			return Vector3.zero;
		}
		set
		{
			Locomotor.transform.position = value;
		}
	}

	public Vector3 CommandCenter => Locomotor.transform.position;

	public Vector3 Forward
	{
		[CompilerGenerated]
		get
		{
			return Locomotor.transform.forward;
		}
	}

	public Vector3 Back
	{
		[CompilerGenerated]
		get
		{
			return Locomotor.transform.rotation * Vector3.back;
		}
	}

	public Quaternion Rotation
	{
		get
		{
			return Locomotor.transform.rotation;
		}
		set
		{
			Locomotor.transform.rotation = value;
		}
	}

	public PoseType PoseType { get; set; }

	public bool IsOnGround { get; set; }

	public ActionPoint CurrentPoint { get; set; }

	public ActionPoint NextPoint { get; set; }

	public EquipState EquipState { get; private set; } = new EquipState();


	public ObservableStateMachineTrigger ObservableStateMachine
	{
		[CompilerGenerated]
		get
		{
			return _observableStateMachine ?? (_observableStateMachine = Animation.Animator.GetBehaviour<ObservableStateMachineTrigger>());
		}
	}

	public int InstanceID
	{
		get
		{
			if (!_instanceID.HasValue)
			{
				_instanceID = GetInstanceID();
			}
			return _instanceID.Value;
		}
	}

	public bool IsSlave { get; set; }

	public BehaviorSchedule Schedule
	{
		get
		{
			return _schedule;
		}
		set
		{
			_schedule = value;
		}
	}

	public bool EnabledSchedule
	{
		get
		{
			return _schedule.enabled;
		}
		set
		{
			_schedule.enabled = value;
		}
	}

	public bool LivesTalkSequence => _talkEnumerator != null;

	public bool LivesWithAnimalSequence
	{
		[CompilerGenerated]
		get
		{
			return _withAnimalEnumerator != null;
		}
	}

	public List<ItemScaleInfo> ScaleCtrlInfo
	{
		[CompilerGenerated]
		get
		{
			return _scaleCtrlInfos;
		}
	}

	public IObservable<int> OnMapAreaChangedAsObservable()
	{
		return _mapAreaID ?? (_mapAreaID = new IntReactiveProperty(-1));
	}

	public void Relocate()
	{
		Vector3 origin = Position + Vector3.up * 10f;
		RaycastHit[] array = Physics.RaycastAll(origin, Vector3.down, 100f, Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.AreaDetectionLayer);
		RaycastHit raycastHit = default(RaycastHit);
		RaycastHit[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			RaycastHit raycastHit2 = array2[i];
			if (raycastHit2.collider.gameObject != Controller.gameObject)
			{
				raycastHit = raycastHit2;
				break;
			}
		}
		Position = raycastHit.point;
	}

	public void SetCurrentSchedule(bool scheduled, string name_, int minMin, int maxMin, bool gestures, bool useGameTime)
	{
		_scheduleThreshold = new UnityEx.ValueTuple<int, int>(minMin, maxMin);
		int num = ((minMin == maxMin) ? minMin : UnityEngine.Random.Range(minMin, maxMin + 1));
		_schedule = new BehaviorSchedule(scheduled, name_, num);
		_schedule.useGameTime = useGameTime;
		TimeSpan i = TimeSpan.FromMinutes(num / 2);
		_gestureTimeLimit = new UnityEx.ValueTuple<bool, TimeSpan>(gestures, i);
	}

	public virtual void EnableEntity()
	{
	}

	public virtual void DisableEntity()
	{
	}

	public void ActivateNavMeshAgent()
	{
		if (_navMeshObstacle.enabled)
		{
			_navMeshObstacle.enabled = false;
		}
		if (!_navMeshAgent.enabled)
		{
			_navMeshAgent.enabled = true;
		}
	}

	public void DeactivateNavMeshAgent()
	{
		if (_navMeshAgent.enabled)
		{
			_navMeshAgent.enabled = false;
		}
		if (!_navMeshObstacle.enabled)
		{
			_navMeshObstacle.enabled = true;
		}
	}

	public void StopNavMeshAgent()
	{
		if (NavMeshAgent.isActiveAndEnabled && NavMeshAgent.isOnNavMesh)
		{
			NavMeshAgent.velocity = Vector3.zero;
			if (!NavMeshAgent.isStopped)
			{
				NavMeshAgent.isStopped = true;
			}
		}
	}

	public void ChangeStaticNavMeshAgentAvoidance()
	{
		if (Singleton<Manager.Resources>.IsInstance())
		{
			_navMeshAgent.avoidancePriority = Singleton<Manager.Resources>.Instance.AgentProfile.AvoidancePriorityStationary;
		}
	}

	public void ChangeDynamicNavMeshAgentAvoidance()
	{
		if (Singleton<Manager.Resources>.IsInstance())
		{
			_navMeshAgent.avoidancePriority = Singleton<Manager.Resources>.Instance.AgentProfile.AvoidancePriorityDefault;
		}
	}

	public void RecoverNavMeshAgentAvoidance()
	{
		if (_originAvoidancePriority >= 0)
		{
			_navMeshAgent.avoidancePriority = _originAvoidancePriority;
			_originAvoidancePriority = -1;
		}
	}

	public void StartTalkSequence(Actor listener)
	{
		StartTalkSequence(listener, 0, 0);
	}

	public void StartTalkSequence(Actor listener, int speakID, int listenID)
	{
		if (listener is AgentActor)
		{
			IEnumerator enumerator2 = (_talkEnumerator = StartTalkSequenceCoroutine(listener, speakID, listenID));
			if (_talkDisposable != null)
			{
				_talkDisposable.Dispose();
			}
			_talkDisposable = Observable.FromCoroutine(() => enumerator2).Subscribe();
			AgentActor agentActor = listener as AgentActor;
			agentActor.CommandPartner = this;
			agentActor.BehaviorResources.ChangeMode(Desire.ActionType.Idle);
		}
		else if (listener is MerchantActor)
		{
			IEnumerator enumerator = (_talkEnumerator = StartTalkSequenceCoroutine(listener, speakID, listenID));
			if (_talkDisposable != null)
			{
				_talkDisposable.Dispose();
			}
			_talkDisposable = Observable.FromCoroutine(() => enumerator).Subscribe();
			MerchantActor merchantActor = listener as MerchantActor;
			merchantActor.CommandPartner = this;
			merchantActor.ChangeBehavior(Merchant.ActionType.TalkWithAgent);
		}
	}

	private IEnumerator StartTalkSequenceCoroutine(Actor listener, int speakID, int listenID)
	{
		PlayState.AnimStateInfo idleState = Singleton<Manager.Resources>.Instance.DefinePack.AnimatorState.IdleStateInfo;
		Animation.PlayTurnAnimation(listener.Position, 1f, idleState);
		listener.Animation.PlayTurnAnimation(Position, 1f, idleState);
		while (Animation.PlayingTurnAnimation || listener.Animation.PlayingTurnAnimation)
		{
			yield return null;
		}
		Manager.Resources.AnimationTables animData = Singleton<Manager.Resources>.Instance.Animation;
		PoseKeyPair speakerPose = animData.TalkSpeakerStateTable[speakID];
		PlayState speakerInfo = animData.AgentActionAnimTable[speakerPose.postureID][speakerPose.poseID];
		bool sInEnableFade = speakerInfo.MainStateInfo.InStateInfo.EnableFade;
		float sInFadeSecond = speakerInfo.MainStateInfo.InStateInfo.FadeSecond;
		int sLayer = speakerInfo.Layer;
		Animation.LoadEventKeyTable(speakerPose.postureID, speakerPose.poseID);
		PlayState listenerInfo2 = null;
		if (listener is AgentActor)
		{
			PoseKeyPair poseKeyPair = animData.TalkListenerStateTable[listenID];
			listenerInfo2 = animData.AgentActionAnimTable[poseKeyPair.postureID][poseKeyPair.poseID];
			listener.Animation.LoadEventKeyTable(poseKeyPair.postureID, poseKeyPair.poseID);
		}
		else
		{
			PoseKeyPair poseKeyPair2 = animData.MerchantListenerStateTable[listenID];
			listenerInfo2 = animData.MerchantCommonActionAnimStateTable[poseKeyPair2.postureID][poseKeyPair2.poseID];
			listener.Animation.LoadEventKeyTable(poseKeyPair2.postureID, poseKeyPair2.poseID);
		}
		bool lInEnableFade = listenerInfo2.MainStateInfo.InStateInfo.EnableFade;
		float lInFadeSecond = listenerInfo2.MainStateInfo.InStateInfo.FadeSecond;
		int lLayer = listenerInfo2.Layer;
		Animation.InitializeStates(speakerInfo.MainStateInfo.InStateInfo.StateInfos, speakerInfo.MainStateInfo.OutStateInfo.StateInfos, speakerInfo.MainStateInfo.AssetBundleInfo);
		listener.Animation.InitializeStates(listenerInfo2.MainStateInfo.InStateInfo.StateInfos, listenerInfo2.MainStateInfo.OutStateInfo.StateInfos, listenerInfo2.MainStateInfo.AssetBundleInfo);
		Animation.PlayInAnimation(sInEnableFade, sInFadeSecond, 1f, speakerInfo.Layer);
		listener.Animation.PlayInAnimation(lInEnableFade, lInFadeSecond, 1f, listenerInfo2.Layer);
		while (Animation.PlayingInAnimation || listener.Animation.PlayingTurnAnimation || listener.Animation.PlayingInAnimation)
		{
			yield return null;
		}
		SetCurrentSchedule(scheduled: true, "会話", speakerInfo.MainStateInfo.LoopMin, speakerInfo.MainStateInfo.LoopMax, speakerInfo.ActionInfo.hasAction, useGameTime: false);
		while (_schedule.enabled)
		{
			yield return null;
		}
		bool sOutEnableFade = speakerInfo.MainStateInfo.OutStateInfo.EnableFade;
		float sOutFadeSecond = speakerInfo.MainStateInfo.OutStateInfo.FadeSecond;
		Animation.PlayOutAnimation(sOutEnableFade, sOutFadeSecond, sLayer);
		bool lOutEnableFade = listenerInfo2.MainStateInfo.OutStateInfo.EnableFade;
		float lOutFadeSecond = listenerInfo2.MainStateInfo.OutStateInfo.FadeSecond;
		listener.Animation.PlayOutAnimation(lOutEnableFade, lOutFadeSecond, lLayer);
		while (Animation.PlayingOutAnimation || listener.Animation.PlayingOutAnimation)
		{
			yield return null;
		}
		_talkEnumerator = null;
	}

	public void StopTalkSequence()
	{
		if (_talkDisposable != null)
		{
			_talkDisposable.Dispose();
			_talkDisposable = null;
		}
		if (_talkEnumerator != null)
		{
			_talkEnumerator = null;
		}
	}

	public void StartWithAnimalSequence(AnimalBase animal)
	{
		StartWithAnimalSequence(animal, 0, 0);
	}

	public void StartWithAnimalSequence(AnimalBase animal, int poseID)
	{
		StartWithAnimalSequence(animal, poseID, poseID);
	}

	public void StartWithAnimalSequence(AnimalBase animal, int actorPoseID, int animalPoseID)
	{
		if (!(animal == null) && (this is AgentActor || this is PlayerActor))
		{
			_withAnimalEnumerator = StartWithAnimalSequenceCoroutine(animal, actorPoseID, animalPoseID);
			animal.SetImpossible(_value: true, this);
			animal.SetState((!(this is AgentActor)) ? AnimalState.WithPlayer : AnimalState.WithAgent);
			if (_withAnimalDisposable != null)
			{
				_withAnimalDisposable.Dispose();
			}
			_withAnimalDisposable = Observable.FromCoroutine(() => _withAnimalEnumerator).Subscribe(delegate
			{
			}, delegate(Exception ex)
			{
				Debug.LogException(ex);
				_withAnimalEnumerator = null;
			}, delegate
			{
				_withAnimalEnumerator = null;
			}).AddTo(this);
		}
	}

	private IEnumerator StartWithAnimalSequenceCoroutine(AnimalBase animal, int actorPoseID, int animalPoseID)
	{
		yield return null;
		if (!IsMatchAnimalWithActor(animal))
		{
			_withAnimalEnumerator = null;
			yield break;
		}
		Renderer bodyRenderer = ChaControl.objBody.GetComponentInChildren<Renderer>();
		Renderer headRenderer = ChaControl.objHead.GetComponentInChildren<Renderer>();
		bool isVisible = bodyRenderer != null && bodyRenderer.isVisible;
		if ((isVisible | (headRenderer != null && headRenderer.isVisible)) && Singleton<Manager.Map>.IsInstance() && Singleton<Manager.Resources>.IsInstance())
		{
			LocomotionProfile locomotionProfile = Singleton<Manager.Resources>.Instance.LocomotionProfile;
			ActorCameraControl cameraControl = Singleton<Manager.Map>.Instance.Player.CameraControl;
			float num = Vector3.Distance(Position, cameraControl.transform.position);
			float crossFadeEnableDistance = locomotionProfile.CrossFadeEnableDistance;
			if (num <= crossFadeEnableDistance)
			{
				cameraControl.CrossFade.FadeStart();
			}
		}
		Vector3 eulerAngle = Quaternion.LookRotation(animal.Position - Position).eulerAngles;
		eulerAngle.x = (eulerAngle.z = 0f);
		Rotation = Quaternion.Euler(eulerAngle);
		animal.SetWithActorPoint(this, animalPoseID);
		PlayState playState = null;
		PlayState.PlayStateInfo mainStateInfo = null;
		int layer = 0;
		int animalTypeID = animal.AnimalTypeID;
		if (Singleton<Manager.Resources>.Instance.Animation.WithAnimalStateTable.ContainsKey(animalTypeID) && Singleton<Manager.Resources>.Instance.Animation.WithAnimalStateTable[animalTypeID].TryGetValue(actorPoseID, out playState))
		{
			layer = playState.Layer;
			mainStateInfo = playState.MainStateInfo;
			bool enableFade = mainStateInfo.InStateInfo.EnableFade;
			float fadeSecond = mainStateInfo.InStateInfo.FadeSecond;
			Animation.InitializeStates(mainStateInfo.InStateInfo.StateInfos, mainStateInfo.OutStateInfo.StateInfos, mainStateInfo.AssetBundleInfo);
			Animation.LoadAnimalEventKeyTable(animal.AnimalTypeID, actorPoseID);
			LoadEventItems(playState);
			Animation.PlayInAnimation(enableFade, fadeSecond, 1f, layer);
		}
		animal.StartInAnimationWithActor(animalPoseID);
		while (Animation.PlayingInAnimation || (animal != null && animal.PlayingInAnimation))
		{
			yield return null;
			if (!IsMatchAnimalWithActor(animal))
			{
				ClearItems();
				_withAnimalEnumerator = null;
				yield break;
			}
		}
		if (!IsMatchAnimalWithActor(animal))
		{
			ClearItems();
			_withAnimalEnumerator = null;
			yield break;
		}
		if (mainStateInfo != null && mainStateInfo.IsLoop)
		{
			SetCurrentSchedule(scheduled: true, "動物行動", mainStateInfo.LoopMin, mainStateInfo.LoopMax, playState.ActionInfo.hasAction, useGameTime: false);
			while (_schedule.enabled)
			{
				yield return null;
				if (!IsMatchAnimalWithActor(animal))
				{
					ClearItems();
					_withAnimalEnumerator = null;
					yield break;
				}
			}
		}
		if (mainStateInfo != null)
		{
			bool enableFade2 = mainStateInfo.OutStateInfo.EnableFade;
			float fadeSecond2 = mainStateInfo.OutStateInfo.FadeSecond;
			Animation.PlayOutAnimation(enableFade2, fadeSecond2, layer);
		}
		if (IsMatchAnimalWithActor(animal))
		{
			animal.StartOutAnimationWithActor();
		}
		else
		{
			animal = null;
		}
		bool outAnimal = animal == null;
		while (Animation.PlayingOutAnimation || (animal != null && animal.PlayingOutAnimation))
		{
			if (!outAnimal)
			{
				if (IsMatchAnimalWithActor(animal))
				{
					if (!animal.PlayingOutAnimation)
					{
						animal.SetImpossible(_value: false, this);
						animal.SetState(animal.BackupState);
						animal = null;
						outAnimal = true;
					}
				}
				else
				{
					animal = null;
					outAnimal = true;
				}
			}
			yield return null;
		}
		if (IsMatchAnimalWithActor(animal))
		{
			animal.SetImpossible(_value: false, this);
			animal.SetState(animal.BackupState);
		}
		ClearItems();
		_withAnimalEnumerator = null;
		Debug.Log("End With Animal Coroutine");
	}

	public void StopWithAnimalSequence()
	{
		if (_withAnimalDisposable != null)
		{
			_withAnimalDisposable.Dispose();
			_withAnimalDisposable = null;
		}
		_withAnimalEnumerator = null;
	}

	public bool IsMatchAnimalWithActor(AnimalBase animal)
	{
		if (animal == null || !animal.Active)
		{
			return false;
		}
		return animal.CurrentState switch
		{
			AnimalState.WithPlayer => this is PlayerActor, 
			AnimalState.WithAgent => this is AgentActor, 
			AnimalState.WithMerchant => this is MerchantActor, 
			_ => false, 
		};
	}

	private void Start()
	{
		_rigidbody.sleepThreshold = -1f;
		OnStart();
		Observable.EveryUpdate().TakeUntilDestroy(this).Subscribe(delegate
		{
			if (_rigidbody != null && _rigidbody.IsSleeping())
			{
				_rigidbody.WakeUp();
			}
			GraphicSystem graphicData = Config.GraphicData;
			if (!(Controller == null) && graphicData != null)
			{
				Controller.FaceLightActive = graphicData.FaceLight;
			}
		});
	}

	public virtual IEnumerator LoadAsync()
	{
		yield break;
	}

	public virtual void LoadEquipments()
	{
	}

	protected void LoadEquipmentItem(StuffItem item, ChaControlDefine.ExtraAccessoryParts parts)
	{
		int id = ((item.CategoryID == -1) ? (-1) : item.ID);
		ChaControl.ChangeExtraAccessory(parts, id);
		ChaControl.ShowExtraAccessory(parts, show: true);
	}

	protected virtual void InitializeIK()
	{
		IsInit = true;
	}

	protected abstract IEnumerator LoadCharAsync(string fileName);

	protected abstract void OnStart();

	public virtual void OnDayUpdated(TimeSpan timeSpan)
	{
	}

	public abstract void OnMinuteUpdated(TimeSpan timeSpan);

	public virtual void OnSecondUpdated(TimeSpan timeSpan)
	{
	}

	private void InitializeRagdoll()
	{
	}

	protected float GetSlopeDamper(Vector3 velocity, Vector3 groundNormal)
	{
		float num = 90f - Vector3.Angle(velocity, groundNormal);
		num -= _slopeStartAngle;
		float num2 = _slopeEndAngle - _slopeStartAngle;
		return 1f - Mathf.Clamp(num / num2, 0f, 1f);
	}

	public virtual bool CanUnlock(int category, int id)
	{
		return false;
	}

	public virtual bool CanAddItem(StuffItem item)
	{
		return true;
	}

	public virtual bool CanAddItem(StuffItemInfo item)
	{
		return true;
	}

	public int Lottery(Dictionary<int, ItemTableElement> table)
	{
		float num = 0f;
		foreach (KeyValuePair<int, ItemTableElement> item in table)
		{
			num += item.Value.Rate;
		}
		float num2 = UnityEngine.Random.Range(0f, num);
		int result = -1;
		foreach (KeyValuePair<int, ItemTableElement> item2 in table)
		{
			float rate = item2.Value.Rate;
			if (num2 <= rate)
			{
				result = item2.Key;
				break;
			}
			num2 -= rate;
		}
		return result;
	}

	public ItemTableElement.GatherElement Lottery(List<ItemTableElement.GatherElement> list)
	{
		float num = 0f;
		foreach (ItemTableElement.GatherElement item in list)
		{
			num += item.prob;
		}
		float num2 = UnityEngine.Random.Range(0f, num);
		int index = -1;
		int num3 = 0;
		foreach (ItemTableElement.GatherElement item2 in list)
		{
			float prob = item2.prob;
			if (num2 <= prob)
			{
				index = num3;
				break;
			}
			num2 -= prob;
			num3++;
		}
		return list[index];
	}

	public virtual SearchInfo RandomAddItem(Dictionary<int, ItemTableElement> itemTable, bool containsNone)
	{
		Manager.Resources instance = Singleton<Manager.Resources>.Instance;
		SearchInfo searchInfo;
		if (itemTable == null)
		{
			searchInfo = new SearchInfo();
			searchInfo.IsSuccess = false;
			return searchInfo;
		}
		Manager.Resources.GameInfoTables gameInfo = Singleton<Manager.Resources>.Instance.GameInfo;
		int key = Lottery(itemTable);
		if (itemTable.TryGetValue(key, out var value))
		{
			searchInfo = new SearchInfo();
			searchInfo.IsSuccess = true;
			SearchInfo searchInfo2 = searchInfo;
			foreach (ItemTableElement.GatherElement element in value.Elements)
			{
				float value2 = UnityEngine.Random.value;
				if (!(value2 < element.prob))
				{
					continue;
				}
				StuffItemInfo item = gameInfo.GetItem(element.categoryID, element.itemID);
				if (item != null)
				{
					int num = UnityEngine.Random.Range(element.minCount, element.maxCount + 1);
					if (num > 0)
					{
						searchInfo2.ItemList.Add(new ItemSearchInfo
						{
							name = item.Name,
							categoryID = element.categoryID,
							id = element.itemID,
							count = num
						});
					}
				}
			}
			if (searchInfo2.ItemList.IsNullOrEmpty())
			{
				searchInfo2.IsSuccess = false;
			}
			return searchInfo2;
		}
		searchInfo = new SearchInfo();
		searchInfo.IsSuccess = false;
		return searchInfo;
	}

	public void ResetEquipEventItem(EquipEventItemInfo itemInfo)
	{
		if (EquipedItem != null)
		{
			if (itemInfo != null)
			{
				if (EquipedItem.EventItemID == itemInfo.EventItemID)
				{
					if (!EquipedItem.AsGameObject.activeSelf)
					{
						EquipedItem.AsGameObject.SetActive(value: true);
					}
				}
				else
				{
					ReleaseEquipedEventItem();
					LoadEquipedEventItem(itemInfo);
				}
			}
			else
			{
				ReleaseEquipedEventItem();
			}
		}
		else if (itemInfo != null)
		{
			LoadEquipedEventItem(itemInfo);
		}
	}

	public virtual void LoadEventItems(PlayState playState)
	{
	}

	public virtual void LoadEventParticles(int eventID, int poseID)
	{
	}

	public void ReleaseEquipedEventItem()
	{
		if (EquipedItem != null)
		{
			if (EquipedItem.AsGameObject != null)
			{
				UnityEngine.Object.Destroy(EquipedItem.AsGameObject);
				EquipedItem.AsGameObject = null;
			}
			EquipedItem = null;
		}
	}

	protected abstract void LoadEquipedEventItem(EquipEventItemInfo eventItemInfo);

	public void LoadEventItem(int itemID, PlayState.ItemInfo itemInfo, ActionItemInfo eventItemInfo)
	{
		LoadEventItem(itemID, itemInfo.parentName, itemInfo.isSync, eventItemInfo);
	}

	public GameObject LoadEventItem(int itemID, string parentName, bool isSync, ActionItemInfo eventItemInfo)
	{
		GameObject gameObject = null;
		if (CurrentPoint != null)
		{
			gameObject = CurrentPoint.CreateEventItems(itemID, parentName, isSync);
		}
		if (gameObject == null)
		{
			gameObject = CommonLib.LoadAsset<GameObject>(eventItemInfo.assetbundleInfo.assetbundle, eventItemInfo.assetbundleInfo.asset, clone: false, eventItemInfo.assetbundleInfo.manifest);
			if (!MapScene.AssetBundlePaths.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == eventItemInfo.assetbundleInfo.assetbundle && x.Item2 == eventItemInfo.assetbundleInfo.manifest))
			{
				MapScene.AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(eventItemInfo.assetbundleInfo.assetbundle, eventItemInfo.assetbundleInfo.manifest));
			}
		}
		if (gameObject != null)
		{
			GameObject gameObject2 = ChaControl.animBody.transform.FindLoop(parentName);
			GameObject gameObject3 = UnityEngine.Object.Instantiate(gameObject, gameObject2.transform, worldPositionStays: false);
			gameObject3.SetActiveIfDifferent(active: true);
			gameObject3.gameObject.name = gameObject.gameObject.name;
			gameObject3.transform.localPosition = Vector3.zero;
			gameObject3.transform.localRotation = Quaternion.identity;
			gameObject3.transform.localScale = Vector3.one;
			Animation.Items.Add(new UnityEx.ValueTuple<int, GameObject>(itemID, gameObject3));
			Renderer[] componentsInChildren = gameObject3.GetComponentsInChildren<Renderer>();
			Renderer[] array = componentsInChildren;
			foreach (Renderer renderer in array)
			{
				renderer.enabled = false;
			}
			Animation.ItemRenderers.Add(new UnityEx.ValueTuple<int, Renderer[]>(itemID, componentsInChildren));
			if (eventItemInfo.existsAnimation)
			{
				Animator component = gameObject3.GetComponent<Animator>();
				RuntimeAnimatorController runtimeAnimatorController = AssetUtility.LoadAsset<RuntimeAnimatorController>(eventItemInfo.animeAssetBundle);
				component.runtimeAnimatorController = runtimeAnimatorController;
				Animation.ItemAnimatorTable[gameObject3.GetInstanceID()] = new ItemAnimInfo
				{
					Animator = component,
					Parameters = component.parameters,
					Sync = isSync
				};
			}
			if (Singleton<Manager.Resources>.Instance.Animation.ItemScaleTable.TryGetValue(itemID, out var value))
			{
				ItemScaleInfo itemScaleInfo = new ItemScaleInfo();
				itemScaleInfo.TargetItem = gameObject3;
				itemScaleInfo.ScaleMode = value.ScaleType;
				itemScaleInfo.SThreshold = value.SThreshold;
				itemScaleInfo.MThreshold = value.MThreshold;
				itemScaleInfo.LThreshold = value.LThreshold;
				ItemScaleInfo item = itemScaleInfo;
				_scaleCtrlInfos.Add(item);
			}
			return gameObject3;
		}
		AssetBundleInfo assetbundleInfo = eventItemInfo.assetbundleInfo;
		Debug.LogError($"イベントアイテム読み込み失敗: バンドルパス[{assetbundleInfo.assetbundle}] プレハブ[{assetbundleInfo.asset}] マニフェスト[{assetbundleInfo.manifest}]");
		return null;
	}

	protected void LoadEventParticle(Dictionary<int, List<AnimeParticleEventInfo>> eventTable)
	{
		ClearParticles();
		foreach (KeyValuePair<int, List<AnimeParticleEventInfo>> item in eventTable)
		{
			if (item.Value.IsNullOrEmpty())
			{
				continue;
			}
			foreach (AnimeParticleEventInfo item2 in item.Value)
			{
				if (item2.EventID % 2 == 1)
				{
					continue;
				}
				int particleID = item2.ParticleID;
				string root = item2.Root;
				int key = item2.EventID / 2;
				if (!Animation.Particles.TryGetValue(key, out var value))
				{
					value = (Animation.Particles[key] = new Dictionary<int, System.Tuple<GameObject, ParticleSystem, ParticleSystemRenderer>>());
				}
				if (value.ContainsKey(particleID) || !Singleton<Manager.Resources>.Instance.Map.EventParticleTable.TryGetValue(particleID, out var assetInfo))
				{
					continue;
				}
				GameObject gameObject = CommonLib.LoadAsset<GameObject>(assetInfo.assetbundle, assetInfo.asset, clone: false, assetInfo.manifest);
				if (gameObject == null)
				{
					continue;
				}
				if (!MapScene.AssetBundlePaths.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == assetInfo.assetbundle && x.Item2 == assetInfo.manifest))
				{
					MapScene.AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(assetInfo.assetbundle, assetInfo.manifest));
				}
				Transform parent = ChaControl.animBody.transform.FindLoop(root)?.transform ?? Locomotor.transform;
				GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject, parent, worldPositionStays: false);
				ParticleSystem particleSystem = gameObject2.GetComponent<ParticleSystem>();
				if (particleSystem == null)
				{
					particleSystem = gameObject2.GetComponentInChildren<ParticleSystem>();
				}
				if (particleSystem == null)
				{
					if (gameObject2 != null)
					{
						UnityEngine.Object.Destroy(gameObject2);
					}
					continue;
				}
				gameObject2.name = gameObject.name;
				gameObject2.transform.localPosition = Vector3.zero;
				gameObject2.transform.localRotation = Quaternion.identity;
				gameObject2.transform.localScale = Vector3.one;
				ParticleSystem.MainModule main = particleSystem.main;
				if (main.playOnAwake)
				{
					main.playOnAwake = false;
				}
				if (particleSystem.isPlaying)
				{
					particleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
				}
				ParticleSystemRenderer component = particleSystem.GetComponent<ParticleSystemRenderer>();
				value[particleID] = new System.Tuple<GameObject, ParticleSystem, ParticleSystemRenderer>(gameObject2, particleSystem, component);
			}
		}
	}

	public void SetDefaultStateHousingItem()
	{
		if (!(CurrentPoint != null))
		{
			return;
		}
		string housingAnimationDefault = Singleton<Manager.Resources>.Instance.DefinePack.AnimatorState.HousingAnimationDefault;
		HousingActionPointAnimation[] componentsInChildren = CurrentPoint.GetComponentsInChildren<HousingActionPointAnimation>(includeInactive: true);
		if (!componentsInChildren.IsNullOrEmpty())
		{
			HousingActionPointAnimation[] array = componentsInChildren;
			foreach (HousingActionPointAnimation housingActionPointAnimation in array)
			{
				housingActionPointAnimation.Animator.Play(housingAnimationDefault, 0);
			}
		}
	}

	public void ChangeAnimator(string assetbundle, string asset)
	{
		RuntimeAnimatorController runtimeAnimatorController = AssetUtility.LoadAsset<RuntimeAnimatorController>(assetbundle, asset, string.Empty);
		if (runtimeAnimatorController != null)
		{
			Animation.SetAnimatorController(runtimeAnimatorController);
		}
	}

	public void ChangeAnimator(RuntimeAnimatorController rac)
	{
		if (rac != null)
		{
			Animation.SetAnimatorController(rac);
		}
	}

	public void ClearItems()
	{
		Animation.ClearItems();
		_scaleCtrlInfos.Clear();
	}

	public void SetActiveOnEquipedItem(bool active)
	{
		if (EquipedItem != null && !(EquipedItem.AsGameObject == null) && EquipedItem.AsGameObject.activeSelf != active)
		{
			EquipedItem.AsGameObject.SetActive(active);
		}
	}

	public void ClearParticles()
	{
		Animation.ClearParticles();
	}

	public void SetStand(Transform t, bool enableFade, float fadeTime, int dirType)
	{
		if (_standDisposable != null)
		{
			_standDisposable.Dispose();
		}
		IConnectableObservable<TimeInterval<float>> connectableObservable = ObservableEasing.Linear(fadeTime).FrameTimeInterval().Publish();
		_standDisposable = connectableObservable.Connect();
		Vector3 position = Position;
		Quaternion rotation = Rotation;
		if (!(t != null))
		{
			return;
		}
		switch (dirType)
		{
		case 0:
			if (enableFade)
			{
				connectableObservable.Subscribe(delegate(TimeInterval<float> x)
				{
					Position = Vector3.Lerp(position, t.position, x.Value);
					Rotation = Quaternion.Slerp(rotation, t.rotation, x.Value);
				});
			}
			else if (_navMeshAgent.enabled)
			{
				NavMeshWarp(t, 0);
			}
			else
			{
				Position = t.position;
				Rotation = t.rotation;
			}
			break;
		case 1:
		{
			Vector3 position2 = base.transform.position;
			position2.y = 0f;
			Vector3 position3 = Position;
			position3.y = 0f;
			Quaternion lookRotation = Quaternion.LookRotation(Vector3.Normalize(position2 - position3));
			if (enableFade)
			{
				connectableObservable.Subscribe(delegate(TimeInterval<float> x)
				{
					Rotation = Quaternion.Slerp(rotation, lookRotation, x.Value);
				});
			}
			else
			{
				Rotation = lookRotation;
			}
			break;
		}
		}
	}

	public void StopStand()
	{
		if (_standDisposable != null)
		{
			_standDisposable.Dispose();
			_standDisposable = null;
		}
	}

	public void NavMeshWarp(Transform t, int idx, float limitDistance = 100f)
	{
		if (!(t == null))
		{
			NavMeshWarp(t.position, t.rotation, idx, limitDistance);
		}
	}

	public void NavMeshWarp(Vector3 position, Quaternion rotation, int idx, float limitDistance = 100f)
	{
		if (!_navMeshAgent.Warp(position))
		{
			List<Vector3> list = ListPool<Vector3>.Get();
			for (int i = 0; i < 3; i++)
			{
				Vector3 queryPos = new Vector3(0f, 0f, 10 + i * 10);
				SearchAroundPosition(position, queryPos, list);
			}
			Vector3? vector = null;
			if (!list.IsNullOrEmpty())
			{
				vector = Nearest(position, list);
			}
			ListPool<Vector3>.Release(list);
			NavMeshHit hit;
			if (vector.HasValue)
			{
				_navMeshAgent.Warp(vector.Value);
			}
			else if (NavMesh.SamplePosition(position, out hit, limitDistance, _navMeshAgent.areaMask))
			{
				_navMeshAgent.Warp(hit.position);
			}
			else
			{
				BasePoint[] basePoints = Singleton<Manager.Map>.Instance.PointAgent.BasePoints;
				foreach (BasePoint basePoint in basePoints)
				{
					if (basePoint.AreaIDInHousing == AreaID)
					{
						Transform transform = basePoint.RecoverPoints[0];
						_navMeshAgent.Warp(transform.position);
					}
				}
			}
		}
		Rotation = rotation;
	}

	private void SearchAroundPosition(Vector3 center, Vector3 queryPos, List<Vector3> list)
	{
		for (int i = 0; i < 9; i++)
		{
			Quaternion quaternion = Quaternion.Euler(0f, 40 * i, 0f);
			Vector3 vector = center + quaternion * queryPos;
			if (NavMesh.SamplePosition(vector, out var _, 10f, _navMeshAgent.areaMask))
			{
				list.Add(vector);
			}
		}
	}

	private Vector3 Nearest(Vector3 center, List<Vector3> list)
	{
		if (list.IsNullOrEmpty())
		{
			return Vector3.zero;
		}
		float num = float.MaxValue;
		Vector3 result = Vector3.zero;
		foreach (Vector3 item in list)
		{
			float num2 = Vector3.Distance(center, item);
			if (num > num2)
			{
				num = num2;
				result = item;
			}
		}
		return result;
	}

	public void SetLookPtn(int ptnEye, int ptnNeck)
	{
		ChaControl.ChangeLookEyesPtn(ptnEye);
		ChaControl.ChangeLookNeckPtn(ptnNeck);
	}

	public void SetLookTarget(int targetEye, int targetNeck, Transform target = null)
	{
		ChaControl.ChangeLookEyesTarget(targetEye, target);
		ChaControl.ChangeLookNeckTarget(targetNeck, target);
	}

	public bool IsVisibleDistanceAll(Transform cameraTransform)
	{
		float charaVisibleDistance = Singleton<Manager.Resources>.Instance.LocomotionProfile.CharaVisibleDistance;
		Transform[] chaFovTargets = ChaFovTargets;
		foreach (Transform transform in chaFovTargets)
		{
			float num = Vector3.Distance(transform.position, cameraTransform.position);
			if (num < charaVisibleDistance)
			{
				return false;
			}
		}
		return true;
	}

	public bool IsVisibleDistance(Transform cameraTransform, BodyPart parts, float limitDistance)
	{
		if (!ChaBodyParts.TryGetValue(parts, out var value))
		{
			return false;
		}
		float num = Vector3.Distance(value.position, cameraTransform.position);
		if (num < limitDistance)
		{
			return false;
		}
		return true;
	}
}
