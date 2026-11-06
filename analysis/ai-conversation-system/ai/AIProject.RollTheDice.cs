using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace AIProject;

[TaskCategory("")]
public class RollTheDice : AgentConditional
{
	[SerializeField]
	private SharedFloat _border = 100f;

	public override TaskStatus OnUpdate()
	{
		float num = Random.Range(0f, 100f);
		if (num <= _border.Value)
		{
			return TaskStatus.Success;
		}
		return TaskStatus.Failure;
	}
}
