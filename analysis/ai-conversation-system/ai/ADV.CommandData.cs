using System;
using System.Collections.Generic;
using Illusion.Elements.Reference;

namespace ADV;

public class CommandData : Pointer<object>
{
	public enum Command
	{
		None,
		Replace,
		Int,
		String,
		BOOL,
		FLOAT
	}

	public bool isVar
	{
		get
		{
			Command command = this.command;
			if (command == Command.None || command == Command.Replace)
			{
				return false;
			}
			return true;
		}
	}

	public Command command { get; private set; }

	public string key { get; private set; }

	public CommandData(Command command, string key, Func<object> get, Action<object> set = null)
		: base(get, set)
	{
		this.command = command;
		this.key = key;
	}

	public bool ReceiveADV(TextScenario scenario)
	{
		if (!isVar)
		{
			return false;
		}
		if (!scenario.Vars.TryGetValue(key, out var valData))
		{
			return false;
		}
		object obj = valData?.o;
		if (obj == null)
		{
			return false;
		}
		base.value = obj;
		return true;
	}

	public static void CreateCommand(List<Program.Transfer> transfers, IReadOnlyCollection<CommandData> collection)
	{
		foreach (CommandData item in collection)
		{
			if (item.value != null)
			{
				switch (item.command)
				{
				case Command.Replace:
					transfers.Add(Program.Transfer.Create(true, ADV.Command.Replace, item.key, (string)item.value));
					break;
				case Command.Int:
					transfers.Add(Program.Transfer.Create(true, ADV.Command.VAR, "int", item.key, item.value?.ToString()));
					break;
				case Command.String:
					transfers.Add(Program.Transfer.Create(true, ADV.Command.VAR, "string", item.key, item.value?.ToString()));
					break;
				case Command.BOOL:
					transfers.Add(Program.Transfer.Create(true, ADV.Command.VAR, "bool", item.key, item.value?.ToString()));
					break;
				case Command.FLOAT:
					transfers.Add(Program.Transfer.Create(true, ADV.Command.VAR, "float", item.key, item.value?.ToString()));
					break;
				}
			}
		}
	}

	public static Command Cast(object o)
	{
		if (o is string)
		{
			return Command.String;
		}
		if (o is int)
		{
			return Command.Int;
		}
		if (o is bool)
		{
			return Command.BOOL;
		}
		if (o is float)
		{
			return Command.FLOAT;
		}
		return Command.None;
	}

	public static Command Cast(Type type)
	{
		if (type == typeof(string))
		{
			return Command.String;
		}
		if (type == typeof(int))
		{
			return Command.Int;
		}
		if (type == typeof(bool))
		{
			return Command.BOOL;
		}
		if (type == typeof(float))
		{
			return Command.FLOAT;
		}
		return Command.None;
	}
}
