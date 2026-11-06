using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using BepisPlugins;
using ChaCustom;
using HarmonyLib;
using Illusion;
using MessagePack;
using MessagePack.Resolvers;
using SaveData;
using Studio;
using UnityEngine;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: AssemblyProduct("ExtensibleSaveFormat")]
[assembly: AssemblyTitle("BepisPlugins.ExtensibleSaveFormat")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("https://github.com/bbepis/BepisPlugins")]
[assembly: AssemblyCopyright("Copyright ©BepisPlugins 2019")]
[assembly: AssemblyTrademark("")]
[assembly: ComVisible(false)]
[assembly: AssemblyFileVersion("20.0")]
[assembly: AssemblyInformationalVersion("20.0")]
[assembly: TargetFramework(".NETFramework,Version=v4.6", FrameworkDisplayName = ".NET Framework 4.6")]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: AssemblyVersion("1.0.0.0")]
[module: UnverifiableCode]
internal static class Extensions
{
	/// <summary>
	/// Returns a new string in which all occurrences of a specified string in the current instance are replaced with another 
	/// specified string according the type of search to use for the specified string.
	/// Stolen from https://stackoverflow.com/a/45756981
	/// </summary>
	/// <param name="str">The string performing the replace method.</param>
	/// <param name="oldValue">The string to be replaced.</param>
	/// <param name="newValue">The string replace all occurrences of <paramref name="oldValue" />. 
	/// If value is equal to <c>null</c>, than all occurrences of <paramref name="oldValue" /> will be removed from the <paramref name="str" />.</param>
	/// <param name="comparisonType">One of the enumeration values that specifies the rules for the search.</param>
	/// <returns>A string that is equivalent to the current string except that all instances of <paramref name="oldValue" /> are replaced with <paramref name="newValue" />. 
	/// If <paramref name="oldValue" /> is not found in the current instance, the method returns the current instance unchanged.</returns>
	internal static string Replace(this string str, string oldValue, string newValue, StringComparison comparisonType)
	{
		if (str == null)
		{
			throw new ArgumentNullException("str");
		}
		if (str.Length == 0)
		{
			return str;
		}
		if (oldValue == null)
		{
			throw new ArgumentNullException("oldValue");
		}
		if (oldValue.Length == 0)
		{
			throw new ArgumentException("String cannot be of zero length.");
		}
		StringBuilder stringBuilder = new StringBuilder(str.Length);
		bool flag = string.IsNullOrEmpty(newValue);
		int num = 0;
		int num2;
		while ((num2 = str.IndexOf(oldValue, num, comparisonType)) != -1)
		{
			int num3 = num2 - num;
			if (num3 != 0)
			{
				stringBuilder.Append(str, num, num3);
			}
			if (!flag)
			{
				stringBuilder.Append(newValue);
			}
			num = num2 + oldValue.Length;
			if (num == str.Length)
			{
				return stringBuilder.ToString();
			}
		}
		int count = str.Length - num;
		stringBuilder.Append(str, num, count);
		return stringBuilder.ToString();
	}

	/// <summary>
	/// Find first position of the byte sequence in the stream starting at current position.
	/// Returns position of first byte of the sequence.
	/// https://stackoverflow.com/questions/1550560/encoding-an-integer-in-7-bit-format-of-c-sharp-binaryreader-readstring
	/// </summary>
	internal static long FindPosition(this Stream stream, byte[] byteSequence)
	{
		if (byteSequence.Length > stream.Length)
		{
			return -1L;
		}
		byte[] array = new byte[byteSequence.Length];
		BufferedStream bufferedStream = new BufferedStream(stream, byteSequence.Length);
		while (bufferedStream.Read(array, 0, byteSequence.Length) == byteSequence.Length)
		{
			if (byteSequence.SequenceEqual(array))
			{
				return bufferedStream.Position - byteSequence.Length;
			}
			bufferedStream.Position -= byteSequence.Length - PadLeftSequence(array, byteSequence);
		}
		return -1L;
		static int PadLeftSequence(byte[] bytes, byte[] seqBytes)
		{
			int i;
			for (i = 1; i < bytes.Length; i++)
			{
				int num = bytes.Length - i;
				byte[] array2 = new byte[num];
				byte[] array3 = new byte[num];
				Array.Copy(bytes, i, array2, 0, num);
				Array.Copy(seqBytes, array3, num);
				if (array2.SequenceEqual(array3))
				{
					return i;
				}
			}
			return i;
		}
	}
}
namespace Shared
{
	/// <summary>
	/// Utility methods for working with IMGUI / OnGui.
	/// </summary>
	internal static class IMGUIUtils
	{
		private static Texture2D SolidBoxTex { get; set; }

		private static Texture2D TransparentBoxTex { get; set; }

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
				texture2D.SetPixel(0, 0, new Color(0.4f, 0.4f, 0.4f));
				texture2D.Apply();
				SolidBoxTex = texture2D;
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
		/// Draw a gray semi-transparent GUI.Box at the specified rect.
		/// </summary>
		public static void DrawTransparentBox(Rect boxRect)
		{
			if (TransparentBoxTex == null)
			{
				Texture2D texture2D = new Texture2D(1, 1, TextureFormat.ARGB32, mipChain: false);
				texture2D.SetPixel(0, 0, new Color(0.4f, 0.4f, 0.4f, 0.7f));
				texture2D.Apply();
				TransparentBoxTex = texture2D;
			}
			GUI.Box(boxRect, GUIContent.none, new GUIStyle
			{
				normal = new GUIStyleState
				{
					background = TransparentBoxTex
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

		public static void DrawLayoutLabelWithShadow(GUIContent content, GUIStyle style, Color txtColor, Color shadowColor, Vector2 direction, params GUILayoutOption[] options)
		{
			DrawLabelWithShadow(GUILayoutUtility.GetRect(content, style, options), content, style, txtColor, shadowColor, direction);
		}

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

		public static bool DrawLayoutButtonWithShadow(GUIContent content, GUIStyle style, float shadowAlpha, Vector2 direction, params GUILayoutOption[] options)
		{
			return DrawButtonWithShadow(GUILayoutUtility.GetRect(content, style, options), content, style, shadowAlpha, direction);
		}
	}
	internal class LazyCustom<T>
	{
		private T _object;

		private Func<T> Factory { get; set; }

		public T Instance
		{
			get
			{
				Initialize();
				return _object;
			}
		}

		private LazyCustom(Func<T> factory)
		{
			Factory = factory;
		}

		public void Initialize()
		{
			if (_object == null || _object.ToString() == "null")
			{
				_object = Factory();
			}
		}

		public static implicit operator T(LazyCustom<T> lazy)
		{
			return lazy.Instance;
		}

		public static LazyCustom<TClass> Create<TClass>() where TClass : new()
		{
			return new LazyCustom<TClass>(() => new TClass());
		}

		public static LazyCustom<TClass> Create<TClass>(Func<TClass> factory)
		{
			return new LazyCustom<TClass>(factory);
		}
	}
}
namespace BepisPlugins
{
	internal static class Constants
	{
		internal const string GameProcessName = "KoikatsuSunshine";

		internal const string StudioProcessName = "CharaStudio";

		internal const string VRProcessName = "KoikatsuSunshine_VR";

		internal static bool InsideStudio => Application.productName == "CharaStudio";
	}
	internal static class Metadata
	{
		/// <summary>
		/// Version number used be all plugins. Must be convertible to <see cref="T:System.Version" />
		/// Use ReleaseVersion.HotfixVersion format
		/// </summary>
		public const string PluginsVersion = "20.0";
	}
	/// <summary>
	/// Utility methods for working with embedded resources.
	/// </summary>
	internal static class ResourceUtils
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
		public static byte[] GetEmbeddedResource(string resourceFileName, Assembly containingAssembly = null)
		{
			if (containingAssembly == null)
			{
				containingAssembly = Assembly.GetCallingAssembly();
			}
			string name = containingAssembly.GetManifestResourceNames().Single((string str) => str.EndsWith(resourceFileName));
			using Stream stream = containingAssembly.GetManifestResourceStream(name);
			return (stream ?? throw new InvalidOperationException("The resource " + resourceFileName + " was not found")).ReadAllBytes();
		}
	}
	internal class WeakKeyDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>, IEnumerable where TValue : class
	{
		private readonly Dictionary<int, List<KeyValuePair<WeakReference, TValue>>> _items;

		public int Count => DoCount();

		public WeakKeyDictionary()
		{
			_items = new Dictionary<int, List<KeyValuePair<WeakReference, TValue>>>();
		}

		private int DoCount()
		{
			int num = 0;
			foreach (List<KeyValuePair<WeakReference, TValue>> value in _items.Values)
			{
				num += value.Count;
			}
			return num;
		}

		public void Set(TKey key, TValue value)
		{
			int hashCode = RuntimeHelpers.GetHashCode(key);
			lock (_items)
			{
				if (_items.TryGetValue(hashCode, out var value2))
				{
					bool flag = false;
					for (int i = 0; i < value2.Count; i++)
					{
						if (value2[i].Value == value)
						{
							value2[i] = new KeyValuePair<WeakReference, TValue>(new WeakReference(key), value);
							flag = true;
							break;
						}
					}
					if (!flag)
					{
						value2.Add(new KeyValuePair<WeakReference, TValue>(new WeakReference(key), value));
					}
				}
				else
				{
					List<KeyValuePair<WeakReference, TValue>> list = new List<KeyValuePair<WeakReference, TValue>>();
					list.Add(new KeyValuePair<WeakReference, TValue>(new WeakReference(key), value));
					_items[hashCode] = list;
				}
				PurgeDeadKeys();
			}
		}

		private void PurgeDeadKeys()
		{
			List<int> list = new List<int>();
			foreach (int key in _items.Keys)
			{
				List<KeyValuePair<WeakReference, TValue>> list2 = _items[key].Where((KeyValuePair<WeakReference, TValue> kvp) => !kvp.Key.IsAlive).ToList();
				foreach (KeyValuePair<WeakReference, TValue> item in list2)
				{
					_items[key].Remove(item);
				}
				if (_items[key].Count == 0)
				{
					list.Add(key);
				}
			}
			foreach (int item2 in list)
			{
				_items.Remove(item2);
			}
		}

		public TValue Get(TKey key)
		{
			int hashCode = RuntimeHelpers.GetHashCode(key);
			if (_items.TryGetValue(hashCode, out var value))
			{
				TValue val = null;
				foreach (KeyValuePair<WeakReference, TValue> item in value)
				{
					if (item.Key.IsAlive && item.Key.Target == (object)key)
					{
						val = item.Value;
					}
				}
				if (val != null)
				{
					return val;
				}
			}
			return null;
		}

		public bool Contains(TKey key)
		{
			List<KeyValuePair<WeakReference, TValue>> value;
			return _items.TryGetValue(RuntimeHelpers.GetHashCode(key), out value);
		}

		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
		{
			List<KeyValuePair<TKey, TValue>> list = new List<KeyValuePair<TKey, TValue>>();
			foreach (int key in _items.Keys)
			{
				foreach (KeyValuePair<WeakReference, TValue> item in _items[key])
				{
					if (item.Key.IsAlive)
					{
						list.Add(new KeyValuePair<TKey, TValue>((TKey)item.Key.Target, item.Value));
					}
				}
			}
			return list.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public void Clear()
		{
			_items.Clear();
		}
	}
}
namespace ExtensibleSaveFormat
{
	/// <summary>
	/// A set of tools for reading and writing extra data to card and scene files.
	/// </summary>
	[BepInProcess("KoikatsuSunshine")]
	[BepInProcess("KoikatsuSunshine_VR")]
	[BepInProcess("CharaStudio")]
	[BepInPlugin("com.bepis.bepinex.extendedsave", "Extended Save", "20.0")]
	public class ExtendedSave : BaseUnityPlugin
	{
		/// <summary> SaveData event handler </summary>
		public delegate void SaveDataEventHandler(WorldData saveData);

		internal static class Hooks
		{
			/// <summary>
			/// Prevent coords from being trimmed to 4 and then overwritten with defaults if MoreOutfits is missing
			/// (basically a copy of some of the MoreOutfits hooks, should not be applied if MoreOutfits exists)
			/// </summary>
			private static class HandleNoMoreOutfitsHooks
			{
				private static Harmony _nmoHooks;

				private static bool DoingImport;

				public static void Apply()
				{
					_nmoHooks = Harmony.CreateAndPatchAll(typeof(HandleNoMoreOutfitsHooks), "HandleNoMoreOutfitsHooks");
				}

				/// <summary>
				/// Ensure extra coordinates are loaded
				/// </summary>
				[HarmonyPrefix]
				[HarmonyPatch(typeof(ChaFile), "SetCoordinateBytes")]
				private static bool SetCoordinateBytes(ChaFile __instance, byte[] data, Version ver)
				{
					List<byte[]> list = MessagePackSerializer.Deserialize<List<byte[]>>(data);
					__instance.coordinate = new ChaFileCoordinate[list.Count];
					for (int i = 0; i < list.Count; i++)
					{
						__instance.coordinate[i] = new ChaFileCoordinate();
					}
					for (int j = 0; j < __instance.coordinate.Length; j++)
					{
						__instance.coordinate[j].LoadBytes(list[j], ver);
					}
					return false;
				}

				[HarmonyPostfix]
				[HarmonyPatch(typeof(ConvertChaFileScene), "Start")]
				private static void ConvertChaFileSceneStart()
				{
					DoingImport = true;
				}

				[HarmonyPostfix]
				[HarmonyPatch(typeof(ConvertChaFileScene), "OnDestroy")]
				private static void ConvertChaFileSceneEnd()
				{
					DoingImport = false;
					_nmoHooks.UnpatchSelf();
				}

				/// <summary>
				/// Don't allow outfits to be replaced by defaults
				/// </summary>
				[HarmonyPrefix]
				[HarmonyPatch(typeof(ChaFile), "AssignCoordinate")]
				private static bool ChaFile_AssignCoordinate()
				{
					if (DoingImport)
					{
						return false;
					}
					return true;
				}
			}

			private static readonly bool MoreOutfitsInstalled;

			private static Dictionary<int, int?> CoordinateMapping;

			private static OCIChar PoseChar;

			private static string PoseName;

			private const string PoseMarker = "KKEx";

			private static bool cardReadEventCalled;

			private static byte[] currentlySavingData;

			[HarmonyPostfix]
			[HarmonyPatch(typeof(WorldData), "SetBytes")]
			private static void SaveDataLoadHook(BinaryReader br, ref WorldData saveData)
			{
				try
				{
					string text = br.ReadString();
					int num = br.ReadInt32();
					int num2 = br.ReadInt32();
					if (text == Marker && num == DataVersion && num2 > 0)
					{
						byte[] bytes = br.ReadBytes(num2);
						Dictionary<string, PluginData> value = MessagePackSerializer.Deserialize<Dictionary<string, PluginData>>(bytes);
						internalSaveDataDictionary.Set(saveData, value);
					}
					else
					{
						internalSaveDataDictionary.Set(saveData, new Dictionary<string, PluginData>());
					}
				}
				catch (EndOfStreamException)
				{
					internalSaveDataDictionary.Set(saveData, new Dictionary<string, PluginData>());
				}
				catch (InvalidOperationException)
				{
					internalSaveDataDictionary.Set(saveData, new Dictionary<string, PluginData>());
				}
				SaveDataReadEvent(saveData);
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(WorldData), "Save", new Type[]
			{
				typeof(string),
				typeof(string)
			})]
			[HarmonyPriority(0)]
			private static bool WorldDataSaveOverride(WorldData __instance, string path, string fileName)
			{
				Utils.File.OpenWrite(path + fileName, isAppend: false, delegate(FileStream f)
				{
					try
					{
						using BinaryWriter binaryWriter = new BinaryWriter(f);
						WorldData worldData = __instance;
						byte[] array = MessagePackSerializer.Serialize(worldData);
						binaryWriter.Write(array.Length);
						binaryWriter.Write(array);
						byte[] bytes = Player.GetBytes(worldData.player);
						binaryWriter.Write(bytes.Length);
						binaryWriter.Write(bytes);
						int count = worldData.heroineList.Count;
						binaryWriter.Write(count);
						for (int i = 0; i < count; i++)
						{
							byte[] bytes2 = Heroine.GetBytes(worldData.heroineList[i]);
							binaryWriter.Write(bytes2.Length);
							binaryWriter.Write(bytes2);
						}
						SaveDataWriteEvent(__instance);
						Logger.Log((LogLevel)32, (object)"SaveData hook!");
						Dictionary<string, PluginData> allExtendedData = GetAllExtendedData(worldData);
						if (allExtendedData != null)
						{
							binaryWriter.Write(Marker);
							binaryWriter.Write(DataVersion);
							byte[] array2 = MessagePackSerializer.Serialize(allExtendedData);
							binaryWriter.Write(array2.Length);
							binaryWriter.Write(array2);
						}
					}
					catch (Exception ex)
					{
						Logger.Log((LogLevel)10, (object)("Failed to save the game, the save file may be corrupted! Try saving again in an empty slot.\nError: " + ex.Message));
						UnityEngine.Debug.LogException(ex);
					}
				});
				return false;
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(WorldData), "GetBytes", new Type[] { typeof(WorldData) })]
			private static void WorldDataGetBytesDisableHook(WorldData saveData, ref byte[] __result)
			{
				throw new NotSupportedException("Do not use this method, use WorldData.Save instead. More info: https://github.com/IllusionMods/BepisPlugins/pull/197");
			}

			static Hooks()
			{
				CoordinateMapping = new Dictionary<int, int?>();
				currentlySavingData = null;
				MoreOutfitsInstalled = Type.GetType("KK_Plugins.MoreOutfits.Plugin, KKS_MoreOutfits") != null;
				if (!MoreOutfitsInstalled)
				{
					HandleNoMoreOutfitsHooks.Apply();
				}
			}

			private static void KKChaFileLoadFileHook(ChaFile file, BlockHeader header, BinaryReader reader)
			{
				CoordinateMapping = new Dictionary<int, int?>();
				ChaFileCoordinate[] array = new ChaFileCoordinate[file.coordinate.Length];
				for (int i = 0; i < file.coordinate.Length; i++)
				{
					int num = i;
					int? value = null;
					switch (i)
					{
					case 0:
						if (MoreOutfitsInstalled)
						{
							value = 4;
						}
						break;
					case 1:
						value = 3;
						break;
					case 2:
						if (MoreOutfitsInstalled)
						{
							value = 5;
						}
						break;
					case 3:
						value = 1;
						break;
					case 4:
						if (MoreOutfitsInstalled)
						{
							value = 6;
						}
						break;
					case 5:
						value = 0;
						break;
					case 6:
						value = 2;
						break;
					default:
						if (MoreOutfitsInstalled)
						{
							value = num;
						}
						break;
					}
					CoordinateMapping[num] = value;
					if (value.HasValue)
					{
						array[value.Value] = file.coordinate[num];
					}
				}
				if (!MoreOutfitsInstalled)
				{
					array = array.Take(4).ToArray();
				}
				file.coordinate = array;
				BlockHeader.Info info = header.SearchInfo(Marker);
				if (info == null || !(info.version == DataVersion.ToString()))
				{
					return;
				}
				long position = reader.BaseStream.Position;
				long num2 = position - header.lstInfo.Sum((BlockHeader.Info x) => x.size);
				reader.BaseStream.Position = num2 + info.pos;
				byte[] obj = reader.ReadBytes((int)info.size);
				reader.BaseStream.Position = position;
				try
				{
					Dictionary<string, PluginData> dictionary = MessagePackDeserialize<Dictionary<string, PluginData>>(obj);
					if (dictionary != null)
					{
						if (!file.charaFileName.IsNullOrEmpty())
						{
							Logger.Log((LogLevel)32, (object)("Importing \"" + file.parameter?.fullname + "\" from \"" + file.charaFileName + "\" "));
						}
						CardImportEvent(dictionary, CoordinateMapping);
						internalCharaDictionary.Set(file, dictionary);
					}
				}
				catch (Exception ex)
				{
					Logger.Log((LogLevel)4, (object)("Invalid or corrupted extended data in card \"" + file.charaFileName + "\" - " + ex.Message));
					reader.BaseStream.Position = position;
				}
			}

			[HarmonyTranspiler]
			[HarmonyPatch(typeof(ChaFile), "LoadFileKoikatsu", new Type[]
			{
				typeof(BinaryReader),
				typeof(bool),
				typeof(bool)
			})]
			private static IEnumerable<CodeInstruction> KKChaFileLoadFileTranspiler(IEnumerable<CodeInstruction> instructions)
			{
				//IL_006f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0075: Expected O, but got Unknown
				//IL_0083: Unknown result type (might be due to invalid IL or missing references)
				//IL_0089: Expected O, but got Unknown
				//IL_0091: Unknown result type (might be due to invalid IL or missing references)
				//IL_0097: Expected O, but got Unknown
				//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
				//IL_00bd: Expected O, but got Unknown
				List<CodeInstruction> list = new List<CodeInstruction>(instructions);
				int num = list.FindIndex((CodeInstruction instruction) => CheckCallVirtName(instruction, "SearchInfo"));
				int num2 = list.FindLastIndex((CodeInstruction instruction) => CheckCallVirtName(instruction, "Seek"));
				CodeInstruction val = list[num - 2];
				list.InsertRange(num2 + 2, (IEnumerable<CodeInstruction>)(object)new CodeInstruction[4]
				{
					new CodeInstruction(OpCodes.Ldarg_0, (object)null),
					new CodeInstruction(val.opcode, val.operand),
					new CodeInstruction(OpCodes.Ldarg_1, (object)null),
					new CodeInstruction(OpCodes.Call, (object)typeof(Hooks).GetMethod("KKChaFileLoadFileHook", AccessTools.all))
				});
				return list;
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFile), "LoadFileKoikatsu", new Type[]
			{
				typeof(BinaryReader),
				typeof(bool),
				typeof(bool)
			})]
			private static void KKChaFileLoadFilePostHook(ChaFile __instance, bool __result, BinaryReader br)
			{
				if (!__result || br.BaseStream.Position == br.BaseStream.Length)
				{
					return;
				}
				long position = br.BaseStream.Position;
				try
				{
					string text = br.ReadString();
					int num = br.ReadInt32();
					if (text == "KKEx" && num == 2)
					{
						int num2 = br.ReadInt32();
						if (num2 <= 0)
						{
							return;
						}
						byte[] obj = br.ReadBytes(num2);
						Dictionary<string, PluginData> dictionary = MessagePackDeserialize<Dictionary<string, PluginData>>(obj);
						if (dictionary != null)
						{
							if (!__instance.charaFileName.IsNullOrEmpty())
							{
								Logger.Log((LogLevel)32, (object)("Importing \"" + __instance.parameter?.fullname + "\" from \"" + __instance.charaFileName + "\" "));
							}
							CardImportEvent(dictionary, CoordinateMapping);
							internalCharaDictionary.Set(__instance, dictionary);
						}
					}
					else
					{
						br.BaseStream.Position = position;
					}
				}
				catch (EndOfStreamException)
				{
					br.BaseStream.Position = position;
				}
				catch (SystemException)
				{
					br.BaseStream.Position = position;
				}
			}

			[HarmonyTranspiler]
			[HarmonyPatch(/*Could not decode attribute arguments.*/)]
			private static IEnumerable<CodeInstruction> SceneInfoLoadTranspiler(IEnumerable<CodeInstruction> instructions)
			{
				bool set = false;
				List<CodeInstruction> instructionsList = instructions.ToList();
				for (int i = 0; i < instructionsList.Count; i++)
				{
					CodeInstruction inst = instructionsList[i];
					yield return inst;
					if (!set && inst.opcode == OpCodes.Stind_Ref && (instructionsList[i + 1].opcode == OpCodes.Leave || instructionsList[i + 1].opcode == OpCodes.Leave_S))
					{
						yield return new CodeInstruction(OpCodes.Ldarg_1, (object)null);
						yield return new CodeInstruction(OpCodes.Ldloc_1, (object)null);
						yield return new CodeInstruction(OpCodes.Call, (object)typeof(Hooks).GetMethod("SceneInfoLoadHook"));
						set = true;
					}
				}
				if (!set)
				{
					throw new Exception("Failed to patch SceneInfo.Load");
				}
			}

			public static void SceneInfoLoadHook(string path, BinaryReader br)
			{
				internalSceneDictionary.Clear();
				try
				{
					br.ReadString();
					string text = br.ReadString();
					int num = br.ReadInt32();
					int num2 = br.ReadInt32();
					if (text.Equals(Marker) && num2 > 0)
					{
						byte[] bytes = br.ReadBytes(num2);
						internalSceneDictionary = MessagePackSerializer.Deserialize<Dictionary<string, PluginData>>(bytes);
					}
				}
				catch (EndOfStreamException)
				{
				}
				catch (InvalidOperationException)
				{
				}
				SceneReadEvent(path);
			}

			[HarmonyTranspiler]
			[HarmonyPatch(typeof(SceneInfo), "Import", new Type[] { typeof(string) })]
			private static IEnumerable<CodeInstruction> SceneInfoImportTranspiler(IEnumerable<CodeInstruction> instructions)
			{
				bool set = false;
				List<CodeInstruction> instructionsList = instructions.ToList();
				for (int i = 0; i < instructionsList.Count; i++)
				{
					CodeInstruction inst = instructionsList[i];
					yield return inst;
					if (!set && inst.opcode == OpCodes.Call && (instructionsList[i + 1].opcode == OpCodes.Leave || instructionsList[i + 1].opcode == OpCodes.Leave_S))
					{
						yield return new CodeInstruction(OpCodes.Ldarg_1, (object)null);
						yield return new CodeInstruction(OpCodes.Ldloc_1, (object)null);
						yield return new CodeInstruction(OpCodes.Ldloc_2, (object)null);
						yield return new CodeInstruction(OpCodes.Call, (object)typeof(Hooks).GetMethod("SceneInfoImportHook"));
						set = true;
					}
				}
				if (!set)
				{
					throw new Exception("Failed to patch SceneInfo.Import");
				}
			}

			public static void SceneInfoImportHook(string path, BinaryReader br, Version _)
			{
				internalSceneDictionary.Clear();
				while (true)
				{
					try
					{
						long num = br.BaseStream.FindPosition(Marker.Select((char c) => (byte)c).ToArray()) - 1;
						if (num < 0)
						{
							break;
						}
						br.BaseStream.Position = num;
						string text = br.ReadString();
						int num2 = br.ReadInt32();
						int num3 = br.ReadInt32();
						if (text.Equals(Marker) && num3 > 0)
						{
							byte[] bytes = br.ReadBytes(num3);
							internalSceneDictionary = MessagePackSerializer.Deserialize<Dictionary<string, PluginData>>(bytes);
						}
						continue;
					}
					catch (EndOfStreamException)
					{
					}
					catch (InvalidOperationException)
					{
						continue;
					}
					break;
				}
				SceneImportEvent(path);
			}

			[HarmonyTranspiler]
			[HarmonyPatch(typeof(SceneInfo), "Save", new Type[] { typeof(string) })]
			private static IEnumerable<CodeInstruction> SceneInfoSaveTranspiler(IEnumerable<CodeInstruction> instructions)
			{
				bool set = false;
				List<CodeInstruction> instructionsList = instructions.ToList();
				for (int i = 0; i < instructionsList.Count; i++)
				{
					CodeInstruction inst = instructionsList[i];
					yield return inst;
					if (!set && inst.opcode == OpCodes.Callvirt && (instructionsList[i + 1].opcode == OpCodes.Leave || instructionsList[i + 1].opcode == OpCodes.Leave_S))
					{
						yield return new CodeInstruction(OpCodes.Ldarg_1, (object)null);
						yield return new CodeInstruction(OpCodes.Ldloc_1, (object)null);
						yield return new CodeInstruction(OpCodes.Call, (object)typeof(Hooks).GetMethod("SceneInfoSaveHook"));
						set = true;
					}
				}
				if (!set)
				{
					throw new Exception("Failed to patch SceneInfo.Save");
				}
			}

			public static void SceneInfoSaveHook(string path, BinaryWriter bw)
			{
				SceneWriteEvent(path);
				Dictionary<string, PluginData> internalSceneDictionary = ExtendedSave.internalSceneDictionary;
				if (internalSceneDictionary == null)
				{
					return;
				}
				List<string> list = new List<string>();
				foreach (KeyValuePair<string, PluginData> item in internalSceneDictionary)
				{
					if (item.Value == null)
					{
						list.Add(item.Key);
					}
				}
				foreach (string item2 in list)
				{
					internalSceneDictionary.Remove(item2);
				}
				byte[] array = MessagePackSerializer.Serialize(internalSceneDictionary);
				bw.Write(Marker);
				bw.Write(DataVersion);
				bw.Write(array.Length);
				bw.Write(array);
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(PauseCtrl), "Load")]
			private static void PauseCtrl_Load(OCIChar _ociChar, string _path)
			{
				PoseChar = _ociChar;
				PoseName = _path;
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(PauseCtrl.FileInfo), "Load")]
			private static void PauseCtrl_FileInfo_Load(BinaryReader _reader, PauseCtrl.FileInfo __instance)
			{
				PoseLoadHook(_reader, __instance);
			}

			private static void PoseLoadHook(BinaryReader br, PauseCtrl.FileInfo fileInfo)
			{
				internalPoseDictionary.Clear();
				try
				{
					string text = br.ReadString();
					int num = br.ReadInt32();
					int num2 = br.ReadInt32();
					if (text.Equals("KKEx") && num2 > 0)
					{
						byte[] bytes = br.ReadBytes(num2);
						internalPoseDictionary = MessagePackSerializer.Deserialize<Dictionary<string, PluginData>>(bytes);
					}
				}
				catch (EndOfStreamException)
				{
				}
				catch (InvalidOperationException)
				{
				}
				PluginData poseExtendedDataById = GetPoseExtendedDataById("com.bepis.bepinex.extendedsave");
				GameNames gameName = GameNames.Unknown;
				if (poseExtendedDataById != null && poseExtendedDataById.data.TryGetValue("gameName", out var value) && value != null)
				{
					gameName = (GameNames)value;
				}
				PoseReadEvent(PoseName, fileInfo, PoseChar, gameName);
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(PauseCtrl), "Save")]
			private static void PauseCtrl_Save(OCIChar _ociChar, string _name)
			{
				PoseChar = _ociChar;
				PoseName = _name;
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(PauseCtrl.FileInfo), "Save")]
			private static void PauseCtrl_FileInfo_Save(BinaryWriter _writer, PauseCtrl.FileInfo __instance)
			{
				PoseSaveHook(_writer, __instance);
			}

			private static void PoseSaveHook(BinaryWriter bw, PauseCtrl.FileInfo fileInfo)
			{
				PoseWriteEvent(PoseName, fileInfo, PoseChar, GameName);
				PluginData pluginData = new PluginData();
				pluginData.data.Add("gameName", GameName);
				SetPoseExtendedDataById("com.bepis.bepinex.extendedsave", pluginData);
				Dictionary<string, PluginData> internalPoseDictionary = ExtendedSave.internalPoseDictionary;
				if (internalPoseDictionary == null)
				{
					return;
				}
				List<string> list = new List<string>();
				foreach (KeyValuePair<string, PluginData> item in internalPoseDictionary)
				{
					if (item.Value == null)
					{
						list.Add(item.Key);
					}
				}
				foreach (string item2 in list)
				{
					internalPoseDictionary.Remove(item2);
				}
				byte[] array = MessagePackSerializer.Serialize(internalPoseDictionary);
				bw.Write("KKEx");
				bw.Write(DataVersion);
				bw.Write(array.Length);
				bw.Write(array);
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(CharaList), "InitFemaleList")]
			private static void StudioFemaleListPreHook()
			{
				LoadEventsEnabled = false;
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(CharaList), "InitFemaleList")]
			private static void StudioFemaleListPostHook()
			{
				LoadEventsEnabled = true;
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(CharaList), "InitMaleList")]
			private static void StudioMaleListPreHook()
			{
				LoadEventsEnabled = false;
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(CharaList), "InitMaleList")]
			private static void StudioMaleListPostHook()
			{
				LoadEventsEnabled = true;
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(MPCharCtrl.CostumeInfo), "InitFileList")]
			private static void StudioCoordinateListPreHook()
			{
				LoadEventsEnabled = false;
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(MPCharCtrl.CostumeInfo), "InitFileList")]
			private static void StudioCoordinateListPostHook()
			{
				LoadEventsEnabled = true;
			}

			internal static void InstallHooks()
			{
				Harmony val = Harmony.CreateAndPatchAll(typeof(Hooks), "com.bepis.bepinex.extendedsave");
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(ChaFile), "LoadFile", new Type[]
			{
				typeof(BinaryReader),
				typeof(bool),
				typeof(bool)
			})]
			private static void ChaFileLoadFilePreHook()
			{
				cardReadEventCalled = false;
			}

			private static void ChaFileLoadFileHook(ChaFile file, BlockHeader header, BinaryReader reader)
			{
				BlockHeader.Info info = header.SearchInfo(Marker);
				if (LoadEventsEnabled && info != null && info.version == DataVersion.ToString())
				{
					long position = reader.BaseStream.Position;
					long num = position - header.lstInfo.Sum((BlockHeader.Info x) => x.size);
					reader.BaseStream.Position = num + info.pos;
					byte[] bytes = reader.ReadBytes((int)info.size);
					reader.BaseStream.Position = position;
					cardReadEventCalled = true;
					try
					{
						Dictionary<string, PluginData> value = MessagePackSerializer.Deserialize<Dictionary<string, PluginData>>(bytes);
						internalCharaDictionary.Set(file, value);
					}
					catch (Exception ex)
					{
						internalCharaDictionary.Set(file, new Dictionary<string, PluginData>());
						Logger.LogWarning((object)("Invalid or corrupted extended data in card \"" + file.charaFileName + "\" - " + ex.Message));
					}
					CardReadEvent(file);
				}
				else
				{
					internalCharaDictionary.Set(file, new Dictionary<string, PluginData>());
				}
			}

			[HarmonyTranspiler]
			[HarmonyPatch(typeof(ChaFile), "LoadFile", new Type[]
			{
				typeof(BinaryReader),
				typeof(bool),
				typeof(bool)
			})]
			private static IEnumerable<CodeInstruction> ChaFileLoadFileTranspiler(IEnumerable<CodeInstruction> instructions)
			{
				//IL_006f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0075: Expected O, but got Unknown
				//IL_0083: Unknown result type (might be due to invalid IL or missing references)
				//IL_0089: Expected O, but got Unknown
				//IL_0091: Unknown result type (might be due to invalid IL or missing references)
				//IL_0097: Expected O, but got Unknown
				//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
				//IL_00bd: Expected O, but got Unknown
				List<CodeInstruction> list = new List<CodeInstruction>(instructions);
				int num = list.FindIndex((CodeInstruction instruction) => CheckCallVirtName(instruction, "SearchInfo"));
				int num2 = list.FindLastIndex((CodeInstruction instruction) => CheckCallVirtName(instruction, "Seek"));
				CodeInstruction val = list[num - 2];
				list.InsertRange(num2 + 2, (IEnumerable<CodeInstruction>)(object)new CodeInstruction[4]
				{
					new CodeInstruction(OpCodes.Ldarg_0, (object)null),
					new CodeInstruction(val.opcode, val.operand),
					new CodeInstruction(OpCodes.Ldarg_1, (object)null),
					new CodeInstruction(OpCodes.Call, (object)typeof(Hooks).GetMethod("ChaFileLoadFileHook", AccessTools.all))
				});
				return list;
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFile), "LoadFile", new Type[]
			{
				typeof(BinaryReader),
				typeof(bool),
				typeof(bool)
			})]
			private static void ChaFileLoadFilePostHook(ChaFile __instance, bool __result, BinaryReader br)
			{
				if (__result && !cardReadEventCalled)
				{
					internalCharaDictionary.Set(__instance, new Dictionary<string, PluginData>());
					CardReadEvent(__instance);
				}
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(ChaFile), "SaveFile", new Type[]
			{
				typeof(BinaryWriter),
				typeof(bool)
			})]
			private static void ChaFileSaveFilePreHook(ChaFile __instance)
			{
				CardWriteEvent(__instance);
			}

			private static void ChaFileSaveFileHook(ChaFile file, BlockHeader header, ref long[] array3)
			{
				Dictionary<string, PluginData> allExtendedData = GetAllExtendedData(file);
				if (allExtendedData == null)
				{
					currentlySavingData = null;
					return;
				}
				List<string> list = new List<string>();
				foreach (KeyValuePair<string, PluginData> item2 in allExtendedData)
				{
					if (item2.Value == null)
					{
						list.Add(item2.Key);
					}
				}
				foreach (string item3 in list)
				{
					allExtendedData.Remove(item3);
				}
				currentlySavingData = MessagePackSerializer.Serialize(allExtendedData);
				long pos = array3.Sum();
				long num = currentlySavingData.LongLength;
				Array.Resize(ref array3, array3.Length + 1);
				array3[array3.Length - 1] = num;
				BlockHeader.Info item = new BlockHeader.Info
				{
					name = Marker,
					version = DataVersion.ToString(),
					pos = pos,
					size = num
				};
				header.lstInfo.Add(item);
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFile), "SaveFile", new Type[]
			{
				typeof(BinaryWriter),
				typeof(bool)
			})]
			private static void ChaFileSaveFilePostHook(bool __result, BinaryWriter bw)
			{
				if (__result && currentlySavingData != null)
				{
					bw.Write(currentlySavingData);
				}
			}

			[HarmonyTranspiler]
			[HarmonyPatch(typeof(ChaFile), "SaveFile", new Type[]
			{
				typeof(BinaryWriter),
				typeof(bool)
			})]
			private static IEnumerable<CodeInstruction> ChaFileSaveFileTranspiler(IEnumerable<CodeInstruction> instructions)
			{
				//IL_008d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0093: Expected O, but got Unknown
				//IL_009c: Unknown result type (might be due to invalid IL or missing references)
				//IL_00a2: Expected O, but got Unknown
				//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
				//IL_00b1: Expected O, but got Unknown
				//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
				//IL_00d7: Expected O, but got Unknown
				List<CodeInstruction> list = new List<CodeInstruction>(instructions);
				string blockHeader = "BlockHeader";
				int num = list.FindLastIndex((CodeInstruction instruction) => CheckNewObjTypeName(instruction, blockHeader));
				int num2 = list.FindIndex((CodeInstruction instruction) => instruction.opcode == OpCodes.Newarr && instruction.operand.ToString() == "System.Int64");
				LocalBuilder localBuilder = (LocalBuilder)list[num + 1].operand;
				LocalBuilder localBuilder2 = (LocalBuilder)list[num2 + 1].operand;
				list.InsertRange(num + 2, (IEnumerable<CodeInstruction>)(object)new CodeInstruction[4]
				{
					new CodeInstruction(OpCodes.Ldarg_0, (object)null),
					new CodeInstruction(OpCodes.Ldloc_S, (object)localBuilder),
					new CodeInstruction(OpCodes.Ldloca_S, (object)localBuilder2),
					new CodeInstruction(OpCodes.Call, (object)typeof(Hooks).GetMethod("ChaFileSaveFileHook", AccessTools.all))
				});
				return list;
			}

			[HarmonyTranspiler]
			[HarmonyPatch(typeof(ChaFileCoordinate), "LoadFile", new Type[] { typeof(Stream) })]
			private static IEnumerable<CodeInstruction> ChaFileCoordinateLoadTranspiler(IEnumerable<CodeInstruction> instructions)
			{
				bool set = false;
				List<CodeInstruction> instructionsList = instructions.ToList();
				for (int i = 0; i < instructionsList.Count; i++)
				{
					CodeInstruction inst = instructionsList[i];
					if (!set && inst.opcode == OpCodes.Ldc_I4_1 && instructionsList[i + 1].opcode == OpCodes.Stloc_3 && (instructionsList[i + 2].opcode == OpCodes.Leave || instructionsList[i + 2].opcode == OpCodes.Leave_S))
					{
						yield return new CodeInstruction(OpCodes.Ldarg_0, (object)null);
						yield return new CodeInstruction(OpCodes.Ldloc_0, (object)null);
						yield return new CodeInstruction(OpCodes.Call, (object)typeof(Hooks).GetMethod("ChaFileCoordinateLoadHook", AccessTools.all));
						set = true;
					}
					yield return inst;
				}
				if (!set)
				{
					throw new InvalidOperationException("Didn't find any matches");
				}
			}

			private static void ChaFileCoordinateLoadHook(ChaFileCoordinate coordinate, BinaryReader br)
			{
				try
				{
					string text = br.ReadString();
					int num = br.ReadInt32();
					int num2 = br.ReadInt32();
					if (text == Marker && num == DataVersion && num2 > 0)
					{
						if (!LoadEventsEnabled)
						{
							br.BaseStream.Seek(num2, SeekOrigin.Current);
							internalCoordinateDictionary.Set(coordinate, new Dictionary<string, PluginData>());
						}
						else
						{
							byte[] bytes = br.ReadBytes(num2);
							Dictionary<string, PluginData> value = MessagePackSerializer.Deserialize<Dictionary<string, PluginData>>(bytes);
							internalCoordinateDictionary.Set(coordinate, value);
						}
					}
					else
					{
						internalCoordinateDictionary.Set(coordinate, new Dictionary<string, PluginData>());
					}
				}
				catch (EndOfStreamException)
				{
					internalCoordinateDictionary.Set(coordinate, new Dictionary<string, PluginData>());
				}
				catch (InvalidOperationException)
				{
					internalCoordinateDictionary.Set(coordinate, new Dictionary<string, PluginData>());
				}
				CoordinateReadEvent(coordinate);
			}

			[HarmonyTranspiler]
			[HarmonyPatch(typeof(ChaFileCoordinate), "SaveFile", new Type[] { typeof(string) })]
			private static IEnumerable<CodeInstruction> ChaFileCoordinateSaveTranspiler(IEnumerable<CodeInstruction> instructions)
			{
				bool hooked = false;
				List<CodeInstruction> instructionsList = instructions.ToList();
				for (int i = 0; i < instructionsList.Count; i++)
				{
					CodeInstruction inst = instructionsList[i];
					yield return inst;
					if (!hooked && inst.opcode == OpCodes.Callvirt && (instructionsList[i + 1].opcode == OpCodes.Leave || instructionsList[i + 1].opcode == OpCodes.Leave_S))
					{
						yield return new CodeInstruction(OpCodes.Ldarg_0, (object)null);
						yield return new CodeInstruction(instructionsList[i - 2]);
						yield return new CodeInstruction(OpCodes.Call, (object)typeof(Hooks).GetMethod("ChaFileCoordinateSaveHook", AccessTools.all));
						hooked = true;
					}
				}
				if (!hooked)
				{
					throw new InvalidOperationException("Didn't find any matches");
				}
			}

			private static void ChaFileCoordinateSaveHook(ChaFileCoordinate file, BinaryWriter bw)
			{
				CoordinateWriteEvent(file);
				Logger.Log((LogLevel)32, (object)"Coordinate hook!");
				Dictionary<string, PluginData> allExtendedData = GetAllExtendedData(file);
				if (allExtendedData == null)
				{
					return;
				}
				List<string> list = new List<string>();
				foreach (KeyValuePair<string, PluginData> item in allExtendedData)
				{
					if (item.Value == null)
					{
						list.Add(item.Key);
					}
				}
				foreach (string item2 in list)
				{
					allExtendedData.Remove(item2);
				}
				byte[] array = MessagePackSerializer.Serialize(allExtendedData);
				bw.Write(Marker);
				bw.Write(DataVersion);
				bw.Write(array.Length);
				bw.Write(array);
			}

			private static bool CheckCallVirtName(CodeInstruction instruction, string name)
			{
				if (instruction.opcode == OpCodes.Callvirt)
				{
					return instruction.operand.GetType().GetProperty("Name", AccessTools.all).GetGetMethod()
						.Invoke(instruction.operand, null)
						.ToString()
						.ToString() == name;
				}
				return false;
			}

			private static bool CheckNewObjTypeName(CodeInstruction instruction, string name)
			{
				if (instruction.opcode == OpCodes.Newobj)
				{
					return instruction.operand.GetType().GetProperty("DeclaringType", AccessTools.all).GetGetMethod()
						.Invoke(instruction.operand, null)
						.ToString() == name;
				}
				return false;
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(CustomCharaFile), "Initialize")]
			private static void CustomScenePreHook()
			{
				LoadEventsEnabled = false;
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(CustomCharaFile), "Initialize")]
			private static void CustomScenePostHook()
			{
				LoadEventsEnabled = true;
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(CustomCoordinateFile), "Initialize")]
			private static void CustomCoordinatePreHook()
			{
				LoadEventsEnabled = false;
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(CustomCoordinateFile), "Initialize")]
			private static void CustomCoordinatePostHook()
			{
				LoadEventsEnabled = true;
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFileAccessory), "MemberInit")]
			private static void MemberInit(ChaFileAccessory __instance)
			{
				Traverse.Create((object)__instance).Property("ExtendedSaveData", (object[])null).SetValue((object)null);
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFileAccessory.PartsInfo), "MemberInit")]
			private static void MemberInit(ChaFileAccessory.PartsInfo __instance)
			{
				Traverse.Create((object)__instance).Property("ExtendedSaveData", (object[])null).SetValue((object)null);
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFileClothes), "MemberInit")]
			private static void MemberInit(ChaFileClothes __instance)
			{
				Traverse.Create((object)__instance).Property("ExtendedSaveData", (object[])null).SetValue((object)null);
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFileClothes.PartsInfo), "MemberInit")]
			private static void MemberInit(ChaFileClothes.PartsInfo __instance)
			{
				Traverse.Create((object)__instance).Property("ExtendedSaveData", (object[])null).SetValue((object)null);
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFileClothes.PartsInfo.ColorInfo), "MemberInit")]
			private static void MemberInit(ChaFileClothes.PartsInfo.ColorInfo __instance)
			{
				Traverse.Create((object)__instance).Property("ExtendedSaveData", (object[])null).SetValue((object)null);
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFileStatus), "MemberInit")]
			private static void MemberInit(ChaFileStatus __instance)
			{
				Traverse.Create((object)__instance).Property("ExtendedSaveData", (object[])null).SetValue((object)null);
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFileStatus), "Copy")]
			private static void Copy(ChaFileStatus __instance, ChaFileStatus src)
			{
				Traverse.Create((object)__instance).Property("ExtendedSaveData", (object[])null).SetValue(Traverse.Create((object)src).Property("ExtendedSaveData", (object[])null).GetValue());
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFileParameter), "MemberInit")]
			private static void MemberInit(ChaFileParameter __instance)
			{
				Traverse.Create((object)__instance).Property("ExtendedSaveData", (object[])null).SetValue((object)null);
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFileParameter), "Copy")]
			private static void Copy(ChaFileParameter __instance, ChaFileParameter src)
			{
				Traverse.Create((object)__instance).Property("ExtendedSaveData", (object[])null).SetValue(Traverse.Create((object)src).Property("ExtendedSaveData", (object[])null).GetValue());
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFileFace), "MemberInit")]
			private static void MemberInit(ChaFileFace __instance)
			{
				Traverse.Create((object)__instance).Property("ExtendedSaveData", (object[])null).SetValue((object)null);
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFileFace.PupilInfo), "MemberInit")]
			private static void MemberInit(ChaFileFace.PupilInfo __instance)
			{
				Traverse.Create((object)__instance).Property("ExtendedSaveData", (object[])null).SetValue((object)null);
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFileFace.PupilInfo), "Copy")]
			private static void Copy(ChaFileFace.PupilInfo __instance, ChaFileFace.PupilInfo src)
			{
				Traverse.Create((object)__instance).Property("ExtendedSaveData", (object[])null).SetValue(Traverse.Create((object)src).Property("ExtendedSaveData", (object[])null).GetValue());
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFileMakeup), "MemberInit")]
			private static void MemberInit(ChaFileMakeup __instance)
			{
				Traverse.Create((object)__instance).Property("ExtendedSaveData", (object[])null).SetValue((object)null);
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFileParameter.Attribute), "MemberInit")]
			private static void MemberInit(ChaFileParameter.Attribute __instance)
			{
				Traverse.Create((object)__instance).Property("ExtendedSaveData", (object[])null).SetValue((object)null);
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFileParameter.Awnser), "MemberInit")]
			private static void MemberInit(ChaFileParameter.Awnser __instance)
			{
				Traverse.Create((object)__instance).Property("ExtendedSaveData", (object[])null).SetValue((object)null);
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFileParameter.Denial), "MemberInit")]
			private static void MemberInit(ChaFileParameter.Denial __instance)
			{
				Traverse.Create((object)__instance).Property("ExtendedSaveData", (object[])null).SetValue((object)null);
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFileParameter.Attribute), "Copy")]
			private static void Copy(ChaFileParameter.Attribute __instance, ChaFileParameter.Attribute src)
			{
				Traverse.Create((object)__instance).Property("ExtendedSaveData", (object[])null).SetValue(Traverse.Create((object)src).Property("ExtendedSaveData", (object[])null).GetValue());
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFileParameter.Awnser), "Copy")]
			private static void Copy(ChaFileParameter.Awnser __instance, ChaFileParameter.Awnser src)
			{
				Traverse.Create((object)__instance).Property("ExtendedSaveData", (object[])null).SetValue(Traverse.Create((object)src).Property("ExtendedSaveData", (object[])null).GetValue());
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFileParameter.Denial), "Copy")]
			private static void Copy(ChaFileParameter.Denial __instance, ChaFileParameter.Denial src)
			{
				Traverse.Create((object)__instance).Property("ExtendedSaveData", (object[])null).SetValue(Traverse.Create((object)src).Property("ExtendedSaveData", (object[])null).GetValue());
			}
		}

		/// <summary> ImportEventHandler </summary>
		public delegate void ImportEventHandler(Dictionary<string, PluginData> importedExtendedData, Dictionary<int, int?> coordinateMapping);

		public enum GameNames
		{
			Unknown,
			Koikatsu,
			EmotionCreators,
			AIGirl,
			HoneySelect2,
			PlayHome,
			KoikatsuSunshine
		}

		/// <summary> SceneEventHandler </summary>
		public delegate void SceneEventHandler(string path);

		/// <summary> PoseEventHandler </summary>
		public delegate void PoseEventHandler(string poseName, PauseCtrl.FileInfo fileInfo, OCIChar ociChar, GameNames gameName);

		/// <summary> CardEventHandler </summary>
		public delegate void CardEventHandler(ChaFile file);

		/// <summary> CoordinateEventHandler </summary>
		public delegate void CoordinateEventHandler(ChaFileCoordinate file);

		internal static WeakKeyDictionary<WorldData, Dictionary<string, PluginData>> internalSaveDataDictionary = new WeakKeyDictionary<WorldData, Dictionary<string, PluginData>>();

		internal static Dictionary<string, PluginData> internalSceneDictionary = new Dictionary<string, PluginData>();

		internal static Dictionary<string, PluginData> internalPoseDictionary = new Dictionary<string, PluginData>();

		/// <summary>
		/// The current game, written to some ext save data to determine which game it was created in
		/// </summary>
		public static readonly GameNames GameName = GameNames.KoikatsuSunshine;

		/// <summary> Plugin GUID </summary>
		public const string GUID = "com.bepis.bepinex.extendedsave";

		/// <summary> Plugin name </summary>
		public const string PluginName = "Extended Save";

		/// <summary> Plugin version </summary>
		public const string Version = "20.0";

		internal static ManualLogSource Logger;

		/// <summary> Marker that indicates the extended save region on cards </summary>
		public static string Marker = "KKEx";

		/// <summary> Version of the extended save data on cards </summary>
		public static int DataVersion = 3;

		/// <summary>
		/// Whether extended data load events should be triggered. Temporarily disable it when extended data will never be used, for example loading lists of cards.
		/// </summary>
		public static bool LoadEventsEnabled = true;

		/// <summary>
		/// Name of the property injected in to classes by the patcher
		/// </summary>
		public const string ExtendedSaveDataPropertyName = "ExtendedSaveData";

		internal static WeakKeyDictionary<ChaFile, Dictionary<string, PluginData>> internalCharaDictionary = new WeakKeyDictionary<ChaFile, Dictionary<string, PluginData>>();

		internal static WeakKeyDictionary<ChaFileCoordinate, Dictionary<string, PluginData>> internalCoordinateDictionary = new WeakKeyDictionary<ChaFileCoordinate, Dictionary<string, PluginData>>();

		/// <summary> Register methods to trigger on save file being saved </summary>
		public static event SaveDataEventHandler SaveDataBeingSaved;

		/// <summary> Register methods to trigger on save file being loaded </summary>
		public static event SaveDataEventHandler SaveDataBeingLoaded;

		/// <summary>
		/// Contains all extended data read from the KK card. Key is data GUID. Convert your data and write it back to the dictionary to get it saved.
		/// coordinateMapping.Key is the index of KK coordinate, and Value is the new index of KKS coordinate. If Value is null the coordinate will be discarded.
		/// You can convert your data by simply switching coordinate IDs from Key to Value in your data. If Value is null you should remove data for that coordinate.
		/// </summary>
		public static event ImportEventHandler CardBeingImported;

		/// <summary> Register methods to trigger on scene being saved </summary>
		public static event SceneEventHandler SceneBeingSaved;

		/// <summary> Register methods to trigger on scene being loaded </summary>
		public static event SceneEventHandler SceneBeingLoaded;

		/// <summary> Register methods to trigger on scene being imported </summary>
		public static event SceneEventHandler SceneBeingImported;

		/// <summary> Register methods to trigger on pose being saved </summary>
		public static event PoseEventHandler PoseBeingSaved;

		/// <summary> Register methods to trigger on pose being loaded </summary>
		public static event PoseEventHandler PoseBeingLoaded;

		/// <summary> Register methods to trigger on card being saved </summary>
		public static event CardEventHandler CardBeingSaved;

		/// <summary> Register methods to trigger on card being loaded </summary>
		public static event CardEventHandler CardBeingLoaded;

		/// <summary> Register methods to trigger on coordinate being saved </summary>
		public static event CoordinateEventHandler CoordinateBeingSaved;

		/// <summary> Register methods to trigger on coordinate being loaded </summary>
		public static event CoordinateEventHandler CoordinateBeingLoaded;

		/// <summary>
		/// Get a dictionary of ID, PluginData containing all extended data for a SaveData
		/// </summary>
		/// <param name="saveData">SaveData for which to get extended data</param>
		/// <returns>Dictionary of ID, PluginData</returns>
		public static Dictionary<string, PluginData> GetAllExtendedData(WorldData saveData)
		{
			return internalSaveDataDictionary.Get(saveData);
		}

		/// <summary>
		/// Get PluginData for a SaveData for the specified extended save data ID
		/// </summary>
		/// <param name="saveData">SaveData for which to get extended save file</param>
		/// <param name="id">ID of the data saved to the save file</param>
		/// <returns>PluginData</returns>
		public static PluginData GetExtendedDataById(WorldData saveData, string id)
		{
			if (saveData == null || id == null)
			{
				return null;
			}
			Dictionary<string, PluginData> dictionary = internalSaveDataDictionary.Get(saveData);
			if (dictionary == null || !dictionary.TryGetValue(id, out var value))
			{
				return null;
			}
			return value;
		}

		/// <summary>
		/// Set PluginData for a SaveData for the specified extended save data ID
		/// </summary>
		/// <param name="saveData">SaveData for which to set extended data</param>
		/// <param name="id">ID of the data to be saved to the save file</param>
		/// <param name="extendedFormatData">PluginData to save to the save file</param>
		public static void SetExtendedDataById(WorldData saveData, string id, PluginData extendedFormatData)
		{
			Dictionary<string, PluginData> dictionary = internalSaveDataDictionary.Get(saveData);
			if (dictionary == null)
			{
				dictionary = new Dictionary<string, PluginData>();
				internalSaveDataDictionary.Set(saveData, dictionary);
			}
			dictionary[id] = extendedFormatData;
		}

		internal static void SaveDataWriteEvent(WorldData saveData)
		{
			if (ExtendedSave.SaveDataBeingSaved == null)
			{
				return;
			}
			Delegate[] invocationList = ExtendedSave.SaveDataBeingSaved.GetInvocationList();
			foreach (Delegate obj in invocationList)
			{
				SaveDataEventHandler saveDataEventHandler = (SaveDataEventHandler)obj;
				try
				{
					saveDataEventHandler(saveData);
				}
				catch (Exception arg)
				{
					Logger.LogError((object)string.Format("Subscriber crash in {0}.{1} - {2}", "ExtendedSave", "SaveDataBeingSaved", arg));
				}
			}
		}

		internal static void SaveDataReadEvent(WorldData saveData)
		{
			if (ExtendedSave.SaveDataBeingLoaded == null)
			{
				return;
			}
			Delegate[] invocationList = ExtendedSave.SaveDataBeingLoaded.GetInvocationList();
			foreach (Delegate obj in invocationList)
			{
				SaveDataEventHandler saveDataEventHandler = (SaveDataEventHandler)obj;
				try
				{
					saveDataEventHandler(saveData);
				}
				catch (Exception arg)
				{
					Logger.LogError((object)string.Format("Subscriber crash in {0}.{1} - {2}", "ExtendedSave", "SaveDataBeingLoaded", arg));
				}
			}
		}

		private static void CardImportEvent(Dictionary<string, PluginData> data, Dictionary<int, int?> coordinateMapping)
		{
			if (ExtendedSave.CardBeingImported == null)
			{
				return;
			}
			Delegate[] invocationList = ExtendedSave.CardBeingImported.GetInvocationList();
			foreach (Delegate obj in invocationList)
			{
				ImportEventHandler importEventHandler = (ImportEventHandler)obj;
				try
				{
					importEventHandler(data, coordinateMapping);
				}
				catch (Exception arg)
				{
					Logger.Log((LogLevel)2, (object)string.Format("Subscriber crash in {0}.{1} - {2}", "ExtendedSave", "CardBeingLoaded", arg));
				}
			}
		}

		/// <summary>
		/// Get PluginData for a scene for the specified extended save data ID
		/// </summary>
		/// <param name="id">ID of the data saved to the card</param>
		/// <returns>PluginData</returns>
		public static PluginData GetSceneExtendedDataById(string id)
		{
			if (id == null || !internalSceneDictionary.TryGetValue(id, out var value))
			{
				return null;
			}
			return value;
		}

		/// <summary>
		/// Set PluginData for a scene for the specified extended save data ID
		/// </summary>
		/// <param name="id">ID of the data to be saved to the card</param>
		/// <param name="extendedFormatData">PluginData to save to the card</param>
		public static void SetSceneExtendedDataById(string id, PluginData extendedFormatData)
		{
			internalSceneDictionary[id] = extendedFormatData;
		}

		/// <summary>
		/// Get PluginData for a pose for the specified extended save data ID
		/// </summary>
		/// <param name="id">ID of the data saved to the card</param>
		/// <returns>PluginData</returns>
		public static PluginData GetPoseExtendedDataById(string id)
		{
			if (id == null || !internalPoseDictionary.TryGetValue(id, out var value))
			{
				return null;
			}
			return value;
		}

		/// <summary>
		/// Set PluginData for a pose for the specified extended save data ID
		/// </summary>
		/// <param name="id">ID of the data to be saved to the card</param>
		/// <param name="extendedFormatData">PluginData to save to the card</param>
		public static void SetPoseExtendedDataById(string id, PluginData extendedFormatData)
		{
			internalPoseDictionary[id] = extendedFormatData;
		}

		internal static void SceneWriteEvent(string path)
		{
			if (ExtendedSave.SceneBeingSaved == null)
			{
				return;
			}
			Delegate[] invocationList = ExtendedSave.SceneBeingSaved.GetInvocationList();
			foreach (Delegate obj in invocationList)
			{
				SceneEventHandler sceneEventHandler = (SceneEventHandler)obj;
				try
				{
					sceneEventHandler(path);
				}
				catch (Exception arg)
				{
					Logger.LogError((object)string.Format("Subscriber crash in {0}.{1} - {2}", "ExtendedSave", "SceneBeingSaved", arg));
				}
			}
		}

		internal static void SceneReadEvent(string path)
		{
			if (ExtendedSave.SceneBeingLoaded == null)
			{
				return;
			}
			Delegate[] invocationList = ExtendedSave.SceneBeingLoaded.GetInvocationList();
			foreach (Delegate obj in invocationList)
			{
				SceneEventHandler sceneEventHandler = (SceneEventHandler)obj;
				try
				{
					sceneEventHandler(path);
				}
				catch (Exception arg)
				{
					Logger.LogError((object)string.Format("Subscriber crash in {0}.{1} - {2}", "ExtendedSave", "SceneBeingLoaded", arg));
				}
			}
		}

		internal static void SceneImportEvent(string path)
		{
			if (ExtendedSave.SceneBeingImported == null)
			{
				return;
			}
			Delegate[] invocationList = ExtendedSave.SceneBeingImported.GetInvocationList();
			foreach (Delegate obj in invocationList)
			{
				SceneEventHandler sceneEventHandler = (SceneEventHandler)obj;
				try
				{
					sceneEventHandler(path);
				}
				catch (Exception arg)
				{
					Logger.LogError((object)string.Format("Subscriber crash in {0}.{1} - {2}", "ExtendedSave", "SceneBeingImported", arg));
				}
			}
		}

		internal static void PoseWriteEvent(string poseName, PauseCtrl.FileInfo fileInfo, OCIChar ociChar, GameNames gameName)
		{
			if (ExtendedSave.PoseBeingSaved == null)
			{
				return;
			}
			Delegate[] invocationList = ExtendedSave.PoseBeingSaved.GetInvocationList();
			foreach (Delegate obj in invocationList)
			{
				PoseEventHandler poseEventHandler = (PoseEventHandler)obj;
				try
				{
					poseEventHandler(poseName, fileInfo, ociChar, gameName);
				}
				catch (Exception arg)
				{
					Logger.LogError((object)string.Format("Subscriber crash in {0}.{1} - {2}", "ExtendedSave", "PoseBeingSaved", arg));
				}
			}
		}

		internal static void PoseReadEvent(string poseName, PauseCtrl.FileInfo fileInfo, OCIChar ociChar, GameNames gameName)
		{
			if (ExtendedSave.PoseBeingLoaded == null)
			{
				return;
			}
			Delegate[] invocationList = ExtendedSave.PoseBeingLoaded.GetInvocationList();
			foreach (Delegate obj in invocationList)
			{
				PoseEventHandler poseEventHandler = (PoseEventHandler)obj;
				try
				{
					poseEventHandler(poseName, fileInfo, ociChar, gameName);
				}
				catch (Exception arg)
				{
					Logger.LogError((object)string.Format("Subscriber crash in {0}.{1} - {2}", "ExtendedSave", "PoseBeingLoaded", arg));
				}
			}
		}

		internal void Awake()
		{
			Logger = ((BaseUnityPlugin)this).Logger;
			Hooks.InstallHooks();
		}

		/// <summary>
		/// Get a dictionary of ID, PluginData containing all extended data for a ChaFile
		/// </summary>
		/// <param name="file">ChaFile for which to get extended data</param>
		/// <returns>Dictionary of ID, PluginData</returns>
		public static Dictionary<string, PluginData> GetAllExtendedData(ChaFile file)
		{
			return internalCharaDictionary.Get(file);
		}

		/// <summary>
		/// Get PluginData for a ChaFile for the specified extended save data ID
		/// </summary>
		/// <param name="file">ChaFile for which to get extended data</param>
		/// <param name="id">ID of the data saved to the card</param>
		/// <returns>PluginData</returns>
		public static PluginData GetExtendedDataById(ChaFile file, string id)
		{
			if (file == null || id == null)
			{
				return null;
			}
			Dictionary<string, PluginData> dictionary = internalCharaDictionary.Get(file);
			if (dictionary == null || !dictionary.TryGetValue(id, out var value))
			{
				return null;
			}
			return value;
		}

		/// <summary>
		/// Set PluginData for a ChaFile for the specified extended save data ID
		/// </summary>
		/// <param name="file">ChaFile for which to set extended data</param>
		/// <param name="id">ID of the data to be saved to the card</param>
		/// <param name="extendedFormatData">PluginData to save to the card</param>
		public static void SetExtendedDataById(ChaFile file, string id, PluginData extendedFormatData)
		{
			Dictionary<string, PluginData> dictionary = internalCharaDictionary.Get(file);
			if (dictionary == null)
			{
				dictionary = new Dictionary<string, PluginData>();
				internalCharaDictionary.Set(file, dictionary);
			}
			dictionary[id] = extendedFormatData;
		}

		/// <summary>
		/// Get a dictionary of ID, PluginData containing all extended data for a ChaFileCoordinate
		/// </summary>
		/// <param name="file">ChaFileCoordinate for which to get extended data</param>
		/// <returns>Dictionary of ID, PluginData</returns>
		public static Dictionary<string, PluginData> GetAllExtendedData(ChaFileCoordinate file)
		{
			return internalCoordinateDictionary.Get(file);
		}

		/// <summary>
		/// Get PluginData for a ChaFileCoordinate for the specified extended save data ID
		/// </summary>
		/// <param name="file">ChaFileCoordinate for which to get extended data</param>
		/// <param name="id">ID of the data saved to the card</param>
		/// <returns>PluginData</returns>
		public static PluginData GetExtendedDataById(ChaFileCoordinate file, string id)
		{
			if (file == null || id == null)
			{
				return null;
			}
			Dictionary<string, PluginData> dictionary = internalCoordinateDictionary.Get(file);
			if (dictionary == null || !dictionary.TryGetValue(id, out var value))
			{
				return null;
			}
			return value;
		}

		/// <summary>
		/// Set PluginData for a ChaFileCoordinate for the specified extended save data ID
		/// </summary>
		/// <param name="file">ChaFileCoordinate for which to set extended data</param>
		/// <param name="id">ID of the data to be saved to the card</param>
		/// <param name="extendedFormatData">PluginData to save to the card</param>
		public static void SetExtendedDataById(ChaFileCoordinate file, string id, PluginData extendedFormatData)
		{
			Dictionary<string, PluginData> dictionary = internalCoordinateDictionary.Get(file);
			if (dictionary == null)
			{
				dictionary = new Dictionary<string, PluginData>();
				internalCoordinateDictionary.Set(file, dictionary);
			}
			dictionary[id] = extendedFormatData;
		}

		internal static byte[] MessagePackSerialize<T>(T obj)
		{
			try
			{
				return MessagePackSerializer.Serialize(obj, StandardResolver.Instance);
			}
			catch (FormatterNotRegisteredException)
			{
				return MessagePackSerializer.Serialize(obj, ContractlessStandardResolver.Instance);
			}
			catch (InvalidOperationException)
			{
				Logger.LogWarning((object)"Only primitive types are supported. Serialize your data first.");
				throw;
			}
		}

		internal static T MessagePackDeserialize<T>(byte[] obj)
		{
			try
			{
				return MessagePackSerializer.Deserialize<T>(obj, StandardResolver.Instance);
			}
			catch (FormatterNotRegisteredException)
			{
				return MessagePackSerializer.Deserialize<T>(obj, ContractlessStandardResolver.Instance);
			}
			catch (InvalidOperationException)
			{
				Logger.LogWarning((object)"Only primitive types are supported. Serialize your data first.");
				throw;
			}
		}

		internal static void CardWriteEvent(ChaFile file)
		{
			if (ExtendedSave.CardBeingSaved == null)
			{
				return;
			}
			Delegate[] invocationList = ExtendedSave.CardBeingSaved.GetInvocationList();
			foreach (Delegate obj in invocationList)
			{
				CardEventHandler cardEventHandler = (CardEventHandler)obj;
				try
				{
					cardEventHandler(file);
				}
				catch (Exception arg)
				{
					Logger.LogError((object)string.Format("Subscriber crash in {0}.{1} - {2}", "ExtendedSave", "CardBeingSaved", arg));
				}
			}
		}

		internal static void CardReadEvent(ChaFile file)
		{
			if (!LoadEventsEnabled || ExtendedSave.CardBeingLoaded == null)
			{
				return;
			}
			Delegate[] invocationList = ExtendedSave.CardBeingLoaded.GetInvocationList();
			foreach (Delegate obj in invocationList)
			{
				CardEventHandler cardEventHandler = (CardEventHandler)obj;
				try
				{
					cardEventHandler(file);
				}
				catch (Exception arg)
				{
					Logger.LogError((object)string.Format("Subscriber crash in {0}.{1} - {2}", "ExtendedSave", "CardBeingLoaded", arg));
				}
			}
		}

		internal static void CoordinateWriteEvent(ChaFileCoordinate file)
		{
			if (ExtendedSave.CoordinateBeingSaved == null)
			{
				return;
			}
			Delegate[] invocationList = ExtendedSave.CoordinateBeingSaved.GetInvocationList();
			foreach (Delegate obj in invocationList)
			{
				CoordinateEventHandler coordinateEventHandler = (CoordinateEventHandler)obj;
				try
				{
					coordinateEventHandler(file);
				}
				catch (Exception arg)
				{
					Logger.LogError((object)string.Format("Subscriber crash in {0}.{1} - {2}", "ExtendedSave", "CoordinateBeingSaved", arg));
				}
			}
		}

		internal static void CoordinateReadEvent(ChaFileCoordinate file)
		{
			if (!LoadEventsEnabled || ExtendedSave.CoordinateBeingLoaded == null)
			{
				return;
			}
			Delegate[] invocationList = ExtendedSave.CoordinateBeingLoaded.GetInvocationList();
			foreach (Delegate obj in invocationList)
			{
				CoordinateEventHandler coordinateEventHandler = (CoordinateEventHandler)obj;
				try
				{
					coordinateEventHandler(file);
				}
				catch (Exception arg)
				{
					Logger.LogError((object)string.Format("Subscriber crash in {0}.{1} - {2}", "ExtendedSave", "CoordinateBeingLoaded", arg));
				}
			}
		}
	}
	public static class Extensions
	{
		private static Dictionary<string, PluginData> GetExtendedData(object messagePackObject)
		{
			try
			{
				if (messagePackObject == null)
				{
					throw new ArgumentNullException("messagePackObject");
				}
				Traverse val = Traverse.Create(messagePackObject);
				Traverse val2 = val.Property("ExtendedSaveData", (object[])null);
				if (!val2.PropertyExists())
				{
					throw new NotSupportedException(string.Format("The type '{0}' does not have the '{1}' property. Make sure the extended save patcher is installed and working.", messagePackObject?.GetType(), "ExtendedSaveData"));
				}
				byte[] array = (byte[])val2.GetValue();
				if (array != null)
				{
					return ExtendedSave.MessagePackDeserialize<Dictionary<string, PluginData>>(array);
				}
			}
			catch (Exception ex)
			{
				ExtendedSave.Logger.LogError((object)ex);
			}
			return null;
		}

		private static bool GetExtendedData(object messagePackObject, string id, out PluginData data)
		{
			Dictionary<string, PluginData> extendedData = GetExtendedData(messagePackObject);
			if (extendedData != null && extendedData.TryGetValue(id, out data))
			{
				return true;
			}
			data = null;
			return false;
		}

		private static void SetExtendedData(object messagePackObject, string id, PluginData data)
		{
			Dictionary<string, PluginData> dictionary = GetExtendedData(messagePackObject);
			if (dictionary == null)
			{
				dictionary = new Dictionary<string, PluginData>();
			}
			if (data == null)
			{
				dictionary.Remove(id);
			}
			else
			{
				dictionary[id] = data;
			}
			try
			{
				byte[] value = ExtendedSave.MessagePackSerialize(dictionary);
				Traverse.Create(messagePackObject).Property("ExtendedSaveData", (object[])null).SetValue((object)value);
			}
			catch (Exception ex)
			{
				ExtendedSave.Logger.LogError((object)ex);
			}
		}

		internal static void TransferSerializedExtendedData(this object destination, object source)
		{
			try
			{
				if (source == null)
				{
					throw new ArgumentNullException("source");
				}
				Traverse val = Traverse.Create(source);
				Traverse val2 = val.Property("ExtendedSaveData", (object[])null);
				if (!val2.PropertyExists())
				{
					throw new NotSupportedException(string.Format("The type '{0}' does not have the '{1}' property. Make sure the extended save patcher is installed and working.", source?.GetType(), "ExtendedSaveData"));
				}
				object value = val2.GetValue();
				if (value != null)
				{
					Traverse.Create(destination).Property("ExtendedSaveData", (object[])null).SetValue(value);
				}
			}
			catch (Exception ex)
			{
				ExtendedSave.Logger.LogError((object)ex);
			}
		}

		public static Dictionary<string, PluginData> GetAllExtendedData(this ChaFileBody messagePackObject)
		{
			return GetExtendedData(messagePackObject);
		}

		public static bool TryGetExtendedDataById(this ChaFileBody messagePackObject, string id, out PluginData data)
		{
			return GetExtendedData(messagePackObject, id, out data);
		}

		public static void SetExtendedDataById(this ChaFileBody messagePackObject, string id, PluginData data)
		{
			SetExtendedData(messagePackObject, id, data);
		}

		public static Dictionary<string, PluginData> GetAllExtendedData(this ChaFileFace messagePackObject)
		{
			return GetExtendedData(messagePackObject);
		}

		public static bool TryGetExtendedDataById(this ChaFileFace messagePackObject, string id, out PluginData data)
		{
			return GetExtendedData(messagePackObject, id, out data);
		}

		public static void SetExtendedDataById(this ChaFileFace messagePackObject, string id, PluginData data)
		{
			SetExtendedData(messagePackObject, id, data);
		}

		public static Dictionary<string, PluginData> GetAllExtendedData(this ChaFileFace.PupilInfo messagePackObject)
		{
			return GetExtendedData(messagePackObject);
		}

		public static bool TryGetExtendedDataById(this ChaFileFace.PupilInfo messagePackObject, string id, out PluginData data)
		{
			return GetExtendedData(messagePackObject, id, out data);
		}

		public static void SetExtendedDataById(this ChaFileFace.PupilInfo messagePackObject, string id, PluginData data)
		{
			SetExtendedData(messagePackObject, id, data);
		}

		public static Dictionary<string, PluginData> GetAllExtendedData(this ChaFileHair messagePackObject)
		{
			return GetExtendedData(messagePackObject);
		}

		public static bool TryGetExtendedDataById(this ChaFileHair messagePackObject, string id, out PluginData data)
		{
			return GetExtendedData(messagePackObject, id, out data);
		}

		public static void SetExtendedDataById(this ChaFileHair messagePackObject, string id, PluginData data)
		{
			SetExtendedData(messagePackObject, id, data);
		}

		public static Dictionary<string, PluginData> GetAllExtendedData(this ChaFileHair.PartsInfo messagePackObject)
		{
			return GetExtendedData(messagePackObject);
		}

		public static bool TryGetExtendedDataById(this ChaFileHair.PartsInfo messagePackObject, string id, out PluginData data)
		{
			return GetExtendedData(messagePackObject, id, out data);
		}

		public static void SetExtendedDataById(this ChaFileHair.PartsInfo messagePackObject, string id, PluginData data)
		{
			SetExtendedData(messagePackObject, id, data);
		}

		public static Dictionary<string, PluginData> GetAllExtendedData(this ChaFileClothes messagePackObject)
		{
			return GetExtendedData(messagePackObject);
		}

		public static bool TryGetExtendedDataById(this ChaFileClothes messagePackObject, string id, out PluginData data)
		{
			return GetExtendedData(messagePackObject, id, out data);
		}

		public static void SetExtendedDataById(this ChaFileClothes messagePackObject, string id, PluginData data)
		{
			SetExtendedData(messagePackObject, id, data);
		}

		public static Dictionary<string, PluginData> GetAllExtendedData(this ChaFileClothes.PartsInfo messagePackObject)
		{
			return GetExtendedData(messagePackObject);
		}

		public static bool TryGetExtendedDataById(this ChaFileClothes.PartsInfo messagePackObject, string id, out PluginData data)
		{
			return GetExtendedData(messagePackObject, id, out data);
		}

		public static void SetExtendedDataById(this ChaFileClothes.PartsInfo messagePackObject, string id, PluginData data)
		{
			SetExtendedData(messagePackObject, id, data);
		}

		public static Dictionary<string, PluginData> GetAllExtendedData(this ChaFileClothes.PartsInfo.ColorInfo messagePackObject)
		{
			return GetExtendedData(messagePackObject);
		}

		public static bool TryGetExtendedDataById(this ChaFileClothes.PartsInfo.ColorInfo messagePackObject, string id, out PluginData data)
		{
			return GetExtendedData(messagePackObject, id, out data);
		}

		public static void SetExtendedDataById(this ChaFileClothes.PartsInfo.ColorInfo messagePackObject, string id, PluginData data)
		{
			SetExtendedData(messagePackObject, id, data);
		}

		public static Dictionary<string, PluginData> GetAllExtendedData(this ChaFileAccessory messagePackObject)
		{
			return GetExtendedData(messagePackObject);
		}

		public static bool TryGetExtendedDataById(this ChaFileAccessory messagePackObject, string id, out PluginData data)
		{
			return GetExtendedData(messagePackObject, id, out data);
		}

		public static void SetExtendedDataById(this ChaFileAccessory messagePackObject, string id, PluginData data)
		{
			SetExtendedData(messagePackObject, id, data);
		}

		public static Dictionary<string, PluginData> GetAllExtendedData(this ChaFileAccessory.PartsInfo messagePackObject)
		{
			return GetExtendedData(messagePackObject);
		}

		public static bool TryGetExtendedDataById(this ChaFileAccessory.PartsInfo messagePackObject, string id, out PluginData data)
		{
			return GetExtendedData(messagePackObject, id, out data);
		}

		public static void SetExtendedDataById(this ChaFileAccessory.PartsInfo messagePackObject, string id, PluginData data)
		{
			SetExtendedData(messagePackObject, id, data);
		}

		public static Dictionary<string, PluginData> GetAllExtendedData(this ChaFileStatus messagePackObject)
		{
			return GetExtendedData(messagePackObject);
		}

		public static bool TryGetExtendedDataById(this ChaFileStatus messagePackObject, string id, out PluginData data)
		{
			return GetExtendedData(messagePackObject, id, out data);
		}

		public static void SetExtendedDataById(this ChaFileStatus messagePackObject, string id, PluginData data)
		{
			SetExtendedData(messagePackObject, id, data);
		}

		public static Dictionary<string, PluginData> GetAllExtendedData(this ChaFileParameter messagePackObject)
		{
			return GetExtendedData(messagePackObject);
		}

		public static bool TryGetExtendedDataById(this ChaFileParameter messagePackObject, string id, out PluginData data)
		{
			return GetExtendedData(messagePackObject, id, out data);
		}

		public static void SetExtendedDataById(this ChaFileParameter messagePackObject, string id, PluginData data)
		{
			SetExtendedData(messagePackObject, id, data);
		}

		public static Dictionary<string, PluginData> GetAllExtendedData(this ChaFileMakeup messagePackObject)
		{
			return GetExtendedData(messagePackObject);
		}

		public static bool TryGetExtendedDataById(this ChaFileMakeup messagePackObject, string id, out PluginData data)
		{
			return GetExtendedData(messagePackObject, id, out data);
		}

		public static void SetExtendedDataById(this ChaFileMakeup messagePackObject, string id, PluginData data)
		{
			SetExtendedData(messagePackObject, id, data);
		}

		public static Dictionary<string, PluginData> GetAllExtendedData(this ChaFileParameter.Attribute messagePackObject)
		{
			return GetExtendedData(messagePackObject);
		}

		public static bool TryGetExtendedDataById(this ChaFileParameter.Attribute messagePackObject, string id, out PluginData data)
		{
			return GetExtendedData(messagePackObject, id, out data);
		}

		public static void SetExtendedDataById(this ChaFileParameter.Attribute messagePackObject, string id, PluginData data)
		{
			SetExtendedData(messagePackObject, id, data);
		}

		public static Dictionary<string, PluginData> GetAllExtendedData(this ChaFileParameter.Awnser messagePackObject)
		{
			return GetExtendedData(messagePackObject);
		}

		public static bool TryGetExtendedDataById(this ChaFileParameter.Awnser messagePackObject, string id, out PluginData data)
		{
			return GetExtendedData(messagePackObject, id, out data);
		}

		public static void SetExtendedDataById(this ChaFileParameter.Awnser messagePackObject, string id, PluginData data)
		{
			SetExtendedData(messagePackObject, id, data);
		}

		public static Dictionary<string, PluginData> GetAllExtendedData(this ChaFileParameter.Denial messagePackObject)
		{
			return GetExtendedData(messagePackObject);
		}

		public static bool TryGetExtendedDataById(this ChaFileParameter.Denial messagePackObject, string id, out PluginData data)
		{
			return GetExtendedData(messagePackObject, id, out data);
		}

		public static void SetExtendedDataById(this ChaFileParameter.Denial messagePackObject, string id, PluginData data)
		{
			SetExtendedData(messagePackObject, id, data);
		}

		public static Dictionary<string, PluginData> GetAllExtendedData(this ChaFileAbout messagePackObject)
		{
			return GetExtendedData(messagePackObject);
		}

		public static bool TryGetExtendedDataById(this ChaFileAbout messagePackObject, string id, out PluginData data)
		{
			return GetExtendedData(messagePackObject, id, out data);
		}

		public static void SetExtendedDataById(this ChaFileAbout messagePackObject, string id, PluginData data)
		{
			SetExtendedData(messagePackObject, id, data);
		}

		public static Dictionary<string, PluginData> GetAllExtendedData(this ChaFileParameter.Interest messagePackObject)
		{
			return GetExtendedData(messagePackObject);
		}

		public static bool TryGetExtendedDataById(this ChaFileParameter.Interest messagePackObject, string id, out PluginData data)
		{
			return GetExtendedData(messagePackObject, id, out data);
		}

		public static void SetExtendedDataById(this ChaFileParameter.Interest messagePackObject, string id, PluginData data)
		{
			SetExtendedData(messagePackObject, id, data);
		}
	}
	/// <summary>
	/// An object containing data saved to and loaded from cards.
	/// </summary>
	[MessagePackObject(false)]
	public class PluginData
	{
		/// <summary>
		/// Version of the plugin data saved to the card. Get or set this if ever your plugin data format changes and use it to differentiate.
		/// </summary>
		[Key(0)]
		public int version;

		/// <summary>
		/// Dictionary of objects saved to or loaded loaded from the card.
		/// </summary>
		[Key(1)]
		public Dictionary<string, object> data = new Dictionary<string, object>();
	}
}
