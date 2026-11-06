using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AIProject.Definitions;
using AIProject.Player;
using AIProject.Scene;
using Illusion.Extensions;
using IllusionUtility.GetUtility;
using Manager;
using Sirenix.OdinInspector;
using UniRx;
using UnityEngine;
using UnityEngine.AI;
using UnityEx;

namespace AIProject;

public class ActionPoint : Point, ICommandable
{
	[Serializable]
	public struct PoseTypePair
	{
		public EventType eventType;

		public PoseType poseType;

		public PoseTypePair(EventType eventType_, PoseType poseType_)
		{
			eventType = eventType_;
			poseType = poseType_;
		}
	}

	[Serializable]
	public class PointPair
	{
		[SerializeField]
		private Transform _point;

		[SerializeField]
		private Transform _recoveryPoint;

		public Transform Point
		{
			get
			{
				return _point;
			}
			set
			{
				_point = value;
			}
		}

		public Transform RecoveryPoint
		{
			get
			{
				return _recoveryPoint ?? (_recoveryPoint = _point);
			}
			set
			{
				_recoveryPoint = value;
			}
		}
	}

	public enum DirectionKind
	{
		Free,
		Lock,
		Look
	}

	[Serializable]
	public class ActionSlotTable : IEnumerable<ActionSlot>, IEnumerable
	{
		public struct Enumerator : IEnumerator<ActionSlot>, IDisposable, IEnumerator
		{
			private List<ActionSlot> _list;

			private int _index;

			private ActionSlot _current;

			object IEnumerator.Current
			{
				get
				{
					if (_index == 0 || _index == _list.Count + 1)
					{
						throw new InvalidOperationException();
					}
					return _current;
				}
			}

			public ActionSlot Current
			{
				[CompilerGenerated]
				get
				{
					return _current;
				}
			}

			public Enumerator(List<ActionSlot> list)
			{
				_list = list;
				_index = 0;
				_current = null;
			}

			public Enumerator(ActionSlotTable table)
			{
				_list = table._table;
				_index = 0;
				_current = null;
			}

			public void Dispose()
			{
			}

			public bool MoveNext()
			{
				if (_index < _list.Count)
				{
					_current = _list[_index];
					_index++;
					return true;
				}
				return MoveNextRare();
			}

			private bool MoveNextRare()
			{
				_index = _list.Count + 1;
				_current = null;
				return false;
			}

			void IEnumerator.Reset()
			{
				_index = 0;
				_current = null;
			}
		}

		[SerializeField]
		private List<ActionSlot> _table = new List<ActionSlot>();

		public List<ActionSlot> Table
		{
			[CompilerGenerated]
			get
			{
				return _table;
			}
		}

		public int Count
		{
			[CompilerGenerated]
			get
			{
				return _table.Count;
			}
		}

		public ActionSlot this[int idx]
		{
			get
			{
				return _table[idx];
			}
			set
			{
				_table[idx] = value;
			}
		}

		public void Initialize()
		{
			Distinct();
		}

		private void Distinct()
		{
			List<ActionSlot> list = ListPool<ActionSlot>.Get();
			foreach (ActionSlot item in _table)
			{
				bool flag = true;
				foreach (ActionSlot item2 in list)
				{
					if (item2.Point == item.Point)
					{
						flag = false;
						break;
					}
				}
				if (flag)
				{
					list.Add(item);
				}
			}
			List<ActionSlot> list2 = ListPool<ActionSlot>.Get();
			foreach (ActionSlot item3 in _table)
			{
				if (!list.Contains(item3))
				{
					list2.Add(item3);
				}
			}
			foreach (ActionSlot item4 in list2)
			{
				_table.Remove(item4);
			}
			ListPool<ActionSlot>.Release(list2);
			ListPool<ActionSlot>.Release(list);
		}

		public Enumerator GetEnumerator()
		{
			return new Enumerator(this);
		}

		IEnumerator<ActionSlot> IEnumerable<ActionSlot>.GetEnumerator()
		{
			return new Enumerator(this);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return new Enumerator(this);
		}
	}

	[Serializable]
	public class ActionSlot
	{
		[SerializeField]
		private EventType _acceptionKey;

		[SerializeField]
		private Transform _point;

		[SerializeField]
		private Transform _recoveryPoint;

		[SerializeField]
		private Actor _actor;

		public EventType AcceptionKey
		{
			[CompilerGenerated]
			get
			{
				return _acceptionKey;
			}
		}

		public Transform Point
		{
			get
			{
				return _point;
			}
			set
			{
				_point = value;
			}
		}

		public Transform RecoveryPoint
		{
			get
			{
				return _recoveryPoint;
			}
			set
			{
				_recoveryPoint = value;
			}
		}

		public Actor Actor
		{
			get
			{
				return _actor;
			}
			set
			{
				_actor = value;
			}
		}
	}

	[SerializeField]
	private int _registerID;

	[SerializeField]
	protected int _id;

	[SerializeField]
	protected int[] _idList;

	[SerializeField]
	[HideInEditorMode]
	protected EventType _playerEventType;

	[SerializeField]
	[HideInEditorMode]
	protected EventType _agentEventType;

	[SerializeField]
	protected EventType[] _playerDateEventType = new EventType[2];

	[SerializeField]
	protected EventType _agentDateEventType;

	protected List<ActionPointInfo> _agentInfos;

	protected List<ActionPointInfo> _playerInfos;

	protected Dictionary<int, List<DateActionPointInfo>> _playerDateInfos;

	protected List<DateActionPointInfo> _agentDateInfos;

	[SerializeField]
	private bool _enabledRangeCheck = true;

	[SerializeField]
	[ShowIf("_enabledRangeCheck", true)]
	private float _radius = 1f;

	[SerializeField]
	private Transform _commandBasePoint;

	[SerializeField]
	protected Transform _destination;

	[SerializeField]
	[HideInEditorMode]
	[DisableInPlayMode]
	private GameObject[] _mapItemObjs;

	[SerializeField]
	private MapItemKeyValuePair[] _mapItemData;

	private NavMeshObstacle[] _obstacles;

	[SerializeField]
	protected ActionSlot _actionSlot = new ActionSlot();

	private NavMeshPath _pathForCalc;

	protected CommandLabel.CommandInfo[] _labels;

	protected CommandLabel.CommandInfo[] _sickLabels;

	protected CommandLabel.CommandInfo[][] _dateLabels = new CommandLabel.CommandInfo[2][];

	[SerializeField]
	private ObjectLayer _layer = ObjectLayer.Command;

	protected List<UnityEx.ValueTuple<int, int>> _bookingList = new List<UnityEx.ValueTuple<int, int>>();

	private int? _hashCode;

	public static Dictionary<EventType, Tuple<int, string, Action<PlayerActor, ActionPoint>>> LabelTable { get; } = new Dictionary<EventType, Tuple<int, string, Action<PlayerActor, ActionPoint>>>
	{
		[EventType.Sleep] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(0, "寝る", delegate(PlayerActor x, ActionPoint y)
		{
			y.ChangeStateIn(x, "Sleep", isDate: false);
		}),
		[EventType.Break] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(1, "休憩する", delegate(PlayerActor x, ActionPoint y)
		{
			y.ChangeStateIn(x, "Break", isDate: false);
		}),
		[EventType.Search] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(6, "調べる", delegate(PlayerActor x, ActionPoint y)
		{
			y.ChangeStateIn(x, "Search", isDate: false);
		}),
		[EventType.StorageIn] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(7, "ストレージ", delegate(PlayerActor x, ActionPoint y)
		{
			Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.BoxOpen);
			y.ChangeStateIn(x, "ItemBox", isDate: false);
		}),
		[EventType.Cook] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(9, "料理する", delegate(PlayerActor x, ActionPoint y)
		{
			y.ChangeStateIn(x, "Kitchen", isDate: false);
		}),
		[EventType.DressIn] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(10, "脱衣所", delegate(PlayerActor x, ActionPoint y)
		{
			y.ChangeStateIn(x, "DressRoom", isDate: false);
		}),
		[EventType.Move] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(14, "移動する", delegate(PlayerActor x, ActionPoint y)
		{
			y.ChangeStateIn(x, "Move", isDate: false);
		}),
		[EventType.DoorOpen] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(19, "ドアを開ける", delegate(PlayerActor x, ActionPoint y)
		{
			y.ChangeStateIn(x, "DoorOpen", isDate: false);
		}),
		[EventType.DoorClose] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(20, "ドアを閉める", delegate(PlayerActor x, ActionPoint y)
		{
			y.ChangeStateIn(x, "DoorClose", isDate: false);
		}),
		[EventType.ClothChange] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(22, "クローゼット", delegate(PlayerActor x, ActionPoint y)
		{
			y.ChangeStateIn(x, "ClothChange", isDate: false);
		}),
		[EventType.Warp] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(23, "ワープ装置", delegate(PlayerActor x, ActionPoint y)
		{
			y.ChangeStateIn(x, "Warp", isDate: false);
		})
	};


	public static Dictionary<EventType, Tuple<int, string, Action<PlayerActor, ActionPoint>>> DateLabelTable { get; } = new Dictionary<EventType, Tuple<int, string, Action<PlayerActor, ActionPoint>>>
	{
		[EventType.Sleep] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(0, "寝る", delegate(PlayerActor x, ActionPoint y)
		{
			ConfirmScene.Sentence = "一緒に寝た場合2人で行動状態が解除されます。";
			ConfirmScene.OnClickedYes = delegate
			{
				Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_L);
				x.AgentPartner.BehaviorResources.ChangeMode(Desire.ActionType.Idle);
				y.ChangeStateIn(x, "DateSleep", isDate: false);
			};
			ConfirmScene.OnClickedNo = delegate
			{
				Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Cancel);
				MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.InvokeAcception);
			};
			Singleton<Game>.Instance.LoadDialog();
			MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.None);
		}),
		[EventType.Break] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(1, "一緒に休憩する", delegate(PlayerActor x, ActionPoint y)
		{
			x.AgentPartner.BehaviorResources.ChangeMode(Desire.ActionType.Idle);
			y.ChangeStateIn(x, "DateBreak", isDate: false);
		}),
		[EventType.Eat] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(3, "一緒に御飯を食べる", delegate(PlayerActor x, ActionPoint y)
		{
			x.AgentPartner.BehaviorResources.ChangeMode(Desire.ActionType.Idle);
			y.ChangeStateIn(x, "DateEat", isDate: false);
		}),
		[EventType.Search] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(6, "調べる", delegate(PlayerActor x, ActionPoint y)
		{
			y.ChangeStateIn(x, "DateSearch", isDate: false);
		}),
		[EventType.StorageIn] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(7, "ストレージ", delegate(PlayerActor x, ActionPoint y)
		{
			Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.BoxOpen);
			y.ChangeStateIn(x, "ItemBox", isDate: false);
		}),
		[EventType.Lesbian] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(13, "Hポイント", delegate(PlayerActor x, ActionPoint y)
		{
			y.ChangeStateIn(x, "SpecialH", isDate: false);
		}),
		[EventType.Move] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(14, "移動する", delegate(PlayerActor x, ActionPoint y)
		{
			y.ChangeStateIn(x, "Move", isDate: false);
		}),
		[EventType.DoorOpen] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(19, "ドアを開ける", delegate(PlayerActor x, ActionPoint y)
		{
			y.ChangeStateIn(x, "DoorOpen", isDate: false);
		}),
		[EventType.DoorClose] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(20, "ドアを閉める", delegate(PlayerActor x, ActionPoint y)
		{
			y.ChangeStateIn(x, "DoorClose", isDate: false);
		}),
		[EventType.Warp] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(23, "ワープ装置", delegate(PlayerActor x, ActionPoint y)
		{
			y.ChangeStateIn(x, "Warp", isDate: false);
		})
	};


	public static Dictionary<EventType, Tuple<int, string, Action<PlayerActor, ActionPoint>>> SickLabelTable { get; } = new Dictionary<EventType, Tuple<int, string, Action<PlayerActor, ActionPoint>>>
	{
		[EventType.DoorOpen] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(19, "ドアを開ける", delegate(PlayerActor x, ActionPoint y)
		{
			y.ChangeStateIn(x, "DoorOpen", isDate: false);
		}),
		[EventType.DoorClose] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(20, "ドアを閉める", delegate(PlayerActor x, ActionPoint y)
		{
			y.ChangeStateIn(x, "DoorClose", isDate: false);
		}),
		[EventType.Warp] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(23, "ワープ装置", delegate(PlayerActor x, ActionPoint y)
		{
			y.ChangeStateIn(x, "Warp", isDate: false);
		}),
		[EventType.Sleep] = new Tuple<int, string, Action<PlayerActor, ActionPoint>>(0, "ベッドに寝かせる", delegate(PlayerActor x, ActionPoint y)
		{
			PutOnTheBed(x, y);
		})
	};


	public override int RegisterID
	{
		get
		{
			return _registerID;
		}
		set
		{
			_registerID = value;
		}
	}

	public int ID
	{
		[CompilerGenerated]
		get
		{
			return _id;
		}
	}

	public int[] IDList
	{
		[CompilerGenerated]
		get
		{
			return _idList;
		}
	}

	public EventType PlayerEventType
	{
		[CompilerGenerated]
		get
		{
			return _playerEventType;
		}
	}

	public EventType AgentEventType
	{
		[CompilerGenerated]
		get
		{
			return _agentEventType;
		}
	}

	public EventType[] PlayerDateEventType
	{
		[CompilerGenerated]
		get
		{
			return _playerDateEventType;
		}
	}

	public EventType AgentDateEventType
	{
		[CompilerGenerated]
		get
		{
			return _agentDateEventType;
		}
	}

	public List<Transform> NavMeshPoints { get; set; } = new List<Transform>();


	public HPoint HPoint { get; set; }

	public Vector3 CommandCenter
	{
		get
		{
			if (_commandBasePoint != null)
			{
				return _commandBasePoint.position;
			}
			return base.transform.position;
		}
	}

	public Actor Actor { get; set; }

	public bool EnabledRangeCheck
	{
		[CompilerGenerated]
		get
		{
			return _enabledRangeCheck;
		}
	}

	public float Radius
	{
		[CompilerGenerated]
		get
		{
			return _radius;
		}
	}

	public GameObject[] MapItemObjs
	{
		get
		{
			return _mapItemObjs;
		}
		set
		{
			_mapItemObjs = value;
		}
	}

	public MapItemKeyValuePair[] MapItemData
	{
		get
		{
			return _mapItemData;
		}
		set
		{
			_mapItemData = value;
		}
	}

	public Vector3 LocatedPosition
	{
		get
		{
			if (_destination != null)
			{
				return _destination.position;
			}
			return base.transform.position;
		}
	}

	public ActionSlot Slot
	{
		[CompilerGenerated]
		get
		{
			return _actionSlot;
		}
	}

	public List<ActionPoint> ConnectedActionPoints { get; set; } = new List<ActionPoint>();


	public List<ActionPoint> GroupActionPoints { get; private set; } = new List<ActionPoint>();


	public bool IsImpossible { get; protected set; }

	public virtual bool IsNeutralCommand
	{
		get
		{
			if (TutorialHideMode())
			{
				return false;
			}
			if (_actionSlot.Actor != null)
			{
				return false;
			}
			if (!_bookingList.IsNullOrEmpty())
			{
				return false;
			}
			return true;
		}
	}

	public Transform TargetCommun { get; set; }

	public Vector3 Position
	{
		[CompilerGenerated]
		get
		{
			return LocatedPosition;
		}
	}

	public virtual CommandLabel.CommandInfo[] Labels
	{
		get
		{
			if (Singleton<Manager.Map>.Instance.Player.PlayerController.State is Onbu)
			{
				return _sickLabels;
			}
			return _labels;
		}
	}

	public virtual CommandLabel.CommandInfo[] DateLabels
	{
		get
		{
			if (!Singleton<Manager.Map>.IsInstance() || Singleton<Manager.Map>.Instance.Player == null)
			{
				return null;
			}
			PlayerActor player = Singleton<Manager.Map>.Instance.Player;
			if (HasPlayerDateActionPointInfo(player.ChaControl.sex, EventType.Lesbian))
			{
				if (player.ChaControl.sex == 1 && !Singleton<Manager.Map>.Instance.Player.ChaControl.fileParam.futanari)
				{
					return null;
				}
			}
			else if (HasPlayerDateActionPointInfo(player.ChaControl.sex, EventType.Eat) && player.PlayerData.DateEatTrigger)
			{
				return null;
			}
			return _dateLabels[player.ChaControl.sex];
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
		get
		{
			if (_playerEventType.Contains(EventType.Move) || _playerEventType.Contains(EventType.DoorOpen) || _playerEventType.Contains(EventType.DoorClose))
			{
				return CommandType.AllAround;
			}
			return CommandType.Forward;
		}
	}

	public Actor Reserver { get; set; }

	public Actor BookingUser { get; private set; }

	public int InstanceID
	{
		get
		{
			if (_hashCode.HasValue)
			{
				return _hashCode.Value;
			}
			_hashCode = GetInstanceID();
			return _hashCode.Value;
		}
	}

	private static void PutOnTheBed(PlayerActor player, ActionPoint point)
	{
		player.CameraControl.CrossFade.FadeStart();
		player.Animation.StopAllAnimCoroutine();
		player.PlayerController.ChangeState("Normal");
		player.CameraControl.Mode = CameraMode.Normal;
		MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.InvokeAcception);
		AgentActor agentPartner = player.AgentPartner;
		agentPartner.Animation.StopAllAnimCoroutine();
		agentPartner.IsSlave = false;
		agentPartner.DeactivateNavMeshAgent();
		agentPartner.Partner = null;
		player.PlayerController.CommandArea.RemoveConsiderationObject(point);
		player.PlayerController.CommandArea.RefreshCommands();
		Observable.EveryLateUpdate().Skip(1).Take(1)
			.Subscribe(delegate
			{
				player.PlayerController.CommandArea.RemoveConsiderationObject(point);
				player.PlayerController.CommandArea.RefreshCommands();
			});
		agentPartner.CurrentPoint = point;
		Desire.ActionType type = Desire.SickPairTable[agentPartner.PrevMode];
		agentPartner.ChangeBehavior(type);
		player.AgentPartner = null;
		agentPartner.IsVisible = true;
	}

	protected void ChangeStateIn(PlayerActor actor, string stateName, bool isDate, System.Action onCompleted = null)
	{
		actor.PlayerController.ChangeState(stateName, this, onCompleted);
	}

	protected void ChangeStateOut(PlayerActor actor, string stateName)
	{
		actor.PlayerController.ChangeState(stateName);
		actor.CameraControl.Mode = CameraMode.Normal;
	}

	protected void ChangeStateDateIn(PlayerActor actor, string stateName, System.Action onCompleted = null)
	{
		ChangeStateIn(actor, stateName, isDate: true, onCompleted);
		actor.AgentPartner.AgentController.ChangeState(stateName);
	}

	protected void ChangeStateDateOut(PlayerActor actor, string stateName)
	{
		ChangeStateOut(actor, stateName);
		actor.AgentPartner.AgentController.ChangeState(stateName);
	}

	public bool HasPlayerDateActionPointInfo(byte sex, EventType eventType)
	{
		if (!_playerDateInfos.TryGetValue(sex, out var value))
		{
			return false;
		}
		foreach (DateActionPointInfo item in value)
		{
			if (item.eventTypeMask == eventType)
			{
				return true;
			}
		}
		return false;
	}

	public bool HasAgentActionPointInfo(EventType eventType)
	{
		foreach (ActionPointInfo agentInfo in _agentInfos)
		{
			if (agentInfo.eventTypeMask == eventType)
			{
				return true;
			}
		}
		return false;
	}

	public bool TryGetAgentActionPointInfo(EventType eventType, out ActionPointInfo outInfo)
	{
		foreach (ActionPointInfo agentInfo in _agentInfos)
		{
			if (agentInfo.eventTypeMask == eventType)
			{
				outInfo = agentInfo;
				return true;
			}
		}
		outInfo = default(ActionPointInfo);
		return false;
	}

	public void GetAgentActionPointInfos(EventType eventType, List<ActionPointInfo> outInfoList)
	{
		foreach (ActionPointInfo agentInfo in _agentInfos)
		{
			if (agentInfo.eventTypeMask == eventType)
			{
				outInfoList.Add(agentInfo);
			}
		}
	}

	public void GetAgentDateActionPointInfos(EventType eventType, List<DateActionPointInfo> outInfoList)
	{
		foreach (DateActionPointInfo agentDateInfo in _agentDateInfos)
		{
			if (agentDateInfo.eventTypeMask == eventType)
			{
				outInfoList.Add(agentDateInfo);
			}
		}
	}

	public ActionPointInfo GetActionPointInfo(AgentActor agent)
	{
		List<ActionPointInfo> list = ListPool<ActionPointInfo>.Get();
		GetAgentActionPointInfos(agent.EventKey, list);
		int item = AIProject.Definitions.Action.NameTable[agent.EventKey].Item1;
		List<int> list2 = Singleton<Manager.Resources>.Instance.Animation.PersonalActionListTable[agent.ChaControl.fileParam.personality][item];
		List<ActionPointInfo> list3 = ListPool<ActionPointInfo>.Get();
		foreach (ActionPointInfo item2 in list)
		{
			if (list2.Contains(item2.poseID))
			{
				list3.Add(item2);
			}
		}
		ListPool<ActionPointInfo>.Release(list);
		ActionPointInfo result;
		if (agent.AgentData.StatsTable[1] <= 40f)
		{
			Dictionary<int, Dictionary<int, ActAnimFlagData>> flagTable = Singleton<Manager.Resources>.Instance.Action.AgentActionFlagTable;
			Dictionary<int, ActAnimFlagData> value;
			ActAnimFlagData value2;
			List<ActionPointInfo> list4 = list3.FindAll((ActionPointInfo x) => flagTable.TryGetValue(x.eventID, out value) && value.TryGetValue(x.poseID, out value2) && value2.isBadMood);
			result = (list4.IsNullOrEmpty() ? list3.GetElement(UnityEngine.Random.Range(0, list3.Count)) : list4.GetElement(UnityEngine.Random.Range(0, list4.Count)));
		}
		else
		{
			result = list3.GetElement(UnityEngine.Random.Range(0, list3.Count));
		}
		ListPool<ActionPointInfo>.Release(list3);
		return result;
	}

	public DateActionPointInfo GetDateActionPointInfo(AgentActor agent)
	{
		List<DateActionPointInfo> list = ListPool<DateActionPointInfo>.Get();
		GetAgentDateActionPointInfos(agent.EventKey, list);
		int item = AIProject.Definitions.Action.NameTable[agent.EventKey].Item1;
		List<int> list2 = Singleton<Manager.Resources>.Instance.Animation.PersonalActionListTable[agent.ChaControl.fileParam.personality][item];
		List<DateActionPointInfo> list3 = ListPool<DateActionPointInfo>.Get();
		foreach (DateActionPointInfo item2 in list)
		{
			if (list2.Contains(item2.poseIDA))
			{
				list3.Add(item2);
			}
		}
		ListPool<DateActionPointInfo>.Release(list);
		DateActionPointInfo element = list3.GetElement(UnityEngine.Random.Range(0, list3.Count));
		ListPool<DateActionPointInfo>.Release(list3);
		return element;
	}

	public bool FindAgentActionPointInfo(EventType eventType, int poseID, out ActionPointInfo outInfo)
	{
		foreach (ActionPointInfo agentInfo in _agentInfos)
		{
			if (agentInfo.eventTypeMask != eventType || agentInfo.poseID != poseID)
			{
				continue;
			}
			outInfo = agentInfo;
			return true;
		}
		outInfo = default(ActionPointInfo);
		return false;
	}

	public bool FindPlayerDateActionPointInfo(byte sex, int pointID, out DateActionPointInfo outInfo)
	{
		if (!_playerDateInfos.TryGetValue(sex, out var _))
		{
			outInfo = default(DateActionPointInfo);
			return false;
		}
		foreach (DateActionPointInfo item in _playerDateInfos[sex])
		{
			if (item.pointID != pointID)
			{
				continue;
			}
			outInfo = item;
			return true;
		}
		outInfo = default(DateActionPointInfo);
		return false;
	}

	public bool TryGetPlayerActionPointInfo(EventType eventType, out ActionPointInfo outInfo)
	{
		foreach (ActionPointInfo playerInfo in _playerInfos)
		{
			if (playerInfo.eventTypeMask == eventType)
			{
				outInfo = playerInfo;
				return true;
			}
		}
		outInfo = default(ActionPointInfo);
		return false;
	}

	public bool TryGetPlayerDateActionPointInfo(byte sex, EventType eventType, out DateActionPointInfo outInfo)
	{
		if (!_playerDateInfos.TryGetValue(sex, out var value))
		{
			outInfo = default(DateActionPointInfo);
			return false;
		}
		foreach (DateActionPointInfo item in value)
		{
			if (item.eventTypeMask == eventType)
			{
				outInfo = item;
				return true;
			}
		}
		outInfo = default(DateActionPointInfo);
		return false;
	}

	public bool TryGetAgentDateActionPointInfo(EventType eventType, out DateActionPointInfo outInfo)
	{
		foreach (DateActionPointInfo agentDateInfo in _agentDateInfos)
		{
			if (agentDateInfo.eventTypeMask == eventType)
			{
				outInfo = agentDateInfo;
				return true;
			}
		}
		outInfo = default(DateActionPointInfo);
		return false;
	}

	public bool ContainsAgentDateActionPointInfo(EventType eventType)
	{
		foreach (DateActionPointInfo agentDateInfo in _agentDateInfos)
		{
			if (agentDateInfo.eventTypeMask == eventType)
			{
				return true;
			}
		}
		return false;
	}

	public virtual bool Entered(Vector3 basePosition, float distance, float radiusA, float radiusB, float angle, Vector3 forward)
	{
		if (TutorialHideMode())
		{
			return false;
		}
		Vector3 commandCenter = CommandCenter;
		commandCenter.y = 0f;
		float num = Vector3.Distance(basePosition, commandCenter);
		List<ActionPoint> connectedActionPoints = ConnectedActionPoints;
		if (connectedActionPoints != null)
		{
			foreach (ActionPoint item in connectedActionPoints)
			{
				if (item == null || item.IsNeutralCommand)
				{
					continue;
				}
				return false;
			}
		}
		if (CommandType == CommandType.Forward)
		{
			float num2 = ((!_enabledRangeCheck) ? radiusA : (radiusA + _radius));
			if (num > num2)
			{
				return false;
			}
			Vector3 vector = commandCenter;
			vector.y = 0f;
			float num3 = angle / 2f;
			float num4 = Vector3.Angle(vector - basePosition, forward);
			if (num4 > num3)
			{
				return false;
			}
		}
		else
		{
			float num5 = ((!_enabledRangeCheck) ? radiusB : (radiusB + _radius));
			if (distance > num5)
			{
				return false;
			}
		}
		return true;
	}

	public bool IsReachable(NavMeshAgent navMeshAgent, float radiusA, float radiusB)
	{
		if (_pathForCalc == null)
		{
			_pathForCalc = new NavMeshPath();
		}
		bool result = true;
		if (navMeshAgent.isActiveAndEnabled)
		{
			bool flag = false;
			foreach (Transform navMeshPoint in NavMeshPoints)
			{
				navMeshAgent.CalculatePath(navMeshPoint.position, _pathForCalc);
				if (_pathForCalc.status == NavMeshPathStatus.PathComplete)
				{
					float num = 0f;
					Vector3[] corners = _pathForCalc.corners;
					float num2 = ((CommandType != 0) ? radiusB : radiusA);
					num2 += _radius;
					for (int i = 0; i < corners.Length - 1; i++)
					{
						float num3 = Vector3.Distance(corners[i], corners[i + 1]);
						num += num3;
					}
					if (num < num2)
					{
						flag = true;
						break;
					}
				}
			}
			if (!flag)
			{
				result = false;
			}
		}
		else
		{
			result = false;
		}
		return result;
	}

	public bool IsReachable(NavMeshAgent navMeshAgent, float radius)
	{
		if (_pathForCalc == null)
		{
			_pathForCalc = new NavMeshPath();
		}
		bool result = true;
		if (navMeshAgent.isActiveAndEnabled)
		{
			bool flag = false;
			foreach (Transform navMeshPoint in NavMeshPoints)
			{
				navMeshAgent.CalculatePath(navMeshPoint.position, _pathForCalc);
				if (_pathForCalc.status == NavMeshPathStatus.PathComplete)
				{
					float num = 0f;
					Vector3[] corners = _pathForCalc.corners;
					float num2 = radius + _radius;
					for (int i = 0; i < corners.Length - 1; i++)
					{
						float num3 = Vector3.Distance(corners[i], corners[i + 1]);
						num += num3;
					}
					if (num < num2)
					{
						flag = true;
						break;
					}
				}
			}
			if (!flag)
			{
				result = false;
			}
		}
		else
		{
			result = false;
		}
		return result;
	}

	public void SetForceImpossible(bool value)
	{
		IsImpossible = value;
	}

	public virtual bool SetImpossible(bool value, Actor actor)
	{
		if (IsImpossible != value)
		{
			IsImpossible = value;
			if (value)
			{
				SetSlot(actor);
			}
			else if (_actionSlot.Actor != null && _actionSlot.Actor == actor)
			{
				ReleaseSlot(actor);
			}
			return true;
		}
		if (value)
		{
			if (_actionSlot.Actor == null)
			{
				IsImpossible = value;
				SetSlot(actor);
				return true;
			}
			return false;
		}
		if (_actionSlot.Actor != null && _actionSlot.Actor == actor)
		{
			IsImpossible = value;
			ReleaseSlot(actor);
			return true;
		}
		IsImpossible = false;
		return true;
	}

	public virtual bool TutorialHideMode()
	{
		if (Manager.Map.TutorialMode)
		{
			if (_playerEventType.Contains(EventType.Move))
			{
				return false;
			}
			CommonDefine commonDefine = ((!Singleton<Manager.Resources>.IsInstance()) ? null : Singleton<Manager.Resources>.Instance.CommonDefine);
			if (commonDefine != null)
			{
				int[] kitchenPointIDList = commonDefine.Tutorial.KitchenPointIDList;
				if (!kitchenPointIDList.IsNullOrEmpty() && kitchenPointIDList.Contains(_id))
				{
					return false;
				}
			}
			return true;
		}
		return false;
	}

	private void Awake()
	{
		_obstacles = GetComponentsInChildren<NavMeshObstacle>(includeInactive: true);
		NavMeshObstacle[] obstacles = _obstacles;
		foreach (NavMeshObstacle navMeshObstacle in obstacles)
		{
		}
	}

	public void Init()
	{
		DefinePack.MapGroup mapDefines = Singleton<Manager.Resources>.Instance.DefinePack.MapDefines;
		NavMeshPoints.Add(base.transform);
		List<GameObject> list = ListPool<GameObject>.Get();
		base.transform.FindLoopPrefix(list, mapDefines.NavMeshTargetName);
		if (!list.IsNullOrEmpty())
		{
			foreach (GameObject item in list)
			{
				NavMeshPoints.Add(item.transform);
			}
		}
		ListPool<GameObject>.Release(list);
		if (_destination == null)
		{
			_destination = base.transform;
		}
		if (_commandBasePoint == null)
		{
			_commandBasePoint = base.transform.FindLoop(mapDefines.CommandTargetName)?.transform ?? base.transform;
		}
		_agentInfos = new List<ActionPointInfo>();
		_playerInfos = new List<ActionPointInfo>();
		_playerDateInfos = new Dictionary<int, List<DateActionPointInfo>>
		{
			[0] = new List<DateActionPointInfo>(),
			[1] = new List<DateActionPointInfo>()
		};
		_agentDateInfos = new List<DateActionPointInfo>();
		Manager.Map instance = Singleton<Manager.Map>.Instance;
		if (Singleton<Manager.Resources>.Instance.Map.AgentActionPointInfoTable[0].TryGetValue(_id, out var value))
		{
			foreach (ActionPointInfo item2 in value)
			{
				_agentInfos.Add(item2);
			}
		}
		if (Singleton<Manager.Resources>.Instance.Map.PlayerActionPointInfoTable[0].TryGetValue(_id, out var value2))
		{
			foreach (ActionPointInfo item3 in value2)
			{
				_playerInfos.Add(item3);
			}
		}
		PlayerActor player = Singleton<Manager.Map>.Instance.Player;
		if (Singleton<Manager.Resources>.Instance.Map.PlayerDateActionPointInfoTable.TryGetValue(0, out var value3))
		{
			foreach (KeyValuePair<int, Dictionary<int, List<DateActionPointInfo>>> item4 in value3)
			{
				int key = item4.Key;
				if (!item4.Value.TryGetValue(_id, out var value4))
				{
					continue;
				}
				if (!_playerDateInfos.TryGetValue(key, out var value5))
				{
					List<DateActionPointInfo> list2 = new List<DateActionPointInfo>();
					_playerDateInfos[key] = list2;
					value5 = list2;
				}
				foreach (DateActionPointInfo item5 in value4)
				{
					value5.Add(item5);
				}
			}
		}
		if (Singleton<Manager.Resources>.Instance.Map.AgentDateActionPointInfoTable[0].TryGetValue(_id, out var value6))
		{
			foreach (DateActionPointInfo item6 in value6)
			{
				_agentDateInfos.Add(item6);
			}
		}
		if (!_idList.IsNullOrEmpty())
		{
			_agentInfos.Clear();
			_playerInfos.Clear();
			foreach (KeyValuePair<int, List<DateActionPointInfo>> playerDateInfo in _playerDateInfos)
			{
				playerDateInfo.Value.Clear();
			}
			_agentDateInfos.Clear();
			int[] idList = _idList;
			foreach (int key2 in idList)
			{
				if (Singleton<Manager.Resources>.Instance.Map.PlayerActionPointInfoTable[0].TryGetValue(key2, out value))
				{
					foreach (ActionPointInfo item7 in value)
					{
						_playerInfos.Add(item7);
					}
				}
				if (Singleton<Manager.Resources>.Instance.Map.AgentActionPointInfoTable[0].TryGetValue(key2, out value2))
				{
					foreach (ActionPointInfo item8 in value2)
					{
						_agentInfos.Add(item8);
					}
				}
				if (Singleton<Manager.Resources>.Instance.Map.PlayerDateActionPointInfoTable.TryGetValue(0, out value3))
				{
					foreach (KeyValuePair<int, Dictionary<int, List<DateActionPointInfo>>> item9 in value3)
					{
						int key3 = item9.Key;
						if (!item9.Value.TryGetValue(key2, out var value7))
						{
							continue;
						}
						if (!_playerDateInfos.TryGetValue(key3, out var value8))
						{
							List<DateActionPointInfo> list2 = new List<DateActionPointInfo>();
							_playerDateInfos[key3] = list2;
							value8 = list2;
						}
						foreach (DateActionPointInfo item10 in value7)
						{
							value8.Add(item10);
						}
					}
				}
				if (!Singleton<Manager.Resources>.Instance.Map.AgentDateActionPointInfoTable[0].TryGetValue(key2, out value6))
				{
					continue;
				}
				foreach (DateActionPointInfo item11 in value6)
				{
					_agentDateInfos.Add(item11);
				}
			}
		}
		base.Start();
		EventType eventType = (EventType)0;
		foreach (ActionPointInfo playerInfo in _playerInfos)
		{
			eventType |= playerInfo.eventTypeMask;
		}
		_playerEventType = eventType;
		eventType = (EventType)0;
		foreach (ActionPointInfo agentInfo in _agentInfos)
		{
			eventType |= agentInfo.eventTypeMask;
		}
		_agentEventType = eventType;
		_playerDateEventType = new EventType[2];
		for (int j = 0; j < 2; j++)
		{
			eventType = (EventType)0;
			foreach (DateActionPointInfo item12 in _playerDateInfos[j])
			{
				eventType |= item12.eventTypeMask;
			}
			_playerDateEventType[j] = eventType;
		}
		eventType = (EventType)0;
		foreach (DateActionPointInfo agentDateInfo in _agentDateInfos)
		{
			eventType |= agentDateInfo.eventTypeMask;
		}
		_agentDateEventType = eventType;
		InitSub();
	}

	protected virtual void InitSub()
	{
		CommonDefine.CommonIconGroup icon = Singleton<Manager.Resources>.Instance.CommonDefine.Icon;
		List<CommandLabel.CommandInfo> list = ListPool<CommandLabel.CommandInfo>.Get();
		foreach (KeyValuePair<EventType, Tuple<int, string, Action<PlayerActor, ActionPoint>>> pair3 in LabelTable)
		{
			if (!_playerEventType.Contains(pair3.Key) || !AIProject.Definitions.Action.NameTable.TryGetValue(pair3.Key, out var _))
			{
				continue;
			}
			ActionPointInfo actionPointInfo = _playerInfos.Find((ActionPointInfo x) => x.eventTypeMask == pair3.Key);
			string actionName = actionPointInfo.actionName;
			Singleton<Manager.Resources>.Instance.itemIconTables.ActionIconTable.TryGetValue(actionPointInfo.iconID, out var value2);
			Transform transform = base.transform.FindLoop(actionPointInfo.labelNullName)?.transform ?? base.transform;
			CommandLabel.CommandInfo commandInfo = new CommandLabel.CommandInfo();
			commandInfo.Text = actionName;
			commandInfo.Icon = value2;
			commandInfo.IsHold = pair3.Key == EventType.Sleep || pair3.Key == EventType.Break || pair3.Key == EventType.Search || pair3.Key == EventType.StorageIn || pair3.Key == EventType.Cook || pair3.Key == EventType.DressIn || pair3.Key == EventType.ClothChange;
			commandInfo.TargetSpriteInfo = icon.ActionSpriteInfo;
			commandInfo.Transform = transform;
			commandInfo.Condition = null;
			commandInfo.Event = delegate
			{
				pair3.Value.Item3(Singleton<Manager.Map>.Instance.Player, this);
			};
			CommandLabel.CommandInfo commandInfo2 = commandInfo;
			if (pair3.Key == EventType.Sleep)
			{
				commandInfo2.Condition = (PlayerActor player) => Singleton<Manager.Map>.Instance.CanSleepInTime() ? true : false;
				commandInfo2.ErrorText = (PlayerActor player) => Singleton<Manager.Map>.Instance.CanSleepInTime() ? string.Empty : "夜になるまで寝ることはできません";
			}
			list.Add(commandInfo2);
		}
		_labels = list.ToArray();
		ListPool<CommandLabel.CommandInfo>.Release(list);
		for (int i = 0; i < 2; i++)
		{
			List<CommandLabel.CommandInfo> list2 = ListPool<CommandLabel.CommandInfo>.Get();
			foreach (KeyValuePair<EventType, Tuple<int, string, Action<PlayerActor, ActionPoint>>> pair2 in DateLabelTable)
			{
				if (_playerDateEventType[i].Contains(pair2.Key) && AIProject.Definitions.Action.NameTable.TryGetValue(pair2.Key, out var _) && _playerDateInfos.TryGetValue(i, out var value4))
				{
					DateActionPointInfo dateActionPointInfo = value4.Find((DateActionPointInfo x) => x.eventTypeMask == pair2.Key);
					string actionName2 = dateActionPointInfo.actionName;
					Singleton<Manager.Resources>.Instance.itemIconTables.ActionIconTable.TryGetValue(dateActionPointInfo.iconID, out var value5);
					Transform transform2 = base.transform.FindLoop(dateActionPointInfo.labelNullName)?.transform ?? base.transform;
					list2.Add(new CommandLabel.CommandInfo
					{
						Text = actionName2,
						Icon = value5,
						IsHold = (pair2.Key == EventType.Sleep || pair2.Key == EventType.Break || pair2.Key == EventType.Eat || pair2.Key == EventType.Search || pair2.Key == EventType.StorageIn || pair2.Key == EventType.Cook || pair2.Key == EventType.DressIn || pair2.Key == EventType.Lesbian || pair2.Key == EventType.ClothChange),
						TargetSpriteInfo = icon.ActionSpriteInfo,
						Transform = transform2,
						Condition = null,
						Event = delegate
						{
							pair2.Value.Item3(Singleton<Manager.Map>.Instance.Player, this);
						}
					});
				}
			}
			_dateLabels[i] = list2.ToArray();
			ListPool<CommandLabel.CommandInfo>.Release(list2);
		}
		List<CommandLabel.CommandInfo> list3 = ListPool<CommandLabel.CommandInfo>.Get();
		foreach (KeyValuePair<EventType, Tuple<int, string, Action<PlayerActor, ActionPoint>>> pair in SickLabelTable)
		{
			if (_playerEventType.Contains(pair.Key) && AIProject.Definitions.Action.NameTable.TryGetValue(pair.Key, out var _))
			{
				ActionPointInfo actionPointInfo2 = _playerInfos.Find((ActionPointInfo x) => x.eventTypeMask == pair.Key);
				Singleton<Manager.Resources>.Instance.itemIconTables.ActionIconTable.TryGetValue(actionPointInfo2.iconID, out var value7);
				Transform transform3 = base.transform.FindLoop(actionPointInfo2.labelNullName)?.transform ?? base.transform;
				list3.Add(new CommandLabel.CommandInfo
				{
					Text = pair.Value.Item2,
					Icon = value7,
					IsHold = true,
					TargetSpriteInfo = icon.ActionSpriteInfo,
					Transform = transform3,
					Condition = null,
					Event = delegate
					{
						pair.Value.Item3(Singleton<Manager.Map>.Instance.Player, this);
					}
				});
			}
		}
		_sickLabels = list3.ToArray();
		ListPool<CommandLabel.CommandInfo>.Release(list3);
	}

	public void SetActiveMapItemObjs(bool active)
	{
		if (!_mapItemObjs.IsNullOrEmpty())
		{
			GameObject[] mapItemObjs = _mapItemObjs;
			foreach (GameObject gameObject in mapItemObjs)
			{
				if (gameObject.activeSelf != active)
				{
					gameObject.SetActive(active);
				}
			}
		}
		if (_mapItemData.IsNullOrEmpty())
		{
			return;
		}
		MapItemKeyValuePair[] mapItemData = _mapItemData;
		foreach (MapItemKeyValuePair mapItemKeyValuePair in mapItemData)
		{
			if (mapItemKeyValuePair != null && !(mapItemKeyValuePair.ItemObj == null))
			{
				mapItemKeyValuePair.ItemObj.SetActiveIfDifferent(active);
			}
		}
	}

	public GameObject CreateEventItems(int itemID, string parentName, bool isSync)
	{
		if (_mapItemData.IsNullOrEmpty())
		{
			return null;
		}
		return _mapItemData.FirstOrDefault((MapItemKeyValuePair x) => x.ID == itemID)?.ItemObj;
	}

	public override void LocateGround()
	{
		base.LocateGround();
	}

	protected override void OnDisable()
	{
		PlayerActor playerActor = ((!Singleton<Manager.Map>.IsInstance()) ? null : Singleton<Manager.Map>.Instance.Player);
		if (playerActor != null)
		{
			CommandArea commandArea = playerActor.PlayerController.CommandArea;
			if (SetImpossible(value: false, playerActor) && commandArea.ContainsConsiderationObject(this))
			{
				commandArea.RemoveConsiderationObject(this);
				commandArea.RefreshCommands();
			}
		}
		base.OnDisable();
	}

	public int GetPriority(int charaID)
	{
		return (0 > charaID) ? (charaID * -1 * 1000) : charaID;
	}

	public bool AddBooking(Actor actor)
	{
		int charaID = actor.ID;
		List<ActionPoint> list = ListPool<ActionPoint>.Get();
		list.Add(this);
		list.AddRange(ConnectedActionPoints);
		bool result = false;
		foreach (ActionPoint item in list)
		{
			if (!item._bookingList.Exists((UnityEx.ValueTuple<int, int> x) => x.Item2 == charaID))
			{
				item._bookingList.Add(new UnityEx.ValueTuple<int, int>(GetPriority(charaID), charaID));
				item._bookingList.Sort((UnityEx.ValueTuple<int, int> x, UnityEx.ValueTuple<int, int> y) => x.Item1 - y.Item1);
				result = true;
			}
		}
		ListPool<ActionPoint>.Release(list);
		return result;
	}

	public bool RemoveBooking(Actor actor)
	{
		int charaID = actor.ID;
		List<ActionPoint> list = ListPool<ActionPoint>.Get();
		list.Add(this);
		list.AddRange(ConnectedActionPoints);
		bool flag = false;
		foreach (ActionPoint item in list)
		{
			int num = item._bookingList.RemoveAll((UnityEx.ValueTuple<int, int> x) => x.Item2 == charaID);
			flag = flag || 1 <= num;
			if (0 < num)
			{
				item._bookingList.Sort((UnityEx.ValueTuple<int, int> x, UnityEx.ValueTuple<int, int> y) => x.Item1 - y.Item1);
			}
		}
		if (BookingUser == actor)
		{
			RemoveBookingUser(actor);
		}
		ListPool<ActionPoint>.Release(list);
		return flag;
	}

	public bool OffMeshAvailablePoint(Actor actor)
	{
		if (BookingUser != null)
		{
			return false;
		}
		if (_bookingList.IsNullOrEmpty())
		{
			AddBooking(actor);
			return true;
		}
		return _bookingList[0].Item2 == actor.ID;
	}

	public bool ForceUse(Actor user)
	{
		if (!(BookingUser == null) && !(BookingUser == user))
		{
			return false;
		}
		SetBookingUser(user);
		if (Singleton<Manager.Map>.IsInstance())
		{
			PlayerActor player = Singleton<Manager.Map>.Instance.Player;
			CommandArea commandArea = player.PlayerController.CommandArea;
			List<ActionPoint> list = ListPool<ActionPoint>.Get();
			list.Add(this);
			list.AddRange(ConnectedActionPoints);
			bool flag = false;
			foreach (ActionPoint item in list)
			{
				if (!(item == null) && item.SetImpossible(value: false, player) && commandArea.ContainsConsiderationObject(item))
				{
					commandArea.RemoveConsiderationObject(item);
					flag = true;
				}
			}
			if (flag)
			{
				commandArea.RefreshCommands();
			}
			ListPool<ActionPoint>.Release(list);
		}
		return true;
	}

	public bool SetBookingUser(Actor user)
	{
		if (user == null)
		{
			return false;
		}
		List<ActionPoint> list = ListPool<ActionPoint>.Get();
		list.Add(this);
		list.AddRange(ConnectedActionPoints);
		bool flag = true;
		foreach (ActionPoint item in list)
		{
			if (!(item == null))
			{
				flag &= item.BookingUser == null || item.BookingUser == user;
				item.BookingUser = user;
			}
		}
		ListPool<ActionPoint>.Release(list);
		return flag;
	}

	public bool RemoveBookingUser(Actor user)
	{
		if (user == null)
		{
			return false;
		}
		List<ActionPoint> list = ListPool<ActionPoint>.Get();
		list.Add(this);
		list.AddRange(ConnectedActionPoints);
		bool flag = true;
		foreach (ActionPoint item in list)
		{
			if (!(item == null))
			{
				flag &= item.BookingUser == null || item.BookingUser == user;
				item.BookingUser = null;
			}
		}
		ListPool<ActionPoint>.Release(list);
		return true;
	}

	private void TransitionAction()
	{
	}

	public Tuple<Transform, Transform> GetSlot(Actor actor)
	{
		return new Tuple<Transform, Transform>(_actionSlot.Point, _actionSlot.RecoveryPoint);
	}

	public Tuple<Transform, Transform> SetSlot(Actor actor)
	{
		if (_actionSlot.Actor == actor)
		{
			actor.CurrentPoint = this;
			return GetSlot(actor);
		}
		_actionSlot.Actor = actor;
		return GetSlot(actor);
	}

	public void ReleaseSlot(Actor actor)
	{
		if (actor == _actionSlot.Actor)
		{
			_actionSlot.Actor = null;
			Reserver = null;
			actor.CurrentPoint = null;
		}
	}

	public void CreateByproduct(int actionID, int poseID)
	{
		MapItemNull component = GetComponent<MapItemNull>();
		if (!(component == null) && Singleton<Manager.Resources>.IsInstance() && Singleton<Manager.Resources>.Instance.Action.ByproductList.TryGetValue(actionID, out var value) && value.TryGetValue(poseID, out var value2))
		{
			List<int> element = value2.ItemList.GetElement(UnityEngine.Random.Range(0, value2.ItemList.Count));
			component.SetActiveObjs(active: true, element);
		}
	}

	public void DestroyByproduct(int actionID, int poseID)
	{
		MapItemNull component = GetComponent<MapItemNull>();
		if (!(component == null))
		{
			component.SetActiveObjs(active: false);
		}
	}

	public bool IsReserved(AgentActor agent)
	{
		if (Reserver != null && Reserver != agent)
		{
			if (!(Reserver is AgentActor))
			{
				return true;
			}
			AgentActor agentActor = Reserver as AgentActor;
			if (agentActor.TargetInSightActionPoint == this)
			{
				return true;
			}
			Reserver = null;
		}
		return false;
	}
}
