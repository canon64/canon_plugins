using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AIProject.ColorDefine;
using AIProject.SaveData;
using AIProject.UI.Viewer;
using Illusion.Extensions;
using Sirenix.OdinInspector;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace AIProject.UI;

public class RecipeItemNodeUI : SerializedMonoBehaviour
{
	[SerializeField]
	private Text _nameLabel;

	[SerializeField]
	private ConditionalTextXtoYViewer _slotCounter;

	[SerializeField]
	[Header("Animation")]
	private Transform _animationRoot;

	[SerializeField]
	protected CanvasGroup _canvasGroup;

	[SerializeField]
	private Image _line;

	[SerializeField]
	[DisableInEditorMode]
	[DisableInPlayMode]
	private StringReactiveProperty _name = new StringReactiveProperty();

	private GameObject _cachedgameObject;

	private CompositeDisposable disposables = new CompositeDisposable();

	private RecipeDataInfo.NeedData _data;

	private Graphic[] _graphics;

	private CraftUI _craftUI;

	public string Name
	{
		[CompilerGenerated]
		get
		{
			return _name.Value;
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

	public Graphic[] graphics => this.GetCache(ref _graphics, delegate
	{
		List<Graphic> list = new List<Graphic>();
		if (_nameLabel != null)
		{
			list.Add(_nameLabel);
		}
		if (_slotCounter.layout != null)
		{
			list.AddRange(_slotCounter.layout.GetComponentsInChildren<Graphic>(includeInactive: true));
		}
		return list.ToArray();
	});

	public int MaxCount { get; private set; }

	private int ItemCount
	{
		[CompilerGenerated]
		get
		{
			return _craftUI.checkStorages.SelectMany((IReadOnlyCollection<StuffItem> x) => x).FindItems(new StuffItem(_data.CategoryID, _data.ID, 0)).Sum((StuffItem p) => p.Count);
		}
	}

	public void Bind(CraftUI craftUI, RecipeDataInfo.NeedData data)
	{
		_craftUI = craftUI;
		_data = data;
		_name.Value = data.Name;
		_slotCounter.y = data.Sum;
		Refresh();
	}

	public void Refresh()
	{
		_slotCounter.x = ItemCount;
		MaxCount = _slotCounter.x / _slotCounter.y;
	}

	protected virtual void Start()
	{
		if (_nameLabel != null)
		{
			_name.SubscribeToText(_nameLabel);
		}
		_slotCounter.Initialize();
		if (_slotCounter.xText != null)
		{
			_slotCounter.X.Select((int i) => (i < _slotCounter.y) ? Colors.Red : Colors.White).Select(Define.Get).Subscribe(delegate(Color color)
			{
				_slotCounter.xText.color = color;
			})
				.AddTo(_slotCounter.xText);
		}
	}

	private void OnDestroy()
	{
		disposables.Clear();
	}
}
