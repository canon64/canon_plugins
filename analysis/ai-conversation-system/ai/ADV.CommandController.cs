using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Illusion.Elements.Reference;
using Illusion.Extensions;
using Manager;
using UnityEngine;

namespace ADV;

public class CommandController : MonoBehaviour
{
	public class FontColor : AutoIndexer<Tuple<int, Color>>
	{
		public new Color this[string key]
		{
			get
			{
				switch (key)
				{
				case "[P]":
				case "[P姓]":
				case "[P名]":
				case "[P名前]":
				case "[Pあだ名]":
					return GetConfigColor(0).Value;
				case "[H]":
				case "[H姓]":
				case "[H名]":
				case "[H名前]":
				case "[Hあだ名]":
					return GetConfigColor(1).Value;
				default:
				{
					Tuple<int, Color> tuple = base[key];
					Color? configColor = GetConfigColor(tuple.Item1);
					return (!configColor.HasValue) ? tuple.Item2 : configColor.Value;
				}
				}
			}
			private set
			{
				Debug.LogError("FontColor Set");
			}
		}

		public FontColor()
			: base(Tuple.Create(-1, Color.white))
		{
		}

		public void Set(string key, Color color)
		{
			Set(key, initializeValue.Item1, color);
		}

		public void Set(string key, int configIndex)
		{
			Set(key, configIndex, initializeValue.Item2);
		}

		private void Set(string key, int configIndex, Color color)
		{
			dic[key] = Tuple.Create(configIndex, color);
		}

		private Color? GetConfigColor(int configIndex)
		{
			return null;
		}
	}

	[Serializable]
	private class CharaCorrectHeightCamera
	{
		[Serializable]
		private struct Pair
		{
			public Vector3 min;

			public Vector3 max;
		}

		[SerializeField]
		private Pair pos;

		[SerializeField]
		private Pair ang;

		public bool Calculate(IEnumerable<CharaData> datas, out Vector3 pos, out Vector3 ang)
		{
			if (datas == null || !datas.Any())
			{
				pos = Vector3.zero;
				ang = Vector3.zero;
				return false;
			}
			float shape = datas.Average((CharaData item) => item.chaCtrl.GetShapeBodyValue(0));
			pos = MathfEx.GetShapeLerpPositionValue(shape, this.pos.min, this.pos.max);
			ang = MathfEx.GetShapeLerpAngleValue(shape, this.ang.min, this.ang.max);
			return true;
		}
	}

	private bool rootPositionLoaded;

	private Root _rootPosition;

	private CommandList nowCommandList;

	private CommandList backGroundCommandList;

	[SerializeField]
	private bool _useCorrectCamera = true;

	[SerializeField]
	private CharaCorrectHeightCamera _correctCamera = new CharaCorrectHeightCamera();

	private bool useCorrectCameraBakup = true;

	private Root rootPosition => this.GetCacheObject(ref _rootPosition, delegate
	{
		rootPositionLoaded = true;
		return Root.Load(Singleton<Map>.Instance.MapRoot);
	});

	public Transform CharaRoot
	{
		[CompilerGenerated]
		get
		{
			return rootPosition.charaRoot;
		}
	}

	public Dictionary<string, Transform> characterStandNulls
	{
		[CompilerGenerated]
		get
		{
			return rootPosition.characterStandNulls;
		}
	}

	public Transform EventCGRoot
	{
		[CompilerGenerated]
		get
		{
			return rootPosition.eventCGRoot;
		}
	}

	public Transform ObjectRoot
	{
		[CompilerGenerated]
		get
		{
			return rootPosition.objectRoot;
		}
	}

	public Transform NullRoot
	{
		[CompilerGenerated]
		get
		{
			return rootPosition.nullRoot;
		}
	}

	public Transform BaseRoot
	{
		[CompilerGenerated]
		get
		{
			return rootPosition.baseRoot;
		}
	}

	public Transform CameraRoot
	{
		[CompilerGenerated]
		get
		{
			return rootPosition.cameraRoot;
		}
	}

	public CommandList NowCommandList => nowCommandList;

	public CommandList BackGroundCommandList => backGroundCommandList;

	public List<CharaData> LoadingCharaList
	{
		[CompilerGenerated]
		get
		{
			return loadingCharaList;
		}
	}

	private List<CharaData> loadingCharaList { get; } = new List<CharaData>();


	public Dictionary<int, CharaData> Characters { get; } = new Dictionary<int, CharaData>();


	public Dictionary<string, GameObject> Objects { get; } = new Dictionary<string, GameObject>();


	public Dictionary<string, Vector3> V3Dic { get; } = new Dictionary<string, Vector3>();


	public Dictionary<string, Transform> NullDic { get; } = new Dictionary<string, Transform>();


	public Dictionary<string, Game.Expression> expDic { get; } = new Dictionary<string, Game.Expression>();


	public Dictionary<string, string[]> motionDic { get; } = new Dictionary<string, string[]>();


	public FontColor fontColor { get; } = new FontColor();


	public bool useCorrectCamera { get; set; }

	private TextScenario scenario { get; set; }

	public bool GetV3Dic(string arg, out Vector3 pos)
	{
		pos = Vector3.zero;
		float result;
		return !arg.IsNullOrEmpty() && !float.TryParse(arg, out result) && V3Dic.TryGetValue(arg, out pos);
	}

	public CharaData GetChara(int no)
	{
		if (no < 0)
		{
			TextScenario.ParamData paramData = null;
			paramData = ((no != -1) ? scenario.heroineList[Mathf.Abs(no + 2)] : scenario.player);
			if (paramData != null)
			{
				foreach (KeyValuePair<int, CharaData> character in Characters)
				{
					if (character.Value.data == paramData)
					{
						return character.Value;
					}
				}
				return new CharaData(paramData, scenario, null);
			}
		}
		CharaData value;
		return (!Characters.TryGetValue(no, out value)) ? scenario.currentChara : value;
	}

	public void AddChara(int no, CharaData data)
	{
		RemoveChara(no);
		Characters[no] = data;
	}

	public void RemoveChara(int no)
	{
		if (Characters.TryGetValue(no, out var value))
		{
			value.Release();
			loadingCharaList.Remove(value);
		}
		Characters.Remove(no);
	}

	public void Initialize()
	{
		useCorrectCameraBakup = _useCorrectCamera;
		if (scenario == null)
		{
			scenario = GetComponent<TextScenario>();
			nowCommandList = new CommandList(scenario);
			backGroundCommandList = new CommandList(scenario);
		}
		else
		{
			nowCommandList.Clear();
			backGroundCommandList.Clear();
		}
		loadingCharaList.Clear();
		Objects.Clear();
		Characters.Clear();
		V3Dic.Clear();
		NullDic.Clear();
		expDic.Clear();
		motionDic.Clear();
		fontColor.Clear();
		rootPosition.SetBackup();
	}

	public void SetObject(GameObject go)
	{
		if (Objects.TryGetValue(go.name, out var value))
		{
			UnityEngine.Object.Destroy(value);
		}
		go.transform.SetParent(ObjectRoot, worldPositionStays: false);
		Objects[go.name] = go;
	}

	public void SetNull(Transform nullT)
	{
		if (NullDic.TryGetValue(nullT.name, out var value) && value != null)
		{
			UnityEngine.Object.Destroy(value.gameObject);
		}
		nullT.SetParent(NullRoot, worldPositionStays: false);
		NullDic[nullT.name] = nullT;
	}

	public void ReleaseObject()
	{
		foreach (GameObject value in Objects.Values)
		{
			if (value != null)
			{
				UnityEngine.Object.Destroy(value);
			}
		}
		Objects.Clear();
	}

	public void ReleaseNull()
	{
		foreach (Transform value in NullDic.Values)
		{
			if (value != null)
			{
				UnityEngine.Object.Destroy(value.gameObject);
			}
		}
		NullDic.Clear();
	}

	public void ReleaseChara()
	{
		foreach (CharaData value in Characters.Values)
		{
			value.Release();
		}
		Characters.Clear();
	}

	public void ReleaseEventCG()
	{
		if (!rootPositionLoaded || _rootPosition == null)
		{
			return;
		}
		Transform eventCGRoot = _rootPosition.eventCGRoot;
		if (!(eventCGRoot == null))
		{
			eventCGRoot.gameObject.Children().ForEach(delegate(GameObject go)
			{
				UnityEngine.Object.Destroy(go);
			});
		}
	}

	public void Release()
	{
		ReleaseObject();
		ReleaseNull();
		ReleaseChara();
		ReleaseEventCG();
		_useCorrectCamera = useCorrectCameraBakup;
	}

	private void OnDestroy()
	{
		Release();
		if (rootPositionLoaded && _rootPosition != null)
		{
			UnityEngine.Object.Destroy(_rootPosition.gameObject);
		}
	}
}
