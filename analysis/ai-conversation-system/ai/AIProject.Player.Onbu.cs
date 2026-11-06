using System;
using System.Collections;
using System.Collections.Generic;
using AIProject.Definitions;
using AIProject.SaveData;
using Manager;
using UniRx;
using UnityEngine;
using UnityEngine.AI;

namespace AIProject.Player;

public class Onbu : PlayerStateBase
{
	private Subject<Unit> _onEndInAnimation = new Subject<Unit>();

	private Subject<bool> _activeSubjectCommand = new Subject<bool>();

	private IDisposable _onEndInAnimationDisposable;

	private IDisposable _activeSubjectCommandDisposable;

	private CommandLabel.CommandInfo _cancelCommandInfo;

	private CommandLabel.CommandInfo _uncancelableCommandInfo;

	private static readonly RaycastHit[] _raycastHits = new RaycastHit[100];

	private LayerMask _layer = 0;

	private string _tag = string.Empty;

	protected override void OnAwake(PlayerActor player)
	{
		Manager.Resources instance = Singleton<Manager.Resources>.Instance;
		Singleton<Manager.Resources>.Instance.itemIconTables.ActionIconTable.TryGetValue(instance.PlayerProfile.CommonActionIconID, out var value);
		_cancelCommandInfo = new CommandLabel.CommandInfo
		{
			Text = "下ろす",
			Icon = value,
			TargetSpriteInfo = null,
			Transform = null,
			Event = delegate
			{
				ExitState();
			}
		};
		_uncancelableCommandInfo = new CommandLabel.CommandInfo
		{
			Text = null,
			Icon = null,
			TargetSpriteInfo = null,
			Transform = null,
			Condition = null,
			Event = delegate
			{
				OutputWarningMessage();
			}
		};
		player.Mode = Desire.ActionType.Onbu;
		player.CameraControl.CrossFade.FadeStart();
		AgentActor agentPartner = player.AgentPartner;
		agentPartner.Partner = player;
		agentPartner.IsSlave = true;
		agentPartner.Animation.StopAllAnimCoroutine();
		agentPartner.BehaviorResources.ChangeMode(Desire.ActionType.Onbu);
		agentPartner.NavMeshAgent.enabled = false;
		player.PlayerController.CommandArea.RefreshCommands();
		if (player.PlayerController.PrevStateName == "Normal" || player.PlayerController.PrevStateName == "Houchi")
		{
			ActivateTransfer(player, agentPartner);
		}
		else
		{
			ActivateTransferImmediate(player, agentPartner);
		}
		_onEndInAnimationDisposable = _onEndInAnimation.Take(1).Subscribe(delegate
		{
			_activeSubjectCommandDisposable = _activeSubjectCommand.DistinctUntilChanged().Subscribe(delegate(bool isOn)
			{
				if (isOn)
				{
					if (MapUIContainer.CommandLabel.SubjectCommand != _cancelCommandInfo)
					{
						MapUIContainer.CommandLabel.SubjectCommand = _cancelCommandInfo;
						player.PlayerController.CommandArea.RefreshCommands();
					}
				}
				else if (MapUIContainer.CommandLabel.SubjectCommand != _uncancelableCommandInfo)
				{
					MapUIContainer.CommandLabel.SubjectCommand = _uncancelableCommandInfo;
					player.PlayerController.CommandArea.RefreshCommands();
				}
			});
			MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.InvokeAcception);
		});
		MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.None);
		_layer = Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.HLayer;
		_tag = Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.OnbuMeshTag;
		if (Singleton<Manager.Map>.IsInstance())
		{
			Singleton<Manager.Map>.Instance.CheckTutorialState(player);
		}
	}

	private void ActivateTransfer(PlayerActor player, Actor partner)
	{
		EquipEventItemInfo itemInfo = null;
		LoadLocomotionAnimation(player, out var info, ref itemInfo);
		player.ResetEquipEventItem(itemInfo);
		partner.Position = player.Position;
		partner.Rotation = player.Rotation;
		int onbuStateID = Singleton<Manager.Resources>.Instance.DefinePack.AnimatorState.OnbuStateID;
		PlayState playState = Singleton<Manager.Resources>.Instance.Animation.PlayerLocomotionStateTable[player.ChaControl.sex][onbuStateID];
		ActorAnimation animation = player.Animation;
		ActorAnimation animation2 = partner.Animation;
		AnimatorStateInfo currentAnimatorStateInfo = animation.Animator.GetCurrentAnimatorStateInfo(0);
		if (info != null)
		{
			animation.InitializeStates(info);
			animation2.InitializeStates(playState);
			bool flag = false;
			PlayState.Info[] stateInfos = info.MainStateInfo.InStateInfo.StateInfos;
			foreach (PlayState.Info info2 in stateInfos)
			{
				if (currentAnimatorStateInfo.shortNameHash == info2.ShortNameStateHash)
				{
					flag = true;
					break;
				}
			}
			if (flag)
			{
				if (info.MaskStateInfo.layer > 0)
				{
					float layerWeight = animation.Animator.GetLayerWeight(info.MaskStateInfo.layer);
					if (layerWeight == 0f)
					{
						flag = false;
					}
				}
				else
				{
					for (int j = 1; j < animation.Animator.layerCount; j++)
					{
						float layerWeight2 = animation.Animator.GetLayerWeight(j);
						if (layerWeight2 > 0f)
						{
							flag = false;
							break;
						}
					}
				}
			}
			if (flag)
			{
				animation.InStates.Clear();
				animation.OutStates.Clear();
				animation.ActionStates.Clear();
				animation2.InStates.Clear();
				animation2.OutStates.Clear();
				animation2.ActionStates.Clear();
			}
			else
			{
				int layer = info.Layer;
				if (animation.RefsActAnimInfo)
				{
					animation.StopAllAnimCoroutine();
					animation.PlayInLocoAnimation(animation.AnimInfo.outEnableBlend, animation.AnimInfo.outBlendSec, layer);
					animation2.StopAllAnimCoroutine();
					animation2.PlayInLocoAnimation(animation.AnimInfo.outEnableBlend, animation.AnimInfo.outBlendSec, layer);
					animation.RefsActAnimInfo = false;
				}
				else
				{
					bool enableFade = info.MainStateInfo.InStateInfo.EnableFade;
					float fadeSecond = info.MainStateInfo.InStateInfo.FadeSecond;
					animation.StopAllAnimCoroutine();
					animation.PlayInLocoAnimation(enableFade, fadeSecond, layer);
					animation2.StopAllAnimCoroutine();
					animation2.PlayInLocoAnimation(enableFade, fadeSecond, layer);
				}
			}
		}
		else
		{
			for (int k = 1; k < animation.Animator.layerCount; k++)
			{
				animation.Animator.SetLayerWeight(k, 0f);
			}
			animation.InitializeStates(playState);
			animation2.InitializeStates(playState);
			int layer2 = playState.Layer;
			if (animation.RefsActAnimInfo)
			{
				animation.StopAllAnimCoroutine();
				animation.PlayInLocoAnimation(animation.AnimInfo.endEnableBlend, animation.AnimInfo.endBlendSec, layer2);
				animation2.StopAllAnimCoroutine();
				animation2.PlayInLocoAnimation(animation.AnimInfo.inEnableBlend, animation.AnimInfo.inBlendSec, layer2);
				animation.RefsActAnimInfo = false;
			}
			else
			{
				ActorAnimInfo actorAnimInfo = default(ActorAnimInfo);
				actorAnimInfo.layer = playState.Layer;
				actorAnimInfo.inEnableBlend = playState.MainStateInfo.InStateInfo.EnableFade;
				actorAnimInfo.inBlendSec = playState.MainStateInfo.InStateInfo.FadeSecond;
				ActorAnimInfo actorAnimInfo2 = actorAnimInfo;
				player.Animation.AnimInfo = actorAnimInfo2;
				ActorAnimInfo actorAnimInfo3 = actorAnimInfo2;
				bool inEnableBlend = actorAnimInfo3.inEnableBlend;
				float inBlendSec = actorAnimInfo3.inBlendSec;
				animation.StopAllAnimCoroutine();
				animation.PlayInLocoAnimation(inEnableBlend, inBlendSec, layer2);
				animation2.StopAllAnimCoroutine();
				animation2.PlayInLocoAnimation(inEnableBlend, inBlendSec, layer2);
			}
		}
		if (player.NavMeshAgent.isStopped)
		{
			player.NavMeshAgent.isStopped = false;
		}
		if (player.IsKinematic)
		{
			player.IsKinematic = false;
		}
	}

	private void ActivateTransferImmediate(PlayerActor player, Actor partner)
	{
		EquipEventItemInfo itemInfo = null;
		LoadLocomotionAnimation(player, out var info, ref itemInfo);
		player.ResetEquipEventItem(itemInfo);
		partner.Position = player.Position;
		partner.Rotation = player.Rotation;
		int onbuStateID = Singleton<Manager.Resources>.Instance.DefinePack.AnimatorState.OnbuStateID;
		PlayState playState = Singleton<Manager.Resources>.Instance.Animation.PlayerLocomotionStateTable[player.ChaControl.sex][onbuStateID];
		ActorAnimation animation = player.Animation;
		ActorAnimation animation2 = partner.Animation;
		animation.InStates.Clear();
		animation.OutStates.Clear();
		animation.ActionStates.Clear();
		partner.Animation.InStates.Clear();
		partner.Animation.OutStates.Clear();
		partner.Animation.ActionStates.Clear();
		PlayState.Info element = playState.MainStateInfo.InStateInfo.StateInfos.GetElement(playState.MainStateInfo.InStateInfo.StateInfos.Length - 1);
		if (info != null)
		{
			PlayState.Info element2 = info.MainStateInfo.InStateInfo.StateInfos.GetElement(info.MainStateInfo.InStateInfo.StateInfos.Length - 1);
			player.Animation.InStates.Enqueue(element2);
			if (!info.MainStateInfo.OutStateInfo.StateInfos.IsNullOrEmpty())
			{
				PlayState.Info[] stateInfos = info.MainStateInfo.OutStateInfo.StateInfos;
				foreach (PlayState.Info item in stateInfos)
				{
					player.Animation.OutStates.Enqueue(item);
				}
			}
			partner.Animation.InStates.Enqueue(element);
			if (!playState.MainStateInfo.OutStateInfo.StateInfos.IsNullOrEmpty())
			{
				PlayState.Info[] stateInfos2 = playState.MainStateInfo.OutStateInfo.StateInfos;
				foreach (PlayState.Info item2 in stateInfos2)
				{
					partner.Animation.OutStates.Enqueue(item2);
				}
			}
			ActorAnimInfo animInfo = player.Animation.AnimInfo;
			int layer = info.Layer;
			player.Animation.StopAllAnimCoroutine();
			player.Animation.PlayInLocoAnimation(animInfo.inEnableBlend, animInfo.inBlendSec, layer);
			partner.Animation.StopAllAnimCoroutine();
			partner.Animation.PlayInLocoAnimation(animInfo.inEnableBlend, animInfo.inBlendSec, layer);
		}
		else
		{
			for (int k = 1; k < animation.Animator.layerCount; k++)
			{
				animation.Animator.SetLayerWeight(k, 0f);
			}
			player.Animation.InStates.Enqueue(element);
			if (!playState.MainStateInfo.OutStateInfo.StateInfos.IsNullOrEmpty())
			{
				PlayState.Info[] stateInfos3 = playState.MainStateInfo.OutStateInfo.StateInfos;
				foreach (PlayState.Info item3 in stateInfos3)
				{
					player.Animation.OutStates.Enqueue(item3);
				}
			}
			partner.Animation.InStates.Enqueue(element);
			if (!playState.MainStateInfo.OutStateInfo.StateInfos.IsNullOrEmpty())
			{
				PlayState.Info[] stateInfos4 = playState.MainStateInfo.OutStateInfo.StateInfos;
				foreach (PlayState.Info item4 in stateInfos4)
				{
					partner.Animation.OutStates.Enqueue(item4);
				}
			}
			ActorAnimInfo animInfo2 = player.Animation.AnimInfo;
			int layer2 = playState.Layer;
			player.Animation.StopAllAnimCoroutine();
			player.Animation.PlayInLocoAnimation(animInfo2.inEnableBlend, animInfo2.inBlendSec, layer2);
			partner.Animation.StopAllAnimCoroutine();
			partner.Animation.PlayInLocoAnimation(animInfo2.inEnableBlend, animInfo2.inBlendSec, layer2);
		}
		if (player.NavMeshAgent.isStopped)
		{
			player.NavMeshAgent.isStopped = false;
		}
		if (player.IsKinematic)
		{
			player.IsKinematic = false;
		}
	}

	private void LoadLocomotionAnimation(PlayerActor player, out PlayState info, ref EquipEventItemInfo itemInfo)
	{
		Manager.Resources instance = Singleton<Manager.Resources>.Instance;
		LocomotionProfile locomotionProfile = instance.LocomotionProfile;
		PlayerProfile playerProfile = instance.PlayerProfile;
		StuffItem equipedLampItem = player.PlayerData.EquipedLampItem;
		CommonDefine.ItemIDDefines itemIDDefine = instance.CommonDefine.ItemIDDefine;
		if (equipedLampItem != null)
		{
			ItemIDKeyPair torchID = itemIDDefine.TorchID;
			ItemIDKeyPair flashlightID = itemIDDefine.FlashlightID;
			ItemIDKeyPair maleLampID = itemIDDefine.MaleLampID;
			if (equipedLampItem.CategoryID == torchID.categoryID && equipedLampItem.ID == torchID.itemID)
			{
				info = instance.Animation.PlayerLocomotionStateTable[player.ChaControl.sex][playerProfile.PoseIDData.TorchOnbuLocoID];
				itemInfo = instance.GameInfo.CommonEquipEventItemTable[torchID.categoryID][torchID.itemID];
				itemInfo.ParentName = instance.LocomotionProfile.PlayerLocoItemParentName;
				return;
			}
			if (equipedLampItem.CategoryID == maleLampID.categoryID && equipedLampItem.ID == maleLampID.itemID)
			{
				info = instance.Animation.PlayerLocomotionStateTable[player.ChaControl.sex][playerProfile.PoseIDData.LampOnbuLocoID];
				itemInfo = instance.GameInfo.CommonEquipEventItemTable[maleLampID.categoryID][maleLampID.itemID];
				itemInfo.ParentName = instance.LocomotionProfile.PlayerLocoItemParentName;
				return;
			}
			if (equipedLampItem.CategoryID == flashlightID.categoryID && equipedLampItem.ID == flashlightID.itemID)
			{
				info = instance.Animation.PlayerLocomotionStateTable[player.ChaControl.sex][playerProfile.PoseIDData.TorchOnbuLocoID];
				itemInfo = instance.GameInfo.CommonEquipEventItemTable[flashlightID.categoryID][flashlightID.itemID];
				itemInfo.ParentName = instance.LocomotionProfile.PlayerLocoItemParentName;
				return;
			}
		}
		info = null;
	}

	private void ExitState()
	{
		MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.None);
		Observable.FromCoroutine((Func<IEnumerator>)delegate
		{
			PlayerActor player2 = Singleton<Manager.Map>.Instance.Player;
			return player2.Controller.State.End(player2);
		}, publishEveryYield: false).Take(1).Subscribe(delegate
		{
			PlayerActor player = Singleton<Manager.Map>.Instance.Player;
			player.CameraControl.CrossFade.FadeStart();
			player.PlayerController.ChangeState("Normal");
			player.CameraControl.Mode = CameraMode.Normal;
			MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.InvokeAcception);
			AgentActor agentPartner = player.AgentPartner;
			agentPartner.ChangeBehavior(agentPartner.PrevMode);
			agentPartner.Partner = null;
			agentPartner.IsSlave = false;
			agentPartner.NavMeshAgent.enabled = true;
			Vector3 point = player.Position + player.Back * 10f;
			LayerMask hLayer = Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.HLayer;
			string onbuMeshTag = Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.OnbuMeshTag;
			GameObject[] array = GameObject.FindGameObjectsWithTag(onbuMeshTag);
			List<MeshCollider> list = ListPool<MeshCollider>.Get();
			GameObject[] array2 = array;
			foreach (GameObject gameObject in array2)
			{
				MeshCollider[] componentsInChildren = gameObject.GetComponentsInChildren<MeshCollider>();
				list.AddRange(componentsInChildren);
			}
			float num = float.PositiveInfinity;
			Vector3 vector = Vector3.zero;
			foreach (MeshCollider item in list)
			{
				Vector3 vector2 = item.NearestVertexTo(point);
				float num2 = Vector3.Distance(player.Position, vector2);
				if (num2 < num)
				{
					num = num2;
					vector = vector2;
				}
			}
			ListPool<MeshCollider>.Release(list);
			if (NavMesh.SamplePosition(vector, out var hit, 100f, -1))
			{
				vector = hit.position;
			}
			player.Partner.NavMeshAgent.Warp(vector);
			Vector3 vector3 = vector + player.Partner.Forward * 10f;
			if (NavMesh.SamplePosition(vector3, out hit, 100f, -1))
			{
				vector3 = hit.position;
			}
			player.NavMeshAgent.Warp(vector3);
			player.Partner = null;
			agentPartner.IsVisible = true;
		});
	}

	private void OutputWarningMessage()
	{
		MapUIContainer.PushMessageUI("ここに女の子を下ろすことはできません", 2, 0);
	}

	protected override void OnAfterUpdate(PlayerActor actor, Actor.InputInfo info)
	{
		actor.CharacterTPS.UpdateState(info, ActorLocomotion.UpdateType.Update);
	}

	protected override void OnUpdate(PlayerActor actor, ref Actor.InputInfo info)
	{
		Singleton<Manager.Map>.Instance.CheckStoryProgress();
		if (actor.Animation.PlayingInLocoAnimation)
		{
			return;
		}
		_onEndInAnimation.OnNext(Unit.Default);
		Manager.Input instance = Singleton<Manager.Input>.Instance;
		if (instance.State == Manager.Input.ValidType.Action)
		{
			Transform transform = actor.CameraControl.transform;
			Vector2 moveAxis = instance.MoveAxis;
			Quaternion quaternion = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);
			Vector3 move = quaternion * Vector3.ClampMagnitude(new Vector3(moveAxis.x, 0f, moveAxis.y), 1f);
			info.move = move;
			if (UnityEngine.Input.GetKeyDown(KeyCode.Mouse2) || UnityEngine.Input.GetKeyDown(KeyCode.F1))
			{
				actor.PlayerController.ChangeState("Menu");
			}
			else
			{
				if (!UnityEngine.Input.GetKeyDown(KeyCode.M))
				{
					return;
				}
				if (Singleton<MapUIContainer>.Instance.MinimapUI.VisibleMode == 0)
				{
					actor.Controller.ChangeState("WMap");
				}
				else if (Singleton<MapUIContainer>.Instance.MinimapUI.VisibleMode == 2)
				{
					if (Config.GameData.MiniMap)
					{
						Singleton<MapUIContainer>.Instance.MinimapUI.OpenMiniMap();
					}
					else
					{
						actor.Controller.ChangeState("WMap");
					}
				}
			}
		}
		else
		{
			info.move = Vector3.zero;
			Transform transform2 = actor.CameraControl.transform;
			info.lookPos = actor.transform.position + transform2.forward * 100f;
		}
	}

	protected override void OnFixedUpdate(PlayerActor player, Actor.InputInfo info)
	{
		if (player.Animation.PlayingInLocoAnimation)
		{
			return;
		}
		_layer = Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.HLayer;
		_tag = Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.OnbuMeshTag;
		Vector3 position = player.Position;
		position.y += 15f;
		int num = Physics.SphereCastNonAlloc(position, 7.5f, Vector3.down, _raycastHits, 25f, _layer);
		bool value = false;
		for (int i = 0; i < num; i++)
		{
			if (_raycastHits[i].collider.tag == _tag)
			{
				value = true;
				break;
			}
		}
		_activeSubjectCommand.OnNext(value);
		for (int j = 0; j < num; j++)
		{
			_raycastHits[j] = default(RaycastHit);
		}
	}

	protected override void OnRelease(PlayerActor player)
	{
		MapUIContainer.CommandLabel.SubjectCommand = null;
		MapUIContainer.CommandLabel.RefreshCommands();
		if (_onEndInAnimationDisposable != null)
		{
			_onEndInAnimationDisposable.Dispose();
		}
		if (_activeSubjectCommandDisposable != null)
		{
			_activeSubjectCommandDisposable.Dispose();
		}
		player.Mode = Desire.ActionType.Normal;
	}
}
