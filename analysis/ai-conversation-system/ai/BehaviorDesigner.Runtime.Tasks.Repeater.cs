namespace BehaviorDesigner.Runtime.Tasks;

[TaskDescription("The repeater task will repeat execution of its child task until the child task has been run a specified number of times. It has the option of continuing to execute the child task even if the child task returns a failure.")]
[TaskIcon("{SkinColor}RepeaterIcon.png")]
public class Repeater : Decorator
{
	[Tooltip("The number of times to repeat the execution of its child task")]
	public SharedInt count = 1;

	[Tooltip("Allows the repeater to repeat forever")]
	public SharedBool repeatForever;

	[Tooltip("Should the task return if the child task returns a failure")]
	public SharedBool endOnFailure;

	private int executionCount;

	private TaskStatus executionStatus;

	public override bool CanExecute()
	{
		return (repeatForever.Value || executionCount < count.Value) && (!endOnFailure.Value || (endOnFailure.Value && executionStatus != TaskStatus.Failure));
	}

	public override void OnChildExecuted(TaskStatus childStatus)
	{
		executionCount++;
		executionStatus = childStatus;
	}

	public override void OnEnd()
	{
		executionCount = 0;
		executionStatus = TaskStatus.Inactive;
	}

	public override void OnReset()
	{
		count = 0;
		endOnFailure = true;
	}
}
