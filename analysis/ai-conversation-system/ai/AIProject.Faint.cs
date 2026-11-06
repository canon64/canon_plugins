using AIProject.Definitions;
using BehaviorDesigner.Runtime.Tasks;
using Manager;

namespace AIProject;

[TaskCategory("")]
public class Faint : AgentAction
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
		agent.StateType = State.Type.Immobility;
		agent.StopNavMeshAgent();
		agent.ChangeStaticNavMeshAgentAvoidance();
		PoseKeyPair faintID = Singleton<Resources>.Instance.AgentProfile.PoseIDTable.FaintID;
		PlayState playState = Singleton<Resources>.Instance.Animation.AgentActionAnimTable[faintID.postureID][faintID.poseID];
		agent.Animation.LoadEventKeyTable(faintID.postureID, faintID.poseID);
		_layer = playState.Layer;
		_inEnableFade = playState.MainStateInfo.InStateInfo.EnableFade;
		_inFadeSecond = playState.MainStateInfo.InStateInfo.FadeSecond;
		agent.Animation.InitializeStates(playState);
		agent.Animation.StopAllAnimCoroutine();
		agent.Animation.PlayInAnimation(_inEnableFade, _inFadeSecond, playState.MainStateInfo.FadeOutTime, _layer);
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
		base.Agent.ChangeDynamicNavMeshAgentAvoidance();
	}
}
