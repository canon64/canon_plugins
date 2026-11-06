using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ADV;

public class CommandList : List<xData>, ICommandListProc, IEnumerable<xData>, IEnumerable
{
	private TextScenario scenario { get; }

	public CommandList(TextScenario scenario)
	{
		this.scenario = scenario;
	}

	public bool Add(ScenarioData.Param param, int currentLine)
	{
		CommandBase commandBase = CommandGenerator.Create(param.Command);
		if (commandBase == null)
		{
			return true;
		}
		xData xData2 = new xData(commandBase);
		xData2.Initialize(scenario, param, currentLine);
		xData2.Do();
		Add(xData2);
		return param.Multi;
	}

	public bool Process()
	{
		this.Where((xData item) => item.Process()).ToList().ForEach(delegate(xData item)
		{
			Remove(item);
			item.End();
		});
		return this.Any((xData item) => item.IsWait());
	}

	public void ProcessEnd()
	{
		ForEach(delegate(xData item)
		{
			item.ForceEnd();
		});
		Clear();
	}
}
