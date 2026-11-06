using System;
using System.Collections;
using System.Linq;
using AIProject;
using AIProject.Scene;
using Illusion.Extensions;
using Manager;
using ReMotion;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.UI;

namespace ConfigScene;

[DefaultExecutionOrder(100)]
public class ConfigWindow : BaseLoader
{
	[Serializable]
	public class ShortCutGroup
	{
		public string title;

		public string name;

		public int visibleNo;

		[SerializeField]
		private RectTransform[] _trans;

		public RectTransform trans => _trans[visibleNo];

		public void VisibleUpdate()
		{
			RectTransform[] array = _trans;
			foreach (RectTransform rectTransform in array)
			{
				rectTransform.gameObject.SetActiveIfDifferent(rectTransform == trans);
			}
		}
	}

	[SerializeField]
	private CanvasGroup canvasGroup;

	[Label("コンフィグ項目を写す場所")]
	[SerializeField]
	private RectTransform mainWindow;

	[Label("ショートカットボタンの場所")]
	[SerializeField]
	private RectTransform shortCutButtonBackGround;

	[Label("ショートカットボタンのプレファブ")]
	[SerializeField]
	private Button shortCutButtonPrefab;

	[Tooltip("ショートカットのリンク")]
	[SerializeField]
	private ShortCutGroup[] shortCuts;

	[Tooltip("初めに設定されているボタン")]
	[SerializeField]
	private Button[] buttons;

	[Tooltip("背景")]
	[SerializeField]
	private Image imgBackGroud;

	private BaseSetting[] settings;

	private float timeScale = 1f;

	private Manager.Input.ValidType _validType;

	public static Action UnLoadAction = null;

	public static Action TitleChangeAction = null;

	public static Color backGroundColor = new Color(0f, 0f, 0f, 0f);

	private readonly string[] localizeIsInit = new string[5]
	{
		"設定を初期化しますか？",
		string.Empty,
		string.Empty,
		string.Empty,
		string.Empty
	};

	private readonly string[] localizeIsTitle = new string[5]
	{
		"タイトルに戻りますか？",
		string.Empty,
		string.Empty,
		string.Empty,
		string.Empty
	};

	public static bool IsConfig = false;

	private GraphicSetting graphicSetting;

	public float timeScaleChange
	{
		set
		{
			timeScale = value;
		}
	}

	public void Unload()
	{
		if (IsConfig)
		{
			Save();
			if (UnLoadAction != null)
			{
				UnLoadAction();
			}
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	private void PlaySE(SoundPack.SystemSE se = SoundPack.SystemSE.OK_S)
	{
		Singleton<Manager.Resources>.Instance.SoundPack.Play(se);
	}

	protected override void Awake()
	{
		base.Awake();
		if (Singleton<Game>.IsInstance())
		{
			if (Singleton<Game>.Instance.Config != null)
			{
				UnityEngine.Object.Destroy(Singleton<Game>.Instance.Config.gameObject);
				Singleton<Game>.Instance.Config = null;
				GC.Collect();
				UnityEngine.Resources.UnloadUnusedAssets();
			}
			Singleton<Game>.Instance.Config = this;
		}
		timeScale = Time.timeScale;
		Time.timeScale = 0f;
		if (Singleton<Manager.Input>.IsInstance())
		{
			_validType = Singleton<Manager.Input>.Instance.State;
			Singleton<Manager.Input>.Instance.ReserveState(Manager.Input.ValidType.UI);
			Singleton<Manager.Input>.Instance.SetupState();
		}
	}

	protected void OnDisable()
	{
		if (Singleton<Game>.IsInstance())
		{
			Singleton<Game>.Instance.Config = null;
		}
		Time.timeScale = timeScale;
		if (Singleton<Manager.Input>.IsInstance())
		{
			Singleton<Manager.Input>.Instance.ReserveState(_validType);
			Singleton<Manager.Input>.Instance.SetupState();
		}
	}

	private IEnumerator Start()
	{
		Open();
		ShortCutGroup[] array = shortCuts;
		foreach (ShortCutGroup shortCutGroup in array)
		{
			shortCutGroup.VisibleUpdate();
		}
		var shortCutList = shortCuts.Select(delegate(ShortCutGroup p, int siblingIndex)
		{
			Button button = UnityEngine.Object.Instantiate(shortCutButtonPrefab, shortCutButtonBackGround, worldPositionStays: false);
			Text componentInChildren = button.GetComponentInChildren<Text>(includeInactive: true);
			componentInChildren.text = p.title;
			button.transform.SetSiblingIndex(siblingIndex);
			ConfigSubWindowComponent comp = button.GetComponentInChildren<ConfigSubWindowComponent>(includeInactive: true);
			comp.selectAction.listActionEnter.Add(delegate
			{
				comp.objSelect.SetActiveIfDifferent(active: true);
				PlaySE(SoundPack.SystemSE.Select);
			});
			comp.selectAction.listActionExit.Add(delegate
			{
				comp.objSelect.SetActiveIfDifferent(active: false);
			});
			return new
			{
				bt = button,
				name = p.name
			};
		}).ToList();
		settings = shortCuts.Select((ShortCutGroup p) => p.trans.GetComponent<BaseSetting>()).ToArray();
		while (!Config.initialized)
		{
			yield return null;
		}
		if ((bool)imgBackGroud)
		{
			imgBackGroud.color = backGroundColor;
		}
		BaseSetting[] array2 = settings;
		foreach (BaseSetting baseSetting in array2)
		{
			baseSetting.Init();
			baseSetting.UIPresenter();
		}
		float spacing = mainWindow.GetComponent<VerticalLayoutGroup>().spacing;
		shortCutList.ForEach(sc =>
		{
			sc.bt.OnClickAsObservable().Subscribe(delegate
			{
				ShortCutGroup[] array3 = shortCuts.TakeWhile((ShortCutGroup p) => p.name != sc.name).ToArray();
				float y = Mathf.Min(array3.Sum((ShortCutGroup p) => p.trans.sizeDelta.y) + spacing * (float)array3.Length, mainWindow.rect.height - (mainWindow.parent as RectTransform).rect.height);
				Vector2 anchoredPosition = mainWindow.anchoredPosition;
				anchoredPosition.y = y;
				mainWindow.anchoredPosition = anchoredPosition;
			});
		});
		graphicSetting = (from sc in shortCuts
			select sc.trans.GetComponent<GraphicSetting>() into sc
			where sc != null
			select sc).FirstOrDefault();
		shortCutList.ForEach(sc =>
		{
			sc.bt.OnClickAsObservable().Subscribe(delegate
			{
				PlaySE();
			});
		});
		buttons.ToList().ForEach(delegate(Button bt)
		{
			this.ObserveEveryValueChanged((ConfigWindow _) => !Singleton<Scene>.Instance.IsNowLoadingFade).SubscribeToInteractable(bt);
			bt.OnPointerEnterAsObservable().Subscribe(delegate
			{
				PlaySE(SoundPack.SystemSE.Select);
			});
			ConfigSubWindowComponent comp = bt.GetComponentInChildren<ConfigSubWindowComponent>(includeInactive: true);
			if ((bool)comp)
			{
				comp.selectAction.listActionEnter.Add(delegate
				{
					comp.objSelect.SetActiveIfDifferent(active: true);
				});
				comp.selectAction.listActionExit.Add(delegate
				{
					comp.objSelect.SetActiveIfDifferent(active: false);
				});
			}
		});
		Button backButton = buttons.FirstOrDefault((Button p) => p.name == "btnClose");
		(from _ in this.UpdateAsObservable()
			where Singleton<Game>.Instance.ExitScene == null && Singleton<Game>.Instance.Dialog == null
			where UnityEngine.Input.GetMouseButtonDown(1)
			where IsConfig
			where !Singleton<Scene>.Instance.IsOverlap && backButton.interactable
			select _).Take(1).Subscribe(delegate
		{
			OnBack();
		});
		(from _ in this.UpdateAsObservable()
			where !Singleton<Scene>.Instance.IsFadeNow
			where Singleton<Game>.Instance.ExitScene == null && Singleton<Game>.Instance.Dialog == null
			select _).Subscribe(delegate
		{
			if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
			{
				Singleton<Game>.Instance.LoadExit();
			}
		});
		if (Singleton<GameCursor>.IsInstance())
		{
			Singleton<GameCursor>.Instance.SetCursorLock(setLockFlag: false);
		}
		IsConfig = true;
	}

	private void OnDestroy()
	{
		IsConfig = false;
		UnLoadAction = null;
		TitleChangeAction = null;
	}

	public void OnDefault()
	{
		PlaySE();
		ConfirmScene.Sentence = localizeIsInit[Singleton<GameSystem>.Instance.languageInt];
		ConfirmScene.OnClickedYes = delegate
		{
			PlaySE(SoundPack.SystemSE.OK_L);
			Singleton<Voice>.Instance.Reset();
			Singleton<Config>.Instance.Reset();
			BaseSetting[] array = settings;
			foreach (BaseSetting baseSetting in array)
			{
				baseSetting.UIPresenter();
			}
		};
		ConfirmScene.OnClickedNo = delegate
		{
			PlaySE(SoundPack.SystemSE.Cancel);
		};
		Singleton<Game>.Instance.LoadDialog();
	}

	public void OnTitle()
	{
		PlaySE();
		bool flag = false;
		string text = "Title";
		if (Singleton<Scene>.Instance.NowSceneNames[0] == text)
		{
			flag = true;
		}
		if (!flag)
		{
			ConfirmScene.Sentence = localizeIsTitle[Singleton<GameSystem>.Instance.languageInt];
			ConfirmScene.OnClickedYes = delegate
			{
				Save();
				if (TitleChangeAction != null)
				{
					TitleChangeAction();
				}
				if (UnLoadAction != null)
				{
					UnLoadAction();
				}
				Singleton<Game>.Instance.Dialog.TimeScale = 1f;
				PlaySE(SoundPack.SystemSE.OK_L);
				Scene.Data data = new Scene.Data
				{
					levelName = "Title",
					isFade = true,
					onLoad = delegate
					{
						if (IsConfig)
						{
							UnityEngine.Object.Destroy(base.gameObject);
							Singleton<Game>.Instance.WorldData = null;
						}
					}
				};
				Singleton<Scene>.Instance.LoadReserve(data, isLoadingImageDraw: true);
			};
			ConfirmScene.OnClickedNo = delegate
			{
				PlaySE(SoundPack.SystemSE.Cancel);
			};
			Singleton<Game>.Instance.LoadDialog();
		}
		else
		{
			Close(delegate
			{
				Unload();
			});
		}
	}

	public void OnGameEnd()
	{
		PlaySE();
	}

	public void OnBack()
	{
		if (!(Singleton<Game>.Instance.ExitScene != null) && !(Singleton<Game>.Instance.Dialog != null))
		{
			PlaySE(SoundPack.SystemSE.Cancel);
			Close(delegate
			{
				Unload();
			});
		}
	}

	private void Save()
	{
		Singleton<Voice>.Instance.Save();
		Singleton<Config>.Instance.Save();
	}

	private void Open()
	{
		canvasGroup.blocksRaycasts = false;
		ObservableEasing.Linear(0.2f, ignoreTimeScale: true).FrameTimeInterval(ignoreTimeScale: true).Subscribe(delegate(TimeInterval<float> x)
		{
			canvasGroup.alpha = x.Value;
		}, delegate(Exception ex)
		{
			Debug.LogException(ex);
		}, delegate
		{
			canvasGroup.blocksRaycasts = true;
		});
	}

	private void Close(Action onCompleted)
	{
		canvasGroup.blocksRaycasts = false;
		ObservableEasing.Linear(0.2f, ignoreTimeScale: true).FrameTimeInterval(ignoreTimeScale: true).Subscribe(delegate(TimeInterval<float> x)
		{
			canvasGroup.alpha = 1f - x.Value;
		}, delegate(Exception ex)
		{
			Debug.LogException(ex);
		}, onCompleted);
	}

	public void CharaEntryInteractable(bool _interactable)
	{
		if ((bool)graphicSetting)
		{
			graphicSetting.EntryInteractable(_interactable);
		}
	}
}
