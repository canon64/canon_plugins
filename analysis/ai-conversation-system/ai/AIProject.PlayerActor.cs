using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ADV;
using AIChara;
using AIProject.Animal;
using AIProject.Definitions;
using AIProject.Player;
using AIProject.RootMotion;
using AIProject.SaveData;
using AIProject.Scene;
using AIProject.UI;
using Cinemachine;
using IllusionUtility.GetUtility;
using Manager;
using RootMotion.FinalIK;
using UniRx;
using UnityEngine;
using UnityEx;
using UnityEx.Misc;

namespace AIProject;

public class PlayerActor : Actor
{
	private float _coolTime;

	private bool _called;

	private float _callElapsedTime;

	private const float _callCoolTime = 2f;

	private IEnumerator _enumerator;

	private IDisposable _disposable;

	private static readonly DateTime _midnightTime = new DateTime(1, 1, 1, 23, 59, 59);

	private TimeSpan _elapsedTimeInSleep = default(TimeSpan);

	private IEnumerator _sleepEventEnumerator;

	private IDisposable _sleepEventDisposable;

	private IEnumerator _eatEventEnumerator;

	private IDisposable _eatEventDisposable;

	private IEnumerator _exitEatEventEnumerator;

	private IDisposable _exitEatEventDisposable;

	private IEnumerator _hizamakuraEventEnumerator;

	private IDisposable _hizamakuraEventDisposable;

	[SerializeField]
	protected ActorAnimationPlayer _animation;

	[SerializeField]
	private ActorCameraControl _cameraCtrl;

	[SerializeField]
	private ActorCameraControl.LocomotionSettingData _locomotionSetting;

	private Vector3 _defaultOfs = Vector3.zero;

	[SerializeField]
	private Vector3 _normalOffset = Vector3.zero;

	[SerializeField]
	private Vector3 _behaviourOffset = Vector3.zero;

	[SerializeField]
	private AQUAS_LensEffects _lensEffect;

	[SerializeField]
	private GameObject _cameraTarget;

	[SerializeField]
	private ActorLocomotionThirdPerson _character;

	[SerializeField]
	private PlayerController _controller;

	private bool? _scheduledInteractionState = false;

	public CommCommandList.CommandInfo[] SleepCommandInfos { get; private set; }

	public CommCommandList.CommandInfo[] CookCommandInfos { get; private set; }

	public CommCommandList.CommandInfo[] ForgeCommandInfos { get; private set; }

	public CommCommandList.CommandInfo[] MixingCommandInfos { get; private set; }

	public CommCommandList.CommandInfo[] BaseCommandInfos { get; private set; }

	public CommCommandList.CommandInfo[] DeviceCommandInfos { get; private set; }

	public CommCommandList.CommandInfo[] ShipCommandInfos { get; private set; }

	public CommCommandList.CommandInfo[] ChickenCoopCommandInfos { get; private set; }

	public CommCommandList.CommandInfo[] CoSleepCommandInfos { get; private set; }

	public CommCommandList.CommandInfo[] DateEatCommandInfos { get; private set; }

	public CommCommandList.CommandInfo[] SpecialHCommandInfo { get; private set; }

	public CommCommandList.CommandInfo[] ExitEatEventCommandInfo { get; private set; }

	public CommCommandList.CommandInfo[] WarpCommandInfos { get; private set; }

	public override bool IsNeutralCommand
	{
		get
		{
			if (Singleton<Game>.IsInstance() && Singleton<Game>.Instance.MapShortcutUI != null)
			{
				return false;
			}
			if (Singleton<Manager.Map>.Instance.IsWarpProc)
			{
				return false;
			}
			bool flag = Mode != Desire.ActionType.Onbu;
			bool flag2 = _controller.State is Normal;
			bool flag3 = _controller.State is Houchi;
			bool flag4 = _controller.State is WMap;
			return flag && (flag2 || flag3 || flag4);
		}
	}

	public int HPoseID { get; set; }

	public bool ProcessingTimeSkip => _enumerator != null;

	public override string CharaName
	{
		[CompilerGenerated]
		get
		{
			return base.ChaControl.fileParam.fullname;
		}
	}

	public override ICharacterInfo TiedInfo
	{
		[CompilerGenerated]
		get
		{
			return PlayerData;
		}
	}

	public PlayerData PlayerData { get; set; }

	public override ActorAnimation Animation
	{
		[CompilerGenerated]
		get
		{
			return _animation;
		}
	}

	public ActorCameraControl CameraControl
	{
		[CompilerGenerated]
		get
		{
			return _cameraCtrl;
		}
	}

	public CameraConfig CameraConfig
	{
		[CompilerGenerated]
		get
		{
			return _cameraCtrl.CameraConfig;
		}
	}

	public Vector3 NormalOffset
	{
		[CompilerGenerated]
		get
		{
			return _normalOffset;
		}
	}

	public Vector3 BehaviourOffset
	{
		[CompilerGenerated]
		get
		{
			return _behaviourOffset;
		}
	}

	public GameObject CameraTarget
	{
		[CompilerGenerated]
		get
		{
			return _cameraTarget;
		}
	}

	public override ActorLocomotion Locomotor
	{
		[CompilerGenerated]
		get
		{
			return _character;
		}
	}

	public ActorLocomotionThirdPerson CharacterTPS
	{
		[CompilerGenerated]
		get
		{
			return _character;
		}
	}

	public override ActorController Controller
	{
		[CompilerGenerated]
		get
		{
			return _controller;
		}
	}

	public PlayerController PlayerController
	{
		[CompilerGenerated]
		get
		{
			return _controller;
		}
	}

	public HandsHolder HandsHolder { get; protected set; }

	public bool OldEnabledHoldingHand { get; set; }

	public DevicePoint CurrentDevicePoint { get; set; }

	public EventPoint CurrentEventPoint { get; set; }

	public FarmPoint CurrentFarmPoint { get; set; }

	public PetHomePoint CurrentPetHomePoint { get; set; }

	public JukePoint CurrentjukePoint { get; set; }

	public CraftPoint CurrentCraftPoint { get; set; }

	public List<UnityEx.ValueTuple<Popup.Tutorial.Type, bool>> TutorialIndexList { get; set; } = new List<UnityEx.ValueTuple<Popup.Tutorial.Type, bool>>();


	public bool CurrentInteractionState { get; private set; }

	public Actor CommCompanion { get; set; }

	public AgentActor AgentPartner
	{
		get
		{
			return base.Partner as AgentActor;
		}
		set
		{
			base.Partner = value;
		}
	}

	public AnimalBase Animal { get; set; }

	public bool IsRunning { get; set; }

	public bool IsBirthday(AgentActor agent)
	{
		bool flag = false;
		if (agent != null)
		{
			if (agent.AgentData.IsPlayerForBirthdayEvent)
			{
				return false;
			}
			if (agent.ChaControl.fileGameInfo.phase < 3)
			{
				return false;
			}
		}
		DateTime now = DateTime.Now;
		int birthMonth = base.ChaControl.fileParam.birthMonth;
		int birthDay = base.ChaControl.fileParam.birthDay;
		return now.Month == birthMonth && now.Day == birthDay;
	}

	public void ResetCoolTime()
	{
		_coolTime = 0f;
	}

	private void InitializeLabels()
	{
		SleepCommandInfos = new CommCommandList.CommandInfo[3]
		{
			new CommCommandList.CommandInfo("そのまま寝転ぶ")
			{
				Condition = null,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
					MapUIContainer.SetActiveCommandList(active: false);
					PlayerController.ChangeState("Lie");
				}
			},
			new CommCommandList.CommandInfo("寝る")
			{
				Condition = null,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
					ConfirmScene.Sentence = "一日を終了しますか？";
					ConfirmScene.OnClickedYes = delegate
					{
						Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_L);
						ElapseTime(delegate
						{
							int num = InvokeRelatedSleepEvent();
							return num != 1;
						}, delegate
						{
							foreach (KeyValuePair<int, AgentActor> item2 in Singleton<Manager.Map>.Instance.AgentTable)
							{
								if (item2.Value.CheckEventADV(2))
								{
									item2.Value.AdvEventStart_SleepingPlayer(this);
									return false;
								}
							}
							return true;
						});
						MapUIContainer.SetActiveCommandList(active: false);
						Singleton<Manager.Map>.Instance.Player.SetScheduledInteractionState(isEnabled: false);
						Singleton<Manager.Map>.Instance.Player.ReleaseInteraction();
						MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.None);
					};
					ConfirmScene.OnClickedNo = delegate
					{
						Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Cancel);
					};
					Singleton<Game>.Instance.LoadDialog();
				}
			},
			new CommCommandList.CommandInfo("起きる")
			{
				Condition = null,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Cancel);
					ActorAnimInfo animInfo4 = Animation.AnimInfo;
					ActivateNavMeshAgent();
					base.IsKinematic = false;
					SetStand(Animation.RecoveryPoint, animInfo4.endEnableBlend, animInfo4.endBlendSec, animInfo4.directionType);
					Animation.RecoveryPoint = null;
					Animation.RefsActAnimInfo = true;
					Controller.ChangeState("Normal");
					ReleaseCurrentPoint();
					if (PlayerController.CommandArea != null)
					{
						PlayerController.CommandArea.enabled = true;
					}
					MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.InvokeAcception);
					MapUIContainer.SetActiveCommandList(active: false);
				}
			}
		};
		CookCommandInfos = new CommCommandList.CommandInfo[3]
		{
			new CommCommandList.CommandInfo("料理をする")
			{
				Condition = null,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
					MapUIContainer.SetActiveCommandList(active: false);
					Controller.ChangeState("Cook");
					SetScheduledInteractionState(isEnabled: false);
				}
			},
			new CommCommandList.CommandInfo("貯蔵庫を見る")
			{
				Condition = () => !ContainsExcludePantryID(),
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
					MapUIContainer.SetActiveCommandList(active: false);
					Controller.ChangeState("Pantry");
					SetScheduledInteractionState(isEnabled: false);
				}
			},
			new CommCommandList.CommandInfo("立ち去る")
			{
				Condition = null,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Cancel);
					MapUIContainer.SetActiveCommandList(active: false);
					MapUIContainer.SetVisibleHUDExceptStoryUI(active: true);
					MapUIContainer.StorySupportUI.Open();
					Controller.ChangeState("Normal");
					ReleaseCurrentPoint();
					if (PlayerController.CommandArea != null)
					{
						PlayerController.CommandArea.enabled = true;
					}
					MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.InvokeAcception);
					ActivateNavMeshAgent();
					base.IsKinematic = false;
				}
			}
		};
		BaseCommandInfos = new CommCommandList.CommandInfo[2]
		{
			new CommCommandList.CommandInfo("ハウジング")
			{
				Condition = null,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
					MapUIContainer.SetActiveCommandList(active: false);
					Controller.ChangeState("Housing");
				}
			},
			new CommCommandList.CommandInfo("立ち去る")
			{
				Condition = null,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Cancel);
					MapUIContainer.SetActiveCommandList(active: false);
					MapUIContainer.SetVisibleHUDExceptStoryUI(active: true);
					MapUIContainer.StorySupportUI.Open();
					Controller.ChangeState("Normal");
				}
			}
		};
		DeviceCommandInfos = new CommCommandList.CommandInfo[7]
		{
			new CommCommandList.CommandInfo("女の子を登場")
			{
				Condition = () => Singleton<Game>.Instance.WorldData.AgentTable.TryGetValue(Singleton<Manager.Map>.Instance.AccessDeviceID, out var value5) && !value5.PlayEnterScene,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
					MapUIContainer.SetActiveCommandList(active: false);
					PlayerController.ChangeState("EntryChara");
				}
			},
			new CommCommandList.CommandInfo("女の子を変更")
			{
				Condition = () => !Singleton<Game>.Instance.WorldData.AgentTable.TryGetValue(Singleton<Manager.Map>.Instance.AccessDeviceID, out var value4) || value4.PlayEnterScene,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
					MapUIContainer.SetActiveCommandList(active: false);
					PlayerController.ChangeState("EditChara");
				}
			},
			new CommCommandList.CommandInfo("女の子の容姿変更")
			{
				Condition = () => !Singleton<Game>.Instance.WorldData.AgentTable.TryGetValue(Singleton<Manager.Map>.Instance.AccessDeviceID, out var value3) || value3.PlayEnterScene,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
					MapUIContainer.SetActiveCommandList(active: false);
					PlayerController.ChangeState("CharaLookEdit");
				}
			},
			new CommCommandList.CommandInfo("女の子の住む島を変更")
			{
				Condition = () => Singleton<Game>.Instance.WorldData.Cleared,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
					MapUIContainer.SetActiveCommandList(active: false);
					PlayerController.ChangeState("CharaMigration");
				}
			},
			new CommCommandList.CommandInfo("主人公を変更")
			{
				Condition = () => !Singleton<Game>.Instance.WorldData.AgentTable.TryGetValue(Singleton<Manager.Map>.Instance.AccessDeviceID, out var value2) || value2.PlayEnterScene,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
					MapUIContainer.SetActiveCommandList(active: false);
					PlayerController.ChangeState("EditPlayer");
				}
			},
			new CommCommandList.CommandInfo("主人公の容姿変更")
			{
				Condition = () => !Singleton<Game>.Instance.WorldData.AgentTable.TryGetValue(Singleton<Manager.Map>.Instance.AccessDeviceID, out var value) || value.PlayEnterScene,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
					MapUIContainer.SetActiveCommandList(active: false);
					PlayerController.ChangeState("PlayerLookEdit");
				}
			},
			new CommCommandList.CommandInfo("立ち去る")
			{
				Condition = null,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Cancel);
					Singleton<Manager.Map>.Instance.AccessDeviceID = -1;
					MapUIContainer.SetActiveCommandList(active: false);
					MapUIContainer.SetVisibleHUDExceptStoryUI(active: true);
					MapUIContainer.StorySupportUI.Open();
					Controller.ChangeState("Normal");
					CurrentDevicePoint = null;
				}
			}
		};
		List<CommCommandList.CommandInfo> list = ListPool<CommCommandList.CommandInfo>.Get();
		foreach (KeyValuePair<int, AssetBundleInfo> map in Singleton<Manager.Resources>.Instance.Map.MapList)
		{
			int id = map.Key;
			string mapName = map.Value.name;
			CommCommandList.CommandInfo commandInfo = new CommCommandList.CommandInfo(mapName);
			commandInfo.Condition = () => Singleton<Manager.Map>.IsInstance() && id != Singleton<Manager.Map>.Instance.MapID;
			commandInfo.Event = delegate
			{
				Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
				ConfirmScene.Sentence = $"{mapName}に移動しますか？";
				ConfirmScene.OnClickedYes = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_L);
					MapUIContainer.SetActiveCommandList(active: false);
					SetScheduledInteractionState(isEnabled: false);
					ReleaseInteraction();
					MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.None);
					Singleton<MapScene>.Instance.SaveProfile(isAuto: true);
					Singleton<Manager.Map>.Instance.ChangeMap(id, null, delegate
					{
						PlayerController.ChangeState("Normal");
						CameraControl.EnabledInput = true;
						MapUIContainer.SetVisibleHUD(active: true);
						MapUIContainer.StorySupportUI.Open();
						if (PlayerController.CommandArea != null)
						{
							PlayerController.CommandArea.enabled = true;
						}
						MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.InvokeAcception);
					});
				};
				ConfirmScene.OnClickedNo = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Cancel);
				};
				Singleton<Game>.Instance.LoadDialog();
			};
			CommCommandList.CommandInfo item = commandInfo;
			list.Add(item);
		}
		list.Add(new CommCommandList.CommandInfo("立ち去る")
		{
			Condition = null,
			Event = delegate
			{
				MapUIContainer.SetActiveCommandList(active: false);
				MapUIContainer.StorySupportUI.Open();
				PlayerController.ChangeState("Normal");
			}
		});
		ShipCommandInfos = list.ToArray();
		ListPool<CommCommandList.CommandInfo>.Release(list);
		ChickenCoopCommandInfos = new CommCommandList.CommandInfo[3]
		{
			new CommCommandList.CommandInfo("タマゴ箱を確認")
			{
				Condition = null,
				Event = delegate
				{
					MapUIContainer.CommandList.Visibled = false;
					MapUIContainer.SetActiveChickenCoopUI(active: true, ChickenCoopUI.Mode.EggBox);
				}
			},
			new CommCommandList.CommandInfo("ニワトリを追加")
			{
				Condition = null,
				Event = delegate
				{
					MapUIContainer.CommandList.Visibled = false;
					MapUIContainer.SetActiveChickenCoopUI(active: true, ChickenCoopUI.Mode.Coop);
				}
			},
			new CommCommandList.CommandInfo("立ち去る")
			{
				Condition = null,
				Event = delegate
				{
					MapUIContainer.SetActiveCommandList(active: false);
					PlayerController.ChangeState("Normal");
				}
			}
		};
		WarpCommandInfos = new CommCommandList.CommandInfo[2]
		{
			new CommCommandList.CommandInfo("移動する")
			{
				Condition = null,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
					ConfirmScene.Sentence = "移動しますか";
					ConfirmScene.OnClickedYes = delegate
					{
						MapUIContainer.SetActiveCommandList(active: false);
						SetScheduledInteractionState(isEnabled: false);
						ReleaseInteraction();
						MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.None);
						string prevStateName = PlayerController.PrevStateName;
						WarpPoint warpPoint = base.CurrentPoint as WarpPoint;
						WarpPoint warpPoint2 = warpPoint.PairPoint();
						Singleton<Manager.Map>.Instance.WarpToPairPoint(warpPoint2, delegate
						{
							MapUIContainer.SetVisibleHUDExceptStoryUI(active: true);
							MapUIContainer.StorySupportUI.Open();
							if (prevStateName == "Onbu")
							{
								Controller.ChangeState("Onbu");
							}
							else
							{
								Controller.ChangeState("Normal");
							}
							Controller.ChangeState("Idle");
							GC.Collect();
						}, delegate
						{
							if (prevStateName == "Onbu")
							{
								Controller.ChangeState("Onbu");
							}
							else
							{
								Controller.ChangeState("Normal");
							}
							Singleton<Manager.Input>.Instance.ReserveState(Manager.Input.ValidType.Action);
							Singleton<Manager.Input>.Instance.SetupState();
							SetScheduledInteractionState(isEnabled: true);
							ReleaseInteraction();
							Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Warp_Out);
						});
						Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Warp_In);
					};
					ConfirmScene.OnClickedNo = delegate
					{
						Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Cancel);
					};
					Singleton<Game>.Instance.LoadDialog();
				}
			},
			new CommCommandList.CommandInfo("立ち去る")
			{
				Condition = null,
				Event = delegate
				{
					CancelWarp();
				}
			}
		};
		CoSleepCommandInfos = new CommCommandList.CommandInfo[4]
		{
			new CommCommandList.CommandInfo("エッチがしたい")
			{
				Condition = delegate
				{
					AgentActor agentPartner5 = AgentPartner;
					return agentPartner5.CanSelectHCommand() && !agentPartner5.IsBadMood();
				},
				Event = delegate
				{
					AgentActor agentPartner4 = AgentPartner;
					int personality4 = agentPartner4.ChaControl.fileParam.personality;
					agentPartner4.Animation.StopAllAnimCoroutine();
					AgentPartner.openData.FindLoad("1", personality4, 9);
					AgentPartner.packData.AttitudeID = 1;
					AgentPartner.packData.onComplete = delegate
					{
						if (AgentPartner.packData.isSuccessH)
						{
							AgentPartner.packData.restoreCommands = null;
							HSceneManager.SleepStart = true;
							InitiateHScene();
						}
						else
						{
							AgentPartner.packData.restoreCommands = CoSleepCommandInfos;
						}
					};
					Singleton<MapUIContainer>.Instance.OpenADV(AgentPartner.openData, AgentPartner.packData);
				}
			},
			new CommCommandList.CommandInfo("エッチなことをする")
			{
				Condition = delegate
				{
					if (base.ChaControl.sex == 1 && !base.ChaControl.fileParam.futanari)
					{
						return false;
					}
					AgentActor agentPartner3 = AgentPartner;
					return agentPartner3.CanSelectHCommand() && agentPartner3.IsBadMood();
				},
				Event = delegate
				{
					AgentActor partner2 = AgentPartner;
					int personality3 = partner2.ChaControl.fileParam.personality;
					partner2.Animation.StopAllAnimCoroutine();
					AgentPartner.openData.FindLoad("1", personality3, 9);
					AgentPartner.packData.AttitudeID = 1;
					AgentPartner.packData.onComplete = delegate
					{
						if (partner2.packData.isSuccessH)
						{
							partner2.packData.restoreCommands = null;
							HSceneManager.SleepStart = true;
							Singleton<HSceneManager>.Instance.isForce = true;
							InitiateHScene();
						}
						else
						{
							AgentPartner.packData.restoreCommands = CoSleepCommandInfos;
						}
					};
					Singleton<MapUIContainer>.Instance.OpenADV(AgentPartner.openData, AgentPartner.packData);
				}
			},
			new CommCommandList.CommandInfo("寝る")
			{
				Condition = () => Singleton<Manager.Map>.Instance.CanSleepInTime(),
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
					ConfirmScene.Sentence = "一日を終了しますか？";
					ConfirmScene.OnClickedYes = delegate
					{
						Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_L);
						ElapseTime(delegate
						{
							OnElapsedTimeFromDateSleep();
						});
						MapUIContainer.SetActiveCommandList(active: false);
						SetScheduledInteractionState(isEnabled: false);
						ReleaseInteraction();
						MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.None);
					};
					ConfirmScene.OnClickedNo = delegate
					{
						Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Cancel);
					};
					Singleton<Game>.Instance.LoadDialog();
				}
			},
			new CommCommandList.CommandInfo("起きる")
			{
				Condition = null,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Cancel);
					CameraControl.Mode = CameraMode.Normal;
					ActorAnimInfo animInfo2 = Animation.AnimInfo;
					ActivateNavMeshAgent();
					base.IsKinematic = false;
					SetStand(Animation.RecoveryPoint, animInfo2.endEnableBlend, animInfo2.endBlendSec, animInfo2.directionType);
					Animation.RecoveryPoint = null;
					Animation.RefsActAnimInfo = true;
					Controller.ChangeState("Normal");
					ReleaseCurrentPoint();
					if (PlayerController.CommandArea != null)
					{
						PlayerController.CommandArea.enabled = true;
					}
					AgentActor agentPartner2 = AgentPartner;
					if (agentPartner2 != null)
					{
						agentPartner2.ActivateNavMeshAgent();
						agentPartner2.SetActiveOnEquipedItem(active: true);
						ActorAnimInfo animInfo3 = agentPartner2.Animation.AnimInfo;
						agentPartner2.SetStand(agentPartner2.Animation.RecoveryPoint, animInfo3.outEnableBlend, animInfo3.outBlendSec, animInfo3.directionType);
						if (OldEnabledHoldingHand)
						{
							HandsHolder.enabled = true;
							OldEnabledHoldingHand = false;
						}
						agentPartner2.ResetActionFlag();
						agentPartner2.BehaviorResources.ChangeMode(Desire.ActionType.Date);
					}
					MapUIContainer.SetActiveCommandList(active: false);
					MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.InvokeAcception);
					MapUIContainer.SetVisibleHUDExceptStoryUI(active: true);
					MapUIContainer.StorySupportUI.Open();
				}
			}
		};
		DateEatCommandInfos = new CommCommandList.CommandInfo[2]
		{
			new CommCommandList.CommandInfo("一緒にご飯を食べる")
			{
				Condition = null,
				Event = delegate
				{
					PlayerData.DateEatTrigger = true;
					MapUIContainer.CommandList.Visibled = false;
					AgentActor partner = AgentPartner;
					int personality2 = partner.ChaControl.fileParam.personality;
					partner.Animation.StopAllAnimCoroutine();
					partner.packData.Init();
					string asset2 = ((partner.ChaControl.fileGameInfo.phase < 2) ? "0" : "1");
					partner.openData.FindLoad(asset2, personality2, 8);
					partner.packData.onComplete = delegate
					{
						partner.packData.restoreCommands = null;
						partner.packData.Release();
						PlayerData.DateEatTrigger = true;
						CameraControl.Mode = CameraMode.Normal;
						Controller.ChangeState("Normal");
						partner.ActivateNavMeshAgent();
						partner.SetActiveOnEquipedItem(active: true);
						ActorAnimInfo animInfo = partner.Animation.AnimInfo;
						partner.SetStand(partner.Animation.RecoveryPoint, animInfo.endEnableBlend, animInfo.endBlendSec, animInfo.directionType);
						if (OldEnabledHoldingHand)
						{
							HandsHolder.enabled = true;
							OldEnabledHoldingHand = false;
						}
						partner.BehaviorResources.ChangeMode(Desire.ActionType.Date);
						MapUIContainer.SetActiveCommandList(active: false);
					};
					Singleton<MapUIContainer>.Instance.OpenADV(partner.openData, partner.packData);
				}
			},
			new CommCommandList.CommandInfo("立ち去る")
			{
				Condition = null,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Cancel);
					CameraControl.Mode = CameraMode.Normal;
					CancelCommand();
				}
			}
		};
		SpecialHCommandInfo = new CommCommandList.CommandInfo[9]
		{
			new CommCommandList.CommandInfo("分娩台")
			{
				Condition = () => HPoseID == 2,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
					MapUIContainer.SetActiveCommandList(active: false);
					StartSpecialH();
				}
			},
			new CommCommandList.CommandInfo("木馬")
			{
				Condition = () => HPoseID == 3,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
					MapUIContainer.SetActiveCommandList(active: false);
					StartSpecialH();
				}
			},
			new CommCommandList.CommandInfo("拘束台鞍馬")
			{
				Condition = () => HPoseID == 4,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
					MapUIContainer.SetActiveCommandList(active: false);
					StartSpecialH();
				}
			},
			new CommCommandList.CommandInfo("ギロチン")
			{
				Condition = () => HPoseID == 5,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
					MapUIContainer.SetActiveCommandList(active: false);
					StartSpecialH();
				}
			},
			new CommCommandList.CommandInfo("拘束デンマ台")
			{
				Condition = () => HPoseID == 6,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
					MapUIContainer.SetActiveCommandList(active: false);
					StartSpecialH();
				}
			},
			new CommCommandList.CommandInfo("拘束機械姦")
			{
				Condition = () => HPoseID == 7,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
					MapUIContainer.SetActiveCommandList(active: false);
					StartSpecialH();
				}
			},
			new CommCommandList.CommandInfo("吊るし挿入")
			{
				Condition = () => HPoseID == 8,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
					MapUIContainer.SetActiveCommandList(active: false);
					StartSpecialH();
				}
			},
			new CommCommandList.CommandInfo("拘束具")
			{
				Condition = () => HPoseID == 9,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_S);
					MapUIContainer.SetActiveCommandList(active: false);
					StartSpecialH(-1);
				}
			},
			new CommCommandList.CommandInfo("立ち去る")
			{
				Condition = null,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Cancel);
					Controller.ChangeState("Normal");
					MapUIContainer.SetActiveCommandList(active: false);
				}
			}
		};
		ExitEatEventCommandInfo = new CommCommandList.CommandInfo[2]
		{
			new CommCommandList.CommandInfo("一緒にご飯を食べる")
			{
				Condition = null,
				Event = delegate
				{
					AgentActor agentPartner = AgentPartner;
					int personality = agentPartner.ChaControl.fileParam.personality;
					agentPartner.Animation.StopAllAnimCoroutine();
					OpenData openData = agentPartner.openData;
					AgentActor.PackData packData = agentPartner.packData;
					packData.Init();
					string asset = ((agentPartner.ChaControl.fileGameInfo.phase < 2) ? "0" : "1");
					openData.FindLoad(asset, personality, 8);
					packData.onComplete = delegate
					{
						packData.restoreCommands = null;
						packData.Release();
						packData.Release();
						Singleton<Manager.ADV>.Instance.Captions.CanvasGroupOFF();
						Singleton<MapUIContainer>.Instance.advScene.gameObject.SetActive(value: false);
						ExitEatEventADV();
					};
					Singleton<MapUIContainer>.Instance.OpenADV(agentPartner.openData, agentPartner.packData);
					MapUIContainer.SetActiveCommandList(active: false);
					SetScheduledInteractionState(isEnabled: false);
					ReleaseInteraction();
					MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.None);
				}
			},
			new CommCommandList.CommandInfo("立ち去る")
			{
				Condition = null,
				Event = delegate
				{
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Cancel);
					ExitEatEventADV();
					MapUIContainer.SetActiveCommandList(active: false);
					SetScheduledInteractionState(isEnabled: false);
					ReleaseInteraction();
					MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.None);
				}
			}
		};
	}

	public void CancelCommand()
	{
		Controller.ChangeState("Normal");
		AgentActor agentPartner = AgentPartner;
		if (agentPartner != null)
		{
			agentPartner.ActivateNavMeshAgent();
			agentPartner.SetActiveOnEquipedItem(active: true);
			ActorAnimInfo animInfo = agentPartner.Animation.AnimInfo;
			agentPartner.SetStand(agentPartner.Animation.RecoveryPoint, animInfo.outEnableBlend, animInfo.outBlendSec, animInfo.directionType);
			if (OldEnabledHoldingHand)
			{
				HandsHolder.enabled = true;
				OldEnabledHoldingHand = false;
			}
			agentPartner.BehaviorResources.ChangeMode(Desire.ActionType.Date);
		}
		MapUIContainer.SetActiveCommandList(active: false);
	}

	public void CancelWarp()
	{
		Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Cancel);
		MapUIContainer.SetActiveCommandList(active: false);
		MapUIContainer.SetVisibleHUDExceptStoryUI(active: true);
		MapUIContainer.StorySupportUI.Open();
		if (PlayerController.PrevStateName == "Onbu")
		{
			PlayerController.ChangeState(PlayerController.PrevStateName);
		}
		else
		{
			PlayerController.ChangeState("Normal");
		}
		ReleaseCurrentPoint();
		if (PlayerController.CommandArea != null)
		{
			PlayerController.CommandArea.enabled = true;
		}
		MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.InvokeAcception);
		ActivateNavMeshAgent();
		base.IsKinematic = false;
	}

	public void CallProc()
	{
		if (_called)
		{
			return;
		}
		List<Desire.ActionType> encounterWhitelist = Singleton<Manager.Resources>.Instance.AgentProfile.EncounterWhitelist;
		float durationCTForCall = Singleton<Manager.Resources>.Instance.AgentProfile.DurationCTForCall;
		StatusProfile statusProfile = Singleton<Manager.Resources>.Instance.StatusProfile;
		int? num = base.MapArea?.ChunkID;
		int num2 = ((!num.HasValue) ? PlayerData.ChunkID : num.Value);
		foreach (KeyValuePair<int, AgentActor> item in Singleton<Manager.Map>.Instance.AgentTable)
		{
			if (item.Value.Mode == Desire.ActionType.Called || item.Value.Mode == Desire.ActionType.Date || item.Value.Mode == Desire.ActionType.Onbu || item.Value.Mode == Desire.ActionType.Cold2A || item.Value.Mode == Desire.ActionType.Cold2B || item.Value.Mode == Desire.ActionType.Cold3A || item.Value.Mode == Desire.ActionType.Cold3B || item.Value.Mode == Desire.ActionType.OverworkA || item.Value.Mode == Desire.ActionType.OverworkB || item.Value.Mode == Desire.ActionType.Cold2BMedicated || item.Value.Mode == Desire.ActionType.Cold3BMedicated || item.Value.Mode == Desire.ActionType.WeaknessA || item.Value.Mode == Desire.ActionType.WeaknessB || item.Value.Mode == Desire.ActionType.FoundPeeping || item.Value.EventKey == EventType.Sleep || item.Value.EventKey == EventType.Toilet || item.Value.EventKey == EventType.Bath || item.Value.EventKey == EventType.Move || item.Value.EventKey == EventType.Masturbation || item.Value.EventKey == EventType.Lesbian || item.Value.Partner != null)
			{
				continue;
			}
			int? num3 = item.Value.MapArea?.ChunkID;
			int num4 = ((!num3.HasValue) ? item.Value.AgentData.ChunkID : num3.Value);
			ChaFileGameInfo fileGameInfo = item.Value.ChaControl.fileGameInfo;
			AgentData agentData = item.Value.AgentData;
			float callProbBaseRate = statusProfile.CallProbBaseRate;
			callProbBaseRate += statusProfile.CallProbPhaseRate * (float)(fileGameInfo.phase + 1);
			float num5 = 0f;
			int num6 = fileGameInfo.flavorState[1];
			int num7 = 0;
			int[] callReliabilityBorder = statusProfile.CallReliabilityBorder;
			foreach (int num8 in callReliabilityBorder)
			{
				if (num6 < num8)
				{
					break;
				}
				num7++;
			}
			num5 += statusProfile.CallReliabilityBuff[num7];
			float num9 = agentData.StatsTable[1];
			if (num9 < fileGameInfo.moodBound.lower)
			{
				num5 += statusProfile.CallLowerMoodProb;
			}
			else if (num9 > fileGameInfo.moodBound.upper)
			{
				num5 += statusProfile.CallUpperMoodProb;
			}
			if (item.Value.ChaControl.fileGameInfo.normalSkill.ContainsValue(34))
			{
				num5 += statusProfile.CallProbSuperSense;
			}
			else if (agentData.CallCTCount < durationCTForCall)
			{
				agentData.CalledCount++;
				if (agentData.CalledCount == 2)
				{
					num5 += statusProfile.CallSecondTimeProb;
				}
				else if (agentData.CalledCount >= 3)
				{
					num5 += statusProfile.CallOverTimeProb;
				}
			}
			else
			{
				agentData.CallCTCount = 0f;
				agentData.CalledCount++;
			}
			float num10 = callProbBaseRate + num5;
			if (!item.Value.ChaControl.fileGameInfo.normalSkill.ContainsValue(34) && num4 != num2)
			{
				num10 *= 0.5f;
			}
			float value = UnityEngine.Random.value;
			if (!(value < num10))
			{
				continue;
			}
			AgentActor value2 = item.Value;
			int num11 = -1;
			item.Value.PoseID = num11;
			value2.ActionID = num11;
			agentData.CarryingItem = null;
			item.Value.StateType = State.Type.Normal;
			if (item.Value.CurrentPoint != null)
			{
				item.Value.CurrentPoint.SetActiveMapItemObjs(active: true);
				item.Value.CurrentPoint.ReleaseSlot(item.Value);
				item.Value.CurrentPoint = null;
			}
			if (item.Value.CommandPartner != null)
			{
				Actor commandPartner = item.Value.CommandPartner;
				if (commandPartner is AgentActor)
				{
					AgentActor agentActor = commandPartner as AgentActor;
					agentActor.CommandPartner = null;
					agentActor.ChangeBehavior(Desire.ActionType.Normal);
				}
				else if (commandPartner is MerchantActor)
				{
					MerchantActor merchantActor = commandPartner as MerchantActor;
					merchantActor.CommandPartner = null;
					merchantActor.ChangeBehavior(merchantActor.LastNormalMode);
				}
				item.Value.CommandPartner = null;
			}
			item.Value.EventKey = (EventType)0;
			item.Value.TargetInSightActionPoint = null;
			item.Value.TargetInSightActor = this;
			item.Value.CommandPartner = null;
			item.Value.ResetActionFlag();
			if (item.Value.Schedule.enabled)
			{
				BehaviorSchedule schedule = item.Value.Schedule;
				schedule.enabled = false;
				item.Value.Schedule = schedule;
			}
			item.Value.ActivateNavMeshAgent();
			item.Value.ActivateTransfer();
			item.Value.ClearItems();
			item.Value.ClearParticles();
			item.Value.Animation.ResetDefaultAnimatorController();
			item.Value.ChangeBehavior(Desire.ActionType.Called);
		}
		_called = true;
		Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Call);
		MapUIContainer.AddSystemLog("全員を呼び出しました", isNotify: true);
	}

	public bool ContainsExcludePantryID()
	{
		if (base.CurrentPoint == null)
		{
			return false;
		}
		List<int> exPantryCommandActPTIDs = Singleton<Manager.Resources>.Instance.PlayerProfile.ExPantryCommandActPTIDs;
		if (!base.CurrentPoint.IDList.IsNullOrEmpty())
		{
			int[] iDList = base.CurrentPoint.IDList;
			foreach (int item in iDList)
			{
				if (exPantryCommandActPTIDs.Contains(item))
				{
					return true;
				}
			}
			return false;
		}
		if (exPantryCommandActPTIDs.Contains(base.CurrentPoint.ID))
		{
			return true;
		}
		return false;
	}

	public void InitiateHScene()
	{
		Singleton<HSceneManager>.Instance.HsceneEnter(AgentPartner);
	}

	public void ElapseTime(System.Action action, bool fadeOut = true)
	{
		if (_disposable != null)
		{
			_disposable.Dispose();
		}
		_enumerator = ElapseTimeCoroutine(action, fadeOut);
		_disposable = Observable.FromCoroutine(() => _enumerator).Subscribe(delegate
		{
		}, delegate(Exception ex)
		{
			if (Debug.isDebugBuild)
			{
				Debug.LogException(ex);
			}
		});
	}

	private IEnumerator ElapseTimeCoroutine(System.Action action, bool fadeOut = true)
	{
		IObservable<Unit> stream2 = MapUIContainer.StartFade(FadeCanvas.PanelType.Blackout, FadeType.In, 3f, ignoreTimeScale: true) ?? Observable.Empty<Unit>();
		yield return stream2.ToYieldInstruction();
		action?.Invoke();
		Singleton<Manager.Map>.Instance.SetVisibleAll(active: false);
		DateTime now = Singleton<Manager.Map>.Instance.Simulator.Now;
		PlayerProfile playerProfile = Singleton<Manager.Resources>.Instance.PlayerProfile;
		EnvironmentSimulator.DateTimeSerialization wakeTime = playerProfile.WakeTime;
		if (now > wakeTime.Time && now < _midnightTime)
		{
			wakeTime.Time = wakeTime.Time.AddDays(1.0);
		}
		float elapsedTime = 0f;
		TimeSpan sleepDuration = wakeTime.Time - now;
		_elapsedTimeInSleep = TimeSpan.Zero;
		float prevDayLength = Singleton<Manager.Map>.Instance.EnvironmentProfile.DayLengthInMinute;
		Singleton<Manager.Map>.Instance.EnvironmentProfile.DayLengthInMinute = prevDayLength / 8f;
		Time.timeScale = 20f;
		while (_elapsedTimeInSleep < sleepDuration)
		{
			elapsedTime += Time.unscaledDeltaTime;
			yield return null;
		}
		Time.timeScale = 1f;
		Singleton<Manager.Map>.Instance.EnvironmentProfile.DayLengthInMinute = prevDayLength;
		Debug.Log($"睡眠の経過時間(実測)：{elapsedTime}");
		Singleton<Manager.Map>.Instance.SetVisibleAll(active: true);
		if (fadeOut)
		{
			stream2 = MapUIContainer.StartFade(FadeCanvas.PanelType.Blackout, FadeType.Out, 3f, ignoreTimeScale: true) ?? Observable.Empty<Unit>();
			yield return stream2.ToYieldInstruction();
		}
		_enumerator = null;
	}

	public void ElapseTime(Func<bool> conditionBefore, Func<bool> conditionAfter = null)
	{
		if (_disposable != null)
		{
			_disposable.Dispose();
		}
		_enumerator = ElapseTimeCoroutine(conditionBefore, conditionAfter);
		_disposable = Observable.FromCoroutine(() => _enumerator).Subscribe(delegate
		{
		}, delegate(Exception ex)
		{
			if (Debug.isDebugBuild)
			{
				Debug.LogException(ex);
			}
		});
	}

	private IEnumerator ElapseTimeCoroutine(Func<bool> conditionBefore, Func<bool> conditionAfter)
	{
		IObservable<Unit> stream2 = MapUIContainer.StartFade(FadeCanvas.PanelType.Blackout, FadeType.In, 3f, ignoreTimeScale: true) ?? Observable.Empty<Unit>();
		yield return stream2.ToYieldInstruction();
		bool? flag = conditionBefore?.Invoke();
		if (flag.HasValue && flag.Value)
		{
			Singleton<Manager.Map>.Instance.SetVisibleAll(active: false);
			DateTime now = Singleton<Manager.Map>.Instance.Simulator.Now;
			PlayerProfile playerProfile = Singleton<Manager.Resources>.Instance.PlayerProfile;
			EnvironmentSimulator.DateTimeSerialization wakeTime = playerProfile.WakeTime;
			DateTime sleepableBegin = playerProfile.CanSleepTime[0].min.Time;
			if (now > sleepableBegin && now < _midnightTime)
			{
				wakeTime.Time = wakeTime.Time.AddDays(1.0);
			}
			float elapsedTime = 0f;
			TimeSpan sleepDuration = wakeTime.Time - now;
			_elapsedTimeInSleep = TimeSpan.Zero;
			float prevDayLength = Singleton<Manager.Map>.Instance.EnvironmentProfile.DayLengthInMinute;
			Singleton<Manager.Map>.Instance.EnvironmentProfile.DayLengthInMinute = prevDayLength / 8f;
			Time.timeScale = 20f;
			while (_elapsedTimeInSleep < sleepDuration)
			{
				elapsedTime += Time.unscaledDeltaTime;
				yield return null;
			}
			Time.timeScale = 1f;
			Singleton<Manager.Map>.Instance.EnvironmentProfile.DayLengthInMinute = prevDayLength;
			Debug.Log($"睡眠の経過時間(実測)：{elapsedTime}");
			Singleton<Manager.Map>.Instance.SetVisibleAll(active: true);
			bool? flag2 = conditionAfter?.Invoke();
			if (!flag2.HasValue || flag2.Value)
			{
				stream2 = MapUIContainer.StartFade(FadeCanvas.PanelType.Blackout, FadeType.Out, 3f, ignoreTimeScale: true) ?? Observable.Empty<Unit>();
				yield return stream2.ToYieldInstruction();
				_enumerator = null;
			}
		}
	}

	private int InvokeRelatedSleepEvent()
	{
		if (Game.isAdd01 && (base.ChaControl.sex == 0 || (base.ChaControl.sex == 1 && base.ChaControl.fileParam.futanari)))
		{
			List<AgentActor> list = ListPool<AgentActor>.Get();
			foreach (KeyValuePair<int, AgentActor> item in Singleton<Manager.Map>.Instance.AgentTable)
			{
				if (item.Value.CanRevRape())
				{
					int chunkID = item.Value.ChunkID;
					if (chunkID == base.ChunkID)
					{
						list.Add(item.Value);
					}
				}
			}
			if (!list.IsNullOrEmpty())
			{
				AgentActor agentActor = null;
				List<AgentActor> list2 = list.FindAll((AgentActor x) => x.Mode == Desire.ActionType.SearchRevRape);
				if (!list2.IsNullOrEmpty())
				{
					agentActor = list2.GetElement(UnityEngine.Random.Range(0, list2.Count));
				}
				if (agentActor == null)
				{
					agentActor = list.GetElement(UnityEngine.Random.Range(0, list.Count));
				}
				ListPool<AgentActor>.Release(list);
				if (agentActor.Mode == Desire.ActionType.Normal || agentActor.Mode == Desire.ActionType.SearchSleep || agentActor.Mode == Desire.ActionType.Encounter)
				{
					float value = agentActor.AgentData.StatsTable[6];
					float t = Mathf.InverseLerp(0f, 100f, value);
					float num = Singleton<Manager.Resources>.Instance.StatusProfile.YobaiMinMax.Lerp(t);
					float num2 = UnityEngine.Random.Range(0f, 100f);
					if (num2 < num)
					{
						StartSneakH(agentActor);
						return 1;
					}
					PoseKeyPair sleepTogetherRight = Singleton<Manager.Resources>.Instance.AgentProfile.PoseIDTable.SleepTogetherRight;
					PoseKeyPair sleepTogetherLeft = Singleton<Manager.Resources>.Instance.AgentProfile.PoseIDTable.SleepTogetherLeft;
					List<ActionPoint> groupActionPoints = base.CurrentPoint.GroupActionPoints;
					ActionPoint actionPoint = null;
					foreach (ActionPoint item2 in groupActionPoints)
					{
						if (item2.IsNeutralCommand)
						{
							actionPoint = item2;
							break;
						}
					}
					if (actionPoint != null && (actionPoint.FindAgentActionPointInfo(EventType.Sleep, sleepTogetherRight.poseID, out var outInfo) || actionPoint.FindAgentActionPointInfo(EventType.Sleep, sleepTogetherLeft.poseID, out outInfo)))
					{
						Transform t2 = actionPoint.transform.FindLoop(outInfo.baseNullName)?.transform ?? actionPoint.transform;
						GameObject gameObject = actionPoint.transform.FindLoop(outInfo.recoveryNullName);
						agentActor.Animation.RecoveryPoint = gameObject?.transform;
						PlayState playState = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[outInfo.eventID][outInfo.poseID];
						agentActor.Animation.LoadEventKeyTable(outInfo.eventID, outInfo.poseID);
						agentActor.LoadEventItems(playState);
						agentActor.LoadEventParticles(outInfo.eventID, outInfo.poseID);
						agentActor.Animation.InitializeStates(playState);
						agentActor.Animation.LoadAnimatorIfNotEquals(playState);
						ActorAnimInfo actorAnimInfo = default(ActorAnimInfo);
						actorAnimInfo.layer = playState.Layer;
						actorAnimInfo.inEnableBlend = playState.MainStateInfo.InStateInfo.EnableFade;
						actorAnimInfo.inBlendSec = playState.MainStateInfo.InStateInfo.FadeSecond;
						actorAnimInfo.outEnableBlend = playState.MainStateInfo.OutStateInfo.EnableFade;
						actorAnimInfo.outBlendSec = playState.MainStateInfo.OutStateInfo.FadeSecond;
						actorAnimInfo.directionType = playState.DirectionType;
						actorAnimInfo.endEnableBlend = playState.EndEnableBlend;
						actorAnimInfo.endBlendSec = playState.EndBlendRate;
						actorAnimInfo.isLoop = playState.MainStateInfo.IsLoop;
						actorAnimInfo.loopMinTime = playState.MainStateInfo.LoopMin;
						actorAnimInfo.loopMaxTime = playState.MainStateInfo.LoopMax;
						actorAnimInfo.hasAction = playState.ActionInfo.hasAction;
						actorAnimInfo.loopStateName = playState.MainStateInfo.InStateInfo.StateInfos.GetElement(playState.MainStateInfo.InStateInfo.StateInfos.Length - 1).stateName;
						actorAnimInfo.randomCount = playState.ActionInfo.randomCount;
						actorAnimInfo.oldNormalizedTime = 0f;
						ActorAnimInfo actorAnimInfo2 = actorAnimInfo;
						agentActor.Animation.AnimInfo = actorAnimInfo2;
						ActorAnimInfo actorAnimInfo3 = actorAnimInfo2;
						agentActor.SetActiveOnEquipedItem(active: false);
						agentActor.ChaControl.setAllLayerWeight(0f);
						agentActor.DisableActionFlag();
						agentActor.DeactivateNavMeshAgent();
						agentActor.IsKinematic = true;
						agentActor.Animation.PlayInAnimation(actorAnimInfo3.inEnableBlend, actorAnimInfo3.inBlendSec, playState.MainStateInfo.FadeOutTime, actorAnimInfo3.layer);
						agentActor.SetStand(t2, playState.MainStateInfo.InStateInfo.EnableFade, playState.MainStateInfo.InStateInfo.FadeSecond, playState.DirectionType);
						agentActor.CurrentPoint = actionPoint;
						agentActor.CurrentPoint.SetSlot(agentActor);
						agentActor.SetCurrentSchedule(actorAnimInfo3.isLoop, "添い寝", actorAnimInfo3.loopMinTime, actorAnimInfo3.loopMaxTime, actorAnimInfo3.hasAction, useGameTime: true);
						agentActor.ChangeBehavior(Desire.ActionType.EndTaskSleepAfterDate);
					}
				}
				else if (agentActor.Mode == Desire.ActionType.SearchRevRape)
				{
					StartSneakH(agentActor);
					return 1;
				}
			}
			else
			{
				ListPool<AgentActor>.Release(list);
			}
		}
		return 0;
	}

	public void StartSneakH(AgentActor agent)
	{
		agent.packData.Init();
		agent.Animation.StopAllAnimCoroutine();
		agent.SetActiveOnEquipedItem(active: false);
		agent.ChaControl.setAllLayerWeight(0f);
		agent.ChangeBehavior(Desire.ActionType.Idle);
		agent.BehaviorResources.ChangeMode(Desire.ActionType.Idle);
		agent.Mode = Desire.ActionType.Idle;
		agent.DisableBehavior();
		agent.DeactivateNavMeshAgent();
		MapUIContainer.SetVisibleHUD(active: false);
		agent.Position = base.Position;
		agent.Rotation = base.Rotation;
		PlayerController.ChangeState("Communication");
		CinemachineBlendDefinition.Style prevStyle = CameraControl.CinemachineBrain.m_DefaultBlend.m_Style;
		CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
		PoseKeyPair yobai = Singleton<Manager.Resources>.Instance.AgentProfile.PoseIDTable.Yobai;
		PlayState playState = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[yobai.postureID][yobai.poseID];
		AssetBundleInfo assetBundleInfo = playState.MainStateInfo.AssetBundleInfo;
		agent.ChangeAnimator(assetBundleInfo.assetbundle, assetBundleInfo.asset);
		Transform target = CameraControl.CameraComponent.transform;
		agent.SetLookPtn(1, 3);
		agent.SetLookTarget(1, 0, target);
		agent.openData.FindLoad("4", agent.AgentData.param.charaID, 2);
		agent.packData.onComplete = delegate
		{
			agent.packData.restoreCommands = null;
			agent.packData.Release();
			CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = prevStyle;
			agent.InitiateHScene(HSceneManager.HEvent.GyakuYobai);
		};
		Observable.EveryUpdate().Skip(1).SkipWhile((long _) => CameraControl.CinemachineBrain.IsBlending)
			.Take(1)
			.DelayFrame(5)
			.Subscribe(delegate
			{
				Singleton<MapUIContainer>.Instance.OpenADV(agent.openData, agent.packData);
			});
	}

	private void StartSpecialH(int advID = 0)
	{
		AgentPartner.packData.Init();
		ActionPoint currentPoint = base.CurrentPoint;
		AgentPartner.CommandPartner = this;
		if (OldEnabledHoldingHand = HandsHolder.enabled)
		{
			HandsHolder.enabled = false;
			if (HandsHolder.EnabledHolding)
			{
				HandsHolder.EnabledHolding = false;
			}
		}
		if (advID == 0)
		{
			AgentPartner.StartCommunication();
			Observable.EveryUpdate().Skip(1).SkipWhile((long _) => Animation.PlayingTurnAnimation)
				.Take(1)
				.Subscribe(delegate
				{
					AgentPartner.openData.FindLoad("3", AgentPartner.charaID, 9);
					AgentPartner.packData.onComplete = delegate
					{
						AgentPartner.packData.restoreCommands = null;
						HPoint hPoint2 = currentPoint.HPoint;
						AgentPartner.CommandPartner = null;
						ReleaseCurrentPoint();
						AgentPartner.packData.Release();
						Singleton<HSceneManager>.Instance.HousingHEnter(AgentPartner, hPoint2);
					};
					Singleton<MapUIContainer>.Instance.OpenADV(AgentPartner.openData, AgentPartner.packData);
				});
			return;
		}
		AgentPartner.StopNavMeshAgent();
		AgentPartner.SetActiveOnEquipedItem(active: false);
		AgentPartner.DisableBehavior();
		Observable.EveryUpdate().Take(5).DoOnCompleted(delegate
		{
			AgentPartner.packData.restoreCommands = null;
			HPoint hPoint = currentPoint.HPoint;
			AgentPartner.CommandPartner = null;
			ReleaseCurrentPoint();
			AgentPartner.packData.Release();
			Singleton<HSceneManager>.Instance.HousingHEnter(AgentPartner, hPoint);
			base.ChaControl.visibleAll = true;
		})
			.Subscribe(delegate
			{
				AgentPartner.BehaviorResources.Current.DisableBehavior(pause: true);
			}, delegate(Exception ex)
			{
				Debug.LogException(ex);
			});
		MapUIContainer.SetVisibleHUD(active: false);
	}

	public void StartSleepTogetherEvent(AgentActor agent)
	{
		agent.BehaviorResources.ChangeMode(Desire.ActionType.Idle);
		PlayerController.ChangeState("Idle");
		SetActiveOnEquipedItem(active: false);
		base.ChaControl.setAllLayerWeight(0f);
		Singleton<Manager.Input>.Instance.ReserveState(Manager.Input.ValidType.UI);
		Singleton<Manager.Input>.Instance.SetupState();
		MapUIContainer.SetVisibleHUDExceptStoryUI(active: false);
		MapUIContainer.StorySupportUI.Close();
		int item = AIProject.Definitions.Action.NameTable[EventType.Sleep].Item1;
		base.CurrentPoint.TryGetPlayerDateActionPointInfo(base.ChaControl.sex, EventType.Sleep, out var apInfo);
		int num = (base.PoseID = apInfo.poseIDA);
		int key = num;
		Transform t = base.CurrentPoint.transform.FindLoop(apInfo.baseNullNameA)?.transform ?? base.CurrentPoint.transform;
		Transform t2 = base.CurrentPoint.transform.FindLoop(apInfo.baseNullNameB)?.transform ?? base.CurrentPoint.transform;
		GameObject gameObject = base.CurrentPoint.transform.FindLoop(apInfo.recoveryNullNameA);
		Animation.RecoveryPoint = gameObject?.transform;
		GameObject gameObject2 = base.CurrentPoint.transform.FindLoop(apInfo.recoveryNullNameB);
		base.Partner.Animation.RecoveryPoint = gameObject2?.transform;
		PlayState playState = Singleton<Manager.Resources>.Instance.Animation.PlayerActionAnimTable[base.ChaControl.sex][item][key];
		Animation.LoadEventKeyTable(item, apInfo.poseIDA);
		LoadEventItems(playState);
		LoadEventParticles(item, apInfo.poseIDA);
		Animation.InitializeStates(playState);
		Actor partner = base.Partner;
		partner.Animation.LoadEventKeyTable(item, apInfo.poseIDB);
		PlayState playState2 = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[item][apInfo.poseIDB];
		partner.LoadEventItems(playState2);
		partner.LoadEventParticles(item, apInfo.poseIDB);
		partner.Animation.InitializeStates(playState2);
		ActorAnimInfo actorAnimInfo = default(ActorAnimInfo);
		actorAnimInfo.layer = playState.Layer;
		actorAnimInfo.inEnableBlend = playState.MainStateInfo.InStateInfo.EnableFade;
		actorAnimInfo.inBlendSec = playState.MainStateInfo.InStateInfo.FadeSecond;
		actorAnimInfo.outEnableBlend = playState.MainStateInfo.OutStateInfo.EnableFade;
		actorAnimInfo.outBlendSec = playState.MainStateInfo.OutStateInfo.FadeSecond;
		actorAnimInfo.directionType = playState.DirectionType;
		actorAnimInfo.isLoop = playState.MainStateInfo.IsLoop;
		ActorAnimInfo actorAnimInfo2 = actorAnimInfo;
		Animation.AnimInfo = actorAnimInfo2;
		ActorAnimInfo actorAnimInfo3 = actorAnimInfo2;
		actorAnimInfo2 = default(ActorAnimInfo);
		actorAnimInfo2.layer = playState2.Layer;
		actorAnimInfo2.inEnableBlend = playState2.MainStateInfo.InStateInfo.EnableFade;
		actorAnimInfo2.inBlendSec = playState2.MainStateInfo.InStateInfo.FadeSecond;
		actorAnimInfo2.outEnableBlend = playState2.MainStateInfo.OutStateInfo.EnableFade;
		actorAnimInfo2.outBlendSec = playState2.MainStateInfo.OutStateInfo.FadeSecond;
		actorAnimInfo2.directionType = playState2.DirectionType;
		actorAnimInfo2.isLoop = playState2.MainStateInfo.IsLoop;
		actorAnimInfo2.loopMinTime = playState.MainStateInfo.LoopMin;
		actorAnimInfo2.loopMaxTime = playState.MainStateInfo.LoopMax;
		actorAnimInfo2.hasAction = playState.ActionInfo.hasAction;
		ActorAnimInfo actorAnimInfo4 = actorAnimInfo2;
		partner.Animation.AnimInfo = actorAnimInfo4;
		ActorAnimInfo actorAnimInfo5 = actorAnimInfo4;
		DeactivateNavMeshAgent();
		base.IsKinematic = true;
		partner.SetActiveOnEquipedItem(active: false);
		partner.ChaControl.setAllLayerWeight(0f);
		partner.DeactivateNavMeshAgent();
		partner.IsKinematic = true;
		Animation.PlayInAnimation(actorAnimInfo3.inEnableBlend, actorAnimInfo3.inBlendSec, playState.MainStateInfo.FadeOutTime, actorAnimInfo3.layer);
		SetStand(t, playState.MainStateInfo.InStateInfo.EnableFade, playState.MainStateInfo.InStateInfo.FadeSecond, playState.DirectionType);
		partner.Animation.PlayInAnimation(actorAnimInfo3.inEnableBlend, actorAnimInfo3.inBlendSec, playState2.MainStateInfo.FadeOutTime, actorAnimInfo3.layer);
		partner.SetStand(t2, actorAnimInfo5.inEnableBlend, actorAnimInfo5.inBlendSec, actorAnimInfo3.layer);
		Observable.EveryLateUpdate().Skip(1).Take(1)
			.Subscribe(delegate
			{
				if (apInfo.pointID == 501)
				{
					Manager.ADV.ChangeADVFixedAngleCamera(this, 5);
				}
				else if (apInfo.pointID == 500)
				{
					Manager.ADV.ChangeADVFixedAngleCamera(partner, 5);
				}
			});
		if (OldEnabledHoldingHand = HandsHolder.enabled)
		{
			HandsHolder.enabled = false;
			if (HandsHolder.EnabledHolding)
			{
				HandsHolder.EnabledHolding = false;
			}
		}
		CameraControl.SetShotTypeForce(ShotType.Near);
		_sleepEventEnumerator = SleepEventCoroutine();
		_sleepEventDisposable = Observable.FromCoroutine(() => _sleepEventEnumerator).Subscribe();
	}

	private IEnumerator SleepEventCoroutine()
	{
		yield return Observable.Timer(TimeSpan.FromMilliseconds(1000.0)).ToYieldInstruction();
		ElapseTime(delegate
		{
		}, fadeOut: false);
		while (ProcessingTimeSkip)
		{
			yield return null;
		}
		AgentActor partner = AgentPartner;
		OpenData openData = partner.openData;
		openData.FindLoad("1", partner.charaID, 13);
		AgentActor.PackData packData = partner.packData;
		packData.AttitudeID = 0;
		packData.Init();
		Singleton<Manager.Map>.Instance.DisableEntity(partner);
		SetStand(Animation.RecoveryPoint, enableFade: false, 0f, 0);
		partner.SetStand(partner.Animation.RecoveryPoint, enableFade: false, 0f, 0);
		PoseKeyPair idleID = Singleton<Manager.Resources>.Instance.AgentProfile.ADVIdleTable[partner.ChaControl.fileParam.personality];
		PlayState info = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[idleID.postureID][idleID.poseID];
		AssetBundleInfo abInfo = info.MainStateInfo.AssetBundleInfo;
		partner.ChangeAnimator(abInfo.assetbundle, abInfo.asset);
		Animator animator = partner.Animation.Animator;
		Queue<PlayState.Info> recoveryStates = QueuePool<PlayState.Info>.Get();
		PlayState.Info[] stateInfos = info.MainStateInfo.InStateInfo.StateInfos;
		foreach (PlayState.Info item in stateInfos)
		{
			recoveryStates.Enqueue(item);
		}
		float blendTime = info.MainStateInfo.InStateInfo.FadeSecond;
		while (recoveryStates.Count > 0)
		{
			PlayState.Info state = recoveryStates.Dequeue();
			partner.Animation.PlayAnimation(state.stateName, state.layer, 0f);
			yield return null;
			if (recoveryStates.Count == 0)
			{
				break;
			}
			yield return null;
			AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(state.layer);
			bool isInTransition = animator.IsInTransition(state.layer);
			while (isInTransition || (stateInfo.IsName(state.stateName) && stateInfo.normalizedTime < 1f))
			{
				stateInfo = animator.GetCurrentAnimatorStateInfo(state.layer);
				isInTransition = animator.IsInTransition(state.layer);
				yield return null;
			}
		}
		Vector3 to = base.Position;
		to.y = 0f;
		Vector3 from = partner.Position;
		from.y = 0f;
		Quaternion destRotation = Quaternion.LookRotation(to - from);
		partner.Rotation = destRotation;
		CinemachineBlendDefinition.Style prevStyle = CameraControl.CinemachineBrain.m_DefaultBlend.m_Style;
		CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
		Manager.ADV.ChangeADVCamera(partner);
		Observable.EveryLateUpdate().Skip(1).Take(1)
			.Subscribe(delegate
			{
				CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = prevStyle;
			});
		yield return Observable.Timer(TimeSpan.FromSeconds(2.0)).ToYieldInstruction();
		bool wait = false;
		packData.onComplete = delegate
		{
			wait = true;
			int desireKey = Desire.GetDesireKey(Desire.Type.Lonely);
			partner.SetDesire(desireKey, 0f);
			int desireKey2 = Desire.GetDesireKey(Desire.Type.Sleep);
			partner.SetDesire(desireKey2, 0f);
			packData.Release();
			Singleton<Manager.ADV>.Instance.Captions.EndADV();
		};
		IObservable<Unit> stream = MapUIContainer.StartFade(FadeCanvas.PanelType.Blackout, FadeType.Out, 3f, ignoreTimeScale: true) ?? Observable.Empty<Unit>();
		yield return stream.ToYieldInstruction();
		packData.restoreCommands = null;
		Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
		while (!wait)
		{
			yield return null;
		}
		partner.Animation.ResetDefaultAnimatorController();
		partner.ActivateNavMeshAgent();
		partner.SetActiveOnEquipedItem(active: true);
		partner.DeactivatePairing(0);
		PlayerController.ChangeState("Normal");
		CameraControl.Mode = CameraMode.Normal;
		CameraControl.RecoverShotType();
		partner.TargetInSightActionPoint = null;
		base.CurrentPoint = null;
		MapUIContainer.SetVisibleHUDExceptStoryUI(active: true);
		MapUIContainer.StorySupportUI.Open();
		Singleton<Manager.Map>.Instance.EnableEntity(partner);
		_sleepEventEnumerator = null;
	}

	public void StartGyakuYobaiEvent(AgentActor agent)
	{
		agent.BehaviorResources.ChangeMode(Desire.ActionType.Idle);
		agent.Mode = Desire.ActionType.Idle;
		MapUIContainer.FadeCanvas.StartFade(FadeCanvas.PanelType.Blackout, FadeType.In, 2f, ignoreTimeScale: true).Subscribe((Action<Unit>)delegate
		{
		}, (System.Action)delegate
		{
			Singleton<Manager.Map>.Instance.DisableEntity(agent);
			base.EventKey = EventType.Sleep;
			SetActiveOnEquipedItem(active: false);
			base.ChaControl.setAllLayerWeight(0f);
			SetScheduledInteractionState(isEnabled: false);
			ReleaseInteraction();
			Singleton<Manager.Input>.Instance.ReserveState(Manager.Input.ValidType.UI);
			Singleton<Manager.Input>.Instance.SetupState();
			PlayActionMotion(EventType.Sleep);
			OpenData openData = agent.openData;
			AgentActor.PackData packData = agent.packData;
			packData.Init();
			agent.Animation.StopAllAnimCoroutine();
			agent.SetActiveOnEquipedItem(active: false);
			agent.ChaControl.setAllLayerWeight(0f);
			agent.BehaviorResources.ChangeMode(Desire.ActionType.Idle);
			agent.Mode = Desire.ActionType.Idle;
			agent.DisableBehavior();
			agent.DeactivateNavMeshAgent();
			MapUIContainer.SetVisibleHUD(active: false);
			agent.Position = base.Position;
			agent.Rotation = base.Rotation;
			PlayerController.ChangeState("Communication");
			CinemachineBlendDefinition.Style prevStyle = CameraControl.CinemachineBrain.m_DefaultBlend.m_Style;
			CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
			PoseKeyPair yobai = Singleton<Manager.Resources>.Instance.AgentProfile.PoseIDTable.Yobai;
			PlayState playState = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[yobai.postureID][yobai.poseID];
			AssetBundleInfo assetBundleInfo = playState.MainStateInfo.AssetBundleInfo;
			agent.ChangeAnimator(assetBundleInfo.assetbundle, assetBundleInfo.asset);
			Transform target = CameraControl.CameraComponent.transform;
			agent.SetLookPtn(1, 3);
			agent.SetLookTarget(1, 0, target);
			openData.FindLoad("4", agent.AgentData.param.charaID, 13);
			packData.onComplete = delegate
			{
				packData.restoreCommands = null;
				packData.Release();
				CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = prevStyle;
				int desireKey = Desire.GetDesireKey(Desire.Type.Lonely);
				agent.SetDesire(desireKey, 0f);
				int desireKey2 = Desire.GetDesireKey(Desire.Type.Sleep);
				agent.SetDesire(desireKey2, 0f);
				Singleton<Manager.Map>.Instance.EnableEntity(agent);
				HSceneManager.SleepStart = true;
				agent.InitiateHScene(HSceneManager.HEvent.GyakuYobai);
			};
			Observable.EveryUpdate().Skip(1).SkipWhile((long _) => CameraControl.CinemachineBrain.IsBlending)
				.Take(1)
				.DelayFrame(5)
				.Subscribe(delegate
				{
					Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
				});
		});
	}

	public void StartEatEvent(AgentActor agent)
	{
		Singleton<Manager.Map>.Instance.DisableEntity(agent);
		agent.BehaviorResources.ChangeMode(Desire.ActionType.Idle);
		PlayerController.ChangeState("Idle");
		SetActiveOnEquipedItem(active: false);
		base.ChaControl.setAllLayerWeight(0f);
		Singleton<Manager.Input>.Instance.ReserveState(Manager.Input.ValidType.UI);
		Singleton<Manager.Input>.Instance.SetupState();
		MapUIContainer.SetVisibleHUDExceptStoryUI(active: false);
		MapUIContainer.StorySupportUI.Close();
		int item = AIProject.Definitions.Action.NameTable[EventType.Eat].Item1;
		base.CurrentPoint.TryGetPlayerDateActionPointInfo(base.ChaControl.sex, EventType.Eat, out var outInfo);
		int num = (base.PoseID = outInfo.poseIDA);
		int key = num;
		Transform t = base.CurrentPoint.transform.FindLoop(outInfo.baseNullNameA)?.transform ?? base.CurrentPoint.transform;
		Transform t2 = base.CurrentPoint.transform.FindLoop(outInfo.baseNullNameB)?.transform ?? base.CurrentPoint.transform;
		GameObject gameObject = base.CurrentPoint.transform.FindLoop(outInfo.recoveryNullNameA);
		Animation.RecoveryPoint = gameObject?.transform;
		GameObject gameObject2 = base.CurrentPoint.transform.FindLoop(outInfo.recoveryNullNameB);
		base.Partner.Animation.RecoveryPoint = gameObject2?.transform;
		PlayState playState = Singleton<Manager.Resources>.Instance.Animation.PlayerActionAnimTable[base.ChaControl.sex][item][key];
		Animation.LoadEventKeyTable(item, outInfo.poseIDA);
		LoadEventItems(playState);
		LoadEventParticles(item, outInfo.poseIDA);
		Animation.InitializeStates(playState);
		Actor partner = base.Partner;
		partner.Animation.LoadEventKeyTable(item, outInfo.poseIDB);
		PlayState playState2 = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[item][outInfo.poseIDB];
		partner.LoadEventItems(playState2);
		partner.LoadEventParticles(item, outInfo.poseIDB);
		partner.Animation.InitializeStates(playState2);
		ActorAnimInfo actorAnimInfo = default(ActorAnimInfo);
		actorAnimInfo.layer = playState.Layer;
		actorAnimInfo.inEnableBlend = playState.MainStateInfo.InStateInfo.EnableFade;
		actorAnimInfo.inBlendSec = playState.MainStateInfo.InStateInfo.FadeSecond;
		actorAnimInfo.outEnableBlend = playState.MainStateInfo.OutStateInfo.EnableFade;
		actorAnimInfo.outBlendSec = playState.MainStateInfo.OutStateInfo.FadeSecond;
		actorAnimInfo.directionType = playState.DirectionType;
		actorAnimInfo.isLoop = playState.MainStateInfo.IsLoop;
		ActorAnimInfo actorAnimInfo2 = actorAnimInfo;
		Animation.AnimInfo = actorAnimInfo2;
		ActorAnimInfo actorAnimInfo3 = actorAnimInfo2;
		actorAnimInfo2 = default(ActorAnimInfo);
		actorAnimInfo2.layer = playState2.Layer;
		actorAnimInfo2.inEnableBlend = playState2.MainStateInfo.InStateInfo.EnableFade;
		actorAnimInfo2.inBlendSec = playState2.MainStateInfo.InStateInfo.FadeSecond;
		actorAnimInfo2.outEnableBlend = playState2.MainStateInfo.OutStateInfo.EnableFade;
		actorAnimInfo2.outBlendSec = playState2.MainStateInfo.OutStateInfo.FadeSecond;
		actorAnimInfo2.directionType = playState2.DirectionType;
		actorAnimInfo2.isLoop = playState2.MainStateInfo.IsLoop;
		actorAnimInfo2.endEnableBlend = false;
		actorAnimInfo2.endBlendSec = 0f;
		actorAnimInfo2.loopMinTime = playState.MainStateInfo.LoopMin;
		actorAnimInfo2.loopMaxTime = playState.MainStateInfo.LoopMax;
		actorAnimInfo2.hasAction = playState.ActionInfo.hasAction;
		ActorAnimInfo actorAnimInfo4 = actorAnimInfo2;
		partner.Animation.AnimInfo = actorAnimInfo4;
		ActorAnimInfo actorAnimInfo5 = actorAnimInfo4;
		List<int> list = ListPool<int>.Get();
		foreach (KeyValuePair<int, Dictionary<int, int>> foodDateEventItem in Singleton<Manager.Resources>.Instance.Map.FoodDateEventItemList)
		{
			foreach (KeyValuePair<int, int> item2 in foodDateEventItem.Value)
			{
				if (item2.Value != -1)
				{
					list.Add(item2.Value);
				}
			}
		}
		int num2 = -1;
		if (!list.IsNullOrEmpty())
		{
			num2 = list.GetElement(UnityEngine.Random.Range(0, list.Count));
		}
		ListPool<int>.Release(list);
		if (Singleton<Manager.Resources>.Instance.Map.EventItemList.TryGetValue(num2, out var value))
		{
			LocomotionProfile locomotionProfile = Singleton<Manager.Resources>.Instance.LocomotionProfile;
			string rootParentName = locomotionProfile.RootParentName;
			GameObject gameObject3 = LoadEventItem(num2, rootParentName, isSync: false, value);
			if (gameObject3 != null)
			{
				Renderer[] componentsInChildren = gameObject3.GetComponentsInChildren<Renderer>(includeInactive: true);
				Renderer[] array = componentsInChildren;
				foreach (Renderer renderer in array)
				{
					renderer.enabled = true;
				}
			}
			GameObject gameObject4 = partner.LoadEventItem(num2, rootParentName, isSync: false, value);
			if (gameObject4 != null)
			{
				Renderer[] componentsInChildren2 = gameObject4.GetComponentsInChildren<Renderer>(includeInactive: true);
				Renderer[] array2 = componentsInChildren2;
				foreach (Renderer renderer2 in array2)
				{
					renderer2.enabled = true;
				}
			}
		}
		DeactivateNavMeshAgent();
		base.IsKinematic = true;
		partner.SetActiveOnEquipedItem(active: false);
		partner.ChaControl.setAllLayerWeight(0f);
		partner.DeactivateNavMeshAgent();
		partner.IsKinematic = true;
		Animation.PlayInAnimation(actorAnimInfo3.inEnableBlend, actorAnimInfo3.inBlendSec, playState.MainStateInfo.FadeOutTime, actorAnimInfo3.layer);
		SetStand(t, playState.MainStateInfo.InStateInfo.EnableFade, playState.MainStateInfo.InStateInfo.FadeSecond, playState.DirectionType);
		partner.Animation.PlayInAnimation(actorAnimInfo3.inEnableBlend, actorAnimInfo3.inBlendSec, playState2.MainStateInfo.FadeOutTime, actorAnimInfo3.layer);
		partner.SetStand(t2, actorAnimInfo5.inEnableBlend, actorAnimInfo5.inBlendSec, actorAnimInfo3.layer);
		if (OldEnabledHoldingHand = HandsHolder.enabled)
		{
			HandsHolder.enabled = false;
			if (HandsHolder.EnabledHolding)
			{
				HandsHolder.EnabledHolding = false;
			}
		}
		CameraControl.SetShotTypeForce(ShotType.Near);
		if (Singleton<Manager.Resources>.Instance.Action.AgentActionFlagTable.TryGetValue(item, out var value2) && value2.TryGetValue(outInfo.poseIDB, out var talkData))
		{
			Observable.EveryLateUpdate().Skip(1).Take(1)
				.Subscribe(delegate
				{
					Manager.ADV.ChangeADVFixedAngleCamera(partner, talkData.attitudeID);
				});
		}
		else
		{
			Manager.ADV.ChangeADVCameraDiagonal(partner);
		}
		_eatEventEnumerator = EatEventCoroutine();
		_eatEventDisposable = Observable.FromCoroutine(() => _eatEventEnumerator).Subscribe();
	}

	private IEnumerator EatEventCoroutine()
	{
		yield return Observable.Timer(TimeSpan.FromMilliseconds(1000.0)).ToYieldInstruction();
		AgentActor partner = AgentPartner;
		OpenData openData = partner.openData;
		openData.FindLoad("5", partner.charaID, 13);
		AgentActor.PackData packData = partner.packData;
		packData.AttitudeID = 0;
		packData.Init();
		bool wait = false;
		packData.onComplete = delegate
		{
			wait = true;
			packData.Release();
			Singleton<Manager.ADV>.Instance.Captions.CanvasGroupOFF();
			Singleton<MapUIContainer>.Instance.advScene.gameObject.SetActive(value: false);
		};
		packData.restoreCommands = null;
		Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
		while (!wait)
		{
			yield return null;
		}
		InitiateExitEatEventCommand();
		_eatEventEnumerator = null;
	}

	private void InitiateExitEatEventCommand()
	{
		PlayerController.ChangeState("ExitEatEvent");
	}

	private void ExitEatEventADV()
	{
		_exitEatEventEnumerator = ExitEatEventCoroutine();
		_exitEatEventDisposable = Observable.FromCoroutine(() => _exitEatEventEnumerator).Subscribe();
	}

	private IEnumerator ExitEatEventCoroutine()
	{
		AgentActor partner = AgentPartner;
		OpenData openData = partner.openData;
		AgentActor.PackData packData = partner.packData;
		CameraControl.CrossFade.FadeStart();
		SetStand(Animation.RecoveryPoint, enableFade: false, 0f, 0);
		partner.SetStand(partner.Animation.RecoveryPoint, enableFade: false, 0f, 0);
		PoseKeyPair idleID = Singleton<Manager.Resources>.Instance.AgentProfile.ADVIdleTable[partner.ChaControl.fileParam.personality];
		PlayState info = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[idleID.postureID][idleID.poseID];
		AssetBundleInfo abInfo = info.MainStateInfo.AssetBundleInfo;
		partner.ChangeAnimator(abInfo.assetbundle, abInfo.asset);
		Animator animator = partner.Animation.Animator;
		Queue<PlayState.Info> recoveryStates = QueuePool<PlayState.Info>.Get();
		PlayState.Info[] stateInfos = info.MainStateInfo.InStateInfo.StateInfos;
		foreach (PlayState.Info item in stateInfos)
		{
			recoveryStates.Enqueue(item);
		}
		float blendTime = info.MainStateInfo.InStateInfo.FadeSecond;
		while (recoveryStates.Count > 0)
		{
			PlayState.Info state = recoveryStates.Dequeue();
			partner.Animation.PlayAnimation(state.stateName, state.layer, 0f);
			yield return null;
			if (recoveryStates.Count == 0)
			{
				break;
			}
			yield return null;
			AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(state.layer);
			bool isInTransition = animator.IsInTransition(state.layer);
			while (isInTransition || (stateInfo.IsName(state.stateName) && stateInfo.normalizedTime < 1f))
			{
				stateInfo = animator.GetCurrentAnimatorStateInfo(state.layer);
				isInTransition = animator.IsInTransition(state.layer);
				yield return null;
			}
		}
		Vector3 to = base.Position;
		to.y = 0f;
		Vector3 from = partner.Position;
		from.y = 0f;
		Quaternion destRotation = Quaternion.LookRotation(to - from);
		partner.Rotation = destRotation;
		ClearItems();
		ClearParticles();
		partner.ClearItems();
		partner.ClearParticles();
		CinemachineBlendDefinition.Style prevStyle = CameraControl.CinemachineBrain.m_DefaultBlend.m_Style;
		CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
		Manager.ADV.ChangeADVCamera(partner);
		Observable.EveryLateUpdate().Skip(1).Take(1)
			.Subscribe(delegate
			{
				CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = prevStyle;
			});
		while (Singleton<Manager.ADV>.Instance.Captions.IsProcEndADV)
		{
			Singleton<Manager.Map>.Instance.Player.SetScheduledInteractionState(isEnabled: false);
			Singleton<Manager.Map>.Instance.Player.ReleaseInteraction();
			MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.None);
			yield return null;
		}
		Singleton<Manager.Map>.Instance.Player.SetScheduledInteractionState(isEnabled: false);
		Singleton<Manager.Map>.Instance.Player.ReleaseInteraction();
		MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.None);
		openData.FindLoad("2", partner.charaID, 13);
		packData.Init();
		bool wait = false;
		packData.onComplete = delegate
		{
			wait = true;
			int desireKey = Desire.GetDesireKey(Desire.Type.Lonely);
			partner.SetDesire(desireKey, 0f);
			int desireKey2 = Desire.GetDesireKey(Desire.Type.Eat);
			partner.SetDesire(desireKey2, 0f);
			packData.Release();
			Singleton<Manager.ADV>.Instance.Captions.EndADV();
		};
		packData.restoreCommands = null;
		Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
		while (!wait)
		{
			yield return null;
		}
		partner.Animation.ResetDefaultAnimatorController();
		partner.ActivateNavMeshAgent();
		partner.SetActiveOnEquipedItem(active: true);
		partner.DeactivatePairing(0);
		PlayerController.ChangeState("Normal");
		CameraControl.Mode = CameraMode.Normal;
		CameraControl.RecoverShotType();
		partner.TargetInSightActionPoint = null;
		base.CurrentPoint = null;
		MapUIContainer.SetVisibleHUDExceptStoryUI(active: true);
		MapUIContainer.StorySupportUI.Open();
		Singleton<Manager.Map>.Instance.EnableEntity(partner);
		_exitEatEventEnumerator = null;
	}

	public void StartHizamakuraEvent(AgentActor agent)
	{
		Singleton<Manager.Map>.Instance.DisableEntity(agent);
		agent.BehaviorResources.ChangeMode(Desire.ActionType.Idle);
		PlayerController.ChangeState("Idle");
		SetActiveOnEquipedItem(active: false);
		base.ChaControl.setAllLayerWeight(0f);
		Singleton<Manager.Input>.Instance.ReserveState(Manager.Input.ValidType.UI);
		Singleton<Manager.Input>.Instance.SetupState();
		MapUIContainer.SetVisibleHUDExceptStoryUI(active: false);
		MapUIContainer.StorySupportUI.Close();
		int item = AIProject.Definitions.Action.NameTable[EventType.Break].Item1;
		int hizamakuraPTID = Singleton<Manager.Resources>.Instance.PlayerProfile.HizamakuraPTID;
		base.CurrentPoint.FindPlayerDateActionPointInfo(base.ChaControl.sex, hizamakuraPTID, out var outInfo);
		int num = (base.PoseID = outInfo.poseIDA);
		int key = num;
		Transform t = base.CurrentPoint.transform.FindLoop(outInfo.baseNullNameA)?.transform ?? base.CurrentPoint.transform;
		Transform t2 = base.CurrentPoint.transform.FindLoop(outInfo.baseNullNameB)?.transform ?? base.CurrentPoint.transform;
		GameObject gameObject = base.CurrentPoint.transform.FindLoop(outInfo.recoveryNullNameA);
		Animation.RecoveryPoint = gameObject?.transform;
		GameObject gameObject2 = base.CurrentPoint.transform.FindLoop(outInfo.recoveryNullNameB);
		base.Partner.Animation.RecoveryPoint = gameObject2?.transform;
		PlayState playState = Singleton<Manager.Resources>.Instance.Animation.PlayerActionAnimTable[base.ChaControl.sex][item][key];
		Animation.LoadEventKeyTable(item, outInfo.poseIDA);
		LoadEventItems(playState);
		LoadEventParticles(item, outInfo.poseIDA);
		Animation.InitializeStates(playState);
		Actor partner = base.Partner;
		partner.Animation.LoadEventKeyTable(item, outInfo.poseIDB);
		PlayState playState2 = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[item][outInfo.poseIDB];
		partner.LoadEventItems(playState2);
		partner.LoadEventParticles(item, outInfo.poseIDB);
		partner.Animation.InitializeStates(playState2);
		ActorAnimInfo actorAnimInfo = default(ActorAnimInfo);
		actorAnimInfo.layer = playState.Layer;
		actorAnimInfo.inEnableBlend = playState.MainStateInfo.InStateInfo.EnableFade;
		actorAnimInfo.inBlendSec = playState.MainStateInfo.InStateInfo.FadeSecond;
		actorAnimInfo.outEnableBlend = playState.MainStateInfo.OutStateInfo.EnableFade;
		actorAnimInfo.outBlendSec = playState.MainStateInfo.OutStateInfo.FadeSecond;
		actorAnimInfo.directionType = playState.DirectionType;
		actorAnimInfo.isLoop = playState.MainStateInfo.IsLoop;
		ActorAnimInfo actorAnimInfo2 = actorAnimInfo;
		Animation.AnimInfo = actorAnimInfo2;
		ActorAnimInfo actorAnimInfo3 = actorAnimInfo2;
		actorAnimInfo2 = default(ActorAnimInfo);
		actorAnimInfo2.layer = playState2.Layer;
		actorAnimInfo2.inEnableBlend = playState2.MainStateInfo.InStateInfo.EnableFade;
		actorAnimInfo2.inBlendSec = playState2.MainStateInfo.InStateInfo.FadeSecond;
		actorAnimInfo2.outEnableBlend = playState2.MainStateInfo.OutStateInfo.EnableFade;
		actorAnimInfo2.outBlendSec = playState2.MainStateInfo.OutStateInfo.FadeSecond;
		actorAnimInfo2.directionType = playState2.DirectionType;
		actorAnimInfo2.isLoop = playState2.MainStateInfo.IsLoop;
		actorAnimInfo2.endEnableBlend = false;
		actorAnimInfo2.endBlendSec = 0f;
		actorAnimInfo2.loopMinTime = playState.MainStateInfo.LoopMin;
		actorAnimInfo2.loopMaxTime = playState.MainStateInfo.LoopMax;
		actorAnimInfo2.hasAction = playState.ActionInfo.hasAction;
		ActorAnimInfo actorAnimInfo4 = actorAnimInfo2;
		partner.Animation.AnimInfo = actorAnimInfo4;
		ActorAnimInfo actorAnimInfo5 = actorAnimInfo4;
		DeactivateNavMeshAgent();
		base.IsKinematic = true;
		partner.SetActiveOnEquipedItem(active: false);
		partner.ChaControl.setAllLayerWeight(0f);
		partner.DeactivateNavMeshAgent();
		partner.IsKinematic = true;
		Animation.PlayInAnimation(actorAnimInfo3.inEnableBlend, actorAnimInfo3.inBlendSec, playState.MainStateInfo.FadeOutTime, actorAnimInfo3.layer);
		SetStand(t, playState.MainStateInfo.InStateInfo.EnableFade, playState.MainStateInfo.InStateInfo.FadeSecond, playState.DirectionType);
		partner.Animation.PlayInAnimation(actorAnimInfo3.inEnableBlend, actorAnimInfo3.inBlendSec, playState2.MainStateInfo.FadeOutTime, actorAnimInfo3.layer);
		partner.SetStand(t2, actorAnimInfo5.inEnableBlend, actorAnimInfo5.inBlendSec, actorAnimInfo3.layer);
		if (OldEnabledHoldingHand = HandsHolder.enabled)
		{
			HandsHolder.enabled = false;
			if (HandsHolder.EnabledHolding)
			{
				HandsHolder.EnabledHolding = false;
			}
		}
		CameraControl.SetShotTypeForce(ShotType.Near);
		Observable.EveryLateUpdate().Skip(1).Take(1)
			.Subscribe(delegate
			{
				Manager.ADV.ChangeADVFixedAngleCamera(partner, 6);
			});
		_hizamakuraEventEnumerator = HizamakuraEventCoroutine();
		_hizamakuraEventDisposable = Observable.FromCoroutine(() => _hizamakuraEventEnumerator).Subscribe();
	}

	private IEnumerator HizamakuraEventCoroutine()
	{
		yield return Observable.Timer(TimeSpan.FromMilliseconds(1000.0)).ToYieldInstruction();
		AgentActor partner = AgentPartner;
		OpenData openData = partner.openData;
		openData.FindLoad("0", partner.charaID, 13);
		AgentActor.PackData packData = partner.packData;
		packData.AttitudeID = 0;
		packData.Init();
		bool wait = false;
		packData.onComplete = delegate
		{
			wait = true;
			packData.Release();
			Singleton<Manager.ADV>.Instance.Captions.CanvasGroupOFF();
			Singleton<MapUIContainer>.Instance.advScene.gameObject.SetActive(value: false);
		};
		packData.restoreCommands = null;
		Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
		while (!wait)
		{
			yield return null;
		}
		SetStand(Animation.RecoveryPoint, enableFade: false, 0f, 0);
		partner.SetStand(partner.Animation.RecoveryPoint, enableFade: false, 0f, 0);
		PoseKeyPair idleID = Singleton<Manager.Resources>.Instance.AgentProfile.ADVIdleTable[partner.ChaControl.fileParam.personality];
		PlayState info = Singleton<Manager.Resources>.Instance.Animation.AgentActionAnimTable[idleID.postureID][idleID.poseID];
		AssetBundleInfo abInfo = info.MainStateInfo.AssetBundleInfo;
		partner.ChangeAnimator(abInfo.assetbundle, abInfo.asset);
		Animator animator = partner.Animation.Animator;
		Queue<PlayState.Info> recoveryStates = QueuePool<PlayState.Info>.Get();
		PlayState.Info[] stateInfos = info.MainStateInfo.InStateInfo.StateInfos;
		foreach (PlayState.Info item in stateInfos)
		{
			recoveryStates.Enqueue(item);
		}
		float blendTime = info.MainStateInfo.InStateInfo.FadeSecond;
		while (recoveryStates.Count > 0)
		{
			PlayState.Info state = recoveryStates.Dequeue();
			partner.Animation.PlayAnimation(state.stateName, state.layer, 0f);
			yield return null;
			if (recoveryStates.Count == 0)
			{
				break;
			}
			yield return null;
			AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(state.layer);
			bool isInTransition = animator.IsInTransition(state.layer);
			while (isInTransition || (stateInfo.IsName(state.stateName) && stateInfo.normalizedTime < 1f))
			{
				stateInfo = animator.GetCurrentAnimatorStateInfo(state.layer);
				isInTransition = animator.IsInTransition(state.layer);
				yield return null;
			}
		}
		Vector3 to = base.Position;
		to.y = 0f;
		Vector3 from = partner.Position;
		from.y = 0f;
		Quaternion destRotation = Quaternion.LookRotation(to - from);
		partner.Rotation = destRotation;
		CinemachineBlendDefinition.Style prevStyle = CameraControl.CinemachineBrain.m_DefaultBlend.m_Style;
		CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
		Manager.ADV.ChangeADVCamera(partner);
		Observable.EveryLateUpdate().Skip(1).Take(1)
			.Subscribe(delegate
			{
				CameraControl.CinemachineBrain.m_DefaultBlend.m_Style = prevStyle;
			});
		yield return Observable.Timer(TimeSpan.FromSeconds(2.0)).ToYieldInstruction();
		openData.FindLoad("3", partner.charaID, 13);
		packData.Init();
		wait = false;
		packData.onComplete = delegate
		{
			wait = true;
			int desireKey = Desire.GetDesireKey(Desire.Type.Lonely);
			partner.SetDesire(desireKey, 0f);
			int desireKey2 = Desire.GetDesireKey(Desire.Type.Break);
			partner.SetDesire(desireKey2, 0f);
			packData.Release();
			Singleton<Manager.ADV>.Instance.Captions.EndADV();
		};
		yield return MapUIContainer.FadeCanvas.StartFade(FadeCanvas.PanelType.Blackout, FadeType.Out, 2f, ignoreTimeScale: true).ToYieldInstruction();
		packData.restoreCommands = null;
		Singleton<MapUIContainer>.Instance.OpenADV(openData, packData);
		while (!wait)
		{
			yield return null;
		}
		partner.Animation.ResetDefaultAnimatorController();
		partner.ActivateNavMeshAgent();
		partner.SetActiveOnEquipedItem(active: true);
		partner.DeactivatePairing(0);
		PlayerController.ChangeState("Normal");
		CameraControl.Mode = CameraMode.Normal;
		CameraControl.RecoverShotType();
		partner.TargetInSightActionPoint = null;
		base.CurrentPoint = null;
		MapUIContainer.SetVisibleHUDExceptStoryUI(active: true);
		MapUIContainer.StorySupportUI.Open();
		Singleton<Manager.Map>.Instance.EnableEntity(partner);
		_hizamakuraEventEnumerator = null;
	}

	public void SetScheduledInteractionState(bool isEnabled)
	{
		_scheduledInteractionState = isEnabled;
	}

	public void ReleaseInteraction()
	{
		if (!_scheduledInteractionState.HasValue)
		{
			Debug.Log("ScheduledInteraction was Empty");
			return;
		}
		CurrentInteractionState = _scheduledInteractionState.Value;
		_cameraCtrl.EnabledInput = _scheduledInteractionState.Value;
		_scheduledInteractionState = null;
	}

	protected override void OnStart()
	{
		(from _ in Observable.EveryUpdate().TakeUntilDestroy(base.gameObject)
			where base.isActiveAndEnabled
			select _).Subscribe(delegate
		{
			OnUpdate();
		});
		(from _ in Observable.EveryLateUpdate().TakeUntilDestroy(base.gameObject)
			where base.isActiveAndEnabled
			select _).Subscribe(delegate
		{
			OnLateUpdate();
		});
	}

	private void OnElapsedTimeFromDateSleep()
	{
		AgentActor agentPartner = AgentPartner;
		ActorAnimInfo animInfo = agentPartner.Animation.AnimInfo;
		if (OldEnabledHoldingHand)
		{
			HandsHolder.enabled = false;
			HandsHolder.EnabledHolding = false;
		}
		agentPartner.CurrentPoint = base.CurrentPoint;
		agentPartner.CurrentPoint.SetSlot(agentPartner);
		agentPartner.SetCurrentSchedule(animInfo.isLoop, "睡眠", animInfo.loopMinTime, animInfo.loopMaxTime, animInfo.hasAction, useGameTime: true);
		agentPartner.BehaviorResources.ChangeMode(Desire.ActionType.EndTaskSleepAfterDate);
		agentPartner.Mode = Desire.ActionType.EndTaskSleepAfterDate;
		agentPartner.DisableActionFlag();
		agentPartner.Partner = null;
		base.Partner = null;
	}

	public void StashData()
	{
		Singleton<MapScene>.Instance.SaveProfile(isAuto: true);
		Game.PrevPlayerStateFromCharaCreate = PlayerController.State.GetType().Name;
		Game.PrevAccessDeviceID = CurrentDevicePoint.ID;
		Singleton<Game>.Instance.WorldData.Copy(Singleton<Game>.Instance.Data.AutoData);
		Singleton<Game>.Instance.IsAuto = true;
	}

	public void PlayActionMotion(EventType eventType)
	{
		base.CurrentPoint.TryGetPlayerActionPointInfo(eventType, out var outInfo);
		int num = (base.PoseID = outInfo.poseID);
		int key = num;
		Transform t = base.CurrentPoint.transform.FindLoop(outInfo.baseNullName)?.transform ?? base.CurrentPoint.transform;
		GameObject gameObject = base.CurrentPoint.transform.FindLoop(outInfo.recoveryNullName);
		Animation.RecoveryPoint = gameObject?.transform;
		PlayState playState = Singleton<Manager.Resources>.Instance.Animation.PlayerActionAnimTable[base.ChaControl.sex][outInfo.eventID][key];
		Animation.LoadEventKeyTable(outInfo.eventID, outInfo.poseID);
		LoadEventItems(playState);
		LoadEventParticles(outInfo.eventID, outInfo.poseID);
		Animation.InitializeStates(playState);
		ActorAnimInfo actorAnimInfo = default(ActorAnimInfo);
		actorAnimInfo.layer = playState.Layer;
		actorAnimInfo.inEnableBlend = playState.MainStateInfo.InStateInfo.EnableFade;
		actorAnimInfo.inBlendSec = playState.MainStateInfo.InStateInfo.FadeSecond;
		actorAnimInfo.outEnableBlend = playState.MainStateInfo.OutStateInfo.EnableFade;
		actorAnimInfo.outBlendSec = playState.MainStateInfo.OutStateInfo.FadeSecond;
		actorAnimInfo.directionType = playState.DirectionType;
		actorAnimInfo.isLoop = playState.MainStateInfo.IsLoop;
		actorAnimInfo.endEnableBlend = playState.EndEnableBlend;
		actorAnimInfo.endBlendSec = playState.EndBlendRate;
		ActorAnimInfo actorAnimInfo2 = actorAnimInfo;
		Animation.AnimInfo = actorAnimInfo2;
		ActorAnimInfo actorAnimInfo3 = actorAnimInfo2;
		DeactivateNavMeshAgent();
		base.IsKinematic = true;
		Animation.StopAllAnimCoroutine();
		Animation.PlayInAnimation(actorAnimInfo3.inEnableBlend, actorAnimInfo3.inBlendSec, playState.MainStateInfo.FadeOutTime, actorAnimInfo3.layer);
		SetStand(t, actorAnimInfo3.inEnableBlend, actorAnimInfo3.inBlendSec, actorAnimInfo3.directionType);
	}

	public override IEnumerator LoadAsync()
	{
		yield return LoadCharAsync(PlayerData.CharaFileNames[PlayerData.Sex]);
		LoadEquipments();
		if (_chaBodyParts == null)
		{
			_chaBodyParts = new Dictionary<BodyPart, Transform>();
		}
		GameObject hips = base.ChaControl.animBody.transform.FindLoop("cf_J_Hips");
		_chaBodyParts[BodyPart.Body] = hips?.transform;
		GameObject bust = base.ChaControl.animBody.transform.FindLoop("cf_J_Mune00");
		_chaBodyParts[BodyPart.Bust] = bust?.transform;
		GameObject head = base.ChaControl.animBody.transform.FindLoop("N_Head");
		_chaBodyParts[BodyPart.Head] = head?.transform;
		GameObject lf = base.ChaControl.animBody.transform.FindLoop("cf_J_Foot01_L");
		_chaBodyParts[BodyPart.LeftFoot] = lf?.transform;
		GameObject rf = base.ChaControl.animBody.transform.FindLoop("cf_J_Foot01_R");
		_chaBodyParts[BodyPart.RightFoot] = rf?.transform;
		Animator animator = base.ChaControl.animBody;
		InitializeLabels();
		FullBodyBipedIK ik = animator.GetComponentInChildren<FullBodyBipedIK>(includeInactive: true);
		GameObject ctrlOld = _animation.gameObject;
		ActorAnimationPlayer actorAnimationPlayer = _animation.CloneComponent(animator.gameObject);
		actorAnimationPlayer.IK = ik;
		actorAnimationPlayer.Actor = this;
		actorAnimationPlayer.Character = _character;
		actorAnimationPlayer.Animator = animator;
		_animation = actorAnimationPlayer;
		UnityEngine.Object.Destroy(ctrlOld);
		AssetBundleInfo animABInfo = default(AssetBundleInfo);
		RuntimeAnimatorController rac = Singleton<Manager.Resources>.Instance.Animation.GetPlayerAnimator(base.ChaControl.sex, ref animABInfo);
		Animation.SetDefaultAnimatorController(rac);
		Animation.SetAnimatorController(rac);
		Animation.AnimABInfo = animABInfo;
		animator.Play("Locomotion", 0, 0f);
		_character.CharacterAnimation = _animation;
		Observable.EveryLateUpdate().Take(1).Subscribe(delegate
		{
			_animation.IK.enabled = true;
		});
		if (_lensEffect == null)
		{
			_lensEffect = GetComponentInChildren<AQUAS_LensEffects>(includeInactive: true);
		}
		if ((bool)_lensEffect)
		{
			GameObject waterObject = Singleton<Manager.Map>.Instance.WaterObject;
			if (waterObject != null)
			{
				if (_lensEffect.gameObjects.waterPlanes.Count > 0)
				{
					_lensEffect.gameObjects.waterPlanes[0] = waterObject;
				}
				else
				{
					_lensEffect.gameObjects.waterPlanes.Add(waterObject);
				}
				_lensEffect.SetActive(isActive: true);
			}
		}
		LoadCamera();
		Relocate();
		InitializeIK();
		_controller.StartBehavior();
	}

	public void ReloadChara()
	{
		ActorAnimationPlayer actorAnimationPlayer = _animation.CloneComponent(Controller.gameObject);
		actorAnimationPlayer.Actor = this;
		actorAnimationPlayer.Character = _character;
		_animation = actorAnimationPlayer;
		Singleton<Character>.Instance.DeleteChara(base.ChaControl);
		LoadChara(PlayerData.CharaFileNames[PlayerData.Sex]);
		LoadEquipments();
		ReleaseEquipedEventItem();
		_chaFovTargets = null;
		if (_chaBodyParts == null)
		{
			_chaBodyParts = new Dictionary<BodyPart, Transform>();
		}
		_chaBodyParts.Clear();
		GameObject gameObject = base.ChaControl.animBody.transform.FindLoop("cf_J_Hips");
		_chaBodyParts[BodyPart.Body] = gameObject?.transform;
		GameObject gameObject2 = base.ChaControl.animBody.transform.FindLoop("cf_J_Mune00");
		_chaBodyParts[BodyPart.Bust] = gameObject2?.transform;
		GameObject gameObject3 = base.ChaControl.animBody.transform.FindLoop("N_Head");
		_chaBodyParts[BodyPart.Head] = gameObject3?.transform;
		GameObject gameObject4 = base.ChaControl.animBody.transform.FindLoop("cf_J_Foot01_L");
		_chaBodyParts[BodyPart.LeftFoot] = gameObject4?.transform;
		GameObject gameObject5 = base.ChaControl.animBody.transform.FindLoop("cf_J_Foot01_R");
		_chaBodyParts[BodyPart.RightFoot] = gameObject5?.transform;
		Animator animBody = base.ChaControl.animBody;
		FullBodyBipedIK componentInChildren = animBody.GetComponentInChildren<FullBodyBipedIK>(includeInactive: true);
		ActorAnimationPlayer animation = _animation;
		ActorAnimationPlayer actorAnimationPlayer2 = _animation.CloneComponent(animBody.gameObject);
		actorAnimationPlayer2.IK = componentInChildren;
		actorAnimationPlayer2.Actor = this;
		actorAnimationPlayer2.Character = _character;
		actorAnimationPlayer2.Animator = animBody;
		_animation = actorAnimationPlayer2;
		UnityEngine.Object.Destroy(animation);
		AssetBundleInfo outInfo = default(AssetBundleInfo);
		RuntimeAnimatorController playerAnimator = Singleton<Manager.Resources>.Instance.Animation.GetPlayerAnimator(base.ChaControl.sex, ref outInfo);
		Animation.SetDefaultAnimatorController(playerAnimator);
		Animation.SetAnimatorController(playerAnimator);
		Animation.AnimABInfo = outInfo;
		animBody.Play("Locomotion", 0, 0f);
		_character.CharacterAnimation = _animation;
		Observable.EveryLateUpdate().Take(1).Subscribe(delegate
		{
			_animation.IK.enabled = true;
		});
		InitializeIK();
		UnityEngine.Resources.UnloadUnusedAssets();
		GC.Collect();
	}

	public override void LoadEquipments()
	{
		LoadEquipmentItem(PlayerData.EquipedHeadItem, ChaControlDefine.ExtraAccessoryParts.Head);
		LoadEquipmentItem(PlayerData.EquipedBackItem, ChaControlDefine.ExtraAccessoryParts.Back);
		LoadEquipmentItem(PlayerData.EquipedNeckItem, ChaControlDefine.ExtraAccessoryParts.Neck);
	}

	public IEnumerator LoadTrialAsync()
	{
		yield return LoadCharAsync(PlayerData.CharaFileNames[PlayerData.Sex]);
		Animator animator = base.ChaControl.animBody;
		InitializeLabels();
		FullBodyBipedIK ik = animator.GetComponentInChildren<FullBodyBipedIK>(includeInactive: true);
		GameObject ctrlOld = _animation.gameObject;
		ActorAnimationPlayer actorAnimationPlayer = _animation.CloneComponent(animator.gameObject);
		actorAnimationPlayer.IK = ik;
		actorAnimationPlayer.Actor = this;
		actorAnimationPlayer.Character = _character;
		actorAnimationPlayer.Animator = animator;
		_animation = actorAnimationPlayer;
		UnityEngine.Object.Destroy(ctrlOld);
		AssetBundleInfo animABInfo = default(AssetBundleInfo);
		RuntimeAnimatorController rac = Singleton<Manager.Resources>.Instance.Animation.GetPlayerAnimator(base.ChaControl.sex, ref animABInfo);
		Animation.SetAnimatorController(rac);
		Animation.AnimABInfo = animABInfo;
		animator.Play("Locomotion", 0, 0f);
		_character.CharacterAnimation = _animation;
		Observable.EveryLateUpdate().Take(1).Subscribe(delegate
		{
			_animation.IK.enabled = true;
		});
		if (_lensEffect == null)
		{
			_lensEffect = GetComponentInChildren<AQUAS_LensEffects>(includeInactive: true);
		}
		if ((bool)_lensEffect)
		{
			GameObject waterObject = Singleton<Manager.Map>.Instance.WaterObject;
			if (waterObject != null)
			{
				if (_lensEffect.gameObjects.waterPlanes.Count > 0)
				{
					_lensEffect.gameObjects.waterPlanes[0] = waterObject;
				}
				else
				{
					_lensEffect.gameObjects.waterPlanes.Add(waterObject);
				}
				_lensEffect.SetActive(isActive: true);
			}
		}
		LoadTrialCamera();
		Relocate();
		InitializeIK();
		_controller.StartBehavior();
	}

	public void LoadCamera()
	{
		DefinePack definePack = Singleton<Manager.Resources>.Instance.DefinePack;
		string cameraAdd = definePack.ABPaths.CameraAdd05;
		CommonDefine commonDefine = Singleton<Manager.Resources>.Instance.CommonDefine;
		string add = definePack.ABManifests.Add05;
		LoadCamera(cameraAdd, commonDefine.FileNames.MainCameraName, add);
	}

	public void LoadTrialCamera()
	{
		DefinePack definePack = Singleton<Manager.Resources>.Instance.DefinePack;
		string camera = definePack.ABPaths.Camera;
		CommonDefine commonDefine = Singleton<Manager.Resources>.Instance.CommonDefine;
		string @default = definePack.ABManifests.Default;
		LoadCamera(camera, commonDefine.FileNames.TrialCamera, @default);
	}

	private void LoadCamera(string assetBundle, string asset, string manifest = null)
	{
		GameObject original = CommonLib.LoadAsset<GameObject>(assetBundle, asset, clone: false, manifest);
		GameObject gameObject = UnityEngine.Object.Instantiate(original);
		gameObject.transform.SetParent(base.transform, worldPositionStays: false);
		gameObject.transform.localPosition = Vector3.zero;
		gameObject.transform.localRotation = Quaternion.identity;
		_cameraCtrl = gameObject.GetComponentInChildren<ActorCameraControl>();
		_cameraCtrl.LocomotionSetting = _locomotionSetting;
		CommonDefine commonDefine = Singleton<Manager.Resources>.Instance.CommonDefine;
		GameObject original2 = CommonLib.LoadAsset<GameObject>(assetBundle, commonDefine.FileNames.NormalCameraName, clone: false, manifest);
		GameObject gameObject2 = UnityEngine.Object.Instantiate(original2);
		gameObject2.transform.SetParent(_cameraCtrl.VirtualCameraRoot, worldPositionStays: false);
		gameObject2.transform.localPosition = Vector3.zero;
		gameObject2.transform.localRotation = Quaternion.identity;
		GameObject original3 = CommonLib.LoadAsset<GameObject>(assetBundle, commonDefine.FileNames.ActionCameraFreeLookName, clone: false, manifest);
		GameObject gameObject3 = UnityEngine.Object.Instantiate(original3);
		gameObject3.transform.SetParent(_cameraCtrl.VirtualCameraRoot, worldPositionStays: false);
		gameObject3.transform.localPosition = Vector3.zero;
		gameObject3.transform.localRotation = Quaternion.identity;
		CameraTable component = gameObject2.GetComponent<CameraTable>();
		CameraTable component2 = gameObject3.GetComponent<CameraTable>();
		_cameraCtrl.AssignCameraTable(component, component2);
		GameObject original4 = CommonLib.LoadAsset<GameObject>(assetBundle, commonDefine.FileNames.ActionCameraNotMoveName, clone: false, manifest);
		GameObject gameObject4 = UnityEngine.Object.Instantiate(original4);
		gameObject4.transform.SetParent(_cameraCtrl.VirtualCameraRoot, worldPositionStays: false);
		_cameraCtrl.ActionCameraNotMove = gameObject4.GetComponentInChildren<CinemachineVirtualCamera>();
		_cameraCtrl.ActionCameraNotMove.Follow = _locomotionSetting.ActionFollow;
		GameObject original5 = CommonLib.LoadAsset<GameObject>(assetBundle, commonDefine.FileNames.FishingCamera, clone: false, manifest);
		GameObject gameObject5 = UnityEngine.Object.Instantiate(original5);
		gameObject5.transform.SetParent(_controller.transform, worldPositionStays: false);
		_cameraCtrl.FishingCamera = gameObject5.GetComponent<CinemachineVirtualCamera>();
		_cameraCtrl.SetLensSetting(Singleton<Manager.Resources>.Instance.LocomotionProfile.DefaultLensSetting);
		_cameraCtrl.XAxisValue = base.Rotation.eulerAngles.y;
		_cameraCtrl.YAxisValue = 0.6f;
		_cameraCtrl.enabled = true;
	}

	protected override void InitializeIK()
	{
		base.InitializeIK();
		string assetName = ((base.ChaControl.fileParam.sex != 0) ? "HandsTarget_F" : "HandsTarget_M");
		GameObject original = CommonLib.LoadAsset<GameObject>(Singleton<Manager.Resources>.Instance.DefinePack.ABPaths.ActorPrefab, assetName, clone: false, "abdata");
		GameObject gameObject = UnityEngine.Object.Instantiate(original, base.transform);
		HandsHolder handsHolder = (HandsHolder = gameObject.GetComponent<HandsHolder>());
		HandsHolder handsHolder2 = handsHolder;
		handsHolder2.enabled = false;
		handsHolder2.EnabledHolding = false;
		handsHolder2.LeftHandAnimator = base.ChaControl.animBody;
		handsHolder2.LeftHandIK = base.ChaControl.fullBodyIK;
		GameObject gameObject2 = base.ChaControl.animBody.transform.FindLoop("cf_J_Kosi02");
		handsHolder2.LeftLookTarget = gameObject2.transform;
		if (!Singleton<Manager.Resources>.Instance.LocomotionProfile.HoldingHandTarget.IsNullOrEmpty())
		{
			GameObject gameObject3 = base.ChaControl.animBody.transform.FindLoop(Singleton<Manager.Resources>.Instance.LocomotionProfile.HoldingHandTarget);
			if (gameObject3 != null)
			{
				handsHolder2.RightHandTarget = gameObject3.transform;
			}
		}
	}

	protected override IEnumerator LoadCharAsync(string fileName)
	{
		ChaFileControl chaFile;
		if (!fileName.IsNullOrEmpty())
		{
			chaFile = new ChaFileControl();
			if (!chaFile.LoadCharaFile(fileName, PlayerData.Sex))
			{
				chaFile = null;
			}
		}
		else
		{
			chaFile = null;
			string text = ((PlayerData.Sex != 0) ? "ill_Default_Female" : "ill_Default_Male");
			Dictionary<int, ListInfoBase> dictionary = ((PlayerData.Sex != 0) ? Singleton<Character>.Instance.chaListCtrl.GetCategoryInfo(ChaListDefine.CategoryNo.cha_sample_f) : Singleton<Character>.Instance.chaListCtrl.GetCategoryInfo(ChaListDefine.CategoryNo.cha_sample_m));
			foreach (KeyValuePair<int, ListInfoBase> item in dictionary)
			{
				if (item.Value.GetInfo(ChaListDefine.KeyType.MainData) == text)
				{
					chaFile = new ChaFileControl();
					chaFile.LoadFromAssetBundle(item.Value.GetInfo(ChaListDefine.KeyType.MainAB), text);
					break;
				}
			}
		}
		if (PlayerData.Sex == 0)
		{
			base.ChaControl = Singleton<Character>.Instance.CreateChara(0, base.gameObject, 0, chaFile);
		}
		else
		{
			base.ChaControl = Singleton<Character>.Instance.CreateChara(1, base.gameObject, 0, chaFile);
		}
		base.ChaControl.isPlayer = true;
		base.ChaControl.Load();
		yield return null;
		base.ChaControl.ChangeLookEyesPtn(3);
		base.ChaControl.ChangeLookNeckPtn(3);
		Controller.InitializeFaceLight(base.ChaControl.gameObject);
		yield return null;
	}

	private void LoadChara(string fileName)
	{
		ChaFileControl chaFileControl;
		if (!fileName.IsNullOrEmpty())
		{
			chaFileControl = new ChaFileControl();
			if (!chaFileControl.LoadCharaFile(fileName, PlayerData.Sex))
			{
				chaFileControl = null;
			}
		}
		else
		{
			chaFileControl = null;
			string text = ((PlayerData.Sex != 0) ? "ill_Default_Female" : "ill_Default_Male");
			Dictionary<int, ListInfoBase> dictionary = ((PlayerData.Sex != 0) ? Singleton<Character>.Instance.chaListCtrl.GetCategoryInfo(ChaListDefine.CategoryNo.cha_sample_f) : Singleton<Character>.Instance.chaListCtrl.GetCategoryInfo(ChaListDefine.CategoryNo.cha_sample_m));
			foreach (KeyValuePair<int, ListInfoBase> item in dictionary)
			{
				if (item.Value.GetInfo(ChaListDefine.KeyType.MainData) == text)
				{
					chaFileControl = new ChaFileControl();
					chaFileControl.LoadFromAssetBundle(item.Value.GetInfo(ChaListDefine.KeyType.MainAB), text);
					break;
				}
			}
		}
		if (PlayerData.Sex == 0)
		{
			base.ChaControl = Singleton<Character>.Instance.CreateChara(0, base.gameObject, 0, chaFileControl);
		}
		else
		{
			base.ChaControl = Singleton<Character>.Instance.CreateChara(1, base.gameObject, 0, chaFileControl);
		}
		base.ChaControl.isPlayer = true;
		base.ChaControl.Load();
		base.ChaControl.ChangeLookEyesPtn(3);
		base.ChaControl.ChangeLookNeckPtn(3);
		Controller.InitializeFaceLight(base.ChaControl.gameObject);
	}

	private void OnEnable()
	{
		Animation.enabled = true;
		Locomotor.enabled = true;
		Controller.enabled = true;
	}

	private void OnDisable()
	{
		Animation.enabled = false;
		Locomotor.enabled = false;
		Controller.enabled = false;
	}

	private void OnDestroy()
	{
	}

	public override void EnableEntity()
	{
		CameraControl.enabled = true;
		Animation.enabled = true;
		Controller.enabled = true;
		Locomotor.enabled = true;
		base.ChaControl.visibleAll = true;
	}

	public override void DisableEntity()
	{
		CameraControl.enabled = false;
		Animation.enabled = false;
		Controller.enabled = false;
		Locomotor.enabled = false;
		base.ChaControl.visibleAll = false;
	}

	private void OnUpdate()
	{
		_navMeshObstacle.transform.position = base.Position;
		_coolTime += Time.deltaTime;
		PlayerData playerData = PlayerData;
		if (_called)
		{
			_callElapsedTime += Time.unscaledDeltaTime;
			if (_callElapsedTime > 2f)
			{
				_called = false;
				_callElapsedTime = 0f;
			}
		}
		if (_schedule.enabled && !_schedule.useGameTime && _schedule.progress)
		{
			_schedule.elapsedTime += Time.deltaTime;
			if (_schedule.elapsedTime > _schedule.duration)
			{
				_schedule.enabled = false;
			}
		}
		if (!_scaleCtrlInfos.IsNullOrEmpty())
		{
			foreach (ItemScaleInfo scaleCtrlInfo in _scaleCtrlInfos)
			{
				if (scaleCtrlInfo.ScaleMode == 0)
				{
					float shapeBodyValue = base.ChaControl.GetShapeBodyValue(0);
					float num = scaleCtrlInfo.Evaluate(shapeBodyValue);
					scaleCtrlInfo.TargetItem.transform.localScale = new Vector3(num, num, num);
				}
			}
		}
		StatusProfile statusProfile = Singleton<Manager.Resources>.Instance.StatusProfile;
		EnvironmentSimulator simulator = Singleton<Manager.Map>.Instance.Simulator;
		if (simulator.EnabledTimeProgression)
		{
			Weather weather = simulator.Weather;
			if (base.AreaType == MapArea.AreaType.Indoor)
			{
				playerData.Wetness += statusProfile.DrySpeed * Time.deltaTime;
			}
			else
			{
				switch (weather)
				{
				case Weather.Rain:
					playerData.Wetness += statusProfile.WetRateInRain * Time.deltaTime;
					break;
				case Weather.Storm:
					playerData.Wetness += statusProfile.WetRateInStorm * Time.deltaTime;
					break;
				default:
					playerData.Wetness += statusProfile.DrySpeed * Time.deltaTime;
					break;
				}
				playerData.Wetness = Mathf.Clamp(playerData.Wetness, 0f, 100f);
			}
		}
		if (base.ChaControl != null)
		{
			float wetRate = Mathf.InverseLerp(0f, 100f, playerData.Wetness);
			base.ChaControl.wetRate = wetRate;
		}
		playerData.Position = base.Position;
		playerData.Rotation = base.Rotation;
		playerData.ChunkID = base.ChunkID;
		if (_mapAreaID != null && base.MapArea != null)
		{
			_mapAreaID.Value = base.MapArea.AreaID;
			PlayerData.AreaID = base.MapArea.AreaID;
		}
		if (HandsHolder != null)
		{
			HandsHolder.BaseTransform.position = base.Position;
			HandsHolder.BaseTransform.rotation = base.Rotation;
		}
	}

	private void OnLateUpdate()
	{
		if (!(_cameraCtrl != null) || !_cameraCtrl.enabled)
		{
			return;
		}
		if (!(Controller.State is Communication) && !(Controller.State is Sex) && !(Controller.State is Fishing))
		{
			if (_cameraCtrl.Mode != CameraMode.ADV && _cameraCtrl.Mode != CameraMode.Fishing && _cameraCtrl.Mode != CameraMode.Event)
			{
				bool flag = true;
				flag &= _cameraCtrl.ShotType != ShotType.PointOfView;
				if (flag)
				{
					flag = ((Controller.State is AIProject.Player.Move) ? (flag & IsVisibleDistance(_cameraCtrl.transform, BodyPart.Head, Singleton<Manager.Resources>.Instance.LocomotionProfile.CharaVisibleDistance)) : (flag & IsVisibleDistanceAll(_cameraCtrl.transform)));
				}
				base.ChaControl.visibleAll = base.IsVisible && flag;
				if (Mode == Desire.ActionType.Onbu && base.Partner != null)
				{
					base.Partner.IsVisible = base.IsVisible && flag;
				}
			}
		}
		else if (Controller.State is Fishing)
		{
			bool flag2 = IsVisibleDistanceAll(_cameraCtrl.transform);
			base.ChaControl.visibleAll = base.IsVisible && flag2;
		}
	}

	public void ActivateTransfer()
	{
		EquipEventItemInfo itemInfo = null;
		LoadLocomotionAnimation(out var info, ref itemInfo);
		ResetEquipEventItem(itemInfo);
		ActorAnimation animation = Animation;
		AnimatorStateInfo currentAnimatorStateInfo = Animation.Animator.GetCurrentAnimatorStateInfo(0);
		if (info != null)
		{
			animation.InitializeStates(info);
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
				Animation.InStates.Clear();
				Animation.OutStates.Clear();
				Animation.ActionStates.Clear();
				return;
			}
			int layer = info.Layer;
			if (animation.RefsActAnimInfo)
			{
				animation.StopAllAnimCoroutine();
				animation.PlayInLocoAnimation(animation.AnimInfo.endEnableBlend, animation.AnimInfo.endBlendSec, layer);
				animation.RefsActAnimInfo = false;
			}
			else
			{
				bool enableFade = info.MainStateInfo.InStateInfo.EnableFade;
				float fadeSecond = info.MainStateInfo.InStateInfo.FadeSecond;
				animation.StopAllAnimCoroutine();
				animation.PlayInLocoAnimation(enableFade, fadeSecond, layer);
			}
			return;
		}
		for (int k = 1; k < animation.Animator.layerCount; k++)
		{
			animation.Animator.SetLayerWeight(k, 0f);
		}
		string normalLocoStateName = Singleton<Manager.Resources>.Instance.PlayerProfile.PoseIDData.NormalLocoStateName;
		int num = Animator.StringToHash(normalLocoStateName);
		if (currentAnimatorStateInfo.shortNameHash != num)
		{
			animation.StopAllAnimCoroutine();
			if (animation.AnimInfo.endEnableBlend)
			{
				Animation.CrossFadeAnimation(normalLocoStateName, animation.AnimInfo.outBlendSec, 0, 0f);
				return;
			}
			CameraControl.CrossFade.FadeStart();
			Animation.PlayAnimation(normalLocoStateName, 0, 0f);
		}
	}

	private void LoadLocomotionAnimation(out PlayState info, ref EquipEventItemInfo itemInfo)
	{
		Manager.Resources instance = Singleton<Manager.Resources>.Instance;
		LocomotionProfile locomotionProfile = instance.LocomotionProfile;
		PlayerProfile playerProfile = instance.PlayerProfile;
		StuffItem equipedLampItem = PlayerData.EquipedLampItem;
		CommonDefine.ItemIDDefines itemIDDefine = instance.CommonDefine.ItemIDDefine;
		if (equipedLampItem != null)
		{
			ItemIDKeyPair torchID = itemIDDefine.TorchID;
			ItemIDKeyPair flashlightID = itemIDDefine.FlashlightID;
			ItemIDKeyPair maleLampID = itemIDDefine.MaleLampID;
			if (equipedLampItem.CategoryID == torchID.categoryID && equipedLampItem.ID == torchID.itemID)
			{
				info = instance.Animation.PlayerLocomotionStateTable[base.ChaControl.sex][playerProfile.PoseIDData.TorchLocoID];
				itemInfo = instance.GameInfo.CommonEquipEventItemTable[torchID.categoryID][torchID.itemID];
				itemInfo.ParentName = instance.LocomotionProfile.PlayerLocoItemParentName;
				return;
			}
			if (equipedLampItem.CategoryID == flashlightID.categoryID && equipedLampItem.ID == flashlightID.itemID)
			{
				info = instance.Animation.PlayerLocomotionStateTable[base.ChaControl.sex][playerProfile.PoseIDData.TorchLocoID];
				itemInfo = instance.GameInfo.CommonEquipEventItemTable[flashlightID.categoryID][flashlightID.itemID];
				itemInfo.ParentName = instance.LocomotionProfile.PlayerLocoItemParentName;
				return;
			}
			if (equipedLampItem.CategoryID == maleLampID.categoryID && equipedLampItem.ID == maleLampID.itemID)
			{
				info = instance.Animation.PlayerLocomotionStateTable[base.ChaControl.sex][playerProfile.PoseIDData.LampLocoID];
				itemInfo = instance.GameInfo.CommonEquipEventItemTable[maleLampID.categoryID][maleLampID.itemID];
				itemInfo.ParentName = instance.LocomotionProfile.PlayerLocoItemParentName;
				return;
			}
		}
		info = null;
	}

	public override void OnMinuteUpdated(TimeSpan deltaTime)
	{
		_elapsedTimeInSleep += deltaTime;
		if (_schedule.enabled && _schedule.useGameTime && _schedule.progress)
		{
			_schedule.elapsedTime += (float)deltaTime.TotalMinutes;
			if (_schedule.elapsedTime > _schedule.duration)
			{
				_schedule.enabled = false;
			}
		}
	}

	public void ReleaseCurrentPoint()
	{
		if (base.CurrentPoint != null)
		{
			SetDefaultStateHousingItem();
			ActionPoint currentPoint = base.CurrentPoint;
			base.CurrentPoint = null;
			CommandArea commandArea = PlayerController.CommandArea;
			commandArea.RemoveConsiderationObject(currentPoint);
			commandArea.RefreshCommands();
		}
	}

	public override bool CanAddItem(StuffItem sourceItem)
	{
		if (PlayerData.ItemList.Count >= PlayerData.InventorySlotMax)
		{
			foreach (StuffItem item in PlayerData.ItemList)
			{
				if (item.CategoryID == sourceItem.CategoryID && item.ID == sourceItem.ID && item.Count + sourceItem.Count < Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.ItemStackUpperLimit)
				{
					return true;
				}
			}
			return false;
		}
		return true;
	}

	public override bool CanAddItem(StuffItemInfo item)
	{
		if (PlayerData.ItemList.Count >= PlayerData.InventorySlotMax)
		{
			foreach (StuffItem item2 in PlayerData.ItemList)
			{
				if (item2.CategoryID == item.CategoryID && item2.ID == item.ID && item2.Count < Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.ItemStackUpperLimit)
				{
					return true;
				}
			}
			return false;
		}
		return true;
	}

	protected override void LoadEquipedEventItem(EquipEventItemInfo eventItemInfo)
	{
		AssetBundleInfo assetbundleInfo = eventItemInfo.ActionItemInfo.assetbundleInfo;
		if (assetbundleInfo.assetbundle.IsNullOrEmpty() || assetbundleInfo.asset.IsNullOrEmpty() || assetbundleInfo.manifest.IsNullOrEmpty())
		{
			return;
		}
		GameObject gameObject = CommonLib.LoadAsset<GameObject>(assetbundleInfo.assetbundle, assetbundleInfo.asset, clone: false, assetbundleInfo.manifest);
		if (gameObject != null)
		{
			string playerLocoItemParentName = Singleton<Manager.Resources>.Instance.LocomotionProfile.PlayerLocoItemParentName;
			GameObject gameObject2 = base.ChaControl.animBody.transform.FindLoop(playerLocoItemParentName);
			if (gameObject2 != null)
			{
				GameObject gameObject3 = UnityEngine.Object.Instantiate(gameObject, gameObject2.transform, worldPositionStays: false);
				gameObject3.gameObject.name = gameObject.gameObject.name;
				gameObject3.transform.localPosition = Vector3.zero;
				gameObject3.transform.localRotation = Quaternion.identity;
				gameObject3.transform.localScale = Vector3.one;
				gameObject3.SetActive(value: true);
				base.EquipedItem = new ItemCache
				{
					EventItemID = eventItemInfo.EventItemID,
					KeyInfo = eventItemInfo.ActionItemInfo,
					AsGameObject = gameObject3
				};
				if (eventItemInfo.ActionItemInfo.existsAnimation)
				{
					Animator component = gameObject3.GetComponent<Animator>();
					RuntimeAnimatorController runtimeAnimatorController = AssetUtility.LoadAsset<RuntimeAnimatorController>(eventItemInfo.ActionItemInfo.animeAssetBundle);
					component.runtimeAnimatorController = runtimeAnimatorController;
					Animation.ItemAnimatorTable[gameObject3.GetInstanceID()] = new ItemAnimInfo
					{
						Animator = component,
						Parameters = component.parameters,
						Sync = true
					};
				}
			}
		}
		else
		{
			Debug.LogError($"イベントアイテム読み込み失敗： バンドルパス[{assetbundleInfo.assetbundle}] プレハブ[{assetbundleInfo.asset}] マニフェスト[{assetbundleInfo.manifest}]");
		}
	}

	public override void LoadEventItems(PlayState playState)
	{
		if (playState.ItemInfoCount <= 0)
		{
			return;
		}
		for (int i = 0; i < playState.ItemInfoCount; i++)
		{
			PlayState.ItemInfo itemInfo = playState.GetItemInfo(i);
			Manager.Resources instance = Singleton<Manager.Resources>.Instance;
			ActionItemInfo value3;
			if (itemInfo.fromEquipedItem)
			{
				if (base.CurrentPoint.TryGetPlayerActionPointInfo(base.EventKey, out var outInfo) && instance.GameInfo.SearchEquipEventItemTable.TryGetValue(outInfo.searchAreaID, out var value) && value.TryGetValue(outInfo.gradeValue, out var value2))
				{
					LoadEventItem(value2.EventItemID, itemInfo, value2.ActionItemInfo);
				}
			}
			else if (instance.Map.EventItemList.TryGetValue(itemInfo.itemID, out value3))
			{
				LoadEventItem(itemInfo.itemID, itemInfo, value3);
			}
		}
	}

	public override void LoadEventParticles(int eventID, int poseID)
	{
		if (Singleton<Manager.Resources>.Instance.Animation.PlayerActParticleEventKeyTable.TryGetValue(base.ChaControl.sex, out var value) && value.TryGetValue(eventID, out var value2) && value2.TryGetValue(poseID, out var value3) && value3 != null)
		{
			LoadEventParticle(value3);
		}
	}

	public void AddTutorialUI(Popup.Tutorial.Type type, bool groupDisplay)
	{
		Dictionary<int, bool> dictionary = ((!Singleton<Game>.IsInstance()) ? null : Singleton<Game>.Instance.WorldData)?.TutorialOpenStateTable;
		if (dictionary != null)
		{
			bool value = false;
			if (!dictionary.TryGetValue((int)type, out value))
			{
				value = (dictionary[(int)type] = false);
			}
			if (value)
			{
				return;
			}
			if (!value)
			{
				dictionary[(int)type] = true;
			}
		}
		TutorialIndexList.Add(new UnityEx.ValueTuple<Popup.Tutorial.Type, bool>(type, groupDisplay));
	}
}
