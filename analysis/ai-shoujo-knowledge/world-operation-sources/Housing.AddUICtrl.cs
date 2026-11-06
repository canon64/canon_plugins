using System;
using System.Collections.Generic;
using System.Linq;
using AIProject;
using AIProject.SaveData;
using AIProject.Scene;
using Housing.Add;
using Housing.Command;
using Illusion.Extensions;
using Manager;
using SuperScrollView;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.UI;

namespace Housing;

[Serializable]
public class AddUICtrl : UIDerived
{
	public class FileInfo
	{
		public int no;

		public Manager.Housing.LoadInfo loadInfo;

		public bool Unlock
		{
			get
			{
				Dictionary<int, Dictionary<int, bool>> unlock = Singleton<Game>.Instance.WorldData.HousingData.Unlock;
				if (!unlock.ContainsKey(loadInfo.category))
				{
					return true;
				}
				bool value = false;
				return !unlock[loadInfo.category].TryGetValue(no, out value) || value;
			}
		}

		public void SetUnlock()
		{
			Manager.Housing.RequiredMaterial[] requiredMaterials = loadInfo.requiredMaterials;
			foreach (Manager.Housing.RequiredMaterial requiredMaterial in requiredMaterials)
			{
				StuffItem item = new StuffItem(requiredMaterial.category, requiredMaterial.no, requiredMaterial.num);
				StuffItem.RemoveStorages(item, (IReadOnlyCollection<List<StuffItem>>)(object)new List<StuffItem>[2]
				{
					Singleton<Map>.Instance.Player.PlayerData.ItemList,
					Singleton<Game>.Instance.Environment.ItemListInStorage
				});
			}
			Singleton<Game>.Instance.WorldData.HousingData.Unlock[loadInfo.category][no] = true;
		}
	}

	[SerializeField]
	private CanvasGroup canvasGroup;

	[SerializeField]
	[Header("一覧関係")]
	private LoopListView2 view;

	[SerializeField]
	private GameObject original;

	[SerializeField]
	private int countPerRow = 6;

	[SerializeField]
	[Header("フィルター関係")]
	private Transform togglesRoot;

	[SerializeField]
	private GameObject objCategory;

	[SerializeField]
	private RawImage imageCategory;

	[SerializeField]
	private Text textCategory;

	[SerializeField]
	[Header("動作関係")]
	private Button buttonAdd;

	[SerializeField]
	private ButtonEx buttonClose;

	[SerializeField]
	private Text textItemLimit;

	[SerializeField]
	[Header("情報関係")]
	private CanvasGroup cgInfo;

	[SerializeField]
	private Text textName;

	[SerializeField]
	private Text textText;

	[SerializeField]
	private Image[] imagesInfo;

	[SerializeField]
	[Header("ロック関係")]
	private CanvasGroup cgCraft;

	[SerializeField]
	private MaterialUI materialUI;

	[SerializeField]
	private Button buttonActivate;

	private FileInfo[] fileInfos;

	private bool isForceUpdate;

	private BoolReactiveProperty activeReactive = new BoolReactiveProperty(initialValue: false);

	private IntReactiveProperty categoryReactive = new IntReactiveProperty(-1);

	private IntReactiveProperty selectReactive = new IntReactiveProperty(-1);

	public bool Active
	{
		get
		{
			return activeReactive.Value;
		}
		set
		{
			activeReactive.Value = value;
		}
	}

	private int Category
	{
		get
		{
			return categoryReactive.Value;
		}
		set
		{
			categoryReactive.Value = value;
		}
	}

	private int Select
	{
		get
		{
			return selectReactive.Value;
		}
		set
		{
			selectReactive.Value = value;
		}
	}

	private bool ButtonAddEnable
	{
		set
		{
			buttonAdd.gameObject.SetActiveIfDifferent(value);
		}
	}

	private bool Visible
	{
		set
		{
			canvasGroup.Enable(value);
		}
	}

	public override void Init(UICtrl _uiCtrl, bool _tutorial)
	{
		base.Init(_uiCtrl, _tutorial);
		foreach (KeyValuePair<int, Manager.Housing.CategoryInfo> item in Singleton<Manager.Housing>.Instance.dicCategoryInfo)
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(objCategory, togglesRoot);
			gameObject.SetActive(value: true);
			TabUI tab = gameObject.GetComponent<TabUI>();
			RawImage rawImage = tab.rawImage;
			rawImage.texture = item.Value.Thumbnail;
			Toggle toggleSelect = tab.toggleSelect;
			toggleSelect.isOn = false;
			int c = item.Key;
			(from _b in toggleSelect.OnValueChangedAsObservable()
				where _b
				select _b).Subscribe(delegate
			{
				Category = c;
			});
			toggleSelect.OnPointerEnterAsObservable().TakeUntilDestroy(tab.imageCursor).Subscribe(delegate
			{
				tab.imageCursor.enabled = true;
			})
				.AddTo(gameObject);
			toggleSelect.OnPointerExitAsObservable().TakeUntilDestroy(tab.imageCursor).Subscribe(delegate
			{
				tab.imageCursor.enabled = false;
			})
				.AddTo(gameObject);
		}
		buttonClose.OnClickAsObservable().Subscribe(delegate
		{
			Active = false;
		});
		buttonAdd.OnClickAsObservable().Subscribe(delegate
		{
			FileInfo fileInfo = fileInfos.SafeGet(Select);
			if (fileInfo != null)
			{
				Singleton<UndoRedoManager>.Instance.Do(new AddItemCommand(fileInfo.no));
				Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.OK_L);
			}
			Active = false;
		});
		buttonActivate.OnClickAsObservable().Subscribe(delegate
		{
			ConfirmScene.Sentence = "作成しますか？";
			ConfirmScene.OnClickedYes = delegate
			{
				FileInfo fileInfo = fileInfos.SafeGet(Select);
				if (fileInfo != null)
				{
					fileInfo.SetUnlock();
					Visible = true;
					selectReactive.SetValueAndForceNotify(Select);
					view.RefreshAllShownItem();
					Singleton<Manager.Resources>.Instance.SoundPack.Play(SoundPack.SystemSE.Craft);
				}
			};
			ConfirmScene.OnClickedNo = delegate
			{
				Visible = true;
			};
			Singleton<Game>.Instance.LoadDialog();
			Visible = false;
		});
		activeReactive.Subscribe(delegate(bool _b)
		{
			Visible = _b;
			if (_b)
			{
				isForceUpdate = true;
				selectReactive.SetValueAndForceNotify(-1);
				buttonClose.ClearState();
			}
		});
		categoryReactive.Subscribe(delegate(int _i)
		{
			CreateList(_i);
			Manager.Housing.CategoryInfo value = null;
			if (Singleton<Manager.Housing>.Instance.dicCategoryInfo.TryGetValue(_i, out value))
			{
				textCategory.text = value.name;
				imageCategory.texture = value.Thumbnail;
			}
			else
			{
				Debug.LogErrorFormat("存在しないカテゴリー[{0}]", _i);
			}
		});
		selectReactive.Subscribe(delegate(int _i)
		{
			FileInfo fileInfo = fileInfos.SafeGet(_i);
			if (fileInfo != null)
			{
				if (fileInfo.Unlock)
				{
					cgCraft.Enable(enable: false);
					bool flag = !Singleton<Manager.Housing>.Instance.CheckLimitNum(fileInfo.no);
					ButtonAddEnable = !flag;
					textItemLimit.enabled = flag;
				}
				else
				{
					cgCraft.Enable(enable: true);
					buttonActivate.interactable = materialUI.UpdateUI(fileInfo.loadInfo);
					ButtonAddEnable = false;
					textItemLimit.enabled = false;
				}
				textName.text = fileInfo.loadInfo.name;
				textText.text = fileInfo.loadInfo.text;
				imagesInfo[0].enabled = fileInfo.loadInfo.isAccess;
				imagesInfo[1].enabled = fileInfo.loadInfo.isAction;
				imagesInfo[2].enabled = fileInfo.loadInfo.isHPoint;
				cgInfo.Enable(enable: true);
			}
			else
			{
				cgInfo.Enable(enable: false);
				cgCraft.Enable(enable: false);
				ButtonAddEnable = false;
				textItemLimit.enabled = false;
				if (isForceUpdate)
				{
					view.RefreshAllShownItem();
					isForceUpdate = false;
				}
			}
		});
		categoryReactive.SetValueAndForceNotify(-1);
	}

	public override void UpdateUI()
	{
	}

	public void Reselect()
	{
		selectReactive.SetValueAndForceNotify(selectReactive.Value);
	}

	private void CreateList(int _category)
	{
		int filter = ((_category < 0) ? (-1) : (1 << _category));
		fileInfos = (from v in Singleton<Manager.Housing>.Instance.dicLoadInfo
			where (v.Value.Category & filter) > 0
			where Singleton<Manager.Housing>.Instance.CheckSize(v.Value.size)
			select new FileInfo
			{
				no = v.Key,
				loadInfo = v.Value
			}).ToArray();
		selectReactive.SetValueAndForceNotify(-1);
		int num = ((!fileInfos.IsNullOrEmpty()) ? (fileInfos.Length / countPerRow) : 0);
		if (!fileInfos.IsNullOrEmpty() && fileInfos.Length % countPerRow > 0)
		{
			num++;
		}
		if (!view.IsInit)
		{
			view.InitListView(num, (LoopListView2 _view, int _index) => OnUpdate(_view, _index));
		}
		else if (!view.SetListItemCount(num))
		{
			view.RefreshAllShownItem();
		}
	}

	private LoopListViewItem2 OnUpdate(LoopListView2 _view, int _index)
	{
		if (_index < 0)
		{
			return null;
		}
		LoopListViewItem2 loopListViewItem = _view.NewListViewItem(original.name);
		ItemRow itemRow = loopListViewItem as ItemRow;
		if ((bool)itemRow)
		{
			for (int i = 0; i < countPerRow; i++)
			{
				int index = _index * countPerRow + i;
				itemRow.SetData(i, fileInfos.SafeGet(index), delegate
				{
					Select = index;
				}, Select == index);
			}
		}
		return loopListViewItem;
	}
}
