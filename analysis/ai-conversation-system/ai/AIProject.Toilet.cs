using AIProject.Definitions;
using BehaviorDesigner.Runtime.Tasks;
using Manager;

namespace AIProject;

[TaskCategory("")]
public class Toilet : AgentStateAction
{
	public override void OnStart()
	{
		AgentActor agent = base.Agent;
		agent.EventKey = EventType.Toilet;
		base.OnStart();
		AgentProfile.PoseIDCollection poseIDTable = Singleton<Resources>.Instance.AgentProfile.PoseIDTable;
		switch (agent.HPositionSubID)
		{
		case 3:
			agent.SurprisePoseID = poseIDTable.SurpriseInToiletSitID;
			break;
		case 5:
			agent.SurprisePoseID = poseIDTable.SurpriseInToiletSquatID;
			break;
		}
	}

	protected override void OnCompletedStateTask()
	{
		AgentActor agent = base.Agent;
		int desireKey = Desire.GetDesireKey(Desire.Type.Toilet);
		agent.SetDesire(desireKey, 0f);
		agent.SurprisePoseID = null;
	}
}
