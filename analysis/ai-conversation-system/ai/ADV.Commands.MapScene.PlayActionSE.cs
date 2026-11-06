using Illusion;
using Manager;
using UnityEngine;

namespace ADV.Commands.MapScene;

public class PlayActionSE : CommandBase
{
	public override string[] ArgsLabel => new string[4] { "ID", "FadeTime", "Type", "Pos" };

	public override string[] ArgsDefault => new string[4]
	{
		"0",
		"0",
		Manager.Sound.Type.GameSE3D.ToString(),
		string.Empty
	};

	public override void Do()
	{
		base.Do();
		int num = 0;
		int key = int.Parse(args[num++]);
		float fadeTime = float.Parse(args[num++]);
		string text = args[num++];
		string posStr = string.Empty;
		args.SafeProc(num++, delegate(string s)
		{
			posStr = s;
		});
		if (!int.TryParse(text, out var result))
		{
			result = Illusion.Utils.Enum<Manager.Sound.Type>.FindIndex(text, ignoreCase: true);
		}
		AudioSource audioSource = Singleton<Manager.Resources>.Instance.SoundPack.Play(key, (Manager.Sound.Type)result, fadeTime);
		if (audioSource != null)
		{
			if (base.scenario.commandController.V3Dic.TryGetValue(posStr, out var value))
			{
				audioSource.transform.position = value;
			}
			else if (base.scenario.AdvCamera != null)
			{
				audioSource.transform.position = base.scenario.AdvCamera.transform.position;
			}
		}
	}
}
