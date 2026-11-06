namespace BehaviorDesigner.Runtime.Tasks;

[TaskDescription("The sequence task is similar to an \"and\" operation. It will return failure as soon as one of its child tasks return failure. If a child task returns success then it will sequentially run the next task. If all child tasks return success then it will return success.")]
[TaskIcon("{SkinColor}SequenceIcon.png")]
public class Sequence : Composite
{
	private int currentChildIndex;

	private TaskStatus executionStatus;

	public override int CurrentChildIndex()
	{
		return currentChildIndex;
	}

	public override bool CanExecute()
	{
		return currentChildIndex < children.Count && executionStatus != TaskStatus.Failure;
	}

	public override void OnChildExecuted(TaskStatus childStatus)
	{
		currentChildIndex++;
		executionStatus = childStatus;
	}

	public override void OnConditionalAbort(int childIndex)
	{
		currentChildIndex = childIndex;
		executionStatus = TaskStatus.Inactive;
	}

	public override void OnEnd()
	{
		executionStatus = TaskStatus.Inactive;
		currentChildIndex = 0;
	}
}
