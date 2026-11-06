namespace BehaviorDesigner.Runtime.Tasks;

[TaskDescription("The selector task is similar to an \"or\" operation. It will return success as soon as one of its child tasks return success. If a child task returns failure then it will sequentially run the next task. If no child task returns success then it will return failure.")]
[TaskIcon("{SkinColor}SelectorIcon.png")]
public class Selector : Composite
{
	private int currentChildIndex;

	private TaskStatus executionStatus;

	public override int CurrentChildIndex()
	{
		return currentChildIndex;
	}

	public override bool CanExecute()
	{
		return currentChildIndex < children.Count && executionStatus != TaskStatus.Success;
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
