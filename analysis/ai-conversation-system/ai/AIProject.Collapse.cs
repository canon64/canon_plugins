using AIProject.Definitions;
using BehaviorDesigner.Runtime.Tasks;
using Manager;

namespace AIProject;

[TaskCategory("")]
public class Collapse : AgentAction
{
	private int _layer = -1;

	protected bool _inEnableFade;

	protected float _inFadeSecond;

	protected bool _outEnableFade;

	protected float _outFadeSecond;

	public override void OnStart()
	{
		base.OnStart();
		AgentActor agent = base.Agent;
		agent.StateType = State.Type.Collapse;
		agent.StopNavMeshAgent();
		agent.ChangeStaticNavMeshAgentAvoidance();
		PoseKeyPair collapseID = Singleton<Resources>.Instance.AgentProfile.PoseIDTable.CollapseID;
		PlayState playState = Singleton<Resources>.Instance.Animation.AgentActionAnimTable[collapseID.postureID][collapseID.poseID];
		agent.Animation.LoadEventKeyTable(collapseID.postureID, collapseID.poseID);
		_layer = playState.Layer;
		_inEnableFade = playState.MainStateInfo.InStateInfo.EnableFade;
		_inFadeSecond = playState.MainStateInfo.InStateInfo.FadeSecond;
		base.Agent.Animation.InitializeStates(playState);
		base.Agent.Animation.PlayInAnimation(_inEnableFade, _inFadeSecond, playState.MainStateInfo.FadeOutTime, _layer);
	}

	public override TaskStatus OnUpdate()
	{
		if (base.Agent.Animation.PlayingInAnimation)
		{
			return TaskStatus.Running;
		}
		return TaskStatus.Success;
	}

	public override void OnEnd()
	{
	}
}
