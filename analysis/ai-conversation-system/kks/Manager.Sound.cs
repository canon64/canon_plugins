using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Illusion.Extensions;
using Illusion.Game;
using Sound;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.Audio;

namespace Manager;

public sealed class Sound : SingletonInitializer<Sound>
{
	public enum Type
	{
		BGM,
		ENV,
		SystemSE,
		GameSE2D,
		GameSE3D,
		EnvInfo
	}

	public class Loader
	{
		public Type type { get; set; }

		public string bundle { get; set; } = "";


		public string asset { get; set; } = "";


		public float fadeTime { get; set; }

		public bool isAssetEqualPlay { get; set; } = true;


		public int settingNo { get; set; } = -1;

	}

	[SerializeField]
	private AudioMixer _mixer;

	[SerializeField]
	private AudioListener _audioListener;

	[SerializeField]
	private Transform _rootSetting;

	[SerializeField]
	private Transform _rootPlay;

	[SerializeField]
	private Transform _rootASCache;

	[SerializeField]
	private GameObject[] _settingObjects;

	[SerializeField]
	private Transform[] _typeObjects;

	private Transform _listenerTransformCache;

	private Quaternion oldRot = Quaternion.identity;

	[SerializeField]
	private float ListenerLerpRotSpeed = 2.5f;

	public static GameObject currentBGM
	{
		get
		{
			return _currentBGM;
		}
		set
		{
			if (oldBGM != null)
			{
				UnityEngine.Object.Destroy(oldBGM);
			}
			oldBGM = _currentBGM;
			_currentBGM = value;
		}
	}

	private static GameObject _currentBGM { get; set; } = null;


	private static GameObject oldBGM { get; set; } = null;


	public static Transform Listener { get; set; }

	public static AudioMixer Mixer => SingletonInitializer<Sound>.instance._mixer;

	private static AudioListener audioListener => SingletonInitializer<Sound>.instance._audioListener;

	private static Transform rootSetting => SingletonInitializer<Sound>.instance._rootSetting;

	private static Transform rootPlay => SingletonInitializer<Sound>.instance._rootPlay;

	private static Transform rootASCache => SingletonInitializer<Sound>.instance._rootASCache;

	private static GameObject[] settingObjects => SingletonInitializer<Sound>.instance._settingObjects;

	private static Transform[] typeObjects => SingletonInitializer<Sound>.instance._typeObjects;

	private static Dictionary<int, Dictionary<string, AudioSource>> _audioCacheTable { get; } = new Dictionary<int, Dictionary<string, AudioSource>>();


	private static List<AudioClip> _useAudioClipList { get; } = new List<AudioClip>();


	private static IReadOnlyDictionary<int, SoundSettingData.Param> _settingDataTable { get; set; } = null;


	private static IReadOnlyDictionary<int, Sound3DSettingData.Param> _setting3DDataTable { get; set; } = null;


	private Transform listenerTransformCache
	{
		get
		{
			if (!(_listenerTransformCache != null))
			{
				return _listenerTransformCache = _audioListener.transform;
			}
			return _listenerTransformCache;
		}
	}

	public static GameObject PlayFade(GameObject fadeOut, AudioSource audioSource, float fadeTime = 0f)
	{
		if (fadeOut != null)
		{
			fadeOut.GetComponent<FadePlayer>().SafeProcObject(delegate(FadePlayer p)
			{
				p.Stop(fadeTime);
			});
		}
		GameObject obj = audioSource.gameObject;
		obj.AddComponent<FadePlayer>().Play(fadeTime);
		return obj;
	}

	public static void ClipAutoRelease(AudioSource audioSource)
	{
		AudioClip clip = audioSource.clip;
		Register(clip);
		audioSource.OnDestroyAsObservable().Subscribe(delegate
		{
			Remove(clip);
		});
	}

	public static void PlayEndDestroy(AudioSource audioSource, float fadeTime)
	{
		audioSource.UpdateAsObservable().TakeWhile((Unit __) => audioSource.isPlaying).Subscribe((Action<Unit>)delegate
		{
		}, (Action)delegate
		{
			if (!(audioSource == null))
			{
				FadePlayer component = audioSource.GetComponent<FadePlayer>();
				if (component != null)
				{
					component.Stop(fadeTime);
				}
				else
				{
					UnityEngine.Object.Destroy(audioSource.gameObject);
				}
			}
		});
	}

	public static void Register(AudioClip clip)
	{
		_useAudioClipList.Add(clip);
	}

	public static void Remove(AudioClip clip)
	{
		if (!_useAudioClipList.Remove(clip) || _useAudioClipList.Count((AudioClip p) => p == clip) == 0)
		{
			Resources.UnloadAsset(clip);
		}
	}

	public static void AudioSettingData(AudioSource audio, int settingNo)
	{
		SoundSettingData.Param audioSettingData = GetAudioSettingData(settingNo);
		if (audioSettingData != null)
		{
			audio.volume = audioSettingData.Volume;
			audio.pitch = audioSettingData.Pitch;
			audio.panStereo = audioSettingData.Pan;
			audio.spatialBlend = audioSettingData.Level3D;
			audio.priority = audioSettingData.Priority;
			audio.loop = audioSettingData.Loop;
			AudioSettingData3DOnly(audio, audioSettingData);
		}
	}

	public static void AudioSettingData3DOnly(AudioSource audio, int settingNo)
	{
		AudioSettingData3DOnly(audio, GetAudioSettingData(settingNo));
	}

	private static void AudioSettingData3DOnly(AudioSource audio, SoundSettingData.Param param)
	{
		if (_setting3DDataTable.TryGetValue(param?.Setting3DNo ?? (-1), out var value) && value != null)
		{
			audio.dopplerLevel = value.DopplerLevel;
			audio.spread = value.Spread;
			audio.minDistance = value.MinDistance;
			audio.maxDistance = value.MaxDistance;
			audio.rolloffMode = (AudioRolloffMode)value.AudioRolloffMode;
		}
	}

	private static SoundSettingData.Param GetAudioSettingData(int settingNo)
	{
		if (!_settingDataTable.TryGetValue(settingNo, out var value))
		{
			return null;
		}
		return value;
	}

	public static AudioSource Create(Type type, bool isCache = false)
	{
		GameObject obj = UnityEngine.Object.Instantiate(settingObjects[(int)type], isCache ? rootASCache : typeObjects[(int)type], worldPositionStays: false);
		obj.SetActive(value: true);
		return obj.GetComponent<AudioSource>();
	}

	public static void SetParent(Type type, Transform t)
	{
		t.SetParent(typeObjects[(int)type], worldPositionStays: false);
	}

	public static List<AudioSource> ToPlaying(Type type)
	{
		List<AudioSource> list = new List<AudioSource>();
		Transform transform = typeObjects[(int)type];
		for (int i = 0; i < transform.childCount; i++)
		{
			list.Add(transform.GetChild(i).GetComponent<AudioSource>());
		}
		return list;
	}

	public static bool IsPlay(Type type, string playName = null)
	{
		Transform transform = typeObjects[(int)type];
		for (int i = 0; i < transform.childCount; i++)
		{
			if (playName.IsNullOrEmpty() || !(playName != transform.GetChild(i).name))
			{
				return true;
			}
		}
		return false;
	}

	public static bool IsPlay(Transform trans)
	{
		for (int i = 0; i < typeObjects.Length; i++)
		{
			if (IsPlay((Type)i, trans))
			{
				return true;
			}
		}
		return false;
	}

	public static bool IsPlay(Type type, Transform trans)
	{
		Transform transform = typeObjects[(int)type];
		for (int i = 0; i < transform.childCount; i++)
		{
			if (transform.GetChild(i) == trans)
			{
				return true;
			}
		}
		return false;
	}

	public static Transform FindAsset(Type type, string asset, string bundle = null)
	{
		Transform transform = typeObjects[(int)type];
		for (int i = 0; i < transform.childCount; i++)
		{
			Transform child = transform.GetChild(i);
			Sound_Component component = child.GetComponent<Sound_Component>();
			if (!(component.asset != asset) && (bundle == null || component.bundle == bundle))
			{
				if (type != 0)
				{
					return child;
				}
				if (child.gameObject != oldBGM)
				{
					return child;
				}
			}
		}
		return null;
	}

	public static AudioSource Play(Loader loader)
	{
		Type type = loader.type;
		string bundle = loader.bundle;
		string asset = loader.asset;
		if (!loader.isAssetEqualPlay)
		{
			Transform transform = FindAsset(type, asset, bundle);
			if (transform != null)
			{
				return transform.GetComponent<AudioSource>();
			}
		}
		AudioClip asset2 = new AssetBundleData(bundle, asset).GetAsset<AudioClip>();
		if (asset2 == null)
		{
			return null;
		}
		AudioSource audioSource = Create(type);
		audioSource.clip = asset2;
		Play_Standby(audioSource, loader);
		return audioSource;
	}

	public static void Play(Loader loader, Action<AudioSource> action)
	{
		PlayAsync(loader, action).Forget();
	}

	public static async UniTask PlayAsync(Loader loader, Action<AudioSource> action)
	{
		action?.Invoke(await PlayAsync(loader));
	}

	public static async UniTask<AudioSource> PlayAsync(Loader loader)
	{
		Type type = loader.type;
		string bundle = loader.bundle;
		string asset = loader.asset;
		if (!loader.isAssetEqualPlay)
		{
			Transform transform = FindAsset(type, asset, bundle);
			if (transform != null)
			{
				return transform.GetComponent<AudioSource>();
			}
		}
		AudioClip audioClip = await new AssetBundleData(bundle, asset).GetAssetAsync<AudioClip>();
		if (audioClip == null)
		{
			return null;
		}
		AudioSource audioSource = Create(type);
		audioSource.clip = audioClip;
		Play_Standby(audioSource, loader);
		return audioSource;
	}

	private static void Play_Standby(AudioSource audioSource, Loader loader)
	{
		AudioClip clip = audioSource.clip;
		audioSource.name = clip.name;
		ClipAutoRelease(audioSource);
		AudioSettingData(audioSource, loader.settingNo);
		float fadeTime = loader.fadeTime;
		GameObject fadeOut = null;
		if (loader.type == Type.BGM)
		{
			fadeTime = Mathf.Max(fadeTime, 0.01f);
			fadeOut = currentBGM;
			currentBGM = audioSource.gameObject;
		}
		audioSource.GetOrAddComponent<Sound_Component>().Bind(loader);
		(from _ in audioSource.UpdateAsObservable()
			where clip.loadState == AudioDataLoadState.Loaded
			select _).Take(1).Subscribe(delegate
		{
			if (fadeTime > 0f)
			{
				PlayFade(fadeOut, audioSource, fadeTime);
			}
			else
			{
				audioSource.Play();
			}
			if (!audioSource.loop)
			{
				PlayEndDestroy(audioSource, fadeTime);
			}
		});
	}

	public static void Stop(Type type)
	{
		List<GameObject> list = new List<GameObject>();
		Transform transform = typeObjects[(int)type];
		for (int i = 0; i < transform.childCount; i++)
		{
			list.Add(transform.GetChild(i).gameObject);
		}
		list.ForEach(delegate(GameObject p)
		{
			UnityEngine.Object.Destroy(p);
		});
	}

	public static void Stop(Type type, Transform trans)
	{
		Transform transform = typeObjects[(int)type];
		for (int i = 0; i < transform.childCount; i++)
		{
			Transform child = transform.GetChild(i);
			if (child == trans)
			{
				UnityEngine.Object.Destroy(child.gameObject);
				break;
			}
		}
	}

	public static void Stop(Transform trans)
	{
		for (int i = 0; i < typeObjects.Length; i++)
		{
			Transform transform = typeObjects[i];
			for (int j = 0; j < transform.childCount; j++)
			{
				Transform child = transform.GetChild(j);
				if (child == trans)
				{
					if (i == 0)
					{
						Stop(Type.BGM);
					}
					else
					{
						UnityEngine.Object.Destroy(child.gameObject);
					}
					return;
				}
			}
		}
	}

	public static void PlayBGM(float fadeTime = 0f)
	{
		if (currentBGM != null)
		{
			currentBGM.GetComponent<FadePlayer>().SafeProcObject(delegate(FadePlayer p)
			{
				p.Play(fadeTime);
			});
		}
	}

	public static void PauseBGM()
	{
		if (currentBGM != null)
		{
			currentBGM.GetComponent<FadePlayer>().SafeProcObject(delegate(FadePlayer p)
			{
				p.Pause();
			});
		}
	}

	public static void StopBGM(float fadeTime = 0f)
	{
		if (currentBGM != null)
		{
			currentBGM.GetComponent<FadePlayer>().SafeProcObject(delegate(FadePlayer p)
			{
				p.Stop(fadeTime);
			});
		}
		(from item in typeObjects[0].gameObject.Children()
			where item != currentBGM
			select item).ToList().ForEach(UnityEngine.Object.Destroy);
	}

	public static AudioSource Play(Type type, AudioClip clip, float fadeTime = 0f)
	{
		AudioSource audioSource = Create(type);
		audioSource.clip = clip;
		audioSource.GetOrAddComponent<FadePlayer>().SafeProcObject(delegate(FadePlayer p)
		{
			p.Play(fadeTime);
		});
		Dictionary<string, float> sEClipVolum = Utils.Sound.SEClipVolum;
		if (sEClipVolum != null && clip != null)
		{
			string key = clip.name;
			if (sEClipVolum.ContainsKey(key))
			{
				audioSource.volume = sEClipVolum[key];
			}
		}
		return audioSource;
	}

	public static AudioSource CreateCache(Type type, AssetBundleData data)
	{
		return CreateCache(type, data.bundle, data.asset);
	}

	public static AudioSource CreateCache(Type type, AssetBundleManifestData data)
	{
		return CreateCache(type, data.bundle, data.asset, data.manifest);
	}

	public static AudioSource CreateCache(Type type, string bundle, string asset, string manifest = null)
	{
		if (!_audioCacheTable.TryGetValue((int)type, out var value))
		{
			value = (_audioCacheTable[(int)type] = new Dictionary<string, AudioSource>());
		}
		if (!value.TryGetValue(asset, out var value2))
		{
			value2 = Create(type, isCache: true);
			value2.name = asset;
			value2.clip = new AssetBundleManifestData(bundle, asset, manifest).GetAsset<AudioClip>();
			Register(value2.clip);
			value.Add(asset, value2);
		}
		return value2;
	}

	public static void ReleaseCache(Type type, string bundle, string asset, string manifest = null)
	{
		if (_audioCacheTable.TryGetValue((int)type, out var value))
		{
			if (value.TryGetValue(asset, out var value2))
			{
				Remove(value2.clip);
				UnityEngine.Object.Destroy(value2.gameObject);
				value.Remove(asset);
				AssetBundleManager.UnloadAssetBundle(bundle, isUnloadForceRefCount: false, manifest);
			}
			if (!value.Any())
			{
				_audioCacheTable.Remove((int)type);
			}
		}
	}

	protected override void Initialize()
	{
		Dictionary<int, SoundSettingData.Param> dictionary = new Dictionary<int, SoundSettingData.Param>();
		List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath("sound/setting/soundsettingdata/", subdirCheck: true);
		assetBundleNameListFromPath.Sort();
		foreach (string item in assetBundleNameListFromPath)
		{
			foreach (List<SoundSettingData.Param> item2 in from p in AssetBundleManager.LoadAllAsset(item, typeof(SoundSettingData)).GetAllAssets<SoundSettingData>()
				select p.param)
			{
				foreach (SoundSettingData.Param item3 in item2)
				{
					dictionary[item3.No] = item3;
				}
			}
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: false);
		}
		_settingDataTable = dictionary;
		Dictionary<int, Sound3DSettingData.Param> dictionary2 = new Dictionary<int, Sound3DSettingData.Param>();
		List<string> assetBundleNameListFromPath2 = CommonLib.GetAssetBundleNameListFromPath("sound/setting/sound3dsettingdata/", subdirCheck: true);
		assetBundleNameListFromPath2.Sort();
		foreach (string item4 in assetBundleNameListFromPath2)
		{
			foreach (List<Sound3DSettingData.Param> item5 in from p in AssetBundleManager.LoadAllAsset(item4, typeof(Sound3DSettingData)).GetAllAssets<Sound3DSettingData>()
				select p.param)
			{
				foreach (Sound3DSettingData.Param item6 in item5)
				{
					dictionary2[item6.No] = item6;
				}
			}
			AssetBundleManager.UnloadAssetBundle(item4, isUnloadForceRefCount: false);
		}
		_setting3DDataTable = dictionary2;
		if (Camera.main != null)
		{
			Listener = Camera.main.transform;
		}
		Config.SoundData.AddEvent(_mixer);
	}

	private void Update()
	{
		if (Listener != null)
		{
			listenerTransformCache.SetPositionAndRotation(Listener.position, ListenerRotCarc(Listener.rotation));
		}
		else
		{
			listenerTransformCache.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
		}
	}

	private Quaternion ListenerRotCarc(Quaternion target)
	{
		float deltaTime = Time.deltaTime;
		float t = Mathf.Clamp01(ListenerLerpRotSpeed * deltaTime);
		return oldRot = Quaternion.Lerp(oldRot, target, t);
	}
}
