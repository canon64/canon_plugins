using System;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AIProject;

public class LocomotionProfile : ScriptableObject
{
	[Serializable]
	public struct PlayerSpeedSetting
	{
		public float normalSpeed;

		public float walkSpeed;
	}

	[Serializable]
	public struct AgentSpeedSetting
	{
		public float walkSpeed;

		public float runSpeed;

		public float hurtSpeed;

		public float escapeSpeed;

		public float followRunSpeed;

		public float tutorialWalkSpeed;

		public float tutorialRunSpeed;
	}

	[Serializable]
	public struct MerchantSpeedSetting
	{
		public float walkSpeed;

		public float runSpeed;
	}

	[Serializable]
	public struct PhotoShotSetting
	{
		public float mouseZoomScale;

		public Vector3 maxOffset;

		public Vector3 minOffset;

		public Vector3 offsetMoveValue;
	}

	[Serializable]
	public struct LensSettings
	{
		public float FieldOfView;

		public float MinFOV;

		public float MaxFOV;

		public float NearClipPlane;

		public float FarClipPlane;

		public float Dutch;

		public float KeyZoomScale;

		public float KeyRotateScale;
	}

	[Serializable]
	public struct HousingWaypointSettings
	{
		public float InstallationDistance;

		public float InstallationHeight;

		public float ClosestEdgeDistance;

		public float SampleDistance;
	}

	[Serializable]
	public struct DropSearchActionPointSettings
	{
		public float AvailableAngle;

		public float AvailableDistance;
	}

	[SerializeField]
	private PlayerSpeedSetting _playerSpeed = default(PlayerSpeedSetting);

	[SerializeField]
	private AgentSpeedSetting _agentSpeed = default(AgentSpeedSetting);

	[SerializeField]
	private MerchantSpeedSetting _merchantSpeed = default(MerchantSpeedSetting);

	[SerializeField]
	private float _lerpSpeed = 5f;

	[SerializeField]
	private float _tutorialLerpSpeed = 10f;

	[SerializeField]
	[MinValue(1.0)]
	private float _walkableDistance = 1f;

	[SerializeField]
	[MinValue(1.0)]
	private float _mustRunDistance = 1f;

	[SerializeField]
	private float _actionPointNavMeshSampleDistance = 1f;

	[SerializeField]
	private float _approachDistanceActionPoint = 1f;

	[SerializeField]
	private float _approachDistanceActionPointCloser = 1f;

	[SerializeField]
	[MinValue(0.0)]
	private float _pointDistanceMargin = 1f;

	[SerializeField]
	[MinValue(0.0)]
	private float _waypointTweenMinDistance = 1f;

	[SerializeField]
	private float _searchActorRadius = 1f;

	[SerializeField]
	private float _accessInvasionRange = 1f;

	[SerializeField]
	private float _charaVisibleDistance = 1f;

	[SerializeField]
	[MinValue(1.0)]
	private float _effectiveDynamicBoneDistance = 20f;

	[SerializeField]
	[MinValue(1.0)]
	private float _crossFadeEnableDistance = 300f;

	[SerializeField]
	private float _timeToLeftState = 30f;

	[SerializeField]
	private int _obonEventItemID;

	[SerializeField]
	private float _timeToBeware = 1f;

	[SerializeField]
	private float _approachDistanceStoryPoint = 1f;

	[SerializeField]
	private float _minDistanceDoor = 5f;

	[SerializeField]
	private float _maxDistanceDoor = 10f;

	[SerializeField]
	private string _playerLocoItemParentName = string.Empty;

	[SerializeField]
	private string _agentLocoItemParentName = string.Empty;

	[SerializeField]
	private string _agentOtherParentName = string.Empty;

	[SerializeField]
	private string _rootParentName = string.Empty;

	[SerializeField]
	private string _leftHandParentName = string.Empty;

	[SerializeField]
	private string _holdingHandTarget = string.Empty;

	[SerializeField]
	private string _holdingElboTarget = string.Empty;

	[SerializeField]
	private string _rightHandParentName = string.Empty;

	[SerializeField]
	private string _faceLightParentName = string.Empty;

	[SerializeField]
	private Vector3 _faceLightOffset = Vector3.zero;

	[SerializeField]
	private Vector3 _enviroEffectOffset = Vector3.zero;

	[SerializeField]
	private PhotoShotSetting _photoShot = default(PhotoShotSetting);

	[SerializeField]
	private Vector3 _communicationOffset = Vector3.back;

	[SerializeField]
	private string _communicationDiagonalTargetName = string.Empty;

	[SerializeField]
	private LensSettings _defaultLensSetting = default(LensSettings);

	[SerializeField]
	private Threshold _cameraPowX = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _cameraPowY = new Threshold(0f, 1f);

	[SerializeField]
	[Space]
	private Vector2 _defaultCameraAxisPow = new Vector2(180f, 1f);

	[SerializeField]
	private Vector2 _cameraAccelRate = new Vector2(0f, 0f);

	[SerializeField]
	private float _turnEnableAngle = 30f;

	[SerializeField]
	private HousingWaypointSettings _housingWaypointSetting = default(HousingWaypointSettings);

	[SerializeField]
	private DropSearchActionPointSettings _dropSearchActionPointSetting = default(DropSearchActionPointSettings);

	[SerializeField]
	private float _fishingWaterCheckDistance;

	public PlayerSpeedSetting PlayerSpeed
	{
		[CompilerGenerated]
		get
		{
			return _playerSpeed;
		}
	}

	public AgentSpeedSetting AgentSpeed
	{
		[CompilerGenerated]
		get
		{
			return _agentSpeed;
		}
	}

	public MerchantSpeedSetting MerchantSpeed
	{
		[CompilerGenerated]
		get
		{
			return _merchantSpeed;
		}
	}

	public float LerpSpeed
	{
		[CompilerGenerated]
		get
		{
			return _lerpSpeed;
		}
	}

	public float TutorialLerpSpeed
	{
		[CompilerGenerated]
		get
		{
			return _tutorialLerpSpeed;
		}
	}

	public float WalkableDistance
	{
		[CompilerGenerated]
		get
		{
			return _walkableDistance;
		}
	}

	public float MustRunDistance
	{
		[CompilerGenerated]
		get
		{
			return _mustRunDistance;
		}
	}

	public float ActionPointNavMeshSampleDistance
	{
		[CompilerGenerated]
		get
		{
			return _actionPointNavMeshSampleDistance;
		}
	}

	public float ApproachDistanceActionPoint
	{
		[CompilerGenerated]
		get
		{
			return _approachDistanceActionPoint;
		}
	}

	public float ApproachDistanceActionPointCloser
	{
		[CompilerGenerated]
		get
		{
			return _approachDistanceActionPointCloser;
		}
	}

	public float PointDistanceMargin
	{
		[CompilerGenerated]
		get
		{
			return _pointDistanceMargin;
		}
	}

	public float WaypointTweenMinDistance
	{
		[CompilerGenerated]
		get
		{
			return _waypointTweenMinDistance;
		}
	}

	public float SearchActorRadius
	{
		[CompilerGenerated]
		get
		{
			return _searchActorRadius;
		}
	}

	public float AccessInvasionRange
	{
		[CompilerGenerated]
		get
		{
			return _accessInvasionRange;
		}
	}

	public float CharaVisibleDistance
	{
		[CompilerGenerated]
		get
		{
			return _charaVisibleDistance;
		}
	}

	public float EffectiveDynamicBoneDistance
	{
		[CompilerGenerated]
		get
		{
			return _effectiveDynamicBoneDistance;
		}
	}

	public float CrossFadeEnableDistance
	{
		[CompilerGenerated]
		get
		{
			return _crossFadeEnableDistance;
		}
	}

	public float TimeToLeftState
	{
		[CompilerGenerated]
		get
		{
			return _timeToLeftState;
		}
	}

	public int ObonEventItemID
	{
		[CompilerGenerated]
		get
		{
			return _obonEventItemID;
		}
	}

	public float TimeToBeware
	{
		[CompilerGenerated]
		get
		{
			return _timeToBeware;
		}
	}

	public float ApproachDistanceStoryPoint
	{
		[CompilerGenerated]
		get
		{
			return _approachDistanceStoryPoint;
		}
	}

	public float MinDistanceDoor
	{
		[CompilerGenerated]
		get
		{
			return _minDistanceDoor;
		}
	}

	public float MaxDistanceDoor
	{
		[CompilerGenerated]
		get
		{
			return _maxDistanceDoor;
		}
	}

	public string PlayerLocoItemParentName
	{
		[CompilerGenerated]
		get
		{
			return _playerLocoItemParentName;
		}
	}

	public string AgentLocoItemParentName
	{
		[CompilerGenerated]
		get
		{
			return _agentLocoItemParentName;
		}
	}

	public string AgentOtherParentName
	{
		[CompilerGenerated]
		get
		{
			return _agentOtherParentName;
		}
	}

	public string RootParentName
	{
		[CompilerGenerated]
		get
		{
			return _rootParentName;
		}
	}

	public string LeftHandParentName
	{
		[CompilerGenerated]
		get
		{
			return _leftHandParentName;
		}
	}

	public string HoldingHandTarget
	{
		[CompilerGenerated]
		get
		{
			return _holdingHandTarget;
		}
	}

	public string HoldingElboTarget
	{
		[CompilerGenerated]
		get
		{
			return _holdingElboTarget;
		}
	}

	public string RightHandParentName
	{
		[CompilerGenerated]
		get
		{
			return _rightHandParentName;
		}
	}

	public string FadeLightParentName
	{
		[CompilerGenerated]
		get
		{
			return _faceLightParentName;
		}
	}

	public Vector3 FaceLightOffset
	{
		[CompilerGenerated]
		get
		{
			return _faceLightOffset;
		}
	}

	public Vector3 EnviroEffectOffset
	{
		[CompilerGenerated]
		get
		{
			return _enviroEffectOffset;
		}
	}

	public PhotoShotSetting PhotoShot
	{
		[CompilerGenerated]
		get
		{
			return _photoShot;
		}
	}

	public Vector3 CommunicationOffset
	{
		[CompilerGenerated]
		get
		{
			return _communicationOffset;
		}
	}

	public string CommunicationDiagonalTargetName
	{
		[CompilerGenerated]
		get
		{
			return _communicationDiagonalTargetName;
		}
	}

	public LensSettings DefaultLensSetting
	{
		[CompilerGenerated]
		get
		{
			return _defaultLensSetting;
		}
	}

	public Threshold CameraPowX
	{
		[CompilerGenerated]
		get
		{
			return _cameraPowX;
		}
	}

	public Threshold CameraPowY
	{
		[CompilerGenerated]
		get
		{
			return _cameraPowY;
		}
	}

	public Vector2 DefaultCameraAxisPow
	{
		[CompilerGenerated]
		get
		{
			return _defaultCameraAxisPow;
		}
	}

	public Vector3 CameraAccelRate
	{
		[CompilerGenerated]
		get
		{
			return _cameraAccelRate;
		}
	}

	public float TurnEnableAngle
	{
		[CompilerGenerated]
		get
		{
			return _turnEnableAngle;
		}
	}

	public HousingWaypointSettings HousingWaypointSetting
	{
		[CompilerGenerated]
		get
		{
			return _housingWaypointSetting;
		}
	}

	public DropSearchActionPointSettings DropSearchActionPointSetting
	{
		[CompilerGenerated]
		get
		{
			return _dropSearchActionPointSetting;
		}
	}

	public float FishingWaterCheckDistance
	{
		[CompilerGenerated]
		get
		{
			return _fishingWaterCheckDistance;
		}
	}
}
