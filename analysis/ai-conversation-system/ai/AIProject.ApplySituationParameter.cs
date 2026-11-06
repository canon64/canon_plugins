using AIProject.Definitions;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace AIProject;

[TaskCategory("")]
public class ApplySituationParameter : AgentAction
{
	[SerializeField]
	private Situation.Types _situation;

	public override TaskStatus OnUpdate()
	{
		base.Agent.ApplySituationResultParameter((int)_situation);
		return TaskStatus.Success;
	}
}
