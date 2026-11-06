using AIProject.Definitions;
using AIProject.SaveData;
using BehaviorDesigner.Runtime.Tasks;
using Manager;

namespace AIProject;

[TaskCategory("")]
public class Cure : AgentAction
{
	private int _layer;

	private bool _inEnableFade;

	private float _inFadeSecond;

	public override void OnStart()
	{
		base.OnStart();
		AgentActor agent = base.Agent;
		agent.StateType = State.Type.Immobility;
		agent.StopNavMeshAgent();
		agent.ChangeStaticNavMeshAgentAvoidance();
		PoseKeyPair cureID = Singleton<Resources>.Instance.AgentProfile.PoseIDTable.CureID;
		PlayState playState = Singleton<Resources>.Instance.Animation.AgentActionAnimTable[cureID.postureID][cureID.poseID];
		agent.Animation.LoadEventKeyTable(cureID.postureID, cureID.poseID);
		_layer = playState.Layer;
		_inEnableFade = playState.MainStateInfo.InStateInfo.EnableFade;
		_inFadeSecond = playState.MainStateInfo.InStateInfo.FadeSecond;
		agent.Animation.InitializeStates(playState);
		agent.Animation.PlayInAnimation(_inEnableFade, _inFadeSecond, playState.MainStateInfo.FadeOutTime, _layer);
	}

	public override TaskStatus OnUpdate()
	{
		if (base.Agent.Animation.PlayingInAnimation)
		{
			return TaskStatus.Running;
		}
		OnComplete(base.Agent);
		return TaskStatus.Success;
	}

	private void OnComplete(AgentActor agent)
	{
		ItemIDKeyPair gauzeID = Singleton<Resources>.Instance.CommonDefine.ItemIDDefine.GauzeID;
		StuffItem stuffItem = agent.AgentData.ItemList.Find((StuffItem x) => x.CategoryID == gauzeID.categoryID && x.ID == gauzeID.itemID);
		StuffItem item = new StuffItem(stuffItem.CategoryID, stuffItem.ID, 1);
		agent.AgentData.ItemList.RemoveItem(item);
	}

	public override void OnEnd()
	{
		base.Agent.ChangeDynamicNavMeshAgentAvoidance();
	}
}
