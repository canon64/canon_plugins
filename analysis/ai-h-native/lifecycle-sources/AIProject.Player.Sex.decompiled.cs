using System.Collections;
using UnityEngine;

namespace AIProject.Player;

public class Sex : PlayerStateBase
{
	protected override void OnAwake(PlayerActor player)
	{
		player.ChaControl.visibleAll = false;
		MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.None);
		player.SetScheduledInteractionState(isEnabled: false);
		player.ReleaseInteraction();
		if (player.CommCompanion != null)
		{
			player.CommCompanion.Animation.BeginIgnoreExpression();
		}
	}

	public override IEnumerator End(Actor actor)
	{
		yield break;
	}

	protected override void OnAfterUpdate(PlayerActor actor, Actor.InputInfo info)
	{
		actor.CharacterTPS.UpdateState(info, ActorLocomotion.UpdateType.Update);
	}

	protected override void OnUpdate(PlayerActor actor, ref Actor.InputInfo info)
	{
		actor.NavMeshAgent.velocity = (info.move = Vector3.zero);
	}
}
