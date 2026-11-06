using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ADV;
using AIChara;
using AIProject.Definitions;
using AIProject.Player;
using AIProject.SaveData;
using AIProject.UI;
using IllusionUtility.GetUtility;
using Manager;
using MessagePack;
using ReMotion;
using RootMotion.FinalIK;
using UniRx;
using UnityEngine;
using UnityEngine.AI;
using UnityEx;

namespace AIProject;

public class MerchantActor : Actor, ICommandable, INavMeshActor
{
	public enum ADV_CATEGORY
	{
		TALK = 0,
		EVENT = 1,
		H = 9,
		HScene = 10,
		TUTORIAL = 100
	}

	private class PackData : CharaPackData
	{
		public CommCommandList.CommandInfo[] restoreCommands { get; set; }

		public bool isHSuccess => hMode > 0;

		public int hMode
		{
			get
			{
				if (base.Vars == null || !base.Vars.TryGetValue("hMode", out var value))
				{
					return 0;
				}
				return (int)value.o;
			}
		}

		public string JumpTag { get; set; } = string.Empty;


		public int OpenAreaID { get; set; }

		public bool isClearFlag
		{
			get
			{
				if (!Singleton<Game>.IsInstance())
				{
					return false;
				}
				bool? flag = Singleton<Game>.Instance.WorldData?.Cleared;
				return flag.HasValue && flag.Value;
			}
		}

		public bool isLesbian { get; set; }

		public bool isWeaknessH { get; set; }

		public override List<Program.Transfer> Create()
		{
			List<Program.Transfer> list = base.Create();
			list.Add(Program.Transfer.Create(true, Command.VAR, "int", "OpenAreaID", $"{OpenAreaID}"));
			list.Add(Program.Transfer.Create(true, Command.VAR, "bool", "isClearFlag", $"{isClearFlag}"));
			list.Add(Program.Transfer.Create(true, Command.VAR, "bool", "isLesbian", $"{isLesbian}"));
			list.Add(Program.Transfer.Create(true, Command.VAR, "bool", "isWeaknessH", $"{isWeaknessH}"));
			list.Add(Program.Transfer.Create(true, Command.VAR, "string", "JumpTag", JumpTag));
			return list;
		}

		public override void Receive(TextScenario scenario)
		{
			base.Receive(scenario);
			if (restoreCommands != null)
			{
				CommCommandList commCommandList = MapUIContainer.CommandList;
				commCommandList.Refresh(restoreCommands, commCommandList.CanvasGroup);
			}
		}
	}

	[MessagePackObject(false)]
	public class MerchantSchedule
	{
		[MessagePackObject(false)]
		public class MerchantEvent
		{
			[Key(0)]
			public Merchant.ActionType Process { get; set; }

			[Key(1)]
			public Merchant.ActionType Purpose { get; set; }

			public MerchantEvent(MerchantEvent @event)
			{
				SetEvent(@event);
			}

			public MerchantEvent(Tuple<Merchant.ActionType, Merchant.ActionType> @event)
			{
				SetEvent(@event);
			}

			public MerchantEvent(Merchant.ActionType process, Merchant.ActionType purpose)
			{
				SetEvent(process, purpose);
			}

			public void SetEvent(Tuple<Merchant.ActionType, Merchant.ActionType> @event)
			{
				Process = @event.Item1;
				Purpose = @event.Item2;
			}

			public void SetEvent(Merchant.ActionType process, Merchant.ActionType purpose)
			{
				Process = process;
				Purpose = purpose;
			}

			public void SetEvent(MerchantEvent @event)
			{
				Process = @event.Process;
				Purpose = @event.Purpose;
			}
		}

		[Key(1)]
		public MerchantEvent Event;

		[Key(0)]
		public bool AbsenceDay { get; set; }

		[IgnoreMember]
		public bool IsAbsent
		{
			[CompilerGenerated]
			get
			{
				return AbsenceDay || IsMatchState(Merchant.StateType.Absent);
			}
		}

		[IgnoreMember]
		public bool IsSearch
		{
			[CompilerGenerated]
			get
			{
				return IsMatchState(Merchant.StateType.Search);
			}
		}

		[IgnoreMember]
		public bool IsWait
		{
			[CompilerGenerated]
			get
			{
				return IsMatchState(Merchant.StateType.Wait);
			}
		}

		public MerchantSchedule()
		{
		}

		public MerchantSchedule(MerchantSchedule _source)
		{
			if (_source != null)
			{
				AbsenceDay = _source.AbsenceDay;
				SetEvent(_source.Event);
			}
		}

		public bool IsMatchState(Merchant.StateType stateType)
		{
			if (AbsenceDay || Event == null)
			{
				return false;
			}
			Merchant.StateType value;
			return Merchant.StateTypeTable.TryGetValue(Event.Process, out value) && value == stateType;
		}

		public void SetEvent(MerchantEvent @event)
		{
			if (@event != null)
			{
				if (Event == null)
				{
					Event = new MerchantEvent(@event.Process, @event.Purpose);
				}
				else
				{
					Event.SetEvent(@event);
				}
			}
		}
	}

	public UnityEx.ValueTuple<CompositeDisposable, System.Action> SequenceAction = new UnityEx.ValueTuple<CompositeDisposable, System.Action>(null, null);

	public Dictionary<int, Dictionary<int, List<MerchantPoint>>> MerchantPointTable = new Dictionary<int, Dictionary<int, List<MerchantPoint>>>();

	private NavMeshPath pathForCalc;

	private CommandLabel.CommandInfo[] _emptyLabel = new CommandLabel.CommandInfo[0];

	private CommandLabel.CommandInfo[] _talkLabel;

	[SerializeField]
	private ObjectLayer _layer = ObjectLayer.Command;

	[SerializeField]
	private ActorAnimationMerchant _animation;

	[SerializeField]
	private ActorLocomotionMerchant _locomotor;

	[SerializeField]
	private MerchantController _controller;

	private float _distanceTweenPlayer = float.MaxValue;

	private float _heightDistTweenPlayer = float.MaxValue;

	private bool _prevCloseToPlayer;

	private bool _prevAnimEnable = true;

	private bool _prevLocomEnable = true;

	private bool _prevConEnable = true;

	private IDisposable _locomotionChangeSpeedDisposable;

	private CommCommandList.CommandInfo[] _normalCommandOptionInfo;

	private RaycastHit[] _raycastHits = new RaycastHit[10];

	public override string CharaName
	{
		[CompilerGenerated]
		get
		{
			return "シャン";
		}
	}

	protected MerchantBehaviorTreeResources BehaviorResources { get; private set; }

	public Merchant.ActionType BehaviorMode
	{
		[CompilerGenerated]
		get
		{
			return (BehaviorResources != null) ? BehaviorResources.Mode : Merchant.ActionType.ToAbsent;
		}
	}

	public Merchant.ActionType CurrentMode { get; private set; } = Merchant.ActionType.Wait;


	public Merchant.ActionType PrevMode { get; private set; } = Merchant.ActionType.Wait;


	public Merchant.ActionType NextMode { get; private set; } = Merchant.ActionType.Wait;


	public Merchant.ActionType LastNormalMode { get; private set; } = Merchant.ActionType.Wait;


	public int OpenAreaID { get; set; }

	public bool IsActionMoving { get; set; }

	public bool IsRunning { get; set; }

	public MerchantPoint CurrentMerchantPoint { get; set; }

	public MerchantPoint TargetInSightMerchantPoint { get; set; }

	public MerchantPoint MainTargetInSightMerchantPoint { get; set; }

	public MerchantPoint PrevMerchantPoint { get; set; }

	public MerchantPoint StartPoint { get; private set; }

	public MerchantPoint ExitPoint { get; private set; }

	public ActionPoint PrevActionPoint { get; set; }

	public ActionPoint BookingActionPoint { get; set; }

	public MerchantSchedule CurrentSchedule
	{
		get
		{
			return MerchantData?.CurrentSchedule;
		}
		private set
		{
			if (MerchantData != null)
			{
				MerchantData.CurrentSchedule = value;
			}
		}
	}

	public List<MerchantSchedule> ScheduleList
	{
		[CompilerGenerated]
		get
		{
			return MerchantData?.ScheduleList;
		}
	}

	public MerchantSchedule PrevSchedule
	{
		get
		{
			return MerchantData?.PrevSchedule;
		}
		private set
		{
			if (MerchantData != null)
			{
				MerchantData.PrevSchedule = value;
			}
		}
	}

	public bool Talkable { get; set; } = true;


	public bool ActiveEncount { get; set; }

	public Vector3 ObstaclePosition
	{
		get
		{
			return base.NavMeshObstacle.transform.position;
		}
		set
		{
			base.NavMeshObstacle.transform.position = value;
		}
	}

	public Quaternion ObstacleRotation
	{
		get
		{
			return base.NavMeshObstacle.transform.rotation;
		}
		set
		{
			base.NavMeshObstacle.transform.rotation = value;
		}
	}

	public bool IsActiveAgnet
	{
		[CompilerGenerated]
		get
		{
			return base.NavMeshAgent.enabled;
		}
	}

	public bool IsActiveObstacle
	{
		[CompilerGenerated]
		get
		{
			return base.NavMeshObstacle.enabled;
		}
	}

	public bool IsActiveNavMeshElement
	{
		[CompilerGenerated]
		get
		{
			return IsActiveAgnet || IsActiveObstacle;
		}
	}

	public bool ElapsedDay { get; set; }

	public int ElapsedDayCount { get; protected set; }

	public List<MerchantPoint> MerchantPoints { get; set; } = new List<MerchantPoint>();


	public bool IsImpossible { get; private set; }

	public Actor CommandPartner { get; set; }

	public override bool IsNeutralCommand
	{
		get
		{
			if (IsImpossible)
			{
				return false;
			}
			if (CurrentSchedule == null)
			{
				return false;
			}
			if (!Talkable)
			{
				return false;
			}
			if (!MerchantData.Unlock)
			{
				return false;
			}
			return Merchant.NormalModeList.Contains(CurrentMode) || CurrentMode == Merchant.ActionType.Encounter;
		}
	}

	public CommandLabel.CommandInfo[] Labels
	{
		get
		{
			PlayerActor playerActor = ((!Singleton<Manager.Map>.IsInstance()) ? null : Singleton<Manager.Map>.Instance.Player);
			if (playerActor != null && playerActor.PlayerController.State is Onbu)
			{
				return null;
			}
			return _talkLabel;
		}
	}

	public CommandLabel.CommandInfo[] DateLabels
	{
		[CompilerGenerated]
		get
		{
			return _emptyLabel;
		}
	}

	public ObjectLayer Layer
	{
		[CompilerGenerated]
		get
		{
			return _layer;
		}
	}

	public CommandType CommandType
	{
		[CompilerGenerated]
		get
		{
			return CommandType.Forward;
		}
	}

	public override ActorAnimation Animation
	{
		[CompilerGenerated]
		get
		{
			return _animation;
		}
	}

	public ActorAnimationMerchant AnimationMerchant
	{
		[CompilerGenerated]
		get
		{
			return _animation;
		}
	}

	public override ActorLocomotion Locomotor
	{
		[CompilerGenerated]
		get
		{
			return _locomotor;
		}
	}

	public ActorLocomotionMerchant LocomotorMerchant
	{
		[CompilerGenerated]
		get
		{
			return _locomotor;
		}
	}

	public override ActorController Controller
	{
		[CompilerGenerated]
		get
		{
			return _controller;
		}
	}

	public MerchantController MerchantController
	{
		[CompilerGenerated]
		get
		{
			return _controller;
		}
	}

	public override ICharacterInfo TiedInfo
	{
		[CompilerGenerated]
		get
		{
			return MerchantData;
		}
	}

	public MerchantData MerchantData { get; set; }

	public float DestinationSpeed { get; set; }

	public List<GameObject> ShipObjects { get; private set; }

	private int charaID
	{
		[CompilerGenerated]
		get
		{
			return MerchantData.param.charaID;
		}
	}

	private OpenData openData { get; } = new OpenData();


	private PackData packData { get; set; }

	public bool IsCloseToPlayer
	{
		get
		{
			AgentProfile.RangeParameter rangeSetting = Singleton<Manager.Resources>.Instance.AgentProfile.RangeSetting;
			return _distanceTweenPlayer <= rangeSetting.arrivedDistance && _heightDistTweenPlayer <= rangeSetting.acceptableHeight;
		}
	}

	public bool IsFarPlayer
	{
		get
		{
			AgentProfile.RangeParameter rangeSetting = Singleton<Manager.Resources>.Instance.AgentProfile.RangeSetting;
			return rangeSetting.leaveDistance < _distanceTweenPlayer || rangeSetting.acceptableHeight < _heightDistTweenPlayer;
		}
	}

	public void EnableBehavior()
	{
		if (BehaviorResources != null && !BehaviorResources.enabled)
		{
			BehaviorResources.enabled = true;
		}
	}

	public void DisableBehavior()
	{
		if (BehaviorResources != null && BehaviorResources.enabled)
		{
			BehaviorResources.enabled = false;
		}
	}

	public void StopBehavior()
	{
		if (BehaviorResources != null)
		{
			BehaviorResources.StopBehaviorTree();
		}
	}

	public void AddSequenceActionDisposable(IDisposable disposable)
	{
		(SequenceAction.Item1 ?? (SequenceAction.Item1 = new CompositeDisposable())).Add(disposable);
	}

	public void AddSequenceActionOnComplete(System.Action onComplete)
	{
		ref UnityEx.ValueTuple<CompositeDisposable, System.Action> sequenceAction = ref SequenceAction;
		sequenceAction.Item2 = (System.Action)Delegate.Combine(sequenceAction.Item2, onComplete);
	}

	public void SetSequenceAction(IDisposable disposable, System.Action onComplete)
	{
		AddSequenceActionDisposable(disposable);
		AddSequenceActionOnComplete(onComplete);
	}

	public void ClearSequenceAction()
	{
		SequenceAction.Item1 = null;
		SequenceAction.Item2 = null;
	}

	public void StopSequenceAction()
	{
		SequenceAction.Item2?.Invoke();
		SequenceAction.Item2 = null;
		SequenceAction.Item1?.Clear();
	}

	public void DisposeSequenceAction()
	{
		SequenceAction.Item2 = null;
		SequenceAction.Item1?.Clear();
	}

	public bool SetImpossible(bool value, Actor actor)
	{
		if (IsImpossible == value)
		{
			return false;
		}
		if (value && CommandPartner != null)
		{
			return false;
		}
		IsImpossible = value;
		CommandPartner = ((!value) ? null : actor);
		return true;
	}

	public bool Entered(Vector3 basePosition, float distance, float radiusA, float radiusB, float angle, Vector3 forward)
	{
		if (radiusA < distance)
		{
			return false;
		}
		Vector3 position = base.Position;
		position.y = 0f;
		float num = angle / 2f;
		float num2 = Vector3.Angle(position - basePosition, forward);
		return num2 <= num;
	}

	public bool IsReachable(NavMeshAgent nmAgent, float radiusA, float radiusB)
	{
		if (pathForCalc == null)
		{
			pathForCalc = new NavMeshPath();
		}
		bool flag = true;
		if (nmAgent.isActiveAndEnabled)
		{
			nmAgent.CalculatePath(base.Position, pathForCalc);
			flag &= pathForCalc.status == NavMeshPathStatus.PathComplete;
			float num = 0f;
			Vector3[] corners = pathForCalc.corners;
			for (int i = 0; i < corners.Length - 1; i++)
			{
				float num2 = Vector3.Distance(corners[i], corners[i + 1]);
				num += num2;
				float num3 = ((CommandType != 0) ? radiusB : radiusA);
				if (num > num3)
				{
					flag = false;
					break;
				}
			}
		}
		else
		{
			flag = false;
		}
		return flag;
	}

	private void Awake()
	{
		BehaviorResources = GetComponentInChildren<MerchantBehaviorTreeResources>(includeInactive: true);
		bool? flag = Animation?.enabled;
		_prevAnimEnable = !flag.HasValue || flag.Value;
		bool? flag2 = (((object)_locomotor != null) ? new bool?(_locomotor.enabled) : null);
		_prevLocomEnable = !flag2.HasValue || flag2.Value;
		bool? flag3 = (((object)_controller != null) ? new bool?(_controller.enabled) : null);
		_prevConEnable = !flag3.HasValue || flag3.Value;
	}

	protected override void OnStart()
	{
		BehaviorResources.Initialize();
		(from _ in Observable.EveryUpdate().TakeUntilDestroy(base.gameObject)
			where base.isActiveAndEnabled
			select _).Subscribe(delegate
		{
			OnUpdate();
		});
	}

	public override IEnumerator LoadAsync()
	{
		Animation.enabled = false;
		Animation.enabled = false;
		if (MerchantData != null)
		{
			yield return LoadCharAsync(MerchantData.CharaFileName);
		}
		else
		{
			yield return LoadCharAsync(string.Empty);
		}
		Animator animator = base.ChaControl.animBody;
		Transform commandTarget = base.ChaControl.transform.FindLoop("cf_J_Mune00")?.transform;
		CommonDefine.CommonIconGroup iconDefine = Singleton<Manager.Resources>.Instance.CommonDefine.Icon;
		packData = new PackData();
		packData.SetCommandData(Singleton<Game>.Instance?.WorldData?.Environment);
		packData.SetParam(MerchantData, Singleton<Game>.Instance?.WorldData?.PlayerData);
		Singleton<Manager.Resources>.Instance.itemIconTables.ActorIconTable.TryGetValue(ID, out var actorIcon);
		_talkLabel = new CommandLabel.CommandInfo[1]
		{
			new CommandLabel.CommandInfo
			{
				Text = "商人と話す",
				Icon = actorIcon,
				TargetSpriteInfo = iconDefine.CharaSpriteInfo,
				Transform = commandTarget,
				Event = delegate
				{
					StartCommunication();
					openData.FindLoad("0", charaID, 0);
					packData.onComplete = delegate
					{
						MapUIContainer.SetActiveCommandList(active: true, CharaName);
						MapUIContainer.SetVisibledSpendMoneyUI(visibled: true);
					};
					packData.OpenAreaID = OpenAreaID;
					packData.restoreCommands = _normalCommandOptionInfo;
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
			}
		};
		_normalCommandOptionInfo = new CommCommandList.CommandInfo[4]
		{
			new CommCommandList.CommandInfo("話す")
			{
				Event = delegate
				{
					Animation.StopAllAnimCoroutine();
					MapUIContainer.SetVisibledSpendMoneyUI(visibled: false);
					openData.FindLoad("2", charaID, 0);
					packData.onComplete = delegate
					{
						if (packData.restoreCommands != null)
						{
							MapUIContainer.SetVisibledSpendMoneyUI(visibled: true);
						}
					};
					packData.OpenAreaID = OpenAreaID;
					packData.restoreCommands = _normalCommandOptionInfo;
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
			},
			new CommCommandList.CommandInfo("ショップ")
			{
				Event = delegate
				{
					Animation.StopAllAnimCoroutine();
					MapUIContainer.SetVisibledSpendMoneyUI(visibled: false);
					openData.FindLoad("3", charaID, 0);
					packData.onComplete = delegate
					{
						MapUIContainer.CommandList.Visibled = false;
						MapUIContainer.ShopUI.OnClose = delegate
						{
							packData.JumpTag = "OUT";
							packData.onComplete = delegate
							{
								MapUIContainer.CommandList.Visibled = true;
								MapUIContainer.SetVisibledSpendMoneyUI(visibled: true);
							};
							Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
						};
						MapUIContainer.SetActiveShopUI(active: true);
					};
					packData.JumpTag = "IN";
					packData.restoreCommands = null;
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
			},
			new CommCommandList.CommandInfo("エッチさせて")
			{
				Condition = delegate
				{
					string[] merchantHMeshTag = Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.MerchantHMeshTag;
					return CanSelectHCommand(merchantHMeshTag);
				},
				Event = delegate
				{
					Animation.StopAllAnimCoroutine();
					MapUIContainer.SetVisibledSpendMoneyUI(visibled: false);
					openData.FindLoad("0", charaID, 9);
					packData.onComplete = delegate
					{
						if (packData.isHSuccess)
						{
							packData.restoreCommands = null;
							HSceneEnter(packData.hMode);
						}
						else
						{
							MapUIContainer.SetVisibledSpendMoneyUI(visibled: true);
						}
					};
					packData.JumpTag = "IN";
					packData.isLesbian = false;
					packData.isWeaknessH = false;
					packData.restoreCommands = _normalCommandOptionInfo;
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
			},
			new CommCommandList.CommandInfo("立ち去る")
			{
				Event = delegate
				{
					LeaveADV();
				}
			}
		};
		FullBodyBipedIK ik = animator.GetComponentInChildren<FullBodyBipedIK>(includeInactive: true);
		GameObject ctrlOld = _animation.gameObject;
		ActorAnimationMerchant actorAnimationMerchant = _animation.CloneComponent(animator.gameObject);
		actorAnimationMerchant.IK = ik;
		actorAnimationMerchant.Actor = this;
		actorAnimationMerchant.Character = _locomotor;
		actorAnimationMerchant.Animator = animator;
		_animation = actorAnimationMerchant;
		ik.enabled = false;
		AssetBundleInfo animABInfo = default(AssetBundleInfo);
		RuntimeAnimatorController rac = Singleton<Manager.Resources>.Instance.Animation.GetMerchantAnimator(0, ref animABInfo);
		Animation.SetDefaultAnimatorController(rac);
		Animation.SetAnimatorController(rac);
		Animation.AnimABInfo = animABInfo;
		animator.Play("Locomotion", 0, 0f);
		Animation.enabled = true;
		UnityEngine.Object.Destroy(ctrlOld);
		_locomotor.CharacterAnimation = _animation;
		MerchantActor merchantActor = this;
		Merchant.ActionType actionType = (NextMode = MerchantData.ModeType);
		actionType = (PrevMode = actionType);
		merchantActor.CurrentMode = actionType;
		LastNormalMode = MerchantData.LastNormalModeType;
		ElapsedDay = MerchantData.MapID == 0 && MerchantData.ElapsedDay;
		ElapsedDayCount = 0;
		IsActionMoving = false;
		InitializeIK();
		_controller.StartBehavior();
	}

	protected override IEnumerator LoadCharAsync(string fileName)
	{
		ChaFileControl chaFile = null;
		string assetName = "ill_Default_Merchant";
		Dictionary<int, ListInfoBase> dict = Singleton<Character>.Instance.chaListCtrl.GetCategoryInfo(ChaListDefine.CategoryNo.cha_sample_f);
		foreach (KeyValuePair<int, ListInfoBase> item in dict)
		{
			if (item.Value.GetInfo(ChaListDefine.KeyType.MainData) == assetName)
			{
				chaFile = new ChaFileControl();
				chaFile.LoadFromAssetBundle(item.Value.GetInfo(ChaListDefine.KeyType.MainAB), assetName);
				break;
			}
		}
		base.ChaControl = Singleton<Character>.Instance.CreateChara(1, base.gameObject, 0, chaFile);
		base.ChaControl.Load();
		yield return null;
		base.ChaControl.ChangeLookEyesPtn(3);
		base.ChaControl.ChangeLookNeckPtn(3);
		Controller.InitializeFaceLight(base.ChaControl.gameObject);
		yield return null;
	}

	public void ChangeMap(int mapID)
	{
		MerchantData merchantData = MerchantData;
		int mapID2 = (base.MapID = mapID);
		merchantData.MapID = mapID2;
		ResetState();
		if (base.MapID == 0)
		{
			DataUpdate(MerchantData);
			return;
		}
		ElapsedDay = false;
		if (StartPoint != null)
		{
			Transform basePoint = StartPoint.GetBasePoint(Merchant.EventType.Wait);
			if (base.NavMeshAgent.isActiveAndEnabled)
			{
				base.NavMeshAgent.Warp(basePoint.position);
			}
			else
			{
				base.Position = basePoint.position;
			}
			base.Rotation = basePoint.rotation;
			TargetInSightMerchantPoint = StartPoint;
			Merchant.ActionType actionType2 = (LastNormalMode = Merchant.ActionType.Wait);
			actionType2 = (PrevMode = actionType2);
			CurrentMode = actionType2;
		}
		else
		{
			Merchant.ActionType actionType2 = (LastNormalMode = Merchant.ActionType.Absent);
			actionType2 = (PrevMode = actionType2);
			CurrentMode = actionType2;
		}
	}

	public void ResetState()
	{
		if (Singleton<Voice>.IsInstance())
		{
			Singleton<Voice>.Instance.Stop(-90);
		}
		Animation.RecoveryPoint = null;
		Animation.EndIgnoreEvent();
		Singleton<Game>.Instance.GetExpression(-90, "標準")?.Change(base.ChaControl);
		Animation.ResetDefaultAnimatorController();
	}

	private void OnUpdate()
	{
		DataUpdate();
		UpdateEncounter();
		if (_mapAreaID != null && base.MapArea != null)
		{
			_mapAreaID.Value = base.MapArea.AreaID;
		}
		if (base.MapArea != null)
		{
			MerchantData.AreaID = base.MapArea.AreaID;
		}
		MerchantData merchantData = MerchantData;
		StatusProfile statusProfile = Singleton<Manager.Resources>.Instance.StatusProfile;
		EnvironmentSimulator simulator = Singleton<Manager.Map>.Instance.Simulator;
		if (simulator.EnabledTimeProgression)
		{
			Weather weather = simulator.Weather;
			if (base.AreaType == MapArea.AreaType.Indoor)
			{
				merchantData.Wetness += statusProfile.DrySpeed * Time.deltaTime;
			}
			else
			{
				switch (weather)
				{
				case Weather.Rain:
					merchantData.Wetness += statusProfile.WetRateInRain * Time.deltaTime;
					break;
				case Weather.Storm:
					merchantData.Wetness += statusProfile.WetRateInStorm * Time.deltaTime;
					break;
				default:
					merchantData.Wetness += statusProfile.DrySpeed * Time.deltaTime;
					break;
				}
				merchantData.Wetness = Mathf.Clamp(merchantData.Wetness, 0f, 100f);
			}
		}
		if (base.ChaControl != null)
		{
			float wetRate = Mathf.InverseLerp(0f, 100f, merchantData.Wetness);
			base.ChaControl.wetRate = wetRate;
		}
	}

	private void DataUpdate()
	{
		MerchantData.MapID = base.MapID;
		if (base.MapID == 0)
		{
			if (!IsActionMoving && _navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
			{
				MerchantData.Position = base.Position;
				MerchantData.Rotation = base.Rotation;
			}
			MerchantData.ChunkID = base.ChunkID;
			MerchantData.AreaID = base.AreaID;
			MerchantData.ModeType = CurrentMode;
			MerchantData.LastNormalModeType = LastNormalMode;
			MerchantData merchantData = MerchantData;
			int? num = CommandPartner?.ID;
			merchantData.ActionTargetID = ((!num.HasValue) ? (-1) : num.Value);
			MerchantData.OpenAreaID = OpenAreaID;
			MerchantData.ElapsedDay = ElapsedDay;
		}
	}

	public void DataUpdate(MerchantData data)
	{
		if (base.NavMeshAgent.isActiveAndEnabled)
		{
			base.NavMeshAgent.Warp(data.Position);
		}
		else
		{
			base.Position = data.Position;
		}
		base.Rotation = data.Rotation;
		base.MapID = data.MapID;
		base.ChunkID = data.ChunkID;
		CurrentMode = data.ModeType;
		LastNormalMode = data.LastNormalModeType;
		OpenAreaID = data.OpenAreaID;
		ElapsedDay = data.MapID == 0 && data.ElapsedDay;
	}

	public void SetOpenAreaID(Manager.Map map)
	{
		if (!Singleton<Manager.Resources>.IsInstance())
		{
			return;
		}
		IReadOnlyDictionary<int, List<UnityEx.ValueTuple<int, int>>> areaOpenState = Singleton<Manager.Resources>.Instance.MerchantProfile.AreaOpenState;
		if (areaOpenState == null || areaOpenState.Count == 0 || !areaOpenState.TryGetValue(map.MapID, out var value) || value.IsNullOrEmpty())
		{
			return;
		}
		OpenAreaID = 0;
		if (MerchantData != null)
		{
			MerchantData.OpenAreaID = 0;
		}
		value.Sort((UnityEx.ValueTuple<int, int> x1, UnityEx.ValueTuple<int, int> x2) => x1.Item1 - x2.Item1);
		for (int i = 0; i < value.Count; i++)
		{
			UnityEx.ValueTuple<int, int> valueTuple = value[i];
			if (map.GetOpenAreaState(valueTuple.Item1))
			{
				OpenAreaID = valueTuple.Item2;
				if (MerchantData != null)
				{
					MerchantData.OpenAreaID = valueTuple.Item2;
				}
				continue;
			}
			break;
		}
	}

	private bool IsTraverseOffMeshLink()
	{
		if (base.NavMeshAgent.isOnOffMeshLink)
		{
			return true;
		}
		if (base.NavMeshAgent.currentOffMeshLinkData.valid)
		{
			return true;
		}
		if (base.NavMeshAgent.currentOffMeshLinkData.offMeshLink != null)
		{
			ActionPoint component = base.NavMeshAgent.currentOffMeshLinkData.offMeshLink.GetComponent<ActionPoint>();
			if (!component.IsNeutralCommand)
			{
				return true;
			}
		}
		if (base.EventKey == EventType.Move || base.EventKey == EventType.DoorClose || base.EventKey == EventType.DoorOpen)
		{
			return true;
		}
		return false;
	}

	private void UpdateEncounter()
	{
		PlayerActor player = Manager.Map.GetPlayer();
		if (player == null)
		{
			return;
		}
		IState state = player.PlayerController.State;
		Vector3 position = base.Position;
		Vector3 position2 = player.Position;
		_heightDistTweenPlayer = Mathf.Abs(position2.y - position.y);
		position.y = (position2.y = 0f);
		_distanceTweenPlayer = Vector3.Distance(position, position2);
		if (!(state is Normal) && !(state is Photo))
		{
			return;
		}
		if (!_prevCloseToPlayer && ActiveEncount && Talkable && IsCloseToPlayer && Merchant.EncountList.Contains(CurrentMode))
		{
			if (!IsTraverseOffMeshLink())
			{
				_prevCloseToPlayer = true;
				ChangeBehavior(Merchant.ActionType.Encounter);
			}
		}
		else if (_prevCloseToPlayer && !IsImpossible && IsFarPlayer)
		{
			_prevCloseToPlayer = false;
		}
	}

	public void SetPointIDInfo(MerchantPoint point)
	{
		if (!(point == null) && MerchantData.MapID == 0)
		{
			MerchantData.PointID = point.PointID;
			MerchantData.PointAreaID = point.AreaID;
			MerchantData.PointGroupID = point.GroupID;
			MerchantData.PointPosition = point.transform.position;
			MerchantData.MainPointPosition = new Vector3(-999f, -999f, -999f);
		}
	}

	public override void OnDayUpdated(TimeSpan timeSpan)
	{
		ElapsedDayCount = Mathf.Max(timeSpan.Days, 1);
		ElapsedDay = true;
		if (!MerchantData.Unlock || base.MapID != 0)
		{
			ElapsedDay = false;
		}
		else if (!IsActionMoving && (!(BehaviorResources != null) || BehaviorResources.enabled) && Merchant.ChangeableModeList.Contains(CurrentMode))
		{
			ChangeNextSchedule();
			SetCurrentSchedule();
		}
	}

	public override void OnMinuteUpdated(TimeSpan timeSpan)
	{
	}

	protected override void LoadEquipedEventItem(EquipEventItemInfo eventItemInfo)
	{
	}

	public override void LoadEventParticles(int eventID, int poseID)
	{
		if (Singleton<Manager.Resources>.Instance.Animation.MerchantCommonParticleEventKeyTable.TryGetValue(eventID, out var value) && value.TryGetValue(poseID, out var value2) && value2 != null)
		{
			LoadEventParticle(value2);
		}
	}

	public void LoadMerchantEventParticles(int eventID, int poseID)
	{
		if (Singleton<Manager.Resources>.Instance.Animation.MerchantOnlyParticleEventKeyTable.TryGetValue(eventID, out var value) && value.TryGetValue(poseID, out var value2) && value2 != null)
		{
			LoadEventParticle(value2);
		}
	}

	public void SetMerchantPoints(MerchantPoint[] merchantPoints)
	{
		StartPoint = null;
		ExitPoint = null;
		if (MerchantPoints == null)
		{
			MerchantPoints = new List<MerchantPoint>();
		}
		else
		{
			MerchantPoints.Clear();
		}
		if (MerchantPointTable == null)
		{
			MerchantPointTable = new Dictionary<int, Dictionary<int, List<MerchantPoint>>>();
		}
		else
		{
			MerchantPointTable.Clear();
		}
		if (merchantPoints.IsNullOrEmpty())
		{
			return;
		}
		MerchantPoints.AddRange(merchantPoints);
		foreach (MerchantPoint merchantPoint in merchantPoints)
		{
			if (!MerchantPointTable.ContainsKey(merchantPoint.AreaID))
			{
				MerchantPointTable[merchantPoint.AreaID] = new Dictionary<int, List<MerchantPoint>>();
			}
			if (!MerchantPointTable[merchantPoint.AreaID].ContainsKey(merchantPoint.GroupID))
			{
				MerchantPointTable[merchantPoint.AreaID][merchantPoint.GroupID] = new List<MerchantPoint>();
			}
			MerchantPointTable[merchantPoint.AreaID][merchantPoint.GroupID].Add(merchantPoint);
			if (!(merchantPoint == null))
			{
				if (merchantPoint.IsStartPoint)
				{
					StartPoint = merchantPoint;
				}
				if (merchantPoint.IsExitPoint)
				{
					ExitPoint = merchantPoint;
				}
			}
		}
		if (!(ExitPoint != null) || !Singleton<Manager.Resources>.IsInstance())
		{
			return;
		}
		int mapShipItemID = Singleton<Manager.Resources>.Instance.MerchantProfile.MapShipItemID;
		ShipObjects = MapItemData.Get(mapShipItemID);
		List<ForcedHideObject> list = new List<ForcedHideObject>();
		if (!ShipObjects.IsNullOrEmpty())
		{
			foreach (GameObject shipObject in ShipObjects)
			{
				if (!(shipObject == null))
				{
					ForcedHideObject orAddComponent = shipObject.GetOrAddComponent<ForcedHideObject>();
					if (!(orAddComponent == null) && !list.Contains(orAddComponent))
					{
						orAddComponent.Init();
						list.Add(orAddComponent);
					}
				}
			}
		}
		ExitPoint.ItemObjects = list;
	}

	public void ChangeBehavior(Merchant.ActionType mode)
	{
		if (mode != CurrentMode)
		{
			PrevMode = CurrentMode;
			CurrentMode = mode;
			if (Merchant.NormalModeList.Contains(CurrentMode))
			{
				LastNormalMode = CurrentMode;
			}
		}
		if (Merchant.StateTypeTable.TryGetValue(mode, out var value))
		{
			MerchantData.StateType = value;
		}
		BehaviorResources.ChangeMode(mode);
	}

	private void OnEnable()
	{
		if (Animation != null && _prevAnimEnable)
		{
			Animation.enabled = true;
		}
		if (Locomotor != null && _prevLocomEnable)
		{
			Locomotor.enabled = true;
		}
		if (Controller != null && _prevConEnable)
		{
			Controller.enabled = true;
		}
	}

	private void OnDisable()
	{
		if (Animation != null && (_prevAnimEnable = Animation.enabled))
		{
			Animation.enabled = false;
		}
		if (Locomotor != null && (_prevLocomEnable = Locomotor.enabled))
		{
			Locomotor.enabled = false;
		}
		if (Controller != null && (_prevConEnable = Controller.enabled))
		{
			Controller.enabled = false;
		}
	}

	public void Show()
	{
		AnimationMerchant.enabled = true;
		LocomotorMerchant.enabled = true;
		MerchantController.enabled = true;
		base.ChaControl.visibleAll = true;
		AnimationMerchant.EnableItems();
		AnimationMerchant.EnableParticleRenderer();
	}

	public void Hide()
	{
		AnimationMerchant.enabled = false;
		LocomotorMerchant.enabled = false;
		MerchantController.enabled = false;
		base.ChaControl.visibleAll = false;
		AnimationMerchant.DisableItems();
		AnimationMerchant.DisableParticleRenderer();
	}

	public override void EnableEntity()
	{
		if (CurrentMode != Merchant.ActionType.Absent)
		{
			Show();
		}
		EnableBehavior();
	}

	public override void DisableEntity()
	{
		Hide();
		DisableBehavior();
	}

	public void ActivateLocomotionMotion()
	{
		int key = 0;
		_locomotor.MovePoseID = 0;
		Dictionary<int, PlayState> merchantLocomotionStateTable = Singleton<Manager.Resources>.Instance.Animation.MerchantLocomotionStateTable;
		merchantLocomotionStateTable.TryGetValue(key, out var value);
		if (value != null)
		{
			Animation.StopAllAnimCoroutine();
			Animator animator = Animation.Animator;
			PlayState.Info[] stateInfos = value.MainStateInfo.InStateInfo.StateInfos;
			if (!stateInfos.IsNullOrEmpty() && !animator.GetCurrentAnimatorStateInfo(0).IsName(stateInfos.LastOrDefault().stateName))
			{
				Animation.InitializeStates(value);
				int layer = value.Layer;
				bool enableFade = value.MainStateInfo.InStateInfo.EnableFade;
				float fadeSecond = value.MainStateInfo.InStateInfo.FadeSecond;
				Animation.PlayInLocoAnimation(enableFade, fadeSecond, layer);
			}
		}
		DestinationSpeed = GetLocomotionSpeed(_locomotor.MovePoseID);
	}

	public void StartLocomotion(Vector3 destination)
	{
		float destinationSpeed = DestinationSpeed;
		float speed = base.NavMeshAgent.speed;
		ActivateNavMeshAgent();
		if (base.NavMeshAgent.isStopped)
		{
			base.NavMeshAgent.isStopped = false;
		}
		if (base.IsKinematic)
		{
			base.IsKinematic = false;
		}
		base.NavMeshAgent.SetDestination(destination);
		if (_locomotionChangeSpeedDisposable != null)
		{
			_locomotionChangeSpeedDisposable.Dispose();
		}
		_locomotionChangeSpeedDisposable = ObservableEasing.Linear(1f).TakeUntilDestroy(base.gameObject).FrameTimeInterval()
			.Subscribe(delegate(TimeInterval<float> x)
			{
				base.NavMeshAgent.speed = Mathf.Lerp(speed, destinationSpeed, x.Value);
			});
	}

	public float GetLocomotionSpeed(int movePoseID)
	{
		return Singleton<Manager.Resources>.Instance.LocomotionProfile.MerchantSpeed.walkSpeed;
	}

	public void DeactivateTransfer()
	{
		base.NavMeshAgent.isStopped = true;
		base.IsKinematic = true;
	}

	public new void ActivateNavMeshAgent()
	{
		if (base.NavMeshObstacle.enabled)
		{
			base.NavMeshObstacle.enabled = false;
		}
		if (!base.NavMeshAgent.enabled)
		{
			base.NavMeshAgent.enabled = true;
		}
	}

	public new void DeactivateNavMeshAgent()
	{
		if (base.NavMeshAgent.enabled)
		{
			base.NavMeshAgent.enabled = false;
		}
	}

	public void ActivateNavMeshObstacle()
	{
		if (base.NavMeshAgent.enabled)
		{
			base.NavMeshAgent.enabled = false;
		}
		if (!base.NavMeshObstacle.enabled)
		{
			base.NavMeshObstacle.enabled = true;
		}
	}

	public void ActivateNavMeshObstacle(Vector3 position)
	{
		base.NavMeshObstacle.transform.position = position;
		if (base.NavMeshAgent.enabled)
		{
			base.NavMeshAgent.enabled = false;
		}
		if (!base.NavMeshObstacle.enabled)
		{
			base.NavMeshObstacle.enabled = true;
		}
	}

	public void DeactivateNavMeshObjstacle()
	{
		if (base.NavMeshObstacle.enabled)
		{
			base.NavMeshObstacle.enabled = false;
		}
	}

	public void DeactivateNavMeshElement()
	{
		if (base.NavMeshObstacle.enabled)
		{
			base.NavMeshObstacle.enabled = false;
		}
		if (base.NavMeshAgent.enabled)
		{
			base.NavMeshAgent.enabled = false;
		}
	}

	public bool IsInvalidMoveDestination()
	{
		OffMeshLink offMeshLink = null;
		if (_navMeshAgent.currentOffMeshLinkData.activated)
		{
			offMeshLink = _navMeshAgent.currentOffMeshLinkData.offMeshLink;
		}
		if (offMeshLink == null && (bool)_navMeshAgent.nextOffMeshLinkData.offMeshLink)
		{
			offMeshLink = _navMeshAgent.nextOffMeshLinkData.offMeshLink;
		}
		if (offMeshLink == null)
		{
			return false;
		}
		ActionPoint component = offMeshLink.GetComponent<ActionPoint>();
		if (component == null)
		{
			return false;
		}
		if (!component.IsNeutralCommand)
		{
			return true;
		}
		List<ActionPoint> connectedActionPoints = component.ConnectedActionPoints;
		if (connectedActionPoints != null)
		{
			foreach (ActionPoint item in connectedActionPoints)
			{
				if (item != null && !item.IsNeutralCommand)
				{
					return true;
				}
			}
		}
		return false;
	}

	public bool IsArrived()
	{
		NavMeshAgent navMeshAgent = base.NavMeshAgent;
		float approachDistanceActionPoint = Singleton<Manager.Resources>.Instance.LocomotionProfile.ApproachDistanceActionPoint;
		bool flag = false;
		if (navMeshAgent.isActiveAndEnabled && (navMeshAgent.hasPath || navMeshAgent.pathPending))
		{
			if (navMeshAgent.hasPath)
			{
				flag = navMeshAgent.remainingDistance <= approachDistanceActionPoint;
				if (flag && TargetInSightMerchantPoint != null)
				{
					flag = Vector3.Distance(base.Position, TargetInSightMerchantPoint.Destination) <= approachDistanceActionPoint;
				}
			}
		}
		else if (TargetInSightMerchantPoint != null)
		{
			flag = Vector3.Distance(base.Position, TargetInSightMerchantPoint.Destination) <= approachDistanceActionPoint;
		}
		return flag;
	}

	public bool IsArrivedToOffMesh()
	{
		OffMeshLink offMeshLink = base.NavMeshAgent.nextOffMeshLinkData.offMeshLink;
		if (offMeshLink == null)
		{
			return false;
		}
		Transform startTransform = offMeshLink.startTransform;
		if (startTransform == null)
		{
			return false;
		}
		Vector3 position = startTransform.position;
		float approachDistanceActionPoint = Singleton<Manager.Resources>.Instance.LocomotionProfile.ApproachDistanceActionPoint;
		float num = Vector3.Distance(base.Position, position);
		return num <= approachDistanceActionPoint;
	}

	public void FirstAppear()
	{
		if (base.MapID != 0)
		{
			ChangeBehavior(CurrentMode);
			return;
		}
		if (ScheduleList.IsNullOrEmpty())
		{
			MerchantSchedule merchantSchedule = new MerchantSchedule();
			merchantSchedule.AbsenceDay = false;
			merchantSchedule.Event = Merchant.ScheduleTaskTable[Merchant.StateType.Wait];
			MerchantSchedule item = merchantSchedule;
			if (StartPoint != null)
			{
				Tuple<MerchantPointInfo, Transform, Transform> eventInfo = StartPoint.GetEventInfo(Merchant.EventType.Wait);
				base.Position = eventInfo.Item2.position;
				base.Rotation = eventInfo.Item2.rotation;
			}
			ScheduleList.Clear();
			ScheduleList.Add(item);
			SetCurrentSchedule();
			MerchantPoint merchantPoint = (TargetInSightMerchantPoint = StartPoint);
			SetMerchantPoint(merchantPoint);
			ChangeBehavior(Merchant.ActionType.Wait);
			return;
		}
		if (0 <= MerchantData.PointAreaID && 0 <= MerchantData.PointGroupID)
		{
			int pointAreaID = MerchantData.PointAreaID;
			int pointGroupID = MerchantData.PointGroupID;
			Vector3 pointPosition = MerchantData.PointPosition;
			Vector3 mainPointPosition = MerchantData.MainPointPosition;
			List<MerchantPoint> list = ListPool<MerchantPoint>.Get();
			TargetInSightMerchantPoint = null;
			MainTargetInSightMerchantPoint = null;
			foreach (MerchantPoint merchantPoint2 in MerchantPoints)
			{
				if (!(merchantPoint2 == null))
				{
					if (TargetInSightMerchantPoint == null && SamePosition(merchantPoint2.transform.position, pointPosition))
					{
						TargetInSightMerchantPoint = merchantPoint2;
					}
					if (MainTargetInSightMerchantPoint == null && SamePosition(merchantPoint2.transform.position, mainPointPosition))
					{
						MainTargetInSightMerchantPoint = merchantPoint2;
					}
					if (merchantPoint2.AreaID == pointAreaID && merchantPoint2.GroupID == pointGroupID)
					{
						list.Add(merchantPoint2);
					}
				}
			}
			if (TargetInSightMerchantPoint == null && !list.IsNullOrEmpty())
			{
				TargetInSightMerchantPoint = list.GetElement(UnityEngine.Random.Range(0, list.Count));
			}
			ListPool<MerchantPoint>.Release(list);
		}
		bool elapsedDay = ElapsedDay;
		SetCurrentSchedule();
		ElapsedDay = elapsedDay;
	}

	private bool SamePosition(Vector3 p0, Vector3 p1)
	{
		return Mathf.Approximately(p0.x, p1.x) && Mathf.Approximately(p0.y, p1.y) && Mathf.Approximately(p0.z, p1.z);
	}

	public void SetMerchantPoint(MerchantPoint merchantPoint)
	{
		if (!(merchantPoint == null) && !(merchantPoint == CurrentMerchantPoint))
		{
			if (CurrentMerchantPoint != null)
			{
				PrevMerchantPoint = CurrentMerchantPoint;
			}
			CurrentMerchantPoint = merchantPoint;
			SetPointIDInfo(merchantPoint);
		}
	}

	public void AddSchedule()
	{
		int? num = ScheduleList?.Count;
		int num2 = (num.HasValue ? num.Value : 0);
		int num3 = 0;
		int num4 = 0;
		bool flag = false;
		bool flag2 = false;
		int oneCycle = Singleton<Manager.Resources>.Instance.MerchantProfile.OneCycle;
		if (ScheduleList.IsNullOrEmpty())
		{
			num3 = UnityEngine.Random.Range(2, oneCycle);
			num4 = num3 - 1;
		}
		else if (ScheduleList[num2 - 1].AbsenceDay)
		{
			num3 = UnityEngine.Random.Range(2, oneCycle);
			num4 = num3 - 1;
			flag2 = true;
		}
		else
		{
			num3 = UnityEngine.Random.Range(1, oneCycle);
			num4 = num3 - 1;
			MerchantSchedule merchantSchedule = ScheduleList[num2 - 1];
			flag = merchantSchedule.IsSearch;
		}
		for (int i = 0; i < oneCycle; i++)
		{
			MerchantSchedule merchantSchedule2 = new MerchantSchedule();
			merchantSchedule2.AbsenceDay = false;
			merchantSchedule2.Event = null;
			MerchantSchedule merchantSchedule3 = merchantSchedule2;
			if (num4 == i || num3 == i)
			{
				merchantSchedule3.Event = Merchant.ScheduleTaskTable[Merchant.StateType.Absent];
				merchantSchedule3.AbsenceDay = num3 == i;
			}
			else if (flag || flag2)
			{
				merchantSchedule3.Event = Merchant.ScheduleTaskTable[Merchant.StateType.Wait];
			}
			else
			{
				float toSearchSelectedRange = Singleton<Manager.Resources>.Instance.MerchantProfile.ToSearchSelectedRange;
				float num5 = UnityEngine.Random.Range(0f, 100f);
				Merchant.StateType key = (Mathf.Approximately(toSearchSelectedRange, 0f) ? Merchant.StateType.Wait : ((!Mathf.Approximately(toSearchSelectedRange, 100f)) ? ((!(num5 <= toSearchSelectedRange)) ? Merchant.StateType.Wait : Merchant.StateType.Search) : Merchant.StateType.Search));
				merchantSchedule3.Event = Merchant.ScheduleTaskTable[key];
			}
			flag = merchantSchedule3.IsSearch;
			flag2 = merchantSchedule3.AbsenceDay;
			ScheduleList.Add(merchantSchedule3);
		}
	}

	public void ChangeNextSchedule()
	{
		int oneCycle = Singleton<Manager.Resources>.Instance.MerchantProfile.OneCycle;
		if (ScheduleList.Count < oneCycle)
		{
			AddSchedule();
		}
		if (CurrentSchedule != null)
		{
			PrevSchedule = CurrentSchedule;
		}
		CurrentSchedule = ScheduleList.Pop();
		if (ScheduleList.Count < oneCycle)
		{
			AddSchedule();
		}
	}

	public void SetCurrentSchedule()
	{
		int oneCycle = Singleton<Manager.Resources>.Instance.MerchantProfile.OneCycle;
		if (ScheduleList.Count < oneCycle)
		{
			AddSchedule();
		}
		if (CurrentSchedule == null)
		{
			CurrentSchedule = ScheduleList.Pop();
		}
		if (ScheduleList.Count < oneCycle)
		{
			AddSchedule();
		}
		ElapsedDay = false;
		if (CurrentSchedule.AbsenceDay)
		{
			if (!BehaviorResources.IsMatchCurrentTree(Merchant.ActionType.Absent))
			{
				ChangeBehavior(Merchant.ActionType.Absent);
			}
			return;
		}
		bool? flag = PrevSchedule?.AbsenceDay;
		if (flag.HasValue && flag.Value)
		{
			ChangeBehavior(CurrentSchedule.Event.Purpose);
		}
		else
		{
			ChangeBehavior(CurrentSchedule.Event.Process);
		}
	}

	public void CrossFade()
	{
		Renderer componentInChildren = base.ChaControl.objBody.GetComponentInChildren<Renderer>();
		Renderer componentInChildren2 = base.ChaControl.objHead.GetComponentInChildren<Renderer>();
		bool flag = componentInChildren != null && componentInChildren.isVisible;
		if (flag | (componentInChildren2 != null && componentInChildren2.isVisible))
		{
			ActorCameraControl cameraControl = Singleton<Manager.Map>.Instance.Player.CameraControl;
			float num = Vector3.Distance(base.Position, cameraControl.transform.position);
			float crossFadeEnableDistance = Singleton<Manager.Resources>.Instance.LocomotionProfile.CrossFadeEnableDistance;
			if (num <= crossFadeEnableDistance)
			{
				cameraControl.CrossFade.FadeStart();
			}
		}
	}

	private void StartCommunication()
	{
		packData.Init();
		PlayerActor player = Singleton<Manager.Map>.Instance.Player;
		player.CommCompanion = this;
		player.Controller.ChangeState("Communication");
		ChangeBehavior(Merchant.ActionType.TalkWithPlayer);
		TurnToActorCrossFade(player);
		base.ChaControl.ChangeLookEyesTarget(0);
		base.ChaControl.ChangeLookNeckTarget(0);
		base.ChaControl.ChangeLookEyesPtn(1);
		base.ChaControl.ChangeLookNeckPtn(1);
		Observable.TimerFrame(1).Subscribe(delegate
		{
			Manager.ADV.ChangeADVCamera(this);
		});
		MapUIContainer.SetVisibleHUD(active: false);
		Singleton<Manager.ADV>.Instance.TargetMerchant = this;
		PopupCommands();
	}

	private void TurnToActorCrossFade(Actor actor)
	{
		DisposeSequenceAction();
		PlayerActor player = Singleton<Manager.Map>.Instance.Player;
		ActorCameraControl cameraControl = player.CameraControl;
		cameraControl.CrossFade.FadeStart();
		Animation.StopAllAnimCoroutine();
		Animation.Animator.Play(Singleton<Manager.Resources>.Instance.DefinePack.AnimatorState.MerchantIdleState, 0);
		Vector3 forward = actor.Position - base.Position;
		forward.y = 0f;
		base.Rotation = Quaternion.LookRotation(forward, Vector3.up);
	}

	private void EndCommunication()
	{
		MapUIContainer.SetVisibledSpendMoneyUI(visibled: false);
		MapUIContainer.SetActiveCommandList(active: false);
		VanishCommands();
		packData.Release();
	}

	private void LeaveADV()
	{
		MapUIContainer.SetVisibledSpendMoneyUI(visibled: false);
		openData.FindLoad("1", charaID, 0);
		packData.onComplete = delegate
		{
			EndCommunication();
		};
		packData.OpenAreaID = OpenAreaID;
		packData.restoreCommands = null;
		Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
	}

	public void PopupCommands()
	{
		Singleton<Manager.Input>.Instance.ReserveState(Manager.Input.ValidType.UI);
		Singleton<Manager.Input>.Instance.SetupState();
		MapUIContainer.CommandList.CancelEvent = delegate
		{
			LeaveADV();
		};
	}

	public void OpenShopUI()
	{
		PopupCommands();
	}

	public void InitiateHScene()
	{
		PopupCommands();
	}

	private void HSceneEnter(int hMode)
	{
		ChangeBehavior(Merchant.ActionType.HWithPlayer);
		Singleton<HSceneManager>.Instance.HsceneEnter(this, hMode);
	}

	public void VanishCommands()
	{
		Singleton<Manager.ADV>.Instance.Captions.EndADV(EnableBehavior);
		if (TargetInSightMerchantPoint == null)
		{
			TargetInSightMerchantPoint = PrevMerchantPoint;
		}
		MapUIContainer.SetVisibleHUD(active: true);
		PlayerActor player = Singleton<Manager.Map>.Instance.Player;
		player.CameraControl.Mode = CameraMode.Normal;
		player.Controller.ChangeState("Normal");
		player.SetScheduledInteractionState(isEnabled: true);
		player.ReleaseInteraction();
		Manager.Input instance = Singleton<Manager.Input>.Instance;
		instance.ReserveState(Manager.Input.ValidType.Action);
		instance.SetupState();
		MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.InvokeAcception);
		player.ChaControl.visibleAll = true;
		base.ChaControl.ChangeLookEyesPtn(0);
		base.ChaControl.ChangeLookNeckPtn(3);
		ChangeBehavior(LastNormalMode);
	}

	public bool CanSelectHCommand(string[] tagName)
	{
		return Game.isAdd01 && IsOnHMesh(tagName);
	}

	private bool IsOnHMesh(string[] tagName = null)
	{
		if (tagName.IsNullOrEmpty())
		{
			return false;
		}
		LayerMask hLayer = Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.HLayer;
		Vector3 position = base.Position;
		position.y += 15f;
		int a = Physics.SphereCastNonAlloc(position, 7.5f, Vector3.down, _raycastHits, 25f, hLayer);
		a = Mathf.Min(a, _raycastHits.Length);
		if (a == 0)
		{
			return false;
		}
		bool flag = false;
		for (int i = 0; i < a; i++)
		{
			RaycastHit raycastHit = _raycastHits[i];
			string text = raycastHit.collider.tag;
			if (text.IsNullOrEmpty() || text == "Untagged")
			{
				continue;
			}
			foreach (string text2 in tagName)
			{
				if (flag = text2 == text)
				{
					break;
				}
			}
			if (flag)
			{
				break;
			}
		}
		return flag;
	}
}
