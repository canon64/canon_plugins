namespace ADV.Commands.Base;

public class Jump : CommandBase
{
	public override string[] ArgsLabel => new string[1] { "Tag" };

	public override string[] ArgsDefault => null;

	public override void Do()
	{
		base.Do();
		base.scenario.SearchTagJumpOrOpenFile(args[0], base.localLine);
	}
}
