using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ADV;
using Illusion.Extensions;
using Manager;
using ReMotion;
using UniRx;
using UniRx.Toolkit;
using UnityEngine;
using UnityEngine.UI;

namespace AIProject.UI;

public class GameLog : MenuUIBehaviour
{
	public class GameLogElementPool : UniRx.Toolkit.ObjectPool<GameLogElement>
	{
		public GameLogElement Source { get; set; }

		public Transform Parent { get; set; }

		protected override GameLogElement CreateInstance()
		{
			return UnityEngine.Object.Instantiate(Source).GetComponent<GameLogElement>();
		}

		protected override void OnBeforeReturn(GameLogElement instance)
		{
			if (!(instance == null))
			{
				instance.transform.SetParent(Parent);
				base.OnBeforeReturn(instance);
			}
		}
	}

	[SerializeField]
	private CanvasGroup _canvasGroup;

	[SerializeField]
	private ScrollRect _scrollRect;

	[SerializeField]
	private Button _closeButton;

	[SerializeField]
	private GameLogElement _node;

	[SerializeField]
	private RectTransform _guideRoot;

	private Queue<GameLogElement> _logs = new Queue<GameLogElement>();

	private GameLogElementPool _elementPool = new GameLogElementPool();

	private const float _alphaFadeDuration = 0.1f;

	private IObservable<TimeInterval<float>> _lerpStream;

	private MenuUIBehaviour[] _menuUIElements;

	private IDisposable _fadeSubscriber;

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

	public Action OnClosed { get; set; }

	protected override void OnBeforeStart()
	{
		_elementPool = new GameLogElementPool
		{
			Source = _node
		};
		_lerpStream = ObservableEasing.Linear(0.1f, ignoreTimeScale: true).FrameTimeInterval(ignoreTimeScale: true);
		OnActiveChangedAsObservable().Subscribe(delegate(bool x)
		{
			SetActiveControl(x);
		});
		if (_closeButton != null)
		{
			_closeButton.onClick.AddListener(delegate
			{
				IsActiveControl = false;
			});
		}
		KeyCodeDownCommand keyCodeDownCommand = new KeyCodeDownCommand();
		keyCodeDownCommand.KeyCode = KeyCode.Mouse1;
		KeyCodeDownCommand keyCodeDownCommand2 = keyCodeDownCommand;
		keyCodeDownCommand2.TriggerEvent.AddListener(delegate
		{
			IsActiveControl = false;
		});
		_keyCommands.Add(keyCodeDownCommand2);
	}

	private void Update()
	{
		if (_guideRoot != null)
		{
			_guideRoot.gameObject.SetActiveIfDifferent(Config.GameData.ActionGuide);
		}
	}

	private void SetActiveControl(bool active)
	{
		IEnumerator coroutine = ((!active) ? Close() : Open());
		if (_fadeSubscriber != null)
		{
			_fadeSubscriber.Dispose();
		}
		_fadeSubscriber = Observable.FromCoroutine(() => coroutine).Subscribe(delegate
		{
		}, delegate(Exception ex)
		{
			if (Debug.isDebugBuild)
			{
				Debug.LogException(ex);
			}
		});
	}

	private IEnumerator Open()
	{
		base.gameObject.SetActive(value: true);
		Singleton<Manager.Input>.Instance.ReserveState(Manager.Input.ValidType.UI);
		Singleton<Manager.Input>.Instance.SetupState();
		if (_canvasGroup.blocksRaycasts)
		{
			_canvasGroup.blocksRaycasts = false;
		}
		_scrollRect.verticalScrollbar.value = 1f;
		IConnectableObservable<TimeInterval<float>> stream = _lerpStream.Publish();
		stream.Subscribe(delegate(TimeInterval<float> x)
		{
			_canvasGroup.alpha = x.Value;
		});
		stream.Connect();
		yield return stream.ToYieldInstruction();
		Singleton<Manager.Input>.Instance.MenuElements = MenuUIElements;
		Singleton<Manager.Input>.Instance.FocusLevel = 0;
		_canvasGroup.blocksRaycasts = true;
		base.EnabledInput = true;
	}

	private IEnumerator Close()
	{
		Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Cancel);
		Singleton<Manager.Input>.Instance.ClearMenuElements();
		base.EnabledInput = false;
		_canvasGroup.blocksRaycasts = false;
		Singleton<Manager.Input>.Instance.ReserveState(Manager.Input.ValidType.Action);
		float startAlpha = _canvasGroup.alpha;
		IConnectableObservable<TimeInterval<float>> stream = _lerpStream.Publish();
		stream.Subscribe(delegate(TimeInterval<float> x)
		{
			_canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, x.Value);
		});
		stream.Connect();
		yield return stream.ToYieldInstruction();
		Singleton<Manager.Input>.Instance.SetupState();
		OnClosed?.Invoke();
		base.gameObject.SetActive(value: false);
	}

	public void AddLog(string name, string message, IReadOnlyCollection<TextScenario.IVoice[]> voices)
	{
		if (_logs.Count > 50)
		{
			GameLogElement instance = _logs.Dequeue();
			_elementPool.Return(instance);
		}
		GameLogElement gameLogElement = _elementPool.Rent();
		gameLogElement.transform.SetParent(_scrollRect.content, worldPositionStays: false);
		gameLogElement.transform.localScale = Vector3.one;
		gameLogElement.transform.SetAsFirstSibling();
		gameLogElement.Add(name, message, voices);
		_logs.Enqueue(gameLogElement);
	}

	private void EnableRaycastTargetIfInvisible(bool isInvisible)
	{
		if (isInvisible)
		{
			_canvasGroup.blocksRaycasts = true;
		}
	}
}
