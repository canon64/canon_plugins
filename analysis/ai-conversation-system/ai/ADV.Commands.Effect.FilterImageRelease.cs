namespace ADV.Commands.Effect;

public class FilterImageRelease : CommandBase
{
	public override string[] ArgsLabel => null;

	public override string[] ArgsDefault => null;

	public override void Do()
	{
		base.Do();
		base.scenario.FilterImage.enabled = false;
		base.scenario.FilterImage.sprite = null;
	}
}
