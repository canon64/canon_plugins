using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Threading;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using ExtensibleSaveFormat;
using Freelancer604.MakeUp.Koi;
using Freelancer604.Wardrobe;
using KKAPI;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Studio;
using KKAPI.Utilities;
using Microsoft.CodeAnalysis;
using Studio;
using UnityEngine;
using UnityEngine.UI;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: TargetFramework(".NETFramework,Version=v4.6.2", FrameworkDisplayName = ".NET Framework 4.6.2")]
[assembly: AssemblyCompany("KKS_F604MakeUp")]
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0")]
[assembly: AssemblyProduct("Freelancer604 Vanity Studio")]
[assembly: AssemblyTitle("KKS_F604MakeUp")]
[assembly: AssemblyVersion("1.0.3.0")]
[module: RefSafetyRules(11)]
namespace Microsoft.CodeAnalysis
{
	[CompilerGenerated]
	[Embedded]
	internal sealed class EmbeddedAttribute : Attribute
	{
	}
}
namespace System.Runtime.CompilerServices
{
	[CompilerGenerated]
	[Embedded]
	[AttributeUsage(AttributeTargets.Module, AllowMultiple = false, Inherited = false)]
	internal sealed class RefSafetyRulesAttribute : Attribute
	{
		public readonly int Version;

		public RefSafetyRulesAttribute(int P_0)
		{
			Version = P_0;
		}
	}
}
namespace Freelancer604.MakeUp
{
	[BepInPlugin("com.freelancer604.makeup", "Freelancer604 Vanity Studio (Make-Up)", "1.0.3")]
	[BepInProcess("CharaStudio")]
	[BepInDependency("marco.kkapi", "1.42.2")]
	[BepInDependency(/*Could not decode attribute arguments.*/)]
	public sealed class MakeUpPlugin : BaseUnityPlugin
	{
		public const string GUID = "com.freelancer604.makeup";

		public const string PluginName = "Freelancer604 Vanity Studio (Make-Up)";

		public const string Version = "1.0.3";

		private const string ShellGuid = "com.freelancer604.wardrobe";

		private const int ShellContractVersion = 1;

		private const int PageOrder = 100;

		private static ManualLogSource log;

		private static ConfigEntry<string> lastReferencePath;

		private static readonly MakeUpMainThreadQueue mainThreadQueue = new MakeUpMainThreadQueue();

		private readonly MakeUpCompanionInputState companionInput = new MakeUpCompanionInputState();

		private bool companionCapturedThisFrame;

		private object suiteBridge;

		private static Func<object> suiteCharacterResolver;

		private MethodInfo tryRegister;

		private MakeUpPage page;

		private MakeUpCleanupPage cleanupPage;

		private ICleanupSourceBackend cleanupBackend;

		internal static string LastReferencePath
		{
			get
			{
				if (lastReferencePath != null)
				{
					return lastReferencePath.Value;
				}
				return string.Empty;
			}
		}

		internal static bool TryGetSuiteCharacter(out object character)
		{
			character = ((suiteCharacterResolver == null) ? null : suiteCharacterResolver());
			return suiteCharacterResolver != null;
		}

		private void Awake()
		{
			log = ((BaseUnityPlugin)this).Logger;
			Camera.onPreCull = (Camera.CameraCallback)Delegate.Combine(Camera.onPreCull, new Camera.CameraCallback(MaintainPreviewBeforeCamera));
			lastReferencePath = ((BaseUnityPlugin)this).Config.Bind<string>("State", "Last reference card", string.Empty, "Path of the last Make-Up reference character card, restored on startup.");
			CharacterApi.RegisterExtraBehaviour<ClothingBlendshapeController>("com.freelancer604.makeup.clothingblendshapes");
			page = new MakeUpPage((MonoBehaviour)(object)this);
			if (!TryGetActiveShell(out var host))
			{
				return;
			}
			MakeUpLang.Bind(((object)host.Instance).GetType().Assembly);
			if (MakeUpCleanupBackendFactory.TryCreate(host, out cleanupBackend))
			{
				MakeUpCleanupRuntime.Initialize(cleanupBackend);
				cleanupBackend.ProcessPendingMoves();
				cleanupPage = new MakeUpCleanupPage(this, cleanupBackend);
			}
			if (!TryRegisterRefreshedShell(host, out var publicBridgeExists))
			{
				if (publicBridgeExists)
				{
					page = null;
					return;
				}
				page = null;
				LogError("Make-Up could not safely attach to this legacy Dress-Up Plus build. No standalone fallback is implemented in this release; legacy Dress-Up remains unchanged.");
			}
		}

		private void Update()
		{
			if (StudioAPI.InsideStudio)
			{
				WardrobePseudoMakerGuards.EnsureInstalled();
			}
			CaptureCompanionInput();
			try
			{
				mainThreadQueue.DrainOne();
			}
			catch (Exception ex)
			{
				LogWarning("Deferred Make-Up operation failed: " + ex.Message);
			}
		}

		private void LateUpdate()
		{
			if (cleanupPage == null)
			{
				return;
			}
			try
			{
				cleanupPage.MaintainRuntimePreview();
			}
			catch (Exception ex)
			{
				LogWarning("Clean-Up preview visibility failed: " + ex.Message);
			}
		}

		private void MaintainPreviewBeforeCamera(Camera ignored)
		{
			if (cleanupPage == null)
			{
				return;
			}
			try
			{
				cleanupPage.MaintainRuntimePreview();
			}
			catch
			{
			}
		}

		private void OnDestroy()
		{
			Camera.onPreCull = (Camera.CameraCallback)Delegate.Remove(Camera.onPreCull, new Camera.CameraCallback(MaintainPreviewBeforeCamera));
		}

		private void CaptureCompanionInput()
		{
			bool mouseOverCompanion = MakeUpLang.HostUiVisible && page != null && page.MouseOverCompanion;
			bool buttonDown = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
			bool buttonHeld = Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);
			bool buttonUp = Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2);
			companionCapturedThisFrame = companionInput.Observe(mouseOverCompanion, buttonDown, buttonHeld, buttonUp);
			if (companionCapturedThisFrame)
			{
				Input.ResetInputAxes();
			}
		}

		private void OnGUI()
		{
			if (page == null || !MakeUpLang.HostUiVisible)
			{
				return;
			}
			Matrix4x4 matrix = MakeUpUiScale.BeginGuiScale();
			bool resumeTranslator;
			Font previous = MakeUpLang.BeginDetachedGui(out resumeTranslator);
			try
			{
				page.DrawCompanion();
				ConsumeCompanionPointerEvent();
			}
			catch (Exception ex)
			{
				LogWarning("Make-Up sidecar draw failed: " + ex.Message);
			}
			finally
			{
				MakeUpLang.EndDetachedGui(previous, resumeTranslator);
				GUI.matrix = matrix;
			}
		}

		private void ConsumeCompanionPointerEvent()
		{
			Event current = Event.current;
			if (current != null && current.type != EventType.Used && (current.type == EventType.MouseDown || current.type == EventType.MouseUp || current.type == EventType.MouseDrag || current.type == EventType.ScrollWheel || current.type == EventType.ContextClick) && (companionCapturedThisFrame || companionInput.GestureOwned || (page != null && page.MouseOverCompanion)))
			{
				current.Use();
			}
		}

		internal static void EnqueueMainThread(Action action)
		{
			mainThreadQueue.Enqueue(action);
		}

		internal static void RememberReferencePath(string path)
		{
			if (lastReferencePath != null && !string.IsNullOrEmpty(path))
			{
				lastReferencePath.Value = path;
			}
		}

		internal void NotifyCharacterCleanupChanged()
		{
			if (page != null)
			{
				page.InvalidateAfterCleanup();
			}
		}

		internal bool RequestCleanupItem(string contributorId, int resolverCategory, int itemId)
		{
			if (suiteBridge == null)
			{
				return false;
			}
			MethodInfo method = suiteBridge.GetType().GetMethod("RequestCleanupItem", BindingFlags.Instance | BindingFlags.Public, null, new Type[3]
			{
				typeof(string),
				typeof(int),
				typeof(int)
			}, null);
			if (method == null || method.ReturnType != typeof(bool))
			{
				return false;
			}
			try
			{
				return (bool)method.Invoke(suiteBridge, new object[3] { contributorId, resolverCategory, itemId });
			}
			catch (Exception ex)
			{
				LogWarning("Cross-cleanup navigation failed: " + ex.Message);
				return false;
			}
		}

		private bool TryGetActiveShell(out PluginInfo host)
		{
			if (!Chainloader.PluginInfos.TryGetValue("com.freelancer604.wardrobe", out host) || host == null || (UnityEngine.Object)(object)host.Instance == null)
			{
				LogError("Make-Up requires the active Dress-Up shell, but no active shell instance was found.");
				return false;
			}
			return true;
		}

		private bool TryRegisterRefreshedShell(PluginInfo host, out bool publicBridgeExists)
		{
			publicBridgeExists = false;
			MethodInfo method = ((object)host.Instance).GetType().GetMethod("GetSuiteBridgeV1", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
			if (method == null)
			{
				return false;
			}
			publicBridgeExists = true;
			if (method.ReturnType != typeof(object))
			{
				LogError("The active Dress-Up shell exposes an incompatible public GetSuiteBridgeV1(). Legacy patching will not be attempted against a refreshed host.");
				return false;
			}
			try
			{
				suiteBridge = method.Invoke(host.Instance, null);
			}
			catch (Exception ex)
			{
				LogError("The active Dress-Up shell bridge could not be opened; legacy patching will not be attempted. " + ex.Message);
				return false;
			}
			if (suiteBridge == null)
			{
				LogError("The active Dress-Up shell returned no suite bridge; legacy patching will not be attempted.");
				return false;
			}
			MethodInfo method2 = suiteBridge.GetType().GetMethod("GetActiveCharacter", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
			if (method2 != null && method2.ReturnType == typeof(object))
			{
				suiteCharacterResolver = (Func<object>)Delegate.CreateDelegate(typeof(Func<object>), suiteBridge, method2);
			}
			tryRegister = suiteBridge.GetType().GetMethod("TryRegister", BindingFlags.Instance | BindingFlags.Public, null, new Type[5]
			{
				typeof(string),
				typeof(string),
				typeof(int),
				typeof(int),
				typeof(object)
			}, null);
			if (tryRegister == null || tryRegister.ReturnType != typeof(bool))
			{
				LogError("The active Dress-Up shell bridge is incompatible: TryRegister signature is unavailable. Legacy patching will not be attempted.");
				suiteBridge = null;
				return false;
			}
			try
			{
				object obj = tryRegister.Invoke(suiteBridge, new object[5] { "page", "makeup", 100, 1, page });
				if (!(obj is bool) || !(bool)obj)
				{
					LogError("Make-Up page registration was rejected by the active Dress-Up shell; legacy patching will not be attempted.");
					return false;
				}
				if (cleanupPage != null)
				{
					obj = tryRegister.Invoke(suiteBridge, new object[5] { "cleanup", "character", 100, 1, cleanupPage });
					if (!(obj is bool) || !(bool)obj)
					{
						LogError("Character Clean-Up registration was rejected by the active Dress-Up Plus shell.");
						return false;
					}
				}
			}
			catch (Exception ex2)
			{
				LogError("Make-Up page registration failed; legacy patching will not be attempted. " + ex2.Message);
				return false;
			}
			return true;
		}

		internal static void LogWarning(string message)
		{
			if (log != null)
			{
				log.LogWarning((object)message);
			}
		}

		internal static void LogMessage(string message)
		{
			if (log != null)
			{
				log.LogDebug((object)message);
			}
		}

		private static void LogError(string message)
		{
			if (log != null)
			{
				log.LogError((object)message);
			}
		}
	}
	internal sealed class MakeUpSession
	{
		private readonly ChaControl character;

		private readonly int characterId;

		private readonly MakeUpSessionState state;

		private MakeUpSelectionLease selectionLease;

		private bool selectionCurrent;

		internal string MissingAssetNotice { get; set; }

		internal int ActiveTab => state.ActiveTab;

		internal ChaControl Character => character;

		internal long SelectionGeneration => selectionLease.Generation;

		internal void ReportMissingAsset(string label, int id)
		{
			string text = label + " (" + id + ")";
			if (string.IsNullOrEmpty(MissingAssetNotice))
			{
				MissingAssetNotice = "Unavailable cosmetic (uses game default on successful restore): " + text;
			}
			else if (!MissingAssetNotice.Contains(text))
			{
				MissingAssetNotice = MissingAssetNotice + ", " + text;
			}
		}

		private MakeUpSession(ChaControl character, MakeUpSessionState uiState)
		{
			this.character = character;
			characterId = character.GetInstanceID();
			state = uiState ?? new MakeUpSessionState();
			state.SetCharacterToken(characterId);
			selectionLease = MakeUpSelectionLease.Acquire();
			selectionCurrent = true;
		}

		internal bool IsCurrentSelection(ChaControl expected, long generation)
		{
			if (expected != null && selectionCurrent && generation == selectionLease.Generation && selectionLease.IsCurrent && expected == character)
			{
				return characterId == expected.GetInstanceID();
			}
			return false;
		}

		internal static MakeUpSession Resolve(MakeUpSession prior)
		{
			return Resolve(prior, (prior == null) ? new MakeUpSessionState() : prior.state);
		}

		internal static MakeUpSession Resolve(MakeUpSession prior, MakeUpSessionState uiState)
		{
			ChaControl activeCharacter = GetActiveCharacter();
			if (activeCharacter == null)
			{
				if (prior != null)
				{
					prior.InvalidateSelection();
				}
				else
				{
					MakeUpSelectionLease.InvalidateCurrent();
				}
				return null;
			}
			if (prior != null && prior.character == activeCharacter && prior.characterId == activeCharacter.GetInstanceID())
			{
				prior.ReactivateSelection();
				return prior;
			}
			prior?.InvalidateSelection();
			return new MakeUpSession(activeCharacter, uiState);
		}

		internal void RequestTab(int tab)
		{
			state.RequestTab(tab);
		}

		internal void ApplyPendingTab(bool isLayout)
		{
			state.ApplyPendingTab(isLayout);
		}

		internal float GetScroll(int tab)
		{
			return state.GetScroll(tab);
		}

		internal void SetScroll(int tab, float value)
		{
			state.SetScroll(tab, value);
		}

		internal bool GetSectionExpanded(string sectionId)
		{
			return state.GetSectionExpanded(sectionId);
		}

		internal void SetSectionExpanded(string sectionId, bool expanded)
		{
			state.SetSectionExpanded(sectionId, expanded);
		}

		internal void ResetUiState()
		{
			state.Reset();
		}

		internal void InvalidateSelection()
		{
			selectionCurrent = false;
			selectionLease.Invalidate();
		}

		internal void SuspendSelection()
		{
			if (selectionCurrent)
			{
				selectionCurrent = false;
				selectionLease.Invalidate();
			}
		}

		private void ReactivateSelection()
		{
			if (!selectionCurrent || !selectionLease.IsCurrent)
			{
				selectionLease = MakeUpSelectionLease.Acquire();
				selectionCurrent = true;
			}
		}

		private static ChaControl GetActiveCharacter()
		{
			try
			{
				if (StudioAPI.InsideStudio)
				{
					if (MakeUpPlugin.TryGetSuiteCharacter(out var obj))
					{
						return obj as ChaControl;
					}
					foreach (OCIChar selectedCharacter in StudioAPI.GetSelectedCharacters())
					{
						ChaControl chaControl = selectedCharacter?.charInfo;
						if (chaControl != null)
						{
							return chaControl;
						}
					}
					return null;
				}
				if (MakerAPI.InsideAndLoaded)
				{
					return MakerAPI.GetCharacterControl();
				}
			}
			catch (Exception ex)
			{
				MakeUpPlugin.LogWarning("Character selection is not ready: " + ex.Message);
			}
			return null;
		}
	}
	internal sealed class MakeUpSessionState
	{
		private readonly float[] scrollY = new float[4];

		private readonly Dictionary<string, bool> sections = new Dictionary<string, bool>(StringComparer.Ordinal);

		private int activeTab;

		private int pendingTab = -1;

		private int characterToken;

		private bool initialNoTargetRouted;

		public int ActiveTab => activeTab;

		public int CharacterToken => characterToken;

		public void RequestTab(int tab)
		{
			pendingTab = NormalizeTab(tab);
		}

		public void ApplyPendingTab(bool isLayout)
		{
			if (isLayout && pendingTab >= 0)
			{
				activeTab = pendingTab;
				pendingTab = -1;
			}
		}

		public int PrepareNoTargetTab(bool isLayout)
		{
			if (!initialNoTargetRouted && characterToken == 0)
			{
				RequestTab(3);
				if (isLayout)
				{
					initialNoTargetRouted = true;
				}
				ApplyPendingTab(isLayout);
				return 3;
			}
			ApplyPendingTab(isLayout);
			return activeTab;
		}

		public float GetScroll(int tab)
		{
			return scrollY[NormalizeTab(tab)];
		}

		public void SetScroll(int tab, float value)
		{
			scrollY[NormalizeTab(tab)] = Math.Max(0f, value);
		}

		public bool GetSectionExpanded(string sectionId)
		{
			bool value = default(bool);
			return !string.IsNullOrEmpty(sectionId) && sections.TryGetValue(sectionId, out value) && value;
		}

		public void SetSectionExpanded(string sectionId, bool expanded)
		{
			if (!string.IsNullOrEmpty(sectionId))
			{
				sections[sectionId] = expanded;
			}
		}

		public bool SetCharacterToken(int token)
		{
			if (token != 0)
			{
				initialNoTargetRouted = true;
			}
			if (characterToken == token)
			{
				return false;
			}
			characterToken = token;
			return true;
		}

		public void Reset()
		{
			activeTab = 0;
			pendingTab = -1;
			scrollY[0] = 0f;
			scrollY[1] = 0f;
			scrollY[2] = 0f;
			scrollY[3] = 0f;
			sections.Clear();
			characterToken = 0;
			initialNoTargetRouted = false;
		}

		private static int NormalizeTab(int tab)
		{
			return tab switch
			{
				2 => 2, 
				3 => 3, 
				_ => (tab == 1) ? 1 : 0, 
			};
		}
	}
	internal sealed class MakeUpSelectionLease
	{
		private static long currentGeneration;

		private readonly long generation;

		private bool active;

		internal long Generation => generation;

		internal bool IsCurrent
		{
			get
			{
				if (active)
				{
					return Interlocked.Read(ref currentGeneration) == generation;
				}
				return false;
			}
		}

		private MakeUpSelectionLease(long generation)
		{
			this.generation = generation;
			active = true;
		}

		internal static MakeUpSelectionLease Acquire()
		{
			return new MakeUpSelectionLease(Interlocked.Increment(ref currentGeneration));
		}

		internal static void InvalidateCurrent()
		{
			Interlocked.Increment(ref currentGeneration);
		}

		internal void Invalidate()
		{
			active = false;
			Interlocked.CompareExchange(ref currentGeneration, generation + 1, generation);
		}
	}
	internal sealed class MakeUpMainThreadQueue
	{
		private readonly object sync = new object();

		private readonly Queue<Action> pending = new Queue<Action>();

		internal void Enqueue(Action action)
		{
			if (action == null)
			{
				return;
			}
			lock (sync)
			{
				pending.Enqueue(action);
			}
		}

		internal bool DrainOne()
		{
			Action action;
			lock (sync)
			{
				if (pending.Count == 0)
				{
					return false;
				}
				action = pending.Dequeue();
			}
			action();
			return true;
		}
	}
	internal sealed class MakeUpCompanionInputState
	{
		private bool gestureActive;

		private bool gestureOwned;

		internal bool GestureOwned
		{
			get
			{
				if (gestureActive)
				{
					return gestureOwned;
				}
				return false;
			}
		}

		internal bool Observe(bool mouseOverCompanion, bool buttonDown, bool buttonHeld, bool buttonUp)
		{
			if (buttonDown)
			{
				gestureActive = true;
				gestureOwned = mouseOverCompanion;
			}
			bool result = ((!(buttonDown || buttonHeld || buttonUp)) ? mouseOverCompanion : (gestureActive && gestureOwned));
			if (buttonUp || (!buttonDown && !buttonHeld && !buttonUp))
			{
				gestureActive = false;
				gestureOwned = false;
			}
			return result;
		}

		internal void Reset()
		{
			gestureActive = false;
			gestureOwned = false;
		}
	}
	internal static class MakeUpLang
	{
		private struct DetachedGuiState
		{
			internal GUISkin PreviousSkin;

			internal GUISkin DrawSkin;

			internal Font DrawFont;

			internal Color Color;

			internal Color Background;

			internal Color Content;
		}

		private static PropertyInfo shellLanguage;

		private static Func<bool> shellUiVisible;

		private static Func<Action, bool> shellDeferFooter;

		private static Func<Vector2> shellBodyOffset;

		private static Font cjkFont;

		private static bool fontTried;

		private static bool translatorResolved;

		private static object translator;

		private static MethodInfo disableTranslator;

		private static MethodInfo enableTranslator;

		private static MethodInfo translatorIsDisabled;

		private static MethodInfo shellThemeGetSkin;

		private static PropertyInfo shellThemeIsActive;

		private static Action<Rect> shellDrawDrawer;

		private static readonly Stack<DetachedGuiState> detachedStates = new Stack<DetachedGuiState>();

		internal static bool HostUiVisible
		{
			get
			{
				if (shellUiVisible != null)
				{
					return shellUiVisible();
				}
				return true;
			}
		}

		internal static Vector2 HostBodyOffset
		{
			get
			{
				if (shellBodyOffset != null)
				{
					return shellBodyOffset();
				}
				return Vector2.zero;
			}
		}

		internal static bool HasDrawerWrapper => shellDrawDrawer != null;

		internal static bool IsShellThemeActive
		{
			get
			{
				try
				{
					return shellThemeIsActive != null && (bool)shellThemeIsActive.GetValue(null, null);
				}
				catch
				{
					return false;
				}
			}
		}

		internal static int Current
		{
			get
			{
				try
				{
					int num = ((!(shellLanguage == null)) ? Convert.ToInt32(shellLanguage.GetValue(null, null)) : 0);
					return (num >= 0 && num <= 2) ? num : 0;
				}
				catch
				{
					return 0;
				}
			}
		}

		internal static bool DeferFooter(Action draw)
		{
			if (shellDeferFooter != null)
			{
				return shellDeferFooter(draw);
			}
			return false;
		}

		internal static void Bind(Assembly shellAssembly)
		{
			if (shellAssembly == null)
			{
				return;
			}
			Type type = shellAssembly.GetType("Freelancer604.Wardrobe.SuiteStudioUiVisibility", throwOnError: false);
			MethodInfo methodInfo = ((type == null) ? null : type.GetMethod("IsVisible", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
			shellUiVisible = ((methodInfo == null) ? null : ((Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), methodInfo)));
			Type type2 = shellAssembly.GetType("Freelancer604.Wardrobe.WardrobeLang", throwOnError: false);
			shellLanguage = ((type2 == null) ? null : type2.GetProperty("Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
			Type type3 = shellAssembly.GetType("Freelancer604.Wardrobe.SuiteMakeUpTheme", throwOnError: false);
			shellThemeGetSkin = ((type3 == null) ? null : type3.GetMethod("GetSkin", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(GUISkin) }, null));
			if (shellThemeGetSkin != null && shellThemeGetSkin.ReturnType != typeof(GUISkin))
			{
				shellThemeGetSkin = null;
			}
			shellThemeIsActive = ((type3 == null) ? null : type3.GetProperty("IsActive", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
			shellDrawDrawer = null;
			Type type4 = shellAssembly.GetType("Freelancer604.Wardrobe.SuiteWrapperTheme", throwOnError: false);
			shellDeferFooter = null;
			shellBodyOffset = null;
			MethodInfo methodInfo2 = ((type4 == null) ? null : type4.GetMethod("BodyOffset", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null));
			if (methodInfo2 != null && methodInfo2.ReturnType == typeof(Vector2))
			{
				try
				{
					shellBodyOffset = (Func<Vector2>)Delegate.CreateDelegate(typeof(Func<Vector2>), methodInfo2);
				}
				catch
				{
				}
			}
			MethodInfo methodInfo3 = ((type4 == null) ? null : type4.GetMethod("DeferFooter", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(Action) }, null));
			if (methodInfo3 != null && methodInfo3.ReturnType == typeof(bool))
			{
				try
				{
					shellDeferFooter = (Func<Action, bool>)Delegate.CreateDelegate(typeof(Func<Action, bool>), methodInfo3);
				}
				catch
				{
				}
			}
			MethodInfo methodInfo4 = ((type4 == null) ? null : type4.GetMethod("DrawDrawer", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(Rect) }, null));
			if (!(methodInfo4 != null) || !(methodInfo4.ReturnType == typeof(void)))
			{
				return;
			}
			try
			{
				shellDrawDrawer = (Action<Rect>)Delegate.CreateDelegate(typeof(Action<Rect>), methodInfo4);
			}
			catch
			{
			}
		}

		internal static void DrawDrawerWrapper(Rect rect)
		{
			if (shellDrawDrawer == null || Event.current == null || Event.current.type != EventType.Repaint)
			{
				return;
			}
			try
			{
				shellDrawDrawer(rect);
			}
			catch
			{
			}
		}

		internal static string T(string english)
		{
			int current = Current;
			return MakeUpTranslationCatalog.ProtectForDisplay(current, MakeUpTranslationCatalog.Translate(current, english));
		}

		internal static string F(string english, params object[] values)
		{
			return string.Format(T(english), values);
		}

		internal static Font BeginDetachedGui(out bool resumeTranslator)
		{
			Font result = ((GUI.skin == null) ? null : GUI.skin.font);
			DetachedGuiState item = new DetachedGuiState
			{
				PreviousSkin = GUI.skin,
				Color = GUI.color,
				Background = GUI.backgroundColor,
				Content = GUI.contentColor
			};
			if (shellThemeGetSkin != null)
			{
				try
				{
					GUISkin gUISkin = shellThemeGetSkin.Invoke(null, new object[1] { GUI.skin }) as GUISkin;
					if (gUISkin != null)
					{
						GUI.skin = gUISkin;
						GUI.color = (GUI.backgroundColor = (GUI.contentColor = Color.white));
					}
				}
				catch
				{
				}
			}
			item.DrawSkin = GUI.skin;
			item.DrawFont = ((GUI.skin == null) ? null : GUI.skin.font);
			detachedStates.Push(item);
			if (Current != 0)
			{
				if (!IsShellThemeActive)
				{
					EnsureFont();
					if (GUI.skin != null && cjkFont != null)
					{
						GUI.skin.font = cjkFont;
					}
				}
				resumeTranslator = SuppressAutoTranslator();
			}
			else
			{
				resumeTranslator = false;
			}
			return result;
		}

		internal static void EndDetachedGui(Font previous, bool resumeTranslator)
		{
			if (detachedStates.Count > 0)
			{
				DetachedGuiState detachedGuiState = detachedStates.Pop();
				if (detachedGuiState.DrawSkin != null)
				{
					detachedGuiState.DrawSkin.font = detachedGuiState.DrawFont;
				}
				GUI.skin = detachedGuiState.PreviousSkin;
				GUI.color = detachedGuiState.Color;
				GUI.backgroundColor = detachedGuiState.Background;
				GUI.contentColor = detachedGuiState.Content;
			}
			else if (GUI.skin != null)
			{
				GUI.skin.font = previous;
			}
			if (resumeTranslator)
			{
				ResumeAutoTranslator();
			}
		}

		private static void EnsureFont()
		{
			if (fontTried)
			{
				return;
			}
			fontTried = true;
			try
			{
				cjkFont = Font.CreateDynamicFontFromOSFont(new string[9] { "Microsoft YaHei", "Microsoft YaHei UI", "Yu Gothic", "Meiryo", "MS Gothic", "Malgun Gothic", "SimSun", "Arial Unicode MS", "Arial" }, 16);
			}
			catch (Exception ex)
			{
				MakeUpPlugin.LogWarning("CJK UI font load failed: " + ex.Message);
			}
		}

		private static bool SuppressAutoTranslator()
		{
			if (!translatorResolved)
			{
				ResolveAutoTranslator();
			}
			if (translator == null || disableTranslator == null)
			{
				return false;
			}
			try
			{
				if (translatorIsDisabled != null && (bool)translatorIsDisabled.Invoke(translator, null))
				{
					return false;
				}
				disableTranslator.Invoke(translator, null);
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static void ResolveAutoTranslator()
		{
			translatorResolved = true;
			try
			{
				Type type = null;
				Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
				foreach (Assembly assembly in assemblies)
				{
					if (assembly.GetName().Name == "XUnity.AutoTranslator.Plugin.Core")
					{
						type = assembly.GetType("XUnity.AutoTranslator.Plugin.Core.AutoTranslationPlugin", throwOnError: false);
						break;
					}
				}
				if (!(type == null))
				{
					PropertyInfo property = type.GetProperty("Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
					FieldInfo field = type.GetField("Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
					translator = ((property != null) ? property.GetValue(null, null) : ((field == null) ? null : field.GetValue(null)));
					if (translator != null)
					{
						disableTranslator = type.GetMethod("DisableAutoTranslator", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						enableTranslator = type.GetMethod("EnableAutoTranslator", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						translatorIsDisabled = type.GetMethod("IsTemporarilyDisabled", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					}
				}
			}
			catch (Exception ex)
			{
				MakeUpPlugin.LogWarning("AutoTranslator resolve failed: " + ex.Message);
			}
		}

		private static void ResumeAutoTranslator()
		{
			try
			{
				if (enableTranslator != null)
				{
					enableTranslator.Invoke(translator, null);
				}
			}
			catch
			{
			}
		}
	}
	internal static class MakeUpTranslationCatalog
	{
		internal const int English = 0;

		internal const int Chinese = 1;

		internal const int Japanese = 2;

		private static readonly Dictionary<string, string[]> Map = Build();

		internal static string Translate(int language, string english)
		{
			if (language == 0 || string.IsNullOrEmpty(english))
			{
				return english;
			}
			if (Map.TryGetValue(english, out var value))
			{
				return value[language - 1];
			}
			return TranslatePattern(language, english);
		}

		internal static string ProtectForDisplay(int language, string translated)
		{
			if (language == 0 || string.IsNullOrEmpty(translated))
			{
				return translated;
			}
			if (translated[0] == '\u180e')
			{
				return translated;
			}
			return "\u180e" + translated.Replace("\u180e", string.Empty);
		}

		private static string TranslatePattern(int language, string text)
		{
			foreach (KeyValuePair<string, string[]> item in Map)
			{
				int num = item.Key.IndexOf("{0}", StringComparison.Ordinal);
				if (num >= 0)
				{
					string text2 = item.Key.Substring(0, num);
					string text3 = item.Key.Substring(num + 3);
					if (text.StartsWith(text2, StringComparison.Ordinal) && text.EndsWith(text3, StringComparison.Ordinal) && text.Length >= text2.Length + text3.Length)
					{
						string newValue = text.Substring(text2.Length, text.Length - text2.Length - text3.Length);
						return item.Value[language - 1].Replace("{0}", newValue);
					}
				}
			}
			return text;
		}

		private static Dictionary<string, string[]> Build()
		{
			Dictionary<string, string[]> map = new Dictionary<string, string[]>(StringComparer.Ordinal);
			Action<string, string, string> action = delegate(string en, string zh, string ja)
			{
				map[en] = new string[2] { zh, ja };
			};
			action("Eastern Nose 1", "东方鼻型 1", "東洋風の鼻 1");
			action("Sultry Eyes", "妩媚眼型", "艶やかな目");
			action("Lips Volume", "嘴唇丰满度", "唇のボリューム");
			action("Lower Features Ratio", "下部五官比例", "顔下部のパーツ比率");
			action("Eyeshadow Fix 1", "眼影修正 1", "アイシャドウ補正 1");
			action("Eyeshadow Fix 2", "眼影修正 2", "アイシャドウ補正 2");
			action("Face Base", "脸部基础", "顔の基本");
			action("Eye FX", "眼部效果", "目の効果");
			action("Face", "脸部", "顔");
			action("Body", "身体", "体");
			action("Hair", "头发", "髪");
			action("Home", "主页", "ホーム");
			action("Face Skin", "脸部皮肤", "顔の肌");
			action("L Eye", "左眼", "左目");
			action("R Eye", "右眼", "右目");
			action("Face Shape", "脸型", "顔形");
			action("Face Details", "脸部细节", "顔の詳細");
			action("Eyes", "眼睛", "目");
			action("Makeup", "妆容", "メイク");
			action("Overlays", "覆盖图", "オーバーレイ");
			action("Body Shape", "体型", "体型");
			action("Body Details", "身体细节", "体の詳細");
			action("Paint / Overlays", "彩绘／覆盖图", "ペイント／オーバーレイ");
			action("Drop here", "拖放到这里", "ここにドロップ");
			action("Search", "搜索", "検索");
			action("Copy", "复制", "コピー");
			action("Paste", "粘贴", "貼り付け");
			action("Reset", "重置", "リセット");
			action("Load", "加载", "読込");
			action("Clear", "清除", "クリア");
			action("Export", "导出", "書き出し");
			action("Undo", "撤销", "元に戻す");
			action("Redo", "重做", "やり直す");
			action("All Hair (default)", "所有头发（默认）", "すべての髪（既定）");
			action("Auto Shades", "自动色调", "色合いを自動調整");
			action("Mirror both eyes (default)", "镜像双眼（默认）", "両目をミラー（既定）");
			action("No controls are available for this character.", "此角色没有可用的控制项。", "このキャラクターで使用できる項目はありません。");
			action("Copy Body", "复制身体", "体をコピー");
			action("Copy Head", "复制头部", "頭部をコピー");
			action("Hide Clothes", "隐藏衣服", "服を隠す");
			action("Clothes Hidden", "衣服已隐藏", "服を非表示中");
			action("Shapes", "形状", "形状");
			action("Appearance", "外观", "外観");
			action("Colors", "颜色", "カラー");
			action("Head", "头部", "頭部");
			action("Face Detail", "脸部细节", "顔ディテール");
			action("Upper Eye Highlight", "上眼高光", "上目ハイライト");
			action("Lower Eye Highlight", "下眼高光", "下目ハイライト");
			action("Upper Eyeline", "上眼线", "上アイライン");
			action("Lower Eyeline", "下眼线", "下アイライン");
			action("Eyeshadow", "眼影", "アイシャドウ");
			action("Lip", "唇彩", "リップ");
			action("Lip Line", "唇线", "リップライン");
			action("Body Detail", "身体细节", "体のディテール");
			action("Body Paint 1", "身体彩绘 1", "ボディペイント 1");
			action("Body Paint 2", "身体彩绘 2", "ボディペイント 2");
			action("Sunburn", "晒痕", "日焼け");
			action("Nipples", "乳头", "乳首");
			action("Underhair", "阴毛", "陰毛");
			action("Nails", "指甲", "爪");
			action("Back Hair", "后发", "後ろ髪");
			action("Front Hair", "前发", "前髪");
			action("Side Hair", "侧发", "横髪");
			action("Hair Option", "附加发型", "髪オプション");
			action("Skin Main", "皮肤主色", "肌メイン");
			action("Skin Sub", "皮肤副色", "肌サブ");
			action("Eye Whites", "眼白", "白目");
			action("Upper Highlight", "上高光", "上ハイライト");
			action("Lower Highlight", "下高光", "下ハイライト");
			action("Eyeline", "眼线", "アイライン");
			action("Mole", "痣", "ほくろ");
			action("Face {0}", "脸部 {0}", "顔 {0}");
			action("Body {0}", "身体 {0}", "体 {0}");
			action("Select a character to edit her appearance.", "请选择要编辑外观的角色。", "外観を編集するキャラクターを選択してください。");
			action("Edit the selected character's native face, body and hair settings without opening Maker.", "无需打开捏人界面即可编辑所选角色的原生脸部、身体和头发设置。", "キャラメイクを開かずに、選択中のキャラクターの顔・体・髪を編集します。");
			action("F604 morph sliders are exclusive to HS2 Make-Up Plus.", "F604 变形滑块仅适用于 HS2 Make-Up Plus。", "F604モーフスライダーはHS2 Make-Up Plus専用です。");
			action("Paste Body", "粘贴身体", "体を貼り付け");
			action("Paste Head", "粘贴头部", "頭部を貼り付け");
			action("Head (including hair)", "头部（包括头发）", "頭部（髪を含む）");
			action("Head (including hair) copied.", "头部（包括头发）已复制。", "頭部（髪を含む）をコピーしました。");
			action("Body copied.", "身体已复制。", "体をコピーしました。");
			action("Head (including hair) pasted.", "头部（包括头发）已粘贴。", "頭部（髪を含む）を貼り付けました。");
			action("Body pasted.", "身体已粘贴。", "体を貼り付けました。");
			action("Paste rejected: {0}", "粘贴被拒绝：{0}", "貼り付けが拒否されました：{0}");
			action("{0} restored to its launch state.", "{0} 已恢复到启动时状态。", "{0}を起動時の状態へ戻しました。");
			action("{0} reset was rolled back: {1}", "{0} 重置已回滚：{1}", "{0}のリセットを巻き戻しました：{1}");
			action("optional provider is not installed.", "未安装可选提供程序。", "オプション機能がインストールされていません。");
			action("Overlay Mods is not installed.", "未安装 Overlay Mods。", "Overlay Modsがインストールされていません。");
			action("Open overlay image", "打开覆盖图图像", "オーバーレイ画像を開く");
			action("Reference Character", "参考角色", "参照キャラクター");
			action("Choose a reference card", "选择一张参考角色卡", "参照キャラカードを選択");
			action("Reference", "参考角色", "参照キャラ");
			action("Snapshot", "快照", "スナップショット");
			action("Name", "名称", "名前");
			action("Select Reference", "选择参考角色", "参照キャラを選択");
			action("Load Reference", "加载参考角色", "参照キャラを読み込む");
			action("Copy All", "全部复制", "すべてコピー");
			action("Import All", "全部导入", "すべて読み込む");
			action("Import Head", "导入头部", "頭部を読み込む");
			action("Import Body", "导入身体", "体を読み込む");
			action("Import Makeup", "导入妆容", "メイクを読み込む");
			action("Import Clothes", "导入服装", "服を読み込む");
			action("Import Acc", "导入饰品", "アクセサリーを読み込む");
			action("Import Accessories", "导入饰品", "アクセサリーを読み込む");
			action("Copy Reference Portrait", "复制参考头像", "参照ポートレートをコピー");
			action("Save Character", "保存角色", "キャラクターを保存");
			action("Cancel Save ({0})", "取消保存（{0}）", "保存をキャンセル（{0}）");
			action("Personality / Identity", "性格／身份", "性格／設定");
			action("Pitch", "音高", "声の高さ");
			action("Month", "月", "月");
			action("Day", "日", "日");
			action("Personality", "性格", "性格");
			action("Trait", "特质", "特性");
			action("Mentality", "心态", "精神状態");
			action("Sex Trait", "性爱特质", "H特性");
			action("(none)", "（无）", "（なし）");
			action("Load the Reference into Studio, then select a character to edit or replace.", "先将参考角色加载到工作室，然后选择要编辑或替换的角色。", "参照キャラをスタジオへ読み込み、編集または置換するキャラクターを選択してください。");
			action("Up", "上一级", "上へ");
			action("No character cards in this folder.", "此文件夹中没有角色卡。", "このフォルダーにキャラカードはありません。");
			action("loading...", "加载中…", "読み込み中…");
			action("Save cancelled.", "已取消保存。", "保存をキャンセルしました。");
			action("Take a Snapshot or copy the Reference portrait first.", "请先拍摄快照或复制参考头像。", "先にスナップショットを撮るか、参照ポートレートをコピーしてください。");
			action("Reference loaded: {0}", "已加载参考角色：{0}", "参照キャラを読み込みました：{0}");
			action("Reference could not be loaded: {0}", "无法加载参考角色：{0}", "参照キャラを読み込めませんでした：{0}");
			action("Reference added to Studio: {0}", "已将参考角色添加到工作室：{0}", "参照キャラをスタジオへ追加しました：{0}");
			action("Reference could not be added: {0}", "无法添加参考角色：{0}", "参照キャラを追加できませんでした：{0}");
			action("Reference portrait copied to Snapshot.", "参考头像已复制到快照。", "参照ポートレートをスナップショットへコピーしました。");
			action("Selected character replaced from Reference.", "已用参考角色替换所选角色。", "選択中のキャラクターを参照キャラで置換しました。");
			action("Copy All failed: {0}", "全部复制失败：{0}", "すべてコピーに失敗しました：{0}");
			action("Snapshot taken!", "快照已拍摄！", "スナップショットを撮りました！");
			action("No camera is available for the Snapshot.", "没有可用于快照的相机。", "スナップショットに使用できるカメラがありません。");
			action("Character card saved: {0}", "角色卡已保存：{0}", "キャラカードを保存しました：{0}");
			action("Character card could not be saved: {0}", "无法保存角色卡：{0}", "キャラカードを保存できませんでした：{0}");
			action("Reference makeup imported.", "参考妆容已导入。", "参照メイクを読み込みました。");
			action("Reference head imported.", "参考头部已导入。", "参照頭部を読み込みました。");
			action("Reference body imported.", "参考身体已导入。", "参照体を読み込みました。");
			action("Reference clothes imported.", "参考服装已导入。", "参照衣装を読み込みました。");
			action("Reference accessories imported.", "参考饰品已导入。", "参照アクセサリーを読み込みました。");
			AddList(action, new string[49]
			{
				"Composed|沉着|おっとり", "Normal|普通|ノーマル", "Hardworking|勤奋|努力家", "Girlfriend|女友型|彼女系", "Fashionable|时尚|オシャレ", "Timid|胆小|内気", "Motherly|母性|母性的", "Sadistic|施虐型|ドS", "Open-Minded|开放|開放的", "Airhead|天然|天然",
				"Careful|谨慎|慎重", "Ideal Japanese|大和抚子|大和撫子", "Tomboy|假小子|ボーイッシュ", "Obsessed|痴迷|執着", "None|无|なし", "Clean Lover|爱干净|綺麗好き", "Lazy|懒惰|怠け者", "Fragile|脆弱|打たれ弱い", "Tough|坚强|タフ", "Weak Bladder|尿急|トイレが近い",
				"Patient|耐心|我慢強い", "Glass Heart|玻璃心|ガラスの心", "Brave|勇敢|勇敢", "Perverted|好色|変態", "Self-Control|自制|自制心", "At Will|随性|気まま", "Sensitive|敏感|敏感", "Curious|好奇|好奇心旺盛", "Affectionate|深情|愛情深い", "Lovestruck|恋爱中|恋煩い",
				"Awkward|笨拙|不器用", "Reluctant|勉强|消極的", "Loathing|厌恶|嫌悪", "Cooperative|配合|協力的", "Obedient|顺从|従順", "Submissive|服从|服従的", "Interested|感兴趣|興味あり", "Charmed|着迷|魅了", "Aroused|兴奋|興奮", "Horny|好色|発情",
				"Sadist|施虐者|サディスト", "Masochist|受虐者|マゾヒスト", "Sensitive Breasts|乳房敏感|胸が敏感", "Sensitive Ass|臀部敏感|お尻が敏感", "Sensitive Pussy|阴部敏感|局部が敏感", "Love Kisses|喜欢接吻|キス好き", "Clean Freak|洁癖|潔癖症", "Sex Hater|讨厌性爱|セックス嫌い", "Lonely|寂寞|寂しがり"
			});
			AddList(action, new string[92]
			{
				"Face Type / Skin|脸型／皮肤|顔タイプ／肌", "Face Type|脸型|顔タイプ", "Face Skin Type|脸部皮肤类型|顔の肌タイプ", "Face Detail Power|脸部细节强度|顔ディテール強度", "Face Detail Type|脸部细节类型|顔ディテール種類", "Shape — Whole|整体形状|全体形状", "Shape â€” Whole|整体形状|全体形状", "Chin|下巴|顎", "Cheek|脸颊|頬", "Eyebrow Shape|眉形|眉形状",
				"Eyes Shape|眼型|目の形", "Nose|鼻子|鼻", "Mouth|嘴巴|口", "Ears|耳朵|耳", "Mole|痣|ほくろ", "Eye — Left|左眼|左目", "Eye — Right|右眼|右目", "Eye â€” Left|左眼|左目", "Eye â€” Right|右眼|右目", "Eye Settings / Highlights|眼睛设置／高光|目の設定／ハイライト",
				"Eyebrows|眉毛|眉", "Eyelashes|睫毛|まつげ", "Eye Shadow|眼影|アイシャドウ", "Cheek Makeup|腮红|チーク", "Lips|唇妆|リップ", "Face Paint 1|脸部彩绘1|フェイスペイント1", "Face Paint 2|脸部彩绘2|フェイスペイント2", "Face Overlay 1|脸部覆盖图1|顔オーバーレイ1", "Face Overlay 2|脸部覆盖图2|顔オーバーレイ2", "Beard|胡须|ひげ",
				"Breast|胸部|胸", "Upper Body|上半身|上半身", "Lower Body|下半身|下半身", "Arms|手臂|腕", "Legs|腿部|脚", "Skin|皮肤|肌", "Sunburn|晒痕|日焼け", "Nipples|乳头|乳首", "Underhair|阴毛|アンダーヘア", "Nails|指甲|爪",
				"Body Paint 1|身体彩绘1|ボディペイント1", "Body Paint 2|身体彩绘2|ボディペイント2", "Body Overlay 1|身体覆盖图1|体オーバーレイ1", "Body Overlay 2|身体覆盖图2|体オーバーレイ2", "Hair Settings|头发设置|髪の設定", "Back Hair|后发|後ろ髪", "Front Hair|前发|前髪", "Side Hair|侧发|横髪", "Option / Extension Hair|附加／接发|オプション／付け毛", "Type|类型|種類",
				"Colour|颜色|色", "Gloss|光泽|ツヤ", "Power|强度|強度", "Size|大小|サイズ", "Rotation|旋转|回転", "Position|位置|位置", "Offset|偏移|オフセット", "Height|身高|身長", "Head Size|头部大小|頭の大きさ", "Skin Type|皮肤类型|肌タイプ",
				"Detail Power|细节强度|ディテール強度", "Detail Type|细节类型|ディテール種類", "Skin Colour|肤色|肌の色", "Skin Gloss|皮肤光泽|肌のツヤ", "Skin Metallic|皮肤金属度|肌のメタリック", "Hair Type|发型|髪型", "Base Colour|基础颜色|ベース色", "Top Colour|顶部颜色|トップ色", "Under Colour|内层颜色|内側の色", "Specular|高光色|スペキュラー",
				"Metallic|金属度|メタリック", "Smoothness|平滑度|滑らかさ", "Accessory Colour|饰品颜色|アクセサリー色", "Mesh Type|网格类型|メッシュ種類", "Mesh Colour|网格颜色|メッシュ色", "Mesh|网格|メッシュ", "Mole Type|痣类型|ほくろ種類", "Mole Colour|痣颜色|ほくろ色", "Pupil Type|瞳孔类型|瞳種類", "Pupil Colour|瞳孔颜色|瞳色",
				"Pupil Emission|瞳孔发光|瞳の発光", "Black Type|黑眼类型|黒目種類", "Black Colour|黑眼颜色|黒目色", "White Colour|眼白颜色|白目色", "Highlight Type|高光类型|ハイライト種類", "Highlight Colour|高光颜色|ハイライト色", "Highlight Tilt|高光倾斜|ハイライト傾き", "Shadow Scale|阴影范围|影の範囲", "Overlay Actions|覆盖图操作|オーバーレイ操作", "Bundles|发束数据|髪束データ",
				"(empty)|（空）|（空）", "(loaded)|（已加载）|（読込済み）"
			});
			AddList(action, new string[147]
			{
				"Face Base W|脸部基础宽度|顔ベース幅", "Face Up Z|上脸前后|顔上部 前後", "Face Up Y|上脸高度|顔上部 高さ", "Face Low Z|下脸前后|顔下部 前後", "Face Low W|下脸宽度|顔下部 幅", "Chin W|下巴宽度|顎の幅", "Chin Y|下巴高度|顎の高さ", "Chin Z|下巴前后|顎の前後", "Chin Rotation|下巴旋转|顎の回転", "Chin Low Y|下巴下部高度|顎下部の高さ",
				"Chin Tip W|下巴尖宽度|顎先の幅", "Chin Tip Y|下巴尖高度|顎先の高さ", "Chin Tip Z|下巴尖前后|顎先の前後", "Cheek Low Y|下脸颊高度|下頬の高さ", "Cheek Low Z|下脸颊前后|下頬の前後", "Cheek Low W|下脸颊宽度|下頬の幅", "Cheek Up Y|上脸颊高度|上頬の高さ", "Cheek Up Z|上脸颊前后|上頬の前後", "Cheek Up W|上脸颊宽度|上頬の幅", "Eyebrow W|眉毛宽度|眉の幅",
				"Eyebrow H|眉毛高度|眉の高さ", "Eyebrow X|眉毛横向位置|眉の横位置", "Eyebrow Y|眉毛纵向位置|眉の縦位置", "Eyebrow Tilt|眉毛倾斜|眉の傾き", "Eye Y|眼睛高度|目の高さ", "Eye X|眼睛横向位置|目の横位置", "Eye Z|眼睛前后|目の前後", "Eye W|眼睛宽度|目の幅", "Eye H|眼睛高度尺寸|目の縦幅", "Eye Rotation Z|眼睛旋转 Z|目の回転 Z",
				"Eye Rotation Y|眼睛旋转 Y|目の回転 Y", "Eye Inner X|内眼角横向|目頭の横位置", "Eye Outer X|外眼角横向|目尻の横位置", "Eye Inner Y|内眼角纵向|目頭の縦位置", "Eye Outer Y|外眼角纵向|目尻の縦位置", "Eyelid Form 01|眼睑形状01|まぶた形状01", "Eyelid Form 02|眼睑形状02|まぶた形状02", "Nose All Y|整体鼻子高度|鼻全体の高さ", "Nose All Z|整体鼻子前后|鼻全体の前後", "Nose All Rotation X|整体鼻子旋转 X|鼻全体の回転 X",
				"Nose All W|整体鼻子宽度|鼻全体の幅", "Bridge H|鼻梁高度|鼻筋の高さ", "Bridge W|鼻梁宽度|鼻筋の幅", "Bridge Form|鼻梁形状|鼻筋の形", "Wing W|鼻翼宽度|小鼻の幅", "Wing Y|鼻翼高度|小鼻の高さ", "Wing Z|鼻翼前后|小鼻の前後", "Wing Rotation X|鼻翼旋转 X|小鼻の回転 X", "Wing Rotation Z|鼻翼旋转 Z|小鼻の回転 Z", "Nose H|鼻子高度|鼻の高さ",
				"Nose Rotation X|鼻子旋转 X|鼻の回転 X", "Nose Size|鼻子大小|鼻の大きさ", "Mouth Y|嘴巴高度|口の高さ", "Mouth W|嘴巴宽度|口の幅", "Mouth H|嘴巴纵向尺寸|口の縦幅", "Mouth Z|嘴巴前后|口の前後", "Mouth Up Form|上唇形状|上唇形状", "Mouth Low Form|下唇形状|下唇形状", "Mouth Corner Form|嘴角形状|口角形状", "Ear Size|耳朵大小|耳の大きさ",
				"Ear Rotation Y|耳朵旋转 Y|耳の回転 Y", "Ear Rotation Z|耳朵旋转 Z|耳の回転 Z", "Ear Up Form|耳朵上部形状|耳上部形状", "Ear Low Form|耳朵下部形状|耳下部形状", "Mole W|痣宽度|ほくろ幅", "Mole H|痣高度|ほくろ高さ", "Mole X|痣横向位置|ほくろ横位置", "Mole Y|痣纵向位置|ほくろ縦位置", "Pupil W|瞳孔宽度|瞳の幅", "Pupil H|瞳孔高度|瞳の高さ",
				"Black W|黑眼宽度|黒目の幅", "Black H|黑眼高度|黒目の高さ", "Highlight W|高光宽度|ハイライト幅", "Highlight H|高光高度|ハイライト高さ", "Highlight X|高光横向位置|ハイライト横位置", "Highlight Y|高光纵向位置|ハイライト縦位置", "Eyebrows Type|眉毛类型|眉の種類", "Eyebrows Colour|眉毛颜色|眉の色", "Eyelashes Type|睫毛类型|まつげ種類", "Eyelashes Colour|睫毛颜色|まつげ色",
				"Eye Shadow Type|眼影类型|アイシャドウ種類", "Eye Shadow Colour|眼影颜色|アイシャドウ色", "Eye Shadow Gloss|眼影光泽|アイシャドウのツヤ", "Cheek Makeup Type|腮红类型|チーク種類", "Cheek Makeup Colour|腮红颜色|チーク色", "Cheek Makeup Gloss|腮红光泽|チークのツヤ", "Lips Type|唇妆类型|リップ種類", "Lips Colour|唇妆颜色|リップ色", "Lips Gloss|唇妆光泽|リップのツヤ", "Beard Type|胡须类型|ひげ種類",
				"Beard Colour|胡须颜色|ひげ色", "Paint Type|彩绘类型|ペイント種類", "Paint Colour|彩绘颜色|ペイント色", "Paint Gloss|彩绘光泽|ペイントのツヤ", "Paint Metallic|彩绘金属度|ペイントのメタリック", "Paint Layout|彩绘布局|ペイント配置", "Paint W|彩绘宽度|ペイント幅", "Paint H|彩绘高度|ペイント高さ", "Paint X|彩绘横向位置|ペイント横位置", "Paint Y|彩绘纵向位置|ペイント縦位置",
				"Paint Rotation|彩绘旋转|ペイント回転", "Bust Size|胸部大小|バストサイズ", "Bust Y|胸部高度|バスト高さ", "Bust Rotation Y|胸部旋转 Y|バスト回転 Y", "Bust X|胸部横向|バスト横位置", "Bust Rotation X|胸部旋转 X|バスト回転 X", "Bust Sharp|胸部尖锐度|バスト尖り", "Areola Bulge|乳晕凸起|乳輪の膨らみ", "Nip Weight|乳头重量|乳首の重み", "Nip Stand|乳头挺立|乳首の立ち",
				"Bust Softness|胸部柔软度|バスト柔らかさ", "Bust Weight|胸部重量|バスト重量", "Areola Size|乳晕大小|乳輪サイズ", "Neck W|颈部宽度|首の幅", "Neck Z|颈部前后|首の前後", "Shoulder W|肩部宽度|肩幅", "Shoulder Z|肩部前后|肩の前後", "Upper Body W|上半身宽度|上半身の幅", "Upper Body Z|上半身前后|上半身の前後", "Lower Upper-Body W|上半身下部宽度|上半身下部の幅",
				"Lower Upper-Body Z|上半身下部前后|上半身下部の前後", "Waist Y|腰部高度|ウエスト高さ", "Upper Waist W|上腰宽度|上ウエスト幅", "Upper Waist Z|上腰前后|上ウエスト前後", "Lower Waist W|下腰宽度|下ウエスト幅", "Lower Waist Z|下腰前后|下ウエスト前後", "Hip|臀围|ヒップ", "Hip Rotation X|臀部旋转 X|ヒップ回転 X", "Arm Upper|上臂|上腕", "Arm Lower|下臂|前腕",
				"Thigh Upper|大腿上部|太もも上部", "Thigh Lower|大腿下部|太もも下部", "Calf|小腿|ふくらはぎ", "Ankle|脚踝|足首", "Sunburn Type|晒痕类型|日焼け種類", "Sunburn Colour|晒痕颜色|日焼け色", "Nipple Type|乳头类型|乳首種類", "Nipple Colour|乳头颜色|乳首色", "Nipple Gloss|乳头光泽|乳首のツヤ", "Underhair Type|阴毛类型|アンダーヘア種類",
				"Underhair Colour|阴毛颜色|アンダーヘア色", "Nail Colour|指甲颜色|爪の色", "Nail Gloss|指甲光泽|爪のツヤ", "Colour Auto Setting|自动颜色设置|色の自動設定", "Colour Same Setting|相同颜色设置|同色設定", "Same Eye Setting|相同眼睛设置|両目同一設定", "Pupil Y|瞳孔高度|瞳の高さ"
			});
			action("ABMX", "ABMX", "ABMX");
			action("ABMX (install ABMX to edit these bones)", "ABMX（安装 ABMX 后可编辑这些骨骼）", "ABMX（これらのボーン編集にはABMXが必要です）");
			AddList(action, new string[77]
			{
				"Both|两侧|両方", "Left|左|左", "Right|右|右", "All|全部|すべて", "Thumb|拇指|親指", "Index|食指|人差し指", "Middle|中指|中指", "Ring|无名指|薬指", "Little|小指|小指", "Base|根部|根元",
				"Center|中部|中央", "Tip|末端|先端", "Scale X|缩放 X|スケール X", "Scale Y|缩放 Y|スケール Y", "Scale Z|缩放 Z|スケール Z", "Offset X|偏移 X|オフセット X", "Offset Y|偏移 Y|オフセット Y", "Offset Z|偏移 Z|オフセット Z", "Length|长度|長さ", "Protrusion|突出|突出",
				"Shape|形状|形状", "Head|头部|頭", "Head + Neck|头部＋颈部|頭＋首", "Lower Head Cheek|下脸颊|下頬", "Upper Head|上头部|上頭部", "Jaw|下颌|顎", "Chin Tip|下巴尖|顎先", "Upper Cheek|上脸颊|上頬", "Lower Cheek|下脸颊|下頬", "Eyebrow|眉毛|眉",
				"Inner Eyebrow|眉头|眉頭", "Outer Eyebrow|眉尾|眉尻", "Eye|眼睛|目", "Upper Front Head|前额上部|前頭部上", "Inner Eye|内眼角|目頭", "Eyeball|眼球|眼球", "Outer Eye|外眼角|目尻", "Nose + Bridge|鼻子＋鼻梁|鼻＋鼻筋", "Nose Tip|鼻尖|鼻先", "Nose Bridge|鼻梁|鼻筋",
				"Lip|嘴唇|唇", "Mouth Side|嘴角|口角", "Upper Lip|上唇|上唇", "Lower Lip|下唇|下唇", "Ear|耳朵|耳", "Upper Ear|耳朵上部|耳上部", "Lower Ear|耳朵下部|耳下部", "Hair Root|发根|髪の根元", "Breast 1|胸部1|胸1", "Breast 2|胸部2|胸2",
				"Breast 3|胸部3|胸3", "Breast Tip|乳房尖端|胸先", "Areola|乳晕|乳輪", "Nipple|乳头|乳首", "Nipple Tip|乳头尖端|乳首先端", "Breast Collision|胸部碰撞|胸コリジョン", "Neck|颈部|首", "Shoulder|肩部|肩", "Upper Torso|躯干上部|上胴", "Middle Torso|躯干中部|中胴",
				"Lower Torso|躯干下部|下胴", "Ass|臀部|尻", "Ass Collision|臀部碰撞|尻コリジョン", "Pelvis|骨盆|骨盤", "Hips|髋部|腰", "Pelvis & Legs|骨盆与腿|骨盤と脚", "Hips & Legs|髋部与腿|腰と脚", "Genital Area|生殖区域|股間", "Anus|肛门|肛門", "Whole Arm|整条手臂|腕全体",
				"Finger|手指|指", "Whole Leg|整条腿|脚全体", "Kneecap|膝盖骨|膝頭", "Kneecap Back|膝后|膝裏", "Foot 1|脚部1|足1", "Foot 2|脚部2|足2", "Foot Toes|脚趾|つま先"
			});
			AddList(action, new string[16]
			{
				"Outer Upper Thigh|大腿外侧上部|太もも外側上部", "Upper Thigh|大腿上部|太もも上部", "Center Thigh|大腿中部|太もも中央", "Lower Thigh|大腿下部|太もも下部", "Upper Calf|小腿上部|ふくらはぎ上部", "Center Calf|小腿中部|ふくらはぎ中央", "Lower Calf|小腿下部|ふくらはぎ下部", "Upper Arm Deltoids|上臂三角肌|上腕三角筋", "Upper Arm Triceps|上臂三头肌|上腕三頭筋", "Upper Arm Lower|上臂下部|上腕下部",
				"Elbow|肘部|肘", "Elbow Cap|肘尖|肘頭", "Forearm Upper|前臂上部|前腕上部", "Forearm Lower|前臂下部|前腕下部", "Wrist|手腕|手首", "Hand|手|手"
			});
			action("F604 Morphs", "F604 变形", "F604 モーフ");
			action("Update Make-Up Plus to display all morphs on this character.", "请更新 Make-Up Plus，以显示此角色的全部变形。", "このキャラクターのすべてのモーフを表示するには、Make-Up Plusを更新してください。");
			action("Installed Make-Up Plus: {0}", "已安装的 Make-Up Plus：{0}", "インストール済みのMake-Up Plus：{0}");
			action("Vanity Studio PLUS morphs present", "此页面含有 Vanity Studio PLUS 变形", "このページにはVanity Studio PLUSのモーフが含まれています");
			action("Update Make-Up Free to display all morphs on this character.", "请更新 Make-Up 免费版，以显示此角色的全部变形。", "このキャラクターのすべてのモーフを表示するには、Make-Up無料版を更新してください。");
			action("Installed morph support: {0}", "已安装的变形支持版本：{0}", "インストール済みのモーフ対応バージョン：{0}");
			action("Unavailable active morph count: {0}", "无法显示的已启用变形数量：{0}", "表示できない有効なモーフの数：{0}");
			action("Required morph version: {0}", "所需变形版本：{0}", "必要なモーフのバージョン：{0}");
			action("Unavailable active morphs: {0}", "无法显示的已启用变形：{0}", "表示できない使用中のモーフ：{0}");
			action("Their saved values are preserved when you save this character.", "保存此角色时，这些变形的数值会被保留。", "このキャラクターを保存しても、これらのモーフの値は保持されます。");
			action("Cheek Pout", "鼓腮", "頬を膨らませる");
			action("Cheek Gaunt", "脸颊凹陷", "こけた頬");
			action("Teeth Height Adjust", "牙齿高度调整", "歯の高さ調整");
			action("Tongue Height Adjust", "舌头高度调整", "舌の高さ調整");
			action("Jawline Square", "方形下颌", "四角い顎");
			action("Jawline Cleft", "下巴凹沟", "割れ顎");
			action("Eye Bags", "眼袋", "目の下のふくらみ");
			action("Nose Bridge Bump", "鼻梁隆起", "鼻筋の隆起");
			action("Nose Tip Style", "鼻尖风格", "鼻先スタイル");
			action("Nostril Definition", "鼻翼塑形", "小鼻の輪郭");
			action("Western Lips", "西式唇形", "西洋風の唇");
			action("Stylized Face 1", "风格化脸型1", "スタイライズ顔1");
			AddList(action, new string[24]
			{
				"Almond Face 1|杏仁脸1|アーモンド顔1", "Almond Face 2|杏仁脸2|アーモンド顔2", "Cranium Reduction|颅顶缩小|頭蓋縮小", "Forehead Slope|额头倾斜|額の傾斜", "Jawline Definition|下颌线塑形|フェイスライン強調", "Silhouette Contouring|轮廓塑形|輪郭補正", "Stylized Face|风格化脸型|スタイライズ顔", "Cheek Rebalancing|脸颊平衡|頬バランス", "Cheekbone Contouring|颧骨塑形|頬骨補正", "Defined Brow|眉骨塑形|眉骨強調",
				"Innocent Brow|无辜眉|イノセント眉", "Dolly Eyes 1|娃娃眼1|ドーリーアイ1", "Dolly Eyes 2|娃娃眼2|ドーリーアイ2", "Lynx Eyes|山猫眼|リンクスアイ", "Chiseled Nose Bridge|立体鼻梁|彫り深い鼻筋", "Flatten Nose Bridge|压平鼻梁|鼻筋を平らに", "Foxy Nose Bridge|狐系鼻梁|フォクシー鼻筋", "Nose Tip Stylize|鼻尖风格化|鼻先スタイル", "Tip Definition|鼻尖塑形|鼻先強調", "Western Nose 1|西式鼻型1|西洋鼻1",
				"Western Nose 2|西式鼻型2|西洋鼻2", "Plump Lower Lip|丰润下唇|ふっくら下唇", "Plump Upper Lip|丰润上唇|ふっくら上唇", "Reangle Lips|唇角调整|唇角調整"
			});
			action("Waiting for the head meshes.", "正在等待头部网格。", "頭部メッシュを待機中です。");
			action("The embedded F604 morph data could not be loaded.", "无法加载内置的 F604 变形数据。", "内蔵F604モーフデータを読み込めませんでした。");
			action("The morph controller is not attached to this Studio character.", "此工作室角色未附加变形控制器。", "このスタジオキャラクターにはモーフコントローラーがありません。");
			action("The head identity matched, but its live face meshes differ from the F604 pack.", "头部身份匹配，但当前脸部网格与 F604 包不同。", "頭部IDは一致しましたが、現在の顔メッシュがF604パックと異なります。");
			action("The head identity matched, but its live face meshes differ from the F604 pack. {0}", "头部身份匹配，但当前脸部网格与 F604 包不同。{0}", "頭部IDは一致しましたが、現在の顔メッシュがF604パックと異なります。{0}");
			action("The head identity was not recognized by the F604 packs. {0}", "F604 包无法识别此头部。{0}", "F604パックでこの頭部を認識できません。{0}");
			action("Ready for {0}.", "已为 {0} 准备就绪。", "{0} の準備ができました。");
			action("The compatible face meshes could not be prepared: {0}", "无法准备兼容的脸部网格：{0}", "互換顔メッシュを準備できませんでした：{0}");
			action("Select a character to use Character Clean-Up.", "请选择一个角色以使用角色清理。", "キャラクター・クリーンアップを使用するキャラクターを選択してください。");
			action("No removable mod appearance choices were found in this section.", "此部分未找到可移除的模组外观项目。", "このセクションには削除可能なMOD外観項目がありません。");
			action("No removable mod items remain in this category.", "此类别中没有剩余的可移除模组项目。", "このカテゴリに削除可能なMOD項目は残っていません。");
			action("Pending quarantine archives: {0}", "待隔离压缩包：{0}", "隔離待ちアーカイブ：{0}");
			action("Source zipmod:  {0}", "来源 zipmod：  {0}", "元zipmod：  {0}");
			action("Folder:  {0}", "文件夹：  {0}", "フォルダー：  {0}");
			action("Also in this zipmod (removed together):", "此 zipmod 中还包含（将一并移除）：", "このzipmodに含まれる項目（一緒に削除）：");
			action("+ {0} items without catalog thumbnails", "+ {0} 个无目录缩略图的项目", "+ カタログ画像なし {0} 項目");
			action("+ {0} Studio items", "+ {0} 个工作室项目", "+ スタジオ項目 {0} 個");
			action("Already terminated - awaiting quarantine on next start.", "已终止——将在下次启动时隔离。", "終了済み—次回起動時の隔離待ちです。");
			action("TERMINATE ZIPMOD", "终止 ZIPMOD", "ZIPMODを終了");
			action("Terminating in {0}s - press again to cancel", "将在 {0} 秒后终止——再次按下可取消", "{0}秒後に終了—もう一度押すとキャンセル");
			action("Termination cancelled.", "已取消终止。", "終了をキャンセルしました。");
			action("Termination could not be queued; no archive was moved.", "无法加入终止队列；未移动任何压缩包。", "終了を予約できませんでした。アーカイブは移動していません。");
			action("Terminated \"{0}\".", "已终止“{0}”。", "「{0}」を終了しました。");
			action("Its items are hidden from the character selectors, and the archive will move to Cleanup_Quarantine on the next game start. Nothing is deleted automatically.", "其项目已从角色选择器中隐藏，压缩包将在下次启动游戏时移至 Cleanup_Quarantine。不会自动删除任何内容。", "項目はキャラクター選択欄から非表示になり、アーカイブは次回ゲーム起動時にCleanup_Quarantineへ移動します。自動削除は行いません。");
			action("Undo Termination", "撤销终止", "終了を取り消す");
			action("Continue Cleaning", "继续清理", "清掃を続ける");
			action("Only a termination queued during this Studio session can be undone here.", "这里只能撤销本次工作室会话中排队的终止操作。", "ここでは、このスタジオセッション中に予約した終了だけを取り消せます。");
			action("Termination undone.", "已撤销终止。", "終了を取り消しました。");
			action("Preview could not be applied: {0}", "无法应用预览：{0}", "プレビューを適用できませんでした：{0}");
			action("Clean-Up action failed: {0}", "清理操作失败：{0}", "クリーンアップ操作に失敗しました：{0}");
			action("Outfit", "服装", "衣装");
			action("Character", "角色", "キャラクター");
			return map;
		}

		private static void AddList(Action<string, string, string> add, IEnumerable<string> rows)
		{
			foreach (string row in rows)
			{
				string[] array = row.Split('|');
				if (array.Length == 3)
				{
					add(array[0], array[1], array[2]);
				}
			}
		}
	}
	internal static class MakeUpUiScale
	{
		private static float scale = 1f;

		private static float logicalScreenWidth;

		private static float logicalScreenHeight;

		internal static float Scale => scale;

		internal static float LogicalScreenWidth
		{
			get
			{
				if (!(logicalScreenWidth > 0f))
				{
					return (float)Screen.width / scale;
				}
				return logicalScreenWidth;
			}
		}

		internal static float LogicalScreenHeight
		{
			get
			{
				if (!(logicalScreenHeight > 0f))
				{
					return (float)Screen.height / scale;
				}
				return logicalScreenHeight;
			}
		}

		internal static void SetHostMetrics(float value, float logicalWidth, float logicalHeight)
		{
			scale = ((value > 0f) ? value : 1f);
			logicalScreenWidth = ((logicalWidth > 0f) ? logicalWidth : ((float)Screen.width / scale));
			logicalScreenHeight = ((logicalHeight > 0f) ? logicalHeight : ((float)Screen.height / scale));
		}

		internal static Vector2 ScreenMousePosition()
		{
			return new Vector2(Input.mousePosition.x / scale, ((float)Screen.height - Input.mousePosition.y) / scale);
		}

		internal static Matrix4x4 BeginGuiScale()
		{
			Matrix4x4 matrix = GUI.matrix;
			GUI.matrix = matrix * Matrix4x4.Scale(new Vector3(scale, scale, 1f));
			return matrix;
		}
	}
	internal class MakeUpSelectorOption
	{
		internal int Id { get; private set; }

		internal string Name { get; private set; }

		internal string ThumbnailAssetBundle { get; private set; }

		internal string ThumbnailAssetName { get; private set; }

		internal object NativeInfo { get; private set; }

		internal string PreferenceIdentity { get; set; }

		internal MakeUpSelectorOption(int id, string name, string thumbnailAssetBundle, string thumbnailAssetName, object nativeInfo)
		{
			Id = id;
			Name = name ?? string.Empty;
			ThumbnailAssetBundle = thumbnailAssetBundle ?? string.Empty;
			ThumbnailAssetName = thumbnailAssetName ?? string.Empty;
			NativeInfo = nativeInfo;
		}
	}
	internal interface IMakeUpCleanupCatalogAdapter : ICharacterCleanupCatalogAdapter
	{
		void SetSession(MakeUpSession session);

		IList<MakeUpSelectorOption> OptionsFor(string categoryId);

		int CurrentItemId(string categoryId);

		bool TryFindOption(int itemId, out string categoryId, out MakeUpSelectorOption option);

		bool TryFindOption(int itemId, int? resolverCategory, out string categoryId, out MakeUpSelectorOption option);

		bool TryFindOption(int itemId, int? resolverCategory, out string categoryId, out int resolvedCategory, out MakeUpSelectorOption option);

		void MaintainPreviewVisibility(string categoryId);
	}
	internal static class MakeUpCleanupThumbnail
	{
		private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

		private static readonly HashSet<string> Failed = new HashSet<string>();

		private static int frame = -1;

		private static int loads;

		internal static Texture2D Load(MakeUpSelectorOption option)
		{
			if (option == null)
			{
				return null;
			}
			string text = option.ThumbnailAssetBundle + "+" + option.ThumbnailAssetName;
			if (Cache.TryGetValue(text, out var value))
			{
				return value;
			}
			if (frame != Time.frameCount)
			{
				frame = Time.frameCount;
				loads = 0;
			}
			if (loads >= 4)
			{
				return null;
			}
			loads++;
			try
			{
				Texture2D texture2D = ((string.IsNullOrEmpty(option.ThumbnailAssetBundle) || string.IsNullOrEmpty(option.ThumbnailAssetName)) ? Texture2D.blackTexture : CommonLib.LoadAsset<Texture2D>(option.ThumbnailAssetBundle, option.ThumbnailAssetName, clone: false, string.Empty));
				if (texture2D == null)
				{
					texture2D = Texture2D.blackTexture;
				}
				Cache[text] = texture2D;
				return texture2D;
			}
			catch (Exception ex)
			{
				Cache[text] = Texture2D.blackTexture;
				if (Failed.Add(text))
				{
					MakeUpPlugin.LogWarning("Thumbnail load failed for " + option.Name + ": " + ex.Message);
				}
				return Texture2D.blackTexture;
			}
		}
	}
	internal static class MakeUpPalette
	{
		internal static void Open(string title, Color initial, bool alpha, Action<Color> changed)
		{
			title = MakeUpLang.T(title);
			try
			{
				if (StudioAPI.InsideStudio)
				{
					global::Studio.Studio instance = Singleton<global::Studio.Studio>.Instance;
					if (!(instance == null) && !(instance.colorPalette == null))
					{
						instance.colorPalette.Setup(title, initial, changed, alpha);
						instance.colorPalette.visible = true;
					}
				}
			}
			catch (Exception ex)
			{
				MakeUpPlugin.LogWarning("Color palette could not be opened: " + ex.Message);
			}
		}

		internal static void ResetMaker()
		{
		}
	}
	internal sealed class MakeUpCardExpressionState
	{
		internal int EyesPattern { get; private set; }

		internal float EyesOpen { get; private set; }

		internal int MouthPattern { get; private set; }

		internal float MouthOpen { get; private set; }

		internal int EyebrowsPattern { get; private set; }

		internal float EyebrowsOpen { get; private set; }

		internal MakeUpCardExpressionState(int eyesPattern, float eyesOpen, int mouthPattern, float mouthOpen, int eyebrowsPattern, float eyebrowsOpen)
		{
			EyesPattern = eyesPattern;
			EyesOpen = eyesOpen;
			MouthPattern = mouthPattern;
			MouthOpen = mouthOpen;
			EyebrowsPattern = eyebrowsPattern;
			EyebrowsOpen = eyebrowsOpen;
		}
	}
	internal static class MakeUpCardExpressionPolicy
	{
		internal static MakeUpCardExpressionState NeutralForSave(MakeUpCardExpressionState live)
		{
			return new MakeUpCardExpressionState(0, 1f, 0, 0f, 0, 1f);
		}
	}
	internal static class MakeUpPoseEditorBinding
	{
		internal static Type FindControllerType(Assembly[] assemblies)
		{
			for (int i = 0; i < assemblies.Length; i++)
			{
				Type type = assemblies[i].GetType("HSPE.PoseController", throwOnError: false);
				if (type != null)
				{
					return type;
				}
			}
			return null;
		}
	}
	public sealed class ClothingBlendshapeController : CharaCustomFunctionController
	{
		private sealed class AppliedWeights
		{
			internal readonly Mesh Mesh;

			internal readonly HashSet<string> Names = new HashSet<string>(StringComparer.Ordinal);

			internal AppliedWeights(Mesh mesh)
			{
				Mesh = mesh;
			}
		}

		public const string DataId = "com.freelancer604.makeup.clothingblendshapes";

		private const string PayloadKey = "clothingWeights";

		private ClothingBlendshapeState state = new ClothingBlendshapeState();

		private int generation;

		private readonly Dictionary<int, int> slotGenerations = new Dictionary<int, int>();

		private int CoordinateIndex
		{
			get
			{
				if (!(((CharaCustomFunctionController)this).ChaControl == null) && ((CharaCustomFunctionController)this).ChaControl.fileStatus != null)
				{
					return ((CharaCustomFunctionController)this).ChaControl.fileStatus.coordinateType;
				}
				return 0;
			}
		}

		protected override void OnCardBeingSaved(GameMode currentGameMode)
		{
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Expected O, but got Unknown
			if (((CharaCustomFunctionController)this).ChaControl == null)
			{
				return;
			}
			try
			{
				state.CaptureCoordinate(CoordinateIndex, CaptureClothing());
				PluginData val = new PluginData
				{
					version = 3
				};
				val.data["clothingWeights"] = state.ToBytes();
				((CharaCustomFunctionController)this).SetExtendedData(val);
			}
			catch (Exception ex)
			{
				MakeUpPlugin.LogWarning("Clothing blendshape card capture failed: " + ex.Message);
			}
		}

		protected override void OnReload(GameMode currentGameMode, bool maintainState)
		{
			generation++;
			if (!maintainState)
			{
				state = new ClothingBlendshapeState();
				try
				{
					PluginData extendedData = ((CharaCustomFunctionController)this).GetExtendedData();
					if (extendedData != null && extendedData.data != null && extendedData.data.TryGetValue("clothingWeights", out var value))
					{
						state = ClothingBlendshapeState.FromBytes(value as byte[]);
					}
				}
				catch (Exception ex)
				{
					MakeUpPlugin.LogWarning("Clothing blendshape card data could not be read: " + ex.Message);
				}
			}
			BeginRestore();
		}

		protected override void OnCoordinateBeingLoaded(ChaFileCoordinate coordinate, bool maintainState)
		{
			generation++;
			if (!maintainState)
			{
				RestoreCoordinateGeometry(coordinate);
				ApplyCoordinateBytes(ReadCoordinateBytes(coordinate));
			}
			else
			{
				BeginRestore();
			}
		}

		private void RestoreCoordinateGeometry(ChaFileCoordinate coordinate)
		{
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			for (int i = 0; i < assemblies.Length; i++)
			{
				Type type = assemblies[i].GetType("Freelancer604.Wardrobe.WardrobeSession", throwOnError: false);
				if (type == null)
				{
					continue;
				}
				MethodInfo method = type.GetMethod("RestoreCoordinateGeometry", BindingFlags.Static | BindingFlags.Public, null, new Type[2]
				{
					typeof(ChaControl),
					typeof(ChaFileCoordinate)
				}, null);
				if (!(method == null))
				{
					try
					{
						method.Invoke(null, new object[2]
						{
							((CharaCustomFunctionController)this).ChaControl,
							coordinate
						});
						break;
					}
					catch (Exception ex)
					{
						MakeUpPlugin.LogWarning("Clothing coordinate geometry could not be restored: " + ex.Message);
						break;
					}
				}
			}
		}

		protected override void OnCoordinateBeingSaved(ChaFileCoordinate coordinate)
		{
			WriteCoordinateBytes(coordinate, CaptureCoordinateBytes());
		}

		public byte[] CaptureCoordinateBytes()
		{
			if (((CharaCustomFunctionController)this).ChaControl == null)
			{
				return null;
			}
			state.CaptureCoordinate(CoordinateIndex, CaptureClothing());
			return state.ExportCoordinate(CoordinateIndex);
		}

		public void ApplyCoordinateBytes(byte[] bytes, int slot = -1)
		{
			if (slot < 0)
			{
				generation++;
			}
			else
			{
				slotGenerations[slot] = SlotGeneration(slot) + 1;
			}
			state.ReplaceCoordinate(CoordinateIndex, null, slot);
			try
			{
				state.ReplaceCoordinate(CoordinateIndex, bytes, slot);
			}
			catch (Exception ex)
			{
				MakeUpPlugin.LogWarning("Clothing coordinate data could not be read: " + ex.Message);
			}
			BeginRestore(fromCard: false, slot);
		}

		public static byte[] ExtractCharacterCoordinateBytes(PluginData data, int index)
		{
			if (data == null || data.data == null || !data.data.TryGetValue("clothingWeights", out var value))
			{
				return null;
			}
			return ClothingBlendshapeState.FromBytes(value as byte[]).ExportCoordinate(index);
		}

		public static byte[] ReadCoordinateBytes(ChaFileCoordinate coordinate)
		{
			if (coordinate == null)
			{
				return null;
			}
			PluginData extendedDataById = ExtendedSave.GetExtendedDataById(coordinate, "com.freelancer604.makeup.clothingblendshapes");
			if (extendedDataById == null || extendedDataById.data == null || !extendedDataById.data.TryGetValue("clothingWeights", out var value))
			{
				return null;
			}
			return value as byte[];
		}

		public static void WriteCoordinateBytes(ChaFileCoordinate coordinate, byte[] bytes)
		{
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Expected O, but got Unknown
			if (coordinate != null)
			{
				PluginData val = null;
				if (bytes != null)
				{
					val = new PluginData
					{
						version = 3
					};
					val.data["clothingWeights"] = bytes;
				}
				ExtendedSave.SetExtendedDataById(coordinate, "com.freelancer604.makeup.clothingblendshapes", val);
			}
		}

		protected override void OnDestroy()
		{
			generation++;
			((CharaCustomFunctionController)this).OnDestroy();
		}

		private List<ClothingMeshSample> CaptureClothing()
		{
			List<ClothingMeshSample> list = new List<ClothingMeshSample>();
			if (((CharaCustomFunctionController)this).ChaControl.objClothes == null)
			{
				return list;
			}
			for (int i = 0; i < ((CharaCustomFunctionController)this).ChaControl.objClothes.Length; i++)
			{
				GameObject gameObject = ((CharaCustomFunctionController)this).ChaControl.objClothes[i];
				string text = GarmentIdentity(i);
				if (gameObject == null || text == null)
				{
					continue;
				}
				SkinnedMeshRenderer[] componentsInChildren = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
				foreach (SkinnedMeshRenderer skinnedMeshRenderer in componentsInChildren)
				{
					if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null && skinnedMeshRenderer.sharedMesh.blendShapeCount > 0)
					{
						list.Add(Sample(i, gameObject.transform, skinnedMeshRenderer, text));
					}
				}
			}
			return list;
		}

		private ClothingMeshSample Sample(int slot, Transform root, SkinnedMeshRenderer renderer, string identity)
		{
			ClothingMeshSample clothingMeshSample = new ClothingMeshSample
			{
				Slot = slot,
				Path = RelativePath(root, renderer.transform),
				SourcePath = RelativePath(((CharaCustomFunctionController)this).ChaControl.transform, renderer.transform),
				VertexCount = renderer.sharedMesh.vertexCount,
				GarmentIdentity = identity
			};
			for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
			{
				clothingMeshSample.Weights[renderer.sharedMesh.GetBlendShapeName(i)] = renderer.GetBlendShapeWeight(i);
			}
			return clothingMeshSample;
		}

		private static string RelativePath(Transform root, Transform target)
		{
			string text = string.Empty;
			Transform transform = target;
			while (transform != null && transform != root)
			{
				text = ((text.Length == 0) ? transform.name : (transform.name + "/" + text));
				transform = transform.parent;
			}
			return text;
		}

		private string GarmentIdentity(int slot)
		{
			if (((CharaCustomFunctionController)this).ChaControl == null || ((CharaCustomFunctionController)this).ChaControl.infoClothes == null || slot < 0 || slot >= ((CharaCustomFunctionController)this).ChaControl.infoClothes.Length || ((CharaCustomFunctionController)this).ChaControl.infoClothes[slot] == null)
			{
				return null;
			}
			ListInfoBase listInfoBase = ((CharaCustomFunctionController)this).ChaControl.infoClothes[slot];
			string info = listInfoBase.GetInfo(ChaListDefine.KeyType.MainAB);
			string info2 = listInfoBase.GetInfo(ChaListDefine.KeyType.MainData);
			if (string.IsNullOrEmpty(info) || string.IsNullOrEmpty(info2))
			{
				return null;
			}
			string empty = string.Empty;
			return listInfoBase.Category + ":" + info.Length + ":" + info + info2.Length + ":" + info2 + empty.Length + ":" + empty;
		}

		private int SlotGeneration(int slot)
		{
			if (!slotGenerations.TryGetValue(slot, out var value))
			{
				return 0;
			}
			return value;
		}

		private void BeginRestore(bool fromCard = true, int slot = -1)
		{
			int coordinateIndex = CoordinateIndex;
			List<ClothingMeshSample> list = new List<ClothingMeshSample>();
			foreach (ClothingMeshSample item in state.ForCoordinate(coordinateIndex))
			{
				if (slot < 0 || item.Slot == slot)
				{
					list.Add(item);
				}
			}
			int ticket = generation;
			if (((CharaCustomFunctionController)this).ChaControl == null || list.Count == 0)
			{
				return;
			}
			Dictionary<int, int> dictionary = new Dictionary<int, int>();
			Dictionary<int, int> dictionary2 = new Dictionary<int, int>();
			foreach (ClothingMeshSample item2 in list)
			{
				dictionary2[item2.Slot] = SlotGeneration(item2.Slot);
				if (TryGetClothingId(coordinateIndex, item2.Slot, fromCard, out var id))
				{
					dictionary[item2.Slot] = id;
				}
			}
			((MonoBehaviour)this).StartCoroutine(Restore(coordinateIndex, ticket, list, dictionary, dictionary2));
		}

		private bool TryGetClothingId(int coordinate, int slot, bool fromCard, out int id)
		{
			id = -1;
			if (((CharaCustomFunctionController)this).ChaControl == null)
			{
				return false;
			}
			ChaFileCoordinate chaFileCoordinate = ((CharaCustomFunctionController)this).ChaControl.nowCoordinate;
			if (fromCard && ((CharaCustomFunctionController)this).ChaControl.chaFile != null && ((CharaCustomFunctionController)this).ChaControl.chaFile.coordinate != null && coordinate >= 0 && coordinate < ((CharaCustomFunctionController)this).ChaControl.chaFile.coordinate.Length)
			{
				chaFileCoordinate = ((CharaCustomFunctionController)this).ChaControl.chaFile.coordinate[coordinate];
			}
			if (chaFileCoordinate == null || chaFileCoordinate.clothes == null || chaFileCoordinate.clothes.parts == null || slot < 0 || slot >= chaFileCoordinate.clothes.parts.Length)
			{
				return false;
			}
			id = chaFileCoordinate.clothes.parts[slot].id;
			return true;
		}

		private IEnumerator Restore(int coordinate, int ticket, IList<ClothingMeshSample> saved, Dictionary<int, int> identities, Dictionary<int, int> slotTickets)
		{
			Dictionary<int, GameObject> roots = new Dictionary<int, GameObject>();
			Dictionary<SkinnedMeshRenderer, AppliedWeights> applied = new Dictionary<SkinnedMeshRenderer, AppliedWeights>();
			for (int attempt = 0; attempt < 20; attempt++)
			{
				yield return new WaitForSeconds(0.25f);
				if (ticket != generation || ((CharaCustomFunctionController)this).ChaControl == null || coordinate != CoordinateIndex)
				{
					break;
				}
				foreach (ClothingMeshSample item in saved)
				{
					if (slotTickets[item.Slot] != SlotGeneration(item.Slot) || !identities.TryGetValue(item.Slot, out var value) || !TryGetClothingId(coordinate, item.Slot, fromCard: false, out var id) || id != value || ((CharaCustomFunctionController)this).ChaControl.objClothes == null || item.Slot >= ((CharaCustomFunctionController)this).ChaControl.objClothes.Length)
					{
						continue;
					}
					GameObject gameObject = ((CharaCustomFunctionController)this).ChaControl.objClothes[item.Slot];
					if (gameObject == null || ((CharaCustomFunctionController)this).ChaControl.infoClothes == null || item.Slot >= ((CharaCustomFunctionController)this).ChaControl.infoClothes.Length || ((CharaCustomFunctionController)this).ChaControl.infoClothes[item.Slot] == null || ((CharaCustomFunctionController)this).ChaControl.infoClothes[item.Slot].Id != value)
					{
						continue;
					}
					string text = GarmentIdentity(item.Slot);
					if (string.IsNullOrEmpty(item.GarmentIdentity) || text != item.GarmentIdentity || (roots.TryGetValue(item.Slot, out var value2) && value2 != gameObject))
					{
						continue;
					}
					Transform transform = (string.IsNullOrEmpty(item.Path) ? gameObject.transform : gameObject.transform.Find(item.Path));
					SkinnedMeshRenderer renderer = ((transform == null) ? null : transform.GetComponent<SkinnedMeshRenderer>());
					if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.vertexCount != item.VertexCount)
					{
						continue;
					}
					if (!applied.TryGetValue(renderer, out var previous) || previous.Mesh != renderer.sharedMesh)
					{
						applied[renderer] = (previous = new AppliedWeights(renderer.sharedMesh));
					}
					roots[item.Slot] = gameObject;
					ClothingBlendshapeState.Replay(item, Sample(item.Slot, gameObject.transform, renderer, text), delegate(string name, float weight)
					{
						if (!previous.Names.Contains(name))
						{
							int blendShapeIndex = renderer.sharedMesh.GetBlendShapeIndex(name);
							if (blendShapeIndex >= 0)
							{
								renderer.SetBlendShapeWeight(blendShapeIndex, weight);
								previous.Names.Add(name);
							}
						}
					});
				}
			}
		}
	}
	internal sealed class ClothingMeshSample
	{
		internal int Slot;

		internal string Path;

		internal int VertexCount;

		internal string GarmentIdentity;

		internal string SourcePath;

		internal readonly Dictionary<string, float> Weights = new Dictionary<string, float>(StringComparer.Ordinal);
	}
	internal sealed class ClothingBlendshapeState
	{
		private readonly Dictionary<int, List<ClothingMeshSample>> outfits = new Dictionary<int, List<ClothingMeshSample>>();

		internal void CaptureCoordinate(int coordinate, IList<ClothingMeshSample> samples)
		{
			List<ClothingMeshSample> list = new List<ClothingMeshSample>();
			foreach (ClothingMeshSample sample in samples)
			{
				ClothingMeshSample clothingMeshSample = new ClothingMeshSample
				{
					Slot = sample.Slot,
					Path = sample.Path,
					VertexCount = sample.VertexCount,
					GarmentIdentity = sample.GarmentIdentity,
					SourcePath = sample.SourcePath
				};
				foreach (KeyValuePair<string, float> weight in sample.Weights)
				{
					if (!float.IsNaN(weight.Value) && !float.IsInfinity(weight.Value))
					{
						clothingMeshSample.Weights[weight.Key] = weight.Value;
					}
				}
				if (clothingMeshSample.Weights.Count > 0)
				{
					list.Add(clothingMeshSample);
				}
			}
			outfits[coordinate] = list;
		}

		internal IList<ClothingMeshSample> ForCoordinate(int coordinate)
		{
			if (!outfits.TryGetValue(coordinate, out var value))
			{
				return new List<ClothingMeshSample>();
			}
			return value;
		}

		internal void RemoveCoordinate(int coordinate)
		{
			outfits.Remove(coordinate);
		}

		internal byte[] ExportCoordinate(int coordinate)
		{
			ClothingBlendshapeState clothingBlendshapeState = new ClothingBlendshapeState();
			clothingBlendshapeState.CaptureCoordinate(0, ForCoordinate(coordinate));
			return clothingBlendshapeState.ToBytes();
		}

		internal void ReplaceCoordinate(int coordinate, byte[] bytes, int slot)
		{
			IList<ClothingMeshSample> list = FromBytes(bytes).ForCoordinate(0);
			List<ClothingMeshSample> list2 = new List<ClothingMeshSample>();
			if (slot >= 0)
			{
				foreach (ClothingMeshSample item in ForCoordinate(coordinate))
				{
					if (item.Slot != slot)
					{
						list2.Add(item);
					}
				}
			}
			foreach (ClothingMeshSample item2 in list)
			{
				if (slot < 0 || item2.Slot == slot)
				{
					list2.Add(item2);
				}
			}
			CaptureCoordinate(coordinate, list2);
		}

		internal byte[] ToBytes()
		{
			using MemoryStream memoryStream = new MemoryStream();
			using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
			binaryWriter.Write(3);
			binaryWriter.Write(outfits.Count);
			foreach (KeyValuePair<int, List<ClothingMeshSample>> outfit in outfits)
			{
				binaryWriter.Write(outfit.Key);
				binaryWriter.Write(outfit.Value.Count);
				foreach (ClothingMeshSample item in outfit.Value)
				{
					binaryWriter.Write(item.Slot);
					binaryWriter.Write(item.Path ?? string.Empty);
					binaryWriter.Write(item.VertexCount);
					binaryWriter.Write(item.GarmentIdentity ?? string.Empty);
					binaryWriter.Write(item.SourcePath ?? string.Empty);
					binaryWriter.Write(item.Weights.Count);
					foreach (KeyValuePair<string, float> weight in item.Weights)
					{
						binaryWriter.Write(weight.Key);
						binaryWriter.Write(weight.Value);
					}
				}
			}
			return memoryStream.ToArray();
		}

		internal static ClothingBlendshapeState FromBytes(byte[] bytes)
		{
			ClothingBlendshapeState clothingBlendshapeState = new ClothingBlendshapeState();
			if (bytes == null)
			{
				return clothingBlendshapeState;
			}
			if (bytes.Length > 16777216)
			{
				throw new InvalidDataException("Clothing state is too large.");
			}
			using MemoryStream memoryStream = new MemoryStream(bytes, writable: false);
			using BinaryReader binaryReader = new BinaryReader(memoryStream);
			int num = binaryReader.ReadInt32();
			if (num != 2 && num != 3)
			{
				throw new InvalidDataException("Unsupported clothing state version.");
			}
			int num2 = ReadCount(binaryReader, 64);
			for (int i = 0; i < num2; i++)
			{
				int num3 = binaryReader.ReadInt32();
				if (num3 < 0 || num3 >= 64 || clothingBlendshapeState.outfits.ContainsKey(num3))
				{
					throw new InvalidDataException("Invalid clothing coordinate.");
				}
				List<ClothingMeshSample> list = new List<ClothingMeshSample>();
				int num4 = ReadCount(binaryReader, 4096);
				for (int j = 0; j < num4; j++)
				{
					ClothingMeshSample clothingMeshSample = new ClothingMeshSample
					{
						Slot = binaryReader.ReadInt32(),
						Path = binaryReader.ReadString(),
						VertexCount = binaryReader.ReadInt32()
					};
					clothingMeshSample.GarmentIdentity = binaryReader.ReadString();
					clothingMeshSample.SourcePath = ((num >= 3) ? binaryReader.ReadString() : null);
					if (clothingMeshSample.Slot < 0 || clothingMeshSample.Slot > 63 || clothingMeshSample.VertexCount < 0 || clothingMeshSample.Path.Length > 4096 || clothingMeshSample.GarmentIdentity.Length > 16384)
					{
						throw new InvalidDataException("Invalid clothing renderer.");
					}
					if (clothingMeshSample.SourcePath != null && clothingMeshSample.SourcePath.Length > 4096)
					{
						throw new InvalidDataException("Invalid clothing source renderer.");
					}
					int num5 = ReadCount(binaryReader, 16384);
					for (int k = 0; k < num5; k++)
					{
						string text = binaryReader.ReadString();
						float num6 = binaryReader.ReadSingle();
						if (text.Length > 4096 || float.IsNaN(num6) || float.IsInfinity(num6))
						{
							throw new InvalidDataException("Invalid clothing shape.");
						}
						clothingMeshSample.Weights[text] = num6;
					}
					list.Add(clothingMeshSample);
				}
				clothingBlendshapeState.outfits[num3] = list;
			}
			if (memoryStream.Position != memoryStream.Length)
			{
				throw new InvalidDataException("Unexpected clothing state data.");
			}
			return clothingBlendshapeState;
		}

		private static int ReadCount(BinaryReader reader, int maximum)
		{
			int num = reader.ReadInt32();
			if (num < 0 || num > maximum)
			{
				throw new InvalidDataException("Invalid clothing state count.");
			}
			return num;
		}

		internal static bool Replay(ClothingMeshSample saved, ClothingMeshSample live, Action<string, float> write)
		{
			if (saved.Slot != live.Slot || saved.Path != live.Path || saved.VertexCount != live.VertexCount || (saved.GarmentIdentity ?? string.Empty) != (live.GarmentIdentity ?? string.Empty))
			{
				return false;
			}
			bool result = true;
			foreach (KeyValuePair<string, float> weight in saved.Weights)
			{
				if (live.Weights.ContainsKey(weight.Key))
				{
					write(weight.Key, weight.Value);
				}
				else
				{
					result = false;
				}
			}
			return result;
		}
	}
	internal sealed class MakeUpBscSaveStage : IDisposable
	{
		private sealed class SavedWeight
		{
			internal object Shape { get; private set; }

			internal float Weight { get; private set; }

			internal SavedWeight(object shape, float weight)
			{
				Shape = shape;
				Weight = weight;
			}
		}

		private const string BscPluginId = "com.shallty.blendshapecreator";

		private readonly object controller;

		private readonly FieldInfo cardDataField;

		private readonly object previousCardData;

		private readonly FieldInfo weightField;

		private readonly ChaFileControl characterFile;

		private readonly PluginData previousExtendedData;

		private readonly ArrayList savedWeights = new ArrayList();

		private bool disposed;

		private MakeUpBscSaveStage(object controller, FieldInfo cardDataField, object previousCardData, FieldInfo weightField, ChaFileControl characterFile, PluginData previousExtendedData)
		{
			this.controller = controller;
			this.cardDataField = cardDataField;
			this.previousCardData = previousCardData;
			this.weightField = weightField;
			this.characterFile = characterFile;
			this.previousExtendedData = previousExtendedData;
		}

		internal static MakeUpBscSaveStage TryCreate(ChaControl character)
		{
			if (character == null || character.chaFile == null)
			{
				return null;
			}
			MakeUpBscSaveStage makeUpBscSaveStage = null;
			try
			{
				Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault((Assembly candidate) => candidate.GetName().Name.IndexOf("BlendshapeCreator", StringComparison.OrdinalIgnoreCase) >= 0);
				if (assembly == null)
				{
					return null;
				}
				Type type = assembly.GetType("BlendshapeCreator.CharacterController");
				Type type2 = assembly.GetType("BlendshapeCreator.BlendshapeCreator+OCIBlendShapeData");
				Type type3 = assembly.GetType("BlendshapeCreator.BlendshapeCreator+BlendShape");
				Type type4 = assembly.GetType("BlendshapeCreator.BlendshapeCreator");
				if (type == null || type2 == null || type3 == null || type4 == null)
				{
					return null;
				}
				FieldInfo field = type.GetField("CharaBlendShapesData", BindingFlags.Instance | BindingFlags.Public);
				FieldInfo field2 = type2.GetField("blendShapes", BindingFlags.Instance | BindingFlags.Public);
				FieldInfo field3 = type3.GetField("rendererPath", BindingFlags.Instance | BindingFlags.Public);
				FieldInfo field4 = type3.GetField("name", BindingFlags.Instance | BindingFlags.Public);
				FieldInfo field5 = type3.GetField("weight", BindingFlags.Instance | BindingFlags.Public);
				FieldInfo field6 = type4.GetField("ociBlendShapesData", BindingFlags.Static | BindingFlags.Public);
				if (field == null || field2 == null || field3 == null || field4 == null || field5 == null || field6 == null)
				{
					return null;
				}
				object component = character.gameObject.GetComponent(type);
				if (component == null)
				{
					return null;
				}
				object value = field.GetValue(component);
				IList list = ((value == null) ? null : (field2.GetValue(value) as IList));
				IList list2 = null;
				if (StudioAPI.InsideStudio)
				{
					OCIChar oCIChar = null;
					global::Studio.Studio instance = Singleton<global::Studio.Studio>.Instance;
					if (instance != null)
					{
						foreach (ObjectCtrlInfo value2 in instance.dicObjectCtrl.Values)
						{
							if (value2 is OCIChar oCIChar2 && oCIChar2.charInfo == character)
							{
								oCIChar = oCIChar2;
								break;
							}
						}
					}
					IDictionary dictionary = field6.GetValue(null) as IDictionary;
					if (oCIChar != null && dictionary != null && dictionary.Contains(oCIChar))
					{
						object obj = dictionary[oCIChar];
						list2 = ((obj == null) ? null : (field2.GetValue(obj) as IList));
					}
				}
				object obj2 = Activator.CreateInstance(type2);
				if (!(field2.GetValue(obj2) is IList list3))
				{
					return null;
				}
				if (list2 != null)
				{
					foreach (object item in list2)
					{
						string text = Convert.ToString(field3.GetValue(item));
						Transform target = (string.IsNullOrEmpty(text) ? null : character.transform.Find(text));
						if (IsClothing(character, target))
						{
							list3.Add(item);
						}
					}
				}
				if (list != null)
				{
					foreach (object item2 in list)
					{
						string text2 = Convert.ToString(field3.GetValue(item2));
						string text3 = Convert.ToString(field4.GetValue(item2));
						bool flag = false;
						foreach (object item3 in list3)
						{
							if (Convert.ToString(field3.GetValue(item3)) == text2 && Convert.ToString(field4.GetValue(item3)) == text3)
							{
								flag = true;
								break;
							}
						}
						if (!flag)
						{
							list3.Add(item2);
						}
					}
				}
				if (list3 == null || list3.Count == 0)
				{
					return null;
				}
				PluginData extendedDataById = ExtendedSave.GetExtendedDataById((ChaFile)character.chaFile, "com.shallty.blendshapecreator");
				makeUpBscSaveStage = new MakeUpBscSaveStage(component, field, value, field5, character.chaFile, extendedDataById);
				field.SetValue(component, obj2);
				makeUpBscSaveStage.NeutralizeForSave(character, list3, field3, field4);
				return makeUpBscSaveStage;
			}
			catch (Exception ex)
			{
				makeUpBscSaveStage?.Dispose();
				MakeUpPlugin.LogWarning("Blendshape Creator save staging was skipped: " + ex.Message);
				return null;
			}
		}

		private void NeutralizeForSave(ChaControl character, IList shapes, FieldInfo pathField, FieldInfo nameField)
		{
			foreach (object shape in shapes)
			{
				float num = Convert.ToSingle(weightField.GetValue(shape));
				savedWeights.Add(new SavedWeight(shape, num));
				string text = Convert.ToString(pathField.GetValue(shape));
				string text2 = Convert.ToString(nameField.GetValue(shape));
				int num2 = -1;
				float num3 = num;
				Transform transform = (string.IsNullOrEmpty(text) ? null : character.transform.Find(text));
				SkinnedMeshRenderer skinnedMeshRenderer = ((transform == null) ? null : transform.GetComponent<SkinnedMeshRenderer>());
				if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null && !string.IsNullOrEmpty(text2))
				{
					num2 = skinnedMeshRenderer.sharedMesh.GetBlendShapeIndex(text2);
					if (num2 >= 0)
					{
						num3 = skinnedMeshRenderer.GetBlendShapeWeight(num2);
					}
				}
				float num4 = (IsClothing(character, transform) ? num3 : num);
				weightField.SetValue(shape, num4);
			}
		}

		internal static bool IsClothing(ChaControl character, Transform target)
		{
			if (character == null || target == null || character.objClothes == null)
			{
				return false;
			}
			GameObject[] objClothes = character.objClothes;
			foreach (GameObject gameObject in objClothes)
			{
				if (gameObject != null && (target == gameObject.transform || target.IsChildOf(gameObject.transform)))
				{
					return true;
				}
			}
			return false;
		}

		public void Dispose()
		{
			if (disposed)
			{
				return;
			}
			disposed = true;
			try
			{
				foreach (SavedWeight savedWeight in savedWeights)
				{
					weightField.SetValue(savedWeight.Shape, savedWeight.Weight);
				}
			}
			catch (Exception ex)
			{
				MakeUpPlugin.LogWarning("Blendshape Creator weights could not be restored: " + ex.Message);
			}
			try
			{
				cardDataField.SetValue(controller, previousCardData);
			}
			catch (Exception ex2)
			{
				MakeUpPlugin.LogWarning("Blendshape Creator save staging could not be restored: " + ex2.Message);
			}
			try
			{
				ExtendedSave.SetExtendedDataById((ChaFile)characterFile, "com.shallty.blendshapecreator", previousExtendedData);
			}
			catch (Exception ex3)
			{
				MakeUpPlugin.LogWarning("Blendshape Creator in-memory card payload could not be restored: " + ex3.Message);
			}
		}
	}
	internal static class MakeUpPairScopeCore
	{
		internal const int Both = 0;

		internal const int Left = 1;

		internal const int Right = 2;

		internal static bool IsPairedSection(string sectionKey)
		{
			if (!string.Equals(sectionKey, "face.eye.left", StringComparison.Ordinal))
			{
				return string.Equals(sectionKey, "face.eye.right", StringComparison.Ordinal);
			}
			return true;
		}

		internal static IList<string> TargetSections(string sectionKey, int scope)
		{
			if (!IsPairedSection(sectionKey))
			{
				return new string[1] { sectionKey };
			}
			return scope switch
			{
				1 => new string[1] { "face.eye.left" }, 
				2 => new string[1] { "face.eye.right" }, 
				_ => new string[2] { "face.eye.left", "face.eye.right" }, 
			};
		}

		internal static string RouteKey(string sectionKey, string key, int scope)
		{
			if (!IsPairedSection(sectionKey))
			{
				return key;
			}
			string text = scope switch
			{
				1 => "left", 
				2 => "right", 
				_ => sectionKey.EndsWith(".right", StringComparison.Ordinal) ? "right" : "left", 
			};
			if (string.Equals(key, "face.eye.left", StringComparison.Ordinal) || string.Equals(key, "face.eye.right", StringComparison.Ordinal))
			{
				return "face.eye." + text;
			}
			if (string.Equals(key, "eye_l", StringComparison.Ordinal) || string.Equals(key, "eye_r", StringComparison.Ordinal))
			{
				if (!(text == "right"))
				{
					return "eye_l";
				}
				return "eye_r";
			}
			return key;
		}

		internal static string RouteColor(string colorKey, int scope)
		{
			if (!string.Equals(colorKey, "Left Eye", StringComparison.Ordinal) && !string.Equals(colorKey, "Right Eye", StringComparison.Ordinal))
			{
				return colorKey;
			}
			return scope switch
			{
				2 => "Right Eye", 
				1 => "Left Eye", 
				_ => colorKey, 
			};
		}

		internal static IList<string> TargetKeys(string sectionKey, string key, int scope)
		{
			IList<string> list = TargetSections(sectionKey, scope);
			List<string> list2 = new List<string>(list.Count);
			for (int i = 0; i < list.Count; i++)
			{
				int scope2 = ((!list[i].EndsWith(".right", StringComparison.Ordinal)) ? 1 : 2);
				list2.Add(RouteKey(sectionKey, key, scope2));
			}
			return list2.AsReadOnly();
		}

		internal static IList<string> TargetColors(string colorKey, int scope)
		{
			if (!string.Equals(colorKey, "Left Eye", StringComparison.Ordinal) && !string.Equals(colorKey, "Right Eye", StringComparison.Ordinal))
			{
				return new string[1] { colorKey };
			}
			return scope switch
			{
				1 => new string[1] { "Left Eye" }, 
				2 => new string[1] { "Right Eye" }, 
				_ => new string[2] { "Left Eye", "Right Eye" }, 
			};
		}
	}
	internal sealed class MakeUpActionState
	{
		internal string Id { get; private set; }

		internal bool Enabled { get; private set; }

		internal MakeUpActionState(string id, bool enabled)
		{
			Id = id;
			Enabled = enabled;
		}
	}
	internal static class MakeUpParityContractCore
	{
		internal const float ContentWidth = 480f;

		internal const float ScrollContentWidth = 452f;

		internal const float SelectorGap = 20f;

		internal const float HomeColumnWidth = 218f;

		internal const float HomeColumnGap = 12f;

		internal const float HomeActionWidth = 200f;

		internal const float HomePairActionWidth = 98f;

		internal const float PortraitWidth = 174f;

		internal const float PortraitHeight = 243f;

		internal const float BottomActionWidth = 86f;

		internal static float SelectorDetailsWidth(float contentWidth, float previewWidth)
		{
			return Math.Max(80f, contentWidth - previewWidth - 20f);
		}

		internal static IList<MakeUpActionState> ResolveHomeActions(bool hasCharacter, bool hasReference, bool insideStudio, bool hasSnapshot)
		{
			return Array.AsReadOnly(new MakeUpActionState[10]
			{
				new MakeUpActionState("LoadReference", hasReference && insideStudio),
				new MakeUpActionState("CopyAll", hasCharacter && hasReference),
				new MakeUpActionState("ImportHead", hasCharacter && hasReference),
				new MakeUpActionState("ImportBody", hasCharacter && hasReference),
				new MakeUpActionState("ImportMakeup", hasCharacter && hasReference),
				new MakeUpActionState("ImportClothes", hasCharacter && hasReference),
				new MakeUpActionState("ImportAccessories", hasCharacter && hasReference),
				new MakeUpActionState("Snapshot", hasCharacter),
				new MakeUpActionState("CopyReferencePortrait", hasReference),
				new MakeUpActionState("SaveCharacter", hasCharacter && hasSnapshot)
			});
		}

		internal static IList<MakeUpActionState> ResolveBottomActions(bool body, int undoCount, int redoCount, bool hasClipboard, bool hasInitial)
		{
			return Array.AsReadOnly(new MakeUpActionState[5]
			{
				new MakeUpActionState("Undo", undoCount > 0),
				new MakeUpActionState(body ? "CopyBody" : "CopyHead", enabled: true),
				new MakeUpActionState("Reset", hasInitial),
				new MakeUpActionState(body ? "PasteBody" : "PasteHead", hasClipboard),
				new MakeUpActionState("Redo", redoCount > 0)
			});
		}
	}
	internal static class MakeUpPoseEditorRefresh
	{
		internal static void Refresh(Component target)
		{
			Type type = MakeUpPoseEditorBinding.FindControllerType(AppDomain.CurrentDomain.GetAssemblies());
			if (type == null)
			{
				return;
			}
			if (target == null)
			{
				throw new InvalidOperationException("The Studio character root is unavailable for card saving.");
			}
			Component component = target.GetComponent(type);
			if (!(component == null))
			{
				FieldInfo field = type.GetField("_blendShapesEditor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (field == null)
				{
					throw new MissingFieldException(type.FullName, "_blendShapesEditor");
				}
				object value = field.GetValue(component);
				if (value == null)
				{
					throw new InvalidOperationException("Pose Editor has not finished initializing this character. Try saving again.");
				}
				MethodInfo method = value.GetType().GetMethod("RefreshSkinnedMeshRendererList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
				if (method == null)
				{
					throw new MissingMethodException(value.GetType().FullName, "RefreshSkinnedMeshRendererList");
				}
				method.Invoke(value, null);
			}
		}
	}
	internal static class SharedClothingSlotsHistory
	{
		private static Type ActiveProvider()
		{
			if (!Chainloader.PluginInfos.TryGetValue("com.freelancer604.wardrobe", out var value) || value == null || (UnityEngine.Object)(object)value.Instance == null)
			{
				return null;
			}
			return ((object)value.Instance).GetType().Assembly.GetType("Freelancer604.Wardrobe.WardrobeClothingSlotsKoi", throwOnError: false);
		}

		internal static byte[] Capture(object character)
		{
			Type type = ActiveProvider();
			if (type == null || character == null)
			{
				return null;
			}
			MethodInfo method = type.GetMethod("CaptureState", BindingFlags.Static | BindingFlags.Public);
			if (method == null)
			{
				return null;
			}
			return (byte[])Invoke(method, character, null, restore: false);
		}

		internal static void Restore(object character, byte[] snapshot)
		{
			if (snapshot != null)
			{
				Type type = ActiveProvider();
				MethodInfo obj = ((type == null) ? null : type.GetMethod("RestoreState", BindingFlags.Static | BindingFlags.Public));
				if (obj == null)
				{
					throw new InvalidOperationException("The clothing slot history provider is unavailable.");
				}
				Invoke(obj, character, snapshot, restore: true);
			}
		}

		private static object Invoke(MethodInfo method, object character, byte[] snapshot, bool restore)
		{
			try
			{
				return method.Invoke(null, (!restore) ? new object[1] { character } : new object[2] { character, snapshot });
			}
			catch (TargetInvocationException ex)
			{
				throw new InvalidOperationException("Numbered clothing history failed: " + ex.GetBaseException().Message, ex.InnerException ?? ex);
			}
		}
	}
	internal static class SharedMakeUpCardSnapshot
	{
		internal static byte[] Capture(ChaControl character)
		{
			character.AssignCoordinate((ChaFileDefine.CoordinateType)character.fileStatus.coordinateType);
			return Serialize(character.chaFile);
		}

		private static byte[] Serialize(ChaFileControl card)
		{
			using MemoryStream memoryStream = new MemoryStream();
			using BinaryWriter bw = new BinaryWriter(memoryStream);
			if (!card.SaveCharaFile(bw, savePng: true))
			{
				throw new InvalidOperationException("Could not capture character history.");
			}
			return memoryStream.ToArray();
		}

		internal static void Restore(ChaControl character, byte[] snapshot)
		{
			ChaFileControl chaFileControl = new ChaFileControl();
			using (MemoryStream input = new MemoryStream(snapshot))
			{
				using BinaryReader br = new BinaryReader(input);
				if (!chaFileControl.LoadCharaFile(br))
				{
					throw new InvalidOperationException("Could not read character history.");
				}
			}
			if (chaFileControl.parameter.sex != character.sex)
			{
				throw new InvalidOperationException("Choose a reference card of the same sex as the selected character.");
			}
			KoiStudioHeightPreservation.Prepare(character, chaFileControl);
			byte[] array = Serialize(chaFileControl);
			if (StudioAPI.InsideStudio)
			{
				foreach (ObjectCtrlInfo value in Singleton<global::Studio.Studio>.Instance.dicObjectCtrl.Values)
				{
					OCIChar selected = value as OCIChar;
					if (selected == null || !(selected.charInfo == character))
					{
						continue;
					}
					WardrobePseudoMakerState.Snapshot snapshot2 = WardrobePseudoMakerState.Capture(character);
					WardrobePseudoMakerState.PrepareFullReplacement(character);
					try
					{
						WithTemporaryCard(array, delegate(string path)
						{
							selected.ChangeChara(path);
						});
						return;
					}
					catch
					{
						WardrobePseudoMakerState.Restore(character, snapshot2);
						throw;
					}
				}
				throw new InvalidOperationException("The editing character is no longer in the scene.");
			}
			WardrobePseudoMakerState.Snapshot snapshot3 = WardrobePseudoMakerState.Capture(character);
			WardrobePseudoMakerState.PrepareFullReplacement(character);
			try
			{
				using (MemoryStream input2 = new MemoryStream(array))
				{
					using BinaryReader br2 = new BinaryReader(input2);
					if (!character.chaFile.LoadCharaFile(br2))
					{
						throw new InvalidOperationException("Could not restore character history.");
					}
				}
				character.ChangeCoordinateType((ChaFileDefine.CoordinateType)character.fileStatus.coordinateType);
				KoiStudioHeightPreservation.Prepare(character);
				character.Reload();
			}
			catch
			{
				WardrobePseudoMakerState.Restore(character, snapshot3);
				throw;
			}
		}

		internal static void WithTemporaryCard(byte[] card, Action<string> load)
		{
			string text = Path.Combine(Path.GetTempPath(), "F604_VanityHistory_" + Guid.NewGuid().ToString("N") + ".png");
			try
			{
				File.WriteAllBytes(text, card);
				load(text);
			}
			finally
			{
				if (File.Exists(text))
				{
					File.Delete(text);
				}
			}
		}
	}
	internal static class SharedMakeUpDrawerLayout
	{
		internal struct Placement
		{
			internal float X;

			internal float Y;

			internal float Height;
		}

		internal static Placement Place(float x, float y, float hostWidth, float width, float desiredHeight, float screenWidth, float screenHeight)
		{
			float num = x + hostWidth + 8f;
			if (num + width > screenWidth - 6f)
			{
				num = x - width - 8f;
			}
			num = Math.Max(6f, Math.Min(num, Math.Max(6f, screenWidth - width - 6f)));
			float num2 = Math.Max(6f, Math.Min(y, Math.Max(6f, screenHeight - 148f)));
			return new Placement
			{
				X = num,
				Y = num2,
				Height = Math.Min(desiredHeight, Math.Max(140f, screenHeight - num2 - 8f))
			};
		}
	}
	internal static class SharedMakeUpEditBars
	{
		internal static void DrawHomeBar(Func<string, string> translate, int undoCount, int redoCount, bool hasInitial, Action undo, Action reset, Action redo)
		{
			if (MakeUpLang.DeferFooter(delegate
			{
				DrawHomeBar(translate, undoCount, redoCount, hasInitial, undo, reset, redo);
			}))
			{
				return;
			}
			Rect rect = GUILayoutUtility.GetRect(480f, 74f, GUILayout.Width(480f), GUILayout.Height(74f));
			bool enabled = GUI.enabled;
			string[] array = new string[3]
			{
				"◀ " + translate("Undo") + " (" + undoCount + ")",
				translate("Reset"),
				translate("Redo") + " (" + redoCount + ") ▶"
			};
			Action[] array2 = new Action[3] { undo, reset, redo };
			bool[] array3 = new bool[3]
			{
				undoCount > 0,
				hasInitial,
				redoCount > 0
			};
			try
			{
				for (int num = 0; num < 3; num++)
				{
					GUI.enabled = enabled && array3[num];
					GUIStyle style = FooterStyle(array[num], 146.66667f);
					if (GUI.Button(new Rect(rect.x + 12f + (float)num * 154.66667f, rect.y + 21f, 146.66667f, 32f), array[num], style) && array2[num] != null)
					{
						array2[num]();
					}
				}
			}
			finally
			{
				GUI.enabled = enabled;
			}
		}

		internal static void DrawSectionToolbar(Func<string, string> translate, string sectionLabel, bool showClothesToggle, bool clothesHidden, bool canPaste, Action toggleClothes, Action copy, Action paste)
		{
			Func<string, string> func = translate ?? ((Func<string, string>)((string value) => value));
			string arg = sectionLabel ?? string.Empty;
			bool enabled = GUI.enabled;
			GUILayout.BeginHorizontal(GUILayout.Height(25f));
			if (showClothesToggle && GUILayout.Button(func(clothesHidden ? "Clothes Hidden" : "Hide Clothes"), GUILayout.Width(125f), GUILayout.Height(25f)))
			{
				toggleClothes?.Invoke();
			}
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(func("Copy") + " " + func(arg), GUILayout.Width(145f), GUILayout.Height(25f)))
			{
				copy?.Invoke();
			}
			GUI.enabled = enabled && canPaste;
			if (GUILayout.Button(func("Paste") + " " + func(arg), GUILayout.Width(145f), GUILayout.Height(25f)))
			{
				paste?.Invoke();
			}
			GUI.enabled = enabled;
			GUILayout.EndHorizontal();
		}

		internal static void DrawBottomBar(Func<string, string> translate, bool body, int undoCount, int redoCount, bool hasClipboard, bool hasInitial, string status, Action undo, Action copy, Action reset, Action paste, Action redo)
		{
			if (MakeUpLang.DeferFooter(delegate
			{
				DrawBottomBar(translate, body, undoCount, redoCount, hasClipboard, hasInitial, status, undo, copy, reset, paste, redo);
			}))
			{
				return;
			}
			Func<string, string> func = translate ?? ((Func<string, string>)((string value) => value));
			Rect rect = GUILayoutUtility.GetRect(480f, 74f, GUILayout.Width(480f), GUILayout.Height(74f));
			if (!string.IsNullOrEmpty(status))
			{
				string text = func(status);
				GUIStyle style = new GUIStyle(GUI.skin.label)
				{
					clipping = TextClipping.Clip,
					wordWrap = false,
					alignment = TextAnchor.MiddleCenter
				};
				GUI.Label(new Rect(rect.x, rect.y - 18f, 480f, 18f), new GUIContent(text, text), style);
			}
			IList<MakeUpActionState> list = MakeUpParityContractCore.ResolveBottomActions(body, undoCount, redoCount, hasClipboard, hasInitial);
			string[] array = new string[5]
			{
				"◀ " + func("Undo") + " (" + undoCount + ")",
				func(body ? "Copy Body" : "Copy Head"),
				func("Reset"),
				func(body ? "Paste Body" : "Paste Head"),
				func("Redo") + " (" + redoCount + ") ▶"
			};
			Action[] array2 = new Action[5] { undo, copy, reset, paste, redo };
			Rect[] array3 = new Rect[5]
			{
				new Rect(rect.x + 12f, rect.y + 41f, 224f, 26f),
				new Rect(rect.x + 12f, rect.y + 7f, 146.66667f, 26f),
				new Rect(rect.x + 12f + 154.66667f, rect.y + 7f, 146.66667f, 26f),
				new Rect(rect.x + 12f + 309.33334f, rect.y + 7f, 146.66667f, 26f),
				new Rect(rect.x + 244f, rect.y + 41f, 224f, 26f)
			};
			bool enabled = GUI.enabled;
			try
			{
				for (int num = 0; num < 5; num++)
				{
					GUI.enabled = enabled && list[num].Enabled;
					GUIStyle style2 = FooterStyle(array[num], array3[num].width);
					if (GUI.Button(array3[num], array[num], style2) && array2[num] != null)
					{
						array2[num]();
					}
				}
			}
			finally
			{
				GUI.enabled = enabled;
			}
		}

		private static GUIStyle FooterStyle(string label, float width)
		{
			return SharedMakeUpHomeView.FitLabel(new GUIStyle(GUI.skin.button)
			{
				fixedWidth = 0f,
				fixedHeight = 0f,
				margin = new RectOffset(),
				border = new RectOffset(6, 6, 6, 6),
				clipping = TextClipping.Clip,
				alignment = TextAnchor.MiddleCenter
			}, label, width);
		}
	}
	internal sealed class SharedMakeUpHomeHistory<T> where T : class
	{
		private readonly Func<T> capture;

		private readonly Action<T> apply;

		private readonly List<T> undo = new List<T>();

		private readonly List<T> redo = new List<T>();

		private int characterId;

		private T initial;

		internal int UndoCount => undo.Count;

		internal int RedoCount => redo.Count;

		internal bool HasInitial => initial != null;

		internal SharedMakeUpHomeHistory(Func<T> capture, Action<T> apply)
		{
			this.capture = capture;
			this.apply = apply;
		}

		internal void EnsureCharacter(int id)
		{
			if (id != characterId || initial == null)
			{
				Clear();
				if (id != 0)
				{
					initial = capture();
					characterId = id;
				}
			}
		}

		internal void Clear()
		{
			characterId = 0;
			Dispose(initial);
			initial = null;
			Clear(undo);
			Clear(redo);
		}

		internal void Execute(Action action)
		{
			T val = capture();
			try
			{
				action();
			}
			catch
			{
				try
				{
					apply(val);
				}
				finally
				{
					Dispose(val);
				}
				throw;
			}
			Push(undo, val);
			Clear(redo);
		}

		internal void Undo()
		{
			Step(undo, redo);
		}

		internal void Redo()
		{
			Step(redo, undo);
		}

		internal void ResetAppearance()
		{
			if (initial != null)
			{
				Execute(delegate
				{
					apply(initial);
				});
			}
		}

		private void Step(List<T> source, List<T> destination)
		{
			if (source.Count == 0)
			{
				return;
			}
			T val = capture();
			T val2 = source[source.Count - 1];
			try
			{
				apply(val2);
			}
			catch
			{
				try
				{
					apply(val);
				}
				finally
				{
					Dispose(val);
				}
				throw;
			}
			source.RemoveAt(source.Count - 1);
			Dispose(val2);
			Push(destination, val);
		}

		private static void Push(List<T> stack, T item)
		{
			stack.Add(item);
			if (stack.Count > 10)
			{
				Dispose(stack[0]);
				stack.RemoveAt(0);
			}
		}

		private static void Dispose(T item)
		{
			if (item is IDisposable disposable)
			{
				disposable.Dispose();
			}
		}

		private static void Clear(List<T> stack)
		{
			foreach (T item in stack)
			{
				Dispose(item);
			}
			stack.Clear();
		}
	}
	internal sealed class SharedMakeUpHomeModel
	{
		internal Func<string, string> Translate { get; set; }

		internal Texture ReferencePreview { get; set; }

		internal Texture SnapshotPreview { get; set; }

		internal string ReferenceCaption { get; set; }

		internal string SaveLabel { get; set; }

		internal bool HasCharacter { get; set; }

		internal bool HasReference { get; set; }

		internal bool InsideStudio { get; set; }

		internal bool HasSnapshot { get; set; }

		internal Action OpenReference { get; set; }

		internal Action LoadReference { get; set; }

		internal Action CopyAll { get; set; }

		internal Action ImportHead { get; set; }

		internal Action ImportBody { get; set; }

		internal Action ImportMakeup { get; set; }

		internal Action ImportClothes { get; set; }

		internal Action ImportAccessories { get; set; }

		internal Action Snapshot { get; set; }

		internal Action CopyReferencePortrait { get; set; }

		internal Action SaveCharacter { get; set; }

		internal Action<Rect, bool> DecorateReference { get; set; }

		internal Action<Rect> DecorateSnapshot { get; set; }
	}
	internal static class SharedMakeUpHomeView
	{
		internal static void Draw(SharedMakeUpHomeModel model)
		{
			if (model != null)
			{
				Func<string, string> func = model.Translate ?? ((Func<string, string>)((string value) => value));
				IList<MakeUpActionState> list = MakeUpParityContractCore.ResolveHomeActions(model.HasCharacter, model.HasReference, model.InsideStudio, model.HasSnapshot);
				GUIStyle style = new GUIStyle(GUI.skin.label)
				{
					fontStyle = FontStyle.Bold,
					alignment = TextAnchor.MiddleCenter
				};
				GUIStyle style2 = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleCenter,
					clipping = TextClipping.Clip
				};
				GUIStyle style3 = new GUIStyle(GUI.skin.button)
				{
					alignment = TextAnchor.MiddleCenter,
					clipping = TextClipping.Clip
				};
				bool enabled = GUI.enabled;
				SharedMakeUpShell.Header(func("Reference Character"));
				GUILayout.BeginHorizontal(GUILayout.Width(452f));
				GUILayout.BeginVertical(GUILayout.Width(218f));
				GUILayout.Label(func("Reference"), style, GUILayout.Width(218f));
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				Rect rect = GUILayoutUtility.GetRect(174f, 243f, GUI.skin.box, GUILayout.Width(174f), GUILayout.Height(243f));
				bool arg = Event.current != null && rect.Contains(Event.current.mousePosition);
				if (GUI.Button(rect, (model.ReferencePreview == null) ? GUIContent.none : new GUIContent(model.ReferencePreview), GUI.skin.box) && model.OpenReference != null)
				{
					model.OpenReference();
				}
				if (model.DecorateReference != null)
				{
					model.DecorateReference(rect, arg);
				}
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
				GUILayout.Label(string.IsNullOrEmpty(model.ReferenceCaption) ? func("Choose a reference card") : model.ReferenceCaption, style2, GUILayout.Width(218f), GUILayout.Height(18f));
				DrawSingle(list[0], func("Load Reference"), model.LoadReference, enabled, style3);
				DrawPair(list[1], func("Import All"), model.CopyAll, list[2], func("Import Head"), model.ImportHead, enabled, style3);
				DrawPair(list[3], func("Import Body"), model.ImportBody, list[4], func("Import Makeup"), model.ImportMakeup, enabled, style3);
				DrawPair(list[5], func("Import Clothes"), model.ImportClothes, list[6], func("Import Accessories"), model.ImportAccessories, enabled, style3);
				GUILayout.EndVertical();
				GUILayout.Space(12f);
				GUILayout.BeginVertical(GUILayout.Width(218f));
				GUILayout.Label(func("Snapshot"), style, GUILayout.Width(218f));
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				Rect rect2 = GUILayoutUtility.GetRect(174f, 243f, GUI.skin.box, GUILayout.Width(174f), GUILayout.Height(243f));
				if (model.SnapshotPreview == null)
				{
					GUI.Box(rect2, GUIContent.none);
				}
				else
				{
					GUI.Box(rect2, new GUIContent(model.SnapshotPreview));
				}
				if (model.DecorateSnapshot != null)
				{
					model.DecorateSnapshot(rect2);
				}
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
				GUILayout.Label(" ", style2, GUILayout.Width(218f), GUILayout.Height(18f));
				DrawSingle(list[7], func("Snapshot"), model.Snapshot, enabled, style3);
				DrawSingle(list[8], func("Copy Reference Portrait"), model.CopyReferencePortrait, enabled, style3);
				DrawSingle(list[9], string.IsNullOrEmpty(model.SaveLabel) ? func("Save Character") : model.SaveLabel, model.SaveCharacter, enabled, style3);
				GUILayout.EndVertical();
				GUI.enabled = enabled;
				GUILayout.EndHorizontal();
			}
		}

		private static void DrawSingle(MakeUpActionState state, string label, Action callback, bool parentEnabled, GUIStyle style)
		{
			style = FitLabel(style, label, 200f);
			float height = Mathf.Max(27f, style.CalcHeight(new GUIContent(label), 200f) + 6f);
			GUILayout.BeginHorizontal(GUILayout.Width(218f));
			GUILayout.FlexibleSpace();
			GUI.enabled = parentEnabled && state.Enabled;
			if (GUILayout.Button(label, style, GUILayout.Width(200f), GUILayout.Height(height)))
			{
				callback?.Invoke();
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
		}

		private static void DrawPair(MakeUpActionState leftState, string leftLabel, Action left, MakeUpActionState rightState, string rightLabel, Action right, bool parentEnabled, GUIStyle style)
		{
			GUIStyle gUIStyle = FitLabel(style, leftLabel, 98f);
			GUIStyle gUIStyle2 = FitLabel(style, rightLabel, 98f);
			float height = Mathf.Max(27f, Mathf.Max(gUIStyle.CalcHeight(new GUIContent(leftLabel), 98f), gUIStyle2.CalcHeight(new GUIContent(rightLabel), 98f)) + 6f);
			GUILayout.BeginHorizontal(GUILayout.Width(218f));
			GUILayout.FlexibleSpace();
			GUI.enabled = parentEnabled && leftState.Enabled;
			if (GUILayout.Button(leftLabel, gUIStyle, GUILayout.Width(98f), GUILayout.Height(height)))
			{
				left?.Invoke();
			}
			GUI.enabled = parentEnabled && rightState.Enabled;
			if (GUILayout.Button(rightLabel, gUIStyle2, GUILayout.Width(98f), GUILayout.Height(height)))
			{
				right?.Invoke();
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
		}

		internal static GUIStyle FitLabel(GUIStyle source, string label, float width)
		{
			GUIStyle gUIStyle = new GUIStyle(source)
			{
				fixedHeight = 0f,
				wordWrap = false
			};
			if (gUIStyle.fontSize <= 0)
			{
				gUIStyle.fontSize = ((GUI.skin.font != null) ? GUI.skin.font.fontSize : 13);
			}
			GUIContent content = new GUIContent(label);
			while (gUIStyle.fontSize > 10 && gUIStyle.CalcSize(content).x > width - 4f)
			{
				gUIStyle.fontSize--;
			}
			gUIStyle.wordWrap = true;
			return gUIStyle;
		}
	}
	internal sealed class SharedMakeUpShell
	{
		private static readonly string[] Tabs = new string[4] { "Face", "Body", "Hair", "Home" };

		private const float FavoriteButtonWidth = 103f;

		private const float ExpandedButtonWidth = 103f;

		private readonly Func<string, string> translate;

		private int tab;

		private bool hadCharacter;

		private string selectedSection;

		private bool showAllSections;

		private bool drawerArrowHovered;

		private double drawerCloseAt;

		private const double DrawerTravelGraceSeconds = 0.2;

		private Vector2 pageScroll;

		private static GUISkin navigationSkin;

		private static GUIStyle navigationStyle;

		private static GUISkin drawerSkin;

		private static GUIStyle drawerButtonStyle;

		private Rect hostRect;

		private Rect drawerRect;

		private float hostScale = 1f;

		private float logicalWidth;

		private float logicalHeight;

		private bool hostActive;

		private Vector2 drawerScroll;

		private readonly List<SharedMakeUpSection> drawerSections = new List<SharedMakeUpSection>();

		private string pendingDrawerSection;

		private const int DrawerWindowId = 604106;

		internal int Tab => tab;

		internal float HostScale => hostScale;

		internal bool CompanionVisible
		{
			get
			{
				if (hostActive && showAllSections && tab != 3 && hostRect.width > 0f)
				{
					return drawerSections.Count > 0;
				}
				return false;
			}
		}

		internal bool MouseOverCompanion
		{
			get
			{
				if (CompanionVisible)
				{
					return drawerRect.Contains(new Vector2(Input.mousePosition.x / hostScale, ((float)Screen.height - Input.mousePosition.y) / hostScale));
				}
				return false;
			}
		}

		internal SharedMakeUpShell(Func<string, string> translateText)
		{
			translate = translateText ?? ((Func<string, string>)((string value) => value));
		}

		internal void SetHostRect(float x, float y, float width, float height)
		{
			hostRect = new Rect(x, y, width, height);
		}

		internal void SetHostMetrics(float scale, float width, float height)
		{
			hostScale = ((scale > 0f) ? scale : 1f);
			logicalWidth = width;
			logicalHeight = height;
		}

		internal void SetActive(bool value)
		{
			hostActive = value;
			if (!value)
			{
				showAllSections = false;
				pendingDrawerSection = null;
			}
		}

		internal void DrawCompanion()
		{
			if (!CompanionVisible)
			{
				return;
			}
			List<string> groups = new List<string>();
			foreach (SharedMakeUpSection drawerSection in drawerSections)
			{
				string item = DrawerGroup(drawerSection);
				if (!groups.Contains(item))
				{
					groups.Add(item);
				}
			}
			if (drawerButtonStyle == null || drawerSkin != GUI.skin)
			{
				drawerSkin = GUI.skin;
				drawerButtonStyle = new GUIStyle(GUI.skin.button)
				{
					fixedWidth = 0f,
					alignment = TextAnchor.MiddleCenter,
					wordWrap = false
				};
			}
			float scrollbarWidth = Mathf.Max(16f, GUI.skin.verticalScrollbar.fixedWidth) + (float)GUI.skin.verticalScrollbar.margin.left + (float)GUI.skin.verticalScrollbar.margin.right;
			float rowMargins = Mathf.Max(drawerButtonStyle.margin.horizontal, GUI.skin.label.margin.horizontal);
			float num = 0f;
			foreach (SharedMakeUpSection drawerSection2 in drawerSections)
			{
				num = Mathf.Max(num, drawerButtonStyle.CalcSize(SharedMakeUpIcons.Content(drawerSection2.Icon, translate(ShortLabel(drawerSection2)))).x);
			}
			foreach (string item2 in groups)
			{
				num = Mathf.Max(num, GUI.skin.label.CalcSize(new GUIContent(translate(item2))).x);
			}
			float width = Mathf.Max(168f, Mathf.Ceil(num) + 22f + scrollbarWidth + rowMargins);
			float verticalInset = (MakeUpLang.HasDrawerWrapper ? 12f : 5f);
			SharedMakeUpDrawerLayout.Placement placement = SharedMakeUpDrawerLayout.Place(hostRect.x, hostRect.y, hostRect.width, width, 2f + 2f * verticalInset + (float)drawerSections.Count * 27f + (float)groups.Count * 23f, (logicalWidth > 0f) ? logicalWidth : ((float)Screen.width / hostScale), (logicalHeight > 0f) ? logicalHeight : ((float)Screen.height / hostScale));
			drawerRect = new Rect(placement.X, placement.Y, width, placement.Height);
			GUI.BringWindowToFront(604106);
			drawerRect = GUI.Window(604106, drawerRect, delegate
			{
				MakeUpLang.DrawDrawerWrapper(new Rect(0f, 0f, width, drawerRect.height));
				GUILayout.BeginArea(new Rect(5f, verticalInset, width - 22f, drawerRect.height - 2f * verticalInset));
				float width2 = Mathf.Max(1f, width - 22f - scrollbarWidth - rowMargins);
				drawerScroll = GUILayout.BeginScrollView(drawerScroll, false, true);
				foreach (string item3 in groups)
				{
					GUILayout.Label(translate(item3), GUILayout.Width(width2), GUILayout.Height(18f));
					foreach (SharedMakeUpSection drawerSection3 in drawerSections)
					{
						if (!(DrawerGroup(drawerSection3) != item3))
						{
							Color backgroundColor = GUI.backgroundColor;
							if (selectedSection == drawerSection3.Key)
							{
								GUI.backgroundColor = new Color(1f, 0.89f, 0.96f);
							}
							if (GUILayout.Button(SharedMakeUpIcons.Content(drawerSection3.Icon, translate(ShortLabel(drawerSection3))), drawerButtonStyle, GUILayout.Width(width2), GUILayout.Height(24f)))
							{
								pendingDrawerSection = drawerSection3.Key;
							}
							GUI.backgroundColor = backgroundColor;
						}
					}
					GUILayout.Space(3f);
				}
				GUILayout.EndScrollView();
				GUILayout.EndArea();
			}, GUIContent.none, GUI.skin.window);
		}

		private static string DrawerGroup(SharedMakeUpSection section)
		{
			if (section.Key.StartsWith("face.shape.", StringComparison.Ordinal))
			{
				return "Shape";
			}
			if (section.Key.StartsWith("face.type", StringComparison.Ordinal) || section.Key.StartsWith("face.overlay", StringComparison.Ordinal))
			{
				return "Face Base";
			}
			if (section.Page == 0)
			{
				return "Makeup";
			}
			if (section.Page == 1)
			{
				if (!section.Key.StartsWith("body.shape.", StringComparison.Ordinal))
				{
					return "Body";
				}
				return "Shape";
			}
			return "Hair";
		}

		internal void Reset()
		{
			tab = 0;
			selectedSection = null;
			showAllSections = false;
			pageScroll = Vector2.zero;
			drawerSections.Clear();
			pendingDrawerSection = null;
			drawerScroll = Vector2.zero;
		}

		internal void Draw(bool hasCharacter, IList<SharedMakeUpSection> sections, Action<SharedMakeUpSection> drawSection, Action drawHome, Action leavingBody)
		{
			if (Event.current != null && Event.current.type == EventType.Layout && pendingDrawerSection != null)
			{
				foreach (SharedMakeUpSection section in sections)
				{
					if (section.Page == tab && section.Key == pendingDrawerSection)
					{
						selectedSection = section.Key;
						pageScroll = Vector2.zero;
						break;
					}
				}
				pendingDrawerSection = null;
			}
			drawerSections.Clear();
			GUILayout.BeginVertical(GUILayout.Width(480f));
			if (hasCharacter)
			{
				hadCharacter = true;
			}
			if (!hasCharacter && !hadCharacter && tab != 3)
			{
				tab = 3;
			}
			int num = SharedMakeUpTabs.Draw(tab, Tabs, translate);
			if (num != tab)
			{
				if (tab == 1 && num != 1)
				{
					leavingBody?.Invoke();
				}
				tab = num;
				showAllSections = false;
				drawerScroll = Vector2.zero;
				pageScroll = Vector2.zero;
			}
			if (tab == 3)
			{
				pageScroll = GUILayout.BeginScrollView(pageScroll, false, true);
				GUILayout.BeginVertical(GUILayout.Width(452f));
				drawHome?.Invoke();
				GUILayout.EndVertical();
				GUILayout.EndScrollView();
				GUILayout.EndVertical();
				return;
			}
			if (!hasCharacter)
			{
				GUILayout.Label(translate("Select a character to edit her appearance."));
				GUILayout.EndVertical();
				return;
			}
			List<SharedMakeUpSection> list = new List<SharedMakeUpSection>();
			foreach (SharedMakeUpSection section2 in sections)
			{
				if (section2.Page == tab)
				{
					list.Add(section2);
				}
			}
			if (list.Count == 0)
			{
				GUILayout.EndVertical();
				return;
			}
			if (Find(list, selectedSection) == null)
			{
				selectedSection = list[0].Key;
			}
			drawerSections.AddRange(list);
			DrawFavorites(list);
			if (showAllSections && hostRect.width <= 0f)
			{
				DrawAllSections(list);
			}
			SharedMakeUpTabs.Divider();
			SharedMakeUpSection sharedMakeUpSection = Find(list, selectedSection);
			pageScroll = GUILayout.BeginScrollView(pageScroll, false, true);
			if (sharedMakeUpSection != null)
			{
				drawSection?.Invoke(sharedMakeUpSection);
			}
			GUILayout.EndScrollView();
			GUILayout.EndVertical();
		}

		private void DrawFavorites(IList<SharedMakeUpSection> sections)
		{
			GUILayout.Space(5f);
			GUILayout.BeginHorizontal();
			string[] array = FavoriteKeys(sections);
			for (int i = 0; i < array.Length; i++)
			{
				SharedMakeUpSection sharedMakeUpSection = Find(sections, array[i]);
				if (sharedMakeUpSection != null)
				{
					DrawSectionButton(sharedMakeUpSection, favorite: true);
				}
			}
			if (VisibleBeyondFavorites(sections).Count > 0)
			{
				if (GUILayout.Button(showAllSections ? "<" : ">", GUILayout.Width(30f), GUILayout.Height(30f)))
				{
					showAllSections = !showAllSections;
				}
				if (Event.current != null && Event.current.type == EventType.Repaint)
				{
					bool flag = GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition);
					if (flag && !drawerArrowHovered && hostRect.width > 0f)
					{
						showAllSections = true;
					}
					double num = Time.realtimeSinceStartup;
					if (flag || MouseOverCompanion || drawerArrowHovered != flag)
					{
						drawerCloseAt = num + 0.2;
					}
					else if (showAllSections && hostRect.width > 0f && num >= drawerCloseAt)
					{
						showAllSections = false;
					}
					drawerArrowHovered = flag;
				}
			}
			else
			{
				showAllSections = false;
				drawerArrowHovered = false;
			}
			GUILayout.EndHorizontal();
		}

		private void DrawAllSections(IList<SharedMakeUpSection> sections)
		{
			IList<SharedMakeUpSection> list = VisibleBeyondFavorites(sections);
			for (int i = 0; i < list.Count; i += 4)
			{
				GUILayout.BeginHorizontal();
				for (int j = 0; j < 4 && i + j < list.Count; j++)
				{
					DrawSectionButton(list[i + j], favorite: false);
				}
				GUILayout.EndHorizontal();
			}
		}

		private static IList<SharedMakeUpSection> VisibleBeyondFavorites(IList<SharedMakeUpSection> sections)
		{
			List<SharedMakeUpSection> list = new List<SharedMakeUpSection>();
			string[] array = FavoriteKeys(sections);
			foreach (SharedMakeUpSection section in sections)
			{
				if (Array.IndexOf(array, section.Key) < 0)
				{
					list.Add(section);
				}
			}
			return list;
		}

		private static string[] FavoriteKeys(IList<SharedMakeUpSection> sections)
		{
			switch (sections[0].Page)
			{
			case 0:
				return new string[4] { "face.type_skin", "face.shape.whole", "face.eye.left", "face.makeup.eyeshadow" };
			case 1:
				return new string[4] { "body.shape.breast", "body.shape.upper", "body.shape.lower", "body.skin" };
			default:
				if (Find(sections, "head.hair.settings") != null)
				{
					return new string[4] { "head.hair.settings", "head.hair.back", "head.hair.front", "head.hair.side" };
				}
				return new string[4] { "head.hair.back", "head.hair.front", "head.hair.side", "head.hair.option" };
			}
		}

		private void DrawSectionButton(SharedMakeUpSection section, bool favorite)
		{
			Color color = GUI.color;
			Color backgroundColor = GUI.backgroundColor;
			bool themeActive = SharedMakeUpTabs.ThemeActive;
			if (themeActive)
			{
				if (navigationStyle == null || navigationSkin != GUI.skin)
				{
					navigationSkin = GUI.skin;
					navigationStyle = new GUIStyle(GUI.skin.button)
					{
						fontSize = 13,
						fontStyle = FontStyle.Bold,
						clipping = TextClipping.Clip,
						alignment = TextAnchor.MiddleCenter
					};
				}
				GUI.color = Color.white;
				GUI.backgroundColor = ((section.Key == selectedSection) ? new Color(1f, 0.89f, 0.96f) : Color.white);
			}
			else if (section.Key == selectedSection)
			{
				GUI.color = new Color(0.68f, 0.91f, 1f);
			}
			GUIContent content = SharedMakeUpIcons.Content(section.Icon, translate(ShortLabel(section)));
			GUILayoutOption gUILayoutOption = GUILayout.Width(favorite ? 103f : 103f);
			if (GUILayout.Button(content, themeActive ? navigationStyle : GUI.skin.button, GUILayout.Height(favorite ? 30f : 27f), gUILayoutOption))
			{
				selectedSection = section.Key;
				showAllSections = false;
				pageScroll = Vector2.zero;
			}
			GUI.backgroundColor = backgroundColor;
			GUI.color = color;
		}

		private static string ShortLabel(SharedMakeUpSection section)
		{
			return section.Key switch
			{
				"face.type_skin" => "Skin", 
				"face.eye.left" => "L Eye", 
				"face.eye.right" => "R Eye", 
				"face.eye.settings" => "Eye FX", 
				"head.hair.settings" => "Settings", 
				_ => section.Label.Replace(" Shape", string.Empty).Replace(" Makeup", string.Empty), 
			};
		}

		private static SharedMakeUpSection Find(IList<SharedMakeUpSection> sections, string key)
		{
			foreach (SharedMakeUpSection section in sections)
			{
				if (section.Key == key)
				{
					return section;
				}
			}
			return null;
		}

		internal static void Header(string label)
		{
			GUILayout.Space(7f);
			GUILayout.Label(label, GUI.skin.box, GUILayout.ExpandWidth(expand: true));
		}

		internal static int PairScope(Func<string, string> translate, int current)
		{
			string[] texts = new string[3]
			{
				translate("Both").ToUpperInvariant(),
				translate("Left").ToUpperInvariant(),
				translate("Right").ToUpperInvariant()
			};
			return GUILayout.SelectionGrid(current, texts, 3, GUILayout.Width(480f), GUILayout.Height(25f));
		}

		internal static void Divider()
		{
			SharedMakeUpTabs.Divider();
		}

		internal static float Slider(string label, float value, Action reset = null)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label(label, GUILayout.Width(136f));
			GUILayout.Label((value * 100f).ToString("0.##"), GUILayout.Width(48f));
			bool flag = GUILayout.RepeatButton("<", GUILayout.Width(31f));
			float result = GUILayout.HorizontalSlider(value, 0f, 1f, GUILayout.MinWidth(105f));
			bool flag2 = GUILayout.RepeatButton(">", GUILayout.Width(31f));
			if (reset != null && GUILayout.Button("R", GUILayout.Width(25f)))
			{
				reset();
			}
			GUILayout.EndHorizontal();
			if (flag)
			{
				result = Mathf.Clamp01(value - 0.01f);
			}
			else if (flag2)
			{
				result = Mathf.Clamp01(value + 0.01f);
			}
			return result;
		}

		internal static bool Swatch(string label, Color color, Action reset = null)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label(label, GUILayout.Width(142f));
			Rect rect = GUILayoutUtility.GetRect(92f, 27f, GUILayout.Width(92f), GUILayout.Height(27f));
			bool result = GUI.Button(rect, GUIContent.none);
			Color color2 = GUI.color;
			GUI.color = new Color(color.r, color.g, color.b, 1f);
			GUI.DrawTexture(new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f), Texture2D.whiteTexture);
			GUI.color = color2;
			GUILayout.Label($"R{(int)(color.r * 255f)} G{(int)(color.g * 255f)} B{(int)(color.b * 255f)}");
			if (reset != null && GUILayout.Button("R", GUILayout.Width(25f)))
			{
				reset();
			}
			GUILayout.EndHorizontal();
			return result;
		}

		internal static int Selector(string label, IList<SharedMakeUpOption> options, int selectedId, ref bool open, ref Vector2 scroll, Action reset = null)
		{
			if (options == null || options.Count == 0)
			{
				return selectedId;
			}
			int num = 0;
			for (int i = 0; i < options.Count; i++)
			{
				if (options[i].Id == selectedId)
				{
					num = i;
					break;
				}
			}
			SharedMakeUpOption sharedMakeUpOption = options[num];
			GUILayout.BeginHorizontal();
			Rect rect = GUILayoutUtility.GetRect(84f, 82f, GUILayout.Width(84f), GUILayout.Height(82f));
			Texture2D texture2D = sharedMakeUpOption.Thumbnail();
			if (GUI.Button(rect, (texture2D == null) ? GUIContent.none : new GUIContent(texture2D), GUI.skin.box))
			{
				open = !open;
			}
			float width = MakeUpParityContractCore.SelectorDetailsWidth(480f, 84f);
			GUILayout.BeginVertical(GUILayout.Width(width));
			GUIStyle style = new GUIStyle(GUI.skin.button)
			{
				clipping = TextClipping.Clip
			};
			if (GUILayout.Button((open ? "▼ " : "▶ ") + label + ": " + sharedMakeUpOption.Name, style, GUILayout.Width(width), GUILayout.Height(28f)))
			{
				open = !open;
			}
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("<", GUILayout.Width(35f)))
			{
				selectedId = options[(num + options.Count - 1) % options.Count].Id;
			}
			if (GUILayout.Button(">", GUILayout.Width(35f)))
			{
				selectedId = options[(num + 1) % options.Count].Id;
			}
			if (reset != null && GUILayout.Button("R", GUILayout.Width(25f)))
			{
				reset();
			}
			GUILayout.EndHorizontal();
			GUILayout.EndVertical();
			GUILayout.EndHorizontal();
			if (!open)
			{
				return selectedId;
			}
			scroll = GUILayout.BeginScrollView(scroll, GUI.skin.box, GUILayout.Height(330f));
			for (int j = 0; j < options.Count; j += 4)
			{
				GUILayout.BeginHorizontal();
				for (int k = 0; k < 4 && j + k < options.Count; k++)
				{
					SharedMakeUpOption sharedMakeUpOption2 = options[j + k];
					GUILayout.BeginVertical(GUILayout.Width(105f));
					Texture2D texture2D2 = sharedMakeUpOption2.Thumbnail();
					Color color = GUI.color;
					if (sharedMakeUpOption2.Id == selectedId)
					{
						GUI.color = Color.cyan;
					}
					if (GUILayout.Button((texture2D2 == null) ? GUIContent.none : new GUIContent(texture2D2), GUI.skin.box, GUILayout.Width(90f), GUILayout.Height(78f)))
					{
						selectedId = sharedMakeUpOption2.Id;
						open = false;
					}
					GUI.color = color;
					GUILayout.Label(sharedMakeUpOption2.Name, GUILayout.Width(100f));
					GUILayout.EndVertical();
				}
				GUILayout.EndHorizontal();
			}
			GUILayout.EndScrollView();
			return selectedId;
		}

		internal static int ListSelector(string label, IList<SharedMakeUpOption> options, int selectedId, ref bool open, ref Vector2 scroll)
		{
			if (options == null || options.Count == 0)
			{
				return selectedId;
			}
			int num = 0;
			for (int i = 0; i < options.Count; i++)
			{
				if (options[i].Id == selectedId)
				{
					num = i;
					break;
				}
			}
			SharedMakeUpOption sharedMakeUpOption = options[num];
			GUIStyle style = new GUIStyle(GUI.skin.button)
			{
				clipping = TextClipping.Clip
			};
			if (GUILayout.Button((open ? "▼ " : "▶ ") + label + ": " + sharedMakeUpOption.Name, style, GUILayout.Height(29f), GUILayout.ExpandWidth(expand: true)))
			{
				open = !open;
			}
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("<", GUILayout.Width(38f)))
			{
				selectedId = options[(num + options.Count - 1) % options.Count].Id;
			}
			if (GUILayout.Button(">", GUILayout.Width(38f)))
			{
				selectedId = options[(num + 1) % options.Count].Id;
			}
			GUILayout.EndHorizontal();
			if (!open)
			{
				return selectedId;
			}
			scroll = GUILayout.BeginScrollView(scroll, GUI.skin.box, GUILayout.Height(250f));
			for (int j = 0; j < options.Count; j++)
			{
				SharedMakeUpOption sharedMakeUpOption2 = options[j];
				Color color = GUI.color;
				if (sharedMakeUpOption2.Id == selectedId)
				{
					GUI.color = Color.cyan;
				}
				if (GUILayout.Button(sharedMakeUpOption2.Name, GUILayout.Height(27f), GUILayout.ExpandWidth(expand: true)))
				{
					selectedId = sharedMakeUpOption2.Id;
					open = false;
				}
				GUI.color = color;
			}
			GUILayout.EndScrollView();
			return selectedId;
		}
	}
	internal sealed class SharedMakeUpSection
	{
		internal string Key;

		internal string Label;

		internal int Page;

		internal string Icon;

		internal SharedMakeUpSection(string key, string label, int page, string icon)
		{
			Key = key;
			Label = label;
			Page = page;
			Icon = icon;
		}
	}
	internal sealed class SharedMakeUpOption
	{
		private readonly Func<Texture2D> thumbnail;

		internal int Id;

		internal string Name;

		internal SharedMakeUpOption(int id, string name, Func<Texture2D> loadThumbnail)
		{
			Id = id;
			Name = name ?? ("ID " + id);
			thumbnail = loadThumbnail;
		}

		internal Texture2D Thumbnail()
		{
			try
			{
				return (thumbnail == null) ? null : thumbnail();
			}
			catch
			{
				return null;
			}
		}
	}
	internal static class SharedMakeUpIcons
	{
		private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

		internal static GUIContent Content(string name, string label)
		{
			Texture2D texture2D = Load(name);
			if (!(texture2D == null))
			{
				return new GUIContent(" " + label, texture2D);
			}
			return new GUIContent(label);
		}

		internal static Texture2D Load(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return null;
			}
			if (Cache.TryGetValue(name, out var value))
			{
				return value;
			}
			try
			{
				using Stream stream = typeof(SharedMakeUpIcons).Assembly.GetManifestResourceStream("Freelancer604.MakeUp.Resources." + name + ".png");
				if (stream == null)
				{
					return null;
				}
				byte[] array = new byte[stream.Length];
				stream.Read(array, 0, array.Length);
				Texture2D texture2D = new Texture2D(2, 2);
				texture2D.LoadImage(array);
				Cache[name] = texture2D;
				return texture2D;
			}
			catch
			{
				return null;
			}
		}
	}
	internal sealed class SharedMakeUpHistory<T> where T : class
	{
		private readonly int capacity;

		private readonly List<T> undo = new List<T>();

		private readonly List<T> redo = new List<T>();

		internal bool CanUndo => undo.Count > 0;

		internal bool CanRedo => redo.Count > 0;

		internal int UndoCount => undo.Count;

		internal int RedoCount => redo.Count;

		internal SharedMakeUpHistory(int maximumDepth)
		{
			capacity = Math.Max(1, maximumDepth);
		}

		internal void Push(T before)
		{
			if (before != null)
			{
				undo.Add(before);
				DisposeAll(redo);
				Trim(undo);
			}
		}

		internal bool TryUndo(T current, out T target)
		{
			target = null;
			if (!CanUndo || current == null)
			{
				Dispose(current);
				return false;
			}
			redo.Add(current);
			Trim(redo);
			target = undo[undo.Count - 1];
			undo.RemoveAt(undo.Count - 1);
			return true;
		}

		internal bool TryRedo(T current, out T target)
		{
			target = null;
			if (!CanRedo || current == null)
			{
				Dispose(current);
				return false;
			}
			undo.Add(current);
			Trim(undo);
			target = redo[redo.Count - 1];
			redo.RemoveAt(redo.Count - 1);
			return true;
		}

		internal void Execute(T before, Action action, Action<T> restore, bool recordHistory = true)
		{
			try
			{
				action();
			}
			catch (Exception failure)
			{
				try
				{
					Rollback(before, restore, failure);
				}
				finally
				{
					Dispose(before);
				}
				throw;
			}
			if (recordHistory)
			{
				Push(before);
			}
			else
			{
				Dispose(before);
			}
		}

		internal bool ApplyUndo(T current, Action<T> apply)
		{
			return Step(undo, redo, current, apply);
		}

		internal bool ApplyRedo(T current, Action<T> apply)
		{
			return Step(redo, undo, current, apply);
		}

		private bool Step(List<T> source, List<T> destination, T current, Action<T> apply)
		{
			if (source.Count == 0 || current == null)
			{
				Dispose(current);
				return false;
			}
			T val = source[source.Count - 1];
			try
			{
				apply(val);
			}
			catch (Exception failure)
			{
				try
				{
					Rollback(current, apply, failure);
				}
				finally
				{
					Dispose(current);
				}
				throw;
			}
			source.RemoveAt(source.Count - 1);
			Dispose(val);
			destination.Add(current);
			Trim(destination);
			return true;
		}

		private static void Rollback(T before, Action<T> restore, Exception failure)
		{
			try
			{
				restore(before);
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException("Appearance change failed: " + failure.Message + ". Restoring the previous appearance also failed: " + ex.Message, failure);
			}
		}

		internal void Clear()
		{
			DisposeAll(undo);
			DisposeAll(redo);
		}

		private void Trim(List<T> values)
		{
			while (values.Count > capacity)
			{
				Dispose(values[0]);
				values.RemoveAt(0);
			}
		}

		private static void Dispose(T value)
		{
			if (value is IDisposable disposable)
			{
				disposable.Dispose();
			}
		}

		private static void DisposeAll(List<T> values)
		{
			foreach (T value in values)
			{
				Dispose(value);
			}
			values.Clear();
		}
	}
	internal sealed class SharedMakeUpSectionClipboard<T> where T : class
	{
		private string sectionKey;

		private T value;

		internal void Copy(string key, T snapshot)
		{
			if (value != snapshot)
			{
				Clear();
			}
			sectionKey = key;
			value = snapshot;
		}

		internal bool TryPaste(string key, out T snapshot)
		{
			snapshot = null;
			if (value == null || !string.Equals(sectionKey, key, StringComparison.Ordinal))
			{
				return false;
			}
			snapshot = value;
			return true;
		}

		internal void Clear()
		{
			if (value is IDisposable disposable)
			{
				disposable.Dispose();
			}
			sectionKey = null;
			value = null;
		}
	}
	internal static class SharedMakeUpTabs
	{
		private static readonly Color TabAccent = new Color(0.25f, 0.86f, 1f);

		private static GUISkin headingSkin;

		private static GUIStyle headingStyle;

		internal static bool ThemeActive => MakeUpLang.IsShellThemeActive;

		internal static int Draw(int active, string[] labels, Func<string, string> translate)
		{
			if (labels == null || labels.Length == 0)
			{
				return active;
			}
			Func<string, string> func = translate ?? ((Func<string, string>)((string value) => value));
			Rect rect = GUILayoutUtility.GetRect(10f, 27f, GUILayout.Width(480f), GUILayout.Height(27f));
			float num = rect.width / (float)labels.Length;
			int result = active;
			bool themeActive = ThemeActive;
			if (themeActive && (headingStyle == null || headingSkin != GUI.skin))
			{
				headingSkin = GUI.skin;
				headingStyle = new GUIStyle(GUI.skin.button)
				{
					fontSize = 14,
					fontStyle = FontStyle.Bold,
					alignment = TextAnchor.MiddleCenter,
					clipping = TextClipping.Clip
				};
			}
			Color color = GUI.color;
			Color backgroundColor = GUI.backgroundColor;
			GUI.color = (themeActive ? new Color(0.78f, 0.65f, 0.77f, 0.55f) : new Color(1f, 1f, 1f, 0.14f));
			GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Texture2D.whiteTexture);
			GUI.color = color;
			for (int num2 = 0; num2 < labels.Length; num2++)
			{
				Rect position = new Rect(rect.x + (float)num2 * num, rect.y, num, rect.height);
				GUI.color = ((themeActive || num2 == active) ? Color.white : new Color(0.72f, 0.72f, 0.72f));
				if (themeActive)
				{
					GUI.backgroundColor = ((num2 == active) ? new Color(1f, 0.89f, 0.96f) : Color.white);
				}
				if (GUI.Button(position, func(labels[num2]), themeActive ? headingStyle : GUI.skin.button))
				{
					result = num2;
				}
				GUI.backgroundColor = backgroundColor;
				GUI.color = color;
				if (num2 == active)
				{
					GUI.color = (themeActive ? new Color(0.7f, 0.34f, 0.55f) : TabAccent);
					GUI.DrawTexture(new Rect(position.x + 3f, position.yMax - 3f, position.width - 6f, 3f), Texture2D.whiteTexture);
					GUI.color = color;
				}
			}
			GUI.color = color;
			return result;
		}

		internal static void Divider()
		{
			GUILayout.Space(5f);
			Rect rect = GUILayoutUtility.GetRect(480f, 2f, GUILayout.Width(480f), GUILayout.Height(2f));
			if (Event.current != null && Event.current.type == EventType.Repaint)
			{
				Color color = GUI.color;
				GUI.color = (ThemeActive ? new Color(0.78f, 0.65f, 0.77f, 0.65f) : new Color(0.35f, 0.35f, 0.35f, 0.9f));
				GUI.DrawTexture(rect, Texture2D.whiteTexture);
				GUI.color = color;
			}
			GUILayout.Space(5f);
		}
	}
	internal static class SharedOverlayPreview
	{
		internal static void Draw(Texture texture)
		{
			Rect rect = GUILayoutUtility.GetRect(120f, 120f, GUILayout.Width(120f), GUILayout.Height(120f));
			if (Event.current.type != EventType.Repaint)
			{
				return;
			}
			GUI.Box(rect, GUIContent.none);
			Rect position = new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f);
			Color color = GUI.color;
			try
			{
				for (int i = 0; i < 12; i++)
				{
					for (int j = 0; j < 12; j++)
					{
						float num = (((j + i) % 2 == 0) ? 0.24f : 0.36f);
						GUI.color = new Color(num, num, num, 1f);
						GUI.DrawTexture(new Rect(position.x + (float)j * position.width / 12f, position.y + (float)i * position.height / 12f, position.width / 12f, position.height / 12f), Texture2D.whiteTexture);
					}
				}
				GUI.color = Color.white;
				if (texture != null)
				{
					GUI.DrawTexture(position, texture, ScaleMode.ScaleToFit, alphaBlend: true);
					return;
				}
				GUI.Label(position, MakeUpLang.T("(empty)"), new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleCenter
				});
			}
			finally
			{
				GUI.color = color;
			}
		}
	}
	internal sealed class SharedReferenceCardBrowser
	{
		private readonly SharedReferenceFolders folders = new SharedReferenceFolders();

		private readonly Dictionary<string, Vector2> positions = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);

		private Vector2 scroll;

		private string pendingFolder;

		private bool refresh;

		internal void Initialize(string root, string rememberedCard)
		{
			folders.Initialize(root, rememberedCard);
		}

		internal void Refresh()
		{
			refresh = true;
		}

		internal void Draw(Func<string, string> translate, string selected, Func<string, Texture2D> thumbnail, Action<string> select)
		{
			if (Event.current.type == EventType.Layout)
			{
				if (pendingFolder != null)
				{
					positions[folders.Current] = scroll;
					if (folders.Navigate(pendingFolder) && !positions.TryGetValue(folders.Current, out scroll))
					{
						scroll = Vector2.zero;
					}
					pendingFolder = null;
				}
				if (refresh)
				{
					folders.Refresh();
					refresh = false;
				}
			}
			GUILayout.Space(7f);
			SharedMakeUpShell.Header(translate("Choose a reference card"));
			GUILayout.BeginHorizontal();
			bool enabled = GUI.enabled;
			GUI.enabled = enabled && folders.CanGoUp;
			if (GUILayout.Button(translate("Up"), GUILayout.Width(45f)))
			{
				pendingFolder = Path.GetDirectoryName(folders.Current);
			}
			GUI.enabled = enabled;
			string text = folders.Current.Substring(folders.Root.Length).TrimStart(Path.DirectorySeparatorChar);
			GUILayout.Label(new GUIContent(Path.GetFileName(folders.Root) + ((text.Length > 0) ? (" / " + text.Replace(Path.DirectorySeparatorChar, '/')) : ""), folders.Current));
			if (GUILayout.Button(translate("Refresh"), GUILayout.Width(65f)))
			{
				refresh = true;
			}
			GUILayout.EndHorizontal();
			scroll = GUILayout.BeginScrollView(scroll, GUI.skin.box, GUILayout.Height(260f));
			if (!string.IsNullOrEmpty(folders.Error))
			{
				GUILayout.Label(folders.Error);
			}
			foreach (string folder in folders.Folders)
			{
				if (GUILayout.Button("[+] " + Path.GetFileName(folder), GUILayout.Height(27f)))
				{
					pendingFolder = folder;
				}
			}
			if (folders.Folders.Count == 0 && folders.Cards.Count == 0)
			{
				GUILayout.Label(translate("No character cards in this folder."));
			}
			for (int i = 0; i < folders.Cards.Count; i += 4)
			{
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				for (int j = 0; j < 4 && i + j < folders.Cards.Count; j++)
				{
					string text2 = folders.Cards[i + j];
					Texture2D texture2D = thumbnail(text2);
					Color color = GUI.color;
					if (string.Equals(text2, selected, StringComparison.OrdinalIgnoreCase))
					{
						GUI.color = Color.cyan;
					}
					GUIContent obj = ((texture2D == null) ? new GUIContent(Path.GetFileNameWithoutExtension(text2)) : new GUIContent(texture2D));
					obj.tooltip = Path.GetFileNameWithoutExtension(text2);
					if (GUILayout.Button(obj, GUI.skin.box, GUILayout.Width(96f), GUILayout.Height(124f)))
					{
						select(text2);
					}
					GUI.color = color;
				}
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
			}
			GUILayout.EndScrollView();
		}
	}
	internal static class SharedReferenceCardFiles
	{
		internal static IList<string> Find(string root)
		{
			List<string> list = new List<string>();
			Stack<string> stack = new Stack<string>();
			if (Directory.Exists(root))
			{
				stack.Push(root);
			}
			while (stack.Count > 0)
			{
				string path = stack.Pop();
				try
				{
					string[] files = Directory.GetFiles(path);
					foreach (string text in files)
					{
						if (string.Equals(Path.GetExtension(text), ".png", StringComparison.OrdinalIgnoreCase))
						{
							list.Add(text);
						}
					}
					files = Directory.GetDirectories(path);
					foreach (string text2 in files)
					{
						if ((File.GetAttributes(text2) & FileAttributes.ReparsePoint) == 0)
						{
							stack.Push(text2);
						}
					}
				}
				catch (UnauthorizedAccessException)
				{
				}
				catch (IOException)
				{
				}
			}
			list.Sort(delegate(string left, string right)
			{
				int num = File.GetLastWriteTimeUtc(right).CompareTo(File.GetLastWriteTimeUtc(left));
				return (num == 0) ? string.Compare(left, right, StringComparison.OrdinalIgnoreCase) : num;
			});
			return list;
		}
	}
	internal sealed class SharedReferenceFolders
	{
		internal readonly List<string> Folders = new List<string>();

		internal readonly List<string> Cards = new List<string>();

		internal string Root { get; private set; }

		internal string Current { get; private set; }

		internal string Error { get; private set; }

		internal bool CanGoUp
		{
			get
			{
				if (Current != null)
				{
					return !string.Equals(Current, Root, StringComparison.OrdinalIgnoreCase);
				}
				return false;
			}
		}

		internal void Initialize(string root, string rememberedCard)
		{
			Root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			Current = Root;
			string text = (string.IsNullOrEmpty(rememberedCard) ? null : Path.GetDirectoryName(rememberedCard));
			if (!string.IsNullOrEmpty(text) && IsInside(text) && Directory.Exists(text))
			{
				Current = Path.GetFullPath(text);
			}
			Refresh();
		}

		internal bool Navigate(string path)
		{
			if (string.IsNullOrEmpty(path) || !IsInside(path) || !Directory.Exists(path))
			{
				return false;
			}
			Current = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			Refresh();
			return true;
		}

		private bool IsInside(string path)
		{
			string text = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			if (!string.Equals(text, Root, StringComparison.OrdinalIgnoreCase))
			{
				return text.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}

		internal void Refresh()
		{
			Folders.Clear();
			Cards.Clear();
			Error = null;
			try
			{
				if (!Directory.Exists(Current) && CanGoUp)
				{
					Current = Root;
				}
				if (!Directory.Exists(Current))
				{
					return;
				}
				Folders.AddRange(Directory.GetDirectories(Current));
				string[] files = Directory.GetFiles(Current);
				foreach (string text in files)
				{
					if (string.Equals(Path.GetExtension(text), ".png", StringComparison.OrdinalIgnoreCase))
					{
						Cards.Add(text);
					}
				}
				Folders.Sort(StringComparer.OrdinalIgnoreCase);
				Cards.Sort(delegate(string a, string b)
				{
					int num = File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a));
					return (num == 0) ? StringComparer.OrdinalIgnoreCase.Compare(a, b) : num;
				});
			}
			catch (IOException ex)
			{
				Error = ex.Message;
			}
			catch (UnauthorizedAccessException ex2)
			{
				Error = ex2.Message;
			}
		}
	}
	internal static class SharedReferenceScenePlacement
	{
		internal static void Load(global::Studio.Studio studio, Action addCharacter)
		{
			TreeNodeObject selectNode = studio.treeNodeCtrl.selectNode;
			ObjectCtrlInfo value;
			Transform transform = ((selectNode != null && studio.dicInfo.TryGetValue(selectNode, out value)) ? (value as OCIChar) : null)?.guideObject.transformTarget;
			bool flag = transform != null;
			float y = (flag ? transform.position.y : 0f);
			Quaternion quaternion = (flag ? transform.rotation : Quaternion.identity);
			HashSet<int> hashSet = new HashSet<int>(studio.dicObjectCtrl.Keys);
			addCharacter();
			OCIChar oCIChar = null;
			foreach (KeyValuePair<int, ObjectCtrlInfo> item in studio.dicObjectCtrl)
			{
				if (!hashSet.Contains(item.Key) && item.Value is OCIChar oCIChar2)
				{
					if (oCIChar != null)
					{
						throw new InvalidOperationException("More than one character was added.");
					}
					oCIChar = oCIChar2;
				}
			}
			if (oCIChar == null)
			{
				throw new InvalidOperationException("Studio did not add the reference character.");
			}
			if (flag)
			{
				GuideObject guideObject = oCIChar.guideObject;
				Vector3 position = guideObject.transformTarget.position;
				position.y = y;
				Transform transform2 = ((guideObject.nonconnect && guideObject.parent != null) ? guideObject.parent : guideObject.transformTarget.parent);
				guideObject.changeAmount.pos = ((transform2 == null) ? position : transform2.InverseTransformPoint(position));
				guideObject.changeAmount.rot = ((transform2 == null) ? quaternion : (Quaternion.Inverse(transform2.rotation) * quaternion)).eulerAngles;
			}
			FollowCamera(oCIChar);
			studio.treeNodeCtrl.SelectSingle(oCIChar.treeNodeObject, _deselect: false);
		}

		private static void FollowCamera(OCIChar character)
		{
			EyeLookController eyeLookCtrl = character.charInfo.eyeLookCtrl;
			if (eyeLookCtrl == null || eyeLookCtrl.eyeLookScript == null)
			{
				return;
			}
			EyeTypeState[] eyeTypeStates = eyeLookCtrl.eyeLookScript.eyeTypeStates;
			if (eyeTypeStates == null)
			{
				return;
			}
			for (int i = 0; i < eyeTypeStates.Length; i++)
			{
				if (eyeTypeStates[i] != null && !(eyeTypeStates[i].lookType.ToString() != "TARGET"))
				{
					character.ChangeLookEyesPtn(i);
					break;
				}
			}
		}
	}
	internal sealed class KoiMakeUpCardSaveStage : IDisposable
	{
		private readonly ChaControl character;

		private readonly OCIChar studioCharacter;

		private readonly MakeUpCardExpressionState live;

		private readonly float liveMouthOpenMax;

		private readonly bool liveMouthFixed;

		private readonly byte liveTongueState;

		private readonly bool liveMouthAdjustWidth;

		private readonly float liveMouthFixedRate;

		private readonly bool hasMouthController;

		private bool disposed;

		private KoiMakeUpCardSaveStage(ChaControl character, OCIChar studioCharacter)
		{
			this.character = character;
			this.studioCharacter = studioCharacter;
			live = Capture();
			liveMouthOpenMax = character.fileStatus.mouthOpenMax;
			liveMouthFixed = character.fileStatus.mouthFixed;
			liveTongueState = character.GetTongueState();
			hasMouthController = character.mouthCtrl != null;
			if (hasMouthController)
			{
				liveMouthAdjustWidth = character.mouthCtrl.useAjustWidthScale;
				liveMouthFixedRate = character.mouthCtrl.FixedRate;
			}
		}

		internal static KoiMakeUpCardSaveStage Create(ChaControl character)
		{
			if (character == null)
			{
				throw new InvalidOperationException("No character is selected for card saving.");
			}
			global::Studio.Studio instance = Singleton<global::Studio.Studio>.Instance;
			if (instance == null)
			{
				throw new InvalidOperationException("Studio is unavailable for character card saving.");
			}
			OCIChar oCIChar = null;
			foreach (ObjectCtrlInfo value in instance.dicObjectCtrl.Values)
			{
				if (value is OCIChar oCIChar2 && oCIChar2.charInfo == character)
				{
					oCIChar = oCIChar2;
					break;
				}
			}
			if (oCIChar == null || oCIChar.oiCharInfo == null || oCIChar.guideObject == null || oCIChar.guideObject.transformTarget == null)
			{
				throw new InvalidOperationException("The selected Studio character could not be matched for card saving.");
			}
			try
			{
				MakeUpPoseEditorRefresh.Refresh(oCIChar.guideObject.transformTarget);
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException("Pose Editor could not prepare this character card: " + ex.GetBaseException().Message, ex);
			}
			KoiMakeUpCardSaveStage koiMakeUpCardSaveStage = new KoiMakeUpCardSaveStage(character, oCIChar);
			try
			{
				koiMakeUpCardSaveStage.Apply(MakeUpCardExpressionPolicy.NeutralForSave(koiMakeUpCardSaveStage.live));
				koiMakeUpCardSaveStage.NeutralizeRawStatus();
				koiMakeUpCardSaveStage.VerifyNeutral();
				return koiMakeUpCardSaveStage;
			}
			catch
			{
				koiMakeUpCardSaveStage.Dispose();
				throw;
			}
		}

		private MakeUpCardExpressionState Capture()
		{
			return new MakeUpCardExpressionState(character.GetEyesPtn(), character.GetEyesOpenMax(), character.GetMouthPtn(), studioCharacter.oiCharInfo.mouthOpen, character.GetEyebrowPtn(), character.GetEyebrowOpenMax());
		}

		private void Apply(MakeUpCardExpressionState state)
		{
			character.ChangeEyesPtn(state.EyesPattern, blend: false);
			character.ChangeEyesOpenMax(state.EyesOpen);
			character.ChangeMouthPtn(state.MouthPattern, blend: false);
			studioCharacter.ChangeMouthOpen(state.MouthOpen);
			character.ChangeEyebrowPtn(state.EyebrowsPattern, blend: false);
			character.ChangeEyebrowOpenMax(state.EyebrowsOpen);
		}

		private void NeutralizeRawStatus()
		{
			character.fileStatus.mouthOpenMax = 1f;
			character.fileStatus.mouthFixed = false;
		}

		private void VerifyNeutral()
		{
			MakeUpCardExpressionState makeUpCardExpressionState = Capture();
			if (makeUpCardExpressionState.EyesPattern != 0 || makeUpCardExpressionState.EyesOpen != 1f || makeUpCardExpressionState.MouthPattern != 0 || makeUpCardExpressionState.MouthOpen != 0f || makeUpCardExpressionState.EyebrowsPattern != 0 || makeUpCardExpressionState.EyebrowsOpen != 1f)
			{
				throw new InvalidOperationException("The Studio expression could not be neutralized for card saving.");
			}
			if (character.fileStatus.mouthOpenMax != 1f || character.fileStatus.mouthFixed)
			{
				throw new InvalidOperationException("The native facial status could not be neutralized for card saving.");
			}
		}

		public void Dispose()
		{
			if (disposed)
			{
				return;
			}
			disposed = true;
			try
			{
				Apply(live);
			}
			finally
			{
				character.ChangeTongueState(liveTongueState);
				character.fileStatus.mouthOpenMax = liveMouthOpenMax;
				character.fileStatus.mouthFixed = liveMouthFixed;
				if (hasMouthController && character.mouthCtrl != null)
				{
					character.mouthCtrl.UseAdjustWidthScale(liveMouthAdjustWidth);
					character.mouthCtrl.FixedRate = liveMouthFixedRate;
				}
			}
		}
	}
	public sealed class MakeUpPage
	{
		private sealed class KoiHomeSnapshot : IDisposable
		{
			private readonly KoiNativeAppearanceSnapshot native;

			private readonly KoiSnapshot overlays;

			private readonly KoiProviderSnapshot providers;

			private readonly byte[] clothingSlots;

			private readonly ChaControl owner;

			private readonly WardrobePseudoMakerState.Snapshot compatibilityState;

			internal KoiHomeSnapshot(MakeUpSession session, KoiAppearanceAdapter adapter)
			{
				owner = session.Character;
				clothingSlots = SharedClothingSlotsHistory.Capture(owner);
				compatibilityState = WardrobePseudoMakerState.Capture(owner);
				native = new KoiNativeAppearanceSnapshot(session.Character, completeHome: true);
				overlays = KoiSnapshot.Capture(session, adapter, KoiMakeUpSchema.AllSections.Where((KoiMakeUpSection s) => s.Key.Contains(".overlay.")));
				providers = new KoiProviderSnapshot(session.Character, 2);
			}

			internal void Apply(MakeUpSession session, KoiAppearanceAdapter adapter)
			{
				RestoreStage("native appearance", delegate
				{
					native.Apply(session.Character);
				});
				RestoreStage("overlays", delegate
				{
					overlays.Apply(session, adapter);
				});
				RestoreStage("material/bone providers", delegate
				{
					providers.Apply(session.Character);
				});
				RestoreStage("numbered clothing", delegate
				{
					SharedClothingSlotsHistory.Restore(session.Character, clothingSlots);
				});
				if (session.Character == owner)
				{
					RestoreStage("PseudoMaker reset values", delegate
					{
						WardrobePseudoMakerState.Restore(owner, compatibilityState);
					});
				}
			}

			private static void RestoreStage(string stage, Action apply)
			{
				try
				{
					apply();
				}
				catch (Exception ex)
				{
					throw new InvalidOperationException(stage + ": " + ex.GetBaseException().Message, ex);
				}
			}

			public void Dispose()
			{
				providers.Dispose();
				overlays.Dispose();
			}
		}

		private sealed class KoiSnapshot : IDisposable
		{
			internal KoiNativeAppearanceSnapshot NativeRegion;

			internal KoiProviderSnapshot Providers;

			internal bool BodyRegion;

			internal ChaControl HistoryOwner;

			internal WardrobePseudoMakerState.Snapshot HistoryCompatibilityState;

			internal readonly Dictionary<int, float> faceShapes = new Dictionary<int, float>();

			internal readonly Dictionary<int, float> bodyShapes = new Dictionary<int, float>();

			private readonly Dictionary<string, int> ids = new Dictionary<string, int>(StringComparer.Ordinal);

			private readonly Dictionary<string, Color> colors = new Dictionary<string, Color>(StringComparer.Ordinal);

			private readonly Dictionary<string, byte[]> overlays = new Dictionary<string, byte[]>(StringComparer.Ordinal);

			internal static KoiSnapshot Capture(MakeUpSession session, KoiAppearanceAdapter adapter, IEnumerable<KoiMakeUpSection> sections)
			{
				KoiSnapshot koiSnapshot = new KoiSnapshot();
				foreach (KoiMakeUpSection section in sections)
				{
					if (section.Key.Contains(".overlay.") && KoiOverlayProvider.Available(session.Character))
					{
						koiSnapshot.overlays[section.Key] = KoiOverlayProvider.Capture(session.Character, section.Key);
					}
					int[] shapeIndices = section.ShapeIndices;
					foreach (int num in shapeIndices)
					{
						if (section.Page == 0 && num >= 0 && num < session.Character.fileFace.shapeValueFace.Length)
						{
							koiSnapshot.faceShapes[num] = session.Character.fileFace.shapeValueFace[num];
						}
						else if (section.Page == 1 && num >= 0 && num < session.Character.fileBody.shapeValueBody.Length)
						{
							koiSnapshot.bodyShapes[num] = adapter.ReadBodyShape(session, num);
						}
					}
					string[] array = SelectorsFor(section.Key);
					foreach (string key in array)
					{
						if (!koiSnapshot.ids.ContainsKey(key))
						{
							koiSnapshot.ids[key] = adapter.ReadId(session, key);
						}
					}
					array = ColorsFor(section.Key);
					foreach (string key2 in array)
					{
						if (!koiSnapshot.colors.ContainsKey(key2))
						{
							koiSnapshot.colors[key2] = adapter.ReadColor(session, key2);
						}
					}
				}
				return koiSnapshot;
			}

			internal void Apply(MakeUpSession session, KoiAppearanceAdapter adapter, bool restoreCompatibilityState = false)
			{
				if (NativeRegion != null)
				{
					NativeRegion.ApplyRegion(session.Character, BodyRegion);
				}
				KoiSnapshotDelta.Apply(ids, (string key) => adapter.ReadId(session, key), delegate(string key, int value)
				{
					adapter.ApplyId(session, key, value);
				}, (int a, int b) => a == b);
				KoiSnapshotDelta.Apply(faceShapes, (int key) => session.Character.fileFace.shapeValueFace[key], delegate(int key, float value)
				{
					adapter.SetFaceShape(session, key, value);
				}, (float a, float b) => a.Equals(b));
				KoiSnapshotDelta.Apply(bodyShapes, (int key) => adapter.ReadBodyShape(session, key), delegate(int key, float value)
				{
					adapter.SetBodyShape(session, key, value);
				}, (float a, float b) => a.Equals(b));
				KoiSnapshotDelta.Apply(colors, (string key) => adapter.ReadColor(session, key), delegate(string key, Color value)
				{
					adapter.ApplyColor(session, key, value);
				}, (Color a, Color b) => a.Equals(b));
				KoiSnapshotDelta.Apply(overlays, (string key) => KoiOverlayProvider.Capture(session.Character, key), delegate(string key, byte[] value)
				{
					KoiOverlayProvider.Apply(session.Character, key, value);
				}, KoiSnapshotDelta.BytesEqual);
				if (Providers != null)
				{
					Providers.Apply(session.Character);
				}
				if (restoreCompatibilityState && session.Character == HistoryOwner)
				{
					WardrobePseudoMakerState.Restore(HistoryOwner, HistoryCompatibilityState);
				}
			}

			internal void RestoreShape(MakeUpSession session, KoiAppearanceAdapter adapter, bool face, int index)
			{
				if ((face ? faceShapes : bodyShapes).TryGetValue(index, out var value) && !(face ? session.Character.fileFace.shapeValueFace[index] : adapter.ReadBodyShape(session, index)).Equals(value))
				{
					if (face)
					{
						adapter.SetFaceShape(session, index, value);
					}
					else
					{
						adapter.SetBodyShape(session, index, value);
					}
				}
			}

			internal void RestoreIds(MakeUpSession session, KoiAppearanceAdapter adapter, IList<string> targets)
			{
				foreach (string target in targets)
				{
					if (ids.TryGetValue(target, out var value) && adapter.ReadId(session, target) != value)
					{
						adapter.ApplyId(session, target, value);
					}
				}
			}

			internal void RestoreColors(MakeUpSession session, KoiAppearanceAdapter adapter, IList<string> targets)
			{
				foreach (string target in targets)
				{
					if (colors.TryGetValue(target, out var value) && !adapter.ReadColor(session, target).Equals(value))
					{
						adapter.ApplyColor(session, target, value);
					}
				}
			}

			public void Dispose()
			{
				if (Providers != null)
				{
					Providers.Dispose();
					Providers = null;
				}
			}
		}

		private readonly KoiAppearanceAdapter adapter = new KoiAppearanceAdapter();

		private readonly MakeUpSessionState state = new MakeUpSessionState();

		private readonly SharedMakeUpShell shell = new SharedMakeUpShell(MakeUpLang.T);

		private readonly Dictionary<string, bool> selectorOpen = new Dictionary<string, bool>();

		private readonly Dictionary<string, Vector2> selectorScroll = new Dictionary<string, Vector2>();

		private readonly SharedReferenceCardBrowser referenceBrowser = new SharedReferenceCardBrowser();

		private readonly Dictionary<string, Texture2D> homeThumbnails = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

		private readonly MonoBehaviour host;

		private readonly SharedMakeUpHomeHistory<KoiHomeSnapshot> homeHistory;

		private readonly SharedMakeUpHistory<KoiSnapshot> headHistory = new SharedMakeUpHistory<KoiSnapshot>(10);

		private readonly SharedMakeUpHistory<KoiSnapshot> bodyHistory = new SharedMakeUpHistory<KoiSnapshot>(10);

		private readonly SharedMakeUpSectionClipboard<KoiSnapshot> sectionClipboard = new SharedMakeUpSectionClipboard<KoiSnapshot>();

		private MakeUpSession session;

		private byte[] clothesBefore;

		private ChaControl clothesCharacter;

		private bool clothesHidden;

		private bool homeCardsLoaded;

		private string referencePath;

		private string homeStatus;

		private string editStatus;

		private string activeEditKey;

		private ChaFileControl referenceCard;

		private KoiSnapshot headClipboard;

		private KoiSnapshot bodyClipboard;

		private KoiSnapshot initialHead;

		private KoiSnapshot initialBody;

		private int historyCharacterId;

		private byte[] snapshotPng;

		private Texture2D snapshotPreview;

		private bool homeBrowserOpen;

		private int pairScope;

		private static readonly string[] UniversalHairColors = new string[4] { "Hair 0", "Hair 1", "Hair 2", "Hair 3" };

		public bool CanDrawHostContent => true;

		internal bool MouseOverCompanion => shell.MouseOverCompanion;

		internal MakeUpPage(MonoBehaviour host)
		{
			this.host = host;
			homeHistory = new SharedMakeUpHomeHistory<KoiHomeSnapshot>(() => new KoiHomeSnapshot(session, adapter), delegate(KoiHomeSnapshot snapshot)
			{
				snapshot.Apply(session, adapter);
			});
		}

		public void Draw()
		{
			session = MakeUpSession.Resolve(session, state);
			if (clothesHidden && (session == null || session.Character != clothesCharacter))
			{
				RestoreClothes();
			}
			if (session != null)
			{
				EnsureHistoryCharacter();
			}
			if (Event.current != null && Event.current.type == EventType.MouseUp)
			{
				activeEditKey = null;
			}
			List<SharedMakeUpSection> list = new List<SharedMakeUpSection>();
			foreach (KoiMakeUpSection allSection in KoiMakeUpSchema.AllSections)
			{
				list.Add(new SharedMakeUpSection(allSection.Key, allSection.Label, allSection.Page, allSection.Icon));
			}
			shell.Draw(session != null, list, DrawSection, DrawHome, RestoreClothes);
			if (session != null && shell.Tab != 3)
			{
				DrawBottomBar(shell.Tab == 1);
			}
			else if (session != null)
			{
				SharedMakeUpEditBars.DrawHomeBar(MakeUpLang.T, homeHistory.UndoCount, homeHistory.RedoCount, homeHistory.HasInitial, delegate
				{
					HomeHistoryAction(homeHistory.Undo);
				}, delegate
				{
					HomeHistoryAction(homeHistory.ResetAppearance);
				}, delegate
				{
					HomeHistoryAction(homeHistory.Redo);
				});
			}
		}

		private void DrawSection(SharedMakeUpSection selected)
		{
			if (session == null)
			{
				return;
			}
			KoiMakeUpSection koiMakeUpSection = KoiMakeUpSchema.AllSections.First((KoiMakeUpSection section) => section.Key == selected.Key);
			DrawSectionTopBar(koiMakeUpSection, selected.Page == 1);
			if (MakeUpPairScopeCore.IsPairedSection(selected.Key))
			{
				pairScope = SharedMakeUpShell.PairScope(MakeUpLang.T, pairScope);
			}
			SharedMakeUpShell.Header(MakeUpLang.T(selected.Label));
			if (selected.Key.Contains(".overlay."))
			{
				DrawOverlay(selected.Key);
				return;
			}
			int[] shapeIndices = koiMakeUpSection.ShapeIndices;
			for (int num = 0; num < shapeIndices.Length; num++)
			{
				int index = shapeIndices[num];
				bool face = selected.Page == 0;
				float[] array = (face ? session.Character.fileFace.shapeValueFace : session.Character.fileBody.shapeValueBody);
				if (index < 0 || index >= array.Length)
				{
					continue;
				}
				float num2 = (face ? array[index] : adapter.ReadBodyShape(session, index));
				float next = SharedMakeUpShell.Slider(MakeUpLang.T(KoiMakeUpSchema.ShapeLabel(face, index)), num2, delegate
				{
					ResetShape(face, index);
				});
				if (Mathf.Approximately(next, num2))
				{
					continue;
				}
				ExecuteEdit(!face, "shape:" + index, delegate
				{
					if (face)
					{
						adapter.SetFaceShape(session, index, next);
					}
					else
					{
						adapter.SetBodyShape(session, index, next);
					}
				});
			}
			string[] array2 = SelectorsFor(selected.Key);
			foreach (string key in array2)
			{
				DrawSelector(selected.Key, key);
			}
			array2 = ColorsFor(selected.Key);
			foreach (string key2 in array2)
			{
				DrawColor(selected.Key, key2);
			}
		}

		private void DrawOverlay(string key)
		{
			if (!KoiOverlayProvider.Available(session.Character))
			{
				GUILayout.Label(MakeUpLang.T("Overlay Mods is not installed."));
				return;
			}
			Texture2D texture2D = KoiOverlayProvider.Get(session.Character, key);
			bool enabled = GUI.enabled;
			GUILayout.BeginHorizontal();
			SharedOverlayPreview.Draw(texture2D);
			GUILayout.BeginVertical();
			try
			{
				GUILayout.Label(MakeUpLang.T((texture2D == null) ? "(empty)" : "(loaded)"));
				if (GUILayout.Button(MakeUpLang.T("Load")))
				{
					MakeUpSession captured = session;
					long generation = captured.SelectionGeneration;
					OpenFileDialog.Show((Action<string[]>)delegate(string[] paths)
					{
						if (paths != null && paths.Length != 0)
						{
							string path2 = paths[0];
							MakeUpPlugin.EnqueueMainThread(delegate
							{
								if (session != captured || !captured.IsCurrentSelection(captured.Character, generation))
								{
									return;
								}
								try
								{
									byte[] bytes = File.ReadAllBytes(path2);
									ExecuteEdit(IsBodyKey(key), null, delegate
									{
										KoiOverlayProvider.Apply(captured.Character, key, bytes);
									});
								}
								catch (Exception ex2)
								{
									editStatus = ex2.GetBaseException().Message;
								}
							});
						}
					}, MakeUpLang.T("Open overlay image"), Path.Combine(Paths.GameRootPath, "UserData"), "PNG image (*.png)|*.png", "png", (OpenSaveFileDialgueFlags)2625544);
				}
				GUI.enabled = enabled && texture2D != null;
				if (GUILayout.Button(MakeUpLang.T("Clear")))
				{
					ExecuteEdit(IsBodyKey(key), null, delegate
					{
						KoiOverlayProvider.Apply(session.Character, key, null);
					});
				}
				if (!GUILayout.Button(MakeUpLang.T("Export")))
				{
					return;
				}
				try
				{
					byte[] array = KoiOverlayProvider.Capture(session.Character, key);
					if (array != null)
					{
						string text = Path.Combine(Paths.GameRootPath, "UserData/Overlays");
						Directory.CreateDirectory(text);
						string path = Path.Combine(text, key.Replace('.', '_') + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png");
						using (FileStream fileStream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
						{
							fileStream.Write(array, 0, array.Length);
						}
						editStatus = "Exported " + Path.GetFileName(path);
					}
				}
				catch (Exception ex)
				{
					editStatus = ex.GetBaseException().Message;
				}
			}
			finally
			{
				GUI.enabled = enabled;
				GUILayout.EndVertical();
				GUILayout.EndHorizontal();
			}
		}

		private void DrawSelector(string sectionKey, string key)
		{
			int scope = ((pairScope != 0) ? pairScope : ((!sectionKey.EndsWith(".right", StringComparison.Ordinal)) ? 1 : 2));
			string key2 = MakeUpPairScopeCore.RouteKey(sectionKey, key, scope);
			KoiAppearanceSpec koiAppearanceSpec = KoiAppearanceCatalog.Find(key2);
			if (koiAppearanceSpec == null)
			{
				return;
			}
			IList<MakeUpSelectorOption> list;
			try
			{
				list = adapter.Options(session, key2, moddedOnly: false);
			}
			catch
			{
				return;
			}
			List<SharedMakeUpOption> list2 = new List<SharedMakeUpOption>();
			foreach (MakeUpSelectorOption item in list)
			{
				MakeUpSelectorOption captured = item;
				list2.Add(new SharedMakeUpOption(item.Id, item.Name, () => MakeUpCleanupThumbnail.Load(captured)));
			}
			selectorOpen.TryGetValue(key, out var value);
			selectorScroll.TryGetValue(key, out var value2);
			int num = adapter.ReadId(session, key2);
			int after = SharedMakeUpShell.Selector(MakeUpLang.T(koiAppearanceSpec.Label), list2, num, ref value, ref value2, delegate
			{
				ResetSelector(sectionKey, key);
			});
			selectorOpen[key] = value;
			selectorScroll[key] = value2;
			if (after == num)
			{
				return;
			}
			ExecuteEdit(IsBodyKey(key), null, delegate
			{
				foreach (string item2 in MakeUpPairScopeCore.TargetKeys(sectionKey, key, pairScope))
				{
					adapter.ApplyId(session, item2, after);
				}
			});
		}

		private void DrawColor(string sectionKey, string key)
		{
			int scope = ((pairScope != 0) ? pairScope : ((!sectionKey.EndsWith(".right", StringComparison.Ordinal)) ? 1 : 2));
			string text = MakeUpPairScopeCore.RouteColor(key, scope);
			Color color;
			try
			{
				color = adapter.ReadColor(session, text);
			}
			catch
			{
				return;
			}
			if (!SharedMakeUpShell.Swatch(MakeUpLang.T(text), color, delegate
			{
				ResetColor(key);
			}))
			{
				return;
			}
			bool body = shell.Tab == 1;
			MakeUpSession capturedSession = session;
			long generation = session.SelectionGeneration;
			string editKey = "palette:" + Guid.NewGuid().ToString();
			IList<string> targets = MakeUpPairScopeCore.TargetColors(key, pairScope);
			MakeUpPalette.Open(text, color, alpha: true, delegate(Color value)
			{
				if (session == capturedSession && capturedSession.IsCurrentSelection(capturedSession.Character, generation))
				{
					ExecuteEdit(body, editKey, delegate
					{
						for (int i = 0; i < targets.Count; i++)
						{
							adapter.ApplyColor(capturedSession, targets[i], value);
						}
					});
				}
			});
		}

		private void ResetShape(bool face, int index)
		{
			KoiSnapshot snapshot = (face ? initialHead : initialBody);
			if (snapshot != null)
			{
				ExecuteEdit(!face, null, delegate
				{
					snapshot.RestoreShape(session, adapter, face, index);
				});
			}
		}

		private void ResetSelector(string sectionKey, string key)
		{
			bool flag = IsBodyKey(key);
			KoiSnapshot snapshot = (flag ? initialBody : initialHead);
			if (snapshot != null)
			{
				ExecuteEdit(flag, null, delegate
				{
					snapshot.RestoreIds(session, adapter, MakeUpPairScopeCore.TargetKeys(sectionKey, key, pairScope));
				});
			}
		}

		private void ResetColor(string key)
		{
			bool flag = shell.Tab == 1;
			KoiSnapshot snapshot = (flag ? initialBody : initialHead);
			if (snapshot != null)
			{
				ExecuteEdit(flag, null, delegate
				{
					snapshot.RestoreColors(session, adapter, MakeUpPairScopeCore.TargetColors(key, pairScope));
				});
			}
		}

		private void EnsureHistoryCharacter()
		{
			int instanceID = session.Character.GetInstanceID();
			if (instanceID != historyCharacterId)
			{
				historyCharacterId = instanceID;
				headHistory.Clear();
				bodyHistory.Clear();
				activeEditKey = null;
				editStatus = null;
				pairScope = 0;
				if (initialHead != null)
				{
					initialHead.Dispose();
				}
				if (initialBody != null)
				{
					initialBody.Dispose();
				}
				initialHead = CaptureWhole(body: false);
				initialBody = CaptureWhole(body: true);
			}
		}

		private void ExecuteEdit(bool body, string key, Action action)
		{
			string text = ((key == null) ? null : ((body ? "body:" : "head:") + key));
			try
			{
				(body ? bodyHistory : headHistory).Execute(CaptureWhole(body), action, delegate(KoiSnapshot snapshot)
				{
					snapshot.Apply(session, adapter, restoreCompatibilityState: true);
				}, text == null || activeEditKey != text);
				activeEditKey = text;
			}
			catch (Exception ex)
			{
				activeEditKey = null;
				editStatus = "Edit failed: " + ex.Message;
			}
		}

		private void DrawSectionTopBar(KoiMakeUpSection section, bool body)
		{
			string shortLabel = MakeUpInteractionLabel(section.Key, section.Label);
			KoiSnapshot copied;
			bool canPaste = sectionClipboard.TryPaste(section.Key, out copied);
			SharedMakeUpEditBars.DrawSectionToolbar(MakeUpLang.T, shortLabel, body, clothesHidden, canPaste, delegate
			{
				if (clothesHidden)
				{
					RestoreClothes();
				}
				else
				{
					HideClothes();
				}
			}, delegate
			{
				try
				{
					KoiSnapshot koiSnapshot = CaptureSection(section);
					sectionClipboard.Copy(section.Key, koiSnapshot);
					editStatus = shortLabel + ((koiSnapshot.Providers != null || section.Key.Contains(".overlay.")) ? " copied." : " copied. Custom material/bone data is not included for this page.");
				}
				catch (Exception ex)
				{
					editStatus = "Copy failed; previous clipboard kept: " + ex.Message;
				}
			}, delegate
			{
				PasteSection(copied, body, shortLabel, section.Key);
			});
		}

		private void PasteSection(KoiSnapshot copied, bool body, string label, string sectionKey)
		{
			try
			{
				(body ? bodyHistory : headHistory).Execute(CaptureWhole(body), delegate
				{
					copied.Apply(session, adapter);
					if (body)
					{
						WardrobePseudoMakerState.InvalidateBodyValues(session.Character, copied.bodyShapes.Keys, new int[0], new int[0]);
					}
					else
					{
						WardrobePseudoMakerState.InvalidateFaceValues(session.Character, copied.faceShapes.Keys, new int[0], new int[0]);
					}
					WardrobePseudoMakerState.InvalidateColorLabels(session.Character, ColorsFor(sectionKey));
				}, delegate(KoiSnapshot snapshot)
				{
					snapshot.Apply(session, adapter, restoreCompatibilityState: true);
				});
				editStatus = label + " pasted.";
			}
			catch (Exception ex)
			{
				editStatus = "Paste failed: " + ex.Message;
			}
		}

		private void DrawBottomBar(bool body)
		{
			SharedMakeUpHistory<KoiSnapshot> sharedMakeUpHistory = (body ? bodyHistory : headHistory);
			SharedMakeUpEditBars.DrawBottomBar(MakeUpLang.T, body, sharedMakeUpHistory.UndoCount, sharedMakeUpHistory.RedoCount, body ? (bodyClipboard != null) : (headClipboard != null), body ? (initialBody != null) : (initialHead != null), editStatus, delegate
			{
				StepHistory(body, forward: false);
			}, delegate
			{
				CopyWhole(body);
			}, delegate
			{
				RestoreSnapshot(body ? initialBody : initialHead, body, "Reset");
			}, delegate
			{
				RestoreSnapshot(body ? bodyClipboard : headClipboard, body, body ? "Body pasted." : "Head (including hair) pasted.");
			}, delegate
			{
				StepHistory(body, forward: true);
			});
		}

		private void StepHistory(bool body, bool forward)
		{
			activeEditKey = null;
			SharedMakeUpHistory<KoiSnapshot> sharedMakeUpHistory = (body ? bodyHistory : headHistory);
			try
			{
				if (forward ? sharedMakeUpHistory.ApplyRedo(CaptureWhole(body), delegate(KoiSnapshot snapshot)
				{
					snapshot.Apply(session, adapter, restoreCompatibilityState: true);
				}) : sharedMakeUpHistory.ApplyUndo(CaptureWhole(body), delegate(KoiSnapshot snapshot)
				{
					snapshot.Apply(session, adapter, restoreCompatibilityState: true);
				}))
				{
					editStatus = (forward ? "Redo" : "Undo");
				}
			}
			catch (Exception ex)
			{
				editStatus = (forward ? "Redo" : "Undo") + " failed: " + ex.Message;
			}
		}

		private void CopyWhole(bool body)
		{
			try
			{
				KoiSnapshot koiSnapshot = CaptureWhole(body);
				if (body)
				{
					if (bodyClipboard != null)
					{
						bodyClipboard.Dispose();
					}
					bodyClipboard = koiSnapshot;
				}
				else
				{
					if (headClipboard != null)
					{
						headClipboard.Dispose();
					}
					headClipboard = koiSnapshot;
				}
				editStatus = (body ? "Body copied." : "Head (including hair) copied.");
			}
			catch (Exception ex)
			{
				editStatus = "Copy failed; previous clipboard kept: " + ex.Message;
			}
		}

		private void RestoreSnapshot(KoiSnapshot target, bool body, string label)
		{
			activeEditKey = null;
			try
			{
				(body ? bodyHistory : headHistory).Execute(CaptureWhole(body), delegate
				{
					target.Apply(session, adapter);
					if (body)
					{
						WardrobePseudoMakerState.InvalidateBody(session.Character);
					}
					else
					{
						WardrobePseudoMakerState.InvalidateHead(session.Character);
					}
				}, delegate(KoiSnapshot snapshot)
				{
					snapshot.Apply(session, adapter, restoreCompatibilityState: true);
				});
				editStatus = label;
			}
			catch (Exception ex)
			{
				editStatus = label + " failed: " + ex.Message;
			}
		}

		private KoiSnapshot CaptureWhole(bool body)
		{
			KoiSnapshot koiSnapshot = KoiSnapshot.Capture(session, adapter, KoiMakeUpSchema.AllSections.Where((KoiMakeUpSection section) => (!body) ? (section.Page == 0 || section.Page == 2) : (section.Page == 1)));
			koiSnapshot.NativeRegion = new KoiNativeAppearanceSnapshot(session.Character);
			koiSnapshot.Providers = new KoiProviderSnapshot(session.Character, body ? 1 : 0);
			koiSnapshot.BodyRegion = body;
			koiSnapshot.HistoryOwner = session.Character;
			koiSnapshot.HistoryCompatibilityState = WardrobePseudoMakerState.Capture(session.Character);
			return koiSnapshot;
		}

		private KoiSnapshot CaptureSection(KoiMakeUpSection section)
		{
			KoiSnapshot koiSnapshot = KoiSnapshot.Capture(session, adapter, new KoiMakeUpSection[1] { section });
			koiSnapshot.Providers = KoiProviderSnapshot.CaptureSection(session.Character, section.Key);
			return koiSnapshot;
		}

		private static bool IsBodyKey(string key)
		{
			return key?.StartsWith("body.", StringComparison.Ordinal) ?? false;
		}

		private static string MakeUpInteractionLabel(string key, string label)
		{
			return key switch
			{
				"face.eye.left" => "L Eye", 
				"face.eye.right" => "R Eye", 
				"face.shape.whole" => "Face Shape", 
				"face.type_skin" => "Face Skin", 
				"head.hair.settings" => "Settings", 
				_ => (label ?? string.Empty).Replace(" Shape", string.Empty).Replace(" Makeup", string.Empty), 
			};
		}

		private static string[] SelectorsFor(string section)
		{
			return section switch
			{
				"face.type_skin" => new string[2] { "face.head", "face.detail" }, 
				"face.mole" => new string[1] { "face.mole" }, 
				"face.eye.left" => new string[1] { "face.eye.left" }, 
				"face.eye.right" => new string[1] { "face.eye.right" }, 
				"face.eye.settings" => new string[4] { "face.eye_hi_up", "face.eye_hi_down", "face.eyeline_up", "face.eyeline_down" }, 
				"face.eyebrows" => new string[1] { "face.eyebrow" }, 
				"face.makeup.eyeshadow" => new string[1] { "face.eyeshadow" }, 
				"face.makeup.cheek" => new string[1] { "face.cheek" }, 
				"face.makeup.lips" => new string[2] { "face.lip", "face.lipline" }, 
				"face.paint.1" => new string[1] { "face.paint.0" }, 
				"face.paint.2" => new string[1] { "face.paint.1" }, 
				"body.skin" => new string[1] { "body.detail" }, 
				"body.sunburn" => new string[1] { "body.sunburn" }, 
				"body.nipples" => new string[1] { "body.nip" }, 
				"body.underhair" => new string[1] { "body.underhair" }, 
				"body.paint.1" => new string[1] { "body.paint.0" }, 
				"body.paint.2" => new string[1] { "body.paint.1" }, 
				"head.hair.back" => new string[1] { "hair.back" }, 
				"head.hair.front" => new string[1] { "hair.front" }, 
				"head.hair.side" => new string[1] { "hair.side" }, 
				"head.hair.option" => new string[1] { "hair.option" }, 
				_ => new string[0], 
			};
		}

		private static string[] ColorsFor(string section)
		{
			return section switch
			{
				"face.type_skin" => new string[2] { "Skin Main", "Skin Sub" }, 
				"face.mole" => new string[1] { "Mole" }, 
				"face.eye.left" => new string[1] { "Left Eye" }, 
				"face.eye.right" => new string[1] { "Right Eye" }, 
				"face.eye.settings" => new string[4] { "Eye Whites", "Upper Highlight", "Lower Highlight", "Eyeline" }, 
				"face.eyebrows" => new string[1] { "Eyebrows" }, 
				"face.makeup.eyeshadow" => new string[1] { "Eyeshadow" }, 
				"face.makeup.cheek" => new string[1] { "Cheek" }, 
				"face.makeup.lips" => new string[2] { "Lip", "Lip Line" }, 
				"face.paint.1" => new string[1] { "Face Paint 1" }, 
				"face.paint.2" => new string[1] { "Face Paint 2" }, 
				"body.skin" => new string[2] { "Skin Main", "Skin Sub" }, 
				"body.sunburn" => new string[1] { "Sunburn" }, 
				"body.nipples" => new string[1] { "Nipples" }, 
				"body.underhair" => new string[1] { "Underhair" }, 
				"body.nails" => new string[1] { "Nails" }, 
				"body.paint.1" => new string[1] { "Body Paint 1" }, 
				"body.paint.2" => new string[1] { "Body Paint 2" }, 
				"head.hair.back" => UniversalHairColors, 
				"head.hair.front" => UniversalHairColors, 
				"head.hair.side" => UniversalHairColors, 
				"head.hair.option" => UniversalHairColors, 
				_ => new string[0], 
			};
		}

		private void DrawClothesToggle()
		{
			GUILayout.BeginHorizontal();
			if (GUILayout.Button(MakeUpLang.T(clothesHidden ? "Clothes Hidden" : "Hide Clothes"), GUILayout.Height(28f)))
			{
				if (clothesHidden)
				{
					RestoreClothes();
				}
				else
				{
					HideClothes();
				}
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
		}

		private void HideClothes()
		{
			ChaControl character = session.Character;
			clothesBefore = new byte[character.fileStatus.clothesState.Length];
			clothesCharacter = character;
			Array.Copy(character.fileStatus.clothesState, clothesBefore, clothesBefore.Length);
			for (int i = 0; i < clothesBefore.Length; i++)
			{
				if (i != 7 && i != 8 && character.fileStatus.clothesState[i] != 2)
				{
					character.SetClothesState(i, 2);
				}
			}
			clothesHidden = true;
		}

		private void RestoreClothes()
		{
			if (clothesHidden && clothesCharacter != null && clothesBefore != null)
			{
				for (int i = 0; i < Math.Min(clothesBefore.Length, clothesCharacter.fileStatus.clothesState.Length); i++)
				{
					if (clothesCharacter.fileStatus.clothesState[i] != clothesBefore[i])
					{
						clothesCharacter.SetClothesState(i, clothesBefore[i]);
					}
				}
			}
			clothesHidden = false;
			clothesBefore = null;
			clothesCharacter = null;
		}

		private void DrawHome()
		{
			try
			{
				homeHistory.EnsureCharacter((session != null) ? session.Character.GetInstanceID() : 0);
			}
			catch (Exception error)
			{
				ReportHomeFailure("History unavailable", error);
			}
			EnsureCards();
			SharedMakeUpHomeModel model = new SharedMakeUpHomeModel
			{
				Translate = MakeUpLang.T,
				ReferencePreview = Thumbnail(referencePath),
				SnapshotPreview = snapshotPreview,
				ReferenceCaption = (string.IsNullOrEmpty(referencePath) ? null : Path.GetFileNameWithoutExtension(referencePath)),
				HasCharacter = (session != null),
				HasReference = (referenceCard != null),
				InsideStudio = StudioAPI.InsideStudio,
				HasSnapshot = (snapshotPng != null),
				OpenReference = delegate
				{
					homeBrowserOpen = !homeBrowserOpen;
					if (homeBrowserOpen)
					{
						referenceBrowser.Refresh();
					}
				},
				LoadReference = LoadReferenceIntoStudio,
				CopyAll = delegate
				{
					HomeEdit(CopyAll);
				},
				ImportHead = delegate
				{
					HomeEdit(ImportHead);
				},
				ImportBody = delegate
				{
					HomeEdit(ImportBody);
				},
				ImportMakeup = delegate
				{
					HomeEdit(ImportMakeup);
				},
				ImportClothes = delegate
				{
					HomeEdit(delegate
					{
						ImportCoordinate(accessoriesOnly: false);
					});
				},
				ImportAccessories = delegate
				{
					HomeEdit(delegate
					{
						ImportCoordinate(accessoriesOnly: true);
					});
				},
				Snapshot = delegate
				{
					host.StartCoroutine(CaptureSnapshotPreview());
				},
				CopyReferencePortrait = CopyReferencePortrait,
				SaveCharacter = SaveCharacter
			};
			if (!string.IsNullOrEmpty(homeStatus))
			{
				GUILayout.Label(homeStatus);
			}
			SharedMakeUpHomeView.Draw(model);
			if (homeBrowserOpen)
			{
				DrawCardBrowser();
			}
		}

		private void DrawCardBrowser()
		{
			referenceBrowser.Draw(MakeUpLang.T, referencePath, Thumbnail, SelectReference);
		}

		private void HomeEdit(Action action)
		{
			try
			{
				homeHistory.Execute(action);
			}
			catch (Exception error)
			{
				ReportHomeFailure("Home edit failed", error);
			}
		}

		private void HomeHistoryAction(Action action)
		{
			try
			{
				action();
				homeStatus = "History applied. Undo: " + homeHistory.UndoCount + ", Redo: " + homeHistory.RedoCount;
			}
			catch (Exception error)
			{
				ReportHomeFailure("History restore failed", error);
			}
		}

		private void ReportHomeFailure(string operation, Exception error)
		{
			string text = operation + ": " + error.Message + " (" + error.GetBaseException().Message + ")";
			_ = homeStatus != text;
			homeStatus = text;
		}

		private static GUIStyle CenteredLabel(bool bold)
		{
			return new GUIStyle(GUI.skin.label)
			{
				alignment = TextAnchor.MiddleCenter,
				fontStyle = (bold ? FontStyle.Bold : FontStyle.Normal),
				clipping = TextClipping.Clip
			};
		}

		private void EnsureCards()
		{
			if (!homeCardsLoaded)
			{
				homeCardsLoaded = true;
				string root = Path.Combine(Path.Combine(Paths.GameRootPath, "UserData"), "chara");
				string lastReferencePath = MakeUpPlugin.LastReferencePath;
				referenceBrowser.Initialize(root, lastReferencePath);
				if (!string.IsNullOrEmpty(lastReferencePath) && File.Exists(lastReferencePath))
				{
					SelectReference(lastReferencePath);
				}
				else
				{
					homeBrowserOpen = true;
				}
			}
		}

		private void SelectReference(string path)
		{
			try
			{
				ChaFileControl chaFileControl = new ChaFileControl();
				if (!chaFileControl.LoadCharaFile(path, SexFromPath(path)))
				{
					throw new InvalidOperationException("The card could not be read.");
				}
				referencePath = path;
				referenceCard = chaFileControl;
				homeBrowserOpen = false;
				MakeUpPlugin.RememberReferencePath(path);
				homeStatus = null;
			}
			catch (Exception ex)
			{
				homeStatus = MakeUpLang.T("Reference could not be loaded: ") + ex.Message;
			}
		}

		private Texture2D Thumbnail(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return null;
			}
			if (homeThumbnails.TryGetValue(path, out var value))
			{
				return value;
			}
			try
			{
				value = new Texture2D(2, 2);
				value.LoadImage(File.ReadAllBytes(path));
				homeThumbnails[path] = value;
				return value;
			}
			catch
			{
				return null;
			}
		}

		private void LoadReferenceIntoStudio()
		{
			try
			{
				global::Studio.Studio studio = Singleton<global::Studio.Studio>.Instance;
				SharedReferenceScenePlacement.Load(studio, delegate
				{
					if (referenceCard.parameter.sex == 0)
					{
						studio.AddMale(referencePath);
					}
					else
					{
						studio.AddFemale(referencePath);
					}
				});
				homeStatus = MakeUpLang.T("Reference loaded into Studio.");
			}
			catch (Exception ex)
			{
				homeStatus = ex.Message;
			}
		}

		private void CopyAll()
		{
			try
			{
				SharedMakeUpCardSnapshot.Restore(session.Character, File.ReadAllBytes(referencePath));
				historyCharacterId = 0;
				homeStatus = MakeUpLang.T("Character replaced from reference.");
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException("Copy All failed: " + ex.Message, ex);
			}
		}

		private void ImportHead()
		{
			RequireCompatibleReference();
			try
			{
				Dictionary<string, byte[]> overlays = KoiOverlayProvider.ReadCardRegion(session.Character, referenceCard, body: false);
				KoiProviderSnapshot.ApplyCardMaterials(session.Character, referenceCard, 0, delegate
				{
					KoiNativeAppearanceSnapshot.ApplyCustom(session.Character, KoiNativeAppearanceSnapshot.CloneCustom(referenceCard.custom), face: true, body: false, hair: true);
					KoiOverlayProvider.ApplyRegion(session.Character, overlays);
				}, (int slot) => (slot >= 0) ? (session.Character.fileHair.parts[slot].id != referenceCard.custom.hair.parts[slot].id) : (session.Character.fileFace.headId != referenceCard.custom.face.headId));
				KoiProviderSnapshot.ApplyCardBones(session.Character, referenceCard, 0);
				WardrobePseudoMakerState.InvalidateHead(session.Character);
				homeStatus = MakeUpLang.T("Reference head imported.");
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException("Reference head import failed: " + ex.Message, ex);
			}
		}

		private void ImportBody()
		{
			RequireCompatibleReference();
			try
			{
				PluginData data = KoiPushupCompatibility.CaptureCardBody(referenceCard);
				Dictionary<string, byte[]> overlays = KoiOverlayProvider.ReadCardRegion(session.Character, referenceCard, body: true);
				KoiProviderSnapshot.ApplyCardMaterials(session.Character, referenceCard, 1, delegate
				{
					KoiNativeAppearanceSnapshot.ApplyCustom(session.Character, KoiNativeAppearanceSnapshot.CloneCustom(referenceCard.custom), face: false, body: true, hair: false);
					KoiOverlayProvider.ApplyRegion(session.Character, overlays);
				});
				KoiProviderSnapshot.ApplyCardBones(session.Character, referenceCard, 1);
				KoiPushupCompatibility.ApplyBody(session.Character, data);
				WardrobePseudoMakerState.InvalidateBody(session.Character);
				homeStatus = MakeUpLang.T("Reference body imported.");
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException("Reference body import failed: " + ex.Message, ex);
			}
		}

		private void ImportMakeup()
		{
			RequireCompatibleReference();
			try
			{
				Dictionary<string, byte[]> values = KoiOverlayProvider.ReadCardRegion(session.Character, referenceCard, body: false);
				ChaFileCustom chaFileCustom = KoiNativeAppearanceSnapshot.CloneCustom(session.Character.chaFile.custom);
				chaFileCustom.face.baseMakeup = CloneMakeup(referenceCard.custom.face.baseMakeup);
				KoiNativeAppearanceSnapshot.ApplyCustom(session.Character, chaFileCustom, face: true, body: false, hair: false);
				int num = Math.Max(0, Math.Min(referenceCard.coordinate.Length - 1, session.Character.fileStatus.coordinateType));
				session.Character.nowCoordinate.makeup = CloneMakeup(referenceCard.coordinate[num].makeup);
				session.Character.nowCoordinate.enableMakeup = referenceCard.coordinate[num].enableMakeup;
				KoiNativeAppearanceSnapshot.RefreshMakeup(session.Character);
				KoiOverlayProvider.ApplyRegion(session.Character, values);
				WardrobePseudoMakerState.InvalidateColorLabels(session.Character, new string[3] { "Eyeshadow", "Cheek", "Lip" });
				homeStatus = MakeUpLang.T("Reference makeup imported.");
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException("Reference makeup import failed: " + ex.Message, ex);
			}
		}

		private void ImportCoordinate(bool accessoriesOnly)
		{
			try
			{
				int num = Math.Max(0, Math.Min(referenceCard.coordinate.Length - 1, session.Character.fileStatus.coordinateType));
				if (!Chainloader.PluginInfos.TryGetValue("com.freelancer604.wardrobe", out var value) || (UnityEngine.Object)(object)value.Instance == null)
				{
					throw new InvalidOperationException("Vanity's outfit engine is not available.");
				}
				Type type = ((object)value.Instance).GetType().Assembly.GetType("Freelancer604.Wardrobe.WardrobeSession");
				MethodInfo obj = ((type == null) ? null : type.GetMethod("ImportReferenceOutfit", BindingFlags.Static | BindingFlags.Public));
				if (obj == null)
				{
					throw new InvalidOperationException("Update Vanity's Dress-Up DLL together with Make-Up.");
				}
				obj.Invoke(null, new object[4] { session.Character, referenceCard, num, accessoriesOnly });
				homeStatus = MakeUpLang.T(accessoriesOnly ? "Reference accessories imported." : "Reference clothes imported.");
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException("Reference outfit import failed: " + (ex.InnerException ?? ex).Message, ex);
			}
		}

		private static ChaFileMakeup CloneMakeup(ChaFileMakeup source)
		{
			return new ChaFileMakeup
			{
				eyeshadowId = source.eyeshadowId,
				eyeshadowColor = source.eyeshadowColor,
				cheekId = source.cheekId,
				cheekColor = source.cheekColor,
				lipId = source.lipId,
				lipColor = source.lipColor,
				paintId = (int[])source.paintId.Clone(),
				paintColor = (Color[])source.paintColor.Clone(),
				paintLayout = (Vector4[])source.paintLayout.Clone()
			};
		}

		private void RequireCompatibleReference()
		{
			if (referenceCard == null || referenceCard.parameter.sex != session.Character.sex)
			{
				throw new InvalidOperationException("Choose a reference card of the same sex as the selected character.");
			}
		}

		private IEnumerator CaptureSnapshotPreview()
		{
			yield return new WaitForEndOfFrame();
			Camera camera = Camera.main;
			if (camera == null)
			{
				camera = UnityEngine.Object.FindObjectOfType<Camera>();
			}
			if (camera == null)
			{
				homeStatus = MakeUpLang.T("Snapshot failed: no camera was found.");
				yield break;
			}
			int height = Screen.height;
			int num = Math.Min(Screen.width, (int)((float)height * (63f / 88f)));
			int num2 = Math.Max(0, (Screen.width - num) / 2);
			RenderTexture renderTexture = new RenderTexture(Screen.width, Screen.height, 24);
			RenderTexture targetTexture = camera.targetTexture;
			RenderTexture active = RenderTexture.active;
			Texture2D texture2D = null;
			try
			{
				camera.targetTexture = renderTexture;
				camera.Render();
				RenderTexture.active = renderTexture;
				texture2D = new Texture2D(num, height, TextureFormat.RGB24, mipChain: false);
				texture2D.ReadPixels(new Rect(num2, 0f, num, height), 0, 0);
				texture2D.Apply();
				snapshotPng = EncodePng(texture2D);
				ReplaceSnapshotPreview(texture2D);
				texture2D = null;
				homeStatus = MakeUpLang.T("Snapshot taken!");
			}
			catch (Exception ex)
			{
				homeStatus = MakeUpLang.T("Snapshot failed: ") + ex.Message;
			}
			finally
			{
				camera.targetTexture = targetTexture;
				RenderTexture.active = active;
				UnityEngine.Object.Destroy(renderTexture);
				if (texture2D != null)
				{
					UnityEngine.Object.Destroy(texture2D);
				}
			}
		}

		private void CopyReferencePortrait()
		{
			if (referenceCard == null || referenceCard.pngData == null)
			{
				homeStatus = MakeUpLang.T("Reference portrait could not be read.");
				return;
			}
			snapshotPng = (byte[])referenceCard.pngData.Clone();
			Texture2D texture2D = new Texture2D(2, 2);
			if (!LoadPng(texture2D, snapshotPng))
			{
				UnityEngine.Object.Destroy(texture2D);
				snapshotPng = null;
				homeStatus = MakeUpLang.T("Reference portrait could not be read.");
			}
			else
			{
				ReplaceSnapshotPreview(texture2D);
				homeStatus = MakeUpLang.T("Reference portrait copied to Snapshot.");
			}
		}

		private void ReplaceSnapshotPreview(Texture2D texture)
		{
			if (snapshotPreview != null)
			{
				UnityEngine.Object.Destroy(snapshotPreview);
			}
			snapshotPreview = texture;
		}

		private static byte[] EncodePng(Texture2D texture)
		{
			return texture.EncodeToPNG();
		}

		private static bool LoadPng(Texture2D texture, byte[] png)
		{
			return texture.LoadImage(png);
		}

		private void SaveCharacter()
		{
			try
			{
				string path = ((session.Character.sex == 0) ? "male" : "female");
				string text = Path.Combine(Path.Combine(Path.Combine(Paths.GameRootPath, "UserData"), "chara"), path);
				Directory.CreateDirectory(text);
				string text2 = Path.Combine(text, "F604MakeUp_" + DateTime.Now.ToString("yyyyMMdd_HHmmssfff") + ".png");
				byte[] pngData = session.Character.chaFile.pngData;
				byte[] pngData2 = ((session.Character.nowCoordinate == null) ? null : session.Character.nowCoordinate.pngData);
				try
				{
					session.Character.chaFile.pngData = snapshotPng;
					if (session.Character.nowCoordinate != null)
					{
						session.Character.nowCoordinate.pngData = snapshotPng;
					}
					using (KoiMakeUpCardSaveStage.Create(session.Character))
					{
						using (MakeUpBscSaveStage.TryCreate(session.Character))
						{
							if (!session.Character.chaFile.SaveCharaFile(text2, session.Character.sex, newFile: true))
							{
								throw new InvalidOperationException("The game rejected the save.");
							}
						}
					}
				}
				finally
				{
					session.Character.chaFile.pngData = pngData;
					if (session.Character.nowCoordinate != null)
					{
						session.Character.nowCoordinate.pngData = pngData2;
					}
				}
				homeStatus = MakeUpLang.T("Character saved: ") + text2;
				referenceBrowser.Refresh();
			}
			catch (Exception ex)
			{
				homeStatus = MakeUpLang.T("Character save failed: ") + ex.Message;
			}
		}

		private static byte SexFromPath(string path)
		{
			return ((path ?? string.Empty).Replace('/', '\\').IndexOf("\\chara\\male\\", StringComparison.OrdinalIgnoreCase) < 0) ? ((byte)1) : ((byte)0);
		}

		public void OnEnter()
		{
			shell.SetActive(value: true);
			session = MakeUpSession.Resolve(session, state);
		}

		public void OnExit()
		{
			shell.SetActive(value: false);
			activeEditKey = null;
			RestoreClothes();
			if (session != null)
			{
				session.SuspendSelection();
			}
		}

		public void Reset()
		{
			homeHistory.Clear();
			RestoreClothes();
			if (session != null)
			{
				session.InvalidateSelection();
			}
			session = null;
			shell.Reset();
			selectorOpen.Clear();
			selectorScroll.Clear();
			state.Reset();
			headHistory.Clear();
			bodyHistory.Clear();
			sectionClipboard.Clear();
			if (headClipboard != null)
			{
				headClipboard.Dispose();
			}
			if (bodyClipboard != null)
			{
				bodyClipboard.Dispose();
			}
			if (initialHead != null)
			{
				initialHead.Dispose();
			}
			if (initialBody != null)
			{
				initialBody.Dispose();
			}
			headClipboard = (bodyClipboard = (initialHead = (initialBody = null)));
			historyCharacterId = 0;
			activeEditKey = null;
			editStatus = null;
			pairScope = 0;
			snapshotPng = null;
			if (snapshotPreview != null)
			{
				UnityEngine.Object.Destroy(snapshotPreview);
			}
			snapshotPreview = null;
			homeBrowserOpen = referenceCard == null;
		}

		public float GetExtraWindowWidth()
		{
			return 0f;
		}

		public void SetHostWindowRect(float x, float y, float width, float height)
		{
			shell.SetHostRect(x, y, width, height);
		}

		public void SetHostUiMetrics(float scale, float logicalScreenWidth, float logicalScreenHeight)
		{
			MakeUpUiScale.SetHostMetrics(scale, logicalScreenWidth, logicalScreenHeight);
			shell.SetHostMetrics(scale, logicalScreenWidth, logicalScreenHeight);
		}

		public void BeginHostPass()
		{
		}

		internal void DrawCompanion()
		{
			shell.DrawCompanion();
		}

		internal void InvalidateAfterCleanup()
		{
		}
	}
	internal sealed class LegacyOutfitCleanupBackend : ICleanupSourceBackend
	{
		private readonly MethodInfo traceItem;

		private readonly MethodInfo siblings;

		private readonly MethodInfo studioItemCount;

		private readonly MethodInfo terminate;

		private readonly MethodInfo undoLast;

		private readonly MethodInfo isItemBanned;

		private readonly MethodInfo processPendingMoves;

		private readonly MethodInfo resolveOutfitCatalogItem;

		private readonly PropertyInfo pendingCount;

		public int PendingCount => (int)pendingCount.GetValue(null, null);

		private LegacyOutfitCleanupBackend(Type type)
		{
			traceItem = type.GetMethod("TraceItem", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[2]
			{
				typeof(int),
				typeof(int)
			}, null);
			siblings = type.GetMethod("Siblings", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(string) }, null);
			studioItemCount = type.GetMethod("StudioItemCount", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(string) }, null);
			undoLast = type.GetMethod("UndoLast", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
			isItemBanned = type.GetMethod("IsItemBanned", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(int) }, null);
			processPendingMoves = type.GetMethod("ProcessPendingMoves", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
			resolveOutfitCatalogItem = type.GetMethod("ResolveOutfitCatalogItem", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(int) }, null);
			pendingCount = type.GetProperty("PendingCount", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			Type nestedType = type.GetNestedType("CleanupTrace", BindingFlags.Public | BindingFlags.NonPublic);
			terminate = ((nestedType == null) ? null : type.GetMethod("Terminate", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { nestedType }, null));
		}

		internal static bool TryCreate(Assembly assembly, out ICleanupSourceBackend backend)
		{
			backend = null;
			Type type = assembly.GetType("Freelancer604.Wardrobe.WardrobeCleanup", throwOnError: false);
			if (type == null)
			{
				return false;
			}
			LegacyOutfitCleanupBackend legacyOutfitCleanupBackend = new LegacyOutfitCleanupBackend(type);
			if (legacyOutfitCleanupBackend.traceItem == null || legacyOutfitCleanupBackend.siblings == null || legacyOutfitCleanupBackend.studioItemCount == null || legacyOutfitCleanupBackend.terminate == null || legacyOutfitCleanupBackend.undoLast == null || legacyOutfitCleanupBackend.isItemBanned == null || legacyOutfitCleanupBackend.pendingCount == null)
			{
				return false;
			}
			backend = legacyOutfitCleanupBackend;
			MakeUpPlugin.LogMessage("Character Clean-Up is sharing Dress-Up Plus's active quarantine authority.");
			return true;
		}

		public CharacterCleanupTrace Trace(CharacterCleanupChoice choice)
		{
			if (choice == null || !choice.ResolverCategory.HasValue)
			{
				return ConvertTrace(choice?.ItemId ?? (-1), null, null);
			}
			object native = traceItem.Invoke(null, new object[2]
			{
				choice.ItemId,
				choice.ResolverCategory.Value
			});
			return ConvertTrace(choice.ItemId, choice.ResolverCategory, native);
		}

		public bool Terminate(CharacterCleanupTrace trace)
		{
			if (trace == null)
			{
				return false;
			}
			if (!trace.ResolverCategory.HasValue)
			{
				return false;
			}
			object obj = traceItem.Invoke(null, new object[2]
			{
				trace.ItemId,
				trace.ResolverCategory.Value
			});
			if (obj != null)
			{
				return (bool)terminate.Invoke(null, new object[1] { obj });
			}
			return false;
		}

		public CharacterCleanupTrace UndoLast()
		{
			object obj = undoLast.Invoke(null, null);
			if (obj != null)
			{
				return ConvertTrace(0, null, obj);
			}
			return null;
		}

		public bool IsItemBanned(int itemId)
		{
			return (bool)isItemBanned.Invoke(null, new object[1] { itemId });
		}

		public void ProcessPendingMoves()
		{
			if (processPendingMoves != null)
			{
				processPendingMoves.Invoke(null, null);
			}
		}

		internal bool TryResolveOutfitCatalogItem(int itemId, out int resolverCategory, out string name, out string assetBundle, out string assetName)
		{
			resolverCategory = 0;
			name = (assetBundle = (assetName = null));
			if (resolveOutfitCatalogItem == null)
			{
				return false;
			}
			if (!(resolveOutfitCatalogItem.Invoke(null, new object[1] { itemId }) is string[] array) || array.Length < 4 || !int.TryParse(array[0], out resolverCategory))
			{
				return false;
			}
			name = array[1];
			assetBundle = array[2];
			assetName = array[3];
			return true;
		}

		private CharacterCleanupTrace ConvertTrace(int itemId, int? resolverCategory, object native)
		{
			if (native == null)
			{
				return new CharacterCleanupTrace(itemId, resolverCategory, null, null, "Mod trace unavailable — protected.", alreadyBanned: false, null, null, 0);
			}
			Type type = native.GetType();
			string text = Field<string>(type, native, "Guid");
			string sourceName = Field<string>(type, native, "ModName");
			string lockedReason = Field<string>(type, native, "Reason");
			bool alreadyBanned = Field<bool>(type, native, "AlreadyBanned");
			List<string> list = new List<string>();
			IEnumerable enumerable = Field<IEnumerable>(type, native, "Files");
			if (enumerable != null)
			{
				foreach (object item in enumerable)
				{
					if (item != null)
					{
						list.Add(item.ToString());
					}
				}
			}
			List<CharacterCleanupSibling> list2 = new List<CharacterCleanupSibling>();
			if (!string.IsNullOrEmpty(text) && siblings.Invoke(null, new object[1] { text }) is IEnumerable enumerable2)
			{
				foreach (object item2 in enumerable2)
				{
					if (item2 != null)
					{
						Type type2 = item2.GetType();
						list2.Add(new CharacterCleanupSibling(Member<int>(type2, item2, "LocalSlot"), Convert.ToInt32(Member<object>(type2, item2, "CategoryNo")), Member<string>(type2, item2, "Name")));
					}
				}
			}
			int num = ((!string.IsNullOrEmpty(text)) ? ((int)studioItemCount.Invoke(null, new object[1] { text })) : 0);
			return new CharacterCleanupTrace(itemId, resolverCategory, text, sourceName, lockedReason, alreadyBanned, list, list2, num);
		}

		private static T Field<T>(Type type, object instance, string name)
		{
			FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (!(field == null) && field.GetValue(instance) != null)
			{
				return (T)field.GetValue(instance);
			}
			return default(T);
		}

		private static T Member<T>(Type type, object instance, string name)
		{
			FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null && field.GetValue(instance) != null)
			{
				return (T)field.GetValue(instance);
			}
			PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (!(property == null) && property.GetValue(instance, null) != null)
			{
				return (T)property.GetValue(instance, null);
			}
			return default(T);
		}
	}
	internal static class MakeUpCleanupRuntime
	{
		private static ICleanupSourceBackend backend;

		internal static ICleanupSourceBackend Backend => backend;

		internal static void Initialize(ICleanupSourceBackend value)
		{
			backend = value;
		}

		internal static bool IsItemBanned(int itemId)
		{
			if (backend != null)
			{
				return backend.IsItemBanned(itemId);
			}
			return false;
		}

		internal static bool TryResolveOutfitCatalogItem(int itemId, out int resolverCategory, out string name, out string assetBundle, out string assetName)
		{
			if (backend is LegacyOutfitCleanupBackend legacyOutfitCleanupBackend)
			{
				return legacyOutfitCleanupBackend.TryResolveOutfitCatalogItem(itemId, out resolverCategory, out name, out assetBundle, out assetName);
			}
			resolverCategory = 0;
			name = (assetBundle = (assetName = null));
			return false;
		}
	}
	internal static class MakeUpCleanupBackendFactory
	{
		internal static bool TryCreate(PluginInfo host, out ICleanupSourceBackend backend)
		{
			backend = null;
			Assembly assembly = ((host == null || (UnityEngine.Object)(object)host.Instance == null) ? null : ((object)host.Instance).GetType().Assembly);
			if (CleanupAuthorityCompatibility.CanShareWith(assembly) && LegacyOutfitCleanupBackend.TryCreate(assembly, out backend))
			{
				return true;
			}
			return false;
		}
	}
	public sealed class MakeUpCleanupPage
	{
		private sealed class CollateralTile
		{
			internal string CategoryId { get; private set; }

			internal int ResolverCategory { get; private set; }

			internal MakeUpSelectorOption Option { get; private set; }

			internal CollateralTile(string categoryId, int resolverCategory, MakeUpSelectorOption option)
			{
				CategoryId = categoryId;
				ResolverCategory = resolverCategory;
				Option = option;
			}
		}

		private static readonly string[] GroupIds = new string[3] { "face", "body", "hair" };

		private static readonly string[] GroupLabels = new string[3] { "Face", "Body", "Hair" };

		private static GUIStyle centerBold;

		private static GUIStyle centerSmall;

		private static GUIStyle dimSmall;

		private static GUIStyle dangerButton;

		private readonly MakeUpPlugin host;

		private readonly IMakeUpCleanupCatalogAdapter catalog;

		private readonly ICleanupSourceBackend source;

		private readonly MakeUpSessionState uiState = new MakeUpSessionState();

		private readonly CharacterCleanupSpotlightState spotlight = new CharacterCleanupSpotlightState();

		private readonly MakeUpCleanupShatterEffect shatter = new MakeUpCleanupShatterEffect();

		private CharacterCleanupController controller;

		private MakeUpSession session;

		private Vector2 optionScroll;

		private int gridScrollTarget = -1;

		private Vector2 siblingScroll;

		private bool active;

		private float terminateDeadline;

		private Rect spotlightRect;

		private readonly DeferredGuiActionState pendingActions = new DeferredGuiActionState();

		private string pendingJumpCategory;

		private int pendingJumpItem = -1;

		private int pendingRouteCategory = -1;

		private int pendingRouteItem = -1;

		private string status;

		private string runtimePreviewCategory;

		private string observedCategory;

		private bool footerHosted;

		private bool footerAvailable;

		private static GUISkin styleSkin;

		internal MakeUpCleanupPage(MakeUpPlugin host, ICleanupSourceBackend source)
		{
			this.host = host;
			this.source = source;
			catalog = new KoiMakeUpCleanupCatalogAdapter();
		}

		public void DrawBody()
		{
			footerHosted = true;
			try
			{
				Draw();
			}
			finally
			{
				footerHosted = false;
			}
		}

		public void DrawFooter()
		{
			if (footerAvailable && controller != null && controller.Trace != null)
			{
				GUILayout.Space((!controller.Trace.CanTerminate) ? 4f : 13f);
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				GUILayout.BeginVertical(GUILayout.Width(440f));
				try
				{
					DrawTerminate(controller.Trace, 48f);
				}
				finally
				{
					GUILayout.EndVertical();
					GUILayout.FlexibleSpace();
					GUILayout.EndHorizontal();
				}
			}
		}

		public void Draw()
		{
			footerAvailable = false;
			EnsureStyles();
			EnsureSession();
			if (controller == null || session == null)
			{
				GUILayout.Label(MakeUpLang.T("Select a character to use Character Clean-Up."));
				return;
			}
			bool flag = Event.current != null && Event.current.type == EventType.Layout;
			try
			{
				pendingActions.Apply(flag);
			}
			catch (Exception ex)
			{
				status = "Clean-Up action failed: " + ex.Message;
			}
			controller.RefreshSelection();
			controller.ApplyDeferredSelection(flag);
			if (!string.Equals(observedCategory, controller.ActiveCategoryId, StringComparison.Ordinal))
			{
				observedCategory = controller.ActiveCategoryId;
				runtimePreviewCategory = null;
			}
			ApplyPendingRoute(flag);
			ApplyPendingJump(flag);
			if (flag && terminateDeadline > 0f && Time.realtimeSinceStartup >= terminateDeadline)
			{
				terminateDeadline = 0f;
				FireTerminate();
			}
			DrawGroups();
			DrawCategories();
			CharacterCleanupCategory characterCleanupCategory = ActiveCategory();
			if (spotlight.ConfirmationArchive != null)
			{
				DrawConfirmation();
			}
			else if (characterCleanupCategory == null)
			{
				GUILayout.Space(28f);
				GUILayout.Label(MakeUpLang.T("No removable mod appearance choices were found in this section."), centerBold);
				GUILayout.Space(28f);
			}
			else
			{
				SyncSpotlight(characterCleanupCategory);
				if (flag && controller.Trace == null && spotlight.ActiveItemId >= 0)
				{
					Inspect(spotlight.ActiveItemId);
				}
				DrawSpotlight(characterCleanupCategory);
			}
			if (!string.IsNullOrEmpty(status))
			{
				GUILayout.Label(MakeUpLang.T(status), centerSmall);
			}
			if (source.PendingCount > 0)
			{
				GUILayout.Label(MakeUpLang.F("Pending quarantine archives: {0}", source.PendingCount), dimSmall);
			}
			shatter.Draw();
		}

		public void OnEnter()
		{
			active = true;
			EnsureSession();
			if (controller != null)
			{
				controller.OnEnter();
			}
		}

		public void OnExit()
		{
			active = false;
			if (controller != null)
			{
				controller.OnExit();
			}
			if (session != null)
			{
				session.SuspendSelection();
			}
			runtimePreviewCategory = null;
			observedCategory = null;
			ResetTransient();
		}

		public void Reset()
		{
			if (controller != null)
			{
				controller.Reset();
			}
			optionScroll = Vector2.zero;
			siblingScroll = Vector2.zero;
			status = null;
			pendingJumpCategory = null;
			pendingJumpItem = -1;
			spotlight.Reset();
			shatter.Reset();
			runtimePreviewCategory = null;
			observedCategory = null;
			ResetTransient();
		}

		public void RequestItem(int resolverCategory, int itemId)
		{
			pendingRouteCategory = resolverCategory;
			pendingRouteItem = itemId;
		}

		private void ApplyPendingRoute(bool isLayout)
		{
			if (isLayout && pendingRouteCategory >= 0 && pendingRouteItem >= 0)
			{
				int value = pendingRouteCategory;
				int itemId = pendingRouteItem;
				pendingRouteCategory = -1;
				pendingRouteItem = -1;
				if (catalog.TryFindOption(itemId, value, out var categoryId, out var _) && !string.IsNullOrEmpty(categoryId))
				{
					QueueJump(categoryId, itemId);
				}
			}
		}

		private void EnsureSession()
		{
			MakeUpSession makeUpSession = MakeUpSession.Resolve(session, uiState);
			if (makeUpSession != session)
			{
				if (controller != null)
				{
					controller.OnExit();
				}
				session = makeUpSession;
				catalog.SetSession(session);
				controller = ((session == null) ? null : new CharacterCleanupController(catalog, source));
				if (active && controller != null)
				{
					controller.OnEnter();
				}
				optionScroll = Vector2.zero;
				siblingScroll = Vector2.zero;
				spotlight.Reset();
				runtimePreviewCategory = null;
				observedCategory = null;
				ResetTransient();
			}
		}

		private void DrawGroups()
		{
			GUILayout.BeginHorizontal();
			for (int i = 0; i < GroupIds.Length; i++)
			{
				if (!HasGroup(GroupIds[i]))
				{
					continue;
				}
				Color color = GUI.color;
				if (string.Equals(GroupIds[i], controller.ActiveGroupId, StringComparison.Ordinal))
				{
					GUI.color = new Color(1f, 0.45f, 0.4f);
				}
				string groupId = GroupIds[i];
				if (GUILayout.Button(MakeUpLang.T(GroupLabels[i]), GUILayout.Height(29f)))
				{
					Queue(delegate
					{
						terminateDeadline = 0f;
						optionScroll = Vector2.zero;
						siblingScroll = Vector2.zero;
						spotlight.ContinueCleaning();
						controller.RequestGroup(groupId);
					});
				}
				GUI.color = color;
			}
			GUILayout.EndHorizontal();
			GUILayout.Space(5f);
		}

		private void DrawCategories()
		{
			IList<CharacterCleanupCategory> visibleCategories = controller.VisibleCategories;
			for (int i = 0; i * 3 < visibleCategories.Count; i++)
			{
				GUILayout.BeginHorizontal();
				for (int j = 0; j < 3; j++)
				{
					int num = i * 3 + j;
					if (num >= visibleCategories.Count)
					{
						GUILayout.FlexibleSpace();
						continue;
					}
					CharacterCleanupCategory characterCleanupCategory = visibleCategories[num];
					Color color = GUI.color;
					if (string.Equals(characterCleanupCategory.Id, controller.ActiveCategoryId, StringComparison.Ordinal))
					{
						GUI.color = new Color(1f, 0.45f, 0.4f);
					}
					string categoryId = characterCleanupCategory.Id;
					if (GUILayout.Button(MakeUpLang.T(characterCleanupCategory.Label), GUILayout.Height(25f)))
					{
						Queue(delegate
						{
							terminateDeadline = 0f;
							optionScroll = Vector2.zero;
							siblingScroll = Vector2.zero;
							spotlight.ContinueCleaning();
							controller.RequestCategory(categoryId);
						});
					}
					GUI.color = color;
				}
				GUILayout.EndHorizontal();
			}
			GUILayout.Space(6f);
		}

		private void DrawSpotlight(CharacterCleanupCategory category)
		{
			IList<MakeUpSelectorOption> options = catalog.OptionsFor(category.Id);
			MakeUpSelectorOption makeUpSelectorOption = FindOption(options, spotlight.ActiveItemId);
			if (makeUpSelectorOption == null)
			{
				GUILayout.Label(MakeUpLang.T("No removable mod items remain in this category."), centerBold);
				return;
			}
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			bool flag = GUILayout.Button("<", GUILayout.Width(44f), GUILayout.Height(128f));
			GUILayout.Space(8f);
			Texture2D texture = MakeUpCleanupThumbnail.Load(makeUpSelectorOption);
			Rect rect = GUILayoutUtility.GetRect(128f, 128f, GUI.skin.box, GUILayout.Width(128f), GUILayout.Height(128f));
			if (Event.current != null && Event.current.type == EventType.Repaint)
			{
				spotlightRect = rect;
			}
			DrawHeatedThumbnail(rect, texture);
			if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
			{
				Queue(delegate
				{
					terminateDeadline = 0f;
					spotlight.ToggleGrid();
					if (spotlight.GridOpen)
					{
						ScrollGridToSelection(options);
					}
				});
			}
			DrawHover(rect);
			GUILayout.Space(8f);
			bool flag2 = GUILayout.Button(">", GUILayout.Width(44f), GUILayout.Height(128f));
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			if (flag || flag2)
			{
				int direction = (flag2 ? 1 : (-1));
				Queue(delegate
				{
					terminateDeadline = 0f;
					if (spotlight.Move(direction))
					{
						Preview(spotlight.ActiveItemId);
					}
				});
			}
			GUILayout.Label(string.IsNullOrEmpty(makeUpSelectorOption.Name) ? ("#" + makeUpSelectorOption.Id) : makeUpSelectorOption.Name, centerBold);
			if (spotlight.GridOpen)
			{
				string key = "cleanup." + category.Id + "." + spotlight.SequenceGeneration;
				DrawSelectorGrid(key, options, makeUpSelectorOption.Id, ref optionScroll, delegate(int itemId)
				{
					Queue(delegate
					{
						terminateDeadline = 0f;
						if (spotlight.SelectFromGrid(itemId))
						{
							Preview(itemId);
						}
					});
				}, 3, 3f);
			}
			CharacterCleanupTrace trace = controller.Trace;
			if (trace == null)
			{
				return;
			}
			GUILayout.Space(4f);
			GUILayout.Label(MakeUpLang.F("Source zipmod:  {0}", trace.SourceDisplayName), centerSmall);
			if (trace.Archives.Count > 0)
			{
				string directoryName = Path.GetDirectoryName(trace.Archives[0]);
				if (!string.IsNullOrEmpty(directoryName))
				{
					GUILayout.Label(MakeUpLang.F("Folder:  {0}", directoryName), dimSmall);
				}
			}
			DrawCollateral(trace);
			GUILayout.Space(6f);
			footerAvailable = true;
			if (!footerHosted)
			{
				DrawTerminate(trace);
			}
		}

		private void DrawHeatedThumbnail(Rect rect, Texture2D texture)
		{
			Rect position = rect;
			bool num = terminateDeadline > 0f;
			float num2 = 0f;
			if (num)
			{
				num2 = 1f - Mathf.Clamp01((terminateDeadline - Time.realtimeSinceStartup) / 3f);
				float num3 = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 10f);
				float num4 = num2 * num2 * (0.55f + 0.45f * num3);
				Color color = GUI.color;
				GUI.color = new Color(1f, 0.4f, 0.12f, 0.4f * num4);
				float num5 = 8f + 12f * num2;
				GUI.DrawTexture(new Rect(rect.x - num5, rect.y - num5, rect.width + num5 * 2f, rect.height + num5 * 2f), Texture2D.whiteTexture, ScaleMode.StretchToFill);
				GUI.color = color;
				float num6 = 4.5f * num2 * num2;
				position.x += Mathf.Sin(Time.realtimeSinceStartup * 47f) * num6;
				position.y += Mathf.Cos(Time.realtimeSinceStartup * 61f) * num6;
			}
			GUI.Box(position, (texture == null) ? Texture2D.blackTexture : texture);
			if (num)
			{
				Color color2 = GUI.color;
				GUI.color = new Color(1f, 0.3f, 0.1f, 0.35f * num2 * num2);
				GUI.DrawTexture(position, Texture2D.whiteTexture, ScaleMode.StretchToFill);
				GUI.color = color2;
			}
		}

		private void DrawSelectorGrid(string key, IList<MakeUpSelectorOption> options, int selectedId, ref Vector2 scroll, Action<int> onPick, int columns, float spacing)
		{
			scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(250f));
			for (int i = 0; i < options.Count; i += columns)
			{
				bool flag = false;
				GUILayout.BeginHorizontal();
				for (int j = 0; j < columns; j++)
				{
					int num = i + j;
					if (num >= options.Count)
					{
						GUILayout.FlexibleSpace();
						continue;
					}
					MakeUpSelectorOption makeUpSelectorOption = options[num];
					if (makeUpSelectorOption.Id == gridScrollTarget)
					{
						flag = true;
					}
					Color color = GUI.color;
					if (makeUpSelectorOption.Id == selectedId)
					{
						GUI.color = Color.cyan;
					}
					GUILayout.BeginVertical(GUILayout.Width(112f));
					if (GUILayout.Button(MakeUpCleanupThumbnail.Load(makeUpSelectorOption), GUILayout.Width(96f), GUILayout.Height(76f)))
					{
						onPick(makeUpSelectorOption.Id);
					}
					GUILayout.Label(makeUpSelectorOption.Name, GUILayout.Width(108f));
					GUILayout.EndVertical();
					GUI.color = color;
					if (spacing > 0f)
					{
						GUILayout.Space(spacing);
					}
				}
				GUILayout.EndHorizontal();
				if (flag && Event.current.type == EventType.Repaint)
				{
					scroll.y = Mathf.Max(0f, GUILayoutUtility.GetLastRect().y);
					gridScrollTarget = -1;
				}
			}
			GUILayout.EndScrollView();
		}

		private void DrawCollateral(CharacterCleanupTrace trace)
		{
			List<CollateralTile> list = new List<CollateralTile>();
			HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
			int num = 0;
			for (int i = 0; i < trace.Siblings.Count; i++)
			{
				CharacterCleanupSibling characterCleanupSibling = trace.Siblings[i];
				if ((characterCleanupSibling.ItemId != trace.ItemId || !trace.ResolverCategory.HasValue || characterCleanupSibling.ResolverCategory != trace.ResolverCategory.Value) && hashSet.Add(characterCleanupSibling.ResolverCategory + ":" + characterCleanupSibling.ItemId))
				{
					if (catalog.TryFindOption(characterCleanupSibling.ItemId, characterCleanupSibling.ResolverCategory, out var categoryId, out var resolvedCategory, out var option))
					{
						list.Add(new CollateralTile(categoryId, resolvedCategory, option));
					}
					else
					{
						num++;
					}
				}
			}
			if (list.Count == 0 && num == 0 && trace.StudioItemCount == 0)
			{
				return;
			}
			GUILayout.Label(MakeUpLang.T("Also in this zipmod (removed together):"), centerSmall);
			int num2 = (list.Count + 12 - 1) / 12;
			if (list.Count > 0)
			{
				siblingScroll = GUILayout.BeginScrollView(siblingScroll, GUILayout.Height(Mathf.Min((float)num2 * 38f + 8f, 121.6f)));
				for (int j = 0; j < num2; j++)
				{
					GUILayout.BeginHorizontal();
					GUILayout.FlexibleSpace();
					for (int k = 0; k < 12; k++)
					{
						int num3 = j * 12 + k;
						if (num3 >= list.Count)
						{
							break;
						}
						CollateralTile collateralTile = list[num3];
						Texture2D texture2D = MakeUpCleanupThumbnail.Load(collateralTile.Option);
						if (!GUILayout.Button((texture2D == null) ? new GUIContent("?", collateralTile.Option.Name) : new GUIContent(texture2D, collateralTile.Option.Name), GUILayout.Width(34f), GUILayout.Height(34f)))
						{
							continue;
						}
						string category = collateralTile.CategoryId;
						int resolverCategory = collateralTile.ResolverCategory;
						int item = collateralTile.Option.Id;
						Queue(delegate
						{
							if (!string.IsNullOrEmpty(category))
							{
								QueueJump(category, item);
							}
							else
							{
								host.RequestCleanupItem("outfit", resolverCategory, item);
							}
						});
					}
					GUILayout.FlexibleSpace();
					GUILayout.EndHorizontal();
				}
				GUILayout.EndScrollView();
			}
			if (num > 0)
			{
				GUILayout.Label(MakeUpLang.F("+ {0} items without catalog thumbnails", num), dimSmall);
			}
			if (trace.StudioItemCount > 0)
			{
				GUILayout.Label(MakeUpLang.F("+ {0} Studio items", trace.StudioItemCount), dimSmall);
			}
		}

		private void DrawTerminate(CharacterCleanupTrace trace, float buttonHeight = 42f)
		{
			GUIStyle gUIStyle = new GUIStyle(dangerButton)
			{
				fixedHeight = 0f,
				fixedWidth = 0f,
				margin = new RectOffset(),
				alignment = TextAnchor.MiddleCenter,
				fontSize = 16,
				fontStyle = FontStyle.Bold,
				clipping = TextClipping.Clip
			};
			if (trace.AlreadyBanned)
			{
				GUILayout.Label(MakeUpLang.T("Already terminated - awaiting quarantine on next start."), centerSmall);
				return;
			}
			if (!trace.CanTerminate)
			{
				bool enabled = GUI.enabled;
				GUI.enabled = false;
				DrawSkullButton(MakeUpLang.T("TERMINATE ZIPMOD"), gUIStyle, buttonHeight, flank: true);
				GUI.enabled = enabled;
				if (!string.IsNullOrEmpty(trace.LockedReason))
				{
					GUILayout.Label(MakeUpLang.T(trace.LockedReason), centerSmall);
				}
				return;
			}
			bool flag = terminateDeadline > 0f;
			Color backgroundColor = GUI.backgroundColor;
			GUI.backgroundColor = (flag ? Color.Lerp(new Color(0.8f, 0.08f, 0.06f), Color.white, 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 10f)) : new Color(0.8f, 0.08f, 0.06f));
			string text = (flag ? MakeUpLang.F("Terminating in {0}s - press again to cancel", Mathf.Max(1, Mathf.CeilToInt(terminateDeadline - Time.realtimeSinceStartup))) : MakeUpLang.T("TERMINATE ZIPMOD"));
			while (gUIStyle.fontSize > 10 && gUIStyle.CalcSize(new GUIContent(text)).x > 436f)
			{
				gUIStyle.fontSize--;
			}
			if (DrawSkullButton(text, gUIStyle, buttonHeight, !flag))
			{
				Queue(delegate
				{
					if (terminateDeadline > 0f)
					{
						terminateDeadline = 0f;
						status = "Termination cancelled.";
					}
					else
					{
						terminateDeadline = Time.realtimeSinceStartup + 3f;
						status = null;
					}
				});
			}
			GUI.backgroundColor = backgroundColor;
		}

		private static bool DrawSkullButton(string label, GUIStyle style, float height, bool flank)
		{
			bool result = GUILayout.Button(label, style, GUILayout.Height(height));
			if (flank && Event.current.type == EventType.Repaint)
			{
				Rect lastRect = GUILayoutUtility.GetLastRect();
				Texture2D texture2D = SharedMakeUpIcons.Load("cleanup-skull");
				float num = Mathf.Min(32f, lastRect.height - 8f);
				float num2 = Mathf.Min(style.CalcSize(new GUIContent(label)).x * 0.5f + 14f, lastRect.width * 0.5f - num - 8f);
				if (texture2D != null)
				{
					GUI.DrawTexture(new Rect(lastRect.center.x - num2 - num, lastRect.center.y - num * 0.5f, num, num), texture2D, ScaleMode.ScaleToFit);
					GUI.DrawTexture(new Rect(lastRect.center.x + num2, lastRect.center.y - num * 0.5f, num, num), texture2D, ScaleMode.ScaleToFit);
				}
			}
			return result;
		}

		private void FireTerminate()
		{
			CharacterCleanupTrace trace = controller.Trace;
			if (trace != null && trace.CanTerminate)
			{
				int itemId = trace.ItemId;
				string sourceDisplayName = trace.SourceDisplayName;
				Texture2D value = null;
				if (catalog.TryFindOption(itemId, out var _, out var option))
				{
					value = MakeUpCleanupThumbnail.Load(option);
				}
				if (!controller.Terminate())
				{
					status = "Termination could not be queued; no archive was moved.";
					return;
				}
				shatter.Start(value, spotlightRect);
				spotlight.MarkTerminated(sourceDisplayName, itemId);
				host.NotifyCharacterCleanupChanged();
				optionScroll = Vector2.zero;
				siblingScroll = Vector2.zero;
				status = null;
			}
		}

		private void DrawConfirmation()
		{
			GUILayout.Space(16f);
			GUILayout.Label(MakeUpLang.F("Terminated \"{0}\".", spotlight.ConfirmationArchive), centerBold);
			GUILayout.Space(6f);
			GUILayout.Label(MakeUpLang.T("Its items are hidden from the character selectors, and the archive will move to Cleanup_Quarantine on the next game start. Nothing is deleted automatically."), centerSmall);
			GUILayout.Space(10f);
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(MakeUpLang.T("Undo Termination"), GUILayout.Width(200f), GUILayout.Height(30f)))
			{
				Queue(UndoTermination);
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.Space(8f);
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(MakeUpLang.T("Continue Cleaning"), GUILayout.Width(200f), GUILayout.Height(28f)))
			{
				Queue(delegate
				{
					spotlight.ContinueCleaning();
					CharacterCleanupCategory characterCleanupCategory = ActiveCategory();
					if (characterCleanupCategory != null)
					{
						SyncSpotlight(characterCleanupCategory);
						if (spotlight.ActiveItemId >= 0)
						{
							Preview(spotlight.ActiveItemId);
						}
					}
				});
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.Space(16f);
		}

		private void UndoTermination()
		{
			int confirmationItemId = spotlight.ConfirmationItemId;
			if (!controller.UndoLast())
			{
				status = "Only a termination queued during this Studio session can be undone here.";
				return;
			}
			host.NotifyCharacterCleanupChanged();
			spotlight.ContinueCleaning();
			CharacterCleanupCategory characterCleanupCategory = ActiveCategory();
			if (characterCleanupCategory != null)
			{
				SyncSpotlight(characterCleanupCategory);
				spotlight.Select(confirmationItemId);
				if (spotlight.ActiveItemId >= 0)
				{
					Preview(spotlight.ActiveItemId);
				}
			}
			status = "Termination undone.";
		}

		private void QueueJump(string categoryId, int itemId)
		{
			terminateDeadline = 0f;
			spotlight.ContinueCleaning();
			pendingJumpCategory = categoryId;
			pendingJumpItem = itemId;
			controller.RequestCategory(categoryId);
		}

		private void ApplyPendingJump(bool layout)
		{
			if (!layout || string.IsNullOrEmpty(pendingJumpCategory) || !string.Equals(controller.ActiveCategoryId, pendingJumpCategory, StringComparison.Ordinal))
			{
				return;
			}
			CharacterCleanupCategory characterCleanupCategory = ActiveCategory();
			if (characterCleanupCategory != null)
			{
				SyncSpotlight(characterCleanupCategory);
				int itemId = pendingJumpItem;
				pendingJumpCategory = null;
				pendingJumpItem = -1;
				if (spotlight.Select(itemId))
				{
					Preview(itemId);
				}
			}
		}

		private void SyncSpotlight(CharacterCleanupCategory category)
		{
			IList<MakeUpSelectorOption> list = catalog.OptionsFor(category.Id);
			List<int> list2 = new List<int>();
			for (int i = 0; i < list.Count; i++)
			{
				list2.Add(list[i].Id);
			}
			int sequenceGeneration = spotlight.SequenceGeneration;
			bool flag = !string.Equals(spotlight.CategoryId, category.Id, StringComparison.Ordinal);
			spotlight.SetSequence(category.Id, list2);
			if (sequenceGeneration != spotlight.SequenceGeneration)
			{
				int itemId = catalog.CurrentItemId(category.Id);
				if (flag)
				{
					spotlight.Select(itemId);
				}
				optionScroll = Vector2.zero;
				siblingScroll = Vector2.zero;
			}
		}

		private void ScrollGridToSelection(IList<MakeUpSelectorOption> options)
		{
			for (int i = 0; i < options.Count && options[i].Id != spotlight.ActiveItemId; i++)
			{
			}
			gridScrollTarget = spotlight.ActiveItemId;
		}

		private void Preview(int itemId)
		{
			try
			{
				if (controller.Preview(itemId))
				{
					runtimePreviewCategory = controller.ActiveCategoryId;
					status = null;
				}
			}
			catch (Exception ex)
			{
				status = "Preview could not be applied: " + ex.Message;
			}
		}

		private void Inspect(int itemId)
		{
			try
			{
				if (controller.Inspect(itemId))
				{
					status = null;
				}
			}
			catch (Exception ex)
			{
				status = "Item could not be inspected: " + ex.Message;
			}
		}

		internal void MaintainRuntimePreview()
		{
			if (active && session != null && !string.IsNullOrEmpty(runtimePreviewCategory))
			{
				catalog.MaintainPreviewVisibility(runtimePreviewCategory);
			}
		}

		private CharacterCleanupCategory ActiveCategory()
		{
			for (int i = 0; i < controller.Categories.Count; i++)
			{
				if (string.Equals(controller.Categories[i].Id, controller.ActiveCategoryId, StringComparison.Ordinal))
				{
					return controller.Categories[i];
				}
			}
			return null;
		}

		private bool HasGroup(string groupId)
		{
			for (int i = 0; i < controller.Categories.Count; i++)
			{
				if (string.Equals(controller.Categories[i].GroupId, groupId, StringComparison.Ordinal))
				{
					return true;
				}
			}
			return false;
		}

		private static MakeUpSelectorOption FindOption(IList<MakeUpSelectorOption> options, int itemId)
		{
			for (int i = 0; i < options.Count; i++)
			{
				if (options[i].Id == itemId)
				{
					return options[i];
				}
			}
			if (options.Count != 0)
			{
				return options[0];
			}
			return null;
		}

		private void Queue(Action action)
		{
			pendingActions.Queue(action);
		}

		private void ResetTransient()
		{
			terminateDeadline = 0f;
			shatter.Reset();
			pendingActions.Reset();
		}

		private static void DrawHover(Rect rect)
		{
			Event current = Event.current;
			if (current != null && rect.Contains(current.mousePosition))
			{
				Color color = GUI.color;
				GUI.color = new Color(0.45f, 0.8f, 1f, 0.9f);
				GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height), Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), Texture2D.whiteTexture);
				GUI.color = color;
			}
		}

		private static void EnsureStyles()
		{
			if (centerBold == null || !(styleSkin == GUI.skin))
			{
				styleSkin = GUI.skin;
				centerBold = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleCenter,
					fontStyle = FontStyle.Bold,
					wordWrap = true
				};
				centerSmall = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleCenter,
					wordWrap = true
				};
				dimSmall = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleCenter,
					fontSize = 10,
					wordWrap = true
				};
				dimSmall.normal.textColor = new Color(0.65f, 0.65f, 0.65f);
				dangerButton = new GUIStyle(GUI.skin.button)
				{
					fontStyle = FontStyle.Bold,
					fontSize = 14
				};
			}
		}
	}
	internal sealed class MakeUpCleanupShatterEffect
	{
		private const int FragmentCount = 4;

		private const float PopDuration = 0.18f;

		private const float CrackDuration = 0.16f;

		private const float FlyDuration = 0.72f;

		private Texture2D texture;

		private Rect origin;

		private float startedAt;

		private float[] magnitudes;

		private Vector2[] jitter;

		internal void Start(Texture2D value, Rect rect)
		{
			if (!(value == null) && !(rect.width <= 0f))
			{
				texture = value;
				origin = rect;
				startedAt = Time.realtimeSinceStartup;
				int num = 16;
				magnitudes = new float[num];
				jitter = new Vector2[num];
				for (int i = 0; i < num; i++)
				{
					magnitudes[i] = UnityEngine.Random.Range(90f, 200f);
					jitter[i] = new Vector2(UnityEngine.Random.Range(-50f, 50f), UnityEngine.Random.Range(-60f, 20f));
				}
			}
		}

		internal void Reset()
		{
			texture = null;
			startedAt = 0f;
		}

		internal void Draw()
		{
			if (startedAt <= 0f || texture == null)
			{
				return;
			}
			float num = Time.realtimeSinceStartup - startedAt;
			float num2 = 1.0600001f;
			if (num >= num2)
			{
				Reset();
				return;
			}
			Event current = Event.current;
			if (current != null && current.type == EventType.MouseDown)
			{
				Reset();
				current.Use();
			}
			else
			{
				if (current == null || current.type != EventType.Repaint)
				{
					return;
				}
				Color color = GUI.color;
				GUI.BeginGroup(origin);
				try
				{
					Vector2 center = new Vector2(origin.width / 2f, origin.height / 2f);
					float num3 = 1f + 0.35f * Mathf.Clamp01(num / 0.18f);
					float num4 = origin.width * num3;
					float num5 = origin.height * num3;
					if (num < 0.18f)
					{
						float num6 = num / 0.18f;
						DrawGlow(center, num4, 0.7f * num6);
						float num7 = 2.5f * num6;
						Rect position = new Rect(center.x - num4 / 2f + Mathf.Sin(num * 57f) * num7, center.y - num5 / 2f + Mathf.Cos(num * 71f) * num7, num4, num5);
						GUI.color = Color.white;
						GUI.DrawTexture(position, texture, ScaleMode.StretchToFill);
						GUI.color = new Color(1f, 0.35f, 0.15f, 0.3f * num6);
						GUI.DrawTexture(position, Texture2D.whiteTexture, ScaleMode.StretchToFill);
					}
					else if (num < 0.34f)
					{
						float num8 = (num - 0.18f) / 0.16f;
						DrawGlow(center, num4, 0.7f + 0.3f * num8);
						DrawFragments(center, num4, num5, 5f * num8, 0f, 1f);
					}
					else
					{
						float num9 = (num - 0.18f - 0.16f) / 0.72f;
						DrawGlow(center, num4 * (1f + 2f * num9), 0.9f * (1f - num9));
						DrawFragments(center, num4, num5, 5f, num9, 1f - num9);
					}
				}
				finally
				{
					GUI.EndGroup();
					GUI.color = color;
				}
			}
		}

		private void DrawFragments(Vector2 center, float width, float height, float gap, float fly, float alpha)
		{
			float num = width / 4f;
			float num2 = height / 4f;
			for (int i = 0; i < 4; i++)
			{
				for (int j = 0; j < 4; j++)
				{
					int num3 = i * 4 + j;
					Vector2 vector = new Vector2(center.x - width / 2f + (float)j * num + num / 2f, center.y - height / 2f + (float)i * num2 + num2 / 2f);
					Vector2 vector2 = vector - center;
					Vector2 vector3 = ((vector2.sqrMagnitude > 0.01f) ? vector2.normalized : Vector2.up);
					Vector2 vector4 = vector + vector3 * (gap + magnitudes[num3] * fly) + jitter[num3] * fly;
					Rect position = new Rect(vector4.x - num / 2f, vector4.y - num2 / 2f, num, num2);
					Rect texCoords = new Rect((float)j / 4f, 1f - (float)(i + 1) / 4f, 0.25f, 0.25f);
					GUI.color = new Color(1f, 1f, 1f, alpha);
					GUI.DrawTextureWithTexCoords(position, texture, texCoords);
				}
			}
		}

		private static void DrawGlow(Vector2 center, float size, float intensity)
		{
			if (!(intensity <= 0f))
			{
				GUI.color = new Color(1f, 0.45f, 0.15f, 0.35f * intensity);
				float num = size * 1.6f;
				GUI.DrawTexture(new Rect(center.x - num / 2f, center.y - num / 2f, num, num), Texture2D.whiteTexture, ScaleMode.StretchToFill);
			}
		}
	}
}
namespace Freelancer604.MakeUp.Koi
{
	internal sealed class KoiAppearanceAdapter
	{
		internal IList<MakeUpSelectorOption> Options(MakeUpSession session, string key, bool moddedOnly)
		{
			KoiAppearanceSpec koiAppearanceSpec = RequireSpec(key);
			Dictionary<int, ListInfoBase> categoryInfo = session.Character.lstCtrl.GetCategoryInfo(koiAppearanceSpec.Category);
			List<MakeUpSelectorOption> list = new List<MakeUpSelectorOption>();
			if (categoryInfo == null)
			{
				return list;
			}
			foreach (KeyValuePair<int, ListInfoBase> item in categoryInfo.OrderBy((KeyValuePair<int, ListInfoBase> x) => x.Key))
			{
				ListInfoBase value = item.Value;
				if (value != null && (!moddedOnly || value.Id >= 100000000))
				{
					list.Add(new MakeUpSelectorOption(value.Id, value.Name, value.GetInfo(ChaListDefine.KeyType.ThumbAB), value.GetInfo(ChaListDefine.KeyType.ThumbTex), value));
				}
			}
			return list;
		}

		internal int ReadId(MakeUpSession session, string key)
		{
			ChaControl chaControl = Require(session);
			return key switch
			{
				"face.head" => chaControl.fileFace.headId, 
				"face.detail" => chaControl.fileFace.detailId, 
				"face.eyebrow" => chaControl.fileFace.eyebrowId, 
				"face.eye.left" => chaControl.fileFace.pupil[0].id, 
				"face.eye.right" => chaControl.fileFace.pupil[1].id, 
				"face.eye_hi_up" => chaControl.fileFace.hlUpId, 
				"face.eye_hi_down" => chaControl.fileFace.hlDownId, 
				"face.eyeline_up" => chaControl.fileFace.eyelineUpId, 
				"face.eyeline_down" => chaControl.fileFace.eyelineDownId, 
				"face.nose" => chaControl.fileFace.noseId, 
				"face.mole" => chaControl.fileFace.moleId, 
				"face.eyeshadow" => chaControl.fileFace.baseMakeup.eyeshadowId, 
				"face.cheek" => chaControl.fileFace.baseMakeup.cheekId, 
				"face.lip" => chaControl.fileFace.baseMakeup.lipId, 
				"face.lipline" => chaControl.fileFace.lipLineId, 
				"face.paint.0" => chaControl.fileFace.baseMakeup.paintId[0], 
				"face.paint.1" => chaControl.fileFace.baseMakeup.paintId[1], 
				"body.detail" => chaControl.fileBody.detailId, 
				"body.paint.0" => chaControl.fileBody.paintId[0], 
				"body.paint.1" => chaControl.fileBody.paintId[1], 
				"body.sunburn" => chaControl.fileBody.sunburnId, 
				"body.nip" => chaControl.fileBody.nipId, 
				"body.underhair" => chaControl.fileBody.underhairId, 
				"hair.back" => chaControl.fileHair.parts[0].id, 
				"hair.front" => chaControl.fileHair.parts[1].id, 
				"hair.side" => chaControl.fileHair.parts[2].id, 
				"hair.option" => chaControl.fileHair.parts[3].id, 
				_ => throw new ArgumentOutOfRangeException("key", key, "Unknown Koi selector"), 
			};
		}

		internal void ApplyId(MakeUpSession session, string key, int id)
		{
			ChaControl chaControl = Require(session);
			if (ReadId(session, key) != id)
			{
				switch (key)
				{
				case "face.head":
					chaControl.ChangeHead(id);
					RefreshHeadEyeBindings(chaControl);
					break;
				case "face.detail":
					chaControl.fileFace.detailId = id;
					chaControl.ChangeSettingFaceDetail();
					break;
				case "face.eyebrow":
					chaControl.fileFace.eyebrowId = id;
					chaControl.ChangeSettingEyebrow();
					break;
				case "face.eye.left":
					chaControl.fileFace.pupil[0].id = id;
					chaControl.ChangeSettingEyeL(updateBaseTex: true, updateMaskTex: false, updateColorAndOffset: true);
					break;
				case "face.eye.right":
					chaControl.fileFace.pupil[1].id = id;
					chaControl.ChangeSettingEyeR(updateBaseTex: true, updateMaskTex: false, updateColorAndOffset: true);
					break;
				case "face.eye_hi_up":
					chaControl.fileFace.hlUpId = id;
					chaControl.ChangeSettingEyeHiUp();
					break;
				case "face.eye_hi_down":
					chaControl.fileFace.hlDownId = id;
					chaControl.ChangeSettingEyeHiDown();
					break;
				case "face.eyeline_up":
					chaControl.fileFace.eyelineUpId = id;
					chaControl.ChangeSettingEyelineUp();
					break;
				case "face.eyeline_down":
					chaControl.fileFace.eyelineDownId = id;
					chaControl.ChangeSettingEyelineDown();
					break;
				case "face.nose":
					chaControl.fileFace.noseId = id;
					chaControl.ChangeSettingNose();
					break;
				case "face.mole":
					chaControl.fileFace.moleId = id;
					RebuildFace(chaControl);
					break;
				case "face.eyeshadow":
					chaControl.fileFace.baseMakeup.eyeshadowId = id;
					chaControl.ChangeSettingEyeShadow();
					break;
				case "face.cheek":
					chaControl.fileFace.baseMakeup.cheekId = id;
					RebuildFace(chaControl);
					break;
				case "face.lip":
					chaControl.fileFace.baseMakeup.lipId = id;
					chaControl.ChangeSettingLip();
					break;
				case "face.lipline":
					chaControl.fileFace.lipLineId = id;
					chaControl.ChangeSettingLip();
					break;
				case "face.paint.0":
					chaControl.fileFace.baseMakeup.paintId[0] = id;
					RebuildFace(chaControl);
					break;
				case "face.paint.1":
					chaControl.fileFace.baseMakeup.paintId[1] = id;
					RebuildFace(chaControl);
					break;
				case "body.detail":
					chaControl.fileBody.detailId = id;
					chaControl.ChangeSettingBodyDetail();
					break;
				case "body.paint.0":
					chaControl.fileBody.paintId[0] = id;
					RebuildBody(chaControl);
					break;
				case "body.paint.1":
					chaControl.fileBody.paintId[1] = id;
					RebuildBody(chaControl);
					break;
				case "body.sunburn":
					chaControl.fileBody.sunburnId = id;
					RebuildBody(chaControl);
					break;
				case "body.nip":
					chaControl.fileBody.nipId = id;
					chaControl.ChangeSettingNip();
					break;
				case "body.underhair":
					chaControl.fileBody.underhairId = id;
					chaControl.ChangeSettingUnderhair();
					break;
				case "hair.back":
					chaControl.ChangeHair(0, id);
					break;
				case "hair.front":
					chaControl.ChangeHair(1, id);
					break;
				case "hair.side":
					chaControl.ChangeHair(2, id);
					break;
				case "hair.option":
					chaControl.ChangeHair(3, id);
					break;
				default:
					throw new ArgumentOutOfRangeException("key", key, "Unknown Koi selector");
				}
			}
		}

		internal void ApplyEyePair(MakeUpSession session, int leftId, int rightId)
		{
			ChaControl chaControl = Require(session);
			chaControl.fileFace.pupil[0].id = leftId;
			chaControl.fileFace.pupil[1].id = rightId;
			chaControl.ChangeSettingEye(updateBaseTex: true, updateMaskTex: true, updateColorAndOffset: true);
		}

		private static void RefreshHeadEyeBindings(ChaControl c)
		{
			if (!(c == null) && !(c.objHead == null))
			{
				c.ChangeSettingWhiteOfEye(updateTex: true, updateColor: true);
				c.ChangeSettingEye(updateBaseTex: true, updateMaskTex: true, updateColorAndOffset: true);
				c.ChangeSettingEyeHiUp();
				c.ChangeSettingEyeHiDown();
				c.ChangeSettingEyelineUp();
				c.ChangeSettingEyelineDown();
			}
		}

		internal void SetFaceShape(MakeUpSession session, int index, float value)
		{
			Require(session).SetShapeFaceValue(index, value);
		}

		internal float ReadBodyShape(MakeUpSession session, int index)
		{
			ChaControl chaControl = Require(session);
			if (!KoiPushupCompatibility.TryReadBodyShape(chaControl, index, out var value))
			{
				return chaControl.fileBody.shapeValueBody[index];
			}
			return value;
		}

		internal void SetBodyShape(MakeUpSession session, int index, float value)
		{
			ChaControl chaControl = Require(session);
			if (!KoiPushupCompatibility.TryWriteBodyShape(chaControl, index, value))
			{
				chaControl.SetShapeBodyValue(index, value);
			}
		}

		internal Color ReadColor(MakeUpSession session, string key)
		{
			ChaControl chaControl = Require(session);
			switch (key)
			{
			case "Skin Main":
				return chaControl.fileBody.skinMainColor;
			case "Skin Sub":
				return chaControl.fileBody.skinSubColor;
			case "Eyebrows":
				return chaControl.fileFace.eyebrowColor;
			case "Eyes":
				return chaControl.fileFace.pupil[0].baseColor;
			case "Left Eye":
				return chaControl.fileFace.pupil[0].baseColor;
			case "Right Eye":
				return chaControl.fileFace.pupil[1].baseColor;
			case "Eye Whites":
				return chaControl.fileFace.whiteBaseColor;
			case "Upper Highlight":
				return chaControl.fileFace.hlUpColor;
			case "Lower Highlight":
				return chaControl.fileFace.hlDownColor;
			case "Eyeline":
				return chaControl.fileFace.eyelineColor;
			case "Mole":
				return chaControl.fileFace.moleColor;
			case "Lip Line":
				return chaControl.fileFace.lipLineColor;
			case "Eyeshadow":
				return chaControl.fileFace.baseMakeup.eyeshadowColor;
			case "Cheek":
				return chaControl.fileFace.baseMakeup.cheekColor;
			case "Lip":
				return chaControl.fileFace.baseMakeup.lipColor;
			case "Face Paint 1":
				return chaControl.fileFace.baseMakeup.paintColor[0];
			case "Face Paint 2":
				return chaControl.fileFace.baseMakeup.paintColor[1];
			case "Sunburn":
				return chaControl.fileBody.sunburnColor;
			case "Nipples":
				return chaControl.fileBody.nipColor;
			case "Underhair":
				return chaControl.fileBody.underhairColor;
			case "Nails":
				return chaControl.fileBody.nailColor;
			case "Body Paint 1":
				return chaControl.fileBody.paintColor[0];
			case "Body Paint 2":
				return chaControl.fileBody.paintColor[1];
			default:
				if (key.StartsWith("Hair ", StringComparison.Ordinal))
				{
					return chaControl.fileHair.parts[int.Parse(key.Substring(5))].baseColor;
				}
				throw new ArgumentOutOfRangeException("key", key, "Unknown Koi colour");
			}
		}

		internal void ApplyColor(MakeUpSession session, string key, Color value)
		{
			ChaControl chaControl = Require(session);
			switch (key)
			{
			case "Skin Main":
				chaControl.fileBody.skinMainColor = value;
				RebuildSkin(chaControl);
				return;
			case "Skin Sub":
				chaControl.fileBody.skinSubColor = value;
				RebuildSkin(chaControl);
				return;
			case "Eyebrows":
				chaControl.fileFace.eyebrowColor = value;
				chaControl.ChangeSettingEyebrowColor();
				return;
			case "Eyes":
				chaControl.fileFace.pupil[0].baseColor = value;
				chaControl.fileFace.pupil[1].baseColor = value;
				chaControl.ChangeSettingEye(updateBaseTex: false, updateMaskTex: false, updateColorAndOffset: true);
				return;
			case "Left Eye":
				chaControl.fileFace.pupil[0].baseColor = value;
				chaControl.ChangeSettingEyeL(updateBaseTex: false, updateMaskTex: false, updateColorAndOffset: true);
				return;
			case "Right Eye":
				chaControl.fileFace.pupil[1].baseColor = value;
				chaControl.ChangeSettingEyeR(updateBaseTex: false, updateMaskTex: false, updateColorAndOffset: true);
				return;
			case "Eye Whites":
				chaControl.fileFace.whiteBaseColor = value;
				chaControl.ChangeSettingWhiteOfEye(updateTex: false, updateColor: true);
				return;
			case "Upper Highlight":
				chaControl.fileFace.hlUpColor = value;
				chaControl.ChangeSettingEyeHiUpColor();
				return;
			case "Lower Highlight":
				chaControl.fileFace.hlDownColor = value;
				chaControl.ChangeSettingEyeHiDownColor();
				return;
			case "Eyeline":
				chaControl.fileFace.eyelineColor = value;
				chaControl.ChangeSettingEyelineColor();
				return;
			case "Mole":
				chaControl.fileFace.moleColor = value;
				RebuildFace(chaControl);
				return;
			case "Lip Line":
				chaControl.fileFace.lipLineColor = value;
				RebuildFace(chaControl);
				return;
			case "Eyeshadow":
				chaControl.fileFace.baseMakeup.eyeshadowColor = value;
				chaControl.ChangeSettingEyeShadowColor();
				return;
			case "Cheek":
				chaControl.fileFace.baseMakeup.cheekColor = value;
				RebuildFace(chaControl);
				return;
			case "Lip":
				chaControl.fileFace.baseMakeup.lipColor = value;
				chaControl.ChangeSettingLipColor();
				return;
			case "Face Paint 1":
				chaControl.fileFace.baseMakeup.paintColor[0] = value;
				RebuildFace(chaControl);
				return;
			case "Face Paint 2":
				chaControl.fileFace.baseMakeup.paintColor[1] = value;
				RebuildFace(chaControl);
				return;
			case "Sunburn":
				chaControl.fileBody.sunburnColor = value;
				RebuildBody(chaControl);
				return;
			case "Nipples":
				chaControl.fileBody.nipColor = value;
				chaControl.ChangeSettingNipColor();
				return;
			case "Underhair":
				chaControl.fileBody.underhairColor = value;
				chaControl.ChangeSettingUnderhairColor();
				return;
			case "Nails":
				chaControl.fileBody.nailColor = value;
				RebuildBody(chaControl);
				return;
			case "Body Paint 1":
				chaControl.fileBody.paintColor[0] = value;
				RebuildBody(chaControl);
				return;
			case "Body Paint 2":
				chaControl.fileBody.paintColor[1] = value;
				RebuildBody(chaControl);
				return;
			}
			if (!key.StartsWith("Hair ", StringComparison.Ordinal))
			{
				throw new ArgumentOutOfRangeException("key", key, "Unknown Koi colour");
			}
			int num = int.Parse(key.Substring(5));
			chaControl.fileHair.parts[num].baseColor = value;
			chaControl.ChangeSettingHairColor(num, c00: true, c01: false, c02: false);
		}

		private static void RebuildFace(ChaControl c)
		{
			c.AddUpdateCMFaceTexFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpCheek: true, inpLipLine: true, inpMole: true);
			c.AddUpdateCMFaceColorFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpCheek: true, inpLipLine: true, inpMole: true);
			c.AddUpdateCMFaceLayoutFlags(inpPaint01: true, inpPaint02: true, inpMole: true);
			c.CreateFaceTexture();
		}

		private static void RebuildBody(ChaControl c)
		{
			c.AddUpdateCMBodyTexFlags(inpBase: false, inpSub: false, inpPaint01: true, inpPaint02: true, inpSunburn: true);
			c.AddUpdateCMBodyColorFlags(inpBase: false, inpSub: false, inpPaint01: true, inpPaint02: true, inpSunburn: true, inpNail: true);
			c.CreateBodyTexture();
		}

		private static void RebuildSkin(ChaControl c)
		{
			c.AddUpdateCMBodyTexFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpSunburn: true);
			c.AddUpdateCMBodyColorFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpSunburn: true, inpNail: true);
			c.AddUpdateCMBodyLayoutFlags(inpPaint01: true, inpPaint02: true);
			c.CreateBodyTexture();
			RebuildFace(c);
		}

		private static ChaControl Require(MakeUpSession session)
		{
			if (session == null || session.Character == null)
			{
				throw new InvalidOperationException("No character selected.");
			}
			return session.Character;
		}

		private static KoiAppearanceSpec RequireSpec(string key)
		{
			return KoiAppearanceCatalog.Find(key) ?? throw new ArgumentOutOfRangeException("key");
		}
	}
	internal sealed class KoiAppearanceSpec
	{
		internal string Group { get; private set; }

		internal string Key { get; private set; }

		internal string Label { get; private set; }

		internal ChaListDefine.CategoryNo Category { get; private set; }

		internal KoiAppearanceSpec(string group, string key, string label, ChaListDefine.CategoryNo category)
		{
			Group = group;
			Key = key;
			Label = label;
			Category = category;
		}
	}
	internal static class KoiAppearanceCatalog
	{
		internal static readonly IList<KoiAppearanceSpec> Selectors = new List<KoiAppearanceSpec>
		{
			new KoiAppearanceSpec("face", "face.head", "Head", ChaListDefine.CategoryNo.bo_head),
			new KoiAppearanceSpec("face", "face.detail", "Face Detail", ChaListDefine.CategoryNo.mt_face_detail),
			new KoiAppearanceSpec("face", "face.eyebrow", "Eyebrows", ChaListDefine.CategoryNo.mt_eyebrow),
			new KoiAppearanceSpec("face", "face.eye.left", "Left Eye", ChaListDefine.CategoryNo.mt_eye),
			new KoiAppearanceSpec("face", "face.eye.right", "Right Eye", ChaListDefine.CategoryNo.mt_eye),
			new KoiAppearanceSpec("face", "face.eye_hi_up", "Upper Eye Highlight", ChaListDefine.CategoryNo.mt_eye_hi_up),
			new KoiAppearanceSpec("face", "face.eye_hi_down", "Lower Eye Highlight", ChaListDefine.CategoryNo.mt_eye_hi_down),
			new KoiAppearanceSpec("face", "face.eyeline_up", "Upper Eyeline", ChaListDefine.CategoryNo.mt_eyeline_up),
			new KoiAppearanceSpec("face", "face.eyeline_down", "Lower Eyeline", ChaListDefine.CategoryNo.mt_eyeline_down),
			new KoiAppearanceSpec("face", "face.nose", "Nose", ChaListDefine.CategoryNo.mt_nose),
			new KoiAppearanceSpec("face", "face.mole", "Mole", ChaListDefine.CategoryNo.mt_mole),
			new KoiAppearanceSpec("face", "face.eyeshadow", "Eyeshadow", ChaListDefine.CategoryNo.mt_eyeshadow),
			new KoiAppearanceSpec("face", "face.cheek", "Cheek", ChaListDefine.CategoryNo.mt_cheek),
			new KoiAppearanceSpec("face", "face.lip", "Lip", ChaListDefine.CategoryNo.mt_lip),
			new KoiAppearanceSpec("face", "face.lipline", "Lip Line", ChaListDefine.CategoryNo.mt_lipline),
			new KoiAppearanceSpec("face", "face.paint.0", "Face Paint 1", ChaListDefine.CategoryNo.mt_face_paint),
			new KoiAppearanceSpec("face", "face.paint.1", "Face Paint 2", ChaListDefine.CategoryNo.mt_face_paint),
			new KoiAppearanceSpec("body", "body.detail", "Body Detail", ChaListDefine.CategoryNo.mt_body_detail),
			new KoiAppearanceSpec("body", "body.paint.0", "Body Paint 1", ChaListDefine.CategoryNo.mt_body_paint),
			new KoiAppearanceSpec("body", "body.paint.1", "Body Paint 2", ChaListDefine.CategoryNo.mt_body_paint),
			new KoiAppearanceSpec("body", "body.sunburn", "Sunburn", ChaListDefine.CategoryNo.mt_sunburn),
			new KoiAppearanceSpec("body", "body.nip", "Nipples", ChaListDefine.CategoryNo.mt_nip),
			new KoiAppearanceSpec("body", "body.underhair", "Underhair", ChaListDefine.CategoryNo.mt_underhair),
			new KoiAppearanceSpec("hair", "hair.back", "Back Hair", ChaListDefine.CategoryNo.bo_hair_b),
			new KoiAppearanceSpec("hair", "hair.front", "Front Hair", ChaListDefine.CategoryNo.bo_hair_f),
			new KoiAppearanceSpec("hair", "hair.side", "Side Hair", ChaListDefine.CategoryNo.bo_hair_s),
			new KoiAppearanceSpec("hair", "hair.option", "Hair Option", ChaListDefine.CategoryNo.bo_hair_o)
		}.AsReadOnly();

		internal static KoiAppearanceSpec Find(string key)
		{
			for (int i = 0; i < Selectors.Count; i++)
			{
				if (Selectors[i].Key == key)
				{
					return Selectors[i];
				}
			}
			return null;
		}
	}
	internal sealed class KoiProviderSnapshot : IDisposable
	{
		private sealed class NativeTextureBinding
		{
			private readonly string member;

			private readonly int row;

			private readonly int column;

			internal NativeTextureBinding(string member, int row, int column)
			{
				this.member = member;
				this.row = row;
				this.column = column;
			}

			internal Texture Read(ChaControl c)
			{
				object obj = Get(c, member);
				if (row >= 0)
				{
					if (!(obj is Array { Rank: 2 } array) || row >= array.GetLength(0) || column >= array.GetLength(1))
					{
						return null;
					}
					obj = array.GetValue(row, column);
				}
				if (obj == null)
				{
					return null;
				}
				MethodInfo method = obj.GetType().GetMethod("GetCreateTexture", BindingFlags.Instance | BindingFlags.Public);
				if (!(method == null))
				{
					return method.Invoke(obj, null) as Texture;
				}
				return null;
			}
		}

		private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		private readonly int region;

		private readonly int hairSlot;

		private readonly HashSet<string> hairBones = new HashSet<string>(StringComparer.Ordinal);

		private readonly Dictionary<string, Material[]> materials = new Dictionary<string, Material[]>();

		private readonly Dictionary<string, int[]> generatedTextures = new Dictionary<string, int[]>();

		private readonly Dictionary<string, Texture[]> originalTextures = new Dictionary<string, Texture[]>();

		private readonly Dictionary<string, object[]> rendererState = new Dictionary<string, object[]>();

		private static readonly string[] RendererProperties = new string[5] { "enabled", "shadowCastingMode", "receiveShadows", "lightProbeUsage", "reflectionProbeUsage" };

		private readonly HashSet<string> bodyMaterialNames = new HashSet<string>(StringComparer.Ordinal);

		private readonly List<NativeTextureBinding> nativeTextures = new List<NativeTextureBinding>();

		private readonly HashSet<string> materialNames = new HashSet<string>(StringComparer.Ordinal);

		private readonly HashSet<string> rendererNames = new HashSet<string>(StringComparer.Ordinal);

		private readonly Dictionary<string, byte[]> records = new Dictionary<string, byte[]>();

		private readonly Dictionary<int, byte[]> textures = new Dictionary<int, byte[]>();

		private readonly HashSet<string> headBones = new HashSet<string>(StringComparer.Ordinal);

		private readonly HashSet<string> bodyBones = new HashSet<string>(StringComparer.Ordinal);

		private readonly HashSet<string> sectionBones;

		private readonly bool bonesOnly;

		private byte[] bones;

		private Type meType;

		private Type boneType;

		private Type boneListType;

		private Type overlayBridge;

		private byte[] clothingOverlays;

		private readonly Dictionary<string, byte[]> skinOverlays = new Dictionary<string, byte[]>();

		internal static void ApplyCardMaterials(ChaControl c, ChaFileControl card, int region, Action applyNative, Func<int, bool> nativeWillRebuild = null)
		{
			Type type = FindType("KK_Plugins.MaterialEditor.MaterialEditorCharaController");
			Component component = ((type == null) ? null : c.GetComponent(type));
			if (component == null)
			{
				applyNative();
				return;
			}
			PluginData extendedDataById = ExtendedSave.GetExtendedDataById((ChaFile)card, "com.deathweasel.bepinex.materialeditor");
			if (extendedDataById != null && extendedDataById.version != 0)
			{
				throw new InvalidOperationException("Unsupported reference Material Editor data version.");
			}
			Dictionary<string, IList> dictionary = new Dictionary<string, IList>();
			Dictionary<int, byte[]> dictionary2 = new Dictionary<int, byte[]>();
			FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			foreach (FieldInfo fieldInfo in fields)
			{
				if (fieldInfo.GetValue(component) is IList && fieldInfo.Name.EndsWith("List", StringComparison.Ordinal) && (fieldInfo.Name.StartsWith("Material") || fieldInfo.Name.StartsWith("Renderer") || fieldInfo.Name.StartsWith("Projector")))
				{
					dictionary[fieldInfo.Name] = ((extendedDataById != null && extendedDataById.data.TryGetValue(fieldInfo.Name, out var value) && value != null) ? ((IList)Deserialize((byte[])value, fieldInfo.FieldType)) : ((IList)Activator.CreateInstance(fieldInfo.FieldType)));
					if (dictionary[fieldInfo.Name] == null)
					{
						throw new FormatException("Invalid reference material list: " + fieldInfo.Name);
					}
				}
			}
			if (extendedDataById != null && extendedDataById.data.TryGetValue("TextureDictionary", out var value2) && value2 != null)
			{
				dictionary2 = (Dictionary<int, byte[]>)Deserialize((byte[])value2, typeof(Dictionary<int, byte[]>));
			}
			if (dictionary2 == null)
			{
				throw new FormatException("Invalid reference material textures.");
			}
			using KoiProviderSnapshot koiProviderSnapshot = new KoiProviderSnapshot(c, region);
			Dictionary<string, IList> dictionary3 = new Dictionary<string, IList>();
			foreach (string key in dictionary.Keys)
			{
				dictionary3[key] = (IList)type.GetField(key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(component);
			}
			List<KeyValuePair<string, object>> list = KoiMaterialRecordPlan.SelectExisting(dictionary3, koiProviderSnapshot.InScope);
			IDictionary dictionary4 = Get(component, "AnimationControllerMap") as IDictionary;
			foreach (KeyValuePair<string, object> item in list)
			{
				dictionary4?.Remove(item.Value);
				dictionary3[item.Key].Remove(item.Value);
			}
			foreach (KeyValuePair<string, object> item2 in list.OrderBy((KeyValuePair<string, object> e) => (!(e.Key == "MaterialShaderList")) ? 1 : 0))
			{
				if (!(item2.Key == "MaterialCopyList") && !(item2.Key == "MaterialNamePropertyList"))
				{
					object record = OriginalRecord(item2.Value);
					WardrobeMERecordApplier.Apply(component, item2.Key, record, initializeCardShader: false, MaterialRoot(c, region));
				}
			}
			KoiNativeMaterialBaseline.Restore(c, region, list, nativeWillRebuild);
			applyNative();
			using KoiProviderSnapshot koiProviderSnapshot2 = new KoiProviderSnapshot(c, region);
			List<KeyValuePair<string, object>> list2 = KoiMaterialRecordPlan.Select(dictionary, koiProviderSnapshot2.InScope);
			koiProviderSnapshot2.records.Clear();
			koiProviderSnapshot2.textures.Clear();
			foreach (KeyValuePair<int, byte[]> item3 in dictionary2)
			{
				koiProviderSnapshot2.textures.Add(item3.Key, item3.Value);
			}
			foreach (KeyValuePair<string, IList> item4 in dictionary)
			{
				IList list3 = (IList)Activator.CreateInstance(type.GetField(item4.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FieldType);
				foreach (KeyValuePair<string, object> item5 in list2)
				{
					if (item5.Key == item4.Key)
					{
						list3.Add(item5.Value);
					}
				}
				koiProviderSnapshot2.records[item4.Key] = Serialize(list3, list3.GetType());
			}
			List<KeyValuePair<string, object>> list4 = koiProviderSnapshot2.ApplyME(c, replayAll: true);
			Dictionary<string, IList> dictionary5 = new Dictionary<string, IList>();
			foreach (KeyValuePair<string, object> item6 in list4)
			{
				if (!dictionary5.TryGetValue(item6.Key, out var value3))
				{
					value3 = (dictionary5[item6.Key] = new ArrayList());
				}
				value3.Add(item6.Value);
			}
			foreach (KeyValuePair<string, object> item7 in KoiMaterialRecordPlan.Select(dictionary5, koiProviderSnapshot2.InScope))
			{
				WardrobeMERecordApplier.Apply(component, item7.Key, item7.Value, initializeCardShader: true, MaterialRoot(c, region));
			}
		}

		private static GameObject MaterialRoot(ChaControl c, int region)
		{
			if (region != 0)
			{
				if (!(c.rendBody == null))
				{
					return c.rendBody.gameObject;
				}
				return null;
			}
			return c.objHead;
		}

		private static object OriginalRecord(object record)
		{
			object obj = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(record, null);
			string[] array = new string[5] { "Value", "ShaderName", "RenderQueue", "Offset", "Scale" };
			foreach (string text in array)
			{
				FieldInfo field = record.GetType().GetField(text + "Original", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (field != null)
				{
					Set(obj, text, field.GetValue(record));
				}
			}
			if (record.GetType().GetField("TexID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null)
			{
				Set(obj, "TexID", null);
			}
			return obj;
		}

		internal KoiProviderSnapshot(ChaControl c, int region)
			: this(c, region, -1)
		{
		}

		internal static void ApplyCardBones(ChaControl c, ChaFileControl card, int region)
		{
			Type type = FindType("KKABMX.Core.BoneController");
			if (type == null || c.GetComponent(type) == null)
			{
				return;
			}
			PluginData extendedDataById = ExtendedSave.GetExtendedDataById((ChaFile)card, "KKABMPlugin.ABMData");
			if (extendedDataById != null && extendedDataById.version != 1 && extendedDataById.version != 2)
			{
				throw new InvalidOperationException("This ABMX card format requires a newer importer.");
			}
			MethodInfo method = type.GetMethod("ReadModifiers", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[1] { typeof(PluginData) }, null);
			if (method == null)
			{
				throw new InvalidOperationException("Installed ABMX does not expose its card reader.");
			}
			IList list;
			if (extendedDataById != null && extendedDataById.version == 2)
			{
				if (!extendedDataById.data.TryGetValue("boneData", out var value) || !(value is byte[]))
				{
					throw new InvalidOperationException("The reference card's ABMX data is damaged.");
				}
				Type type2 = FindType("MessagePack.LZ4MessagePackSerializer");
				Type type3 = typeof(List<>).MakeGenericType(type.Assembly.GetType("KKABMX.Core.BoneModifier"));
				list = (IList)type2.GetMethods(BindingFlags.Static | BindingFlags.Public).First((MethodInfo m) => m.Name == "Deserialize" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(byte[])).MakeGenericMethod(type3)
					.Invoke(null, new object[1] { value });
				if (list == null)
				{
					throw new InvalidOperationException("The reference card's ABMX data is damaged.");
				}
			}
			else
			{
				if (extendedDataById != null)
				{
					if (!extendedDataById.data.TryGetValue("boneData", out var value2) || !(value2 is string) || string.IsNullOrEmpty((string)value2))
					{
						throw new InvalidOperationException("The reference card's legacy ABMX data is damaged.");
					}
					string[] array = ((string)value2).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
					for (int num = 0; num < array.Length; num++)
					{
						string[] array2 = array[num].Split(',');
						if (array2.Length < 6 || array2[1].Length < 3)
						{
							throw new FormatException("Invalid legacy ABMX row.");
						}
						for (int num2 = 3; num2 < Math.Min(array2.Length, 7); num2++)
						{
							float.Parse(array2[num2]);
						}
					}
				}
				list = (IList)method.Invoke(null, new object[1] { extendedDataById });
			}
			using KoiProviderSnapshot koiProviderSnapshot = new KoiProviderSnapshot(c, region, -1, bonesOnly: true);
			IList list2 = (IList)Activator.CreateInstance(koiProviderSnapshot.boneListType);
			foreach (object item in list)
			{
				if (koiProviderSnapshot.BoneInScope(item))
				{
					list2.Add(item);
				}
			}
			koiProviderSnapshot.bones = Serialize(list2, koiProviderSnapshot.boneListType);
			koiProviderSnapshot.ApplyBones(c);
		}

		internal static int HairSlotForSection(string section)
		{
			return section switch
			{
				"head.hair.back" => 0, 
				"head.hair.front" => 1, 
				"head.hair.side" => 2, 
				"head.hair.option" => 3, 
				_ => -1, 
			};
		}

		internal static KoiProviderSnapshot CaptureSection(ChaControl c, string section)
		{
			int num = HairSlotForSection(section);
			if (num >= 0)
			{
				return new KoiProviderSnapshot(c, 0, num);
			}
			HashSet<string> hashSet = KoiSectionBoneScope.ForSection(section);
			if (hashSet.Count != 0)
			{
				return new KoiProviderSnapshot(c, section.StartsWith("body.", StringComparison.Ordinal) ? 1 : 0, -1, bonesOnly: true, hashSet);
			}
			return null;
		}

		private KoiProviderSnapshot(ChaControl c, int region, int hairSlot, bool bonesOnly = false, HashSet<string> sectionBones = null)
		{
			this.region = region;
			this.hairSlot = hairSlot;
			this.bonesOnly = bonesOnly;
			this.sectionBones = sectionBones;
			try
			{
				if (hairSlot < 0 && !bonesOnly)
				{
					if (region != 1)
					{
						foreach (KeyValuePair<string, byte[]> item2 in KoiOverlayProvider.CaptureRegion(c, body: false))
						{
							skinOverlays[item2.Key] = item2.Value;
						}
					}
					if (region != 0)
					{
						foreach (KeyValuePair<string, byte[]> item3 in KoiOverlayProvider.CaptureRegion(c, body: true))
						{
							skinOverlays[item3.Key] = item3.Value;
						}
					}
				}
				if (hairSlot >= 0)
				{
					foreach (Transform item4 in HairTransforms(c, hairSlot))
					{
						hairBones.Add(item4.name);
					}
				}
				if (!bonesOnly)
				{
					string[] array = new string[7] { "customTexCtrlFace", "customTexCtrlBody", "ctCreateEyeL", "ctCreateEyeR", "ctCreateEyeW", "ctCreateClothes", "ctCreateClothesSub" };
					foreach (string text in array)
					{
						if (Get(c, text) is Array { Rank: 2 } array2)
						{
							for (int j = 0; j < array2.GetLength(0); j++)
							{
								for (int k = 0; k < array2.GetLength(1); k++)
								{
									nativeTextures.Add(new NativeTextureBinding(text, j, k));
								}
							}
						}
						else
						{
							nativeTextures.Add(new NativeTextureBinding(text, -1, -1));
						}
					}
					if (c.rendBody != null)
					{
						Material[] sharedMaterials = c.rendBody.sharedMaterials;
						foreach (Material material in sharedMaterials)
						{
							if (material != null)
							{
								bodyMaterialNames.Add(Clean(material.name));
							}
						}
					}
					Texture texture = ((c.customTexCtrlFace == null) ? null : c.customTexCtrlFace.GetCreateTexture());
					Texture texture2 = ((c.customTexCtrlBody == null) ? null : c.customTexCtrlBody.GetCreateTexture());
					foreach (Renderer r in ScopedRenderers(c))
					{
						Material[] sharedMaterials2 = r.sharedMaterials;
						Material[] array3 = new Material[sharedMaterials2.Length];
						string key = Path(c.transform, r.transform);
						materials.Add(key, array3);
						int[] array4 = new int[sharedMaterials2.Length];
						for (int l = 0; l < sharedMaterials2.Length; l++)
						{
							if (!(sharedMaterials2[l] != null))
							{
								continue;
							}
							array3[l] = new Material(sharedMaterials2[l]);
							array3[l].name = sharedMaterials2[l].name;
							materialNames.Add(Clean(sharedMaterials2[l].name));
							if (texture != null && sharedMaterials2[l].mainTexture == texture)
							{
								array4[l] = 1;
							}
							else if (texture2 != null && sharedMaterials2[l].mainTexture == texture2)
							{
								array4[l] = 2;
							}
							else if (sharedMaterials2[l].mainTexture is RenderTexture)
							{
								array4[l] = 3;
							}
							for (int m = 0; m < nativeTextures.Count; m++)
							{
								Texture texture3 = nativeTextures[m].Read(c);
								if (texture3 != null && sharedMaterials2[l].mainTexture == texture3)
								{
									array4[l] = m + 10;
									break;
								}
							}
						}
						generatedTextures[key] = array4;
						originalTextures[key] = sharedMaterials2.Select((Material material2) => (!(material2 == null)) ? material2.mainTexture : null).ToArray();
						rendererState[key] = RendererProperties.Select((string p) => Get(r, p)).ToArray();
						rendererNames.Add(Clean(r.name));
					}
				}
				HashSet<Transform> hashSet = new HashSet<Transform>();
				if (Get(c, "objAccessory") is GameObject[] array5)
				{
					GameObject[] array6 = array5;
					foreach (GameObject gameObject in array6)
					{
						if (gameObject != null)
						{
							Transform[] componentsInChildren = gameObject.GetComponentsInChildren<Transform>(includeInactive: true);
							foreach (Transform item in componentsInChildren)
							{
								hashSet.Add(item);
							}
						}
					}
				}
				GameObject gameObject2 = Get(c, "objHeadBone") as GameObject;
				Transform transform = c.GetComponentsInChildren<Transform>(includeInactive: true).FirstOrDefault((Transform t) => t.name == "cf_j_head" || t.name == "cm_j_head");
				foreach (Transform item5 in (IEnumerable<Transform>)((gameObject2 != null) ? gameObject2.GetComponentsInChildren<Transform>(includeInactive: true) : ((transform != null) ? transform.GetComponentsInChildren<Transform>(includeInactive: true) : new Transform[0])))
				{
					if (!hashSet.Contains(item5))
					{
						headBones.Add(item5.name);
					}
				}
				GameObject gameObject3 = Get(c, "objBodyBone") as GameObject;
				if (gameObject3 != null)
				{
					Transform[] componentsInChildren = gameObject3.GetComponentsInChildren<Transform>(includeInactive: true);
					foreach (Transform transform2 in componentsInChildren)
					{
						if (!hashSet.Contains(transform2) && !headBones.Contains(transform2.name))
						{
							bodyBones.Add(transform2.name);
						}
					}
				}
				if (!bonesOnly)
				{
					CaptureME(c);
				}
				if (region == 2 && !bonesOnly)
				{
					overlayBridge = FindType("Freelancer604.Wardrobe.WardrobeOverlay");
					if (overlayBridge != null)
					{
						clothingOverlays = CaptureClothingOverlays(c);
					}
				}
				boneType = FindType("KKABMX.Core.BoneController");
				object obj = ((boneType == null) ? null : c.GetComponent(boneType));
				if (obj == null)
				{
					return;
				}
				IList list = ReadBones(obj);
				boneListType = list.GetType();
				IList list2 = (IList)Activator.CreateInstance(boneListType);
				foreach (object item6 in list)
				{
					if (BoneInScope(item6))
					{
						list2.Add(item6);
					}
				}
				bones = Serialize(list2, boneListType);
			}
			catch
			{
				Dispose();
				throw;
			}
		}

		private void CaptureME(ChaControl c)
		{
			meType = FindType("KK_Plugins.MaterialEditor.MaterialEditorCharaController");
			object obj = ((meType == null) ? null : c.GetComponent(meType));
			if (obj == null)
			{
				return;
			}
			Dictionary<string, IList> dictionary = new Dictionary<string, IList>();
			FieldInfo[] fields = meType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			foreach (FieldInfo fieldInfo in fields)
			{
				if (fieldInfo.GetValue(obj) is IList value && fieldInfo.Name.EndsWith("List", StringComparison.Ordinal) && (fieldInfo.Name.StartsWith("Material", StringComparison.Ordinal) || fieldInfo.Name.StartsWith("Renderer", StringComparison.Ordinal) || fieldInfo.Name.StartsWith("Projector", StringComparison.Ordinal)))
				{
					dictionary[fieldInfo.Name] = value;
				}
			}
			List<KeyValuePair<string, object>> list = KoiMaterialRecordPlan.SelectExisting(dictionary, InScope);
			foreach (KeyValuePair<string, IList> item in dictionary)
			{
				Type fieldType = meType.GetField(item.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FieldType;
				IList list2 = (IList)Activator.CreateInstance(fieldType);
				foreach (KeyValuePair<string, object> item2 in list)
				{
					if (item2.Key == item.Key)
					{
						list2.Add(item2.Value);
					}
				}
				records[item.Key] = Serialize(list2, fieldType);
			}
			if (!(Get(obj, "TextureDictionary") is IDictionary dictionary2))
			{
				return;
			}
			foreach (DictionaryEntry item3 in dictionary2)
			{
				if (Get(item3.Value, "Data") is byte[] array)
				{
					textures[Convert.ToInt32(item3.Key)] = (byte[])array.Clone();
				}
			}
		}

		internal void Apply(ChaControl c)
		{
			if (hairSlot >= 0)
			{
				Dictionary<string, Renderer> dictionary = ScopedRenderers(c).ToDictionary((Renderer r) => Path(c.transform, r.transform));
				foreach (KeyValuePair<string, Material[]> material in materials)
				{
					if (!dictionary.TryGetValue(material.Key, out var value) || value.sharedMaterials.Length != material.Value.Length)
					{
						throw new InvalidOperationException("Copied hair materials cannot be matched to the current hair: " + material.Key);
					}
				}
			}
			ApplyBones(c);
			if (bonesOnly)
			{
				return;
			}
			ApplyClothingOverlays(c);
			if (skinOverlays.Count != 0)
			{
				Dictionary<string, byte[]> dictionary2 = KoiOverlayProvider.CaptureRegion(c, body: false);
				foreach (KeyValuePair<string, byte[]> item in KoiOverlayProvider.CaptureRegion(c, body: true))
				{
					dictionary2[item.Key] = item.Value;
				}
				Dictionary<string, byte[]> dictionary3 = new Dictionary<string, byte[]>();
				foreach (KeyValuePair<string, byte[]> skinOverlay in skinOverlays)
				{
					if (!dictionary2.TryGetValue(skinOverlay.Key, out var value2) || !KoiSnapshotDelta.BytesEqual(value2, skinOverlay.Value))
					{
						dictionary3[skinOverlay.Key] = skinOverlay.Value;
					}
				}
				KoiOverlayProvider.ApplyRegion(c, dictionary3);
			}
			List<KeyValuePair<string, object>> list = ApplyME(c);
			foreach (Renderer item2 in ScopedRenderers(c))
			{
				string key = Path(c.transform, item2.transform);
				if (!materials.TryGetValue(key, out var value3))
				{
					continue;
				}
				Material[] array = item2.materials;
				if (array.Length != value3.Length)
				{
					Array.Resize(ref array, value3.Length);
				}
				for (int num = 0; num < value3.Length; num++)
				{
					if (value3[num] == null)
					{
						continue;
					}
					Texture mainTexture = ((array[num] == null) ? null : array[num].mainTexture);
					if (array[num] == null)
					{
						array[num] = new Material(value3[num]);
					}
					else
					{
						if (array[num].shader != value3[num].shader)
						{
							array[num].shader = value3[num].shader;
						}
						array[num].CopyPropertiesFromMaterial(value3[num]);
					}
					array[num].name = value3[num].name;
					int num2 = generatedTextures[key][num];
					if (num2 >= 10)
					{
						array[num].mainTexture = nativeTextures[num2 - 10].Read(c);
					}
					else if (num2 == 1 && c.customTexCtrlFace != null)
					{
						array[num].mainTexture = c.customTexCtrlFace.GetCreateTexture();
					}
					else if (num2 == 2 && c.customTexCtrlBody != null)
					{
						array[num].mainTexture = c.customTexCtrlBody.GetCreateTexture();
					}
					else if (num2 == 3 && value3[num].mainTexture == null)
					{
						array[num].mainTexture = mainTexture;
					}
					else if ((object)originalTextures[key][num] != null && originalTextures[key][num] == null)
					{
						array[num].mainTexture = mainTexture;
					}
				}
				item2.materials = array;
				object[] array2 = rendererState[key];
				for (int num3 = 0; num3 < array2.Length; num3++)
				{
					if (array2[num3] != null)
					{
						Set(item2, RendererProperties[num3], array2[num3]);
					}
				}
			}
			Component component = ((meType == null) ? null : c.GetComponent(meType));
			if (!(component != null))
			{
				return;
			}
			foreach (KeyValuePair<string, object> item3 in list)
			{
				WardrobeMERecordApplier.Apply(component, item3.Key, item3.Value, initializeCardShader: false, (region == 2) ? null : MaterialRoot(c, region));
			}
		}

		private byte[] CaptureClothingOverlays(ChaControl c)
		{
			ChaFileCoordinate chaFileCoordinate = new ChaFileCoordinate();
			chaFileCoordinate.LoadBytes(c.nowCoordinate.SaveBytes(), c.nowCoordinate.loadVersion);
			BindingFlags bindingAttr = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			overlayBridge.GetMethod("WriteCoordExt", bindingAttr).Invoke(null, new object[2] { c, chaFileCoordinate });
			return (byte[])overlayBridge.GetMethod("SerializeCoordExt", bindingAttr).Invoke(null, new object[1] { chaFileCoordinate });
		}

		private void ApplyClothingOverlays(ChaControl c)
		{
			if (!(overlayBridge == null) && !KoiSnapshotDelta.BytesEqual(clothingOverlays, CaptureClothingOverlays(c)))
			{
				ChaFileCoordinate chaFileCoordinate = new ChaFileCoordinate();
				chaFileCoordinate.LoadBytes(c.nowCoordinate.SaveBytes(), c.nowCoordinate.loadVersion);
				BindingFlags bindingAttr = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
				overlayBridge.GetMethod("AttachCoordExt", bindingAttr).Invoke(null, new object[2] { chaFileCoordinate, clothingOverlays });
				overlayBridge.GetMethod("ApplyToLive", bindingAttr).Invoke(null, new object[2] { c, chaFileCoordinate });
			}
		}

		private List<KeyValuePair<string, object>> ApplyME(ChaControl c, bool replayAll = false)
		{
			List<KeyValuePair<string, object>> list = new List<KeyValuePair<string, object>>();
			object controller = ((meType == null) ? null : c.GetComponent(meType));
			if (controller == null)
			{
				return list;
			}
			MethodInfo register = meType.GetMethod("SetAndGetTextureID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(byte[]) }, null);
			Dictionary<int, int> mapped = new Dictionary<int, int>();
			Func<int, int> func = delegate(int id)
			{
				if (mapped.TryGetValue(id, out var value2))
				{
					return value2;
				}
				if (!textures.TryGetValue(id, out var value3) || register == null)
				{
					throw new InvalidOperationException("Material history texture data is missing.");
				}
				value2 = Convert.ToInt32(register.Invoke(controller, new object[1] { value3 }));
				mapped[id] = value2;
				return value2;
			};
			IDictionary dictionary = Get(controller, "AnimationControllerMap") as IDictionary;
			foreach (KeyValuePair<string, byte[]> record in records)
			{
				FieldInfo field = meType.GetField(record.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (field == null)
				{
					continue;
				}
				IList list2 = field.GetValue(controller) as IList;
				IList list3 = Deserialize(record.Value, field.FieldType) as IList;
				if (list2 == null || list3 == null)
				{
					continue;
				}
				for (int num = list2.Count - 1; num >= 0; num--)
				{
					if (InTargetScope(list2[num], c))
					{
						dictionary?.Remove(list2[num]);
						list2.RemoveAt(num);
					}
				}
				foreach (object item in list3)
				{
					object obj = Get(item, "TexID");
					if (obj != null)
					{
						Set(item, "TexID", func(Convert.ToInt32(obj)));
					}
					object obj2 = Get(item, "TexAnimationDef");
					if (obj2 != null)
					{
						object obj3 = Get(obj2, "parentTexID");
						if (obj3 != null)
						{
							Set(obj2, "parentTexID", func(Convert.ToInt32(obj3)));
						}
						if (Get(obj2, "frames") is Array array)
						{
							for (int num2 = 0; num2 < array.Length; num2++)
							{
								object value = array.GetValue(num2);
								object obj4 = Get(value, "texID");
								if (obj4 != null)
								{
									Set(value, "texID", func(Convert.ToInt32(obj4)));
								}
								array.SetValue(value, num2);
							}
						}
					}
					list2.Add(item);
					if (replayAll || record.Key == "MaterialTexturePropertyList" || record.Key == "RendererPropertyList" || record.Key == "ProjectorPropertyList")
					{
						list.Add(new KeyValuePair<string, object>(record.Key, item));
					}
				}
			}
			return list;
		}

		private void ApplyBones(ChaControl c)
		{
			object obj = ((boneType == null) ? null : c.GetComponent(boneType));
			if (obj == null || bones == null)
			{
				return;
			}
			IList obj2 = (IList)Deserialize(bones, boneListType);
			IList list = ReadBones(obj);
			Dictionary<string, object> dictionary = new Dictionary<string, object>();
			foreach (object item2 in obj2)
			{
				dictionary[BoneKey(item2)] = item2;
			}
			MethodInfo method = boneType.GetMethod("RemoveModifier");
			MethodInfo method2 = boneType.GetMethod("AddModifier");
			for (int num = list.Count - 1; num >= 0; num--)
			{
				object item = list[num];
				bool num2;
				if (hairSlot < 0)
				{
					num2 = BoneInScope(item);
				}
				else
				{
					if (Get(item, "BoneLocation") != null && Convert.ToInt32(Get(item, "BoneLocation")) >= 10)
					{
						continue;
					}
					if (hairBones.Contains((string)Get(item, "BoneName")))
					{
						goto IL_0164;
					}
					num2 = HairTransforms(c, hairSlot).Any((Transform t) => t.name == (string)Get(item, "BoneName"));
				}
				if (!num2)
				{
					continue;
				}
				goto IL_0164;
				IL_0164:
				string key = BoneKey(item);
				if (dictionary.TryGetValue(key, out var value) && KoiSnapshotDelta.BytesEqual(Serialize(item, item.GetType()), Serialize(value, value.GetType())))
				{
					dictionary.Remove(key);
					continue;
				}
				method.Invoke(obj, new object[1] { item });
			}
			foreach (object value2 in dictionary.Values)
			{
				method2.Invoke(obj, new object[1] { value2 });
			}
		}

		private bool BoneInScope(object item)
		{
			if (region == 2)
			{
				return true;
			}
			object obj = Get(item, "BoneLocation");
			if (obj != null && Convert.ToInt32(obj) >= 10)
			{
				return false;
			}
			string item2 = (string)Get(item, "BoneName");
			if (sectionBones != null)
			{
				return sectionBones.Contains(item2);
			}
			if (hairSlot < 0)
			{
				return ((region == 0) ? headBones : bodyBones).Contains(item2);
			}
			return hairBones.Contains(item2);
		}

		private static string BoneKey(object item)
		{
			return Convert.ToString(Get(item, "BoneLocation")) + ":" + (string)Get(item, "BoneName");
		}

		private static IList ReadBones(object controller)
		{
			MethodInfo method = controller.GetType().GetMethod("GetAllModifiers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
			if (method == null)
			{
				return (IList)controller.GetType().GetProperty("Modifiers").GetValue(controller, null);
			}
			object obj = method.Invoke(controller, null);
			if (obj is IList)
			{
				return (IList)obj;
			}
			Type type = controller.GetType().Assembly.GetType("KKABMX.Core.BoneModifier");
			IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type));
			foreach (object item in (IEnumerable)obj)
			{
				list.Add(item);
			}
			return list;
		}

		private bool HairRecordInScope(object record)
		{
			object obj = Get(record, "ObjectType");
			object obj2 = Get(record, "Slot");
			if (obj != null && obj2 != null && Convert.ToInt32(obj) == 3)
			{
				return Convert.ToInt32(obj2) == hairSlot;
			}
			return false;
		}

		private bool InScope(object record)
		{
			if (hairSlot >= 0)
			{
				return HairRecordInScope(record);
			}
			if (region == 2)
			{
				return true;
			}
			object obj = Get(record, "ObjectType");
			if (obj == null)
			{
				return false;
			}
			switch (Convert.ToInt32(obj))
			{
			case 3:
				return region == 0;
			default:
				return false;
			case 4:
			{
				string text = Get(record, "MaterialName") as string;
				string text2 = (Get(record, "RendererName") ?? Get(record, "Renderer")) as string;
				if (region == 0 && text != null)
				{
					if (materialNames.Contains(Clean(text)))
					{
						return !bodyMaterialNames.Contains(Clean(text));
					}
					return false;
				}
				if (string.IsNullOrEmpty(text) || !materialNames.Contains(Clean(text)))
				{
					if (!string.IsNullOrEmpty(text2))
					{
						return rendererNames.Contains(Clean(text2));
					}
					return false;
				}
				return true;
			}
			}
		}

		private bool InTargetScope(object record, ChaControl c)
		{
			if (hairSlot >= 0)
			{
				return HairRecordInScope(record);
			}
			if (region == 2)
			{
				return true;
			}
			object obj = Get(record, "ObjectType");
			if (obj == null)
			{
				return false;
			}
			switch (Convert.ToInt32(obj))
			{
			case 3:
				return region == 0;
			default:
				return false;
			case 4:
			{
				string material = Get(record, "MaterialName") as string;
				if (material != null)
				{
					bool flag = c.rendBody != null && c.rendBody.sharedMaterials.Any((Material m) => m != null && Clean(m.name) == Clean(material));
					if (region != 1)
					{
						if (!flag)
						{
							return Renderers(c, 0).Any((Renderer r) => r.sharedMaterials.Any((Material m) => m != null && Clean(m.name) == Clean(material)));
						}
						return false;
					}
					return flag;
				}
				string name = (Get(record, "RendererName") ?? Get(record, "Renderer")) as string;
				return Renderers(c, region).Any((Renderer r) => Clean(r.name) == Clean(name));
			}
			}
		}

		private static IEnumerable<Renderer> Renderers(ChaControl c, int scope)
		{
			switch (scope)
			{
			case 2:
				return c.GetComponentsInChildren<Renderer>(includeInactive: true);
			case 1:
				if (!(c.rendBody == null))
				{
					return new Renderer[1] { c.rendBody };
				}
				return new Renderer[0];
			default:
			{
				List<Renderer> list = new List<Renderer>();
				if (c.objHead != null)
				{
					list.AddRange(c.objHead.GetComponentsInChildren<Renderer>(includeInactive: true));
				}
				GameObject[] objHair = c.objHair;
				foreach (GameObject gameObject in objHair)
				{
					if (gameObject != null)
					{
						list.AddRange(gameObject.GetComponentsInChildren<Renderer>(includeInactive: true));
					}
				}
				return list;
			}
			}
		}

		private IEnumerable<Renderer> ScopedRenderers(ChaControl c)
		{
			if (hairSlot < 0)
			{
				return Renderers(c, region);
			}
			GameObject gameObject = ((c.objHair != null && hairSlot < c.objHair.Length) ? c.objHair[hairSlot] : null);
			if (!(gameObject == null))
			{
				return gameObject.GetComponentsInChildren<Renderer>(includeInactive: true);
			}
			return new Renderer[0];
		}

		private static IEnumerable<Transform> HairTransforms(ChaControl c, int slot)
		{
			GameObject gameObject = ((c.objHair != null && slot < c.objHair.Length) ? c.objHair[slot] : null);
			if (!(gameObject == null))
			{
				return gameObject.GetComponentsInChildren<Transform>(includeInactive: true);
			}
			return new Transform[0];
		}

		private static string Path(Transform root, Transform item)
		{
			string text = item.name;
			while (item.parent != null && item.parent != root)
			{
				item = item.parent;
				text = item.name + "/" + text;
			}
			return text;
		}

		private static string Clean(string name)
		{
			return (name ?? "").Replace(" (Instance)", "").Trim();
		}

		private static Type FindType(string name)
		{
			return (from a in AppDomain.CurrentDomain.GetAssemblies()
				select a.GetType(name, throwOnError: false)).FirstOrDefault((Type t) => t != null);
		}

		private static object Get(object target, string name)
		{
			if (target == null)
			{
				return null;
			}
			Type type = target.GetType();
			FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null)
			{
				return field.GetValue(target);
			}
			PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (!(property == null))
			{
				return property.GetValue(target, null);
			}
			return null;
		}

		private static void Set(object target, string name, object value)
		{
			Type type = target.GetType();
			FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null)
			{
				field.SetValue(target, value);
				return;
			}
			PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property != null)
			{
				property.SetValue(target, value, null);
			}
		}

		private static MethodInfo Serializer(string name)
		{
			Type type = FindType("MessagePack.MessagePackSerializer");
			if (type == null)
			{
				throw new InvalidOperationException("MessagePack is unavailable for appearance history.");
			}
			return type.GetMethods(BindingFlags.Static | BindingFlags.Public).First((MethodInfo m) => m.Name == name && m.IsGenericMethodDefinition && m.GetParameters().Length == 1 && ((!(name == "Serialize")) ? (m.GetParameters()[0].ParameterType == typeof(byte[])) : (m.ReturnType == typeof(byte[]))));
		}

		private static byte[] Serialize(object value, Type type)
		{
			return (byte[])Serializer("Serialize").MakeGenericMethod(type).Invoke(null, new object[1] { value });
		}

		private static object Deserialize(byte[] value, Type type)
		{
			return Serializer("Deserialize").MakeGenericMethod(type).Invoke(null, new object[1] { value });
		}

		public void Dispose()
		{
			foreach (Material[] value in materials.Values)
			{
				foreach (Material material in value)
				{
					if (material != null)
					{
						UnityEngine.Object.Destroy(material);
					}
				}
			}
			materials.Clear();
		}
	}
	internal sealed class KoiMakeUpCleanupCatalogAdapter : IMakeUpCleanupCatalogAdapter, ICharacterCleanupCatalogAdapter
	{
		private sealed class Baseline
		{
			internal MakeUpSession Session;

			internal long Generation;

			internal Dictionary<string, int> Values;

			internal byte[] Clothes;

			internal bool[] HairVisibility;

			internal Baseline(MakeUpSession session, long generation, Dictionary<string, int> values, byte[] clothes, bool[] hairVisibility)
			{
				Session = session;
				Generation = generation;
				Values = values;
				Clothes = clothes;
				HairVisibility = hairVisibility;
			}
		}

		private const string EyeCategoryId = "face.eye.right";

		private const string LeftEyeCategoryId = "face.eye.left";

		private readonly KoiAppearanceAdapter adapter = new KoiAppearanceAdapter();

		private readonly Dictionary<string, IList<MakeUpSelectorOption>> options = new Dictionary<string, IList<MakeUpSelectorOption>>(StringComparer.Ordinal);

		private MakeUpSession session;

		public string SelectionToken
		{
			get
			{
				if (session != null)
				{
					return session.Character.GetInstanceID() + ":" + session.SelectionGeneration;
				}
				return string.Empty;
			}
		}

		public void SetSession(MakeUpSession value)
		{
			session = value;
			options.Clear();
		}

		public IList<MakeUpSelectorOption> OptionsFor(string id)
		{
			if (!options.TryGetValue(id, out var value))
			{
				return new List<MakeUpSelectorOption>();
			}
			return value;
		}

		public int CurrentItemId(string id)
		{
			try
			{
				return (session == null) ? (-1) : adapter.ReadId(session, id);
			}
			catch
			{
				return -1;
			}
		}

		public bool TryFindOption(int itemId, out string categoryId, out MakeUpSelectorOption option)
		{
			return TryFindOption(itemId, null, out categoryId, out option);
		}

		public bool TryFindOption(int itemId, int? resolverCategory, out string categoryId, out MakeUpSelectorOption option)
		{
			foreach (KeyValuePair<string, IList<MakeUpSelectorOption>> option2 in options)
			{
				KoiAppearanceSpec koiAppearanceSpec = KoiAppearanceCatalog.Find(option2.Key);
				if (!resolverCategory.HasValue || koiAppearanceSpec.Category == (ChaListDefine.CategoryNo)resolverCategory.Value)
				{
					option = option2.Value.FirstOrDefault((MakeUpSelectorOption x) => x.Id == itemId);
					if (option != null)
					{
						categoryId = option2.Key;
						return true;
					}
				}
			}
			categoryId = null;
			option = null;
			return false;
		}

		public bool TryFindOption(int itemId, int? resolverCategory, out string categoryId, out int resolvedCategory, out MakeUpSelectorOption option)
		{
			bool num = TryFindOption(itemId, resolverCategory, out categoryId, out option);
			resolvedCategory = ((int?)(num ? KoiAppearanceCatalog.Find(categoryId) : null)?.Category) ?? resolverCategory.GetValueOrDefault();
			return num;
		}

		public IList<CharacterCleanupCategory> GetCategories()
		{
			options.Clear();
			List<CharacterCleanupCategory> list = new List<CharacterCleanupCategory>();
			int num = 0;
			foreach (KoiAppearanceSpec spec in KoiAppearanceCatalog.Selectors)
			{
				if (string.Equals(spec.Key, "face.eye.left", StringComparison.Ordinal))
				{
					continue;
				}
				IList<MakeUpSelectorOption> list2;
				try
				{
					list2 = adapter.Options(session, spec.Key, moddedOnly: true);
				}
				catch
				{
					continue;
				}
				if (list2.Count != 0)
				{
					options[spec.Key] = list2;
					string label = (string.Equals(spec.Key, "face.eye.right", StringComparison.Ordinal) ? "Eye" : spec.Label);
					list.Add(new CharacterCleanupCategory(spec.Group, spec.Key, label, num++, list2.Select((MakeUpSelectorOption x) => new CharacterCleanupChoice(x.Id, x.Name, x.ThumbnailAssetBundle, x.ThumbnailAssetName, (int)spec.Category)).ToList()));
				}
			}
			return list;
		}

		public object CaptureBaseline()
		{
			if (session == null)
			{
				return null;
			}
			Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.Ordinal);
			foreach (KoiAppearanceSpec selector in KoiAppearanceCatalog.Selectors)
			{
				try
				{
					dictionary[selector.Key] = adapter.ReadId(session, selector.Key);
				}
				catch
				{
				}
			}
			byte[] array = new byte[session.Character.fileStatus.clothesState.Length];
			Array.Copy(session.Character.fileStatus.clothesState, array, array.Length);
			return new Baseline(session, session.SelectionGeneration, dictionary, array, CharacterHairVisibility.Capture(session.Character));
		}

		public void RestoreBaseline(object value, string categoryId)
		{
			if (!(value is Baseline baseline) || !baseline.Session.IsCurrentSelection(baseline.Session.Character, baseline.Generation))
			{
				return;
			}
			int value4;
			if (string.Equals(categoryId, "face.eye.right", StringComparison.Ordinal))
			{
				if (baseline.Values.TryGetValue("face.eye.left", out var value2) && baseline.Values.TryGetValue("face.eye.right", out var value3))
				{
					ApplyEyePair(value2, value3);
				}
			}
			else if (baseline.Values.TryGetValue(categoryId, out value4) && !MakeUpCleanupRuntime.IsItemBanned(value4) && adapter.ReadId(baseline.Session, categoryId) != value4)
			{
				adapter.ApplyId(baseline.Session, categoryId, value4);
			}
			if (CharacterCleanupPreviewPolicy.HideClothesForCharacterCategory(categoryId))
			{
				RestoreClothes(baseline);
			}
			if (CharacterCleanupPreviewPolicy.HairPartsToHide(categoryId).Count > 0)
			{
				CharacterHairVisibility.Restore(baseline.Session.Character, baseline.HairVisibility);
			}
		}

		public void RecoverAfterTermination()
		{
			if (session == null)
			{
				return;
			}
			foreach (KoiAppearanceSpec selector in KoiAppearanceCatalog.Selectors)
			{
				int itemId;
				try
				{
					itemId = adapter.ReadId(session, selector.Key);
				}
				catch
				{
					continue;
				}
				if (MakeUpCleanupRuntime.IsItemBanned(itemId))
				{
					MakeUpSelectorOption makeUpSelectorOption = adapter.Options(session, selector.Key, moddedOnly: false).FirstOrDefault((MakeUpSelectorOption x) => x.Id < 100000000 && !MakeUpCleanupRuntime.IsItemBanned(x.Id));
					if (makeUpSelectorOption != null)
					{
						adapter.ApplyId(session, selector.Key, makeUpSelectorOption.Id);
					}
				}
			}
		}

		public void Preview(string categoryId, int itemId)
		{
			if (string.Equals(categoryId, "face.eye.right", StringComparison.Ordinal))
			{
				ApplyEyePair(itemId, itemId);
			}
			else if (adapter.ReadId(session, categoryId) != itemId)
			{
				adapter.ApplyId(session, categoryId, itemId);
			}
			if (CharacterCleanupPreviewPolicy.HideClothesForCharacterCategory(categoryId))
			{
				HideClothes(session.Character);
			}
			CharacterHairVisibility.ApplyExclusivePreview(session.Character, categoryId);
		}

		public void MaintainPreviewVisibility(string categoryId)
		{
			if (session != null)
			{
				CharacterHairVisibility.ApplyExclusivePreview(session.Character, categoryId);
			}
		}

		public bool IsSelectionCurrent(string token)
		{
			return string.Equals(token, SelectionToken, StringComparison.Ordinal);
		}

		private void ApplyEyePair(int leftId, int rightId)
		{
			if (session != null && (adapter.ReadId(session, "face.eye.left") != leftId || adapter.ReadId(session, "face.eye.right") != rightId))
			{
				adapter.ApplyEyePair(session, leftId, rightId);
			}
		}

		private static void HideClothes(ChaControl c)
		{
			for (int i = 0; i < c.fileStatus.clothesState.Length; i++)
			{
				if (i != 7 && i != 8 && c.fileStatus.clothesState[i] != 2)
				{
					c.SetClothesState(i, 2);
				}
			}
		}

		private static void RestoreClothes(Baseline b)
		{
			for (int i = 0; i < Math.Min(b.Clothes.Length, b.Session.Character.fileStatus.clothesState.Length); i++)
			{
				if (b.Session.Character.fileStatus.clothesState[i] != b.Clothes[i])
				{
					b.Session.Character.SetClothesState(i, b.Clothes[i]);
				}
			}
		}
	}
	internal sealed class KoiMakeUpSection
	{
		internal string Key { get; private set; }

		internal string Label { get; private set; }

		internal int Page { get; private set; }

		internal string Icon { get; private set; }

		internal int[] ShapeIndices { get; private set; }

		internal KoiMakeUpSection(string key, string label, int page, string icon, params int[] indices)
		{
			Key = key;
			Label = label;
			Page = page;
			Icon = icon;
			ShapeIndices = indices ?? new int[0];
		}
	}
	internal static class KoiMakeUpSchema
	{
		private static readonly string[] FaceLabels = new string[52]
		{
			"Face Base W", "Face Up Z", "Face Up Y", "Face Up Size", "Face Low Z", "Face Low W", "Chin Low Y", "Chin Low Z", "Chin Y", "Chin W",
			"Chin Z", "Chin Tip Y", "Chin Tip Z", "Chin Tip W", "Cheekbone W", "Cheekbone Z", "Cheek W", "Cheek Z", "Cheek Y", "Eyebrow Y",
			"Eyebrow X", "Eyebrow Rotation Z", "Eyebrow Inner Form", "Eyebrow Outer Form", "Upper Eyelid Form 1", "Upper Eyelid Form 2", "Upper Eyelid Form 3", "Lower Eyelid Form 1", "Lower Eyelid Form 2", "Lower Eyelid Form 3",
			"Eye Y", "Eye X", "Eye Z", "Eye Tilt", "Eye H", "Eye W", "Eye Inner X", "Eye Outer Y", "Nose Tip H", "Nose Y",
			"Nose Bridge H", "Mouth Y", "Mouth W", "Mouth Z", "Upper Lip Form", "Lower Lip Form", "Mouth Corner Form", "Ear Size", "Ear Rotation Y", "Ear Rotation Z",
			"Ear Upper Form", "Ear Low Form"
		};

		private static readonly string[] BodyLabels = new string[44]
		{
			"Height", "Head Size", "Neck W", "Neck Z", "Bust Size", "Bust Y", "Bust Rotation X", "Bust X", "Bust Rotation Y", "Bust Sharp",
			"Bust Form", "Areola Bulge", "Nipple Weight", "Nipple Stand", "Body Shoulder W", "Body Shoulder Z", "Upper Body W", "Upper Body Z", "Lower Upper-Body W", "Lower Upper-Body Z",
			"Waist Y", "Belly", "Upper Waist W", "Upper Waist Z", "Lower Waist W", "Lower Waist Z", "Hip", "Hip Rotation X", "Upper Thigh W", "Upper Thigh Z",
			"Lower Thigh W", "Lower Thigh Z", "Lower Knee W", "Lower Knee Z", "Calf", "Ankle W", "Ankle Z", "Shoulder W", "Shoulder Z", "Upper Arm W",
			"Upper Arm Z", "Elbow W", "Elbow Z", "Arm Low"
		};

		private static readonly IList<KoiMakeUpSection> Sections = Array.AsReadOnly(new KoiMakeUpSection[39]
		{
			new KoiMakeUpSection("face.type_skin", "Face Type / Skin", 0, "face-type"),
			new KoiMakeUpSection("face.overlay.over", "Face Overlay 1", 0, "face-overlay"),
			new KoiMakeUpSection("face.overlay.under", "Face Overlay 2", 0, "face-overlay"),
			new KoiMakeUpSection("face.shape.whole", "Face Shape", 0, "face-whole", 0, 1, 2, 3, 4, 5),
			new KoiMakeUpSection("face.shape.chin", "Chin", 0, "face-chin", 6, 7, 8, 9, 10, 11, 12, 13),
			new KoiMakeUpSection("face.shape.cheek", "Cheek", 0, "face-cheek", 14, 15, 16, 17, 18),
			new KoiMakeUpSection("face.shape.eyebrow", "Eyebrow Shape", 0, "face-eyebrow", 19, 20, 21, 22, 23),
			new KoiMakeUpSection("face.shape.eyes", "Eyes Shape", 0, "face-eyes", 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37),
			new KoiMakeUpSection("face.shape.nose", "Nose", 0, "face-nose", 38, 39, 40),
			new KoiMakeUpSection("face.shape.mouth", "Mouth", 0, "face-mouth", 41, 42, 43, 44, 45, 46),
			new KoiMakeUpSection("face.shape.ears", "Ears", 0, "face-ears", 47, 48, 49, 50, 51),
			new KoiMakeUpSection("face.mole", "Mole", 0, "face-mole"),
			new KoiMakeUpSection("face.eye.left", "Left Eye", 0, "face-eye"),
			new KoiMakeUpSection("face.eye.right", "Right Eye", 0, "face-eye"),
			new KoiMakeUpSection("face.eye.settings", "Eye Settings / Highlights", 0, "face-eye-fx"),
			new KoiMakeUpSection("face.eyebrows", "Eyebrows", 0, "face-eyebrow"),
			new KoiMakeUpSection("face.makeup.eyeshadow", "Eye Shadow", 0, "face-eyeshadow"),
			new KoiMakeUpSection("face.makeup.cheek", "Cheek Makeup", 0, "face-cheek-makeup"),
			new KoiMakeUpSection("face.makeup.lips", "Lips", 0, "face-lips"),
			new KoiMakeUpSection("face.paint.1", "Face Paint 1", 0, "face-paint"),
			new KoiMakeUpSection("face.paint.2", "Face Paint 2", 0, "face-paint"),
			new KoiMakeUpSection("body.shape.breast", "Breast", 1, "body-breast", 4, 5, 6, 7, 8, 9, 10, 11, 12, 13),
			new KoiMakeUpSection("body.shape.upper", "Upper Body", 1, "body-upper", 0, 1, 2, 3, 14, 15, 16, 17, 18, 19),
			new KoiMakeUpSection("body.shape.lower", "Lower Body", 1, "body-lower", 20, 21, 22, 23, 24, 25, 26, 27),
			new KoiMakeUpSection("body.shape.arms", "Arms", 1, "body-arms", 37, 38, 39, 40, 41, 42, 43),
			new KoiMakeUpSection("body.shape.legs", "Legs", 1, "body-legs", 28, 29, 30, 31, 32, 33, 34, 35, 36),
			new KoiMakeUpSection("body.skin", "Skin", 1, "body-skin"),
			new KoiMakeUpSection("body.sunburn", "Sunburn", 1, "body-sunburn"),
			new KoiMakeUpSection("body.nipples", "Nipples", 1, "body-nipples"),
			new KoiMakeUpSection("body.underhair", "Underhair", 1, "body-underhair"),
			new KoiMakeUpSection("body.nails", "Nails", 1, "body-nails"),
			new KoiMakeUpSection("body.paint.1", "Body Paint 1", 1, "body-paint"),
			new KoiMakeUpSection("body.paint.2", "Body Paint 2", 1, "body-paint"),
			new KoiMakeUpSection("body.overlay.over", "Body Overlay 1", 1, "body-overlay"),
			new KoiMakeUpSection("body.overlay.under", "Body Overlay 2", 1, "body-overlay"),
			new KoiMakeUpSection("head.hair.back", "Back Hair", 2, "hair-back"),
			new KoiMakeUpSection("head.hair.front", "Front Hair", 2, "hair-front"),
			new KoiMakeUpSection("head.hair.side", "Side Hair", 2, "hair-side"),
			new KoiMakeUpSection("head.hair.option", "Option / Extension Hair", 2, "hair-option")
		});

		private static readonly IList<string> Actions = Array.AsReadOnly(new string[10] { "LoadReference", "CopyAll", "ImportHead", "ImportBody", "ImportMakeup", "ImportClothes", "ImportAccessories", "Snapshot", "CopyReferencePortrait", "SaveCharacter" });

		internal static IList<KoiMakeUpSection> AllSections => Sections;

		internal static IList<string> HomeActions => Actions;

		internal static IList<string> SectionKeys(int page)
		{
			List<string> list = new List<string>();
			foreach (KoiMakeUpSection section in Sections)
			{
				if (section.Page == page)
				{
					list.Add(section.Key);
				}
			}
			return list.AsReadOnly();
		}

		internal static int CountDistinctShapeIndices(bool face)
		{
			HashSet<int> hashSet = new HashSet<int>();
			foreach (KoiMakeUpSection section in Sections)
			{
				if ((face && section.Page == 0) || (!face && section.Page == 1))
				{
					int[] shapeIndices = section.ShapeIndices;
					foreach (int item in shapeIndices)
					{
						hashSet.Add(item);
					}
				}
			}
			return hashSet.Count;
		}

		internal static string ShapeLabel(bool face, int index)
		{
			string[] array = (face ? FaceLabels : BodyLabels);
			if (index < 0 || index >= array.Length)
			{
				return string.Empty;
			}
			return array[index];
		}
	}
	internal static class KoiMaterialRecordPlan
	{
		internal static bool RequiresNativeRefresh(string key, object record)
		{
			switch (key)
			{
			case "MaterialTexturePropertyList":
				return Get(record, "TexID") != null;
			default:
				return false;
			case "MaterialCopyList":
			case "MaterialNamePropertyList":
			case "MaterialShaderList":
				return true;
			}
		}

		internal static List<KeyValuePair<string, object>> SelectExisting(IDictionary<string, IList> lists, Func<object, bool> owns)
		{
			HashSet<string> aliases = new HashSet<string>(StringComparer.Ordinal);
			bool flag;
			do
			{
				flag = false;
				foreach (KeyValuePair<string, IList> list in lists)
				{
					if (list.Key != "MaterialCopyList" && list.Key != "MaterialNamePropertyList")
					{
						continue;
					}
					foreach (object item in list.Value)
					{
						string text = Get(item, "MaterialName") as string;
						string text2 = Get(item, (list.Key == "MaterialCopyList") ? "MaterialCopyName" : "Value") as string;
						object obj = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(item, null);
						FieldInfo field = item.GetType().GetField("MaterialName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						if (field != null)
						{
							field.SetValue(obj, text2);
						}
						if (owns(item) || owns(obj) || aliases.Contains(Key(item, text)) || aliases.Contains(Key(item, text2)))
						{
							if (!string.IsNullOrEmpty(text))
							{
								flag |= aliases.Add(Key(item, text));
							}
							if (!string.IsNullOrEmpty(text2))
							{
								flag |= aliases.Add(Key(item, text2));
							}
						}
					}
				}
			}
			while (flag);
			return Select(lists, (object record) => owns(record) || aliases.Contains(Key(record, Get(record, "MaterialName") as string)));
		}

		internal static List<KeyValuePair<string, object>> Select(IDictionary<string, IList> lists, Func<object, bool> owns)
		{
			HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
			List<KeyValuePair<string, object>> list = new List<KeyValuePair<string, object>>();
			List<KeyValuePair<string, object>> list2 = new List<KeyValuePair<string, object>>();
			foreach (KeyValuePair<string, IList> list4 in lists)
			{
				foreach (object item2 in list4.Value)
				{
					if (list4.Key == "MaterialCopyList" || list4.Key == "MaterialNamePropertyList")
					{
						list2.Add(new KeyValuePair<string, object>(list4.Key, item2));
					}
				}
			}
			bool flag;
			do
			{
				flag = false;
				int num = 0;
				while (num < list2.Count)
				{
					KeyValuePair<string, object> item = list2[num];
					object record = item.Value;
					if (!owns(record) && !hashSet.Contains(Key(record, Get(record, "MaterialName") as string)))
					{
						num++;
						continue;
					}
					string source = Key(record, Get(record, "MaterialName") as string);
					if (list2.Any((KeyValuePair<string, object> other) => other.Value != record && Key(other.Value, Get(other.Value, (other.Key == "MaterialCopyList") ? "MaterialCopyName" : "Value") as string) == source))
					{
						num++;
						continue;
					}
					string text = Get(record, (item.Key == "MaterialCopyList") ? "MaterialCopyName" : "Value") as string;
					if (!string.IsNullOrEmpty(text))
					{
						hashSet.Add(Key(record, text));
					}
					list.Add(item);
					list2.RemoveAt(num);
					flag = true;
				}
			}
			while (flag);
			List<KeyValuePair<string, object>> list3 = new List<KeyValuePair<string, object>>(list);
			string[] array = new string[2] { "shader", "properties" };
			foreach (string text2 in array)
			{
				foreach (KeyValuePair<string, IList> list5 in lists)
				{
					if (list5.Key == "MaterialCopyList" || list5.Key == "MaterialNamePropertyList" || list5.Key == "MaterialShaderList" != (text2 == "shader"))
					{
						continue;
					}
					foreach (object item3 in list5.Value)
					{
						if (owns(item3) || hashSet.Contains(Key(item3, Get(item3, "MaterialName") as string)))
						{
							list3.Add(new KeyValuePair<string, object>(list5.Key, item3));
						}
					}
				}
			}
			return list3;
		}

		private static string Key(object record, string name)
		{
			return Convert.ToString(Get(record, "ObjectType")) + ":" + Convert.ToString(Get(record, "Slot")) + ":" + Clean(name);
		}

		internal static string Clean(string name)
		{
			return (name ?? "").Replace("(Instance)", "").Trim();
		}

		internal static object Get(object value, string name)
		{
			if (value == null)
			{
				return null;
			}
			FieldInfo field = value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (!(field != null))
			{
				return value.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(value, null);
			}
			return field.GetValue(value);
		}
	}
	internal sealed class KoiNativeAppearanceSnapshot
	{
		private readonly byte[] custom;

		private readonly byte[] coordinate;

		private readonly Version coordinateVersion;

		private readonly ChaFileParameter parameter;

		private readonly byte[][] coordinates;

		private readonly Version[] coordinateVersions;

		private readonly int coordinateType;

		private readonly PluginData naturalBody;

		private readonly PluginData pushupClothing;

		private readonly PluginData[] pushupCoordinateStates;

		internal KoiNativeAppearanceSnapshot(ChaControl c, bool completeHome = false)
		{
			custom = c.chaFile.custom.SaveBytes();
			coordinate = c.nowCoordinate.SaveBytes();
			coordinateVersion = c.nowCoordinate.loadVersion;
			naturalBody = KoiPushupCompatibility.CaptureBody(c);
			pushupClothing = KoiPushupCompatibility.CaptureCoordinate(c);
			if (completeHome)
			{
				parameter = new ChaFileParameter();
				parameter.Copy(c.chaFile.parameter);
				coordinateType = c.fileStatus.coordinateType;
				coordinates = new byte[c.chaFile.coordinate.Length][];
				coordinateVersions = new Version[coordinates.Length];
				for (int i = 0; i < coordinates.Length; i++)
				{
					coordinates[i] = c.chaFile.coordinate[i].SaveBytes();
					coordinateVersions[i] = c.chaFile.coordinate[i].loadVersion;
				}
				if (coordinateType >= 0 && coordinateType < coordinates.Length)
				{
					coordinates[coordinateType] = coordinate;
					coordinateVersions[coordinateType] = coordinateVersion;
				}
				pushupCoordinateStates = KoiPushupCompatibility.CaptureAllCoordinates(c, coordinates.Length);
			}
		}

		internal void Apply(ChaControl c)
		{
			ChaFileCustom chaFileCustom = new ChaFileCustom();
			chaFileCustom.LoadBytes(custom, null);
			ChaFileCoordinate chaFileCoordinate = new ChaFileCoordinate();
			chaFileCoordinate.LoadBytes(coordinate, coordinateVersion);
			ApplyCustom(c, chaFileCustom, face: true, body: true, hair: true);
			KoiPushupCompatibility.ApplyBody(c, naturalBody);
			ApplyCoordinate(c, chaFileCoordinate);
			if (parameter != null)
			{
				c.chaFile.parameter.Copy(parameter);
				ChaFileCoordinate[] array = new ChaFileCoordinate[coordinates.Length];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = new ChaFileCoordinate();
					array[i].LoadBytes(coordinates[i], coordinateVersions[i]);
				}
				c.chaFile.coordinate = array;
				c.fileStatus.coordinateType = coordinateType;
			}
			if (pushupCoordinateStates != null)
			{
				KoiPushupCompatibility.ApplyAllCoordinates(c, pushupCoordinateStates);
			}
			else
			{
				KoiPushupCompatibility.ApplyClothing(c, pushupClothing, -1);
			}
		}

		internal void ApplyRegion(ChaControl c, bool body)
		{
			ChaFileCustom chaFileCustom = new ChaFileCustom();
			chaFileCustom.LoadBytes(custom, null);
			ApplyCustom(c, chaFileCustom, !body, body, !body);
			if (body)
			{
				KoiPushupCompatibility.ApplyBody(c, naturalBody);
			}
		}

		internal static ChaFileCustom CloneCustom(ChaFileCustom source)
		{
			ChaFileCustom chaFileCustom = new ChaFileCustom();
			chaFileCustom.LoadBytes(source.SaveBytes(), null);
			return chaFileCustom;
		}

		internal static void ApplyCustom(ChaControl c, ChaFileCustom target, bool face, bool body, bool hair)
		{
			if (face)
			{
				ApplyFace(c, target.face);
			}
			if (body)
			{
				ApplyBody(c, target.body);
			}
			if (hair)
			{
				ApplyHair(c, target.hair);
			}
		}

		private static void ApplyFace(ChaControl c, ChaFileFace target)
		{
			ChaFileFace fileFace = c.fileFace;
			HashSet<string> hashSet = KoiNativeDifference.Changed(fileFace, target);
			if (hashSet.Count == 0)
			{
				return;
			}
			c.chaFile.custom.face = target;
			if (hashSet.Contains("headId"))
			{
				c.ChangeHead(target.headId, forceChange: true);
			}
			if (hashSet.Contains("shapeValueFace"))
			{
				for (int i = 0; i < target.shapeValueFace.Length; i++)
				{
					if (i >= fileFace.shapeValueFace.Length || fileFace.shapeValueFace[i] != target.shapeValueFace[i])
					{
						c.SetShapeFaceValue(i, target.shapeValueFace[i]);
					}
				}
			}
			if (hashSet.Contains("skinId"))
			{
				RebuildFace(c);
			}
			Run(c, hashSet, "detailId", "ChangeSettingFaceDetail");
			Run(c, hashSet, "detailPower", "ChangeSettingFaceDetailPower");
			Run(c, hashSet, "cheekGlossPower", "ChangeSettingCheekGlossPower");
			Run(c, hashSet, "eyebrowId", "ChangeSettingEyebrow");
			Run(c, hashSet, "eyebrowColor", "ChangeSettingEyebrowColor");
			Run(c, hashSet, "noseId", "ChangeSettingNose");
			if (hashSet.Contains("pupil"))
			{
				if (!KoiNativeDifference.Equal(fileFace.pupil[0], target.pupil[0]))
				{
					c.ChangeSettingEyeL(updateBaseTex: true, updateMaskTex: true, updateColorAndOffset: true);
				}
				if (!KoiNativeDifference.Equal(fileFace.pupil[1], target.pupil[1]))
				{
					c.ChangeSettingEyeR(updateBaseTex: true, updateMaskTex: true, updateColorAndOffset: true);
				}
			}
			Run(c, hashSet, "hlUpId", "ChangeSettingEyeHiUp");
			Run(c, hashSet, "hlDownId", "ChangeSettingEyeHiDown");
			Run(c, hashSet, "hlUpColor", "ChangeSettingEyeHiUpColor");
			Run(c, hashSet, "hlDownColor", "ChangeSettingEyeHiDownColor");
			if (Any(hashSet, "whiteId", "whiteBaseColor", "whiteSubColor"))
			{
				c.ChangeSettingWhiteOfEye(hashSet.Contains("whiteId"), updateColor: true);
			}
			Run(c, hashSet, "pupilWidth", "ChangeSettingEyeScaleWidth");
			Run(c, hashSet, "pupilHeight", "ChangeSettingEyeScaleHeight");
			Run(c, hashSet, "pupilX", "ChangeSettingEyePosX");
			Run(c, hashSet, "pupilY", "ChangeSettingEyePosY");
			Run(c, hashSet, "hlUpY", "ChangeSettingEyeHLUpPosY");
			Run(c, hashSet, "hlDownY", "ChangeSettingEyeHLDownPosY");
			Run(c, hashSet, "hlUpX", "ChangeSettingEyeHLUpPosX");
			Run(c, hashSet, "hlDownX", "ChangeSettingEyeHLDownPosX");
			if (Any(hashSet, "eyelineUpId", "eyelineUpWeight"))
			{
				c.ChangeSettingEyelineUp();
			}
			Run(c, hashSet, "eyelineDownId", "ChangeSettingEyelineDown");
			Run(c, hashSet, "eyelineColor", "ChangeSettingEyelineColor");
			Run(c, hashSet, "lipGlossPower", "ChangeSettingLipGlossPower");
			Run(c, hashSet, "doubleTooth", "VisibleDoubleTooth");
			if (Any(hashSet, "foregroundEyes", "foregroundEyebrow"))
			{
				c.SetForegroundEyesAndEyebrow();
			}
			if (Any(hashSet, "moleId", "moleColor", "moleLayout", "lipLineId", "lipLineColor"))
			{
				RebuildFace(c);
			}
			if (hashSet.Contains("baseMakeup"))
			{
				RefreshMakeup(c);
			}
		}

		private static void ApplyBody(ChaControl c, ChaFileBody target)
		{
			ChaFileBody fileBody = c.fileBody;
			HashSet<string> hashSet = KoiNativeDifference.Changed(fileBody, target);
			if (hashSet.Count == 0)
			{
				return;
			}
			c.chaFile.custom.body = target;
			if (hashSet.Contains("typeBone"))
			{
				c.UpdateShapeBody();
				c.UpdateShapeFace();
			}
			if (hashSet.Contains("shapeValueBody"))
			{
				for (int i = 0; i < target.shapeValueBody.Length; i++)
				{
					if (i >= fileBody.shapeValueBody.Length || fileBody.shapeValueBody[i] != target.shapeValueBody[i])
					{
						c.SetShapeBodyValue(i, target.shapeValueBody[i]);
					}
				}
			}
			if (Any(hashSet, "skinId", "skinMainColor", "skinSubColor"))
			{
				RebuildBody(c);
				RebuildFace(c);
			}
			Run(c, hashSet, "detailId", "ChangeSettingBodyDetail");
			Run(c, hashSet, "detailPower", "ChangeSettingBodyDetailPower");
			Run(c, hashSet, "skinGlossPower", "ChangeSettingSkinGlossPower");
			Run(c, hashSet, "nipId", "ChangeSettingNip");
			Run(c, hashSet, "nipColor", "ChangeSettingNipColor");
			Run(c, hashSet, "nipGlossPower", "ChangeSettingNipGlossPower");
			Run(c, hashSet, "areolaSize", "ChangeSettingAreolaSize");
			Run(c, hashSet, "underhairId", "ChangeSettingUnderhair");
			Run(c, hashSet, "underhairColor", "ChangeSettingUnderhairColor");
			Run(c, hashSet, "nailGlossPower", "ChangeSettingNailGlossPower");
			Run(c, hashSet, "drawAddLine", "VisibleAddBodyLine");
			if (Any(hashSet, "paintId", "paintColor", "paintLayout", "paintLayoutId", "sunburnId", "sunburnColor", "nailColor"))
			{
				RebuildBody(c);
			}
			if (Any(hashSet, "bustSoftness", "bustWeight"))
			{
				c.UpdateBustSoftnessAndGravity();
			}
		}

		private static void ApplyHair(ChaControl c, ChaFileHair target)
		{
			ChaFileHair fileHair = c.fileHair;
			if (KoiNativeDifference.Equal(fileHair, target))
			{
				return;
			}
			c.chaFile.custom.hair = target;
			if (fileHair.glossId != target.glossId)
			{
				c.LoadHairGlossMask();
				c.ChangeSettingHairGlossMaskAll();
			}
			for (int i = 0; i < target.parts.Length; i++)
			{
				HashSet<string> hashSet = KoiNativeDifference.Changed(fileHair.parts[i], target.parts[i]);
				if (hashSet.Contains("id"))
				{
					c.ChangeHair(i, target.parts[i].id, forceChange: true);
				}
				if (Any(hashSet, "baseColor", "startColor", "endColor"))
				{
					c.ChangeSettingHairColor(i, c00: true, c01: true, c02: true);
				}
				if (hashSet.Contains("outlineColor"))
				{
					c.ChangeSettingHairOutlineColor(i);
				}
				if (hashSet.Contains("acsColor"))
				{
					c.ChangeSettingHairAcsColor(i);
				}
				if (hashSet.Contains("length"))
				{
					c.ChangeSettingHairLength(i);
				}
				if (hashSet.Contains("noShake"))
				{
					c.ChangeShakeHair(i);
				}
				if (hashSet.Contains("glossColor"))
				{
					c.ChangeSettingHairGlossColor(i);
				}
			}
		}

		internal static void ApplyCoordinate(ChaControl c, ChaFileCoordinate target)
		{
			bool num = !KoiNativeDifference.Equal(c.nowCoordinate.clothes, target.clothes);
			bool flag = !KoiNativeDifference.Equal(c.nowCoordinate.accessory, target.accessory);
			bool flag2 = c.nowCoordinate.enableMakeup != target.enableMakeup || !KoiNativeDifference.Equal(c.nowCoordinate.makeup, target.makeup);
			if (num)
			{
				c.nowCoordinate.clothes = target.clothes;
				c.ChangeClothes(forceChange: true);
			}
			if (flag)
			{
				c.nowCoordinate.accessory = target.accessory;
				c.ChangeAccessory(forceChange: true);
			}
			if (flag2)
			{
				c.nowCoordinate.makeup = target.makeup;
				c.nowCoordinate.enableMakeup = target.enableMakeup;
				RefreshMakeup(c);
			}
		}

		private static bool Any(HashSet<string> dirty, params string[] names)
		{
			foreach (string item in names)
			{
				if (dirty.Contains(item))
				{
					return true;
				}
			}
			return false;
		}

		private static void Run(ChaControl c, HashSet<string> dirty, string property, string method)
		{
			if (dirty.Contains(property))
			{
				MethodInfo method2 = c.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
				if (method2 == null)
				{
					throw new MissingMethodException(c.GetType().FullName, method);
				}
				method2.Invoke(c, null);
			}
		}

		internal static void RefreshMakeup(ChaControl c)
		{
			c.ChangeSettingEyeShadow();
			c.ChangeSettingEyeShadowColor();
			c.ChangeSettingLip();
			c.ChangeSettingLipColor();
			RebuildFace(c);
		}

		private static void RebuildFace(ChaControl c)
		{
			c.AddUpdateCMFaceTexFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpCheek: true, inpLipLine: true, inpMole: true);
			c.AddUpdateCMFaceColorFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpCheek: true, inpLipLine: true, inpMole: true);
			c.AddUpdateCMFaceLayoutFlags(inpPaint01: true, inpPaint02: true, inpMole: true);
			c.CreateFaceTexture();
		}

		private static void RebuildBody(ChaControl c)
		{
			c.AddUpdateCMBodyTexFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpSunburn: true);
			c.AddUpdateCMBodyColorFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpSunburn: true, inpNail: true);
			c.AddUpdateCMBodyLayoutFlags(inpPaint01: true, inpPaint02: true);
			c.CreateBodyTexture();
		}
	}
	internal static class KoiNativeDifference
	{
		internal static bool Equal(object a, object b)
		{
			if (a == b)
			{
				return true;
			}
			if (a == null || b == null || a.GetType() != b.GetType())
			{
				return false;
			}
			Type type = a.GetType();
			if (type.IsValueType || type == typeof(string) || type == typeof(Version))
			{
				return a.Equals(b);
			}
			Array array = a as Array;
			Array array2 = b as Array;
			if (array != null && array2 != null)
			{
				if (array.Rank != array2.Rank)
				{
					return false;
				}
				for (int i = 0; i < array.Rank; i++)
				{
					if (array.GetLength(i) != array2.GetLength(i) || array.GetLowerBound(i) != array2.GetLowerBound(i))
					{
						return false;
					}
				}
				IEnumerator enumerator = array.GetEnumerator();
				IEnumerator enumerator2 = array2.GetEnumerator();
				while (enumerator.MoveNext())
				{
					if (!enumerator2.MoveNext() || !Equal(enumerator.Current, enumerator2.Current))
					{
						return false;
					}
				}
				return !enumerator2.MoveNext();
			}
			IList list = a as IList;
			IList list2 = b as IList;
			if (list != null && list2 != null)
			{
				if (list.Count != list2.Count)
				{
					return false;
				}
				for (int j = 0; j < list.Count; j++)
				{
					if (!Equal(list[j], list2[j]))
					{
						return false;
					}
				}
				return true;
			}
			PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
			foreach (PropertyInfo propertyInfo in properties)
			{
				if (propertyInfo.CanRead && propertyInfo.CanWrite && propertyInfo.GetIndexParameters().Length == 0 && !Equal(propertyInfo.GetValue(a, null), propertyInfo.GetValue(b, null)))
				{
					return false;
				}
			}
			FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
			foreach (FieldInfo fieldInfo in fields)
			{
				if (!Equal(fieldInfo.GetValue(a), fieldInfo.GetValue(b)))
				{
					return false;
				}
			}
			return true;
		}

		internal static HashSet<string> Changed(object before, object after)
		{
			HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
			PropertyInfo[] properties = after.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
			foreach (PropertyInfo propertyInfo in properties)
			{
				if (propertyInfo.CanRead && propertyInfo.CanWrite && propertyInfo.GetIndexParameters().Length == 0 && !Equal(propertyInfo.GetValue(before, null), propertyInfo.GetValue(after, null)))
				{
					hashSet.Add(propertyInfo.Name);
				}
			}
			return hashSet;
		}
	}
	internal static class KoiNativeMaterialBaseline
	{
		internal static void Restore(ChaControl c, int region, List<KeyValuePair<string, object>> previous, Func<int, bool> nativeWillRebuild = null)
		{
			List<KeyValuePair<string, object>> list = previous.Where((KeyValuePair<string, object> e) => KoiMaterialRecordPlan.RequiresNativeRefresh(e.Key, e.Value)).ToList();
			switch (region)
			{
			case 0:
				if (list.Any((KeyValuePair<string, object> e) => Convert.ToInt32(KoiMaterialRecordPlan.Get(e.Value, "ObjectType")) == 4) && (nativeWillRebuild == null || !nativeWillRebuild(-1)))
				{
					Complete(c.ChangeHeadAsync(c.fileFace.headId, forceChange: true, asyncFlags: false));
				}
				{
					foreach (int item in (from e in list
						where Convert.ToInt32(KoiMaterialRecordPlan.Get(e.Value, "ObjectType")) == 3
						select Convert.ToInt32(KoiMaterialRecordPlan.Get(e.Value, "Slot"))).Distinct())
					{
						if (item >= 0 && item < c.fileHair.parts.Length && (nativeWillRebuild == null || !nativeWillRebuild(item)))
						{
							Complete(c.ChangeHairAsync(item, c.fileHair.parts[item].id, forceChange: true, asyncFlags: false));
						}
					}
					break;
				}
			case 1:
			{
				if (list.Count == 0 || c.rendBody == null)
				{
					break;
				}
				Material material = null;
				try
				{
					foreach (KeyValuePair<string, object> item2 in list.Where((KeyValuePair<string, object> e) => e.Key == "MaterialShaderList"))
					{
						string text = KoiMaterialRecordPlan.Clean(KoiMaterialRecordPlan.Get(item2.Value, "MaterialName") as string);
						Material[] materials = c.rendBody.materials;
						foreach (Material material2 in materials)
						{
							if (material2 == null || KoiMaterialRecordPlan.Clean(material2.name) != text)
							{
								continue;
							}
							if (material == null)
							{
								material = LoadBodyTemplate(c);
							}
							Dictionary<string, float> dictionary = new Dictionary<string, float>();
							string[] array = new string[6] { "_alpha_a", "_alpha_b", "_liquidftop", "_liquidfbot", "_liquidbtop", "_liquidbbot" };
							foreach (string text2 in array)
							{
								if (material2.HasProperty(text2))
								{
									dictionary[text2] = material2.GetFloat(text2);
								}
							}
							material2.shader = material.shader;
							material2.CopyPropertiesFromMaterial(material);
							foreach (KeyValuePair<string, float> item3 in dictionary)
							{
								if (material2.HasProperty(item3.Key))
								{
									material2.SetFloat(item3.Key, item3.Value);
								}
							}
							if (material2.HasProperty("_AlphaMask"))
							{
								material2.SetTexture("_AlphaMask", NativeBodyMask(c));
							}
						}
					}
					foreach (KeyValuePair<string, object> item4 in list.Where((KeyValuePair<string, object> e) => e.Key == "MaterialTexturePropertyList"))
					{
						string text3 = KoiMaterialRecordPlan.Clean(KoiMaterialRecordPlan.Get(item4.Value, "MaterialName") as string);
						string text4 = "_" + ((string)KoiMaterialRecordPlan.Get(item4.Value, "Property")).TrimStart('_');
						Material[] materials = c.rendBody.materials;
						foreach (Material material3 in materials)
						{
							if (material3 == null || KoiMaterialRecordPlan.Clean(material3.name) != text3 || !material3.HasProperty(text4))
							{
								continue;
							}
							if (text4 == "_AlphaMask")
							{
								material3.SetTexture(text4, NativeBodyMask(c));
								continue;
							}
							if (material == null)
							{
								material = LoadBodyTemplate(c);
							}
							if (material.HasProperty(text4))
							{
								material3.SetTexture(text4, material.GetTexture(text4));
								continue;
							}
							Material material4 = new Material(material3.shader);
							try
							{
								material3.SetTexture(text4, material4.GetTexture(text4));
							}
							finally
							{
								UnityEngine.Object.Destroy(material4);
							}
						}
					}
					foreach (KeyValuePair<string, object> item5 in previous.Where((KeyValuePair<string, object> e) => e.Key == "MaterialNamePropertyList").Reverse())
					{
						Material[] materials = c.rendBody.materials;
						foreach (Material material5 in materials)
						{
							if (material5 != null && KoiMaterialRecordPlan.Clean(material5.name) == KoiMaterialRecordPlan.Clean(KoiMaterialRecordPlan.Get(item5.Value, "Value") as string))
							{
								material5.name = (string)KoiMaterialRecordPlan.Get(item5.Value, "MaterialName");
							}
						}
					}
					HashSet<string> copies = new HashSet<string>(from e in previous
						where e.Key == "MaterialCopyList"
						select KoiMaterialRecordPlan.Clean(KoiMaterialRecordPlan.Get(e.Value, "MaterialCopyName") as string));
					if (copies.Count != 0)
					{
						c.rendBody.materials = c.rendBody.materials.Where((Material m) => m == null || !copies.Contains(KoiMaterialRecordPlan.Clean(m.name))).ToArray();
					}
					c.AddUpdateCMBodyTexFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpSunburn: true);
					c.AddUpdateCMBodyColorFlags(inpBase: true, inpSub: true, inpPaint01: true, inpPaint02: true, inpSunburn: true, inpNail: true);
					c.AddUpdateCMBodyLayoutFlags(inpPaint01: true, inpPaint02: true);
					c.CreateBodyTexture();
					c.ChangeCustomBodyWithoutCustomTexture();
					break;
				}
				finally
				{
					if (material != null)
					{
						UnityEngine.Object.Destroy(material);
					}
				}
			}
			}
		}

		private static Texture NativeBodyMask(ChaControl c)
		{
			PropertyInfo property = typeof(ChaControl).GetProperty("texBodyAlphaMask", BindingFlags.Instance | BindingFlags.NonPublic);
			if (property == null)
			{
				throw new MissingMemberException(typeof(ChaControl).FullName, "texBodyAlphaMask");
			}
			return property.GetValue(c, null) as Texture;
		}

		private static Material LoadBodyTemplate(ChaControl c)
		{
			bool flag = c.sex == 0 && c.exType == 1;
			Material material = CommonLib.LoadAsset<Material>(flag ? "chara/54/chara_mat_00.unity3d" : "chara/mm_base.unity3d", flag ? "cm_m_ex_body_00" : ((c.sex == 0) ? "cm_m_body" : "cf_m_body"), clone: true, flag ? "add54" : "abdata");
			if (material == null)
			{
				throw new InvalidOperationException("Native body material baseline could not be loaded.");
			}
			return material;
		}

		private static void Complete(IEnumerator operation)
		{
			try
			{
				while (operation.MoveNext())
				{
					if (operation.Current is IEnumerator)
					{
						Complete((IEnumerator)operation.Current);
						continue;
					}
					throw new InvalidOperationException("Unexpected deferred native material refresh.");
				}
			}
			finally
			{
				(operation as IDisposable)?.Dispose();
			}
		}
	}
	internal static class KoiOverlayProvider
	{
		private static Type controllerType;

		private static Type textureType;

		private static PropertyInfo storage;

		private static MethodInfo get;

		private static MethodInfo set;

		private static bool initialized;

		internal static bool Available(ChaControl character)
		{
			if (!initialized)
			{
				initialized = true;
				Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
				foreach (Assembly assembly in assemblies)
				{
					controllerType = assembly.GetType("KoiSkinOverlayX.KoiSkinOverlayController", throwOnError: false);
					if (!(controllerType == null))
					{
						textureType = assembly.GetType("KoiSkinOverlayX.TexType", throwOnError: false);
						storage = controllerType.GetProperty("OverlayStorage");
						if (textureType != null && storage != null)
						{
							get = storage.PropertyType.GetMethod("GetTexture", new Type[1] { textureType });
							set = controllerType.GetMethod("SetOverlayTex", new Type[2]
							{
								typeof(byte[]),
								textureType
							});
						}
						break;
					}
				}
			}
			if (character != null && get != null && set != null)
			{
				return character.GetComponent(controllerType) != null;
			}
			return false;
		}

		private static object Kind(string key)
		{
			return Enum.Parse(textureType, (key.StartsWith("face.", StringComparison.Ordinal) ? "Face" : "Body") + (key.EndsWith(".under", StringComparison.Ordinal) ? "Under" : "Over"));
		}

		internal static Texture2D Get(ChaControl character, string key)
		{
			if (!Available(character))
			{
				return null;
			}
			object value = storage.GetValue(character.GetComponent(controllerType), null);
			if (value != null)
			{
				return get.Invoke(value, new object[1] { Kind(key) }) as Texture2D;
			}
			return null;
		}

		internal static byte[] Capture(ChaControl character, string key)
		{
			if (!Available(character))
			{
				return null;
			}
			return ReadBytes(storage.GetValue(character.GetComponent(controllerType), null), Kind(key));
		}

		internal static void Apply(ChaControl character, string key, byte[] bytes)
		{
			if (Available(character))
			{
				set.Invoke(character.GetComponent(controllerType), new object[2]
				{
					bytes,
					Kind(key)
				});
			}
		}

		internal static string[] RegionKinds(bool body)
		{
			if (body)
			{
				return new string[2] { "BodyUnder", "BodyOver" };
			}
			return new string[6] { "FaceUnder", "FaceOver", "EyeUnderL", "EyeOverL", "EyeUnderR", "EyeOverR" };
		}

		private static byte[] Encode(Texture2D image)
		{
			if (image == null)
			{
				return null;
			}
			return image.EncodeToPNG();
		}

		private static byte[] ReadBytes(object data, object kind)
		{
			if (data == null)
			{
				return null;
			}
			MethodInfo method = data.GetType().GetMethod("GetCurrentOverlayTextures", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			FieldInfo field = data.GetType().GetField("_textureStorage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			object obj = ((field == null) ? null : field.GetValue(data));
			FieldInfo fieldInfo = obj?.GetType().GetField("_data", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (method != null && fieldInfo != null)
			{
				IDictionary dictionary = method.Invoke(data, null) as IDictionary;
				IDictionary dictionary2 = fieldInfo.GetValue(obj) as IDictionary;
				if (dictionary != null && dictionary2 != null)
				{
					if (!dictionary.Contains(kind))
					{
						return null;
					}
					object obj2 = dictionary2[dictionary[kind]];
					if (obj2 != null)
					{
						if (obj2.GetType().GetProperty("Data", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(obj2, null) is byte[] array)
						{
							return (byte[])array.Clone();
						}
						return null;
					}
				}
			}
			return Encode(get.Invoke(data, new object[1] { kind }) as Texture2D);
		}

		internal static Dictionary<string, byte[]> CaptureRegion(ChaControl character, bool body)
		{
			Dictionary<string, byte[]> dictionary = new Dictionary<string, byte[]>();
			if (!Available(character))
			{
				return dictionary;
			}
			object value = storage.GetValue(character.GetComponent(controllerType), null);
			string[] array = RegionKinds(body);
			foreach (string text in array)
			{
				dictionary[text] = ReadBytes(value, Enum.Parse(textureType, text));
			}
			return dictionary;
		}

		internal static Dictionary<string, byte[]> ReadCardRegion(ChaControl character, ChaFileControl card, bool body)
		{
			Dictionary<string, byte[]> dictionary = new Dictionary<string, byte[]>();
			if (!Available(character))
			{
				return dictionary;
			}
			PluginData extendedDataById = ExtendedSave.GetExtendedDataById((ChaFile)card, "KSOX");
			object obj = null;
			try
			{
				if (extendedDataById != null && extendedDataById.version >= 2)
				{
					ValidateCardStorage(extendedDataById);
					Type propertyType = storage.PropertyType;
					object[] args = new Component[1] { character.GetComponent(controllerType) };
					obj = Activator.CreateInstance(propertyType, args);
					storage.PropertyType.GetMethod("Load", new Type[1] { typeof(PluginData) }).Invoke(obj, new object[1] { extendedDataById });
				}
				string[] array = RegionKinds(body);
				foreach (string text in array)
				{
					byte[] array2 = null;
					if (obj != null)
					{
						array2 = ReadBytes(obj, Enum.Parse(textureType, text));
					}
					else if (extendedDataById != null)
					{
						if (extendedDataById.data.TryGetValue(text, out var value))
						{
							array2 = value as byte[];
						}
						if (array2 == null && text.StartsWith("Eye", StringComparison.Ordinal) && extendedDataById.data.TryGetValue(text.Substring(0, text.Length - 1), out value))
						{
							array2 = value as byte[];
						}
					}
					dictionary[text] = ((array2 == null) ? null : ((byte[])array2.Clone()));
				}
				return dictionary;
			}
			finally
			{
				if (obj != null)
				{
					storage.PropertyType.GetMethod("Clear").Invoke(obj, null);
				}
			}
		}

		private static void ValidateCardStorage(PluginData data)
		{
			if (!data.data.TryGetValue("Lookup", out var value))
			{
				return;
			}
			if (!(value is byte[]))
			{
				throw new FormatException("Invalid reference overlay lookup.");
			}
			Type fieldType = storage.PropertyType.GetField("_allOverlayTextures", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FieldType;
			foreach (DictionaryEntry item in ((from a in AppDomain.CurrentDomain.GetAssemblies()
				select a.GetType("MessagePack.MessagePackSerializer", throwOnError: false)).First((Type t) => t != null).GetMethods(BindingFlags.Static | BindingFlags.Public).First((MethodInfo m) => m.Name == "Deserialize" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(byte[]))
				.MakeGenericMethod(fieldType)
				.Invoke(null, new object[1] { value }) as IDictionary) ?? throw new FormatException("Invalid reference overlay lookup."))
			{
				foreach (DictionaryEntry item2 in (item.Value as IDictionary) ?? throw new FormatException("Invalid reference overlay coordinate."))
				{
					if (!data.data.TryGetValue("_TextureID_" + item2.Value, out value) || !(value is byte[]) || ((byte[])value).Length == 0)
					{
						throw new FormatException("Reference overlay texture data is missing.");
					}
				}
			}
			foreach (KeyValuePair<string, object> datum in data.data)
			{
				if (datum.Key.StartsWith("_TextureID_", StringComparison.Ordinal))
				{
					int.Parse(datum.Key.Substring(11));
					if (!(datum.Value is byte[]))
					{
						throw new FormatException("Invalid reference overlay texture data.");
					}
				}
			}
		}

		internal static void ApplyRegion(ChaControl character, Dictionary<string, byte[]> values)
		{
			if (!Available(character))
			{
				return;
			}
			foreach (KeyValuePair<string, byte[]> value in values)
			{
				set.Invoke(character.GetComponent(controllerType), new object[2]
				{
					value.Value,
					Enum.Parse(textureType, value.Key)
				});
			}
		}
	}
	internal static class KoiSectionBoneScope
	{
		internal static HashSet<string> ForSection(string section)
		{
			HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
			switch (section)
			{
			case "face.shape.whole":
				hashSet.Add("cf_hit_head");
				hashSet.Add("cf_J_FaceBase");
				hashSet.Add("cf_J_FaceLow_sx");
				hashSet.Add("cf_J_FaceLow_tz");
				hashSet.Add("cf_J_FaceUp_ty");
				hashSet.Add("cf_J_FaceUp_tz");
				hashSet.Add("cf_s_head");
				break;
			case "face.shape.chin":
				hashSet.Add("cf_J_Chin_Base");
				hashSet.Add("cf_J_ChinLow");
				hashSet.Add("cf_J_ChinTip_Base");
				hashSet.Add("cf_J_MouthCavity");
				break;
			case "face.shape.cheek":
				hashSet.Add("cf_J_CheekLow_s_L");
				hashSet.Add("cf_J_CheekLow_s_R");
				hashSet.Add("cf_J_CheekUp_s_L");
				hashSet.Add("cf_J_CheekUp_s_R");
				hashSet.Add("cf_J_CheekUp2_L");
				hashSet.Add("cf_J_CheekUp2_R");
				hashSet.Add("cf_J_CheekUpBase");
				break;
			case "face.shape.eyebrow":
				hashSet.Add("cf_J_Mayu_L");
				hashSet.Add("cf_J_Mayu_R");
				hashSet.Add("cf_J_MayuMid_s_L");
				hashSet.Add("cf_J_MayuMid_s_R");
				hashSet.Add("cf_J_MayuTip_s_L");
				hashSet.Add("cf_J_MayuTip_s_R");
				break;
			case "face.shape.eyes":
				hashSet.Add("cf_J_Eye_rz_L");
				hashSet.Add("cf_J_Eye_rz_R");
				hashSet.Add("cf_J_Eye_tx_L");
				hashSet.Add("cf_J_Eye_tx_R");
				hashSet.Add("cf_J_Eye_tz");
				hashSet.Add("cf_J_Eye01_s_L");
				hashSet.Add("cf_J_Eye01_s_R");
				hashSet.Add("cf_J_Eye02_s_L");
				hashSet.Add("cf_J_Eye02_s_R");
				hashSet.Add("cf_J_Eye03_s_L");
				hashSet.Add("cf_J_Eye03_s_R");
				hashSet.Add("cf_J_Eye04_s_L");
				hashSet.Add("cf_J_Eye04_s_R");
				hashSet.Add("cf_J_Eye05_s_L");
				hashSet.Add("cf_J_Eye05_s_R");
				hashSet.Add("cf_J_Eye06_s_L");
				hashSet.Add("cf_J_Eye06_s_R");
				hashSet.Add("cf_J_Eye07_s_L");
				hashSet.Add("cf_J_Eye07_s_R");
				hashSet.Add("cf_J_Eye08_s_L");
				hashSet.Add("cf_J_Eye08_s_R");
				break;
			case "face.shape.nose":
				hashSet.Add("cf_J_megane_rx_ear");
				hashSet.Add("cf_J_Nose_tip");
				hashSet.Add("cf_J_NoseBase_rx");
				hashSet.Add("cf_J_NoseBridge_rx");
				hashSet.Add("cf_J_NoseBridge_ty");
				break;
			case "face.shape.mouth":
				hashSet.Add("cf_J_Mouth_L");
				hashSet.Add("cf_J_Mouth_R");
				hashSet.Add("cf_J_MouthBase_rx");
				hashSet.Add("cf_J_MouthBase_ty");
				hashSet.Add("cf_J_MouthLow");
				hashSet.Add("cf_J_Mouthup");
				break;
			case "face.shape.ears":
				hashSet.Add("cf_J_EarBase_ry_L");
				hashSet.Add("cf_J_EarBase_ry_R");
				hashSet.Add("cf_J_EarLow_L");
				hashSet.Add("cf_J_EarLow_R");
				hashSet.Add("cf_J_EarUp_L");
				hashSet.Add("cf_J_EarUp_R");
				break;
			case "body.shape.upper":
				hashSet.Add("cf_hit_shoulder_L");
				hashSet.Add("cf_hit_shoulder_R");
				hashSet.Add("cf_hit_spine01");
				hashSet.Add("cf_hit_spine02_L");
				hashSet.Add("cf_n_height");
				hashSet.Add("cf_s_neck");
				hashSet.Add("cf_s_shoulder02_L");
				hashSet.Add("cf_s_shoulder02_R");
				hashSet.Add("cf_s_spine01");
				hashSet.Add("cf_s_spine02");
				hashSet.Add("cf_s_spine03");
				break;
			case "body.shape.lower":
				hashSet.Add("cf_d_kokan");
				hashSet.Add("cf_hit_berry");
				hashSet.Add("cf_hit_siri_L");
				hashSet.Add("cf_hit_siri_R");
				hashSet.Add("cf_hit_waist_L");
				hashSet.Add("cf_j_ana");
				hashSet.Add("cf_j_kokan");
				hashSet.Add("cf_j_spine01");
				hashSet.Add("cf_j_spine02");
				hashSet.Add("cf_j_spine03");
				hashSet.Add("cf_j_waist01");
				hashSet.Add("cf_j_waist02");
				hashSet.Add("cf_s_siri_L");
				hashSet.Add("cf_s_siri_R");
				hashSet.Add("cf_s_waist01");
				hashSet.Add("cf_s_waist02");
				hashSet.Add("cm_J_dan_f_L");
				hashSet.Add("cm_J_dan_f_R");
				hashSet.Add("cm_J_dan100_00");
				hashSet.Add("cm_J_dan109_00");
				break;
			case "body.shape.breast":
				hashSet.Add("cf_d_bnip01_L");
				hashSet.Add("cf_d_bnip01_R");
				hashSet.Add("cf_d_bust01_L");
				hashSet.Add("cf_d_bust01_R");
				hashSet.Add("cf_d_bust02_L");
				hashSet.Add("cf_d_bust02_R");
				hashSet.Add("cf_d_bust03_L");
				hashSet.Add("cf_d_bust03_R");
				hashSet.Add("cf_hit_bust02_L");
				hashSet.Add("cf_hit_bust02_R");
				hashSet.Add("cf_s_bnip01_L");
				hashSet.Add("cf_s_bnip01_R");
				hashSet.Add("cf_s_bnip02_L");
				hashSet.Add("cf_s_bnip02_R");
				hashSet.Add("cf_s_bnip025_L");
				hashSet.Add("cf_s_bnip025_R");
				hashSet.Add("cf_s_bnipacc_L");
				hashSet.Add("cf_s_bnipacc_R");
				hashSet.Add("cf_s_bust00_L");
				hashSet.Add("cf_s_bust00_R");
				hashSet.Add("cf_s_bust01_L");
				hashSet.Add("cf_s_bust01_R");
				hashSet.Add("cf_s_bust02_L");
				hashSet.Add("cf_s_bust02_R");
				hashSet.Add("cf_s_bust03_L");
				hashSet.Add("cf_s_bust03_R");
				break;
			case "body.shape.arms":
				hashSet.Add("cf_hit_arm_L");
				hashSet.Add("cf_hit_arm_R");
				hashSet.Add("cf_j_arm00_L");
				hashSet.Add("cf_j_arm00_R");
				hashSet.Add("cf_j_forearm01_L");
				hashSet.Add("cf_j_forearm01_R");
				hashSet.Add("cf_j_hand_L");
				hashSet.Add("cf_j_hand_R");
				hashSet.Add("cf_j_shoulder_L");
				hashSet.Add("cf_j_shoulder_R");
				hashSet.Add("cf_s_arm01_L");
				hashSet.Add("cf_s_arm01_R");
				hashSet.Add("cf_s_arm02_L");
				hashSet.Add("cf_s_arm02_R");
				hashSet.Add("cf_s_arm03_L");
				hashSet.Add("cf_s_arm03_R");
				hashSet.Add("cf_s_forearm01_L");
				hashSet.Add("cf_s_forearm01_R");
				hashSet.Add("cf_s_forearm02_L");
				hashSet.Add("cf_s_forearm02_R");
				hashSet.Add("cf_s_hand_L");
				hashSet.Add("cf_s_hand_R");
				hashSet.Add("cf_s_wrist_L");
				hashSet.Add("cf_s_wrist_R");
				hashSet.Add("cf_j_thumb01_L");
				hashSet.Add("cf_j_thumb01_R");
				hashSet.Add("cf_j_thumb02_L");
				hashSet.Add("cf_j_thumb02_R");
				hashSet.Add("cf_j_thumb03_L");
				hashSet.Add("cf_j_thumb03_R");
				hashSet.Add("cf_j_index01_L");
				hashSet.Add("cf_j_index01_R");
				hashSet.Add("cf_j_index02_L");
				hashSet.Add("cf_j_index02_R");
				hashSet.Add("cf_j_index03_L");
				hashSet.Add("cf_j_index03_R");
				hashSet.Add("cf_j_middle01_L");
				hashSet.Add("cf_j_middle01_R");
				hashSet.Add("cf_j_middle02_L");
				hashSet.Add("cf_j_middle02_R");
				hashSet.Add("cf_j_middle03_L");
				hashSet.Add("cf_j_middle03_R");
				hashSet.Add("cf_j_ring01_L");
				hashSet.Add("cf_j_ring01_R");
				hashSet.Add("cf_j_ring02_L");
				hashSet.Add("cf_j_ring02_R");
				hashSet.Add("cf_j_ring03_L");
				hashSet.Add("cf_j_ring03_R");
				hashSet.Add("cf_j_little01_L");
				hashSet.Add("cf_j_little01_R");
				hashSet.Add("cf_j_little02_L");
				hashSet.Add("cf_j_little02_R");
				hashSet.Add("cf_j_little03_L");
				hashSet.Add("cf_j_little03_R");
				break;
			case "body.shape.legs":
				hashSet.Add("cf_hit_thigh01_L");
				hashSet.Add("cf_hit_thigh01_R");
				hashSet.Add("cf_hit_thigh02_L");
				hashSet.Add("cf_hit_thigh02_R");
				hashSet.Add("cf_j_foot_L");
				hashSet.Add("cf_j_foot_R");
				hashSet.Add("cf_j_leg01_L");
				hashSet.Add("cf_j_leg01_R");
				hashSet.Add("cf_j_leg03_L");
				hashSet.Add("cf_j_leg03_R");
				hashSet.Add("cf_j_thigh00_L");
				hashSet.Add("cf_j_thigh00_R");
				hashSet.Add("cf_j_toes_L");
				hashSet.Add("cf_j_toes_R");
				hashSet.Add("cf_s_leg01_L");
				hashSet.Add("cf_s_leg01_R");
				hashSet.Add("cf_s_leg02_L");
				hashSet.Add("cf_s_leg02_R");
				hashSet.Add("cf_s_leg03_L");
				hashSet.Add("cf_s_leg03_R");
				hashSet.Add("cf_s_thigh01_L");
				hashSet.Add("cf_s_thigh01_R");
				hashSet.Add("cf_s_thigh02_L");
				hashSet.Add("cf_s_thigh02_R");
				hashSet.Add("cf_s_thigh03_L");
				hashSet.Add("cf_s_thigh03_R");
				break;
			case "body.nipples":
				hashSet.Add("cf_d_bnip01_L");
				hashSet.Add("cf_d_bnip01_R");
				hashSet.Add("cf_s_bnip01_L");
				hashSet.Add("cf_s_bnip01_R");
				hashSet.Add("cf_s_bnip02_L");
				hashSet.Add("cf_s_bnip02_R");
				hashSet.Add("cf_s_bnip025_L");
				hashSet.Add("cf_s_bnip025_R");
				hashSet.Add("cf_s_bnipacc_L");
				hashSet.Add("cf_s_bnipacc_R");
				break;
			}
			return hashSet;
		}
	}
	internal static class KoiSnapshotDelta
	{
		internal static void Apply<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> desired, Func<TKey, TValue> read, Action<TKey, TValue> write, Func<TValue, TValue, bool> equal)
		{
			foreach (KeyValuePair<TKey, TValue> item in desired)
			{
				if (!equal(read(item.Key), item.Value))
				{
					write(item.Key, item.Value);
				}
			}
		}

		internal static bool BytesEqual(byte[] a, byte[] b)
		{
			if (a == b)
			{
				return true;
			}
			if (a == null || b == null || a.Length != b.Length)
			{
				return false;
			}
			for (int i = 0; i < a.Length; i++)
			{
				if (a[i] != b[i])
				{
					return false;
				}
			}
			return true;
		}
	}
	internal static class KoiStudioHeightPreservation
	{
		private const string PluginId = "orange.spork.fkheightadjustuiplugin";

		private const string ControllerName = "FKHeightAdjustUI.FKHeightAdjustUICharaController";

		internal static void Prepare(ChaControl character)
		{
			Prepare(character, (character == null) ? null : character.chaFile);
		}

		internal static void Prepare(ChaControl character, ChaFileControl card)
		{
			//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00be: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fa: Expected O, but got Unknown
			if (character == null || card == null)
			{
				return;
			}
			MonoBehaviour[] components = character.GetComponents<MonoBehaviour>();
			foreach (MonoBehaviour monoBehaviour in components)
			{
				if (monoBehaviour == null || monoBehaviour.GetType().FullName != "FKHeightAdjustUI.FKHeightAdjustUICharaController")
				{
					continue;
				}
				Type type = monoBehaviour.GetType();
				PropertyInfo property = type.GetProperty("HeightAdjust", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				FieldInfo field = type.GetField("hipsChangeAmount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (!(property == null) && !(property.PropertyType != typeof(float)) && !(field == null))
				{
					float num = (float)property.GetValue(monoBehaviour, null);
					if (field.GetValue(monoBehaviour) != null)
					{
						PluginData extendedDataById = ExtendedSave.GetExtendedDataById((ChaFile)card, "orange.spork.fkheightadjustuiplugin");
						PluginData val = new PluginData
						{
							version = (extendedDataById?.version ?? 0),
							data = ((extendedDataById == null || extendedDataById.data == null) ? new Dictionary<string, object>() : new Dictionary<string, object>(extendedDataById.data))
						};
						val.data["HeightAdjust"] = num;
						ExtendedSave.SetExtendedDataById((ChaFile)card, "orange.spork.fkheightadjustuiplugin", val);
					}
				}
				break;
			}
		}
	}
}
namespace Freelancer604.Wardrobe
{
	public static class KoiPushupCompatibility
	{
		private const string PluginId = "com.deathweasel.bepinex.pushup";

		private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;

		private static readonly string[] BodyNames = new string[12]
		{
			"Size", "VerticalPosition", "HorizontalAngle", "HorizontalPosition", "VerticalAngle", "Depth", "Roundness", "AreolaDepth", "NippleWidth", "NippleDepth",
			"Softness", "Weight"
		};

		private static readonly string[] ClothNames = new string[8] { "Firmness", "Lift", "PushTogether", "Squeeze", "CenterNipples", "EnablePushup", "FlattenNipples", "UseAdvanced" };

		private static bool recalcWarningShown;

		private static bool Supported(out PluginInfo plugin)
		{
			if (!Chainloader.PluginInfos.TryGetValue("com.deathweasel.bepinex.pushup", out plugin) || (UnityEngine.Object)(object)plugin.Instance == null || plugin.Metadata == null || plugin.Metadata.Version == null)
			{
				return false;
			}
			Version version = plugin.Metadata.Version;
			if (version.Major == 1 && version.Minor == 4)
			{
				return version.Build == 0;
			}
			return false;
		}

		private static Type ControllerType(ChaControl character)
		{
			if (character == null)
			{
				return null;
			}
			if (!Supported(out var plugin))
			{
				return null;
			}
			Type type = ((object)plugin.Instance).GetType().Assembly.GetType("KK_Plugins.Pushup+PushupController", throwOnError: false);
			if (type == null || type.GetField("BaseData", BindingFlags.Instance | BindingFlags.Public) == null || type.GetProperty("CurrentBraData", BindingFlags.Instance | BindingFlags.Public) == null || type.GetProperty("CurrentTopData", BindingFlags.Instance | BindingFlags.Public) == null || type.GetMethod("RecalculateBody", BindingFlags.Instance | BindingFlags.Public, null, new Type[2]
			{
				typeof(bool),
				typeof(bool)
			}, null) == null)
			{
				return null;
			}
			return type;
		}

		private static object Controller(ChaControl character)
		{
			Type type = ControllerType(character);
			if (!(type == null))
			{
				return character.GetComponent(type);
			}
			return null;
		}

		private static object Member(object value, string name)
		{
			if (value == null)
			{
				return null;
			}
			Type type = value.GetType();
			PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
			if (property != null)
			{
				return property.GetValue(value, null);
			}
			FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public);
			if (!(field == null))
			{
				return field.GetValue(value);
			}
			return null;
		}

		private static void SetMember(object value, string name, object next)
		{
			PropertyInfo property = value.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
			if (property != null && property.CanWrite && property.PropertyType == next.GetType())
			{
				property.SetValue(value, next, null);
			}
		}

		private static void ResetCloth(object controller, string propertyName)
		{
			object obj = Member(controller, "BaseData");
			PropertyInfo property = controller.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
			if (obj != null && !(property == null) && property.CanWrite)
			{
				ConstructorInfo constructor = property.PropertyType.GetConstructor(new Type[1] { obj.GetType() });
				if (constructor != null)
				{
					property.SetValue(controller, constructor.Invoke(new object[1] { obj }), null);
				}
			}
		}

		private static PluginData Capture(object body, object bra, object top)
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Expected O, but got Unknown
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Expected O, but got Unknown
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Expected O, but got Unknown
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Expected O, but got Unknown
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_006c: Expected O, but got Unknown
			//IL_006d: Expected O, but got Unknown
			if (body == null && bra == null && top == null)
			{
				return null;
			}
			PluginData val = new PluginData
			{
				version = 1
			};
			Add(val, "Body", body, BodyNames);
			Add(val, "Bra", bra, BodyNames);
			Add(val, "Bra", bra, ClothNames);
			Add(val, "Top", top, BodyNames);
			Add(val, "Top", top, ClothNames);
			return val;
		}

		private static void Add(PluginData data, string prefix, object source, string[] names)
		{
			if (source == null)
			{
				return;
			}
			foreach (string text in names)
			{
				object obj = Member(source, text);
				if (obj is float || obj is bool)
				{
					data.data[prefix + "." + text] = obj;
				}
			}
		}

		private static bool Apply(PluginData data, string prefix, object destination, string[] names)
		{
			if (data == null || destination == null)
			{
				return false;
			}
			bool result = false;
			foreach (string text in names)
			{
				if (data.data.TryGetValue(prefix + "." + text, out var value) && value != null)
				{
					object obj = Member(destination, text);
					if (obj != null && !(obj.GetType() != value.GetType()) && !obj.Equals(value))
					{
						SetMember(destination, text, value);
						result = true;
					}
				}
			}
			return result;
		}

		public static PluginData CaptureBody(ChaControl character)
		{
			object obj = Controller(character);
			if (obj != null)
			{
				return Capture(Member(obj, "BaseData"), null, null);
			}
			return null;
		}

		public static PluginData CaptureCoordinate(ChaControl character)
		{
			object obj = Controller(character);
			if (obj != null)
			{
				return Capture(null, Member(obj, "CurrentBraData"), Member(obj, "CurrentTopData"));
			}
			return null;
		}

		public static PluginData[] CaptureAllCoordinates(ChaControl character, int count)
		{
			object obj = Controller(character);
			if (obj == null || count < 0)
			{
				return null;
			}
			IDictionary dictionary = StoredClothes(obj, "BraDataDictionary");
			IDictionary dictionary2 = StoredClothes(obj, "TopDataDictionary");
			if (dictionary == null || dictionary2 == null)
			{
				return null;
			}
			PluginData[] array = (PluginData[])(object)new PluginData[count];
			for (int i = 0; i < count; i++)
			{
				array[i] = Capture(null, dictionary.Contains(i) ? dictionary[i] : null, dictionary2.Contains(i) ? dictionary2[i] : null);
			}
			return array;
		}

		private static IDictionary StoredClothes(object controller, string name)
		{
			FieldInfo field = controller.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
			if (!(field == null))
			{
				return field.GetValue(controller) as IDictionary;
			}
			return null;
		}

		public static void ApplyAllCoordinates(ChaControl character, PluginData[] states)
		{
			object obj = Controller(character);
			if (obj == null || states == null || character.chaFile == null)
			{
				return;
			}
			MethodInfo method = obj.GetType().GetMethod("GetBraData", BindingFlags.Instance | BindingFlags.Public, null, new Type[1] { typeof(int) }, null);
			MethodInfo method2 = obj.GetType().GetMethod("GetTopData", BindingFlags.Instance | BindingFlags.Public, null, new Type[1] { typeof(int) }, null);
			IDictionary dictionary = StoredClothes(obj, "BraDataDictionary");
			IDictionary dictionary2 = StoredClothes(obj, "TopDataDictionary");
			if (method == null || method2 == null || dictionary == null || dictionary2 == null)
			{
				return;
			}
			int num = Math.Min(states.Length, character.chaFile.coordinate.Length);
			int coordinateType = character.fileStatus.coordinateType;
			for (int i = 0; i < num; i++)
			{
				PluginData val = states[i];
				if (val == null)
				{
					dictionary.Remove(i);
					dictionary2.Remove(i);
					if (i != coordinateType && character.chaFile.coordinate[i] != null)
					{
						ExtendedSave.SetExtendedDataById(character.chaFile.coordinate[i], "com.deathweasel.bepinex.pushup", (PluginData)null);
					}
					continue;
				}
				object obj2 = (val.data.ContainsKey("Bra.EnablePushup") ? method.Invoke(obj, new object[1] { i }) : null);
				object obj3 = (val.data.ContainsKey("Top.EnablePushup") ? method2.Invoke(obj, new object[1] { i }) : null);
				if (obj2 == null)
				{
					dictionary.Remove(i);
				}
				if (obj3 == null)
				{
					dictionary2.Remove(i);
				}
				Apply(val, "Bra", obj2, BodyNames);
				Apply(val, "Bra", obj2, ClothNames);
				Apply(val, "Top", obj3, BodyNames);
				Apply(val, "Top", obj3, ClothNames);
				if (i != coordinateType && character.chaFile.coordinate[i] != null)
				{
					ExtendedSave.SetExtendedDataById(character.chaFile.coordinate[i], "com.deathweasel.bepinex.pushup", BuildCoordinateData(obj2 ?? DefaultCloth(obj, "CurrentBraData"), obj3 ?? DefaultCloth(obj, "CurrentTopData")));
				}
			}
			StoreCurrentCoordinate(character, obj);
			Recalculate(character);
		}

		public static bool TryReadBodyShape(ChaControl character, int index, out float value)
		{
			value = 0f;
			if (index < 4 || index > 13)
			{
				return false;
			}
			object obj = Member(Controller(character), "BaseData");
			object obj2 = ((obj == null) ? null : Member(obj, BodyNames[index - 4]));
			if (!(obj2 is float))
			{
				return false;
			}
			value = (float)obj2;
			return true;
		}

		public static bool TryWriteBodyShape(ChaControl character, int index, float value)
		{
			if (index < 4 || index > 13)
			{
				return false;
			}
			object obj = Member(Controller(character), "BaseData");
			if (obj == null || !(Member(obj, BodyNames[index - 4]) is float))
			{
				return false;
			}
			SetMember(obj, BodyNames[index - 4], value);
			Recalculate(character);
			return true;
		}

		public static void ApplyBody(ChaControl character, PluginData data)
		{
			object obj = Controller(character);
			if (obj != null && Apply(data, "Body", Member(obj, "BaseData"), BodyNames))
			{
				Recalculate(character);
			}
		}

		public static PluginData CaptureCardBody(ChaFileControl card)
		{
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Expected O, but got Unknown
			if (card == null)
			{
				return null;
			}
			object obj = Decode(ExtendedSave.GetExtendedDataById((ChaFile)card, "com.deathweasel.bepinex.pushup"), "Pushup_BodyData", "KK_Plugins.Pushup+BodyData");
			if (obj != null)
			{
				return Capture(obj, null, null);
			}
			PluginData val = new PluginData
			{
				version = 1
			};
			ChaFileBody body = card.custom.body;
			for (int i = 4; i <= 13; i++)
			{
				val.data["Body." + BodyNames[i - 4]] = body.shapeValueBody[i];
			}
			val.data["Body.Softness"] = body.bustSoftness;
			val.data["Body.Weight"] = body.bustWeight;
			return val;
		}

		public static PluginData CaptureCardCoordinate(ChaFileCoordinate coordinate)
		{
			if (coordinate == null)
			{
				return null;
			}
			PluginData extendedDataById = ExtendedSave.GetExtendedDataById(coordinate, "com.deathweasel.bepinex.pushup");
			object bra = Decode(extendedDataById, "PushupCoordinate_BraData", "KK_Plugins.Pushup+ClothData");
			object top = Decode(extendedDataById, "PushupCoordinate_TopData", "KK_Plugins.Pushup+ClothData");
			return Capture(null, bra, top);
		}

		public static PluginData CaptureCardCoordinate(ChaFileControl card, int coordinateIndex)
		{
			if (card == null || coordinateIndex < 0 || coordinateIndex >= card.coordinate.Length)
			{
				return null;
			}
			PluginData extendedDataById = ExtendedSave.GetExtendedDataById((ChaFile)card, "com.deathweasel.bepinex.pushup");
			object bra = DecodeDictionaryEntry(extendedDataById, "Pushup_BraData", coordinateIndex);
			object top = DecodeDictionaryEntry(extendedDataById, "Pushup_TopData", coordinateIndex);
			PluginData val = CaptureCardCoordinate(card.coordinate[coordinateIndex]);
			PluginData val2 = Capture(null, bra, top);
			if (val2 == null)
			{
				return val;
			}
			if (val != null)
			{
				foreach (KeyValuePair<string, object> datum in val.data)
				{
					if (!val2.data.ContainsKey(datum.Key))
					{
						val2.data[datum.Key] = datum.Value;
					}
				}
			}
			return val2;
		}

		private static object DecodeDictionaryEntry(PluginData data, string key, int index)
		{
			if (data == null || !data.data.TryGetValue(key, out var value) || !(value is byte[]))
			{
				return null;
			}
			if (!Supported(out var plugin))
			{
				return null;
			}
			Type type = ((object)plugin.Instance).GetType().Assembly.GetType("KK_Plugins.Pushup+ClothData", throwOnError: false);
			Type type2 = SerializerType();
			if (type == null || type2 == null)
			{
				return null;
			}
			Type type3 = typeof(Dictionary<, >).MakeGenericType(typeof(int), type);
			MethodInfo[] methods = type2.GetMethods(BindingFlags.Static | BindingFlags.Public);
			foreach (MethodInfo methodInfo in methods)
			{
				if (methodInfo.Name == "Deserialize" && methodInfo.IsGenericMethodDefinition && methodInfo.GetParameters().Length == 1 && methodInfo.GetParameters()[0].ParameterType == typeof(byte[]))
				{
					try
					{
						return (!(methodInfo.MakeGenericMethod(type3).Invoke(null, new object[1] { value }) is IDictionary dictionary)) ? null : dictionary[index];
					}
					catch
					{
						return null;
					}
				}
			}
			return null;
		}

		private static object Decode(PluginData data, string key, string typeName)
		{
			if (data == null || !data.data.TryGetValue(key, out var value) || !(value is byte[]))
			{
				return null;
			}
			if (!Supported(out var plugin))
			{
				return null;
			}
			Type type = ((object)plugin.Instance).GetType().Assembly.GetType(typeName, throwOnError: false);
			if (type == null)
			{
				return null;
			}
			Type type2 = SerializerType();
			if (type2 == null)
			{
				return null;
			}
			MethodInfo[] methods = type2.GetMethods(BindingFlags.Static | BindingFlags.Public);
			foreach (MethodInfo methodInfo in methods)
			{
				if (methodInfo.Name == "Deserialize" && methodInfo.IsGenericMethodDefinition && methodInfo.GetParameters().Length == 1 && methodInfo.GetParameters()[0].ParameterType == typeof(byte[]))
				{
					try
					{
						return methodInfo.MakeGenericMethod(type).Invoke(null, new object[1] { value });
					}
					catch
					{
						return null;
					}
				}
			}
			return null;
		}

		private static byte[] Encode(object value)
		{
			if (value == null)
			{
				return null;
			}
			Type type = SerializerType();
			if (type == null)
			{
				return null;
			}
			MethodInfo[] methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public);
			foreach (MethodInfo methodInfo in methods)
			{
				if (methodInfo.Name == "Serialize" && methodInfo.IsGenericMethodDefinition && methodInfo.GetParameters().Length == 1 && methodInfo.GetParameters()[0].ParameterType.IsGenericParameter)
				{
					try
					{
						return methodInfo.MakeGenericMethod(value.GetType()).Invoke(null, new object[1] { value }) as byte[];
					}
					catch
					{
						return null;
					}
				}
			}
			return null;
		}

		private static Type SerializerType()
		{
			Type type = Type.GetType("MessagePack.MessagePackSerializer, Assembly-CSharp-firstpass", throwOnError: false) ?? Type.GetType("MessagePack.MessagePackSerializer, MessagePack", throwOnError: false);
			if (type != null)
			{
				return type;
			}
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			for (int i = 0; i < assemblies.Length; i++)
			{
				type = assemblies[i].GetType("MessagePack.MessagePackSerializer", throwOnError: false);
				if (type != null)
				{
					return type;
				}
			}
			return null;
		}

		private static void StoreCurrentCoordinate(ChaControl character, object controller)
		{
			if (character.nowCoordinate == null || character.chaFile == null)
			{
				return;
			}
			object obj = Member(controller, "CurrentBraData");
			object obj2 = Member(controller, "CurrentTopData");
			if (obj != null && obj2 != null)
			{
				PluginData val = BuildCoordinateData(obj, obj2);
				ExtendedSave.SetExtendedDataById(character.nowCoordinate, "com.deathweasel.bepinex.pushup", val);
				int coordinateType = character.fileStatus.coordinateType;
				if (coordinateType >= 0 && coordinateType < character.chaFile.coordinate.Length && character.chaFile.coordinate[coordinateType] != null && character.chaFile.coordinate[coordinateType] != character.nowCoordinate)
				{
					ExtendedSave.SetExtendedDataById(character.chaFile.coordinate[coordinateType], "com.deathweasel.bepinex.pushup", val);
				}
			}
		}

		private static PluginData BuildCoordinateData(object bra, object top)
		{
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Expected O, but got Unknown
			PluginData val = null;
			if ((Member(bra, "EnablePushup") is bool && (bool)Member(bra, "EnablePushup")) || (Member(top, "EnablePushup") is bool && (bool)Member(top, "EnablePushup")))
			{
				byte[] array = Encode(bra);
				byte[] array2 = Encode(top);
				if (array == null || array2 == null)
				{
					throw new InvalidOperationException("Installed Pushup could not serialize its clothing settings.");
				}
				val = new PluginData
				{
					version = 1
				};
				val.data["PushupCoordinate_BraData"] = array;
				val.data["PushupCoordinate_TopData"] = array2;
			}
			return val;
		}

		private static object DefaultCloth(object controller, string propertyName)
		{
			object obj = Member(controller, "BaseData");
			PropertyInfo property = controller.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
			ConstructorInfo constructorInfo = ((obj == null || property == null) ? null : property.PropertyType.GetConstructor(new Type[1] { obj.GetType() }));
			if (!(constructorInfo == null))
			{
				return constructorInfo.Invoke(new object[1] { obj });
			}
			return null;
		}

		public static void WriteLiveCoordinate(ChaControl character)
		{
			object obj = Controller(character);
			if (obj != null)
			{
				StoreCurrentCoordinate(character, obj);
			}
		}

		public static void ApplyClothing(ChaControl character, PluginData data, int slot)
		{
			ApplyClothing(character, data, slot, recalculate: true);
		}

		public static void ApplyClothing(ChaControl character, PluginData data, int slot, bool recalculate)
		{
			object obj = Controller(character);
			if (obj == null)
			{
				return;
			}
			bool flag = false;
			if (slot == -1 || slot == 2)
			{
				if (data == null || !data.data.ContainsKey("Bra.EnablePushup"))
				{
					ResetCloth(obj, "CurrentBraData");
					flag = true;
				}
				else
				{
					object destination = Member(obj, "CurrentBraData");
					flag |= Apply(data, "Bra", destination, BodyNames);
					flag |= Apply(data, "Bra", destination, ClothNames);
				}
			}
			if (slot == -1 || slot == 0)
			{
				if (data == null || !data.data.ContainsKey("Top.EnablePushup"))
				{
					ResetCloth(obj, "CurrentTopData");
					flag = true;
				}
				else
				{
					object destination2 = Member(obj, "CurrentTopData");
					flag |= Apply(data, "Top", destination2, BodyNames);
					flag |= Apply(data, "Top", destination2, ClothNames);
				}
			}
			if (flag)
			{
				StoreCurrentCoordinate(character, obj);
				if (recalculate)
				{
					Recalculate(character);
				}
			}
		}

		public static void Recalculate(ChaControl character)
		{
			object obj = Controller(character);
			if (obj == null)
			{
				return;
			}
			try
			{
				obj.GetType().GetMethod("RecalculateBody", BindingFlags.Instance | BindingFlags.Public, null, new Type[2]
				{
					typeof(bool),
					typeof(bool)
				}, null).Invoke(obj, new object[2] { true, false });
			}
			catch (Exception)
			{
				if (!recalcWarningShown)
				{
					recalcWarningShown = true;
				}
			}
		}
	}
	internal static class WardrobePseudoMakerGuards
	{
		private sealed class Reservation
		{
			internal WeakReference Character;

			internal WeakReference Coordinate;

			internal int Count;
		}

		private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

		private static FieldInfo refreshField;

		private static FieldInfo inputField;

		private static float nextProbe;

		private static bool installed;

		private static bool unsupported;

		private static bool displayInstalled;

		private static bool trimInstalled;

		private static readonly Dictionary<int, List<Reservation>> reservations = new Dictionary<int, List<Reservation>>();

		internal static void EnsureInstalled()
		{
			if (installed || unsupported || Time.realtimeSinceStartup < nextProbe)
			{
				return;
			}
			nextProbe = Time.realtimeSinceStartup + 1f;
			try
			{
				Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
				foreach (Assembly assembly in assemblies)
				{
					Type type = assembly.GetType("PseudoMaker.PseudoMaker", throwOnError: false);
					if (type == null)
					{
						continue;
					}
					bool flag = false;
					object[] customAttributes = type.GetCustomAttributes(inherit: false);
					foreach (object obj in customAttributes)
					{
						Type type2 = obj.GetType();
						if (!(type2.FullName != "BepInEx.BepInPlugin"))
						{
							object value = type2.GetProperty("GUID").GetValue(obj, null);
							object value2 = type2.GetProperty("Version").GetValue(obj, null);
							flag = (string)value == "com.rikkibalboa.bepinex.studioPseudoMaker" && value2.ToString() == "1.5.1.1";
						}
					}
					Type type3 = assembly.GetType("PseudoMaker.UI.InputFieldComponent", throwOnError: false);
					Type type4 = assembly.GetType("PseudoMaker.Hooks", throwOnError: false);
					MethodInfo methodInfo = ((type3 == null) ? null : type3.GetMethod("UpdateValue", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(float) }, null));
					MethodInfo methodInfo2 = ((type4 == null) ? null : type4.GetMethod("ChangeCoordinateTypePrefix", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(ChaControl) }, null));
					if (!flag || !BindInput(type3) || methodInfo == null || methodInfo.ReturnType != typeof(void) || methodInfo2 == null || methodInfo2.ReturnType != typeof(void))
					{
						unsupported = true;
						break;
					}
					displayInstalled = displayInstalled || Install(methodInfo, "DisplayRefreshPrefix");
					trimInstalled = trimInstalled || Install(methodInfo2, "CoordinateTrimPrefix");
					installed = displayInstalled && trimInstalled;
					break;
				}
			}
			catch (Exception)
			{
				unsupported = true;
			}
		}

		private static bool BindInput(Type type)
		{
			if (type == null)
			{
				return false;
			}
			refreshField = type.GetField("shouldNotUpdate", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			inputField = type.GetField("inputField", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (refreshField != null && refreshField.FieldType == typeof(bool) && inputField != null)
			{
				return inputField.FieldType == typeof(InputField);
			}
			return false;
		}

		private static bool DisplayRefreshPrefix(object __instance, float value)
		{
			if (refreshField == null || WardrobePseudoMakerGuards.inputField == null || !refreshField.DeclaringType.IsInstanceOfType(__instance))
			{
				return true;
			}
			if (!(bool)refreshField.GetValue(__instance))
			{
				return true;
			}
			InputField inputField = WardrobePseudoMakerGuards.inputField.GetValue(__instance) as InputField;
			if (inputField == null)
			{
				return true;
			}
			inputField.text = value.ToString("0.000");
			return false;
		}

		private static ChaFileCoordinate Current(ChaControl character)
		{
			if (character == null || character.chaFile == null || character.fileStatus == null || character.chaFile.coordinate == null)
			{
				return null;
			}
			int coordinateType = character.fileStatus.coordinateType;
			if (coordinateType >= 0 && coordinateType < character.chaFile.coordinate.Length)
			{
				return character.chaFile.coordinate[coordinateType];
			}
			return null;
		}

		internal static int CaptureReservation(ChaControl character)
		{
			ChaFileCoordinate chaFileCoordinate = Current(character);
			if (character == null || chaFileCoordinate == null || !reservations.TryGetValue(character.GetInstanceID(), out var value))
			{
				return 0;
			}
			foreach (Reservation item in value)
			{
				if (item.Character.Target == character && item.Coordinate.Target == chaFileCoordinate)
				{
					return item.Count;
				}
			}
			return 0;
		}

		internal static void Reserve(ChaControl character, int count)
		{
			ChaFileCoordinate chaFileCoordinate = Current(character);
			if (character == null || chaFileCoordinate == null || count <= 20)
			{
				return;
			}
			int count2 = Math.Max(CaptureReservation(character), count);
			ClearReservation(character);
			if (!reservations.TryGetValue(character.GetInstanceID(), out var value))
			{
				value = (reservations[character.GetInstanceID()] = new List<Reservation>());
			}
			value.Add(new Reservation
			{
				Character = new WeakReference(character),
				Coordinate = new WeakReference(chaFileCoordinate),
				Count = count2
			});
			if (reservations.Count <= 64)
			{
				return;
			}
			List<int> list2 = new List<int>();
			foreach (KeyValuePair<int, List<Reservation>> reservation in reservations)
			{
				reservation.Value.RemoveAll((Reservation reservation) => !reservation.Character.IsAlive || !reservation.Coordinate.IsAlive);
				if (reservation.Value.Count == 0)
				{
					list2.Add(reservation.Key);
				}
			}
			foreach (int item in list2)
			{
				reservations.Remove(item);
			}
		}

		internal static void RestoreReservation(ChaControl character, int count)
		{
			ClearReservation(character);
			Reserve(character, count);
		}

		internal static void ClearReservation(ChaControl character)
		{
			if (!(character == null) && reservations.TryGetValue(character.GetInstanceID(), out var value))
			{
				ChaFileCoordinate coordinate = Current(character);
				value.RemoveAll((Reservation reservation) => !reservation.Character.IsAlive || !reservation.Coordinate.IsAlive || reservation.Character.Target != character || reservation.Coordinate.Target == coordinate);
				if (value.Count == 0)
				{
					reservations.Remove(character.GetInstanceID());
				}
			}
		}

		private static bool CoordinateTrimPrefix(ChaControl __0)
		{
			int num = CaptureReservation(__0);
			ChaFileCoordinate chaFileCoordinate = Current(__0);
			ChaFileAccessory.PartsInfo[] array = ((chaFileCoordinate == null || chaFileCoordinate.accessory == null) ? null : chaFileCoordinate.accessory.parts);
			if (num > 20 && array != null)
			{
				return array.Length < num;
			}
			return true;
		}

		private static bool Install(MethodInfo target, string handler)
		{
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach (Assembly assembly in assemblies)
			{
				Type type = assembly.GetType("HarmonyLib.Harmony", throwOnError: false) ?? assembly.GetType("Harmony.HarmonyInstance", throwOnError: false);
				if (type == null)
				{
					continue;
				}
				Type type2 = assembly.GetType(type.Namespace + ".HarmonyMethod", throwOnError: false);
				if (type2 == null)
				{
					continue;
				}
				string text = "Freelancer604.VanityStudio.PseudoMaker." + typeof(WardrobePseudoMakerGuards).Assembly.GetName().Name;
				object obj = ((type.Name == "HarmonyInstance") ? type.GetMethod("Create", BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[1] { text }) : Activator.CreateInstance(type, text));
				object obj2 = Activator.CreateInstance(type2, typeof(WardrobePseudoMakerGuards).GetMethod(handler, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public);
				foreach (MethodInfo methodInfo in methods)
				{
					ParameterInfo[] parameters = methodInfo.GetParameters();
					if (!(methodInfo.Name != "Patch") && parameters.Length >= 2 && !(parameters[1].Name != "prefix"))
					{
						object[] array = new object[parameters.Length];
						array[0] = target;
						array[1] = obj2;
						methodInfo.Invoke(obj, array);
						return true;
					}
				}
			}
			return false;
		}
	}
	internal static class WardrobePseudoMakerState
	{
		internal sealed class Snapshot
		{
			internal readonly ChaControl Owner;

			internal readonly Dictionary<string, IDictionary> Records;

			internal Snapshot(ChaControl owner, Dictionary<string, IDictionary> records)
			{
				Owner = owner;
				Records = records;
			}
		}

		private sealed class Contract
		{
			internal Type ControllerType;

			internal Dictionary<string, FieldInfo> Fields;
		}

		private static readonly string[] RecordNames = new string[8] { "OriginalClothingColors", "OriginalAccessoryColors", "OriginalColors", "OriginalFloats", "OriginalAccessoryFloats", "OriginalBodyShapeValues", "OriginalFaceShapeValues", "OriginalPushupValue" };

		private static readonly string[] ReloadOmissions = new string[3] { "OriginalAccessoryColors", "OriginalAccessoryFloats", "OriginalPushupValue" };

		private static Contract bound;

		internal static Snapshot Capture(ChaControl character)
		{
			if (!TryGetController(character, out var contract, out var controller))
			{
				return null;
			}
			try
			{
				Dictionary<string, IDictionary> dictionary = new Dictionary<string, IDictionary>(StringComparer.Ordinal);
				string[] recordNames = RecordNames;
				foreach (string key in recordNames)
				{
					dictionary.Add(key, CloneDictionary((IDictionary)contract.Fields[key].GetValue(controller)));
				}
				return new Snapshot(character, dictionary);
			}
			catch
			{
				return null;
			}
		}

		internal static void Restore(ChaControl character, Snapshot snapshot)
		{
			if (snapshot == null || (object)snapshot.Owner != character || !TryGetController(character, out var contract, out var controller))
			{
				return;
			}
			try
			{
				string[] recordNames = RecordNames;
				foreach (string key in recordNames)
				{
					contract.Fields[key].SetValue(controller, CloneDictionary(snapshot.Records[key]));
				}
			}
			catch
			{
			}
		}

		internal static void PrepareFullReplacement(ChaControl character)
		{
			Mutate(character, delegate(Contract contract, object controller)
			{
				string[] reloadOmissions = ReloadOmissions;
				foreach (string key in reloadOmissions)
				{
					((IDictionary)contract.Fields[key].GetValue(controller)).Clear();
				}
			});
		}

		internal static void InvalidateHead(ChaControl character)
		{
			Mutate(character, delegate(Contract contract, object controller)
			{
				Clear(contract, controller, "OriginalFaceShapeValues");
				RemoveWhere(contract, controller, "OriginalColors", (object key) => Convert.ToInt32(key) >= 6);
				RemoveWhere(contract, controller, "OriginalFloats", delegate(object key)
				{
					int num = Convert.ToInt32(key);
					return num >= 7 && num <= 27;
				});
			});
		}

		internal static void InvalidateBody(ChaControl character)
		{
			Mutate(character, delegate(Contract contract, object controller)
			{
				Clear(contract, controller, "OriginalBodyShapeValues");
				RemoveWhere(contract, controller, "OriginalColors", (object key) => Convert.ToInt32(key) <= 5);
				RemoveWhere(contract, controller, "OriginalFloats", delegate(object key)
				{
					int num = Convert.ToInt32(key);
					return num <= 6 || num == 28;
				});
				RemoveWhere(contract, controller, "OriginalPushupValue", delegate(object key)
				{
					int num = ReadIntProperty(key, "PushupValue");
					return num >= 4 && num <= 15;
				});
			});
		}

		internal static void InvalidateFaceValues(ChaControl character, IEnumerable<int> shapeIndices, IEnumerable<int> colorTypes, IEnumerable<int> floatTypes)
		{
			InvalidateValues(character, "OriginalFaceShapeValues", shapeIndices, colorTypes, floatTypes);
		}

		internal static void InvalidateColorLabels(ChaControl character, IEnumerable<string> labels)
		{
			HashSet<int> colors = new HashSet<int>();
			if (labels != null)
			{
				using IEnumerator<string> enumerator = labels.GetEnumerator();
				while (enumerator.MoveNext())
				{
					switch (enumerator.Current)
					{
					case "Skin Main":
						colors.Add(0);
						break;
					case "Skin Sub":
						colors.Add(1);
						break;
					case "Sunburn":
						colors.Add(2);
						break;
					case "Nipples":
						colors.Add(3);
						break;
					case "Nails":
						colors.Add(4);
						break;
					case "Underhair":
						colors.Add(5);
						break;
					case "Hair 0":
					case "Hair 1":
					case "Hair 2":
					case "Hair 3":
					{
						for (int i = 6; i <= 10; i++)
						{
							colors.Add(i);
						}
						break;
					}
					case "Eyebrows":
						colors.Add(11);
						colors.Add(12);
						break;
					case "Eyeline":
						colors.Add(13);
						break;
					case "Eye Whites":
						colors.Add(14);
						break;
					case "Upper Highlight":
						colors.Add(16);
						break;
					case "Lower Highlight":
						colors.Add(17);
						break;
					case "Left Eye":
						colors.Add(18);
						colors.Add(20);
						break;
					case "Right Eye":
						colors.Add(18);
						colors.Add(22);
						break;
					case "Lip Line":
						colors.Add(24);
						break;
					case "Eyeshadow":
						colors.Add(25);
						break;
					case "Cheek":
						colors.Add(26);
						break;
					case "Lip":
						colors.Add(27);
						break;
					}
				}
			}
			if (colors.Count == 0)
			{
				return;
			}
			Mutate(character, delegate(Contract contract, object controller)
			{
				RemoveWhere(contract, controller, "OriginalColors", (object key) => colors.Contains(Convert.ToInt32(key)));
			});
		}

		internal static void InvalidateBodyValues(ChaControl character, IEnumerable<int> shapeIndices, IEnumerable<int> colorTypes, IEnumerable<int> floatTypes)
		{
			InvalidateValues(character, "OriginalBodyShapeValues", shapeIndices, colorTypes, floatTypes);
			InvalidatePushupBaseValues(character, shapeIndices, softness: false, weight: false);
		}

		internal static void InvalidatePushupBaseValues(ChaControl character, IEnumerable<int> bodyShapeIndices, bool softness, bool weight)
		{
			HashSet<int> shapes = ToSet(bodyShapeIndices);
			Mutate(character, delegate(Contract contract, object controller)
			{
				RemoveWhere(contract, controller, "OriginalPushupValue", delegate(object key)
				{
					int num = ReadIntProperty(key, "PushupValue");
					return (num >= 4 && num <= 13 && shapes.Contains(num)) || (softness && num == 14) || (weight && num == 15);
				});
			});
		}

		private static void InvalidateValues(ChaControl character, string shapeRecord, IEnumerable<int> shapeIndices, IEnumerable<int> colorTypes, IEnumerable<int> floatTypes)
		{
			HashSet<int> shapes = ToSet(shapeIndices);
			HashSet<int> colors = ToSet(colorTypes);
			HashSet<int> floats = ToSet(floatTypes);
			Mutate(character, delegate(Contract contract, object controller)
			{
				RemoveWhere(contract, controller, shapeRecord, (object key) => shapes.Contains(Convert.ToInt32(key)));
				RemoveWhere(contract, controller, "OriginalColors", (object key) => colors.Contains(Convert.ToInt32(key)));
				RemoveWhere(contract, controller, "OriginalFloats", (object key) => floats.Contains(Convert.ToInt32(key)));
			});
		}

		internal static void InvalidateClothing(ChaControl character, int coordinate, int kind)
		{
			Mutate(character, delegate(Contract contract, object controller)
			{
				RemoveWhere(contract, controller, "OriginalClothingColors", (object key) => ReadIntProperty(key, "OutfitSlot") == coordinate && ReadIntProperty(key, "ClothingKind") == kind);
				if (kind == 0 || kind == 2)
				{
					InvalidatePushupGarment(contract, controller, coordinate);
				}
			});
		}

		internal static void InvalidateClothingColors(ChaControl character, int coordinate)
		{
			Mutate(character, delegate(Contract contract, object controller)
			{
				RemoveWhere(contract, controller, "OriginalClothingColors", (object key) => ReadIntProperty(key, "OutfitSlot") == coordinate);
			});
		}

		internal static void InvalidateClothingColorChannel(ChaControl character, int coordinate, int kind, int channel, bool includePattern)
		{
			Mutate(character, delegate(Contract contract, object controller)
			{
				RemoveWhere(contract, controller, "OriginalClothingColors", (object key) => ReadIntProperty(key, "OutfitSlot") == coordinate && ReadIntProperty(key, "ClothingKind") == kind && ReadIntProperty(key, "ColorNr") == channel && ReadIntProperty(key, "SlotNr") < 0 && (includePattern || !ReadBoolProperty(key, "IsPattern")));
			});
		}

		internal static void InvalidateAllClothing(ChaControl character, int coordinate)
		{
			Mutate(character, delegate(Contract contract, object controller)
			{
				RemoveWhere(contract, controller, "OriginalClothingColors", (object key) => ReadIntProperty(key, "OutfitSlot") == coordinate);
				InvalidatePushupGarment(contract, controller, coordinate);
			});
		}

		internal static void InvalidateAccessory(ChaControl character, int coordinate, int slot)
		{
			Mutate(character, delegate(Contract contract, object controller)
			{
				RemoveWhere(contract, controller, "OriginalAccessoryColors", (object key) => ReadIntProperty(key, "OutfitSlot") == coordinate && ReadIntProperty(key, "SlotNr") == slot);
				RemoveWhere(contract, controller, "OriginalAccessoryFloats", (object key) => ReadIntProperty(key, "OutfitSlot") == coordinate && ReadIntProperty(key, "SlotNr") == slot);
			});
		}

		internal static void InvalidateAccessoryColorChannel(ChaControl character, int coordinate, int slot, int channel)
		{
			Mutate(character, delegate(Contract contract, object controller)
			{
				RemoveWhere(contract, controller, "OriginalAccessoryColors", (object key) => ReadIntProperty(key, "OutfitSlot") == coordinate && ReadIntProperty(key, "SlotNr") == slot && ReadIntProperty(key, "ColorNr") == channel);
			});
		}

		internal static void InvalidateAllAccessories(ChaControl character, int coordinate)
		{
			Mutate(character, delegate(Contract contract, object controller)
			{
				RemoveWhere(contract, controller, "OriginalAccessoryColors", (object key) => ReadIntProperty(key, "OutfitSlot") == coordinate);
				RemoveWhere(contract, controller, "OriginalAccessoryFloats", (object key) => ReadIntProperty(key, "OutfitSlot") == coordinate);
			});
		}

		internal static void InvalidateOutfit(ChaControl character, int coordinate)
		{
			Mutate(character, delegate(Contract contract, object controller)
			{
				string[] array = new string[3] { "OriginalClothingColors", "OriginalAccessoryColors", "OriginalAccessoryFloats" };
				foreach (string record in array)
				{
					RemoveWhere(contract, controller, record, (object key) => ReadIntProperty(key, "OutfitSlot") == coordinate);
				}
				InvalidatePushupGarment(contract, controller, coordinate);
			});
		}

		private static void InvalidatePushupGarment(Contract contract, object controller, int coordinate)
		{
			RemoveWhere(contract, controller, "OriginalPushupValue", delegate(object key)
			{
				int num = ReadIntProperty(key, "PushupValue");
				return ReadIntProperty(key, "OutfitSlot") == coordinate && num >= 20 && num <= 24;
			});
		}

		private static HashSet<int> ToSet(IEnumerable<int> values)
		{
			if (values != null)
			{
				return new HashSet<int>(values);
			}
			return new HashSet<int>();
		}

		private static void Clear(Contract contract, object controller, string record)
		{
			((IDictionary)contract.Fields[record].GetValue(controller)).Clear();
		}

		private static void RemoveWhere(Contract contract, object controller, string record, Func<object, bool> predicate)
		{
			IDictionary dictionary = (IDictionary)contract.Fields[record].GetValue(controller);
			List<object> list = new List<object>();
			foreach (DictionaryEntry item in dictionary)
			{
				if (predicate(item.Key))
				{
					list.Add(item.Key);
				}
			}
			foreach (object item2 in list)
			{
				dictionary.Remove(item2);
			}
		}

		private static int ReadIntProperty(object key, string name)
		{
			return Convert.ToInt32(key.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public).GetValue(key, null));
		}

		private static bool ReadBoolProperty(object key, string name)
		{
			return (bool)key.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public).GetValue(key, null);
		}

		private static IDictionary CloneDictionary(IDictionary source)
		{
			IDictionary dictionary = (IDictionary)Activator.CreateInstance(source.GetType());
			foreach (DictionaryEntry item in source)
			{
				object value = item.Value;
				Type type = value.GetType();
				PropertyInfo property = type.GetProperty("OriginalValue");
				PropertyInfo property2 = type.GetProperty("Value");
				if (property == null || property2 == null)
				{
					throw new InvalidOperationException("Unknown PseudoMaker Reset value.");
				}
				dictionary.Add(item.Key, Activator.CreateInstance(type, property.GetValue(value, null), property2.GetValue(value, null)));
			}
			return dictionary;
		}

		private static void Mutate(ChaControl character, Action<Contract, object> change)
		{
			if (!TryGetController(character, out var contract, out var controller))
			{
				return;
			}
			try
			{
				change(contract, controller);
			}
			catch
			{
			}
		}

		private static bool TryGetController(ChaControl character, out Contract contract, out object controller)
		{
			contract = bound ?? Bind();
			controller = null;
			if (character == null || contract == null)
			{
				return false;
			}
			try
			{
				controller = character.gameObject.GetComponent(contract.ControllerType);
			}
			catch
			{
				return false;
			}
			return controller != null;
		}

		private static Contract Bind()
		{
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Expected O, but got Unknown
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach (Assembly assembly in assemblies)
			{
				try
				{
					Type type = assembly.GetType("PseudoMaker.PseudoMaker", throwOnError: false);
					Type type2 = assembly.GetType("PseudoMaker.PseudoMakerCharaController", throwOnError: false);
					if (type == null || type2 == null)
					{
						continue;
					}
					object[] customAttributes = type.GetCustomAttributes(typeof(BepInPlugin), inherit: false);
					if (customAttributes.Length != 1)
					{
						continue;
					}
					BepInPlugin val = (BepInPlugin)customAttributes[0];
					if (val.GUID != "com.rikkibalboa.bepinex.studioPseudoMaker" || val.Version == null || val.Version.ToString() != "1.5.1.1")
					{
						continue;
					}
					Dictionary<string, FieldInfo> dictionary = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
					bool flag = true;
					string[] recordNames = RecordNames;
					foreach (string text in recordNames)
					{
						FieldInfo field = type2.GetField(text, BindingFlags.Instance | BindingFlags.NonPublic);
						if (field == null || !typeof(IDictionary).IsAssignableFrom(field.FieldType))
						{
							flag = false;
							break;
						}
						dictionary[text] = field;
					}
					if (!flag)
					{
						continue;
					}
					bound = new Contract
					{
						ControllerType = type2,
						Fields = dictionary
					};
					return bound;
				}
				catch
				{
				}
			}
			return null;
		}
	}
	internal static class WardrobeMERecordApplier
	{
		private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		internal static bool Claims(int objectType, int coordinate, int activeCoordinate, bool clothes, bool accessories)
		{
			if (coordinate == activeCoordinate)
			{
				if (!(objectType == 1 && clothes))
				{
					return objectType == 2 && accessories;
				}
				return true;
			}
			return false;
		}

		private static void ApplyLegacyTexture(Component controller, Type api, GameObject go, string name, object record)
		{
			FieldInfo field = controller.GetType().GetField("TextureDictionary", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			IDictionary dictionary = ((field == null) ? null : (field.GetValue(controller) as IDictionary));
			object obj = Field(record, "TexID");
			object obj2 = ((dictionary != null && obj != null && dictionary.Contains(obj)) ? dictionary[obj] : null);
			Texture texture = ((obj2 == null) ? null : (obj2.GetType().GetProperty("Texture")?.GetValue(obj2, null) as Texture));
			if (!(texture == null) && !(api == null))
			{
				string text = Field(record, "Property") as string;
				Type type = controller.GetType().Assembly.GetType("MaterialEditorAPI.MaterialEditorPluginBase");
				object obj3 = (type?.GetField("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))?.GetValue(null) ?? type?.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null, null);
				MethodInfo methodInfo = type?.GetMethod("ConvertNormalMap", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (obj3 != null && methodInfo != null)
				{
					object[] array = new object[2] { texture, text };
					methodInfo.Invoke(obj3, array);
					texture = array[0] as Texture;
				}
				api.GetMethod("SetTexture", new Type[4]
				{
					typeof(GameObject),
					typeof(string),
					typeof(string),
					typeof(Texture)
				})?.Invoke(null, new object[4] { go, name, text, texture });
			}
		}

		private static object Field(object value, string name)
		{
			FieldInfo field = value.GetType().GetField(name);
			if (!(field == null))
			{
				return field.GetValue(value);
			}
			return null;
		}

		private static string Clean(string value)
		{
			return (value ?? "").Replace("(Instance)", "").Trim();
		}

		internal static void Apply(Component controller, string key, object record, bool initializeCardShader = false, GameObject characterRoot = null, GameObject detachedClothingRoot = null)
		{
			Type type = controller.GetType();
			MethodInfo method = type.GetMethod("FindGameObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			GameObject gameObject = detachedClothingRoot;
			if (gameObject == null)
			{
				gameObject = ((method == null) ? null : (method.Invoke(controller, new object[2]
				{
					Field(record, "ObjectType"),
					Field(record, "Slot")
				}) as GameObject));
			}
			if (characterRoot != null && Convert.ToInt32(Field(record, "ObjectType")) == 4)
			{
				gameObject = characterRoot;
			}
			if (gameObject == null)
			{
				return;
			}
			string text = Clean(Field(record, "MaterialName") as string);
			Type type2 = type.Assembly.GetType("MaterialEditorAPI.MaterialAPI", throwOnError: false);
			Renderer[] componentsInChildren;
			switch (key)
			{
			case "RendererPropertyList":
			case "ProjectorPropertyList":
			{
				bool flag = key == "ProjectorPropertyList";
				object obj = Field(record, "Property");
				Type type3 = (flag ? typeof(float) : typeof(string));
				MethodInfo methodInfo = ((type2 == null) ? null : type2.GetMethod(flag ? "SetProjectorProperty" : "SetRendererProperty", new Type[4]
				{
					typeof(GameObject),
					typeof(string),
					obj.GetType(),
					type3
				}));
				object obj2 = (flag ? ((object)Convert.ToSingle(Field(record, "Value"), CultureInfo.InvariantCulture)) : Field(record, "Value"));
				if (methodInfo != null)
				{
					methodInfo.Invoke(null, new object[4]
					{
						gameObject,
						Field(record, flag ? "ProjectorName" : "RendererName"),
						obj,
						obj2
					});
				}
				return;
			}
			case "MaterialCopyList":
			{
				string text2 = Clean(Field(record, "MaterialCopyName") as string);
				componentsInChildren = gameObject.GetComponentsInChildren<Renderer>(includeInactive: true);
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					Material[] sharedMaterials = componentsInChildren[i].sharedMaterials;
					foreach (Material material in sharedMaterials)
					{
						if (material != null && Clean(material.name) == text2)
						{
							return;
						}
					}
				}
				MethodInfo methodInfo2 = ((type2 == null) ? null : type2.GetMethod("CopyMaterial", new Type[3]
				{
					typeof(GameObject),
					typeof(string),
					typeof(string)
				}));
				if (methodInfo2 != null)
				{
					methodInfo2.Invoke(null, new object[3] { gameObject, text, text2 });
				}
				return;
			}
			case "MaterialTexturePropertyList":
				if (Field(record, "TexID") != null)
				{
					MethodInfo method2 = type.GetMethod("SetTextureWithProperty", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (method2 != null)
					{
						method2.Invoke(controller, new object[2] { gameObject, record });
					}
					else
					{
						ApplyLegacyTexture(controller, type2, gameObject, text, record);
					}
				}
				break;
			}
			if (key == "MaterialShaderList" && initializeCardShader)
			{
				string text3 = Field(record, "ShaderName") as string;
				MethodInfo methodInfo3 = ((type2 == null) ? null : type2.GetMethod("SetShader", new Type[4]
				{
					typeof(GameObject),
					typeof(string),
					typeof(string),
					typeof(bool)
				}));
				if (!string.IsNullOrEmpty(text3) && methodInfo3 != null)
				{
					methodInfo3.Invoke(null, new object[4] { gameObject, text, text3, false });
				}
			}
			componentsInChildren = gameObject.GetComponentsInChildren<Renderer>(includeInactive: true);
			foreach (Renderer renderer in componentsInChildren)
			{
				if (key == "MaterialNamePropertyList" && Clean(renderer.name) != Clean(Field(record, "Renderer") as string))
				{
					continue;
				}
				Material[] sharedMaterials = renderer.materials;
				foreach (Material material2 in sharedMaterials)
				{
					if (material2 == null || Clean(material2.name) != text)
					{
						continue;
					}
					if (key == "MaterialNamePropertyList")
					{
						material2.name = (string)Field(record, "Value");
					}
					else if (key == "MaterialShaderList")
					{
						string text4 = Field(record, "ShaderName") as string;
						if (!initializeCardShader && !string.IsNullOrEmpty(text4) && material2.shader.name != text4)
						{
							Type type4 = type.Assembly.GetType("MaterialEditorAPI.MaterialEditorPluginBase", throwOnError: false);
							FieldInfo fieldInfo = ((type4 == null) ? null : type4.GetField("LoadedShaders", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
							IDictionary dictionary = ((fieldInfo == null) ? null : (fieldInfo.GetValue(null) as IDictionary));
							object obj3 = ((dictionary != null && dictionary.Contains(text4)) ? dictionary[text4] : null);
							Shader shader = ((obj3 == null) ? Shader.Find(text4) : (Field(obj3, "Shader") as Shader));
							if (shader != null)
							{
								material2.shader = shader;
							}
						}
						object obj4 = Field(record, "RenderQueue");
						if (obj4 != null)
						{
							material2.renderQueue = Convert.ToInt32(obj4);
						}
					}
					else
					{
						if (!(Field(record, "Property") is string text5))
						{
							continue;
						}
						if (key == "MaterialKeywordPropertyList")
						{
							if ((bool)Field(record, "Value"))
							{
								material2.EnableKeyword(text5);
							}
							else
							{
								material2.DisableKeyword(text5);
							}
							continue;
						}
						string name = "_" + text5.TrimStart('_');
						if (!material2.HasProperty(name))
						{
							continue;
						}
						switch (key)
						{
						case "MaterialFloatPropertyList":
							material2.SetFloat(name, Convert.ToSingle(Field(record, "Value"), CultureInfo.InvariantCulture));
							break;
						case "MaterialColorPropertyList":
							material2.SetColor(name, (Color)Field(record, "Value"));
							break;
						case "MaterialTexturePropertyList":
						{
							object obj5 = Field(record, "Offset");
							object obj6 = Field(record, "Scale");
							if (obj5 != null)
							{
								material2.SetTextureOffset(name, (Vector2)obj5);
							}
							if (obj6 != null)
							{
								material2.SetTextureScale(name, (Vector2)obj6);
							}
							break;
						}
						}
					}
				}
			}
		}
	}
	internal sealed class CharacterCleanupCategory
	{
		private readonly List<CharacterCleanupChoice> choices;

		internal string GroupId { get; private set; }

		internal string Id { get; private set; }

		internal string Label { get; private set; }

		internal int Order { get; private set; }

		internal IList<CharacterCleanupChoice> Choices => choices.AsReadOnly();

		internal CharacterCleanupCategory(string groupId, string id, string label, int order, IList<CharacterCleanupChoice> choices)
		{
			if (string.IsNullOrEmpty(groupId))
			{
				throw new ArgumentException("A cleanup group ID is required.", "groupId");
			}
			if (string.IsNullOrEmpty(id))
			{
				throw new ArgumentException("A cleanup category ID is required.", "id");
			}
			GroupId = groupId;
			Id = id;
			Label = label ?? id;
			Order = CharacterCleanupOrdering.Normalize(id, order);
			this.choices = ((choices == null) ? new List<CharacterCleanupChoice>() : new List<CharacterCleanupChoice>(choices));
		}

		internal CharacterCleanupCategory(string id, string label, int order, IList<CharacterCleanupChoice> choices)
			: this("character", id, label, order, choices)
		{
		}
	}
	internal static class CharacterCleanupCategoryPolicy
	{
		internal static bool Include(string categoryId)
		{
			return !string.Equals(categoryId, "face.eye.left.pupil.type", StringComparison.Ordinal);
		}
	}
	internal sealed class CharacterCleanupChoice
	{
		internal int ItemId { get; private set; }

		internal string Name { get; private set; }

		internal string ThumbnailBundle { get; private set; }

		internal string ThumbnailAsset { get; private set; }

		internal int? ResolverCategory { get; private set; }

		internal CharacterCleanupChoice(int itemId, string name, string thumbnailBundle, string thumbnailAsset)
			: this(itemId, name, thumbnailBundle, thumbnailAsset, null)
		{
		}

		internal CharacterCleanupChoice(int itemId, string name, string thumbnailBundle, string thumbnailAsset, int? resolverCategory)
		{
			ItemId = itemId;
			Name = name ?? string.Empty;
			ThumbnailBundle = thumbnailBundle ?? string.Empty;
			ThumbnailAsset = thumbnailAsset ?? string.Empty;
			ResolverCategory = resolverCategory;
		}
	}
	internal sealed class CharacterCleanupController
	{
		private readonly ICharacterCleanupCatalogAdapter catalog;

		private readonly ICleanupSourceBackend source;

		private IList<CharacterCleanupCategory> categories = new List<CharacterCleanupCategory>();

		private object baseline;

		private string selectionToken;

		private string activeCategoryId;

		private string pendingCategoryId;

		private string activeGroupId;

		private string pendingGroupId;

		private string dirtyCategoryId;

		private bool entered;

		internal IList<CharacterCleanupCategory> Categories => categories;

		internal string ActiveCategoryId => activeCategoryId;

		internal string ActiveGroupId => activeGroupId;

		internal IList<CharacterCleanupCategory> VisibleCategories
		{
			get
			{
				List<CharacterCleanupCategory> list = new List<CharacterCleanupCategory>();
				for (int i = 0; i < categories.Count; i++)
				{
					if (string.Equals(categories[i].GroupId, activeGroupId, StringComparison.Ordinal))
					{
						list.Add(categories[i]);
					}
				}
				return list.AsReadOnly();
			}
		}

		internal CharacterCleanupTrace Trace { get; private set; }

		internal bool IsEntered => entered;

		internal CharacterCleanupController(ICharacterCleanupCatalogAdapter catalog, ICleanupSourceBackend source)
		{
			if (catalog == null)
			{
				throw new ArgumentNullException("catalog");
			}
			if (source == null)
			{
				throw new ArgumentNullException("source");
			}
			this.catalog = catalog;
			this.source = source;
		}

		internal void OnEnter()
		{
			if (!entered)
			{
				entered = true;
				selectionToken = catalog.SelectionToken;
				baseline = catalog.CaptureBaseline();
				RefreshCategories();
			}
		}

		internal void OnExit()
		{
			RestoreBaseline();
			entered = false;
			pendingCategoryId = null;
			pendingGroupId = null;
			Trace = null;
		}

		internal void Reset()
		{
			RestoreBaseline();
			pendingCategoryId = null;
			pendingGroupId = null;
			Trace = null;
			if (entered)
			{
				RefreshCategories();
			}
		}

		internal bool RequestCategory(string categoryId)
		{
			CharacterCleanupCategory characterCleanupCategory = FindCategory(categoryId);
			if (characterCleanupCategory == null)
			{
				return false;
			}
			if (!string.Equals(characterCleanupCategory.GroupId, activeGroupId, StringComparison.Ordinal))
			{
				pendingGroupId = characterCleanupCategory.GroupId;
			}
			pendingCategoryId = categoryId;
			return true;
		}

		internal bool RequestGroup(string groupId)
		{
			if (FirstCategoryInGroup(groupId) == null)
			{
				return false;
			}
			pendingGroupId = groupId;
			return true;
		}

		internal void ApplyDeferredSelection(bool isLayout)
		{
			if (!isLayout)
			{
				return;
			}
			if (!string.IsNullOrEmpty(pendingGroupId))
			{
				string a = pendingGroupId;
				pendingGroupId = null;
				if (!string.Equals(a, activeGroupId, StringComparison.Ordinal))
				{
					RestoreBaseline();
					activeGroupId = a;
					activeCategoryId = FirstCategoryInGroup(activeGroupId)?.Id;
					Trace = null;
				}
			}
			if (!string.IsNullOrEmpty(pendingCategoryId))
			{
				string a2 = pendingCategoryId;
				pendingCategoryId = null;
				if (!string.Equals(a2, activeCategoryId, StringComparison.Ordinal))
				{
					RestoreBaseline();
					activeCategoryId = a2;
					Trace = null;
				}
			}
		}

		internal bool Preview(int itemId)
		{
			CharacterCleanupChoice characterCleanupChoice = FindChoice(activeCategoryId, itemId);
			if (!entered || characterCleanupChoice == null)
			{
				return false;
			}
			RestoreBaseline();
			catalog.Preview(activeCategoryId, itemId);
			dirtyCategoryId = activeCategoryId;
			Trace = source.Trace(characterCleanupChoice);
			return true;
		}

		internal bool Inspect(int itemId)
		{
			CharacterCleanupChoice characterCleanupChoice = FindChoice(activeCategoryId, itemId);
			if (!entered || characterCleanupChoice == null)
			{
				return false;
			}
			Trace = source.Trace(characterCleanupChoice);
			return true;
		}

		internal bool Terminate()
		{
			if (Trace == null)
			{
				return false;
			}
			CharacterCleanupTrace trace = Trace;
			RestoreBaseline();
			if (!source.Terminate(trace))
			{
				return false;
			}
			catalog.RecoverAfterTermination();
			baseline = catalog.CaptureBaseline();
			Trace = null;
			RefreshCategories();
			return true;
		}

		internal bool UndoLast()
		{
			if (source.UndoLast() == null)
			{
				return false;
			}
			RefreshCategories();
			return true;
		}

		internal bool RefreshSelection()
		{
			string a = catalog.SelectionToken;
			if (string.Equals(a, selectionToken, StringComparison.Ordinal) && catalog.IsSelectionCurrent(selectionToken))
			{
				return false;
			}
			RestoreBaseline();
			selectionToken = a;
			baseline = catalog.CaptureBaseline();
			Trace = null;
			RefreshCategories();
			return true;
		}

		private void RefreshCategories()
		{
			IList<CharacterCleanupCategory> list = catalog.GetCategories() ?? new List<CharacterCleanupCategory>();
			List<CharacterCleanupCategory> list2 = new List<CharacterCleanupCategory>();
			for (int i = 0; i < list.Count; i++)
			{
				CharacterCleanupCategory characterCleanupCategory = list[i];
				List<CharacterCleanupChoice> list3 = new List<CharacterCleanupChoice>();
				for (int j = 0; j < characterCleanupCategory.Choices.Count; j++)
				{
					if (!source.IsItemBanned(characterCleanupCategory.Choices[j].ItemId))
					{
						list3.Add(characterCleanupCategory.Choices[j]);
					}
				}
				if (list3.Count != 0)
				{
					list2.Add(new CharacterCleanupCategory(characterCleanupCategory.GroupId, characterCleanupCategory.Id, characterCleanupCategory.Label, characterCleanupCategory.Order, list3));
				}
			}
			list2.Sort(delegate(CharacterCleanupCategory left, CharacterCleanupCategory right)
			{
				int num = left.Order.CompareTo(right.Order);
				return (num == 0) ? string.Compare(left.Id, right.Id, StringComparison.Ordinal) : num;
			});
			categories = list2.AsReadOnly();
			if (FirstCategoryInGroup(activeGroupId) == null)
			{
				activeGroupId = ((list2.Count == 0) ? null : list2[0].GroupId);
			}
			CharacterCleanupCategory characterCleanupCategory2 = FindCategory(activeCategoryId);
			if (characterCleanupCategory2 == null || !string.Equals(characterCleanupCategory2.GroupId, activeGroupId, StringComparison.Ordinal))
			{
				activeCategoryId = FirstCategoryInGroup(activeGroupId)?.Id;
			}
		}

		private void RestoreBaseline()
		{
			if (baseline != null && !string.IsNullOrEmpty(dirtyCategoryId))
			{
				string categoryId = dirtyCategoryId;
				dirtyCategoryId = null;
				catalog.RestoreBaseline(baseline, categoryId);
			}
		}

		private CharacterCleanupCategory FindCategory(string id)
		{
			for (int i = 0; i < categories.Count; i++)
			{
				if (string.Equals(categories[i].Id, id, StringComparison.Ordinal))
				{
					return categories[i];
				}
			}
			return null;
		}

		private CharacterCleanupChoice FindChoice(string categoryId, int itemId)
		{
			CharacterCleanupCategory characterCleanupCategory = FindCategory(categoryId);
			if (characterCleanupCategory == null)
			{
				return null;
			}
			for (int i = 0; i < characterCleanupCategory.Choices.Count; i++)
			{
				if (characterCleanupCategory.Choices[i].ItemId == itemId)
				{
					return characterCleanupCategory.Choices[i];
				}
			}
			return null;
		}

		private CharacterCleanupCategory FirstCategoryInGroup(string groupId)
		{
			for (int i = 0; i < categories.Count; i++)
			{
				if (string.Equals(categories[i].GroupId, groupId, StringComparison.Ordinal))
				{
					return categories[i];
				}
			}
			return null;
		}
	}
	internal static class CharacterCleanupNaming
	{
		internal static string Label(string sectionId, string controlLabel, string sectionLabel)
		{
			if (!string.IsNullOrEmpty(sectionId))
			{
				if (string.Equals(sectionId, "face.eye.right.pupil.type", StringComparison.Ordinal))
				{
					return "Iris";
				}
				if (sectionId.IndexOf(".front", StringComparison.Ordinal) >= 0)
				{
					return "Front Hair";
				}
				if (sectionId.IndexOf(".back", StringComparison.Ordinal) >= 0)
				{
					return "Back Hair";
				}
				if (sectionId.IndexOf(".side", StringComparison.Ordinal) >= 0)
				{
					return "Side Hair";
				}
				if (sectionId.IndexOf(".option", StringComparison.Ordinal) >= 0)
				{
					return "Option Hair";
				}
			}
			if (!string.IsNullOrEmpty(controlLabel))
			{
				return controlLabel;
			}
			return sectionLabel;
		}
	}
	internal static class CharacterCleanupOrdering
	{
		private const string IrisCategoryId = "face.eye.right.pupil.type";

		private const string FaceTypeCategoryId = "face.type";

		internal static int Normalize(string categoryId, int catalogOrder)
		{
			if (string.Equals(categoryId, "face.eye.right.pupil.type", StringComparison.Ordinal))
			{
				return -2147483548;
			}
			if (string.Equals(categoryId, "face.type", StringComparison.Ordinal))
			{
				return 2147483547;
			}
			return catalogOrder;
		}
	}
	internal static class CharacterCleanupPreviewPolicy
	{
		private const string LeftPrefix = "face.eye.left.";

		private const string RightPrefix = "face.eye.right.";

		internal static bool ApplyToBothEyes(string categoryId)
		{
			if (!string.IsNullOrEmpty(categoryId))
			{
				if (!categoryId.StartsWith("face.eye.left.", StringComparison.Ordinal))
				{
					return categoryId.StartsWith("face.eye.right.", StringComparison.Ordinal);
				}
				return true;
			}
			return false;
		}

		internal static string Counterpart(string categoryId)
		{
			if (string.IsNullOrEmpty(categoryId))
			{
				return string.Empty;
			}
			if (categoryId.StartsWith("face.eye.left.", StringComparison.Ordinal))
			{
				return "face.eye.right." + categoryId.Substring("face.eye.left.".Length);
			}
			if (categoryId.StartsWith("face.eye.right.", StringComparison.Ordinal))
			{
				return "face.eye.left." + categoryId.Substring("face.eye.right.".Length);
			}
			return string.Empty;
		}

		internal static int OuterClothesSlotToHide(int previewClothesSlot)
		{
			return previewClothesSlot switch
			{
				2 => 0, 
				3 => 1, 
				_ => -1, 
			};
		}

		internal static bool HideClothesForCharacterCategory(string categoryId)
		{
			if (!string.IsNullOrEmpty(categoryId))
			{
				return categoryId.StartsWith("body.", StringComparison.Ordinal);
			}
			return false;
		}

		internal static IList<int> HairPartsToHide(string categoryId)
		{
			if (IsHairCategory(categoryId, "back"))
			{
				return new int[3] { 1, 2, 3 };
			}
			if (IsHairCategory(categoryId, "front"))
			{
				return new int[3] { 0, 2, 3 };
			}
			return new int[0];
		}

		private static bool IsHairCategory(string categoryId, string part)
		{
			if (string.IsNullOrEmpty(categoryId))
			{
				return false;
			}
			if (!categoryId.StartsWith("hair." + part, StringComparison.Ordinal))
			{
				return categoryId.StartsWith("head.hair." + part, StringComparison.Ordinal);
			}
			return true;
		}
	}
	internal sealed class CharacterCleanupSpotlightState
	{
		private readonly List<int> itemIds = new List<int>();

		private string categoryId;

		private int activeIndex = -1;

		internal string CategoryId => categoryId;

		internal int ActiveItemId
		{
			get
			{
				if (activeIndex >= 0 && activeIndex < itemIds.Count)
				{
					return itemIds[activeIndex];
				}
				return -1;
			}
		}

		internal int SequenceGeneration { get; private set; }

		internal bool GridOpen { get; private set; }

		internal string ConfirmationArchive { get; private set; }

		internal int ConfirmationItemId { get; private set; }

		internal void SetSequence(string nextCategoryId, IList<int> nextItems)
		{
			int activeItemId = ActiveItemId;
			if (string.Equals(categoryId, nextCategoryId, StringComparison.Ordinal) && SameItems(nextItems))
			{
				return;
			}
			int num = (string.Equals(categoryId, nextCategoryId, StringComparison.Ordinal) ? ContinuationIndex(itemIds, activeItemId, nextItems) : 0);
			categoryId = nextCategoryId;
			itemIds.Clear();
			if (nextItems != null)
			{
				for (int i = 0; i < nextItems.Count; i++)
				{
					itemIds.Add(nextItems[i]);
				}
			}
			activeIndex = itemIds.IndexOf(activeItemId);
			if (activeIndex < 0 && itemIds.Count != 0)
			{
				activeIndex = num;
			}
			SequenceGeneration++;
			GridOpen = false;
		}

		internal bool Move(int direction)
		{
			if (itemIds.Count == 0 || direction == 0)
			{
				return false;
			}
			activeIndex = (activeIndex + ((direction > 0) ? 1 : (-1)) + itemIds.Count) % itemIds.Count;
			return true;
		}

		internal bool Select(int itemId)
		{
			int num = itemIds.IndexOf(itemId);
			if (num < 0)
			{
				return false;
			}
			activeIndex = num;
			return true;
		}

		internal bool SelectFromGrid(int itemId)
		{
			if (!Select(itemId))
			{
				return false;
			}
			GridOpen = false;
			return true;
		}

		internal void ToggleGrid()
		{
			GridOpen = !GridOpen;
		}

		internal void CloseGrid()
		{
			GridOpen = false;
		}

		internal static int ContinuationIndex(IList<int> before, int removedItem, IList<int> remaining)
		{
			if (remaining == null || remaining.Count == 0)
			{
				return -1;
			}
			int num = before?.IndexOf(removedItem) ?? (-1);
			if (num >= 0)
			{
				for (int i = num + 1; i < before.Count; i++)
				{
					int num2 = remaining.IndexOf(before[i]);
					if (num2 >= 0)
					{
						return num2;
					}
				}
			}
			return 0;
		}

		internal void MarkTerminated(string archive, int itemId)
		{
			ConfirmationArchive = archive ?? string.Empty;
			ConfirmationItemId = itemId;
			GridOpen = false;
		}

		internal void ContinueCleaning()
		{
			ConfirmationArchive = null;
			ConfirmationItemId = -1;
		}

		internal void Reset()
		{
			categoryId = null;
			itemIds.Clear();
			activeIndex = -1;
			SequenceGeneration++;
			GridOpen = false;
			ContinueCleaning();
		}

		private bool SameItems(IList<int> nextItems)
		{
			int num = nextItems?.Count ?? 0;
			if (itemIds.Count != num)
			{
				return false;
			}
			for (int i = 0; i < num; i++)
			{
				if (itemIds[i] != nextItems[i])
				{
					return false;
				}
			}
			return true;
		}
	}
	internal sealed class CharacterCleanupSibling
	{
		internal int ItemId { get; private set; }

		internal int ResolverCategory { get; private set; }

		internal string Name { get; private set; }

		internal CharacterCleanupSibling(int itemId, string name)
			: this(itemId, 0, name)
		{
		}

		internal CharacterCleanupSibling(int itemId, int resolverCategory, string name)
		{
			ItemId = itemId;
			ResolverCategory = resolverCategory;
			Name = name ?? string.Empty;
		}
	}
	internal sealed class CharacterCleanupTrace
	{
		private readonly List<string> archives;

		private readonly List<CharacterCleanupSibling> siblings;

		internal int ItemId { get; private set; }

		internal int? ResolverCategory { get; private set; }

		internal string SourceId { get; private set; }

		internal string SourceName { get; private set; }

		internal string LockedReason { get; private set; }

		internal bool AlreadyBanned { get; private set; }

		internal IList<string> Archives => archives.AsReadOnly();

		internal IList<CharacterCleanupSibling> Siblings => siblings.AsReadOnly();

		internal int StudioItemCount { get; private set; }

		internal string SourceDisplayName
		{
			get
			{
				if (!string.IsNullOrEmpty(SourceName))
				{
					return SourceName;
				}
				if (archives.Count != 0 && !string.IsNullOrEmpty(archives[0]))
				{
					return Path.GetFileName(archives[0]);
				}
				if (!string.IsNullOrEmpty(SourceId))
				{
					return SourceId;
				}
				return "Unknown";
			}
		}

		internal bool CanTerminate
		{
			get
			{
				if (!AlreadyBanned && string.IsNullOrEmpty(LockedReason) && !string.IsNullOrEmpty(SourceId))
				{
					return archives.Count != 0;
				}
				return false;
			}
		}

		internal CharacterCleanupTrace(int itemId, string sourceId, string sourceName, string lockedReason, bool alreadyBanned, IList<string> archives, IList<CharacterCleanupSibling> siblings, int studioItemCount)
			: this(itemId, null, sourceId, sourceName, lockedReason, alreadyBanned, archives, siblings, studioItemCount)
		{
		}

		internal CharacterCleanupTrace(int itemId, int? resolverCategory, string sourceId, string sourceName, string lockedReason, bool alreadyBanned, IList<string> archives, IList<CharacterCleanupSibling> siblings, int studioItemCount)
		{
			ItemId = itemId;
			ResolverCategory = resolverCategory;
			SourceId = sourceId;
			SourceName = sourceName ?? string.Empty;
			LockedReason = lockedReason;
			AlreadyBanned = alreadyBanned;
			this.archives = ((archives == null) ? new List<string>() : new List<string>(archives));
			this.siblings = ((siblings == null) ? new List<CharacterCleanupSibling>() : new List<CharacterCleanupSibling>(siblings));
			StudioItemCount = studioItemCount;
		}
	}
	internal static class CharacterHairVisibility
	{
		internal static bool[] Capture(object character)
		{
			Array array = HairObjects(character);
			if (array == null)
			{
				return new bool[0];
			}
			bool[] array2 = new bool[array.Length];
			for (int i = 0; i < array.Length; i++)
			{
				GameObject gameObject = array.GetValue(i) as GameObject;
				array2[i] = gameObject != null && gameObject.activeSelf;
			}
			return array2;
		}

		internal static void Restore(object character, IList<bool> states)
		{
			Array array = HairObjects(character);
			if (array != null && states != null)
			{
				int num = Math.Min(array.Length, states.Count);
				for (int i = 0; i < num; i++)
				{
					SetActive(array.GetValue(i) as GameObject, states[i]);
				}
			}
		}

		internal static void ApplyExclusivePreview(object character, string categoryId)
		{
			IList<int> list = CharacterCleanupPreviewPolicy.HairPartsToHide(categoryId);
			if (list.Count == 0)
			{
				return;
			}
			Array array = HairObjects(character);
			if (array == null)
			{
				return;
			}
			for (int i = 0; i < list.Count; i++)
			{
				int num = list[i];
				if (num >= 0 && num < array.Length)
				{
					SetActive(array.GetValue(num) as GameObject, active: false);
				}
			}
			int num2 = ((!IsPart(categoryId, "back")) ? (IsPart(categoryId, "front") ? 1 : (-1)) : 0);
			if (num2 >= 0 && num2 < array.Length)
			{
				SetActive(array.GetValue(num2) as GameObject, active: true);
			}
		}

		private static bool IsPart(string categoryId, string part)
		{
			if (!string.IsNullOrEmpty(categoryId))
			{
				if (!categoryId.StartsWith("hair." + part, StringComparison.Ordinal))
				{
					return categoryId.StartsWith("head.hair." + part, StringComparison.Ordinal);
				}
				return true;
			}
			return false;
		}

		private static void SetActive(GameObject part, bool active)
		{
			if (part != null && part.activeSelf != active)
			{
				part.SetActive(active);
			}
		}

		private static Array HairObjects(object character)
		{
			if (character == null)
			{
				return null;
			}
			Type type = character.GetType();
			while (type != null)
			{
				FieldInfo field = type.GetField("objHair", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (field != null)
				{
					return field.GetValue(character) as Array;
				}
				PropertyInfo property = type.GetProperty("objHair", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (property != null && property.GetIndexParameters().Length == 0)
				{
					return property.GetValue(character, null) as Array;
				}
				type = type.BaseType;
			}
			return null;
		}
	}
	internal static class CleanupAssetPolicy
	{
		internal static bool IsCleanupCandidate(int itemId, int modIdFloor)
		{
			return itemId >= modIdFloor;
		}

		internal static bool IsVisible(int itemId, int modIdFloor, Func<int, bool> isBanned)
		{
			if (itemId < modIdFloor)
			{
				return true;
			}
			if (isBanned != null)
			{
				return !isBanned(itemId);
			}
			return true;
		}
	}
	internal static class CleanupAuthorityCompatibility
	{
		private const string SessionBanMarkerType = "Freelancer604.Wardrobe.CleanupSessionBanSet";

		internal static bool CanShareWith(Assembly hostAssembly)
		{
			if (hostAssembly != null)
			{
				return hostAssembly.GetType("Freelancer604.Wardrobe.CleanupSessionBanSet", throwOnError: false) != null;
			}
			return false;
		}
	}
	internal sealed class CleanupClothesStateTracker
	{
		private readonly List<KeyValuePair<int, int>> originalStates = new List<KeyValuePair<int, int>>();

		internal void Remember(int slot, int state)
		{
			for (int i = 0; i < originalStates.Count; i++)
			{
				if (originalStates[i].Key == slot)
				{
					return;
				}
			}
			originalStates.Add(new KeyValuePair<int, int>(slot, state));
		}

		internal IList<KeyValuePair<int, int>> Take()
		{
			List<KeyValuePair<int, int>> list = new List<KeyValuePair<int, int>>(originalStates);
			originalStates.Clear();
			return list.AsReadOnly();
		}

		internal void Reset()
		{
			originalStates.Clear();
		}
	}
	internal interface ICleanupContributor
	{
		string Id { get; }

		int Order { get; }

		int MinimumShellContractVersion { get; }
	}
	internal static class CleanupLegacyBanLedger
	{
		internal static void Retire(string path)
		{
			if (!string.IsNullOrEmpty(path) && File.Exists(path) && new FileInfo(path).Length != 0L)
			{
				File.WriteAllText(path, string.Empty);
			}
		}
	}
	internal sealed class CleanupPreviewAttempt
	{
		private object target;

		private int category;

		private int item;

		private bool attempted;

		internal bool Matches(object character, int categoryId, int itemId)
		{
			if (attempted && target == character && category == categoryId)
			{
				return item == itemId;
			}
			return false;
		}

		internal void Mark(object character, int categoryId, int itemId)
		{
			target = character;
			category = categoryId;
			item = itemId;
			attempted = true;
		}

		internal void Reset()
		{
			target = null;
			attempted = false;
		}
	}
	internal sealed class CleanupPreviewScopeTracker
	{
		private string dirtyScope;

		internal void MarkDirty(string scope)
		{
			if (string.IsNullOrEmpty(scope))
			{
				throw new ArgumentException("A cleanup preview scope is required.", "scope");
			}
			dirtyScope = scope;
		}

		internal string TakeDirtyScope()
		{
			string result = dirtyScope;
			dirtyScope = null;
			return result;
		}

		internal void Reset()
		{
			dirtyScope = null;
		}
	}
	internal sealed class CleanupResolveCandidate
	{
		internal int ItemId { get; private set; }

		internal int Category { get; private set; }

		internal string SourceId { get; private set; }

		internal CleanupResolveCandidate(int itemId, int category, string sourceId)
		{
			ItemId = itemId;
			Category = category;
			SourceId = sourceId ?? string.Empty;
		}
	}
	internal static class CleanupResolveSelector
	{
		internal const int NotFound = -1;

		internal const int Ambiguous = -2;

		internal static int Select(IList<CleanupResolveCandidate> candidates, int itemId, int? category)
		{
			if (candidates == null)
			{
				return -1;
			}
			int num = -1;
			string a = null;
			for (int i = 0; i < candidates.Count; i++)
			{
				CleanupResolveCandidate cleanupResolveCandidate = candidates[i];
				if (cleanupResolveCandidate != null && cleanupResolveCandidate.ItemId == itemId && (!category.HasValue || cleanupResolveCandidate.Category == category.Value))
				{
					if (num == -1)
					{
						num = i;
						a = cleanupResolveCandidate.SourceId;
					}
					else if (!string.Equals(a, cleanupResolveCandidate.SourceId, StringComparison.Ordinal))
					{
						return -2;
					}
				}
			}
			return num;
		}
	}
	internal sealed class CleanupSessionBanSet
	{
		private readonly HashSet<string> guids = new HashSet<string>(StringComparer.Ordinal);

		internal int Count => guids.Count;

		internal bool Add(string guid)
		{
			if (!string.IsNullOrEmpty(guid))
			{
				return guids.Add(guid);
			}
			return false;
		}

		internal bool Remove(string guid)
		{
			if (!string.IsNullOrEmpty(guid))
			{
				return guids.Remove(guid);
			}
			return false;
		}

		internal bool Contains(string guid)
		{
			if (!string.IsNullOrEmpty(guid))
			{
				return guids.Contains(guid);
			}
			return false;
		}
	}
	internal static class CleanupStudioItemCounts
	{
		internal static Dictionary<string, int> Read(Type resolverType)
		{
			Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.Ordinal);
			if (resolverType == null)
			{
				return dictionary;
			}
			IEnumerable enumerable = null;
			string[] array = new string[2] { "LoadedStudioResolutionInfos", "LoadedStudioResolutionInfo" };
			foreach (string name in array)
			{
				try
				{
					PropertyInfo property = resolverType.GetProperty(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
					FieldInfo fieldInfo = ((property == null) ? resolverType.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) : null);
					enumerable = ((property != null) ? property.GetValue(null, null) : ((fieldInfo != null) ? fieldInfo.GetValue(null) : null)) as IEnumerable;
					if (enumerable != null)
					{
						break;
					}
				}
				catch
				{
				}
			}
			if (enumerable == null)
			{
				return dictionary;
			}
			try
			{
				foreach (object item in enumerable)
				{
					if (item != null)
					{
						Type type = item.GetType();
						FieldInfo field = type.GetField("GUID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						PropertyInfo propertyInfo = ((field == null) ? type.GetProperty("GUID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) : null);
						string text = ((field != null) ? field.GetValue(item) : ((propertyInfo != null) ? propertyInfo.GetValue(item, null) : null)) as string;
						if (!string.IsNullOrEmpty(text))
						{
							dictionary.TryGetValue(text, out var value);
							dictionary[text] = value + 1;
						}
					}
				}
			}
			catch
			{
				dictionary.Clear();
			}
			return dictionary;
		}
	}
	internal interface ICharacterCleanupCatalogAdapter
	{
		string SelectionToken { get; }

		IList<CharacterCleanupCategory> GetCategories();

		object CaptureBaseline();

		void RestoreBaseline(object baseline, string categoryId);

		void RecoverAfterTermination();

		void Preview(string categoryId, int itemId);

		bool IsSelectionCurrent(string token);
	}
	internal interface ICleanupSourceBackend
	{
		int PendingCount { get; }

		CharacterCleanupTrace Trace(CharacterCleanupChoice choice);

		bool Terminate(CharacterCleanupTrace trace);

		CharacterCleanupTrace UndoLast();

		bool IsItemBanned(int itemId);

		void ProcessPendingMoves();
	}
	internal sealed class DeferredGuiActionState
	{
		private Action pending;

		internal bool HasPending => pending != null;

		internal void Queue(Action action)
		{
			if (pending == null)
			{
				pending = action;
			}
		}

		internal void Replace(Action action)
		{
			pending = action;
		}

		internal void Apply(bool isLayout)
		{
			if (isLayout && pending != null)
			{
				Action action = pending;
				pending = null;
				action();
			}
		}

		internal void Reset()
		{
			pending = null;
		}
	}
	internal sealed class WardrobeCleanupMoveResult
	{
		internal int Moved;

		internal int Gone;

		internal int Failed;

		internal readonly List<string> Errors = new List<string>();
	}
	internal static class WardrobeCleanupMoverCore
	{
		internal static WardrobeCleanupMoveResult Process(string gameRootPath)
		{
			WardrobeCleanupMoveResult wardrobeCleanupMoveResult = new WardrobeCleanupMoveResult();
			string text = Path.Combine(gameRootPath, "UserData\\DressUp604");
			string path = Path.Combine(text, "cleanup_pending.txt");
			string path2 = Path.Combine(text, "cleanup_restorelog.txt");
			string modsRoot = Path.Combine(gameRootPath, "mods");
			string path3 = Path.Combine(gameRootPath, "Cleanup_Quarantine");
			if (!File.Exists(path))
			{
				return wardrobeCleanupMoveResult;
			}
			string[] array;
			try
			{
				array = File.ReadAllLines(path);
			}
			catch (Exception ex)
			{
				wardrobeCleanupMoveResult.Failed++;
				wardrobeCleanupMoveResult.Errors.Add("could not read cleanup_pending.txt: " + ex.Message);
				return wardrobeCleanupMoveResult;
			}
			List<string> list = new List<string>();
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			string[] array2 = array;
			foreach (string text2 in array2)
			{
				string text3 = ((text2 == null) ? "" : text2.Trim());
				if (text3.Length == 0 || !hashSet.Add(text3))
				{
					continue;
				}
				try
				{
					if (!File.Exists(text3))
					{
						wardrobeCleanupMoveResult.Gone++;
						continue;
					}
					if (!TryRelativeToMods(modsRoot, text3, out var relative))
					{
						throw new InvalidOperationException("source is outside the game's mods folder");
					}
					string text4 = NextAvailablePath(Path.Combine(path3, relative));
					string directoryName = Path.GetDirectoryName(text4);
					if (!string.IsNullOrEmpty(directoryName))
					{
						Directory.CreateDirectory(directoryName);
					}
					File.Move(text3, text4);
					wardrobeCleanupMoveResult.Moved++;
					try
					{
						Directory.CreateDirectory(text);
						File.AppendAllText(path2, text4 + "|" + text3 + "|" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine);
					}
					catch (Exception ex2)
					{
						wardrobeCleanupMoveResult.Errors.Add("archive moved but restore log could not be updated: " + ex2.Message);
					}
				}
				catch (Exception ex3)
				{
					wardrobeCleanupMoveResult.Failed++;
					list.Add(text3);
					wardrobeCleanupMoveResult.Errors.Add(Path.GetFileName(text3) + " — " + ex3.Message);
				}
			}
			try
			{
				Directory.CreateDirectory(text);
				File.WriteAllLines(path, list.ToArray());
			}
			catch (Exception ex4)
			{
				wardrobeCleanupMoveResult.Errors.Add("could not update cleanup_pending.txt: " + ex4.Message);
			}
			return wardrobeCleanupMoveResult;
		}

		private static bool TryRelativeToMods(string modsRoot, string candidate, out string relative)
		{
			relative = null;
			string text = Path.GetFullPath(modsRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
			string fullPath = Path.GetFullPath(candidate);
			if (!fullPath.StartsWith(text, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
			relative = fullPath.Substring(text.Length);
			return relative.Length > 0;
		}

		private static string NextAvailablePath(string desired)
		{
			if (!File.Exists(desired))
			{
				return desired;
			}
			string directoryName = Path.GetDirectoryName(desired);
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(desired);
			string extension = Path.GetExtension(desired);
			int num = 2;
			string text;
			do
			{
				text = Path.Combine(directoryName, fileNameWithoutExtension + " (" + num++ + ")" + extension);
			}
			while (File.Exists(text));
			return text;
		}
	}
}
