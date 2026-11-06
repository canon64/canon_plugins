using Illusion;
using Illusion.Game;
using UnityEngine;

namespace ADV.Commands.Sound.BGM;

public class Play : CommandBase
{
	public override string[] ArgsLabel => new string[9] { "Bundle", "Asset", "Delay", "Fade", "isAsync", "Pitch", "PanStereo", "Time", "Volume" };

	public override string[] ArgsDefault => new string[9]
	{
		string.Empty,
		string.Empty,
		"0",
		string.Empty,
		bool.TrueString,
		string.Empty,
		string.Empty,
		string.Empty,
		string.Empty
	};

	public override void Do()
	{
		base.Do();
		int num = 0;
		string text = args[num++];
		Illusion.Game.Utils.Sound.SettingBGM setting;
		if (int.TryParse(text, out var result))
		{
			setting = new Illusion.Game.Utils.Sound.SettingBGM(result);
		}
		else
		{
			result = Illusion.Utils.Enum<Illusion.Game.BGM>.FindIndex(text, ignoreCase: true);
			if (result != -1)
			{
				setting = new Illusion.Game.Utils.Sound.SettingBGM(result);
			}
			else
			{
				setting = new Illusion.Game.Utils.Sound.SettingBGM(text);
			}
		}
		args.SafeProc(num++, delegate(string s)
		{
			setting.loader.asset = s;
		});
		float.Parse(args[num++]);
		args.SafeProc(num++, delegate(string s)
		{
			setting.loader.fadeTime = float.Parse(s);
		});
		bool.Parse(args[num++]);
		AudioSource audioSource = Illusion.Game.Utils.Sound.Play(setting);
		if (audioSource != null)
		{
			args.SafeProc(num++, delegate(string s)
			{
				audioSource.pitch = float.Parse(s);
			});
			args.SafeProc(num++, delegate(string s)
			{
				audioSource.panStereo = float.Parse(s);
			});
			args.SafeProc(num++, delegate(string s)
			{
				audioSource.time = float.Parse(s);
			});
			args.SafeProc(num++, delegate(string s)
			{
				audioSource.volume = float.Parse(s);
			});
		}
	}
}
