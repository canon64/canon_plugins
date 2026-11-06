using Illusion;
using Illusion.Extensions;
using Illusion.Game;
using UnityEngine;

namespace ADV.Commands.Chara;

public class VoicePlay : CommandBase
{
	private enum Type
	{
		Normal,
		Once,
		Overlap
	}

	public override string[] ArgsLabel => new string[11]
	{
		"No", "Type", "Bundle", "Asset", "Delay", "Fade", "isLoop", "isAsync", "VoiceNo", "Pitch",
		"is2D"
	};

	public override string[] ArgsDefault => new string[11]
	{
		int.MaxValue.ToString(),
		Type.Normal.ToString(),
		string.Empty,
		string.Empty,
		"0",
		"0",
		bool.FalseString,
		bool.TrueString,
		string.Empty,
		string.Empty,
		bool.FalseString
	};

	public override void Do()
	{
		base.Do();
		int num = 0;
		int no = int.Parse(args[num++]);
		int num2 = args[num++].Check(ignoreCase: true, Illusion.Utils.Enum<Type>.Names);
		CharaData chara = base.scenario.commandController.GetChara(no);
		string assetBundleName = args[num++];
		string assetName = args[num++];
		float delayTime = float.Parse(args[num++]);
		float fadeTime = float.Parse(args[num++]);
		bool flag = bool.Parse(args[num++]);
		bool isAsync = bool.Parse(args[num++]);
		int voiceNo = 0;
		bool flag2 = args.SafeProc(num++, delegate(string s)
		{
			voiceNo = int.Parse(s);
		});
		float pitch = 1f;
		bool flag3 = args.SafeProc(num++, delegate(string s)
		{
			pitch = float.Parse(s);
		});
		bool is2D = bool.Parse(args[num++]);
		Illusion.Game.Utils.Voice.Setting setting = new Illusion.Game.Utils.Voice.Setting
		{
			no = voiceNo,
			assetBundleName = assetBundleName,
			assetName = assetName,
			delayTime = delayTime,
			fadeTime = fadeTime,
			isPlayEndDelete = !flag,
			isAsync = isAsync,
			pitch = pitch,
			is2D = is2D
		};
		ChaControl chaControl = null;
		if (chara != null)
		{
			chaControl = chara.chaCtrl;
			if (!flag2)
			{
				setting.no = chara.voiceNo;
			}
			if (!flag3)
			{
				setting.pitch = chara.voicePitch;
			}
			setting.voiceTrans = chara.voiceTrans;
		}
		AudioSource audioSource = null;
		switch ((Type)num2)
		{
		case Type.Normal:
			audioSource = Illusion.Game.Utils.Voice.OncePlayChara(setting);
			break;
		case Type.Once:
			audioSource = Illusion.Game.Utils.Voice.OncePlay(setting);
			break;
		case Type.Overlap:
			audioSource = Illusion.Game.Utils.Voice.Play(setting);
			break;
		}
		if (chaControl != null)
		{
			chaControl.SetLipSync(audioSource);
		}
		if (audioSource != null && flag)
		{
			audioSource.loop = flag;
			base.scenario.loopVoiceList.Add(new TextScenario.LoopVoicePack(setting.no, chaControl, audioSource));
		}
	}
}
