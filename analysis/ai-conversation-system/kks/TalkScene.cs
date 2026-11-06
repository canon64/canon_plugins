using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ADV;
using ActionGame;
using ActionGame.Communication;
using Communication.UI;
using Cysharp.Threading.Tasks;
using Illusion.Component.UI;
using Illusion.Extensions;
using Illusion.Game;
using Illusion.Game.Elements.EasyLoader;
using IllusionUtility.GetUtility;
using Manager;
using SaveData;
using SuperScrollView;
using TMPro;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TalkScene : SingletonInitializer<TalkScene>
{
	[Serializable]
	public class ButtonInfo
	{
		public Button button;

		public Sprite[] sprites;

		public GameObject root;

		private Image _image;

		private RectTransform _rectTransform;

		private GameObject _parent;

		private Image Image => _image ?? (_image = button.image);

		private RectTransform RectTransform => _rectTransform ?? (_rectTransform = button.transform as RectTransform);

		private GameObject Parent => _parent ?? (_parent = button.transform.parent.gameObject);

		public bool Active
		{
			set
			{
				button.gameObject.SetActiveIfDifferent(value);
			}
		}

		public bool ActiveParent
		{
			set
			{
				Parent.SetActiveIfDifferent(value);
			}
		}

		public bool Select
		{
			set
			{
				Image.sprite = (value ? sprites[1] : sprites[0]);
				RectTransform.sizeDelta = (value ? new Vector2(96f, 96f) : new Vector2(88f, 88f));
				if (root != null)
				{
					root.SetActiveIfDifferent(value);
				}
			}
		}

		public bool VisibleRoot
		{
			set
			{
				if (root != null)
				{
					root.SetActiveIfDifferent(value);
				}
			}
		}

		public bool interactable
		{
			set
			{
				button.interactable = value;
			}
		}
	}

	[Serializable]
	public class TopicSprite
	{
		public Sprite[] sprites;
	}

	[Serializable]
	private class BackGround
	{
		[SerializeField]
		private Image[] images;

		public bool visible
		{
			set
			{
				Image[] array = images;
				for (int i = 0; i < array.Length; i++)
				{
					array[i].enabled = value;
				}
			}
		}
	}

	[Serializable]
	private class CanvasRaycast
	{
		[SerializeField]
		private GraphicRaycaster[] raycasters;

		public bool enabled
		{
			set
			{
				GraphicRaycaster[] array = raycasters;
				for (int i = 0; i < array.Length; i++)
				{
					array[i].enabled = value;
				}
			}
		}
	}

	private class DroppedTopic
	{
		public int topic { get; private set; } = -1;


		public int probability { get; private set; } = 10;


		public int command { get; private set; }

		public DroppedTopic(int _topic, int _probability, int _command)
		{
			topic = _topic;
			probability = _probability;
			command = _command;
		}
	}

	private struct ListenTopic
	{
		public int Topic { get; set; }

		public ActionGame.Communication.Info.TopicListenInfo Info { get; set; }

		public int Category => SingletonInitializer<ActionScene>.instance.topicDic[Topic].Category;
	}

	private class GaugeInfo
	{
		public Image image { get; protected set; }

		public int min { get; protected set; }

		public int max { get; protected set; }

		public int now { get; protected set; }

		public int target { get; set; }

		public bool isUpdate => now != target;

		public float inverseLerp => Mathf.InverseLerp(min, max, now);

		public IDisposable changed { get; set; }

		public GaugeInfo(Image _image, int _min, int _max, int _now)
		{
			image = _image;
			min = _min;
			max = _max;
			now = _now;
			target = _now;
			image.fillAmount = inverseLerp;
		}

		public virtual void Update()
		{
			int num = Mathf.Clamp(Mathf.Abs(target - now) / 10, 1, 5);
			now = Mathf.Clamp((target > now) ? (now + num) : (now - num), min, max);
			image.fillAmount = inverseLerp;
		}

		public void Dispose()
		{
			changed?.Dispose();
		}
	}

	private class FavorGaugeInfo : GaugeInfo
	{
		public Image[] imageArray { get; protected set; }

		public int stage { get; protected set; }

		public FavorGaugeInfo(Image[] _imageArray, int _stage, int _min, int _max, int _now)
			: base(_imageArray[_stage], _min, _max, _now)
		{
			imageArray = _imageArray;
			SetStage(_stage);
		}

		public override void Update()
		{
			base.Update();
		}

		public void SetStage(int _stage, int _now = -1)
		{
			base.image = imageArray[_stage];
			stage = _stage;
			switch (stage)
			{
			case 1:
				imageArray[0].fillAmount = 1f;
				imageArray[2].fillAmount = 0f;
				break;
			case 2:
				imageArray[0].fillAmount = 0f;
				imageArray[1].fillAmount = 1f;
				break;
			default:
				imageArray[1].fillAmount = 0f;
				imageArray[2].fillAmount = 0f;
				break;
			}
			if (_now >= 0)
			{
				int num2 = (base.target = _now);
				base.now = num2;
			}
			Update();
		}

		public void SetMax(int _value)
		{
			base.max = _value;
		}
	}

	private class TouchPointInfo
	{
		public GameObject obj { get; }

		public Collider col { get; }

		private bool enableCol { get; }

		public string tag { get; }

		public int layer { get; }

		public TouchPointInfo(GameObject _obj, Collider _col, string _tag, int _layer)
		{
			obj = _obj;
			col = _col;
			enableCol = col.enabled;
			col.enabled = true;
			tag = obj.tag;
			obj.tag = _tag;
			layer = obj.layer;
			obj.layer = _layer;
		}

		private void Reset()
		{
			col.enabled = enableCol;
			obj.tag = tag;
			obj.layer = layer;
		}
	}

	private delegate UniTask touchFunc(string _kind, Vector3 _pos);

	private delegate void enterExitFunc(string _kind);

	private class ColDisposableInfo
	{
		private string _name;

		public Collider col { get; }

		private touchFunc touchFunc { get; }

		private enterExitFunc enterFunc { get; }

		private enterExitFunc exitFunc { get; }

		private IDisposable disposableTouch { get; set; }

		private IDisposable disposableEnter { get; set; }

		private IDisposable disposableExit { get; set; }

		private string name
		{
			get
			{
				if (_name == null)
				{
					return _name = col.tag.Replace("Com/Hit/", string.Empty);
				}
				return _name;
			}
		}

		private int mode { get; set; }

		private TalkScene talkScene { get; }

		public ColDisposableInfo(Collider _col, touchFunc _touchFunc, enterExitFunc _enterFunc, enterExitFunc _exitFunc, int _mode, TalkScene _talk)
		{
			col = _col;
			touchFunc = _touchFunc;
			enterFunc = _enterFunc;
			exitFunc = _exitFunc;
			mode = _mode;
			talkScene = _talk;
		}

		public void Start()
		{
			End();
			disposableTouch = (from _ in col.OnMouseDownAsObservable()
				where !EventSystem.current.IsPointerOverGameObject()
				where ((1 << talkScene.touchMode) & mode) != 0
				select _).Subscribe(delegate
			{
				RaycastHit hitInfo;
				bool flag = col.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hitInfo, 10f);
				touchFunc(name, flag ? hitInfo.point : Vector3.zero).Forget();
			}).AddTo(col);
			disposableEnter = (from _ in col.OnMouseEnterAsObservable()
				where !EventSystem.current.IsPointerOverGameObject()
				where ((1 << talkScene.touchMode) & mode) != 0
				select _).Subscribe(delegate
			{
				enterFunc(name);
			}).AddTo(col);
			disposableExit = (from _ in col.OnMouseExitAsObservable()
				where !EventSystem.current.IsPointerOverGameObject()
				where ((1 << talkScene.touchMode) & mode) != 0
				select _).Subscribe(delegate
			{
				exitFunc(name);
			}).AddTo(col);
		}

		public void End()
		{
			disposableTouch?.Dispose();
			disposableTouch = null;
			disposableEnter?.Dispose();
			disposableEnter = null;
			disposableExit?.Dispose();
			disposableExit = null;
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

		public bool Calculate(float _shape, out Vector3 pos, out Vector3 ang)
		{
			pos = MotionIK.GetShapeLerpPositionValue(_shape, this.pos.min, this.pos.max);
			ang = MotionIK.GetShapeLerpAngleValue(_shape, this.ang.min, this.ang.max);
			return true;
		}
	}

	public class ADVParam : SceneParameter
	{
		public bool isInVisibleChara = true;

		public ADVParam(MonoBehaviour scene)
			: base(scene)
		{
		}

		public override void Init(Data data)
		{
			ADVScene aDVScene = SceneParameter.advScene;
			TextScenario scenario = aDVScene.Scenario;
			scenario.BackCamera.fieldOfView = Camera.main.fieldOfView;
			scenario.LoadBundleName = data.bundleName;
			scenario.LoadAssetName = data.assetName;
			aDVScene.Stand.SetPositionAndRotation(data.position, data.rotation);
			scenario.heroineList = data.heroineList;
			scenario.transferList = data.transferList;
			if (!data.heroineList.IsNullOrEmpty())
			{
				for (int i = 0; i < data.heroineList.Count; i++)
				{
					Heroine heroine = data.heroineList[i];
					ADV.CharaData.MotionReserver motionReserver = data.motionReserverList.SafeGet(i);
					scenario.commandController.AddChara(i, new ADV.CharaData(heroine, heroine.chaCtrl, scenario, motionReserver));
				}
				scenario.ChangeCurrentChara(0);
			}
			aDVScene.Map = (SingletonInitializer<ActionScene>.initialized ? SingletonInitializer<ActionScene>.instance.Map : null);
			if (data.camera != null)
			{
				scenario.BackCamera.transform.SetPositionAndRotation(data.camera.position, data.camera.rotation);
			}
			TalkScene talkScene = base.mono as TalkScene;
			foreach (KeyValuePair<string, Func<int>> aDVVariable in talkScene.ADVVariables)
			{
				scenario.Vars[aDVVariable.Key] = new ValData(ValData.Convert(aDVVariable.Value(), typeof(int)));
			}
			foreach (KeyValuePair<string, Func<string>> aDVVariableStr in talkScene.ADVVariableStrs)
			{
				scenario.Vars[aDVVariableStr.Key] = new ValData(ValData.Convert(aDVVariableStr.Value(), typeof(string)));
			}
			float fadeInTime = data.fadeInTime;
			if (fadeInTime > 0f)
			{
				aDVScene.fadeTime = fadeInTime;
			}
			else
			{
				isInVisibleChara = false;
			}
		}

		public override void Release()
		{
			ADVScene aDVScene = SceneParameter.advScene;
			aDVScene.gameObject.SetActive(value: false);
			if (base.mono is TalkScene talkScene)
			{
				int select = (talkScene.selectNo = (int)aDVScene.Scenario.Vars[KEY_NAME].o);
				if (talkScene.m_CVInfo is ChangeValueSelectInfo changeValueSelectInfo)
				{
					changeValueSelectInfo.select = select;
				}
			}
		}

		public override void WaitEndProc()
		{
		}
	}

	[Header("各種ボタン関係")]
	[SerializeField]
	private Canvas canvas;

	[SerializeField]
	private CanvasRaycast raycast = new CanvasRaycast();

	[SerializeField]
	private ButtonInfo[] buttonInfos;

	[SerializeField]
	private Button buttonEnd;

	[Header("女の子の状態UI")]
	[SerializeField]
	private Canvas canvasFemale;

	[SerializeField]
	private Image imageTime;

	[SerializeField]
	private Image[] imageFavor;

	[SerializeField]
	private Sprite[] spriteFavor;

	[SerializeField]
	private Image imageLewdness;

	[SerializeField]
	private Sprite[] spriteLewdness;

	[SerializeField]
	private GameObject objLewdness;

	[SerializeField]
	private Image imageFrame;

	[SerializeField]
	private Sprite[] spriteFrame;

	[Header("話をする関係")]
	[SerializeField]
	private Button[] buttonTalkContents;

	[Header("話題関係")]
	[SerializeField]
	private TextMeshProUGUI textTopicTitle;

	[SerializeField]
	private LoopListView2 loopListView2;

	[SerializeField]
	private TopicSprite[] topicSprites;

	[SerializeField]
	private TopicSprite[] topicImages;

	[SerializeField]
	private GameObject objBlocker;

	[SerializeField]
	private TopicSprite topicNoneSprite;

	[SerializeField]
	private TopicSprite topicNoneImage;

	[Header("一緒に行動関係")]
	[SerializeField]
	private Button[] buttonEventContents;

	[Header("R18関係")]
	[SerializeField]
	private Button[] buttonR18Contents;

	[Header("背景用")]
	[SerializeField]
	private Camera cameraMap;

	[SerializeField]
	private RawImage rawImageMap;

	[SerializeField]
	private Illusion.Component.UI.BackGroundParam backGroundParam;

	[SerializeField]
	private BackGround backGround = new BackGround();

	[Header("触る・見る関係")]
	[SerializeField]
	private Button[] buttonTouch;

	[SerializeField]
	private Sprite[] spriteTouch;

	[SerializeField]
	private Texture2D[] texCursor;

	[SerializeField]
	private Canvas canvasBack;

	[SerializeField]
	private RawImage rawFace;

	[SerializeField]
	private Image imageState;

	[SerializeField]
	private Sprite[] spriteState;

	[SerializeField]
	private CharaCorrectHeightCamera correctCamera = new CharaCorrectHeightCamera();

	[SerializeField]
	private Transform[] cameraTransform;

	[SerializeField]
	private float fieldOfView = 23f;

	private bool _isPaly;

	private ChangeValueAbstractInfo m_CVInfo;

	private bool isIntroductionOnly;

	private int heroineEvent = -1;

	private ActionGame.Communication.Info.EndADV endADVNo = ActionGame.Communication.Info.EndADV.General;

	private ListenTopic listenTopic;

	private static readonly int sr_PrayTopicDrop = 1;

	private static readonly int sr_PrayH = 1002;

	private List<int> lottery;

	public Action otherInitialize { get; set; }

	private Heroine targetHeroine { get; set; }

	private int Relation
	{
		get
		{
			if (targetHeroine.fixCharaID >= 0)
			{
				return targetHeroine.relation;
			}
			return targetHeroine.relationNPC;
		}
	}

	private int stage => Mathf.Clamp(Relation, 0, 2);

	private NecessaryInfo necessaryInfo { get; set; }

	public ResultInfo resultInfo { get; private set; }

	public IKMotion resultIK => ikmotion;

	private IKMotion ikmotion { get; set; }

	private Illusion.Game.Elements.EasyLoader.Motion motion { get; } = new Illusion.Game.Elements.EasyLoader.Motion();


	private List<DroppedTopic> droppedTopics { get; set; }

	private Dictionary<int, int> commandCount { get; set; }

	private DroppedTopic droppedTopic { get; set; }

	private ActionGame.Communication.Info info { get; set; }

	public static bool isPaly
	{
		get
		{
			if (!SingletonInitializer<TalkScene>.initialized)
			{
				return false;
			}
			return SingletonInitializer<TalkScene>.instance._isPaly;
		}
	}

	public static string KEY_NAME { get; } = "Result";


	private Dictionary<string, Func<int>> ADVVariables { get; set; }

	private Dictionary<string, Func<string>> ADVVariableStrs { get; set; }

	private ChangeValueAbstractInfo cvInfo => m_CVInfo;

	private int selectNo { get; set; } = -1;


	private int baseMask { get; set; }

	private Camera nowCamera { get; set; }

	private Transform transVoice { get; set; }

	private CancellationTokenSource leaveAloneCancellation { get; set; }

	private List<GameObject> lstAddCol { get; set; }

	private List<ColDisposableInfo> lstColDisposable { get; set; }

	private int m_touchMode { get; set; }

	public int touchMode
	{
		get
		{
			return m_touchMode;
		}
		private set
		{
			m_touchMode = value;
			for (int i = 0; i < buttonTouch.Length; i++)
			{
				buttonTouch[i].image.sprite = spriteTouch[((i == value) ? 1 : 0) + i * 2];
			}
		}
	}

	private Dictionary<int, int> dicTouchCount { get; set; } = new Dictionary<int, int>();


	private CancellationTokenSource touchCancellation { get; set; }

	private CrossFade crossFade { get; set; }

	private float backupFOV { get; set; } = 45f;


	private CameraClearFlags backupClearFlags { get; set; }

	private bool isUpdateCamera { get; set; } = true;


	private bool isSuccess { get; set; }

	private float koikatsuPoint { get; set; }

	private RenderTexture renderTextureMap { get; set; }

	private CameraEffectorConfig cameraEffectorConfig { get; set; }

	private GaugeInfo timeGaugeInfo { get; set; }

	private FavorGaugeInfo favorGaugeInfo { get; set; }

	private GaugeInfo lewdnessGaugeInfo { get; set; }

	private List<Topic.Param> topics { get; set; }

	private int selectTopic { get; set; } = -1;


	private CancellationTokenSource topicCancellation { get; set; }

	private bool isDesire { get; set; }

	private int selectButton { get; set; } = -1;


	private int selectCommand { get; set; } = -1;


	private bool isNPC { get; set; }

	private CancellationTokenSource cancellation { get; set; }

	private IDisposable disposableOverlapsEvent { get; set; }

	public bool isActive => cancellation != null;

	private void LeaveAloneCancel()
	{
		leaveAloneCancellation?.Cancel();
		leaveAloneCancellation?.Dispose();
		leaveAloneCancellation = null;
	}

	private void TouchCancel()
	{
		touchCancellation?.Cancel();
		touchCancellation?.Dispose();
		touchCancellation = null;
	}

	private void TopicCancel()
	{
		topicCancellation?.Cancel();
		topicCancellation?.Dispose();
		topicCancellation = null;
	}

	public void Cancel()
	{
		cancellation?.Cancel();
		cancellation?.Dispose();
		cancellation = null;
		disposableOverlapsEvent?.Dispose();
		disposableOverlapsEvent = null;
	}

	private static async UniTask Fade(FadeCanvas.Fade fade)
	{
		await Scene.sceneFadeCanvas.StartFadeAysnc(fade);
	}

	protected override void Initialize()
	{
		canvas.enabled = false;
		raycast.enabled = false;
		canvasBack.enabled = false;
		cameraMap.enabled = false;
		backGroundParam.visible = false;
		canvasFemale.enabled = false;
		droppedTopics = new List<DroppedTopic>();
		commandCount = new Dictionary<int, int>();
		lottery = new List<int>();
		this.OnDestroyAsObservable().Subscribe(delegate
		{
			Cancel();
		});
	}

	public async UniTask Setup(Heroine heroine, NecessaryInfo necessaryInfo)
	{
		targetHeroine = heroine;
		this.necessaryInfo = necessaryInfo;
		resultInfo = new ResultInfo();
		canvas.enabled = false;
		raycast.enabled = false;
		canvasBack.enabled = false;
		cameraMap.enabled = false;
		backGroundParam.visible = false;
		canvasFemale.enabled = false;
		isUpdateCamera = true;
		isIntroductionOnly = false;
		backGround.visible = true;
		_isPaly = true;
		selectTopic = -1;
		selectButton = -1;
		selectCommand = -1;
		endADVNo = ActionGame.Communication.Info.EndADV.General;
		droppedTopics.Clear();
		commandCount.Clear();
		OnClickMain(-1);
		isNPC = heroine.fixCharaID < 0;
		koikatsuPoint = 0f;
		ikmotion = new IKMotion();
		disposableOverlapsEvent = Scene.OverlapsEvent.ObserveAdd().Subscribe(delegate(CollectionAddEvent<Scene.IOverlap> xData)
		{
			if (xData.Value is Tutorial || xData.Value is ConfigScene || xData.Value is ShortcutScene)
			{
				Cursor.SetCursor(null, Vector2.zero, CursorMode.ForceSoftware);
			}
		});
		ADVVariables = new Dictionary<string, Func<int>> { 
		{
			KEY_NAME,
			() => int.MaxValue
		} };
		ADVVariableStrs = new Dictionary<string, Func<string>>
		{
			{
				"NowMap",
				() => SingletonInitializer<ActionScene>.initialized ? SingletonInitializer<ActionScene>.instance.Map.no.ToString() : string.Empty
			},
			{
				"NowMapName",
				() => SingletonInitializer<ActionScene>.initialized ? ((SingletonInitializer<ActionScene>.instance.Map.no != -1) ? SingletonInitializer<ActionScene>.instance.Map.ConvertMapName(SingletonInitializer<ActionScene>.instance.Map.no) : "なし") : string.Empty
			},
			{
				"NowCycle",
				() => SingletonInitializer<ActionScene>.initialized ? SingletonInitializer<ActionScene>.instance.Cycle.nowType.ToString() : string.Empty
			},
			{
				"NowCycleName",
				() => SingletonInitializer<ActionScene>.initialized ? SingletonInitializer<ActionScene>.instance.Cycle.nowType.GetName() : string.Empty
			},
			{
				"NowWeek",
				() => SingletonInitializer<ActionScene>.initialized ? SingletonInitializer<ActionScene>.instance.Cycle.nowWeek.ToString() : string.Empty
			},
			{
				"NowWeekName",
				() => SingletonInitializer<ActionScene>.initialized ? SingletonInitializer<ActionScene>.instance.Cycle.nowWeek.GetName() : string.Empty
			}
		};
		ParameterList.Add(new ADVParam(this));
		cancellation = new CancellationTokenSource();
		CancellationToken cancelToken = cancellation.Token;
		cancelToken.Register(delegate
		{
			canvas.enabled = false;
			raycast.enabled = false;
			canvasBack.enabled = false;
			cameraMap.enabled = false;
			backGroundParam.visible = false;
			canvasFemale.enabled = false;
			_isPaly = false;
			disposableOverlapsEvent?.Dispose();
			disposableOverlapsEvent = null;
			ParameterList.Remove(this);
			if (!isIntroductionOnly)
			{
				if (nowCamera != null)
				{
					nowCamera.cullingMask = baseMask;
					nowCamera.fieldOfView = backupFOV;
					nowCamera.clearFlags = backupClearFlags;
				}
				if (renderTextureMap != null)
				{
					cameraMap.targetTexture = null;
					rawImageMap.texture = null;
					UnityEngine.Object.DestroyImmediate(renderTextureMap);
				}
				cameraEffectorConfig.SetFog(enable: true);
				cameraEffectorConfig.SetDOF(enable: true);
				Cursor.SetCursor(null, Vector2.zero, CursorMode.ForceSoftware);
				LeaveAloneCancel();
				TouchCancel();
			}
		});
		if (Tutorial.Open(Tutorial.Category.Communication))
		{
			await UniTask.WaitWhile(() => Tutorial.active, PlayerLoopTiming.Update, cancelToken);
		}
		await UniTask.WaitWhile(() => Scene.IsOverlap, PlayerLoopTiming.Update, cancelToken);
		if (heroine.fixCharaID == -13 && Tutorial.Open(Tutorial.Category.Guide))
		{
			await UniTask.WaitWhile(() => Tutorial.active, PlayerLoopTiming.Update, cancelToken);
		}
		if (necessaryInfo.state == 0 && Game.Player.charaBase != null && heroine.charaBase != null)
		{
			Vector3 position = Game.Player.charaBase.position;
			Vector3 position2 = heroine.charaBase.position;
			position.y = position2.y;
			heroine.charaBase.rotation = Quaternion.LookRotation(position - position2, Vector3.up);
		}
		info = new ActionGame.Communication.Info();
		info.Init(new PassingInfo
		{
			player = Game.Player,
			heroine = heroine,
			info = necessaryInfo,
			map = (SingletonInitializer<ActionScene>.initialized ? SingletonInitializer<ActionScene>.instance.Map : GetComponent<BaseMap>())
		});
		if (Camera.main != null)
		{
			nowCamera = Camera.main;
		}
		else
		{
			GameObject asset = AssetBundleManager.LoadAsset("camera/action.unity3d", "ActionCamera", typeof(GameObject)).GetAsset<GameObject>();
			nowCamera = asset.GetComponent<Camera>();
			AssetBundleManager.UnloadAssetBundle("camera/action.unity3d", isUnloadForceRefCount: false);
		}
		renderTextureMap = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
		renderTextureMap.antiAliasing = 8;
		cameraMap.targetTexture = renderTextureMap;
		cameraMap.clearFlags = nowCamera.clearFlags;
		rawImageMap.texture = renderTextureMap;
		Transform nowCameraTransform = nowCamera.transform;
		Manager.Sound.Listener = nowCameraTransform;
		if (!necessaryInfo.isHAttack)
		{
			int _eventNo = -1;
			List<Program.Transfer> list = ((!isNPC) ? info.GetIntroductionADV(ref isIntroductionOnly, ref _eventNo, out m_CVInfo, _check: true) : info.GetIntroductionADV(ref isIntroductionOnly));
			if (isIntroductionOnly)
			{
				ADVScene s = SingletonInitializer<ActionScene>.instance.AdvScene;
				bool b = s.Scenario.commandController.useCorrectCamera;
				s.Scenario.commandController.useCorrectCamera = false;
				await StartADV(list, cancelToken);
				ReflectChangeValue();
				await TalkEnd(cancelToken);
				s.Scenario.commandController.useCorrectCamera = b;
				return;
			}
		}
		Scene.sceneFadeCanvas.SetColor(Color.black);
		await Fade(FadeCanvas.Fade.In);
		if (SingletonInitializer<ActionScene>.instance != null)
		{
			SingletonInitializer<ActionScene>.instance.Player?.ChaserEnd();
		}
		backupClearFlags = nowCamera.clearFlags;
		nowCamera.clearFlags = CameraClearFlags.Depth;
		canvasFemale.enabled = true;
		Texture2D texture2D = new Texture2D(240, 320);
		texture2D.LoadImage(heroine.charFile.facePngData);
		rawFace.texture = texture2D;
		rawFace.enabled = true;
		UpdateUI(_gauge: true);
		otherInitialize();
		Transform root = heroine.transform;
		GameObject parent = ((!SingletonInitializer<ActionScene>.initialized) ? base.gameObject : SingletonInitializer<ActionScene>.instance.gameObject);
		ChaFileControl chaFileControl = new ChaFileControl();
		ChaFile.CopyChaFile(chaFileControl, (heroine.chaCtrl == null) ? heroine.charFile : heroine.chaCtrl.chaFile);
		ChaControl female = Character.CreateFemale(parent, 0, chaFileControl);
		heroine.SetRoot(female.gameObject);
		Character.loading.Load(0, female, reflectStatus: true);
		await UniTask.WaitUntil(() => Character.loading.IsEnd(female), PlayerLoopTiming.Update, cancelToken);
		if (root != null)
		{
			female.transform.SetPositionAndRotation(root.position, root.rotation);
		}
		await UniTask.WaitWhile(() => !Singleton<GameAssist>.Instance.isInit, PlayerLoopTiming.Update, cancelToken);
		TopicDropData.Param param = Singleton<GameAssist>.Instance.DropTopic(heroine.charaBase.motion.state);
		if (param != null && param.probability > 0)
		{
			if (heroine.topicDrop == null)
			{
				heroine.topicDrop = new HashSet<int>();
			}
			if (!heroine.topicDrop.Contains(param.no))
			{
				droppedTopics.Add(new DroppedTopic(param.no, param.probability, -1));
			}
		}
		heroine.chaCtrl.animBody.runtimeAnimatorController = SingletonInitializer<Game>.instance.advAnimePack.ac;
		AnimePlay("通常立ち");
		Singleton<GameAssist>.Instance.ChangeExpression(heroine.FixCharaIDOrPersonality, "笑顔", heroine.chaCtrl);
		heroine.chaCtrl.ChangeLookNeckPtn(3, 0f);
		InitTouch();
		canvasBack.enabled = true;
		cameraMap.enabled = true;
		backGroundParam.visible = true;
		baseMask = nowCamera.cullingMask;
		nowCamera.cullingMask = 1 << LayerMask.NameToLayer("Chara");
		(from _ in this.UpdateAsObservable()
			where isUpdateCamera
			select _).Subscribe(delegate
		{
			cameraTransform[1].SetPositionAndRotation(heroine.transform.position, heroine.transform.rotation);
			correctCamera.Calculate(heroine.chaCtrl.GetShapeBodyValue(0), out var pos, out var ang);
			cameraTransform[0].position = cameraTransform[0].position + pos;
			cameraTransform[0].eulerAngles = cameraTransform[0].eulerAngles + ang;
			nowCameraTransform.SetPositionAndRotation(cameraTransform[2].position, cameraTransform[2].rotation);
			cameraTransform[0].localPosition = Vector3.zero;
			cameraTransform[0].localEulerAngles = Vector3.zero;
			cameraMap.transform.SetPositionAndRotation(nowCameraTransform.position, nowCameraTransform.rotation);
		}).AddTo(cancelToken);
		backupFOV = nowCamera.fieldOfView;
		nowCamera.fieldOfView = fieldOfView;
		cameraEffectorConfig = SingletonInitializer<Game>.instance.cameraEffector.config;
		cameraEffectorConfig.SetFog(enable: false);
		cameraEffectorConfig.SetDOF(enable: false);
		heroine.chaCtrl.ChangeLookEyesTarget(0, nowCameraTransform);
		heroine.chaCtrl.ChangeLookNeckTarget(0, nowCameraTransform);
		crossFade = nowCamera.GetComponent<CrossFade>();
		for (int i = 0; i < buttonInfos.Length; i++)
		{
			int id2 = i;
			switch (i)
			{
			case 1:
				buttonInfos[1].button.OnClickAsObservable().Subscribe(delegate
				{
					OnClickMain(1);
					CommandFunc(3, cancelToken).Forget();
				}).AddTo(cancelToken);
				break;
			case 2:
				buttonInfos[2].button.OnClickAsObservable().Subscribe(delegate
				{
					OnClickMain(2);
					CommandFunc(4, cancelToken).Forget();
				}).AddTo(cancelToken);
				break;
			default:
				buttonInfos[id2].button.OnClickAsObservable().Subscribe(delegate
				{
					OnClickMain(id2);
				}).AddTo(cancelToken);
				break;
			}
		}
		buttonTalkContents[0].OnClickAsObservable().Subscribe(delegate
		{
			koikatsuPoint += 1f;
			CommandFunc(0, cancelToken).Forget();
			Utils.Sound.Play(SystemSE.ok_s);
		}).AddTo(cancelToken);
		buttonTalkContents[1].OnClickAsObservable().Subscribe(delegate
		{
			koikatsuPoint += 1f;
			CommandFunc(1, cancelToken).Forget();
			Utils.Sound.Play(SystemSE.ok_s);
		}).AddTo(cancelToken);
		buttonTalkContents[2].OnClickAsObservable().Subscribe(delegate
		{
			koikatsuPoint += 1f;
			CommandFunc(2, cancelToken).Forget();
			Utils.Sound.Play(SystemSE.ok_s);
		}).AddTo(cancelToken);
		if (!Game.isAddH)
		{
			buttonTalkContents[2].gameObject.SetActiveIfDifferent(active: false);
		}
		for (int j = 0; j < buttonEventContents.Length; j++)
		{
			int id = j + 5;
			buttonEventContents[j].OnClickAsObservable().Subscribe(delegate
			{
				OnClickMain(-1);
				CommandFunc(id, cancelToken).Forget();
				Utils.Sound.Play(SystemSE.ok_s);
			}).AddTo(cancelToken);
		}
		buttonR18Contents[0].OnClickAsObservable().Subscribe(delegate
		{
			OnClickMain(-1);
			CommandFunc(20, cancelToken).Forget();
			Utils.Sound.Play(SystemSE.ok_s);
		}).AddTo(cancelToken);
		buttonR18Contents[1].OnClickAsObservable().Subscribe(delegate
		{
			OnClickMain(-1);
			CommandFunc(21, cancelToken).Forget();
			Utils.Sound.Play(SystemSE.ok_s);
		}).AddTo(cancelToken);
		buttonEnd.OnClickAsObservable().Subscribe(delegate
		{
			CommandFunc(-1, cancelToken).Forget();
			Utils.Sound.Play(SystemSE.ok_s);
		}).AddTo(cancelToken);
		touchMode = 0;
		buttonTouch[0].OnClickAsObservable().Subscribe(delegate
		{
			touchMode = 0;
			Utils.Sound.Play(SystemSE.sel);
		}).AddTo(cancelToken);
		buttonTouch[1].OnClickAsObservable().Subscribe(delegate
		{
			touchMode = 1;
			Utils.Sound.Play(SystemSE.sel);
		}).AddTo(cancelToken);
		timeGaugeInfo = new GaugeInfo(imageTime, 0, heroine.talkTimeMax, heroine.talkTime);
		(from _ in Observable.Interval(TimeSpan.FromMilliseconds(25.0))
			where timeGaugeInfo.isUpdate
			select _).Subscribe(delegate
		{
			timeGaugeInfo.Update();
		}).AddTo(cancelToken);
		timeGaugeInfo.changed = this.ObserveEveryValueChanged((TalkScene _) => heroine.talkTime).Subscribe(delegate(int time)
		{
			timeGaugeInfo.target = time;
		}).AddTo(cancelToken);
		bool flag = isNPC && Relation == 3;
		favorGaugeInfo = new FavorGaugeInfo(imageFavor, stage, 0, SingletonInitializer<ActionScene>.instance.stepUpDic[stage], flag ? SingletonInitializer<ActionScene>.instance.stepUpDic[stage] : heroine.favor);
		(from _ in Observable.Interval(TimeSpan.FromMilliseconds(50.0))
			where favorGaugeInfo.isUpdate
			select _).Subscribe(delegate
		{
			favorGaugeInfo.Update();
			imageFavor[2].sprite = spriteFavor[(!isNPC) ? ((Relation == 3) ? 1 : 0) : ((Relation == 2) ? 2 : 0)];
		}).AddTo(cancelToken);
		if (!flag)
		{
			favorGaugeInfo.changed = this.ObserveEveryValueChanged((TalkScene _) => heroine.favor).Subscribe(delegate(int x)
			{
				favorGaugeInfo.target = x;
			}).AddTo(this);
		}
		lewdnessGaugeInfo = new GaugeInfo(imageLewdness, 0, 100, heroine.lewdness);
		(from _ in Observable.Interval(TimeSpan.FromMilliseconds(50.0))
			where lewdnessGaugeInfo.isUpdate
			select _).Subscribe(delegate
		{
			lewdnessGaugeInfo.Update();
			imageLewdness.sprite = spriteLewdness[(!(lewdnessGaugeInfo.inverseLerp < 1f)) ? 1u : 0u];
		}).AddTo(cancelToken);
		lewdnessGaugeInfo.changed = this.ObserveEveryValueChanged((TalkScene _) => heroine.lewdness).Subscribe(delegate(int x)
		{
			lewdnessGaugeInfo.target = x;
		}).AddTo(cancelToken);
		if (isNPC)
		{
			switch (heroine.relationNPC)
			{
			case 0:
				Utils.Sound.Play(new Utils.Sound.SettingBGM(BGM.Encounter));
				break;
			case 1:
			case 2:
				Utils.Sound.Play(new Utils.Sound.SettingBGM(BGM.Communication));
				break;
			default:
				Utils.Sound.Play(new Utils.Sound.SettingBGM(BGM.Lover));
				break;
			}
		}
		else
		{
			switch (heroine.relation)
			{
			case 0:
				Utils.Sound.Play(new Utils.Sound.SettingBGM(BGM.Encounter));
				break;
			case 1:
				Utils.Sound.Play(new Utils.Sound.SettingBGM(BGM.Communication));
				break;
			default:
				Utils.Sound.Play(new Utils.Sound.SettingBGM(BGM.Lover));
				break;
			}
		}
		await UniTask.Yield(PlayerLoopTiming.Update, cancelToken);
		await Fade(FadeCanvas.Fade.Out);
		await Introduction(cancelToken);
	}

	private void OnClickMain(int _idx)
	{
		TopicCancel();
		if (selectButton == _idx)
		{
			_idx = -1;
		}
		for (int i = 0; i < buttonInfos.Length; i++)
		{
			if (i != _idx)
			{
				buttonInfos[i].Select = false;
			}
		}
		buttonInfos.SafeProc(_idx, delegate(ButtonInfo v)
		{
			v.Select = true;
		});
		selectButton = _idx;
		if (_idx != -1)
		{
			Utils.Sound.Play(SystemSE.sel);
		}
	}

	private void InitTouch()
	{
		lstColDisposable = new List<ColDisposableInfo>();
		SetCharaTagLayer();
		string[,] array = new string[3, 3]
		{
			{ "cf_j_hand_L", "communication/hit_00.unity3d", "com_hit_hand_L" },
			{ "cf_j_hand_R", "communication/hit_00.unity3d", "com_hit_hand_R" },
			{ "cf_j_head", "communication/hit_00.unity3d", "com_hit_head" }
		};
		int[] array2 = new int[3] { 1, 1, 3 };
		Transform transform = targetHeroine.chaCtrl.objBodyBone.transform;
		lstAddCol = new List<GameObject>();
		for (int i = 0; i < array.GetLength(0); i++)
		{
			GameObject gameObject = transform.FindLoop(array[i, 0]);
			if (gameObject == null)
			{
				continue;
			}
			GameObject gameObject2 = CommonLib.LoadAsset<GameObject>(array[i, 1], array[i, 2], clone: true);
			if (!(gameObject2 == null))
			{
				gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
				lstAddCol.Add(gameObject2);
				Collider[] componentsInChildren = gameObject2.GetComponentsInChildren<Collider>();
				for (int j = 0; j < componentsInChildren.Length; j++)
				{
					lstColDisposable.Add(new ColDisposableInfo(componentsInChildren[j], TouchFunc, EnterFunc, ExitFunc, array2[i], this));
				}
			}
		}
		dicTouchCount = new Dictionary<int, int>
		{
			{ 0, 0 },
			{ 1, 0 },
			{ 2, 0 },
			{ 3, 0 },
			{ 10, 0 },
			{ 11, 0 }
		};
	}

	private void SetCharaTagLayer()
	{
		string[,] array = new string[2, 3]
		{
			{ "cf_hit_bust02_L", "Com/Hit/MuneL", "Chara" },
			{ "cf_hit_bust02_R", "Com/Hit/MuneR", "Chara" }
		};
		int[] array2 = new int[2] { 3, 3 };
		Transform transform = targetHeroine.chaCtrl.objBodyBone.transform;
		int length = array.GetLength(0);
		for (int i = 0; i < length; i++)
		{
			int num = 0;
			GameObject gameObject = transform.FindLoop(array[i, num++]);
			if (!(gameObject == null))
			{
				Collider component = gameObject.GetComponent<Collider>();
				if (!(component == null))
				{
					new TouchPointInfo(gameObject, component, array[i, num++], LayerMask.NameToLayer(array[i, num++]));
					lstColDisposable.Add(new ColDisposableInfo(component, TouchFunc, EnterFunc, ExitFunc, array2[i], this));
				}
			}
		}
	}

	private async UniTask Introduction(CancellationToken token)
	{
		Heroine heroine = targetHeroine;
		bool isIntroductionOnly = false;
		if (!necessaryInfo.introSkip)
		{
			if (necessaryInfo.isHAttack)
			{
				await StartADV(info.GetEventFromHeroineADV(3, out m_CVInfo, ref heroineEvent), token);
				await HeroineEventWait(token);
				return;
			}
			int _eventNo = -1;
			await StartADV((!isNPC) ? info.GetIntroductionADV(ref isIntroductionOnly, ref _eventNo, out m_CVInfo) : info.GetIntroductionADV(ref isIntroductionOnly), token);
		}
		isUpdateCamera = false;
		await UniTask.Yield(PlayerLoopTiming.Update, token);
		await Program.ADVProcessingCheck(token);
		if (isIntroductionOnly)
		{
			Cancel();
			return;
		}
		heroine.chaCtrl.ChangeLookNeckPtn(3, 0f);
		StartSelection();
	}

	private void UpdateUI(bool _gauge = false)
	{
		Heroine heroine = targetHeroine;
		int key = Mathf.Clamp((!isNPC) ? heroine.relation : heroine.relationNPC, 0, 2);
		HashSet<int> hashSet = null;
		buttonInfos[0].Active = heroine.fixCharaID >= 0;
		Dictionary<int, Topic.Param> topic = SingletonInitializer<ActionScene>.instance.topicDic;
		Dictionary<int, Player.TopicState> stock = Game.Player.topicStock;
		if (!isNPC)
		{
			buttonInfos[1].interactable = stock.Where((KeyValuePair<int, Player.TopicState> v) => topic[v.Key].Category != 4).Any((KeyValuePair<int, Player.TopicState> v) => v.Value.num > 0);
		}
		else
		{
			buttonInfos[1].interactable = true;
		}
		buttonInfos[2].interactable = stock.Where(delegate(KeyValuePair<int, Player.TopicState> v)
		{
			if (topic[v.Key].Category != 4)
			{
				return true;
			}
			return !isNPC && !heroine.isVirgin;
		}).Any((KeyValuePair<int, Player.TopicState> v) => v.Value.num > 0 && !heroine.topicTalked.Contains(v.Key));
		if (!isNPC)
		{
			hashSet = info.ConfirmEvent(key);
		}
		else
		{
			hashSet = new HashSet<int>();
			if (Relation == 2 && heroine.favor >= SingletonInitializer<ActionScene>.instance.stepUpDic[2])
			{
				HashSet<int> tips = Game.saveData.tips;
				if (tips.Contains(2) && tips.Contains(4) && tips.Contains(5) && tips.Contains(6))
				{
					hashSet.Add(6);
				}
			}
		}
		for (int i = 0; i < buttonEventContents.Length; i++)
		{
			buttonEventContents[i].gameObject.SetActiveIfDifferent(hashSet.Contains(i));
		}
		buttonInfos[3].ActiveParent = !hashSet.IsNullOrEmpty();
		if (!isNPC)
		{
			hashSet = info.ConfirmR18(key);
		}
		else
		{
			hashSet = new HashSet<int>();
			if (Relation == 3)
			{
				hashSet.Add(0);
				hashSet.Add(1);
			}
		}
		for (int j = 0; j < buttonR18Contents.Length; j++)
		{
			buttonR18Contents[j].gameObject.SetActiveIfDifferent(hashSet.Contains(j));
		}
		buttonInfos[4].Active = !hashSet.IsNullOrEmpty() && Game.isAddH;
		topics = (from v in SingletonInitializer<ActionScene>.instance.topicDic.Values
			where stock.TryGetValue(v.No, out var value2) && value2.num > 0 && !heroine.topicTalked.Contains(v.No) && (v.Category != 4 || (!isNPC && !heroine.isVirgin && Game.isAddH))
			orderby v.Rarity
			orderby v.Category
			select v).ToList();
		int count = topics.Count;
		if (!loopListView2.IsInit)
		{
			loopListView2.InitListView(count, (LoopListView2 _view, int _index) => OnUpdate(_view, _index));
		}
		else if (!loopListView2.SetListItemCount(count, resetPos: true, OnUpdate))
		{
			loopListView2.RefreshAllShownItem();
		}
		int num = ((!isNPC) ? heroine.relation : heroine.relationNPC);
		imageState.enabled = true;
		if (isNPC)
		{
			switch (num)
			{
			case 2:
				imageState.sprite = spriteState[4];
				break;
			case 3:
				imageState.sprite = spriteState[2];
				break;
			default:
				imageState.sprite = spriteState[num];
				break;
			}
		}
		else
		{
			imageState.sprite = spriteState[num];
		}
		if (_gauge)
		{
			imageTime.fillAmount = Mathf.InverseLerp(0f, heroine.talkTimeMax, heroine.talkTime);
			imageFavor[0].fillAmount = Mathf.InverseLerp(0f, SingletonInitializer<ActionScene>.instance.stepUpDic[key], heroine.favor);
			imageLewdness.fillAmount = Mathf.InverseLerp(0f, 100f, heroine.lewdness);
			imageLewdness.sprite = spriteLewdness[(!(imageLewdness.fillAmount < 1f)) ? 1u : 0u];
			if (!Game.isAddH)
			{
				objLewdness.SetActiveIfDifferent(active: false);
			}
			else
			{
				objLewdness.SetActiveIfDifferent(!isNPC);
			}
			imageFrame.sprite = spriteFrame[isNPC ? 1 : 0];
		}
		LoopListViewItem2 OnUpdate(LoopListView2 _view, int _index)
		{
			if (_index >= 0)
			{
				LoopListViewItem2 loopListViewItem = _view.NewListViewItem("Node");
				NodeInfo component = loopListViewItem.GetComponent<NodeInfo>();
				component.Text = topics[_index].Name;
				(SingletonInitializer<ActionScene>.instance.Player.charaData as Player).topicStock.TryGetValue(topics[_index].No, out var value);
				int num2 = Mathf.Clamp(value.num, 0, 99);
				component.Num = $"x{num2:00}";
				TopicSprite topicSprite = topicSprites[topics[_index].Rarity];
				component.Button.image.sprite = topicSprite.sprites[0];
				SpriteState spriteState = component.Button.spriteState;
				spriteState.highlightedSprite = topicSprite.sprites[1];
				spriteState.pressedSprite = topicSprite.sprites[1];
				spriteState.selectedSprite = topicSprite.sprites[0];
				component.Button.spriteState = spriteState;
				component.Image = topicImages[topics[_index].Category].sprites[0];
				component.Initialize();
				component.Button.onClick.RemoveAllListeners();
				int id = _index;
				component.Button.onClick.AddListener(delegate
				{
					selectTopic = id;
					Utils.Sound.Play(SystemSE.ok_s);
				});
				return loopListViewItem;
			}
			return null;
		}
	}

	private void StartSelection()
	{
		isUpdateCamera = true;
		UpdateUI();
		canvas.enabled = true;
		raycast.enabled = true;
		SetLeaveAloneTimer().Forget();
		lstColDisposable.ForEach(delegate(ColDisposableInfo x)
		{
			x.Start();
		});
	}

	private async UniTaskVoid SetLeaveAloneTimer()
	{
		LeaveAloneCancel();
		leaveAloneCancellation = new CancellationTokenSource();
		CancellationToken token = leaveAloneCancellation.Token;
		while (!token.IsCancellationRequested)
		{
			if ((await UniTask.WaitUntilValueChanged(base.transform, (Transform _) => Input.mousePosition, PlayerLoopTiming.Update, null, token).TimeoutWithoutException(TimeSpan.FromSeconds(30.0))).Item1)
			{
				token.ThrowIfCancellationRequested();
				if (!canvas.enabled || SingletonInitializer<ActionScene>.instance.AdvScene.isActiveAndEnabled)
				{
					break;
				}
				LeaveAloneReactionInfo leaveAloneVoice = info.GetLeaveAloneVoice();
				LoadVoice(leaveAloneVoice.assetbundle, leaveAloneVoice.file, leaveAloneVoice.facialExpression, leaveAloneVoice.pose);
				isUpdateCamera = false;
				await UniTask.Yield(PlayerLoopTiming.Update, token);
				await UniTask.WaitWhile(() => transVoice != null, PlayerLoopTiming.Update, token);
			}
		}
	}

	private async UniTask CommandFunc(int _kind, CancellationToken token)
	{
		Heroine heroine = targetHeroine;
		bool isWait = true;
		if (touchCancellation != null)
		{
			AnimePlay("通常立ち");
		}
		selectCommand = _kind;
		switch (_kind)
		{
		case 0:
		case 1:
		case 2:
			canvas.enabled = false;
			raycast.enabled = false;
			await StartADV(ActionGame.Communication.Info.Group.Talk, _kind, token);
			Singleton<GameAssist>.Instance.DecreaseTalkTime(heroine, 1);
			AddDroppedTopics(_kind, m_CVInfo.topic);
			break;
		case 3:
			Singleton<GameAssist>.Instance.DecreaseTalkTime(heroine, 1);
			canvas.enabled = false;
			raycast.enabled = false;
			if (!isNPC)
			{
				int topic4 = Singleton<GameAssist>.Instance.GetTopic(heroine);
				ActionGame.Communication.Info.TopicListenInfo tli = info.GetTopicListenInfo(topic4);
				await StartADV(info.CreateADV(tli.introduction), token);
				listenTopic.Topic = topic4;
				listenTopic.Info = tli;
			}
			else if (heroine.tipsCount <= 0 || !info.CheckTips())
			{
				await StartADV(info.GetListenADV(), token);
			}
			else
			{
				int topic4 = -1;
				await StartADV(info.GetTipsADV(ref topic4), token);
				heroine.tipsCount--;
				Game.saveData.tips.Add(topic4);
			}
			break;
		case 4:
		{
			textTopicTitle.text = "話題を振る";
			selectTopic = -1;
			topicCancellation = new CancellationTokenSource();
			await UniTask.WaitUntil(() => selectTopic != -1, PlayerLoopTiming.Update, topicCancellation.Token);
			TopicCancel();
			Singleton<GameAssist>.Instance.DecreaseTalkTime(heroine, 1);
			canvas.enabled = false;
			raycast.enabled = false;
			Topic.Param t = topics[selectTopic];
			Game.Player.topicStock[t.No].Sub();
			if (!isNPC)
			{
				GameAssist.TopicTalkPoint tkp = Singleton<GameAssist>.Instance.topicTalkPoint;
				int rarity = t.Rarity;
				if ((uint)rarity <= 1u)
				{
					int topicTalkCommon = Singleton<GameAssist>.Instance.GetTopicTalkCommon(heroine, t);
					await StartADV(info.CreateGenericADV(info.dicTopicCommon[topicTalkCommon]), token);
				}
				else
				{
					await StartADV(info.CreateGenericADV(info.dicTopicRare[t.No]), token);
					heroine.topicTalked.Add(t.No);
				}
				int num = ((t.Category != 4) ? tkp.favor[t.Rarity] : 0);
				int num2 = ((t.Category == 4) ? tkp.favor[t.Rarity] : 0);
				int num3 = tkp.point[t.Rarity];
				ChaFileParameter.Interest interest = heroine.parameter.interest;
				if (interest.answer[0] == t.Category || interest.answer[1] == t.Category)
				{
					GameAssist.TopicInterest topicInterest = Singleton<GameAssist>.Instance.topicInterest;
					num += topicInterest.favor[t.Category];
					num2 += topicInterest.lewdness[t.Category];
					num3 += topicInterest.point[t.Category];
				}
				m_CVInfo = new ChangeValueTopicInfo(num, num2, num3);
			}
			else
			{
				_ = Singleton<GameAssist>.Instance.topicTalkPoint;
				int topicReplyNPC = Singleton<GameAssist>.Instance.GetTopicReplyNPC(heroine, t);
				await StartADV(info.GetTalkADV(topicReplyNPC), token);
				int key = Mathf.Clamp(heroine.relationNPC, 0, 2);
				int num4 = SingletonInitializer<ActionScene>.instance.stepUpDic[key];
				m_CVInfo = new ChangeValueTopicInfo(Mathf.CeilToInt((float)num4 / 4f));
			}
			break;
		}
		case 5:
		case 6:
		case 7:
		case 8:
		case 9:
		case 10:
		case 11:
			Singleton<GameAssist>.Instance.DecreaseTalkTime(heroine, 1);
			if (!isNPC)
			{
				int topic4 = -1;
				int command2 = _kind - 5;
				bool _success = false;
				List<Program.Transfer> eventADV = info.GetEventADV(command2, ref topic4, ref resultInfo.isFirst, ref _success);
				if (_kind == 11 && _success && info.mapNo == 9)
				{
					topic4 = 10;
				}
				canvas.enabled = false;
				raycast.enabled = false;
				if (!eventADV.IsNullOrEmpty())
				{
					await StartADV(eventADV, token);
				}
				heroine.talkCommand.Add(command2);
				if (topic4 != -1)
				{
					resultInfo.result = (ResultEnum)topic4;
					ResultEnum result = resultInfo.result;
					if (result == ResultEnum.Meal)
					{
						heroine.isLunch = true;
						SingletonInitializer<ActionScene>.instance.actCtrl.SetDesire(heroine, 3);
					}
					if (topic4 == 10)
					{
						LeaveAloneCancel();
						lstColDisposable.ForEach(delegate(ColDisposableInfo x)
						{
							x.End();
						});
						TouchCancel();
						if (transVoice != null)
						{
							Manager.Voice.Stop(transVoice);
							transVoice = null;
						}
						if (!token.IsCancellationRequested)
						{
							OnClickMain(-1);
						}
						isUpdateCamera = false;
						timeGaugeInfo?.Dispose();
						favorGaugeInfo?.Dispose();
						lewdnessGaugeInfo?.Dispose();
						if (lstColDisposable != null)
						{
							lstColDisposable.ForEach(delegate(ColDisposableInfo x)
							{
								x.End();
							});
						}
						await Fade(FadeCanvas.Fade.In);
						targetHeroine.favor = 0;
						Cancel();
						return;
					}
					await TalkEnd(token);
					isWait = false;
				}
				else if (command2 == 6 && heroine.isGirlfriend)
				{
					favorGaugeInfo.SetStage(2, 0);
					favorGaugeInfo.SetMax(SingletonInitializer<ActionScene>.instance.stepUpDic[2]);
					m_CVInfo = new ChangeValueTopicInfo(0, 0, Singleton<GameAssist>.Instance.pointDrops[8]);
				}
			}
			else
			{
				Utils.Sound.Play(new Utils.Sound.SettingBGM(BGM.Reminiscence));
				await StartADV(info.GetReminiscenceADV(), token);
				endADVNo = ActionGame.Communication.Info.EndADV.RankUp;
				koikatsuPoint += Singleton<GameAssist>.Instance.pointDrops[10 + targetHeroine.relationNPC];
				canvas.enabled = false;
				raycast.enabled = false;
				await TalkEnd(token);
				isWait = false;
			}
			break;
		case 20:
		case 21:
		{
			Singleton<GameAssist>.Instance.DecreaseTalkTime(heroine, 1);
			int command2 = -1;
			int topic4 = _kind;
			bool _success2 = false;
			await StartADV(info.GetEventADV(topic4, ref command2, ref resultInfo.isFirst, ref _success2), token);
			heroine.talkCommand.Add(topic4);
			if (command2 != -1)
			{
				resultInfo.result = (ResultEnum)command2;
				_ = resultInfo.result;
				_ = 2;
				await TalkEnd(token);
				isWait = false;
			}
			break;
		}
		case -1:
			await StartADV((!isNPC) ? info.GetEndConversationADV(_oldAnger: false, _endSelect: true, out endADVNo) : info.GetEndConversationADV(_endSelect: true), token);
			canvas.enabled = false;
			raycast.enabled = false;
			await TalkEnd(token);
			isWait = false;
			break;
		default:
			Singleton<GameAssist>.Instance.DecreaseTalkTime(heroine, 1);
			break;
		}
		LeaveAloneCancel();
		lstColDisposable.ForEach(delegate(ColDisposableInfo x)
		{
			x.End();
		});
		TouchCancel();
		if (transVoice != null)
		{
			Manager.Voice.Stop(transVoice);
			transVoice = null;
		}
		if (!token.IsCancellationRequested)
		{
			OnClickMain(-1);
		}
		if (isWait)
		{
			await CommandWait(token);
		}
	}

	private async UniTask TouchFunc(string _kind, Vector3 _pos)
	{
		bool flag = touchMode == 0;
		int num = -1;
		int num2 = ((!flag) ? 10 : 0);
		switch (_kind)
		{
		case "Head":
			num = (flag ? 2 : 0);
			break;
		case "Cheek":
			num = (flag ? 3 : 0);
			break;
		case "HandL":
		case "HandR":
			num = ((!flag) ? (-1) : 0);
			break;
		case "MuneL":
		case "MuneR":
			num = 1;
			break;
		}
		if (num == -1)
		{
			return;
		}
		int num3 = ++dicTouchCount[num + num2];
		float num4 = 0f;
		Mathf.Clamp((float)(num3 - 1) * 50f - num4, 0f, 100f);
		int num5 = num3 switch
		{
			1 => 0, 
			2 => 2, 
			_ => 1, 
		};
		if (isNPC)
		{
			num5 = 0;
		}
		Game.Player.koikatsuPoint++;
		Singleton<GameAssist>.Instance.DecreaseTalkTime(targetHeroine, 1);
		Manager.Voice.Stop(targetHeroine.voiceNo);
		TouchReactionInfo touchVoice = info.GetTouchVoice(flag, num, num5, out m_CVInfo);
		LoadVoice(touchVoice.assetbundle, touchVoice.file, touchVoice.facialExpression, touchVoice.pose);
		LeaveAloneCancel();
		Cursor.SetCursor(null, Vector2.zero, CursorMode.ForceSoftware);
		lstColDisposable.ForEach(delegate(ColDisposableInfo x)
		{
			x.End();
		});
		canvas.enabled = false;
		raycast.enabled = false;
		TouchCancel();
		TalkScene talkScene = this;
		CancellationTokenSource cancellationTokenSource2 = (touchCancellation = new CancellationTokenSource());
		await talkScene.TouchEnd(cancellationTokenSource2.Token);
		ReflectChangeValue();
		List<Program.Transfer> endConversationADV = info.GetEndConversationADV(_oldAnger: false, _endSelect: false, out endADVNo);
		if (endConversationADV != null)
		{
			CancellationToken token = cancellation.Token;
			canvas.enabled = false;
			raycast.enabled = false;
			Cursor.SetCursor(null, Vector2.zero, CursorMode.ForceSoftware);
			if (transVoice != null)
			{
				Manager.Voice.Stop(transVoice);
				transVoice = null;
			}
			if (endADVNo == ActionGame.Communication.Info.EndADV.RankUp)
			{
				koikatsuPoint += Singleton<GameAssist>.Instance.pointDrops[7];
			}
			await StartADV(endConversationADV, token);
			await TalkEnd(token);
		}
		else
		{
			m_CVInfo = null;
			StartSelection();
		}
	}

	private void EnterFunc(string _kind)
	{
		Cursor.SetCursor((touchMode == 0) ? texCursor[0] : texCursor[1], new Vector2(32f, 32f), CursorMode.ForceSoftware);
	}

	private void ExitFunc(string _kind)
	{
		Cursor.SetCursor(null, Vector2.zero, CursorMode.ForceSoftware);
	}

	private int RandomValue(params int[] _array)
	{
		return _array.Shuffle().First();
	}

	private void UpdateTopicListen()
	{
		Dictionary<int, Player.TopicState> stock = Game.Player.topicStock;
		int hitC = listenTopic.Category;
		topics = (from v in SingletonInitializer<ActionScene>.instance.topicDic.Values
			where stock.TryGetValue(v.No, out var value2) && value2.num > 0
			orderby v.Rarity
			orderby (v.Category != hitC) ? v.Category : (-1)
			select v).ToList();
		topics.Add(new Topic.Param
		{
			No = -1,
			Category = -1,
			Rarity = -1,
			RarityName = "Unknown",
			Name = "誤魔化す"
		});
		int count = topics.Count;
		if (!loopListView2.IsInit)
		{
			loopListView2.InitListView(count, (LoopListView2 _view, int _index) => OnUpdate(_view, _index));
		}
		else if (!loopListView2.SetListItemCount(count, resetPos: true, OnUpdate))
		{
			loopListView2.RefreshAllShownItem();
		}
		LoopListViewItem2 OnUpdate(LoopListView2 _view, int _index)
		{
			if (_index >= 0)
			{
				LoopListViewItem2 loopListViewItem = _view.NewListViewItem("Node");
				NodeInfo component = loopListViewItem.GetComponent<NodeInfo>();
				Topic.Param param = topics[_index];
				component.Text = param.Name;
				if ((SingletonInitializer<ActionScene>.instance.Player.charaData as Player).topicStock.TryGetValue(param.No, out var value))
				{
					int num = Mathf.Clamp(value.num, 0, 99);
					component.Num = $"x{num:00}";
				}
				else
				{
					component.Num = "";
				}
				if (param.Rarity >= 0)
				{
					TopicSprite topicSprite = topicSprites[param.Rarity];
					component.Button.image.sprite = topicSprite.sprites[0];
					SpriteState spriteState = component.Button.spriteState;
					spriteState.highlightedSprite = topicSprite.sprites[1];
					spriteState.pressedSprite = topicSprite.sprites[1];
					spriteState.selectedSprite = topicSprite.sprites[0];
					component.Button.spriteState = spriteState;
					component.Image = topicImages[param.Category].sprites[0];
				}
				else
				{
					component.Button.image.sprite = topicNoneSprite.sprites[0];
					SpriteState spriteState2 = component.Button.spriteState;
					spriteState2.highlightedSprite = topicNoneSprite.sprites[1];
					spriteState2.pressedSprite = topicNoneSprite.sprites[1];
					spriteState2.selectedSprite = topicNoneSprite.sprites[0];
					component.Button.spriteState = spriteState2;
					component.Image = topicNoneImage.sprites[0];
				}
				component.Initialize();
				component.Button.onClick.RemoveAllListeners();
				int id = _index;
				component.Button.onClick.AddListener(delegate
				{
					selectTopic = id;
					Utils.Sound.Play(SystemSE.ok_s);
				});
				return loopListViewItem;
			}
			return null;
		}
	}

	private void ReflectChangeValue()
	{
		Heroine heroine = targetHeroine;
		if (isDesire)
		{
			ActionControl actCtrl = SingletonInitializer<ActionScene>.instance.actCtrl;
			actCtrl.AddDesire(4, heroine, RandomValue(0, 1, 1, 1, 1, 2, 2, 2, 3, 3));
			int num = RandomValue(0, 1, 1, 1, 1, 2, 2, 2, 3, 3) * heroine.relation;
			if (Game.saveData.prayedResult == sr_PrayH && SingletonInitializer<ActionScene>.instance.prayInfoTable.TryGetValue(sr_PrayH, out var value))
			{
				num += value.Effect;
			}
			actCtrl.AddDesire(5, heroine, num);
			int value2 = (heroine.parameter.attribute.likeGirls ? RandomValue(2, 2, 2, 2, 3, 3, 3, 4, 4, 5) : RandomValue(0, 0, 0, 0, 0, 1, 1, 1, 2, 2));
			actCtrl.AddDesire(26, heroine, value2);
			isDesire = false;
		}
		if (m_CVInfo != null)
		{
			isSuccess |= Singleton<GameAssist>.Instance.ChangeParam(heroine, m_CVInfo, isNPC);
			if (m_CVInfo is ChangeValueTopicInfo changeValueTopicInfo)
			{
				Game.Player.koikatsuPoint += changeValueTopicInfo.point;
			}
			else if (koikatsuPoint > 0f)
			{
				Game.Player.koikatsuPoint += Mathf.FloorToInt(koikatsuPoint);
				koikatsuPoint = 0f;
			}
			if (m_CVInfo is ChangeValueSelectInfo changeValueSelectInfo)
			{
				Manager.Sound.Play(new Manager.Sound.Loader
				{
					type = Manager.Sound.Type.SystemSE,
					bundle = "sound/data/systemse/00.unity3d",
					asset = (changeValueSelectInfo.isSuccess ? "sse_01_00" : "sse_01_01")
				});
			}
		}
	}

	private void LoadVoice(string _assetbundle, string _file, string _expression, string _pose, bool _isCheck = false)
	{
		Heroine heroine = targetHeroine;
		AudioSource audioSource = Utils.Voice.Play(new Utils.Voice.Setting
		{
			assetBundleName = _assetbundle,
			assetName = _file,
			no = heroine.voiceNo,
			pitch = heroine.voicePitch
		});
		transVoice = ((audioSource != null) ? audioSource.transform : null);
		heroine.chaCtrl.SetLipSync(audioSource);
		Singleton<GameAssist>.Instance.ChangeExpression(heroine.FixCharaIDOrPersonality, _expression, heroine.chaCtrl);
		AnimePlay(_pose, _isCheck);
	}

	private void AnimePlay(string _pose, bool _isCheck = false)
	{
		if (_pose.IsNullOrEmpty())
		{
			return;
		}
		Heroine heroine = targetHeroine;
		string[] poseInfo = Singleton<GameAssist>.Instance.GetPoseInfo(heroine.FixCharaIDOrPersonality, _pose);
		if (!((IReadOnlyCollection<string>)(object)poseInfo).IsNullOrEmpty())
		{
			if (motion.Setting(heroine.chaCtrl.animBody, poseInfo[0], poseInfo[1], poseInfo[2], _isCheck))
			{
				motion.Play(heroine.chaCtrl.animBody);
				heroine.chaCtrl.animBody.Update(0f);
			}
			ikmotion.Setting(heroine.chaCtrl, poseInfo.SafeGet(3), poseInfo.SafeGet(4), poseInfo[2], _isCheck);
			if ((bool)crossFade)
			{
				crossFade.FadeStart();
			}
		}
	}

	private async UniTask StartADV(ActionGame.Communication.Info.Group _group, int _command, CancellationToken token)
	{
		await StartADV(info.GetADV(out m_CVInfo, _group, _command), token);
		if (_group == ActionGame.Communication.Info.Group.Talk && _command == 2)
		{
			isDesire = true;
		}
	}

	private async UniTask StartADV(List<Program.Transfer> _list, CancellationToken token)
	{
		Transform transform = (isIntroductionOnly ? nowCamera.transform : cameraTransform[2]);
		await Program.Open(new Data
		{
			fadeInTime = 0f,
			position = Vector3.zero,
			rotation = Quaternion.identity,
			camera = new OpenData.CameraData
			{
				position = transform.position,
				rotation = transform.rotation
			},
			scene = this,
			transferList = _list,
			heroineList = new List<Heroine> { targetHeroine },
			motionReserverList = new List<ADV.CharaData.MotionReserver>
			{
				new ADV.CharaData.MotionReserver
				{
					ikMotion = ikmotion
				}
			}
		}, token);
	}

	private async UniTask CommandWait(CancellationToken token)
	{
		isUpdateCamera = false;
		await Program.ADVProcessingCheck(token);
		if (selectCommand == 3 && !isNPC)
		{
			canvas.enabled = true;
			raycast.enabled = true;
			textTopicTitle.text = "話題を選ぶ";
			selectTopic = -1;
			UpdateTopicListen();
			OnClickMain(1);
			objBlocker.SetActiveIfDifferent(active: true);
			await UniTask.WaitUntil(() => selectTopic != -1, PlayerLoopTiming.Update, token);
			OnClickMain(-1);
			objBlocker.SetActiveIfDifferent(active: false);
			Topic.Param param = topics[selectTopic];
			if (Game.Player.topicStock.TryGetValue(param.No, out var value))
			{
				value.Sub();
			}
			canvas.enabled = false;
			raycast.enabled = false;
			GameAssist.TopicListenPoint topicListenPoint = Singleton<GameAssist>.Instance.topicListenPoint;
			if (param.No == listenTopic.Topic)
			{
				m_CVInfo = new ChangeValueTopicInfo((param.Rarity == 0) ? topicListenPoint.common[0] : topicListenPoint.rare[0], 0, topicListenPoint.point[0]);
				await StartADV(info.CreateADV(listenTopic.Info.success), token);
			}
			else if (param.Category == listenTopic.Category)
			{
				m_CVInfo = new ChangeValueTopicInfo((param.Rarity == 0) ? topicListenPoint.common[1] : topicListenPoint.rare[2], 0, topicListenPoint.point[1]);
				await StartADV(info.CreateADV(listenTopic.Info.Normal), token);
			}
			else
			{
				m_CVInfo = new ChangeValueTopicInfo((param.Rarity == 0) ? topicListenPoint.common[2] : topicListenPoint.rare[2], 0, topicListenPoint.point[2]);
				await StartADV(info.CreateADV(listenTopic.Info.Failure), token);
			}
			await Program.ADVProcessingCheck(token);
		}
		ReflectChangeValue();
		switch (selectCommand)
		{
		case 2:
			SingletonInitializer<ActionControl>.instance.AddDesire(4, targetHeroine, 5);
			SingletonInitializer<ActionControl>.instance.AddDesire(5, targetHeroine, 5);
			SingletonInitializer<ActionControl>.instance.AddDesire(53, targetHeroine, 5);
			SingletonInitializer<ActionControl>.instance.AddDesire(54, targetHeroine, 5);
			break;
		case 4:
			if (topics[selectTopic].Category == 4)
			{
				SingletonInitializer<ActionControl>.instance.AddDesire(4, targetHeroine, 10);
				SingletonInitializer<ActionControl>.instance.AddDesire(5, targetHeroine, 10);
				SingletonInitializer<ActionControl>.instance.AddDesire(53, targetHeroine, 10);
				SingletonInitializer<ActionControl>.instance.AddDesire(54, targetHeroine, 10);
			}
			break;
		}
		if (droppedTopic != null)
		{
			int num = droppedTopic.probability;
			if (Game.saveData.prayedResult == sr_PrayTopicDrop && SingletonInitializer<ActionScene>.instance.prayInfoTable.TryGetValue(sr_PrayTopicDrop, out var value2))
			{
				num += value2.Effect;
			}
			if (!commandCount.TryGetValue(droppedTopic.command, out var value3))
			{
				value3 = 0;
			}
			int command = droppedTopic.command;
			if ((uint)(command - 1) <= 1u)
			{
				num += value3 * 2;
			}
			if (Lottery(num))
			{
				Dictionary<int, Player.TopicState> topicStock = Game.Player.topicStock;
				if (!topicStock.TryGetValue(droppedTopic.topic, out var value4))
				{
					value4 = (topicStock[droppedTopic.topic] = new Player.TopicState());
				}
				Topic.Param param2 = SingletonInitializer<ActionScene>.instance.topicDic[droppedTopic.topic];
				if (!value4.Add())
				{
					await InformationUI.SetTopicFullAsync();
				}
				else
				{
					Game.Player.koikatsuPoint += Singleton<GameAssist>.Instance.pointDrops[param2.Rarity + 4];
					await InformationUI.SetTopicAsync(param2.Name);
				}
			}
			droppedTopic = null;
		}
		List<Program.Transfer> eventFromHeroineADV = info.GetEventFromHeroineADV(out m_CVInfo, ref heroineEvent);
		if (eventFromHeroineADV != null)
		{
			await StartADV(eventFromHeroineADV, token);
			await HeroineEventWait(token);
			return;
		}
		if (isNPC)
		{
			List<Program.Transfer> rankUpADV = info.GetRankUpADV();
			if (!rankUpADV.IsNullOrEmpty())
			{
				switch (targetHeroine.relationNPC)
				{
				case 0:
					Utils.Sound.Play(new Utils.Sound.SettingBGM(BGM.Communication));
					break;
				case 1:
					Utils.Sound.Play(new Utils.Sound.SettingBGM(BGM.Lover));
					break;
				case 2:
					Utils.Sound.Play(new Utils.Sound.SettingBGM(BGM.Reminiscence));
					break;
				}
				await StartADV(rankUpADV, token);
				endADVNo = ActionGame.Communication.Info.EndADV.RankUp;
				koikatsuPoint += Singleton<GameAssist>.Instance.pointDrops[10 + targetHeroine.relationNPC];
				canvas.enabled = false;
				raycast.enabled = false;
				await TalkEnd(token);
				return;
			}
		}
		List<Program.Transfer> list = ((!isNPC) ? info.GetEndConversationADV(_oldAnger: false, _endSelect: false, out endADVNo) : info.GetEndConversationADV());
		if (list != null)
		{
			await StartADV(list, token);
			if (endADVNo == ActionGame.Communication.Info.EndADV.RankUp)
			{
				koikatsuPoint += Singleton<GameAssist>.Instance.pointDrops[7];
			}
			canvas.enabled = false;
			raycast.enabled = false;
			await TalkEnd(token);
			return;
		}
		List<Program.Transfer> eventFromHeroineADV2 = info.GetEventFromHeroineADV(out m_CVInfo, ref heroineEvent);
		if (eventFromHeroineADV2 != null)
		{
			await StartADV(eventFromHeroineADV2, token);
			await HeroineEventWait(token);
			return;
		}
		if (!Manager.Voice.IsPlay(targetHeroine.voiceNo))
		{
			InterludeReactionInfo interludeVoice = info.GetInterludeVoice(m_CVInfo);
			if (interludeVoice.isInterlude)
			{
				if (interludeVoice.isScenario)
				{
					await StartADV(info.CreateGenericADV(interludeVoice.genericInfo), token);
					await Program.ADVProcessingCheck(token);
				}
				else
				{
					LoadVoice(interludeVoice.assetbundle, interludeVoice.file, interludeVoice.facialExpression, interludeVoice.pose);
				}
			}
		}
		m_CVInfo = null;
		selectTopic = -1;
		selectButton = -1;
		selectCommand = -1;
		StartSelection();
	}

	private async UniTask HeroineEventWait(CancellationToken token)
	{
		Heroine heroine = targetHeroine;
		isUpdateCamera = false;
		await UniTask.Yield(PlayerLoopTiming.Update, token);
		await Program.ADVProcessingCheck(token);
		switch (heroineEvent)
		{
		case 0:
			resultInfo.result = ResultEnum.Tourism;
			break;
		case 1:
			resultInfo.result = ResultEnum.Play;
			break;
		case 2:
			resultInfo.result = ResultEnum.Experience;
			break;
		case 3:
			resultInfo.result = ResultEnum.Meal;
			break;
		case 5:
			resultInfo.result = ResultEnum.Date;
			break;
		case 7:
			resultInfo.result = ResultEnum.ByTheWay;
			break;
		default:
			resultInfo.result = ResultEnum.None;
			break;
		}
		heroine.talkCommand.Add(heroineEvent);
		switch (heroineEvent)
		{
		case 6:
			if (selectNo == 0)
			{
				heroine.isGirlfriend = true;
				heroine.favor = 0;
				favorGaugeInfo.SetStage(2, 0);
				favorGaugeInfo.SetMax(SingletonInitializer<ActionScene>.instance.stepUpDic[2]);
				if (!heroine.isFirstGirlfriend)
				{
					heroine.isFirstGirlfriend = true;
					SingletonInitializer<Game>.instance.rankSaveData.girlfriendCount++;
				}
			}
			else
			{
				heroine.talkTime = 0;
			}
			heroine.confessed = true;
			break;
		case 7:
			resultInfo.result = ((selectNo == 0) ? ResultEnum.ByTheWay : ResultEnum.None);
			if (resultInfo.result == ResultEnum.ByTheWay)
			{
				heroine.talkCommand.Remove(1);
				heroine.talkEvent.Add(12);
				heroine.talkEvent.Add(8);
				Cancel();
				return;
			}
			if (necessaryInfo.isHAttack)
			{
				Cancel();
				return;
			}
			break;
		case 5:
			if (selectNo == 0)
			{
				heroine.talkEvent.Add(9);
				heroine.talkEvent.Add(13);
				SingletonInitializer<ActionScene>.instance.Cycle.dateHeroine = heroine;
			}
			break;
		default:
			if (selectNo == 0)
			{
				if (resultInfo.result == ResultEnum.Meal)
				{
					resultInfo.isFirst = !heroine.talkEvent.Contains(79);
					heroine.isLunch = true;
					SingletonInitializer<ActionScene>.instance.actCtrl.SetDesire(heroine, 3);
				}
				ResultEnum result = resultInfo.result;
				if ((uint)(result - 4) <= 3u)
				{
					await Fade(FadeCanvas.Fade.In);
				}
				Cancel();
				return;
			}
			resultInfo.result = ResultEnum.None;
			break;
		}
		ReflectChangeValue();
		List<Program.Transfer> endConversationADV = info.GetEndConversationADV(_oldAnger: false, _endSelect: false, out endADVNo);
		if (endConversationADV != null)
		{
			await StartADV(endConversationADV, token);
			await TalkEnd(token);
		}
		else
		{
			StartSelection();
		}
	}

	private async UniTask InterludeWait(CancellationToken token)
	{
		isUpdateCamera = false;
		await UniTask.Yield(PlayerLoopTiming.Update, token);
		await Program.ADVProcessingCheck(token);
		StartSelection();
	}

	private async UniTask TalkEnd(CancellationToken token)
	{
		Heroine heroine = targetHeroine;
		isUpdateCamera = false;
		await UniTask.Yield(PlayerLoopTiming.Update, token);
		await Program.ADVProcessingCheck(token);
		timeGaugeInfo?.Dispose();
		favorGaugeInfo?.Dispose();
		lewdnessGaugeInfo?.Dispose();
		if (lstColDisposable != null)
		{
			lstColDisposable.ForEach(delegate(ColDisposableInfo x)
			{
				x.End();
			});
		}
		ActionGame.Communication.Info.EndADV endADV = endADVNo;
		if (endADV == ActionGame.Communication.Info.EndADV.RankUp)
		{
			if (!isNPC)
			{
				heroine.talkEvent.Add(0);
				heroine.favor = 0;
				heroine.isFriend = true;
			}
			else
			{
				heroine.relationNPC++;
				heroine.isFriend = heroine.relationNPC > 0;
				heroine.isGirlfriend = heroine.relationNPC == 3;
				if (heroine.relationNPC < 3)
				{
					heroine.favor = 0;
				}
			}
		}
		switch (resultInfo.result)
		{
		case ResultEnum.Tourism:
			Game.Player.koikatsuPoint += Mathf.FloorToInt(koikatsuPoint);
			break;
		case ResultEnum.Play:
			Game.Player.koikatsuPoint += Mathf.FloorToInt(koikatsuPoint);
			break;
		case ResultEnum.Experience:
			Game.Player.koikatsuPoint += Mathf.FloorToInt(koikatsuPoint);
			break;
		case ResultEnum.Meal:
			Game.Player.koikatsuPoint += Mathf.FloorToInt(koikatsuPoint);
			break;
		case ResultEnum.None:
			if (!droppedTopics.IsNullOrEmpty())
			{
				int drop = DropTopic();
				if (drop >= 0)
				{
					Dictionary<int, Player.TopicState> topicStock = Game.Player.topicStock;
					if (!topicStock.TryGetValue(drop, out var value))
					{
						value = (topicStock[drop] = new Player.TopicState());
					}
					Topic.Param param = SingletonInitializer<ActionScene>.instance.topicDic[drop];
					if (!value.Add())
					{
						await InformationUI.SetTopicFullAsync();
					}
					else
					{
						koikatsuPoint += Singleton<GameAssist>.Instance.pointDrops[param.Rarity + 4];
						Game.Player.koikatsuPoint += Mathf.FloorToInt(koikatsuPoint);
						await InformationUI.SetTopicAsync(param.Name);
					}
					if (heroine.topicDrop == null)
					{
						heroine.topicDrop = new HashSet<int>();
					}
					heroine.topicDrop.Add(drop);
				}
				else
				{
					Game.Player.koikatsuPoint += Mathf.FloorToInt(koikatsuPoint);
					await UniTask.WaitWhile(() => KoikatsuPointUI.isActive);
				}
			}
			else
			{
				Game.Player.koikatsuPoint += Mathf.FloorToInt(koikatsuPoint);
				await UniTask.WaitWhile(() => KoikatsuPointUI.isActive);
			}
			break;
		}
		ResultEnum result = resultInfo.result;
		if ((uint)(result - 3) <= 4u || result == ResultEnum.Confess)
		{
			await Fade(FadeCanvas.Fade.In);
		}
		Cancel();
		int DropTopic()
		{
			int result2 = -1;
			foreach (DroppedTopic item in droppedTopics.Shuffle())
			{
				int num = item.probability;
				if (Game.saveData.prayedResult == sr_PrayTopicDrop && SingletonInitializer<ActionScene>.instance.prayInfoTable.TryGetValue(sr_PrayTopicDrop, out var value2))
				{
					num += value2.Effect;
				}
				if (!commandCount.TryGetValue(item.command, out var value3))
				{
					value3 = 0;
				}
				int command = item.command;
				num = (((uint)(command - 1) > 1u) ? (num + value3 * 10) : (num + value3 * 2));
				if (Lottery(num))
				{
					result2 = item.topic;
					break;
				}
			}
			return result2;
		}
	}

	private async UniTask TouchEnd(CancellationToken token)
	{
		_ = targetHeroine;
		isUpdateCamera = false;
		await UniTask.Yield(PlayerLoopTiming.Update, token);
		await UniTask.WaitWhile(() => transVoice != null, PlayerLoopTiming.Update, token);
		AnimePlay("通常立ち");
	}

	private void AddDroppedTopics(int _command, int _no)
	{
		if (_no >= 0)
		{
			if (!commandCount.ContainsKey(_command))
			{
				commandCount[_command] = 1;
			}
			else
			{
				commandCount[_command]++;
			}
			TopicDropData.Param param = Singleton<GameAssist>.Instance.DropTopic(_no);
			if (param != null && param.probability > 0)
			{
				droppedTopic = new DroppedTopic(param.no, param.probability, _command);
			}
		}
	}

	private bool Lottery(int _value)
	{
		if (_value >= 100)
		{
			return true;
		}
		_value = Mathf.Clamp(_value, 0, 100);
		lottery.Clear();
		for (int i = 0; i < _value; i++)
		{
			lottery.Add(1);
		}
		for (int j = 0; j < 100 - _value; j++)
		{
			lottery.Add(0);
		}
		return lottery.Shuffle().First() == 1;
	}
}
