using System.Collections.Generic;
using System.Reflection;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace AIProject;

public class AgentBehaviorTree : Behavior
{
	[SerializeField]
	private AgentActor _sourceAgent;

	private bool _initialized;

	private bool _isPause;

	private ExternalBehavior _externalBehavior;

	public AgentActor SourceAgent
	{
		get
		{
			return _sourceAgent;
		}
		set
		{
			_sourceAgent = value;
		}
	}

	public new ExternalBehavior ExternalBehavior
	{
		get
		{
			return _externalBehavior;
		}
		set
		{
			if (_externalBehavior == value)
			{
				return;
			}
			if (BehaviorManager.instance != null)
			{
				BehaviorManager.instance.DisableBehavior(this);
			}
			if (value != null && value.Initialized)
			{
				BehaviorSource behaviorSource = GetBehaviorSource();
				List<SharedVariable> allVariables = behaviorSource.GetAllVariables();
				behaviorSource = value.BehaviorSource;
				behaviorSource.HasSerialized = true;
				if (allVariables != null)
				{
					for (int i = 0; i < allVariables.Count; i++)
					{
						if (allVariables[i] != null)
						{
							behaviorSource.SetVariable(allVariables[i].Name, allVariables[i]);
						}
					}
				}
				SetBehaviorSource(behaviorSource);
			}
			else
			{
				GetBehaviorSource().HasSerialized = false;
				base.HasInheritedVariables = false;
			}
			_initialized = false;
			_externalBehavior = value;
			if (base.StartWhenEnabled)
			{
				EnableBehavior();
			}
		}
	}

	private new void Start()
	{
		if (base.StartWhenEnabled)
		{
			base.StartWhenEnabled = false;
		}
		base.Start();
	}

	public new void OnEnable()
	{
		if (BehaviorManager.instance != null && _isPause)
		{
			BehaviorManager.instance.EnableBehavior(this);
			_isPause = false;
		}
		else if (base.StartWhenEnabled && _initialized)
		{
			EnableBehavior();
		}
	}

	public static AIBehaviorManager CreateAIBehaviorManager()
	{
		if (BehaviorManager.instance == null && Application.isPlaying)
		{
			GameObject gameObject = new GameObject();
			gameObject.name = "Behavior Manager";
			return gameObject.AddComponent<AIBehaviorManager>();
		}
		return null;
	}

	public new void EnableBehavior()
	{
		CreateAIBehaviorManager();
		if (BehaviorManager.instance != null)
		{
			BehaviorManager.instance.EnableBehavior(this);
		}
		BehaviorSource behaviorSource = GetBehaviorSource();
		if (!_initialized)
		{
			for (int i = 0; i < 12; i++)
			{
				bool[] array = base.HasEvent;
				int num = i;
				EventTypes eventTypes = (EventTypes)i;
				array[num] = TaskContainsMethod(eventTypes.ToString(), behaviorSource.RootTask);
			}
		}
	}

	public new void DisableBehavior()
	{
		if (BehaviorManager.instance != null)
		{
			BehaviorManager.instance.DisableBehavior(this, base.PauseWhenDisabled);
			_isPause = base.PauseWhenDisabled;
		}
	}

	public new void DisableBehavior(bool pause)
	{
		if (BehaviorManager.instance != null)
		{
			BehaviorManager.instance.DisableBehavior(this, pause);
			_isPause = pause;
		}
	}

	private bool TaskContainsMethod(string methodName, Task task)
	{
		if (task == null)
		{
			return false;
		}
		MethodInfo method = task.GetType().GetMethod(methodName, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if (method != null && method.DeclaringType.IsAssignableFrom(task.GetType()))
		{
			return true;
		}
		if (task is ParentTask)
		{
			ParentTask parentTask = task as ParentTask;
			if (parentTask.Children != null)
			{
				for (int i = 0; i < parentTask.Children.Count; i++)
				{
					if (TaskContainsMethod(methodName, parentTask.Children[i]))
					{
						return true;
					}
				}
			}
		}
		return false;
	}
}
