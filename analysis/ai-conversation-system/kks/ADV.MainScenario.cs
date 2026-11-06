using System;
using System.Collections.Generic;
using Illusion.Component.UI;
using Manager;
using UniRx;
using UniRx.Triggers;
using UnityEngine;

namespace ADV;

public sealed class MainScenario : TextScenario
{
	public enum Mode
	{
		Normal,
		WindowNone,
		BackLog,
		Movie
	}

	[Serializable]
	public class ModeReactiveProperty : ReactiveProperty<Mode>
	{
		public ModeReactiveProperty()
		{
		}

		public ModeReactiveProperty(Mode initialValue)
			: base(initialValue)
		{
		}
	}

	[SerializeField]
	private BackLog backLog;

	[SerializeField]
	private ADVButton advButton;

	[SerializeField]
	private float movieAutoWaitTime = 3f;

	[SerializeField]
	private ModeReactiveProperty _mode = new ModeReactiveProperty(Mode.Normal);

	[SerializeField]
	private Canvas canvas;

	public BackLog BackLog => backLog;

	public Mode mode
	{
		get
		{
			return _mode.Value;
		}
		set
		{
			_mode.Value = value;
		}
	}

	public bool modeChanging { get; private set; }

	public Canvas Canvas => canvas;

	public override void Initialize()
	{
		base.Initialize();
		advButton.IsVisible = windowImage.enabled;
	}

	public override void Release()
	{
		base.Release();
		advButton.IsVisible = windowImage.enabled;
		_mode.Value = Mode.Normal;
		UnityEngine.Object.Destroy(GetComponent<Lookat_dan>());
		UnityEngine.Object.Destroy(GetComponent<HMotionShake>());
	}

	public void VoicePlay(List<IVoice[]> voices)
	{
		foreach (IVoice[] voice in voices)
		{
			for (int i = 0; i < voice.Length; i++)
			{
				voice[i].Convert2D();
			}
		}
		base.currentCharaData.isSkip = true;
		VoicePlay(voices, null, null);
	}

	protected override void Start()
	{
		base.Start();
		_mode.Scan(delegate(Mode prev, Mode current)
		{
			switch (prev)
			{
			case Mode.Movie:
				base.regulate.SetRegulate((Regulate.Control)0);
				base.isWindowImage = true;
				base.textController.nameVisible = true;
				base.textController.MessageWindow.alignment = base.textController.NameWindow.alignment;
				base.textController.isMovie = false;
				break;
			}
			return current;
		}).Subscribe(delegate(Mode mode)
		{
			modeChanging = true;
			switch (mode)
			{
			case Mode.Normal:
				msgWindowCanvas.enabled = true;
				base.Choices.gameObject.SetActive(base.isChoice);
				backLog.Visible = false;
				break;
			case Mode.WindowNone:
				base.isSkip = false;
				base.isAuto = false;
				msgWindowCanvas.enabled = false;
				base.Choices.gameObject.SetActive(value: false);
				break;
			case Mode.BackLog:
				base.isSkip = false;
				base.isAuto = false;
				msgWindowCanvas.enabled = true;
				base.Choices.gameObject.SetActive(value: false);
				backLog.Visible = true;
				break;
			case Mode.Movie:
				base.regulate.SetRegulate((Regulate.Control)20);
				base.isWindowImage = false;
				base.textController.nameVisible = false;
				base.textController.MessageWindow.alignment = TextAnchor.LowerCenter;
				base.textController.isMovie = true;
				break;
			}
		});
		Func<bool> isAddReg = delegate
		{
			if (modeChanging)
			{
				modeChanging = false;
				return false;
			}
			if (base.advScene.startAddSceneName != Scene.AddSceneName)
			{
				base.isSkip = false;
				base.isAuto = false;
				base.currentCharaData.isSkip = true;
				return false;
			}
			return true;
		};
		(from _ in this.UpdateAsObservable().Do(delegate
			{
				UpdateBefore();
			})
			where isAddReg()
			where !UpdateRegulate()
			select _).Subscribe(delegate
		{
			bool flag = false;
			bool flag2 = true;
			KeyInput.Data data = KeyInput.TextNext(base.isCameraLock ? flag2 : base.isSelectMessageWindow, base.isCameraLock);
			if (data.isMouse)
			{
				flag = !Input.GetMouseButtonDown(0) || !advButton.isSelect;
			}
			flag |= data.isKey;
			if (flag && base.isSkip && !base.isChoice)
			{
				base.isSkip = false;
			}
			if (!base.isCameraLock && _mode.Value == Mode.Normal)
			{
				flag2 = base.isSelectMessageWindow;
			}
			bool flag3 = KeyInput.SkipButton;
			bool isCompleteDisplayText = false;
			switch (_mode.Value)
			{
			case Mode.Normal:
				if (advButton.gameObject.activeSelf)
				{
					if (advButton.BackLog.interactable)
					{
						KeyInput.Data data2 = KeyInput.BackLogButton(flag2, base.isCameraLock);
						if (data2.isCheck && (!base.isChoice || !data2.isKey || !Input.GetKeyDown(KeyCode.UpArrow)) && !base.isInputLock)
						{
							_mode.Value = Mode.BackLog;
						}
					}
					if (advButton.Close.interactable && KeyInput.WindowNoneButton(flag2, base.isCameraLock).isCheck)
					{
						bool flag4 = true;
						if (advButton.isFocus)
						{
							flag4 = advButton.Close.GetComponent<SelectUI>().isFocus;
						}
						if (flag4)
						{
							_mode.Value = Mode.WindowNone;
						}
					}
				}
				isCompleteDisplayText = base.textController.IsCompleteDisplayText;
				break;
			case Mode.WindowNone:
				if (!advButton.isSelect && !advButton.isFocus && KeyInput.WindowNoneButtonCancel(flag2, base.isCameraLock).isCheck)
				{
					_mode.Value = Mode.Normal;
				}
				isCompleteDisplayText = base.textController.IsCompleteDisplayText;
				flag = false;
				flag3 = false;
				break;
			case Mode.BackLog:
				if (!advButton.isSelect && !advButton.isFocus && KeyInput.BackLogButtonCancel(flag2, base.isCameraLock).isCheck)
				{
					_mode.Value = Mode.Normal;
				}
				isCompleteDisplayText = base.textController.IsCompleteDisplayText;
				flag = false;
				flag3 = false;
				break;
			case Mode.Movie:
				base.textController.ForceCompleteDisplayText();
				isCompleteDisplayText = base.textController.IsCompleteDisplayText;
				autoWaitTime = movieAutoWaitTime;
				flag = false;
				flag3 = false;
				break;
			}
			base._nextInfo.Set(isCompleteDisplayText, flag, flag3);
			MessageWindowProc(base._nextInfo);
		});
	}

	protected override void UpdateBefore()
	{
		if (_mode.Value != Mode.WindowNone && msgWindowCanvas != null)
		{
			msgWindowCanvas.enabled = !base.textController.MessageWindow.text.IsNullOrEmpty();
		}
		if (windowImage != null)
		{
			windowImage.enabled = base.isWindowImage && !base.textController.MessageWindow.text.IsNullOrEmpty();
		}
		bool flag = base.isWindowImage;
		advButton.IsVisible = flag;
		if (!flag)
		{
			nextMarker.enabled = false;
		}
		base.textController.FontColor = base.commandController.fontColor[base.fontColorKey ?? string.Empty];
	}

	protected override void Update()
	{
	}
}
