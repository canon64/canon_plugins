using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AIProject.SaveData;
using AIProject.UI.Viewer;
using Illusion.Extensions;
using Manager;
using UniRx;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AIProject.UI;

public class PlantUI : MenuUIBehaviour
{
	[SerializeField]
	private CanvasGroup _canvasGroup;

	[SerializeField]
	private RectTransform _farmland;

	[SerializeField]
	private Image _cursor;

	[SerializeField]
	private Button _allGetButton;

	public UnityEvent OnSubmitRemove { get; private set; } = new UnityEvent();

	public UnityEvent OnCancel { get; private set; } = new UnityEvent();

	public PlantIcon currentIcon
	{
		[CompilerGenerated]
		get
		{
			return _plantIcons[_currentIndex];
		}
	}

	public int currentIndex
	{
		[CompilerGenerated]
		get
		{
			return _currentIndex;
		}
	}

	public PlantIcon[] plantIcons
	{
		[CompilerGenerated]
		get
		{
			return _plantIcons;
		}
	}

	public IObservable<Unit> AllGet
	{
		[CompilerGenerated]
		get
		{
			return _allGetButton.OnClickAsObservable();
		}
	}

	private PlantIcon[] _plantIcons { get; set; }

	private int _currentIndex { get; set; } = -1;

	private List<AIProject.SaveData.Environment.PlantInfo> _plantList { get; set; }

	private CompositeDisposable disposable { get; } = new CompositeDisposable();

	public event Action<PlantIcon> IconChanged;

	public void SetPlantItem(StuffItem item)
	{
		StuffItemInfo item2 = Singleton<Manager.Resources>.Instance.GameInfo.GetItem(item.CategoryID, item.ID);
		if (item2 == null)
		{
			Debug.LogError(string.Format("Item not Find\n[{0}:{1}][{2}:{3}]", "CategoryID", item.CategoryID, "ID", item.ID), this);
		}
		else
		{
			AIProject.SaveData.Environment.PlantInfo plantInfo = Singleton<Manager.Resources>.Instance.GameInfo.GetPlantInfo(item2.nameHash);
			currentIcon.info = plantInfo;
			_plantList[_currentIndex] = plantInfo;
		}
	}

	public int GetEmptySum()
	{
		return _plantList.Count((AIProject.SaveData.Environment.PlantInfo x) => x == null);
	}

	public void SetPlantItemForAll(StuffItem item, int count)
	{
		StuffItemInfo item2 = Singleton<Manager.Resources>.Instance.GameInfo.GetItem(item.CategoryID, item.ID);
		if (item2 == null)
		{
			Debug.LogError(string.Format("Item not Find\n[{0}:{1}][{2}:{3}]", "CategoryID", item.CategoryID, "ID", item.ID), this);
			return;
		}
		int num = 0;
		for (int i = 0; i < _plantList.Count; i++)
		{
			if (_plantList[i] == null)
			{
				if (num++ >= count)
				{
					break;
				}
				AIProject.SaveData.Environment.PlantInfo plantInfo = Singleton<Manager.Resources>.Instance.GameInfo.GetPlantInfo(item2.nameHash);
				_plantIcons[i].info = plantInfo;
				_plantList[i] = plantInfo;
			}
		}
	}

	public void Open(List<AIProject.SaveData.Environment.PlantInfo> plantList)
	{
		_plantList = plantList;
		int count = plantList.Count;
		for (int i = 0; i < _plantIcons.Length; i++)
		{
			bool flag = i < count;
			_plantIcons[i].visible = flag;
			if (flag)
			{
				_plantIcons[i].info = plantList[i];
			}
			else
			{
				_plantIcons[i].info = null;
			}
		}
		PlantIcon[] array = _plantIcons;
		foreach (PlantIcon plantIcon in array)
		{
			plantIcon.toggle.isOn = false;
		}
		_plantIcons[0].toggle.isOn = true;
		_cursor.enabled = false;
	}

	public void Refresh()
	{
		if (this.IconChanged != null)
		{
			this.IconChanged(currentIcon);
		}
	}

	private IEnumerator BindingUI()
	{
		_plantIcons = (from t in _farmland.Children()
			select t.GetComponent<PlantIcon>()).ToArray();
		_plantIcons.Select((PlantIcon p) => p.toggle).BindToEnter(isExit: true, _cursor).AddTo(disposable);
		_plantIcons.Select((PlantIcon p) => p.toggle).BindToGroup(delegate(int sel)
		{
			_currentIndex = sel;
			if (this.IconChanged != null)
			{
				this.IconChanged(currentIcon);
			}
		}).AddTo(disposable);
		yield break;
	}

	protected override void Start()
	{
		_canvasGroup.alpha = 1f;
		StartCoroutine(BindingUI());
		IsActiveControl = true;
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
		actionIDDownCommand = new ActionIDDownCommand();
		actionIDDownCommand.ActionID = ActionID.SquareX;
		ActionIDDownCommand actionIDDownCommand4 = actionIDDownCommand;
		actionIDDownCommand4.TriggerEvent.AddListener(delegate
		{
			OnInputSubmit();
		});
		_actionCommands.Add(actionIDDownCommand4);
		actionIDDownCommand = new ActionIDDownCommand();
		actionIDDownCommand.ActionID = ActionID.Submit;
		actionIDDownCommand4 = actionIDDownCommand;
		actionIDDownCommand4.TriggerEvent.AddListener(delegate
		{
			OnInputSubmit();
		});
		_actionCommands.Add(actionIDDownCommand4);
		base.Start();
	}

	private void OnDestroy()
	{
		disposable.Clear();
	}

	private void OnInputSubmit()
	{
		OnSubmitRemove?.Invoke();
	}

	private void OnInputCancel()
	{
		OnCancel?.Invoke();
	}
}
