using System;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace AIProject;

[TaskCategory("")]
public class SetSickDuration : AgentAction
{
	[SerializeField]
	private float _dailyDuration = 1f;

	public override TaskStatus OnUpdate()
	{
		TimeSpan duration = TimeSpan.FromDays(_dailyDuration);
		base.Agent.AgentData.SickState.Duration = duration;
		return TaskStatus.Success;
	}
}
