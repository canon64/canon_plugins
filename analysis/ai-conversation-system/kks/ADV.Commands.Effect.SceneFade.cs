using Illusion.Extensions;
using Manager;

namespace ADV.Commands.Effect;

public class SceneFade : CommandBase
{
	private FadeCanvas.Fade fadeType;

	public override string[] ArgsLabel => new string[5] { "Fade", "Time", "Color", "Bundle", "Asset" };

	public override string[] ArgsDefault => new string[5]
	{
		"in",
		string.Empty,
		string.Empty,
		string.Empty,
		"none"
	};

	public override void Do()
	{
		base.Do();
		int num = 0;
		fadeType = (args[num++].Compare("in", ignoreCase: true) ? FadeCanvas.Fade.In : FadeCanvas.Fade.Out);
		Scene.sceneFadeCanvas.StartFade(fadeType);
		args.SafeProc(num++, delegate(string s)
		{
			Scene.sceneFadeCanvas.time = float.Parse(s);
		});
		string self = args[num++];
		if (!self.IsNullOrEmpty())
		{
			Scene.sceneFadeCanvas.SetColor(self.GetColor());
		}
	}

	public override bool Process()
	{
		base.Process();
		return Scene.sceneFadeCanvas.isEnd;
	}

	public override void Result(bool processEnd)
	{
		Scene.sceneFadeCanvas.Force(fadeType);
		Scene.sceneFadeCanvas.DefaultTime();
	}
}
