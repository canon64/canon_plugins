using System.Collections;
using Manager;
using UnityEngine;

namespace AIProject.Player;

public class Harvest : PlayerStateBase
{
	protected override void OnAwake(PlayerActor player)
	{
		Singleton<Manager.Input>.Instance.ReserveState(Manager.Input.ValidType.UI);
		Singleton<Manager.Input>.Instance.SetupState();
	}

	protected override void OnRelease(PlayerActor player)
	{
	}

	protected override void OnUpdate(PlayerActor player, ref Actor.InputInfo info)
	{
		player.NavMeshAgent.velocity = (info.move = Vector3.zero);
	}

	protected override void OnAfterUpdate(PlayerActor player, Actor.InputInfo info)
	{
		player.CharacterTPS.UpdateState(info, ActorLocomotion.UpdateType.Update);
	}

	public override IEnumerator End(Actor actor)
	{
		yield break;
	}
}
