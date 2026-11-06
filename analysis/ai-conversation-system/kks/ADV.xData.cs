namespace ADV;

public class xData
{
	public CommandBase commandBase { get; }

	private bool doit { get; set; }

	public xData(CommandBase commandBase)
	{
		this.commandBase = commandBase;
	}

	public void Initialize(TextScenario scenario, ScenarioData.Param param, int currentLine)
	{
		commandBase.Initialize(scenario, param.Command, param.Args);
		commandBase.ConvertBeforeArgsProc();
		for (int i = 0; i < commandBase.args.Length; i++)
		{
			commandBase.args[i] = scenario.ReplaceVars(commandBase.args[i]);
		}
		commandBase.localLine = currentLine;
	}

	public void Do()
	{
		if (!doit)
		{
			commandBase.Do();
			doit = true;
		}
	}

	public bool Process()
	{
		Do();
		return commandBase.Process();
	}

	public void End()
	{
		Do();
		commandBase.Result(processEnd: true);
	}

	public void ForceEnd()
	{
		Do();
		commandBase.Result(processEnd: false);
	}

	public bool IsWait()
	{
		return IsWait(commandBase.command);
	}

	private static bool IsWait(Command command)
	{
		switch (command)
		{
		case Command.Choice:
		case Command.Selection:
		case Command.WaitAbs:
		case Command.TaskWait:
		case Command.FadeWait:
		case Command.CharaKaraokePlay:
		case Command.OpenEntryPlayer:
		case Command.OpenWhereLive:
		case Command.GetTopic:
		case Command.KoikatsuPointAdd:
			return true;
		default:
			return false;
		}
	}
}
