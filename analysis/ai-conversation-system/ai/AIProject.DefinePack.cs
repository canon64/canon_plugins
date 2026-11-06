using System;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AIProject;

public class DefinePack : SerializedScriptableObject
{
	[Serializable]
	public class AssetBundleManifestsDefine
	{
		[SerializeField]
		private string _default = string.Empty;

		[SerializeField]
		private string _add05 = string.Empty;

		[SerializeField]
		private string _add07 = string.Empty;

		[SerializeField]
		private string _add12 = string.Empty;

		public string Default
		{
			[CompilerGenerated]
			get
			{
				return _default;
			}
		}

		public string Add05
		{
			[CompilerGenerated]
			get
			{
				return _add05;
			}
		}

		public string Add07
		{
			[CompilerGenerated]
			get
			{
				return _add07;
			}
		}

		public string Add12
		{
			[CompilerGenerated]
			get
			{
				return _add12;
			}
		}
	}

	[Serializable]
	public class AssetBundleDirectoriesDefine
	{
		[SerializeField]
		private string _inputIconList = string.Empty;

		[SerializeField]
		private string _actionIconList = string.Empty;

		[SerializeField]
		private string _actorIconList = string.Empty;

		[SerializeField]
		private string _weatherIconList = string.Empty;

		[SerializeField]
		private string _equipItemIconList = string.Empty;

		[SerializeField]
		private string _statusIconList = string.Empty;

		[SerializeField]
		private string _sickIconList = string.Empty;

		[SerializeField]
		private string _loadingSpriteList;

		[SerializeField]
		private string _searchEquipItemObjList = string.Empty;

		[SerializeField]
		private string _commonEquipItemObjList = string.Empty;

		[SerializeField]
		private string _accessoryItem = string.Empty;

		[SerializeField]
		private string _actionPointPrefabList = string.Empty;

		[SerializeField]
		private string _basePointPrefabList = string.Empty;

		[SerializeField]
		private string _devicePointPrefabList = string.Empty;

		[SerializeField]
		private string _farmPointPrefabList = string.Empty;

		[SerializeField]
		private string _shipPointPrefabList = string.Empty;

		[SerializeField]
		private string _playerActionPointList = string.Empty;

		[SerializeField]
		private string _agentActionPointList = string.Empty;

		[SerializeField]
		private string _merchantActionPointList = string.Empty;

		[SerializeField]
		private string _lightSwitchPointList = string.Empty;

		[SerializeField]
		private string _eventPointList = string.Empty;

		[SerializeField]
		private string _storyPointList = string.Empty;

		[SerializeField]
		private string _mapList = string.Empty;

		[SerializeField]
		private string _chunkList = string.Empty;

		[SerializeField]
		private string _waypointList = string.Empty;

		[SerializeField]
		private string _AreaGroupList = string.Empty;

		[SerializeField]
		private string _plantItemList = string.Empty;

		[SerializeField]
		private string _ivyFilterList = string.Empty;

		[SerializeField]
		private string _eventItemList = string.Empty;

		[SerializeField]
		private string _eventParticleList = string.Empty;

		[SerializeField]
		private string _popupInfoList = string.Empty;

		[SerializeField]
		private string _areaOpenStateList = string.Empty;

		[SerializeField]
		private string _timeRelationInfoList = string.Empty;

		[SerializeField]
		private string _foodInfo = string.Empty;

		[SerializeField]
		private string _drinkInfo = string.Empty;

		[SerializeField]
		private string _itemList = string.Empty;

		[SerializeField]
		private string _gatheringTable = string.Empty;

		[SerializeField]
		private string _frogItemTable = string.Empty;

		[SerializeField]
		private string _comCamera = string.Empty;

		[SerializeField]
		private string _mapActionVoiceInfoList = string.Empty;

		[SerializeField]
		private string _defaultFemaleFootStepSEInfoList = string.Empty;

		[SerializeField]
		private string _defaultMaleFootStepSEInfoList = string.Empty;

		[SerializeField]
		private string _mapBGMInfoList = string.Empty;

		[SerializeField]
		private string _enviroSEInfoList = string.Empty;

		[SerializeField]
		private string _actorAnimatorList = string.Empty;

		[SerializeField]
		private string _playerFemaleAnimeInfo = string.Empty;

		[SerializeField]
		private string _playerMaleAnimeInfo = string.Empty;

		[SerializeField]
		private string _agentPhase = string.Empty;

		[SerializeField]
		private string _agentPersonalityMotivation = string.Empty;

		[SerializeField]
		private string _lifestyleTable = string.Empty;

		[SerializeField]
		private string _flavorPickSkillTable = string.Empty;

		[SerializeField]
		private string _flavorPickHSkillTable = string.Empty;

		[SerializeField]
		private string _agentDesire = string.Empty;

		[SerializeField]
		private string _agentCommunicationFlags = string.Empty;

		[SerializeField]
		private string _agentAnimeInfo = string.Empty;

		[SerializeField]
		private string _agentLocomotionBreath = string.Empty;

		[SerializeField]
		private string _gravurePoseInfo = string.Empty;

		[SerializeField]
		private string _surpriseItemInfo = string.Empty;

		[SerializeField]
		private string _agentActionResult = string.Empty;

		[SerializeField]
		private string _agentSituationResult = string.Empty;

		[SerializeField]
		private string _actorVanishList = string.Empty;

		[SerializeField]
		private string _behaviorTree = string.Empty;

		[SerializeField]
		private string _tutrialBehaviorTree = string.Empty;

		[SerializeField]
		private string _merchantAnimeInfo = string.Empty;

		[SerializeField]
		private string _merchantBehaviorTree = string.Empty;

		[SerializeField]
		private string _mapIKList = string.Empty;

		[SerializeField]
		private string _minimapIconNameList = string.Empty;

		[SerializeField]
		private string _vanishCameraList = string.Empty;

		[SerializeField]
		private string _expList = string.Empty;

		[SerializeField]
		private string _actionExpList = string.Empty;

		[SerializeField]
		private string _actionExpKeyFrameList = string.Empty;

		[SerializeField]
		private string _actionBustCtrlList = string.Empty;

		[SerializeField]
		private string _actionCameraData = string.Empty;

		[SerializeField]
		private string _actionByproductList = string.Empty;

		[SerializeField]
		private string _enviroInfoList = string.Empty;

		[SerializeField]
		private string _recyclingInfoList = string.Empty;

		public string InputIconList
		{
			[CompilerGenerated]
			get
			{
				return _inputIconList;
			}
		}

		public string ActionIconList
		{
			[CompilerGenerated]
			get
			{
				return _actionIconList;
			}
		}

		public string ActorIconList
		{
			[CompilerGenerated]
			get
			{
				return _actorIconList;
			}
		}

		public string WeatherIconList
		{
			[CompilerGenerated]
			get
			{
				return _weatherIconList;
			}
		}

		public string EquipItemIconList
		{
			[CompilerGenerated]
			get
			{
				return _equipItemIconList;
			}
		}

		public string StatusIconList
		{
			[CompilerGenerated]
			get
			{
				return _statusIconList;
			}
		}

		public string SickIconList
		{
			[CompilerGenerated]
			get
			{
				return _sickIconList;
			}
		}

		public string LoadingSpriteList
		{
			[CompilerGenerated]
			get
			{
				return _loadingSpriteList;
			}
		}

		public string SearchEquipItemObjList
		{
			[CompilerGenerated]
			get
			{
				return _searchEquipItemObjList;
			}
		}

		public string CommonEquipItemObjList
		{
			[CompilerGenerated]
			get
			{
				return _commonEquipItemObjList;
			}
		}

		public string AccessoryItem
		{
			[CompilerGenerated]
			get
			{
				return _accessoryItem;
			}
		}

		public string ActionPointPrefabList
		{
			[CompilerGenerated]
			get
			{
				return _actionPointPrefabList;
			}
		}

		public string BasePointPrefabList
		{
			[CompilerGenerated]
			get
			{
				return _basePointPrefabList;
			}
		}

		public string DevicePointPrefabList
		{
			[CompilerGenerated]
			get
			{
				return _devicePointPrefabList;
			}
		}

		public string FarmPointPrefabList
		{
			[CompilerGenerated]
			get
			{
				return _farmPointPrefabList;
			}
		}

		public string ShipPointPrefabList
		{
			[CompilerGenerated]
			get
			{
				return _shipPointPrefabList;
			}
		}

		public string PlayerActionPointList
		{
			[CompilerGenerated]
			get
			{
				return _playerActionPointList;
			}
		}

		public string AgentActionPointList
		{
			[CompilerGenerated]
			get
			{
				return _agentActionPointList;
			}
		}

		public string MerchantActionPointList
		{
			[CompilerGenerated]
			get
			{
				return _merchantActionPointList;
			}
		}

		public string LightSwitchPointList
		{
			[CompilerGenerated]
			get
			{
				return _lightSwitchPointList;
			}
		}

		public string EventPointList
		{
			[CompilerGenerated]
			get
			{
				return _eventPointList;
			}
		}

		public string StoryPointList
		{
			[CompilerGenerated]
			get
			{
				return _storyPointList;
			}
		}

		public string MapList
		{
			[CompilerGenerated]
			get
			{
				return _mapList;
			}
		}

		public string ChunkList
		{
			[CompilerGenerated]
			get
			{
				return _chunkList;
			}
		}

		public string WaypointList
		{
			[CompilerGenerated]
			get
			{
				return _waypointList;
			}
		}

		public string AreaGroupList
		{
			[CompilerGenerated]
			get
			{
				return _AreaGroupList;
			}
		}

		public string PlantItemList
		{
			[CompilerGenerated]
			get
			{
				return _plantItemList;
			}
		}

		public string IvyFilterList
		{
			[CompilerGenerated]
			get
			{
				return _ivyFilterList;
			}
		}

		public string EventItemList
		{
			[CompilerGenerated]
			get
			{
				return _eventItemList;
			}
		}

		public string EventParticleList
		{
			[CompilerGenerated]
			get
			{
				return _eventParticleList;
			}
		}

		public string PopupInfoList
		{
			[CompilerGenerated]
			get
			{
				return _popupInfoList;
			}
		}

		public string AreaOpenStateList
		{
			[CompilerGenerated]
			get
			{
				return _areaOpenStateList;
			}
		}

		public string TimeRelationInfoList
		{
			[CompilerGenerated]
			get
			{
				return _timeRelationInfoList;
			}
		}

		public string FoodInfo
		{
			[CompilerGenerated]
			get
			{
				return _foodInfo;
			}
		}

		public string DrinkInfo
		{
			[CompilerGenerated]
			get
			{
				return _drinkInfo;
			}
		}

		public string ItemList
		{
			[CompilerGenerated]
			get
			{
				return _itemList;
			}
		}

		public string GatheringTable
		{
			[CompilerGenerated]
			get
			{
				return _gatheringTable;
			}
		}

		public string FrogItemTable
		{
			[CompilerGenerated]
			get
			{
				return _frogItemTable;
			}
		}

		public string ComCamera
		{
			[CompilerGenerated]
			get
			{
				return _comCamera;
			}
		}

		public string MapActionVoiceInfoList
		{
			[CompilerGenerated]
			get
			{
				return _mapActionVoiceInfoList;
			}
		}

		public string DefaultFemaleFootStepSEInfoList
		{
			[CompilerGenerated]
			get
			{
				return _defaultFemaleFootStepSEInfoList;
			}
		}

		public string DefaultMaleFootStepSEInfoList
		{
			[CompilerGenerated]
			get
			{
				return _defaultMaleFootStepSEInfoList;
			}
		}

		public string MapBGMInfoList
		{
			[CompilerGenerated]
			get
			{
				return _mapBGMInfoList;
			}
		}

		public string EnviroSEInfoList
		{
			[CompilerGenerated]
			get
			{
				return _enviroSEInfoList;
			}
		}

		public string ActorAnimatorList
		{
			[CompilerGenerated]
			get
			{
				return _actorAnimatorList;
			}
		}

		public string PlayerFemaleAnimeInfo
		{
			[CompilerGenerated]
			get
			{
				return _playerFemaleAnimeInfo;
			}
		}

		public string PlayerMaleAnimeInfo
		{
			[CompilerGenerated]
			get
			{
				return _playerMaleAnimeInfo;
			}
		}

		public string AgentPhase
		{
			[CompilerGenerated]
			get
			{
				return _agentPhase;
			}
		}

		public string AgentPersonalityMotivation
		{
			[CompilerGenerated]
			get
			{
				return _agentPersonalityMotivation;
			}
		}

		public string LifestyleTable
		{
			[CompilerGenerated]
			get
			{
				return _lifestyleTable;
			}
		}

		public string FlavorPickSkillTable
		{
			[CompilerGenerated]
			get
			{
				return _flavorPickSkillTable;
			}
		}

		public string FlavorPickHSkillTable
		{
			[CompilerGenerated]
			get
			{
				return _flavorPickHSkillTable;
			}
		}

		public string AgentDesire
		{
			[CompilerGenerated]
			get
			{
				return _agentDesire;
			}
		}

		public string AgentCommunicationFlags
		{
			[CompilerGenerated]
			get
			{
				return _agentCommunicationFlags;
			}
		}

		public string AgentAnimeInfo
		{
			[CompilerGenerated]
			get
			{
				return _agentAnimeInfo;
			}
		}

		public string AgentLocomotionBreath
		{
			[CompilerGenerated]
			get
			{
				return _agentLocomotionBreath;
			}
		}

		public string GravurePoseInfo
		{
			[CompilerGenerated]
			get
			{
				return _gravurePoseInfo;
			}
		}

		public string SurpriseItemInfo
		{
			[CompilerGenerated]
			get
			{
				return _surpriseItemInfo;
			}
		}

		public string AgentActionResult
		{
			[CompilerGenerated]
			get
			{
				return _agentActionResult;
			}
		}

		public string AgentSituationResult
		{
			[CompilerGenerated]
			get
			{
				return _agentSituationResult;
			}
		}

		public string ActorVanishList
		{
			[CompilerGenerated]
			get
			{
				return _actorVanishList;
			}
		}

		public string BehaviorTree
		{
			[CompilerGenerated]
			get
			{
				return _behaviorTree;
			}
		}

		public string TutorialBehaviorTree
		{
			[CompilerGenerated]
			get
			{
				return _tutrialBehaviorTree;
			}
		}

		public string MerchantAnimeInfo
		{
			[CompilerGenerated]
			get
			{
				return _merchantAnimeInfo;
			}
		}

		public string MerchantBehaviorTree
		{
			[CompilerGenerated]
			get
			{
				return _merchantBehaviorTree;
			}
		}

		public string MapIKList
		{
			[CompilerGenerated]
			get
			{
				return _mapIKList;
			}
		}

		public string MinimapIconNameList
		{
			[CompilerGenerated]
			get
			{
				return _minimapIconNameList;
			}
		}

		public string VanishCameraList
		{
			[CompilerGenerated]
			get
			{
				return _vanishCameraList;
			}
		}

		public string ExpList
		{
			[CompilerGenerated]
			get
			{
				return _expList;
			}
		}

		public string ActionExpList
		{
			[CompilerGenerated]
			get
			{
				return _actionExpList;
			}
		}

		public string ActionExpKeyFrameList
		{
			[CompilerGenerated]
			get
			{
				return _actionExpKeyFrameList;
			}
		}

		public string ActionBustCtrlList
		{
			[CompilerGenerated]
			get
			{
				return _actionBustCtrlList;
			}
		}

		public string ActionCameraData
		{
			[CompilerGenerated]
			get
			{
				return _actionCameraData;
			}
		}

		public string ActionByproductList
		{
			[CompilerGenerated]
			get
			{
				return _actionByproductList;
			}
		}

		public string EnviroInfoList
		{
			[CompilerGenerated]
			get
			{
				return _enviroInfoList;
			}
		}

		public string RecyclingInfoList
		{
			[CompilerGenerated]
			get
			{
				return _recyclingInfoList;
			}
		}
	}

	[Serializable]
	public class AssetBundlePathsDefine
	{
		[SerializeField]
		private string _mapScene = string.Empty;

		[SerializeField]
		private string _mapScenePrefab = string.Empty;

		[SerializeField]
		private string _mapScenePrefabAdd05 = string.Empty;

		[SerializeField]
		private string _mapScenePrefabAdd07 = string.Empty;

		[SerializeField]
		private string _mapScenePrefabAdd12 = string.Empty;

		[SerializeField]
		private string _mapDebug = string.Empty;

		[SerializeField]
		private string _actorPrefab = string.Empty;

		[SerializeField]
		private string _agent = string.Empty;

		[SerializeField]
		private string _camera = string.Empty;

		[SerializeField]
		private string _cameraAdd05 = string.Empty;

		public string MapScene
		{
			[CompilerGenerated]
			get
			{
				return _mapScene;
			}
		}

		public string MapScenePrefab
		{
			[CompilerGenerated]
			get
			{
				return _mapScenePrefab;
			}
		}

		public string MapScenePrefabAdd05
		{
			[CompilerGenerated]
			get
			{
				return _mapScenePrefabAdd05;
			}
		}

		public string MapScenePrefabAdd07
		{
			[CompilerGenerated]
			get
			{
				return _mapScenePrefabAdd07;
			}
		}

		public string MapScenePrefabAdd12
		{
			[CompilerGenerated]
			get
			{
				return _mapScenePrefabAdd12;
			}
		}

		public string MapDebug
		{
			[CompilerGenerated]
			get
			{
				return _mapDebug;
			}
		}

		public string ActorPrefab
		{
			[CompilerGenerated]
			get
			{
				return _actorPrefab;
			}
		}

		public string Agent
		{
			[CompilerGenerated]
			get
			{
				return _agent;
			}
		}

		public string Camera
		{
			[CompilerGenerated]
			get
			{
				return _camera;
			}
		}

		public string CameraAdd05
		{
			[CompilerGenerated]
			get
			{
				return _cameraAdd05;
			}
		}
	}

	[Serializable]
	public class SceneNameGroup
	{
		[SerializeField]
		private string _logoScene = string.Empty;

		[SerializeField]
		private string _titleScene = string.Empty;

		[SerializeField]
		private string _mapScene = string.Empty;

		[SerializeField]
		private string _mapUIScene = string.Empty;

		[SerializeField]
		private string _hScene = string.Empty;

		[SerializeField]
		private string _mapShortcutScene = string.Empty;

		[SerializeField]
		private string _configScene = string.Empty;

		[SerializeField]
		private string _dialogScene = string.Empty;

		[SerializeField]
		private string _exitScene = string.Empty;

		public string LogoScene
		{
			[CompilerGenerated]
			get
			{
				return _logoScene;
			}
		}

		public string TitleScene
		{
			[CompilerGenerated]
			get
			{
				return _titleScene;
			}
		}

		public string MapScene
		{
			[CompilerGenerated]
			get
			{
				return _mapScene;
			}
		}

		public string MapUIScene
		{
			[CompilerGenerated]
			get
			{
				return _mapUIScene;
			}
		}

		public string HScene
		{
			[CompilerGenerated]
			get
			{
				return _hScene;
			}
		}

		public string MapShortcutScene
		{
			[CompilerGenerated]
			get
			{
				return _mapShortcutScene;
			}
		}

		public string ConfigScene
		{
			[CompilerGenerated]
			get
			{
				return _configScene;
			}
		}

		public string DialogScene
		{
			[CompilerGenerated]
			get
			{
				return _dialogScene;
			}
		}

		public string ExitScene
		{
			[CompilerGenerated]
			get
			{
				return _exitScene;
			}
		}
	}

	[Serializable]
	public struct PlayerDefine
	{
		public string walk;

		public string run;
	}

	[Serializable]
	public class AnimatorStateNameGroup
	{
		[SerializeField]
		private PlayState.AnimStateInfo _idleStateInfo;

		[SerializeField]
		private PlayState.AnimStateInfo _turnStateInfo;

		[SerializeField]
		private PlayState.AnimStateInfo _fastTurnStateInfo;

		[SerializeField]
		private string _fastTurnAssetBundleName = string.Empty;

		[SerializeField]
		private string _fastTurnAnimatorName = string.Empty;

		[SerializeField]
		private string _idleState = string.Empty;

		[SerializeField]
		private string _turnState;

		[SerializeField]
		private string _housingAnimationDefault = string.Empty;

		[SerializeField]
		private int _onbuStateID;

		[SerializeField]
		private string _merchantIdleState = string.Empty;

		public PlayState.AnimStateInfo IdleStateInfo
		{
			[CompilerGenerated]
			get
			{
				return _idleStateInfo;
			}
		}

		public PlayState.AnimStateInfo TurnStateInfo
		{
			[CompilerGenerated]
			get
			{
				return _turnStateInfo;
			}
		}

		public PlayState.AnimStateInfo FastTurnStateInfo
		{
			[CompilerGenerated]
			get
			{
				return _fastTurnStateInfo;
			}
		}

		public string FastTurnAssetBundleName
		{
			[CompilerGenerated]
			get
			{
				return _fastTurnAssetBundleName;
			}
		}

		public string FastTurnAnimatorName
		{
			[CompilerGenerated]
			get
			{
				return _fastTurnAnimatorName;
			}
		}

		public string IdleState
		{
			[CompilerGenerated]
			get
			{
				return _idleState;
			}
		}

		public string TurnState
		{
			[CompilerGenerated]
			get
			{
				return _turnState;
			}
		}

		public string HousingAnimationDefault
		{
			[CompilerGenerated]
			get
			{
				return _housingAnimationDefault;
			}
		}

		public int OnbuStateID
		{
			[CompilerGenerated]
			get
			{
				return _onbuStateID;
			}
		}

		public string MerchantIdleState
		{
			[CompilerGenerated]
			get
			{
				return _merchantIdleState;
			}
		}
	}

	[Serializable]
	public class BasicAnimatorParameter
	{
		[SerializeField]
		private string _forwardMove = string.Empty;

		[SerializeField]
		private string _heightParameterName = string.Empty;

		[SerializeField]
		private string _height1ParameterName = string.Empty;

		[SerializeField]
		private string _directionParameterName = string.Empty;

		[SerializeField]
		private string _speedParameterName = string.Empty;

		public string ForwardMove
		{
			[CompilerGenerated]
			get
			{
				return _forwardMove;
			}
		}

		public string HeightParameterName
		{
			[CompilerGenerated]
			get
			{
				return _heightParameterName;
			}
		}

		public string Height1ParameterName
		{
			[CompilerGenerated]
			get
			{
				return _height1ParameterName;
			}
		}

		public string DirectionParameterName
		{
			[CompilerGenerated]
			get
			{
				return _directionParameterName;
			}
		}

		public string SpeedParameterName
		{
			[CompilerGenerated]
			get
			{
				return _speedParameterName;
			}
		}
	}

	[Serializable]
	public class MapGroup
	{
		[SerializeField]
		[Range(0.1f, 10f)]
		private float _worldSize = 1f;

		[SerializeField]
		private int _agentDefaultNum;

		[SerializeField]
		private int _agentMax;

		[SerializeField]
		private LayerMask _charaLayer = 0;

		[SerializeField]
		private LayerMask _mapLayer = 0;

		[SerializeField]
		private LayerMask _areaDetectionLayer = 0;

		[SerializeField]
		private LayerMask _roofLayer = 0;

		[SerializeField]
		private LayerMask _seLayer = 0;

		[SerializeField]
		private LayerMask _hLayer = 0;

		[SerializeField]
		private LayerMask _envLightCulMask = -1;

		[SerializeField]
		private LayerMask _envLightCulMaskCustom = -1;

		[SerializeField]
		private string _onbuMeshTag = string.Empty;

		[SerializeField]
		private string[] _floorTypeHMeshTag;

		[SerializeField]
		private string[] _lesTypeHMeshTag;

		[SerializeField]
		private string[] _merchantHMeshTag;

		[SerializeField]
		[EnumMask]
		private EventType _playerEventMask;

		[SerializeField]
		private int _defaultMotivation;

		[SerializeField]
		private int _itemStackUpperLimit;

		[SerializeField]
		private int _itemSlotMax = 1;

		[SerializeField]
		private string _navMeshTargetName = string.Empty;

		[SerializeField]
		private string _commandTargetName = string.Empty;

		[SerializeField]
		private string _doorOpenCommandTargetName = string.Empty;

		[SerializeField]
		private string _doorCloseCommandTargetName = string.Empty;

		[SerializeField]
		private string _basePointLabelTargetName = string.Empty;

		[SerializeField]
		private string _basePointWarpTargetName = string.Empty;

		[SerializeField]
		private string _housingCenterTargetName = string.Empty;

		[SerializeField]
		private string _devicePointLabelTargetName = string.Empty;

		[SerializeField]
		private string _devicePointPivotTargetName = string.Empty;

		[SerializeField]
		private string[] _devicePointRecoveryTargetNames = new string[1] { string.Empty };

		[SerializeField]
		private string _devicePointPlayerRecoveryTargetName = string.Empty;

		[SerializeField]
		private string _farmPointLabelTargetName = string.Empty;

		[SerializeField]
		private string _craftPointLabelTargetName = string.Empty;

		[SerializeField]
		private string _eventPointLabelTargetName = string.Empty;

		[SerializeField]
		private string _petHomePointLabelTargetName = string.Empty;

		[SerializeField]
		private string _jukeBoxPointLabelTargetName = string.Empty;

		[SerializeField]
		private string _jukeBoxSoundRootTargetName = string.Empty;

		[SerializeField]
		private string _lightPointLabelTargetName = string.Empty;

		[SerializeField]
		private string _shipPointLabelTargetName = string.Empty;

		[SerializeField]
		private string _stealPivotName = string.Empty;

		[SerializeField]
		private int _shadowDistance = 400;

		public float WorldSize
		{
			[CompilerGenerated]
			get
			{
				return _worldSize;
			}
		}

		public int AgentDefaultNum
		{
			[CompilerGenerated]
			get
			{
				return _agentDefaultNum;
			}
		}

		public int AgentMax
		{
			[CompilerGenerated]
			get
			{
				return _agentMax;
			}
		}

		public LayerMask CharaLayer
		{
			[CompilerGenerated]
			get
			{
				return _charaLayer;
			}
		}

		public LayerMask MapLayer
		{
			[CompilerGenerated]
			get
			{
				return _mapLayer;
			}
		}

		public LayerMask AreaDetectionLayer
		{
			[CompilerGenerated]
			get
			{
				return _areaDetectionLayer;
			}
		}

		public LayerMask RoofLayer
		{
			[CompilerGenerated]
			get
			{
				return _roofLayer;
			}
		}

		public LayerMask SELayer
		{
			[CompilerGenerated]
			get
			{
				return _seLayer;
			}
		}

		public LayerMask HLayer
		{
			[CompilerGenerated]
			get
			{
				return _hLayer;
			}
		}

		public LayerMask EnvLightCulMask
		{
			[CompilerGenerated]
			get
			{
				return _envLightCulMask;
			}
		}

		public LayerMask EnvLightCulMaskCustom
		{
			[CompilerGenerated]
			get
			{
				return _envLightCulMaskCustom;
			}
		}

		public string OnbuMeshTag
		{
			[CompilerGenerated]
			get
			{
				return _onbuMeshTag;
			}
		}

		public string[] FloorTypeHMeshTag
		{
			[CompilerGenerated]
			get
			{
				return _floorTypeHMeshTag;
			}
		}

		public string[] LesTypeHMeshTag
		{
			[CompilerGenerated]
			get
			{
				return _lesTypeHMeshTag;
			}
		}

		public string[] MerchantHMeshTag
		{
			[CompilerGenerated]
			get
			{
				return _merchantHMeshTag;
			}
		}

		public EventType PlayerEventMask
		{
			[CompilerGenerated]
			get
			{
				return _playerEventMask;
			}
		}

		public int DefaultMotivation
		{
			[CompilerGenerated]
			get
			{
				return _defaultMotivation;
			}
		}

		public int ItemStackUpperLimit
		{
			[CompilerGenerated]
			get
			{
				return _itemStackUpperLimit;
			}
		}

		public int ItemSlotMax
		{
			[CompilerGenerated]
			get
			{
				return _itemSlotMax;
			}
		}

		public string NavMeshTargetName
		{
			[CompilerGenerated]
			get
			{
				return _navMeshTargetName;
			}
		}

		public string CommandTargetName
		{
			[CompilerGenerated]
			get
			{
				return _commandTargetName;
			}
		}

		public string DoorOpenCommandTargetName
		{
			[CompilerGenerated]
			get
			{
				return _doorOpenCommandTargetName;
			}
		}

		public string DoorCloseCommandTargetName
		{
			[CompilerGenerated]
			get
			{
				return _doorCloseCommandTargetName;
			}
		}

		public string BasePointLabelTargetName
		{
			[CompilerGenerated]
			get
			{
				return _basePointLabelTargetName;
			}
		}

		public string BasePointWarpTargetName
		{
			[CompilerGenerated]
			get
			{
				return _basePointWarpTargetName;
			}
		}

		public string HousingCenterTargetName
		{
			[CompilerGenerated]
			get
			{
				return _housingCenterTargetName;
			}
		}

		public string DevicePointLabelTargetName
		{
			[CompilerGenerated]
			get
			{
				return _devicePointLabelTargetName;
			}
		}

		public string DevicePointPivotTargetName
		{
			[CompilerGenerated]
			get
			{
				return _devicePointPivotTargetName;
			}
		}

		public string[] DevicePointRecoveryTargetNames
		{
			[CompilerGenerated]
			get
			{
				return _devicePointRecoveryTargetNames;
			}
		}

		public string DevicePointPlayerRecoveryTargetName
		{
			[CompilerGenerated]
			get
			{
				return _devicePointPlayerRecoveryTargetName;
			}
		}

		public string FarmPointLabelTargetName
		{
			[CompilerGenerated]
			get
			{
				return _farmPointLabelTargetName;
			}
		}

		public string CraftPointLabelTargetName
		{
			[CompilerGenerated]
			get
			{
				return _craftPointLabelTargetName;
			}
		}

		public string EventPointLabelTargetName
		{
			[CompilerGenerated]
			get
			{
				return _eventPointLabelTargetName;
			}
		}

		public string PetHomePointLabelTargetName
		{
			[CompilerGenerated]
			get
			{
				return _petHomePointLabelTargetName;
			}
		}

		public string JukeBoxPointLabelTargetName
		{
			[CompilerGenerated]
			get
			{
				return _jukeBoxPointLabelTargetName;
			}
		}

		public string JukeBoxSoundRootTargetName
		{
			[CompilerGenerated]
			get
			{
				return _jukeBoxSoundRootTargetName;
			}
		}

		public string LightPointLabelTargetName
		{
			[CompilerGenerated]
			get
			{
				return _lightPointLabelTargetName;
			}
		}

		public string ShipPointLabelTargetName
		{
			[CompilerGenerated]
			get
			{
				return _shipPointLabelTargetName;
			}
		}

		public string StealPivotName
		{
			[CompilerGenerated]
			get
			{
				return _stealPivotName;
			}
		}

		public int ShadowDistance
		{
			[CompilerGenerated]
			get
			{
				return _shadowDistance;
			}
		}
	}

	[Serializable]
	public class ItemBoxCapacity
	{
		[SerializeField]
		private int _storageCapacity = 200;

		[SerializeField]
		private int _pantryCapacity = 200;

		public int StorageCapacity
		{
			[CompilerGenerated]
			get
			{
				return _storageCapacity;
			}
		}

		public int PantryCapacity
		{
			[CompilerGenerated]
			get
			{
				return _pantryCapacity;
			}
		}
	}

	[Serializable]
	public class MinimapUI
	{
		[SerializeField]
		private int _baseIconID = -1;

		[SerializeField]
		private int _deviceIconID = -1;

		[SerializeField]
		private int _farmIconID = -1;

		[SerializeField]
		private int _eventIconID = -1;

		[SerializeField]
		private int _chickenIconID = -1;

		[SerializeField]
		private int _dragDeskIconID = -1;

		[SerializeField]
		private int _petUnionIconID = -1;

		[SerializeField]
		private int _recycleIconID = -1;

		[SerializeField]
		private int _jukeIconID = -1;

		public int BaseIconID
		{
			[CompilerGenerated]
			get
			{
				return _baseIconID;
			}
		}

		public int DeviceIconID
		{
			[CompilerGenerated]
			get
			{
				return _deviceIconID;
			}
		}

		public int FarmIconID
		{
			[CompilerGenerated]
			get
			{
				return _farmIconID;
			}
		}

		public int EventIconID
		{
			[CompilerGenerated]
			get
			{
				return _eventIconID;
			}
		}

		public int ChickenIconID
		{
			[CompilerGenerated]
			get
			{
				return _chickenIconID;
			}
		}

		public int DragDeskIconID
		{
			[CompilerGenerated]
			get
			{
				return _dragDeskIconID;
			}
		}

		public int PetUnionIconID
		{
			[CompilerGenerated]
			get
			{
				return _petUnionIconID;
			}
		}

		public int RecycleIconID
		{
			[CompilerGenerated]
			get
			{
				return _recycleIconID;
			}
		}

		public int JukeIconID
		{
			[CompilerGenerated]
			get
			{
				return _jukeIconID;
			}
		}
	}

	[Serializable]
	public class MapLes
	{
		[SerializeField]
		private float _loopChangeTime;

		[SerializeField]
		private string _motionParameterName;

		public float LoopChangeTime
		{
			[CompilerGenerated]
			get
			{
				return _loopChangeTime;
			}
		}

		public string MotionParameterName
		{
			[CompilerGenerated]
			get
			{
				return _motionParameterName;
			}
		}
	}

	[Serializable]
	public class RecyclingSetting
	{
		[SerializeField]
		private int _decidedItemCapacity = 100;

		[SerializeField]
		private int _createItemCapacity = 100;

		[SerializeField]
		private float _itemCreateTime = 10f;

		[SerializeField]
		private int _needNumber = 10;

		public int DecidedItemCapacity
		{
			[CompilerGenerated]
			get
			{
				return _decidedItemCapacity;
			}
		}

		public int CreateItemCapacity
		{
			[CompilerGenerated]
			get
			{
				return _createItemCapacity;
			}
		}

		public float ItemCreateTime
		{
			[CompilerGenerated]
			get
			{
				return _itemCreateTime;
			}
		}

		public int NeedNumber
		{
			[CompilerGenerated]
			get
			{
				return _needNumber;
			}
		}
	}

	[SerializeField]
	private AssetBundleManifestsDefine _abManifests;

	[SerializeField]
	private AssetBundleDirectoriesDefine _abDirectories;

	[SerializeField]
	private AssetBundlePathsDefine _abPaths;

	[SerializeField]
	[Header("Scene Name")]
	private SceneNameGroup _sceneNames;

	[SerializeField]
	[Header("Animator State")]
	private AnimatorStateNameGroup _animatorState;

	[SerializeField]
	[Header("Animator Parameter")]
	private BasicAnimatorParameter _animatorParameter;

	[SerializeField]
	[Header("Map")]
	private MapGroup _mapDefines;

	[SerializeField]
	[Header("ItemBoxCapacity")]
	private ItemBoxCapacity _itemBoxCapacityDefines;

	[SerializeField]
	[Header("MinimapUIDefine")]
	private MinimapUI _minimapUIDefine;

	[SerializeField]
	[Header("MapLesbianDefine")]
	private MapLes _MapLes;

	[SerializeField]
	[Header("RecyclingSetting")]
	private RecyclingSetting _recycling;

	public AssetBundleManifestsDefine ABManifests
	{
		[CompilerGenerated]
		get
		{
			return _abManifests;
		}
	}

	public AssetBundleDirectoriesDefine ABDirectories
	{
		[CompilerGenerated]
		get
		{
			return _abDirectories;
		}
	}

	public AssetBundlePathsDefine ABPaths
	{
		[CompilerGenerated]
		get
		{
			return _abPaths;
		}
	}

	public SceneNameGroup SceneNames
	{
		[CompilerGenerated]
		get
		{
			return _sceneNames;
		}
	}

	public AnimatorStateNameGroup AnimatorState
	{
		[CompilerGenerated]
		get
		{
			return _animatorState;
		}
	}

	public BasicAnimatorParameter AnimatorParameter
	{
		[CompilerGenerated]
		get
		{
			return _animatorParameter;
		}
	}

	public MapGroup MapDefines
	{
		[CompilerGenerated]
		get
		{
			return _mapDefines;
		}
	}

	public ItemBoxCapacity ItemBoxCapacityDefines
	{
		[CompilerGenerated]
		get
		{
			return _itemBoxCapacityDefines;
		}
	}

	public MinimapUI MinimapUIDefine
	{
		[CompilerGenerated]
		get
		{
			return _minimapUIDefine;
		}
	}

	public MapLes MapLesDefine
	{
		[CompilerGenerated]
		get
		{
			return _MapLes;
		}
	}

	public RecyclingSetting Recycling
	{
		[CompilerGenerated]
		get
		{
			return _recycling;
		}
	}
}
