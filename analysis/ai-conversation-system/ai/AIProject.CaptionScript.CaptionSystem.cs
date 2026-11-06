using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using ConfigScene;
using Manager;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace AIProject.CaptionScript;

public class CaptionSystem : MonoBehaviour
{
	private const int _drawCount = 88;

	[SerializeField]
	private CanvasGroup _advWindowRootCG;

	[SerializeField]
	private CanvasGroup _advWindowCG;

	[SerializeField]
	private Image _backgroundImage;

	[SerializeField]
	private Image _windowImage;

	[SerializeField]
	private Image _nameFrameImage;

	[SerializeField]
	private Image _raycasterImage;

	[SerializeField]
	private Text _nameLabel;

	[SerializeField]
	private Text _messageLabel;

	[SerializeField]
	private TypefaceAnimatorEx _typefaceAnimator;

	private const int MAX_FONT_SPEED = 100;

	[SerializeField]
	[RangeReactiveProperty(1f, 100f)]
	private IntReactiveProperty _fontSpeed = new IntReactiveProperty(100);

	[SerializeField]
	private ColorReactiveProperty _fontColor = new ColorReactiveProperty(Color.white);

	private string[] _textBuffer;

	private int _currentLine = -1;

	private bool _endAnimation;

	private bool _clicked;

	private HyphenationJpn _hypJpn;

	private static readonly string[] _replaceStrings = new string[2] { "\r\n", "\r" };

	public int FontSpeed
	{
		get
		{
			return _fontSpeed.Value;
		}
		set
		{
			_fontSpeed.Value = Mathf.Clamp(value, 1, 100);
		}
	}

	public bool Visible
	{
		get
		{
			return _advWindowRootVisible.Value;
		}
		set
		{
			_advWindowRootVisible.Value = value;
		}
	}

	private BoolReactiveProperty _advWindowRootVisible { get; } = new BoolReactiveProperty(initialValue: true);


	private BoolReactiveProperty _advWindowVisible { get; } = new BoolReactiveProperty(initialValue: true);


	public Image Background
	{
		get
		{
			return _backgroundImage;
		}
		set
		{
			_backgroundImage = value;
		}
	}

	public Image RaycasterImage
	{
		[CompilerGenerated]
		get
		{
			return _raycasterImage;
		}
	}

	public Action Action { get; set; }

	private StringReactiveProperty _name { get; } = new StringReactiveProperty();


	private StringReactiveProperty _message { get; } = new StringReactiveProperty();


	public CrossFade CrossFade { get; private set; }

	public bool IsCompleteDisplayText => !_typefaceAnimator.isPlaying;

	private void Awake()
	{
		_hypJpn = _messageLabel.GetOrAddComponent<HyphenationJpn>();
		_name.Value = string.Empty;
		_name.SubscribeToText(_nameLabel);
		_message.Value = string.Empty;
		_message.SubscribeToText(_messageLabel);
		_advWindowRootVisible.Subscribe(delegate(bool isOn)
		{
			_advWindowRootCG.alpha = (isOn ? 1 : 0);
			_advWindowRootCG.blocksRaycasts = isOn;
		}).AddTo(_advWindowRootCG);
		_advWindowVisible.Subscribe(delegate(bool isOn)
		{
			_advWindowCG.alpha = (isOn ? 1 : 0);
			_advWindowCG.blocksRaycasts = isOn;
		}).AddTo(_advWindowCG);
		_message.Select((string s) => !s.IsNullOrEmpty()).Subscribe(delegate(bool isOn)
		{
			_advWindowVisible.Value = isOn;
		});
		base.enabled = false;
		_windowImage.color = Color.black - Color.black * 0.5f;
		if (_nameFrameImage != null)
		{
			_nameFrameImage.color = Color.black;
		}
		_fontSpeed.TakeUntilDestroy(this).Subscribe(delegate(int value)
		{
			_typefaceAnimator.isNoWait = value == 100;
			if (!_typefaceAnimator.isNoWait)
			{
				_typefaceAnimator.timeMode = TypefaceAnimatorEx.TimeMode.Speed;
				_typefaceAnimator.speed = value;
			}
		});
	}

	private void Start()
	{
		CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
		if (canvasGroup != null)
		{
			(from _ in Observable.EveryGameObjectUpdate().TakeUntilDestroy(base.gameObject)
				where base.isActiveAndEnabled
				where Mathf.Approximately(canvasGroup.alpha, 1f)
				select _).Subscribe(delegate
			{
				OnUpdate();
			});
		}
	}

	private void OnUpdate()
	{
		if ((!Singleton<Manager.Scene>.IsInstance() || !Singleton<Manager.Scene>.Instance.IsNowLoadingFade) && (!Singleton<MapUIContainer>.IsInstance() || !MapUIContainer.FadeCanvas.IsFading) && Singleton<Game>.IsInstance() && !(Singleton<Game>.Instance.Config != null) && !(Singleton<Game>.Instance.Dialog != null) && !(Singleton<Game>.Instance.ExitScene != null) && !(Singleton<Game>.Instance.MapShortcutUI != null) && Visible && _advWindowVisible.Value && UnityEngine.Input.GetKeyDown(KeyCode.F3))
		{
			ConfigWindow.TitleChangeAction = delegate
			{
				Singleton<Game>.Instance.Dialog.TimeScale = 1f;
			};
			Singleton<Game>.Instance.LoadConfig();
		}
	}

	public bool ChangeChara(int id)
	{
		return false;
	}

	public void Clear()
	{
		_name.Value = string.Empty;
		_message.Value = string.Empty;
	}

	public void SetName(string name)
	{
		_nameLabel.enabled = false;
		_name.Value = name;
		_nameLabel.enabled = true;
	}

	public void SetText(string text, bool noWait = false)
	{
		base.enabled = true;
		_message.Value = text;
		if ((object)_hypJpn != null)
		{
			_hypJpn.SetText(text);
		}
		FontSpeed = Config.GameData.FontSpeed;
		if (noWait && !_typefaceAnimator.isNoWait)
		{
			FontSpeed = 100;
		}
		_typefaceAnimator.Play();
	}

	public void ForceCompleteDisplayText()
	{
		_typefaceAnimator.progress = 1f;
	}

	private List<string> AnalysisText(string text)
	{
		List<string> list = new List<string>();
		int num = 0;
		text = text.Replace("\n", _replaceStrings);
		StringBuilder stringBuilder = new StringBuilder();
		do
		{
			int num2 = 29;
			stringBuilder.Length = 0;
			for (int i = 0; i < 3; i++)
			{
				if (num + num2 > text.Length)
				{
					num2 += text.Length - (num + num2);
					if (num2 <= 0)
					{
						break;
					}
				}
				string text2 = text.Substring(num, num2);
				int num3 = text2.IndexOf("\n");
				if (num3 < 0)
				{
					stringBuilder.Append(text2);
					num += num2;
				}
				else
				{
					string text3 = text2.Substring(0, num3 + 1);
					stringBuilder.Append(text3);
					num += text3.Length;
				}
			}
			list.Add(stringBuilder.ToString());
		}
		while (num < text.Length);
		return list;
	}

	private void OnStartText()
	{
		_endAnimation = false;
	}

	private void OnEndText()
	{
		if (_currentLine < _textBuffer.Length)
		{
			NextLine();
		}
		else
		{
			_endAnimation = true;
		}
	}

	private void NextLine(bool noWait = false)
	{
		StringBuilder stringBuilder = StringBuilderPool.Get();
		stringBuilder.Append(_name.Value);
		stringBuilder.Append("： ");
		stringBuilder.Append(_textBuffer[_currentLine]);
		_message.Value = stringBuilder.ToString();
		_currentLine++;
		FontSpeed = Config.GameData.FontSpeed;
		if (noWait && !_typefaceAnimator.isNoWait)
		{
			FontSpeed = 100;
		}
		_typefaceAnimator.Play();
	}
}
