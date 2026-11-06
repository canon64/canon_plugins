using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Illusion.Extensions;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Manager;

public sealed class Scene : SingletonInitializer<Scene>
{
	public interface IOverlap
	{
		Canvas canvas { get; }

		int order { get; set; }

		int priority { get; }

		void AddEvent();

		void RemoveEvent();
	}

	private class SceneStack<T> : Stack<T> where T : Data
	{
		public List<string> NowSceneNameList => _nowSceneNameList;

		private List<string> _nowSceneNameList { get; } = new List<string>();


		public SceneStack(T item)
		{
			base.Push(item);
			_nowSceneNameList.Push(item.levelName);
		}

		public new void Push(T item)
		{
			base.Push(item);
			if (!item.isAdd)
			{
				_nowSceneNameList.Clear();
			}
			_nowSceneNameList.Push(item.levelName);
		}

		public new T Pop()
		{
			T val = base.Pop();
			if (_nowSceneNameList.Any())
			{
				_nowSceneNameList.Pop();
			}
			if (!_nowSceneNameList.Any())
			{
				_nowSceneNameList.AddRange(GetNowSceneNameList(this));
			}
			AssetBundleManager.UnloadAssetBundle(val.bundleName, isUnloadForceRefCount: false, val.manifestName);
			return val;
		}

		public static IReadOnlyList<string> GetNowSceneNameList(IEnumerable<T> scenes)
		{
			List<string> list = new List<string>();
			foreach (T scene in scenes)
			{
				list.Add(scene.levelName);
				if (!scene.isAdd)
				{
					break;
				}
			}
			return list;
		}
	}

	[Serializable]
	public class FogData
	{
		[SerializeField]
		private FogMode _mode = FogMode.Exponential;

		[SerializeField]
		private bool _use;

		[SerializeField]
		private Color _color = Color.clear;

		[SerializeField]
		private float _density = 0.01f;

		[SerializeField]
		private float _start;

		[SerializeField]
		private float _end = 1000f;

		public FogMode mode
		{
			get
			{
				return _mode;
			}
			set
			{
				_mode = value;
			}
		}

		public bool use
		{
			get
			{
				return _use;
			}
			set
			{
				_use = value;
			}
		}

		public Color color
		{
			get
			{
				return _color;
			}
			set
			{
				_color = value;
			}
		}

		public float density
		{
			get
			{
				return _density;
			}
			set
			{
				_density = value;
			}
		}

		public float start
		{
			get
			{
				return _start;
			}
			set
			{
				_start = value;
			}
		}

		public float end
		{
			get
			{
				return _end;
			}
			set
			{
				_end = value;
			}
		}

		public void Change()
		{
			RenderSettings.fog = use;
			RenderSettings.fogMode = mode;
			RenderSettings.fogColor = color;
			RenderSettings.fogDensity = density;
			RenderSettings.fogStartDistance = start;
			RenderSettings.fogEndDistance = end;
		}
	}

	public class Data
	{
		public FadeCanvas.Fade fadeType { get; set; }

		public bool isFade
		{
			set
			{
				fadeType = (value ? FadeCanvas.Fade.InOut : FadeCanvas.Fade.None);
			}
		}

		public bool isFadeIn
		{
			get
			{
				if (!fadeType.HasFlag(FadeCanvas.Fade.In))
				{
					return fadeType.HasFlag(FadeCanvas.Fade.InOut);
				}
				return true;
			}
		}

		public bool isFadeOut
		{
			get
			{
				if (!fadeType.HasFlag(FadeCanvas.Fade.Out))
				{
					return fadeType.HasFlag(FadeCanvas.Fade.InOut);
				}
				return true;
			}
		}

		public string bundleName { get; set; } = string.Empty;


		public string levelName { get; set; } = string.Empty;


		public string manifestName { get; set; }

		public bool isAdd { get; set; }

		public bool isAsync { get; private set; }

		public Action onLoad { get; set; }

		public Func<UniTask> onFadeIn { get; set; }

		public Func<UniTask> onFadeOut { get; set; }

		public bool isLoading { get; private set; }

		public bool Async(bool isOn)
		{
			return isAsync = isOn;
		}

		public void Loading(bool isOn)
		{
			isLoading = isOn;
		}

		public AsyncOperation Unload()
		{
			if (isAdd)
			{
				return SceneManager.UnloadSceneAsync(levelName);
			}
			return null;
		}

		public void Load()
		{
			if (!bundleName.IsNullOrEmpty())
			{
				AssetBundleManager.LoadLevel(bundleName, levelName, isAdd, manifestName);
			}
			else
			{
				SceneManager.LoadScene(levelName, isAdd ? LoadSceneMode.Additive : LoadSceneMode.Single);
			}
			if (onLoad != null)
			{
				Observable.NextFrame().Subscribe(delegate
				{
					onLoad();
				});
			}
		}

		public async UniTask<AsyncOperation> LoadAsync(Action<float> progress)
		{
			AsyncOperation operation = (bundleName.IsNullOrEmpty() ? SceneManager.LoadSceneAsync(levelName, isAdd ? LoadSceneMode.Additive : LoadSceneMode.Single) : (await AssetBundleManager.LoadLevelAsync(bundleName, levelName, isAdd, manifestName)));
			if (operation != null)
			{
				operation.AsObservable().Subscribe(delegate
				{
					OnLoad();
				});
				if (progress != null)
				{
					await operation.ToUniTask(Progress.Create(delegate(float value)
					{
						progress(value);
					}));
				}
				else
				{
					await operation;
				}
			}
			else
			{
				Observable.NextFrame().Subscribe(delegate
				{
					OnLoad();
				});
			}
			return operation;
			void OnLoad()
			{
				onLoad?.Invoke();
			}
		}

		public void EventClear()
		{
			onLoad = null;
			onFadeIn = null;
			onFadeOut = null;
		}
	}

	[SerializeField]
	private Image _nowLoadingImage;

	[SerializeField]
	private SceneFadeCanvas _sceneFadeCanvas;

	[SerializeField]
	private LoadingIconJob _loadingIconJob;

	public static GameObject commonSpace { get; private set; }

	public static Data baseScene => _baseScene;

	private static Data _baseScene { get; set; } = null;


	private static SceneStack<Data> _sceneStack { get; set; }

	private static Stack<Data> _loadStack { get; } = new Stack<Data>();


	private static int loadingCount { get; set; }

	private static Image nowLoadingImage => SingletonInitializer<Scene>.instance._nowLoadingImage;

	public static SceneFadeCanvas sceneFadeCanvas => SingletonInitializer<Scene>.instance._sceneFadeCanvas;

	public static LoadingIconJob loadingIconJob => SingletonInitializer<Scene>.instance._loadingIconJob;

	public static IReactiveCollection<IOverlap> OverlapsEvent => _overlapList;

	public static IReadOnlyCollection<IOverlap> Overlaps => _overlapList;

	public static bool IsOverlap => _overlapList.Any();

	private static ReactiveCollection<IOverlap> _overlapList { get; } = new ReactiveCollection<IOverlap>();


	public static bool isReturnTitle { get; set; }

	public static UnityEngine.SceneManagement.Scene ActiveScene
	{
		get
		{
			return SceneManager.GetActiveScene();
		}
		set
		{
			SceneManager.SetActiveScene(value);
		}
	}

	public static bool IsNowLoading
	{
		get
		{
			if (!_loadStack.Any())
			{
				return _sceneStack.Any((Data item) => item.isLoading);
			}
			return true;
		}
	}

	public static bool IsNowLoadingFade
	{
		get
		{
			if (!IsNowLoading)
			{
				return IsFadeNow;
			}
			return true;
		}
	}

	public static bool IsFadeNow => sceneFadeCanvas.isFading;

	public static bool IsFadeEnd => sceneFadeCanvas.isEnd;

	public static string LoadSceneName => _sceneStack.NowSceneNameList.Last();

	public static string AddSceneName
	{
		get
		{
			if (_sceneStack.NowSceneNameList.Count <= 1)
			{
				return string.Empty;
			}
			return _sceneStack.NowSceneNameList[0];
		}
	}

	public static List<string> NowSceneNames => _sceneStack.NowSceneNameList;

	public static Data NowData => _sceneStack.Peek();

	protected override void Initialize()
	{
		_sceneStack = new SceneStack<Data>(new Data
		{
			levelName = ActiveScene.name,
			isAdd = false
		});
		DrawImageAndProgress(-1f, isLoadingImageDraw: false);
		CreateSpace();
		_sceneFadeCanvas.onStart += delegate(FadeCanvas.Fade type)
		{
			if (type == FadeCanvas.Fade.In)
			{
				DrawLoadIcon();
			}
		};
	}

	public static bool IsOverlapFirst(IOverlap overlap)
	{
		return _overlapList.FirstOrDefault() == overlap;
	}

	public static bool Add(IOverlap overlap)
	{
		if (_overlapList.Contains(overlap))
		{
			return false;
		}
		Canvas canvas = overlap.canvas;
		overlap.order = canvas.sortingOrder;
		if (_overlapList.Any())
		{
			if (overlap.priority > _overlapList.Min((IOverlap p) => p.priority))
			{
				return false;
			}
			int num = _overlapList.Max((IOverlap p) => p.canvas.sortingOrder) + 1;
			if (canvas.sortingOrder < num)
			{
				canvas.sortingOrder = num;
			}
		}
		overlap.AddEvent();
		_overlapList.Push(overlap);
		return true;
	}

	public static bool Pop()
	{
		if (!_overlapList.Any())
		{
			return false;
		}
		IOverlap overlap = _overlapList.Pop();
		overlap.RemoveEvent();
		overlap.canvas.sortingOrder = overlap.order;
		return true;
	}

	public static bool Remove(IOverlap overlap)
	{
		bool num = _overlapList.Remove(overlap);
		if (num)
		{
			overlap.RemoveEvent();
			overlap.canvas.sortingOrder = overlap.order;
		}
		return num;
	}

	public static UnityEngine.SceneManagement.Scene GetScene(string levelName)
	{
		return SceneManager.GetSceneByName(levelName);
	}

	public static GameObject[] GetRootGameObjects(string sceneName)
	{
		UnityEngine.SceneManagement.Scene scene = GetScene(sceneName);
		if (scene.isLoaded)
		{
			return scene.GetRootGameObjects();
		}
		return null;
	}

	public static T GetRootComponent<T>(string sceneName) where T : Component
	{
		GameObject[] rootGameObjects = GetRootGameObjects(sceneName);
		if (rootGameObjects == null)
		{
			return null;
		}
		GameObject[] array = rootGameObjects;
		for (int i = 0; i < array.Length; i++)
		{
			T component = array[i].GetComponent<T>();
			if (component != null)
			{
				return component;
			}
		}
		array = rootGameObjects;
		for (int i = 0; i < array.Length; i++)
		{
			T[] componentsInChildren = array[i].GetComponentsInChildren<T>(includeInactive: true);
			if (!((IReadOnlyCollection<T>)(object)componentsInChildren).IsNullOrEmpty())
			{
				return componentsInChildren[0];
			}
		}
		return null;
	}

	public static void LoadReserve(Data data, bool isLoadingImageDraw)
	{
		data.Async(isOn: false);
		LoadStart(data, isLoadingImageDraw).Forget();
	}

	public static void LoadReserveAsyncForget(Data data, bool isLoadingImageDraw)
	{
		LoadReserveAsync(data, isLoadingImageDraw).Forget();
	}

	public static async UniTask LoadReserveAsync(Data data, bool isLoadingImageDraw)
	{
		data.Async(isOn: true);
		await LoadStart(data, isLoadingImageDraw);
	}

	public static void Unload(bool useDataFade = true)
	{
		UnloadAsync(useDataFade).Forget();
	}

	public static async UniTask<bool> UnloadAsync(bool useDataFade = true)
	{
		if (_sceneStack.Count <= 1)
		{
			return false;
		}
		Data scene = _sceneStack.Peek();
		if (!scene.isAdd)
		{
			await ForLoadSceneAsync(isNowSceneRemove: true, isAddSceneLoad: false);
		}
		else
		{
			bool isFadeError = false;
			if (useDataFade && scene.isFadeIn)
			{
				try
				{
					await sceneFadeCanvas.StartFadeAysnc(FadeCanvas.Fade.In, throwOnError: true);
				}
				catch (OperationCanceledException)
				{
					isFadeError = true;
				}
			}
			await scene.Unload();
			if (!isFadeError && useDataFade && scene.isFadeOut)
			{
				try
				{
					await sceneFadeCanvas.StartFadeAysnc(FadeCanvas.Fade.Out, throwOnError: true);
				}
				catch (OperationCanceledException)
				{
				}
			}
			_sceneStack.Pop();
		}
		return true;
	}

	public static void UnloadAddScene()
	{
		UnloadAddSceneAsync(null).Forget();
	}

	public static async UniTask UnloadAddSceneAsync(Action<float> progress)
	{
		int addSceneSum = 0;
		if (progress != null)
		{
			addSceneSum = _sceneStack.TakeWhile((Data o) => !o.isAdd).Count();
		}
		while (_sceneStack.Peek().isAdd)
		{
			AsyncOperation asyncOperation = _sceneStack.Peek().Unload();
			if (asyncOperation != null)
			{
				if (progress == null)
				{
					await asyncOperation;
				}
				else
				{
					await asyncOperation.ToUniTask(Progress.Create(delegate(float value)
					{
						progress(value / (float)addSceneSum);
					}));
				}
			}
			int num = addSceneSum - 1;
			addSceneSum = num;
			_sceneStack.Pop();
		}
		await Resources.UnloadUnusedAssets();
	}

	public static async UniTask UnloadAddSceneAsyncFade(bool isLoadingImageDraw, Action onUnload)
	{
		await sceneFadeCanvas.StartFadeAysnc(FadeCanvas.Fade.In);
		DrawImageAndProgress(0f, isLoadingImageDraw);
		await UnloadAddSceneAsync(delegate(float value)
		{
			DrawImageAndProgress(value, isLoadingImageDraw);
		});
		onUnload?.Invoke();
		await sceneFadeCanvas.StartFadeAysnc(FadeCanvas.Fade.Out);
		DrawImageAndProgress(-1f, isLoadingImageDraw);
	}

	public static async UniTask UnloadToAddSceneAsync()
	{
		if (_sceneStack.Count > 1)
		{
			await ForLoadSceneAsync(isNowSceneRemove: true, isAddSceneLoad: true);
		}
	}

	public static async UniTask UnloadByOrderSceneAsync(string levelName, Func<bool, UniTask> action)
	{
		bool isFind = IsFind(levelName);
		if (action != null)
		{
			await action(isFind);
		}
		if (isFind)
		{
			while (_sceneStack.Peek().levelName != levelName)
			{
				_sceneStack.Pop();
			}
			await ForLoadSceneAsync(isNowSceneRemove: false, isAddSceneLoad: true);
		}
	}

	public static void ForLoadScene(bool isNowSceneRemove, bool isAddSceneLoad)
	{
		ForLoadSceneAsync(isNowSceneRemove, isAddSceneLoad).Forget();
	}

	public static async UniTask ForLoadSceneAsync(bool isNowSceneRemove, bool isAddSceneLoad)
	{
		if (isNowSceneRemove && _sceneStack.Count > 1)
		{
			_sceneStack.Pop();
		}
		do
		{
			_loadStack.Push(_sceneStack.Pop());
		}
		while (_loadStack.Peek().isAdd);
		loadingCount = _loadStack.Count;
		while (_loadStack.Any())
		{
			Data data = _loadStack.Pop();
			if (!isAddSceneLoad)
			{
				_loadStack.Clear();
			}
			await LoadStart(data);
		}
		loadingCount = 0;
	}

	public static bool IsFind(string levelName)
	{
		return _sceneStack.Any((Data scene) => scene.levelName == levelName);
	}

	public static void Reload()
	{
		ReloadAsync().Forget();
	}

	public static async UniTask ReloadAsync()
	{
		await ForLoadSceneAsync(isNowSceneRemove: false, isAddSceneLoad: true);
	}

	public static void CreateSpace()
	{
		UnityEngine.Object.Destroy(commonSpace);
		commonSpace = new GameObject("CommonSpace");
		UnityEngine.Object.DontDestroyOnLoad(commonSpace);
	}

	public static void SpaceRegister(Transform trans, bool worldPositionStays = false)
	{
		trans.SetParent(commonSpace.transform, worldPositionStays);
	}

	public static void DrawImageAndProgress(float value = -1f, bool isLoadingImageDraw = true)
	{
	}

	public static void DrawLoadIcon()
	{
		if (!(loadingIconJob == null) && !loadingIconJob.IsProc)
		{
			loadingIconJob.SetUseProc(val: true);
			Observable.EveryUpdate().TakeUntilDestroy(loadingIconJob).SkipWhile((long _) => sceneFadeCanvas.isFading)
				.Take(1)
				.Subscribe(delegate
				{
					loadingIconJob.SetUseProc(val: false);
				});
		}
	}

	public static void LoadBaseScene(Data data)
	{
		LoadBaseSceneAsync(data).Forget();
	}

	public static async UniTask LoadBaseSceneAsync(Data data)
	{
		data.isAdd = true;
		if (data.isFadeIn)
		{
			await sceneFadeCanvas.StartFadeAysnc(FadeCanvas.Fade.In);
			if (data.onFadeIn != null)
			{
				await data.onFadeIn();
			}
		}
		await UnloadBaseSceneAsync(LoadSceneName);
		data.Load();
		await UnloadBaseSceneAsync(data.levelName);
		_baseScene = data;
		if (data.isFadeOut)
		{
			if (data.onFadeOut != null)
			{
				await data.onFadeOut();
			}
			await sceneFadeCanvas.StartFadeAysnc(FadeCanvas.Fade.Out);
		}
	}

	public static void UnloadBaseScene()
	{
		UnloadBaseSceneAsync(LoadSceneName).Forget();
	}

	public static async UniTask UnloadBaseSceneAsync()
	{
		await UnloadBaseSceneAsync(LoadSceneName);
	}

	public static async UniTask UnloadBaseSceneAsync(string levelName)
	{
		if (_baseScene != null)
		{
			if (GetScene(_baseScene.levelName).IsValid())
			{
				await SceneManager.UnloadSceneAsync(_baseScene.levelName);
			}
			AssetBundleManager.UnloadAssetBundle(_baseScene.bundleName, isUnloadForceRefCount: true);
		}
		_baseScene = null;
		UnityEngine.SceneManagement.Scene scene = GetScene(levelName);
		if (!scene.isLoaded)
		{
			await UniTask.WaitUntil(delegate
			{
				UnityEngine.SceneManagement.Scene scene2 = (scene = GetScene(levelName));
				return scene2.isLoaded;
			});
		}
		ActiveScene = scene;
		await Resources.UnloadUnusedAssets();
	}

	public static void MapSettingChange(LightMapDataObject lightMap, FogData fog = null)
	{
		if (lightMap != null)
		{
			lightMap.Change();
		}
		fog?.Change();
	}

	private static void Load(Data data)
	{
		_sceneStack.Push(data);
		data.Load();
	}

	private static async UniTask LoadAsync(Data data)
	{
		_sceneStack.Push(data);
		Action<float> progress = null;
		await data.LoadAsync(progress);
	}

	private static async UniTask LoadStart(Data data, bool isLoadingImageDraw = true)
	{
		if (data.isFadeIn)
		{
			DrawImageAndProgress((!data.isAsync) ? (-1) : 0, isLoadingImageDraw);
			try
			{
				await sceneFadeCanvas.StartFadeAysnc(FadeCanvas.Fade.In, throwOnError: true);
			}
			catch (OperationCanceledException)
			{
			}
			await UniTask.WaitWhile(() => !sceneFadeCanvas.isEnd);
			if (data.onFadeIn != null)
			{
				await data.onFadeIn();
			}
		}
		data.Loading(isOn: true);
		if (data.isAsync)
		{
			await LoadAsync(data);
		}
		else
		{
			Load(data);
		}
		data.Loading(isOn: false);
		if (data.isFadeOut && data.onFadeOut != null)
		{
			await data.onFadeOut();
		}
		if (_loadStack.Any())
		{
			return;
		}
		await Resources.UnloadUnusedAssets();
		if (data.isFadeOut && IsFadeNow)
		{
			try
			{
				await sceneFadeCanvas.StartFadeAysnc(FadeCanvas.Fade.Out, throwOnError: true);
			}
			catch (OperationCanceledException)
			{
			}
		}
		DrawImageAndProgress(-1f, isLoadingImageDraw: false);
	}
}
