using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AIProject.Definitions;
using Manager;
using Sirenix.OdinInspector;
using UniRx;
using UnityEngine;

namespace AIProject;

public class BehaviorTreeResources : SerializedMonoBehaviour
{
	[SerializeField]
	[HideInInspector]
	private AgentBehaviorTree _current;

	[SerializeField]
	private AgentActor _sourceAgent;

	[SerializeField]
	private Dictionary<Desire.ActionType, AgentBehaviorTree> _behaviorTreeTable = new Dictionary<Desire.ActionType, AgentBehaviorTree>();

	public AgentBehaviorTree Current
	{
		[CompilerGenerated]
		get
		{
			return _current;
		}
	}

	public Desire.ActionType Mode { get; private set; }

	public AgentActor SourceAgent
	{
		[CompilerGenerated]
		get
		{
			return _sourceAgent;
		}
	}

	public AgentBehaviorTree GetBehaviorTree(Desire.ActionType mode)
	{
		if (_behaviorTreeTable.TryGetValue(mode, out var value))
		{
			return value;
		}
		return null;
	}

	public void Initialize()
	{
		_behaviorTreeTable.Clear();
		foreach (Desire.ActionType value in Enum.GetValues(typeof(Desire.ActionType)))
		{
			AgentBehaviorTree behavior = Singleton<Manager.Resources>.Instance.BehaviorTree.GetBehavior(value);
			if (behavior != null)
			{
				AgentBehaviorTree agentBehaviorTree = UnityEngine.Object.Instantiate(behavior);
				agentBehaviorTree.transform.SetParent(base.transform, worldPositionStays: false);
				agentBehaviorTree.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
				_behaviorTreeTable[value] = agentBehaviorTree;
				agentBehaviorTree.SourceAgent = _sourceAgent;
			}
		}
		foreach (AgentBehaviorTree value2 in _behaviorTreeTable.Values)
		{
			value2.StartWhenEnabled = false;
		}
	}

	public void ChangeMode(Desire.ActionType mode)
	{
		if (_behaviorTreeTable.TryGetValue(mode, out var value) && !(Current == value))
		{
			Debug.Log($"[{SourceAgent.gameObject.name}] モード変更 {Mode} → {mode}");
			Mode = mode;
			Current?.DisableBehavior(pause: false);
			_current = value;
			Observable.NextFrame().Subscribe(delegate
			{
				Current.EnableBehavior();
			});
		}
	}

	private void OnEnable()
	{
		Current?.EnableBehavior();
	}

	private void OnDisable()
	{
		Current?.DisableBehavior(pause: true);
	}

	public void DisableAllBehaviors()
	{
		foreach (KeyValuePair<Desire.ActionType, AgentBehaviorTree> item in _behaviorTreeTable)
		{
			if (!(item.Value == null))
			{
				item.Value.DisableBehavior();
			}
		}
	}
}
