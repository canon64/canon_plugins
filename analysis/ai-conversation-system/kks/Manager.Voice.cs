using System;
using System.Collections.Generic;
using System.Linq;
using Config;
using Cysharp.Threading.Tasks;
using Illusion.Elements.Xml;
using Illusion.Extensions;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.Audio;

namespace Manager;

public sealed class Voice : SingletonInitializer<Voice>
{
	public enum Type
	{
		PCM
	}

	public class Loader
	{
		public int no { get; set; }

		public float pitch { get; set; } = 1f;


		public string bundle { get; set; } = "";


		public string asset { get; set; } = "";


		public float fadeTime { get; set; }

		public int settingNo { get; set; } = -1;


		public Transform voiceTrans { get; set; }
	}

	[SerializeField]
	private Transform _rootSetting;

	[SerializeField]
	private Transform _rootPlay;

	[SerializeField]
	private Transform _rootASCache;

	[SerializeField]
	private GameObject[] _settingObjects;

	public static IReadOnlyDictionary<int, VoiceInfo.Param> infoTable { get; private set; }

	private static Dictionary<int, Transform> _transTable { get; } = new Dictionary<int, Transform>();


	public static AudioMixer Mixer => Sound.Mixer;

	private static Transform rootSetting => SingletonInitializer<Voice>.instance._rootSetting;

	private static Transform rootPlay => SingletonInitializer<Voice>.instance._rootPlay;

	private static Transform rootASCache => SingletonInitializer<Voice>.instance._rootASCache;

	private static GameObject[] settingObjects => SingletonInitializer<Voice>.instance._settingObjects;

	private static Dictionary<int, Dictionary<string, AudioSource>> _audioCacheTable { get; } = new Dictionary<int, Dictionary<string, AudioSource>>();


	public static VoiceSystem _Config { get; private set; }

	private static Control xmlCtrl { get; set; } = null;


	public static void ResetConfig()
	{
		xmlCtrl?.Init();
	}

	public static AudioSource Create(Transform vt)
	{
		GameObject obj = UnityEngine.Object.Instantiate(settingObjects[0], vt, worldPositionStays: false);
		obj.SetActive(value: true);
		return obj.GetComponent<AudioSource>();
	}

	public static void SetParent(int no, Transform t)
	{
		if (_transTable.TryGetValue(no, out var value))
		{
			t.SetParent(value, worldPositionStays: false);
		}
	}

	public static List<AudioSource> ToPlaying(int no)
	{
		List<AudioSource> list = new List<AudioSource>();
		if (!_transTable.TryGetValue(no, out var value))
		{
			return list;
		}
		for (int i = 0; i < value.childCount; i++)
		{
			list.Add(value.GetChild(i).GetComponent<AudioSource>());
		}
		return list;
	}

	public static bool IsPlay(int no)
	{
		if (_transTable.TryGetValue(no, out var value))
		{
			return value.childCount > 0;
		}
		return false;
	}

	public static bool IsPlay(Transform voiceTrans, bool isLoopCheck = true)
	{
		foreach (Transform value in _transTable.Values)
		{
			for (int i = 0; i < value.childCount; i++)
			{
				Transform child = value.GetChild(i);
				if (!(child.GetComponent<Voice_Component>().voiceTrans != voiceTrans) && (isLoopCheck || !child.GetComponent<AudioSource>().loop))
				{
					return true;
				}
			}
		}
		return false;
	}

	public static bool IsPlay(int no, Transform voiceTrans, bool isLoopCheck = true)
	{
		if (!_transTable.TryGetValue(no, out var value))
		{
			return false;
		}
		for (int i = 0; i < value.childCount; i++)
		{
			Transform child = value.GetChild(i);
			if (!(child.GetComponent<Voice_Component>().voiceTrans != voiceTrans) && (isLoopCheck || !child.GetComponent<AudioSource>().loop))
			{
				return true;
			}
		}
		return false;
	}

	public static bool IsPlay()
	{
		return _transTable.Values.Any((Transform v) => v.childCount != 0);
	}

	public static AudioSource Play(Loader loader)
	{
		if (!_transTable.TryGetValue(loader.no, out var value))
		{
			return null;
		}
		AudioClip asset = new AssetBundleData(loader.bundle, loader.asset).GetAsset<AudioClip>();
		if (asset == null)
		{
			return null;
		}
		AudioSource audioSource = Create(value);
		audioSource.clip = asset;
		Play_Standby(audioSource, loader);
		return audioSource;
	}

	public static AudioSource OncePlay(Loader loader)
	{
		StopAll();
		return Play(loader);
	}

	public static AudioSource OncePlayChara(Loader loader)
	{
		if (loader.voiceTrans != null)
		{
			Stop(loader.no, loader.voiceTrans);
		}
		else
		{
			Stop(loader.no);
		}
		return Play(loader);
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
		if (!_transTable.TryGetValue(loader.no, out var vt))
		{
			return null;
		}
		AudioClip audioClip = await new AssetBundleData(loader.bundle, loader.asset).GetAssetAsync<AudioClip>();
		if (audioClip == null)
		{
			return null;
		}
		AudioSource audioSource = Create(vt);
		audioSource.clip = audioClip;
		Play_Standby(audioSource, loader);
		return audioSource;
	}

	public static void OncePlay(Loader loader, Action<AudioSource> action)
	{
		OncePlayAsync(loader, action).Forget();
	}

	public static async UniTask OncePlayAsync(Loader loader, Action<AudioSource> action)
	{
		action?.Invoke(await OncePlayAsync(loader));
	}

	public static async UniTask<AudioSource> OncePlayAsync(Loader loader)
	{
		StopAll();
		return await PlayAsync(loader);
	}

	public static void OncePlayChara(Loader loader, Action<AudioSource> action)
	{
		OncePlayCharaAsync(loader, action).Forget();
	}

	public static async UniTask OncePlayCharaAsync(Loader loader, Action<AudioSource> action)
	{
		action?.Invoke(await OncePlayCharaAsync(loader));
	}

	public static async UniTask<AudioSource> OncePlayCharaAsync(Loader loader)
	{
		if (loader.voiceTrans != null)
		{
			Stop(loader.no, loader.voiceTrans);
		}
		else
		{
			Stop(loader.no);
		}
		return await PlayAsync(loader);
	}

	private static void Play_Standby(AudioSource audioSource, Loader loader)
	{
		AudioClip clip = audioSource.clip;
		audioSource.name = clip.name;
		Sound.ClipAutoRelease(audioSource);
		int settingNo = loader.settingNo;
		Sound.AudioSettingData(audioSource, settingNo);
		if (settingNo < 0)
		{
			audioSource.spatialBlend = ((loader.voiceTrans != null) ? 1 : 0);
		}
		audioSource.pitch = loader.pitch;
		int no = loader.no;
		audioSource.UpdateAsObservable().Subscribe(delegate
		{
			audioSource.volume = GetVolume(no);
		});
		Transform voiceTrans = loader.voiceTrans;
		if (voiceTrans != null)
		{
			Transform audioTrans = audioSource.transform;
			audioSource.UpdateAsObservable().TakeUntilDestroy(voiceTrans).Subscribe(delegate
			{
				audioTrans.SetPositionAndRotation(voiceTrans.position, voiceTrans.rotation);
			});
		}
		audioSource.GetOrAddComponent<Voice_Component>().Bind(loader);
		float fadeTime = loader.fadeTime;
		(from _ in audioSource.UpdateAsObservable()
			where clip.loadState == AudioDataLoadState.Loaded
			select _).Take(1).Subscribe(delegate
		{
			if (fadeTime > 0f)
			{
				Sound.PlayFade(null, audioSource, fadeTime);
			}
			else
			{
				audioSource.Play();
			}
			if (!audioSource.loop)
			{
				Sound.PlayEndDestroy(audioSource, fadeTime);
			}
		});
	}

	public static void StopAll(bool isLoopStop = true)
	{
		List<GameObject> list = new List<GameObject>();
		foreach (Transform value in _transTable.Values)
		{
			for (int i = 0; i < value.childCount; i++)
			{
				Transform child = value.GetChild(i);
				if (!isLoopStop)
				{
					AudioSource componentInChildren = child.GetComponentInChildren<AudioSource>();
					if (componentInChildren != null && componentInChildren.loop)
					{
						continue;
					}
				}
				list.Add(child.gameObject);
			}
		}
		list.ForEach(delegate(GameObject p)
		{
			UnityEngine.Object.Destroy(p);
		});
	}

	public static void Stop(int no)
	{
		if (_transTable.TryGetValue(no, out var value))
		{
			List<GameObject> list = new List<GameObject>();
			for (int i = 0; i < value.childCount; i++)
			{
				list.Add(value.GetChild(i).gameObject);
			}
			list.ForEach(delegate(GameObject p)
			{
				UnityEngine.Object.Destroy(p);
			});
		}
	}

	public static void Stop(Transform voiceTrans)
	{
		if (voiceTrans == null)
		{
			return;
		}
		List<GameObject> list = new List<GameObject>();
		foreach (Transform value in _transTable.Values)
		{
			if (value == null)
			{
				continue;
			}
			for (int i = 0; i < value.childCount; i++)
			{
				Transform child = value.GetChild(i);
				if (child.GetComponent<Voice_Component>().voiceTrans == voiceTrans)
				{
					list.Add(child.gameObject);
				}
			}
		}
		list.ForEach(delegate(GameObject p)
		{
			UnityEngine.Object.Destroy(p);
		});
	}

	public static void Stop(int no, Transform voiceTrans)
	{
		if (!_transTable.TryGetValue(no, out var value))
		{
			return;
		}
		List<GameObject> list = new List<GameObject>();
		for (int i = 0; i < value.childCount; i++)
		{
			Transform child = value.GetChild(i);
			if (child.GetComponent<Voice_Component>().voiceTrans == voiceTrans)
			{
				list.Add(child.gameObject);
			}
		}
		list.ForEach(UnityEngine.Object.Destroy);
	}

	public static float GetVolume(int charaNo)
	{
		if (!_Config.chara.TryGetValue(charaNo, out var value))
		{
			return 0f;
		}
		return value.sound.GetVolume();
	}

	public static AudioSource CreateCache(int voiceNo, AssetBundleData data)
	{
		return CreateCache(voiceNo, data.bundle, data.asset);
	}

	public static AudioSource CreateCache(int voiceNo, AssetBundleManifestData data)
	{
		return CreateCache(voiceNo, data.bundle, data.asset, data.manifest);
	}

	public static AudioSource CreateCache(int voiceNo, string bundle, string asset, string manifest = null)
	{
		if (!_audioCacheTable.TryGetValue(voiceNo, out var value))
		{
			value = (_audioCacheTable[voiceNo] = new Dictionary<string, AudioSource>());
		}
		if (!value.TryGetValue(asset, out var value2))
		{
			value2 = Create(rootASCache);
			value2.name = asset;
			value2.clip = new AssetBundleManifestData(bundle, asset, manifest).GetAsset<AudioClip>();
			Sound.Register(value2.clip);
			value.Add(asset, value2);
		}
		return value2;
	}

	public static void ReleaseCache(int voiceNo, string bundle, string asset, string manifest = null)
	{
		if (_audioCacheTable.TryGetValue(voiceNo, out var value))
		{
			if (value.TryGetValue(asset, out var value2))
			{
				Sound.Remove(value2.clip);
				UnityEngine.Object.Destroy(value2.gameObject);
				value.Remove(asset);
				AssetBundleManager.UnloadAssetBundle(bundle, isUnloadForceRefCount: false, manifest);
			}
			if (!value.Any())
			{
				_audioCacheTable.Remove(voiceNo);
			}
		}
	}

	protected override void Initialize()
	{
		Dictionary<int, VoiceInfo.Param> dictionary = new Dictionary<int, VoiceInfo.Param>();
		List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath("etcetra/list/config/", subdirCheck: true);
		assetBundleNameListFromPath.Sort();
		foreach (string item in assetBundleNameListFromPath)
		{
			foreach (List<VoiceInfo.Param> item2 in from p in AssetBundleManager.LoadAllAsset(item, typeof(VoiceInfo)).GetAllAssets<VoiceInfo>()
				select p.param)
			{
				foreach (VoiceInfo.Param item3 in item2)
				{
					dictionary[item3.No] = item3;
				}
			}
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: false);
		}
		infoTable = dictionary.OrderBy((KeyValuePair<int, VoiceInfo.Param> v) => v.Value.Sort).ToDictionary((KeyValuePair<int, VoiceInfo.Param> v) => v.Key, (KeyValuePair<int, VoiceInfo.Param> v) => v.Value);
		Dictionary<int, string> dictionary2 = new Dictionary<int, string>();
		foreach (int key in infoTable.Keys)
		{
			string value = "c" + key.MinusThroughToString("00");
			dictionary2.Add(key, value);
			Transform transform = new GameObject(value).transform;
			transform.SetParent(_rootPlay, worldPositionStays: false);
			_transTable.Add(key, transform);
		}
		_Config = new VoiceSystem("Volume", dictionary2);
		xmlCtrl = new Control("config", "voice.xml", "Voice", _Config);
		xmlCtrl.Read();
		this.OnDestroyAsObservable().Subscribe(delegate
		{
			xmlCtrl.Write();
		});
	}
}
