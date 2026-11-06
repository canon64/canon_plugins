using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ADV.Commands.Base;
using Illusion.CustomAttributes;
using Illusion.Extensions;
using Illusion.Game;
using Manager;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ADV;

public class BackLog : MonoBehaviour
{
	public class Data
	{
		private static MainScenario _scenario;

		public int line { get; private set; }

		public ADV.Commands.Base.Text.Data text { get; private set; }

		public List<TextScenario.IVoice[]> voiceList { get; private set; }

		public Color textColor => _scenario.commandController.fontColor[text.colorKey];

		public static void Initialize(MainScenario scenario)
		{
			_scenario = scenario;
		}

		public Data(ADV.Commands.Base.Text.Data text, List<TextScenario.IVoice[]> voiceList)
		{
			Initialize(-1, text, voiceList);
		}

		public Data(int line, ADV.Commands.Base.Text.Data text, List<TextScenario.IVoice[]> voiceList)
		{
			Initialize(line, text, voiceList);
		}

		private void Initialize(int line, ADV.Commands.Base.Text.Data text, List<TextScenario.IVoice[]> voiceList)
		{
			this.line = line;
			this.text = text;
			this.voiceList = voiceList;
		}

		public void VoicePlay()
		{
			_scenario.VoicePlay(voiceList);
		}
	}

	private class ObjData
	{
		private readonly UnityEngine.UI.Text _name;

		private readonly UnityEngine.UI.Text _message;

		private readonly Button _voiceButton;

		private readonly ReactiveProperty<Data> _data = new ReactiveProperty<Data>();

		public UnityEngine.UI.Text Name => _name;

		public UnityEngine.UI.Text Message => _message;

		public Button VoiceButton => _voiceButton;

		public Data Data
		{
			get
			{
				return _data.Value;
			}
			set
			{
				_data.Value = value;
			}
		}

		private HyphenationJpn hypJpn { get; set; }

		public ObjData(LogData logData)
		{
			_name = logData.Name;
			_message = logData.Message;
			_voiceButton = logData.VoiceButton;
			hypJpn = _message.GetOrAddComponent<HyphenationJpn>();
			if (hypJpn != null)
			{
				hypJpn.SetText(_message);
			}
			_data.TakeUntilDestroy(_voiceButton).Subscribe(delegate(Data value)
			{
				if (value == null)
				{
					_name.text = string.Empty;
					_message.text = string.Empty;
				}
				else
				{
					_name.text = value.text.name;
					_message.text = value.text.text;
				}
				if (hypJpn != null)
				{
					hypJpn.SetText(_message.text);
				}
				SetFontColor();
			});
		}

		public void SetFontColor()
		{
			if (Data != null)
			{
				SetFontColor(Data.textColor);
			}
		}

		private void SetFontColor(Color color)
		{
			_name.color = color;
			_message.color = color;
		}
	}

	[SerializeField]
	private Canvas canvas;

	[SerializeField]
	private RectTransform logRoot;

	[SerializeField]
	private Scrollbar scrollbar;

	[SerializeField]
	private Button upButton;

	[SerializeField]
	private Button downButton;

	[SerializeField]
	private Button returnButton;

	[SerializeField]
	private MainScenario scenario;

	[SerializeField]
	[NotEditable]
	private int desplayTextSum;

	private readonly List<Data> dataList = new List<Data>();

	private readonly ReactiveProperty<int> _nowIndex = new ReactiveProperty<int>(-1);

	private static int LOG_LIMITED { get; } = 100;


	public bool Visible
	{
		set
		{
			canvas.enabled = value;
			if (value)
			{
				Init();
			}
		}
	}

	public List<Data> Logs => dataList;

	public Data LastData => dataList.LastOrDefault();

	public int NowIndex
	{
		get
		{
			return VisibleRangeClamp(_nowIndex.Value);
		}
		set
		{
			_nowIndex.Value = value;
		}
	}

	private int Min => desplayTextSum - 1;

	private int Max => Mathf.Max(Min, dataList.Count - 1);

	public void NextIndex()
	{
		SetValue(NowIndex + 1);
	}

	public void PrevIndex()
	{
		SetValue(NowIndex - 1);
	}

	[Conditional("UNITY_EDITOR")]
	public void TextHeightCheck(Data data)
	{
		LogData component = logRoot.GetChild(0).GetComponent<LogData>();
		component.Message.text = data.text.text;
		component.Message.rectTransform.IsHeightOver(component.Message);
	}

	public void SetValue(int index)
	{
		NowIndex = VisibleRangeClamp(index);
	}

	private int VisibleRangeClamp(int value)
	{
		return Mathf.Clamp(value, Min, Max);
	}

	public void Init()
	{
		NowIndex = -1;
		SetValue(dataList.Count - 1);
		scrollbar.size = Mathf.Lerp(0f, 1f, Mathf.InverseLerp(0f, Max, Min));
	}

	public void Add(Data data)
	{
		dataList.Add(data);
		if (dataList.Count > LOG_LIMITED)
		{
			Remove();
		}
	}

	public void Remove()
	{
		if (dataList.Count > 0)
		{
			dataList.RemoveAt(0);
		}
	}

	public void Clear()
	{
		dataList.Clear();
	}

	private void Start()
	{
		Data.Initialize(scenario);
		bool isScrollLock = false;
		bool isValueLock = false;
		(from _ in scrollbar.onValueChanged.AsObservable()
			where !isScrollLock
			select _ into value
			select (int)Mathf.Lerp(Max, Min, value)).Subscribe(delegate(int value)
		{
			isValueLock = true;
			NowIndex = value;
			isValueLock = false;
		});
		Action close = delegate
		{
			scenario.mode = MainScenario.Mode.Normal;
		};
		returnButton.OnClickAsObservable().Subscribe(delegate
		{
			Utils.Sound.Play(SystemSE.sel);
			close();
		});
		List<ObjData> logObjList = new List<ObjData>();
		_nowIndex.TakeUntilDestroy(this).Subscribe(delegate(int value)
		{
			if (value >= 0)
			{
				value = VisibleRangeClamp(value);
				int num = Mathf.Min(value, dataList.Count - 1);
				int min = Min;
				int max = Max;
				for (int j = 0; j < logObjList.Count; j++)
				{
					ObjData objData2 = logObjList[j];
					int num2 = num - j;
					objData2.Data = ((num2 < 0 || num2 >= dataList.Count) ? null : dataList[num2]);
					objData2.VoiceButton.gameObject.SetActive(objData2.Data != null && !objData2.Data.voiceList.IsNullOrEmpty());
					objData2.SetFontColor();
				}
				if (!isValueLock)
				{
					isScrollLock = true;
					scrollbar.value = Mathf.InverseLerp(max, min, value);
					isScrollLock = false;
				}
			}
		});
		desplayTextSum = logRoot.childCount;
		for (int i = 0; i < desplayTextSum; i++)
		{
			ObjData objData = new ObjData(logRoot.GetChild(i).GetComponent<LogData>());
			(from _ in objData.VoiceButton.OnClickAsObservable()
				where objData.Data != null
				select _).Subscribe(delegate
			{
				objData.Data.VoicePlay();
			});
			objData.VoiceButton.gameObject.SetActive(value: false);
			logObjList.Add(objData);
		}
		(from _ in upButton.OnPointerDownAsObservable().Merge(downButton.OnPointerDownAsObservable())
			where Input.GetMouseButtonDown(0)
			select _).Subscribe(delegate
		{
			Utils.Sound.Play(SystemSE.sel);
		});
		(from _ in this.UpdateAsObservable()
			where canvas.enabled
			where !scenario.modeChanging
			select _).Subscribe(delegate
		{
			if (Manager.Scene.IsOverlap && Manager.Scene.IsOverlapFirst(SingletonInitializer<ConfigScene>.instance))
			{
				logObjList.ForEach(delegate(ObjData p)
				{
					p.SetFontColor();
				});
			}
			bool isOnWindow = true;
			if (KeyInput.BackLogTextPageNext)
			{
				SetValue(NowIndex - logObjList.Count);
			}
			else if (KeyInput.BackLogTextPageBack)
			{
				SetValue(NowIndex + logObjList.Count);
			}
			else if (KeyInput.BackLogTextFirst)
			{
				SetValue(Min);
			}
			else if (KeyInput.BackLogTextLast)
			{
				SetValue(Max);
			}
			else if (KeyInput.BackLogTextNext(isOnWindow, isKeyCondition: true).isCheck)
			{
				PrevIndex();
			}
			else
			{
				KeyInput.Data data = KeyInput.BackLogTextBack(isOnWindow, isKeyCondition: true);
				if (data.isCheck)
				{
					int nowIndex = NowIndex;
					NextIndex();
					if (data.isKey && Input.GetKeyDown(KeyCode.DownArrow) && nowIndex == NowIndex)
					{
						close();
					}
				}
			}
		});
	}
}
