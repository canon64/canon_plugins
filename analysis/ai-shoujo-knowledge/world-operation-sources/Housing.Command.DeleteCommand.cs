using System.Linq;
using Manager;

namespace Housing.Command;

public class DeleteCommand : ICommand
{
	private class Info
	{
		public ObjectCtrl ObjectCtrl { get; private set; }

		public ObjectCtrl Parent { get; private set; }

		public int Index { get; private set; }

		public Info(ObjectCtrl _objectCtrl)
		{
			ObjectCtrl = _objectCtrl;
			Parent = ObjectCtrl.Parent;
			Index = ObjectCtrl.InfoIndex;
		}
	}

	private Info[] infos;

	public DeleteCommand(ObjectCtrl[] _objectCtrls)
	{
		infos = _objectCtrls.Select((ObjectCtrl v) => new Info(v)).ToArray();
	}

	public void Do()
	{
		Info[] array = infos;
		foreach (Info info in array)
		{
			info.ObjectCtrl.OnDelete();
		}
		Singleton<CraftScene>.Instance.UICtrl.AddUICtrl.Reselect();
	}

	public void Redo()
	{
		Do();
		Singleton<CraftScene>.Instance.UICtrl.AddUICtrl.Reselect();
		Singleton<CraftScene>.Instance.UICtrl.ListUICtrl.UpdateUI();
	}

	public void Undo()
	{
		Info[] array = infos;
		foreach (Info info in array)
		{
			Singleton<Manager.Housing>.Instance.RestoreObject(info.ObjectCtrl, info.Parent, info.Index);
		}
		Singleton<CraftScene>.Instance.UICtrl.AddUICtrl.Reselect();
		Singleton<CraftScene>.Instance.UICtrl.ListUICtrl.UpdateUI();
	}
}
