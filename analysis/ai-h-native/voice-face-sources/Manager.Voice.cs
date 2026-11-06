using System.Collections.Generic;
using System.Linq;
using ConfigScene;
using Illusion;
using Illusion.CustomAttributes;
using Illusion.Elements.Xml;
using Illusion.Extensions;
using UnityEngine;
using UnityEngine.Audio;

namespace Manager;

public sealed class Voice : Singleton<Voice>
{
	public enum Type
	{
		PCM
	}

	private Dictionary<int, VoiceInfo.Param> _voiceInfoDic;

	[SerializeField]
	[NotEditable]
	private Transform rootSetting;

	[SerializeField]
	[NotEditable]
	private Transform rootPlay;

	[SerializeField]
	[NotEditable]
	private Transform ASCacheRoot;

	[SerializeField]
	[NotEditable]
	private GameObject[] settingObjects;

	private Dictionary<int, Transform> voiceDic = new Dictionary<int, Transform>();

	private Dictionary<int, Dictionary<string, AudioSource>> dicASCache;

	private const string UserPath = "config";

	private const string FileName = "voice.xml";

	private const string RootName = "Voice";

	private const string ElementName = "Volume";

	private Control xmlCtrl;

	public static AudioMixer Mixer => Sound.Mixer;

	public Dictionary<int, VoiceInfo.Param> voiceInfoDic => this.GetCache(ref _voiceInfoDic, () => voiceInfoList.ToDictionary((VoiceInfo.Param v) => v.No, (VoiceInfo.Param v) => v));

	public List<VoiceInfo.Param> voiceInfoList { get; private set; }

	public VoiceSystem _Config { get; private set; }

	protected override void Awake()
	{
		if (!CheckInstance())
		{
			return;
		}
		Object.DontDestroyOnLoad(base.gameObject);
		rootSetting = new GameObject("SettingObjectPCM").transform;
		rootSetting.SetParent(base.transform, worldPositionStays: false);
		rootPlay = new GameObject("PlayObjectPCM").transform;
		rootPlay.SetParent(base.transform, worldPositionStays: false);
		settingObjects = new GameObject[Illusion.Utils.Enum<Type>.Length];
		for (int i = 0; i < settingObjects.Length; i++)
		{
			LoadSetting((Type)i);
		}
		dicASCache = new Dictionary<int, Dictionary<string, AudioSource>>();
		ASCacheRoot = new GameObject("AudioSourceCache").transform;
		ASCacheRoot.SetParent(rootPlay, worldPositionStays: false);
		string text = AssetBundleManager.BaseDownloadingURL + "sound/data/pcm/";
		List<VoiceInfo.Param> sortList = new List<VoiceInfo.Param>();
		HashSet<int> distinctCheck = new HashSet<int>();
		CommonLib.GetAssetBundleNameListFromPath("etcetra/list/config/", subdirCheck: true).ForEach(delegate(string file)
		{
			AssetBundleLoadAssetOperation assetBundleLoadAssetOperation = AssetBundleManager.LoadAllAsset(file, typeof(VoiceInfo));
			foreach (List<VoiceInfo.Param> item in from voiceInfo in assetBundleLoadAssetOperation.GetAllAssets<VoiceInfo>()
				select voiceInfo.param)
			{
				foreach (VoiceInfo.Param p in item.Where((VoiceInfo.Param param) => !distinctCheck.Add(param.No)))
				{
					Debug.LogWarning("Remove Voice No:" + p.No);
					sortList.Remove(sortList.FirstOrDefault((VoiceInfo.Param l) => l.No == p.No));
				}
				sortList.AddRange(item);
			}
			AssetBundleManager.UnloadAssetBundle(file, isUnloadForceRefCount: false);
		});
		sortList.Sort((VoiceInfo.Param a, VoiceInfo.Param b) => a.Sort.CompareTo(b.Sort));
		voiceInfoList = sortList;
		Dictionary<int, string> dic = new Dictionary<int, string>();
		voiceInfoList.ForEach(delegate(VoiceInfo.Param p)
		{
			string value = string.Format("c{0}", p.No.MinusThroughToString("00"));
			dic.Add(p.No, value);
			Transform transform = new GameObject(value).transform;
			transform.SetParent(rootPlay, worldPositionStays: false);
			voiceDic.Add(p.No, transform);
		});
		_Config = new VoiceSystem("Volume", dic);
		xmlCtrl = new Control("config", "voice.xml", "Voice", _Config);
		Load();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		Object.Destroy(rootSetting.gameObject);
		Object.Destroy(rootPlay.gameObject);
		voiceDic.Clear();
	}

	private void LoadSetting(Type type, int settingNo = -1)
	{
		string text = type.ToString();
		AssetBundleData assetBundleData = new AssetBundleData("sound/setting/object/00.unity3d", text.ToLower());
		GameObject gameObject = Object.Instantiate(assetBundleData.GetAsset<GameObject>(), rootSetting, worldPositionStays: false);
		gameObject.name = text + "_Setting";
		AudioSource component = gameObject.GetComponent<AudioSource>();
		Singleton<Sound>.Instance.AudioSettingData(component, settingNo);
		settingObjects[(int)type] = gameObject;
		assetBundleData.UnloadBundle(isUnloadForceRefCount: true);
	}

	public void SetParent(int no, Transform t)
	{
		if (voiceDic.TryGetValue(no, out var value))
		{
			t.SetParent(value, worldPositionStays: false);
		}
	}

	public void Bind(LoadVoice script)
	{
		if (script.audioSource == null)
		{
			if (!voiceDic.TryGetValue(script.no, out var value))
			{
				return;
			}
			Singleton<Sound>.Instance.SetParent(value, script, settingObjects[(int)script.type]);
		}
		AudioSource audioSource = script.audioSource;
		audioSource.clip = script.clip;
		Singleton<Sound>.Instance.Register(script.clip);
		audioSource.name = audioSource.clip.name;
		audioSource.volume = GetVolume(script.no);
		Sound.OutputSettingData outputSettingData = Singleton<Sound>.Instance.AudioSettingData(audioSource, script.settingNo);
		if (outputSettingData != null)
		{
			script.delayTime = outputSettingData.delayTime;
		}
	}

	public List<AudioSource> GetPlayingList(int no)
	{
		List<AudioSource> list = new List<AudioSource>();
		if (!voiceDic.TryGetValue(no, out var value))
		{
			return list;
		}
		for (int i = 0; i < value.childCount; i++)
		{
			list.Add(value.GetChild(i).GetComponent<AudioSource>());
		}
		return list;
	}

	public bool IsVoiceCheck(int no)
	{
		if (!voiceDic.TryGetValue(no, out var value))
		{
			return false;
		}
		return value.childCount != 0;
	}

	public bool IsVoiceCheck(Transform voiceTrans, bool isLoopCheck = true)
	{
		foreach (Transform value in voiceDic.Values)
		{
			for (int i = 0; i < value.childCount; i++)
			{
				LoadVoice componentInChildren = value.GetChild(i).GetComponentInChildren<LoadVoice>();
				if (componentInChildren.voiceTrans == voiceTrans && (isLoopCheck || !componentInChildren.audioSource.loop))
				{
					return true;
				}
			}
		}
		return false;
	}

	public bool IsVoiceCheck(int no, Transform voiceTrans, bool isLoopCheck = true)
	{
		if (!voiceDic.TryGetValue(no, out var value))
		{
			return false;
		}
		for (int i = 0; i < value.childCount; i++)
		{
			LoadVoice componentInChildren = value.GetChild(i).GetComponentInChildren<LoadVoice>();
			if (componentInChildren.voiceTrans == voiceTrans && (isLoopCheck || !componentInChildren.audioSource.loop))
			{
				return true;
			}
		}
		return false;
	}

	public bool IsVoiceCheck()
	{
		return voiceDic.Values.Any((Transform v) => v.childCount != 0);
	}

	public Transform Play(int no, string assetBundleName, string assetName, float pitch = 1f, float delayTime = 0f, float fadeTime = 0f, bool isAsync = true, Transform voiceTrans = null, Type type = Type.PCM, int settingNo = -1, bool isPlayEndDelete = true, bool isBundleUnload = true, bool is2D = false)
	{
		LoadVoice loadVoice = new GameObject("Voice Loading").AddComponent<LoadVoice>();
		loadVoice.no = no;
		loadVoice.assetBundleName = assetBundleName;
		loadVoice.assetName = assetName;
		loadVoice.pitch = pitch;
		loadVoice.delayTime = delayTime;
		loadVoice.fadeTime = fadeTime;
		loadVoice.isAsync = isAsync;
		loadVoice.voiceTrans = voiceTrans;
		loadVoice.type = type;
		loadVoice.settingNo = settingNo;
		loadVoice.isPlayEndDelete = isPlayEndDelete;
		loadVoice.isBundleUnload = isBundleUnload;
		loadVoice.is2D = is2D;
		if (!voiceDic.TryGetValue(no, out var value))
		{
			return null;
		}
		return Singleton<Sound>.Instance.SetParent(value, loadVoice, settingObjects[(int)type]);
	}

	public Transform OnecePlay(int no, string assetBundleName, string assetName, float pitch = 1f, float delayTime = 0f, float fadeTime = 0f, bool isAsync = true, Transform voiceTrans = null, Type type = Type.PCM, int settingNo = -1, bool isPlayEndDelete = true, bool isBundleUnload = true, bool is2D = false)
	{
		StopAll();
		return Play(no, assetBundleName, assetName, pitch, delayTime, fadeTime, isAsync, voiceTrans, type, settingNo, isPlayEndDelete, isBundleUnload, is2D);
	}

	public Transform OnecePlayChara(int no, string assetBundleName, string assetName, float pitch = 1f, float delayTime = 0f, float fadeTime = 0f, bool isAsync = true, Transform voiceTrans = null, Type type = Type.PCM, int settingNo = -1, bool isPlayEndDelete = true, bool isBundleUnload = true, bool is2D = false)
	{
		if (voiceTrans != null)
		{
			Stop(no, voiceTrans);
		}
		else
		{
			Stop(no);
		}
		return Play(no, assetBundleName, assetName, pitch, delayTime, fadeTime, isAsync, voiceTrans, type, settingNo, isPlayEndDelete, isBundleUnload, is2D);
	}

	public void StopAll(bool isLoopStop = true)
	{
		List<GameObject> list = new List<GameObject>();
		foreach (Transform value in voiceDic.Values)
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
			Object.Destroy(p);
		});
	}

	public void Stop(int no)
	{
		if (voiceDic.TryGetValue(no, out var value))
		{
			List<GameObject> list = new List<GameObject>();
			for (int i = 0; i < value.childCount; i++)
			{
				list.Add(value.GetChild(i).gameObject);
			}
			list.ForEach(delegate(GameObject p)
			{
				Object.Destroy(p);
			});
		}
	}

	public void Stop(Transform voiceTrans)
	{
		List<GameObject> list = new List<GameObject>();
		foreach (Transform value in voiceDic.Values)
		{
			for (int i = 0; i < value.childCount; i++)
			{
				Transform child = value.GetChild(i);
				LoadVoice componentInChildren = child.GetComponentInChildren<LoadVoice>();
				if (componentInChildren.voiceTrans == voiceTrans)
				{
					list.Add(child.gameObject);
				}
			}
		}
		list.ForEach(delegate(GameObject p)
		{
			Object.Destroy(p);
		});
	}

	public void Stop(int no, Transform voiceTrans)
	{
		if (!voiceDic.TryGetValue(no, out var value))
		{
			return;
		}
		List<GameObject> list = new List<GameObject>();
		for (int i = 0; i < value.childCount; i++)
		{
			Transform child = value.GetChild(i);
			LoadVoice componentInChildren = child.GetComponentInChildren<LoadVoice>();
			if (componentInChildren.voiceTrans == voiceTrans)
			{
				list.Add(child.gameObject);
			}
		}
		list.ForEach(Object.Destroy);
	}

	public float GetVolume(int charaNo)
	{
		if (!_Config.chara.TryGetValue(charaNo, out var value))
		{
			Debug.LogError("charaNo:" + charaNo + "VolumeNone");
			return 0f;
		}
		return value.sound.GetVolume();
	}

	public AudioSource CreateCache(int voiceNo, AssetBundleData data)
	{
		return CreateCache(voiceNo, data.bundle, data.asset);
	}

	public AudioSource CreateCache(int voiceNo, AssetBundleManifestData data)
	{
		return CreateCache(voiceNo, data.bundle, data.asset, data.manifest);
	}

	public AudioSource CreateCache(int voiceNo, string bundle, string asset, string manifest = null)
	{
		if (!dicASCache.TryGetValue(voiceNo, out var value))
		{
			Dictionary<string, AudioSource> dictionary = new Dictionary<string, AudioSource>();
			dicASCache[voiceNo] = dictionary;
			value = dictionary;
		}
		if (!value.TryGetValue(asset, out var value2))
		{
			GameObject gameObject = Object.Instantiate(settingObjects[0], ASCacheRoot, worldPositionStays: false);
			gameObject.name = asset;
			gameObject.SetActive(value: true);
			value2 = gameObject.GetComponent<AudioSource>();
			value2.clip = AssetBundleManager.LoadAsset(bundle, asset, typeof(AudioClip), manifest).GetAsset<AudioClip>();
			Singleton<Sound>.Instance.Register(value2.clip);
			value.Add(asset, value2);
		}
		return value2;
	}

	public void ReleaseCache(int voiceNo, string bundle, string asset, string manifest = null)
	{
		if (dicASCache.TryGetValue(voiceNo, out var value))
		{
			if (value.TryGetValue(asset, out var value2))
			{
				Singleton<Sound>.Instance.Remove(value2.clip);
				Object.Destroy(value2.gameObject);
				value.Remove(asset);
				AssetBundleManager.UnloadAssetBundle(bundle, isUnloadForceRefCount: false, manifest);
			}
			if (!value.Any())
			{
				dicASCache.Remove(voiceNo);
			}
		}
	}

	public void Reset()
	{
		if (xmlCtrl != null)
		{
			xmlCtrl.Init();
		}
	}

	public void Load()
	{
		xmlCtrl.Read();
	}

	public void Save()
	{
		xmlCtrl.Write();
	}
}
