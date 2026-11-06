using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AIProject.ColorDefine;
using AIProject.Definitions;
using AIProject.Scene;
using Illusion.Extensions;
using Manager;
using ReMotion;
using Sirenix.OdinInspector;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using UnityEx;

namespace AIProject.UI;

public class RecipeItemTitleNodeUI : SerializedMonoBehaviour
{
	private UITrigger.TriggerEvent _onEnter;

	[SerializeField]
	private Button _button;

	[SerializeField]
	private Text _nameLabel;

	[SerializeField]
	private Text _successLabel;

	[SerializeField]
	[Header("Animation")]
	private Transform _animationRoot;

	[SerializeField]
	protected CanvasGroup _canvasGroup;

	[SerializeField]
	private float _easingDuration;

	[SerializeField]
	private MotionType _motionType;

	[SerializeField]
	private Transform _from;

	[SerializeField]
	private Transform _to;

	[SerializeField]
	private MotionType _alphaMotionType;

	[SerializeField]
	private float _fromAlpha;

	[SerializeField]
	private float _toAlpha = 1f;

	[SerializeField]
	private Image _line;

	[SerializeField]
	[DisableInEditorMode]
	[DisableInPlayMode]
	private StringReactiveProperty _name = new StringReactiveProperty();

	private BoolReactiveProperty _isSuccess = new BoolReactiveProperty();

	private GameObject _cachedgameObject;

	private CompositeDisposable disposables = new CompositeDisposable();

	private static GameObject _nodeBase;

	private RecipeDataInfo _data;

	[SerializeField]
	private RectTransform _content;

	public Button.ButtonClickedEvent OnClick
	{
		[CompilerGenerated]
		get
		{
			return _button.onClick;
		}
	}

	public bool IsInteractable
	{
		[CompilerGenerated]
		get
		{
			return _button.IsInteractable();
		}
	}

	public UITrigger.TriggerEvent onEnter => this.GetCache(ref _onEnter, delegate
	{
		UITrigger.TriggerEvent triggerEvent = new UITrigger.TriggerEvent();
		this.GetOrAddComponent<PointerEnterTrigger>().Triggers.Add(triggerEvent);
		return triggerEvent;
	});

	public int MaxCount { get; private set; }

	public RecipeDataInfo data
	{
		[CompilerGenerated]
		get
		{
			return _data;
		}
	}

	public IReadOnlyList<RecipeItemNodeUI> recipeItemNodeUIs { get; private set; }

	public string Name
	{
		[CompilerGenerated]
		get
		{
			return _name.Value;
		}
	}

	public bool isSuccess
	{
		[CompilerGenerated]
		get
		{
			return _isSuccess.Value;
		}
	}

	public bool Visible
	{
		get
		{
			return cachedgameObject.activeSelf;
		}
		set
		{
			cachedgameObject.SetActiveIfDifferent(value);
		}
	}

	private unsafe GameObject cachedgameObject
	{
		[CompilerGenerated]
		get
		{
			return this.GetCacheObject(ref _cachedgameObject, new Func<GameObject>(this, (nint)__ldftn(Component.get_gameObject)));
		}
	}

	private static GameObject nodeBase
	{
		get
		{
			if (_nodeBase == null)
			{
				string bundle = Singleton<Manager.Resources>.Instance.DefinePack.ABPaths.MapScenePrefab;
				_nodeBase = CommonLib.LoadAsset<GameObject>(bundle, "RecipeItemNodeOption", clone: false, string.Empty);
				if (!MapScene.AssetBundlePaths.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == bundle))
				{
					MapScene.AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(bundle, string.Empty));
				}
			}
			return _nodeBase;
		}
	}

	public void Bind(CraftUI craftUI, int count, RecipeDataInfo data)
	{
		_data = data;
		_name.Value = "レシピ" + (count + 1);
		List<RecipeItemNodeUI> list = new List<RecipeItemNodeUI>();
		RecipeDataInfo.NeedData[] needList = data.NeedList;
		foreach (RecipeDataInfo.NeedData needData in needList)
		{
			RecipeItemNodeUI recipeItemNodeUI = UnityEngine.Object.Instantiate(nodeBase.GetComponent<RecipeItemNodeUI>(), _content, worldPositionStays: false);
			recipeItemNodeUI.Bind(craftUI, needData);
			list.Add(recipeItemNodeUI);
		}
		recipeItemNodeUIs = list;
		Refresh();
	}

	public void Refresh()
	{
		foreach (RecipeItemNodeUI recipeItemNodeUI in recipeItemNodeUIs)
		{
			recipeItemNodeUI.Refresh();
		}
		MaxCount = (from x in recipeItemNodeUIs
			select x.MaxCount into x
			orderby x
			select x).FirstOrDefault();
		_isSuccess.Value = MaxCount > 0;
		float a = ((!_isSuccess.Value) ? 0.5f : 1f);
		foreach (RecipeItemNodeUI recipeItemNodeUI2 in recipeItemNodeUIs)
		{
			Graphic[] graphics = recipeItemNodeUI2.graphics;
			foreach (Graphic graphic in graphics)
			{
				Color color = graphic.color;
				color.a = a;
				graphic.color = color;
			}
		}
	}

	protected virtual void Start()
	{
		if (_nameLabel != null)
		{
			_name.SubscribeToText(_nameLabel);
		}
		if (_successLabel != null)
		{
			_isSuccess.SubscribeToInteractable(_button);
			_isSuccess.SubscribeToText(_successLabel, (bool success) => (!success) ? string.Empty : "OK!");
			_isSuccess.Subscribe(delegate(bool success)
			{
				_successLabel.color = Define.Get(success ? Colors.Blue : Colors.White);
			});
		}
		if (Tween.MotionFunctionTable.TryGetValue(_motionType, out var value))
		{
		}
		if (Tween.MotionFunctionTable.TryGetValue(_alphaMotionType, out value))
		{
			ObservableEasing.Create(value, _easingDuration, ignoreTimeScale: true).FrameTimeInterval(ignoreTimeScale: true).Subscribe(delegate(TimeInterval<float> x)
			{
				_canvasGroup.alpha = Mathf.Lerp(_fromAlpha, _toAlpha, x.Value);
			})
				.AddTo(disposables);
		}
	}

	private void OnDestroy()
	{
		disposables.Clear();
	}
}
