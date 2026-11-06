using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AIChara;
using AIProject;
using AIProject.Definitions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CharaUtils;
using HarmonyLib;
using Manager;
using RootMotion;
using RootMotion.FinalIK;
using UnityEngine;

namespace AI_BetterHScenes;

[BepInPlugin("AI_BetterHScenes", "AI_BetterHScenes", "2.6.5")]
[BepInProcess("AI-Syoujyo")]
public class AI_BetterHScenes : BaseUnityPlugin
{
	private enum ProcMode
	{
		Aibu,
		Houshi,
		Sonyu,
		Spnking,
		Masturbation,
		Peeping,
		Les,
		MultiPlay_F2M1
	}

	private enum Effector
	{
		LeftHand = 5,
		RightHand,
		LeftFoot,
		RightFoot
	}

	public const string VERSION = "2.6.5";

	public static ManualLogSource Logger;

	private static readonly Random rand = new Random();

	public static HScene hScene;

	public static HSceneManager manager;

	public static HSceneFlagCtrl hFlagCtrl;

	private static HSceneSprite hSprite;

	private static VirtualCameraController hCamera;

	private static Traverse hSceneTrav;

	private static Traverse listTrav;

	private static Harmony harmony;

	public static List<ChaControl> characters;

	public static List<ChaControl> maleCharacters;

	public static List<ChaControl> femaleCharacters;

	public static List<ChaControl> shouldCleanUp;

	public static List<EyeNeck> maleMotionList = new List<EyeNeck>();

	private static GameObject map;

	private static Light sun;

	private static EnviroSky enviroSky;

	private static List<SkinnedCollisionHelper> collisionHelpers;

	private static bool OnHStart;

	internal static bool activeDraggerUI;

	internal static bool activeAnimationUI;

	internal static bool activeConfirmDeleteUI;

	private static bool patched;

	private static bool cameraShouldLock;

	private static bool oldMapState;

	private static LightShadows oldSunShadowsState;

	private static bool shouldApplyOffsets;

	public static string currentMotion;

	public static int hProcMode = 0;

	public static bool bBaseReplacement = false;

	public static bool bIdleAfterException = false;

	public static bool bFootJobException = false;

	public static bool bTwoFootException = false;

	public static bool useReplacements = false;

	public static bool applyKissOffset = false;

	private static readonly List<string> siriReplaceList = new List<string> { "ais_f_02", "ais_f_13", "ais_f_31", "ais_f_43", "ait_f_00", "ait_f_07" };

	private static readonly List<string> kosiReplaceList = new List<string> { "ais_f_27", "ais_f_28", "ais_f_29", "ais_f_35", "ais_f_36", "ais_f_37", "ais_f_38" };

	private static readonly List<string> huggingReplaceList = new List<string> { "h2s_f_12", "h2s_f_13" };

	private static readonly List<string> footReplaceList = new List<string> { "aih_f_08", "aih_f_24", "aih_f_28" };

	private static readonly List<string> rightKokanReplaceList = new List<string> { "aia_f_14", "aia_f_21" };

	private static readonly List<string> leftKokanReplaceList = new List<string> { "aia_f_15", "aia_f_20" };

	private static readonly List<string> rightKosiReplaceList = new List<string> { "aia_f_09" };

	private static readonly List<string> leftKosiReplaceList = new List<string> { "aia_f_16" };

	private static readonly List<string> kissCorrectionList = new List<string> { "aia_f_00", "aia_f_01", "aia_f_07", "aia_f_11", "aia_f_12", "h2a_f_00" };

	private static ConfigEntry<KeyboardShortcut> showDraggerUI { get; set; }

	private static ConfigEntry<KeyboardShortcut> showAnimationUI { get; set; }

	private static ConfigEntry<bool> applySavedOffsets { get; set; }

	public static ConfigEntry<bool> useOneOffsetForAllMotions { get; private set; }

	public static ConfigEntry<bool> useUniqueOffsetForWeak { get; private set; }

	public static ConfigEntry<string> offsetFile { get; private set; }

	public static ConfigEntry<string> offsetFileV2 { get; private set; }

	public static ConfigEntry<float> sliderMaxBodyPosition { get; private set; }

	public static ConfigEntry<float> sliderMaxBodyRotation { get; private set; }

	public static ConfigEntry<float> sliderMaxLimbPosition { get; private set; }

	public static ConfigEntry<float> sliderMaxLimbRotation { get; private set; }

	public static ConfigEntry<float> sliderMaxHintPosition { get; private set; }

	public static ConfigEntry<bool> enableAnimationFixer { get; private set; }

	public static ConfigEntry<bool> solveDependenciesFirst { get; private set; }

	public static ConfigEntry<bool> useLastSolutionForMales { get; private set; }

	public static ConfigEntry<bool> useLastSolutionForFemales { get; private set; }

	public static ConfigEntry<bool> fixAttachmentPoints { get; private set; }

	public static ConfigEntry<bool> fixEffectors { get; private set; }

	public static ConfigEntry<bool> kissCorrection { get; private set; }

	public static ConfigEntry<Vector3> kissOffset { get; private set; }

	public static ConfigEntry<bool> defaultJointCorrection { get; private set; }

	private static ConfigEntry<bool> preventDefaultAnimationChangeStrip { get; set; }

	private static ConfigEntry<Tools.OffHStartAnimChange> stripMaleClothes { get; set; }

	private static ConfigEntry<Tools.ClothesStrip> stripMaleTop { get; set; }

	private static ConfigEntry<Tools.ClothesStrip> stripMaleBottom { get; set; }

	private static ConfigEntry<Tools.ClothesStrip> stripMaleBra { get; set; }

	private static ConfigEntry<Tools.ClothesStrip> stripMalePanties { get; set; }

	private static ConfigEntry<Tools.ClothesStrip> stripMaleGloves { get; set; }

	private static ConfigEntry<Tools.ClothesStrip> stripMalePantyhose { get; set; }

	private static ConfigEntry<Tools.ClothesStrip> stripMaleSocks { get; set; }

	private static ConfigEntry<Tools.ClothesStrip> stripMaleShoes { get; set; }

	private static ConfigEntry<Tools.OffHStartAnimChange> stripFemaleClothes { get; set; }

	private static ConfigEntry<Tools.ClothesStrip> stripFemaleTop { get; set; }

	private static ConfigEntry<Tools.ClothesStrip> stripFemaleBottom { get; set; }

	private static ConfigEntry<Tools.ClothesStrip> stripFemaleBra { get; set; }

	private static ConfigEntry<Tools.ClothesStrip> stripFemalePanties { get; set; }

	private static ConfigEntry<Tools.ClothesStrip> stripFemaleGloves { get; set; }

	private static ConfigEntry<Tools.ClothesStrip> stripFemalePantyhose { get; set; }

	private static ConfigEntry<Tools.ClothesStrip> stripFemaleSocks { get; set; }

	private static ConfigEntry<Tools.ClothesStrip> stripFemaleShoes { get; set; }

	private static ConfigEntry<int> countToWeakness { get; set; }

	private static ConfigEntry<Tools.OffWeaknessAlways> forceTears { get; set; }

	private static ConfigEntry<Tools.OffWeaknessAlways> forceCloseEyes { get; set; }

	private static ConfigEntry<Tools.OffWeaknessAlways> forceStopBlinking { get; set; }

	private static ConfigEntry<Tools.AutoFinish> autoFinish { get; set; }

	private static ConfigEntry<Tools.AutoServicePrefer> autoServicePrefer { get; set; }

	private static ConfigEntry<Tools.AutoInsertPrefer> autoInsertPrefer { get; set; }

	public static ConfigEntry<Tools.CleanCum> cleanCumAfterH { get; private set; }

	private static ConfigEntry<bool> increaseBathDesire { get; set; }

	private static ConfigEntry<Tools.OffWeaknessAlways> alwaysGaugesHeart { get; set; }

	public static ConfigEntry<bool> keepButtonsInteractive { get; private set; }

	private static ConfigEntry<int> hPointSearchRange { get; set; }

	private static ConfigEntry<bool> unlockCamera { get; set; }

	private static ConfigEntry<bool> disableMap { get; set; }

	private static ConfigEntry<bool> disableSunShadows { get; set; }

	private static ConfigEntry<bool> pauseTimeDuringH { get; set; }

	private static ConfigEntry<bool> optimizeCollisionHelpers { get; set; }

	private void Awake()
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Expected O, but got Unknown
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Expected O, but got Unknown
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Expected O, but got Unknown
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Expected O, but got Unknown
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Expected O, but got Unknown
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Expected O, but got Unknown
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a9: Expected O, but got Unknown
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d8: Expected O, but got Unknown
		//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Expected O, but got Unknown
		//IL_022c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0236: Expected O, but got Unknown
		//IL_025b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0265: Expected O, but got Unknown
		//IL_0286: Unknown result type (might be due to invalid IL or missing references)
		//IL_0290: Expected O, but got Unknown
		//IL_02d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e0: Expected O, but got Unknown
		//IL_0301: Unknown result type (might be due to invalid IL or missing references)
		//IL_030b: Expected O, but got Unknown
		//IL_032c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0336: Expected O, but got Unknown
		//IL_0357: Unknown result type (might be due to invalid IL or missing references)
		//IL_0361: Expected O, but got Unknown
		//IL_03a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b1: Expected O, but got Unknown
		//IL_03f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0401: Expected O, but got Unknown
		//IL_044a: Unknown result type (might be due to invalid IL or missing references)
		//IL_045a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0464: Expected O, but got Unknown
		//IL_0485: Unknown result type (might be due to invalid IL or missing references)
		//IL_048f: Expected O, but got Unknown
		//IL_04b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ba: Expected O, but got Unknown
		//IL_04db: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e5: Expected O, but got Unknown
		//IL_0506: Unknown result type (might be due to invalid IL or missing references)
		//IL_0510: Expected O, but got Unknown
		//IL_0531: Unknown result type (might be due to invalid IL or missing references)
		//IL_053b: Expected O, but got Unknown
		//IL_055c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0566: Expected O, but got Unknown
		//IL_0587: Unknown result type (might be due to invalid IL or missing references)
		//IL_0591: Expected O, but got Unknown
		//IL_05b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_05bc: Expected O, but got Unknown
		//IL_05dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_05e7: Expected O, but got Unknown
		//IL_0608: Unknown result type (might be due to invalid IL or missing references)
		//IL_0612: Expected O, but got Unknown
		//IL_0633: Unknown result type (might be due to invalid IL or missing references)
		//IL_063d: Expected O, but got Unknown
		//IL_065e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0668: Expected O, but got Unknown
		//IL_0689: Unknown result type (might be due to invalid IL or missing references)
		//IL_0693: Expected O, but got Unknown
		//IL_06b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_06be: Expected O, but got Unknown
		//IL_06df: Unknown result type (might be due to invalid IL or missing references)
		//IL_06e9: Expected O, but got Unknown
		//IL_070a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0714: Expected O, but got Unknown
		//IL_0735: Unknown result type (might be due to invalid IL or missing references)
		//IL_073f: Expected O, but got Unknown
		//IL_0760: Unknown result type (might be due to invalid IL or missing references)
		//IL_076a: Expected O, but got Unknown
		//IL_078b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0795: Expected O, but got Unknown
		//IL_07b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_07c0: Expected O, but got Unknown
		//IL_07eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_07f5: Expected O, but got Unknown
		//IL_083b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0845: Expected O, but got Unknown
		//IL_0866: Unknown result type (might be due to invalid IL or missing references)
		//IL_0870: Expected O, but got Unknown
		//IL_0891: Unknown result type (might be due to invalid IL or missing references)
		//IL_089b: Expected O, but got Unknown
		//IL_08bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_08c6: Expected O, but got Unknown
		//IL_08e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_08f1: Expected O, but got Unknown
		//IL_0912: Unknown result type (might be due to invalid IL or missing references)
		//IL_091c: Expected O, but got Unknown
		//IL_093d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0947: Expected O, but got Unknown
		//IL_0968: Unknown result type (might be due to invalid IL or missing references)
		//IL_0972: Expected O, but got Unknown
		//IL_0993: Unknown result type (might be due to invalid IL or missing references)
		//IL_099d: Expected O, but got Unknown
		//IL_09be: Unknown result type (might be due to invalid IL or missing references)
		//IL_09c8: Expected O, but got Unknown
		//IL_09f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a01: Expected O, but got Unknown
		//IL_0a47: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a51: Expected O, but got Unknown
		//IL_0a97: Unknown result type (might be due to invalid IL or missing references)
		//IL_0aa1: Expected O, but got Unknown
		//IL_0ae7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0af1: Expected O, but got Unknown
		//IL_0b37: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b41: Expected O, but got Unknown
		//IL_0b87: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b91: Expected O, but got Unknown
		//IL_0bcb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bd5: Expected O, but got Unknown
		Logger = ((BaseUnityPlugin)this).Logger;
		shouldCleanUp = new List<ChaControl>();
		showDraggerUI = ((BaseUnityPlugin)this).Config.Bind<KeyboardShortcut>("Animations > Draggers", "Show draggers UI", new KeyboardShortcut((KeyCode)110, Array.Empty<KeyCode>()), (ConfigDescription)null);
		showAnimationUI = ((BaseUnityPlugin)this).Config.Bind<KeyboardShortcut>("Animations > Draggers", "Show animation UI", new KeyboardShortcut((KeyCode)110, (KeyCode[])(object)new KeyCode[1] { (KeyCode)306 }), new ConfigDescription("Displays a UI that can be used to change the current animation motion.  Intended to be used with draggers UI to make adjustments.  May break the flow of an HScene so use with caution.", (AcceptableValueBase)null, Array.Empty<object>()));
		(applySavedOffsets = ((BaseUnityPlugin)this).Config.Bind<bool>("Animations > Draggers", "Apply saved offsets", true, new ConfigDescription("Apply previously saved character offsets for character pair / position during H", (AcceptableValueBase)null, Array.Empty<object>()))).SettingChanged += delegate
		{
			if (applySavedOffsets.Value)
			{
				shouldApplyOffsets = true;
			}
		};
		useOneOffsetForAllMotions = ((BaseUnityPlugin)this).Config.Bind<bool>("Animations > Draggers", "Use one offset for all motions", true, new ConfigDescription("If disabled, the Save button in the UI will only save the offsets for the current motion of the position.  A Default button will be added to save it for all motions of that position that don't already have an offset.", (AcceptableValueBase)null, Array.Empty<object>()));
		useUniqueOffsetForWeak = ((BaseUnityPlugin)this).Config.Bind<bool>("Animations > Draggers", "Use unique default offset for weak motions", true, new ConfigDescription("If enabled, saving a default motion will save unique offsets for the current state the girl is in, either normal or weakness. This lets you save two separate default offsets, one for normal state and one for weakness.", (AcceptableValueBase)null, Array.Empty<object>()));
		offsetFile = ((BaseUnityPlugin)this).Config.Bind<string>("Animations > Draggers", "Legacy Offset File Path", "UserData/BetterHScenesOffsets.xml", new ConfigDescription("Path of the legacy offset file card on disk, will be converted to new offset file on startup.", (AcceptableValueBase)null, Array.Empty<object>()));
		offsetFileV2 = ((BaseUnityPlugin)this).Config.Bind<string>("Animations > Draggers", "Offset File Path V2", "UserData/BetterHScenesOffsetsV2.xml", new ConfigDescription("Path of the offset file card on disk.", (AcceptableValueBase)null, Array.Empty<object>()));
		sliderMaxBodyPosition = ((BaseUnityPlugin)this).Config.Bind<float>("Animations > Draggers", "Body Slider min/max position", 2.5f, new ConfigDescription("Maximum limits of the body position slider bars.", (AcceptableValueBase)null, Array.Empty<object>()));
		sliderMaxBodyRotation = ((BaseUnityPlugin)this).Config.Bind<float>("Animations > Draggers", "Body Slider min/max rotation", 45f, new ConfigDescription("Maximum limits of the body rotation slider bars.", (AcceptableValueBase)null, Array.Empty<object>()));
		sliderMaxLimbPosition = ((BaseUnityPlugin)this).Config.Bind<float>("Animations > Draggers", "Limb Slider min/max position", 5f, new ConfigDescription("Maximum limits of the limb position slider bars.", (AcceptableValueBase)null, Array.Empty<object>()));
		sliderMaxLimbRotation = ((BaseUnityPlugin)this).Config.Bind<float>("Animations > Draggers", "Limb Slider min/max rotation", 90f, new ConfigDescription("Maximum limits of the limb rotation slider bars.", (AcceptableValueBase)null, Array.Empty<object>()));
		sliderMaxHintPosition = ((BaseUnityPlugin)this).Config.Bind<float>("Animations > Draggers", "Hint Slider min/max position", 15f, new ConfigDescription("Maximum limits of the hint position slider bars.", (AcceptableValueBase)null, Array.Empty<object>()));
		(solveDependenciesFirst = ((BaseUnityPlugin)this).Config.Bind<bool>("Animations > Solver", "Solve Independent Animations First", true, new ConfigDescription("Re-orders animation solving.  If the male animation is dependent on the female animation, the female animation will be solved first.  Some animations have both male and female dependencies.  These ones will run females first, so female dependencies will be broken.  This can be fixed by using last frame (see below)", (AcceptableValueBase)null, Array.Empty<object>()))).SettingChanged += delegate
		{
			if ((Object)(object)hScene != (Object)null)
			{
				SliderUI.UpdateDependentStatus();
			}
		};
		useLastSolutionForFemales = ((BaseUnityPlugin)this).Config.Bind<bool>("Animations > Solver", "Use Last Frame Solutions for Females", true, new ConfigDescription("Use Last Frame's result as input to next frame.  This can fix problems when the female animations are solved before the male animations but are dependent on the male animations.  It will add a framerate dependent amount of jitter to the animations, which can be a good thing if your fps is high, or a bad thing if your fps is low.", (AcceptableValueBase)null, Array.Empty<object>()));
		useLastSolutionForMales = ((BaseUnityPlugin)this).Config.Bind<bool>("Animations > Solver", "Use Last Frame Solutions for Males", false, new ConfigDescription("Use Last Frame's result as input to next frame.  This can fix problems when the male animations are solved before the female animatiosn but are dependent on the male animations.  It will add a framerate dependent amount of jitter to the animations, which can be a good thing if your fps is high, or a bad thing if your fps is low.", (AcceptableValueBase)null, Array.Empty<object>()));
		enableAnimationFixer = ((BaseUnityPlugin)this).Config.Bind<bool>("Animations > Solver", "Enable Animation Fixer", true, new ConfigDescription("Corrects most animations by using the other characters solutions if available. No other solver options will work without this enabled.", (AcceptableValueBase)null, Array.Empty<object>()));
		(fixAttachmentPoints = ((BaseUnityPlugin)this).Config.Bind<bool>("Animations > Solver", "Fix broken Animation Tables", true, new ConfigDescription("Corrects certain animations by attaching certain IK points to the correct location instead of leaving them dangling in air.", (AcceptableValueBase)null, Array.Empty<object>()))).SettingChanged += delegate
		{
			if ((Object)(object)hScene != (Object)null)
			{
				FixMotionList(hScene.ctrlFlag.nowAnimationInfo.fileFemale);
			}
		};
		(fixEffectors = ((BaseUnityPlugin)this).Config.Bind<bool>("Animations > Solver", "Fix broken Effectors", false, new ConfigDescription("Allows limb movement on certain positions by fixing their effector weights.", (AcceptableValueBase)null, Array.Empty<object>()))).SettingChanged += delegate
		{
			if ((Object)(object)hScene != (Object)null)
			{
				FixEffectors();
			}
		};
		(kissCorrection = ((BaseUnityPlugin)this).Config.Bind<bool>("Animations > Solver", "Fix Kiss Animations", true, new ConfigDescription("Apply an offset to the male character to align kiss animations", (AcceptableValueBase)null, Array.Empty<object>()))).SettingChanged += delegate
		{
			if ((Object)(object)hScene != (Object)null)
			{
				FixMotionList(hScene.ctrlFlag.nowAnimationInfo.fileFemale);
			}
		};
		kissOffset = ((BaseUnityPlugin)this).Config.Bind<Vector3>("Animations > Solver", "Kiss Offset", new Vector3(0f, -0.08f, 0.19f), new ConfigDescription("Offset applied to the target location for kiss alignment", (AcceptableValueBase)null, Array.Empty<object>()));
		defaultJointCorrection = ((BaseUnityPlugin)this).Config.Bind<bool>("Animations > Solver", "Joint Correction Default", false, new ConfigDescription("Default enable/disable state of joint corrections in Slider UI", (AcceptableValueBase)null, Array.Empty<object>()));
		preventDefaultAnimationChangeStrip = ((BaseUnityPlugin)this).Config.Bind<bool>("QoL > Clothes", "Prevent default animationchange strip", true, new ConfigDescription("Prevent default animation change clothes strip (pants, panties, top half state)", (AcceptableValueBase)null, Array.Empty<object>()));
		stripMaleClothes = ((BaseUnityPlugin)this).Config.Bind<Tools.OffHStartAnimChange>("QoL > Clothes", "Should strip male clothes", Tools.OffHStartAnimChange.OnHStart, new ConfigDescription("Should strip male clothes during H", (AcceptableValueBase)null, Array.Empty<object>()));
		stripMaleTop = ((BaseUnityPlugin)this).Config.Bind<Tools.ClothesStrip>("QoL > Clothes", "Strip male top", Tools.ClothesStrip.All, new ConfigDescription("Strip male top during H", (AcceptableValueBase)null, Array.Empty<object>()));
		stripMaleBottom = ((BaseUnityPlugin)this).Config.Bind<Tools.ClothesStrip>("QoL > Clothes", "Strip male bottom", Tools.ClothesStrip.All, new ConfigDescription("Strip male bottom during H", (AcceptableValueBase)null, Array.Empty<object>()));
		stripMaleBra = ((BaseUnityPlugin)this).Config.Bind<Tools.ClothesStrip>("QoL > Clothes", "Strip male bra", Tools.ClothesStrip.Half, new ConfigDescription("Strip male (futa) bra during H", (AcceptableValueBase)null, Array.Empty<object>()));
		stripMalePanties = ((BaseUnityPlugin)this).Config.Bind<Tools.ClothesStrip>("QoL > Clothes", "Strip male panties", Tools.ClothesStrip.Half, new ConfigDescription("Strip male (futa) panties during H", (AcceptableValueBase)null, Array.Empty<object>()));
		stripMaleGloves = ((BaseUnityPlugin)this).Config.Bind<Tools.ClothesStrip>("QoL > Clothes", "Strip male gloves", Tools.ClothesStrip.Off, new ConfigDescription("Strip male gloves during H", (AcceptableValueBase)null, Array.Empty<object>()));
		stripMalePantyhose = ((BaseUnityPlugin)this).Config.Bind<Tools.ClothesStrip>("QoL > Clothes", "Strip male pantyhose", Tools.ClothesStrip.Half, new ConfigDescription("Strip male (futa) pantyhose during H", (AcceptableValueBase)null, Array.Empty<object>()));
		stripMaleSocks = ((BaseUnityPlugin)this).Config.Bind<Tools.ClothesStrip>("QoL > Clothes", "Strip male socks", Tools.ClothesStrip.Off, new ConfigDescription("Strip male (futa) socks during H", (AcceptableValueBase)null, Array.Empty<object>()));
		stripMaleShoes = ((BaseUnityPlugin)this).Config.Bind<Tools.ClothesStrip>("QoL > Clothes", "Strip male shoes", Tools.ClothesStrip.Off, new ConfigDescription("Strip male shoes during H", (AcceptableValueBase)null, Array.Empty<object>()));
		stripFemaleClothes = ((BaseUnityPlugin)this).Config.Bind<Tools.OffHStartAnimChange>("QoL > Clothes", "Should strip female clothes", Tools.OffHStartAnimChange.Both, new ConfigDescription("Should strip female clothes during H", (AcceptableValueBase)null, Array.Empty<object>()));
		stripFemaleTop = ((BaseUnityPlugin)this).Config.Bind<Tools.ClothesStrip>("QoL > Clothes", "Strip female top", Tools.ClothesStrip.Half, new ConfigDescription("Strip female top during H", (AcceptableValueBase)null, Array.Empty<object>()));
		stripFemaleBottom = ((BaseUnityPlugin)this).Config.Bind<Tools.ClothesStrip>("QoL > Clothes", "Strip female bottom", Tools.ClothesStrip.Half, new ConfigDescription("Strip female bottom during H", (AcceptableValueBase)null, Array.Empty<object>()));
		stripFemaleBra = ((BaseUnityPlugin)this).Config.Bind<Tools.ClothesStrip>("QoL > Clothes", "Strip female bra", Tools.ClothesStrip.Half, new ConfigDescription("Strip female bra during H", (AcceptableValueBase)null, Array.Empty<object>()));
		stripFemalePanties = ((BaseUnityPlugin)this).Config.Bind<Tools.ClothesStrip>("QoL > Clothes", "Strip female panties", Tools.ClothesStrip.Half, new ConfigDescription("Strip female panties during H", (AcceptableValueBase)null, Array.Empty<object>()));
		stripFemaleGloves = ((BaseUnityPlugin)this).Config.Bind<Tools.ClothesStrip>("QoL > Clothes", "Strip female gloves", Tools.ClothesStrip.Off, new ConfigDescription("Strip female gloves during H", (AcceptableValueBase)null, Array.Empty<object>()));
		stripFemalePantyhose = ((BaseUnityPlugin)this).Config.Bind<Tools.ClothesStrip>("QoL > Clothes", "Strip female pantyhose", Tools.ClothesStrip.Half, new ConfigDescription("Strip female pantyhose during H", (AcceptableValueBase)null, Array.Empty<object>()));
		stripFemaleSocks = ((BaseUnityPlugin)this).Config.Bind<Tools.ClothesStrip>("QoL > Clothes", "Strip female socks", Tools.ClothesStrip.Off, new ConfigDescription("Strip female socks during H", (AcceptableValueBase)null, Array.Empty<object>()));
		stripFemaleShoes = ((BaseUnityPlugin)this).Config.Bind<Tools.ClothesStrip>("QoL > Clothes", "Strip female shoes", Tools.ClothesStrip.Off, new ConfigDescription("Strip female shoes during H", (AcceptableValueBase)null, Array.Empty<object>()));
		(countToWeakness = ((BaseUnityPlugin)this).Config.Bind<int>("QoL > Weakness", "Orgasm count until weakness", 3, new ConfigDescription("How many times does the girl have to orgasm to reach weakness", (AcceptableValueBase)(object)new AcceptableValueRange<int>(1, 999), Array.Empty<object>()))).SettingChanged += delegate
		{
			Tools.SetGotoWeaknessCount(countToWeakness.Value);
		};
		forceTears = ((BaseUnityPlugin)this).Config.Bind<Tools.OffWeaknessAlways>("QoL > Weakness", "Force show tears", Tools.OffWeaknessAlways.WeaknessOnly, new ConfigDescription("Make girl cry", (AcceptableValueBase)null, Array.Empty<object>()));
		forceCloseEyes = ((BaseUnityPlugin)this).Config.Bind<Tools.OffWeaknessAlways>("QoL > Weakness", "Force close eyes", Tools.OffWeaknessAlways.Off, new ConfigDescription("Close girl eyes", (AcceptableValueBase)null, Array.Empty<object>()));
		forceStopBlinking = ((BaseUnityPlugin)this).Config.Bind<Tools.OffWeaknessAlways>("QoL > Weakness", "Force stop blinking", Tools.OffWeaknessAlways.Off, new ConfigDescription("Stop blinking", (AcceptableValueBase)null, Array.Empty<object>()));
		alwaysGaugesHeart = ((BaseUnityPlugin)this).Config.Bind<Tools.OffWeaknessAlways>("QoL > Weakness", "Always hit gauge heart", Tools.OffWeaknessAlways.WeaknessOnly, new ConfigDescription("Always hit gauge heart. Will cause progress to increase without having to scroll specific amount", (AcceptableValueBase)null, Array.Empty<object>()));
		autoFinish = ((BaseUnityPlugin)this).Config.Bind<Tools.AutoFinish>("QoL > Cum", "Auto finish", Tools.AutoFinish.Both, new ConfigDescription("Automatically finish inside when both gauges reach max", (AcceptableValueBase)null, Array.Empty<object>()));
		autoServicePrefer = ((BaseUnityPlugin)this).Config.Bind<Tools.AutoServicePrefer>("QoL > Cum", "Preferred auto service finish", Tools.AutoServicePrefer.Drink, new ConfigDescription("Preferred auto finish type. Will fall back to any available option if selected is not available", (AcceptableValueBase)null, Array.Empty<object>()));
		autoInsertPrefer = ((BaseUnityPlugin)this).Config.Bind<Tools.AutoInsertPrefer>("QoL > Cum", "Preferred auto insert finish", Tools.AutoInsertPrefer.Same, new ConfigDescription("Preferred auto finish type. Will fall back to any available option if selected is not available", (AcceptableValueBase)null, Array.Empty<object>()));
		cleanCumAfterH = ((BaseUnityPlugin)this).Config.Bind<Tools.CleanCum>("QoL > Cum", "Clean cum on body after H", Tools.CleanCum.All, new ConfigDescription("Clean cum on body after H", (AcceptableValueBase)null, Array.Empty<object>()));
		increaseBathDesire = ((BaseUnityPlugin)this).Config.Bind<bool>("QoL > Cum", "Increase bath desire after H", false, new ConfigDescription("Increase bath desire after H (agents only)", (AcceptableValueBase)null, Array.Empty<object>()));
		keepButtonsInteractive = ((BaseUnityPlugin)this).Config.Bind<bool>("QoL > General", "Keep UI buttons interactive*", false, new ConfigDescription("Keep buttons interactive during certain events like orgasm (WARNING: May cause bugs)", (AcceptableValueBase)null, Array.Empty<object>()));
		(hPointSearchRange = ((BaseUnityPlugin)this).Config.Bind<int>("QoL > General", "H point search range", 300, new ConfigDescription("Range in which H points are shown when changing location (default 60)", (AcceptableValueBase)(object)new AcceptableValueRange<int>(1, 999), Array.Empty<object>()))).SettingChanged += delegate
		{
			if (!((Object)(object)hSprite == (Object)null))
			{
				hSprite.HpointSearchRange = hPointSearchRange.Value;
			}
		};
		(unlockCamera = ((BaseUnityPlugin)this).Config.Bind<bool>("QoL > General", "Unlock camera movement", true, new ConfigDescription("Unlock camera zoom out / distance limit during H", (AcceptableValueBase)null, Array.Empty<object>()))).SettingChanged += delegate
		{
			if (!((Object)(object)hCamera == (Object)null))
			{
				hCamera.isLimitDir = !unlockCamera.Value;
				hCamera.isLimitPos = !unlockCamera.Value;
			}
		};
		(disableMap = ((BaseUnityPlugin)this).Config.Bind<bool>("Performance Improvements", "Disable map", false, new ConfigDescription("Disable map during H scene", (AcceptableValueBase)null, Array.Empty<object>()))).SettingChanged += delegate
		{
			if (!((Object)(object)map == (Object)null))
			{
				map.SetActive(!disableMap.Value);
			}
		};
		(disableSunShadows = ((BaseUnityPlugin)this).Config.Bind<bool>("Performance Improvements", "Disable sun shadows", false, new ConfigDescription("Disable sun shadows during H scene", (AcceptableValueBase)null, Array.Empty<object>()))).SettingChanged += delegate
		{
			if (!((Object)(object)sun == (Object)null))
			{
				sun.shadows = (LightShadows)((!disableSunShadows.Value) ? 2 : 0);
			}
		};
		(pauseTimeDuringH = ((BaseUnityPlugin)this).Config.Bind<bool>("Performance Improvements", "Disable world simulation", false, new ConfigDescription("Disable world simulation (time) during H scene", (AcceptableValueBase)null, Array.Empty<object>()))).SettingChanged += delegate
		{
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			if (!((Object)(object)enviroSky == (Object)null) && !((Object)(object)hScene == (Object)null))
			{
				if (pauseTimeDuringH.Value)
				{
					enviroSky.GameTime.ProgressTime = (TimeProgressMode)0;
				}
				else
				{
					enviroSky.GameTime.ProgressTime = (TimeProgressMode)1;
				}
			}
		};
		(optimizeCollisionHelpers = ((BaseUnityPlugin)this).Config.Bind<bool>("Performance Improvements", "Optimize collisionhelpers", true, new ConfigDescription("Optimize collisionhelpers by letting them update once per frame", (AcceptableValueBase)null, Array.Empty<object>()))).SettingChanged += delegate
		{
			if (collisionHelpers == null)
			{
				return;
			}
			foreach (SkinnedCollisionHelper item in collisionHelpers.Where((SkinnedCollisionHelper helper) => (Object)(object)helper != (Object)null))
			{
				if (!optimizeCollisionHelpers.Value)
				{
					item.forceUpdate = true;
				}
				item.updateOncePerFrame = optimizeCollisionHelpers.Value;
			}
		};
		shouldApplyOffsets = false;
		HSceneOffset.LoadOffsetsFromFile();
		harmony = new Harmony("AI_BetterHScenes");
		harmony.PatchAll(typeof(Transpilers));
	}

	private void OnGUI()
	{
		if (activeDraggerUI && (Object)(object)hScene != (Object)null)
		{
			SliderUI.DrawDraggersUI();
		}
		if (activeAnimationUI && (Object)(object)hScene != (Object)null)
		{
			AnimationUI.DrawAnimationUI();
		}
		if (activeConfirmDeleteUI && (Object)(object)hScene != (Object)null)
		{
			SliderUI.DrawConfirmDeleteUI();
		}
	}

	private void Update()
	{
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_016e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0267: Unknown result type (might be due to invalid IL or missing references)
		//IL_0277: Unknown result type (might be due to invalid IL or missing references)
		//IL_0287: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02da: Unknown result type (might be due to invalid IL or missing references)
		//IL_02df: Unknown result type (might be due to invalid IL or missing references)
		bool isHScene = HSceneManager.isHScene;
		if (isHScene && !patched)
		{
			HScene_sceneLoaded(loaded: true);
		}
		else if (!isHScene && patched)
		{
			HScene_sceneLoaded(loaded: false);
		}
		if ((Object)(object)hScene == (Object)null)
		{
			return;
		}
		KeyboardShortcut value = showDraggerUI.Value;
		if (((KeyboardShortcut)(ref value)).IsDown())
		{
			activeDraggerUI = !activeDraggerUI;
		}
		value = showAnimationUI.Value;
		if (((KeyboardShortcut)(ref value)).IsDown())
		{
			activeAnimationUI = !activeAnimationUI;
		}
		if (shouldApplyOffsets && !hScene.NowChangeAnim)
		{
			HSceneOffset.ApplyCharacterOffsets();
			SliderUI.UpdateDependentStatus();
			FixMotionList(hScene.ctrlFlag.nowAnimationInfo.fileFemale);
			FixEffectors();
			shouldApplyOffsets = false;
		}
		if (autoFinish.Value == Tools.AutoFinish.Off)
		{
			return;
		}
		if (hFlagCtrl.feel_m >= 0.98f && Tools.IsService() && autoFinish.Value != Tools.AutoFinish.InsertOnly)
		{
			bool flag = hSprite.IsFinishVisible(4);
			bool flag2 = hSprite.IsFinishVisible(5);
			bool flag3 = hSprite.IsFinishVisible(0);
			switch (autoServicePrefer.Value)
			{
			case Tools.AutoServicePrefer.Drink:
				if (flag)
				{
					hFlagCtrl.click = (ClickKind)4;
				}
				break;
			case Tools.AutoServicePrefer.Spit:
				if (flag2)
				{
					hFlagCtrl.click = (ClickKind)5;
				}
				break;
			case Tools.AutoServicePrefer.Outside:
				if (flag3)
				{
					hFlagCtrl.click = (ClickKind)2;
				}
				break;
			case Tools.AutoServicePrefer.Random:
			{
				List<ClickKind> list = new List<ClickKind>();
				if (flag)
				{
					list.Add((ClickKind)4);
				}
				if (flag2)
				{
					list.Add((ClickKind)5);
				}
				if (flag3)
				{
					list.Add((ClickKind)2);
				}
				if (list.Count >= 1)
				{
					hFlagCtrl.click = list[rand.Next(list.Count)];
				}
				break;
			}
			}
		}
		else
		{
			if (!(hFlagCtrl.feel_f >= 0.98f) || !(hFlagCtrl.feel_m >= 0.98f) || !Tools.IsInsert() || autoFinish.Value == Tools.AutoFinish.ServiceOnly)
			{
				return;
			}
			bool flag4 = hSprite.IsFinishVisible(2);
			bool flag5 = hSprite.IsFinishVisible(0);
			bool flag6 = hSprite.IsFinishVisible(1);
			switch (autoInsertPrefer.Value)
			{
			case Tools.AutoInsertPrefer.Inside:
				if (flag4)
				{
					hFlagCtrl.click = (ClickKind)1;
				}
				break;
			case Tools.AutoInsertPrefer.Outside:
				if (flag5)
				{
					hFlagCtrl.click = (ClickKind)2;
				}
				break;
			case Tools.AutoInsertPrefer.Same:
				if (flag6)
				{
					hFlagCtrl.click = (ClickKind)3;
				}
				break;
			case Tools.AutoInsertPrefer.Random:
			{
				List<ClickKind> list2 = new List<ClickKind>();
				if (flag4)
				{
					list2.Add((ClickKind)1);
				}
				if (flag5)
				{
					list2.Add((ClickKind)2);
				}
				if (flag6)
				{
					list2.Add((ClickKind)3);
				}
				if (list2.Count >= 1)
				{
					hFlagCtrl.click = list2[rand.Next(list2.Count)];
				}
				break;
			}
			}
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(FaceBlendShape), "OnLateUpdate")]
	public static void FaceBlendShape_OnLateUpdate(FaceBlendShape __instance)
	{
		if ((Object)(object)hScene == (Object)null || shouldApplyOffsets)
		{
			return;
		}
		ChaControl componentInParent = ((Component)__instance).GetComponentInParent<ChaControl>();
		if ((Object)(object)componentInParent == (Object)null)
		{
			return;
		}
		int charIndex = 0;
		if (!((ChaInfo)componentInParent).isPlayer)
		{
			charIndex = 1;
			if (femaleCharacters.Count > 1 && (Object)(object)femaleCharacters[1] != (Object)null && ((ChaInfo)componentInParent).loadNo == ((ChaInfo)femaleCharacters[1]).loadNo)
			{
				charIndex = 2;
			}
		}
		SliderUI.ApplyMouthOffset(charIndex, __instance.MouthCtrl);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SolverManager), "LateUpdate")]
	public static bool SolverManager_PreLateUpdate(SolverManager __instance)
	{
		if ((Object)(object)hScene == (Object)null || shouldApplyOffsets)
		{
			return true;
		}
		ChaControl componentInParent = ((Component)__instance).GetComponentInParent<ChaControl>();
		if ((Object)(object)componentInParent == (Object)null)
		{
			return true;
		}
		int num = 0;
		if (!((ChaInfo)componentInParent).isPlayer)
		{
			num = 1;
			if (femaleCharacters.Count > 1 && (Object)(object)femaleCharacters[1] != (Object)null && ((ChaInfo)componentInParent).loadNo == ((ChaInfo)femaleCharacters[1]).loadNo)
			{
				num = 2;
			}
		}
		if (enableAnimationFixer.Value && solveDependenciesFirst.Value && ((ChaInfo)componentInParent).isPlayer && SliderUI.characterOffsets[num].dependentAnimation)
		{
			return false;
		}
		if (!((ChaInfo)componentInParent).isPlayer)
		{
			bool leftFootJob = bFootJobException && (!bTwoFootException || currentMotion.Contains("Idle") || currentMotion.Contains("WLoop"));
			bool rightFootJob = bFootJobException && bTwoFootException && currentMotion.Contains("O");
			SliderUI.ApplyLimbOffsets(num, useLastSolutionForFemales.Value, useReplacements, leftFootJob, rightFootJob, !componentInParent.IsBareFoot, kissOffset: false);
		}
		else
		{
			SliderUI.ApplyLimbOffsets(num, useLastSolutionForMales.Value, useReplacements, leftFootJob: false, rightFootJob: false, !componentInParent.IsBareFoot, applyKissOffset);
		}
		return true;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SolverManager), "LateUpdate")]
	public static void SolverManager_PostLateUpdate(SolverManager __instance)
	{
		if ((Object)(object)hScene == (Object)null || !enableAnimationFixer.Value || !solveDependenciesFirst.Value || shouldApplyOffsets)
		{
			return;
		}
		ChaControl componentInParent = ((Component)__instance).GetComponentInParent<ChaControl>();
		if ((Object)(object)componentInParent == (Object)null || ((ChaInfo)componentInParent).isPlayer || (femaleCharacters.Count > 1 && (Object)(object)femaleCharacters[1] != (Object)null && ((ChaInfo)femaleCharacters[1]).loadNo == ((ChaInfo)componentInParent).loadNo))
		{
			return;
		}
		for (int i = 0; i < maleCharacters.Count; i++)
		{
			if (SliderUI.characterOffsets[i].dependentAnimation)
			{
				SliderUI.ApplyLimbOffsets(i, useLastSolutionForMales.Value, useReplacements, leftFootJob: false, rightFootJob: false, !maleCharacters[i].IsBareFoot, applyKissOffset);
				((SolverManager)((ChaInfo)maleCharacters[i]).fullBodyIK).UpdateSolverExternal();
			}
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(H_Lookat_dan), "LateUpdate")]
	public static void H_Lookat_dan_PostLateUpdate()
	{
		if ((Object)(object)hScene != (Object)null)
		{
			SliderUI.SaveBasePoints(useReplacements);
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(HScene), "SetStartVoice")]
	public static void HScene_SetStartVoice_Patch(HScene __instance, HSceneSprite ___sprite, HSceneManager ___hSceneManager)
	{
		//IL_02d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_032e: Unknown result type (might be due to invalid IL or missing references)
		Console.WriteLine("BetterHScenes: HScene Start");
		hScene = __instance;
		hSprite = ___sprite;
		manager = ___hSceneManager;
		if ((Object)(object)hScene == (Object)null || (Object)(object)hSprite == (Object)null || (Object)(object)manager == (Object)null)
		{
			return;
		}
		hFlagCtrl = hScene.ctrlFlag;
		if ((Object)(object)hFlagCtrl == (Object)null)
		{
			return;
		}
		hCamera = hFlagCtrl.cameraCtrl;
		if ((Object)(object)hCamera == (Object)null)
		{
			return;
		}
		hSceneTrav = Traverse.Create((object)hScene);
		listTrav = Traverse.Create((object)hScene.ctrlEyeNeckMale[0]);
		Traverse obj = listTrav;
		maleMotionList = ((obj != null) ? obj.Field("lstEyeNeck").GetValue<List<EyeNeck>>() : null);
		map = GameObject.Find("map00_Beach");
		if ((Object)(object)map == (Object)null)
		{
			map = GameObject.Find("map_01_data");
		}
		if ((Object)(object)map == (Object)null)
		{
			return;
		}
		GameObject val = GameObject.Find("CommonSpace/MapRoot/MapSimulation(Clone)/EnviroSkyGroup(Clone)/Enviro Directional Light");
		if ((Object)(object)val == (Object)null)
		{
			return;
		}
		GameObject obj2 = GameObject.Find("CommonSpace/MapRoot/MapSimulation(Clone)/EnviroSkyGroup(Clone)/EnviroSky");
		enviroSky = ((obj2 != null) ? obj2.GetComponent<EnviroSky>() : null);
		cameraShouldLock = true;
		sun = val.GetComponent<Light>();
		collisionHelpers = new List<SkinnedCollisionHelper>();
		characters = new List<ChaControl>();
		maleCharacters = new List<ChaControl>();
		foreach (ChaControl item in from male in __instance.GetMales()
			where (Object)(object)male != (Object)null
			select male)
		{
			maleCharacters.Add(item);
			characters.Add(item);
		}
		femaleCharacters = new List<ChaControl>();
		foreach (ChaControl item2 in from female in __instance.GetFemales()
			where (Object)(object)female != (Object)null
			select female)
		{
			femaleCharacters.Add(item2);
			characters.Add(item2);
		}
		if (characters == null)
		{
			return;
		}
		foreach (ChaControl item3 in characters.Where((ChaControl character) => (Object)(object)character != (Object)null))
		{
			if (!((Object)(object)((Component)item3).GetComponent<Expression>() != (Object)null))
			{
				Expression obj3 = ((Component)item3).gameObject.AddComponent<Expression>();
				obj3.SetCharaTransform(((Component)item3).transform);
				obj3.LoadSetting("list/expression.unity3d", "cf_expression");
				obj3.Initialize();
			}
		}
		oldMapState = map.activeSelf;
		oldSunShadowsState = sun.shadows;
		if (disableMap.Value)
		{
			map.SetActive(false);
		}
		if (disableSunShadows.Value)
		{
			sun.shadows = (LightShadows)0;
		}
		if ((Object)(object)enviroSky != (Object)null && pauseTimeDuringH.Value)
		{
			enviroSky.GameTime.ProgressTime = (TimeProgressMode)0;
		}
		if (unlockCamera.Value)
		{
			hCamera.isLimitDir = false;
			hCamera.isLimitPos = false;
		}
		if (hPointSearchRange.Value != 60 && (Object)(object)hSprite != (Object)null)
		{
			hSprite.HpointSearchRange = hPointSearchRange.Value;
		}
		Tools.hFlagCtrlTrav = Traverse.Create((object)hFlagCtrl);
		Tools.SetGotoWeaknessCount(countToWeakness.Value);
		SliderUI.InitDraggersUI();
		EnableJointCorrection(defaultJointCorrection.Value);
		Console.WriteLine("BetterHScenes: HScene Started Successfully");
	}

	private static void SetAfterHBathDesire()
	{
		if ((Object)(object)hScene == (Object)null || (Object)(object)manager == (Object)null || manager.bMerchant || !increaseBathDesire.Value)
		{
			return;
		}
		try
		{
			ReadOnlyDictionary<int, AgentActor> agentTable = Singleton<Map>.Instance.AgentTable;
			if (agentTable == null)
			{
				return;
			}
			foreach (ChaControl female2 in from female in hScene.GetFemales()
				where (Object)(object)female != (Object)null
				select female)
			{
				AgentActor value = agentTable.FirstOrDefault((KeyValuePair<int, AgentActor> pair) => (Object)(object)pair.Value != (Object)null && (Object)(object)((Actor)pair.Value).ChaControl != (Object)null && (Object)(object)((Actor)pair.Value).ChaControl == (Object)(object)female2).Value;
				if (!((Object)(object)value == (Object)null))
				{
					int desireKey = Desire.GetDesireKey((Type)2);
					int desireKey2 = Desire.GetDesireKey((Type)256);
					float num = Tools.Remap(value.GetFlavorSkill((Type)2), 0f, 99999f, 0f, 100f);
					float num2 = Tools.Remap(value.GetFlavorSkill((Type)4), 0f, 99999f, 0f, 100f);
					float valueOrDefault = value.GetDesire(desireKey2).GetValueOrDefault();
					float num3 = 100f + num * 1.25f - num2 - valueOrDefault * 1.5f;
					value.SetDesire(desireKey, Mathf.Clamp(num3, 0f, 100f));
				}
			}
		}
		catch (Exception value2)
		{
			Logger.LogMessage((object)"HScene_SetAfterHBathDesire error!");
			Logger.LogWarning((object)"HScene_SetAfterHBathDesire error!");
			Console.WriteLine(value2);
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(HScene), "EndProc")]
	public static void HScene_EndProc_Patch()
	{
		EndHScene();
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(HScene), "EndProcADV")]
	public static void HScene_EndProcADV_Patch()
	{
		EndHScene();
	}

	private static void EndHScene()
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)hScene == (Object)null)
		{
			return;
		}
		Console.WriteLine("BetterHScenes: HScene End");
		if ((Object)(object)map != (Object)null)
		{
			map.SetActive(oldMapState);
		}
		if ((Object)(object)sun != (Object)null)
		{
			sun.shadows = oldSunShadowsState;
		}
		if ((Object)(object)enviroSky != (Object)null)
		{
			enviroSky.GameTime.ProgressTime = (TimeProgressMode)1;
		}
		activeDraggerUI = false;
		activeAnimationUI = false;
		OnHStart = false;
		SetAfterHBathDesire();
		Console.WriteLine("SetAfterHBathDesire");
		if (characters != null)
		{
			foreach (ChaControl item in characters.Where((ChaControl character) => (Object)(object)character != (Object)null))
			{
				Expression component = ((Component)item).GetComponent<Expression>();
				if ((Object)(object)component != (Object)null)
				{
					Object.Destroy((Object)(object)component);
				}
			}
		}
		Console.WriteLine("DestroyExpressions");
		hScene = null;
		hFlagCtrl = null;
		hSprite = null;
		manager = null;
		hCamera = null;
		hSceneTrav = null;
		listTrav = null;
		characters = new List<ChaControl>();
		maleCharacters = new List<ChaControl>();
		femaleCharacters = new List<ChaControl>();
		maleMotionList = new List<EyeNeck>();
		map = null;
		sun = null;
		collisionHelpers = new List<SkinnedCollisionHelper>();
		cameraShouldLock = false;
		oldMapState = false;
		shouldApplyOffsets = false;
		currentMotion = null;
		hProcMode = 0;
		bBaseReplacement = false;
		bIdleAfterException = false;
		bFootJobException = false;
		bTwoFootException = false;
		useReplacements = false;
		Console.WriteLine("BetterHScenes: HScene Ended Successfully");
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(HScene), "SyncAnimation")]
	public static void HScene_SyncAnimation_StripClothes()
	{
		if (!OnHStart)
		{
			HScene_StripClothes(stripMaleClothes.Value == Tools.OffHStartAnimChange.OnHStart || stripMaleClothes.Value == Tools.OffHStartAnimChange.Both, stripFemaleClothes.Value == Tools.OffHStartAnimChange.OnHStart || stripFemaleClothes.Value == Tools.OffHStartAnimChange.Both);
			OnHStart = true;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(FeelHit), "isHit")]
	public static void FeelHit_isHit_AlwaysGaugesHeart(ref bool __result)
	{
		if (alwaysGaugesHeart.Value == Tools.OffWeaknessAlways.Always || (alwaysGaugesHeart.Value == Tools.OffWeaknessAlways.WeaknessOnly && hFlagCtrl.isFaintness))
		{
			__result = true;
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(VirtualCameraController), "LateUpdate")]
	public static bool VirtualCameraController_LateUpdate_DisableCameraControl(VirtualCameraController __instance)
	{
		if (!cameraShouldLock || !activeDraggerUI)
		{
			return true;
		}
		Traverse.Create((object)__instance).Property("isControlNow", (object[])null).SetValue((object)false);
		return false;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(HVoiceCtrl), "SetFace")]
	public static void HVoiceCtrl_SetFace_ForceTearsOnWeakness(ref FaceInfo _face)
	{
		if (_face != null && !((Object)(object)hFlagCtrl == (Object)null))
		{
			if (forceTears.Value == Tools.OffWeaknessAlways.Always || (forceTears.Value == Tools.OffWeaknessAlways.WeaknessOnly && hFlagCtrl.isFaintness))
			{
				_face.tear = 1f;
			}
			if (forceCloseEyes.Value == Tools.OffWeaknessAlways.Always || (forceCloseEyes.Value == Tools.OffWeaknessAlways.WeaknessOnly && hFlagCtrl.isFaintness))
			{
				_face.openEye = 0.05f;
			}
			if (forceStopBlinking.Value == Tools.OffWeaknessAlways.Always || (forceStopBlinking.Value == Tools.OffWeaknessAlways.WeaknessOnly && hFlagCtrl.isFaintness))
			{
				_face.blink = false;
			}
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SkinnedCollisionHelper), "Init")]
	public static void SkinnedCollisionHelper_Init_UpdateOncePerFrame(SkinnedCollisionHelper __instance)
	{
		if (collisionHelpers != null)
		{
			collisionHelpers.Add(__instance);
			if (optimizeCollisionHelpers.Value)
			{
				__instance.updateOncePerFrame = true;
			}
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SiruPasteCtrl), "Proc")]
	public static void SiruPasteCtrl_Proc_PopulateList(ChaControl ___chaFemale)
	{
		if (cleanCumAfterH.Value < Tools.CleanCum.AgentsOnly)
		{
			return;
		}
		ChaControl chara = ___chaFemale;
		if ((Object)(object)chara == (Object)null || ((ChaInfo)chara).isPlayer || shouldCleanUp.Contains(chara) || manager.bMerchant || (Object)(object)Singleton<Map>.Instance.AgentTable.Values.FirstOrDefault((AgentActor actor) => (Object)(object)actor != (Object)null && (Object)(object)((Actor)actor).ChaControl != (Object)null && (Object)(object)((Actor)actor).ChaControl == (Object)(object)chara) == (Object)null)
		{
			return;
		}
		for (int i = 0; i < 5; i++)
		{
			if (chara.GetSiruFlag((SiruParts)i) != 0)
			{
				shouldCleanUp.Add(chara);
				break;
			}
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(HScene), "SetClothStateStartMotion")]
	public static bool HScene_SetClothStateStartMotion_PreventDefaultClothesStrip(int ___mode, int ___modeCtrl)
	{
		Tools.mode = ___mode;
		Tools.modeCtrl = ___modeCtrl;
		HScene_StripClothes(stripMaleClothes.Value > Tools.OffHStartAnimChange.OnHStart, stripFemaleClothes.Value > Tools.OffHStartAnimChange.OnHStart);
		return !preventDefaultAnimationChangeStrip.Value;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Bath), "OnCompletedStateTask")]
	public static void Bath_OnCompletedStateTask_CleanUpCum(Bath __instance)
	{
		Tools.CleanUpSiru((AgentStateAction)(object)__instance);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(ClothChange), "OnCompletedStateTask")]
	public static void ClothChange_OnCompletedStateTask_CleanUpCum(ClothChange __instance)
	{
		Tools.CleanUpSiru((AgentStateAction)(object)__instance);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(HScene), "ChangeAnimation")]
	private static void HScene_PreChangeAnimation()
	{
		if (applySavedOffsets.Value)
		{
			shouldApplyOffsets = true;
		}
		if (!((Object)(object)hScene == (Object)null))
		{
			SliderUI.ClearBaseReplacements();
			bBaseReplacement = false;
			bIdleAfterException = false;
			bFootJobException = false;
			bTwoFootException = false;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(HScene), "ChangeAnimation")]
	private static void HScene_PostChangeAnimation()
	{
		if (!((Object)(object)hScene == (Object)null))
		{
			Traverse obj = listTrav;
			maleMotionList = ((obj != null) ? obj.Field("lstEyeNeck").GetValue<List<EyeNeck>>() : null);
			hProcMode = hSceneTrav.Field("mode").GetValue<int>();
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(HScene), "SetMovePositionPoint")]
	private static void HScene_SetMovePositionPoint()
	{
		if (applySavedOffsets.Value)
		{
			shouldApplyOffsets = true;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(ChaControl), "setPlay")]
	private static void ChaControl_PostSetPlay(string _strAnmName)
	{
		if (!((Object)(object)hScene == (Object)null) && !NullCheck.IsNullOrEmpty(_strAnmName))
		{
			currentMotion = _strAnmName;
			bool flag = currentMotion.Contains("Idle") || currentMotion.Contains("_A");
			string text = hScene?.ctrlFlag?.nowAnimationInfo?.fileFemale;
			useReplacements = bBaseReplacement && !bFootJobException && (!bIdleAfterException || !flag);
			applyKissOffset = kissCorrection.Value && !flag && !NullCheck.IsNullOrEmpty(text) && kissCorrectionList.Contains(text);
			if (applySavedOffsets.Value && !useOneOffsetForAllMotions.Value)
			{
				shouldApplyOffsets = true;
			}
		}
	}

	private static void HScene_StripClothes(bool stripMales, bool stripFemales)
	{
		if (stripMales)
		{
			List<Tools.ClothesStrip> source = new List<Tools.ClothesStrip> { stripMaleTop.Value, stripMaleBottom.Value, stripMaleBra.Value, stripMalePanties.Value, stripMaleGloves.Value, stripMalePantyhose.Value, stripMaleSocks.Value, stripMaleShoes.Value };
			foreach (ChaControl item in from male in hScene.GetMales()
				where (Object)(object)male != (Object)null
				select male)
			{
				foreach (var item2 in source.Select((Tools.ClothesStrip x, int i) => new { x, i }))
				{
					if (item2.x > Tools.ClothesStrip.Off && item.IsClothesStateKind(item2.i) && ((ChaInfo)item).fileStatus.clothesState[item2.i] != 2)
					{
						item.SetClothesState(item2.i, (byte)item2.x, true);
					}
				}
			}
		}
		if (!stripFemales)
		{
			return;
		}
		List<Tools.ClothesStrip> source2 = new List<Tools.ClothesStrip> { stripFemaleTop.Value, stripFemaleBottom.Value, stripFemaleBra.Value, stripFemalePanties.Value, stripFemaleGloves.Value, stripFemalePantyhose.Value, stripFemaleSocks.Value, stripFemaleShoes.Value };
		foreach (ChaControl item3 in from female in hScene.GetFemales()
			where (Object)(object)female != (Object)null
			select female)
		{
			foreach (var item4 in source2.Select((Tools.ClothesStrip x, int i) => new { x, i }))
			{
				if (item4.x > Tools.ClothesStrip.Off && item3.IsClothesStateKind(item4.i) && ((ChaInfo)item3).fileStatus.clothesState[item4.i] != 2)
				{
					item3.SetClothesState(item4.i, (byte)item4.x, true);
				}
			}
		}
	}

	public static void SwitchAnimations(string playAnimation)
	{
		Traverse obj = hSceneTrav;
		HItemCtrl val = ((obj != null) ? obj.Field("ctrlItem").GetValue<HItemCtrl>() : null);
		if (femaleCharacters == null || (Object)(object)femaleCharacters[0] == (Object)null)
		{
			return;
		}
		femaleCharacters[0].setPlay(playAnimation, 0);
		MotionIK component = ((Component)femaleCharacters[0]).GetComponent<MotionIK>();
		if (component != null)
		{
			component.Calc(playAnimation);
		}
		if ((hProcMode == 7 || hProcMode == 6) && ((ChaInfo)femaleCharacters[1]).visibleAll && (Object)(object)((ChaInfo)femaleCharacters[1]).objTop != (Object)null)
		{
			femaleCharacters[1].setPlay(playAnimation, 0);
			component = ((Component)femaleCharacters[1]).GetComponent<MotionIK>();
			if (component != null)
			{
				component.Calc(playAnimation);
			}
		}
		if (hProcMode == 4 || hProcMode == 6)
		{
			return;
		}
		if ((Object)(object)((ChaInfo)maleCharacters[0]).objTop != (Object)null)
		{
			maleCharacters[0].setPlay(playAnimation, 0);
			component = ((Component)maleCharacters[0]).GetComponent<MotionIK>();
			if (component != null)
			{
				component.Calc(playAnimation);
			}
		}
		if (val != null)
		{
			val.setPlay(playAnimation);
		}
	}

	public static void PlayAnimations(int play)
	{
		foreach (ChaControl item in characters.Where((ChaControl character) => (Object)(object)character != (Object)null))
		{
			if (((ChaInfo)item).visibleAll)
			{
				Animator animBody = ((ChaInfo)item).animBody;
				if (!((Object)(object)animBody == (Object)null))
				{
					animBody.speed = play;
				}
			}
		}
	}

	public static void FixMotionList(string fileFemale)
	{
		SliderUI.ClearBaseReplacements();
		bBaseReplacement = false;
		bIdleAfterException = false;
		bFootJobException = false;
		bTwoFootException = false;
		if (!fixAttachmentPoints.Value || maleCharacters == null || (Object)(object)maleCharacters[0] == (Object)null || femaleCharacters == null || (Object)(object)femaleCharacters[0] == (Object)null)
		{
			return;
		}
		if (siriReplaceList.Contains(fileFemale))
		{
			Transform val = (from x in ((Component)femaleCharacters[0]).GetComponentsInChildren<Transform>()
				where ((Object)x).name.Contains("k_f_siriL_00")
				select x).FirstOrDefault();
			Transform val2 = (from x in ((Component)femaleCharacters[0]).GetComponentsInChildren<Transform>()
				where ((Object)x).name.Contains("k_f_siriR_00")
				select x).FirstOrDefault();
			if ((Object)(object)val != (Object)null)
			{
				SliderUI.SetBaseReplacement(0, 1, val);
			}
			if ((Object)(object)val2 != (Object)null)
			{
				SliderUI.SetBaseReplacement(0, 2, val2);
			}
			bBaseReplacement = true;
		}
		else if (kosiReplaceList.Contains(fileFemale))
		{
			Transform val3 = (from x in ((Component)femaleCharacters[0]).GetComponentsInChildren<Transform>()
				where ((Object)x).name.Contains("k_f_kosi02_00")
				select x).FirstOrDefault();
			Transform val4 = (from x in ((Component)femaleCharacters[0]).GetComponentsInChildren<Transform>()
				where ((Object)x).name.Contains("k_f_kosi02_01")
				select x).FirstOrDefault();
			if ((Object)(object)val3 != (Object)null)
			{
				SliderUI.SetBaseReplacement(0, 1, val3);
			}
			if ((Object)(object)val4 != (Object)null)
			{
				SliderUI.SetBaseReplacement(0, 2, val4);
			}
			bBaseReplacement = true;
		}
		else if (huggingReplaceList.Contains(fileFemale))
		{
			Transform val5 = (from x in ((Component)maleCharacters[0]).GetComponentsInChildren<Transform>()
				where ((Object)x).name.Contains("k_f_spine03_00")
				select x).FirstOrDefault();
			Transform val6 = (from x in ((Component)femaleCharacters[0]).GetComponentsInChildren<Transform>()
				where ((Object)x).name.Contains("k_f_armlowL_00")
				select x).FirstOrDefault();
			if ((Object)(object)val5 != (Object)null)
			{
				SliderUI.SetBaseReplacement(maleCharacters.Count, 1, val5);
			}
			if ((Object)(object)val6 != (Object)null)
			{
				SliderUI.SetBaseReplacement(maleCharacters.Count, 2, val6);
			}
			bBaseReplacement = true;
		}
		else if (footReplaceList.Contains(fileFemale))
		{
			Transform val7 = (from x in ((Component)femaleCharacters[0]).GetComponentsInChildren<Transform>()
				where ((Object)x).name.Contains("f_k_foot_L")
				select x).FirstOrDefault();
			Transform val8 = (from x in ((Component)maleCharacters[0]).GetComponentsInChildren<Transform>()
				where ((Object)x).name.Contains("k_m_dansao00_00")
				select x).FirstOrDefault();
			if ((Object)(object)val7 != (Object)null)
			{
				SliderUI.SetBaseReplacement(maleCharacters.Count, 3, val7);
			}
			if ((Object)(object)val8 != (Object)null)
			{
				SliderUI.SetBaseReplacement(maleCharacters.Count, 1, val8);
			}
			bFootJobException = true;
			if (fileFemale != footReplaceList[0])
			{
				Transform val9 = (from x in ((Component)femaleCharacters[0]).GetComponentsInChildren<Transform>()
					where ((Object)x).name.Contains("f_k_foot_R")
					select x).FirstOrDefault();
				Transform val10 = (from x in ((Component)maleCharacters[0]).GetComponentsInChildren<Transform>()
					where ((Object)x).name.Contains("k_m_dansao00_01")
					select x).FirstOrDefault();
				if ((Object)(object)val9 != (Object)null)
				{
					SliderUI.SetBaseReplacement(maleCharacters.Count, 4, val9);
				}
				if ((Object)(object)val10 != (Object)null)
				{
					SliderUI.SetBaseReplacement(maleCharacters.Count, 2, val10);
				}
				bTwoFootException = true;
			}
		}
		else if (rightKokanReplaceList.Contains(fileFemale))
		{
			Transform val11 = (from x in ((Component)femaleCharacters[0]).GetComponentsInChildren<Transform>()
				where ((Object)x).name.Contains("k_f_kokan_00")
				select x).FirstOrDefault();
			if ((Object)(object)val11 != (Object)null)
			{
				SliderUI.SetBaseReplacement(0, 2, val11);
			}
			bBaseReplacement = true;
			bIdleAfterException = true;
		}
		else if (leftKokanReplaceList.Contains(fileFemale))
		{
			Transform val12 = (from x in ((Component)femaleCharacters[0]).GetComponentsInChildren<Transform>()
				where ((Object)x).name.Contains("k_f_kokan_00")
				select x).FirstOrDefault();
			if ((Object)(object)val12 != (Object)null)
			{
				SliderUI.SetBaseReplacement(0, 1, val12);
			}
			bBaseReplacement = true;
			bIdleAfterException = true;
		}
		else if (rightKosiReplaceList.Contains(fileFemale))
		{
			Transform val13 = (from x in ((Component)femaleCharacters[0]).GetComponentsInChildren<Transform>()
				where ((Object)x).name.Contains("k_f_kosi02_00")
				select x).FirstOrDefault();
			if ((Object)(object)val13 != (Object)null)
			{
				SliderUI.SetBaseReplacement(0, 2, val13);
			}
			bBaseReplacement = true;
			bIdleAfterException = true;
		}
		else if (leftKosiReplaceList.Contains(fileFemale))
		{
			Transform val14 = (from x in ((Component)femaleCharacters[0]).GetComponentsInChildren<Transform>()
				where ((Object)x).name.Contains("k_f_kosi02_00")
				select x).FirstOrDefault();
			if ((Object)(object)val14 != (Object)null)
			{
				SliderUI.SetBaseReplacement(0, 1, val14);
			}
			bBaseReplacement = true;
			bIdleAfterException = true;
		}
		bool flag = currentMotion.Contains("Idle") || currentMotion.Contains("_A");
		useReplacements = bBaseReplacement && !bFootJobException && (!bIdleAfterException || !flag);
		applyKissOffset = kissCorrection.Value && kissCorrectionList.Contains(fileFemale) && !flag;
	}

	public static void FixEffectors()
	{
		if (!fixEffectors.Value)
		{
			return;
		}
		foreach (ChaControl character in characters)
		{
			IKEffector[] effectors = ((IKSolverFullBody)((ChaInfo)character).fullBodyIK.solver).effectors;
			if (effectors != null)
			{
				for (Effector effector = Effector.LeftHand; effector <= Effector.RightFoot && (int)effector < effectors.Length; effector++)
				{
					effectors[(int)effector].positionWeight = 1f;
					effectors[(int)effector].rotationWeight = 1f;
				}
			}
		}
	}

	private static void EnableJointCorrection(bool enable)
	{
		foreach (ChaControl item in characters.Where((ChaControl character) => (Object)(object)character != (Object)null))
		{
			Expression component = ((Component)item).GetComponent<Expression>();
			if ((Object)(object)component != (Object)null)
			{
				component.enable = true;
			}
			ScriptInfo[] info = component.info;
			for (int i = 0; i < info.Length; i++)
			{
				info[i].enable = enable;
			}
		}
	}

	private static void HScene_sceneLoaded(bool loaded)
	{
		patched = loaded;
		if (loaded)
		{
			harmony.PatchAll(typeof(AI_BetterHScenes));
			return;
		}
		harmony.UnpatchAll("AI_BetterHScenes");
		hScene = null;
		hFlagCtrl = null;
		hSprite = null;
		manager = null;
		hCamera = null;
		hSceneTrav = null;
		listTrav = null;
		characters = null;
		maleCharacters = null;
		femaleCharacters = null;
		maleMotionList = null;
		map = null;
		sun = null;
		collisionHelpers = null;
		cameraShouldLock = false;
		oldMapState = false;
		shouldApplyOffsets = false;
		currentMotion = null;
		hProcMode = 0;
		bBaseReplacement = false;
		bIdleAfterException = false;
		bFootJobException = false;
		bTwoFootException = false;
		useReplacements = false;
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.2.0.10050' (yours is '8.2.0.7535-95108c96')
