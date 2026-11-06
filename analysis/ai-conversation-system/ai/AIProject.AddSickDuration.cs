using System;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace AIProject;

[TaskCategory("")]
public class AddSickDuration : AgentAction
{
	[SerializeField]
	private float _dailyDuration = 1f;

	public override TaskStatus OnUpdate()
	{
		TimeSpan timeSpan = TimeSpan.FromDays(_dailyDuration);
		base.Agent.AgentData.SickState.Duration += timeSpan;
		return TaskStatus.Success;
	}
}
