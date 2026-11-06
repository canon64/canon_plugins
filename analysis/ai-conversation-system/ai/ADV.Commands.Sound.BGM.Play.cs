using Illusion.Game;
using Manager;
using UnityEngine;

namespace ADV.Commands.Sound.BGM;

public class Play : CommandBase
{
	public override string[] ArgsLabel => new string[9] { "Bundle", "Asset", "Delay", "Fade", "isAsync", "Pitch", "PanStereo", "Time", "Volume" };

	public override string[] ArgsDefault => new string[9]
	{
		"sound/data/bgm/00.unity3d",
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
		string assetBundleName = args[num++];
		Illusion.Game.Utils.Sound.SettingBGM setting = new Illusion.Game.Utils.Sound.SettingBGM(assetBundleName);
		args.SafeProc(num++, delegate(string s)
		{
			setting.assetName = s;
		});
		setting.delayTime = float.Parse(args[num++]);
		args.SafeProc(num++, delegate(string s)
		{
			setting.fadeTime = float.Parse(s);
		});
		setting.isAsync = bool.Parse(args[num++]);
		if (Singleton<SoundPlayer>.IsInstance())
		{
			Singleton<SoundPlayer>.Instance.MuteHousingAreaBGM(setting.fadeTime);
		}
		AudioSource audioSource = Illusion.Game.Utils.Sound.Play(setting).GetComponent<AudioSource>();
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
