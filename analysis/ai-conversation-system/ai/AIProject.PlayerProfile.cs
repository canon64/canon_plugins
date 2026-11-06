using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AIProject;

public class PlayerProfile : SerializedScriptableObject
{
	[Serializable]
	public class PoseIDCollection
	{
		[SerializeField]
		private PoseKeyPair _leftPoseID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _menuPoseID = default(PoseKeyPair);

		[SerializeField]
		private PoseKeyPair _wakeupPoseID = default(PoseKeyPair);

		[SerializeField]
		private string _normalLocoStateName = string.Empty;

		[SerializeField]
		private int _torchLocoID;

		[SerializeField]
		private int _lampLocoID;

		[SerializeField]
		private int _torchOnbuLocoID;

		[SerializeField]
		private int _lampOnbuLocoID;

		public PoseKeyPair LeftPoseID
		{
			[CompilerGenerated]
			get
			{
				return _leftPoseID;
			}
		}

		public PoseKeyPair MenuPoseID
		{
			[CompilerGenerated]
			get
			{
				return _menuPoseID;
			}
		}

		public PoseKeyPair WakeupPoseID
		{
			[CompilerGenerated]
			get
			{
				return _wakeupPoseID;
			}
		}

		public string NormalLocoStateName
		{
			[CompilerGenerated]
			get
			{
				return _normalLocoStateName;
			}
		}

		public int TorchLocoID
		{
			[CompilerGenerated]
			get
			{
				return _torchLocoID;
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

		public int TorchOnbuLocoID
		{
			[CompilerGenerated]
			get
			{
				return _torchOnbuLocoID;
			}
		}

		public int LampOnbuLocoID
		{
			[CompilerGenerated]
			get
			{
				return _lampOnbuLocoID;
			}
		}
	}

	[SerializeField]
	private int _defaultInventoryMaxSlot = 1;

	[SerializeField]
	private int _agentInventoryMaxSlot = 1;

	[SerializeField]
	private int _commonActionIconID;

	[SerializeField]
	private PoseIDCollection _poseIDData;

	[SerializeField]
	private List<int> _exPantryCommandActPTIDs = new List<int>();

	[SerializeField]
	private Dictionary<int, int[]> _disableWaterVFXAreaList = new Dictionary<int, int[]>();

	[SerializeField]
	[DisableInPlayMode]
	private EnvironmentSimulator.DateTimeThreshold[] _canSleepTime;

	[SerializeField]
	[DisableInPlayMode]
	private EnvironmentSimulator.DateTimeSerialization _wakeTime = default(EnvironmentSimulator.DateTimeSerialization);

	[SerializeField]
	[DisableInPlayMode]
	private int _hizamakuraPTID;

	public int DefaultInventoryMax
	{
		[CompilerGenerated]
		get
		{
			return _defaultInventoryMaxSlot;
		}
	}

	public int AgentInventoryMaxSlot
	{
		[CompilerGenerated]
		get
		{
			return _agentInventoryMaxSlot;
		}
	}

	public int CommonActionIconID
	{
		[CompilerGenerated]
		get
		{
			return _commonActionIconID;
		}
	}

	public PoseIDCollection PoseIDData
	{
		[CompilerGenerated]
		get
		{
			return _poseIDData;
		}
	}

	public List<int> ExPantryCommandActPTIDs
	{
		[CompilerGenerated]
		get
		{
			return _exPantryCommandActPTIDs;
		}
	}

	public Dictionary<int, int[]> DisableWaterVFXAreaList
	{
		[CompilerGenerated]
		get
		{
			return _disableWaterVFXAreaList;
		}
	}

	public EnvironmentSimulator.DateTimeThreshold[] CanSleepTime
	{
		[CompilerGenerated]
		get
		{
			return _canSleepTime;
		}
	}

	public EnvironmentSimulator.DateTimeSerialization WakeTime
	{
		[CompilerGenerated]
		get
		{
			return _wakeTime;
		}
	}

	public int HizamakuraPTID
	{
		[CompilerGenerated]
		get
		{
			return _hizamakuraPTID;
		}
	}
}
