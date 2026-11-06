using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ADV.Commands.Base;
using Config;
using Illusion.Component.UI;
using Illusion.CustomAttributes;
using Illusion.Extensions;
using Illusion.Game.Elements;
using Manager;
using SaveData;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace ADV;

[RequireComponent(typeof(CommandController))]
public class TextScenario : MonoBehaviour
{
	public interface IVoice
	{
		int personality { get; }

		string bundle { get; }

		string asset { get; }

		void Convert2D();

		AudioSource Play();

		bool Wait();
	}

	public interface IChara
	{
		int no { get; }

		void Play(TextScenario scenario);

		CharaData GetChara(TextScenario scenario);
	}

	public interface IMotion : IChara
	{
	}

	public interface IExpression : IChara
	{
	}

	public interface IExpressionIcon : IChara
	{
	}

	public class CurrentCharaData
	{
		private List<IExpressionIcon[]> _expressionIconList;

		public Dictionary<int, string> bundleVoices => _bundleVoices;

		public bool isSkip { get; set; }

		public IVoice karaokePlayer
		{
			set
			{
				_karaokePlayer = new List<IVoice[]> { new IVoice[1] { value } };
			}
		}

		public bool isKaraoke => _karaokePlayer != null;

		public List<IVoice[]> voiceList => _karaokePlayer ?? _voiceList;

		public List<IMotion[]> motionList => _motionList;

		public List<IExpression[]> expressionList => _expressionList;

		public List<IExpressionIcon[]> expressionIconList => _expressionIconList;

		private List<IVoice[]> _karaokePlayer { get; set; }

		private List<IVoice[]> _voiceList { get; set; }

		private List<IMotion[]> _motionList { get; set; }

		private List<IExpression[]> _expressionList { get; set; }

		private Dictionary<int, string> _bundleVoices { get; set; }

		public CurrentCharaData()
		{
			_bundleVoices = new Dictionary<int, string>();
		}

		public void CreateVoiceList()
		{
			if (voiceList == null)
			{
				_voiceList = new List<IVoice[]>();
			}
		}

		public void CreateMotionList()
		{
			if (motionList == null)
			{
				_motionList = new List<IMotion[]>();
			}
		}

		public void CreateExpressionList()
		{
			if (expressionList == null)
			{
				_expressionList = new List<IExpression[]>();
			}
		}

		public void CreateExpressionIconList()
		{
			if (expressionIconList == null)
			{
				_expressionIconList = new List<IExpressionIcon[]>();
			}
		}

		public void Clear()
		{
			_karaokePlayer = null;
			_voiceList = null;
			_motionList = null;
			_expressionList = null;
			_expressionIconList = null;
		}
	}

	public class ChoiceData
	{
		public string jump { get; }

		public string text { get; }

		public ChoiceData(string jump, string text)
		{
			this.jump = jump;
			this.text = text;
		}
	}

	public class LoopVoicePack
	{
		public int voiceNo { get; private set; }

		public ChaControl chaCtrl { get; private set; }

		public AudioSource audio { get; private set; }

		public LoopVoicePack(int voiceNo, ChaControl chaCtrl, AudioSource audio)
		{
			this.voiceNo = voiceNo;
			this.chaCtrl = chaCtrl;
			this.audio = audio;
		}

		public bool Set()
		{
			if (chaCtrl == null)
			{
				return false;
			}
			chaCtrl.SetLipSync(audio);
			return true;
		}
	}

	protected class NextInfo
	{
		public bool isCompleteDisplayText { get; set; }

		public bool isNext { get; set; }

		public bool isSkip { get; set; }

		public void Set(bool isCompleteDisplayText, bool isNext, bool isSkip)
		{
			this.isCompleteDisplayText = isCompleteDisplayText;
			this.isNext = isNext;
			this.isSkip = isSkip;
		}
	}

	[Serializable]
	private sealed class FileOpen
	{
		[SerializeField]
		private List<RootData> fileList = new List<RootData>();

		[SerializeField]
		private List<RootData> rootList = new List<RootData>();

		public List<RootData> FileList => fileList;

		public List<RootData> RootList => rootList;

		public void Clear()
		{
			fileList.Clear();
			rootList.Clear();
		}
	}

	[SerializeField]
	private Regulate _regulate;

	[SerializeField]
	private Info _info = new Info();

	[SerializeField]
	private Camera _AdvCamera;

	private CrossFade _crossFade;

	private CommandController _commandController;

	private TextController _textController;

	private ADVScene _advScene;

	private BaseCameraControl _baseCamCtrl;

	[SerializeField]
	protected RectTransform messageWindow;

	[SerializeField]
	protected Image nextMarker;

	[SerializeField]
	protected Canvas msgWindowCanvas;

	[SerializeField]
	private ADVFade advFade;

	[SerializeField]
	private BackGroundParam bgParam;

	[SerializeField]
	private Camera backCamera;

	[SerializeField]
	private Transform choices;

	[SerializeField]
	private Transform characters;

	[SerializeField]
	private Transform faceIcons;

	[SerializeField]
	private Image filterImage;

	[SerializeField]
	protected bool _isWindowImage = true;

	[SerializeField]
	protected bool _isSkip;

	[SerializeField]
	protected bool _isAuto;

	[SerializeField]
	protected bool _isAspect;

	[SerializeField]
	protected bool _isWait;

	[SerializeField]
	private string _loadBundleName = string.Empty;

	[SerializeField]
	private string _loadAssetName = string.Empty;

	[Header("Debug表示")]
	[SerializeField]
	[NotEditable]
	protected int currentLine;

	[SerializeField]
	[NotEditable]
	protected float autoWaitTimer;

	[SerializeField]
	[NotEditable]
	protected float autoWaitTime = 3f;

	[SerializeField]
	[NotEditable]
	protected bool _isText;

	[SerializeField]
	[NotEditable]
	protected bool _isChoice;

	[SerializeField]
	[NotEditable]
	protected bool _isInputLock;

	[SerializeField]
	[NotEditable]
	private bool _isSceneFadeRegulate = true;

	[SerializeField]
	[NotEditable]
	private bool _isStartRun;

	[SerializeField]
	[NotEditable]
	private bool _isCameraLock;

	protected Image windowImage;

	private Dictionary<string, ADVFaceIconData.Param> faceIconParamDic;

	protected HashSet<int> textHash = new HashSet<int>();

	private Dictionary<string, ValData> vars = new Dictionary<string, ValData>();

	private Dictionary<string, string> replaces = new Dictionary<string, string>();

	private IDisposable voicePlayDis;

	public Regulate regulate => _regulate;

	public Info info => _info;

	public Camera AdvCamera
	{
		get
		{
			return _AdvCamera;
		}
		set
		{
			if (value == null)
			{
				UnityEngine.Object.Destroy(_baseCamCtrl);
				_crossFade = null;
			}
			else
			{
				_baseCamCtrl = value.GetOrAddComponent<BaseCameraControl>();
				_baseCamCtrl.enabled = false;
				_crossFade = value.GetComponent<CrossFade>();
			}
			_AdvCamera = value;
		}
	}

	public CrossFade crossFade => _crossFade;

	public CurrentCharaData currentCharaData { get; } = new CurrentCharaData();


	public List<Program.Transfer> transferList
	{
		get
		{
			return _transferList;
		}
		set
		{
			_transferList = value;
		}
	}

	private List<Program.Transfer> _transferList { get; set; }

	public string fontColorKey { get; set; }

	public bool isWindowImage
	{
		get
		{
			return _isWindowImage;
		}
		set
		{
			_isWindowImage = value;
		}
	}

	public bool isSkip
	{
		get
		{
			return _isSkip;
		}
		set
		{
			_isSkip = value;
		}
	}

	public bool isAuto
	{
		get
		{
			return _isAuto;
		}
		set
		{
			_isAuto = value;
		}
	}

	public bool isAspect
	{
		get
		{
			return _isAspect;
		}
		set
		{
			_isAspect = value;
			if (AdvCamera != null)
			{
				AdvCamera.rect = (_isAspect ? MathfEx.AspectRect() : new Rect(0f, 0f, 1f, 1f));
			}
		}
	}

	public bool isWait
	{
		get
		{
			return _isWait;
		}
		set
		{
			_isWait = value;
		}
	}

	public int CurrentLine
	{
		get
		{
			return currentLine - 1;
		}
		set
		{
			currentLine = value;
		}
	}

	public string LoadBundleName
	{
		get
		{
			return _loadBundleName;
		}
		set
		{
			_loadBundleName = value;
		}
	}

	public string LoadAssetName
	{
		get
		{
			return _loadAssetName;
		}
		set
		{
			_loadAssetName = value;
		}
	}

	public bool isSceneFadeRegulate
	{
		get
		{
			return _isSceneFadeRegulate;
		}
		set
		{
			_isSceneFadeRegulate = value;
		}
	}

	public bool isChoice
	{
		get
		{
			return _isChoice;
		}
		set
		{
			_isChoice = value;
		}
	}

	public bool isInputLock
	{
		get
		{
			return _isInputLock;
		}
		set
		{
			_isInputLock = value;
		}
	}

	public bool isCameraLock
	{
		get
		{
			return _isCameraLock;
		}
		set
		{
			_isCameraLock = value;
			BaseCameraControl baseCamCtrl = BaseCamCtrl;
			if (baseCamCtrl != null)
			{
				baseCamCtrl.enabled = !_isCameraLock;
				baseCamCtrl.isInit = true;
				if (!_isCameraLock)
				{
					baseCamCtrl.SetCamera(baseCamCtrl.transform.position, baseCamCtrl.transform.eulerAngles, baseCamCtrl.transform.rotation, Vector3.zero - new Vector3(0f, 0f, baseCamCtrl.transform.position.z));
				}
				baseCamCtrl.ZoomCondition = () => false;
				baseCamCtrl.KeyCondition = () => !_isCameraLock;
				baseCamCtrl.NoCtrlCondition = () => _isCameraLock;
			}
		}
	}

	public bool isBackGroundCommanding { get; set; }

	public int commandTaskNo { get; set; }

	public CommandController commandController => this.GetComponentCache(ref _commandController);

	public TextController textController => this.GetComponentCache(ref _textController);

	public ADVScene advScene => this.GetComponentCache(ref _advScene);

	public List<ScenarioData.Param> CommandPacks => commandPacks;

	protected List<ScenarioData.Param> commandPacks { get; } = new List<ScenarioData.Param>();


	private FileOpen fileOpenData { get; } = new FileOpen();


	public virtual RectTransform MessageWindow => messageWindow;

	public BackGroundParam BGParam => bgParam;

	public Camera BackCamera => backCamera;

	public Transform Choices => choices;

	public Transform Characters => characters;

	public Transform FaceIcons => faceIcons;

	public Dictionary<string, ADVFaceIconData.Param> FaceIconParamDic => faceIconParamDic;

	public Image FilterImage => filterImage;

	public bool isSelectMessageWindow => messageWindowSelectUI.isSelect;

	public BaseCameraControl BaseCamCtrl
	{
		get
		{
			if (_baseCamCtrl != null)
			{
				return _baseCamCtrl;
			}
			if (_AdvCamera == null)
			{
				return null;
			}
			return _baseCamCtrl = _AdvCamera.GetComponent<BaseCameraControl>();
		}
	}

	public bool isFadeAllEnd
	{
		get
		{
			if (!SingletonInitializer<Manager.Scene>.initialized || advScene == null || advScene.AdvFade == null || _crossFade == null)
			{
				return true;
			}
			if (!Manager.Scene.IsFadeNow && advScene.AdvFade.IsEnd)
			{
				return _crossFade.isEnd;
			}
			return false;
		}
	}

	protected bool isRequestLine { get; set; }

	private SelectUI messageWindowSelectUI { get; set; }

	public Dictionary<string, ValData> Vars => vars;

	public Dictionary<string, string> Replaces => replaces;

	public Player player => Game.Player;

	public List<Heroine> heroineList { get; set; }

	public CharaData currentChara
	{
		get
		{
			return _currentChara;
		}
		set
		{
			_currentChara = value;
		}
	}

	private CharaData _currentChara { get; set; }

	public Heroine currentHeroine
	{
		get
		{
			if (currentChara == null)
			{
				return null;
			}
			return currentChara.heroine;
		}
	}

	private BackupPosRot backCameraBackup { get; set; }

	private ICommandListProc nowCommandList => GetCommandList(0);

	private Dictionary<int, ICommandListProc> _commandListTable { get; } = new Dictionary<int, ICommandListProc>();


	public IObservable<Unit> OnInitializedAsync => _single;

	private Illusion.Game.Elements.Single _single { get; set; } = new Illusion.Game.Elements.Single();


	public List<ChoiceData> choiceList { get; } = new List<ChoiceData>();


	public List<IDisposable> karaokeList => _karaokeList;

	private List<IDisposable> _karaokeList { get; } = new List<IDisposable>();


	public List<LoopVoicePack> loopVoiceList => _loopVoiceList;

	private List<LoopVoicePack> _loopVoiceList { get; } = new List<LoopVoicePack>();


	public static int VOICE_SET_NO { get; } = 1;


	public bool isButtonSkip => _nextInfo.isSkip;

	protected NextInfo _nextInfo { get; } = new NextInfo();


	public void CrossFadeStart()
	{
		if (!(_crossFade == null) && isFadeAllEnd)
		{
			_crossFade.FadeStart(info.anime.play.crossFadeTime);
		}
	}

	public ICommandListProc GetCommandList(int no)
	{
		if (!_commandListTable.TryGetValue(no, out var value))
		{
			ICommandListProc commandListProc = ((no != 0) ? ((ICommandListProc)new CommandLazyList(this)) : ((ICommandListProc)new CommandList(this)));
			value = (_commandListTable[no] = commandListProc);
		}
		return value;
	}

	public bool ChangeCurrentChara(int no)
	{
		currentChara = commandController.GetChara(no);
		return currentChara != null;
	}

	public string ReplaceVars(string arg)
	{
		if (Vars.TryGetValue(arg, out var value))
		{
			return value.o.ToString();
		}
		return arg;
	}

	public string ReplaceText(string text, bool useOnceTagConvert)
	{
		StringBuilder stringBuilder = new StringBuilder();
		int num = 0;
		try
		{
			foreach (Match item in Regex.Matches(text, "\\[.*?\\]"))
			{
				if (!item.Success)
				{
					continue;
				}
				string key = string.Empty;
				try
				{
					key = Regex.Replace(item.Value, "\\[|\\]", string.Empty);
				}
				catch (Exception)
				{
				}
				if (Replaces.TryGetValue(key, out var value))
				{
					if (!value.IsNullOrEmpty() && !NameCheck(key))
					{
						value = ReplaceText(value, useOnceTagConvert);
					}
					if (useOnceTagConvert)
					{
						OnceTagConvert(ref key, ref value);
					}
					if (value == null)
					{
						value = item.Value;
					}
				}
				else
				{
					value = item.Value;
				}
				stringBuilder.Append(text.Substring(num, item.Index - num));
				stringBuilder.Append(value);
				num = item.Index + item.Length;
			}
		}
		catch (Exception)
		{
		}
		stringBuilder.Append(text.Substring(num, text.Length - num));
		return stringBuilder.ToString();
	}

	public virtual void Initialize()
	{
		commandPacks.Clear();
		fileOpenData.Clear();
		_regulate.SetRegulate((Regulate.Control)0);
		windowImage = windowImage ?? MessageWindow.GetComponent<Image>();
		messageWindowSelectUI = MessageWindow.GetOrAddComponent<SelectUI>();
		if (backCameraBackup != null && backCamera != null)
		{
			backCameraBackup.Set(backCamera.transform);
		}
		currentLine = 0;
		autoWaitTimer = 0f;
		foreach (ICommandListProc value in _commandListTable.Values)
		{
			value.Clear();
		}
		commandController.Initialize();
		textController.Clear();
		vars.Clear();
		replaces.Clear();
		if (msgWindowCanvas != null)
		{
			msgWindowCanvas.enabled = false;
		}
		if (windowImage != null)
		{
			windowImage.enabled = false;
		}
		if (filterImage != null)
		{
			filterImage.enabled = false;
			filterImage.sprite = null;
		}
		ChoicesInit();
		advFade.SafeProc(delegate(ADVFade p)
		{
			p.Initialize();
		});
		_single.Done();
	}

	protected void MemberInit()
	{
		_isWindowImage = true;
		_isSkip = false;
		_isAuto = false;
		_isWait = false;
		_isChoice = false;
		_isSceneFadeRegulate = true;
		_isStartRun = false;
		_isCameraLock = false;
		textHash.Clear();
	}

	public virtual void Release()
	{
		if (voicePlayDis != null)
		{
			voicePlayDis.Dispose();
		}
		voicePlayDis = null;
		loopVoiceList.ForEach(delegate(LoopVoicePack p)
		{
			if (p.audio != null)
			{
				UnityEngine.Object.Destroy(p.audio.gameObject);
			}
		});
		loopVoiceList.Clear();
		karaokeList.ForEach(delegate(IDisposable p)
		{
			p.Dispose();
		});
		karaokeList.Clear();
		_single = new Illusion.Game.Elements.Single();
		commandController.Release();
		AdvCamera = null;
		info.audio.is2D = false;
		info.audio.isNotMoveMouth = false;
		choiceList.Clear();
	}

	public void CommandAdd(bool isNext, int line, bool multi, Command command, string[] args)
	{
		List<string> list = new List<string>();
		list.Add("0");
		list.Add(multi.ToString());
		list.Add(command.ToString());
		list.AddRange(args ?? new string[1] { string.Empty });
		ScenarioData.Param item = new ScenarioData.Param(list);
		if (commandPacks.Count == line)
		{
			commandPacks.Add(item);
		}
		else
		{
			commandPacks.Insert(line, item);
		}
		if (isNext)
		{
			RequestNextLine();
		}
	}

	public virtual bool LoadFile(string bundle, string asset, bool isClear = true, bool isClearCheck = true, bool isNext = true)
	{
		if (isClear)
		{
			textController.Clear();
			fileOpenData.Clear();
		}
		if (bundle.IsNullOrEmpty())
		{
			bundle = LoadBundleName;
		}
		if (!isClear && isClearCheck && fileOpenData.FileList.Any((RootData p) => p.bundleName == bundle && p.fileName == asset))
		{
			return false;
		}
		ScenarioData asset2 = AssetBundleManager.LoadAsset(bundle, asset, typeof(ScenarioData)).GetAsset<ScenarioData>();
		if (!isClear)
		{
			commandPacks.InsertRange(currentLine, asset2.list);
			if (!fileOpenData.FileList.Any((RootData p) => p.bundleName == bundle && p.fileName == asset && p.line == currentLine))
			{
				fileOpenData.FileList.Add(new RootData
				{
					bundleName = bundle,
					fileName = asset,
					line = CurrentLine
				});
			}
		}
		else
		{
			LoadBundleName = bundle;
			LoadAssetName = asset;
			string[] args = new string[5]
			{
				LoadBundleName,
				LoadAssetName,
				bool.FalseString,
				bool.TrueString,
				bool.TrueString
			};
			currentLine = 0;
			commandPacks.Clear();
			CommandAdd(isNext: false, currentLine++, multi: false, Command.Open, args);
			commandPacks.AddRange(asset2.list);
		}
		if (isNext)
		{
			RequestNextLine();
		}
		AssetBundleManager.UnloadAssetBundle(bundle, isUnloadForceRefCount: true);
		return true;
	}

	public bool SearchTagJumpOrOpenFile(string jump, int localLine)
	{
		string[] array = jump.Split(':');
		if (array.Length == 1)
		{
			if (SearchTag(jump, out var n))
			{
				Jump(n);
				return true;
			}
			return false;
		}
		Open open = new Open();
		open.Set(Command.Open);
		string[] argsDefault = open.ArgsDefault;
		for (int i = 0; i < array.Length && i < argsDefault.Length; i++)
		{
			argsDefault[i] = ReplaceVars(array[i]);
		}
		CommandAdd(isNext: false, localLine + 1, multi: false, open.command, argsDefault);
		return true;
	}

	public bool SearchTag(string tagName, out int n)
	{
		n = commandPacks.TakeWhile(delegate(ScenarioData.Param p)
		{
			if (p.Command != Command.Tag)
			{
				return true;
			}
			return (ReplaceVars(p.Args[0]) != tagName) ? true : false;
		}).Count();
		return n < commandPacks.Count;
	}

	public void Jump(int n)
	{
		currentLine = n;
		RequestNextLine();
	}

	public void ChangeWindow(UnityEngine.UI.Text nameWindow, UnityEngine.UI.Text messageWindow)
	{
		textController.Change(nameWindow, messageWindow);
		UnityEngine.UI.Text text = MessageWindow.GetComponentsInChildren<UnityEngine.UI.Text>(includeInactive: true).FirstOrDefault((UnityEngine.UI.Text p) => p.name == "Message");
		MessageWindow.gameObject.SetActive(text != null && text == messageWindow);
	}

	public virtual void ConfigProc()
	{
		if (Manager.Config.initialized)
		{
			TextSystem textData = Manager.Config.TextData;
			if (windowImage != null)
			{
				windowImage.color = Manager.Config.TextData.WindowColor;
			}
			textController.FontSpeed = textData.FontSpeed;
			autoWaitTime = textData.AutoWaitTime;
		}
	}

	public virtual void ChoicesInit()
	{
		_isChoice = false;
		if (Choices != null)
		{
			Choices.gameObject.SetActive(value: false);
		}
		nextMarker.SafeProc(delegate(Image p)
		{
			p.enabled = false;
		});
	}

	public void VoicePlay(List<IVoice[]> voices, Action onChange, Action onEnd)
	{
		if (voicePlayDis != null)
		{
			voicePlayDis.Dispose();
		}
		voicePlayDis = null;
		Manager.Voice.StopAll(isLoopStop: false);
		if (voices == null)
		{
			return;
		}
		if (_loopVoiceList.Any())
		{
			HashSet<int> hashSet = new HashSet<int>();
			foreach (IVoice[] voice2 in voices)
			{
				foreach (IVoice voice in voice2)
				{
					hashSet.Add(voice.personality);
				}
			}
			foreach (int item in hashSet)
			{
				foreach (LoopVoicePack loopVoice in _loopVoiceList)
				{
					if (loopVoice.voiceNo == item && loopVoice.audio != null)
					{
						loopVoice.audio.Pause();
					}
				}
			}
		}
		voicePlayDis = Observable.FromCoroutine((IObserver<Unit> observer) => VoicePlayCoroutine(observer, voices)).Subscribe((Action<Unit>)delegate
		{
			onChange?.Invoke();
		}, (Action)delegate
		{
			List<LoopVoicePack> list = new List<LoopVoicePack>();
			foreach (LoopVoicePack loopVoice2 in _loopVoiceList)
			{
				if (!loopVoice2.Set())
				{
					list.Add(loopVoice2);
				}
				else
				{
					loopVoice2.audio.Play();
				}
			}
			list.ForEach(delegate(LoopVoicePack item)
			{
				_loopVoiceList.Remove(item);
			});
			onEnd?.Invoke();
		});
	}

	private IEnumerator VoicePlayCoroutine(IObserver<Unit> observer, List<IVoice[]> voiceList)
	{
		foreach (IVoice[] voice in voiceList)
		{
			IVoice[] array = voice;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].Play();
			}
			observer.OnNext(Unit.Default);
			while (voice.Any((IVoice p) => p.Wait()))
			{
				yield return null;
			}
		}
		observer.OnCompleted();
	}

	protected virtual bool StartRun()
	{
		if (_isStartRun)
		{
			return false;
		}
		ADVCameraSetting();
		isAspect = isAspect;
		_isStartRun = true;
		int num = 0;
		Program.Transfer transfer = null;
		if (!_transferList.IsNullOrEmpty())
		{
			Program.Transfer transfer2 = _transferList[0];
			if (transfer2.param.Command == Command.SceneFadeRegulate)
			{
				_isSceneFadeRegulate = bool.Parse(transfer2.param.Args[0]);
			}
			transfer = _transferList.Find((Program.Transfer p) => p.param.Command == Command.Open);
			if (transfer != null)
			{
				LoadBundleName = transfer.param.Args[0];
				LoadAssetName = transfer.param.Args[1];
			}
		}
		if (!_transferList.IsNullOrEmpty())
		{
			foreach (Program.Transfer transfer3 in _transferList)
			{
				int line = ((transfer3.line == -1) ? num : transfer3.line);
				CommandAdd(isNext: false, line, transfer3.param.Multi, transfer3.param.Command, transfer3.param.Args);
				num++;
			}
		}
		if (transfer == null && !LoadBundleName.IsNullOrEmpty() && !LoadAssetName.IsNullOrEmpty())
		{
			string[] args = new string[5]
			{
				LoadBundleName,
				LoadAssetName,
				bool.FalseString,
				bool.TrueString,
				bool.TrueString
			};
			CommandAdd(isNext: true, num, multi: false, Command.Open, args);
		}
		return true;
	}

	protected void RequestNextLine()
	{
		StartCoroutine(_RequestNextLine());
	}

	protected virtual IEnumerator _RequestNextLine()
	{
		isRequestLine = true;
		nowCommandList.ProcessEnd();
		textHash.Clear();
		autoWaitTimer = 0f;
		while (true)
		{
			if (commandController.LoadingCharaList.Any())
			{
				yield return null;
				continue;
			}
			bool flag = false;
			if (currentLine < commandPacks.Count)
			{
				ScenarioData.Param param = commandPacks[currentLine++];
				if (param.Command != 0 && param.Command != Command.Log)
				{
					int no = ((param.Command != Command.Task) ? commandTaskNo : 0);
					flag = GetCommandList(no).Add(param, CurrentLine);
					Command command = param.Command;
					if (command == Command.Text)
					{
						textHash.Add(param.Hash);
					}
				}
			}
			if (!flag)
			{
				break;
			}
		}
		isRequestLine = false;
		foreach (int item in textHash)
		{
			if (AlreadyReadInfo.Add(item) && isSkip && Manager.Config.TextData.ReadSkip)
			{
				isSkip = false;
			}
		}
	}

	protected void ADVCameraSetting()
	{
		Camera advCamera = AdvCamera;
		if (advCamera != null)
		{
			Transform obj = advCamera.transform;
			Transform transform = BackCamera.transform;
			obj.SetParent(transform.parent, worldPositionStays: false);
			obj.SetPositionAndRotation(transform.position, transform.rotation);
			obj.localScale = Vector3.one;
			advCamera.fieldOfView = BackCamera.fieldOfView;
			advCamera.farClipPlane = 10000f;
		}
		if (bgParam != null)
		{
			bgParam.visible = false;
		}
		isCameraLock = true;
	}

	protected bool MessageWindowProc(NextInfo nextInfo)
	{
		if (isRequestLine)
		{
			return false;
		}
		bool flag = false;
		foreach (KeyValuePair<int, ICommandListProc> item in _commandListTable)
		{
			if (item.Value.Process() && item.Key == 0)
			{
				flag = true;
			}
		}
		if (flag)
		{
			return false;
		}
		if (commandPacks.Count == 0)
		{
			return false;
		}
		if (regulate.control.HasFlag(Regulate.Control.ClickNext))
		{
			nextInfo.isNext = false;
		}
		if (regulate.control.HasFlag(Regulate.Control.Skip))
		{
			nextInfo.isSkip = false;
			isSkip = false;
		}
		if (regulate.control.HasFlag(Regulate.Control.Auto))
		{
			isAuto = false;
		}
		if (regulate.control.HasFlag(Regulate.Control.AutoForce))
		{
			isAuto = true;
		}
		bool isCompleteDisplayText = nextInfo.isCompleteDisplayText;
		bool isNext = nextInfo.isNext;
		bool flag2 = nextInfo.isSkip;
		if (!isCompleteDisplayText)
		{
			if (isNext || isSkip || flag2)
			{
				textController.ForceCompleteDisplayText();
				nowCommandList.ProcessEnd();
			}
			return false;
		}
		autoWaitTimer = Mathf.Min(autoWaitTimer + Time.deltaTime, autoWaitTime);
		bool isProcessing = nowCommandList.Any();
		bool flag3 = textHash.Count > 0;
		bool flag4 = false;
		if (flag2 || isSkip)
		{
			flag4 = flag4 || true;
		}
		else if (isAuto && flag3)
		{
			flag4 |= autoWaitTimer >= autoWaitTime && !isProcessing;
		}
		nextMarker.SafeProc(delegate(Image p)
		{
			p.enabled = windowImage.enabled && (_isChoice || !isProcessing);
		});
		flag4 = flag4 || isNext;
		if (regulate.control.HasFlag(Regulate.Control.Next))
		{
			flag4 = false;
		}
		flag4 = flag4 || (!flag3 && !isProcessing);
		if (flag4)
		{
			nextMarker.SafeProc(delegate(Image p)
			{
				p.enabled = false;
			});
			currentCharaData.Clear();
			RequestNextLine();
		}
		nextInfo.isSkip = false;
		return flag4;
	}

	protected virtual void UpdateBefore()
	{
		bool flag = textController.MessageWindow.text.IsNullOrEmpty();
		if (msgWindowCanvas != null)
		{
			msgWindowCanvas.enabled = !flag;
		}
		if (windowImage != null)
		{
			windowImage.enabled = isWindowImage && !flag;
		}
		textController.FontColor = commandController.fontColor[fontColorKey ?? string.Empty];
	}

	protected virtual bool UpdateRegulate()
	{
		if (Manager.Scene.isReturnTitle || ExitDialog.isGameEnd)
		{
			return true;
		}
		if (!SingletonInitializer<Manager.Scene>.initialized || !SingletonInitializer<Game>.initialized)
		{
			return true;
		}
		if (Manager.Scene.IsNowLoading)
		{
			return true;
		}
		if (advScene != null && advScene.Map != null && advScene.Map.isMapLoading)
		{
			return true;
		}
		if (_isWait)
		{
			return true;
		}
		StartRun();
		foreach (CharaData value in commandController.Characters.Values)
		{
			if (!value.initialized)
			{
				return true;
			}
		}
		if (_isSceneFadeRegulate && Manager.Scene.IsFadeNow)
		{
			return true;
		}
		if (Manager.Scene.IsOverlap)
		{
			return true;
		}
		if (Mathf.Max(0, Manager.Scene.NowSceneNames.IndexOf("ADV")) > 0)
		{
			return true;
		}
		return false;
	}

	private static bool NameCheck(string text)
	{
		return new string[2] { "P", "H" }.Any((string head) => text.Check(ignoreCase: true, (string s) => head + s, SaveData.CharaData.Names) != -1);
	}

	private void OnceTagConvert(ref string key, ref string value)
	{
		string text = null;
		string text2 = key;
		if (!(text2 == "P"))
		{
			if (text2 == "H" && currentHeroine != null)
			{
				text = key + SaveData.CharaData.Names[(int)currentHeroine.nameType];
			}
		}
		else if (SingletonInitializer<Game>.initialized && Game.Player != null)
		{
			text = key + SaveData.CharaData.Names[(int)Game.Player.nameType];
		}
		if (text != null)
		{
			key = text;
			Replaces.TryGetValue(text, out value);
		}
	}

	protected virtual void Awake()
	{
		_regulate = new Regulate(this);
		if (backCamera != null)
		{
			backCameraBackup = new BackupPosRot(backCamera.transform);
		}
	}

	protected virtual void OnEnable()
	{
		Initialize();
	}

	protected virtual void OnDisable()
	{
		if (!ExitDialog.isGameEnd)
		{
			Release();
			MemberInit();
		}
	}

	protected virtual void Start()
	{
		faceIconParamDic = new Dictionary<string, ADVFaceIconData.Param>();
		CommonLib.GetAssetBundleNameListFromPath("adv/faceicon/list/", subdirCheck: true).ForEach(delegate(string file)
		{
			ADVFaceIconData[] allAssets = AssetBundleManager.LoadAllAsset(file, typeof(ADVFaceIconData)).GetAllAssets<ADVFaceIconData>();
			for (int i = 0; i < allAssets.Length; i++)
			{
				foreach (ADVFaceIconData.Param item in allAssets[i].param)
				{
					faceIconParamDic[item.key] = item;
				}
			}
			AssetBundleManager.UnloadAssetBundle(file, isUnloadForceRefCount: false);
		});
	}

	protected virtual void Update()
	{
		UpdateBefore();
		if (!UpdateRegulate())
		{
			KeyInput.Data data = KeyInput.TextNext(isOnWindow: true, isCameraLock);
			_nextInfo.Set(textController.IsCompleteDisplayText, data.isCheck, KeyInput.SkipButton);
			MessageWindowProc(_nextInfo);
		}
	}
}
