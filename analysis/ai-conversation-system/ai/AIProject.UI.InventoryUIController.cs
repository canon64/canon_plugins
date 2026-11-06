using System;
using System.Collections.Generic;
using AIProject.SaveData;
using AIProject.Scene;
using Manager;

namespace AIProject.UI;

public class InventoryUIController : InventoryFilterUIController
{
	public Action<StuffItem> OnSubmit { get; set; }

	public bool isConfirm { get; set; } = true;


	protected override void ItemInfoEvent()
	{
		int selectIndex = -1;
		ItemNodeUI selectItem = null;
		base.itemListUI.CurrentChanged += delegate(int i, ItemNodeUI option)
		{
			if (!(option == null))
			{
				selectIndex = i;
				selectItem = option;
				SelectItem(selectItem.Item);
			}
		};
		_itemInfoUI.OnSubmit += delegate
		{
			if (!isConfirm || selectItem.isNone)
			{
				EnterItem(selectIndex, selectItem);
			}
			else
			{
				ConfirmScene.Sentence = _itemInfoUI.ConfirmLabel;
				ConfirmScene.OnClickedYes = delegate
				{
					EnterItem(selectIndex, selectItem);
					base.playSE.Play(SoundPack.SystemSE.OK_L);
				};
				ConfirmScene.OnClickedNo = delegate
				{
					base.playSE.Play(SoundPack.SystemSE.Cancel);
				};
				Singleton<Game>.Instance.LoadDialog();
			}
		};
	}

	private void EnterItem(int selectIndex, ItemNodeUI selectItem)
	{
		StuffItem item = selectItem.Item;
		item.Count -= _itemInfoUI.Count;
		OnSubmit?.Invoke(new StuffItem(item.CategoryID, item.ID, _itemInfoUI.Count));
		bool flag = item.Count <= 0;
		_itemInfoUI.Refresh(item);
		List<StuffItem> list = base.itemList();
		if (flag)
		{
			list.Remove(item);
			base.itemListUI.RemoveItemNode(selectIndex);
			base.itemListUI.ForceSetNonSelect();
			_itemInfoUI.Close();
		}
		_inventoryUI.Refresh();
	}
}
