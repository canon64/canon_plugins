using Illusion.Extensions;
using UnityEngine;

namespace ADV.Commands.Effect;

public class Fade : CommandBase
{
	private Color color = Color.clear;

	private float time;

	private float timer;

	private bool fadeIn = true;

	private bool isFront = true;

	private bool isInitColorSet;

	private ADVFade advFade;

	public override string[] ArgsLabel => new string[5] { "Fade", "Time", "Color", "Type", "isInit" };

	public override string[] ArgsDefault => new string[5]
	{
		"in",
		"0",
		"clear",
		"front",
		bool.TrueString
	};

	public override void Do()
	{
		base.Do();
		timer = 0f;
		int num = 0;
		fadeIn = args[num++].Compare("in", ignoreCase: true);
		time = float.Parse(args[num++]);
		color = args[num++].GetColor();
		isFront = args[num++].Compare("front", ignoreCase: true);
		if (fadeIn)
		{
			base.scenario.FadeIn(time);
		}
		else
		{
			base.scenario.FadeOut(time);
		}
	}

	public override bool Process()
	{
		base.Process();
		return !base.scenario.Fading;
	}

	public override void Result(bool processEnd)
	{
		base.Result(processEnd);
		if (!processEnd)
		{
			if (fadeIn)
			{
				base.scenario.FadeIn(0f);
			}
			else
			{
				base.scenario.FadeOut(0f);
			}
		}
	}
}
