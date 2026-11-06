using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Illusion.Extensions;
using Manager;
using ReMotion;
using UniRx;
using UniRx.Toolkit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEx.Misc;

namespace AIProject.UI;

public class CommCommandList : MenuUIBehaviour
{
	public class NodePool : UniRx.Toolkit.ObjectPool<ScrollCylinderNode>
	{
		public GameObject Source { get; set; }

		protected override ScrollCylinderNode CreateInstance()
		{
			return UnityEngine.Object.Instantiate(Source).GetComponent<ScrollCylinderNode>();
		}
	}

	[Serializable]
	public class CommandInfo
	{
		public string Name { get; set; }

		public Func<bool> Condition { get; set; }

		public Action<int> Event { get; set; }

		public bool IsSpecial { get; set; }

		public Func<float> Timer { get; set; }

		public int TimerRedZoneBorder { get; set; }

		public CommandInfo()
		{
		}

		public CommandInfo(string name)
		{
			Name = name;
		}

		public CommandInfo(string name, Func<bool> cond, Action<int> action)
		{
			Name = name;
			Condition = cond;
			Event = action;
		}
	}

	private IDisposable _fadeSubscriber;

	private MenuUIBehaviour[] _menuUIElements;

	private bool _visibled = true;

	private IDisposable _refreshDisposable;

	[SerializeField]
	private IntReactiveProperty _selectedID = new IntReactiveProperty(0);

	private IConnectableObservable<int> _selectIDChange;

	[SerializeField]
	private ScrollCylinder _scrollCylinder;

	[SerializeField]
	private Text _label;

	[SerializeField]
	private RectTransform _guideRoot;

	[SerializeField]
	private RectTransform _poolRoot;

	[SerializeField]
	private ScrollCylinderNode _node;

	[SerializeField]
	private GuideOption _guideNode;

	[SerializeField]
	private Sprite _normalSprite;

	[SerializeField]
	private Sprite _selectedSprite;

	[SerializeField]
	private Sprite _specialSelectedSprite;

	[SerializeField]
	private Color _normalTimerColor = Color.white;

	[SerializeField]
	private Color _redZoneTimerColor = Color.white;

	private CommandInfo _selectedCommand;

	private CommCommandOption _selectedOption;

	private Action _cancelEvent;

	private List<CommandInfo> _commandList = new List<CommandInfo>();

	private CommandInfo[] _options;

	private NodePool _nodePool;

	[SerializeField]
	private CanvasGroup _panelCanvasGroup;

	private List<ScrollCylinderNode> _nodeList = new List<ScrollCylinderNode>();

	private List<CommCommandOption> _optionList = new List<CommCommandOption>();

	private GuideOption _okGuide;

	private GuideOption _cancelGuide;

	private GuideOption _scrollGuide;

	private Subject<Unit> _completeDoStop;

	public Action OnOpened { get; set; }

	private MenuUIBehaviour[] MenuUIElements
	{
		[CompilerGenerated]
		get
		{
			MenuUIBehaviour[] array = _menuUIElements;
			if (array == null)
			{
				MenuUIBehaviour[] obj = new MenuUIBehaviour[1] { this };
				MenuUIBehaviour[] array2 = obj;
				_menuUIElements = obj;
				array = array2;
			}
			return array;
		}
	}

	public bool Visibled
	{
		get
		{
			return _visibled;
		}
		set
		{
			if (_visibled != value)
			{
				_visibled = value;
				if (_fadeSubscriber != null)
				{
					_fadeSubscriber.Dispose();
				}
				IEnumerator coroutine = ((!value) ? Vanish(CanvasGroup) : Display(CanvasGroup));
				_fadeSubscriber = Observable.FromCoroutine(() => coroutine).Subscribe(delegate
				{
				}, delegate(Exception ex)
				{
					Debug.LogException(ex);
				});
			}
		}
	}

	public Text Label
	{
		[CompilerGenerated]
		get
		{
			return _label;
		}
	}

	public Action CancelEvent
	{
		get
		{
			return _cancelEvent;
		}
		set
		{
			if (!(_cancelEvent == value))
			{
				_cancelEvent = value;
				_cancelGuide.SetActive(value != null);
			}
		}
	}

	public CommandInfo[] Options
	{
		get
		{
			return _options;
		}
		set
		{
			_options = value.ToArray();
			_commandList.Clear();
			CommandInfo[] options = _options;
			foreach (CommandInfo commandInfo in options)
			{
				bool? flag = commandInfo.Condition?.Invoke();
				if (!flag.HasValue || flag.Value)
				{
					_commandList.Add(commandInfo);
				}
			}
		}
	}

	public int ID { get; set; }

	public CanvasGroup CanvasGroup { get; private set; }

	private void SetActiveControl(bool isActive)
	{
		IEnumerator coroutine = ((!isActive) ? Close() : Open());
		if (_fadeSubscriber != null)
		{
			_fadeSubscriber.Dispose();
		}
		_fadeSubscriber = Observable.FromCoroutine(() => coroutine).Subscribe(delegate
		{
		}, delegate(Exception ex)
		{
			Debug.LogException(ex);
		});
	}

	public IObservable<int> OnSelectIDChagnedAsObservable()
	{
		if (_selectIDChange == null)
		{
			_selectIDChange = _selectedID.TakeUntilDestroy(base.gameObject).Publish();
			_selectIDChange.Connect();
		}
		return _selectIDChange;
	}

	protected override void Awake()
	{
		base.Awake();
		CanvasGroup = GetComponentInChildren<CanvasGroup>(includeInactive: true);
	}

	protected override void OnBeforeStart()
	{
		if (!Application.isPlaying)
		{
			return;
		}
		_scrollCylinder.enableInternalScroll = false;
		OnActiveChangedAsObservable().Subscribe(delegate(bool x)
		{
			SetActiveControl(x);
		});
		OnSelectIDChagnedAsObservable().Subscribe(delegate(int x)
		{
			_selectedCommand = _commandList.GetElement(x);
			if (_selectedOption != null)
			{
				_selectedOption.Sprite = _normalSprite;
			}
			_selectedOption = _optionList.GetElement(x);
			if (_selectedOption != null)
			{
				if (_selectedCommand.IsSpecial)
				{
					_selectedOption.Sprite = _specialSelectedSprite;
				}
				else
				{
					_selectedOption.Sprite = _selectedSprite;
				}
			}
			ScrollCylinderNode element = _nodeList.GetElement(x);
			if (element != null)
			{
				_scrollCylinder.SetTarget(element);
			}
		});
		IConnectableObservable<long> connectableObservable = Observable.EveryUpdate().TakeUntilDestroy(base.gameObject).Publish();
		connectableObservable.Connect();
		connectableObservable.Where((long _) => base.isActiveAndEnabled).Subscribe(delegate
		{
			OnUpdate();
		});
		ActionIDDownCommand actionIDDownCommand = new ActionIDDownCommand();
		actionIDDownCommand.ActionID = ActionID.Cancel;
		ActionIDDownCommand actionIDDownCommand2 = actionIDDownCommand;
		actionIDDownCommand2.TriggerEvent.AddListener(delegate
		{
			OnInputCancel();
		});
		_actionCommands.Add(actionIDDownCommand2);
		actionIDDownCommand = new ActionIDDownCommand();
		actionIDDownCommand.ActionID = ActionID.MouseRight;
		ActionIDDownCommand actionIDDownCommand3 = actionIDDownCommand;
		actionIDDownCommand3.TriggerEvent.AddListener(delegate
		{
			OnInputCancel();
		});
		_actionCommands.Add(actionIDDownCommand3);
		_nodePool = new NodePool
		{
			Source = _node.gameObject
		};
		Manager.Resources.ItemIconTables iconTable = Singleton<Manager.Resources>.Instance.itemIconTables;
		CommonDefine commonDefine = Singleton<Manager.Resources>.Instance.CommonDefine;
		Singleton<Manager.Resources>.Instance.LoadMapResourceStream.Subscribe(delegate
		{
			_okGuide = UnityEngine.Object.Instantiate(_guideNode, _guideRoot, worldPositionStays: false);
			_okGuide.Icon = iconTable.InputIconTable[commonDefine.Icon.GuideOKID];
			_okGuide.CaptionText = "決定";
			_okGuide.SetActive(isActive: true);
			_cancelGuide = UnityEngine.Object.Instantiate(_guideNode, _guideRoot, worldPositionStays: false);
			_cancelGuide.Icon = iconTable.InputIconTable[commonDefine.Icon.GuideCancelID];
			_cancelGuide.CaptionText = "もどる";
			_cancelGuide.SetActive(isActive: true);
			_scrollGuide = UnityEngine.Object.Instantiate(_guideNode, _guideRoot, worldPositionStays: false);
			_scrollGuide.Icon = iconTable.InputIconTable[commonDefine.Icon.GuideScrollID];
			_scrollGuide.CaptionText = "項目選択";
			_scrollGuide.SetActive(isActive: true);
		});
	}

	private void OnUpdate()
	{
		if (_guideRoot != null)
		{
			_guideRoot.gameObject.SetActiveIfDifferent(Config.GameData.ActionGuide);
		}
		if (!Singleton<Game>.IsInstance() || !Singleton<Manager.Input>.IsInstance())
		{
			return;
		}
		Manager.Input instance = Singleton<Manager.Input>.Instance;
		Game instance2 = Singleton<Game>.Instance;
		if (instance2.ExitScene != null || instance2.Dialog != null || instance2.Config != null || instance2.MapShortcutUI != null || !base.EnabledInput || base.FocusLevel != Singleton<Manager.Input>.Instance.FocusLevel)
		{
			return;
		}
		float num = Singleton<Manager.Input>.Instance.ScrollValue();
		if (num < 0f)
		{
			int num2 = _selectedID.Value + 1;
			if (num2 < _nodeList.Count)
			{
				_selectedID.Value = num2;
				Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Select);
			}
		}
		else if (num > 0f)
		{
			int num3 = _selectedID.Value - 1;
			if (num3 >= 0)
			{
				_selectedID.Value = num3;
				Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Select);
			}
		}
		if (!(_selectedOption != null) || _selectedCommand == null)
		{
			return;
		}
		foreach (CommCommandOption option in _optionList)
		{
			if (option == _selectedOption)
			{
				option.SetActiveTimer(_selectedCommand.Timer != null);
				if (option.ActiveTimer)
				{
					float num4 = _selectedCommand.Timer();
					Color timeColor = ((!(num4 > (float)_selectedCommand.TimerRedZoneBorder)) ? _redZoneTimerColor : _normalTimerColor);
					option.SetTimeColor(timeColor);
					option.SetTime(num4);
				}
			}
			else
			{
				option.SetActiveTimer(active: false);
			}
		}
	}

	private IEnumerator Open()
	{
		if (_enabledInput.Value)
		{
			_enabledInput.Value = false;
		}
		_visibled = true;
		Singleton<Map>.Instance.Player.SetScheduledInteractionState(isEnabled: false);
		Singleton<Map>.Instance.Player.ReleaseInteraction();
		yield return DisplayElement(CanvasGroup);
		CanvasGroup.blocksRaycasts = true;
		Singleton<Manager.Input>.Instance.MenuElements = MenuUIElements;
		Singleton<Manager.Input>.Instance.FocusLevel = 0;
		yield return null;
		_enabledInput.Value = true;
		OnOpened?.Invoke();
		OnOpened = null;
	}

	private IEnumerator DisplayElement(CanvasGroup canvasGroup)
	{
		Queue<ScrollCylinderNode> queue = _nodeList.ToQueue();
		while (queue.Count > 0)
		{
			ScrollCylinderNode scrollCylinderNode = queue.Dequeue();
			scrollCylinderNode.transform.SetParent(_poolRoot, worldPositionStays: false);
			_nodePool.Return(scrollCylinderNode);
		}
		_nodeList.Clear();
		_optionList.Clear();
		_scrollCylinder.ClearBlank();
		_scrollCylinder.BlankSet(_node);
		for (int i = 0; i < _commandList.Count; i++)
		{
			int optionID = i;
			ScrollCylinderNode scrollCylinderNode2 = _nodePool.Rent();
			scrollCylinderNode2.transform.SetParent(_scrollCylinder.Contents.transform);
			CommCommandOption component = scrollCylinderNode2.GetComponent<CommCommandOption>();
			component.OnClick = delegate
			{
				OnInputSubmitFromButton(component, optionID);
			};
			component.LabelText = _commandList[i].Name;
			component.Sprite = _normalSprite;
			_nodeList.Add(scrollCylinderNode2);
			_optionList.Add(component);
		}
		_scrollCylinder.ListNodeSet(_nodeList);
		_selectedID.SetValueAndForceNotify(0);
		IConnectableObservable<TimeInterval<float>> stream = ObservableEasing.Linear(1f, ignoreTimeScale: true).FrameTimeInterval(ignoreTimeScale: true).Publish();
		stream.Connect();
		stream.Subscribe(delegate(TimeInterval<float> x)
		{
			canvasGroup.alpha = x.Value;
		});
		yield return stream.ToYieldInstruction();
		canvasGroup.blocksRaycasts = true;
	}

	private IEnumerator Close()
	{
		_enabledInput.Value = false;
		_visibled = false;
		Singleton<Manager.Input>.Instance.ClearMenuElements();
		Singleton<Manager.Input>.Instance.FocusLevel = -1;
		Singleton<Manager.Input>.Instance.ReserveState(Manager.Input.ValidType.Action);
		Singleton<Map>.Instance.Player.SetScheduledInteractionState(isEnabled: true);
		CanvasGroup.blocksRaycasts = false;
		Singleton<Map>.Instance.Player.ReleaseInteraction();
		if (Singleton<Map>.Instance.Player.CurrentInteractionState)
		{
			MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.InvokeAcception);
		}
		else
		{
			MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.None);
		}
		Singleton<Manager.Input>.Instance.SetupState();
		yield return VanishElement(CanvasGroup);
		if (_completeDoStop != null)
		{
			_completeDoStop.OnNext(Unit.Default);
		}
	}

	private IEnumerator VanishElement(CanvasGroup canvasGroup)
	{
		canvasGroup.blocksRaycasts = false;
		float startAlpha = canvasGroup.alpha;
		IConnectableObservable<TimeInterval<float>> stream = ObservableEasing.Linear(0.3f, ignoreTimeScale: true).FrameTimeInterval(ignoreTimeScale: true).Publish();
		stream.Connect();
		stream.Subscribe(delegate(TimeInterval<float> x)
		{
			canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, x.Value);
		});
		yield return stream.ToYieldInstruction();
	}

	private IEnumerator Display(CanvasGroup canvasGroup)
	{
		if (_enabledInput.Value)
		{
			_enabledInput.Value = false;
		}
		float startAlpha = canvasGroup.alpha;
		IObservable<TimeInterval<float>> stream = ObservableEasing.Linear(0.3f, ignoreTimeScale: true).FrameTimeInterval(ignoreTimeScale: true).Do(delegate(TimeInterval<float> x)
		{
			canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, x.Value);
		});
		yield return stream.ToYieldInstruction();
		canvasGroup.blocksRaycasts = true;
		if (Singleton<Manager.Input>.Instance.MenuElements.IsNullOrEmpty())
		{
			Singleton<Manager.Input>.Instance.MenuElements = MenuUIElements;
		}
		_enabledInput.Value = true;
	}

	private IEnumerator Vanish(CanvasGroup canvasGroup)
	{
		_enabledInput.Value = false;
		canvasGroup.blocksRaycasts = false;
		float startAlpha = canvasGroup.alpha;
		IObservable<TimeInterval<float>> stream = ObservableEasing.Linear(0.3f, ignoreTimeScale: true).FrameTimeInterval(ignoreTimeScale: true).Do(delegate(TimeInterval<float> x)
		{
			canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, x.Value);
		});
		yield return stream.ToYieldInstruction();
	}

	public IObservable<Unit> OnCompletedStopAsObservable()
	{
		return _completeDoStop ?? (_completeDoStop = new Subject<Unit>());
	}

	public override void OnInputMoveDirection(MoveDirection moveDir)
	{
		switch (moveDir)
		{
		case MoveDirection.Down:
		{
			int num2 = _selectedID.Value + 1;
			if (num2 < _nodeList.Count)
			{
				_selectedID.Value = num2;
			}
			break;
		}
		case MoveDirection.Up:
		{
			int num = _selectedID.Value - 1;
			if (num >= 0)
			{
				_selectedID.Value = num;
			}
			break;
		}
		}
	}

	private void OnInputSubmit()
	{
		if (_selectedCommand != null)
		{
			_selectedCommand.Event?.Invoke(ID);
		}
	}

	private void OnInputSubmitFromButton(CommCommandOption option, int optionID)
	{
		if (option == _selectedOption)
		{
			if (_selectedCommand != null)
			{
				_selectedCommand.Event?.Invoke(ID);
			}
		}
		else
		{
			Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Select);
			_selectedID.Value = optionID;
		}
	}

	private void OnInputCancel()
	{
		CancelEvent?.Invoke();
	}

	public void Refresh(CommandInfo[] options, Action onCompleted = null)
	{
		if (_refreshDisposable != null)
		{
			_refreshDisposable.Dispose();
		}
		_refreshDisposable = Observable.FromCoroutine(() => Reopen(options, _panelCanvasGroup)).DoOnCompleted(delegate
		{
			onCompleted?.Invoke();
		}).Subscribe();
	}

	public void Refresh(CommandInfo[] options, CanvasGroup cg, Action onCompleted = null)
	{
		if (_refreshDisposable != null)
		{
			_refreshDisposable.Dispose();
		}
		_refreshDisposable = Observable.FromCoroutine(() => Reopen(options, cg)).DoOnCompleted(delegate
		{
			onCompleted?.Invoke();
		}).Subscribe();
	}

	private IEnumerator Reopen(CommandInfo[] options, CanvasGroup canvasGroup)
	{
		if (_enabledInput.Value)
		{
			_enabledInput.Value = false;
			yield return VanishElement(canvasGroup);
		}
		_options = options;
		_commandList.Clear();
		CommandInfo[] options2 = _options;
		foreach (CommandInfo commandInfo in options2)
		{
			bool? flag = commandInfo.Condition?.Invoke();
			if (!flag.HasValue || flag.Value)
			{
				_commandList.Add(commandInfo);
			}
		}
		yield return DisplayElement(canvasGroup);
		_visibled = true;
		_enabledInput.Value = true;
	}
}
