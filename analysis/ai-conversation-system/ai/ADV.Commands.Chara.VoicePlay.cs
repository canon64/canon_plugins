using AIChara;
using Illusion;
using Illusion.Extensions;
using Illusion.Game;
using Manager;
using UnityEngine;

namespace ADV.Commands.Chara;

public class VoicePlay : CommandBase
{
	private enum Type
	{
		Normal,
		Onece,
		Overlap
	}

	public override string[] ArgsLabel => new string[12]
	{
		"No", "Type", "Bundle", "Asset", "Delay", "Fade", "isLoop", "isAsync", "VoiceNo", "Pitch",
		"is2D", "useADV"
	};

	public override string[] ArgsDefault => new string[12]
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
		bool.FalseString,
		bool.TrueString
	};

	public override void Do()
	{
		base.Do();
		int num = 0;
		int no = int.Parse(args[num++]);
		string self = args[num++];
		int num2 = self.Check(ignoreCase: true, Illusion.Utils.Enum<Type>.Names);
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
		bool flag4 = bool.Parse(args[num++]);
		Illusion.Game.Utils.Voice.Setting setting = new Illusion.Game.Utils.Voice.Setting();
		setting.no = voiceNo;
		setting.assetBundleName = assetBundleName;
		setting.assetName = assetName;
		setting.delayTime = delayTime;
		setting.fadeTime = fadeTime;
		setting.isPlayEndDelete = !flag;
		setting.isAsync = isAsync;
		setting.pitch = pitch;
		setting.is2D = is2D;
		Illusion.Game.Utils.Voice.Setting setting2 = setting;
		ChaControl chaControl = null;
		if (chara != null)
		{
			chaControl = chara.chaCtrl;
			if (!flag2)
			{
				setting2.no = chara.voiceNo;
			}
			if (!flag3)
			{
				setting2.pitch = chara.voicePitch;
			}
			setting2.voiceTrans = chara.voiceTrans;
		}
		Transform transform = null;
		switch ((Type)num2)
		{
		case Type.Normal:
			transform = Illusion.Game.Utils.Voice.OnecePlayChara(setting2);
			break;
		case Type.Onece:
			transform = Illusion.Game.Utils.Voice.OnecePlay(setting2);
			break;
		case Type.Overlap:
			transform = Illusion.Game.Utils.Voice.Play(setting2);
			break;
		}
		if (chaControl != null)
		{
			chaControl.SetVoiceTransform(transform);
		}
		if (!(transform != null))
		{
			return;
		}
		AudioSource component = transform.GetComponent<AudioSource>();
		if (component != null)
		{
			if (flag4)
			{
				Singleton<Manager.Sound>.Instance.AudioSettingData3DOnly(component, 1);
			}
			if (flag)
			{
				component.loop = flag;
				base.scenario.loopVoiceList.Add(new TextScenario.LoopVoicePack(setting2.no, chaControl, component));
			}
		}
	}
}
