using System.Collections.Generic;
using System.Linq;
using AIProject;
using AIProject.SaveData;
using AIProject.UI;
using AIProject.UI.Viewer;
using Manager;

namespace ADV.Commands.MapScene;

public abstract class InventoryBase : CommandBase
{
	protected bool isClose { get; private set; }

	protected StuffItem Item { get; private set; }

	protected void OpenInventory(int cnt, List<StuffItem> itemList)
	{
		MapUIContainer.ReserveSystemMenuMode(SystemMenuUI.MenuMode.InventoryEnter);
		SystemMenuUI systemUI = MapUIContainer.SystemMenuUI;
		InventoryUIController inventoryUI = systemUI.InventoryEnterUI;
		inventoryUI.isConfirm = true;
		inventoryUI.CountViewerVisible(isVisible: true);
		inventoryUI.itemList = () => itemList;
		inventoryUI.SetItemFilter(ToFilter(GetArgToSplitLastTable(cnt)));
		inventoryUI.OnSubmit = delegate(StuffItem item)
		{
			Item = item;
			inventoryUI?.OnClose();
		};
		inventoryUI.OnClose = delegate
		{
			inventoryUI.OnSubmit = null;
			inventoryUI.IsActiveControl = false;
			systemUI.IsActiveControl = false;
			Singleton<Input>.Instance.FocusLevel = 0;
			Singleton<Input>.Instance.ReserveState(Input.ValidType.UI);
			isClose = true;
			inventoryUI.OnClose = null;
		};
		MapUIContainer.SetActiveSystemMenuUI(active: true);
	}

	private static InventoryFacadeViewer.ItemFilter[] ToFilter(string[][] arrays)
	{
		return arrays.Select((string[] x) => new InventoryFacadeViewer.ItemFilter(int.Parse(x[0]), x.Skip(1).Select(int.Parse).ToArray())).ToArray();
	}
}
