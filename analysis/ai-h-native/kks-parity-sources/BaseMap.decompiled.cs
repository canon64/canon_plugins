using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Manager;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BaseMap : MonoBehaviour
{
	public static string MAP_ROOT_NAME { get; } = "Map";

	public bool isLock
	{
		set
		{
			_isLock = value;
		}
	}

	protected bool _isLock { get; set; }

	public GameObject mapRoot { get; private set; }

	public bool isMapLoading { get; protected set; }

	public int no { get; protected set; }

	public int prevNo { get; protected set; }

	public IReadOnlyDictionary<int, MapInfo.Param> infoDic { get; private set; }

	public IReadOnlyDictionary<int, MapThumbnailInfo.Param> mapThumbnailInfoDic { get; private set; }

	public bool sceneFog { get; set; }

	public void Change(int no, FadeCanvas.Fade fadeType = FadeCanvas.Fade.InOut, bool isForce = false)
	{
		ChangeAsync(no, fadeType, isForce).Forget();
	}

	public virtual async UniTask ChangeAsync(int no, FadeCanvas.Fade fadeType = FadeCanvas.Fade.InOut, bool isForce = false)
	{
		if ((!(mapRoot != null) || this.no != no || isForce) && infoDic.TryGetValue(no, out var value))
		{
			isMapLoading = true;
			prevNo = this.no;
			this.no = no;
			await Manager.Scene.LoadBaseSceneAsync(new Manager.Scene.Data
			{
				bundleName = value.AssetBundleName,
				levelName = value.AssetName,
				fadeType = fadeType
			});
		}
	}

	public void Change(string mapName, FadeCanvas.Fade fadeType = FadeCanvas.Fade.InOut)
	{
		Change(ConvertMapNo(mapName), fadeType);
	}

	public async UniTask ChangeAsync(string mapName, FadeCanvas.Fade fadeType = FadeCanvas.Fade.InOut)
	{
		await ChangeAsync(ConvertMapNo(mapName), fadeType);
	}

	public MapInfo.Param GetParam(int no)
	{
		infoDic.TryGetValue(no, out var value);
		return value;
	}

	public MapInfo.Param GetParam(string mapName)
	{
		return infoDic.Values.FirstOrDefault((MapInfo.Param p) => p.MapName == mapName);
	}

	public int ConvertMapNo(string mapName)
	{
		return GetParam(mapName)?.No ?? (-1);
	}

	public string ConvertMapName(int no)
	{
		return GetParam(no)?.MapName ?? string.Empty;
	}

	public string ConvertDisplayName(int no)
	{
		return GetParam(no)?.DisplayName ?? string.Empty;
	}

	public static Dictionary<int, MapInfo.Param> LoadMapInfo()
	{
		Dictionary<int, MapInfo.Param> dictionary = new Dictionary<int, MapInfo.Param>();
		foreach (string item in from s in CommonLib.GetAssetBundleNameListFromPath("map/list/mapinfo/", subdirCheck: true)
			orderby s
			select s)
		{
			foreach (List<MapInfo.Param> item2 in from p in AssetBundleManager.LoadAllAsset(item, typeof(MapInfo)).GetAllAssets<MapInfo>()
				select p.param)
			{
				foreach (MapInfo.Param item3 in item2)
				{
					dictionary[item3.No] = item3;
				}
			}
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: false);
		}
		return dictionary;
	}

	public static Dictionary<int, MapThumbnailInfo.Param> LoadMapThumbnailInfo()
	{
		Dictionary<int, MapThumbnailInfo.Param> dictionary = new Dictionary<int, MapThumbnailInfo.Param>();
		foreach (string item in from s in CommonLib.GetAssetBundleNameListFromPath("map/list/mapthumbnailinfo/", subdirCheck: true)
			orderby s
			select s)
		{
			foreach (List<MapThumbnailInfo.Param> item2 in from p in AssetBundleManager.LoadAllAsset(item, typeof(MapThumbnailInfo)).GetAllAssets<MapThumbnailInfo>()
				select p.param)
			{
				foreach (MapThumbnailInfo.Param item3 in item2)
				{
					dictionary[item3.ID] = item3;
				}
			}
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: false);
		}
		return dictionary;
	}

	protected virtual void Awake()
	{
		isMapLoading = false;
		no = -1;
		SceneManager.activeSceneChanged += Reserve;
	}

	protected virtual void OnDestroy()
	{
		SceneManager.activeSceneChanged -= Reserve;
	}

	protected virtual void Start()
	{
		infoDic = LoadMapInfo();
		mapThumbnailInfoDic = LoadMapThumbnailInfo();
	}

	protected virtual void Update()
	{
		if (Manager.Config.initialized && !isMapLoading && sceneFog)
		{
			RenderSettings.fog = Manager.Config.GraphicData.Fog;
		}
	}

	protected virtual void Reserve(UnityEngine.SceneManagement.Scene oldScene, UnityEngine.SceneManagement.Scene newScene)
	{
		if (!_isLock)
		{
			sceneFog = false;
			mapRoot = newScene.GetRootGameObjects().FirstOrDefault((GameObject p) => p.name == MAP_ROOT_NAME);
			if (!(mapRoot == null))
			{
				isMapLoading = false;
				sceneFog = RenderSettings.fog;
			}
		}
	}
}
