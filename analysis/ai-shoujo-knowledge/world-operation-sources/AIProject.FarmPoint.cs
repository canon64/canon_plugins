using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AIProject.Animal;
using AIProject.Player;
using AIProject.SaveData;
using Housing;
using IllusionUtility.GetUtility;
using Manager;
using UnityEngine;
using UnityEngine.AI;

namespace AIProject;

public class FarmPoint : Point, ICommandable
{
	public enum FarmKind
	{
		Plant,
		ChickenCoop,
		Well
	}

	[SerializeField]
	private int _id;

	[SerializeField]
	private FarmSection[] _harvestSections;

	[SerializeField]
	private bool _enabledRangeCheck = true;

	[SerializeField]
	private float _radius = 1f;

	[SerializeField]
	private FarmKind _farmKind;

	[SerializeField]
	private Transform _commandBasePoint;

	private int? _hashCode;

	private NavMeshPath _pathForCalc;

	private CommandLabel.CommandInfo[] _labels;

	private List<Vector3> _chickenWaypointPositionList;

	private Transform _chickenWaypointRoot;

	private List<Waypoint> _chickenWaypointList = new List<Waypoint>();

	private List<Waypoint> _chickenWaypointCacheList = new List<Waypoint>();

	private static RaycastHit[] _chickenWaypointRaycastHits = new RaycastHit[10];

	private Transform _animalRoot;

	public override int RegisterID
	{
		get
		{
			return _id;
		}
		set
		{
			_id = value;
		}
	}

	public FarmSection[] HarvestSections
	{
		[CompilerGenerated]
		get
		{
			return _harvestSections;
		}
	}

	public float Radius
	{
		[CompilerGenerated]
		get
		{
			return _radius;
		}
	}

	public FarmKind Kind
	{
		[CompilerGenerated]
		get
		{
			return _farmKind;
		}
	}

	public int AreaIDOnHousingArea { get; set; } = -1;

	public int InstanceID
	{
		get
		{
			if (!_hashCode.HasValue)
			{
				_hashCode = GetInstanceID();
			}
			return _hashCode.Value;
		}
	}

	public bool IsImpossible { get; private set; }

	public bool IsNeutralCommand
	{
		get
		{
			if (TutorialHideMode())
			{
				return false;
			}
			return true;
		}
	}

	public Vector3 Position
	{
		[CompilerGenerated]
		get
		{
			return base.transform.position;
		}
	}

	public Vector3 CommandCenter
	{
		get
		{
			if (_commandBasePoint != null)
			{
				return _commandBasePoint.position;
			}
			return base.transform.position;
		}
	}

	public List<Transform> NavMeshPoints { get; set; } = new List<Transform>();

	public CommandLabel.CommandInfo[] Labels
	{
		get
		{
			PlayerActor playerActor = ((!Singleton<Map>.IsInstance()) ? null : Singleton<Map>.Instance.Player);
			if (playerActor != null && playerActor.PlayerController.State is Onbu)
			{
				return null;
			}
			return _labels;
		}
	}

	public CommandLabel.CommandInfo[] DateLabels { get; private set; }

	public ObjectLayer Layer { get; } = ObjectLayer.Command;

	public CommandType CommandType { get; }

	public IReadOnlyList<Vector3> ChickenWaypointPositionList
	{
		[CompilerGenerated]
		get
		{
			return _chickenWaypointPositionList;
		}
	}

	public IReadOnlyList<Waypoint> ChickenWaypointList
	{
		[CompilerGenerated]
		get
		{
			return _chickenWaypointList;
		}
	}

	public Transform AnimalRoot
	{
		get
		{
			if (_animalRoot == null)
			{
				_animalRoot = new GameObject("Animal Root").transform;
				ItemComponent componentInParent = GetComponentInParent<ItemComponent>();
				Transform parent = ((!(componentInParent != null) || !componentInParent.transform) ? base.transform : componentInParent.transform);
				_animalRoot.SetParent(parent, worldPositionStays: false);
			}
			return _animalRoot;
		}
	}

	public List<PetChicken> ChickenList { get; private set; }

	public bool SetImpossible(bool value, Actor actor)
	{
		return true;
	}

	public bool Entered(Vector3 basePosition, float distance, float radiusA, float radiusB, float angle, Vector3 forward)
	{
		if (TutorialHideMode())
		{
			return false;
		}
		if (distance > radiusA)
		{
			return false;
		}
		Vector3 commandCenter = CommandCenter;
		commandCenter.y = 0f;
		float num = angle / 2f;
		float num2 = Vector3.Angle(commandCenter - basePosition, forward);
		if (num2 > num)
		{
			return false;
		}
		return true;
	}

	public bool IsReachable(NavMeshAgent nmAgent, float radiusA, float radiusB)
	{
		if (_pathForCalc == null)
		{
			_pathForCalc = new NavMeshPath();
		}
		bool result = true;
		if (nmAgent.isActiveAndEnabled)
		{
			bool flag = false;
			foreach (Transform navMeshPoint in NavMeshPoints)
			{
				nmAgent.CalculatePath(Position, _pathForCalc);
				if (_pathForCalc.status == NavMeshPathStatus.PathComplete)
				{
					float num = 0f;
					Vector3[] corners = _pathForCalc.corners;
					float num2 = ((CommandType != CommandType.Forward) ? radiusB : radiusA);
					num2 += _radius;
					for (int i = 0; i < corners.Length - 1; i++)
					{
						float num3 = Vector3.Distance(corners[i], corners[i + 1]);
						num += num3;
					}
					if (num < num2)
					{
						flag = true;
						break;
					}
					if (!flag)
					{
						result = false;
					}
				}
			}
		}
		else
		{
			result = false;
		}
		return result;
	}

	public bool TutorialHideMode()
	{
		if (Map.TutorialMode)
		{
			return true;
		}
		return false;
	}

	private void Awake()
	{
		_harvestSections = GetComponentsInChildren<FarmSection>();
		int num = 0;
		FarmSection[] harvestSections = _harvestSections;
		foreach (FarmSection farmSection in harvestSections)
		{
			farmSection.HarvestID = _id;
			farmSection.SectionID = num++;
		}
	}

	protected override void Start()
	{
		CommonDefine.CommonIconGroup icon = Singleton<Manager.Resources>.Instance.CommonDefine.Icon;
		DefinePack.MapGroup mapDefines = Singleton<Manager.Resources>.Instance.DefinePack.MapDefines;
		if (_commandBasePoint == null)
		{
			_commandBasePoint = base.transform.FindLoop(Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.CommandTargetName)?.transform ?? base.transform;
		}
		base.Start();
		NavMeshPoints.Add(base.transform);
		List<GameObject> list = ListPool<GameObject>.Get();
		base.transform.FindLoopPrefix(list, mapDefines.NavMeshTargetName);
		if (!list.IsNullOrEmpty())
		{
			foreach (GameObject item in list)
			{
				NavMeshPoints.Add(item.transform);
			}
		}
		ListPool<GameObject>.Release(list);
		int farmIconID = Singleton<Manager.Resources>.Instance.CommonDefine.Icon.FarmIconID;
		Singleton<Manager.Resources>.Instance.itemIconTables.ActionIconTable.TryGetValue(farmIconID, out var value);
		int chickenCoopIconID = Singleton<Manager.Resources>.Instance.CommonDefine.Icon.ChickenCoopIconID;
		Singleton<Manager.Resources>.Instance.itemIconTables.ActionIconTable.TryGetValue(chickenCoopIconID, out var value2);
		int wellIconID = Singleton<Manager.Resources>.Instance.CommonDefine.Icon.WellIconID;
		Singleton<Manager.Resources>.Instance.itemIconTables.ActionIconTable.TryGetValue(wellIconID, out var _);
		Transform transform = base.transform.FindLoop(mapDefines.FarmPointLabelTargetName)?.transform ?? base.transform;
		if (_farmKind == FarmKind.Plant)
		{
			_labels = new CommandLabel.CommandInfo[1]
			{
				new CommandLabel.CommandInfo
				{
					Text = "畑",
					Icon = value,
					IsHold = true,
					TargetSpriteInfo = icon.ActionSpriteInfo,
					Transform = transform,
					Condition = (PlayerActor x) => _farmKind == FarmKind.Plant,
					Event = delegate
					{
						MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.None);
						if (!Singleton<Game>.Instance.Environment.FarmlandTable.TryGetValue(_id, out var value4))
						{
							List<Environment.PlantInfo> list2 = new List<Environment.PlantInfo>();
							Singleton<Game>.Instance.Environment.FarmlandTable[_id] = list2;
							value4 = list2;
							FarmSection[] harvestSections = _harvestSections;
							foreach (FarmSection farmSection in harvestSections)
							{
								value4.Add(null);
							}
						}
						MapUIContainer.FarmlandUI.currentPlant = value4;
						MapUIContainer.SetActiveFarmlandUI(active: true);
						Singleton<Map>.Instance.Player.Controller.ChangeState("Harvest");
					}
				}
			};
		}
		else if (_farmKind == FarmKind.ChickenCoop)
		{
			_labels = new CommandLabel.CommandInfo[1]
			{
				new CommandLabel.CommandInfo
				{
					Text = "鶏小屋",
					Icon = value2,
					IsHold = true,
					TargetSpriteInfo = icon.ActionSpriteInfo,
					Transform = transform,
					Condition = (PlayerActor x) => _farmKind == FarmKind.ChickenCoop,
					Event = delegate
					{
						PlayerActor playerActor = ((!Singleton<Map>.IsInstance()) ? null : Singleton<Map>.Instance.Player);
						if (playerActor != null)
						{
							playerActor.CurrentFarmPoint = this;
							playerActor.PlayerController.ChangeState("ChickenCoopMenu");
						}
					}
				}
			};
		}
		else if (_farmKind != FarmKind.Well)
		{
		}
	}

	private int GetHousingAreaID()
	{
		if (!Singleton<Manager.Resources>.IsInstance())
		{
			return -1;
		}
		ItemComponent componentInParent = GetComponentInParent<ItemComponent>();
		if (componentInParent == null)
		{
			return -1;
		}
		Vector3 origin = componentInParent.position + Vector3.up * 5f;
		LayerMask areaDetectionLayer = Singleton<Manager.Resources>.Instance.DefinePack.MapDefines.AreaDetectionLayer;
		int a = Physics.RaycastNonAlloc(origin, Vector3.down, Point._raycastHits, 1000f, areaDetectionLayer);
		a = Mathf.Min(a, Point._raycastHits.Length);
		if (a <= 0)
		{
			return -1;
		}
		for (int i = 0; i < a; i++)
		{
			RaycastHit raycastHit = Point._raycastHits[i];
			if (!(raycastHit.transform == null))
			{
				MapArea componentInParent2 = raycastHit.transform.GetComponentInParent<MapArea>();
				if (!(componentInParent2 == null))
				{
					return componentInParent2.AreaID;
				}
			}
		}
		return -1;
	}

	public void SetChickenWayPoint()
	{
		if (_farmKind != FarmKind.ChickenCoop)
		{
			return;
		}
		AreaIDOnHousingArea = GetHousingAreaID();
		if (!Singleton<Manager.Resources>.IsInstance())
		{
			return;
		}
		ItemComponent componentInParent = GetComponentInParent<ItemComponent>();
		Collider[] array = componentInParent?.GetComponentsInChildren<Collider>();
		if (array.IsNullOrEmpty())
		{
			return;
		}
		AnimalDefinePack.ChickenCoopWaypointSettings chickenCoopWaypointSetting = Singleton<Manager.Resources>.Instance.AnimalDefinePack.ChickenCoopWaypointSetting;
		Collider collider = null;
		LayerMask layer = chickenCoopWaypointSetting.Layer;
		string tagName = chickenCoopWaypointSetting.TagName;
		Collider[] array2 = array;
		foreach (Collider collider2 in array2)
		{
			if (!(collider2 == null) && !(collider2.gameObject == null))
			{
				int num = 1 << collider2.gameObject.layer;
				if ((num & (int)layer) != 0 && collider2.gameObject.tag == tagName)
				{
					collider = collider2;
					break;
				}
			}
		}
		if (collider == null)
		{
			return;
		}
		Transform parent = componentInParent.transform;
		Vector3 position = collider.transform.position;
		Quaternion rotation = collider.transform.rotation;
		if (_chickenWaypointPositionList == null)
		{
			_chickenWaypointPositionList = new List<Vector3>();
		}
		else
		{
			_chickenWaypointPositionList.Clear();
		}
		if (_chickenWaypointRoot == null)
		{
			_chickenWaypointRoot = new GameObject("Chicken Waypoint Root").transform;
			_chickenWaypointRoot.SetParent(parent, worldPositionStays: false);
			Transform chickenWaypointRoot = _chickenWaypointRoot;
			Vector3 zero = Vector3.zero;
			_chickenWaypointRoot.localEulerAngles = zero;
			chickenWaypointRoot.localPosition = zero;
		}
		Vector3 installation = chickenCoopWaypointSetting.Installation;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		while (++num4 <= 999999)
		{
			int num5 = 0;
			int num6 = 0;
			while (true)
			{
				Vector3 a = new Vector3(ReversiNumber(num3), 0f, ReversiNumber(num6));
				a = Vector3.Scale(a, installation);
				a = position + rotation * a;
				int b = Physics.RaycastNonAlloc(a + Vector3.up, Vector3.down, _chickenWaypointRaycastHits, 2f, layer);
				b = Mathf.Min(_chickenWaypointRaycastHits.Length, b);
				bool flag = 0 < b;
				if (flag)
				{
					flag = false;
					for (int j = 0; j < b; j++)
					{
						RaycastHit raycastHit = _chickenWaypointRaycastHits[j];
						if (collider == raycastHit.collider)
						{
							flag = true;
							break;
						}
					}
				}
				if (!flag)
				{
					num5++;
					if (num6 == 0)
					{
						num2++;
						break;
					}
				}
				else
				{
					num2 = 0;
					num5 = 0;
					_chickenWaypointPositionList.Add(a);
				}
				if (2 <= num5)
				{
					break;
				}
				num6++;
			}
			num3++;
			if (2 <= num2)
			{
				break;
			}
		}
		foreach (Waypoint chickenWaypoint in _chickenWaypointList)
		{
			ReturnChickenWaypoint(chickenWaypoint);
		}
		_chickenWaypointList.Clear();
		if (_chickenWaypointPositionList.IsNullOrEmpty())
		{
			return;
		}
		float closestEdgeDistance = chickenCoopWaypointSetting.ClosestEdgeDistance;
		float sampleDistance = chickenCoopWaypointSetting.SampleDistance;
		int agentAreaMask = chickenCoopWaypointSetting.AgentAreaMask;
		for (int k = 0; k < _chickenWaypointPositionList.Count; k++)
		{
			Vector3 vector = _chickenWaypointPositionList[k];
			bool flag2 = true;
			flag2 &= NavMesh.SamplePosition(vector, out var hit, sampleDistance, agentAreaMask);
			if (flag2)
			{
				vector = hit.position;
			}
			if (flag2)
			{
				flag2 &= NavMesh.FindClosestEdge(vector, out hit, agentAreaMask);
			}
			if (flag2)
			{
				flag2 &= closestEdgeDistance <= hit.distance;
			}
			if (flag2)
			{
				int num7 = Physics.RaycastNonAlloc(vector + Vector3.up, Vector3.down, _chickenWaypointRaycastHits, sampleDistance * 2f, layer);
				flag2 = 0 < num7;
				if (flag2)
				{
					num7 = Mathf.Min(num7, _chickenWaypointRaycastHits.Length);
					for (int l = 0; l < num7; l++)
					{
						RaycastHit raycastHit2 = _chickenWaypointRaycastHits[l];
						if (flag2 = collider == raycastHit2.collider)
						{
							break;
						}
					}
				}
			}
			if (flag2)
			{
				Waypoint orCreateChickenWaypoint = GetOrCreateChickenWaypoint(k);
				orCreateChickenWaypoint.transform.position = vector;
				if (!orCreateChickenWaypoint.gameObject.activeSelf)
				{
					orCreateChickenWaypoint.gameObject.SetActive(value: true);
				}
				_chickenWaypointList.Add(orCreateChickenWaypoint);
			}
		}
	}

	private int ReversiNumber(int num)
	{
		return (num % 2 != 0) ? (num - num / 2) : (num * -1 + num / 2);
	}

	private Waypoint GetOrCreateChickenWaypoint(int id)
	{
		Waypoint waypoint = _chickenWaypointCacheList.PopFront();
		if (waypoint != null)
		{
			return waypoint;
		}
		GameObject gameObject = new GameObject($"chicken_waypoint_{id:00}");
		waypoint = gameObject.GetOrAddComponent<Waypoint>();
		waypoint.GroupID = RegisterID;
		waypoint.ID = id;
		waypoint.transform.SetParent(_chickenWaypointRoot, worldPositionStays: false);
		waypoint.Affiliation = Waypoint.AffiliationType.Item;
		return waypoint;
	}

	private bool ReturnChickenWaypoint(Waypoint point)
	{
		if (point == null || point.gameObject == null)
		{
			return false;
		}
		if (!_chickenWaypointCacheList.Contains(point))
		{
			if (point.gameObject.activeSelf)
			{
				point.gameObject.SetActive(value: false);
			}
			_chickenWaypointCacheList.Add(point);
			return true;
		}
		return false;
	}

	public void ClearChickenWayPoint()
	{
		if (!(_chickenWaypointRoot == null))
		{
			Object.Destroy(_chickenWaypointRoot.gameObject);
			_chickenWaypointRoot = null;
			if (_chickenWaypointPositionList != null)
			{
				_chickenWaypointPositionList.Clear();
			}
			_chickenWaypointPositionList = null;
			_chickenWaypointList.Clear();
			_chickenWaypointCacheList.Clear();
		}
	}

	public void CreateChicken()
	{
		if (_farmKind != FarmKind.ChickenCoop)
		{
			return;
		}
		if (ChickenList != null)
		{
			ChickenList.RemoveAll((PetChicken x) => x == null);
		}
		else
		{
			if (!Singleton<Game>.IsInstance() || !Singleton<AnimalManager>.IsInstance())
			{
				return;
			}
			Environment environment = Singleton<Game>.Instance.Environment;
			if (environment == null)
			{
				return;
			}
			ChickenList = ListPool<PetChicken>.Get();
			Dictionary<int, List<Environment.ChickenInfo>> chickenTable = environment.ChickenTable;
			if (chickenTable == null)
			{
				return;
			}
			List<Environment.ChickenInfo> value = null;
			if (!chickenTable.TryGetValue(RegisterID, out value) || value.IsNullOrEmpty())
			{
				if (Singleton<Map>.IsInstance())
				{
					int mapID = Singleton<Map>.Instance.MapID;
					int areaIDOnHousingArea = AreaIDOnHousingArea;
					Dictionary<int, Dictionary<int, Dictionary<int, AIProject.SaveData.AnimalData>>> housingChickenDataTable = environment.HousingChickenDataTable;
					if (!housingChickenDataTable.TryGetValue(mapID, out var value2) || value2.IsNullOrEmpty() || !value2.TryGetValue(areaIDOnHousingArea, out var value3) || value3.IsNullOrEmpty())
					{
						return;
					}
					int num = value3.Keys.Max();
					if (value == null)
					{
						value = (chickenTable[RegisterID] = new List<Environment.ChickenInfo>());
					}
					while (value.Count <= num)
					{
						value.Add(null);
					}
					for (int num2 = 0; num2 < value.Count; num2++)
					{
						value[num2] = null;
					}
					foreach (KeyValuePair<int, AIProject.SaveData.AnimalData> item in value3)
					{
						int key = item.Key;
						AIProject.SaveData.AnimalData value4 = item.Value;
						if (value4 == null)
						{
							value[key] = null;
							continue;
						}
						Environment.ChickenInfo chickenInfo = new Environment.ChickenInfo();
						chickenInfo.name = value4.Nickname;
						chickenInfo.AnimalData = value4;
						value[key] = chickenInfo;
					}
				}
				if (value.IsNullOrEmpty())
				{
					return;
				}
			}
			for (int num3 = 0; num3 < value.Count; num3++)
			{
				AIProject.SaveData.AnimalData animalData = value.GetElement(num3)?.AnimalData;
				if (animalData == null)
				{
					continue;
				}
				if (!animalData.InitAnimalTypeID || animalData.AnimalTypeID < 0)
				{
					animalData.AnimalTypeID = AIProject.Animal.AnimalData.GetAnimalTypeID(animalData.AnimalType);
					animalData.InitAnimalTypeID = true;
				}
				AnimalBase animalBase = Singleton<AnimalManager>.Instance.CreateBase(animalData.AnimalTypeID, (int)animalData.BreedingType);
				if (!(animalBase == null))
				{
					animalBase.transform.SetParent(AnimalRoot, worldPositionStays: true);
					animalData.AnimalID = animalBase.AnimalID;
					if (animalBase is IPetAnimal petAnimal)
					{
						petAnimal.AnimalData = animalData;
					}
					PetChicken petChicken = animalBase as PetChicken;
					AddChicken(num3, petChicken);
					if (petChicken != null)
					{
						petChicken.Initialize(this);
					}
				}
			}
		}
	}

	public void AddChicken(int index, PetChicken chicken)
	{
		if (chicken == null || ChickenList == null || ChickenList.Contains(chicken))
		{
			return;
		}
		ChickenList.Add(chicken);
		if (!Singleton<Map>.IsInstance() || !Singleton<Game>.IsInstance())
		{
			return;
		}
		Environment environment = Singleton<Game>.Instance.Environment;
		if (environment != null)
		{
			int mapID = Singleton<Map>.Instance.MapID;
			int areaIDOnHousingArea = AreaIDOnHousingArea;
			Dictionary<int, Dictionary<int, Dictionary<int, AIProject.SaveData.AnimalData>>> housingChickenDataTable = environment.HousingChickenDataTable;
			if (!housingChickenDataTable.TryGetValue(mapID, out var value) || value == null)
			{
				value = (housingChickenDataTable[mapID] = new Dictionary<int, Dictionary<int, AIProject.SaveData.AnimalData>>());
			}
			if (!value.TryGetValue(areaIDOnHousingArea, out var value2) || value2 == null)
			{
				value2 = (value[areaIDOnHousingArea] = new Dictionary<int, AIProject.SaveData.AnimalData>());
			}
			value[areaIDOnHousingArea][index] = chicken.AnimalData;
		}
	}

	public void RemoveChicken(int index, PetChicken chicken)
	{
		if (chicken == null || ChickenList.IsNullOrEmpty() || !ChickenList.Contains(chicken))
		{
			return;
		}
		ChickenList.Remove(chicken);
		if (!Singleton<Map>.IsInstance() || !Singleton<Game>.IsInstance())
		{
			return;
		}
		Environment environment = Singleton<Game>.Instance.Environment;
		if (environment != null)
		{
			int mapID = Singleton<Map>.Instance.MapID;
			int areaIDOnHousingArea = AreaIDOnHousingArea;
			Dictionary<int, Dictionary<int, Dictionary<int, AIProject.SaveData.AnimalData>>> housingChickenDataTable = environment.HousingChickenDataTable;
			if (housingChickenDataTable.TryGetValue(mapID, out var value) && !value.IsNullOrEmpty() && value.TryGetValue(areaIDOnHousingArea, out var value2) && !value2.IsNullOrEmpty())
			{
				value2.Remove(index);
			}
		}
	}

	public void DestroyChicken()
	{
		if (_farmKind != FarmKind.ChickenCoop)
		{
			return;
		}
		if (ChickenList != null)
		{
			ChickenList.RemoveAll((PetChicken x) => x == null);
			foreach (PetChicken chicken in ChickenList)
			{
				IPetAnimal petAnimal = chicken;
				if (petAnimal != null)
				{
					petAnimal.Release();
				}
				else
				{
					chicken.Release();
				}
			}
			ListPool<PetChicken>.Release(ChickenList);
			ChickenList = null;
		}
		Environment environment = ((!Singleton<Game>.IsInstance()) ? null : Singleton<Game>.Instance.Environment);
		if (environment != null)
		{
			Dictionary<int, List<Environment.ChickenInfo>> chickenTable = environment.ChickenTable;
			if (chickenTable.TryGetValue(RegisterID, out var _))
			{
				chickenTable.Remove(RegisterID);
			}
		}
	}
}
