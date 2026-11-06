using System;
using System.Collections;
using System.Collections.Generic;
using AIProject.Definitions;
using AIProject.SaveData;
using IllusionUtility.GetUtility;
using Manager;
using UniRx;
using UnityEngine;

namespace AIProject.Player;

public class Search : PlayerStateBase
{
	private Subject<Unit> _onEndAction = new Subject<Unit>();

	protected override void OnAwake(PlayerActor player)
	{
		player.EventKey = EventType.Search;
		player.SetActiveOnEquipedItem(active: false);
		player.ChaControl.setAllLayerWeight(0f);
		AIProject.Definitions.Action.NameTable.TryGetValue(EventType.Search, out var value);
		int item = value.Item1;
		ActionPointInfo outInfo;
		if (player.CurrentPoint is TutorialSearchActionPoint)
		{
			outInfo = (player.CurrentPoint as TutorialSearchActionPoint).GetPlayerActionPointInfo();
		}
		else
		{
			player.CurrentPoint.TryGetPlayerActionPointInfo(EventType.Search, out outInfo);
		}
		int poseID = outInfo.poseID;
		player.PoseID = poseID;
		int num = poseID;
		Transform t = player.CurrentPoint.transform.FindLoop(outInfo.baseNullName)?.transform ?? player.CurrentPoint.transform;
		GameObject gameObject = player.CurrentPoint.transform.FindLoop(outInfo.recoveryNullName);
		player.Animation.RecoveryPoint = gameObject?.transform;
		PlayState playState = Singleton<Manager.Resources>.Instance.Animation.PlayerActionAnimTable[player.ChaControl.sex][item][num];
		player.Animation.LoadEventKeyTable(item, outInfo.poseID);
		player.LoadEventItems(playState);
		player.LoadEventParticles(item, outInfo.poseID);
		player.Animation.InitializeStates(playState.MainStateInfo.InStateInfo.StateInfos, playState.MainStateInfo.OutStateInfo.StateInfos, playState.MainStateInfo.AssetBundleInfo);
		ActorAnimInfo actorAnimInfo = new ActorAnimInfo
		{
			layer = playState.Layer,
			inEnableBlend = playState.MainStateInfo.InStateInfo.EnableFade,
			inBlendSec = playState.MainStateInfo.InStateInfo.FadeSecond,
			inFadeOutTime = playState.MainStateInfo.FadeOutTime,
			outEnableBlend = playState.MainStateInfo.OutStateInfo.EnableFade,
			outBlendSec = playState.MainStateInfo.OutStateInfo.FadeSecond,
			directionType = playState.DirectionType,
			endEnableBlend = playState.EndEnableBlend,
			endBlendSec = playState.EndBlendRate
		};
		player.Animation.AnimInfo = actorAnimInfo;
		ActorAnimInfo animInfo = actorAnimInfo;
		player.DeactivateNavMeshAgent();
		player.IsKinematic = true;
		player.Animation.PlayInAnimation(animInfo.inEnableBlend, animInfo.inBlendSec, animInfo.inFadeOutTime, animInfo.layer);
		_onEndAction.Take(1).Subscribe(delegate
		{
			player.Animation.PlayOutAnimation(animInfo.outEnableBlend, animInfo.outBlendSec, animInfo.layer);
		});
		player.SetStand(t, animInfo.inEnableBlend, animInfo.inBlendSec, animInfo.directionType);
		player.CameraControl.Mode = CameraMode.ActionFreeLook;
		player.CameraControl.SetShotTypeForce(ShotType.Near);
		player.CameraControl.LoadActionCameraFile(item, num);
	}

	private void Elapsed(PlayerActor player)
	{
		ActionPoint currentPoint = player.CurrentPoint;
		Type type = currentPoint.GetType();
		if (Singleton<Game>.IsInstance() && type == typeof(SearchActionPoint))
		{
			Dictionary<int, AIProject.SaveData.Environment.SearchActionInfo> searchActionLockTable = Singleton<Game>.Instance.Environment.SearchActionLockTable;
			if (!searchActionLockTable.TryGetValue(currentPoint.RegisterID, out var value))
			{
				value = new AIProject.SaveData.Environment.SearchActionInfo();
			}
			value.Count++;
			searchActionLockTable[currentPoint.RegisterID] = value;
		}
		Actor.SearchInfo searchInfo = new Actor.SearchInfo();
		searchInfo.IsSuccess = false;
		Actor.SearchInfo searchInfo2 = searchInfo;
		if (currentPoint is SearchActionPoint)
		{
			SearchActionPoint searchActionPoint = currentPoint as SearchActionPoint;
			Manager.Resources instance = Singleton<Manager.Resources>.Instance;
			int pointID = (searchActionPoint.IDList.IsNullOrEmpty() ? searchActionPoint.ID : searchActionPoint.IDList.GetElement(0));
			Dictionary<int, ItemTableElement> itemTableInArea = instance.GameInfo.GetItemTableInArea(pointID);
			searchInfo2 = player.RandomAddItem(itemTableInArea, containsNone: true);
		}
		else if (currentPoint is TutorialSearchActionPoint)
		{
			TutorialSearchActionPoint tutorialSearchActionPoint = currentPoint as TutorialSearchActionPoint;
			searchInfo2 = tutorialSearchActionPoint.GetSearchInfo();
		}
		if (type == typeof(OnceSearchActionPoint))
		{
			OnceSearchActionPoint onceSearchActionPoint = currentPoint as OnceSearchActionPoint;
			if (onceSearchActionPoint.HaveMapItems)
			{
				Manager.Map.FadeStart();
			}
			onceSearchActionPoint.SetAvailable(active: false);
		}
		else if (type == typeof(DropSearchActionPoint))
		{
			DropSearchActionPoint dropSearchActionPoint = currentPoint as DropSearchActionPoint;
			if (dropSearchActionPoint.HaveMapItems)
			{
				Manager.Map.FadeStart();
			}
			dropSearchActionPoint.SetCoolTime();
		}
		if (searchInfo2.IsSuccess)
		{
			foreach (Actor.ItemSearchInfo item2 in searchInfo2.ItemList)
			{
				StuffItem stuffItem = new StuffItem(item2.categoryID, item2.id, item2.count);
				if (player.PlayerData.ItemList.Count < player.PlayerData.InventorySlotMax)
				{
					player.PlayerData.ItemList.AddItem(stuffItem, stuffItem.Count, player.PlayerData.InventorySlotMax);
				}
				StuffItemInfo item = Singleton<Manager.Resources>.Instance.GameInfo.GetItem(item2.categoryID, item2.id);
				MapUIContainer.AddSystemItemLog(item, item2.count, isNotify: true);
			}
			player.Controller.ChangeState("Normal");
		}
		else
		{
			MapUIContainer.AddNotify(MapUIContainer.ItemGetEmptyText);
			player.Controller.ChangeState("Normal");
		}
	}

	protected override void OnRelease(PlayerActor player)
	{
		player.ClearItems();
		player.ClearParticles();
		ActorAnimInfo animInfo = player.Animation.AnimInfo;
		player.SetStand(player.Animation.RecoveryPoint, animInfo.endEnableBlend, animInfo.endBlendSec, animInfo.directionType);
		player.Animation.RefsActAnimInfo = true;
	}

	protected override void OnUpdate(PlayerActor actor, ref Actor.InputInfo info)
	{
		actor.NavMeshAgent.velocity = (info.move = Vector3.zero);
		if (!actor.Animation.PlayingInAnimation)
		{
			if (_onEndAction != null)
			{
				_onEndAction.OnNext(Unit.Default);
			}
			if (!actor.Animation.PlayingOutAnimation)
			{
				Elapsed(actor);
			}
		}
	}

	protected override void OnAfterUpdate(PlayerActor actor, Actor.InputInfo info)
	{
		actor.CharacterTPS.UpdateState(info, ActorLocomotion.UpdateType.Update);
	}

	protected override void OnAnimatorStateEnterInternal(PlayerController control, AnimatorStateInfo stateInfo)
	{
	}

	protected override void OnAnimatorStateExitInternal(PlayerController control, AnimatorStateInfo stateInfo)
	{
	}

	public override IEnumerator End(Actor actor)
	{
		yield break;
	}
}
