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
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepisPlugins;
using ChaCustom;
using Config;
using ExtensibleSaveFormat;
using HarmonyLib;
using ICSharpCode.SharpZipLib.Zip;
using Illusion.Elements.Xml;
using Illusion.Extensions;
using JetBrains.Annotations;
using Manager;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Win32;
using Shared;
using Sideloader.AutoResolver;
using Sideloader.ListLoader;
using Studio;
using UnityEngine;
using XUnity.ResourceRedirector;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: AssemblyProduct("Sideloader")]
[assembly: AssemblyTitle("BepisPlugins.Sideloader")]
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
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Expected O, but got Unknown
			//IL_0065: Expected O, but got Unknown
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Expected O, but got Unknown
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)SolidBoxTex == (Object)null)
			{
				Texture2D val = new Texture2D(1, 1, (TextureFormat)5, false);
				val.SetPixel(0, 0, new Color(0.4f, 0.4f, 0.4f));
				val.Apply();
				SolidBoxTex = val;
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
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			//IL_0065: Expected O, but got Unknown
			//IL_006a: Expected O, but got Unknown
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Expected O, but got Unknown
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)TransparentBoxTex == (Object)null)
			{
				Texture2D val = new Texture2D(1, 1, (TextureFormat)5, false);
				val.SetPixel(0, 0, new Color(0.4f, 0.4f, 0.4f, 0.7f));
				val.Apply();
				TransparentBoxTex = val;
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
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			if (((Rect)(ref eatRect)).Contains(new Vector2(Input.mousePosition.x, (float)Screen.height - Input.mousePosition.y)))
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
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_008e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
			//IL_0112: Unknown result type (might be due to invalid IL or missing references)
			//IL_0143: Unknown result type (might be due to invalid IL or missing references)
			//IL_0149: Unknown result type (might be due to invalid IL or missing references)
			//IL_014f: Unknown result type (might be due to invalid IL or missing references)
			//IL_015d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0163: Unknown result type (might be due to invalid IL or missing references)
			Color textColor = style.normal.textColor;
			Color color = GUI.color;
			style.normal.textColor = outlineColor;
			GUI.color = outlineColor;
			Rect val = rect;
			((Rect)(ref rect)).x = ((Rect)(ref rect)).x - (float)outlineThickness;
			((Rect)(ref rect)).y = ((Rect)(ref rect)).y - (float)outlineThickness;
			float x;
			while (true)
			{
				x = ((Rect)(ref rect)).x;
				((Rect)(ref rect)).x = x + 1f;
				if (!(x < ((Rect)(ref val)).x + (float)outlineThickness))
				{
					break;
				}
				GUI.Label(rect, text, style);
			}
			x = ((Rect)(ref rect)).x;
			((Rect)(ref rect)).x = x - 1f;
			while (true)
			{
				x = ((Rect)(ref rect)).y;
				((Rect)(ref rect)).y = x + 1f;
				if (!(x < ((Rect)(ref val)).y + (float)outlineThickness))
				{
					break;
				}
				GUI.Label(rect, text, style);
			}
			x = ((Rect)(ref rect)).y;
			((Rect)(ref rect)).y = x - 1f;
			while (true)
			{
				x = ((Rect)(ref rect)).x;
				((Rect)(ref rect)).x = x - 1f;
				if (!(x > ((Rect)(ref val)).x - (float)outlineThickness))
				{
					break;
				}
				GUI.Label(rect, text, style);
			}
			x = ((Rect)(ref rect)).x;
			((Rect)(ref rect)).x = x + 1f;
			while (true)
			{
				x = ((Rect)(ref rect)).y;
				((Rect)(ref rect)).y = x - 1f;
				if (!(x > ((Rect)(ref val)).y - (float)outlineThickness))
				{
					break;
				}
				GUI.Label(rect, text, style);
			}
			style.normal.textColor = txtColor;
			GUI.color = txtColor;
			GUI.Label(val, text, style);
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
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_008f: Unknown result type (might be due to invalid IL or missing references)
			Color textColor = style.normal.textColor;
			style.normal.textColor = shadowColor;
			((Rect)(ref rect)).x = ((Rect)(ref rect)).x + shadowOffset.x;
			((Rect)(ref rect)).y = ((Rect)(ref rect)).y + shadowOffset.y;
			GUI.Label(rect, content, style);
			style.normal.textColor = txtColor;
			((Rect)(ref rect)).x = ((Rect)(ref rect)).x - shadowOffset.x;
			((Rect)(ref rect)).y = ((Rect)(ref rect)).y - shadowOffset.y;
			GUI.Label(rect, content, style);
			style.normal.textColor = textColor;
		}

		public static void DrawLayoutLabelWithShadow(GUIContent content, GUIStyle style, Color txtColor, Color shadowColor, Vector2 direction, params GUILayoutOption[] options)
		{
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			DrawLabelWithShadow(GUILayoutUtility.GetRect(content, style, options), content, style, txtColor, shadowColor, direction);
		}

		public static bool DrawButtonWithShadow(Rect r, GUIContent content, GUIStyle style, float shadowAlpha, Vector2 direction)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Expected O, but got Unknown
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			GUIStyle val = new GUIStyle(style);
			val.normal.background = null;
			val.hover.background = null;
			val.active.background = null;
			bool result = GUI.Button(r, content, style);
			Color txtColor = (((Rect)(ref r)).Contains(Event.current.mousePosition) ? val.hover.textColor : val.normal.textColor);
			DrawLabelWithShadow(r, content, val, txtColor, new Color(0f, 0f, 0f, shadowAlpha), direction);
			return result;
		}

		public static bool DrawLayoutButtonWithShadow(GUIContent content, GUIStyle style, float shadowAlpha, Vector2 direction, params GUILayoutOption[] options)
		{
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
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

		public static implicit operator T(Shared.LazyCustom<T> lazy)
		{
			return lazy.Instance;
		}

		public static Shared.LazyCustom<TClass> Create<TClass>() where TClass : new()
		{
			return new Shared.LazyCustom<TClass>(() => new TClass());
		}

		public static Shared.LazyCustom<TClass> Create<TClass>(Func<TClass> factory)
		{
			return new Shared.LazyCustom<TClass>(factory);
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
namespace Sideloader
{
	/// <summary>
	/// Allows for loading mods in .zip format from the mods folder and automatically resolves ID conflicts.
	/// </summary>
	[BepInProcess("KoikatsuSunshine")]
	[BepInProcess("KoikatsuSunshine_VR")]
	[BepInProcess("CharaStudio")]
	[BepInDependency("com.bepis.bepinex.extendedsave", "20.0")]
	[BepInDependency("gravydevsupreme.xunity.resourceredirector", "1.2.0")]
	[BepInPlugin("com.bepis.bepinex.sideloader", "Sideloader", "20.0")]
	public class Sideloader : BaseUnityPlugin
	{
		/// <summary>
		/// The EX 0408 and some later updates introduced filename checks of list files.
		/// If the name isn't a number, it is now ignored. It was done to sort list files by their number.
		/// These patches sort the files in a compatible way while removing the filename limitation.
		/// GameInfo.LoadInfo doesn't need fixing since the filename check was always there.
		/// Program.FindADVBundleFilePath doesn't need fixing since no mods put bundles inside adv/scenario
		/// </summary>
		internal static class KKSEX0408fix
		{
			private static class NativeMethods
			{
				/// <summary>
				/// String comparer that is equivalent to the one used by Windows Explorer to sort files (e.g. 2 will go before 10, unlike normal compare).
				/// </summary>
				[DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
				public static extern int StrCmpLogicalW(string x, string y);
			}

			public static void ApplyIfNeeded(Harmony harmony)
			{
				//IL_0033: Unknown result type (might be due to invalid IL or missing references)
				//IL_0041: Expected O, but got Unknown
				//IL_0078: Unknown result type (might be due to invalid IL or missing references)
				//IL_0084: Expected O, but got Unknown
				//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
				//IL_00ba: Expected O, but got Unknown
				MethodInfo methodInfo = AccessTools.PropertyGetter(typeof(FileListInfo), "PathList");
				if (methodInfo != null)
				{
					try
					{
						harmony.Patch((MethodBase)methodInfo, new HarmonyMethod(typeof(KKSEX0408fix), "StudioInfoPathListOverride", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
						harmony.Patch((MethodBase)AccessTools.Constructor(typeof(FileListInfo), new Type[1] { typeof(List<string>) }, false), (HarmonyMethod)null, (HarmonyMethod)null, new HarmonyMethod(typeof(KKSEX0408fix), "StudioInfoRemoveDictTpl", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null);
						harmony.Patch((MethodBase)AccessTools.Method(typeof(ChaListControl), "LoadListInfoAll", (Type[])null, (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, new HarmonyMethod(typeof(KKSEX0408fix), "LoadListInfoAllSortReplaceTpl", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null);
					}
					catch (Exception ex)
					{
						Logger.LogWarning((object)("Failed to apply a fix for the KKS EX 0408 patch, many modded items might not work. Cause: " + ex));
					}
				}
			}

			private static IEnumerable<CodeInstruction> StudioInfoRemoveDictTpl(IEnumerable<CodeInstruction> instructions)
			{
				//IL_0002: Unknown result type (might be due to invalid IL or missing references)
				//IL_0031: Unknown result type (might be due to invalid IL or missing references)
				//IL_0037: Expected O, but got Unknown
				//IL_005f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0065: Expected O, but got Unknown
				//IL_006d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0073: Expected O, but got Unknown
				return new CodeMatcher(instructions, (ILGenerator)null).MatchForward(true, (CodeMatch[])(object)new CodeMatch[1]
				{
					new CodeMatch((OpCode?)OpCodes.Callvirt, (object)AccessTools.Method(typeof(SortedDictionary<int, string>), "Add", (Type[])null, (Type[])null), (string)null)
				}).ThrowIfInvalid("No SortedDictionary<Int32, string>.Add found").SetAndAdvance(OpCodes.Pop, (object)null)
					.Insert((CodeInstruction[])(object)new CodeInstruction[2]
					{
						new CodeInstruction(OpCodes.Pop, (object)null),
						new CodeInstruction(OpCodes.Pop, (object)null)
					})
					.Instructions();
			}

			private static IEnumerable<CodeInstruction> LoadListInfoAllSortReplaceTpl(IEnumerable<CodeInstruction> instructions)
			{
				//IL_0002: Unknown result type (might be due to invalid IL or missing references)
				//IL_0008: Expected O, but got Unknown
				//IL_0033: Unknown result type (might be due to invalid IL or missing references)
				//IL_0039: Expected O, but got Unknown
				//IL_006b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0071: Expected O, but got Unknown
				CodeMatcher val = new CodeMatcher(instructions, (ILGenerator)null);
				val.MatchForward(true, (CodeMatch[])(object)new CodeMatch[1]
				{
					new CodeMatch((OpCode?)OpCodes.Call, (object)AccessTools.Method(typeof(CommonLib), "GetAssetBundleNameListFromPath", (Type[])null, (Type[])null), (string)null)
				}).ThrowIfNotMatchForward("No OrderBy found", (CodeMatch[])(object)new CodeMatch[1]
				{
					new CodeMatch((Func<CodeInstruction, bool>)((CodeInstruction ci) => ci.opcode == OpCodes.Call && ((MethodInfo)ci.operand).Name == "OrderBy"), (string)null)
				}).Advance(1);
				while (val.Opcode != OpCodes.Stloc_3)
				{
					val.SetAndAdvance(OpCodes.Nop, (object)null);
				}
				val.ThrowIfInvalid("Stloc_3 not found, the IL has changed");
				val.Advance(-1);
				val.Set(OpCodes.Call, (object)AccessTools.Method(typeof(KKSEX0408fix), "CustomListSort", (Type[])null, (Type[])null));
				return val.Instructions();
			}

			private static List<string> CustomListSort(IEnumerable<string> originalList)
			{
				return AnonymousComparer.OrderBy<string, string>(originalList, (Func<string, string>)Path.GetFileNameWithoutExtension, (Func<string, string, int>)NativeMethods.StrCmpLogicalW).ToList();
			}

			private static bool StudioInfoPathListOverride(FileListInfo __instance, out List<string> __result)
			{
				__result = CustomListSort(__instance.dicFile.Keys);
				return false;
			}
		}

		internal static class Hooks
		{
			internal static void InstallHooks()
			{
				//IL_0061: Unknown result type (might be due to invalid IL or missing references)
				//IL_006e: Expected O, but got Unknown
				Harmony val = Harmony.CreateAndPatchAll(typeof(Hooks), "com.bepis.bepinex.sideloader");
				val.Patch((MethodBase)typeof(GlobalMethod).GetMethod("LoadAllFolder", AccessTools.all).MakeGenericMethod(typeof(Object)), (HarmonyMethod)null, new HarmonyMethod(typeof(Hooks).GetMethod("LoadAllFolderPostfix", AccessTools.all)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
				KKSEX0408fix.ApplyIfNeeded(val);
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(AssetBundleCheck), "IsFile")]
			private static void IsFileHook(string assetBundleName, ref bool __result)
			{
				if (!__result)
				{
					__result = IsSideloaderAB(assetBundleName);
				}
			}

			[HarmonyPostfix]
			[HarmonyPatch(/*Could not decode attribute arguments.*/)]
			private static void IsFileHook2(ref bool __result, AssetBundleData __instance)
			{
				if (!__result)
				{
					__result = IsSideloaderAB(__instance.bundle);
				}
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(CommonLib), "GetAssetBundleNameListFromPath")]
			private static void GetAssetBundleNameListFromPath(string path, List<string> __result)
			{
				if (path == "h/list/" || path == "map/list/mapinfo/" || path == "map/list/mapthumbnailinfo/")
				{
					foreach (string item in BundleManager.Bundles.Keys.Where((string x) => x.StartsWith(path)))
					{
						if (!__result.Contains(item))
						{
							__result.Add(item);
						}
					}
					return;
				}
				if (!(path == "custom/customscenelist/"))
				{
					return;
				}
				foreach (string item2 in BundleManager.Bundles.Keys.Where((string x) => x.StartsWith(path)))
				{
					if (!__result.Contains(item2))
					{
						__result.Add(item2);
					}
				}
			}

			/// <summary>
			/// The game gets from a list by index which will cause errors. Get them safely for sideloader items
			/// </summary>
			[HarmonyPrefix]
			[HarmonyPatch(typeof(CustomFacePaintLayoutPreset), "OnPush")]
			private static bool FacePaintOnPush(int index, CustomFacePaintLayoutPreset __instance)
			{
				//IL_0055: Unknown result type (might be due to invalid IL or missing references)
				if (index >= 100000000)
				{
					FacePaintPreset val = __instance.lstPreset.FirstOrDefault((FacePaintPreset p) => p.index == index);
					if (val == null)
					{
						return false;
					}
					__instance.cvsMakeup.UpdatePushFacePaintLayout(new Vector4(val.x, val.y, val.r, val.s));
					return false;
				}
				return true;
			}

			/// <summary>
			/// The game gets from a list by index which will cause errors. Get them safely for sideloader items
			/// </summary>
			[HarmonyPrefix]
			[HarmonyPatch(typeof(CustomMoleLayoutPreset), "OnPush")]
			private static bool OnPush(int index, CustomMoleLayoutPreset __instance)
			{
				//IL_0069: Unknown result type (might be due to invalid IL or missing references)
				if (index >= 100000000)
				{
					MolePreset val = __instance.lstPreset.FirstOrDefault((MolePreset p) => p.index == index);
					if (val == null)
					{
						return false;
					}
					ChaControl chaCtrl = Singleton<CustomBase>.Instance.chaCtrl;
					((ChaFile)((ChaInfo)chaCtrl).chaFile).custom.face.moleLayout = new Vector4(val.x, val.y, 0f, val.w);
					__instance.cvsMole.FuncUpdateMoleLayout();
					__instance.cvsMole.UpdateCustomUI();
					return false;
				}
				return true;
			}

			/// <summary>
			/// Patch for loading h/common/ stuff for Sideloader maps
			/// </summary>
			internal static void LoadAllFolderPostfix(string _findFolder, string _strLoadFile, ref List<Object> __result)
			{
				if (__result.Count != 0 || (!(_findFolder == "h/common/") && !(_findFolder == "vr/common/")))
				{
					return;
				}
				foreach (KeyValuePair<string, List<Shared.LazyCustom<AssetBundle>>> item in BundleManager.Bundles.Where((KeyValuePair<string, List<Shared.LazyCustom<AssetBundle>>> x) => x.Key.StartsWith(_findFolder)))
				{
					foreach (Shared.LazyCustom<AssetBundle> item2 in item.Value)
					{
						string[] allAssetNames = item2.Instance.GetAllAssetNames();
						foreach (string text in allAssetNames)
						{
							if (_strLoadFile.ToLower() == Path.GetFileNameWithoutExtension(text.ToLower()))
							{
								GameObject val = CommonLib.LoadAsset<GameObject>(item.Key, text, false, "", true);
								if (Object.op_Implicit((Object)(object)val))
								{
									__result.Add((Object)(object)val);
								}
							}
						}
					}
				}
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(Info), "LoadExcelData")]
			private static void LoadExcelDataPostfix(string _bundlePath, string _fileName, ref ExcelData __result)
			{
				//IL_0070: Unknown result type (might be due to invalid IL or missing references)
				//IL_0076: Expected O, but got Unknown
				//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
				//IL_0102: Unknown result type (might be due to invalid IL or missing references)
				//IL_010c: Expected O, but got Unknown
				//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
				//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
				//IL_00bb: Expected O, but got Unknown
				if (!Lists.ExternalStudioDataList.TryGetValue(_bundlePath, out var value))
				{
					return;
				}
				List<Lists.StudioListData> list = value.Where((Lists.StudioListData x) => x.FileNameWithoutExtension == _fileName).ToList();
				if (list.Count <= 0)
				{
					return;
				}
				bool flag = false;
				int count = list[0].Headers.Count;
				if ((Object)(object)__result == (Object)null)
				{
					__result = (ExcelData)ScriptableObject.CreateInstance(typeof(ExcelData));
				}
				else
				{
					flag = true;
				}
				foreach (Lists.StudioListData item3 in list)
				{
					if (!flag)
					{
						foreach (List<string> header in item3.Headers)
						{
							Param item = new Param
							{
								list = header
							};
							__result.list.Add(item);
						}
						flag = true;
					}
					foreach (List<string> entry in item3.Entries)
					{
						Param item2 = new Param
						{
							list = entry
						};
						__result.list.Add(item2);
					}
				}
				int num = 0;
				while (num < __result.list.Count)
				{
					int result;
					if (num <= count - 1)
					{
						num++;
					}
					else if (__result.list[num].list.Count == 0)
					{
						__result.list.RemoveAt(num);
					}
					else if (!int.TryParse(__result.list[num].list[0], out result))
					{
						__result.list.RemoveAt(num);
					}
					else
					{
						num++;
					}
				}
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(AssetBundleCheck), "GetAllFileName")]
			private static bool GetAllFileName(string _assetBundleName, ref string[] __result)
			{
				if (!Lists.ExternalStudioDataList.TryGetValue(_assetBundleName, out var value))
				{
					return true;
				}
				string[] array = value.Select((Lists.StudioListData y) => y.FileNameWithoutExtension.ToLower()).ToArray();
				if (array.Length != 0)
				{
					__result = array;
					return false;
				}
				return true;
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(Info), "FindAllAssetName")]
			private static bool FindAllAssetNamePrefix(string _bundlePath, string _regex, ref string[] __result)
			{
				if (!Lists.ExternalStudioDataList.TryGetValue(_bundlePath, out var value))
				{
					return true;
				}
				List<string> list = value.Select((Lists.StudioListData x) => x.FileNameWithoutExtension).ToList();
				if (list.Count > 0)
				{
					__result = list.Where((string x) => Regex.Match(x, _regex, RegexOptions.IgnoreCase).Success).ToArray();
					return false;
				}
				return true;
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(CommonLib), "GetAssetBundleNameListFromPath")]
			private static void GetAssetBundleNameListFromPathStudio(string path, List<string> __result)
			{
				if (!(path == "studio/info/"))
				{
					return;
				}
				foreach (string key in Lists.ExternalStudioDataList.Keys)
				{
					if (!__result.Contains(key))
					{
						__result.Add(key);
					}
				}
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(FileCheck), "Check")]
			internal static void FileCheck(string _path, ref bool __result, Dictionary<string, bool> ___dicConfirmed)
			{
				if (!__result && _path != null && BundleManager.Bundles.TryGetValue(_path, out var _))
				{
					__result = true;
					___dicConfirmed[_path] = true;
				}
			}

			[HarmonyFinalizer]
			[HarmonyPatch(/*Could not decode attribute arguments.*/)]
			internal static Exception InvalidMapLoadInfoCategoryFix(Exception __exception, MapLoadInfo __instance, List<string> _list)
			{
				//IL_0052: Unknown result type (might be due to invalid IL or missing references)
				//IL_005c: Expected O, but got Unknown
				if (__exception is FormatException)
				{
					int num = 1;
					((LoadCommonInfo)__instance).name = _list[num++];
					((FileInfo)__instance).bundlePath = _list[num++];
					((FileInfo)__instance).fileName = _list[num++];
					((FileInfo)__instance).manifest = NullCheck.SafeGet<string>((IReadOnlyList<string>)_list, num++);
					__instance.vanish = new FileInfo();
					__instance.vanish.bundlePath = NullCheck.SafeGet<string>((IReadOnlyList<string>)_list, num++);
					__instance.vanish.fileName = NullCheck.SafeGet<string>((IReadOnlyList<string>)_list, num++);
					Singleton<Info>.Instance.dicMapCategory[69133769] = "Invalid";
					__instance.category = 69133769;
					return null;
				}
				return __exception;
			}
		}

		internal static readonly string[] GameNameList = new string[3] { "koikatsu sunshine", "koikatu sunshine", "コイカツ sunshine" };

		/// <summary> Plugin GUID </summary>
		public const string GUID = "com.bepis.bepinex.sideloader";

		/// <summary> Plugin name </summary>
		public const string PluginName = "Sideloader";

		/// <summary> Plugin version </summary>
		public const string Version = "20.0";

		internal static ManualLogSource Logger;

		/// <summary> Dictionary of loaded zip file name and its zipmod metadata </summary>
		internal static readonly Dictionary<string, ZipmodInfo> Zipmods = new Dictionary<string, ZipmodInfo>();

		/// <summary> Dictionary of GUID and loaded manifest files </summary>
		public static readonly Dictionary<string, Manifest> Manifests = new Dictionary<string, Manifest>();

		/// <summary> Dictionary of GUID and loaded zip file name </summary>
		public static readonly Dictionary<string, string> ZipArchives = new Dictionary<string, string>();

		/// <summary> List of all loaded manifest files </summary>
		[Obsolete("Use Manifests or GetManifest")]
		public static List<Manifest> LoadedManifests;

		private static readonly Dictionary<string, ZipmodInfo> PngList = new Dictionary<string, ZipmodInfo>();

		private static readonly HashSet<string> PngFolderList = new HashSet<string>();

		private static readonly HashSet<string> PngFolderOnlyList = new HashSet<string>();

		private static readonly string _CacheName = "sideloader_zipmod_cache.bin";

		private static readonly string _CacheDirectory = Paths.CachePath;

		private static readonly string _CachePath = Path.Combine(_CacheDirectory, _CacheName);

		internal static readonly HashSet<string> StudioListResolveBlacklist = new HashSet<string> { "itemcategory", "animecategory", "voicecategory", "itemgroup", "animegroup", "voicegroup", "bone", "mapcategory" };

		/// <summary> Directory from which to load mods </summary>
		public static string ModsDirectory { get; } = Path.Combine(Paths.GameRootPath, "mods");


		internal static ConfigEntry<bool> MissingModWarning { get; private set; }

		internal static ConfigEntry<bool> DebugLogging { get; private set; }

		internal static ConfigEntry<bool> DebugLoggingResolveInfo { get; private set; }

		internal static ConfigEntry<bool> DebugLoggingModLoading { get; private set; }

		internal static ConfigEntry<bool> RandomizeSlotIds { get; private set; }

		internal static ConfigEntry<bool> KeepMissingAccessories { get; private set; }

		internal static ConfigEntry<bool> MigrationEnabled { get; private set; }

		internal static ConfigEntry<string> AdditionalModsDirectory { get; private set; }

		internal static ConfigEntry<bool> CachingEnabled { get; private set; }

		private static string FindKoiZipmodDir()
		{
			try
			{
				if (Directory.Exists(Path.Combine(Paths.GameRootPath, "mods\\Sideloader Modpack")))
				{
					return string.Empty;
				}
				using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Illusion\\Koikatu\\koikatu");
				if (registryKey?.GetValue("INSTALLDIR") is string path)
				{
					string text = Path.Combine(path, "mods\\Sideloader Modpack");
					if (Directory.Exists(text))
					{
						return text;
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError((object)"Crash when trying to find Koikatsu mods directory");
				Logger.LogError((object)ex);
			}
			return string.Empty;
		}

		[UsedImplicitly]
		private void Awake()
		{
			//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0105: Expected O, but got Unknown
			Logger = ((BaseUnityPlugin)this).Logger;
			Hooks.InstallHooks();
			UniversalAutoResolver.Hooks.InstallHooks();
			Lists.Hooks.InstallHooks();
			ResourceRedirection.EnableSyncOverAsyncAssetLoads();
			ResourceRedirection.EnableRedirectMissingAssetBundlesToEmptyAssetBundle(-1000);
			ResourceRedirection.EnableRandomizeCabIfConflict(-2000, false);
			ResourceRedirection.RegisterAsyncAndSyncAssetLoadingHook((Action<IAssetLoadingContext>)RedirectHook);
			ResourceRedirection.RegisterAsyncAndSyncAssetBundleLoadingHook((Action<IAssetBundleLoadingContext>)AssetBundleLoadingHook);
			MissingModWarning = ((BaseUnityPlugin)this).Config.Bind<bool>("Logging", "Show missing mod warnings", true, "Whether missing mod warnings will be displayed on screen. Messages will still be written to the log.");
			DebugLogging = ((BaseUnityPlugin)this).Config.Bind<bool>("Logging", "Debug logging", false, "Enable additional logging useful for debugging issues with Sideloader and sideloader mods.\nWarning: Will increase load and save times noticeably and will result in very large log sizes.");
			DebugLoggingResolveInfo = ((BaseUnityPlugin)this).Config.Bind<bool>("Logging", "Debug resolve info logging", false, "Enable verbose logging for debugging issues with Sideloader and sideloader mods.\nWarning: Will increase game start up time and will result in very large log sizes.");
			DebugLoggingModLoading = ((BaseUnityPlugin)this).Config.Bind<bool>("Logging", "Debug mod loading logging", false, "Enable verbose logging when loading mods.");
			RandomizeSlotIds = ((BaseUnityPlugin)this).Config.Bind<bool>("General", "Randomize Slot IDs", true, new ConfigDescription("Helps detect bugs in Sideloader and other plugins by making them more obvious. If false, the bugs might not affect current game instance but will suddenly pop up after updating the game or sending scenes/cards to another users.", (AcceptableValueBase)null, new object[1] { "Advanced" }));
			KeepMissingAccessories = ((BaseUnityPlugin)this).Config.Bind<bool>("General", "Keep missing accessories", false, "Missing accessories will be replaced by a default item with color and position information intact when loaded in the character maker.");
			MigrationEnabled = ((BaseUnityPlugin)this).Config.Bind<bool>("General", "Migration enabled", true, "Attempt to change the GUID and/or ID of mods based on the data configured in the manifest.xml. Helps keep backwards compatibility when updating mods.");
			AdditionalModsDirectory = ((BaseUnityPlugin)this).Config.Bind<string>("General", "Additional mods directory", FindKoiZipmodDir(), "Additional directory to load zipmods from.");
			CachingEnabled = ((BaseUnityPlugin)this).Config.Bind<bool>("General", "Cache zipmod metadata", true, "Drastically speeds up game startup speed, especially on slow HDDs, by not parsing zipmods unless they get changed. Disable to force sideloader to always read and parse all zipmods.");
			if (!Directory.Exists(ModsDirectory))
			{
				Logger.LogWarning((object)("Could not find the mods directory: " + ModsDirectory));
			}
			if (!NullCheck.IsNullOrWhiteSpace(AdditionalModsDirectory.Value) && !Directory.Exists(AdditionalModsDirectory.Value))
			{
				Logger.LogWarning((object)("Could not find the additional mods directory specified in config: " + AdditionalModsDirectory.Value));
			}
			LoadModsFromDirectories(ModsDirectory, AdditionalModsDirectory.Value);
		}

		private void LoadModsFromDirectories(params string[] modDirectories)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			List<ZipmodInfo> foundZipfiles = new List<ZipmodInfo>();
			ManualResetEvent waitHandle = new ManualResetEvent(initialState: false);
			ThreadPool.QueueUserWorkItem(delegate
			{
				try
				{
					foreach (string item in modDirectories.Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists))
					{
						Stopwatch stopwatch2 = Stopwatch.StartNew();
						int count = foundZipfiles.Count;
						string[] files = Directory.GetFiles(item, "*", SearchOption.AllDirectories);
						foreach (string text6 in files)
						{
							if (text6.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || text6.EndsWith(".zipmod", StringComparison.OrdinalIgnoreCase))
							{
								foundZipfiles.Add(new ZipmodInfo(text6));
							}
						}
						Logger.LogInfo((object)$"Found {foundZipfiles.Count - count} zip/zipmod files in directory [{item}] in {stopwatch2.ElapsedMilliseconds}ms");
					}
				}
				catch (Exception ex2)
				{
					Logger.LogError((object)("Crash while searching for zip/zipmod files! " + ex2));
				}
				finally
				{
					waitHandle.Set();
				}
			});
			Dictionary<string, ZipmodInfo> dictionary = LoadCache();
			waitHandle.WaitOne();
			Dictionary<string, List<ZipmodInfo>> groupedZipmodsToLoad = new Dictionary<string, List<ZipmodInfo>>();
			foreach (ZipmodInfo item2 in foundZipfiles)
			{
				dictionary.TryGetValue(item2.FileName, out var value);
				if (value != null && item2.FileSize == value.FileSize && item2.LastWriteTime == value.LastWriteTime)
				{
					Zipmods.Add(value.FileName, value);
					if (value.Error != null)
					{
						if (value.Error.StartsWith("Skipping"))
						{
							Logger.LogInfo((object)value.Error);
						}
						else
						{
							Logger.LogError((object)value.Error);
						}
					}
					else
					{
						AddZipmodToLoad(value);
					}
					continue;
				}
				try
				{
					if (dictionary.Count > 0 && DebugLogging.Value)
					{
						Logger.LogDebug((object)("Cache MISS for " + item2.FileName + "  -  " + ((value != null) ? "size/date changed" : "entry missing")));
					}
					Zipmods.Add(item2.FileName, item2);
					ZipFile zipFile = item2.GetZipFile();
					item2.Manifest = Manifest.LoadFromZip(zipFile);
					if (item2.Manifest.Games.Count != 0 && !item2.Manifest.Games.Select((string x) => x.ToLower()).Any(((IEnumerable<string>)GameNameList).Contains<string>))
					{
						throw new PlatformNotSupportedException();
					}
					item2.LoadAllLists();
					AddZipmodToLoad(item2);
				}
				catch (PlatformNotSupportedException)
				{
					string text = "Skipping archive \"" + item2.RelativeFileName + "\" because it's meant for " + string.Join(", ", item2.Manifest.Games.ToArray());
					Logger.LogInfo((object)text);
					item2.Error = text;
					item2.Dispose();
				}
				catch (Exception arg)
				{
					string text2 = $"Failed to load archive \"{item2.RelativeFileName}\" with error: {arg}";
					Logger.LogError((object)text2);
					item2.Error = text2;
					item2.Dispose();
				}
			}
			WriteCache();
			bool value2 = DebugLoggingModLoading.Value;
			StringBuilder stringBuilder = (value2 ? new StringBuilder(1000) : null);
			List<ResolveInfo> list = new List<ResolveInfo>();
			List<MigrationInfo> list2 = new List<MigrationInfo>();
			List<BundleLoadInfo> list3 = new List<BundleLoadInfo>();
			foreach (List<ZipmodInfo> item3 in from x in groupedZipmodsToLoad
				orderby x.Key
				select x.Value)
			{
				ZipmodInfo zipmodInfo2;
				if (item3.Count > 1)
				{
					List<ZipmodInfo> list4;
					try
					{
						IOrderedEnumerable<ZipmodInfo> source = (item3.All((ZipmodInfo x) => !string.IsNullOrEmpty(x.Manifest.Version)) ? item3.OrderByDescending((ZipmodInfo x) => x.Manifest.Version, new ManifestVersionComparer()).ThenByDescending((ZipmodInfo x) => x.FileName.Length) : item3.OrderByDescending((ZipmodInfo x) => x.LastWriteTime));
						list4 = source.ToList();
					}
					catch (Exception arg2)
					{
						Logger.LogError((object)string.Format("Failed to sort versions of [{0}]: [{1}] with error: {2}", item3[0].Manifest.GUID, string.Join("] , [", item3.Select((ZipmodInfo x) => x.Manifest.Version).ToArray()), arg2));
						list4 = item3;
					}
					zipmodInfo2 = list4[0];
					string text3 = string.Join(", ", (from x in list4.Skip(1)
						select "\"" + x.RelativeFileName + "\"").ToArray());
					Logger.LogWarning((object)("Multiple versions detected, only \"" + zipmodInfo2.RelativeFileName + "\" will be loaded. Skipped versions: " + text3));
					foreach (ZipmodInfo item4 in list4.Skip(1))
					{
						item4.Dispose();
					}
				}
				else
				{
					zipmodInfo2 = item3[0];
				}
				try
				{
					Manifest manifest = zipmodInfo2.Manifest;
					ZipArchives[manifest.GUID] = zipmodInfo2.FileName;
					Manifests[manifest.GUID] = manifest;
					list3.AddRange(zipmodInfo2.BundleInfos);
					AddAllLists(zipmodInfo2, list);
					BuildPngFolderList(zipmodInfo2);
					list2.AddRange(manifest.MigrationList);
					if (value2)
					{
						string text4 = manifest.Name?.Trim();
						string text5 = ((!string.IsNullOrEmpty(text4)) ? text4 : Path.GetFileName(zipmodInfo2.FileName));
						stringBuilder.AppendLine("Loaded " + text5 + " " + manifest.Version);
					}
					zipmodInfo2.Loaded = true;
				}
				catch (Exception arg3)
				{
					Logger.LogError((object)$"Failed to load archive \"{zipmodInfo2.RelativeFileName}\", state might be corrupted! Error: {arg3}");
				}
			}
			AddBundles(list3);
			UniversalAutoResolver.SetResolveInfos(list);
			UniversalAutoResolver.SetMigrationInfos(list2);
			BuildPngOnlyFolderList();
			LoadedManifests = Manifests.Values.AsEnumerable().ToList();
			if (value2)
			{
				Logger.LogInfo((object)$"List of loaded mods:\n{stringBuilder}");
			}
			Logger.LogInfo((object)$"Successfully loaded {Zipmods.Count((KeyValuePair<string, ZipmodInfo> x) => x.Value.Loaded)} mods out of {Zipmods.Count} archives in {stopwatch.ElapsedMilliseconds}ms");
			string[] array = (from x in Zipmods.Values
				where !x.Loaded
				select x.RelativeFileName).ToArray();
			if (array.Length != 0)
			{
				Logger.LogWarning((object)("Could not load " + array.Length + " mods, see previous warnings for more information. File names of skipped archives:\n" + string.Join(" | ", array)));
			}
			void AddZipmodToLoad(ZipmodInfo zipmodInfo)
			{
				if (zipmodInfo.Valid)
				{
					if (!groupedZipmodsToLoad.TryGetValue(zipmodInfo.Manifest.GUID, out var value3))
					{
						value3 = new List<ZipmodInfo>(1);
						groupedZipmodsToLoad[zipmodInfo.Manifest.GUID] = value3;
					}
					value3.Add(zipmodInfo);
				}
			}
		}

		private static void AddAllLists(ZipmodInfo zipmod, List<ResolveInfo> gatheredResolutionInfos)
		{
			Manifest manifest = zipmod.Manifest;
			foreach (ChaListData charaList in zipmod.CharaLists)
			{
				UniversalAutoResolver.GenerateResolutionInfo(manifest, charaList, gatheredResolutionInfos);
				Lists.ExternalDataList.Add(charaList);
			}
			foreach (Lists.StudioListData studioList in zipmod.StudioLists)
			{
				UniversalAutoResolver.GenerateStudioResolutionInfo(manifest, studioList);
				if (!Lists.ExternalStudioDataList.TryGetValue(studioList.AssetBundleName, out var value))
				{
					value = new List<Lists.StudioListData>();
					Lists.ExternalStudioDataList.Add(studioList.AssetBundleName, value);
				}
				value.Add(studioList);
			}
			foreach (Lists.StudioListData mapList in zipmod.MapLists)
			{
				Lists.AddExcelDataCSV(mapList);
			}
			foreach (Lists.StudioListData boneList in zipmod.BoneLists)
			{
				UniversalAutoResolver.GenerateStudioResolutionInfo(manifest, boneList);
				if (!Lists.ExternalStudioDataList.TryGetValue(boneList.AssetBundleName, out var value2))
				{
					value2 = new List<Lists.StudioListData>();
					Lists.ExternalStudioDataList.Add(boneList.AssetBundleName, value2);
				}
				value2.Add(boneList);
			}
		}

		/// <summary>
		/// Construct a list of all folders that contain a .png
		/// </summary>
		private static void BuildPngFolderList(ZipmodInfo zipmod)
		{
			foreach (string pngName in zipmod.PngNames)
			{
				if (PngList.ContainsKey(pngName))
				{
					if (DebugLoggingModLoading.Value)
					{
						Logger.LogWarning((object)("Duplicate .png asset detected! " + pngName + " in \"" + zipmod.RelativeFileName + "\""));
					}
				}
				else
				{
					PngList.Add(pngName, zipmod);
				}
				string text = pngName;
				text = text.Remove(0, text.IndexOf('/') + 1);
				text = text.Remove(text.LastIndexOf('/'));
				if (!PngFolderList.Contains(text))
				{
					PngFolderList.Add(text);
				}
			}
		}

		/// <summary>
		/// Build a list of folders that contain .pngs but do not match an existing asset bundle
		/// </summary>
		private void BuildPngOnlyFolderList()
		{
			foreach (string pngFolder in PngFolderList)
			{
				string text = pngFolder + ".unity3d";
				if (!File.Exists(Application.dataPath + "/../abdata/" + text) && !BundleManager.Bundles.ContainsKey(text))
				{
					PngFolderOnlyList.Add(pngFolder);
				}
			}
		}

		private static void AddBundles(ICollection<BundleLoadInfo> bundleInfos)
		{
			if (DebugLoggingModLoading.Value)
			{
				foreach (IGrouping<string, BundleLoadInfo> item in bundleInfos.ToLookup((BundleLoadInfo x) => x.BundleTrimmedPath))
				{
					if (item.Count() > 1)
					{
						IEnumerable<string> source = item.Select((BundleLoadInfo info, int i) => $"{i + 1}: {GetRelativeArchiveDir(info.ArchiveFilename)}");
						Logger.LogWarning((object)("AssetBundle at [" + item.First().BundleFullPath + "] has multiple overrides! Order in which zipmods are searched for assets:\n" + string.Join("\n", source.ToArray())));
					}
				}
			}
			foreach (BundleLoadInfo bundleInfo in bundleInfos)
			{
				BundleManager.AddBundleLoader(bundleInfo.LoadBundle, bundleInfo.BundleTrimmedPath);
			}
		}

		/// <summary>
		/// Check whether the asset bundle matches a folder that contains .png files and does not match an existing asset bundle
		/// </summary>
		public static bool IsPngFolderOnly(string assetBundleName)
		{
			int num = assetBundleName.LastIndexOf('.');
			string item = ((num >= 0) ? assetBundleName.Remove(num) : assetBundleName);
			return PngFolderOnlyList.Contains(item);
		}

		/// <summary>
		/// Check if a mod with specified GUID has been loaded.
		/// </summary>
		[Obsolete("Use GetManifest and check null instead")]
		public bool IsModLoaded(string guid)
		{
			return GetManifest(guid) != null;
		}

		/// <summary>
		/// Check if a mod with specified GUID has been loaded and fetch its manifest.
		/// Returns null if there was no mod with this guid loaded.
		/// </summary>
		/// <param name="guid">GUID of the mod.</param>
		/// <returns>Manifest of the loaded mod or null if mod is not loaded.</returns>
		public static Manifest GetManifest(string guid)
		{
			if (guid == null)
			{
				return null;
			}
			guid = guid.Trim();
			if (guid.Length == 0)
			{
				return null;
			}
			Manifests.TryGetValue(guid, out var value);
			return value;
		}

		/// <summary>
		/// Get a list of file paths to all png files inside the loaded mods
		/// </summary>
		public static IEnumerable<string> GetPngNames()
		{
			return PngList.Keys;
		}

		/// <summary>
		/// Get a new copy of the png file if it exists in any of the loaded zipmods
		/// </summary>
		public static Texture2D GetPng(string pngPath, TextureFormat format = 4, bool mipmap = true)
		{
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Expected O, but got Unknown
			if (string.IsNullOrEmpty(pngPath))
			{
				return null;
			}
			if (PngList.TryGetValue(pngPath, out var value))
			{
				ZipFile zipFile = value.GetZipFile();
				ZipEntry entry = zipFile.GetEntry(pngPath);
				if (entry != null)
				{
					Stream inputStream = zipFile.GetInputStream(entry);
					int num = (int)entry.Size;
					byte[] array = new byte[num];
					inputStream.Read(array, 0, num);
					Texture2D val = new Texture2D(2, 2, format, mipmap);
					ImageConversion.LoadImage(val, array);
					if (pngPath.Contains("clamp"))
					{
						((Texture)val).wrapMode = (TextureWrapMode)1;
					}
					else if (pngPath.Contains("repeat"))
					{
						((Texture)val).wrapMode = (TextureWrapMode)0;
					}
					return val;
				}
			}
			return null;
		}

		/// <summary>
		/// Check whether the .png file comes from a sideloader mod
		/// </summary>
		public static bool IsPng(string pngFile)
		{
			return PngList.ContainsKey(pngFile);
		}

		/// <summary>
		/// Try to get ExcelData that was originally in .csv form in the mod
		/// </summary>
		/// <param name="assetBundleName">Name of the folder containing the .csv file</param>
		/// <param name="assetName">Name of the .csv file without the file extension</param>
		/// <param name="excelData">ExcelData or null if none exists</param>
		/// <returns>True if ExcelData was returned</returns>
		public static bool TryGetExcelData(string assetBundleName, string assetName, out ExcelData excelData)
		{
			excelData = null;
			if (Lists.ExternalExcelData.TryGetValue(assetBundleName, out var value) && value.TryGetValue(assetName, out excelData))
			{
				return true;
			}
			return false;
		}

		/// <summary>
		/// Check whether the asset bundle at the specified path is one managed by Sideloader
		/// </summary>
		/// <param name="assetBundlePath">Path to the asset bundle without the leading abdata, i.e. map/list/mapinfo/mymap.unity3d</param>
		/// <returns>True if the asset bundle is managed by Sideloader, false if not (doesn't exist, vanilla asset bundle, etc)</returns>
		public static bool IsSideloaderAB(string assetBundlePath)
		{
			if (BundleManager.Bundles.ContainsKey(assetBundlePath))
			{
				return true;
			}
			if (Lists.ExternalExcelData.ContainsKey(assetBundlePath))
			{
				return true;
			}
			if (IsPngFolderOnly(assetBundlePath))
			{
				return true;
			}
			return false;
		}

		internal static string GetRelativeArchiveDir(string archiveDir)
		{
			if (archiveDir.StartsWith(ModsDirectory, StringComparison.OrdinalIgnoreCase))
			{
				return archiveDir.Substring(ModsDirectory.Length).Trim(' ', '/', '\\');
			}
			return archiveDir;
		}

		private void RedirectHook(IAssetLoadingContext context)
		{
			if (context.Parameters.Name == null || ((Object)context.Bundle).name == null)
			{
				return;
			}
			if (typeof(Texture).IsAssignableFrom(context.Parameters.Type))
			{
				string pngPath = "abdata/" + ((Object)context.Bundle).name.Replace(".unity3d", "", StringComparison.OrdinalIgnoreCase) + "/" + context.Parameters.Name + ".png";
				Texture2D png = GetPng(pngPath, (TextureFormat)4);
				if ((Object)(object)png != (Object)null)
				{
					context.Asset = (Object)(object)png;
					context.Complete(true, (bool?)true, (bool?)true);
					return;
				}
			}
			Object obj;
			if (context.Parameters.Type == typeof(ExcelData) && TryGetExcelData(((Object)context.Bundle).name, context.Parameters.Name, out var excelData))
			{
				context.Asset = (Object)(object)excelData;
				context.Complete(true, (bool?)true, (bool?)true);
			}
			else if (BundleManager.TryGetObjectFromName(context.Parameters.Name, ((Object)context.Bundle).name, context.Parameters.Type, out obj))
			{
				context.Asset = obj;
				context.Complete(true, (bool?)true, (bool?)true);
			}
		}

		private void AssetBundleLoadingHook(IAssetBundleLoadingContext context)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Invalid comparison between Unknown and I4
			if ((int)context.Parameters.LoadType != 1)
			{
				return;
			}
			string path = context.Parameters.Path;
			if (path == null)
			{
				return;
			}
			int num = path.IndexOf("/abdata/", StringComparison.OrdinalIgnoreCase);
			if (num == -1 || num + "/abdata/".Length >= path.Length)
			{
				return;
			}
			string text = path.Substring(num + "/abdata/".Length);
			if (!File.Exists(path))
			{
				if (BundleManager.Bundles.TryGetValue(text, out var value))
				{
					context.Bundle = value[0].Instance;
					((Object)context.Bundle).name = text;
					context.Complete();
				}
				else if (IsPngFolderOnly(text))
				{
					context.Bundle = AssetBundleHelper.CreateEmptyAssetBundle();
					((Object)context.Bundle).name = text;
					context.Complete();
				}
				else if (Lists.ExternalExcelData.ContainsKey(text))
				{
					context.Bundle = AssetBundleHelper.CreateEmptyAssetBundle();
					((Object)context.Bundle).name = text;
					context.Complete();
				}
			}
			else
			{
				AssetBundle val = AssetBundle.LoadFromFile(context.Parameters.Path, context.Parameters.Crc, context.Parameters.Offset);
				if ((Object)(object)val != (Object)null)
				{
					context.Bundle = val;
					((Object)context.Bundle).name = text;
					context.Complete();
				}
			}
		}

		private void WriteCache()
		{
			try
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				string[] files = Directory.GetFiles(_CacheDirectory, _CacheName + ".*");
				foreach (string path in files)
				{
					File.Delete(path);
				}
				if (!CachingEnabled.Value)
				{
					return;
				}
				int threadCount = Mathf.Clamp(Zipmods.Count / 1500, 1, SystemInfo.processorCount);
				int modsPerThread = Zipmods.Count / threadCount + 1;
				ManualResetEvent wait = new ManualResetEvent(initialState: false);
				int finished = 0;
				for (int j = 0; j < threadCount; j++)
				{
					int threadIndex = j;
					int modsToSkip = threadIndex * modsPerThread;
					ThreadPool.QueueUserWorkItem(delegate
					{
						string text = _CachePath + "." + threadIndex;
						try
						{
							using FileStream fileStream = File.OpenWrite(text);
							List<ZipmodInfo> list = Zipmods.Values.Skip(modsToSkip).Take(modsPerThread).ToList();
							LZ4MessagePackSerializer.Serialize<List<ZipmodInfo>>((Stream)fileStream, list, ContractlessStandardResolverAllowPrivate.Instance);
						}
						catch (Exception arg)
						{
							Logger.LogWarning((object)$"Failed to save cache part [{text}] with error: {arg}");
						}
						finally
						{
							if (Interlocked.Add(ref finished, 1) >= threadCount)
							{
								wait.Set();
							}
						}
					});
				}
				File.WriteAllText(_CachePath + ".ver", ((BaseUnityPlugin)this).Info.Metadata.Version.ToString());
				wait.WaitOne();
				Logger.LogDebug((object)$"Saved zipmod cache to \"{_CachePath}\" in {stopwatch.ElapsedMilliseconds}ms using {threadCount} threads");
			}
			catch (Exception ex)
			{
				Logger.LogWarning((object)("Failed to save cache: " + ex));
			}
		}

		private Dictionary<string, ZipmodInfo> LoadCache()
		{
			Dictionary<string, ZipmodInfo> cache = new Dictionary<string, ZipmodInfo>();
			if (!CachingEnabled.Value)
			{
				return cache;
			}
			try
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				if (File.Exists(_CachePath + ".ver"))
				{
					Version version = new Version(File.ReadAllText(_CachePath + ".ver"));
					if (version != ((BaseUnityPlugin)this).Info.Metadata.Version)
					{
						throw new Exception($"Cache version ({version}) doesn't match Sideloader version ({((BaseUnityPlugin)this).Info.Metadata.Version}), it has to be regenerated.");
					}
					List<string> cachePartFiles = (from x in Directory.GetFiles(_CacheDirectory, _CacheName + ".*")
						where !x.EndsWith(".ver", StringComparison.OrdinalIgnoreCase)
						select x).ToList();
					ManualResetEvent wait = new ManualResetEvent(initialState: false);
					int finished = 0;
					for (int i = 0; i < cachePartFiles.Count; i++)
					{
						string cachePartFile = cachePartFiles[i];
						ThreadPool.QueueUserWorkItem(delegate
						{
							try
							{
								using FileStream fileStream = File.OpenRead(cachePartFile);
								List<ZipmodInfo> list = LZ4MessagePackSerializer.Deserialize<List<ZipmodInfo>>((Stream)fileStream, ContractlessStandardResolverAllowPrivate.Instance);
								lock (cache)
								{
									foreach (ZipmodInfo item in list)
									{
										cache.Add(item.FileName, item);
									}
								}
							}
							catch (Exception arg)
							{
								Logger.LogWarning((object)$"Failed to load cache part [{cachePartFile}] with error: {arg}");
							}
							finally
							{
								if (Interlocked.Add(ref finished, 1) >= cachePartFiles.Count)
								{
									wait.Set();
								}
							}
						});
					}
					wait.WaitOne();
					Logger.LogDebug((object)$"Loaded zipmod cache from \"{_CachePath}\" in {stopwatch.ElapsedMilliseconds}ms using {cachePartFiles.Count} threads");
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning((object)("Failed to load cache: " + ex));
			}
			return cache;
		}
	}
	internal static class ReflectionHelper
	{
		internal static T GetValue<T>(this FieldInfo info, object instance)
		{
			return (T)info.GetValue(instance);
		}
	}
	internal static class BundleManager
	{
		internal static Dictionary<string, List<Shared.LazyCustom<AssetBundle>>> Bundles = new Dictionary<string, List<Shared.LazyCustom<AssetBundle>>>();

		private static long CABCounter;

		internal static string GenerateCAB()
		{
			return "CAB-" + Interlocked.Increment(ref CABCounter).ToString("x32");
		}

		internal static void RandomizeCAB(byte[] assetBundleData)
		{
			string @string = Encoding.ASCII.GetString(assetBundleData, 0, Mathf.Min(1024, assetBundleData.Length - 4));
			int num = @string.IndexOf("CAB-", StringComparison.Ordinal);
			if (num < 0)
			{
				return;
			}
			int num2 = @string.Substring(num).IndexOf('\0');
			if (num2 >= 0)
			{
				string s = GenerateCAB().Substring(4);
				byte[] bytes = Encoding.ASCII.GetBytes(s);
				if (num2 <= 36)
				{
					Buffer.BlockCopy(bytes, 36 - num2, assetBundleData, num + 4, num2 - 4);
				}
			}
		}

		internal static void AddBundleLoader(Func<AssetBundle> func, string path)
		{
			if (!Bundles.TryGetValue(path, out var value))
			{
				value = new List<Shared.LazyCustom<AssetBundle>>();
				Bundles.Add(path, value);
			}
			value.Add(Shared.LazyCustom<AssetBundle>.Create(func));
		}

		internal static bool TryGetObjectFromName<T>(string name, string assetBundle, out T obj) where T : Object
		{
			Object obj2;
			bool result = TryGetObjectFromName(name, assetBundle, typeof(T), out obj2);
			obj = (T)(object)obj2;
			return result;
		}

		internal static bool TryGetObjectFromName(string name, string assetBundle, Type type, out Object obj)
		{
			obj = null;
			if (Bundles.TryGetValue(assetBundle, out var value))
			{
				int num = -1;
				for (int i = 0; i < value.Count; i++)
				{
					AssetBundle val = value[i];
					if (val.Contains(name))
					{
						if (!Sideloader.DebugLoggingModLoading.Value)
						{
							obj = val.LoadAsset(name, type);
							return true;
						}
						if (num >= 0)
						{
							Sideloader.Logger.LogWarning((object)("Asset [" + name + "] in bundle [" + assetBundle + "] is overridden by multiple zipmods! " + $"Only asset from override #{num + 1} will be used! It also exists in override #{i + 1}."));
						}
						else
						{
							num = i;
							obj = val.LoadAsset(name, type);
						}
					}
				}
				if (num >= 0)
				{
					return true;
				}
			}
			return false;
		}
	}
	/// <summary>
	/// Contains data about the loaded manifest.xml
	/// </summary>
	[MessagePackObject(false)]
	public class Manifest
	{
		/// <summary>
		/// Parsed contents of the manifest.xml.
		/// </summary>
		[IgnoreMember]
		[Obsolete("Use ManifestDocument instead")]
		public readonly XDocument manifestDocument;

		/// <summary>
		/// Version of this manifest.
		/// </summary>
		[Key(0)]
		public int SchemaVer { get; } = 1;


		/// <summary>
		/// GUID of the mod.
		/// </summary>
		[Key(1)]
		public string GUID { get; }

		/// <summary>
		/// Name of the mod. Only used for display the name of the mod when mods are loaded.
		/// </summary>
		[Key(2)]
		public string Name { get; }

		/// <summary>
		/// Version of the mod.
		/// </summary>
		[Key(3)]
		public string Version { get; }

		/// <summary>
		/// Author of the mod. Not currently used for anything.
		/// </summary>
		[Key(4)]
		public string Author { get; }

		/// <summary>
		/// Website of the mod. Not currently used for anything.
		/// </summary>
		[Key(5)]
		public string Website { get; }

		/// <summary>
		/// Description of the mod. Not currently used for anything.
		/// </summary>
		[Key(6)]
		public string Description { get; }

		/// <summary>
		/// Parsed contents of the manifest.xml.
		/// </summary>
		[IgnoreMember]
		public XDocument ManifestDocument => manifestDocument;

		/// <summary>
		/// Raw contents of the manifest.xml.
		/// </summary>
		[Key(7)]
		public string ManifestString { get; }

		/// <summary>
		/// Game the mod is made for. If specified, the mod will only load for that game. If not specified will load on any game.
		/// </summary>
		[IgnoreMember]
		[Obsolete("Use Games instead")]
		public string Game => Games.OrderByDescending((string x) => Array.IndexOf(Sideloader.GameNameList, x.ToLowerInvariant())).FirstOrDefault();

		/// <summary>
		/// Games the mod is made for. If specified, the mod will only load for those games. If not specified will load on any game.
		/// </summary>
		[Key(8)]
		public List<string> Games { get; }

		/// <summary>
		/// List of all migration info for this mod
		/// </summary>
		[Key(9)]
		public List<MigrationInfo> MigrationList { get; }

		public Manifest(Stream stream)
		{
			using (XmlReader reader = XmlReader.Create(stream))
			{
				manifestDocument = XDocument.Load(reader);
			}
			if (manifestDocument.Root == null)
			{
				throw new OperationCanceledException("Manifest.xml is in an invalid format");
			}
			string text = manifestDocument.Root.Attribute("schema-ver")?.Value;
			if (text != SchemaVer.ToString())
			{
				throw new OperationCanceledException($"Manifest.xml is in an unknown version: {text} (supported: {SchemaVer})");
			}
			ManifestString = manifestDocument.ToString(SaveOptions.DisableFormatting);
			Games = (from x in manifestDocument.Root.Elements("game")
				select x.Value.Trim() into x
				where x.Length > 0
				select x).ToList();
			Description = manifestDocument.Root?.Element("description")?.Value.Trim();
			Website = manifestDocument.Root?.Element("website")?.Value.Trim();
			Author = manifestDocument.Root?.Element("author")?.Value.Trim();
			Version = manifestDocument.Root?.Element("version")?.Value.Trim();
			Name = manifestDocument.Root?.Element("name")?.Value.Trim();
			GUID = manifestDocument.Root?.Element("guid")?.Value.Trim();
			MigrationList = new List<MigrationInfo>();
		}

		[SerializationConstructor]
		public Manifest(int schemaVer, string guid, string name, string version, string author, string website, string description, string manifestString, List<string> games, List<MigrationInfo> migrationList)
		{
			SchemaVer = schemaVer;
			GUID = guid?.Trim();
			Name = name;
			Version = version;
			Author = author;
			Website = website;
			Description = description;
			MigrationList = migrationList ?? throw new ArgumentNullException("migrationList");
			Games = games ?? throw new ArgumentNullException("games");
			ManifestString = manifestString ?? throw new ArgumentNullException("manifestString");
			using StringReader textReader = new StringReader(manifestString);
			manifestDocument = XDocument.Load(textReader);
		}

		private void LoadMigrationInfo()
		{
			//IL_019e: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_0204: Unknown result type (might be due to invalid IL or missing references)
			XElement xElement = manifestDocument?.Root?.Element("migrationInfo");
			if (xElement == null)
			{
				return;
			}
			foreach (XElement item in xElement.Elements("info"))
			{
				try
				{
					MigrationType migrationType = ((item.Attribute("migrationType")?.Value != null && !NullCheck.IsNullOrWhiteSpace(item.Attribute("migrationType").Value)) ? ((MigrationType)Enum.Parse(typeof(MigrationType), item.Attribute("migrationType").Value)) : MigrationType.Migrate);
					string text = item.Attribute("guidOld")?.Value;
					string text2 = item.Attribute("guidNew")?.Value;
					if (NullCheck.IsNullOrWhiteSpace(text2))
					{
						text2 = GUID;
					}
					if (NullCheck.IsNullOrEmpty(text))
					{
						throw new Exception("guidOld must be specified for migration.");
					}
					if (NullCheck.IsNullOrEmpty(text2) && migrationType == MigrationType.Migrate)
					{
						throw new Exception("guidNew must be specified for migration.");
					}
					if (migrationType == MigrationType.MigrateAll || migrationType == MigrationType.StripAll)
					{
						MigrationList.Add(new MigrationInfo(migrationType, text, text2));
						continue;
					}
					if (item.Attribute("category")?.Value == null)
					{
						throw new Exception("Category must be specified for migration.");
					}
					CategoryNo category = (CategoryNo)Enum.Parse(typeof(CategoryNo), item.Attribute("category").Value);
					if (!int.TryParse(item.Attribute("idOld").Value, out var result) && migrationType == MigrationType.Migrate)
					{
						throw new Exception("ID must be specified for migration.");
					}
					if (!int.TryParse(item.Attribute("idNew").Value, out var result2) && migrationType == MigrationType.Migrate)
					{
						throw new Exception("ID must be specified for migration.");
					}
					MigrationList.Add(new MigrationInfo(migrationType, category, text, text2, result, result2));
				}
				catch (Exception ex)
				{
					Sideloader.Logger.LogError((object)("Could not load migration data for " + GUID + ", skipping line."));
					Sideloader.Logger.LogError((object)ex);
				}
			}
		}

		internal static bool TryLoadFromZip(ZipFile zip, out Manifest manifest)
		{
			manifest = null;
			try
			{
				manifest = LoadFromZip(zip);
				return true;
			}
			catch (SystemException ex)
			{
				Sideloader.Logger.LogWarning((object)("Cannot load " + Path.GetFileName(zip.Name) + " - " + ex.Message + "."));
				if (!(ex is OperationCanceledException))
				{
					Sideloader.Logger.LogDebug((object)("Error details: " + ex));
				}
				return false;
			}
		}

		internal static Manifest LoadFromZip(ZipFile zip)
		{
			ZipEntry entry = zip.GetEntry("manifest.xml");
			if (entry == null)
			{
				throw new OperationCanceledException("Manifest.xml is missing, make sure this is a zipmod");
			}
			Manifest manifest = new Manifest(zip.GetInputStream(entry));
			if (manifest.GUID == null)
			{
				throw new OperationCanceledException("Manifest.xml is missing the GUID");
			}
			manifest.LoadMigrationInfo();
			return manifest;
		}
	}
	internal class ManifestVersionComparer : IComparer<string>
	{
		public int Compare(string x, string y)
		{
			return CompareVersions(x, y);
		}

		public static int CompareVersions(string firstVer, string secondVer)
		{
			firstVer = firstVer?.Trim().TrimStart('v', 'V', 'r', 'R', ' ');
			if (string.IsNullOrEmpty(firstVer))
			{
				firstVer = "0";
			}
			secondVer = secondVer?.Trim().TrimStart('v', 'V', 'r', 'R', ' ');
			if (string.IsNullOrEmpty(secondVer))
			{
				secondVer = "0";
			}
			if (firstVer == secondVer)
			{
				return 0;
			}
			var anon = new
			{
				First = Tokenize(firstVer),
				Second = Tokenize(secondVer)
			};
			int num = Math.Max(anon.First.Count, anon.Second.Count);
			for (int i = 0; i < num; i++)
			{
				IComparable comparable = (IComparable)(anon.First.ElementAtOrDefault(i) ?? ((object)0));
				IComparable comparable2 = (IComparable)(anon.Second.ElementAtOrDefault(i) ?? ((object)0));
				try
				{
					int num2 = comparable.CompareTo(comparable2);
					if (num2 != 0)
					{
						return num2;
					}
				}
				catch (ArgumentException)
				{
					string text = comparable.ToString();
					string text2 = comparable2.ToString();
					if (text == "0" && text2 != "0")
					{
						return -1;
					}
					if (text2 == "0" && text != "0")
					{
						return 1;
					}
					int num3 = string.CompareOrdinal(text, text2);
					if (num3 != 0)
					{
						return num3;
					}
				}
			}
			return 0;
		}

		private static ICollection<IComparable> Tokenize(string version)
		{
			List<IComparable> list = new List<IComparable>(2);
			string[] array = version.Trim().Split('.', ' ', '-', ',', '_');
			foreach (string text in array)
			{
				if (text.Length == 0)
				{
					list.Add(0);
					continue;
				}
				bool flag = char.IsDigit(text[0]);
				string text2 = text[0].ToString();
				for (int j = 1; j < text.Length; j++)
				{
					if (flag == char.IsDigit(text[j]))
					{
						text2 += text[j];
						continue;
					}
					list.Add(Parse(text2));
					text2 = text[j].ToString();
					flag = !flag;
				}
				list.Add(Parse(text2));
			}
			return list;
		}

		private static IComparable Parse(string version)
		{
			if (!int.TryParse(version, out var result))
			{
				return version;
			}
			return result;
		}
	}
	[MessagePackObject(false)]
	internal class ZipmodInfo : IDisposable
	{
		[Key(0)]
		public Manifest Manifest;

		[Key(1)]
		public string FileName;

		[Key(2)]
		public string RelativeFileName;

		[Key(3)]
		public DateTime LastWriteTime;

		[Key(4)]
		public long FileSize;

		[Key(5)]
		public string Error;

		[Key(6)]
		public List<string> PngNames = new List<string>();

		[Key(7)]
		public List<ChaListData> CharaLists = new List<ChaListData>();

		[Key(8)]
		public List<BundleLoadInfo> BundleInfos = new List<BundleLoadInfo>();

		[Key(9)]
		public List<Lists.StudioListData> BoneLists = new List<Lists.StudioListData>();

		[Key(10)]
		public List<Lists.StudioListData> StudioLists = new List<Lists.StudioListData>();

		[Key(11)]
		public List<Lists.StudioListData> MapLists = new List<Lists.StudioListData>();

		[IgnoreMember]
		private ZipFile _zipFile;

		[IgnoreMember]
		public bool Loaded;

		private static readonly MethodInfo _LocateZipEntryMethodInfo = typeof(ZipFile).GetMethod("LocateEntry", AccessTools.all);

		[IgnoreMember]
		public bool Valid
		{
			get
			{
				if (Manifest != null)
				{
					return Error == null;
				}
				return false;
			}
		}

		[SerializationConstructor]
		public ZipmodInfo()
		{
		}

		public ZipmodInfo(string fileName)
		{
			FileName = fileName;
			RelativeFileName = Sideloader.GetRelativeArchiveDir(fileName);
			FileInfo fileInfo = new FileInfo(fileName);
			LastWriteTime = fileInfo.LastWriteTimeUtc;
			FileSize = fileInfo.Length;
		}

		public ZipFile GetZipFile()
		{
			if (_zipFile == null)
			{
				_zipFile = new ZipFile(FileName);
			}
			else if (_zipFile.Count == 0L)
			{
				_zipFile.Close();
				_zipFile = new ZipFile(FileName);
			}
			return _zipFile;
		}

		public void Dispose()
		{
			if (_zipFile != null)
			{
				_zipFile.Close();
				_zipFile = null;
			}
		}

		public void LoadAllLists()
		{
			ZipFile zipFile = GetZipFile();
			Manifest manifest = Manifest;
			foreach (ZipEntry item3 in zipFile)
			{
				if (item3.Name.EndsWith(".unity3d", StringComparison.OrdinalIgnoreCase))
				{
					string text = item3.Name;
					if (text.Contains('/'))
					{
						text = text.Remove(0, text.IndexOf('/') + 1);
					}
					if (item3.CompressionMethod == CompressionMethod.Stored)
					{
						long streamOffset = (long)_LocateZipEntryMethodInfo.Invoke(zipFile, new object[1] { item3 });
						BundleInfos.Add(new BundleLoadInfo(zipFile.Name, streamOffset, item3.Name, text));
					}
					else
					{
						BundleInfos.Add(new BundleLoadInfo(zipFile.Name, -1L, item3.Name, text));
					}
				}
			}
			foreach (ZipEntry item4 in zipFile)
			{
				try
				{
					if (item4.Name.StartsWith("abdata/list/characustom", StringComparison.OrdinalIgnoreCase) && item4.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
					{
						Stream inputStream = zipFile.GetInputStream(item4);
						ChaListData val = Lists.LoadCSV(inputStream);
						SetPossessNew(val);
						CharaLists.Add(val);
					}
					else if (item4.Name.StartsWith("abdata/studio/info", StringComparison.OrdinalIgnoreCase) && item4.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
					{
						if (Path.GetFileNameWithoutExtension(item4.Name).ToLower().StartsWith("itembonelist_"))
						{
							Stream inputStream2 = zipFile.GetInputStream(item4);
							Lists.StudioListData item = Lists.LoadStudioCSV(inputStream2, item4.Name, manifest.GUID);
							BoneLists.Add(item);
						}
						else
						{
							Stream inputStream3 = zipFile.GetInputStream(item4);
							Lists.StudioListData item2 = Lists.LoadStudioCSV(inputStream3, item4.Name, manifest.GUID);
							StudioLists.Add(item2);
						}
					}
				}
				catch (Exception arg)
				{
					Sideloader.Logger.LogError((object)$"Failed to load list file \"{item4.Name}\" from archive \"{Sideloader.GetRelativeArchiveDir(zipFile.Name)}\" with error: {arg}");
				}
			}
			foreach (ZipEntry item5 in zipFile)
			{
				if (item5.Name.StartsWith("abdata/", StringComparison.OrdinalIgnoreCase) && item5.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
				{
					PngNames.Add(item5.Name);
				}
			}
		}

		private static void SetPossessNew(ChaListData data)
		{
			for (int i = 0; i < data.lstKey.Count; i++)
			{
				if (!(data.lstKey[i] == "Possess"))
				{
					continue;
				}
				{
					foreach (KeyValuePair<int, List<string>> dict in data.dictList)
					{
						dict.Value[i] = "1";
					}
					break;
				}
			}
		}
	}
	[MessagePackObject(false)]
	internal class BundleLoadInfo
	{
		[Key(0)]
		public string ArchiveFilename { get; }

		[Key(1)]
		public long StreamOffset { get; }

		[IgnoreMember]
		public bool CanBeStreamed => StreamOffset > 0;

		[Key(2)]
		public string BundleFullPath { get; }

		[Key(3)]
		public string BundleTrimmedPath { get; }

		[SerializationConstructor]
		public BundleLoadInfo(string archiveFilename, long streamOffset, string bundleFullPath, string bundleTrimmedPath)
		{
			ArchiveFilename = archiveFilename;
			StreamOffset = streamOffset;
			BundleFullPath = bundleFullPath;
			BundleTrimmedPath = bundleTrimmedPath;
		}

		public AssetBundle LoadBundle()
		{
			AssetBundle val;
			if (CanBeStreamed)
			{
				if (Sideloader.DebugLogging.Value)
				{
					Sideloader.Logger.LogDebug((object)$"Streaming \"{BundleFullPath}\" ({Sideloader.GetRelativeArchiveDir(ArchiveFilename)}) unity3d file from disk, offset {StreamOffset}");
				}
				val = AssetBundle.LoadFromFile(ArchiveFilename, 0u, (ulong)StreamOffset);
			}
			else
			{
				Sideloader.Logger.LogDebug((object)("Cannot stream \"" + BundleFullPath + "\" (" + Sideloader.GetRelativeArchiveDir(ArchiveFilename) + ") unity3d file from disk, loading to RAM instead"));
				ZipFile zipFile = Sideloader.Zipmods[ArchiveFilename].GetZipFile();
				ZipEntry entry = zipFile.GetEntry(BundleFullPath);
				Stream inputStream = zipFile.GetInputStream(entry);
				byte[] array = new byte[entry.Size];
				inputStream.Read(array, 0, (int)entry.Size);
				val = AssetBundleHelper.LoadFromMemory("\"" + BundleFullPath + "\" (" + Sideloader.GetRelativeArchiveDir(ArchiveFilename) + ")", array, 0u);
			}
			if ((Object)(object)val == (Object)null)
			{
				Sideloader.Logger.LogError((object)("Asset bundle \"" + BundleFullPath + "\" (" + Sideloader.GetRelativeArchiveDir(ArchiveFilename) + ") failed to load. It might have a conflicting CAB string."));
			}
			return val;
		}
	}
}
namespace Sideloader.ListLoader
{
	internal static class Lists
	{
		internal static class Hooks
		{
			internal static void InstallHooks()
			{
				Harmony.CreateAndPatchAll(typeof(Hooks), "Sideloader.ListLoader");
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(ChaListControl), "CheckItemID", new Type[]
			{
				typeof(int),
				typeof(int)
			})]
			private static bool CheckItemIDHook(int category, int id, ref byte __result, ChaListControl __instance)
			{
				int num = CalculateGlobalID(category, id);
				byte b = __instance.CheckItemID(num);
				if (b > 0)
				{
					__result = b;
					return false;
				}
				return true;
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(ChaListControl), "AddItemID", new Type[]
			{
				typeof(int),
				typeof(int),
				typeof(byte)
			})]
			private static bool AddItemIDHook(int category, int id, byte flags, ChaListControl __instance)
			{
				int num = CalculateGlobalID(category, id);
				byte b = __instance.CheckItemID(num);
				if (b > 0)
				{
					__instance.AddItemID(num, flags);
					return false;
				}
				return true;
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaListControl), "LoadListInfoAll")]
			private static void LoadListInfoAllPostHook(ChaListControl __instance)
			{
				LoadAllLists(__instance);
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(ChaListControl), "AddItemID", new Type[]
			{
				typeof(int),
				typeof(int),
				typeof(byte)
			})]
			private static bool AddItemIDHook(int category, int id)
			{
				if (id >= 100000000)
				{
					ResolveInfo resolveInfo = UniversalAutoResolver.TryGetResolutionInfo((CategoryNo)category, id);
					if (!CheckItemList.ContainsKey(resolveInfo.GUID))
					{
						CheckItemList[resolveInfo.GUID] = new Dictionary<int, HashSet<int>>();
					}
					if (!CheckItemList[resolveInfo.GUID].ContainsKey(category))
					{
						CheckItemList[resolveInfo.GUID][category] = new HashSet<int>();
					}
					if (!CheckItemList[resolveInfo.GUID][category].Contains(resolveInfo.Slot))
					{
						CheckItemList[resolveInfo.GUID][category].Add(resolveInfo.Slot);
						SaveCheckItemList();
					}
					return false;
				}
				return true;
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(ChaListControl), "CheckItemID", new Type[]
			{
				typeof(int),
				typeof(int)
			})]
			private static bool CheckItemIDHook(int category, int id, ref byte __result)
			{
				if (id >= 100000000)
				{
					ResolveInfo resolveInfo = UniversalAutoResolver.TryGetResolutionInfo((CategoryNo)category, id);
					if (CheckItemList.TryGetValue(resolveInfo.GUID, out var value) && value.TryGetValue(category, out var value2) && value2.Contains(resolveInfo.Slot))
					{
						__result = 2;
						return false;
					}
					__result = 1;
					return false;
				}
				return true;
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaListControl), "LoadItemID")]
			private static void LoadItemIDHook()
			{
				LoadCheckItemList();
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaListControl), "SaveItemID")]
			private static void SaveItemIDHook()
			{
				SaveCheckItemList();
			}
		}

		[MessagePackObject(false)]
		public class StudioListData
		{
			[Key("fileName")]
			public string FileName { get; private set; }

			[Key("fileNameWithoutExtension")]
			public string FileNameWithoutExtension { get; private set; }

			[Key("assetBundleName")]
			public string AssetBundleName { get; private set; }

			[Key("headers")]
			public List<List<string>> Headers { get; private set; } = new List<List<string>>();


			[Key("entries")]
			public List<List<string>> Entries { get; private set; } = new List<List<string>>();


			public StudioListData(string fileName)
			{
				FileName = fileName;
				FileNameWithoutExtension = Path.GetFileNameWithoutExtension(FileName);
				AssetBundleName = FileName.Remove(FileName.LastIndexOf('/')).Remove(0, FileName.IndexOf('/') + 1) + ".unity3d";
			}

			[SerializationConstructor]
			public StudioListData(string fileName, string fileNameWithoutExtension, string assetBundleName, List<List<string>> headers, List<List<string>> entries)
			{
				FileName = fileName;
				FileNameWithoutExtension = fileNameWithoutExtension;
				AssetBundleName = assetBundleName;
				Headers = headers;
				Entries = entries;
			}
		}

		internal const int CategoryMultiplier = 1000000;

		private const string CheckItemFile = "UserData/save/checkitem.xml";

		internal static readonly FieldInfo r_dictListInfo = typeof(ChaListControl).GetField("dictListInfo", AccessTools.all);

		internal static Dictionary<string, Dictionary<int, HashSet<int>>> CheckItemList = new Dictionary<string, Dictionary<int, HashSet<int>>>();

		internal static HashSet<int> _internalStudioItemList = null;

		internal static Dictionary<CategoryNo, Dictionary<int, ListInfoBase>> InternalDataList { get; private set; } = new Dictionary<CategoryNo, Dictionary<int, ListInfoBase>>();


		internal static List<ChaListData> ExternalDataList { get; private set; } = new List<ChaListData>();


		internal static Dictionary<string, Dictionary<string, ExcelData>> ExternalExcelData { get; private set; } = new Dictionary<string, Dictionary<string, ExcelData>>();


		internal static Dictionary<string, List<StudioListData>> ExternalStudioDataList { get; private set; } = new Dictionary<string, List<StudioListData>>();


		internal static HashSet<int> InternalStudioItemList
		{
			get
			{
				if (_internalStudioItemList == null)
				{
					_internalStudioItemList = new HashSet<int>();
					foreach (KeyValuePair<int, Dictionary<int, Dictionary<int, ItemLoadInfo>>> item in Singleton<Info>.Instance.dicItemLoadInfo)
					{
						foreach (KeyValuePair<int, Dictionary<int, ItemLoadInfo>> item2 in item.Value)
						{
							foreach (KeyValuePair<int, ItemLoadInfo> item3 in item2.Value)
							{
								_internalStudioItemList.Add(item3.Key);
							}
						}
					}
				}
				return _internalStudioItemList;
			}
		}

		internal static int CalculateGlobalID(int category, int ID)
		{
			return category * 1000000 + ID;
		}

		internal static void LoadAllLists(ChaListControl instance)
		{
			InternalDataList = r_dictListInfo.GetValue<Dictionary<CategoryNo, Dictionary<int, ListInfoBase>>>(instance);
			foreach (ChaListData externalData in ExternalDataList)
			{
				instance.LoadList(externalData);
			}
			instance.LoadItemID();
		}

		internal static void LoadList(this ChaListControl instance, ChaListData data)
		{
			instance.LoadList((CategoryNo)data.categoryNo, data);
		}

		internal static void LoadList(this ChaListControl instance, CategoryNo category, ChaListData data)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			Dictionary<CategoryNo, Dictionary<int, ListInfoBase>> value = r_dictListInfo.GetValue<Dictionary<CategoryNo, Dictionary<int, ListInfoBase>>>(instance);
			if (value.TryGetValue(category, out var value2))
			{
				instance.LoadListInternal(value2, data);
			}
		}

		internal static void LoadListInternal(this ChaListControl instance, Dictionary<int, ListInfoBase> dictData, ChaListData chaListData)
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Expected O, but got Unknown
			foreach (KeyValuePair<int, List<string>> dict in chaListData.dictList)
			{
				ListInfoBase val = new ListInfoBase();
				if (val.Set(chaListData.categoryNo, chaListData.distributionNo, chaListData.lstKey, dict.Value) && !dictData.ContainsKey(val.Id))
				{
					dictData[val.Id] = val;
					int infoInt = val.GetInfoInt((KeyType)55);
					int num = CalculateGlobalID(val.Category, val.Id);
					instance.AddItemID(num, (byte)infoInt);
				}
			}
		}

		[Pure]
		internal static ChaListData LoadCSV(Stream stream)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Expected O, but got Unknown
			ChaListData val = new ChaListData();
			using (StreamReader streamReader = new StreamReader(stream, Encoding.UTF8))
			{
				val.categoryNo = int.Parse(streamReader.ReadLine().Split(',')[0].Trim());
				val.distributionNo = int.Parse(streamReader.ReadLine().Split(',')[0].Trim());
				val.filePath = streamReader.ReadLine().Split(',')[0].Trim();
				val.lstKey = streamReader.ReadLine().Trim().Split(',')
					.ToList();
				int num = 0;
				while (!streamReader.EndOfStream)
				{
					string text = streamReader.ReadLine().Trim();
					if (!text.Contains(','))
					{
						break;
					}
					string[] source = text.Split(',');
					val.dictList.Add(num++, source.ToList());
				}
			}
			return val;
		}

		[Pure]
		internal static StudioListData LoadExcelDataCSV(Stream stream, string csvFilename)
		{
			StudioListData studioListData = new StudioListData(csvFilename);
			using StreamReader streamReader = new StreamReader(stream, Encoding.UTF8);
			while (!streamReader.EndOfStream)
			{
				string text = streamReader.ReadLine().Trim();
				studioListData.Entries.Add(text.Split(',').ToList());
			}
			return studioListData;
		}

		internal static void AddExcelDataCSV(StudioListData data)
		{
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Expected O, but got Unknown
			ExcelData val = (ExcelData)ScriptableObject.CreateInstance(typeof(ExcelData));
			val.list = ((IEnumerable<List<string>>)data.Entries).Select((Func<List<string>, Param>)((List<string> x) => new Param
			{
				list = x
			})).ToList();
			if (!ExternalExcelData.ContainsKey(data.AssetBundleName))
			{
				ExternalExcelData[data.AssetBundleName] = new Dictionary<string, ExcelData>();
			}
			ExternalExcelData[data.AssetBundleName][data.FileNameWithoutExtension] = val;
		}

		internal static void LoadCheckItemList()
		{
			if (!File.Exists("UserData/save/checkitem.xml") || CheckItemList.Count > 0)
			{
				return;
			}
			try
			{
				XDocument xDocument = XDocument.Load("UserData/save/checkitem.xml");
				XElement xElement = xDocument.Element("checkitem");
				foreach (XElement item in xElement.Elements("mod"))
				{
					string text = item.Attribute("guid")?.Value?.Trim();
					if (string.IsNullOrEmpty(text))
					{
						continue;
					}
					foreach (XElement item2 in item.Elements("category"))
					{
						if (!int.TryParse(item2.Attribute("number")?.Value, out var result))
						{
							continue;
						}
						foreach (XElement item3 in item2.Elements("item"))
						{
							if (int.TryParse(item3.Attribute("id")?.Value, out var result2))
							{
								if (!CheckItemList.ContainsKey(text))
								{
									CheckItemList[text] = new Dictionary<int, HashSet<int>>();
								}
								if (!CheckItemList[text].ContainsKey(result))
								{
									CheckItemList[text][result] = new HashSet<int>();
								}
								CheckItemList[text][result].Add(result2);
							}
						}
					}
				}
			}
			catch
			{
				File.Delete("UserData/save/checkitem.xml");
			}
		}

		internal static void SaveCheckItemList()
		{
			File.Delete("UserData/save/checkitem.xml");
			try
			{
				XDocument xDocument = new XDocument();
				XElement xElement = new XElement("checkitem");
				xDocument.Add(xElement);
				foreach (KeyValuePair<string, Dictionary<int, HashSet<int>>> checkItem in CheckItemList)
				{
					XElement xElement2 = new XElement("mod");
					xElement2.SetAttributeValue("guid", checkItem.Key);
					xElement.Add(xElement2);
					foreach (KeyValuePair<int, HashSet<int>> item in checkItem.Value)
					{
						XElement xElement3 = new XElement("category");
						xElement3.SetAttributeValue("number", item.Key);
						xElement2.Add(xElement3);
						foreach (int item2 in item.Value)
						{
							XElement xElement4 = new XElement("item");
							xElement4.SetAttributeValue("id", item2);
							xElement3.Add(xElement4);
						}
					}
				}
				xDocument.Save("UserData/save/checkitem.xml");
			}
			catch
			{
				File.Delete("UserData/save/checkitem.xml");
			}
		}

		[Pure]
		internal static StudioListData LoadStudioCSV(Stream stream, string fileName, string guid)
		{
			StudioListData studioListData = new StudioListData(fileName);
			string text = fileName.Remove(0, fileName.LastIndexOf('/') + 1);
			string item = text.Split('_')[0].ToLower();
			bool categoryOrGroup2 = false;
			if (text.Contains("_") && Sideloader.StudioListResolveBlacklist.Contains(item))
			{
				categoryOrGroup2 = true;
			}
			using (StreamReader streamReader = new StreamReader(stream, Encoding.UTF8))
			{
				string text2 = streamReader.ReadLine();
				if (text2 == null)
				{
					return studioListData;
				}
				List<string> item2 = text2.Trim().Split(',').ToList();
				studioListData.Headers.Add(item2);
				text2 = streamReader.ReadLine();
				if (text2 == null)
				{
					return studioListData;
				}
				List<string> list = text2.Trim().Split(',').ToList();
				if (int.TryParse(list[0], out var _))
				{
					studioListData.Entries.Add(FormatList(list, categoryOrGroup2));
				}
				else
				{
					studioListData.Headers.Add(list);
				}
				while (!streamReader.EndOfStream)
				{
					text2 = streamReader.ReadLine().Trim();
					if (!text2.Contains(','))
					{
						break;
					}
					string[] source = text2.Split(',');
					studioListData.Entries.Add(FormatList(source.ToList(), categoryOrGroup2));
				}
			}
			return studioListData;
			static List<string> FormatList(List<string> line, bool categoryOrGroup)
			{
				return line;
			}
		}
	}
}
namespace Sideloader.AutoResolver
{
	/// <summary>
	/// Automatically resolves ID conflicts by saving GUID to the card and changing item IDs at runtime
	/// </summary>
	public static class UniversalAutoResolver
	{
		internal static class Hooks
		{
			private const string ConfigFilePath = "UserData/config/system.xml";

			/// <summary>
			/// A flag for disabling certain events when importing KK cards to EC. Should always be set to false in InstallHooks for KK and always remain false.
			/// </summary>
			private static bool DoingImport;

			/// <summary>
			/// Re-enable sideloader card and coordinate saving once import is finished
			/// </summary>
			[HarmonyPostfix]
			[HarmonyPatch(typeof(ConvertChaFileScene), "OnDestroy")]
			private static void ConvertChaFileSceneEnd()
			{
				DoingImport = false;
			}

			internal static void ExtendedCardImport(Dictionary<string, PluginData> importedExtendedData, Dictionary<int, int?> coordinateMapping)
			{
				if (importedExtendedData.TryGetValue("com.bepis.sideloader.universalautoresolver", out var value) && value != null && value.data.ContainsKey("info"))
				{
					object[] source = (object[])value.data["info"];
					List<ResolveInfo> list = source.Select((object x) => ResolveInfo.Deserialize((byte[])x)).ToList();
					List<ResolveInfo> list2 = new List<ResolveInfo>();
					foreach (ResolveInfo item in list)
					{
						if (item.Property.StartsWith("outfit"))
						{
							string[] array = item.Property.Split('.');
							int key = int.Parse(array[0].Replace("outfit", ""));
							if (coordinateMapping.TryGetValue(key, out var value2) && value2.HasValue)
							{
								array[0] = $"outfit{value2}";
								item.Property = string.Join(".", array);
							}
							else
							{
								list2.Add(item);
							}
						}
					}
					foreach (ResolveInfo item2 in list2)
					{
						list.Remove(item2);
					}
					importedExtendedData["com.bepis.sideloader.universalautoresolver"] = new PluginData
					{
						data = new Dictionary<string, object> { ["info"] = list.Select((ResolveInfo x) => x.Serialize()).ToList() }
					};
				}
				if (!Sideloader.DebugLogging.Value || !importedExtendedData.TryGetValue("com.bepis.sideloader.universalautoresolver", out var value3))
				{
					return;
				}
				if (value3 == null || !value3.data.ContainsKey("info"))
				{
					Sideloader.Logger.Log((LogLevel)32, (object)"Imported card data: No sideloader marker found");
					return;
				}
				List<byte[]> source2 = (List<byte[]>)value3.data["info"];
				List<ResolveInfo> list3 = source2.Select(ResolveInfo.Deserialize).ToList();
				Sideloader.Logger.Log((LogLevel)32, (object)$"Imported card data: Sideloader marker found, external info count: {list3.Count}");
				foreach (ResolveInfo item3 in list3)
				{
					Sideloader.Logger.Log((LogLevel)32, (object)$"External info: {item3.GUID} : {item3.Property} : {item3.Slot}");
				}
			}

			[HarmonyPrefix]
			[HarmonyPatch(typeof(Control), "Write")]
			private static void XMLWritePrefix(Control __instance, ref int __state)
			{
				try
				{
					__state = -1;
					Data[] datas = __instance.Datas;
					foreach (Data val in datas)
					{
						GraphicSystem val2 = (GraphicSystem)(object)((val is GraphicSystem) ? val : null);
						if (val2 != null && val2.rampId >= 100000000)
						{
							ResolveInfo resolveInfo = TryGetResolutionInfo("Ramp", val2.rampId);
							if (resolveInfo == null)
							{
								__state = 1;
								val2.rampId = 1;
							}
							else
							{
								__state = val2.rampId;
								val2.rampId = resolveInfo.Slot;
							}
						}
					}
				}
				catch (Exception ex)
				{
					__state = -1;
					Debug.LogException(ex);
				}
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(Control), "Write")]
			private static void XMLWritePostfix(Control __instance, ref int __state)
			{
				try
				{
					int num = __state;
					if (num < 100000000)
					{
						return;
					}
					Data[] datas = __instance.Datas;
					foreach (Data val in datas)
					{
						GraphicSystem val2 = (GraphicSystem)(object)((val is GraphicSystem) ? val : null);
						if (val2 != null)
						{
							ResolveInfo resolveInfo = TryGetResolutionInfo("Ramp", num);
							if (resolveInfo != null)
							{
								val2.rampId = resolveInfo.LocalSlot;
								XDocument xDocument = XDocument.Load("UserData/config/system.xml");
								xDocument.Element("System").Element("Graphic").Element("rampId")
									.AddAfterSelf(new XElement("rampGUID", resolveInfo.GUID));
								xDocument.Save("UserData/config/system.xml");
							}
						}
					}
				}
				catch (Exception ex)
				{
					Debug.LogException(ex);
				}
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(Control), "Read")]
			private static void XMLReadPostfix(Control __instance)
			{
				try
				{
					Data[] datas = __instance.Datas;
					foreach (Data val in datas)
					{
						GraphicSystem val2 = (GraphicSystem)(object)((val is GraphicSystem) ? val : null);
						if (val2 == null)
						{
							continue;
						}
						if (val2.rampId >= 100000000)
						{
							val2.rampId = 1;
						}
						else
						{
							if (!File.Exists("UserData/config/system.xml"))
							{
								continue;
							}
							XDocument xDocument = XDocument.Load("UserData/config/system.xml");
							string text = xDocument.Element("System").Element("Graphic").Element("rampGUID")?.Value;
							if (!NullCheck.IsNullOrWhiteSpace(text))
							{
								ResolveInfo resolveInfo = TryGetResolutionInfo(val2.rampId, "Ramp", text);
								if (resolveInfo == null)
								{
									val2.rampId = 1;
								}
								else
								{
									val2.rampId = resolveInfo.LocalSlot;
								}
							}
						}
					}
				}
				catch (Exception ex)
				{
					Debug.LogException(ex);
				}
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(SceneInfo), "Init")]
			private static void SceneInfoInit(SceneInfo __instance)
			{
				try
				{
					if (!File.Exists("UserData/config/system.xml"))
					{
						return;
					}
					XDocument xDocument = XDocument.Load("UserData/config/system.xml");
					string text = xDocument.Element("System").Element("Graphic").Element("rampGUID")?.Value;
					string text2 = xDocument.Element("System").Element("Graphic").Element("rampId")?.Value;
					if (!NullCheck.IsNullOrWhiteSpace(text) && !NullCheck.IsNullOrWhiteSpace(text2) && int.TryParse(text2, out var result))
					{
						ResolveInfo resolveInfo = TryGetResolutionInfo(result, "Ramp", text);
						if (resolveInfo == null)
						{
							__instance.rampG = 1;
						}
						else
						{
							__instance.rampG = resolveInfo.LocalSlot;
						}
					}
				}
				catch (Exception ex)
				{
					Debug.LogException(ex);
				}
			}

			internal static void InstallHooks()
			{
				Harmony.CreateAndPatchAll(typeof(Hooks), "Sideloader.UniversalAutoResolver");
				ExtendedSave.CardBeingLoaded += ExtendedCardLoad;
				ExtendedSave.CardBeingSaved += ExtendedCardSave;
				ExtendedSave.CoordinateBeingLoaded += ExtendedCoordinateLoad;
				ExtendedSave.CoordinateBeingSaved += ExtendedCoordinateSave;
				ExtendedSave.CardBeingImported += ExtendedCardImport;
				ExtendedSave.SceneBeingLoaded += ExtendedSceneLoad;
				ExtendedSave.SceneBeingImported += ExtendedSceneImport;
				DoingImport = !BepisPlugins.Constants.InsideStudio;
			}

			internal static void ExtendedCardLoad(ChaFile file)
			{
				string text = file.charaFileName;
				if (NullCheck.IsNullOrEmpty(text))
				{
					ChaFileParameter parameter = file.parameter;
					text = ((parameter == null) ? null : parameter.fullname?.Trim());
				}
				Sideloader.Logger.LogDebug((object)("Loading card [" + text + "]"));
				PluginData pluginData = ExtendedSave.GetExtendedDataById(file, "EC.Core.Sideloader.UniversalAutoResolver") ?? ExtendedSave.GetExtendedDataById(file, "com.bepis.sideloader.universalautoresolver");
				List<ResolveInfo> list;
				if (pluginData == null || !pluginData.data.ContainsKey("info"))
				{
					Sideloader.Logger.LogDebug((object)"No sideloader marker found");
					list = null;
				}
				else
				{
					object[] source = (object[])pluginData.data["info"];
					list = source.Select((object x) => ResolveInfo.Deserialize((byte[])x)).ToList();
					Sideloader.Logger.LogDebug((object)$"Sideloader marker found, external info count: {list.Count}");
					if (Sideloader.DebugLogging.Value)
					{
						foreach (ResolveInfo item in list)
						{
							Sideloader.Logger.LogDebug((object)$"External info: {item.GUID} : {item.Property} : {item.Slot}");
						}
					}
				}
				IterateCardPrefixes(ResolveStructure, file, list);
			}

			internal static void ExtendedCardSave(ChaFile file)
			{
				List<ResolveInfo> resolutionInfo;
				if (!DoingImport)
				{
					resolutionInfo = new List<ResolveInfo>();
					IterateCardPrefixes(IterateStruct, file, null);
					ExtendedSave.SetExtendedDataById(file, "com.bepis.sideloader.universalautoresolver", new PluginData
					{
						data = new Dictionary<string, object> { ["info"] = resolutionInfo.Select((ResolveInfo x) => x.Serialize()).ToList() }
					});
				}
				void IterateStruct(Dictionary<CategoryProperty, StructValue<int>> dict, object obj, IEnumerable<ResolveInfo> extInfo, string propertyPrefix = "")
				{
					//IL_0041: Unknown result type (might be due to invalid IL or missing references)
					//IL_005f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0066: Expected O, but got Unknown
					//IL_006d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0079: Invalid comparison between Unknown and I4
					foreach (KeyValuePair<CategoryProperty, StructValue<int>> item in dict)
					{
						int num = item.Value.GetMethod(obj);
						if (num != 0 && (num >= 100000000 || !Lists.InternalDataList[item.Key.Category].ContainsKey(num)))
						{
							if (obj is PartsInfo)
							{
								PartsInfo val = (PartsInfo)obj;
								if ((int)item.Key.Category != val.type)
								{
									continue;
								}
							}
							else if (item.Key.Prefix == StructReference.AccessoryPropPrefix)
							{
								continue;
							}
							ResolveInfo resolveInfo = TryGetResolutionInfo(item.Key.ToString(), num);
							if (resolveInfo != null)
							{
								ResolveInfo resolveInfo2 = CopyExtensions.DeepCopy<ResolveInfo>(resolveInfo);
								resolveInfo2.Property = propertyPrefix + resolveInfo2.Property;
								item.Value.SetMethod(obj, resolveInfo2.Slot);
								resolutionInfo.Add(resolveInfo2);
							}
						}
					}
				}
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFile), "SaveFile", new Type[]
			{
				typeof(BinaryWriter),
				typeof(bool)
			})]
			private static void ChaFileSaveFilePostHook(ChaFile __instance)
			{
				if (DoingImport)
				{
					return;
				}
				string text = __instance.charaFileName;
				if (NullCheck.IsNullOrEmpty(text))
				{
					ChaFileParameter parameter = __instance.parameter;
					text = ((parameter == null) ? null : parameter.fullname?.Trim());
				}
				PluginData pluginData = ExtendedSave.GetExtendedDataById(__instance, "EC.Core.Sideloader.UniversalAutoResolver") ?? ExtendedSave.GetExtendedDataById(__instance, "com.bepis.sideloader.universalautoresolver");
				List<ResolveInfo> list;
				if (pluginData.data["info"] is List<byte[]> source)
				{
					list = source.Select(ResolveInfo.Deserialize).ToList();
				}
				else
				{
					if (!(pluginData.data["info"] is object[] source2))
					{
						Sideloader.Logger.LogError((object)string.Format("Reloading card [{0}] failed - Unknown data type:{1}", text, pluginData.data["info"]?.GetType()));
						return;
					}
					list = source2.Select((object x) => ResolveInfo.Deserialize((byte[])x)).ToList();
				}
				Sideloader.Logger.LogDebug((object)$"Reloading card [{text}] - External info count: {list.Count}");
				if (Sideloader.DebugLogging.Value)
				{
					foreach (ResolveInfo item in list)
					{
						Sideloader.Logger.LogDebug((object)$"External info: {item.GUID} : {item.Property} : {item.Slot}");
					}
				}
				Dictionary<string, ResolveInfo> extInfoLookup = new Dictionary<string, ResolveInfo>();
				for (int num = list.Count - 1; num >= 0; num--)
				{
					ResolveInfo resolveInfo = list[num];
					extInfoLookup[resolveInfo.Property] = resolveInfo;
				}
				IterateCardPrefixes(ResetStructResolveStructure, __instance, list);
				void ResetStructResolveStructure(Dictionary<CategoryProperty, StructValue<int>> propertyDict, object structure, IEnumerable<ResolveInfo> extInfo2, string propertyPrefix = "")
				{
					foreach (KeyValuePair<CategoryProperty, StructValue<int>> item2 in propertyDict)
					{
						extInfoLookup.TryGetValue($"{propertyPrefix}{item2.Key}", out var value);
						if (value != null)
						{
							item2.Value.SetMethod(structure, value.LocalSlot);
						}
					}
				}
			}

			internal static void ExtendedCoordinateLoad(ChaFileCoordinate file)
			{
				Sideloader.Logger.LogDebug((object)("Loading coordinate [" + file.coordinateName + "]"));
				PluginData pluginData = ExtendedSave.GetExtendedDataById(file, "EC.Core.Sideloader.UniversalAutoResolver") ?? ExtendedSave.GetExtendedDataById(file, "com.bepis.sideloader.universalautoresolver");
				List<ResolveInfo> list;
				if (pluginData == null || !pluginData.data.ContainsKey("info"))
				{
					Sideloader.Logger.LogDebug((object)"No sideloader marker found");
					list = null;
				}
				else
				{
					object[] source = (object[])pluginData.data["info"];
					list = source.Select((object x) => ResolveInfo.Deserialize((byte[])x)).ToList();
					Sideloader.Logger.LogDebug((object)$"Sideloader marker found, external info count: {list.Count}");
					if (Sideloader.DebugLogging.Value)
					{
						foreach (ResolveInfo item in list)
						{
							Sideloader.Logger.LogDebug((object)$"External info: {item.GUID} : {item.Property} : {item.Slot}");
						}
					}
				}
				IterateCoordinatePrefixes(ResolveStructure, file, list);
			}

			internal static void ExtendedCoordinateSave(ChaFileCoordinate file)
			{
				List<ResolveInfo> resolutionInfo;
				if (!DoingImport)
				{
					resolutionInfo = new List<ResolveInfo>();
					IterateCoordinatePrefixes(IterateStruct, file, null);
					ExtendedSave.SetExtendedDataById(file, "com.bepis.sideloader.universalautoresolver", new PluginData
					{
						data = new Dictionary<string, object> { ["info"] = resolutionInfo.Select((ResolveInfo x) => x.Serialize()).ToList() }
					});
				}
				void IterateStruct(Dictionary<CategoryProperty, StructValue<int>> dict, object obj, IEnumerable<ResolveInfo> extInfo, string propertyPrefix = "")
				{
					//IL_0041: Unknown result type (might be due to invalid IL or missing references)
					//IL_005f: Unknown result type (might be due to invalid IL or missing references)
					//IL_0066: Expected O, but got Unknown
					//IL_006d: Unknown result type (might be due to invalid IL or missing references)
					//IL_0079: Invalid comparison between Unknown and I4
					foreach (KeyValuePair<CategoryProperty, StructValue<int>> item in dict)
					{
						int num = item.Value.GetMethod(obj);
						if (num != 0 && (num >= 100000000 || !Lists.InternalDataList[item.Key.Category].ContainsKey(num)))
						{
							if (obj is PartsInfo)
							{
								PartsInfo val = (PartsInfo)obj;
								if ((int)item.Key.Category != val.type)
								{
									continue;
								}
							}
							else if (item.Key.Prefix == StructReference.AccessoryPropPrefix)
							{
								continue;
							}
							ResolveInfo resolveInfo = TryGetResolutionInfo(item.Key.ToString(), num);
							if (resolveInfo != null)
							{
								ResolveInfo resolveInfo2 = CopyExtensions.DeepCopy<ResolveInfo>(resolveInfo);
								resolveInfo2.Property = propertyPrefix + resolveInfo2.Property;
								item.Value.SetMethod(obj, resolveInfo2.Slot);
								resolutionInfo.Add(resolveInfo2);
							}
						}
					}
				}
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ChaFileCoordinate), "SaveFile", new Type[] { typeof(string) })]
			private static void ChaFileCoordinateSaveFilePostHook(ChaFileCoordinate __instance, string path)
			{
				if (DoingImport)
				{
					return;
				}
				Sideloader.Logger.LogDebug((object)("Reloading coordinate [" + path + "]"));
				PluginData pluginData = ExtendedSave.GetExtendedDataById(__instance, "EC.Core.Sideloader.UniversalAutoResolver") ?? ExtendedSave.GetExtendedDataById(__instance, "com.bepis.sideloader.universalautoresolver");
				List<byte[]> source = (List<byte[]>)pluginData.data["info"];
				List<ResolveInfo> extInfo3 = source.Select(ResolveInfo.Deserialize).ToList();
				Sideloader.Logger.LogDebug((object)$"External info count: {extInfo3.Count}");
				if (Sideloader.DebugLogging.Value)
				{
					foreach (ResolveInfo item in extInfo3)
					{
						Sideloader.Logger.LogDebug((object)$"External info: {item.GUID} : {item.Property} : {item.Slot}");
					}
				}
				IterateCoordinatePrefixes(ResetStructResolveStructure, __instance, extInfo3);
				void ResetStructResolveStructure(Dictionary<CategoryProperty, StructValue<int>> propertyDict, object structure, IEnumerable<ResolveInfo> extInfo2, string propertyPrefix = "")
				{
					foreach (KeyValuePair<CategoryProperty, StructValue<int>> kv in propertyDict)
					{
						ResolveInfo resolveInfo = extInfo3.FirstOrDefault((ResolveInfo x) => x.Property == $"{propertyPrefix}{kv.Key}");
						if (resolveInfo != null)
						{
							kv.Value.SetMethod(structure, resolveInfo.LocalSlot);
						}
					}
				}
			}

			/// <summary>
			/// Translate the value (selected index) to the actual ID of the filter. This allows us to save the ID to the scene.
			/// Without this, the index is saved which will be different depending on installed mods and make it impossible to save and load correctly.
			/// </summary>
			[HarmonyPrefix]
			[HarmonyPatch(typeof(AmplifyColorEffectInfo), "OnValueChangedLut")]
			internal static void OnValueChangedLutPrefix(ref int _value)
			{
				int num = 0;
				foreach (KeyValuePair<int, LoadCommonInfo> item in Singleton<Info>.Instance.dicFilterLoadInfo)
				{
					if (num == _value)
					{
						_value = item.Key;
						break;
					}
					num++;
				}
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(AmplifyColorEffectInfo), "UpdateInfo")]
			internal static void ACEUpdateInfoPostfix(AmplifyColorEffectInfo __instance)
			{
				int num = 0;
				foreach (KeyValuePair<int, LoadCommonInfo> item in Singleton<Info>.Instance.dicFilterLoadInfo)
				{
					if (item.Key == Singleton<Studio>.Instance.sceneInfo.aceNo)
					{
						__instance.dropdownLut.value = num;
						break;
					}
					num++;
				}
			}

			[HarmonyPostfix]
			[HarmonyPatch(typeof(EtcInfo), "UpdateInfo")]
			internal static void ETCUpdateInfoPostfix(EtcInfo __instance)
			{
				int num = 0;
				foreach (KeyValuePair<int, ListInfoBase> item in Lists.InternalDataList[(CategoryNo)432])
				{
					if (item.Key == Singleton<Studio>.Instance.sceneInfo.rampG)
					{
						__instance.dropdownRamp.value = num;
						break;
					}
					num++;
				}
			}

			internal static void ExtendedSceneLoad(string path)
			{
				PluginData sceneExtendedDataById = ExtendedSave.GetSceneExtendedDataById("com.bepis.sideloader.universalautoresolver");
				ResolveStudioObjects(sceneExtendedDataById, ResolveType.Load);
				ResolveStudioMap(sceneExtendedDataById, ResolveType.Load);
				ResolveStudioFilter(sceneExtendedDataById, ResolveType.Load);
				ResolveStudioRamp(sceneExtendedDataById, ResolveType.Load);
				ResolveStudioBGM(sceneExtendedDataById, ResolveType.Load);
			}

			internal static void ExtendedSceneImport(string path)
			{
				PluginData sceneExtendedDataById = ExtendedSave.GetSceneExtendedDataById("com.bepis.sideloader.universalautoresolver");
				Dictionary<int, ObjectInfo> dictionary = StudioObjectSearch.FindObjectInfo(StudioObjectSearch.SearchType.All);
				Dictionary<int, int> source = StudioObjectSearch.FindObjectInfoOrder(StudioObjectSearch.SearchType.Import, typeof(OIItemInfo));
				if (sceneExtendedDataById != null && sceneExtendedDataById.data.ContainsKey("patternInfo"))
				{
					List<StudioPatternResolveInfo> list = ((object[])sceneExtendedDataById.data["patternInfo"]).Select((object x) => StudioPatternResolveInfo.Deserialize((byte[])x)).ToList();
					foreach (StudioPatternResolveInfo extPatternResolve in list)
					{
						int key = (from x in source
							where x.Value == extPatternResolve.ObjectOrder
							select x.Key).FirstOrDefault();
						ObjectInfo obj = dictionary[key];
						OIItemInfo val = (OIItemInfo)(object)((obj is OIItemInfo) ? obj : null);
						if (val != null)
						{
							ResolveStudioObjectPattern(extPatternResolve, (ObjectInfo)(object)val);
						}
					}
				}
				if (sceneExtendedDataById != null && sceneExtendedDataById.data.ContainsKey("itemInfo"))
				{
					List<StudioResolveInfo> list2 = ((object[])sceneExtendedDataById.data["itemInfo"]).Select((object x) => StudioResolveInfo.Deserialize((byte[])x)).ToList();
					Dictionary<int, int> source2 = StudioObjectSearch.FindObjectInfoOrder(StudioObjectSearch.SearchType.Import, typeof(OILightInfo));
					Dictionary<int, int> source3 = StudioObjectSearch.FindObjectInfoOrder(StudioObjectSearch.SearchType.Import, typeof(OICharInfo));
					foreach (StudioResolveInfo extResolve in list2)
					{
						int key2 = (from x in source
							where x.Value == extResolve.ObjectOrder
							select x.Key).FirstOrDefault();
						if (!dictionary.TryGetValue(key2, out var value))
						{
							Sideloader.Logger.LogWarning((object)"Corrupted Sideloader data, some items might not load properly");
							continue;
						}
						OIItemInfo val2 = (OIItemInfo)(object)((value is OIItemInfo) ? value : null);
						if (val2 != null)
						{
							ResolveStudioObject(extResolve, (ObjectInfo)(object)val2);
							dictionary.Remove(key2);
							continue;
						}
						key2 = (from x in source2
							where x.Value == extResolve.ObjectOrder
							select x.Key).FirstOrDefault();
						OILightInfo val3 = (OILightInfo)(object)((value is OILightInfo) ? value : null);
						if (val3 != null)
						{
							ResolveStudioObject(extResolve, (ObjectInfo)(object)val3);
							dictionary.Remove(key2);
							continue;
						}
						key2 = (from x in source3
							where x.Value == extResolve.ObjectOrder
							select x.Key).FirstOrDefault();
						OICharInfo val4 = (OICharInfo)(object)((value is OICharInfo) ? value : null);
						if (val4 != null)
						{
							ResolveStudioObject(extResolve, (ObjectInfo)(object)val4);
							dictionary.Remove(key2);
						}
					}
				}
				foreach (ObjectInfo item in from x in dictionary
					where x.Value is OIItemInfo || x.Value is OILightInfo
					select x.Value)
				{
					OIItemInfo val5 = (OIItemInfo)(object)((item is OIItemInfo) ? item : null);
					if (val5 != null)
					{
						ResolveStudioObject((ObjectInfo)(object)val5);
						continue;
					}
					OILightInfo val6 = (OILightInfo)(object)((item is OILightInfo) ? item : null);
					if (val6 != null)
					{
						ResolveStudioObject((ObjectInfo)(object)val6);
					}
				}
			}

			/// <summary>
			/// Before the scene saves, go through every item, map, BGM, etc. in the scene, create extended save data with the GUID and other relevant info,
			/// and restore the IDs back to the original, non-resolved ID for hard mod compatibility
			/// </summary>
			[HarmonyPrefix]
			[HarmonyPatch(typeof(SceneInfo), "Save", new Type[] { typeof(string) })]
			private static void SavePrefix()
			{
				Dictionary<string, object> dictionary = new Dictionary<string, object>();
				List<StudioResolveInfo> list = new List<StudioResolveInfo>();
				List<StudioPatternResolveInfo> list2 = new List<StudioPatternResolveInfo>();
				Dictionary<int, int> dicObjectInfoOrder;
				Dictionary<int, ObjectInfo> source = StudioObjectSearch.FindObjectInfoAndOrder(StudioObjectSearch.SearchType.All, typeof(OIItemInfo), out dicObjectInfoOrder);
				Dictionary<int, int> dictionary2 = StudioObjectSearch.FindObjectInfoOrder(StudioObjectSearch.SearchType.All, typeof(OILightInfo));
				Dictionary<int, int> dictionary3 = StudioObjectSearch.FindObjectInfoOrder(StudioObjectSearch.SearchType.All, typeof(OICharInfo));
				foreach (ObjectInfo item4 in source.Select((KeyValuePair<int, ObjectInfo> x) => x.Value))
				{
					OIItemInfo val = (OIItemInfo)(object)((item4 is OIItemInfo) ? item4 : null);
					if (val != null)
					{
						StudioPatternResolveInfo studioPatternResolveInfo = new StudioPatternResolveInfo
						{
							DicKey = ((ObjectInfo)val).dicKey,
							ObjectOrder = dicObjectInfoOrder[((ObjectInfo)val).dicKey],
							ObjectPatternInfo = new Dictionary<int, StudioPatternResolveInfo.PatternInfo>()
						};
						for (int i = 0; i < val.pattern.Length; i++)
						{
							if (val.pattern[i].key < 100000000)
							{
								continue;
							}
							ResolveInfo resolveInfo = TryGetResolutionInfo((CategoryNo)430, val.pattern[i].key);
							if (resolveInfo != null)
							{
								studioPatternResolveInfo.ObjectPatternInfo[i] = new StudioPatternResolveInfo.PatternInfo
								{
									GUID = resolveInfo.GUID,
									Slot = resolveInfo.Slot,
									LocalSlot = val.pattern[i].key,
									Author = resolveInfo.Author,
									Website = resolveInfo.Website,
									Name = resolveInfo.Name
								};
								if (Sideloader.DebugLogging.Value)
								{
									Sideloader.Logger.LogDebug((object)$"Setting [{((ObjectInfo)val).dicKey}] ID:{val.pattern[i].key}->{resolveInfo.Slot}");
								}
								val.pattern[i].key = resolveInfo.Slot;
							}
						}
						if (studioPatternResolveInfo.ObjectPatternInfo.Count > 0)
						{
							list2.Add(studioPatternResolveInfo);
						}
						if (val.no < 100000000)
						{
							continue;
						}
						StudioResolveInfo studioResolveInfo = GetStudioResolveInfos(val.no, onlyResolveItems: false).FirstOrDefault();
						if (studioResolveInfo != null)
						{
							StudioResolveInfo item = new StudioResolveInfo
							{
								GUID = studioResolveInfo.GUID,
								Slot = studioResolveInfo.Slot,
								LocalSlot = studioResolveInfo.LocalSlot,
								DicKey = ((ObjectInfo)val).dicKey,
								ObjectOrder = dicObjectInfoOrder[((ObjectInfo)val).dicKey],
								Author = studioResolveInfo.Author,
								Website = studioResolveInfo.Website,
								Name = studioResolveInfo.Name
							};
							list.Add(item);
							if (Sideloader.DebugLogging.Value)
							{
								Sideloader.Logger.LogDebug((object)$"Setting [{((ObjectInfo)val).dicKey}] ID:{val.no}->{studioResolveInfo.Slot}");
							}
							val.no = studioResolveInfo.Slot;
						}
						continue;
					}
					OILightInfo val2 = (OILightInfo)(object)((item4 is OILightInfo) ? item4 : null);
					if (val2 != null && val2.no >= 100000000)
					{
						StudioResolveInfo studioResolveInfo2 = GetStudioResolveInfos(val2.no, onlyResolveItems: false).FirstOrDefault();
						if (studioResolveInfo2 != null)
						{
							StudioResolveInfo item2 = new StudioResolveInfo
							{
								GUID = studioResolveInfo2.GUID,
								Slot = studioResolveInfo2.Slot,
								LocalSlot = studioResolveInfo2.LocalSlot,
								DicKey = ((ObjectInfo)val2).dicKey,
								ObjectOrder = dictionary2[((ObjectInfo)val2).dicKey],
								Author = studioResolveInfo2.Author,
								Website = studioResolveInfo2.Website,
								Name = studioResolveInfo2.Name
							};
							list.Add(item2);
							if (Sideloader.DebugLogging.Value)
							{
								Sideloader.Logger.LogDebug((object)$"Setting [{((ObjectInfo)val2).dicKey}] ID:{val2.no}->{studioResolveInfo2.Slot}");
							}
							val2.no = studioResolveInfo2.Slot;
						}
						continue;
					}
					OICharInfo val3 = (OICharInfo)(object)((item4 is OICharInfo) ? item4 : null);
					if (val3 == null || val3.animeInfo.no < 100000000)
					{
						continue;
					}
					StudioResolveInfo studioResolveInfo3 = GetStudioResolveInfos(val3.animeInfo.no, onlyResolveItems: false).FirstOrDefault();
					if (studioResolveInfo3 != null)
					{
						StudioResolveInfo item3 = new StudioResolveInfo
						{
							GUID = studioResolveInfo3.GUID,
							Slot = studioResolveInfo3.Slot,
							LocalSlot = studioResolveInfo3.LocalSlot,
							Group = studioResolveInfo3.Group,
							Category = studioResolveInfo3.Category,
							DicKey = ((ObjectInfo)val3).dicKey,
							ObjectOrder = dictionary3[((ObjectInfo)val3).dicKey],
							Author = studioResolveInfo3.Author,
							Website = studioResolveInfo3.Website,
							Name = studioResolveInfo3.Name
						};
						list.Add(item3);
						if (Sideloader.DebugLogging.Value)
						{
							Sideloader.Logger.LogDebug((object)$"Setting [{((ObjectInfo)val3).dicKey}] AnimationID:{val3.animeInfo.no}->{studioResolveInfo3.Slot}");
						}
						val3.animeInfo.no = studioResolveInfo3.Slot;
					}
				}
				if (!NullCheck.IsNullOrEmpty<StudioResolveInfo>((IReadOnlyCollection<StudioResolveInfo>)list))
				{
					dictionary.Add("itemInfo", list.Select((StudioResolveInfo x) => x.Serialize()).ToList());
				}
				if (!NullCheck.IsNullOrEmpty<StudioPatternResolveInfo>((IReadOnlyCollection<StudioPatternResolveInfo>)list2))
				{
					dictionary.Add("patternInfo", list2.Select((StudioPatternResolveInfo x) => x.Serialize()).ToList());
				}
				int mapID = GetMapID();
				if (mapID > 100000000)
				{
					StudioResolveInfo studioResolveInfo4 = GetStudioResolveInfos(mapID, onlyResolveItems: false).FirstOrDefault();
					if (studioResolveInfo4 != null)
					{
						dictionary.Add("mapInfoGUID", studioResolveInfo4.GUID);
						dictionary.Add("mapInfoAuthor", studioResolveInfo4.Author);
						dictionary.Add("mapInfoWebsite", studioResolveInfo4.Website);
						dictionary.Add("mapInfoName", studioResolveInfo4.Name);
						if (Sideloader.DebugLogging.Value)
						{
							Sideloader.Logger.LogDebug((object)$"Setting Map ID:{mapID}->{studioResolveInfo4.Slot}");
						}
						SetMapID(studioResolveInfo4.Slot);
					}
				}
				int aceNo = Singleton<Studio>.Instance.sceneInfo.aceNo;
				if (aceNo > 100000000)
				{
					StudioResolveInfo studioResolveInfo5 = GetStudioResolveInfos(aceNo, onlyResolveItems: false).FirstOrDefault();
					if (studioResolveInfo5 != null)
					{
						dictionary.Add("filterInfoGUID", studioResolveInfo5.GUID);
						dictionary.Add("filterInfoAuthor", studioResolveInfo5.Author);
						dictionary.Add("filterInfoWebsite", studioResolveInfo5.Website);
						dictionary.Add("filterInfoName", studioResolveInfo5.Name);
						if (Sideloader.DebugLogging.Value)
						{
							Sideloader.Logger.LogDebug((object)$"Setting Filter ID:{aceNo}->{studioResolveInfo5.Slot}");
						}
						Singleton<Studio>.Instance.sceneInfo.aceNo = studioResolveInfo5.Slot;
					}
				}
				int rampG = Singleton<Studio>.Instance.sceneInfo.rampG;
				if (rampG > 100000000)
				{
					ResolveInfo resolveInfo2 = TryGetResolutionInfo("Ramp", rampG);
					if (resolveInfo2 != null)
					{
						dictionary.Add("rampInfoGUID", resolveInfo2.GUID);
						dictionary.Add("rampInfoAuthor", resolveInfo2.Author);
						dictionary.Add("rampInfoWebsite", resolveInfo2.Website);
						dictionary.Add("rampInfoName", resolveInfo2.Name);
						if (Sideloader.DebugLogging.Value)
						{
							Sideloader.Logger.LogDebug((object)$"Setting Ramp ID:{rampG}->{resolveInfo2.Slot}");
						}
						Singleton<Studio>.Instance.sceneInfo.rampG = resolveInfo2.Slot;
					}
				}
				int no = Singleton<Studio>.Instance.sceneInfo.bgmCtrl.no;
				if (no > 100000000)
				{
					StudioResolveInfo studioResolveInfo6 = GetStudioResolveInfos(no, onlyResolveItems: false).FirstOrDefault();
					if (studioResolveInfo6 != null)
					{
						dictionary.Add("bgmInfoGUID", studioResolveInfo6.GUID);
						dictionary.Add("bgmInfoAuthor", studioResolveInfo6.Author);
						dictionary.Add("bgmInfoWebsite", studioResolveInfo6.Website);
						dictionary.Add("bgmInfoName", studioResolveInfo6.Name);
						if (Sideloader.DebugLogging.Value)
						{
							Sideloader.Logger.LogDebug((object)$"Setting BGM ID:{no}->{studioResolveInfo6.Slot}");
						}
						Singleton<Studio>.Instance.sceneInfo.bgmCtrl.no = studioResolveInfo6.Slot;
					}
				}
				if (dictionary.Count == 0)
				{
					ExtendedSave.SetSceneExtendedDataById("com.bepis.sideloader.universalautoresolver", null);
					return;
				}
				ExtendedSave.SetSceneExtendedDataById("com.bepis.sideloader.universalautoresolver", new PluginData
				{
					data = dictionary
				});
			}

			/// <summary>
			/// Set item IDs back to the resolved ID
			/// </summary>
			[HarmonyPostfix]
			[HarmonyPatch(typeof(SceneInfo), "Save", new Type[] { typeof(string) })]
			private static void SavePostfix()
			{
				PluginData sceneExtendedDataById = ExtendedSave.GetSceneExtendedDataById("com.bepis.sideloader.universalautoresolver");
				ResolveStudioObjects(sceneExtendedDataById, ResolveType.Save);
				ResolveStudioMap(sceneExtendedDataById, ResolveType.Save);
				ResolveStudioFilter(sceneExtendedDataById, ResolveType.Save);
				ResolveStudioRamp(sceneExtendedDataById, ResolveType.Save);
			}
		}

		internal enum ResolveType
		{
			Save,
			Load
		}

		/// <summary>
		/// Extended save ID
		/// </summary>
		public const string UARExtID = "com.bepis.sideloader.universalautoresolver";

		/// <summary>
		/// Extended save ID used in EmotionCreators once upon a time, no longer used but must still be checked for cards that still use it
		/// </summary>
		public const string UARExtIDOld = "EC.Core.Sideloader.UniversalAutoResolver";

		private static ILookup<int, ResolveInfo> _resolveInfoLookupSlot;

		private static ILookup<int, ResolveInfo> _resolveInfoLookupLocalSlot;

		private static ILookup<string, MigrationInfo> _migrationInfoLookupGUID;

		private static ILookup<int, MigrationInfo> _migrationInfoLookupSlot;

		/// <summary>
		/// The starting point for UAR IDs
		/// </summary>
		public const int BaseSlotID = 100000000;

		private static int CurrentSlotID;

		/// <summary>
		/// Extended save ID for Studio animations saved to characters in scenes
		/// </summary>
		[Obsolete("Unused")]
		public const string UARExtIDStudioAnimation = "com.bepis.sideloader.universalautoresolver.studioanimation";

		/// <summary>
		/// All loaded StudioResolveInfo
		/// </summary>
		[Obsolete("Use GetStudioResolveInfos and AddStudioResolutionInfo instead")]
		public static List<StudioResolveInfo> LoadedStudioResolutionInfo;

		private static int LastLoadedStudioResolutionInfoCount;

		/// <summary>
		/// LocalSlot -&gt; resolve info lookup.
		/// </summary>
		internal static Dictionary<int, List<StudioResolveInfo>> StudioResolutionInfoLocalSlotLookup;

		/// <summary>
		/// GUID + Slot -&gt; resolve info lookup.
		/// </summary>
		internal static Dictionary<string, Dictionary<int, List<StudioResolveInfo>>> StudioResolutionInfoGuidLookup;

		private static readonly ICollection<StudioResolveInfo> _EmptyResolveInfos;

		/// <summary>
		/// All loaded ResolveInfos.
		/// Use TryGetResolutionInfo if you need to find a specific item since it's much faster.
		/// </summary>
		public static IEnumerable<ResolveInfo> LoadedResolutionInfo { get; private set; }

		/// <summary>
		/// Find the field containing the map ID for cross version compatibility since this was changed in an update to AI Girl and is different between KK and AI/HS2
		/// </summary>
		private static Traverse MapIDField
		{
			get
			{
				if (Traverse.Create((object)Singleton<Studio>.Instance.sceneInfo).Field("map").FieldExists())
				{
					return Traverse.Create((object)Singleton<Studio>.Instance.sceneInfo).Field("map");
				}
				return Traverse.Create((object)Singleton<Studio>.Instance.sceneInfo).Field("mapInfo").Field("no");
			}
		}

		/// <summary>
		/// Get a new unique slot ID above <see cref="F:Sideloader.AutoResolver.UniversalAutoResolver.BaseSlotID" />. Returns a different unique ID on every call.
		/// </summary>
		public static int GetUniqueSlotID()
		{
			return Interlocked.Increment(ref CurrentSlotID);
		}

		static UniversalAutoResolver()
		{
			LoadedResolutionInfo = new ResolveInfo[0];
			LoadedStudioResolutionInfo = new List<StudioResolveInfo>();
			LastLoadedStudioResolutionInfoCount = 0;
			StudioResolutionInfoLocalSlotLookup = new Dictionary<int, List<StudioResolveInfo>>();
			StudioResolutionInfoGuidLookup = new Dictionary<string, Dictionary<int, List<StudioResolveInfo>>>(StringComparer.OrdinalIgnoreCase);
			_EmptyResolveInfos = new StudioResolveInfo[0];
			if (Sideloader.RandomizeSlotIds.Value)
			{
				int num = new Random().Next(0, 1000);
				CurrentSlotID = 100000000 + num;
				Sideloader.Logger.LogDebug((object)("Starting Slot IDs at " + CurrentSlotID));
			}
		}

		/// <summary>
		/// Get the ResolveInfo for an item
		/// </summary>
		/// <param name="property">Property as defined in StructReference</param>
		/// <param name="localSlot">Current (resolved) ID of the item</param>
		/// <returns>ResolveInfo</returns>
		public static ResolveInfo TryGetResolutionInfo(string property, int localSlot)
		{
			return _resolveInfoLookupLocalSlot?[localSlot].FirstOrDefault((ResolveInfo x) => x.Property == property);
		}

		public static ResolveInfo TryGetResolutionInfo(CategoryNo categoryNo, int localSlot)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			return _resolveInfoLookupLocalSlot?[localSlot].FirstOrDefault((ResolveInfo x) => x.CategoryNo == categoryNo);
		}

		public static ResolveInfo TryGetResolutionInfo(int slot, string property, CategoryNo categoryNo)
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			return _resolveInfoLookupSlot?[slot].FirstOrDefault((ResolveInfo x) => x.Property == property && x.CategoryNo == categoryNo);
		}

		public static ResolveInfo TryGetResolutionInfo(int slot, CategoryNo categoryNo, string guid)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			guid = guid?.Trim();
			return _resolveInfoLookupSlot?[slot].FirstOrDefault((ResolveInfo x) => x.CategoryNo == categoryNo && x.GUID == guid);
		}

		/// <summary>
		/// Get the ResolveInfo for an item
		/// </summary>
		/// <param name="slot">Original ID as defined in the list file</param>
		/// <param name="property"></param>
		/// <param name="guid"></param>
		/// <returns>ResolveInfo</returns>
		public static ResolveInfo TryGetResolutionInfo(int slot, string property, string guid)
		{
			guid = guid?.Trim();
			return _resolveInfoLookupSlot?[slot].FirstOrDefault((ResolveInfo x) => x.Property == property && x.GUID == guid);
		}

		public static ResolveInfo TryGetResolutionInfo(int slot, string property, CategoryNo categoryNo, string guid)
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			guid = guid?.Trim();
			return _resolveInfoLookupSlot?[slot].FirstOrDefault((ResolveInfo x) => x.Property == property && x.CategoryNo == categoryNo && x.GUID == guid);
		}

		/// <summary>
		/// Get all MigrationInfo for the GUID
		/// </summary>
		/// <param name="guidOld">GUID that will be migrated</param>
		/// <returns>A list of MigrationInfo</returns>
		public static List<MigrationInfo> GetMigrationInfo(string guidOld)
		{
			return _migrationInfoLookupGUID?[guidOld?.Trim()].ToList();
		}

		/// <summary>
		/// Get all MigrationInfo for the ID
		/// </summary>
		/// <param name="idOld">ID that will be migrated</param>
		/// <returns>A list of MigrationInfo</returns>
		public static List<MigrationInfo> GetMigrationInfo(int idOld)
		{
			return _migrationInfoLookupSlot?[idOld].ToList();
		}

		internal static void SetResolveInfos(ICollection<ResolveInfo> results)
		{
			_resolveInfoLookupSlot = results.ToLookup((ResolveInfo info) => info.Slot);
			_resolveInfoLookupLocalSlot = results.ToLookup((ResolveInfo info) => info.LocalSlot);
			LoadedResolutionInfo = results;
		}

		internal static void SetMigrationInfos(ICollection<MigrationInfo> results)
		{
			_migrationInfoLookupGUID = results.ToLookup((MigrationInfo info) => info.GUIDOld);
			_migrationInfoLookupSlot = results.ToLookup((MigrationInfo info) => info.IDOld);
		}

		/// <summary>
		/// Change the ID of items saved to a card to their resolved IDs
		/// </summary>
		internal static void ResolveStructure(Dictionary<CategoryProperty, StructValue<int>> propertyDict, object structure, ICollection<ResolveInfo> extInfo, string propertyPrefix = "")
		{
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Expected O, but got Unknown
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Invalid comparison between Unknown and I4
			//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0187: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
			foreach (KeyValuePair<CategoryProperty, StructValue<int>> item in propertyDict)
			{
				string property = $"{propertyPrefix}{item.Key}";
				if (structure is PartsInfo)
				{
					PartsInfo val = (PartsInfo)structure;
					if ((int)item.Key.Category != val.type)
					{
						continue;
					}
				}
				else if (item.Key.Prefix == StructReference.AccessoryPropPrefix)
				{
					continue;
				}
				ResolveInfo resolveInfo = extInfo?.FirstOrDefault((ResolveInfo x) => x.Property == property);
				if (resolveInfo == null)
				{
					CompatibilityResolve(item, structure);
					continue;
				}
				if (Sideloader.MigrationEnabled.Value)
				{
					MigrateData(resolveInfo);
				}
				if (NullCheck.IsNullOrWhiteSpace(resolveInfo.GUID))
				{
					CompatibilityResolve(item, structure);
					continue;
				}
				int slot = resolveInfo.Slot;
				CategoryProperty key = item.Key;
				ResolveInfo resolveInfo2 = TryGetResolutionInfo(slot, key.ToString(), item.Key.Category, resolveInfo.GUID);
				if (resolveInfo2 != null)
				{
					if (Sideloader.DebugLogging.Value)
					{
						Sideloader.Logger.LogDebug((object)$"Resolving {resolveInfo.GUID}:{resolveInfo.Property} from slot {resolveInfo.Slot} to slot {resolveInfo2.LocalSlot}");
					}
					item.Value.SetMethod(structure, resolveInfo2.LocalSlot);
				}
				else if (Lists.InternalDataList[item.Key.Category].ContainsKey(item.Value.GetMethod(structure)))
				{
					Lists.InternalDataList[item.Key.Category][item.Value.GetMethod(structure)].dictInfo.TryGetValue(30, out var value);
					object obj;
					if (value == null)
					{
						obj = null;
					}
					else
					{
						string text = value.Replace("chara/", "").Replace(".unity3d", "");
						key = item.Key;
						obj = text.Replace(((object)(CategoryNo)(ref key.Category)).ToString() + "_", "").Replace("/", "");
					}
					value = (string)obj;
					if (int.TryParse(value, out var _))
					{
						ShowGUIDError(resolveInfo.GUID, resolveInfo.Author, resolveInfo.Website, resolveInfo.Name);
						if (structure is PartsInfo && Sideloader.KeepMissingAccessories.Value && GetNowSceneNames().Any((string sceneName) => sceneName == "CustomScene"))
						{
							item.Value.SetMethod(structure, 1);
						}
						else
						{
							item.Value.SetMethod(structure, 99999999);
						}
					}
					else
					{
						Sideloader.Logger.LogDebug((object)("Missing mod detected [" + resolveInfo.GUID + "] but matching ID found"));
					}
				}
				else
				{
					ShowGUIDError(resolveInfo.GUID, resolveInfo.Author, resolveInfo.Website, resolveInfo.Name);
					if (structure is PartsInfo && Sideloader.KeepMissingAccessories.Value && GetNowSceneNames().Any((string sceneName) => sceneName == "CustomScene"))
					{
						item.Value.SetMethod(structure, 1);
					}
					else
					{
						item.Value.SetMethod(structure, 99999999);
					}
				}
			}
		}

		private static void CompatibilityResolve(KeyValuePair<CategoryProperty, StructValue<int>> kv, object structure)
		{
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0084: Unknown result type (might be due to invalid IL or missing references)
			//IL_012f: Unknown result type (might be due to invalid IL or missing references)
			//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
			if (Lists.InternalDataList[kv.Key.Category].ContainsKey(kv.Value.GetMethod(structure)))
			{
				return;
			}
			int num = kv.Value.GetMethod(structure);
			ResolveInfo resolveInfo = TryGetResolutionInfo(num, kv.Key.ToString(), kv.Key.Category);
			if (resolveInfo != null)
			{
				if (Sideloader.DebugLogging.Value)
				{
					Sideloader.Logger.LogDebug((object)$"Compatibility resolving {resolveInfo.Property} from slot {num} to slot {resolveInfo.LocalSlot}");
				}
				kv.Value.SetMethod(structure, resolveInfo.LocalSlot);
				return;
			}
			MigrationInfo migrationInfo = GetMigrationInfo(num).FirstOrDefault((MigrationInfo x) => x.Category == kv.Key.Category);
			if (migrationInfo != null)
			{
				ResolveInfo resolveInfo2 = TryGetResolutionInfo(migrationInfo.IDNew, kv.Key.ToString(), kv.Key.Category, migrationInfo.GUIDNew);
				if (resolveInfo2 != null)
				{
					Sideloader.Logger.LogInfo((object)$"Migrating {migrationInfo.IDOld} -> {migrationInfo.GUIDNew}:{migrationInfo.IDNew}");
					kv.Value.SetMethod(structure, resolveInfo2.LocalSlot);
					return;
				}
			}
			if (Sideloader.DebugLogging.Value)
			{
				Sideloader.Logger.LogDebug((object)$"Compatibility resolving failed, no match found for ID {kv.Value.GetMethod(structure)} Category {kv.Key.Category}");
			}
			if (structure is PartsInfo && Sideloader.KeepMissingAccessories.Value && GetNowSceneNames().Any((string sceneName) => sceneName == "CustomScene"))
			{
				kv.Value.SetMethod(structure, 1);
			}
		}

		internal static void IterateCardPrefixes(Action<Dictionary<CategoryProperty, StructValue<int>>, object, ICollection<ResolveInfo>, string> action, ChaFile file, ICollection<ResolveInfo> extInfo)
		{
			action(StructReference.ChaFileFaceProperties, file.custom.face, extInfo, "");
			action(StructReference.ChaFileBodyProperties, file.custom.body, extInfo, "");
			action(StructReference.ChaFileHairProperties, file.custom.hair, extInfo, "");
			action(StructReference.ChaFileMakeupProperties, file.custom.face.baseMakeup, extInfo, "");
			for (int i = 0; i < file.coordinate.Length; i++)
			{
				ChaFileCoordinate coordinate = file.coordinate[i];
				string prefix = $"outfit{i}.";
				IterateCoordinatePrefixes(action, coordinate, extInfo, file.parameter.sex, prefix);
			}
		}

		internal static void IterateCoordinatePrefixes(Action<Dictionary<CategoryProperty, StructValue<int>>, object, ICollection<ResolveInfo>, string> action, ChaFileCoordinate coordinate, ICollection<ResolveInfo> extInfo, int sex = -1, string prefix = "")
		{
			prefix = (NullCheck.IsNullOrWhiteSpace(prefix) ? string.Empty : prefix);
			action(StructReference.ChaFileClothesProperties, coordinate.clothes, extInfo, prefix);
			for (int i = 0; i < coordinate.accessory.parts.Length; i++)
			{
				string arg = $"{prefix}accessory{i}.";
				action(StructReference.ChaFileAccessoryPartsInfoProperties, coordinate.accessory.parts[i], extInfo, arg);
			}
		}

		internal static void MigrateData(ResolveInfo extResolve)
		{
			if (NullCheck.IsNullOrWhiteSpace(extResolve.GUID))
			{
				return;
			}
			List<MigrationInfo> migrationInfo = GetMigrationInfo(extResolve.GUID);
			if (migrationInfo.Any((MigrationInfo x) => x.MigrationType == MigrationType.StripAll))
			{
				extResolve.GUID = "";
				return;
			}
			foreach (MigrationInfo item in migrationInfo.Where((MigrationInfo x) => x.IDOld == extResolve.Slot && x.Category == extResolve.CategoryNo))
			{
				if (Sideloader.GetManifest(item.GUIDNew) != null)
				{
					Sideloader.Logger.LogInfo((object)$"Migrating {item.GUIDOld}:{item.IDOld} -> {item.GUIDNew}:{item.IDNew}");
					extResolve.GUID = item.GUIDNew;
					extResolve.Slot = item.IDNew;
					return;
				}
			}
			foreach (MigrationInfo item2 in migrationInfo.Where((MigrationInfo x) => x.MigrationType == MigrationType.MigrateAll))
			{
				if (Sideloader.GetManifest(item2.GUIDNew) != null)
				{
					Sideloader.Logger.LogInfo((object)("Migrating GUID " + item2.GUIDOld + " -> " + item2.GUIDNew));
					extResolve.GUID = item2.GUIDNew;
					break;
				}
			}
		}

		internal static void GenerateResolutionInfo(Manifest manifest, ChaListData data, List<ResolveInfo> results)
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_01fa: Unknown result type (might be due to invalid IL or missing references)
			//IL_0168: Unknown result type (might be due to invalid IL or missing references)
			CategoryNo category = (CategoryNo)data.categoryNo;
			List<CategoryProperty> source = StructReference.CollatedStructValues.Keys.Where((CategoryProperty x) => x.Category == category).ToList();
			foreach (KeyValuePair<int, List<string>> kv in data.dictList)
			{
				int newSlot = GetUniqueSlotID();
				if (data.categoryNo == 432)
				{
					if (Sideloader.DebugLoggingResolveInfo.Value)
					{
						Sideloader.Logger.LogInfo((object)("ResolveInfo - GUID: " + manifest.GUID + " Name: " + manifest.Name + " Author: " + manifest.Author + " Website: " + manifest.Website + " " + $"Slot: {int.Parse(kv.Value[0])} " + $"LocalSlot: {newSlot} " + "Property: Ramp " + $"CategoryNo: {category} " + $"Count: {((ICollection<ResolveInfo>)LoadedResolutionInfo).Count}"));
					}
					results.Add(new ResolveInfo
					{
						GUID = manifest.GUID,
						Slot = int.Parse(kv.Value[0]),
						LocalSlot = newSlot,
						Property = "Ramp",
						CategoryNo = category,
						Author = manifest.Author,
						Website = manifest.Website,
						Name = manifest.Name
					});
				}
				else
				{
					results.AddRange(source.Select(delegate(CategoryProperty propertyKey)
					{
						//IL_0183: Unknown result type (might be due to invalid IL or missing references)
						//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
						if (Sideloader.DebugLoggingResolveInfo.Value)
						{
							Sideloader.Logger.LogInfo((object)("ResolveInfo - GUID: " + manifest.GUID + " Name: " + manifest.Name + " Author: " + manifest.Author + "Website: " + manifest.Website + $"Slot: {int.Parse(kv.Value[0])} " + $"LocalSlot: {newSlot} " + $"Property: {propertyKey} " + $"CategoryNo: {category} " + $"Count: {((ICollection<ResolveInfo>)LoadedResolutionInfo).Count}"));
						}
						return new ResolveInfo
						{
							GUID = manifest.GUID,
							Slot = int.Parse(kv.Value[0]),
							LocalSlot = newSlot,
							Property = propertyKey.ToString(),
							CategoryNo = category,
							Author = manifest.Author,
							Website = manifest.Website,
							Name = manifest.Name
						};
					}));
				}
				kv.Value[0] = newSlot.ToString();
			}
		}

		internal static void ShowGUIDError(string guid, string author, string website, string name)
		{
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0110: Unknown result type (might be due to invalid IL or missing references)
			//IL_00af: Unknown result type (might be due to invalid IL or missing references)
			LogLevel val = (LogLevel)(Sideloader.MissingModWarning.Value ? 12 : 4);
			if (LoadedResolutionInfo.Any((ResolveInfo x) => x.GUID == guid))
			{
				Sideloader.Logger.Log(val, (object)("Outdated zipmod! Some items are missing! - " + (string.IsNullOrEmpty(author) ? "" : (author + " : ")) + (string.IsNullOrEmpty(name) ? guid : name)));
				Sideloader.Logger.LogWarning((object)("[UAR] WARNING! Outdated mod detected! [" + guid + "]  " + website));
			}
			else if (StudioResolutionInfoGuidLookup.ContainsKey(guid))
			{
				Sideloader.Logger.Log(val, (object)("Outdated zipmod! Some items are missing! - " + (string.IsNullOrEmpty(author) ? "" : (author + " : ")) + (string.IsNullOrEmpty(name) ? guid : name)));
				Sideloader.Logger.LogWarning((object)("[UAR] WARNING! Outdated mod detected! [" + guid + "]  " + website));
			}
			else
			{
				Sideloader.Logger.Log(val, (object)("Missing zipmod! Some items are missing! - " + (string.IsNullOrEmpty(author) ? "" : (author + " : ")) + (string.IsNullOrEmpty(name) ? guid : name)));
				Sideloader.Logger.LogWarning((object)("[UAR] WARNING! Missing mod detected! [" + guid + "]  " + website));
			}
		}

		private static List<string> GetNowSceneNames()
		{
			return Scene.NowSceneNames;
		}

		public static void AddStudioResolutionInfo(StudioResolveInfo sri)
		{
			UpdateLookupsIfNeeded();
			LoadedStudioResolutionInfo.Add(sri);
			LastLoadedStudioResolutionInfoCount = LoadedStudioResolutionInfo.Count;
			AddToLookups(sri);
		}

		/// <summary>
		/// Handle other plugins adding items to LoadedStudioResolutionInfo directly. Assumes items are added to the end, not inserted.
		/// Necessary for AnimationLoader to work.
		/// </summary>
		private static void UpdateLookupsIfNeeded()
		{
			while (LastLoadedStudioResolutionInfoCount < LoadedStudioResolutionInfo.Count)
			{
				AddToLookups(LoadedStudioResolutionInfo[LastLoadedStudioResolutionInfoCount]);
				LastLoadedStudioResolutionInfoCount++;
			}
		}

		private static void AddToLookups(StudioResolveInfo resolveInfo)
		{
			if (!StudioResolutionInfoLocalSlotLookup.TryGetValue(resolveInfo.LocalSlot, out var value))
			{
				value = new List<StudioResolveInfo>();
				StudioResolutionInfoLocalSlotLookup.Add(resolveInfo.LocalSlot, value);
			}
			value.Add(resolveInfo);
			if (!StudioResolutionInfoGuidLookup.TryGetValue(resolveInfo.GUID, out var value2))
			{
				value2 = new Dictionary<int, List<StudioResolveInfo>>();
				StudioResolutionInfoGuidLookup.Add(resolveInfo.GUID, value2);
			}
			if (!value2.TryGetValue(resolveInfo.Slot, out var value3))
			{
				value3 = new List<StudioResolveInfo>();
				value2.Add(resolveInfo.Slot, value3);
			}
			value3.Add(resolveInfo);
		}

		private static bool IsResolveItem(StudioResolveInfo sri)
		{
			return sri.ResolveItem;
		}

		/// <summary>
		/// Get all resolve infos with a given Local Slot. Optionally only return ResolveItems.
		/// With <paramref name="onlyResolveItems" /><code>==true</code> in most cases returns a single item so it's fine to use FirstOrDefault.
		/// </summary>
		public static IEnumerable<StudioResolveInfo> GetStudioResolveInfos(int localSlot, bool onlyResolveItems)
		{
			UpdateLookupsIfNeeded();
			if (StudioResolutionInfoLocalSlotLookup.TryGetValue(localSlot, out var value))
			{
				if (!onlyResolveItems)
				{
					return value;
				}
				return value.Where(IsResolveItem);
			}
			return _EmptyResolveInfos;
		}

		/// <summary>
		/// Get all resolve infos with a given GUID and Slot. Optionally only return ResolveItems.
		/// With <paramref name="onlyResolveItems" /><code>==true</code> in most cases returns a single item so it's fine to use FirstOrDefault.
		/// </summary>
		public static IEnumerable<StudioResolveInfo> GetStudioResolveInfos(string guid, int slot, bool onlyResolveItems)
		{
			UpdateLookupsIfNeeded();
			if (StudioResolutionInfoGuidLookup.TryGetValue(guid.Trim(), out var value) && value.TryGetValue(slot, out var value2))
			{
				if (!onlyResolveItems)
				{
					return value2;
				}
				return value2.Where(IsResolveItem);
			}
			return _EmptyResolveInfos;
		}

		internal static void GenerateStudioResolutionInfo(Manifest manifest, Lists.StudioListData data)
		{
			if (!data.FileNameWithoutExtension.Contains('_'))
			{
				return;
			}
			string text = data.FileNameWithoutExtension.Split('_')[0].ToLower();
			if (text == "itembonelist")
			{
				foreach (List<string> entry in data.Entries)
				{
					int num = int.Parse(entry[0]);
					int localSlot = GetStudioResolveInfos(manifest.GUID, num, onlyResolveItems: true).FirstOrDefault()?.LocalSlot ?? num;
					AddStudioResolutionInfo(new StudioResolveInfo
					{
						GUID = manifest.GUID,
						Slot = num,
						LocalSlot = localSlot,
						ResolveItem = false,
						Author = manifest.Author,
						Website = manifest.Website,
						Name = manifest.Name
					});
					entry[0] = localSlot.ToString();
				}
				return;
			}
			if (Sideloader.StudioListResolveBlacklist.Contains(text))
			{
				foreach (List<string> entry2 in data.Entries)
				{
					AddStudioResolutionInfo(new StudioResolveInfo
					{
						GUID = manifest.GUID,
						Slot = int.Parse(entry2[0]),
						LocalSlot = int.Parse(entry2[0]),
						ResolveItem = false,
						Author = manifest.Author,
						Website = manifest.Website,
						Name = manifest.Name
					});
				}
				return;
			}
			foreach (List<string> entry3 in data.Entries)
			{
				int uniqueSlotID = GetUniqueSlotID();
				StudioResolveInfo studioResolveInfo = new StudioResolveInfo
				{
					GUID = manifest.GUID,
					Slot = int.Parse(entry3[0]),
					LocalSlot = uniqueSlotID,
					ResolveItem = true,
					Author = manifest.Author,
					Website = manifest.Website,
					Name = manifest.Name
				};
				if (text == "anime" || text == "hanime")
				{
					studioResolveInfo.Group = int.Parse(entry3[1]);
					studioResolveInfo.Category = int.Parse(entry3[2]);
				}
				AddStudioResolutionInfo(studioResolveInfo);
				if (Sideloader.DebugLoggingResolveInfo.Value)
				{
					Sideloader.Logger.LogInfo((object)("StudioResolveInfo - GUID: " + manifest.GUID + " Name: " + manifest.Name + " Author: " + manifest.Author + " Website: " + manifest.Website + " " + $"Slot: {int.Parse(entry3[0])} " + $"LocalSlot: {uniqueSlotID} " + $"Count: {LoadedStudioResolutionInfo.Count}"));
				}
				entry3[0] = uniqueSlotID.ToString();
			}
		}

		internal static void ResolveStudioObjects(PluginData extendedData, ResolveType resolveType)
		{
			Dictionary<int, ObjectInfo> dictionary = StudioObjectSearch.FindObjectInfo(StudioObjectSearch.SearchType.All);
			if (extendedData != null && extendedData.data.ContainsKey("patternInfo"))
			{
				List<StudioPatternResolveInfo> list = ((resolveType != 0) ? ((object[])extendedData.data["patternInfo"]).Select((object x) => StudioPatternResolveInfo.Deserialize((byte[])x)).ToList() : ((List<byte[]>)extendedData.data["patternInfo"]).Select((byte[] x) => StudioPatternResolveInfo.Deserialize(x)).ToList());
				foreach (StudioPatternResolveInfo item in list)
				{
					ResolveStudioObjectPattern(item, dictionary[item.DicKey], resolveType);
				}
			}
			if (extendedData != null && extendedData.data.ContainsKey("itemInfo"))
			{
				List<StudioResolveInfo> list2 = ((resolveType != 0) ? ((object[])extendedData.data["itemInfo"]).Select((object x) => StudioResolveInfo.Deserialize((byte[])x)).ToList() : ((List<byte[]>)extendedData.data["itemInfo"]).Select((byte[] x) => StudioResolveInfo.Deserialize(x)).ToList());
				foreach (StudioResolveInfo item2 in list2)
				{
					ResolveStudioObject(item2, dictionary[item2.DicKey], resolveType);
					dictionary.Remove(item2.DicKey);
				}
			}
			if (resolveType != ResolveType.Load)
			{
				return;
			}
			foreach (ObjectInfo item3 in from x in dictionary
				where x.Value is OIItemInfo || x.Value is OILightInfo || x.Value is OICharInfo
				select x.Value)
			{
				ResolveStudioObject(item3);
			}
		}

		internal static void ResolveStudioObject(StudioResolveInfo extResolve, ObjectInfo OI, ResolveType resolveType = ResolveType.Load)
		{
			OIItemInfo val = (OIItemInfo)(object)((OI is OIItemInfo) ? OI : null);
			if (val != null)
			{
				StudioResolveInfo studioResolveInfoForItem = GetStudioResolveInfoForItem(extResolve, val);
				if (studioResolveInfoForItem != null)
				{
					if (resolveType == ResolveType.Load && Sideloader.DebugLogging.Value)
					{
						Sideloader.Logger.LogDebug((object)$"Resolving (Studio Item) [{extResolve.GUID}] {val.no}->{studioResolveInfoForItem.LocalSlot}");
					}
					val.no = studioResolveInfoForItem.LocalSlot;
				}
				else if (resolveType == ResolveType.Load)
				{
					ShowGUIDError(extResolve.GUID, extResolve.Author, extResolve.Website, extResolve.Name);
				}
				return;
			}
			OILightInfo val2 = (OILightInfo)(object)((OI is OILightInfo) ? OI : null);
			if (val2 != null)
			{
				StudioResolveInfo studioResolveInfo = GetStudioResolveInfos(extResolve.GUID, val2.no, onlyResolveItems: true).FirstOrDefault();
				if (studioResolveInfo != null)
				{
					if (resolveType == ResolveType.Load && Sideloader.DebugLogging.Value)
					{
						Sideloader.Logger.LogDebug((object)$"Resolving (Studio Light) [{extResolve.GUID}] {val2.no}->{studioResolveInfo.LocalSlot}");
					}
					val2.no = studioResolveInfo.LocalSlot;
				}
				else if (resolveType == ResolveType.Load)
				{
					ShowGUIDError(extResolve.GUID, extResolve.Author, extResolve.Website, extResolve.Name);
				}
				return;
			}
			OICharInfo val3 = (OICharInfo)(object)((OI is OICharInfo) ? OI : null);
			if (val3 == null)
			{
				return;
			}
			StudioResolveInfo studioResolveInfo2 = GetStudioResolveInfos(extResolve.GUID, val3.animeInfo.no, onlyResolveItems: true).FirstOrDefault((StudioResolveInfo x) => x.Group == extResolve.Group && x.Category == extResolve.Category);
			if (studioResolveInfo2 != null)
			{
				if (resolveType == ResolveType.Load && Sideloader.DebugLogging.Value)
				{
					Sideloader.Logger.LogDebug((object)$"Resolving (Studio Animation) [{extResolve.GUID}] {val3.animeInfo.group}:{val3.animeInfo.category}:{val3.animeInfo.no}->{studioResolveInfo2.LocalSlot}");
				}
				val3.animeInfo.no = studioResolveInfo2.LocalSlot;
			}
			else if (resolveType == ResolveType.Load)
			{
				ShowGUIDError(extResolve.GUID, extResolve.Author, extResolve.Website, extResolve.Name);
			}
		}

		private static StudioResolveInfo GetStudioResolveInfoForItem(StudioResolveInfo extResolve, OIItemInfo item)
		{
			StudioResolveInfo studioResolveInfo = null;
			foreach (StudioResolveInfo studioResolveInfo2 in GetStudioResolveInfos(extResolve.GUID, item.no, onlyResolveItems: true))
			{
				if (studioResolveInfo2.Group == item.group && studioResolveInfo2.Category == item.category)
				{
					return studioResolveInfo2;
				}
				if (studioResolveInfo == null)
				{
					studioResolveInfo = studioResolveInfo2;
				}
			}
			if (studioResolveInfo != null)
			{
				Sideloader.Logger.LogWarning((object)$"Resolved (Studio Item) [{extResolve.GUID}] {item.no} does not match category/group of its OIItemInfo! Group:{item.group}->{studioResolveInfo.Group} Category:{item.category}->{studioResolveInfo.Category}");
			}
			return studioResolveInfo;
		}

		/// <summary>
		/// Compatibility resolving for objects with no extended save data
		/// </summary>
		internal static void ResolveStudioObject(ObjectInfo OI)
		{
			OIItemInfo Item = (OIItemInfo)(object)((OI is OIItemInfo) ? OI : null);
			if (Item != null)
			{
				if (Lists.InternalStudioItemList.Contains(Item.no))
				{
					return;
				}
				StudioResolveInfo studioResolveInfo = LoadedStudioResolutionInfo.FirstOrDefault((StudioResolveInfo x) => x.ResolveItem && x.Slot == Item.no);
				if (studioResolveInfo != null)
				{
					if (Sideloader.DebugLogging.Value)
					{
						Sideloader.Logger.LogDebug((object)$"Compatibility resolving (Studio Item) {Item.no}->{studioResolveInfo.LocalSlot}");
					}
					Item.no = studioResolveInfo.LocalSlot;
				}
				else
				{
					Sideloader.Logger.Log((LogLevel)12, (object)$"[UAR] Compatibility resolving (Studio Item) failed, no match found for ID {Item.no}");
				}
				return;
			}
			OILightInfo Light = (OILightInfo)(object)((OI is OILightInfo) ? OI : null);
			if (Light != null)
			{
				if (Singleton<Info>.Instance.dicLightLoadInfo.TryGetValue(Light.no, out var _))
				{
					return;
				}
				StudioResolveInfo studioResolveInfo2 = LoadedStudioResolutionInfo.FirstOrDefault((StudioResolveInfo x) => x.ResolveItem && x.Slot == Light.no);
				if (studioResolveInfo2 != null)
				{
					if (Sideloader.DebugLogging.Value)
					{
						Sideloader.Logger.LogDebug((object)$"Compatibility resolving (Studio Light) {Light.no}->{studioResolveInfo2.LocalSlot}");
					}
					Light.no = studioResolveInfo2.LocalSlot;
				}
				else
				{
					Sideloader.Logger.Log((LogLevel)12, (object)$"[UAR] Compatibility resolving (Studio Light) failed, no match found for ID {Light.no}");
				}
				return;
			}
			OICharInfo CharInfo = (OICharInfo)(object)((OI is OICharInfo) ? OI : null);
			if (CharInfo == null)
			{
				return;
			}
			bool flag = false;
			if (Singleton<Info>.Instance.dicAnimeLoadInfo.TryGetValue(CharInfo.animeInfo.group, out var value2) && value2.TryGetValue(CharInfo.animeInfo.category, out var value3) && value3.TryGetValue(CharInfo.animeInfo.no, out var _))
			{
				flag = true;
			}
			if (flag)
			{
				return;
			}
			StudioResolveInfo studioResolveInfo3 = LoadedStudioResolutionInfo.FirstOrDefault((StudioResolveInfo x) => x.ResolveItem && x.Slot == CharInfo.animeInfo.no && x.Group == CharInfo.animeInfo.group && x.Category == CharInfo.animeInfo.category);
			if (studioResolveInfo3 != null)
			{
				if (Sideloader.DebugLogging.Value)
				{
					Sideloader.Logger.LogDebug((object)$"Compatibility resolving (Studio Animation) {CharInfo.animeInfo.no}->{studioResolveInfo3.LocalSlot} Group {CharInfo.animeInfo.group} Category {CharInfo.animeInfo.category}");
				}
				CharInfo.animeInfo.no = studioResolveInfo3.LocalSlot;
			}
			else
			{
				Sideloader.Logger.Log((LogLevel)12, (object)$"[UAR] Compatibility resolving (Studio Animation) failed, no match found for ID {CharInfo.animeInfo} Group {CharInfo.animeInfo.group} Category {CharInfo.animeInfo.category}");
			}
		}

		internal static void ResolveStudioObjectPattern(StudioPatternResolveInfo extResolve, ObjectInfo OI, ResolveType resolveType = ResolveType.Load)
		{
			OIItemInfo val = (OIItemInfo)(object)((OI is OIItemInfo) ? OI : null);
			if (val == null)
			{
				return;
			}
			for (int i = 0; i < val.pattern.Length; i++)
			{
				if (!extResolve.ObjectPatternInfo.TryGetValue(i, out var value))
				{
					continue;
				}
				string text = value.GUID?.Trim();
				ResolveInfo resolveInfo = TryGetResolutionInfo(val.pattern[i].key, (CategoryNo)430, text);
				if (resolveInfo != null)
				{
					if (resolveType == ResolveType.Load && Sideloader.DebugLogging.Value)
					{
						Sideloader.Logger.LogDebug((object)$"Resolving (Studio Item Pattern) [{text}] {val.pattern[i].key}->{resolveInfo.LocalSlot}");
					}
					val.pattern[i].key = resolveInfo.LocalSlot;
				}
				else if (resolveType == ResolveType.Load)
				{
					ShowGUIDError(text, value.Author, value.Website, value.Name);
					val.pattern[i].key = 99999999;
				}
			}
		}

		internal static void ResolveStudioMap(PluginData extData, ResolveType resolveType)
		{
			int MapID = GetMapID();
			if (MapID == -1)
			{
				return;
			}
			if (extData != null && extData.data.ContainsKey("mapInfoGUID"))
			{
				string text = (string)extData.data["mapInfoGUID"];
				string author = null;
				if (extData.data.TryGetValue("mapInfoAuthor", out var value))
				{
					author = (string)value;
				}
				string website = null;
				if (extData.data.TryGetValue("mapInfoWebsite", out var value2))
				{
					website = (string)value2;
				}
				string name = null;
				if (extData.data.TryGetValue("mapInfoName", out var value3))
				{
					name = (string)value3;
				}
				StudioResolveInfo studioResolveInfo = GetStudioResolveInfos(text, MapID, onlyResolveItems: true).FirstOrDefault();
				if (studioResolveInfo != null)
				{
					if (resolveType == ResolveType.Load && Sideloader.DebugLogging.Value)
					{
						Sideloader.Logger.LogDebug((object)$"Resolving (Studio Map) [{text}] {MapID}->{studioResolveInfo.LocalSlot}");
					}
					SetMapID(studioResolveInfo.LocalSlot);
				}
				else
				{
					ShowGUIDError(text, author, website, name);
				}
			}
			else
			{
				if (resolveType != ResolveType.Load || Singleton<Info>.Instance.dicMapLoadInfo.TryGetValue(MapID, out var _))
				{
					return;
				}
				StudioResolveInfo studioResolveInfo2 = LoadedStudioResolutionInfo.FirstOrDefault((StudioResolveInfo x) => x.ResolveItem && x.Slot == MapID);
				if (studioResolveInfo2 != null)
				{
					if (Sideloader.DebugLogging.Value)
					{
						Sideloader.Logger.LogDebug((object)$"Compatibility resolving (Studio Map) {MapID}->{studioResolveInfo2.LocalSlot}");
					}
					SetMapID(studioResolveInfo2.LocalSlot);
				}
				else
				{
					Sideloader.Logger.Log((LogLevel)12, (object)$"[UAR] Compatibility resolving (Studio Map) failed, no match found for ID {MapID}");
				}
			}
		}

		internal static void ResolveStudioFilter(PluginData extData, ResolveType resolveType)
		{
			int filterID = Singleton<Studio>.Instance.sceneInfo.aceNo;
			if (extData != null && extData.data.ContainsKey("filterInfoGUID"))
			{
				string text = (string)extData.data["filterInfoGUID"];
				string author = null;
				if (extData.data.TryGetValue("filterInfoAuthor", out var value))
				{
					author = (string)value;
				}
				string website = null;
				if (extData.data.TryGetValue("filterInfoWebsite", out var value2))
				{
					website = (string)value2;
				}
				string name = null;
				if (extData.data.TryGetValue("filterInfoName", out var value3))
				{
					name = (string)value3;
				}
				StudioResolveInfo studioResolveInfo = GetStudioResolveInfos(text, filterID, onlyResolveItems: true).FirstOrDefault();
				if (studioResolveInfo != null)
				{
					if (resolveType == ResolveType.Load && Sideloader.DebugLogging.Value)
					{
						Sideloader.Logger.LogDebug((object)$"Resolving (Studio Filter) [{text}] {filterID}->{studioResolveInfo.LocalSlot}");
					}
					Singleton<Studio>.Instance.sceneInfo.aceNo = studioResolveInfo.LocalSlot;
				}
				else
				{
					ShowGUIDError(text, author, website, name);
				}
			}
			else
			{
				if (resolveType != ResolveType.Load || Singleton<Info>.Instance.dicFilterLoadInfo.TryGetValue(filterID, out var _))
				{
					return;
				}
				StudioResolveInfo studioResolveInfo2 = LoadedStudioResolutionInfo.FirstOrDefault((StudioResolveInfo x) => x.ResolveItem && x.Slot == filterID);
				if (studioResolveInfo2 != null)
				{
					if (Sideloader.DebugLogging.Value)
					{
						Sideloader.Logger.LogDebug((object)$"Compatibility resolving (Studio Filter) {filterID}->{studioResolveInfo2.LocalSlot}");
					}
					Singleton<Studio>.Instance.sceneInfo.aceNo = studioResolveInfo2.LocalSlot;
				}
				else
				{
					Sideloader.Logger.Log((LogLevel)12, (object)$"[UAR] Compatibility resolving (Studio Filter) failed, no match found for ID {filterID}");
				}
			}
		}

		internal static void ResolveStudioRamp(PluginData extData, ResolveType resolveType)
		{
			int rampID = Singleton<Studio>.Instance.sceneInfo.rampG;
			if (extData != null && extData.data.ContainsKey("rampInfoGUID"))
			{
				string guid = (string)extData.data["rampInfoGUID"];
				string author = null;
				if (extData.data.TryGetValue("rampInfoAuthor", out var value))
				{
					author = (string)value;
				}
				string website = null;
				if (extData.data.TryGetValue("rampInfoWebsite", out var value2))
				{
					website = (string)value2;
				}
				string name = null;
				if (extData.data.TryGetValue("rampInfoName", out var value3))
				{
					name = (string)value3;
				}
				ResolveInfo resolveInfo = TryGetResolutionInfo(rampID, "Ramp", guid);
				if (resolveInfo != null)
				{
					if (resolveType == ResolveType.Load && Sideloader.DebugLogging.Value)
					{
						Sideloader.Logger.LogDebug((object)$"Resolving (Studio Ramp) [{rampID}] {rampID}->{resolveInfo.LocalSlot}");
					}
					Singleton<Studio>.Instance.sceneInfo.rampG = resolveInfo.LocalSlot;
				}
				else
				{
					ShowGUIDError(guid, author, website, name);
				}
			}
			else
			{
				if (resolveType != ResolveType.Load || Lists.InternalDataList[(CategoryNo)432].ContainsKey(rampID))
				{
					return;
				}
				ResolveInfo resolveInfo2 = LoadedResolutionInfo.FirstOrDefault((ResolveInfo x) => x.Property == "Ramp" && x.Slot == rampID);
				if (resolveInfo2 != null)
				{
					if (Sideloader.DebugLogging.Value)
					{
						Sideloader.Logger.LogDebug((object)$"Compatibility resolving (Studio Ramp) {rampID}->{resolveInfo2.LocalSlot}");
					}
					Singleton<Studio>.Instance.sceneInfo.rampG = resolveInfo2.LocalSlot;
				}
				else
				{
					Sideloader.Logger.Log((LogLevel)12, (object)$"[UAR] Compatibility resolving (Studio Ramp) failed, no match found for ID {rampID}");
				}
			}
		}

		internal static void ResolveStudioBGM(PluginData extData, ResolveType resolveType)
		{
			int bgmID = Singleton<Studio>.Instance.sceneInfo.bgmCtrl.no;
			if (bgmID == -1)
			{
				return;
			}
			if (extData != null && extData.data.ContainsKey("bgmInfoGUID"))
			{
				string text = (string)extData.data["bgmInfoGUID"];
				string author = null;
				if (extData.data.TryGetValue("bgmInfoAuthor", out var value))
				{
					author = (string)value;
				}
				string website = null;
				if (extData.data.TryGetValue("bgmInfoWebsite", out var value2))
				{
					website = (string)value2;
				}
				string name = null;
				if (extData.data.TryGetValue("bgmInfoName", out var value3))
				{
					name = (string)value3;
				}
				StudioResolveInfo studioResolveInfo = GetStudioResolveInfos(text, bgmID, onlyResolveItems: true).FirstOrDefault();
				if (studioResolveInfo != null)
				{
					if (resolveType == ResolveType.Load && Sideloader.DebugLogging.Value)
					{
						Sideloader.Logger.LogDebug((object)$"Resolving (Studio BGM) [{text}] {bgmID}->{studioResolveInfo.LocalSlot}");
					}
					Singleton<Studio>.Instance.sceneInfo.bgmCtrl.no = studioResolveInfo.LocalSlot;
				}
				else
				{
					ShowGUIDError(text, author, website, name);
				}
			}
			else
			{
				if (resolveType != ResolveType.Load || Singleton<Info>.Instance.dicBGMLoadInfo.TryGetValue(bgmID, out var _))
				{
					return;
				}
				StudioResolveInfo studioResolveInfo2 = LoadedStudioResolutionInfo.FirstOrDefault((StudioResolveInfo x) => x.ResolveItem && x.Slot == bgmID);
				if (studioResolveInfo2 != null)
				{
					if (Sideloader.DebugLogging.Value)
					{
						Sideloader.Logger.LogDebug((object)$"Compatibility resolving (Studio BGM) {bgmID}->{studioResolveInfo2.LocalSlot}");
					}
					Singleton<Studio>.Instance.sceneInfo.bgmCtrl.no = studioResolveInfo2.LocalSlot;
				}
				else
				{
					Sideloader.Logger.Log((LogLevel)12, (object)$"[UAR] Compatibility resolving (Studio BGM) failed, no match found for ID {bgmID}");
				}
			}
		}

		/// <summary>
		/// Get the current map's ID
		/// </summary>
		/// <returns></returns>
		internal static int GetMapID()
		{
			return (int)MapIDField.GetValue();
		}

		/// <summary>
		/// Set the current map's ID
		/// </summary>
		/// <param name="id"></param>
		internal static void SetMapID(int id)
		{
			MapIDField.SetValue((object)id);
		}
	}
	/// <summary>
	/// Data about the migration to be performed
	/// </summary>
	[MessagePackObject(false)]
	public class MigrationInfo
	{
		/// <summary>
		/// Type of migration to perform
		/// </summary>
		[Key(0)]
		public MigrationType MigrationType;

		/// <summary>
		/// Category of the item
		/// </summary>
		[Key(1)]
		public CategoryNo Category;

		/// <summary>
		/// GUID of the item to perform migration on
		/// </summary>
		[Key(2)]
		public string GUIDOld;

		/// <summary>
		/// GUID to migrate to
		/// </summary>
		[Key(3)]
		public string GUIDNew;

		/// <summary>
		/// ID of the item to perform migration on
		/// </summary>
		[Key(4)]
		public int IDOld;

		/// <summary>
		/// ID to migrate to
		/// </summary>
		[Key(5)]
		public int IDNew;

		[SerializationConstructor]
		public MigrationInfo(MigrationType migrationType, CategoryNo category, string guidOld, string guidNew, int idOld, int idNew)
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			MigrationType = migrationType;
			Category = category;
			GUIDOld = guidOld?.Trim();
			GUIDNew = guidNew?.Trim();
			IDOld = idOld;
			IDNew = idNew;
		}

		public MigrationInfo(MigrationType migrationType, string guidOld, string guidNew)
		{
			MigrationType = migrationType;
			GUIDOld = guidOld?.Trim();
			GUIDNew = guidNew?.Trim();
		}
	}
	/// <summary>
	/// Type of migration that will be performed
	/// </summary>
	public enum MigrationType
	{
		/// <summary>
		/// Change the old GUID to the new GUID and the old ID to the new ID
		/// </summary>
		Migrate,
		/// <summary>
		/// Change the old GUID to the new GUID for all IDs
		/// </summary>
		MigrateAll,
		/// <summary>
		/// Remove the GUID and perform compatibility resolve
		/// </summary>
		StripAll
	}
	/// <summary>
	/// Contains information saved to the card for resolving ID conflicts
	/// </summary>
	[Serializable]
	[MessagePackObject(false)]
	public class ResolveInfo
	{
		[IgnoreMember]
		private string _guid;

		/// <summary>
		/// GUID of the mod as defined in the manifest.xml
		/// </summary>
		[Key("ModID")]
		public string GUID
		{
			get
			{
				return _guid;
			}
			set
			{
				_guid = value?.Trim();
			}
		}

		/// <summary>
		/// ID of the item as defined in the mod's list files
		/// </summary>
		[Key("Slot")]
		public int Slot { get; set; }

		/// <summary>
		/// Resolved item ID. IDs greater than 100000000 are resolved IDs belonging to Sideloader. Use the resolved ID (local slot) to look up the original ID (slot)
		/// </summary>
		[Key("LocalSlot")]
		public int LocalSlot { get; set; }

		/// <summary>
		/// Property of the object as defined in Sideloader's StructReference.
		/// If ever you need to know what to use for this, enable debug resolve info logging and see what Sideloader generates at the start of the game.
		/// </summary>
		[Key("Property")]
		public string Property { get; set; }

		/// <summary>
		/// ChaListDefine.CategoryNo. Typically only used for hard mod resolving in cases where the GUID is not known.
		/// </summary>
		[Key("CategoryNo")]
		public CategoryNo CategoryNo { get; set; }

		/// <summary>
		/// Author of the mod as defined in the manifest.xml
		/// </summary>
		[Key("Author")]
		public string Author { get; set; }

		/// <summary>
		/// Website of the mod as defined in the manifest.xml
		/// </summary>
		[Key("Website")]
		public string Website { get; set; }

		/// <summary>
		/// Display name of the mod as defined in the manifest.xml
		/// </summary>
		[Key("Name")]
		public string Name { get; set; }

		internal static ResolveInfo Deserialize(byte[] data)
		{
			return MessagePackSerializer.Deserialize<ResolveInfo>(data);
		}

		internal byte[] Serialize()
		{
			return MessagePackSerializer.Serialize<ResolveInfo>(this);
		}
	}
	internal struct CategoryProperty
	{
		internal CategoryNo Category;

		internal string Property;

		internal string Prefix;

		internal CategoryProperty(CategoryNo category, string property, string prefix = "")
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			Category = category;
			Property = property;
			Prefix = prefix;
		}

		public override string ToString()
		{
			if (!(Prefix != ""))
			{
				return Property;
			}
			return Prefix + "." + Property;
		}
	}
	internal static class StructReference
	{
		internal static readonly string AccessoryPropPrefix = "ChaFileAccessory.PartsInfo";

		internal static Dictionary<CategoryProperty, StructValue<int>> ChaFileFaceProperties { get; } = ChaFileFaceGenerator();


		internal static Dictionary<CategoryProperty, StructValue<int>> ChaFileBodyProperties { get; } = ChaFileBodyGenerator();


		internal static Dictionary<CategoryProperty, StructValue<int>> ChaFileHairProperties { get; } = ChaFileHairGenerator();


		internal static Dictionary<CategoryProperty, StructValue<int>> ChaFileMakeupProperties { get; } = ChaFileMakeupGenerator();


		internal static Dictionary<CategoryProperty, StructValue<int>> ChaFileClothesProperties { get; } = ChaFileClothesGenerator();


		internal static Dictionary<CategoryProperty, StructValue<int>> ChaFileAccessoryPartsInfoProperties { get; } = ChaFileAccessoryPartsInfoGenerator();


		internal static Dictionary<CategoryProperty, StructValue<int>> CollatedStructValues { get; } = CollatedGenerator();


		private static Dictionary<CategoryProperty, StructValue<int>> GeneratePropertyInfoDictionary(Type t, IEnumerable<CategoryProperty> properties, string prefix = "")
		{
			Dictionary<CategoryProperty, StructValue<int>> dictionary = new Dictionary<CategoryProperty, StructValue<int>>();
			foreach (CategoryProperty property in properties)
			{
				CategoryProperty key = property;
				if (prefix != "")
				{
					key.Prefix = prefix;
				}
				dictionary.Add(key, new StructValue<int>(AccessTools.Property(t, key.Property)));
			}
			return dictionary;
		}

		private static Dictionary<CategoryProperty, StructValue<int>> ChaFileFaceGenerator()
		{
			List<CategoryProperty> properties = new List<CategoryProperty>
			{
				new CategoryProperty((CategoryNo)100, "headId"),
				new CategoryProperty((CategoryNo)400, "detailId"),
				new CategoryProperty((CategoryNo)406, "eyebrowId"),
				new CategoryProperty((CategoryNo)414, "noseId"),
				new CategoryProperty((CategoryNo)410, "hlUpId"),
				new CategoryProperty((CategoryNo)411, "hlDownId"),
				new CategoryProperty((CategoryNo)407, "whiteId"),
				new CategoryProperty((CategoryNo)412, "eyelineUpId"),
				new CategoryProperty((CategoryNo)413, "eyelineDownId"),
				new CategoryProperty((CategoryNo)415, "moleId"),
				new CategoryProperty((CategoryNo)404, "lipLineId")
			};
			Dictionary<CategoryProperty, StructValue<int>> dictionary = GeneratePropertyInfoDictionary(typeof(ChaFileFace), properties, "ChaFileFace");
			dictionary.Add(new CategoryProperty((CategoryNo)408, "Pupil1", "ChaFileFace"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileFace)obj).pupil[0].id = value;
			}, (object obj) => ((ChaFileFace)obj).pupil[0].id));
			dictionary.Add(new CategoryProperty((CategoryNo)408, "Pupil2", "ChaFileFace"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileFace)obj).pupil[1].id = value;
			}, (object obj) => ((ChaFileFace)obj).pupil[1].id));
			dictionary.Add(new CategoryProperty((CategoryNo)409, "PupilGradient1", "ChaFileFace"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileFace)obj).pupil[0].gradMaskId = value;
			}, (object obj) => ((ChaFileFace)obj).pupil[0].gradMaskId));
			dictionary.Add(new CategoryProperty((CategoryNo)409, "PupilGradient2", "ChaFileFace"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileFace)obj).pupil[1].gradMaskId = value;
			}, (object obj) => ((ChaFileFace)obj).pupil[1].gradMaskId));
			return dictionary;
		}

		private static Dictionary<CategoryProperty, StructValue<int>> ChaFileBodyGenerator()
		{
			List<CategoryProperty> properties = new List<CategoryProperty>
			{
				new CategoryProperty((CategoryNo)420, "detailId"),
				new CategoryProperty((CategoryNo)422, "sunburnId"),
				new CategoryProperty((CategoryNo)423, "nipId"),
				new CategoryProperty((CategoryNo)424, "underhairId")
			};
			Dictionary<CategoryProperty, StructValue<int>> dictionary = GeneratePropertyInfoDictionary(typeof(ChaFileBody), properties, "ChaFileBody");
			dictionary.Add(new CategoryProperty((CategoryNo)421, "PaintID1", "ChaFileBody"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileBody)obj).paintId[0] = value;
			}, (object obj) => ((ChaFileBody)obj).paintId[0]));
			dictionary.Add(new CategoryProperty((CategoryNo)421, "PaintID2", "ChaFileBody"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileBody)obj).paintId[1] = value;
			}, (object obj) => ((ChaFileBody)obj).paintId[1]));
			dictionary.Add(new CategoryProperty((CategoryNo)3, "PaintLayoutID1", "ChaFileBody"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileBody)obj).paintLayoutId[0] = value;
			}, (object obj) => ((ChaFileBody)obj).paintLayoutId[0]));
			dictionary.Add(new CategoryProperty((CategoryNo)3, "PaintLayoutID2", "ChaFileBody"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileBody)obj).paintLayoutId[1] = value;
			}, (object obj) => ((ChaFileBody)obj).paintLayoutId[1]));
			return dictionary;
		}

		private static Dictionary<CategoryProperty, StructValue<int>> ChaFileHairGenerator()
		{
			List<CategoryProperty> properties = new List<CategoryProperty>
			{
				new CategoryProperty((CategoryNo)433, "glossId")
			};
			Dictionary<CategoryProperty, StructValue<int>> dictionary = GeneratePropertyInfoDictionary(typeof(ChaFileHair), properties, "ChaFileHair");
			dictionary.Add(new CategoryProperty((CategoryNo)101, "HairBack", "ChaFileHair"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileHair)obj).parts[0].id = value;
			}, (object obj) => ((ChaFileHair)obj).parts[0].id));
			dictionary.Add(new CategoryProperty((CategoryNo)102, "HairFront", "ChaFileHair"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileHair)obj).parts[1].id = value;
			}, (object obj) => ((ChaFileHair)obj).parts[1].id));
			dictionary.Add(new CategoryProperty((CategoryNo)103, "HairSide", "ChaFileHair"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileHair)obj).parts[2].id = value;
			}, (object obj) => ((ChaFileHair)obj).parts[2].id));
			dictionary.Add(new CategoryProperty((CategoryNo)104, "HairOption", "ChaFileHair"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileHair)obj).parts[3].id = value;
			}, (object obj) => ((ChaFileHair)obj).parts[3].id));
			return dictionary;
		}

		private static Dictionary<CategoryProperty, StructValue<int>> ChaFileMakeupGenerator()
		{
			List<CategoryProperty> properties = new List<CategoryProperty>
			{
				new CategoryProperty((CategoryNo)401, "eyeshadowId"),
				new CategoryProperty((CategoryNo)402, "cheekId"),
				new CategoryProperty((CategoryNo)403, "lipId")
			};
			Dictionary<CategoryProperty, StructValue<int>> dictionary = GeneratePropertyInfoDictionary(typeof(ChaFileMakeup), properties, "ChaFileMakeup");
			dictionary.Add(new CategoryProperty((CategoryNo)405, "PaintID1", "ChaFileMakeup"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileMakeup)obj).paintId[0] = value;
			}, (object obj) => ((ChaFileMakeup)obj).paintId[0]));
			dictionary.Add(new CategoryProperty((CategoryNo)405, "PaintID2", "ChaFileMakeup"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileMakeup)obj).paintId[1] = value;
			}, (object obj) => ((ChaFileMakeup)obj).paintId[1]));
			return dictionary;
		}

		private static Dictionary<CategoryProperty, StructValue<int>> ChaFileClothesGenerator()
		{
			Dictionary<CategoryProperty, StructValue<int>> dictionary = new Dictionary<CategoryProperty, StructValue<int>>();
			dictionary.Add(new CategoryProperty((CategoryNo)105, "ClothesTop", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileClothes)obj).parts[0].id = value;
			}, (object obj) => ((ChaFileClothes)obj).parts[0].id));
			dictionary.Add(new CategoryProperty((CategoryNo)106, "ClothesBot", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileClothes)obj).parts[1].id = value;
			}, (object obj) => ((ChaFileClothes)obj).parts[1].id));
			dictionary.Add(new CategoryProperty((CategoryNo)107, "ClothesBra", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileClothes)obj).parts[2].id = value;
			}, (object obj) => ((ChaFileClothes)obj).parts[2].id));
			dictionary.Add(new CategoryProperty((CategoryNo)108, "ClothesShorts", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileClothes)obj).parts[3].id = value;
			}, (object obj) => ((ChaFileClothes)obj).parts[3].id));
			dictionary.Add(new CategoryProperty((CategoryNo)109, "ClothesGloves", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileClothes)obj).parts[4].id = value;
			}, (object obj) => ((ChaFileClothes)obj).parts[4].id));
			dictionary.Add(new CategoryProperty((CategoryNo)110, "ClothesPants", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileClothes)obj).parts[5].id = value;
			}, (object obj) => ((ChaFileClothes)obj).parts[5].id));
			dictionary.Add(new CategoryProperty((CategoryNo)111, "ClothesSocks", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileClothes)obj).parts[6].id = value;
			}, (object obj) => ((ChaFileClothes)obj).parts[6].id));
			dictionary.Add(new CategoryProperty((CategoryNo)112, "ClothesShoesInner", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileClothes)obj).parts[7].id = value;
			}, (object obj) => ((ChaFileClothes)obj).parts[7].id));
			dictionary.Add(new CategoryProperty((CategoryNo)112, "ClothesShoesOuter", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileClothes)obj).parts[8].id = value;
			}, (object obj) => ((ChaFileClothes)obj).parts[8].id));
			dictionary.Add(new CategoryProperty((CategoryNo)210, "ClothesJacketSubA", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileClothes)obj).subPartsId[0] = value;
			}, (object obj) => ((ChaFileClothes)obj).subPartsId[0]));
			dictionary.Add(new CategoryProperty((CategoryNo)211, "ClothesJacketSubB", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileClothes)obj).subPartsId[1] = value;
			}, (object obj) => ((ChaFileClothes)obj).subPartsId[1]));
			dictionary.Add(new CategoryProperty((CategoryNo)212, "ClothesJacketSubC", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileClothes)obj).subPartsId[2] = value;
			}, (object obj) => ((ChaFileClothes)obj).subPartsId[2]));
			dictionary.Add(new CategoryProperty((CategoryNo)200, "ClothesSailorSubA", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileClothes)obj).subPartsId[0] = value;
			}, (object obj) => ((ChaFileClothes)obj).subPartsId[0]));
			dictionary.Add(new CategoryProperty((CategoryNo)201, "ClothesSailorSubB", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileClothes)obj).subPartsId[1] = value;
			}, (object obj) => ((ChaFileClothes)obj).subPartsId[1]));
			dictionary.Add(new CategoryProperty((CategoryNo)202, "ClothesSailorSubC", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileClothes)obj).subPartsId[2] = value;
			}, (object obj) => ((ChaFileClothes)obj).subPartsId[2]));
			dictionary.Add(new CategoryProperty((CategoryNo)431, "ClothesTopEmblem", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileClothes)obj).parts[0].emblemeId = value;
			}, (object obj) => ((ChaFileClothes)obj).parts[0].emblemeId));
			dictionary.Add(new CategoryProperty((CategoryNo)431, "ClothesBotEmblem", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileClothes)obj).parts[1].emblemeId = value;
			}, (object obj) => ((ChaFileClothes)obj).parts[1].emblemeId));
			dictionary.Add(new CategoryProperty((CategoryNo)431, "ClothesBraEmblem", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileClothes)obj).parts[2].emblemeId = value;
			}, (object obj) => ((ChaFileClothes)obj).parts[2].emblemeId));
			dictionary.Add(new CategoryProperty((CategoryNo)431, "ClothesShortsEmblem", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileClothes)obj).parts[3].emblemeId = value;
			}, (object obj) => ((ChaFileClothes)obj).parts[3].emblemeId));
			dictionary.Add(new CategoryProperty((CategoryNo)431, "ClothesGlovesEmblem", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileClothes)obj).parts[4].emblemeId = value;
			}, (object obj) => ((ChaFileClothes)obj).parts[4].emblemeId));
			dictionary.Add(new CategoryProperty((CategoryNo)431, "ClothesPantsEmblem", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileClothes)obj).parts[5].emblemeId = value;
			}, (object obj) => ((ChaFileClothes)obj).parts[5].emblemeId));
			dictionary.Add(new CategoryProperty((CategoryNo)431, "ClothesSocksEmblem", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileClothes)obj).parts[6].emblemeId = value;
			}, (object obj) => ((ChaFileClothes)obj).parts[6].emblemeId));
			dictionary.Add(new CategoryProperty((CategoryNo)431, "ClothesShoesInnerEmblem", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileClothes)obj).parts[7].emblemeId = value;
			}, (object obj) => ((ChaFileClothes)obj).parts[7].emblemeId));
			dictionary.Add(new CategoryProperty((CategoryNo)431, "ClothesShoesOuterEmblem", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				((ChaFileClothes)obj).parts[8].emblemeId = value;
			}, (object obj) => ((ChaFileClothes)obj).parts[8].emblemeId));
			if (typeof(PartsInfo).GetProperties(AccessTools.all).Any((PropertyInfo p) => p.Name == "emblemeId2"))
			{
				dictionary.Add(new CategoryProperty((CategoryNo)431, "ClothesTopEmblem2", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					((ChaFileClothes)obj).parts[0].emblemeId2 = value;
				}, (object obj) => ((ChaFileClothes)obj).parts[0].emblemeId2));
				dictionary.Add(new CategoryProperty((CategoryNo)431, "ClothesBotEmblem2", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					((ChaFileClothes)obj).parts[1].emblemeId2 = value;
				}, (object obj) => ((ChaFileClothes)obj).parts[1].emblemeId2));
				dictionary.Add(new CategoryProperty((CategoryNo)431, "ClothesBraEmblem2", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					((ChaFileClothes)obj).parts[2].emblemeId2 = value;
				}, (object obj) => ((ChaFileClothes)obj).parts[2].emblemeId2));
				dictionary.Add(new CategoryProperty((CategoryNo)431, "ClothesShortsEmblem2", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					((ChaFileClothes)obj).parts[3].emblemeId2 = value;
				}, (object obj) => ((ChaFileClothes)obj).parts[3].emblemeId2));
				dictionary.Add(new CategoryProperty((CategoryNo)431, "ClothesGlovesEmblem2", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					((ChaFileClothes)obj).parts[4].emblemeId2 = value;
				}, (object obj) => ((ChaFileClothes)obj).parts[4].emblemeId2));
				dictionary.Add(new CategoryProperty((CategoryNo)431, "ClothesPantsEmblem2", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					((ChaFileClothes)obj).parts[5].emblemeId2 = value;
				}, (object obj) => ((ChaFileClothes)obj).parts[5].emblemeId2));
				dictionary.Add(new CategoryProperty((CategoryNo)431, "ClothesSocksEmblem2", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					((ChaFileClothes)obj).parts[6].emblemeId2 = value;
				}, (object obj) => ((ChaFileClothes)obj).parts[6].emblemeId2));
				dictionary.Add(new CategoryProperty((CategoryNo)431, "ClothesShoesInnerEmblem2", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					((ChaFileClothes)obj).parts[7].emblemeId2 = value;
				}, (object obj) => ((ChaFileClothes)obj).parts[7].emblemeId2));
				dictionary.Add(new CategoryProperty((CategoryNo)431, "ClothesShoesOuterEmblem2", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					((ChaFileClothes)obj).parts[8].emblemeId = value;
				}, (object obj) => ((ChaFileClothes)obj).parts[8].emblemeId2));
			}
			for (int i = 0; i < 4; i++)
			{
				int index = i;
				dictionary.Add(new CategoryProperty((CategoryNo)430, $"ClothesTopPattern{index}", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					((ChaFileClothes)obj).parts[0].colorInfo[index].pattern = value;
				}, (object obj) => ((ChaFileClothes)obj).parts[0].colorInfo[index].pattern));
				dictionary.Add(new CategoryProperty((CategoryNo)430, $"ClothesBotPattern{index}", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					((ChaFileClothes)obj).parts[1].colorInfo[index].pattern = value;
				}, (object obj) => ((ChaFileClothes)obj).parts[1].colorInfo[index].pattern));
				dictionary.Add(new CategoryProperty((CategoryNo)430, $"ClothesBraPattern{index}", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					((ChaFileClothes)obj).parts[2].colorInfo[index].pattern = value;
				}, (object obj) => ((ChaFileClothes)obj).parts[2].colorInfo[index].pattern));
				dictionary.Add(new CategoryProperty((CategoryNo)430, $"ClothesShortsPattern{index}", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					((ChaFileClothes)obj).parts[3].colorInfo[index].pattern = value;
				}, (object obj) => ((ChaFileClothes)obj).parts[3].colorInfo[index].pattern));
				dictionary.Add(new CategoryProperty((CategoryNo)430, $"ClothesGlovesPattern{index}", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					((ChaFileClothes)obj).parts[4].colorInfo[index].pattern = value;
				}, (object obj) => ((ChaFileClothes)obj).parts[4].colorInfo[index].pattern));
				dictionary.Add(new CategoryProperty((CategoryNo)430, $"ClothesPantsPattern{index}", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					((ChaFileClothes)obj).parts[5].colorInfo[index].pattern = value;
				}, (object obj) => ((ChaFileClothes)obj).parts[5].colorInfo[index].pattern));
				dictionary.Add(new CategoryProperty((CategoryNo)430, $"ClothesSocksPattern{index}", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					((ChaFileClothes)obj).parts[6].colorInfo[index].pattern = value;
				}, (object obj) => ((ChaFileClothes)obj).parts[6].colorInfo[index].pattern));
				dictionary.Add(new CategoryProperty((CategoryNo)430, $"ClothesShoesInnerPattern{index}", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					((ChaFileClothes)obj).parts[7].colorInfo[index].pattern = value;
				}, (object obj) => ((ChaFileClothes)obj).parts[7].colorInfo[index].pattern));
				dictionary.Add(new CategoryProperty((CategoryNo)430, $"ClothesShoesOuterPattern{index}", "ChaFileClothes"), new StructValue<int>(delegate(object obj, int value)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					((ChaFileClothes)obj).parts[8].colorInfo[index].pattern = value;
				}, (object obj) => ((ChaFileClothes)obj).parts[8].colorInfo[index].pattern));
			}
			return dictionary;
		}

		private static Dictionary<CategoryProperty, StructValue<int>> ChaFileAccessoryPartsInfoGenerator()
		{
			List<CategoryProperty> properties = (from CategoryNo x in Enum.GetValues(typeof(CategoryNo))
				select new CategoryProperty(x, "id", AccessoryPropPrefix)).ToList();
			return GeneratePropertyInfoDictionary(typeof(PartsInfo), properties, AccessoryPropPrefix);
		}

		private static Dictionary<CategoryProperty, StructValue<int>> CollatedGenerator()
		{
			Dictionary<CategoryProperty, StructValue<int>> dictionary = new Dictionary<CategoryProperty, StructValue<int>>();
			foreach (KeyValuePair<CategoryProperty, StructValue<int>> chaFileFaceProperty in ChaFileFaceProperties)
			{
				dictionary.Add(chaFileFaceProperty.Key, chaFileFaceProperty.Value);
			}
			foreach (KeyValuePair<CategoryProperty, StructValue<int>> chaFileBodyProperty in ChaFileBodyProperties)
			{
				dictionary.Add(chaFileBodyProperty.Key, chaFileBodyProperty.Value);
			}
			foreach (KeyValuePair<CategoryProperty, StructValue<int>> chaFileHairProperty in ChaFileHairProperties)
			{
				dictionary.Add(chaFileHairProperty.Key, chaFileHairProperty.Value);
			}
			foreach (KeyValuePair<CategoryProperty, StructValue<int>> chaFileClothesProperty in ChaFileClothesProperties)
			{
				dictionary.Add(chaFileClothesProperty.Key, chaFileClothesProperty.Value);
			}
			foreach (KeyValuePair<CategoryProperty, StructValue<int>> chaFileMakeupProperty in ChaFileMakeupProperties)
			{
				dictionary.Add(chaFileMakeupProperty.Key, chaFileMakeupProperty.Value);
			}
			foreach (KeyValuePair<CategoryProperty, StructValue<int>> chaFileAccessoryPartsInfoProperty in ChaFileAccessoryPartsInfoProperties)
			{
				dictionary.Add(chaFileAccessoryPartsInfoProperty.Key, chaFileAccessoryPartsInfoProperty.Value);
			}
			return dictionary;
		}
	}
	internal class StructValue<TValue>
	{
		internal delegate TValue GetterDelegate(object obj);

		internal delegate void SetterDelegate(object obj, TValue value);

		internal SetterDelegate SetMethod { get; set; }

		internal GetterDelegate GetMethod { get; set; }

		internal Type StructType { get; set; }

		internal StructValue(PropertyInfo info)
		{
			GetterDelegate getMethod = CreateGetter(info);
			SetterDelegate setMethod = CreateSetter(info);
			SetMethod = setMethod;
			GetMethod = getMethod;
		}

		internal StructValue(FieldInfo info)
		{
			SetMethod = delegate(object obj, TValue value)
			{
				info.SetValue(obj, value);
			};
			GetMethod = (object obj) => (TValue)info.GetValue(obj);
		}

		internal StructValue(SetterDelegate setMethod, GetterDelegate getMethod)
		{
			SetMethod = setMethod;
			GetMethod = getMethod;
		}

		private static GetterDelegate CreateGetter(PropertyInfo property)
		{
			DynamicMethod dynamicMethod = new DynamicMethod($"{property} Getter", typeof(TValue), new Type[1] { typeof(object) }, restrictedSkipVisibility: true);
			ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Call, property.GetGetMethod(nonPublic: true));
			iLGenerator.Emit(OpCodes.Ret);
			return (GetterDelegate)dynamicMethod.CreateDelegate(typeof(GetterDelegate));
		}

		private static SetterDelegate CreateSetter(PropertyInfo property)
		{
			DynamicMethod dynamicMethod = new DynamicMethod($"{property} Setter", typeof(void), new Type[2]
			{
				typeof(object),
				typeof(TValue)
			}, restrictedSkipVisibility: true);
			ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldarg_1);
			iLGenerator.Emit(OpCodes.Call, property.GetSetMethod(nonPublic: true));
			iLGenerator.Emit(OpCodes.Ret);
			return (SetterDelegate)dynamicMethod.CreateDelegate(typeof(SetterDelegate));
		}
	}
	internal class StudioObjectSearch
	{
		internal enum SearchType
		{
			All,
			Import
		}

		/// <summary>
		/// Returns a dictionary of ObjectInfo.dicKey and their order in a scene for the specified ObjectInfo type.
		/// </summary>
		internal static Dictionary<int, int> FindObjectInfoOrder(SearchType searchType, Type objectType)
		{
			Dictionary<int, ObjectInfo> dicObjectInfo = new Dictionary<int, ObjectInfo>();
			Dictionary<int, int> dicObjectInfoOrder = new Dictionary<int, int>();
			int ObjectOrder = 0;
			Dictionary<int, ObjectInfo> dictionary = ((searchType != 0) ? Singleton<Studio>.Instance.sceneInfo.dicImport : Singleton<Studio>.Instance.sceneInfo.dicObject);
			foreach (KeyValuePair<int, ObjectInfo> item in dictionary)
			{
				if (((object)item.Value).GetType() == objectType)
				{
					dicObjectInfoOrder.Add(item.Key, ObjectOrder++);
				}
				FindObjectsRecursive(item.Value, ref dicObjectInfo, ref dicObjectInfoOrder, ref ObjectOrder, objectType);
			}
			return dicObjectInfoOrder;
		}

		/// <summary>
		/// Returns a dictionary of ObjectInfo.dicKey and ObjectInfo of every ObjectInfo in a scene
		/// </summary>
		internal static Dictionary<int, ObjectInfo> FindObjectInfo(SearchType searchType)
		{
			Dictionary<int, ObjectInfo> dicObjectInfo = new Dictionary<int, ObjectInfo>();
			Dictionary<int, int> dicObjectInfoOrder = new Dictionary<int, int>();
			int ObjectOrder = 0;
			Dictionary<int, ObjectInfo> dictionary = ((searchType != 0) ? Singleton<Studio>.Instance.sceneInfo.dicImport : Singleton<Studio>.Instance.sceneInfo.dicObject);
			foreach (KeyValuePair<int, ObjectInfo> item in dictionary)
			{
				dicObjectInfo.Add(item.Key, item.Value);
				FindObjectsRecursive(item.Value, ref dicObjectInfo, ref dicObjectInfoOrder, ref ObjectOrder, null);
			}
			return dicObjectInfo;
		}

		/// <summary>
		/// Returns a dictionary of ObjectInfo.dicKey and ObjectInfo of every ObjectInfo in a scene.
		/// Also a dictionary of ObjectInfo.dicKey and their order in a scene for the specified ObjectInfo type as an out parameter.
		/// </summary>
		internal static Dictionary<int, ObjectInfo> FindObjectInfoAndOrder(SearchType searchType, Type objectType, out Dictionary<int, int> dicObjectInfoOrder)
		{
			Dictionary<int, ObjectInfo> dicObjectInfo = new Dictionary<int, ObjectInfo>();
			dicObjectInfoOrder = new Dictionary<int, int>();
			int ObjectOrder = 0;
			Dictionary<int, ObjectInfo> dictionary = ((searchType != 0) ? Singleton<Studio>.Instance.sceneInfo.dicImport : Singleton<Studio>.Instance.sceneInfo.dicObject);
			foreach (KeyValuePair<int, ObjectInfo> item in dictionary)
			{
				dicObjectInfo.Add(item.Value.dicKey, item.Value);
				if (objectType != null && ((object)item.Value).GetType() == objectType)
				{
					dicObjectInfoOrder.Add(item.Value.dicKey, ObjectOrder++);
				}
				FindObjectsRecursive(item.Value, ref dicObjectInfo, ref dicObjectInfoOrder, ref ObjectOrder, objectType);
			}
			return dicObjectInfo;
		}

		/// <summary>
		/// Function for finding all ObjectInfo recursively
		/// </summary>
		private static void FindObjectsRecursive(ObjectInfo objectInfo, ref Dictionary<int, ObjectInfo> dicObjectInfo, ref Dictionary<int, int> dicObjectInfoOrder, ref int ObjectOrder, Type objectType)
		{
			OICharInfo val = (OICharInfo)(object)((objectInfo is OICharInfo) ? objectInfo : null);
			if (val == null)
			{
				OIItemInfo val2 = (OIItemInfo)(object)((objectInfo is OIItemInfo) ? objectInfo : null);
				if (val2 == null)
				{
					OIFolderInfo val3 = (OIFolderInfo)(object)((objectInfo is OIFolderInfo) ? objectInfo : null);
					if (val3 == null)
					{
						OIRouteInfo val4 = (OIRouteInfo)(object)((objectInfo is OIRouteInfo) ? objectInfo : null);
						if (val4 == null)
						{
							return;
						}
						{
							foreach (ObjectInfo item in val4.child)
							{
								dicObjectInfo.Add(item.dicKey, item);
								if (objectType != null && ((object)item).GetType() == objectType)
								{
									dicObjectInfoOrder.Add(item.dicKey, ObjectOrder++);
								}
								FindObjectsRecursive(item, ref dicObjectInfo, ref dicObjectInfoOrder, ref ObjectOrder, objectType);
							}
							return;
						}
					}
					{
						foreach (ObjectInfo item2 in val3.child)
						{
							dicObjectInfo.Add(item2.dicKey, item2);
							if (objectType != null && ((object)item2).GetType() == objectType)
							{
								dicObjectInfoOrder.Add(item2.dicKey, ObjectOrder++);
							}
							FindObjectsRecursive(item2, ref dicObjectInfo, ref dicObjectInfoOrder, ref ObjectOrder, objectType);
						}
						return;
					}
				}
				{
					foreach (ObjectInfo item3 in val2.child)
					{
						dicObjectInfo.Add(item3.dicKey, item3);
						if (objectType != null && ((object)item3).GetType() == objectType)
						{
							dicObjectInfoOrder.Add(item3.dicKey, ObjectOrder++);
						}
						FindObjectsRecursive(item3, ref dicObjectInfo, ref dicObjectInfoOrder, ref ObjectOrder, objectType);
					}
					return;
				}
			}
			foreach (KeyValuePair<int, List<ObjectInfo>> item4 in val.child)
			{
				foreach (ObjectInfo item5 in item4.Value)
				{
					dicObjectInfo.Add(item5.dicKey, item5);
					if (objectType != null && ((object)item5).GetType() == objectType)
					{
						dicObjectInfoOrder.Add(item5.dicKey, ObjectOrder++);
					}
					FindObjectsRecursive(item5, ref dicObjectInfo, ref dicObjectInfoOrder, ref ObjectOrder, objectType);
				}
			}
		}
	}
	/// <summary>
	/// Contains information saved to the card for resolving ID conflicts
	/// </summary>
	[Serializable]
	[MessagePackObject(false)]
	public class StudioPatternResolveInfo
	{
		/// <summary>
		/// Information about the patterns
		/// </summary>
		[Serializable]
		[MessagePackObject(false)]
		public class PatternInfo
		{
			/// <summary>
			/// GUID of the mod as defined in the manifest.xml
			/// </summary>
			[Key("GUID")]
			public string GUID;

			/// <summary>
			/// ID of the item as defined in the mod's list files
			/// </summary>
			[Key("Slot")]
			public int Slot;

			/// <summary>
			/// Resolved item ID. IDs greater than 100000000 are resolved IDs belonging to Sideloader. Use the resolved ID (local slot) to look up the original ID (slot)
			/// </summary>
			[Key("LocalSlot")]
			public int LocalSlot;

			/// <summary>
			/// Author of the mod as defined in the manifest.xml
			/// </summary>
			[Key("Author")]
			public string Author;

			/// <summary>
			/// Website of the mod as defined in the manifest.xml
			/// </summary>
			[Key("Website")]
			public string Website;

			/// <summary>
			/// Name of the mod as defined in the manifest.xml
			/// </summary>
			[Key("Name")]
			public string Name;
		}

		/// <summary>
		/// Dictionary key of the item, used on scene load
		/// </summary>
		[Key("SceneDicKey")]
		public int DicKey { get; set; }

		/// <summary>
		/// Order of the item saved to the scene, used on scene import
		/// </summary>
		[Key("SceneObjectOrder")]
		public int ObjectOrder { get; set; }

		/// <summary>
		/// Information about the patterns saved to the item
		/// </summary>
		[Key("ObjectPatternInfo")]
		public Dictionary<int, PatternInfo> ObjectPatternInfo { get; set; }

		internal static StudioPatternResolveInfo Deserialize(byte[] data)
		{
			return MessagePackSerializer.Deserialize<StudioPatternResolveInfo>(data);
		}

		internal byte[] Serialize()
		{
			return MessagePackSerializer.Serialize<StudioPatternResolveInfo>(this);
		}
	}
	/// <summary>
	/// Contains information saved to the card for resolving ID conflicts
	/// </summary>
	[Serializable]
	[MessagePackObject(false)]
	public class StudioResolveInfo
	{
		private string _guid;

		/// <summary>
		/// GUID of the mod as defined in the manifest.xml
		/// </summary>
		[Key("ModID")]
		public string GUID
		{
			get
			{
				return _guid;
			}
			set
			{
				_guid = value?.Trim();
			}
		}

		/// <summary>
		/// ID of the item as defined in the mod's list files
		/// </summary>
		[Key("Slot")]
		public int Slot { get; set; }

		/// <summary>
		/// Resolved item ID. IDs greater than 100000000 are resolved IDs belonging to Sideloader. Use the resolved ID (local slot) to look up the original ID (slot)
		/// </summary>
		[Key("LocalSlot")]
		public int LocalSlot { get; set; }

		/// <summary>
		/// Dictionary key of the item, used on scene load
		/// </summary>
		[Key("SceneDicKey")]
		public int DicKey { get; set; }

		/// <summary>
		/// Order of the item saved to the scene, used on scene import
		/// </summary>
		[Key("SceneObjectOrder")]
		public int ObjectOrder { get; set; }

		/// <summary>
		/// Used to determine if the item should be searched for ID lookups
		/// </summary>
		[Key("ResolveItem")]
		public bool ResolveItem { get; set; }

		/// <summary>
		/// Group of the item, used by animations
		/// </summary>
		[Key("Group")]
		public int Group { get; set; }

		/// <summary>
		/// Category of the item, used by animations
		/// </summary>
		[Key("Category")]
		public int Category { get; set; }

		/// <summary>
		/// Author of the mod as defined in the manifest.
		/// </summary>
		[Key("Author")]
		public string Author { get; set; }

		/// <summary>
		/// /// Author of the mod as defined in the manifest.
		/// </summary>
		[Key("Website")]
		public string Website { get; set; }

		/// <summary>
		/// Display name of the mod as defined in the manifest.xml
		/// </summary>
		[Key("Name")]
		public string Name { get; set; }

		internal static StudioResolveInfo Deserialize(byte[] data)
		{
			return MessagePackSerializer.Deserialize<StudioResolveInfo>(data);
		}

		internal byte[] Serialize()
		{
			return MessagePackSerializer.Serialize<StudioResolveInfo>(this);
		}
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.2.0.10050' (yours is '8.2.0.7535-95108c96')
