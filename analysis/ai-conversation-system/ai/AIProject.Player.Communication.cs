using Manager;
using UnityEngine;

namespace AIProject.Player;

public class Communication : PlayerStateBase
{
	protected override void OnAwake(PlayerActor player)
	{
		player.SetActiveOnEquipedItem(active: false);
		player.ChaControl.setAllLayerWeight(0f);
		player.ChaControl.visibleAll = false;
		MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.None);
		player.SetScheduledInteractionState(isEnabled: false);
		player.ReleaseInteraction();
		ActorAnimInfo animInfo = player.Animation.AnimInfo;
		animInfo.outBlendSec = 0f;
		animInfo.outEnableBlend = true;
		player.Animation.AnimInfo = animInfo;
		if (player.CommCompanion != null)
		{
			player.CommCompanion.Animation.BeginIgnoreExpression();
			player.CommCompanion.Animation.BeginIgnoreVoice();
		}
	}

	protected override void OnRelease(PlayerActor player)
	{
		player.SetScheduledInteractionState(isEnabled: true);
		player.ReleaseInteraction();
		Singleton<Manager.Input>.Instance.ReserveState(Manager.Input.ValidType.Action);
		Singleton<Manager.Input>.Instance.SetupState();
		if (player.CommCompanion != null)
		{
			player.CommCompanion.Animation.EndIgnoreExpression();
			player.CommCompanion.Animation.EndIgnoreVoice();
		}
	}

	protected override void OnUpdate(PlayerActor player, ref Actor.InputInfo info)
	{
		player.NavMeshAgent.velocity = (info.move = Vector3.zero);
	}

	protected override void OnAfterUpdate(PlayerActor player, Actor.InputInfo info)
	{
		player.CharacterTPS.UpdateState(info, ActorLocomotion.UpdateType.Update);
	}
}
