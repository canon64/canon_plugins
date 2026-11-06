using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.UI;

public class TextController : MonoBehaviour
{
	[SerializeField]
	private Text nameWindow;

	[SerializeField]
	private Text messageWindow;

	private const int MAX_FONT_SPEED = 100;

	[SerializeField]
	[RangeReactiveProperty(1f, 100f)]
	private IntReactiveProperty _fontSpeed = new IntReactiveProperty(100);

	private ColorReactiveProperty _fontColor = new ColorReactiveProperty(Color.white);

	private TypefaceAnimatorEx TA;

	private HyphenationJpn hypJpn;

	public bool isMovie;

	[SerializeField]
	private float movieFontSpeed = 1f;

	[SerializeField]
	[Range(0f, 1f)]
	private float movieProgress;

	public bool initialized { get; private set; }

	public bool IsCompleteDisplayText
	{
		get
		{
			if (isMovie)
			{
				return movieProgress >= 1f;
			}
			return !TA.isPlaying;
		}
	}

	public bool NameMessageVisible
	{
		get
		{
			if (nameVisible)
			{
				return messageVisible;
			}
			return false;
		}
		set
		{
			nameVisible = value;
			messageVisible = value;
		}
	}

	public bool nameVisible
	{
		get
		{
			if (!(nameWindow == null))
			{
				return nameWindow.enabled;
			}
			return false;
		}
		set
		{
			nameWindow.SafeProc(delegate(Text p)
			{
				p.enabled = value;
			});
		}
	}

	public bool messageVisible
	{
		get
		{
			if (!(messageWindow == null))
			{
				return messageWindow.enabled;
			}
			return false;
		}
		set
		{
			messageWindow.SafeProc(delegate(Text p)
			{
				p.enabled = value;
			});
		}
	}

	public Text NameWindow => nameWindow;

	public Text MessageWindow => messageWindow;

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

	public Color FontColor
	{
		get
		{
			return _fontColor.Value;
		}
		set
		{
			_fontColor.Value = value;
		}
	}

	public void Change(Text nameWindow, Text messageWindow)
	{
		Clear();
		this.nameWindow = nameWindow;
		this.messageWindow = messageWindow;
		Object.Destroy(hypJpn);
		Initialize();
	}

	public void Clear()
	{
		if (!initialized)
		{
			Initialize();
		}
		nameWindow.SafeProc(delegate(Text p)
		{
			p.text = string.Empty;
		});
		messageWindow.text = string.Empty;
		hypJpn.SetText(string.Empty);
		TA.Stop();
		TA.progress = 0f;
		movieProgress = 0f;
	}

	public void Set(string nameText, string messageText)
	{
		nameWindow.SafeProc(delegate(Text p)
		{
			p.text = nameText;
		});
		messageWindow.text = messageText;
		hypJpn.SetText(messageWindow.text);
		TA.Play();
		movieProgress = 0f;
	}

	public void ForceCompleteDisplayText()
	{
		TA.progress = 1f;
	}

	public void Initialize()
	{
		hypJpn = messageWindow.GetOrAddComponent<HyphenationJpn>();
		hypJpn.SetText(messageWindow);
		TA = messageWindow.GetComponent<TypefaceAnimatorEx>();
		initialized = true;
	}

	private void Awake()
	{
		if (!initialized)
		{
			Initialize();
		}
	}

	private void Start()
	{
		_fontSpeed.TakeUntilDestroy(this).Subscribe(delegate(int value)
		{
			TA.isNoWait = value == 100;
			if (!TA.isNoWait)
			{
				TA.timeMode = TypefaceAnimatorEx.TimeMode.Speed;
				TA.speed = value;
			}
		});
		_fontColor.TakeUntilDestroy(this).Subscribe(delegate(Color color)
		{
			nameWindow.SafeProc(delegate(Text p)
			{
				p.color = color;
			});
			messageWindow.SafeProc(delegate(Text p)
			{
				p.color = color;
			});
		});
		(from _ in this.UpdateAsObservable()
			where base.enabled
			where isMovie
			select _).Subscribe(delegate
		{
			movieProgress = Mathf.Min(movieProgress + Time.deltaTime / movieFontSpeed, 1f);
		});
	}
}
