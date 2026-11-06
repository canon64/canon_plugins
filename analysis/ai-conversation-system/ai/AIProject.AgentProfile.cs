using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using AIProject.Definitions;
using AIProject.UI.Viewer;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AIProject;

public class AgentProfile : SerializedScriptableObject
{
	[Serializable]
	public class NormalSkillIDDefines
	{
	}

	[Serializable]
	public struct HSkillIDDefines
	{
		public int homosexualID;

		public int groperID;
	}

	[Serializable]
	public class PoseIDCollection
	{
		[SerializeField]
		private int _locomotionID;

		[SerializeField]
		private int _walkLocoID;

		[SerializeField]
		private int _umbrellaLocoID;

		[SerializeField]
		private int _lampLocoID;

		[SerializeField]
		private int _lampWalkLocoID;

		[SerializeField]
		private int _hurtLocoID;

		[SerializeField]
		private int _mojimojiLocoID;

		[SerializeField]
		private int _rainRunLocoID;

		[SerializeField]
		private int _cookMoveLocoID;

		[SerializeField]
		private PoseKeyPair _faintID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _collapseID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _comaID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _medicID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _cureID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _weaknessID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _greetPoseID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair[] _normalIDList;

		[SerializeField]
		private PoseKeyPair _coldPoseID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _hotPoseID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _coughID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _grossID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _yawnID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _wakeUpID = default(PoseKeyPair);

		private PoseKeyPair _fearID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _chuckleID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _standHurtID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _surprisedID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _deepBreathID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _wakenUpID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _surpriseMasturbationID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _surpriseInToiletSquatID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _surpriseInToiletSitID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _surpriseInBathStandID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _surpriseInBathSitID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _surpriseInGoemonID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _chairCoughID = default(PoseKeyPair);

		[SerializeField]
		[Header("しゃがみアクション指定")]
		[LabelText("しゃがみ怖がる")]
		private PoseKeyPair _squatFearID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _groomyID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _angryID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _hungryID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _ovationID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair[] _playGameStandIDList;

		[SerializeField]
		private PoseKeyPair[] _playGameStandOutdoorIDList;

		[SerializeField]
		private PoseKeyPair _rainUmbrellaID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _clearPoseID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _eatStandID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _eatChairID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _eatDeskID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _eatDishID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _snitchFoodID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _stealFoundID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _drinkStandID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _drinkChairID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _peeID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _sleepTogetherRight = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _sleepTogetherLeft = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _wateringID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _faceWash = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _handWash = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _yobai = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair[] _appearIDList;

		public int LocomotionID
		{
			[CompilerGenerated]
			get
			{
				return _locomotionID;
			}
		}

		public int WalkLocoID
		{
			[CompilerGenerated]
			get
			{
				return _walkLocoID;
			}
		}

		public int UmbrellaLocoID
		{
			[CompilerGenerated]
			get
			{
				return _umbrellaLocoID;
			}
		}

		public int LampLocoID
		{
			[CompilerGenerated]
			get
			{
				return _lampLocoID;
			}
		}

		public int LampWalkLocoID
		{
			[CompilerGenerated]
			get
			{
				return _lampWalkLocoID;
			}
		}

		public int HurtLocoID
		{
			[CompilerGenerated]
			get
			{
				return _hurtLocoID;
			}
		}

		public int MojimojiLocoID
		{
			[CompilerGenerated]
			get
			{
				return _mojimojiLocoID;
			}
		}

		public int RainRunLocoID
		{
			[CompilerGenerated]
			get
			{
				return _rainRunLocoID;
			}
		}

		public int CookMoveLocoID
		{
			[CompilerGenerated]
			get
			{
				return _cookMoveLocoID;
			}
		}

		public PoseKeyPair FaintID
		{
			[CompilerGenerated]
			get
			{
				return _faintID;
			}
		}

		public PoseKeyPair CollapseID
		{
			[CompilerGenerated]
			get
			{
				return _collapseID;
			}
		}

		public PoseKeyPair ComaID
		{
			[CompilerGenerated]
			get
			{
				return _comaID;
			}
		}

		public PoseKeyPair MedicID
		{
			[CompilerGenerated]
			get
			{
				return _medicID;
			}
		}

		public PoseKeyPair CureID
		{
			[CompilerGenerated]
			get
			{
				return _cureID;
			}
		}

		public PoseKeyPair WeaknessID
		{
			[CompilerGenerated]
			get
			{
				return _weaknessID;
			}
		}

		public PoseKeyPair GreetPoseID
		{
			[CompilerGenerated]
			get
			{
				return _greetPoseID;
			}
		}

		public PoseKeyPair[] NormalIDList
		{
			[CompilerGenerated]
			get
			{
				return _normalIDList;
			}
		}

		public PoseKeyPair ColdPoseID
		{
			[CompilerGenerated]
			get
			{
				return _coldPoseID;
			}
		}

		public PoseKeyPair HotPoseID
		{
			[CompilerGenerated]
			get
			{
				return _hotPoseID;
			}
		}

		public PoseKeyPair CoughID
		{
			[CompilerGenerated]
			get
			{
				return _coughID;
			}
		}

		public PoseKeyPair GrossID
		{
			[CompilerGenerated]
			get
			{
				return _grossID;
			}
		}

		public PoseKeyPair YawnID
		{
			[CompilerGenerated]
			get
			{
				return _yawnID;
			}
		}

		public PoseKeyPair WakeUpID
		{
			[CompilerGenerated]
			get
			{
				return _wakeUpID;
			}
		}

		public PoseKeyPair FearID
		{
			[CompilerGenerated]
			get
			{
				return _fearID;
			}
		}

		public PoseKeyPair ChuckleID
		{
			[CompilerGenerated]
			get
			{
				return _chuckleID;
			}
		}

		public PoseKeyPair StandHurtID
		{
			[CompilerGenerated]
			get
			{
				return _standHurtID;
			}
		}

		public PoseKeyPair SurprisedID
		{
			[CompilerGenerated]
			get
			{
				return _surprisedID;
			}
		}

		public PoseKeyPair DeepBreathID
		{
			[CompilerGenerated]
			get
			{
				return _deepBreathID;
			}
		}

		public PoseKeyPair WakenUpID
		{
			[CompilerGenerated]
			get
			{
				return _wakenUpID;
			}
		}

		public PoseKeyPair SurpriseMasturbationID
		{
			[CompilerGenerated]
			get
			{
				return _surpriseMasturbationID;
			}
		}

		public PoseKeyPair SurpriseInToiletSquatID
		{
			[CompilerGenerated]
			get
			{
				return _surpriseInToiletSquatID;
			}
		}

		public PoseKeyPair SurpriseInToiletSitID
		{
			[CompilerGenerated]
			get
			{
				return _surpriseInToiletSitID;
			}
		}

		public PoseKeyPair SurpriseInBathStandID
		{
			[CompilerGenerated]
			get
			{
				return _surpriseInBathStandID;
			}
		}

		public PoseKeyPair SurpriseInBathSitID
		{
			[CompilerGenerated]
			get
			{
				return _surpriseInBathSitID;
			}
		}

		public PoseKeyPair SurpriseInGoemonID
		{
			[CompilerGenerated]
			get
			{
				return _surpriseInGoemonID;
			}
		}

		public PoseKeyPair ChairCoughID
		{
			[CompilerGenerated]
			get
			{
				return _chairCoughID;
			}
		}

		public PoseKeyPair SquatFearID
		{
			[CompilerGenerated]
			get
			{
				return _squatFearID;
			}
		}

		public PoseKeyPair GroomyID
		{
			[CompilerGenerated]
			get
			{
				return _groomyID;
			}
		}

		public PoseKeyPair AngryID
		{
			[CompilerGenerated]
			get
			{
				return _angryID;
			}
		}

		public PoseKeyPair HungryID
		{
			[CompilerGenerated]
			get
			{
				return _hungryID;
			}
		}

		public PoseKeyPair OvationID
		{
			[CompilerGenerated]
			get
			{
				return _ovationID;
			}
		}

		public PoseKeyPair[] PlayGameStandIDList
		{
			[CompilerGenerated]
			get
			{
				return _playGameStandIDList;
			}
		}

		public PoseKeyPair[] PlayGameStandOutdoorIDList
		{
			[CompilerGenerated]
			get
			{
				return _playGameStandOutdoorIDList;
			}
		}

		public PoseKeyPair RainUmbrellaID
		{
			[CompilerGenerated]
			get
			{
				return _rainUmbrellaID;
			}
		}

		public PoseKeyPair ClearPoseID
		{
			[CompilerGenerated]
			get
			{
				return _clearPoseID;
			}
		}

		public PoseKeyPair EatStandID
		{
			[CompilerGenerated]
			get
			{
				return _eatStandID;
			}
		}

		public PoseKeyPair EatChairID
		{
			[CompilerGenerated]
			get
			{
				return _eatChairID;
			}
		}

		public PoseKeyPair EatDeskID
		{
			[CompilerGenerated]
			get
			{
				return _eatDeskID;
			}
		}

		public PoseKeyPair EatDishID
		{
			[CompilerGenerated]
			get
			{
				return _eatDishID;
			}
		}

		public PoseKeyPair SnitchFoodID
		{
			[CompilerGenerated]
			get
			{
				return _snitchFoodID;
			}
		}

		public PoseKeyPair StealFoundID
		{
			[CompilerGenerated]
			get
			{
				return _stealFoundID;
			}
		}

		public PoseKeyPair DrinkStandID
		{
			[CompilerGenerated]
			get
			{
				return _drinkStandID;
			}
		}

		public PoseKeyPair DrinkChairID
		{
			[CompilerGenerated]
			get
			{
				return _drinkChairID;
			}
		}

		public PoseKeyPair PeeID
		{
			[CompilerGenerated]
			get
			{
				return _peeID;
			}
		}

		public PoseKeyPair SleepTogetherRight
		{
			[CompilerGenerated]
			get
			{
				return _sleepTogetherRight;
			}
		}

		public PoseKeyPair SleepTogetherLeft
		{
			[CompilerGenerated]
			get
			{
				return _sleepTogetherLeft;
			}
		}

		public PoseKeyPair WateringID
		{
			[CompilerGenerated]
			get
			{
				return _wateringID;
			}
		}

		public PoseKeyPair FaceWash
		{
			[CompilerGenerated]
			get
			{
				return _faceWash;
			}
		}

		public PoseKeyPair HandWash
		{
			[CompilerGenerated]
			get
			{
				return _handWash;
			}
		}

		public PoseKeyPair Yobai
		{
			[CompilerGenerated]
			get
			{
				return _yobai;
			}
		}

		public PoseKeyPair[] AppearIDList
		{
			[CompilerGenerated]
			get
			{
				return _appearIDList;
			}
		}
	}

	[Serializable]
	public struct WalkParameter
	{
		[MinValue(3.0)]
		public int reservedPathCount;

		public float arrivedDistance;

		public ThresholdInt viaPointNumThreshold;

		public WalkParameter(int pathCount, float distance, int min, int max)
		{
			reservedPathCount = pathCount;
			arrivedDistance = distance;
			viaPointNumThreshold = new ThresholdInt(min, max);
		}

		public WalkParameter(int pathCount, float distance, ThresholdInt threshold)
		{
			reservedPathCount = pathCount;
			arrivedDistance = distance;
			viaPointNumThreshold = threshold;
		}
	}

	[Serializable]
	public struct RangeParameter
	{
		public float arrivedDistance;

		public float acceptableHeight;

		public float arrivedDistanceIncludeAct;

		public float acceptableHeightIncludeAct;

		public float leaveDistance;

		public float leaveDistanceInSurprise;

		public RangeParameter(float arrivedDistance_, float distIncludeAction, float height, float heightAction, float leaveDistance_, float leaveDistanceInSurprise_)
		{
			arrivedDistance = arrivedDistance_;
			arrivedDistanceIncludeAct = distIncludeAction;
			acceptableHeight = height;
			acceptableHeightIncludeAct = heightAction;
			leaveDistance = leaveDistance_;
			leaveDistanceInSurprise = leaveDistanceInSurprise_;
		}
	}

	[Serializable]
	public class ActionPointSightSetting
	{
		[SerializeField]
		private float _fovAngle = 90f;

		[SerializeField]
		private float _heightRange = 1f;

		[SerializeField]
		private float _viewDistance;

		[SerializeField]
		private Vector3 _offset = Vector3.zero;

		[SerializeField]
		private float _angleOffset2D;

		public float FOVAngle
		{
			[CompilerGenerated]
			get
			{
				return _fovAngle;
			}
		}

		public float HeightRange
		{
			[CompilerGenerated]
			get
			{
				return _heightRange;
			}
		}

		public float ViewDistance
		{
			[CompilerGenerated]
			get
			{
				return _viewDistance;
			}
		}

		public Vector3 Offset
		{
			[CompilerGenerated]
			get
			{
				return _offset;
			}
		}

		public float AngleOffset2D
		{
			[CompilerGenerated]
			get
			{
				return _angleOffset2D;
			}
		}

		public bool HasEntered(Transform @base, Vector3 target, float radius)
		{
			if (Vector3.Distance(target, @base.position) > _viewDistance + radius)
			{
				return false;
			}
			if (Mathf.Abs(@base.transform.position.y - target.y) > _heightRange)
			{
				return false;
			}
			float num = _fovAngle / 2f;
			float num2 = Vector3.Angle(target - @base.position, @base.forward);
			return num2 < num;
		}

		public bool HasEntered(Collider collider, Transform @base)
		{
			Vector3 vector = ((collider is SphereCollider) ? (collider as SphereCollider).center : ((!(collider is CapsuleCollider)) ? Vector3.zero : (collider as CapsuleCollider).center));
			float num = ((collider is SphereCollider) ? (collider as SphereCollider).radius : ((!(collider is CapsuleCollider)) ? 0f : (collider as CapsuleCollider).radius));
			if (Vector3.Distance(collider.transform.position + vector, @base.position) > _viewDistance + num)
			{
				return false;
			}
			if (Mathf.Abs(@base.transform.position.y - collider.transform.position.y) > _heightRange)
			{
				return false;
			}
			float num2 = _fovAngle / 2f;
			float num3 = Vector3.Angle(collider.transform.position - @base.position, @base.forward);
			return num3 < num2;
		}
	}

	[Serializable]
	public class SightSetting
	{
		[SerializeField]
		private float _fovAngle = 90f;

		[SerializeField]
		private float _heightRange = 1f;

		[SerializeField]
		private float _viewDistance;

		[SerializeField]
		private Vector3 _offset = Vector3.zero;

		[SerializeField]
		private float _angleOffset2D;

		public float FOVAngle
		{
			[CompilerGenerated]
			get
			{
				return _fovAngle;
			}
		}

		public float HeightRange
		{
			[CompilerGenerated]
			get
			{
				return _heightRange;
			}
		}

		public float ViewDistance
		{
			[CompilerGenerated]
			get
			{
				return _viewDistance;
			}
		}

		public Vector3 Offset
		{
			[CompilerGenerated]
			get
			{
				return _offset;
			}
		}

		public float AngleOffset2D
		{
			[CompilerGenerated]
			get
			{
				return _angleOffset2D;
			}
		}

		public bool HasEntered(Transform @base, Vector3 targetPosition)
		{
			if (Vector3.Distance(targetPosition, @base.position) > _viewDistance)
			{
				return false;
			}
			if (Mathf.Abs(@base.transform.position.y - targetPosition.y) > _heightRange)
			{
				return false;
			}
			float num = _fovAngle / 2f;
			Vector3 vector = new Vector3(@base.position.x, 0f, @base.position.z);
			Vector3 vector2 = new Vector3(targetPosition.x, 0f, targetPosition.z);
			float num2 = Vector3.Angle(vector2 - vector, @base.forward);
			return num2 < num;
		}

		public bool HasEntered(Transform @base, Vector3 targetPosition, float angleOffsetY)
		{
			if (_viewDistance < Vector3.Distance(targetPosition, @base.position))
			{
				return false;
			}
			if (Mathf.Abs(@base.transform.position.y - targetPosition.y) > _heightRange)
			{
				return false;
			}
			float num = _fovAngle / 2f;
			Vector3 to = Quaternion.AngleAxis(angleOffsetY, @base.up) * @base.forward;
			Vector3 vector = new Vector3(@base.position.x, 0f, @base.position.z);
			Vector3 vector2 = new Vector3(targetPosition.x, 0f, targetPosition.z);
			float num2 = Vector3.Angle(vector2 - vector, to);
			return num2 < num;
		}

		public void DrawGizmos(Transform transform)
		{
		}

		public void DrawGizmos(Transform transform, float angleOffsetY)
		{
		}
	}

	[Serializable]
	public class TutorialSetting
	{
		[SerializeField]
		private int _animatorID;

		[SerializeField]
		private string _defaultStateName = string.Empty;

		[SerializeField]
		private float _defaultStateFadeTime;

		[SerializeField]
		private PoseKeyPair _goGhroughAnimID = default(PoseKeyPair);

		[SerializeField]
		private int[] _goGhroughActionIDList = new int[0];

		[SerializeField]
		private PoseKeyPair _threeStepJumpAnimID = default(PoseKeyPair);

		[SerializeField]
		private int[] _threeStepJumpActionIDList = new int[0];

		public int AnimatorID
		{
			[CompilerGenerated]
			get
			{
				return _animatorID;
			}
		}

		public string DefaultStateName
		{
			[CompilerGenerated]
			get
			{
				return _defaultStateName;
			}
		}

		public float DefaultStateFadeTime
		{
			[CompilerGenerated]
			get
			{
				return _defaultStateFadeTime;
			}
		}

		public PoseKeyPair GoGhroughAnimID
		{
			[CompilerGenerated]
			get
			{
				return _goGhroughAnimID;
			}
		}

		public int[] GoGhroughActionIDList
		{
			[CompilerGenerated]
			get
			{
				return _goGhroughActionIDList;
			}
		}

		public PoseKeyPair ThreeStepJumpAnimID
		{
			[CompilerGenerated]
			get
			{
				return _threeStepJumpAnimID;
			}
		}

		public int[] ThreeStepJumpActionIDList
		{
			[CompilerGenerated]
			get
			{
				return _threeStepJumpActionIDList;
			}
		}
	}

	[Serializable]
	public struct MapMasturbationSetting
	{
		public int wMotivation;

		public int mMotivation;

		public int sMotivation;

		public int oMotivation;
	}

	[Serializable]
	public struct MapLesbianSetting
	{
		public int wMotivation;

		public int sMotivation;

		public int oMotivation;
	}

	[Serializable]
	public struct DiminuationRates
	{
		[SerializeField]
		private float _durationInHour;

		[SerializeField]
		private float _durationInHourRecovery;

		public float value;

		public float valueRecovery;

		public static DiminuationRates Default
		{
			[CompilerGenerated]
			get
			{
				DiminuationRates result = default(DiminuationRates);
				result.value = 0f;
				return result;
			}
		}

		private float DurationInHour
		{
			[CompilerGenerated]
			get
			{
				return _durationInHour;
			}
		}

		private float DurationInHourRecovery
		{
			[CompilerGenerated]
			get
			{
				return _durationInHourRecovery;
			}
		}

		public DiminuationRates(float value_, float recovery)
		{
			value = value_;
			valueRecovery = recovery;
			_durationInHour = 0f;
			_durationInHourRecovery = 0f;
		}
	}

	[Serializable]
	public struct PhotoShotRangeParameter
	{
		public float arriveDistance;

		public float acceptableHeight;

		public float leaveDistance;

		public float sightAngle;

		public float sightOffsetZ;

		public float invisibleAngle;

		public float reliabilityBorder;
	}

	private ReadOnlyDictionary<EventType, EventType> _afterActionReadOnlyTable;

	[SerializeField]
	private Dictionary<EventType, EventType> _afterActionTable = new Dictionary<EventType, EventType>();

	[SerializeField]
	private List<Desire.ActionType> _encounterWhitelist;

	[SerializeField]
	private List<Desire.ActionType> _strollWhitelist = new List<Desire.ActionType>();

	[SerializeField]
	private List<Desire.ActionType> _blackListInSaveAndLoad = new List<Desire.ActionType>();

	[SerializeField]
	private Dictionary<int, int> _defaultAreaIDTable = new Dictionary<int, int>();

	[SerializeField]
	private WalkParameter _walkSetting = default(WalkParameter);

	[SerializeField]
	private int _avoidancePriorityDefault;

	[SerializeField]
	private int _avoidancePriorityStationary;

	[SerializeField]
	private float _actionPointNearDistance = 10f;

	[SerializeField]
	private ThresholdInt _escapeViaPointNumThreshold = new ThresholdInt(1, 3);

	[SerializeField]
	private PoseIDCollection _poseIDTable;

	[SerializeField]
	private Dictionary<int, PoseKeyPair> _advIdleTable = new Dictionary<int, PoseKeyPair>();

	[SerializeField]
	private Dictionary<int, PoseKeyPair> _advHouchiTable = new Dictionary<int, PoseKeyPair>();

	[SerializeField]
	private Dictionary<int, PoseKeyPair> _advLeaveTable = new Dictionary<int, PoseKeyPair>();

	[SerializeField]
	private Dictionary<int, PoseKeyPair> _advBreastTable = new Dictionary<int, PoseKeyPair>();

	[SerializeField]
	private Dictionary<int, PoseKeyPair> _advBreastNoReactionTable = new Dictionary<int, PoseKeyPair>();

	[SerializeField]
	private NormalSkillIDDefines _normalSkillIDSetting = new NormalSkillIDDefines();

	[SerializeField]
	private HSkillIDDefines _hSkillIDSetting = default(HSkillIDDefines);

	[SerializeField]
	private float _turnMinAngle;

	[SerializeField]
	private RangeParameter _rangeSetting = default(RangeParameter);

	[SerializeField]
	private PhotoShotRangeParameter _photoShotRangeSetting = default(PhotoShotRangeParameter);

	[SerializeField]
	private ActionPointSightSetting _actionPointSight = new ActionPointSightSetting();

	[SerializeField]
	private int _defaultRelationShip = 50;

	[SerializeField]
	private float _hSampleDistance = 50f;

	[SerializeField]
	private SightSetting _characterFarSight = new SightSetting();

	[SerializeField]
	private SightSetting _characterNearSight = new SightSetting();

	[SerializeField]
	private SightSetting _animalSight = new SightSetting();

	[SerializeField]
	private float _durationCTForCall = 9999999f;

	[SerializeField]
	private float _talkLockDuration = 9999999f;

	[SerializeField]
	private DiminuationRates _diminuationRate = new DiminuationRates(1f, 2f);

	[SerializeField]
	private Dictionary<int, DiminuationRates> _diminMotivationRate = Enum.GetValues(typeof(Desire.Type)).Cast<Desire.Type>().Select((Desire.Type _, int idx) => idx)
		.ToDictionary((int x) => x, (int x) => new DiminuationRates(1f, 2f));

	[SerializeField]
	private DiminuationRates _weaknessDiminuationRate = new DiminuationRates(1f, 2f);

	[SerializeField]
	private DiminuationRates _talkMotivationDimRate = new DiminuationRates(1f, 2f);

	[SerializeField]
	private ThresholdInt _secondSleepDurationMinMax = new ThresholdInt(30, 60);

	[SerializeField]
	private Threshold _standDurationMinMax = new Threshold(1f, 3f);

	[SerializeField]
	private ItemIDKeyPair[] _canStandEatItems;

	[SerializeField]
	private ItemIDKeyPair[] _lowerTempDrinkItems;

	[SerializeField]
	private ItemIDKeyPair[] _raiseTempDrinkItems;

	[SerializeField]
	private ItemIDKeyPair _coconutDrinkID = default(ItemIDKeyPair);

	[SerializeField]
	private float _coldTempBorder = 20f;

	[SerializeField]
	private float _hotTempBorder = 80f;

	[SerializeField]
	private Dictionary<int, float> _motivationMinValueTable = new Dictionary<int, float>();

	[SerializeField]
	private float _activeMotivationBorder = 20f;

	[SerializeField]
	[Range(0f, 1f)]
	private float _mustRunMotivationPercent = 50f;

	[SerializeField]
	private Threshold _diminuationInMasturbation;

	[SerializeField]
	private Threshold _diminuationInLesbian;

	[SerializeField]
	private int _itemSlotMaxInInventory = 1;

	[SerializeField]
	private int _itemSlotCountToItemBox = 1;

	[SerializeField]
	private InventoryFacadeViewer.ItemFilter[] _presentItemFilter;

	[SerializeField]
	private ItemIDKeyPair[] _medicineNormalItemList;

	[SerializeField]
	private ItemIDKeyPair[] _medicineColdItemList;

	[SerializeField]
	private ItemIDKeyPair[] _medicineHurtItemList;

	[SerializeField]
	private ItemIDKeyPair[] _medicineStomachacheItemList;

	[SerializeField]
	private ItemIDKeyPair[] _medicineHeatStrokeItemList;

	[SerializeField]
	private ItemIDKeyPair _feverReducerID = default(ItemIDKeyPair);

	[SerializeField]
	private ItemIDKeyPair _coldMedicineID = default(ItemIDKeyPair);

	[SerializeField]
	private ItemIDKeyPair _stomachMeidicineID = default(ItemIDKeyPair);

	[SerializeField]
	private ItemIDKeyPair _wetTowelID = default(ItemIDKeyPair);

	[SerializeField]
	private Vector3 _offsetInDate = Vector3.zero;

	[SerializeField]
	private Vector3 _offsetInPartyS = Vector3.zero;

	[SerializeField]
	private Vector3 _offsetInPartyM = Vector3.zero;

	[SerializeField]
	private Vector3 _offsetInPartyL = Vector3.zero;

	[SerializeField]
	private float _restDistance = 1f;

	[SerializeField]
	private float _runDistance = 10f;

	[NavMeshAreaEnumMask]
	[SerializeField]
	private int _actionPointSightNavMeshArea;

	[SerializeField]
	private float _catEventBaseProb = 1f;

	[SerializeField]
	private Dictionary<int, PoseKeyPair> _tutorialIdlePoseTable = new Dictionary<int, PoseKeyPair>();

	[SerializeField]
	private Dictionary<int, PoseKeyPair> _tutorialWakeUpPoseTable = new Dictionary<int, PoseKeyPair>();

	[SerializeField]
	private TutorialSetting _tutorial;

	public ReadOnlyDictionary<EventType, EventType> AfterActionTable => _afterActionReadOnlyTable ?? (_afterActionReadOnlyTable = new ReadOnlyDictionary<EventType, EventType>(_afterActionTable));

	public List<Desire.ActionType> EncounterWhitelist
	{
		[CompilerGenerated]
		get
		{
			return _encounterWhitelist;
		}
	}

	public List<Desire.ActionType> ScrollWhitelist
	{
		[CompilerGenerated]
		get
		{
			return _strollWhitelist;
		}
	}

	public List<Desire.ActionType> BlackListInSaveAndLoad
	{
		[CompilerGenerated]
		get
		{
			return _blackListInSaveAndLoad;
		}
	}

	public Dictionary<int, int> DefaultAreaIDTable
	{
		[CompilerGenerated]
		get
		{
			return _defaultAreaIDTable;
		}
	}

	public WalkParameter WalkSetting
	{
		[CompilerGenerated]
		get
		{
			return _walkSetting;
		}
	}

	public int AvoidancePriorityDefault
	{
		[CompilerGenerated]
		get
		{
			return _avoidancePriorityDefault;
		}
	}

	public int AvoidancePriorityStationary
	{
		[CompilerGenerated]
		get
		{
			return _avoidancePriorityStationary;
		}
	}

	public float ActionPointNearDistance
	{
		[CompilerGenerated]
		get
		{
			return _actionPointNearDistance;
		}
	}

	public ThresholdInt EscapeViaPointNumThreshold
	{
		[CompilerGenerated]
		get
		{
			return _escapeViaPointNumThreshold;
		}
	}

	public PoseIDCollection PoseIDTable
	{
		[CompilerGenerated]
		get
		{
			return _poseIDTable;
		}
	}

	public Dictionary<int, PoseKeyPair> ADVIdleTable
	{
		[CompilerGenerated]
		get
		{
			return _advIdleTable;
		}
	}

	public Dictionary<int, PoseKeyPair> ADVHouchiTable
	{
		[CompilerGenerated]
		get
		{
			return _advHouchiTable;
		}
	}

	public Dictionary<int, PoseKeyPair> ADVLeaveTable
	{
		[CompilerGenerated]
		get
		{
			return _advLeaveTable;
		}
	}

	public Dictionary<int, PoseKeyPair> ADVBreastTable
	{
		[CompilerGenerated]
		get
		{
			return _advBreastTable;
		}
	}

	public Dictionary<int, PoseKeyPair> ADVBreastNoReactionTable
	{
		[CompilerGenerated]
		get
		{
			return _advBreastNoReactionTable;
		}
	}

	public NormalSkillIDDefines NormalSkillIDSetting
	{
		[CompilerGenerated]
		get
		{
			return _normalSkillIDSetting;
		}
	}

	public HSkillIDDefines HSkillIDSetting
	{
		[CompilerGenerated]
		get
		{
			return _hSkillIDSetting;
		}
	}

	public float TurnMinAngle
	{
		[CompilerGenerated]
		get
		{
			return _turnMinAngle;
		}
	}

	public RangeParameter RangeSetting
	{
		[CompilerGenerated]
		get
		{
			return _rangeSetting;
		}
	}

	public PhotoShotRangeParameter PhotoShotRangeSetting
	{
		[CompilerGenerated]
		get
		{
			return _photoShotRangeSetting;
		}
	}

	public ActionPointSightSetting ActionPointSight
	{
		[CompilerGenerated]
		get
		{
			return _actionPointSight;
		}
	}

	public int DefaultRelationShip
	{
		[CompilerGenerated]
		get
		{
			return _defaultRelationShip;
		}
	}

	public float HSampleDistance
	{
		[CompilerGenerated]
		get
		{
			return _hSampleDistance;
		}
	}

	public SightSetting CharacterFarSight
	{
		[CompilerGenerated]
		get
		{
			return _characterFarSight;
		}
	}

	public SightSetting CharacterNearSight
	{
		[CompilerGenerated]
		get
		{
			return _characterNearSight;
		}
	}

	public SightSetting AnimalSight
	{
		[CompilerGenerated]
		get
		{
			return _animalSight;
		}
	}

	public float DurationCTForCall
	{
		[CompilerGenerated]
		get
		{
			return _durationCTForCall;
		}
	}

	public float TalkLockDuration
	{
		[CompilerGenerated]
		get
		{
			return _talkLockDuration;
		}
	}

	public DiminuationRates DiminuationRate
	{
		[CompilerGenerated]
		get
		{
			return _diminuationRate;
		}
	}

	public Dictionary<int, DiminuationRates> DiminMotivationRate
	{
		[CompilerGenerated]
		get
		{
			return _diminMotivationRate;
		}
	}

	public DiminuationRates WeaknessDiminuationRate
	{
		[CompilerGenerated]
		get
		{
			return _weaknessDiminuationRate;
		}
	}

	public DiminuationRates TalkMotivationDimRate
	{
		[CompilerGenerated]
		get
		{
			return _talkMotivationDimRate;
		}
	}

	public ThresholdInt SecondSleepDurationMinMax
	{
		[CompilerGenerated]
		get
		{
			return _secondSleepDurationMinMax;
		}
	}

	public Threshold StandDurationMinMax
	{
		[CompilerGenerated]
		get
		{
			return _standDurationMinMax;
		}
	}

	public ItemIDKeyPair[] CanStandEatItems
	{
		[CompilerGenerated]
		get
		{
			return _canStandEatItems;
		}
	}

	public ItemIDKeyPair[] LowerTempDrinkItems
	{
		[CompilerGenerated]
		get
		{
			return _lowerTempDrinkItems;
		}
	}

	public ItemIDKeyPair[] RaiseTempDrinkItems
	{
		[CompilerGenerated]
		get
		{
			return _raiseTempDrinkItems;
		}
	}

	public ItemIDKeyPair CoconutDrinkID
	{
		[CompilerGenerated]
		get
		{
			return _coconutDrinkID;
		}
	}

	public float ColdTempBorder
	{
		[CompilerGenerated]
		get
		{
			return _coldTempBorder;
		}
	}

	public float HotTempBorder
	{
		[CompilerGenerated]
		get
		{
			return _hotTempBorder;
		}
	}

	public Dictionary<int, float> MotivationMinValueTable
	{
		[CompilerGenerated]
		get
		{
			return _motivationMinValueTable;
		}
	}

	public float ActiveMotivationBorder
	{
		[CompilerGenerated]
		get
		{
			return _activeMotivationBorder;
		}
	}

	public float MustRunMotivationPercent
	{
		[CompilerGenerated]
		get
		{
			return _mustRunMotivationPercent;
		}
	}

	public Threshold DiminuationInMasturbation
	{
		[CompilerGenerated]
		get
		{
			return _diminuationInMasturbation;
		}
	}

	public Threshold DiminuationInLesbian
	{
		[CompilerGenerated]
		get
		{
			return _diminuationInLesbian;
		}
	}

	public int ItemSlotMaxInInventory
	{
		[CompilerGenerated]
		get
		{
			return _itemSlotMaxInInventory;
		}
	}

	public int ItemSlotCountToItemBox
	{
		[CompilerGenerated]
		get
		{
			return _itemSlotCountToItemBox;
		}
	}

	public InventoryFacadeViewer.ItemFilter[] PresentItemFilter
	{
		[CompilerGenerated]
		get
		{
			return _presentItemFilter;
		}
	}

	public ItemIDKeyPair[] MedicineNormalItemList
	{
		[CompilerGenerated]
		get
		{
			return _medicineNormalItemList;
		}
	}

	public ItemIDKeyPair[] MedicineColdItemList
	{
		[CompilerGenerated]
		get
		{
			return _medicineColdItemList;
		}
	}

	public ItemIDKeyPair[] MedicineHurtItemList
	{
		[CompilerGenerated]
		get
		{
			return _medicineHurtItemList;
		}
	}

	public ItemIDKeyPair[] MedicineStomachacheItemList
	{
		[CompilerGenerated]
		get
		{
			return _medicineStomachacheItemList;
		}
	}

	public ItemIDKeyPair[] MedicineHeatStrokeItemList
	{
		[CompilerGenerated]
		get
		{
			return _medicineHeatStrokeItemList;
		}
	}

	public ItemIDKeyPair FeverReducerID
	{
		[CompilerGenerated]
		get
		{
			return _feverReducerID;
		}
	}

	public ItemIDKeyPair ColdMedicineID
	{
		[CompilerGenerated]
		get
		{
			return _coldMedicineID;
		}
	}

	public ItemIDKeyPair StomachMedicineID
	{
		[CompilerGenerated]
		get
		{
			return _stomachMeidicineID;
		}
	}

	public ItemIDKeyPair WetTowelID
	{
		[CompilerGenerated]
		get
		{
			return _wetTowelID;
		}
	}

	public Vector3 OffsetInDate
	{
		[CompilerGenerated]
		get
		{
			return _offsetInDate;
		}
	}

	public Vector3 OffsetInPartyS
	{
		[CompilerGenerated]
		get
		{
			return _offsetInPartyS;
		}
	}

	public Vector3 OffsetInPartyM
	{
		[CompilerGenerated]
		get
		{
			return _offsetInPartyM;
		}
	}

	public Vector3 OffsetInPartyL
	{
		[CompilerGenerated]
		get
		{
			return Vector3.zero;
		}
	}

	public float RestDistance
	{
		[CompilerGenerated]
		get
		{
			return _restDistance;
		}
	}

	public float RunDistance
	{
		[CompilerGenerated]
		get
		{
			return _runDistance;
		}
	}

	public int ActionPointSightNavMeshArea
	{
		[CompilerGenerated]
		get
		{
			return _actionPointSightNavMeshArea;
		}
	}

	public float CatEventBaseProb
	{
		[CompilerGenerated]
		get
		{
			return _catEventBaseProb;
		}
	}

	public Dictionary<int, PoseKeyPair> TutorialIdlePoseTable
	{
		[CompilerGenerated]
		get
		{
			return _tutorialIdlePoseTable;
		}
	}

	public Dictionary<int, PoseKeyPair> TutorialWakeUpPoseTable
	{
		[CompilerGenerated]
		get
		{
			return _tutorialWakeUpPoseTable;
		}
	}

	public TutorialSetting Tutorial
	{
		[CompilerGenerated]
		get
		{
			return _tutorial;
		}
	}

	public Vector3 GetOffsetInParty(float rate)
	{
		if (rate < 0.5f)
		{
			float t = Mathf.InverseLerp(0f, 0.5f, rate);
			return Vector3.Lerp(_offsetInPartyS, _offsetInPartyM, t);
		}
		float t2 = Mathf.InverseLerp(0.5f, 1f, rate);
		return Vector3.Lerp(_offsetInPartyM, _offsetInPartyL, t2);
	}
}
