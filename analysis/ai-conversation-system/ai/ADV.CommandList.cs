using System.Collections.Generic;
using System.Linq;
using ADV.Commands.Base;
using ADV.Commands.Chara;
using ADV.Commands.Effect;
using ADV.Commands.EventCG;
using ADV.Commands.Game;
using ADV.Commands.H;
using ADV.Commands.MapScene;
using ADV.Commands.Object;
using ADV.Commands.Sound.BGM;
using ADV.Commands.Sound.ENV;
using ADV.Commands.Sound.SE2D;
using ADV.Commands.Sound.SE3D;
using UnityEngine;

namespace ADV;

public class CommandList : List<CommandBase>
{
	private TextScenario scenario { get; }

	public CommandList(TextScenario scenario)
	{
		this.scenario = scenario;
	}

	public new void Add(CommandBase item)
	{
		Debug.LogError("CommandList Add Error!");
	}

	public bool Add(ScenarioData.Param item, int currentLine)
	{
		CommandBase commandBase = CommandGet(item.Command);
		if (commandBase == null)
		{
			Debug.LogError("CommandGet Failed:" + item.Command);
			return true;
		}
		commandBase.Initialize(scenario, item.Command, item.Args);
		commandBase.ConvertBeforeArgsProc();
		for (int i = 0; i < commandBase.args.Length; i++)
		{
			commandBase.args[i] = scenario.ReplaceVars(commandBase.args[i]);
		}
		commandBase.localLine = currentLine;
		commandBase.Do();
		base.Add(commandBase);
		return item.Multi;
	}

	public bool Process()
	{
		this.Where((CommandBase item) => item.Process()).ToList().ForEach(delegate(CommandBase item)
		{
			Remove(item);
			item.Result(processEnd: true);
		});
		return this.Any((CommandBase p) => IsWait(p.command));
	}

	public void ProcessEnd()
	{
		ForEach(delegate(CommandBase item)
		{
			item.Result(processEnd: false);
		});
		Clear();
	}

	private static bool IsWait(Command command)
	{
		switch (command)
		{
		case Command.Choice:
		case Command.Fade:
		case Command.FadeWait:
		case Command.CharaKaraokePlay:
		case Command.TaskWait:
			return true;
		default:
			return false;
		}
	}

	public static CommandBase CommandGet(Command command)
	{
		return command switch
		{
			Command.Tag => new Tag(), 
			Command.Text => new Text(), 
			Command.Voice => new Voice(), 
			Command.Motion => new ADV.Commands.Base.Motion(), 
			Command.Expression => new ADV.Commands.Base.Expression(), 
			Command.VAR => new VAR(), 
			Command.RandomVar => new RandomVar(), 
			Command.IF => new IF(), 
			Command.Switch => new Switch(), 
			Command.Calc => new Calc(), 
			Command.Clamp => new Clamp(), 
			Command.Min => new Min(), 
			Command.Max => new Max(), 
			Command.Lerp => new Lerp(), 
			Command.LerpAngle => new LerpAngle(), 
			Command.InverseLerp => new InverseLerp(), 
			Command.LerpV3 => new LerpV3(), 
			Command.LerpAngleV3 => new LerpAngleV3(), 
			Command.Open => new Open(), 
			Command.Close => new Close(), 
			Command.Jump => new Jump(), 
			Command.Choice => new Choice(), 
			Command.Wait => new Wait(), 
			Command.TextClear => new Clear(), 
			Command.FontColor => new FontColor(), 
			Command.Scene => new Scene(), 
			Command.Regulate => new ADV.Commands.Base.Regulate(), 
			Command.Replace => new Replace(), 
			Command.Reset => new Reset(), 
			Command.Vector => new Vector(), 
			Command.Format => new Format(), 
			Command.NullLoad => new NullLoad(), 
			Command.NullRelease => new NullRelease(), 
			Command.NullSet => new NullSet(), 
			Command.InfoAudioEco => new InfoAudioEco(), 
			Command.InfoAnimePlay => new InfoAnimePlay(), 
			Command.Fade => new ADV.Commands.Effect.Fade(), 
			Command.SceneFade => new ADV.Commands.Effect.SceneFade(), 
			Command.SceneFadeRegulate => new SceneFadeRegulate(), 
			Command.FadeWait => new FadeWait(), 
			Command.FilterImageLoad => new FilterImageLoad(), 
			Command.FilterImageRelease => new FilterImageRelease(), 
			Command.FilterSet => new FilterSet(), 
			Command.Filter => new Filter(), 
			Command.BGMPlay => new ADV.Commands.Sound.BGM.Play(), 
			Command.BGMStop => new ADV.Commands.Sound.BGM.Stop(), 
			Command.EnvPlay => new ADV.Commands.Sound.ENV.Play(), 
			Command.EnvStop => new ADV.Commands.Sound.ENV.Stop(), 
			Command.SE2DPlay => new ADV.Commands.Sound.SE2D.Play(), 
			Command.SE2DStop => new ADV.Commands.Sound.SE2D.Stop(), 
			Command.SE3DPlay => new ADV.Commands.Sound.SE3D.Play(), 
			Command.SE3DStop => new ADV.Commands.Sound.SE3D.Stop(), 
			Command.CharaStand => new StandPosition(), 
			Command.CharaStandFind => new StandFindPosition(), 
			Command.CharaPositionAdd => new AddPosition(), 
			Command.CharaPositionSet => new SetPosition(), 
			Command.CharaPositionLocalAdd => new AddPositionLocal(), 
			Command.CharaPositionLocalSet => new SetPositionLocal(), 
			Command.CharaMotion => new ADV.Commands.Chara.Motion(), 
			Command.CharaMotionWait => new MotionWait(), 
			Command.CharaMotionLayerWeight => new MotionLayerWeight(), 
			Command.CharaMotionSetParam => new MotionSetParam(), 
			Command.CharaMotionIKSetPartner => new MotionIKSetPartner(), 
			Command.CharaExpression => new ADV.Commands.Chara.Expression(), 
			Command.CharaFixEyes => new FixEyes(), 
			Command.CharaFixMouth => new FixMouth(), 
			Command.CharaGetShape => new GetShape(), 
			Command.CharaClothState => new ClothState(), 
			Command.CharaSiruState => new SiruState(), 
			Command.CharaVoicePlay => new VoicePlay(), 
			Command.CharaVoiceStop => new VoiceStop(), 
			Command.CharaVoiceStopAll => new VoiceStopAll(), 
			Command.CharaVoiceWait => new VoiceWait(), 
			Command.CharaVoiceWaitAll => new VoiceWaitAll(), 
			Command.CharaLookEyes => new LookEyes(), 
			Command.CharaLookEyesTarget => new LookEyesTarget(), 
			Command.CharaLookEyesTargetChara => new LookEyesTargetChara(), 
			Command.CharaLookNeck => new LookNeck(), 
			Command.CharaLookNeckTarget => new LookNeckTarget(), 
			Command.CharaLookNeckTargetChara => new LookNeckTargetChara(), 
			Command.CharaLookNeckSkip => new LookNeckSkip(), 
			Command.CharaItemCreate => new ItemCreate(), 
			Command.CharaItemDelete => new ItemDelete(), 
			Command.CharaItemAnime => new ItemAnime(), 
			Command.CharaItemFind => new ItemFind(), 
			Command.EventCGSetting => new Setting(), 
			Command.EventCGRelease => new Release(), 
			Command.EventCGNext => new Next(), 
			Command.ObjectCreate => new Create(), 
			Command.ObjectLoad => new Load(), 
			Command.ObjectDelete => new Delete(), 
			Command.ObjectPosition => new ADV.Commands.Object.Position(), 
			Command.ObjectRotation => new Rotation(), 
			Command.ObjectScale => new Scale(), 
			Command.ObjectParent => new Parent(), 
			Command.ObjectComponent => new ADV.Commands.Object.Component(), 
			Command.ObjectAnimeParam => new AnimeParam(), 
			Command.CharaActive => new CharaActive(), 
			Command.CharaVisible => new CharaVisible(), 
			Command.CharaColor => new CharaColor(), 
			Command.AddCollider => new AddCollider(), 
			Command.ColliderSetActive => new ColliderSetActive(), 
			Command.AddNavMeshAgent => new AddNavMeshAgent(), 
			Command.NavMeshAgentSetActive => new NavMeshAgentSetActive(), 
			Command.CameraLookAt => new ADV.Commands.Game.CameraLookAt(), 
			Command.MozVisible => new MozVisible(), 
			Command.BundleCheck => new BundleCheck(), 
			Command.Prob => new Prob(), 
			Command.Probs => new Probs(), 
			Command.FormatVAR => new FormatVAR(), 
			Command.Task => new Task(), 
			Command.TaskWait => new TaskWait(), 
			Command.TaskEnd => new TaskEnd(), 
			Command.Log => new ADV.Commands.Base.Log(), 
			Command.CharaSetShape => new SetShape(), 
			Command.CharaCoordinateChange => new CoordinateChange(), 
			Command.InfoAudio => new InfoAudio(), 
			Command.ReplaceLanguage => new ReplaceLanguage(), 
			Command.InfoText => new InfoText(), 
			Command.SendCommandData => new SendCommandData(), 
			Command.SendCommandDataList => new SendCommandDataList(), 
			Command.PlaySystemSE => new PlaySystemSE(), 
			Command.PlayActionSE => new PlayActionSE(), 
			Command.PlayEnviroSE => new PlayEnviroSE(), 
			Command.PlayFootStepSE => new PlayFootStepSE(), 
			Command.InventoryCheck => new InventoryCheck(), 
			Command.SetPresent => new SetPresent(), 
			Command.SetPresentBirthday => new SetPresentBirthday(), 
			Command.ClearItems => new ClearItems(), 
			Command.AddItem => new AddItem(), 
			Command.RemoveItem => new RemoveItem(), 
			Command.ChangeADVFixedAngleCamera => new ChangeADVFixedAngleCamera(), 
			Command.InventoryGiveItem => new InventoryGiveItem(), 
			Command.SetItemScrounge => new SetItemScrounge(), 
			Command.CharaSetting => new CharaSetting(), 
			Command.AddItemInPlayer => new AddItemInPlayer(), 
			_ => null, 
		};
	}
}
