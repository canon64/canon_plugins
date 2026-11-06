using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using ADV.Commands.Base;
using AIChara;
using AIProject;
using AIProject.CaptionScript;
using AIProject.SaveData;
using AIProject.UI;
using Cinemachine;
using Illusion;
using Illusion.Extensions;
using Illusion.Game.Elements;
using Manager;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ADV;

[RequireComponent(typeof(CommandController))]
public class TextScenario : MonoBehaviour
{
	private class AlreadyReadInfo
	{
		private const string Path = "save";

		private const string FileName = "read.dat";

		private HashSet<int> read { get; } = new HashSet<int>();

		public AlreadyReadInfo()
		{
			read.Add(0);
			Load();
		}

		public bool Add(int i)
		{
			return read.Add(i);
		}

		public void Save()
		{
			Illusion.Utils.File.OpenWrite(UserData.Create("save") + "read.dat", isAppend: false, delegate(FileStream f)
			{
				Debug.Log("既読データセーブ".Coloring("yellow"));
				using BinaryWriter binaryWriter = new BinaryWriter(f);
				binaryWriter.Write(read.Count);
				foreach (int item in read)
				{
					binaryWriter.Write(item);
				}
			});
		}

		public bool Load()
		{
			return Illusion.Utils.File.OpenRead(UserData.Path + "save" + '/' + "read.dat", delegate(FileStream f)
			{
				Debug.Log("既読データロード".Coloring("yellow"));
				using BinaryReader binaryReader = new BinaryReader(f);
				int num = binaryReader.ReadInt32();
				for (int i = 0; i < num; i++)
				{
					read.Add(binaryReader.ReadInt32());
				}
			});
		}
	}

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
		private List<IVoice[]> _voiceList;

		private List<IMotion[]> _motionList;

		private List<IExpression[]> _expressionList;

		private Dictionary<int, string> _bundleVoices;

		public Dictionary<int, string> bundleVoices
		{
			[CompilerGenerated]
			get
			{
				return _bundleVoices;
			}
		}

		public bool isSkip { get; set; }

		public List<IVoice[]> voiceList
		{
			[CompilerGenerated]
			get
			{
				return _voiceList;
			}
		}

		public List<IMotion[]> motionList
		{
			[CompilerGenerated]
			get
			{
				return _motionList;
			}
		}

		public List<IExpression[]> expressionList
		{
			[CompilerGenerated]
			get
			{
				return _expressionList;
			}
		}

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

		public void Clear()
		{
			_voiceList = null;
			_motionList = null;
			_expressionList = null;
		}
	}

	public class ParamData
	{
		public bool isPlayer
		{
			[CompilerGenerated]
			get
			{
				return type == 0;
			}
		}

		public bool isHeroine
		{
			[CompilerGenerated]
			get
			{
				return MathfEx.IsRange(1, type, 2, isEqual: true);
			}
		}

		public int type { get; }

		public IParams param { get; }

		public CharaParams charaParam { get; }

		public Actor actor { get; }

		public PlayerActor playerActor { get; }

		public AgentActor agentActor { get; }

		public MerchantActor merchantActor { get; }

		public ICharacterInfo characterInfo { get; }

		public PlayerData playerData { get; }

		public AgentData agentData { get; }

		public MerchantData merchantData { get; }

		public bool isVisible
		{
			get
			{
				if (actor != null && type > 0)
				{
					return actor.IsVisible;
				}
				if (chaCtrl != null)
				{
					return chaCtrl.visibleAll;
				}
				return false;
			}
			set
			{
				if (actor != null && type > 0)
				{
					actor.IsVisible = value;
				}
				else if (chaCtrl != null)
				{
					chaCtrl.visibleAll = value;
				}
			}
		}

		public int sex { get; } = 1;

		public int voiceNo { get; }

		public float voicePitch { get; } = 1f;

		public ChaControl chaCtrl { get; }

		public Transform transform { get; }

		public ParamData(IParams param)
		{
			this.param = param;
			int num = -1;
			if (param is PlayerData)
			{
				num = 0;
			}
			else if (param is AgentData)
			{
				num = 1;
			}
			else if (param is MerchantData)
			{
				num = 2;
			}
			type = num;
			charaParam = param?.param as CharaParams;
			int? num2 = charaParam?.charaID;
			voiceNo = (num2.HasValue ? num2.Value : 0);
			actor = charaParam?.actor;
			if (actor != null)
			{
				chaCtrl = actor.ChaControl;
				transform = actor.Animation.Character.transform;
			}
			if (chaCtrl != null)
			{
				voicePitch = chaCtrl.fileParam.voicePitch;
			}
			switch (num)
			{
			case 0:
				playerData = param as PlayerData;
				characterInfo = playerData;
				playerActor = actor as PlayerActor;
				sex = playerData.Sex;
				break;
			case 1:
				agentData = param as AgentData;
				characterInfo = agentData;
				agentActor = actor as AgentActor;
				break;
			case 2:
				merchantData = param as MerchantData;
				characterInfo = merchantData;
				merchantActor = actor as MerchantActor;
				break;
			}
		}

		public ChaControl Create()
		{
			if (actor == null)
			{
				return null;
			}
			return type switch
			{
				0 => (sex != 0) ? Singleton<Character>.Instance.CreateChara(1, actor.gameObject, 0) : Singleton<Character>.Instance.CreateChara(0, actor.gameObject, 0), 
				1 => Singleton<Character>.Instance.CreateChara(1, actor.gameObject, 0), 
				2 => Singleton<Character>.Instance.CreateChara(1, actor.gameObject, 0), 
				_ => null, 
			};
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
			if (chaCtrl == null || audio == null)
			{
				return false;
			}
			return true;
		}
	}

	protected class NextInfo
	{
		public bool isCompleteDisplayText;

		public bool isNext;

		public bool isSkip;

		public NextInfo(bool isCompleteDisplayText, bool isNext, bool isSkip)
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

		public List<RootData> FileList
		{
			[CompilerGenerated]
			get
			{
				return fileList;
			}
		}

		public List<RootData> RootList
		{
			[CompilerGenerated]
			get
			{
				return rootList;
			}
		}

		public void Clear()
		{
			fileList.Clear();
			rootList.Clear();
		}
	}

	private static AlreadyReadInfo readInfo;

	[SerializeField]
	private Captions _captions;

	private bool _clicked;

	private IDisposable _choiceCommandDis;

	[SerializeField]
	private ADVUI _advUI;

	[SerializeField]
	private Regulate _regulate;

	[SerializeField]
	private Info _info = new Info();

	[SerializeField]
	private Camera _AdvCamera;

	private CinemachineVirtualCamera _virtualCamera;

	private CrossFade _crossFade;

	public CurrentCharaData currentCharaData = new CurrentCharaData();

	[SerializeField]
	private OpenData _openData = new OpenData();

	private CommandController _commandController;

	private ADVScene _advScene;

	[SerializeField]
	private ADVFade advFade;

	[SerializeField]
	private Camera backCamera;

	[SerializeField]
	private Transform characters;

	[SerializeField]
	private Image filterImage;

	[SerializeField]
	protected BoolReactiveProperty _isSkip = new BoolReactiveProperty(initialValue: false);

	[SerializeField]
	protected BoolReactiveProperty _isAuto = new BoolReactiveProperty(initialValue: false);

	[SerializeField]
	protected bool _isWait;

	[Header("Debug表示")]
	[SerializeField]
	protected int currentLine;

	[SerializeField]
	protected float autoWaitTimer;

	[SerializeField]
	protected float autoWaitTime = 3f;

	[SerializeField]
	protected bool _isText;

	[SerializeField]
	private bool _isSceneFadeRegulate = true;

	[SerializeField]
	private bool _isStartRun;

	private Illusion.Game.Elements.Single _single = new Illusion.Game.Elements.Single();

	private IDisposable voicePlayDis;

	private List<LoopVoicePack> _loopVoiceList = new List<LoopVoicePack>();

	public const int VOICE_SET_NO = 1;

	public bool Fading
	{
		[CompilerGenerated]
		get
		{
			return MapUIContainer.FadeCanvas.IsFading;
		}
	}

	public CaptionSystem captionSystem
	{
		[CompilerGenerated]
		get
		{
			return captions.CaptionSystem;
		}
	}

	public Captions captions
	{
		[CompilerGenerated]
		get
		{
			return _captions ?? (_captions = Singleton<Manager.ADV>.Instance.Captions);
		}
	}

	private Manager.Input input
	{
		[CompilerGenerated]
		get
		{
			return Singleton<Manager.Input>.Instance;
		}
	}

	public ADVUI advUI
	{
		[CompilerGenerated]
		get
		{
			return _advUI;
		}
	}

	public Regulate regulate
	{
		[CompilerGenerated]
		get
		{
			return _regulate;
		}
	}

	public Info info
	{
		[CompilerGenerated]
		get
		{
			return _info;
		}
	}

	public Camera AdvCamera
	{
		get
		{
			return _AdvCamera;
		}
		set
		{
			_AdvCamera = value;
		}
	}

	public CinemachineVirtualCamera virtualCamera
	{
		get
		{
			return _virtualCamera;
		}
		set
		{
			_virtualCamera = value;
		}
	}

	public CrossFade crossFade
	{
		[CompilerGenerated]
		get
		{
			return _crossFade;
		}
	}

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

	public bool isSkip
	{
		get
		{
			return _isSkip.Value;
		}
		set
		{
			_isSkip.Value = value;
		}
	}

	public bool isAuto
	{
		get
		{
			return _isAuto.Value;
		}
		set
		{
			_isAuto.Value = value;
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

	public OpenData openData
	{
		[CompilerGenerated]
		get
		{
			return _openData;
		}
	}

	public string LoadBundleName
	{
		get
		{
			return _openData.bundle;
		}
		set
		{
			_openData.bundle = value;
		}
	}

	public string LoadAssetName
	{
		get
		{
			return _openData.asset;
		}
		set
		{
			_openData.asset = value;
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

	public bool isCameraLock => true;

	public bool isBackGroundCommanding { get; set; }

	public CommandController commandController => this.GetComponentCache(ref _commandController);

	public ADVScene advScene => this.GetComponentCache(ref _advScene);

	public List<ScenarioData.Param> CommandPacks => commandPacks;

	public bool isBackGroundCommandProcessing => backCommandList.Count > 0;

	public Image FilterImage => filterImage;

	public bool isFadeAllEnd
	{
		get
		{
			if (!Singleton<Manager.Scene>.IsInstance() || advScene == null || advScene.AdvFade == null || _crossFade == null)
			{
				return true;
			}
			return !Singleton<Manager.Scene>.Instance.IsFadeNow && advScene.AdvFade.IsEnd && _crossFade.isEnd;
		}
	}

	protected List<ScenarioData.Param> commandPacks { get; } = new List<ScenarioData.Param>();

	private FileOpen fileOpenData { get; } = new FileOpen();

	protected bool isRequestLine { get; set; }

	protected HashSet<int> textHash { get; } = new HashSet<int>();

	public Dictionary<string, ValData> Vars
	{
		[CompilerGenerated]
		get
		{
			return vars;
		}
	}

	private Dictionary<string, ValData> vars { get; } = new Dictionary<string, ValData>();

	public Dictionary<string, string> Replaces
	{
		[CompilerGenerated]
		get
		{
			return replaces;
		}
	}

	private Dictionary<string, string> replaces { get; } = new Dictionary<string, string>();

	public IPack package { get; private set; }

	public ParamData player { get; private set; }

	public List<ParamData> heroineList { get; private set; }

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

	private BackupPosRot backCameraBackup { get; set; }

	private CommandList nowCommandList => _commandController.NowCommandList;

	private CommandList backCommandList => _commandController.BackGroundCommandList;

	public IObservable<Unit> OnInitializedAsync
	{
		[CompilerGenerated]
		get
		{
			return _single;
		}
	}

	public List<LoopVoicePack> loopVoiceList => _loopVoiceList;

	public event Action<string, string, IReadOnlyCollection<IVoice[]>> TextLog;

	public event Action VisibleLog;

	public static void LoadReadInfo()
	{
		if (readInfo == null)
		{
			readInfo = new AlreadyReadInfo();
		}
	}

	public static void SaveReadInfo()
	{
		if (readInfo != null)
		{
			readInfo.Save();
		}
	}

	public void TextLogCall(ADV.Commands.Base.Text.Data data, IReadOnlyCollection<IVoice[]> voices)
	{
		if (this.TextLog != null)
		{
			this.TextLog(data.name, data.text, voices);
		}
	}

	public void FadeIn(float duration, bool ignoreTimeScale = true)
	{
		MapUIContainer.FadeCanvas.StartFade(FadeCanvas.PanelType.Blackout, FadeType.In, duration, ignoreTimeScale);
	}

	public void FadeOut(float duration, bool ignoreTimeScale = true)
	{
		MapUIContainer.FadeCanvas.StartFade(FadeCanvas.PanelType.Blackout, FadeType.Out, duration, ignoreTimeScale);
	}

	public string ChoiceON(string title, CommCommandList.CommandInfo[] options)
	{
		string text = MapUIContainer.ChoiceUI.Label.text;
		MapUIContainer.SetActiveChoiceUI(active: true, title);
		MapUIContainer.ChoiceUI.Refresh(options, MapUIContainer.ChoiceUI.CanvasGroup);
		return text;
	}

	public void ChoiceOFF(string label)
	{
		if (_choiceCommandDis != null)
		{
			_choiceCommandDis.Dispose();
		}
		_choiceCommandDis = null;
		if (!label.IsNullOrEmpty())
		{
			_choiceCommandDis = MapUIContainer.ChoiceUI.OnCompletedStopAsObservable().Take(1).Subscribe(delegate
			{
				MapUIContainer.ChoiceUI.Label.text = label;
			});
		}
		MapUIContainer.ChoiceUI.Visibled = false;
	}

	public void CrossFadeStart()
	{
		if (!(_crossFade == null) && isFadeAllEnd)
		{
			_crossFade.FadeStart(info.anime.play.crossFadeTime);
		}
	}

	public void SetPackage(IPack package)
	{
		this.package = package;
		SetCharacters(package?.param);
	}

	public void SetCharacters(IParams[] param)
	{
		if (param == null)
		{
			return;
		}
		ParamData[] source = param.Select((IParams p) => new ParamData(p)).ToArray();
		heroineList = source.Where((ParamData p) => p.isHeroine).ToList();
		player = source.FirstOrDefault((ParamData p) => p.isPlayer);
		CharaData.MotionReserver motionReserver = null;
		foreach (var item in heroineList.Select((ParamData data, int index) => new { data, index }))
		{
			commandController.AddChara(item.index, new CharaData(item.data, this, motionReserver));
		}
		if (player != null)
		{
			commandController.AddChara(-1, new CharaData(player, this, motionReserver));
		}
		if (commandController.Characters.Any())
		{
			ChangeCurrentChara(commandController.Characters.First().Key);
		}
	}

	public bool ChangeCurrentChara(int no)
	{
		currentChara = commandController.GetChara(no);
		return currentChara != null;
	}

	public string ReplaceVars(string arg)
	{
		ValData value;
		return vars.TryGetValue(arg, out value) ? value.o.ToString() : arg;
	}

	public string ReplaceText(string text)
	{
		StringBuilder stringBuilder = new StringBuilder();
		int num = 0;
		try
		{
			foreach (Match item in Regex.Matches(text, "\\[.*?\\]"))
			{
				if (item.Success)
				{
					string key = string.Empty;
					try
					{
						key = Regex.Replace(item.Value, "\\[|\\]", string.Empty);
					}
					catch (Exception)
					{
						Debug.LogErrorFormat("Text {0} Regex.Replace Error\n{1}", item.Value, text);
					}
					if (replaces.TryGetValue(key, out var value))
					{
						if (!value.IsNullOrEmpty())
						{
							value = ReplaceText(value);
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
				else
				{
					Debug.LogError("Text Match.Failed\n" + text);
				}
			}
		}
		catch (Exception)
		{
			Debug.LogError("Text Regex.Match Error\n" + text);
		}
		stringBuilder.Append(text.Substring(num, text.Length - num));
		return stringBuilder.ToString();
	}

	public virtual void Initialize()
	{
		commandPacks.Clear();
		fileOpenData.Clear();
		_regulate.SetRegulate((Regulate.Control)0);
		if (backCameraBackup != null && backCamera != null)
		{
			backCameraBackup.Set(backCamera.transform);
		}
		currentLine = 0;
		autoWaitTimer = 0f;
		commandController.Initialize();
		captionSystem.Clear();
		vars.Clear();
		replaces.Clear();
		advFade.SafeProcObject(delegate(ADVFade p)
		{
			p.Initialize();
		});
		_single.Done();
		LoadReadInfo();
	}

	protected void MemberInit()
	{
		isSkip = false;
		isAuto = false;
		_isWait = false;
		_isSceneFadeRegulate = true;
		_isStartRun = false;
		textHash.Clear();
	}

	public virtual void Release()
	{
		if (voicePlayDis != null)
		{
			voicePlayDis.Dispose();
		}
		voicePlayDis = null;
		if (_choiceCommandDis != null)
		{
			_choiceCommandDis.Dispose();
		}
		_choiceCommandDis = null;
		loopVoiceList.ForEach(delegate(LoopVoicePack p)
		{
			if (p.audio != null)
			{
				UnityEngine.Object.Destroy(p.audio.gameObject);
			}
		});
		loopVoiceList.Clear();
		_single = new Illusion.Game.Elements.Single();
		commandController.Release();
		AdvCamera = null;
		info.audio.is2D = false;
		info.audio.isNotMoveMouth = false;
		if (!Singleton<Game>.IsInstance())
		{
			SaveReadInfo();
		}
	}

	public void CommandAdd(bool isNext, int line, bool multi, Command command, string[] args)
	{
		List<string> list = new List<string>();
		list.Add("0");
		list.Add(multi.ToString());
		list.Add(command.ToString());
		list.AddRange(args ?? new string[1] { string.Empty });
		ScenarioData.Param item = new ScenarioData.Param(list.ToArray());
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
			captionSystem.Clear();
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
		openData.Load(bundle, asset);
		if (!isClear)
		{
			commandPacks.InsertRange(currentLine, openData.data.list);
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
				bundle,
				asset,
				bool.FalseString,
				bool.TrueString,
				bool.TrueString
			};
			currentLine = 0;
			commandPacks.Clear();
			CommandAdd(isNext: false, currentLine++, multi: false, Command.Open, args);
			commandPacks.AddRange(openData.data.list);
		}
		if (isNext)
		{
			RequestNextLine();
		}
		openData.ClearData();
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
			Debug.LogWarningFormat("not jump : {0}\nLine : {1}", jump, localLine);
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

	public virtual void ConfigProc()
	{
		if (Config.initialized)
		{
			autoWaitTime = Config.GameData.AutoWaitTime;
		}
	}

	public void BackGroundCommandProcessEnd()
	{
		backCommandList.ProcessEnd();
	}

	public void VoicePlay(List<IVoice[]> voices, Action onChange, Action onEnd)
	{
		if (voicePlayDis != null)
		{
			voicePlayDis.Dispose();
		}
		voicePlayDis = null;
		Singleton<Manager.Voice>.Instance.StopAll(isLoopStop: false);
		if (voices == null)
		{
			return;
		}
		if (_loopVoiceList.Any())
		{
			HashSet<int> hashSet = new HashSet<int>();
			foreach (IVoice[] voice2 in voices)
			{
				IVoice[] array = voice2;
				foreach (IVoice voice in array)
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
			onChange.Call();
		}, (Action)delegate
		{
			List<LoopVoicePack> list = new List<LoopVoicePack>();
			foreach (LoopVoicePack loopVoice2 in _loopVoiceList)
			{
				if (!loopVoice2.Set() || loopVoice2.audio == null)
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
			onEnd.Call();
		});
	}

	private IEnumerator VoicePlayCoroutine(IObserver<Unit> observer, List<IVoice[]> voiceList)
	{
		foreach (IVoice[] voice in voiceList)
		{
			IVoice[] array = voice;
			foreach (IVoice voice2 in array)
			{
				voice2.Play();
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
		_isStartRun = true;
		int num = 0;
		if (!_transferList.IsNullOrEmpty())
		{
			Program.Transfer transfer = _transferList[0];
			if (transfer.param.Command == Command.SceneFadeRegulate)
			{
				_isSceneFadeRegulate = bool.Parse(transfer.param.Args[0]);
			}
			foreach (Program.Transfer transfer2 in _transferList)
			{
				int line = ((transfer2.line != -1) ? transfer2.line : num);
				CommandAdd(isNext: false, line, transfer2.param.Multi, transfer2.param.Command, transfer2.param.Args);
				num++;
			}
		}
		if (!LoadBundleName.IsNullOrEmpty() && !LoadAssetName.IsNullOrEmpty())
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
		else if (_openData.HasData)
		{
			commandPacks.AddRange(openData.data.list);
			RequestNextLine();
			openData.ClearData();
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
		bool isMulti = false;
		ScenarioData.Param pack = null;
		textHash.Clear();
		autoWaitTimer = 0f;
		while (true)
		{
			if (commandController.LoadingCharaList.Any())
			{
				yield return null;
				continue;
			}
			isMulti = false;
			if (currentLine < commandPacks.Count)
			{
				pack = commandPacks[currentLine++];
				if (pack.Command == Command.Log)
				{
					isMulti = true;
				}
				else
				{
					isMulti = ((isBackGroundCommanding && pack.Command != Command.Task) ? backCommandList.Add(pack, CurrentLine) : nowCommandList.Add(pack, CurrentLine));
					Command command = pack.Command;
					if (command == Command.Text)
					{
						textHash.Add(pack.Hash);
					}
				}
			}
			if (!isMulti)
			{
				break;
			}
		}
		isRequestLine = false;
		foreach (int item in textHash)
		{
			if (readInfo.Add(item) && isSkip && Config.GameData.ReadSkip)
			{
				isSkip = false;
			}
		}
	}

	protected void ADVCameraSetting()
	{
	}

	protected bool MessageWindowProc(NextInfo nextInfo)
	{
		if (isRequestLine)
		{
			return false;
		}
		backCommandList.Process();
		if (nowCommandList.Process())
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
		bool flag = nextInfo.isSkip;
		if (!isCompleteDisplayText)
		{
			if (isNext || isSkip || flag)
			{
				captionSystem.ForceCompleteDisplayText();
				nowCommandList.ProcessEnd();
			}
			return false;
		}
		autoWaitTimer = Mathf.Min(autoWaitTimer + Time.deltaTime, autoWaitTime);
		bool flag2 = nowCommandList.Count > 0;
		bool flag3 = textHash.Count > 0;
		bool flag4 = false;
		if (flag || isSkip)
		{
			flag4 = (byte)((flag4 ? 1u : 0u) | 1u) != 0;
		}
		else if (isAuto && flag3)
		{
			flag4 |= autoWaitTimer >= autoWaitTime && !flag2;
		}
		flag4 = flag4 || isNext;
		if (regulate.control.HasFlag(Regulate.Control.Next))
		{
			flag4 = false;
		}
		flag4 = flag4 || (!flag3 && !flag2);
		if (flag4)
		{
			currentCharaData.Clear();
			RequestNextLine();
		}
		return flag4;
	}

	protected virtual void UpdateBefore()
	{
	}

	protected virtual bool UpdateRegulate()
	{
		if (Manager.Scene.isReturnTitle || Manager.Scene.isGameEnd)
		{
			return true;
		}
		if (!Singleton<Manager.Scene>.IsInstance() || !Singleton<Game>.IsInstance())
		{
			return true;
		}
		if (Singleton<Manager.Scene>.Instance.IsNowLoading)
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
		if (_isSceneFadeRegulate && Singleton<Manager.Scene>.Instance.sceneFade.IsFadeNow)
		{
			return true;
		}
		if (Singleton<Manager.Scene>.Instance.IsOverlap)
		{
			return true;
		}
		if (Mathf.Max(0, Singleton<Manager.Scene>.Instance.NowSceneNames.IndexOf("ADV")) > 0)
		{
			return true;
		}
		if (Singleton<Game>.Instance.Config != null || Singleton<Game>.Instance.Dialog != null || Singleton<Game>.Instance.ExitScene != null || Singleton<Game>.Instance.MapShortcutUI != null || Fading)
		{
			return true;
		}
		if (!captionSystem.Visible)
		{
			return true;
		}
		return false;
	}

	protected virtual void Awake()
	{
		_regulate = new Regulate(this);
		if (backCamera != null)
		{
			backCameraBackup = new BackupPosRot(backCamera.transform);
		}
		(from ped in captionSystem.RaycasterImage.OnPointerClickAsObservable()
			where base.isActiveAndEnabled
			where ped != null
			select ped).Subscribe(delegate(PointerEventData ped)
		{
			bool flag = false;
			bool flag2 = false;
			switch (ped.button)
			{
			case PointerEventData.InputButton.Left:
				flag = true;
				break;
			case PointerEventData.InputButton.Right:
				flag2 = true;
				break;
			}
			if ((flag || flag2) && !captionSystem.Visible)
			{
				captionSystem.Visible = true;
			}
			else if (flag)
			{
				_clicked = true;
			}
		});
		if (!(advUI != null))
		{
			return;
		}
		if (advUI.skip != null)
		{
			_isSkip.Where((bool isOn) => isOn != advUI.skip.isOn).Subscribe(delegate(bool isOn)
			{
				advUI.useSE = false;
				advUI.skip.isOn = isOn;
				advUI.useSE = true;
			});
			(from isOn in advUI.skip.OnValueChangedAsObservable()
				where isOn != isSkip
				select isOn).Subscribe(delegate(bool isOn)
			{
				isSkip = isOn;
				advUI.PlaySE(SoundPack.SystemSE.OK_S);
			});
		}
		if (advUI.auto != null)
		{
			_isAuto.Where((bool isOn) => isOn != advUI.auto.isOn).Subscribe(delegate(bool isOn)
			{
				advUI.useSE = false;
				advUI.auto.isOn = isOn;
				advUI.useSE = true;
			});
			(from isOn in advUI.auto.OnValueChangedAsObservable()
				where isOn != isAuto
				select isOn).Subscribe(delegate(bool isOn)
			{
				isAuto = isOn;
				advUI.PlaySE(SoundPack.SystemSE.OK_S);
			});
		}
		if (advUI.log != null)
		{
			advUI.log.OnClickAsObservable().Subscribe(delegate
			{
				if (this.VisibleLog != null)
				{
					this.VisibleLog();
				}
				advUI.PlaySE(SoundPack.SystemSE.OK_S);
			});
		}
		if (advUI.close != null)
		{
			(from _ in advUI.close.OnClickAsObservable()
				where captionSystem.Visible
				select _).Subscribe(delegate
			{
				captionSystem.Visible = false;
				advUI.PlaySE(SoundPack.SystemSE.Cancel);
			});
		}
	}

	protected virtual void OnEnable()
	{
		Initialize();
	}

	protected virtual void OnDisable()
	{
		if (!Manager.Scene.isGameEnd)
		{
			Release();
			MemberInit();
		}
	}

	protected virtual void Update()
	{
		UpdateBefore();
		if (!UpdateRegulate())
		{
			bool isNext = input.IsPressedSubmit() || _clicked;
			bool flag = input.IsDown(KeyCode.LeftControl) || input.IsDown(KeyCode.RightControl);
			_clicked = false;
			MessageWindowProc(new NextInfo(captionSystem.IsCompleteDisplayText, isNext, flag));
		}
	}
}
