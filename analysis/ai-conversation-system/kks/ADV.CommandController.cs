using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Illusion.Elements.Reference;
using Manager;
using SaveData;
using UnityEngine;

namespace ADV;

public class CommandController : MonoBehaviour
{
	public class FontColor : AutoIndexer<(int, Color)>
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
					(int, Color) tuple = base[key];
					Color? configColor = GetConfigColor(tuple.Item1);
					if (!configColor.HasValue)
					{
						return tuple.Item2;
					}
					return configColor.Value;
				}
				}
			}
			private set
			{
			}
		}

		public FontColor()
			: base((-1, Color.white))
		{
		}

		public void Set(string key, Color color)
		{
			Set(key, base.initializeValue.Item1, color);
		}

		public void Set(string key, int configIndex)
		{
			Set(key, configIndex, base.initializeValue.Item2);
		}

		private void Set(string key, int configIndex, Color color)
		{
			base.table[key] = (configIndex, color);
		}

		private Color? GetConfigColor(int configIndex)
		{
			return configIndex switch
			{
				0 => Manager.Config.TextData.Font0Color, 
				1 => Manager.Config.TextData.Font1Color, 
				2 => Manager.Config.TextData.Font2Color, 
				_ => null, 
			};
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
			pos = MotionIK.GetShapeLerpPositionValue(shape, this.pos.min, this.pos.max);
			ang = MotionIK.GetShapeLerpAngleValue(shape, this.ang.min, this.ang.max);
			return true;
		}
	}

	[SerializeField]
	private Transform character;

	private Dictionary<string, Transform> _characterStandNulls;

	private int charaStartIndex;

	[SerializeField]
	private RectTransform character2D;

	[SerializeField]
	private Transform eventCGRoot;

	[SerializeField]
	private Transform objectRoot;

	[SerializeField]
	private Transform nullRoot;

	[SerializeField]
	private Transform basePosition;

	[SerializeField]
	private Transform cameraPosition;

	[SerializeField]
	private HitReaction hitReaction;

	private Action correctCameraReset;

	[SerializeField]
	private bool _useCorrectCamera = true;

	[SerializeField]
	private CharaCorrectHeightCamera correctCamera = new CharaCorrectHeightCamera();

	private bool useCorrectCameraBakup;

	private CommandList nowCommandList;

	private CommandList backGroundCommandList;

	private List<CharaData> loadingCharaList;

	private TextScenario scenario;

	public Transform Character => character;

	public Dictionary<string, Transform> characterStandNulls
	{
		get
		{
			if (_characterStandNulls == null)
			{
				return _characterStandNulls = (from i in Enumerable.Range(0, charaStartIndex)
					select character.GetChild(i)).ToDictionary((Transform v) => v.name, (Transform v) => v, StringComparer.InvariantCultureIgnoreCase);
			}
			return _characterStandNulls;
		}
	}

	private BackupPosRot cameraRoot { get; set; }

	private BackupPosRot charaRoot { get; set; }

	public RectTransform Character2D => character2D;

	public Transform EventCGRoot => eventCGRoot;

	public Transform ObjectRoot => objectRoot;

	public Transform NullRoot => nullRoot;

	public Transform BasePositon => basePosition;

	public Transform CameraPosition => cameraPosition;

	public HitReaction HitReaction => hitReaction;

	public bool useCorrectCamera
	{
		get
		{
			return _useCorrectCamera;
		}
		set
		{
			_useCorrectCamera = value;
			if (!value && correctCameraReset != null)
			{
				correctCameraReset();
				correctCameraReset = null;
			}
		}
	}

	public CommandList NowCommandList => nowCommandList;

	public CommandList BackGroundCommandList => backGroundCommandList;

	public List<CharaData> LoadingCharaList => loadingCharaList;

	public Dictionary<int, CharaData> Characters { get; private set; }

	public Dictionary<int, CharaData2D> Characters2D { get; private set; }

	public Dictionary<string, GameObject> Objects { get; private set; }

	public Dictionary<string, Vector3> V3Dic { get; private set; }

	public Dictionary<string, Transform> NullDic { get; private set; }

	public Dictionary<string, Game.Expression> expDic { get; private set; }

	public Dictionary<string, string[]> motionDic { get; private set; }

	public FontColor fontColor { get; private set; }

	public bool GetV3Dic(string arg, out Vector3 pos)
	{
		pos = Vector3.zero;
		if (!arg.IsNullOrEmpty() && !float.TryParse(arg, out var _))
		{
			return V3Dic.TryGetValue(arg, out pos);
		}
		return false;
	}

	public CharaData GetChara(int no)
	{
		if (no < 0)
		{
			SaveData.CharaData charaData = null;
			charaData = ((no != -1) ? ((SaveData.CharaData)scenario.heroineList[Mathf.Abs(no + 2)]) : ((SaveData.CharaData)Game.Player));
			if (charaData != null)
			{
				foreach (KeyValuePair<int, CharaData> character in Characters)
				{
					if (character.Value.data == charaData)
					{
						return character.Value;
					}
				}
				return new CharaData(charaData, scenario, null);
			}
		}
		if (!Characters.TryGetValue(no, out var value))
		{
			return scenario.currentChara;
		}
		return value;
	}

	public void AddChara(int no, SaveData.CharaData charaData, ChaControl chaCtrl, CharaData.MotionReserver motionReserver = null, bool hiPoly = true)
	{
		RemoveChara(no);
		Characters[no] = new CharaData(charaData, chaCtrl, scenario, motionReserver, hiPoly);
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
		scenario = GetComponent<TextScenario>();
		nowCommandList = new CommandList(scenario);
		backGroundCommandList = new CommandList(scenario);
		loadingCharaList = new List<CharaData>();
		Objects = new Dictionary<string, GameObject>();
		Characters = new Dictionary<int, CharaData>();
		Characters2D = new Dictionary<int, CharaData2D>();
		V3Dic = new Dictionary<string, Vector3>();
		NullDic = new Dictionary<string, Transform>();
		expDic = new Dictionary<string, Game.Expression>();
		motionDic = new Dictionary<string, string[]>();
		fontColor = new FontColor();
		if (cameraRoot != null)
		{
			cameraRoot.Set(cameraPosition);
		}
		if (charaRoot != null)
		{
			charaRoot.Set(character);
		}
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

	public void ReleaseChara2D()
	{
		foreach (CharaData2D value in Characters2D.Values)
		{
			value.Release();
		}
		Characters2D.Clear();
	}

	public void ReleaseEventCG()
	{
		if (!(eventCGRoot == null))
		{
			for (int i = 0; i < eventCGRoot.childCount; i++)
			{
				UnityEngine.Object.Destroy(eventCGRoot.GetChild(i).gameObject);
			}
		}
	}

	public void Release()
	{
		ReleaseObject();
		ReleaseNull();
		ReleaseChara();
		ReleaseChara2D();
		ReleaseEventCG();
		_useCorrectCamera = useCorrectCameraBakup;
	}

	private IEnumerator RestoreCameraPosition(Vector3 pos, Quaternion rot)
	{
		correctCameraReset = delegate
		{
			cameraPosition.SetPositionAndRotation(pos, rot);
		};
		yield return new WaitForEndOfFrame();
		if (correctCameraReset != null)
		{
			correctCameraReset();
			correctCameraReset = null;
		}
	}

	private void OnDestroy()
	{
		Release();
	}

	private void Awake()
	{
		if (cameraPosition != null)
		{
			cameraRoot = new BackupPosRot(cameraPosition);
		}
		if (character != null)
		{
			charaStartIndex = character.childCount;
			charaRoot = new BackupPosRot(character);
		}
	}

	private void Update()
	{
		if (_useCorrectCamera && cameraPosition != null && correctCamera.Calculate(Characters.Values, out var pos, out var ang))
		{
			StartCoroutine(RestoreCameraPosition(cameraPosition.position, cameraPosition.rotation));
			cameraPosition.SetPositionAndRotation(cameraPosition.position + pos, cameraPosition.rotation * Quaternion.Euler(ang));
		}
		foreach (CharaData value in Characters.Values)
		{
			value.Update();
		}
	}
}
