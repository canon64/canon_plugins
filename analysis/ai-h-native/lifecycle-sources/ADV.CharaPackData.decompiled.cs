using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Manager;

namespace ADV;

public class CharaPackData : IPack
{
	private List<CommandData> _commandList = new List<CommandData>();

	bool IPack.isCommandListVisibleEnabled
	{
		[CompilerGenerated]
		get
		{
			return isCommandListVisibleEnabled;
		}
	}

	public Action onComplete { get; set; }

	public Dictionary<string, ValData> Vars { get; set; }

	public ICommandData[] commandData { get; private set; }

	public IParams[] param { get; private set; }

	public IReadOnlyCollection<CommandData> commandList
	{
		[CompilerGenerated]
		get
		{
			return _commandList;
		}
	}

	private bool isCommandListVisibleEnabled { get; set; }

	public void SetCommandData(params ICommandData[] commandData)
	{
		this.commandData = commandData.Where((ICommandData p) => p != null).ToArray();
	}

	public void SetParam(params IParams[] param)
	{
		this.param = param.Where((IParams p) => p != null).ToArray();
	}

	public virtual void Init()
	{
		if (Singleton<SoundPlayer>.IsInstance())
		{
			Singleton<SoundPlayer>.Instance.BGMPlayActive = false;
		}
	}

	public virtual void Release()
	{
		if (Singleton<SoundPlayer>.IsInstance())
		{
			Singleton<SoundPlayer>.Instance.ActivateMapBGM();
		}
	}

	public virtual List<Program.Transfer> Create()
	{
		Vars = null;
		_commandList.Clear();
		List<Program.Transfer> list = Program.Transfer.NewList();
		if (commandData != null)
		{
			ICommandData[] array = commandData;
			foreach (ICommandData self in array)
			{
				self.AddList(_commandList, "G_");
			}
			CommandData.CreateCommand(list, _commandList);
		}
		if (param != null)
		{
			IParams[] array2 = param;
			foreach (IParams obj in array2)
			{
				obj.param.Reset();
				obj.param.CreateCommand(list);
				_commandList.AddRange(obj.param.list);
			}
		}
		return list;
	}

	public virtual void Receive(TextScenario scenario)
	{
		foreach (CommandData command in _commandList)
		{
			command.ReceiveADV(scenario);
		}
		Vars = scenario.Vars;
		onComplete?.Invoke();
	}

	public void CommandListVisibleEnabled(bool enabled)
	{
		isCommandListVisibleEnabled = enabled;
	}

	void IPack.CommandListVisibleEnabledDefault()
	{
		isCommandListVisibleEnabled = true;
	}
}
