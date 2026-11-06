using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using ADV;
using ActionGame;
using ActionGame.Chara;
using ActionGame.Communication;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using ChaCustom;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.CompilerServices;
using ExtensibleSaveFormat;
using H;
using HarmonyLib;
using Illusion.Component;
using Illusion.Extensions;
using Illusion.Game;
using IllusionUtility.GetUtility;
using IllusionUtility.SetUtility;
using JetBrains.Annotations;
using KKAPI;
using KKAPI.Chara;
using KKAPI.MainGame;
using KKAPI.Maker;
using KKAPI.Maker.UI;
using KKAPI.Maker.UI.Sidebar;
using KKAPI.Studio;
using KKAPI.Studio.SaveLoad;
using KKAPI.Studio.UI;
using KKAPI.Utilities;
using Manager;
using MessagePack;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using PAP.Assist;
using SaveData;
using SceneAssist;
using Sound;
using Studio;
using TMPro;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using XUnity.AutoTranslator.Plugin.Core;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: AssemblyTitle("KKSAPI")]
[assembly: AssemblyDescription("Modding API for Koikatsu Sunshine")]
[assembly: AssemblyCompany("ManlyMarco")]
[assembly: AssemblyProduct("KKSAPI")]
[assembly: AssemblyCopyright("Copyright ©  2021")]
[assembly: ComVisible(false)]
[assembly: Guid("74c2db86-55bb-47b4-9805-51b5b992ef35")]
[assembly: AssemblyFileVersion("1.42.2")]
[assembly: TargetFramework(".NETFramework,Version=v4.6", FrameworkDisplayName = ".NET Framework 4.6")]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: AssemblyVersion("1.42.2.0")]
[module: UnverifiableCode]
namespace KoiSkinOverlayX
{
	/// <summary>
	/// A class for storing textures that should be saved and loaded from extended data's <see cref="T:ExtensibleSaveFormat.PluginData" /> (e.g. to character cards and scenes).
	/// Duplicate textures are automatically handled so that only one copy of the texture is held in memory and saved.
	/// </summary>
	public class TextureStorage : IDisposable
	{
		private sealed class TextureHolder : IDisposable
		{
			private readonly TextureFormat _format;

			private byte[] _data;

			private Texture2D _texture;

			public byte[] Data
			{
				get
				{
					return _data;
				}
				set
				{
					Dispose();
					_data = value;
				}
			}

			public Texture2D Texture
			{
				get
				{
					if (_texture == null && _data != null)
					{
						_texture = _data.LoadTexture(_format);
					}
					return _texture;
				}
			}

			public TextureHolder(byte[] data, TextureFormat format)
			{
				Data = data ?? throw new ArgumentNullException("data");
				_format = format;
			}

			public void Dispose()
			{
				if (_texture != null)
				{
					UnityEngine.Object.Destroy(_texture);
					_texture = null;
				}
			}
		}

		private const string DataMarker = "_TextureID_";

		private readonly Dictionary<int, TextureHolder> _data = new Dictionary<int, TextureHolder>();

		private readonly TextureFormat _format;

		/// <summary>
		/// Create a new TextureStorage.
		/// </summary>
		/// <param name="format">Format of the loaded textures. It doesn't affect data saved to extended data.</param>
		public TextureStorage(TextureFormat format = TextureFormat.ARGB32)
		{
			_format = format;
		}

		void IDisposable.Dispose()
		{
			lock (_data)
			{
				foreach (KeyValuePair<int, TextureHolder> datum in _data)
				{
					datum.Value?.Dispose();
				}
				_data.Clear();
			}
		}

		/// <summary>
		/// Remove unused textures based on a list of used IDs. Textures with IDs not in the list will be removed.
		/// </summary>
		/// <param name="usedIDs">A list of IDs to be kept if they exist</param>
		public void PurgeUnused(IEnumerable<int> usedIDs)
		{
			if (usedIDs == null)
			{
				throw new ArgumentNullException("usedIDs");
			}
			HashSet<int> hashSet = new HashSet<int>(usedIDs);
			lock (_data)
			{
				foreach (KeyValuePair<int, TextureHolder> item in _data.ToList())
				{
					bool flag = hashSet.Contains(item.Key);
					if (!flag || item.Value?.Data == null)
					{
						Console.WriteLine(string.Format("Removing {0} texture with ID {1}", flag ? "empty" : "unused", item.Key));
						item.Value?.Dispose();
						_data.Remove(item.Key);
					}
				}
			}
		}

		/// <summary>
		/// Get IDs of all textures stored in this object.
		/// </summary>
		public int[] GetAllTextureIDs()
		{
			lock (_data)
			{
				return _data.Keys.ToArray();
			}
		}

		/// <summary>
		/// Clear the texture list and optionally destroy all textures.
		/// </summary>
		public void Clear(bool destroy = true)
		{
			lock (_data)
			{
				if (destroy)
				{
					((IDisposable)this).Dispose();
				}
				else
				{
					_data.Clear();
				}
			}
		}

		/// <summary>
		/// Load textures from extended data that were stored with <see cref="M:KoiSkinOverlayX.TextureStorage.Save(ExtensibleSaveFormat.PluginData)" />.
		/// </summary>
		public void Load(PluginData data)
		{
			if (data == null)
			{
				throw new ArgumentNullException("data");
			}
			lock (_data)
			{
				foreach (KeyValuePair<string, object> item in data.data.Where((KeyValuePair<string, object> x) => x.Key.StartsWith("_TextureID_")))
				{
					string text = item.Key.Substring("_TextureID_".Length);
					if (!int.TryParse(text, out var result))
					{
						KoikatuAPI.Logger.LogDebug((object)("Invalid ID " + text + " in key " + item.Key));
						continue;
					}
					byte[] array = item.Value as byte[];
					if (array == null && item.Value != null)
					{
						KoikatuAPI.Logger.LogDebug((object)$"Invalid value of ID {result}. Should be of type byte[] but is {item.Value.GetType()}");
					}
					else
					{
						_data[result] = new TextureHolder(array, _format);
					}
				}
			}
		}

		/// <summary>
		/// Save textures stored in this object to extended data. Can be loaded later with <see cref="M:KoiSkinOverlayX.TextureStorage.Load(ExtensibleSaveFormat.PluginData)" />.
		/// </summary>
		public void Save(PluginData data)
		{
			if (data == null)
			{
				throw new ArgumentNullException("data");
			}
			lock (_data)
			{
				foreach (KeyValuePair<int, TextureHolder> datum in _data)
				{
					if (datum.Value != null)
					{
						data.data["_TextureID_" + datum.Key] = datum.Value.Data;
					}
				}
			}
		}

		/// <summary>
		/// Store a texture and get an ID representing it. The ID can be used to get the texture with <see cref="M:KoiSkinOverlayX.TextureStorage.GetSharedTexture(System.Int32)" />.
		/// If you try to store a texture that was already stored before, the ID of the previous texture is returned so there are no multiple identical textures stored.
		/// </summary>
		/// <param name="tex">Raw PNG data of the texture. If you reuse a texture make sure you always use the same PNG data or deduplicating won't work.</param>
		public int StoreTexture(byte[] tex)
		{
			if (tex == null)
			{
				throw new ArgumentNullException("tex");
			}
			lock (_data)
			{
				KeyValuePair<int, TextureHolder> keyValuePair = _data.FirstOrDefault((KeyValuePair<int, TextureHolder> x) => x.Value != null && x.Value.Data.SequenceEqualFast(tex));
				if (keyValuePair.Value != null)
				{
					Console.WriteLine("StoreTexture - Texture already exists, reusing it");
					return keyValuePair.Key;
				}
				int num;
				for (num = UnityEngine.Random.Range(1000, 9990); _data.ContainsKey(num); num++)
				{
				}
				_data[num] = new TextureHolder(tex, _format);
				return num;
			}
		}

		/// <summary>
		/// Get a texture based on texture ID. The same texture is returned every time, so it shouldn't be destroyed.
		/// </summary>
		/// <param name="id">ID of the texture you want to get. You get the ID when using <see cref="M:KoiSkinOverlayX.TextureStorage.StoreTexture(System.Byte[])" />.</param>
		/// <returns></returns>
		public Texture2D GetSharedTexture(int id)
		{
			lock (_data)
			{
				if (_data.TryGetValue(id, out var value))
				{
					if (value == null)
					{
						return null;
					}
					if (value.Texture.IsDestroyed())
					{
						KoikatuAPI.Logger.LogDebug((object)$"Texture ID={id} from TextureStorage was destroyed, recreating");
					}
					return value.Texture;
				}
			}
			KoikatuAPI.Logger.LogWarning((object)("Tried getting texture with nonexisting ID: " + id));
			return null;
		}
	}
}
namespace KKAPI
{
	/// <summary>
	/// Provides overall information about the game and the API itself, and provides some useful tools.
	/// More information is available in project wiki at https://github.com/ManlyMarco/KKAPI/wiki
	/// </summary>
	[BepInPlugin("marco.kkapi", "Modding API", "1.42.2")]
	[BepInDependency(/*Could not decode attribute arguments.*/)]
	[BepInDependency("com.bepis.bepinex.extendedsave", "19.4")]
	[BepInIncompatibility("com.bepis.makerapi")]
	public class KoikatuAPI : BaseUnityPlugin
	{
		private static class GlobalHooks
		{
			public static void Init()
			{
				Harmony.CreateAndPatchAll(typeof(GlobalHooks), (string)null);
			}

			/// <summary>
			/// UniTasks can fail silently if an exception is thrown and it bubbles all the way up to a unity event method that was made async.
			/// This enusres that the exception is logged. Only warning because it might be manually caught or it might not be in an unity event, in both cases not causing a silent crash.
			/// </summary>
			[HarmonyPostfix]
			[HarmonyPatch(typeof(Cysharp.Threading.Tasks.ExceptionHolder), "GetException")]
			private static void LogUnitaskException(ExceptionDispatchInfo __result)
			{
				if (__result != null)
				{
					UnityEngine.Debug.LogWarning("Exception has been thrown inside a UniTask, it might crash the task if not caught! (ExceptionHolder.GetException)\n" + __result.SourceException);
				}
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(AsyncUniTaskMethodBuilder), "SetException")]
			private static void LogUnitaskException(Exception exception)
			{
				if (exception != null && !(exception is OperationCanceledException))
				{
					UnityEngine.Debug.LogWarning("Exception has been thrown inside a UniTask, it might crash the task if not caught! (AsyncUniTaskMethodBuilder.SetException)\n" + exception);
				}
			}
		}

		internal enum RememberFilePickerFolderSetting
		{
			Enabled = 1,
			ResetOnRestart,
			Disabled
		}

		/// <summary>
		/// The studio process name for use with <see cref="T:BepInEx.BepInProcess" /> attributes.
		/// </summary>
		public const string StudioProcessName = "CharaStudio";

		/// <summary>
		/// The game process name for use with <see cref="T:BepInEx.BepInProcess" /> attributes.
		/// This is for the jp release. In almost all cases should be used together with the steam version.
		/// </summary>
		public const string GameProcessName = "KoikatsuSunshine";

		/// <summary>
		/// The VR module process name for use with <see cref="T:BepInEx.BepInProcess" /> attributes.
		/// This is for the jp release. In almost all cases should be used together with the steam version.
		/// </summary>
		public const string VRProcessName = "KoikatsuSunshine_VR";

		private static readonly bool _isVr = Application.productName == "KoikatsuSunshine_VR";

		/// <summary>
		/// Version of this assembly/plugin.
		/// WARNING: This is a const field, therefore it will be copied to your assembly!
		/// Use this field to check if the installed version of the plugin is up to date by adding this attribute to your plugin class:
		/// <code>[BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]</code>
		/// THIS VALUE WILL NOT BE READ FROM THE INSTALLED VERSION, YOU WILL READ THE VALUE FROM THIS VERSION THAT YOU COMPILE YOUR PLUGIN AGAINST!
		/// More info: https://stackoverflow.com/questions/55984/what-is-the-difference-between-const-and-readonly
		/// </summary>
		public const string VersionConst = "1.42.2";

		/// <summary>
		/// GUID of this plugin, use for checking dependancies with <see cref="T:BepInEx.BepInDependency" />."/&gt;
		/// </summary>
		public const string GUID = "marco.kkapi";

		/// <summary>
		/// Enables display of additional log messages when certain events are triggered within KKAPI. 
		/// Useful for plugin devs to understand when controller messages are fired.
		/// </summary>
		[Browsable(false)]
		public static bool EnableDebugLogging
		{
			get
			{
				return EnableDebugLoggingSetting.Value;
			}
			set
			{
				EnableDebugLoggingSetting.Value = value;
			}
		}

		private static ConfigEntry<bool> EnableDebugLoggingSetting { get; set; }

		internal static ConfigEntry<RememberFilePickerFolderSetting> RememberFilePickerFolder { get; set; }

		internal static KoikatuAPI Instance { get; private set; }

		internal static ManualLogSource Logger { get; private set; }

		/// <summary>
		/// Can be used to detect if application is currently quitting.
		/// </summary>
		/// <value>
		///   <c>true</c> if application is quitting; otherwise, <c>false</c>.
		/// </value>
		public static bool IsQuitting { get; internal set; }

		/// <summary>
		/// Occurs when application is quitting.
		/// Plugins can use this to do things like write config files and caches, or stop outstanding coroutines to prevent shutdown delays.
		/// Note: This event might not fire if the game isn't closed cleanly (hard crashes, killed process, closing the console window, etc.).
		/// </summary>
		public static event EventHandler Quitting;

		private void Awake()
		{
			BaseAwake();
			bool insideStudio = Application.productName == "CharaStudio";
			MakerAPI.Init(insideStudio);
			StudioAPI.Init(insideStudio);
			CharacterApi.Init();
			GameAPI.Init(insideStudio);
			GlobalHooks.Init();
		}

		private void Start()
		{
			AccessoriesApi.Init();
		}

		/// <summary>
		/// Get current game mode. 
		/// </summary>
		public static GameMode GetCurrentGameMode()
		{
			if (StudioAPI.InsideStudio)
			{
				return GameMode.Studio;
			}
			if (MakerAPI.InsideMaker)
			{
				return GameMode.Maker;
			}
			if (SingletonInitializer<Game>.initialized)
			{
				return GameMode.MainGame;
			}
			return GameMode.Unknown;
		}

		/// <summary>
		/// Get current version of the game.
		/// </summary>
		public static Version GetGameVersion()
		{
			return GameSystem.GameVersion;
		}

		/// <summary>
		/// Check if the game is the Steam release instead of the original Japanese release.
		/// <remarks>It's best to not rely on this and instead make the same code work in both versions (if possible).</remarks>
		/// </summary>
		[Obsolete("Not implemented yet, always false")]
		public static bool IsSteamRelease()
		{
			return false;
		}

		/// <summary>
		/// Check if this is the official VR module. Main game VR mods are ignored (returns false).
		/// </summary>
		public static bool IsVR()
		{
			return _isVr;
		}

		/// <summary>
		/// Needs to be called at the start of Awake
		/// </summary>
		private void BaseAwake()
		{
			//IL_026c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0271: Unknown result type (might be due to invalid IL or missing references)
			//IL_0296: Unknown result type (might be due to invalid IL or missing references)
			//IL_02c0: Expected O, but got Unknown
			Instance = this;
			Logger = ((BaseUnityPlugin)this).Logger;
			EnableDebugLoggingSetting = ((BaseUnityPlugin)this).Config.Bind<bool>("Debug", "Show debug messages", false, "Enables display of additional log messages when certain events are triggered within KKAPI. Useful for plugin devs to understand when controller messages are fired. Changes take effect after game restart.");
			RememberFilePickerFolder = ((BaseUnityPlugin)this).Config.Bind<RememberFilePickerFolderSetting>("General", "Remember last folder in file pickers", RememberFilePickerFolderSetting.Enabled, "If enabled, file picker dialogs will remember the last opened folder and open inside of it instead of the default folder. If disabled, the dialogs will always open in the default folder.\n\n'Reset On Restart' will remember the last opened folders only until the game is restarted, after which they will be reset.\n\nWarning: This setting only applies to plugins that use the file picker through this API.");
			Logger.LogDebug((object)$"Game version {GetGameVersion()} running under {Thread.CurrentThread.CurrentCulture.Name} culture at UTC time {DateTime.UtcNow:yyyy/MM/dd HH:mm:ss}");
			string path = Path.Combine(Paths.GameRootPath, "abdata");
			if (Directory.Exists(path))
			{
				string[] files = Directory.GetFiles(path, "add*", SearchOption.TopDirectoryOnly);
				if (files.Any())
				{
					string[] value = (from x in Enumerable.Select(files, Path.GetFileName)
						where x != null && x.Length > 3
						select x.Substring(3)).OrderBy((string x) => x, new WindowsStringComparer()).ToArray();
					Logger.LogDebug((object)("Installed DLC: " + string.Join(" ", value)));
				}
			}
			Logger.LogDebug((object)string.Format("Processor: {0} ({1} threads @ {2}MHz); RAM: {3}MB ({4}% used); OS: {5}", SystemInfo.processorType, SystemInfo.processorCount, SystemInfo.processorFrequency, SystemInfo.systemMemorySize, MemoryInfo.GetCurrentStatus()?.dwMemoryLoad.ToString() ?? "--", SystemInfo.operatingSystem));
			PrintFileIfExists(".doorstop_version");
			PrintFileIfExists("version");
			UnityEngine.SceneManagement.SceneManager.sceneLoaded += delegate(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
			{
				Logger.LogDebug((object)$"SceneManager.sceneLoaded - {scene.name} in {mode} mode");
			};
			UnityEngine.SceneManagement.SceneManager.sceneUnloaded += delegate(UnityEngine.SceneManagement.Scene scene)
			{
				Logger.LogDebug((object)("SceneManager.sceneUnloaded - " + scene.name));
			};
			UnityEngine.SceneManagement.SceneManager.activeSceneChanged += delegate(UnityEngine.SceneManagement.Scene prev, UnityEngine.SceneManagement.Scene next)
			{
				Logger.LogDebug((object)("SceneManager.activeSceneChanged - from " + prev.name + " to " + next.name));
			};
			if (!TomlTypeConverter.CanConvert(typeof(Rect)))
			{
				TomlTypeConverter.AddConverter(typeof(Rect), new TypeConverter
				{
					ConvertToObject = delegate(string s, Type type)
					{
						Rect rect = default(Rect);
						if (s != null)
						{
							string text = s.Trim('{', '}').Replace(" ", "");
							string[] array = text.Split(',');
							foreach (string text2 in array)
							{
								string[] array2 = text2.Split(':');
								if (array2.Length == 2 && float.TryParse(array2[1], out var result))
								{
									switch (array2[0].Trim('"'))
									{
									case "x":
										rect.x = result;
										break;
									case "y":
										rect.y = result;
										break;
									case "width":
									case "z":
										rect.width = result;
										break;
									case "height":
									case "w":
										rect.height = result;
										break;
									}
								}
							}
						}
						return rect;
					},
					ConvertToString = delegate(object o, Type type)
					{
						Rect rect = (Rect)o;
						return string.Format(CultureInfo.InvariantCulture, "{{ \"x\":{0}, \"y\":{1}, \"width\":{2}, \"height\":{3} }}", rect.x, rect.y, rect.width, rect.height);
					}
				});
			}
			OpenFileDialog.LoadFilePickerStates();
			Application.quitting += delegate
			{
				OnQuitting(EventArgs.Empty);
			};
			static void PrintFileIfExists(string fileName)
			{
				string path2 = Path.Combine(Paths.GameRootPath, fileName);
				if (File.Exists(path2))
				{
					string text = File.ReadAllText(path2).Trim();
					if (!string.IsNullOrEmpty(text))
					{
						Logger.LogDebug((object)("Contents of the '" + fileName + "' file: " + text));
					}
				}
			}
		}

		/// <summary>
		/// Check if a plugin is loaded and has at least the minimum version. 
		/// If the plugin is missing or older than minimumVersion, user is shown an error message on screen and false is returned.
		/// Warning: Run only from Start, not from constructor or Awake because some plugins might not be loaded yet!
		/// </summary>
		/// <param name="origin">Your plugin</param>
		/// <param name="guid">Guid of the plugin your plugin is dependant on</param>
		/// <param name="minimumVersion">Minimum version of the required plugin</param>
		/// <param name="level">Level of the issue - <code>Error</code> if plugin can't work, <code>Warning</code> if there might be issues, or <code>None</code> to not show any message.</param>
		/// <returns>True if plugin exists and it's version equals or is newer than minimumVersion, otherwise false</returns>
		[Obsolete("Use the new overload of BepInDependency attribute with version number")]
		public unsafe static bool CheckRequiredPlugin(BaseUnityPlugin origin, string guid, Version minimumVersion, LogLevel level = (LogLevel)2)
		{
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
			BepInPlugin val = Chainloader.Plugins.Where((BaseUnityPlugin x) => (UnityEngine.Object)(object)x != null).Select((Func<BaseUnityPlugin, BepInPlugin>)MetadataHelper.GetMetadata).FirstOrDefault((BepInPlugin x) => x.GUID == guid);
			if (val == null)
			{
				if ((int)level != 0)
				{
					Logger.LogMessage((object)(((object)(*(LogLevel*)(&level))/*cast due to .constrained prefix*/).ToString().ToUpper() + ": Plugin \"" + guid + "\" required by \"" + MetadataHelper.GetMetadata((object)origin).GUID + "\" was not found!"));
				}
				return false;
			}
			if (minimumVersion > val.Version)
			{
				if ((int)level != 0)
				{
					Logger.LogMessage((object)$"{((object)(*(LogLevel*)(&level))/*cast due to .constrained prefix*/).ToString().ToUpper()}: Plugin \"{guid}\" required by \"{MetadataHelper.GetMetadata((object)origin).GUID}\" is outdated! At least v{minimumVersion} is needed!");
				}
				return false;
			}
			return true;
		}

		/// <summary>
		/// Check if a plugin that is not compatible with your plugin is loaded. 
		/// If the plugin is loaded, user is shown a warning message on screen and true is returned.
		/// Warning: Run only from Start, not from constructor or Awake because some plugins might not be loaded yet!
		/// </summary>
		/// <param name="origin">Your plugin</param>
		/// <param name="guid">Guid of the plugin your plugin is incompatible with</param>
		/// <param name="level">Level of the issue - <code>Error</code> if plugin can't work, <code>Warning</code> if there might be issues, or <code>None</code> to not show any message.</param>
		/// <returns>True if plugin exists, otherwise false</returns>
		[Obsolete("Use the new attribute BepInIncompatibility")]
		public unsafe static bool CheckIncompatiblePlugin(BaseUnityPlugin origin, string guid, LogLevel level = (LogLevel)4)
		{
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			BepInPlugin val = Chainloader.Plugins.Where((BaseUnityPlugin x) => (UnityEngine.Object)(object)x != null).Select((Func<BaseUnityPlugin, BepInPlugin>)MetadataHelper.GetMetadata).FirstOrDefault((BepInPlugin x) => x.GUID == guid);
			if (val != null)
			{
				if ((int)level != 0)
				{
					Logger.LogMessage((object)(((object)(*(LogLevel*)(&level))/*cast due to .constrained prefix*/).ToString().ToUpper() + ": Plugin \"" + guid + "\" is incompatible with \"" + MetadataHelper.GetMetadata((object)origin).GUID + "\"!"));
				}
				return true;
			}
			return false;
		}

		/// <summary>
		/// Invoke the Action on the main unity thread. Use to synchronize your threads.
		/// </summary>
		[Obsolete("Use ThreadingHelper.Instance.StartSyncInvoke instead")]
		public static void SynchronizedInvoke(Action action)
		{
			ThreadingHelper.Instance.StartSyncInvoke(action);
		}

		internal static void Assert(bool success, string error)
		{
			if (!success)
			{
				Logger.LogWarning((object)("Assertion failed: " + error + "\nat: " + new StackTrace(1)));
			}
		}

		internal void OnQuitting(EventArgs e)
		{
			Logger.LogDebug((object)"The game is quitting!");
			IsQuitting = true;
			KoikatuAPI.Quitting?.Invoke(this, e);
		}
	}
	[BepInPlugin("marco.authordata", "Card Author Data", "1.42.2")]
	[BepInDependency(/*Could not decode attribute arguments.*/)]
	[Browsable(false)]
	[UsedImplicitly]
	internal class CardAuthorData : BaseUnityPlugin
	{
		private sealed class CardAuthorDataController : CharaCustomFunctionController
		{
			private const string AuthorsKey = "Authors";

			private string[] _previousAuthors;

			public IEnumerable<string> Authors
			{
				get
				{
					IEnumerable<string> previousAuthors = _previousAuthors;
					return previousAuthors ?? Enumerable.Empty<string>();
				}
			}

			protected override void OnCardBeingSaved(GameMode currentGameMode)
			{
				//IL_0039: Unknown result type (might be due to invalid IL or missing references)
				//IL_003e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0045: Unknown result type (might be due to invalid IL or missing references)
				//IL_0065: Expected O, but got Unknown
				List<string> list = Authors.ToList();
				if (MakerAPI.InsideMaker && list.LastOrDefault() != CurrentNickname)
				{
					list.Add(CurrentNickname);
				}
				if (list.Any())
				{
					SetExtendedData(new PluginData
					{
						version = 1,
						data = { 
						{
							"Authors",
							(object)LZ4MessagePackSerializer.Serialize(list.ToArray())
						} }
					});
				}
				else
				{
					SetExtendedData(null);
				}
			}

			protected override void OnReload(GameMode currentGameMode)
			{
				CharacterLoadFlags characterLoadFlags = MakerAPI.GetCharacterLoadFlags();
				if (ReplacedParts(characterLoadFlags) > 3)
				{
					_previousAuthors = ((characterLoadFlags == null) ? null : new string[1] { "[Unknown]" });
				}
				PluginData extendedData = GetExtendedData();
				if (extendedData != null && extendedData.data.TryGetValue("Authors", out var value) && value is byte[] bytes)
				{
					_previousAuthors = LZ4MessagePackSerializer.Deserialize<string[]>(bytes);
				}
			}
		}

		[UsedImplicitly]
		private sealed class CardAuthorDataWindow : ImguiWindow<CardAuthorDataWindow>
		{
			private readonly StringBuilder _sb = new StringBuilder();

			private CardAuthorDataController _controller;

			private Vector2 _scrollPos;

			public static void ToggleShowWindow()
			{
				if (MakerAPI.InsideAndLoaded)
				{
					GameObject gameObject = MakerAPI.GetMakerBase().gameObject;
					CardAuthorDataWindow cardAuthorDataWindow = gameObject.GetComponent<CardAuthorDataWindow>();
					if (!cardAuthorDataWindow)
					{
						cardAuthorDataWindow = gameObject.AddComponent<CardAuthorDataWindow>();
					}
					cardAuthorDataWindow.enabled = !cardAuthorDataWindow.enabled;
				}
			}

			private void Awake()
			{
				Title = "Author history";
			}

			protected override Rect GetDefaultWindowRect(Rect screenRect)
			{
				return new Rect(80f, 80f, 350f, 350f);
			}

			protected override void OnEnable()
			{
				_controller = MakerAPI.GetCharacterControl().GetComponent<CardAuthorDataController>();
				base.OnEnable();
			}

			private void Update()
			{
				if (!MakerAPI.InsideAndLoaded)
				{
					UnityEngine.Object.DestroyImmediate(this);
				}
			}

			protected override void DrawContents()
			{
				GUILayout.Label("Character: " + _controller.ChaFileControl.GetFancyCharacterName(), GUI.skin.box, IMGUIUtils.EmptyLayoutOptions);
				GUILayout.Label("This card was modified or resaved by following people:\n(in chronological order, starting from oldest edits)", IMGUIUtils.EmptyLayoutOptions);
				_scrollPos = GUILayout.BeginScrollView(_scrollPos, alwaysShowHorizontal: false, alwaysShowVertical: true, IMGUIUtils.EmptyLayoutOptions);
				GUILayout.BeginVertical(IMGUIUtils.EmptyLayoutOptions);
				int num = 0;
				foreach (string author in _controller.Authors)
				{
					num++;
					GUILayout.BeginHorizontal(GUI.skin.box, IMGUIUtils.EmptyLayoutOptions);
					_sb.Length = 0;
					_sb.Append('#');
					_sb.Append(num);
					if (num < 10)
					{
						_sb.Append(' ');
					}
					_sb.Append(' ');
					_sb.Append('\t');
					_sb.Append(author);
					GUILayout.Label(_sb.ToString(), GUILayout.ExpandWidth(expand: true));
					GUILayout.EndHorizontal();
				}
				if (num == 0)
				{
					GUILayout.Label("This card has no author history", GUILayout.ExpandWidth(expand: true));
				}
				GUILayout.EndVertical();
				GUILayout.EndScrollView();
			}
		}

		private const string DefaultNickname = "Anonymous";

		private const string GUID = "marco.authordata";

		private static ManualLogSource _logger;

		private static ConfigEntry<string> _nickname;

		private MakerText _authorText;

		private static readonly HashSet<char> _invalidChars = new HashSet<char>(Path.GetInvalidFileNameChars().Concat(new char[1] { '.' }));

		private static string CurrentNickname
		{
			get
			{
				string value = _nickname.Value;
				if (!string.IsNullOrEmpty(value))
				{
					return value;
				}
				return "Anonymous";
			}
		}

		private void Start()
		{
			if (KoikatuAPI.GetCurrentGameMode() != GameMode.Studio)
			{
				_logger = ((BaseUnityPlugin)this).Logger;
				MakerCardSave.RegisterNewCardSavePathModifier(null, FilenameModifier);
				_nickname = ((BaseUnityPlugin)this).Config.Bind<string>("", "Nickname", "Anonymous", "Your nickname that will be saved to your cards and used in the card filenames.");
				CharacterApi.RegisterExtraBehaviour<CardAuthorDataController>("marco.authordata");
				MakerAPI.RegisterCustomSubCategories += MakerAPI_RegisterCustomSubCategories;
				MakerAPI.ReloadCustomInterface += MakerApiOnReloadCustomInterface;
				MakerAPI.MakerExiting += MakerAPI_MakerExiting;
			}
		}

		private static string FilenameModifier(string currentCardName)
		{
			string result = currentCardName;
			try
			{
				if (currentCardName.EndsWith(".png"))
				{
					currentCardName = currentCardName.Substring(0, currentCardName.Length - 4);
				}
				ChaFileParameter fileParam = MakerAPI.GetCharacterControl().fileParam;
				string text = fileParam.fullname.Trim();
				if (text.Length == 0)
				{
					text = fileParam.nickname.Trim();
				}
				string text2 = "_" + text;
				if (CurrentNickname != "Anonymous")
				{
					text2 = text2 + "_" + CurrentNickname;
				}
				text2 = new string(text2.Select((char c) => (!_invalidChars.Contains(c)) ? c : '_').ToArray());
				return currentCardName + text2;
			}
			catch (Exception ex)
			{
				_logger.LogError((object)ex);
				return result;
			}
		}

		private static string GetAuthorsText()
		{
			string[] array = MakerAPI.GetCharacterControl().GetComponent<CardAuthorDataController>().Authors.ToArray();
			if (array.Length == 0)
			{
				return "Author history: [Empty]";
			}
			List<string> list = new List<string>(array.Length);
			foreach (string item in array.Reverse())
			{
				if (!list.Contains(item))
				{
					list.Add(item);
				}
			}
			string text = array[0];
			if (list.Count > 6)
			{
				list.RemoveRange(5, list.Count - 5);
				list.Add("...");
				list.Add(text);
			}
			else if (list[list.Count - 1] != text)
			{
				list.Add(text);
			}
			list.Reverse();
			return "Author history: " + string.Join(" > ", list.ToArray());
		}

		private void MakerAPI_MakerExiting(object sender, EventArgs e)
		{
			_authorText = null;
		}

		private void MakerAPI_RegisterCustomSubCategories(object sender, RegisterSubCategoriesEvent e)
		{
			MakerCategory character = MakerConstants.Parameter.Character;
			_authorText = e.AddControl(new MakerText("Not loaded yet...", character, (BaseUnityPlugin)(object)this));
			e.AddControl(new MakerButton("Open full author history", character, (BaseUnityPlugin)(object)this)).OnClick.AddListener(CardAuthorDataWindow.ToggleShowWindow);
			MakerTextbox tb = e.AddControl(new MakerTextbox(character, "Your nickname", "Anonymous", (BaseUnityPlugin)(object)this));
			tb.Value = CurrentNickname;
			tb.ValueChanged.Subscribe(delegate(string s)
			{
				if (string.IsNullOrEmpty(s))
				{
					tb.Value = "Anonymous";
				}
				else
				{
					_nickname.Value = s;
				}
			});
			e.AddControl(new MakerText("Your nickname will be saved to the card and used in the card's filename. This setting is global.", character, (BaseUnityPlugin)(object)this)
			{
				TextColor = MakerText.ExplanationGray
			});
		}

		private void MakerApiOnReloadCustomInterface(object sender, EventArgs eventArgs)
		{
			if (_authorText != null)
			{
				string authorsText = GetAuthorsText();
				_authorText.Text = authorsText;
			}
		}

		private static int ReplacedParts(CharacterLoadFlags flags)
		{
			if (flags == null)
			{
				return 5;
			}
			bool[] source = new bool[5] { flags.Body, flags.Clothes, flags.Face, flags.Hair, flags.Parameters };
			return source.Sum((bool b) => b ? 1 : 0);
		}
	}
	internal static class Constants
	{
		public const string Version = "1.42.2";

		public const string GUID = "marco.kkapi";
	}
	/// <summary>
	/// Current gameplay mode the game is in
	/// </summary>
	public enum GameMode
	{
		/// <summary>
		/// Anywhere else, including main menu
		/// </summary>
		Unknown,
		/// <summary>
		/// Inside character maker (can be started from main menu or from class roster)
		/// </summary>
		Maker,
		/// <summary>
		/// Anywhere inside CharaStudio.exe
		/// </summary>
		Studio,
		/// <summary>
		/// Anywhere inside the main game.
		/// Includes everything after starting a new game from title screen and after loading a saved game.
		/// This means this includes story scenes, night menu, roaming around and h scenes inside story mode.
		/// This does not hoverwer include the character maker launched from within the class menu.
		/// </summary>
		MainGame
	}
	/// <summary>
	/// Game-agnostic version of Manager.Scene. It allows using the same code in all games without any #if directives.
	/// </summary>
	public static class SceneApi
	{
		/// <summary>
		/// Get name of the currently loaded overlay scene (eg. exit game box, config, confirmation dialogs).
		/// </summary>
		public static string GetAddSceneName()
		{
			return Manager.Scene.AddSceneName;
		}

		/// <summary>
		/// Get name of the currently loaded game scene (eg. maker, h, adv).
		/// </summary>
		public static string GetLoadSceneName()
		{
			return Manager.Scene.LoadSceneName;
		}

		/// <summary>
		/// True if loading screen is being displayed, or if screen is currently fading in or out.
		/// </summary>
		public static bool GetIsNowLoadingFade()
		{
			return Manager.Scene.IsNowLoadingFade;
		}

		/// <summary>
		/// True if loading screen is being displayed.
		/// </summary>
		public static bool GetIsNowLoading()
		{
			return Manager.Scene.IsNowLoading;
		}

		/// <summary>
		/// True if screen is currently fading in or out.
		/// </summary>
		public static bool GetIsFadeNow()
		{
			return Manager.Scene.IsFadeNow;
		}

		/// <summary>
		/// True if a dialog box or some other overlapping menu is shown (e.g. exit dialog after pressing esc).
		/// </summary>
		public static bool GetIsOverlap()
		{
			return Manager.Scene.IsOverlap;
		}
	}
	/// <summary>
	/// ChaReference.RefObjKey alternative that's safe to use in all versions of KK.
	/// Why: Everything below CORRECT_HAND_R changed its int value in darkness vs other versions of the game, so if you
	/// compile with darkness dll you will unexpectedly get a different enum value in games without darkness.
	/// This isn't necessary in KKS, but it does work in case your code targets both KK and KKS.
	/// Only values present in all game versions are provided (the lowest common denominator being KK without Dankness).
	/// </summary>
	public static class UniversalRefObjKey
	{
		public static readonly ChaReference.RefObjKey HeadParent;

		public static readonly ChaReference.RefObjKey HairParent;

		public static readonly ChaReference.RefObjKey a_n_hair_pony;

		public static readonly ChaReference.RefObjKey a_n_hair_twin_L;

		public static readonly ChaReference.RefObjKey a_n_hair_twin_R;

		public static readonly ChaReference.RefObjKey a_n_hair_pin;

		public static readonly ChaReference.RefObjKey a_n_hair_pin_R;

		public static readonly ChaReference.RefObjKey a_n_headtop;

		public static readonly ChaReference.RefObjKey a_n_headflont;

		public static readonly ChaReference.RefObjKey a_n_head;

		public static readonly ChaReference.RefObjKey a_n_headside;

		public static readonly ChaReference.RefObjKey a_n_megane;

		public static readonly ChaReference.RefObjKey a_n_earrings_L;

		public static readonly ChaReference.RefObjKey a_n_earrings_R;

		public static readonly ChaReference.RefObjKey a_n_nose;

		public static readonly ChaReference.RefObjKey a_n_mouth;

		public static readonly ChaReference.RefObjKey a_n_neck;

		public static readonly ChaReference.RefObjKey a_n_bust_f;

		public static readonly ChaReference.RefObjKey a_n_bust;

		public static readonly ChaReference.RefObjKey a_n_nip_L;

		public static readonly ChaReference.RefObjKey a_n_nip_R;

		public static readonly ChaReference.RefObjKey a_n_back;

		public static readonly ChaReference.RefObjKey a_n_back_L;

		public static readonly ChaReference.RefObjKey a_n_back_R;

		public static readonly ChaReference.RefObjKey a_n_waist;

		public static readonly ChaReference.RefObjKey a_n_waist_f;

		public static readonly ChaReference.RefObjKey a_n_waist_b;

		public static readonly ChaReference.RefObjKey a_n_waist_L;

		public static readonly ChaReference.RefObjKey a_n_waist_R;

		public static readonly ChaReference.RefObjKey a_n_leg_L;

		public static readonly ChaReference.RefObjKey a_n_leg_R;

		public static readonly ChaReference.RefObjKey a_n_knee_L;

		public static readonly ChaReference.RefObjKey a_n_knee_R;

		public static readonly ChaReference.RefObjKey a_n_ankle_L;

		public static readonly ChaReference.RefObjKey a_n_ankle_R;

		public static readonly ChaReference.RefObjKey a_n_heel_L;

		public static readonly ChaReference.RefObjKey a_n_heel_R;

		public static readonly ChaReference.RefObjKey a_n_shoulder_L;

		public static readonly ChaReference.RefObjKey a_n_shoulder_R;

		public static readonly ChaReference.RefObjKey a_n_elbo_L;

		public static readonly ChaReference.RefObjKey a_n_elbo_R;

		public static readonly ChaReference.RefObjKey a_n_arm_L;

		public static readonly ChaReference.RefObjKey a_n_arm_R;

		public static readonly ChaReference.RefObjKey a_n_wrist_L;

		public static readonly ChaReference.RefObjKey a_n_wrist_R;

		public static readonly ChaReference.RefObjKey a_n_hand_L;

		public static readonly ChaReference.RefObjKey a_n_hand_R;

		public static readonly ChaReference.RefObjKey a_n_ind_L;

		public static readonly ChaReference.RefObjKey a_n_ind_R;

		public static readonly ChaReference.RefObjKey a_n_mid_L;

		public static readonly ChaReference.RefObjKey a_n_mid_R;

		public static readonly ChaReference.RefObjKey a_n_ring_L;

		public static readonly ChaReference.RefObjKey a_n_ring_R;

		public static readonly ChaReference.RefObjKey a_n_dan;

		public static readonly ChaReference.RefObjKey a_n_kokan;

		public static readonly ChaReference.RefObjKey a_n_ana;

		public static readonly ChaReference.RefObjKey k_f_handL_00;

		public static readonly ChaReference.RefObjKey k_f_handR_00;

		public static readonly ChaReference.RefObjKey k_f_shoulderL_00;

		public static readonly ChaReference.RefObjKey k_f_shoulderR_00;

		public static readonly ChaReference.RefObjKey ObjEyeline;

		public static readonly ChaReference.RefObjKey ObjEyelineLow;

		public static readonly ChaReference.RefObjKey ObjEyebrow;

		public static readonly ChaReference.RefObjKey ObjNoseline;

		public static readonly ChaReference.RefObjKey ObjEyeL;

		public static readonly ChaReference.RefObjKey ObjEyeR;

		public static readonly ChaReference.RefObjKey ObjEyeWL;

		public static readonly ChaReference.RefObjKey ObjEyeWR;

		public static readonly ChaReference.RefObjKey ObjFace;

		public static readonly ChaReference.RefObjKey ObjDoubleTooth;

		public static readonly ChaReference.RefObjKey ObjBody;

		public static readonly ChaReference.RefObjKey ObjNip;

		public static readonly ChaReference.RefObjKey N_FaceSpecial;

		public static readonly ChaReference.RefObjKey CORRECT_ARM_L;

		public static readonly ChaReference.RefObjKey CORRECT_ARM_R;

		public static readonly ChaReference.RefObjKey CORRECT_HAND_L;

		public static readonly ChaReference.RefObjKey CORRECT_HAND_R;

		public static readonly ChaReference.RefObjKey S_ANA;

		public static readonly ChaReference.RefObjKey S_TongueF;

		public static readonly ChaReference.RefObjKey S_TongueB;

		public static readonly ChaReference.RefObjKey S_Son;

		public static readonly ChaReference.RefObjKey S_SimpleTop;

		public static readonly ChaReference.RefObjKey S_SimpleBody;

		public static readonly ChaReference.RefObjKey S_SimpleTongue;

		public static readonly ChaReference.RefObjKey S_MNPA;

		public static readonly ChaReference.RefObjKey S_MNPB;

		public static readonly ChaReference.RefObjKey S_MOZ_ALL;

		public static readonly ChaReference.RefObjKey S_GOMU;

		public static readonly ChaReference.RefObjKey S_CTOP_T_DEF;

		public static readonly ChaReference.RefObjKey S_CTOP_T_NUGE;

		public static readonly ChaReference.RefObjKey S_CTOP_B_DEF;

		public static readonly ChaReference.RefObjKey S_CTOP_B_NUGE;

		public static readonly ChaReference.RefObjKey S_CBOT_T_DEF;

		public static readonly ChaReference.RefObjKey S_CBOT_T_NUGE;

		public static readonly ChaReference.RefObjKey S_CBOT_B_DEF;

		public static readonly ChaReference.RefObjKey S_CBOT_B_NUGE;

		public static readonly ChaReference.RefObjKey S_UWT_T_DEF;

		public static readonly ChaReference.RefObjKey S_UWT_T_NUGE;

		public static readonly ChaReference.RefObjKey S_UWT_B_DEF;

		public static readonly ChaReference.RefObjKey S_UWT_B_NUGE;

		public static readonly ChaReference.RefObjKey S_UWB_T_DEF;

		public static readonly ChaReference.RefObjKey S_UWB_T_NUGE;

		public static readonly ChaReference.RefObjKey S_UWB_B_DEF;

		public static readonly ChaReference.RefObjKey S_UWB_B_NUGE;

		public static readonly ChaReference.RefObjKey S_UWB_B_NUGE2;

		public static readonly ChaReference.RefObjKey S_PANST_DEF;

		public static readonly ChaReference.RefObjKey S_PANST_NUGE;

		public static readonly ChaReference.RefObjKey S_TPARTS_00_DEF;

		public static readonly ChaReference.RefObjKey S_TPARTS_00_NUGE;

		public static readonly ChaReference.RefObjKey S_TPARTS_01_DEF;

		public static readonly ChaReference.RefObjKey S_TPARTS_01_NUGE;

		public static readonly ChaReference.RefObjKey S_TPARTS_02_DEF;

		public static readonly ChaReference.RefObjKey S_TPARTS_02_NUGE;

		public static readonly ChaReference.RefObjKey ObjBraDef;

		public static readonly ChaReference.RefObjKey ObjBraNuge;

		public static readonly ChaReference.RefObjKey ObjInnerDef;

		public static readonly ChaReference.RefObjKey ObjInnerNuge;

		public static readonly ChaReference.RefObjKey S_TEARS_01;

		public static readonly ChaReference.RefObjKey S_TEARS_02;

		public static readonly ChaReference.RefObjKey S_TEARS_03;

		public static readonly ChaReference.RefObjKey N_EyeBase;

		public static readonly ChaReference.RefObjKey N_Hitomi;

		public static readonly ChaReference.RefObjKey N_Gag00;

		public static readonly ChaReference.RefObjKey N_Gag01;

		public static readonly ChaReference.RefObjKey N_Gag02;

		public static readonly ChaReference.RefObjKey DB_SKIRT_TOP;

		public static readonly ChaReference.RefObjKey DB_SKIRT_TOPA;

		public static readonly ChaReference.RefObjKey DB_SKIRT_TOPB;

		public static readonly ChaReference.RefObjKey DB_SKIRT_BOT;

		public static readonly ChaReference.RefObjKey F_ADJUSTWIDTHSCALE;

		public static readonly ChaReference.RefObjKey A_ROOTBONE;

		public static readonly ChaReference.RefObjKey BUSTUP_TARGET;

		public static readonly ChaReference.RefObjKey NECK_LOOK_TARGET;

		static UniversalRefObjKey()
		{
			FieldInfo[] fields = typeof(UniversalRefObjKey).GetFields(BindingFlags.Static | BindingFlags.Public);
			foreach (FieldInfo fieldInfo in fields)
			{
				fieldInfo.SetValue(null, Enum.Parse(typeof(ChaReference.RefObjKey), fieldInfo.Name));
			}
		}
	}
}
namespace KKAPI.Studio
{
	/// <summary>
	/// Provides a way to add custom menu items to CharaStudio, and gives useful methods for interfacing with the studio.
	/// </summary>
	public static class StudioAPI
	{
		private static class Hooks
		{
			public static void SetupHooks()
			{
				Harmony.CreateAndPatchAll(typeof(Hooks), (string)null);
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(MPCharCtrl), "OnClickRoot", new Type[] { typeof(int) })]
			public static void OnClickRoot(int _idx, OCIChar ___m_OCIChar)
			{
				((MonoBehaviour)(object)KoikatuAPI.Instance).StartCoroutine(DelayedUpdateTrigger());
				IEnumerator DelayedUpdateTrigger()
				{
					yield return CoroutineUtils.WaitForEndOfFrame;
					if (_idx == 0)
					{
						foreach (CurrentStateCategory customCurrentStateCategory in _customCurrentStateCategories)
						{
							customCurrentStateCategory.UpdateInfo(___m_OCIChar);
						}
					}
				}
			}
		}

		private static readonly List<CurrentStateCategory> _customCurrentStateCategories = new List<CurrentStateCategory>();

		private static GameObject _customStateRoot;

		/// <summary>
		/// True if we are currently inside CharaStudio.exe
		/// </summary>
		public static bool InsideStudio { get; private set; }

		/// <summary>
		/// True inside studio after it finishes loading the interface (when the starting loading screen finishes), 
		/// right before custom controls are created.
		/// </summary>
		public static bool StudioLoaded { get; private set; }

		/// <summary>
		/// Fires once after studio finished loading the interface, right before custom controls are created.
		/// </summary>
		public static event EventHandler StudioLoadedChanged;

		/// <summary>
		/// Add a new custom category to the Anim &gt; CurrentState tab in the studio top-left menu.
		/// Can use this at any point.
		/// </summary>
		[Obsolete("Use GetOrCreateCurrentStateCategory instead")]
		public static void CreateCurrentStateCategory(CurrentStateCategory category)
		{
			if (category == null)
			{
				throw new ArgumentNullException("category");
			}
			if (!InsideStudio)
			{
				KoikatuAPI.Logger.LogDebug((object)"Tried to run StudioAPI.CreateCurrentStateCategory outside of studio!");
				return;
			}
			if (StudioLoaded)
			{
				CreateCategory(category);
			}
			_customCurrentStateCategories.Add(category);
		}

		/// <summary>
		/// Add a new custom category to the Anim &gt; CurrentState tab in the studio top-left menu.
		/// Can use this at any point. Always returns null outside of studio.
		/// If the name is empty or null, the Misc/Other category is returned.
		/// </summary>
		public static CurrentStateCategory GetOrCreateCurrentStateCategory(string name)
		{
			if (!InsideStudio)
			{
				KoikatuAPI.Logger.LogDebug((object)"Tried to run StudioAPI.CreateCurrentStateCategory outside of studio!");
				return null;
			}
			if (string.IsNullOrEmpty(name))
			{
				name = "Misc/Other";
			}
			CurrentStateCategory currentStateCategory = _customCurrentStateCategories.FirstOrDefault((CurrentStateCategory x) => x.CategoryName == name);
			if (currentStateCategory != null)
			{
				return currentStateCategory;
			}
			CurrentStateCategory currentStateCategory2 = new CurrentStateCategory(name);
			if (StudioLoaded)
			{
				CreateCategory(currentStateCategory2);
			}
			_customCurrentStateCategories.Add(currentStateCategory2);
			return currentStateCategory2;
		}

		/// <summary>
		/// Get all instances of this controller that belong to characters that are selected in Studio's Workspace.
		/// </summary>
		public static IEnumerable<T> GetSelectedControllers<T>() where T : CharaCustomFunctionController
		{
			return from x in GetSelectedCharacters().Select(delegate(OCIChar x)
				{
					ChaControl chaControl = x.GetChaControl();
					return ((object)chaControl == null) ? null : chaControl.GetComponent<T>();
				})
				where x != null
				select x;
		}

		/// <summary>
		/// Get all character objects currently selected in Studio's Workspace.
		/// </summary>
		public static IEnumerable<OCIChar> GetSelectedCharacters()
		{
			return GetSelectedObjects().OfType<OCIChar>();
		}

		/// <summary>
		/// Get all objects (all types) currently selected in Studio's Workspace.
		/// </summary>
		public static IEnumerable<ObjectCtrlInfo> GetSelectedObjects()
		{
			if (!StudioLoaded)
			{
				yield break;
			}
			TreeNodeObject[] selectNodes = Singleton<global::Studio.Studio>.Instance.treeNodeCtrl.selectNodes;
			for (int i = 0; i < selectNodes.Length; i++)
			{
				if (Singleton<global::Studio.Studio>.Instance.dicInfo.TryGetValue(selectNodes[i], out var value))
				{
					yield return value;
				}
			}
		}

		private static void CreateCategory(CurrentStateCategory category)
		{
			if (category == null)
			{
				throw new ArgumentNullException("category");
			}
			if (_customStateRoot == null)
			{
				_customStateRoot = GameObject.Find("StudioScene/Canvas Main Menu/02_Manipulate/00_Chara/01_State/Viewport/Content");
			}
			category.CreateCategory(_customStateRoot);
		}

		internal static void Init(bool insideStudio)
		{
			InsideStudio = insideStudio;
			if (insideStudio)
			{
				Hooks.SetupHooks();
				StudioSaveLoadApi.Init();
				UnityEngine.SceneManagement.SceneManager.sceneLoaded += SceneLoaded;
			}
		}

		private static void SceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode loadSceneMode)
		{
			if (StudioLoaded || !(scene.name == "Studio"))
			{
				return;
			}
			StudioLoaded = true;
			if (StudioAPI.StudioLoadedChanged != null)
			{
				Delegate[] invocationList = StudioAPI.StudioLoadedChanged.GetInvocationList();
				foreach (Delegate obj in invocationList)
				{
					try
					{
						((EventHandler)obj)(KoikatuAPI.Instance, EventArgs.Empty);
					}
					catch (Exception ex)
					{
						KoikatuAPI.Logger.LogError((object)ex);
					}
				}
			}
			((MonoBehaviour)(object)KoikatuAPI.Instance).StartCoroutine(DelayedLoadCo());
			static IEnumerator DelayedLoadCo()
			{
				yield return null;
				foreach (CurrentStateCategory customCurrentStateCategory in _customCurrentStateCategories)
				{
					CreateCategory(customCurrentStateCategory);
				}
				yield return null;
				CustomToolbarButtons.OnStudioLoaded();
			}
		}

		[Conditional("DEBUG")]
		private static void DebugControls()
		{
			CurrentStateCategory orCreateCurrentStateCategory = GetOrCreateCurrentStateCategory("Control test category");
			orCreateCurrentStateCategory.AddControl(new CurrentStateCategoryToggle("Test 1", 2, delegate(OCIChar c)
			{
				KoikatuAPI.Logger.LogMessage((object)(c?.charInfo?.name + " - updateValue"));
				return 1;
			})).Value.Subscribe(delegate(int val)
			{
				KoikatuAPI.Logger.LogMessage((object)val);
			});
			orCreateCurrentStateCategory.AddControl(new CurrentStateCategoryToggle("Test 2", 3, (OCIChar c) => 2)).Value.Subscribe(delegate(int val)
			{
				KoikatuAPI.Logger.LogMessage((object)val);
			});
			orCreateCurrentStateCategory.AddControl(new CurrentStateCategoryToggle("Test 3", 4, (OCIChar c) => 3)).Value.Subscribe(delegate(int val)
			{
				KoikatuAPI.Logger.LogMessage((object)val);
			});
			CurrentStateCategory orCreateCurrentStateCategory2 = GetOrCreateCurrentStateCategory("Control test category");
			orCreateCurrentStateCategory2.AddControl(new CurrentStateCategorySwitch("Test add", (OCIChar c) => true)).Value.Subscribe(delegate(bool val)
			{
				KoikatuAPI.Logger.LogMessage((object)val);
			});
			orCreateCurrentStateCategory2.AddControl(new CurrentStateCategorySlider("Test slider", (OCIChar c) => 0.75f)).Value.Subscribe(delegate(float val)
			{
				KoikatuAPI.Logger.LogMessage((object)val);
			});
			orCreateCurrentStateCategory2.AddControl(new CurrentStateCategoryDropdown("dropdown test", new string[3] { "item 1", "i2", "test 3" }, (OCIChar c) => 1)).Value.Subscribe(delegate(int val)
			{
				KoikatuAPI.Logger.LogMessage((object)("dd " + val));
			});
			orCreateCurrentStateCategory2.AddControls(new CurrentStateCategoryColorPicker("Test Color Picker", (OCIChar c) => Color.white, delegate(Color c)
			{
				ManualLogSource logger = KoikatuAPI.Logger;
				Color color = c;
				logger.LogMessage((object)("color picker: " + color.ToString()));
			}));
			CustomToolbarButtons.AddLeftToolbarButton(new Texture2D(32, 32), delegate
			{
				Console.WriteLine("click");
			});
			CustomToolbarButtons.AddLeftToolbarToggle(new Texture2D(55, 32), initialValue: true, delegate(bool v)
			{
				Console.WriteLine("click " + v);
			});
		}
	}
	/// <summary>
	/// Useful extensions for studio metaobjects
	/// </summary>
	public static class StudioObjectExtensions
	{
		private static global::Studio.Studio Studio => Singleton<global::Studio.Studio>.Instance;

		/// <summary>
		/// Get the ObjectCtrlInfo object that uses this ObjectInfo.
		/// If the object was not found in current scene, null is returned.
		/// </summary>
		public static ObjectCtrlInfo GetObjectCtrlInfo(this ObjectInfo obj)
		{
			if (obj == null)
			{
				throw new ArgumentNullException("obj");
			}
			if (Studio == null)
			{
				throw new InvalidOperationException("Studio is not initialized yet!");
			}
			return Studio.dicObjectCtrl.Values.FirstOrDefault((ObjectCtrlInfo x) => x.objectInfo == obj);
		}

		/// <summary>
		/// Get the ID of this object as used in the currently loaded scene.
		/// If the object was not found in current scene, -1 is returned.
		/// </summary>
		public static int GetSceneId(this ObjectCtrlInfo obj)
		{
			if (obj == null)
			{
				throw new ArgumentNullException("obj");
			}
			if (Studio == null)
			{
				throw new InvalidOperationException("Studio is not initialized yet!");
			}
			if (Studio.dicObjectCtrl.TryGetValue(obj.objectInfo.dicKey, out var value) && value == obj)
			{
				return obj.objectInfo.dicKey;
			}
			return -1;
		}

		/// <summary>
		/// Get the ID of this object as used in the currently loaded scene.
		/// If the object was not found in current scene, -1 is returned.
		/// </summary>
		public static int GetSceneId(this ObjectInfo obj)
		{
			if (obj == null)
			{
				throw new ArgumentNullException("obj");
			}
			if (Studio == null)
			{
				throw new InvalidOperationException("Studio is not initialized yet!");
			}
			if (Studio.dicObjectCtrl.TryGetValue(obj.dicKey, out var value) && value.objectInfo == obj)
			{
				return obj.dicKey;
			}
			return -1;
		}

		/// <summary>
		/// Get GetOCIChar that is assigned to this character. Only works in CharaStudio, returns null elsewhere.
		/// </summary>
		public static OCIChar GetOCIChar(this ChaControl chaControl)
		{
			if (!StudioAPI.InsideStudio)
			{
				return null;
			}
			Dictionary<TreeNodeObject, ObjectCtrlInfo> dicInfo = Singleton<global::Studio.Studio>.Instance.dicInfo;
			IEnumerable<OCIChar> source = dicInfo.Values.OfType<OCIChar>();
			return source.FirstOrDefault((OCIChar x) => x.charInfo == chaControl);
		}

		/// <summary>
		/// Get character component for this studio object
		/// </summary>
		public static ChaControl GetChaControl(this OCIChar ociChar)
		{
			return ociChar.charInfo;
		}

		/// <summary>
		/// Try to get the ObjectCtrlInfo controlled by this TreeNodeObject.
		/// </summary>
		public static bool TryGetObjectCtrlInfo(this TreeNodeObject tno, out ObjectCtrlInfo objectCtrlInfo)
		{
			return Studio.dicInfo.TryGetValue(tno, out objectCtrlInfo);
		}
	}
}
namespace KKAPI.Studio.UI
{
	/// <summary>
	/// Base class of controls that hold a value. 
	/// Subscribe to <see cref="P:KKAPI.Studio.UI.BaseCurrentStateEditableGuiEntry`1.Value" /> to update your control's state whenever the value changes.
	/// </summary>
	/// <typeparam name="T">Type of the held value</typeparam>
	public abstract class BaseCurrentStateEditableGuiEntry<T> : CurrentStateCategorySubItemBase
	{
		private readonly Func<OCIChar, T> _updateValue;

		/// <summary>
		/// Current value of this control
		/// </summary>
		public BehaviorSubject<T> Value { get; }

		/// <summary>
		/// Create a new control that holds a value
		/// </summary>
		/// <param name="name">Name of the control</param>
		/// <param name="updateValue">Function called every time current character changes and the value needs to be updated</param>
		/// <param name="initialValue">Initial value used before first updateValue call</param>
		protected BaseCurrentStateEditableGuiEntry(string name, Func<OCIChar, T> updateValue, T initialValue = default(T))
			: base(name)
		{
			_updateValue = updateValue ?? throw new ArgumentNullException("updateValue");
			Value = new BehaviorSubject<T>(initialValue);
		}

		/// <inheritdoc />
		protected internal override void OnUpdateInfo(OCIChar ociChar)
		{
			Value.OnNext(_updateValue(ociChar));
		}
	}
	/// <summary>
	/// Category under the Anim &gt; CustomState tab
	/// </summary>
	public class CurrentStateCategory
	{
		private static GameObject _originalText;

		private readonly List<CurrentStateCategorySubItemBase> _subItems;

		private GameObject _categoryContentsObject;

		/// <summary>
		/// Name of the category. Controls are drawn under it.
		/// </summary>
		public string CategoryName { get; }

		/// <summary>
		/// All custom controls under this category.
		/// </summary>
		public IEnumerable<CurrentStateCategorySubItemBase> SubItems => _subItems;

		/// <summary>
		/// The category was created and still exists.
		/// </summary>
		public bool Created => _categoryContentsObject != null;

		/// <summary>
		/// Create a new custom CurrentState category
		/// </summary>
		/// <param name="categoryName">Name of the category</param>
		/// <param name="subItems">Controls under this category</param>
		[Obsolete("Manually creating categories is no longer supported", true)]
		public CurrentStateCategory(string categoryName, IEnumerable<CurrentStateCategorySubItemBase> subItems)
		{
			CategoryName = categoryName;
			_subItems = subItems.ToList();
		}

		/// <summary>
		/// Create a new custom CurrentState category
		/// </summary>
		internal CurrentStateCategory(string categoryName)
		{
			CategoryName = categoryName;
			_subItems = new List<CurrentStateCategorySubItemBase>();
		}

		/// <summary>
		/// Used by the API to actually create the custom control object
		/// </summary>
		protected internal virtual void CreateCategory(GameObject containerObject)
		{
			if (containerObject == null)
			{
				throw new ArgumentNullException("containerObject");
			}
			if (!StudioAPI.StudioLoaded)
			{
				throw new InvalidOperationException("Called before studio was loaded");
			}
			if (!Created)
			{
				if (_originalText == null)
				{
					_originalText = GameObject.Find("StudioScene/Canvas Main Menu/02_Manipulate/00_Chara/01_State/Viewport/Content/Cos/Text (TMP)");
				}
				GameObject gameObject = UnityEngine.Object.Instantiate(_originalText, containerObject.transform, worldPositionStays: true);
				gameObject.AddComponent<LayoutElement>();
				gameObject.name = CategoryName + "_Header_SAPI";
				TextMeshProUGUI component = gameObject.GetComponent<TextMeshProUGUI>();
				component.fontSize = 15f;
				component.fontSizeMax = 15f;
				component.text = CategoryName;
				_categoryContentsObject = new GameObject(CategoryName + "_Items_SAPI");
				_categoryContentsObject.transform.SetParent(containerObject.transform);
				RectTransform rectTransform = _categoryContentsObject.AddComponent<RectTransform>();
				rectTransform.pivot = new Vector2(0f, 0.5f);
				rectTransform.localScale = Vector3.one;
				VerticalLayoutGroup verticalLayoutGroup = _categoryContentsObject.AddComponent<VerticalLayoutGroup>();
				verticalLayoutGroup.padding = new RectOffset(0, 0, 0, 0);
				verticalLayoutGroup.spacing = 4f;
				verticalLayoutGroup.childAlignment = TextAnchor.UpperLeft;
				CreateControls(SubItems);
			}
		}

		/// <summary>
		/// Fired when currently selected character changes and the controls need to be updated
		/// </summary>
		/// <param name="ociChar">Newly selected character</param>
		protected internal virtual void UpdateInfo(OCIChar ociChar)
		{
			foreach (CurrentStateCategorySubItemBase subItem in SubItems)
			{
				subItem.OnUpdateInfo(ociChar);
			}
		}

		/// <summary>
		/// Add new control to this category
		/// </summary>
		public T AddControl<T>(T control) where T : CurrentStateCategorySubItemBase
		{
			_subItems.Add(control);
			if (StudioAPI.StudioLoaded)
			{
				CreateControls(new CurrentStateCategorySubItemBase[1] { control });
			}
			return control;
		}

		/// <summary>
		/// Add new controls to this category
		/// </summary>
		public void AddControls(params CurrentStateCategorySubItemBase[] controls)
		{
			_subItems.AddRange(controls);
			if (StudioAPI.StudioLoaded)
			{
				CreateControls(controls);
			}
		}

		private void CreateControls(IEnumerable<CurrentStateCategorySubItemBase> controls)
		{
			foreach (CurrentStateCategorySubItemBase control in controls)
			{
				control.CreateItemInt(_categoryContentsObject);
			}
		}
	}
	/// <summary>
	/// Custom control that draws a color picker in the Chara &gt; CurrentState studio menu.
	/// </summary>
	public class CurrentStateCategoryColorPicker : BaseCurrentStateEditableGuiEntry<Color>
	{
		private static GameObject _originalObject;

		private const float LineSpacing = 0.5f;

		private readonly Action<Color> OnValueChanged;

		public CurrentStateCategoryColorPicker(string name, Func<OCIChar, Color> updateValue, Action<Color> onValueChanged)
			: base(name, updateValue, default(Color))
		{
			OnValueChanged = onValueChanged;
		}

		/// <inheritdoc />
		protected override GameObject CreateItem(GameObject categoryObject)
		{
			if (_originalObject == null)
			{
				_originalObject = GameObject.Find("StudioScene/Canvas Main Menu/02_Manipulate/00_Chara/01_State/Viewport/Content/Etc/Color");
			}
			GameObject gameObject = UnityEngine.Object.Instantiate(_originalObject, categoryObject.transform, worldPositionStays: true);
			gameObject.gameObject.SetActive(value: true);
			gameObject.name = "CustomColor " + base.Name;
			gameObject.transform.localScale = Vector3.one;
			Text componentInChildren = gameObject.GetComponentInChildren<Text>(includeInactive: true);
			componentInChildren.lineSpacing = 0.5f;
			componentInChildren.gameObject.SetActive(value: true);
			componentInChildren.gameObject.name = "Text " + base.Name;
			componentInChildren.text = base.Name;
			Button componentInChildren2 = gameObject.GetComponentInChildren<Button>(includeInactive: true);
			componentInChildren2.gameObject.SetActive(value: true);
			Image image = componentInChildren2.GetComponentInChildren<Image>(includeInactive: true);
			image.gameObject.SetActive(value: true);
			image.color = base.Value.Value;
			componentInChildren2.onClick.ActuallyRemoveAllListeners();
			componentInChildren2.onClick.AddListener(delegate
			{
				base.Value.OnNext(base.Value.Value);
			});
			componentInChildren2.onClick.AddListener(delegate
			{
				Singleton<global::Studio.Studio>.Instance.colorPalette.Setup(base.Name, base.Value.Value, delegate(Color c)
				{
					OnValueChanged(c);
					image.color = c;
				}, _useAlpha: true);
				Singleton<global::Studio.Studio>.Instance.colorPalette.visible = true;
			});
			base.Value.Subscribe(delegate(Color newValue)
			{
				image.color = newValue;
			});
			return gameObject;
		}
	}
	/// <summary>
	/// Custom control that draws a dropdown menu in the Chara &gt; CurrentState studio menu.
	/// </summary>
	public class CurrentStateCategoryDropdown : BaseCurrentStateEditableGuiEntry<int>
	{
		private readonly string[] _items;

		private static GameObject _originalObject;

		private const string ObjectSourcePath = "StudioScene/Canvas Main Menu/02_Manipulate/00_Chara/02_Kinematic/05_Etc/Eyes Draw";

		/// <summary>
		/// A dropdown for the Chara &gt; CurrentState studio menu.
		/// </summary>
		/// <param name="name">Name of the button, shown on left.</param>
		/// <param name="items">Items shown in the dropdown box. Value is the currently selected index.</param>
		/// <param name="updateValue">Function called when the current character changes and the on/off state needs to be updated.
		/// <code>OCIChar</code> is the newly selected character. Return the new state. Can't be null.</param>
		public CurrentStateCategoryDropdown(string name, string[] items, Func<OCIChar, int> updateValue)
			: base(name, updateValue, 0)
		{
			_items = items;
		}

		/// <inheritdoc />
		protected override GameObject CreateItem(GameObject categoryObject)
		{
			if (_originalObject == null)
			{
				_originalObject = GameObject.Find("StudioScene/Canvas Main Menu/02_Manipulate/00_Chara/02_Kinematic/05_Etc/Eyes Draw");
				foreach (Transform item in from Transform t in _originalObject.transform
					where t.name.StartsWith("Toggle")
					select t)
				{
					UnityEngine.Object.DestroyImmediate(item.gameObject);
				}
			}
			GameObject gameObject = UnityEngine.Object.Instantiate(_originalObject, categoryObject.transform, worldPositionStays: true);
			gameObject.gameObject.SetActive(value: true);
			gameObject.transform.localScale = Vector3.one;
			gameObject.name = "CustomDropdown " + base.Name;
			LayoutElement layoutElement = gameObject.AddComponent<LayoutElement>();
			layoutElement.preferredHeight = 20f;
			layoutElement.preferredWidth = 210f;
			TextMeshProUGUI componentInChildren = gameObject.transform.Find("TextMeshPro").GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
			componentInChildren.lineSpacing = -20f;
			componentInChildren.gameObject.SetActive(value: true);
			componentInChildren.text = base.Name;
			RectTransform rectTransform = componentInChildren.rectTransform;
			rectTransform.offsetMin = new Vector2(0f, -20f);
			rectTransform.offsetMax = new Vector2(100f, 0f);
			Dropdown dropdown = gameObject.GetComponentInChildren<Dropdown>(includeInactive: true);
			dropdown.gameObject.SetActive(value: true);
			RectTransform component = dropdown.GetComponent<RectTransform>();
			component.offsetMin = new Vector2(100f, -20f);
			component.offsetMax = new Vector2(210f, 0f);
			dropdown.ClearOptions();
			dropdown.AddOptions(_items.ToList());
			dropdown.onValueChanged.ActuallyRemoveAllListeners();
			dropdown.onValueChanged.AddListener(base.Value.OnNext);
			base.Value.Subscribe(delegate(int newSet)
			{
				dropdown.value = newSet;
			});
			return gameObject;
		}
	}
	/// <summary>
	/// Custom control that draws a slider in the Chara &gt; CurrentState studio menu.
	/// </summary>
	public class CurrentStateCategorySlider : BaseCurrentStateEditableGuiEntry<float>
	{
		private static GameObject _originalObject;

		private const float LineSpacing = 0.5f;

		/// <summary>
		/// Minimum value of the slider
		/// </summary>
		public float MinValue { get; }

		/// <summary>
		/// Maximum value of the slider
		/// </summary>
		public float MaxValue { get; }

		/// <summary>
		/// Custom control that draws a slider in the Chara &gt; CurrentState studio menu.
		/// </summary>
		/// <param name="name">Name of the button, shown on left</param> 
		/// <param name="updateValue">Function called when the current character changes and the slider value needs to be updated.
		/// <code>OCIChar</code> is the newly selected character. Return the new state. Can't be null.</param>
		/// <param name="minValue">Minimum value of the slider</param>
		/// <param name="maxValue">Maximum value of the slider</param>
		public CurrentStateCategorySlider(string name, Func<OCIChar, float> updateValue, float minValue = 0f, float maxValue = 1f)
			: base(name, updateValue, 0f)
		{
			if (minValue >= maxValue)
			{
				throw new ArgumentException("minValue has to be smaller than maxValue");
			}
			MinValue = minValue;
			MaxValue = maxValue;
		}

		/// <inheritdoc />
		protected override GameObject CreateItem(GameObject categoryObject)
		{
			if (_originalObject == null)
			{
				_originalObject = GameObject.Find("StudioScene/Canvas Main Menu/02_Manipulate/00_Chara/01_State/Viewport/Content/Etc/Cheek");
			}
			GameObject gameObject = UnityEngine.Object.Instantiate(_originalObject, categoryObject.transform, worldPositionStays: true);
			gameObject.gameObject.SetActive(value: true);
			gameObject.name = "CustomSlider " + base.Name;
			gameObject.transform.localScale = Vector3.one;
			Text componentInChildren = gameObject.GetComponentInChildren<Text>(includeInactive: true);
			componentInChildren.lineSpacing = 0.5f;
			componentInChildren.gameObject.SetActive(value: true);
			componentInChildren.gameObject.name = "Text " + base.Name;
			componentInChildren.text = base.Name;
			RectTransform rectTransform = componentInChildren.rectTransform;
			rectTransform.offsetMin = new Vector2(0f, -20f);
			rectTransform.offsetMax = new Vector2(100f, 0f);
			Slider slider = gameObject.GetComponentInChildren<Slider>(includeInactive: true);
			slider.gameObject.SetActive(value: true);
			slider.gameObject.name = "Slider " + base.Name;
			slider.minValue = MinValue;
			slider.maxValue = MaxValue;
			slider.value = base.Value.Value;
			slider.onValueChanged.RemoveAllListeners();
			slider.onValueChanged.AddListener(base.Value.OnNext);
			base.Value.Subscribe(delegate(float newValue)
			{
				slider.value = newValue;
			});
			return gameObject;
		}
	}
	/// <summary>
	/// Base of custom controls created under CurrentState category
	/// </summary>
	public abstract class CurrentStateCategorySubItemBase
	{
		/// <summary>
		/// Name of the setting, displayed to the left
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// The control's root gameobject. null if the control was not created yet.
		/// </summary>
		public GameObject RootGameObject { get; private set; }

		/// <summary>
		/// The control was created and still exists.
		/// </summary>
		public bool Created => RootGameObject != null;

		/// <summary>
		/// The control is visible to the user (usually the same as it's GameObject being active).
		/// </summary>
		public BehaviorSubject<bool> Visible { get; }

		/// <summary>
		/// Create a new custom CurrentState control
		/// </summary>
		/// <param name="name">Name of the setting displayed on the left</param>
		protected CurrentStateCategorySubItemBase(string name)
		{
			Name = name ?? throw new ArgumentNullException("name");
			Visible = new BehaviorSubject<bool>(defaultValue: true);
			Visible.Subscribe(delegate(bool b)
			{
				if (RootGameObject != null)
				{
					RootGameObject.SetActive(b);
				}
			});
		}

		/// <summary>
		/// Fired when API wants to create the control. Should return the control's root GameObject
		/// </summary>
		/// <param name="categoryObject">Parent object of the control to be created</param>
		protected abstract GameObject CreateItem(GameObject categoryObject);

		internal void CreateItemInt(GameObject categoryObject)
		{
			if (categoryObject == null)
			{
				throw new ArgumentNullException("categoryObject");
			}
			if (!StudioAPI.StudioLoaded)
			{
				throw new InvalidOperationException("Called before studio was loaded");
			}
			if (!Created)
			{
				GameObject gameObject = CreateItem(categoryObject);
				if (gameObject == null)
				{
					throw new ArgumentException("CreateItem has to return a GameObject, check its overload in " + GetType().FullName);
				}
				RootGameObject = gameObject;
				gameObject.SetActive(Visible.Value);
			}
		}

		/// <summary>
		/// Fired when currently selected character changes and the control need to be updated
		/// </summary>
		/// <param name="ociChar">Newly selected character</param>
		protected internal abstract void OnUpdateInfo(OCIChar ociChar);
	}
	/// <summary>
	/// Custom control that draws from 2 to 4 radio buttons (they are drawn like toggles)
	/// </summary>
	public class CurrentStateCategoryToggle : BaseCurrentStateEditableGuiEntry<int>
	{
		private static GameObject _originalToggle;

		private const float LineSpacing = 0.5f;

		/// <summary>
		/// Number of the radio buttons, can be 2, 3 or 4
		/// </summary>
		public int ToggleCount { get; }

		/// <summary>
		/// Currently selected button (starts at 0)
		/// </summary>
		[Obsolete("Use Value instead")]
		public BehaviorSubject<int> SelectedIndex => base.Value;

		/// <summary>
		/// A toggle list for the Chara &gt; CurrentState studio menu.
		/// </summary>
		/// <param name="name">Name of the list, shown on left</param>
		/// <param name="toggleCount">Number of the toggles, can be 2, 3 or 4</param>
		/// <param name="onUpdateSelection">Function called when the current character changes and the selected index needs to be updated.
		/// <code>OCIChar</code> is the newly selected character. Return the new selected index. Can't be null.</param>
		public CurrentStateCategoryToggle(string name, int toggleCount, Func<OCIChar, int> onUpdateSelection)
			: base(name, onUpdateSelection, 0)
		{
			if (toggleCount > 4 || toggleCount < 2)
			{
				throw new ArgumentException("Need to set 2 to 4 toggle buttons", "toggleCount");
			}
			ToggleCount = toggleCount;
		}

		/// <inheritdoc />
		protected override GameObject CreateItem(GameObject categoryObject)
		{
			if (_originalToggle == null)
			{
				_originalToggle = GameObject.Find("StudioScene/Canvas Main Menu/02_Manipulate/00_Chara/01_State/Viewport/Content/Etc/Tears");
			}
			GameObject gameObject = UnityEngine.Object.Instantiate(_originalToggle, categoryObject.transform, worldPositionStays: true);
			gameObject.name = "CustomToggle-" + base.Name;
			gameObject.transform.localScale = Vector3.one;
			List<Transform> list = gameObject.transform.Children();
			foreach (Transform item in list.Skip(1))
			{
				UnityEngine.Object.Destroy(item.gameObject);
			}
			Transform transform = list[0];
			Text component = transform.GetComponent<Text>();
			component.lineSpacing = 0.5f;
			transform.name = "Text " + base.Name;
			component.text = base.Name;
			RectTransform rectTransform = component.rectTransform;
			rectTransform.offsetMin = new Vector2(0f, -20f);
			rectTransform.offsetMax = new Vector2(100f, 0f);
			List<Button> copiedToggles = new List<Button>(ToggleCount);
			GameObject gameObject2 = GameObject.Find("StudioScene/Canvas Main Menu/02_Manipulate/00_Chara/01_State/Viewport/Content/Liquid");
			List<Transform> list2 = gameObject2.transform.Children().Skip(1).Take(3)
				.ToList();
			foreach (Transform item2 in list2)
			{
				if (ToggleCount <= copiedToggles.Count)
				{
					break;
				}
				Transform transform2 = UnityEngine.Object.Instantiate(item2, gameObject.transform, worldPositionStays: true);
				Button component2 = transform2.GetComponent<Button>();
				component2.onClick.ActuallyRemoveAllListeners();
				copiedToggles.Add(component2);
				if (copiedToggles.Count == 3 && ToggleCount == 4)
				{
					transform2 = UnityEngine.Object.Instantiate(item2, gameObject.transform, worldPositionStays: true);
					component2 = transform2.GetComponent<Button>();
					component2.onClick.ActuallyRemoveAllListeners();
					copiedToggles.Add(component2);
					transform2.localPosition += copiedToggles[1].transform.localPosition - copiedToggles[0].transform.localPosition;
				}
			}
			for (int i = 0; i < copiedToggles.Count; i++)
			{
				Button button = copiedToggles[i];
				int buttonIndex = i;
				button.transform.name = $"Button {base.Name} {buttonIndex}";
				button.onClick.AddListener(delegate
				{
					base.Value.OnNext(buttonIndex);
				});
				button.transform.localPosition = new Vector3(button.transform.localPosition.x, 0f, 0f);
				button.transform.localScale = Vector3.one;
				button.gameObject.SetActive(value: true);
			}
			base.Value.Subscribe(delegate(int newval)
			{
				for (int j = 0; j < copiedToggles.Count; j++)
				{
					Button button2 = copiedToggles[j];
					if (!(button2 == null))
					{
						button2.image.color = ((!button2.interactable || j != newval) ? Color.white : Color.green);
					}
				}
			});
			return gameObject;
		}
	}
	/// <summary>
	/// Custom control that draws a single, circular button with an on/off state in the Chara &gt; CurrentState studio menu.
	/// </summary>
	public class CurrentStateCategorySwitch : BaseCurrentStateEditableGuiEntry<bool>
	{
		private static GameObject _originalObject;

		private const float LineSpacing = 0.5f;

		/// <summary>
		/// A single button for the Chara &gt; CurrentState studio menu.
		/// </summary>
		/// <param name="name">Name of the button, shown on left</param> 
		/// <param name="updateValue">Function called when the current character changes and the on/off state needs to be updated.
		/// <code>OCIChar</code> is the newly selected character. Return the new state. Can't be null.</param>
		public CurrentStateCategorySwitch(string name, Func<OCIChar, bool> updateValue)
			: base(name, updateValue, initialValue: false)
		{
		}

		/// <inheritdoc />
		protected override GameObject CreateItem(GameObject categoryObject)
		{
			if (_originalObject == null)
			{
				_originalObject = GameObject.Find("StudioScene/Canvas Main Menu/02_Manipulate/00_Chara/01_State/Viewport/Content/Etc/Son");
			}
			GameObject gameObject = UnityEngine.Object.Instantiate(_originalObject, categoryObject.transform, worldPositionStays: true);
			gameObject.gameObject.SetActive(value: true);
			gameObject.transform.localScale = Vector3.one;
			gameObject.name = "CustomSwitch " + base.Name;
			Text componentInChildren = gameObject.GetComponentInChildren<Text>(includeInactive: true);
			componentInChildren.lineSpacing = 0.5f;
			componentInChildren.gameObject.SetActive(value: true);
			componentInChildren.gameObject.name = "Text " + base.Name;
			componentInChildren.text = base.Name;
			RectTransform rectTransform = componentInChildren.rectTransform;
			rectTransform.offsetMin = new Vector2(0f, -20f);
			rectTransform.offsetMax = new Vector2(100f, 0f);
			Toggle toggle = gameObject.GetComponentInChildren<Toggle>(includeInactive: true);
			toggle.gameObject.SetActive(value: true);
			toggle.gameObject.name = "Button " + base.Name;
			toggle.isOn = base.Value.Value;
			toggle.onValueChanged.RemoveAllListeners();
			toggle.onValueChanged.AddListener(base.Value.OnNext);
			base.Value.Subscribe(delegate(bool newSet)
			{
				toggle.isOn = newSet;
			});
			return gameObject;
		}
	}
	/// <summary>
	/// A container for the value of a ColorPicker, associated label and button, and the setter method that triggers on value change.
	/// </summary>
	public class SceneEffectsColorPickerSet
	{
		private Color CurrentValue;

		private bool EventsEnabled = true;

		private string _text;

		/// <summary>
		/// Label UI element.
		/// </summary>
		public TextMeshProUGUI Label { get; set; }

		/// <summary>
		/// Button UI element.
		/// </summary>
		public Button Button { get; set; }

		/// <summary>
		/// Color image, actual click functionality handled by button.
		/// </summary>
		public Image ColorImage { get; set; }

		/// <summary>
		/// Method called when the value of the Color is changed.
		/// </summary>
		public Action<Color> Setter { get; set; }

		/// <summary>
		/// Get or set the text of the label.
		/// </summary>
		public string Text
		{
			get
			{
				return _text;
			}
			set
			{
				_text = value;
				Label.text = value;
			}
		}

		/// <summary>
		/// Get or set the value of the ColorPicker.
		/// </summary>
		public Color Value
		{
			get
			{
				return GetValue();
			}
			set
			{
				SetValue(value);
			}
		}

		/// <summary>
		/// Create a new ColorPicker. Typically, you want to use SceneEffectsCategory.AddColorPicker instead of creating these manually.
		/// </summary>
		/// <param name="label">Label UI element</param>
		/// <param name="button">Button component</param>
		/// <param name="text">Label text</param>
		/// <param name="setter">Method that will be called on value change</param>
		/// <param name="initialValue">The initial value to show on the ColorPicker.</param>
		public SceneEffectsColorPickerSet(TextMeshProUGUI label, Button button, string text, Action<Color> setter, Color initialValue)
		{
			Label = label;
			Button = button;
			ColorImage = button.GetComponent<Image>();
			Setter = setter;
			Text = text;
			ColorImage.color = initialValue;
			CurrentValue = initialValue;
			button.onClick.RemoveAllListeners();
			button.onClick.AddListener(delegate
			{
				Singleton<global::Studio.Studio>.Instance.colorPalette.Setup(Text, Value, SetValue, _useAlpha: true);
				Singleton<global::Studio.Studio>.Instance.colorPalette.visible = true;
			});
			Label.gameObject.name = "Label " + Text;
			Button.gameObject.name = "ColorPicker " + Text;
		}

		/// <summary>
		/// Get the value of the ColorPicker.
		/// </summary>
		/// <returns>Value of the ColorPicker</returns>
		public Color GetValue()
		{
			return CurrentValue;
		}

		/// <summary>
		/// Set the value of the ColorPicker and trigger the Setter method.
		/// </summary>
		/// <param name="value">Value to set the ColorPicker</param>
		public void SetValue(Color value)
		{
			SetValue(value, triggerEvents: true);
		}

		/// <summary>
		/// Set the value of the
		/// </summary>
		/// <param name="value">Value to set the ColorPicker</param>
		/// <param name="triggerEvents">Whether to trigger the Setter method</param>
		public void SetValue(Color value, bool triggerEvents)
		{
			EventsEnabled = false;
			CurrentValue = value;
			ColorImage.color = value;
			EventsEnabled = true;
			if (triggerEvents)
			{
				Setter(value);
			}
		}
	}
	/// <summary>
	/// A container for the value of a ColorPicker, associated label and button, and the setter method that triggers on value change.
	/// </summary>
	public class SceneEffectsLabelSet
	{
		/// <summary>
		/// Label UI element.
		/// </summary>
		public TextMeshProUGUI Label { get; set; }

		/// <summary>
		/// Get or set the text of the label.
		/// </summary>
		public string Text
		{
			get
			{
				return Label.text;
			}
			set
			{
				Label.text = value;
			}
		}

		/// <summary>
		/// Create a new Label. Typically, you want to use SceneEffectsCategory.AddLabel instead of creating these manually.
		/// </summary>
		/// <param name="label">Label UI element</param>
		/// <param name="text">Label text</param>
		public SceneEffectsLabelSet(TextMeshProUGUI label, string text)
		{
			Label = label;
			Text = text;
			Label.gameObject.name = "Label " + Text;
		}
	}
	/// <summary>
	/// A container for the value of a toggle, associated label and toggle UI elements, and the setter method that triggers on value change.
	/// </summary>
	public class SceneEffectsToggleSet
	{
		private bool CurrentValue;

		private bool EventsEnabled = true;

		private string _text;

		/// <summary>
		/// Label UI element.
		/// </summary>
		public TextMeshProUGUI Label { get; set; }

		/// <summary>
		/// Toggle UI element.
		/// </summary>
		public Toggle Toggle { get; set; }

		/// <summary>
		/// Method called when the value of the toggle is changed.
		/// </summary>
		public Action<bool> Setter { get; set; }

		/// <summary>
		/// Initial state of the toggle.
		/// </summary>
		public bool InitialValue { get; set; }

		/// <summary>
		/// Get or set the text of the label.
		/// </summary>
		public string Text
		{
			get
			{
				return _text;
			}
			set
			{
				_text = value;
				Label.text = value;
			}
		}

		/// <summary>
		/// Get or set the value of the toggle.
		/// </summary>
		public bool Value
		{
			get
			{
				return GetValue();
			}
			set
			{
				SetValue(value);
			}
		}

		/// <summary>
		/// Create a new ToggleSet. Typically you want to use SceneEffectsCategory.AddToggleSet instead of creating these manually.
		/// </summary>
		/// <param name="label">Label UI element</param>
		/// <param name="toggle">Toggle UI element</param>
		/// <param name="text">Label text</param>
		/// <param name="setter">Method that will be called on value change</param>
		/// <param name="initialValue">Initial state of the toggle</param>
		public SceneEffectsToggleSet(TextMeshProUGUI label, Toggle toggle, string text, Action<bool> setter, bool initialValue)
		{
			Label = label;
			Toggle = toggle;
			Setter = setter;
			InitialValue = initialValue;
			Value = InitialValue;
			Text = text;
			Toggle.isOn = Value;
			Toggle.onValueChanged.RemoveAllListeners();
			Toggle.onValueChanged.AddListener(delegate(bool value)
			{
				if (EventsEnabled)
				{
					Value = value;
				}
			});
			Label.gameObject.name = "Label " + Text;
			Toggle.gameObject.name = "Toggle " + Text;
		}

		/// <summary>
		/// Get the value of the toggle.
		/// </summary>
		/// <returns>Value of the toggle</returns>
		public bool GetValue()
		{
			return CurrentValue;
		}

		/// <summary>
		/// Set the value of the toggle and trigger the Setter method.
		/// </summary>
		/// <param name="value">Value to set the toggle</param>
		public void SetValue(bool value)
		{
			SetValue(value, triggerEvents: true);
		}

		/// <summary>
		/// Set the value of the toggle.
		/// </summary>
		/// <param name="value">Value to set the toggle</param>
		/// <param name="triggerEvents">Whether to trigger the Setter method</param>
		public void SetValue(bool value, bool triggerEvents)
		{
			EventsEnabled = false;
			CurrentValue = value;
			Toggle.isOn = value;
			EventsEnabled = true;
			if (triggerEvents)
			{
				Setter(value);
			}
		}

		/// <summary>
		/// Reset the toggle to the initial value and trigger the Setter method.
		/// </summary>
		public void Reset()
		{
			Reset(triggerEvents: true);
		}

		/// <summary>
		/// Reset the toggle to the initial value.
		/// </summary>
		/// <param name="triggerEvents">Whether to trigger the Setter method</param>
		public void Reset(bool triggerEvents)
		{
			EventsEnabled = false;
			CurrentValue = InitialValue;
			Toggle.isOn = InitialValue;
			EventsEnabled = true;
			if (triggerEvents)
			{
				Setter(InitialValue);
			}
		}
	}
	/// <summary>
	/// Add custom buttons to studio toolbars
	/// </summary>
	public static class CustomToolbarButtons
	{
		private static readonly List<BaseGuiEntry> _controlsToSpawn = new List<BaseGuiEntry>();

		private static bool _studioLoaded;

		/// <summary>
		/// Add a toggle button to the top of the left studio toolbar.
		/// Clicking on the button will toggle it between being on and off.
		/// </summary>
		/// <param name="iconTex">
		/// A 32x32 icon used for the button.
		/// You can find a template here
		/// https://github.com/IllusionMods/IllusionModdingAPI/blob/master/doc/studio%20icon%20template.png
		/// For best performance and smallest size save your thumbnail as 8bit grayscale png (or indexed if you need colors) with no alpha channel.
		/// </param>
		/// <param name="initialValue">Initial state of the toggle.</param>
		/// <param name="onValueChanged">
		/// Action fired each time user clicks on the toggle button.
		/// The value is true if the toggle is enabled by the click, false if disabled.
		/// </param>
		public static ToolbarToggle AddLeftToolbarToggle(Texture2D iconTex, bool initialValue = false, Action<bool> onValueChanged = null)
		{
			if (iconTex == null)
			{
				throw new ArgumentNullException("iconTex");
			}
			if (iconTex.width != 32 || iconTex.height != 32)
			{
				KoikatuAPI.Logger.LogWarning((object)$"Icon texture passed to AddLeftToolbarToggle has wrong size, it should be 32x32 but is {iconTex.width}x{iconTex.height}");
			}
			if (!StudioAPI.InsideStudio)
			{
				KoikatuAPI.Logger.LogDebug((object)"Tried to run StudioAPI.AddLeftToolbarToggle outside of studio!");
				return null;
			}
			ToolbarToggle toolbarToggle = new ToolbarToggle(iconTex, initialValue);
			if (_studioLoaded)
			{
				toolbarToggle.CreateControl(null);
			}
			else
			{
				_controlsToSpawn.Add(toolbarToggle);
			}
			if (onValueChanged != null)
			{
				toolbarToggle.ValueChanged.Subscribe(onValueChanged);
			}
			return toolbarToggle;
		}

		/// <summary>
		/// Add a simple button to the top of the left studio toolbar.
		/// </summary>
		/// ///
		/// <param name="iconTex">
		/// A 32x32 icon used for the button.
		/// You can find a template here
		/// https://github.com/ManlyMarco/QuickAccessBox/blob/master/Shared_QuickAccessBox/UI/toolbar-icon.png
		/// </param>
		/// <param name="onClicked">Action fired each time user clicks on the button</param>
		public static ToolbarButton AddLeftToolbarButton(Texture2D iconTex, Action onClicked = null)
		{
			if (iconTex == null)
			{
				throw new ArgumentNullException("iconTex");
			}
			if (iconTex.width != 32 || iconTex.height != 32)
			{
				KoikatuAPI.Logger.LogWarning((object)$"Icon texture passed to AddLeftToolbarButton has wrong size, it should be 32x32 but is {iconTex.width}x{iconTex.height}");
			}
			if (!StudioAPI.InsideStudio)
			{
				KoikatuAPI.Logger.LogDebug((object)"Tried to run StudioAPI.AddLeftToolbarButton outside of studio!");
				return null;
			}
			ToolbarButton toolbarButton = new ToolbarButton(iconTex);
			if (_studioLoaded)
			{
				toolbarButton.CreateControl(null);
			}
			else
			{
				_controlsToSpawn.Add(toolbarButton);
			}
			if (onClicked != null)
			{
				toolbarButton.Clicked.Subscribe(delegate
				{
					onClicked();
				});
			}
			return toolbarButton;
		}

		internal static void OnStudioLoaded()
		{
			_studioLoaded = true;
			foreach (BaseGuiEntry item in _controlsToSpawn)
			{
				item.CreateControl(null);
			}
		}
	}
	/// <summary>
	/// Class that adds a new subcategory to the Scene Effects menu. Create a new instance and then add SliderSets, ToggleSets, DropdownSets, ColorPickerSets, or just plain LabelSets.
	/// </summary>
	public class SceneEffectsCategory
	{
		private const float OffsetMultiplier = -25f;

		private static bool _wasInitialized;

		private static GameObject _headerSource;

		private static GameObject _contentSource;

		private static GameObject _labelSource;

		private static GameObject _toggleSource;

		private static GameObject _sliderSource;

		private static GameObject _dropdownSource;

		private static GameObject _colorPickerSource;

		private static GameObject _inputSource;

		private static GameObject _buttonSource;

		private static Sprite _expandSprite;

		private static Sprite _collapseSprite;

		/// <summary>
		/// Labels that have been added.
		/// </summary>
		public List<SceneEffectsLabelSet> Labels = new List<SceneEffectsLabelSet>();

		/// <summary>
		/// Toggles that have been added.
		/// </summary>
		public List<SceneEffectsToggleSet> Toggles = new List<SceneEffectsToggleSet>();

		/// <summary>
		/// Sliders that have been added.
		/// </summary>
		public List<SceneEffectsSliderSet> Sliders = new List<SceneEffectsSliderSet>();

		/// <summary>
		/// Dropdowns that have been added.
		/// </summary>
		public List<SceneEffectsDropdownSet> Dropdowns = new List<SceneEffectsDropdownSet>();

		/// <summary>
		/// Color pickers that have been added.
		/// </summary>
		public List<SceneEffectsColorPickerSet> ColorPickers = new List<SceneEffectsColorPickerSet>();

		/// <summary>
		/// Element that contains the header of the category.
		/// </summary>
		public GameObject Header { get; set; }

		/// <summary>
		/// Element that contains the content of the category.
		/// </summary>
		public GameObject Content { get; set; }

		private static void Initialize()
		{
			if (!_wasInitialized)
			{
				SystemButtonCtrl systemButtonCtrl = Singleton<global::Studio.Studio>.Instance.systemButtonCtrl;
				GameObject gameObject = systemButtonCtrl.transform.FindLoop("01_Screen Effect");
				_headerSource = gameObject.transform.Find("Screen Effect/Viewport/Content/Image Depth of Field")?.gameObject ?? throw new ArgumentException("Could not find Screen Effect/Viewport/Content/Image Depth of Field");
				_contentSource = gameObject.transform.Find("Screen Effect/Viewport/Content/Depth of Field")?.gameObject ?? throw new ArgumentException("Could not find Screen Effect/Viewport/Content/Depth of Field");
				_labelSource = gameObject.transform.Find("Screen Effect/Viewport/Content/Depth of Field/TextMeshPro Draw")?.gameObject ?? throw new ArgumentException("Could not find Screen Effect/Viewport/Content/Depth of Field/TextMeshPro Draw");
				_toggleSource = gameObject.transform.Find("Screen Effect/Viewport/Content/Depth of Field/Toggle Draw")?.gameObject ?? throw new ArgumentException("Could not find Screen Effect/Viewport/Content/Depth of Field/Toggle Draw");
				_sliderSource = gameObject.transform.Find("Screen Effect/Viewport/Content/Depth of Field/Slider Focal Size")?.gameObject ?? throw new ArgumentException("Could not find Screen Effect/Viewport/Content/Depth of Field/Slider Focal Size");
				_inputSource = gameObject.transform.Find("Screen Effect/Viewport/Content/Depth of Field/InputField Focal Size")?.gameObject ?? throw new ArgumentException("Could not find Screen Effect/Viewport/Content/Depth of Field/InputField Focal Size");
				_buttonSource = gameObject.transform.Find("Screen Effect/Viewport/Content/Depth of Field/Button Focal Size Default")?.gameObject ?? throw new ArgumentException("Could not find Screen Effect/Viewport/Content/Depth of Field/Button Focal Size Default");
				_dropdownSource = gameObject.transform.Find("Screen Effect/Viewport/Content/Amplify Color Effect/Dropdown Lut")?.gameObject ?? throw new ArgumentException("Could not find Screen Effect/Viewport/Content/Amplify Color Effect/Dropdown Lut");
				_colorPickerSource = gameObject.transform.Find("Screen Effect/Viewport/Content/Amplify Occlusion Effect/Button Color")?.gameObject ?? throw new ArgumentException("Could not find Screen Effect/Viewport/Content/Amplify Occlusion Effect/Button Color");
				Sprite[] source = Resources.FindObjectsOfTypeAll<Sprite>();
				_expandSprite = source.FirstOrDefault((Sprite r) => r.name.Equals("sp_sn_09_00_05"));
				_collapseSprite = source.FirstOrDefault((Sprite r) => r.name.Equals("sp_sn_09_00_03"));
				_wasInitialized = true;
			}
		}

		/// <summary>
		/// Create a new Screen Effects subcategory.
		/// </summary>
		/// <param name="labelText">Text that will appear on the header of the category</param>
		public SceneEffectsCategory(string labelText)
		{
			SceneEffectsCategory sceneEffectsCategory = this;
			Initialize();
			Header = UnityEngine.Object.Instantiate(_headerSource);
			Header.name = "Image " + labelText;
			Header.transform.SetParent(_headerSource.transform.parent, worldPositionStays: false);
			TextMeshProUGUI componentInChildren = Header.GetComponentInChildren<TextMeshProUGUI>();
			componentInChildren.text = labelText;
			Content = UnityEngine.Object.Instantiate(_contentSource);
			Content.name = labelText;
			Content.transform.SetParent(_contentSource.transform.parent, worldPositionStays: false);
			VerticalLayoutGroup component = Content.GetComponent<VerticalLayoutGroup>();
			UnityEngine.Object.DestroyImmediate(component);
			LayoutElement component2 = Content.GetComponent<LayoutElement>();
			component2.preferredHeight = 0f;
			component2.preferredWidth = 375f;
			Button button = Header.GetComponentInChildren<Button>();
			button.onClick.AddListener(delegate
			{
				sceneEffectsCategory.Content.SetActive(!sceneEffectsCategory.Content.activeSelf);
				button.image.sprite = (sceneEffectsCategory.Content.activeSelf ? _collapseSprite : _expandSprite);
			});
			foreach (Transform item in Content.transform)
			{
				UnityEngine.Object.Destroy(item.gameObject);
			}
		}

		/// <summary>
		/// Add a label to the category, can be used for sectioning.
		/// </summary>
		/// <param name="text">Label text</param>
		/// <returns>Instance of the LabelSet</returns>
		public SceneEffectsLabelSet AddLabelSet(string text)
		{
			RectTransform rectTransform = new GameObject().AddComponent<RectTransform>();
			rectTransform.name = text;
			rectTransform.SetParent(Content.transform, worldPositionStays: false);
			((MonoBehaviour)(object)KoikatuAPI.Instance).StartCoroutine(SetPosDelayed(rectTransform.transform, GetCurrentOffset()));
			TextMeshProUGUI component = UnityEngine.Object.Instantiate(_labelSource).GetComponent<TextMeshProUGUI>();
			component.transform.SetParent(rectTransform.transform, worldPositionStays: false);
			component.transform.localPosition = new Vector3(4f, 0f, 0f);
			SceneEffectsLabelSet sceneEffectsLabelSet = new SceneEffectsLabelSet(component, text);
			Labels.Add(sceneEffectsLabelSet);
			CorrectCategoryScale();
			return sceneEffectsLabelSet;
		}

		/// <summary>
		/// Add a toggle to this Screen Effects subcategory.
		/// </summary>
		/// <param name="text">Label text</param>
		/// <param name="setter">Method to be called when the toggle changes value</param>
		/// <param name="initialValue">Initial state of the toggle</param>
		/// <returns>Instance of the ToggleSet</returns>
		public SceneEffectsToggleSet AddToggleSet(string text, Action<bool> setter, bool initialValue)
		{
			RectTransform rectTransform = new GameObject().AddComponent<RectTransform>();
			rectTransform.name = text;
			rectTransform.SetParent(Content.transform, worldPositionStays: false);
			((MonoBehaviour)(object)KoikatuAPI.Instance).StartCoroutine(SetPosDelayed(rectTransform.transform, GetCurrentOffset()));
			TextMeshProUGUI component = UnityEngine.Object.Instantiate(_labelSource).GetComponent<TextMeshProUGUI>();
			component.transform.SetParent(rectTransform.transform, worldPositionStays: false);
			component.transform.localPosition = new Vector3(4f, 0f, 0f);
			Toggle component2 = UnityEngine.Object.Instantiate(_toggleSource).GetComponent<Toggle>();
			component2.transform.SetParent(rectTransform.transform, worldPositionStays: false);
			component2.transform.localPosition = new Vector3(160f, 0f, 0f);
			SceneEffectsToggleSet sceneEffectsToggleSet = new SceneEffectsToggleSet(component, component2, text, setter, initialValue);
			Toggles.Add(sceneEffectsToggleSet);
			CorrectCategoryScale();
			return sceneEffectsToggleSet;
		}

		/// <summary>
		/// Add a slider with text box to this Screen Effects subcategory.
		/// </summary>
		/// <param name="text">Label text</param>
		/// <param name="setter">Method to be called when the slider or text box changes value</param>
		/// <param name="initialValue">Initial value of the slider and text box</param>
		/// <param name="sliderMinimum">Minimum value the slider can slide. Can be overriden by the user typing in to the text box if EnforceSliderMinimum is set to false.</param>
		/// <param name="sliderMaximum">Maximum value the slider can slide. Can be overriden by the user typing in to the text box if EnforceSliderMaximum is set to false.</param>
		/// <returns>Instance of the SliderSet</returns>
		public SceneEffectsSliderSet AddSliderSet(string text, Action<float> setter, float initialValue, float sliderMinimum, float sliderMaximum)
		{
			RectTransform rectTransform = new GameObject().AddComponent<RectTransform>();
			rectTransform.name = text;
			rectTransform.SetParent(Content.transform, worldPositionStays: false);
			((MonoBehaviour)(object)KoikatuAPI.Instance).StartCoroutine(SetPosDelayed(rectTransform.transform, GetCurrentOffset()));
			TextMeshProUGUI component = UnityEngine.Object.Instantiate(_labelSource).GetComponent<TextMeshProUGUI>();
			component.transform.SetParent(rectTransform.transform, worldPositionStays: false);
			component.transform.localPosition = new Vector3(4f, 0f, 0f);
			Slider component2 = UnityEngine.Object.Instantiate(_sliderSource).GetComponent<Slider>();
			component2.transform.SetParent(rectTransform.transform, worldPositionStays: false);
			component2.transform.localPosition = new Vector3(160f, 0f, 0f);
			InputField component3 = UnityEngine.Object.Instantiate(_inputSource).GetComponent<InputField>();
			component3.transform.SetParent(rectTransform.transform, worldPositionStays: false);
			component3.transform.localPosition = new Vector3(295f, -10f, 0f);
			Button component4 = UnityEngine.Object.Instantiate(_buttonSource).GetComponent<Button>();
			component4.transform.SetParent(rectTransform.transform, worldPositionStays: false);
			component4.transform.localPosition = new Vector3(340f, 0f, 0f);
			SceneEffectsSliderSet sceneEffectsSliderSet = new SceneEffectsSliderSet(component, component2, component3, component4, text, setter, initialValue, sliderMinimum, sliderMaximum);
			Sliders.Add(sceneEffectsSliderSet);
			CorrectCategoryScale();
			return sceneEffectsSliderSet;
		}

		/// <summary>
		/// Add a dropdown to this Screen Effects subcategory.
		/// </summary>
		/// <param name="text">Label text</param>
		/// <param name="setter">Method to be called when the dropdown selection changes.</param>
		/// <param name="options">A list of the options to display in the dropdown.</param>
		/// <param name="initialValue">The initial value to be selected in the dropdown.</param>
		/// <returns>Instance of the DropdownSet</returns>
		public SceneEffectsDropdownSet AddDropdownSet(string text, Action<int> setter, List<string> options, string initialValue)
		{
			RectTransform rectTransform = new GameObject().AddComponent<RectTransform>();
			rectTransform.name = text;
			rectTransform.SetParent(Content.transform, worldPositionStays: false);
			((MonoBehaviour)(object)KoikatuAPI.Instance).StartCoroutine(SetPosDelayed(rectTransform.transform, GetCurrentOffset()));
			TextMeshProUGUI component = UnityEngine.Object.Instantiate(_labelSource).GetComponent<TextMeshProUGUI>();
			component.transform.SetParent(rectTransform.transform, worldPositionStays: false);
			component.transform.localPosition = new Vector3(4f, 0f, 0f);
			Dropdown component2 = UnityEngine.Object.Instantiate(_dropdownSource).GetComponent<Dropdown>();
			component2.transform.SetParent(rectTransform.transform, worldPositionStays: false);
			component2.transform.localPosition = new Vector3(160f, 0f, 0f);
			SceneEffectsDropdownSet sceneEffectsDropdownSet = new SceneEffectsDropdownSet(component, component2, text, setter, options, initialValue);
			Dropdowns.Add(sceneEffectsDropdownSet);
			CorrectCategoryScale();
			return sceneEffectsDropdownSet;
		}

		/// <summary>
		/// Add a color picker to this category.
		/// </summary>
		/// <param name="text">The UI label text.</param>
		/// <param name="setter">Function to be called when the color changes.</param>
		/// <param name="initialValue">The initial color to display.</param>
		/// <returns>Instance of the ColorPickerSet</returns>
		public SceneEffectsColorPickerSet AddColorPickerSet(string text, Action<Color> setter, Color initialValue)
		{
			RectTransform rectTransform = new GameObject().AddComponent<RectTransform>();
			rectTransform.name = text;
			rectTransform.SetParent(Content.transform, worldPositionStays: false);
			((MonoBehaviour)(object)KoikatuAPI.Instance).StartCoroutine(SetPosDelayed(rectTransform.transform, GetCurrentOffset()));
			TextMeshProUGUI component = UnityEngine.Object.Instantiate(_labelSource).GetComponent<TextMeshProUGUI>();
			component.transform.SetParent(rectTransform.transform, worldPositionStays: false);
			component.transform.localPosition = new Vector3(4f, 0f, 0f);
			Button component2 = UnityEngine.Object.Instantiate(_colorPickerSource).GetComponent<Button>();
			component2.transform.SetParent(rectTransform.transform, worldPositionStays: false);
			component2.transform.localPosition = new Vector3(160f, 0f, 0f);
			SceneEffectsColorPickerSet sceneEffectsColorPickerSet = new SceneEffectsColorPickerSet(component, component2, text, setter, initialValue);
			ColorPickers.Add(sceneEffectsColorPickerSet);
			CorrectCategoryScale();
			return sceneEffectsColorPickerSet;
		}

		private void CorrectCategoryScale()
		{
			Content.GetComponent<LayoutElement>().preferredHeight += 25f;
		}

		private static IEnumerator SetPosDelayed(Transform tr, float offset)
		{
			while (!tr.gameObject.activeInHierarchy)
			{
				yield return new WaitForSeconds(1f);
			}
			yield return CoroutineUtils.WaitForEndOfFrame;
			Vector3 localPosition = new Vector3(0f, offset, 0f);
			tr.localPosition = localPosition;
		}

		private float GetCurrentOffset()
		{
			return -25f * (float)(Toggles.Count + Sliders.Count + Dropdowns.Count + ColorPickers.Count + Labels.Count);
		}
	}
	/// <summary>
	/// A container for the value of a slider, associated label, slider, textbox, and reset button UI elements, and the setter method that triggers on value change.
	/// </summary>
	public class SceneEffectsSliderSet
	{
		private float _currentValue;

		private bool _eventsEnabled;

		private string _text;

		private float _sliderMinimum;

		private float _sliderMaximum;

		/// <summary>
		/// Show the input field for typing in values.
		/// todo Not actually working?
		/// </summary>
		public bool ShowInput;

		/// <summary>
		/// Show the reset button.
		/// todo Not actually working?
		/// </summary>
		public bool ShowButton;

		/// <summary>
		/// Label UI element.
		/// </summary>
		public TextMeshProUGUI Label { get; private set; }

		/// <summary>
		/// Slider UI element.
		/// </summary>
		public Slider Slider { get; private set; }

		/// <summary>
		/// Input field UI element.
		/// </summary>
		public InputField Input { get; private set; }

		/// <summary>
		/// Reset button UI element.
		/// </summary>
		public Button Button { get; private set; }

		/// <summary>
		/// Method called when the value of the toggle is changed.
		/// </summary>
		public Action<float> Setter { get; set; }

		/// <summary>
		/// Initial state of the toggle.
		/// </summary>
		public float InitialValue { get; set; }

		/// <summary>
		/// Minimum value the slider can slide. Can be overriden by the user typing in the textbox if EnforceSliderMinimum is set to false.
		/// </summary>
		public float SliderMinimum
		{
			get
			{
				return _sliderMinimum;
			}
			set
			{
				_sliderMinimum = value;
				Slider.minValue = value;
			}
		}

		/// <summary>
		/// Maximum value the slider can slide. Can be overriden by the user typing in the textbox if EnforceSliderMaximum is set to false.
		/// </summary>
		public float SliderMaximum
		{
			get
			{
				return _sliderMaximum;
			}
			set
			{
				_sliderMaximum = value;
				Slider.maxValue = value;
			}
		}

		/// <summary>
		/// Whether to enforce the SliderMinimum value. If false, users can type values in to the textbox that exceed the minimum value.
		/// </summary>
		public bool EnforceSliderMinimum { get; set; } = true;

		/// <summary>
		/// Whether to enforce the SliderMaximum value. If false, users can type values in to the textbox that exceed the maximum value.
		/// </summary>
		public bool EnforceSliderMaximum { get; set; } = true;

		/// <summary>
		/// Get or set the text of the label.
		/// </summary>
		public string Text
		{
			get
			{
				return _text;
			}
			set
			{
				_text = value;
				Label.text = value;
			}
		}

		/// <summary>
		/// Get or set the value of the toggle.
		/// </summary>
		public float Value
		{
			get
			{
				return GetValue();
			}
			set
			{
				SetValue(value);
			}
		}

		/// <summary>
		/// Create a new SliderSet. Typically you want to use SceneEffectsCategory.AddSliderSet instead of creating these manually.
		/// </summary>
		/// <param name="label">Label UI element</param>
		/// <param name="slider">Slider UI element</param>
		/// <param name="text">Label text</param>
		/// <param name="setter">Method that will be called on value change</param>
		/// <param name="initialValue">Initial value of the slider and textbox</param>
		/// <param name="sliderMinimum">Minimum value of the slider and textbox</param>
		/// <param name="sliderMaximum">Maximum value of the slider and textbox</param>
		public SceneEffectsSliderSet(TextMeshProUGUI label, Slider slider, string text, Action<float> setter, float initialValue, float sliderMinimum, float sliderMaximum)
		{
			Label = label;
			Slider = slider;
			Text = text;
			Setter = setter;
			InitialValue = initialValue;
			SliderMinimum = sliderMinimum;
			SliderMaximum = sliderMaximum;
			ShowInput = false;
			ShowButton = false;
			Init();
		}

		/// <summary>
		/// Create a new SliderSet. Typically you want to use SceneEffectsCategory.AddSliderSet instead of creating these manually.
		/// </summary>
		/// <param name="label">Label UI element</param>
		/// <param name="slider">Slider UI element</param>
		/// <param name="input">Input field UI element</param>
		/// <param name="button">Reset button UI element</param>
		/// <param name="text">Label text</param>
		/// <param name="setter">Method that will be called on value change</param>
		/// <param name="initialValue">Initial value of the slider and textbox</param>
		/// <param name="sliderMinimum">Minimum value of the slider and textbox</param>
		/// <param name="sliderMaximum">Maximum value of the slider and textbox</param>
		public SceneEffectsSliderSet(TextMeshProUGUI label, Slider slider, InputField input, Button button, string text, Action<float> setter, float initialValue, float sliderMinimum, float sliderMaximum)
		{
			Label = label;
			Slider = slider;
			Input = input;
			Button = button;
			Text = text;
			Setter = setter;
			InitialValue = initialValue;
			SliderMinimum = sliderMinimum;
			SliderMaximum = sliderMaximum;
			ShowInput = true;
			ShowButton = true;
			Init();
		}

		private void Init()
		{
			Slider.onValueChanged.RemoveAllListeners();
			if (ShowInput)
			{
				Input.onValueChanged.RemoveAllListeners();
			}
			if (ShowInput)
			{
				Input.onEndEdit.RemoveAllListeners();
			}
			if (ShowButton)
			{
				Button.onClick.RemoveAllListeners();
			}
			Slider.maxValue = SliderMaximum;
			Slider.value = InitialValue;
			if (ShowInput)
			{
				Input.text = InitialValue.ToString(CultureInfo.InvariantCulture);
			}
			Slider.onValueChanged.AddListener(delegate(float value)
			{
				if (_eventsEnabled)
				{
					SetValue(value);
				}
			});
			if (ShowInput)
			{
				Input.onEndEdit.AddListener(delegate(string value)
				{
					if (_eventsEnabled)
					{
						SetValue(value);
					}
				});
			}
			Button.onClick.AddListener(Reset);
			SetValue(InitialValue, triggerEvents: false);
			_eventsEnabled = true;
			Label.gameObject.name = "Label " + Text;
			Slider.gameObject.name = "Slider " + Text;
			if (ShowInput)
			{
				Input.gameObject.name = "InputField " + Text;
			}
			if (ShowButton)
			{
				Button.gameObject.name = "Button " + Text;
			}
		}

		/// <summary>
		/// Get the value of the slider set.
		/// </summary>
		/// <returns>Value of the slider set</returns>
		public float GetValue()
		{
			return _currentValue;
		}

		/// <summary>
		/// Set the value of the slider set, update the UI elements, and trigger the Setter method.
		/// </summary>
		/// <param name="value">Value to set the toggle set</param>
		public void SetValue(string value)
		{
			SetValue(value, triggerEvents: true);
		}

		/// <summary>
		/// Set the value of the slider set and update the UI elements.
		/// </summary>
		/// <param name="value">Value to set the toggle set</param>
		/// <param name="triggerEvents">Whether to trigger the Setter method</param>
		public void SetValue(string value, bool triggerEvents)
		{
			if (!float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
			{
				result = _currentValue;
			}
			SetValue(result, triggerEvents);
		}

		/// <summary>
		/// Set the value of the slider set, update the UI elements, and trigger the Setter method.
		/// </summary>
		/// <param name="value">Value to set the toggle set</param>
		public void SetValue(float value)
		{
			SetValue(value, triggerEvents: true);
		}

		/// <summary>
		/// Set the value of the slider set and update the UI elements.
		/// </summary>
		/// <param name="value">Value to set the toggle set</param>
		/// <param name="triggerEvents">Whether to trigger the Setter method</param>
		public void SetValue(float value, bool triggerEvents)
		{
			if (EnforceSliderMinimum && value < SliderMinimum)
			{
				value = SliderMinimum;
			}
			if (EnforceSliderMaximum && value > SliderMaximum)
			{
				value = SliderMaximum;
			}
			_eventsEnabled = false;
			_currentValue = value;
			Slider.value = value;
			if (ShowInput)
			{
				Input.text = value.ToString(CultureInfo.InvariantCulture);
			}
			_eventsEnabled = true;
			if (triggerEvents)
			{
				Setter(value);
			}
		}

		/// <summary>
		/// Reset the slider set to the initial value, update UI elements, and trigger the Setter method.
		/// </summary>
		public void Reset()
		{
			Reset(triggerEvents: true);
		}

		/// <summary>
		/// Reset the slider set to the initial value and update UI elements.
		/// </summary>
		/// <param name="triggerEvents">Whether to trigger the Setter method</param>
		public void Reset(bool triggerEvents)
		{
			_eventsEnabled = false;
			_currentValue = InitialValue;
			Slider.value = InitialValue;
			if (ShowInput)
			{
				Input.text = InitialValue.ToString(CultureInfo.InvariantCulture);
			}
			_eventsEnabled = true;
			if (triggerEvents)
			{
				Setter(InitialValue);
			}
		}
	}
	/// <summary>
	/// A container for the value of a dropdown, associated label and dropdown, and the setter method that triggers on value change.
	/// </summary>
	public class SceneEffectsDropdownSet
	{
		private int CurrentValue = -1;

		private bool EventsEnabled = true;

		private string _text;

		/// <summary>
		/// Label UI element.
		/// </summary>
		public TextMeshProUGUI Label { get; set; }

		/// <summary>
		/// DropDown UI element.
		/// </summary>
		public Dropdown Dropdown { get; set; }

		/// <summary>
		/// Method called when the value of the Dropdown is changed.
		/// </summary>
		public Action<int> Setter { get; set; }

		/// <summary>
		/// Get or set the text of the label.
		/// </summary>
		public string Text
		{
			get
			{
				return _text;
			}
			set
			{
				_text = value;
				Label.text = value;
			}
		}

		/// <summary>
		/// Get or set the value of the Dropdown.
		/// </summary>
		public int Value
		{
			get
			{
				return GetValue();
			}
			set
			{
				SetValue(value);
			}
		}

		/// <summary>
		/// Create a new Dropdown. Typically, you want to use SceneEffectsCategory.AddDropdown instead of creating these manually.
		/// </summary>
		/// <param name="label">Label UI element</param>
		/// <param name="dropdown">Dropdown component</param>
		/// <param name="text">Label text</param>
		/// <param name="setter">Method that will be called on value change</param>
		/// <param name="optionsList">List of options the Dropdown will display.</param>
		/// <param name="initialValue">The initial value to show on the Dropdown.</param>
		public SceneEffectsDropdownSet(TextMeshProUGUI label, Dropdown dropdown, string text, Action<int> setter, List<string> optionsList, string initialValue = "")
		{
			SceneEffectsDropdownSet sceneEffectsDropdownSet = this;
			Label = label;
			Dropdown = dropdown;
			Setter = setter;
			Dropdown.ClearOptions();
			Dropdown.AddOptions(optionsList);
			Text = text;
			if (!initialValue.IsNullOrWhiteSpace())
			{
				Dropdown.value = optionsList.FindIndex((string r) => r.Equals(initialValue));
				Dropdown.RefreshShownValue();
			}
			Dropdown.onValueChanged.RemoveAllListeners();
			Dropdown.onValueChanged.AddListener(delegate(int value)
			{
				if (sceneEffectsDropdownSet.EventsEnabled)
				{
					sceneEffectsDropdownSet.Value = value;
				}
			});
			Label.gameObject.name = "Label " + Text;
			Dropdown.gameObject.name = "Dropdown " + Text;
		}

		/// <summary>
		/// Get the value of the Dropdown.
		/// </summary>
		/// <returns>Value of the Dropdown</returns>
		public int GetValue()
		{
			return CurrentValue;
		}

		/// <summary>
		/// Set the value of the Dropdown and trigger the Setter method.
		/// </summary>
		/// <param name="value">Value to set the Dropdown</param>
		public void SetValue(int value)
		{
			SetValue(value, triggerEvents: true);
		}

		/// <summary>
		/// Set the value of the Dropdown.
		/// </summary>
		/// <param name="value">Value to set the Dropdown</param>
		/// <param name="triggerEvents">Whether to trigger the Setter method</param>
		public void SetValue(int value, bool triggerEvents)
		{
			EventsEnabled = false;
			CurrentValue = value;
			Dropdown.value = value;
			Dropdown.RefreshShownValue();
			EventsEnabled = true;
			if (triggerEvents)
			{
				Setter(value);
			}
		}
	}
	/// <summary>
	/// Custom toolbar button. Add using <see cref="M:KKAPI.Studio.UI.CustomToolbarButtons.AddLeftToolbarButton(UnityEngine.Texture2D,System.Action)" />.
	/// </summary>
	public class ToolbarButton : BaseGuiEntry
	{
		private static GameObject _existingButton;

		private readonly Subject<Unit> _clicked = new Subject<Unit>();

		private readonly Texture2D _iconTex;

		/// <summary>
		/// Triggered when the button is clicked.
		/// </summary>
		public IObservable<Unit> Clicked => _clicked;

		internal ToolbarButton(Texture2D iconTex)
			: base(null, null)
		{
			if (!iconTex)
			{
				throw new ArgumentNullException("iconTex");
			}
			_iconTex = iconTex;
		}

		/// <inheritdoc />
		protected internal override void Initialize()
		{
		}

		/// <inheritdoc />
		protected override GameObject OnCreateControl(Transform subCategoryList)
		{
			Button button = CreateLeftToolbarButton(_iconTex);
			button.onClick.AddListener(delegate
			{
				_clicked.OnNext(Unit.Default);
			});
			return button.gameObject;
		}

		internal static Button CreateLeftToolbarButton(Texture2D iconTex)
		{
			Sprite sprite = Sprite.Create(iconTex, new Rect(0f, 0f, 32f, 32f), new Vector2(16f, 16f));
			if (_existingButton == null)
			{
				_existingButton = GameObject.Find("StudioScene/Canvas System Menu/01_Button/Button Center");
			}
			Transform parent = _existingButton.transform.parent;
			List<RectTransform> source = parent.OfType<RectTransform>().ToList();
			IEnumerable<IGrouping<int, RectTransform>> source2 = from rt in source
				group rt by Mathf.RoundToInt(rt.anchoredPosition.x);
			IGrouping<int, RectTransform> source3 = (from x in source2
				orderby x.Max((RectTransform r) => r.anchoredPosition.y), x.Key
				select x).First();
			RectTransform rectTransform = source3.OrderByDescending((RectTransform r) => r.anchoredPosition.y).First();
			GameObject gameObject = UnityEngine.Object.Instantiate(_existingButton, parent, worldPositionStays: false);
			gameObject.name = "Button Custom (StudioAPI)";
			RectTransform component = gameObject.GetComponent<RectTransform>();
			component.localScale = Vector3.one;
			component.anchoredPosition = rectTransform.anchoredPosition + new Vector2(0f, 40f);
			Button component2 = component.GetComponent<Button>();
			component2.onClick.ActuallyRemoveAllListeners();
			Image image = component2.image;
			image.sprite = sprite;
			image.color = Color.white;
			return component2;
		}
	}
	/// <summary>
	/// Custom toolbar toggle button. Add using <see cref="M:KKAPI.Studio.UI.CustomToolbarButtons.AddLeftToolbarToggle(UnityEngine.Texture2D,System.Boolean,System.Action{System.Boolean})" />.
	/// </summary>
	public class ToolbarToggle : BaseEditableGuiEntry<bool>
	{
		private readonly Texture2D _iconTex;

		internal ToolbarToggle(Texture2D iconTex, bool initialValue)
			: base((MakerCategory)null, initialValue, (BaseUnityPlugin)null)
		{
			if (!iconTex)
			{
				throw new ArgumentNullException("iconTex");
			}
			_iconTex = iconTex;
		}

		/// <inheritdoc />
		protected internal override void Initialize()
		{
		}

		/// <inheritdoc />
		protected override GameObject OnCreateControl(Transform subCategoryList)
		{
			Button btn = ToolbarButton.CreateLeftToolbarButton(_iconTex);
			btn.onClick.AddListener(delegate
			{
				base.Value = !base.Value;
			});
			base.BufferedValueChanged.Subscribe(delegate
			{
				btn.image.color = (base.Value ? Color.green : Color.white);
			});
			return btn.gameObject;
		}
	}
}
namespace KKAPI.Studio.SaveLoad
{
	/// <summary>
	/// Arguments used in object deleted events
	/// </summary>
	/// <inheritdoc />
	public sealed class ObjectDeletedEventArgs : EventArgs
	{
		/// <summary>
		/// Object deleted by the event
		/// </summary>
		public ObjectCtrlInfo DeletedObject { get; }

		/// <inheritdoc />
		/// <param name="deletedObject">Object being deleted</param>
		public ObjectDeletedEventArgs(ObjectCtrlInfo deletedObject)
		{
			DeletedObject = deletedObject;
		}
	}
	/// <summary>
	/// Arguments used in objects copied events
	/// </summary>
	/// <inheritdoc />
	public sealed class ObjectsCopiedEventArgs : EventArgs
	{
		/// <summary>
		/// Objects copied by the event and their original IDs
		/// </summary>
		public KKAPI.Utilities.ReadOnlyDictionary<int, ObjectCtrlInfo> LoadedObjects { get; }

		/// <inheritdoc />
		/// <param name="loadedObjects">Objects copied by the event</param>
		public ObjectsCopiedEventArgs(KKAPI.Utilities.ReadOnlyDictionary<int, ObjectCtrlInfo> loadedObjects)
		{
			LoadedObjects = loadedObjects;
		}
	}
	/// <summary>
	/// Arguments used in object deleted events
	/// </summary>
	/// <inheritdoc />
	public sealed class ObjectsSelectedEventArgs : EventArgs
	{
		/// <summary>
		/// Object modified by the event
		/// </summary>
		public List<ObjectCtrlInfo> SelectedObjects { get; }

		/// <inheritdoc />
		/// <param name="selectedObjects">Objects being selected</param>
		public ObjectsSelectedEventArgs(List<ObjectCtrlInfo> selectedObjects)
		{
			SelectedObjects = selectedObjects;
		}
	}
	/// <summary>
	/// Arguments used in object deleted events
	/// </summary>
	/// <inheritdoc />
	public sealed class ObjectVisibilityToggledEventArgs : EventArgs
	{
		/// <summary>
		/// Object being toggled
		/// </summary>
		public ObjectCtrlInfo ToggledObject { get; }

		/// <summary>
		/// Visibility of the object
		/// </summary>
		public bool Visible { get; }

		/// <inheritdoc />
		/// <param name="toggledObject">Object being toggled</param>
		/// <param name="visible">Visibility of the object</param>
		public ObjectVisibilityToggledEventArgs(ObjectCtrlInfo toggledObject, bool visible)
		{
			ToggledObject = toggledObject;
			Visible = visible;
		}
	}
	/// <summary>
	/// Base type for custom scene/studio extensions.
	/// It provides many useful methods that abstract away the nasty hooks needed to figure out when
	/// a scene is loaded or imported, or how to save and load your custom data to the scene file.
	///
	/// This controller is a MonoBehaviour that is created upon registration in <see cref="M:KKAPI.Studio.SaveLoad.StudioSaveLoadApi.RegisterExtraBehaviour``1(System.String)" />. 
	/// The controller is created only once. If it's created too late it might miss some scene load events.
	/// It's recommended to register controllers in your Start method.
	/// </summary>
	public abstract class SceneCustomFunctionController : MonoBehaviour
	{
		/// <summary>
		/// ID used for extended data by this controller. It's set when registering the controller
		/// with <see cref="M:KKAPI.Studio.SaveLoad.StudioSaveLoadApi.RegisterExtraBehaviour``1(System.String)" />
		/// </summary>
		public string ExtendedDataId { get; internal set; }

		/// <summary>
		/// Fired when a scene is successfully changed, either by loading, importing or resetting.
		/// </summary>
		/// <param name="operation">Operation that caused this event</param>
		/// <param name="loadedItems">A dictionary of items loaded by this operation and their original IDs.
		/// The IDs are identical to the IDs at the time of saving the scene, even during import.
		/// Warning: The IDs here might not be the same as IDs of the objects in the scene!
		/// To get current scene ID of the object call <see cref="M:KKAPI.Studio.StudioObjectExtensions.GetSceneId(Studio.ObjectCtrlInfo)" /></param>
		protected internal abstract void OnSceneLoad(SceneOperationKind operation, KKAPI.Utilities.ReadOnlyDictionary<int, ObjectCtrlInfo> loadedItems);

		/// <summary>
		/// Fired when a scene is about to be saved and any exteneded data needs to be written.
		/// </summary>
		protected internal abstract void OnSceneSave();

		/// <summary>
		/// Fired when objects are copied.
		/// </summary>
		/// <param name="copiedItems">A dictionary of items loaded by this operation and their original IDs.
		/// The IDs match the original object in the scene.
		/// To get current scene ID of the object call <see cref="M:KKAPI.Studio.StudioObjectExtensions.GetSceneId(Studio.ObjectCtrlInfo)" /></param>
		protected internal virtual void OnObjectsCopied(KKAPI.Utilities.ReadOnlyDictionary<int, ObjectCtrlInfo> copiedItems)
		{
		}

		/// <summary>
		/// Fired when objects are deleted.
		/// </summary>
		/// <param name="objectCtrlInfo">ObjectCtrlInfo associated with the item being deleted</param>
		protected internal virtual void OnObjectDeleted(ObjectCtrlInfo objectCtrlInfo)
		{
		}

		/// <summary>
		/// Fired when objects have their visibility toggled.
		/// </summary>
		/// <param name="objectCtrlInfo">ObjectCtrlInfo associated with the item being toggled</param>
		/// <param name="visible">Visibility of the object</param>
		protected internal virtual void OnObjectVisibilityToggled(ObjectCtrlInfo objectCtrlInfo, bool visible)
		{
		}

		/// <summary>
		/// Fired when objects are selected.
		/// </summary>
		/// <param name="objectCtrlInfo">ObjectCtrlInfo associated with the item being selected</param>
		protected internal virtual void OnObjectsSelected(List<ObjectCtrlInfo> objectCtrlInfo)
		{
		}

		/// <summary>
		/// Get extended data of the last loaded scene by using the ID you specified when registering this controller.
		/// </summary>
		public PluginData GetExtendedData()
		{
			if (ExtendedDataId == null)
			{
				throw new ArgumentException("ExtendedDataId");
			}
			return ExtendedSave.GetSceneExtendedDataById(ExtendedDataId);
		}

		/// <summary>
		/// Save your custom data to the scene under the ID you specified when registering this controller.
		/// </summary>
		/// <param name="data">Your custom data to be written to the scene. Can be null to remove the data.</param>
		public void SetExtendedData(PluginData data)
		{
			if (ExtendedDataId == null)
			{
				throw new ArgumentException("ExtendedDataId");
			}
			ExtendedSave.SetSceneExtendedDataById(ExtendedDataId, data);
		}

		/// <summary>
		/// Get the instance of the Studio game manager object.
		/// </summary>
		public global::Studio.Studio GetStudio()
		{
			return Singleton<global::Studio.Studio>.Instance;
		}
	}
	/// <summary>
	/// Arguments used in scene loaded/imported events
	/// </summary>
	/// <inheritdoc />
	public sealed class SceneLoadEventArgs : EventArgs
	{
		/// <summary>
		/// Operation that caused the event
		/// </summary>
		public SceneOperationKind Operation { get; }

		/// <summary>
		/// Objects loaded by the event and their original IDs (from the time the scene was saved)
		/// </summary>
		public KKAPI.Utilities.ReadOnlyDictionary<int, ObjectCtrlInfo> LoadedObjects { get; }

		/// <inheritdoc />
		/// <param name="operation">Operation that caused the event</param>
		/// <param name="loadedObjects">Objects loaded by the event</param>
		public SceneLoadEventArgs(SceneOperationKind operation, KKAPI.Utilities.ReadOnlyDictionary<int, ObjectCtrlInfo> loadedObjects)
		{
			Operation = operation;
			LoadedObjects = loadedObjects;
		}
	}
	/// <summary>
	/// Scene load/change operations
	/// </summary>
	public enum SceneOperationKind
	{
		/// <summary>
		/// Scene is being loaded and will replace what's currently loaded.
		/// </summary>
		Load,
		/// <summary>
		/// Scene is being loaded and will be added to what's currently loaded. 
		/// <remarks>IDs in the scene will be different from the IDs in the file of the scene being imported, 
		/// use <see cref="T:KKAPI.Studio.SaveLoad.SceneCustomFunctionController" /> to get IDs from the scene file.</remarks>
		/// </summary>
		Import,
		/// <summary>
		/// Scene is being cleared of all state (by default, only user clicking the "Reset" button can trigger this).
		/// This is not triggered when studio starts.
		/// </summary>
		Clear
	}
	/// <summary>
	/// Provides API for loading and saving scenes, as well as a convenient way for registering custom studio functions.
	/// </summary>
	public static class StudioSaveLoadApi
	{
		private static class Hooks
		{
			[Serializable]
			[CompilerGenerated]
			private sealed class <>c
			{
				public static readonly <>c <>9 = new <>c();

				public static SceneEventHandler <>9__3_0;

				public static SceneEventHandler <>9__3_1;

				internal void <SetupHooks>b__3_0(string path)
				{
					_loadOrImportSuccess = true;
				}

				internal void <SetupHooks>b__3_1(string path)
				{
					_loadOrImportSuccess = true;
				}
			}

			/// <summary>
			/// A lookup for original dicKey IDs. It is generated on scene import, and only useful then.
			/// Key is the new ID in the scene (needs to be used currently), while Value is the old ID
			/// in the save file (same as at the time scene was saved).
			/// </summary>
			public static readonly Dictionary<int, int> ImportDictionary = new Dictionary<int, int>();

			private static bool _loadOrImportSuccess;

			private static int _newIndex;

			private static bool _selectingMultipleTreeNodes;

			public static void SetupHooks()
			{
				//IL_0025: Unknown result type (might be due to invalid IL or missing references)
				//IL_002a: Unknown result type (might be due to invalid IL or missing references)
				//IL_0030: Expected O, but got Unknown
				//IL_0049: Unknown result type (might be due to invalid IL or missing references)
				//IL_004e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0054: Expected O, but got Unknown
				Harmony.CreateAndPatchAll(typeof(Hooks), (string)null);
				object obj = <>c.<>9__3_0;
				if (obj == null)
				{
					SceneEventHandler val = delegate
					{
						_loadOrImportSuccess = true;
					};
					<>c.<>9__3_0 = val;
					obj = (object)val;
				}
				ExtendedSave.SceneBeingImported += (SceneEventHandler)obj;
				object obj2 = <>c.<>9__3_1;
				if (obj2 == null)
				{
					SceneEventHandler val2 = delegate
					{
						_loadOrImportSuccess = true;
					};
					<>c.<>9__3_1 = val2;
					obj2 = (object)val2;
				}
				ExtendedSave.SceneBeingLoaded += (SceneEventHandler)obj2;
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(global::Studio.Studio), "GetNewIndex")]
			public static void GetNewIndex(int __result)
			{
				_newIndex = __result;
			}

			/// <summary>
			/// The original code reads the dicKey of an object on import and does nothing with it. Capture that variable and use it to
			/// construct an import dictionary.
			/// GetNewIndex is called before this and will be used to get the dicKey used in the scene.
			/// </summary>
			[HarmonyTranspiler]
			[HarmonyPatch(typeof(ObjectInfo), "Load")]
			public static IEnumerable<CodeInstruction> ObjectInfoLoadTranspiler(IEnumerable<CodeInstruction> instructions)
			{
				foreach (CodeInstruction instruction in instructions)
				{
					if (instruction.opcode == OpCodes.Pop)
					{
						instruction.opcode = OpCodes.Call;
						instruction.operand = typeof(Hooks).GetMethod("SetImportDictionary", AccessTools.all);
					}
					yield return instruction;
				}
			}

			private static void SetImportDictionary(int originalDicKey)
			{
				ImportDictionary[_newIndex] = originalDicKey;
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(global::Studio.Studio), "InitScene")]
			public static void InitScenePrefix()
			{
				ImportDictionary.Clear();
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(global::Studio.Studio), "InitScene")]
			public static void InitScenePostfix()
			{
				if (!LoadInProgress && !ImportInProgress)
				{
					SceneLoadComplete(SceneOperationKind.Clear);
				}
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(global::Studio.Studio), "ImportScene")]
			public static void ImportScenePostfix()
			{
				SceneLoadComplete(SceneOperationKind.Import);
				ImportDictionary.Clear();
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(global::Studio.Studio), "ImportScene")]
			public static void ImportScenePrefix()
			{
				ImportDictionary.Clear();
				ImportInProgress = true;
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(global::Studio.Studio), "LoadSceneCoroutine")]
			public static void LoadSceneCoroutinePostfix(ref IEnumerator __result)
			{
				IEnumerator enumerator = __result;
				__result = new IEnumerator[2]
				{
					enumerator,
					LoadCoPostfixCo()
				}.GetEnumerator();
			}

			private static IEnumerator LoadCoPostfixCo()
			{
				SceneLoadComplete(SceneOperationKind.Load);
				yield break;
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(global::Studio.Studio), "LoadSceneCoroutine")]
			public static void LoadSceneCoroutinePrefix()
			{
				ImportDictionary.Clear();
				LoadInProgress = true;
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(global::Studio.Studio), "LoadScene")]
			public static void LoadScenePostfix()
			{
				SceneLoadComplete(SceneOperationKind.Load);
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(global::Studio.Studio), "LoadScene")]
			public static void LoadScenePrefix()
			{
				ImportDictionary.Clear();
				LoadInProgress = true;
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(global::Studio.Studio), "Duplicate")]
			public static void DuplicatePostfix()
			{
				OnObjectsBeingCopied();
				ImportDictionary.Clear();
			}

			private static void SceneLoadComplete(SceneOperationKind operation)
			{
				LoadInProgress = false;
				ImportInProgress = false;
				if (_loadOrImportSuccess || operation == SceneOperationKind.Clear)
				{
					_loadOrImportSuccess = false;
					OnSceneBeingLoaded(operation);
				}
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(OCIItem), "OnDelete")]
			[HarmonyPatch(typeof(OCIChar), "OnDelete")]
			[HarmonyPatch(typeof(OCIFolder), "OnDelete")]
			[HarmonyPatch(typeof(OCILight), "OnDelete")]
			[HarmonyPatch(typeof(OCICamera), "OnDelete")]
			[HarmonyPatch(typeof(OCIRoute), "OnDelete")]
			private static void OCI_OnDelete(ObjectCtrlInfo __instance)
			{
				OnObjectBeingDeleted(__instance);
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(OCIItem), "OnVisible")]
			[HarmonyPatch(typeof(OCIChar), "OnVisible")]
			[HarmonyPatch(typeof(OCIFolder), "OnVisible")]
			[HarmonyPatch(typeof(OCILight), "OnVisible")]
			[HarmonyPatch(typeof(OCICamera), "OnVisible")]
			[HarmonyPatch(typeof(OCIRoute), "OnVisible")]
			private static void OCI_OnVisible(ObjectCtrlInfo __instance, bool _visible)
			{
				OnObjectVisibilityToggled(__instance, _visible);
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(TreeNodeCtrl), "SelectSingle")]
			private static void TreeNodeCtrl_SelectSingle(TreeNodeObject _node)
			{
				if (!_selectingMultipleTreeNodes)
				{
					ObjectCtrlInfo ctrlInfo = global::Studio.Studio.GetCtrlInfo(_node);
					if (ctrlInfo != null)
					{
						OnObjectsSelected(new List<ObjectCtrlInfo> { ctrlInfo });
					}
				}
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(TreeNodeCtrl), "SelectMultiple", new Type[]
			{
				typeof(TreeNodeObject),
				typeof(TreeNodeObject)
			})]
			private static void TreeNodeCtrl_SelectMultiple()
			{
				List<ObjectCtrlInfo> list = new List<ObjectCtrlInfo>();
				TreeNodeObject[] selectNodes = Singleton<global::Studio.Studio>.Instance.treeNodeCtrl.selectNodes;
				foreach (TreeNodeObject node in selectNodes)
				{
					list.Add(global::Studio.Studio.GetCtrlInfo(node));
				}
				OnObjectsSelected(list);
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(TreeNodeCtrl), "SelectMultiple", new Type[] { typeof(TreeNodeObject[]) })]
			private static void TreeNodeCtrl_SelectMultiple2Prefix()
			{
				_selectingMultipleTreeNodes = true;
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(TreeNodeCtrl), "SelectMultiple", new Type[] { typeof(TreeNodeObject[]) })]
			private static void TreeNodeCtrl_SelectMultiple2Postfix()
			{
				TreeNodeCtrl_SelectMultiple();
				_selectingMultipleTreeNodes = false;
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(TreeNodeCtrl), "SetSelectNode")]
			private static void TreeNodeCtrl_SetSelectNode()
			{
				if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
				{
					List<ObjectCtrlInfo> list = new List<ObjectCtrlInfo>();
					TreeNodeObject[] selectNodes = Singleton<global::Studio.Studio>.Instance.treeNodeCtrl.selectNodes;
					foreach (TreeNodeObject node in selectNodes)
					{
						list.Add(global::Studio.Studio.GetCtrlInfo(node));
					}
					OnObjectsSelected(list);
				}
			}
		}

		[Serializable]
		[CompilerGenerated]
		private sealed class <>c
		{
			public static readonly <>c <>9 = new <>c();

			public static SceneEventHandler <>9__0_0;

			public static Action<EventHandler> <>9__4_0;

			public static Func<KeyValuePair<int, ObjectCtrlInfo>, int> <>9__10_0;

			public static Func<KeyValuePair<int, int>, int> <>9__10_1;

			public static Func<KeyValuePair<int, ObjectCtrlInfo>, KeyValuePair<int, int>, global::<>f__AnonymousType0<int, ObjectCtrlInfo>> <>9__10_2;

			public static Func<global::<>f__AnonymousType0<int, ObjectCtrlInfo>, int> <>9__10_3;

			public static Func<global::<>f__AnonymousType0<int, ObjectCtrlInfo>, ObjectCtrlInfo> <>9__10_4;

			internal void <Init>b__0_0(string path)
			{
				OnSceneBeingSaved();
			}

			internal void <OnSceneBeingSaved>b__4_0(EventHandler handler)
			{
				handler(KoikatuAPI.Instance, EventArgs.Empty);
			}

			internal int <GetLoadedObjects>b__10_0(KeyValuePair<int, ObjectCtrlInfo> pair)
			{
				return pair.Key;
			}

			internal int <GetLoadedObjects>b__10_1(KeyValuePair<int, int> pair)
			{
				return pair.Key;
			}

			internal global::<>f__AnonymousType0<int, ObjectCtrlInfo> <GetLoadedObjects>b__10_2(KeyValuePair<int, ObjectCtrlInfo> current, KeyValuePair<int, int> idLookup)
			{
				return new
				{
					originalId = idLookup.Value,
					obj = current.Value
				};
			}

			internal int <GetLoadedObjects>b__10_3(global::<>f__AnonymousType0<int, ObjectCtrlInfo> x)
			{
				return x.originalId;
			}

			internal ObjectCtrlInfo <GetLoadedObjects>b__10_4(global::<>f__AnonymousType0<int, ObjectCtrlInfo> x)
			{
				return x.obj;
			}
		}

		private static GameObject _functionControllerContainer;

		private static readonly Dictionary<SceneCustomFunctionController, string> _registeredHandlers = new Dictionary<SceneCustomFunctionController, string>();

		/// <summary>
		/// A scene is currently being imported
		/// </summary>
		public static bool ImportInProgress { get; private set; }

		/// <summary>
		/// A scene is currently being loaded (not imported or cleared)
		/// </summary>
		public static bool LoadInProgress { get; private set; }

		/// <summary>
		/// Fired right after a scene is succesfully imported, loaded or cleared. 
		/// Runs immediately after all <see cref="T:KKAPI.Studio.SaveLoad.SceneCustomFunctionController" /> objects trigger their events.
		/// </summary>
		public static event EventHandler<SceneLoadEventArgs> SceneLoad;

		/// <summary>
		/// Fired right before a scene is saved to file. 
		/// Runs immediately after all <see cref="T:KKAPI.Studio.SaveLoad.SceneCustomFunctionController" /> objects trigger their events.
		/// </summary>
		public static event EventHandler SceneSave;

		/// <summary>
		/// Fired when objects in the scene are copied
		/// </summary>
		public static event EventHandler<ObjectsCopiedEventArgs> ObjectsCopied;

		/// <summary>
		/// Fired when an object in the scene is being deleted
		/// </summary>
		public static event EventHandler<ObjectDeletedEventArgs> ObjectDeleted;

		/// <summary>
		/// Fired when an object in the scene had its visibility toggled
		/// </summary>
		public static event EventHandler<ObjectVisibilityToggledEventArgs> ObjectVisibilityToggled;

		/// <summary>
		/// Fired when an object in the scene is selected
		/// </summary>
		public static event EventHandler<ObjectsSelectedEventArgs> ObjectsSelected;

		internal static void Init()
		{
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Expected O, but got Unknown
			Hooks.SetupHooks();
			object obj = <>c.<>9__0_0;
			if (obj == null)
			{
				SceneEventHandler val = delegate
				{
					OnSceneBeingSaved();
				};
				<>c.<>9__0_0 = val;
				obj = (object)val;
			}
			ExtendedSave.SceneBeingSaved += (SceneEventHandler)obj;
			_functionControllerContainer = new GameObject("SceneCustomFunctionController Zoo");
			_functionControllerContainer.transform.SetParent(Chainloader.ManagerObject.transform, worldPositionStays: false);
			if (KoikatuAPI.EnableDebugLogging)
			{
				RegisterExtraBehaviour<TestSceneFunctionController>(null);
			}
		}

		/// <summary>
		/// Register new functionality that will be added to studio. Offers easy API for saving and loading extended data.
		/// All necessary hooking and event subscribing is done for you. Importing scenes is also handled for you.
		/// All you have to do is create a type that inherits from <see cref="T:KKAPI.Studio.SaveLoad.SceneCustomFunctionController" />&gt;
		/// (don't make instances, the API will make them for you). Warning: The custom controller is immediately
		/// created when it's registered, but its OnSceneLoad method is not called until a scene actually loads.
		/// This might mean that if the registration happens too late you will potentially miss some load events. 
		/// </summary>
		/// <typeparam name="T">Type with your custom logic to add to a character</typeparam>
		/// <param name="extendedDataId">Extended data ID used by this behaviour. Set to null if not used.</param>
		public static void RegisterExtraBehaviour<T>(string extendedDataId) where T : SceneCustomFunctionController, new()
		{
			if (_functionControllerContainer != null)
			{
				T val = _functionControllerContainer.AddComponent<T>();
				val.ExtendedDataId = extendedDataId;
				_registeredHandlers.Add(val, extendedDataId);
			}
		}

		private static void OnSceneBeingSaved()
		{
			ApiEventExecutionLogger eventLogger = ApiEventExecutionLogger.GetEventLogger();
			eventLogger.Begin("OnSceneBeingSaved", null);
			foreach (KeyValuePair<SceneCustomFunctionController, string> registeredHandler in _registeredHandlers)
			{
				eventLogger.PluginStart();
				try
				{
					registeredHandler.Key.OnSceneSave();
				}
				catch (Exception ex)
				{
					KoikatuAPI.Logger.LogError((object)ex);
				}
				eventLogger.PluginEnd(registeredHandler.Key);
			}
			StudioSaveLoadApi.SceneSave.SafeInvokeWithLogging(delegate(EventHandler handler)
			{
				handler(KoikatuAPI.Instance, EventArgs.Empty);
			}, "SceneSave", eventLogger);
			eventLogger.End();
		}

		private static void OnSceneBeingLoaded(SceneOperationKind operation)
		{
			ApiEventExecutionLogger eventLogger = ApiEventExecutionLogger.GetEventLogger();
			eventLogger.Begin("OnSceneBeingLoaded", operation.ToString());
			KKAPI.Utilities.ReadOnlyDictionary<int, ObjectCtrlInfo> readOnlyDictionary = GetLoadedObjects(operation).ToReadOnlyDictionary();
			foreach (KeyValuePair<SceneCustomFunctionController, string> registeredHandler in _registeredHandlers)
			{
				eventLogger.PluginStart();
				try
				{
					registeredHandler.Key.OnSceneLoad(operation, readOnlyDictionary);
				}
				catch (Exception ex)
				{
					KoikatuAPI.Logger.LogError((object)ex);
				}
				eventLogger.PluginEnd(registeredHandler.Key);
			}
			SceneLoadEventArgs args = new SceneLoadEventArgs(operation, readOnlyDictionary);
			StudioSaveLoadApi.SceneLoad.SafeInvokeWithLogging(delegate(EventHandler<SceneLoadEventArgs> handler)
			{
				handler(KoikatuAPI.Instance, args);
			}, "SceneLoad", eventLogger);
			eventLogger.End();
		}

		private static void OnObjectsBeingCopied()
		{
			ApiEventExecutionLogger eventLogger = ApiEventExecutionLogger.GetEventLogger();
			eventLogger.Begin("OnObjectsBeingCopied", null);
			KKAPI.Utilities.ReadOnlyDictionary<int, ObjectCtrlInfo> readOnlyDictionary = GetLoadedObjects(SceneOperationKind.Import).ToReadOnlyDictionary();
			foreach (KeyValuePair<SceneCustomFunctionController, string> registeredHandler in _registeredHandlers)
			{
				eventLogger.PluginStart();
				try
				{
					registeredHandler.Key.OnObjectsCopied(readOnlyDictionary);
				}
				catch (Exception ex)
				{
					KoikatuAPI.Logger.LogError((object)ex);
				}
				eventLogger.PluginEnd(registeredHandler.Key);
			}
			ObjectsCopiedEventArgs args = new ObjectsCopiedEventArgs(readOnlyDictionary);
			StudioSaveLoadApi.ObjectsCopied.SafeInvokeWithLogging(delegate(EventHandler<ObjectsCopiedEventArgs> handler)
			{
				handler(KoikatuAPI.Instance, args);
			}, "ObjectsCopied", eventLogger);
			eventLogger.End();
		}

		private static void OnObjectBeingDeleted(ObjectCtrlInfo objectCtrlInfo)
		{
			ApiEventExecutionLogger eventLogger = ApiEventExecutionLogger.GetEventLogger();
			eventLogger.Begin("OnObjectBeingDeleted", objectCtrlInfo?.treeNodeObject?.textName);
			foreach (KeyValuePair<SceneCustomFunctionController, string> registeredHandler in _registeredHandlers)
			{
				eventLogger.PluginStart();
				try
				{
					registeredHandler.Key.OnObjectDeleted(objectCtrlInfo);
				}
				catch (Exception ex)
				{
					KoikatuAPI.Logger.LogError((object)ex);
				}
				eventLogger.PluginEnd(registeredHandler.Key);
			}
			ObjectDeletedEventArgs args = new ObjectDeletedEventArgs(objectCtrlInfo);
			StudioSaveLoadApi.ObjectDeleted.SafeInvokeWithLogging(delegate(EventHandler<ObjectDeletedEventArgs> handler)
			{
				handler(KoikatuAPI.Instance, args);
			}, "ObjectDeleted", eventLogger);
			eventLogger.End();
		}

		private static void OnObjectVisibilityToggled(ObjectCtrlInfo objectCtrlInfo, bool visible)
		{
			ApiEventExecutionLogger eventLogger = ApiEventExecutionLogger.GetEventLogger();
			eventLogger.Begin("OnObjectVisibilityToggled", objectCtrlInfo?.treeNodeObject?.textName);
			foreach (KeyValuePair<SceneCustomFunctionController, string> registeredHandler in _registeredHandlers)
			{
				eventLogger.PluginStart();
				try
				{
					registeredHandler.Key.OnObjectVisibilityToggled(objectCtrlInfo, visible);
				}
				catch (Exception ex)
				{
					KoikatuAPI.Logger.LogError((object)ex);
				}
				eventLogger.PluginEnd(registeredHandler.Key);
			}
			ObjectVisibilityToggledEventArgs args = new ObjectVisibilityToggledEventArgs(objectCtrlInfo, visible);
			StudioSaveLoadApi.ObjectVisibilityToggled.SafeInvokeWithLogging(delegate(EventHandler<ObjectVisibilityToggledEventArgs> handler)
			{
				handler(KoikatuAPI.Instance, args);
			}, "ObjectVisibilityToggled", eventLogger);
			eventLogger.End();
		}

		private static void OnObjectsSelected(List<ObjectCtrlInfo> objectCtrlInfos)
		{
			ApiEventExecutionLogger eventLogger = ApiEventExecutionLogger.GetEventLogger();
			eventLogger.Begin("OnObjectsSelected", null);
			foreach (KeyValuePair<SceneCustomFunctionController, string> registeredHandler in _registeredHandlers)
			{
				eventLogger.PluginStart();
				try
				{
					registeredHandler.Key.OnObjectsSelected(objectCtrlInfos);
				}
				catch (Exception ex)
				{
					KoikatuAPI.Logger.LogError((object)ex);
				}
				eventLogger.PluginEnd(registeredHandler.Key);
			}
			ObjectsSelectedEventArgs args = new ObjectsSelectedEventArgs(objectCtrlInfos);
			StudioSaveLoadApi.ObjectsSelected.SafeInvokeWithLogging(delegate(EventHandler<ObjectsSelectedEventArgs> handler)
			{
				handler(KoikatuAPI.Instance, args);
			}, "ObjectsSelected", eventLogger);
			eventLogger.End();
		}

		private static Dictionary<int, ObjectCtrlInfo> GetLoadedObjects(SceneOperationKind operation)
		{
			return operation switch
			{
				SceneOperationKind.Load => Singleton<global::Studio.Studio>.Instance.dicObjectCtrl, 
				SceneOperationKind.Import => Singleton<global::Studio.Studio>.Instance.dicObjectCtrl.Join(Hooks.ImportDictionary, (KeyValuePair<int, ObjectCtrlInfo> pair) => pair.Key, (KeyValuePair<int, int> pair) => pair.Key, (KeyValuePair<int, ObjectCtrlInfo> current, KeyValuePair<int, int> idLookup) => new
				{
					originalId = idLookup.Value,
					obj = current.Value
				}).ToDictionary(x => x.originalId, x => x.obj), 
				SceneOperationKind.Clear => new Dictionary<int, ObjectCtrlInfo>(), 
				_ => throw new ArgumentOutOfRangeException("operation", operation, null), 
			};
		}
	}
	internal class TestSceneFunctionController : SceneCustomFunctionController
	{
		protected internal override void OnSceneLoad(SceneOperationKind operation, KKAPI.Utilities.ReadOnlyDictionary<int, ObjectCtrlInfo> loadedItems)
		{
			KoikatuAPI.Logger.Log((LogLevel)12, (object)$"OnSceneLoad {operation} - {loadedItems.Count}");
		}

		protected internal override void OnSceneSave()
		{
			KoikatuAPI.Logger.Log((LogLevel)12, (object)"OnSceneSave");
		}

		protected internal override void OnObjectsCopied(KKAPI.Utilities.ReadOnlyDictionary<int, ObjectCtrlInfo> copiedItems)
		{
			KoikatuAPI.Logger.Log((LogLevel)12, (object)"OnObjectsCopied");
		}
	}
}
namespace KKAPI.Utilities
{
	internal class ApiEventExecutionLogger
	{
		[ThreadStatic]
		private static ApiEventExecutionLogger _eventLogger;

		private StringBuilder _sb;

		private Stopwatch _swPlug;

		private Stopwatch _swTotal;

		private int _pluginCounter;

		private int _handlerCounter;

		private bool _running;

		private bool _addedPluginHeader;

		public static ApiEventExecutionLogger GetEventLogger()
		{
			if (_eventLogger == null || _eventLogger._running)
			{
				return _eventLogger = new ApiEventExecutionLogger();
			}
			return _eventLogger;
		}

		private ApiEventExecutionLogger()
		{
		}

		public void Begin(string eventName, string targetName)
		{
			if (!KoikatuAPI.EnableDebugLogging)
			{
				_running = false;
				return;
			}
			if (_sb == null)
			{
				_swPlug = new Stopwatch();
				_swTotal = new Stopwatch();
				_sb = new StringBuilder();
			}
			else
			{
				_sb.Length = 0;
			}
			_running = true;
			_addedPluginHeader = false;
			_sb.Append("Finished raising event: ").Append(eventName);
			if (targetName != null)
			{
				_sb.Append(" | Target: ").Append(targetName);
			}
			_pluginCounter = 0;
			_handlerCounter = 0;
			_swTotal.Reset();
			_swTotal.Start();
		}

		public void PluginStart()
		{
			if (_running)
			{
				if (!_addedPluginHeader)
				{
					_sb.AppendLine().Append("___/ ").Append("Controllers:");
					_addedPluginHeader = true;
				}
				_swPlug.Start();
			}
		}

		public void PluginEnd(MonoBehaviour pluginBehaviour)
		{
			if (_running)
			{
				PluginEnd(pluginBehaviour.GetType().FullName);
			}
		}

		public void PluginEnd(string pluginName)
		{
			if (_running)
			{
				_swPlug.Stop();
				LogEnd(++_pluginCounter, pluginName ?? "NULL", _swPlug.ElapsedMilliseconds);
				_swPlug.Reset();
			}
		}

		public void EventHandlerBegin(string eventName)
		{
			if (_running)
			{
				_sb.AppendLine().Append("___/ ").Append(eventName)
					.Append(" event handlers:");
				_handlerCounter = 0;
			}
		}

		public void EventHandlerStart()
		{
			if (_running)
			{
				_swPlug.Start();
			}
		}

		public void EventHandlerEnd(MethodInfo handler)
		{
			if (_running)
			{
				_swPlug.Stop();
				string text = handler.DeclaringType?.FullName;
				if (text != null && text.StartsWith("KKAPI.Chara.CharacterExtensions+<>c__DisplayClass4_0`2[["))
				{
					text = text.Substring("KKAPI.Chara.CharacterExtensions+<>c__DisplayClass4_0`2[[".Length, text.IndexOfAny(new char[2] { ',', ']' }, "KKAPI.Chara.CharacterExtensions+<>c__DisplayClass4_0`2[[".Length) - "KKAPI.Chara.CharacterExtensions+<>c__DisplayClass4_0`2[[".Length);
				}
				LogEnd(++_handlerCounter, (text ?? "NULL") + "::" + handler.Name, _swPlug.ElapsedMilliseconds);
				_swPlug.Reset();
			}
		}

		private void LogEnd(int count, string fullName, long milliseconds)
		{
			_sb.AppendLine().Append("#").Append(count.ToString("D2"))
				.Append(" ")
				.Append(fullName.PadRight(62))
				.Append("\t - ")
				.Append(milliseconds)
				.Append("ms ")
				.Append(new string('<', (int)(milliseconds / 20)));
		}

		public void End()
		{
			_running = false;
			if (_sb != null)
			{
				_sb.AppendLine().Append("____ ").Append("Total: ")
					.Append(_swTotal.ElapsedMilliseconds)
					.Append("ms");
				KoikatuAPI.Logger.LogDebug((object)_sb.ToString());
			}
		}
	}
	/// <summary>
	/// Class that behaves like a Dictonary[TKey,TValue].
	/// Keys are held by weak reference; if a key is deleted by the GC, the Key-Value of that key is deleted.
	/// </summary>
	internal class WeakKeyDictionary<TKey, TValue> where TKey : class
	{
		private readonly Dictionary<object, TValue> _dict = new Dictionary<object, TValue>(new WeakKeyComperer<object>());

		private int _cleanFrame;

		public TValue this[TKey key]
		{
			get
			{
				return _dict[key];
			}
			set
			{
				SometimesSweepDeadValues();
				_dict[new WeakKey<TKey>(key)] = value;
			}
		}

		public void Add(TKey key, TValue value)
		{
			SometimesSweepDeadValues();
			_dict.Add(new WeakKey<TKey>(key), value);
		}

		public bool Remove(TKey key)
		{
			SometimesSweepDeadValues();
			return _dict.Remove(key);
		}

		public bool TryGetValue(TKey key, out TValue value)
		{
			return _dict.TryGetValue(key, out value);
		}

		private void SometimesSweepDeadValues()
		{
			if (_cleanFrame == Time.frameCount)
			{
				return;
			}
			_cleanFrame = Time.frameCount;
			List<object> list = new List<object>();
			foreach (KeyValuePair<object, TValue> item in _dict)
			{
				WeakKey<TKey> weakKey = (WeakKey<TKey>)item.Key;
				if (!weakKey.IsAlive)
				{
					list.Add(weakKey);
				}
			}
			for (int i = 0; i < list.Count; i++)
			{
				_dict.Remove(list[i]);
			}
		}

		public bool ContainsKey(TKey key)
		{
			return _dict.ContainsKey(key);
		}

		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
		{
			List<object> deads = new List<object>();
			_cleanFrame = Time.frameCount;
			foreach (KeyValuePair<object, TValue> item in _dict)
			{
				WeakKey<TKey> weakKey = (WeakKey<TKey>)item.Key;
				TKey target = weakKey.Target;
				if (target != null)
				{
					yield return new KeyValuePair<TKey, TValue>(target, item.Value);
				}
				else
				{
					deads.Add(weakKey);
				}
			}
			for (int i = 0; i < deads.Count; i++)
			{
				_dict.Remove(deads[i]);
			}
		}
	}
	/// <summary>
	/// A class that behaves as a dictionary key.
	/// Keys are held by weak reference.
	/// </summary>
	internal class WeakKey<TKey> where TKey : class
	{
		private WeakReference _ref;

		private int _hash;

		public bool IsAlive => _ref.IsAlive;

		public TKey Target => (TKey)_ref.Target;

		public WeakKey(TKey key)
		{
			if (key == null)
			{
				throw new ArgumentNullException("key");
			}
			_ref = new WeakReference(key);
			_hash = key.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			if (obj == null || obj.GetHashCode() != _hash)
			{
				return false;
			}
			if (obj is TKey)
			{
				return _ref.Target == obj;
			}
			if (obj is WeakKey<TKey>)
			{
				WeakKey<TKey> weakKey = (WeakKey<TKey>)obj;
				object target = _ref.Target;
				object target2 = weakKey._ref.Target;
				bool flag = target != null;
				bool flag2 = target2 != null;
				if (flag != flag2)
				{
					return false;
				}
				if (flag)
				{
					return target == target2;
				}
				return this == obj;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return _hash;
		}
	}
	internal class WeakKeyComperer<TKey> : IEqualityComparer<object> where TKey : class
	{
		/// <summary>
		/// Determine whether the objects are the same.
		/// Accepts TKey and WeakKey[TKey] as arguments.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public new bool Equals(object x, object y)
		{
			if (y is WeakKey<TKey>)
			{
				return y.Equals(x);
			}
			return x.Equals(y);
		}

		public int GetHashCode(object obj)
		{
			return obj.GetHashCode();
		}
	}
	/// <summary>
	/// Dropdown control for use in GUILayout areas and windows. Keep the instance and call Show on it to draw it inside OnGUI.
	/// Remember to call `DrawDropdownIfOpen` at the very end of the OnGUI area/window to actually display the dropdown list if it's open.
	/// Only one dropdown list can be open globally. If a new dropdown is opened, all others are closed without changing the selected index.
	/// </summary>
	public class ImguiComboBox
	{
		private static ImguiComboBox _visibleBox;

		private Vector2 _scrollPosition = Vector2.zero;

		private GUIContent[] _cachedContents;

		private int _newIndex;

		private bool _wasChanged;

		private Action<int> _onIndexChanged;

		private int _selectedItem;

		private Vector2 _dropdownLocation;

		private Rect _outerRectScreen;

		private Rect _innerRect;

		/// <summary>
		/// Show a button that when clicked opens a dropdown list. Returns new index if user selected a different option, or the old index.
		/// Warning: The list itself is not drawn here, you have to call DrawDropdownIfOpen at the end of your GUILayout area/window.
		/// </summary>
		/// <param name="selectedIndex">Currently selected item from the content list. It will be shown on the button.</param>
		/// <param name="listContent">All items shown in the dropdown list when it is open.</param>
		/// <param name="windowYmax">How low the dropdown can reach. Used to prevent the dropdown from extending off-screen. Set it to the GUILayout.Window rect yMax value.</param>
		/// <param name="listStyle">Optional style of list buttons. By default the standard button style is used.</param>
		public int Show(int selectedIndex, GUIContent[] listContent, int windowYmax = int.MaxValue, GUIStyle listStyle = null)
		{
			GUIContent selectedContent = ((selectedIndex >= 0 && selectedIndex < listContent.Length) ? listContent[selectedIndex] : null);
			Show(selectedContent, () => listContent, delegate(int i)
			{
				_newIndex = i;
				_wasChanged = true;
			}, windowYmax, listStyle);
			if (_wasChanged)
			{
				GUI.changed = true;
				_wasChanged = false;
				return _newIndex;
			}
			return selectedIndex;
		}

		/// <summary>
		/// Show a button that when clicked opens a dropdown list.
		/// Warning: The list itself is not drawn here, you have to call DrawDropdownIfOpen at the end of your GUILayout area/window.
		/// </summary>
		/// <param name="selectedContent">Content shown on the button. It will also be selected in the dropdown list if it exists.</param>
		/// <param name="getListContent">Called once to get the list contents when the button is clicked. It should return contents of the dropdown list, including <paramref name="selectedContent" />.</param>
		/// <param name="onIndexChanged">Called when user selects a new item</param>
		/// <param name="windowYmax"></param>
		/// <param name="listStyle"></param>
		public void Show(GUIContent selectedContent, Func<GUIContent[]> getListContent, Action<int> onIndexChanged, int windowYmax = int.MaxValue, GUIStyle listStyle = null)
		{
			if (getListContent == null)
			{
				throw new ArgumentNullException("getListContent");
			}
			if (onIndexChanged == null)
			{
				throw new ArgumentNullException("onIndexChanged");
			}
			if (listStyle == null)
			{
				listStyle = GUI.skin.button;
			}
			GUIContent content = selectedContent ?? GUIContent.none;
			Rect rect = GUILayoutUtility.GetRect(content, listStyle, GUILayout.ExpandWidth(expand: true));
			bool changed = GUI.changed;
			bool flag = false;
			int controlID = GUIUtility.GetControlID(FocusType.Passive);
			Vector2 vector = Vector2.zero;
			if (Event.current.GetTypeForControl(controlID) == EventType.MouseUp && _visibleBox == this)
			{
				flag = true;
				vector = Event.current.mousePosition;
			}
			if (GUI.Button(rect, content, listStyle))
			{
				_visibleBox = this;
				_cachedContents = getListContent();
			}
			if (_visibleBox == this)
			{
				GUI.enabled = false;
				GUI.color = new Color(1f, 1f, 1f, 2f);
				_dropdownLocation = GUIUtility.GUIToScreenPoint(new Vector2(rect.x, rect.y + listStyle.CalcHeight(_cachedContents[0], 1f)));
				Vector2 vector2 = new Vector2(rect.width, listStyle.CalcHeight(_cachedContents[0], 1f) * (float)_cachedContents.Length);
				_innerRect = new Rect(0f, 0f, vector2.x, vector2.y);
				_outerRectScreen = new Rect(_dropdownLocation.x, _dropdownLocation.y, vector2.x, vector2.y);
				if (_outerRectScreen.yMax > (float)windowYmax)
				{
					_outerRectScreen.height = (float)windowYmax - _outerRectScreen.y;
					_outerRectScreen.width += 20f;
				}
				if (selectedContent == null)
				{
					_selectedItem = -1;
				}
				else
				{
					_selectedItem = Array.FindIndex(_cachedContents, (GUIContent c) => c.text == selectedContent.text && c.image == selectedContent.image);
				}
				_onIndexChanged = onIndexChanged;
				if (vector != Vector2.zero && _outerRectScreen.Contains(GUIUtility.GUIToScreenPoint(vector)))
				{
					flag = false;
				}
			}
			if (flag)
			{
				_visibleBox = null;
			}
			GUI.changed = changed;
		}

		private void DrawDropdownList()
		{
			GUI.enabled = true;
			Vector2 vector = GUIUtility.ScreenToGUIPoint(_dropdownLocation);
			Rect rect = new Rect(vector.x, vector.y, _outerRectScreen.width, _outerRectScreen.height);
			IMGUIUtils.DrawSolidBox(rect);
			_scrollPosition = GUI.BeginScrollView(rect, _scrollPosition, _innerRect, alwaysShowHorizontal: false, alwaysShowVertical: false);
			int num = GUI.SelectionGrid(_innerRect, _selectedItem, _cachedContents, 1);
			if (num != _selectedItem)
			{
				_onIndexChanged(num);
				_visibleBox = null;
			}
			GUI.EndScrollView(handleScrollWheel: true);
		}

		/// <summary>
		/// Draws the dropdown list on top of all other window controls if it is open.
		/// This should always be called at the very end of area/window that `Show` was called in.
		/// Returns true if the dropdown list was opened and subsequently drawn.
		/// </summary>
		public bool DrawDropdownIfOpen()
		{
			if (_visibleBox == this)
			{
				_visibleBox.DrawDropdownList();
				return true;
			}
			return false;
		}
	}
	/// <summary>
	/// Base class for IMGUI windows that are implemented as full MonoBehaviours.
	/// Instantiate to add the window, only one instance should ever exist.
	/// Turn drawing the window on and off by setting the enable property (off by default).
	/// </summary>
	public abstract class ImguiWindow<T> : MonoBehaviour where T : ImguiWindow<T>
	{
		/// <summary>
		/// Instance of the window. Null if none were created yet.
		/// </summary>
		public static T Instance { get; private set; }

		/// <summary>
		/// Title of the window.
		/// </summary>
		public virtual string Title { get; set; }

		/// <summary>
		/// ID of the window, set to a random number by default.
		/// </summary>
		public int WindowId { get; set; }

		/// <summary>
		/// Position and size of the window.
		/// </summary>
		public virtual Rect WindowRect { get; set; }

		/// <summary>
		/// Minimum size of the window.
		/// </summary>
		public Vector2 MinimumSize { get; set; } = new Vector2(100f, 100f);

		/// <summary>
		/// Base constructor
		/// </summary>
		protected ImguiWindow()
		{
			WindowId = GetHashCode();
			Instance = (T)this;
			base.enabled = false;
		}

		/// <summary>
		/// Make sure to call base.OnGUI when overriding!
		/// </summary>
		protected virtual void OnGUI()
		{
			GUISkin skin = GUI.skin;
			GUI.skin = IMGUIUtils.SolidBackgroundGuiSkin;
			WindowRect = GUILayout.Window(WindowId, WindowRect, DrawContentsInt, Title);
			if (WindowRect.width < MinimumSize.x)
			{
				Rect windowRect = WindowRect;
				windowRect.width = MinimumSize.x;
				WindowRect = windowRect;
			}
			if (WindowRect.height < MinimumSize.y)
			{
				Rect windowRect2 = WindowRect;
				windowRect2.height = MinimumSize.y;
				WindowRect = windowRect2;
			}
			GUI.skin = skin;
		}

		private void DrawContentsInt(int id)
		{
			int num = GUI.skin.window.border.top - 4;
			if (GUI.Button(new Rect(WindowRect.width - (float)num - 2f, 2f, num, num), "X"))
			{
				base.enabled = false;
				return;
			}
			try
			{
				DrawContents();
				IMGUIUtils.DrawTooltip(WindowRect);
			}
			catch (Exception ex)
			{
				if (!ex.Message.Contains("GUILayout"))
				{
					KoikatuAPI.Logger.LogError((object)$"[{Title ?? GetType().FullName}] GUI crash: {ex}");
				}
			}
			WindowRect = IMGUIUtils.DragResizeEatWindow(id, WindowRect);
		}

		/// <summary>
		/// Should return the initial desired size of the window, adjusted to fit inside the screen space.
		/// </summary>
		protected abstract Rect GetDefaultWindowRect(Rect screenRect);

		/// <summary>
		/// Draw contents of the IMGUI window (this is inside of the GUILayout.Window func).
		/// Use GUILayout instead of GUI, and expect the window size to change during runtime.
		/// </summary>
		protected abstract void DrawContents();

		/// <summary>
		/// Make sure to call base.OnEnable when overriding!
		/// </summary>
		protected virtual void OnEnable()
		{
			if (WindowRect == default(Rect))
			{
				ResetWindowRect();
			}
		}

		/// <summary>
		/// Reset the window rect (position and size) to its default value.
		/// </summary>
		public void ResetWindowRect()
		{
			Rect screenRect = new Rect(0f, 0f, Screen.width, Screen.height);
			WindowRect = GetDefaultWindowRect(screenRect);
		}
	}
	/// <summary>
	/// API for adding Timeline support to other plugins. Support is done by registering interpolable models that appear in the interpolables list in Timeline.
	/// </summary>
	/// <remarks>
	/// Always call IsTimelineAvailable first and only call other methods in this class if it returns <c>true</c>.
	/// <example>
	/// Example usage (assuming using a KKAPI chara function controller):
	/// <code>
	/// if (TimelineCompatibility.IsTimelineAvailable())
	/// {
	///     TimelineCompatibility.AddCharaFunctionInterpolable&lt;int, PregnancyCharaController&gt;(
	///         GUID, "week", "Pregnancy week",
	///         (oci, parameter, leftValue, rightValue, factor) =&gt; parameter.Data.Week = Mathf.RoundToInt(Mathf.LerpUnclamped(leftValue, rightValue, factor)),
	///         null,
	///         (oci, parameter) =&gt; parameter.Data.Week);
	/// }
	/// </code>
	/// You can use other methods in this class if you are not working with function controllers.
	/// </example>
	/// This API probably won't work if called from a thread.
	/// </remarks>
	public static class TimelineCompatibility
	{
		/// <inheritdoc cref="T:KKAPI.Utilities.TimelineCompatibility.InterpolableDelegate`2" />
		public delegate void InterpolableCharaDelegate<in TValue, in TParameter>(OCIChar oci, TParameter parameter, TValue leftValue, TValue rightValue, float factor);

		/// <inheritdoc cref="T:KKAPI.Utilities.TimelineCompatibility.InterpolableDelegate`2" />
		public delegate void InterpolableDelegate(ObjectCtrlInfo oci, object parameter, object leftValue, object rightValue, float factor);

		/// <summary>
		/// Action that applies a value interpolated by Timeline.
		/// </summary>
		/// <remarks>
		/// <example>Example callback that changes a transform's position:<code>((Transform)parameter).localPosition = Vector3.LerpUnclamped((Vector3)leftValue, (Vector3)rightValue, factor);</code></example>
		/// If <typeparamref name="TValue" /> is a <c>bool</c> then simply use the <paramref name="leftValue" /> directly.
		/// </remarks>
		/// <typeparam name="TValue">The type of the value.</typeparam>
		/// <typeparam name="TParameter">The type of the parameter.</typeparam>
		/// <param name="oci">Target studio object.</param>
		/// <param name="parameter">Parameter that's being worked on.</param>
		/// <param name="leftValue">Value of the interpolable on the left side of current playback position.</param>
		/// <param name="rightValue">Value of the interpolable on the right side of current playback position.</param>
		/// <param name="factor">
		/// Point between <paramref name="leftValue" /> and <paramref name="rightValue" />we are currently at (percentage in 0-1 range).
		/// Use with <see cref="M:UnityEngine.Mathf.LerpUnclamped(System.Single,System.Single,System.Single)" /> and similar methods to get the final value. Make sure to use Unclamped lerp methods unless you need the percentage to be in the 0-1 range.
		/// </param>
		public delegate void InterpolableDelegate<in TValue, in TParameter>(ObjectCtrlInfo oci, TParameter parameter, TValue leftValue, TValue rightValue, float factor);

		private static class XmlConvertHelper
		{
			private static HashSet<Type> _supportedTypes = new HashSet<Type>
			{
				typeof(bool),
				typeof(byte),
				typeof(char),
				typeof(DateTime),
				typeof(DateTimeOffset),
				typeof(decimal),
				typeof(double),
				typeof(Guid),
				typeof(short),
				typeof(int),
				typeof(long),
				typeof(sbyte),
				typeof(float),
				typeof(TimeSpan),
				typeof(ushort),
				typeof(uint),
				typeof(ulong),
				typeof(string),
				typeof(Vector2),
				typeof(Vector3),
				typeof(Vector4),
				typeof(Quaternion),
				typeof(Color),
				typeof(Color32),
				typeof(Rect)
			};

			public static bool IsTypeSupported(Type t)
			{
				return _supportedTypes.Contains(t);
			}

			public static object Deserialize(string s, Type targetType)
			{
				if (targetType == typeof(string))
				{
					return XmlConvert.DecodeName(s);
				}
				if (targetType == typeof(bool))
				{
					return XmlConvert.ToBoolean(s);
				}
				if (targetType == typeof(byte))
				{
					return XmlConvert.ToByte(s);
				}
				if (targetType == typeof(char))
				{
					return XmlConvert.ToChar(s);
				}
				if (targetType == typeof(DateTime))
				{
					return XmlConvert.ToDateTime(s, XmlDateTimeSerializationMode.Utc);
				}
				if (targetType == typeof(DateTimeOffset))
				{
					return XmlConvert.ToDateTimeOffset(s);
				}
				if (targetType == typeof(decimal))
				{
					return XmlConvert.ToDecimal(s);
				}
				if (targetType == typeof(double))
				{
					return XmlConvert.ToDouble(s);
				}
				if (targetType == typeof(Guid))
				{
					return XmlConvert.ToGuid(s);
				}
				if (targetType == typeof(short))
				{
					return XmlConvert.ToInt16(s);
				}
				if (targetType == typeof(int))
				{
					return XmlConvert.ToInt32(s);
				}
				if (targetType == typeof(long))
				{
					return XmlConvert.ToInt64(s);
				}
				if (targetType == typeof(sbyte))
				{
					return XmlConvert.ToSByte(s);
				}
				if (targetType == typeof(float))
				{
					return XmlConvert.ToSingle(s);
				}
				if (targetType == typeof(TimeSpan))
				{
					return XmlConvert.ToTimeSpan(s);
				}
				if (targetType == typeof(ushort))
				{
					return XmlConvert.ToUInt16(s);
				}
				if (targetType == typeof(uint))
				{
					return XmlConvert.ToUInt32(s);
				}
				if (targetType == typeof(ulong))
				{
					return XmlConvert.ToUInt64(s);
				}
				if (targetType == typeof(Vector2))
				{
					string[] array = s.Split(';');
					return new Vector2(XmlConvert.ToSingle(array[0]), XmlConvert.ToSingle(array[1]));
				}
				if (targetType == typeof(Vector3))
				{
					string[] array2 = s.Split(';');
					return new Vector3(XmlConvert.ToSingle(array2[0]), XmlConvert.ToSingle(array2[1]), XmlConvert.ToSingle(array2[2]));
				}
				if (targetType == typeof(Vector4))
				{
					string[] array3 = s.Split(';');
					return new Vector4(XmlConvert.ToSingle(array3[0]), XmlConvert.ToSingle(array3[1]), XmlConvert.ToSingle(array3[2]), XmlConvert.ToSingle(array3[3]));
				}
				if (targetType == typeof(Quaternion))
				{
					string[] array4 = s.Split(';');
					return new Quaternion(XmlConvert.ToSingle(array4[0]), XmlConvert.ToSingle(array4[1]), XmlConvert.ToSingle(array4[2]), XmlConvert.ToSingle(array4[3]));
				}
				if (targetType == typeof(Color))
				{
					string[] array5 = s.Split(';');
					return new Color(XmlConvert.ToSingle(array5[0]), XmlConvert.ToSingle(array5[1]), XmlConvert.ToSingle(array5[2]), XmlConvert.ToSingle(array5[3]));
				}
				if (targetType == typeof(Color32))
				{
					string[] array6 = s.Split(';');
					return new Color32(XmlConvert.ToByte(array6[0]), XmlConvert.ToByte(array6[1]), XmlConvert.ToByte(array6[2]), XmlConvert.ToByte(array6[3]));
				}
				if (targetType == typeof(Rect))
				{
					string[] array7 = s.Split(';');
					return new Rect(XmlConvert.ToSingle(array7[0]), XmlConvert.ToSingle(array7[1]), XmlConvert.ToSingle(array7[2]), XmlConvert.ToSingle(array7[3]));
				}
				throw new NotSupportedException("Could not convert type " + targetType.FullName + " with XmlConvert, you have to convert it manually");
			}

			public static string Serialize(object o, Type targetType)
			{
				if (targetType == typeof(string))
				{
					return XmlConvert.EncodeName((string)o);
				}
				if (targetType == typeof(bool))
				{
					return XmlConvert.ToString((bool)o);
				}
				if (targetType == typeof(byte))
				{
					return XmlConvert.ToString((byte)o);
				}
				if (targetType == typeof(char))
				{
					return XmlConvert.ToString((char)o);
				}
				if (targetType == typeof(DateTime))
				{
					return XmlConvert.ToString((DateTime)o, XmlDateTimeSerializationMode.Utc);
				}
				if (targetType == typeof(DateTimeOffset))
				{
					return XmlConvert.ToString((DateTimeOffset)o);
				}
				if (targetType == typeof(decimal))
				{
					return XmlConvert.ToString((decimal)o);
				}
				if (targetType == typeof(double))
				{
					return XmlConvert.ToString((double)o);
				}
				if (targetType == typeof(Guid))
				{
					return XmlConvert.ToString((Guid)o);
				}
				if (targetType == typeof(short))
				{
					return XmlConvert.ToString((short)o);
				}
				if (targetType == typeof(int))
				{
					return XmlConvert.ToString((int)o);
				}
				if (targetType == typeof(long))
				{
					return XmlConvert.ToString((long)o);
				}
				if (targetType == typeof(sbyte))
				{
					return XmlConvert.ToString((sbyte)o);
				}
				if (targetType == typeof(float))
				{
					return XmlConvert.ToString((float)o);
				}
				if (targetType == typeof(TimeSpan))
				{
					return XmlConvert.ToString((TimeSpan)o);
				}
				if (targetType == typeof(ushort))
				{
					return XmlConvert.ToString((ushort)o);
				}
				if (targetType == typeof(uint))
				{
					return XmlConvert.ToString((uint)o);
				}
				if (targetType == typeof(ulong))
				{
					return XmlConvert.ToString((ulong)o);
				}
				if (targetType == typeof(Vector2))
				{
					Vector2 vector = (Vector2)o;
					return XmlConvert.ToString(vector.x) + ";" + XmlConvert.ToString(vector.y);
				}
				if (targetType == typeof(Vector3))
				{
					Vector3 vector2 = (Vector3)o;
					return XmlConvert.ToString(vector2.x) + ";" + XmlConvert.ToString(vector2.y) + ";" + XmlConvert.ToString(vector2.z);
				}
				if (targetType == typeof(Vector4))
				{
					Vector4 vector3 = (Vector4)o;
					return XmlConvert.ToString(vector3.x) + ";" + XmlConvert.ToString(vector3.y) + ";" + XmlConvert.ToString(vector3.z) + ";" + XmlConvert.ToString(vector3.w);
				}
				if (targetType == typeof(Quaternion))
				{
					Quaternion quaternion = (Quaternion)o;
					return XmlConvert.ToString(quaternion.x) + ";" + XmlConvert.ToString(quaternion.y) + ";" + XmlConvert.ToString(quaternion.z) + ";" + XmlConvert.ToString(quaternion.w);
				}
				if (targetType == typeof(Color))
				{
					Color color = (Color)o;
					return XmlConvert.ToString(color.r) + ";" + XmlConvert.ToString(color.g) + ";" + XmlConvert.ToString(color.b) + ";" + XmlConvert.ToString(color.a);
				}
				if (targetType == typeof(Color32))
				{
					Color32 color2 = (Color32)o;
					return XmlConvert.ToString(color2.r) + ";" + XmlConvert.ToString(color2.g) + ";" + XmlConvert.ToString(color2.b) + ";" + XmlConvert.ToString(color2.a);
				}
				if (targetType == typeof(Rect))
				{
					Rect rect = (Rect)o;
					return XmlConvert.ToString(rect.x) + ";" + XmlConvert.ToString(rect.y) + ";" + XmlConvert.ToString(rect.width) + ";" + XmlConvert.ToString(rect.height);
				}
				throw new NotSupportedException("Could not convert type " + targetType.FullName + " with XmlConvert, you have to convert it manually");
			}
		}

		private static Func<float> _getPlaybackTime;

		private static Func<float> _getDuration;

		private static Func<bool> _getIsPlaying;

		private static Action _play;

		private static MethodInfo _addInterpolableModelStatic;

		private static MethodInfo _addInterpolableModelDynamic;

		private static Action _refreshInterpolablesList;

		private static Type _interpolableDelegate;

		private static bool _initialized;

		private static readonly object _lockObj = new object();

		/// <summary>
		/// Check if Timeline is loaded and available to be used. If false, other methods in this class will throw.
		/// This must be called after all plugins finish loadeding (in Start/Main instead of constructors/Awake).
		/// It always returns false outside of studio.
		/// </summary>
		public static bool IsTimelineAvailable()
		{
			lock (_lockObj)
			{
				if (_initialized)
				{
					return _interpolableDelegate != null;
				}
				if (!(bool)AccessTools.Field(typeof(Chainloader), "_loaded").GetValue(null))
				{
					throw new InvalidOperationException("This API can't be used before all plugins finish loadeding (in constructors/Awake). Use it inside Start or Main instead.");
				}
				if (!StudioAPI.InsideStudio)
				{
					_initialized = true;
					return false;
				}
				try
				{
					Type type = Type.GetType("Timeline.Timeline,Timeline", throwOnError: false);
					if (type != null)
					{
						_getPlaybackTime = (Func<float>)Delegate.CreateDelegate(typeof(Func<float>), type.GetProperty("playbackTime", BindingFlags.Static | BindingFlags.Public).GetGetMethod());
						_getDuration = (Func<float>)Delegate.CreateDelegate(typeof(Func<float>), type.GetProperty("duration", BindingFlags.Static | BindingFlags.Public).GetGetMethod());
						_getIsPlaying = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), type.GetProperty("isPlaying", BindingFlags.Static | BindingFlags.Public).GetGetMethod());
						_play = (Action)Delegate.CreateDelegate(typeof(Action), type.GetMethod("Play", BindingFlags.Static | BindingFlags.Public));
						_addInterpolableModelStatic = type.GetMethod("AddInterpolableModelStatic", BindingFlags.Static | BindingFlags.Public);
						_addInterpolableModelDynamic = type.GetMethod("AddInterpolableModelDynamic", BindingFlags.Static | BindingFlags.Public);
						_refreshInterpolablesList = (Action)Delegate.CreateDelegate(typeof(Action), type.GetMethod("RefreshInterpolablesList", BindingFlags.Static | BindingFlags.Public));
						_interpolableDelegate = Type.GetType("Timeline.InterpolableDelegate,Timeline");
						_initialized = true;
						return true;
					}
				}
				catch (Exception ex)
				{
					UnityEngine.Debug.LogError("Exception caught when trying to find Timeline: " + ex);
				}
			}
			return false;
		}

		/// <summary>
		/// Gets current playback location in seconds.
		/// </summary>
		public static float GetPlaybackTime()
		{
			InitCheck();
			return _getPlaybackTime();
		}

		/// <summary>
		/// Gets the total duration in seconds.
		/// </summary>
		public static float GetDuration()
		{
			InitCheck();
			return _getDuration();
		}

		/// <summary>
		/// Check if timeline is currently playing.
		/// </summary>
		public static bool GetIsPlaying()
		{
			InitCheck();
			return _getIsPlaying();
		}

		/// <summary>
		/// Play/Pause timeline playback (toggles between the two).
		/// </summary>
		public static void Play()
		{
			InitCheck();
			_play();
		}

		/// <summary>
		/// Adds an interpolableModel with a dynamic parameter to the available interpolables list. This interpolable can then be instantiated by the user by adding a keyframe.
		/// The dynamic parameter means that every time user creates this interpolable, you can provide a different parameter - this allows having multiple instances of this interpolable for a single studio object. The instances are differentiated by the parameter, which must be unique and have a unique hash code (if the parameters have identical hashes they are treated as the same thing).
		/// Examples where this is useful: editing individual character bone positions/scales, changing color of individual accessories.
		/// If you want to make a global interpolable you can use AddInterpolableModelStatic instead.
		/// Try to keep the callbacks as light and self-contained as possible since they can be called on every frame during playback or UI usage (except the xml callbacks). If you need to find some object to later use in the callbacks, consider using it as the parameter (that way it will only be computed once and you can use it later for free, just make sure the parameter is always the same since if you have different parameters will create separate instances).
		/// </summary>
		/// <remarks>
		/// Use cases:
		/// <list type="bullet">
		/// <item>To make a global interpolable (affects overall studio state) set <paramref name="useOciInHash" /> to false and don't provide unique parameters (or any parameters at all).</item>
		/// <item>To make a single interpolable per studio object (e.g. changing a character's hair color) set <paramref name="useOciInHash" /> to true and don't provide unique parameters (or any parameters at all).</item>
		/// <item>To make multiple interpolables per studio object (e.g. changing color of different clothing items) set <paramref name="useOciInHash" /> to true and provide unique parameters that represent the exact thing that is being changed.
		/// How this works: When user adds a new interpolable then</item>
		/// </list>
		/// Execution order:
		/// <list type="number">
		/// <item>InterpolableModel is created and added</item>
		/// <item><paramref name="isCompatibleWithTarget" /> is called when any studio object is selected (null if nothing is selected)</item>
		/// <item>When user adds a new keyframe for this interpolable, <paramref name="isCompatibleWithTarget" /> is called to determine if it can be created.</item>
		/// <item><paramref name="getParameter" /> and <paramref name="getValue" /> are called in that order. The new interpolable's hash is computed based on <paramref name="useOciInHash" /> and the returned parameter.</item>
		/// <item><paramref name="shouldShow" /> and <paramref name="getFinalName" /> are called after creating the interpolable and every time the user does something that should update the UI.</item>
		/// <item>If playing, <paramref name="interpolateBefore" /> and <paramref name="interpolateAfter" /> are called in that order. <paramref name="checkIntegrity" /> called before each of them to determine if the callbacks should be ran. Put logic that affects the studio object here (e.g. changing bone position).</item>
		/// <item>When saving the studio scene or just the timeline data, <paramref name="writeParameterToXml" /> and <paramref name="writeValueToXml" /> are called as necessary.</item>
		/// </list>
		/// </remarks>
		/// <typeparam name="TValue">The type of the value.</typeparam>
		/// <typeparam name="TParameter">The type of the parameter.</typeparam>
		/// <param name="owner">Display name of the owner of this interpolable. It's used both in the UI and for serialization. Must not change between game restarts.</param>
		/// <param name="id">ID of this interpolable used for serialization. Must be unique and not change between game restarts.</param>
		/// <param name="name">Display name shown in the available interpolable list. It's also used for names of the created interpolable instances if <paramref name="getFinalName" /> is not provided.</param>
		/// <param name="interpolateBefore">
		/// Action to do around the start of the frame (inside Update).
		/// Make changes to the object state here (e.g. change transform position).
		/// <example><code>((Transform)parameter).localPosition = Vector3.LerpUnclamped((Vector3)leftValue, (Vector3)rightValue, factor);</code></example>
		/// </param>
		/// <param name="interpolateAfter">
		/// Action to do around the end of the frame (inside LateUpdate).
		/// Make changes to the object state here (e.g. change transform position) if you need to do them inside LateUpdate.
		/// <example><code>((Transform)parameter).localPosition = Vector3.LerpUnclamped((Vector3)leftValue, (Vector3)rightValue, factor);</code></example>
		/// </param>
		/// <param name="isCompatibleWithTarget">
		/// Func that should return true if this interpolable can be used on the given studio object.
		/// This is called before all other callbacks and before the interpolable is actually created.
		/// <example><code>oci is OCIChar</code></example>
		/// </param>
		/// <param name="getValue">
		/// Get the current value of this interpolable.
		/// If loading timeline data, <paramref name="readValueFromXml" /> is used instead.
		/// <example><code>((Transform)parameter).localPosition</code></example>
		/// </param>
		/// <param name="readValueFromXml">
		/// Deserialize a value of this interpolable from XML.
		/// It's optional as long as you use a common simple value type like <see cref="T:System.Single" /> or <see cref="T:UnityEngine.Vector3" /> that has a deserializer available.
		/// <example><code>node.ReadVector3("value")</code></example>
		/// </param>
		/// <param name="writeValueToXml">
		/// Serialize a value of this interpolable to XML.
		/// It's optional as long as you use a common simple value type like <see cref="T:System.Single" /> or <see cref="T:UnityEngine.Vector3" /> that has a serializer available.
		/// <example><code>writer.WriteValue("value", (Vector3)obj)</code></example>
		/// </param>
		/// <param name="getParameter">
		/// Get object/data to apply changes to.
		/// The main use of parameters is when you can have multiple instances of this interpolable on a single studio object - you can then use parameters to differentiate individual parts, e.g. bones.
		/// For example you can use the transform that this interporable will affect (set its position etc.) as a parameter - that way you can have separate interpolables for different transforms of the same studio object.
		/// This parameter will be passed to other callbacks, and it will be serialized and deserialized when saving/loading timeline data (serializing it is optional but without serializing the parameter you will lose it after scene reload).
		/// If you are using parameters, make sure to implement <paramref name="getFinalName" /> as well - it's used to give different names to interpolables based on the parameter (e.g. transform name).
		/// Note: Everything related to parameters is optional and can be skipped if you don't need multiple instances of the interpolable on a single studio object.
		/// Parameters should be hashable (hash should be stable during the game session).
		/// <example><code>((OCIChar)oci).GetChaControl().transform.Find("someBone")</code></example>
		/// </param>
		/// <param name="readParameterFromXml">
		/// Deserialize a parameter previosuly saved with <paramref name="writeParameterToXml" />.
		/// <example><code>return oci.guideObject.transformTarget.transform.Find(node.Attributes["parameter"].Value));</code></example>
		/// </param>
		/// <param name="writeParameterToXml">
		/// Serialize the parameter to store it inside timeline data.
		/// It will be later deserialized with <paramref name="readParameterFromXml" />.
		/// For example if you use a <see cref="T:UnityEngine.Transform" /> for your parameter, you can write its path or name if its unique, and then search for it when deserializing.
		/// <example><code>writer.WriteAttributeString("parameter", ((Transform)parameter).name);</code></example>
		/// </param>
		/// <param name="checkIntegrity">
		/// Check if the parameter and data are valid and can be interpolated.
		/// If false is returned, <paramref name="interpolateBefore" /> and <paramref name="interpolateAfter" /> are not called (everything else is unaffected).
		/// </param>
		/// <param name="useOciInHash">If true, assign this interpolable to a specific studio object. If false, the interpolable is global and most callbacks will be given <c>null</c> as the <see cref="T:Studio.ObjectCtrlInfo" /> argument.</param>
		/// <param name="getFinalName">Get a name for a newly created interpolable instance. Use to differentiate different instances of the same interpolable. If <c>null</c>, <paramref name="name" /> is used.</param>
		/// <param name="shouldShow">Check if this interpolable <b>instance</b> should apper in the list given the current state. This is called on every list refresh <b>after</b> the interpolable is created.</param>
		public static void AddInterpolableModelDynamic<TValue, TParameter>(string owner, string id, string name, InterpolableDelegate<TValue, TParameter> interpolateBefore, InterpolableDelegate<TValue, TParameter> interpolateAfter, Func<ObjectCtrlInfo, bool> isCompatibleWithTarget, Func<ObjectCtrlInfo, TParameter, TValue> getValue, Func<TParameter, XmlNode, TValue> readValueFromXml = null, Action<TParameter, XmlTextWriter, TValue> writeValueToXml = null, Func<ObjectCtrlInfo, TParameter> getParameter = null, Func<ObjectCtrlInfo, XmlNode, TParameter> readParameterFromXml = null, Action<ObjectCtrlInfo, XmlTextWriter, TParameter> writeParameterToXml = null, Func<ObjectCtrlInfo, TParameter, TValue, TValue, bool> checkIntegrity = null, bool useOciInHash = true, Func<string, ObjectCtrlInfo, TParameter, string> getFinalName = null, Func<ObjectCtrlInfo, TParameter, bool> shouldShow = null)
		{
			Type valueType = typeof(TValue);
			if (readValueFromXml == null)
			{
				if (!XmlConvertHelper.IsTypeSupported(valueType))
				{
					throw new ArgumentNullException("readValueFromXml", $"Can not convert type {valueType} automatically, you have to provide custom readValueFromXml");
				}
				readValueFromXml = (TParameter parameter, XmlNode node) => (TValue)XmlConvertHelper.Deserialize(node.Attributes["value"].Value, valueType);
			}
			if (writeValueToXml == null)
			{
				if (!XmlConvertHelper.IsTypeSupported(valueType))
				{
					throw new ArgumentNullException("writeValueToXml", $"Can not convert type {valueType} automatically, you have to provide custom writeValueToXml");
				}
				writeValueToXml = delegate(TParameter parameter, XmlTextWriter writer, TValue val)
				{
					writer.WriteAttributeString("value", XmlConvertHelper.Serialize(val, valueType));
				};
			}
			AddInterpolableModelDynamic(owner, id, name, (interpolateBefore != null) ? ((InterpolableDelegate)delegate(ObjectCtrlInfo oci, object parameter, object value, object rightValue, float factor)
			{
				interpolateBefore(oci, (TParameter)parameter, (TValue)value, (TValue)rightValue, factor);
			}) : null, (interpolateAfter != null) ? ((InterpolableDelegate)delegate(ObjectCtrlInfo oci, object parameter, object value, object rightValue, float factor)
			{
				interpolateAfter(oci, (TParameter)parameter, (TValue)value, (TValue)rightValue, factor);
			}) : null, isCompatibleWithTarget, (getValue != null) ? ((Func<ObjectCtrlInfo, object, object>)((ObjectCtrlInfo info, object param) => getValue(info, (TParameter)param))) : null, (object param, XmlNode node) => readValueFromXml((TParameter)param, node), delegate(object param, XmlTextWriter writer, object val)
			{
				writeValueToXml((TParameter)param, writer, (TValue)val);
			}, (getParameter != null) ? ((Func<ObjectCtrlInfo, object>)((ObjectCtrlInfo info) => getParameter(info))) : null, (readParameterFromXml != null) ? ((Func<ObjectCtrlInfo, XmlNode, object>)((ObjectCtrlInfo info, XmlNode node) => readParameterFromXml(info, node))) : null, (writeParameterToXml != null) ? ((Action<ObjectCtrlInfo, XmlTextWriter, object>)delegate(ObjectCtrlInfo info, XmlTextWriter writer, object param)
			{
				writeParameterToXml(info, writer, (TParameter)param);
			}) : null, (checkIntegrity != null) ? ((Func<ObjectCtrlInfo, object, object, object, bool>)((ObjectCtrlInfo info, object parameter, object left, object right) => checkIntegrity(info, (TParameter)parameter, (TValue)left, (TValue)right))) : null, useOciInHash, (getFinalName != null) ? ((Func<string, ObjectCtrlInfo, object, string>)((string s, ObjectCtrlInfo info, object param) => getFinalName(s, info, (TParameter)param))) : null, (shouldShow != null) ? ((Func<ObjectCtrlInfo, object, bool>)((ObjectCtrlInfo info, object parameter) => shouldShow(info, (TParameter)parameter))) : null);
		}

		/// <inheritdoc cref="M:KKAPI.Utilities.TimelineCompatibility.AddInterpolableModelDynamic``2(System.String,System.String,System.String,KKAPI.Utilities.TimelineCompatibility.InterpolableDelegate{``0,``1},KKAPI.Utilities.TimelineCompatibility.InterpolableDelegate{``0,``1},System.Func{Studio.ObjectCtrlInfo,System.Boolean},System.Func{Studio.ObjectCtrlInfo,``1,``0},System.Func{``1,System.Xml.XmlNode,``0},System.Action{``1,System.Xml.XmlTextWriter,``0},System.Func{Studio.ObjectCtrlInfo,``1},System.Func{Studio.ObjectCtrlInfo,System.Xml.XmlNode,``1},System.Action{Studio.ObjectCtrlInfo,System.Xml.XmlTextWriter,``1},System.Func{Studio.ObjectCtrlInfo,``1,``0,``0,System.Boolean},System.Boolean,System.Func{System.String,Studio.ObjectCtrlInfo,``1,System.String},System.Func{Studio.ObjectCtrlInfo,``1,System.Boolean})" />
		public static void AddInterpolableModelDynamic(string owner, string id, string name, InterpolableDelegate interpolateBefore, InterpolableDelegate interpolateAfter, Func<ObjectCtrlInfo, bool> isCompatibleWithTarget, Func<ObjectCtrlInfo, object, object> getValue, Func<object, XmlNode, object> readValueFromXml, Action<object, XmlTextWriter, object> writeValueToXml, Func<ObjectCtrlInfo, object> getParameter, Func<ObjectCtrlInfo, XmlNode, object> readParameterFromXml = null, Action<ObjectCtrlInfo, XmlTextWriter, object> writeParameterToXml = null, Func<ObjectCtrlInfo, object, object, object, bool> checkIntegrity = null, bool useOciInHash = true, Func<string, ObjectCtrlInfo, object, string> getFinalName = null, Func<ObjectCtrlInfo, object, bool> shouldShow = null)
		{
			InitCheck();
			if (owner == null)
			{
				throw new ArgumentNullException("owner");
			}
			if (id == null)
			{
				throw new ArgumentNullException("id");
			}
			if (name == null)
			{
				throw new ArgumentNullException("name");
			}
			if (isCompatibleWithTarget == null)
			{
				throw new ArgumentNullException("isCompatibleWithTarget");
			}
			if (getValue == null)
			{
				throw new ArgumentNullException("getValue");
			}
			if (readValueFromXml == null)
			{
				throw new ArgumentNullException("readValueFromXml");
			}
			if (writeValueToXml == null)
			{
				throw new ArgumentNullException("writeValueToXml");
			}
			Delegate obj = null;
			if (interpolateBefore != null)
			{
				obj = Delegate.CreateDelegate(_interpolableDelegate, interpolateBefore.Target, interpolateBefore.Method);
			}
			Delegate obj2 = null;
			if (interpolateAfter != null)
			{
				obj2 = Delegate.CreateDelegate(_interpolableDelegate, interpolateAfter.Target, interpolateAfter.Method);
			}
			_addInterpolableModelDynamic.Invoke(null, new object[16]
			{
				owner, id, name, obj, obj2, isCompatibleWithTarget, getValue, readValueFromXml, writeValueToXml, getParameter,
				readParameterFromXml, writeParameterToXml, checkIntegrity, useOciInHash, getFinalName, shouldShow
			});
		}

		/// <summary>
		/// Adds an interpolableModel to the available interpolables list. This interpolable can then be instantiated by the user by adding a keyframe.
		/// User can create a single instance of this interpolable for each studio object (or single instance globally if <paramref name="useOciInHash" /> is <c>false</c>). If you want to have multiple instances of this interpolable for a single studio object use AddInterpolableModelDynamic instead.
		/// Try to keep the callbacks as light and self-contained as possible since they can be called on every frame during playback or UI usage (except the xml callbacks).
		/// </summary>
		/// <param name="parameter">Optional parameter for this interpolable. Mostly useful for global interpolables where required objects might be expensive to get.</param>
		/// <inheritdoc cref="M:KKAPI.Utilities.TimelineCompatibility.AddInterpolableModelDynamic``2(System.String,System.String,System.String,KKAPI.Utilities.TimelineCompatibility.InterpolableDelegate{``0,``1},KKAPI.Utilities.TimelineCompatibility.InterpolableDelegate{``0,``1},System.Func{Studio.ObjectCtrlInfo,System.Boolean},System.Func{Studio.ObjectCtrlInfo,``1,``0},System.Func{``1,System.Xml.XmlNode,``0},System.Action{``1,System.Xml.XmlTextWriter,``0},System.Func{Studio.ObjectCtrlInfo,``1},System.Func{Studio.ObjectCtrlInfo,System.Xml.XmlNode,``1},System.Action{Studio.ObjectCtrlInfo,System.Xml.XmlTextWriter,``1},System.Func{Studio.ObjectCtrlInfo,``1,``0,``0,System.Boolean},System.Boolean,System.Func{System.String,Studio.ObjectCtrlInfo,``1,System.String},System.Func{Studio.ObjectCtrlInfo,``1,System.Boolean})" />
		public static void AddInterpolableModelStatic<TValue, TParameter>(string owner, string id, TParameter parameter, string name, InterpolableDelegate<TValue, TParameter> interpolateBefore, InterpolableDelegate<TValue, TParameter> interpolateAfter, Func<ObjectCtrlInfo, bool> isCompatibleWithTarget, Func<ObjectCtrlInfo, TParameter, TValue> getValue, Func<TParameter, XmlNode, TValue> readValueFromXml = null, Action<TParameter, XmlTextWriter, TValue> writeValueToXml = null, Func<ObjectCtrlInfo, XmlNode, TParameter> readParameterFromXml = null, Action<ObjectCtrlInfo, XmlTextWriter, TParameter> writeParameterToXml = null, Func<ObjectCtrlInfo, TParameter, TValue, TValue, bool> checkIntegrity = null, bool useOciInHash = true, Func<string, ObjectCtrlInfo, TParameter, string> getFinalName = null, Func<ObjectCtrlInfo, TParameter, bool> shouldShow = null)
		{
			Type valueType = typeof(TValue);
			if (readValueFromXml == null)
			{
				if (!XmlConvertHelper.IsTypeSupported(valueType))
				{
					throw new ArgumentNullException("readValueFromXml", $"Can not convert type {valueType} automatically, you have to provide custom readValueFromXml");
				}
				readValueFromXml = (TParameter param, XmlNode node) => (TValue)XmlConvertHelper.Deserialize(node.Attributes["value"].Value, valueType);
			}
			if (writeValueToXml == null)
			{
				if (!XmlConvertHelper.IsTypeSupported(valueType))
				{
					throw new ArgumentNullException("writeValueToXml", $"Can not convert type {valueType} automatically, you have to provide custom writeValueToXml");
				}
				writeValueToXml = delegate(TParameter param, XmlTextWriter writer, TValue val)
				{
					writer.WriteAttributeString("value", XmlConvertHelper.Serialize(val, valueType));
				};
			}
			AddInterpolableModelStatic(owner, id, parameter, name, (interpolateBefore != null) ? ((InterpolableDelegate)delegate(ObjectCtrlInfo oci, object param, object value, object rightValue, float factor)
			{
				interpolateBefore(oci, (TParameter)param, (TValue)value, (TValue)rightValue, factor);
			}) : null, (interpolateAfter != null) ? ((InterpolableDelegate)delegate(ObjectCtrlInfo oci, object param, object value, object rightValue, float factor)
			{
				interpolateAfter(oci, (TParameter)param, (TValue)value, (TValue)rightValue, factor);
			}) : null, isCompatibleWithTarget, (getValue != null) ? ((Func<ObjectCtrlInfo, object, object>)((ObjectCtrlInfo info, object param) => getValue(info, (TParameter)param))) : null, (object param, XmlNode node) => readValueFromXml((TParameter)param, node), delegate(object param, XmlTextWriter writer, object val)
			{
				writeValueToXml((TParameter)param, writer, (TValue)val);
			}, (readParameterFromXml != null) ? ((Func<ObjectCtrlInfo, XmlNode, object>)((ObjectCtrlInfo info, XmlNode node) => readParameterFromXml(info, node))) : null, (writeParameterToXml != null) ? ((Action<ObjectCtrlInfo, XmlTextWriter, object>)delegate(ObjectCtrlInfo info, XmlTextWriter writer, object param)
			{
				writeParameterToXml(info, writer, (TParameter)param);
			}) : null, (checkIntegrity != null) ? ((Func<ObjectCtrlInfo, object, object, object, bool>)((ObjectCtrlInfo info, object param, object left, object right) => checkIntegrity(info, (TParameter)param, (TValue)left, (TValue)right))) : null, useOciInHash, (getFinalName != null) ? ((Func<string, ObjectCtrlInfo, object, string>)((string s, ObjectCtrlInfo info, object param) => getFinalName(s, info, (TParameter)param))) : null, (shouldShow != null) ? ((Func<ObjectCtrlInfo, object, bool>)((ObjectCtrlInfo info, object param) => shouldShow(info, (TParameter)param))) : null);
		}

		/// <inheritdoc cref="M:KKAPI.Utilities.TimelineCompatibility.AddInterpolableModelStatic``2(System.String,System.String,``1,System.String,KKAPI.Utilities.TimelineCompatibility.InterpolableDelegate{``0,``1},KKAPI.Utilities.TimelineCompatibility.InterpolableDelegate{``0,``1},System.Func{Studio.ObjectCtrlInfo,System.Boolean},System.Func{Studio.ObjectCtrlInfo,``1,``0},System.Func{``1,System.Xml.XmlNode,``0},System.Action{``1,System.Xml.XmlTextWriter,``0},System.Func{Studio.ObjectCtrlInfo,System.Xml.XmlNode,``1},System.Action{Studio.ObjectCtrlInfo,System.Xml.XmlTextWriter,``1},System.Func{Studio.ObjectCtrlInfo,``1,``0,``0,System.Boolean},System.Boolean,System.Func{System.String,Studio.ObjectCtrlInfo,``1,System.String},System.Func{Studio.ObjectCtrlInfo,``1,System.Boolean})" />
		public static void AddInterpolableModelStatic(string owner, string id, object parameter, string name, InterpolableDelegate interpolateBefore, InterpolableDelegate interpolateAfter, Func<ObjectCtrlInfo, bool> isCompatibleWithTarget, Func<ObjectCtrlInfo, object, object> getValue, Func<object, XmlNode, object> readValueFromXml, Action<object, XmlTextWriter, object> writeValueToXml, Func<ObjectCtrlInfo, XmlNode, object> readParameterFromXml = null, Action<ObjectCtrlInfo, XmlTextWriter, object> writeParameterToXml = null, Func<ObjectCtrlInfo, object, object, object, bool> checkIntegrity = null, bool useOciInHash = true, Func<string, ObjectCtrlInfo, object, string> getFinalName = null, Func<ObjectCtrlInfo, object, bool> shouldShow = null)
		{
			InitCheck();
			if (owner == null)
			{
				throw new ArgumentNullException("owner");
			}
			if (id == null)
			{
				throw new ArgumentNullException("id");
			}
			if (name == null)
			{
				throw new ArgumentNullException("name");
			}
			if (isCompatibleWithTarget == null)
			{
				throw new ArgumentNullException("isCompatibleWithTarget");
			}
			if (getValue == null)
			{
				throw new ArgumentNullException("getValue");
			}
			if (readValueFromXml == null)
			{
				throw new ArgumentNullException("readValueFromXml");
			}
			if (writeValueToXml == null)
			{
				throw new ArgumentNullException("writeValueToXml");
			}
			Delegate obj = null;
			if (interpolateBefore != null)
			{
				obj = Delegate.CreateDelegate(_interpolableDelegate, interpolateBefore.Target, interpolateBefore.Method);
			}
			Delegate obj2 = null;
			if (interpolateAfter != null)
			{
				obj2 = Delegate.CreateDelegate(_interpolableDelegate, interpolateAfter.Target, interpolateAfter.Method);
			}
			_addInterpolableModelStatic.Invoke(null, new object[16]
			{
				owner, id, parameter, name, obj, obj2, isCompatibleWithTarget, getValue, readValueFromXml, writeValueToXml,
				readParameterFromXml, writeParameterToXml, checkIntegrity, useOciInHash, getFinalName, shouldShow
			});
		}

		/// <summary>
		/// Adds an interpolableModel that targets CharaCustomFunctionControllers on characters to the available interpolables list. This interpolable can then be instantiated by the user by adding a keyframe.
		/// </summary>
		/// <typeparam name="TValue">The type of the value.</typeparam>
		/// <typeparam name="TController">Type of a plugin's <see cref="T:KKAPI.Chara.CharaCustomFunctionController" />.</typeparam>
		/// <inheritdoc cref="M:KKAPI.Utilities.TimelineCompatibility.AddInterpolableModelDynamic``2(System.String,System.String,System.String,KKAPI.Utilities.TimelineCompatibility.InterpolableDelegate{``0,``1},KKAPI.Utilities.TimelineCompatibility.InterpolableDelegate{``0,``1},System.Func{Studio.ObjectCtrlInfo,System.Boolean},System.Func{Studio.ObjectCtrlInfo,``1,``0},System.Func{``1,System.Xml.XmlNode,``0},System.Action{``1,System.Xml.XmlTextWriter,``0},System.Func{Studio.ObjectCtrlInfo,``1},System.Func{Studio.ObjectCtrlInfo,System.Xml.XmlNode,``1},System.Action{Studio.ObjectCtrlInfo,System.Xml.XmlTextWriter,``1},System.Func{Studio.ObjectCtrlInfo,``1,``0,``0,System.Boolean},System.Boolean,System.Func{System.String,Studio.ObjectCtrlInfo,``1,System.String},System.Func{Studio.ObjectCtrlInfo,``1,System.Boolean})" />
		public static void AddCharaFunctionInterpolable<TValue, TController>(string owner, string id, string name, InterpolableCharaDelegate<TValue, TController> interpolateBefore, InterpolableCharaDelegate<TValue, TController> interpolateAfter, Func<OCIChar, TController, TValue> getValue, Func<TController, XmlNode, TValue> readValueFromXml = null, Action<TController, XmlTextWriter, TValue> writeValueToXml = null, Func<OCIChar, TController, TValue, TValue, bool> checkIntegrity = null, bool useOciInHash = true, Func<string, OCIChar, TController, string> getFinalName = null, Func<OCIChar, TController, bool> shouldShow = null) where TController : CharaCustomFunctionController
		{
			if (getValue == null)
			{
				throw new ArgumentNullException("getValue");
			}
			AddInterpolableModelDynamic(owner, id, name, (interpolateBefore != null) ? ((InterpolableDelegate<TValue, TController>)delegate(ObjectCtrlInfo oci, TController parameter, TValue value, TValue rightValue, float factor)
			{
				interpolateBefore((OCIChar)oci, parameter, value, rightValue, factor);
			}) : null, (interpolateAfter != null) ? ((InterpolableDelegate<TValue, TController>)delegate(ObjectCtrlInfo oci, TController parameter, TValue value, TValue rightValue, float factor)
			{
				interpolateAfter((OCIChar)oci, parameter, value, rightValue, factor);
			}) : null, (ObjectCtrlInfo oci) => oci is OCIChar, (ObjectCtrlInfo oci, TController parameter) => getValue((OCIChar)oci, parameter), readValueFromXml, writeValueToXml, (ObjectCtrlInfo oci) => ((OCIChar)oci).GetChaControl().GetComponent<TController>(), null, null, (checkIntegrity != null) ? ((Func<ObjectCtrlInfo, TController, TValue, TValue, bool>)((ObjectCtrlInfo info, TController controller, TValue leftValue, TValue rightValue) => checkIntegrity((OCIChar)info, controller, leftValue, rightValue))) : null, useOciInHash, (getFinalName != null) ? ((Func<string, ObjectCtrlInfo, TController, string>)((string s, ObjectCtrlInfo info, TController param) => getFinalName(s, (OCIChar)info, param))) : null, (shouldShow != null) ? ((Func<ObjectCtrlInfo, TController, bool>)((ObjectCtrlInfo info, TController controller) => shouldShow((OCIChar)info, controller))) : null);
		}

		/// <summary>
		/// Refreshes the list of displayed interpolables. This function is quite heavy as it must go through each InterpolableModel and check if it's compatible with the current target.
		/// It is called automatically by Timeline when selecting another Workspace object or GuideObject.
		/// This triggers visibility checks of all interpolables and can be used to force update all instances of your interpolables if something important changes and Timeline doesn't notice it.
		/// </summary>
		public static void RefreshInterpolablesList()
		{
			InitCheck();
			if (_refreshInterpolablesList != null)
			{
				_refreshInterpolablesList();
			}
		}

		private static void InitCheck()
		{
			if (!_initialized)
			{
				throw new InvalidOperationException("TimelineCompatibility was not initialized. Use IsTimelineAvailable() first");
			}
			if (_interpolableDelegate == null)
			{
				if (StudioAPI.InsideStudio)
				{
					throw new InvalidOperationException("Timeline is not installed or an incompatible version is installed. Use IsTimelineAvailable() to check if it's available before using this API");
				}
				throw new InvalidOperationException("Timeline is only available inside studio. Use IsTimelineAvailable() to check if it's available before using this API");
			}
		}
	}
	/// <summary>
	/// Class that specifies how a setting should be displayed inside the ConfigurationManager settings window.
	///
	/// Usage:
	/// You can use this copy of the class instead of including it in your own plugin.
	/// Make a new instance, assign any fields that you want to override, and pass it as a tag for your setting.
	///
	/// If a field is null (default), it will be ignored and won't change how the setting is displayed.
	/// If a field is non-null (you assigned a value to it), it will override default behavior.
	/// </summary>
	///
	/// <example> 
	/// Here's an example of overriding order of settings and marking one of the settings as advanced:
	/// <code>
	/// // Override IsAdvanced and Order
	/// Config.Bind("X", "1", 1, new ConfigDescription("", null, new ConfigurationManagerAttributes { IsAdvanced = true, Order = 3 }));
	/// // Override only Order, IsAdvanced stays as the default value assigned by ConfigManager
	/// Config.Bind("X", "2", 2, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 1 }));
	/// Config.Bind("X", "3", 3, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 2 }));
	/// </code>
	/// </example>
	///
	/// <remarks> 
	/// You can read more and see examples in the readme at https://github.com/BepInEx/BepInEx.ConfigurationManager
	/// You can optionally remove fields that you won't use from this class, it's the same as leaving them null.
	/// </remarks>
	public sealed class ConfigurationManagerAttributes
	{
		/// <summary>
		/// Custom setting draw action that allows polling keyboard input with the Input class.
		/// Note: Make sure to focus on your UI control when you are accepting input so user doesn't type in the search box or in another setting (best to do this on every frame).
		/// If you don't draw any selectable UI controls You can use `GUIUtility.keyboardControl = -1;` on every frame to make sure that nothing is selected.
		/// </summary>
		/// <example>
		/// CustomHotkeyDrawer = (ConfigEntryBase setting, ref bool isEditing) =&gt;
		/// {
		///     if (isEditing)
		///     {
		///         // Make sure nothing else is selected since we aren't focusing on a text box with GUI.FocusControl.
		///         GUIUtility.keyboardControl = -1;
		///
		///         // Use Input.GetKeyDown and others here, remember to set isEditing to false after you're done!
		///         // It's best to check Input.anyKeyDown and set isEditing to false immediately if it's true,
		///         // so that the input doesn't have a chance to propagate to the game itself.
		///
		///         if (GUILayout.Button("Stop"))
		///             isEditing = false;
		///     }
		///     else
		///     {
		///         if (GUILayout.Button("Start"))
		///             isEditing = true;
		///     }
		///
		///     // This will only be true when isEditing is true and you hold any key
		///     GUILayout.Label("Any key pressed: " + Input.anyKey);
		/// }
		/// </example>
		/// <param name="setting">
		/// Setting currently being set (if available).
		/// </param>
		/// <param name="isCurrentlyAcceptingInput">
		/// Set this ref parameter to true when you want the current setting drawer to receive Input events.
		/// The value will persist after being set, use it to see if the current instance is being edited.
		/// Remember to set it to false after you are done!
		/// </param>
		public delegate void CustomHotkeyDrawerFunc(ConfigEntryBase setting, ref bool isCurrentlyAcceptingInput);

		/// <summary>
		/// Should the setting be shown as a percentage (only use with value range settings).
		/// </summary>
		public bool? ShowRangeAsPercent;

		/// <summary>
		/// Custom setting editor (OnGUI code that replaces the default editor provided by ConfigurationManager).
		/// See below for a deeper explanation. Using a custom drawer will cause many of the other fields to do nothing.
		/// </summary>
		public Action<ConfigEntryBase> CustomDrawer;

		/// <summary>
		/// Custom setting editor that allows polling keyboard input with the Input (or UnityInput) class.
		/// Use either CustomDrawer or CustomHotkeyDrawer, using both at the same time leads to undefined behaviour.
		/// </summary>
		public CustomHotkeyDrawerFunc CustomHotkeyDrawer;

		/// <summary>
		/// Show this setting in the settings screen at all? If false, don't show.
		/// </summary>
		public bool? Browsable;

		/// <summary>
		/// Category the setting is under. Null to be directly under the plugin.
		/// </summary>
		public string Category;

		/// <summary>
		/// If set, a "Default" button will be shown next to the setting to allow resetting to default.
		/// </summary>
		public object DefaultValue;

		/// <summary>
		/// Force the "Reset" button to not be displayed, even if a valid DefaultValue is available. 
		/// </summary>
		public bool? HideDefaultButton;

		/// <summary>
		/// Force the setting name to not be displayed. Should only be used with a <see cref="F:KKAPI.Utilities.ConfigurationManagerAttributes.CustomDrawer" /> to get more space.
		/// Can be used together with <see cref="F:KKAPI.Utilities.ConfigurationManagerAttributes.HideDefaultButton" /> to gain even more space.
		/// </summary>
		public bool? HideSettingName;

		/// <summary>
		/// Optional description shown when hovering over the setting.
		/// Not recommended, provide the description when creating the setting instead.
		/// </summary>
		public string Description;

		/// <summary>
		/// Name of the setting.
		/// </summary>
		public string DispName;

		/// <summary>
		/// Order of the setting on the settings list relative to other settings in a category.
		/// 0 by default, higher number is higher on the list.
		/// </summary>
		public int? Order;

		/// <summary>
		/// Only show the value, don't allow editing it.
		/// </summary>
		public bool? ReadOnly;

		/// <summary>
		/// If true, don't show the setting by default. User has to turn on showing advanced settings or search for it.
		/// </summary>
		public bool? IsAdvanced;

		/// <summary>
		/// Custom converter from setting type to string for the built-in editor textboxes.
		/// </summary>
		public Func<object, string> ObjToStr;

		/// <summary>
		/// Custom converter from string to setting type for the built-in editor textboxes.
		/// </summary>
		public Func<string, object> StrToObj;
	}
	/// <summary>
	/// Utility methods for working with coroutines.
	/// </summary>
	public static class CoroutineUtils
	{
		/// <summary>
		/// Cached WaitForEndOfFrame. Use instead of creating a new instance every time to reduce garbage production.
		/// </summary>
		public static readonly WaitForEndOfFrame WaitForEndOfFrame = new WaitForEndOfFrame();

		/// <summary>
		/// Create a coroutine that calls the appendCoroutine after baseCoroutine finishes
		/// </summary>
		public static IEnumerator AppendCo(this IEnumerator baseCoroutine, IEnumerator appendCoroutine)
		{
			return ComposeCoroutine(baseCoroutine, appendCoroutine);
		}

		/// <summary>
		/// Create a coroutine that calls the yieldInstruction after baseCoroutine finishes.
		/// Useless on its own, append further coroutines to run after this.
		/// </summary>
		public static IEnumerator AppendCo(this IEnumerator baseCoroutine, YieldInstruction yieldInstruction)
		{
			return new object[2] { baseCoroutine, yieldInstruction }.GetEnumerator();
		}

		/// <summary>
		/// Create a coroutine that calls each of the actions in order after base coroutine finishes.
		/// One action is called per frame. First action is called right after the coroutine finishes.
		/// </summary>
		public static IEnumerator AppendCo(this IEnumerator baseCoroutine, params Action[] actions)
		{
			return ComposeCoroutine(baseCoroutine, CreateCoroutine(actions));
		}

		/// <summary>
		/// Create a coroutine that calls each of the action delegates on consecutive frames.
		/// One action is called per frame. First action is called right away. There is no frame skip after the last action.
		/// </summary>
		public static IEnumerator CreateCoroutine(params Action[] actions)
		{
			if (actions == null)
			{
				throw new ArgumentNullException("actions");
			}
			bool first = true;
			foreach (Action action in actions)
			{
				if (first)
				{
					first = false;
				}
				else
				{
					yield return null;
				}
				action();
			}
		}

		/// <summary>
		/// Create a coroutine that calls each of the action delegates on consecutive frames.
		/// One action is called per frame. First action is called right after the yieldInstruction. There is no frame skip after the last action.
		/// </summary>
		public static IEnumerator CreateCoroutine(YieldInstruction yieldInstruction, params Action[] actions)
		{
			if (yieldInstruction == null)
			{
				throw new ArgumentNullException("yieldInstruction");
			}
			if (actions == null)
			{
				throw new ArgumentNullException("actions");
			}
			yield return yieldInstruction;
			yield return CreateCoroutine(actions);
		}

		/// <summary>
		/// Create a coroutine that calls each of the supplied coroutines in order.
		/// </summary>
		public static IEnumerator ComposeCoroutine(params IEnumerator[] coroutine)
		{
			return coroutine.GetEnumerator();
		}

		/// <summary>
		/// Create a coroutine that is the same as the supplied coroutine, except every time it yields the onYieldAction is invoked.
		/// (i.e. onYieldAction is invoked after every yield return in the original coroutine)
		/// If the coroutine returns another coroutine, the action is not called for yields performed by the returned coroutine, only the topmost one. Use FlattenCo if that's an issue.
		/// </summary>
		public static IEnumerator AttachToYield(this IEnumerator coroutine, Action onYieldAction)
		{
			if (coroutine == null)
			{
				throw new ArgumentNullException("coroutine");
			}
			if (onYieldAction == null)
			{
				throw new ArgumentNullException("onYieldAction");
			}
			while (coroutine.MoveNext())
			{
				onYieldAction();
				yield return coroutine.Current;
			}
		}

		/// <summary>
		/// Flatten the coroutine to yield all values directly. Any coroutines yield returned by this coroutine will have their values directly returned by this new coroutine (this is recursive).
		/// For example if another coroutine is yielded by this coroutine, the yielded coroutine will not be returned and instead the values that it yields will be returned.
		/// If a yielded coroutine yields yet another coroutine, that second coroutine's values will be returned directly from the flattened coroutine.
		/// </summary>
		public static IEnumerator FlattenCo(this IEnumerator coroutine)
		{
			if (coroutine == null)
			{
				throw new ArgumentNullException("coroutine");
			}
			while (coroutine.MoveNext())
			{
				object current = coroutine.Current;
				if (current is IEnumerator coroutine2)
				{
					IEnumerator flattenedSubCo = coroutine2.FlattenCo();
					while (flattenedSubCo.MoveNext())
					{
						yield return flattenedSubCo.Current;
					}
				}
				else
				{
					yield return current;
				}
			}
		}

		/// <summary>
		/// Remove yields from the coroutine, making its code run immediately.
		/// </summary>
		/// <param name="coroutine">Coroutine to strip</param>
		/// <param name="onlyStripNulls">Should only yield return null be stripped? If false, all yields are stripped</param>
		/// <param name="flatten">
		/// Should the coroutine be flattened before stripping it? 
		/// If this is false then yields from coroutines returned by this coroutine will not be stripped. 
		/// If this and onlyStripNulls are both false, coroutines returned by this coroutine will not be executed.
		/// </param>
		public static IEnumerator StripYields(this IEnumerator coroutine, bool onlyStripNulls = true, bool flatten = true)
		{
			if (coroutine == null)
			{
				throw new ArgumentNullException("coroutine");
			}
			if (flatten)
			{
				coroutine = coroutine.FlattenCo();
			}
			while (coroutine.MoveNext())
			{
				if (onlyStripNulls)
				{
					object current = coroutine.Current;
					if (current != null)
					{
						yield return current;
					}
				}
			}
		}

		/// <summary>
		/// Fully executes the coroutine synchronously (immediately run all of its code till completion).
		/// </summary>
		public static void RunImmediately(this IEnumerator coroutine)
		{
			coroutine = coroutine.FlattenCo();
			while (coroutine.MoveNext())
			{
			}
		}

		/// <summary>
		/// Prevent a coroutine from getting stopped by exceptions. Exceptions are caught and logged.
		/// Code after the exception is thrown doesn't run up until the next yield. The coroutine continues after the yield then.
		/// </summary>
		public static IEnumerator PreventFromCrashing(this IEnumerator coroutine)
		{
			if (coroutine == null)
			{
				throw new ArgumentNullException("coroutine");
			}
			while (true)
			{
				try
				{
					if (!coroutine.MoveNext())
					{
						break;
					}
				}
				catch (Exception exception)
				{
					UnityEngine.Debug.LogException(exception);
					break;
				}
				yield return coroutine.Current;
			}
		}

		/// <summary>
		/// Find the compiler-generated MoveNext method that contains the Coroutine/UniTask code. It can be used to apply transpliers to Coroutines and UniTasks.
		/// Note: When writing transpliers for coroutines you might want to turn off the "Decompiler\Decompile enumerators" setting in DnSpy so that you can see the real code.
		/// UniTasks are considered "async/await" so you need to turn off the "Decompile async methods" setting instead.
		/// </summary>
		public static MethodInfo GetMoveNext(MethodBase targetMethod)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Expected O, but got Unknown
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Expected O, but got Unknown
			ILContext val = new ILContext(new DynamicMethodDefinition(targetMethod).Definition);
			ILCursor val2 = new ILCursor(val);
			Type type;
			if (((MemberReference)((MethodReference)val2.Method).ReturnType).Name.StartsWith("UniTask"))
			{
				type = ReflectionHelper.ResolveReflection(((VariableReference)val2.Body.Variables[0]).VariableType);
				if (!type.Name.Contains(targetMethod.Name))
				{
					throw new ArgumentException("Unexpected type name " + type.Name + ", should contain " + targetMethod.Name);
				}
			}
			else
			{
				MethodReference enumeratorCtor = null;
				val2.GotoNext(new Func<Instruction, bool>[1]
				{
					(Instruction instruction) => ILPatternMatchingExt.MatchNewobj(instruction, ref enumeratorCtor)
				});
				if (enumeratorCtor == null)
				{
					throw new ArgumentNullException("enumeratorCtor");
				}
				if (((MemberReference)enumeratorCtor).Name != ".ctor")
				{
					throw new ArgumentException("Unexpected method name " + ((MemberReference)enumeratorCtor).Name + ", should be .ctor", "enumeratorCtor");
				}
				type = ReflectionHelper.ResolveReflection(((MemberReference)enumeratorCtor).DeclaringType);
			}
			MethodInfo method = type.GetMethod("MoveNext", AccessTools.all);
			if (method == null)
			{
				throw new ArgumentNullException("movenext");
			}
			KoikatuAPI.Logger.LogDebug((object)("GetMoveNext found [" + GeneralExtensions.FullDescription((MethodBase)method) + "] for [" + GeneralExtensions.FullDescription(targetMethod) + "]"));
			return method;
		}

		/// <summary>
		/// Used to patch coroutines/IEnumerator methods and async UniTask methods.
		/// This will method automatically find the compiler-generated MoveNext method that contains the coroutine code and apply patches on that. The method you patch must return an IEnumerator or an UniTask.
		/// Warning: Postfix patches will not work as expected, they might be fired after every iteration. Prefix is practically the same as prefixing the entry method. It's best to only use transpliers with this method.
		/// Note: When writing transpliers you might want to turn off the "Decompiler\Decompile enumerators/async" settings in DnSpy so that you can see the real code.
		/// </summary>
		/// <inheritdoc cref="M:HarmonyLib.Harmony.Patch(System.Reflection.MethodBase,HarmonyLib.HarmonyMethod,HarmonyLib.HarmonyMethod,HarmonyLib.HarmonyMethod,HarmonyLib.HarmonyMethod,HarmonyLib.HarmonyMethod)" />
		public static MethodInfo PatchMoveNext(this Harmony harmonyInstance, MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null, HarmonyMethod finalizer = null, HarmonyMethod ilmanipulator = null)
		{
			MethodInfo moveNext = GetMoveNext(original);
			return harmonyInstance.Patch((MethodBase)moveNext, prefix, postfix, transpiler, finalizer, ilmanipulator);
		}

		/// <summary>
		/// Create a coroutine that is the same as the supplied coroutine, but will stop early if <see cref="P:KKAPI.KoikatuAPI.IsQuitting" /> is <c>true</c>.
		/// If the coroutine returns another coroutine, the <see cref="P:KKAPI.KoikatuAPI.IsQuitting" /> check only runs on the topmost one. Use <see cref="M:KKAPI.Utilities.CoroutineUtils.FlattenCo(System.Collections.IEnumerator)" /> if that's an issue.
		/// </summary>
		public static IEnumerator StopCoOnQuit(this IEnumerator enumerator)
		{
			while (!KoikatuAPI.IsQuitting && enumerator.MoveNext())
			{
				yield return enumerator.Current;
			}
		}

		/// <summary>
		/// Create a coroutine that waits until the task completes.
		/// Use AppendCo to run code after the task has finished.
		/// </summary>
		public static IEnumerator WaitForFinishCo(this UniTask uniTask)
		{
			IUniTaskSource source = uniTask.source;
			if (source != null)
			{
				while (source.UnsafeGetStatus() > UniTaskStatus.Pending)
				{
					yield return null;
				}
			}
		}
	}
	/// <summary>
	/// Utility methods for working with H Scenes / main game.
	/// </summary>
	public static class HSceneUtils
	{
		/// <summary>
		/// Get the heroine that is currently in leading position in the h scene. 
		/// In 3P returns the heroine the cum options affect. Outside of 3P it gets the single heroine.
		/// </summary>
		public static Heroine GetLeadingHeroine(this HFlag hFlag)
		{
			if (hFlag == null)
			{
				throw new ArgumentNullException("hFlag");
			}
			return hFlag.lstHeroine[hFlag.GetLeadingHeroineId()];
		}

		/// <summary>
		/// Get the heroine that is currently in leading position in the h scene. 
		/// In 3P returns the heroine the cum options affect. Outside of 3P it gets the single heroine.
		/// </summary>
		public static Heroine GetLeadingHeroine(this HSprite hSprite)
		{
			if (hSprite == null)
			{
				throw new ArgumentNullException("hSprite");
			}
			return hSprite.flags.GetLeadingHeroine();
		}

		/// <summary>
		/// Get ID of the heroine that is currently in leading position in the h scene. 0 is the main heroine, 1 is the "tag along".
		/// In 3P returns the heroine the cum options affect. Outside of 3P it gets the single heroine.
		/// </summary>
		public static int GetLeadingHeroineId(this HFlag hFlag)
		{
			if (hFlag == null)
			{
				throw new ArgumentNullException("hFlag");
			}
			if (hFlag.mode != HFlag.EMode.houshi3P && hFlag.mode != HFlag.EMode.sonyu3P)
			{
				return 0;
			}
			return hFlag.nowAnimationInfo.id % 2;
		}

		/// <summary>
		/// Get ID of the heroine that is currently in leading position in the h scene. 0 is the main heroine, 1 is the "tag along".
		/// In 3P returns the heroine the cum options affect. Outside of 3P it gets the single heroine.
		/// </summary>
		public static int GetLeadingHeroineId(this HSprite hSprite)
		{
			if (hSprite == null)
			{
				throw new ArgumentNullException("hSprite");
			}
			return hSprite.flags.GetLeadingHeroineId();
		}

		/// <summary>
		/// Is current H mode penetration?
		/// </summary>
		public static bool IsSonyu(this HFlag hFlag)
		{
			if (hFlag.mode != HFlag.EMode.sonyu && hFlag.mode != HFlag.EMode.sonyu3P)
			{
				return hFlag.mode == HFlag.EMode.sonyu3PMMF;
			}
			return true;
		}

		/// <summary>
		/// Is current h mode service?
		/// </summary>
		public static bool IsHoushi(this HFlag hFlag)
		{
			if (hFlag.mode != HFlag.EMode.houshi && hFlag.mode != HFlag.EMode.houshi3P)
			{
				return hFlag.mode == HFlag.EMode.houshi3PMMF;
			}
			return true;
		}
	}
	/// <summary>
	/// Utility methods for working with IMGUI / OnGui.
	/// </summary>
	public static class IMGUIUtils
	{
		private static bool _resizeHandleClicked;

		private static Vector3 _resizeClickedPosition;

		private static Rect _resizeOriginalWindow;

		private static int _resizeCurrentWindowId;

		private static GUIStyle _tooltipStyle;

		private static GUIContent _tooltipContent;

		private static Texture2D _tooltipBackground;

		/// <summary>
		/// Empty GUILayoutOption array. You can use this instead of nothing to avoid allocations.
		/// For example: <code>GUILayout.Label("Hello world!", IMGUIUtils.EmptyLayoutOptions);</code>
		/// At that point you might also want to use GUIContent (created once and stored) instead of string.
		/// </summary>
		public static GUILayoutOption[] EmptyLayoutOptions = new GUILayoutOption[0];

		internal static bool ColorFilterAffectsImgui => false;

		/// <summary>
		/// A custom GUISkin with a solid background, sharper edges and less padding.
		/// The skin background color is adjusted to the game (if its color filter affects imgui layer).
		/// Warning: Only use inside OnGUI or things might break.
		/// </summary>
		public static GUISkin SolidBackgroundGuiSkin => InterfaceMaker.CustomSkin;

		private static Texture2D SolidBoxTex { get; set; }

		/// <summary>
		/// Draw a gray non-transparent GUI.Box at the specified rect. Use before a GUI.Window or other controls to get rid of 
		/// the default transparency and make the GUI easier to read.
		/// <example>
		/// IMGUIUtils.DrawSolidBox(screenRect);
		/// GUILayout.Window(362, screenRect, TreeWindow, "Select character folder");
		/// </example>
		/// </summary>
		public static void DrawSolidBox(Rect boxRect)
		{
			if (SolidBoxTex == null)
			{
				Texture2D texture2D = new Texture2D(1, 1, TextureFormat.ARGB32, mipChain: false);
				texture2D.SetPixel(0, 0, ColorFilterAffectsImgui ? new Color(0.84f, 0.84f, 0.84f) : new Color(0.4f, 0.4f, 0.4f));
				texture2D.Apply();
				SolidBoxTex = texture2D;
				UnityEngine.Object.DontDestroyOnLoad(texture2D);
			}
			GUI.Box(boxRect, GUIContent.none, new GUIStyle
			{
				normal = new GUIStyleState
				{
					background = SolidBoxTex
				}
			});
		}

		/// <summary>
		/// Block input from going through to the game/canvases if the mouse cursor is within the specified Rect.
		/// Use after a GUI.Window call or the window will not be able to get the inputs either.
		/// <example>
		/// GUILayout.Window(362, screenRect, TreeWindow, "Select character folder");
		/// Utils.EatInputInRect(screenRect);
		/// </example>
		/// </summary>
		/// <param name="eatRect"></param>
		public static void EatInputInRect(Rect eatRect)
		{
			if (eatRect.Contains(new Vector2(Input.mousePosition.x, (float)Screen.height - Input.mousePosition.y)))
			{
				Input.ResetInputAxes();
			}
		}

		/// <summary>
		/// Draw a label with an outline
		/// </summary>
		/// <param name="rect">Size of the control</param>
		/// <param name="text">Text of the label</param>
		/// <param name="style">Style to be applied to the label</param>
		/// <param name="txtColor">Color of the text</param>
		/// <param name="outlineColor">Color of the outline</param>
		/// <param name="outlineThickness">Thickness of the outline in pixels</param>
		public static void DrawLabelWithOutline(Rect rect, string text, GUIStyle style, Color txtColor, Color outlineColor, int outlineThickness)
		{
			Color textColor = style.normal.textColor;
			Color color = GUI.color;
			style.normal.textColor = outlineColor;
			GUI.color = outlineColor;
			Rect position = rect;
			rect.x -= outlineThickness;
			rect.y -= outlineThickness;
			while (rect.x++ < position.x + (float)outlineThickness)
			{
				GUI.Label(rect, text, style);
			}
			rect.x--;
			while (rect.y++ < position.y + (float)outlineThickness)
			{
				GUI.Label(rect, text, style);
			}
			rect.y--;
			while (rect.x-- > position.x - (float)outlineThickness)
			{
				GUI.Label(rect, text, style);
			}
			rect.x++;
			while (rect.y-- > position.y - (float)outlineThickness)
			{
				GUI.Label(rect, text, style);
			}
			style.normal.textColor = txtColor;
			GUI.color = txtColor;
			GUI.Label(position, text, style);
			style.normal.textColor = textColor;
			GUI.color = color;
		}

		/// <summary>
		/// Draw a label with a shadow
		/// </summary>        
		/// <param name="rect">Size of the control</param>
		/// <param name="content">Contents of the label</param>
		/// <param name="style">Style to be applied to the label</param>
		/// <param name="txtColor">Color of the outline</param>
		/// <param name="shadowColor">Color of the text</param>
		/// <param name="shadowOffset">Offset of the shadow in pixels</param>
		public static void DrawLabelWithShadow(Rect rect, GUIContent content, GUIStyle style, Color txtColor, Color shadowColor, Vector2 shadowOffset)
		{
			Color textColor = style.normal.textColor;
			style.normal.textColor = shadowColor;
			rect.x += shadowOffset.x;
			rect.y += shadowOffset.y;
			GUI.Label(rect, content, style);
			style.normal.textColor = txtColor;
			rect.x -= shadowOffset.x;
			rect.y -= shadowOffset.y;
			GUI.Label(rect, content, style);
			style.normal.textColor = textColor;
		}

		/// <inheritdoc cref="M:KKAPI.Utilities.IMGUIUtils.DrawLabelWithShadow(UnityEngine.Rect,UnityEngine.GUIContent,UnityEngine.GUIStyle,UnityEngine.Color,UnityEngine.Color,UnityEngine.Vector2)" />
		public static void DrawLayoutLabelWithShadow(GUIContent content, GUIStyle style, Color txtColor, Color shadowColor, Vector2 direction, params GUILayoutOption[] options)
		{
			DrawLabelWithShadow(GUILayoutUtility.GetRect(content, style, options), content, style, txtColor, shadowColor, direction);
		}

		/// <inheritdoc cref="M:KKAPI.Utilities.IMGUIUtils.DrawLabelWithShadow(UnityEngine.Rect,UnityEngine.GUIContent,UnityEngine.GUIStyle,UnityEngine.Color,UnityEngine.Color,UnityEngine.Vector2)" />
		public static bool DrawButtonWithShadow(Rect r, GUIContent content, GUIStyle style, float shadowAlpha, Vector2 direction)
		{
			GUIStyle gUIStyle = new GUIStyle(style);
			gUIStyle.normal.background = null;
			gUIStyle.hover.background = null;
			gUIStyle.active.background = null;
			bool result = GUI.Button(r, content, style);
			Color txtColor = (r.Contains(Event.current.mousePosition) ? gUIStyle.hover.textColor : gUIStyle.normal.textColor);
			DrawLabelWithShadow(r, content, gUIStyle, txtColor, new Color(0f, 0f, 0f, shadowAlpha), direction);
			return result;
		}

		/// <inheritdoc cref="M:KKAPI.Utilities.IMGUIUtils.DrawLabelWithShadow(UnityEngine.Rect,UnityEngine.GUIContent,UnityEngine.GUIStyle,UnityEngine.Color,UnityEngine.Color,UnityEngine.Vector2)" />
		public static bool DrawLayoutButtonWithShadow(GUIContent content, GUIStyle style, float shadowAlpha, Vector2 direction, params GUILayoutOption[] options)
		{
			return DrawButtonWithShadow(GUILayoutUtility.GetRect(content, style, options), content, style, shadowAlpha, direction);
		}

		/// <summary>
		/// Handle both dragging and resizing of OnGUI windows.
		/// Use this instead of GUI.DragWindow(), don't use both at the same time.
		/// To use, place this at the end of your Window method: _windowRect = IMGUIUtils.DragResizeWindow(windowId, _windowRect);
		/// </summary>
		/// <param name="windowId">The ID passed to your window method</param>
		/// <param name="windowRect">The rect of your window. Make sure to set it to the result of this method</param>
		public static Rect DragResizeWindow(int windowId, Rect windowRect)
		{
			GUI.Box(new Rect(windowRect.width - 13f, windowRect.height - 13f, 13f, 13f), GUIContent.none);
			if (_resizeCurrentWindowId != 0 && _resizeCurrentWindowId != windowId)
			{
				return windowRect;
			}
			Vector3 mousePosition = Input.mousePosition;
			mousePosition.y = (float)Screen.height - mousePosition.y;
			Rect rect = windowRect;
			Rect rect2 = new Rect(rect.x + rect.width - 25f, rect.y + rect.height - 25f, 25f, 25f);
			if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect2.Contains(mousePosition))
			{
				_resizeHandleClicked = true;
				_resizeClickedPosition = mousePosition;
				_resizeOriginalWindow = rect;
				_resizeCurrentWindowId = windowId;
			}
			if (_resizeHandleClicked)
			{
				Rect rect3 = rect;
				rect3.width = Mathf.Clamp(_resizeOriginalWindow.width + (mousePosition.x - _resizeClickedPosition.x), 100f, Screen.width);
				rect3.height = Mathf.Clamp(_resizeOriginalWindow.height + (mousePosition.y - _resizeClickedPosition.y), 100f, Screen.height);
				windowRect = rect3;
				if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
				{
					_resizeHandleClicked = false;
					_resizeCurrentWindowId = 0;
				}
			}
			else
			{
				GUI.DragWindow();
			}
			return windowRect;
		}

		/// <summary>
		/// Handle both dragging and resizing of OnGUI windows, as well as eat mouse inputs when cursor is over the window.
		/// Use this instead of <see cref="M:UnityEngine.GUI.DragWindow(UnityEngine.Rect)" /> and <see cref="M:KKAPI.Utilities.IMGUIUtils.EatInputInRect(UnityEngine.Rect)" />. Don't use these methods at the same time as DragResizeEatWindow.
		/// To use, place this at the end of your Window method: _windowRect = IMGUIUtils.DragResizeEatWindow(windowId, _windowRect);
		/// </summary>
		/// <param name="windowId">The ID passed to your window method</param>
		/// <param name="windowRect">The rect of your window. Make sure to set it to the result of this method</param>
		public static Rect DragResizeEatWindow(int windowId, Rect windowRect)
		{
			Rect rect = DragResizeWindow(windowId, windowRect);
			EatInputInRect(rect);
			return rect;
		}

		/// <summary>
		/// Display a tooltip for any GUIContent with the tootlip property set in a given window.
		/// To use, place this at the end of your Window method: IMGUIUtils.DrawTooltip(_windowRect);
		/// </summary>
		/// <param name="area">Area where the tooltip can appear</param>
		/// <param name="tooltipWidth">Minimum width of the tooltip, can't be larger than area's width</param>
		public static void DrawTooltip(Rect area, int tooltipWidth = 400)
		{
			if (!string.IsNullOrEmpty(GUI.tooltip))
			{
				if (_tooltipBackground == null)
				{
					_tooltipBackground = new Texture2D(1, 1, TextureFormat.ARGB32, mipChain: false);
					_tooltipBackground.SetPixel(0, 0, Color.black);
					_tooltipBackground.Apply();
					_tooltipStyle = new GUIStyle
					{
						normal = new GUIStyleState
						{
							textColor = Color.white,
							background = _tooltipBackground
						},
						wordWrap = true,
						alignment = TextAnchor.MiddleCenter
					};
					_tooltipContent = new GUIContent();
				}
				string[] source = GUI.tooltip.Split('\n');
				string text = source.OrderByDescending((string l) => l.Length).First();
				_tooltipContent.text = text;
				_tooltipStyle.CalcMinMaxWidth(_tooltipContent, out var _, out var maxWidth);
				int num = (int)area.width;
				if (maxWidth > (float)num)
				{
					maxWidth = num;
				}
				if (tooltipWidth > num)
				{
					tooltipWidth = num;
				}
				_tooltipContent.text = GUI.tooltip;
				float num2 = _tooltipStyle.CalcHeight(_tooltipContent, tooltipWidth) + 10f;
				float num3 = num2 / area.height;
				float num4 = (float)num * (num3 + 0.05f);
				if (num4 > (float)tooltipWidth)
				{
					tooltipWidth = Mathf.Min((int)maxWidth, (int)num4);
					num2 = _tooltipStyle.CalcHeight(_tooltipContent, tooltipWidth) + 10f;
				}
				Event current = Event.current;
				float x = ((current.mousePosition.x + (float)tooltipWidth > area.width) ? (area.width - (float)tooltipWidth) : current.mousePosition.x);
				float y = ((current.mousePosition.y + 25f + num2 > area.height) ? (current.mousePosition.y - num2 - 15f) : (current.mousePosition.y + 25f));
				GUI.Box(new Rect(x, y, tooltipWidth, num2), GUI.tooltip, _tooltipStyle);
			}
		}
	}
	internal static class InterfaceMaker
	{
		private static Texture2D _boxBackground;

		private static Texture2D _winBackground;

		private static GUISkin _customSkin;

		public static GUISkin CustomSkin
		{
			get
			{
				if (_customSkin == null)
				{
					try
					{
						KoikatuAPI.Logger.LogDebug((object)"Instantiating custom GUISkin");
						_customSkin = CreateSkin(IMGUIUtils.ColorFilterAffectsImgui);
					}
					catch (Exception ex)
					{
						KoikatuAPI.Logger.LogWarning((object)("Could not load custom GUISkin - " + ex.Message));
						_customSkin = GUI.skin;
					}
				}
				return _customSkin;
			}
		}

		private static GUISkin CreateSkin(bool lightVersion)
		{
			GUISkin gUISkin = UnityEngine.Object.Instantiate(GUI.skin);
			UnityEngine.Object.DontDestroyOnLoad(gUISkin);
			_boxBackground = ResourceUtils.GetEmbeddedResource(lightVersion ? "guisharp-box-light.png" : "guisharp-box.png").LoadTexture();
			UnityEngine.Object.DontDestroyOnLoad(_boxBackground);
			gUISkin.box.onNormal.background = null;
			gUISkin.box.normal.background = _boxBackground;
			gUISkin.box.normal.textColor = Color.white;
			_winBackground = ResourceUtils.GetEmbeddedResource(lightVersion ? "guisharp-window-light.png" : "guisharp-window.png").LoadTexture();
			UnityEngine.Object.DontDestroyOnLoad(_winBackground);
			gUISkin.window.onNormal.background = null;
			gUISkin.window.normal.background = _winBackground;
			gUISkin.window.padding = new RectOffset(6, 6, 22, 6);
			gUISkin.window.border = new RectOffset(10, 10, 20, 10);
			gUISkin.window.normal.textColor = Color.white;
			gUISkin.button.padding = new RectOffset(4, 4, 3, 3);
			gUISkin.button.normal.textColor = Color.white;
			gUISkin.textField.normal.textColor = Color.white;
			gUISkin.label.normal.textColor = Color.white;
			return gUISkin;
		}
	}
	/// <summary>
	/// Provides information about system memory status
	/// </summary>
	public static class MemoryInfo
	{
		/// <summary>
		/// contains information about the current state of both physical and virtual memory, including extended memory
		/// </summary>
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
		public class MEMORYSTATUSEX
		{
			/// <summary>
			/// Size of the structure, in bytes. You must set this member before calling GlobalMemoryStatusEx.
			/// </summary>
			public uint dwLength;

			/// <summary>
			/// Number between 0 and 100 that specifies the approximate percentage of physical memory that is in use (0 indicates no memory use and 100 indicates full memory use).
			/// </summary>
			public uint dwMemoryLoad;

			/// <summary>
			/// Total size of physical memory, in bytes.
			/// </summary>
			public ulong ullTotalPhys;

			/// <summary>
			/// Size of physical memory available, in bytes.
			/// </summary>
			public ulong ullAvailPhys;

			/// <summary>
			/// Size of the committed memory limit, in bytes. This is physical memory plus the size of the page file, minus a small overhead.
			/// </summary>
			public ulong ullTotalPageFile;

			/// <summary>
			/// Size of available memory to commit, in bytes. The limit is ullTotalPageFile.
			/// </summary>
			public ulong ullAvailPageFile;

			/// <summary>
			/// Total size of the user mode portion of the virtual address space of the calling process, in bytes.
			/// </summary>
			public ulong ullTotalVirtual;

			/// <summary>
			/// Size of unreserved and uncommitted memory in the user mode portion of the virtual address space of the calling process, in bytes.
			/// </summary>
			public ulong ullAvailVirtual;

			/// <summary>
			/// Size of unreserved and uncommitted memory in the extended portion of the virtual address space of the calling process, in bytes.
			/// </summary>
			public ulong ullAvailExtendedVirtual;

			/// <summary>
			/// Initializes a new instance of the <see cref="T:MEMORYSTATUSEX" /> class.
			/// </summary>
			public MEMORYSTATUSEX()
			{
				dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
			}
		}

		/// <summary>
		/// Can return null if the call fails for whatever reason
		/// </summary>
		public static MEMORYSTATUSEX GetCurrentStatus()
		{
			try
			{
				MEMORYSTATUSEX mEMORYSTATUSEX = new MEMORYSTATUSEX();
				if (GlobalMemoryStatusEx(mEMORYSTATUSEX))
				{
					return mEMORYSTATUSEX;
				}
				return null;
			}
			catch (Exception value)
			{
				Console.WriteLine(value);
				return null;
			}
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GlobalMemoryStatusEx([In][Out] MEMORYSTATUSEX lpBuffer);
	}
	/// <summary>
	/// Additions to the UniRx IObservable extension methods
	/// </summary>
	public static class ObservableExtensions
	{
		/// <summary>
		/// Get an observable that triggers on every OnGUI call on this gameObject
		/// </summary>
		public static IObservable<Unit> OnGUIAsObservable(this UnityEngine.Component component)
		{
			if (!(component == null))
			{
				return component.GetOrAddComponent<ObservableOnGUITrigger>().OnGUIAsObservable();
			}
			return Observable.Empty<Unit>();
		}

		/// <inheritdoc cref="M:KKAPI.Utilities.ObservableExtensions.OnGUIAsObservable(UnityEngine.Component)" />
		public static IObservable<Unit> OnGUIAsObservable(this Transform transform)
		{
			if (!(transform == null))
			{
				return transform.GetOrAddComponent<ObservableOnGUITrigger>().OnGUIAsObservable();
			}
			return Observable.Empty<Unit>();
		}

		/// <inheritdoc cref="M:KKAPI.Utilities.ObservableExtensions.OnGUIAsObservable(UnityEngine.Component)" />
		public static IObservable<Unit> OnGUIAsObservable(this GameObject gameObject)
		{
			if (!(gameObject == null))
			{
				return gameObject.GetOrAddComponent<ObservableOnGUITrigger>().OnGUIAsObservable();
			}
			return Observable.Empty<Unit>();
		}
	}
	/// <summary>
	/// Trigger component that implements <see cref="M:KKAPI.Utilities.ObservableExtensions.OnGUIAsObservable(UnityEngine.Component)" />
	/// </summary>
	[DisallowMultipleComponent]
	public class ObservableOnGUITrigger : ObservableTriggerBase
	{
		private Subject<Unit> _onGui;

		private void OnGUI()
		{
			_onGui?.OnNext(Unit.Default);
		}

		/// <summary>
		/// Get observable that triggers every time this component's OnGUI is called
		/// </summary>
		public IObservable<Unit> OnGUIAsObservable()
		{
			return _onGui ?? (_onGui = new Subject<Unit>());
		}

		/// <inheritdoc />
		public override void RaiseOnCompletedOnDestroy()
		{
			_onGui?.OnCompleted();
		}
	}
	/// <summary>
	/// Read-only dictionary wrapper. Will protect the base dictionary from being changed.
	/// Warning: Any reference types inside the dictionary can still be modified.
	/// </summary>
	/// <inheritdoc />
	public class ReadOnlyDictionary<TKey, TValue> : IDictionary<TKey, TValue>, ICollection<KeyValuePair<TKey, TValue>>, IEnumerable<KeyValuePair<TKey, TValue>>, IEnumerable
	{
		private readonly IDictionary<TKey, TValue> _dictionary;

		public ICollection<TKey> Keys => _dictionary.Keys;

		public ICollection<TValue> Values => _dictionary.Values;

		public TValue this[TKey key] => _dictionary[key];

		TValue IDictionary<TKey, TValue>.this[TKey key]
		{
			get
			{
				return this[key];
			}
			set
			{
				throw ReadOnlyException();
			}
		}

		public int Count => _dictionary.Count;

		public bool IsReadOnly => true;

		/// <summary>
		/// Create a new wrapper around the specified dictionary
		/// </summary>
		public ReadOnlyDictionary(IDictionary<TKey, TValue> dictionary)
		{
			_dictionary = dictionary;
		}

		void IDictionary<TKey, TValue>.Add(TKey key, TValue value)
		{
			throw ReadOnlyException();
		}

		public bool ContainsKey(TKey key)
		{
			return _dictionary.ContainsKey(key);
		}

		bool IDictionary<TKey, TValue>.Remove(TKey key)
		{
			throw ReadOnlyException();
		}

		public bool TryGetValue(TKey key, out TValue value)
		{
			return _dictionary.TryGetValue(key, out value);
		}

		void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
		{
			throw ReadOnlyException();
		}

		void ICollection<KeyValuePair<TKey, TValue>>.Clear()
		{
			throw ReadOnlyException();
		}

		public bool Contains(KeyValuePair<TKey, TValue> item)
		{
			return _dictionary.Contains(item);
		}

		public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
		{
			_dictionary.CopyTo(array, arrayIndex);
		}

		bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
		{
			throw ReadOnlyException();
		}

		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
		{
			return _dictionary.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		private static Exception ReadOnlyException()
		{
			return new NotSupportedException("This dictionary is read-only");
		}
	}
	/// <summary>
	/// Utility methods for working with embedded resources.
	/// </summary>
	public static class ResourceUtils
	{
		/// <summary>
		/// Read all bytes starting at current position and ending at the end of the stream.
		/// </summary>
		public static byte[] ReadAllBytes(this Stream input)
		{
			byte[] array = new byte[16384];
			using MemoryStream memoryStream = new MemoryStream();
			int count;
			while ((count = input.Read(array, 0, array.Length)) > 0)
			{
				memoryStream.Write(array, 0, count);
			}
			return memoryStream.ToArray();
		}

		/// <summary>
		/// Get a file set as "Embedded Resource" from the assembly that is calling this code, or optionally from a specified assembly.
		/// The filename is matched to the end of the resource path, no need to give the full path.
		/// If 0 or more than 1 resources match the provided filename, an exception is thrown.
		/// For example if you have a file "ProjectRoot\Resources\icon.png" set as "Embedded Resource", you can use this to load it by
		/// doing <code>GetEmbeddedResource("icon.png"), assuming that no other embedded files have the same name.</code>
		/// </summary>
		/// <exception cref="T:System.IO.IOException">Thrown if none or more than one resources were found matching the given resourceFileName</exception>
		public static byte[] GetEmbeddedResource(string resourceFileName, Assembly containingAssembly = null)
		{
			bool flag = false;
			if (containingAssembly == null)
			{
				containingAssembly = Assembly.GetCallingAssembly();
				flag = true;
			}
			List<string> list = (from str in containingAssembly.GetManifestResourceNames()
				where str.EndsWith(resourceFileName)
				select str).Take(2).ToList();
			if (list.Count == 0 && flag)
			{
				Assembly assembly = containingAssembly;
				containingAssembly = new StackFrame(1).GetMethod().DeclaringType.Assembly;
				if (assembly != containingAssembly)
				{
					list = (from str in containingAssembly.GetManifestResourceNames()
						where str.EndsWith(resourceFileName)
						select str).Take(2).ToList();
					if (list.Count == 0)
					{
						throw new IOException($"Could not find resource with name {resourceFileName} inside assembly {containingAssembly} or {assembly} - make sure the name and assembly are correct. Two assemblies were checked likely because your method has been harmony patched");
					}
				}
			}
			if (list.Count == 0)
			{
				throw new IOException($"Could not find resource with name {list} inside assembly {containingAssembly} - make sure the name and assembly are correct");
			}
			if (list.Count == 2)
			{
				throw new IOException($"Found more than one resource with name {list} inside assembly {containingAssembly} - include more of the path in the name to make it not ambiguous");
			}
			using Stream stream = containingAssembly.GetManifestResourceStream(list[0]);
			return (stream ?? throw new InvalidOperationException($"The resource {resourceFileName} was not found inside assembly {containingAssembly} or it failed to load")).ReadAllBytes();
		}
	}
	/// <summary>
	/// Utility methods for working with texture objects.
	/// </summary>
	public static class TextureUtils
	{
		/// <summary>
		/// Filtering to use when resizing a texture
		/// </summary>
		public enum ImageFilterMode
		{
			/// <summary>
			/// Nearest pixel, fastest but gets aliased
			/// </summary>
			Nearest,
			/// <summary>
			/// Use bilinear scaling
			/// </summary>
			Biliner,
			/// <summary>
			/// Average of all nearby pixels
			/// </summary>
			Average
		}

		/// <summary>
		/// Copy this texture inside a new editable Texture2D.
		/// </summary>
		/// <param name="tex">Texture to copy</param>
		/// <param name="format">Format of the copy</param>
		/// <param name="mipMaps">Copy has mipmaps</param>
		public static Texture2D ToTexture2D(this Texture tex, TextureFormat format = TextureFormat.ARGB32, bool mipMaps = false)
		{
			RenderTexture temporary = RenderTexture.GetTemporary(tex.width, tex.height);
			RenderTexture active = RenderTexture.active;
			RenderTexture.active = temporary;
			GL.Clear(clearDepth: true, clearColor: true, Color.clear);
			Graphics.Blit(tex, temporary);
			Texture2D texture2D = new Texture2D(tex.width, tex.height, format, mipMaps);
			texture2D.ReadPixels(new Rect(0f, 0f, tex.width, tex.height), 0, 0);
			texture2D.Apply(updateMipmaps: false);
			RenderTexture.active = active;
			RenderTexture.ReleaseTemporary(temporary);
			return texture2D;
		}

		/// <summary>
		/// Create texture from an image stored in a byte array, for example a png file read from disk.
		/// </summary>
		public static Texture2D LoadTexture(this byte[] texBytes, TextureFormat format = TextureFormat.ARGB32, bool mipMaps = false)
		{
			if (texBytes == null)
			{
				throw new ArgumentNullException("texBytes");
			}
			Texture2D texture2D = new Texture2D(2, 2, format, mipMaps);
			texture2D.LoadImage(texBytes);
			return texture2D;
		}

		/// <summary>
		/// Create a sprite based on this texture.
		/// </summary>
		public static Sprite ToSprite(this Texture2D texture)
		{
			if (texture == null)
			{
				throw new ArgumentNullException("texture");
			}
			return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
		}

		/// <summary>
		/// Create a resized copy of this texture.
		/// http://blog.collectivemass.com/2014/03/resizing-textures-in-unity/
		/// </summary>
		/// <param name="pSource">Texture to resize</param>
		/// <param name="pFilterMode">How to resize</param>
		/// <param name="pScale">New scale</param>
		public static Texture2D ResizeTexture(this Texture2D pSource, ImageFilterMode pFilterMode, float pScale)
		{
			Color[] pixels = pSource.GetPixels(0);
			Vector2 vector = new Vector2(pSource.width, pSource.height);
			float num = Mathf.RoundToInt((float)pSource.width * pScale);
			float num2 = Mathf.RoundToInt((float)pSource.height * pScale);
			Texture2D texture2D = new Texture2D((int)num, (int)num2, TextureFormat.RGBA32, mipChain: false);
			int num3 = (int)num * (int)num2;
			Color[] array = new Color[num3];
			Vector2 vector2 = new Vector2(vector.x / num, vector.y / num2);
			Vector2 vector3 = default(Vector2);
			for (int i = 0; i < num3; i++)
			{
				float num4 = (float)i % num;
				float num5 = Mathf.Floor((float)i / num);
				vector3.x = num4 / num * vector.x;
				vector3.y = num5 / num2 * vector.y;
				switch (pFilterMode)
				{
				case ImageFilterMode.Nearest:
				{
					vector3.x = Mathf.Round(vector3.x);
					vector3.y = Mathf.Round(vector3.y);
					int num15 = (int)(vector3.y * vector.x + vector3.x);
					array[i] = pixels[num15];
					break;
				}
				case ImageFilterMode.Biliner:
				{
					float t = vector3.x - Mathf.Floor(vector3.x);
					float t2 = vector3.y - Mathf.Floor(vector3.y);
					int num11 = (int)(Mathf.Floor(vector3.y) * vector.x + Mathf.Floor(vector3.x));
					int num12 = (int)(Mathf.Floor(vector3.y) * vector.x + Mathf.Ceil(vector3.x));
					int num13 = (int)(Mathf.Ceil(vector3.y) * vector.x + Mathf.Floor(vector3.x));
					int num14 = (int)(Mathf.Ceil(vector3.y) * vector.x + Mathf.Ceil(vector3.x));
					array[i] = Color.Lerp(Color.Lerp(pixels[num11], pixels[num12], t), Color.Lerp(pixels[num13], pixels[num14], t), t2);
					break;
				}
				case ImageFilterMode.Average:
				{
					int num6 = (int)Mathf.Max(Mathf.Floor(vector3.x - vector2.x * 0.5f), 0f);
					int num7 = (int)Mathf.Min(Mathf.Ceil(vector3.x + vector2.x * 0.5f), vector.x);
					int num8 = (int)Mathf.Max(Mathf.Floor(vector3.y - vector2.y * 0.5f), 0f);
					int num9 = (int)Mathf.Min(Mathf.Ceil(vector3.y + vector2.y * 0.5f), vector.y);
					Color color = default(Color);
					float num10 = 0f;
					for (int j = num8; j < num9; j++)
					{
						for (int k = num6; k < num7; k++)
						{
							color += pixels[(int)((float)j * vector.x + (float)k)];
							num10 += 1f;
						}
					}
					array[i] = color / num10;
					break;
				}
				}
			}
			texture2D.SetPixels(array);
			texture2D.Apply();
			return texture2D;
		}

		/// <summary>
		/// Gets texture as it is shown by this sprite. If it's not packed then returns the original texture.
		/// If it's packed then this tries to crop out the part that the sprite is supposed to show and return only that.
		/// </summary>
		public static Texture2D GetVisibleTexture(this Sprite spr)
		{
			if (spr.packed && spr.packingMode != SpritePackingMode.Tight)
			{
				Texture2D texture2D = spr.texture.ToTexture2D();
				int num = (int)spr.textureRect.width;
				int num2 = (int)spr.textureRect.height;
				Color[] pixels = texture2D.GetPixels((int)spr.textureRect.x, (int)spr.textureRect.y, num, num2);
				UnityEngine.Object.Destroy(texture2D);
				Texture2D texture2D2 = new Texture2D(num, num2);
				texture2D2.SetPixels(pixels);
				texture2D2.Apply();
				return texture2D2;
			}
			return spr.texture;
		}
	}
	/// <summary>
	/// Utility methods for working with text.
	/// </summary>
	public static class TextUtils
	{
		/// <summary>
		/// Convert PascalCase to Sentence case.
		/// </summary>
		public static string PascalCaseToSentenceCase(this string str)
		{
			if (str == null)
			{
				throw new ArgumentNullException("str");
			}
			return Regex.Replace(str, "[a-z][A-Z]", (Match m) => $"{m.Value[0]} {char.ToLower(m.Value[1])}");
		}
	}
	/// <summary>
	/// Gives access to the Windows open file dialog.
	/// http://www.pinvoke.net/default.aspx/comdlg32/GetOpenFileName.html
	/// http://www.pinvoke.net/default.aspx/Structures/OpenFileName.html
	/// http://www.pinvoke.net/default.aspx/Enums/OpenSaveFileDialgueFlags.html
	/// https://social.msdn.microsoft.com/Forums/en-US/2f4dd95e-5c7b-4f48-adfc-44956b350f38/getopenfilename-for-multiple-files?forum=csharpgeneral
	/// </summary>
	public class OpenFileDialog
	{
		private static class NativeMethods
		{
			[DllImport("comdlg32.dll", CharSet = CharSet.Unicode)]
			public static extern bool GetOpenFileName([In][Out] OpenFileName ofn);

			[DllImport("user32.dll")]
			public static extern IntPtr GetActiveWindow();
		}

		[Flags]
		public enum OpenSaveFileDialgueFlags
		{
			OFN_READONLY = 1,
			OFN_OVERWRITEPROMPT = 2,
			OFN_HIDEREADONLY = 4,
			OFN_NOCHANGEDIR = 8,
			OFN_SHOWHELP = 0x10,
			OFN_ENABLEHOOK = 0x20,
			OFN_ENABLETEMPLATE = 0x40,
			OFN_ENABLETEMPLATEHANDLE = 0x80,
			OFN_NOVALIDATE = 0x100,
			OFN_ALLOWMULTISELECT = 0x200,
			OFN_EXTENSIONDIFFERENT = 0x400,
			OFN_PATHMUSTEXIST = 0x800,
			OFN_FILEMUSTEXIST = 0x1000,
			OFN_CREATEPROMPT = 0x2000,
			OFN_SHAREAWARE = 0x4000,
			OFN_NOREADONLYRETURN = 0x8000,
			OFN_NOTESTFILECREATE = 0x10000,
			OFN_NONETWORKBUTTON = 0x20000,
			/// <summary>
			/// Force no long names for 4.x modules
			/// </summary>
			OFN_NOLONGNAMES = 0x40000,
			/// <summary>
			/// New look commdlg
			/// </summary>
			OFN_EXPLORER = 0x80000,
			OFN_NODEREFERENCELINKS = 0x100000,
			/// <summary>
			/// Force long names for 3.x modules
			/// </summary>
			OFN_LONGNAMES = 0x200000
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		private class OpenFileName
		{
			public int structSize;

			public IntPtr dlgOwner = IntPtr.Zero;

			public IntPtr instance = IntPtr.Zero;

			public string filter;

			public string customFilter;

			public int maxCustFilter;

			public int filterIndex;

			public IntPtr file;

			public int maxFile;

			public string fileTitle;

			public int maxFileTitle;

			public string initialDir;

			public string title;

			public int flags;

			public short fileOffset;

			public short fileExtension;

			public string defExt;

			public IntPtr custData = IntPtr.Zero;

			public IntPtr hook = IntPtr.Zero;

			public string templateName;

			public IntPtr reservedPtr = IntPtr.Zero;

			public int reservedInt;

			public int flagsEx;
		}

		/// <summary>
		/// Arguments used for opening a single file
		/// </summary>
		public const OpenSaveFileDialgueFlags SingleFileFlags = OpenSaveFileDialgueFlags.OFN_NOCHANGEDIR | OpenSaveFileDialgueFlags.OFN_FILEMUSTEXIST | OpenSaveFileDialgueFlags.OFN_EXPLORER | OpenSaveFileDialgueFlags.OFN_LONGNAMES;

		/// <summary>
		/// Arguments used for opening multiple files
		/// </summary>
		public const OpenSaveFileDialgueFlags MultiFileFlags = OpenSaveFileDialgueFlags.OFN_NOCHANGEDIR | OpenSaveFileDialgueFlags.OFN_ALLOWMULTISELECT | OpenSaveFileDialgueFlags.OFN_FILEMUSTEXIST | OpenSaveFileDialgueFlags.OFN_EXPLORER | OpenSaveFileDialgueFlags.OFN_LONGNAMES;

		/// <summary>
		/// Dictionary storing last opened path per initialDir.
		/// Key is the initialDir, value is the last opened path.
		/// </summary>
		private static readonly Dictionary<string, string> LastOpenedPaths = new Dictionary<string, string>();

		private static readonly string lastOpenedPathsCachePath = Path.Combine(Paths.CachePath, "ModdingAPI.lastOpenedPaths");

		/// <inheritdoc cref="M:KKAPI.Utilities.OpenFileDialog.ShowDialog(System.String,System.String,System.String,System.String,KKAPI.Utilities.OpenFileDialog.OpenSaveFileDialgueFlags,System.String,System.IntPtr)" />
		public static string[] ShowDialog(string title, string initialDir, string filter, string defaultExt, OpenSaveFileDialgueFlags flags, IntPtr owner = default(IntPtr))
		{
			return ShowDialog(title, initialDir, filter, defaultExt, flags, null, owner);
		}

		/// <summary>
		/// Show windows file open dialog. Blocks the thread until user closes the dialog. Returns list of selected files, or null if user cancelled the action.
		/// </summary>
		/// <param name="title">
		/// A string to be placed in the title bar of the dialog box. If this member is NULL, the system uses
		/// the default title (that is, Save As or Open)
		/// </param>
		/// <param name="initialDir">
		/// The initial directory. The algorithm for selecting the initial directory varies on different
		/// platforms.
		/// </param>
		/// <param name="filter">
		/// A list of filter pairs separated by |. First item is the display name, while the second is
		/// the actual filter (e.g. *.txt) Example: <code>"Log files (.log)|*.log|All files|*.*"</code>
		/// </param>
		/// <param name="defaultExt">
		/// The default extension. This extension is appended to the file name if the user fails to type
		/// an extension.
		/// </param>
		/// <param name="flags">
		/// A set of bit flags you can use to initialize the dialog box. When the dialog box returns, it sets these flags to
		/// indicate the user's input.
		/// This member can be a combination of the CommomDialgueFlags.
		/// </param>
		/// <param name="owner">Hwnd pointer of the owner window. IntPtr.Zero to use default parent</param>
		/// <param name="defaultFilename"> Filename that is initially entered in the filename box. </param>
		public static string[] ShowDialog(string title, string initialDir, string filter, string defaultExt, OpenSaveFileDialgueFlags flags, string defaultFilename, IntPtr owner = default(IntPtr))
		{
			OpenFileName openFileName = new OpenFileName();
			openFileName.structSize = Marshal.SizeOf(openFileName);
			openFileName.filter = filter.Replace("|", "\0") + "\0";
			openFileName.fileTitle = new string(new char[2048]);
			openFileName.maxFileTitle = openFileName.fileTitle.Length;
			if (KoikatuAPI.RememberFilePickerFolder.Value != KoikatuAPI.RememberFilePickerFolderSetting.Disabled)
			{
				LastOpenedPaths.TryGetValue(initialDir, out openFileName.initialDir);
			}
			if (string.IsNullOrEmpty(openFileName.initialDir))
			{
				openFileName.initialDir = initialDir;
			}
			openFileName.title = title;
			openFileName.flags = (int)flags;
			if (defaultExt != null)
			{
				openFileName.defExt = defaultExt;
			}
			char[] array = new char[2048];
			defaultFilename?.CopyTo(0, array, 0, defaultFilename.Length);
			string text = new string(array);
			openFileName.file = Marshal.StringToBSTR(text);
			openFileName.maxFile = text.Length;
			if (owner == (IntPtr)0)
			{
				owner = NativeMethods.GetActiveWindow();
			}
			openFileName.dlgOwner = owner;
			bool runInBackground = Application.runInBackground;
			Application.runInBackground = false;
			string currentWorkingDirectory = Environment.CurrentDirectory;
			bool run = true;
			ThreadingHelper.Instance.StartAsyncInvoke((Func<Action>)delegate
			{
				while (run)
				{
					Environment.CurrentDirectory = currentWorkingDirectory;
				}
				return (Action)null;
			});
			bool openFileName2 = NativeMethods.GetOpenFileName(openFileName);
			run = false;
			Environment.CurrentDirectory = currentWorkingDirectory;
			Application.runInBackground = runInBackground;
			if (openFileName2)
			{
				List<string> list = new List<string>();
				long num = (long)openFileName.file;
				string text2 = Marshal.PtrToStringAuto(openFileName.file);
				while (!string.IsNullOrEmpty(text2))
				{
					list.Add(text2);
					num += text2.Length * 2 + 2;
					openFileName.file = (IntPtr)num;
					text2 = Marshal.PtrToStringAuto(openFileName.file);
				}
				LastOpenedPaths[initialDir] = Path.GetDirectoryName(list[0]);
				SaveFilePickerStates();
				if (list.Count == 1)
				{
					return list.ToArray();
				}
				string[] array2 = new string[list.Count - 1];
				for (int num2 = 0; num2 < array2.Length; num2++)
				{
					array2[num2] = list[0] + "\\" + list[num2 + 1];
				}
				return array2;
			}
			return null;
		}

		/// <summary>
		/// Show windows file open dialog. Doesn't pause the game.
		/// </summary>
		/// <param name="onAccept">Action that gets called with results of user's selection. Returns list of selected files, or null if user cancelled the action.
		/// WARNING: This runs on another thread! Game will crash if you attempt to access unity methods.
		/// You can use <code>KoikatuAPI.SynchronizedInvoke</code> to go back to the main thread.</param>
		/// <param name="title">
		/// A string to be placed in the title bar of the dialog box. If this member is NULL, the system uses
		/// the default title (that is, Save As or Open)
		/// </param>
		/// <param name="initialDir">
		/// The initial directory. The algorithm for selecting the initial directory varies on different
		/// platforms.
		/// </param>
		/// <param name="filter">
		/// A list of filter pairs separated by |. First item is the display name, while the second is
		/// the actual filter (e.g. *.txt) Example: <code>"Log files (.log)|*.log|All files|*.*"</code>
		/// </param>
		/// <param name="defaultExt">
		/// The default extension. This extension is appended to the file name if the user fails to type
		/// an extension.
		/// </param>
		/// <param name="flags">
		/// A set of bit flags you can use to initialize the dialog box. When the dialog box returns, it sets these flags to
		/// indicate the user's input.
		/// This member can be a combination of the CommomDialgueFlags.
		/// </param>
		public static void Show(Action<string[]> onAccept, string title, string initialDir, string filter, string defaultExt, OpenSaveFileDialgueFlags flags = OpenSaveFileDialgueFlags.OFN_NOCHANGEDIR | OpenSaveFileDialgueFlags.OFN_FILEMUSTEXIST | OpenSaveFileDialgueFlags.OFN_EXPLORER | OpenSaveFileDialgueFlags.OFN_LONGNAMES)
		{
			if (onAccept == null)
			{
				throw new ArgumentNullException("onAccept");
			}
			IntPtr handle = NativeMethods.GetActiveWindow();
			new Thread((ThreadStart)delegate
			{
				string[] obj = ShowDialog(title, initialDir, filter, defaultExt, flags, handle);
				onAccept(obj);
			}).Start();
		}

		internal static void SaveFilePickerStates()
		{
			try
			{
				using FileStream stream = File.Create(lastOpenedPathsCachePath);
				using StreamWriter streamWriter = new StreamWriter(stream);
				foreach (KeyValuePair<string, string> lastOpenedPath in LastOpenedPaths)
				{
					streamWriter.WriteLine(lastOpenedPath.Key + ", " + lastOpenedPath.Value);
				}
			}
			catch (Exception arg)
			{
				KoikatuAPI.Logger.LogError((object)$"Failed saving folder states to '{lastOpenedPathsCachePath}' because of error: {arg}");
			}
		}

		internal static void LoadFilePickerStates()
		{
			try
			{
				if (KoikatuAPI.RememberFilePickerFolder.Value != KoikatuAPI.RememberFilePickerFolderSetting.Enabled || !File.Exists(lastOpenedPathsCachePath))
				{
					return;
				}
				string[] array = File.ReadAllLines(lastOpenedPathsCachePath);
				string[] array2 = array;
				foreach (string text in array2)
				{
					if (!string.IsNullOrEmpty(text))
					{
						string[] array3 = text.Split(',');
						if (array3.Length == 2)
						{
							LastOpenedPaths[array3[0]] = array3[1];
						}
					}
				}
			}
			catch (Exception arg)
			{
				KoikatuAPI.Logger.LogError((object)$"Failed loading folder states from '{lastOpenedPathsCachePath}' because of error: {arg}");
			}
		}
	}
	/// <summary>
	/// Allows to move files to recycle bin instead of completely removing them.
	/// https://stackoverflow.com/questions/3282418/send-a-file-to-the-recycle-bin?answertab=votes#tab-top
	/// </summary>
	public static class RecycleBinUtil
	{
		/// <summary>
		/// Possible flags for the SHFileOperation method.
		/// </summary>
		[Flags]
		public enum FileOperationFlags : ushort
		{
			/// <summary>
			/// Do not show a dialog during the process
			/// </summary>
			FOF_SILENT = 4,
			/// <summary>
			/// Do not ask the user to confirm selection
			/// </summary>
			FOF_NOCONFIRMATION = 0x10,
			/// <summary>
			/// Delete the file to the recycle bin.  (Required flag to send a file to the bin
			/// </summary>
			FOF_ALLOWUNDO = 0x40,
			/// <summary>
			/// Do not show the names of the files or folders that are being recycled.
			/// </summary>
			FOF_SIMPLEPROGRESS = 0x100,
			/// <summary>
			/// Surpress errors, if any occur during the process.
			/// </summary>
			FOF_NOERRORUI = 0x400,
			/// <summary>
			/// Warn if files are too big to fit in the recycle bin and will need
			/// to be deleted completely.
			/// </summary>
			FOF_WANTNUKEWARNING = 0x4000
		}

		/// <summary>
		/// File Operation Function Type for SHFileOperation
		/// </summary>
		private enum FileOperationType : uint
		{
			/// <summary>
			/// Move the objects
			/// </summary>
			FO_MOVE = 1u,
			/// <summary>
			/// Copy the objects
			/// </summary>
			FO_COPY,
			/// <summary>
			/// Delete (or recycle) the objects
			/// </summary>
			FO_DELETE,
			/// <summary>
			/// Rename the object(s)
			/// </summary>
			FO_RENAME
		}

		/// <summary>
		/// SHFILEOPSTRUCT for SHFileOperation from COM
		/// </summary>
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
		private struct SHFILEOPSTRUCT
		{
			public readonly IntPtr hwnd;

			[MarshalAs(UnmanagedType.U4)]
			public FileOperationType wFunc;

			public string pFrom;

			public readonly string pTo;

			public FileOperationFlags fFlags;

			[MarshalAs(UnmanagedType.Bool)]
			public readonly bool fAnyOperationsAborted;

			public readonly IntPtr hNameMappings;

			public readonly string lpszProgressTitle;
		}

		/// <summary>
		/// Send file to recycle bin
		/// </summary>
		/// <param name="path">Location of directory or file to recycle</param>
		/// <param name="flags">FileOperationFlags to add in addition to FOF_ALLOWUNDO</param>
		public static bool MoveToRecycleBin(string path, FileOperationFlags flags)
		{
			try
			{
				SHFILEOPSTRUCT FileOp = new SHFILEOPSTRUCT
				{
					wFunc = FileOperationType.FO_DELETE,
					pFrom = path + "\0\0",
					fFlags = (FileOperationFlags.FOF_ALLOWUNDO | flags)
				};
				SHFileOperation(ref FileOp);
				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}

		/// <summary>
		/// Send file silently to recycle bin.  Surpress dialog, surpress errors, delete if too large.
		/// </summary>
		/// <param name="path">Location of directory or file to recycle</param>
		public static bool MoveToRecycleBin(string path)
		{
			return MoveToRecycleBin(path, FileOperationFlags.FOF_SILENT | FileOperationFlags.FOF_NOCONFIRMATION | FileOperationFlags.FOF_NOERRORUI);
		}

		[DllImport("shell32.dll", CharSet = CharSet.Auto)]
		private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);
	}
	/// <summary>
	/// General utility extensions that don't fit in other categories.
	/// </summary>
	public static class Extensions
	{
		/// <summary>
		/// Wrap this dictionary in a read-only wrapper that will prevent any changes to it. 
		/// Warning: Any reference types inside the dictionary can still be modified.
		/// </summary>
		public static ReadOnlyDictionary<TKey, TValue> ToReadOnlyDictionary<TKey, TValue>(this IDictionary<TKey, TValue> original)
		{
			return new ReadOnlyDictionary<TKey, TValue>(original);
		}

		/// <summary>
		/// Mark GameObject of this Component as ignored by AutoTranslator. Prevents AutoTranslator from trying to translate custom UI elements.
		/// </summary>
		public static void MarkXuaIgnored(this UnityEngine.Component target)
		{
			if (target == null)
			{
				throw new ArgumentNullException("target");
			}
			target.gameObject.name += "(XUAIGNORE)";
		}

		/// <summary>
		/// Same as RemoveAllListeners but also disables all PersistentListeners.
		/// To avoid frustration always use this instead of RemoveAllListeners, unless you want to keep the PersistentListeners.
		/// </summary>
		public static void ActuallyRemoveAllListeners(this UnityEventBase evt)
		{
			evt.m_Calls.Clear();
			evt.m_Calls.ClearPersistent();
			evt.m_PersistentCalls.Clear();
			evt.m_CallsDirty = false;
		}

		/// <summary>
		/// Attempt to project each element of the sequence into a new form (Select but ignore exceptions).
		/// Exceptions thrown while doing this are ignored and any elements that fail to be converted are silently skipped.
		/// </summary>
		public static IEnumerable<T2> Attempt<T, T2>(this IEnumerable<T> items, Func<T, T2> action)
		{
			return items.Attempt(action, null);
		}

		/// <inheritdoc cref="M:KKAPI.Utilities.Extensions.Attempt``2(System.Collections.Generic.IEnumerable{``0},System.Func{``0,``1})" />
		public static IEnumerable<T2> Attempt<T, T2>(this IEnumerable<T> items, Func<T, T2> action, Action<Exception> onError)
		{
			foreach (T item in items)
			{
				T2 val;
				try
				{
					val = action(item);
				}
				catch (Exception obj)
				{
					onError?.Invoke(obj);
					continue;
				}
				yield return val;
			}
		}

		/// <summary>
		/// Destroy this GameObject. Safe to use on null objects.
		/// </summary>
		/// <param name="self">Object to destroy</param>
		/// <param name="useDestroyImmediate">Use DestroyImmediate instead of Destroy</param>
		/// <param name="detachParent">Set parent of the object to null before destroying it (useful when doing FindInChildren and such immediately after destroying to make sure that this destroyed object isn't included in the results)</param>
		public static void FancyDestroy(this GameObject self, bool useDestroyImmediate = false, bool detachParent = false)
		{
			if (!(self == null))
			{
				if (detachParent)
				{
					self.transform.SetParent(null);
				}
				if (useDestroyImmediate)
				{
					UnityEngine.Object.DestroyImmediate(self);
				}
				else
				{
					UnityEngine.Object.Destroy(self);
				}
			}
		}

		/// <summary>
		/// Get full GameObject "path" to this GameObject.
		/// Example: RootObject\ChildObject1\ChildObject2
		/// </summary>
		public static string GetFullPath(this GameObject self)
		{
			if (self == null)
			{
				return string.Empty;
			}
			StringBuilder stringBuilder = new StringBuilder();
			bool flag = true;
			Transform transform = self.transform;
			while (transform != null)
			{
				if (flag)
				{
					flag = false;
				}
				else
				{
					stringBuilder.Insert(0, "/");
				}
				stringBuilder.Insert(0, transform.gameObject.name);
				transform = transform.parent;
			}
			return stringBuilder.ToString();
		}

		/// <summary>
		/// Get full GameObject "path" to this Component.
		/// Example: RootObject\ChildObject1\ChildObject2 [Renderer]
		/// </summary>
		public static string GetFullPath(this UnityEngine.Component self)
		{
			if (self == null)
			{
				return string.Empty;
			}
			return self.gameObject.GetFullPath() + " [" + self.GetType().Name + "]";
		}

		/// <summary>
		/// Get the topmost parent of Transform that this this Component is attached to.
		/// </summary>
		public static Transform GetTopmostParent(this UnityEngine.Component src)
		{
			return src.transform.GetTopmostParent();
		}

		/// <summary>
		/// Get the topmost parent of this GameObject.
		/// </summary>
		public static Transform GetTopmostParent(this GameObject src)
		{
			return src.transform.GetTopmostParent();
		}

		/// <summary>
		/// Get the topmost parent of this Transform.
		/// </summary>
		public static Transform GetTopmostParent(this Transform src)
		{
			while ((bool)src.parent)
			{
				src = src.parent;
			}
			return src;
		}

		/// <summary>
		/// Return true if the object is a "fake" null (i.e. it was destroyed).
		/// </summary>
		public static bool IsDestroyed(this UnityEngine.Object obj)
		{
			if ((object)obj != null)
			{
				return !obj;
			}
			return false;
		}

		/// <summary>
		/// Get value of the aaWeightsBody field
		/// </summary>
		public static AssignedAnotherWeights GetAaWeightsBody(this ChaControl ctrl)
		{
			return ctrl.aaWeightsBody;
		}

		/// <summary>
		/// Get value of a property through reflection
		/// </summary>
		/// <param name="self">Object that has the property</param>
		/// <param name="name">Name of the property</param>
		/// <param name="value">Value returned by the property</param>
		/// <returns>True if the property exists, flase if it doesn't</returns>
		public static bool GetPropertyValue(this object self, string name, out object value)
		{
			Traverse val = Traverse.Create(self).Property(name, (object[])null);
			if (val.PropertyExists())
			{
				value = val.GetValue();
				return true;
			}
			UnityEngine.Debug.LogWarning("Property " + name + " doesn't exist on " + self?.GetType().FullName);
			value = null;
			return false;
		}

		/// <summary>
		/// Set value of a property through reflection
		/// </summary>
		/// <param name="self">Object that has the property</param>
		/// <param name="name">Name of the property</param>
		/// <param name="value">Value to be set to the property</param>
		/// <returns>True if the property exists, flase if it doesn't</returns>
		public static bool SetPropertyValue(this object self, string name, object value)
		{
			return Traverse.Create(self).Property(name, (object[])null).SetValue(value)
				.PropertyExists();
		}

		/// <summary>
		/// Get value of a field through reflection
		/// </summary>
		/// <param name="self">Object that has the field</param>
		/// <param name="name">Name of the field</param>
		/// <param name="value">Value returned by the field</param>
		/// <returns>True if the field exists, flase if it doesn't</returns>
		public static bool GetFieldValue(this object self, string name, out object value)
		{
			Traverse val = Traverse.Create(self).Field(name);
			if (val.FieldExists())
			{
				value = val.GetValue();
				return true;
			}
			UnityEngine.Debug.LogWarning("Field " + name + " doesn't exist on " + self?.GetType().FullName);
			value = null;
			return false;
		}

		/// <summary>
		/// Set value of a field through reflection
		/// </summary>
		/// <param name="self">Object that has the field</param>
		/// <param name="name">Name of the property</param>
		/// <param name="value">Value to be set to the field</param>
		/// <returns>True if the field exists, flase if it doesn't</returns>
		public static bool SetFieldValue(this object self, string name, object value)
		{
			return Traverse.Create(self).Field(name).SetValue(value)
				.FieldExists();
		}

		internal static void SafeInvoke<T>(this T handler, Action<T> invokeCallback) where T : Delegate
		{
			if (handler == null)
			{
				return;
			}
			try
			{
				Delegate[] invocationList = handler.GetInvocationList();
				foreach (Delegate obj in invocationList)
				{
					try
					{
						invokeCallback((T)obj);
					}
					catch (Exception ex)
					{
						KoikatuAPI.Logger.LogError((object)("Event handler crashed with exception: " + ex));
					}
				}
			}
			catch (Exception ex2)
			{
				KoikatuAPI.Logger.LogError((object)("Unexpected crash when running events, some events might have been skipped! Reason: " + ex2));
			}
		}

		internal static void SafeInvokeWithLogging<T>(this T handler, Action<T> invokeCallback, string handlerName, ApiEventExecutionLogger eventLogger) where T : Delegate
		{
			if (handler == null)
			{
				return;
			}
			try
			{
				eventLogger?.EventHandlerBegin(handlerName);
				Delegate[] invocationList = handler.GetInvocationList();
				foreach (Delegate obj in invocationList)
				{
					eventLogger?.EventHandlerStart();
					try
					{
						invokeCallback((T)obj);
					}
					catch (Exception ex)
					{
						KoikatuAPI.Logger.LogError((object)("Event handler crashed with exception: " + ex));
					}
					eventLogger?.EventHandlerEnd(obj.Method);
				}
			}
			catch (Exception ex2)
			{
				KoikatuAPI.Logger.LogError((object)("Unexpected crash when running events, some events might have been skipped! Reason: " + ex2));
			}
		}

		/// <summary>
		/// This method compares two byte arrays for equality, returning true if they are identical and false otherwise.
		/// It is optimized for high performance and uses unsafe code.
		/// </summary>
		/// <param name="a">The first byte array to compare.</param>
		/// <param name="b">The second byte array to compare.</param>
		/// <returns>True if the byte arrays are equal, false otherwise.</returns>
		public unsafe static bool SequenceEqualFast(this byte[] a, byte[] b)
		{
			if (a == b)
			{
				return true;
			}
			if (a == null || b == null)
			{
				return false;
			}
			int num = a.Length;
			if (num != b.Length)
			{
				return false;
			}
			if (num <= 0)
			{
				return true;
			}
			fixed (byte* ptr = &a[0])
			{
				fixed (byte* ptr2 = &b[0])
				{
					int num2 = 0;
					if (((int)ptr & 7) == 0 && ((int)ptr2 & 7) == 0 && num >= 32)
					{
						num2 = num & -32;
						byte* ptr3 = ptr;
						byte* ptr4 = ptr2;
						byte* ptr5 = ptr + num2;
						while (*(long*)ptr3 == *(long*)ptr4)
						{
							ptr3 += 8;
							ptr4 += 8;
							if (*(long*)ptr3 != *(long*)ptr4)
							{
								break;
							}
							ptr3 += 8;
							ptr4 += 8;
							if (*(long*)ptr3 != *(long*)ptr4)
							{
								break;
							}
							ptr3 += 8;
							ptr4 += 8;
							if (*(long*)ptr3 != *(long*)ptr4)
							{
								break;
							}
							ptr3 += 8;
							ptr4 += 8;
							if (ptr3 != ptr5)
							{
								continue;
							}
							goto IL_015b;
						}
					}
					else
					{
						if (((int)ptr & 3) != 0 || ((int)ptr2 & 3) != 0 || num < 16)
						{
							goto IL_015b;
						}
						num2 = num & -16;
						byte* ptr6 = ptr;
						byte* ptr7 = ptr2;
						byte* ptr8 = ptr + num2;
						while (*(uint*)ptr6 == *(uint*)ptr7)
						{
							ptr6 += 4;
							ptr7 += 4;
							if (*(uint*)ptr6 != *(uint*)ptr7)
							{
								break;
							}
							ptr6 += 4;
							ptr7 += 4;
							if (*(uint*)ptr6 != *(uint*)ptr7)
							{
								break;
							}
							ptr6 += 4;
							ptr7 += 4;
							if (*(uint*)ptr6 != *(uint*)ptr7)
							{
								break;
							}
							ptr6 += 4;
							ptr7 += 4;
							if (ptr6 != ptr8)
							{
								continue;
							}
							goto IL_015b;
						}
					}
					goto end_IL_0038;
					IL_015b:
					int num3 = num2;
					while (num3 < num)
					{
						if (ptr[num3] == ptr2[num3])
						{
							num3++;
							continue;
						}
						goto end_IL_0038;
					}
					goto IL_0185;
					end_IL_0038:;
				}
			}
			return false;
			IL_0185:
			return true;
		}
	}
	/// <summary>
	/// Class that abstracts away AutoTranslator. It lets you translate text to current language.
	/// </summary>
	public static class TranslationHelper
	{
		private static readonly Action<string, Action<string>> _translatorCallback;

		private static readonly Func<string, string> _tryTranslateCallback;

		/// <summary>
		/// True if a reasonably recent version of AutoTranslator is installed.
		/// It might return false for some very old versions that don't have the necessary APIs to make this class work.
		/// </summary>
		public static bool AutoTranslatorInstalled { get; }

		static TranslationHelper()
		{
			Type type = Type.GetType("XUnity.AutoTranslator.Plugin.Core.ITranslator, XUnity.AutoTranslator.Plugin.Core", throwOnError: false);
			if (type != null && type.GetMethods().Any((MethodInfo x) => x.Name == "TranslateAsync"))
			{
				_translatorCallback = delegate(string s, Action<string> action)
				{
					AutoTranslator.Default.TranslateAsync(s, (Action<TranslationResult>)delegate(TranslationResult result)
					{
						if (result.Succeeded)
						{
							action(result.TranslatedText);
						}
					});
				};
				_tryTranslateCallback = (string s) => (!AutoTranslator.Default.TryTranslate(s, ref s)) ? null : s;
				AutoTranslatorInstalled = true;
			}
			else
			{
				KoikatuAPI.Logger.LogWarning((object)"Could not find method AutoTranslator.Default.TranslateAsync, translations will be limited or unavailable");
				_translatorCallback = null;
			}
		}

		/// <summary>
		/// Queries AutoTranslator to provide a translated text for the untranslated text.
		/// If the translation cannot be found in the cache, it will make a request to the translator selected by the user.
		/// If AutoTranslator is not installed, this will do nothing.
		/// </summary>
		/// <param name="untranslatedText">The untranslated text to provide a translation for.</param>
		/// <param name="onCompleted">Callback with the completed translation. It can return immediately or at some later point.</param>
		public static void TranslateAsync(string untranslatedText, Action<string> onCompleted)
		{
			if (onCompleted == null)
			{
				throw new ArgumentNullException("onCompleted");
			}
			if (!string.IsNullOrEmpty(untranslatedText))
			{
				_translatorCallback?.Invoke(untranslatedText, onCompleted);
			}
		}

		/// <summary>
		/// Queries the plugin to provide a translated text for the untranslated text.
		/// If the translation cannot be found in the cache, the method returns false
		/// and returns null as the untranslated text.
		/// </summary>
		/// <param name="untranslatedText">The untranslated text to provide a translation for.</param>
		/// <param name="translatedText">The translated text.</param>
		public static bool TryTranslate(string untranslatedText, out string translatedText)
		{
			if (string.IsNullOrEmpty(untranslatedText) || _tryTranslateCallback == null)
			{
				translatedText = null;
				return false;
			}
			translatedText = _tryTranslateCallback(untranslatedText);
			return translatedText != null;
		}
	}
	/// <summary>
	/// String comparer that is equivalent to the one used by Windows Explorer to sort files (e.g. 2 will go before 10, unlike normal compare).
	/// </summary>
	/// <inheritdoc />
	public class WindowsStringComparer : IComparer<string>
	{
		[DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
		private static extern int StrCmpLogicalW(string x, string y);

		/// <summary>
		/// Compare two strings with rules used by Windows Explorer to logically sort files.
		/// </summary>
		public int Compare(string x, string y)
		{
			return StrCmpLogicalW(x, y);
		}

		/// <summary>
		/// Compare two strings with rules used by Windows Explorer to logically sort files.
		/// </summary>
		public static int LogicalCompare(string x, string y)
		{
			return StrCmpLogicalW(x, y);
		}
	}
}
namespace KKAPI.Chara
{
	/// <summary>
	/// Provides an easy way to add custom logic to all characters in the game and in studio. 
	/// It takes care of all the error-prone plumbing and lets you easily save and load data to the character cards.
	/// </summary>
	public static class CharacterApi
	{
		/// <summary>
		/// Information about a single kind of a <see cref="T:KKAPI.Chara.CharaCustomFunctionController" />. New kind is created every time
		/// you call <see cref="M:KKAPI.Chara.CharacterApi.RegisterExtraBehaviour``1(System.String)" />.
		/// </summary>
		public sealed class ControllerRegistration
		{
			private readonly List<CharaCustomFunctionController> _instances = new List<CharaCustomFunctionController>();

			/// <summary>
			/// All currently existing instances of this kind of controller.
			/// </summary>
			public IEnumerable<CharaCustomFunctionController> Instances => _instances.Where((CharaCustomFunctionController x) => x != null);

			/// <summary>
			/// Type of the custom controller kind.
			/// </summary>
			public Type ControllerType { get; }

			/// <summary>
			/// ID of the extended data used by this controller kind.
			/// </summary>
			public string ExtendedDataId { get; }

			/// <summary>
			/// Method used to copy extended data used by this controller in case that's necessary.
			/// </summary>
			public CopyExtendedDataFunc ExtendedDataCopier { get; }

			/// <summary>
			/// If true, the current state of all controllers of this kind should be preserved inside <see cref="M:KKAPI.Chara.CharaCustomFunctionController.OnReload(KKAPI.GameMode,System.Boolean)" />.
			/// New extended data will not be read, instead currently data will be reused, or the reload will not happen at all.
			/// </summary>
			public bool MaintainState { get; set; }

			/// <summary>
			/// If true, the current state of all controllers of this kind should be preserved inside <see cref="M:KKAPI.Chara.CharaCustomFunctionController.OnCoordinateBeingLoaded(ChaFileCoordinate,System.Boolean)" />.
			/// New extended data will not be read, instead currently data will be reused, or the load will not happen at all.
			/// </summary>
			public bool MaintainCoordinateState { get; set; }

			internal ControllerRegistration(Type controllerType, string extendedDataId, CopyExtendedDataFunc extendedDataCopier)
			{
				ControllerType = controllerType;
				ExtendedDataId = extendedDataId;
				ExtendedDataCopier = extendedDataCopier;
			}

			internal void CreateInstance(ChaControl target)
			{
				CharaCustomFunctionController charaCustomFunctionController = (CharaCustomFunctionController)target.gameObject.AddComponent(ControllerType);
				charaCustomFunctionController.ControllerRegistration = this;
				_instances.Add(charaCustomFunctionController);
				_instances.RemoveAll((CharaCustomFunctionController x) => x == null);
			}
		}

		/// <summary>
		/// Override to supply custom extended data copying logic.
		/// By default copies all data under <code>ExtendedDataId</code> by reference.
		/// </summary>
		/// <param name="dst">Copy current character's ext data to this character</param>
		/// <param name="src">Current character to copy the data from</param>
		public delegate void CopyExtendedDataFunc(ChaFile dst, ChaFile src);

		internal static class Hooks
		{
			/// <summary>
			/// Prevents firing coordinate load events when the coordinate window is populating
			/// </summary>
			public static bool ClothesFileControlLoading;

			private static readonly HashSet<SaveData.CharaData> _dirtyCharas = new HashSet<SaveData.CharaData>();

			public static void InitHooks()
			{
				//IL_0058: Unknown result type (might be due to invalid IL or missing references)
				//IL_0065: Expected O, but got Unknown
				//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
				//IL_00ba: Expected O, but got Unknown
				//IL_0102: Unknown result type (might be due to invalid IL or missing references)
				//IL_010f: Expected O, but got Unknown
				//IL_0159: Unknown result type (might be due to invalid IL or missing references)
				//IL_0166: Expected O, but got Unknown
				//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
				//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
				//IL_01c0: Expected O, but got Unknown
				Harmony val = Harmony.CreateAndPatchAll(typeof(Hooks), (string)null);
				MethodInfo methodInfo = typeof(ChaControl).GetMethods().Single((MethodInfo info) => info.Name == "Initialize" && info.GetParameters().Length >= 5);
				val.Patch((MethodBase)methodInfo, (HarmonyMethod)null, new HarmonyMethod(typeof(Hooks), "ChaControl_InitializePostHook", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
				MethodInfo methodInfo2 = typeof(ChaControl).GetMethods().Single((MethodInfo info) => info.Name == "ReloadAsync" && info.GetParameters().Length >= 5);
				val.Patch((MethodBase)methodInfo2, (HarmonyMethod)null, new HarmonyMethod(typeof(Hooks), "ReloadAsyncPostHook", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
				MethodInfo methodInfo3 = typeof(ChaControl).GetMethods().Single((MethodInfo info) => info.Name == "ReloadNoAsync" && info.GetParameters().Length >= 4);
				val.Patch((MethodBase)methodInfo3, (HarmonyMethod)null, new HarmonyMethod(typeof(Hooks), "ReloadNoAsyncPostHook", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
				MethodInfo methodInfo4 = typeof(ChaFile).GetMethods().Single((MethodInfo info) => info.Name == "CopyAll");
				val.Patch((MethodBase)methodInfo4, (HarmonyMethod)null, new HarmonyMethod(typeof(Hooks), "ChaFile_CopyChaFileHook", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
				val.Patch((MethodBase)AccessTools.FirstMethod(typeof(ChaFile), (Func<MethodInfo, bool>)((MethodInfo info) => info.Name == "LoadFile" && info.GetParameters().FirstOrDefault()?.ParameterType == typeof(BinaryReader))), new HarmonyMethod(typeof(Hooks), "ChaFileLoadHook", (Type[])null)
				{
					wrapTryCatch = true
				}, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			}

			private static void ChaFileLoadHook(ChaFile __instance, BinaryReader br)
			{
				if (br.BaseStream is FileStream fileStream)
				{
					string fullPath = Path.GetFullPath(fileStream.Name);
					CharacterExtensions.ChaFileFullPathLookup[__instance] = fullPath;
				}
			}

			public static void ChaControl_InitializePostHook(ChaControl __instance)
			{
				KoikatuAPI.Logger.LogDebug((object)("Character card load: " + GetLogName(__instance?.chaFile) + " " + (MakerAPI.CharaListIsLoading ? "inside CharaList" : string.Empty)));
				ChaControls.Add(__instance);
				if (!MakerAPI.CharaListIsLoading)
				{
					CreateOrAddBehaviours(__instance);
				}
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaControl), "OnDestroy")]
			public static void ChaControl_OnDestroyHook(ChaControl __instance)
			{
				ChaControls.Remove(__instance);
			}

			/// <summary>
			/// Needed for saving in class maker, rest is handled by ExtendedSave.CardBeingSaved
			/// </summary>
			[HarmonyPrefix]
			[HarmonyPatch(typeof(CvsExit), "ExitSceneRestoreStatus", new Type[] { typeof(string) })]
			public static void CvsExit_ExitSceneRestoreStatus(string strInput, CvsExit __instance)
			{
				OnCardBeingSaved(Singleton<CustomBase>.Instance.chaCtrl.chaFile);
			}

			/// <summary>
			/// Copy extended data when moving between class roster and main game data, and in free h
			/// (the character data gets transferred to predefined slots instead of creating new characters)
			/// </summary>
			public static void ChaFile_CopyChaFileHook(ChaFile __instance, ChaFile _chafile)
			{
				OnCopyChaFile(__instance, _chafile);
			}

			private static void OnCopyChaFile(ChaFile destination, ChaFile source)
			{
				foreach (KeyValuePair<int, ControllerRegistration> registeredHandler in _registeredHandlers)
				{
					if (registeredHandler.Value.ExtendedDataCopier != null)
					{
						try
						{
							registeredHandler.Value.ExtendedDataCopier(destination, source);
						}
						catch (Exception ex)
						{
							KoikatuAPI.Logger.LogError((object)ex);
						}
					}
				}
			}

			/// <summary>
			/// Fix extended data being lost in ADV by copying it over when chara data is copied
			/// </summary>
			public static IEnumerable<CodeInstruction> FixEventSceneLambdaTpl(MethodBase original, IEnumerable<CodeInstruction> instructions)
			{
				//IL_006c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0072: Expected O, but got Unknown
				//IL_0092: Unknown result type (might be due to invalid IL or missing references)
				//IL_0098: Expected O, but got Unknown
				//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
				//IL_00a6: Expected O, but got Unknown
				//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
				//IL_00c9: Expected O, but got Unknown
				List<CodeInstruction> list = instructions.ToList();
				FieldInfo target = AccessTools.Field(typeof(ChaFile), "pngData");
				if (target == null)
				{
					throw new ArgumentNullException("target");
				}
				int num = list.FindIndex((CodeInstruction instruction) => instruction.opcode == OpCodes.Stfld && instruction.operand is FieldInfo fieldInfo && fieldInfo == target);
				if (num > 0)
				{
					list.InsertRange(num + 1, (IEnumerable<CodeInstruction>)(object)new CodeInstruction[4]
					{
						new CodeInstruction(OpCodes.Ldloc_2, (object)null),
						new CodeInstruction(OpCodes.Callvirt, (object)AccessTools.Property(typeof(SaveData.CharaData), "charFile").GetGetMethod()),
						new CodeInstruction(OpCodes.Ldloc_1, (object)null),
						new CodeInstruction(OpCodes.Call, (object)AccessTools.Method(typeof(Hooks), "OnCopyChaFile", (Type[])null, (Type[])null))
					});
					if (KoikatuAPI.EnableDebugLogging)
					{
						KoikatuAPI.Logger.LogDebug((object)("FixEventSceneLambdaTpl success on " + GeneralExtensions.FullDescription(original)));
					}
				}
				return list;
			}

			/// <summary>
			/// Needed to update controllers after ADV scene finishes loading since characters get loaded async so other events fire too early
			/// </summary>
			[HarmonyPostfix]
			[HarmonyPatch(typeof(ADV.CharaData), "Initialize")]
			public static void CharaData_InitializePost(ADV.CharaData __instance)
			{
				if (__instance.isADVCreateChara)
				{
					ReloadChara(__instance.chaCtrl);
				}
			}

			/// <summary>
			/// Update controllers after selecting live mode character
			/// </summary>
			[HarmonyPostfix]
			[HarmonyPatch(typeof(LiveCharaSelectSprite), "Start")]
			public static void LiveCharaSelectSprite_StartPostHook(LiveCharaSelectSprite __instance)
			{
				__instance.btnIdolBack?.onClick.AddListener(delegate
				{
					__instance.StartCoroutine(DelayedReloadChara(__instance.heroine.chaCtrl));
				});
			}

			/// <summary>
			/// Force reload when going to next day in school
			/// It's needed because after 1st day since loading the characters are reset but not reloaded, and can cause issues
			/// </summary>
			[HarmonyPrefix]
			[HarmonyPatch(typeof(ActionScene), "NPCLoadAll", new Type[] { })]
			[HarmonyPatch(typeof(ActionScene), "NPCLoadAll", new Type[] { typeof(bool) })]
			public static void ActionScene_NPCLoadAllPreHook()
			{
				CollectionExtensions.Do<Heroine>(Game.HeroineList.Where((Heroine x) => x?.chaCtrl != null), (Action<Heroine>)delegate(Heroine x)
				{
					OnCardBeingSaved(x.chaCtrl, GameMode.MainGame);
				});
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ActionScene), "NPCLoadAll", new Type[] { })]
			[HarmonyPatch(typeof(ActionScene), "NPCLoadAll", new Type[] { typeof(bool) })]
			public static void ActionScene_NPCLoadAllPostHook(ActionScene __instance, UniTask __result)
			{
				__instance.StartCoroutine(__result.WaitForFinishCo().AppendCo(delegate
				{
					CollectionExtensions.Do<Heroine>(Game.HeroineList.Where((Heroine x) => x?.chaCtrl), (Action<Heroine>)delegate(Heroine x)
					{
						ReloadChara(x.chaCtrl);
					});
				}));
			}

			/// <summary>
			/// Needed for some edge cases, replacing characters in scene maker in EC
			/// </summary>
			public static void ReloadAsyncPostHook(bool noChangeClothes, bool noChangeHead, bool noChangeHair, bool noChangeBody, ChaControl __instance, ref IEnumerator __result)
			{
				if (!(noChangeClothes || noChangeHead || noChangeHair || noChangeBody) && !IsCurrentlyReloading(__instance))
				{
					IEnumerator enumerator = __result;
					__result = new IEnumerator[2]
					{
						enumerator,
						DelayedReloadChara(__instance)
					}.GetEnumerator();
				}
			}

			public static void ReloadNoAsyncPostHook(bool noChangeClothes, bool noChangeHead, bool noChangeHair, bool noChangeBody, ChaControl __instance)
			{
				if (!(noChangeClothes || noChangeHead || noChangeHair || noChangeBody) && !MakerAPI.InsideMaker)
				{
					ReloadChara(__instance);
				}
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(clothesFileControl), "Initialize")]
			public static void clothesFileControl_InitializePreHook()
			{
				ClothesFileControlLoading = true;
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(clothesFileControl), "Initialize")]
			public static void clothesFileControl_InitializePostHook()
			{
				ClothesFileControlLoading = false;
			}

			/// <summary>
			/// Mark character to reload next time their assigned ChaControl changes, so that any changes in function 
			/// controller on previous chara can get propagated to controller on new chara
			/// todo limit this better to avoid unnecessary reloads? might not be an issue
			/// </summary>
			internal static bool SetDirty(SaveData.CharaData chara, bool dirty)
			{
				if (dirty && GameAPI.GameBeingSaved)
				{
					return false;
				}
				if (!dirty)
				{
					return _dirtyCharas.Remove(chara);
				}
				return _dirtyCharas.Add(chara);
			}

			[HarmonyPrefix]
			[HarmonyPatch(/*Could not decode attribute arguments.*/)]
			private static void OnChaCtrlChangePre(ChaControl value, SaveData.CharaData __instance, out bool __state)
			{
				__state = value != __instance.chaCtrl;
			}

			[HarmonyFinalizer]
			[HarmonyPatch(/*Could not decode attribute arguments.*/)]
			private static void OnChaCtrlChangePre(ChaControl value, SaveData.CharaData __instance, bool __state)
			{
				if (__state && _dirtyCharas.Remove(__instance))
				{
					ReloadChara(value);
				}
			}
		}

		[Serializable]
		[CompilerGenerated]
		private sealed class <>c
		{
			public static readonly <>c <>9 = new <>c();

			public static Func<KeyValuePair<int, ControllerRegistration>, IEnumerable<CharaCustomFunctionController>> <>9__7_0;

			public static EventHandler<ChaFileLoadedEventArgs> <>9__15_0;

			public static CoordinateEventHandler <>9__15_1;

			public static CoordinateEventHandler <>9__15_2;

			public static Action <>9__16_1;

			internal IEnumerable<CharaCustomFunctionController> <GetBehaviours>b__7_0(KeyValuePair<int, ControllerRegistration> x)
			{
				return x.Value.Instances;
			}

			internal void <Init>b__15_0(object sender, ChaFileLoadedEventArgs args)
			{
				ChaControl characterControl = MakerAPI.GetCharacterControl();
				if (characterControl != null)
				{
					ReloadChara(characterControl);
				}
			}

			internal void <Init>b__15_1(ChaFileCoordinate file)
			{
				if (file != null)
				{
					ChaControl characterControl = MakerAPI.GetCharacterControl();
					if (characterControl == null)
					{
						KoikatuAPI.Logger.LogError((object)("OnCoordinateBeingSaved fired outside chara maker for " + file.coordinateName));
						KoikatuAPI.Logger.LogInfo((object)new StackTrace());
					}
					else
					{
						OnCoordinateBeingSaved(characterControl, file);
					}
				}
			}

			internal void <Init>b__15_2(ChaFileCoordinate file)
			{
				<>c__DisplayClass15_0 CS$<>8__locals3 = new <>c__DisplayClass15_0
				{
					file = file
				};
				if (!Hooks.ClothesFileControlLoading && CS$<>8__locals3.file != null)
				{
					ChaControl chaControl = ChaControls.FirstOrDefault((ChaControl x) => x.nowCoordinate == CS$<>8__locals3.file);
					if (chaControl != null)
					{
						OnCoordinateBeingLoaded(chaControl, CS$<>8__locals3.file);
					}
				}
			}

			internal void <CreateOrAddBehaviours>b__16_1()
			{
			}
		}

		[CompilerGenerated]
		private sealed class <>c__DisplayClass15_0
		{
			public ChaFileCoordinate file;

			internal bool <Init>b__3(ChaControl x)
			{
				return x.nowCoordinate == file;
			}
		}

		internal static readonly HashSet<ChaControl> ChaControls = new HashSet<ChaControl>();

		private static readonly SortedList<int, ControllerRegistration> _registeredHandlers = new SortedList<int, ControllerRegistration>();

		private static readonly HashSet<ChaControl> _currentlyReloading = new HashSet<ChaControl>();

		/// <summary>
		/// All currently registered kinds of <see cref="T:KKAPI.Chara.CharaCustomFunctionController" /> controllers.
		/// </summary>
		public static IEnumerable<ControllerRegistration> RegisteredHandlers => _registeredHandlers.Values;

		/// <summary>
		/// Fired after all CharaCustomFunctionController have updated.
		/// </summary>
		public static event EventHandler<CharaReloadEventArgs> CharacterReloaded;

		/// <summary>
		/// Fired after a coordinate card was loaded and all controllers were updated.
		/// Not filed if the coordinate file was not loaded into a character (so not during list updates). 
		/// </summary>
		public static event EventHandler<CoordinateEventArgs> CoordinateLoaded;

		/// <summary>
		/// Fired just before a coordinate card is saved, but after all controllers wrote their data.
		/// </summary>
		public static event EventHandler<CoordinateEventArgs> CoordinateSaving;

		/// <summary>
		/// Get ChaControl that is using the specified ChaFileControl.
		/// </summary>
		public static ChaControl FileControlToChaControl(ChaFileControl fileControl)
		{
			return ChaControls.Single((ChaControl x) => x.chaFile == fileControl);
		}

		/// <summary>
		/// Get all extra behaviours for specified character. If null, returns extra behaviours for all characters.
		/// </summary>
		public static IEnumerable<CharaCustomFunctionController> GetBehaviours(ChaControl character = null)
		{
			if (character == null)
			{
				return _registeredHandlers.SelectMany((KeyValuePair<int, ControllerRegistration> x) => x.Value.Instances);
			}
			return character.GetComponents<CharaCustomFunctionController>();
		}

		/// <summary>
		/// Get the first controller that was registered with the specified extendedDataId.
		/// </summary>
		public static ControllerRegistration GetRegisteredBehaviour(string extendedDataId)
		{
			return _registeredHandlers.Values.First((ControllerRegistration registration) => string.Equals(registration.ExtendedDataId, extendedDataId, StringComparison.Ordinal));
		}

		/// <summary>
		/// Get the first controller of the specified type that was registered. The type has to be an exact match.
		/// </summary>
		public static ControllerRegistration GetRegisteredBehaviour(Type controllerType)
		{
			return _registeredHandlers.Values.First((ControllerRegistration registration) => registration.ControllerType == controllerType);
		}

		/// <summary>
		/// Get the first controller of the specified type that was registered with the specified extendedDataId. The type has to be an exact match.
		/// </summary>
		public static ControllerRegistration GetRegisteredBehaviour(Type controllerType, string extendedDataId)
		{
			return _registeredHandlers.Values.First((ControllerRegistration registration) => registration.ControllerType == controllerType && string.Equals(registration.ExtendedDataId, extendedDataId, StringComparison.Ordinal));
		}

		/// <summary>
		/// Register new functionality that will be automatically added to all characters (where applicable).
		/// Offers easy API for saving and loading extended data, and for running logic to apply it to the characters.
		/// All necessary hooking and event subscribing is done for you. All you have to do is create a type
		/// that inherits from <code>CharaExtraBehaviour</code> (don't make instances, the API will make them for you).
		/// </summary>
		/// <typeparam name="T">Type with your custom logic to add to a character</typeparam>
		/// <param name="extendedDataId">Extended data ID used by this behaviour. Set to null if not used. Needed to copy the data in some situations.</param>
		public static void RegisterExtraBehaviour<T>(string extendedDataId) where T : CharaCustomFunctionController, new()
		{
			RegisterExtraBehaviour<T>(extendedDataId, 1000);
		}

		/// <summary>
		/// Register new functionality that will be automatically added to all characters (where applicable).
		/// Offers easy API for saving and loading extended data, and for running logic to apply it to the characters.
		/// All necessary hooking and event subscribing is done for you. All you have to do is create a type
		/// that inherits from <code>CharaExtraBehaviour</code> (don't make instances, the API will make them for you).
		/// </summary>
		/// <typeparam name="T">Type with your custom logic to add to a character</typeparam>
		/// <param name="extendedDataId">Extended data ID used by this behaviour. Set to null if not used. Needed to copy the data in some situations.</param>
		/// <param name="priority">Default value is 1000, Decrease to increase priority or vice versa such on <see cref="M:KKAPI.Chara.CharaCustomFunctionController.OnReload(KKAPI.GameMode)" /> or related functions.</param>
		public static void RegisterExtraBehaviour<T>(string extendedDataId, int priority) where T : CharaCustomFunctionController, new()
		{
			CopyExtendedDataFunc customDataCopier = ((extendedDataId == null) ? null : new CopyExtendedDataFunc(BasicCopier));
			RegisterExtraBehaviour<T>(extendedDataId, customDataCopier, priority);
			void BasicCopier(ChaFile dst, ChaFile src)
			{
				PluginData extendedDataById = ExtendedSave.GetExtendedDataById(src, extendedDataId);
				ExtendedSave.SetExtendedDataById(dst, extendedDataId, extendedDataById);
			}
		}

		/// <summary>
		/// Register new functionality that will be automatically added to all characters (where applicable).
		/// Offers easy API for saving and loading extended data, and for running logic to apply it to the characters.
		/// All necessary hooking and event subscribing is done for you. All you have to do is create a type
		/// that inherits from <code>CharaExtraBehaviour</code> (don't make instances, the API will make them for you).
		/// </summary>
		/// <typeparam name="T">Type with your custom logic to add to a character</typeparam>
		/// <param name="extendedDataId">Extended data ID used by this behaviour. Set to null if not used.</param>
		/// <param name="customDataCopier">Override default extended data copy logic</param>
		public static void RegisterExtraBehaviour<T>(string extendedDataId, CopyExtendedDataFunc customDataCopier) where T : CharaCustomFunctionController, new()
		{
			RegisterExtraBehaviour<T>(extendedDataId, customDataCopier, 1000);
		}

		/// <summary>
		/// Register new functionality that will be automatically added to all characters (where applicable).
		/// Offers easy API for saving and loading extended data, and for running logic to apply it to the characters.
		/// All necessary hooking and event subscribing is done for you. All you have to do is create a type
		/// that inherits from <code>CharaExtraBehaviour</code> (don't make instances, the API will make them for you).
		/// </summary>
		/// <typeparam name="T">Type with your custom logic to add to a character</typeparam>
		/// <param name="extendedDataId">Extended data ID used by this behaviour. Set to null if not used.</param>
		/// <param name="customDataCopier">Override default extended data copy logic</param>
		/// <param name="priority">Default value is 1000, Decrease to increase priority or vice versa such on <see cref="M:KKAPI.Chara.CharaCustomFunctionController.OnReload(KKAPI.GameMode)" /> or related functions.</param>
		public static void RegisterExtraBehaviour<T>(string extendedDataId, CopyExtendedDataFunc customDataCopier, int priority) where T : CharaCustomFunctionController, new()
		{
			while (_registeredHandlers.ContainsKey(priority))
			{
				priority++;
			}
			_registeredHandlers.Add(priority, new ControllerRegistration(typeof(T), extendedDataId, customDataCopier));
		}

		internal static void Init()
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Expected O, but got Unknown
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Expected O, but got Unknown
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_007d: Expected O, but got Unknown
			Hooks.InitHooks();
			ExtendedSave.CardBeingSaved += new CardEventHandler(OnCardBeingSaved);
			MakerAPI.ChaFileLoaded += delegate
			{
				ChaControl characterControl = MakerAPI.GetCharacterControl();
				if (characterControl != null)
				{
					ReloadChara(characterControl);
				}
			};
			object obj = <>c.<>9__15_1;
			if (obj == null)
			{
				CoordinateEventHandler val = delegate(ChaFileCoordinate file)
				{
					if (file != null)
					{
						ChaControl characterControl = MakerAPI.GetCharacterControl();
						if (characterControl == null)
						{
							KoikatuAPI.Logger.LogError((object)("OnCoordinateBeingSaved fired outside chara maker for " + file.coordinateName));
							KoikatuAPI.Logger.LogInfo((object)new StackTrace());
						}
						else
						{
							OnCoordinateBeingSaved(characterControl, file);
						}
					}
				};
				<>c.<>9__15_1 = val;
				obj = (object)val;
			}
			ExtendedSave.CoordinateBeingSaved += (CoordinateEventHandler)obj;
			object obj2 = <>c.<>9__15_2;
			if (obj2 == null)
			{
				CoordinateEventHandler val2 = delegate(ChaFileCoordinate file)
				{
					if (!Hooks.ClothesFileControlLoading && file != null)
					{
						ChaControl chaControl = ChaControls.FirstOrDefault((ChaControl x) => x.nowCoordinate == file);
						if (chaControl != null)
						{
							OnCoordinateBeingLoaded(chaControl, file);
						}
					}
				};
				<>c.<>9__15_2 = val2;
				obj2 = (object)val2;
			}
			ExtendedSave.CoordinateBeingLoaded += (CoordinateEventHandler)obj2;
			if (KoikatuAPI.EnableDebugLogging)
			{
				RegisterExtraBehaviour<TestCharaCustomFunctionController>("TestCharaCustomFunctionController");
			}
		}

		private static void CreateOrAddBehaviours(ChaControl target)
		{
			foreach (KeyValuePair<int, ControllerRegistration> handler in _registeredHandlers)
			{
				CharaCustomFunctionController charaCustomFunctionController = target.gameObject.GetComponents(handler.Value.ControllerType).Cast<CharaCustomFunctionController>().FirstOrDefault((CharaCustomFunctionController x) => x.ExtendedDataId == handler.Value.ExtendedDataId);
				if (charaCustomFunctionController == null)
				{
					try
					{
						handler.Value.CreateInstance(target);
					}
					catch (Exception ex)
					{
						KoikatuAPI.Logger.LogError((object)ex);
					}
				}
			}
			CharaCustomFunctionController[] controllers = target.GetComponents<CharaCustomFunctionController>();
			target.UpdateAsObservable().Subscribe(delegate
			{
				bool flag = false;
				ChaFileDefine.CoordinateType coordinateType = (ChaFileDefine.CoordinateType)target.fileStatus.coordinateType;
				for (int i = 0; i < controllers.Length; i++)
				{
					BehaviorSubject<ChaFileDefine.CoordinateType> currentCoordinate = controllers[i].CurrentCoordinate;
					if (i == 0)
					{
						flag = coordinateType != currentCoordinate.Value;
						if (flag && KoikatuAPI.EnableDebugLogging)
						{
							KoikatuAPI.Logger.LogMessage((object)$"Changed coord of character {target.chaFile.GetFancyCharacterName()} to {coordinateType}");
						}
					}
					if (flag)
					{
						currentCoordinate.OnNext(coordinateType);
					}
				}
			});
			if (!MakerAPI.InsideMaker)
			{
				target.StartCoroutine(CoroutineUtils.CreateCoroutine(new WaitForEndOfFrame(), delegate
				{
				}, delegate
				{
					ApiEventExecutionLogger eventLogger = ApiEventExecutionLogger.GetEventLogger();
					eventLogger.Begin("CreateOrAddBehaviours", GetLogName(target.chaFile));
					OnCharacterReloaded(target, eventLogger);
					eventLogger.End();
				}));
			}
		}

		private static void OnCardBeingSaved(ChaFile chaFile)
		{
			GameMode currentGameMode = KoikatuAPI.GetCurrentGameMode();
			ChaControl chaControl = ((currentGameMode == GameMode.Maker) ? MakerAPI.GetCharacterControl() : chaFile.GetChaControl());
			if (chaControl == null)
			{
				if (GameAPI.GameBeingSaved)
				{
					KoikatuAPI.Logger.LogWarning((object)("Could not find chaControl responsible for this chaFile! The save event will NOT be triggered for " + GetLogName(chaFile)));
					return;
				}
				KoikatuAPI.Logger.LogWarning((object)"Could not find chaControl responsible for this chaFile! Triggering the save event on all chaControls!");
			}
			OnCardBeingSaved(chaControl, currentGameMode);
		}

		private static void OnCardBeingSaved(ChaControl chaControl, GameMode gamemode)
		{
			string logName = GetLogName(chaControl?.chaFile);
			KoikatuAPI.Logger.LogDebug((object)("Character save: " + logName));
			ApiEventExecutionLogger eventLogger = ApiEventExecutionLogger.GetEventLogger();
			eventLogger.Begin("OnCardBeingSaved", logName);
			foreach (CharaCustomFunctionController behaviour in GetBehaviours(chaControl))
			{
				eventLogger.PluginStart();
				behaviour.OnCardBeingSavedInternal(gamemode);
				eventLogger.PluginEnd(behaviour);
			}
			eventLogger.End();
		}

		private static void ReloadChara(ChaControl chaControl = null)
		{
			if (IsCurrentlyReloading(chaControl))
			{
				return;
			}
			if (chaControl == null)
			{
				_currentlyReloading.UnionWith(ChaControls);
			}
			else
			{
				_currentlyReloading.Add(chaControl);
			}
			string logName = GetLogName(chaControl?.chaFile);
			KoikatuAPI.Logger.LogDebug((object)("Character load/reload: " + logName));
			ApiEventExecutionLogger eventLogger = ApiEventExecutionLogger.GetEventLogger();
			eventLogger.Begin("ReloadChara", logName);
			GameMode currentGameMode = KoikatuAPI.GetCurrentGameMode();
			foreach (CharaCustomFunctionController behaviour in GetBehaviours(chaControl))
			{
				eventLogger.PluginStart();
				behaviour.OnReloadInternal(currentGameMode);
				eventLogger.PluginEnd(behaviour);
			}
			OnCharacterReloaded(chaControl, eventLogger);
			if (MakerAPI.InsideAndLoaded)
			{
				MakerAPI.OnReloadInterface(chaControl, new CharaReloadEventArgs(chaControl), eventLogger);
			}
			eventLogger.End();
			if (chaControl == null)
			{
				_currentlyReloading.Clear();
			}
			else
			{
				_currentlyReloading.Remove(chaControl);
			}
		}

		private static void OnCharacterReloaded(ChaControl chaControl, ApiEventExecutionLogger eventLogger)
		{
			if (CharacterApi.CharacterReloaded != null)
			{
				CharaReloadEventArgs args = new CharaReloadEventArgs(chaControl);
				CharacterApi.CharacterReloaded.SafeInvokeWithLogging(delegate(EventHandler<CharaReloadEventArgs> handler)
				{
					handler(chaControl, args);
				}, "CharacterApi.CharacterReloaded", eventLogger);
			}
		}

		private static bool IsCurrentlyReloading(ChaControl chaControl)
		{
			if (!(chaControl == null) || _currentlyReloading.Count <= 0)
			{
				return _currentlyReloading.Contains(chaControl);
			}
			return true;
		}

		private static IEnumerator DelayedReloadChara(ChaControl chaControl)
		{
			if (!MakerAPI.InsideMaker && !IsCurrentlyReloading(chaControl))
			{
				yield return null;
				ReloadChara(chaControl);
			}
		}

		private static void OnCoordinateBeingSaved(ChaControl character, ChaFileCoordinate coordinateFile)
		{
			string logName = GetLogName(character?.chaFile);
			KoikatuAPI.Logger.LogDebug((object)$"Saving coord \"{coordinateFile.coordinateName}\" to chara \"{logName}\" / {(ChaFileDefine.CoordinateType)character.fileStatus.coordinateType}");
			ApiEventExecutionLogger eventLogger = ApiEventExecutionLogger.GetEventLogger();
			eventLogger.Begin("OnCoordinateBeingSaved", logName);
			foreach (CharaCustomFunctionController behaviour in GetBehaviours(character))
			{
				eventLogger.PluginStart();
				behaviour.OnCoordinateBeingSavedInternal(coordinateFile);
				eventLogger.PluginEnd(behaviour);
			}
			CoordinateEventArgs args = new CoordinateEventArgs(character, coordinateFile);
			CharacterApi.CoordinateSaving.SafeInvokeWithLogging(delegate(EventHandler<CoordinateEventArgs> handler)
			{
				handler(character, args);
			}, "CoordinateSaving", eventLogger);
			eventLogger.End();
		}

		private static void OnCoordinateBeingLoaded(ChaControl character, ChaFileCoordinate coordinateFile)
		{
			string logName = GetLogName(character?.chaFile);
			KoikatuAPI.Logger.LogDebug((object)$"Loading coord \"{coordinateFile.coordinateName}\" to chara \"{logName}\" / {(ChaFileDefine.CoordinateType)character.fileStatus.coordinateType}");
			ApiEventExecutionLogger eventLogger = ApiEventExecutionLogger.GetEventLogger();
			eventLogger.Begin("OnCoordinateBeingLoaded", logName);
			foreach (CharaCustomFunctionController behaviour in GetBehaviours(character))
			{
				eventLogger.PluginStart();
				behaviour.OnCoordinateBeingLoadedInternal(coordinateFile);
				eventLogger.PluginEnd(behaviour);
			}
			CoordinateEventArgs args = new CoordinateEventArgs(character, coordinateFile);
			CharacterApi.CoordinateLoaded.SafeInvokeWithLogging(delegate(EventHandler<CoordinateEventArgs> handler)
			{
				handler(character, args);
			}, "CoordinateLoaded", eventLogger);
			if (MakerAPI.InsideAndLoaded)
			{
				MakerAPI.OnReloadInterface(character, args, eventLogger);
			}
			eventLogger.End();
		}

		private static string GetLogName(ChaFile chaFile)
		{
			if (chaFile != null)
			{
				return chaFile.GetFancyCharacterName() + " from " + (string.IsNullOrEmpty(chaFile.charaFileName) ? "Unknown" : chaFile.charaFileName);
			}
			return "NULL / ALL CHARACTERS";
		}
	}
	/// <summary>
	/// Extensions for use with ChaControl, ChaFile and similar
	/// </summary>
	public static class CharacterExtensions
	{
		/// <summary>
		/// Function for pulling data from a controller to be set to a maker control.
		/// </summary>
		/// <typeparam name="TController">Type of a custom character function controller.</typeparam>
		/// <typeparam name="TValue">Type of the value used by the control.</typeparam>
		/// <param name="controller">Controller to pull the value from.</param>
		public delegate TValue GetValueForInterface<in TController, out TValue>(TController controller) where TController : CharaCustomFunctionController;

		/// <summary>
		/// Function for pushing data from a control to be set in a controller.
		/// </summary>
		/// <typeparam name="TController">Type of a custom character function controller.</typeparam>
		/// <typeparam name="TValue">Type of the value used by the control.</typeparam>
		/// <param name="controller">Controller to push the value to.</param>
		/// <param name="value">Value to be pushed to the controller.</param>
		public delegate void SetValueToController<in TController, in TValue>(TController controller, TValue value) where TController : CharaCustomFunctionController;

		internal static readonly WeakKeyDictionary<ChaFile, string> ChaFileFullPathLookup = new WeakKeyDictionary<ChaFile, string>();

		/// <summary>
		/// Get ChaControl that is using this ChaFile if any exist.
		/// </summary>
		public static ChaControl GetChaControl(this ChaFile chaFile)
		{
			if (chaFile == null)
			{
				return null;
			}
			ChaControl chaControl = CharacterApi.ChaControls.FirstOrDefault((ChaControl control) => control.chaFile == chaFile);
			if (chaControl != null)
			{
				return chaControl;
			}
			if (Game.Player?.charFile != chaFile)
			{
				return Game.HeroineList.FirstOrDefault((Heroine h) => h.charFile == chaFile)?.chaCtrl;
			}
			return Game.Player.chaCtrl;
		}

		/// <summary>
		/// Get cleaned up full name of this character, including its translation if available.
		/// </summary>
		/// <param name="chaFile">Character to get the name of.</param>
		/// <param name="describeIfEmpty">If the chaFile is null or the name is empty, a string describing this is returned. If set to false, an empty string is returned instead.</param>
		public static string GetFancyCharacterName(this ChaFile chaFile, bool describeIfEmpty = true)
		{
			if (chaFile == null || chaFile.parameter?.fullname == null)
			{
				if (!describeIfEmpty)
				{
					return string.Empty;
				}
				return "[NULL]";
			}
			string text = chaFile.parameter.fullname.Trim();
			if (text.Length == 0)
			{
				if (!describeIfEmpty)
				{
					return string.Empty;
				}
				return "[NO NAME]";
			}
			TranslationHelper.TryTranslate(text, out var translatedText);
			if (translatedText != null)
			{
				translatedText = translatedText.Trim();
				if (translatedText.Length > 0 && text != translatedText)
				{
					return translatedText + " (" + text + ")";
				}
			}
			return text;
		}

		/// <summary>
		/// Synchronize this maker control to the state of your custom <see cref="T:KKAPI.Chara.CharaCustomFunctionController" />.
		/// When the control's value changes the change is sent to the controller, and when the character is reloaded 
		/// the change is automatically pulled from the controller into the control.
		/// </summary>
		/// <param name="guiEntry">Control to bind</param>
		/// <param name="getValue">Function that extracts the new value of the control from the controller.</param>
		/// <param name="setValue">Function that sets the new value from the control on the controller.</param>
		/// <typeparam name="TController">Type of the custom controller to bind the control to.</typeparam>
		/// <typeparam name="TValue">Type of the value used by the control.</typeparam>
		/// <exception cref="T:System.InvalidOperationException">The controller has not been registered in CharacterApi.</exception>
		/// <exception cref="T:System.ArgumentNullException">Any of the parameters is null.</exception>
		public static void BindToFunctionController<TController, TValue>(this BaseEditableGuiEntry<TValue> guiEntry, GetValueForInterface<TController, TValue> getValue, SetValueToController<TController, TValue> setValue) where TController : CharaCustomFunctionController
		{
			if (guiEntry == null)
			{
				throw new ArgumentNullException("guiEntry");
			}
			if (getValue == null)
			{
				throw new ArgumentNullException("getValue");
			}
			if (setValue == null)
			{
				throw new ArgumentNullException("setValue");
			}
			guiEntry.ThrowIfDisposed("guiEntry");
			Type controllerType = typeof(TController);
			if (!CharacterApi.RegisteredHandlers.Select((CharacterApi.ControllerRegistration x) => x.ControllerType).Contains(controllerType))
			{
				throw new InvalidOperationException("The controller " + controllerType.FullName + " has not been registered in CharacterApi");
			}
			guiEntry.ValueChanged.Subscribe(OnValueChanged);
			MakerAPI.ReloadCustomInterface += OnReloadInterface;
			MakerAPI.MakerExiting += OnCleanup;
			void OnCleanup(object sender, EventArgs args)
			{
				MakerAPI.ReloadCustomInterface -= OnReloadInterface;
				MakerAPI.MakerExiting -= OnCleanup;
			}
			void OnReloadInterface(object sender, EventArgs args)
			{
				try
				{
					TController controller = (TController)MakerAPI.GetCharacterControl().GetComponent(controllerType);
					guiEntry.Value = getValue(controller);
				}
				catch (Exception)
				{
				}
			}
			void OnValueChanged(TValue newVal)
			{
				try
				{
					TController controller = (TController)MakerAPI.GetCharacterControl().GetComponent(controllerType);
					setValue(controller, newVal);
				}
				catch (Exception)
				{
				}
			}
		}

		/// <summary>
		/// Gets full path to the file where this ChaFile was loaded from. Usually this means the character card,
		/// but can also point to a studio scene or a game save file if the character was contained inside them.
		/// If the ChaFile was loaded from memory or copied, this will most likely return null. Might not work in maker in some games (todo).
		/// </summary>
		public static string GetSourceFilePath(this ChaFile chaFile)
		{
			ChaFileFullPathLookup.TryGetValue(chaFile, out var value);
			return value;
		}
	}
	/// <summary>
	/// Base type for custom character extensions.
	/// It provides many useful methods that abstract away the nasty hooks needed to figure out when
	/// a character is changed or how to save and load your custom data to the character card.
	///
	/// This controller is a MonoBehaviour that is added to root gameObjects of ALL characters spawned into the game. 
	/// It's recommended to not use constructors, Awake or Start in controllers. Use <see cref="M:KKAPI.Chara.CharaCustomFunctionController.OnReload(KKAPI.GameMode,System.Boolean)" /> instead.
	/// </summary>
	public abstract class CharaCustomFunctionController : MonoBehaviour
	{
		private bool _wasLoaded;

		/// <summary>
		/// ChaControl of the character this controller is attached to. It's on the same gameObject as this controller.
		/// </summary>
		public ChaControl ChaControl { get; private set; }

		/// <summary>
		/// ChaFile of the character this controller is attached to.
		/// </summary>
		public ChaFileControl ChaFileControl => ChaControl.chaFile;

		/// <summary>
		/// ID used for extended data by this controller. It's set when registering the controller
		/// with <see cref="M:KKAPI.Chara.CharacterApi.RegisterExtraBehaviour``1(System.String)" />
		/// </summary>
		public string ExtendedDataId => ControllerRegistration.ExtendedDataId;

		/// <summary>
		/// Definition of this kind of function controllers.
		/// </summary>
		public CharacterApi.ControllerRegistration ControllerRegistration { get; internal set; }

		/// <summary>
		/// True if this controller has been initialized
		/// </summary>
		public bool Started { get; private set; }

		/// <summary>
		/// Currently selected clothes on this character. Can subscribe to listen for changes.
		/// </summary>
		public BehaviorSubject<ChaFileDefine.CoordinateType> CurrentCoordinate { get; private set; }

		/// <summary>
		/// Get extended data based on supplied ExtendedDataId. When in chara maker loads data from character that's being loaded. 
		/// This should be used inside the <see cref="M:KKAPI.Chara.CharaCustomFunctionController.OnReload(KKAPI.GameMode,System.Boolean)" /> event.
		/// Consider using one of the other "Get___ExtData" and "Set___ExtData" methods instead since they are more reliable and handle copying and transferring outfits and they conform to built in maker load toggles.
		/// </summary>
		public PluginData GetExtendedData()
		{
			return GetExtendedData(getFromLoadedChara: true);
		}

		/// <summary>
		/// Get extended data of the current character by using the ID you specified when registering this controller.
		/// This should be used inside the <see cref="M:KKAPI.Chara.CharaCustomFunctionController.OnReload(KKAPI.GameMode,System.Boolean)" /> event.
		/// Consider using one of the other "Get___ExtData" and "Set___ExtData" methods instead since they are more reliable and handle copying and transferring outfits and they conform to built in maker load toggles.
		/// </summary>
		/// <param name="getFromLoadedChara">If true, when in chara maker load data from character that's being loaded. 
		/// When outside maker or false, always grab current character's data.</param>
		public PluginData GetExtendedData(bool getFromLoadedChara)
		{
			if (ExtendedDataId == null)
			{
				throw new ArgumentException("ExtendedDataId");
			}
			ChaFile chaFile = (getFromLoadedChara ? (MakerAPI.LastLoadedChaFile ?? ChaFileControl) : ChaFileControl);
			return ExtendedSave.GetExtendedDataById(chaFile, ExtendedDataId);
		}

		/// <summary>
		/// Save your custom data to the character card under the ID you specified when registering this controller.
		/// This should be used inside the <see cref="M:KKAPI.Chara.CharaCustomFunctionController.OnCardBeingSaved(KKAPI.GameMode)" /> event.
		/// Consider using one of the other "Get___ExtData" and "Set___ExtData" methods instead since they are more reliable and handle copying and transferring outfits and they conform to built in maker load toggles.
		/// </summary>
		/// <param name="data">Your custom data to be written to the character card. Can be null to remove the data.</param>
		public void SetExtendedData(PluginData data)
		{
			if (ExtendedDataId == null)
			{
				throw new ArgumentException("ExtendedDataId");
			}
			ExtendedSave.SetExtendedDataById((ChaFile)ChaFileControl, ExtendedDataId, data);
			if (KoikatuAPI.GetCurrentGameMode() != GameMode.MainGame)
			{
				return;
			}
			Heroine heroine = ChaControl.GetHeroine();
			if (heroine != null)
			{
				ExtendedSave.SetExtendedDataById((ChaFile)heroine.charFile, ExtendedDataId, data);
				if (ChaControl != heroine.chaCtrl)
				{
					ExtendedSave.SetExtendedDataById((ChaFile)heroine.chaCtrl.chaFile, ExtendedDataId, data);
					CharacterApi.Hooks.SetDirty(heroine, dirty: true);
				}
				NPC nPC = heroine.GetNPC();
				if (nPC != null && nPC.chaCtrl != null && nPC.chaCtrl != ChaControl && nPC.chaCtrl != heroine.chaCtrl)
				{
					ExtendedSave.SetExtendedDataById((ChaFile)nPC.chaCtrl.chaFile, ExtendedDataId, data);
					CharacterApi.Hooks.SetDirty(heroine, dirty: true);
				}
				return;
			}
			SaveData.Player player = ChaControl.GetPlayer();
			if (player != null)
			{
				ExtendedSave.SetExtendedDataById((ChaFile)player.charFile, ExtendedDataId, data);
				if (ChaControl != player.chaCtrl)
				{
					ExtendedSave.SetExtendedDataById((ChaFile)player.chaCtrl.chaFile, ExtendedDataId, data);
					CharacterApi.Hooks.SetDirty(player, dirty: true);
				}
			}
		}

		/// <summary>
		/// Get extended data of the specified coordinate by using the ID you specified when registering this controller.
		/// This should be used inside the <see cref="M:KKAPI.Chara.CharaCustomFunctionController.OnCoordinateBeingLoaded(ChaFileCoordinate,System.Boolean)" /> event.
		/// Consider using one of the other "Get___ExtData" and "Set___ExtData" methods instead since they are more reliable and handle copying and transferring outfits and they conform to built in maker load toggles.
		/// </summary>
		/// <param name="coordinate">Coordinate you want to get the data from</param>
		public PluginData GetCoordinateExtendedData(ChaFileCoordinate coordinate)
		{
			if (coordinate == null)
			{
				throw new ArgumentNullException("coordinate");
			}
			if (ExtendedDataId == null)
			{
				throw new ArgumentException("ExtendedDataId");
			}
			return ExtendedSave.GetExtendedDataById(coordinate, ExtendedDataId);
		}

		/// <summary>
		/// Set extended data to the specified coordinate by using the ID you specified when registering this controller.
		/// This should be used inside the <see cref="M:KKAPI.Chara.CharaCustomFunctionController.OnCoordinateBeingSaved(ChaFileCoordinate)" /> event.
		/// Consider using one of the other "Get___ExtData" and "Set___ExtData" methods instead since they are more reliable and handle copying and transferring outfits and they conform to built in maker load toggles.
		/// </summary>
		/// <param name="coordinate">Coordinate you want to set the data to</param>
		/// <param name="data">Your custom data to be saved to the coordinate card</param>
		public void SetCoordinateExtendedData(ChaFileCoordinate coordinate, PluginData data)
		{
			if (coordinate == null)
			{
				throw new ArgumentNullException("coordinate");
			}
			if (ExtendedDataId == null)
			{
				throw new ArgumentException("ExtendedDataId");
			}
			ExtendedSave.SetExtendedDataById(coordinate, ExtendedDataId, data);
		}

		/// <summary>
		/// Fired when the character information is being saved.
		/// It handles all types of saving (to character card, to a scene etc.)
		/// Write any of your extended data in this method by using <see cref="M:KKAPI.Chara.CharaCustomFunctionController.SetExtendedData(ExtensibleSaveFormat.PluginData)" />.
		/// Avoid reusing old PluginData since we might no longer be pointed to the same character.
		/// </summary>
		protected abstract void OnCardBeingSaved(GameMode currentGameMode);

		internal void OnCardBeingSavedInternal()
		{
			OnCardBeingSavedInternal(KoikatuAPI.GetCurrentGameMode());
		}

		internal void OnCardBeingSavedInternal(GameMode gamemode)
		{
			if (!_wasLoaded)
			{
				KoikatuAPI.Logger.LogWarning((object)("Tried to save card before it was loaded - " + ChaFileControl.GetFancyCharacterName()));
				return;
			}
			try
			{
				OnCardBeingSaved(gamemode);
			}
			catch (Exception ex)
			{
				KoikatuAPI.Logger.LogError((object)ex);
			}
		}

		/// <summary>
		/// OnReload is fired whenever the character's state needs to be updated.
		/// This might be beacuse the character was just loaded into the game, 
		/// was replaced with a different character, etc.
		/// Use this method instead of Awake and Start. It will always get called
		/// before other methods, but after the character is in a usable state.
		/// WARNING: Make sure to completely reset your state in this method!
		///          Assume that all of your variables are no longer valid!
		/// </summary>
		/// <param name="currentGameMode">Game mode we are currently in</param>
		/// <param name="maintainState">If true, the current state should be preserved.
		/// Do not load new extended data, instead reuse what you currently have or do nothing.</param>
		protected virtual void OnReload(GameMode currentGameMode, bool maintainState)
		{
		}

		/// <summary>
		/// OnReload is fired whenever the character's state needs to be updated.
		/// This might be beacuse the character was just loaded into the game, 
		/// was replaced with a different character, etc.
		/// Use this method instead of Awake and Start. It will always get called
		/// before other methods, but after the character is in a usable state.
		/// WARNING: Make sure to completely reset your state in this method!
		///          Assume that all of your variables are no longer valid!
		/// WARNING: Will not get fired if disabled in <see cref="P:KKAPI.Chara.CharacterApi.RegisteredHandlers" />, 
		/// use overloads with maintainState parameter in that case.
		/// </summary>
		/// <param name="currentGameMode">Game mode we are currently in</param>
		protected virtual void OnReload(GameMode currentGameMode)
		{
		}

		internal void OnReloadInternal()
		{
			OnReloadInternal(KoikatuAPI.GetCurrentGameMode());
		}

		internal void OnReloadInternal(GameMode currentGameMode)
		{
			if (currentGameMode == GameMode.MainGame)
			{
				CharacterApi.Hooks.SetDirty(ChaControl.GetHeroine(), dirty: false);
			}
			try
			{
				if (!ControllerRegistration.MaintainState)
				{
					OnReload(currentGameMode);
				}
				OnReload(currentGameMode, ControllerRegistration.MaintainState);
				_wasLoaded = true;
			}
			catch (Exception ex)
			{
				KoikatuAPI.Logger.LogError((object)ex);
			}
		}

		/// <summary>
		/// Fired just before current coordinate is saved to a coordinate card. Use <see cref="M:KKAPI.Chara.CharaCustomFunctionController.SetCoordinateExtendedData(ChaFileCoordinate,ExtensibleSaveFormat.PluginData)" /> to save data to it. 
		/// You might need to wait for the next frame with <see cref="M:UnityEngine.MonoBehaviour.StartCoroutine(System.Collections.IEnumerator)" /> before handling this.
		/// </summary>
		protected virtual void OnCoordinateBeingSaved(ChaFileCoordinate coordinate)
		{
		}

		internal void OnCoordinateBeingSavedInternal(ChaFileCoordinate coordinate)
		{
			if (!_wasLoaded)
			{
				KoikatuAPI.Logger.LogWarning((object)("Tried to save coordinate before the character was loaded - " + ChaFileControl.charaFileName));
			}
			try
			{
				OnCoordinateBeingSaved(coordinate);
			}
			catch (Exception ex)
			{
				KoikatuAPI.Logger.LogError((object)ex);
			}
		}

		/// <summary>
		/// Fired just after loading a coordinate card into the current coordinate slot.
		/// Use <see cref="M:KKAPI.Chara.CharaCustomFunctionController.GetCoordinateExtendedData(ChaFileCoordinate)" /> to get save data of the loaded coordinate.
		/// </summary>
		/// <param name="coordinate">Coordinate being currently loaded.</param>
		/// <param name="maintainState">If true, the current state should be preserved.
		/// Do not load new extended data, instead reuse what you currently have or do nothing.</param>
		protected virtual void OnCoordinateBeingLoaded(ChaFileCoordinate coordinate, bool maintainState)
		{
		}

		/// <summary>
		/// Fired just after loading a coordinate card into the current coordinate slot.
		/// Use <see cref="M:KKAPI.Chara.CharaCustomFunctionController.GetCoordinateExtendedData(ChaFileCoordinate)" /> to get save data of the loaded coordinate.
		/// Will not get fired if disabled in <see cref="P:KKAPI.Chara.CharacterApi.RegisteredHandlers" />, 
		/// use overloads with maintainState parameter in that case.
		/// </summary>
		/// <param name="coordinate">Coordinate being currently loaded.</param>
		protected virtual void OnCoordinateBeingLoaded(ChaFileCoordinate coordinate)
		{
		}

		internal void OnCoordinateBeingLoadedInternal(ChaFileCoordinate coordinate)
		{
			if (!_wasLoaded)
			{
				StartCoroutine(new WaitUntil(() => _wasLoaded).AppendCo(delegate
				{
					OnCoordinateBeingLoadedInternal(coordinate);
				}));
				return;
			}
			try
			{
				if (!ControllerRegistration.MaintainCoordinateState)
				{
					OnCoordinateBeingLoaded(coordinate);
				}
				OnCoordinateBeingLoaded(coordinate, ControllerRegistration.MaintainCoordinateState);
			}
			catch (Exception ex)
			{
				KoikatuAPI.Logger.LogError((object)ex);
			}
		}

		/// <summary>
		/// Warning: When overriding make sure to call the base method at the end of your logic!
		/// </summary>
		protected virtual void Update()
		{
		}

		/// <summary>
		/// Warning: When overriding make sure to call the base method at the end of your logic!
		/// </summary>
		protected virtual void OnDestroy()
		{
			CurrentCoordinate.Dispose();
		}

		/// <summary>
		/// Warning: When overriding make sure to call the base method at the end of your logic!
		/// </summary>
		protected virtual void OnEnable()
		{
			if (Started)
			{
				OnReloadInternal(KoikatuAPI.GetCurrentGameMode());
			}
		}

		/// <summary>
		/// Warning: When overriding make sure to call the base method at the end of your logic!
		/// </summary>
		protected virtual void Awake()
		{
			ChaControl = GetComponent<ChaControl>();
			CurrentCoordinate = new BehaviorSubject<ChaFileDefine.CoordinateType>((ChaFileDefine.CoordinateType)ChaControl.fileStatus.coordinateType);
		}

		/// <summary>
		/// Warning: When overriding make sure to call the base method at the end of your logic!
		/// </summary>
		protected virtual void Start()
		{
			Started = true;
			OnReloadInternal(KoikatuAPI.GetCurrentGameMode());
		}

		private ChaFileCoordinate GetCoordinate(int coordinateId)
		{
			KoikatuAPI.Assert(ChaControl.nowCoordinate != null, "ChaControl.nowCoordinate != null");
			if (coordinateId >= 0)
			{
				return ChaFileControl.coordinate[coordinateId];
			}
			return ChaControl.nowCoordinate;
		}

		private ChaFile GetExtDataTargetChaFile(bool setDirty)
		{
			if (KoikatuAPI.GetCurrentGameMode() == GameMode.MainGame)
			{
				Heroine heroine = ChaControl.GetHeroine();
				if (heroine != null)
				{
					if (setDirty)
					{
						CharacterApi.Hooks.SetDirty(heroine, dirty: true);
					}
					return heroine.charFile;
				}
				SaveData.Player player = ChaControl.GetPlayer();
				if (player != null)
				{
					if (setDirty)
					{
						CharacterApi.Hooks.SetDirty(player, dirty: true);
					}
					return player.charFile;
				}
			}
			return ChaFileControl;
		}

		/// <summary>
		/// Get extended data for specific clothes.
		/// Do not store this data because it might change without notice, for example when clothing is copied. Always call Get at the point where you need the data, not earlier.
		/// If you change any of the data, remember to call the corresponding Set method or the change might not be saved.
		/// This data is saved alongside game data, which means it is automatically copied and moved as necessary.
		/// If no extended data of this plugin was set yet, this method will return null.
		/// In maker, you can update controls that use this data in the <see cref="E:KKAPI.Maker.MakerAPI.ReloadCustomInterface" /> event.
		/// </summary>
		/// <param name="coordinateId">The coordinate number to open extened data for if the game supports multiple coords (0-indexed). -1 will use the current coordinate.</param>
		public PluginData GetClothesExtData(int coordinateId = -1)
		{
			ChaFileCoordinate coordinate = GetCoordinate(coordinateId);
			KoikatuAPI.Assert(coordinate != null, "coord != null");
			KoikatuAPI.Assert(coordinate.clothes != null, "coord.clothes != null");
			ChaFileClothes clothes = coordinate.clothes;
			PluginData result = default(PluginData);
			Extensions.TryGetExtendedDataById(clothes, ExtendedDataId, ref result);
			return result;
		}

		/// <summary>
		/// Get extended data for a specific accessory.
		/// Do not store this data because it might change without notice, for example when clothing is copied. Always call Get at the point where you need the data, not earlier.
		/// If you change any of the data, remember to call the corresponding Set method or the change might not be saved.
		/// This data is saved alongside game data, which means it is automatically copied and moved as necessary.
		/// If no extended data of this plugin was set yet, this method will return null.
		/// In maker, you can update controls that use this data in the <see cref="E:KKAPI.Maker.MakerAPI.ReloadCustomInterface" /> event.
		/// </summary>
		/// <param name="accessoryPartId">The accessory part number to open extened data for (0-indexed).</param>
		/// <param name="coordinateId">The coordinate number to open extened data for if the game supports multiple coords (0-indexed). -1 will use the current coordinate.</param>
		public PluginData GetAccessoryExtData(int accessoryPartId, int coordinateId = -1)
		{
			ChaFileCoordinate coordinate = GetCoordinate(coordinateId);
			KoikatuAPI.Assert(coordinate != null, "coord != null");
			KoikatuAPI.Assert(coordinate.accessory != null, "coord.accessory != null");
			ChaFileAccessory.PartsInfo partsInfo = coordinate.accessory.parts[accessoryPartId];
			PluginData result = default(PluginData);
			Extensions.TryGetExtendedDataById(partsInfo, ExtendedDataId, ref result);
			return result;
		}

		/// <summary>
		/// Get extended data for character's body (body sliders, tattoos).
		/// Do not store this data because it might change without notice, for example when clothing is copied. Always call Get at the point where you need the data, not earlier.
		/// If you change any of the data, remember to call the corresponding Set method or the change might not be saved.
		/// This data is saved alongside game data, which means it is automatically copied and moved as necessary.
		/// If no extended data of this plugin was set yet, this method will return null.
		/// In maker, you can update controls that use this data in the <see cref="E:KKAPI.Maker.MakerAPI.ReloadCustomInterface" /> event.
		/// </summary>
		public PluginData GetBodyExtData()
		{
			ChaFile extDataTargetChaFile = GetExtDataTargetChaFile(setDirty: false);
			KoikatuAPI.Assert(extDataTargetChaFile.custom != null, "chafile.custom != null");
			KoikatuAPI.Assert(extDataTargetChaFile.custom.body != null, "chafile.custom.body != null");
			PluginData result = default(PluginData);
			Extensions.TryGetExtendedDataById(extDataTargetChaFile.custom.body, ExtendedDataId, ref result);
			return result;
		}

		/// <summary>
		/// Get extended data for character's face (face sliders, eye settings).
		/// Do not store this data because it might change without notice, for example when clothing is copied. Always call Get at the point where you need the data, not earlier.
		/// If you change any of the data, remember to call the corresponding Set method or the change might not be saved.
		/// This data is saved alongside game data, which means it is automatically copied and moved as necessary.
		/// If no extended data of this plugin was set yet, this method will return null.
		/// In maker, you can update controls that use this data in the <see cref="E:KKAPI.Maker.MakerAPI.ReloadCustomInterface" /> event.
		/// </summary>
		public PluginData GetFaceExtData()
		{
			ChaFile extDataTargetChaFile = GetExtDataTargetChaFile(setDirty: false);
			KoikatuAPI.Assert(extDataTargetChaFile.custom != null, "chafile.custom != null");
			KoikatuAPI.Assert(extDataTargetChaFile.custom.face != null, "chafile.custom.face != null");
			PluginData result = default(PluginData);
			Extensions.TryGetExtendedDataById(extDataTargetChaFile.custom.face, ExtendedDataId, ref result);
			return result;
		}

		/// <summary>
		/// Get extended data for character's parameters (personality, preferences, traits).
		/// Do not store this data because it might change without notice, for example when clothing is copied. Always call Get at the point where you need the data, not earlier.
		/// If you change any of the data, remember to call the corresponding Set method or the change might not be saved.
		/// This data is saved alongside game data, which means it is automatically copied and moved as necessary.
		/// If no extended data of this plugin was set yet, this method will return null.
		/// In maker, you can update controls that use this data in the <see cref="E:KKAPI.Maker.MakerAPI.ReloadCustomInterface" /> event.
		/// </summary>
		public PluginData GetParameterExtData()
		{
			ChaFile extDataTargetChaFile = GetExtDataTargetChaFile(setDirty: false);
			KoikatuAPI.Assert(extDataTargetChaFile.parameter != null, "chafile.parameter != null");
			PluginData result = default(PluginData);
			Extensions.TryGetExtendedDataById(extDataTargetChaFile.parameter, ExtendedDataId, ref result);
			return result;
		}

		/// <summary>
		/// Set extended data for specific clothes.
		/// Always call Set right after changing any of the data, or the change might not be saved if the data is changed for whatever reason (clothing change, reload, etc.)
		/// This data is saved alongside game data, which means it is automatically copied and moved as necessary.
		/// </summary>
		/// <param name="data">Extended data to save.</param>
		/// <param name="coordinateId">The coordinate number to open extened data for if the game supports multiple coords (0-indexed). -1 will use the current coordinate.</param>
		public void SetClothesExtData(PluginData data, int coordinateId = -1)
		{
			ChaFileCoordinate coordinate = GetCoordinate(coordinateId);
			KoikatuAPI.Assert(coordinate != null, "coord != null");
			KoikatuAPI.Assert(coordinate.clothes != null, "coord.clothes != null");
			ChaFileClothes clothes = coordinate.clothes;
			Extensions.SetExtendedDataById(clothes, ExtendedDataId, data);
		}

		/// <summary>
		/// Set extended data for a specific accessory.
		/// Always call Set right after changing any of the data, or the change might not be saved if the data is changed for whatever reason (clothing change, reload, etc.)
		/// This data is saved alongside game data, which means it is automatically copied and moved as necessary.
		/// </summary>
		/// <param name="data">Extended data to save.</param>
		/// <param name="accessoryPartId">The accessory part number to open extened data for (0-indexed).</param>
		/// <param name="coordinateId">The coordinate number to open extened data for if the game supports multiple coords (0-indexed). -1 will use the current coordinate.</param>
		public void SetAccessoryExtData(PluginData data, int accessoryPartId, int coordinateId = -1)
		{
			ChaFileCoordinate coordinate = GetCoordinate(coordinateId);
			KoikatuAPI.Assert(coordinate != null, "coord != null");
			KoikatuAPI.Assert(coordinate.accessory != null, "coord.accessory != null");
			ChaFileAccessory.PartsInfo partsInfo = coordinate.accessory.parts[accessoryPartId];
			Extensions.SetExtendedDataById(partsInfo, ExtendedDataId, data);
		}

		/// <summary>
		/// Set extended data for character's body (body sliders, tattoos).
		/// Always call Set right after changing any of the data, or the change might not be saved if the data is changed for whatever reason (clothing change, reload, etc.)
		/// This data is saved alongside game data, which means it is automatically copied and moved as necessary.
		/// </summary>
		/// <param name="data">Extended data to save.</param>
		public void SetBodyExtData(PluginData data)
		{
			ChaFile extDataTargetChaFile = GetExtDataTargetChaFile(setDirty: true);
			KoikatuAPI.Assert(extDataTargetChaFile.custom != null, "chafile.custom != null");
			KoikatuAPI.Assert(extDataTargetChaFile.custom.body != null, "chafile.custom.body != null");
			Extensions.SetExtendedDataById(extDataTargetChaFile.custom.body, ExtendedDataId, data);
			if (extDataTargetChaFile != ChaFileControl)
			{
				Extensions.SetExtendedDataById(ChaFileControl.custom.body, ExtendedDataId, data);
			}
		}

		/// <summary>
		/// Set extended data for character's face (face sliders, eye settings).
		/// Always call Set right after changing any of the data, or the change might not be saved if the data is changed for whatever reason (clothing change, reload, etc.)
		/// This data is saved alongside game data, which means it is automatically copied and moved as necessary.
		/// </summary>
		/// <param name="data">Extended data to save.</param>
		public void SetFaceExtData(PluginData data)
		{
			ChaFile extDataTargetChaFile = GetExtDataTargetChaFile(setDirty: true);
			KoikatuAPI.Assert(extDataTargetChaFile.custom != null, "chafile.custom != null");
			KoikatuAPI.Assert(extDataTargetChaFile.custom.face != null, "chafile.custom.face != null");
			Extensions.SetExtendedDataById(extDataTargetChaFile.custom.face, ExtendedDataId, data);
			if (extDataTargetChaFile != ChaFileControl)
			{
				Extensions.SetExtendedDataById(ChaFileControl.custom.face, ExtendedDataId, data);
			}
		}

		/// <summary>
		/// Set extended data for character's parameters (personality, preferences, traits).
		/// Always call Set right after changing any of the data, or the change might not be saved if the data is changed for whatever reason (clothing change, reload, etc.)
		/// This data is saved alongside game data, which means it is automatically copied and moved as necessary.
		/// </summary>
		/// <param name="data">Extended data to save.</param>
		public void SetParameterExtData(PluginData data)
		{
			ChaFile extDataTargetChaFile = GetExtDataTargetChaFile(setDirty: true);
			KoikatuAPI.Assert(extDataTargetChaFile.parameter != null, "chafile.parameter != null");
			Extensions.SetExtendedDataById(extDataTargetChaFile.parameter, ExtendedDataId, data);
			if (extDataTargetChaFile != ChaFileControl)
			{
				Extensions.SetExtendedDataById(ChaFileControl.parameter, ExtendedDataId, data);
			}
		}
	}
	/// <summary>
	/// Event arguments used by character reload events
	/// </summary>
	public sealed class CharaReloadEventArgs : EventArgs
	{
		/// <summary>
		/// Can be null when all characters in a scene are reloaded
		/// </summary>
		public ChaControl ReloadedCharacter { get; }

		public CharaReloadEventArgs(ChaControl reloadedCharacter)
		{
			ReloadedCharacter = reloadedCharacter;
		}
	}
	/// <summary>
	/// Fired in events that deal with coordinate cards
	/// </summary>
	public sealed class CoordinateEventArgs : EventArgs
	{
		/// <summary>
		/// Character the coordinate was loaded to
		/// </summary>
		public ChaControl Character { get; }

		/// <summary>
		/// The loaded coordinate
		/// </summary>
		public ChaFileCoordinate LoadedCoordinate { get; }

		public CoordinateEventArgs(ChaControl character, ChaFileCoordinate loadedCoordinate)
		{
			Character = character;
			LoadedCoordinate = loadedCoordinate;
		}

		/// <summary>
		/// Get all exrtended data assigned to this coordinate card
		/// </summary>
		public Dictionary<string, PluginData> GetCoordinateExtData()
		{
			return ExtendedSave.GetAllExtendedData(LoadedCoordinate);
		}

		/// <summary>
		/// Set extended data for this coordinate card
		/// </summary>
		/// <param name="dataId">Key to save the data under (usually plugin GUID)</param>
		/// <param name="data">Data to set</param>
		public void SetCoordinateExtData(string dataId, PluginData data)
		{
			ExtendedSave.SetExtendedDataById(LoadedCoordinate, dataId, data);
		}
	}
	internal sealed class TestCharaCustomFunctionController : CharaCustomFunctionController
	{
		public string id;

		public static int No;

		protected override void OnCardBeingSaved(GameMode currentGameMode)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Expected O, but got Unknown
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Expected O, but got Unknown
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_0075: Expected O, but got Unknown
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_009c: Expected O, but got Unknown
			SetParameterExtData(new PluginData
			{
				data = new Dictionary<string, object> { { "id", id } }
			});
			SetBodyExtData(new PluginData
			{
				data = new Dictionary<string, object> { { "id", id } }
			});
			SetFaceExtData(new PluginData
			{
				data = new Dictionary<string, object> { { "id", id } }
			});
			SetExtendedData(new PluginData
			{
				data = new Dictionary<string, object> { { "id", id } }
			});
			Console.WriteLine("Save ID to ext data - " + id + "  |  Chara - " + base.ChaControl.name);
			KoikatuAPI.Logger.LogWarning((object)$"event:OnCardBeingSaved  chara:{base.ChaControl.name}  currentGameMode:{currentGameMode}");
		}

		protected override void OnReload(GameMode currentGameMode, bool maintainState)
		{
			//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0208: Expected O, but got Unknown
			//IL_0209: Unknown result type (might be due to invalid IL or missing references)
			//IL_020e: Unknown result type (might be due to invalid IL or missing references)
			//IL_022f: Expected O, but got Unknown
			//IL_0230: Unknown result type (might be due to invalid IL or missing references)
			//IL_0235: Unknown result type (might be due to invalid IL or missing references)
			//IL_0256: Expected O, but got Unknown
			//IL_0257: Unknown result type (might be due to invalid IL or missing references)
			//IL_025c: Unknown result type (might be due to invalid IL or missing references)
			//IL_027d: Expected O, but got Unknown
			PluginData extendedData = GetExtendedData();
			PluginData bodyExtData = GetBodyExtData();
			PluginData parameterExtData = GetParameterExtData();
			PluginData faceExtData = GetFaceExtData();
			KoikatuAPI.Assert((extendedData == null && bodyExtData == null && parameterExtData == null && faceExtData == null) || (extendedData != null && bodyExtData != null && parameterExtData != null && faceExtData != null), "ext data not lining up");
			if (extendedData != null)
			{
				KoikatuAPI.Assert(bodyExtData != null, "b != null");
				KoikatuAPI.Assert(parameterExtData != null, "c != null");
				KoikatuAPI.Assert(faceExtData != null, "d != null");
				string text = extendedData.data["id"] as string;
				KoikatuAPI.Assert(text == bodyExtData?.data["id"] as string, "a.data[\"id\"] == b.data[\"id\"]");
				KoikatuAPI.Assert(text == parameterExtData?.data["id"] as string, "a.data[\"id\"] == c.data[\"id\"]");
				KoikatuAPI.Assert(text == faceExtData?.data["id"] as string, "a.data[\"id\"] == d.data[\"id\"]");
				Console.WriteLine("ID get from ext data - " + text + "  |  Old ID - " + id + "  |  Chara - " + base.ChaControl.name);
				id = text;
			}
			if (id == null)
			{
				id = $"{No++} - {SceneApi.GetLoadSceneName()} - {SceneApi.GetAddSceneName()}";
				Console.WriteLine("New ID assigned - " + id + "  |  Chara - " + base.ChaControl.name + " | " + base.ChaFileControl.GetFancyCharacterName());
				SetParameterExtData(new PluginData
				{
					data = new Dictionary<string, object> { { "id", id } }
				});
				SetBodyExtData(new PluginData
				{
					data = new Dictionary<string, object> { { "id", id } }
				});
				SetFaceExtData(new PluginData
				{
					data = new Dictionary<string, object> { { "id", id } }
				});
				SetExtendedData(new PluginData
				{
					data = new Dictionary<string, object> { { "id", id } }
				});
			}
			KoikatuAPI.Logger.LogWarning((object)$"event:OnReload  chara:{base.ChaControl.name}  currentGameMode:{currentGameMode}  maintainState:{maintainState}");
		}

		protected override void OnCoordinateBeingLoaded(ChaFileCoordinate coordinate, bool maintainState)
		{
			KoikatuAPI.Logger.LogWarning((object)$"event:OnCoordinateBeingLoaded  chara:{base.ChaControl.name}  coordinate:{coordinate?.coordinateFileName}; maintainState:{maintainState}");
		}

		protected override void OnCoordinateBeingSaved(ChaFileCoordinate coordinate)
		{
			KoikatuAPI.Logger.LogWarning((object)("event:OnCoordinateBeingSaved  chara:" + base.ChaControl.name + "  coordinate:" + coordinate?.coordinateFileName));
		}
	}
}
namespace KKAPI.Maker
{
	/// <summary>
	/// Event args for accessory copy events.
	/// </summary>
	public class AccessoryCopyEventArgs : EventArgs
	{
		/// <summary>
		/// Indexes of accessories that were selected to be copied.
		/// </summary>
		public IEnumerable<int> CopiedSlotIndexes { get; }

		/// <summary>
		/// Coordinate the accessories are copied from.
		/// </summary>
		public ChaFileDefine.CoordinateType CopySource { get; }

		/// <summary>
		/// Coordinate the accessories are copied into.
		/// </summary>
		public ChaFileDefine.CoordinateType CopyDestination { get; }

		/// <inheritdoc />
		public AccessoryCopyEventArgs(IEnumerable<int> copiedSlotIndexes, ChaFileDefine.CoordinateType copySource, ChaFileDefine.CoordinateType copyDestination)
		{
			CopiedSlotIndexes = copiedSlotIndexes ?? throw new ArgumentNullException("copiedSlotIndexes");
			CopySource = copySource;
			CopyDestination = copyDestination;
		}
	}
	/// <summary>
	/// Specifies which parts of the character will be loaded when loading a card in character maker.
	/// (It's the toggles at the bottom of load window) Only includes stock toggles.
	/// </summary>
	public sealed class CharacterLoadFlags
	{
		public bool Clothes;

		public bool Face;

		public bool Hair;

		public bool Body;

		public bool Parameters;
	}
	/// <summary>
	/// Specifies which parts of the coordinate will be loaded when loading a clothing card in character maker.
	/// </summary>
	public sealed class CoordinateLoadFlags
	{
		public bool Clothes;

		public bool Accessories;
	}
	public sealed class ChaFileLoadedEventArgs : EventArgs
	{
		public string Filename { get; }

		public byte Sex { get; }

		public bool Face { get; }

		public bool Body { get; }

		public bool Hair { get; }

		public bool Parameter { get; }

		public bool Coordinate { get; }

		public ChaFileControl CharacterInstance { get; }

		/// <summary>
		/// Use this to get extended data on the character
		/// </summary>
		public ChaFile LoadedChaFile { get; }

		public ChaFileLoadedEventArgs(string filename, byte sex, bool face, bool body, bool hair, bool parameter, bool coordinate, ChaFileControl characterInstance, ChaFile loadedChaFile)
		{
			Filename = filename;
			Sex = sex;
			Face = face;
			Body = body;
			Hair = hair;
			Parameter = parameter;
			Coordinate = coordinate;
			CharacterInstance = characterInstance;
			LoadedChaFile = loadedChaFile;
		}
	}
	/// <summary>
	/// Event fired when character maker is starting and plugins are given an opportunity to register custom controls
	/// </summary>
	public class RegisterCustomControlsEvent : EventArgs
	{
		/// <summary>
		/// Add custom controls. If you want to use custom sub categories, register them by calling AddSubCategory.
		/// </summary>
		public T AddControl<T>(T control) where T : BaseGuiEntry
		{
			return MakerAPI.AddControl(control);
		}

		/// <summary>
		/// Add a control to the right sidebar in chara maker (the "Control Panel" where you set eye blinking, mouth expressions etc.)
		/// </summary>
		public T AddSidebarControl<T>(T control) where T : BaseGuiEntry, ISidebarControl
		{
			return MakerAPI.AddSidebarControl(control);
		}

		/// <summary>
		/// Add a toggle to the bottom of the "Load character" window that allows for partial loading of characters.
		/// </summary>
		public MakerLoadToggle AddLoadToggle(MakerLoadToggle toggle)
		{
			return MakerLoadToggle.AddLoadToggle(toggle);
		}

		/// <summary>
		/// Add a toggle to the bottom of the "Load coordinate/clothes" window that allows for partial loading of coordinate cards.
		/// </summary>
		public MakerCoordinateLoadToggle AddCoordinateLoadToggle(MakerCoordinateLoadToggle toggle)
		{
			return MakerCoordinateLoadToggle.AddLoadToggle(toggle);
		}
	}
	/// <summary>
	/// Event fired when character maker is starting and plugins are given an opportunity to register custom categories and controls
	/// </summary>
	public class RegisterSubCategoriesEvent : RegisterCustomControlsEvent
	{
		/// <summary>
		/// Add custom sub categories. They need to be added before maker starts loading,
		/// or in the RegisterCustomSubCategories event.
		/// </summary>
		public void AddSubCategory(MakerCategory category)
		{
			MakerAPI.AddSubCategory(category);
		}
	}
	/// <summary>
	/// Provides a way to add custom items to the in-game Character Maker, and gives useful methods for interfacing with the maker.
	/// </summary>
	public static class MakerAPI
	{
		private static class Hooks
		{
			private static bool _makerStarting;

			public static ChaFile InternalLastLoadedChaFile;

			public static int _modeSex;

			[HarmonyPrefix]
			[HarmonyPatch(typeof(UI_ToggleGroupCtrl), "Initialize")]
			public static void HBeforeToggleGroupStart(UI_ToggleGroupCtrl __instance)
			{
				Transform transform = __instance.transform;
				if (transform != null && transform.parent != null && transform.parent.name == "CvsMenuTree")
				{
					if (!_makerStarting)
					{
						InsideMaker = true;
						_makerStarting = true;
						OnRegisterCustomSubCategories();
						((MonoBehaviour)(object)KoikatuAPI.Instance).StartCoroutine(OnMakerLoadingCo());
					}
					MakerInterfaceCreator.AddMissingSubCategories(__instance);
				}
			}

			private static IEnumerator OnMakerLoadingCo()
			{
				Stopwatch sw = Stopwatch.StartNew();
				yield return CoroutineUtils.WaitForEndOfFrame;
				long sw2 = sw.ElapsedMilliseconds;
				OnMakerStartedLoading();
				for (int i = 0; i < 3; i++)
				{
					yield return null;
				}
				long sw3 = sw.ElapsedMilliseconds - sw2;
				OnMakerBaseLoaded();
				yield return null;
				long num = sw.ElapsedMilliseconds - sw2 - sw3;
				OnCreateCustomControls();
				long sw4 = sw.ElapsedMilliseconds - sw2 - sw3 - num;
				for (int i = 0; i < 2; i++)
				{
					yield return null;
				}
				_makerStarting = false;
				OnMakerFinishedLoading();
				KoikatuAPI.Logger.LogDebug((object)$"Maker loaded in {sw.ElapsedMilliseconds}ms -> 1st frame:{sw2}ms; Maker base:{sw3}ms; Custom controls:{sw4}ms");
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(CustomScene), "Start")]
			public static void CustomScene_Start(CustomScene __instance)
			{
				InsideMaker = Singleton<CustomBase>.Instance != null;
				_modeSex = (__instance.modeNew ? __instance.modeSex : __instance.chaFileCtrl.parameter.sex);
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(CustomScene), "OnDestroy")]
			public static void CustomScene_Destroy()
			{
				OnMakerExiting();
				InsideMaker = false;
				InternalLastLoadedChaFile = null;
				_modeSex = -1;
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(CustomCharaFile), "Initialize")]
			public static void CustomScenePrefix()
			{
				CharaListIsLoading = true;
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(CustomCharaFile), "Initialize")]
			public static void CustomScenePostfix()
			{
				CharaListIsLoading = false;
			}

			public static void ChaFileLoadFilePreHook(ChaFile __instance)
			{
				if (!CharaListIsLoading && InsideMaker && Singleton<CustomBase>.Instance.defChaInfo != __instance)
				{
					InternalLastLoadedChaFile = __instance;
				}
				else
				{
					InternalLastLoadedChaFile = null;
				}
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFileControl), "LoadFileLimited", new Type[]
			{
				typeof(string),
				typeof(byte),
				typeof(bool),
				typeof(bool),
				typeof(bool),
				typeof(bool),
				typeof(bool),
				typeof(bool)
			})]
			public static void ChaFileControl_LoadLimitedPostHook(string filename, byte sex, bool face, bool body, bool hair, bool parameter, bool coordinate, ChaFileControl __instance)
			{
				if (!CharaListIsLoading && InsideMaker)
				{
					OnChaFileLoaded(__instance, new ChaFileLoadedEventArgs(filename, sex, face, body, hair, parameter, coordinate, __instance, InternalLastLoadedChaFile), null);
				}
			}

			/// <summary>
			/// Keep Load button in maker character load list enabled if any of the extra toggles are enabled, but none of the stock ones are. 
			/// </summary>
			[HarmonyPrefix]
			[HarmonyPatch(/*Could not decode attribute arguments.*/)]
			public static void LoadButtonOverride(Selectable __instance, ref bool value)
			{
				if (!value)
				{
					if ((object)__instance == MakerLoadToggle.LoadButton)
					{
						value = MakerLoadToggle.AnyEnabled;
					}
					else if ((object)__instance == MakerCoordinateLoadToggle.LoadButton)
					{
						value = MakerCoordinateLoadToggle.AnyEnabled;
					}
				}
			}

			public static void Init()
			{
				//IL_009a: Unknown result type (might be due to invalid IL or missing references)
				//IL_00a8: Expected O, but got Unknown
				Harmony val = Harmony.CreateAndPatchAll(typeof(Hooks), (string)null);
				MethodInfo methodInfo = (from x in AccessTools.GetDeclaredMethods(typeof(ChaFile))
					where x.Name == "LoadFile" && x.GetParameters().Any((ParameterInfo p) => p.ParameterType == typeof(BinaryReader))
					orderby x.GetParameters().Length descending
					select x).First();
				KoikatuAPI.Logger.LogDebug((object)("Hooking " + GeneralExtensions.FullDescription((MethodBase)methodInfo)));
				val.Patch((MethodBase)methodInfo, new HarmonyMethod(typeof(Hooks), "ChaFileLoadFilePreHook", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			}
		}

		private static bool _insideMaker;

		private static bool _makerLoaded;

		private static GameObject ccwGo;

		/// <summary>
		/// Use to avoid unnecessary processing cards when they are loaded to the character list.
		/// For example, don't load extended data for these characters since it's never used.
		/// </summary>
		public static bool CharaListIsLoading { get; private set; }

		/// <summary>
		/// ChaFile of the character currently opened in maker. Do not use to save extended data, or it will be lost when saving the card.
		/// Use ChaFile from <code>ExtendedSave.CardBeingSaved</code> event to save extended data instead.
		/// </summary>
		public static ChaFile LastLoadedChaFile
		{
			get
			{
				if (!InsideMaker)
				{
					return null;
				}
				object obj = Hooks.InternalLastLoadedChaFile;
				if (obj == null)
				{
					ChaControl characterControl = GetCharacterControl();
					if ((object)characterControl == null)
					{
						return null;
					}
					obj = characterControl.chaFile;
				}
				return (ChaFile)obj;
			}
		}

		/// <summary>
		/// The maker scene is currently loaded. It might still be loading!
		/// </summary>
		public static bool InsideMaker
		{
			get
			{
				return _insideMaker;
			}
			private set
			{
				if (_insideMaker != value)
				{
					_insideMaker = value;
					MakerAPI.InsideMakerChanged.SafeInvoke(delegate(EventHandler handler)
					{
						handler(KoikatuAPI.Instance, EventArgs.Empty);
					});
				}
				if (!_insideMaker)
				{
					_makerLoaded = false;
				}
			}
		}

		/// <summary>
		/// Maker is fully loaded and running
		/// </summary>
		public static bool InsideAndLoaded
		{
			get
			{
				if (InsideMaker)
				{
					return _makerLoaded;
				}
				return false;
			}
		}

		/// <summary>
		/// This event is fired every time the character maker is being loaded, near the very beginning.
		/// This is the only chance to add custom sub categories. Custom controls can be added now on later in <see cref="E:KKAPI.Maker.MakerAPI.MakerBaseLoaded" />.
		/// Warning: All custom subcategories and custom controls are cleared on maker exit and need to be re-added on next maker start.
		/// It's recommended to completely clear your GUI state in <see cref="E:KKAPI.Maker.MakerAPI.MakerExiting" /> in preparation for loading into maker again.
		/// </summary>
		public static event EventHandler<RegisterSubCategoriesEvent> RegisterCustomSubCategories;

		/// <summary>
		/// Early in the process of maker loading. Most game components are initialized and had their Start methods ran.
		/// Warning: Some components and objects might not be loaded or initialized yet, especially if they are mods.
		/// Warning: All custom subcategories and custom controls are cleared on maker exit and need to be re-added on next maker
		/// start.
		/// </summary>
		public static event EventHandler<RegisterCustomControlsEvent> MakerStartedLoading;

		/// <summary>
		/// Maker is fully loaded. Use to load mods that rely on something that is loaded late, else use MakerStartedLoading.
		/// This is the last chance to add custom controls!
		/// Warning: All custom subcategories and custom controls are cleared on maker exit and need to be re-added on next maker
		/// start.
		/// </summary>
		public static event EventHandler<RegisterCustomControlsEvent> MakerBaseLoaded;

		/// <summary>
		/// Maker is fully loaded and the user has control.
		/// Warning: Avoid loading mods or doing anything heavy in this event.
		/// </summary>
		public static event EventHandler MakerFinishedLoading;

		/// <summary>
		/// Fired after the user exits the maker. Use this to clean up any references and resources.
		/// You want to return to the state you were in before maker was loaded.
		/// </summary>
		public static event EventHandler MakerExiting;

		/// <summary>
		/// Fired when the current ChaFile in maker is being changed by loading other cards or coordinates.
		/// This event is only fired when inside the character maker.
		///
		/// You might need to wait for the next frame with <see cref="M:UnityEngine.MonoBehaviour.StartCoroutine(System.Collections.IEnumerator)" /> before handling this.
		/// </summary>
		[Obsolete("Use ReloadCustomInterface instead")]
		public static event EventHandler<ChaFileLoadedEventArgs> ChaFileLoaded;

		/// <summary>
		/// Fired after character or coordinate is loaded in maker, after all controllers had their events fired.
		/// This event is only fired when inside the character maker. Use this to update values of custom controls.
		/// EventArgs can be either <see cref="T:KKAPI.Chara.CharaReloadEventArgs" /> or <see cref="T:KKAPI.Chara.CoordinateEventArgs" /> depending on why the reload happened.
		/// </summary>
		public static event EventHandler ReloadCustomInterface;

		/// <summary>
		/// Fired when the visbility state of accessory controls, added by <see cref="M:KKAPI.Maker.MakerAPI.AddAccessoryWindowControl``1(``0)" />, should change state if not managed automatically by the api.
		/// </summary>
		public static event EventHandler<AccessoryContolVisibilityArgs> AccessoryContolVisibility;

		/// <summary>
		/// Firen whenever <see cref="P:KKAPI.Maker.MakerAPI.InsideMaker" /> changes. This is the earliest event fired when user starts the character maker.
		/// </summary>
		public static event EventHandler InsideMakerChanged;

		/// <summary>
		/// Add custom controls. If you want to use custom sub categories, register them by calling AddSubCategory.
		/// </summary>
		public static T AddControl<T>(T control) where T : BaseGuiEntry
		{
			if (control == null)
			{
				throw new ArgumentNullException("control");
			}
			control.ThrowIfDisposed("control");
			MakerInterfaceCreator.AddControl(control);
			return control;
		}

		/// <summary>
		/// Add custom sub categories. They need to be added before maker starts loading,
		/// or in the <see cref="E:KKAPI.Maker.MakerAPI.RegisterCustomSubCategories" /> event.
		/// </summary>
		internal static void AddSubCategory(MakerCategory category)
		{
			if (category == null)
			{
				throw new ArgumentNullException("category");
			}
			MakerInterfaceCreator.AddSubCategory(category);
		}

		/// <summary>
		/// Add a control to the right sidebar in chara maker (the "Control Panel" where you set eye blinking, mouth expressions etc.)
		/// </summary>
		public static T AddSidebarControl<T>(T control) where T : BaseGuiEntry, ISidebarControl
		{
			if (control == null)
			{
				throw new ArgumentNullException("control");
			}
			control.ThrowIfDisposed("control");
			MakerInterfaceCreator.AddSidebarControl(control);
			return control;
		}

		/// <summary>
		/// Add a control to the accessory selection and settings window.
		/// For editable controls that depend on the selected accessory use <see cref="M:KKAPI.Maker.MakerAPI.AddEditableAccessoryWindowControl``2(``0)" />.
		/// </summary>
		public static T AddAccessoryWindowControl<T>(T control) where T : BaseGuiEntry
		{
			if (control == null)
			{
				throw new ArgumentNullException("control");
			}
			control.ThrowIfDisposed("control");
			MakerInterfaceCreator.AddAccessoryWindowControl(control);
			return control;
		}

		/// <summary>
		/// Add a control to the accessory selection and settings window.
		/// For editable controls that depend on the selected accessory use <see cref="M:KKAPI.Maker.MakerAPI.AddEditableAccessoryWindowControl``2(``0)" />.
		/// To have API control visibility automatically set automate_visible to true.
		/// </summary>
		public static T AddAccessoryWindowControl<T>(T control, bool automate_visible) where T : BaseGuiEntry
		{
			if (control == null)
			{
				throw new ArgumentNullException("control");
			}
			control.ThrowIfDisposed("control");
			MakerInterfaceCreator.AddAccessoryWindowControl(control, automate_visible);
			return control;
		}

		/// <summary>
		/// Add a control to the accessory selection and settings window. The control is wrapped to properly respond to changes in selected accessory slot (has unique values for each slot).
		/// </summary>
		public static AccessoryControlWrapper<T, TVal> AddEditableAccessoryWindowControl<T, TVal>(T control) where T : BaseEditableGuiEntry<TVal>
		{
			if (control == null)
			{
				throw new ArgumentNullException("control");
			}
			control.ThrowIfDisposed("control");
			MakerInterfaceCreator.AddAccessoryWindowControl(control);
			return new AccessoryControlWrapper<T, TVal>(control);
		}

		/// <summary>
		/// Add a control to the accessory selection and settings window. The control is wrapped to properly respond to changes in selected accessory slot (has unique values for each slot).
		/// To have API control visibility automatically set automate_visible to true.
		/// </summary>
		public static AccessoryControlWrapper<T, TVal> AddEditableAccessoryWindowControl<T, TVal>(T control, bool automate_visible) where T : BaseEditableGuiEntry<TVal>
		{
			if (control == null)
			{
				throw new ArgumentNullException("control");
			}
			control.ThrowIfDisposed("control");
			MakerInterfaceCreator.AddAccessoryWindowControl(control, automate_visible);
			return new AccessoryControlWrapper<T, TVal>(control);
		}

		/// <summary>
		/// 0 is male, 1 is female
		/// </summary>
		public static int GetMakerSex()
		{
			return Hooks._modeSex;
		}

		/// <summary>
		/// Returns current maker logic instance.
		/// Same as <see cref="P:Singleton`1.Instance" />
		/// </summary>
		public static CustomBase GetMakerBase()
		{
			return Singleton<CustomBase>.Instance;
		}

		/// <summary>
		/// Get the ChaControl of the character serving as a preview in character maker.
		/// Outside of character maker and early on in maker load process this returns null.
		/// </summary>
		public static ChaControl GetCharacterControl()
		{
			if (!InsideMaker)
			{
				return null;
			}
			return GetMakerBase()?.chaCtrl;
		}

		/// <summary>
		/// Check if the maker was loaded from within classroom select screen in main game
		/// </summary>
		[Obsolete("Not implemented, always false")]
		public static bool IsInsideClassMaker()
		{
			return false;
		}

		/// <summary>
		/// Currently selected maker coordinate
		/// </summary>
		public static ChaFileDefine.CoordinateType GetCurrentCoordinateType()
		{
			return (ChaFileDefine.CoordinateType)GetMakerBase().chaCtrl.fileStatus.coordinateType;
		}

		private static void OnRegisterCustomSubCategories()
		{
			MakerInterfaceCreator.InitializeMaker();
			RegisterSubCategoriesEvent args = new RegisterSubCategoriesEvent();
			MakerAPI.RegisterCustomSubCategories.SafeInvoke(delegate(EventHandler<RegisterSubCategoriesEvent> handler)
			{
				handler(KoikatuAPI.Instance, args);
			});
		}

		private static void OnMakerStartedLoading()
		{
			RegisterCustomControlsEvent args = new RegisterCustomControlsEvent();
			MakerAPI.MakerStartedLoading.SafeInvoke(delegate(EventHandler<RegisterCustomControlsEvent> handler)
			{
				handler(KoikatuAPI.Instance, args);
			});
		}

		private static void OnMakerFinishedLoading()
		{
			MakerAPI.MakerFinishedLoading.SafeInvoke(delegate(EventHandler handler)
			{
				handler(KoikatuAPI.Instance, EventArgs.Empty);
			});
			AccessoriesApi.AutomaticControlVisibility();
			_makerLoaded = true;
		}

		private static void OnMakerBaseLoaded()
		{
			RegisterCustomControlsEvent args = new RegisterCustomControlsEvent();
			MakerAPI.MakerBaseLoaded.SafeInvoke(delegate(EventHandler<RegisterCustomControlsEvent> handler)
			{
				handler(KoikatuAPI.Instance, args);
			});
			MakerInterfaceCreator.InitializeGuiEntries();
		}

		private static void OnCreateCustomControls()
		{
			ApiEventExecutionLogger eventLogger = ApiEventExecutionLogger.GetEventLogger();
			eventLogger.Begin("OnCreateCustomControls", null);
			eventLogger.PluginStart();
			MakerInterfaceCreator.CreateCustomControls();
			eventLogger.PluginEnd("MakerInterfaceCreator.CreateCustomControls");
			OnChaFileLoaded(KoikatuAPI.Instance, new ChaFileLoadedEventArgs(null, (byte)GetMakerSex(), face: true, body: true, hair: true, parameter: true, coordinate: true, GetCharacterControl().chaFile, LastLoadedChaFile), eventLogger);
			OnReloadInterface(KoikatuAPI.Instance, new CharaReloadEventArgs(GetCharacterControl()), eventLogger);
			eventLogger.End();
		}

		private static void OnMakerExiting()
		{
			MakerAPI.MakerExiting.SafeInvoke(delegate(EventHandler handler)
			{
				handler(KoikatuAPI.Instance, EventArgs.Empty);
			});
			MakerInterfaceCreator.RemoveCustomControls();
		}

		private static void OnChaFileLoaded(object sender, ChaFileLoadedEventArgs args, ApiEventExecutionLogger eventLogger)
		{
			MakerAPI.ChaFileLoaded.SafeInvokeWithLogging(delegate(EventHandler<ChaFileLoadedEventArgs> handler)
			{
				handler(sender, args);
			}, "ChaFileLoaded", eventLogger);
		}

		internal static void OnReloadInterface(object sender, EventArgs args, ApiEventExecutionLogger eventLogger)
		{
			MakerAPI.ReloadCustomInterface.SafeInvokeWithLogging(delegate(EventHandler handler)
			{
				handler(sender, args);
			}, "ReloadCustomInterface", eventLogger);
			AccessoriesApi.AutomaticControlVisibility();
		}

		internal static void OnVisibilityTrigger(AccessoryContolVisibilityArgs args)
		{
			MakerAPI.AccessoryContolVisibility.SafeInvoke(delegate(EventHandler<AccessoryContolVisibilityArgs> handler)
			{
				handler(KoikatuAPI.Instance, args);
			});
		}

		/// <summary>
		/// Get values of the default partial load checkboxes present at the bottom of the 
		/// character load window (load face, body, hair, parameters, clothes).
		/// Returns null if the values could not be collected (safe to assume it's the same as being enabled).
		/// </summary>
		public static CharacterLoadFlags GetCharacterLoadFlags()
		{
			if (!InsideAndLoaded)
			{
				return null;
			}
			return MakerInterfaceCreator.GetCharacterLoadFlags();
		}

		/// <summary>
		/// Get which parts of the coordinate will be loaded when loading a clothing card in character maker.
		/// Returns null if the values could not be collected (safe to assume it's the same as being enabled).
		/// </summary>
		public static CoordinateLoadFlags GetCoordinateLoadFlags()
		{
			if (!InsideAndLoaded)
			{
				return null;
			}
			return MakerInterfaceCreator.GetCoordinateLoadFlags();
		}

		internal static void Init(bool insideStudio)
		{
			if (!insideStudio)
			{
				Hooks.Init();
			}
		}

		/// <summary>
		/// Check if maker interface is currently visible and not obscured by settings screen or other things.
		/// Useful for knowing when to display OnGui mod windows in maker.
		/// </summary>
		public static bool IsInterfaceVisible()
		{
			if (!InsideMaker)
			{
				return false;
			}
			CustomBase makerBase = GetMakerBase();
			if (makerBase == null || makerBase.chaCtrl == null)
			{
				return false;
			}
			if (SceneApi.GetIsNowLoadingFade())
			{
				return false;
			}
			if (makerBase.customCtrl.hideFrontUI)
			{
				return false;
			}
			string addSceneName = SceneApi.GetAddSceneName();
			if ((!string.IsNullOrEmpty(addSceneName) && addSceneName != "CustomScene") || SceneApi.GetIsOverlap())
			{
				return false;
			}
			if (!ccwGo)
			{
				ccwGo = UnityEngine.Object.FindObjectOfType<CustomChangeSystemMenu>()?.checkWindow.gameObject;
			}
			else if (ccwGo.activeSelf)
			{
				return false;
			}
			return true;
		}
	}
	/// <summary>
	/// Specifies a category inside character maker.
	/// </summary>
	public sealed class MakerCategory
	{
		/// <summary>
		/// Main category gameObject name. Main categories are the square buttons at the top-left edge of the screen.
		/// They contain multiple subcategories (tabs on the left edge of the screen).
		/// </summary>
		public string CategoryName { get; }

		/// <summary>
		/// Sub category gameObject name. Sub categories are the named tabs on the left edge of the screen.
		/// They contain the actual controls (inside the window on the right of the tabs).
		/// </summary>
		public string SubCategoryName { get; }

		/// <summary>
		/// Numeric position of the subcategory.
		/// When making new subcategories you can set this value to be in-between stock subcategories.
		/// </summary>
		public int Position { get; }

		/// <summary>
		/// The text displayed on the subcategory tab on the left edge of the screen.
		/// </summary>
		public string DisplayName { get; }

		private bool Equals(MakerCategory other)
		{
			if (string.Equals(CategoryName, other.CategoryName))
			{
				return string.Equals(SubCategoryName, other.SubCategoryName);
			}
			return false;
		}

		/// <inheritdoc />
		public override bool Equals(object obj)
		{
			if (obj == null)
			{
				return false;
			}
			if (this == obj)
			{
				return true;
			}
			if (obj is MakerCategory other)
			{
				return Equals(other);
			}
			return false;
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return (((CategoryName != null) ? CategoryName.GetHashCode() : 0) * 397) ^ ((SubCategoryName != null) ? SubCategoryName.GetHashCode() : 0);
		}

		/// <summary>
		/// Make a new custom subcategory. 
		/// </summary>
		public MakerCategory(string categoryName, string subCategoryName, int position = int.MaxValue, string displayName = null)
		{
			CategoryName = categoryName;
			SubCategoryName = subCategoryName;
			Position = position;
			DisplayName = displayName;
		}

		/// <summary>
		/// Get combined name for logging etc.
		/// </summary>
		public override string ToString()
		{
			return CategoryName + " / " + SubCategoryName;
		}
	}
	/// <summary>
	/// Collection of methods useful for interfacing with character accessories. Has methods both for chara maker and
	/// everywhere else.
	/// Abstracts away MoreAccessories so you don't have to worry if it's installed or not.
	/// </summary>
	public static class AccessoriesApi
	{
		private static class Hooks
		{
			[HarmonyPostfix]
			[HarmonyPatch(typeof(CustomAcsSelectKind), "ChangeSlot")]
			public static void ChangeSlotPostfix(CustomAcsSelectKind __instance, int _no)
			{
				if (CustomAcs != null)
				{
					OnSelectedMakerSlotChanged(__instance, _no);
				}
			}

			[HarmonyBefore(new string[] { "com.joan6694.kkplugins.moreaccessories" })]
			[HarmonyPrefix]
			[HarmonyPatch(typeof(CvsAccessory), "UpdateSelectAccessoryKind")]
			public static void UpdateSelectAccessoryKindPrefix(CvsAccessory __instance, int index, ref bool __state)
			{
				__state = __instance.accessory.parts[__instance.nSlotNo].id != index;
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(CvsAccessory), "UpdateSelectAccessoryKind")]
			public static void UpdateSelectAccessoryKindPostfix(CvsAccessory __instance, ref bool __state)
			{
				if (__state)
				{
					OnAccessoryKindChanged(__instance, (int)__instance.slotNo);
				}
				AutomaticControlVisibility();
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(CvsAccessory), "UpdateSelectAccessoryType")]
			public static void UpdateSelectAccessoryTypePrefix(CvsAccessory __instance, int index, ref bool __state)
			{
				__state = __instance.accessory.parts[__instance.nSlotNo].type - 120 != index;
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(CvsAccessory), "UpdateSelectAccessoryType")]
			public static void UpdateSelectAccessoryTypePostfix(CvsAccessory __instance, ref bool __state)
			{
				if (__state)
				{
					OnAccessoryKindChanged(__instance, (int)__instance.slotNo);
				}
				AutomaticControlVisibility();
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(CvsAccessoryCopy), "CopyAcs")]
			public static void CopyCopyAcsPostfix(CvsAccessoryCopy __instance)
			{
				OnCopyAcs(__instance);
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaControl), "ChangeCoordinateType", new Type[]
			{
				typeof(ChaFileDefine.CoordinateType),
				typeof(bool)
			})]
			public static void ChangeCoordinateTypePostfix()
			{
				AutomaticControlVisibility();
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(CvsAccessoryChange), "CopyAcs")]
			public static void ChangeCopyAcsPostfix(CvsAccessoryChange __instance)
			{
				OnChangeAcs(__instance, __instance.selSrc, __instance.selDst);
			}

			[HarmonyPostfix]
			[HarmonyPriority(800)]
			[HarmonyPatch(typeof(CustomAcsChangeSlot), "Start")]
			private static void CustomAcsChangeSlotPostfix(CustomAcsChangeSlot __instance)
			{
				CustomAcs = __instance;
			}
		}

		private static UnityEngine.Object _moreAccessoriesInstance;

		private static Type _moreAccessoriesType;

		private static CanvasGroup _accessorySlotCanvasGroup;

		internal static CustomAcsChangeSlot CustomAcs { get; private set; }

		/// <summary>
		/// Returns true if the accessory tab in maker is currently selected.
		/// If you want to know if the user can actually see the tab on the screen check <see cref="M:KKAPI.Maker.MakerAPI.IsInterfaceVisible" />.
		/// </summary>
		public static bool AccessoryCanvasVisible
		{
			get
			{
				if (_accessorySlotCanvasGroup != null)
				{
					return _accessorySlotCanvasGroup.alpha.Equals(1f);
				}
				return false;
			}
		}

		/// <summary>
		/// True if the MoreAccessories mod is installed.
		/// Avoid relying on this and instead use other methods in this class since they will handle this for you.
		/// </summary>
		public static bool MoreAccessoriesInstalled => _moreAccessoriesType != null;

		/// <summary>
		/// Get the index of the currently selected accessory slot under Accessories group in Chara Maker.
		/// If none are selected or chara maker is not opened, returns -1. 0-indexed.
		/// Use <see cref="E:KKAPI.Maker.AccessoriesApi.SelectedMakerAccSlotChanged" /> to get notified when the selected slot changes.
		/// </summary>
		public static int SelectedMakerAccSlot { get; private set; } = -1;

		/// <summary>
		/// Fires whenever the index of the currently selected accessory slot under Accessories group in Chara Maker is changed.
		/// This happens when user click on another slot.
		/// </summary>
		public static event EventHandler<AccessorySlotEventArgs> SelectedMakerAccSlotChanged;

		/// <summary>
		/// A new slot was added by MoreAccessories. Adding 10 slots triggers this 10 times.
		/// </summary>
		public static event EventHandler<AccessorySlotEventArgs> MakerAccSlotAdded;

		/// <summary>
		/// Fires when user selects a different accessory in the accessory window.
		/// </summary>
		public static event EventHandler<AccessorySlotEventArgs> AccessoryKindChanged;

		/// <summary>
		/// Fires after user copies accessories between coordinates by using the Copy window.
		/// </summary>
		public static event EventHandler<AccessoryCopyEventArgs> AccessoriesCopied;

		/// <summary>
		/// Fires after user copies an accessory within a single coordinate by using the Transfer window.
		/// </summary>
		public static event EventHandler<AccessoryTransferEventArgs> AccessoryTransferred;

		/// <summary>
		/// Get a list of all accessory objects for this character.
		/// If an accessory slot exists but has no accessory in it, it will appear as null on the list.
		/// </summary>
		public static GameObject[] GetAccessoryObjects(this ChaControl character)
		{
			if (character == null)
			{
				throw new ArgumentNullException("character");
			}
			return character.objAccessory;
		}

		/// <summary>
		/// Get accessory objects of specified index for this character.
		/// null will be returned if an accessory slot exists but has no accessory in it, or if the slot doesn't exist.
		/// If index is below 0 or the character is null an exception will be thrown.
		/// </summary>
		public static GameObject GetAccessoryObject(this ChaControl character, int index)
		{
			if (character == null)
			{
				throw new ArgumentNullException("character");
			}
			return character.GetAccessory(index)?.gameObject;
		}

		/// <summary>
		/// Get count of the UI entries for accessories (accessory slots).
		/// Returns 0 outside of chara maker.
		/// </summary>
		public static int GetMakerAccessoryCount()
		{
			return GetCvsAccessoryCount();
		}

		/// <summary>
		/// Get the root gameobject of the maker UI page for the specified accessory.
		/// Returns null if the page doesn't exist or can't be accessed (e.g. when outside maker).
		/// </summary>
		/// <param name="index">Index of the accessory to get the UI for. Use -1 to get the currently opened accessory page</param>
		public static GameObject GetMakerAccessoryPageObject(int index = -1)
		{
			if (index < 0)
			{
				index = SelectedMakerAccSlot;
			}
			return GetCvsAccessory(index)?.gameObject;
		}

		/// <summary>
		/// Get the character that owns this accessory
		/// </summary>
		public static ChaControl GetOwningCharacter(GameObject accessoryRootObject)
		{
			if (accessoryRootObject == null)
			{
				throw new ArgumentNullException("accessoryRootObject");
			}
			return accessoryRootObject.GetComponentInParent<ChaControl>();
		}

		/// <summary>
		/// Get index of this accessory, or -1 if it doesn't exist for the specified character.
		/// </summary>
		public static int GetAccessoryIndex(this ChaControl character, GameObject accessoryRootObject)
		{
			if (character == null)
			{
				throw new ArgumentNullException("character");
			}
			if (accessoryRootObject == null)
			{
				throw new ArgumentNullException("accessoryRootObject");
			}
			ChaAccessoryComponent component = accessoryRootObject.GetComponent<ChaAccessoryComponent>();
			if (component == null)
			{
				return -1;
			}
			return Array.IndexOf(character.cusAcsCmp, component);
		}

		/// <summary>
		/// Get the accessory given a slot index.
		/// </summary>
		[Obsolete]
		public static ChaAccessoryComponent GetAccessory(this ChaControl character, int accessoryIndex)
		{
			if (accessoryIndex < 0 || accessoryIndex >= character.cusAcsCmp.Length)
			{
				return null;
			}
			return character.cusAcsCmp[accessoryIndex];
		}

		/// <summary>
		/// Get slot index of his accessory, useful for referencing to the accesory in extended data.
		/// </summary>
		[Obsolete]
		public static int GetAccessoryIndex(this ChaAccessoryComponent accessoryComponent)
		{
			ChaControl owningChaControl = accessoryComponent.GetOwningChaControl();
			return Array.IndexOf(owningChaControl.cusAcsCmp, accessoryComponent);
		}

		/// <summary>
		/// Get accessory UI entry in maker.
		/// Only works inside chara maker.
		/// </summary>
		[Obsolete]
		public static CvsAccessory GetCvsAccessory(int index)
		{
			if (CustomAcs == null)
			{
				throw new InvalidOperationException("Can only call GetCvsAccessory when inside Chara Maker");
			}
			return CustomAcs.cvsAccessory[index];
		}

		/// <summary>
		/// Get accessory PartsInfo entry in maker.
		/// Only works inside chara maker.
		/// </summary>
		[Obsolete]
		public static ChaFileAccessory.PartsInfo GetPartsInfo(int index)
		{
			if (!Singleton<CustomBase>.IsInstance())
			{
				throw new InvalidOperationException("Can only call GetPartsInfo when inside Chara Maker");
			}
			ChaControl chaCtrl = Singleton<CustomBase>.instance.chaCtrl;
			if (chaCtrl == null)
			{
				throw new InvalidOperationException("Chara Maker Not Ready");
			}
			ChaFileAccessory.PartsInfo[] parts = chaCtrl.nowCoordinate.accessory.parts;
			if (parts.Length <= index)
			{
				return new ChaFileAccessory.PartsInfo();
			}
			return parts[index];
		}

		/// <summary>
		/// Get count of the UI entries for accessories (accessory slots).
		/// Returns 0 outside of chara maker.
		/// </summary>
		[Obsolete]
		public static int GetCvsAccessoryCount()
		{
			if (CustomAcs == null)
			{
				return 0;
			}
			return CustomAcs.cvsAccessory.Length;
		}

		/// <summary>
		/// Get the ChaControl that owns this accessory
		/// </summary>
		[Obsolete]
		public static ChaControl GetOwningChaControl(this ChaAccessoryComponent accessoryComponent)
		{
			return accessoryComponent.GetComponentInParent<ChaControl>();
		}

		internal static void Init()
		{
			DetectMoreAccessories();
			Harmony.CreateAndPatchAll(typeof(Hooks), (string)null);
			MakerAPI.InsideMakerChanged += MakerAPI_InsideMakerChanged;
			MakerAPI.MakerFinishedLoading += delegate(object sender, EventArgs args)
			{
				OnSelectedMakerSlotChanged(sender, 0);
			};
		}

		private static void DetectMoreAccessories()
		{
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				_moreAccessoriesType = Type.GetType("MoreAccessoriesKOI.MoreAccessories, MoreAccessories, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", throwOnError: true);
				if (!(_moreAccessoriesType != null))
				{
					return;
				}
				_moreAccessoriesInstance = UnityEngine.Object.FindObjectOfType(_moreAccessoriesType);
				Version version = ((BaseUnityPlugin)_moreAccessoriesInstance).Info.Metadata.Version;
				Version version2 = new Version(2, 0, 6);
				if (version >= version2)
				{
					System.Reflection.EventInfo eventInfo = _moreAccessoriesType.GetEvent("onCharaMakerSlotAdded", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (eventInfo != null)
					{
						eventInfo.AddEventHandler(_moreAccessoriesInstance, (Action<int, Transform>)delegate(int i, Transform transform)
						{
							OnMakerAccSlotAdded(_moreAccessoriesInstance, i, transform);
						});
						return;
					}
				}
				_moreAccessoriesType = null;
				KoikatuAPI.Logger.Log((LogLevel)12, (object)$"WARNING: MoreAccesories is outdated! Some features won't work until you update to v{version2} or later (Current verion: v{version}).");
			}
			catch (Exception ex)
			{
				_moreAccessoriesType = null;
				KoikatuAPI.Logger.LogWarning((object)"Failed to detect MoreAccessories!");
				KoikatuAPI.Logger.LogDebug((object)ex);
			}
		}

		private static void MakerAPI_InsideMakerChanged(object sender, EventArgs e)
		{
			if (MakerAPI.InsideMaker)
			{
				CustomAcsChangeSlot customAcsChangeSlot = UnityEngine.Object.FindObjectOfType<CustomAcsChangeSlot>();
				_accessorySlotCanvasGroup = customAcsChangeSlot.GetComponent<CanvasGroup>();
			}
			else
			{
				_accessorySlotCanvasGroup = null;
				SelectedMakerAccSlot = -1;
			}
		}

		private static void OnSelectedMakerSlotChanged(object source, int newSlotIndex)
		{
			if (newSlotIndex == SelectedMakerAccSlot)
			{
				return;
			}
			SelectedMakerAccSlot = newSlotIndex;
			AccessorySlotEventArgs args = new AccessorySlotEventArgs(newSlotIndex);
			if (KoikatuAPI.EnableDebugLogging)
			{
				KoikatuAPI.Logger.LogMessage((object)("SelectedMakerAccSlotChanged - slot:" + newSlotIndex));
				ApiEventExecutionLogger eventLogger = ApiEventExecutionLogger.GetEventLogger();
				eventLogger.Begin("OnSelectedMakerSlotChanged", newSlotIndex.ToString());
				AccessoriesApi.SelectedMakerAccSlotChanged.SafeInvokeWithLogging(delegate(EventHandler<AccessorySlotEventArgs> handler)
				{
					handler(source, args);
				}, "SelectedMakerAccSlotChanged", eventLogger);
				eventLogger.End();
			}
			else
			{
				AccessoriesApi.SelectedMakerAccSlotChanged.SafeInvoke(delegate(EventHandler<AccessorySlotEventArgs> handler)
				{
					handler(source, args);
				});
			}
			AutomaticControlVisibility();
		}

		private static void OnMakerAccSlotAdded(object source, int newSlotIndex, Transform newSlotTransform)
		{
			if (KoikatuAPI.EnableDebugLogging)
			{
				KoikatuAPI.Logger.LogMessage((object)("MakerAccSlotAdded - slot:" + newSlotIndex));
			}
			ApiEventExecutionLogger eventLogger = ApiEventExecutionLogger.GetEventLogger();
			eventLogger.Begin("OnMakerAccSlotAdded", newSlotIndex.ToString());
			eventLogger.PluginStart();
			MakerInterfaceCreator.OnMakerAccSlotAdded(newSlotTransform);
			eventLogger.PluginEnd("MakerInterfaceCreator.OnMakerAccSlotAdded");
			AccessorySlotEventArgs args = new AccessorySlotEventArgs(newSlotIndex);
			AccessoriesApi.MakerAccSlotAdded.SafeInvokeWithLogging(delegate(EventHandler<AccessorySlotEventArgs> handler)
			{
				handler(source, args);
			}, "MakerAccSlotAdded", eventLogger);
			eventLogger.End();
		}

		private static void OnAccessoryKindChanged(object source, int slotNo)
		{
			AccessorySlotEventArgs args = new AccessorySlotEventArgs(slotNo);
			if (KoikatuAPI.EnableDebugLogging)
			{
				KoikatuAPI.Logger.LogMessage((object)("AccessoryKindChanged - slot:" + slotNo));
				ApiEventExecutionLogger eventLogger = ApiEventExecutionLogger.GetEventLogger();
				eventLogger.Begin("OnAccessoryKindChanged", slotNo.ToString());
				AccessoriesApi.AccessoryKindChanged.SafeInvokeWithLogging(delegate(EventHandler<AccessorySlotEventArgs> handler)
				{
					handler(source, args);
				}, "AccessoryKindChanged", eventLogger);
				eventLogger.End();
			}
			else
			{
				AccessoriesApi.AccessoryKindChanged.SafeInvoke(delegate(EventHandler<AccessorySlotEventArgs> handler)
				{
					handler(source, args);
				});
			}
		}

		private static void OnCopyAcs(CvsAccessoryCopy instance)
		{
			if (AccessoriesApi.AccessoriesCopied == null && !KoikatuAPI.EnableDebugLogging)
			{
				return;
			}
			try
			{
				Toggle[] tglKind = instance.tglKind;
				List<int> list = new List<int>();
				for (int i = 0; i < tglKind.Length; i++)
				{
					if (tglKind[i].isOn)
					{
						list.Add(i);
					}
				}
				TMP_Dropdown[] ddCoordeType = instance.ddCoordeType;
				AccessoryCopyEventArgs args = new AccessoryCopyEventArgs(list, (ChaFileDefine.CoordinateType)ddCoordeType[1].value, (ChaFileDefine.CoordinateType)ddCoordeType[0].value);
				if (KoikatuAPI.EnableDebugLogging)
				{
					KoikatuAPI.Logger.LogMessage((object)string.Format("AccessoriesCopied - ids: {0}, src:{1}, dst:{2}", string.Join(", ", args.CopiedSlotIndexes.Select((int x) => x.ToString()).ToArray()), args.CopySource, args.CopyDestination));
					ApiEventExecutionLogger eventLogger = ApiEventExecutionLogger.GetEventLogger();
					eventLogger.Begin("OnAccessoryKindChanged", null);
					AccessoriesApi.AccessoriesCopied.SafeInvokeWithLogging(delegate(EventHandler<AccessoryCopyEventArgs> handler)
					{
						handler(instance, args);
					}, "AccessoriesCopied", eventLogger);
					eventLogger.End();
				}
				else
				{
					AccessoriesApi.AccessoriesCopied.SafeInvoke(delegate(EventHandler<AccessoryCopyEventArgs> handler)
					{
						handler(instance, args);
					});
				}
			}
			catch (Exception ex)
			{
				KoikatuAPI.Logger.LogError((object)("Crash in AccessoriesCopied event: " + ex));
			}
		}

		private static void OnChangeAcs(object instance, int selSrc, int selDst)
		{
			AccessoryTransferEventArgs args = new AccessoryTransferEventArgs(selSrc, selDst);
			if (KoikatuAPI.EnableDebugLogging)
			{
				KoikatuAPI.Logger.LogMessage((object)$"AccessoryTransferred - srcId:{selSrc}, dstId:{selDst}");
				ApiEventExecutionLogger eventLogger = ApiEventExecutionLogger.GetEventLogger();
				eventLogger.Begin("OnChangeAcs", selSrc + " -> " + selDst);
				AccessoriesApi.AccessoryTransferred.SafeInvokeWithLogging(delegate(EventHandler<AccessoryTransferEventArgs> handler)
				{
					handler(instance, args);
				}, "AccessoryTransferred", eventLogger);
				eventLogger.End();
			}
			else
			{
				AccessoriesApi.AccessoryTransferred.SafeInvoke(delegate(EventHandler<AccessoryTransferEventArgs> handler)
				{
					handler(instance, args);
				});
			}
		}

		/// <summary>
		/// Used to tell non-automated plugins that accessory kind has changed
		/// </summary>
		internal static void AutomaticControlVisibility()
		{
			int selectedMakerAccSlot = SelectedMakerAccSlot;
			if (selectedMakerAccSlot >= 0)
			{
				ChaFileAccessory.PartsInfo partsInfo = GetPartsInfo(selectedMakerAccSlot);
				bool showAccControls = partsInfo.type != 120;
				MakerInterfaceCreator.AutomaticAccessoryControlVisibility(showAccControls);
			}
		}
	}
	/// <summary>
	/// A wrapper for custom controls used in accessory window (added by using <see cref="M:KKAPI.Maker.MakerAPI.AddAccessoryWindowControl``1(``0)" />).
	/// It abstracts away switching between accessory slots and provides a simple list of values for each accessory.
	/// </summary>
	/// <typeparam name="T">Type of the control to be wrapped. The control has to be added by using <see cref="M:KKAPI.Maker.MakerAPI.AddAccessoryWindowControl``1(``0)" /> or results will be undefined.</typeparam>
	/// <typeparam name="TVal">Type of the control's value.</typeparam>
	public class AccessoryControlWrapper<T, TVal> where T : BaseEditableGuiEntry<TVal>
	{
		private readonly TVal _defaultValue;

		private readonly Dictionary<int, TVal> _values = new Dictionary<int, TVal>();

		private bool _isDisposed;

		/// <summary>
		/// The wrapped control.
		/// </summary>
		public T Control { get; }

		/// <summary>
		/// Index of the currently selected accessory.
		/// </summary>
		public int CurrentlySelectedIndex => AccessoriesApi.SelectedMakerAccSlot;

		/// <summary>
		/// If true, the control has been disposed and can no longer be used, likely because the character maker exited.
		/// A new control has to be created to be used again.
		/// </summary>
		public bool IsDisposed => CheckDisposed();

		/// <summary>
		/// Fired when the value of this control changes for any of the accessories.
		/// </summary>
		public event EventHandler<AccessoryWindowControlValueChangedEventArgs<TVal>> ValueChanged;

		/// <summary>
		/// Fired when the currently visible accessory was changed by the user clicking on one of the slots.
		/// </summary>
		public event EventHandler<AccessorySlotEventArgs> VisibleIndexChanged;

		/// <summary>
		/// Fires when user selects a different accessory in the accessory window.
		/// </summary>
		public event EventHandler<AccessorySlotEventArgs> AccessoryKindChanged;

		/// <summary>
		/// Fires after user copies accessories between coordinates by using the Copy window.
		/// </summary>
		public static event EventHandler<AccessoryCopyEventArgs> AccessoriesCopied;

		/// <summary>
		/// Fires after user copies an accessory within a single coordinate by using the Transfer window.
		/// </summary>
		public static event EventHandler<AccessoryTransferEventArgs> AccessoryTransferred;

		/// <summary>
		/// Create a new wrapper.
		/// </summary>
		/// <param name="control">Control to be wrapped. The control has to be added by using <see cref="M:KKAPI.Maker.MakerAPI.AddAccessoryWindowControl``1(``0)" /> or results will be undefined.</param>
		public AccessoryControlWrapper(T control)
		{
			if (control == null)
			{
				throw new ArgumentNullException("control");
			}
			Control = control;
			_defaultValue = control.Value;
			control.ValueChanged.Subscribe(delegate(TVal val)
			{
				SetValue(AccessoriesApi.SelectedMakerAccSlot, val);
			});
			AccessoriesApi.SelectedMakerAccSlotChanged += OnSelectedMakerAccSlotChanged;
			AccessoriesApi.AccessoryKindChanged += OnAccessoryKindChanged;
			AccessoriesApi.AccessoriesCopied += OnAccessoriesCopied;
			AccessoriesApi.AccessoryTransferred += OnAccessoryTransferred;
		}

		private void OnAccessoryTransferred(object sender, AccessoryTransferEventArgs e)
		{
			if (!CheckDisposed())
			{
				AccessoryControlWrapper<T, TVal>.AccessoryTransferred?.Invoke(sender, e);
			}
		}

		private void OnAccessoriesCopied(object sender, AccessoryCopyEventArgs e)
		{
			if (!CheckDisposed())
			{
				AccessoryControlWrapper<T, TVal>.AccessoriesCopied?.Invoke(sender, e);
			}
		}

		private void OnAccessoryKindChanged(object sender, AccessorySlotEventArgs accessorySlotEventArgs)
		{
			if (!CheckDisposed())
			{
				this.AccessoryKindChanged?.Invoke(sender, accessorySlotEventArgs);
			}
		}

		private void OnSelectedMakerAccSlotChanged(object o, AccessorySlotEventArgs accessorySlotEventArgs)
		{
			if (!CheckDisposed())
			{
				Control.SetValue(GetValue(accessorySlotEventArgs.SlotIndex), fireEvents: false);
				this.VisibleIndexChanged?.Invoke(o, accessorySlotEventArgs);
			}
		}

		/// <summary>
		/// Get value of the control for the specified accessory.
		/// </summary>
		public TVal GetValue(int accessoryIndex)
		{
			CheckDisposedThrow();
			CheckIndexRangeThrow(accessoryIndex);
			if (_values.TryGetValue(accessoryIndex, out var value))
			{
				return value;
			}
			return _defaultValue;
		}

		/// <summary>
		/// Get value of the control for the currently selected accessory.
		/// </summary>
		public TVal GetSelectedValue()
		{
			return GetValue(CurrentlySelectedIndex);
		}

		/// <summary>
		/// Set value of the control for the specified accessory.
		/// </summary>
		/// <param name="accessoryIndex">Index of the accessory to set the value for</param>
		/// <param name="value">Value to set</param>
		public void SetValue(int accessoryIndex, TVal value)
		{
			SetValue(accessoryIndex, value, fireEvents: true);
		}

		/// <summary>
		/// Set value of the control for the specified accessory.
		/// </summary>
		/// <param name="accessoryIndex">Index of the accessory to set the value for</param>
		/// <param name="value">Value to set</param>
		/// <param name="fireEvents">Fire the <see cref="E:KKAPI.Maker.AccessoryControlWrapper`2.ValueChanged" /> event if the value actually changed.</param>
		public void SetValue(int accessoryIndex, TVal value, bool fireEvents)
		{
			CheckDisposedThrow();
			if (accessoryIndex >= 0)
			{
				CheckIndexRangeThrow(accessoryIndex);
				_values[accessoryIndex] = value;
				if (AccessoriesApi.SelectedMakerAccSlot == accessoryIndex)
				{
					Control.SetValue(value, fireEvents: false);
				}
				if (fireEvents)
				{
					this.ValueChanged?.Invoke(this, new AccessoryWindowControlValueChangedEventArgs<TVal>(value, CurrentlySelectedIndex));
				}
			}
		}

		/// <summary>
		/// Set value of the control for the currently selected accessory.
		/// </summary>
		/// <param name="value">Value to set</param>
		public void SetSelectedValue(TVal value)
		{
			SetSelectedValue(value, fireEvents: false);
		}

		/// <summary>
		/// Set value of the control for the currently selected accessory.
		/// </summary>
		/// <param name="value">Value to set</param>
		/// <param name="fireEvents">Fire the <see cref="E:KKAPI.Maker.AccessoryControlWrapper`2.ValueChanged" /> event if the value actually changed.</param>
		public void SetSelectedValue(TVal value, bool fireEvents)
		{
			SetValue(CurrentlySelectedIndex, value, fireEvents);
		}

		private static void CheckIndexRangeThrow(int accessoryIndex)
		{
			if (accessoryIndex < 0 || accessoryIndex >= AccessoriesApi.GetMakerAccessoryCount())
			{
				throw new IndexOutOfRangeException("accessoryIndex has to be between 0 and AccessoriesApi.GetCvsAccessoryCount() - 1");
			}
		}

		private void CheckDisposedThrow()
		{
			if (CheckDisposed())
			{
				throw new ObjectDisposedException("The control has been disposed. Controls only live in Maker, and you need to create a new one every time maker starts");
			}
		}

		private bool CheckDisposed()
		{
			if (_isDisposed)
			{
				return true;
			}
			if (Control.IsDisposed)
			{
				_isDisposed = true;
				_values.Clear();
				this.ValueChanged = null;
				this.VisibleIndexChanged = null;
				this.AccessoryKindChanged = null;
				AccessoryControlWrapper<T, TVal>.AccessoryTransferred = null;
				AccessoriesApi.SelectedMakerAccSlotChanged -= OnSelectedMakerAccSlotChanged;
				AccessoriesApi.AccessoryKindChanged -= OnAccessoryKindChanged;
				AccessoriesApi.AccessoryTransferred -= OnAccessoryTransferred;
				AccessoryControlWrapper<T, TVal>.AccessoriesCopied = null;
				AccessoriesApi.AccessoriesCopied -= OnAccessoriesCopied;
				return true;
			}
			return false;
		}
	}
	/// <summary>
	/// Event args for events that are related to accessory control visibility.
	/// </summary>
	public class AccessoryContolVisibilityArgs : EventArgs
	{
		/// <summary>
		/// Visibility state of accessory control
		/// </summary>
		public bool Show { get; }

		/// <inheritdoc />
		public AccessoryContolVisibilityArgs(bool _show)
		{
			Show = _show;
		}
	}
	/// <summary>
	/// Event args for events that are related to accessory slot indexes.
	/// </summary>
	public class AccessorySlotEventArgs : EventArgs
	{
		/// <summary>
		/// Currently opened accessory slot index. 0-indexed.
		/// </summary>
		public int SlotIndex { get; }

		/// <summary>
		/// Get accessory UI entry in maker.
		/// </summary>
		[Obsolete]
		public CvsAccessory CvsAccessory => AccessoriesApi.GetCvsAccessory(SlotIndex);

		/// <summary>
		/// Get accessory component.
		/// </summary>
		[Obsolete]
		public ChaAccessoryComponent AccessoryComponent => MakerAPI.GetCharacterControl().GetAccessory(SlotIndex);

		/// <inheritdoc />
		public AccessorySlotEventArgs(int slotIndex)
		{
			SlotIndex = slotIndex;
		}
	}
	/// <summary>
	/// Event args for accessory transfer events.
	/// </summary>
	public class AccessoryTransferEventArgs : EventArgs
	{
		/// <summary>
		/// Index of the source accessory.
		/// </summary>
		public int SourceSlotIndex { get; }

		/// <summary>
		/// Index the source accessory is copied to.
		/// </summary>
		public int DestinationSlotIndex { get; }

		/// <inheritdoc />
		public AccessoryTransferEventArgs(int sourceSlotIndex, int destinationSlotIndex)
		{
			SourceSlotIndex = sourceSlotIndex;
			DestinationSlotIndex = destinationSlotIndex;
		}
	}
	/// <summary>
	/// Event args used in <see cref="T:KKAPI.Maker.AccessoryControlWrapper`2" />.
	/// </summary>
	public sealed class AccessoryWindowControlValueChangedEventArgs<TVal> : EventArgs
	{
		/// <summary>
		/// Newly assigned value.
		/// </summary>
		public TVal NewValue { get; }

		/// <summary>
		/// Index of the accessory the value was assigned to.
		/// </summary>
		public int SlotIndex { get; }

		/// <inheritdoc />
		public AccessoryWindowControlValueChangedEventArgs(TVal newValue, int slotIndex)
		{
			NewValue = newValue;
			SlotIndex = slotIndex;
		}
	}
	/// <summary>
	/// API for modifying the process of saving cards in maker.
	/// </summary>
	public static class MakerCardSave
	{
		/// <summary>
		/// Used for modifying card save paths. Parameter is the original path, return the changed path.
		/// </summary>
		public delegate string DirectoryPathModifier(string currentDirectoryPath);

		/// <summary>
		/// Used for modifying card file names. Parameter is the original name, return the changed name.
		/// </summary>
		public delegate string CardNameModifier(string currentCardName);

		private static readonly Harmony _harmony;

		private static readonly List<KeyValuePair<DirectoryPathModifier, CardNameModifier>> _modifiers;

		static MakerCardSave()
		{
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Expected O, but got Unknown
			_modifiers = new List<KeyValuePair<DirectoryPathModifier, CardNameModifier>>();
			_harmony = new Harmony("MakerCardSave");
			_harmony.PatchAll(typeof(MakerCardSave));
		}

		/// <summary>
		/// Add a function that can modify the path of the saved cards.
		/// Use sparingly and insert/replace parts of the path instead of overwriting the whole path to keep compatibility with other plugins.
		/// </summary>
		/// <param name="directoryPathModifier">Modifier for the directory the card is saved to. Set to null if no change is required.</param>
		/// <param name="filenameModifier">Modifier for the name the card file itself. Set to null if no change is required.</param>
		public static void RegisterNewCardSavePathModifier(DirectoryPathModifier directoryPathModifier, CardNameModifier filenameModifier)
		{
			_modifiers.Add(new KeyValuePair<DirectoryPathModifier, CardNameModifier>(directoryPathModifier, filenameModifier));
		}

		[HarmonyTranspiler]
		[HarmonyPatch(typeof(CustomControl), "Start")]
		internal static IEnumerable<CodeInstruction> FindSaveMethod(IEnumerable<CodeInstruction> instructions)
		{
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Expected O, but got Unknown
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Expected O, but got Unknown
			//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cd: Expected O, but got Unknown
			//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d6: Expected O, but got Unknown
			//IL_015a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0168: Expected O, but got Unknown
			FieldInfo btnSaveField = AccessTools.Field(typeof(CustomControl), "btnSave") ?? throw new MissingFieldException("could not find btnSave");
			MethodInfo methodInfo = AccessTools.Method(typeof(MakerCardSave), "CardSavePatch", (Type[])null, (Type[])null) ?? throw new MissingMethodException("could not find CardSavePatch");
			CodeMatcher val = new CodeMatcher(instructions, (ILGenerator)null);
			FieldInfo fieldInfo = (FieldInfo)val.MatchForward(false, (CodeMatch[])(object)new CodeMatch[1]
			{
				new CodeMatch((OpCode?)OpCodes.Stfld, (object)null, (string)null)
			}).Instruction.operand;
			MethodInfo method = fieldInfo.DeclaringType.GetMethod("MoveNext", AccessTools.all);
			if (method == null)
			{
				throw new ArgumentNullException("movenext");
			}
			ILContext val2 = new ILContext(new DynamicMethodDefinition((MethodBase)method).Definition);
			ILCursor val3 = new ILCursor(val2);
			val3.GotoNext(new Func<Instruction, bool>[1]
			{
				(Instruction instruction) => ILPatternMatchingExt.MatchLdfld(instruction, btnSaveField)
			});
			MethodReference targetMethodReference = null;
			val3.GotoNext(new Func<Instruction, bool>[1]
			{
				(Instruction instruction) => ILPatternMatchingExt.MatchLdftn(instruction, ref targetMethodReference)
			});
			if (targetMethodReference == null)
			{
				throw new ArgumentNullException("targetMethodReference");
			}
			MethodBase methodBase = ReflectionHelper.ResolveReflection(targetMethodReference);
			KoikatuAPI.Logger.LogDebug((object)("Save method found for patching MakerCardSave - " + methodBase.Name));
			_harmony.Patch(methodBase, new HarmonyMethod(methodInfo), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			return instructions;
		}

		private static bool CardSavePatch(CustomControl __instance)
		{
			if (!MakerAPI.InsideMaker || !_modifiers.Any((KeyValuePair<DirectoryPathModifier, CardNameModifier> x) => x.Key != null || x.Value != null))
			{
				return true;
			}
			try
			{
				Utils.Sound.Play(SystemSE.ok_s);
				ChaControl chaCtrl = Singleton<CustomBase>.Instance.chaCtrl;
				bool flag = chaCtrl.sex == 0;
				string text = UserData.Path + (flag ? "chara/male/" : "chara/female/");
				string text2 = (__instance.saveNew ? string.Format("KoikatsuSun_{0}_{1:yyyyMMddHHmmssfff}", flag ? "M" : "F", DateTime.Now) : __instance.saveFileName);
				foreach (KeyValuePair<DirectoryPathModifier, CardNameModifier> modifier in _modifiers)
				{
					if (modifier.Key != null)
					{
						text = modifier.Key(text);
					}
					if (modifier.Value != null && __instance.saveNew)
					{
						text2 = modifier.Value(text2);
					}
				}
				string filename = Path.Combine(text, text2);
				chaCtrl.chaFile.SaveCharaFile(filename);
				if (__instance.saveFileListCtrl != null)
				{
					CustomCharaFile customCharaFile = UnityEngine.Object.FindObjectOfType<CustomCharaFile>();
					if (customCharaFile != null)
					{
						RefreshThumbs(customCharaFile);
					}
				}
				__instance.saveMode = false;
				return false;
			}
			catch (Exception ex)
			{
				KoikatuAPI.Logger.LogError((object)ex);
				return true;
			}
		}

		private static void RefreshThumbs(CustomCharaFile listCtrl)
		{
			Traverse val = Traverse.Create((object)listCtrl);
			Traverse val2 = val.Method("Initialize", new object[2] { true, false });
			if (val2.MethodExists())
			{
				val2.GetValue();
			}
			else
			{
				val.Method("Initialize", Array.Empty<object>()).GetValue();
			}
		}
	}
	/// <summary>
	/// Useful values from the character maker. Mostly built-in categories for use with registering custom controls.
	/// </summary>
	public static class MakerConstants
	{
		public static class Face
		{
			public static MakerCategory All => GetBuiltInCategory("00_FaceTop", "tglAll");

			public static MakerCategory Ear => GetBuiltInCategory("00_FaceTop", "tglEar");

			public static MakerCategory Chin => GetBuiltInCategory("00_FaceTop", "tglChin");

			public static MakerCategory Cheek => GetBuiltInCategory("00_FaceTop", "tglCheek");

			public static MakerCategory Eyebrow => GetBuiltInCategory("00_FaceTop", "tglEyebrow");

			public static MakerCategory Eye => GetBuiltInCategory("00_FaceTop", "tglEye01");

			public static MakerCategory Iris => GetBuiltInCategory("00_FaceTop", "tglEye02");

			public static MakerCategory Nose => GetBuiltInCategory("00_FaceTop", "tglNose");

			public static MakerCategory Mouth => GetBuiltInCategory("00_FaceTop", "tglMouth");

			public static MakerCategory Mole => GetBuiltInCategory("00_FaceTop", "tglMole");

			public static MakerCategory Makeup => GetBuiltInCategory("00_FaceTop", "tglMakeup");

			public static MakerCategory Shape => GetBuiltInCategory("00_FaceTop", "tglShape");
		}

		public static class Body
		{
			public static MakerCategory All => GetBuiltInCategory("01_BodyTop", "tglAll");

			public static MakerCategory Breast => GetBuiltInCategory("01_BodyTop", "tglBreast");

			public static MakerCategory Upper => GetBuiltInCategory("01_BodyTop", "tglUpper");

			public static MakerCategory Lower => GetBuiltInCategory("01_BodyTop", "tglLower");

			public static MakerCategory Arm => GetBuiltInCategory("01_BodyTop", "tglArm");

			public static MakerCategory Leg => GetBuiltInCategory("01_BodyTop", "tglLeg");

			public static MakerCategory Nail => GetBuiltInCategory("01_BodyTop", "tglNail");

			public static MakerCategory Underhair => GetBuiltInCategory("01_BodyTop", "tglUnderhair");

			public static MakerCategory Sunburn => GetBuiltInCategory("01_BodyTop", "tglSunburn");

			public static MakerCategory Paint => GetBuiltInCategory("01_BodyTop", "tglPaint");

			public static MakerCategory Shape => GetBuiltInCategory("01_BodyTop", "tglShape");
		}

		public static class Hair
		{
			public static MakerCategory Common => GetBuiltInCategory("02_HairTop", "common");

			public static MakerCategory Back => GetBuiltInCategory("02_HairTop", "tglBack");

			public static MakerCategory Front => GetBuiltInCategory("02_HairTop", "tglFront");

			public static MakerCategory Side => GetBuiltInCategory("02_HairTop", "tglSide");

			public static MakerCategory Extension => GetBuiltInCategory("02_HairTop", "tglExtension");

			public static MakerCategory Etc => GetBuiltInCategory("02_HairTop", "tglEtc");
		}

		public static class Clothes
		{
			public static MakerCategory Top => GetBuiltInCategory("03_ClothesTop", "tglTop");

			public static MakerCategory Bottom => GetBuiltInCategory("03_ClothesTop", "tglBot");

			public static MakerCategory Bra => GetBuiltInCategory("03_ClothesTop", "tglBra");

			public static MakerCategory Shorts => GetBuiltInCategory("03_ClothesTop", "tglShorts");

			public static MakerCategory Gloves => GetBuiltInCategory("03_ClothesTop", "tglGloves");

			public static MakerCategory Panst => GetBuiltInCategory("03_ClothesTop", "tglPanst");

			public static MakerCategory Socks => GetBuiltInCategory("03_ClothesTop", "tglSocks");

			public static MakerCategory OuterShoes => GetBuiltInCategory("03_ClothesTop", "tglOuterShoes");

			public static MakerCategory Copy => GetBuiltInCategory("03_ClothesTop", "tglCopy");
		}

		public static class Parameter
		{
			public static MakerCategory Character => GetBuiltInCategory("05_ParameterTop", "tglCharactor");

			public static MakerCategory CharacterEx => GetBuiltInCategory("05_ParameterTop", "tglCharactorEx");

			public static MakerCategory H => GetBuiltInCategory("05_ParameterTop", "tglH");

			public static MakerCategory QA => GetBuiltInCategory("05_ParameterTop", "tglQA");

			public static MakerCategory Attribute => GetBuiltInCategory("05_ParameterTop", "tglAttribute");

			public static MakerCategory ADK => GetBuiltInCategory("05_ParameterTop", "tglADK");
		}

		/// <summary>
		/// Default text color for maker controls.
		/// </summary>
		public static readonly Color DefaultControlTextColor = Color.white;

		private static readonly List<MakerCategory> _builtInCategories = new List<MakerCategory>
		{
			new MakerCategory("00_FaceTop", "tglAll", 10),
			new MakerCategory("00_FaceTop", "tglEar", 20),
			new MakerCategory("00_FaceTop", "tglChin", 30),
			new MakerCategory("00_FaceTop", "tglCheek", 40),
			new MakerCategory("00_FaceTop", "tglEyebrow", 50),
			new MakerCategory("00_FaceTop", "tglEye01", 60),
			new MakerCategory("00_FaceTop", "tglEye02", 70),
			new MakerCategory("00_FaceTop", "tglNose", 80),
			new MakerCategory("00_FaceTop", "tglMouth", 90),
			new MakerCategory("00_FaceTop", "tglMole", 100),
			new MakerCategory("00_FaceTop", "tglMakeup", 110),
			new MakerCategory("00_FaceTop", "tglShape", 120),
			new MakerCategory("01_BodyTop", "tglAll", 10),
			new MakerCategory("01_BodyTop", "tglBreast", 20),
			new MakerCategory("01_BodyTop", "tglUpper", 30),
			new MakerCategory("01_BodyTop", "tglLower", 40),
			new MakerCategory("01_BodyTop", "tglArm", 50),
			new MakerCategory("01_BodyTop", "tglLeg", 60),
			new MakerCategory("01_BodyTop", "tglNail", 70),
			new MakerCategory("01_BodyTop", "tglUnderhair", 80),
			new MakerCategory("01_BodyTop", "tglSunburn", 90),
			new MakerCategory("01_BodyTop", "tglPaint", 100),
			new MakerCategory("01_BodyTop", "tglShape", 110),
			new MakerCategory("02_HairTop", "common", 10),
			new MakerCategory("02_HairTop", "tglBack", 20),
			new MakerCategory("02_HairTop", "tglFront", 30),
			new MakerCategory("02_HairTop", "tglSide", 40),
			new MakerCategory("02_HairTop", "tglExtension", 50),
			new MakerCategory("02_HairTop", "tglEtc", 60),
			new MakerCategory("03_ClothesTop", "tglTop", 10),
			new MakerCategory("03_ClothesTop", "tglBot", 20),
			new MakerCategory("03_ClothesTop", "tglBra", 30),
			new MakerCategory("03_ClothesTop", "tglShorts", 40),
			new MakerCategory("03_ClothesTop", "tglGloves", 50),
			new MakerCategory("03_ClothesTop", "tglPanst", 60),
			new MakerCategory("03_ClothesTop", "tglSocks", 70),
			new MakerCategory("03_ClothesTop", "tglOuterShoes", 90),
			new MakerCategory("03_ClothesTop", "tglCopy", 100),
			new MakerCategory("05_ParameterTop", "tglCharactor", 10),
			new MakerCategory("05_ParameterTop", "tglCharactorEx", 20),
			new MakerCategory("05_ParameterTop", "tglH", 30),
			new MakerCategory("05_ParameterTop", "tglAttribute", 50),
			new MakerCategory("05_ParameterTop", "tglADK", 60),
			new MakerCategory("05_ParameterTop", "tglInterest", 70)
		};

		private static Dictionary<string, MakerCategory> _categoryLookup;

		/// <summary>
		/// All ategories that are built-into the character maker by default.
		/// </summary>
		public static IEnumerable<MakerCategory> BuiltInCategories => _builtInCategories;

		/// <summary>
		/// Quick search for a built-in category. If you know what category you want to use at 
		/// compile time you can use the shortcuts instead, e.g. <see cref="P:KKAPI.Maker.MakerConstants.Face.Ear" /> 
		/// </summary>
		public static MakerCategory GetBuiltInCategory(string category, string subCategory)
		{
			if (category == null)
			{
				throw new ArgumentNullException("category");
			}
			if (subCategory == null)
			{
				throw new ArgumentNullException("subCategory");
			}
			if (_categoryLookup == null)
			{
				_categoryLookup = new Dictionary<string, MakerCategory>();
				foreach (MakerCategory builtInCategory in _builtInCategories)
				{
					_categoryLookup.Add(MakeKey(builtInCategory.CategoryName, builtInCategory.SubCategoryName), builtInCategory);
				}
			}
			if (!_categoryLookup.TryGetValue(MakeKey(category, subCategory), out var value))
			{
				return null;
			}
			return value;
			static string MakeKey(string catName, string subCatName)
			{
				return catName + "|||" + subCatName;
			}
		}
	}
	internal static class MakerInterfaceCreator
	{
		private static readonly List<MakerCategory> _categories = new List<MakerCategory>();

		private static readonly List<BaseGuiEntry> _guiEntries = new List<BaseGuiEntry>();

		private static readonly List<BaseGuiEntry> _sidebarEntries = new List<BaseGuiEntry>();

		private static readonly List<BaseGuiEntry> _accessoryWindowEntries = new List<BaseGuiEntry>();

		private static readonly MakerCategory _accessorySlotWindowCategory = new MakerCategory("04_AccessoryTop", "Slots");

		private static bool _accessoryControlShowState = true;

		public static void RemoveCustomControls()
		{
			foreach (BaseGuiEntry item in _guiEntries.Concat(_sidebarEntries).Concat(_accessoryWindowEntries))
			{
				item.Dispose();
			}
			_guiEntries.Clear();
			_categories.Clear();
			_sidebarEntries.Clear();
			_accessoryWindowEntries.Clear();
			MakerLoadToggle.Reset();
			MakerCoordinateLoadToggle.Reset();
		}

		public static T AddControl<T>(T control) where T : BaseGuiEntry
		{
			if (control is MakerLoadToggle || control is MakerCoordinateLoadToggle || control is ISidebarControl)
			{
				throw new ArgumentException("Can't add " + control.GetType().FullName + " as a normal control", "control");
			}
			_guiEntries.Add(control);
			return control;
		}

		public static void AddSubCategory(MakerCategory category)
		{
			if (!_categories.Contains(category))
			{
				_categories.Add(category);
			}
			else
			{
				KoikatuAPI.Logger.LogInfo((object)$"Duplicate custom subcategory was added: {category} The duplicate will be ignored.");
			}
		}

		internal static void InitializeMaker()
		{
			MakerLoadToggle.Setup();
			MakerCoordinateLoadToggle.Setup();
			_accessoryControlShowState = true;
		}

		public static T AddAccessoryWindowControl<T>(T control) where T : BaseGuiEntry
		{
			return AddAccessoryWindowControl(control, automate_visible: false);
		}

		public static T AddAccessoryWindowControl<T>(T control, bool automate_visible) where T : BaseGuiEntry
		{
			control.Category = _accessorySlotWindowCategory;
			control.AutomateVisible = automate_visible;
			_accessoryWindowEntries.Add(control);
			return control;
		}

		public static void InitializeGuiEntries()
		{
			foreach (BaseGuiEntry guiEntry in _guiEntries)
			{
				guiEntry.Initialize();
			}
		}

		[Conditional("DEBUG")]
		private static void DebugControls()
		{
			KoikatuAPI instance = KoikatuAPI.Instance;
			MakerCategory all = MakerConstants.Face.All;
			AddControl(new MakerSeparator(all, (BaseUnityPlugin)(object)instance));
			AddControl(new MakerSeparator(all, (BaseUnityPlugin)(object)instance));
			AddControl(new MakerSeparator(all, (BaseUnityPlugin)(object)instance));
			AddControl(new MakerToggle(all, "test toggle", (BaseUnityPlugin)(object)instance)).ValueChanged.Subscribe(delegate(bool b)
			{
				KoikatuAPI.Logger.LogMessage((object)b);
			});
			AddControl(new MakerButton("test btn", all, (BaseUnityPlugin)(object)instance)).OnClick.AddListener(delegate
			{
				KoikatuAPI.Logger.LogMessage((object)"Clicked");
			});
			AddControl(new MakerColor("test color", useAlpha: true, all, Color.magenta, (BaseUnityPlugin)(object)instance)).ValueChanged.Subscribe(delegate(Color color)
			{
				KoikatuAPI.Logger.LogMessage((object)color);
			});
			AddControl(new MakerDropdown("test toggle", new string[4] { "t0", "t1", "t2", "t3" }, all, 1, (BaseUnityPlugin)(object)instance)).ValueChanged.Subscribe(delegate(int b)
			{
				KoikatuAPI.Logger.LogMessage((object)b);
			});
			AddControl(new MakerRadioButtons(all, (BaseUnityPlugin)(object)instance, "radio btns", "b1", "b2")).ValueChanged.Subscribe(delegate(int b)
			{
				KoikatuAPI.Logger.LogMessage((object)b);
			});
			AddControl(new MakerRadioButtons(all, (BaseUnityPlugin)(object)instance, "radio btns 1 row", "b1", "b2", "b3", "b4", "b5", "b6", "b7", "b8")).ValueChanged.Subscribe(delegate(int b)
			{
				KoikatuAPI.Logger.LogMessage((object)b);
			});
			AddControl(new MakerRadioButtons(all, (BaseUnityPlugin)(object)instance, "radio btns 5 per row", "b1", "b2", "b3", "b4", "b5", "b6", "b7", "b8")
			{
				ColumnCount = 5
			}).ValueChanged.Subscribe(delegate(int b)
			{
				KoikatuAPI.Logger.LogMessage((object)b);
			});
			AddControl(new MakerSlider(all, "test slider", 0f, 1f, 1f, (BaseUnityPlugin)(object)instance)).ValueChanged.Subscribe(delegate(float b)
			{
				KoikatuAPI.Logger.LogMessage((object)b);
			});
			AddControl(new MakerText("test text test text test text test text test text test text test text test text test text test text", all, (BaseUnityPlugin)(object)instance));
			AddControl(new MakerTextbox(all, "test textbox", "String test", (BaseUnityPlugin)(object)instance)).ValueChanged.Subscribe(delegate(string b)
			{
				KoikatuAPI.Logger.LogMessage((object)b);
			});
			MakerLoadToggle.AddLoadToggle(new MakerLoadToggle("Test toggle")).ValueChanged.Subscribe(delegate(bool b)
			{
				KoikatuAPI.Logger.LogMessage((object)b);
			});
			MakerLoadToggle.AddLoadToggle(new MakerLoadToggle("Test toggle 2")).ValueChanged.Subscribe(delegate(bool b)
			{
				KoikatuAPI.Logger.LogMessage((object)b);
			});
			MakerCoordinateLoadToggle.AddLoadToggle(new MakerCoordinateLoadToggle("Test toggle")).ValueChanged.Subscribe(delegate(bool b)
			{
				KoikatuAPI.Logger.LogMessage((object)b);
			});
			MakerCoordinateLoadToggle.AddLoadToggle(new MakerCoordinateLoadToggle("Test toggle 2")).ValueChanged.Subscribe(delegate(bool b)
			{
				KoikatuAPI.Logger.LogMessage((object)b);
			});
			AddSidebarControl(new SidebarToggle("Test toggle", initialValue: false, (BaseUnityPlugin)(object)instance)).ValueChanged.Subscribe(delegate(bool b)
			{
				KoikatuAPI.Logger.LogMessage((object)b);
			});
			AddSidebarControl(new SidebarToggle("Test toggle2", initialValue: true, (BaseUnityPlugin)(object)instance)).ValueChanged.Subscribe(delegate(bool b)
			{
				KoikatuAPI.Logger.LogMessage((object)b);
			});
			AddAccessoryWindowControl(new MakerToggle(all, "test toggle", null)).ValueChanged.Subscribe(delegate(bool b)
			{
				KoikatuAPI.Logger.LogMessage((object)$"Toggled to {b} in accessory slot index {AccessoriesApi.SelectedMakerAccSlot}");
			});
			AddAccessoryWindowControl(new MakerColor("test accessory color", useAlpha: false, all, Color.cyan, (BaseUnityPlugin)(object)instance)
			{
				ColorBoxWidth = 230
			}).ValueChanged.Subscribe(delegate(Color b)
			{
				KoikatuAPI.Logger.LogMessage((object)$"Color to {b} in accessory slot index {AccessoriesApi.SelectedMakerAccSlot}");
			});
		}

		public static void CreateCustomControls()
		{
			foreach (Transform item in GameObject.Find("CvsMenuTree").transform)
			{
				CreateCustomControlsInCategory(item);
			}
			Transform transform = GameObject.Find("CustomScene/CustomRoot/FrontUIGroup/CvsDraw/Top").transform;
			if (_sidebarEntries.Any())
			{
				SidebarSeparator sidebarSeparator = new SidebarSeparator((BaseUnityPlugin)(object)KoikatuAPI.Instance);
				sidebarSeparator.CreateControl(transform);
				foreach (BaseGuiEntry sidebarEntry in _sidebarEntries)
				{
					sidebarEntry.CreateControl(transform);
				}
				KoikatuAPI.Logger.LogDebug((object)($"Added {_sidebarEntries.Count} custom controls " + "to Control Panel sidebar"));
			}
			if (_accessoryWindowEntries.Any())
			{
				CreateCustomAccessoryWindowControls();
			}
			MakerLoadToggle.CreateCustomToggles();
			MakerCoordinateLoadToggle.CreateCustomToggles();
		}

		private static void MakeSidebarScrollable(Transform sidebarTop)
		{
			sidebarTop.Find("sldEyeOpen/Slider").GetComponent<ObservableScrollTrigger>().enabled = false;
			sidebarTop.Find("sldMouthOpen/Slider").GetComponent<ObservableScrollTrigger>().enabled = false;
			sidebarTop.Find("sldLightingX/Slider").GetComponent<ObservableScrollTrigger>().enabled = false;
			sidebarTop.Find("sldLightingY/Slider").GetComponent<ObservableScrollTrigger>().enabled = false;
			List<Transform> list = new List<Transform>();
			foreach (Transform item in sidebarTop)
			{
				list.Add(item);
			}
			GameObject gameObject = DefaultControls.CreateScrollView(default(DefaultControls.Resources));
			gameObject.name = "SidebarScrollView";
			gameObject.transform.SetParent(sidebarTop.transform, worldPositionStays: false);
			ScrollRect component = gameObject.GetComponent<ScrollRect>();
			component.horizontal = false;
			component.scrollSensitivity = 40f;
			component.movementType = ScrollRect.MovementType.Clamped;
			component.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
			component.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
			UnityEngine.Object.DestroyImmediate(component.GetComponent<Image>());
			UnityEngine.Object.DestroyImmediate(component.horizontalScrollbar.gameObject);
			UnityEngine.Object.DestroyImmediate(component.verticalScrollbar.gameObject);
			component.gameObject.AddComponent<LayoutElement>();
			sidebarTop.GetComponent<VerticalLayoutGroup>().childForceExpandHeight = true;
			VerticalLayoutGroup verticalLayoutGroup = component.content.gameObject.AddComponent<VerticalLayoutGroup>();
			verticalLayoutGroup.childControlWidth = true;
			verticalLayoutGroup.childControlHeight = true;
			verticalLayoutGroup.childForceExpandWidth = true;
			verticalLayoutGroup.childForceExpandHeight = false;
			component.content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			foreach (Transform item2 in list)
			{
				item2.SetParent(component.content);
			}
		}

		private static void CreateCustomControlsInCategory(Transform categoryTransfrom)
		{
			foreach (IGrouping<string, BaseGuiEntry> item in from x in _guiEntries
				where x.Category.CategoryName == categoryTransfrom.name
				group x by x.Category.SubCategoryName)
			{
				Transform transform = categoryTransfrom.Find(item.Key);
				if (transform != null)
				{
					CreateCustomControlsInSubCategory(transform, item.ToList());
					continue;
				}
				KoikatuAPI.Logger.LogError((object)($"Failed to add {item.Count()} custom controls " + "to " + categoryTransfrom.name + "/" + item.Key + " - The category was not registered with AddSubCategory."));
			}
		}

		private static void CreateCustomControlsInSubCategory(Transform subCategoryTransform, ICollection<BaseGuiEntry> entriesToAdd, bool accessorieswindow = false)
		{
			if (entriesToAdd.Count == 0)
			{
				return;
			}
			Transform transform = FindSubcategoryContentParent(subCategoryTransform);
			bool flag = transform.childCount > 1;
			foreach (IGrouping<string, BaseGuiEntry> item in from x in entriesToAdd
				group x by x.GroupingID into x
				orderby x.Key
				select x)
			{
				List<BaseGuiEntry> controlList = item.ToList();
				MakerSeparator seperator;
				bool inited;
				if (flag)
				{
					seperator = new MakerSeparator(new MakerCategory(null, null), (BaseUnityPlugin)(object)KoikatuAPI.Instance);
					inited = false;
					foreach (BaseGuiEntry item2 in controlList)
					{
						item2.Visible.Subscribe(delegate
						{
							UpdateSidebarVisibility();
						});
					}
					inited = true;
					UpdateSidebarVisibility();
					seperator.CreateControl(transform);
				}
				foreach (BaseGuiEntry item3 in controlList)
				{
					item3.CreateControl(transform);
				}
				flag = true;
				void UpdateSidebarVisibility()
				{
					if (inited)
					{
						bool flag2 = controlList.Any((BaseGuiEntry c) => c.Visible.Value);
						if (flag2 != seperator.Visible.Value)
						{
							seperator.Visible.OnNext(flag2);
						}
					}
				}
			}
			MakerCategory category = entriesToAdd.First().Category;
			KoikatuAPI.Logger.LogDebug((object)($"Added {entriesToAdd.Count} custom controls " + "to " + category.CategoryName + "/" + category.SubCategoryName));
		}

		private static void CreateCustomAccessoryWindowControls()
		{
			CustomAcsChangeSlot customAcsChangeSlot = UnityEngine.Object.FindObjectOfType<CustomAcsChangeSlot>();
			if (customAcsChangeSlot == null)
			{
				throw new ArgumentNullException("customAcsChangeSlot");
			}
			GameObject gameObject = customAcsChangeSlot.transform.FindLoop("tglSlot01");
			if (gameObject == null)
			{
				throw new ArgumentNullException("tglSlot01GameObject");
			}
			Transform parent = gameObject.transform.parent;
			foreach (Transform item in from Transform x in parent
				where x.name.StartsWith("tglSlot")
				orderby x.name
				select x)
			{
				Transform transform = FindSubcategoryContentParent(item);
				string[] array = new string[2] { "txtExplanation", "txtAcsExplanation" };
				foreach (string n in array)
				{
					Transform transform2 = transform.Find(n);
					if (transform2 != null)
					{
						transform2.GetComponent<LayoutElement>().enabled = false;
						transform2.Cast<Transform>().First().gameObject.SetActive(value: false);
					}
				}
				CreateCustomControlsInSubCategory(item, _accessoryWindowEntries, accessorieswindow: true);
			}
			AccessoriesApi.AutomaticControlVisibility();
		}

		internal static void OnMakerAccSlotAdded(Transform newSlotTransform)
		{
			if (MakerAPI.InsideAndLoaded)
			{
				RemoveCustomControlsInSubCategory(newSlotTransform);
				CreateCustomControlsInSubCategory(newSlotTransform, _accessoryWindowEntries);
			}
		}

		private static void RemoveCustomControlsInSubCategory(Transform newSlotTransform)
		{
			Transform source = FindSubcategoryContentParent(newSlotTransform);
			foreach (Transform item in from Transform x in source
				where x.name.EndsWith(BaseGuiEntry.GuiApiNameAppendix)
				select x)
			{
				UnityEngine.Object.Destroy(item.gameObject);
			}
		}

		internal static Transform FindSubcategoryContentParent(Transform categorySubTransform)
		{
			Transform transform = categorySubTransform.Find("Scroll View/Viewport/Content");
			if (transform != null)
			{
				return transform;
			}
			Transform imgoff = categorySubTransform.Find("imgOff");
			if (imgoff == null)
			{
				return categorySubTransform;
			}
			Transform transform2 = categorySubTransform.Cast<Transform>().First((Transform x) => x != imgoff);
			return transform2.Find("Scroll View/Viewport/Content") ?? transform2;
		}

		/// <summary>
		/// Needs to run before UI_ToggleGroupCtrl.Start of the category runs, or it won't get added properly
		/// </summary>
		internal static void AddMissingSubCategories(UI_ToggleGroupCtrl mainCategory)
		{
			Transform transform = mainCategory.transform;
			if (transform.name == "04_AccessoryTop" || transform.name == "06_SystemTop")
			{
				return;
			}
			bool flag = transform.name == "02_HairTop";
			List<KeyValuePair<Transform, int>> list = new List<KeyValuePair<Transform, int>>();
			foreach (MakerCategory category in _categories)
			{
				if (!(transform.name != category.CategoryName))
				{
					Transform key = transform.Find(category.SubCategoryName) ?? SubCategoryCreator.AddNewSubCategory(mainCategory, category);
					list.Add(new KeyValuePair<Transform, int>(key, category.Position));
				}
			}
			if (flag)
			{
				return;
			}
			foreach (Transform subTransform in transform)
			{
				if (!list.Any((KeyValuePair<Transform, int> x) => x.Key == subTransform))
				{
					MakerCategory builtInCategory = MakerConstants.GetBuiltInCategory(transform.name, subTransform.name);
					if (builtInCategory != null)
					{
						list.Add(new KeyValuePair<Transform, int>(subTransform, builtInCategory.Position));
					}
					else
					{
						KoikatuAPI.Logger.LogWarning((object)("Missing MakerCategory for existing transfrom " + transform.name + " / " + subTransform.name));
					}
				}
			}
			int num = 0;
			foreach (KeyValuePair<Transform, int> item in list.OrderBy((KeyValuePair<Transform, int> x) => x.Value))
			{
				item.Key.SetSiblingIndex(num++);
			}
			((MonoBehaviour)(object)KoikatuAPI.Instance).StartCoroutine(FixCategoryContentOffsets(mainCategory));
		}

		private static IEnumerator FixCategoryContentOffsets(UI_ToggleGroupCtrl mainCategory)
		{
			yield return null;
			int num = 0;
			foreach (Transform item in mainCategory.transform)
			{
				if (item.gameObject.activeSelf)
				{
					Transform transform2 = item.Cast<Transform>().First((Transform x) => !x.name.Equals("imgOff"));
					Vector3 localPosition = transform2.localPosition;
					localPosition.y = 40 * num;
					transform2.localPosition = localPosition;
					num++;
				}
			}
		}

		public static T AddSidebarControl<T>(T control) where T : BaseGuiEntry
		{
			_sidebarEntries.Add(control);
			return control;
		}

		public static CharacterLoadFlags GetCharacterLoadFlags()
		{
			CustomFileWindow customFileWindow = UnityEngine.Object.FindObjectsOfType<CustomFileWindow>().FirstOrDefault((CustomFileWindow i) => i.fwType == CustomFileWindow.FileWindowType.CharaLoad);
			if (customFileWindow == null)
			{
				return null;
			}
			return new CharacterLoadFlags
			{
				Body = customFileWindow.tglChaLoadBody.isOn,
				Clothes = customFileWindow.tglChaLoadCoorde.isOn,
				Hair = customFileWindow.tglChaLoadHair.isOn,
				Face = customFileWindow.tglChaLoadFace.isOn,
				Parameters = customFileWindow.tglChaLoadParam.isOn
			};
		}

		public static CoordinateLoadFlags GetCoordinateLoadFlags()
		{
			Toggle toggle = GameObject.Find("CustomScene/CustomRoot/FrontUIGroup/CustomUIGroup/CvsMenuTree/06_SystemTop/cosFileControl/charaFileWindow/WinRect/CoordinateLoad/Select/tglItem01")?.GetComponentInChildren<Toggle>(includeInactive: true);
			if (toggle == null)
			{
				return null;
			}
			Toggle toggle2 = GameObject.Find("CustomScene/CustomRoot/FrontUIGroup/CustomUIGroup/CvsMenuTree/06_SystemTop/cosFileControl/charaFileWindow/WinRect/CoordinateLoad/Select/tglItem02")?.GetComponentInChildren<Toggle>(includeInactive: true);
			if (toggle2 == null)
			{
				return null;
			}
			return new CoordinateLoadFlags
			{
				Clothes = toggle.isOn,
				Accessories = toggle2.isOn
			};
		}

		internal static void AutomaticAccessoryControlVisibility(bool showAccControls)
		{
			MakerAPI.OnVisibilityTrigger(new AccessoryContolVisibilityArgs(showAccControls));
			if (_accessoryControlShowState == showAccControls)
			{
				return;
			}
			foreach (BaseGuiEntry accessoryWindowEntry in _accessoryWindowEntries)
			{
				if (accessoryWindowEntry.AutomateVisible)
				{
					accessoryWindowEntry.Visible.OnNext(showAccControls);
				}
			}
			_accessoryControlShowState = showAccControls;
		}
	}
}
namespace KKAPI.Maker.UI
{
	/// <summary>
	/// Base of custom controls that have a value that can be changed and watched for changes.
	/// </summary>
	public abstract class BaseEditableGuiEntry<TValue> : BaseGuiEntry
	{
		private readonly BehaviorSubject<TValue> _incomingValue;

		private readonly Subject<TValue> _outgoingValue;

		private bool _firingEnabled;

		/// <summary>
		/// Buttons 1, 2, 3 are values 0, 1, 2
		/// </summary>
		public TValue Value
		{
			get
			{
				return _incomingValue.Value;
			}
			set
			{
				SetValue(value);
			}
		}

		/// <summary>
		/// Fired every time the value is changed, and once when the control is created.
		/// Buttons 1, 2, 3 are values 0, 1, 2
		/// </summary>
		public IObservable<TValue> ValueChanged => _outgoingValue;

		/// <summary>
		/// Use to get value changes for controls. Fired by external value set and by SetNewValue.
		/// </summary>
		protected IObservable<TValue> BufferedValueChanged => _incomingValue;

		/// <inheritdoc />
		protected BaseEditableGuiEntry(MakerCategory category, TValue initialValue, BaseUnityPlugin owner)
			: base(category, owner)
		{
			_incomingValue = new BehaviorSubject<TValue>(initialValue);
			_outgoingValue = new Subject<TValue>();
		}

		/// <summary>
		/// Set the new value and trigger the <see cref="P:KKAPI.Maker.UI.BaseEditableGuiEntry`1.ValueChanged" /> event if the control has been created and the value actually changed.
		/// </summary>
		/// <param name="newValue">Value to set</param>
		public void SetValue(TValue newValue)
		{
			SetValue(newValue, fireEvents: true);
		}

		/// <summary>
		/// Set the new value and optionally trigger the <see cref="P:KKAPI.Maker.UI.BaseEditableGuiEntry`1.ValueChanged" /> event if the control has been created.
		/// </summary>
		/// <param name="newValue">Value to set</param>
		/// <param name="fireEvents">Fire the <see cref="P:KKAPI.Maker.UI.BaseEditableGuiEntry`1.ValueChanged" /> event if the value actually changed.</param>
		public void SetValue(TValue newValue, bool fireEvents)
		{
			if (!object.Equals(newValue, _incomingValue.Value))
			{
				_incomingValue.OnNext(newValue);
				if (_firingEnabled && fireEvents)
				{
					_outgoingValue.OnNext(newValue);
				}
			}
		}

		internal override void CreateControl(Transform subCategoryList)
		{
			bool exists = base.Exists;
			_firingEnabled = false;
			base.CreateControl(subCategoryList);
			_firingEnabled = true;
			if (!exists)
			{
				_incomingValue.OnNext(_incomingValue.Value);
				if (!MakerAPI.InsideAndLoaded)
				{
					_outgoingValue.OnNext(_incomingValue.Value);
				}
			}
		}

		/// <inheritdoc />
		public override void Dispose()
		{
			_incomingValue.Dispose();
			_outgoingValue.Dispose();
			base.Dispose();
		}
	}
	/// <summary>
	/// Base of all custom character maker controls.
	/// </summary>
	public abstract class BaseGuiEntry : IDisposable
	{
		/// <summary>
		/// Added to the end of most custom controls to mark them as being created by this API.
		/// </summary>
		public static readonly string GuiApiNameAppendix = "(MakerAPI)";

		private static Transform _guiCacheTransfrom;

		private readonly List<GameObject> _controlObjects = new List<GameObject>(1);

		private static readonly Type[] _localisationComponentTypes = (from t in typeof(Manager.Scene).Assembly.GetTypes()
			where string.Equals(t.Namespace, "Localize.Translate", StringComparison.Ordinal)
			where typeof(UnityEngine.Component).IsAssignableFrom(t)
			select t).ToArray();

		/// <summary>
		/// Category and subcategory that this control is inside of.
		/// </summary>
		public MakerCategory Category { get; internal set; }

		/// <summary>
		/// Parent transform that holds temporary gui entries used to instantiate custom controls.
		/// </summary>
		protected internal static Transform GuiCacheTransfrom
		{
			get
			{
				if (_guiCacheTransfrom == null)
				{
					GameObject gameObject = new GameObject("MakerAPI Cache");
					gameObject.transform.SetParent(((UnityEngine.Component)(object)KoikatuAPI.Instance).transform);
					_guiCacheTransfrom = gameObject.transform;
				}
				return _guiCacheTransfrom;
			}
		}

		/// <summary>
		/// If true, the control has been disposed and can no longer be used, likely because the character maker exited.
		/// A new control has to be created to be used again.
		/// </summary>
		public bool IsDisposed { get; private set; }

		/// <summary>
		/// Text color of the control's description text (usually on the left).
		/// Can only set this before the control is created.
		/// </summary>
		public Color TextColor { get; set; } = MakerConstants.DefaultControlTextColor;

		/// <summary>
		/// The plugin that owns this custom control.
		/// </summary>
		public BaseUnityPlugin Owner { get; }

		/// <summary>
		/// ID used when grouping controls into groups separated by separators. Groups are sorted alphabetically.
		/// If multiple plugins use the same group, the controls are placed together.
		/// By default each plugin has its own group. Set to null to use the "misc" group.
		/// </summary>
		public string GroupingID { get; set; }

		/// <summary>
		/// If true, Api automates showing and hiding of accessory controls (hide if no accessory is selected)
		/// </summary>
		internal bool AutomateVisible { get; set; }

		/// <summary>
		/// The control is visible to the user (usually the same as it's GameObject being active).
		/// </summary>
		public BehaviorSubject<bool> Visible { get; }

		/// <summary>
		/// GameObject(s) of the control. Populated once instantiated.
		/// Contains 1 item in most cases, can contain multiple in case of accessory window controls.
		/// </summary>
		public IEnumerable<GameObject> ControlObjects => _controlObjects.Where((GameObject x) => x != null);

		/// <summary>
		/// GameObject of the control. Populated once instantiated.
		/// If there are multiple objects, returns one of them. Use <see cref="P:KKAPI.Maker.UI.BaseGuiEntry.ControlObjects" /> in that case.
		/// </summary>
		public GameObject ControlObject => _controlObjects.FirstOrDefault((GameObject x) => x != null);

		/// <summary>
		/// True if the control is currently instantiated in the scene
		/// </summary>
		public bool Exists => _controlObjects.Any((GameObject x) => x != null);

		/// <summary>
		/// Create a new custom control
		/// </summary>
		/// <param name="category">Category the control will be created under</param>
		/// <param name="owner">Plugin that owns the control</param>
		protected BaseGuiEntry(MakerCategory category, BaseUnityPlugin owner)
		{
			Category = category;
			Owner = owner;
			BaseUnityPlugin owner2 = Owner;
			object obj;
			if (owner2 == null)
			{
				obj = null;
			}
			else
			{
				PluginInfo info = owner2.Info;
				obj = ((info != null) ? info.Metadata : null);
			}
			BepInPlugin val = (BepInPlugin)obj;
			if (val != null)
			{
				GroupingID = val.GUID;
			}
			Visible = new BehaviorSubject<bool>(defaultValue: true);
			Visible.Subscribe(delegate(bool b)
			{
				foreach (GameObject controlObject in ControlObjects)
				{
					controlObject.SetActive(b);
				}
			});
		}

		internal static void RemoveLocalisation(GameObject control)
		{
			Type[] localisationComponentTypes = _localisationComponentTypes;
			foreach (Type type in localisationComponentTypes)
			{
				UnityEngine.Component[] componentsInChildren = control.GetComponentsInChildren(type, includeInactive: true);
				foreach (UnityEngine.Component obj in componentsInChildren)
				{
					UnityEngine.Object.DestroyImmediate(obj, allowDestroyingAssets: false);
				}
			}
		}

		internal static void SetTextAutosize(Text txtCmp)
		{
			txtCmp.resizeTextMaxSize = txtCmp.fontSize;
			txtCmp.resizeTextForBestFit = true;
			txtCmp.verticalOverflow = VerticalWrapMode.Truncate;
			txtCmp.horizontalOverflow = HorizontalWrapMode.Wrap;
		}

		/// <summary>
		/// Find first control of this name under the specified category transform
		/// </summary>
		protected static Transform GetExistingControl(string categoryPath, string controlName)
		{
			GameObject gameObject = GameObject.Find(categoryPath);
			Transform transform = MakerInterfaceCreator.FindSubcategoryContentParent(gameObject.transform);
			return transform.Find(controlName);
		}

		/// <summary>
		/// Called before OnCreateControl to setup the object before instantiating the control.
		/// </summary>
		protected internal abstract void Initialize();

		/// <summary>
		/// Remove the control. Called when maker is quitting.
		/// </summary>
		/// <inheritdoc />
		public virtual void Dispose()
		{
			IsDisposed = true;
		}

		internal void ThrowIfDisposed(string paramName)
		{
			if (IsDisposed)
			{
				throw new ObjectDisposedException(paramName, "A new control has to be created every time maker is started");
			}
		}

		internal virtual void CreateControl(Transform subCategoryList)
		{
			GameObject gameObject = OnCreateControl(subCategoryList);
			gameObject.name += GuiApiNameAppendix;
			if (MakerAPI.InsideMaker)
			{
				LayoutElement component = gameObject.GetComponent<LayoutElement>();
				if (component != null)
				{
					component.minWidth = 300f;
				}
			}
			gameObject.SetActive(Visible.Value);
			_controlObjects.Add(gameObject);
		}

		/// <summary>
		/// Used by the API to actually create the custom control.
		/// Should return main GameObject of the control
		/// </summary>
		protected abstract GameObject OnCreateControl(Transform subCategoryList);
	}
	/// <summary>
	/// Custom control that draws a simple blue button.
	/// </summary>
	public class MakerButton : BaseGuiEntry
	{
		private static Transform _buttonCopy;

		/// <summary>
		/// Fired when user clicks on the button
		/// </summary>
		public Button.ButtonClickedEvent OnClick { get; }

		/// <summary>
		/// Text displayed on the button
		/// </summary>
		public string Text { get; }

		private static Transform ButtonCopy
		{
			get
			{
				if (_buttonCopy == null)
				{
					MakeCopy();
				}
				return _buttonCopy;
			}
		}

		/// <summary>
		/// Create a new custom control. Create and register it in <see cref="E:KKAPI.Maker.MakerAPI.RegisterCustomSubCategories" />.
		/// </summary>
		/// <param name="text">Text displayed on the button</param>
		/// <param name="category">Category the control will be created under</param>
		/// <param name="owner">Plugin that owns the control</param>
		public MakerButton(string text, MakerCategory category, BaseUnityPlugin owner)
			: base(category, owner)
		{
			Text = text;
			OnClick = new Button.ButtonClickedEvent();
		}

		private static void MakeCopy()
		{
			Transform existingControl = BaseGuiEntry.GetExistingControl("CustomScene/CustomRoot/FrontUIGroup/CustomUIGroup/CvsMenuTree/02_HairTop/tglBack", "grpBtn");
			_buttonCopy = UnityEngine.Object.Instantiate(existingControl, BaseGuiEntry.GuiCacheTransfrom, worldPositionStays: false);
			_buttonCopy.gameObject.SetActive(value: false);
			_buttonCopy.name = "btnCustom";
			Button componentInChildren = _buttonCopy.GetComponentInChildren<Button>();
			componentInChildren.onClick.RemoveAllListeners();
			componentInChildren.targetGraphic.raycastTarget = true;
			BaseGuiEntry.RemoveLocalisation(_buttonCopy.gameObject);
		}

		/// <inheritdoc />
		protected internal override void Initialize()
		{
			if (_buttonCopy == null)
			{
				MakeCopy();
			}
		}

		/// <inheritdoc />
		public override void Dispose()
		{
			OnClick.RemoveAllListeners();
			base.Dispose();
		}

		/// <inheritdoc />
		protected override GameObject OnCreateControl(Transform subCategoryList)
		{
			Transform transform = UnityEngine.Object.Instantiate(ButtonCopy, subCategoryList, worldPositionStays: false);
			Button componentInChildren = transform.GetComponentInChildren<Button>();
			componentInChildren.onClick.AddListener(OnClick.Invoke);
			TextMeshProUGUI componentInChildren2 = transform.GetComponentInChildren<TextMeshProUGUI>();
			componentInChildren2.text = Text;
			componentInChildren2.color = base.TextColor;
			LayoutElement component = transform.GetComponent<LayoutElement>();
			component.flexibleWidth = 1f;
			return transform.gameObject;
		}
	}
	/// <summary>
	/// Control that allows user to change a <see cref="T:UnityEngine.Color" /> in a separate color selector window
	/// </summary>
	public class MakerColor : BaseEditableGuiEntry<Color>
	{
		private static Transform _colorCopy;

		private readonly BehaviorSubject<int> _colorBoxWidth = new BehaviorSubject<int>(276);

		/// <summary>
		/// Name of the setting
		/// </summary>
		public string SettingName { get; }

		/// <summary>
		/// If true, the color selector will allow the user to change alpha of the color.
		/// If false, no color slider is shown and alpha is always 1f.
		/// </summary>
		public bool UseAlpha { get; }

		/// <summary>
		/// Width of the color box. Can adjust this to allow for longer label text.
		/// Default width is 276 and might need to get lowered to allow longer labels.
		/// The default color boxes in accessory window are 230 wide.
		/// </summary>
		public int ColorBoxWidth
		{
			get
			{
				return _colorBoxWidth.Value;
			}
			set
			{
				_colorBoxWidth.OnNext(value);
			}
		}

		private static Transform ColorCopy
		{
			get
			{
				if (_colorCopy == null)
				{
					MakeCopy();
				}
				return _colorCopy;
			}
		}

		/// <summary>
		/// Create a new custom control. Create and register it in <see cref="E:KKAPI.Maker.MakerAPI.RegisterCustomSubCategories" />.
		/// </summary>
		/// <param name="settingName">Text displayed next to the control</param>
		/// <param name="useAlpha">
		/// If true, the color selector will allow the user to change alpha of the color.
		/// If false, no color slider is shown and alpha is always 1f.
		/// </param>
		/// <param name="category">Category the control will be created under</param>
		/// <param name="initialValue">Color set to the control when it is created</param>
		/// <param name="owner">Plugin that owns the control</param>
		public MakerColor(string settingName, bool useAlpha, MakerCategory category, Color initialValue, BaseUnityPlugin owner)
			: base(category, initialValue, owner)
		{
			SettingName = settingName;
			UseAlpha = useAlpha;
		}

		private static void MakeCopy()
		{
			Transform existingControl = BaseGuiEntry.GetExistingControl("CustomScene/CustomRoot/FrontUIGroup/CustomUIGroup/CvsMenuTree/00_FaceTop/tglMole", "btnMoleColor");
			_colorCopy = UnityEngine.Object.Instantiate(existingControl, BaseGuiEntry.GuiCacheTransfrom, worldPositionStays: false);
			_colorCopy.gameObject.SetActive(value: false);
			_colorCopy.name = "btnColor";
			Button componentInChildren = _colorCopy.GetComponentInChildren<Button>();
			componentInChildren.onClick.RemoveAllListeners();
			componentInChildren.targetGraphic.raycastTarget = true;
			BaseGuiEntry.RemoveLocalisation(_colorCopy.gameObject);
		}

		/// <inheritdoc />
		protected internal override void Initialize()
		{
			if (_colorCopy == null)
			{
				MakeCopy();
			}
		}

		/// <inheritdoc />
		protected override GameObject OnCreateControl(Transform subCategoryList)
		{
			Transform transform = UnityEngine.Object.Instantiate(ColorCopy, subCategoryList, worldPositionStays: false);
			TextMeshProUGUI componentInChildren = transform.GetComponentInChildren<TextMeshProUGUI>();
			componentInChildren.text = SettingName;
			componentInChildren.color = base.TextColor;
			CvsColor cvsColor = GameObject.Find("CustomScene/CustomRoot/FrontUIGroup/CustomUIGroup/CvsColor/Top").GetComponent<CvsColor>();
			Button componentInChildren2 = transform.GetComponentInChildren<Button>();
			CvsColor.ConnectColorKind connectColorKind = (CvsColor.ConnectColorKind)SettingName.GetHashCode();
			componentInChildren2.onClick.AddListener(delegate
			{
				if (cvsColor.isOpen && cvsColor.connectColorKind == connectColorKind)
				{
					cvsColor.Close();
				}
				else
				{
					MethodInfo methodInfo = AccessTools.Method(typeof(CvsColor), "Setup", (Type[])null, (Type[])null);
					switch (methodInfo.GetParameters().Length)
					{
					case 5:
						methodInfo.Invoke(cvsColor, new object[5]
						{
							SettingName,
							connectColorKind,
							base.Value,
							new Action<Color>(base.SetValue),
							UseAlpha
						});
						break;
					case 6:
						methodInfo.Invoke(cvsColor, new object[6]
						{
							SettingName,
							connectColorKind,
							base.Value,
							new Action<Color>(base.SetValue),
							(Action)delegate
							{
							},
							UseAlpha
						});
						break;
					default:
						throw new InvalidOperationException("Where am I what is this help me");
					}
				}
			});
			Image previewImg = transform.Find("backrect/alpha/imgColorSample").GetComponent<Image>();
			base.BufferedValueChanged.Subscribe(delegate(Color c)
			{
				previewImg.color = c;
			});
			RectTransform boxRt = transform.Find("backrect").GetComponent<RectTransform>();
			_colorBoxWidth.Subscribe(delegate(int width)
			{
				boxRt.offsetMin = new Vector2(width * -1, boxRt.offsetMin.y);
			});
			LayoutElement component = transform.GetComponent<LayoutElement>();
			component.flexibleWidth = 1f;
			return transform.gameObject;
		}
	}
	/// <summary>
	/// Adds a toggle to the bottom of the coordinate/clothes card load window in character maker.
	/// Use to allow user to not load data related to your mod.
	/// Use with <see cref="M:KKAPI.Maker.UI.MakerCoordinateLoadToggle.AddLoadToggle(KKAPI.Maker.UI.MakerCoordinateLoadToggle)" />
	/// </summary>
	public class MakerCoordinateLoadToggle : BaseEditableGuiEntry<bool>
	{
		private const int TotalWidth = 672;

		private static readonly int _verticalOffset;

		private static readonly List<MakerCoordinateLoadToggle> Toggles;

		private static Transform _baseToggle;

		private static GameObject _root;

		private static int _createdCount;

		private static List<RectTransform> _baseToggles;

		/// <summary>
		/// Text displayed next to the toggle
		/// </summary>
		public string Text { get; }

		/// <summary>
		/// Check if any of the custom toggles are checked
		/// </summary>
		public static bool AnyEnabled => Toggles.Any((MakerCoordinateLoadToggle x) => x.Value);

		internal static Button LoadButton { get; private set; }

		static MakerCoordinateLoadToggle()
		{
			Toggles = new List<MakerCoordinateLoadToggle>();
			_verticalOffset = (Chainloader.PluginInfos.ContainsKey("KK_ClothesLoadOption") ? 52 : 26);
		}

		/// <summary>
		/// Create a new coordinate load toggle. Create and register it in <see cref="E:KKAPI.Maker.MakerAPI.RegisterCustomSubCategories" /> 
		/// with <see cref="M:KKAPI.Maker.RegisterCustomControlsEvent.AddCoordinateLoadToggle(KKAPI.Maker.UI.MakerCoordinateLoadToggle)" />.
		/// </summary>
		/// <param name="text">Text displayed next to the checkbox</param>
		/// <param name="initialValue">Initial value of the toggle</param>
		public MakerCoordinateLoadToggle(string text, bool initialValue = true)
			: base((MakerCategory)null, initialValue, (BaseUnityPlugin)null)
		{
			Text = text;
		}

		internal static void CreateCustomToggles()
		{
			foreach (MakerCoordinateLoadToggle toggle in Toggles)
			{
				toggle.CreateControl(_root.transform);
			}
			int num = 672 / (_baseToggles.Count + Toggles.Count);
			for (int i = 0; i < _baseToggles.Count; i++)
			{
				RectTransform rectTransform = _baseToggles[i];
				rectTransform.localPosition = new Vector3(_baseToggle.localPosition.x + (float)(num * i), _verticalOffset, 0f);
				rectTransform.offsetMax = new Vector2(rectTransform.offsetMin.x + (float)num, rectTransform.offsetMin.y + 26f);
				((MonoBehaviour)(object)KoikatuAPI.Instance).StartCoroutine(FixLayout(rectTransform));
			}
		}

		/// <inheritdoc />
		protected override GameObject OnCreateControl(Transform loadBoxTransform)
		{
			Transform transform = UnityEngine.Object.Instantiate(_baseToggle, _root.transform);
			transform.name = "tglItem";
			BaseGuiEntry.RemoveLocalisation(transform.gameObject);
			Toggle tgl = transform.GetComponentInChildren<Toggle>();
			tgl.onValueChanged.AddListener(base.SetValue);
			base.BufferedValueChanged.Subscribe(delegate(bool b)
			{
				tgl.isOn = b;
			});
			tgl.image.raycastTarget = true;
			tgl.graphic.raycastTarget = true;
			TextMeshProUGUI componentInChildren = transform.GetComponentInChildren<TextMeshProUGUI>();
			componentInChildren.text = Text;
			int num = 672 / (_baseToggles.Count + Toggles.Count);
			RectTransform component = transform.GetComponent<RectTransform>();
			component.localPosition = new Vector3(_baseToggle.localPosition.x + (float)(num * _createdCount), _verticalOffset, 0f);
			component.offsetMax = new Vector2(component.offsetMin.x + (float)num, component.offsetMin.y + 26f);
			transform.gameObject.SetActive(value: true);
			_createdCount++;
			((MonoBehaviour)(object)KoikatuAPI.Instance).StartCoroutine(FixLayout(component));
			return transform.gameObject;
		}

		private static IEnumerator FixLayout(RectTransform rt)
		{
			yield return new WaitUntil(() => rt == null || rt.gameObject.activeInHierarchy);
			yield return null;
			rt.GetComponent<HorizontalLayoutGroup>().enabled = true;
			LayoutRebuilder.MarkLayoutForRebuild(rt);
		}

		/// <inheritdoc />
		protected internal override void Initialize()
		{
		}

		internal static MakerCoordinateLoadToggle AddLoadToggle(MakerCoordinateLoadToggle toggle)
		{
			if (toggle == null)
			{
				throw new ArgumentNullException("toggle");
			}
			toggle.ThrowIfDisposed("toggle");
			Toggles.Add(toggle);
			return toggle;
		}

		internal static void Reset()
		{
			foreach (MakerCoordinateLoadToggle toggle in Toggles)
			{
				toggle.Dispose();
			}
			Toggles.Clear();
			_createdCount = 0;
			LoadButton = null;
		}

		internal static void Setup()
		{
			Reset();
			_root = GetRootObject();
			_baseToggles = MakerLoadToggle.GetBaseToggles(_root.transform);
			_createdCount = _baseToggles.Count;
			_baseToggle = _baseToggles[0];
			LoadButton = _root.transform.parent.Find("btnLoad").GetComponent<Button>();
		}

		private static GameObject GetRootObject()
		{
			return GameObject.Find("CustomScene/CustomRoot/FrontUIGroup/CustomUIGroup/CvsMenuTree/06_SystemTop/cosFileControl/charaFileWindow/WinRect/CoordinateLoad/Select");
		}
	}
	/// <summary>
	/// Custom control that draws a dropdown list
	/// </summary>
	public class MakerDropdown : BaseEditableGuiEntry<int>
	{
		private static Transform _dropdownCopy;

		/// <summary>
		/// List of all options in the dropdown
		/// </summary>
		public string[] Options { get; }

		/// <summary>
		/// Name displayed next to the dropdown
		/// </summary>
		public string SettingName { get; }

		private static Transform DropdownCopy
		{
			get
			{
				if (_dropdownCopy == null)
				{
					MakeCopy();
				}
				return _dropdownCopy;
			}
		}

		/// <summary>
		/// Create a new custom control. Create and register it in <see cref="E:KKAPI.Maker.MakerAPI.RegisterCustomSubCategories" />.
		/// </summary>
		/// <param name="settingName">Text displayed next to the dropdown</param>
		/// <param name="options">Items for the dropdown menu</param>
		/// <param name="category">Category the control will be created under</param>
		/// <param name="initialValue">Initially selected item in the dropdown menu</param>
		/// <param name="owner">Plugin that owns the control</param>
		public MakerDropdown(string settingName, string[] options, MakerCategory category, int initialValue, BaseUnityPlugin owner)
			: base(category, initialValue, owner)
		{
			SettingName = settingName;
			Options = options;
		}

		private static void MakeCopy()
		{
			Transform existingControl = BaseGuiEntry.GetExistingControl("CustomScene/CustomRoot/FrontUIGroup/CustomUIGroup/CvsMenuTree/06_SystemTop/tglVisualSettings", "ddRampG");
			_dropdownCopy = UnityEngine.Object.Instantiate(existingControl, BaseGuiEntry.GuiCacheTransfrom, worldPositionStays: false);
			_dropdownCopy.gameObject.SetActive(value: false);
			_dropdownCopy.name = "ddList";
			TMP_Dropdown componentInChildren = _dropdownCopy.GetComponentInChildren<TMP_Dropdown>();
			componentInChildren.onValueChanged.RemoveAllListeners();
			componentInChildren.ClearOptions();
			componentInChildren.template.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 540f);
			Image[] componentsInChildren = componentInChildren.GetComponentsInChildren<Image>(includeInactive: true);
			foreach (Image image in componentsInChildren)
			{
				image.raycastTarget = true;
			}
			BaseGuiEntry.RemoveLocalisation(_dropdownCopy.gameObject);
		}

		/// <inheritdoc />
		protected internal override void Initialize()
		{
			if (_dropdownCopy == null)
			{
				MakeCopy();
			}
		}

		/// <inheritdoc />
		protected override GameObject OnCreateControl(Transform subCategoryList)
		{
			Transform transform = UnityEngine.Object.Instantiate(DropdownCopy, subCategoryList, worldPositionStays: false);
			TextMeshProUGUI component = transform.Find("textKindTitle").GetComponent<TextMeshProUGUI>();
			component.text = SettingName;
			component.color = base.TextColor;
			TMP_Dropdown dropdown = transform.GetComponentInChildren<TMP_Dropdown>();
			dropdown.options.AddRange(Options.Select((string x) => new TMP_Dropdown.OptionData(x)));
			dropdown.onValueChanged.AddListener(base.SetValue);
			base.BufferedValueChanged.Subscribe(delegate(int i)
			{
				dropdown.value = i;
			});
			if (base.Value == dropdown.value)
			{
				dropdown.RefreshShownValue();
				SetValue(dropdown.value);
			}
			LayoutElement component2 = transform.GetComponent<LayoutElement>();
			component2.flexibleWidth = 1f;
			return transform.gameObject;
		}
	}
	/// <summary>
	/// Custom control that displays a texture in a small preview thumbnail
	/// </summary>
	public class MakerImage : BaseGuiEntry
	{
		private readonly BehaviorSubject<Texture> _texture;

		private int _width = 100;

		private int _height = 100;

		/// <summary>
		/// Texture to display in the preview
		/// </summary>
		public Texture Texture
		{
			get
			{
				return _texture.Value;
			}
			set
			{
				_texture.OnNext(value);
			}
		}

		/// <summary>
		/// Width of the texture preview
		/// </summary>
		public int Width
		{
			get
			{
				return _width;
			}
			set
			{
				_width = value;
				_texture.OnNext(_texture.Value);
			}
		}

		/// <summary>
		/// Height of the texture preview
		/// </summary>
		public int Height
		{
			get
			{
				return _height;
			}
			set
			{
				_height = value;
				_texture.OnNext(_texture.Value);
			}
		}

		/// <summary>
		/// Create a new custom control. Create and register it in <see cref="E:KKAPI.Maker.MakerAPI.RegisterCustomSubCategories" />.
		/// </summary>
		/// <param name="texture">Texture to be displayed in the image box. Can be null for empty.</param>
		/// <param name="category">Category the control will be created under</param>
		/// <param name="owner">Plugin that owns the control</param>
		public MakerImage(Texture texture, MakerCategory category, BaseUnityPlugin owner)
			: base(category, owner)
		{
			_texture = new BehaviorSubject<Texture>(texture);
		}

		/// <inheritdoc />
		protected internal override void Initialize()
		{
		}

		/// <inheritdoc />
		protected override GameObject OnCreateControl(Transform subCategoryList)
		{
			GameObject gameObject = new GameObject("image", typeof(RectTransform), typeof(LayoutElement));
			gameObject.transform.SetParent(subCategoryList, worldPositionStays: false);
			gameObject.layer = 5;
			LayoutElement le = gameObject.GetComponent<LayoutElement>();
			le.minWidth = 456f;
			GameObject gameObject2 = new GameObject("img", typeof(RectTransform), typeof(CanvasRenderer));
			gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
			gameObject2.layer = 5;
			RawImage i = gameObject2.AddComponent<RawImage>();
			RectTransform irt = gameObject2.GetComponent<RectTransform>();
			_texture.Subscribe(delegate(Texture texture)
			{
				i.texture = texture;
				le.minHeight = Height + 30;
				irt.offsetMin = new Vector2((float)(-1 * Width) / 2f, (float)(-1 * Height) / 2f);
				irt.offsetMax = new Vector2((float)Width / 2f, (float)Height / 2f);
				le.enabled = false;
				le.enabled = true;
			});
			return gameObject;
		}
	}
	/// <summary>
	/// Adds a toggle to the bottom of the character card load window in character maker.
	/// Use to allow user to not load data related to your mod.
	/// Use with <see cref="M:KKAPI.Maker.UI.MakerLoadToggle.AddLoadToggle(KKAPI.Maker.UI.MakerLoadToggle)" />
	/// </summary>
	public class MakerLoadToggle : BaseEditableGuiEntry<bool>
	{
		private const int TotalWidth = 647;

		private static readonly List<MakerLoadToggle> Toggles = new List<MakerLoadToggle>();

		private static Transform _baseToggle;

		private static GameObject _root;

		private static int _createdCount;

		/// <summary>
		/// Text displayed next to the toggle
		/// </summary>
		public string Text { get; }

		/// <summary>
		/// Check if any of the custom toggles are checked
		/// </summary>
		public static bool AnyEnabled => Toggles.Any((MakerLoadToggle x) => x.Value);

		internal static Button LoadButton { get; private set; }

		/// <summary>
		/// Create a new load toggle. Create and register it in <see cref="E:KKAPI.Maker.MakerAPI.RegisterCustomSubCategories" /> 
		/// with <see cref="M:KKAPI.Maker.RegisterCustomControlsEvent.AddLoadToggle(KKAPI.Maker.UI.MakerLoadToggle)" />.
		/// </summary>
		/// <param name="text">Text displayed next to the checkbox</param>
		/// <param name="initialValue">Initial value of the toggle</param>
		public MakerLoadToggle(string text, bool initialValue = true)
			: base((MakerCategory)null, initialValue, (BaseUnityPlugin)null)
		{
			Text = text;
		}

		internal static void CreateCustomToggles()
		{
			foreach (MakerLoadToggle toggle in Toggles)
			{
				toggle.CreateControl(_root.transform);
			}
		}

		/// <inheritdoc />
		protected override GameObject OnCreateControl(Transform loadBoxTransform)
		{
			Transform transform = UnityEngine.Object.Instantiate(_baseToggle, _root.transform);
			transform.name = "tglItem";
			BaseGuiEntry.RemoveLocalisation(transform.gameObject);
			Toggle tgl = transform.GetComponentInChildren<Toggle>();
			tgl.onValueChanged.AddListener(base.SetValue);
			base.BufferedValueChanged.Subscribe(delegate(bool b)
			{
				tgl.isOn = b;
			});
			tgl.image.raycastTarget = true;
			tgl.graphic.raycastTarget = true;
			TextMeshProUGUI componentInChildren = transform.GetComponentInChildren<TextMeshProUGUI>();
			componentInChildren.text = Text;
			int num = 647 / Toggles.Count;
			RectTransform component = transform.GetComponent<RectTransform>();
			component.localPosition = new Vector3(_baseToggle.localPosition.x + (float)(num * _createdCount), 26f);
			component.offsetMax = new Vector2(component.offsetMin.x + (float)num, component.offsetMin.y + 26f);
			transform.gameObject.SetActive(value: true);
			_createdCount++;
			((MonoBehaviour)(object)KoikatuAPI.Instance).StartCoroutine(FixLayout(component));
			return transform.gameObject;
		}

		private static IEnumerator FixLayout(RectTransform rt)
		{
			yield return new WaitUntil(() => !rt || rt.gameObject.activeInHierarchy);
			if ((bool)rt)
			{
				yield return null;
				rt.GetComponent<HorizontalLayoutGroup>().enabled = true;
				LayoutRebuilder.MarkLayoutForRebuild(rt);
			}
		}

		/// <inheritdoc />
		protected internal override void Initialize()
		{
		}

		internal static MakerLoadToggle AddLoadToggle(MakerLoadToggle toggle)
		{
			if (toggle == null)
			{
				throw new ArgumentNullException("toggle");
			}
			toggle.ThrowIfDisposed("toggle");
			Toggles.Add(toggle);
			return toggle;
		}

		internal static void Reset()
		{
			foreach (MakerLoadToggle toggle in Toggles)
			{
				toggle.Dispose();
			}
			Toggles.Clear();
			_createdCount = 0;
			LoadButton = null;
		}

		internal static void Setup()
		{
			Reset();
			_root = GetRootObject();
			List<RectTransform> baseToggles = GetBaseToggles(_root.transform);
			_baseToggle = baseToggles[0];
			int num = 647 / baseToggles.Count;
			for (int i = 0; i < baseToggles.Count; i++)
			{
				RectTransform rectTransform = baseToggles[i];
				rectTransform.localPosition = new Vector3(_baseToggle.localPosition.x + (float)(num * i), 52f, 0f);
				rectTransform.offsetMax = new Vector2(rectTransform.offsetMin.x + (float)num, rectTransform.offsetMin.y + 26f);
				((MonoBehaviour)(object)KoikatuAPI.Instance).StartCoroutine(FixLayout(rectTransform));
			}
			Transform transform = _root.transform.Find("btnAllOn");
			transform.GetComponentInChildren<Button>().onClick.AddListener(OnAllOn);
			Transform transform2 = _root.transform.Find("btnAllOff");
			transform2.GetComponentInChildren<Button>().onClick.AddListener(OnAllOff);
			transform.GetComponent<RectTransform>().sizeDelta = new Vector2(82f, 24f);
			transform2.GetComponent<RectTransform>().sizeDelta = new Vector2(82f, 24f);
			LoadButton = _root.transform.parent.Find("btnLoad").GetComponent<Button>();
		}

		internal static List<RectTransform> GetBaseToggles(Transform root)
		{
			List<RectTransform> list = new List<RectTransform>();
			foreach (Transform item in root.transform.Cast<Transform>().ToList())
			{
				if (!item.gameObject.activeSelf)
				{
					continue;
				}
				if (item.name.StartsWith("tglHeader"))
				{
					foreach (Transform item2 in item.Cast<Transform>().ToList())
					{
						item2.SetParent(root.transform, worldPositionStays: false);
						list.Add(item2.GetComponent<RectTransform>());
					}
				}
				else if (item.name.StartsWith("tglItem"))
				{
					list.Add(item.GetComponent<RectTransform>());
				}
			}
			return list;
		}

		private static GameObject GetRootObject()
		{
			return GameObject.Find("CustomScene/CustomRoot/FrontUIGroup/CustomUIGroup/CvsMenuTree/06_SystemTop/charaFileControl/charaFileWindow/WinRect/CharaLoad/Select");
		}

		private static void OnAllOff()
		{
			foreach (MakerLoadToggle toggle in Toggles)
			{
				if (toggle != null)
				{
					toggle.Value = false;
				}
			}
		}

		private static void OnAllOn()
		{
			foreach (MakerLoadToggle toggle in Toggles)
			{
				if (toggle != null)
				{
					toggle.Value = true;
				}
			}
		}
	}
	/// <summary>
	/// Custom control that displays multiple radio buttons
	/// </summary>
	public class MakerRadioButtons : BaseEditableGuiEntry<int>
	{
		private readonly string _settingName;

		private readonly string[] _buttons;

		private static Transform _radioCopy;

		/// <summary>
		/// Objects of all of the radio buttons
		/// </summary>
		public ReadOnlyCollection<Toggle> Buttons { get; private set; }

		/// <summary>
		/// How many buttons to show in a each row. By default show all buttons in a single row.
		/// If set to 3, 3 buttons will be shown in a row, then the next 3 buttons will be shown in a row below, and so on.
		/// </summary>
		public int ColumnCount { get; set; } = 9999;

		private static Transform RadioCopy
		{
			get
			{
				if (_radioCopy == null)
				{
					MakeCopy();
				}
				return _radioCopy;
			}
		}

		/// <summary>
		/// Create a new custom control. Create and register it in <see cref="E:KKAPI.Maker.MakerAPI.RegisterCustomSubCategories" />.
		/// </summary>
		/// <param name="settingName">Text displayed next to the buttons</param>
		/// <param name="category">Category the control will be created under</param>
		/// <param name="owner">Plugin that owns the control</param>
		/// <param name="buttons">Names of the radio buttons. Need at least 2 buttons.</param>
		public MakerRadioButtons(MakerCategory category, BaseUnityPlugin owner, string settingName, params string[] buttons)
			: this(category, owner, settingName, 0, buttons)
		{
		}

		/// <summary>
		/// Create a new custom control. Create and register it in <see cref="E:KKAPI.Maker.MakerAPI.RegisterCustomSubCategories" />.
		/// </summary>
		/// <param name="settingName">Text displayed next to the buttons</param>
		/// <param name="category">Category the control will be created under</param>
		/// <param name="owner">Plugin that owns the control</param>
		/// <param name="initialValue">Initial value of the control</param>
		/// <param name="buttons">Names of the radio buttons. Need at least 2 buttons.</param>
		public MakerRadioButtons(MakerCategory category, BaseUnityPlugin owner, string settingName, int initialValue, params string[] buttons)
			: base(category, initialValue, owner)
		{
			if (buttons.Length < 2)
			{
				throw new ArgumentException("Need at least two buttons.", "buttons");
			}
			_settingName = settingName;
			_buttons = buttons;
		}

		/// <inheritdoc />
		protected internal override void Initialize()
		{
			if (_radioCopy == null)
			{
				MakeCopy();
			}
		}

		/// <inheritdoc />
		protected override GameObject OnCreateControl(Transform subCategoryList)
		{
			Transform tr = UnityEngine.Object.Instantiate(RadioCopy, subCategoryList, worldPositionStays: false);
			tr.name = "rb";
			TextMeshProUGUI component = tr.Find("textTglTitle").GetComponent<TextMeshProUGUI>();
			component.text = _settingName;
			component.color = base.TextColor;
			Transform sourceToggle = tr.Find("rb00");
			Buttons = (from x in _buttons.Select(delegate(string x, int i)
				{
					if (i == 0)
					{
						return sourceToggle;
					}
					Transform transform = UnityEngine.Object.Instantiate(sourceToggle, tr, worldPositionStays: false);
					transform.name = "rb0" + i;
					return transform;
				})
				select x.GetComponent<Toggle>()).ToList().AsReadOnly();
			int num = 280 / Mathf.Min(Buttons.Count, ColumnCount);
			int num2 = Mathf.CeilToInt((float)Buttons.Count / (float)ColumnCount);
			tr.GetComponent<LayoutElement>().minHeight = 40 * num2;
			int num3 = 0;
			for (int num4 = 0; num4 < num2; num4++)
			{
				for (int num5 = 0; num5 < ColumnCount; num5++)
				{
					if (num3 >= Buttons.Count)
					{
						break;
					}
					Toggle toggle = Buttons[num3];
					RectTransform component2 = toggle.GetComponent<RectTransform>();
					component2.offsetMin = new Vector2(num * num5 - 280, 8f);
					component2.offsetMax = new Vector2(num * (num5 + 1) - 280, -8f);
					component2.anchorMax = new Vector2(1f, 1f - (float)num4 / (float)num2);
					component2.anchorMin = new Vector2(1f, 1f - (float)(num4 + 1) / (float)num2);
					toggle.GetComponentInChildren<TextMeshProUGUI>().text = _buttons[num3];
					int indexCopy = num3;
					toggle.onValueChanged.AddListener(delegate(bool a)
					{
						if (a || indexCopy == base.Value)
						{
							SetValue(indexCopy);
						}
					});
					num3++;
				}
			}
			KoikatuAPI.Assert(num3 >= Buttons.Count, "Didn't loop over all radio buttons, only " + num3);
			base.BufferedValueChanged.Subscribe(delegate(int i)
			{
				for (int j = 0; j < Buttons.Count; j++)
				{
					Toggle toggle2 = Buttons[j];
					toggle2.isOn = j == i;
				}
			});
			LayoutElement component3 = tr.GetComponent<LayoutElement>();
			component3.flexibleWidth = 1f;
			return tr.gameObject;
		}

		private static void MakeCopy()
		{
			Transform existingControl = BaseGuiEntry.GetExistingControl("CustomScene/CustomRoot/FrontUIGroup/CustomUIGroup/CvsMenuTree/00_FaceTop/tglEye02", "rbEyeSettingType");
			_radioCopy = UnityEngine.Object.Instantiate(existingControl, BaseGuiEntry.GuiCacheTransfrom, worldPositionStays: false);
			_radioCopy.gameObject.SetActive(value: false);
			_radioCopy.name = "rbSide";
			UnityEngine.Object.DestroyImmediate(_radioCopy.GetComponent<ToggleGroup>());
			Toggle[] componentsInChildren = _radioCopy.GetComponentsInChildren<Toggle>();
			foreach (Toggle toggle in componentsInChildren)
			{
				toggle.onValueChanged.RemoveAllListeners();
				string text = "rb" + toggle.gameObject.name.Substring(8);
				if (text == "rb00")
				{
					toggle.image.raycastTarget = true;
					toggle.graphic.raycastTarget = true;
					toggle.gameObject.name = text;
				}
				else
				{
					UnityEngine.Object.Destroy(toggle.gameObject);
				}
			}
			BaseGuiEntry.RemoveLocalisation(_radioCopy.gameObject);
		}
	}
	/// <summary>
	/// Custom control that draws a simple horizontal separator
	/// </summary>
	public class MakerSeparator : BaseGuiEntry
	{
		private static Transform _sourceSeparator;

		private static Transform SourceSeparator
		{
			get
			{
				if (_sourceSeparator == null)
				{
					MakeCopy();
				}
				return _sourceSeparator;
			}
		}

		/// <inheritdoc />
		protected override GameObject OnCreateControl(Transform subCategoryList)
		{
			Transform transform = UnityEngine.Object.Instantiate(SourceSeparator, subCategoryList, worldPositionStays: false);
			transform.name = "Separate";
			return transform.gameObject;
		}

		/// <inheritdoc />
		protected internal override void Initialize()
		{
			if (_sourceSeparator == null)
			{
				MakeCopy();
			}
		}

		private static void MakeCopy()
		{
			_sourceSeparator = BaseGuiEntry.GetExistingControl("CustomScene/CustomRoot/FrontUIGroup/CustomUIGroup/CvsMenuTree/00_FaceTop/tglAll", "Separate").transform;
			LayoutElement component = _sourceSeparator.GetComponent<LayoutElement>();
			component.flexibleWidth = 1f;
		}

		/// <summary>
		/// Create a new custom control. Create and register it in <see cref="E:KKAPI.Maker.MakerAPI.RegisterCustomSubCategories" />.
		/// </summary>
		/// <param name="category">Category the control will be created under</param>
		/// <param name="owner">Plugin that owns the control</param>
		public MakerSeparator(MakerCategory category, BaseUnityPlugin owner)
			: base(category, owner)
		{
		}
	}
	/// <summary>
	/// Custom control that draws a slider and a text box (both are used to edit the same value)
	/// </summary>
	public class MakerSlider : BaseEditableGuiEntry<float>
	{
		private static Transform _sliderCopy;

		private readonly string _settingName;

		private readonly float _maxValue;

		private readonly float _minValue;

		/// <summary>
		/// Value used when user presses the Reset button.
		/// </summary>
		public float DefaultValue { get; set; }

		/// <summary>
		/// Custom converter from text in the textbox to the slider value.
		/// If not set, <code>float.Parse(txt) / 100f</code> is used.
		/// You may need to also change <see cref="P:KKAPI.Maker.UI.MakerSlider.ValueToString" /> and <see cref="P:KKAPI.Maker.UI.MakerSlider.MouseScrollValueChange" /> if you set this.
		/// </summary>
		public Func<string, float> StringToValue { get; set; }

		/// <summary>
		/// Custom converter from the slider value to what's displayed in the textbox.
		/// If not set, <code>Mathf.RoundToInt(f * 100).ToString()</code> is used.
		/// You may need to also change <see cref="P:KKAPI.Maker.UI.MakerSlider.StringToValue" /> and <see cref="P:KKAPI.Maker.UI.MakerSlider.MouseScrollValueChange" /> if you set this.
		/// </summary>
		public Func<float, string> ValueToString { get; set; }

		/// <summary>
		/// Override for the default mouse scroll behaviour. It should return a value that should be added to the slider value (will be clamped, can be negative).
		/// The Vector2 parameter is the mouse scroll delta. Most mice only use the Y scroll value. Delta can be negative and larger than 1.
		/// If not set, the slider value will be changed based on the slider range (1% of log10 of the max value per vertical mouse wheel tick).
		/// </summary>
		public Func<Vector2, float> MouseScrollValueChange { get; set; }

		/// <summary>
		/// Use integers instead of floats
		/// </summary>
		public bool WholeNumbers { get; set; }

		private static Transform SliderCopy
		{
			get
			{
				if (_sliderCopy == null)
				{
					MakeCopy();
				}
				return _sliderCopy;
			}
		}

		/// <summary>
		/// Create a new custom control. Create and register it in <see cref="E:KKAPI.Maker.MakerAPI.RegisterCustomSubCategories" />.
		/// </summary>
		/// <param name="settingName">Text displayed next to the slider</param>
		/// <param name="category">Category the control will be created under</param>
		/// <param name="owner">Plugin that owns the control</param>
		/// <param name="minValue">Lowest allowed value (inclusive)</param>
		/// <param name="maxValue">Highest allowed value (inclusive)</param>
		/// <param name="defaultValue">Value the slider will be set to after creation</param>
		public MakerSlider(MakerCategory category, string settingName, float minValue, float maxValue, float defaultValue, BaseUnityPlugin owner)
			: base(category, defaultValue, owner)
		{
			_settingName = settingName;
			_minValue = minValue;
			_maxValue = maxValue;
			DefaultValue = defaultValue;
		}

		private static void MakeCopy()
		{
			Transform existingControl = BaseGuiEntry.GetExistingControl("CustomScene/CustomRoot/FrontUIGroup/CustomUIGroup/CvsMenuTree/00_FaceTop/tglAll", "sldTemp");
			_sliderCopy = UnityEngine.Object.Instantiate(existingControl, BaseGuiEntry.GuiCacheTransfrom, worldPositionStays: false);
			_sliderCopy.gameObject.SetActive(value: false);
			_sliderCopy.name = "sldTemp";
			Slider component = _sliderCopy.Find("Slider").GetComponent<Slider>();
			component.onValueChanged.RemoveAllListeners();
			TMP_InputField component2 = _sliderCopy.Find("InputField").GetComponent<TMP_InputField>();
			component2.onValueChanged.RemoveAllListeners();
			component2.onSubmit.RemoveAllListeners();
			component2.onEndEdit.RemoveAllListeners();
			Button component3 = _sliderCopy.Find("btnReset").GetComponent<Button>();
			component3.onClick.RemoveAllListeners();
			Image[] componentsInChildren = _sliderCopy.GetComponentsInChildren<Image>();
			foreach (Image image in componentsInChildren)
			{
				image.raycastTarget = true;
			}
			BaseGuiEntry.RemoveLocalisation(_sliderCopy.gameObject);
		}

		/// <inheritdoc />
		protected internal override void Initialize()
		{
			if (_sliderCopy == null)
			{
				MakeCopy();
			}
		}

		/// <inheritdoc />
		protected override GameObject OnCreateControl(Transform subCategoryList)
		{
			Transform transform = UnityEngine.Object.Instantiate(SliderCopy, subCategoryList, worldPositionStays: false);
			transform.name = "sldTemp";
			TextMeshProUGUI component = transform.Find("textShape").GetComponent<TextMeshProUGUI>();
			component.text = _settingName;
			component.color = base.TextColor;
			Slider slider = transform.Find("Slider").GetComponent<Slider>();
			slider.minValue = _minValue;
			slider.maxValue = _maxValue;
			slider.wholeNumbers = WholeNumbers;
			slider.onValueChanged.AddListener(base.SetValue);
			slider.value = DefaultValue;
			slider.GetComponent<ObservableScrollTrigger>().OnScrollAsObservable().Subscribe(delegate(PointerEventData data)
			{
				if (MouseScrollValueChange != null)
				{
					float num = MouseScrollValueChange(data.scrollDelta);
					slider.value += num;
				}
				else
				{
					float num2 = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(Mathf.Max(Mathf.Abs(slider.minValue), Mathf.Abs(slider.maxValue)) / 100f)));
					float y = data.scrollDelta.y;
					if (y < 0f)
					{
						slider.value += num2;
					}
					else if (y > 0f)
					{
						slider.value -= num2;
					}
				}
			});
			TMP_InputField inputField = transform.Find("InputField").GetComponent<TMP_InputField>();
			if (MakerAPI.InsideMaker)
			{
				Singleton<CustomBase>.Instance.lstTmpInputField.Add(inputField);
			}
			InputField(DefaultValue, inputField);
			inputField.onEndEdit.AddListener(delegate(string txt)
			{
				try
				{
					float value = StringToValue?.Invoke(txt) ?? (float.Parse(txt) / 100f);
					slider.value = Mathf.Clamp(value, slider.minValue, slider.maxValue);
				}
				catch
				{
				}
			});
			slider.onValueChanged.AddListener(delegate(float f)
			{
				InputField(f, inputField);
			});
			Button component2 = transform.Find("btnReset").GetComponent<Button>();
			component2.onClick.AddListener(delegate
			{
				slider.value = DefaultValue;
			});
			base.BufferedValueChanged.Subscribe(delegate(float f)
			{
				slider.value = f;
			});
			LayoutElement component3 = transform.GetComponent<LayoutElement>();
			component3.flexibleWidth = 1f;
			return transform.gameObject;
		}

		private void InputField(float f, TMP_InputField inputField)
		{
			if (ValueToString != null)
			{
				inputField.text = ValueToString(f);
			}
			else if (WholeNumbers)
			{
				inputField.text = f.ToString();
			}
			else
			{
				inputField.text = Mathf.RoundToInt(f * 100f).ToString();
			}
		}
	}
	/// <summary>
	/// Custom control that displays a simple text
	/// </summary>
	public class MakerText : BaseGuiEntry
	{
		private static Transform _textCopy;

		private string _text;

		private TextMeshProUGUI _instance;

		/// <summary>
		/// Light gray color best used for text explaining another setting
		/// </summary>
		public static Color ExplanationGray => new Color(0.7f, 0.7f, 0.7f);

		/// <summary>
		/// Displayed text
		/// </summary>
		public string Text
		{
			get
			{
				return _text;
			}
			set
			{
				_text = value;
				if (_instance != null)
				{
					_instance.text = value;
				}
			}
		}

		private static Transform TextCopy
		{
			get
			{
				if (_textCopy == null)
				{
					MakeCopy();
				}
				return _textCopy;
			}
		}

		/// <summary>
		/// Create a new custom control. Create and register it in <see cref="E:KKAPI.Maker.MakerAPI.RegisterCustomSubCategories" />.
		/// </summary>
		/// <param name="text">Displayed text</param>
		/// <param name="category">Category the control will be created under</param>
		/// <param name="owner">Plugin that owns the control</param>
		public MakerText(string text, MakerCategory category, BaseUnityPlugin owner)
			: base(category, owner)
		{
			Text = text;
		}

		private static void MakeCopy()
		{
			Transform existingControl = BaseGuiEntry.GetExistingControl("CustomScene/CustomRoot/FrontUIGroup/CustomUIGroup/CvsMenuTree/06_SystemTop/tglVisualSettings", "txtExplanation");
			_textCopy = UnityEngine.Object.Instantiate(existingControl, BaseGuiEntry.GuiCacheTransfrom, worldPositionStays: false);
			_textCopy.gameObject.SetActive(value: false);
			_textCopy.name = "txtCustom";
			BaseGuiEntry.RemoveLocalisation(_textCopy.gameObject);
		}

		/// <inheritdoc />
		protected internal override void Initialize()
		{
			if (_textCopy == null)
			{
				MakeCopy();
			}
		}

		/// <inheritdoc />
		protected override GameObject OnCreateControl(Transform subCategoryList)
		{
			Transform transform = UnityEngine.Object.Instantiate(TextCopy, subCategoryList, worldPositionStays: false);
			_instance = transform.GetComponentInChildren<TextMeshProUGUI>();
			_instance.text = Text;
			_instance.color = base.TextColor;
			LayoutElement component = transform.GetComponent<LayoutElement>();
			component.flexibleWidth = 1f;
			return transform.gameObject;
		}
	}
	/// <summary>
	/// Custom control that draws a single-line text box.
	/// </summary>
	public class MakerTextbox : BaseEditableGuiEntry<string>
	{
		private static Transform _textboxCopy;

		private readonly string _settingName;

		/// <summary>
		/// Value used when user presses the Reset button.
		/// </summary>
		public string DefaultValue { get; set; }

		/// <summary>
		/// Type of content filtering to do on the input.
		/// </summary>
		public InputField.ContentType ContentType { get; set; }

		/// <summary>
		/// Maximum number of characters, about 22 are visible at once.
		/// </summary>
		public int CharacterLimit { get; set; } = 200;

		private static Transform TextboxCopy
		{
			get
			{
				if (_textboxCopy == null)
				{
					MakeCopy();
				}
				return _textboxCopy;
			}
		}

		/// <summary>
		/// Create a new custom control. Create and register it in <see cref="E:KKAPI.Maker.MakerAPI.RegisterCustomSubCategories" />.
		/// </summary>
		/// <param name="settingName">Text displayed next to the slider</param>
		/// <param name="category">Category the control will be created under</param>
		/// <param name="owner">Plugin that owns the control</param>
		/// <param name="defaultValue">Value the textbox will be set to after creation</param>
		public MakerTextbox(MakerCategory category, string settingName, string defaultValue, BaseUnityPlugin owner)
			: base(category, defaultValue, owner)
		{
			_settingName = settingName;
			DefaultValue = defaultValue;
		}

		private static void MakeCopy()
		{
			Transform existingControl = BaseGuiEntry.GetExistingControl("CustomScene/CustomRoot/FrontUIGroup/CustomUIGroup/CvsMenuTree/00_FaceTop/tglAll", "sldTemp");
			_textboxCopy = UnityEngine.Object.Instantiate(existingControl, BaseGuiEntry.GuiCacheTransfrom, worldPositionStays: false);
			_textboxCopy.gameObject.SetActive(value: false);
			_textboxCopy.name = "textboxTemp";
			GameObject gameObject = _textboxCopy.Find("Slider").gameObject;
			UnityEngine.Object.Destroy(gameObject);
			Transform transform = _textboxCopy.Find("InputField");
			RectTransform component = transform.GetComponent<RectTransform>();
			component.offsetMin = new Vector2(-275f, 6f);
			TMP_InputField component2 = transform.GetComponent<TMP_InputField>();
			component2.onValueChanged.ActuallyRemoveAllListeners();
			component2.onSubmit.ActuallyRemoveAllListeners();
			component2.onEndEdit.ActuallyRemoveAllListeners();
			Button component3 = _textboxCopy.Find("btnReset").GetComponent<Button>();
			component3.onClick.ActuallyRemoveAllListeners();
			Image[] componentsInChildren = _textboxCopy.GetComponentsInChildren<Image>();
			foreach (Image image in componentsInChildren)
			{
				image.raycastTarget = true;
			}
			BaseGuiEntry.RemoveLocalisation(_textboxCopy.gameObject);
		}

		/// <inheritdoc />
		protected internal override void Initialize()
		{
			if (_textboxCopy == null)
			{
				MakeCopy();
			}
		}

		/// <inheritdoc />
		protected override GameObject OnCreateControl(Transform subCategoryList)
		{
			Transform transform = UnityEngine.Object.Instantiate(TextboxCopy, subCategoryList, worldPositionStays: false);
			transform.name = "sldTextbox";
			TextMeshProUGUI component = transform.Find("textShape").GetComponent<TextMeshProUGUI>();
			component.text = _settingName;
			component.color = base.TextColor;
			TMP_InputField inputField = transform.Find("InputField").GetComponent<TMP_InputField>();
			inputField.contentType = (TMP_InputField.ContentType)ContentType;
			inputField.characterLimit = CharacterLimit;
			switch (ContentType)
			{
			case InputField.ContentType.Autocorrected:
				inputField.inputType = TMP_InputField.InputType.AutoCorrect;
				break;
			case InputField.ContentType.Password:
				inputField.inputType = TMP_InputField.InputType.Password;
				break;
			default:
				inputField.inputType = TMP_InputField.InputType.Standard;
				break;
			}
			inputField.onEndEdit.AddListener(base.SetValue);
			if (MakerAPI.InsideMaker)
			{
				Singleton<CustomBase>.Instance.lstTmpInputField.Add(inputField);
			}
			Button component2 = transform.Find("btnReset").GetComponent<Button>();
			component2.onClick.AddListener(delegate
			{
				SetValue(DefaultValue);
			});
			base.BufferedValueChanged.Subscribe(delegate(string text)
			{
				inputField.text = text;
			});
			LayoutElement component3 = transform.GetComponent<LayoutElement>();
			component3.flexibleWidth = 1f;
			return transform.gameObject;
		}
	}
	/// <summary>
	/// Custom control that displays a toggle
	/// </summary>
	public class MakerToggle : BaseEditableGuiEntry<bool>
	{
		private static Transform _toggleCopy;

		/// <summary>
		/// Text shown next to the checkbox
		/// </summary>
		public string DisplayName { get; }

		private static Transform ToggleCopy
		{
			get
			{
				if (_toggleCopy == null)
				{
					MakeCopy();
				}
				return _toggleCopy;
			}
		}

		/// <summary>
		/// Create a new custom control. Create and register it in <see cref="E:KKAPI.Maker.MakerAPI.RegisterCustomSubCategories" />.
		/// </summary>
		/// <param name="displayName">Text shown next to the checkbox</param>
		/// <param name="category">Category the control will be created under</param>
		/// <param name="owner">Plugin that owns the control</param>
		public MakerToggle(MakerCategory category, string displayName, BaseUnityPlugin owner)
			: this(category, displayName, initialValue: false, owner)
		{
		}

		/// <summary>
		/// Create a new custom control. Create and register it in <see cref="E:KKAPI.Maker.MakerAPI.RegisterCustomSubCategories" />.
		/// </summary>
		/// <param name="displayName">Text shown next to the checkbox</param>
		/// <param name="category">Category the control will be created under</param>
		/// <param name="initialValue">Initial value of the toggle</param>
		/// <param name="owner">Plugin that owns the control</param>
		public MakerToggle(MakerCategory category, string displayName, bool initialValue, BaseUnityPlugin owner)
			: base(category, initialValue, owner)
		{
			DisplayName = displayName;
		}

		private static void MakeCopy()
		{
			Transform existingControl = BaseGuiEntry.GetExistingControl("CustomScene/CustomRoot/FrontUIGroup/CustomUIGroup/CvsMenuTree/00_FaceTop/tglMouth", "tglCanine");
			Transform transform = UnityEngine.Object.Instantiate(existingControl, BaseGuiEntry.GuiCacheTransfrom, worldPositionStays: false);
			transform.gameObject.SetActive(value: false);
			transform.name = "tglSingle";
			Toggle componentInChildren = transform.GetComponentInChildren<Toggle>();
			componentInChildren.onValueChanged.RemoveAllListeners();
			componentInChildren.image.raycastTarget = true;
			componentInChildren.graphic.raycastTarget = true;
			componentInChildren.GetComponent<RectTransform>().offsetMax = new Vector2(460f, -8f);
			_toggleCopy = transform;
			BaseGuiEntry.RemoveLocalisation(_toggleCopy.gameObject);
		}

		/// <inheritdoc />
		protected internal override void Initialize()
		{
			if (_toggleCopy == null)
			{
				MakeCopy();
			}
		}

		/// <inheritdoc />
		protected override GameObject OnCreateControl(Transform subCategoryList)
		{
			Transform transform = UnityEngine.Object.Instantiate(ToggleCopy, subCategoryList, worldPositionStays: false);
			Toggle tgl = transform.GetComponentInChildren<Toggle>();
			tgl.onValueChanged.AddListener(base.SetValue);
			base.BufferedValueChanged.Subscribe(delegate(bool b)
			{
				tgl.isOn = b;
			});
			TextMeshProUGUI componentInChildren = transform.GetComponentInChildren<TextMeshProUGUI>();
			componentInChildren.text = DisplayName;
			componentInChildren.color = base.TextColor;
			LayoutElement component = transform.GetComponent<LayoutElement>();
			component.flexibleWidth = 1f;
			return transform.gameObject;
		}
	}
	internal static class SubCategoryCreator
	{
		private static Transform _subCategoryCopy;

		private static Transform _scrollbarCopy;

		private static Transform SubCategoryCopy
		{
			get
			{
				if (_subCategoryCopy == null)
				{
					Transform original = GameObject.Find("00_FaceTop").transform.Find("tglEar");
					_subCategoryCopy = UnityEngine.Object.Instantiate(original, BaseGuiEntry.GuiCacheTransfrom, worldPositionStays: true);
					_subCategoryCopy.name = "CustomSubcategory" + BaseGuiEntry.GuiApiNameAppendix;
					_subCategoryCopy.gameObject.SetActive(value: false);
					BaseGuiEntry.RemoveLocalisation(_subCategoryCopy.gameObject);
					Toggle component = _subCategoryCopy.GetComponent<Toggle>();
					component.onValueChanged.RemoveAllListeners();
					component.isOn = false;
					Transform transform = _subCategoryCopy.Find("EarTop");
					transform.name = "CustomSubcategoryTop";
					UnityEngine.Object.DestroyImmediate(transform.GetComponent<CvsEar>());
					UnityEngine.Object.DestroyImmediate(transform.GetComponent<Image>());
					UnityEngine.Object.DestroyImmediate(transform.GetComponent<ContentSizeFitter>());
					UnityEngine.Object.DestroyImmediate(transform.GetComponent<VerticalLayoutGroup>());
					UnityEngine.Object.DestroyImmediate(transform.GetComponent<CanvasRenderer>());
					foreach (Transform item in transform)
					{
						UnityEngine.Object.Destroy(item.gameObject);
					}
					Image[] componentsInChildren = _subCategoryCopy.GetComponentsInChildren<Image>();
					foreach (Image image in componentsInChildren)
					{
						image.raycastTarget = true;
					}
					GameObject gameObject = GameObject.Find("CustomScene/CustomRoot/FrontUIGroup/CustomUIGroup/CvsMenuTree/03_ClothesTop/tglSocks/SocksTop/Scroll View");
					_scrollbarCopy = UnityEngine.Object.Instantiate(gameObject.transform, BaseGuiEntry.GuiCacheTransfrom, worldPositionStays: true);
					_scrollbarCopy.gameObject.SetActive(value: false);
					foreach (Transform item2 in _scrollbarCopy.Find("Viewport/Content"))
					{
						UnityEngine.Object.Destroy(item2.gameObject);
					}
					UI_RaycastCtrl[] componentsInChildren2 = _subCategoryCopy.GetComponentsInChildren<UI_RaycastCtrl>(includeInactive: true);
					foreach (UI_RaycastCtrl uI_RaycastCtrl in componentsInChildren2)
					{
						uI_RaycastCtrl.canvasGrp = null;
						uI_RaycastCtrl.imgRaycastTargetOn = null;
					}
				}
				return _subCategoryCopy;
			}
		}

		private static string GetSubcategoryDisplayName(MakerCategory subCategory)
		{
			if (subCategory.DisplayName != null)
			{
				return subCategory.DisplayName;
			}
			if (!subCategory.SubCategoryName.StartsWith("tgl"))
			{
				return subCategory.SubCategoryName;
			}
			return subCategory.SubCategoryName.Substring(3);
		}

		public static Transform AddNewSubCategory(UI_ToggleGroupCtrl mainCategory, MakerCategory subCategory)
		{
			KoikatuAPI.Logger.LogDebug((object)("Adding custom subcategory " + subCategory.SubCategoryName + " to " + mainCategory.transform.name));
			Transform transform = UnityEngine.Object.Instantiate(SubCategoryCopy.gameObject, mainCategory.transform, worldPositionStays: true).transform;
			transform.name = subCategory.SubCategoryName;
			Transform transform2 = transform.Find("CustomSubcategoryTop");
			transform2.name = subCategory.SubCategoryName + "Top";
			foreach (Transform item in transform2.transform)
			{
				UnityEngine.Object.Destroy(item.gameObject);
			}
			transform.GetComponentInChildren<TextMeshProUGUI>().text = GetSubcategoryDisplayName(subCategory);
			Toggle component = transform.GetComponent<Toggle>();
			component.group = mainCategory.transform.GetComponent<ToggleGroup>();
			CanvasGroup component2 = transform2.GetComponent<CanvasGroup>();
			mainCategory.items = CollectionExtensions.AddItem<UI_ToggleGroupCtrl.ItemInfo>(mainCategory.items.Where((UI_ToggleGroupCtrl.ItemInfo x) => x.tglItem != null), new UI_ToggleGroupCtrl.ItemInfo
			{
				tglItem = component,
				cgItem = component2
			}).ToArray();
			((MonoBehaviour)(object)KoikatuAPI.Instance).StartCoroutine(FinishInit(transform2));
			transform.gameObject.SetActive(value: true);
			return transform;
		}

		private static IEnumerator FinishInit(Transform trTop)
		{
			yield return null;
			Transform transform = UnityEngine.Object.Instantiate(_scrollbarCopy, trTop, worldPositionStays: true);
			transform.name = "Scroll View";
			RectTransform component = transform.GetComponent<RectTransform>();
			component.offsetMax = new Vector2(472f, 0f);
			component.offsetMin = new Vector2(0f, -952f);
			transform.Find("Viewport/Content").GetComponent<Image>().raycastTarget = true;
			transform.Find("Scrollbar Vertical").GetComponent<Image>().raycastTarget = true;
			transform.gameObject.SetActive(value: true);
			yield return new WaitUntil(() => MakerAPI.InsideAndLoaded);
			trTop.GetComponentInChildren<UI_RaycastCtrl>(includeInactive: true)?.Reset();
			trTop.gameObject.SetActiveIfDifferent(active: true);
		}
	}
}
namespace KKAPI.Maker.UI.Sidebar
{
	/// <summary>
	/// Marks the control as being intended for use on Control Panel sidebar in chara maker
	/// </summary>
	public interface ISidebarControl
	{
	}
	/// <summary>
	/// A separator to be used in the right "Control Panel" sidebar in character maker.
	/// The space is limited so use sparingly.
	/// </summary>
	public class SidebarSeparator : BaseGuiEntry, ISidebarControl
	{
		/// <inheritdoc />
		public SidebarSeparator(BaseUnityPlugin owner)
			: base(null, owner)
		{
		}

		/// <inheritdoc />
		protected internal override void Initialize()
		{
		}

		/// <inheritdoc />
		protected override GameObject OnCreateControl(Transform subCategoryList)
		{
			Transform existingControl = BaseGuiEntry.GetExistingControl("CustomScene/CustomRoot/FrontUIGroup/CvsDraw/Top", "Separate");
			return UnityEngine.Object.Instantiate(existingControl.gameObject, existingControl.transform.parent);
		}
	}
	/// <summary>
	/// A toggle to be used in the right "Control Panel" sidebar in character maker.
	/// The space is limited so use sparingly.
	/// </summary>
	public class SidebarToggle : BaseEditableGuiEntry<bool>, ISidebarControl
	{
		/// <summary>
		/// Text displayed next to the checkbox
		/// </summary>
		public string Text { get; }

		/// <inheritdoc />
		public SidebarToggle(string text, bool initialValue, BaseUnityPlugin owner)
			: base((MakerCategory)null, initialValue, owner)
		{
			Text = text;
		}

		/// <inheritdoc />
		protected internal override void Initialize()
		{
		}

		/// <inheritdoc />
		protected override GameObject OnCreateControl(Transform subCategoryList)
		{
			Transform existingControl = BaseGuiEntry.GetExistingControl("CustomScene/CustomRoot/FrontUIGroup/CvsDraw/Top", "tglBlink");
			GameObject gameObject = UnityEngine.Object.Instantiate(existingControl.gameObject, existingControl.transform.parent);
			BaseGuiEntry.RemoveLocalisation(gameObject);
			Toggle tgl = gameObject.GetComponentInChildren<Toggle>();
			tgl.graphic.raycastTarget = true;
			tgl.onValueChanged.RemoveAllListeners();
			tgl.onValueChanged.AddListener(base.SetValue);
			base.BufferedValueChanged.Subscribe(delegate(bool val)
			{
				tgl.isOn = val;
			});
			TextMeshProUGUI componentInChildren = gameObject.GetComponentInChildren<TextMeshProUGUI>();
			componentInChildren.text = Text;
			componentInChildren.color = base.TextColor;
			componentInChildren.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 145f);
			return gameObject;
		}
	}
}
namespace KKAPI.MainGame
{
	internal static class CustomActionIcon
	{
		private sealed class ActionIconEntry
		{
			public readonly Texture Icon;

			public readonly Color Color;

			public readonly string PopupText;

			public readonly int MapNo;

			public readonly Vector3 Position;

			public readonly Action OnOpen;

			public readonly Action<TriggerEnterExitEvent> OnCreated;

			public UnityEngine.Object Instance;

			public ActionIconEntry(int mapNo, Vector3 position, Texture icon, Color color, string popupText, Action onOpen, Action<TriggerEnterExitEvent> onCreated)
			{
				OnOpen = onOpen;
				OnCreated = onCreated;
				Icon = icon;
				Color = color;
				PopupText = popupText;
				MapNo = mapNo;
				Position = position;
			}

			public void Dispose()
			{
				UnityEngine.Object.Destroy(Instance);
			}
		}

		private static readonly List<ActionIconEntry> _entries = new List<ActionIconEntry>();

		public static IDisposable AddActionIcon(int mapNo, Vector3 position, Texture icon, Color color, string popupText, Action onOpen, Action<TriggerEnterExitEvent> onCreated = null, bool delayed = true, bool immediate = false)
		{
			if (icon == null)
			{
				throw new ArgumentNullException("icon");
			}
			if (popupText == null)
			{
				throw new ArgumentNullException("popupText");
			}
			if (onOpen == null)
			{
				throw new ArgumentNullException("onOpen");
			}
			UnityEngine.Object.DontDestroyOnLoad(icon);
			ActionIconEntry entry = new ActionIconEntry(mapNo, position, icon, color, popupText, onOpen, onCreated);
			if (delayed)
			{
				_entries.Add(entry);
			}
			if (immediate && SingletonInitializer<ActionScene>.initialized)
			{
				ActionGame.Chara.Player player = SingletonInitializer<ActionScene>.instance.Player;
				if ((object)player != null && player.mapNo == mapNo)
				{
					SpawnActionPoint(entry, 100);
				}
			}
			return Disposable.Create(delegate
			{
				_entries.Remove(entry);
				entry.Dispose();
			});
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(ActionMap), "Reserve")]
		private static void OnMapChangedHook(ActionMap __instance)
		{
			if (__instance.mapRoot == null || __instance.isMapLoading)
			{
				return;
			}
			int num = 0;
			foreach (ActionIconEntry entry in _entries)
			{
				if (entry.MapNo == __instance.no && entry.Instance == null)
				{
					try
					{
						SpawnActionPoint(entry, num);
						num++;
					}
					catch (Exception arg)
					{
						KoikatuAPI.Logger.LogError((object)$"Failed to created custom action point on map no {__instance.no} at {entry.Position}\n{arg}");
					}
				}
			}
			if (num > 0)
			{
				KoikatuAPI.Logger.LogDebug((object)$"Created {num} custom action points on map no {__instance.no}");
			}
		}

		private static void SpawnActionPoint(ActionIconEntry iconEntry, int created)
		{
			GameObject gameObject = CommonLib.LoadAsset<GameObject>("map/playeractionpoint/00.unity3d", "PlayerActionPoint_05", clone: true);
			gameObject.gameObject.name = "CustomActionPoint_" + created;
			GameObject gameObject2 = GameObject.Find("Map/ActionPoints");
			gameObject.transform.SetParent(gameObject2.transform, worldPositionStays: true);
			PlayerActionPoint componentInChildren = gameObject.GetComponentInChildren<PlayerActionPoint>();
			GameObject gameObject3 = componentInChildren.gameObject;
			componentInChildren.gameObject.name = gameObject.name;
			Transform transform = componentInChildren.transform;
			Renderer renderer = componentInChildren.renderers.Reverse().First(delegate(Renderer x)
			{
				Texture mainTexture = x.material.mainTexture;
				return mainTexture.width == 256 && mainTexture.height == 256;
			});
			Animator animator = componentInChildren.animator;
			Renderer[] renderers = componentInChildren.renderers;
			ParticleSystem[] particles = componentInChildren.particleSystems;
			KoikatuAPI.Assert(particles.Length == 3, "SpawnActionPoint particles.Length == 3 failed");
			componentInChildren.gameObject.layer = LayerMask.NameToLayer("Action/ActionPoint");
			foreach (Transform item in componentInChildren.transform.parent)
			{
				if (item != componentInChildren.transform)
				{
					UnityEngine.Object.Destroy(item.gameObject);
				}
			}
			UnityEngine.Object.DestroyImmediate(componentInChildren, allowDestroyingAssets: false);
			transform.position = iconEntry.Position;
			Color color = iconEntry.Color;
			Renderer[] array = renderers;
			foreach (Renderer renderer2 in array)
			{
				renderer2.material.color = color;
			}
			ParticleSystem[] array2 = particles;
			foreach (ParticleSystem particleSystem in array2)
			{
				particleSystem.startColor = color;
			}
			particles[0].Play();
			particles[1].Stop();
			particles[2].Stop();
			TriggerEnterExitEvent evt = gameObject3.AddComponent<TriggerEnterExitEvent>();
			renderer.material.mainTexture = iconEntry.Icon;
			bool playerInRange = false;
			evt.onTriggerEnter += delegate(Collider c)
			{
				if (c.CompareTag("Player"))
				{
					playerInRange = true;
					animator.Play(PAP.Assist.Animation.SpinState);
					Utils.Sound.Play(Manager.Sound.Type.GameSE2D, Utils.Sound.SEClipTable[56]);
					c.GetComponent<ActionGame.Chara.Player>().actionPointList.Add(evt);
					SingletonInitializer<ActionScene>.instance.actionChangeUI.Set(ActionChangeUI.ActionType.Shop);
					SingletonInitializer<ActionScene>.instance.actionChangeUI._text.text = iconEntry.PopupText;
					particles[0].Stop();
					particles[1].Play();
					particles[2].Play();
				}
			};
			evt.onTriggerExit += delegate(Collider c)
			{
				if (c.CompareTag("Player"))
				{
					playerInRange = false;
					animator.Play(PAP.Assist.Animation.IdleState);
					c.GetComponent<ActionGame.Chara.Player>().actionPointList.Remove(evt);
					SingletonInitializer<ActionScene>.instance.actionChangeUI.Remove(ActionChangeUI.ActionType.Shop);
					particles[0].Play();
					particles[1].Stop();
					particles[2].Stop();
				}
			};
			evt.UpdateAsObservable().Subscribe(delegate
			{
				bool flag = !Game.IsRegulate(isAddSceneCheck: true) && SingletonInitializer<ActionScene>.initialized;
				if (flag)
				{
					ActionScene instance = SingletonInitializer<ActionScene>.instance;
					flag = instance.regulate == (ActionScene.Regulate)0 && (instance.AdvScene == null || !instance.AdvScene.gameObject.activeSelf) && !TalkScene.isPaly;
				}
				if (renderers[0].enabled != flag)
				{
					Renderer[] array3 = renderers;
					foreach (Renderer renderer3 in array3)
					{
						renderer3.enabled = flag;
					}
					if (!flag)
					{
						particles[0].Stop();
						particles[1].Stop();
						particles[2].Stop();
					}
					else
					{
						particles[0].Play();
						particles[1].Stop();
						particles[2].Stop();
					}
				}
				if (flag && playerInRange && ActionInput.isAction && !SingletonInitializer<ActionScene>.instance.Player.isActionNow)
				{
					iconEntry.OnOpen();
				}
			}).AddTo(evt);
			iconEntry.Instance = gameObject;
			iconEntry.OnCreated?.Invoke(evt);
		}
	}
	/// <summary>
	/// Used to add custom buttons to the H Scene "trespassing" menu (red button below position selections on the right, originally used when having H near another girl so you can start 3P).
	/// Warning: Works both in main game and FreeH by default! If you don't want your button to appear in FreeH then check for <code>!hSprite.flags.isFreeH</code> in your spawnConditionCheck.
	/// </summary>
	public static class CustomTrespassingHsceneButtons
	{
		private static class Hooks
		{
			private static bool? _defaultBtnShown;

			private static bool _applied;

			public static void ApplyHooks()
			{
				if (!_applied)
				{
					Harmony.CreateAndPatchAll(typeof(Hooks), typeof(Hooks).FullName);
					_applied = true;
				}
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(HSprite), "InitTrespassingHelp")]
			private static void InitTrespassingHelpPost(HSprite __instance)
			{
				bool activeSelf = __instance.btnTrespassing.gameObject.activeSelf;
				int num = SpawnButtons(__instance, activeSelf ? 1 : 0);
				if (num > 0)
				{
					__instance.btnTrespassing.gameObject.SetActiveIfDifferent(active: true);
					_defaultBtnShown = activeSelf;
				}
				else
				{
					_defaultBtnShown = null;
				}
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(HSprite), "InitPointMenuAndHelp")]
			private static void InitPointMenuAndHelpPost(HSprite __instance)
			{
				if (_defaultBtnShown == false)
				{
					__instance.autoDisableTrespassingHelp.ForceFlag();
					__instance.autoDisableTrespassingHelp.gameObject.SetActiveIfDifferent(active: false);
					__instance.menuActionSub.SetActive(_active: false, 7);
				}
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(HSprite), "OnActionMenu")]
			private static void InitPointMenuAndHelpPost(HSprite __instance, int _kind)
			{
				if (!_defaultBtnShown.HasValue)
				{
					return;
				}
				if (_kind == 3)
				{
					bool flag;
					if (_defaultBtnShown == false)
					{
						__instance.menuActionSub.SetActive(_active: false, 7);
						flag = !_customTrespassingButtons.Any((CustomTrespassingButton x) => x.Instance != null && x.Instance.activeSelf);
					}
					else
					{
						flag = !__instance.autoDisableTrespassingHelp.gameObject.activeSelf;
					}
					if (flag)
					{
						foreach (CustomTrespassingButton customTrespassingButton in _customTrespassingButtons)
						{
							if (customTrespassingButton.Instance != null)
							{
								customTrespassingButton.Instance.SetActiveIfDifferent(active: true);
							}
						}
						return;
					}
				}
				foreach (CustomTrespassingButton customTrespassingButton2 in _customTrespassingButtons)
				{
					if (customTrespassingButton2.Instance != null)
					{
						customTrespassingButton2.Instance.SetActiveIfDifferent(active: false);
					}
				}
			}
		}

		private sealed class CustomTrespassingButton : IDisposable
		{
			public readonly string ButtonText;

			public readonly Action<HSprite> OnButtonClicked;

			public readonly Func<HSprite, bool> SpawnConditionCheck;

			public GameObject Instance;

			public CustomTrespassingButton(string buttonText, Func<HSprite, bool> spawnConditionCheck, Action<HSprite> onButtonClicked)
			{
				if (buttonText == null)
				{
					throw new ArgumentNullException("buttonText");
				}
				if (onButtonClicked == null)
				{
					throw new ArgumentNullException("onButtonClicked");
				}
				ButtonText = buttonText;
				SpawnConditionCheck = spawnConditionCheck;
				OnButtonClicked = onButtonClicked;
			}

			public void Dispose()
			{
				if (Instance != null)
				{
					HSprite hSprite = UnityEngine.Object.FindObjectOfType<HSprite>();
					if (hSprite != null)
					{
						hSprite.menuActionSub.lstObj.Remove(Instance);
						hSprite.menuActionSub.makeParent.Remove(Instance);
					}
					UnityEngine.Object.Destroy(Instance);
				}
				Instance = null;
			}
		}

		private static readonly List<CustomTrespassingButton> _customTrespassingButtons = new List<CustomTrespassingButton>();

		/// <summary>
		/// Add a new button to the H Scene "trespassing" menu (red button below position selections on the right, originally used when having H near another girl so you can start 3P).
		/// After user clicks the button, a confirmation dialog will be shown.
		/// </summary>
		/// <inheritdoc cref="M:KKAPI.MainGame.CustomTrespassingHsceneButtons.AddHsceneTrespassingButton(System.String,System.Func{HSprite,System.Boolean},System.Action{HSprite})" />
		/// <param name="confirmBoxTitle">Title of the confirmation box after user clicked the button.</param>
		/// <param name="confirmBoxSentence">Explanation text of the confirmation box after user clicked the button. Can have newlines but try to keep it short.</param>
		/// <param name="onConfirmed">Called when user clicks the button and presses "Yes" in the confirmation box. If the user presses "No" nothing is done.</param>
		public static IDisposable AddHsceneTrespassingButtonWithConfirmation(string buttonText, Func<HSprite, bool> spawnConditionCheck, string confirmBoxTitle, string confirmBoxSentence, Action<HSprite> onConfirmed)
		{
			return AddHsceneTrespassingButton(buttonText, spawnConditionCheck, delegate(HSprite hSprite)
			{
				ConfirmDialog.Status status = ConfirmDialog.status;
				status.Title = confirmBoxTitle;
				status.Sentence = confirmBoxSentence;
				status.Yes = delegate
				{
					onConfirmed(hSprite);
				};
				status.No = delegate
				{
				};
				ConfirmDialog.Load();
			});
		}

		/// <summary>
		/// Add a new button to the H Scene "trespassing" menu (red button below position selections on the right, originally used when having H near another girl so you can start 3P).
		/// </summary>
		/// <param name="buttonText">Text on the button. The button is styled as a speech bubble, so consider spelling it like the MC is saying this.</param>
		/// <param name="spawnConditionCheck">
		/// Check if the button should be shown in the current H scene.
		/// Called at scene start. Return true if button should be spawned, false if not.
		/// Set to null if the button should always be spawned.
		/// </param>
		/// <param name="onButtonClicked">Called when user clicks the button and it is interactable.</param>
		/// <returns>Returns a disposable that when disposed removes this button permanently.</returns>
		public static IDisposable AddHsceneTrespassingButton(string buttonText, Func<HSprite, bool> spawnConditionCheck, Action<HSprite> onButtonClicked)
		{
			CustomTrespassingButton btn = new CustomTrespassingButton(buttonText, spawnConditionCheck, onButtonClicked);
			_customTrespassingButtons.Add(btn);
			Hooks.ApplyHooks();
			return Disposable.Create(delegate
			{
				btn.Dispose();
				_customTrespassingButtons.Remove(btn);
			});
		}

		private static int SpawnButtons(HSprite hSprite, int existingButtons)
		{
			int num = 0;
			foreach (CustomTrespassingButton customTrespassingButton in _customTrespassingButtons)
			{
				try
				{
					customTrespassingButton.Dispose();
					if (SpawnSingleButton(num + existingButtons, hSprite, customTrespassingButton))
					{
						num++;
					}
				}
				catch (Exception arg)
				{
					KoikatuAPI.Logger.LogWarning((object)$"Failed to spawn CustomTrespassingButton text={customTrespassingButton.ButtonText.Replace('\r', ' ').Replace('\n', ' ')}\n{arg}");
				}
			}
			if (num > 0)
			{
				KoikatuAPI.Logger.LogInfo((object)$"Created {num} CustomTrespassingButtons ({existingButtons} already existed)");
			}
			return num;
		}

		private static bool SpawnSingleButton(int id, HSprite hSprite, CustomTrespassingButton buttonData)
		{
			if (buttonData.SpawnConditionCheck != null && !buttonData.SpawnConditionCheck(hSprite))
			{
				return false;
			}
			GameObject gameObject = hSprite.menuActionSub.GetObject(7);
			GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject, gameObject.transform.parent, worldPositionStays: false);
			try
			{
				gameObject2.name = "CustomTrespassButton_" + id;
				UnityEngine.Object.DestroyImmediate(gameObject2.GetComponent<TextChangeCtrl>());
				UnityEngine.Object.DestroyImmediate(gameObject2.GetComponent<HSpriteAutoDisable>());
				SceneAssist.PointerAction component = gameObject2.GetComponent<SceneAssist.PointerAction>();
				component.listDownAction.Add(hSprite.OnMouseDownSlider);
				gameObject2.GetComponentInChildren<TextMeshProUGUI>().text = buttonData.ButtonText;
				gameObject2.transform.SetLocalPositionY(gameObject2.transform.localPosition.y - (float)(100 * id));
				Button component2 = gameObject2.GetComponent<Button>();
				Button.ButtonClickedEvent onClick = component2.onClick;
				((UnityEventBase)onClick).m_Calls.Clear();
				((UnityEventBase)onClick).m_Calls.ClearPersistent();
				((UnityEventBase)onClick).m_PersistentCalls.Clear();
				((UnityEventBase)onClick).m_CallsDirty = false;
				component2.onClick.AddListener(delegate
				{
					if (!Manager.Scene.IsNowLoadingFade && !(Manager.Scene.NowSceneNames[0] != "HProc") && hSprite.IsSpriteAciotn())
					{
						Utils.Sound.Play(SystemSE.sel);
						buttonData.OnButtonClicked(hSprite);
					}
				});
				hSprite.menuActionSub.lstObj.Add(gameObject2);
				hSprite.menuActionSub.makeParent.Add(gameObject2);
				buttonData.Instance = gameObject2;
			}
			catch
			{
				UnityEngine.Object.Destroy(gameObject2);
				throw;
			}
			return true;
		}
	}
	/// <summary>
	/// Allows adding custom topics to the game. You can use them when talking to characters to trigger your own custom events or other actions.
	/// Topics should be registered while still in the title menu or earlier (before the player loads a save file or starts a new game), but registering them later can work too (make sure to test well).
	/// Only useful in the story mode.
	/// </summary>
	public static class TopicApi
	{
		/// <summary>
		/// Apply results of talking about a custom topic. Fired immediately after the ADV scene ends.
		/// Return value is used to add to basic parameters with a fancy animation. The topic field takes an index of the <see cref="P:Manager.GameAssist.topicDrops" /> list (it has an ID of a topic and a % chance of it dropping, it's better to just do it on your own instead of trying to use this).
		/// </summary>
		/// <param name="scene">Talk scene we are in</param>
		/// <param name="topicNo">ID of the custom topic we are talking about</param>
		/// <param name="personality">Personality of the heroine</param>
		/// <param name="isNpc">Is the heroine an NPC (namely the island girl)</param>
		/// <param name="advVars">Variable dictionary from the ADV scene.
		/// You can use this to show choices in your custom ADV scene for player to click (with the Choice command), then set a variable based on what the player chooses (with the VAR command), and finally look for your variable in this dictionary and decide what to do.
		/// In case you want to give the player a cancel option, you can work around the topic being removed at the end by using <see cref="M:KKAPI.MainGame.TopicApi.AddTopicToInventory(System.Int32,System.Boolean,System.Int32)" /> with popup disabled to give it back.</param>
		public delegate ChangeValueTopicInfo GetTopicADVresult(TalkScene scene, int topicNo, int personality, bool isNpc, Dictionary<string, ValData> advVars);

		/// <summary>
		/// Apply results of talking about a custom topic. Fired immediately after the ADV scene ends.
		/// Return value is used to add to basic parameters with a fancy animation.
		/// </summary>
		/// <param name="scene">Talk scene we are in</param>
		/// <param name="topicNo">ID of the custom topic we are talking about</param>
		/// <param name="personality">Personality of the heroine</param>
		/// <param name="isNpc">Is the heroine an NPC (namely the island girl)</param>
		public delegate List<Program.Transfer> GetTopicADVscript(TalkScene scene, int topicNo, int personality, bool isNpc);

		/// <summary>
		/// Category of your topic. Changes which icon is shown, how it can be obtained and when it is shown.
		/// </summary>
		public enum TopicCategory
		{
			/// <summary>
			/// Used for topics related to playing and lazing around.
			/// Can be traded for in the topic exchange shop.
			/// ??? Can randomly drop if the rarity is <see cref="F:KKAPI.MainGame.TopicApi.TopicRarity.Rarity3" /> or lower. (is it a predefined drop table?)
			/// Shown with an icon of beach sunbed and umbrella.
			/// </summary>
			Leisure,
			/// <summary>
			/// Used for topics related to nature.
			/// Can be traded for in the topic exchange shop.
			/// ??? Can randomly drop if the rarity is <see cref="F:KKAPI.MainGame.TopicApi.TopicRarity.Rarity3" /> or lower. (is it a predefined drop table?)
			/// Shown with an icon of a flower.
			/// </summary>
			Nature,
			/// <summary>
			/// Used for topics related to the sea and sea creatures.
			/// Can be traded for in the topic exchange shop.
			/// ??? Can randomly drop if the rarity is <see cref="F:KKAPI.MainGame.TopicApi.TopicRarity.Rarity3" /> or lower. (is it a predefined drop table?)
			/// Shown with an icon of a shell.
			/// </summary>
			Sealife,
			/// <summary>
			/// Used for topics related to the occult and traditions.
			/// Can be traded for in the topic exchange shop.
			/// ??? Can randomly drop if the rarity is <see cref="F:KKAPI.MainGame.TopicApi.TopicRarity.Rarity3" /> or lower. (is it a predefined drop table?)
			/// Shown with an icon of a weird painted face.
			/// </summary>
			Occult,
			/// <summary>
			/// Used for topics related to love and eros.
			/// Topics with this category will not appear when talking with NPCs (island girl), with virgins, or if the R18 patch is not installed.
			/// ??? Can randomly drop if the rarity is <see cref="F:KKAPI.MainGame.TopicApi.TopicRarity.Rarity3" /> or lower. (is it a predefined drop table?)
			/// Can NOT be traded for in the topic exchange shop.
			/// If the rarity is <see cref="F:KKAPI.MainGame.TopicApi.TopicRarity.Rarity3" /> or <see cref="F:KKAPI.MainGame.TopicApi.TopicRarity.Rarity4" />, it can be randomly bought in night shop with the "Ask for a random topic" item.
			/// Shown with an icon of hearts.
			/// </summary>
			Love
		}

		/// <summary>
		/// Rarity of the topic. Changes what color background is used and how it can be obtained.
		/// </summary>
		public enum TopicRarity
		{
			/// <summary>
			/// Can drop on the map.
			/// </summary>
			Rarity1,
			/// <summary>
			/// Can drop on the map.
			/// </summary>
			Rarity2,
			/// <summary>
			/// Can only drop if player has the "good topic drops" prayer effect.
			/// </summary>
			Rarity3,
			/// <summary>
			/// Can't drop. It needs to be added through code or obtained through the topic merge shop.
			/// </summary>
			Rarity4,
			/// <summary>
			/// Can't drop. It needs to be added through code or obtained through the topic merge shop.
			/// </summary>
			Rarity5
		}

		private sealed class CustomTopicInfo
		{
			public readonly Topic.Param Param;

			public readonly GetTopicADVresult ResultGetter;

			public readonly GetTopicADVscript ScriptGetter;

			public CustomTopicInfo(Topic.Param param, GetTopicADVscript scriptGetter, GetTopicADVresult resultGetter)
			{
				Param = param;
				ScriptGetter = scriptGetter;
				ResultGetter = resultGetter;
			}
		}

		private static class TopicHooks
		{
			private static bool _topichooked;

			public static void ApplyHooksIfNeeded()
			{
				//IL_0022: Unknown result type (might be due to invalid IL or missing references)
				//IL_0028: Expected O, but got Unknown
				//IL_004e: Unknown result type (might be due to invalid IL or missing references)
				//IL_005b: Expected O, but got Unknown
				//IL_0085: Unknown result type (might be due to invalid IL or missing references)
				//IL_0091: Expected O, but got Unknown
				if (!_topichooked)
				{
					_topichooked = true;
					KoikatuAPI.Logger.LogWarning((object)"TopicHooks.ApplyHooks");
					Harmony val = new Harmony("TopicApi_Hooks");
					val.Patch((MethodBase)AccessTools.PropertySetter(typeof(ActionScene), "_topicDic"), (HarmonyMethod)null, new HarmonyMethod(typeof(TopicHooks), "TopicDicSetHook", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					val.PatchMoveNext(AccessTools.Method(typeof(TalkScene), "CommandFunc", (Type[])null, (Type[])null), null, null, new HarmonyMethod(typeof(TopicHooks), "TryPlayCustomTopicAdvTpl", (Type[])null));
				}
			}

			public static void TopicDicSetHook(ActionScene __instance)
			{
				foreach (KeyValuePair<int, CustomTopicInfo> customTopic in _customTopics)
				{
					int key = customTopic.Key;
					if (__instance._topicDic.ContainsKey(key))
					{
						KoikatuAPI.Logger.LogWarning((object)$"Overwriting existing topic No={key} Name={__instance._topicDic[key].Name} with Name={customTopic.Value.Param.Name}");
					}
					__instance._topicDic[key] = customTopic.Value.Param;
				}
			}

			private static IEnumerable<CodeInstruction> TryPlayCustomTopicAdvTpl(IEnumerable<CodeInstruction> instructions)
			{
				//IL_0008: Unknown result type (might be due to invalid IL or missing references)
				//IL_0026: Unknown result type (might be due to invalid IL or missing references)
				//IL_002c: Expected O, but got Unknown
				//IL_0059: Unknown result type (might be due to invalid IL or missing references)
				//IL_005f: Expected O, but got Unknown
				//IL_0083: Unknown result type (might be due to invalid IL or missing references)
				//IL_0089: Expected O, but got Unknown
				//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
				//IL_00c5: Expected O, but got Unknown
				//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
				//IL_00ef: Expected O, but got Unknown
				//IL_0124: Unknown result type (might be due to invalid IL or missing references)
				//IL_012a: Expected O, but got Unknown
				//IL_014b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0152: Expected O, but got Unknown
				//IL_017c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0182: Expected O, but got Unknown
				//IL_018a: Unknown result type (might be due to invalid IL or missing references)
				//IL_0190: Expected O, but got Unknown
				CodeMatcher val = new CodeMatcher(instructions, (ILGenerator)null).MatchForward(true, (CodeMatch[])(object)new CodeMatch[1]
				{
					new CodeMatch((OpCode?)OpCodes.Ldstr, (object)"話題を振る", (string)null)
				}).MatchForward(true, (CodeMatch[])(object)new CodeMatch[1]
				{
					new CodeMatch((OpCode?)OpCodes.Call, (object)AccessTools.PropertyGetter(typeof(TalkScene), "isNPC"), (string)null)
				}).Advance(1)
					.ThrowIfNotMatch("Brtrue not found", (CodeMatch[])(object)new CodeMatch[1]
					{
						new CodeMatch((OpCode?)OpCodes.Brtrue, (object)null, (string)null)
					});
				int pos = val.Pos;
				Label elseLabel = (Label)val.Operand;
				val.MatchForward(false, (CodeMatch[])(object)new CodeMatch[1]
				{
					new CodeMatch((Func<CodeInstruction, bool>)((CodeInstruction instruction) => instruction.labels.Contains(elseLabel)), (string)null)
				}).Advance(-1).ThrowIfNotMatch("Br not found", (CodeMatch[])(object)new CodeMatch[1]
				{
					new CodeMatch((OpCode?)OpCodes.Br, (object)null, (string)null)
				});
				object operand = val.Operand;
				val.Advance(pos - val.Pos).ThrowIfNotMatch("Brtrue not found 2", (CodeMatch[])(object)new CodeMatch[1]
				{
					new CodeMatch((OpCode?)OpCodes.Brtrue, (object)null, (string)null)
				});
				val.Advance(-2);
				CodeInstruction val2 = new CodeInstruction(val.Opcode, val.Operand);
				val.Advance(1).Insert((CodeInstruction[])(object)new CodeInstruction[3]
				{
					new CodeInstruction(OpCodes.Call, (object)AccessTools.Method(typeof(TopicHooks), "TryPlayCustomTopicAdvHook", (Type[])null, (Type[])null)),
					new CodeInstruction(OpCodes.Brtrue, operand),
					val2
				});
				return val.Instructions();
			}

			private static bool TryPlayCustomTopicAdvHook(TalkScene scene)
			{
				Topic.Param param = scene.topics[scene.selectTopic];
				int no = param.No;
				_customTopics.TryGetValue(no, out var value);
				if (value != null)
				{
					KoikatuAPI.Logger.LogDebug((object)("Handling custom topic No=" + no));
					List<Program.Transfer> list = value.ScriptGetter(scene, no, scene.targetHeroine.personality, scene.isNPC);
					if (list != null)
					{
						KoikatuAPI.Logger.LogDebug((object)"Playing ADV scene for the topic");
						scene.StartADV(list, CancellationToken.None).GetAwaiter().GetResult();
						Dictionary<string, ValData> advVars = (SingletonInitializer<ActionScene>.initialized ? SingletonInitializer<ActionScene>.instance.AdvScene.Scenario.Vars : SceneParameter.advScene.Scenario.Vars);
						ChangeValueTopicInfo changeValueTopicInfo = value.ResultGetter(scene, no, scene.targetHeroine.personality, scene.isNPC, advVars);
						scene.m_CVInfo = changeValueTopicInfo ?? new ChangeValueTopicInfo();
					}
					return true;
				}
				return false;
			}
		}

		private static readonly Dictionary<int, CustomTopicInfo> _customTopics = new Dictionary<int, CustomTopicInfo>();

		/// <summary>
		/// Add a custom topic to the game.
		/// Custom topics can not be asked about by heroines when using the "listen" option, and selecting a custom topic as an answer will always result in failing the prompt. You can only use custom topics when talking to the heroine (speech bubble icon).
		/// If you want your topic to not be obtainable outside of giving it to the player through code, you need to set rarity to <see cref="F:KKAPI.MainGame.TopicApi.TopicRarity.Rarity4" /> or higher (unless the category is <see cref="F:KKAPI.MainGame.TopicApi.TopicCategory.Love" />, then it has to be <see cref="F:KKAPI.MainGame.TopicApi.TopicRarity.Rarity5" />. (??? drop tables would make it safe as long as it's not buyable)
		/// </summary>
		/// <param name="topicNo">Unique ID of the topic.
		/// This ID is used in the save file to keep track of owned and used topics, so it has to always be the same between game starts (i.e. use a hardcoded number and never change it).
		/// Topic IDs below 100 are reserved for the base game. For safety it's best to use IDs above 100000. Be careful to not conflict with other plugins!</param>
		/// <param name="topicName">Name of your topic shown on the lists.</param>
		/// <param name="category">Category of your topic. Changes which icon is shown, how it can be obtained and when it is shown.</param>
		/// <param name="rarity">Rarity of your topic. Changes what color background is used and how it can be obtained.</param>
		/// <param name="getAdvScript">Called when player chooses your topic to talk about. It should return a valid ADV event (remember to add Close at the end!) or null if you don't want to display anything. You can use the ADVEditor plugin to create your own scenes. The base game uses different variations of events based on the personality and sometimes relationship of the character you talk to. You can do the same by checking the personality and isNPC parameters.</param>
		/// <param name="getAdvResult">Called after your ADV event from 'getAdvScript' finishes. You can change basic stats with the return value (they will be animated), or do something else (return null if you don't want to change any basic stats).</param>
		/// <returns>Dispose the return value to remove the topic. Warning: This is intended only for development use! This might not remove the topic immediately or fully, and you might need to go back to title menu and load the game again for the changes to take effect. Disposing won't clear the topic from topic inventory or other similar lists.</returns>
		public static IDisposable RegisterTopic(int topicNo, string topicName, TopicCategory category, TopicRarity rarity, GetTopicADVscript getAdvScript, GetTopicADVresult getAdvResult)
		{
			if (StudioAPI.InsideStudio)
			{
				return Disposable.Empty;
			}
			if (topicNo < 100)
			{
				throw new ArgumentOutOfRangeException("topicNo", topicNo, "Topic IDs below 100 are reserved for the base game");
			}
			if (topicName == null)
			{
				throw new ArgumentNullException("topicName");
			}
			if (!Enum.IsDefined(typeof(TopicCategory), category))
			{
				throw new ArgumentOutOfRangeException("category", category, "Invalid TopicCategory");
			}
			if (!Enum.IsDefined(typeof(TopicRarity), rarity))
			{
				throw new ArgumentOutOfRangeException("rarity", rarity, "Invalid TopicRarity");
			}
			return RegisterTopic(new Topic.Param
			{
				No = topicNo,
				Name = topicName,
				Category = (int)category,
				Rarity = (int)rarity,
				RarityName = "Modded"
			}, getAdvScript, getAdvResult);
		}

		/// <summary>
		/// Add a custom topic to the game. Check the other overloads for more info.
		/// Warning: This overload skips most of the parameter checks! It's best to use the other overload instead.
		/// </summary>
		public static IDisposable RegisterTopic(Topic.Param param, GetTopicADVscript getAdvScript, GetTopicADVresult getAdvResult)
		{
			if (StudioAPI.InsideStudio)
			{
				return Disposable.Empty;
			}
			if (param == null)
			{
				throw new ArgumentNullException("param");
			}
			if (getAdvScript == null)
			{
				throw new ArgumentNullException("getAdvScript", "You need to return some sort of ADV script or picking the topic will crash the game. You can return a null if you don't want to show any ADV.");
			}
			if (getAdvResult == null)
			{
				throw new ArgumentNullException("getAdvResult", "You need to return some sort of ADV result. You can return a null if you don't want to add any stats.");
			}
			if (param.No < 100)
			{
				throw new ArgumentOutOfRangeException("param", param.No, "No has to be above 100");
			}
			TopicHooks.ApplyHooksIfNeeded();
			_customTopics.Add(param.No, new CustomTopicInfo(param, getAdvScript, getAdvResult));
			if (SingletonInitializer<ActionScene>.initialized && SingletonInitializer<ActionScene>.instance.topicDic != null)
			{
				SingletonInitializer<ActionScene>.instance.topicDic.Add(param.No, param);
			}
			return Disposable.Create(delegate
			{
				_customTopics.Remove(param.No);
				SingletonInitializer<ActionScene>.instance.topicDic.Remove(param.No);
			});
		}

		/// <summary>
		/// Add topic to player's topic inventory/stock. Does nothing if the topic doesn't exist in <see cref="P:ActionScene.topicDic" />.
		/// Returns true if adding was successful, false if there are already 99 of this topic, or if 'amount' is negative and there are 0 of this topic.
		/// </summary>
		/// <param name="topicID">ID of the topic, as seen in ActionScene.instance.topicDic</param>
		/// <param name="showPopup">Should the "topic gained" popup be shown to the player (might not work outside of the roaming map and talk scenes, make sure to test)</param>
		/// <param name="amount">How much of the topic to add. Set to negative number to subtract. Range is -99 to 99 (player can only hold 99 of a single topic).</param>
		public static bool AddTopicToInventory(int topicID, bool showPopup = true, int amount = 1)
		{
			if (StudioAPI.InsideStudio)
			{
				return false;
			}
			if (amount < -99 || amount > 99)
			{
				throw new ArgumentOutOfRangeException("amount", amount, "Value must be between -99 and 99");
			}
			if (!SingletonInitializer<ActionScene>.initialized)
			{
				throw new InvalidOperationException("ActionScene is not initialized");
			}
			if (SingletonInitializer<ActionScene>.instance.topicDic.TryGetValue(topicID, out var value))
			{
				if (showPopup && !SingletonInitializer<InformationUI>.initialized)
				{
					showPopup = false;
				}
				Dictionary<int, SaveData.Player.TopicState> topicStock = Game.Player.topicStock;
				if (!topicStock.TryGetValue(topicID, out var value2))
				{
					SaveData.Player.TopicState topicState = (topicStock[topicID] = new SaveData.Player.TopicState());
					value2 = topicState;
				}
				if (value2.Add(amount))
				{
					if (showPopup)
					{
						InformationUI.SetTopic(value.Name);
					}
					return true;
				}
				if (showPopup)
				{
					InformationUI.SetTopicFull();
				}
				return false;
			}
			KoikatuAPI.Logger.LogWarning((object)("Tried to add nonexisting topic ID=" + topicID));
			return false;
		}
	}
	/// <summary>
	/// Allows adding custom items to the main game shop.
	/// Items have to be registered while still in the title menu or earlier (before the player loads a save file or starts a new game).
	/// Only useful in the story mode.
	/// </summary>
	public static class StoreApi
	{
		/// <summary>
		/// Background color of the item (shows the rough type of the item: normal / unlock / lewd).
		/// </summary>
		public enum ShopBackground
		{
			/// <summary>
			/// Used for normal items that usually restock after day ends (e.g. conversation topics).
			/// </summary>
			Blue,
			/// <summary>
			/// Used for special items that usually don't restock after day ends (they might permanently unlock actions on the map or other features).
			/// </summary>
			Yellow,
			/// <summary>
			/// Used for lewd items that may be permanent unlocks (e.g. new touch options), or single use and restock after day ends (e.g. pills).
			/// </summary>
			Pink
		}

		/// <summary>
		/// Which shop tab to show the item in (day/night).
		/// </summary>
		public enum ShopType
		{
			/// <summary>
			/// Normal - sold in day shop / points store
			/// </summary>
			Normal,
			/// <summary>
			/// Lewd - sold in night shop / night only store
			/// </summary>
			NightOnly
		}

		private static class StoreHooks
		{
			private static bool _storehooked;

			public static void ApplyHooksIfNeeded()
			{
				//IL_0022: Unknown result type (might be due to invalid IL or missing references)
				//IL_0028: Expected O, but got Unknown
				//IL_0051: Unknown result type (might be due to invalid IL or missing references)
				//IL_005d: Expected O, but got Unknown
				//IL_0087: Unknown result type (might be due to invalid IL or missing references)
				//IL_0093: Expected O, but got Unknown
				//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
				//IL_00c9: Expected O, but got Unknown
				if (!_storehooked)
				{
					_storehooked = true;
					KoikatuAPI.Logger.LogDebug((object)"StoreHooks.ApplyHooks");
					Harmony val = new Harmony("StoreApi_Hooks");
					val.PatchMoveNext(AccessTools.Method(typeof(ShopView), "Start", (Type[])null, (Type[])null), null, null, new HarmonyMethod(typeof(StoreHooks), "ShopInitTpl", (Type[])null));
					val.PatchMoveNext(AccessTools.Method(typeof(ShopView), "Buy", (Type[])null, (Type[])null), null, null, new HarmonyMethod(typeof(StoreHooks), "OnBuyTpl", (Type[])null));
					val.Patch((MethodBase)AccessTools.Method(typeof(ShopView), "Conditions", (Type[])null, (Type[])null), new HarmonyMethod(typeof(StoreHooks), "ConditionCheckPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
				}
			}

			public static IEnumerable<CodeInstruction> ShopInitTpl(IEnumerable<CodeInstruction> instructions)
			{
				//IL_0002: Unknown result type (might be due to invalid IL or missing references)
				//IL_002f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0035: Expected O, but got Unknown
				//IL_0056: Unknown result type (might be due to invalid IL or missing references)
				//IL_005c: Expected O, but got Unknown
				//IL_007e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0084: Expected O, but got Unknown
				//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
				//IL_00c2: Expected O, but got Unknown
				return new CodeMatcher(instructions, (ILGenerator)null).MatchForward(false, (CodeMatch[])(object)new CodeMatch[3]
				{
					new CodeMatch((OpCode?)OpCodes.Call, (object)AccessTools.PropertyGetter(typeof(SingletonInitializer<ActionScene>), "instance"), (string)null),
					new CodeMatch((OpCode?)OpCodes.Callvirt, (object)AccessTools.PropertyGetter(typeof(ActionScene), "shopInfoTable"), (string)null),
					new CodeMatch((Func<CodeInstruction, bool>)((CodeInstruction instruction) => instruction.opcode == OpCodes.Callvirt && ((MethodInfo)instruction.operand).Name == "GetEnumerator"), (string)null)
				}).ThrowIfInvalid("foreach not found").Advance(1)
					.Insert((CodeInstruction[])(object)new CodeInstruction[1]
					{
						new CodeInstruction(OpCodes.Call, (object)AccessTools.Method(typeof(StoreHooks), "ShopInitHook", (Type[])null, (Type[])null))
					})
					.Instructions();
			}

			private static void ShopInitHook()
			{
				ShopView shopView = UnityEngine.Object.FindObjectOfType<ShopView>();
				for (int i = 0; i < _customShopCategoryIcons.Count; i++)
				{
					Texture2D value = _customShopCategoryIcons[i];
					int key = i + 1000000;
					shopView.iconTable[key] = value;
				}
				Dictionary<int, ShopInfo.Param> shopInfoTable = SingletonInitializer<ActionScene>.instance.shopInfoTable;
				foreach (ShopInfo.Param customShopItem in _customShopItems)
				{
					KoikatuAPI.Logger.LogDebug((object)("Adding custom item " + customShopItem.Name + " to the shop"));
					if (shopInfoTable.ContainsKey(customShopItem.ID))
					{
						KoikatuAPI.Logger.LogWarning((object)$"Overwriting existing item ID={customShopItem.ID} with Name={customShopItem.Name}");
					}
					shopInfoTable[customShopItem.ID] = customShopItem;
				}
			}

			private static IEnumerable<CodeInstruction> OnBuyTpl(IEnumerable<CodeInstruction> instructions)
			{
				//IL_0002: Unknown result type (might be due to invalid IL or missing references)
				//IL_0031: Unknown result type (might be due to invalid IL or missing references)
				//IL_0037: Expected O, but got Unknown
				//IL_0054: Unknown result type (might be due to invalid IL or missing references)
				//IL_005a: Expected O, but got Unknown
				return new CodeMatcher(instructions, (ILGenerator)null).MatchForward(false, (CodeMatch[])(object)new CodeMatch[1]
				{
					new CodeMatch((OpCode?)OpCodes.Call, (object)AccessTools.Method(typeof(ShopView), "SetInteractable", (Type[])null, (Type[])null), (string)null)
				}).ThrowIfInvalid("SetInteractable not found").Insert((CodeInstruction[])(object)new CodeInstruction[2]
				{
					new CodeInstruction(OpCodes.Dup, (object)null),
					CodeInstruction.Call(typeof(StoreHooks), "OnBuyHook", (Type[])null, (Type[])null)
				})
					.Instructions();
			}

			private static void OnBuyHook(ShopItem itemBought)
			{
				KoikatuAPI.Logger.LogDebug((object)("Bought item " + itemBought.name + " in shop"));
				if (_customShopBuyActions.TryGetValue(itemBought.param.ID, out var value) && value != null)
				{
					try
					{
						value(itemBought);
					}
					catch (Exception exception)
					{
						UnityEngine.Debug.LogException(exception);
					}
				}
			}

			private static bool ConditionCheckPrefix(ref int __result, ShopView __instance, ShopInfo.Param param)
			{
				if (param.ID < 1200 || param.InitType != -1)
				{
					return true;
				}
				__instance.player.buyNumTable.TryGetValue(param.ID, out var value);
				__result = param.Num - value;
				return false;
			}
		}

		private const int CustomCategoryIndexOffset = 1000000;

		private const int MinimumItemId = 1200;

		private static readonly List<ShopInfo.Param> _customShopItems = new List<ShopInfo.Param>();

		private static readonly Dictionary<int, Action<ShopItem>> _customShopBuyActions = new Dictionary<int, Action<ShopItem>>();

		private static readonly List<Texture2D> _customShopCategoryIcons = new List<Texture2D>();

		/// <summary>
		/// Register a new category of shop items (needed for adding custom icons to your shop items).
		/// After registering, use the return value of this method as your item's <see cref="F:ActionGame.ShopInfo.Param.Category" />.
		/// </summary>
		public static int RegisterShopItemCategory(Texture2D categoryIcon)
		{
			_customShopCategoryIcons.Add(categoryIcon);
			return _customShopCategoryIcons.Count - 1 + 1000000;
		}

		/// <summary>
		/// Registers a custom shop item.
		/// </summary>
		/// <param name="itemId">Unique ID of the shop item.
		/// This ID is used in the save file to keep track of purchases, so it has to always be the same between game starts (i.e. use a hardcoded number and never change it).
		/// Values below 1200 are reserved for the base game items. It's best to use values above 100000 just to be safe (be mindful of other mods, ID collisions are not handled).
		/// </param>
		/// <param name="itemName">Name of the item shown on the shop list.</param>
		/// <param name="explaination">Description of the item when hovering over it.</param>
		/// <param name="shopType">Which shop tab to show the item in (day/night).</param>
		/// <param name="itemBackground">Background color of the item (shows the rough type of the item: normal / unlock / lewd).</param>
		/// <param name="itemCategory">Category of the item, changes which icon is used.
		/// Default icons:
		/// 0 - Speech buble
		/// 1 - Seeds
		/// 2 - Egg in a nest
		/// 3 - Question mark
		/// 4 - Massager
		/// 5 - Vibe
		/// 6 - Onahole
		/// 7 - Perfume
		/// 8 - Drugs
		/// 9 - Book
		/// You can add custom icons by using <see cref="M:KKAPI.MainGame.StoreApi.RegisterShopItemCategory(UnityEngine.Texture2D)" />
		/// </param>
		/// <param name="stock">How many of this item are available to be bought.
		/// Number of bought items is stored in <see cref="P:SaveData.Player.buyNumTable" />, and if the stored value is equal or higher than this parameter then the item can't be bought any more.</param>
		/// <param name="resetsDaily">If true, item is rectocked daily. If false, it can only be bought once.
		/// If the item is set to restock every day:
		/// 1 - It will get its owned count reset to 0 at the start of a new day when you click the "Go out" button in your room. You will be able to buy the item again since you don't own any of it any more.
		/// 2 - For the duration of that day it will be placed in the <see cref="P:SaveData.WorldData.shopItemEffect" /> dictionary (key = ID of the item; value = number of the items).
		/// Basically, on the day the item was bought it will be counted in <see cref="P:SaveData.Player.buyNumTable" /> which this method uses, and on the next day it will be counted in <see cref="P:SaveData.WorldData.shopItemEffect" />, until it is completely removed on the day after that.
		/// </param>
		/// <param name="cost">How many koikatsu points this item costs to buy.</param>
		/// <param name="sort">Number used to sort items on the list. Larger number is lower on the list. Value of default items range from 0 to 200.</param>
		/// <param name="numText">Override the "You can buy x more of this item today" text shown below the name of the item. The string has to contain `{0}` in it (gets replaced with the number left). If null, the default game string is used (differs based on the value of resetsDaily).</param>
		/// <param name="onBought">Action to run right after the item was bought by the player (similar to how it pops up the "new topic" message right after buying topics in shop). You can use it to apply effects, or you can check if the item was bought later with <see cref="M:KKAPI.MainGame.StoreApi.GetItemAmountBought(System.Int32)" />. This is fired for each item bought (can't buy multiple items at once).</param>
		/// <returns>Dispose of the return value to remove the item. Warning: This is intended only for development use! This might not remove the item immediately or fully, and you might need to go back to title menu and load the game again for the changes to take effect. Disposing won't clear the item from inventory lists.</returns>
		public static IDisposable RegisterShopItem(int itemId, string itemName, string explaination, ShopType shopType, ShopBackground itemBackground, int itemCategory, int stock, bool resetsDaily, int cost, int sort = 300, string numText = null, Action<ShopItem> onBought = null)
		{
			if (StudioAPI.InsideStudio)
			{
				return Disposable.Empty;
			}
			if (itemId < 1200)
			{
				throw new ArgumentOutOfRangeException("itemId", itemId, "Values below 1200 are reserved for the base game items");
			}
			if (itemName == null)
			{
				throw new ArgumentNullException("itemName");
			}
			if (explaination == null)
			{
				throw new ArgumentNullException("explaination");
			}
			if (!Enum.IsDefined(typeof(ShopType), shopType))
			{
				throw new ArgumentOutOfRangeException("shopType", shopType, "Invalid ShopType");
			}
			if (!Enum.IsDefined(typeof(ShopBackground), itemBackground))
			{
				throw new ArgumentOutOfRangeException("itemBackground", itemBackground, "Invalid ShopBackground");
			}
			if (stock < 0)
			{
				throw new ArgumentOutOfRangeException("stock", stock, "Value can't be negative");
			}
			if (cost < 0)
			{
				throw new ArgumentOutOfRangeException("cost", cost, "Value can't be negative");
			}
			if (itemCategory < 0 || (itemCategory > 9 && itemCategory < 1000000) || itemCategory >= 1000000 + _customShopCategoryIcons.Count)
			{
				throw new ArgumentOutOfRangeException("itemCategory", itemCategory, "itemCategory has to be in range of 0-9 for default categories, or it has to use an index returned from RegisterShopItemCategory.");
			}
			if (numText == null)
			{
				numText = (resetsDaily ? "本日残り{0}回" : "残り{0}個");
			}
			ShopInfo.Param itemParams = new ShopInfo.Param
			{
				ID = itemId,
				Name = itemName,
				Explan = explaination,
				Type = (int)shopType,
				BackGround = (int)itemBackground,
				Category = itemCategory,
				Num = stock,
				NumText = numText,
				InitType = ((!resetsDaily) ? (-1) : 0),
				Pt = cost,
				Sort = sort
			};
			IDisposable result = RegisterShopItem(itemParams);
			if (onBought != null)
			{
				_customShopBuyActions.Add(itemId, onBought);
			}
			return result;
		}

		/// <summary>
		/// Registers a new shop item. Check other RegisterShopItem overloads for more info.
		/// Warning: This overload skips most of the parameter checks! It's best to use the other overload instead.
		/// </summary>
		public static IDisposable RegisterShopItem(ShopInfo.Param itemParams)
		{
			if (StudioAPI.InsideStudio)
			{
				return Disposable.Empty;
			}
			ShopInfo.Param param = _customShopItems.Find((ShopInfo.Param x) => x.ID == itemParams.ID);
			if (param != null)
			{
				throw new ArgumentException($"Could not add new shop item. The ID={itemParams.ID} is already being used by a custom item with Name={param.Name}");
			}
			if (SingletonInitializer<ActionScene>.initialized)
			{
				Dictionary<int, ShopInfo.Param> shopInfoTable = SingletonInitializer<ActionScene>.instance.shopInfoTable;
				if (shopInfoTable != null && shopInfoTable.ContainsKey(itemParams.ID))
				{
					KoikatuAPI.Logger.LogWarning((object)$"Added item ID={itemParams.ID} is already being used by a base game item with Name={SingletonInitializer<ActionScene>.instance.shopInfoTable[itemParams.ID].Name}");
				}
			}
			_customShopItems.Add(itemParams);
			StoreHooks.ApplyHooksIfNeeded();
			return Disposable.Create(delegate
			{
				_customShopItems.Remove(itemParams);
			});
		}

		/// <summary>
		/// Check if store item with this ID was ever bought in the game. This is saved globally and will stay true after first time the item is bought (can work as a permanent global unlock, it works in Free H).
		/// </summary>
		public static bool WasItemBoughtGlobal(int itemId)
		{
			if (StudioAPI.InsideStudio)
			{
				return false;
			}
			return Game.globalData?.buyList?.Contains(itemId) == true;
		}

		/// <summary>
		/// Set if store item with this ID was ever bought in the game. This is saved globally and will stay true after first time the item is bought (can work as a permanent global unlock, it works in Free H).
		/// Returns true if the previous value was different than wasBought, false if it was the same.
		/// </summary>
		public static bool SetItemBoughtGlobal(int itemId, bool wasBought)
		{
			if (StudioAPI.InsideStudio)
			{
				return false;
			}
			if (!wasBought)
			{
				return Game.globalData.buyList.Remove(itemId);
			}
			return Game.globalData.buyList.Add(itemId);
		}

		/// <summary>
		/// Check how many store items with this ID were bought on this save file.
		/// This value can be manually changed with <see cref="M:KKAPI.MainGame.StoreApi.SetItemAmountBought(System.Int32,System.Int32)" />.
		/// This can be used as an inventory, just be careful to avoid ID conflicts with real existing items.
		/// If the item is set to restock every day it will get its count reset to 0 at the start of a new day when you click the "Go out" button in your room.
		/// You can use <see cref="M:KKAPI.MainGame.StoreApi.GetShopItemEffect(System.Int32)" /> to check which restockable items were bought on the previous day.
		/// </summary>
		public static int GetItemAmountBought(int itemId)
		{
			if (StudioAPI.InsideStudio)
			{
				return 0;
			}
			if (Game.Player != null && Game.Player.buyNumTable != null && Game.Player.buyNumTable.TryGetValue(itemId, out var value))
			{
				return value;
			}
			return 0;
		}

		/// <summary>
		/// Check how many store items with this ID were bought on this save file on the previous day.
		/// Only works with items set as resetsDaily=true (restock every day).
		/// Updates when player clicks "Go out" at the start of day. Immediately after buying (before new day start) you can use <see cref="M:KKAPI.MainGame.StoreApi.GetItemAmountBought(System.Int32)" /> instead.
		/// </summary>
		public static int GetShopItemEffect(int itemId)
		{
			if (StudioAPI.InsideStudio)
			{
				return 0;
			}
			if (Game.Player != null && Game.saveData?.shopItemEffect != null && Game.saveData.shopItemEffect.TryGetValue(itemId, out var value))
			{
				return value;
			}
			return 0;
		}

		/// <summary>
		/// Set how many store items with this id were bought. This is used by the store to keep track of how many of each item is still available for purchase.
		/// This value can be read with <see cref="M:KKAPI.MainGame.StoreApi.GetItemAmountBought(System.Int32)" />.
		/// This be used as an inventory, just be careful to avoid ID conflicts with real existing items.
		/// </summary>
		public static void SetItemAmountBought(int itemId, int amount)
		{
			if (!StudioAPI.InsideStudio)
			{
				Dictionary<int, int> buyNumTable = Game.Player.buyNumTable;
				buyNumTable[itemId] = amount;
			}
		}

		/// <summary>
		/// Decrease amount of owned/bought shop items with this id.
		/// The amount is decreased by the <paramref name="amountDecrease" /> parameter (the amount never goes below 0).
		/// The final amount is returned (after being decreased and clamped). The amount can also be read later with <see cref="M:KKAPI.MainGame.StoreApi.GetItemAmountBought(System.Int32)" />.
		/// This be used as an inventory, just be careful to avoid ID conflicts with real existing items.
		/// </summary>
		public static int DecreaseItemAmountBought(int itemId, int amountDecrease = 1)
		{
			if (StudioAPI.InsideStudio)
			{
				return 0;
			}
			int num = Mathf.Max(0, GetItemAmountBought(itemId) - amountDecrease);
			SetItemAmountBought(itemId, num);
			return num;
		}
	}
	/// <summary>
	/// Arguments used with main game save/load events.
	/// </summary>
	public sealed class GameSaveLoadEventArgs : EventArgs
	{
		/// <summary>
		/// Name of the safe file.
		/// </summary>
		public string FileName { get; }

		/// <summary>
		/// Full filename of the save file.
		/// </summary>
		public string FullFilename => System.IO.Path.Combine(Path, FileName);

		/// <summary>
		/// Path to which the save file will be written.
		/// </summary>
		public string Path { get; }

		/// <summary>
		/// Create a new instance
		/// </summary>
		public GameSaveLoadEventArgs(string path, string fileName)
		{
			Path = path;
			FileName = fileName;
		}
	}
	/// <summary>
	/// Provides API for interfacing with the main game. It is useful mostly in the actual game, but some
	/// functions will work outside of it (for example in FreeH).
	/// </summary>
	public static class GameAPI
	{
		public class DayChangeEventArgs : EventArgs
		{
			public Cycle.Week NewDay { get; }

			public DayChangeEventArgs(Cycle.Week newDay)
			{
				NewDay = newDay;
			}
		}

		public class PeriodChangeEventArgs : EventArgs
		{
			public Cycle.Type NewPeriod { get; }

			public PeriodChangeEventArgs(Cycle.Type period)
			{
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
				UniTask task = __result;
				__result = task.ContinueWith(() => __instance.StartCoroutine(OnHStart(__instance)));
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(HSceneProc), "NewHeroineEndProc")]
			[HarmonyPatch(typeof(HSceneProc), "EndProc")]
			public static void EndProcPost(MonoBehaviour __instance)
			{
				OnHEnd(__instance);
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(Cycle), "Change", new Type[] { typeof(Cycle.Type) })]
			public static void CycleChangeTypeHook(Cycle.Type type)
			{
				OnPeriodChange(type);
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(Cycle), "Change", new Type[] { typeof(Cycle.Week) })]
			public static void CycleChangeWeekHook(Cycle.Week week)
			{
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
				where registration.Key.GetType() == controllerType
				select registration.Key).FirstOrDefault();
		}

		/// <summary>
		/// Get the first controller of the specified type that was registered with the specified extendedDataId. The type has to be an exact match.
		/// </summary>
		public static GameCustomFunctionController GetRegisteredBehaviour(Type controllerType, string extendedDataId)
		{
			return (from registration in _registeredHandlers
				where string.Equals(registration.Value, extendedDataId, StringComparison.Ordinal) && registration.Key.GetType() == controllerType
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
			if (insideStudio)
			{
				return;
			}
			Harmony val = new Harmony(typeof(GameAPI).FullName);
			Hooks.SetupHooks(val);
			val.PatchAll(typeof(CustomActionIcon));
			val.PatchAll(typeof(CustomTalkSceneTouchIcon));
			_functionControllerContainer = new GameObject("GameCustomFunctionController Zoo");
			_functionControllerContainer.transform.SetParent(Chainloader.ManagerObject.transform, worldPositionStays: false);
			UnityEngine.SceneManagement.SceneManager.sceneLoaded += delegate(UnityEngine.SceneManagement.Scene arg0, LoadSceneMode mode)
			{
				if (arg0.name == "HotelMyroom" && Manager.Scene.LoadSceneName != "H")
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
			UnityEngine.SceneManagement.SceneManager.activeSceneChanged += delegate(UnityEngine.SceneManagement.Scene scene1, UnityEngine.SceneManagement.Scene scene2)
			{
				int num = _newGameDetectionScenes.IndexOf(scene2.name);
				if (num != -1 && scene1.name.IsNullOrWhiteSpace() && (num == _newGameDetectionIndex + 1 || num == _newGameDetectionIndex - 1))
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
			HSceneProc hSceneProc = baseLoader as HSceneProc;
			HFlag hFlag = hSceneProc?.flags ?? UnityEngine.Object.FindObjectOfType<HFlag>();
			foreach (KeyValuePair<GameCustomFunctionController, string> registeredHandler in _registeredHandlers)
			{
				try
				{
					registeredHandler.Key.OnEndH(baseLoader, hFlag, (object)hSceneProc == null);
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
			HSceneProc hSceneProc = baseLoader as HSceneProc;
			HFlag hFlag = hSceneProc?.flags ?? UnityEngine.Object.FindObjectOfType<HFlag>();
			foreach (KeyValuePair<GameCustomFunctionController, string> registeredHandler in _registeredHandlers)
			{
				try
				{
					registeredHandler.Key.OnStartH(baseLoader, hFlag, (object)hSceneProc == null);
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

		private static void OnDayChange(Cycle.Week day)
		{
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

		private static void OnPeriodChange(Cycle.Type period)
		{
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
			return SingletonInitializer<ActionControl>.instance.actionScene?.AdvScene;
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
			HFlag hFlag = UnityEngine.Object.FindObjectOfType<HFlag>();
			if (hFlag != null)
			{
				return hFlag.GetLeadingHeroine();
			}
			ADVScene aDVScene = GetADVScene();
			if (aDVScene != null)
			{
				if (aDVScene.nowScene is TalkScene { targetHeroine: not null } talkScene)
				{
					return talkScene.targetHeroine;
				}
				if (aDVScene.Scenario != null && aDVScene.Scenario.currentHeroine != null)
				{
					return aDVScene.Scenario.currentHeroine;
				}
			}
			TalkScene talkScene2 = GetTalkScene();
			if (talkScene2 != null)
			{
				Heroine targetHeroine = talkScene2.targetHeroine;
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
	/// <summary>
	/// Base type for custom game extensions.
	/// It provides many useful methods that abstract away the nasty hooks needed to figure out when the state of the game
	/// changes.
	///
	/// This controller is a MonoBehaviour that is created upon registration in <see cref="M:KKAPI.MainGame.GameAPI.RegisterExtraBehaviour``1(System.String)" />.
	/// The controller is created only once. If it's created too late it might miss some events.
	/// It's recommended to register controllers in your Start method.
	/// </summary>
	public abstract class GameCustomFunctionController : MonoBehaviour
	{
		private static Cycle _cycle;

		/// <summary>
		/// Extended save ID used by this function controller
		/// </summary>
		public string ExtendedDataId { get; internal set; }

		/// <summary>
		/// Get extended data based on supplied ExtendedDataId. When in chara maker loads data from character that's being loaded. 
		/// </summary>
		public PluginData GetExtendedData()
		{
			if (ExtendedDataId == null)
			{
				throw new ArgumentException("ExtendedDataId");
			}
			return ExtendedSave.GetExtendedDataById(Game.saveData, ExtendedDataId);
		}

		/// <summary>
		/// Save your custom data to the character card under the ID you specified when registering this controller.
		/// </summary>
		/// <param name="data">Your custom data to be written to the character card. Can be null to remove the data.</param>
		public void SetExtendedData(PluginData data)
		{
			if (ExtendedDataId == null)
			{
				throw new ArgumentException("ExtendedDataId");
			}
			ExtendedSave.SetExtendedDataById(Game.saveData, ExtendedDataId, data);
		}

		/// <summary>
		/// Triggered when the H scene is ended, but before it is unloaded.
		/// Warning: This is triggered in main game, free H, and in VR module as well!
		/// </summary>
		/// <param name="proc">H scene controller instance. Can be either HSceneProc or VRHScene.</param>
		/// <param name="hFlag">H scene parameters, use this over proc if possible.</param>
		/// <param name="vr">If true, we are inside the VR module</param>
		protected internal virtual void OnEndH(MonoBehaviour proc, HFlag hFlag, bool vr)
		{
		}

		/// <summary>
		/// Triggered when the night menu is entered at the end of the day (screen where you can save and load the game).
		/// You can use <see cref="M:KKAPI.MainGame.GameCustomFunctionController.GetCycle" /> to see what day it is as well as other game state.
		/// </summary>
		protected internal virtual void OnEnterHotelMyroomMenu()
		{
		}

		/// <summary>
		/// Triggered right after game state was loaded from a file. Some things might still be uninitialized.
		/// </summary>
		protected internal virtual void OnGameLoad(GameSaveLoadEventArgs args)
		{
		}

		/// <summary>
		/// Triggered right before game state is saved to a file.
		/// </summary>
		protected internal virtual void OnGameSave(GameSaveLoadEventArgs args)
		{
		}

		/// <summary>
		/// Triggered after an H scene is loaded.
		/// Warning: This is triggered in main game, free H, and in VR module as well!
		/// </summary>
		/// <param name="proc">H scene controller instance. Can be either HSceneProc or VRHScene.</param>
		/// <param name="hFlag">H scene parameters, use this over proc if possible.</param>
		/// <param name="vr">If true, we are inside the VR module</param>
		protected internal virtual void OnStartH(MonoBehaviour proc, HFlag hFlag, bool vr)
		{
		}

		/// <summary>
		/// Get the current game Cycle object, if it exists.
		/// </summary>
		protected static Cycle GetCycle()
		{
			if (_cycle == null)
			{
				_cycle = UnityEngine.Object.FindObjectOfType<Cycle>();
			}
			return _cycle;
		}

		/// <summary>
		/// Triggered when the current day changes in story mode.
		/// </summary>
		protected internal virtual void OnDayChange(Cycle.Week day)
		{
		}

		/// <summary>
		/// Triggered when the current time of the day changes in story mode.
		/// </summary>
		protected internal virtual void OnPeriodChange(Cycle.Type period)
		{
		}

		/// <summary>
		/// Triggered when a new game is started in story mode.
		/// </summary>
		protected internal virtual void OnNewGame()
		{
		}
	}
	/// <summary>
	/// Specifies the 3 buttons on the right when talking to a girl (the ones that you click to get a list of conversation topics)
	/// </summary>
	public enum TalkSceneActionKind
	{
		/// <summary>
		/// Top button, speak to the character about something.
		/// </summary>
		Talk,
		/// <summary>
		/// Middle button, listen to the character. Warning: You can't add custom buttons to this!
		/// </summary>
		Listen,
		/// <summary>
		/// Bottom button, ask the character to do something together, e.g. go exercise.
		/// </summary>
		Event
	}
	/// <summary>
	/// Utility methods for use with TalkScenes in main game roaming mode
	/// </summary>
	public static class TalkSceneUtils
	{
		/// <summary>
		/// Where to touch. Same as clicking on the character with your mouse.
		/// </summary>
		public enum TouchLocation
		{
			/// <summary>
			/// Works in both Touch kinds
			/// </summary>
			Head,
			/// <summary>
			/// Only works with Touch kind
			/// </summary>
			Cheek,
			/// <summary>
			/// Only works with Touch kind
			/// </summary>
			HandL,
			/// <summary>
			/// Only works with Touch kind
			/// </summary>
			HandR,
			/// <summary>
			/// Works in both Touch kinds
			/// </summary>
			MuneL,
			/// <summary>
			/// Works in both Touch kinds
			/// </summary>
			MuneR
		}

		/// <summary>
		/// How to touch. Represents the eye and hand buttons at top right corner.
		/// </summary>
		public enum TouchKind
		{
			/// <summary>
			/// Mild reaction, doesn't work with some TouchLocations
			/// </summary>
			Look,
			/// <summary>
			/// Major reaction, works with all TouchLocations
			/// </summary>
			Touch
		}

		/// <summary>
		/// Get scenario data for a specified girl. The data is inside abdata\adv\scenario.
		/// </summary>
		/// <example>
		/// var senarioData = TalkSceneUtils.GetSenarioData(talkScene.targetHeroine, "42"); // 42 - angry event
		/// </example>
		/// <param name="girl">Girl to get the scenario data for</param>
		/// <param name="asset">Scenario name as seen inside the bundles</param>
		public static List<ScenarioData.Param> GetSenarioData(Heroine girl, string asset)
		{
			string[] files = Directory.GetFiles(Path.Combine(Paths.GameRootPath, "abdata\\adv\\scenario\\" + girl.ChaName), "??.unity3d");
			string[] array = files;
			foreach (string path in array)
			{
				AssetBundleLoadAssetOperation assetBundleLoadAssetOperation = AssetBundleManager.LoadAsset("adv/scenario/" + girl.ChaName + "/" + Path.GetFileName(path), asset, typeof(ScenarioData));
				if (assetBundleLoadAssetOperation != null)
				{
					ScenarioData asset2 = assetBundleLoadAssetOperation.GetAsset<ScenarioData>();
					if (!(asset2 == null) && asset2.list != null && asset2.list.Count != 0)
					{
						return asset2.list;
					}
				}
			}
			return null;
		}

		/// <summary>
		/// Simulate touching the character in TalkScene with mouse.
		/// </summary>
		/// <param name="talkScene">Talk scene</param>
		/// <param name="touchLocation">Where to touch</param>
		/// <param name="touchKind">How to touch</param>
		/// <param name="touchPosition">Optional position at which the touch happened (essentially mouse position)</param>
		public static void Touch(this TalkScene talkScene, TouchLocation touchLocation, TouchKind touchKind, Vector3 touchPosition = default(Vector3))
		{
			int touchMode = talkScene.m_touchMode;
			talkScene.m_touchMode = (int)touchKind;
			talkScene.TouchFunc(touchLocation.ToString(), touchPosition);
			talkScene.m_touchMode = touchMode;
		}
	}
	internal sealed class TestGameFunctionController : GameCustomFunctionController
	{
		protected internal override void OnEndH(MonoBehaviour proc, HFlag hFlag, bool vr)
		{
			KoikatuAPI.Logger.Log((LogLevel)12, (object)$"GameController - OnEndH - proc={proc} HFlag={hFlag} vr={vr}");
		}

		protected internal override void OnEnterHotelMyroomMenu()
		{
			KoikatuAPI.Logger.Log((LogLevel)12, (object)"GameController - OnEnterHotelMyroomMenu");
		}

		protected internal override void OnGameLoad(GameSaveLoadEventArgs args)
		{
			KoikatuAPI.Logger.Log((LogLevel)12, (object)("GameController - OnGameLoad - Path=" + args.FullFilename));
		}

		protected internal override void OnGameSave(GameSaveLoadEventArgs args)
		{
			KoikatuAPI.Logger.Log((LogLevel)12, (object)("GameController - OnGameSave - Path=" + args.FullFilename));
		}

		protected internal override void OnStartH(MonoBehaviour proc, HFlag hFlag, bool vr)
		{
			KoikatuAPI.Logger.Log((LogLevel)12, (object)$"GameController - OnStartH - proc={proc} HFlag={hFlag} vr={vr}");
		}

		protected internal override void OnDayChange(Cycle.Week day)
		{
			KoikatuAPI.Logger.Log((LogLevel)12, (object)("GameController - OnDayChange - day=" + day));
		}

		protected internal override void OnPeriodChange(Cycle.Type period)
		{
			KoikatuAPI.Logger.Log((LogLevel)12, (object)("GameController - OnPeriodChange - period=" + period));
		}

		protected internal override void OnNewGame()
		{
			KoikatuAPI.Logger.Log((LogLevel)12, (object)"GameController - OnNewGame");
		}
	}
	internal static class CustomTalkSceneTouchIcon
	{
		private sealed class TouchIconEntry
		{
			public readonly Sprite Icon;

			public readonly int Row;

			public readonly int Order;

			public readonly Action<Button> OnCreated;

			internal UnityEngine.Object Instance;

			public TouchIconEntry(Sprite icon, Action<Button> onCreated, int row, int order)
			{
				Icon = icon;
				OnCreated = onCreated;
				Row = row;
				Order = order;
			}

			public void Dispose()
			{
				UnityEngine.Object.Destroy(Instance);
			}
		}

		private static readonly List<TouchIconEntry> _buttons = new List<TouchIconEntry>();

		public static IDisposable AddTouchIcon(Sprite icon, Action<Button> onCreated, int row = 1, int order = 0)
		{
			if (icon == null)
			{
				throw new ArgumentNullException("icon");
			}
			if (onCreated == null)
			{
				throw new ArgumentNullException("onCreated");
			}
			if (row < 0 || row > 5)
			{
				throw new ArgumentOutOfRangeException("row", "row should be either 0, 1 or 2. It can't be below 0 or higher than 5");
			}
			UnityEngine.Object.DontDestroyOnLoad(icon);
			TouchIconEntry entry = new TouchIconEntry(icon, onCreated, row, order);
			_buttons.Add(entry);
			return Disposable.Create(delegate
			{
				_buttons.Remove(entry);
				entry.Dispose();
			});
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(TalkScene), "Setup")]
		private static void AwakePost(TalkScene __instance)
		{
			Transform transform = __instance.buttonTouch[0].transform;
			Transform transform2 = __instance.buttonTouch[1].transform;
			float x = (transform.localPosition - transform2.localPosition).x;
			foreach (IGrouping<int, TouchIconEntry> item in from touchIconEntry in _buttons
				group touchIconEntry by touchIconEntry.Row)
			{
				float num = 0f - x;
				if (item.Key == 0)
				{
					num += 2f * x;
				}
				float y = x * (float)item.Key * 0.97f;
				Vector3 vector = transform2.localPosition + new Vector3(num, y, 0f);
				foreach (TouchIconEntry item2 in item.OrderBy((TouchIconEntry touchIconEntry) => touchIconEntry.Order).ThenBy((TouchIconEntry item) => _buttons.IndexOf(item)))
				{
					GameObject gameObject = UnityEngine.Object.Instantiate(transform.gameObject, transform.parent, worldPositionStays: false);
					gameObject.transform.localPosition = vector + new Vector3(x, 0f, 0f);
					vector = gameObject.transform.localPosition;
					Button component = gameObject.GetComponent<Button>();
					component.onClick.ActuallyRemoveAllListeners();
					component.image.sprite = item2.Icon;
					component.transition = Selectable.Transition.ColorTint;
					item2.Instance = gameObject;
					item2.OnCreated(component);
				}
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(TalkScene), "Cancel")]
		private static void CancelPost()
		{
			foreach (TouchIconEntry button in _buttons)
			{
				button.Dispose();
			}
		}
	}
	/// <summary>
	/// Utilities for creating and playing custom ADV scenes (talking scenes with the text box at the bottom).
	/// Check CommandList.CommandGet in dnSpy to see source for different ADV commands that you can use.
	/// You can use the ADV_Editor plugin to help with creating custom events: https://github.com/ManlyMarco/ADV_Editor
	/// <code>
	/// var list = EventUtils.CreateNewEvent();
	/// list.Add(Program.Transfer.Text(EventUtils.HeroineName, "Hi, what's your hobby?"));
	/// list.Add(Program.Transfer.Text(EventUtils.PlayerName, "I make plugins for KK!"));
	/// list.Add(Program.Transfer.Text(EventUtils.HeroineName, "That got me wet, take me now!"));
	/// list.Add(Program.Transfer.Text(EventUtils.PlayerName, "No time, writing code."));
	/// list.Add(Program.Transfer.Close());
	/// return EventUtils.StartTextSceneEvent(talkScene, list, decreaseTalkTime: true);
	/// </code>
	/// </summary>
	public static class EventApi
	{
		/// <summary>
		/// Use as Text owner to get the neutral white text used to describe the situation
		/// </summary>
		public const string Narrator = "";

		/// <summary>
		/// Returns empty on default character. Not sure what's the intended use, could be used to set text to player's color while not having player name appear
		/// Need to do Program.SetParam and/or Command.CharaChange on player/heroine for this to work
		/// </summary>
		public const string Player = "[P]";

		/// <summary>
		/// Returns empty on default character. Avoid using, better to use PlayerFullName
		/// Need to do Program.SetParam and/or Command.CharaChange on player/heroine for this to work
		/// </summary>
		public const string PlayerSurname = "[P姓]";

		/// <summary>
		/// Should be used when indicating who is speaking
		/// Need to do Program.SetParam and/or Command.CharaChange on player/heroine for this to work
		/// </summary>
		public const string PlayerName = "[P名]";

		/// <summary>
		/// Name + Surname. Returns same as PlayerName on default character since he has no surname
		/// Need to do Program.SetParam and/or Command.CharaChange on player/heroine for this to work
		/// </summary>
		public const string PlayerFullName = "[P名前]";

		/// <summary>
		/// Nickname. Doesn't seem to work, not set with Program.SetParam?
		/// </summary>
		public const string PlayerNickname = "[Pあだ名]";

		/// <summary>
		/// Avoid using, not sure what's the use
		/// Need to do Program.SetParam and/or Command.CharaChange on player/heroine for this to work
		/// </summary>
		public const string Heroine = "[H]";

		/// <summary>
		/// Some characters might not have a surname, safer to use HeroineFullName
		/// Need to do Program.SetParam and/or Command.CharaChange on player/heroine for this to work
		/// </summary>
		public const string HeroineSurname = "[H姓]";

		/// <summary>
		/// Should be used when indicating who is speaking
		/// Need to do Program.SetParam and/or Command.CharaChange on player/heroine for this to work
		/// </summary>
		public const string HeroineName = "[H名]";

		/// <summary>
		/// Name + Surname
		/// Need to do Program.SetParam and/or Command.CharaChange on player/heroine for this to work
		/// </summary>
		public const string HeroineFullName = "[H名前]";

		/// <summary>
		/// Nickname. Doesn't seem to work, not set with Program.SetParam?
		/// </summary>
		public const string HeroineNickname = "[Hあだ名]";

		/// <summary>
		/// Get current save data of the player
		/// </summary>
		public static SaveData.Player GetPlayerData()
		{
			return Game.saveData?.player;
		}

		/// <summary>
		/// Helper for creating a new event command list. Sets up player variables automatically.
		/// </summary>
		/// <param name="waitForSceneFade">Stop processing inputs until any Scene.Fade finishes (<see cref="F:ADV.Command.SceneFadeRegulate" />, <see cref="F:ADV.Command.SceneFade" />). If true, game might lock up in some cases.</param>
		/// <param name="setPlayerParam">Automatically add any vars of the player to the start of the list (Program.SetParam). You still need to add any Heroine params either through code or by selecting the character with the <see cref="F:ADV.Command.CharaChange" /> command.</param>
		public static List<Program.Transfer> CreateNewEvent(bool waitForSceneFade = false, bool setPlayerParam = true)
		{
			List<Program.Transfer> list = Program.Transfer.NewList(multi: true, waitForSceneFade);
			if (setPlayerParam)
			{
				SaveData.Player playerData = GetPlayerData();
				if (playerData == null)
				{
					throw new ArgumentNullException("playerData");
				}
				Program.SetParam(playerData, list);
			}
			return list;
		}

		/// <summary>
		/// Start a new ADV event inside of a TalkScene (same as stock game events when talking).
		/// The target heroine is automatically added to the heroine list, and their vars are set at the very start.
		/// Warning: You have to have a Close command or the scene will softlock after reaching the end!
		/// </summary>
		/// <param name="talkScene">Currently opened talk scene</param>
		/// <param name="commands">List of commands for the event</param>
		/// <param name="endTalkScene">If true, end the talk scene after this event finishes.</param>
		/// <param name="decreaseTalkTime">Should the talk time bar decrease after the event. If the bar runs out then the talk scene will end.</param>
		public static IEnumerator StartTextSceneEvent(TalkScene talkScene, IEnumerable<Program.Transfer> commands, bool decreaseTalkTime, bool endTalkScene = false)
		{
			if (commands == null)
			{
				throw new ArgumentNullException("commands");
			}
			if (talkScene == null || !talkScene.isActive)
			{
				throw new ArgumentNullException("talkScene", "Has to be ran inside of a TalkScene");
			}
			if (talkScene.targetHeroine == null)
			{
				throw new ArgumentNullException("targetHeroine", "Heroine in TalkScene is null somehow?");
			}
			CancellationToken token = talkScene.cancellation.Token;
			token.ThrowIfCancellationRequested();
			List<Program.Transfer> list = commands.ToList();
			list.Insert(0, Program.Transfer.Create(true, Command.CharaChange, "-2", "true"));
			talkScene.canvas.enabled = false;
			talkScene.raycast.enabled = false;
			yield return talkScene.StartADV(list, token).ToCoroutine(UnityEngine.Debug.LogException);
			if (decreaseTalkTime)
			{
				Singleton<GameAssist>.Instance.DecreaseTalkTime(talkScene.targetHeroine, 1);
				if (talkScene.targetHeroine.talkTime <= 0)
				{
					endTalkScene = true;
				}
			}
			if (endTalkScene)
			{
				yield return talkScene.TalkEnd(token).ToCoroutine(UnityEngine.Debug.LogException);
			}
			talkScene.LeaveAloneCancel();
			talkScene.lstColDisposable.ForEach(delegate(TalkScene.ColDisposableInfo x)
			{
				x.End();
			});
			talkScene.TouchCancel();
			if (talkScene.transVoice != null)
			{
				Voice.Stop(talkScene.transVoice);
				talkScene.transVoice = null;
			}
			if (!token.IsCancellationRequested)
			{
				talkScene.OnClickMain(-1);
			}
			if (!endTalkScene)
			{
				yield return talkScene.CommandWait(token).ToCoroutine(UnityEngine.Debug.LogException);
			}
		}

		/// <summary>
		/// Start a new full ADV scene with fade in, full control over camera and map, multiple characters and such. Can be used in roaming mode and some other cases.
		/// Best to use this for custom events where player talks to other characters, or characters other than player talk (if player talks with himself consider using <see cref="M:KKAPI.MainGame.EventApi.StartMonologueEvent(System.Collections.Generic.IEnumerable{ADV.Program.Transfer})" /> instead).
		/// Do not use in TalkScenes, use <see cref="M:KKAPI.MainGame.EventApi.StartTextSceneEvent(TalkScene,System.Collections.Generic.IEnumerable{ADV.Program.Transfer},System.Boolean,System.Boolean)" /> instead.
		/// You can get variables set in the commands through actScene.AdvScene.Scenario.Vars.
		/// You can specify heroines you want to use in this script and use the CharaChange command to load their parameters. You can also use Program.SetParam to hard code the parameters in the list. 
		/// Warning: By default you need to have ScreenFadeRegulate and SceneFade commands at the start of your script, check fadeIn parameter description for more info.
		/// Warning: You have to have a Close command or the scene will softlock after reaching the end!
		/// </summary>
		/// <param name="commands">List of commands for the event</param>
		/// <param name="fadeIn">Should the scene fade in and out (otherwise it instantly pops in and out of existence, in that case consider using <see cref="M:KKAPI.MainGame.EventApi.StartMonologueEvent(System.Collections.Generic.IEnumerable{ADV.Program.Transfer})" /> instead).
		/// Important: With this turned on you have to have `ScreenFadeRegulate "false"` as first command or your script will never start (it's automatically added by <see cref="M:KKAPI.MainGame.EventApi.CreateNewEvent(System.Boolean,System.Boolean)" />).
		/// You also need to add a `SceneFade "out"` command at the start to fade out the effect and show the dialog (spawn characters and do all initialization before this step so it's not visible).
		/// At the end of the scene if you want to fade out then add a `SceneFade "in"` command at the end of your script (after closing it will fade back to the game automatically).</param>
		/// <param name="extraData">Event called when running the scene and fade, also fade type. Can be safely ignored in most cases.</param>
		/// <param name="camera">Initial position and rotation of the camera. By default the camera starts facing the center character. Can be safely ignored in most cases.</param>
		/// <param name="heroines">List of heroines to set in event context. They can be accessed from commands like <see cref="F:ADV.Command.CharaCreate" /> and <see cref="F:ADV.Command.CharaChange" />. If null, current Scenario.heroineList is used.</param>
		/// <param name="position">This is the position of the center character, NOT the camera. Camera is controlled separately and by default looks at the character from the front.
		/// You can use <see cref="F:ADV.Command.CameraPositionSet" /> and <see cref="F:ADV.Command.CharaPositionSet" /> to change this from your script instead.</param>
		/// <param name="rotation">This is the rotation of the center character, NOT the camera. Camera is controlled separately and by default looks at the character from the front. Should only change Y rotation and keep XZ = 0.
		/// You can use <see cref="F:ADV.Command.CameraRotationSet" /> to change the camera rotation from your script.</param>
		public static IEnumerator StartAdvEvent(IEnumerable<Program.Transfer> commands, Vector3 position, Quaternion rotation, bool fadeIn = true, Program.OpenDataProc extraData = null, OpenData.CameraData camera = null, List<Heroine> heroines = null)
		{
			if (commands == null)
			{
				throw new ArgumentNullException("commands");
			}
			List<Program.Transfer> list = commands.ToList();
			if (list.All((Program.Transfer x) => x.param.Command != Command.Close))
			{
				list.Add(Program.Transfer.Close());
				UnityEngine.Debug.LogWarning("No Close command in the transfer list! Adding close at end to prevent lockup. Add Program.Transfer.Close() at the end of list to fix this.");
			}
			CancellationToken token = CancellationToken.None;
			ActionScene actScene = SingletonInitializer<ActionScene>.instance;
			actScene.Player.isActionNow = true;
			string prevBGM = string.Empty;
			float prevVolume = 1f;
			yield return TuplelessGetBgmAndVolume(delegate(string x, float y)
			{
				prevBGM = x;
				prevVolume = y;
			});
			if (fadeIn)
			{
				if (list.Take(4).All((Program.Transfer x) => x.param.Command != Command.SceneFadeRegulate))
				{
					list.Insert(0, Program.Transfer.Create(true, Command.SceneFadeRegulate, "false"));
					UnityEngine.Debug.LogWarning("No 'SceneFadeRegulate false' command at the start of the transfer list when fadeIn=true! Adding SceneFadeRegulate at start to prevent lockup. Add 'SceneFadeRegulate false' at the start of list to fix this.");
				}
				if (list.All((Program.Transfer x) => x.param.Command != Command.SceneFade || !(x.param.Args.FirstOrDefault()?.ToLower() == "out")))
				{
					list.Insert(1, Program.Transfer.Create(true, Command.SceneFade, "out"));
					UnityEngine.Debug.LogWarning("No 'SceneFade out' command in the transfer list! Adding 'SceneFade out' at start to prevent lockup. Add 'SceneFade out' after you are done with initializing the scene and before first text to fix this. Make sure it's set to multi=true and that there are no multi=false items before it or the game will lock up.");
				}
				if (list.All((Program.Transfer x) => x.param.Command != Command.SceneFade || !(x.param.Args.FirstOrDefault()?.ToLower() == "in")))
				{
					UnityEngine.Debug.LogWarning("No 'SceneFade in' command in the transfer list! The scene won't fade out back to gameplay, instead it will instantly jump at the end until you add a 'SceneFade in' command before the Close command. Warning: Make sure that 'SceneFadeRegulate false' command has been ran before you run SceneFade or it will lock up.");
				}
				yield return Manager.Scene.sceneFadeCanvas.StartFadeAysnc(FadeCanvas.Fade.In).ToCoroutine(UnityEngine.Debug.LogException);
			}
			list.Insert(1, Program.Transfer.Create(true, Command.CameraSetFov, Program.BASE_FOV));
			actScene.Player.SetActive(active: false);
			actScene.npcList.ForEach(delegate(NPC p)
			{
				p.SetActive(active: false);
			});
			actScene.npcList.ForEach(delegate(NPC p)
			{
				p.Pause(isPause: true);
			});
			if (actScene.fixChara != null)
			{
				actScene.fixChara.SetActive(active: false);
			}
			bool isOpenADV = false;
			if (extraData == null)
			{
				extraData = new Program.OpenDataProc();
			}
			Program.OpenDataProc openDataProc = extraData;
			openDataProc.onLoad = (Action)Delegate.Combine(openDataProc.onLoad, (Action)delegate
			{
				isOpenADV = true;
			});
			yield return Program.Open(new Data
			{
				fadeInTime = 0f,
				position = position,
				rotation = rotation,
				camera = camera,
				heroineList = (heroines ?? actScene.AdvScene.Scenario.heroineList),
				scene = SingletonInitializer<ActionScene>.instance,
				transferList = list
			}, token, extraData).ToCoroutine(UnityEngine.Debug.LogException);
			yield return UniTask.WaitUntil(() => isOpenADV, PlayerLoopTiming.Update, token).ToCoroutine(UnityEngine.Debug.LogException);
			yield return Program.Wait(string.Empty, token).ToCoroutine(UnityEngine.Debug.LogException);
			actScene.Player.SetActive(active: true);
			actScene.npcList.ForEach(delegate(NPC p)
			{
				p.SetActive(p.mapNo == actScene.Map.no);
			});
			actScene.npcList.ForEach(delegate(NPC p)
			{
				p.Pause(isPause: false);
			});
			if (actScene.fixChara != null)
			{
				actScene.fixChara.SetActive(actScene.fixChara.mapNo == actScene.Map.no);
			}
			yield return Manager.Scene.sceneFadeCanvas.StartFadeAysnc(FadeCanvas.Fade.Out).ToCoroutine(UnityEngine.Debug.LogException);
			Manager.Scene.sceneFadeCanvas.DefaultColor();
			yield return Utils.Sound.GetFadePlayerWhileNull(prevBGM, prevVolume).ToCoroutine(UnityEngine.Debug.LogException);
			actScene.Player.isActionNow = false;
		}

		/// <summary>
		/// Start a new "light" ADV scene that only pauses player input and shows a dialog box. Can be used in roaming mode and some other cases.
		/// Best to use this for events where player thinks or talks to himself with no other characters participating, for example after picking up an item on the map.
		/// Do not use in TalkScenes, use <see cref="M:KKAPI.MainGame.EventApi.StartTextSceneEvent(TalkScene,System.Collections.Generic.IEnumerable{ADV.Program.Transfer},System.Boolean,System.Boolean)" /> instead.
		/// You can get variables set in the commands through actScene.AdvScene.Scenario.Vars
		/// You need to use Program.SetParam if you want to add parameters related to a heroine. Alternatively use the CharaChange command with the global heroine syntax.
		/// Warning: You have to have a Close command or the scene will softlock after reaching the end!
		/// </summary>
		/// <param name="commands">List of commands for the event</param>
		public static IEnumerator StartMonologueEvent(IEnumerable<Program.Transfer> commands)
		{
			if (commands == null)
			{
				throw new ArgumentNullException("commands");
			}
			List<Program.Transfer> list = commands.ToList();
			if (list.All((Program.Transfer x) => x.param.Command != Command.Close))
			{
				list.Add(Program.Transfer.Close());
				UnityEngine.Debug.LogWarning("No Close command in the transfer list! Adding close at end to prevent lockup. Add Program.Transfer.Close() at the end of list to fix this.");
			}
			CancellationToken token = CancellationToken.None;
			ActionScene actScene = SingletonInitializer<ActionScene>.instance;
			actScene.Player.isActionNow = true;
			string prevBGM = string.Empty;
			float prevVolume = 1f;
			yield return TuplelessGetBgmAndVolume(delegate(string x, float y)
			{
				prevBGM = x;
				prevVolume = y;
			});
			ActionScene instance = SingletonInitializer<ActionScene>.instance;
			Transform transform = Camera.main.transform;
			bool isOpenADV = false;
			yield return Program.Open(new Data
			{
				position = instance.Player.position,
				rotation = instance.Player.rotation,
				scene = instance,
				camera = new OpenData.CameraData
				{
					position = transform.position,
					rotation = transform.rotation
				},
				transferList = list
			}, token, new Program.OpenDataProc
			{
				onLoad = delegate
				{
					isOpenADV = true;
				}
			}).ToCoroutine(UnityEngine.Debug.LogException);
			yield return UniTask.WaitUntil(() => isOpenADV, PlayerLoopTiming.Update, token).ToCoroutine(UnityEngine.Debug.LogException);
			yield return Program.ADVProcessingCheck(token).ToCoroutine(UnityEngine.Debug.LogException);
			yield return Utils.Sound.GetFadePlayerWhileNull(prevBGM, prevVolume).ToCoroutine(UnityEngine.Debug.LogException);
			Manager.Scene.sceneFadeCanvas.DefaultColor();
			actScene.Player.isActionNow = false;
		}

		private static IEnumerator TuplelessGetBgmAndVolume(Action<string, float> callback)
		{
			Sound_Component currentBgm = Utils.Sound.GetBGM();
			if (currentBgm != null)
			{
				AudioSource src = currentBgm.GetComponent<AudioSource>();
				FadePlayer fp = src.GetComponent<FadePlayer>();
				yield return new WaitWhile(() => fp != null && !(fp.nowState is FadePlayer.Playing));
				callback(currentBgm.bundle, src.volume);
			}
			else
			{
				callback(string.Empty, 1f);
			}
		}

		/// <summary>
		/// Reads value of a variable from the ADV VAR dictionary.
		/// </summary>
		/// <typeparam name="T">Type of the value.</typeparam>
		/// <param name="advvars">The VAR dictionary.</param>
		/// <param name="varName">Name of the variable to get.</param>
		/// <param name="value">Value of the variable if it exists and is of a type equal to or convertible to <typeparamref name="T" />. If the variable isn't defined or is null, a default value is returned.</param>
		/// <returns>True if the value exists and its value could be read, false otherwise.</returns>
		public static bool TryGetVarValue<T>(this Dictionary<string, ValData> advvars, string varName, out T value) where T : IConvertible
		{
			if (advvars == null)
			{
				throw new ArgumentNullException("advvars");
			}
			if (varName == null)
			{
				throw new ArgumentNullException("varName");
			}
			if (advvars.TryGetValue(varName, out var value2))
			{
				if (value2.o == null)
				{
					value = default(T);
					return true;
				}
				if (value2.o is T val)
				{
					value = val;
					return true;
				}
				try
				{
					value = (T)Convert.ChangeType(value2.o, typeof(T));
					return true;
				}
				catch (Exception ex)
				{
					UnityEngine.Debug.Log("Could not convert VAR name=" + varName + " from type=" + value2.o.GetType().Name + " to type=" + typeof(T).Name + " - " + ex.Message);
				}
			}
			value = default(T);
			return false;
		}
	}
	/// <summary>
	/// Extensions useful in the main game
	/// </summary>
	public static class GameExtensions
	{
		/// <summary>
		/// Returns true if the H scene is peeping in the shower.
		/// Use <see cref="P:HFlag.mode" /> to get info on what mode the H scene is in.
		/// </summary>
		public static bool IsShowerPeeping(this HFlag hFlag)
		{
			if (hFlag == null)
			{
				throw new ArgumentNullException("hFlag");
			}
			if (hFlag.mode == HFlag.EMode.peeping)
			{
				return hFlag.nowAnimationInfo.nameAnimation == "シャワー覗き";
			}
			return false;
		}

		/// <summary>
		/// Get the persisting heroine object that describes this character.
		/// Returns null if the heroine could not be found. Works only in the main game.
		/// </summary>
		public static Heroine GetHeroine(this ChaControl chaControl)
		{
			if (chaControl == null)
			{
				throw new ArgumentNullException("chaControl");
			}
			if (!SingletonInitializer<Game>.initialized)
			{
				return null;
			}
			return Game.HeroineList.Find((Heroine heroine) => heroine.chaCtrl == chaControl);
		}

		/// <summary>
		/// Get the persisting heroine object that describes this character.
		/// Returns null if the heroine could not be found. Works only in the main game.
		/// </summary>
		public static Heroine GetHeroine(this ChaFileControl chaFile)
		{
			if (chaFile == null)
			{
				throw new ArgumentNullException("chaFile");
			}
			if (!SingletonInitializer<Game>.initialized)
			{
				return null;
			}
			return Game.HeroineList.Find((Heroine heroine) => heroine.GetRelatedChaFiles().Contains(chaFile));
		}

		/// <summary>
		/// Get the NPC that represents this heroine in the game. Works only in the main game.
		/// If the heroine has not been spawned into the game it returns null.
		/// </summary>
		public static NPC GetNPC(this Heroine heroine)
		{
			if (heroine == null)
			{
				throw new ArgumentNullException("heroine");
			}
			if (heroine.charaBase is NPC result)
			{
				return result;
			}
			if (heroine.transform == null)
			{
				return null;
			}
			return heroine.transform.GetComponent<NPC>();
		}

		/// <summary>
		/// Get ChaFiles that are related to this heroine. Warning: It might not return some copies.
		/// </summary>
		public static IEnumerable<ChaFileControl> GetRelatedChaFiles(this Heroine heroine)
		{
			if (heroine == null)
			{
				throw new ArgumentNullException("heroine");
			}
			HashSet<ChaFileControl> hashSet = new HashSet<ChaFileControl>();
			if (heroine.charFile != null)
			{
				hashSet.Add(heroine.charFile);
			}
			if (heroine.chaCtrl != null && heroine.chaCtrl.chaFile != null)
			{
				hashSet.Add(heroine.chaCtrl.chaFile);
			}
			NPC nPC = heroine.GetNPC();
			if (nPC != null && nPC.chaCtrl != null && nPC.chaCtrl.chaFile != null)
			{
				hashSet.Add(nPC.chaCtrl.chaFile);
			}
			return hashSet;
		}

		/// <summary>
		/// Get the persisting player object that describes this character.
		/// Returns null if the player could not be found. Works only in the main game.
		/// </summary>
		public static SaveData.Player GetPlayer(this ChaControl chaControl)
		{
			if (chaControl == null)
			{
				throw new ArgumentNullException("chaControl");
			}
			if (!SingletonInitializer<Game>.initialized || Game.Player == null)
			{
				return null;
			}
			if (!(Game.Player.chaCtrl == chaControl))
			{
				return null;
			}
			return Game.Player;
		}

		/// <summary>
		/// Get the persisting player object that describes this character.
		/// Returns null if the player could not be found. Works only in the main game.
		/// </summary>
		public static SaveData.Player GetPlayer(this ChaFileControl chaFile)
		{
			if (chaFile == null)
			{
				throw new ArgumentNullException("chaFile");
			}
			if (!SingletonInitializer<Game>.initialized || Game.Player == null)
			{
				return null;
			}
			if (!Game.Player.GetRelatedChaFiles().Contains(chaFile))
			{
				return null;
			}
			return Game.Player;
		}

		/// <summary>
		/// Get ChaFiles that are related to this player. Warning: It might not return some copies.
		/// </summary>
		public static IEnumerable<ChaFileControl> GetRelatedChaFiles(this SaveData.Player player)
		{
			if (player == null)
			{
				throw new ArgumentNullException("player");
			}
			HashSet<ChaFileControl> hashSet = new HashSet<ChaFileControl>();
			if (player.charFile != null)
			{
				hashSet.Add(player.charFile);
			}
			if (player.chaCtrl != null && player.chaCtrl.chaFile != null)
			{
				hashSet.Add(player.chaCtrl.chaFile);
			}
			return hashSet;
		}

		/// <summary>
		/// Set the value of isCursorLock (setter is private by default).
		/// Used to regain mouse cursor during roaming mode.
		/// Best used together with setting <see cref="P:UnityEngine.Time.timeScale" /> to 0 to pause the game.
		/// </summary>
		public static void SetIsCursorLock(this ActionScene actScene, bool value)
		{
			actScene._isCursorLock = value;
		}

		/// <summary>
		/// Get a list of free slots in class roster. New heroines can be added to these slots in saveData.heroineList.
		/// Warning: When inside the character roster, ClassRoomSelectScene.instance._classRoomList.charaList has to be used instead!
		/// <example>
		/// var freeSlot = saveData.GetFreeClassSlots(1).FirstOrDefault();
		/// freeSlot.SetCharFile(fairyCard.charFile);
		/// freeSlot.charFileInitialized = true;
		/// saveData.heroineList.Add(freeSlot);
		/// </example>
		/// </summary>
		/// <param name="saveData"></param>
		/// <param name="classNumber">Class to search for free slots. Starts at 0, 1 is player's class, last 'class' is not usable since it's only for NPCs.</param>
		public static IEnumerable<Heroine> GetFreeClassSlots(this WorldData saveData, int classNumber)
		{
			return from index in Enumerable.Range(0, saveData.GetClassMaxSeatCount(classNumber))
				where saveData.GetHeroineAtSeat(classNumber, index) == null
				select new Heroine(isRandomize: true)
				{
					schoolClass = classNumber,
					schoolClassIndex = index
				};
		}

		/// <summary>
		/// Get seat count in a class based on game settings.
		/// </summary>
		public static int GetClassMaxSeatCount(this WorldData saveData, int classNumber)
		{
			if (!saveData.isLiveNumMax)
			{
				switch (classNumber)
				{
				case 0:
					return 20;
				case 1:
					return 5;
				case 2:
					return 15;
				}
			}
			else
			{
				switch (classNumber)
				{
				case 0:
					return 45;
				case 1:
					return 20;
				case 2:
					return 35;
				}
			}
			return 0;
		}

		/// <summary>
		/// Get heroine at a specified class and seat.
		/// </summary>
		public static Heroine GetHeroineAtSeat(this WorldData saveData, int classNumber, int classIndex)
		{
			List<Heroine> list = (SingletonInitializer<ClassRoomSelectScene>.initialized ? SingletonInitializer<ClassRoomSelectScene>.instance._classRoomList.charaList.OfType<Heroine>().ToList() : saveData.heroineList);
			return list.Find((Heroine h) => h.schoolClass == classNumber && h.schoolClassIndex == classIndex);
		}
	}
	/// <summary>
	/// An equality comparer that uses StringComparison.OrdinalIgnoreCase rule
	/// </summary>
	public class IgnoreCaseEqualityComparer : IEqualityComparer<string>
	{
		/// <summary>
		/// Instance of the comparer for use in linq and such
		/// </summary>
		public static readonly IgnoreCaseEqualityComparer Instance = new IgnoreCaseEqualityComparer();

		/// <inheritdoc />
		public bool Equals(string x, string y)
		{
			if (x == null || y == null)
			{
				return false;
			}
			return x.Equals(y, StringComparison.OrdinalIgnoreCase);
		}

		/// <inheritdoc />
		public int GetHashCode(string obj)
		{
			return obj.GetHashCode();
		}
	}
}
