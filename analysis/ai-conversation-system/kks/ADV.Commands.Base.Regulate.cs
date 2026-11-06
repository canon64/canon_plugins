using Illusion;
using Illusion.Extensions;

namespace ADV.Commands.Base;

public class Regulate : CommandBase
{
	private enum Type
	{
		Set,
		Add,
		Sub
	}

	public override string[] ArgsLabel => new string[2] { "Type", "Regulate" };

	public override string[] ArgsDefault => new string[2]
	{
		"Set",
		ADV.Regulate.Control.Next.ToString()
	};

	public override void Do()
	{
		base.Do();
		int num = 0;
		int num2 = args[num++].Check(ignoreCase: true, Utils.Enum<Type>.Names);
		int index = args[num++].Check(ignoreCase: true, Utils.Enum<ADV.Regulate.Control>.Names);
		ADV.Regulate.Control regulate = (ADV.Regulate.Control)Utils.Enum<ADV.Regulate.Control>.Values.GetValue(index);
		switch ((Type)num2)
		{
		case Type.Set:
			base.scenario.regulate.SetRegulate(regulate);
			break;
		case Type.Add:
			base.scenario.regulate.AddRegulate(regulate);
			break;
		case Type.Sub:
			base.scenario.regulate.SubRegulate(regulate);
			break;
		}
	}
}
