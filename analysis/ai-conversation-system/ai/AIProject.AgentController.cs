using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using AIProject.Definitions;
using AIProject.SaveData;
using Manager;
using UniRx;
using UnityEngine;
using UnityEx;

namespace AIProject;

[RequireComponent(typeof(ActorLocomotion))]
public class AgentController : ActorController
{
	public enum PermissionStatus
	{
		None,
		Prohibition,
		Permission
	}

	[SerializeField]
	private ActorLocomotionAgent _character;

	[SerializeField]
	private SearchArea _searchArea;

	[SerializeField]
	private float _distanceReached = 0.5f;

	private bool _sleepedSchedule;

	private DateTime _startTime = DateTime.MinValue;

	private TimeSpan _extendedTime = TimeSpan.MinValue;

	public SearchArea SearchArea
	{
		[CompilerGenerated]
		get
		{
			return _searchArea;
		}
	}

	public float DistanceReached
	{
		[CompilerGenerated]
		get
		{
			return _distanceReached;
		}
	}

	public int PrevID { get; private set; } = -1;


	public EventType PrevEvent { get; private set; }

	public Rarelity PrevRarelity { get; private set; } = Rarelity.N;


	public PermissionStatus Permission { get; private set; }

	public bool SleepedSchedule
	{
		get
		{
			return _sleepedSchedule;
		}
		set
		{
			if (value != _sleepedSchedule)
			{
				_sleepedSchedule = value;
				EnvironmentSimulator simulator = Singleton<Manager.Map>.Instance.Simulator;
				if (value)
				{
					_startTime = simulator.Now;
					return;
				}
				DateTime now = simulator.Now;
				_extendedTime = now - _startTime;
			}
		}
	}

	private void OnDisable()
	{
		if ((bool)_character)
		{
			_character.enabled = false;
		}
	}

	public override void StartBehavior()
	{
		if (_character == null)
		{
			_character = GetComponent<ActorLocomotionAgent>();
		}
	}

	protected override void Start()
	{
		base.Start();
		(from _ in Observable.EveryUpdate().TakeUntilDestroy(base.gameObject)
			where base.isActiveAndEnabled
			select _).Subscribe(delegate
		{
			OnUpdate();
		});
	}

	private void OnUpdate()
	{
		bool? flag = (((object)_actor != null) ? new bool?(_actor.IsInit) : null);
		if (flag.HasValue && flag.Value)
		{
			if ((object)_character != null)
			{
				_character.Move(Vector3.zero);
			}
			if (!Singleton<Manager.Scene>.Instance.IsNowLoadingFade)
			{
			}
		}
	}

	protected override void SubFixedUpdate()
	{
		if ((object)_character != null)
		{
			_character.UpdateState();
		}
	}

	public PermissionStatus GetPermission(ActionPoint point)
	{
		AgentActor agentActor = _actor as AgentActor;
		UnityEx.ValueTuple<EventType, Desire.Type>[] valuePairs = Desire.ValuePairs;
		Desire.Type requestedDesire = agentActor.RequestedDesire;
		if (!point.IsNeutralCommand)
		{
			Debug.Log("行動ポイントが満員御礼");
			PermissionStatus permissionStatus2 = (Permission = PermissionStatus.Prohibition);
			PermissionStatus permissionStatus3;
			return permissionStatus3 = permissionStatus2;
		}
		if (AnyDesire(valuePairs, requestedDesire))
		{
			EventType type = FirstDesire(valuePairs, requestedDesire).Item1;
			if (requestedDesire == Desire.Type.Bath && (float)agentActor.ChaControl.fileGameInfo.flavorState[2] < Singleton<Manager.Resources>.Instance.StatusProfile.CanDressBorder)
			{
				type = EventType.Bath;
			}
			if (point.AgentEventType.Contains(type))
			{
				if (point is SearchActionPoint)
				{
					SearchActionPoint searchActionPoint = point as SearchActionPoint;
					StuffItem itemInfo = agentActor.AgentData.EquipedSearchItem(searchActionPoint.TableID);
					if (agentActor.SearchAreaID == 0)
					{
						if (searchActionPoint.TableID == 0 || searchActionPoint.TableID == 1 || searchActionPoint.TableID == 2)
						{
							if (searchActionPoint.CanSearch(EventType.Search, itemInfo))
							{
								return Permission = PermissionStatus.Permission;
							}
							return Permission = PermissionStatus.Prohibition;
						}
						return Permission = PermissionStatus.Prohibition;
					}
					if (agentActor.SearchAreaID == searchActionPoint.TableID)
					{
						if (searchActionPoint.CanSearch(EventType.Search, itemInfo))
						{
							return Permission = PermissionStatus.Permission;
						}
						return Permission = PermissionStatus.Prohibition;
					}
					return Permission = PermissionStatus.Prohibition;
				}
				return Permission = PermissionStatus.Permission;
			}
			return Permission = PermissionStatus.Prohibition;
		}
		return Permission = PermissionStatus.Prohibition;
	}

	private static bool AnyDesire(ReadOnlyDictionary<EventType, Desire.Type> table, Desire.Type type)
	{
		foreach (KeyValuePair<EventType, Desire.Type> item in table)
		{
			if (item.Value == type)
			{
				return true;
			}
		}
		return false;
	}

	private static bool AnyDesire(UnityEx.ValueTuple<EventType, Desire.Type>[] collection, Desire.Type type)
	{
		for (int i = 0; i < collection.Length; i++)
		{
			UnityEx.ValueTuple<EventType, Desire.Type> valueTuple = collection[i];
			if (valueTuple.Item2 == type)
			{
				return true;
			}
		}
		return false;
	}

	private static KeyValuePair<EventType, Desire.Type> FirstDesire(ReadOnlyDictionary<EventType, Desire.Type> table, Desire.Type type)
	{
		foreach (KeyValuePair<EventType, Desire.Type> item in table)
		{
			if (item.Value == type)
			{
				return item;
			}
		}
		return default(KeyValuePair<EventType, Desire.Type>);
	}

	public static UnityEx.ValueTuple<EventType, Desire.Type> FirstDesire(UnityEx.ValueTuple<EventType, Desire.Type>[] collection, Desire.Type type)
	{
		for (int i = 0; i < collection.Length; i++)
		{
			UnityEx.ValueTuple<EventType, Desire.Type> result = collection[i];
			if (result.Item2 == type)
			{
				return result;
			}
		}
		return default(UnityEx.ValueTuple<EventType, Desire.Type>);
	}

	private void OnTriggerExit(Collider other)
	{
	}

	public override void ChangeState(string stateName)
	{
		if (Singleton<Manager.Scene>.Instance.AddSceneName.IsNullOrEmpty())
		{
		}
	}
}
