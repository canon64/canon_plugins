namespace ADV.Commands.Base;

public class Tag : CommandBase
{
	public override string[] ArgsLabel => new string[1] { "Label" };

	public override string[] ArgsDefault => null;
}
