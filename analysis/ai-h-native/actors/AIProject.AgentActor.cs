using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using ADV;
using AIChara;
using AIProject.Animal;
using AIProject.Definitions;
using AIProject.Player;
using AIProject.RootMotion;
using AIProject.SaveData;
using AIProject.Scene;
using AIProject.UI;
using AIProject.UI.Viewer;
using Cinemachine;
using Illusion.Extensions;
using IllusionUtility.GetUtility;
using Manager;
using RootMotion.FinalIK;
using Sirenix.OdinInspector;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEx;

namespace AIProject;

public class AgentActor : Actor, ICommandable, INavMeshActor
{
	public enum ADV_CATEGORY
	{
		TALK = 0,
		INSTRUCTION = 1,
		SPEAK = 2,
		PRESENT = 3,
		IN = 4,
		OUT = 5,
		Event = 6,
		Phase = 7,
		Together = 8,
		H = 9,
		HScene = 10,
		Sickness = 11,
		Food = 12,
		AddEV = 13,
		TUTORIAL = 100
	}

	public class PackData : CharaPackData
	{
		public CommCommandList.CommandInfo[] restoreCommands
		{
			set
			{
				_restoreCommands = value;
			}
		}

		private CommCommandList.CommandInfo[] _restoreCommands { get; set; }

		public System.Action OnEndRefreshCommand { get; set; }

		public int AttitudeID { get; set; }

		public bool isFavoriteTarget { get; set; }

		public string FavoriteTargetName { get; set; } = string.Empty;


		public bool isThisPartner { get; set; } = true;


		public bool isSuccessFollow
		{
			get
			{
				if (base.Vars == null || !base.Vars.TryGetValue("isSuccessFollow", out var value))
				{
					return false;
				}
				return (bool)value.o;
			}
		}

		public bool isSuccessH
		{
			get
			{
				if (base.Vars == null || !base.Vars.TryGetValue("isSuccessH", out var value))
				{
					return false;
				}
				return (bool)value.o;
			}
		}

		public int hMode
		{
			get
			{
				if (base.Vars == null || !base.Vars.TryGetValue("hMode", out var value))
				{
					return -1;
				}
				return (int)value.o;
			}
		}

		public override List<Program.Transfer> Create()
		{
			List<Program.Transfer> list = base.Create();
			list.Add(Program.Transfer.Create(true, Command.VAR, "int", "AttitudeID", AttitudeID.ToString()));
			list.Add(Program.Transfer.Create(true, Command.VAR, "bool", "isFavoriteTarget", isFavoriteTarget.ToString()));
			list.Add(Program.Transfer.Create(true, Command.VAR, "string", "FavoriteTargetName", FavoriteTargetName));
			list.Add(Program.Transfer.Create(true, Command.VAR, "bool", "isThisPartner", isThisPartner.ToString()));
			return list;
		}

		public override void Receive(TextScenario scenario)
		{
			base.Receive(scenario);
			CommCommandList commCommandList = MapUIContainer.CommandList;
			if (_restoreCommands != null)
			{
				commCommandList.Refresh(_restoreCommands, commCommandList.CanvasGroup, OnEndRefreshCommand);
			}
		}
	}

	private class LeaveAloneDisposableInfo
	{
		private SingleAssignmentDisposable[] _arrayDisposable;

		public SingleAssignmentDisposable Timer
		{
			get
			{
				return _arrayDisposable[0];
			}
			set
			{
				_arrayDisposable[0] = value;
			}
		}

		public SingleAssignmentDisposable Wait
		{
			get
			{
				return _arrayDisposable[1];
			}
			set
			{
				_arrayDisposable[1] = value;
			}
		}

		public LeaveAloneDisposableInfo()
		{
			_arrayDisposable = new SingleAssignmentDisposable[2];
		}

		public void End()
		{
			for (int i = 0; i < 2; i++)
			{
				_arrayDisposable[i]?.Dispose();
				_arrayDisposable[i] = null;
			}
		}
	}

	private class TouchDisposableInfo
	{
		public SingleAssignmentDisposable Wait { get; set; }

		public bool Check => Wait != null;

		public void End()
		{
			Wait?.Dispose();
			Wait = null;
		}
	}

	private class TouchInfo
	{
		private bool _enableCol;

		public GameObject Obj { get; private set; }

		public Collider Col { get; private set; }

		public int Layer { get; private set; }

		public TouchInfo(GameObject obj, Collider col, int layer)
		{
			Obj = obj;
			Col = col;
			_enableCol = Col.enabled;
			Col.enabled = true;
			Layer = obj.layer;
			Obj.layer = layer;
		}

		private void Reset()
		{
			Col.enabled = _enableCol;
			Obj.layer = Layer;
		}
	}

	private class ColDisposableInfo
	{
		private System.Action _onTouch;

		private System.Action _onEnter;

		private System.Action _onExit;

		private SingleAssignmentDisposable _disposableTouch;

		private SingleAssignmentDisposable _disposableEnter;

		private SingleAssignmentDisposable _disposableExit;

		public Collider Col { get; private set; }

		public ColDisposableInfo(Collider col, System.Action onTouch, System.Action onEnter, System.Action onExit)
		{
			Col = col;
			_onTouch = onTouch;
			_onEnter = onEnter;
			_onExit = onExit;
		}

		public void Start()
		{
			End();
			_disposableTouch = new SingleAssignmentDisposable();
			_disposableTouch.Disposable = (from _ in Col.OnMouseDownAsObservable()
				where !EventSystem.current.IsPointerOverGameObject()
				select _).Subscribe(delegate
			{
				if (_onTouch != null)
				{
					_onTouch();
				}
			}).AddTo(Col);
			_disposableEnter = new SingleAssignmentDisposable();
			_disposableEnter.Disposable = (from _ in Col.OnMouseEnterAsObservable()
				where !EventSystem.current.IsPointerOverGameObject()
				select _).Subscribe(delegate
			{
				if (_onEnter != null)
				{
					_onEnter();
				}
			}).AddTo(Col);
			_disposableExit = new SingleAssignmentDisposable();
			_disposableExit.Disposable = (from _ in Col.OnMouseExitAsObservable()
				where !EventSystem.current.IsPointerOverGameObject()
				select _).Subscribe(delegate
			{
				if (_onExit != null)
				{
					_onExit();
				}
			}).AddTo(Col);
		}

		public void End()
		{
			if (_disposableTouch != null)
			{
				_disposableTouch.Dispose();
			}
			_disposableTouch = null;
			if (_disposableEnter != null)
			{
				_disposableEnter.Dispose();
			}
			_disposableEnter = null;
			if (_disposableExit != null)
			{
				_disposableExit.Dispose();
			}
			_disposableExit = null;
		}
	}

	private CommandLabel.CommandInfo[] _labels;

	private CommandLabel.CommandInfo[] _pEventLabels;

	private CommandLabel.CommandInfo[] _pEventFishingLabels;

	private CommandLabel.CommandInfo[] _eventLabels;

	private CommandLabel.CommandInfo[] _sleepLabels;

	private CommandLabel.CommandInfo[] _bathLabels;

	private CommandLabel.CommandInfo[] _isuToiletLabels;

	private CommandLabel.CommandInfo[] _shagamiLabels;

	private CommandLabel.CommandInfo[] _shagamiFoundLabels;

	private CommandLabel.CommandInfo[] _searchLabels;

	private CommandLabel.CommandInfo[] _cookLabels;

	private CommandLabel.CommandInfo[] _standLabels;

	private CommandLabel.CommandInfo[] _mapBathLabels;

	private CommandLabel.CommandInfo[] _holeLabels;

	private CommandLabel.CommandInfo[] _sleepOnanismLabels;

	private CommandLabel.CommandInfo[] _deskBackLabels;

	private CommandLabel.CommandInfo[] _collapseLabels;

	private CommandLabel.CommandInfo[] _coldLabels;

	private CommandLabel.CommandInfo[] _tutorialPassFishingRodLabels;

	private CommandLabel.CommandInfo[] _tutorialFoodRequestLabels;

	private CommandLabel.CommandInfo[] _tutorialWaitAtBaseLabels;

	private CommandLabel.CommandInfo[] _tutorialGrilledFishRequestLabels;

	private CommandLabel.CommandInfo[] _dateLabels;

	private List<TouchInfo> _touchList;

	private List<ColDisposableInfo> _colDisposableList;

	private TouchDisposableInfo _disposableInfo = new TouchDisposableInfo();

	private RaycastHit[] _hits = new RaycastHit[10];

	private ActionPoint _recoveryActionPointFromHScene;

	private Desire.ActionType _recoveryModeFromHScene;

	public bool _canTalkCache = true;

	public int _attitudeIDCache;

	public bool _useNeckLookCache = true;

	public bool _canHCommandCache = true;

	public bool _isSpecialCache;

	public int _hPositionIDCache;

	public int _hPositionSubIDCache;

	public float _obstacleSizeCache;

	private Dictionary<int, System.Action> _presentParameterTable;

	private LeaveAloneDisposableInfo _ladInfo;

	[SerializeField]
	private ActorAnimationAgent _animation;

	[SerializeField]
	private ActorLocomotionAgent _character;

	[SerializeField]
	private AgentController _controller;

	[SerializeField]
	[HideInEditorMode]
	[DisableInPlayMode]
	private Vector3[] _positions;

	private NavMeshPath _pathForCalc;

	private State.Type _stateType = State.Type.Normal;

	[SerializeField]
	private float _impossibleCooldownTime = 3f;

	private float _elapsedTimeFromLastImpossible;

	private bool _isStandby;

	private bool _prevCloseToPlayer;

	private float _distanceTweenPlayer = float.MaxValue;

	private float _heightDistTweenPlayer = float.MaxValue;

	private float _angleDiffTweenPlayer = float.MaxValue;

	[SerializeField]
	private ObjectLayer _layer = ObjectLayer.Command;

	public int SearchAreaID;

	private Desire.ActionType _modeType;

	private Desire.Type _requestedDesire;

	private Dictionary<Actor, bool> _actorConsideredTable = new Dictionary<Actor, bool>();

	private List<ActionPoint> _actionPoints = new List<ActionPoint>();

	private ReadOnlyCollection<ActionPoint> _readonlyActionPoints;

	private Dictionary<ActionPoint, bool> _actionPointCheckFlagTable = new Dictionary<ActionPoint, bool>();

	private Dictionary<int, DateTimeThreshold[]> _desireDailyRhythm;

	[SerializeField]
	[HideInEditorMode]
	[DisableInPlayMode]
	private ActionPoint _targetInSightActionPoint;

	[SerializeField]
	private Threshold _coolTimeThreshold = new Threshold(0f, 1f);

	[SerializeField]
	[HideInEditorMode]
	[DisableInPlayMode]
	private Actor _targetInSightActor;

	[SerializeField]
	[HideInEditorMode]
	[DisableInPlayMode]
	private AnimalBase _targetInSightAnimal;

	private UnityEx.ValueTuple<Desire.ActionType, Desire.ActionType, bool> _modeCache = default(UnityEx.ValueTuple<Desire.ActionType, Desire.ActionType, bool>);

	private static readonly FovBodyPart[] _checkParts = new FovBodyPart[2]
	{
		FovBodyPart.Head,
		FovBodyPart.Body
	};

	private float _maxMotivationInMasturbation;

	private float _runtimeMotivationInMasturbation;

	private List<string> abName;

	private HParticleCtrl HParticle;

	private HSeCtrl Hse;

	private HItemCtrl HItem;

	private GameObject HItemPlace;

	private HSceneManager hSceneManager;

	private HScene hScene;

	private HSceneFlagCtrl hFlagCtrl;

	private HVoiceCtrl HVoiceCtrl;

	private YureCtrl.YureCtrlMapH HYureCtrl;

	private HLayerCtrl HLayerCtrl;

	private int yureID = -1;

	private int HLayerID = -1;

	private int HVoiceID = -1;

	private List<IDisposable> MasturbationDisposables = new List<IDisposable>();

	private float height = -1f;

	private float breast = -1f;

	private IEnumerator _masturbationEnumerator;

	private IDisposable _masturbationDisposable;

	private float _maxMotivationInLesbian;

	private float _runtimeMotivationInLesbian;

	private YureCtrl.YureCtrlMapH[] HYureCtrlLes = new YureCtrl.YureCtrlMapH[2];

	private int[] yureLesID = new int[2] { -1, -1 };

	private int[] HLayerLesID = new int[2] { -1, -1 };

	private int HVoiceLesID = -1;

	private ChaControl[] lesChars = new ChaControl[2];

	private ActorAnimation[] lesCharAnimes = new ActorAnimation[2];

	private List<IDisposable> lesbianDisposable = new List<IDisposable>();

	private float[] timeMotions = new float[2];

	private bool[] enableMotions = new bool[2];

	private bool[] allowMotions = new bool[2] { true, true };

	private Vector2[] lerpMotions = new Vector2[2]
	{
		Vector2.zero,
		Vector2.zero
	};

	private float[] lerpTimes = new float[2];

	protected float[] timeChangeMotions = new float[2];

	protected float[] timeChangeMotionDeltaTimes = new float[2];

	private IEnumerator _lesbianHEnumerator;

	private IDisposable _lesbianHDisposable;

	private IDisposable advEventResetDisposable;

	private List<Waypoint> _reservedWaypoints = new List<Waypoint>();

	private List<Waypoint> _reservedNearActionWaypoints = new List<Waypoint>();

	private List<Waypoint> _reservedDateNearActionWaypoints = new List<Waypoint>();

	private List<Waypoint> _reservedLocationWaypoints = new List<Waypoint>();

	private List<Waypoint> _reservedActorWaypoints = new List<Waypoint>();

	private List<Waypoint> _reservedPlayerWaypoints = new List<Waypoint>();

	private List<Waypoint> _reservedAnimalWaypoints = new List<Waypoint>();

	private static readonly YieldInstruction _waitMinute = new WaitForSeconds(1f);

	private NavMeshPath _calcElementPath;

	private IEnumerator _patrolCoroutine;

	private IEnumerator _calcEnumerator;

	private IEnumerator _additiveCalcProcess;

	private IEnumerator _actionPatrolCoroutine;

	private IEnumerator _actionCalcEnumerator;

	private IEnumerator _additiveActionCalcProcess;

	private IEnumerator _dateActionPatrolCoroutine;

	private IEnumerator _dateActionCalcEnumerator;

	private IEnumerator _additiveDateActionCalcProcess;

	private IEnumerator _locationPatrolCoroutine;

	private IEnumerator _locationCalcEnumerator;

	private IEnumerator _additiveLocationCalcProcess;

	private IEnumerator _actorPatrolCoroutine;

	private IEnumerator _actorCalcEnumerator;

	private IEnumerator _additiveActorCalcProcess;

	private IEnumerator _playerPatrolCoroutine;

	private IEnumerator _playerCalcEnumerator;

	private IEnumerator _additivePlayerCalcProcess;

	private IEnumerator _animalPatrolCoroutine;

	private IEnumerator _animalCalcEnumerator;

	private IEnumerator _additiveAnimalCalcProcess;

	public int charaID
	{
		[CompilerGenerated]
		get
		{
			return AgentData.param.charaID;
		}
	}

	public bool IsAdvEvent
	{
		[CompilerGenerated]
		get
		{
			return !advEventName.IsNullOrEmpty() || CheckFishingEvent();
		}
	}

	private string advEventName { get; set; }

	private AgentAdvEventInfo.Param advEventParam { get; set; }

	public OpenData openData { get; } = new OpenData();


	public PackData packData { get; private set; }

	public CommandLabel.CommandInfo[] Labels
	{
		get
		{
			if (TutorialMode)
			{
				return TutorialType switch
				{
					AIProject.Definitions.Tutorial.ActionType.PassFishingRod => _tutorialPassFishingRodLabels, 
					AIProject.Definitions.Tutorial.ActionType.FoodRequest => _tutorialFoodRequestLabels, 
					AIProject.Definitions.Tutorial.ActionType.WaitAtBase => _tutorialWaitAtBaseLabels, 
					AIProject.Definitions.Tutorial.ActionType.GrilledFishRequest => _tutorialGrilledFishRequestLabels, 
					_ => null, 
				};
			}
			if (Singleton<Manager.Map>.IsInstance() && Singleton<Manager.Map>.Instance.Player.PlayerController.State is Normal)
			{
				PlayerActor player = Singleton<Manager.Map>.Instance.Player;
				switch (Mode)
				{
				case Desire.ActionType.Date:
					return DateLabels;
				case Desire.ActionType.Onbu:
					return null;
				default:
					if (CheckFishingEvent())
					{
						return _pEventFishingLabels;
					}
					if (CheckEventADV(1))
					{
						return _pEventLabels;
					}
					switch (StateType)
					{
					case State.Type.Immobility:
						return null;
					case State.Type.Collapse:
						return _collapseLabels;
					case State.Type.Cold:
						if (!AgentData.SickState.UsedMedicine && !AgentData.SickState.Enabled)
						{
							return _coldLabels;
						}
						return null;
					default:
						if (IsSpecial)
						{
							if (player.ChaControl.sex == 1 && !player.ChaControl.fileParam.futanari)
							{
								if (HPositionID == 1)
								{
									if (Mode == Desire.ActionType.EndTaskSecondSleep || Mode == Desire.ActionType.ShallowSleep)
									{
										return null;
									}
									return _sleepLabels;
								}
								return null;
							}
							switch (HPositionID)
							{
							case 1:
								if (Mode == Desire.ActionType.EndTaskSecondSleep || Mode == Desire.ActionType.ShallowSleep)
								{
									return null;
								}
								return _sleepLabels;
							case 2:
								return _bathLabels;
							case 3:
								return _isuToiletLabels;
							case 4:
								return _shagamiLabels;
							case 5:
								return _shagamiFoundLabels;
							case 6:
								return _searchLabels;
							case 7:
								return _cookLabels;
							case 8:
								return _standLabels;
							case 13:
								return _mapBathLabels;
							case 14:
								return _holeLabels;
							case 16:
								return _sleepOnanismLabels;
							case 17:
								return _deskBackLabels;
							}
						}
						else
						{
							if (CheckEventADV(0))
							{
								return _pEventLabels;
							}
							if (IsStandby)
							{
								return _eventLabels;
							}
							if (CanTalk)
							{
								return _labels;
							}
						}
						return null;
					}
				}
			}
			return null;
		}
	}

	public CommandLabel.CommandInfo[] DateLabels
	{
		get
		{
			switch (StateType)
			{
			case State.Type.Immobility:
				return null;
			case State.Type.Collapse:
				return null;
			case State.Type.Cold:
				if (!AgentData.SickState.UsedMedicine && !AgentData.SickState.Enabled)
				{
					return _coldLabels;
				}
				return null;
			default:
				if (IsSpecial)
				{
					return null;
				}
				if (CanTalk)
				{
					return _dateLabels;
				}
				return null;
			}
		}
	}

	private CommCommandList.CommandInfo[] NormalCommandOptionInfos { get; set; }

	private CommCommandList.CommandInfo[] TalkCommandOptionInfos { get; set; }

	private CommCommandList.CommandInfo[] PraiseCommandOptionInfos { get; set; }

	private CommCommandList.CommandInfo[] InstructionCommandOptionInfos { get; set; }

	private CommCommandList.CommandInfo[] DateCommandOptionInfos { get; set; }

	private CommCommandList.CommandInfo[] DateCommandOptionInfosTP { get; set; }

	private CommCommandList.CommandInfo[] SpecialCommandOptionInfos { get; set; }

	private CommCommandList.CommandInfo[] ColdCommandOptionInfos { get; set; }

	public int TouchCount { get; set; }

	public bool CanTalk { get; set; } = true;


	public int AttitudeID { get; set; }

	public bool UseNeckLook { get; set; } = true;


	public bool CanHCommand { get; set; } = true;


	public bool IsSpecial { get; set; }

	public int HPositionID { get; set; }

	public int HPositionSubID { get; set; }

	private Dictionary<int, System.Action> presentParameterTable => this.GetCache(ref _presentParameterTable, delegate
	{
		Action<FlavorSkill.Type, int> action = delegate(FlavorSkill.Type type, int add)
		{
			Debug.Log($"AddParameter:[{type}:{add}]");
			AgentData.SetFlavorSkill((int)type, base.ChaControl.fileGameInfo.flavorState[(int)type] + add);
		};
		return new Dictionary<int, System.Action>
		{
			[0] = delegate
			{
				action(FlavorSkill.Type.Pheromone, 10);
			},
			[1] = delegate
			{
				action(FlavorSkill.Type.Reliability, 10);
			},
			[2] = delegate
			{
				action(FlavorSkill.Type.Reason, 10);
			},
			[3] = delegate
			{
				action(FlavorSkill.Type.Instinct, 10);
			},
			[4] = delegate
			{
				action(FlavorSkill.Type.Dirty, 10);
			},
			[5] = delegate
			{
				action(FlavorSkill.Type.Wariness, 10);
			},
			[6] = delegate
			{
				action(FlavorSkill.Type.Sociability, 10);
			},
			[7] = delegate
			{
				action(FlavorSkill.Type.Darkness, 10);
			},
			[8] = delegate
			{
				action(FlavorSkill.Type.Pheromone, 50);
			},
			[9] = delegate
			{
				action(FlavorSkill.Type.Reliability, 50);
			},
			[10] = delegate
			{
				action(FlavorSkill.Type.Reason, 50);
			},
			[11] = delegate
			{
				action(FlavorSkill.Type.Instinct, 50);
			},
			[12] = delegate
			{
				action(FlavorSkill.Type.Dirty, 50);
			},
			[13] = delegate
			{
				action(FlavorSkill.Type.Wariness, 50);
			},
			[14] = delegate
			{
				action(FlavorSkill.Type.Sociability, 50);
			},
			[15] = delegate
			{
				action(FlavorSkill.Type.Darkness, 50);
			},
			[16] = delegate
			{
				action(FlavorSkill.Type.Pheromone, 100);
			},
			[17] = delegate
			{
				action(FlavorSkill.Type.Reliability, 100);
			},
			[18] = delegate
			{
				action(FlavorSkill.Type.Reason, 100);
			},
			[19] = delegate
			{
				action(FlavorSkill.Type.Instinct, 100);
			},
			[20] = delegate
			{
				action(FlavorSkill.Type.Dirty, 100);
			},
			[21] = delegate
			{
				action(FlavorSkill.Type.Wariness, 100);
			},
			[22] = delegate
			{
				action(FlavorSkill.Type.Sociability, 100);
			},
			[23] = delegate
			{
				action(FlavorSkill.Type.Darkness, 100);
			},
			[24] = delegate
			{
				action(FlavorSkill.Type.Pheromone, -50);
			},
			[25] = delegate
			{
				action(FlavorSkill.Type.Reliability, -50);
			},
			[26] = delegate
			{
				action(FlavorSkill.Type.Reason, -50);
			},
			[27] = delegate
			{
				action(FlavorSkill.Type.Instinct, -50);
			},
			[28] = delegate
			{
				action(FlavorSkill.Type.Dirty, -50);
			},
			[29] = delegate
			{
				action(FlavorSkill.Type.Wariness, -50);
			},
			[30] = delegate
			{
				action(FlavorSkill.Type.Sociability, -50);
			},
			[31] = delegate
			{
				action(FlavorSkill.Type.Darkness, -50);
			}
		};
	});

	public override string CharaName => base.ChaControl.fileParam.fullname;

	public override ActorAnimation Animation
	{
		[CompilerGenerated]
		get
		{
			return _animation;
		}
	}

	public ActorAnimationAgent AnimationAgent
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
			return _character;
		}
	}

	public ActorLocomotionAgent LocomotorAgent
	{
		[CompilerGenerated]
		get
		{
			return _character;
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

	public AgentController AgentController
	{
		[CompilerGenerated]
		get
		{
			return _controller;
		}
	}

	public bool ReleasableCommand => !IsImpossible;

	public bool IsImpossible { get; private set; }

	public override bool IsNeutralCommand
	{
		get
		{
			if (TutorialMode)
			{
				return TutorialCanTalk;
			}
			if (Mode == Desire.ActionType.SearchActor || Mode == Desire.ActionType.WithPlayer || Mode == Desire.ActionType.SearchPlayerToTalk || Mode == Desire.ActionType.EndTaskTalkToPlayer)
			{
				return false;
			}
			if (IsAdvEvent)
			{
				return true;
			}
			return CanTalk;
		}
	}

	public bool IsEncounterable => StateType == State.Type.Normal || StateType == State.Type.Greet;

	public State.Type StateType
	{
		get
		{
			return _stateType;
		}
		set
		{
			if (_stateType == value)
			{
				return;
			}
			_stateType = value;
			if (Singleton<Manager.Map>.IsInstance())
			{
				CommandArea commandArea = Singleton<Manager.Map>.Instance.Player?.PlayerController?.CommandArea;
				if (commandArea != null && commandArea.ContainsConsiderationObject(this))
				{
					commandArea.RefreshCommands();
				}
			}
		}
	}

	public Transform TargetCommun { get; set; }

	public int SelectedActionID { get; set; }

	public bool IsEvent { get; set; }

	public bool IsStandby
	{
		get
		{
			return _isStandby;
		}
		set
		{
			if (_isStandby != value)
			{
				_isStandby = value;
				Singleton<Manager.Map>.Instance.Player.PlayerController.CommandArea.RefreshCommands();
			}
		}
	}

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
			return _distanceTweenPlayer > rangeSetting.leaveDistance || _heightDistTweenPlayer > rangeSetting.acceptableHeight;
		}
	}

	public bool IsFarPlayerInSurprise
	{
		get
		{
			AgentProfile.RangeParameter rangeSetting = Singleton<Manager.Resources>.Instance.AgentProfile.RangeSetting;
			return _distanceTweenPlayer > rangeSetting.leaveDistanceInSurprise || _heightDistTweenPlayer > rangeSetting.acceptableHeight;
		}
	}

	public bool IsCloseToPlayerByPhotoMode
	{
		get
		{
			AgentProfile.PhotoShotRangeParameter photoShotRangeSetting = Singleton<Manager.Resources>.Instance.AgentProfile.PhotoShotRangeSetting;
			return _distanceTweenPlayer <= photoShotRangeSetting.arriveDistance && _heightDistTweenPlayer <= photoShotRangeSetting.acceptableHeight && _angleDiffTweenPlayer * 2f <= photoShotRangeSetting.sightAngle;
		}
	}

	public bool IsFarPlayerByPhotoMode
	{
		get
		{
			AgentProfile.PhotoShotRangeParameter photoShotRangeSetting = Singleton<Manager.Resources>.Instance.AgentProfile.PhotoShotRangeSetting;
			return photoShotRangeSetting.leaveDistance < _distanceTweenPlayer || photoShotRangeSetting.acceptableHeight < _heightDistTweenPlayer || photoShotRangeSetting.invisibleAngle < _angleDiffTweenPlayer * 2f;
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

	public AgentBehaviorTree NormalBehaviorTree { get; private set; }

	public override Desire.ActionType Mode
	{
		get
		{
			return _modeType;
		}
		set
		{
			if (_modeType != value)
			{
				_modeType = value;
			}
		}
	}

	public Desire.ActionType PrevMode { get; set; }

	public Desire.ActionType PrevActionMode { get; set; }

	public Desire.ActionType ReservedMode { get; set; }

	public AIProject.Definitions.Tutorial.ActionType TutorialType { get; set; }

	public bool TutorialMode { get; set; }

	public bool TutorialCanTalk { get; set; }

	public int TutorialLocomoCaseID { get; set; }

	public Actor CommandPartner { get; set; }

	public BehaviorTreeResources BehaviorResources { get; private set; }

	protected TutorialBehaviorTreeResources TutorialBehaviorResources { get; private set; }

	public CommandType CommandType { get; }

	public override ICharacterInfo TiedInfo
	{
		[CompilerGenerated]
		get
		{
			return AgentData;
		}
	}

	public AgentData AgentData { get; set; }

	public Desire.Type ScheduledDesire { get; private set; }

	public Desire.Type RuntimeDesire { get; set; }

	public Desire.Type RequestedDesire
	{
		get
		{
			if (!Desire.ModeTable.TryGetValue(_modeType, out _requestedDesire))
			{
				_requestedDesire = Desire.Type.None;
			}
			return _requestedDesire;
		}
	}

	public List<Actor> TargetActors { get; private set; } = new List<Actor>();


	public ReadOnlyCollection<ActionPoint> ActionPoints
	{
		get
		{
			if (_readonlyActionPoints == null)
			{
				_readonlyActionPoints = new ReadOnlyCollection<ActionPoint>(_actionPoints);
			}
			return _readonlyActionPoints;
		}
	}

	public ActionPoint[] SearchTargets { get; private set; } = Array.Empty<ActionPoint>();


	public List<AnimalBase> TargetAnimals { get; private set; } = new List<AnimalBase>();


	public IVisible TargetVisual { get; set; }

	public ActionPoint TargetInSightActionPoint
	{
		get
		{
			return _targetInSightActionPoint;
		}
		set
		{
			_targetInSightActionPoint = value;
		}
	}

	public Vector3? DestPosition { get; set; }

	public ActionPoint BookingActionPoint { get; set; }

	public StoryPoint TargetStoryPoint { get; set; }

	public OffMeshLink TargetOffMeshLink { get; set; }

	public OffMeshLink NearOffMeshLink
	{
		get
		{
			OffMeshLink offMeshLink = null;
			if (_navMeshAgent.currentOffMeshLinkData.activated)
			{
				offMeshLink = _navMeshAgent.currentOffMeshLinkData.offMeshLink;
			}
			if (offMeshLink == null && _navMeshAgent.nextOffMeshLinkData.activated)
			{
				offMeshLink = _navMeshAgent.nextOffMeshLinkData.offMeshLink;
			}
			return offMeshLink;
		}
	}

	public ActionPoint PrevActionPoint { get; set; }

	public bool UpdateMotivation { get; set; }

	public float MotivationInEncounter { get; set; }

	public float RuntimeMotivationInPhoto { get; set; }

	public Actor TargetInSightActor
	{
		get
		{
			return _targetInSightActor;
		}
		set
		{
			_targetInSightActor = value;
		}
	}

	public PlayerActor Pair { get; set; }

	public AnimalBase TargetInSightAnimal
	{
		get
		{
			return _targetInSightAnimal;
		}
		set
		{
			_targetInSightAnimal = value;
		}
	}

	public float AnimalFovAngleOffsetY { get; set; }

	public Dictionary<int, CollisionState> ActionPointCollisionStateTable { get; private set; } = new Dictionary<int, CollisionState>();


	public Dictionary<int, CollisionState> ActorCollisionStateTable { get; private set; } = new Dictionary<int, CollisionState>();


	public Dictionary<int, CollisionState> ActorFarCollisionStateTable { get; private set; } = new Dictionary<int, CollisionState>();


	public Dictionary<int, CollisionState> AnimalCollisionStateTable { get; private set; } = new Dictionary<int, CollisionState>();


	public PoseKeyPair? SurprisePoseID { get; set; }

	public bool IsMasturbating { get; set; }

	public bool SleepTrigger { get; set; }

	public bool SuccessCook { get; set; }

	private Vector3 LocalTargetPosition { get; set; }

	public Quaternion LocalTargetRotation { get; set; }

	public float MaxMotivationInMasturbation
	{
		[CompilerGenerated]
		get
		{
			return _maxMotivationInMasturbation;
		}
	}

	public float RuntimeMotivationInMasturbation
	{
		[CompilerGenerated]
		get
		{
			return _runtimeMotivationInMasturbation;
		}
	}

	public bool PlayingMasturbationSequence => _masturbationEnumerator != null;

	public float MaxMotivationInLesbian
	{
		[CompilerGenerated]
		get
		{
			return _maxMotivationInLesbian;
		}
	}

	public float RuntimeMotivationInLesbian
	{
		[CompilerGenerated]
		get
		{
			return _runtimeMotivationInLesbian;
		}
	}

	public bool LivesLesbianHSequence => _lesbianHEnumerator != null;

	public Queue<Waypoint> WalkRoute { get; private set; } = new Queue<Waypoint>();


	public Queue<Waypoint> SearchActionRoute { get; private set; } = new Queue<Waypoint>();


	public Queue<Waypoint> SearchDateActionRoute { get; private set; } = new Queue<Waypoint>();


	public Queue<Waypoint> SearchLocationRoute { get; private set; } = new Queue<Waypoint>();


	public Queue<Waypoint> SearchActorRoute { get; private set; } = new Queue<Waypoint>();


	public Queue<Waypoint> SearchPlayerRoute { get; private set; } = new Queue<Waypoint>();


	public Queue<Waypoint> SearchAnimalRoute { get; private set; } = new Queue<Waypoint>();


	public Waypoint DestWaypoint { get; private set; }

	public bool IsRunning { get; set; }

	public bool LivesPatrol
	{
		[CompilerGenerated]
		get
		{
			return _patrolCoroutine != null;
		}
	}

	public bool LivesCalc
	{
		[CompilerGenerated]
		get
		{
			return _calcEnumerator != null;
		}
	}

	public bool LivesActionPatrol
	{
		[CompilerGenerated]
		get
		{
			return _actionPatrolCoroutine != null;
		}
	}

	public bool LivesActionCalc
	{
		[CompilerGenerated]
		get
		{
			return _actionCalcEnumerator != null;
		}
	}

	public bool LivesDateActionPatrol
	{
		[CompilerGenerated]
		get
		{
			return _dateActionPatrolCoroutine != null;
		}
	}

	public bool LivesDateActionCalc
	{
		[CompilerGenerated]
		get
		{
			return _dateActionCalcEnumerator != null;
		}
	}

	public bool LivesLocationPatrol
	{
		[CompilerGenerated]
		get
		{
			return _locationPatrolCoroutine != null;
		}
	}

	public bool LivesLocationCalc
	{
		[CompilerGenerated]
		get
		{
			return _locationCalcEnumerator != null;
		}
	}

	public bool LivesActorPatrol
	{
		[CompilerGenerated]
		get
		{
			return _actorPatrolCoroutine != null;
		}
	}

	public bool LivesActorCalc
	{
		[CompilerGenerated]
		get
		{
			return _actorCalcEnumerator != null;
		}
	}

	public bool LivesPlayerPatrol
	{
		[CompilerGenerated]
		get
		{
			return _playerPatrolCoroutine != null;
		}
	}

	public bool LivesPlayerCalc
	{
		[CompilerGenerated]
		get
		{
			return _playerCalcEnumerator != null;
		}
	}

	public bool SearchAnimalEmpty { get; set; }

	public bool LivesAnimalPatrol
	{
		[CompilerGenerated]
		get
		{
			return _animalPatrolCoroutine != null;
		}
	}

	public bool LivesAnimalCalc
	{
		[CompilerGenerated]
		get
		{
			return _animalCalcEnumerator != null;
		}
	}

	public bool CanGreet
	{
		get
		{
			AgentData agentData = AgentData;
			if (agentData.Greeted)
			{
				return false;
			}
			int num = base.ChaControl.fileGameInfo.flavorState[1];
			if ((float)num < Singleton<Manager.Resources>.Instance.StatusProfile.CanGreetBorder)
			{
				return false;
			}
			float num2 = agentData.StatsTable[1];
			if (num2 < base.ChaControl.fileGameInfo.moodBound.upper)
			{
				return false;
			}
			return true;
		}
	}

	public bool CanScrounge
	{
		get
		{
			int phase = base.ChaControl.fileGameInfo.phase;
			return phase >= 2 && !AgentData.ItemScrounge.isEvent;
		}
	}

	public bool CanPresent
	{
		get
		{
			int phase = base.ChaControl.fileGameInfo.phase;
			return phase >= 2;
		}
	}

	public bool CanBirthdayPresent => !IsBadMood() && Singleton<Manager.Map>.Instance.Player.IsBirthday(this);

	public bool IsHealthManager
	{
		get
		{
			bool flag = base.ChaControl.fileGameInfo.normalSkill.ContainsValue(28);
			float num = AgentData.StatsTable[3];
			bool flag2 = num < Singleton<Manager.Resources>.Instance.StatusProfile.HealthyPhysicalBorder;
			if (flag && flag2)
			{
				return true;
			}
			return false;
		}
	}

	public bool CanWashFace
	{
		get
		{
			int num = base.ChaControl.fileGameInfo.flavorState[2];
			int washFaceBorder = Singleton<Manager.Resources>.Instance.StatusProfile.WashFaceBorder;
			return num >= washFaceBorder;
		}
	}

	public bool CanClothChange
	{
		get
		{
			int num = base.ChaControl.fileGameInfo.flavorState[0];
			float canClothChangeBorder = Singleton<Manager.Resources>.Instance.StatusProfile.CanClothChangeBorder;
			return (float)num >= canClothChangeBorder;
		}
	}

	public bool ShouldRestoreCloth
	{
		get
		{
			bool isOtherCoordinate = AgentData.IsOtherCoordinate;
			DateTime now = Singleton<Manager.Map>.Instance.Simulator.Now;
			DateTime time = Singleton<Manager.Resources>.Instance.StatusProfile.ShouldRestoreCoordTime.Time;
			float restoreRangeMinuteTime = Singleton<Manager.Resources>.Instance.StatusProfile.RestoreRangeMinuteTime;
			float num = Mathf.Abs((float)(time - now).TotalMinutes);
			bool flag = num < restoreRangeMinuteTime;
			return isOtherCoordinate && flag;
		}
	}

	public bool CanGirlsAction
	{
		get
		{
			int num = base.ChaControl.fileGameInfo.flavorState[7];
			int girlsActionBorder = Singleton<Manager.Resources>.Instance.StatusProfile.GirlsActionBorder;
			return num >= girlsActionBorder;
		}
	}

	public bool CanCauseHurt
	{
		get
		{
			if (AgentData.SickState.ID != -1)
			{
				return false;
			}
			int flavor = base.ChaControl.fileGameInfo.flavorState[5];
			StatusProfile statusProfile = Singleton<Manager.Resources>.Instance.StatusProfile;
			float num = FlavorVariation(statusProfile.HurtRateBuffMinMax, statusProfile.HurtRateBuff, flavor);
			float num2 = statusProfile.HurtDefaultIncidence + num;
			float num3 = UnityEngine.Random.Range(0f, 100f);
			bool flag = num3 <= num2;
			bool flag2 = base.AreaType == MapArea.AreaType.Normal;
			return flag && flag2;
		}
	}

	public bool CanInvitation
	{
		get
		{
			int num = base.ChaControl.fileGameInfo.flavorState[4];
			int invitationBorder = Singleton<Manager.Resources>.Instance.StatusProfile.InvitationBorder;
			bool flag = num >= invitationBorder;
			return Game.isAdd01 && flag;
		}
	}

	public bool CanMasturbation
	{
		get
		{
			int num = base.ChaControl.fileGameInfo.flavorState[4];
			int masturbationBorder = Singleton<Manager.Resources>.Instance.StatusProfile.MasturbationBorder;
			return Game.isAdd01 && num >= masturbationBorder;
		}
	}

	public bool CanLesbian
	{
		get
		{
			if (!Game.isAdd01)
			{
				return false;
			}
			if (TargetInSightActor == null)
			{
				return false;
			}
			bool flag = base.ChaControl.fileGameInfo.hSkill.ContainsValue(12);
			int iD = TargetInSightActor.ID;
			if (!AgentData.FriendlyRelationShipTable.TryGetValue(iD, out var value))
			{
				int num = 50;
				AgentData.FriendlyRelationShipTable[iD] = num;
				value = num;
			}
			StatusProfile statusProfile = Singleton<Manager.Resources>.Instance.StatusProfile;
			bool flag2 = value >= statusProfile.LesbianFriendlyRelationBorder;
			if (flag && flag2)
			{
				float num2 = UnityEngine.Random.Range(0f, 100f);
				if (num2 <= statusProfile.LesbianRate)
				{
					return true;
				}
			}
			return false;
		}
	}

	public bool IsEmptyWeaknessMotivation => AgentData.WeaknessMotivation <= 0f;

	public bool FromFemale { get; set; }

	public bool IsBadMood()
	{
		if (base.ChaControl.fileGameInfo.phase < 2)
		{
			AgentData.StatsTable[1] = (base.ChaControl.fileGameInfo.moodBound.lower + base.ChaControl.fileGameInfo.moodBound.upper) / 2f;
		}
		return AgentData.StatsTable[1] <= base.ChaControl.fileGameInfo.moodBound.lower;
	}

	public bool IsGoodMood()
	{
		if (base.ChaControl.fileGameInfo.phase < 2)
		{
			AgentData.StatsTable[1] = (base.ChaControl.fileGameInfo.moodBound.lower + base.ChaControl.fileGameInfo.moodBound.upper) / 2f;
		}
		return AgentData.StatsTable[1] > base.ChaControl.fileGameInfo.moodBound.upper;
	}

	public bool CheckEventADV(int eventType)
	{
		advEventParam = GetAdvEvent(eventType);
		advEventName = advEventParam?.FileName;
		return !advEventName.IsNullOrEmpty();
	}

	public void ResetActionFlag()
	{
		UseNeckLook = true;
		CanTalk = true;
		CanHCommand = true;
		AttitudeID = 0;
		IsSpecial = false;
		HPositionID = 0;
		HPositionSubID = 0;
		if (_navMeshObstacle != null)
		{
			_navMeshObstacle.radius = 2f;
		}
	}

	public void LoadActionFlag(int actionID, int poseID)
	{
		if (Singleton<Manager.Resources>.Instance.Action.AgentActionFlagTable.TryGetValue(actionID, out var value) && value.TryGetValue(poseID, out var value2))
		{
			UseNeckLook = value2.useNeckLook;
			CanTalk = value2.canTalk;
			AttitudeID = value2.attitudeID;
			CanHCommand = value2.canHCommand;
			IsSpecial = value2.isSpecial;
			HPositionID = value2.hPositionID1;
			HPositionSubID = value2.hPositionID2;
			if (_navMeshObstacle != null)
			{
				_navMeshObstacle.radius = value2.obstacleRadius;
			}
		}
		else
		{
			UseNeckLook = false;
			CanTalk = false;
			AttitudeID = -1;
			CanHCommand = false;
			IsSpecial = false;
			HPositionID = -1;
			HPositionSubID = -1;
			if (_navMeshObstacle != null)
			{
				_navMeshObstacle.radius = 2f;
			}
		}
	}

	public void DisableActionFlag()
	{
		UseNeckLook = false;
		CanTalk = false;
		AttitudeID = 0;
		CanHCommand = false;
		IsSpecial = false;
		HPositionID = -1;
		HPositionSubID = -1;
		if (_navMeshObstacle != null)
		{
			_navMeshObstacle.radius = 2f;
		}
	}

	public bool WaitTutorialUIDisplay()
	{
		bool? flag = (((!Singleton<Manager.Map>.IsInstance()) ? null : Singleton<Manager.Map>.Instance.Player)?.CameraControl?.CinemachineBrain)?.IsBlending;
		if (!flag.HasValue || !flag.Value)
		{
			bool? flag2 = Animation?.PlayingTurnAnimation;
			if (!flag2.HasValue || !flag2.Value)
			{
				AudioSource audioSource = base.ChaControl?.asVoice;
				if (audioSource == null)
				{
					return false;
				}
				return !audioSource.loop;
			}
		}
		return true;
	}

	private unsafe void InitCommands()
	{
		Transform transform = base.ChaControl.transform.FindLoop("cf_J_Mune00")?.transform;
		CommonDefine.CommonIconGroup icon = Singleton<Manager.Resources>.Instance.CommonDefine.Icon;
		Singleton<Manager.Resources>.Instance.itemIconTables.ActorIconTable.TryGetValue(ID, out var value);
		_labels = new CommandLabel.CommandInfo[1]
		{
			new CommandLabel.CommandInfo
			{
				OnText = () => $"{CharaName}と話す",
				Text = $"{CharaName}と話す",
				Icon = value,
				TargetSpriteInfo = icon.CharaSpriteInfo,
				Transform = transform,
				CoolTimeFillRate = delegate
				{
					bool lockTalk3 = AgentData.LockTalk;
					float talkElapsedTime3 = AgentData.TalkElapsedTime;
					float talkLockDuration3 = Singleton<Manager.Resources>.Instance.AgentProfile.TalkLockDuration;
					if (lockTalk3)
					{
						float num4 = Mathf.Clamp(talkElapsedTime3 / talkLockDuration3, 0f, 1f);
						return 1f - num4;
					}
					return 0f;
				},
				Event = delegate
				{
					if (CheckStealEvent())
					{
						StartStealEvent();
					}
					else if (CanProgressPhase())
					{
						StartPhaseEvent();
					}
					else if (CheckCatEvent())
					{
						StartCatEvent();
					}
					else if (AgentData.LockTalk)
					{
						StartCommunication();
						Observable.EveryUpdate().Skip(1).SkipWhile((long _) => Animation.PlayingTurnAnimation)
							.Take(1)
							.Subscribe(delegate
							{
								openData.FindLoad("10", charaID, 0);
								packData.onComplete = delegate
								{
									packData.restoreCommands = null;
									EndCommunication();
								};
								Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
							});
					}
					else
					{
						StartCommunication();
						if (AgentData.SickState.ID == 0)
						{
							openData.FindLoad("9", charaID, 4);
						}
						else if (AgentData.SickState.ID == 4)
						{
							openData.FindLoad("11", charaID, 4);
						}
						else
						{
							openData.FindLoad("0", charaID, 4);
						}
						packData.onComplete = delegate
						{
							int tutorialID = 4;
							bool flag11 = Singleton<MapUIContainer>.IsInstance() && !MapUIContainer.GetTutorialOpenState(tutorialID);
							packData.restoreCommands = null;
							if (!flag11)
							{
								PopupCommands(NormalCommandOptionInfos, delegate
								{
									StartCommonSelection();
									MapUIContainer.CommandList.OnOpened = null;
								});
							}
							else
							{
								Observable.EveryLateUpdate().SkipWhile((long _) => WaitTutorialUIDisplay()).Take(1)
									.Delay(TimeSpan.FromSeconds(0.5))
									.Subscribe(delegate
									{
										MapUIContainer.TutorialUI.ClosedEvent = delegate
										{
											PopupCommands(NormalCommandOptionInfos, delegate
											{
												StartCommonSelection();
												MapUIContainer.CommandList.OnOpened = null;
											});
										};
										MapUIContainer.OpenTutorialUI(tutorialID);
									});
							}
						};
						Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
					}
				}
			}
		};
		_pEventLabels = new CommandLabel.CommandInfo[1]
		{
			new CommandLabel.CommandInfo
			{
				OnText = () => $"{CharaName}と話す",
				Text = $"{CharaName}と話す",
				Icon = value,
				TargetSpriteInfo = icon.CharaSpriteInfo,
				Transform = transform,
				CoolTimeFillRate = delegate
				{
					bool lockTalk2 = AgentData.LockTalk;
					float talkElapsedTime2 = AgentData.TalkElapsedTime;
					float talkLockDuration2 = Singleton<Manager.Resources>.Instance.AgentProfile.TalkLockDuration;
					if (lockTalk2)
					{
						float num3 = Mathf.Clamp(talkElapsedTime2 / talkLockDuration2, 0f, 1f);
						return 1f - num3;
					}
					return 0f;
				},
				Event = delegate
				{
					AdvEventStart(advEventName);
				}
			}
		};
		_pEventFishingLabels = new CommandLabel.CommandInfo[1]
		{
			new CommandLabel.CommandInfo
			{
				OnText = () => $"{CharaName}と話す",
				Text = $"{CharaName}と話す",
				Icon = value,
				TargetSpriteInfo = icon.CharaSpriteInfo,
				Transform = transform,
				CoolTimeFillRate = delegate
				{
					bool lockTalk = AgentData.LockTalk;
					float talkElapsedTime = AgentData.TalkElapsedTime;
					float talkLockDuration = Singleton<Manager.Resources>.Instance.AgentProfile.TalkLockDuration;
					if (lockTalk)
					{
						float num2 = Mathf.Clamp(talkElapsedTime / talkLockDuration, 0f, 1f);
						return 1f - num2;
					}
					return 0f;
				},
				Event = delegate
				{
					FishingEvent();
				}
			}
		};
		_eventLabels = new CommandLabel.CommandInfo[1]
		{
			new CommandLabel.CommandInfo
			{
				OnText = () => $"{CharaName}と話す",
				Text = $"{CharaName}と話す",
				Icon = value,
				TargetSpriteInfo = icon.CharaSpriteInfo,
				Transform = transform,
				Event = delegate
				{
					StartEvent();
				}
			}
		};
		_sleepLabels = new CommandLabel.CommandInfo[1]
		{
			new CommandLabel.CommandInfo
			{
				OnText = () => $"{CharaName}にアクション",
				Text = $"{CharaName}にアクション",
				Icon = value,
				TargetSpriteInfo = icon.CharaSpriteInfo,
				Transform = transform,
				Event = delegate
				{
					StartCommunication();
					openData.FindLoad("1", charaID, 4);
					packData.onComplete = delegate
					{
						packData.restoreCommands = null;
						PopupCommands(SpecialCommandOptionInfos);
					};
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
			}
		};
		_bathLabels = new CommandLabel.CommandInfo[1]
		{
			new CommandLabel.CommandInfo
			{
				OnText = () => $"{CharaName}にアクション",
				Text = $"{CharaName}にアクション",
				Icon = value,
				TargetSpriteInfo = icon.CharaSpriteInfo,
				Transform = transform,
				Event = delegate
				{
					StartCommunication();
					openData.FindLoad("2", charaID, 4);
					packData.onComplete = delegate
					{
						packData.restoreCommands = null;
						PopupCommands(SpecialCommandOptionInfos);
					};
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
			}
		};
		_isuToiletLabels = new CommandLabel.CommandInfo[1]
		{
			new CommandLabel.CommandInfo
			{
				OnText = () => $"{CharaName}にアクション",
				Text = $"{CharaName}にアクション",
				Icon = value,
				TargetSpriteInfo = icon.CharaSpriteInfo,
				Transform = transform,
				Event = delegate
				{
					StartCommunication();
					openData.FindLoad("3", charaID, 4);
					packData.onComplete = delegate
					{
						packData.restoreCommands = null;
						PopupCommands(SpecialCommandOptionInfos);
					};
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
			}
		};
		_shagamiLabels = new CommandLabel.CommandInfo[1]
		{
			new CommandLabel.CommandInfo
			{
				OnText = () => $"{CharaName}にアクション",
				Text = $"{CharaName}にアクション",
				Icon = value,
				TargetSpriteInfo = icon.CharaSpriteInfo,
				Transform = transform,
				Event = delegate
				{
					StartCommunication();
					openData.FindLoad("4", charaID, 4);
					packData.onComplete = delegate
					{
						packData.restoreCommands = null;
						PopupCommands(SpecialCommandOptionInfos);
					};
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
			}
		};
		_shagamiFoundLabels = new CommandLabel.CommandInfo[1]
		{
			new CommandLabel.CommandInfo
			{
				OnText = () => $"{CharaName}にアクション",
				Text = $"{CharaName}にアクション",
				Icon = value,
				TargetSpriteInfo = icon.CharaSpriteInfo,
				Transform = transform,
				Event = delegate
				{
					StartCommunication();
					openData.FindLoad("5", charaID, 4);
					packData.onComplete = delegate
					{
						packData.restoreCommands = null;
						PopupCommands(SpecialCommandOptionInfos);
					};
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
			}
		};
		_searchLabels = new CommandLabel.CommandInfo[1]
		{
			new CommandLabel.CommandInfo
			{
				OnText = () => $"{CharaName}にアクション",
				Text = $"{CharaName}にアクション",
				Icon = value,
				TargetSpriteInfo = icon.CharaSpriteInfo,
				Transform = transform,
				Event = delegate
				{
					StartCommunication();
					openData.FindLoad("6", charaID, 4);
					packData.onComplete = delegate
					{
						packData.restoreCommands = null;
						PopupCommands(SpecialCommandOptionInfos);
					};
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
			}
		};
		_cookLabels = new CommandLabel.CommandInfo[1]
		{
			new CommandLabel.CommandInfo
			{
				OnText = () => $"{CharaName}にアクション",
				Text = $"{CharaName}にアクション",
				Icon = value,
				TargetSpriteInfo = icon.CharaSpriteInfo,
				Transform = transform,
				Event = delegate
				{
					StartCommunication();
					openData.FindLoad("7", charaID, 4);
					packData.onComplete = delegate
					{
						packData.restoreCommands = null;
						PopupCommands(SpecialCommandOptionInfos);
					};
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
			}
		};
		_standLabels = new CommandLabel.CommandInfo[1]
		{
			new CommandLabel.CommandInfo
			{
				OnText = () => $"{CharaName}にアクション",
				Text = $"{CharaName}にアクション",
				Icon = value,
				TargetSpriteInfo = icon.CharaSpriteInfo,
				Transform = transform,
				Event = delegate
				{
					StartCommunication();
					openData.FindLoad("8", charaID, 4);
					packData.onComplete = delegate
					{
						packData.restoreCommands = null;
						PopupCommands(SpecialCommandOptionInfos);
					};
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
			}
		};
		_mapBathLabels = new CommandLabel.CommandInfo[1]
		{
			new CommandLabel.CommandInfo
			{
				OnText = () => $"{CharaName}にアクション",
				Text = $"{CharaName}にアクション",
				Icon = value,
				TargetSpriteInfo = icon.CharaSpriteInfo,
				Transform = transform,
				Event = delegate
				{
					StartCommunication();
					openData.FindLoad("2", charaID, 4);
					packData.onComplete = delegate
					{
						packData.restoreCommands = null;
						PopupCommands(SpecialCommandOptionInfos);
					};
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
			}
		};
		_holeLabels = new CommandLabel.CommandInfo[1]
		{
			new CommandLabel.CommandInfo
			{
				OnText = () => $"{CharaName}にアクション",
				Text = $"{CharaName}にアクション",
				Icon = value,
				TargetSpriteInfo = icon.CharaSpriteInfo,
				Transform = transform,
				Event = delegate
				{
					StartCommunication();
					openData.FindLoad("6", charaID, 4);
					packData.onComplete = delegate
					{
						packData.restoreCommands = null;
						PopupCommands(SpecialCommandOptionInfos);
					};
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
			}
		};
		_sleepOnanismLabels = new CommandLabel.CommandInfo[1]
		{
			new CommandLabel.CommandInfo
			{
				OnText = () => $"{CharaName}にアクション",
				Text = $"{CharaName}にアクション",
				Icon = value,
				TargetSpriteInfo = icon.CharaSpriteInfo,
				Transform = transform,
				Event = delegate
				{
					StartCommunication();
					openData.FindLoad("6", charaID, 4);
					packData.onComplete = delegate
					{
						packData.restoreCommands = null;
						PopupCommands(SpecialCommandOptionInfos);
					};
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
			}
		};
		_deskBackLabels = new CommandLabel.CommandInfo[1]
		{
			new CommandLabel.CommandInfo
			{
				OnText = () => $"{CharaName}にアクション",
				Text = $"{CharaName}にアクション",
				Icon = value,
				TargetSpriteInfo = icon.CharaSpriteInfo,
				Transform = transform,
				Event = delegate
				{
					StartCommunication();
					openData.FindLoad("6", charaID, 4);
					packData.onComplete = delegate
					{
						packData.restoreCommands = null;
						PopupCommands(SpecialCommandOptionInfos);
					};
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
			}
		};
		_collapseLabels = new CommandLabel.CommandInfo[1]
		{
			new CommandLabel.CommandInfo
			{
				OnText = () => $"{CharaName}を担ぐ",
				Text = $"{CharaName}を担ぐ",
				Icon = value,
				IsHold = true,
				TargetSpriteInfo = icon.CharaSpriteInfo,
				Transform = transform,
				Event = delegate
				{
					Singleton<Manager.Map>.Instance.Player.Partner = this;
					PrevMode = Mode;
					Mode = Desire.ActionType.Onbu;
					Singleton<Manager.Map>.Instance.Player.Controller.ChangeState("Onbu");
				}
			}
		};
		_coldLabels = new CommandLabel.CommandInfo[1]
		{
			new CommandLabel.CommandInfo
			{
				OnText = () => $"{CharaName}に話しかける",
				Text = $"{CharaName}に話しかける",
				Icon = value,
				TargetSpriteInfo = icon.CharaSpriteInfo,
				Transform = transform,
				Event = delegate
				{
					StartCommunication();
					openData.FindLoad("10", charaID, 4);
					packData.onComplete = delegate
					{
						packData.restoreCommands = null;
						PopupCommands(ColdCommandOptionInfos);
					};
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
			}
		};
		_dateLabels = new CommandLabel.CommandInfo[1]
		{
			new CommandLabel.CommandInfo
			{
				OnText = () => $"{CharaName}と話す",
				Text = $"{CharaName}と話す",
				Icon = value,
				TargetSpriteInfo = icon.CharaSpriteInfo,
				Transform = transform,
				Event = delegate
				{
					StartDateCommunication();
					openData.FindLoad("12", charaID, 4);
					packData.onComplete = delegate
					{
						packData.restoreCommands = null;
						PopupDateCommands(Singleton<Manager.Map>.Instance.Player.Partner == this);
					};
					packData.isThisPartner = Singleton<Manager.Map>.Instance.Player.Partner == this;
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
			}
		};
		_tutorialPassFishingRodLabels = new CommandLabel.CommandInfo[1]
		{
			new CommandLabel.CommandInfo
			{
				OnText = () => $"{CharaName}と話す",
				Text = $"{CharaName}と話す",
				Icon = value,
				TargetSpriteInfo = icon.CharaSpriteInfo,
				Transform = transform,
				Event = delegate
				{
					StartTutorialADV(1);
				}
			}
		};
		_tutorialFoodRequestLabels = new CommandLabel.CommandInfo[1]
		{
			new CommandLabel.CommandInfo
			{
				OnText = () => $"{CharaName}と話す",
				Text = $"{CharaName}と話す",
				Icon = value,
				TargetSpriteInfo = icon.CharaSpriteInfo,
				Transform = transform,
				Event = delegate
				{
					StartTutorialADV(2);
				}
			}
		};
		_tutorialWaitAtBaseLabels = new CommandLabel.CommandInfo[1]
		{
			new CommandLabel.CommandInfo
			{
				OnText = () => $"{CharaName}と話す",
				Text = $"{CharaName}と話す",
				Icon = value,
				TargetSpriteInfo = icon.CharaSpriteInfo,
				Transform = transform,
				Event = delegate
				{
					StartTutorialADV(3);
				}
			}
		};
		_tutorialGrilledFishRequestLabels = new CommandLabel.CommandInfo[1]
		{
			new CommandLabel.CommandInfo
			{
				OnText = () => $"{CharaName}と話す",
				Text = $"{CharaName}と話す",
				Icon = value,
				TargetSpriteInfo = icon.CharaSpriteInfo,
				Transform = transform,
				Event = delegate
				{
					StartTutorialADV(4);
				}
			}
		};
		NormalCommandOptionInfos = new CommCommandList.CommandInfo[10]
		{
			new CommCommandList.CommandInfo("トーク")
			{
				Condition = null,
				Event = delegate
				{
					ChangeCommandList(TalkCommandOptionInfos, "トーク", delegate
					{
						BackCommandList(NormalCommandOptionInfos, CharaName, null);
					});
				}
			},
			new CommCommandList.CommandInfo("アドバイスする")
			{
				Condition = null,
				Event = delegate
				{
					ChangeCommandList(InstructionCommandOptionInfos, "アドバイス", delegate
					{
						BackCommandList(NormalCommandOptionInfos, CharaName, null);
					});
				}
			},
			new CommCommandList.CommandInfo("アイテムを渡す")
			{
				Condition = null,
				Event = delegate
				{
					PresentADV(NormalCommandOptionInfos);
				}
			},
			new CommCommandList.CommandInfo("頼まれもの")
			{
				Condition = () => AgentData.ItemScrounge.isEvent,
				IsSpecial = true,
				Timer = () => AgentData.ItemScrounge.remainingTime,
				Event = delegate
				{
					StartScroungeEvent(NormalCommandOptionInfos);
				}
			},
			new CommCommandList.CommandInfo("薬をあげる")
			{
				Condition = delegate
				{
					AIProject.SaveData.Sickness sickState4 = AgentData.SickState;
					return sickState4.ID != 0;
				},
				Event = delegate
				{
					StartSicknessEvent(0);
				}
			},
			new CommCommandList.CommandInfo("薬をあげる")
			{
				Condition = delegate
				{
					AIProject.SaveData.Sickness sickState3 = AgentData.SickState;
					return sickState3.ID == 0;
				},
				Event = delegate
				{
					StartSicknessEvent(0);
				}
			},
			new CommCommandList.CommandInfo("ついてきて")
			{
				Condition = () => !IsBadMood(),
				Event = delegate(int x)
				{
					EndCommonSelection();
					Animation.StopAllAnimCoroutine();
					openData.FindLoad("5", charaID, 0);
					packData.onComplete = delegate
					{
						if (packData.isSuccessFollow)
						{
							ResetActionFlag();
							if (base.CurrentPoint != null)
							{
								Animation.CrossFadeScreen();
								base.CurrentPoint.SetActiveMapItemObjs(active: true);
								base.CurrentPoint.CreateByproduct(base.ActionID, base.PoseID);
								base.CurrentPoint.ReleaseSlot(this);
								base.CurrentPoint = null;
								ActivateNavMeshAgent();
							}
							ActivatePairing(x, isDate: true);
							bool flag9 = base.ChaControl.fileGameInfo.phase >= 2;
							bool flag10 = base.ChaControl.fileGameInfo.flavorState[1] >= Singleton<Manager.Resources>.Instance.StatusProfile.HoldingHandBorderReliability;
							if (flag9 && flag10)
							{
								ActivateHoldingHands(x, enabled: true);
							}
							packData.restoreCommands = null;
							EndCommunication();
						}
						else
						{
							CheckTalkForceEnd(NormalCommandOptionInfos);
						}
					};
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
			},
			new CommCommandList.CommandInfo("エッチがしたい")
			{
				Condition = delegate
				{
					ChaControl chaControl5 = Singleton<Manager.Map>.Instance.Player.ChaControl;
					if (chaControl5.sex == 1 && !chaControl5.fileParam.futanari)
					{
						string[] lesTypeHMeshTag2 = Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.LesTypeHMeshTag;
						return CanSelectHCommand(lesTypeHMeshTag2) && !IsBadMood();
					}
					return CanSelectHCommand() && !IsBadMood();
				},
				Event = delegate
				{
					InviteH(NormalCommandOptionInfos);
				}
			},
			new CommCommandList.CommandInfo("エッチなことをする")
			{
				Condition = delegate
				{
					ChaControl chaControl4 = Singleton<Manager.Map>.Instance.Player.ChaControl;
					return (chaControl4.sex != 1 || chaControl4.fileParam.futanari) && CanSelectHCommand() && IsBadMood();
				},
				Event = delegate
				{
					Singleton<HSceneManager>.Instance.isForce = true;
					InviteH(NormalCommandOptionInfos);
				}
			},
			new CommCommandList.CommandInfo("立ち去る")
			{
				Condition = null,
				Event = delegate
				{
					Left("0");
				}
			}
		};
		TalkCommandOptionInfos = new CommCommandList.CommandInfo[9]
		{
			new CommCommandList.CommandInfo("印象を聞く")
			{
				Condition = null,
				Event = delegate
				{
					StartTalk("0", string.Empty);
				}
			},
			new CommCommandList.CommandInfo("調子どう？")
			{
				Condition = null,
				Event = delegate
				{
					StartTalk("1", string.Empty);
				}
			},
			new CommCommandList.CommandInfo("誰と仲良し？")
			{
				Condition = null,
				Event = delegate
				{
					EndCommonSelection();
					Animation.StopAllAnimCoroutine();
					openData.FindLoad("2", charaID, 0);
					packData.onComplete = delegate
					{
						CheckTalkForceEnd(TalkCommandOptionInfos);
					};
					SearchFavoriteTargetForADV();
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
			},
			new CommCommandList.CommandInfo("お気に入りの場所は？")
			{
				Condition = null,
				Event = delegate
				{
					StartTalk("3", string.Empty);
				}
			},
			new CommCommandList.CommandInfo("おだてる")
			{
				Condition = null,
				Event = delegate
				{
					ChangeCommandList(PraiseCommandOptionInfos, "おだてる", delegate
					{
						BackCommandList(TalkCommandOptionInfos, "トーク", delegate
						{
							PlayerActor player23 = Singleton<Manager.Map>.Instance.Player;
							CommCommandList.CommandInfo[] commands4 = ((player23.Mode != Desire.ActionType.Date) ? NormalCommandOptionInfos : ((!(player23.Partner == this)) ? DateCommandOptionInfosTP : DateCommandOptionInfos));
							BackCommandList(commands4, CharaName, null);
						});
					});
				}
			},
			new CommCommandList.CommandInfo("エッチな会話")
			{
				Condition = new Func<bool>(null, (nint)(delegate*<bool>)(&Game.get_isAdd01)),
				Event = delegate
				{
					EndCommonSelection();
					Animation.StopAllAnimCoroutine();
					openData.FindLoad("0", charaID, 9);
					packData.onComplete = delegate
					{
						CheckTalkForceEnd(TalkCommandOptionInfos);
					};
					packData.restoreCommands = TalkCommandOptionInfos;
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
			},
			new CommCommandList.CommandInfo("戻る")
			{
				Condition = () => Singleton<Manager.Map>.Instance.Player.Mode != Desire.ActionType.Date,
				Event = delegate
				{
					BackCommandList(NormalCommandOptionInfos, CharaName, null);
				}
			},
			new CommCommandList.CommandInfo("戻る")
			{
				Condition = delegate
				{
					PlayerActor player22 = Singleton<Manager.Map>.Instance.Player;
					bool flag7 = player22.Mode == Desire.ActionType.Date;
					bool flag8 = player22.Partner == this;
					return flag7 && flag8;
				},
				Event = delegate
				{
					BackCommandList(DateCommandOptionInfos, CharaName, null);
				}
			},
			new CommCommandList.CommandInfo("戻る")
			{
				Condition = delegate
				{
					PlayerActor player21 = Singleton<Manager.Map>.Instance.Player;
					bool flag5 = player21.Mode == Desire.ActionType.Date;
					bool flag6 = player21.Partner != this;
					return flag5 && flag6;
				},
				Event = delegate
				{
					BackCommandList(DateCommandOptionInfosTP, CharaName, null);
				}
			}
		};
		PraiseCommandOptionInfos = new CommCommandList.CommandInfo[3]
		{
			new CommCommandList.CommandInfo("容姿についてほめる")
			{
				Condition = null,
				Event = delegate
				{
					StartTalk("4", "トーク");
					MapUIContainer.CommandList.CancelEvent = delegate
					{
						PlayerActor player20 = Singleton<Manager.Map>.Instance.Player;
						CommCommandList.CommandInfo[] commands3 = ((player20.Mode != Desire.ActionType.Date) ? NormalCommandOptionInfos : ((!(player20.Partner == this)) ? DateCommandOptionInfosTP : DateCommandOptionInfos));
						BackCommandList(commands3, CharaName, null);
					};
				}
			},
			new CommCommandList.CommandInfo("内面についてほめる")
			{
				Condition = null,
				Event = delegate
				{
					StartTalk("6", "トーク");
					MapUIContainer.CommandList.CancelEvent = delegate
					{
						PlayerActor player19 = Singleton<Manager.Map>.Instance.Player;
						CommCommandList.CommandInfo[] commands2 = ((player19.Mode != Desire.ActionType.Date) ? NormalCommandOptionInfos : ((!(player19.Partner == this)) ? DateCommandOptionInfosTP : DateCommandOptionInfos));
						BackCommandList(commands2, CharaName, null);
					};
				}
			},
			new CommCommandList.CommandInfo("戻る")
			{
				Condition = null,
				Event = delegate
				{
					BackCommandList(TalkCommandOptionInfos, "トーク", delegate
					{
						PlayerActor player18 = Singleton<Manager.Map>.Instance.Player;
						CommCommandList.CommandInfo[] commands = ((player18.Mode != Desire.ActionType.Date) ? NormalCommandOptionInfos : ((!(player18.Partner == this)) ? DateCommandOptionInfosTP : DateCommandOptionInfos));
						BackCommandList(commands, CharaName, null);
					});
				}
			}
		};
		InstructionCommandOptionInfos = new CommCommandList.CommandInfo[12]
		{
			new CommCommandList.CommandInfo("寝に行ったら？")
			{
				Condition = null,
				Event = delegate
				{
					StartInstruction("7");
				}
			},
			new CommCommandList.CommandInfo("少し休んだら？")
			{
				Condition = null,
				Event = delegate
				{
					StartInstruction("0");
				}
			},
			new CommCommandList.CommandInfo("採取手伝って")
			{
				Condition = null,
				Event = delegate
				{
					StartInstruction("1");
				}
			},
			new CommCommandList.CommandInfo("なにか食べたら？")
			{
				Condition = null,
				Event = delegate
				{
					StartInstruction("2");
				}
			},
			new CommCommandList.CommandInfo("なにか飲んだら？")
			{
				Condition = null,
				Event = delegate
				{
					StartInstruction("6");
				}
			},
			new CommCommandList.CommandInfo("料理作って")
			{
				Condition = null,
				Event = delegate
				{
					StartInstruction("5");
				}
			},
			new CommCommandList.CommandInfo("たまには気分転換を")
			{
				Condition = null,
				Event = delegate
				{
					StartInstruction("3");
				}
			},
			new CommCommandList.CommandInfo("トイレ行ったら？")
			{
				Condition = null,
				Event = delegate
				{
					StartInstruction("8");
				}
			},
			new CommCommandList.CommandInfo("風呂入ったら？")
			{
				Condition = null,
				Event = delegate
				{
					StartInstruction("9");
				}
			},
			new CommCommandList.CommandInfo("エッチな行動を要求")
			{
				Condition = new Func<bool>(null, (nint)(delegate*<bool>)(&Game.get_isAdd01)),
				Event = delegate
				{
					StartInstruction("4");
				}
			},
			new CommCommandList.CommandInfo("戻る")
			{
				Condition = () => Singleton<Manager.Map>.Instance.Player.Mode != Desire.ActionType.Date,
				Event = delegate
				{
					BackCommandList(NormalCommandOptionInfos, CharaName, null);
				}
			},
			new CommCommandList.CommandInfo("戻る")
			{
				Condition = delegate
				{
					PlayerActor player17 = Singleton<Manager.Map>.Instance.Player;
					bool flag3 = player17.Mode == Desire.ActionType.Date;
					bool flag4 = player17.Partner != this;
					return flag3 && flag4;
				},
				Event = delegate
				{
					BackCommandList(DateCommandOptionInfosTP, CharaName, null);
				}
			}
		};
		DateCommandOptionInfos = new CommCommandList.CommandInfo[6]
		{
			new CommCommandList.CommandInfo("トーク")
			{
				Condition = null,
				Event = delegate
				{
					ChangeCommandList(TalkCommandOptionInfos, "トーク", delegate
					{
						BackCommandList(DateCommandOptionInfos, CharaName, delegate
						{
							CancelCommunication();
						});
					});
				}
			},
			new CommCommandList.CommandInfo("アイテムを渡す")
			{
				Condition = null,
				Event = delegate
				{
					PresentADV(DateCommandOptionInfos);
				}
			},
			new CommCommandList.CommandInfo("解散する")
			{
				Condition = null,
				Event = delegate(int x)
				{
					if (!(Singleton<Game>.Instance.MapShortcutUI != null))
					{
						EndCommonSelection();
						openData.FindLoad("1", charaID, 5);
						packData.onComplete = delegate
						{
							packData.restoreCommands = null;
							EndCommunication();
							DeactivatePairing(x);
							ActivateHoldingHands(x, enabled: false);
						};
						Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
					}
				}
			},
			new CommCommandList.CommandInfo("エッチがしたい")
			{
				Condition = delegate
				{
					ChaControl chaControl3 = Singleton<Manager.Map>.Instance.Player.ChaControl;
					if (chaControl3.sex == 1 && !chaControl3.fileParam.futanari)
					{
						string[] lesTypeHMeshTag = Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.LesTypeHMeshTag;
						return CanSelectHCommand(lesTypeHMeshTag) && !IsBadMood();
					}
					return CanSelectHCommand() && !IsBadMood();
				},
				Event = delegate
				{
					InviteH(DateCommandOptionInfos);
				}
			},
			new CommCommandList.CommandInfo("エッチなことをする")
			{
				Condition = delegate
				{
					ChaControl chaControl2 = Singleton<Manager.Map>.Instance.Player.ChaControl;
					return (chaControl2.sex != 1 || chaControl2.fileParam.futanari) && CanSelectHCommand() && IsBadMood();
				},
				Event = delegate
				{
					Singleton<HSceneManager>.Instance.isForce = true;
					InviteH(DateCommandOptionInfos);
				}
			},
			new CommCommandList.CommandInfo("立ち去る")
			{
				Condition = null,
				Event = delegate
				{
					Left("2");
				}
			}
		};
		DateCommandOptionInfosTP = new CommCommandList.CommandInfo[8]
		{
			new CommCommandList.CommandInfo("トーク")
			{
				Condition = null,
				Event = delegate
				{
					ChangeCommandList(TalkCommandOptionInfos, "トーク", delegate
					{
						BackCommandList(DateCommandOptionInfosTP, CharaName, delegate
						{
							CancelCommunication();
						});
					});
				}
			},
			new CommCommandList.CommandInfo("アドバイスする")
			{
				Condition = null,
				Event = delegate
				{
					ChangeCommandList(InstructionCommandOptionInfos, "アドバイス", delegate
					{
						BackCommandList(DateCommandOptionInfosTP, CharaName, delegate
						{
							CancelCommunication();
						});
					});
				}
			},
			new CommCommandList.CommandInfo("アイテムを渡す")
			{
				Condition = null,
				Event = delegate
				{
					PresentADV(DateCommandOptionInfosTP);
				}
			},
			new CommCommandList.CommandInfo("頼まれもの")
			{
				Condition = () => AgentData.ItemScrounge.isEvent,
				IsSpecial = true,
				Timer = () => AgentData.ItemScrounge.remainingTime,
				Event = delegate
				{
					StartScroungeEvent(DateCommandOptionInfosTP);
				}
			},
			new CommCommandList.CommandInfo("薬をあげる")
			{
				Condition = delegate
				{
					AIProject.SaveData.Sickness sickState2 = AgentData.SickState;
					return sickState2.ID != 0;
				},
				Event = delegate
				{
					StartSicknessEvent(0);
				}
			},
			new CommCommandList.CommandInfo("薬をあげる")
			{
				Condition = delegate
				{
					AIProject.SaveData.Sickness sickState = AgentData.SickState;
					return sickState.ID == 0;
				},
				Event = delegate
				{
					StartSicknessEvent(0);
				}
			},
			new CommCommandList.CommandInfo("3人でエッチがしたい")
			{
				Condition = delegate
				{
					PlayerActor player16 = Singleton<Manager.Map>.Instance.Player;
					ChaControl chaControl = player16.ChaControl;
					if (chaControl.sex == 1 && !chaControl.fileParam.futanari)
					{
						return false;
					}
					if (player16.AgentPartner == null)
					{
						return false;
					}
					string[] floorTypeHMeshTag = Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.FloorTypeHMeshTag;
					return CanSelectHCommand(floorTypeHMeshTag) && !IsBadMood();
				},
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_L);
					EndCommonSelection();
					Animation.StopAllAnimCoroutine();
					openData.FindLoad("2", charaID, 9);
					packData.onComplete = delegate
					{
						if (packData.isSuccessH)
						{
							packData.restoreCommands = null;
							PlayerActor player15 = Singleton<Manager.Map>.Instance.Player;
							InitiateHScene3P(player15.AgentPartner, this);
						}
						else
						{
							CheckTalkForceEnd(DateCommandOptionInfosTP);
						}
					};
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
			},
			new CommCommandList.CommandInfo("立ち去る")
			{
				Condition = null,
				Event = delegate
				{
					Left("3");
				}
			}
		};
		SpecialCommandOptionInfos = new CommCommandList.CommandInfo[16]
		{
			new CommCommandList.CommandInfo("起こす")
			{
				Condition = () => (Mode == Desire.ActionType.EndTaskSleep || Mode == Desire.ActionType.EndTaskSleepAfterDate || Mode == Desire.ActionType.EndTaskSleepTogether) && !SleepTrigger,
				Event = delegate
				{
					if (!(Singleton<Game>.Instance.MapShortcutUI != null))
					{
						EndCommonSelection();
						Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_L);
						SleepTrigger = true;
						EndCommunication();
						ChangeBehavior(Desire.ActionType.WokenUp);
					}
				}
			},
			new CommCommandList.CommandInfo("エッチなことをする")
			{
				Condition = delegate
				{
					if (!Singleton<Manager.Map>.IsInstance())
					{
						return false;
					}
					PlayerActor player14 = Singleton<Manager.Map>.Instance.Player;
					if (player14.ChaControl.sex == 1 && !player14.ChaControl.fileParam.futanari)
					{
						return false;
					}
					bool flag = Mode == Desire.ActionType.EndTaskSleep || Mode == Desire.ActionType.EndTaskSleepAfterDate || Mode == Desire.ActionType.EndTaskSleepTogether;
					bool flag2 = HPositionID == 1;
					return flag && flag2 && !AgentData.YobaiTrigger;
				},
				Event = delegate
				{
					ClearDesire(Desire.Type.Sleep);
					AgentData.YobaiTrigger = true;
					GotoHScene(HPositionID);
				}
			},
			new CommCommandList.CommandInfo("エッチなことをする")
			{
				Condition = delegate
				{
					if (!Singleton<Manager.Map>.IsInstance())
					{
						return false;
					}
					PlayerActor player13 = Singleton<Manager.Map>.Instance.Player;
					return (player13.ChaControl.sex != 1 || player13.ChaControl.fileParam.futanari) && HPositionID == 2;
				},
				Event = delegate
				{
					ClearDesire(Desire.Type.Bath);
					GotoHScene(HPositionID);
				}
			},
			new CommCommandList.CommandInfo("エッチなことをする")
			{
				Condition = delegate
				{
					if (!Singleton<Manager.Map>.IsInstance())
					{
						return false;
					}
					PlayerActor player12 = Singleton<Manager.Map>.Instance.Player;
					return (player12.ChaControl.sex != 1 || player12.ChaControl.fileParam.futanari) && HPositionID == 3;
				},
				Event = delegate
				{
					int desireKey3 = Desire.GetDesireKey(Desire.Type.Toilet);
					Singleton<HSceneManager>.Instance.ReserveToilet = GetDesire(desireKey3);
					ClearDesire(Desire.Type.Toilet);
					GotoHScene(HPositionID);
				}
			},
			new CommCommandList.CommandInfo("エッチなことをする")
			{
				Condition = delegate
				{
					if (!Singleton<Manager.Map>.IsInstance())
					{
						return false;
					}
					PlayerActor player11 = Singleton<Manager.Map>.Instance.Player;
					return (player11.ChaControl.sex != 1 || player11.ChaControl.fileParam.futanari) && HPositionID == 4;
				},
				Event = delegate
				{
					int desireKey2 = Desire.GetDesireKey(Desire.Type.Toilet);
					Singleton<HSceneManager>.Instance.ReserveToilet = GetDesire(desireKey2);
					ClearDesire(Desire.Type.Toilet);
					GotoHScene(HPositionID);
				}
			},
			new CommCommandList.CommandInfo("エッチなことをする")
			{
				Condition = delegate
				{
					if (!Singleton<Manager.Map>.IsInstance())
					{
						return false;
					}
					PlayerActor player10 = Singleton<Manager.Map>.Instance.Player;
					return (player10.ChaControl.sex != 1 || player10.ChaControl.fileParam.futanari) && HPositionID == 5;
				},
				Event = delegate
				{
					if (Desire.ModeTable.TryGetValue(PrevMode, out var value2))
					{
						int desireKey = Desire.GetDesireKey(value2);
						SetDesire(desireKey, 0f);
					}
					GotoHScene(HPositionID);
				}
			},
			new CommCommandList.CommandInfo("エッチなことをする")
			{
				Condition = delegate
				{
					if (!Singleton<Manager.Map>.IsInstance())
					{
						return false;
					}
					PlayerActor player9 = Singleton<Manager.Map>.Instance.Player;
					return (player9.ChaControl.sex != 1 || player9.ChaControl.fileParam.futanari) && HPositionID == 6;
				},
				Event = delegate
				{
					ClearDesire(Desire.Type.Hunt);
					GotoHScene(HPositionID);
				}
			},
			new CommCommandList.CommandInfo("エッチなことをする")
			{
				Condition = delegate
				{
					if (!Singleton<Manager.Map>.IsInstance())
					{
						return false;
					}
					PlayerActor player8 = Singleton<Manager.Map>.Instance.Player;
					return (player8.ChaControl.sex != 1 || player8.ChaControl.fileParam.futanari) && HPositionID == 7;
				},
				Event = delegate
				{
					ClearDesire(Desire.Type.Cook);
					GotoHScene(HPositionID);
				}
			},
			new CommCommandList.CommandInfo("エッチなことをする")
			{
				Condition = delegate
				{
					if (!Singleton<Manager.Map>.IsInstance())
					{
						return false;
					}
					PlayerActor player7 = Singleton<Manager.Map>.Instance.Player;
					return (player7.ChaControl.sex != 1 || player7.ChaControl.fileParam.futanari) && HPositionID == 8;
				},
				Event = delegate
				{
					GotoHScene(HPositionID);
				}
			},
			new CommCommandList.CommandInfo("エッチなことをする")
			{
				Condition = delegate
				{
					if (!Singleton<Manager.Map>.IsInstance())
					{
						return false;
					}
					PlayerActor player6 = Singleton<Manager.Map>.Instance.Player;
					return (player6.ChaControl.sex != 1 || player6.ChaControl.fileParam.futanari) && HPositionID == 9;
				},
				Event = delegate
				{
					GotoHScene(HPositionID);
				}
			},
			new CommCommandList.CommandInfo("エッチなことをする")
			{
				Condition = delegate
				{
					if (!Singleton<Manager.Map>.IsInstance())
					{
						return false;
					}
					PlayerActor player5 = Singleton<Manager.Map>.Instance.Player;
					return (player5.ChaControl.sex != 1 || player5.ChaControl.fileParam.futanari) && HPositionID == 10;
				},
				Event = delegate
				{
					GotoHScene(HPositionID);
				}
			},
			new CommCommandList.CommandInfo("エッチなことをする")
			{
				Condition = delegate
				{
					if (!Singleton<Manager.Map>.IsInstance())
					{
						return false;
					}
					PlayerActor player4 = Singleton<Manager.Map>.Instance.Player;
					return (player4.ChaControl.sex != 1 || player4.ChaControl.fileParam.futanari) && HPositionID == 13;
				},
				Event = delegate
				{
					ClearDesire(Desire.Type.Bath);
					GotoHScene(HPositionID);
				}
			},
			new CommCommandList.CommandInfo("エッチなことをする")
			{
				Condition = delegate
				{
					if (!Singleton<Manager.Map>.IsInstance())
					{
						return false;
					}
					PlayerActor player3 = Singleton<Manager.Map>.Instance.Player;
					return (player3.ChaControl.sex != 1 || player3.ChaControl.fileParam.futanari) && HPositionID == 14;
				},
				Event = delegate
				{
					ClearDesire(Desire.Type.Game);
					float num = 0f - Animation.GetAngleFromForward(Controller.transform.forward);
					Vector3 position = base.Position;
					position.y = 0f;
					Vector3 position2 = Singleton<Manager.Map>.Instance.Player.Position;
					position2.y = 0f;
					float angleFromForward = Animation.GetAngleFromForward(Vector3.Normalize(position2 - position));
					float f = angleFromForward - num;
					if (Mathf.Abs(f) > 90f)
					{
						Debug.Log("壁穴後ろから");
						GotoHScene(HPositionID);
					}
					else
					{
						Debug.Log("壁穴前から");
						GotoHScene(HPositionSubID);
					}
				}
			},
			new CommCommandList.CommandInfo("エッチなことをする")
			{
				Condition = delegate
				{
					if (!Singleton<Manager.Map>.IsInstance())
					{
						return false;
					}
					PlayerActor player2 = Singleton<Manager.Map>.Instance.Player;
					return (player2.ChaControl.sex != 1 || player2.ChaControl.fileParam.futanari) && HPositionID == 16;
				},
				Event = delegate
				{
					GotoHScene(HPositionID);
				}
			},
			new CommCommandList.CommandInfo("エッチなことをする")
			{
				Condition = delegate
				{
					if (!Singleton<Manager.Map>.IsInstance())
					{
						return false;
					}
					PlayerActor player = Singleton<Manager.Map>.Instance.Player;
					return (player.ChaControl.sex != 1 || player.ChaControl.fileParam.futanari) && HPositionID == 17;
				},
				Event = delegate
				{
					GotoHScene(HPositionID);
				}
			},
			new CommCommandList.CommandInfo("立ち去る")
			{
				Condition = null,
				Event = delegate
				{
					Left("4");
				}
			}
		};
		ColdCommandOptionInfos = new CommCommandList.CommandInfo[2]
		{
			new CommCommandList.CommandInfo("薬をあげる")
			{
				Condition = null,
				IsSpecial = true,
				Event = delegate
				{
					StartSicknessEvent(1);
				}
			},
			new CommCommandList.CommandInfo("立ち去る")
			{
				Condition = null,
				Event = delegate
				{
					Left("10");
				}
			}
		};
	}

	public void StartCommunication()
	{
		StopNavMeshAgent();
		if (Mode != Desire.ActionType.Encounter)
		{
			_originAvoidancePriority = _navMeshAgent.avoidancePriority;
			ChangeStaticNavMeshAgentAvoidance();
		}
		packData.Init();
		PlayerActor player = Singleton<Manager.Map>.Instance.Player;
		player.NavMeshAgent.velocity = Vector3.zero;
		player.CommCompanion = this;
		player.Controller.ChangeState("Communication");
		packData.AttitudeID = AttitudeID;
		switch (AttitudeID)
		{
		case 0:
		{
			if (UseNeckLook)
			{
				base.ChaControl.ChangeLookEyesTarget(1, player.CameraControl.CameraComponent.transform);
				base.ChaControl.ChangeLookEyesPtn(1);
				base.ChaControl.ChangeLookNeckTarget(0);
				base.ChaControl.ChangeLookNeckPtn(3);
			}
			PoseKeyPair poseKeyPair = Singleton<Manager.Resources>.Instance.AgentProfile.ADVIdleTable[base.ChaControl.fileParam.personality];
			PlayState playState = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[poseKeyPair.postureID][poseKeyPair.poseID];
			AssetBundleInfo assetBundleInfo = playState.MainStateInfo.AssetBundleInfo;
			ChangeAnimator(assetBundleInfo.assetbundle, assetBundleInfo.asset);
			Animation.PlayTurnAnimation(player.Position, 1f, playState.MainStateInfo.InStateInfo);
			SetActiveOnEquipedItem(active: false);
			break;
		}
		case 1:
		case 2:
		case 3:
		case 4:
		case 5:
			if (UseNeckLook)
			{
				base.ChaControl.ChangeLookEyesTarget(1, player.CameraControl.CameraComponent.transform);
				base.ChaControl.ChangeLookEyesPtn(1);
				base.ChaControl.ChangeLookNeckTarget(1, player.CameraControl.CameraComponent.transform);
				base.ChaControl.ChangeLookNeckPtn(1);
			}
			_schedule.progress = false;
			break;
		}
		DisableBehavior();
		Debug.Log($"StartCommunication.DisableBehavior proc frame: {Time.frameCount}");
		Observable.EveryUpdate().Take(5).Subscribe(delegate
		{
			switch (AttitudeID)
			{
			case 0:
				if (UseNeckLook)
				{
					base.ChaControl.ChangeLookEyesTarget(1, player.CameraControl.CameraComponent.transform);
					base.ChaControl.ChangeLookEyesPtn(1);
					base.ChaControl.ChangeLookNeckTarget(0);
					base.ChaControl.ChangeLookNeckPtn(3);
				}
				break;
			case 1:
			case 2:
			case 3:
			case 4:
			case 5:
				if (UseNeckLook)
				{
					base.ChaControl.ChangeLookEyesTarget(1, player.CameraControl.CameraComponent.transform);
					base.ChaControl.ChangeLookEyesPtn(1);
					base.ChaControl.ChangeLookNeckTarget(1, player.CameraControl.CameraComponent.transform);
					base.ChaControl.ChangeLookNeckPtn(1);
				}
				_schedule.progress = false;
				break;
			}
			BehaviorResources.Current.DisableBehavior(pause: true);
		});
		if (base.Partner != null && base.Partner is AgentActor)
		{
			AgentActor agentActor = base.Partner as AgentActor;
			if (AttitudeID != 0)
			{
				agentActor.DisableBehavior();
			}
		}
		switch (AttitudeID)
		{
		case 0:
			Manager.ADV.ChangeADVCamera(this);
			break;
		case 1:
			Manager.ADV.ChangeADVCameraDiagonal(this);
			break;
		default:
			Manager.ADV.ChangeADVFixedAngleCamera(this, AttitudeID);
			break;
		}
		player.CameraControl.VanishControl.LoadHousingVanish(player);
		player.CameraControl.VanishControl.Load();
		MapUIContainer.SetVisibleHUD(active: false);
		Singleton<Manager.ADV>.Instance.TargetCharacter = this;
	}

	private void StartDateCommunication()
	{
		StopNavMeshAgent();
		packData.Init();
		PlayerActor player = Singleton<Manager.Map>.Instance.Player;
		player.CommCompanion = this;
		player.Controller.ChangeState("Communication");
		if (UseNeckLook)
		{
			base.ChaControl.ChangeLookEyesTarget(1, player.CameraControl.CameraComponent.transform);
			base.ChaControl.ChangeLookEyesPtn(1);
			base.ChaControl.ChangeLookNeckTarget(0);
			base.ChaControl.ChangeLookNeckPtn(3);
		}
		packData.AttitudeID = AttitudeID;
		switch (AttitudeID)
		{
		case 0:
		{
			PoseKeyPair poseKeyPair = Singleton<Manager.Resources>.Instance.AgentProfile.ADVIdleTable[base.ChaControl.fileParam.personality];
			PlayState playState = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[poseKeyPair.postureID][poseKeyPair.poseID];
			AssetBundleInfo assetBundleInfo = playState.MainStateInfo.AssetBundleInfo;
			ChangeAnimator(assetBundleInfo.assetbundle, assetBundleInfo.asset);
			Animation.PlayTurnAnimation(player.Position, 1f, playState.MainStateInfo.InStateInfo);
			if (player.OldEnabledHoldingHand = player.HandsHolder.enabled)
			{
				player.HandsHolder.enabled = false;
				if (player.HandsHolder.EnabledHolding)
				{
					player.HandsHolder.EnabledHolding = false;
				}
			}
			SetActiveOnEquipedItem(active: false);
			break;
		}
		case 1:
		case 2:
		case 3:
		case 4:
		case 5:
			_schedule.progress = false;
			break;
		}
		DisableBehavior();
		Manager.ADV instance = Singleton<Manager.ADV>.Instance;
		Manager.ADV.ChangeADVCamera(this);
		MapUIContainer.SetVisibleHUD(active: false);
		instance.TargetCharacter = this;
	}

	private void EndCommunication()
	{
		MapUIContainer.SetActiveCommandList(active: false);
		VanishCommands();
		PlayerActor player = Singleton<Manager.Map>.Instance.Player;
		if (player.OldEnabledHoldingHand)
		{
			player.HandsHolder.enabled = true;
			player.OldEnabledHoldingHand = false;
		}
		Singleton<Manager.Map>.Instance.Player.PlayerController.CommandArea.RefreshCommands();
		packData.Release();
	}

	private void CancelCommunication()
	{
		EndCommonSelection();
		MapUIContainer.SetActiveCommandList(active: false);
		VanishCommands();
		PlayerActor player = Singleton<Manager.Map>.Instance.Player;
		if (player.OldEnabledHoldingHand)
		{
			player.HandsHolder.enabled = true;
			player.OldEnabledHoldingHand = false;
		}
		Singleton<Manager.Map>.Instance.Player.PlayerController.CommandArea.RefreshCommands();
		packData.Release();
	}

	private void StartTalk(string asset, string titleOnBack = "")
	{
		Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_L);
		EndCommonSelection();
		Animation.StopAllAnimCoroutine();
		openData.FindLoad(asset, charaID, 0);
		packData.onComplete = delegate
		{
			if (!titleOnBack.IsNullOrEmpty())
			{
				MapUIContainer.CommandList.Label.text = titleOnBack;
			}
			CheckTalkForceEnd(TalkCommandOptionInfos);
		};
		Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
	}

	private void StartInstruction(string asset)
	{
		Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_L);
		EndCommonSelection();
		Animation.StopAllAnimCoroutine();
		openData.FindLoad(asset, charaID, 1);
		packData.onComplete = delegate
		{
			CheckTalkForceEnd(InstructionCommandOptionInfos);
		};
		Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
	}

	private void InviteH(CommCommandList.CommandInfo[] restoreCommands)
	{
		Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_L);
		EndCommonSelection();
		Animation.StopAllAnimCoroutine();
		openData.FindLoad("1", charaID, 9);
		packData.onComplete = delegate
		{
			if (packData.isSuccessH)
			{
				Desire.Type value;
				if (Mode == Desire.ActionType.Encounter)
				{
					if (Desire.ModeTable.TryGetValue(PrevMode, out value))
					{
						ClearDesire(value);
					}
				}
				else if (Desire.ModeTable.TryGetValue(Mode, out value))
				{
					ClearDesire(value);
				}
				packData.restoreCommands = null;
				InitiateHScene();
			}
			else
			{
				CheckTalkForceEnd(restoreCommands);
			}
		};
		Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
	}

	private void GotoHScene(int hPosID)
	{
		EndCommonSelection();
		Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_L);
		MapUIContainer.SetActiveCommandList(active: false);
		Singleton<HSceneManager>.Instance.isForce = true;
		InitiateHScene((HSceneManager.HEvent)hPosID);
	}

	private void Left(string asset)
	{
		if (!(Singleton<Game>.Instance.MapShortcutUI != null))
		{
			EndCommonSelection();
			Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Cancel);
			openData.FindLoad(asset, charaID, 5);
			packData.onComplete = delegate
			{
				packData.restoreCommands = null;
				EndCommunication();
			};
			Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
		}
	}

	private void ChangeCommandList(CommCommandList.CommandInfo[] commands, string title, System.Action onCancel)
	{
		Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
		MapUIContainer.CommandList.Refresh(commands);
		MapUIContainer.CommandList.Label.text = title;
		MapUIContainer.CommandList.CancelEvent = onCancel;
	}

	private void BackCommandList(CommCommandList.CommandInfo[] commands, string title, System.Action onCancel)
	{
		Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Cancel);
		MapUIContainer.CommandList.Refresh(commands);
		MapUIContainer.CommandList.Label.text = title;
		MapUIContainer.CommandList.CancelEvent = onCancel;
	}

	private void CheckTalkForceEnd(CommCommandList.CommandInfo[] restoreCommands)
	{
		if (AgentData.TalkMotivation <= 0f)
		{
			Debug.Log("会話のやる気が0になった");
			AgentData.LockTalk = true;
			AgentData.TalkElapsedTime = 0f;
			packData.restoreCommands = null;
			EndCommunication();
			PlayerActor player = Singleton<Manager.Map>.Instance.Player;
			if (player.Mode == Desire.ActionType.Date && player.Partner == this)
			{
				DeactivatePairing(0);
				ActivateHoldingHands(0, enabled: false);
			}
		}
		else
		{
			packData.restoreCommands = restoreCommands;
			packData.OnEndRefreshCommand = delegate
			{
				StartCommonSelection();
				packData.OnEndRefreshCommand = null;
			};
		}
	}

	private void StartEvent()
	{
		IsEvent = true;
	}

	private bool CheckStealEvent()
	{
		PoseKeyPair snitchFoodID = Singleton<Manager.Resources>.Instance.AgentProfile.PoseIDTable.SnitchFoodID;
		if (base.ActionID == snitchFoodID.postureID && base.PoseID == snitchFoodID.poseID)
		{
			return true;
		}
		return false;
	}

	private void StartStealEvent()
	{
		MapUIContainer.SetVisibleHUDExceptStoryUI(active: false);
		MapUIContainer.StorySupportUI.Close();
		Desire.ActionType mode = BehaviorResources.Mode;
		BehaviorResources.ChangeMode(Desire.ActionType.Idle);
		Observable.EveryLateUpdate().Take(20).Subscribe(delegate
		{
			StopNavMeshAgent();
		});
		StopNavMeshAgent();
		Animation.CrossFadeScreen();
		packData.Init();
		PlayerActor player = Singleton<Manager.Map>.Instance.Player;
		player.CommCompanion = this;
		player.PlayerController.ChangeState("Idle");
		PoseKeyPair foundID = Singleton<Manager.Resources>.Instance.AgentProfile.PoseIDTable.StealFoundID;
		Observable.FromCoroutine(() => StealAnimCoroutine(foundID.postureID, foundID.poseID)).Subscribe(delegate
		{
			player.PlayerController.ChangeState("Communication");
			Animation.CrossFadeScreen();
			Animation.StopAllAnimCoroutine();
			if (UseNeckLook)
			{
				base.ChaControl.ChangeLookEyesTarget(1, player.CameraControl.CameraComponent.transform);
				base.ChaControl.ChangeLookEyesPtn(1);
				base.ChaControl.ChangeLookNeckTarget(0);
				base.ChaControl.ChangeLookNeckPtn(3);
			}
			PoseKeyPair poseKeyPair = Singleton<Manager.Resources>.Instance.AgentProfile.ADVIdleTable[base.ChaControl.fileParam.personality];
			PlayState playState = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[poseKeyPair.postureID][poseKeyPair.poseID];
			AssetBundleInfo assetBundleInfo = playState.MainStateInfo.AssetBundleInfo;
			ChangeAnimator(assetBundleInfo.assetbundle, assetBundleInfo.asset);
			Animation.PlayTurnAnimation(player.Position, 1f, playState.MainStateInfo.InStateInfo);
			Manager.ADV.ChangeADVCamera(this);
			player.CameraControl.VanishControl.LoadHousingVanish(player);
			player.CameraControl.VanishControl.Load();
			openData.FindLoad("2", charaID, 7);
			packData.onComplete = delegate
			{
				packData.restoreCommands = null;
				SetLookPtn(0, 3);
				SetLookTarget(0, 0);
				Animation.StopAllAnimCoroutine();
				Animation.ResetDefaultAnimatorController();
				if (base.CurrentPoint != null)
				{
					base.CurrentPoint.SetActiveMapItemObjs(active: true);
					base.CurrentPoint.ReleaseSlot(this);
					base.CurrentPoint = null;
				}
				if (AgentData.CarryingItem != null)
				{
					ApplyFoodParameter(AgentData.CarryingItem);
					AgentData.CarryingItem = null;
				}
				ChangeBehavior(Desire.ActionType.Normal);
				player.CameraControl.Mode = CameraMode.Normal;
				player.PlayerController.ChangeState("Normal");
				MapUIContainer.SetVisibleHUD(active: true);
				MapUIContainer.StorySupportUI.Open();
				packData.Release();
			};
			Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
		});
	}

	private bool CheckCatEvent()
	{
		if (AttitudeID != 0)
		{
			return false;
		}
		if (AgentData.CheckCatEvent)
		{
			return false;
		}
		if (PrevActionMode != Desire.ActionType.EndTaskWildAnimal)
		{
			return false;
		}
		int num = UnityEngine.Random.Range(0, 100);
		float catEventBaseProb = Singleton<Manager.Resources>.Instance.AgentProfile.CatEventBaseProb;
		int flavor = base.ChaControl.fileGameInfo.flavorState[3];
		Threshold flavorCatCaptureMinMax = Singleton<Manager.Resources>.Instance.StatusProfile.FlavorCatCaptureMinMax;
		Threshold flavorCatCaptureRate = Singleton<Manager.Resources>.Instance.StatusProfile.FlavorCatCaptureRate;
		float f = FlavorVariation(flavorCatCaptureMinMax, flavorCatCaptureRate, flavor);
		catEventBaseProb += (float)Mathf.RoundToInt(f);
		if (base.ChaControl.fileGameInfo.normalSkill.ContainsValue(19))
		{
			catEventBaseProb += (float)Singleton<Manager.Resources>.Instance.StatusProfile.CatCaptureProbBuff;
		}
		AgentData.CheckCatEvent = true;
		if ((float)num < catEventBaseProb)
		{
			Debug.Log("猫イベント発生");
			return true;
		}
		Debug.Log("猫イベント失敗");
		return false;
	}

	private void StartCatEvent()
	{
		packData.Init();
		PlayerActor player = Singleton<Manager.Map>.Instance.Player;
		Desire.ActionType prevMode = BehaviorResources.Mode;
		BehaviorResources.ChangeMode(Desire.ActionType.Idle);
		player.PlayerController.ChangeState("Idle");
		openData.FindLoad("1", charaID, 7);
		packData.onComplete = delegate
		{
			packData.restoreCommands = null;
			SetLookPtn(0, 3);
			SetLookTarget(0, 0);
			if (AttitudeID == 0)
			{
				Animation.StopAllAnimCoroutine();
				Animation.ResetDefaultAnimatorController();
			}
			CinemachineBlendDefinition.Style prevStyle2 = player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style;
			player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
			player.CameraControl.Mode = CameraMode.Normal;
			player.ChaControl.visibleAll = player.CameraControl.ShotType != ShotType.PointOfView;
			Observable.EveryLateUpdate().Skip(1).Take(1)
				.Subscribe(delegate
				{
					player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = prevStyle2;
				});
			Singleton<Manager.Map>.Instance.EnableEntity(this);
			MapUIContainer.FadeCanvas.StartFade(FadeCanvas.PanelType.Blackout, FadeType.Out, 2f, ignoreTimeScale: true).Subscribe((Action<Unit>)delegate
			{
			}, (System.Action)delegate
			{
				BehaviorResources.ChangeMode(prevMode);
				player.PlayerController.ChangeState("Normal");
				MapUIContainer.SetVisibleHUD(active: true);
				MapUIContainer.StorySupportUI.Open();
				packData.Release();
			});
		};
		packData.Init();
		MapUIContainer.SetVisibleHUDExceptStoryUI(active: false);
		MapUIContainer.StorySupportUI.Close();
		if (MapUIContainer.FadeCanvas.IsFading)
		{
			MapUIContainer.FadeCanvas.SkipFade();
		}
		MapUIContainer.FadeCanvas.StartFade(FadeCanvas.PanelType.Blackout, FadeType.In, 2f, ignoreTimeScale: true).Subscribe((Action<Unit>)delegate
		{
		}, (System.Action)delegate
		{
			Singleton<Manager.Map>.Instance.DisableEntity(this);
			Vector3 position = base.Position;
			position.y = 0f;
			Vector3 position2 = player.Position;
			position2.y = 0f;
			base.Rotation = Quaternion.LookRotation(position2 - position);
			player.CommCompanion = this;
			player.PlayerController.ChangeState("Communication");
			PoseKeyPair poseKeyPair = Singleton<Manager.Resources>.Instance.AgentProfile.ADVIdleTable[base.ChaControl.fileParam.personality];
			PlayState playState = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[poseKeyPair.postureID][poseKeyPair.poseID];
			AssetBundleInfo assetBundleInfo = playState.MainStateInfo.AssetBundleInfo;
			ChangeAnimator(assetBundleInfo.assetbundle, assetBundleInfo.asset);
			player.Rotation = Quaternion.LookRotation(position - position2);
			CinemachineBlendDefinition.Style prevStyle = player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style;
			player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
			Manager.ADV.ChangeADVCamera(this);
			Observable.EveryLateUpdate().Skip(1).Take(1)
				.Subscribe(delegate
				{
					player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = prevStyle;
				});
			Transform target = player.CameraControl.CameraComponent.transform;
			SetLookPtn(1, 3);
			SetLookTarget(1, 0, target);
			Observable.EveryUpdate().Skip(1).SkipWhile((long _) => player.CameraControl.CinemachineBrain.IsBlending)
				.Take(1)
				.DelayFrame(5)
				.Subscribe(delegate
				{
					Observable.TimerFrame(30).Subscribe(delegate
					{
						Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
					});
				});
		});
	}

	private bool CheckFishingEvent()
	{
		if (IsBadMood())
		{
			return false;
		}
		return base.ActionID == 6 && new int[3] { 6, 7, 8 }.Contains(base.PoseID);
	}

	private void FishingEvent()
	{
		packData.Init();
		int desireKey = Desire.GetDesireKey(RequestedDesire);
		SetDesire(desireKey, 0f);
		PlayerActor player = Singleton<Manager.Map>.Instance.Player;
		player.PlayerController.ChangeState("Idle");
		BehaviorResources.ChangeMode(Desire.ActionType.Idle);
		DisableBehavior();
		DeactivateNavMeshAgent();
		openData.FindLoad("0", charaID, 7);
		packData.onComplete = delegate
		{
			packData.restoreCommands = null;
			SetLookPtn(0, 3);
			SetLookTarget(0, 0);
			ResetActionFlag();
			Animation.StopAllAnimCoroutine();
			Animation.ResetDefaultAnimatorController();
			CinemachineBlendDefinition.Style prevStyle2 = player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style;
			player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
			player.CameraControl.Mode = CameraMode.Normal;
			player.ChaControl.visibleAll = player.CameraControl.ShotType != ShotType.PointOfView;
			Observable.EveryLateUpdate().Skip(1).Take(1)
				.Subscribe(delegate
				{
					player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = prevStyle2;
				});
			if (AgentData.CarryingItem != null)
			{
				AgentData.CarryingItem = null;
			}
			EnableBehavior();
			ActivateNavMeshAgent();
			Singleton<Manager.Map>.Instance.EnableEntity(this);
			MapUIContainer.FadeCanvas.StartFade(FadeCanvas.PanelType.Blackout, FadeType.Out, 2f, ignoreTimeScale: true).Subscribe((Action<Unit>)delegate
			{
			}, (System.Action)delegate
			{
				ChangeBehavior(Desire.ActionType.Normal);
				player.PlayerController.ChangeState("Normal");
				MapUIContainer.SetVisibleHUD(active: true);
				MapUIContainer.StorySupportUI.Open();
				packData.Release();
			});
		};
		packData.Init();
		MapUIContainer.SetVisibleHUDExceptStoryUI(active: false);
		MapUIContainer.StorySupportUI.Close();
		if (MapUIContainer.FadeCanvas.IsFading)
		{
			MapUIContainer.FadeCanvas.SkipFade();
		}
		MapUIContainer.FadeCanvas.StartFade(FadeCanvas.PanelType.Blackout, FadeType.In, 2f, ignoreTimeScale: true).Subscribe((Action<Unit>)delegate
		{
		}, (System.Action)delegate
		{
			Singleton<Manager.Map>.Instance.DisableEntity(this);
			Vector3 position = base.Position;
			position.y = 0f;
			Vector3 position2 = player.Position;
			position2.y = 0f;
			base.Rotation = Quaternion.LookRotation(position2 - position);
			player.CommCompanion = this;
			player.PlayerController.ChangeState("Communication");
			PoseKeyPair poseKeyPair = Singleton<Manager.Resources>.Instance.AgentProfile.ADVIdleTable[base.ChaControl.fileParam.personality];
			PlayState playState = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[poseKeyPair.postureID][poseKeyPair.poseID];
			AssetBundleInfo assetBundleInfo = playState.MainStateInfo.AssetBundleInfo;
			ChangeAnimator(assetBundleInfo.assetbundle, assetBundleInfo.asset);
			player.Rotation = Quaternion.LookRotation(position - position2);
			CinemachineBlendDefinition.Style prevStyle = player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style;
			player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
			Manager.ADV.ChangeADVCamera(this);
			Observable.EveryLateUpdate().Skip(1).Take(1)
				.Subscribe(delegate
				{
					player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = prevStyle;
				});
			Transform target = player.CameraControl.CameraComponent.transform;
			SetLookPtn(1, 3);
			SetLookTarget(1, 0, target);
			Observable.EveryUpdate().Skip(1).SkipWhile((long _) => player.CameraControl.CinemachineBrain.IsBlending)
				.Take(1)
				.DelayFrame(5)
				.Subscribe(delegate
				{
					Observable.TimerFrame(30).Subscribe(delegate
					{
						Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
					});
				});
		});
	}

	private void OpenDataFindLoadEvent(string fileName)
	{
		int num = 6;
		AgentData.advEventLimitation.Add(num);
		AgentData.GetAdvEventCheck(num).Add(fileName);
		openData.FindLoad(fileName, charaID, num);
	}

	public void AdvEventStart()
	{
		AdvEventStart(advEventName);
	}

	private void AdvEventStart(string fileName)
	{
		packData.Init();
		int desireKey = Desire.GetDesireKey(RequestedDesire);
		SetDesire(desireKey, 0f);
		PlayerActor player = Singleton<Manager.Map>.Instance.Player;
		player.PlayerController.ChangeState("Idle");
		BehaviorResources.ChangeMode(Desire.ActionType.Idle);
		DisableBehavior();
		DeactivateNavMeshAgent();
		OpenDataFindLoadEvent(fileName);
		packData.onComplete = delegate
		{
			packData.restoreCommands = null;
			SetLookPtn(0, 3);
			SetLookTarget(0, 0);
			ResetActionFlag();
			Animation.StopAllAnimCoroutine();
			Animation.ResetDefaultAnimatorController();
			if (base.CurrentPoint != null)
			{
				base.CurrentPoint.SetActiveMapItemObjs(active: true);
				base.CurrentPoint.ReleaseSlot(this);
				base.CurrentPoint = null;
			}
			CinemachineBlendDefinition.Style prevStyle2 = player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style;
			player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
			player.CameraControl.Mode = CameraMode.Normal;
			player.ChaControl.visibleAll = player.CameraControl.ShotType != ShotType.PointOfView;
			Observable.EveryLateUpdate().Skip(1).Take(1)
				.Subscribe(delegate
				{
					player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = prevStyle2;
				});
			if (AgentData.CarryingItem != null)
			{
				AgentData.CarryingItem = null;
			}
			EnableBehavior();
			ActivateNavMeshAgent();
			Singleton<Manager.Map>.Instance.EnableEntity(this);
			MapUIContainer.FadeCanvas.StartFade(FadeCanvas.PanelType.Blackout, FadeType.Out, 2f, ignoreTimeScale: true).Subscribe((Action<Unit>)delegate
			{
			}, (System.Action)delegate
			{
				ChangeBehavior(Desire.ActionType.Normal);
				player.PlayerController.ChangeState("Normal");
				MapUIContainer.SetVisibleHUD(active: true);
				MapUIContainer.StorySupportUI.Open();
				packData.Release();
			});
		};
		MapUIContainer.SetVisibleHUDExceptStoryUI(active: false);
		MapUIContainer.StorySupportUI.Close();
		if (MapUIContainer.FadeCanvas.IsFading)
		{
			MapUIContainer.FadeCanvas.SkipFade();
		}
		MapUIContainer.FadeCanvas.StartFade(FadeCanvas.PanelType.Blackout, FadeType.In, 2f, ignoreTimeScale: true).Subscribe((Action<Unit>)delegate
		{
		}, (System.Action)delegate
		{
			Singleton<Manager.Map>.Instance.DisableEntity(this);
			int? num = advEventParam?.ExpansionID;
			if (num.HasValue)
			{
				int value = num.Value;
				if (value == 2 && base.CurrentPoint != null)
				{
					EventPivot component = base.CurrentPoint.GetComponent<EventPivot>();
					if (component != null && component.PivotTransform != null)
					{
						Transform pivotTransform = component.PivotTransform;
						base.Position = pivotTransform.position;
						base.Rotation = pivotTransform.rotation;
					}
				}
			}
			AgentAdvEventInfo.EventPosition eventPosition = advEventParam?.EventPos;
			if (eventPosition != null && eventPosition.isOrder && Singleton<Manager.Map>.Instance.EventStartPointDic.TryGetValue(eventPosition.mapID, out var value2) && value2.TryGetValue(eventPosition.ID, out var value3))
			{
				base.Position = value3.position;
				base.Rotation = value3.rotation;
			}
			bool? flag = advEventParam?.LookPlayer;
			if (!flag.HasValue || flag.Value)
			{
				Vector3 position = base.Position;
				position.y = 0f;
				Vector3 position2 = player.Position;
				position2.y = 0f;
				base.Rotation = Quaternion.LookRotation(position2 - position);
				player.Rotation = Quaternion.LookRotation(position - position2);
			}
			player.CommCompanion = this;
			player.PlayerController.ChangeState("Communication");
			SetActiveOnEquipedItem(active: false);
			PoseKeyPair poseKeyPair = Singleton<Manager.Resources>.Instance.AgentProfile.ADVIdleTable[base.ChaControl.fileParam.personality];
			PlayState playState = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[poseKeyPair.postureID][poseKeyPair.poseID];
			AssetBundleInfo assetBundleInfo = playState.MainStateInfo.AssetBundleInfo;
			ChangeAnimator(assetBundleInfo.assetbundle, assetBundleInfo.asset);
			CinemachineBlendDefinition.Style prevStyle = player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style;
			player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
			Manager.ADV.ChangeADVCamera(this);
			Observable.EveryLateUpdate().Skip(1).Take(1)
				.Subscribe(delegate
				{
					player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = prevStyle;
				});
			Transform target = player.CameraControl.CameraComponent.transform;
			SetLookPtn(1, 3);
			SetLookTarget(1, 0, target);
			Observable.EveryUpdate().Skip(1).SkipWhile((long _) => player.CameraControl.CinemachineBrain.IsBlending)
				.Take(1)
				.DelayFrame(5)
				.Subscribe(delegate
				{
					Observable.TimerFrame(30).Subscribe(delegate
					{
						Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
					});
				});
		});
	}

	public void AdvEventStart_SleepingPlayer(PlayerActor player)
	{
		packData.Init();
		int desireKey = Desire.GetDesireKey(RequestedDesire);
		SetDesire(desireKey, 0f);
		Animation.StopAllAnimCoroutine();
		SetActiveOnEquipedItem(active: false);
		base.ChaControl.setAllLayerWeight(0f);
		player.PlayerController.ChangeState("Idle");
		BehaviorResources.ChangeMode(Desire.ActionType.Idle);
		DisableBehavior();
		DeactivateNavMeshAgent();
		MapUIContainer.SetVisibleHUD(active: false);
		var bkData = new { base.Position, base.Rotation };
		base.Position = player.Position;
		base.Rotation = player.Rotation;
		PoseKeyPair poseKeyPair = Singleton<Manager.Resources>.Instance.AgentProfile.ADVIdleTable[base.ChaControl.fileParam.personality];
		PlayState playState = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[poseKeyPair.postureID][poseKeyPair.poseID];
		AssetBundleInfo assetBundleInfo = playState.MainStateInfo.AssetBundleInfo;
		ChangeAnimator(assetBundleInfo.assetbundle, assetBundleInfo.asset);
		Transform target = player.CameraControl.CameraComponent.transform;
		SetLookPtn(1, 3);
		SetLookTarget(1, 0, target);
		OpenDataFindLoadEvent(advEventName);
		System.Action cameraBlendStyleChange = delegate
		{
			CinemachineBlendDefinition.Style prevStyle = player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style;
			player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
			Observable.EveryLateUpdate().Skip(1).Take(1)
				.Subscribe(delegate
				{
					player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = prevStyle;
				});
		};
		packData.onComplete = delegate
		{
			packData.restoreCommands = null;
			SetLookPtn(0, 3);
			SetLookTarget(0, 0);
			ResetActionFlag();
			Animation.StopAllAnimCoroutine();
			Animation.ResetDefaultAnimatorController();
			player.CameraControl.Mode = CameraMode.Normal;
			player.ChaControl.visibleAll = player.CameraControl.ShotType != ShotType.PointOfView;
			cameraBlendStyleChange();
			base.Position = bkData.Position;
			base.Rotation = bkData.Rotation;
			if (AgentData.CarryingItem != null)
			{
				AgentData.CarryingItem = null;
			}
			EnableBehavior();
			ActivateNavMeshAgent();
			packData.Release();
			MapUIContainer.FadeCanvas.StartFade(FadeCanvas.PanelType.Blackout, FadeType.Out, 2f, ignoreTimeScale: true).Subscribe((Action<Unit>)delegate
			{
			}, (System.Action)delegate
			{
				Singleton<Manager.Map>.Instance.EnableEntity(this);
				ChangeBehavior(Desire.ActionType.Normal);
				player.PlayerController.ChangeState("Normal");
				MapUIContainer.SetVisibleHUD(active: true);
				MapUIContainer.StorySupportUI.Open();
			});
		};
		Manager.ADV.ChangeADVCamera(this);
		cameraBlendStyleChange();
		Singleton<Manager.Map>.Instance.DisableEntity(this);
		Observable.EveryUpdate().Skip(1).SkipWhile((long _) => player.CameraControl.CinemachineBrain.IsBlending)
			.Take(1)
			.DelayFrame(5)
			.Subscribe(delegate
			{
				Observable.TimerFrame(30).Subscribe(delegate
				{
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				});
			});
	}

	private void StartPhaseEvent()
	{
		StopNavMeshAgent();
		if (Mode != Desire.ActionType.Encounter)
		{
			_originAvoidancePriority = _navMeshAgent.avoidancePriority;
			ChangeStaticNavMeshAgentAvoidance();
		}
		packData.Init();
		PlayerActor player = Singleton<Manager.Map>.Instance.Player;
		Desire.ActionType prevMode = BehaviorResources.Mode;
		BehaviorResources.ChangeMode(Desire.ActionType.Idle);
		player.CommCompanion = this;
		player.PlayerController.ChangeState("Idle");
		MapUIContainer.SetVisibleHUDExceptStoryUI(active: false);
		MapUIContainer.StorySupportUI.Close();
		int phase = base.ChaControl.fileGameInfo.phase;
		openData.FindLoad($"{phase + 1}", AgentData.param.charaID, 6);
		packData.onComplete = delegate
		{
			packData.restoreCommands = null;
			packData.Release();
			SetLookPtn(0, 3);
			SetLookTarget(0, 0);
			if (AttitudeID == 0)
			{
				Animation.StopAllAnimCoroutine();
				Animation.ResetDefaultAnimatorController();
			}
			CinemachineBlendDefinition.Style prevStyle2 = player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style;
			player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
			player.CameraControl.Mode = CameraMode.Normal;
			player.ChaControl.visibleAll = player.CameraControl.ShotType != ShotType.PointOfView;
			Observable.EveryLateUpdate().Skip(1).Take(1)
				.Subscribe(delegate
				{
					player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = prevStyle2;
				});
			SetPhase(base.ChaControl.fileGameInfo.phase + 1);
			Singleton<Manager.Map>.Instance.EnableEntity(this);
			MapUIContainer.FadeCanvas.StartFade(FadeCanvas.PanelType.Blackout, FadeType.Out, 2f, ignoreTimeScale: true).Subscribe((Action<Unit>)delegate
			{
			}, (System.Action)delegate
			{
				if (Mode != Desire.ActionType.Encounter)
				{
					RecoverNavMeshAgentAvoidance();
				}
				BehaviorResources.ChangeMode(prevMode);
				player.PlayerController.ChangeState("Normal");
				MapUIContainer.SetVisibleHUD(active: true);
				MapUIContainer.StorySupportUI.Open();
				packData.Release();
			});
		};
		if (MapUIContainer.FadeCanvas.IsFading)
		{
			MapUIContainer.FadeCanvas.SkipFade();
		}
		MapUIContainer.FadeCanvas.StartFade(FadeCanvas.PanelType.Blackout, FadeType.In, 2f, ignoreTimeScale: true).Subscribe((Action<Unit>)delegate
		{
		}, (System.Action)delegate
		{
			Singleton<Manager.Map>.Instance.DisableEntity(this);
			player.CommCompanion = this;
			player.PlayerController.ChangeState("Communication");
			SetActiveOnEquipedItem(active: false);
			Vector3 position = base.Position;
			position.y = 0f;
			Vector3 position2 = player.Position;
			position2.y = 0f;
			base.Rotation = Quaternion.LookRotation(position2 - position);
			PoseKeyPair poseKeyPair = Singleton<Manager.Resources>.Instance.AgentProfile.ADVIdleTable[base.ChaControl.fileParam.personality];
			PlayState playState = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[poseKeyPair.postureID][poseKeyPair.poseID];
			AssetBundleInfo assetBundleInfo = playState.MainStateInfo.AssetBundleInfo;
			ChangeAnimator(assetBundleInfo.assetbundle, assetBundleInfo.asset);
			player.Rotation = Quaternion.LookRotation(position - position2);
			CinemachineBlendDefinition.Style prevStyle = player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style;
			player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
			Manager.ADV.ChangeADVCamera(this);
			Observable.EveryUpdate().Skip(1).Take(1)
				.Subscribe(delegate
				{
					player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = prevStyle;
				});
			Transform target = player.CameraControl.CameraComponent.transform;
			SetLookPtn(1, 3);
			SetLookTarget(1, 0, target);
			Observable.EveryUpdate().Skip(1).SkipWhile((long _) => player.CameraControl.CinemachineBrain.IsBlending)
				.Take(1)
				.DelayFrame(5)
				.Subscribe(delegate
				{
					Observable.TimerFrame(30).Subscribe(delegate
					{
						Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
					});
				});
		});
	}

	private void StartSicknessEvent(int id)
	{
		EndCommonSelection();
		MapUIContainer.CommandList.Visibled = false;
		MapUIContainer.ReserveSystemMenuMode(SystemMenuUI.MenuMode.InventoryEnter);
		SystemMenuUI systemUI = MapUIContainer.SystemMenuUI;
		InventoryUIController inventoryUI = systemUI.InventoryEnterUI;
		AgentProfile agentProfile = Singleton<Manager.Resources>.Instance.AgentProfile;
		Dictionary<int, List<int>> dictionary = DictionaryPool<int, List<int>>.Get();
		int iD = AgentData.SickState.ID;
		ItemIDKeyPair[] array = null;
		array = ((iD == 0 && !AgentData.SickState.UsedMedicine && !AgentData.SickState.Enabled) ? agentProfile.MedicineColdItemList : ((iD == 4 && !AgentData.SickState.Enabled) ? agentProfile.MedicineHurtItemList : (iD switch
		{
			1 => agentProfile.MedicineStomachacheItemList, 
			3 => agentProfile.MedicineHeatStrokeItemList, 
			_ => agentProfile.MedicineNormalItemList, 
		})));
		ItemIDKeyPair[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			ItemIDKeyPair itemIDKeyPair = array2[i];
			if (!dictionary.TryGetValue(itemIDKeyPair.categoryID, out var value))
			{
				List<int> list = ListPool<int>.Get();
				dictionary[itemIDKeyPair.categoryID] = list;
				value = list;
			}
			value.Add(itemIDKeyPair.itemID);
		}
		inventoryUI.isConfirm = true;
		inventoryUI.CountViewerVisible(isVisible: false);
		inventoryUI.EmptyTextAutoVisible(isVisible: true);
		InventoryFacadeViewer.ItemFilter[] itemFilter = dictionary.Select((KeyValuePair<int, List<int>> x) => new InventoryFacadeViewer.ItemFilter(x.Key, x.Value.ToArray())).ToArray();
		inventoryUI.SetItemFilter(itemFilter);
		int[] array3 = dictionary.Keys.ToArray();
		int[] array4 = array3;
		foreach (int key in array4)
		{
			ListPool<int>.Release(dictionary[key]);
		}
		DictionaryPool<int, List<int>>.Release(dictionary);
		inventoryUI.itemList = () => Singleton<Manager.Map>.Instance.Player.PlayerData.ItemList;
		inventoryUI.itemList_System = null;
		inventoryUI.DoubleClickAction(null);
		bool isVisibleCommand = true;
		inventoryUI.OnSubmit = delegate(StuffItem item)
		{
			if (Singleton<Game>.Instance.Dialog != null)
			{
				Singleton<Game>.Instance.Dialog.TimeScale = 1f;
			}
			isVisibleCommand = false;
			inventoryUI?.OnClose?.Invoke();
			MapUIContainer.SetActiveCommandList(active: false);
			Desire.ActionType prevMode = BehaviorResources.Mode;
			BehaviorResources.ChangeMode(Desire.ActionType.Idle);
			Observable.EveryLateUpdate().Take(20).Subscribe(delegate
			{
				StopNavMeshAgent();
			});
			PlayerActor player = Singleton<Manager.Map>.Instance.Player;
			player.PlayerController.ChangeState("Idle");
			PoseKeyPair medicID = Singleton<Manager.Resources>.Instance.AgentProfile.PoseIDTable.MedicID;
			if (item.MatchItem(agentProfile.ColdMedicineID) || item.MatchItem(agentProfile.FeverReducerID))
			{
				if (id == 0)
				{
					openData.FindLoad("0", charaID, 11);
					packData.onComplete = delegate
					{
						packData.restoreCommands = null;
						SetLookPtn(0, 3);
						SetLookTarget(0, 0);
						if (AttitudeID == 0)
						{
							Animation.StopAllAnimCoroutine();
							Animation.ResetDefaultAnimatorController();
						}
						ClearItems();
						ClearParticles();
						if (item.MatchItem(agentProfile.ColdMedicineID))
						{
							AgentData.SickState.ID = -1;
							SetStatus(0, 50f);
							AgentData.ColdLockInfo.Lock = true;
							ApplySituationResultParameter(0);
						}
						else if (item.MatchItem(agentProfile.FeverReducerID))
						{
							AgentData.SickState.UsedMedicine = true;
							int num2 = UnityEngine.Random.Range(0, 100);
							if (num2 < 30)
							{
								AgentData.SickState.ID = -1;
								SetStatus(0, 50f);
								AgentData.ColdLockInfo.Lock = true;
								ApplySituationResultParameter(0);
							}
						}
						EnableBehavior();
						BehaviorResources.ChangeMode(prevMode);
						if (AgentData.TalkMotivation <= 0f)
						{
							Debug.Log("会話のやる気が0になった");
							AgentData.LockTalk = true;
							AgentData.TalkElapsedTime = 0f;
							if (player.Mode == Desire.ActionType.Date && player.Partner == this)
							{
								DeactivatePairing(0);
								ActivateHoldingHands(0, enabled: false);
							}
						}
						player.CameraControl.Mode = CameraMode.Normal;
						player.CameraControl.VanishControl.VisibleForceVanish(v: true);
						player.CameraControl.VanishControl.ResetVanish();
						if (Singleton<Manager.Housing>.IsInstance())
						{
							Singleton<Manager.Housing>.Instance.EndShield();
						}
						player.PlayerController.ChangeState("Normal");
						MapUIContainer.SetVisibleHUD(active: true);
						packData.Release();
					};
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
				else if (id == 1)
				{
					CinemachineBlendDefinition.Style prevStyle = player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style;
					player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
					Manager.ADV.ChangeADVFixedAngleCamera(this, 2);
					Observable.EveryLateUpdate().Skip(1).Take(1)
						.Subscribe(delegate
						{
							player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = prevStyle;
						});
					Observable.FromCoroutine(() => AnimCoroutine(medicID.postureID, medicID.poseID)).Subscribe(delegate
					{
						if (item.MatchItem(agentProfile.ColdMedicineID))
						{
							AgentData.SickState.Enabled = true;
							AgentData.SickState.UsedMedicine = true;
							if (Mode == Desire.ActionType.Cold2B)
							{
								openData.FindLoad("1", charaID, 11);
							}
							else if (Mode == Desire.ActionType.Cold3B)
							{
								openData.FindLoad("2", charaID, 11);
							}
						}
						else if (item.MatchItem(agentProfile.FeverReducerID))
						{
							int num = UnityEngine.Random.Range(0, 100);
							if (num < 30)
							{
								AgentData.SickState.Enabled = true;
							}
							AgentData.SickState.UsedMedicine = true;
							if (Mode == Desire.ActionType.Cold2B)
							{
								openData.FindLoad("1", charaID, 11);
							}
							else if (Mode == Desire.ActionType.Cold3B)
							{
								openData.FindLoad("2", charaID, 11);
							}
						}
						packData.onComplete = delegate
						{
							packData.restoreCommands = null;
							SetLookPtn(0, 3);
							SetLookTarget(0, 0);
							ClearItems();
							ClearParticles();
							EnableBehavior();
							if (Mode == Desire.ActionType.Cold2B)
							{
								BehaviorResources.ChangeMode(Mode);
								if (AgentData.SickState.Enabled)
								{
									AIProject.SaveData.Sickness sickState = AgentData.SickState;
									sickState.Duration = sickState.ElapsedTime + TimeSpan.FromHours(12.0);
								}
							}
							else if (Mode == Desire.ActionType.Cold3B)
							{
								BehaviorResources.ChangeMode(Mode);
								if (AgentData.SickState.Enabled)
								{
									AIProject.SaveData.Sickness sickState2 = AgentData.SickState;
									if (sickState2.ElapsedTime.Days >= 6)
									{
										sickState2.Duration = sickState2.ElapsedTime + TimeSpan.FromHours(1.0);
									}
								}
							}
							player.CameraControl.Mode = CameraMode.Normal;
							player.CameraControl.VanishControl.VisibleForceVanish(v: true);
							player.CameraControl.VanishControl.ResetVanish();
							if (Singleton<Manager.Housing>.IsInstance())
							{
								Singleton<Manager.Housing>.Instance.EndShield();
							}
							player.PlayerController.ChangeState("Normal");
							MapUIContainer.SetVisibleHUD(active: true);
							packData.Release();
						};
						Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
					});
				}
			}
			else if (id == 0)
			{
				openData.FindLoad("3", charaID, 11);
				packData.onComplete = delegate
				{
					StatusProfile statusProfile2 = Singleton<Manager.Resources>.Instance.StatusProfile;
					if (item.ID == 1)
					{
						AgentData.ItemList.Add(item);
						if (AgentData.SickState.ID == 4)
						{
							AgentData.SickState.Enabled = true;
						}
					}
					else if (item.ID == 2)
					{
						AddStatus(6, statusProfile2.PotionImmoralAdd);
					}
					else if (item.ID == 3)
					{
						int desireKey3 = Desire.GetDesireKey(Desire.Type.Toilet);
						AddDesire(desireKey3, statusProfile2.DiureticToiletAdd);
					}
					else if (item.ID == 4)
					{
						int desireKey4 = Desire.GetDesireKey(Desire.Type.Sleep);
						AddDesire(desireKey4, statusProfile2.PillSleepAdd);
					}
					else if (item.ID == 8)
					{
						AgentData.SickState.ID = -1;
					}
					else if (item.ID == 9)
					{
						AgentData.SickState.ID = -1;
						AgentData.HeatStrokeLockInfo.Lock = true;
						SetStatus(0, 50f);
					}
					packData.restoreCommands = null;
					SetLookPtn(0, 3);
					SetLookTarget(0, 0);
					if (AttitudeID == 0)
					{
						Animation.StopAllAnimCoroutine();
						Animation.ResetDefaultAnimatorController();
					}
					ClearItems();
					ClearParticles();
					EnableBehavior();
					BehaviorResources.ChangeMode(prevMode);
					if (AgentData.TalkMotivation <= 0f)
					{
						Debug.Log("会話のやる気が0になった");
						AgentData.LockTalk = true;
						AgentData.TalkElapsedTime = 0f;
						if (player.Mode == Desire.ActionType.Date && player.Partner == this)
						{
							DeactivatePairing(0);
							ActivateHoldingHands(0, enabled: false);
						}
					}
					player.CameraControl.Mode = CameraMode.Normal;
					player.CameraControl.VanishControl.VisibleForceVanish(v: true);
					player.CameraControl.VanishControl.ResetVanish();
					if (Singleton<Manager.Housing>.IsInstance())
					{
						Singleton<Manager.Housing>.Instance.EndShield();
					}
					player.PlayerController.ChangeState("Normal");
					MapUIContainer.SetVisibleHUD(active: true);
					packData.Release();
				};
				Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
			}
			else if (id == 1)
			{
				openData.FindLoad("4", charaID, 11);
				packData.onComplete = delegate
				{
					StatusProfile statusProfile = Singleton<Manager.Resources>.Instance.StatusProfile;
					if (item.ID == 2)
					{
						AddStatus(6, statusProfile.PotionImmoralAdd);
					}
					else if (item.ID == 3)
					{
						int desireKey = Desire.GetDesireKey(Desire.Type.Toilet);
						AddDesire(desireKey, statusProfile.DiureticToiletAdd);
					}
					else if (item.ID == 4)
					{
						int desireKey2 = Desire.GetDesireKey(Desire.Type.Sleep);
						AddDesire(desireKey2, statusProfile.PillSleepAdd);
					}
					packData.restoreCommands = null;
					SetLookPtn(0, 3);
					SetLookTarget(0, 0);
					if (AttitudeID == 0)
					{
						Animation.StopAllAnimCoroutine();
						Animation.ResetDefaultAnimatorController();
					}
					ClearItems();
					ClearParticles();
					EnableBehavior();
					BehaviorResources.ChangeMode(prevMode);
					if (AgentData.TalkMotivation <= 0f)
					{
						Debug.Log("会話のやる気が0になった");
						AgentData.LockTalk = true;
						AgentData.TalkElapsedTime = 0f;
						if (player.Mode == Desire.ActionType.Date && player.Partner == this)
						{
							DeactivatePairing(0);
							ActivateHoldingHands(0, enabled: false);
						}
					}
					player.CameraControl.Mode = CameraMode.Normal;
					player.CameraControl.VanishControl.VisibleForceVanish(v: true);
					player.CameraControl.VanishControl.ResetVanish();
					if (Singleton<Manager.Housing>.IsInstance())
					{
						Singleton<Manager.Housing>.Instance.EndShield();
					}
					player.PlayerController.ChangeState("Normal");
					MapUIContainer.SetVisibleHUD(active: true);
					packData.Release();
				};
				Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
			}
		};
		inventoryUI.OnClose = delegate
		{
			inventoryUI.OnSubmit = null;
			inventoryUI.IsActiveControl = false;
			systemUI.IsActiveControl = false;
			if (isVisibleCommand)
			{
				MapUIContainer.CommandList.Visibled = true;
				StartCommonSelection();
			}
			Singleton<Manager.Input>.Instance.FocusLevel = 0;
			Singleton<Manager.Input>.Instance.ReserveState(Manager.Input.ValidType.UI);
			inventoryUI.OnClose = null;
		};
		MapUIContainer.SetActiveSystemMenuUI(active: true);
	}

	private void StartScroungeEvent(CommCommandList.CommandInfo[] restoreCommands)
	{
		EndCommonSelection();
		openData.FindLoad("13", charaID, 4);
		packData.onComplete = delegate
		{
			MapUIContainer.CommandList.Visibled = false;
			MapUIContainer.ScroungeUI.OnClose = delegate
			{
				if (!AgentData.ItemScrounge.isEvent)
				{
					packData.onComplete = delegate
					{
						MapUIContainer.CommandList.Visibled = true;
						StartCommonSelection();
					};
					packData.restoreCommands = restoreCommands;
					openData.FindLoad("7", charaID, 0);
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				}
				else
				{
					MapUIContainer.CommandList.Refresh(restoreCommands, MapUIContainer.CommandList.CanvasGroup);
					MapUIContainer.CommandList.Visibled = true;
					StartCommonSelection();
				}
			};
			packData.restoreCommands = null;
			MapUIContainer.ScroungeUI.agent = this;
			MapUIContainer.SetActiveScroungeUI(active: true);
		};
		Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
	}

	private IEnumerator AnimCoroutine(int eventID, int poseID)
	{
		PlayState info = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[eventID][poseID];
		Animation.LoadEventKeyTable(eventID, poseID);
		ActorAnimInfo animInfo = Animation.LoadActionState(eventID, poseID, info);
		DisableActionFlag();
		Animation.StopAllAnimCoroutine();
		Animation.PlayInAnimation(animInfo.inEnableBlend, animInfo.inBlendSec, info.MainStateInfo.FadeOutTime, animInfo.layer);
		while (Animation.PlayingInAnimation)
		{
			yield return null;
		}
		Animation.PlayOutAnimation(animInfo.outEnableBlend, animInfo.outBlendSec, animInfo.layer);
		while (Animation.PlayingOutAnimation)
		{
			yield return null;
		}
	}

	private IEnumerator StealAnimCoroutine(int eventID, int poseID)
	{
		PlayState info = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[eventID][poseID];
		Animation.LoadEventKeyTable(eventID, poseID);
		ActorAnimInfo animInfo = Animation.LoadActionState(eventID, poseID, info);
		DisableActionFlag();
		Animation.StopAllAnimCoroutine();
		Animation.PlayInAnimation(animInfo.inEnableBlend, animInfo.inBlendSec, info.MainStateInfo.FadeOutTime, animInfo.layer);
		while (Animation.PlayingInAnimation)
		{
			yield return null;
		}
		ClearItems();
		ClearParticles();
	}

	public void PopupCommands(CommCommandList.CommandInfo[] infos, System.Action onOpen = null)
	{
		Debug.Log("PopupCommands");
		Singleton<Manager.Input>.Instance.ReserveState(Manager.Input.ValidType.UI);
		Singleton<Manager.Input>.Instance.SetupState();
		MapUIContainer.RefreshCommands(ID, infos);
		MapUIContainer.CommandList.CancelEvent = null;
		MapUIContainer.CommandList.OnOpened = onOpen;
		MapUIContainer.SetActiveCommandList(active: true, CharaName);
		InitCommon();
	}

	public void PopupDateCommands(bool isFirstPerson)
	{
		Debug.Log("PopupDateCommands");
		Singleton<Manager.Input>.Instance.ReserveState(Manager.Input.ValidType.UI);
		Singleton<Manager.Input>.Instance.SetupState();
		if (isFirstPerson)
		{
			MapUIContainer.RefreshCommands(ID, DateCommandOptionInfos);
		}
		else
		{
			MapUIContainer.RefreshCommands(ID, DateCommandOptionInfosTP);
		}
		MapUIContainer.CommandList.CancelEvent = null;
		MapUIContainer.CommandList.OnOpened = delegate
		{
			StartCommonSelection();
			MapUIContainer.CommandList.OnOpened = null;
		};
		MapUIContainer.SetActiveCommandList(active: true, CharaName);
		InitCommon();
	}

	public void VanishCommands()
	{
		EndCommonSelection();
		PlayerActor player = Singleton<Manager.Map>.Instance.Player;
		player.CameraControl.VanishControl.VisibleForceVanish(v: true);
		player.CameraControl.VanishControl.ResetVanish();
		if (Singleton<Manager.Housing>.IsInstance())
		{
			Singleton<Manager.Housing>.Instance.EndShield();
		}
		if (IsEvent)
		{
			IsEvent = false;
		}
		MapUIContainer.SetVisibleHUD(active: true);
		Desire.ActionType mode = player.Mode;
		if (mode == Desire.ActionType.Normal || mode == Desire.ActionType.Date)
		{
			player.CameraControl.Mode = CameraMode.Normal;
			player.Controller.ChangeState("Normal");
		}
		Singleton<Manager.Map>.Instance.Player.ChaControl.visibleAll = true;
		switch (AttitudeID)
		{
		case 0:
			Animation.StopAllAnimCoroutine();
			Animation.ResetDefaultAnimatorController();
			SetActiveOnEquipedItem(active: true);
			ActivateTransfer(move: false);
			EnableBehavior();
			break;
		case 1:
		case 2:
		case 3:
		case 4:
		case 5:
			EnableBehavior();
			_schedule.progress = true;
			break;
		default:
			EnableBehavior();
			break;
		}
		if (Mode != Desire.ActionType.Encounter)
		{
			RecoverNavMeshAgentAvoidance();
		}
		base.ChaControl.ChangeLookEyesPtn(0);
		base.ChaControl.ChangeLookEyesTarget(0);
		base.ChaControl.ChangeLookNeckPtn(3);
		base.ChaControl.ChangeLookNeckTarget(0);
	}

	public bool CanSelectHCommand()
	{
		return IsOnHMesh() && CanHCommand && Game.isAdd01;
	}

	public bool CanSelectHCommand(string[] tagName)
	{
		return IsOnHMesh(tagName) && CanHCommand && Game.isAdd01;
	}

	private bool IsOnHMesh(string[] tagName = null)
	{
		LayerMask hLayer = Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.HLayer;
		Vector3 position = base.Position;
		position.y += 15f;
		int num = Physics.SphereCastNonAlloc(position, 7.5f, Vector3.down, _hits, 25f, hLayer);
		if (num == 0)
		{
			return false;
		}
		bool flag = true;
		for (int i = 0; i < num; i++)
		{
			flag = true;
			RaycastHit raycastHit = _hits[i];
			string text = raycastHit.collider.tag;
			flag = text != "Untagged";
			if (!flag)
			{
				break;
			}
			if (!tagName.IsNullOrEmpty())
			{
				bool flag2 = false;
				foreach (string text2 in tagName)
				{
					if (text == text2)
					{
						flag2 = true;
						break;
					}
				}
				flag = flag2;
			}
			if (flag)
			{
				break;
			}
		}
		return flag;
	}

	public void InitiateHScene(HSceneManager.HEvent hEvent = HSceneManager.HEvent.Normal)
	{
		_recoveryActionPointFromHScene = base.CurrentPoint;
		_recoveryModeFromHScene = Mode;
		_canTalkCache = CanTalk;
		_attitudeIDCache = AttitudeID;
		_useNeckLookCache = UseNeckLook;
		_canHCommandCache = CanHCommand;
		_isSpecialCache = IsSpecial;
		_hPositionIDCache = HPositionID;
		_hPositionSubIDCache = HPositionSubID;
		_obstacleSizeCache = _navMeshObstacle.radius;
		AgentData.ScheduleEnabled = _schedule.enabled;
		AgentData.ScheduleElapsedTime = _schedule.elapsedTime;
		AgentData.ScheduleDuration = _schedule.duration;
		if (base.CurrentPoint != null)
		{
			base.CurrentPoint.SetActiveMapItemObjs(active: true);
			base.CurrentPoint.ReleaseSlot(this);
			base.CurrentPoint = null;
		}
		Singleton<HSceneManager>.Instance.HsceneEnter(this, -1, null, hEvent);
	}

	public void RecoverAction()
	{
		Singleton<Manager.Map>.Instance.Player.PlayerController.CommandArea.RemoveConsiderationObject(_recoveryActionPointFromHScene);
		_recoveryActionPointFromHScene.SetForceImpossible(value: false);
		CanTalk = _canTalkCache;
		AttitudeID = _attitudeIDCache;
		UseNeckLook = _useNeckLookCache;
		CanHCommand = _canHCommandCache;
		IsSpecial = _isSpecialCache;
		HPositionID = _hPositionIDCache;
		HPositionSubID = _hPositionSubIDCache;
		_navMeshObstacle.radius = _obstacleSizeCache;
		TargetInSightActionPoint = _recoveryActionPointFromHScene;
		Mode = _recoveryModeFromHScene;
		_recoveryActionPointFromHScene.SetSlot(this);
		BehaviorResources.ChangeMode(_recoveryModeFromHScene);
	}

	public void InitiateHScene3P(AgentActor main, AgentActor sub)
	{
		if (base.CurrentPoint != null)
		{
			base.CurrentPoint.SetActiveMapItemObjs(active: true);
			base.CurrentPoint.ReleaseSlot(this);
			base.CurrentPoint = null;
		}
		Singleton<HSceneManager>.Instance.HsceneEnter(main, -1, sub);
	}

	private void StartTutorialADV(int id)
	{
		if (!Singleton<Manager.Map>.IsInstance())
		{
			return;
		}
		switch (id)
		{
		case 1:
			packData.onComplete = delegate
			{
				packData.restoreCommands = null;
				PlayerActor player2 = Singleton<Manager.Map>.Instance.Player;
				List<StuffItem> itemList = player2.PlayerData.ItemList;
				FishingDefinePack.IDGroup iDInfo = Singleton<Manager.Resources>.Instance.FishingDefinePack.IDInfo;
				FishingDefinePack.ItemIDPair brokenFishingRod = iDInfo.BrokenFishingRod;
				itemList.AddItem(new StuffItem(brokenFishingRod.CategoryID, brokenFishingRod.ItemID, 1));
				player2.AddTutorialUI(Popup.Tutorial.Type.Collection, groupDisplay: false);
				Manager.Map.SetTutorialProgress(3);
				Singleton<Manager.Map>.Instance.CreateTutorialSearchPoint();
				ChangeTutorialBehavior(AIProject.Definitions.Tutorial.ActionType.FoodRequest);
				EndTutorialADV(changeNormalMode: true);
			};
			break;
		case 2:
			packData.onComplete = delegate
			{
				packData.restoreCommands = null;
				ChangeTutorialBehavior(AIProject.Definitions.Tutorial.ActionType.HeadToBase);
				EndTutorialADV(changeNormalMode: true);
			};
			break;
		case 3:
			packData.onComplete = delegate
			{
				packData.restoreCommands = null;
				PlayerActor player3 = Singleton<Manager.Map>.Instance.Player;
				player3.AddTutorialUI(Popup.Tutorial.Type.BasePoint, groupDisplay: false);
				Manager.Map.SetTutorialProgress(8);
				ChangeTutorialBehavior(AIProject.Definitions.Tutorial.ActionType.GrilledFishRequest);
				EndTutorialADV(changeNormalMode: true);
			};
			break;
		case 4:
			packData.onComplete = delegate
			{
				packData.restoreCommands = null;
				PlayerActor player4 = Singleton<Manager.Map>.Instance.Player;
				List<StuffItem> itemList2 = player4.PlayerData.ItemList;
				FishingDefinePack.ItemIDPair grilledFish = Singleton<Manager.Resources>.Instance.FishingDefinePack.IDInfo.GrilledFish;
				itemList2.RemoveItem(new StuffItem(grilledFish.CategoryID, grilledFish.ItemID, 1));
				Manager.Map.SetTutorialProgress(14);
				Singleton<Manager.Map>.Instance.DestroyTutorialLockArea();
				ChangeTutorialBehavior(AIProject.Definitions.Tutorial.ActionType.HeadToAgit);
				float followGirlWaitTime = Singleton<Manager.Resources>.Instance.CommonDefine.Tutorial.FollowGirlWaitTime;
				Observable.Timer(TimeSpan.FromSeconds(followGirlWaitTime)).Subscribe(delegate
				{
					player4.CameraControl.XAxisValue = player4.Rotation.eulerAngles.y;
					player4.CameraControl.YAxisValue = ((player4.CameraControl.ShotType != ShotType.PointOfView) ? 0.6f : 0.5f);
					EndTutorialADV(changeNormalMode: true);
				});
			};
			break;
		default:
			Debug.LogWarning($"TutorialADV 範囲外のIDが渡された ID[{id}]");
			return;
		}
		PlayerActor player = Singleton<Manager.Map>.Instance.Player;
		ChangeTutorialBehavior(AIProject.Definitions.Tutorial.ActionType.Idle);
		openData.FindLoad($"{id}", charaID, 100);
		packData.Init();
		MapUIContainer.SetVisibleHUDExceptStoryUI(active: false);
		MapUIContainer.StorySupportUI.Close();
		if (id != 4)
		{
			player.CommCompanion = this;
			player.PlayerController.ChangeState("Communication");
			Manager.ADV.ChangeADVCamera(this);
			Vector3 position = player.Position;
			if (Singleton<Manager.Resources>.IsInstance())
			{
				float num = Vector3.Angle(position - base.Position, base.Forward);
				float turnEnableAngle = Singleton<Manager.Resources>.Instance.LocomotionProfile.TurnEnableAngle;
				if (turnEnableAngle <= num)
				{
					Animation.StopAllAnimCoroutine();
					PlayState.AnimStateInfo tutorialPersonalIdleState = GetTutorialPersonalIdleState();
					Animation.PlayTurnAnimation(position, 1f, tutorialPersonalIdleState);
				}
				else
				{
					Transform transform = Locomotor.transform;
					transform.LookAt(position);
					Vector3 eulerAngles = transform.eulerAngles;
					eulerAngles.x = (eulerAngles.z = 0f);
					transform.eulerAngles = eulerAngles;
				}
			}
			else
			{
				Animation.StopAllAnimCoroutine();
				PlayState.AnimStateInfo tutorialPersonalIdleState2 = GetTutorialPersonalIdleState();
				Animation.PlayTurnAnimation(position, 1f, tutorialPersonalIdleState2);
			}
			Transform target = player.CameraControl.CameraComponent.transform;
			SetLookPtn(1, 3);
			SetLookTarget(1, 0, target);
			Observable.EveryUpdate().Skip(1).SkipWhile((long _) => player.CameraControl.CinemachineBrain.IsBlending || Animation.PlayingTurnAnimation)
				.Take(1)
				.DelayFrame(30)
				.Subscribe(delegate
				{
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				});
			return;
		}
		player.PlayerController.ChangeState("Idle");
		MapUIContainer.FadeCanvas.StartFade(FadeCanvas.PanelType.Blackout, FadeType.In, 2f, ignoreTimeScale: true).Subscribe((Action<Unit>)delegate
		{
		}, (System.Action)delegate
		{
			Locomotor.transform.localRotation = Quaternion.Euler(0f, 80f, 0f);
			Vector3 vector = base.Forward * 10f + base.Position;
			if (player.NavMeshAgent.enabled)
			{
				player.NavMeshAgent.Warp(vector);
			}
			else
			{
				player.Position = vector;
			}
			player.CommCompanion = this;
			player.PlayerController.ChangeState("Communication");
			Transform transform2 = player.Locomotor.transform;
			transform2.LookAt(base.Position);
			Vector3 eulerAngles2 = transform2.eulerAngles;
			eulerAngles2.x = (eulerAngles2.z = 0f);
			transform2.eulerAngles = eulerAngles2;
			CinemachineBlendDefinition.Style prevStyle = player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style;
			player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
			Manager.ADV.ChangeADVCamera(this);
			Observable.EveryLateUpdate().Skip(1).Take(1)
				.Subscribe(delegate
				{
					player.CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = prevStyle;
				});
			Transform target2 = player.CameraControl.CameraComponent.transform;
			SetLookPtn(1, 3);
			SetLookTarget(1, 0, target2);
			Observable.EveryUpdate().Skip(1).SkipWhile((long _) => player.CameraControl.CinemachineBrain.IsBlending)
				.Take(1)
				.DelayFrame(5)
				.Subscribe(delegate
				{
					MapUIContainer.FadeCanvas.StartFade(FadeCanvas.PanelType.Blackout, FadeType.Out, 2f, ignoreTimeScale: true).Subscribe((Action<Unit>)delegate
					{
					}, (System.Action)delegate
					{
						Observable.TimerFrame(30).Subscribe(delegate
						{
							Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
						});
					});
				});
		});
	}

	private void EndTutorialADV(bool changeNormalMode)
	{
		if (Singleton<Manager.Map>.IsInstance())
		{
			SetLookPtn(0, 3);
			SetLookTarget(0, 0);
			PlayerActor player = Singleton<Manager.Map>.Instance.Player;
			player.CameraControl.Mode = CameraMode.Normal;
			if (changeNormalMode)
			{
				player.PlayerController.ChangeState("Normal");
				MapUIContainer.SetVisibleHUD(active: true);
			}
			packData.Release();
		}
	}

	private void PresentADV(CommCommandList.CommandInfo[] restoreCommands)
	{
		Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
		EndCommonSelection();
		MapUIContainer.CommandList.Visibled = false;
		MapUIContainer.ReserveSystemMenuMode(SystemMenuUI.MenuMode.InventoryEnter);
		SystemMenuUI systemUI = MapUIContainer.SystemMenuUI;
		InventoryUIController inventoryUI = systemUI.InventoryEnterUI;
		inventoryUI.isConfirm = true;
		inventoryUI.CountViewerVisible(isVisible: false);
		inventoryUI.EmptyTextAutoVisible(isVisible: true);
		AgentProfile agentProfile = Singleton<Manager.Resources>.Instance.AgentProfile;
		inventoryUI.SetItemFilter(agentProfile.PresentItemFilter);
		inventoryUI.itemList = () => Singleton<Manager.Map>.Instance.Player.PlayerData.ItemList;
		inventoryUI.itemList_System = null;
		inventoryUI.DoubleClickAction(null);
		bool isVisibleCommand = true;
		inventoryUI.OnSubmit = delegate(StuffItem item)
		{
			if (Singleton<Game>.Instance.Dialog != null)
			{
				Singleton<Game>.Instance.Dialog.TimeScale = 1f;
			}
			Animation.StopAllAnimCoroutine();
			isVisibleCommand = false;
			inventoryUI?.OnClose();
			if (item != null)
			{
				System.Action value3;
				if (Singleton<Manager.Resources>.Instance.GameInfo.FoodParameterTable.TryGetValue(item.CategoryID, out var value) && value.TryGetValue(item.ID, out var value2))
				{
					AgentData.ItemList.AddItem(item);
				}
				else if (Singleton<Manager.Resources>.Instance.GameInfo.DrinkParameterTable.TryGetValue(item.CategoryID, out value) && value.TryGetValue(item.ID, out value2))
				{
					AgentData.ItemList.AddItem(item);
				}
				else if (presentParameterTable.TryGetValue(item.ID, out value3))
				{
					if (value3 != null)
					{
						value3();
					}
					else
					{
						Debug.LogError(string.Format("{0}:{1}.{2}:{3}:action none", "PresentADV", "item", "ID", item.ID));
					}
				}
			}
			else
			{
				Debug.LogError(string.Format("{0}:{1}[{2}]", "PresentADV", "item", (item != null) ? item.ID.ToString() : "null"));
			}
			openData.FindLoad("0", charaID, 3);
			packData.onComplete = delegate
			{
				CheckTalkForceEnd(restoreCommands);
			};
			Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
		};
		inventoryUI.OnClose = delegate
		{
			inventoryUI.OnSubmit = null;
			inventoryUI.IsActiveControl = false;
			systemUI.IsActiveControl = false;
			if (isVisibleCommand)
			{
				MapUIContainer.CommandList.Visibled = true;
				StartCommonSelection();
			}
			Singleton<Manager.Input>.Instance.FocusLevel = 0;
			Singleton<Manager.Input>.Instance.ReserveState(Manager.Input.ValidType.UI);
			inventoryUI.OnClose = null;
		};
		MapUIContainer.SetActiveSystemMenuUI(active: true);
	}

	private void SearchFavoriteTargetForADV()
	{
		int? num = null;
		using (IEnumerator<KeyValuePair<int, int>> enumerator = (from v in AgentData.FriendlyRelationShipTable
			where v.Key != -99
			orderby v.Value descending
			where v.Value > 0
			select v).GetEnumerator())
		{
			if (enumerator.MoveNext())
			{
				num = enumerator.Current.Key;
			}
		}
		packData.isFavoriteTarget = num.HasValue;
		packData.FavoriteTargetName = string.Empty;
		if (num.HasValue)
		{
			int value = num.Value;
			AgentActor value2;
			if (value == -90)
			{
				packData.FavoriteTargetName = Singleton<Manager.Map>.Instance.Merchant.CharaName;
			}
			else if (Singleton<Manager.Map>.Instance.AgentTable.TryGetValue(value, out value2))
			{
				packData.FavoriteTargetName = value2.CharaName;
			}
		}
	}

	private AgentAdvEventInfo.Param GetAdvEvent(int eventType)
	{
		int num = 6;
		if (AgentData.advEventLimitation.Contains(num))
		{
			return null;
		}
		if (AgentData.SickState.ID != -1)
		{
			return null;
		}
		if (IsBadMood())
		{
			return null;
		}
		int id2 = -1;
		int[] array = null;
		ActionPoint currentPoint = base.CurrentPoint;
		bool flag = currentPoint != null;
		if (flag)
		{
			if (!currentPoint.IDList.IsNullOrEmpty())
			{
				array = currentPoint.IDList;
			}
			else
			{
				id2 = currentPoint.ID;
			}
		}
		foreach (AgentAdvEventInfo.Param param in from x in Singleton<Manager.Resources>.Instance.GameInfo.GetAgentAdvEvents(this).Values.Shuffle()
			orderby x.SortID descending
			select x)
		{
			if (param.EventType != eventType || (flag && param.IsStateEmpty) || AgentData.GetAdvEventCheck(num).Contains(param.FileName) || (param.PlaceIDs.Any() && !param.PlaceIDs.Contains(base.AreaID)))
			{
				continue;
			}
			if (array == null)
			{
				if (!param.IsState(id2, base.ActionID, base.PoseID))
				{
					continue;
				}
			}
			else if (!array.Any((int id) => param.IsState(id, base.ActionID, base.PoseID)))
			{
				continue;
			}
			if ((param.Phases.Any() && !param.Phases.Contains(base.ChaControl.fileGameInfo.phase + 1)) || !param.TimeRound.Check(Singleton<Manager.Map>.Instance.Simulator.Now.Hour) || (param.Weathers.Any() && !param.Weathers.Contains((int)Singleton<Manager.Map>.Instance.Simulator.Weather)))
			{
				continue;
			}
			switch (param.ExpansionID)
			{
			case 0:
				if (Mode == Desire.ActionType.EndTaskPetAnimal && base.LivesWithAnimalSequence)
				{
					break;
				}
				continue;
			case 1:
			{
				int num2 = 89;
				PlayerActor player = Singleton<Manager.Map>.Instance.Player;
				if (player == null)
				{
					continue;
				}
				ActionPoint currentPoint3 = player.CurrentPoint;
				if (currentPoint3 == null)
				{
					continue;
				}
				if (!currentPoint3.IDList.IsNullOrEmpty())
				{
					if (!currentPoint3.IDList.Contains(num2))
					{
						continue;
					}
				}
				else if (currentPoint3.ID != num2)
				{
					continue;
				}
				break;
			}
			case 2:
			{
				ActionPoint currentPoint2 = base.CurrentPoint;
				if (!flag)
				{
					continue;
				}
				EventPivot component = currentPoint2.GetComponent<EventPivot>();
				if (component == null || component.PivotTransform == null)
				{
					continue;
				}
				break;
			}
			}
			return param;
		}
		return null;
	}

	private void InitCommon()
	{
		_ladInfo = new LeaveAloneDisposableInfo();
		_colDisposableList = new List<ColDisposableInfo>();
		string[,] array = new string[2, 2]
		{
			{ "cf_hit_Mune02_s_L", "Chara" },
			{ "cf_hit_Mune02_s_R", "Chara" }
		};
		ChaControl chaControl = base.ChaControl;
		Transform transform = chaControl.objBodyBone.transform;
		int length = array.GetLength(0);
		_touchList = new List<TouchInfo>();
		for (int i = 0; i < length; i++)
		{
			int num = 0;
			GameObject gameObject = transform.FindLoop(array[i, num++]);
			if (!(gameObject == null))
			{
				Collider component = gameObject.GetComponent<Collider>();
				if (!(component == null))
				{
					TouchInfo item = new TouchInfo(gameObject, component, LayerMask.NameToLayer(array[i, num++]));
					_touchList.Add(item);
					_colDisposableList.Add(new ColDisposableInfo(component, OnTouch, OnEnter, OnExit));
				}
			}
		}
	}

	private void StartCommonSelection()
	{
		StartLeaveAlone();
		foreach (ColDisposableInfo colDisposable in _colDisposableList)
		{
			colDisposable.Start();
		}
	}

	private void EndCommonSelection()
	{
		if (_ladInfo != null)
		{
			_ladInfo.End();
		}
		if (!_colDisposableList.IsNullOrEmpty())
		{
			foreach (ColDisposableInfo colDisposable in _colDisposableList)
			{
				colDisposable?.End();
			}
		}
		if (_disposableInfo != null)
		{
			_disposableInfo.End();
		}
	}

	private void StartLeaveAlone()
	{
		_ladInfo.End();
		_ladInfo.Timer = new SingleAssignmentDisposable();
		_ladInfo.Timer.Disposable = Observable.Timer(TimeSpan.FromSeconds(30.0)).Repeat().Subscribe(delegate
		{
			OnLeaveAlone();
		})
			.AddTo(this);
	}

	private void OnLeaveAlone()
	{
		_ladInfo.Timer.Dispose();
		openData.FindLoad("9", charaID, 0);
		packData.onComplete = delegate
		{
			Debug.Log("放置終了");
		};
		packData.restoreCommands = null;
		packData.CommandListVisibleEnabled(enabled: false);
		Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
		Cursor.SetCursor(null, Vector2.zero, CursorMode.ForceSoftware);
		_ladInfo.Wait = new SingleAssignmentDisposable();
		_ladInfo.Wait.Disposable = Observable.FromCoroutine(CommonEventEnd).Subscribe().AddTo(this);
	}

	private void OnTouch()
	{
		TouchCount++;
		Animation.StopAllAnimCoroutine();
		openData.FindLoad("8", charaID, 0);
		packData.onComplete = delegate
		{
			Debug.Log("お触り終了");
		};
		packData.restoreCommands = null;
		packData.CommandListVisibleEnabled(enabled: false);
		Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
		_ladInfo.End();
		Cursor.SetCursor(null, Vector2.zero, CursorMode.ForceSoftware);
		foreach (ColDisposableInfo colDisposable in _colDisposableList)
		{
			colDisposable.End();
		}
		_disposableInfo.End();
		_disposableInfo.Wait = new SingleAssignmentDisposable();
		_disposableInfo.Wait.Disposable = Observable.FromCoroutine(CommonEventEnd).Subscribe().AddTo(this);
	}

	private void OnEnter()
	{
		if (Singleton<Manager.Resources>.IsInstance())
		{
			Texture2D touchCursorTexture = Singleton<Manager.Resources>.Instance.CommonDefine.Icon.TouchCursorTexture;
			Vector2 hotspot = touchCursorTexture.texelSize * 0.5f;
			Cursor.SetCursor(Singleton<Manager.Resources>.Instance.CommonDefine.Icon.TouchCursorTexture, hotspot, CursorMode.ForceSoftware);
		}
	}

	private void OnExit()
	{
		Cursor.SetCursor(null, Vector2.zero, CursorMode.ForceSoftware);
	}

	private IEnumerator CommonEventEnd()
	{
		while (base.ChaControl != null && base.ChaControl.asVoice == null && !Animation.PlayingInAnimation)
		{
			yield return null;
		}
		while ((base.ChaControl != null && base.ChaControl.asVoice != null) || Animation.PlayingInAnimation)
		{
			yield return null;
		}
		base.ChaControl.ChangeLookEyesPtn(0);
		base.ChaControl.ChangeLookEyesTarget(0);
		StartCommonSelection();
	}

	public bool Entered(Vector3 basePosition, float distance, float radiusA, float radiusB, float angle, Vector3 forward)
	{
		if (Mode == Desire.ActionType.Onbu)
		{
			return false;
		}
		if (!TutorialMode)
		{
			Desire.ActionType mode = BehaviorResources.Mode;
			if (mode == Desire.ActionType.Idle || mode == Desire.ActionType.ChaseToTalk || mode == Desire.ActionType.ChaseLesbianH)
			{
				return false;
			}
		}
		if (Mode == Desire.ActionType.SearchActor || Mode == Desire.ActionType.SearchBirthdayGift || Mode == Desire.ActionType.SearchGift || Mode == Desire.ActionType.GiftForceEncounter || Mode == Desire.ActionType.SearchH || Mode == Desire.ActionType.EndTaskH || Mode == Desire.ActionType.SearchRevRape || Mode == Desire.ActionType.ReverseRape || Mode == Desire.ActionType.SearchPlayerToTalk || Mode == Desire.ActionType.WalkWithAgent || Mode == Desire.ActionType.WalkWithAgentFollow)
		{
			return false;
		}
		if (Mode == Desire.ActionType.InviteSleep || Mode == Desire.ActionType.InviteSleepH || Mode == Desire.ActionType.InviteEat || Mode == Desire.ActionType.InviteBreak)
		{
			return false;
		}
		if (distance > radiusA)
		{
			return false;
		}
		Vector3 position = base.Position;
		position.y = 0f;
		float num = angle / 2f;
		float num2 = Vector3.Angle(position - basePosition, forward);
		if (num2 > num)
		{
			return false;
		}
		return true;
	}

	public bool IsReachable(NavMeshAgent nmAgent, float radiusA, float radiusB)
	{
		if (_pathForCalc == null)
		{
			_pathForCalc = new NavMeshPath();
		}
		bool flag = true;
		if (nmAgent.isActiveAndEnabled)
		{
			float radius = ((CommandType != 0) ? radiusB : radiusA);
			if (TutorialMode)
			{
				if (!TutorialCanTalk)
				{
					return false;
				}
				Vector3 dest;
				if (_navMeshAgent.isActiveAndEnabled)
				{
					dest = base.Position;
				}
				else
				{
					Vector3? vector = TargetStoryPoint?.Position;
					dest = ((!vector.HasValue) ? Vector3.zero : vector.Value);
				}
				flag &= IsReachable(nmAgent, dest, radius);
			}
			else if (_navMeshAgent.isActiveAndEnabled)
			{
				flag &= IsReachable(nmAgent, base.Position, radius);
			}
			else if (base.CurrentPoint != null)
			{
				bool flag2 = false;
				foreach (Transform navMeshPoint in base.CurrentPoint.NavMeshPoints)
				{
					flag2 = IsReachable(nmAgent, navMeshPoint.position, radius);
					if (flag2)
					{
						break;
					}
				}
				if (!flag2)
				{
					flag = false;
				}
			}
			else
			{
				flag = false;
			}
		}
		else
		{
			flag = false;
		}
		return flag;
	}

	private bool IsReachable(NavMeshAgent navMeshAgent, Vector3 dest, float radius)
	{
		navMeshAgent.CalculatePath(dest, _pathForCalc);
		if (_pathForCalc.status != 0)
		{
			return false;
		}
		float num = 0f;
		Vector3[] corners = _pathForCalc.corners;
		for (int i = 0; i < corners.Length - 1; i++)
		{
			float num2 = Vector3.Distance(corners[i], corners[i + 1]);
			num += num2;
			if (num > radius)
			{
				return false;
			}
		}
		return true;
	}

	public void SetForceImpossible(bool value, Actor actor)
	{
		IsImpossible = value;
		CommandPartner = actor;
	}

	public bool SetImpossible(bool value, Actor actor)
	{
		if (IsImpossible != value)
		{
			if (value && CommandPartner != null)
			{
				return false;
			}
			IsImpossible = value;
			if (value)
			{
				_elapsedTimeFromLastImpossible = 0f;
				CommandPartner = actor;
			}
			else
			{
				CommandPartner = null;
			}
			return true;
		}
		return false;
	}

	public void EnableBehavior()
	{
		if (BehaviorResources != null && !BehaviorResources.enabled)
		{
			BehaviorResources.enabled = true;
		}
		if (TutorialBehaviorResources != null && !TutorialBehaviorResources.enabled)
		{
			TutorialBehaviorResources.enabled = true;
		}
	}

	public void DisableBehavior()
	{
		if (BehaviorResources != null && BehaviorResources.enabled)
		{
			BehaviorResources.enabled = false;
		}
		if (TutorialBehaviorResources != null && TutorialBehaviorResources.enabled)
		{
			TutorialBehaviorResources.enabled = false;
		}
	}

	private void Awake()
	{
		BehaviorResources = GetComponentInChildren<BehaviorTreeResources>();
		NormalBehaviorTree = BehaviorResources.GetBehaviorTree(Desire.ActionType.Normal);
		BehaviorResources.Initialize();
	}

	protected override void OnStart()
	{
		(from _ in Observable.EveryUpdate().TakeUntilDestroy(base.gameObject)
			where base.isActiveAndEnabled
			select _).Do(delegate
		{
			OnUpdate();
		}, delegate(Exception ex)
		{
			Debug.LogException(ex);
		}).OnErrorRetry(delegate(Exception ex)
		{
			Debug.LogException(ex);
		}).Subscribe(delegate
		{
		}, delegate(Exception ex)
		{
			Debug.LogException(ex);
		});
	}

	public override IEnumerator LoadAsync()
	{
		if (AgentData != null)
		{
			yield return LoadCharAsync(AgentData.CharaFileName);
		}
		else
		{
			yield return LoadCharAsync(string.Empty);
		}
		LoadEquipments();
		if (_chaBodyParts == null)
		{
			_chaBodyParts = new Dictionary<BodyPart, Transform>();
		}
		_chaBodyParts[BodyPart.Body] = base.ChaControl.animBody.transform.FindLoop("cf_J_Hips")?.transform;
		_chaBodyParts[BodyPart.Bust] = base.ChaControl.animBody.transform.FindLoop("cf_J_Mune00")?.transform;
		_chaBodyParts[BodyPart.Head] = base.ChaControl.animBody.transform.FindLoop("N_Head")?.transform;
		_chaBodyParts[BodyPart.LeftFoot] = base.ChaControl.animBody.transform.FindLoop("cf_J_Foot01_L")?.transform;
		_chaBodyParts[BodyPart.RightFoot] = base.ChaControl.animBody.transform.FindLoop("cf_J_Foot01_R")?.transform;
		Animator animator = base.ChaControl.animBody;
		packData = new PackData();
		packData.SetCommandData(Singleton<Game>.Instance?.WorldData?.Environment);
		packData.SetParam(AgentData, Singleton<Game>.Instance?.WorldData?.PlayerData);
		InitCommands();
		FullBodyBipedIK ik = animator.GetComponentInChildren<FullBodyBipedIK>(includeInactive: true);
		GameObject ctrlOld = _animation.gameObject;
		ActorAnimationAgent actorAnimationAgent = _animation.CloneComponent(animator.gameObject);
		actorAnimationAgent.IK = ik;
		actorAnimationAgent.Actor = this;
		actorAnimationAgent.Character = _character;
		actorAnimationAgent.Animator = animator;
		_animation = actorAnimationAgent;
		UnityEngine.Object.Destroy(ctrlOld);
		AssetBundleInfo animABInfo = default(AssetBundleInfo);
		RuntimeAnimatorController rac = Singleton<Manager.Resources>.Instance.Animation.GetCharaAnimator(0, ref animABInfo);
		Animation.SetDefaultAnimatorController(rac);
		Animation.SetAnimatorController(rac);
		Animation.AnimABInfo = animABInfo;
		animator.Play("Locomotion", 0, 0f);
		_character.CharacterAnimation = _animation;
		InitializeIK();
		_controller.StartBehavior();
	}

	public void Load()
	{
		AgentData agentData = AgentData;
		if (agentData != null)
		{
			LoadChar(agentData.CharaFileName);
		}
		else
		{
			LoadChar(string.Empty);
		}
		if (!base.ChaControl.fileGameInfo.gameRegistration)
		{
			base.ChaControl.chaFile.InitGameInfoParam();
			base.ChaControl.fileGameInfo.gameRegistration = true;
			if (!agentData.CharaFileName.IsNullOrEmpty())
			{
				base.ChaControl.chaFile.SaveCharaFile(base.ChaControl.chaFile.charaFileName);
			}
		}
		LoadEquipments();
		if (_chaBodyParts == null)
		{
			_chaBodyParts = new Dictionary<BodyPart, Transform>();
		}
		_chaBodyParts[BodyPart.Body] = base.ChaControl.animBody.transform.FindLoop("cf_J_Hips")?.transform;
		_chaBodyParts[BodyPart.Bust] = base.ChaControl.animBody.transform.FindLoop("cf_J_Mune00")?.transform;
		_chaBodyParts[BodyPart.Head] = base.ChaControl.animBody.transform.FindLoop("N_Head")?.transform;
		_chaBodyParts[BodyPart.LeftFoot] = base.ChaControl.animBody.transform.FindLoop("cf_J_Foot01_L")?.transform;
		_chaBodyParts[BodyPart.RightFoot] = base.ChaControl.animBody.transform.FindLoop("cf_J_Foot01_R")?.transform;
		Animator animBody = base.ChaControl.animBody;
		packData = new PackData();
		packData.SetCommandData(Singleton<Game>.Instance?.WorldData?.Environment);
		packData.SetParam(agentData, Singleton<Game>.Instance?.WorldData?.PlayerData);
		InitCommands();
		FullBodyBipedIK componentInChildren = animBody.GetComponentInChildren<FullBodyBipedIK>(includeInactive: true);
		GameObject obj = _animation.gameObject;
		ActorAnimationAgent actorAnimationAgent = _animation.CloneComponent(animBody.gameObject);
		actorAnimationAgent.IK = componentInChildren;
		actorAnimationAgent.Actor = this;
		actorAnimationAgent.Character = _character;
		actorAnimationAgent.Animator = animBody;
		_animation = actorAnimationAgent;
		UnityEngine.Object.Destroy(obj);
		AssetBundleInfo outInfo = default(AssetBundleInfo);
		RuntimeAnimatorController charaAnimator = Singleton<Manager.Resources>.Instance.Animation.GetCharaAnimator(0, ref outInfo);
		Animation.SetDefaultAnimatorController(charaAnimator);
		Animation.SetAnimatorController(charaAnimator);
		Animation.AnimABInfo = outInfo;
		animBody.Play("Locomotion", 0, 0f);
		_character.CharacterAnimation = _animation;
		InitializeIK();
		_controller.StartBehavior();
	}

	public override void LoadEquipments()
	{
		LoadEquipmentItem(AgentData.EquipedHeadItem, ChaControlDefine.ExtraAccessoryParts.Head);
		LoadEquipmentItem(AgentData.EquipedBackItem, ChaControlDefine.ExtraAccessoryParts.Back);
		LoadEquipmentItem(AgentData.EquipedNeckItem, ChaControlDefine.ExtraAccessoryParts.Neck);
		LoadEquipmentItem(AgentData.EquipedLampItem, ChaControlDefine.ExtraAccessoryParts.Waist);
	}

	public override void EnableEntity()
	{
		if (_modeCache.Item3)
		{
			ActivateNavMeshAgent();
			base.NavMeshAgent.Warp(base.Position);
		}
		else if (base.EventKey == EventType.Move)
		{
			ActivateNavMeshAgent();
			SetDefaultStateHousingItem();
			if (base.CurrentPoint != null)
			{
				OffMeshLink component = base.CurrentPoint.GetComponent<OffMeshLink>();
				if (component != null)
				{
					Transform endTransform = component.endTransform;
					base.NavMeshAgent.Warp(endTransform.position);
					base.Rotation = endTransform.rotation;
				}
				base.CurrentPoint.RemoveBookingUser(this);
				base.CurrentPoint.SetActiveMapItemObjs(active: true);
				base.CurrentPoint.ReleaseSlot(this);
				base.CurrentPoint = null;
			}
			base.EventKey = (EventType)0;
			TargetInSightActionPoint = null;
			Animation.ResetDefaultAnimatorController();
			_modeCache.Item1 = Desire.ActionType.Normal;
			_modeCache.Item2 = Desire.ActionType.Normal;
		}
		else if (base.EventKey == EventType.DoorOpen)
		{
			ActivateNavMeshAgent();
			SetDefaultStateHousingItem();
			if (base.CurrentPoint != null)
			{
				DoorPoint doorPoint = base.CurrentPoint as DoorPoint;
				if (doorPoint != null)
				{
					if (doorPoint.OpenState == DoorPoint.OpenPattern.Close)
					{
						if (doorPoint.OpenType == DoorPoint.OpenTypeState.Right || doorPoint.OpenType == DoorPoint.OpenTypeState.Right90)
						{
							doorPoint.SetOpenState(DoorPoint.OpenPattern.OpenRight, isSelf: true);
						}
						else
						{
							doorPoint.SetOpenState(DoorPoint.OpenPattern.OpenLeft, isSelf: true);
						}
					}
					doorPoint.RemoveBookingUser(this);
				}
				base.CurrentPoint.SetActiveMapItemObjs(active: true);
				base.CurrentPoint.ReleaseSlot(this);
				base.CurrentPoint = null;
			}
			base.EventKey = (EventType)0;
			TargetInSightActionPoint = null;
			Animation.ResetDefaultAnimatorController();
			_modeCache.Item1 = Desire.ActionType.Normal;
			_modeCache.Item2 = Desire.ActionType.Normal;
		}
		else if (base.EventKey == EventType.Toilet || base.EventKey == EventType.DressIn || base.EventKey == EventType.Bath || base.EventKey == EventType.DressOut || AgentData.PlayedDressIn || _modeCache.Item1 == Desire.ActionType.FoundPeeping)
		{
			ActivateNavMeshAgent();
			SetDefaultStateHousingItem();
			if (base.CurrentPoint != null)
			{
				base.CurrentPoint.SetActiveMapItemObjs(active: true);
				base.CurrentPoint.ReleaseSlot(this);
				base.CurrentPoint = null;
			}
			base.ChaControl.ChangeNowCoordinate(reload: true);
			AgentData.BathCoordinateFileName = null;
			base.ChaControl.SetClothesState(0, 0);
			base.ChaControl.SetClothesState(1, 0);
			base.ChaControl.SetClothesState(2, 0);
			base.ChaControl.SetClothesState(3, 0);
			base.ChaControl.SetClothesState(5, 0);
			AgentData.PlayedDressIn = false;
			base.EventKey = (EventType)0;
			TargetInSightActionPoint = null;
			Animation.ResetDefaultAnimatorController();
			_modeCache.Item1 = Desire.ActionType.Normal;
			_modeCache.Item2 = Desire.ActionType.Normal;
		}
		else if (_modeCache.Item1 == Desire.ActionType.Encounter)
		{
			ActivateNavMeshAgent();
			base.EventKey = (EventType)0;
			TargetInSightActionPoint = null;
			Animation.ResetDefaultAnimatorController();
			_modeCache.Item1 = Desire.ActionType.Normal;
			_modeCache.Item2 = Desire.ActionType.Normal;
		}
		Controller.enabled = true;
		base.ChaControl.visibleAll = true;
		AnimationAgent.enabled = true;
		SetActiveOnEquipedItem(active: false);
		EnableBehavior();
		if (_modeCache.Item1 == Desire.ActionType.Normal && _modeCache.Item2 == Desire.ActionType.Normal)
		{
			ResetActionFlag();
			if (_schedule.enabled)
			{
				_schedule.enabled = false;
			}
		}
		Mode = _modeCache.Item1;
		BehaviorResources.ChangeMode(_modeCache.Item2);
		AnimationAgent.EnableItems();
		AnimationAgent.EnableParticleRenderer();
	}

	public override void DisableEntity()
	{
		Desire.ActionType mode = BehaviorResources.Mode;
		bool flag = base.NavMeshAgent.enabled;
		_modeCache = new UnityEx.ValueTuple<Desire.ActionType, Desire.ActionType, bool>(Mode, mode, flag);
		SetActiveOnEquipedItem(active: false);
		Controller.enabled = false;
		if (flag)
		{
			base.NavMeshAgent.enabled = false;
		}
		AnimationAgent.enabled = false;
		base.ChaControl.visibleAll = false;
		if (mode == Desire.ActionType.EndTaskMasturbation || mode == Desire.ActionType.EndTaskLesbianH || mode == Desire.ActionType.EndTaskLesbianMerchantH)
		{
			BehaviorResources.ChangeMode(Desire.ActionType.Idle);
		}
		DisableBehavior();
		AnimationAgent.DisableItems();
		AnimationAgent.DisableParticleRenderer();
	}

	private void OnUpdate()
	{
		_navMeshObstacle.transform.position = base.Position;
		if (!_scaleCtrlInfos.IsNullOrEmpty())
		{
			foreach (ItemScaleInfo scaleCtrlInfo in _scaleCtrlInfos)
			{
				if (scaleCtrlInfo.ScaleMode == 0)
				{
					float shapeBodyValue = base.ChaControl.GetShapeBodyValue(0);
					float num = scaleCtrlInfo.Evaluate(shapeBodyValue);
					scaleCtrlInfo.TargetItem.transform.localScale = new Vector3(num, num, num);
				}
			}
		}
		AgentData agentData = AgentData;
		StatusProfile statusProfile = Singleton<Manager.Resources>.Instance.StatusProfile;
		EnvironmentSimulator simulator = Singleton<Manager.Map>.Instance.Simulator;
		if (simulator.EnabledTimeProgression)
		{
			Weather weather = Singleton<Manager.Map>.Instance.Simulator.Weather;
			if (base.AreaType == MapArea.AreaType.Indoor)
			{
				agentData.Wetness += statusProfile.DrySpeed * Time.deltaTime;
			}
			else
			{
				switch (weather)
				{
				case Weather.Rain:
					agentData.Wetness += statusProfile.WetRateInRain * Time.deltaTime;
					break;
				case Weather.Storm:
					agentData.Wetness += statusProfile.WetRateInStorm * Time.deltaTime;
					break;
				default:
					agentData.Wetness += statusProfile.DrySpeed * Time.deltaTime;
					break;
				}
				agentData.Wetness = Mathf.Clamp(agentData.Wetness, 0f, 100f);
			}
		}
		if (agentData.Wetness >= 100f)
		{
			agentData.IsWet = true;
		}
		if (agentData.Wetness <= 0f)
		{
			agentData.IsWet = false;
		}
		if (base.ChaControl != null)
		{
			float wetRate = Mathf.InverseLerp(0f, 100f, agentData.Wetness);
			base.ChaControl.wetRate = wetRate;
		}
		if (BehaviorResources.enabled && _schedule.enabled && !_schedule.useGameTime && _schedule.progress)
		{
			_schedule.elapsedTime += Time.deltaTime;
			if (_schedule.elapsedTime > _schedule.duration)
			{
				_schedule.enabled = false;
			}
		}
		Vector3 position = base.Position;
		Vector3 position2 = Singleton<Manager.Map>.Instance.Player.Position;
		Vector3 forward = base.Forward;
		position.y = (position2.y = (forward.y = 0f));
		_distanceTweenPlayer = Vector3.Distance(position, position2);
		_heightDistTweenPlayer = Mathf.Abs(Singleton<Manager.Map>.Instance.Player.Position.y - base.Position.y);
		forward.Normalize();
		AgentProfile.PhotoShotRangeParameter photoShotRangeSetting = Singleton<Manager.Resources>.Instance.AgentProfile.PhotoShotRangeSetting;
		Vector3 vector = position + forward * photoShotRangeSetting.sightOffsetZ;
		_angleDiffTweenPlayer = Vector3.Angle(forward, position2 - vector);
		UpdateActionPointCollision();
		UpdateActorSightCollision();
		UpdateActorFarSightCollision();
		UpdateAnimalSightCollision();
		_elapsedTimeFromLastImpossible += Time.deltaTime;
		if (base.NavMeshAgent.isActiveAndEnabled && base.NavMeshAgent.isOnNavMesh)
		{
			AgentData.Position = base.Position;
			AgentData.Rotation = base.Rotation;
		}
		AgentData.ChunkID = base.ChunkID;
		AgentData.ModeType = Mode;
		AgentData.PrevMode = PrevMode;
		AgentData.TutorialModeType = TutorialType;
		if (!TutorialMode)
		{
			UpdateEncounter();
		}
		if (_mapAreaID != null && base.MapArea != null)
		{
			_mapAreaID.Value = base.MapArea.AreaID;
			AgentData.AreaID = base.MapArea.AreaID;
		}
	}

	private void UpdateEncounter()
	{
		IState state = Singleton<Manager.Map>.Instance.Player.PlayerController.State;
		int num = base.ChaControl.fileGameInfo.flavorState[2];
		int surpriseBorder = Singleton<Manager.Resources>.Instance.StatusProfile.SurpriseBorder;
		bool flag = num >= surpriseBorder;
		if (state is Normal)
		{
			if (_modeType == Desire.ActionType.EndTaskMasturbation || _modeType == Desire.ActionType.EndTaskToilet || _modeType == Desire.ActionType.EndTaskBath)
			{
				if (flag && ExistsInvader() && base.CurrentPoint != null && SurprisePoseID.HasValue && CanTalk)
				{
					switch (_modeType)
					{
					case Desire.ActionType.EndTaskMasturbation:
						ReservedMode = Desire.ActionType.Normal;
						break;
					case Desire.ActionType.EndTaskToilet:
						ReservedMode = Desire.ActionType.Normal;
						break;
					case Desire.ActionType.EndTaskBath:
						ReservedMode = Desire.ActionType.GotoDressOut;
						break;
					}
					ChangeBehavior(Desire.ActionType.FoundPeeping);
				}
				return;
			}
			PlayerActor player = Singleton<Manager.Map>.Instance.Player;
			if (Mode != Desire.ActionType.Encounter)
			{
				if (UseNeckLook)
				{
					if ((!IsCloseToPlayer || !_prevCloseToPlayer) && ReleasableCommand && IsFarPlayer && _prevCloseToPlayer)
					{
						base.ChaControl.ChangeLookEyesPtn(0);
						base.ChaControl.ChangeLookEyesTarget(0);
						base.ChaControl.ChangeLookNeckPtn(3);
						base.ChaControl.ChangeLookNeckTarget(0);
					}
				}
				else
				{
					base.ChaControl.ChangeLookEyesPtn(0);
					base.ChaControl.ChangeLookEyesTarget(0);
					base.ChaControl.ChangeLookNeckPtn(3);
					base.ChaControl.ChangeLookNeckTarget(0);
				}
			}
			if (IsCloseToPlayer && !_prevCloseToPlayer && CanTalk && Singleton<Manager.Resources>.Instance.AgentProfile.EncounterWhitelist.Contains(_modeType) && BehaviorResources.Mode != Desire.ActionType.Idle)
			{
				if (!IsTraverseOffMeshLink())
				{
					_prevCloseToPlayer = true;
					ChangeBehavior(Desire.ActionType.Encounter);
					Debug.Log($"UpdateEncounter ChangeBehavior Encounter proc frame : {Time.frameCount}");
				}
			}
			else if (ReleasableCommand && IsFarPlayer && _prevCloseToPlayer)
			{
				_prevCloseToPlayer = false;
			}
		}
		else if (state is Onbu)
		{
			if ((_modeType == Desire.ActionType.EndTaskMasturbation || _modeType == Desire.ActionType.EndTaskToilet || _modeType == Desire.ActionType.EndTaskBath) && flag && ExistsInvader() && base.CurrentPoint != null && SurprisePoseID.HasValue && CanTalk)
			{
				switch (_modeType)
				{
				case Desire.ActionType.EndTaskMasturbation:
					ReservedMode = Desire.ActionType.Normal;
					break;
				case Desire.ActionType.EndTaskToilet:
					ReservedMode = Desire.ActionType.Normal;
					break;
				case Desire.ActionType.EndTaskBath:
					ReservedMode = Desire.ActionType.GotoDressOut;
					break;
				}
				ChangeBehavior(Desire.ActionType.FoundPeeping);
			}
		}
		else
		{
			if (!(state is Photo))
			{
				return;
			}
			if (_modeType == Desire.ActionType.EndTaskMasturbation || _modeType == Desire.ActionType.EndTaskToilet || _modeType == Desire.ActionType.EndTaskBath)
			{
				if (flag && ExistsInvader() && base.CurrentPoint != null && SurprisePoseID.HasValue && CanTalk)
				{
					switch (_modeType)
					{
					case Desire.ActionType.EndTaskMasturbation:
						ReservedMode = Desire.ActionType.Normal;
						break;
					case Desire.ActionType.EndTaskToilet:
						ReservedMode = Desire.ActionType.Normal;
						break;
					case Desire.ActionType.EndTaskBath:
						ReservedMode = Desire.ActionType.GotoDressOut;
						break;
					}
					ChangeBehavior(Desire.ActionType.FoundPeeping);
				}
				return;
			}
			PlayerActor player2 = Singleton<Manager.Map>.Instance.Player;
			if (Mode != Desire.ActionType.Encounter)
			{
				if (UseNeckLook)
				{
					if (IsCloseToPlayer && _prevCloseToPlayer)
					{
						Transform trfHeadParent = player2.ChaControl.cmpBoneBody.targetEtc.trfHeadParent;
						Transform trfTarg = trfHeadParent.transform;
						base.ChaControl.ChangeLookEyesTarget(1, trfTarg);
						base.ChaControl.ChangeLookEyesPtn(1);
						base.ChaControl.ChangeLookNeckTarget(1, trfTarg);
						base.ChaControl.ChangeLookNeckPtn(1);
					}
					else if (ReleasableCommand && IsFarPlayer && _prevCloseToPlayer)
					{
						base.ChaControl.ChangeLookEyesPtn(0);
						base.ChaControl.ChangeLookEyesTarget(0);
						base.ChaControl.ChangeLookNeckPtn(3);
						base.ChaControl.ChangeLookNeckTarget(0);
					}
				}
				else
				{
					base.ChaControl.ChangeLookEyesPtn(0);
					base.ChaControl.ChangeLookEyesTarget(0);
					base.ChaControl.ChangeLookNeckPtn(3);
					base.ChaControl.ChangeLookNeckTarget(0);
				}
			}
			if (IsCloseToPlayer && !_prevCloseToPlayer && CanTalk && Singleton<Manager.Resources>.Instance.AgentProfile.EncounterWhitelist.Contains(_modeType) && BehaviorResources.Mode != Desire.ActionType.Idle)
			{
				if (!IsTraverseOffMeshLink())
				{
					_prevCloseToPlayer = true;
					ChangeBehavior(Desire.ActionType.Encounter);
					Debug.Log($"UpdateEncounter ChangeBehavior Encounter proc frame : {Time.frameCount}");
				}
			}
			else if (ReleasableCommand && IsFarPlayer && _prevCloseToPlayer)
			{
				_prevCloseToPlayer = false;
			}
		}
	}

	protected override IEnumerator LoadCharAsync(string fileName)
	{
		ChaFileControl chaFile;
		if (!fileName.IsNullOrEmpty())
		{
			chaFile = new ChaFileControl();
			if (!chaFile.LoadCharaFile(fileName, 1))
			{
				chaFile = null;
			}
		}
		else
		{
			chaFile = null;
		}
		base.ChaControl = Singleton<Character>.Instance.CreateChara(1, base.gameObject, 0, chaFile);
		base.ChaControl.Load();
		yield return null;
		base.ChaControl.ChangeLookEyesPtn(3);
		base.ChaControl.ChangeLookNeckPtn(3);
		Controller.InitializeFaceLight(base.ChaControl.gameObject);
		yield return null;
	}

	protected void LoadChar(string fileName)
	{
		ChaFileControl chaFileControl;
		if (!fileName.IsNullOrEmpty())
		{
			chaFileControl = new ChaFileControl();
			if (!chaFileControl.LoadCharaFile(fileName, 1))
			{
				chaFileControl = null;
			}
		}
		else
		{
			chaFileControl = null;
		}
		base.ChaControl = Singleton<Character>.Instance.CreateChara(1, base.gameObject, 0, chaFileControl);
		base.ChaControl.Load();
		base.ChaControl.ChangeLookEyesPtn(3);
		base.ChaControl.ChangeLookNeckPtn(3);
		Controller.InitializeFaceLight(base.ChaControl.gameObject);
	}

	public void RefreshWalkStatus(Waypoint[] waypoints)
	{
		AgentController.SearchArea.RefreshQueryPoints();
	}

	public void UpdateActionPointCollision()
	{
		Dictionary<int, CollisionState> actionPointCollisionStateTable = ActionPointCollisionStateTable;
		float viewDistance = Singleton<Manager.Resources>.Instance.AgentProfile.ActionPointSight.ViewDistance;
		foreach (ActionPoint actionPoint in _actionPoints)
		{
			int instanceID = actionPoint.InstanceID;
			if (!actionPointCollisionStateTable.TryGetValue(instanceID, out var value))
			{
				CollisionState collisionState2 = (actionPointCollisionStateTable[instanceID] = CollisionState.None);
				value = collisionState2;
			}
			bool flag = FovCheck(actionPoint);
			if (flag)
			{
				flag &= actionPoint.IsReachable(base.NavMeshAgent, viewDistance);
			}
			if (flag)
			{
				switch (value)
				{
				case CollisionState.None:
				case CollisionState.Exit:
					if (actionPoint.IsNeutralCommand)
					{
						SetCollisionState(actionPointCollisionStateTable, instanceID, CollisionState.Enter);
					}
					break;
				case CollisionState.Enter:
				case CollisionState.Stay:
					SetCollisionState(actionPointCollisionStateTable, instanceID, CollisionState.Stay);
					break;
				}
			}
			else
			{
				switch (value)
				{
				case CollisionState.None:
				case CollisionState.Exit:
					SetCollisionState(actionPointCollisionStateTable, instanceID, CollisionState.None);
					break;
				case CollisionState.Enter:
				case CollisionState.Stay:
					SetCollisionState(actionPointCollisionStateTable, instanceID, CollisionState.Exit);
					break;
				}
			}
		}
	}

	private bool HasCommands(ActionPoint point)
	{
		Actor partner = base.Partner;
		Desire.Type requestedDesire = RequestedDesire;
		EventType eventType = (EventType)0;
		UnityEx.ValueTuple<EventType, Desire.Type>[] valuePairs = Desire.ValuePairs;
		for (int i = 0; i < valuePairs.Length; i++)
		{
			UnityEx.ValueTuple<EventType, Desire.Type> valueTuple = valuePairs[i];
			if (valueTuple.Item2 == requestedDesire)
			{
				eventType = valueTuple.Item1;
				break;
			}
		}
		if (partner == null)
		{
			return point.HasAgentActionPointInfo(eventType);
		}
		return point.ContainsAgentDateActionPointInfo(eventType);
	}

	private bool FovCheck(ActionPoint actionPoint)
	{
		Transform @base = AgentController.transform;
		AgentProfile.ActionPointSightSetting actionPointSight = Singleton<Manager.Resources>.Instance.AgentProfile.ActionPointSight;
		if (actionPointSight.HasEntered(@base, actionPoint.CommandCenter, actionPoint.Radius))
		{
			return true;
		}
		return false;
	}

	public static ActorController GetCapturedInSight(AgentActor agent, List<Actor> actors)
	{
		Actor element = actors.GetElement(UnityEngine.Random.Range(0, actors.Count));
		if (element == null)
		{
			return null;
		}
		if (!IsCaptureInSight(agent, element))
		{
			return null;
		}
		ActorController controller = element.Controller;
		if (controller == null)
		{
			return null;
		}
		return controller;
	}

	private static bool IsCaptureInSight(AgentActor agent, Actor target)
	{
		if (target == null)
		{
			return false;
		}
		Vector3 position = agent.FovTargetPointTable[FovBodyPart.Head].position;
		int num = LayerMask.NameToLayer("Map");
		FovBodyPart[] checkParts = _checkParts;
		foreach (FovBodyPart key in checkParts)
		{
			Transform transform = target.FovTargetPointTable[key];
			Vector3 direction = position - transform.position;
			Ray ray = new Ray(position, direction);
			if (Physics.Raycast(ray, direction.magnitude, 1 << num))
			{
				return false;
			}
		}
		return true;
	}

	public void UpdateActorSightCollision()
	{
		Dictionary<int, CollisionState> actorCollisionStateTable = ActorCollisionStateTable;
		foreach (Actor targetActor in TargetActors)
		{
			int instanceID = targetActor.InstanceID;
			if (!actorCollisionStateTable.TryGetValue(instanceID, out var value))
			{
				CollisionState collisionState2 = (actorCollisionStateTable[instanceID] = CollisionState.None);
				value = collisionState2;
			}
			if (FovCheck(targetActor))
			{
				switch (value)
				{
				case CollisionState.None:
				case CollisionState.Exit:
					SetCollisionState(actorCollisionStateTable, instanceID, CollisionState.Enter);
					break;
				case CollisionState.Enter:
				case CollisionState.Stay:
					SetCollisionState(actorCollisionStateTable, instanceID, CollisionState.Stay);
					break;
				}
			}
			else
			{
				switch (value)
				{
				case CollisionState.None:
				case CollisionState.Exit:
					SetCollisionState(actorCollisionStateTable, instanceID, CollisionState.None);
					break;
				case CollisionState.Enter:
				case CollisionState.Stay:
					SetCollisionState(actorCollisionStateTable, instanceID, CollisionState.Exit);
					break;
				}
			}
		}
	}

	private void SetCollisionState(Dictionary<int, CollisionState> table, int id, CollisionState state)
	{
		table[id] = state;
	}

	private bool FovCheck(Actor actor)
	{
		Transform @base = AgentController.transform;
		AgentProfile.SightSetting characterFarSight = Singleton<Manager.Resources>.Instance.AgentProfile.CharacterFarSight;
		AgentProfile.SightSetting characterNearSight = Singleton<Manager.Resources>.Instance.AgentProfile.CharacterNearSight;
		if (characterFarSight.HasEntered(@base, actor.Position))
		{
			return true;
		}
		if (characterNearSight.HasEntered(@base, actor.Position))
		{
			return true;
		}
		return false;
	}

	public void UpdateActorFarSightCollision()
	{
		Dictionary<int, CollisionState> actorFarCollisionStateTable = ActorFarCollisionStateTable;
		foreach (Actor targetActor in TargetActors)
		{
			int instanceID = targetActor.InstanceID;
			if (!actorFarCollisionStateTable.TryGetValue(base.InstanceID, out var value))
			{
				CollisionState collisionState = CollisionState.None;
				actorFarCollisionStateTable[base.InstanceID] = collisionState;
				value = collisionState;
			}
			if (FovFarCheck(targetActor))
			{
				switch (value)
				{
				case CollisionState.None:
				case CollisionState.Exit:
					SetCollisionState(actorFarCollisionStateTable, instanceID, CollisionState.Enter);
					break;
				case CollisionState.Enter:
				case CollisionState.Stay:
					SetCollisionState(actorFarCollisionStateTable, instanceID, CollisionState.Stay);
					break;
				}
			}
			else
			{
				switch (value)
				{
				case CollisionState.None:
				case CollisionState.Exit:
					SetCollisionState(actorFarCollisionStateTable, instanceID, CollisionState.None);
					break;
				case CollisionState.Enter:
				case CollisionState.Stay:
					SetCollisionState(actorFarCollisionStateTable, instanceID, CollisionState.Exit);
					break;
				}
			}
		}
	}

	private bool FovFarCheck(Actor actor)
	{
		Transform @base = AgentController.transform;
		AgentProfile.SightSetting characterFarSight = Singleton<Manager.Resources>.Instance.AgentProfile.CharacterFarSight;
		if (characterFarSight.HasEntered(@base, actor.Position))
		{
			return true;
		}
		return false;
	}

	public void UpdateAnimalSightCollision()
	{
		Dictionary<int, CollisionState> animalCollisionStateTable = AnimalCollisionStateTable;
		foreach (AnimalBase targetAnimal in TargetAnimals)
		{
			int instanceID = targetAnimal.InstanceID;
			if (!animalCollisionStateTable.TryGetValue(base.InstanceID, out var value))
			{
				CollisionState collisionState = CollisionState.None;
				animalCollisionStateTable[base.InstanceID] = collisionState;
				value = collisionState;
			}
			if (FovCheck(targetAnimal))
			{
				switch (value)
				{
				case CollisionState.None:
				case CollisionState.Exit:
					SetCollisionState(animalCollisionStateTable, instanceID, CollisionState.Enter);
					break;
				case CollisionState.Enter:
				case CollisionState.Stay:
					SetCollisionState(animalCollisionStateTable, instanceID, CollisionState.Stay);
					break;
				}
			}
			else
			{
				switch (value)
				{
				case CollisionState.Enter:
				case CollisionState.Stay:
					SetCollisionState(animalCollisionStateTable, instanceID, CollisionState.Exit);
					break;
				case CollisionState.None:
				case CollisionState.Exit:
					SetCollisionState(animalCollisionStateTable, instanceID, CollisionState.None);
					break;
				}
			}
		}
	}

	private bool FovCheck(AnimalBase animal)
	{
		if (animal == null || animal.transform == null)
		{
			return false;
		}
		Transform @base = AgentController.transform;
		AgentProfile.SightSetting animalSight = Singleton<Manager.Resources>.Instance.AgentProfile.AnimalSight;
		return animalSight.HasEntered(@base, animal.Position, AnimalFovAngleOffsetY);
	}

	public void AddActor(Actor actor)
	{
		if (!TargetActors.Contains(actor))
		{
			TargetActors.Add(actor);
		}
	}

	public bool ExistsInvader()
	{
		Manager.Map instance = Singleton<Manager.Map>.Instance;
		PlayerActor player = instance.Player;
		float num = Vector3.Distance(player.Position, base.Position);
		LocomotionProfile locomotionProfile = Singleton<Manager.Resources>.Instance.LocomotionProfile;
		if (num < locomotionProfile.AccessInvasionRange && ActorCollisionStateTable.TryGetValue(player.InstanceID, out var value) && (value == CollisionState.Enter || value == CollisionState.Stay))
		{
			return true;
		}
		return false;
	}

	public void RemoveActor(Actor actor)
	{
		if (TargetActors.Contains(actor))
		{
			TargetActors.Remove(actor);
		}
	}

	public void AddActionPoint(ActionPoint point)
	{
		if (!_actionPoints.Contains(point))
		{
			_actionPoints.Add(point);
		}
		_actionPointCheckFlagTable[point] = false;
		List<ActionPoint> list = ListPool<ActionPoint>.Get();
		foreach (ActionPoint actionPoint in _actionPoints)
		{
			list.Add(actionPoint);
		}
		SearchTargets = list.ToArray();
		point.Actor = this;
		ListPool<ActionPoint>.Release(list);
	}

	public void RemoveActionPoint(ActionPoint point)
	{
		if (_actionPoints.Contains(point))
		{
			_actionPoints.Remove(point);
		}
		List<ActionPoint> list = ListPool<ActionPoint>.Get();
		foreach (ActionPoint actionPoint in _actionPoints)
		{
			list.Add(actionPoint);
		}
		SearchTargets = list.ToArray();
		point.Actor = null;
		ListPool<ActionPoint>.Release(list);
	}

	public void AddAnimal(AnimalBase animal)
	{
		if (!(animal == null) && !TargetAnimals.Contains(animal))
		{
			TargetAnimals.Add(animal);
		}
	}

	public void RemoveAnimal(AnimalBase animal)
	{
		if (!(animal == null))
		{
			if (TargetAnimals.Contains(animal))
			{
				TargetAnimals.Remove(animal);
			}
			if (AnimalCollisionStateTable.ContainsKey(animal.InstanceID))
			{
				AnimalCollisionStateTable.Remove(animal.InstanceID);
			}
		}
	}

	private void ActivatePairing(int id, bool isDate)
	{
		PlayerActor player = Singleton<Manager.Map>.Instance.Player;
		player.Partner = this;
		BehaviorResources.ChangeMode(Desire.ActionType.Date);
		Mode = Desire.ActionType.Date;
		AgentData.CarryingItem = null;
		base.Partner = player;
		player.Mode = Desire.ActionType.Date;
		Singleton<Manager.Map>.Instance.Player.PlayerController.CommandArea.RefreshCommands();
	}

	public void DeactivatePairing(int id)
	{
		PlayerActor player = Singleton<Manager.Map>.Instance.Player;
		player.Partner = null;
		AgentBehaviorTree current = BehaviorResources.Current;
		BehaviorResources.ChangeMode(Desire.ActionType.Normal);
		Mode = Desire.ActionType.Normal;
		base.Partner = null;
		player.Mode = Desire.ActionType.Normal;
		player.PlayerData.DateEatTrigger = false;
		Singleton<Manager.Map>.Instance.Player.PlayerController.CommandArea.RefreshCommands();
	}

	public void ActivateHoldingHands(int id, bool enabled)
	{
		PlayerActor player = Singleton<Manager.Map>.Instance.Player;
		HandsHolder handsHolder = player.HandsHolder;
		handsHolder.RightHandAnimator = base.ChaControl.animBody;
		handsHolder.RightHandIK = base.ChaControl.fullBodyIK;
		if (handsHolder.RightHandTarget != null)
		{
			if (enabled)
			{
				LocalTargetPosition = handsHolder.TargetTransform.localPosition;
				LocalTargetRotation = handsHolder.TargetTransform.localRotation;
			}
			else
			{
				handsHolder.TargetTransform.localPosition = LocalTargetPosition;
				handsHolder.TargetTransform.localRotation = LocalTargetRotation;
			}
		}
		GameObject gameObject = base.ChaControl.animBody.transform.FindLoop("cf_J_Kosi02");
		handsHolder.RightLookTarget = gameObject.transform;
		if (!Singleton<Manager.Resources>.Instance.LocomotionProfile.HoldingElboTarget.IsNullOrEmpty())
		{
			GameObject gameObject2 = base.ChaControl.animBody.transform.FindLoop(Singleton<Manager.Resources>.Instance.LocomotionProfile.HoldingElboTarget);
			if (gameObject2 != null)
			{
				handsHolder.RightElboTarget = gameObject2.transform;
			}
		}
		handsHolder.MinDistance = Singleton<Manager.Resources>.Instance.AgentProfile.GetOffsetInParty(base.ChaControl.GetShapeBodyValue(0)).magnitude;
		handsHolder.enabled = enabled;
		handsHolder.EnabledHolding = enabled;
		if (!enabled)
		{
			player.OldEnabledHoldingHand = false;
		}
		string height1ParameterName = Singleton<Manager.Resources>.Instance.DefinePack.AnimatorParameter.Height1ParameterName;
		float shapeBodyValue = base.ChaControl.GetShapeBodyValue(0);
		AnimatorControllerParameter[] parameters = player.Animation.Animator.parameters;
		foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
		{
			if (animatorControllerParameter.type == AnimatorControllerParameterType.Float && animatorControllerParameter.name == height1ParameterName)
			{
				player.Animation.Animator.SetFloat(height1ParameterName, shapeBodyValue);
			}
		}
	}

	public void ActivateTransfer(bool move = true)
	{
		int iD = AgentData.SickState.ID;
		if (!Singleton<Manager.Map>.IsInstance())
		{
			return;
		}
		Weather weather = Singleton<Manager.Map>.Instance.Simulator.Weather;
		EquipEventItemInfo itemInfo = null;
		LoadLocomotionAnimation(this, iD, weather, out var info, ref itemInfo);
		if (info == null)
		{
			return;
		}
		ResetEquipEventItem(itemInfo);
		ActorAnimation animation = Animation;
		animation.InitializeStates(info);
		if (animation.Animator == null)
		{
			return;
		}
		AnimatorStateInfo currentAnimatorStateInfo = animation.Animator.GetCurrentAnimatorStateInfo(0);
		bool flag = false;
		PlayState.Info[] stateInfos = info.MainStateInfo.InStateInfo.StateInfos;
		foreach (PlayState.Info info2 in stateInfos)
		{
			if (currentAnimatorStateInfo.shortNameHash == info2.ShortNameStateHash)
			{
				flag = true;
				break;
			}
		}
		if (flag)
		{
			if (info.MaskStateInfo.layer > 0)
			{
				float layerWeight = Animation.Animator.GetLayerWeight(info.MaskStateInfo.layer);
				if (layerWeight == 0f)
				{
					flag = false;
				}
			}
			else
			{
				for (int j = 1; j < animation.Animator.layerCount; j++)
				{
					float layerWeight2 = animation.Animator.GetLayerWeight(j);
					if (layerWeight2 > 0f)
					{
						flag = false;
						break;
					}
				}
			}
		}
		if (flag)
		{
			Animation.InStates.Clear();
			Animation.OutStates.Clear();
			Animation.ActionStates.Clear();
		}
		else
		{
			int layer = info.Layer;
			if (animation.RefsActAnimInfo)
			{
				animation.StopAllAnimCoroutine();
				animation.PlayInLocoAnimation(animation.AnimInfo.endEnableBlend, animation.AnimInfo.endBlendSec, layer);
				animation.RefsActAnimInfo = false;
			}
			else
			{
				bool enableFade = info.MainStateInfo.InStateInfo.EnableFade;
				float fadeSecond = info.MainStateInfo.InStateInfo.FadeSecond;
				animation.StopAllAnimCoroutine();
				animation.PlayInLocoAnimation(enableFade, fadeSecond, layer);
			}
		}
		if (move)
		{
			if (base.NavMeshAgent.isStopped)
			{
				base.NavMeshAgent.isStopped = false;
			}
			if (base.IsKinematic)
			{
				base.IsKinematic = false;
			}
		}
	}

	private void LoadLocomotionAnimation(AgentActor agent, int sickID, Weather weather, out PlayState info, ref EquipEventItemInfo itemInfo)
	{
		LocomotionProfile locomotionProfile = Singleton<Manager.Resources>.Instance.LocomotionProfile;
		AgentProfile agentProfile = Singleton<Manager.Resources>.Instance.AgentProfile;
		StuffItem carryingItem = agent.AgentData.CarryingItem;
		if (carryingItem != null && !agentProfile.CanStandEatItems.Exists((ItemIDKeyPair pair) => pair.categoryID == carryingItem.CategoryID && pair.itemID == carryingItem.ID))
		{
			int cookMoveLocoID = agentProfile.PoseIDTable.CookMoveLocoID;
			info = Singleton<Manager.Resources>.Instance.Animation.AgentLocomotionStateTable[cookMoveLocoID];
			int obonEventItemID = locomotionProfile.ObonEventItemID;
			ActionItemInfo actionItemInfo = Singleton<Manager.Resources>.Instance.Map.EventItemList[obonEventItemID];
			string agentOtherParentName = base.LocomotionProfile.AgentOtherParentName;
			itemInfo = new EquipEventItemInfo
			{
				EventItemID = obonEventItemID,
				ActionItemInfo = actionItemInfo,
				ParentName = agentOtherParentName
			};
			return;
		}
		switch (sickID)
		{
		case 3:
		{
			int locomotionID = agentProfile.PoseIDTable.LocomotionID;
			info = Singleton<Manager.Resources>.Instance.Animation.AgentLocomotionStateTable[locomotionID];
			return;
		}
		case 4:
		{
			int hurtLocoID = agentProfile.PoseIDTable.HurtLocoID;
			info = Singleton<Manager.Resources>.Instance.Animation.AgentLocomotionStateTable[hurtLocoID];
			return;
		}
		}
		StuffItem equipedUmbrellaItem = agent.AgentData.EquipedUmbrellaItem;
		CommonDefine.ItemIDDefines itemIDDefine = Singleton<Manager.Resources>.Instance.CommonDefine.ItemIDDefine;
		if (equipedUmbrellaItem != null && equipedUmbrellaItem.CategoryID == itemIDDefine.UmbrellaID.categoryID && equipedUmbrellaItem.ID == itemIDDefine.UmbrellaID.itemID)
		{
			if (weather == Weather.Rain || weather == Weather.Storm)
			{
				int umbrellaLocoID = agentProfile.PoseIDTable.UmbrellaLocoID;
				info = Singleton<Manager.Resources>.Instance.Animation.AgentLocomotionStateTable[umbrellaLocoID];
				ItemIDKeyPair umbrellaID = Singleton<Manager.Resources>.Instance.CommonDefine.ItemIDDefine.UmbrellaID;
				itemInfo = Singleton<Manager.Resources>.Instance.GameInfo.CommonEquipEventItemTable[umbrellaID.categoryID][umbrellaID.itemID];
				itemInfo.ParentName = locomotionProfile.AgentLocoItemParentName;
			}
			else
			{
				LoadLocoAnimationOtherCond(weather, out info, ref itemInfo);
			}
		}
		else
		{
			LoadLocoAnimationOtherCond(weather, out info, ref itemInfo);
		}
	}

	private void LoadLocoAnimationOtherCond(Weather weather, out PlayState info, ref EquipEventItemInfo itemInfo)
	{
		Manager.Resources instance = Singleton<Manager.Resources>.Instance;
		Desire.ActionType mode = Mode;
		if (mode == Desire.ActionType.EndTaskGift || mode == Desire.ActionType.EndTaskH)
		{
			int mojimojiLocoID = instance.AgentProfile.PoseIDTable.MojimojiLocoID;
			info = instance.Animation.AgentLocomotionStateTable[mojimojiLocoID];
		}
		else
		{
			LoadLocoAnimationDefault(out info);
		}
	}

	private void LoadLocoAnimationDefault(out PlayState info)
	{
		Manager.Resources instance = Singleton<Manager.Resources>.Instance;
		AgentProfile agentProfile = instance.AgentProfile;
		int key = ((!agentProfile.ScrollWhitelist.Contains(Mode)) ? instance.AgentProfile.PoseIDTable.LocomotionID : instance.AgentProfile.PoseIDTable.WalkLocoID);
		instance.Animation.AgentLocomotionStateTable.TryGetValue(key, out info);
	}

	public void ResetLocomotionAnimation(bool move = true)
	{
		ActivateTransfer(move);
	}

	public void UpdateLocomotionSpeed(Waypoint destination)
	{
		int num = 0;
		if (TutorialMode && ((num = Manager.Map.GetTutorialProgress()) == 14 || num == 15) && (TutorialLocomoCaseID == 100 || TutorialLocomoCaseID == 101))
		{
			LocomotionProfile locomotionProfile = Singleton<Manager.Resources>.Instance.LocomotionProfile;
			float speed = base.NavMeshAgent.speed;
			float b = ((TutorialLocomoCaseID != 100) ? locomotionProfile.AgentSpeed.tutorialRunSpeed : locomotionProfile.AgentSpeed.tutorialWalkSpeed);
			base.NavMeshAgent.speed = Mathf.Lerp(speed, b, locomotionProfile.TutorialLerpSpeed);
		}
		else
		{
			float speed2 = base.NavMeshAgent.speed;
			int iD = AgentData.SickState.ID;
			Weather weather = Singleton<Manager.Map>.Instance.Simulator.Weather;
			LoadLocomotionSpeed(destination, iD, weather, out var speed3);
			LocomotionProfile locomotionProfile2 = Singleton<Manager.Resources>.Instance.LocomotionProfile;
			base.NavMeshAgent.speed = Mathf.Lerp(speed2, speed3, locomotionProfile2.LerpSpeed);
		}
	}

	private void LoadLocomotionSpeed(Waypoint destination, int sickID, Weather weather, out float speed)
	{
		AgentProfile agentProfile = Singleton<Manager.Resources>.Instance.AgentProfile;
		StuffItem carryingItem = AgentData.CarryingItem;
		if (carryingItem != null && !agentProfile.CanStandEatItems.Exists((ItemIDKeyPair pair) => pair.categoryID == carryingItem.CategoryID && pair.itemID == carryingItem.ID))
		{
			speed = Singleton<Manager.Resources>.Instance.LocomotionProfile.AgentSpeed.walkSpeed;
			return;
		}
		if (sickID == 3 || sickID == 4)
		{
			speed = Singleton<Manager.Resources>.Instance.LocomotionProfile.AgentSpeed.walkSpeed;
			return;
		}
		StuffItem equipedUmbrellaItem = AgentData.EquipedUmbrellaItem;
		CommonDefine.ItemIDDefines itemIDDefine = Singleton<Manager.Resources>.Instance.CommonDefine.ItemIDDefine;
		if (equipedUmbrellaItem != null && equipedUmbrellaItem.CategoryID == itemIDDefine.UmbrellaID.categoryID && equipedUmbrellaItem.ID == itemIDDefine.UmbrellaID.itemID)
		{
			if (weather == Weather.Rain || weather == Weather.Storm)
			{
				speed = Singleton<Manager.Resources>.Instance.LocomotionProfile.AgentSpeed.walkSpeed;
			}
			else
			{
				LoadLocomotionSpeedOtherCondition(destination, sickID, weather, out speed);
			}
		}
		else
		{
			LoadLocomotionSpeedOtherCondition(destination, sickID, weather, out speed);
		}
	}

	private void LoadLocomotionSpeedOtherCondition(Waypoint destination, int sickID, Weather weather, out float speed)
	{
		LocomotionProfile locomotionProfile = Singleton<Manager.Resources>.Instance.LocomotionProfile;
		Desire.ActionType mode = Mode;
		if (mode == Desire.ActionType.EndTaskGift || mode == Desire.ActionType.EndTaskH)
		{
			speed = locomotionProfile.AgentSpeed.walkSpeed;
			return;
		}
		if (weather == Weather.Rain || weather == Weather.Storm)
		{
			speed = locomotionProfile.AgentSpeed.runSpeed;
			return;
		}
		if (Mode == Desire.ActionType.Normal)
		{
			if (base.MapArea != null && destination != null && destination.OwnerArea != null)
			{
				if (destination.OwnerArea.AreaID == base.MapArea.AreaID)
				{
					speed = locomotionProfile.AgentSpeed.walkSpeed;
				}
				else
				{
					speed = locomotionProfile.AgentSpeed.runSpeed;
				}
			}
			else
			{
				speed = locomotionProfile.AgentSpeed.walkSpeed;
			}
			return;
		}
		float num = AgentData.StatsTable[5];
		float num2 = num * Singleton<Manager.Resources>.Instance.AgentProfile.MustRunMotivationPercent;
		int desireKey = Desire.GetDesireKey(RequestedDesire);
		float? motivation = GetMotivation(desireKey);
		if (motivation.HasValue && motivation.Value < num2)
		{
			speed = locomotionProfile.AgentSpeed.runSpeed;
		}
		else if (base.MapArea != null)
		{
			int areaID = base.MapArea.AreaID;
			if (TargetInSightActionPoint != null && TargetInSightActionPoint.OwnerArea != null)
			{
				if (TargetInSightActionPoint.OwnerArea.AreaID == areaID)
				{
					speed = locomotionProfile.AgentSpeed.walkSpeed;
				}
				else
				{
					speed = locomotionProfile.AgentSpeed.runSpeed;
				}
			}
			else if (destination != null && destination.OwnerArea != null)
			{
				if (destination.OwnerArea.AreaID == areaID)
				{
					speed = locomotionProfile.AgentSpeed.walkSpeed;
				}
				else
				{
					speed = locomotionProfile.AgentSpeed.runSpeed;
				}
			}
			else
			{
				speed = locomotionProfile.AgentSpeed.walkSpeed;
			}
		}
		else
		{
			speed = locomotionProfile.AgentSpeed.walkSpeed;
		}
	}

	public void ChangeBehavior(Desire.ActionType type)
	{
		PrevMode = Mode;
		if (Mode != 0)
		{
			PrevActionMode = Mode;
			if (PrevActionMode != Desire.ActionType.EndTaskWildAnimal)
			{
				AgentData.CheckCatEvent = false;
			}
		}
		Mode = type;
		if (AgentData.CarryingItem != null && type != Desire.ActionType.Encounter && type != Desire.ActionType.SearchEatSpot && type != Desire.ActionType.EndTaskEat && type != Desire.ActionType.EndTaskEatThere)
		{
			AgentData.CarryingItem = null;
		}
		if (type == Desire.ActionType.Normal)
		{
			int actionID = (base.PoseID = -1);
			base.ActionID = actionID;
			StateType = State.Type.Normal;
			base.EventKey = (EventType)0;
			TargetInSightActionPoint = null;
			TargetInSightActor = null;
			base.Partner = null;
			CommandPartner = null;
			ResetActionFlag();
			if (_schedule.enabled)
			{
				_schedule.enabled = false;
			}
			if (IsHealthManager)
			{
				int desireKey = Desire.GetDesireKey(Desire.Type.Break);
				SetDesire(desireKey, 70f);
			}
			ActivateNavMeshAgent();
			ActivateTransfer();
		}
		BehaviorResources.ChangeMode(type);
	}

	public void ChangeReservedBehavior()
	{
		ChangeBehavior(ReservedMode);
	}

	public void StartWeakness()
	{
		AgentData.WeaknessMotivation = AgentData.StatsTable[5];
		ChangeBehavior(Desire.ActionType.WeaknessA);
		CommandPartner = null;
	}

	public void ChangeTutorialBehavior(AIProject.Definitions.Tutorial.ActionType type)
	{
		AIProject.Definitions.Tutorial.ActionType tutorialType = TutorialType;
		AgentData.TutorialModeType = type;
		TutorialType = type;
		if (TutorialBehaviorResources == null)
		{
			return;
		}
		if (TutorialType != tutorialType)
		{
			CommandArea commandArea = ((!Singleton<Manager.Map>.IsInstance()) ? null : Singleton<Manager.Map>.Instance.Player?.PlayerController?.CommandArea);
			if (commandArea != null && commandArea.ContainsConsiderationObject(this))
			{
				commandArea.RefreshCommands();
			}
		}
		TutorialBehaviorResources.ChangeMode(type);
	}

	public void CreateTutorialBehaviorResources()
	{
		if (TutorialBehaviorResources == null)
		{
			Transform transform = new GameObject("TutorialBehaviorResources").transform;
			transform.SetParent(base.transform, worldPositionStays: false);
			TutorialBehaviorResources = transform.GetOrAddComponent<TutorialBehaviorTreeResources>();
			TutorialBehaviorResources.Initialize(this);
		}
	}

	public void ChangeFirstTutorialBehavior()
	{
		if (BehaviorResources != null && BehaviorResources.enabled)
		{
			BehaviorResources.enabled = false;
		}
		ChangeBehavior(Desire.ActionType.Idle);
		PrevMode = Desire.ActionType.Idle;
		TutorialMode = true;
		AgentProfile.TutorialSetting tutorialSetting = null;
		if (Singleton<Manager.Resources>.IsInstance() && (tutorialSetting = Singleton<Manager.Resources>.Instance.AgentProfile.Tutorial) != null)
		{
			AssetBundleInfo outInfo = default(AssetBundleInfo);
			RuntimeAnimatorController charaAnimator = Singleton<Manager.Resources>.Instance.Animation.GetCharaAnimator(tutorialSetting.AnimatorID, ref outInfo);
			Animation.AnimABInfo = outInfo;
			ChangeAnimator(charaAnimator);
			PlayTutorialDefaultStateAnim();
		}
		ChangeTutorialBehavior(AgentData.TutorialModeType);
		CommandArea commandArea = Manager.Map.GetCommandArea();
		if (commandArea != null && commandArea.ContainsConsiderationObject(this))
		{
			commandArea.RefreshCommands();
		}
	}

	public void ChangeFirstNormalBehavior()
	{
		if (TutorialBehaviorResources != null && TutorialBehaviorResources.gameObject != null)
		{
			UnityEngine.Object.Destroy(TutorialBehaviorResources.gameObject);
			TutorialBehaviorResources = null;
		}
		EnableBehavior();
		AssetBundleInfo outInfo = default(AssetBundleInfo);
		RuntimeAnimatorController charaAnimator = Singleton<Manager.Resources>.Instance.Animation.GetCharaAnimator(0, ref outInfo);
		if (charaAnimator != null)
		{
			Animation.AnimABInfo = outInfo;
			ChangeAnimator(charaAnimator);
		}
		TutorialMode = false;
		AIProject.Definitions.Tutorial.ActionType actionType = AIProject.Definitions.Tutorial.ActionType.End;
		AgentData.TutorialModeType = actionType;
		TutorialType = actionType;
		ActivateNavMeshAgent();
		ChangeBehavior(Desire.ActionType.Normal);
	}

	public void PlayTutorialDefaultStateAnim()
	{
		if (!Singleton<Manager.Resources>.IsInstance())
		{
			return;
		}
		PlayState.AnimStateInfo animStateInfo = Singleton<Manager.Resources>.Instance.DefinePack?.AnimatorState?.IdleStateInfo;
		if (animStateInfo != null)
		{
			Animator animator = Animation.Animator;
			if (Animation.isActiveAndEnabled && animator.isActiveAndEnabled)
			{
				animator.CrossFadeInFixedTime(animStateInfo.StateInfos.LastOrDefault().stateName ?? "Locomotion", animStateInfo.FadeSecond, 0, 0f);
			}
		}
	}

	public PlayState.AnimStateInfo GetTutorialPersonalIdleState()
	{
		if (!Singleton<Manager.Resources>.IsInstance())
		{
			return null;
		}
		Dictionary<int, PoseKeyPair> tutorialIdlePoseTable = Singleton<Manager.Resources>.Instance.AgentProfile.TutorialIdlePoseTable;
		if (tutorialIdlePoseTable.IsNullOrEmpty())
		{
			return Singleton<Manager.Resources>.Instance.DefinePack.AnimatorState.IdleStateInfo;
		}
		int key = charaID;
		if (!tutorialIdlePoseTable.TryGetValue(key, out var value) && !tutorialIdlePoseTable.TryGetValue(0, out value))
		{
			return Singleton<Manager.Resources>.Instance.DefinePack.AnimatorState.IdleStateInfo;
		}
		if (!Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable.TryGetValue(value.postureID, out var value2))
		{
			return Singleton<Manager.Resources>.Instance.DefinePack.AnimatorState.IdleStateInfo;
		}
		if (!value2.TryGetValue(value.poseID, out var value3) || value3 == null)
		{
			return Singleton<Manager.Resources>.Instance.DefinePack.AnimatorState.IdleStateInfo;
		}
		return value3.MainStateInfo.InStateInfo;
	}

	public void ElectNextPoint()
	{
		if (!Singleton<Manager.Resources>.Instance.AgentProfile.AfterActionTable.TryGetValue(base.EventKey, out var value))
		{
			return;
		}
		Singleton<Manager.Map>.Instance.ChunkTable.TryGetValue(base.ChunkID, out var value2);
		if ((bool)value2)
		{
			ActionPoint actionPoint = null;
			float? distance = null;
			NearestActionPoint(value2.ActionPoints, value, ref actionPoint, ref distance);
			NearestActionPoint(value2.AppendActionPoints, value, ref actionPoint, ref distance);
			if (actionPoint != null)
			{
				base.NextPoint = actionPoint;
			}
		}
	}

	private void NearestActionPoint(List<ActionPoint> actionPoints, EventType ev, ref ActionPoint actionPoint, ref float? distance)
	{
		foreach (ActionPoint actionPoint2 in actionPoints)
		{
			if (actionPoint2.HasAgentActionPointInfo(ev))
			{
				float num = Vector3.Distance(actionPoint2.Position, base.Position);
				if (!distance.HasValue)
				{
					actionPoint = actionPoint2;
					distance = num;
				}
				else if (!(num >= distance.Value))
				{
					actionPoint = actionPoint2;
					distance = num;
				}
			}
		}
	}

	public override void OnMinuteUpdated(TimeSpan deltaTime)
	{
		AgentProfile agentProfile = Singleton<Manager.Resources>.Instance.AgentProfile;
		StatusProfile statusProfile = Singleton<Manager.Resources>.Instance.StatusProfile;
		AgentData agentData = AgentData;
		AgentProfile.DiminuationRates diminuationRate = agentProfile.DiminuationRate;
		AgentProfile.DiminuationRates weaknessDiminuationRate = agentProfile.WeaknessDiminuationRate;
		AgentProfile.DiminuationRates talkMotivationDimRate = agentProfile.TalkMotivationDimRate;
		Dictionary<int, AgentProfile.DiminuationRates> diminMotivationRate = agentProfile.DiminMotivationRate;
		if (Mode == Desire.ActionType.Normal || Mode == Desire.ActionType.CommonSearchBreak || Mode == Desire.ActionType.CommonBreak || Mode == Desire.ActionType.CommonGameThere)
		{
			UpdateDesire(deltaTime, diminMotivationRate);
		}
		float max = agentData.StatsTable[5];
		if (BehaviorResources.enabled)
		{
			float value = agentData.TalkMotivation + talkMotivationDimRate.valueRecovery * (float)deltaTime.TotalMinutes;
			agentData.TalkMotivation = Mathf.Clamp(value, 0f, max);
		}
		if (UpdateMotivation)
		{
			if (Desire.ModeTable.TryGetValue(Mode, out var value2))
			{
				int desireKey = Desire.GetDesireKey(value2);
				float? motivation = GetMotivation(desireKey);
				if (motivation.HasValue)
				{
					float num = diminMotivationRate[desireKey].value * (float)deltaTime.TotalMinutes;
					float value3 = Mathf.Clamp(motivation.Value - num, 0f, max);
					SetMotivation(desireKey, value3);
				}
			}
			MotivationInEncounter -= diminuationRate.value;
			MotivationInEncounter = Mathf.Clamp(MotivationInEncounter, 0f, max);
			AgentData.WeaknessMotivation -= weaknessDiminuationRate.value;
			AgentData.WeaknessMotivation = Mathf.Clamp(AgentData.WeaknessMotivation, 0f, max);
			RuntimeMotivationInPhoto -= diminuationRate.value;
			RuntimeMotivationInPhoto = Mathf.Clamp(RuntimeMotivationInPhoto, 0f, max);
			Threshold diminuationInMasturbation = agentProfile.DiminuationInMasturbation;
			Threshold diminuationInLesbian = agentProfile.DiminuationInLesbian;
			_runtimeMotivationInMasturbation -= diminuationInMasturbation.RandomValue;
			_runtimeMotivationInLesbian -= diminuationInLesbian.RandomValue;
		}
		_runtimeMotivationInMasturbation = Mathf.Clamp(_runtimeMotivationInMasturbation, 0f, 100f);
		_runtimeMotivationInLesbian = Mathf.Clamp(_runtimeMotivationInLesbian, 0f, 100f);
		if (!AgentController.SleepedSchedule && BehaviorResources.enabled && _schedule.enabled && _schedule.useGameTime && _schedule.progress)
		{
			_schedule.elapsedTime += (float)deltaTime.TotalMinutes;
			if (_schedule.elapsedTime > _schedule.duration)
			{
				_schedule.enabled = false;
			}
		}
	}

	public override void OnSecondUpdated(TimeSpan timeSpan)
	{
		OnSickUpdate(timeSpan);
	}

	public StuffItem SelectDrinkItem()
	{
		AgentData agentData = AgentData;
		AgentProfile agentProfile = Singleton<Manager.Resources>.Instance.AgentProfile;
		Dictionary<int, Dictionary<int, Dictionary<int, FoodParameterPacket>>> drinkParameterTable = Singleton<Manager.Resources>.Instance.GameInfo.DrinkParameterTable;
		List<StuffItem> list = ListPool<StuffItem>.Get();
		float num = agentData.StatsTable[0];
		foreach (StuffItem item in agentData.ItemList)
		{
			if (drinkParameterTable.TryGetValue(item.CategoryID, out var value) && value.TryGetValue(item.ID, out var _))
			{
				list.Add(item);
			}
		}
		StuffItem stuffItem3 = null;
		if (num < agentProfile.ColdTempBorder)
		{
			List<StuffItem> list2 = ListPool<StuffItem>.Get();
			foreach (StuffItem stuffItem in list)
			{
				if (agentProfile.LowerTempDrinkItems.Exists((ItemIDKeyPair pair) => pair.categoryID == stuffItem.CategoryID && pair.itemID == stuffItem.ID))
				{
					list2.Add(stuffItem);
				}
			}
			if (!list2.IsNullOrEmpty())
			{
				stuffItem3 = list2.GetElement(UnityEngine.Random.Range(0, list2.Count));
			}
			ListPool<StuffItem>.Release(list2);
		}
		else if (num > agentProfile.HotTempBorder)
		{
			List<StuffItem> list3 = ListPool<StuffItem>.Get();
			foreach (StuffItem stuffItem2 in list)
			{
				if (agentProfile.RaiseTempDrinkItems.Exists((ItemIDKeyPair pair) => pair.categoryID == stuffItem2.CategoryID && pair.itemID == stuffItem2.ID))
				{
					list3.Add(stuffItem2);
				}
			}
			if (!list3.IsNullOrEmpty())
			{
				stuffItem3 = list3.GetElement(UnityEngine.Random.Range(0, list3.Count));
			}
			ListPool<StuffItem>.Release(list3);
		}
		if (stuffItem3 == null)
		{
			stuffItem3 = list.GetElement(UnityEngine.Random.Range(0, list.Count));
		}
		ListPool<StuffItem>.Release(list);
		return new StuffItem(stuffItem3.CategoryID, stuffItem3.ID, 1);
	}

	protected override void LoadEquipedEventItem(EquipEventItemInfo eventItemInfo)
	{
		AssetBundleInfo bundleInfo = eventItemInfo.ActionItemInfo.assetbundleInfo;
		if (bundleInfo.assetbundle.IsNullOrEmpty() || bundleInfo.asset.IsNullOrEmpty() || bundleInfo.manifest.IsNullOrEmpty())
		{
			return;
		}
		GameObject gameObject = CommonLib.LoadAsset<GameObject>(bundleInfo.assetbundle, bundleInfo.asset, clone: false, bundleInfo.manifest);
		if (gameObject != null)
		{
			if (!MapScene.AssetBundlePaths.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == bundleInfo.assetbundle && x.Item2 == bundleInfo.manifest))
			{
				MapScene.AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(bundleInfo.assetbundle, bundleInfo.manifest));
			}
			string parentName = eventItemInfo.ParentName;
			GameObject gameObject2 = base.ChaControl.animBody.transform.FindLoop(parentName);
			if (gameObject2 != null)
			{
				GameObject gameObject3 = UnityEngine.Object.Instantiate(gameObject, gameObject2.transform, worldPositionStays: false);
				gameObject3.gameObject.name = gameObject.gameObject.name;
				gameObject3.transform.localPosition = Vector3.zero;
				gameObject3.transform.localRotation = Quaternion.identity;
				gameObject3.transform.localScale = Vector3.one;
				gameObject3.SetActive(value: true);
				base.EquipedItem = new ItemCache
				{
					EventItemID = eventItemInfo.EventItemID,
					KeyInfo = eventItemInfo.ActionItemInfo,
					AsGameObject = gameObject3
				};
				if (eventItemInfo.ActionItemInfo.existsAnimation)
				{
					Animator component = gameObject3.GetComponent<Animator>();
					RuntimeAnimatorController runtimeAnimatorController = AssetUtility.LoadAsset<RuntimeAnimatorController>(eventItemInfo.ActionItemInfo.animeAssetBundle);
					component.runtimeAnimatorController = runtimeAnimatorController;
					Animation.ItemAnimatorTable[gameObject3.GetInstanceID()] = new ItemAnimInfo
					{
						Animator = component,
						Parameters = component.parameters,
						Sync = true
					};
				}
			}
		}
		else
		{
			Debug.LogError($"イベントアイテム読み込み失敗： バンドルパス[{bundleInfo.assetbundle}] プレハブ[{bundleInfo.asset}] マニフェスト[{bundleInfo.manifest}]");
		}
	}

	public override void LoadEventItems(PlayState playState)
	{
		if (playState.ItemInfoCount <= 0)
		{
			return;
		}
		Manager.Resources instance = Singleton<Manager.Resources>.Instance;
		for (int i = 0; i < playState.ItemInfoCount; i++)
		{
			PlayState.ItemInfo itemInfo = playState.GetItemInfo(i);
			ActionItemInfo value3;
			if (itemInfo.fromEquipedItem)
			{
				ActionPointInfo actionPointInfo = Animation.ActionPointInfo;
				if (instance.GameInfo.SearchEquipEventItemTable.TryGetValue(actionPointInfo.searchAreaID, out var value) && value.TryGetValue(actionPointInfo.gradeValue, out var value2))
				{
					LoadEventItem(value2.EventItemID, itemInfo, value2.ActionItemInfo);
				}
			}
			else if (instance.Map.EventItemList.TryGetValue(itemInfo.itemID, out value3))
			{
				LoadEventItem(itemInfo.itemID, itemInfo, value3);
			}
		}
	}

	public void LoadEventItems(List<PlayState.ItemInfo> itemList)
	{
		Manager.Resources instance = Singleton<Manager.Resources>.Instance;
		foreach (PlayState.ItemInfo item in itemList)
		{
			ActionItemInfo value3;
			if (item.fromEquipedItem)
			{
				ActionPointInfo actionPointInfo = Animation.ActionPointInfo;
				if (instance.GameInfo.SearchEquipEventItemTable.TryGetValue(actionPointInfo.searchAreaID, out var value) && value.TryGetValue(actionPointInfo.gradeValue, out var value2))
				{
					LoadEventItem(value2.EventItemID, item, value2.ActionItemInfo);
				}
			}
			else if (instance.Map.EventItemList.TryGetValue(item.itemID, out value3))
			{
				LoadEventItem(item.itemID, item, value3);
			}
		}
	}

	public override void LoadEventParticles(int eventID, int poseID)
	{
		if (Singleton<Manager.Resources>.Instance.Animation.AgentActParticleEventKeyTable.TryGetValue(eventID, out var value) && value.TryGetValue(poseID, out var value2) && value2 != null)
		{
			LoadEventParticle(value2);
		}
	}

	public void StartMasturbationSequence(HScene.AnimationListInfo info)
	{
		IEnumerator enumerator = (_masturbationEnumerator = StartMasturbationSequenceCoroutine(info));
		_masturbationDisposable = Observable.FromCoroutine(() => enumerator).Subscribe();
	}

	public void StopMasturbationSequence()
	{
		if (_masturbationDisposable == null)
		{
			return;
		}
		base.ChaControl.SetClothesStateAll(0);
		Animation.CanUseMapIK = true;
		Animation.MapIK.MapIK = true;
		if (HParticle != null)
		{
			HParticle.ReleaseObject();
			HParticle = null;
		}
		if (hFlagCtrl != null)
		{
			if (HVoiceCtrl != null)
			{
				hFlagCtrl.RemoveMapHvoices(HVoiceID);
				HVoiceCtrl = null;
			}
			hFlagCtrl.EndProc();
			hFlagCtrl = null;
		}
		if (HItem != null)
		{
			HItem.ReleaseItem();
			HItem = null;
		}
		if (HItemPlace != null)
		{
			UnityEngine.Object.Destroy(HItemPlace);
			HItemPlace = null;
		}
		if (HLayerCtrl != null)
		{
			HLayerCtrl.MapHLayerRemove(HLayerID);
			HLayerCtrl = null;
		}
		if (HYureCtrl != null)
		{
			HYureCtrl.ResetShape(yureID);
			HYureCtrl.RemoveChaInit(yureID);
			HYureCtrl = null;
		}
		_masturbationDisposable.Dispose();
		_masturbationEnumerator = null;
		for (int i = 0; i < MasturbationDisposables.Count; i++)
		{
			MasturbationDisposables[i].Dispose();
		}
		MasturbationDisposables.Clear();
		if (!Singleton<HSceneManager>.IsInstance())
		{
			return;
		}
		foreach (string item in hSceneManager.hashUseAssetBundle)
		{
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
		}
		hSceneManager.hashUseAssetBundle.Clear();
	}

	private IEnumerator StartMasturbationSequenceCoroutine(HScene.AnimationListInfo info)
	{
		Animation.CrossFadeScreen();
		Animator animator = Animation.Animator;
		float motivation = AgentData.StatsTable[5];
		_maxMotivationInMasturbation = (_runtimeMotivationInMasturbation = motivation);
		DefinePack definePack = Singleton<Manager.Resources>.Instance.DefinePack;
		string speedName = definePack.AnimatorParameter.SpeedParameterName;
		hSceneManager = Singleton<HSceneManager>.Instance;
		hScene = ((!(hSceneManager.HSceneSet != null)) ? Singleton<Manager.Resources>.Instance.HSceneTable.HSceneSet.GetComponentInChildren<HScene>(includeInactive: true) : hSceneManager.HSceneSet.GetComponentInChildren<HScene>(includeInactive: true));
		byte state = (byte)info.nFemaleUpperCloths[0];
		byte state2 = (byte)info.nFemaleLowerCloths[0];
		GlobalMethod.SetAllClothState(base.ChaControl, _isUpper: true, state);
		GlobalMethod.SetAllClothState(base.ChaControl, _isUpper: false, state2);
		height = base.ChaControl.GetShapeBodyValue(0);
		breast = base.ChaControl.GetShapeBodyValue(1);
		base.ChaControl.setAnimatorParamFloat("height", height);
		base.ChaControl.setAnimatorParamFloat("breast", breast);
		Animation.CanUseMapIK = false;
		if (abName == null)
		{
			abName = CommonLib.GetAssetBundleNameListFromPath(hSceneManager.strAssetIKListFolder);
		}
		Animation.MapIK.MapIK = false;
		foreach (string item in abName)
		{
			if (!AssetBundleCheck.IsFile(item, info.fileFemale))
			{
				continue;
			}
			Animation.MapIK.LoadData(item, info.fileFemale);
			if (base.ChaControl.getAnimatorStateInfo(0).IsName("Idle"))
			{
				Animation.MapIK.Calc("Idle");
			}
			break;
		}
		yield return null;
		if (HParticle == null)
		{
			HParticle = new HParticleCtrl();
			HParticle.particlePlace = hScene.hParticlePlace;
			HParticle.Init();
		}
		HParticle.RePlaceObject();
		HParticle.ParticleLoad(base.ChaControl.objBodyBone, 1);
		if (Hse == null)
		{
			Hse = new HSeCtrl();
		}
		Hse.Load(hSceneManager.strAssetSEListFolder, info.fileSe, null, base.ChaControl.objBodyBone);
		yield return null;
		if (info.isNeedItem)
		{
			if (HItem == null)
			{
				HItem = new HItemCtrl();
			}
			HItemPlace = new GameObject("HItemPlace");
			HItemPlace.transform.SetParent(base.transform, worldPositionStays: false);
			HItemPlace.transform.position = Vector3.zero;
			HItemPlace.transform.rotation = Quaternion.identity;
			HItem.HItemInit(HItemPlace.transform);
			HItem.LoadItem(info.ActionCtrl.Item1, info.id, null, base.ChaControl.objBodyBone, null, null);
		}
		yield return null;
		hFlagCtrl = hScene.ctrlFlag;
		hFlagCtrl.nowAnimationInfo = info;
		HVoiceID = hFlagCtrl.AddMapHvoices();
		hFlagCtrl.ctrlMapHVoice.TryGetValue(HVoiceID, out HVoiceCtrl);
		hFlagCtrl.MapHvoices[HVoiceID].voiceTrs[0] = base.ChaControl.cmpBoneBody.targetEtc.trfHeadParent;
		hFlagCtrl.MapHvoices[HVoiceID].voiceTrs[1] = null;
		HLayerCtrl = hScene.ctrlLayer;
		HYureCtrl = hScene.ctrlYures[0].CtrlMapH;
		if (HYureCtrl != null)
		{
			yureID = HYureCtrl.AddChaInit(base.ChaControl);
			HYureCtrl.AddLoadInfo(info.id, info.ActionCtrl.Item1, yureID);
		}
		if (HLayerCtrl != null)
		{
			HLayerID = HLayerCtrl.Init(base.ChaControl, hFlagCtrl);
			string animatorName = $"{info.fileFemale}";
			HLayerCtrl.LoadExcel(animatorName, 1, 0, mapH: true, HLayerID);
		}
		if (HVoiceCtrl != null)
		{
			yield return HVoiceCtrl.Init(base.ChaControl.fileParam.personality, 1f, this, 0, 0f, null, -1, musterbation: true);
			hFlagCtrl.MapHvoices[HVoiceID].playStart = 2;
			HVoiceCtrl.SetVoiceList(info.nAnimListInfoID, info.nBreathID, info.id, info.lstSystem);
			HVoiceCtrl.SetBreathVoiceList(new ChaControl[2] { base.ChaControl, null }, 3, info.ActionCtrl.Item2, info.nInitiativeFemale != 0, hSceneManager.isForce);
		}
		MasturbationDisposables.Add(Observable.EveryUpdate().Subscribe(delegate
		{
			AnimatorStateInfo animatorStateInfo = base.ChaControl.getAnimatorStateInfo(0);
			if (Hse != null)
			{
				Hse.Proc(animatorStateInfo, base.ChaControl);
			}
			if (HVoiceCtrl != null)
			{
				HVoiceCtrl.Proc(animatorStateInfo, base.ChaControl);
			}
		}));
		MasturbationDisposables.Add(Observable.EveryLateUpdate().Subscribe(delegate
		{
			if (HYureCtrl != null)
			{
				HYureCtrl.Proc(base.ChaControl.getAnimatorStateInfo(0), yureID);
			}
			if (HLayerCtrl != null)
			{
				HLayerCtrl.MapHProc(HLayerID);
			}
		}));
		yield return null;
		AnimatorControllerParameter[] parameters = Animation.Parameters;
		foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
		{
			if (animatorControllerParameter.name == speedName && animatorControllerParameter.type == AnimatorControllerParameterType.Float)
			{
				animator.SetFloat(speedName, 1f);
			}
		}
		AnimatorStateInfo stateInfo7 = base.ChaControl.getAnimatorStateInfo(0);
		Hse.Proc(stateInfo7, base.ChaControl);
		yield return Observable.Timer(TimeSpan.FromSeconds(2.0)).ToYieldInstruction();
		while (HVoiceCtrl.nowVoices[0].state == HVoiceCtrl.VoiceKind.startVoice || HVoiceCtrl.nowVoices[0].state == HVoiceCtrl.VoiceKind.voice || HVoiceCtrl.nowVoices[1].state == HVoiceCtrl.VoiceKind.startVoice || HVoiceCtrl.nowVoices[1].state == HVoiceCtrl.VoiceKind.voice)
		{
			yield return null;
		}
		SetPlay("WLoop");
		yield return null;
		stateInfo7 = base.ChaControl.getAnimatorStateInfo(0);
		Hse.Proc(stateInfo7, base.ChaControl);
		while (stateInfo7.IsName("WLoop"))
		{
			if (GotoNextLoop(0.8f, "MLoop"))
			{
				yield return null;
				break;
			}
			stateInfo7 = base.ChaControl.getAnimatorStateInfo(0);
			Hse.Proc(stateInfo7, base.ChaControl);
			HVoiceCtrl.Proc(stateInfo7, base.ChaControl);
			yield return null;
		}
		stateInfo7 = base.ChaControl.getAnimatorStateInfo(0);
		while (stateInfo7.IsName("MLoop"))
		{
			if (GotoNextLoop(0.6f, "SLoop"))
			{
				yield return null;
				break;
			}
			stateInfo7 = base.ChaControl.getAnimatorStateInfo(0);
			Hse.Proc(stateInfo7, base.ChaControl);
			HVoiceCtrl.Proc(stateInfo7, base.ChaControl);
			yield return null;
		}
		stateInfo7 = base.ChaControl.getAnimatorStateInfo(0);
		Hse.Proc(stateInfo7, base.ChaControl);
		while (stateInfo7.IsName("SLoop"))
		{
			if (GotoNextLoop(0.4f, "OLoop"))
			{
				yield return null;
				break;
			}
			stateInfo7 = base.ChaControl.getAnimatorStateInfo(0);
			Hse.Proc(stateInfo7, base.ChaControl);
			HVoiceCtrl.Proc(stateInfo7, base.ChaControl);
			yield return null;
		}
		stateInfo7 = base.ChaControl.getAnimatorStateInfo(0);
		Hse.Proc(stateInfo7, base.ChaControl);
		while (stateInfo7.IsName("OLoop"))
		{
			float rate = _runtimeMotivationInMasturbation / _maxMotivationInMasturbation;
			if (rate <= 0f)
			{
				SetPlay("Orgasm");
				HParticle.Play(0);
				yield return null;
				break;
			}
			stateInfo7 = base.ChaControl.getAnimatorStateInfo(0);
			Hse.Proc(stateInfo7, base.ChaControl);
			HVoiceCtrl.Proc(stateInfo7, base.ChaControl);
			yield return null;
		}
		stateInfo7 = base.ChaControl.getAnimatorStateInfo(0);
		Hse.Proc(stateInfo7, base.ChaControl);
		while (stateInfo7.IsName("Orgasm"))
		{
			if (stateInfo7.normalizedTime >= 1f)
			{
				SetPlay("Orgasm_A");
				yield return null;
				break;
			}
			stateInfo7 = base.ChaControl.getAnimatorStateInfo(0);
			Hse.Proc(stateInfo7, base.ChaControl);
			HVoiceCtrl.Proc(stateInfo7, base.ChaControl);
			yield return null;
		}
		float timeToEnd = Mathf.Lerp(3f, 5f, UnityEngine.Random.value);
		stateInfo7 = base.ChaControl.getAnimatorStateInfo(0);
		Hse.Proc(stateInfo7, base.ChaControl);
		HVoiceCtrl.Proc(stateInfo7, base.ChaControl);
		while (HVoiceCtrl.nowVoices[0].state == HVoiceCtrl.VoiceKind.startVoice || HVoiceCtrl.nowVoices[0].state == HVoiceCtrl.VoiceKind.voice || HVoiceCtrl.nowVoices[1].state == HVoiceCtrl.VoiceKind.startVoice || HVoiceCtrl.nowVoices[1].state == HVoiceCtrl.VoiceKind.voice)
		{
			yield return null;
		}
		yield return Observable.Timer(TimeSpan.FromSeconds(timeToEnd)).ToYieldInstruction();
		_masturbationEnumerator = null;
	}

	private void SetPlay(string stateName, bool isFade = true)
	{
		Animation.PlayAnimation(stateName, 0, 0f);
		if (Animation.MapIK.data != null)
		{
			Animation.MapIK.Calc(stateName);
		}
		base.ChaControl.setAnimatorParamFloat("height", height);
		base.ChaControl.setAnimatorParamFloat("breast", breast);
		if (!isFade)
		{
			return;
		}
		Animation.CrossFadeScreen(1f);
		switch (stateName)
		{
		case "WLoop":
		case "SLoop":
		case "OLoop":
			Observable.Timer(TimeSpan.FromSeconds(1.0)).Subscribe(delegate
			{
				HvoiceFlagSet();
			});
			break;
		}
	}

	private void HvoiceFlagSet(int mode = 0)
	{
		switch (mode)
		{
		case 0:
			hFlagCtrl.MapHvoices[HVoiceID].playVoices[0] = true;
			hFlagCtrl.MapHvoices[HVoiceID].playShorts[0] = 0;
			break;
		case 1:
			hFlagCtrl.MapHvoices[HVoiceLesID].playVoices[0] = true;
			hFlagCtrl.MapHvoices[HVoiceLesID].playVoices[1] = true;
			hFlagCtrl.MapHvoices[HVoiceLesID].playShorts[0] = 0;
			hFlagCtrl.MapHvoices[HVoiceLesID].playShorts[1] = 0;
			break;
		}
	}

	private bool GotoNextLoop(float range, string nextState)
	{
		float num = _runtimeMotivationInMasturbation / _maxMotivationInMasturbation;
		if (num > range)
		{
			return false;
		}
		SetPlay(nextState);
		return true;
	}

	public void StartLesbianSequence(Actor partner, HScene.AnimationListInfo info)
	{
		if (partner is AgentActor)
		{
			IEnumerator enumerator2 = (_lesbianHEnumerator = StartLesbianSequenceCoroutine(partner, info));
			_lesbianHDisposable = Observable.FromCoroutine(() => enumerator2).Subscribe();
			AgentActor agentActor = partner as AgentActor;
			agentActor.BehaviorResources.ChangeMode(Desire.ActionType.Idle);
			agentActor.Mode = Desire.ActionType.Idle;
		}
		else
		{
			IEnumerator enumerator = (_lesbianHEnumerator = StartLesbianSequenceCoroutine(partner, info));
			_lesbianHDisposable = Observable.FromCoroutine(() => enumerator).Subscribe();
			MerchantActor merchantActor = partner as MerchantActor;
			merchantActor.ChangeBehavior(Merchant.ActionType.HWithAgent);
		}
	}

	public void StopLesbianSequence()
	{
		if (_lesbianHDisposable == null)
		{
			return;
		}
		for (int i = 0; i < lesChars.Length; i++)
		{
			lesChars[i].SetClothesStateAll(0);
		}
		for (int j = 0; j < lesCharAnimes.Length; j++)
		{
			if (!(lesCharAnimes[j] == null))
			{
				lesCharAnimes[j].CanUseMapIK = true;
				lesCharAnimes[j].MapIK.MapIK = true;
			}
		}
		if (HParticle != null)
		{
			HParticle.ReleaseObject();
			HParticle = null;
		}
		if (hFlagCtrl != null)
		{
			if (HVoiceCtrl != null)
			{
				hFlagCtrl.RemoveMapHvoices(HVoiceLesID);
				HVoiceCtrl = null;
			}
			hFlagCtrl.EndProc();
			hFlagCtrl = null;
		}
		if (HLayerCtrl != null)
		{
			for (int k = 0; k < 2; k++)
			{
				HLayerCtrl.MapHLayerRemove(HLayerLesID[k]);
			}
			HLayerCtrl = null;
		}
		for (int l = 0; l < 2; l++)
		{
			if (HYureCtrlLes[l] != null)
			{
				HYureCtrlLes[l].ResetShape(yureLesID[l]);
				HYureCtrlLes[l].RemoveChaInit(yureLesID[l]);
				HYureCtrlLes[l] = null;
			}
		}
		_lesbianHDisposable.Dispose();
		_lesbianHEnumerator = null;
		for (int m = 0; m < lesbianDisposable.Count; m++)
		{
			lesbianDisposable[m].Dispose();
		}
		lesbianDisposable.Clear();
		if (!Singleton<HSceneManager>.IsInstance())
		{
			return;
		}
		foreach (string item in hSceneManager.hashUseAssetBundle)
		{
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
		}
		hSceneManager.hashUseAssetBundle.Clear();
	}

	private IEnumerator StartLesbianSequenceCoroutine(Actor receiver, HScene.AnimationListInfo info)
	{
		lesChars[0] = receiver.ChaControl;
		lesChars[1] = base.ChaControl;
		lesCharAnimes[0] = receiver.Animation;
		lesCharAnimes[1] = Animation;
		Animator[] animator = new Animator[2]
		{
			lesCharAnimes[0].Animator,
			lesCharAnimes[1].Animator
		};
		float motivation = GetComponent<AgentActor>().AgentData.StatsTable[5];
		_maxMotivationInLesbian = (_runtimeMotivationInLesbian = motivation);
		DefinePack definePack = Singleton<Manager.Resources>.Instance.DefinePack;
		string speedName = definePack.AnimatorParameter.SpeedParameterName;
		string height = definePack.AnimatorParameter.HeightParameterName;
		string height2 = definePack.AnimatorParameter.Height1ParameterName;
		string motion = definePack.MapLesDefine.MotionParameterName;
		float changeLoopTime = definePack.MapLesDefine.LoopChangeTime;
		float[] breast = new float[2] { -1f, -1f };
		hSceneManager = Singleton<HSceneManager>.Instance;
		hScene = ((!(hSceneManager.HSceneSet != null)) ? Singleton<Manager.Resources>.Instance.HSceneTable.HSceneSet.GetComponentInChildren<HScene>(includeInactive: true) : hSceneManager.HSceneSet.GetComponentInChildren<HScene>(includeInactive: true));
		for (int i = 0; i < lesChars.Length; i++)
		{
			byte state = (byte)info.nFemaleUpperCloths[i];
			byte state2 = (byte)info.nFemaleLowerCloths[i];
			GlobalMethod.SetAllClothState(lesChars[i], _isUpper: true, state);
			GlobalMethod.SetAllClothState(lesChars[i], _isUpper: false, state2);
		}
		lesCharAnimes[0].CanUseMapIK = false;
		if (abName == null)
		{
			abName = CommonLib.GetAssetBundleNameListFromPath(hSceneManager.strAssetIKListFolder);
		}
		lesCharAnimes[0].MapIK.MapIK = false;
		foreach (string item in abName)
		{
			if (!AssetBundleCheck.IsFile(item, info.fileFemale2))
			{
				continue;
			}
			lesCharAnimes[0].MapIK.LoadData(item, info.fileFemale2);
			lesCharAnimes[0].MapIK.SetPartners(lesCharAnimes[0].MapIK, lesCharAnimes[1].MapIK);
			if (lesChars[0].getAnimatorStateInfo(0).IsName("Idle"))
			{
				lesCharAnimes[0].MapIK.Calc("Idle");
			}
			break;
		}
		lesCharAnimes[1].CanUseMapIK = false;
		if (abName == null)
		{
			abName = CommonLib.GetAssetBundleNameListFromPath(hSceneManager.strAssetIKListFolder);
		}
		lesCharAnimes[1].MapIK.MapIK = false;
		foreach (string item2 in abName)
		{
			if (!AssetBundleCheck.IsFile(item2, info.fileFemale))
			{
				continue;
			}
			lesCharAnimes[1].MapIK.LoadData(item2, info.fileFemale);
			lesCharAnimes[1].MapIK.SetPartners(lesCharAnimes[1].MapIK, lesCharAnimes[0].MapIK);
			if (lesChars[1].getAnimatorStateInfo(0).IsName("Idle"))
			{
				lesCharAnimes[1].MapIK.Calc("Idle");
			}
			break;
		}
		yield return null;
		if (HParticle == null)
		{
			HParticle = new HParticleCtrl();
			HParticle.particlePlace = hScene.hParticlePlace;
			HParticle.Init();
		}
		HParticle.RePlaceObject();
		HParticle.ParticleLoad(receiver.ChaControl.objBodyBone, 1);
		HParticle.ParticleLoad(base.ChaControl.objBodyBone, 3);
		if (Hse == null)
		{
			Hse = new HSeCtrl();
		}
		GameObject[] objBoneFemale = new GameObject[2]
		{
			lesChars[0].objBodyBone,
			lesChars[1].objBodyBone
		};
		Hse.Load(hSceneManager.strAssetSEListFolder, info.fileSe, null, objBoneFemale);
		yield return null;
		if (hFlagCtrl == null)
		{
			hFlagCtrl = hScene.ctrlFlag;
		}
		hFlagCtrl.nowAnimationInfo = info;
		HVoiceLesID = hFlagCtrl.AddMapHvoices();
		hFlagCtrl.ctrlMapHVoice.TryGetValue(HVoiceLesID, out HVoiceCtrl);
		hFlagCtrl.MapHvoices[HVoiceLesID].voiceTrs[0] = receiver.ChaControl.cmpBoneBody.targetEtc.trfHeadParent;
		hFlagCtrl.MapHvoices[HVoiceLesID].voiceTrs[1] = base.ChaControl.cmpBoneBody.targetEtc.trfHeadParent;
		if (HVoiceCtrl != null)
		{
			if (receiver.GetComponent<MerchantActor>() == null)
			{
				yield return HVoiceCtrl.Init(lesChars[0].fileParam.personality, lesChars[0].fileParam.voicePitch, receiver, lesChars[1].fileParam.personality, lesChars[1].fileParam.voicePitch, this, -1, musterbation: false, les: true);
			}
			else
			{
				yield return HVoiceCtrl.Init(-90, lesChars[0].fileParam.voicePitch, receiver, lesChars[1].fileParam.personality, lesChars[1].fileParam.voicePitch, this, -1, musterbation: false, les: true);
			}
			hFlagCtrl.MapHvoices[HVoiceLesID].playStart = 2;
			HVoiceCtrl.SetVoiceList(info.nAnimListInfoID, info.nBreathID, info.id, info.lstSystem);
			HVoiceCtrl.SetBreathVoiceList(lesChars, info.ActionCtrl.Item1, info.ActionCtrl.Item2, info.nInitiativeFemale != 0, hSceneManager.isForce);
		}
		HLayerCtrl = hScene.ctrlLayer;
		HYureCtrlLes[0] = hScene.ctrlYures[0].CtrlMapH;
		HYureCtrlLes[1] = hScene.ctrlYures[1].CtrlMapH;
		for (int j = 0; j < 2; j++)
		{
			if (HYureCtrlLes[j] != null)
			{
				yureLesID[j] = HYureCtrlLes[j].AddChaInit(lesChars[j]);
				HYureCtrlLes[j].AddLoadInfo(info.id, info.ActionCtrl.Item1, yureLesID[j]);
			}
		}
		if (HLayerCtrl != null)
		{
			for (int k = 0; k < 2; k++)
			{
				string animatorName = ((k != 0) ? info.fileFemale2 : info.fileFemale);
				HLayerLesID[k] = HLayerCtrl.Init(lesChars[k], hFlagCtrl);
				HLayerCtrl.LoadExcel(animatorName, 1, 0, mapH: true, HLayerLesID[k]);
			}
		}
		lesbianDisposable.Add(Observable.EveryUpdate().Subscribe(delegate
		{
			AnimatorStateInfo animatorStateInfo2 = base.ChaControl.getAnimatorStateInfo(0);
			if (Hse != null)
			{
				Hse.Proc(animatorStateInfo2, lesChars);
			}
			if (HVoiceCtrl != null)
			{
				HVoiceCtrl.Proc(animatorStateInfo2, lesChars);
			}
			for (int num3 = 0; num3 < lesCharAnimes.Length; num3++)
			{
				AnimatorControllerParameter[] parameters2 = lesCharAnimes[num3].Parameters;
				foreach (AnimatorControllerParameter animatorControllerParameter2 in parameters2)
				{
					if (animatorControllerParameter2.name == motion && animatorControllerParameter2.type == AnimatorControllerParameterType.Float)
					{
						animator[num3].SetFloat(motion, hFlagCtrl.motions[num3]);
					}
				}
			}
			for (int num5 = 0; num5 < lesCharAnimes.Length; num5++)
			{
				if (enableMotions[num5])
				{
					timeMotions[num5] = Mathf.Clamp(timeMotions[num5] + Time.deltaTime, 0f, lerpTimes[num5]);
					float time = Mathf.Clamp01(timeMotions[num5] / lerpTimes[num5]);
					time = hFlagCtrl.changeMotionCurve.Evaluate(time);
					hFlagCtrl.motions[num5] = Mathf.Lerp(lerpMotions[num5].x, lerpMotions[num5].y, time);
					if (time >= 1f)
					{
						enableMotions[num5] = false;
					}
				}
			}
		}));
		lesbianDisposable.Add(Observable.EveryLateUpdate().Subscribe(delegate
		{
			AnimatorStateInfo animatorStateInfo = lesChars[1].getAnimatorStateInfo(0);
			if (HYureCtrlLes[0] != null)
			{
				HYureCtrlLes[0].Proc(animatorStateInfo, yureLesID[0]);
			}
			animatorStateInfo = lesChars[0].getAnimatorStateInfo(0);
			if (HYureCtrlLes[1] != null)
			{
				HYureCtrlLes[1].Proc(animatorStateInfo, yureLesID[1]);
			}
			if (HLayerCtrl != null)
			{
				HLayerCtrl.MapHProc(HLayerLesID[0]);
				HLayerCtrl.MapHProc(HLayerLesID[1]);
			}
		}));
		for (int l = 0; l < lesCharAnimes.Length; l++)
		{
			AnimatorControllerParameter[] parameters = lesCharAnimes[l].Parameters;
			foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
			{
				if (animatorControllerParameter.name == speedName && animatorControllerParameter.type == AnimatorControllerParameterType.Float)
				{
					animator[l].SetFloat(speedName, 1f);
				}
				else if (animatorControllerParameter.name == height && animatorControllerParameter.type == AnimatorControllerParameterType.Float)
				{
					animator[l].SetFloat(height, lesChars[l].GetShapeBodyValue(0));
				}
				else if (animatorControllerParameter.name == height2 && animatorControllerParameter.type == AnimatorControllerParameterType.Float)
				{
					animator[l].SetFloat(height2, lesChars[l ^ 1].GetShapeBodyValue(0));
				}
			}
		}
		for (int n = 0; n < lesChars.Length; n++)
		{
			breast[n] = lesChars[n].GetShapeBodyValue(1);
			lesChars[n].setAnimatorParamFloat("breast", breast[n]);
		}
		AnimatorStateInfo stateInfo5 = base.ChaControl.getAnimatorStateInfo(0);
		yield return Observable.Timer(TimeSpan.FromSeconds(2.0)).ToYieldInstruction();
		while (HVoiceCtrl.nowVoices[0].state == HVoiceCtrl.VoiceKind.startVoice || HVoiceCtrl.nowVoices[0].state == HVoiceCtrl.VoiceKind.voice || HVoiceCtrl.nowVoices[1].state == HVoiceCtrl.VoiceKind.startVoice || HVoiceCtrl.nowVoices[1].state == HVoiceCtrl.VoiceKind.voice)
		{
			yield return null;
		}
		SetPlayLes("WLoop");
		float loopTime = 0f;
		yield return null;
		stateInfo5 = base.ChaControl.getAnimatorStateInfo(0);
		bool isWLoop = stateInfo5.IsName("WLoop");
		bool isSLoop = stateInfo5.IsName("SLoop");
		while (isWLoop || isSLoop)
		{
			if (GotoNextLoopLes(0.4f, "OLoop"))
			{
				yield return null;
				break;
			}
			loopTime += Time.deltaTime;
			if (loopTime > changeLoopTime)
			{
				if (isWLoop)
				{
					SetPlayLes("SLoop");
				}
				else if (isSLoop)
				{
					SetPlayLes("WLoop");
				}
				loopTime = 0f;
			}
			stateInfo5 = base.ChaControl.getAnimatorStateInfo(0);
			isWLoop = stateInfo5.IsName("WLoop");
			isSLoop = stateInfo5.IsName("SLoop");
			for (int num = 0; num < lesCharAnimes.Length; num++)
			{
				timeChangeMotionDeltaTimes[num] += Time.deltaTime;
				if (!(timeChangeMotions[num] <= timeChangeMotionDeltaTimes[num]) || enableMotions[num])
				{
					continue;
				}
				timeChangeMotions[num] = UnityEngine.Random.Range(hFlagCtrl.changeAutoMotionTimeMin, hFlagCtrl.changeAutoMotionTimeMax);
				timeChangeMotionDeltaTimes[num] = 0f;
				enableMotions[num] = true;
				timeMotions[num] = 0f;
				float num2;
				if (allowMotions[num])
				{
					num2 = 1f - hFlagCtrl.motions[num];
					num2 = ((!(num2 <= hFlagCtrl.changeMotionMinRate)) ? (hFlagCtrl.motions[num] + UnityEngine.Random.Range(hFlagCtrl.changeMotionMinRate, num2)) : 1f);
					if (num2 >= 1f)
					{
						allowMotions[num] = false;
					}
				}
				else
				{
					num2 = hFlagCtrl.motions[num];
					num2 = ((!(num2 <= hFlagCtrl.changeMotionMinRate)) ? (hFlagCtrl.motions[num] - UnityEngine.Random.Range(hFlagCtrl.changeMotionMinRate, num2)) : 0f);
					if (num2 <= 0f)
					{
						allowMotions[num] = true;
					}
				}
				ref Vector2 reference = ref lerpMotions[num];
				reference = new Vector2(hFlagCtrl.motions[num], num2);
				lerpTimes[num] = UnityEngine.Random.Range(hFlagCtrl.changeMotionTimeMin, hFlagCtrl.changeMotionTimeMax);
			}
			yield return null;
		}
		stateInfo5 = base.ChaControl.getAnimatorStateInfo(0);
		Hse.Proc(stateInfo5, base.ChaControl);
		while (stateInfo5.IsName("OLoop"))
		{
			float rate = _runtimeMotivationInLesbian / _maxMotivationInLesbian;
			if (rate <= 0f)
			{
				SetPlayLes("Orgasm");
				if (GetFlavorSkill(FlavorSkill.Type.Dirty) >= 100 && info.id == 0)
				{
					HParticle.Play(4);
				}
				AgentActor partner = receiver.GetComponent<AgentActor>();
				if (partner != null && partner.GetFlavorSkill(FlavorSkill.Type.Dirty) >= 100)
				{
					HParticle.Play(0);
				}
				yield return null;
				break;
			}
			yield return null;
		}
		stateInfo5 = base.ChaControl.getAnimatorStateInfo(0);
		Hse.Proc(stateInfo5, base.ChaControl);
		while (stateInfo5.IsName("Orgasm"))
		{
			if (stateInfo5.normalizedTime >= 1f)
			{
				SetPlayLes("Orgasm_A");
				yield return null;
				break;
			}
			stateInfo5 = base.ChaControl.getAnimatorStateInfo(0);
			yield return null;
		}
		float timeToEnd = Mathf.Lerp(3f, 5f, UnityEngine.Random.value);
		yield return Observable.Timer(TimeSpan.FromSeconds(timeToEnd)).ToYieldInstruction();
		while (HVoiceCtrl.nowVoices[0].state == HVoiceCtrl.VoiceKind.startVoice || HVoiceCtrl.nowVoices[0].state == HVoiceCtrl.VoiceKind.voice || HVoiceCtrl.nowVoices[1].state == HVoiceCtrl.VoiceKind.startVoice || HVoiceCtrl.nowVoices[1].state == HVoiceCtrl.VoiceKind.voice)
		{
			yield return null;
		}
		_lesbianHEnumerator = null;
	}

	private void SetPlayLes(string stateName, bool isFade = true)
	{
		for (int i = 0; i < lesCharAnimes.Length; i++)
		{
			lesCharAnimes[i].PlayAnimation(stateName, 0, 0f);
			if (lesCharAnimes[i].MapIK.data != null)
			{
				lesCharAnimes[i].MapIK.Calc(stateName);
			}
		}
		if (!isFade)
		{
			return;
		}
		Animation.CrossFadeScreen(1f);
		switch (stateName)
		{
		case "WLoop":
		case "SLoop":
		case "OLoop":
			Observable.Timer(TimeSpan.FromSeconds(1.0)).Subscribe(delegate
			{
				HvoiceFlagSet(1);
			});
			break;
		}
	}

	private bool GotoNextLoopLes(float range, string nextState)
	{
		float num = _runtimeMotivationInLesbian / _maxMotivationInLesbian;
		if (num > range)
		{
			return false;
		}
		SetPlayLes(nextState);
		return true;
	}

	public override SearchInfo RandomAddItem(Dictionary<int, ItemTableElement> itemTable, bool containsNone)
	{
		Manager.Resources instance = Singleton<Manager.Resources>.Instance;
		SearchInfo searchInfo;
		if (itemTable == null)
		{
			searchInfo = new SearchInfo();
			searchInfo.IsSuccess = false;
			return searchInfo;
		}
		StatusProfile statusProfile = instance.StatusProfile;
		int flavor = base.ChaControl.fileGameInfo.flavorState[3];
		Manager.Resources.GameInfoTables gameInfo = instance.GameInfo;
		int key = Lottery(itemTable);
		if (itemTable.TryGetValue(key, out var value))
		{
			searchInfo = new SearchInfo();
			searchInfo.IsSuccess = true;
			SearchInfo searchInfo2 = searchInfo;
			foreach (ItemTableElement.GatherElement element in value.Elements)
			{
				float value2 = UnityEngine.Random.value;
				StuffItemInfo item = gameInfo.GetItem(element.categoryID, element.itemID);
				float num = element.prob;
				if (item == null)
				{
					continue;
				}
				if (item.Grade == Grade._3)
				{
					float num2 = FlavorVariation(statusProfile.DropBuffMinMax, statusProfile.DropBuff, flavor);
					num += num2;
					if (base.ChaControl.fileGameInfo.normalSkill.ContainsValue(44))
					{
						num += 10f;
					}
				}
				if (value2 < num)
				{
					int num3 = UnityEngine.Random.Range(element.minCount, element.maxCount + 1);
					if (num3 > 0)
					{
						searchInfo2.ItemList.Add(new ItemSearchInfo
						{
							name = item.Name,
							categoryID = element.categoryID,
							id = element.itemID,
							count = num3
						});
					}
				}
			}
			if (searchInfo2.ItemList.IsNullOrEmpty())
			{
				if (base.ChaControl.fileGameInfo.normalSkill.ContainsValue(49))
				{
					List<ItemTableElement.GatherElement> list = ListPool<ItemTableElement.GatherElement>.Get();
					foreach (ItemTableElement.GatherElement element2 in value.Elements)
					{
						if (element2.categoryID == 16 || element2.categoryID == 17)
						{
							list.Add(element2);
						}
					}
					if (!list.IsNullOrEmpty())
					{
						ItemTableElement.GatherElement gatherElement = Lottery(list);
						StuffItemInfo item2 = gameInfo.GetItem(gatherElement.categoryID, gatherElement.itemID);
						if (item2 != null)
						{
							int count = UnityEngine.Random.Range(Mathf.Max(gatherElement.minCount, 1), gatherElement.maxCount + 1);
							searchInfo2.ItemList.Add(new ItemSearchInfo
							{
								name = item2.Name,
								categoryID = gatherElement.categoryID,
								id = gatherElement.itemID,
								count = count
							});
						}
						else
						{
							searchInfo2.IsSuccess = false;
						}
					}
					else
					{
						searchInfo2.IsSuccess = false;
					}
					ListPool<ItemTableElement.GatherElement>.Release(list);
				}
				else
				{
					searchInfo2.IsSuccess = false;
				}
			}
			return searchInfo2;
		}
		searchInfo = new SearchInfo();
		searchInfo.IsSuccess = false;
		return searchInfo;
	}

	public override void OnDayUpdated(TimeSpan timeSpan)
	{
		SetAdvEventLimitationResetReserve();
	}

	public void SetAdvEventLimitationResetReserve()
	{
		if (advEventResetDisposable != null)
		{
			advEventResetDisposable.Dispose();
		}
		advEventResetDisposable = Observable.EveryUpdate().TakeUntilDestroy(base.gameObject).Subscribe(delegate
		{
			if (Singleton<Manager.Map>.Instance.IsHour7After)
			{
				AgentData.advEventLimitation.Remove(6);
				if (advEventResetDisposable != null)
				{
					advEventResetDisposable.Dispose();
				}
				advEventResetDisposable = null;
			}
		});
	}

	public Waypoint[] GetReservedWaypoints()
	{
		return _reservedWaypoints.ToArray();
	}

	public Waypoint[] GetReservedNearActionWaypoints()
	{
		return _reservedNearActionWaypoints.ToArray();
	}

	public void ClearReservedNearActionWaypoints()
	{
		_reservedNearActionWaypoints.Clear();
	}

	public Waypoint[] GetReservedDateNearActionWaypoints()
	{
		return _reservedDateNearActionWaypoints.ToArray();
	}

	public Waypoint[] GetReservedLocationWaypoints()
	{
		return _reservedLocationWaypoints.ToArray();
	}

	public Waypoint[] GetReservedActorWaypoints()
	{
		return _reservedActorWaypoints.ToArray();
	}

	public Waypoint[] GetReservedPlayerWaypoints()
	{
		return _reservedPlayerWaypoints.ToArray();
	}

	public Waypoint[] GetReservedAnimalWaypoints()
	{
		return _reservedAnimalWaypoints.ToArray();
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
		if (base.EventKey == EventType.Move)
		{
			return true;
		}
		return false;
	}

	public void SetOriginalDestination()
	{
		if (!(DestWaypoint == null))
		{
			SetDestinationByDirectPathForce(DestWaypoint);
		}
	}

	private bool AnyWaypoint(List<Waypoint> list, Waypoint pt)
	{
		foreach (Waypoint item in list)
		{
			if (item == pt)
			{
				return true;
			}
		}
		return false;
	}

	public void LoadWaypoints()
	{
		Chunk chunk = null;
		foreach (KeyValuePair<int, Chunk> item in Singleton<Manager.Map>.Instance.ChunkTable)
		{
			if (item.Value.ID == AgentData.ChunkID)
			{
				chunk = item.Value;
				break;
			}
		}
		foreach (int reservedWaypointID in AgentData.ReservedWaypointIDList)
		{
			Waypoint waypoint = null;
			MapArea[] mapAreas = chunk.MapAreas;
			foreach (MapArea mapArea in mapAreas)
			{
				foreach (Waypoint waypoint3 in mapArea.Waypoints)
				{
					if (waypoint3.ID != reservedWaypointID)
					{
						continue;
					}
					waypoint = waypoint3;
					break;
				}
				if (waypoint != null)
				{
					break;
				}
			}
			if (waypoint != null)
			{
				_reservedWaypoints.Add(waypoint);
			}
		}
		foreach (int walkRouteWaypointID in AgentData.WalkRouteWaypointIDList)
		{
			Waypoint waypoint2 = null;
			MapArea[] mapAreas2 = chunk.MapAreas;
			foreach (MapArea mapArea2 in mapAreas2)
			{
				foreach (Waypoint waypoint4 in mapArea2.Waypoints)
				{
					if (waypoint4.ID != walkRouteWaypointID)
					{
						continue;
					}
					waypoint2 = waypoint4;
					break;
				}
				if (waypoint2 != null)
				{
					break;
				}
			}
			if (waypoint2 != null)
			{
				_reservedWaypoints.Add(waypoint2);
			}
		}
	}

	public void AbortCalc()
	{
		if (_calcEnumerator != null)
		{
			StopCoroutine(_calcEnumerator);
			_calcEnumerator = null;
		}
	}

	private bool ValidatePoints(Waypoint point, int chunkID, Weather weather, bool checkWeather = true, bool checkChunk = true)
	{
		if (point == null || point.OwnerArea == null)
		{
			return false;
		}
		if (checkWeather && !WeatherCheck(weather, point))
		{
			return false;
		}
		if (checkChunk)
		{
			MapArea ownerArea = point.OwnerArea;
			if (ownerArea.ChunkID != chunkID)
			{
				return false;
			}
		}
		if (point.Reserver != null && point.Reserver != this)
		{
			return false;
		}
		return true;
	}

	private bool AvailableAreaCheck(MapArea area, Dictionary<int, bool> availableArea, Manager.Map mapManager)
	{
		if (area != null && area.ChunkID != base.ChunkID)
		{
			if (!availableArea.TryGetValue(area.AreaID, out var value))
			{
				value = (availableArea[area.AreaID] = mapManager.CheckAvailableMapArea(area.AreaID));
			}
			if (!value)
			{
				return false;
			}
			return true;
		}
		return true;
	}

	public IEnumerator CalculateWaypoints()
	{
		Weather weather = Singleton<Manager.Map>.Instance.Simulator.Weather;
		_reservedWaypoints.Clear();
		Waypoint[] waypoints = Singleton<Manager.Map>.Instance.PointAgent.Waypoints;
		List<Waypoint> elements = ListPool<Waypoint>.Get();
		int chunkID = base.ChunkID;
		if (!waypoints.IsNullOrEmpty())
		{
			bool checkWeather = waypoints.Exists((Waypoint x) => !(x == null) && x.AreaType == MapArea.AreaType.Indoor);
			Waypoint[] array = waypoints;
			foreach (Waypoint waypoint in array)
			{
				if (ValidatePoints(waypoint, chunkID, weather, checkWeather))
				{
					elements.Add(waypoint);
				}
			}
		}
		bool existsHousingPointsSameChunk = false;
		if (!Singleton<Manager.Map>.Instance.PointAgent.AppendActionPoints.IsNullOrEmpty())
		{
			foreach (ActionPoint appendActionPoint in Singleton<Manager.Map>.Instance.PointAgent.AppendActionPoints)
			{
				if (appendActionPoint == null || appendActionPoint.OwnerArea == null || appendActionPoint.OwnerArea.ChunkID != chunkID)
				{
					continue;
				}
				existsHousingPointsSameChunk = true;
				break;
			}
		}
		if (Singleton<Manager.Map>.Instance.MapID > 0 || existsHousingPointsSameChunk)
		{
			foreach (KeyValuePair<int, List<Waypoint>> item in Singleton<Manager.Map>.Instance.PointAgent.HousingWaypointTable)
			{
				if (item.Value.IsNullOrEmpty())
				{
					continue;
				}
				bool checkWeather2 = item.Value.Exists((Waypoint x) => !(x == null) && x.AreaType == MapArea.AreaType.Indoor);
				foreach (Waypoint item2 in item.Value)
				{
					if (ValidatePoints(item2, chunkID, weather, checkWeather2))
					{
						elements.Add(item2);
					}
				}
			}
		}
		yield return CalculatePath(elements, Singleton<Manager.Resources>.Instance.AgentProfile.WalkSetting.reservedPathCount);
		ListPool<Waypoint>.Release(elements);
		_calcEnumerator = null;
	}

	private IEnumerator CalculatePath(List<Waypoint> points, int count)
	{
		List<Waypoint> routes = ListPool<Waypoint>.Get();
		if (points.IsNullOrEmpty())
		{
			_reservedWaypoints.Clear();
			yield return _waitMinute;
		}
		else
		{
			for (int i = 0; i < count; i++)
			{
				yield return CalculatePath(points, routes);
			}
			foreach (Waypoint item in routes)
			{
				_reservedWaypoints.Add(item);
			}
		}
		ListPool<Waypoint>.Release(routes);
	}

	private IEnumerator CalculatePath(List<Waypoint> points, List<Waypoint> list)
	{
		if (_calcElementPath == null)
		{
			_calcElementPath = new NavMeshPath();
		}
		if (points.IsNullOrEmpty())
		{
			yield break;
		}
		Waypoint pt = points.GetElement(UnityEngine.Random.Range(0, points.Count));
		if (pt == null)
		{
			yield break;
		}
		int prevCount = list.Count;
		if (!AnyWaypoint(list, pt) && pt.Reserver == null)
		{
			float waypointTweenMinDistance = Singleton<Manager.Resources>.Instance.LocomotionProfile.WaypointTweenMinDistance;
			if (list.IsNullOrEmpty())
			{
				if (Vector3.Distance(pt.transform.position, base.Position) > waypointTweenMinDistance)
				{
					list.Add(pt);
				}
			}
			else
			{
				Waypoint waypoint = list[list.Count - 1];
				if (Vector3.Distance(pt.transform.position, waypoint.transform.position) > waypointTweenMinDistance)
				{
					list.Add(pt);
				}
			}
		}
		points.Remove(pt);
		if (prevCount == list.Count)
		{
			yield return CalculatePath(points, list);
		}
	}

	public IEnumerator CalculateNearActionWaypoints()
	{
		Weather weather = Singleton<Manager.Map>.Instance.Simulator.Weather;
		List<Waypoint> elements = ListPool<Waypoint>.Get();
		int chunkID = base.ChunkID;
		int areaID = ((!(base.MapArea != null)) ? AgentData.AreaID : base.MapArea.AreaID);
		Manager.Map mapManager = Singleton<Manager.Map>.Instance;
		Dictionary<int, bool> availableArea = DictionaryPool<int, bool>.Get();
		Waypoint[] waypoints = Singleton<Manager.Map>.Instance.PointAgent.Waypoints;
		if (!waypoints.IsNullOrEmpty())
		{
			Waypoint[] array = waypoints;
			foreach (Waypoint waypoint in array)
			{
				if (ValidatePoints(waypoint, chunkID, weather))
				{
					MapArea ownerArea = waypoint.OwnerArea;
					if (AvailableAreaCheck(ownerArea, availableArea, mapManager))
					{
						elements.Add(waypoint);
					}
				}
			}
		}
		foreach (KeyValuePair<int, List<Waypoint>> item in Singleton<Manager.Map>.Instance.PointAgent.HousingWaypointTable)
		{
			if (item.Value.IsNullOrEmpty())
			{
				continue;
			}
			foreach (Waypoint item2 in item.Value)
			{
				if (ValidatePoints(item2, chunkID, weather))
				{
					MapArea ownerArea2 = item2.OwnerArea;
					if (AvailableAreaCheck(ownerArea2, availableArea, mapManager))
					{
						elements.Add(item2);
					}
				}
			}
		}
		int pathCount = Singleton<Manager.Resources>.Instance.AgentProfile.WalkSetting.reservedPathCount;
		ActionPoint[] actionPoints = Singleton<Manager.Map>.Instance.PointAgent.ActionPoints;
		List<ActionPoint> apElements = ListPool<ActionPoint>.Get();
		Desire.Type requestedDesire = RequestedDesire;
		EventType type = (EventType)0;
		UnityEx.ValueTuple<EventType, Desire.Type>[] valuePairs = Desire.ValuePairs;
		for (int j = 0; j < valuePairs.Length; j++)
		{
			UnityEx.ValueTuple<EventType, Desire.Type> valueTuple = valuePairs[j];
			if (valueTuple.Item2 == requestedDesire)
			{
				type = valueTuple.Item1;
				break;
			}
		}
		ActionPoint[] array2 = actionPoints;
		foreach (ActionPoint actionPoint in array2)
		{
			if (!WeatherCheck(weather, actionPoint))
			{
				continue;
			}
			MapArea ownerArea3 = actionPoint.OwnerArea;
			if (ownerArea3 != null && ownerArea3.AreaID != areaID)
			{
				if (!availableArea.TryGetValue(ownerArea3.AreaID, out var value))
				{
					value = (availableArea[ownerArea3.AreaID] = mapManager.CheckAvailableMapArea(ownerArea3.AreaID));
				}
				if (!value)
				{
					continue;
				}
			}
			if (type == EventType.Search)
			{
				if (!(actionPoint is SearchActionPoint))
				{
					continue;
				}
				SearchActionPoint searchActionPoint = actionPoint as SearchActionPoint;
				int tableID = searchActionPoint.TableID;
				if (SearchAreaID == 0)
				{
					if (tableID != 0 && tableID != 1 && tableID != 2)
					{
						continue;
					}
				}
				else if (SearchAreaID != tableID)
				{
					continue;
				}
				if (SearchAreaID != searchActionPoint.TableID)
				{
					continue;
				}
			}
			if (!apElements.Contains(actionPoint) && actionPoint.AgentEventType.Contains(type))
			{
				apElements.Add(actionPoint);
			}
		}
		DictionaryPool<int, bool>.Release(availableArea);
		yield return CalculateNearActionPath(elements, apElements, pathCount);
		ListPool<Waypoint>.Release(elements);
		ListPool<ActionPoint>.Release(apElements);
		_actionCalcEnumerator = null;
	}

	public IEnumerator CalculateNearActionWaypointsAdditive(int count)
	{
		Weather weather = Singleton<Manager.Map>.Instance.Simulator.Weather;
		float value = UnityEngine.Random.value;
		List<Waypoint> elements = ListPool<Waypoint>.Get();
		int chunkID = base.ChunkID;
		Manager.Map mapManager = Singleton<Manager.Map>.Instance;
		Dictionary<int, bool> availableArea = DictionaryPool<int, bool>.Get();
		Waypoint[] waypoints = Singleton<Manager.Map>.Instance.PointAgent.Waypoints;
		if (!waypoints.IsNullOrEmpty())
		{
			Waypoint[] array = waypoints;
			foreach (Waypoint waypoint in array)
			{
				if (ValidatePoints(waypoint, chunkID, weather))
				{
					MapArea ownerArea = waypoint.OwnerArea;
					if (AvailableAreaCheck(ownerArea, availableArea, mapManager))
					{
						elements.Add(waypoint);
					}
				}
			}
		}
		foreach (KeyValuePair<int, List<Waypoint>> item in Singleton<Manager.Map>.Instance.PointAgent.HousingWaypointTable)
		{
			if (item.Value.IsNullOrEmpty())
			{
				continue;
			}
			foreach (Waypoint item2 in item.Value)
			{
				if (ValidatePoints(item2, chunkID, weather))
				{
					MapArea ownerArea2 = item2.OwnerArea;
					if (AvailableAreaCheck(ownerArea2, availableArea, mapManager))
					{
						elements.Add(item2);
					}
				}
			}
		}
		ActionPoint[] actionPoints = Singleton<Manager.Map>.Instance.PointAgent.ActionPoints;
		List<ActionPoint> apElements = ListPool<ActionPoint>.Get();
		Desire.Type requestedDesire = RequestedDesire;
		EventType type = (EventType)0;
		UnityEx.ValueTuple<EventType, Desire.Type>[] valuePairs = Desire.ValuePairs;
		for (int j = 0; j < valuePairs.Length; j++)
		{
			UnityEx.ValueTuple<EventType, Desire.Type> valueTuple = valuePairs[j];
			if (valueTuple.Item2 == requestedDesire)
			{
				type = valueTuple.Item1;
				break;
			}
		}
		ActionPoint[] array2 = actionPoints;
		foreach (ActionPoint actionPoint in array2)
		{
			if (WeatherCheck(weather, actionPoint))
			{
				MapArea ownerArea3 = actionPoint.OwnerArea;
				if (AvailableAreaCheck(ownerArea3, availableArea, mapManager) && !apElements.Contains(actionPoint) && actionPoint.AgentEventType.Contains(type))
				{
					apElements.Add(actionPoint);
				}
			}
		}
		DictionaryPool<int, bool>.Release(availableArea);
		yield return CalculateNearActionPath(elements, apElements, count);
		ListPool<Waypoint>.Release(elements);
		ListPool<ActionPoint>.Release(apElements);
		_additiveActionCalcProcess = null;
	}

	private IEnumerator CalculateNearActionPath(List<Waypoint> points, List<ActionPoint> actionPoints, int count)
	{
		List<Waypoint> routes = ListPool<Waypoint>.Get();
		if (points.IsNullOrEmpty())
		{
			_reservedNearActionWaypoints.Clear();
			yield return _waitMinute;
		}
		else
		{
			for (int i = 0; i < count; i++)
			{
				yield return CalculateNearActionPath(points, actionPoints, routes);
			}
			foreach (Waypoint item in routes)
			{
				_reservedNearActionWaypoints.Add(item);
			}
		}
		ListPool<Waypoint>.Release(routes);
	}

	private IEnumerator CalculateNearActionPath(List<Waypoint> points, List<ActionPoint> actionPoints, List<Waypoint> list)
	{
		if (_calcElementPath == null)
		{
			_calcElementPath = new NavMeshPath();
		}
		if (actionPoints.IsNullOrEmpty() || points.IsNullOrEmpty())
		{
			yield break;
		}
		ActionPoint apt = actionPoints.GetElement(UnityEngine.Random.Range(0, actionPoints.Count));
		if (apt == null)
		{
			yield break;
		}
		float distance = Singleton<Manager.Resources>.Instance.AgentProfile.ActionPointNearDistance;
		List<Waypoint> aroundWaypoints = ListPool<Waypoint>.Get();
		foreach (Waypoint point in points)
		{
			if (!(Vector3.Distance(point.transform.position, apt.transform.position) > distance))
			{
				aroundWaypoints.Add(point);
			}
		}
		Waypoint pt = aroundWaypoints.GetElement(UnityEngine.Random.Range(0, aroundWaypoints.Count));
		ListPool<Waypoint>.Release(aroundWaypoints);
		if (pt == null)
		{
			yield break;
		}
		int prevCount = list.Count;
		if (!AnyWaypoint(list, pt) && pt.Reserver == null)
		{
			float waypointTweenMinDistance = Singleton<Manager.Resources>.Instance.LocomotionProfile.WaypointTweenMinDistance;
			if (list.IsNullOrEmpty())
			{
				if (Vector3.Distance(pt.transform.position, base.Position) > waypointTweenMinDistance)
				{
					list.Add(pt);
				}
			}
			else
			{
				Waypoint waypoint = list[list.Count - 1];
				if (Vector3.Distance(pt.transform.position, waypoint.transform.position) > waypointTweenMinDistance)
				{
					list.Add(pt);
				}
			}
		}
		actionPoints.Remove(apt);
		points.Remove(pt);
		if (prevCount == list.Count)
		{
			yield return CalculateNearActionPath(points, actionPoints, list);
		}
	}

	public IEnumerator CalculateDateActionWaypoints()
	{
		Weather weather = Singleton<Manager.Map>.Instance.Simulator.Weather;
		List<Waypoint> elements = ListPool<Waypoint>.Get();
		int chunkID = base.ChunkID;
		Manager.Map mapManager = Singleton<Manager.Map>.Instance;
		Dictionary<int, bool> availableArea = DictionaryPool<int, bool>.Get();
		Waypoint[] waypoints = Singleton<Manager.Map>.Instance.PointAgent.Waypoints;
		if (!waypoints.IsNullOrEmpty())
		{
			Waypoint[] array = waypoints;
			foreach (Waypoint waypoint in array)
			{
				if (ValidatePoints(waypoint, chunkID, weather))
				{
					MapArea ownerArea = waypoint.OwnerArea;
					if (AvailableAreaCheck(ownerArea, availableArea, mapManager))
					{
						elements.Add(waypoint);
					}
				}
			}
		}
		foreach (KeyValuePair<int, List<Waypoint>> item in Singleton<Manager.Map>.Instance.PointAgent.HousingWaypointTable)
		{
			if (item.Value.IsNullOrEmpty())
			{
				continue;
			}
			foreach (Waypoint item2 in item.Value)
			{
				if (ValidatePoints(item2, chunkID, weather))
				{
					MapArea ownerArea2 = item2.OwnerArea;
					if (AvailableAreaCheck(ownerArea2, availableArea, mapManager))
					{
						elements.Add(item2);
					}
				}
			}
		}
		int pathCount = Singleton<Manager.Resources>.Instance.AgentProfile.WalkSetting.reservedPathCount;
		ActionPoint[] actionPoints = Singleton<Manager.Map>.Instance.PointAgent.ActionPoints;
		List<ActionPoint> apElements = ListPool<ActionPoint>.Get();
		Desire.Type requestedDesire = RequestedDesire;
		EventType type = (EventType)0;
		UnityEx.ValueTuple<EventType, Desire.Type>[] valuePairs = Desire.ValuePairs;
		for (int j = 0; j < valuePairs.Length; j++)
		{
			UnityEx.ValueTuple<EventType, Desire.Type> valueTuple = valuePairs[j];
			if (valueTuple.Item2 == requestedDesire)
			{
				type = valueTuple.Item1;
				break;
			}
		}
		ActionPoint[] array2 = actionPoints;
		foreach (ActionPoint actionPoint in array2)
		{
			MapArea ownerArea3 = actionPoint.OwnerArea;
			if (AvailableAreaCheck(ownerArea3, availableArea, mapManager) && !apElements.Contains(actionPoint) && actionPoint.AgentDateEventType.Contains(type))
			{
				apElements.Add(actionPoint);
			}
		}
		DictionaryPool<int, bool>.Release(availableArea);
		yield return CalculateDateActionPath(elements, apElements, pathCount);
		ListPool<Waypoint>.Release(elements);
		ListPool<ActionPoint>.Release(apElements);
		_dateActionCalcEnumerator = null;
	}

	public IEnumerator CalculateDateActionWaypointsAdditive(int count)
	{
		Weather weather = Singleton<Manager.Map>.Instance.Simulator.Weather;
		List<Waypoint> elements = ListPool<Waypoint>.Get();
		int chunkID = base.ChunkID;
		Manager.Map mapManager = Singleton<Manager.Map>.Instance;
		Dictionary<int, bool> availableArea = DictionaryPool<int, bool>.Get();
		Waypoint[] waypoints = Singleton<Manager.Map>.Instance.PointAgent.Waypoints;
		if (!waypoints.IsNullOrEmpty())
		{
			Waypoint[] array = waypoints;
			foreach (Waypoint waypoint in array)
			{
				if (ValidatePoints(waypoint, chunkID, weather))
				{
					MapArea ownerArea = waypoint.OwnerArea;
					if (AvailableAreaCheck(ownerArea, availableArea, mapManager))
					{
						elements.Add(waypoint);
					}
				}
			}
		}
		foreach (KeyValuePair<int, List<Waypoint>> item in Singleton<Manager.Map>.Instance.PointAgent.HousingWaypointTable)
		{
			if (item.Value.IsNullOrEmpty())
			{
				continue;
			}
			foreach (Waypoint item2 in item.Value)
			{
				if (ValidatePoints(item2, chunkID, weather))
				{
					MapArea ownerArea2 = item2.OwnerArea;
					if (AvailableAreaCheck(ownerArea2, availableArea, mapManager))
					{
						elements.Add(item2);
					}
				}
			}
		}
		ActionPoint[] actionPoints = Singleton<Manager.Map>.Instance.PointAgent.ActionPoints;
		List<ActionPoint> apElements = ListPool<ActionPoint>.Get();
		Desire.Type requestedDesire = RequestedDesire;
		EventType type = (EventType)0;
		UnityEx.ValueTuple<EventType, Desire.Type>[] valuePairs = Desire.ValuePairs;
		for (int j = 0; j < valuePairs.Length; j++)
		{
			UnityEx.ValueTuple<EventType, Desire.Type> valueTuple = valuePairs[j];
			if (valueTuple.Item2 == requestedDesire)
			{
				type = valueTuple.Item1;
				break;
			}
		}
		ActionPoint[] array2 = actionPoints;
		foreach (ActionPoint actionPoint in array2)
		{
			MapArea ownerArea3 = actionPoint.OwnerArea;
			if (AvailableAreaCheck(ownerArea3, availableArea, mapManager) && !apElements.Contains(actionPoint) && actionPoint.AgentEventType.Contains(type))
			{
				apElements.Add(actionPoint);
			}
		}
		DictionaryPool<int, bool>.Release(availableArea);
		yield return CalculateNearActionPath(elements, apElements, count);
		ListPool<Waypoint>.Release(elements);
		ListPool<ActionPoint>.Release(apElements);
		_additiveDateActionCalcProcess = null;
	}

	private IEnumerator CalculateDateActionPath(List<Waypoint> points, List<ActionPoint> actionPoints, int count)
	{
		List<Waypoint> routes = ListPool<Waypoint>.Get();
		if (points.IsNullOrEmpty())
		{
			_reservedDateNearActionWaypoints.Clear();
			yield return _waitMinute;
		}
		else
		{
			for (int i = 0; i < count; i++)
			{
				yield return CalculateDateActionPath(points, actionPoints, routes);
			}
			foreach (Waypoint item in routes)
			{
				_reservedDateNearActionWaypoints.Add(item);
			}
		}
		ListPool<Waypoint>.Release(routes);
	}

	private IEnumerator CalculateDateActionPath(List<Waypoint> points, List<ActionPoint> actionPoints, List<Waypoint> list)
	{
		if (_calcElementPath == null)
		{
			_calcElementPath = new NavMeshPath();
		}
		if (actionPoints.IsNullOrEmpty() || points.IsNullOrEmpty())
		{
			yield break;
		}
		ActionPoint apt = actionPoints.GetElement(UnityEngine.Random.Range(0, actionPoints.Count));
		if (apt == null)
		{
			yield break;
		}
		float distance = Singleton<Manager.Resources>.Instance.AgentProfile.ActionPointNearDistance;
		List<Waypoint> aroundWaypoints = ListPool<Waypoint>.Get();
		foreach (Waypoint point in points)
		{
			if (!(Vector3.Distance(point.transform.position, apt.transform.position) > distance))
			{
				aroundWaypoints.Add(point);
			}
		}
		Waypoint pt = aroundWaypoints.GetElement(UnityEngine.Random.Range(0, aroundWaypoints.Count));
		ListPool<Waypoint>.Release(aroundWaypoints);
		if (pt == null)
		{
			yield break;
		}
		int prevCount = list.Count;
		if (!AnyWaypoint(list, pt) && pt.Reserver == null)
		{
			float waypointTweenMinDistance = Singleton<Manager.Resources>.Instance.LocomotionProfile.WaypointTweenMinDistance;
			if (list.IsNullOrEmpty())
			{
				if (Vector3.Distance(pt.transform.position, base.Position) > waypointTweenMinDistance)
				{
					list.Add(pt);
				}
			}
			else
			{
				Waypoint waypoint = list[list.Count - 1];
				if (Vector3.Distance(pt.transform.position, waypoint.transform.position) > waypointTweenMinDistance)
				{
					list.Add(pt);
				}
			}
		}
		actionPoints.Remove(apt);
		points.Remove(pt);
		if (prevCount == list.Count)
		{
			yield return CalculateDateActionPath(points, actionPoints, list);
		}
	}

	public IEnumerator CalculateLocationWaypoints()
	{
		List<Waypoint> elements = ListPool<Waypoint>.Get();
		int chunkID = base.ChunkID;
		Manager.Map mapManager = Singleton<Manager.Map>.Instance;
		Dictionary<int, bool> availableArea = DictionaryPool<int, bool>.Get();
		Waypoint[] waypoints = Singleton<Manager.Map>.Instance.PointAgent.Waypoints;
		if (!waypoints.IsNullOrEmpty())
		{
			Waypoint[] array = waypoints;
			foreach (Waypoint waypoint in array)
			{
				if (!(waypoint == null) && !(waypoint.OwnerArea == null) && (waypoint.Reserver == null || waypoint.Reserver != this))
				{
					MapArea ownerArea = waypoint.OwnerArea;
					if (AvailableAreaCheck(ownerArea, availableArea, mapManager))
					{
						elements.Add(waypoint);
					}
				}
			}
		}
		foreach (KeyValuePair<int, List<Waypoint>> item in Singleton<Manager.Map>.Instance.PointAgent.HousingWaypointTable)
		{
			if (item.Value.IsNullOrEmpty())
			{
				continue;
			}
			foreach (Waypoint item2 in item.Value)
			{
				if (!(item2 == null) && !(item2.OwnerArea == null) && (item2.Reserver == null || item2.Reserver == this))
				{
					MapArea ownerArea2 = item2.OwnerArea;
					if (AvailableAreaCheck(ownerArea2, availableArea, mapManager))
					{
						elements.Add(item2);
					}
				}
			}
		}
		int pathCount = Singleton<Manager.Resources>.Instance.AgentProfile.WalkSetting.reservedPathCount;
		ActionPoint[] actionPoints = Singleton<Manager.Map>.Instance.PointAgent.ActionPoints;
		List<ActionPoint> apElements = ListPool<ActionPoint>.Get();
		Desire.Type requestedDesire = RequestedDesire;
		EventType type = (EventType)0;
		UnityEx.ValueTuple<EventType, Desire.Type>[] valuePairs = Desire.ValuePairs;
		for (int j = 0; j < valuePairs.Length; j++)
		{
			UnityEx.ValueTuple<EventType, Desire.Type> valueTuple = valuePairs[j];
			if (valueTuple.Item2 == requestedDesire)
			{
				type = valueTuple.Item1;
				break;
			}
		}
		ActionPoint[] array2 = actionPoints;
		foreach (ActionPoint actionPoint in array2)
		{
			MapArea ownerArea3 = actionPoint.OwnerArea;
			if (ownerArea3 != null)
			{
				if (!availableArea.TryGetValue(ownerArea3.AreaID, out var value))
				{
					value = (availableArea[ownerArea3.AreaID] = mapManager.CheckAvailableMapArea(ownerArea3.AreaID));
				}
				if (!value)
				{
					continue;
				}
			}
			if (!apElements.Contains(actionPoint) && actionPoint.AgentEventType.Contains(type))
			{
				apElements.Add(actionPoint);
			}
		}
		DictionaryPool<int, bool>.Release(availableArea);
		yield return CalculateLocationPath(elements, apElements, pathCount);
		ListPool<Waypoint>.Release(elements);
		ListPool<ActionPoint>.Release(apElements);
		_locationCalcEnumerator = null;
	}

	public IEnumerator CalculateLocationWaypointsAdditive(int count)
	{
		List<Waypoint> elements = ListPool<Waypoint>.Get();
		int chunkID = base.ChunkID;
		Manager.Map mapManager = Singleton<Manager.Map>.Instance;
		Dictionary<int, bool> availableArea = DictionaryPool<int, bool>.Get();
		Waypoint[] waypoints = Singleton<Manager.Map>.Instance.PointAgent.Waypoints;
		if (!waypoints.IsNullOrEmpty())
		{
			Waypoint[] array = waypoints;
			foreach (Waypoint waypoint in array)
			{
				if (!(waypoint == null) && !(waypoint.OwnerArea == null) && (waypoint.Reserver == null || waypoint.Reserver != this))
				{
					MapArea ownerArea = waypoint.OwnerArea;
					if (AvailableAreaCheck(ownerArea, availableArea, mapManager))
					{
						elements.Add(waypoint);
					}
				}
			}
		}
		foreach (KeyValuePair<int, List<Waypoint>> item in Singleton<Manager.Map>.Instance.PointAgent.HousingWaypointTable)
		{
			if (item.Value.IsNullOrEmpty())
			{
				continue;
			}
			foreach (Waypoint item2 in item.Value)
			{
				if (!(item2 == null) && !(item2.OwnerArea == null) && (item2.Reserver == null || item2.Reserver == this))
				{
					MapArea ownerArea2 = item2.OwnerArea;
					if (AvailableAreaCheck(ownerArea2, availableArea, mapManager))
					{
						elements.Add(item2);
					}
				}
			}
		}
		ActionPoint[] actionPoints = Singleton<Manager.Map>.Instance.PointAgent.ActionPoints;
		List<ActionPoint> apElements = ListPool<ActionPoint>.Get();
		Desire.Type requestedDesire = RequestedDesire;
		EventType type = (EventType)0;
		UnityEx.ValueTuple<EventType, Desire.Type>[] valuePairs = Desire.ValuePairs;
		for (int j = 0; j < valuePairs.Length; j++)
		{
			UnityEx.ValueTuple<EventType, Desire.Type> valueTuple = valuePairs[j];
			if (valueTuple.Item2 == requestedDesire)
			{
				type = valueTuple.Item1;
				break;
			}
		}
		ActionPoint[] array2 = actionPoints;
		foreach (ActionPoint actionPoint in array2)
		{
			MapArea ownerArea3 = actionPoint.OwnerArea;
			if (AvailableAreaCheck(ownerArea3, availableArea, mapManager) && !apElements.Contains(actionPoint) && actionPoint.AgentEventType.Contains(type))
			{
				apElements.Add(actionPoint);
			}
		}
		DictionaryPool<int, bool>.Release(availableArea);
		yield return CalculateLocationPath(elements, apElements, count);
		ListPool<Waypoint>.Release(elements);
		ListPool<ActionPoint>.Release(apElements);
		_additiveActionCalcProcess = null;
	}

	private IEnumerator CalculateLocationPath(List<Waypoint> points, List<ActionPoint> actionPoints, int count)
	{
		List<Waypoint> routes = ListPool<Waypoint>.Get();
		if (points.IsNullOrEmpty())
		{
			_reservedLocationWaypoints.Clear();
			yield return _waitMinute;
		}
		else
		{
			for (int i = 0; i < count; i++)
			{
				yield return CalculateLocationPath(points, actionPoints, routes);
			}
			foreach (Waypoint item in routes)
			{
				_reservedLocationWaypoints.Add(item);
			}
		}
		ListPool<Waypoint>.Release(routes);
	}

	private IEnumerator CalculateLocationPath(List<Waypoint> points, List<ActionPoint> actionPoints, List<Waypoint> list)
	{
		if (_calcElementPath == null)
		{
			_calcElementPath = new NavMeshPath();
		}
		if (actionPoints.IsNullOrEmpty() || points.IsNullOrEmpty())
		{
			yield break;
		}
		ActionPoint apt = actionPoints.GetElement(UnityEngine.Random.Range(0, actionPoints.Count));
		if (apt == null)
		{
			yield break;
		}
		float distance = Singleton<Manager.Resources>.Instance.AgentProfile.ActionPointNearDistance;
		List<Waypoint> aroundWaypoints = ListPool<Waypoint>.Get();
		foreach (Waypoint point in points)
		{
			if (!(Vector3.Distance(point.transform.position, apt.transform.position) > distance))
			{
				aroundWaypoints.Add(point);
			}
		}
		Waypoint pt = aroundWaypoints.GetElement(UnityEngine.Random.Range(0, aroundWaypoints.Count));
		ListPool<Waypoint>.Release(aroundWaypoints);
		if (pt == null)
		{
			yield break;
		}
		int prevCount = list.Count;
		if (!AnyWaypoint(list, pt) && pt.Reserver == null)
		{
			float waypointTweenMinDistance = Singleton<Manager.Resources>.Instance.LocomotionProfile.WaypointTweenMinDistance;
			if (list.IsNullOrEmpty())
			{
				if (Vector2.Distance(pt.transform.position, base.Position) > waypointTweenMinDistance)
				{
					list.Add(pt);
				}
			}
			else
			{
				Waypoint waypoint = list[list.Count - 1];
				if (Vector3.Distance(pt.transform.position, waypoint.transform.position) > waypointTweenMinDistance)
				{
					list.Add(pt);
				}
			}
		}
		actionPoints.Remove(apt);
		points.Remove(pt);
		if (prevCount == list.Count)
		{
			yield return CalculateLocationPath(points, actionPoints, list);
		}
	}

	public IEnumerator CalculateActorWaypoints()
	{
		Weather weather = Singleton<Manager.Map>.Instance.Simulator.Weather;
		float value = UnityEngine.Random.value;
		List<Waypoint> elements = ListPool<Waypoint>.Get();
		int chunkID = base.ChunkID;
		Manager.Map mapManager = Singleton<Manager.Map>.Instance;
		Dictionary<int, bool> availableArea = DictionaryPool<int, bool>.Get();
		List<Actor> sameChunkActors = ListPool<Actor>.Get();
		List<Actor> diffAreaActors = ListPool<Actor>.Get();
		foreach (Actor targetActor in TargetActors)
		{
			int chunkID2 = targetActor.ChunkID;
			int areaID = targetActor.AreaID;
			if (chunkID2 == base.ChunkID)
			{
				sameChunkActors.Add(targetActor);
				continue;
			}
			if (!availableArea.TryGetValue(areaID, out var value2))
			{
				value2 = (availableArea[areaID] = mapManager.CheckAvailableMapArea(areaID));
			}
			if (value2)
			{
				diffAreaActors.Add(targetActor);
			}
		}
		Actor target = null;
		if (!sameChunkActors.IsNullOrEmpty())
		{
			target = sameChunkActors.GetElement(UnityEngine.Random.Range(0, sameChunkActors.Count));
		}
		else if (!diffAreaActors.IsNullOrEmpty())
		{
			target = diffAreaActors.GetElement(UnityEngine.Random.Range(0, diffAreaActors.Count));
		}
		ListPool<Actor>.Release(sameChunkActors);
		ListPool<Actor>.Release(diffAreaActors);
		Waypoint[] waypoints = Singleton<Manager.Map>.Instance.PointAgent.Waypoints;
		if (!waypoints.IsNullOrEmpty())
		{
			bool checkWeather = waypoints.Exists((Waypoint x) => !(x == null) && x.AreaType == MapArea.AreaType.Indoor);
			Waypoint[] array = waypoints;
			foreach (Waypoint waypoint in array)
			{
				if (ValidatePoints(waypoint, chunkID, weather, checkWeather, checkChunk: false))
				{
					MapArea ownerArea = waypoint.OwnerArea;
					if (AvailableAreaCheck(ownerArea, availableArea, mapManager))
					{
						elements.Add(waypoint);
					}
				}
			}
		}
		foreach (KeyValuePair<int, List<Waypoint>> item in Singleton<Manager.Map>.Instance.PointAgent.HousingWaypointTable)
		{
			if (item.Value.IsNullOrEmpty())
			{
				continue;
			}
			bool checkWeather2 = item.Value.Exists((Waypoint x) => !(x == null) && x.AreaType == MapArea.AreaType.Indoor);
			foreach (Waypoint item2 in item.Value)
			{
				if (ValidatePoints(item2, chunkID, weather, checkWeather2, checkChunk: false))
				{
					MapArea ownerArea2 = item2.OwnerArea;
					if (AvailableAreaCheck(ownerArea2, availableArea, mapManager))
					{
						elements.Add(item2);
					}
				}
			}
		}
		DictionaryPool<int, bool>.Release(availableArea);
		int pathCount = Singleton<Manager.Resources>.Instance.AgentProfile.WalkSetting.reservedPathCount;
		yield return CalculateActorPath(elements, target, pathCount);
		ListPool<Waypoint>.Release(elements);
		_actorCalcEnumerator = null;
	}

	public IEnumerator CalculateActorWaypointsAdditive(int count)
	{
		Weather weather = Singleton<Manager.Map>.Instance.Simulator.Weather;
		float value = UnityEngine.Random.value;
		List<Waypoint> elements = ListPool<Waypoint>.Get();
		int chunkID = base.ChunkID;
		Manager.Map mapManager = Singleton<Manager.Map>.Instance;
		Dictionary<int, bool> availableArea = DictionaryPool<int, bool>.Get();
		List<Actor> sameAreaActors = ListPool<Actor>.Get();
		List<Actor> diffAreaActors = ListPool<Actor>.Get();
		foreach (Actor targetActor in TargetActors)
		{
			int chunkID2 = targetActor.ChunkID;
			int areaID = targetActor.AreaID;
			if (chunkID2 == chunkID)
			{
				sameAreaActors.Add(targetActor);
				continue;
			}
			if (!availableArea.TryGetValue(areaID, out var value2))
			{
				value2 = (availableArea[areaID] = mapManager.CheckAvailableMapArea(areaID));
			}
			if (value2)
			{
				diffAreaActors.Add(targetActor);
			}
		}
		Actor target = null;
		if (!sameAreaActors.IsNullOrEmpty())
		{
			target = sameAreaActors[UnityEngine.Random.Range(0, sameAreaActors.Count)];
		}
		else if (!diffAreaActors.IsNullOrEmpty())
		{
			target = diffAreaActors[UnityEngine.Random.Range(0, diffAreaActors.Count)];
		}
		ListPool<Actor>.Release(sameAreaActors);
		ListPool<Actor>.Release(diffAreaActors);
		Waypoint[] waypoints = Singleton<Manager.Map>.Instance.PointAgent.Waypoints;
		if (!waypoints.IsNullOrEmpty())
		{
			bool checkWeather = waypoints.Exists((Waypoint x) => !(x == null) && x.AreaType == MapArea.AreaType.Indoor);
			Waypoint[] array = waypoints;
			foreach (Waypoint waypoint in array)
			{
				if (ValidatePoints(waypoint, chunkID, weather, checkWeather, checkChunk: false))
				{
					MapArea ownerArea = waypoint.OwnerArea;
					if (AvailableAreaCheck(ownerArea, availableArea, mapManager))
					{
						elements.Add(waypoint);
					}
				}
			}
		}
		foreach (KeyValuePair<int, List<Waypoint>> item in Singleton<Manager.Map>.Instance.PointAgent.HousingWaypointTable)
		{
			if (item.Value.IsNullOrEmpty())
			{
				continue;
			}
			bool checkWeather2 = item.Value.Exists((Waypoint x) => !(x == null) && x.AreaType == MapArea.AreaType.Indoor);
			foreach (Waypoint item2 in item.Value)
			{
				if (ValidatePoints(item2, chunkID, weather, checkWeather2, checkChunk: false))
				{
					MapArea ownerArea2 = item2.OwnerArea;
					if (AvailableAreaCheck(ownerArea2, availableArea, mapManager))
					{
						elements.Add(item2);
					}
				}
			}
		}
		DictionaryPool<int, bool>.Release(availableArea);
		yield return CalculateActorPath(elements, target, count);
		ListPool<Waypoint>.Release(elements);
	}

	private IEnumerator CalculateActorPath(List<Waypoint> points, Actor actor, int count)
	{
		List<Waypoint> routes = ListPool<Waypoint>.Get();
		if (points.IsNullOrEmpty())
		{
			_reservedActorWaypoints.Clear();
			yield return _waitMinute;
		}
		else
		{
			for (int i = 0; i < count; i++)
			{
				yield return CalculateActorPath(points, actor, routes);
			}
			foreach (Waypoint item in routes)
			{
				_reservedActorWaypoints.Add(item);
			}
		}
		ListPool<Waypoint>.Release(routes);
	}

	private IEnumerator CalculateActorPath(List<Waypoint> points, Actor actor, List<Waypoint> list)
	{
		if (_calcElementPath == null)
		{
			_calcElementPath = new NavMeshPath();
		}
		if (actor == null || points.IsNullOrEmpty())
		{
			yield break;
		}
		int chunkID = actor.ChunkID;
		int areaID = actor.AreaID;
		List<Waypoint> waypoints = ListPool<Waypoint>.Get();
		foreach (Waypoint point in points)
		{
			MapArea ownerArea = point.OwnerArea;
			if (ownerArea.ChunkID == chunkID)
			{
				waypoints.Add(point);
			}
		}
		Waypoint pt = waypoints.GetElement(UnityEngine.Random.Range(0, waypoints.Count));
		ListPool<Waypoint>.Release(waypoints);
		if (pt == null)
		{
			yield break;
		}
		int prevCount = list.Count;
		if (!AnyWaypoint(list, pt) && pt.Reserver == null)
		{
			float waypointTweenMinDistance = Singleton<Manager.Resources>.Instance.LocomotionProfile.WaypointTweenMinDistance;
			if (list.IsNullOrEmpty())
			{
				if (Vector3.Distance(pt.transform.position, base.Position) > waypointTweenMinDistance)
				{
					list.Add(pt);
				}
			}
			else
			{
				Waypoint waypoint = list[list.Count - 1];
				if (Vector3.Distance(pt.transform.position, waypoint.transform.position) > waypointTweenMinDistance)
				{
					list.Add(pt);
				}
			}
		}
		points.Remove(pt);
		if (prevCount == list.Count)
		{
			yield return CalculateActorPath(points, actor, list);
		}
	}

	public IEnumerator CalculatePlayerWaypoins()
	{
		Weather weather = Singleton<Manager.Map>.Instance.Simulator.Weather;
		float value = UnityEngine.Random.value;
		List<Waypoint> elements = ListPool<Waypoint>.Get();
		Manager.Map mapManager = Singleton<Manager.Map>.Instance;
		Dictionary<int, bool> availableArea = DictionaryPool<int, bool>.Get();
		PlayerActor target = Singleton<Manager.Map>.Instance.Player;
		int targetAreaID = target.AreaID;
		Waypoint[] waypoints = Singleton<Manager.Map>.Instance.PointAgent.Waypoints;
		if (!waypoints.IsNullOrEmpty())
		{
			bool checkWeather = waypoints.Exists((Waypoint x) => !(x == null) && x.AreaType == MapArea.AreaType.Indoor);
			Waypoint[] array = waypoints;
			foreach (Waypoint waypoint in array)
			{
				if (ValidatePoints(waypoint, base.ChunkID, weather, checkWeather, checkChunk: false))
				{
					MapArea ownerArea = waypoint.OwnerArea;
					if (AvailableAreaCheck(ownerArea, availableArea, mapManager))
					{
						elements.Add(waypoint);
					}
				}
			}
		}
		foreach (KeyValuePair<int, List<Waypoint>> item in Singleton<Manager.Map>.Instance.PointAgent.HousingWaypointTable)
		{
			if (item.Value.IsNullOrEmpty())
			{
				continue;
			}
			bool checkWeather2 = item.Value.Exists((Waypoint x) => !(x == null) && x.AreaType == MapArea.AreaType.Indoor);
			foreach (Waypoint item2 in item.Value)
			{
				if (ValidatePoints(item2, base.ChunkID, weather, checkWeather2, checkChunk: false))
				{
					MapArea ownerArea2 = item2.OwnerArea;
					if (AvailableAreaCheck(ownerArea2, availableArea, mapManager))
					{
						elements.Add(item2);
					}
				}
			}
		}
		DictionaryPool<int, bool>.Release(availableArea);
		int pathCount = Singleton<Manager.Resources>.Instance.AgentProfile.WalkSetting.reservedPathCount;
		yield return CalculateActorPath(elements, target, pathCount);
		ListPool<Waypoint>.Release(elements);
		_playerCalcEnumerator = null;
	}

	public IEnumerator CalculatePlayerWaypointsAdditive(int count)
	{
		Weather weather = Singleton<Manager.Map>.Instance.Simulator.Weather;
		float value = UnityEngine.Random.value;
		List<Waypoint> elements = ListPool<Waypoint>.Get();
		Manager.Map mapManager = Singleton<Manager.Map>.Instance;
		Dictionary<int, bool> availableArea = DictionaryPool<int, bool>.Get();
		PlayerActor target = Singleton<Manager.Map>.Instance.Player;
		Waypoint[] waypoints = Singleton<Manager.Map>.Instance.PointAgent.Waypoints;
		if (!waypoints.IsNullOrEmpty())
		{
			bool checkWeather = waypoints.Exists((Waypoint x) => !(x == null) && x.AreaType == MapArea.AreaType.Indoor);
			Waypoint[] array = waypoints;
			foreach (Waypoint waypoint in array)
			{
				if (ValidatePoints(waypoint, base.ChunkID, weather, checkWeather, checkChunk: false))
				{
					MapArea ownerArea = waypoint.OwnerArea;
					if (AvailableAreaCheck(ownerArea, availableArea, mapManager))
					{
						elements.Add(waypoint);
					}
				}
			}
		}
		foreach (KeyValuePair<int, List<Waypoint>> item in Singleton<Manager.Map>.Instance.PointAgent.HousingWaypointTable)
		{
			if (item.Value.IsNullOrEmpty())
			{
				continue;
			}
			bool checkWeather2 = item.Value.Exists((Waypoint x) => !(x == null) && x.AreaType == MapArea.AreaType.Indoor);
			foreach (Waypoint item2 in item.Value)
			{
				if (ValidatePoints(item2, base.ChunkID, weather, checkWeather2, checkChunk: false))
				{
					MapArea ownerArea2 = item2.OwnerArea;
					if (AvailableAreaCheck(ownerArea2, availableArea, mapManager))
					{
						elements.Add(item2);
					}
				}
			}
		}
		DictionaryPool<int, bool>.Release(availableArea);
		yield return CalculatePlayerPath(elements, target, count);
		ListPool<Waypoint>.Release(elements);
	}

	private IEnumerator CalculatePlayerPath(List<Waypoint> points, Actor actor, int count)
	{
		List<Waypoint> routes = ListPool<Waypoint>.Get();
		if (points.IsNullOrEmpty())
		{
			_reservedPlayerWaypoints.Clear();
			yield return _waitMinute;
		}
		else
		{
			for (int i = 0; i < count; i++)
			{
				yield return CalculateActorPath(points, actor, routes);
			}
			foreach (Waypoint item in routes)
			{
				_reservedPlayerWaypoints.Add(item);
			}
		}
		ListPool<Waypoint>.Release(routes);
	}

	public IEnumerator CalculateAnimalWaypoints()
	{
		PointManager pointAgent = ((!Singleton<Manager.Map>.IsInstance()) ? null : Singleton<Manager.Map>.Instance.PointAgent);
		Waypoint[] waypoints = ((!(pointAgent != null)) ? null : pointAgent.Waypoints);
		Dictionary<int, List<Waypoint>> housingPointTable = ((!(pointAgent != null)) ? null : pointAgent.HousingWaypointTable);
		bool empty = waypoints.IsNullOrEmpty();
		if (empty && !housingPointTable.IsNullOrEmpty())
		{
			foreach (KeyValuePair<int, List<Waypoint>> item in housingPointTable)
			{
				List<Waypoint> value2 = item.Value;
				bool flag;
				empty = (flag = value2.IsNullOrEmpty());
				if (!flag)
				{
					break;
				}
			}
		}
		if (empty)
		{
			SearchAnimalEmpty = true;
			_animalCalcEnumerator = null;
			yield break;
		}
		Weather weather = Singleton<Manager.Map>.Instance.Simulator.Weather;
		float value = UnityEngine.Random.value;
		int chunkID = base.ChunkID;
		int areaID = ((!(base.MapArea != null)) ? AgentData.AreaID : base.MapArea.AreaID);
		AnimalBase target = null;
		AnimalTypes requestedAnimal = AnimalTypes.Cat;
		List<AnimalBase> animals = ListPool<AnimalBase>.Get();
		List<AnimalBase> sameAreaAnimals = ListPool<AnimalBase>.Get();
		List<AnimalBase> sameChunkAnimals = ListPool<AnimalBase>.Get();
		Manager.Map mapManager = Singleton<Manager.Map>.Instance;
		Dictionary<int, bool> availableArea = DictionaryPool<int, bool>.Get();
		foreach (AnimalBase targetAnimal in TargetAnimals)
		{
			if (targetAnimal == null || targetAnimal.CurrentMapArea == null || targetAnimal.AnimalType != requestedAnimal)
			{
				continue;
			}
			MapArea currentMapArea = targetAnimal.CurrentMapArea;
			if (currentMapArea.AreaID == areaID)
			{
				sameAreaAnimals.Add(targetAnimal);
				continue;
			}
			if (!availableArea.TryGetValue(currentMapArea.AreaID, out var value3))
			{
				value3 = (availableArea[currentMapArea.AreaID] = mapManager.CheckAvailableMapArea(currentMapArea.AreaID));
			}
			if (value3)
			{
				if (currentMapArea.ChunkID == chunkID)
				{
					sameChunkAnimals.Add(targetAnimal);
				}
				else
				{
					animals.Add(targetAnimal);
				}
			}
		}
		if (!sameAreaAnimals.IsNullOrEmpty())
		{
			target = sameAreaAnimals.GetElement(UnityEngine.Random.Range(0, sameAreaAnimals.Count));
		}
		else if (!sameChunkAnimals.IsNullOrEmpty())
		{
			target = sameChunkAnimals.GetElement(UnityEngine.Random.Range(0, sameChunkAnimals.Count));
		}
		else if (!animals.IsNullOrEmpty())
		{
			target = animals.GetElement(UnityEngine.Random.Range(0, animals.Count));
		}
		ListPool<AnimalBase>.Release(animals);
		ListPool<AnimalBase>.Release(sameAreaAnimals);
		ListPool<AnimalBase>.Release(sameChunkAnimals);
		if (SearchAnimalEmpty = target == null)
		{
			DictionaryPool<int, bool>.Release(availableArea);
			_animalCalcEnumerator = null;
			yield break;
		}
		List<Waypoint> elements = ListPool<Waypoint>.Get();
		int animalAreaID = target.CurrentMapArea.AreaID;
		if (!waypoints.IsNullOrEmpty())
		{
			Waypoint[] array = waypoints;
			foreach (Waypoint waypoint in array)
			{
				if (waypoint == null || waypoint.OwnerArea == null || !waypoint.isActiveAndEnabled || !WeatherCheck(weather, waypoint))
				{
					continue;
				}
				MapArea ownerArea = waypoint.OwnerArea;
				if (ownerArea != null && ownerArea.AreaID != animalAreaID)
				{
					if (!availableArea.TryGetValue(ownerArea.AreaID, out var value4))
					{
						value4 = (availableArea[ownerArea.AreaID] = mapManager.CheckAvailableMapArea(ownerArea.AreaID));
					}
					if (!value4)
					{
						continue;
					}
				}
				if (waypoint.Reserver == null || waypoint.Reserver == this)
				{
					elements.Add(waypoint);
				}
			}
		}
		if (!housingPointTable.IsNullOrEmpty())
		{
			foreach (KeyValuePair<int, List<Waypoint>> item2 in housingPointTable)
			{
				List<Waypoint> value5 = item2.Value;
				if (value5.IsNullOrEmpty())
				{
					continue;
				}
				foreach (Waypoint item3 in value5)
				{
					if (item3 == null || item3.OwnerArea == null || !item3.isActiveAndEnabled || !WeatherCheck(weather, item3))
					{
						continue;
					}
					MapArea ownerArea2 = item3.OwnerArea;
					if (ownerArea2 != null && ownerArea2.AreaID != animalAreaID)
					{
						int areaID2 = ownerArea2.AreaID;
						if (!availableArea.TryGetValue(areaID2, out var value6))
						{
							value6 = (availableArea[areaID2] = mapManager.CheckAvailableMapArea(areaID2));
						}
						if (!value6)
						{
							continue;
						}
					}
					if (item3.Reserver == null || item3.Reserver == this)
					{
						elements.Add(item3);
					}
				}
			}
		}
		DictionaryPool<int, bool>.Release(availableArea);
		if (SearchAnimalEmpty = elements.IsNullOrEmpty())
		{
			ListPool<Waypoint>.Release(elements);
			_animalCalcEnumerator = null;
			yield break;
		}
		int pathCount = Singleton<Manager.Resources>.Instance.AgentProfile.WalkSetting.reservedPathCount;
		yield return CalculateAnimalPath(elements, target, pathCount);
		ListPool<Waypoint>.Release(elements);
		_animalCalcEnumerator = null;
	}

	public IEnumerator CalculateAnimalWaypointsAdditive(int count)
	{
		PointManager pointAgent = ((!Singleton<Manager.Map>.IsInstance()) ? null : Singleton<Manager.Map>.Instance.PointAgent);
		Waypoint[] waypoints = ((!(pointAgent != null)) ? null : pointAgent.Waypoints);
		Dictionary<int, List<Waypoint>> housingPointTable = ((!(pointAgent != null)) ? null : pointAgent.HousingWaypointTable);
		bool empty = waypoints.IsNullOrEmpty();
		if (empty && !housingPointTable.IsNullOrEmpty())
		{
			foreach (KeyValuePair<int, List<Waypoint>> item in housingPointTable)
			{
				List<Waypoint> value2 = item.Value;
				bool flag;
				empty = (flag = value2.IsNullOrEmpty());
				if (!flag)
				{
					break;
				}
			}
		}
		if (empty)
		{
			SearchAnimalEmpty = true;
			_additiveAnimalCalcProcess = null;
			yield break;
		}
		Weather weather = Singleton<Manager.Map>.Instance.Simulator.Weather;
		float value = UnityEngine.Random.value;
		int chunkID = base.ChunkID;
		int areaID = ((!(base.MapArea != null)) ? AgentData.AreaID : base.MapArea.AreaID);
		AnimalBase target = null;
		AnimalTypes requestedAnimal = AnimalTypes.Cat;
		List<AnimalBase> animals = ListPool<AnimalBase>.Get();
		List<AnimalBase> sameAreaAnimals = ListPool<AnimalBase>.Get();
		List<AnimalBase> sameChunkAnimals = ListPool<AnimalBase>.Get();
		Manager.Map mapManager = Singleton<Manager.Map>.Instance;
		Dictionary<int, bool> availableArea = DictionaryPool<int, bool>.Get();
		foreach (AnimalBase targetAnimal in TargetAnimals)
		{
			if (targetAnimal == null || targetAnimal.CurrentMapArea == null || targetAnimal.AnimalType != requestedAnimal)
			{
				continue;
			}
			MapArea currentMapArea = targetAnimal.CurrentMapArea;
			if (currentMapArea.AreaID == areaID)
			{
				sameAreaAnimals.Add(targetAnimal);
				continue;
			}
			if (!availableArea.TryGetValue(currentMapArea.AreaID, out var value3))
			{
				value3 = (availableArea[currentMapArea.AreaID] = mapManager.CheckAvailableMapArea(currentMapArea.AreaID));
			}
			if (value3)
			{
				if (currentMapArea.ChunkID == chunkID)
				{
					sameChunkAnimals.Add(targetAnimal);
				}
				else
				{
					animals.Add(targetAnimal);
				}
			}
		}
		if (!sameAreaAnimals.IsNullOrEmpty())
		{
			target = sameAreaAnimals.GetElement(UnityEngine.Random.Range(0, sameAreaAnimals.Count));
		}
		else if (!sameChunkAnimals.IsNullOrEmpty())
		{
			target = sameChunkAnimals.GetElement(UnityEngine.Random.Range(0, sameChunkAnimals.Count));
		}
		else if (!animals.IsNullOrEmpty())
		{
			target = animals.GetElement(UnityEngine.Random.Range(0, animals.Count));
		}
		ListPool<AnimalBase>.Release(animals);
		ListPool<AnimalBase>.Release(sameAreaAnimals);
		ListPool<AnimalBase>.Release(sameChunkAnimals);
		SearchAnimalEmpty = target == null;
		if (SearchAnimalEmpty = target == null)
		{
			DictionaryPool<int, bool>.Release(availableArea);
			_additiveAnimalCalcProcess = null;
			yield break;
		}
		List<Waypoint> elements = ListPool<Waypoint>.Get();
		int animalAreaID = target.CurrentMapArea.AreaID;
		if (!waypoints.IsNullOrEmpty())
		{
			Waypoint[] array = waypoints;
			foreach (Waypoint waypoint in array)
			{
				if (waypoint == null || waypoint.OwnerArea == null || !WeatherCheck(weather, waypoint))
				{
					continue;
				}
				MapArea ownerArea = waypoint.OwnerArea;
				if (ownerArea != null && ownerArea.AreaID != animalAreaID)
				{
					if (!availableArea.TryGetValue(ownerArea.AreaID, out var value4))
					{
						value4 = (availableArea[ownerArea.AreaID] = mapManager.CheckAvailableMapArea(ownerArea.AreaID));
					}
					if (!value4)
					{
						continue;
					}
				}
				if (waypoint.Reserver == null || waypoint.Reserver == this)
				{
					elements.Add(waypoint);
				}
			}
		}
		if (!housingPointTable.IsNullOrEmpty())
		{
			foreach (KeyValuePair<int, List<Waypoint>> item2 in housingPointTable)
			{
				List<Waypoint> value5 = item2.Value;
				if (value5.IsNullOrEmpty())
				{
					continue;
				}
				foreach (Waypoint item3 in value5)
				{
					if (item3 == null || item3.OwnerArea == null || !item3.isActiveAndEnabled || !WeatherCheck(weather, item3))
					{
						continue;
					}
					MapArea ownerArea2 = item3.OwnerArea;
					if (ownerArea2 != null && ownerArea2.AreaID != animalAreaID)
					{
						int areaID2 = ownerArea2.AreaID;
						if (!availableArea.TryGetValue(areaID2, out var value6))
						{
							value6 = (availableArea[areaID2] = mapManager.CheckAvailableMapArea(areaID2));
						}
						if (!value6)
						{
							continue;
						}
					}
					if (item3.Reserver == null || item3.Reserver == this)
					{
						elements.Add(item3);
					}
				}
			}
		}
		if (SearchAnimalEmpty = elements.IsNullOrEmpty())
		{
			ListPool<Waypoint>.Release(elements);
			_additiveAnimalCalcProcess = null;
			yield break;
		}
		DictionaryPool<int, bool>.Release(availableArea);
		yield return CalculateAnimalPath(elements, target, count);
		ListPool<Waypoint>.Release(elements);
		_additiveAnimalCalcProcess = null;
	}

	private IEnumerator CalculateAnimalPath(List<Waypoint> points, AnimalBase target, int count)
	{
		List<Waypoint> routes = ListPool<Waypoint>.Get();
		if (points.IsNullOrEmpty())
		{
			SearchAnimalEmpty = true;
			_reservedAnimalWaypoints.Clear();
			yield return _waitMinute;
		}
		else
		{
			for (int i = 0; i < count; i++)
			{
				yield return CalculateAnimalPath(points, target, routes);
			}
			SearchAnimalEmpty = routes.IsNullOrEmpty();
			foreach (Waypoint item in routes)
			{
				_reservedAnimalWaypoints.Add(item);
			}
		}
		ListPool<Waypoint>.Release(routes);
	}

	private IEnumerator CalculateAnimalPath(List<Waypoint> points, AnimalBase animal, List<Waypoint> list)
	{
		if (_calcElementPath == null)
		{
			_calcElementPath = new NavMeshPath();
		}
		if (points.IsNullOrEmpty())
		{
			yield break;
		}
		int prevCount = list.Count;
		List<Waypoint> waypoints = ListPool<Waypoint>.Get();
		if (animal != null)
		{
			int areaID = animal.CurrentMapArea.AreaID;
			foreach (Waypoint point in points)
			{
				if (point.OwnerArea.AreaID == areaID)
				{
					waypoints.Add(point);
				}
			}
		}
		else
		{
			waypoints.AddRange(points);
		}
		Waypoint pt = waypoints.GetElement(UnityEngine.Random.Range(0, waypoints.Count));
		ListPool<Waypoint>.Release(waypoints);
		if (pt == null)
		{
			yield break;
		}
		if (!AnyWaypoint(list, pt) && pt.Reserver == null)
		{
			float waypointTweenMinDistance = Singleton<Manager.Resources>.Instance.LocomotionProfile.WaypointTweenMinDistance;
			Vector3 b = ((!list.IsNullOrEmpty()) ? list[list.Count - 1].transform.position : base.Position);
			if (waypointTweenMinDistance < Vector3.Distance(pt.transform.position, b))
			{
				list.Add(pt);
			}
		}
		points.Remove(pt);
		if (prevCount == list.Count)
		{
			yield return CalculateAnimalPath(points, animal, list);
		}
	}

	private bool WeatherCheck(Weather weather, Point point)
	{
		if ((weather == Weather.Rain || weather == Weather.Storm) && !base.ChaControl.fileGameInfo.normalSkill.ContainsValue(16) && point.AreaType != MapArea.AreaType.Indoor)
		{
			return false;
		}
		return true;
	}

	public void StartPatrol()
	{
		_patrolCoroutine = Patrol();
		StartCoroutine(_patrolCoroutine);
	}

	public void ResumePatrol()
	{
		if (base.isActiveAndEnabled)
		{
			if (_patrolCoroutine == null)
			{
				_patrolCoroutine = Patrol();
			}
			StartCoroutine(_patrolCoroutine);
		}
	}

	public void StopPatrol()
	{
		if (_patrolCoroutine != null)
		{
			StopCoroutine(_patrolCoroutine);
		}
	}

	public void AbortPatrol()
	{
		if (_patrolCoroutine != null)
		{
			StopCoroutine(_patrolCoroutine);
			_patrolCoroutine = null;
		}
	}

	public void ClearWalkPath()
	{
		_reservedWaypoints.Clear();
	}

	private IEnumerator Patrol()
	{
		_calcEnumerator = CalculateWaypoints();
		StartCoroutine(_calcEnumerator);
		while (LivesCalc)
		{
			yield return null;
		}
		WalkRoute.Clear();
		int count = Singleton<Manager.Resources>.Instance.AgentProfile.WalkSetting.viaPointNumThreshold.RandomValue;
		for (int i = 0; i < count; i++)
		{
			WalkRoute.Enqueue(_reservedWaypoints.PopFront());
		}
		while (WalkRoute.Count > 0)
		{
			if (DestWaypoint != null)
			{
				while (!HasArrived())
				{
					yield return null;
					SetDestinationByDirectPath(DestWaypoint);
				}
			}
			if (WalkRoute.IsNullOrEmpty())
			{
				continue;
			}
			Waypoint destination = WalkRoute.Dequeue();
			if (destination == null)
			{
				continue;
			}
			SetDestinationByDirectPathForce(destination);
			do
			{
				yield return null;
				if (DestWaypoint != null && destination != DestWaypoint)
				{
					while (!HasArrived())
					{
						yield return null;
						SetDestinationByDirectPath(DestWaypoint);
					}
				}
				SetDestinationByDirectPath(destination);
			}
			while (!HasArrived());
			if (DestWaypoint != null)
			{
				DestWaypoint.Reserver = null;
				DestWaypoint = null;
			}
		}
		_patrolCoroutine = null;
	}

	private bool SetDestination(Waypoint destination)
	{
		DestWaypoint = destination;
		return _navMeshAgent.SetDestination(destination.transform.position);
	}

	private bool SetDestinationByDirectPathForce(Waypoint destination)
	{
		DestWaypoint = destination;
		bool result = false;
		if (_navMeshAgent == null)
		{
			return false;
		}
		if (!_navMeshAgent.isOnNavMesh)
		{
			return result;
		}
		NavMeshPath path = new NavMeshPath();
		if (!_navMeshAgent.CalculatePath(destination.transform.position, path) || !_navMeshAgent.SetPath(path) || _navMeshAgent.path.corners.IsNullOrEmpty())
		{
		}
		return result;
	}

	private bool SetDestinationByDirectPath(Waypoint destination)
	{
		DestWaypoint = destination;
		bool result = false;
		if (!_navMeshAgent.isOnNavMesh)
		{
			return result;
		}
		if (_navMeshAgent.path.corners.IsNullOrEmpty())
		{
			NavMeshPath path = new NavMeshPath();
			if (_navMeshAgent.CalculatePath(destination.transform.position, path) && _navMeshAgent.SetPath(path) && !_navMeshAgent.path.corners.IsNullOrEmpty())
			{
			}
		}
		return result;
	}

	public bool HasArrived()
	{
		if (_navMeshAgent.enabled)
		{
			float arrivedDistance = Singleton<Manager.Resources>.Instance.AgentProfile.WalkSetting.arrivedDistance;
			float num = ((!_navMeshAgent.pathPending) ? _navMeshAgent.remainingDistance : float.PositiveInfinity);
			return num <= arrivedDistance;
		}
		return false;
	}

	public bool HasArrivedOffMeshLink(OffMeshLink offMeshLink)
	{
		if (offMeshLink == null)
		{
			return false;
		}
		if (_navMeshAgent.isActiveAndEnabled)
		{
			float arrivedDistance = Singleton<Manager.Resources>.Instance.AgentProfile.WalkSetting.arrivedDistance;
			float num = ((!_navMeshAgent.pathPending) ? _navMeshAgent.remainingDistance : float.PositiveInfinity);
			return num <= arrivedDistance;
		}
		return false;
	}

	public bool IsInvalidMoveDestination(OffMeshLink checkLink)
	{
		if (checkLink == null)
		{
			return false;
		}
		ActionPoint component = checkLink.GetComponent<ActionPoint>();
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
				if (item == null || item.IsNeutralCommand)
				{
					continue;
				}
				return true;
			}
		}
		return false;
	}

	public void StartActionPatrol()
	{
		if (_actionPatrolCoroutine == null)
		{
			_actionPatrolCoroutine = ActionPatrol();
		}
		StartCoroutine(_actionPatrolCoroutine);
	}

	public void StopActionPatrol()
	{
		if (_actionPatrolCoroutine != null)
		{
			StopCoroutine(_actionPatrolCoroutine);
		}
	}

	public void AbortActionPatrol()
	{
		if (_actionPatrolCoroutine != null)
		{
			StopCoroutine(_actionPatrolCoroutine);
			_actionPatrolCoroutine = null;
		}
	}

	private IEnumerator ActionPatrol()
	{
		if (_reservedNearActionWaypoints.IsNullOrEmpty())
		{
			_actionCalcEnumerator = CalculateNearActionWaypoints();
			StartCoroutine(_actionCalcEnumerator);
		}
		while (LivesActionCalc)
		{
			yield return null;
		}
		int count = Singleton<Manager.Resources>.Instance.AgentProfile.WalkSetting.viaPointNumThreshold.RandomValue;
		for (int i = 0; i < count; i++)
		{
			Waypoint waypoint = _reservedNearActionWaypoints.PopFront();
			if (waypoint != null)
			{
				SearchActionRoute.Enqueue(waypoint);
			}
		}
		_additiveActionCalcProcess = CalculateNearActionWaypointsAdditive(count);
		StartCoroutine(_additiveActionCalcProcess);
		while (SearchActionRoute.Count > 0)
		{
			Waypoint destination = SearchActionRoute.Dequeue();
			if (!(destination == null))
			{
				SetDestinationByDirectPathForce(destination);
				do
				{
					yield return null;
					SetDestinationByDirectPath(destination);
				}
				while (!HasArrived());
				if (DestWaypoint != null)
				{
					DestWaypoint.Reserver = null;
					DestWaypoint = null;
				}
			}
		}
		_actionPatrolCoroutine = null;
	}

	public void StartDateActionPatrol()
	{
		if (_dateActionPatrolCoroutine == null)
		{
			_dateActionPatrolCoroutine = DateActionPatrol();
		}
		StartCoroutine(_dateActionPatrolCoroutine);
	}

	public void StopDateActionPatrol()
	{
		if (_dateActionPatrolCoroutine != null)
		{
			StopCoroutine(_dateActionPatrolCoroutine);
		}
	}

	private IEnumerator DateActionPatrol()
	{
		if (_reservedNearActionWaypoints.IsNullOrEmpty())
		{
			_dateActionCalcEnumerator = CalculateDateActionWaypoints();
			StartCoroutine(_dateActionCalcEnumerator);
		}
		while (LivesDateActionCalc)
		{
			yield return null;
		}
		int count = Singleton<Manager.Resources>.Instance.AgentProfile.WalkSetting.viaPointNumThreshold.RandomValue;
		for (int i = 0; i < count; i++)
		{
			Waypoint waypoint = _reservedNearActionWaypoints.PopFront();
			if (waypoint != null)
			{
				SearchDateActionRoute.Enqueue(waypoint);
			}
		}
		_additiveDateActionCalcProcess = CalculateDateActionWaypointsAdditive(count);
		StartCoroutine(_additiveDateActionCalcProcess);
		while (SearchDateActionRoute.Count > 0)
		{
			Waypoint destination = SearchDateActionRoute.Dequeue();
			if (!(destination == null))
			{
				SetDestinationByDirectPathForce(destination);
				do
				{
					yield return null;
					SetDestinationByDirectPath(destination);
				}
				while (!HasArrived());
				if (DestWaypoint != null)
				{
					DestWaypoint.Reserver = null;
					DestWaypoint = null;
				}
			}
		}
		_dateActionPatrolCoroutine = null;
	}

	public void StartLocationPatrol()
	{
		if (_locationPatrolCoroutine == null)
		{
			_locationPatrolCoroutine = LocationPatrol();
		}
		StartCoroutine(_locationPatrolCoroutine);
	}

	public void StopLocationPatrol()
	{
		if (_locationPatrolCoroutine != null)
		{
			StopCoroutine(_locationPatrolCoroutine);
		}
	}

	private IEnumerator LocationPatrol()
	{
		if (_reservedLocationWaypoints.IsNullOrEmpty())
		{
			_locationCalcEnumerator = CalculateLocationWaypoints();
			StartCoroutine(_locationCalcEnumerator);
		}
		while (LivesLocationCalc)
		{
			yield return null;
		}
		int count = Singleton<Manager.Resources>.Instance.AgentProfile.WalkSetting.viaPointNumThreshold.RandomValue;
		for (int i = 0; i < count; i++)
		{
			Waypoint waypoint = _reservedLocationWaypoints.PopFront();
			if (waypoint != null)
			{
				SearchLocationRoute.Enqueue(waypoint);
			}
		}
		_additiveLocationCalcProcess = CalculateLocationWaypointsAdditive(count);
		StartCoroutine(_additiveLocationCalcProcess);
		while (SearchLocationRoute.Count > 0)
		{
			Waypoint destination = SearchLocationRoute.Dequeue();
			if (!(destination == null))
			{
				SetDestinationByDirectPathForce(destination);
				do
				{
					yield return null;
					SetDestinationByDirectPath(destination);
				}
				while (!HasArrived());
				if (DestWaypoint != null)
				{
					DestWaypoint.Reserver = null;
					DestWaypoint = null;
				}
			}
		}
		_actionPatrolCoroutine = null;
	}

	public void StartActorPatrol()
	{
		if (_actorPatrolCoroutine == null)
		{
			_actorPatrolCoroutine = ActorPatrol();
		}
		StartCoroutine(_actorPatrolCoroutine);
	}

	public void StopActorPatrol()
	{
		if (_actorPatrolCoroutine != null)
		{
			StopCoroutine(_actorPatrolCoroutine);
		}
	}

	private IEnumerator ActorPatrol()
	{
		if (_reservedActorWaypoints.IsNullOrEmpty())
		{
			_actorCalcEnumerator = CalculateActorWaypoints();
			StartCoroutine(_actorCalcEnumerator);
		}
		while (LivesActorCalc)
		{
			yield return null;
		}
		int count = Singleton<Manager.Resources>.Instance.AgentProfile.WalkSetting.viaPointNumThreshold.RandomValue;
		for (int i = 0; i < count; i++)
		{
			Waypoint waypoint = _reservedActorWaypoints.PopFront();
			if (waypoint != null)
			{
				SearchActorRoute.Enqueue(waypoint);
			}
		}
		_additiveActorCalcProcess = CalculateActorWaypointsAdditive(count);
		StartCoroutine(_additiveActorCalcProcess);
		while (SearchActorRoute.Count > 0)
		{
			Waypoint destination = SearchActorRoute.Dequeue();
			if (!(destination == null))
			{
				SetDestinationByDirectPathForce(destination);
				do
				{
					yield return null;
					SetDestinationByDirectPath(destination);
				}
				while (!HasArrived());
				if (DestWaypoint != null)
				{
					DestWaypoint.Reserver = null;
					DestWaypoint = null;
				}
			}
		}
		_actorPatrolCoroutine = null;
	}

	public void StartPlayerPatrol()
	{
		if (_playerPatrolCoroutine == null)
		{
			_playerPatrolCoroutine = PlayerPatrol();
		}
		StartCoroutine(_playerPatrolCoroutine);
	}

	public void StopPlayerPatrol()
	{
		if (_playerPatrolCoroutine != null)
		{
			StopCoroutine(_playerPatrolCoroutine);
		}
	}

	private IEnumerator PlayerPatrol()
	{
		if (_reservedPlayerWaypoints.IsNullOrEmpty())
		{
			_playerCalcEnumerator = CalculatePlayerWaypoins();
			StartCoroutine(_playerCalcEnumerator);
		}
		while (LivesPlayerCalc)
		{
			yield return null;
		}
		int count = Singleton<Manager.Resources>.Instance.AgentProfile.WalkSetting.viaPointNumThreshold.RandomValue;
		for (int i = 0; i < count; i++)
		{
			Waypoint waypoint = _reservedPlayerWaypoints.PopFront();
			if (waypoint != null)
			{
				SearchPlayerRoute.Enqueue(waypoint);
			}
		}
		_additivePlayerCalcProcess = CalculatePlayerWaypointsAdditive(count);
		StartCoroutine(_additivePlayerCalcProcess);
		while (SearchPlayerRoute.Count > 0)
		{
			Waypoint destination = SearchPlayerRoute.Dequeue();
			if (!(destination == null))
			{
				SetDestinationByDirectPathForce(destination);
				do
				{
					yield return null;
					SetDestinationByDirectPath(destination);
				}
				while (!HasArrived());
				if (DestWaypoint != null)
				{
					DestWaypoint.Reserver = null;
					DestWaypoint = null;
				}
			}
		}
		_playerPatrolCoroutine = null;
	}

	public void StartAnimalPatrol()
	{
		if (_animalPatrolCoroutine == null)
		{
			_animalPatrolCoroutine = AnimalPatrol();
		}
		StartCoroutine(_animalPatrolCoroutine);
	}

	public void StopAnimalPatrol()
	{
		if (_animalPatrolCoroutine != null)
		{
			StopCoroutine(_animalPatrolCoroutine);
		}
	}

	public void StopForcedAnimalPatrol()
	{
		if (_animalPatrolCoroutine != null)
		{
			StopCoroutine(_animalPatrolCoroutine);
			_animalPatrolCoroutine = null;
		}
		if (_animalCalcEnumerator != null)
		{
			StopCoroutine(_animalCalcEnumerator);
			_animalCalcEnumerator = null;
		}
		if (_additiveAnimalCalcProcess != null)
		{
			StopCoroutine(_additiveAnimalCalcProcess);
			_additiveAnimalCalcProcess = null;
		}
	}

	public void ClearAnimalRoutePoints()
	{
		if (!_reservedAnimalWaypoints.IsNullOrEmpty())
		{
			_reservedAnimalWaypoints.Clear();
		}
		if (!SearchAnimalRoute.IsNullOrEmpty())
		{
			SearchAnimalRoute.Clear();
		}
	}

	private IEnumerator AnimalPatrol()
	{
		if (_reservedAnimalWaypoints.IsNullOrEmpty())
		{
			_animalCalcEnumerator = CalculateAnimalWaypoints();
			StartCoroutine(_animalCalcEnumerator);
		}
		while (LivesAnimalCalc)
		{
			yield return null;
		}
		int count = Singleton<Manager.Resources>.Instance.AgentProfile.WalkSetting.viaPointNumThreshold.RandomValue;
		for (int i = 0; i < count; i++)
		{
			Waypoint waypoint = _reservedAnimalWaypoints.PopFront();
			if (waypoint != null)
			{
				SearchAnimalRoute.Enqueue(waypoint);
			}
		}
		_additiveAnimalCalcProcess = CalculateAnimalWaypointsAdditive(count);
		StartCoroutine(_additiveAnimalCalcProcess);
		SearchAnimalEmpty = SearchAnimalRoute.IsNullOrEmpty();
		while (0 < SearchAnimalRoute.Count)
		{
			Waypoint destination = SearchAnimalRoute.Dequeue();
			if (!(destination == null))
			{
				SetDestinationByDirectPathForce(destination);
				do
				{
					yield return null;
					SetDestinationByDirectPath(destination);
				}
				while (!HasArrived());
				if (DestWaypoint != null)
				{
					DestWaypoint.Reserver = null;
					DestWaypoint = null;
				}
			}
		}
		_animalPatrolCoroutine = null;
	}

	public void SetDefaultImmoral()
	{
		StatusProfile statusProfile = Singleton<Manager.Resources>.Instance.StatusProfile;
		ChaFileGameInfo fileGameInfo = base.ChaControl.fileGameInfo;
		int immoral = fileGameInfo.immoral;
		int num = fileGameInfo.flavorState[4];
		float num2 = Mathf.InverseLerp(statusProfile.DirtyImmoralMinMax.min, statusProfile.DirtyImmoralMinMax.max, num);
		float f = Mathf.Lerp(statusProfile.DirtyImmoralBuff.min, statusProfile.DirtyImmoralBuff.max, num);
		immoral += Mathf.RoundToInt(f);
		int num3 = 0;
		foreach (KeyValuePair<int, int> item in fileGameInfo.hSkill)
		{
			if (item.Value != -1)
			{
				num3++;
			}
		}
		immoral += Mathf.RoundToInt(statusProfile.ImmoralBuff * (float)num3);
		if (fileGameInfo.hSkill.ContainsValue(22))
		{
			immoral += statusProfile.LustImmoralBuff;
		}
		else if (fileGameInfo.hSkill.ContainsValue(11))
		{
			immoral += statusProfile.FiredBodyImmoralBuff;
		}
		SetStatus(6, immoral);
	}

	public void UpdateStatus(int actionID, int poseID)
	{
		ApplyActionResultParameter(actionID, poseID);
		StatusProfile statusProfile = Singleton<Manager.Resources>.Instance.StatusProfile;
		switch (Singleton<Manager.Map>.Instance.Simulator.Temperature)
		{
		case Temperature.Hot:
			AddStatus(0, statusProfile.HotTemperatureValue);
			break;
		case Temperature.Cold:
			AddStatus(0, statusProfile.ColdTemperatureValue);
			break;
		}
		if (AgentData.IsWet && !base.ChaControl.fileGameInfo.normalSkill.ContainsValue(4))
		{
			AddStatus(0, statusProfile.WetTemperatureRate);
		}
		if (AgentData.StatsTable[2] <= 0f)
		{
			AddFlavorSkill(5, statusProfile.StarveWarinessValue);
			AddFlavorSkill(6, statusProfile.StarveDarknessValue);
		}
		if (base.ChaControl.fileGameInfo.normalSkill.ContainsValue(24))
		{
			AddStatus(3, statusProfile.CursedPhysicalBuff);
			AddStatus(6, statusProfile.CursedImmoralBuff);
			int desireKey = Desire.GetDesireKey(Desire.Type.H);
			AddDesire(desireKey, statusProfile.CursedHBuff);
		}
		AddStatus(6, 1f);
		AIProject.SaveData.Sickness sickState = AgentData.SickState;
		switch (sickState.ID)
		{
		case 1:
			ApplySituationResultParameter(7);
			break;
		case 3:
			ApplySituationResultParameter(10);
			break;
		}
	}

	public void ApplyActionResultParameter(int actionID, int poseID)
	{
		if (!Singleton<Manager.Resources>.Instance.Action.ActionStatusResultTable.TryGetValue(actionID, out var value))
		{
			return;
		}
		if (!value.TryGetValue(poseID, out var value2))
		{
			if (Debug.isDebugBuild && AIProject.Definitions.Action.EventTypeTable.TryGetValue(actionID, out var value3) && AIProject.Definitions.Action.NameTable.TryGetValue(value3, out var value4))
			{
				Debug.LogWarning($"リザルトパラメーターがない：行動={value4.Item2} ポーズ={poseID}");
			}
		}
		else
		{
			ApplyParameters(value2);
		}
	}

	public void ApplySituationResultParameter(int id)
	{
		if (Singleton<Manager.Resources>.Instance.Action.SituationStatusResultTable.TryGetValue(id, out var value))
		{
			ApplyParameters(value);
			int iD = AgentData.SickState.ID;
			if (iD == 3 && AgentData.StatsTable[0] < base.ChaControl.fileGameInfo.tempBound.upper)
			{
				AgentData.SickState.ID = -1;
			}
		}
	}

	private void ApplyParameters(Dictionary<int, ParameterPacket> parameters)
	{
		float num = 0f;
		foreach (KeyValuePair<int, ParameterPacket> parameter in parameters)
		{
			num += parameter.Value.Probability;
		}
		float num2 = UnityEngine.Random.Range(0f, num);
		ParameterPacket parameterPacket = null;
		foreach (KeyValuePair<int, ParameterPacket> parameter2 in parameters)
		{
			if (num2 <= parameter2.Value.Probability)
			{
				parameterPacket = parameter2.Value;
				break;
			}
			num2 -= parameter2.Value.Probability;
		}
		float rate = 1f;
		if (base.ChaControl.fileGameInfo.normalSkill.ContainsValue(5))
		{
			rate = ((UnityEngine.Random.Range(0, 10) != 0) ? 1 : 2);
		}
		foreach (KeyValuePair<int, TriThreshold> parameter3 in parameterPacket.Parameters)
		{
			ApplyParameter(parameter3, rate);
		}
	}

	private void ApplyParameter(KeyValuePair<int, TriThreshold> param, float rate)
	{
		int key = param.Key;
		if (key < 10)
		{
			float value = UnityEngine.Random.value;
			float value2 = (float)param.Value.Evaluate(value) * rate;
			AddStatus(key, value2);
			return;
		}
		if (key < 100)
		{
			key -= 10;
			float value3 = UnityEngine.Random.value;
			float num = (float)param.Value.Evaluate(value3) * rate;
			if (base.ChaControl.fileGameInfo.normalSkill.ContainsValue(9) && key == 1)
			{
				num += (float)Singleton<Manager.Resources>.Instance.StatusProfile.ReliabilityGWife;
			}
			if (base.ChaControl.fileGameInfo.normalSkill.ContainsValue(4) && key == 6)
			{
				num += (float)Singleton<Manager.Resources>.Instance.StatusProfile.DarknessReduceMaiden;
			}
			AddFlavorSkill(key, (int)num);
			return;
		}
		key -= 100;
		float value4 = UnityEngine.Random.value;
		float num2 = (float)param.Value.Evaluate(value4) * rate;
		if (AgentData.DesireTable.ContainsKey(key))
		{
			float num3 = AgentData.DesireTable[key] + num2;
			if (num3 < 0f)
			{
				num3 = 0f;
			}
			else if (num3 > 100f)
			{
				num3 = 100f;
			}
			SetDesire(key, num3);
			if (base.ChaControl.fileGameInfo.normalSkill.ContainsValue(36) && key == Desire.GetDesireKey(Desire.Type.Bath))
			{
				AddStatus(1, Singleton<Manager.Resources>.Instance.StatusProfile.DebuffMoodInBathDesire);
			}
		}
	}

	public void ApplyFoodParameter(StuffItem item)
	{
		if (item != null && Singleton<Manager.Resources>.Instance.GameInfo.FoodParameterTable.TryGetValue(item.CategoryID, out var value) && value.TryGetValue(item.ID, out var value2))
		{
			ApplyEatParameters(value2);
			AgentData.ItemList.RemoveItem(item);
		}
	}

	public void ApplyDrinkParameter(StuffItem item)
	{
		if (item != null && Singleton<Manager.Resources>.Instance.GameInfo.DrinkParameterTable.TryGetValue(item.CategoryID, out var value) && value.TryGetValue(item.ID, out var value2))
		{
			if (base.ChaControl.fileGameInfo.normalSkill.ContainsValue(45))
			{
			}
			ApplyDrinkParameters(value2);
			AgentData.ItemList.RemoveItem(item);
		}
	}

	private void ApplyEatParameters(Dictionary<int, FoodParameterPacket> parameters)
	{
		float num = 0f;
		foreach (KeyValuePair<int, FoodParameterPacket> parameter in parameters)
		{
			num += parameter.Value.Probability;
		}
		float num2 = UnityEngine.Random.Range(0f, num);
		FoodParameterPacket foodParameterPacket = null;
		foreach (KeyValuePair<int, FoodParameterPacket> parameter2 in parameters)
		{
			if (num2 <= parameter2.Value.Probability)
			{
				foodParameterPacket = parameter2.Value;
				break;
			}
			num2 -= parameter2.Value.Probability;
		}
		float rate = 1f;
		if (base.ChaControl.fileGameInfo.normalSkill.ContainsValue(5))
		{
			rate = ((UnityEngine.Random.Range(0, 10) != 0) ? 1 : 2);
		}
		foreach (KeyValuePair<int, TriThreshold> parameter3 in foodParameterPacket.Parameters)
		{
			ApplyParameter(parameter3, rate);
		}
		if (AgentData.SickState.ID == -1)
		{
			int num3 = UnityEngine.Random.Range(0, 100);
			float stomachacheRate = foodParameterPacket.StomachacheRate;
			StatusProfile statusProfile = Singleton<Manager.Resources>.Instance.StatusProfile;
			int flavor = base.ChaControl.fileGameInfo.flavorState[3];
			float num4 = FlavorVariation(statusProfile.StomachacheRateDebuffMinMax, statusProfile.StomachacheRateBuff, flavor);
			stomachacheRate += num4;
			if ((float)num3 < stomachacheRate)
			{
				CauseStomachache();
			}
		}
	}

	private void ApplyDrinkParameters(Dictionary<int, FoodParameterPacket> parameters)
	{
		float num = 0f;
		foreach (KeyValuePair<int, FoodParameterPacket> parameter in parameters)
		{
			num += parameter.Value.Probability;
		}
		float num2 = UnityEngine.Random.Range(0f, num);
		FoodParameterPacket foodParameterPacket = null;
		foreach (KeyValuePair<int, FoodParameterPacket> parameter2 in parameters)
		{
			if (num2 < parameter2.Value.Probability)
			{
				foodParameterPacket = parameter2.Value;
				break;
			}
			num2 -= parameter2.Value.Probability;
		}
		float rate = 1f;
		if (base.ChaControl.fileGameInfo.normalSkill.ContainsValue(5))
		{
			rate = ((UnityEngine.Random.Range(0, 10) != 0) ? 1 : 2);
		}
		foreach (KeyValuePair<int, TriThreshold> parameter3 in foodParameterPacket.Parameters)
		{
			ApplyParameter(parameter3, rate);
		}
		if (AgentData.SickState.ID == -1)
		{
			int num3 = UnityEngine.Random.Range(0, 100);
			float stomachacheRate = foodParameterPacket.StomachacheRate;
			StatusProfile statusProfile = Singleton<Manager.Resources>.Instance.StatusProfile;
			int flavor = base.ChaControl.fileGameInfo.flavorState[3];
			float num4 = FlavorVariation(statusProfile.StomachacheRateDebuffMinMax, statusProfile.StomachacheRateBuff, flavor);
			stomachacheRate += num4;
			if ((float)num3 < foodParameterPacket.StomachacheRate)
			{
				CauseStomachache();
			}
		}
	}

	private void CauseStomachache()
	{
		AgentData.SickState.ID = 1;
		int desireKey = Desire.GetDesireKey(Desire.Type.Toilet);
		SetDesire(desireKey, 100f);
	}

	public void HealSickBySleep()
	{
		AIProject.SaveData.Sickness sickState = AgentData.SickState;
		switch (sickState.ID)
		{
		case 1:
			sickState.ID = -1;
			break;
		case 3:
			ApplySituationResultParameter(11);
			AgentData.HeatStrokeLockInfo.Lock = true;
			sickState.ID = -1;
			break;
		case 4:
			ApplySituationResultParameter(14);
			sickState.ID = -1;
			break;
		}
	}

	public float? GetMotivation(int key)
	{
		if (!AgentData.MotivationTable.TryGetValue(key, out var value))
		{
			return null;
		}
		return value;
	}

	public bool SetMotivation(int key, float value)
	{
		Dictionary<int, float> motivationTable = AgentData.MotivationTable;
		if (motivationTable != null && motivationTable.TryGetValue(key, out var value2))
		{
			if (value2 == value)
			{
				return false;
			}
			motivationTable[key] = value;
			return true;
		}
		return false;
	}

	public void AddMotivation(int key, float addValue)
	{
		float? motivation = GetMotivation(key);
		if (motivation.HasValue)
		{
			float value = motivation.Value + addValue;
			SetMotivation(key, value);
		}
	}

	private float GetAddRate(int key)
	{
		Dictionary<int, float> desireAddRateTable = Singleton<Manager.Resources>.Instance.GetDesireAddRateTable(0, Singleton<Manager.Map>.Instance.Simulator.TimeZone);
		if (desireAddRateTable == null)
		{
			return 0f;
		}
		if (!desireAddRateTable.TryGetValue(key, out var value))
		{
			return 0f;
		}
		return value;
	}

	public float? GetDesire(int key)
	{
		if (!AgentData.DesireTable.TryGetValue(key, out var value))
		{
			return null;
		}
		return value;
	}

	public bool SetDesire(int key, float desireValue)
	{
		Dictionary<int, float> desireTable = AgentData.DesireTable;
		if (desireTable != null && desireTable.TryGetValue(key, out var value))
		{
			if (value == desireValue)
			{
				return false;
			}
			desireTable[key] = desireValue;
			return true;
		}
		return false;
	}

	public void SetForceDesire(int key, float desireValue)
	{
		AgentData.DesireTable[key] = desireValue;
	}

	public void AddDesire(int key, float addValue)
	{
		float? desire = GetDesire(key);
		if (desire.HasValue)
		{
			float desireValue = desire.Value + addValue;
			SetDesire(key, desireValue);
		}
	}

	public void MulDesire(int key, float mulValue)
	{
		float? desire = GetDesire(key);
		if (desire.HasValue)
		{
			float b = desire.Value * mulValue;
			b = Mathf.Max(0f, b);
			SetDesire(key, b);
		}
	}

	public void ClearDesire(Desire.Type type)
	{
		int desireKey = Desire.GetDesireKey(type);
		SetDesire(desireKey, 0f);
	}

	private void UpdateDesire(TimeSpan deltaTime, Dictionary<int, AgentProfile.DiminuationRates> rate)
	{
		StatusProfile statusProfile = Singleton<Manager.Resources>.Instance.StatusProfile;
		ChaFileGameInfo fileGameInfo = base.ChaControl.fileGameInfo;
		int flavor = fileGameInfo.flavorState[0];
		int flavor2 = fileGameInfo.flavorState[1];
		int flavor3 = fileGameInfo.flavorState[2];
		int flavor4 = fileGameInfo.flavorState[3];
		int flavor5 = fileGameInfo.flavorState[4];
		int flavor6 = fileGameInfo.flavorState[5];
		int flavor7 = fileGameInfo.flavorState[7];
		int flavor8 = fileGameInfo.flavorState[6];
		bool flag = base.ChaControl.fileGameInfo.normalSkill.ContainsValue(0);
		bool flag2 = base.ChaControl.fileGameInfo.normalSkill.ContainsValue(1);
		bool flag3 = base.ChaControl.fileGameInfo.normalSkill.ContainsValue(2);
		bool flag4 = base.ChaControl.fileGameInfo.normalSkill.ContainsValue(3);
		bool flag5 = base.ChaControl.fileGameInfo.normalSkill.ContainsValue(7);
		bool flag6 = base.ChaControl.fileGameInfo.normalSkill.ContainsValue(13);
		bool flag7 = base.ChaControl.fileGameInfo.normalSkill.ContainsValue(14);
		bool flag8 = base.ChaControl.fileGameInfo.normalSkill.ContainsValue(15);
		bool flag9 = base.ChaControl.fileGameInfo.normalSkill.ContainsValue(21);
		bool flag10 = base.ChaControl.fileGameInfo.normalSkill.ContainsValue(22);
		bool flag11 = base.ChaControl.fileGameInfo.normalSkill.ContainsValue(30);
		bool flag12 = base.ChaControl.fileGameInfo.normalSkill.ContainsValue(34);
		bool flag13 = base.ChaControl.fileGameInfo.normalSkill.ContainsValue(35);
		bool flag14 = base.ChaControl.fileGameInfo.normalSkill.ContainsValue(40);
		bool flag15 = base.ChaControl.fileGameInfo.normalSkill.ContainsValue(41);
		int desireKey = Desire.GetDesireKey(Desire.Type.Bath);
		int desireKey2 = Desire.GetDesireKey(Desire.Type.Sleep);
		int desireKey3 = Desire.GetDesireKey(Desire.Type.Break);
		int desireKey4 = Desire.GetDesireKey(Desire.Type.Gift);
		int desireKey5 = Desire.GetDesireKey(Desire.Type.Want);
		int desireKey6 = Desire.GetDesireKey(Desire.Type.Eat);
		int desireKey7 = Desire.GetDesireKey(Desire.Type.H);
		int desireKey8 = Desire.GetDesireKey(Desire.Type.Game);
		int desireKey9 = Desire.GetDesireKey(Desire.Type.Lonely);
		int desireKey10 = Desire.GetDesireKey(Desire.Type.Hunt);
		int desireKey11 = Desire.GetDesireKey(Desire.Type.Cook);
		int desireKey12 = Desire.GetDesireKey(Desire.Type.Animal);
		int desireKey13 = Desire.GetDesireKey(Desire.Type.Location);
		int desireKey14 = Desire.GetDesireKey(Desire.Type.Drink);
		float max = AgentData.StatsTable[5];
		int[] desireTableKeys = AgentData.DesireTableKeys;
		bool flag16 = false;
		int[] array = desireTableKeys;
		foreach (int num in array)
		{
			float? desire = GetDesire(num);
			if (desire.HasValue)
			{
				float addRate = GetAddRate(num);
				base.ChaControl.fileGameInfo.desireBuffVal.TryGetValue(num, out var value);
				float num2 = addRate + value;
				if (num == desireKey && flag2)
				{
					num2 += statusProfile.BuffBath;
				}
				if (num == desireKey2)
				{
					float num3 = FlavorVariation(statusProfile.SleepSociabilityBuffMinMax, statusProfile.SleepSociabilityBuff, flavor7);
					num2 += num3;
					if (flag4)
					{
						num2 += statusProfile.BuffSleep;
					}
				}
				if (num == desireKey6)
				{
					float num4 = FlavorVariation(statusProfile.EatPheromoneDebuffMinMax, statusProfile.EatPheromoneDebuff, flavor);
					num2 += num4;
					float num5 = FlavorVariation(statusProfile.EatInstinctBuffMinMax, statusProfile.EatInstinctBuff, flavor4);
					num2 += num5;
					float num6 = FlavorVariation(statusProfile.EatDarknessDebuffMinMax, statusProfile.EatDarknessDebuff, flavor8);
					num2 += num6;
					if (flag8)
					{
						num2 += statusProfile.BuffEat;
					}
				}
				if (num == desireKey3)
				{
					float num7 = FlavorVariation(statusProfile.BreakReasonBuffMinMax, statusProfile.BreakReasonBuff, flavor3);
					num2 += num7;
					float num8 = FlavorVariation(statusProfile.BreakInstinctBuffMinMax, statusProfile.BreakInstinctBuff, flavor3);
					num2 += num8;
					if (flag13)
					{
						num2 += statusProfile.BuffBreak;
					}
				}
				if (num == desireKey4)
				{
					float num9 = FlavorVariation(statusProfile.GiftReliabilityBuffMinMax, statusProfile.GiftReliabilityBuff, flavor2);
					num2 += num9;
					if (flag5)
					{
						num2 += statusProfile.BuffGift;
					}
				}
				if (num == desireKey5)
				{
					float num10 = FlavorVariation(statusProfile.GimmeWarinessBuffMinMax, statusProfile.GimmeWarinessBuff, flavor6);
					num2 += num10;
					float num11 = FlavorVariation(statusProfile.GimmeDarknessBuffMinMax, statusProfile.GimmeDarknessBuff, flavor8);
					num2 += num11;
					if (flag6)
					{
						num2 += statusProfile.BuffGimme;
					}
				}
				if (num == desireKey9)
				{
					float num12 = FlavorVariation(statusProfile.LonelySociabilityBuffMinMax, statusProfile.LonelySociabilityBuff, flavor7);
					num2 += num12;
					if (flag11)
					{
						num2 += statusProfile.BuffLonely;
					}
					if (flag12)
					{
						num2 += statusProfile.BuffLonelySuperSense;
					}
				}
				if (num == desireKey7)
				{
					float num13 = FlavorVariation(statusProfile.HDirtyBuffMinMax, statusProfile.HDirtyBuff, flavor5);
					num2 += num13;
					if (flag10)
					{
						num2 += statusProfile.BuffH;
					}
				}
				if (num == desireKey10)
				{
					float num14 = FlavorVariation(statusProfile.SearchWarinessBuffMinMax, statusProfile.SearchWarinessBuff, flavor6);
					num2 += num14;
					float num15 = FlavorVariation(statusProfile.SearchDarknessDebuffMinMax, statusProfile.SearchDarknessDebuff, flavor8);
					num2 += num15;
					if (flag7)
					{
						num2 += statusProfile.BuffSearchTough;
					}
					if (flag15)
					{
						num2 += statusProfile.BuffSearch;
					}
				}
				if (num == desireKey8)
				{
					float num16 = FlavorVariation(statusProfile.PlayReasonDebuffMinMax, statusProfile.PlayReasonDebuff, flavor3);
					num2 += num16;
					float num17 = FlavorVariation(statusProfile.PlayInstinctBuffMinMax, statusProfile.PlayInstinctBuff, flavor4);
					num2 += num17;
					if (flag9)
					{
						num2 += statusProfile.BuffPlay;
					}
				}
				if (num == desireKey11)
				{
					float num18 = FlavorVariation(statusProfile.CookPheromoneBuffMinMax, statusProfile.CookPheromoneBuff, flavor);
					num2 += num18;
					if (flag)
					{
						num2 += statusProfile.BuffCook;
					}
				}
				if (num == desireKey12 && flag3)
				{
					num2 += statusProfile.BuffAnimal;
				}
				if (num == desireKey13 && flag14)
				{
					num2 += statusProfile.BuffLocation;
				}
				if (num == desireKey14)
				{
					float num19 = FlavorVariation(statusProfile.DrinkWarinessBuffMinMax, statusProfile.DrinkWarinessBuff, flavor6);
					num2 += num19;
				}
				num2 = Mathf.Max(0f, num2);
				float num20 = num2 / 60f;
				float desireValue = Mathf.Clamp(desire.Value + num20 * (float)deltaTime.TotalMinutes, 0f, 100f);
				if (SetDesire(num, desireValue))
				{
					flag16 = true;
				}
			}
			float? motivation = GetMotivation(num);
			if (motivation.HasValue)
			{
				float valueRecovery = rate[num].valueRecovery;
				float value2 = Mathf.Clamp(motivation.Value + valueRecovery * (float)deltaTime.TotalMinutes, 0f, max);
				SetMotivation(num, value2);
			}
		}
		float value3 = AgentData.TalkMotivation + Singleton<Manager.Resources>.Instance.AgentProfile.TalkMotivationDimRate.valueRecovery * (float)deltaTime.TotalMinutes;
		AgentData.TalkMotivation = Mathf.Clamp(value3, 0f, max);
		if (!flag16)
		{
		}
	}

	public static float FlavorVariation(Threshold minmax, Threshold threshold, int flavor)
	{
		float t = Mathf.InverseLerp(minmax.min, minmax.max, flavor);
		return threshold.Lerp(t);
	}

	public void SetStatus(int id, float value)
	{
		if (id == 1 && base.ChaControl.fileGameInfo.phase < 2)
		{
			AgentData.StatsTable[1] = (base.ChaControl.fileGameInfo.moodBound.lower + base.ChaControl.fileGameInfo.moodBound.upper) / 2f;
			return;
		}
		StatusProfile statusProfile = Singleton<Manager.Resources>.Instance.StatusProfile;
		switch (id)
		{
		case 5:
		{
			value = ((!Singleton<Manager.Resources>.Instance.AgentProfile.MotivationMinValueTable.TryGetValue(base.ChaControl.fileParam.personality, out var value2)) ? Mathf.Clamp(value, 0f, 150f) : Mathf.Clamp(value, value2, 150f));
			AgentData.StatsTable[id] = value;
			break;
		}
		case 7:
			base.ChaControl.fileGameInfo.morality = Mathf.Clamp((int)value, 0, 100);
			break;
		case 1:
			if (base.ChaControl.fileGameInfo.normalSkill.ContainsValue(25))
			{
				value += statusProfile.DebuffMood;
			}
			if (base.ChaControl.fileGameInfo.phase < 2)
			{
				AgentData.StatsTable[id] = Mathf.Clamp(value, base.ChaControl.fileGameInfo.moodBound.lower, base.ChaControl.fileGameInfo.moodBound.upper);
			}
			else
			{
				AgentData.StatsTable[id] = Mathf.Clamp(value, 0f, 100f);
			}
			break;
		case 2:
			AgentData.StatsTable[id] = Mathf.Clamp(value, 0f, 100f);
			break;
		default:
			value = Mathf.Clamp(value, 0f, 100f);
			AgentData.StatsTable[id] = value;
			break;
		}
	}

	public void AddStatus(int id, float value)
	{
		ChaFileGameInfo fileGameInfo = base.ChaControl.fileGameInfo;
		if (id == 7)
		{
			int morality = fileGameInfo.morality;
			float value2 = (float)morality + value;
			SetStatus(id, value2);
			return;
		}
		float num = AgentData.StatsTable[id];
		float num2 = num + value;
		bool flag = fileGameInfo.normalSkill.ContainsValue(29);
		bool flag2 = fileGameInfo.normalSkill.ContainsValue(20);
		if (id == 3)
		{
			StatusProfile statusProfile = Singleton<Manager.Resources>.Instance.StatusProfile;
			int flavor = fileGameInfo.flavorState[6];
			float num3 = FlavorVariation(statusProfile.DarknessPhysicalBuffMinMax, statusProfile.DarknessPhysicalBuff, flavor);
			num2 += num3;
		}
		if (id == 6 && flag2 && num2 > num)
		{
			num2 += Singleton<Manager.Resources>.Instance.StatusProfile.BuffImmoral;
		}
		if (flag && id == 2)
		{
			if (num < num2)
			{
				SetStatus(id, num2);
			}
		}
		else
		{
			SetStatus(id, num2);
		}
	}

	public bool CanRevRape()
	{
		bool flag = base.ChaControl.fileGameInfo.hSkill.ContainsValue(13);
		bool flag2 = base.ChaControl.fileGameInfo.flavorState[4] >= Singleton<Manager.Resources>.Instance.StatusProfile.RevRapeBorder;
		if (flag && flag2)
		{
			return true;
		}
		return false;
	}

	public void AddFlavorSkill(int id, int value)
	{
		if (AgentData == null)
		{
			return;
		}
		ChaFileGameInfo fileGameInfo = base.ChaControl.fileGameInfo;
		if (!fileGameInfo.flavorState.ContainsKey(id) || AgentData.ParameterLock || Config.GameData.ParameterLock)
		{
			return;
		}
		int value2 = fileGameInfo.flavorState[id] + value;
		fileGameInfo.flavorState[id] = Mathf.Clamp(value2, 0, 99999);
		if (id == 4)
		{
			if (!fileGameInfo.isHAddTaii0 && fileGameInfo.flavorState[id] >= 100)
			{
				fileGameInfo.isHAddTaii0 = true;
			}
			if (!fileGameInfo.isHAddTaii1 && fileGameInfo.flavorState[id] >= 170)
			{
				fileGameInfo.isHAddTaii1 = true;
			}
		}
		int a = fileGameInfo.totalFlavor + value;
		fileGameInfo.totalFlavor = Mathf.Max(a, 0);
		AgentData.AddFlavorSkill(id, value);
	}

	public void AddFlavorSkill(FlavorSkill.Type type, int value)
	{
		AddFlavorSkill((int)type, value);
	}

	public void SetFlavorSkill(int id, int value)
	{
		if (AgentData == null)
		{
			return;
		}
		ChaFileGameInfo fileGameInfo = base.ChaControl.fileGameInfo;
		if (!fileGameInfo.flavorState.ContainsKey(id) || AgentData.ParameterLock || Config.GameData.ParameterLock)
		{
			return;
		}
		int num = value - fileGameInfo.flavorState[id];
		fileGameInfo.flavorState[id] = Mathf.Clamp(value, 0, 99999);
		if (id == 4)
		{
			if (!fileGameInfo.isHAddTaii0 && fileGameInfo.flavorState[id] >= 100)
			{
				fileGameInfo.isHAddTaii0 = true;
			}
			if (!fileGameInfo.isHAddTaii1 && fileGameInfo.flavorState[id] >= 170)
			{
				fileGameInfo.isHAddTaii1 = true;
			}
		}
		int a = fileGameInfo.totalFlavor + num;
		fileGameInfo.totalFlavor = Mathf.Max(a, 0);
		AgentData.AddFlavorAdditionAmount(num);
	}

	public void SetFlavorSkill(FlavorSkill.Type type, int value)
	{
		SetFlavorSkill((int)type, value);
	}

	public int GetFlavorSkill(int id)
	{
		if (base.ChaControl != null && base.ChaControl.fileGameInfo != null)
		{
			return base.ChaControl.fileGameInfo.flavorState[id];
		}
		return 0;
	}

	public int GetFlavorSkill(FlavorSkill.Type type)
	{
		return GetFlavorSkill((int)type);
	}

	private bool CanProgressPhase()
	{
		if (AttitudeID != 0)
		{
			return false;
		}
		if (!Singleton<Manager.Resources>.Instance.Action.PhaseExp.TryGetValue(base.ChaControl.fileParam.personality, out var value))
		{
			return false;
		}
		if (!value.TryGetValue(base.ChaControl.fileGameInfo.phase, out var value2))
		{
			return false;
		}
		return value2 <= base.ChaControl.fileGameInfo.totalFlavor;
	}

	public void SetPhase(int phase)
	{
		int phase2 = base.ChaControl.fileGameInfo.phase;
		if (phase2 == phase)
		{
			return;
		}
		if (Debug.isDebugBuild)
		{
			Debug.Log($"{base.gameObject.name} change phase: {base.ChaControl.fileGameInfo.phase} -> {phase}");
		}
		base.ChaControl.fileGameInfo.phase = phase;
		switch (phase)
		{
		case 2:
		{
			KeyValuePair<int, int>[] array2 = base.ChaControl.fileGameInfo.flavorState.OrderByDescending((KeyValuePair<int, int> pair) => pair.Value).Take(3).ToArray();
			int num = 0;
			int num2 = 0;
			KeyValuePair<int, int>[] array3 = array2;
			for (int i = 0; i < array3.Length; i++)
			{
				KeyValuePair<int, int> keyValuePair = array3[i];
				if (Singleton<Manager.Resources>.Instance.Action.FlavorPickSkillTable.TryGetValue(keyValuePair.Key, out var value3))
				{
					int key3 = LotterySkill(value3);
					if (value3.TryGetValue(key3, out var value4))
					{
						base.ChaControl.fileGameInfo.normalSkill[num++] = value4.Info.ItemID;
					}
				}
				if (Game.isAdd01 && Singleton<Manager.Resources>.Instance.Action.FlavorPickHSkillTable.TryGetValue(keyValuePair.Key, out value3))
				{
					int key4 = LotterySkill(value3);
					if (value3.TryGetValue(key4, out var value5))
					{
						base.ChaControl.fileGameInfo.hSkill[num2++] = value5.Info.ItemID;
					}
				}
			}
			int morality = base.ChaControl.fileGameInfo.morality;
			Dictionary<int, ObtainItemInfo> value8;
			if (morality >= 50)
			{
				if (Singleton<Manager.Resources>.Instance.Action.FlavorPickSkillTable.TryGetValue(17, out var value6))
				{
					int key5 = LotterySkill(value6);
					if (value6.TryGetValue(key5, out var value7))
					{
						base.ChaControl.fileGameInfo.normalSkill[num++] = value7.Info.ItemID;
					}
				}
			}
			else if (Singleton<Manager.Resources>.Instance.Action.FlavorPickSkillTable.TryGetValue(18, out value8))
			{
				int key6 = LotterySkill(value8);
				if (value8.TryGetValue(key6, out var value9))
				{
					base.ChaControl.fileGameInfo.normalSkill[num++] = value9.Info.ItemID;
				}
			}
			break;
		}
		case 3:
		{
			KeyValuePair<int, int>[] array = base.ChaControl.fileGameInfo.flavorState.OrderByDescending((KeyValuePair<int, int> pair) => pair.Value).Take(2).ToArray();
			int key = array[0].Key;
			int key2 = array[1].Key;
			if (Singleton<Manager.Resources>.Instance.Action.LifestyleTable.TryGetValue(key, out var value) && value.TryGetValue(key2, out var value2))
			{
				base.ChaControl.fileGameInfo.lifestyle = value2;
			}
			break;
		}
		}
	}

	private int LotterySkill(Dictionary<int, ObtainItemInfo> dic)
	{
		float num = 0f;
		foreach (KeyValuePair<int, ObtainItemInfo> item in dic)
		{
			num += (float)item.Value.Rate;
		}
		float num2 = UnityEngine.Random.Range(0f, num);
		int result = -1;
		foreach (KeyValuePair<int, ObtainItemInfo> item2 in dic)
		{
			int rate = item2.Value.Rate;
			if (num2 <= (float)rate)
			{
				result = item2.Key;
				break;
			}
			num2 -= (float)rate;
		}
		return result;
	}

	public void CauseSick()
	{
		AIProject.SaveData.Sickness sickState = AgentData.SickState;
		if (sickState.ID != -1)
		{
			return;
		}
		AgentProfile agentProfile = Singleton<Manager.Resources>.Instance.AgentProfile;
		StatusProfile statusProfile = Singleton<Manager.Resources>.Instance.StatusProfile;
		float num = AgentData.StatsTable[3];
		float num2 = AgentData.StatsTable[0];
		int flavor = base.ChaControl.fileGameInfo.flavorState[5];
		if (num <= 0f)
		{
			if (base.ChaControl.fileGameInfo.normalSkill.ContainsValue(14))
			{
				return;
			}
			sickState.ID = 2;
		}
		if (num2 <= agentProfile.ColdTempBorder)
		{
			if (!AgentData.ColdLockInfo.Lock)
			{
				float num3 = UnityEngine.Random.Range(0f, 100f);
				float coldDefaultIncidence = statusProfile.ColdDefaultIncidence;
				float num4 = FlavorVariation(statusProfile.ColdRateBuffMinMax, statusProfile.ColdRateBuff, flavor);
				coldDefaultIncidence += num4;
				if (base.ChaControl.fileGameInfo.normalSkill.ContainsValue(38))
				{
					coldDefaultIncidence += statusProfile.ColdRateDebuffWeak;
				}
				if (num3 < coldDefaultIncidence)
				{
					sickState.ID = 0;
				}
			}
		}
		else if (num2 >= agentProfile.HotTempBorder && !AgentData.HeatStrokeLockInfo.Lock)
		{
			float num5 = UnityEngine.Random.Range(0f, 100f);
			float heatStrokeDefaultIncidence = statusProfile.HeatStrokeDefaultIncidence;
			float num6 = FlavorVariation(statusProfile.HeatStrokeRateBuffMinMax, statusProfile.HeatStrokeRateBuff, flavor);
			heatStrokeDefaultIncidence += num6;
			if (base.ChaControl.fileGameInfo.normalSkill.ContainsValue(43))
			{
				heatStrokeDefaultIncidence += statusProfile.HeatStrokeBuffGuts;
			}
			if (num5 < heatStrokeDefaultIncidence)
			{
				sickState.ID = 3;
			}
		}
	}

	private void OnSickUpdate(TimeSpan timeSpan)
	{
		AIProject.SaveData.Sickness sickState = AgentData.SickState;
		if (sickState.ID <= -1 || Mode == Desire.ActionType.Onbu)
		{
			return;
		}
		sickState.ElapsedTime += timeSpan;
		if (Desire.ContainsSickFilterTable(Mode))
		{
			return;
		}
		Manager.Resources instance = Singleton<Manager.Resources>.Instance;
		switch (sickState.ID)
		{
		case 0:
			if (sickState.ElapsedTime.Days >= 4)
			{
				if (_navMeshAgent.isActiveAndEnabled && BehaviorResources.enabled && instance.AgentProfile.EncounterWhitelist.Contains(BehaviorResources.Mode))
				{
					CommandPartner = null;
					AgentData.CarryingItem = null;
					ActivateTransfer(move: false);
					ReservedMode = Desire.ActionType.Cold3A;
					ChangeBehavior(Desire.ActionType.Faint);
					MapUIContainer.AddSystemLog($"{MapUIContainer.CharaNameColor(this)}が倒れました", isNotify: true);
					sickState.Duration = TimeSpan.FromDays(4.0) + TimeSpan.FromDays(2.0);
				}
			}
			else if (sickState.ElapsedTime.Days >= 2 && _navMeshAgent.isActiveAndEnabled && BehaviorResources.enabled && instance.AgentProfile.EncounterWhitelist.Contains(BehaviorResources.Mode))
			{
				CommandPartner = null;
				AgentData.CarryingItem = null;
				ActivateTransfer(move: false);
				ReservedMode = Desire.ActionType.Cold2A;
				ChangeBehavior(Desire.ActionType.Faint);
				MapUIContainer.AddSystemLog($"{MapUIContainer.CharaNameColor(this)}が倒れました", isNotify: true);
				sickState.Duration = TimeSpan.FromDays(2.0) + TimeSpan.FromDays(2.0);
			}
			break;
		case 2:
			if (base.ChaControl.fileGameInfo.normalSkill.ContainsValue(14))
			{
				sickState.ID = -1;
			}
			else if (_navMeshAgent.isActiveAndEnabled && BehaviorResources.enabled && instance.AgentProfile.EncounterWhitelist.Contains(BehaviorResources.Mode))
			{
				float num = UnityEngine.Random.Range(0f, (float)TimeSpan.FromDays(1.0).TotalHours);
				sickState.Duration = sickState.ElapsedTime + TimeSpan.FromDays(2.0) + TimeSpan.FromHours(num);
				CommandPartner = null;
				AgentData.CarryingItem = null;
				ActivateTransfer(move: false);
				ReservedMode = Desire.ActionType.OverworkA;
				ChangeBehavior(Desire.ActionType.Faint);
				MapUIContainer.AddSystemLog($"{MapUIContainer.CharaNameColor(this)}が倒れました", isNotify: true);
			}
			break;
		}
	}
}
