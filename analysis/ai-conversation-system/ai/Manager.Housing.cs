using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AIProject;
using AIProject.Animal;
using AIProject.SaveData;
using Housing;
using UniRx;
using UnityEngine;

namespace Manager;

public class Housing : Singleton<Housing>
{
	public class PathInfo
	{
		public string bundle = string.Empty;

		public string file = string.Empty;

		public string manifest = string.Empty;

		public bool IsNullOrEmpty
		{
			[CompilerGenerated]
			get
			{
				return bundle.IsNullOrEmpty() || file.IsNullOrEmpty();
			}
		}
	}

	public class RequiredMaterial
	{
		public int category = -1;

		public int no = -1;

		public int num = -1;

		public RequiredMaterial()
		{
		}

		public RequiredMaterial(string[] _s)
		{
			_s.SafeProc(0, delegate(string s)
			{
				int.TryParse(s, out category);
			});
			_s.SafeProc(1, delegate(string s)
			{
				int.TryParse(s, out no);
			});
			_s.SafeProc(2, delegate(string s)
			{
				int.TryParse(s, out num);
			});
		}
	}

	public class LoadInfo
	{
		public int package;

		public int category;

		public string name = string.Empty;

		public string text = string.Empty;

		public PathInfo filePath = new PathInfo();

		public PathInfo thumbnailPath = new PathInfo();

		public Vector3 size = Vector3.one;

		public RequiredMaterial[] requiredMaterials;

		public bool useOption;

		public bool isAccess;

		public bool isAction;

		public bool isHPoint;

		public int limitNum = -1;

		public int Category
		{
			[CompilerGenerated]
			get
			{
				return 1 << category;
			}
		}

		public LoadInfo(int _package, List<string> _lst)
		{
			int num = 1;
			package = _package;
			category = int.Parse(_lst[num++]);
			name = _lst[num++];
			this.text = _lst[num++];
			filePath.bundle = _lst.SafeGet(num++);
			filePath.file = _lst.SafeGet(num++);
			filePath.manifest = _lst.SafeGet(num++);
			thumbnailPath.bundle = _lst.SafeGet(num++);
			thumbnailPath.file = _lst.SafeGet(num++);
			thumbnailPath.manifest = _lst.SafeGet(num++);
			size = new Vector3(TryParseF(_lst.SafeGet(num++)), TryParseF(_lst.SafeGet(num++)), TryParseF(_lst.SafeGet(num++)));
			List<RequiredMaterial> list = new List<RequiredMaterial>();
			for (int i = 0; i < 8; i++)
			{
				string text = _lst.SafeGet(num++);
				if (!text.IsNullOrEmpty())
				{
					list.Add(new RequiredMaterial(text.Split('/')));
				}
			}
			requiredMaterials = list.ToArray();
			bool.TryParse(_lst.SafeGet(num++), out useOption);
			isAccess = TryParseIntToBool(_lst.SafeGet(num++));
			isAction = TryParseIntToBool(_lst.SafeGet(num++));
			isHPoint = TryParseIntToBool(_lst.SafeGet(num++));
			if (!int.TryParse(_lst.SafeGet(num++), out limitNum))
			{
				limitNum = -1;
			}
		}

		private float TryParseF(string _str)
		{
			float result = 0f;
			float.TryParse(_str, out result);
			return result;
		}

		private bool TryParseIntToBool(string _str)
		{
			int result = 0;
			return int.TryParse(_str, out result) && result == 1;
		}
	}

	public class CategoryInfo
	{
		public string name = string.Empty;

		public PathInfo thumbnailPath = new PathInfo();

		public Texture2D Thumbnail
		{
			[CompilerGenerated]
			get
			{
				string bundle = thumbnailPath.bundle;
				string file = thumbnailPath.file;
				string manifest = thumbnailPath.manifest;
				return CommonLib.LoadAsset<Texture2D>(bundle, file, clone: false, manifest);
			}
		}

		public CategoryInfo(List<string> _lst)
		{
			int num = 1;
			name = _lst.SafeGet(num++);
			thumbnailPath.bundle = _lst.SafeGet(num++);
			thumbnailPath.file = _lst.SafeGet(num++);
			thumbnailPath.manifest = _lst.SafeGet(num++);
		}
	}

	public class AreaSizeInfo
	{
		public int no;

		public Vector3Int limitSize = new Vector3Int(100, 80, 100);

		public HashSet<int> compatibility = new HashSet<int>();

		public AreaSizeInfo(List<string> _lst)
		{
			int num = 0;
			no = int.Parse(_lst.SafeGet(num++));
			limitSize.Set(int.Parse(_lst.SafeGet(num++)), int.Parse(_lst.SafeGet(num++)), int.Parse(_lst.SafeGet(num++)));
			string[] array = _lst.SafeGet(num++).Split('/');
			if (!array.IsNullOrEmpty())
			{
				string[] array2 = array;
				foreach (string s in array2)
				{
					int result = 0;
					if (int.TryParse(s, out result))
					{
						compatibility.Add(result);
					}
				}
			}
			compatibility.Add(no);
		}
	}

	public class AreaInfo
	{
		public int no;

		public int size;

		public PathInfo presetPath = new PathInfo();

		public AreaInfo(List<string> _lst)
		{
			int num = 0;
			no = int.Parse(_lst.SafeGet(num++));
			size = int.Parse(_lst.SafeGet(num++));
			presetPath.bundle = _lst.SafeGet(num++);
			presetPath.file = _lst.SafeGet(num++);
			presetPath.manifest = _lst.SafeGet(num++);
		}
	}

	private class WaitTime
	{
		private const float intervalTime = 0.03f;

		private float nextFrameTime;

		public bool isOver => Time.realtimeSinceStartup >= nextFrameTime;

		public WaitTime()
		{
			Next();
		}

		public void Next()
		{
			nextFrameTime = Time.realtimeSinceStartup + 0.03f;
		}
	}

	private class FileListInfo
	{
		public Dictionary<string, string[]> dicFile;

		public FileListInfo(List<string> _list)
		{
			dicFile = _list.ToDictionary((string s) => s, (string s) => AssetBundleCheck.GetAllAssetName(s, _WithExtension: false, null, isAllCheck: true));
		}

		public bool Check(string _path, string _file)
		{
			string[] value = null;
			if (!AssetBundleCheck.IsSimulation)
			{
				_file = _file.ToLower();
			}
			return dicFile.TryGetValue(_path, out value) && value.Contains(_file);
		}

		public string[] FindRegex(string _path, string _regex)
		{
			string[] value = null;
			if (!AssetBundleCheck.IsSimulation)
			{
				_regex = _regex.ToLower();
			}
			return (!dicFile.TryGetValue(_path, out value)) ? null : value.Where((string s) => Regex.Match(s, _regex, RegexOptions.IgnoreCase).Success).ToArray();
		}
	}

	public const string SavePath = "housing/";

	private WaitTime waitTime;

	private GameObject objScene;

	private CraftScene craftScene;

	private ItemComponent.ColInfo[] colInfos;

	public Dictionary<int, LoadInfo> dicLoadInfo { get; private set; } = new Dictionary<int, LoadInfo>();


	public Dictionary<int, CategoryInfo> dicCategoryInfo { get; private set; } = new Dictionary<int, CategoryInfo>();


	public Dictionary<int, AreaSizeInfo> dicAreaSizeInfo { get; private set; } = new Dictionary<int, AreaSizeInfo>();


	public Dictionary<int, AreaInfo> dicAreaInfo { get; private set; } = new Dictionary<int, AreaInfo>();


	public bool IsLoadList { get; private set; }

	public CraftInfo CraftInfo { get; private set; }

	public List<ObjectCtrl> ObjectCtrls
	{
		get
		{
			if (CraftInfo == null)
			{
				return null;
			}
			return (from v in CraftInfo.ObjectCtrls
				orderby CraftInfo.ObjectInfos.FindIndex((IObjectInfo _i) => _i == v.Key)
				select v.Value).ToList();
		}
	}

	private Dictionary<int, GameObject> ObjRoots { get; set; } = new Dictionary<int, GameObject>();


	public bool IsCraft
	{
		[CompilerGenerated]
		get
		{
			return (bool)objScene && objScene.activeSelf;
		}
	}

	public ActionPoint[] ActionPoints
	{
		get
		{
			List<ActionPoint> _points = new List<ActionPoint>();
			HousingData housingData = Singleton<Game>.Instance.WorldData.HousingData;
			foreach (KeyValuePair<int, CraftInfo> craftInfo in housingData.CraftInfos)
			{
				craftInfo.Value?.GetActionPoint(ref _points);
			}
			return _points.ToArray();
		}
	}

	public FarmPoint[] FarmPoints
	{
		get
		{
			List<FarmPoint> _points = new List<FarmPoint>();
			HousingData housingData = Singleton<Game>.Instance.WorldData.HousingData;
			foreach (KeyValuePair<int, CraftInfo> craftInfo in housingData.CraftInfos)
			{
				craftInfo.Value?.GetFarmPoint(ref _points);
			}
			return _points.ToArray();
		}
	}

	public HPoint[] HPoints
	{
		get
		{
			List<HPoint> _points = new List<HPoint>();
			HousingData housingData = Singleton<Game>.Instance.WorldData.HousingData;
			foreach (KeyValuePair<int, CraftInfo> craftInfo in housingData.CraftInfos)
			{
				craftInfo.Value?.GetHPoint(ref _points);
			}
			return _points.ToArray();
		}
	}

	public PetHomePoint[] PetHomePoints
	{
		get
		{
			List<PetHomePoint> _points = new List<PetHomePoint>();
			HousingData housingData = Singleton<Game>.Instance.WorldData.HousingData;
			foreach (KeyValuePair<int, CraftInfo> craftInfo in housingData.CraftInfos)
			{
				craftInfo.Value?.GetPetHomePoint(ref _points);
			}
			return _points.ToArray();
		}
	}

	public JukePoint[] JukePoints
	{
		get
		{
			List<JukePoint> _points = new List<JukePoint>();
			HousingData housingData = Singleton<Game>.Instance.WorldData.HousingData;
			foreach (KeyValuePair<int, CraftInfo> craftInfo in housingData.CraftInfos)
			{
				craftInfo.Value?.GetJukePoint(ref _points);
			}
			return _points.ToArray();
		}
	}

	public CraftPoint[] CraftPoints
	{
		get
		{
			List<CraftPoint> _points = new List<CraftPoint>();
			HousingData housingData = Singleton<Game>.Instance.WorldData.HousingData;
			foreach (KeyValuePair<int, CraftInfo> craftInfo in housingData.CraftInfos)
			{
				craftInfo.Value?.GetCraftPoint(ref _points);
			}
			return _points.ToArray();
		}
	}

	public LightSwitchPoint[] LightSwitchPoints
	{
		get
		{
			List<LightSwitchPoint> _points = new List<LightSwitchPoint>();
			HousingData housingData = Singleton<Game>.Instance.WorldData.HousingData;
			foreach (KeyValuePair<int, CraftInfo> craftInfo in housingData.CraftInfos)
			{
				craftInfo.Value?.GetLightSwitchPoint(ref _points);
			}
			return _points.ToArray();
		}
	}

	private IEnumerator LoadExcelDataCoroutine()
	{
		if (IsLoadList)
		{
			yield break;
		}
		yield return null;
		waitTime = new WaitTime();
		dicCategoryInfo.Clear();
		dicLoadInfo.Clear();
		dicAreaSizeInfo.Clear();
		dicAreaInfo.Clear();
		List<string> pathList = CommonLib.GetAssetBundleNameListFromPath("housing/info/", subdirCheck: true);
		pathList.Sort();
		FileListInfo fli = new FileListInfo(pathList);
		if (waitTime.isOver)
		{
			yield return null;
			waitTime.Next();
		}
		string[] array2 = null;
		for (int i = 0; i < pathList.Count; i++)
		{
			string bundlePath = pathList[i];
			string fileName = Path.GetFileNameWithoutExtension(bundlePath);
			int pack = 0;
			int.TryParse(fileName, out pack);
			if (fli.Check(bundlePath, "Category_" + fileName))
			{
				LoadCategoryInfo(AssetUtility.LoadAsset<ExcelData>(bundlePath, "Category_" + fileName, string.Empty), dicCategoryInfo);
			}
			if (waitTime.isOver)
			{
				yield return null;
				waitTime.Next();
			}
			array2 = fli.FindRegex(bundlePath, $"ItemList_{fileName}_(\\d*)");
			if (!array2.IsNullOrEmpty())
			{
				string[] array3 = array2;
				foreach (string f in array3)
				{
					LoadLoadInfo(AssetUtility.LoadAsset<ExcelData>(bundlePath, f, string.Empty), dicLoadInfo, pack);
					if (waitTime.isOver)
					{
						yield return null;
						waitTime.Next();
					}
				}
			}
			if (fli.Check(bundlePath, "AreaSize_" + fileName))
			{
				LoadAreaSizeInfo(AssetUtility.LoadAsset<ExcelData>(bundlePath, "AreaSize_" + fileName, string.Empty));
			}
			if (waitTime.isOver)
			{
				yield return null;
				waitTime.Next();
			}
			if (fli.Check(bundlePath, "Area_" + fileName))
			{
				LoadAreaInfo(AssetUtility.LoadAsset<ExcelData>(bundlePath, "Area_" + fileName, string.Empty));
			}
			if (waitTime.isOver)
			{
				yield return null;
				waitTime.Next();
			}
		}
		waitTime = null;
		IsLoadList = true;
	}

	private void LoadCategoryInfo(ExcelData _ed, Dictionary<int, CategoryInfo> _dic)
	{
		if (_ed == null)
		{
			return;
		}
		foreach (List<string> item in from v in _ed.list.Skip(2)
			select v.list)
		{
			int result = -1;
			if (!int.TryParse(item.SafeGet(0), out result))
			{
				break;
			}
			_dic[result] = new CategoryInfo(item);
		}
	}

	private void LoadLoadInfo(ExcelData _ed, Dictionary<int, LoadInfo> _dic, int _pack)
	{
		if (_ed == null)
		{
			return;
		}
		foreach (List<string> item in from v in _ed.list.Skip(2)
			select v.list)
		{
			int result = -1;
			if (!int.TryParse(item.SafeGet(0), out result))
			{
				break;
			}
			_dic[result] = new LoadInfo(_pack, item);
		}
	}

	private void LoadAreaSizeInfo(ExcelData _ed)
	{
		if (_ed == null)
		{
			return;
		}
		foreach (List<string> item in from v in _ed.list.Skip(2)
			select v.list)
		{
			int result = -1;
			if (!int.TryParse(item.SafeGet(0), out result))
			{
				break;
			}
			dicAreaSizeInfo[result] = new AreaSizeInfo(item);
		}
	}

	private void LoadAreaInfo(ExcelData _ed)
	{
		if (_ed == null)
		{
			return;
		}
		foreach (List<string> item in from v in _ed.list.Skip(2)
			select v.list)
		{
			int result = -1;
			if (!int.TryParse(item.SafeGet(0), out result))
			{
				break;
			}
			dicAreaInfo[result] = new AreaInfo(item);
		}
	}

	public void SetCraftInfo(CraftInfo _craftInfo, bool _load = true)
	{
		CraftInfo = _craftInfo;
		if (_load)
		{
			LoadObject();
		}
	}

	public IEnumerator LoadHousing(int _type = 0)
	{
		waitTime = new WaitTime();
		ObjRoots.Clear();
		HousingData hd = Singleton<Game>.Instance.WorldData.HousingData;
		int[] keys = null;
		switch (_type)
		{
		case 0:
			keys = new int[3] { 0, 1, 2 };
			break;
		case 1:
			keys = new int[1] { 3 };
			break;
		}
		foreach (KeyValuePair<int, CraftInfo> v2 in hd.CraftInfos.Where((KeyValuePair<int, CraftInfo> v) => keys.Contains(v.Key)))
		{
			CraftInfo info = v2.Value;
			info.ObjRoot = GetRoot(v2.Key);
			if (waitTime.isOver)
			{
				yield return null;
				waitTime.Next();
			}
			yield return LoadObjectAsync(info);
		}
		waitTime = null;
	}

	public void Release()
	{
		if (Singleton<Game>.Instance.Data.AutoData != null)
		{
			HousingData housingData = Singleton<Game>.Instance.Data.AutoData.HousingData;
			if (housingData != null)
			{
				foreach (KeyValuePair<int, CraftInfo> craftInfo in housingData.CraftInfos)
				{
					craftInfo.Value.ObjectCtrls.Clear();
				}
			}
		}
		foreach (KeyValuePair<int, WorldData> world in Singleton<Game>.Instance.Data.WorldList)
		{
			if (world.Value == null)
			{
				continue;
			}
			HousingData housingData2 = world.Value.HousingData;
			if (housingData2 == null)
			{
				continue;
			}
			foreach (KeyValuePair<int, CraftInfo> craftInfo2 in housingData2.CraftInfos)
			{
				craftInfo2.Value.ObjectCtrls.Clear();
			}
		}
		HousingData housingData3 = Singleton<Game>.Instance.WorldData.HousingData;
		foreach (KeyValuePair<int, CraftInfo> craftInfo3 in housingData3.CraftInfos)
		{
			craftInfo3.Value.ObjectCtrls.Clear();
		}
		ObjRoots.Clear();
	}

	public bool StartHousing()
	{
		objScene = CommonLib.LoadAsset<GameObject>("housing/base/06.unity3d", "CraftScene", clone: true, string.Empty);
		if (objScene == null)
		{
			return false;
		}
		craftScene = objScene.GetComponent<CraftScene>();
		int tutorialID = 13;
		craftScene.DisplayTutorial = Singleton<MapUIContainer>.IsInstance() && !MapUIContainer.GetTutorialOpenState(tutorialID);
		if (craftScene.DisplayTutorial)
		{
			MapUIContainer.TutorialUI.BlockRaycastEnabled = true;
		}
		(from b in craftScene.ObserveEveryValueChanged((CraftScene cs) => cs.IsInit)
			where b
			select b).Take(1).Subscribe(delegate
		{
			MapUIContainer.StartFade(FadeCanvas.PanelType.Blackout, FadeType.Out, 1f, ignoreTimeScale: true).Subscribe((Action<Unit>)delegate
			{
			}, (Action)delegate
			{
				if (craftScene.DisplayTutorial)
				{
					MapUIContainer.TutorialUI.ClosedEvent = delegate
					{
						craftScene.DisplayTutorial = false;
					};
					MapUIContainer.OpenTutorialUI(tutorialID);
				}
			});
		});
		return true;
	}

	public void EndHousing()
	{
		CraftInfo?.SetOverlapColliders(_flag: false);
		if ((bool)objScene)
		{
			UnityEngine.Object.DestroyImmediate(objScene);
		}
		objScene = null;
		craftScene = null;
	}

	public HPoint[] GetHPoint(int _id)
	{
		List<HPoint> _points = new List<HPoint>();
		HousingData housingData = Singleton<Game>.Instance.WorldData.HousingData;
		CraftInfo value = null;
		if (housingData.CraftInfos.TryGetValue(_id, out value))
		{
			value?.GetHPoint(ref _points);
		}
		return _points.ToArray();
	}

	public ItemComponent.ColInfo[] GetColInfo(int _id)
	{
		List<ItemComponent.ColInfo> _infos = new List<ItemComponent.ColInfo>();
		HousingData housingData = Singleton<Game>.Instance.WorldData.HousingData;
		CraftInfo value = null;
		if (housingData.CraftInfos.TryGetValue(_id, out value))
		{
			value?.GetColInfo(ref _infos);
		}
		return _infos.ToArray();
	}

	public void StartShield(int _id)
	{
		List<ItemComponent.ColInfo> _infos = new List<ItemComponent.ColInfo>();
		HousingData housingData = Singleton<Game>.Instance.WorldData.HousingData;
		CraftInfo value = null;
		if (housingData.CraftInfos.TryGetValue(_id, out value) && value != null)
		{
			value.GetColInfo(ref _infos);
		}
		else
		{
			Debug.LogWarningFormat("指定されたID[{0}]は対象外なので遮蔽情報の取得出来ません。", _id);
		}
		colInfos = _infos.ToArray();
	}

	public void ShieldProc(Collider _collider)
	{
		if (!colInfos.IsNullOrEmpty())
		{
			ItemComponent.ColInfo[] array = colInfos;
			foreach (ItemComponent.ColInfo colInfo in array)
			{
				colInfo.CheckCollision(_collider);
			}
		}
	}

	public void EndShield()
	{
		VisibleShield();
		colInfos = null;
	}

	public void VisibleShield()
	{
		if (!colInfos.IsNullOrEmpty())
		{
			ItemComponent.ColInfo[] array = colInfos;
			foreach (ItemComponent.ColInfo colInfo in array)
			{
				colInfo.Visible = true;
			}
		}
	}

	public int GetSizeType(int _area)
	{
		AreaInfo value = null;
		return dicAreaInfo.TryGetValue(_area, out value) ? value.size : 0;
	}

	public GameObject GetRoot(int _idx)
	{
		GameObject value = null;
		if (!ObjRoots.TryGetValue(_idx, out value))
		{
			value = new GameObject($"housing {_idx}");
			if (Singleton<Map>.IsInstance() && (bool)Singleton<Map>.Instance.NavMeshSurface)
			{
				value.transform.SetParent(Singleton<Map>.Instance.NavMeshSurface.transform);
			}
			Transform value2 = null;
			if (Singleton<Map>.Instance.HousingPointTable.TryGetValue(_idx, out value2))
			{
				value.transform.SetPositionAndRotation(value2.position, value2.rotation);
			}
			else
			{
				value.transform.localPosition = Vector3.zero;
				value.transform.localRotation = Quaternion.identity;
			}
			ObjRoots.Add(_idx, value);
		}
		return value;
	}

	public void DeleteRoot()
	{
		foreach (KeyValuePair<int, GameObject> objRoot in ObjRoots)
		{
			if (!(objRoot.Value == null))
			{
				UnityEngine.Object.DestroyImmediate(objRoot.Value);
			}
		}
		ObjRoots.Clear();
	}

	public OCItem AddObject(int _id)
	{
		if (!dicLoadInfo.ContainsKey(_id))
		{
			return null;
		}
		return LoadObject(new OIItem
		{
			ID = _id
		}, null, _new: true, _create: true);
	}

	public ObjectCtrl AddFolder()
	{
		return LoadFolder(new OIFolder(), null, _check: false);
	}

	public ObjectCtrl DuplicateObject(ObjectCtrl _src)
	{
		if (_src == null)
		{
			return null;
		}
		ObjectCtrl result = null;
		switch (_src.ObjectInfo.Kind)
		{
		case 0:
		{
			OIItem oIItem = _src.ObjectInfo as OIItem;
			if (!CheckLimitNum(oIItem.ID))
			{
				return null;
			}
			result = LoadObject(new OIItem(_src.ObjectInfo as OIItem), _src.Parent, _new: false, _create: true);
			break;
		}
		case 1:
			result = LoadFolder(new OIFolder(_src.ObjectInfo as OIFolder), _src.Parent, _check: true);
			break;
		}
		return result;
	}

	public bool RestoreObject(ObjectCtrl _src, ObjectCtrl _parent, int _insert, bool _info = true)
	{
		switch (_src.ObjectInfo.Kind)
		{
		case 0:
			RestoreItem(_src as OCItem, _parent, _insert, _info);
			return CheckOverlap(_src as OCItem);
		case 1:
			return RestoreFolder(_src as OCFolder, _parent, _insert, _info);
		default:
			return false;
		}
	}

	public bool Load(string _path, bool _keep = true, bool _create = false)
	{
		DeleteObject();
		Vector3 limitSize = CraftInfo.LimitSize;
		int areaNo = CraftInfo.AreaNo;
		if (!CraftInfo.Load(_path))
		{
			return false;
		}
		if (_keep)
		{
			CraftInfo.LimitSize = limitSize;
			CraftInfo.AreaNo = areaNo;
		}
		LoadObject();
		if (_create)
		{
			CraftInfo.SetOverlapColliders(_flag: true);
		}
		return true;
	}

	public bool LoadObject()
	{
		if (CraftInfo == null)
		{
			return false;
		}
		DeleteObject();
		for (int i = 0; i < CraftInfo.ObjectInfos.Count; i++)
		{
			IObjectInfo objectInfo = CraftInfo.ObjectInfos[i];
			CreateObjectCtrl(objectInfo, null);
		}
		return true;
	}

	public void ResetObject()
	{
		DeleteObject();
		CraftInfo?.ObjectCtrls.Clear();
		CraftInfo?.ObjectInfos.Clear();
	}

	public bool CheckLimitNum(int _no)
	{
		LoadInfo value = null;
		if (!dicLoadInfo.TryGetValue(_no, out value))
		{
			return false;
		}
		if (value.limitNum < 0)
		{
			return true;
		}
		int usedNum = CraftInfo.GetUsedNum(_no);
		return usedNum < value.limitNum;
	}

	public bool CheckOverlap(ObjectCtrl _oc)
	{
		bool flag = false;
		foreach (KeyValuePair<IObjectInfo, ObjectCtrl> objectCtrl in CraftInfo.ObjectCtrls)
		{
			flag |= objectCtrl.Value.CheckOverlap(_oc);
			if (!(_oc is OCFolder oCFolder))
			{
				continue;
			}
			foreach (KeyValuePair<IObjectInfo, ObjectCtrl> item in oCFolder.Child)
			{
				flag |= CheckOverlap(item.Value);
			}
		}
		return flag;
	}

	public bool CheckOverlap()
	{
		bool flag = false;
		foreach (KeyValuePair<IObjectInfo, ObjectCtrl> objectCtrl in CraftInfo.ObjectCtrls)
		{
			flag |= CheckOverlap(objectCtrl.Value);
		}
		return flag;
	}

	public bool CheckSize(Vector3 _size)
	{
		Vector3 limitSize = CraftInfo.LimitSize;
		return _size.x <= limitSize.x && _size.y <= limitSize.y && _size.z <= limitSize.z;
	}

	private OCItem LoadObject(OIItem _oiItem, ObjectCtrl _parent, bool _new = false, bool _create = false)
	{
		OCItem oCItem = CreateOCItem(_oiItem);
		if (oCItem == null)
		{
			return null;
		}
		if (_parent == null)
		{
			AddInfoAndCtrl(CraftInfo.ObjectInfos, _oiItem, CraftInfo.ObjectCtrls, oCItem);
		}
		else
		{
			oCItem.OnAttach(_parent);
		}
		if (_new)
		{
			_oiItem.Pos = oCItem.ItemComponent.initPos;
			_oiItem.Color1 = oCItem.ItemComponent.defColor1;
			_oiItem.Color2 = oCItem.ItemComponent.defColor2;
			_oiItem.Color3 = oCItem.ItemComponent.defColor3;
			_oiItem.EmissionColor = oCItem.ItemComponent.defEmissionColor;
		}
		if (_create)
		{
			oCItem.ItemComponent.SetOverlapColliders(_flag: true);
		}
		oCItem.VisibleOption = oCItem.VisibleOption;
		oCItem.UpdateColor();
		oCItem.CalcTransform();
		ActionPoint[] actionPoints = oCItem.ActionPoints;
		if (!actionPoints.IsNullOrEmpty() && (_oiItem.ActionPoints.IsNullOrEmpty() || actionPoints.Length != _oiItem.ActionPoints.Length))
		{
			_oiItem.ActionPoints = Enumerable.Repeat(-1, actionPoints.Length).ToArray();
		}
		for (int i = 0; i < actionPoints.Length; i++)
		{
			if (!(actionPoints[i] == null))
			{
				actionPoints[i].RegisterID = _oiItem.ActionPoints[i];
				_oiItem.ActionPoints[i] = Singleton<Map>.Instance.RegisterRuntimePoint(actionPoints[i]);
			}
		}
		FarmPoint[] farmPoints = oCItem.FarmPoints;
		if (!farmPoints.IsNullOrEmpty() && (_oiItem.FarmPoints.IsNullOrEmpty() || farmPoints.Length != _oiItem.FarmPoints.Length))
		{
			_oiItem.FarmPoints = Enumerable.Repeat(-1, farmPoints.Length).ToArray();
		}
		for (int j = 0; j < farmPoints.Length; j++)
		{
			if (!(farmPoints[j] == null))
			{
				farmPoints[j].RegisterID = _oiItem.FarmPoints[j];
				_oiItem.FarmPoints[j] = Singleton<Map>.Instance.RegisterRuntimePoint(farmPoints[j]);
			}
		}
		PetHomePoint[] petHomePoints = oCItem.PetHomePoints;
		if (!petHomePoints.IsNullOrEmpty() && (_oiItem.PetHomePoints.IsNullOrEmpty() || petHomePoints.Length != _oiItem.PetHomePoints.Length))
		{
			_oiItem.PetHomePoints = Enumerable.Repeat(-1, petHomePoints.Length).ToArray();
		}
		for (int k = 0; k < petHomePoints.Length; k++)
		{
			if (!(petHomePoints[k] == null))
			{
				petHomePoints[k].RegisterID = _oiItem.PetHomePoints[k];
				_oiItem.PetHomePoints[k] = Singleton<Map>.Instance.RegisterRuntimePoint(petHomePoints[k]);
			}
		}
		JukePoint[] jukePoints = oCItem.JukePoints;
		if (!jukePoints.IsNullOrEmpty() && (_oiItem.JukePoints.IsNullOrEmpty() || jukePoints.Length != _oiItem.JukePoints.Length))
		{
			_oiItem.JukePoints = Enumerable.Repeat(-1, jukePoints.Length).ToArray();
		}
		for (int l = 0; l < jukePoints.Length; l++)
		{
			if (!(jukePoints[l] == null))
			{
				jukePoints[l].RegisterID = _oiItem.JukePoints[l];
				_oiItem.JukePoints[l] = Singleton<Map>.Instance.RegisterRuntimePoint(jukePoints[l]);
			}
		}
		CraftPoint[] craftPoints = oCItem.CraftPoints;
		if (!craftPoints.IsNullOrEmpty() && (_oiItem.CraftPoints.IsNullOrEmpty() || craftPoints.Length != _oiItem.CraftPoints.Length))
		{
			_oiItem.CraftPoints = Enumerable.Repeat(-1, craftPoints.Length).ToArray();
		}
		for (int m = 0; m < craftPoints.Length; m++)
		{
			if (!(craftPoints[m] == null))
			{
				craftPoints[m].RegisterID = _oiItem.CraftPoints[m];
				_oiItem.CraftPoints[m] = Singleton<Map>.Instance.RegisterRuntimePoint(craftPoints[m]);
			}
		}
		LightSwitchPoint[] lightSwitchPoints = oCItem.LightSwitchPoints;
		if (!lightSwitchPoints.IsNullOrEmpty() && (_oiItem.LightSwitchPoints.IsNullOrEmpty() || lightSwitchPoints.Length != _oiItem.LightSwitchPoints.Length))
		{
			_oiItem.LightSwitchPoints = Enumerable.Repeat(-1, lightSwitchPoints.Length).ToArray();
		}
		for (int n = 0; n < lightSwitchPoints.Length; n++)
		{
			if (!(lightSwitchPoints[n] == null))
			{
				lightSwitchPoints[n].RegisterID = _oiItem.LightSwitchPoints[n];
				_oiItem.LightSwitchPoints[n] = Singleton<Map>.Instance.RegisterRuntimePoint(lightSwitchPoints[n]);
			}
		}
		return oCItem;
	}

	private ObjectCtrl LoadFolder(OIFolder _oiFolder, ObjectCtrl _parent, bool _check)
	{
		ObjectCtrl objectCtrl = CreateOCFolder(_oiFolder);
		if (objectCtrl == null)
		{
			return null;
		}
		if (_parent == null)
		{
			AddInfoAndCtrl(CraftInfo.ObjectInfos, _oiFolder, CraftInfo.ObjectCtrls, objectCtrl);
		}
		else
		{
			objectCtrl.OnAttach(_parent);
		}
		objectCtrl.CalcTransform();
		List<IObjectInfo> list = new List<IObjectInfo>();
		foreach (IObjectInfo item in _oiFolder.Child)
		{
			ObjectCtrl objectCtrl2 = CreateObjectCtrl(item, objectCtrl, _check);
			if (objectCtrl2 == null)
			{
				list.Add(item);
			}
		}
		if (!list.IsNullOrEmpty())
		{
			foreach (IObjectInfo item2 in list)
			{
				_oiFolder.Child.Remove(item2);
			}
		}
		return objectCtrl;
	}

	private ObjectCtrl CreateObjectCtrl(IObjectInfo _objectInfo, ObjectCtrl _parent, bool _check = false)
	{
		ObjectCtrl result = null;
		switch (_objectInfo.Kind)
		{
		case 0:
		{
			OIItem oIItem = _objectInfo as OIItem;
			if (_check && !CheckLimitNum(oIItem.ID))
			{
				return null;
			}
			OIItem oiItem = _objectInfo as OIItem;
			bool create = _check;
			result = LoadObject(oiItem, _parent, _new: false, create);
			break;
		}
		case 1:
			result = LoadFolder(_objectInfo as OIFolder, _parent, _check);
			break;
		}
		return result;
	}

	private OCItem CreateOCItem(OIItem _objectInfo)
	{
		LoadInfo value = null;
		if (!dicLoadInfo.TryGetValue(_objectInfo.ID, out value))
		{
			return null;
		}
		GameObject gameObject = LoadObject(_objectInfo);
		return new OCItem(_objectInfo, gameObject, CraftInfo, value);
	}

	private GameObject LoadObject(OIItem _objectInfo)
	{
		LoadInfo value = null;
		if (!dicLoadInfo.TryGetValue(_objectInfo.ID, out value))
		{
			return null;
		}
		GameObject gameObject = CommonLib.LoadAsset<GameObject>(value.filePath.bundle, value.filePath.file, clone: true, value.filePath.manifest);
		if (gameObject == null)
		{
			return null;
		}
		gameObject.transform.SetParent(CraftInfo.ObjRoot.transform);
		return gameObject;
	}

	private ObjectCtrl CreateOCFolder(OIFolder _oiFolder)
	{
		GameObject gameObject = new GameObject(_oiFolder.Name);
		if (gameObject == null)
		{
			return null;
		}
		gameObject.transform.SetParent(CraftInfo.ObjRoot.transform);
		return new OCFolder(_oiFolder, gameObject, CraftInfo);
	}

	public void DeleteObject()
	{
		CraftInfo?.DeleteObject();
	}

	public void DeleteObject(ObjectCtrl _objectCtrl)
	{
		_objectCtrl?.OnDelete();
	}

	private void RestoreItem(OCItem _ocItem, ObjectCtrl _parent, int _insert, bool _info)
	{
		_ocItem.RestoreObject(LoadObject(_ocItem.OIItem));
		if (_info)
		{
			if (_parent == null)
			{
				AddInfoAndCtrl(CraftInfo.ObjectInfos, _ocItem.ObjectInfo, CraftInfo.ObjectCtrls, _ocItem, _insert);
			}
			else
			{
				_ocItem.OnAttach(_parent, _insert);
			}
		}
		_ocItem.ItemComponent.SetOverlapColliders(_flag: true);
		_ocItem.VisibleOption = _ocItem.VisibleOption;
		_ocItem.UpdateColor();
		_ocItem.CalcTransform();
		if (!Singleton<Map>.IsInstance())
		{
			return;
		}
		OIItem oIItem = _ocItem.OIItem;
		ActionPoint[] actionPoints = _ocItem.ActionPoints;
		for (int i = 0; i < actionPoints.Length; i++)
		{
			if (!(actionPoints[i] == null))
			{
				actionPoints[i].RegisterID = oIItem.ActionPoints[i];
				Singleton<Map>.Instance.RemoveRegIDCache(actionPoints[i].RegisterID);
			}
		}
		FarmPoint[] farmPoints = _ocItem.FarmPoints;
		for (int j = 0; j < farmPoints.Length; j++)
		{
			if (!(farmPoints[j] == null))
			{
				farmPoints[j].RegisterID = oIItem.FarmPoints[j];
				FarmSection[] harvestSections = farmPoints[j].HarvestSections;
				foreach (FarmSection farmSection in harvestSections)
				{
					farmSection.HarvestID = farmPoints[j].RegisterID;
				}
				Singleton<Map>.Instance.RemoveRegIDCache(farmPoints[j].RegisterID);
			}
		}
		PetHomePoint[] petHomePoints = _ocItem.PetHomePoints;
		for (int l = 0; l < petHomePoints.Length; l++)
		{
			if (!(petHomePoints[l] == null))
			{
				petHomePoints[l].RegisterID = oIItem.PetHomePoints[l];
				Singleton<Map>.Instance.RemoveRegIDCache(petHomePoints[l].RegisterID);
			}
		}
		JukePoint[] jukePoints = _ocItem.JukePoints;
		for (int m = 0; m < jukePoints.Length; m++)
		{
			if (!(jukePoints[m] == null))
			{
				jukePoints[m].RegisterID = oIItem.JukePoints[m];
				Singleton<Map>.Instance.RemoveRegIDCache(jukePoints[m].RegisterID);
			}
		}
		CraftPoint[] craftPoints = _ocItem.CraftPoints;
		for (int n = 0; n < craftPoints.Length; n++)
		{
			if (!(craftPoints[n] == null))
			{
				craftPoints[n].RegisterID = oIItem.CraftPoints[n];
				Singleton<Map>.Instance.RemoveRegIDCache(craftPoints[n].RegisterID);
			}
		}
		LightSwitchPoint[] lightSwitchPoints = _ocItem.LightSwitchPoints;
		for (int num = 0; num < lightSwitchPoints.Length; num++)
		{
			if (!(lightSwitchPoints[num] == null))
			{
				lightSwitchPoints[num].RegisterID = oIItem.LightSwitchPoints[num];
				Singleton<Map>.Instance.RemoveRegIDCache(lightSwitchPoints[num].RegisterID);
			}
		}
	}

	private bool RestoreFolder(OCFolder _ocFolder, ObjectCtrl _parent, int _insert, bool _info)
	{
		GameObject gameObject = new GameObject(_ocFolder.Name);
		if (gameObject == null)
		{
			return false;
		}
		gameObject.transform.SetParent(CraftInfo.ObjRoot.transform);
		_ocFolder.RestoreObject(gameObject);
		if (_info)
		{
			if (_parent == null)
			{
				AddInfoAndCtrl(CraftInfo.ObjectInfos, _ocFolder.ObjectInfo, CraftInfo.ObjectCtrls, _ocFolder, _insert);
			}
			else
			{
				_ocFolder.OnAttach(_parent, _insert);
			}
		}
		_ocFolder.CalcTransform();
		bool flag = false;
		Dictionary<IObjectInfo, ObjectCtrl> child = _ocFolder.Child;
		foreach (KeyValuePair<IObjectInfo, ObjectCtrl> item in child)
		{
			flag |= RestoreObject(item.Value, _ocFolder, -1, _info: false);
		}
		return flag;
	}

	private void AddInfoAndCtrl(List<IObjectInfo> _objectInfos, IObjectInfo _objectInfo, Dictionary<IObjectInfo, ObjectCtrl> _objectCtrls, ObjectCtrl _objectCtrl, int _insert = -1)
	{
		if (!_objectInfos.Contains(_objectInfo))
		{
			if (_insert != -1)
			{
				_objectInfos.Insert(_insert, _objectInfo);
			}
			else
			{
				_objectInfos.Add(_objectInfo);
			}
		}
		if (!_objectCtrls.ContainsKey(_objectInfo))
		{
			_objectCtrls.Add(_objectInfo, _objectCtrl);
		}
	}

	private void AddCtrl(Dictionary<IObjectInfo, ObjectCtrl> _objectCtrls, IObjectInfo _objectInfo, ObjectCtrl _objectCtrl)
	{
		if (!_objectCtrls.ContainsKey(_objectInfo))
		{
			_objectCtrls.Add(_objectInfo, _objectCtrl);
		}
	}

	public bool LoadAsync(string _path, Action<bool> _afterAction)
	{
		DeleteObject();
		Vector3 limitSize = CraftInfo.LimitSize;
		int areaNo = CraftInfo.AreaNo;
		if (!CraftInfo.Load(_path))
		{
			_afterAction?.Invoke(obj: false);
			return false;
		}
		CraftInfo.LimitSize = limitSize;
		CraftInfo.AreaNo = areaNo;
		Observable.FromCoroutine(LoadAsync).Subscribe((Action<Unit>)delegate
		{
		}, (Action)delegate
		{
			_afterAction?.Invoke(obj: true);
		});
		return true;
	}

	private IEnumerator LoadAsync()
	{
		if (CraftInfo == null || CraftInfo.ObjectInfos.IsNullOrEmpty())
		{
			yield break;
		}
		waitTime = new WaitTime();
		foreach (IObjectInfo info in CraftInfo.ObjectInfos)
		{
			yield return CreateObjectCtrlAsync(info, null);
		}
		if (waitTime.isOver)
		{
			yield return null;
			waitTime.Next();
		}
		HashSet<OCItem> checkItem = new HashSet<OCItem>();
		foreach (KeyValuePair<IObjectInfo, ObjectCtrl> objectCtrl in CraftInfo.ObjectCtrls)
		{
			GetOverlapTargets(objectCtrl.Value, checkItem);
		}
		foreach (OCItem v2 in checkItem)
		{
			v2.BeforeCheckOverlap();
			if (waitTime.isOver)
			{
				yield return null;
				waitTime.Next();
			}
		}
		yield return null;
		foreach (OCItem oci in checkItem)
		{
			foreach (OCItem v in checkItem)
			{
				oci.CheckOverlap(v, _load: true);
				if (waitTime.isOver)
				{
					yield return null;
					waitTime.Next();
				}
			}
		}
		foreach (OCItem item in checkItem)
		{
			item.AfterCheckOverlap();
		}
		if (waitTime.isOver)
		{
			yield return null;
			waitTime.Next();
		}
		waitTime = null;
	}

	private void GetOverlapTargets(ObjectCtrl _oc, HashSet<OCItem> _targets)
	{
		if (_oc is OCItem)
		{
			_targets.Add(_oc as OCItem);
		}
		else
		{
			if (!(_oc is OCFolder oCFolder))
			{
				return;
			}
			foreach (KeyValuePair<IObjectInfo, ObjectCtrl> item in oCFolder.Child)
			{
				GetOverlapTargets(item.Value, _targets);
			}
		}
	}

	private IEnumerator LoadObjectAsync(CraftInfo _craftInfo)
	{
		CraftInfo = _craftInfo;
		DeleteObject();
		if (waitTime.isOver)
		{
			yield return null;
			waitTime.Next();
		}
		for (int i = 0; i < CraftInfo.ObjectInfos.Count; i++)
		{
			yield return CreateObjectCtrlAsync(CraftInfo.ObjectInfos[i], null);
		}
	}

	private IEnumerator CreateObjectCtrlAsync(IObjectInfo _objectInfo, ObjectCtrl _parent)
	{
		switch (_objectInfo.Kind)
		{
		case 0:
			LoadObject(_objectInfo as OIItem, _parent);
			if (waitTime.isOver)
			{
				yield return null;
				waitTime.Next();
			}
			break;
		case 1:
			yield return LoadFolderAsync(_objectInfo as OIFolder, _parent);
			break;
		}
	}

	private IEnumerator LoadFolderAsync(OIFolder _oiFolder, ObjectCtrl _parent)
	{
		ObjectCtrl oc = CreateOCFolder(_oiFolder);
		if (oc == null)
		{
			yield break;
		}
		if (waitTime.isOver)
		{
			yield return null;
			waitTime.Next();
		}
		if (_parent == null)
		{
			AddInfoAndCtrl(CraftInfo.ObjectInfos, _oiFolder, CraftInfo.ObjectCtrls, oc);
		}
		else
		{
			oc.OnAttach(_parent);
		}
		oc.CalcTransform();
		Dictionary<IObjectInfo, ObjectCtrl> Child = (oc as OCFolder).Child;
		foreach (IObjectInfo c in _oiFolder.Child)
		{
			yield return CreateObjectCtrlAsync(c, oc);
		}
	}

	public bool[] CheckMOD(CraftInfo _craftInfo, Dictionary<IObjectInfo, int> _modObjects)
	{
		bool[] array = new bool[3];
		if (_craftInfo == null)
		{
			return array;
		}
		if (!_craftInfo.ObjectInfos.IsNullOrEmpty())
		{
			foreach (IObjectInfo objectInfo in _craftInfo.ObjectInfos)
			{
				array[0] |= CheckMOD(objectInfo, _modObjects);
			}
		}
		AreaInfo value = null;
		if (dicAreaInfo.TryGetValue(_craftInfo.AreaNo, out value))
		{
			AreaSizeInfo value2 = null;
			if (dicAreaSizeInfo.TryGetValue(value.size, out value2))
			{
				array[1] = _craftInfo.LimitSize != value2.limitSize;
			}
			else
			{
				array[1] = true;
			}
		}
		else
		{
			array[1] = true;
		}
		array[2] = !dicAreaInfo.ContainsKey(_craftInfo.AreaNo);
		return array;
	}

	private bool CheckMOD(IObjectInfo _objectInfo, Dictionary<IObjectInfo, int> _modObjects)
	{
		bool flag = false;
		switch (_objectInfo.Kind)
		{
		case 0:
		{
			OIItem oIItem = _objectInfo as OIItem;
			int num = 0;
			if (!dicCategoryInfo.ContainsKey(oIItem.Category))
			{
				num++;
			}
			if (!dicLoadInfo.ContainsKey(oIItem.ID))
			{
				num += 2;
			}
			flag = num != 0;
			if (flag)
			{
				_modObjects.Add(_objectInfo, num);
			}
			break;
		}
		case 1:
		{
			OIFolder oIFolder = _objectInfo as OIFolder;
			if (oIFolder.Child.IsNullOrEmpty())
			{
				break;
			}
			foreach (IObjectInfo item in oIFolder.Child)
			{
				flag |= CheckMOD(item, _modObjects);
			}
			break;
		}
		}
		return flag;
	}

	protected override void Awake()
	{
		if (CheckInstance())
		{
			UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
			StartCoroutine("LoadExcelDataCoroutine");
		}
	}
}
