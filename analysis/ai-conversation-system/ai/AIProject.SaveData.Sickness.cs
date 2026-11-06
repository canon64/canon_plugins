using System;
using System.Collections.Generic;
using ADV;
using AIProject.Definitions;
using MessagePack;

namespace AIProject.SaveData;

[MessagePackObject(false)]
public class Sickness : ICommandData
{
	private int _id = -1;

	[Key(0)]
	public int ID
	{
		get
		{
			return _id;
		}
		set
		{
			if (_id != value)
			{
				SetID(value);
			}
		}
	}

	[IgnoreMember]
	public int OverwritableID
	{
		get
		{
			return _id;
		}
		set
		{
			SetID(value);
		}
	}

	[IgnoreMember]
	public string Name
	{
		get
		{
			if (_id == -1)
			{
				return "なし";
			}
			if (!AIProject.Definitions.Sickness.NameTable.TryGetValue(ID, out var value))
			{
				return $"無効:{ID.ToString()}";
			}
			return value;
		}
	}

	[Key(1)]
	public TimeSpan ElapsedTime { get; set; }

	[Key(2)]
	public bool UsedMedicine { get; set; }

	[Key(3)]
	public bool Enabled { get; set; }

	[Key(4)]
	public TimeSpan Duration { get; set; }

	public Sickness()
	{
	}

	public Sickness(Sickness source)
	{
		_id = source._id;
		ElapsedTime = source.ElapsedTime;
		UsedMedicine = source.UsedMedicine;
		Enabled = source.Enabled;
	}

	private void SetID(int idValue)
	{
		_id = idValue;
		UsedMedicine = false;
		Enabled = false;
		ElapsedTime = TimeSpan.Zero;
		Duration = TimeSpan.Zero;
	}

	public unsafe IEnumerable<CommandData> CreateCommandData(string head)
	{
		return new CommandData[3]
		{
			new CommandData(CommandData.Command.String, head + string.Format(".{0}", "Name"), new Func<object>(this, (nint)__ldftn(Sickness.get_Name))),
			new CommandData(CommandData.Command.String, head + string.Format(".{0}", "ElapsedTime"), () => ElapsedTime.ToString()),
			new CommandData(CommandData.Command.BOOL, head + string.Format(".{0}", "UsedMedicine"), () => UsedMedicine)
		};
	}
}
