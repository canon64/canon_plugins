using BehaviorDesigner.Runtime.Tasks;
using Manager;

namespace AIProject;

[TaskCategory("")]
public class ResetState : AgentAction
{
	public override TaskStatus OnUpdate()
	{
		Singleton<Map>.Instance.Player.CameraControl.CrossFade.FadeStart();
		base.Agent.ActivateTransfer();
		return TaskStatus.Success;
	}
}
