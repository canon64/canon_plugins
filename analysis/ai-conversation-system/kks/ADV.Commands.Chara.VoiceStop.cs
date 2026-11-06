using Manager;

namespace ADV.Commands.Chara;

public class VoiceStop : CommandBase
{
	public override string[] ArgsLabel => new string[1] { "No" };

	public override string[] ArgsDefault => new string[1] { int.MaxValue.ToString() };

	public override void Do()
	{
		base.Do();
		CharaData chara = base.scenario.commandController.GetChara(int.Parse(args[0]));
		Voice.Stop(chara.voiceNo, chara.voiceTrans);
		base.scenario.loopVoiceList.RemoveAll((TextScenario.LoopVoicePack item) => item.voiceNo == chara.voiceNo);
	}
}
