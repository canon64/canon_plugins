using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ADV;
using ActionGame;
using BepInEx.Bootstrap;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using Illusion.Component;
using KKAPI.Studio;
using KKAPI.Utilities;
using Manager;
using SaveData;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KKAPI.MainGame;

/// <summary>
/// Provides API for interfacing with the main game. It is useful mostly in the actual game, but some
/// functions will work outside of it (for example in FreeH).
/// </summary>
public static class GameAPI
{
	public class DayChangeEventArgs : EventArgs
	{
		public Week NewDay { get; }

		public DayChangeEventArgs(Week newDay)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			NewDay = newDay;
		}
	}

	public class PeriodChangeEventArgs : EventArgs
	{
		public Type NewPeriod { get; }

		public PeriodChangeEventArgs(Type period)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			NewPeriod = period;
		}
	}

	private class Hooks
	{
		public static void SetupHooks(Harmony hi)
		{
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Expected O, but got Unknown
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Expected O, but got Unknown
			//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bf: Expected O, but got Unknown
			hi.PatchAll(typeof(Hooks));
			Type type = Type.GetType("VRHScene, Assembly-CSharp");
			if (type != null)
			{
				hi.Patch((MethodBase)AccessTools.Method(type, "Start", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(AccessTools.Method(typeof(Hooks), "StartProcPostVR", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
				hi.Patch((MethodBase)AccessTools.Method(type, "EndProc", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(AccessTools.Method(typeof(Hooks), "EndProcPost", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
				hi.Patch((MethodBase)AccessTools.Method(type, "OnBack", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(AccessTools.Method(typeof(Hooks), "EndProcPost", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(Game), "Load", new Type[] { typeof(string) })]
		public static void LoadHook(string fileName)
		{
			OnGameBeingLoaded(WorldData.Path + "/", fileName);
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(WorldData), "Save", new Type[]
		{
			typeof(string),
			typeof(string)
		})]
		public static void SaveHook(string path, string fileName)
		{
			GameBeingSaved = true;
			OnGameBeingSaved(path, fileName);
		}

		[HarmonyFinalizer]
		[HarmonyPatch(typeof(WorldData), "Save", new Type[]
		{
			typeof(string),
			typeof(string)
		})]
		public static void SaveHookPost()
		{
			GameBeingSaved = false;
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(HSceneProc), "Start")]
		public static void StartProcPost(MonoBehaviour __instance, ref IEnumerator __result)
		{
			IEnumerator enumerator = __result;
			__result = new IEnumerator[2]
			{
				enumerator,
				OnHStart(__instance)
			}.GetEnumerator();
		}

		public static void StartProcPostVR(MonoBehaviour __instance, ref UniTask __result)
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			UniTask val = __result;
			__result = UniTask<Coroutine>.op_Implicit(UniTaskExtensions.ContinueWith<Coroutine>(val, (Func<Coroutine>)(() => __instance.StartCoroutine(OnHStart(__instance)))));
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(HSceneProc), "NewHeroineEndProc")]
		[HarmonyPatch(typeof(HSceneProc), "EndProc")]
		public static void EndProcPost(MonoBehaviour __instance)
		{
			OnHEnd(__instance);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(Cycle), "Change", new Type[] { typeof(Type) })]
		public static void CycleChangeTypeHook(Type type)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			OnPeriodChange(type);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(Cycle), "Change", new Type[] { typeof(Week) })]
		public static void CycleChangeWeekHook(Week week)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			OnDayChange(week);
		}
	}

	private static readonly Dictionary<GameCustomFunctionController, string> _registeredHandlers = new Dictionary<GameCustomFunctionController, string>();

	private static GameObject _functionControllerContainer;

	private static readonly IList<string> _newGameDetectionScenes = new List<string>(new string[4] { "Title", "EntryPlayer", "ClassRoomSelect", "Action" });

	private static int _newGameDetectionIndex = -1;

	/// <summary>
	/// True if any sort of H scene is currently loaded.
	/// </summary>
	public static bool InsideHScene { get; private set; }

	/// <summary>
	/// True if the game is in process of being saved.
	/// </summary>
	public static bool GameBeingSaved { get; private set; }

	/// <summary>
	/// Fired after an H scene is loaded. Can be both in the main game and in free h.
	/// Runs immediately after all <see cref="T:KKAPI.MainGame.GameCustomFunctionController" /> objects trigger their events.
	/// </summary>
	public static event EventHandler StartH;

	/// <summary>
	/// Fired after an H scene is ended, but before it is unloaded. Can be both in the main game and in free h.
	/// Runs immediately after all <see cref="T:KKAPI.MainGame.GameCustomFunctionController" /> objects trigger their events.
	/// </summary>
	public static event EventHandler EndH;

	/// <summary>
	/// Fired right after a game save is succesfully loaded.
	/// Runs immediately after all <see cref="T:KKAPI.MainGame.GameCustomFunctionController" /> objects trigger their events.
	/// </summary>
	public static event EventHandler<GameSaveLoadEventArgs> GameLoad;

	/// <summary>
	/// Fired right before the game state is saved to file.
	/// Runs immediately after all <see cref="T:KKAPI.MainGame.GameCustomFunctionController" /> objects trigger their events.
	/// </summary>
	public static event EventHandler<GameSaveLoadEventArgs> GameSave;

	/// <summary>
	/// Triggered when the current day changes in story mode.
	/// Runs immediately after all <see cref="T:KKAPI.MainGame.GameCustomFunctionController" /> objects trigger their events.
	/// </summary>
	public static event EventHandler<DayChangeEventArgs> DayChange;

	/// <summary>
	/// Triggered when the current time of the day changes in story mode.
	/// Runs immediately after all <see cref="T:KKAPI.MainGame.GameCustomFunctionController" /> objects trigger their events.
	/// </summary>
	public static event EventHandler<PeriodChangeEventArgs> PeriodChange;

	/// <summary>
	/// Triggered when a new game is started in story mode.
	/// Runs immediately after all <see cref="T:KKAPI.MainGame.GameCustomFunctionController" /> objects trigger their events.
	/// </summary>
	public static event EventHandler NewGame;

	/// <summary>
	/// Get all registered behaviours for the game.
	/// </summary>
	public static IEnumerable<GameCustomFunctionController> GetBehaviours()
	{
		return _registeredHandlers.Keys.AsEnumerable();
	}

	/// <summary>
	/// Get the first controller that was registered with the specified extendedDataId.
	/// </summary>
	public static GameCustomFunctionController GetRegisteredBehaviour(string extendedDataId)
	{
		return (from registration in _registeredHandlers
			where string.Equals(registration.Value, extendedDataId, StringComparison.Ordinal)
			select registration.Key).FirstOrDefault();
	}

	/// <summary>
	/// Get the first controller of the specified type that was registered. The type has to be an exact match.
	/// </summary>
	public static GameCustomFunctionController GetRegisteredBehaviour(Type controllerType)
	{
		return (from registration in _registeredHandlers
			where ((object)registration.Key).GetType() == controllerType
			select registration.Key).FirstOrDefault();
	}

	/// <summary>
	/// Get the first controller of the specified type that was registered with the specified extendedDataId. The type has to be an exact match.
	/// </summary>
	public static GameCustomFunctionController GetRegisteredBehaviour(Type controllerType, string extendedDataId)
	{
		return (from registration in _registeredHandlers
			where string.Equals(registration.Value, extendedDataId, StringComparison.Ordinal) && ((object)registration.Key).GetType() == controllerType
			select registration.Key).FirstOrDefault();
	}

	/// <summary>
	/// Register new functionality that will be added to main game. Offers easy API for custom main game logic.
	/// All you have to do is create a type that inherits from <see cref="T:KKAPI.MainGame.GameCustomFunctionController" />&gt;
	/// (don't make instances, the API will make them for you). Warning: The custom controller is immediately
	/// created when it's registered, but its OnGameLoad method is not called until a game actually loads.
	/// This might mean that if the registration happens too late you will potentially miss some load events.
	/// </summary>
	/// <typeparam name="T">Type with your custom logic to add to a character</typeparam>
	/// <param name="extendedDataId">Extended data ID used by this behaviour. Set to null if not used.</param>
	public static void RegisterExtraBehaviour<T>(string extendedDataId) where T : GameCustomFunctionController, new()
	{
		if (!StudioAPI.InsideStudio)
		{
			T val = _functionControllerContainer.AddComponent<T>();
			val.ExtendedDataId = extendedDataId;
			_registeredHandlers.Add(val, extendedDataId);
		}
	}

	/// <summary>
	/// Register a new action icon / action point in roaming mode (like the icons for training/studying, club report screen, peeping).
	/// Dispose the return value to remove the icon.
	/// Icon templates can be found here https://github.com/IllusionMods/IllusionModdingAPI/tree/master/src/KKSAPI/MainGame/ActionIcons
	/// </summary>
	/// <param name="mapNo">Identification number of the map the icon should be spawned on</param>
	/// <param name="position">Position of the icon. All default icons are spawned at y=0, but different heights work fine to a degree.
	/// You can figure out the position by walking to it and getting the player position with RUE.</param>
	/// <param name="icon">Icon shown at the top of the action point. It should be pure white, use the <paramref name="color" /> parameter to change the color instead.</param>
	/// <param name="color">Color of the action point.</param>
	/// <param name="popupText">Text shown at the bottom of the screen when player is close to the action point.</param>
	/// <param name="onOpen">Action triggered when player clicks the icon (If you want to open your own menu, use <see cref="M:KKAPI.MainGame.GameExtensions.SetIsCursorLock(ActionScene,System.Boolean)" />
	/// to enable mouse cursor and hide the action icon to prevent it from being clicked again.).</param>
	/// <param name="onCreated">Optional action to run after the icon is created.
	/// Use to attach extra code to the icon, e.g. by using <see cref="M:UniRx.Triggers.ObservableTriggerExtensions.UpdateAsObservable(UnityEngine.Component)" /> and similar methods.</param>
	/// <param name="delayed">Should the icon be spawned every time the map is entered</param>
	/// <param name="immediate">Should the icon be spawned immediately if the player is on the correct map</param>
	public static IDisposable AddActionIcon(int mapNo, Vector3 position, Texture icon, Color color, string popupText, Action onOpen, Action<TriggerEnterExitEvent> onCreated = null, bool delayed = true, bool immediate = false)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		if (StudioAPI.InsideStudio)
		{
			return Disposable.Empty;
		}
		return CustomActionIcon.AddActionIcon(mapNo, position, icon, color, popupText, onOpen, onCreated, delayed, immediate);
	}

	/// <summary>
	/// Register a new touch icon in talk scenes in roaming mode (like the touch and look buttons on top right when talking to a character).
	/// Dispose the return value to remove the icon.
	/// Icon templates can be found here https://github.com/IllusionMods/IllusionModdingAPI/tree/master/src/KKAPI/MainGame/TouchIcons
	/// By default this functions as a simple button. If you want to turn this into a toggle you have to manually switch button.image.sprite as needed.
	/// </summary>
	/// <param name="icon">Icon shown by default</param>
	/// <param name="onCreated">Action to run after the icon is created.
	/// Use to subscribe to the onClick event and/or attach extra code to the button, e.g. by using <see cref="M:UniRx.Triggers.ObservableTriggerExtensions.UpdateAsObservable(UnityEngine.Component)" /> and similar methods.</param>
	/// <param name="row">Row of the button, counted from top at 0. Buttons are added from right to left. Row has to be between 0 and 5, but 0 to 2 are recommended.</param>
	/// <param name="order">Order of the buttons in a row. Lower value is placed more to the right. By default order of adding the icons is used.</param>
	public static IDisposable AddTouchIcon(Sprite icon, Action<Button> onCreated, int row = 1, int order = 0)
	{
		if (StudioAPI.InsideStudio)
		{
			return Disposable.Empty;
		}
		return CustomTalkSceneTouchIcon.AddTouchIcon(icon, onCreated, row, order);
	}

	/// <summary>
	/// Register a new conversation button in talk scenes in roaming mode.
	/// (the white buttons you get when you click on the buttons on the right when talking to someone, for example "Confess", "Excercise together")
	/// Dispose the return value to remove the button (It will not be created anymore and have to be re-added. If inside a talk scene the button will be removed immediately).
	/// </summary>
	/// <param name="text">Text of the new button.</param>
	/// <param name="onCreated">Action to run after the icon is created.
	/// Use to subscribe to the onClick event and/or attach extra code to the button, e.g. by using <see cref="M:UniRx.Triggers.ObservableTriggerExtensions.UpdateAsObservable(UnityEngine.Component)" /> and similar methods.</param>
	/// <param name="targetMenu">Which submenu to put your button under (from the 3 buttons on right, only top and bottom buttons can be used here).</param>
	[Obsolete("Not implemented yet")]
	public static IDisposable AddTalkButton(string text, Action<Button> onCreated, TalkSceneActionKind targetMenu)
	{
		throw new NotImplementedException("Not implemented yet");
	}

	internal static void Init(bool insideStudio)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected O, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected O, but got Unknown
		if (insideStudio)
		{
			return;
		}
		Harmony val = new Harmony(typeof(GameAPI).FullName);
		Hooks.SetupHooks(val);
		val.PatchAll(typeof(CustomActionIcon));
		val.PatchAll(typeof(CustomTalkSceneTouchIcon));
		_functionControllerContainer = new GameObject("GameCustomFunctionController Zoo");
		_functionControllerContainer.transform.SetParent(Chainloader.ManagerObject.transform, false);
		SceneManager.sceneLoaded += delegate(Scene arg0, LoadSceneMode mode)
		{
			if (((Scene)(ref arg0)).name == "HotelMyroom" && Scene.LoadSceneName != "H")
			{
				foreach (KeyValuePair<GameCustomFunctionController, string> registeredHandler in _registeredHandlers)
				{
					try
					{
						registeredHandler.Key.OnEnterHotelMyroomMenu();
					}
					catch (Exception ex)
					{
						KoikatuAPI.Logger.LogError((object)ex);
					}
				}
			}
		};
		SceneManager.activeSceneChanged += delegate(Scene scene1, Scene scene2)
		{
			int num = _newGameDetectionScenes.IndexOf(((Scene)(ref scene2)).name);
			if (num != -1 && NullCheck.IsNullOrWhiteSpace(((Scene)(ref scene1)).name) && (num == _newGameDetectionIndex + 1 || num == _newGameDetectionIndex - 1))
			{
				_newGameDetectionIndex = num;
				if (_newGameDetectionIndex + 1 == _newGameDetectionScenes.Count)
				{
					_newGameDetectionIndex = -1;
					OnNewGame();
				}
			}
			else
			{
				_newGameDetectionIndex = -1;
			}
		};
		if (KoikatuAPI.EnableDebugLogging)
		{
			RegisterExtraBehaviour<TestGameFunctionController>(null);
		}
	}

	private static void OnGameBeingLoaded(string path, string fileName)
	{
		GameSaveLoadEventArgs e = new GameSaveLoadEventArgs(path, fileName);
		foreach (KeyValuePair<GameCustomFunctionController, string> registeredHandler in _registeredHandlers)
		{
			try
			{
				registeredHandler.Key.OnGameLoad(e);
			}
			catch (Exception ex)
			{
				KoikatuAPI.Logger.LogError((object)ex);
			}
		}
		try
		{
			GameAPI.GameLoad?.Invoke(KoikatuAPI.Instance, e);
		}
		catch (Exception ex2)
		{
			KoikatuAPI.Logger.LogError((object)ex2);
		}
	}

	private static void OnGameBeingSaved(string path, string fileName)
	{
		GameSaveLoadEventArgs e = new GameSaveLoadEventArgs(path, fileName);
		foreach (KeyValuePair<GameCustomFunctionController, string> registeredHandler in _registeredHandlers)
		{
			try
			{
				registeredHandler.Key.OnGameSave(e);
			}
			catch (Exception ex)
			{
				KoikatuAPI.Logger.LogError((object)ex);
			}
		}
		try
		{
			GameAPI.GameSave?.Invoke(KoikatuAPI.Instance, e);
		}
		catch (Exception ex2)
		{
			KoikatuAPI.Logger.LogError((object)ex2);
		}
	}

	private static void OnHEnd(MonoBehaviour baseLoader)
	{
		HSceneProc val = (HSceneProc)(object)((baseLoader is HSceneProc) ? baseLoader : null);
		HFlag hFlag = val?.flags ?? Object.FindObjectOfType<HFlag>();
		foreach (KeyValuePair<GameCustomFunctionController, string> registeredHandler in _registeredHandlers)
		{
			try
			{
				registeredHandler.Key.OnEndH(baseLoader, hFlag, val == null);
			}
			catch (Exception ex)
			{
				KoikatuAPI.Logger.LogError((object)ex);
			}
		}
		try
		{
			GameAPI.EndH?.Invoke(KoikatuAPI.Instance, EventArgs.Empty);
		}
		catch (Exception ex2)
		{
			KoikatuAPI.Logger.LogError((object)ex2);
		}
		InsideHScene = false;
	}

	private static IEnumerator OnHStart(MonoBehaviour baseLoader)
	{
		InsideHScene = true;
		yield return null;
		HSceneProc val = (HSceneProc)(object)((baseLoader is HSceneProc) ? baseLoader : null);
		HFlag hFlag = val?.flags ?? Object.FindObjectOfType<HFlag>();
		foreach (KeyValuePair<GameCustomFunctionController, string> registeredHandler in _registeredHandlers)
		{
			try
			{
				registeredHandler.Key.OnStartH(baseLoader, hFlag, val == null);
			}
			catch (Exception ex)
			{
				KoikatuAPI.Logger.LogError((object)ex);
			}
		}
		try
		{
			GameAPI.StartH?.Invoke(KoikatuAPI.Instance, EventArgs.Empty);
		}
		catch (Exception ex2)
		{
			KoikatuAPI.Logger.LogError((object)ex2);
		}
	}

	private static void OnDayChange(Week day)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		foreach (KeyValuePair<GameCustomFunctionController, string> registeredHandler in _registeredHandlers)
		{
			try
			{
				registeredHandler.Key.OnDayChange(day);
			}
			catch (Exception ex)
			{
				KoikatuAPI.Logger.LogError((object)ex);
			}
		}
		try
		{
			GameAPI.DayChange?.Invoke(KoikatuAPI.Instance, new DayChangeEventArgs(day));
		}
		catch (Exception ex2)
		{
			KoikatuAPI.Logger.LogError((object)ex2);
		}
	}

	private static void OnPeriodChange(Type period)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		foreach (KeyValuePair<GameCustomFunctionController, string> registeredHandler in _registeredHandlers)
		{
			try
			{
				registeredHandler.Key.OnPeriodChange(period);
			}
			catch (Exception ex)
			{
				KoikatuAPI.Logger.LogError((object)ex);
			}
		}
		try
		{
			GameAPI.PeriodChange?.Invoke(KoikatuAPI.Instance, new PeriodChangeEventArgs(period));
		}
		catch (Exception ex2)
		{
			KoikatuAPI.Logger.LogError((object)ex2);
		}
	}

	private static void OnNewGame()
	{
		foreach (KeyValuePair<GameCustomFunctionController, string> registeredHandler in _registeredHandlers)
		{
			try
			{
				registeredHandler.Key.OnNewGame();
			}
			catch (Exception ex)
			{
				KoikatuAPI.Logger.LogError((object)ex);
			}
		}
		try
		{
			GameAPI.NewGame?.Invoke(KoikatuAPI.Instance, EventArgs.Empty);
		}
		catch (Exception ex2)
		{
			KoikatuAPI.Logger.LogError((object)ex2);
		}
	}

	/// <summary>
	/// Gets the ActionControl instance if it's initialized, null otherwise
	/// </summary>
	public static ActionControl GetActionControl()
	{
		if (!SingletonInitializer<ActionControl>.initialized)
		{
			return null;
		}
		return SingletonInitializer<ActionControl>.instance;
	}

	/// <summary>
	/// Gets the ADVScene instance if it's initialized, null otherwise
	/// </summary>
	public static ADVScene GetADVScene()
	{
		if (!SingletonInitializer<ActionControl>.initialized)
		{
			return null;
		}
		ActionScene actionScene = SingletonInitializer<ActionControl>.instance.actionScene;
		if (actionScene == null)
		{
			return null;
		}
		return actionScene.AdvScene;
	}

	/// <summary>
	/// Gets the ActionScene instance if it's initialized, null otherwise
	/// </summary>
	public static ActionScene GetActionScene()
	{
		if (!SingletonInitializer<ActionControl>.initialized)
		{
			return null;
		}
		return SingletonInitializer<ActionControl>.instance.actionScene;
	}

	/// <summary>
	/// Gets the TalkScene instance if it's initialized, null otherwise
	/// </summary>
	public static TalkScene GetTalkScene()
	{
		if (!SingletonInitializer<TalkScene>.initialized)
		{
			return null;
		}
		return SingletonInitializer<TalkScene>.instance;
	}

	/// <summary>
	/// Find heroine that is currently in focus (in a talk or adv scene, leading girl in H scene).
	/// null if not found.
	/// </summary>
	public static Heroine GetCurrentHeroine()
	{
		HFlag val = Object.FindObjectOfType<HFlag>();
		if ((Object)(object)val != (Object)null)
		{
			return val.GetLeadingHeroine();
		}
		ADVScene aDVScene = GetADVScene();
		if ((Object)(object)aDVScene != (Object)null)
		{
			MonoBehaviour nowScene = aDVScene.nowScene;
			TalkScene val2 = (TalkScene)(object)((nowScene is TalkScene) ? nowScene : null);
			if (val2 != null && val2.targetHeroine != null)
			{
				return val2.targetHeroine;
			}
			if ((Object)(object)aDVScene.Scenario != (Object)null && aDVScene.Scenario.currentHeroine != null)
			{
				return aDVScene.Scenario.currentHeroine;
			}
		}
		TalkScene talkScene = GetTalkScene();
		if ((Object)(object)talkScene != (Object)null)
		{
			Heroine targetHeroine = talkScene.targetHeroine;
			if (targetHeroine != null)
			{
				return targetHeroine;
			}
		}
		SortedDictionary<int, ChaControl> dictEntryChara = Character.dictEntryChara;
		if (dictEntryChara != null && dictEntryChara.Count > 0)
		{
			return Character.dictEntryChara[0]?.GetHeroine();
		}
		return null;
	}
}
