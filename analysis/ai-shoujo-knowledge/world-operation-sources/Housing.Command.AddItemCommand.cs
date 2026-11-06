using Manager;

namespace Housing.Command;

public class AddItemCommand : ICommand
{
	private int id;

	private ObjectCtrl objectCtrl;

	public AddItemCommand(int _id)
	{
		id = _id;
	}

	public void Do()
	{
		objectCtrl = Singleton<Manager.Housing>.Instance.AddObject(id);
		bool flag = Singleton<Manager.Housing>.Instance.CheckOverlap(objectCtrl as OCItem);
		Singleton<CraftScene>.Instance.UICtrl.ListUICtrl.AddList(objectCtrl);
		if (flag)
		{
			Singleton<CraftScene>.Instance.UICtrl.ListUICtrl.RefreshList();
		}
		Singleton<CraftScene>.Instance.UICtrl.AddUICtrl.Reselect();
	}

	public void Redo()
	{
		bool flag = Singleton<Manager.Housing>.Instance.RestoreObject(objectCtrl, null, -1);
		Singleton<CraftScene>.Instance.UICtrl.AddUICtrl.Reselect();
		Singleton<CraftScene>.Instance.UICtrl.ListUICtrl.AddList(objectCtrl);
		if (flag)
		{
			Singleton<CraftScene>.Instance.UICtrl.ListUICtrl.RefreshList();
		}
	}

	public void Undo()
	{
		Singleton<CraftScene>.Instance.UICtrl.ListUICtrl.isOnFuncSkip = true;
		Singleton<CraftScene>.Instance.UICtrl.ListUICtrl.RemoveList(objectCtrl);
		Singleton<CraftScene>.Instance.UICtrl.AddUICtrl.Reselect();
		Singleton<CraftScene>.Instance.UICtrl.ListUICtrl.RefreshList();
	}
}
