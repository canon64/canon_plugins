using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AIProject.Definitions;
using Illusion.Game;
using Manager;
using Sirenix.OdinInspector;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEx;

namespace AIProject;

public class SoundPack : SerializedScriptableObject
{
	public interface IData
	{
		string Summary { get; }

		string AssetBundleName { get; }

		string AssetName { get; }

		bool IsActive { get; }
	}

	[Serializable]
	public struct Data2D : IData
	{
		[SerializeField]
		private string _summary;

		[SerializeField]
		private string _assetBundleName;

		[SerializeField]
		private string _assetName;

		public string Summary
		{
			[CompilerGenerated]
			get
			{
				return _summary;
			}
		}

		public string AssetBundleName
		{
			[CompilerGenerated]
			get
			{
				return _assetBundleName;
			}
		}

		public string AssetName
		{
			[CompilerGenerated]
			get
			{
				return _assetName;
			}
		}

		public bool IsActive
		{
			[CompilerGenerated]
			get
			{
				return !AssetBundleName.IsNullOrEmpty() && !AssetName.IsNullOrEmpty();
			}
		}
	}

	[Serializable]
	public struct Data3D : IData
	{
		[SerializeField]
		private string _summary;

		[SerializeField]
		private string _assetBundleName;

		[SerializeField]
		private string _assetName;

		[SerializeField]
		[LabelText("減衰開始距離")]
		private float _minDistance;

		[SerializeField]
		[LabelText("減衰終了距離")]
		private float _maxDistance;

		public string Summary
		{
			[CompilerGenerated]
			get
			{
				return _summary;
			}
		}

		public string AssetBundleName
		{
			[CompilerGenerated]
			get
			{
				return _assetBundleName;
			}
		}

		public string AssetName
		{
			[CompilerGenerated]
			get
			{
				return _assetName;
			}
		}

		public float MinDistance
		{
			[CompilerGenerated]
			get
			{
				return _minDistance;
			}
		}

		public float MaxDistance
		{
			[CompilerGenerated]
			get
			{
				return _maxDistance;
			}
		}

		public bool IsActive
		{
			[CompilerGenerated]
			get
			{
				return !AssetBundleName.IsNullOrEmpty() && !AssetName.IsNullOrEmpty();
			}
		}
	}

	[Serializable]
	public class SoundSystemInfoGroup
	{
		[SerializeField]
		[FoldoutGroup("同時最大再生数", 0)]
		[LabelText("環境音同時最大再生数")]
		[MinValue(1.0)]
		private int _enviroSEMaxCount = 20;

		[SerializeField]
		[FoldoutGroup("同時最大再生数", 0)]
		[LabelText("３ＤＳＥ同時最大再生数")]
		[MinValue(1.0)]
		private int _game3DSEMaxCount = 20;

		public int EnviroSEMaxCount
		{
			[CompilerGenerated]
			get
			{
				return _enviroSEMaxCount;
			}
		}

		public int Game3DMaxCount
		{
			[CompilerGenerated]
			get
			{
				return _game3DSEMaxCount;
			}
		}
	}

	[Serializable]
	public class BGMInfoGroup
	{
		[SerializeField]
		[LabelText("マップ切り替わり時のフェードの時間(秒)")]
		private float _mapBGMFadeTime = 4f;

		public float MapBGMFadeTime
		{
			[CompilerGenerated]
			get
			{
				return _mapBGMFadeTime;
			}
		}
	}

	[Serializable]
	public class Game3DSEInfoGroup
	{
		[SerializeField]
		[LabelText("減衰終了距離＋α 再生距離")]
		[MinValue(0.0)]
		private float _marginMaxDistance = 10f;

		[SerializeField]
		[LabelText("停止時のフェード時間(秒)")]
		[MinValue(0.0)]
		private float _stopFadeTime = 0.5f;

		public float MarginMaxDistance
		{
			[CompilerGenerated]
			get
			{
				return _marginMaxDistance;
			}
		}

		public float StopFadeTime
		{
			[CompilerGenerated]
			get
			{
				return _stopFadeTime;
			}
		}
	}

	[Serializable]
	public class EnviroSEInfoGroup
	{
		[SerializeField]
		[LabelText("環境音切り替わり時のフェードの時間(秒)")]
		private float _fadeTime = 4f;

		[SerializeField]
		[LabelText("減衰値Max＋αでアクティブになる")]
		private float _enableDistance = 1f;

		[SerializeField]
		[LabelText("減衰値Max＋αで非アクティブになる")]
		private float _disableDistance = 10f;

		[SerializeField]
		[LabelText("ロールオフモード")]
		private AudioRolloffMode _rolloffMode;

		[SerializeField]
		private float _lineAreaSEBlendDistance = 2f;

		[SerializeField]
		[FoldoutGroup("広範囲系", 0)]
		[LabelText("エリア切り替わり時のフェードの時間(秒)")]
		private float _wideRangeQuickFadeTime = 0.5f;

		[SerializeField]
		[FoldoutGroup("広範囲系", 0)]
		[LabelText("天候切り替わり時のフェードの時間(秒)")]
		private float _wideRangeSlowFadeTime = 4f;

		public float FadeTime
		{
			[CompilerGenerated]
			get
			{
				return _fadeTime;
			}
		}

		public float EnableDistance
		{
			[CompilerGenerated]
			get
			{
				return _enableDistance;
			}
		}

		public float DisableDistance
		{
			[CompilerGenerated]
			get
			{
				return _disableDistance;
			}
		}

		public AudioRolloffMode RolloffMode
		{
			[CompilerGenerated]
			get
			{
				return _rolloffMode;
			}
		}

		public float LineAreaSEBlendDistance
		{
			[CompilerGenerated]
			get
			{
				return _lineAreaSEBlendDistance;
			}
		}

		public float WideRangeQuickFadeTime
		{
			[CompilerGenerated]
			get
			{
				return _wideRangeQuickFadeTime;
			}
		}

		public float WideRangeSlowFadeTime
		{
			[CompilerGenerated]
			get
			{
				return _wideRangeSlowFadeTime;
			}
		}
	}

	[Serializable]
	public class FootStepInfoGroup
	{
		[SerializeField]
		[LabelText("減衰開始距離")]
		[Range(0.1f, 1000f)]
		private float _minDistance = 1f;

		[SerializeField]
		[LabelText("減衰終了距離")]
		[Range(0.1f, 1000f)]
		private float _maxDistance = 15f;

		[SerializeField]
		[LabelText("減衰終了距離＋α 再生距離")]
		[MinValue(0.0)]
		private float _marginMaxDistance = 10f;

		[SerializeField]
		[LabelText("ロールオフモード")]
		private AudioRolloffMode _rolloffMode;

		public float MinDistance
		{
			[CompilerGenerated]
			get
			{
				return _minDistance;
			}
		}

		public float MaxDistance
		{
			[CompilerGenerated]
			get
			{
				return _maxDistance;
			}
		}

		public float MarginMaxDistance
		{
			[CompilerGenerated]
			get
			{
				return _marginMaxDistance;
			}
		}

		public AudioRolloffMode RolloffMode
		{
			[CompilerGenerated]
			get
			{
				return _rolloffMode;
			}
		}

		public float PlayEnableDistance
		{
			[CompilerGenerated]
			get
			{
				return _maxDistance + _marginMaxDistance;
			}
		}
	}

	[Serializable]
	public struct DoorSEIDInfo
	{
		[SerializeField]
		private int _openID;

		[SerializeField]
		private int _closeID;

		public int OpenID
		{
			[CompilerGenerated]
			get
			{
				return _openID;
			}
		}

		public int CloseID
		{
			[CompilerGenerated]
			get
			{
				return _closeID;
			}
		}
	}

	public enum PlayAreaType
	{
		Normal,
		Indoor
	}

	public enum EnvSEWeatherType
	{
		Clear,
		LightCloud,
		HeavyCloud,
		LightRain,
		HeavyRain,
		Fog
	}

	public enum SystemSE
	{
		Select,
		OK_L,
		OK_S,
		Cancel,
		Error,
		Save,
		Load,
		Shop,
		Popup,
		Page,
		Craft,
		Skill,
		BootDevice,
		Fishing_Result,
		Photo,
		Call,
		LevelUP,
		BoxOpen,
		BoxClose,
		Warp_In,
		Warp_Out
	}

	[Serializable]
	public struct WideRangeEnviroInfo
	{
		[SerializeField]
		[FormerlySerializedAs("ClearID")]
		[LabelText("晴れ")]
		private int[] _clearID;

		[SerializeField]
		[FormerlySerializedAs("LightCloudID")]
		[LabelText("曇り")]
		private int[] _lightCloudID;

		[SerializeField]
		[FormerlySerializedAs("HeavyCloudID")]
		[LabelText("曇天")]
		private int[] _heavyCloudID;

		[SerializeField]
		[FormerlySerializedAs("LightRainID")]
		[LabelText("小雨")]
		private int[] _lightRainID;

		[SerializeField]
		[FormerlySerializedAs("HeavyRainID")]
		[LabelText("大雨")]
		private int[] _heavyRainID;

		[SerializeField]
		[FormerlySerializedAs("FogID")]
		[LabelText("霧")]
		private int[] _fogID;

		public int[] ClearID
		{
			[CompilerGenerated]
			get
			{
				return _clearID;
			}
		}

		public int[] LightCloudID
		{
			[CompilerGenerated]
			get
			{
				return _lightCloudID;
			}
		}

		public int[] HeavyCloudID
		{
			[CompilerGenerated]
			get
			{
				return _heavyCloudID;
			}
		}

		public int[] LightRainID
		{
			[CompilerGenerated]
			get
			{
				return _lightRainID;
			}
		}

		public int[] HeavyRainID
		{
			[CompilerGenerated]
			get
			{
				return _heavyRainID;
			}
		}

		public int[] FogID
		{
			[CompilerGenerated]
			get
			{
				return _fogID;
			}
		}
	}

	[SerializeField]
	[FoldoutGroup("定義", 0)]
	[LabelText("サウンド全般的定義")]
	private SoundSystemInfoGroup _soundSystemInfo;

	[SerializeField]
	[FoldoutGroup("定義", 0)]
	[LabelText("BGM関連定義")]
	private BGMInfoGroup _bgmInfo;

	[SerializeField]
	[FoldoutGroup("定義", 0)]
	[LabelText("3DSE関連定義")]
	private Game3DSEInfoGroup _game3DSEInfo;

	[SerializeField]
	[FoldoutGroup("定義", 0)]
	[LabelText("環境音関連定義")]
	private EnviroSEInfoGroup _enviroInfo;

	[SerializeField]
	[FoldoutGroup("定義", 0)]
	[LabelText("足音関連定義")]
	private FootStepInfoGroup _footStepInfo;

	[SerializeField]
	[FoldoutGroup("定義/再生IDテーブル", 0)]
	[DictionaryDrawerSettings(KeyLabel = "ドアの材質", ValueLabel = "SE ID")]
	private Dictionary<DoorMatType, DoorSEIDInfo> _doorIDTable = new Dictionary<DoorMatType, DoorSEIDInfo>();

	[NonSerialized]
	private List<AudioSource> PlayingAudioList = new List<AudioSource>();

	[SerializeField]
	[FoldoutGroup("アクションSE関連", 0)]
	[DictionaryDrawerSettings(KeyLabel = "ID", ValueLabel = "Info")]
	private Dictionary<int, List<Data3D>> _actionSEDataTable = new Dictionary<int, List<Data3D>>();

	[SerializeField]
	[FoldoutGroup("システムSE関係", 0)]
	[DictionaryDrawerSettings(KeyLabel = "ID", ValueLabel = "Info")]
	private Dictionary<int, Data2D> _systemSEDataTable = new Dictionary<int, Data2D>();

	[SerializeField]
	[FoldoutGroup("システムSE関係", 0)]
	private Dictionary<SystemSE, int> _systemSETable = Enum.GetValues(typeof(SystemSE)).Cast<SystemSE>().ToDictionary((SystemSE x) => x, (SystemSE x) => -1);

	[SerializeField]
	[Header("男女共通")]
	[FoldoutGroup("足音関係", 0)]
	[DictionaryDrawerSettings(KeyLabel = "Tag Name", ValueLabel = "SE Type")]
	private Dictionary<string, AIProject.Definitions.Map.FootStepSE> _footStepSETagTable = Enum.GetValues(typeof(AIProject.Definitions.Map.FootStepSE)).Cast<AIProject.Definitions.Map.FootStepSE>().ToDictionary((AIProject.Definitions.Map.FootStepSE x) => $"FootStepSE_{x.ToString()}", (AIProject.Definitions.Map.FootStepSE x) => x);

	[SerializeField]
	[FoldoutGroup("足音関係/男性", 0)]
	[HideInInspector]
	[LabelText("音量スケール")]
	[Range(0f, 1f)]
	private float _maleFootStepVolumeScale = 1f;

	[SerializeField]
	[FoldoutGroup("足音関係/男性", 0)]
	[DictionaryDrawerSettings(KeyLabel = "SE ID", ValueLabel = "SE Info")]
	private Dictionary<int, Data2D> _maleFootStepSEData = new Dictionary<int, Data2D>();

	[SerializeField]
	[FoldoutGroup("足音関係/男性", 0)]
	[DictionaryDrawerSettings(KeyLabel = "SE Type", ValueLabel = "SE ID List")]
	private Dictionary<AIProject.Definitions.Map.FootStepSE, List<int>> _maleFootStepSEMeshTable = Enum.GetValues(typeof(AIProject.Definitions.Map.FootStepSE)).Cast<AIProject.Definitions.Map.FootStepSE>().ToDictionary((AIProject.Definitions.Map.FootStepSE x) => x, (AIProject.Definitions.Map.FootStepSE x) => new List<int>());

	[SerializeField]
	[FoldoutGroup("足音関係/男性", 0)]
	[DictionaryDrawerSettings(KeyLabel = "SE Type", ValueLabel = "SE ID List")]
	private Dictionary<AIProject.Definitions.Map.FootStepSE, List<int>> _maleBareFootStepSEMeshTable = new Dictionary<AIProject.Definitions.Map.FootStepSE, List<int>>();

	[SerializeField]
	[FoldoutGroup("足音関係/男性", 0)]
	[DictionaryDrawerSettings(KeyLabel = "SE Type", ValueLabel = "SE ID List")]
	private Dictionary<AIProject.Definitions.Map.FootStepSE, List<int>> _maleRainFootStepSEMeshTable = new Dictionary<AIProject.Definitions.Map.FootStepSE, List<int>>();

	[SerializeField]
	[FoldoutGroup("足音関係/女性", 0)]
	[HideInInspector]
	[LabelText("音量スケール")]
	[Range(0f, 1f)]
	private float _femaleFootStepVolumeScale = 1f;

	[SerializeField]
	[FoldoutGroup("足音関係/女性", 0)]
	[DictionaryDrawerSettings(KeyLabel = "SE ID", ValueLabel = "SE Info")]
	private Dictionary<int, Data2D> _femaleFootStepSEData = new Dictionary<int, Data2D>();

	[SerializeField]
	[FoldoutGroup("足音関係/女性", 0)]
	[DictionaryDrawerSettings(KeyLabel = "SE Type", ValueLabel = "SE ID List")]
	private Dictionary<AIProject.Definitions.Map.FootStepSE, List<int>> _femaleFootStepSEMeshTable = Enum.GetValues(typeof(AIProject.Definitions.Map.FootStepSE)).Cast<AIProject.Definitions.Map.FootStepSE>().ToDictionary((AIProject.Definitions.Map.FootStepSE x) => x, (AIProject.Definitions.Map.FootStepSE x) => new List<int>());

	[SerializeField]
	[FoldoutGroup("足音関係/女性", 0)]
	[DictionaryDrawerSettings(KeyLabel = "SE Type", ValueLabel = "SE ID List")]
	private Dictionary<AIProject.Definitions.Map.FootStepSE, List<int>> _femaleBareFootStepSEMeshTable = new Dictionary<AIProject.Definitions.Map.FootStepSE, List<int>>();

	[SerializeField]
	[FoldoutGroup("足音関係/女性", 0)]
	[DictionaryDrawerSettings(KeyLabel = "SE Type", ValueLabel = "SE ID List")]
	private Dictionary<AIProject.Definitions.Map.FootStepSE, List<int>> _femaleRainFootStepSEMeshTable = new Dictionary<AIProject.Definitions.Map.FootStepSE, List<int>>();

	[SerializeField]
	[FoldoutGroup("環境音関係", 0)]
	[DictionaryDrawerSettings(KeyLabel = "SE ID", ValueLabel = "Info")]
	private Dictionary<int, List<Data2D>> _env3DSEData = new Dictionary<int, List<Data2D>>();

	[SerializeField]
	[FoldoutGroup("環境音関係/広範囲系", 0)]
	[DictionaryDrawerSettings(KeyLabel = "Area Type", ValueLabel = "ID Info")]
	private Dictionary<PlayAreaType, WideRangeEnviroInfo> _wideRangeEnvTable = new Dictionary<PlayAreaType, WideRangeEnviroInfo>();

	[SerializeField]
	[FoldoutGroup("環境音関係/広範囲系", 0)]
	[DictionaryDrawerSettings(KeyLabel = "Map ID", ValueLabel = "Area ID")]
	private Dictionary<int, int[]> _muteAreaTable = new Dictionary<int, int[]>();

	private static Dictionary<string, Dictionary<string, UnityEx.ValueTuple<int, AudioClip>>> _usedEnviroSEInfo = new Dictionary<string, Dictionary<string, UnityEx.ValueTuple<int, AudioClip>>>();

	private static Dictionary<string, Dictionary<string, AudioClip>> _audioClipTable = new Dictionary<string, Dictionary<string, AudioClip>>();

	public SoundSystemInfoGroup SoundSystemInfo
	{
		[CompilerGenerated]
		get
		{
			return _soundSystemInfo;
		}
	}

	public BGMInfoGroup BGMInfo
	{
		[CompilerGenerated]
		get
		{
			return _bgmInfo;
		}
	}

	public Game3DSEInfoGroup Game3DInfo
	{
		[CompilerGenerated]
		get
		{
			return _game3DSEInfo;
		}
	}

	public EnviroSEInfoGroup EnviroInfo
	{
		[CompilerGenerated]
		get
		{
			return _enviroInfo;
		}
	}

	public FootStepInfoGroup FootStepInfo
	{
		[CompilerGenerated]
		get
		{
			return _footStepInfo;
		}
	}

	public Dictionary<DoorMatType, DoorSEIDInfo> DoorIDTable
	{
		[CompilerGenerated]
		get
		{
			return _doorIDTable;
		}
	}

	public static int EnviroSECount { get; private set; } = 0;


	public float GetFootStepSEVolumeScale(byte sex)
	{
		return sex switch
		{
			0 => _maleFootStepVolumeScale, 
			1 => _femaleFootStepVolumeScale, 
			_ => 1f, 
		};
	}

	public void Play(SystemSE se)
	{
		if (_systemSETable.TryGetValue(se, out var value) && _systemSEDataTable.TryGetValue(value, out var value2) && value2.IsActive)
		{
			Play(value2);
		}
	}

	public AudioSource Play(int key, Manager.Sound.Type soundType, float fadeTime = 0f)
	{
		if (!_actionSEDataTable.TryGetValue(key, out var value))
		{
			return null;
		}
		if (value.IsNullOrEmpty())
		{
			return null;
		}
		Data3D data3D = value[UnityEngine.Random.Range(0, value.Count)];
		return Play(data3D, soundType, fadeTime);
	}

	public AudioSource PlayFootStep(byte sex, bool bareFoot, AIProject.Definitions.Map.FootStepSE seType, Weather weather, PlayAreaType areaType)
	{
		if (sex != 0 && sex != 1)
		{
			return null;
		}
		List<int> value = null;
		if (areaType == PlayAreaType.Normal && value.IsNullOrEmpty() && (weather == Weather.Rain || weather == Weather.Storm))
		{
			Dictionary<AIProject.Definitions.Map.FootStepSE, List<int>> dictionary = ((sex != 0) ? _femaleRainFootStepSEMeshTable : _maleRainFootStepSEMeshTable);
			if (!dictionary.TryGetValue(seType, out value))
			{
				value = null;
			}
			else if (value.IsNullOrEmpty())
			{
				return null;
			}
		}
		if (bareFoot && value.IsNullOrEmpty())
		{
			Dictionary<AIProject.Definitions.Map.FootStepSE, List<int>> dictionary2 = ((sex != 0) ? _femaleBareFootStepSEMeshTable : _maleBareFootStepSEMeshTable);
			if (!dictionary2.TryGetValue(seType, out value))
			{
				value = null;
			}
			else if (value.IsNullOrEmpty())
			{
				return null;
			}
		}
		if (value.IsNullOrEmpty())
		{
			Dictionary<AIProject.Definitions.Map.FootStepSE, List<int>> dictionary3 = ((sex != 0) ? _femaleFootStepSEMeshTable : _maleFootStepSEMeshTable);
			if (!dictionary3.TryGetValue(seType, out value))
			{
				return null;
			}
			if (value.IsNullOrEmpty())
			{
				return null;
			}
		}
		if (value.IsNullOrEmpty())
		{
			return null;
		}
		int key = value[UnityEngine.Random.Range(0, value.Count)];
		Dictionary<int, Data2D> dictionary4 = ((sex != 0) ? _femaleFootStepSEData : _maleFootStepSEData);
		if (!dictionary4.TryGetValue(key, out var value2))
		{
			return null;
		}
		return Play(value2, Manager.Sound.Type.GameSE3D);
	}

	public AudioSource PlayFootStep(byte sex, bool bareFoot, string tag, Weather weather, PlayAreaType areaType)
	{
		if (tag == "Untagged")
		{
			return null;
		}
		if (sex != 0 && sex != 1)
		{
			return null;
		}
		if (!_footStepSETagTable.TryGetValue(tag ?? string.Empty, out var value))
		{
			return null;
		}
		return PlayFootStep(sex, bareFoot, value, weather, areaType);
	}

	public AudioSource PlayFootStep(byte sex, bool bareFoot, int mapID, int areaID, Weather weather, PlayAreaType areaType)
	{
		if (!Singleton<Manager.Resources>.IsInstance())
		{
			return null;
		}
		if (sex != 0 && sex != 1)
		{
			return null;
		}
		int[] array = null;
		Dictionary<int, Dictionary<int, Dictionary<int, UnityEx.ValueTuple<int[], int[], int[]>>>> defaultFootStepSETable = Singleton<Manager.Resources>.Instance.Sound.DefaultFootStepSETable;
		if (defaultFootStepSETable.TryGetValue(sex, out var value) && value.TryGetValue(mapID, out var value2) && value2.TryGetValue(areaID, out var value3))
		{
			if (areaType == PlayAreaType.Normal && array.IsNullOrEmpty() && (weather == Weather.Rain || weather == Weather.Storm))
			{
				array = value3.Item3;
			}
			if (bareFoot && array.IsNullOrEmpty())
			{
				array = value3.Item2;
			}
			if (array.IsNullOrEmpty())
			{
				array = value3.Item1;
			}
		}
		if (array.IsNullOrEmpty())
		{
			return null;
		}
		int key = array[UnityEngine.Random.Range(0, array.Length)];
		Dictionary<int, Data2D> dictionary = ((sex != 0) ? _femaleFootStepSEData : _maleFootStepSEData);
		if (!dictionary.TryGetValue(key, out var value4))
		{
			return null;
		}
		return Play(value4, Manager.Sound.Type.GameSE3D);
	}

	public AudioClip LoadEnviroSEClip(int clipID, out int idx)
	{
		if (!_env3DSEData.TryGetValue(clipID, out var value) || value.IsNullOrEmpty())
		{
			idx = -1;
			return null;
		}
		idx = UnityEngine.Random.Range(0, value.Count);
		return Load(value[idx]);
	}

	public AudioClip LoadEnviroSEClip(int clipID, int idx)
	{
		if (!_env3DSEData.TryGetValue(clipID, out var value) || value.IsNullOrEmpty() || idx < 0 || value.Count <= idx)
		{
			return null;
		}
		return Load(value[idx]);
	}

	private static void AddCountUsedEnviroSEInfo(string assetBundle, string asset, AudioClip clip)
	{
		if (!_usedEnviroSEInfo.TryGetValue(assetBundle, out var value) || value == null)
		{
			value = (_usedEnviroSEInfo[assetBundle] = new Dictionary<string, UnityEx.ValueTuple<int, AudioClip>>());
		}
		if (value.TryGetValue(asset, out var value2) && value2.Item2 != null)
		{
			value2.Item1++;
			value[asset] = value2;
			EnviroSECount++;
		}
		else if (clip != null)
		{
			value[asset] = new UnityEx.ValueTuple<int, AudioClip>(1, clip);
			EnviroSECount++;
		}
	}

	private static void RemoveUsedEnviroSEInfo(string assetBundle, string asset)
	{
		if (!_usedEnviroSEInfo.TryGetValue(assetBundle, out var value) || !value.TryGetValue(asset, out var value2))
		{
			return;
		}
		if (value2.Item2 == null)
		{
			value.Remove(asset);
			return;
		}
		value2.Item1--;
		EnviroSECount--;
		if (value2.Item1 <= 0)
		{
			UnityEngine.Resources.UnloadAsset(value2.Item2);
			value.Remove(asset);
		}
		else
		{
			value[asset] = value2;
		}
	}

	public AudioSource PlayEnviroSE(int clipID, float fadeTime = 0f)
	{
		if (!_env3DSEData.TryGetValue(clipID, out var value) || value.IsNullOrEmpty())
		{
			return null;
		}
		int index = ((value.Count != 0) ? UnityEngine.Random.Range(0, value.Count) : 0);
		Data2D data2D = value[index];
		AudioClip audioClip = Load(data2D);
		if (audioClip == null)
		{
			return null;
		}
		AudioSource audioSource = Illusion.Game.Utils.Sound.Play(Manager.Sound.Type.ENV, audioClip, fadeTime);
		if (audioSource == null)
		{
			return null;
		}
		string bundle = data2D.AssetBundleName;
		string asset = data2D.AssetName;
		AddCountUsedEnviroSEInfo(bundle, asset, audioClip);
		audioSource.OnDestroyAsObservable().Subscribe(delegate
		{
			RemoveUsedEnviroSEInfo(bundle, asset);
		});
		audioSource.dopplerLevel = 0f;
		audioSource.rolloffMode = EnviroInfo.RolloffMode;
		return audioSource;
	}

	public AudioSource PlayEnviroSE(int clipID, out int idx, float fadeTime = 0f)
	{
		if (!_env3DSEData.TryGetValue(clipID, out var value) || value.IsNullOrEmpty())
		{
			idx = -1;
			return null;
		}
		idx = ((value.Count != 1) ? UnityEngine.Random.Range(0, value.Count) : 0);
		Data2D data2D = value[idx];
		AudioClip audioClip = Load(data2D);
		if (audioClip == null)
		{
			idx = -1;
			return null;
		}
		AudioSource audioSource = Illusion.Game.Utils.Sound.Play(Manager.Sound.Type.ENV, audioClip, fadeTime);
		if (audioSource == null)
		{
			return null;
		}
		string bundle = data2D.AssetBundleName;
		string asset = data2D.AssetName;
		AddCountUsedEnviroSEInfo(bundle, asset, audioClip);
		audioSource.OnDestroyAsObservable().Subscribe(delegate
		{
			RemoveUsedEnviroSEInfo(bundle, asset);
		});
		return audioSource;
	}

	public AudioSource PlayEnviroSE(int clipID, int idx, float fadeTime = 0f)
	{
		if (!_env3DSEData.TryGetValue(clipID, out var value) || value.IsNullOrEmpty() || idx < 0 || value.Count <= idx)
		{
			return null;
		}
		Data2D data2D = value[idx];
		AudioClip audioClip = Load(data2D);
		if (audioClip == null)
		{
			return null;
		}
		AudioSource audioSource = Illusion.Game.Utils.Sound.Play(Manager.Sound.Type.ENV, audioClip, fadeTime);
		if (audioSource == null)
		{
			return null;
		}
		string bundle = data2D.AssetBundleName;
		string asset = data2D.AssetName;
		AddCountUsedEnviroSEInfo(bundle, asset, audioClip);
		audioSource.OnDestroyAsObservable().Subscribe(delegate
		{
			RemoveUsedEnviroSEInfo(bundle, asset);
		});
		return audioSource;
	}

	public int EnviroSEClipCount(int clipID)
	{
		if (!_env3DSEData.TryGetValue(clipID, out var value) || value.IsNullOrEmpty())
		{
			return 0;
		}
		return value.Count;
	}

	private int[] GetWideEnviroID(WideRangeEnviroInfo envInfo, Weather weather)
	{
		switch (weather)
		{
		case Weather.Clear:
		case Weather.Cloud1:
		case Weather.Cloud2:
			return envInfo.ClearID;
		case Weather.Cloud3:
			return envInfo.LightCloudID;
		case Weather.Cloud4:
			return envInfo.HeavyCloudID;
		case Weather.Rain:
			return envInfo.LightRainID;
		case Weather.Storm:
			return envInfo.HeavyRainID;
		case Weather.Fog:
			return envInfo.FogID;
		default:
			return null;
		}
	}

	public bool TryGetWideEnvIDList(PlayAreaType areaType, Weather weather, ref List<int> idList)
	{
		if (idList == null)
		{
			return false;
		}
		if (_wideRangeEnvTable.TryGetValue(areaType, out var value))
		{
			int[] wideEnviroID = GetWideEnviroID(value, weather);
			if (!wideEnviroID.IsNullOrEmpty())
			{
				idList.AddRange(wideEnviroID);
				return true;
			}
		}
		if (_wideRangeEnvTable.TryGetValue(PlayAreaType.Normal, out var value2))
		{
			int[] wideEnviroID2 = GetWideEnviroID(value2, weather);
			if (!wideEnviroID2.IsNullOrEmpty())
			{
				idList.AddRange(wideEnviroID2);
				return true;
			}
		}
		return false;
	}

	public bool WideEnvMuteArea(int mapID, int areaID)
	{
		if (_muteAreaTable.IsNullOrEmpty())
		{
			return false;
		}
		if (_muteAreaTable.TryGetValue(mapID, out var value) && !value.IsNullOrEmpty())
		{
			return value.Contains(areaID);
		}
		return false;
	}

	public static void UnloadAudioClipAll()
	{
		foreach (KeyValuePair<string, Dictionary<string, AudioClip>> item in _audioClipTable)
		{
			if (item.Value.IsNullOrEmpty())
			{
				continue;
			}
			foreach (KeyValuePair<string, AudioClip> item2 in item.Value)
			{
				if (item2.Value != null)
				{
					UnityEngine.Resources.UnloadAsset(item2.Value);
				}
			}
		}
		_audioClipTable.Clear();
		_usedEnviroSEInfo.Clear();
	}

	private void Play(IData data)
	{
		Illusion.Game.Utils.Sound.Get(Manager.Sound.Type.SystemSE, data.AssetBundleName, data.AssetName)?.Play();
	}

	private AudioClip Load(IData data)
	{
		if (data.AssetBundleName.IsNullOrEmpty() || data.AssetName.IsNullOrEmpty())
		{
			if (Debug.isDebugBuild)
			{
				Debug.LogWarning($"SoundPack:Load BundleName[{data.AssetBundleName ?? string.Empty}] AssetName[{data.AssetName ?? string.Empty}] に未記入があります");
			}
			return null;
		}
		if (!_audioClipTable.TryGetValue(data.AssetBundleName, out var value))
		{
			Dictionary<string, AudioClip> dictionary = new Dictionary<string, AudioClip>();
			_audioClipTable[data.AssetBundleName] = dictionary;
			value = dictionary;
		}
		if (!value.TryGetValue(data.AssetName, out var value2) || value2 == null)
		{
			AudioClip audioClip = CommonLib.LoadAsset<AudioClip>(data.AssetBundleName, data.AssetName, clone: false, string.Empty);
			value[data.AssetName] = audioClip;
			return audioClip;
		}
		return value2;
	}

	public bool TryGetActionSEData(int clipID, out Data3D data)
	{
		if (_actionSEDataTable.TryGetValue(clipID, out var value) && !value.IsNullOrEmpty())
		{
			data = value[UnityEngine.Random.Range(0, value.Count)];
			return true;
		}
		data = default(Data3D);
		return false;
	}

	private int GetSqrDistanceSort(Transform camera, Transform t1, Transform t2)
	{
		Vector3 vector = t1.position - camera.position;
		Vector3 vector2 = t2.position - camera.position;
		return (int)(Vector3.SqrMagnitude(vector) - Vector3.SqrMagnitude(vector2));
	}

	public AudioSource Play(IData data, Manager.Sound.Type type, float fadeTime = 0f)
	{
		AudioClip audioClip = Load(data);
		if (audioClip == null)
		{
			return null;
		}
		AudioSource audio = Illusion.Game.Utils.Sound.Play(type, audioClip, fadeTime);
		if (type == Manager.Sound.Type.GameSE3D && audio != null)
		{
			PlayingAudioList.RemoveAll((AudioSource x) => x == null || x.gameObject == null);
			if (_soundSystemInfo.Game3DMaxCount <= PlayingAudioList.Count)
			{
				int num = PlayingAudioList.Count - _soundSystemInfo.Game3DMaxCount + 1;
				Transform cameraT = Singleton<Manager.Map>.Instance.Player.CameraControl.CameraComponent.transform;
				List<AudioSource> list = ListPool<AudioSource>.Get();
				list.AddRange(PlayingAudioList);
				list.Sort((AudioSource a1, AudioSource a2) => GetSqrDistanceSort(cameraT, a2.transform, a1.transform));
				for (int i = 0; i < num; i++)
				{
					AudioSource element = list.GetElement(i);
					PlayingAudioList.Remove(element);
					UnityEngine.Object.Destroy(element.gameObject);
				}
				ListPool<AudioSource>.Release(list);
			}
			audio.OnDestroyAsObservable().Subscribe(delegate
			{
				if (audio != null)
				{
					PlayingAudioList.Remove(audio);
				}
			});
			PlayingAudioList.Add(audio);
		}
		if (data is Data3D data3D)
		{
			audio.minDistance = data3D.MinDistance;
			audio.maxDistance = data3D.MaxDistance;
		}
		return audio;
	}
}
