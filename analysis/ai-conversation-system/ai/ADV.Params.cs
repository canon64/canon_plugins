using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Illusion;

namespace ADV;

public class Params
{
	public virtual List<CommandData> list
	{
		[CompilerGenerated]
		get
		{
			return _list;
		}
	}

	protected object data { get; }

	private List<CommandData> _list { get; } = new List<CommandData>();


	public Params(object data)
	{
		this.data = data;
	}

	public Params(object data, string header)
		: this(data)
	{
		Initialize(header);
	}

	protected virtual void Initialize(string header)
	{
		_list.AddRange(from info in Illusion.Utils.Type.GetPublicFields(data.GetType())
			select new
			{
				info = info,
				command = CommandData.Cast(info.FieldType)
			} into p
			where p.command != CommandData.Command.None
			select new CommandData(p.command, header + p.info.Name, () => p.info.GetValue(data), delegate(object o)
			{
				p.info.SetValue(data, Convert.ChangeType(o, p.info.FieldType));
			}));
		_list.AddRange((from info in Illusion.Utils.Type.GetPublicProperties(data.GetType())
			select new
			{
				info = info,
				command = CommandData.Cast(info.PropertyType)
			} into p
			where p.command != CommandData.Command.None
			select p).Select(p =>
		{
			Func<object> get = null;
			Action<object> set = null;
			if (p.info.CanRead)
			{
				get = () => p.info.GetValue(data, null);
			}
			if (p.info.CanWrite)
			{
				set = delegate(object o)
				{
					p.info.SetValue(data, o);
				};
			}
			return new CommandData(p.command, header + p.info.Name, get, set);
		}));
	}

	public virtual void CreateCommand(List<Program.Transfer> transfers)
	{
		CommandData.CreateCommand(transfers, _list);
	}

	public virtual void Reset(string header = null)
	{
		_list.Clear();
		Initialize(header);
	}

	public virtual void SetParamSync(TextScenario scenario, string key, object value)
	{
		CommandData commandData = _list.FirstOrDefault((CommandData p) => p.key == key);
		if (commandData != null)
		{
			ValData valData = new ValData(ValData.Convert(value, commandData.value.GetType()));
			commandData.value = valData.o;
			scenario.Vars[commandData.key] = valData;
			UpdateReplaceADV(scenario);
		}
	}

	public virtual void SetADV(TextScenario scenario)
	{
		foreach (CommandData item in _list.Where((CommandData p) => p.isVar))
		{
			scenario.Vars[item.key] = new ValData(ValData.Convert(item.value, item.value.GetType()));
		}
		UpdateReplaceADV(scenario);
	}

	public virtual void UpdateReplaceADV(TextScenario scenario)
	{
		foreach (CommandData item in _list.Where((CommandData p) => p.command == CommandData.Command.Replace))
		{
			scenario.Replaces[item.key] = (string)item.value;
		}
	}

	public virtual void ReceiveADV(TextScenario scenario)
	{
		foreach (CommandData item in _list)
		{
			item.ReceiveADV(scenario);
		}
	}
}
