using System;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AIProject;

public class StatusProfile : SerializedScriptableObject
{
	[SerializeField]
	private Threshold _cookPheromoneBuffMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _cookPheromoneBuff = new Threshold(0f, 1f);

	[SerializeField]
	private float _buffCook;

	[SerializeField]
	private float _buffBath;

	[SerializeField]
	private float _buffAnimal;

	[SerializeField]
	private float _buffSleep;

	[SerializeField]
	private Threshold _sleepSociabilityBuffMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _sleepSociabilityBuff = new Threshold(0f, 1f);

	[SerializeField]
	private float _buffGift;

	[SerializeField]
	private Threshold _giftReliabilityBuffMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _giftReliabilityBuff = new Threshold(0f, 1f);

	[SerializeField]
	private float _buffGimme;

	[SerializeField]
	private Threshold _gimmeDarknessBuffMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _gimmeDarknessBuff = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _gimmeWarinessBuffMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _gimmeWarinessBuff = new Threshold(0f, 1f);

	[SerializeField]
	private float _buffEat;

	[SerializeField]
	private Threshold _eatPheromoneDebuffMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _eatPheromoneDebuff = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _eatDarknessDebuffMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _eatDarknessDebuff = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _eatInstinctBuffMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _eatInstinctBuff = new Threshold(0f, 1f);

	[SerializeField]
	private float _buffPlay;

	[SerializeField]
	private Threshold _playReasonDebuffMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _playReasonDebuff = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _playInstinctBuffMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _playInstinctBuff = new Threshold(0f, 1f);

	[SerializeField]
	private float _buffH;

	[SerializeField]
	private Threshold _hDirtyBuffMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _hDirtyBuff = new Threshold(0f, 1f);

	[SerializeField]
	private float _cursedHBuff;

	[SerializeField]
	private float _buffLonely;

	[SerializeField]
	private float _buffLonelySuperSense;

	[SerializeField]
	private Threshold _lonelySociabilityBuffMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _lonelySociabilityBuff = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _breakReasonBuffMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _breakReasonBuff = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _breakInstinctBuffMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _breakInstinctBuff = new Threshold(0f, 1f);

	[SerializeField]
	private float _buffBreak;

	[SerializeField]
	private float _buffLocation;

	[SerializeField]
	private float _buffSearchTough;

	[SerializeField]
	private float _buffSearch;

	[SerializeField]
	private Threshold _searchWarinessBuffMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _searchWarinessBuff = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _searchDarknessDebuffMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _searchDarknessDebuff = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _drinkWarinessBuffMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _drinkWarinessBuff = new Threshold(0f, 1f);

	[SerializeField]
	private float _debuffMood;

	[SerializeField]
	private float _debuffMoodInBathDesire;

	[SerializeField]
	private float _buffImmoral;

	[SerializeField]
	private float _gWifeMotivationBuff;

	[SerializeField]
	private float _activeBuffMotivation;

	[SerializeField]
	private float _healthyPhysicalBorder;

	[SerializeField]
	private float _cursedPhysicalBuff;

	[SerializeField]
	private Threshold _darknessPhysicalBuffMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _darknessPhysicalBuff = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _dirtyImmoralMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _dirtyImmoralBuff = new Threshold(0f, 1f);

	[SerializeField]
	private float _immoralBuff;

	[SerializeField]
	private int _lustImmoralBuff;

	[SerializeField]
	private int _firedBodyImmoralBuff;

	[SerializeField]
	private float _cursedImmoralBuff;

	[SerializeField]
	private int _lesbianFriendlyRelationBorder;

	[SerializeField]
	private float _canClothChangeBorder;

	[SerializeField]
	private Threshold _clothChangePheromoneValueMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _clothChangePheromoneValue = new Threshold(0f, 1f);

	[SerializeField]
	private int _darknessReduceMaiden;

	[SerializeField]
	private int _reliabilityGWifeBuff;

	[SerializeField]
	private int _masturbationBorder;

	[SerializeField]
	private int _invitationBorder;

	[SerializeField]
	private int _revRapeBorder;

	[SerializeField]
	private int _lesbianBorder;

	[SerializeField]
	private int _holdingHandBorderReliability;

	[SerializeField]
	private int _approachBorderReliability;

	[SerializeField]
	private int _canGreetBorder;

	[SerializeField]
	private float _canDressBorder;

	[SerializeField]
	private int _washFaceBorder;

	[SerializeField]
	private int _nightLightBorder;

	[SerializeField]
	private int _surpriseBorder;

	[SerializeField]
	private int _girlsActionBorder;

	[SerializeField]
	private int _talkRelationUpperBorder;

	[SerializeField]
	private int _lesbianSociabilityBuffBorder;

	[SerializeField]
	private Threshold _flavorCookSuccessBoostMinMax = default(Threshold);

	[SerializeField]
	private Threshold _flavorCookSuccessBoost = default(Threshold);

	[SerializeField]
	private int _chefCookSuccessBoost;

	[SerializeField]
	private Threshold _flavorCatCaptureMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _flavorCatCaptureRate = new Threshold(0f, 1f);

	[SerializeField]
	private int _catCaptureProbBuff;

	[SerializeField]
	private float _defaultInstructionRate;

	[SerializeField]
	private Threshold _flavorReliabilityInstructionMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _flavorReliabilityInstruction = new Threshold(0f, 1f);

	[SerializeField]
	private float _instructionRateDebuff;

	[SerializeField]
	private float _defaultFollowRate;

	[SerializeField]
	private Threshold _followReliabilityMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _followRateReliabilityBuff = new Threshold(0f, 1f);

	[SerializeField]
	private float _followRateBuff;

	[SerializeField]
	private Threshold _dropBuffMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _dropBuff = new Threshold(0f, 1f);

	[SerializeField]
	private float _girlsActionProb;

	[SerializeField]
	private float _lesbianRate;

	[SerializeField]
	private float _shallowSleepProb;

	[SerializeField]
	private Threshold _yobaiMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private float _callProbBaseRate;

	[SerializeField]
	private float _callProbPhaseRate;

	[SerializeField]
	private int[] _callReliabilityBorder;

	[SerializeField]
	private float[] _callReliabilityBuff;

	[SerializeField]
	private float _callLowerMoodProb;

	[SerializeField]
	private float _callUpperMoodProb;

	[SerializeField]
	private float _callSecondTimeProb;

	[SerializeField]
	private float _callOverTimeProb;

	[SerializeField]
	private float _callProbSuperSense;

	[SerializeField]
	private float _handSearchProbBuff;

	[SerializeField]
	private float _fishingSearchProbBuff;

	[SerializeField]
	private float _pickelSearchProbBuff;

	[SerializeField]
	private float _shovelSearchProbBuff;

	[SerializeField]
	private float _netSearchProbBuff;

	[SerializeField]
	private float _coldDefaultIncidence;

	[SerializeField]
	private float _coldLockDuration = 1f;

	[SerializeField]
	private int _coldBaseDuration = 1;

	[SerializeField]
	private float _heatStrokeDefaultIncidence;

	[SerializeField]
	private float _heatStrokeLockDuration = 1f;

	[SerializeField]
	private float _hurtDefaultIncidence;

	[SerializeField]
	private Threshold _stomachacheRateDebuffMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _stomachacheRateBuff = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _coldRateBuffMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _coldRateBuff = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _heatStrokeRateBuffMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _heatStrokeRateBuff = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _hurtRateBuffMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _hurtRateBuff = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _sickIncidenceDarknessBuffMinMax = new Threshold(0f, 1f);

	[SerializeField]
	private Threshold _sickIncidenceDarknessBuff = new Threshold(0f, 1f);

	[SerializeField]
	private float _coldRateDebuffWeak;

	[SerializeField]
	private float _heatStrokeBuffGuts;

	[SerializeField]
	private int _starveWarinessValue = 1;

	[SerializeField]
	private int _starveDarknessValue = 1;

	[SerializeField]
	private float _wetRateInRain;

	[SerializeField]
	private float _wetRateInStorm;

	[SerializeField]
	private float _drySpeed;

	[SerializeField]
	private float _wetTemperatureRate;

	[SerializeField]
	private float _coldTemperatureValue;

	[SerializeField]
	private float _hotTemperatureValue;

	[SerializeField]
	private float _lesbianBorderDesire;

	[SerializeField]
	private float _shallowSleepHungerLowBorder;

	[SerializeField]
	private int _lampEquipableBorder;

	[SerializeField]
	private EnvironmentSimulator.DateTimeSerialization _shouldRestoreCoordTime = new EnvironmentSimulator.DateTimeSerialization(DateTime.MinValue);

	[SerializeField]
	private float _restoreRangeMinuteTime;

	[SerializeField]
	private int _soineReliabilityBorder;

	[SerializeField]
	private float _potionImmoralAdd;

	[SerializeField]
	private float _diureticToiletAdd;

	[SerializeField]
	private float _pillSleepAdd;

	public Threshold CookPheromoneBuffMinMax
	{
		[CompilerGenerated]
		get
		{
			return _cookPheromoneBuffMinMax;
		}
	}

	public Threshold CookPheromoneBuff
	{
		[CompilerGenerated]
		get
		{
			return _cookPheromoneBuff;
		}
	}

	public float BuffCook
	{
		[CompilerGenerated]
		get
		{
			return _buffCook;
		}
	}

	public float BuffBath
	{
		[CompilerGenerated]
		get
		{
			return _buffBath;
		}
	}

	public float BuffAnimal
	{
		[CompilerGenerated]
		get
		{
			return _buffAnimal;
		}
	}

	public float BuffSleep
	{
		[CompilerGenerated]
		get
		{
			return _buffSleep;
		}
	}

	public Threshold SleepSociabilityBuffMinMax
	{
		[CompilerGenerated]
		get
		{
			return _sleepSociabilityBuffMinMax;
		}
	}

	public Threshold SleepSociabilityBuff
	{
		[CompilerGenerated]
		get
		{
			return _sleepSociabilityBuff;
		}
	}

	public float BuffGift
	{
		[CompilerGenerated]
		get
		{
			return _buffGift;
		}
	}

	public Threshold GiftReliabilityBuffMinMax
	{
		[CompilerGenerated]
		get
		{
			return _giftReliabilityBuffMinMax;
		}
	}

	public Threshold GiftReliabilityBuff
	{
		[CompilerGenerated]
		get
		{
			return _giftReliabilityBuff;
		}
	}

	public float BuffGimme
	{
		[CompilerGenerated]
		get
		{
			return _buffGimme;
		}
	}

	public Threshold GimmeDarknessBuffMinMax
	{
		[CompilerGenerated]
		get
		{
			return _gimmeDarknessBuffMinMax;
		}
	}

	public Threshold GimmeDarknessBuff
	{
		[CompilerGenerated]
		get
		{
			return _gimmeDarknessBuff;
		}
	}

	public Threshold GimmeWarinessBuffMinMax
	{
		[CompilerGenerated]
		get
		{
			return _gimmeWarinessBuffMinMax;
		}
	}

	public Threshold GimmeWarinessBuff
	{
		[CompilerGenerated]
		get
		{
			return _gimmeWarinessBuff;
		}
	}

	public float BuffEat
	{
		[CompilerGenerated]
		get
		{
			return _buffEat;
		}
	}

	public Threshold EatPheromoneDebuffMinMax
	{
		[CompilerGenerated]
		get
		{
			return _eatPheromoneDebuffMinMax;
		}
	}

	public Threshold EatPheromoneDebuff
	{
		[CompilerGenerated]
		get
		{
			return _eatPheromoneDebuff;
		}
	}

	public Threshold EatDarknessDebuffMinMax
	{
		[CompilerGenerated]
		get
		{
			return _eatDarknessDebuffMinMax;
		}
	}

	public Threshold EatDarknessDebuff
	{
		[CompilerGenerated]
		get
		{
			return _eatDarknessDebuff;
		}
	}

	public Threshold EatInstinctBuffMinMax
	{
		[CompilerGenerated]
		get
		{
			return _eatInstinctBuffMinMax;
		}
	}

	public Threshold EatInstinctBuff
	{
		[CompilerGenerated]
		get
		{
			return _eatInstinctBuff;
		}
	}

	public float BuffPlay
	{
		[CompilerGenerated]
		get
		{
			return _buffPlay;
		}
	}

	public Threshold PlayReasonDebuffMinMax
	{
		[CompilerGenerated]
		get
		{
			return _playReasonDebuffMinMax;
		}
	}

	public Threshold PlayReasonDebuff
	{
		[CompilerGenerated]
		get
		{
			return _playReasonDebuff;
		}
	}

	public Threshold PlayInstinctBuffMinMax
	{
		[CompilerGenerated]
		get
		{
			return _playInstinctBuffMinMax;
		}
	}

	public Threshold PlayInstinctBuff
	{
		[CompilerGenerated]
		get
		{
			return _playInstinctBuff;
		}
	}

	public float BuffH
	{
		[CompilerGenerated]
		get
		{
			return _buffH;
		}
	}

	public Threshold HDirtyBuffMinMax
	{
		[CompilerGenerated]
		get
		{
			return _hDirtyBuffMinMax;
		}
	}

	public Threshold HDirtyBuff
	{
		[CompilerGenerated]
		get
		{
			return _hDirtyBuff;
		}
	}

	public float CursedHBuff
	{
		[CompilerGenerated]
		get
		{
			return _cursedHBuff;
		}
	}

	public float BuffLonely
	{
		[CompilerGenerated]
		get
		{
			return _buffLonely;
		}
	}

	public float BuffLonelySuperSense
	{
		[CompilerGenerated]
		get
		{
			return _buffLonelySuperSense;
		}
	}

	public Threshold LonelySociabilityBuffMinMax
	{
		[CompilerGenerated]
		get
		{
			return _lonelySociabilityBuffMinMax;
		}
	}

	public Threshold LonelySociabilityBuff
	{
		[CompilerGenerated]
		get
		{
			return _lonelySociabilityBuff;
		}
	}

	public Threshold BreakReasonBuffMinMax
	{
		[CompilerGenerated]
		get
		{
			return _breakReasonBuffMinMax;
		}
	}

	public Threshold BreakReasonBuff
	{
		[CompilerGenerated]
		get
		{
			return _breakReasonBuff;
		}
	}

	public Threshold BreakInstinctBuffMinMax
	{
		[CompilerGenerated]
		get
		{
			return _breakInstinctBuffMinMax;
		}
	}

	public Threshold BreakInstinctBuff
	{
		[CompilerGenerated]
		get
		{
			return _breakInstinctBuff;
		}
	}

	public float BuffBreak
	{
		[CompilerGenerated]
		get
		{
			return _buffBreak;
		}
	}

	public float BuffLocation
	{
		[CompilerGenerated]
		get
		{
			return _buffLocation;
		}
	}

	public float BuffSearchTough
	{
		[CompilerGenerated]
		get
		{
			return _buffSearchTough;
		}
	}

	public float BuffSearch
	{
		[CompilerGenerated]
		get
		{
			return _buffSearch;
		}
	}

	public Threshold SearchWarinessBuffMinMax
	{
		[CompilerGenerated]
		get
		{
			return _searchWarinessBuffMinMax;
		}
	}

	public Threshold SearchWarinessBuff
	{
		[CompilerGenerated]
		get
		{
			return _searchWarinessBuff;
		}
	}

	public Threshold SearchDarknessDebuffMinMax
	{
		[CompilerGenerated]
		get
		{
			return _searchDarknessDebuffMinMax;
		}
	}

	public Threshold SearchDarknessDebuff
	{
		[CompilerGenerated]
		get
		{
			return _searchDarknessDebuff;
		}
	}

	public Threshold DrinkWarinessBuffMinMax
	{
		[CompilerGenerated]
		get
		{
			return _drinkWarinessBuffMinMax;
		}
	}

	public Threshold DrinkWarinessBuff
	{
		[CompilerGenerated]
		get
		{
			return _drinkWarinessBuff;
		}
	}

	public float DebuffMood
	{
		[CompilerGenerated]
		get
		{
			return _debuffMood;
		}
	}

	public float DebuffMoodInBathDesire
	{
		[CompilerGenerated]
		get
		{
			return _debuffMoodInBathDesire;
		}
	}

	public float BuffImmoral
	{
		[CompilerGenerated]
		get
		{
			return _buffImmoral;
		}
	}

	public float GWifeMotivationBuff
	{
		[CompilerGenerated]
		get
		{
			return _gWifeMotivationBuff;
		}
	}

	public float ActiveBuffMotivation
	{
		[CompilerGenerated]
		get
		{
			return _activeBuffMotivation;
		}
	}

	public float HealthyPhysicalBorder
	{
		[CompilerGenerated]
		get
		{
			return _healthyPhysicalBorder;
		}
	}

	public float CursedPhysicalBuff
	{
		[CompilerGenerated]
		get
		{
			return _cursedPhysicalBuff;
		}
	}

	public Threshold DarknessPhysicalBuffMinMax
	{
		[CompilerGenerated]
		get
		{
			return _darknessPhysicalBuffMinMax;
		}
	}

	public Threshold DarknessPhysicalBuff
	{
		[CompilerGenerated]
		get
		{
			return _darknessPhysicalBuff;
		}
	}

	public Threshold DirtyImmoralMinMax
	{
		[CompilerGenerated]
		get
		{
			return _dirtyImmoralMinMax;
		}
	}

	public Threshold DirtyImmoralBuff
	{
		[CompilerGenerated]
		get
		{
			return _dirtyImmoralBuff;
		}
	}

	public float ImmoralBuff
	{
		[CompilerGenerated]
		get
		{
			return _immoralBuff;
		}
	}

	public int LustImmoralBuff
	{
		[CompilerGenerated]
		get
		{
			return _lustImmoralBuff;
		}
	}

	public int FiredBodyImmoralBuff
	{
		[CompilerGenerated]
		get
		{
			return _firedBodyImmoralBuff;
		}
	}

	public float CursedImmoralBuff
	{
		[CompilerGenerated]
		get
		{
			return _cursedImmoralBuff;
		}
	}

	public int LesbianFriendlyRelationBorder
	{
		[CompilerGenerated]
		get
		{
			return _lesbianFriendlyRelationBorder;
		}
	}

	public float CanClothChangeBorder
	{
		[CompilerGenerated]
		get
		{
			return _canClothChangeBorder;
		}
	}

	public Threshold ClothChangePheromoneValueMinMax
	{
		[CompilerGenerated]
		get
		{
			return _clothChangePheromoneValueMinMax;
		}
	}

	public Threshold ClothChangePheromoneValue
	{
		[CompilerGenerated]
		get
		{
			return _clothChangePheromoneValue;
		}
	}

	public int DarknessReduceMaiden
	{
		[CompilerGenerated]
		get
		{
			return _darknessReduceMaiden;
		}
	}

	public int ReliabilityGWife
	{
		[CompilerGenerated]
		get
		{
			return _reliabilityGWifeBuff;
		}
	}

	public int MasturbationBorder
	{
		[CompilerGenerated]
		get
		{
			return _masturbationBorder;
		}
	}

	public int InvitationBorder
	{
		[CompilerGenerated]
		get
		{
			return _invitationBorder;
		}
	}

	public int RevRapeBorder
	{
		[CompilerGenerated]
		get
		{
			return _revRapeBorder;
		}
	}

	public int LesbianBorder
	{
		[CompilerGenerated]
		get
		{
			return _lesbianBorder;
		}
	}

	public int HoldingHandBorderReliability
	{
		[CompilerGenerated]
		get
		{
			return _holdingHandBorderReliability;
		}
	}

	public int ApproachBorderReliability
	{
		[CompilerGenerated]
		get
		{
			return _approachBorderReliability;
		}
	}

	public float CanGreetBorder
	{
		[CompilerGenerated]
		get
		{
			return _canGreetBorder;
		}
	}

	public float CanDressBorder
	{
		[CompilerGenerated]
		get
		{
			return _canDressBorder;
		}
	}

	public int WashFaceBorder
	{
		[CompilerGenerated]
		get
		{
			return _washFaceBorder;
		}
	}

	public int NightLightBorder
	{
		[CompilerGenerated]
		get
		{
			return _nightLightBorder;
		}
	}

	public int SurpriseBorder
	{
		[CompilerGenerated]
		get
		{
			return _surpriseBorder;
		}
	}

	public int GirlsActionBorder
	{
		[CompilerGenerated]
		get
		{
			return _girlsActionBorder;
		}
	}

	public int TalkRelationUpperBorder
	{
		[CompilerGenerated]
		get
		{
			return _talkRelationUpperBorder;
		}
	}

	public int LesbianSociabilityBuffBorder
	{
		[CompilerGenerated]
		get
		{
			return _lesbianSociabilityBuffBorder;
		}
	}

	public Threshold FlavorCookSuccessBoostMinMax
	{
		[CompilerGenerated]
		get
		{
			return _flavorCookSuccessBoostMinMax;
		}
	}

	public Threshold FlavorCookSuccessBoost
	{
		[CompilerGenerated]
		get
		{
			return _flavorCookSuccessBoost;
		}
	}

	public int ChefCookSuccessBoost
	{
		[CompilerGenerated]
		get
		{
			return _chefCookSuccessBoost;
		}
	}

	public Threshold FlavorCatCaptureMinMax
	{
		[CompilerGenerated]
		get
		{
			return _flavorCatCaptureMinMax;
		}
	}

	public Threshold FlavorCatCaptureRate
	{
		[CompilerGenerated]
		get
		{
			return _flavorCatCaptureRate;
		}
	}

	public int CatCaptureProbBuff
	{
		[CompilerGenerated]
		get
		{
			return _catCaptureProbBuff;
		}
	}

	public float DefaultInstructionRate
	{
		[CompilerGenerated]
		get
		{
			return _defaultInstructionRate;
		}
	}

	public Threshold FlavorReliabilityInstructionMinMax
	{
		[CompilerGenerated]
		get
		{
			return _flavorReliabilityInstructionMinMax;
		}
	}

	public Threshold FlavorReliabilityInstruction
	{
		[CompilerGenerated]
		get
		{
			return _flavorReliabilityInstruction;
		}
	}

	public float InstructionRateDebuff
	{
		[CompilerGenerated]
		get
		{
			return _instructionRateDebuff;
		}
	}

	public float DefaultFollowRate
	{
		[CompilerGenerated]
		get
		{
			return _defaultFollowRate;
		}
	}

	public Threshold FollowReliabilityMinMax
	{
		[CompilerGenerated]
		get
		{
			return _followReliabilityMinMax;
		}
	}

	public Threshold FollowRateReliabilityBuff
	{
		[CompilerGenerated]
		get
		{
			return _followRateReliabilityBuff;
		}
	}

	public float FollowRateBuff
	{
		[CompilerGenerated]
		get
		{
			return _followRateBuff;
		}
	}

	public Threshold DropBuffMinMax
	{
		[CompilerGenerated]
		get
		{
			return _dropBuffMinMax;
		}
	}

	public Threshold DropBuff
	{
		[CompilerGenerated]
		get
		{
			return _dropBuff;
		}
	}

	public float GirlsActionProb
	{
		[CompilerGenerated]
		get
		{
			return _girlsActionProb;
		}
	}

	public float LesbianRate
	{
		[CompilerGenerated]
		get
		{
			return _lesbianRate;
		}
	}

	public float ShallowSleepProb
	{
		[CompilerGenerated]
		get
		{
			return _shallowSleepProb;
		}
	}

	public Threshold YobaiMinMax
	{
		[CompilerGenerated]
		get
		{
			return _yobaiMinMax;
		}
	}

	public float CallProbBaseRate
	{
		[CompilerGenerated]
		get
		{
			return _callProbBaseRate;
		}
	}

	public float CallProbPhaseRate
	{
		[CompilerGenerated]
		get
		{
			return _callProbPhaseRate;
		}
	}

	public int[] CallReliabilityBorder
	{
		[CompilerGenerated]
		get
		{
			return _callReliabilityBorder;
		}
	}

	public float[] CallReliabilityBuff
	{
		[CompilerGenerated]
		get
		{
			return _callReliabilityBuff;
		}
	}

	public float CallLowerMoodProb
	{
		[CompilerGenerated]
		get
		{
			return _callLowerMoodProb;
		}
	}

	public float CallUpperMoodProb
	{
		[CompilerGenerated]
		get
		{
			return _callUpperMoodProb;
		}
	}

	public float CallSecondTimeProb
	{
		[CompilerGenerated]
		get
		{
			return _callSecondTimeProb;
		}
	}

	public float CallOverTimeProb
	{
		[CompilerGenerated]
		get
		{
			return _callOverTimeProb;
		}
	}

	public float CallProbSuperSense
	{
		[CompilerGenerated]
		get
		{
			return _callProbSuperSense;
		}
	}

	public float HandSearchProbBuff
	{
		[CompilerGenerated]
		get
		{
			return _handSearchProbBuff;
		}
	}

	public float FishingSearchProbBuff
	{
		[CompilerGenerated]
		get
		{
			return _fishingSearchProbBuff;
		}
	}

	public float PickelSearchProbBuff
	{
		[CompilerGenerated]
		get
		{
			return _pickelSearchProbBuff;
		}
	}

	public float ShovelSearchProbBuff
	{
		[CompilerGenerated]
		get
		{
			return _shovelSearchProbBuff;
		}
	}

	public float NetSearchProbBuff
	{
		[CompilerGenerated]
		get
		{
			return _netSearchProbBuff;
		}
	}

	public float ColdDefaultIncidence
	{
		[CompilerGenerated]
		get
		{
			return _coldDefaultIncidence;
		}
	}

	public float ColdLockDuration
	{
		[CompilerGenerated]
		get
		{
			return _coldLockDuration;
		}
	}

	public int ColdBaseDuration
	{
		[CompilerGenerated]
		get
		{
			return _coldBaseDuration;
		}
	}

	public float HeatStrokeDefaultIncidence
	{
		[CompilerGenerated]
		get
		{
			return _heatStrokeDefaultIncidence;
		}
	}

	public float HeatStrokeLockDuration
	{
		[CompilerGenerated]
		get
		{
			return _coldLockDuration;
		}
	}

	public float HurtDefaultIncidence
	{
		[CompilerGenerated]
		get
		{
			return _hurtDefaultIncidence;
		}
	}

	public Threshold StomachacheRateDebuffMinMax
	{
		[CompilerGenerated]
		get
		{
			return _stomachacheRateDebuffMinMax;
		}
	}

	public Threshold StomachacheRateBuff
	{
		[CompilerGenerated]
		get
		{
			return _stomachacheRateBuff;
		}
	}

	public Threshold ColdRateBuffMinMax
	{
		[CompilerGenerated]
		get
		{
			return _coldRateBuffMinMax;
		}
	}

	public Threshold ColdRateBuff
	{
		[CompilerGenerated]
		get
		{
			return _coldRateBuff;
		}
	}

	public Threshold HeatStrokeRateBuffMinMax
	{
		[CompilerGenerated]
		get
		{
			return _coldRateBuffMinMax;
		}
	}

	public Threshold HeatStrokeRateBuff
	{
		[CompilerGenerated]
		get
		{
			return _heatStrokeRateBuff;
		}
	}

	public Threshold HurtRateBuffMinMax
	{
		[CompilerGenerated]
		get
		{
			return _hurtRateBuffMinMax;
		}
	}

	public Threshold HurtRateBuff
	{
		[CompilerGenerated]
		get
		{
			return _hurtRateBuff;
		}
	}

	public Threshold SickIncidenceDarknessBuffMinMax
	{
		[CompilerGenerated]
		get
		{
			return _sickIncidenceDarknessBuffMinMax;
		}
	}

	public Threshold SickIncidenceDarknessBuff
	{
		[CompilerGenerated]
		get
		{
			return _sickIncidenceDarknessBuff;
		}
	}

	public float ColdRateDebuffWeak
	{
		[CompilerGenerated]
		get
		{
			return _coldRateDebuffWeak;
		}
	}

	public float HeatStrokeBuffGuts
	{
		[CompilerGenerated]
		get
		{
			return _heatStrokeBuffGuts;
		}
	}

	public int StarveWarinessValue
	{
		[CompilerGenerated]
		get
		{
			return _starveWarinessValue;
		}
	}

	public int StarveDarknessValue
	{
		[CompilerGenerated]
		get
		{
			return _starveDarknessValue;
		}
	}

	public float WetRateInRain
	{
		[CompilerGenerated]
		get
		{
			return _wetRateInRain;
		}
	}

	public float WetRateInStorm
	{
		[CompilerGenerated]
		get
		{
			return _wetRateInStorm;
		}
	}

	public float DrySpeed
	{
		[CompilerGenerated]
		get
		{
			return _drySpeed;
		}
	}

	public float WetTemperatureRate
	{
		[CompilerGenerated]
		get
		{
			return _wetTemperatureRate;
		}
	}

	public float ColdTemperatureValue
	{
		[CompilerGenerated]
		get
		{
			return _coldTemperatureValue;
		}
	}

	public float HotTemperatureValue
	{
		[CompilerGenerated]
		get
		{
			return _hotTemperatureValue;
		}
	}

	public float LesbianBorderDesire
	{
		[CompilerGenerated]
		get
		{
			return _lesbianBorderDesire;
		}
	}

	public float ShallowSleepHungerLowBorder
	{
		[CompilerGenerated]
		get
		{
			return _shallowSleepHungerLowBorder;
		}
	}

	public int LampEquipableBorder
	{
		[CompilerGenerated]
		get
		{
			return _lampEquipableBorder;
		}
	}

	public EnvironmentSimulator.DateTimeSerialization ShouldRestoreCoordTime
	{
		[CompilerGenerated]
		get
		{
			return _shouldRestoreCoordTime;
		}
	}

	public float RestoreRangeMinuteTime
	{
		[CompilerGenerated]
		get
		{
			return _restoreRangeMinuteTime;
		}
	}

	public int SoineReliabilityBorder
	{
		[CompilerGenerated]
		get
		{
			return _soineReliabilityBorder;
		}
	}

	public float PotionImmoralAdd
	{
		[CompilerGenerated]
		get
		{
			return _potionImmoralAdd;
		}
	}

	public float DiureticToiletAdd
	{
		[CompilerGenerated]
		get
		{
			return _diureticToiletAdd;
		}
	}

	public float PillSleepAdd
	{
		[CompilerGenerated]
		get
		{
			return _pillSleepAdd;
		}
	}
}
