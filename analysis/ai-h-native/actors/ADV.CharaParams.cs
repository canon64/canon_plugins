using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AIProject;
using UnityEngine;

namespace ADV;

public class CharaParams : Params
{
	private List<CommandData> cachedList;

	private List<CommandData> _addList;

	public string HEADER { get; } = string.Empty;


	public string HEADER_PARAM => HEADER + "_";

	public override List<CommandData> list
	{
		[CompilerGenerated]
		get
		{
			return cachedList ?? (cachedList = base.list.Concat(addList).ToList());
		}
	}

	public List<CommandData> addList
	{
		get
		{
			if (_addList != null)
			{
				return _addList;
			}
			_addList = new List<CommandData>(((ICommandData)base.data).CreateCommandData(HEADER_PARAM));
			CreateCommandData_Actor(_addList);
			return _addList;
		}
	}

	public int charaID
	{
		get
		{
			if (actor == null)
			{
				return 0;
			}
			if (actor is AgentActor)
			{
				if (actor != null && actor.ChaControl != null && actor.ChaControl.chaFile != null && actor.ChaControl.chaFile.parameter != null)
				{
					return actor.ChaControl.chaFile.parameter.personality;
				}
				Debug.LogError("ADV Parameter ActorChara Data none");
			}
			return actor.ID;
		}
	}

	public Actor actor { get; private set; }

	public CharaParams(ICommandData commandData, string HEADER)
		: base(commandData)
	{
		this.HEADER = HEADER;
		Initialize(HEADER_PARAM);
	}

	public void Bind(Actor actor)
	{
		this.actor = actor;
	}

	public void CreateCommandData_Actor(List<CommandData> list)
	{
		if (!(actor == null))
		{
			list.Add(new CommandData(CommandData.Command.String, HEADER_PARAM + string.Format("[{0}]", "CharaName"), () => actor.CharaName));
			list.Add(new CommandData(CommandData.Command.Replace, HEADER, () => actor.CharaName));
		}
	}

	public override void Reset(string header = null)
	{
		_addList = null;
		cachedList = null;
	}
}
