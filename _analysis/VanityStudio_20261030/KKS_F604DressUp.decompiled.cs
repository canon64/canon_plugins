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
using System.Text;
using System.Xml;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using ChaCustom;
using ExtensibleSaveFormat;
using KKAPI;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Maker.UI;
using KKAPI.Studio;
using KKAPI.Studio.UI;
using Manager;
using MessagePack;
using Microsoft.CodeAnalysis;
using Studio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: TargetFramework(".NETFramework,Version=v4.6.2", FrameworkDisplayName = ".NET Framework 4.6.2")]
[assembly: AssemblyCompany("KKS_F604DressUp")]
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0")]
[assembly: AssemblyProduct("Freelancer604 Vanity Studio")]
[assembly: AssemblyTitle("KKS_F604DressUp")]
[assembly: AssemblyVersion("1.11.7.0")]
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
namespace Freelancer604.Wardrobe
{
	public sealed class AppearanceCapabilities
	{
		public bool SupportsCustomSliders { get; private set; }

		private AppearanceCapabilities(bool supportsCustomSliders)
		{
			SupportsCustomSliders = supportsCustomSliders;
		}

		public static AppearanceCapabilities ForGame(string gameId)
		{
			return new AppearanceCapabilities(string.Equals(gameId, "HS2", StringComparison.Ordinal));
		}
	}
	public enum AppearanceScopeKind
	{
		Head,
		Body,
		Section
	}
	public sealed class AppearanceScope
	{
		public static readonly AppearanceScope Head = new AppearanceScope(AppearanceScopeKind.Head, null);

		public static readonly AppearanceScope Body = new AppearanceScope(AppearanceScopeKind.Body, null);

		public AppearanceScopeKind Kind { get; private set; }

		public string SectionId { get; private set; }

		public string Identity
		{
			get
			{
				if (Kind == AppearanceScopeKind.Head)
				{
					return "head";
				}
				if (Kind == AppearanceScopeKind.Body)
				{
					return "body";
				}
				return "section:" + SectionId;
			}
		}

		private AppearanceScope(AppearanceScopeKind kind, string sectionId)
		{
			Kind = kind;
			SectionId = sectionId;
		}

		public static AppearanceScope Section(string sectionId)
		{
			if (string.IsNullOrEmpty(sectionId))
			{
				throw new ArgumentException("A section scope needs a stable section identifier.", "sectionId");
			}
			return new AppearanceScope(AppearanceScopeKind.Section, sectionId);
		}
	}
	public interface IAppearanceSchemaMigrator
	{
		int SourceSchemaVersion { get; }

		int TargetSchemaVersion { get; }

		AppearanceSnapshot Migrate(AppearanceSnapshot snapshot);
	}
	public sealed class AppearanceKey : IEquatable<AppearanceKey>
	{
		public string Id { get; private set; }

		public AppearanceKey(string id)
		{
			if (!IsSemantic(id))
			{
				throw new ArgumentException("Appearance keys must be stable semantic identifiers, not raw indices.", "id");
			}
			Id = id;
		}

		public bool Equals(AppearanceKey other)
		{
			if (other != null)
			{
				return string.Equals(Id, other.Id, StringComparison.Ordinal);
			}
			return false;
		}

		public override bool Equals(object obj)
		{
			return Equals(obj as AppearanceKey);
		}

		public override int GetHashCode()
		{
			return StringComparer.Ordinal.GetHashCode(Id);
		}

		private static bool IsSemantic(string id)
		{
			if (string.IsNullOrEmpty(id) || id.IndexOf('.') < 1)
			{
				return false;
			}
			foreach (char c in id)
			{
				if ((c < 'a' || c > 'z') && (c < '0' || c > '9') && c != '.' && c != '_' && c != '-')
				{
					return false;
				}
			}
			return true;
		}
	}
	internal interface IAppearanceDataProvider
	{
		string Id { get; }

		int Order { get; }

		int MinimumShellContractVersion { get; }
	}
	internal interface ICustomSliderProvider
	{
		string Id { get; }

		int Order { get; }

		int MinimumShellContractVersion { get; }
	}
	public sealed class AppearanceSnapshot
	{
		public const int CurrentSchemaVersion = 1;

		public int SchemaVersion { get; set; }

		public string GameId { get; private set; }

		public int SexId { get; private set; }

		public AppearanceScope Scope { get; private set; }

		public IDictionary<AppearanceKey, string> Values { get; private set; }

		public byte[] NativePayload { get; set; }

		public IDictionary<string, byte[]> ProviderPayloads { get; private set; }

		public IDictionary<string, CustomSliderState> CustomSliders { get; private set; }

		public AppearanceSnapshot(string gameId, int sexId, AppearanceScope scope)
		{
			if (string.IsNullOrEmpty(gameId))
			{
				throw new ArgumentException("A game identifier is required.", "gameId");
			}
			if (scope == null)
			{
				throw new ArgumentNullException("scope");
			}
			SchemaVersion = 1;
			GameId = gameId;
			SexId = sexId;
			Scope = scope;
			Values = new Dictionary<AppearanceKey, string>();
			ProviderPayloads = new Dictionary<string, byte[]>(StringComparer.Ordinal);
			CustomSliders = new Dictionary<string, CustomSliderState>(StringComparer.Ordinal);
		}

		public AppearanceSnapshot Clone()
		{
			AppearanceSnapshot appearanceSnapshot = new AppearanceSnapshot(GameId, SexId, Scope);
			appearanceSnapshot.SchemaVersion = SchemaVersion;
			foreach (KeyValuePair<AppearanceKey, string> value in Values)
			{
				appearanceSnapshot.Values.Add(value.Key, value.Value);
			}
			appearanceSnapshot.NativePayload = Copy(NativePayload);
			foreach (KeyValuePair<string, byte[]> providerPayload in ProviderPayloads)
			{
				appearanceSnapshot.ProviderPayloads.Add(providerPayload.Key, Copy(providerPayload.Value));
			}
			foreach (KeyValuePair<string, CustomSliderState> customSlider in CustomSliders)
			{
				appearanceSnapshot.CustomSliders.Add(customSlider.Key, (customSlider.Value == null) ? null : customSlider.Value.Clone());
			}
			return appearanceSnapshot;
		}

		public float GetCustomSliderValue(string sliderId)
		{
			if (string.IsNullOrEmpty(sliderId) || !CustomSliders.TryGetValue(sliderId, out var value) || value == null)
			{
				return 0f;
			}
			return value.Value;
		}

		internal static byte[] Copy(byte[] bytes)
		{
			if (bytes != null)
			{
				return (byte[])bytes.Clone();
			}
			return null;
		}
	}
	public static class AppearanceSnapshotCodec
	{
		private const int Magic = 1095979057;

		private const int CodecVersion = 2;

		private const int MaximumBytes = 1048576;

		private const int MaximumEntries = 4096;

		private const int MaximumStringBytes = 16384;

		public static byte[] Serialize(AppearanceSnapshot snapshot)
		{
			if (snapshot == null)
			{
				throw new ArgumentNullException("snapshot");
			}
			using MemoryStream memoryStream = new MemoryStream();
			using BinaryWriter binaryWriter = new BinaryWriter(memoryStream, Encoding.UTF8);
			WriteInt(binaryWriter, 1095979057);
			WriteInt(binaryWriter, 2);
			WriteInt(binaryWriter, snapshot.SchemaVersion);
			WriteString(binaryWriter, snapshot.GameId);
			WriteInt(binaryWriter, snapshot.SexId);
			WriteInt(binaryWriter, (int)snapshot.Scope.Kind);
			WriteString(binaryWriter, snapshot.Scope.SectionId);
			WriteValues(binaryWriter, snapshot.Values);
			WriteBytes(binaryWriter, snapshot.NativePayload);
			WritePayloads(binaryWriter, snapshot.ProviderPayloads);
			WriteSliders(binaryWriter, snapshot.CustomSliders);
			binaryWriter.Flush();
			return memoryStream.ToArray();
		}

		public static AppearanceSnapshot Deserialize(byte[] data)
		{
			if (data == null || data.Length > 1048576)
			{
				throw new InvalidDataException("Appearance snapshot is invalid.");
			}
			try
			{
				using MemoryStream memoryStream = new MemoryStream(data, writable: false);
				using BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8);
				if (binaryReader.ReadInt32() != 1095979057 || binaryReader.ReadInt32() != 2)
				{
					throw new InvalidDataException("Appearance snapshot format is unsupported.");
				}
				int schemaVersion = binaryReader.ReadInt32();
				string gameId = ReadString(binaryReader);
				int sexId = binaryReader.ReadInt32();
				AppearanceScope scope = ReadScope(binaryReader.ReadInt32(), ReadString(binaryReader));
				AppearanceSnapshot appearanceSnapshot = new AppearanceSnapshot(gameId, sexId, scope);
				appearanceSnapshot.SchemaVersion = schemaVersion;
				ReadValues(binaryReader, appearanceSnapshot.Values);
				appearanceSnapshot.NativePayload = ReadBytes(binaryReader);
				ReadPayloads(binaryReader, appearanceSnapshot.ProviderPayloads);
				ReadSliders(binaryReader, appearanceSnapshot.CustomSliders);
				if (memoryStream.Position != memoryStream.Length)
				{
					throw new InvalidDataException("Appearance snapshot has trailing data.");
				}
				return appearanceSnapshot;
			}
			catch (EndOfStreamException innerException)
			{
				throw new InvalidDataException("Appearance snapshot is truncated.", innerException);
			}
			catch (ArgumentException innerException2)
			{
				throw new InvalidDataException("Appearance snapshot is invalid.", innerException2);
			}
		}

		private static void WriteValues(BinaryWriter writer, IDictionary<AppearanceKey, string> values)
		{
			List<AppearanceKey> list = new List<AppearanceKey>(values.Keys);
			list.Sort(CompareAppearanceKeys);
			WriteCount(writer, list.Count);
			for (int i = 0; i < list.Count; i++)
			{
				WriteString(writer, list[i].Id);
				WriteString(writer, values[list[i]]);
			}
		}

		private static void ReadValues(BinaryReader reader, IDictionary<AppearanceKey, string> values)
		{
			int num = ReadCount(reader);
			for (int i = 0; i < num; i++)
			{
				values.Add(new AppearanceKey(ReadString(reader)), ReadString(reader));
			}
		}

		private static void WritePayloads(BinaryWriter writer, IDictionary<string, byte[]> payloads)
		{
			List<string> list = SortedKeys(payloads);
			WriteCount(writer, list.Count);
			for (int i = 0; i < list.Count; i++)
			{
				WriteString(writer, list[i]);
				WriteBytes(writer, payloads[list[i]]);
			}
		}

		private static void ReadPayloads(BinaryReader reader, IDictionary<string, byte[]> payloads)
		{
			int num = ReadCount(reader);
			for (int i = 0; i < num; i++)
			{
				payloads.Add(RequiredId(ReadString(reader)), ReadBytes(reader));
			}
		}

		private static void WriteSliders(BinaryWriter writer, IDictionary<string, CustomSliderState> sliders)
		{
			List<string> list = SortedKeys(sliders);
			WriteCount(writer, list.Count);
			for (int i = 0; i < list.Count; i++)
			{
				CustomSliderState customSliderState = sliders[list[i]];
				WriteString(writer, list[i]);
				WriteBoolean(writer, customSliderState != null);
				if (customSliderState != null)
				{
					WriteString(writer, customSliderState.DriverKindId);
					WriteSingle(writer, customSliderState.Value);
					WriteBytes(writer, customSliderState.OpaqueState);
				}
			}
		}

		private static void ReadSliders(BinaryReader reader, IDictionary<string, CustomSliderState> sliders)
		{
			int num = ReadCount(reader);
			for (int i = 0; i < num; i++)
			{
				string key = RequiredId(ReadString(reader));
				if (!reader.ReadBoolean())
				{
					sliders.Add(key, null);
				}
				else
				{
					sliders.Add(key, new CustomSliderState(ReadString(reader), reader.ReadSingle(), ReadBytes(reader)));
				}
			}
		}

		private static List<string> SortedKeys<T>(IDictionary<string, T> values)
		{
			List<string> list = new List<string>(values.Keys);
			list.Sort(StringComparer.Ordinal);
			return list;
		}

		private static int CompareAppearanceKeys(AppearanceKey left, AppearanceKey right)
		{
			return string.Compare(left.Id, right.Id, StringComparison.Ordinal);
		}

		private static AppearanceScope ReadScope(int kind, string sectionId)
		{
			return kind switch
			{
				0 => AppearanceScope.Head, 
				1 => AppearanceScope.Body, 
				2 => AppearanceScope.Section(sectionId), 
				_ => throw new InvalidDataException("Appearance scope is invalid."), 
			};
		}

		private static void WriteCount(BinaryWriter writer, int count)
		{
			if (count < 0 || count > 4096)
			{
				throw new InvalidDataException("Appearance snapshot has too many entries.");
			}
			WriteInt(writer, count);
		}

		private static int ReadCount(BinaryReader reader)
		{
			int num = reader.ReadInt32();
			if (num < 0 || num > 4096)
			{
				throw new InvalidDataException("Appearance snapshot has too many entries.");
			}
			return num;
		}

		private static void WriteString(BinaryWriter writer, string value)
		{
			if (value == null)
			{
				WriteInt(writer, -1);
				return;
			}
			byte[] bytes = Encoding.UTF8.GetBytes(value);
			if (bytes.Length > 16384)
			{
				throw new InvalidDataException("Appearance string is too large.");
			}
			WriteLengthAndBytes(writer, bytes);
		}

		private static string ReadString(BinaryReader reader)
		{
			int num = reader.ReadInt32();
			if (num == -1)
			{
				return null;
			}
			if (num < 0 || num > 16384)
			{
				throw new InvalidDataException("Appearance string is invalid.");
			}
			byte[] array = reader.ReadBytes(num);
			if (array.Length != num)
			{
				throw new EndOfStreamException();
			}
			return Encoding.UTF8.GetString(array);
		}

		private static void WriteBytes(BinaryWriter writer, byte[] bytes)
		{
			if (bytes == null)
			{
				WriteInt(writer, -1);
				return;
			}
			if (bytes.Length > 1048576)
			{
				throw new InvalidDataException("Appearance payload is too large.");
			}
			WriteLengthAndBytes(writer, bytes);
		}

		private static byte[] ReadBytes(BinaryReader reader)
		{
			int num = reader.ReadInt32();
			if (num == -1)
			{
				return null;
			}
			if (num < 0 || num > 1048576)
			{
				throw new InvalidDataException("Appearance payload is invalid.");
			}
			byte[] array = reader.ReadBytes(num);
			if (array.Length != num)
			{
				throw new EndOfStreamException();
			}
			return array;
		}

		private static string RequiredId(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				throw new InvalidDataException("Appearance identifier is invalid.");
			}
			return value;
		}

		private static void WriteInt(BinaryWriter writer, int value)
		{
			EnsureWritable(writer, 4L);
			writer.Write(value);
		}

		private static void WriteSingle(BinaryWriter writer, float value)
		{
			EnsureWritable(writer, 4L);
			writer.Write(value);
		}

		private static void WriteBoolean(BinaryWriter writer, bool value)
		{
			EnsureWritable(writer, 1L);
			writer.Write(value);
		}

		private static void WriteLengthAndBytes(BinaryWriter writer, byte[] bytes)
		{
			EnsureWritable(writer, 4L + (long)bytes.Length);
			writer.Write(bytes.Length);
			writer.Write(bytes);
		}

		private static void EnsureWritable(BinaryWriter writer, long byteCount)
		{
			if (byteCount < 0 || byteCount > 1048576 - writer.BaseStream.Position)
			{
				throw new InvalidDataException("Appearance snapshot is too large.");
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
	public sealed class CustomSliderDefinition
	{
		public string Id { get; private set; }

		public string DriverKindId { get; private set; }

		public float Minimum { get; private set; }

		public float Maximum { get; private set; }

		public CustomSliderDefinition(string id, string driverKindId, float minimum, float maximum)
		{
			if (string.IsNullOrEmpty(id))
			{
				throw new ArgumentException("A slider identifier is required.", "id");
			}
			if (!CustomSliderDrivers.TryParse(driverKindId, out var _))
			{
				throw new ArgumentException("Unknown slider drivers cannot be executed.", "driverKindId");
			}
			if (minimum > maximum)
			{
				throw new ArgumentException("The slider range is invalid.", "minimum");
			}
			Id = id;
			DriverKindId = driverKindId;
			Minimum = minimum;
			Maximum = maximum;
		}
	}
	public enum CustomSliderDriverKind
	{
		HeadMeshDelta,
		BodyBoneRecipe
	}
	internal static class CustomSliderDrivers
	{
		public static bool TryParse(string id, out CustomSliderDriverKind kind)
		{
			if (string.Equals(id, "HeadMeshDelta", StringComparison.Ordinal))
			{
				kind = CustomSliderDriverKind.HeadMeshDelta;
				return true;
			}
			if (string.Equals(id, "BodyBoneRecipe", StringComparison.Ordinal))
			{
				kind = CustomSliderDriverKind.BodyBoneRecipe;
				return true;
			}
			kind = CustomSliderDriverKind.HeadMeshDelta;
			return false;
		}
	}
	public sealed class CustomSliderState
	{
		public string DriverKindId { get; private set; }

		public float Value { get; private set; }

		public byte[] OpaqueState { get; private set; }

		public CustomSliderState(string driverKindId, float value, byte[] opaqueState)
		{
			DriverKindId = driverKindId;
			Value = value;
			OpaqueState = AppearanceSnapshot.Copy(opaqueState);
		}

		public bool IsExecutable(AppearanceCapabilities capabilities)
		{
			CustomSliderDriverKind kind;
			if (capabilities != null && capabilities.SupportsCustomSliders)
			{
				return CustomSliderDrivers.TryParse(DriverKindId, out kind);
			}
			return false;
		}

		public CustomSliderState Clone()
		{
			return new CustomSliderState(DriverKindId, Value, OpaqueState);
		}
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
	internal interface ISuitePage
	{
		string Id { get; }

		int Order { get; }

		int MinimumShellContractVersion { get; }
	}
	internal sealed class PopupHoverState
	{
		private bool observedInside;

		internal bool IsOpen { get; private set; }

		internal bool Observe(bool layoutEvent, bool inside)
		{
			if (!layoutEvent)
			{
				observedInside = inside;
				return IsOpen;
			}
			IsOpen = inside || observedInside;
			return IsOpen;
		}

		internal void Close()
		{
			observedInside = false;
			IsOpen = false;
		}
	}
	internal sealed class ReflectionCleanupAdapter
	{
		private readonly Action draw;

		private readonly Action onEnter;

		private readonly Action onExit;

		private readonly Action reset;

		private readonly Action<int, int> requestItem;

		internal Action BodyCallback { get; private set; }

		internal Action FooterCallback { get; private set; }

		private ReflectionCleanupAdapter(Action draw, Action onEnter, Action onExit, Action reset, Action<int, int> requestItem)
		{
			this.draw = draw;
			this.onEnter = onEnter;
			this.onExit = onExit;
			this.reset = reset;
			this.requestItem = requestItem;
		}

		internal static bool TryCreate(object callback, out ReflectionCleanupAdapter adapter)
		{
			adapter = null;
			if (callback == null)
			{
				return false;
			}
			Type type = callback.GetType();
			MethodInfo methodInfo = ExactVoid(type, "Draw");
			MethodInfo methodInfo2 = ExactVoid(type, "OnEnter");
			MethodInfo methodInfo3 = ExactVoid(type, "OnExit");
			MethodInfo methodInfo4 = ExactVoid(type, "Reset");
			MethodInfo methodInfo5 = ExactVoid(type, "RequestItem", new Type[2]
			{
				typeof(int),
				typeof(int)
			});
			if (methodInfo == null || methodInfo2 == null || methodInfo3 == null || methodInfo4 == null)
			{
				return false;
			}
			try
			{
				adapter = new ReflectionCleanupAdapter((Action)Delegate.CreateDelegate(typeof(Action), callback, methodInfo), (Action)Delegate.CreateDelegate(typeof(Action), callback, methodInfo2), (Action)Delegate.CreateDelegate(typeof(Action), callback, methodInfo3), (Action)Delegate.CreateDelegate(typeof(Action), callback, methodInfo4), (methodInfo5 == null) ? null : ((Action<int, int>)Delegate.CreateDelegate(typeof(Action<int, int>), callback, methodInfo5)));
				MethodInfo methodInfo6 = ExactVoid(type, "DrawBody");
				MethodInfo methodInfo7 = ExactVoid(type, "DrawFooter");
				if (methodInfo6 != null && methodInfo7 != null)
				{
					adapter.BodyCallback = (Action)Delegate.CreateDelegate(typeof(Action), callback, methodInfo6);
					adapter.FooterCallback = (Action)Delegate.CreateDelegate(typeof(Action), callback, methodInfo7);
				}
				return true;
			}
			catch
			{
				return false;
			}
		}

		internal void Draw()
		{
			draw();
		}

		internal void OnEnter()
		{
			onEnter();
		}

		internal void OnExit()
		{
			onExit();
		}

		internal void Reset()
		{
			reset();
		}

		internal bool RequestItem(int resolverCategory, int itemId)
		{
			if (requestItem == null)
			{
				return false;
			}
			requestItem(resolverCategory, itemId);
			return true;
		}

		private static MethodInfo ExactVoid(Type type, string name)
		{
			return ExactVoid(type, name, Type.EmptyTypes);
		}

		private static MethodInfo ExactVoid(Type type, string name, Type[] parameters)
		{
			MethodInfo method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public, null, parameters, null);
			if (!(method != null) || !(method.ReturnType == typeof(void)))
			{
				return null;
			}
			return method;
		}
	}
	internal sealed class ReflectionPageAdapter
	{
		private readonly object callback;

		private readonly MethodInfo draw;

		private readonly MethodInfo onEnter;

		private readonly MethodInfo onExit;

		private readonly MethodInfo reset;

		private readonly MethodInfo getExtraWindowWidth;

		private readonly MethodInfo setHostWindowRect;

		private readonly MethodInfo setHostUiMetrics;

		public bool SupportsHostUiMetrics => setHostUiMetrics != null;

		internal string CallbackAssemblyName => callback.GetType().Assembly.GetName().Name;

		private ReflectionPageAdapter(object callback, MethodInfo draw, MethodInfo onEnter, MethodInfo onExit, MethodInfo reset, MethodInfo getExtraWindowWidth, MethodInfo setHostWindowRect, MethodInfo setHostUiMetrics)
		{
			this.callback = callback;
			this.draw = draw;
			this.onEnter = onEnter;
			this.onExit = onExit;
			this.reset = reset;
			this.getExtraWindowWidth = getExtraWindowWidth;
			this.setHostWindowRect = setHostWindowRect;
			this.setHostUiMetrics = setHostUiMetrics;
		}

		public static bool TryCreate(object callback, out ReflectionPageAdapter adapter)
		{
			adapter = null;
			if (callback == null)
			{
				return false;
			}
			Type type = callback.GetType();
			MethodInfo methodInfo = FindVoidCallback(type, "Draw");
			MethodInfo methodInfo2 = FindVoidCallback(type, "OnEnter");
			MethodInfo methodInfo3 = FindVoidCallback(type, "OnExit");
			MethodInfo methodInfo4 = FindVoidCallback(type, "Reset");
			if (methodInfo == null || methodInfo2 == null || methodInfo3 == null || methodInfo4 == null)
			{
				return false;
			}
			MethodInfo methodInfo5 = type.GetMethod("GetExtraWindowWidth", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
			if (methodInfo5 != null && methodInfo5.ReturnType != typeof(float))
			{
				methodInfo5 = null;
			}
			MethodInfo methodInfo6 = type.GetMethod("SetHostWindowRect", BindingFlags.Instance | BindingFlags.Public, null, new Type[4]
			{
				typeof(float),
				typeof(float),
				typeof(float),
				typeof(float)
			}, null);
			if (methodInfo6 != null && methodInfo6.ReturnType != typeof(void))
			{
				methodInfo6 = null;
			}
			MethodInfo methodInfo7 = type.GetMethod("SetHostUiMetrics", BindingFlags.Instance | BindingFlags.Public, null, new Type[3]
			{
				typeof(float),
				typeof(float),
				typeof(float)
			}, null);
			if (methodInfo7 != null && methodInfo7.ReturnType != typeof(void))
			{
				methodInfo7 = null;
			}
			adapter = new ReflectionPageAdapter(callback, methodInfo, methodInfo2, methodInfo3, methodInfo4, methodInfo5, methodInfo6, methodInfo7);
			return true;
		}

		public void Draw()
		{
			Invoke(draw);
		}

		public void OnEnter()
		{
			Invoke(onEnter);
		}

		public void OnExit()
		{
			Invoke(onExit);
		}

		public void Reset()
		{
			Invoke(reset);
		}

		public float GetExtraWindowWidth()
		{
			if (getExtraWindowWidth == null)
			{
				return 0f;
			}
			try
			{
				return Convert.ToSingle(getExtraWindowWidth.Invoke(callback, null));
			}
			catch (TargetInvocationException ex)
			{
				if (ex.InnerException != null)
				{
					throw ex.InnerException;
				}
				throw;
			}
		}

		public void SetHostWindowRect(float x, float y, float width, float height)
		{
			if (setHostWindowRect == null)
			{
				return;
			}
			try
			{
				setHostWindowRect.Invoke(callback, new object[4] { x, y, width, height });
			}
			catch (TargetInvocationException ex)
			{
				if (ex.InnerException != null)
				{
					throw ex.InnerException;
				}
				throw;
			}
		}

		public void SetHostUiMetrics(float scale, float logicalWidth, float logicalHeight)
		{
			if (setHostUiMetrics == null)
			{
				return;
			}
			try
			{
				setHostUiMetrics.Invoke(callback, new object[3] { scale, logicalWidth, logicalHeight });
			}
			catch (TargetInvocationException ex)
			{
				if (ex.InnerException != null)
				{
					throw ex.InnerException;
				}
				throw;
			}
		}

		private void Invoke(MethodInfo method)
		{
			try
			{
				method.Invoke(callback, null);
			}
			catch (TargetInvocationException ex)
			{
				if (ex.InnerException != null)
				{
					throw ex.InnerException;
				}
				throw;
			}
		}

		private static MethodInfo FindVoidCallback(Type type, string name)
		{
			MethodInfo method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
			if (!(method != null) || !(method.ReturnType == typeof(void)))
			{
				return null;
			}
			return method;
		}
	}
	public sealed class SuiteBridgeV1
	{
		private readonly List<SuiteBridgeRegistration> registrations = new List<SuiteBridgeRegistration>();

		private readonly int shellContractVersion;

		private readonly SuitePageHost pageHost;

		private readonly SuiteCleanupHost cleanupHost;

		private readonly Action ensureCleanupPage;

		internal Func<object> CharacterResolver;

		public int ProtocolVersion => 1;

		internal IList<SuiteBridgeRegistration> Registrations => registrations.AsReadOnly();

		public SuiteBridgeV1(int shellVersion)
		{
			shellContractVersion = shellVersion;
		}

		public bool IsCompatible(int minimumShellContractVersion)
		{
			return minimumShellContractVersion <= shellContractVersion;
		}

		public bool TryRegister(string kind, string id, int order, int minimumShellContractVersion, object callback)
		{
			if (!IsCompatible(minimumShellContractVersion))
			{
				return false;
			}
			if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(id))
			{
				return false;
			}
			for (int i = 0; i < registrations.Count; i++)
			{
				if (string.Equals(registrations[i].Kind, kind, StringComparison.Ordinal) && string.Equals(registrations[i].Id, id, StringComparison.Ordinal))
				{
					return false;
				}
			}
			SuiteBridgeRegistration suiteBridgeRegistration = new SuiteBridgeRegistration(kind, id, order, callback);
			if (!TryAcceptRegistration(suiteBridgeRegistration, minimumShellContractVersion))
			{
				return false;
			}
			registrations.Add(suiteBridgeRegistration);
			registrations.Sort(CompareRegistrations);
			return true;
		}

		private static int CompareRegistrations(SuiteBridgeRegistration left, SuiteBridgeRegistration right)
		{
			int num = left.Order.CompareTo(right.Order);
			if (num != 0)
			{
				return num;
			}
			int num2 = string.Compare(left.Id, right.Id, StringComparison.Ordinal);
			if (num2 == 0)
			{
				return string.Compare(left.Kind, right.Kind, StringComparison.Ordinal);
			}
			return num2;
		}

		public object GetActiveCharacter()
		{
			if (CharacterResolver != null)
			{
				return CharacterResolver();
			}
			return null;
		}

		internal SuiteBridgeV1(int shellVersion, SuitePageHost host)
			: this(shellVersion, host, null, null)
		{
		}

		internal SuiteBridgeV1(int shellVersion, SuitePageHost host, SuiteCleanupHost cleanup, Action ensureCleanupPage)
		{
			shellContractVersion = shellVersion;
			pageHost = host;
			cleanupHost = cleanup;
			this.ensureCleanupPage = ensureCleanupPage;
		}

		private bool TryAcceptRegistration(SuiteBridgeRegistration registration, int minimumShellContractVersion)
		{
			if (string.Equals(registration.Kind, "page", StringComparison.Ordinal))
			{
				if (!ReflectionPageAdapter.TryCreate(registration.Callback, out var adapter))
				{
					return false;
				}
				if (pageHost != null)
				{
					return pageHost.TryRegisterExternal(registration.Id, registration.Order, minimumShellContractVersion, adapter);
				}
				return true;
			}
			if (string.Equals(registration.Kind, "cleanup", StringComparison.Ordinal))
			{
				if (cleanupHost == null || !cleanupHost.TryRegisterExternal(registration.Id, CleanupLabel(registration.Id), registration.Order, minimumShellContractVersion, registration.Callback))
				{
					return false;
				}
				if (ensureCleanupPage != null)
				{
					ensureCleanupPage();
				}
			}
			return true;
		}

		private static string CleanupLabel(string id)
		{
			if (string.Equals(id, "outfit", StringComparison.Ordinal))
			{
				return "Outfit";
			}
			if (string.Equals(id, "character", StringComparison.Ordinal))
			{
				return "Character";
			}
			if (!string.IsNullOrEmpty(id))
			{
				return char.ToUpperInvariant(id[0]) + id.Substring(1);
			}
			return string.Empty;
		}

		public bool RequestCleanupItem(string contributorId, int resolverCategory, int itemId)
		{
			if (cleanupHost != null)
			{
				return cleanupHost.RequestItem(contributorId, resolverCategory, itemId);
			}
			return false;
		}
	}
	internal sealed class SuiteBridgeRegistration
	{
		public string Kind { get; private set; }

		public string Id { get; private set; }

		public int Order { get; private set; }

		public object Callback { get; private set; }

		public SuiteBridgeRegistration(string kind, string id, int order, object callback)
		{
			Kind = kind;
			Id = id;
			Order = order;
			Callback = callback;
		}
	}
	internal sealed class SuiteCleanupHost
	{
		private readonly int shellContractVersion;

		private readonly List<SuiteHostedCleanup> contributors = new List<SuiteHostedCleanup>();

		private string activeId;

		private string pendingId;

		private bool entered;

		internal IList<SuiteHostedCleanup> Contributors => contributors.AsReadOnly();

		internal string ActiveContributorId => activeId;

		internal bool SelectorVisible => contributors.Count >= 2;

		internal bool HasContributors => contributors.Count != 0;

		internal bool HasActiveFooter => Find(activeId)?.HasFooter ?? false;

		internal SuiteCleanupHost(int shellContractVersion)
		{
			this.shellContractVersion = shellContractVersion;
		}

		internal bool TryRegisterBuiltIn(string id, string label, int order, int minimumShellContractVersion, Action draw, Action onEnter, Action onExit, Action reset)
		{
			return TryRegisterBuiltIn(id, label, order, minimumShellContractVersion, draw, onEnter, onExit, reset, null);
		}

		internal bool TryRegisterBuiltIn(string id, string label, int order, int minimumShellContractVersion, Action draw, Action onEnter, Action onExit, Action reset, Action<int, int> requestItem, Action drawBody = null, Action drawFooter = null)
		{
			if (draw == null || onEnter == null || onExit == null || reset == null)
			{
				return false;
			}
			return TryRegister(new SuiteHostedCleanup(id, label, order, minimumShellContractVersion, draw, onEnter, onExit, reset, requestItem, drawBody, drawFooter));
		}

		internal bool TryRegisterExternal(string id, string label, int order, int minimumShellContractVersion, object callback)
		{
			if (!ReflectionCleanupAdapter.TryCreate(callback, out var adapter))
			{
				return false;
			}
			return TryRegister(new SuiteHostedCleanup(id, label, order, minimumShellContractVersion, (Action)adapter.Draw, (Action)adapter.OnEnter, (Action)adapter.OnExit, (Action)adapter.Reset, (Func<int, int, bool>)adapter.RequestItem, adapter.BodyCallback, adapter.FooterCallback));
		}

		internal bool RequestContributor(string id)
		{
			if (Find(id) == null)
			{
				return false;
			}
			pendingId = id;
			return true;
		}

		internal bool RequestItem(string contributorId, int resolverCategory, int itemId)
		{
			SuiteHostedCleanup suiteHostedCleanup = Find(contributorId);
			if (suiteHostedCleanup == null || !suiteHostedCleanup.RequestItem(resolverCategory, itemId))
			{
				return false;
			}
			pendingId = contributorId;
			return true;
		}

		internal void ApplyDeferredSelection(bool isLayout)
		{
			if (!isLayout || string.IsNullOrEmpty(pendingId))
			{
				return;
			}
			string text = pendingId;
			pendingId = null;
			if (string.Equals(text, activeId, StringComparison.Ordinal))
			{
				return;
			}
			SuiteHostedCleanup suiteHostedCleanup = Find(activeId);
			SuiteHostedCleanup suiteHostedCleanup2 = Find(text);
			if (suiteHostedCleanup2 != null)
			{
				if (entered)
				{
					suiteHostedCleanup?.OnExit();
				}
				activeId = suiteHostedCleanup2.Id;
				if (entered)
				{
					suiteHostedCleanup2.OnEnter();
				}
			}
		}

		internal void DrawActiveBody()
		{
			Find(activeId)?.DrawBody();
		}

		internal void DrawActiveFooter()
		{
			Find(activeId)?.DrawFooter();
		}

		internal void DrawActive()
		{
			Find(activeId)?.Draw();
		}

		internal void OnEnter()
		{
			if (!entered)
			{
				entered = true;
				Find(activeId)?.OnEnter();
			}
		}

		internal void OnExit()
		{
			if (entered)
			{
				Find(activeId)?.OnExit();
				entered = false;
				pendingId = null;
			}
		}

		internal void Reset()
		{
			pendingId = null;
			Find(activeId)?.Reset();
		}

		private bool TryRegister(SuiteHostedCleanup contributor)
		{
			if (contributor == null || string.IsNullOrEmpty(contributor.Id) || string.IsNullOrEmpty(contributor.Label) || contributor.MinimumShellContractVersion > shellContractVersion || Find(contributor.Id) != null)
			{
				return false;
			}
			contributors.Add(contributor);
			contributors.Sort(Compare);
			if (!entered)
			{
				activeId = contributors[0].Id;
			}
			return true;
		}

		private SuiteHostedCleanup Find(string id)
		{
			for (int i = 0; i < contributors.Count; i++)
			{
				if (string.Equals(contributors[i].Id, id, StringComparison.Ordinal))
				{
					return contributors[i];
				}
			}
			return null;
		}

		private static int Compare(SuiteHostedCleanup left, SuiteHostedCleanup right)
		{
			int num = left.Order.CompareTo(right.Order);
			if (num == 0)
			{
				return string.Compare(left.Id, right.Id, StringComparison.Ordinal);
			}
			return num;
		}
	}
	internal sealed class SuiteHostedCleanup
	{
		private readonly Action draw;

		private readonly Action drawBody;

		private readonly Action drawFooter;

		private readonly Action onEnter;

		private readonly Action onExit;

		private readonly Action reset;

		private readonly Func<int, int, bool> requestItem;

		internal string Id { get; private set; }

		internal string Label { get; private set; }

		internal int Order { get; private set; }

		internal int MinimumShellContractVersion { get; private set; }

		internal bool HasFooter
		{
			get
			{
				if (drawBody != null)
				{
					return drawFooter != null;
				}
				return false;
			}
		}

		internal SuiteHostedCleanup(string id, string label, int order, int minimumShellContractVersion, Action draw, Action onEnter, Action onExit, Action reset)
			: this(id, label, order, minimumShellContractVersion, draw, onEnter, onExit, reset, (Func<int, int, bool>)null, (Action)null, (Action)null)
		{
		}

		internal SuiteHostedCleanup(string id, string label, int order, int minimumShellContractVersion, Action draw, Action onEnter, Action onExit, Action reset, Action<int, int> requestItem, Action drawBody = null, Action drawFooter = null)
		{
			Id = id;
			Label = label;
			Order = order;
			MinimumShellContractVersion = minimumShellContractVersion;
			this.draw = draw;
			this.drawBody = drawBody;
			this.drawFooter = drawFooter;
			this.onEnter = onEnter;
			this.onExit = onExit;
			this.reset = reset;
			this.requestItem = ((requestItem == null) ? null : ((Func<int, int, bool>)delegate(int category, int item)
			{
				requestItem(category, item);
				return true;
			}));
		}

		internal SuiteHostedCleanup(string id, string label, int order, int minimumShellContractVersion, Action draw, Action onEnter, Action onExit, Action reset, Func<int, int, bool> requestItem, Action drawBody = null, Action drawFooter = null)
		{
			Id = id;
			Label = label;
			Order = order;
			MinimumShellContractVersion = minimumShellContractVersion;
			this.draw = draw;
			this.drawBody = drawBody;
			this.drawFooter = drawFooter;
			this.onEnter = onEnter;
			this.onExit = onExit;
			this.reset = reset;
			this.requestItem = requestItem;
		}

		internal void DrawBody()
		{
			if (HasFooter)
			{
				drawBody();
			}
			else
			{
				draw();
			}
		}

		internal void DrawFooter()
		{
			if (HasFooter)
			{
				drawFooter();
			}
		}

		internal void Draw()
		{
			draw();
		}

		internal void OnEnter()
		{
			onEnter();
		}

		internal void OnExit()
		{
			onExit();
		}

		internal void Reset()
		{
			reset();
		}

		internal bool RequestItem(int resolverCategory, int itemId)
		{
			if (requestItem != null)
			{
				return requestItem(resolverCategory, itemId);
			}
			return false;
		}
	}
	public static class SuiteContractVersion
	{
		public const int Current = 1;
	}
	internal static class SuiteMakeUpTheme
	{
		private static readonly SuiteThemeSkin makeup = new SuiteThemeSkin(0);

		private static readonly SuiteThemeSkin dressup = new SuiteThemeSkin(1);

		private static readonly SuiteThemeSkin cleanup = new SuiteThemeSkin(2);

		internal static bool IsActive => makeup.IsActive;

		internal static bool IsAnyActive
		{
			get
			{
				if (!makeup.IsActive && !dressup.IsActive)
				{
					return cleanup.IsActive;
				}
				return true;
			}
		}

		internal static Color ActiveAccent
		{
			get
			{
				if (!cleanup.IsActive)
				{
					if (!dressup.IsActive)
					{
						return makeup.ThemeAccent;
					}
					return dressup.ThemeAccent;
				}
				return cleanup.ThemeAccent;
			}
		}

		internal static GUISkin GetSkin(GUISkin source)
		{
			return makeup.GetSkin(source);
		}

		internal static GUISkin GetPageSkin(GUISkin source, string pageId)
		{
			return pageId switch
			{
				"makeup" => makeup.GetSkin(source), 
				"dressup" => dressup.GetSkin(source), 
				"cleanup" => cleanup.GetSkin(source), 
				_ => source, 
			};
		}

		internal static void Dispose()
		{
			makeup.Dispose();
			dressup.Dispose();
			cleanup.Dispose();
		}
	}
	internal sealed class SuiteThemeSkin
	{
		internal static readonly Color Accent = new Color(0.78f, 0.4f, 0.51f, 1f);

		private static readonly Color Ink = new Color(0.94f, 0.87f, 0.89f, 1f);

		private static readonly Color Muted = new Color(0.8f, 0.67f, 0.72f, 1f);

		private static readonly Color Canvas = new Color(0.085f, 0.035f, 0.05f, 1f);

		private static readonly Color Panel = new Color(0.14f, 0.065f, 0.09f, 1f);

		private static readonly Color Raised = new Color(0.25f, 0.105f, 0.15f, 1f);

		private static readonly Color Line = new Color(0.42f, 0.22f, 0.29f, 1f);

		private readonly List<Texture2D> textures = new List<Texture2D>();

		private GUISkin skin;

		private Font font;

		private Font buttonFont;

		private readonly int palette;

		internal Color ThemeAccent => Tone(Accent);

		internal bool IsActive
		{
			get
			{
				if (skin != null)
				{
					return GUI.skin == skin;
				}
				return false;
			}
		}

		internal SuiteThemeSkin(int palette)
		{
			this.palette = palette;
		}

		private Color Tone(Color color)
		{
			if (palette == 1)
			{
				return new Color(color.g, color.b, color.r, color.a);
			}
			if (palette == 2)
			{
				return new Color(color.r, color.g * 0.65f, color.b * 0.2f, color.a);
			}
			return color;
		}

		internal GUISkin GetSkin(GUISkin source)
		{
			if (skin != null || source == null)
			{
				return skin ?? source;
			}
			skin = UnityEngine.Object.Instantiate(source);
			skin.name = ((palette == 1) ? "F604 Velvet Dress-Up" : ((palette == 2) ? "F604 Ember Clean-Up" : "F604 Atelier Make-Up"));
			skin.hideFlags = HideFlags.HideAndDontSave;
			try
			{
				font = Font.CreateDynamicFontFromOSFont(new string[6] { "Segoe UI", "Microsoft YaHei UI", "Microsoft YaHei", "Yu Gothic UI", "Meiryo", "Arial" }, 14);
				if (font != null)
				{
					font.hideFlags = HideFlags.HideAndDontSave;
					skin.font = font;
				}
				buttonFont = Font.CreateDynamicFontFromOSFont(new string[7] { "Arial Rounded MT Bold", "Segoe UI", "Microsoft YaHei UI", "Microsoft YaHei", "Yu Gothic UI", "Meiryo", "Arial" }, 14);
				if (buttonFont != null)
				{
					buttonFont.hideFlags = HideFlags.HideAndDontSave;
				}
			}
			catch
			{
			}
			Texture2D normal = Surface(Raised, Line, 8f, domed: true);
			Texture2D hover = Surface(new Color(0.34f, 0.15f, 0.21f), Accent, 8f, domed: true);
			Texture2D active = Surface(new Color(0.39f, 0.15f, 0.23f), Accent, 8f);
			Texture2D normal2 = Surface(new Color(0.07f, 0.025f, 0.04f), new Color(0.36f, 0.19f, 0.25f), 6f);
			Texture2D texture2D = Surface(new Color(0.19f, 0.07f, 0.12f), Accent, 6f);
			skin.button = new GUIStyle(source.button);
			Text(skin.button, Ink);
			States(skin.button, normal, hover, active);
			skin.button.border = new RectOffset(9, 9, 9, 9);
			skin.button.fontSize = 13;
			skin.button.fontStyle = FontStyle.Bold;
			if (buttonFont != null)
			{
				skin.button.font = buttonFont;
			}
			skin.button.clipping = TextClipping.Clip;
			skin.button.alignment = TextAnchor.MiddleCenter;
			skin.button.contentOffset = new Vector2(0f, -1f);
			skin.button.fixedHeight = 24f;
			int num = (source.button.padding.left + source.button.padding.right) / 2;
			skin.button.padding = new RectOffset(num, num, 0, 0);
			skin.label = new GUIStyle(source.label);
			Text(skin.label, Ink);
			skin.label.fontSize = 12;
			skin.label.alignment = TextAnchor.MiddleLeft;
			skin.box = new GUIStyle(source.box);
			Text(skin.box, Muted);
			Texture2D texture2D2 = Surface(Panel, new Color(0.31f, 0.16f, 0.22f), 8f);
			States(skin.box, texture2D2, texture2D2, texture2D2);
			skin.box.border = new RectOffset(9, 9, 9, 9);
			skin.box.fontSize = 13;
			skin.box.fontStyle = FontStyle.Bold;
			skin.box.alignment = TextAnchor.MiddleCenter;
			skin.window = new GUIStyle(source.window);
			Text(skin.window, Ink);
			Texture2D texture2D3 = Surface(Canvas, new Color(0.39f, 0.2f, 0.27f), 12f);
			States(skin.window, texture2D3, texture2D3, texture2D3);
			skin.window.border = new RectOffset(14, 14, 14, 14);
			skin.textField = new GUIStyle(source.textField);
			skin.textArea = new GUIStyle(source.textArea);
			GUIStyle[] array = new GUIStyle[2] { skin.textField, skin.textArea };
			foreach (GUIStyle gUIStyle in array)
			{
				Text(gUIStyle, Ink);
				States(gUIStyle, normal2, texture2D, texture2D);
				gUIStyle.border = new RectOffset(6, 6, 6, 6);
				gUIStyle.fontSize = 12;
			}
			skin.settings.cursorColor = ThemeAccent;
			skin.settings.selectionColor = Tone(new Color(0.52f, 0.22f, 0.34f, 0.85f));
			skin.toggle = new GUIStyle(skin.button);
			States(skin.toggle, normal2, hover, active);
			skin.toggle.fixedHeight = 24f;
			skin.toggle.fixedWidth = 0f;
			skin.toggle.padding = new RectOffset(8, 8, 0, 0);
			skin.horizontalSlider = new GUIStyle(source.horizontalSlider);
			Texture2D texture2D4 = Surface(new Color(0.1f, 0.035f, 0.065f), new Color(0.32f, 0.16f, 0.23f), 3f);
			States(skin.horizontalSlider, texture2D4, texture2D4, texture2D4);
			skin.horizontalSlider.border = new RectOffset(4, 4, 2, 2);
			skin.horizontalSlider.fixedHeight = 12f;
			skin.horizontalSlider.overflow = new RectOffset(0, 0, -3, -3);
			RectOffset margin = source.horizontalSlider.margin;
			skin.horizontalSlider.margin = new RectOffset(margin.left, margin.right, margin.top + 6, margin.bottom);
			skin.horizontalSliderThumb = new GUIStyle(source.horizontalSliderThumb);
			Texture2D normal3 = Surface(new Color(0.67f, 0.31f, 0.44f), Accent, 6f, domed: true);
			Texture2D texture2D5 = Surface(new Color(0.79f, 0.43f, 0.55f), Accent, 6f, domed: true);
			States(skin.horizontalSliderThumb, normal3, texture2D5, texture2D5);
			skin.horizontalSliderThumb.border = new RectOffset(5, 5, 5, 5);
			skin.horizontalSliderThumb.fixedWidth = 12f;
			skin.horizontalSliderThumb.fixedHeight = 12f;
			skin.verticalScrollbar = new GUIStyle(source.verticalScrollbar);
			skin.verticalScrollbarThumb = new GUIStyle(source.verticalScrollbarThumb);
			Texture2D texture2D6 = ScrollSurface(recessed: true, horizontal: false, 0);
			States(skin.verticalScrollbar, texture2D6, texture2D6, texture2D6);
			skin.verticalScrollbar.border = new RectOffset(6, 6, 6, 6);
			States(skin.verticalScrollbarThumb, ScrollSurface(recessed: false, horizontal: false, 0), ScrollSurface(recessed: false, horizontal: false, 1), ScrollSurface(recessed: false, horizontal: false, 2));
			skin.verticalScrollbarThumb.border = new RectOffset(6, 6, 6, 6);
			skin.horizontalScrollbar = new GUIStyle(source.horizontalScrollbar);
			skin.horizontalScrollbarThumb = new GUIStyle(source.horizontalScrollbarThumb);
			Texture2D texture2D7 = ScrollSurface(recessed: true, horizontal: true, 0);
			States(skin.horizontalScrollbar, texture2D7, texture2D7, texture2D7);
			skin.horizontalScrollbar.border = new RectOffset(6, 6, 6, 6);
			States(skin.horizontalScrollbarThumb, ScrollSurface(recessed: false, horizontal: true, 0), ScrollSurface(recessed: false, horizontal: true, 1), ScrollSurface(recessed: false, horizontal: true, 2));
			skin.horizontalScrollbarThumb.border = new RectOffset(6, 6, 6, 6);
			skin.verticalScrollbarUpButton = new GUIStyle(source.verticalScrollbarUpButton);
			skin.verticalScrollbarDownButton = new GUIStyle(source.verticalScrollbarDownButton);
			return skin;
		}

		private Texture2D ScrollSurface(bool recessed, bool horizontal, int state)
		{
			Color color = ((palette == 1) ? new Color(0.12f, 0.19f, 0.29f, 1f) : ((palette == 2) ? new Color(0.27f, 0.095f, 0.075f, 1f) : new Color(0.24f, 0.085f, 0.12f, 1f)));
			if (recessed)
			{
				color = Color.Lerp(color, Color.black, 0.76f);
			}
			else
			{
				switch (state)
				{
				case 1:
					color = Color.Lerp(color, new Color(0.53f, 0.52f, 0.55f, 1f), 0.16f);
					break;
				case 2:
					color = Color.Lerp(color, Color.black, 0.18f);
					break;
				}
			}
			Color[] array = new Color[1024];
			for (int i = 0; i < 32; i++)
			{
				for (int j = 0; j < 32; j++)
				{
					float num = (horizontal ? ((float)(32 - i) - 0.5f) : ((float)j + 0.5f));
					float num2 = Math.Min(num, 32f - num);
					float num3 = SuiteThemeGeometry.Coverage((float)j + 0.5f, (float)i + 0.5f, 32f, 5f, 0f);
					float num4 = SuiteThemeGeometry.Coverage((float)j + 0.5f, (float)i + 0.5f, 32f, 5f, 1f);
					float num5 = Math.Max(0f, 1f - num2 / 6f);
					Color a = color;
					a = Color.Lerp(b: (!recessed) ? ((num < 16f) ? Color.Lerp(a, new Color(0.55f, 0.57f, 0.61f, 1f), num5 * 0.2f) : Color.Lerp(a, Color.black, num5 * 0.5f)) : Color.Lerp(a, Color.black, num5 * 0.65f), a: Color.Lerp(color, Color.black, recessed ? 0.25f : 0.48f), t: (num3 > 0f) ? (num4 / num3) : 0f);
					a.a = num3;
					array[i * 32 + j] = a;
				}
			}
			Texture2D texture2D = new Texture2D(32, 32, TextureFormat.RGBA32, mipChain: false);
			texture2D.name = (recessed ? "F604 recessed scrollbar track" : "F604 shaded scrollbar thumb");
			texture2D.hideFlags = HideFlags.HideAndDontSave;
			texture2D.wrapMode = TextureWrapMode.Clamp;
			texture2D.filterMode = FilterMode.Bilinear;
			texture2D.SetPixels(array);
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
			textures.Add(texture2D);
			return texture2D;
		}

		private void Text(GUIStyle style, Color color)
		{
			if (font != null)
			{
				style.font = font;
			}
			style.normal.textColor = color;
			style.hover.textColor = Ink;
			style.active.textColor = Ink;
			style.focused.textColor = Ink;
			style.onNormal.textColor = Ink;
			style.onHover.textColor = Ink;
			style.onActive.textColor = Ink;
			style.onFocused.textColor = Ink;
		}

		private static void States(GUIStyle style, Texture2D normal, Texture2D hover, Texture2D active)
		{
			style.normal.background = normal;
			style.hover.background = hover;
			style.active.background = active;
			style.focused.background = hover;
			style.onNormal.background = active;
			style.onHover.background = hover;
			style.onActive.background = active;
			style.onFocused.background = hover;
		}

		private Texture2D Surface(Color fill, Color stroke, float radius, bool domed = false)
		{
			fill = Tone(fill);
			stroke = Tone(stroke);
			Color[] array = new Color[1024];
			for (int i = 0; i < 32; i++)
			{
				for (int j = 0; j < 32; j++)
				{
					float num = SuiteThemeGeometry.Coverage((float)j + 0.5f, (float)i + 0.5f, 32f, radius, 0f);
					float num2 = SuiteThemeGeometry.Coverage((float)j + 0.5f, (float)i + 0.5f, 32f, radius, 1f);
					float num3 = SuiteThemeGeometry.VelvetLight((float)j + 0.5f, (float)i + 0.5f, 32f, domed);
					Color b = ((num3 >= 0f) ? Color.Lerp(fill, new Color(0.94f, 0.87f, 0.81f, fill.a), num3) : Color.Lerp(fill, new Color(0f, 0f, 0f, fill.a), 0f - num3));
					float num4 = ((float)i / 31f - 0.5f) * 0.18f;
					Color color = Color.Lerp((num4 >= 0f) ? Color.Lerp(stroke, new Color(0.94f, 0.87f, 0.81f, stroke.a), num4) : Color.Lerp(stroke, new Color(0f, 0f, 0f, stroke.a), 0f - num4), b, (num > 0f) ? (num2 / num) : 0f);
					color.a *= num;
					array[i * 32 + j] = color;
				}
			}
			Texture2D texture2D = new Texture2D(32, 32, TextureFormat.RGBA32, mipChain: false);
			texture2D.name = "F604 Atelier surface";
			texture2D.hideFlags = HideFlags.HideAndDontSave;
			texture2D.wrapMode = TextureWrapMode.Clamp;
			texture2D.filterMode = FilterMode.Bilinear;
			texture2D.SetPixels(array);
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
			textures.Add(texture2D);
			return texture2D;
		}

		internal void Dispose()
		{
			if (skin != null)
			{
				UnityEngine.Object.Destroy(skin);
			}
			skin = null;
			if (font != null)
			{
				UnityEngine.Object.Destroy(font);
			}
			font = null;
			if (buttonFont != null)
			{
				UnityEngine.Object.Destroy(buttonFont);
			}
			buttonFont = null;
			foreach (Texture2D texture in textures)
			{
				if (texture != null)
				{
					UnityEngine.Object.Destroy(texture);
				}
			}
			textures.Clear();
		}
	}
	internal sealed class SuiteNavigation
	{
		private string activePageId;

		private string pendingPageId;

		public string ActivePageId => activePageId;

		public void SetInitialPage(string pageId)
		{
			if (string.IsNullOrEmpty(activePageId))
			{
				activePageId = pageId;
			}
		}

		public bool RequestPage(string pageId)
		{
			if (string.IsNullOrEmpty(pageId))
			{
				return false;
			}
			pendingPageId = pageId;
			return true;
		}

		public bool TryApplyLayout(bool isLayout, out string outgoingPageId, out string incomingPageId)
		{
			outgoingPageId = null;
			incomingPageId = null;
			if (!isLayout || string.IsNullOrEmpty(pendingPageId))
			{
				return false;
			}
			string text = pendingPageId;
			pendingPageId = null;
			if (string.Equals(activePageId, text, StringComparison.Ordinal))
			{
				return false;
			}
			outgoingPageId = activePageId;
			activePageId = text;
			incomingPageId = text;
			return true;
		}
	}
	internal sealed class SuiteNoSelectionStartupState
	{
		private bool routedForCurrentNoSelection;

		internal bool ShouldRouteToMakeUp(bool hasSelectedCharacter, bool makeUpAvailable)
		{
			if (hasSelectedCharacter)
			{
				routedForCurrentNoSelection = true;
				return false;
			}
			if (!makeUpAvailable || routedForCurrentNoSelection)
			{
				return false;
			}
			routedForCurrentNoSelection = true;
			return true;
		}

		internal void Reset()
		{
			routedForCurrentNoSelection = false;
		}
	}
	internal sealed class SuitePageHost
	{
		private readonly int shellContractVersion;

		private readonly List<SuiteHostedPage> pages = new List<SuiteHostedPage>();

		private readonly SuiteNavigation navigation = new SuiteNavigation();

		private Action<Exception> errorSink;

		private bool hostVisible = true;

		internal string MakeUpAssemblyName { get; private set; }

		public IList<SuiteHostedPage> Pages => pages.AsReadOnly();

		public string ActivePageId => navigation.ActivePageId;

		public bool SelectorVisible => pages.Count >= 2;

		public bool ActivePageSupportsUiScale => FindPage(ActivePageId)?.SupportsHostUiMetrics ?? false;

		public SuitePageHost(int shellContractVersion)
		{
			this.shellContractVersion = shellContractVersion;
		}

		public bool TryRegisterBuiltIn(string id, int order, int minimumShellContractVersion, Action draw, Action onEnter, Action onExit, Action reset)
		{
			if (draw == null || onEnter == null || onExit == null || reset == null)
			{
				return false;
			}
			return TryRegister(new SuiteHostedPage(id, order, minimumShellContractVersion, draw, onEnter, onExit, reset, null, null, null, supportsHostUiMetrics: true));
		}

		public bool TryRegisterExternal(string id, int order, int minimumShellContractVersion, object callback)
		{
			if (!ReflectionPageAdapter.TryCreate(callback, out var adapter))
			{
				return false;
			}
			return TryRegisterExternal(id, order, minimumShellContractVersion, adapter);
		}

		internal bool TryRegisterExternal(string id, int order, int minimumShellContractVersion, ReflectionPageAdapter adapter)
		{
			if (adapter == null)
			{
				return false;
			}
			bool num = TryRegister(new SuiteHostedPage(id, order, minimumShellContractVersion, adapter.Draw, adapter.OnEnter, adapter.OnExit, adapter.Reset, adapter.GetExtraWindowWidth, adapter.SetHostWindowRect, adapter.SetHostUiMetrics, adapter.SupportsHostUiMetrics));
			if (num && string.Equals(id, "makeup", StringComparison.Ordinal))
			{
				MakeUpAssemblyName = adapter.CallbackAssemblyName;
			}
			return num;
		}

		internal void SetErrorSink(Action<Exception> sink)
		{
			errorSink = sink;
		}

		public bool RequestPage(string id)
		{
			if (FindPage(id) != null)
			{
				return navigation.RequestPage(id);
			}
			return false;
		}

		internal bool HasPage(string id)
		{
			return FindPage(id) != null;
		}

		public void ApplyDeferredSelection(bool isLayout)
		{
			if (!navigation.TryApplyLayout(isLayout, out var outgoingPageId, out var incomingPageId))
			{
				return;
			}
			SuiteHostedPage suiteHostedPage = FindPage(outgoingPageId);
			SuiteHostedPage suiteHostedPage2 = FindPage(incomingPageId);
			if (hostVisible)
			{
				if (suiteHostedPage != null)
				{
					SafeInvoke(suiteHostedPage.OnExit);
				}
				if (suiteHostedPage2 != null)
				{
					SafeInvoke(suiteHostedPage2.OnEnter);
				}
			}
		}

		public void SetHostVisible(bool visible)
		{
			if (hostVisible != visible)
			{
				SuiteHostedPage suiteHostedPage = FindPage(ActivePageId);
				if (!visible && suiteHostedPage != null)
				{
					SafeInvoke(suiteHostedPage.OnExit);
				}
				hostVisible = visible;
				if (visible && suiteHostedPage != null)
				{
					SafeInvoke(suiteHostedPage.OnEnter);
				}
			}
		}

		public float GetActiveExtraWindowWidth()
		{
			SuiteHostedPage suiteHostedPage = FindPage(ActivePageId);
			if (suiteHostedPage == null)
			{
				return 0f;
			}
			try
			{
				return Math.Max(0f, suiteHostedPage.GetExtraWindowWidth());
			}
			catch (Exception error)
			{
				ReportCallbackFault(error);
				return 0f;
			}
		}

		public void DrawActivePage()
		{
			SuiteHostedPage suiteHostedPage = FindPage(ActivePageId);
			if (suiteHostedPage != null)
			{
				SafeInvoke(suiteHostedPage.Draw);
			}
		}

		public void SetActiveHostWindowRect(float x, float y, float width, float height)
		{
			SuiteHostedPage suiteHostedPage = FindPage(ActivePageId);
			if (suiteHostedPage == null)
			{
				return;
			}
			try
			{
				suiteHostedPage.SetHostWindowRect(x, y, width, height);
			}
			catch (Exception error)
			{
				ReportCallbackFault(error);
			}
		}

		public void SetActiveHostUiMetrics(float scale, float logicalWidth, float logicalHeight)
		{
			SuiteHostedPage suiteHostedPage = FindPage(ActivePageId);
			if (suiteHostedPage == null || !suiteHostedPage.SupportsHostUiMetrics)
			{
				return;
			}
			try
			{
				suiteHostedPage.SetHostUiMetrics(scale, logicalWidth, logicalHeight);
			}
			catch (Exception error)
			{
				ReportCallbackFault(error);
			}
		}

		public void ResetPages()
		{
			for (int i = 0; i < pages.Count; i++)
			{
				SafeInvoke(pages[i].Reset);
			}
		}

		private bool TryRegister(SuiteHostedPage page)
		{
			if (page.MinimumShellContractVersion > shellContractVersion || string.IsNullOrEmpty(page.Id))
			{
				return false;
			}
			if (FindPage(page.Id) != null)
			{
				return false;
			}
			pages.Add(page);
			pages.Sort(ComparePages);
			navigation.SetInitialPage(pages[0].Id);
			return true;
		}

		private SuiteHostedPage FindPage(string id)
		{
			for (int i = 0; i < pages.Count; i++)
			{
				if (string.Equals(pages[i].Id, id, StringComparison.Ordinal))
				{
					return pages[i];
				}
			}
			return null;
		}

		private static int ComparePages(SuiteHostedPage left, SuiteHostedPage right)
		{
			int num = left.Order.CompareTo(right.Order);
			if (num == 0)
			{
				return string.Compare(left.Id, right.Id, StringComparison.Ordinal);
			}
			return num;
		}

		private void SafeInvoke(Action callback)
		{
			try
			{
				callback();
			}
			catch (Exception error)
			{
				ReportCallbackFault(error);
			}
		}

		private void ReportCallbackFault(Exception error)
		{
			try
			{
				if (errorSink != null)
				{
					errorSink(error);
				}
			}
			catch
			{
			}
		}
	}
	internal sealed class SuiteHostedPage : ISuitePage
	{
		private readonly Action draw;

		private readonly Action onEnter;

		private readonly Action onExit;

		private readonly Action reset;

		private readonly Func<float> getExtraWindowWidth;

		private readonly Action<float, float, float, float> setHostWindowRect;

		private readonly Action<float, float, float> setHostUiMetrics;

		public string Id { get; private set; }

		public int Order { get; private set; }

		public int MinimumShellContractVersion { get; private set; }

		public bool SupportsHostUiMetrics { get; private set; }

		public SuiteHostedPage(string id, int order, int minimumShellContractVersion, Action draw, Action onEnter, Action onExit, Action reset, Func<float> getExtraWindowWidth = null, Action<float, float, float, float> setHostWindowRect = null, Action<float, float, float> setHostUiMetrics = null, bool supportsHostUiMetrics = false)
		{
			Id = id;
			Order = order;
			MinimumShellContractVersion = minimumShellContractVersion;
			this.draw = draw;
			this.onEnter = onEnter;
			this.onExit = onExit;
			this.reset = reset;
			this.getExtraWindowWidth = getExtraWindowWidth;
			this.setHostWindowRect = setHostWindowRect;
			this.setHostUiMetrics = setHostUiMetrics;
			SupportsHostUiMetrics = supportsHostUiMetrics;
		}

		public void Draw()
		{
			draw();
		}

		public void OnEnter()
		{
			onEnter();
		}

		public void OnExit()
		{
			onExit();
		}

		public void Reset()
		{
			reset();
		}

		public float GetExtraWindowWidth()
		{
			if (getExtraWindowWidth != null)
			{
				return getExtraWindowWidth();
			}
			return 0f;
		}

		public void SetHostWindowRect(float x, float y, float width, float height)
		{
			if (setHostWindowRect != null)
			{
				setHostWindowRect(x, y, width, height);
			}
		}

		public void SetHostUiMetrics(float scale, float logicalWidth, float logicalHeight)
		{
			if (setHostUiMetrics != null)
			{
				setHostUiMetrics(scale, logicalWidth, logicalHeight);
			}
		}
	}
	internal static class SuitePageLabels
	{
		internal static string DisplayName(string pageId)
		{
			if (string.Equals(pageId, "dressup", StringComparison.Ordinal))
			{
				return "Dress-Up";
			}
			if (string.Equals(pageId, "makeup", StringComparison.Ordinal))
			{
				return "Make-Up";
			}
			if (string.Equals(pageId, "cleanup", StringComparison.Ordinal))
			{
				return "Clean-Up";
			}
			return pageId;
		}
	}
	public static class SuiteProductIdentity
	{
		public const string DisplayName = "Freelancer604 Vanity Studio";

		public const string DisplayVersion = "1.0";

		public const string DressUpGuid = "com.freelancer604.wardrobe";

		public const string DressUpFreeVersion = "1.11.7";

		public const string DressUpPlusVersion = "2.2.14";

		public const string MakeUpGuid = "com.freelancer604.makeup";

		public const string MakeUpFreeGuid = "com.freelancer604.makeup";

		public const string MakeUpPlusGuid = "com.freelancer604.makeup";

		public const string MakeUpFreeVersion = "1.0.0";

		public const string MakeUpPlusVersion = "2.1.11";
	}
	internal static class SuiteProductPresentation
	{
		internal static bool IsMakeUpPlusAssembly(string simpleName)
		{
			if (!string.Equals(simpleName, "HS2_F604MakeUpPlus", StringComparison.OrdinalIgnoreCase) && !string.Equals(simpleName, "KK_F604MakeUpPlus", StringComparison.OrdinalIgnoreCase))
			{
				return string.Equals(simpleName, "KKS_F604MakeUpPlus", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}

		internal static bool ShowPlusBadge(string pageId, bool dressUpPlus, string makeUpAssemblyName)
		{
			if (!string.Equals(pageId, "makeup", StringComparison.Ordinal))
			{
				return dressUpPlus;
			}
			return IsMakeUpPlusAssembly(makeUpAssemblyName);
		}
	}
	internal sealed class SuiteRegistry
	{
		private readonly List<ISuitePage> pages = new List<ISuitePage>();

		private readonly Dictionary<string, ICleanupContributor> cleanup = new Dictionary<string, ICleanupContributor>(StringComparer.Ordinal);

		private readonly Dictionary<string, IAppearanceDataProvider> providers = new Dictionary<string, IAppearanceDataProvider>(StringComparer.Ordinal);

		private readonly List<ICleanupContributor> cleanupContributors = new List<ICleanupContributor>();

		private readonly List<IAppearanceDataProvider> appearanceProviders = new List<IAppearanceDataProvider>();

		private readonly Dictionary<string, ICustomSliderProvider> sliderProviders = new Dictionary<string, ICustomSliderProvider>(StringComparer.Ordinal);

		private readonly List<ICustomSliderProvider> customSliderProviders = new List<ICustomSliderProvider>();

		public int ShellContractVersion { get; private set; }

		public IList<ISuitePage> Pages => pages.AsReadOnly();

		public IList<ICleanupContributor> CleanupContributors => cleanupContributors.AsReadOnly();

		public IList<IAppearanceDataProvider> AppearanceProviders => appearanceProviders.AsReadOnly();

		public IList<ICustomSliderProvider> CustomSliderProviders => customSliderProviders.AsReadOnly();

		public SuiteRegistry(int shellContractVersion)
		{
			ShellContractVersion = shellContractVersion;
		}

		public bool TryRegisterPage(ISuitePage page)
		{
			if (page == null)
			{
				throw new ArgumentNullException("page");
			}
			if (page.MinimumShellContractVersion > ShellContractVersion)
			{
				return false;
			}
			EnsureId(page.Id, "page");
			for (int i = 0; i < pages.Count; i++)
			{
				if (string.Equals(pages[i].Id, page.Id, StringComparison.Ordinal))
				{
					throw new ArgumentException("A page with this identifier is already registered.", "page");
				}
			}
			pages.Add(page);
			pages.Sort(ComparePages);
			return true;
		}

		public void RegisterPage(ISuitePage page)
		{
			TryRegisterPage(page);
		}

		public bool TryRegisterCleanupContributor(ICleanupContributor contributor)
		{
			if (contributor == null)
			{
				throw new ArgumentNullException("contributor");
			}
			if (contributor.MinimumShellContractVersion > ShellContractVersion)
			{
				return false;
			}
			EnsureId(contributor.Id, "contributor");
			if (cleanup.ContainsKey(contributor.Id))
			{
				throw new ArgumentException("A cleanup contributor with this identifier is already registered.", "contributor");
			}
			cleanup.Add(contributor.Id, contributor);
			cleanupContributors.Add(contributor);
			cleanupContributors.Sort(CompareCleanupContributors);
			return true;
		}

		public void RegisterCleanupContributor(ICleanupContributor contributor)
		{
			TryRegisterCleanupContributor(contributor);
		}

		public bool TryRegisterAppearanceProvider(IAppearanceDataProvider provider)
		{
			if (provider == null)
			{
				throw new ArgumentNullException("provider");
			}
			if (provider.MinimumShellContractVersion > ShellContractVersion)
			{
				return false;
			}
			EnsureId(provider.Id, "provider");
			if (providers.ContainsKey(provider.Id))
			{
				throw new ArgumentException("An appearance provider with this identifier is already registered.", "provider");
			}
			providers.Add(provider.Id, provider);
			appearanceProviders.Add(provider);
			appearanceProviders.Sort(CompareAppearanceProviders);
			return true;
		}

		public void RegisterAppearanceProvider(IAppearanceDataProvider provider)
		{
			TryRegisterAppearanceProvider(provider);
		}

		public bool TryRegisterCustomSliderProvider(ICustomSliderProvider provider)
		{
			if (provider == null)
			{
				throw new ArgumentNullException("provider");
			}
			if (provider.MinimumShellContractVersion > ShellContractVersion)
			{
				return false;
			}
			EnsureId(provider.Id, "provider");
			if (sliderProviders.ContainsKey(provider.Id))
			{
				throw new ArgumentException("A custom slider provider with this identifier is already registered.", "provider");
			}
			sliderProviders.Add(provider.Id, provider);
			customSliderProviders.Add(provider);
			customSliderProviders.Sort(CompareCustomSliderProviders);
			return true;
		}

		public void RegisterCustomSliderProvider(ICustomSliderProvider provider)
		{
			TryRegisterCustomSliderProvider(provider);
		}

		private static void EnsureId(string id, string parameter)
		{
			if (string.IsNullOrEmpty(id))
			{
				throw new ArgumentException("A stable identifier is required.", parameter);
			}
		}

		private static int ComparePages(ISuitePage left, ISuitePage right)
		{
			int num = left.Order.CompareTo(right.Order);
			if (num == 0)
			{
				return string.Compare(left.Id, right.Id, StringComparison.Ordinal);
			}
			return num;
		}

		private static int CompareCleanupContributors(ICleanupContributor left, ICleanupContributor right)
		{
			int num = left.Order.CompareTo(right.Order);
			if (num == 0)
			{
				return string.Compare(left.Id, right.Id, StringComparison.Ordinal);
			}
			return num;
		}

		private static int CompareAppearanceProviders(IAppearanceDataProvider left, IAppearanceDataProvider right)
		{
			int num = left.Order.CompareTo(right.Order);
			if (num == 0)
			{
				return string.Compare(left.Id, right.Id, StringComparison.Ordinal);
			}
			return num;
		}

		private static int CompareCustomSliderProviders(ICustomSliderProvider left, ICustomSliderProvider right)
		{
			int num = left.Order.CompareTo(right.Order);
			if (num == 0)
			{
				return string.Compare(left.Id, right.Id, StringComparison.Ordinal);
			}
			return num;
		}
	}
	internal static class SuiteResizeGripGeometry
	{
		internal const int GlyphSize = 12;

		internal const int HitSize = 16;

		internal static bool IsVisiblePixel(int x, int y)
		{
			if (x >= 0 && y >= 0 && x < 12 && y < 12)
			{
				return x >= y;
			}
			return false;
		}
	}
	internal static class SuiteStudioUiVisibility
	{
		private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

		private static Type providerType;

		private static FieldInfo visibleField;

		private static FieldInfo handlerField;

		private static float nextProbe;

		private static bool patchInstalled;

		private static bool editingImGui;

		private static bool editingWindow;

		internal static bool IsVisible()
		{
			if (!WardrobeContext.InStudio)
			{
				return true;
			}
			Resolve();
			try
			{
				object obj = ((handlerField == null) ? null : handlerField.GetValue(null));
				return obj == null || !providerType.IsInstanceOfType(obj) || visibleField == null || (bool)visibleField.GetValue(obj);
			}
			catch
			{
				return true;
			}
		}

		internal static void ObserveGuiFocus()
		{
			if (WardrobeContext.InStudio)
			{
				editingImGui = TextFieldFocused();
				Resolve();
			}
		}

		internal static void ObserveWindowFocus()
		{
			editingWindow = TextFieldFocused();
		}

		private static bool TextFieldFocused()
		{
			int keyboardControl = GUIUtility.keyboardControl;
			TextEditor textEditor = ((keyboardControl == 0) ? null : (GUIUtility.QueryStateObject(typeof(TextEditor), keyboardControl) as TextEditor));
			if (textEditor != null)
			{
				return textEditor.controlID == keyboardControl;
			}
			return false;
		}

		private static bool AllowToggle()
		{
			if (!WardrobeContext.InStudio)
			{
				return true;
			}
			if (editingImGui || (WardrobeGUI.visible && IsVisible() && editingWindow))
			{
				return false;
			}
			EventSystem current = EventSystem.current;
			if (current != null && current.currentSelectedGameObject != null)
			{
				InputField componentInParent = current.currentSelectedGameObject.GetComponentInParent<InputField>();
				if (componentInParent != null && componentInParent.isFocused)
				{
					return false;
				}
			}
			return true;
		}

		private static void Resolve()
		{
			if (patchInstalled || Time.realtimeSinceStartup < nextProbe)
			{
				return;
			}
			nextProbe = Time.realtimeSinceStartup + 1f;
			try
			{
				Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
				foreach (Assembly assembly in assemblies)
				{
					providerType = assembly.GetType("HideAllUI.HideStudioUI", throwOnError: false);
					if (!(providerType == null))
					{
						Type type = assembly.GetType("HideAllUI.HideAllUICore", throwOnError: false);
						handlerField = ((type == null) ? null : type.GetField("currentUIHandler", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
						visibleField = providerType.GetField("visible", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
						MethodInfo method = providerType.GetMethod("ToggleUI", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
						if (visibleField != null && method != null)
						{
							patchInstalled = InstallGuard(method);
						}
						break;
					}
				}
			}
			catch (Exception)
			{
			}
		}

		private static bool InstallGuard(MethodInfo target)
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
				object obj = ((type.Name == "HarmonyInstance") ? type.GetMethod("Create", BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[1] { "Freelancer604.VanityStudio.UIVisibility" }) : Activator.CreateInstance(type, "Freelancer604.VanityStudio.UIVisibility"));
				object obj2 = Activator.CreateInstance(type2, typeof(SuiteStudioUiVisibility).GetMethod("AllowToggle", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
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
	public static class SuiteThemeGeometry
	{
		public static float VelvetLight(float x, float y, float size, bool raised)
		{
			float num = y / size;
			int num2 = ((int)x * 374761393) ^ ((int)y * 668265263);
			float num3 = ((float)(((num2 ^ (num2 >> 13)) * 1274126177) & 0xFF) / 255f - 0.5f) * 0.022f;
			float num4 = (float)(Math.Sin((double)x * 0.41) * Math.Sin((double)y * 0.29)) * 0.008f;
			if (!raised)
			{
				return -0.035f + 0.035f * num + num3 * 0.6f + num4;
			}
			float num5 = (float)Math.Exp(0.0 - Math.Pow((num - 0.72f) / 0.28f, 2.0)) * 0.1f;
			float num6 = (float)Math.Exp((0f - num) * 13f) * 0.18f;
			float num7 = (float)Math.Exp((0f - Math.Min(x, size - x)) * 0.45f) * 0.06f;
			return num5 - num6 - num7 + num3 + num4;
		}

		public static float Coverage(float x, float y, float size, float radius, float inset)
		{
			float num = size * 0.5f - inset;
			float num2 = Math.Max(0f, radius - inset);
			float val = Math.Abs(x - size * 0.5f) - (num - num2);
			float num3 = Math.Abs(y - size * 0.5f) - (num - num2);
			float num4 = Math.Max(val, 0f);
			float num5 = Math.Max(num3, 0f);
			float num6 = (float)Math.Sqrt(num4 * num4 + num5 * num5) + Math.Min(Math.Max(val, num3), 0f) - num2;
			return Math.Max(0f, Math.Min(1f, 0.5f - num6));
		}
	}
	internal static class SuiteUiScalePolicy
	{
		internal const float MaximumScale = 3f;

		internal const float MinimumScale = 0.65f;

		internal const float TotalScreenMargin = 16f;

		internal static float ClampUserScale(float requestedScale, float screenWidth, float screenHeight, float designFootprintWidth, float designHeight)
		{
			if (screenWidth <= 0f || screenHeight <= 0f || designFootprintWidth <= 0f || designHeight <= 0f)
			{
				return 1f;
			}
			float val = Math.Max(1f, screenWidth - 16f) / designFootprintWidth;
			float val2 = Math.Max(1f, screenHeight - 16f) / designHeight;
			return Math.Min(Math.Max(0.65f, Math.Min(3f, requestedScale)), Math.Min(val, val2));
		}

		internal static float LogicalExtent(float physicalExtent, float scale)
		{
			if (!(scale > 0f))
			{
				return physicalExtent;
			}
			return physicalExtent / scale;
		}
	}
	internal static class SuiteWrapperLayout
	{
		internal const float Left = 140f;

		internal const float Top = 48f;

		internal const float Width = 700f;

		internal const float Height = 1026f;

		internal const float ExtraWidth = 60f;

		internal const float ExtraHeight = 148f;

		internal const float CaseWidth = 560f;

		internal const float HeaderCenter = 440f;

		internal const float HeaderBottom = 170f;

		internal const float ContentLeft = 180f;

		internal const float ContentTop = 198f;

		internal const float ContentWidth = 480f;

		internal const float ContentHeight = 682f;

		internal const float FooterTop = 898f;

		internal const float FooterContentTop = 914f;

		internal const float FooterHeight = 74f;

		internal const float TagLeft = 66f;

		internal const float TagRight = 172f;

		internal const float CloseX = 638f;

		internal const float CloseY = 76f;

		internal const float EmptyLeft = 56f;

		internal static float ModeButtonX(int index)
		{
			return 79f + (float)(index % 3) * 28f;
		}

		internal static float ModeButtonY(int index)
		{
			return 184f + (float)(index / 3) * 32f;
		}
	}
	internal static class SuiteWrapperTheme
	{
		private static Texture2D art;

		private static Texture2D baseArt;

		private static Texture2D cleanupTag;

		private static Texture2D drawerArt;

		private static Texture2D titleArt;

		private static Texture2D closeGlyph;

		private static int activeMode;

		private static readonly Texture2D[] crests = new Texture2D[3];

		private static readonly Texture2D[] headers = new Texture2D[3];

		private static Font titleFont;

		private static GUIStyle titleStyle;

		private static bool attempted;

		internal static bool DrawingBody;

		private static Action footer;

		internal static bool Available
		{
			get
			{
				Ensure();
				if (art != null)
				{
					return baseArt != null;
				}
				return false;
			}
		}

		internal static float TitleGemCenter => 425.19034f;

		internal static void BeginBody()
		{
			footer = null;
			DrawingBody = true;
		}

		internal static void EndBody()
		{
			DrawingBody = false;
		}

		internal static Vector2 BodyOffset()
		{
			if (!DrawingBody)
			{
				return Vector2.zero;
			}
			return new Vector2(40f, 150f);
		}

		internal static bool DeferFooter(Action draw)
		{
			if (!DrawingBody || draw == null)
			{
				return false;
			}
			footer = draw;
			return true;
		}

		internal static void DrawFooter()
		{
			Action action = footer;
			footer = null;
			action?.Invoke();
		}

		private static void Ensure()
		{
			if (attempted)
			{
				return;
			}
			attempted = true;
			try
			{
				using (Stream stream = typeof(SuiteWrapperTheme).Assembly.GetManifestResourceStream("Freelancer604.Wardrobe.Resources.vanity-ornaments.png"))
				{
					if (stream == null)
					{
						return;
					}
					byte[] array = new byte[stream.Length];
					int num;
					for (int i = 0; i < array.Length; i += num)
					{
						if ((num = stream.Read(array, i, array.Length - i)) <= 0)
						{
							break;
						}
					}
					art = CreateTexture();
					if (!art.LoadImage(array))
					{
						UnityEngine.Object.Destroy(art);
						art = null;
						return;
					}
					art.name = "Vanity ornate source atlas";
					art.hideFlags = HideFlags.HideAndDontSave;
					art.wrapMode = TextureWrapMode.Clamp;
					art.filterMode = FilterMode.Bilinear;
					Color32[] pixels = art.GetPixels32();
					int width = art.width;
					int height = art.height;
					bool[] array2 = new bool[pixels.Length];
					int[] array3 = new int[pixels.Length];
					int num2 = 0;
					int tail = 0;
					for (int j = 0; j < width; j++)
					{
						Seed(j, pixels, array2, array3, ref tail);
						Seed((height - 1) * width + j, pixels, array2, array3, ref tail);
					}
					for (int k = 1; k < height - 1; k++)
					{
						Seed(k * width, pixels, array2, array3, ref tail);
						Seed(k * width + width - 1, pixels, array2, array3, ref tail);
					}
					while (num2 < tail)
					{
						int num3 = array3[num2++];
						int num4 = num3 % width;
						if (num4 > 0)
						{
							Seed(num3 - 1, pixels, array2, array3, ref tail);
						}
						if (num4 < width - 1)
						{
							Seed(num3 + 1, pixels, array2, array3, ref tail);
						}
						if (num3 >= width)
						{
							Seed(num3 - width, pixels, array2, array3, ref tail);
						}
						if (num3 < pixels.Length - width)
						{
							Seed(num3 + width, pixels, array2, array3, ref tail);
						}
					}
					for (int l = 0; l < pixels.Length; l++)
					{
						if (array2[l])
						{
							pixels[l].a = 0;
						}
					}
					for (int m = 248; m < 474; m++)
					{
						float num5 = Math.Min(1f, Math.Max(0f, (float)(m - 250) / 74f));
						int num6 = (int)(728f + -74f * num5) - 5;
						int num7 = (int)(728f + 103f * num5) + 5;
						for (int n = num6; n <= num7; n++)
						{
							pixels[(height - 1 - m) * width + n].a = 0;
						}
					}
					TrimExterior(pixels, width, height);
					art.SetPixels32(pixels);
					art.Apply(updateMipmaps: false, makeNoLongerReadable: true);
				}
				using (Stream stream2 = typeof(SuiteWrapperTheme).Assembly.GetManifestResourceStream("Freelancer604.Wardrobe.Resources.vanity-cleanup-tag.png"))
				{
					if (stream2 == null)
					{
						return;
					}
					byte[] array4 = new byte[stream2.Length];
					int num9;
					for (int num8 = 0; num8 < array4.Length; num8 += num9)
					{
						if ((num9 = stream2.Read(array4, num8, array4.Length - num8)) <= 0)
						{
							break;
						}
					}
					cleanupTag = CreateTexture();
					if (!cleanupTag.LoadImage(array4))
					{
						return;
					}
					Texture2D texture2D = cleanupTag;
					Color32[] pixels2 = texture2D.GetPixels32();
					Color32[] array5 = new Color32[920200];
					for (int num10 = 0; num10 < 1070; num10++)
					{
						for (int num11 = 0; num11 < 430; num11++)
						{
							array5[num10 * 860 + 429 - num11] = (array5[num10 * 860 + 430 + num11] = pixels2[(texture2D.height - 1160 + num10) * texture2D.width + 628 + num11]);
						}
					}
					cleanupTag = CreateTexture(860, 1070);
					TrimExterior(array5, 860, 1070);
					cleanupTag.SetPixels32(array5);
					cleanupTag.Apply(updateMipmaps: false, makeNoLongerReadable: true);
					UnityEngine.Object.Destroy(texture2D);
					cleanupTag.name = "Vanity symmetric Clean-Up tag";
					cleanupTag.hideFlags = HideFlags.HideAndDontSave;
					cleanupTag.wrapMode = TextureWrapMode.Clamp;
					cleanupTag.filterMode = FilterMode.Bilinear;
				}
				using (Stream stream3 = typeof(SuiteWrapperTheme).Assembly.GetManifestResourceStream("Freelancer604.Wardrobe.Resources.vanity-base.png"))
				{
					if (stream3 == null)
					{
						return;
					}
					byte[] array6 = new byte[stream3.Length];
					int num13;
					for (int num12 = 0; num12 < array6.Length; num12 += num13)
					{
						if ((num13 = stream3.Read(array6, num12, array6.Length - num12)) <= 0)
						{
							break;
						}
					}
					baseArt = CreateTexture();
					if (!baseArt.LoadImage(array6))
					{
						UnityEngine.Object.Destroy(baseArt);
						baseArt = null;
						return;
					}
					baseArt.name = "Vanity clean case base";
					baseArt.hideFlags = HideFlags.HideAndDontSave;
					baseArt.wrapMode = TextureWrapMode.Clamp;
					baseArt.filterMode = FilterMode.Bilinear;
					ClearBaseMatte(baseArt);
				}
				using (Stream stream4 = typeof(SuiteWrapperTheme).Assembly.GetManifestResourceStream("Freelancer604.Wardrobe.Resources.vanity-drawer.png"))
				{
					if (stream4 != null)
					{
						byte[] array7 = new byte[stream4.Length];
						int num15;
						for (int num14 = 0; num14 < array7.Length; num14 += num15)
						{
							if ((num15 = stream4.Read(array7, num14, array7.Length - num14)) <= 0)
							{
								break;
							}
						}
						drawerArt = CreateTexture();
						drawerArt.LoadImage(array7);
						drawerArt.hideFlags = HideFlags.HideAndDontSave;
						drawerArt.wrapMode = TextureWrapMode.Clamp;
						drawerArt.filterMode = FilterMode.Bilinear;
						drawerArt.Apply(updateMipmaps: false, makeNoLongerReadable: true);
					}
				}
				using (Stream stream5 = typeof(SuiteWrapperTheme).Assembly.GetManifestResourceStream("Freelancer604.Wardrobe.Resources.vanity-titles.png"))
				{
					if (stream5 != null)
					{
						byte[] array8 = new byte[stream5.Length];
						int num17;
						for (int num16 = 0; num16 < array8.Length; num16 += num17)
						{
							if ((num17 = stream5.Read(array8, num16, array8.Length - num16)) <= 0)
							{
								break;
							}
						}
						titleArt = CreateTexture();
						if (!titleArt.LoadImage(array8))
						{
							UnityEngine.Object.Destroy(titleArt);
							titleArt = null;
						}
						else
						{
							titleArt.hideFlags = HideFlags.HideAndDontSave;
							titleArt.wrapMode = TextureWrapMode.Clamp;
							titleArt.filterMode = FilterMode.Bilinear;
							Color32[] pixels3 = titleArt.GetPixels32();
							for (int num18 = 0; num18 < pixels3.Length; num18++)
							{
								Color32 color = pixels3[num18];
								int num19 = Math.Min(color.r, Math.Min(color.g, color.b));
								int num20 = Math.Max(color.r, Math.Max(color.g, color.b));
								if (num20 - num19 < 18 && num19 > 50 && num20 < 220)
								{
									pixels3[num18].a = 0;
								}
							}
							for (int num21 = 162; num21 < 210; num21++)
							{
								for (int num22 = 810; num22 < 910; num22++)
								{
									pixels3[(titleArt.height - 1 - num21) * titleArt.width + num22].a = 0;
								}
							}
							for (int num23 = 550; num23 < 588; num23++)
							{
								for (int num24 = 180; num24 < 280; num24++)
								{
									pixels3[(titleArt.height - 1 - num23) * titleArt.width + num24].a = 0;
								}
							}
							titleArt.SetPixels32(pixels3);
							titleArt.Apply(updateMipmaps: false, makeNoLongerReadable: true);
						}
					}
				}
				titleFont = Font.CreateDynamicFontFromOSFont(new string[3] { "Book Antiqua", "Palatino Linotype", "Georgia" }, 50);
				if (titleFont != null)
				{
					titleFont.hideFlags = HideFlags.HideAndDontSave;
				}
			}
			catch (Exception ex)
			{
				if (WardrobePlugin.Log != null)
				{
					WardrobePlugin.Log.LogWarning((object)("Vanity wrapper unavailable: " + ex.Message));
				}
			}
		}

		private static void Seed(int i, Color32[] pixels, bool[] visited, int[] queue, ref int tail)
		{
			if (!visited[i])
			{
				Color32 color = pixels[i];
				int num = Math.Min(color.r, Math.Min(color.g, color.b));
				int num2 = Math.Max(color.r, Math.Max(color.g, color.b));
				if (color.a == 0 || (num >= 110 && num2 - num <= 22))
				{
					visited[i] = true;
					queue[tail++] = i;
				}
			}
		}

		private static Texture2D CreateTexture(int width = 2, int height = 2)
		{
			return new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
			{
				wrapMode = TextureWrapMode.Clamp
			};
		}

		private static void TrimExterior(Color32[] pixels, int width, int height)
		{
			for (int i = 0; i < 2; i++)
			{
				bool[] array = new bool[pixels.Length];
				for (int j = 0; j < height; j++)
				{
					for (int k = 0; k < width; k++)
					{
						int num = j * width + k;
						array[num] = pixels[num].a == 0 || k == 0 || j == 0 || k == width - 1 || j == height - 1 || pixels[num - 1].a == 0 || pixels[num + 1].a == 0 || pixels[num - width].a == 0 || pixels[num + width].a == 0;
					}
				}
				for (int l = 0; l < pixels.Length; l++)
				{
					if (array[l])
					{
						pixels[l].a = 0;
					}
				}
			}
			Color32[] array2 = (Color32[])pixels.Clone();
			for (int m = 1; m < height - 1; m++)
			{
				for (int n = 1; n < width - 1; n++)
				{
					int num2 = m * width + n;
					if (array2[num2].a != 0)
					{
						continue;
					}
					int num3 = 0;
					int num4 = 0;
					int num5 = 0;
					int num6 = 0;
					for (int num7 = -1; num7 <= 1; num7++)
					{
						for (int num8 = -1; num8 <= 1; num8++)
						{
							Color32 color = array2[num2 + num7 * width + num8];
							if (color.a != 0)
							{
								num3 += color.r;
								num4 += color.g;
								num5 += color.b;
								num6++;
							}
						}
					}
					if (num6 > 0)
					{
						pixels[num2].r = (byte)(num3 / num6);
						pixels[num2].g = (byte)(num4 / num6);
						pixels[num2].b = (byte)(num5 / num6);
					}
				}
			}
		}

		private static void SoftenCaseEdge(Color32[] pixels, int width, int height)
		{
			for (int i = 0; i < 2; i++)
			{
				Color32[] array = (Color32[])pixels.Clone();
				for (int j = 1; j < height - 1; j++)
				{
					for (int k = 1; k < width - 1; k++)
					{
						int num = j * width + k;
						Color32 color = array[num];
						if (color.a != 0 && Math.Max(color.r, Math.Max(color.g, color.b)) <= 42 && (array[num - 1].a == 0 || array[num + 1].a == 0 || array[num - width].a == 0 || array[num + width].a == 0))
						{
							pixels[num].a = 0;
						}
					}
				}
			}
			Color32[] array2 = (Color32[])pixels.Clone();
			for (int l = 1; l < height - 1; l++)
			{
				for (int m = 1; m < width - 1; m++)
				{
					int num2 = l * width + m;
					if (array2[num2].a == 0)
					{
						continue;
					}
					int num3 = 0;
					for (int n = -1; n <= 1; n++)
					{
						for (int num4 = -1; num4 <= 1; num4++)
						{
							num3 += array2[num2 + n * width + num4].a;
						}
					}
					pixels[num2].a = (byte)Math.Min(array2[num2].a, num3 / 9);
				}
			}
		}

		private static void ClearBaseMatte(Texture2D texture)
		{
			Color32[] pixels = texture.GetPixels32();
			int width = texture.width;
			int height = texture.height;
			bool[] array = new bool[pixels.Length];
			int[] array2 = new int[pixels.Length];
			int num = 0;
			int tail = 0;
			for (int i = 0; i < width; i++)
			{
				Seed(i, pixels, array, array2, ref tail);
				Seed((height - 1) * width + i, pixels, array, array2, ref tail);
			}
			for (int j = 1; j < height - 1; j++)
			{
				Seed(j * width, pixels, array, array2, ref tail);
				Seed(j * width + width - 1, pixels, array, array2, ref tail);
			}
			while (num < tail)
			{
				int num2 = array2[num++];
				int num3 = num2 % width;
				if (num3 > 0)
				{
					Seed(num2 - 1, pixels, array, array2, ref tail);
				}
				if (num3 < width - 1)
				{
					Seed(num2 + 1, pixels, array, array2, ref tail);
				}
				if (num2 >= width)
				{
					Seed(num2 - width, pixels, array, array2, ref tail);
				}
				if (num2 < pixels.Length - width)
				{
					Seed(num2 + width, pixels, array, array2, ref tail);
				}
			}
			for (int k = 0; k < pixels.Length; k++)
			{
				if (array[k])
				{
					pixels[k].a = 0;
				}
			}
			for (int l = 847; l < height; l++)
			{
				for (int m = 0; m < width; m++)
				{
					pixels[(height - 1 - l) * width + m].a = 0;
				}
			}
			TrimExterior(pixels, width, height);
			SoftenCaseEdge(pixels, width, height);
			RestrainMakeUpInsets(pixels, width, height);
			int[] array3 = new int[3] { 337, 934, 1496 };
			int[] array4 = new int[3] { 70, 652, 1217 };
			int[] array5 = new int[3] { 567, 1136, 1723 };
			_ = new int[3] { 507, 1078, 1662 };
			for (int n = 0; n < 3; n++)
			{
				int num4 = array5[n] - array4[n];
				Color32[] array6 = new Color32[num4 * 108];
				for (int num5 = 0; num5 < 108; num5++)
				{
					for (int num6 = 0; num6 < num4; num6++)
					{
						int num7 = 159 - num5;
						int num8 = array4[n] + num6;
						array6[num5 * num4 + num6] = pixels[(height - 1 - num7) * width + num8];
					}
				}
				headers[n] = CreateTexture(num4, 108);
				headers[n].SetPixels32(array6);
				headers[n].Apply(updateMipmaps: false, makeNoLongerReadable: true);
				headers[n].hideFlags = HideFlags.HideAndDontSave;
				headers[n].filterMode = FilterMode.Bilinear;
				Color32[] array7 = new Color32[13188];
				for (int num9 = 0; num9 < 42; num9++)
				{
					for (int num10 = 0; num10 < 314; num10++)
					{
						Color32 color = pixels[(height - 162 + num9) * width + array3[n] - 157 + num10];
						if ((color.g <= 55 || !((float)(int)color.r > (float)(int)color.b * 1.2f) || !((float)(int)color.g > (float)(int)color.b * 1.08f)) && (color.r <= 180 || color.g <= 180 || color.b <= 180))
						{
							color.a = 0;
						}
						array7[num9 * 314 + num10] = color;
					}
				}
				crests[n] = CreateTexture(314, 42);
				crests[n].SetPixels32(array7);
				crests[n].Apply(updateMipmaps: false, makeNoLongerReadable: true);
				crests[n].hideFlags = HideFlags.HideAndDontSave;
				crests[n].filterMode = FilterMode.Bilinear;
			}
			texture.SetPixels32(pixels);
			texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		}

		private static void Slice(Rect target, float x, float y, float width, float height)
		{
			GUI.DrawTextureWithTexCoords(target, art, new Rect(x / (float)art.width, 1f - (y + height) / (float)art.height, width / (float)art.width, height / (float)art.height), alphaBlend: true);
		}

		private static void BaseSlice(Rect target, float x, float y, float width, float height)
		{
			GUI.DrawTextureWithTexCoords(target, baseArt, new Rect(x / (float)baseArt.width, 1f - (y + height) / (float)baseArt.height, width / (float)baseArt.width, height / (float)baseArt.height), alphaBlend: true);
		}

		internal static void Draw(string page)
		{
			Ensure();
			if (!(art == null) && !(baseArt == null) && Event.current.type == EventType.Repaint)
			{
				int num = (activeMode = ((page == "cleanup") ? 1 : ((page == "makeup") ? 2 : 0)));
				float num2 = num switch
				{
					1 => 652, 
					0 => 70, 
					_ => 1217, 
				};
				float num3 = num switch
				{
					1 => 1136, 
					0 => 567, 
					_ => 1723, 
				};
				Color color = GUI.color;
				GUI.color = Color.white;
				GUI.DrawTextureWithTexCoords(new Rect(140f, 48f, 560f, 122f), headers[num], new Rect(0f, 0f, 1f, 1f), alphaBlend: true);
				BaseSlice(new Rect(140f, 170f, 560f, 36f), num2, 160f, num3 - num2, 36f);
				BaseSlice(new Rect(140f, 205f, 560f, 658f), num2, 195f, num3 - num2, 526f);
				BaseSlice(new Rect(140f, 862f, 560f, 36f), num2, 720f, num3 - num2, 36f);
				BaseSlice(new Rect(140f, 898f, 560f, 128f), num2, 756f, num3 - num2, 96f);
				GUI.color = color;
			}
		}

		internal static void DrawOrnaments()
		{
			if (art == null || Event.current.type != EventType.Repaint)
			{
				return;
			}
			int num = activeMode;
			Color color = GUI.color;
			GUI.color = Color.white;
			float num2 = num switch
			{
				1 => 654, 
				0 => 195, 
				_ => 1103, 
			};
			float num3 = num switch
			{
				1 => 610, 
				0 => 180, 
				_ => 1048, 
			};
			float num4 = num switch
			{
				1 => 430, 
				0 => 280, 
				_ => 488, 
			};
			float num5 = ((num == 1) ? 177f : 188f);
			float num6 = 106f / num5;
			float num7 = 31f / 56f;
			float num8 = 66f + (num3 - num2) * num6;
			float num9 = 138f - 250f * num7;
			if (num == 1)
			{
				float num10 = 53f - (728f - num2) * num6;
				Slice(new Rect(num8 + num10, num9, num4 * num6, 474f * num7), num3, 0f, num4, 474f);
				float y = num9 + 250f * num7;
				float height = 224f * num7;
				if (cleanupTag != null)
				{
					GUI.DrawTextureWithTexCoords(new Rect(66f, y, 106f, height), cleanupTag, new Rect(0f, 0f, 1f, 1f), alphaBlend: true);
				}
				Slice(new Rect(num8, num9 + 474f * num7, num4 * num6, 506f * num7), num3, 474f, num4, 506f);
			}
			else
			{
				Slice(new Rect(num8, num9, num4 * num6, 980f * num7), num3, 0f, num4, 980f);
			}
			GUI.color = color;
		}

		internal static void DrawTitle(string title)
		{
			Ensure();
			int num = title switch
			{
				"Make-Up" => 2, 
				"Clean-Up" => 1, 
				"Dress-Up" => 0, 
				_ => -1, 
			};
			if (titleArt != null && num >= 0)
			{
				float[] array = new float[3] { 34f, 34f, 34f };
				float[] array2 = new float[3] { 162f, 560f, 969f };
				float[] array3 = new float[3] { 1077f, 1077f, 1077f };
				float[] array4 = new float[3] { 220f, 228f, 220f };
				Color color = GUI.color;
				GUI.color = Color.white;
				GUI.DrawTextureWithTexCoords(new Rect(220f, 76f, 410f, 68f), titleArt, new Rect(array[num] / (float)titleArt.width, 1f - (array2[num] + array4[num]) / (float)titleArt.height, array3[num] / (float)titleArt.width, array4[num] / (float)titleArt.height), alphaBlend: true);
				GUI.color = color;
				return;
			}
			if (crests[activeMode] != null)
			{
				GUI.DrawTextureWithTexCoords(new Rect(282f, 116f, 316f, 38f), crests[activeMode], new Rect(0f, 0f, 1f, 1f), alphaBlend: true);
			}
			if (titleStyle == null)
			{
				titleStyle = new GUIStyle(GUI.skin.label)
				{
					font = titleFont,
					fontSize = 50,
					alignment = TextAnchor.MiddleCenter,
					padding = new RectOffset(),
					clipping = TextClipping.Clip
				};
			}
			Rect position = new Rect(256f, 66f, 368f, 62f);
			titleStyle.fontSize = 50;
			while (titleStyle.fontSize > 24 && titleStyle.CalcSize(new GUIContent(title)).x > position.width)
			{
				titleStyle.fontSize--;
			}
			Color color2 = GUI.color;
			GUI.color = Color.white;
			titleStyle.normal.textColor = new Color(0.12f, 0.055f, 0.015f, 1f);
			GUI.Label(new Rect(position.x + 1f, position.y + 2f, position.width, position.height), title, titleStyle);
			titleStyle.normal.textColor = new Color(0.92f, 0.79f, 0.57f, 1f);
			GUI.Label(position, title, titleStyle);
			GUI.color = color2;
		}

		internal static void DrawDrawer(Rect bounds)
		{
			Ensure();
			if (Event.current.type != EventType.Repaint)
			{
				return;
			}
			float num = bounds.width - 12f;
			if (drawerArt != null)
			{
				Color color = GUI.color;
				GUI.color = Color.white;
				float[] array = new float[4] { 286f, 318f, 704f, 736f };
				float[] array2 = new float[4] { 38f, 104f, 1430f, 1498f };
				float[] array3 = new float[4]
				{
					bounds.x,
					bounds.x + 5f,
					bounds.x + num - 5f,
					bounds.x + num
				};
				float[] array4 = new float[4]
				{
					bounds.y,
					bounds.y + 12f,
					bounds.yMax - 12f,
					bounds.yMax
				};
				for (int i = 0; i < 3; i++)
				{
					for (int j = 0; j < 3; j++)
					{
						DrawerSlice(new Rect(array3[j], array4[i], array3[j + 1] - array3[j], array4[i + 1] - array4[i]), array[j], array2[i], array[j + 1] - array[j], array2[i + 1] - array2[i]);
					}
				}
				DrawerSlice(new Rect(bounds.x + num, bounds.y + bounds.height * 0.42f, 12f, 72f), 736f, 590f, 62f, 330f);
				GUI.color = color;
			}
			else
			{
				GUI.Box(new Rect(bounds.x, bounds.y, num, bounds.height), GUIContent.none, GUI.skin.window);
				Color color2 = GUI.color;
				GUI.color = new Color(0.65f, 0.43f, 0.27f, 1f);
				GUI.DrawTexture(new Rect(bounds.x, bounds.y, 2f, bounds.height), Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(bounds.x + num - 2f, bounds.y, 2f, bounds.height), Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(bounds.x, bounds.y, num, 2f), Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(bounds.x, bounds.yMax - 2f, num, 2f), Texture2D.whiteTexture);
				GUI.color = new Color(0.9f, 0.74f, 0.53f, 1f);
				Rect rect = new Rect(bounds.x + num - 1f, bounds.y + bounds.height * 0.42f, 10f, 68f);
				GUI.DrawTexture(new Rect(rect.x + 7f, rect.y, 3f, rect.height), Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(rect.x, rect.y, 10f, 3f), Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(rect.x, rect.yMax - 3f, 10f, 3f), Texture2D.whiteTexture);
				GUI.color = color2;
			}
		}

		private static void DrawerSlice(Rect target, float x, float y, float width, float height)
		{
			GUI.DrawTextureWithTexCoords(target, drawerArt, new Rect(x / (float)drawerArt.width, 1f - (y + height) / (float)drawerArt.height, width / (float)drawerArt.width, height / (float)drawerArt.height), alphaBlend: true);
		}

		private static void RestrainMakeUpInsets(Color32[] pixels, int width, int height)
		{
			for (int i = 175; i <= 833; i++)
			{
				for (int j = 1235; j <= 1704; j++)
				{
					bool num = i <= 745 && (j <= 1246 || j >= 1691 || i <= 184 || i >= 735);
					bool flag = i >= 759 && (j <= 1247 || j >= 1691 || i <= 769 || i >= 823);
					if (num || flag)
					{
						int num2 = (height - 1 - i) * width + j;
						Color32 color = pixels[num2];
						byte num3 = Math.Max(color.r, Math.Max(color.g, color.b));
						int num4 = Math.Min(color.r, Math.Min(color.g, color.b));
						bool flag2 = (float)(int)color.b > (float)(int)color.g * 1.12f && (float)(int)color.r > (float)(int)color.g * 1.12f && (float)(int)color.b > (float)(int)color.r * 0.65f;
						bool flag3 = num3 - num4 < 26 && num4 > 125;
						if (num3 >= 65 && (flag2 || flag3))
						{
							float num5 = (float)(color.r + color.g + color.b) / 765f;
							color.r = (byte)(65f + 30f * num5);
							color.g = (byte)(43f + 22f * num5);
							color.b = (byte)(64f + 29f * num5);
							pixels[num2] = color;
						}
					}
				}
			}
		}

		internal static Rect CloseRect()
		{
			float num = ((activeMode == 0) ? 70 : ((activeMode == 1) ? 652 : 1217));
			float num2 = ((activeMode == 0) ? 567 : ((activeMode == 1) ? 1136 : 1723));
			float num3 = ((activeMode == 0) ? 507 : ((activeMode == 1) ? 1075 : 1662));
			return new Rect(140f + (num3 - num) * 560f / (num2 - num), 83.018524f, 19040f / (num2 - num), 39.537037f);
		}

		internal static void DrawCloseGlyph()
		{
			if (closeGlyph == null)
			{
				closeGlyph = CreateTexture(16, 16);
				Color32[] array = new Color32[256];
				for (int i = 2; i < 14; i++)
				{
					for (int j = 2; j < 14; j++)
					{
						float num = Math.Min(Math.Abs(j - i), Math.Abs(j + i - 15));
						byte a = (byte)((num == 0f) ? byte.MaxValue : ((num == 1f) ? 170 : 0));
						array[i * 16 + j] = new Color32
						{
							r = 241,
							g = 221,
							b = 192,
							a = a
						};
					}
				}
				closeGlyph.SetPixels32(array);
				closeGlyph.Apply(updateMipmaps: false, makeNoLongerReadable: true);
				closeGlyph.hideFlags = HideFlags.HideAndDontSave;
				closeGlyph.filterMode = FilterMode.Bilinear;
			}
			Color color = GUI.color;
			GUI.color = Color.white;
			Rect rect = CloseRect();
			GUI.DrawTexture(new Rect(rect.x + (rect.width - 16f) * 0.5f, rect.y + (rect.height - 16f) * 0.5f, 16f, 16f), closeGlyph);
			GUI.color = color;
		}

		internal static void Dispose()
		{
			if (art != null)
			{
				UnityEngine.Object.Destroy(art);
			}
			if (baseArt != null)
			{
				UnityEngine.Object.Destroy(baseArt);
			}
			if (cleanupTag != null)
			{
				UnityEngine.Object.Destroy(cleanupTag);
			}
			if (drawerArt != null)
			{
				UnityEngine.Object.Destroy(drawerArt);
			}
			if (titleArt != null)
			{
				UnityEngine.Object.Destroy(titleArt);
			}
			if (closeGlyph != null)
			{
				UnityEngine.Object.Destroy(closeGlyph);
			}
			for (int i = 0; i < crests.Length; i++)
			{
				if (crests[i] != null)
				{
					UnityEngine.Object.Destroy(crests[i]);
				}
				crests[i] = null;
			}
			for (int j = 0; j < headers.Length; j++)
			{
				if (headers[j] != null)
				{
					UnityEngine.Object.Destroy(headers[j]);
				}
				headers[j] = null;
			}
			if (titleFont != null)
			{
				UnityEngine.Object.Destroy(titleFont);
			}
			art = null;
			baseArt = null;
			cleanupTag = null;
			drawerArt = null;
			titleArt = null;
			closeGlyph = null;
			titleFont = null;
			titleStyle = null;
			attempted = false;
			footer = null;
			DrawingBody = false;
		}
	}
	internal sealed class UiPointerCaptureState
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

		internal bool Observe(bool mouseOverWindow, bool buttonDown, bool buttonHeld, bool buttonUp)
		{
			if (buttonDown)
			{
				gestureActive = true;
				gestureOwned = mouseOverWindow;
			}
			bool result = ((!(buttonDown || buttonHeld || buttonUp)) ? mouseOverWindow : (gestureActive && gestureOwned));
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
	internal sealed class WardrobeSavedAccessoryIdentity
	{
		internal readonly string Property;

		internal readonly int OriginalSlot;

		internal readonly string Guid;

		internal WardrobeSavedAccessoryIdentity(string property, int originalSlot, string guid)
		{
			Property = property;
			OriginalSlot = originalSlot;
			Guid = guid;
		}
	}
	internal static class WardrobeAccessoryIdentity
	{
		internal const int MissingSideloaderSlot = 99999999;

		internal const string AccessoryIdProperty = "ChaFileAccessory.PartsInfo.id";

		internal static int Resolve(int absoluteSlot, int category, int rawId, IList<WardrobeSavedAccessoryIdentity> saved, Func<int, string, int, string, int?> currentSlotLookup)
		{
			if (saved == null)
			{
				return rawId;
			}
			string b = "accessory" + absoluteSlot + ".ChaFileAccessory.PartsInfo.id";
			WardrobeSavedAccessoryIdentity wardrobeSavedAccessoryIdentity = null;
			for (int i = 0; i < saved.Count; i++)
			{
				WardrobeSavedAccessoryIdentity wardrobeSavedAccessoryIdentity2 = saved[i];
				if (wardrobeSavedAccessoryIdentity2 != null && string.Equals(wardrobeSavedAccessoryIdentity2.Property, b, StringComparison.Ordinal))
				{
					wardrobeSavedAccessoryIdentity = wardrobeSavedAccessoryIdentity2;
					break;
				}
			}
			if (wardrobeSavedAccessoryIdentity == null)
			{
				return rawId;
			}
			if (currentSlotLookup == null)
			{
				return 99999999;
			}
			return currentSlotLookup(wardrobeSavedAccessoryIdentity.OriginalSlot, "ChaFileAccessory.PartsInfo.id", category, wardrobeSavedAccessoryIdentity.Guid) ?? 99999999;
		}
	}
	internal static class WardrobeAccTab
	{
		private static int slot;

		private static int renderSlot;

		private static bool renderPopulated;

		private static int renderCategory = 120;

		private static bool renderComponentReady;

		private static bool renderUseColor01;

		private static bool renderUseColor02;

		private static bool renderUseColor03;

		private static bool renderUseColor04;

		private static int renderMoveGroups;

		private static readonly bool[] loadingSlot = new bool[150];

		private const int AddAccessorySlotListId = -604;

		private static readonly ChaListDefine.CategoryNo[] Cats = new ChaListDefine.CategoryNo[11]
		{
			ChaListDefine.CategoryNo.ao_none,
			ChaListDefine.CategoryNo.ao_hair,
			ChaListDefine.CategoryNo.ao_head,
			ChaListDefine.CategoryNo.ao_face,
			ChaListDefine.CategoryNo.ao_neck,
			ChaListDefine.CategoryNo.ao_body,
			ChaListDefine.CategoryNo.ao_waist,
			ChaListDefine.CategoryNo.ao_leg,
			ChaListDefine.CategoryNo.ao_arm,
			ChaListDefine.CategoryNo.ao_hand,
			ChaListDefine.CategoryNo.ao_kokan
		};

		private static readonly string[] CatEn = new string[11]
		{
			"None", "Hair", "Head", "Face", "Neck", "Body", "Waist", "Leg", "Arm", "Hand",
			"Crotch"
		};

		private static readonly string[] CatZh = new string[11]
		{
			"无", "头发", "头部", "脸部", "颈部", "身体", "腰部", "腿部", "手臂", "手部",
			"下体"
		};

		private static readonly string[] CatJa = new string[11]
		{
			"なし", "髪", "頭", "顔", "首", "体", "腰", "脚", "腕", "手",
			"股間"
		};

		private static List<WardrobeItem> categoryList;

		private static List<WardrobeItem> parentList;

		private static int categoryListLang = -1;

		private static int parentListLang = -1;

		internal static int SelectedSlot => slot;

		internal static void FocusSlot(int accSlot)
		{
			slot = accSlot;
		}

		private static string CatLabel(int idx)
		{
			switch (WardrobeLang.Current)
			{
			case 1:
				if (idx >= CatZh.Length)
				{
					return "#" + idx;
				}
				return CatZh[idx];
			case 2:
				if (idx >= CatJa.Length)
				{
					return "#" + idx;
				}
				return CatJa[idx];
			default:
				if (idx >= CatEn.Length)
				{
					return "#" + idx;
				}
				return CatEn[idx];
			}
		}

		private static int ParentToInt(string key)
		{
			if (string.IsNullOrEmpty(key))
			{
				return 0;
			}
			try
			{
				return (int)(ChaAccessoryDefine.AccessoryParentKey)Enum.Parse(typeof(ChaAccessoryDefine.AccessoryParentKey), key);
			}
			catch
			{
				return 0;
			}
		}

		private static string IntToParent(int v)
		{
			ChaAccessoryDefine.AccessoryParentKey accessoryParentKey = (ChaAccessoryDefine.AccessoryParentKey)v;
			return accessoryParentKey.ToString();
		}

		internal static void Draw(WardrobeSession s)
		{
			ChaControl cha = s.cha;
			int accessorySlotCount = s.AccessorySlotCount;
			if (accessorySlotCount <= 0)
			{
				return;
			}
			if (slot < 0 || slot >= accessorySlotCount)
			{
				slot = 0;
			}
			WardrobeWidgets.SelectorRow(s, "Slot", "acc_slot", slot, () => BuildSlotList(s), delegate(int i)
			{
				if (i == -604)
				{
					byte[] array = s.SaveCoordinateBytes();
					int num2 = s.TryAddAccessorySlot();
					if (num2 >= 0)
					{
						if (array != null)
						{
							s.PushUndoBytes(array);
						}
						else
						{
							s.PushUndo();
						}
						slot = num2;
						WardrobeGUI.SetMessage(WardrobeLang.T("Accessory slot added."));
					}
					else
					{
						WardrobeGUI.SetMessage(WardrobeLang.T("Couldn't add a new accessory slot."));
					}
				}
				else
				{
					slot = i;
				}
				WardrobeWidgets.CollapseAll();
			}, thumbs: false);
			GUILayout.Space(4f);
			if (Event.current.type == EventType.Layout)
			{
				renderSlot = slot;
			}
			int k = renderSlot;
			ListInfoBase listInfoBase = ((cha.infoAccessory != null && k < cha.infoAccessory.Length) ? cha.infoAccessory[k] : null);
			ChaAccessoryComponent accessoryComponent = cha.GetAccessoryComponent(k);
			ChaFileAccessory.PartsInfo partsInfo = cha.nowCoordinate.accessory.parts[k];
			if (Event.current.type == EventType.Layout)
			{
				renderPopulated = listInfoBase != null;
				if (renderPopulated)
				{
					renderCategory = listInfoBase.Category;
				}
				else
				{
					renderCategory = 120;
				}
				renderComponentReady = accessoryComponent != null;
				renderUseColor01 = renderComponentReady && accessoryComponent.useColor01;
				renderUseColor02 = renderComponentReady && accessoryComponent.useColor02;
				renderUseColor03 = renderComponentReady && accessoryComponent.useColor03;
				renderUseColor04 = renderComponentReady && accessoryComponent.rendAlpha != null && accessoryComponent.rendAlpha.Length != 0;
				Vector3[,] addMove = partsInfo.addMove;
				renderMoveGroups = ((addMove != null && addMove.GetLength(1) >= 3) ? addMove.GetLength(0) : 0);
			}
			bool flag = !renderPopulated;
			int category = renderCategory;
			string key = "acc_item" + k;
			string key2 = "acc_cat" + k;
			string key3 = "acc_parent" + k;
			Func<List<WardrobeItem>> listFunc = () => WardrobeCatalog.List(category);
			WardrobeWidgets.BeginZone(null);
			GUILayout.BeginHorizontal();
			if (flag)
			{
				GUILayout.Box(Texture2D.blackTexture, GUILayout.Width(96f), GUILayout.Height(96f));
			}
			else
			{
				WardrobeWidgets.CurrentThumbBox(key, partsInfo.id, listFunc, category);
			}
			GUILayout.Space(4f);
			GUILayout.BeginVertical();
			if (!flag)
			{
				WardrobeWidgets.SelectorHeader("Item", key, partsInfo.id, listFunc, 54f);
			}
			WardrobeWidgets.SelectorHeader("Category", key2, category, GetCategoryList, 54f);
			if (!flag)
			{
				WardrobeWidgets.SelectorHeader("Attach To", key3, ParentToInt(partsInfo.parentKey), GetParentList, 54f);
				GUILayout.BeginHorizontal();
				WardrobeWidgets.ToggleRow(s, "Visible", "acc_vis" + k, () => cha.fileStatus.showAccessory[k], delegate(bool v)
				{
					cha.fileStatus.showAccessory[k] = v;
					cha.SetAccessoryState(k, v);
				});
				GUILayout.FlexibleSpace();
				if (renderComponentReady && WardrobeWidgets.MiniButton("Restore"))
				{
					s.PushUndo();
					RestoreDefaultColors(cha, k, -1);
					s.MarkAccCustom(k);
				}
				if (WardrobeWidgets.MiniButton("Copy Colors"))
				{
					WardrobeColorClipboard.CopyFromAccessory(cha, k, partsInfo);
				}
				bool enabled = GUI.enabled;
				GUI.enabled = enabled && WardrobeColorClipboard.HasData;
				if (WardrobeWidgets.MiniButton("Paste Colors"))
				{
					s.PushUndo();
					WardrobeColorClipboard.PasteToAccessory(cha, k);
					s.MarkAccCustom(k);
				}
				GUI.enabled = enabled;
				GUILayout.EndHorizontal();
				if (WardrobeME.HasStudioME && WardrobeContext.InStudio && WardrobeWidgets.FillButtonRow("Mat", 54f, new string[1] { "Material Editor" }, -1, 38f) == 0 && !WardrobeME.OpenStudioAccessory(cha, k))
				{
					WardrobeGUI.SetMessage("Couldn't open Material Editor.");
				}
			}
			else
			{
				GUILayout.Label(WardrobeLang.T("(empty slot — pick a category to add an accessory)"));
			}
			GUILayout.EndVertical();
			GUILayout.EndHorizontal();
			WardrobeWidgets.EndZone();
			if (!flag)
			{
				WardrobeWidgets.SelectorBody(s, key, partsInfo.id, delegate(int id)
				{
					s.PushUndo();
					ChaFileAccessory.PartsInfo partsInfo2 = cha.nowCoordinate.accessory.parts[k];
					string parentKey = partsInfo2.parentKey;
					s.QueueEditorAccessory(k, partsInfo2.type, id, parentKey ?? "", preserveParent: true);
				}, thumbs: true, category);
			}
			WardrobeWidgets.SelectorBody(s, key2, category, delegate(int v)
			{
				s.PushUndo();
				s.QueueEditorAccessory(k, v, FirstItemId(v), "", preserveParent: false);
			}, thumbs: false);
			if (flag)
			{
				return;
			}
			WardrobeWidgets.SelectorBody(s, key3, ParentToInt(partsInfo.parentKey), delegate(int v)
			{
				s.PushUndo();
				cha.ChangeAccessoryParent(k, IntToParent(v));
				WardrobeCompat.FileCoord(cha).accessory.parts[k].parentKey = cha.nowCoordinate.accessory.parts[k].parentKey;
				s.MarkAccCustom(k);
			}, thumbs: false);
			if (renderComponentReady)
			{
				if (renderUseColor01)
				{
					DrawColorZone(s, cha, k, 0);
				}
				if (renderUseColor02)
				{
					DrawColorZone(s, cha, k, 1);
				}
				if (renderUseColor03)
				{
					DrawColorZone(s, cha, k, 2);
				}
				if (renderUseColor04)
				{
					DrawColorZone(s, cha, k, 3);
				}
				WardrobeShaderColor4TabKoi.DrawAccessory(s, k, partsInfo.id);
				for (int num = 0; num < renderMoveGroups; num++)
				{
					DrawMoveGroup(s, cha, k, num);
				}
				WardrobeWidgets.BeginZone("Physics");
				int ns = k;
				WardrobeWidgets.ToggleRow(s, "No Shake", "acc_noshake" + k, () => cha.nowCoordinate.accessory.parts[ns].noShake, delegate(bool v)
				{
					cha.nowCoordinate.accessory.parts[ns].noShake = v;
					WardrobeCompat.FileCoord(cha).accessory.parts[ns].noShake = v;
				});
				WardrobeWidgets.EndZone();
			}
		}

		private static void DrawColorZone(WardrobeSession s, ChaControl cha, int slotIndex, int ci)
		{
			int c = ci;
			Func<ChaFileAccessory.PartsInfo> P = () => cha.nowCoordinate.accessory.parts[slotIndex];
			Func<ChaFileAccessory.PartsInfo> O = () => WardrobeCompat.FileCoord(cha).accessory.parts[slotIndex];
			if (P().color == null || c >= P().color.Length)
			{
				return;
			}
			WardrobeWidgets.BeginZone((ci == 3) ? "Color 4 (alpha)" : $"Color {ci + 1}", delegate
			{
				if (WardrobeWidgets.MiniButton("Copy", 44f))
				{
					WardrobeChannelClipboard.CopyAccessoryChannel(P(), c);
				}
				bool enabled = GUI.enabled;
				GUI.enabled = enabled && WardrobeChannelClipboard.HasData;
				if (WardrobeWidgets.MiniButton("Paste", 44f))
				{
					s.PushUndo();
					WardrobeChannelClipboard.ApplyAccessoryChannel(P(), c);
					ChaFileAccessory.PartsInfo partsInfo = O();
					if (partsInfo.color != null && c < partsInfo.color.Length)
					{
						WardrobeChannelClipboard.ApplyAccessoryChannel(partsInfo, c);
					}
					cha.ChangeAccessoryColor(slotIndex);
					s.MarkAccCustom(slotIndex);
					WardrobePseudoMakerState.InvalidateAccessoryColorChannel(cha, cha.fileStatus.coordinateType, slotIndex, c);
				}
				GUI.enabled = enabled;
				if (WardrobeWidgets.MiniButton("Reset", 46f))
				{
					s.PushUndo();
					RestoreDefaultColors(cha, slotIndex, c);
					s.MarkAccCustom(slotIndex);
				}
			});
			WardrobeWidgets.SwatchColumn(s, null, "acc" + slotIndex + "_col" + c, () => P().color[c], delegate(Color v)
			{
				P().color[c] = v;
				ChaFileAccessory.PartsInfo partsInfo = O();
				if (partsInfo.color != null && c < partsInfo.color.Length)
				{
					partsInfo.color[c] = v;
				}
				cha.ChangeAccessoryColor(slotIndex);
				s.MarkAccCustom(slotIndex);
			});
			WardrobeWidgets.EndZone();
		}

		private static void DrawMoveGroup(WardrobeSession s, ChaControl cha, int slotIndex, int m)
		{
			Func<ChaFileAccessory.PartsInfo> P = () => cha.nowCoordinate.accessory.parts[slotIndex];
			Func<ChaFileAccessory.PartsInfo> O = () => WardrobeCompat.FileCoord(cha).accessory.parts[slotIndex];
			WardrobeWidgets.BeginZone(null);
			WardrobeWidgets.AxisHeaderRow($"Adjust {m + 1}");
			for (int num = 0; num < 3; num++)
			{
				int tc = num;
				string label = num switch
				{
					1 => "Rotation", 
					0 => "Position", 
					_ => "Scale", 
				};
				float step = ((num == 1) ? 1f : 0.01f);
				float coarseStep = ((num == 1) ? 10f : 0.1f);
				float def = ((num == 2) ? 1f : 0f);
				WardrobeWidgets.TripleValueRow(s, label, $"acc{slotIndex}_mv{m}_{num}", (int a) => P().addMove[m, tc][a], delegate(int a, float v)
				{
					Vector3 vector = P().addMove[m, tc];
					vector[a] = v;
					P().addMove[m, tc] = vector;
					O().addMove[m, tc] = vector;
					cha.UpdateAccessoryMoveFromInfo(slotIndex);
					s.MarkAccCustom(slotIndex);
				}, step, coarseStep, def, num == 1);
			}
			WardrobeWidgets.EndZone();
		}

		private static void RestoreDefaultColors(ChaControl cha, int slotIndex, int colorIndex)
		{
			ChaFileAccessory.PartsInfo partsInfo = cha.nowCoordinate.accessory.parts[slotIndex];
			ChaFileAccessory.PartsInfo partsInfo2 = WardrobeCompat.FileCoord(cha).accessory.parts[slotIndex];
			int num = ((partsInfo.color != null) ? Mathf.Min(partsInfo.color.Length, 4) : 0);
			for (int i = 0; i < num; i++)
			{
				if (colorIndex != -1 && colorIndex != i)
				{
					continue;
				}
				Color color = Color.white;
				if (cha.GetAccessoryDefaultColor(ref color, slotIndex, i))
				{
					partsInfo.color[i] = color;
					if (partsInfo2.color != null && i < partsInfo2.color.Length)
					{
						partsInfo2.color[i] = color;
					}
				}
			}
			cha.ChangeAccessoryColor(slotIndex);
		}

		private static int FirstItemId(int categoryNo)
		{
			foreach (WardrobeItem item in WardrobeCatalog.List(categoryNo))
			{
				if (item.id != 0)
				{
					return item.id;
				}
			}
			return 0;
		}

		private static List<WardrobeItem> BuildSlotList(WardrobeSession s)
		{
			ChaControl cha = s.cha;
			List<WardrobeItem> list = new List<WardrobeItem>();
			for (int i = 0; i < WardrobeSession.AccessorySlotCountFor(cha); i++)
			{
				string text = ((cha.infoAccessory != null && i < cha.infoAccessory.Length && cha.infoAccessory[i] != null) ? cha.infoAccessory[i].Name : "---");
				list.Add(new WardrobeItem
				{
					id = i,
					name = $"{i + 1:D2}: " + text
				});
			}
			if (s.CanAddAccessorySlot)
			{
				list.Add(new WardrobeItem
				{
					id = -604,
					name = WardrobeLang.T("Add slot")
				});
			}
			return list;
		}

		private static List<WardrobeItem> GetCategoryList()
		{
			if (categoryList == null || categoryListLang != WardrobeLang.Current)
			{
				categoryListLang = WardrobeLang.Current;
				categoryList = new List<WardrobeItem>();
				for (int i = 0; i < Cats.Length; i++)
				{
					categoryList.Add(new WardrobeItem
					{
						id = (int)Cats[i],
						name = CatLabel(i)
					});
				}
			}
			return categoryList;
		}

		private static List<WardrobeItem> GetParentList()
		{
			if (parentList == null || parentListLang != WardrobeLang.Current)
			{
				parentListLang = WardrobeLang.Current;
				parentList = new List<WardrobeItem>();
				string[] accessoryParentName = ChaAccessoryDefine.AccessoryParentName;
				for (int i = 1; i < accessoryParentName.Length; i++)
				{
					parentList.Add(new WardrobeItem
					{
						id = i,
						name = accessoryParentName[i]
					});
				}
			}
			return parentList;
		}
	}
	internal static class WardrobeAuthoredColor4Koi
	{
		private static GameObject Prefab(ListInfoBase info, ChaControl cha)
		{
			if (info == null)
			{
				return null;
			}
			string info2 = info.GetInfo(ChaListDefine.KeyType.MainAB);
			string info3 = info.GetInfo(ChaListDefine.KeyType.MainData);
			if (string.IsNullOrEmpty(info2) || string.IsNullOrEmpty(info3))
			{
				return null;
			}
			try
			{
				return CommonLib.LoadAsset<GameObject>(info2, info3, clone: false, info.GetInfo(ChaListDefine.KeyType.MainManifest));
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("Authored Color 4 asset could not be read: " + ex.GetBaseException().Message));
				return null;
			}
		}

		private static string Path(Transform child, Transform root)
		{
			List<string> list = new List<string>();
			while (child != null && child != root)
			{
				list.Add(child.name + "#" + child.GetSiblingIndex());
				child = child.parent;
			}
			if (child != root)
			{
				return null;
			}
			list.Reverse();
			return string.Join("/", list.ToArray());
		}

		private static WardrobeMaterialColor4Koi.Value Read(Material material)
		{
			WardrobeMaterialColor4Koi.Value result = new WardrobeMaterialColor4Koi.Value
			{
				Color = material.GetColor("_Color4")
			};
			if (material.HasProperty("_Glossiness4"))
			{
				result.Gloss = material.GetFloat("_Glossiness4");
				result.HasGloss = true;
			}
			if (material.HasProperty("_Metallic4"))
			{
				result.Metallic = material.GetFloat("_Metallic4");
				result.HasMetallic = true;
			}
			return result;
		}

		internal static bool TrySourceClothing(ChaControl cha, int kind, int itemId, out WardrobeMaterialColor4Koi.Value value)
		{
			value = default(WardrobeMaterialColor4Koi.Value);
			if (cha == null || itemId <= 0 || kind < 0 || kind >= 9)
			{
				return false;
			}
			ListInfoBase value2;
			try
			{
				if (!WardrobeCompat.ListCtrl.GetCategoryInfo(WardrobeSession.ClothesCategories[kind]).TryGetValue(itemId, out value2))
				{
					return false;
				}
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("Source Color 4 catalog lookup failed: " + ex.GetBaseException().Message));
				return false;
			}
			GameObject gameObject = Prefab(value2, cha);
			if (gameObject == null)
			{
				return false;
			}
			bool flag = false;
			bool flag2 = false;
			Renderer[] componentsInChildren = gameObject.GetComponentsInChildren<Renderer>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				Material[] sharedMaterials = componentsInChildren[i].sharedMaterials;
				foreach (Material material in sharedMaterials)
				{
					if (!(material == null) && material.HasProperty("_Color4"))
					{
						WardrobeMaterialColor4Koi.Value value3 = Read(material);
						bool flag3 = Math.Abs(value3.Color.r - 1f) > 0.004f || Math.Abs(value3.Color.g - 1f) > 0.004f || Math.Abs(value3.Color.b - 1f) > 0.004f;
						if (!flag || (flag3 && !flag2))
						{
							value = value3;
							flag = true;
							flag2 = flag3;
						}
					}
				}
			}
			return flag;
		}

		private static Dictionary<Material, WardrobeMaterialColor4Koi.Value> Match(GameObject live, ListInfoBase info, ChaControl cha)
		{
			Dictionary<Material, WardrobeMaterialColor4Koi.Value> dictionary = new Dictionary<Material, WardrobeMaterialColor4Koi.Value>();
			if (live == null)
			{
				return dictionary;
			}
			GameObject gameObject = Prefab(info, cha);
			if (gameObject == null)
			{
				return dictionary;
			}
			Dictionary<string, Renderer> dictionary2 = new Dictionary<string, Renderer>();
			Renderer[] componentsInChildren = gameObject.GetComponentsInChildren<Renderer>(includeInactive: true);
			foreach (Renderer renderer in componentsInChildren)
			{
				if (!(renderer == null))
				{
					string text = Path(renderer.transform, gameObject.transform);
					if (text != null && !dictionary2.ContainsKey(text))
					{
						dictionary2[text] = renderer;
					}
				}
			}
			componentsInChildren = live.GetComponentsInChildren<Renderer>(includeInactive: true);
			foreach (Renderer renderer2 in componentsInChildren)
			{
				if (renderer2 == null)
				{
					continue;
				}
				string text2 = Path(renderer2.transform, live.transform);
				if (text2 == null || !dictionary2.TryGetValue(text2, out var value))
				{
					continue;
				}
				Material[] sharedMaterials = renderer2.sharedMaterials;
				Material[] sharedMaterials2 = value.sharedMaterials;
				for (int j = 0; j < sharedMaterials.Length && j < sharedMaterials2.Length; j++)
				{
					Material material = sharedMaterials[j];
					Material material2 = sharedMaterials2[j];
					if (!(material == null) && !(material2 == null) && material.HasProperty("_Color4") && material2.HasProperty("_Color4"))
					{
						dictionary[material] = Read(material2);
					}
				}
			}
			return dictionary;
		}

		internal static Dictionary<Material, WardrobeMaterialColor4Koi.Value> Clothing(ChaControl cha, int kind, int index)
		{
			GameObject gameObject = WardrobeMaterialColor4Koi.ClothingRoot(cha, kind, index);
			ListInfoBase listInfoBase = null;
			if (index == 0 && ((UnityEngine.Object)(object)WardrobeClothingSlotsKoi.Controller(cha) == null || !WardrobeClothingSlotsKoi.Controller(cha).IsManagedPrimary(kind)) && cha != null && cha.infoClothes != null && kind >= 0 && kind < cha.infoClothes.Length)
			{
				listInfoBase = cha.infoClothes[kind];
			}
			if (listInfoBase == null && gameObject != null)
			{
				ListInfoComponent component = gameObject.GetComponent<ListInfoComponent>();
				if (component != null)
				{
					listInfoBase = component.data;
				}
			}
			return Match(gameObject, listInfoBase, cha);
		}

		internal static Dictionary<Material, WardrobeMaterialColor4Koi.Value> Accessory(ChaControl cha, int slot)
		{
			return Match(WardrobeMaterialColor4Koi.AccessoryRoot(cha, slot), (slot >= 0 && cha.infoAccessory != null && slot < cha.infoAccessory.Length) ? cha.infoAccessory[slot] : null, cha);
		}
	}
	internal static class WardrobeBorrowedBodyMaterialsKoi
	{
		private static readonly Material[] empty = new Material[0];

		internal static HashSet<Renderer> Targets(GameObject root)
		{
			HashSet<Renderer> hashSet = new HashSet<Renderer>();
			if (root == null)
			{
				return hashSet;
			}
			MonoBehaviour[] componentsInChildren = root.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
			foreach (MonoBehaviour monoBehaviour in componentsInChildren)
			{
				if (monoBehaviour == null || monoBehaviour.GetType().FullName != "KK_CopyChaBodyMat.ChaCopyBodyMaterial")
				{
					continue;
				}
				PropertyInfo property = monoBehaviour.GetType().GetProperty("RendTarget", BindingFlags.Instance | BindingFlags.Public);
				if (property == null || property.PropertyType != typeof(Renderer[]) || property.GetIndexParameters().Length != 0 || !(property.GetValue(monoBehaviour, null) is Renderer[] array))
				{
					continue;
				}
				Renderer[] array2 = array;
				foreach (Renderer renderer in array2)
				{
					if (renderer != null)
					{
						hashSet.Add(renderer);
					}
				}
			}
			return hashSet;
		}

		internal static Renderer[] Filter(GameObject root, Renderer[] renderers)
		{
			List<Renderer> list = new List<Renderer>();
			HashSet<Renderer> hashSet = Targets(root);
			if (renderers != null)
			{
				foreach (Renderer renderer in renderers)
				{
					if (renderer != null && !hashSet.Contains(renderer))
					{
						list.Add(renderer);
					}
				}
			}
			return list.ToArray();
		}

		internal static Material[] Materials(Renderer renderer, HashSet<Renderer> borrowed)
		{
			if (!(renderer == null) && !borrowed.Contains(renderer))
			{
				return renderer.sharedMaterials;
			}
			return empty;
		}
	}
	internal static class WardrobeCardBrowser
	{
		private sealed class TargetPosition
		{
			internal string Root;

			internal string Directory;

			internal string Search;

			internal Vector2 Scroll;
		}

		private const int Cols = 3;

		private const float GridW = 440f;

		private const float NameH = 18f;

		private const int LoadsPerFrame = 4;

		private static int openFor;

		private static Action<string> imagePick;

		private static string root = "";

		private static string curDir;

		private static string[] subDirs = new string[0];

		private static string[] files = new string[0];

		private static string search = "";

		private static string appliedSearch = "";

		private static string pendingDir;

		private static readonly WardrobeSearchCache<string> searchCache = new WardrobeSearchCache<string>(Path.GetFileNameWithoutExtension);

		private static Vector2 scroll;

		private static readonly Dictionary<int, TargetPosition> targetPositions = new Dictionary<int, TargetPosition>();

		private static GUIStyle nameStyle;

		private static GUIStyle headStyle;

		private static readonly Dictionary<string, Texture2D> thumbs = new Dictionary<string, Texture2D>();

		private static int loadFrame = -1;

		private static int loadsThisFrame;

		internal static bool IsOpen => openFor != 0;

		internal static bool IsImagePick => openFor == -1;

		internal static void OpenImage(string rootDir, Action<string> onPick)
		{
			RememberTargetPosition();
			openFor = -1;
			imagePick = onPick;
			root = rootDir;
			curDir = rootDir;
			Refresh();
		}

		internal static void Open(int which, string rootDir)
		{
			RememberTargetPosition();
			openFor = which;
			root = rootDir;
			int num;
			string directory;
			if (targetPositions.TryGetValue(which, out var value) && string.Equals(Path.GetFullPath(value.Root), Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
			{
				num = (Directory.Exists(value.Directory) ? 1 : 0);
				if (num != 0)
				{
					directory = value.Directory;
					goto IL_005b;
				}
			}
			else
			{
				num = 0;
			}
			directory = root;
			goto IL_005b;
			IL_005b:
			curDir = directory;
			search = (appliedSearch = ((num != 0) ? value.Search : ""));
			Refresh();
			if (num != 0)
			{
				scroll = value.Scroll;
			}
		}

		private static void RememberTargetPosition()
		{
			if (openFor == 2 || openFor == 3)
			{
				targetPositions[openFor] = new TargetPosition
				{
					Root = root,
					Directory = curDir,
					Search = appliedSearch,
					Scroll = scroll
				};
			}
		}

		internal static void Close()
		{
			RememberTargetPosition();
			openFor = 0;
			imagePick = null;
			foreach (Texture2D value in thumbs.Values)
			{
				if (value != null)
				{
					UnityEngine.Object.Destroy(value);
				}
			}
			thumbs.Clear();
			searchCache.Clear();
			pendingDir = null;
		}

		private static void Refresh()
		{
			pendingDir = null;
			try
			{
				subDirs = (Directory.Exists(curDir) ? Directory.GetDirectories(curDir) : new string[0]);
				files = (Directory.Exists(curDir) ? Directory.GetFiles(curDir, "*.png") : new string[0]);
				Array.Sort(subDirs, StringComparer.OrdinalIgnoreCase);
				Dictionary<string, DateTime> stamps = new Dictionary<string, DateTime>(files.Length);
				string[] array = files;
				foreach (string text in array)
				{
					stamps[text] = File.GetLastWriteTimeUtc(text);
				}
				Array.Sort(files, (string a, string b) => stamps[b].CompareTo(stamps[a]));
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogError((object)("Card browser refresh failed: " + ex));
				subDirs = new string[0];
				files = new string[0];
			}
			scroll = Vector2.zero;
		}

		private static Texture2D GetThumb(string path)
		{
			if (thumbs.TryGetValue(path, out var value))
			{
				return value;
			}
			if (loadFrame != Time.frameCount)
			{
				loadFrame = Time.frameCount;
				loadsThisFrame = 0;
			}
			if (loadsThisFrame >= 4)
			{
				return null;
			}
			loadsThisFrame++;
			try
			{
				Texture2D texture2D = new Texture2D(2, 2, TextureFormat.ARGB32, mipChain: false);
				if (!WardrobeCompat.LoadImage(texture2D, File.ReadAllBytes(path)))
				{
					UnityEngine.Object.Destroy(texture2D);
					thumbs[path] = null;
					return null;
				}
				thumbs[path] = texture2D;
				return texture2D;
			}
			catch
			{
				thumbs[path] = null;
				return null;
			}
		}

		internal static void Draw(WardrobeSession s)
		{
			if (Event.current.type == EventType.Layout && pendingDir != null)
			{
				curDir = pendingDir;
				Refresh();
			}
			if (nameStyle == null)
			{
				nameStyle = new GUIStyle(GUI.skin.label)
				{
					fontSize = 10,
					alignment = TextAnchor.UpperCenter,
					clipping = TextClipping.Clip,
					wordWrap = false
				};
				headStyle = new GUIStyle(GUI.skin.label)
				{
					fontStyle = FontStyle.Bold
				};
			}
			GUILayout.BeginHorizontal();
			GUILayout.Label(IsImagePick ? WardrobeLang.T("Select overlay image") : $"Select coordinate card for T{openFor - 1}", headStyle);
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("× " + WardrobeLang.T("Cancel"), GUILayout.Width(90f)))
			{
				Close();
				return;
			}
			GUILayout.EndHorizontal();
			bool flag = string.Equals(Path.GetFullPath(curDir).TrimEnd('\\'), Path.GetFullPath(root).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
			GUILayout.BeginHorizontal();
			bool enabled = GUI.enabled;
			GUI.enabled = !flag;
			if (GUILayout.Button("▲ Up", GUILayout.Width(60f)))
			{
				pendingDir = Path.GetDirectoryName(curDir.TrimEnd('\\'));
			}
			GUI.enabled = enabled;
			GUILayout.Label((!flag) ? ("…\\" + Path.GetFileName(curDir.TrimEnd('\\'))) : (IsImagePick ? Path.GetFileName(root.TrimEnd('\\')) : "coordinate\\female"), GUILayout.ExpandWidth(expand: true));
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			GUILayout.Label("Search:", GUILayout.Width(52f));
			search = GUILayout.TextField(search ?? "", GUILayout.ExpandWidth(expand: true));
			GUILayout.EndHorizontal();
			if (Event.current.type == EventType.Layout && !string.Equals(appliedSearch, search, StringComparison.Ordinal))
			{
				appliedSearch = search;
				scroll = Vector2.zero;
			}
			List<string> list = searchCache.Get(files, appliedSearch, Event.current.type == EventType.Layout);
			float num = 142f;
			float num2 = Mathf.Min(num - 10f, 128f);
			float num3 = (IsImagePick ? num2 : (num2 * 352f / 252f));
			float num4 = num3 + 18f + 8f;
			int num5 = (list.Count + 3 - 1) / 3;
			float num6 = (float)subDirs.Length * 26f;
			float num7 = Mathf.Min(num6 + (float)num5 * num4 + 10f, 3f * num4);
			if (!IsImagePick)
			{
				scroll.y = Mathf.Clamp(scroll.y, 0f, Mathf.Max(0f, num6 + (float)num5 * num4 + 10f - num7));
			}
			string text = null;
			string text2 = null;
			scroll = GUILayout.BeginScrollView(scroll, GUI.skin.box, GUILayout.Height(num7), GUILayout.ExpandWidth(expand: true));
			scroll.x = 0f;
			string[] array = subDirs;
			foreach (string text3 in array)
			{
				if (GUILayout.Button("► " + Path.GetFileName(text3), GUILayout.Height(22f)))
				{
					text = text3;
				}
			}
			if (list.Count == 0 && subDirs.Length == 0)
			{
				GUILayout.Label("(no coordinate cards in this folder)");
			}
			int num8 = Mathf.Max(0, (int)((scroll.y - num6) / num4) - 1);
			int num9 = (int)(num7 / num4) + 3;
			int num10 = Mathf.Min(num5, num8 + num9);
			if (num8 > 0)
			{
				GUILayout.Space((float)num8 * num4);
			}
			for (int j = num8; j < num10; j++)
			{
				Rect rect = GUILayoutUtility.GetRect(440f, num4, GUILayout.Width(440f));
				for (int k = 0; k < 3; k++)
				{
					int num11 = j * 3 + k;
					if (num11 >= list.Count)
					{
						break;
					}
					string text4 = list[num11];
					Rect rect2 = new Rect(rect.x + (float)k * num, rect.y, num, num4);
					Rect position = new Rect(rect2.x + (num - num2) / 2f, rect2.y + 3f, num2, num3);
					Texture2D thumb = GetThumb(text4);
					if (GUI.Button(position, (thumb != null) ? thumb : Texture2D.blackTexture, GUI.skin.box))
					{
						text2 = text4;
					}
					if (thumb == null)
					{
						GUI.Label(new Rect(position.x, position.y + position.height / 2f - 9f, position.width, 18f), "loading…", nameStyle);
					}
					if (position.Contains(Event.current.mousePosition))
					{
						Color color = GUI.color;
						GUI.color = new Color(0.45f, 0.8f, 1f, 0.9f);
						GUI.DrawTexture(new Rect(position.x, position.y, position.width, 1f), Texture2D.whiteTexture);
						GUI.DrawTexture(new Rect(position.x, position.yMax - 1f, position.width, 1f), Texture2D.whiteTexture);
						GUI.DrawTexture(new Rect(position.x, position.y, 1f, position.height), Texture2D.whiteTexture);
						GUI.DrawTexture(new Rect(position.xMax - 1f, position.y, 1f, position.height), Texture2D.whiteTexture);
						GUI.color = color;
					}
					string text5 = Path.GetFileNameWithoutExtension(text4);
					if (text5.Length > 26)
					{
						text5 = text5.Substring(0, 25) + "…";
					}
					GUI.Label(new Rect(rect2.x + 2f, position.yMax + 1f, rect2.width - 4f, 18f), text5, nameStyle);
				}
			}
			if (num10 < num5)
			{
				GUILayout.Space((float)(num5 - num10) * num4);
			}
			GUILayout.EndScrollView();
			if (text != null)
			{
				pendingDir = text;
			}
			else
			{
				if (text2 == null)
				{
					return;
				}
				if (IsImagePick)
				{
					Action<string> action = imagePick;
					Close();
					action?.Invoke(text2);
					return;
				}
				int num12 = openFor;
				Close();
				if (s.LoadTargetCard(num12, text2))
				{
					WardrobeGUI.SetMessage($"T{num12 - 1} ← {Path.GetFileNameWithoutExtension(text2)}");
				}
			}
		}
	}
	internal static class WardrobeCelebration
	{
		private struct Particle
		{
			public Vector2 dir;

			public float speed;

			public float size;

			public float delay;

			public float life;

			public float grav;

			public float flicker;

			public int type;
		}

		private struct Twinkle
		{
			public Vector2 uv;

			public float size;

			public float phase;

			public float speed;

			public float delay;
		}

		private static string dustMode;

		private static float dustStart = -1f;

		private const float DustDuration = 3.2f;

		private const float PopDur = 0.35f;

		private const float BurstT = 0.3f;

		private const float ShineStart = 0.6f;

		private const float ShineDur = 0.7f;

		private const float PoofStart = 1.85f;

		private const float PoofDur = 0.45f;

		private const float EndT = 2.6f;

		private static float start;

		private static Texture2D card;

		private static Rect origin;

		private static Color frameCol;

		private static Particle[] parts;

		private static Twinkle[] twinkles;

		private static Texture2D glowTex;

		private static Texture2D bandTex;

		private static Texture2D edgeTex;

		private const float FlourishDur = 1.8f;

		private static float flourishStart;

		private static Twinkle[] flourishTw;

		internal static bool Active => start > 0f;

		internal static Texture2D Glow
		{
			get
			{
				EnsureTex();
				return glowTex;
			}
		}

		internal static Texture2D EdgeGlow
		{
			get
			{
				EnsureTex();
				return edgeTex;
			}
		}

		private static float DustSample(int index, int channel)
		{
			int num = index * 73856093 + channel * 19349663 + 83492791;
			int num2 = (num ^ (num >>> 16)) * 2146121005;
			int num3 = (num2 ^ (num2 >>> 15)) * -2073254261;
			return (float)(uint)((num3 ^ (num3 >>> 16)) & 0xFFFFFF) / 16777215f;
		}

		internal static void ResetModeDust()
		{
			dustMode = null;
			dustStart = -1f;
		}

		internal static void ObserveMode(string pageId)
		{
			if (!string.IsNullOrEmpty(pageId) && !(pageId == dustMode))
			{
				if (dustMode != null)
				{
					dustStart = Time.realtimeSinceStartup;
				}
				dustMode = pageId;
			}
		}

		internal static void DrawModeDust(Rect frame)
		{
			if (dustStart < 0f || Event.current.type != EventType.Repaint)
			{
				return;
			}
			float num = Time.realtimeSinceStartup - dustStart;
			if (num >= 3.2f)
			{
				dustStart = -1f;
				return;
			}
			Texture2D glow = Glow;
			Color color = GUI.color;
			try
			{
				for (int i = 0; i < 96; i++)
				{
					float num2 = DustSample(i, 0);
					float num3 = num - DustSample(i, 1) * 0.45f;
					float num4 = 1.1f + DustSample(i, 2) * 1.5f;
					if (!(num3 < 0f) && !(num3 >= num4))
					{
						float num5 = num3 / num4;
						float num6 = Mathf.Clamp01(num3 / 0.08f) * Mathf.Pow(1f - num5, 0.7f);
						float num7 = 0.7f + 0.3f * Mathf.Sin(num3 * 13f + (float)i * 2.4f);
						float num8 = 6f + DustSample(i, 3) * 6f;
						float num9 = frame.x + (0.04f + num2 * 0.92f) * frame.width;
						float num10 = frame.yMax - 3f;
						float num11 = (DustSample(i, 4) - 0.5f) * 110f;
						float num12 = 20f + DustSample(i, 5) * 95f;
						if (i >= 60 && i < 90)
						{
							float num13 = ((i % 2 == 0) ? (-1f) : 1f);
							num9 = ((num13 < 0f) ? (frame.x + 3f) : (frame.xMax - 3f));
							num10 = frame.y + (0.08f + num2 * 0.84f) * frame.height;
							num11 = num13 * (30f + DustSample(i, 4) * 80f);
							num12 = -45f + DustSample(i, 5) * 75f;
						}
						else if (i >= 90)
						{
							num10 = frame.y + 3f;
							num12 = -70f - DustSample(i, 5) * 55f;
						}
						num9 += num11 * num3;
						num10 += num12 * num3 + 0.5f * (60f + DustSample(i, 6) * 90f) * num3 * num3;
						GUI.color = new Color(1f, 0.98f, 0.91f, color.a * num6 * num7);
						GUI.DrawTexture(new Rect(num9 - num8 * 0.5f, num10 - num8 * 0.5f, num8, num8), glow, ScaleMode.ScaleToFit, alphaBlend: true);
						GUI.color = new Color(1f, 1f, 1f, color.a * num6);
						GUI.DrawTexture(new Rect(num9 - 1f, num10 - 1f, 2f, 2f), Texture2D.whiteTexture);
					}
				}
			}
			finally
			{
				GUI.color = color;
			}
		}

		internal static void Begin(Texture2D cardTex, Rect originRect, Color frameColor)
		{
			card = cardTex;
			origin = ((originRect.width > 1f) ? originRect : new Rect(200f, 300f, 40f, 53f));
			frameCol = new Color(frameColor.r, frameColor.g, frameColor.b, 1f);
			start = Time.realtimeSinceStartup;
			System.Random random = new System.Random();
			parts = new Particle[46];
			for (int i = 0; i < parts.Length; i++)
			{
				double num = random.NextDouble() * Math.PI * 2.0;
				Particle particle = default(Particle);
				particle.dir = new Vector2((float)Math.Cos(num), (float)Math.Sin(num));
				bool flag = i >= 38;
				particle.delay = (flag ? 1.85f : (0.3f + (float)random.NextDouble() * 0.3f));
				particle.speed = (flag ? (50f + (float)random.NextDouble() * 90f) : (140f + (float)random.NextDouble() * 330f));
				particle.size = 13f + (float)random.NextDouble() * 19f;
				particle.life = 0.9f + (float)random.NextDouble() * 0.8f;
				int num2 = i % 4;
				particle.type = ((!flag) ? (num2 switch
				{
					2 => 3, 
					0 => 0, 
					_ => 2, 
				}) : 0);
				particle.grav = ((particle.type >= 2) ? (-70f) : 90f);
				particle.flicker = (float)(random.NextDouble() * 20.0);
				parts[i] = particle;
			}
			twinkles = new Twinkle[16];
			for (int j = 0; j < twinkles.Length; j++)
			{
				Twinkle twinkle = new Twinkle
				{
					uv = new Vector2(0.06f + (float)random.NextDouble() * 0.88f, 0.06f + (float)random.NextDouble() * 0.88f),
					size = 13f + (float)random.NextDouble() * 15f,
					phase = (float)(random.NextDouble() * Math.PI * 2.0),
					speed = 4f + (float)random.NextDouble() * 5f,
					delay = 0.3f + (float)random.NextDouble() * 0.6f
				};
				twinkles[j] = twinkle;
			}
		}

		internal static void Draw(Rect win)
		{
			if (start <= 0f)
			{
				return;
			}
			float num = Time.realtimeSinceStartup - start;
			if (num >= 2.6f)
			{
				start = 0f;
				card = null;
				parts = null;
				twinkles = null;
			}
			else
			{
				if (Event.current.type != EventType.Repaint)
				{
					return;
				}
				EnsureTex();
				float num2 = win.width * 0.56f;
				float num3 = num2 * 1.3968254f;
				Vector2 vector = new Vector2(win.width * 0.5f, win.height * 0.47f);
				Rect rect = new Rect(vector.x - num2 / 2f, vector.y - num3 / 2f, num2, num3);
				Color color = GUI.color;
				float num4 = (num - 0.3f) / 0.5f;
				if (num4 >= 0f && num4 < 1f)
				{
					float num5 = Mathf.Lerp(0.6f, 3.4f, num4) * num2;
					GUI.color = new Color(1f, 0.95f, 0.78f, 0.9f * (1f - num4) * (1f - num4));
					DrawCentered(glowTex, vector, num5, num5);
				}
				float num6 = 1f;
				Rect rect2;
				if (num < 0.35f)
				{
					float u = EaseOutBack(num / 0.35f);
					rect2 = LerpRect(origin, rect, u);
				}
				else if (num >= 1.85f)
				{
					float num7 = Mathf.Clamp01((num - 1.85f) / 0.45f);
					num6 = 1f - num7;
					rect2 = ScaleRect(rect, 1f - 0.45f * num7 * num7);
					float num8 = num2 * (1f + 1.7f * num7);
					GUI.color = new Color(1f, 1f, 1f, 0.5f * (1f - num7));
					DrawCentered(glowTex, vector, num8, num8);
				}
				else
				{
					rect2 = ScaleRect(rect, 1f + 0.015f * Mathf.Sin((num - 0.35f) * 6f));
				}
				if (parts != null)
				{
					for (int i = 0; i < parts.Length; i++)
					{
						Particle particle = parts[i];
						float num9 = num - particle.delay;
						if (!(num9 <= 0f) && !(num9 >= particle.life))
						{
							float num10 = num9 / particle.life;
							float a = Mathf.Clamp01(num10 * 8f) * Mathf.Clamp01((1f - num10) / 0.35f);
							Vector2 c = vector + particle.dir * (particle.speed * num9) + new Vector2(0f, particle.grav * num9 * num9);
							Texture2D texture2D = ((particle.type == 2) ? WardrobeEmoji.Tex("hearts") : ((particle.type == 3) ? WardrobeEmoji.Tex("sparkheart") : glowTex));
							if (texture2D == null)
							{
								texture2D = glowTex;
							}
							GUI.color = ((texture2D == glowTex) ? new Color(1f, 0.93f, 0.62f, a) : new Color(1f, 1f, 1f, a));
							DrawCentered(texture2D, c, particle.size, particle.size);
						}
					}
				}
				if (num6 > 0.01f)
				{
					GUI.color = new Color(1f, 0.9f, 0.6f, 0.45f * num6);
					GUI.DrawTexture(new Rect(rect2.x - 26f, rect2.y - 26f, rect2.width + 52f, rect2.height + 52f), glowTex, ScaleMode.StretchToFill);
					DrawFrame(rect2, frameCol, num6);
					GUI.color = new Color(0.07f, 0.07f, 0.09f, num6);
					GUI.DrawTexture(rect2, Texture2D.whiteTexture, ScaleMode.StretchToFill);
					GUI.color = new Color(1f, 1f, 1f, num6);
					if (card != null)
					{
						GUI.DrawTexture(rect2, card, ScaleMode.ScaleToFit);
					}
					if (num < 1.6f)
					{
						float num11 = Mathf.Pow(1f - num / 1.6f, 1.35f);
						Color color2 = Color.Lerp(new Color(1f, 0.72f, 0.25f), new Color(1f, 1f, 0.92f), num11);
						GUI.color = new Color(color2.r, color2.g, color2.b, num11 * num6);
						GUI.DrawTexture(rect2, edgeTex, ScaleMode.StretchToFill);
					}
					float num12 = (num - 0.6f) / 0.7f;
					if (num12 >= 0f && num12 < 1f && card != null)
					{
						GUI.BeginGroup(rect2);
						float num13 = rect2.height * 0.3f;
						float num14 = 0.33f * rect2.width;
						float num15 = Mathf.Lerp(0f - num13 - num14, rect2.height + num13, num12);
						GUI.color = new Color(1f, 1f, 1f, 0.55f * num6);
						float num16 = rect2.width / 16f;
						for (int j = 0; j < 16; j++)
						{
							GUI.DrawTexture(new Rect((float)j * num16, num15 + num14 * ((float)j + 0.5f) / 16f, num16 + 1f, num13), bandTex, ScaleMode.StretchToFill);
						}
						GUI.EndGroup();
					}
				}
				if (twinkles != null && num6 > 0.01f)
				{
					Texture2D texture2D2 = WardrobeEmoji.Tex("sparkles");
					for (int k = 0; k < twinkles.Length; k++)
					{
						Twinkle twinkle = twinkles[k];
						float num17 = num - twinkle.delay;
						if (!(num17 <= 0f))
						{
							float num18 = Mathf.Sin(num17 * twinkle.speed + twinkle.phase);
							float num19 = Mathf.Clamp01(num17 * 4f) * num6 * Mathf.Pow(Mathf.Max(0f, num18), 2f);
							if (!(num19 < 0.02f))
							{
								float num20 = twinkle.size * (0.55f + 0.45f * Mathf.Abs(num18));
								Vector2 c2 = new Vector2(rect2.x + twinkle.uv.x * rect2.width, rect2.y + twinkle.uv.y * rect2.height);
								GUI.color = new Color(1f, 1f, 1f, num19);
								DrawCentered((texture2D2 != null) ? texture2D2 : glowTex, c2, num20, num20);
							}
						}
					}
				}
				GUI.color = color;
			}
		}

		internal static void BeginFlourish()
		{
			flourishStart = Time.realtimeSinceStartup;
			System.Random random = new System.Random();
			flourishTw = new Twinkle[8];
			for (int i = 0; i < flourishTw.Length; i++)
			{
				Twinkle twinkle = new Twinkle
				{
					uv = new Vector2(0.08f + (float)random.NextDouble() * 0.84f, 0.08f + (float)random.NextDouble() * 0.84f),
					size = 9f + (float)random.NextDouble() * 8f,
					phase = (float)(random.NextDouble() * Math.PI * 2.0),
					speed = 5f + (float)random.NextDouble() * 4f,
					delay = (float)random.NextDouble() * 0.4f
				};
				flourishTw[i] = twinkle;
			}
		}

		internal static void DrawFlourish(Rect r)
		{
			if (flourishStart <= 0f)
			{
				return;
			}
			float num = Time.realtimeSinceStartup - flourishStart;
			if (num >= 1.8f)
			{
				flourishStart = 0f;
				flourishTw = null;
			}
			else
			{
				if (Event.current.type != EventType.Repaint)
				{
					return;
				}
				EnsureTex();
				Color color = GUI.color;
				float num2 = Mathf.Pow(1f - num / 1.8f, 1.35f);
				Color color2 = Color.Lerp(new Color(1f, 0.72f, 0.25f), new Color(1f, 1f, 0.92f), num2);
				GUI.color = new Color(color2.r, color2.g, color2.b, num2);
				GUI.DrawTexture(r, edgeTex, ScaleMode.StretchToFill);
				float num3 = Mathf.Clamp01((1.8f - num) / 0.4f);
				Texture2D texture2D = WardrobeEmoji.Tex("sparkles");
				for (int i = 0; i < flourishTw.Length; i++)
				{
					Twinkle twinkle = flourishTw[i];
					float num4 = num - twinkle.delay;
					if (!(num4 <= 0f))
					{
						float num5 = Mathf.Sin(num4 * twinkle.speed + twinkle.phase);
						float num6 = Mathf.Clamp01(num4 * 4f) * num3 * Mathf.Pow(Mathf.Max(0f, num5), 2f);
						if (!(num6 < 0.02f))
						{
							float num7 = twinkle.size * (0.55f + 0.45f * Mathf.Abs(num5));
							Vector2 c = new Vector2(r.x + twinkle.uv.x * r.width, r.y + twinkle.uv.y * r.height);
							GUI.color = new Color(1f, 1f, 1f, num6);
							DrawCentered((texture2D != null) ? texture2D : glowTex, c, num7, num7);
						}
					}
				}
				GUI.color = color;
			}
		}

		private static void DrawFrame(Rect r, Color col, float a)
		{
			Rect rect = new Rect(r.x - 6f, r.y - 6f, r.width + 12f, r.height + 12f);
			Texture2D whiteTexture = Texture2D.whiteTexture;
			GUI.color = new Color(col.r, col.g, col.b, a);
			GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 6f), whiteTexture);
			GUI.DrawTexture(new Rect(rect.x, rect.yMax - 6f, rect.width, 6f), whiteTexture);
			GUI.DrawTexture(new Rect(rect.x, rect.y + 6f, 6f, rect.height - 12f), whiteTexture);
			GUI.DrawTexture(new Rect(rect.xMax - 6f, rect.y + 6f, 6f, rect.height - 12f), whiteTexture);
			GUI.color = new Color(Mathf.Lerp(col.r, 1f, 0.55f), Mathf.Lerp(col.g, 1f, 0.55f), Mathf.Lerp(col.b, 1f, 0.55f), a);
			GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1.5f), whiteTexture);
			GUI.DrawTexture(new Rect(rect.x, rect.y, 1.5f, rect.height), whiteTexture);
			GUI.color = new Color(col.r * 0.35f, col.g * 0.35f, col.b * 0.35f, a);
			GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1.5f, rect.width, 1.5f), whiteTexture);
			GUI.DrawTexture(new Rect(rect.xMax - 1.5f, rect.y, 1.5f, rect.height), whiteTexture);
			GUI.color = new Color(0f, 0f, 0f, 0.45f * a);
			GUI.DrawTexture(new Rect(r.x - 1f, r.y - 1f, r.width + 2f, 1f), whiteTexture);
			GUI.DrawTexture(new Rect(r.x - 1f, r.yMax, r.width + 2f, 1f), whiteTexture);
			GUI.DrawTexture(new Rect(r.x - 1f, r.y, 1f, r.height), whiteTexture);
			GUI.DrawTexture(new Rect(r.xMax, r.y, 1f, r.height), whiteTexture);
		}

		private static void DrawCentered(Texture2D tex, Vector2 c, float w, float h)
		{
			GUI.DrawTexture(new Rect(c.x - w / 2f, c.y - h / 2f, w, h), tex, ScaleMode.StretchToFill);
		}

		private static Rect LerpRect(Rect a, Rect b, float u)
		{
			return new Rect(a.x + (b.x - a.x) * u, a.y + (b.y - a.y) * u, a.width + (b.width - a.width) * u, a.height + (b.height - a.height) * u);
		}

		private static Rect ScaleRect(Rect r, float s)
		{
			return new Rect(r.center.x - r.width * s / 2f, r.center.y - r.height * s / 2f, r.width * s, r.height * s);
		}

		private static float EaseOutBack(float u)
		{
			float num = u - 1f;
			return 1f + num * num * (num * 3.2f + 2.2f);
		}

		private static void EnsureTex()
		{
			if (glowTex != null)
			{
				return;
			}
			glowTex = new Texture2D(64, 64, TextureFormat.RGBA32, mipChain: false);
			for (int i = 0; i < 64; i++)
			{
				for (int j = 0; j < 64; j++)
				{
					float num = ((float)j + 0.5f) / 64f * 2f - 1f;
					float num2 = ((float)i + 0.5f) / 64f * 2f - 1f;
					float num3 = Mathf.Sqrt(num * num + num2 * num2);
					float a = Mathf.Pow(Mathf.Clamp01(1f - num3), 2.2f);
					glowTex.SetPixel(j, i, new Color(1f, 1f, 1f, a));
				}
			}
			glowTex.wrapMode = TextureWrapMode.Clamp;
			glowTex.Apply();
			bandTex = new Texture2D(1, 64, TextureFormat.RGBA32, mipChain: false);
			for (int k = 0; k < 64; k++)
			{
				bandTex.SetPixel(0, k, new Color(1f, 1f, 1f, Mathf.Sin((float)Math.PI * ((float)k + 0.5f) / 64f)));
			}
			bandTex.wrapMode = TextureWrapMode.Clamp;
			bandTex.Apply();
			edgeTex = new Texture2D(96, 96, TextureFormat.RGBA32, mipChain: false);
			for (int l = 0; l < 96; l++)
			{
				for (int m = 0; m < 96; m++)
				{
					float num4 = ((float)m + 0.5f) / 96f;
					float num5 = ((float)l + 0.5f) / 96f;
					float num6 = Mathf.Min(Mathf.Min(num4, 1f - num4), Mathf.Min(num5, 1f - num5));
					float a2 = Mathf.Pow(Mathf.Clamp01(1f - num6 / 0.33f), 1.6f);
					edgeTex.SetPixel(m, l, new Color(1f, 1f, 1f, a2));
				}
			}
			edgeTex.wrapMode = TextureWrapMode.Clamp;
			edgeTex.Apply();
		}
	}
	internal static class WardrobeChannelClipboard
	{
		private static bool has;

		private static bool fromClothing;

		private static bool hasGloss;

		private static bool hasMetallic;

		private static Color baseColor;

		private static Color patternColor;

		private static float glossPower;

		private static float metallicPower;

		private static int pattern;

		private static Vector2 tiling;

		private static Vector2 offset;

		private static float rotate;

		internal static bool HasData => has;

		internal static bool HasClothingPattern
		{
			get
			{
				if (has)
				{
					return fromClothing;
				}
				return false;
			}
		}

		internal static void CopyChannel(ChaFileClothes.PartsInfo part, int c)
		{
			if (part == null || part.colorInfo == null || c < 0 || c >= part.colorInfo.Length)
			{
				has = false;
				return;
			}
			ChaFileClothes.PartsInfo.ColorInfo obj = part.colorInfo[c];
			fromClothing = true;
			hasGloss = (hasMetallic = false);
			baseColor = obj.baseColor;
			pattern = obj.pattern;
			patternColor = obj.patternColor;
			tiling = obj.tiling;
			offset = obj.offset;
			rotate = obj.rotate;
			has = true;
		}

		internal static void ApplyChannel(ChaFileClothes.PartsInfo part, int c)
		{
			if (has && part != null && part.colorInfo != null && c >= 0 && c < part.colorInfo.Length)
			{
				ChaFileClothes.PartsInfo.ColorInfo colorInfo = part.colorInfo[c];
				colorInfo.baseColor = baseColor;
				if (fromClothing)
				{
					colorInfo.pattern = pattern;
					colorInfo.patternColor = patternColor;
					colorInfo.tiling = tiling;
					colorInfo.offset = offset;
					colorInfo.rotate = rotate;
				}
			}
		}

		internal static void CopyAccessoryChannel(ChaFileAccessory.PartsInfo part, int c)
		{
			if (part == null || part.color == null || c < 0 || c >= part.color.Length)
			{
				has = false;
				return;
			}
			baseColor = part.color[c];
			fromClothing = false;
			hasGloss = (hasMetallic = false);
			has = true;
		}

		internal static void ApplyAccessoryChannel(ChaFileAccessory.PartsInfo part, int c)
		{
			if (has && part != null && part.color != null && c >= 0 && c < part.color.Length)
			{
				part.color[c] = baseColor;
			}
		}

		internal static void CopyMaterialChannel(WardrobeMaterialColor4Koi.Value source)
		{
			baseColor = source.Color;
			glossPower = source.Gloss;
			metallicPower = source.Metallic;
			hasGloss = source.HasGloss;
			hasMetallic = source.HasMetallic;
			fromClothing = false;
			has = true;
		}

		internal static WardrobeMaterialColor4Koi.Value ApplyMaterialChannel(WardrobeMaterialColor4Koi.Value target)
		{
			if (!has)
			{
				return target;
			}
			target.Color = baseColor;
			if (hasGloss && target.HasGloss)
			{
				target.Gloss = glossPower;
			}
			if (hasMetallic && target.HasMetallic)
			{
				target.Metallic = metallicPower;
			}
			return target;
		}
	}
	internal static class WardrobeCleanupLogic
	{
		internal static bool IsClothesCategory(int selection, int clothesCount)
		{
			if (selection >= 0)
			{
				return selection < clothesCount;
			}
			return false;
		}

		internal static int AccessoryIndex(int selection, int clothesCount)
		{
			return selection - clothesCount;
		}

		internal static void AddArchive(Dictionary<string, List<string>> filesByGuid, string guid, string path)
		{
			if (filesByGuid == null || string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(path))
			{
				return;
			}
			if (!filesByGuid.TryGetValue(guid, out var value))
			{
				value = (filesByGuid[guid] = new List<string>());
			}
			for (int i = 0; i < value.Count; i++)
			{
				if (string.Equals(value[i], path, StringComparison.OrdinalIgnoreCase))
				{
					return;
				}
			}
			value.Add(path);
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
	internal static class WardrobeClothesTab
	{
		private static readonly int[] selectedNumber = new int[9];

		private static int[] layoutNumberCounts = new int[9];

		private static float numberFlashUntil;

		private static WardrobeSession pendingSession;

		private static Action pendingNumberAction;

		private static int pendingSelectionKind = -1;

		private static int pendingSelectionIndex;

		private static int slot;

		private static readonly string[] ThreeStateNames = new string[3] { "ON", "Half", "OFF" };

		private static readonly int[] ThreeStates = new int[3] { 0, 1, 2 };

		private static readonly string[] TwoStateNames = new string[2] { "ON", "OFF" };

		private static readonly int[] TwoStates = new int[2] { 0, 2 };

		internal static int SelectedNumber => selectedNumber[slot];

		internal static int SelectedSlot => slot;

		internal static void FocusSlot(int kind, int index)
		{
			FocusSlot(kind);
			selectedNumber[kind] = Math.Max(0, index);
		}

		internal static void SelectCreatedNumber(int kind, int index)
		{
			FocusSlot(kind, index);
			numberFlashUntil = Time.realtimeSinceStartup + 1.5f;
			WardrobeWidgets.CollapseAll();
		}

		internal static void ApplyPendingNumberChange(WardrobeSession current)
		{
			if (Event.current.type != EventType.Layout)
			{
				return;
			}
			if (pendingSelectionKind >= 0)
			{
				selectedNumber[pendingSelectionKind] = pendingSelectionIndex;
				pendingSelectionKind = -1;
				WardrobeWidgets.CollapseAll();
			}
			if (pendingNumberAction != null)
			{
				Action action = pendingNumberAction;
				bool num = current == pendingSession;
				pendingNumberAction = null;
				pendingSession = null;
				if (num)
				{
					action();
				}
			}
		}

		internal static void RequestAdd(WardrobeSession s, int kind)
		{
			pendingSession = s;
			pendingNumberAction = delegate
			{
				int num = s.AddClothingSlot(kind);
				if (num > 0)
				{
					SelectCreatedNumber(kind, num);
				}
			};
		}

		internal static void DrawNumberActions(WardrobeSession s, int kind, int index, int count)
		{
			bool flag = count < 5;
			bool enabled = GUI.enabled;
			GUI.enabled = enabled && (index > 0 || flag);
			string arg = WardrobeLang.T(WardrobeSession.ClothesSlotNames[kind]);
			string text = ((index == 0) ? string.Format(WardrobeLang.T("Add another {0}"), arg) : string.Format(WardrobeLang.T("Remove {0} {1}"), arg, index + 1));
			if (WardrobeWidgets.FillButtonRow("", 38f, new string[1] { text }) == 0)
			{
				int k = kind;
				int n = index;
				if (n == 0)
				{
					RequestAdd(s, k);
				}
				else
				{
					pendingSession = s;
					pendingNumberAction = delegate
					{
						s.RemoveClothingSlot(k, n);
						selectedNumber[k] = Mathf.Clamp(n - 1, 0, s.GetClothingSlotCount(k) - 1);
						WardrobeWidgets.CollapseAll();
					};
				}
			}
			GUI.enabled = enabled;
		}

		private static bool DrawNumberedSelection(WardrobeSession s)
		{
			if (Event.current.type == EventType.Layout)
			{
				for (int i = 0; i < layoutNumberCounts.Length; i++)
				{
					layoutNumberCounts[i] = Mathf.Clamp(s.GetClothingSlotCount(i), 1, 5);
				}
			}
			int num = layoutNumberCounts[slot];
			if (num > 1)
			{
				selectedNumber[slot] = Mathf.Clamp(selectedNumber[slot], 0, num - 1);
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				for (int j = 0; j < 5; j++)
				{
					bool enabled = GUI.enabled;
					GUI.enabled = enabled && j < num;
					if (GUILayout.Button((j + 1).ToString(), GUILayout.Width(46f), GUILayout.Height(28f)))
					{
						pendingSelectionKind = slot;
						pendingSelectionIndex = j;
					}
					Rect lastRect = GUILayoutUtility.GetLastRect();
					if (j == selectedNumber[slot])
					{
						Color color = GUI.color;
						float num2 = Mathf.Max(0f, numberFlashUntil - Time.realtimeSinceStartup);
						GUI.color = ((num2 > 0f && num2 % 0.5f < 0.25f) ? new Color(1f, 0.86f, 0.32f, 0.9f) : new Color(0.35f, 0.9f, 1f, 0.75f));
						GUI.DrawTexture(new Rect(lastRect.x, lastRect.yMax - 3f, lastRect.width, 3f), Texture2D.whiteTexture);
						GUI.color = color;
					}
					GUI.enabled = enabled;
				}
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
				GUILayout.Space(4f);
			}
			else
			{
				selectedNumber[slot] = 0;
			}
			bool flag = selectedNumber[slot] == 0 && (s.IsManagedClothingSlot(slot, 0) || (slot == 0 && WardrobeCompositeTopKoi.TopType(s.cha.nowCoordinate.clothes.parts[0].id) != 0));
			if (selectedNumber[slot] > 0 || flag)
			{
				WardrobeExtraClothesTabKoi.Draw(s, slot, selectedNumber[slot]);
				return true;
			}
			return false;
		}

		internal static void FocusSlot(int kind)
		{
			slot = kind;
		}

		private static void RepaintClothes(ChaControl cha, int k)
		{
			WardrobeClothesTextureInputs.Prepare(cha, k);
			cha.ChangeCustomClothes(main: true, k, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true, updateTex04: true);
		}

		internal static void Draw(WardrobeSession s)
		{
			ChaControl cha = s.cha;
			if (cha.sex == 0)
			{
				GUILayout.Label(WardrobeLang.T("Male characters are not supported (female coordinate slots only)."));
				return;
			}
			int num = GUILayout.SelectionGrid(slot, WardrobeEmoji.SlotLabels, 4);
			if (num != slot)
			{
				slot = num;
				WardrobeWidgets.CollapseAll();
			}
			GUILayout.Space(4f);
			if (DrawNumberedSelection(s))
			{
				return;
			}
			ChaFileClothes.PartsInfo partsInfo = cha.nowCoordinate.clothes.parts[slot];
			ChaClothesComponent customClothesComponent = cha.GetCustomClothesComponent(slot);
			int prefCategory = (int)WardrobeSession.ClothesCategories[slot];
			string key = "cloth_item" + slot;
			Func<List<WardrobeItem>> func = () => WardrobeKoiTopCatalog.List(slot);
			int currentToken = s.ClothingSelectionId(slot, 0);
			Func<List<WardrobeItem>> func2 = () => WardrobeKoiTopCatalog.HeaderList(slot, currentToken);
			Func<List<WardrobeItem>> listFunc = ((slot == 0 && currentToken > 0 && WardrobeCompositeTopKoi.TopType(currentToken) != 0) ? func2 : func);
			WardrobeWidgets.BeginZone(null);
			GUILayout.BeginHorizontal();
			GUILayout.BeginVertical(GUILayout.Width(96f));
			WardrobeWidgets.CurrentThumbBox(key, currentToken, listFunc, prefCategory);
			if (WardrobeWidgets.MiniButton(WardrobeEmoji.C("dice", " " + WardrobeLang.T("Randomize")), 96f))
			{
				s.PushUndo();
				s.RandomizeClothingItem(slot);
			}
			GUILayout.EndVertical();
			GUILayout.Space(4f);
			GUILayout.BeginVertical();
			WardrobeWidgets.SelectorHeader("Item", key, currentToken, listFunc, 38f);
			int num2;
			string[] array;
			if (customClothesComponent != null)
			{
				if (slot != 4 && slot != 6 && slot != 7)
				{
					num2 = ((slot != 8) ? 1 : 0);
					if (num2 != 0)
					{
						array = ThreeStateNames;
						goto IL_023b;
					}
				}
				else
				{
					num2 = 0;
				}
				array = TwoStateNames;
				goto IL_023b;
			}
			GUILayout.Label(WardrobeLang.T("(empty slot — pick an item to edit its colors)"));
			goto IL_044f;
			IL_044f:
			DrawNumberActions(s, slot, 0, s.GetClothingSlotCount(slot));
			GUILayout.EndVertical();
			GUILayout.EndHorizontal();
			WardrobeWidgets.EndZone();
			WardrobeWidgets.SelectorBody(s, key, currentToken, delegate(int id)
			{
				s.PushUndo();
				s.SetClothingSlotItem(slot, 0, id);
			}, thumbs: true, prefCategory);
			if (customClothesComponent == null)
			{
				return;
			}
			if (customClothesComponent.useColorA01 || customClothesComponent.useColorN01)
			{
				DrawColorZone(s, cha, 0);
			}
			if (customClothesComponent.useColorA02 || customClothesComponent.useColorN02)
			{
				DrawColorZone(s, cha, 1);
			}
			if (customClothesComponent.useColorA03 || customClothesComponent.useColorN03)
			{
				DrawColorZone(s, cha, 2);
			}
			WardrobeShaderColor4TabKoi.DrawClothing(s, slot, partsInfo.id);
			bool flag = customClothesComponent.objOpt01 != null && customClothesComponent.objOpt01.Length != 0;
			bool flag2 = customClothesComponent.objOpt02 != null && customClothesComponent.objOpt02.Length != 0;
			if (flag || flag2)
			{
				WardrobeWidgets.BeginZone("Options");
				GUILayout.BeginHorizontal();
				if (flag)
				{
					WardrobeWidgets.ToggleRow(s, "Option Parts 1", "cloth_opt1" + slot, () => !cha.nowCoordinate.clothes.parts[slot].hideOpt[0], delegate(bool en)
					{
						SetOption(cha, slot, 0, en);
					});
				}
				if (flag2)
				{
					WardrobeWidgets.ToggleRow(s, "Option Parts 2", "cloth_opt2" + slot, () => !cha.nowCoordinate.clothes.parts[slot].hideOpt[1], delegate(bool en)
					{
						SetOption(cha, slot, 1, en);
					});
				}
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
				WardrobeWidgets.EndZone();
			}
			DrawOverlayZone(s, cha, slot);
			return;
			IL_023b:
			string[] array2 = array;
			int[] array3 = ((num2 != 0) ? ThreeStates : TwoStates);
			int clothingSlotState = s.GetClothingSlotState(slot, 0);
			string[] array4 = new string[array2.Length + 1];
			Array.Copy(array2, array4, array2.Length);
			array4[array2.Length] = "Remove";
			int num3 = WardrobeWidgets.FillButtonRow("State", 38f, array4, Array.IndexOf(array3, clothingSlotState));
			if (num3 >= 0)
			{
				if (num3 < array2.Length)
				{
					if (array3[num3] != clothingSlotState)
					{
						s.PushUndo();
						s.SetClothingSlotState(slot, 0, Convert.ToByte(array3[num3]));
					}
				}
				else
				{
					s.PushUndo();
					s.ClearClothesSlot(slot);
				}
			}
			switch (WardrobeWidgets.FillButtonRow("Color", 38f, new string[3] { "Restore Colors", "Copy Colors", "Paste Colors" }, -1, 0f, new bool[3]
			{
				true,
				true,
				WardrobeColorClipboard.HasData
			}))
			{
			case 0:
				s.PushUndo();
				RestoreDefaultColors(cha, slot, -1);
				s.MarkClothesCustom(slot, colors: true, itemToo: false);
				break;
			case 1:
				WardrobeColorClipboard.CopyFromClothes(cha, slot, cha.nowCoordinate.clothes.parts[slot]);
				break;
			case 2:
				s.PushUndo();
				WardrobeColorClipboard.PasteToClothes(cha, slot);
				s.MarkClothesCustom(slot, colors: true, itemToo: false);
				break;
			}
			if (WardrobeME.HasStudioME && WardrobeContext.InStudio)
			{
				float rowHeight = 0f;
				if (WardrobeWidgets.FillButtonRow("Mat", 38f, new string[1] { "Material Editor" }, -1, rowHeight) == 0 && !WardrobeME.OpenStudioClothes(cha, slot))
				{
					WardrobeGUI.SetMessage("Couldn't open Material Editor.");
				}
			}
			goto IL_044f;
		}

		private static void SetOption(ChaControl cha, int k, int idx, bool visible)
		{
			cha.nowCoordinate.clothes.parts[k].hideOpt[idx] = !visible;
			WardrobeCompat.FileCoord(cha).clothes.parts[k].hideOpt[idx] = !visible;
			ChaClothesComponent customClothesComponent = cha.GetCustomClothesComponent(k);
			GameObject[] array = ((idx != 0) ? customClothesComponent?.objOpt02 : customClothesComponent?.objOpt01);
			if (array == null)
			{
				return;
			}
			GameObject[] array2 = array;
			foreach (GameObject gameObject in array2)
			{
				if (gameObject != null)
				{
					gameObject.SetActive(visible);
				}
			}
		}

		private static void DrawColorZone(WardrobeSession s, ChaControl cha, int ci)
		{
			int k = slot;
			int c = ci;
			string text = "cloth" + k + "_z" + c;
			WardrobeWidgets.BeginZone($"Color {ci + 1}", delegate
			{
				if (WardrobeWidgets.MiniButton("Copy", 44f))
				{
					WardrobeChannelClipboard.CopyChannel(cha.nowCoordinate.clothes.parts[k], c);
				}
				bool enabled = GUI.enabled;
				GUI.enabled = enabled && WardrobeChannelClipboard.HasData;
				if (WardrobeWidgets.MiniButton("Paste", 44f))
				{
					s.PushUndo();
					WardrobeChannelClipboard.ApplyChannel(cha.nowCoordinate.clothes.parts[k], c);
					WardrobeChannelClipboard.ApplyChannel(WardrobeCompat.FileCoord(cha).clothes.parts[k], c);
					RepaintClothes(cha, k);
					s.MarkClothesCustom(k, colors: true, itemToo: false);
					WardrobePseudoMakerState.InvalidateClothingColorChannel(cha, cha.fileStatus.coordinateType, k, c, WardrobeChannelClipboard.HasClothingPattern);
				}
				GUI.enabled = enabled;
				if (WardrobeWidgets.MiniButton("Reset", 46f))
				{
					s.PushUndo();
					RestoreDefaultColors(cha, k, c);
					s.MarkClothesCustom(k, colors: true, itemToo: false);
				}
			});
			GUILayout.BeginHorizontal();
			WardrobeWidgets.SwatchColumn(s, null, text + "_color", () => cha.nowCoordinate.clothes.parts[k].colorInfo[c].baseColor, delegate(Color v)
			{
				cha.nowCoordinate.clothes.parts[k].colorInfo[c].baseColor = v;
				WardrobeCompat.FileCoord(cha).clothes.parts[k].colorInfo[c].baseColor = v;
				RepaintClothes(cha, k);
				s.MarkClothesCustom(k, colors: true, itemToo: false);
			});
			GUILayout.Space(4f);
			GUILayout.BeginVertical();
			WardrobeWidgets.SelectorHeader("Pattern", text + "_ptn", cha.nowCoordinate.clothes.parts[k].colorInfo[c].pattern, () => WardrobeCatalog.List(430), 52f);
			GUILayout.EndVertical();
			GUILayout.EndHorizontal();
			WardrobeWidgets.SelectorBody(s, text + "_ptn", cha.nowCoordinate.clothes.parts[k].colorInfo[c].pattern, delegate(int id)
			{
				s.PushUndo();
				cha.nowCoordinate.clothes.parts[k].colorInfo[c].pattern = id;
				WardrobeCompat.FileCoord(cha).clothes.parts[k].colorInfo[c].pattern = id;
				RepaintClothes(cha, k);
				s.MarkClothesCustom(k, colors: true, itemToo: false);
			}, thumbs: true, 430);
			if (cha.nowCoordinate.clothes.parts[k].colorInfo[c].pattern != 0)
			{
				WardrobeWidgets.SwatchColumn(s, "Pattern Color", text + "_ptncol", () => cha.nowCoordinate.clothes.parts[k].colorInfo[c].patternColor, delegate(Color v)
				{
					cha.nowCoordinate.clothes.parts[k].colorInfo[c].patternColor = v;
					WardrobeCompat.FileCoord(cha).clothes.parts[k].colorInfo[c].patternColor = v;
					RepaintClothes(cha, k);
					s.MarkClothesCustom(k, colors: true, itemToo: false);
				});
			}
			WardrobeWidgets.EndZone();
		}

		internal static void RestoreDefaultColors(ChaControl cha, int k, int colorIndex)
		{
			ChaClothesComponent customClothesComponent = cha.GetCustomClothesComponent(k);
			if (customClothesComponent == null)
			{
				return;
			}
			bool[] array = new bool[3]
			{
				customClothesComponent.useColorA01 || customClothesComponent.useColorN01,
				customClothesComponent.useColorA02 || customClothesComponent.useColorN02,
				customClothesComponent.useColorA03 || customClothesComponent.useColorN03
			};
			for (int i = 0; i < 3; i++)
			{
				if (array[i] && (colorIndex == -1 || colorIndex == i))
				{
					Color clothesDefaultColor = cha.GetClothesDefaultColor(k, i);
					ChaFileClothes.PartsInfo.ColorInfo[] array2 = new ChaFileClothes.PartsInfo.ColorInfo[2]
					{
						cha.nowCoordinate.clothes.parts[k].colorInfo[i],
						WardrobeCompat.FileCoord(cha).clothes.parts[k].colorInfo[i]
					};
					foreach (ChaFileClothes.PartsInfo.ColorInfo obj in array2)
					{
						obj.baseColor = clothesDefaultColor;
						obj.pattern = 0;
						obj.patternColor = Color.white;
					}
				}
			}
			RepaintClothes(cha, k);
		}

		private static void DrawOverlayZone(WardrobeSession s, ChaControl cha, int k)
		{
			if (!WardrobeOverlay.Available(cha))
			{
				return;
			}
			WardrobeWidgets.BeginZone("Overlay");
			GUILayout.BeginHorizontal();
			Texture2D overlay = WardrobeOverlay.GetOverlay(cha, k);
			Rect rect = GUILayoutUtility.GetRect(72f, 72f, GUILayout.Width(72f), GUILayout.Height(72f));
			GUI.Box(rect, (overlay != null) ? GUIContent.none : new GUIContent(WardrobeLang.T("None")));
			if (overlay != null)
			{
				GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), overlay, ScaleMode.ScaleToFit);
			}
			GUILayout.Space(6f);
			GUILayout.BeginVertical();
			bool enabled = WardrobeOverlay.HasOverlay(cha, k);
			bool enabled2 = GUI.enabled;
			GUILayout.BeginHorizontal();
			if (GUILayout.Button(WardrobeLang.T("Load")))
			{
				int kk = k;
				WardrobeCardBrowser.OpenImage(WardrobeOverlay.OverlayDir, delegate(string path)
				{
					try
					{
						s.PushUndo();
						WardrobeOverlay.SetOverlay(cha, kk, File.ReadAllBytes(path));
						WardrobeGUI.SetMessage(WardrobeLang.T("Overlay loaded."));
					}
					catch (Exception)
					{
						WardrobeGUI.SetMessage(WardrobeLang.T("Could not load image."));
					}
				});
			}
			GUI.enabled = enabled;
			if (GUILayout.Button(WardrobeLang.T("Clear")))
			{
				s.PushUndo();
				WardrobeOverlay.SetOverlay(cha, k, null);
			}
			GUI.enabled = enabled2;
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			GUI.enabled = enabled;
			if (GUILayout.Button(WardrobeLang.T("Export")))
			{
				WardrobeGUI.SetMessage((WardrobeOverlay.ExportOverlay(cha, k) != null) ? WardrobeLang.T("Saved to UserData\\overlays.") : WardrobeLang.T("Nothing to export."));
			}
			GUI.enabled = enabled2;
			if (GUILayout.Button(WardrobeLang.T("Dump original")))
			{
				WardrobeOverlay.DumpOriginal(cha, k);
			}
			GUILayout.EndHorizontal();
			WardrobeWidgets.ToggleRow(s, "Hide base texture", "cloth_ovhide" + k, () => WardrobeOverlay.GetHideBase(cha, k), delegate(bool en)
			{
				s.PushUndo();
				WardrobeOverlay.SetHideBase(cha, k, en);
			});
			GUILayout.EndVertical();
			GUILayout.EndHorizontal();
			WardrobeWidgets.EndZone();
		}
	}
	internal static class WardrobeClothesTextureInputs
	{
		private static readonly MethodInfo Init = typeof(ChaControl).GetMethod("InitBaseCustomTextureClothes", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[2]
		{
			typeof(bool),
			typeof(int)
		}, null);

		internal static void Prepare(ChaControl cha, int part)
		{
			Prepare(cha, main: true, part);
		}

		internal static void Prepare(ChaControl cha, bool main, int part)
		{
			if (cha == null || Init == null || part < 0)
			{
				return;
			}
			CustomTextureCreate[,] array = null;
			ListInfoBase[] array2 = (main ? cha.infoClothes : cha.infoParts);
			GameObject[] array3 = (main ? cha.objClothes : cha.objParts);
			if (array2 == null || array3 == null || part >= array2.Length || part >= array3.Length || array2[part] == null || array3[part] == null)
			{
				return;
			}
			CustomTextureCreate[,] array4 = (main ? cha.ctCreateClothes : cha.ctCreateClothesSub);
			if (!ValidRow(array4, part) || (array != null && !ValidRow(array, part)) || (array4[part, 0] != null && (array == null || array[part, 0] != null)))
			{
				return;
			}
			CustomTextureCreate[] prior = SaveRow(array4, part);
			CustomTextureCreate[] prior2 = ((array == null) ? null : SaveRow(array, part));
			bool initialized = false;
			try
			{
				initialized = (bool)Init.Invoke(cha, new object[2] { main, part });
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("Clothing colour inputs unavailable for " + (main ? "slot " : "top piece ") + part + ": " + (ex.InnerException ?? ex).Message));
			}
			finally
			{
				RestoreExisting(array4, part, prior, initialized);
				if (array != null)
				{
					RestoreExisting(array, part, prior2, initialized);
				}
			}
		}

		internal static bool CanPrepare(ChaControl cha, int part)
		{
			if (cha == null || part < 0 || cha.infoClothes == null || cha.objClothes == null || part >= cha.infoClothes.Length || part >= cha.objClothes.Length || cha.infoClothes[part] == null || cha.objClothes[part] == null)
			{
				return false;
			}
			if (ValidRow(cha.ctCreateClothes, part) && cha.ctCreateClothes[part, 0] != null)
			{
				return true;
			}
			ListInfoBase obj = cha.infoClothes[part];
			string info = obj.GetInfo(ChaListDefine.KeyType.MainTex);
			string info2 = obj.GetInfo(ChaListDefine.KeyType.ColorMaskTex);
			if (!string.IsNullOrEmpty(info) && info != "0" && !string.IsNullOrEmpty(info2))
			{
				return info2 != "0";
			}
			return false;
		}

		private static bool ValidRow(CustomTextureCreate[,] row, int part)
		{
			if (row != null && part >= 0 && part < row.GetLength(0))
			{
				return row.GetLength(1) == 3;
			}
			return false;
		}

		private static CustomTextureCreate[] SaveRow(CustomTextureCreate[,] row, int part)
		{
			return new CustomTextureCreate[3]
			{
				row[part, 0],
				row[part, 1],
				row[part, 2]
			};
		}

		private static void RestoreExisting(CustomTextureCreate[,] row, int part, CustomTextureCreate[] prior, bool initialized)
		{
			for (int i = 0; i < 3; i++)
			{
				if (prior[i] != null || !initialized)
				{
					CustomTextureCreate customTextureCreate = row[part, i];
					row[part, i] = prior[i];
					if (customTextureCreate != null && customTextureCreate != prior[i])
					{
						customTextureCreate.Release();
					}
				}
			}
		}
	}
	internal static class KoiClothingCoordinateMap
	{
		private const int Magic = 1129068102;

		private const int Version = 1;

		internal static bool UseSavedEntry(bool callbackIsLive, int incoming, int loaded)
		{
			if (callbackIsLive)
			{
				return incoming != loaded;
			}
			return false;
		}

		internal static byte[] Encode(ClothingSlotSet[] coordinates)
		{
			if (coordinates == null || coordinates.Length > 32)
			{
				throw new InvalidDataException("Invalid clothing coordinate count.");
			}
			using MemoryStream memoryStream = new MemoryStream();
			using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
			binaryWriter.Write(1129068102);
			binaryWriter.Write(1);
			binaryWriter.Write(coordinates.Length);
			for (int i = 0; i < coordinates.Length; i++)
			{
				byte[] array = (coordinates[i] ?? new ClothingSlotSet()).Encode(prune: true);
				binaryWriter.Write(array.Length);
				binaryWriter.Write(array);
			}
			return memoryStream.ToArray();
		}

		internal static ClothingSlotSet[] Decode(byte[] bytes, int expectedCount, Func<byte[], ClothingSlotSet> validate)
		{
			if (bytes == null || expectedCount < 0 || expectedCount > 32)
			{
				throw new InvalidDataException("Invalid clothing coordinate map.");
			}
			using MemoryStream memoryStream = new MemoryStream(bytes, writable: false);
			using BinaryReader binaryReader = new BinaryReader(memoryStream);
			if (binaryReader.ReadInt32() != 1129068102 || binaryReader.ReadInt32() != 1)
			{
				throw new InvalidDataException("Unknown clothing coordinate map.");
			}
			int num = binaryReader.ReadInt32();
			if (num != expectedCount)
			{
				throw new InvalidDataException("Clothing coordinate count changed.");
			}
			ClothingSlotSet[] array = new ClothingSlotSet[num];
			for (int i = 0; i < num; i++)
			{
				int num2 = binaryReader.ReadInt32();
				if (num2 < 0 || num2 > 134217728 || num2 > memoryStream.Length - memoryStream.Position)
				{
					throw new InvalidDataException("Invalid clothing coordinate payload.");
				}
				array[i] = validate(binaryReader.ReadBytes(num2));
			}
			if (memoryStream.Position != memoryStream.Length)
			{
				throw new InvalidDataException("Trailing clothing coordinates.");
			}
			return array;
		}
	}
	internal static class WardrobeClothingGeometry
	{
		private sealed class Shape
		{
			internal int Slot;

			internal int Vertices;

			internal string Garment;

			internal string Path;

			internal string SourcePath;

			internal string Name;

			internal string DeltaVertices;

			internal string DeltaNormals;

			internal string DeltaTangents;

			internal float Weight;
		}

		private sealed class PreviousShape
		{
			internal object Definition;

			internal Mesh Mesh;
		}

		private const string BscId = "com.shallty.blendshapecreator";

		private const int Magic = 1195587142;

		private static Type controllerType;

		private static Type dataType;

		private static Type shapeType;

		private static int nextTicket;

		private static readonly Dictionary<ChaControl, int[]> tickets = new Dictionary<ChaControl, int[]>();

		private static FieldInfo cardField;

		private static FieldInfo listField;

		private static FieldInfo sceneField;

		private static bool Ready()
		{
			if (shapeType != null)
			{
				return true;
			}
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach (Assembly assembly in assemblies)
			{
				if (assembly.GetName().Name.IndexOf("BlendshapeCreator", StringComparison.OrdinalIgnoreCase) < 0)
				{
					continue;
				}
				Type type = assembly.GetType("BlendshapeCreator.BlendshapeCreator+BlendShape");
				Type type2 = assembly.GetType("BlendshapeCreator.CharacterController");
				Type type3 = assembly.GetType("BlendshapeCreator.BlendshapeCreator+OCIBlendShapeData");
				Type type4 = assembly.GetType("BlendshapeCreator.BlendshapeCreator");
				if (type == null || type2 == null || type3 == null || type4 == null)
				{
					continue;
				}
				FieldInfo field = type2.GetField("CharaBlendShapesData");
				FieldInfo field2 = type3.GetField("blendShapes");
				FieldInfo field3 = type4.GetField("ociBlendShapesData", BindingFlags.Static | BindingFlags.Public);
				if (field == null || field2 == null || field3 == null || type.GetMethod("LoadBlendShape", new Type[2]
				{
					typeof(Transform),
					typeof(bool)
				}) == null)
				{
					continue;
				}
				string[] array = new string[6] { "name", "rendererPath", "deltaVertices", "deltaNormals", "deltaTangents", "weight" };
				foreach (string name in array)
				{
					if (type.GetField(name) == null)
					{
						return false;
					}
				}
				controllerType = type2;
				dataType = type3;
				cardField = field;
				listField = field2;
				sceneField = field3;
				shapeType = type;
				return true;
			}
			return false;
		}

		internal static byte[] Capture(ChaControl character)
		{
			if (character == null || !Ready())
			{
				return null;
			}
			try
			{
				List<Shape> records = new List<Shape>();
				HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
				Collect(character, SceneData(character, create: false), records, keys);
				object component = character.gameObject.GetComponent(controllerType);
				if (component != null)
				{
					Collect(character, cardField.GetValue(component), records, keys);
				}
				return Encode(records);
			}
			catch (Exception error)
			{
				Warn("capture", error);
				return null;
			}
		}

		private static void Collect(ChaControl character, object data, List<Shape> records, HashSet<string> keys)
		{
			IList list = ((data == null) ? null : (listField.GetValue(data) as IList));
			if (list == null)
			{
				return;
			}
			foreach (object item in list)
			{
				if (item == null)
				{
					continue;
				}
				Shape shape = ReadShape(item);
				if (string.IsNullOrEmpty(shape.Path) || string.IsNullOrEmpty(shape.Name))
				{
					continue;
				}
				Transform transform = character.transform.Find(shape.Path);
				int num = ClothingSlot(character, transform);
				if (num < 0)
				{
					continue;
				}
				SkinnedMeshRenderer component = transform.GetComponent<SkinnedMeshRenderer>();
				if (component == null || component.sharedMesh == null)
				{
					continue;
				}
				int blendShapeIndex = component.sharedMesh.GetBlendShapeIndex(shape.Name);
				if (blendShapeIndex >= 0 && keys.Add(shape.Path + "\n" + shape.Name))
				{
					shape.Slot = num;
					shape.Garment = Garment(character, num);
					if (shape.Garment != null)
					{
						shape.Vertices = component.sharedMesh.vertexCount;
						shape.Path = RelativePath(character.objClothes[num].transform, transform);
						shape.Weight = component.GetBlendShapeWeight(blendShapeIndex);
						records.Add(shape);
					}
				}
			}
		}

		internal static void Apply(ChaControl character, byte[] bytes, int slot = -1)
		{
			if (character == null || !Ready())
			{
				return;
			}
			CancelPending(character, slot);
			List<Shape> records;
			try
			{
				records = ((bytes == null) ? new List<Shape>() : Decode(bytes));
			}
			catch (Exception error)
			{
				Warn("payload validation", error);
				return;
			}
			if (!tickets.TryGetValue(character, out var value))
			{
				value = new int[(character.objClothes == null) ? 16 : character.objClothes.Length];
				tickets[character] = value;
			}
			int[] array = new int[value.Length];
			for (int i = 0; i < value.Length; i++)
			{
				if (slot < 0 || slot == i)
				{
					array[i] = (value[i] = ++nextTicket);
				}
			}
			Dictionary<string, PreviousShape> previous = ClearRegistered(character, slot);
			try
			{
				character.StartCoroutine(Restore(character, records, array, previous));
			}
			catch (Exception error2)
			{
				Warn("restore scheduling", error2);
			}
		}

		private static void CancelPending(ChaControl character, int slot)
		{
			if (!tickets.TryGetValue(character, out var value))
			{
				return;
			}
			bool flag = false;
			for (int i = 0; i < value.Length; i++)
			{
				if (slot < 0 || slot == i)
				{
					value[i] = 0;
				}
				if (value[i] != 0)
				{
					flag = true;
				}
			}
			if (!flag)
			{
				tickets.Remove(character);
			}
		}

		private static IEnumerator Restore(ChaControl character, List<Shape> records, int[] own, Dictionary<string, PreviousShape> previous)
		{
			Dictionary<string, Mesh> applied = new Dictionary<string, Mesh>(StringComparer.Ordinal);
			try
			{
				for (int retry = 0; retry < 20; retry++)
				{
					if (!(character != null))
					{
						break;
					}
					if (!tickets.TryGetValue(character, out var value))
					{
						break;
					}
					List<Shape> list = new List<Shape>();
					foreach (Shape record in records)
					{
						if (record.Slot >= 0 && record.Slot < value.Length && own[record.Slot] != 0 && value[record.Slot] == own[record.Slot])
						{
							list.Add(record);
						}
					}
					if (list.Count == 0)
					{
						break;
					}
					RestoreNow(character, list, applied, previous);
					yield return new WaitForSeconds(0.25f);
				}
			}
			finally
			{
				if (tickets.TryGetValue(character, out var value2))
				{
					bool flag = false;
					for (int value = 0; value < value2.Length; value++)
					{
						if (own[value] != 0 && value2[value] == own[value])
						{
							value2[value] = 0;
						}
						if (value2[value] != 0)
						{
							flag = true;
						}
					}
					if (!flag || character == null)
					{
						tickets.Remove(character);
					}
				}
			}
		}

		private static void RestoreNow(ChaControl character, List<Shape> records, Dictionary<string, Mesh> applied, Dictionary<string, PreviousShape> oldShapes)
		{
			try
			{
				foreach (Shape record in records)
				{
					Transform transform = null;
					int num = record.Slot;
					if (num >= 0)
					{
						if (character.objClothes == null || num >= character.objClothes.Length || character.objClothes[num] == null || Garment(character, num) != record.Garment)
						{
							continue;
						}
						Transform transform2 = character.objClothes[num].transform;
						transform = (string.IsNullOrEmpty(record.Path) ? transform2 : transform2.Find(record.Path));
					}
					else
					{
						if (string.IsNullOrEmpty(record.Path))
						{
							continue;
						}
						transform = character.transform.Find(record.Path);
						num = ClothingSlot(character, transform);
						if (num < 0)
						{
							continue;
						}
					}
					if (transform == null || ClothingSlot(character, transform) != num)
					{
						continue;
					}
					SkinnedMeshRenderer component = transform.GetComponent<SkinnedMeshRenderer>();
					if (component == null || component.sharedMesh == null || (record.Vertices > 0 && component.sharedMesh.vertexCount != record.Vertices))
					{
						continue;
					}
					string text = RelativePath(character.transform, transform);
					string key = num + "\n" + text + "\n" + record.Name;
					if (applied.TryGetValue(key, out var value) && value == component.sharedMesh)
					{
						continue;
					}
					object obj = Activator.CreateInstance(shapeType, text, record.Name, record.DeltaVertices, record.DeltaNormals, record.DeltaTangents, record.Weight);
					Mesh sharedMesh = component.sharedMesh;
					if (component.sharedMesh.GetBlendShapeIndex(record.Name) >= 0 && oldShapes.TryGetValue(text + "\n" + record.Name, out var value2) && value2.Mesh == component.sharedMesh && (Text(value2.Definition, "deltaVertices") != record.DeltaVertices || (Text(value2.Definition, "deltaNormals") ?? "") != (record.DeltaNormals ?? "") || (Text(value2.Definition, "deltaTangents") ?? "") != (record.DeltaTangents ?? "")))
					{
						MethodInfo method = shapeType.GetMethod("DeleteBlendShape", new Type[1] { typeof(Transform) });
						if (method == null)
						{
							continue;
						}
						method.Invoke(value2.Definition, new object[1] { character.transform });
					}
					shapeType.GetMethod("LoadBlendShape", new Type[2]
					{
						typeof(Transform),
						typeof(bool)
					}).Invoke(obj, new object[2] { character.transform, true });
					foreach (string item in new List<string>(applied.Keys))
					{
						if (item.StartsWith(num + "\n" + text + "\n", StringComparison.Ordinal) && applied[item] == sharedMesh)
						{
							applied[item] = component.sharedMesh;
						}
					}
					foreach (PreviousShape value3 in oldShapes.Values)
					{
						if (value3.Mesh == sharedMesh)
						{
							value3.Mesh = component.sharedMesh;
						}
					}
					int blendShapeIndex = component.sharedMesh.GetBlendShapeIndex(record.Name);
					if (blendShapeIndex < 0)
					{
						continue;
					}
					component.SetBlendShapeWeight(blendShapeIndex, record.Weight);
					object component2 = character.gameObject.GetComponent(controllerType);
					if (component2 != null)
					{
						object obj2 = cardField.GetValue(component2);
						if (obj2 == null)
						{
							obj2 = Activator.CreateInstance(dataType);
							cardField.SetValue(component2, obj2);
						}
						Register(obj2, obj);
					}
					Register(SceneData(character, create: true), obj);
					applied[key] = component.sharedMesh;
				}
			}
			catch (Exception error)
			{
				Warn("restore", error);
			}
		}

		internal static byte[] FromCharacter(ChaFileControl character, int coordinate, byte[] normalizedWeights)
		{
			if (character == null || !Ready())
			{
				return null;
			}
			try
			{
				PluginData extendedDataById = ExtendedSave.GetExtendedDataById((ChaFile)character, "com.shallty.blendshapecreator");
				if (extendedDataById == null || !extendedDataById.data.TryGetValue("CharaBlendShapesData", out var value) || !(value is byte[]))
				{
					return null;
				}
				MethodInfo methodInfo = null;
				MethodInfo[] methods = typeof(MessagePackSerializer).GetMethods(BindingFlags.Static | BindingFlags.Public);
				foreach (MethodInfo methodInfo2 in methods)
				{
					if (methodInfo2.Name == "Deserialize" && methodInfo2.IsGenericMethodDefinition && methodInfo2.GetParameters().Length == 1 && methodInfo2.GetParameters()[0].ParameterType == typeof(byte[]))
					{
						methodInfo = methodInfo2.MakeGenericMethod(dataType);
						break;
					}
				}
				if (methodInfo == null)
				{
					return null;
				}
				object obj = methodInfo.Invoke(null, new object[1] { value });
				IList list = ((obj == null) ? null : (listField.GetValue(obj) as IList));
				if (list == null)
				{
					return null;
				}
				if (normalizedWeights == null)
				{
					return null;
				}
				List<Shape> list2 = ReadWeights(normalizedWeights);
				List<Shape> list3 = new List<Shape>();
				foreach (object item in list)
				{
					if (item == null)
					{
						continue;
					}
					Shape shape = ReadShape(item);
					Shape shape2 = null;
					bool flag = false;
					foreach (Shape item2 in list2)
					{
						if (SourceMatches(item2, shape))
						{
							if (shape2 != null)
							{
								flag = true;
								break;
							}
							shape2 = item2;
						}
					}
					if (!(shape2 == null || flag) && !string.IsNullOrEmpty(shape2.Garment))
					{
						shape.Slot = shape2.Slot;
						shape.Path = shape2.Path;
						shape.Garment = shape2.Garment;
						shape.Vertices = shape2.Vertices;
						shape.Weight = shape2.Weight;
						list3.Add(shape);
					}
				}
				return Encode(list3);
			}
			catch (Exception error)
			{
				Warn("character import", error);
				return null;
			}
		}

		private static List<Shape> ReadWeights(byte[] bytes)
		{
			if (bytes.Length > 16777216)
			{
				throw new InvalidDataException("Clothing weights too large.");
			}
			List<Shape> list = new List<Shape>();
			using BinaryReader binaryReader = new BinaryReader(new MemoryStream(bytes, writable: false));
			int num = binaryReader.ReadInt32();
			if (num != 2 && num != 3)
			{
				throw new InvalidDataException("Unsupported clothing weights format.");
			}
			int num2 = Count(binaryReader, 64);
			for (int i = 0; i < num2; i++)
			{
				int num3 = binaryReader.ReadInt32();
				int num4 = Count(binaryReader, 4096);
				for (int j = 0; j < num4; j++)
				{
					int num5 = binaryReader.ReadInt32();
					string path = binaryReader.ReadString();
					int num6 = binaryReader.ReadInt32();
					string garment = binaryReader.ReadString();
					string sourcePath = ((num >= 3) ? binaryReader.ReadString() : null);
					int num7 = Count(binaryReader, 16384);
					for (int k = 0; k < num7; k++)
					{
						string name = binaryReader.ReadString();
						float num8 = binaryReader.ReadSingle();
						if (num3 == 0 && num5 >= 0 && num5 < 64 && num6 > 0 && !float.IsNaN(num8) && !float.IsInfinity(num8))
						{
							list.Add(new Shape
							{
								Slot = num5,
								Path = path,
								Vertices = num6,
								Garment = garment,
								SourcePath = sourcePath,
								Name = name,
								Weight = num8
							});
						}
					}
				}
			}
			return list;
		}

		private static bool SourceMatches(Shape sample, Shape record)
		{
			if (!string.IsNullOrEmpty(sample.SourcePath) && string.Equals(sample.SourcePath, record.Path, StringComparison.Ordinal))
			{
				return string.Equals(sample.Name, record.Name, StringComparison.Ordinal);
			}
			return false;
		}

		private static int Count(BinaryReader reader, int maximum)
		{
			int num = reader.ReadInt32();
			if (num < 0 || num > maximum)
			{
				throw new InvalidDataException("Invalid clothing record count.");
			}
			return num;
		}

		private static Shape ReadShape(object shape)
		{
			return new Shape
			{
				Slot = -1,
				Garment = null,
				Path = Text(shape, "rendererPath"),
				Name = Text(shape, "name"),
				DeltaVertices = Text(shape, "deltaVertices"),
				DeltaNormals = Text(shape, "deltaNormals"),
				DeltaTangents = Text(shape, "deltaTangents"),
				Weight = Convert.ToSingle(shapeType.GetField("weight").GetValue(shape))
			};
		}

		private static string Text(object shape, string field)
		{
			return shapeType.GetField(field).GetValue(shape) as string;
		}

		private static string Garment(ChaControl character, int slot)
		{
			if (character.infoClothes == null || slot < 0 || slot >= character.infoClothes.Length || character.infoClothes[slot] == null)
			{
				return null;
			}
			ListInfoBase listInfoBase = character.infoClothes[slot];
			string info = listInfoBase.GetInfo(ChaListDefine.KeyType.MainAB);
			string info2 = listInfoBase.GetInfo(ChaListDefine.KeyType.MainData);
			if (string.IsNullOrEmpty(info) || string.IsNullOrEmpty(info2))
			{
				return null;
			}
			string text = "";
			return listInfoBase.Category + ":" + info.Length + ":" + info + info2.Length + ":" + info2 + text.Length + ":" + text;
		}

		private static int ClothingSlot(ChaControl character, Transform target)
		{
			if (target == null || character.objClothes == null)
			{
				return -1;
			}
			for (int i = 0; i < character.objClothes.Length; i++)
			{
				GameObject gameObject = character.objClothes[i];
				if (gameObject != null && (gameObject.transform == target || target.IsChildOf(gameObject.transform)))
				{
					return i;
				}
			}
			return -1;
		}

		private static string RelativePath(Transform root, Transform target)
		{
			string text = "";
			Transform transform = target;
			while (transform != null && transform != root)
			{
				text = ((text.Length == 0) ? transform.name : (transform.name + "/" + text));
				transform = transform.parent;
			}
			return text;
		}

		private static object SceneData(ChaControl character, bool create)
		{
			if (!StudioAPI.InsideStudio || Singleton<global::Studio.Studio>.Instance == null)
			{
				return null;
			}
			if (!(sceneField.GetValue(null) is IDictionary dictionary))
			{
				return null;
			}
			foreach (ObjectCtrlInfo value in Singleton<global::Studio.Studio>.Instance.dicObjectCtrl.Values)
			{
				if (value is OCIChar oCIChar && !(oCIChar.charInfo != character))
				{
					if (dictionary.Contains(oCIChar))
					{
						return dictionary[oCIChar];
					}
					if (!create)
					{
						return null;
					}
					return dictionary[oCIChar] = Activator.CreateInstance(dataType);
				}
			}
			return null;
		}

		private static Dictionary<string, PreviousShape> ClearRegistered(ChaControl character, int slot)
		{
			Dictionary<string, PreviousShape> dictionary = new Dictionary<string, PreviousShape>(StringComparer.Ordinal);
			try
			{
				object component = character.gameObject.GetComponent(controllerType);
				ClearList(character, (component == null) ? null : cardField.GetValue(component), slot, dictionary);
				ClearList(character, SceneData(character, create: false), slot, dictionary);
			}
			catch (Exception error)
			{
				Warn("previous clothing state cleanup", error);
			}
			return dictionary;
		}

		private static void ClearList(ChaControl character, object data, int slot, Dictionary<string, PreviousShape> previous)
		{
			IList list = ((data == null) ? null : (listField.GetValue(data) as IList));
			if (list == null)
			{
				return;
			}
			for (int num = list.Count - 1; num >= 0; num--)
			{
				if (list[num] != null)
				{
					string text = Text(list[num], "rendererPath");
					int num2 = (string.IsNullOrEmpty(text) ? (-1) : ClothingSlot(character, character.transform.Find(text)));
					if (num2 >= 0 && (slot < 0 || num2 == slot))
					{
						SkinnedMeshRenderer component = character.transform.Find(text).GetComponent<SkinnedMeshRenderer>();
						if (component != null && component.sharedMesh != null)
						{
							previous[text + "\n" + Text(list[num], "name")] = new PreviousShape
							{
								Definition = list[num],
								Mesh = component.sharedMesh
							};
						}
						list.RemoveAt(num);
					}
				}
			}
		}

		private static void Register(object data, object shape)
		{
			IList list = ((data == null) ? null : (listField.GetValue(data) as IList));
			if (list == null)
			{
				return;
			}
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i] != null && Text(list[i], "rendererPath") == Text(shape, "rendererPath") && Text(list[i], "name") == Text(shape, "name"))
				{
					list[i] = shape;
					return;
				}
			}
			list.Add(shape);
		}

		private static byte[] Encode(List<Shape> records)
		{
			if (records.Count == 0)
			{
				return null;
			}
			using MemoryStream memoryStream = new MemoryStream();
			using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
			binaryWriter.Write(1195587142);
			binaryWriter.Write(records.Count);
			foreach (Shape record in records)
			{
				binaryWriter.Write(record.Slot);
				binaryWriter.Write(record.Garment ?? "");
				binaryWriter.Write(record.Vertices);
				binaryWriter.Write(record.Path ?? "");
				binaryWriter.Write(record.Name ?? "");
				binaryWriter.Write(record.DeltaVertices ?? "");
				binaryWriter.Write(record.DeltaNormals ?? "");
				binaryWriter.Write(record.DeltaTangents ?? "");
				binaryWriter.Write(record.Weight);
			}
			return memoryStream.ToArray();
		}

		private static List<Shape> Decode(byte[] bytes)
		{
			if (bytes.Length > 268435456)
			{
				throw new InvalidDataException("Clothing geometry too large.");
			}
			List<Shape> list = new List<Shape>();
			using MemoryStream memoryStream = new MemoryStream(bytes);
			using BinaryReader binaryReader = new BinaryReader(memoryStream);
			if (binaryReader.ReadInt32() != 1195587142)
			{
				throw new InvalidDataException("Unknown clothing geometry format.");
			}
			int num = binaryReader.ReadInt32();
			if (num < 0 || num > 8192)
			{
				throw new InvalidDataException("Invalid clothing geometry count.");
			}
			for (int i = 0; i < num; i++)
			{
				Shape shape = new Shape
				{
					Slot = binaryReader.ReadInt32(),
					Garment = binaryReader.ReadString(),
					Vertices = binaryReader.ReadInt32(),
					Path = binaryReader.ReadString(),
					Name = binaryReader.ReadString(),
					DeltaVertices = binaryReader.ReadString(),
					DeltaNormals = binaryReader.ReadString(),
					DeltaTangents = binaryReader.ReadString(),
					Weight = binaryReader.ReadSingle()
				};
				if (shape.Slot < 0 || shape.Slot >= 64 || shape.Vertices <= 0 || shape.Path.Length > 4096 || string.IsNullOrEmpty(shape.Name) || shape.Name.Length > 4096 || string.IsNullOrEmpty(shape.Garment) || shape.Garment.Length > 16384 || string.IsNullOrEmpty(shape.DeltaVertices) || float.IsNaN(shape.Weight) || float.IsInfinity(shape.Weight))
				{
					throw new InvalidDataException("Invalid clothing geometry record.");
				}
				list.Add(shape);
			}
			if (memoryStream.Position != memoryStream.Length)
			{
				throw new InvalidDataException("Unexpected clothing geometry data.");
			}
			return list;
		}

		private static void Warn(string operation, Exception error)
		{
			WardrobePlugin.Log.LogWarning((object)("Clothing blendshape geometry " + operation + " failed: " + error.GetBaseException().Message));
		}
	}
	internal static class WardrobeClothingMixSource
	{
		internal static ClothingSlotRecord Select(ClothingSlotSet source, int kind, int index, Func<ClothingSlotRecord> first)
		{
			if (kind < 0 || kind >= 9 || index < 1 || index > 4)
			{
				return null;
			}
			ClothingSlotRecord clothingSlotRecord = source?.Get(kind, index);
			if (clothingSlotRecord == null || clothingSlotRecord.ItemId == 0)
			{
				clothingSlotRecord = first?.Invoke();
			}
			if (clothingSlotRecord != null && clothingSlotRecord.ItemId != 0)
			{
				return clothingSlotRecord.Clone();
			}
			return null;
		}
	}
	internal sealed class ClothingSlotVisualKoi : IDisposable
	{
		internal sealed class Piece
		{
			internal GameObject Root;

			internal ChaClothesComponent Component;

			internal int Kind;

			internal readonly CustomTextureCreate[] Composed = new CustomTextureCreate[3];
		}

		private sealed class RestoredTexture
		{
			internal Material Material;

			internal string Property;

			internal Texture Original;

			internal Texture2D Created;
		}

		internal GameObject Root;

		internal ChaClothesComponent Component;

		internal ChaFileClothes.PartsInfo Part;

		internal ClothingSlotRecord Record;

		internal Texture BodyMask;

		internal Texture BraMask;

		internal int ExtraType;

		internal int StandaloneSubKind = -1;

		internal readonly List<Piece> Pieces = new List<Piece>();

		private bool supportsHalf = true;

		private readonly CustomTextureCreate[] composed = new CustomTextureCreate[3];

		private bool highPoly = true;

		private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

		private readonly List<RestoredTexture> restored = new List<RestoredTexture>();

		private readonly Dictionary<string, Texture> baseline = new Dictionary<string, Texture>();

		internal ChaClothesComponent[] Components
		{
			get
			{
				if (Pieces.Count != 0)
				{
					return (from x in Pieces
						select x.Component into x
						where x != null
						select x).ToArray();
				}
				if (!(Component == null))
				{
					return new ChaClothesComponent[1] { Component };
				}
				return new ChaClothesComponent[0];
			}
		}

		internal Renderer[] Renderers
		{
			get
			{
				if (!(Root == null))
				{
					return Root.GetComponentsInChildren<Renderer>(includeInactive: true);
				}
				return new Renderer[0];
			}
		}

		internal void AdoptEditedMaterials()
		{
			HashSet<Renderer> borrowed = WardrobeBorrowedBodyMaterialsKoi.Targets(Root);
			Renderer[] renderers = Renderers;
			for (int i = 0; i < renderers.Length; i++)
			{
				Material[] array = WardrobeBorrowedBodyMaterialsKoi.Materials(renderers[i], borrowed);
				foreach (Material material in array)
				{
					if (material != null && !owned.Contains(material))
					{
						owned.Add(material);
					}
				}
			}
		}

		internal void AddPiece(GameObject root, ChaClothesComponent component, int kind)
		{
			Pieces.Add(new Piece
			{
				Root = root,
				Component = component,
				Kind = kind
			});
		}

		internal void Initialize(bool nativePrimary = false, bool hiPoly = true)
		{
			if (Root == null)
			{
				return;
			}
			highPoly = hiPoly;
			HashSet<Renderer> hashSet = WardrobeBorrowedBodyMaterialsKoi.Targets(Root);
			Renderer[] renderers = Renderers;
			foreach (Renderer renderer in renderers)
			{
				Material[] array = WardrobeBorrowedBodyMaterialsKoi.Materials(renderer, hashSet);
				for (int j = 0; j < array.Length; j++)
				{
					Material material = array[j];
					if (!(material == null))
					{
						Material material2 = new Material(material);
						owned.Add(material2);
						array[j] = material2;
					}
				}
				if (!hashSet.Contains(renderer))
				{
					renderer.sharedMaterials = array;
				}
			}
			CaptureBaseline();
			if (nativePrimary)
			{
				return;
			}
			if (Pieces.Count == 0)
			{
				InitializeCompositors(Root, composed, StandaloneSubKind);
			}
			else
			{
				foreach (Piece piece in Pieces)
				{
					InitializeCompositors(piece.Root, piece.Composed, piece.Kind);
				}
			}
			Repaint();
			ApplyState();
		}

		private static Texture2D LoadTexture(ListInfoBase info, ChaListDefine.KeyType bundleKey, ChaListDefine.KeyType textureKey, bool hiPoly)
		{
			if (info == null)
			{
				return null;
			}
			string info2 = info.GetInfo(bundleKey);
			string text = info.GetInfo(textureKey);
			if (info2 == "0" || string.IsNullOrEmpty(info2))
			{
				info2 = info.GetInfo(ChaListDefine.KeyType.MainAB);
			}
			if (info2 == "0" || string.IsNullOrEmpty(info2) || text == "0" || string.IsNullOrEmpty(text))
			{
				return null;
			}
			if (!hiPoly)
			{
				text += "_low";
			}
			return CommonLib.LoadAsset<Texture2D>(info2, text, clone: false, info.GetInfo(ChaListDefine.KeyType.MainManifest));
		}

		private void InitializeCompositors(GameObject source, CustomTextureCreate[] target, int pieceKind)
		{
			ListInfoComponent listInfoComponent = ((source == null) ? null : source.GetComponent<ListInfoComponent>());
			ListInfoBase listInfoBase = ((listInfoComponent == null) ? null : listInfoComponent.data);
			if (listInfoBase == null)
			{
				return;
			}
			if (byte.TryParse(listInfoBase.GetInfo(ChaListDefine.KeyType.StateType), out var result))
			{
				supportsHalf = result != 1;
			}
			if (Record != null && Record.Kind == 0 && pieceKind <= 0)
			{
				BodyMask = LoadOverMask(listInfoBase, ChaListDefine.KeyType.OverBodyMaskAB, ChaListDefine.KeyType.OverBodyMask);
				BraMask = LoadOverMask(listInfoBase, ChaListDefine.KeyType.OverBraMaskAB, ChaListDefine.KeyType.OverBraMask);
			}
			ChaListDefine.KeyType[] array = new ChaListDefine.KeyType[3]
			{
				ChaListDefine.KeyType.MainTexAB,
				ChaListDefine.KeyType.MainTex02AB,
				ChaListDefine.KeyType.MainTex03AB
			};
			ChaListDefine.KeyType[] array2 = new ChaListDefine.KeyType[3]
			{
				ChaListDefine.KeyType.MainTex,
				ChaListDefine.KeyType.MainTex02,
				ChaListDefine.KeyType.MainTex03
			};
			ChaListDefine.KeyType[] array3 = new ChaListDefine.KeyType[3]
			{
				ChaListDefine.KeyType.ColorMaskAB,
				ChaListDefine.KeyType.ColorMask02AB,
				ChaListDefine.KeyType.ColorMask03AB
			};
			ChaListDefine.KeyType[] array4 = new ChaListDefine.KeyType[3]
			{
				ChaListDefine.KeyType.ColorMaskTex,
				ChaListDefine.KeyType.ColorMask02Tex,
				ChaListDefine.KeyType.ColorMask03Tex
			};
			for (int i = 0; i < 3; i++)
			{
				Texture2D texture2D = LoadTexture(listInfoBase, array[i], array2[i], highPoly);
				Texture2D texture2D2 = LoadTexture(listInfoBase, array3[i], array4[i], highPoly);
				if (!(texture2D == null) && (i != 0 || !(texture2D2 == null)))
				{
					CustomTextureCreate customTextureCreate = new CustomTextureCreate(source.transform);
					if (!customTextureCreate.Initialize("chara/mm_base.unity3d", "cf_m_clothesN_create", "", texture2D.width, texture2D.height))
					{
						customTextureCreate.Release();
						continue;
					}
					customTextureCreate.SetMainTexture(texture2D);
					customTextureCreate.SetTexture(ChaShader._ColorMask, texture2D2);
					target[i] = customTextureCreate;
				}
			}
		}

		private Texture LoadOverMask(ListInfoBase info, ChaListDefine.KeyType bundleKey, ChaListDefine.KeyType nameKey)
		{
			string info2 = info.GetInfo(bundleKey);
			string text = info.GetInfo(nameKey);
			if (string.IsNullOrEmpty(info2) || info2 == "0" || string.IsNullOrEmpty(text) || text == "0")
			{
				return null;
			}
			if (!highPoly)
			{
				text += "_low";
			}
			string manifestName = ((ExtraType == 1) ? "add54" : "abdata");
			Texture texture = CommonLib.LoadAsset<Texture>(info2, text, clone: false, manifestName);
			if (texture != null)
			{
				Character.AddLoadAssetBundle(info2, manifestName);
			}
			return texture;
		}

		internal void CaptureBaseline()
		{
			HashSet<Renderer> borrowed = WardrobeBorrowedBodyMaterialsKoi.Targets(Root);
			Renderer[] renderers = Renderers;
			foreach (Renderer renderer in renderers)
			{
				Material[] array = WardrobeBorrowedBodyMaterialsKoi.Materials(renderer, borrowed);
				for (int j = 0; j < array.Length; j++)
				{
					Material material = array[j];
					if (material == null)
					{
						continue;
					}
					string[] array2 = new string[6] { "_MainTex", "_MainTex2", "_BumpMap", "_DetailMainTex", "_ColorMask", "_PatternMask" };
					foreach (string text in array2)
					{
						if (material.HasProperty(text))
						{
							baseline[Path(renderer.transform) + "#" + j + text] = material.GetTexture(text);
						}
					}
				}
			}
		}

		internal void Repaint()
		{
			if (Part == null || Part.colorInfo == null)
			{
				return;
			}
			if (Pieces.Count == 0)
			{
				RepaintOne(Component, composed, StandaloneSubKind);
			}
			else
			{
				foreach (Piece piece in Pieces)
				{
					RepaintOne(piece.Component, piece.Composed, piece.Kind);
				}
			}
			ApplyState();
		}

		private ChaFileClothes.PartsInfo.ColorInfo Entry(int channel, int pieceKind)
		{
			int num = WardrobeCompositeTopKoi.ChannelIndex(pieceKind, channel);
			if (num >= Part.colorInfo.Length)
			{
				num = channel;
			}
			if (num >= Part.colorInfo.Length)
			{
				return null;
			}
			return Part.colorInfo[num];
		}

		private void RepaintOne(ChaClothesComponent component, CustomTextureCreate[] textures, int pieceKind)
		{
			HashSet<Renderer> borrowed = WardrobeBorrowedBodyMaterialsKoi.Targets(Root);
			if (component == null)
			{
				return;
			}
			int[] array = new int[3]
			{
				ChaShader._PatternMask1,
				ChaShader._PatternMask2,
				ChaShader._PatternMask3
			};
			CustomTextureCreate[] array2;
			for (int i = 0; i < 3; i++)
			{
				ChaFileClothes.PartsInfo.ColorInfo colorInfo = Entry(i, pieceKind);
				if (colorInfo == null)
				{
					continue;
				}
				Texture2D tex = null;
				if (colorInfo.pattern != 0)
				{
					Dictionary<int, ListInfoBase> categoryInfo = Character.chaListCtrl.GetCategoryInfo(ChaListDefine.CategoryNo.mt_pattern);
					if (categoryInfo != null && categoryInfo.TryGetValue(colorInfo.pattern, out var value))
					{
						tex = LoadTexture(value, ChaListDefine.KeyType.MainTexAB, ChaListDefine.KeyType.MainTex, highPoly);
					}
				}
				array2 = textures;
				for (int j = 0; j < array2.Length; j++)
				{
					array2[j]?.SetTexture(array[i], tex);
				}
			}
			ChaFileClothes.PartsInfo.ColorInfo colorInfo2 = Entry(0, pieceKind);
			ChaFileClothes.PartsInfo.ColorInfo colorInfo3 = Entry(1, pieceKind);
			ChaFileClothes.PartsInfo.ColorInfo colorInfo4 = Entry(2, pieceKind);
			if (colorInfo2 == null || colorInfo3 == null || colorInfo4 == null)
			{
				return;
			}
			array2 = textures;
			foreach (CustomTextureCreate customTextureCreate in array2)
			{
				if (customTextureCreate != null)
				{
					customTextureCreate.SetColor(ChaShader._Color, colorInfo2.baseColor);
					customTextureCreate.SetColor(ChaShader._Color1_2, colorInfo2.patternColor);
					customTextureCreate.SetColor(ChaShader._Color2, colorInfo3.baseColor);
					customTextureCreate.SetColor(ChaShader._Color2_2, colorInfo3.patternColor);
					customTextureCreate.SetColor(ChaShader._Color3, colorInfo4.baseColor);
					customTextureCreate.SetColor(ChaShader._Color3_2, colorInfo4.patternColor);
					customTextureCreate.SetFloat(ChaShader._PatternScale1u, colorInfo2.tiling.x);
					customTextureCreate.SetFloat(ChaShader._PatternScale1v, colorInfo2.tiling.y);
					customTextureCreate.SetFloat(ChaShader._PatternScale2u, colorInfo3.tiling.x);
					customTextureCreate.SetFloat(ChaShader._PatternScale2v, colorInfo3.tiling.y);
					customTextureCreate.SetFloat(ChaShader._PatternScale3u, colorInfo4.tiling.x);
					customTextureCreate.SetFloat(ChaShader._PatternScale3v, colorInfo4.tiling.y);
					customTextureCreate.SetFloat(ChaShader._PatternOffset1u, colorInfo2.offset.x);
					customTextureCreate.SetFloat(ChaShader._PatternOffset1v, colorInfo2.offset.y);
					customTextureCreate.SetFloat(ChaShader._PatternOffset2u, colorInfo3.offset.x);
					customTextureCreate.SetFloat(ChaShader._PatternOffset2v, colorInfo3.offset.y);
					customTextureCreate.SetFloat(ChaShader._PatternOffset3u, colorInfo4.offset.x);
					customTextureCreate.SetFloat(ChaShader._PatternOffset3v, colorInfo4.offset.y);
					customTextureCreate.SetFloat(ChaShader._PatternRotator1, colorInfo2.rotate);
					customTextureCreate.SetFloat(ChaShader._PatternRotator2, colorInfo3.rotate);
					customTextureCreate.SetFloat(ChaShader._PatternRotator3, colorInfo4.rotate);
				}
			}
			Renderer[][] array3 = new Renderer[3][] { component.rendNormal01, component.rendNormal02, component.rendNormal03 };
			for (int k = 0; k < array3.Length; k++)
			{
				Texture texture = ((textures[k] == null) ? null : textures[k].RebuildTextureAndSetMaterial());
				IEnumerable<Renderer> enumerable2;
				if (k != 0)
				{
					IEnumerable<Renderer> enumerable = array3[k] ?? new Renderer[0];
					enumerable2 = enumerable;
				}
				else
				{
					enumerable2 = (array3[0] ?? new Renderer[0]).Concat(component.rendAlpha01 ?? new Renderer[0]);
				}
				IEnumerable<Renderer> enumerable3 = enumerable2;
				if (k == 0 && component.rendAccessory != null)
				{
					enumerable3 = enumerable3.Concat(new Renderer[1] { component.rendAccessory });
				}
				foreach (Renderer item in enumerable3)
				{
					if (item == null)
					{
						continue;
					}
					Material[] array4 = WardrobeBorrowedBodyMaterialsKoi.Materials(item, borrowed);
					foreach (Material material in array4)
					{
						if (material == null)
						{
							continue;
						}
						if (texture != null && material.HasProperty(ChaShader._MainTex))
						{
							material.SetTexture(ChaShader._MainTex, texture);
						}
						for (int l = 0; l < 3; l++)
						{
							ChaFileClothes.PartsInfo.ColorInfo colorInfo5 = Entry(l, pieceKind);
							if (colorInfo5 != null)
							{
								string name = ((l == 0) ? "_Color" : ("_Color" + (l + 1)));
								if (texture == null && material.HasProperty(name))
								{
									material.SetColor(name, colorInfo5.baseColor);
								}
								string name2 = "_Color" + (l + 1) + "_2";
								if (texture == null && material.HasProperty(name2))
								{
									material.SetColor(name2, colorInfo5.patternColor);
								}
							}
						}
					}
				}
			}
		}

		internal void ApplyState()
		{
			if (Root == null || Record == null)
			{
				return;
			}
			bool enabled = Record.State == 0 || (Record.State == 1 && !supportsHalf);
			bool enabled2 = Record.State == 1 && supportsHalf;
			GameObject[] array = ((Pieces.Count != 0) ? Pieces.Select((Piece x) => x.Root).ToArray() : new GameObject[1] { Root });
			foreach (GameObject root in array)
			{
				if (Record.Kind <= 3)
				{
					ToggleNamed(root, "n_top_a", enabled);
					ToggleNamed(root, "n_top_b", enabled2);
					ToggleNamed(root, "n_bot_a", enabled);
					ToggleNamed(root, "n_bot_b", enabled2);
					if (Record.Kind == 3)
					{
						ToggleNamed(root, "n_bot_c", enabled2);
					}
					if (Record.Kind == 2)
					{
						ToggleNamed(root, "o_bra_a", enabled);
						ToggleNamed(root, "o_bra_b", enabled2);
					}
				}
				else if (Record.Kind == 5)
				{
					ToggleNamed(root, "n_panst_a", enabled);
					ToggleNamed(root, "n_panst_b", enabled2);
				}
			}
			ChaClothesComponent[] components = Components;
			foreach (ChaClothesComponent chaClothesComponent in components)
			{
				Toggle(chaClothesComponent.objOpt01, Part == null || Part.hideOpt == null || Part.hideOpt.Length < 1 || !Part.hideOpt[0]);
				Toggle(chaClothesComponent.objOpt02, Part == null || Part.hideOpt == null || Part.hideOpt.Length < 2 || !Part.hideOpt[1]);
				if (Part != null)
				{
					Toggle(chaClothesComponent.objSleeves01, Part.sleevesType == 0);
					Toggle(chaClothesComponent.objSleeves02, Part.sleevesType == 1);
					Toggle(chaClothesComponent.objSleeves03, Part.sleevesType == 2);
				}
			}
			Root.SetActive(Record.State != 2);
		}

		private static void ToggleNamed(GameObject root, string name, bool enabled)
		{
			if (root == null)
			{
				return;
			}
			Transform[] componentsInChildren = root.GetComponentsInChildren<Transform>(includeInactive: true);
			foreach (Transform transform in componentsInChildren)
			{
				if (transform != null && transform.name == name)
				{
					transform.gameObject.SetActive(enabled);
					break;
				}
			}
		}

		private static void Toggle(GameObject[] objects, bool enabled)
		{
			if (objects == null)
			{
				return;
			}
			foreach (GameObject gameObject in objects)
			{
				if (gameObject != null)
				{
					gameObject.SetActive(enabled);
				}
			}
		}

		internal void SyncCharacterVisibility(ChaControl cha)
		{
			if (!(Root == null) && !(cha == null))
			{
				bool flag = Record.State != 2 && cha.visibleAll && cha.fileStatus.visibleBodyAlways && !cha.fileStatus.visibleSimple && cha.objTop != null && cha.objTop.activeInHierarchy;
				if (Root.activeSelf != flag)
				{
					Root.SetActive(flag);
				}
			}
		}

		internal byte[] Capture(Func<Texture, bool> exclude = null)
		{
			HashSet<Renderer> borrowed = WardrobeBorrowedBodyMaterialsKoi.Targets(Root);
			using MemoryStream memoryStream = new MemoryStream();
			using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
			binaryWriter.Write(1263289158);
			binaryWriter.Write(2);
			Renderer[] renderers = Renderers;
			binaryWriter.Write(renderers.Length);
			Renderer[] array = renderers;
			foreach (Renderer renderer in array)
			{
				string text = Path(renderer.transform);
				binaryWriter.Write(text);
				binaryWriter.Write(renderer.enabled);
				Material[] array2 = WardrobeBorrowedBodyMaterialsKoi.Materials(renderer, borrowed);
				binaryWriter.Write(array2.Length);
				for (int j = 0; j < array2.Length; j++)
				{
					Material material = array2[j];
					binaryWriter.Write(material != null);
					if (material == null)
					{
						continue;
					}
					binaryWriter.Write((material.shader == null) ? "" : material.shader.name);
					binaryWriter.Write(material.renderQueue);
					string[] array3 = new string[6] { "_Color", "_Color2", "_Color3", "_Color4", "_SpecColor", "_EmissionColor" };
					binaryWriter.Write(array3.Length);
					string[] array4 = array3;
					foreach (string name in array4)
					{
						bool flag = material.HasProperty(name);
						binaryWriter.Write(flag);
						if (flag)
						{
							WriteColor(binaryWriter, material.GetColor(name));
						}
					}
					string[] array5 = new string[7] { "_Glossiness", "_Metallic", "_Cutoff", "_BumpScale", "_Alpha", "_Glossiness4", "_Metallic4" };
					binaryWriter.Write(array5.Length);
					array4 = array5;
					foreach (string name2 in array4)
					{
						bool flag2 = material.HasProperty(name2);
						binaryWriter.Write(flag2);
						if (flag2)
						{
							binaryWriter.Write(material.GetFloat(name2));
						}
					}
					string[] array6 = new string[6] { "_MainTex", "_MainTex2", "_BumpMap", "_DetailMainTex", "_ColorMask", "_PatternMask" };
					binaryWriter.Write(array6.Length);
					array4 = array6;
					foreach (string text2 in array4)
					{
						Texture texture = (material.HasProperty(text2) ? material.GetTexture(text2) : null);
						baseline.TryGetValue(text + "#" + j + text2, out var value);
						bool flag3 = false;
						CustomTextureCreate[] array7 = composed;
						foreach (CustomTextureCreate customTextureCreate in array7)
						{
							if (customTextureCreate != null && customTextureCreate.GetCreateTexture() == texture)
							{
								flag3 = true;
							}
						}
						foreach (Piece piece in Pieces)
						{
							array7 = piece.Composed;
							foreach (CustomTextureCreate customTextureCreate2 in array7)
							{
								if (customTextureCreate2 != null && customTextureCreate2.GetCreateTexture() == texture)
								{
									flag3 = true;
								}
							}
						}
						byte[] array8 = ((texture != null && texture != value && !flag3 && (exclude == null || !exclude(texture))) ? EncodeTexture(texture) : null);
						binaryWriter.Write((array8 != null) ? array8.Length : 0);
						if (array8 != null)
						{
							binaryWriter.Write(array8);
						}
					}
				}
				SkinnedMeshRenderer skinnedMeshRenderer = renderer as SkinnedMeshRenderer;
				Mesh mesh = ((skinnedMeshRenderer == null) ? null : skinnedMeshRenderer.sharedMesh);
				int num = ((!(mesh == null)) ? mesh.blendShapeCount : 0);
				binaryWriter.Write(num);
				for (int m = 0; m < num; m++)
				{
					binaryWriter.Write(mesh.GetBlendShapeName(m));
					binaryWriter.Write(skinnedMeshRenderer.GetBlendShapeWeight(m));
				}
			}
			if (memoryStream.Length > 67108864)
			{
				throw new InvalidOperationException("Clothing appearance is too large.");
			}
			return memoryStream.ToArray();
		}

		internal void Restore(byte[] bytes)
		{
			if (bytes == null)
			{
				return;
			}
			ResetRestoredTextures();
			HashSet<Renderer> borrowed = WardrobeBorrowedBodyMaterialsKoi.Targets(Root);
			using MemoryStream memoryStream = new MemoryStream(bytes, writable: false);
			using BinaryReader binaryReader = new BinaryReader(memoryStream);
			if (binaryReader.ReadInt32() != 1263289158)
			{
				throw new InvalidDataException("Unknown KOI clothing appearance format.");
			}
			int num = binaryReader.ReadInt32();
			if (num != 1 && num != 2)
			{
				throw new InvalidDataException("Unknown KOI clothing appearance version.");
			}
			Dictionary<string, Renderer> dictionary = (from x in Renderers
				group x by Path(x.transform)).ToDictionary((IGrouping<string, Renderer> x) => x.Key, (IGrouping<string, Renderer> x) => x.First());
			int num2 = Count(binaryReader, 512);
			for (int num3 = 0; num3 < num2; num3++)
			{
				string key = binaryReader.ReadString();
				bool enabled = binaryReader.ReadBoolean();
				dictionary.TryGetValue(key, out var value);
				if (value != null)
				{
					value.enabled = enabled;
				}
				Material[] array = WardrobeBorrowedBodyMaterialsKoi.Materials(value, borrowed);
				int num4 = Count(binaryReader, 128);
				for (int num5 = 0; num5 < num4; num5++)
				{
					if (!binaryReader.ReadBoolean())
					{
						continue;
					}
					string text = binaryReader.ReadString();
					int renderQueue = binaryReader.ReadInt32();
					Material material = ((num5 < array.Length) ? array[num5] : null);
					if (material != null && material.shader != null && material.shader.name == text)
					{
						material.renderQueue = renderQueue;
					}
					string[] array2 = new string[6] { "_Color", "_Color2", "_Color3", "_Color4", "_SpecColor", "_EmissionColor" };
					if (Count(binaryReader, 32) != array2.Length)
					{
						throw new InvalidDataException("Invalid clothing colors.");
					}
					string[] array3 = array2;
					foreach (string name in array3)
					{
						if (binaryReader.ReadBoolean())
						{
							Color value2 = ReadColor(binaryReader);
							if (material != null && material.HasProperty(name))
							{
								material.SetColor(name, value2);
							}
						}
					}
					string[] array4 = ((num != 1) ? new string[7] { "_Glossiness", "_Metallic", "_Cutoff", "_BumpScale", "_Alpha", "_Glossiness4", "_Metallic4" } : new string[5] { "_Glossiness", "_Metallic", "_Cutoff", "_BumpScale", "_Alpha" });
					if (Count(binaryReader, 32) != array4.Length)
					{
						throw new InvalidDataException("Invalid clothing floats.");
					}
					array3 = array4;
					foreach (string name2 in array3)
					{
						if (binaryReader.ReadBoolean())
						{
							float value3 = binaryReader.ReadSingle();
							if (material != null && material.HasProperty(name2))
							{
								material.SetFloat(name2, value3);
							}
						}
					}
					string[] array5 = new string[6] { "_MainTex", "_MainTex2", "_BumpMap", "_DetailMainTex", "_ColorMask", "_PatternMask" };
					if (Count(binaryReader, 32) != array5.Length)
					{
						throw new InvalidDataException("Invalid clothing textures.");
					}
					array3 = array5;
					foreach (string text2 in array3)
					{
						int num7 = binaryReader.ReadInt32();
						if (num7 < 0 || num7 > 33554432 || num7 > memoryStream.Length - memoryStream.Position)
						{
							throw new InvalidDataException("Invalid clothing texture payload.");
						}
						if (num7 == 0)
						{
							continue;
						}
						byte[] data = binaryReader.ReadBytes(num7);
						if (!(material == null) && material.HasProperty(text2))
						{
							Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
							if (!texture2D.LoadImage(data))
							{
								UnityEngine.Object.Destroy(texture2D);
								throw new InvalidDataException("Invalid clothing texture image.");
							}
							restored.Add(new RestoredTexture
							{
								Material = material,
								Property = text2,
								Original = material.GetTexture(text2),
								Created = texture2D
							});
							material.SetTexture(text2, texture2D);
						}
					}
				}
				SkinnedMeshRenderer skinnedMeshRenderer = value as SkinnedMeshRenderer;
				Mesh mesh = ((skinnedMeshRenderer == null) ? null : skinnedMeshRenderer.sharedMesh);
				int num8 = Count(binaryReader, 4096);
				for (int num9 = 0; num9 < num8; num9++)
				{
					string blendShapeName = binaryReader.ReadString();
					float value4 = binaryReader.ReadSingle();
					if (mesh != null)
					{
						int blendShapeIndex = mesh.GetBlendShapeIndex(blendShapeName);
						if (blendShapeIndex >= 0)
						{
							skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, value4);
						}
					}
				}
			}
			if (memoryStream.Position != memoryStream.Length)
			{
				throw new InvalidDataException("Trailing clothing appearance data.");
			}
		}

		private static int Count(BinaryReader reader, int max)
		{
			int num = reader.ReadInt32();
			if (num < 0 || num > max)
			{
				throw new InvalidDataException("Invalid clothing appearance count.");
			}
			return num;
		}

		private void ResetRestoredTextures()
		{
			foreach (RestoredTexture item in restored)
			{
				if (item.Material != null && item.Material.GetTexture(item.Property) == item.Created)
				{
					item.Material.SetTexture(item.Property, item.Original);
				}
				if (item.Created != null)
				{
					UnityEngine.Object.Destroy(item.Created);
				}
			}
			restored.Clear();
		}

		private static void WriteColor(BinaryWriter writer, Color value)
		{
			writer.Write(value.r);
			writer.Write(value.g);
			writer.Write(value.b);
			writer.Write(value.a);
		}

		private static Color ReadColor(BinaryReader reader)
		{
			return new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
		}

		private string Path(Transform node)
		{
			List<string> list = new List<string>();
			while (node != null && node != Root.transform)
			{
				list.Add(node.name);
				node = node.parent;
			}
			list.Reverse();
			return string.Join("/", list.ToArray());
		}

		private static byte[] EncodeTexture(Texture texture)
		{
			if (texture.width > 4096 || texture.height > 4096)
			{
				return null;
			}
			RenderTexture active = RenderTexture.active;
			RenderTexture renderTexture = null;
			Texture2D texture2D = null;
			try
			{
				renderTexture = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
				Graphics.Blit(texture, renderTexture);
				RenderTexture.active = renderTexture;
				texture2D = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, mipChain: false);
				texture2D.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
				texture2D.Apply();
				return texture2D.EncodeToPNG();
			}
			finally
			{
				RenderTexture.active = active;
				if (renderTexture != null)
				{
					RenderTexture.ReleaseTemporary(renderTexture);
				}
				if (texture2D != null)
				{
					UnityEngine.Object.Destroy(texture2D);
				}
			}
		}

		public void Dispose()
		{
			Dispose(destroyRoot: true);
		}

		internal void Dispose(bool destroyRoot)
		{
			ResetRestoredTextures();
			CustomTextureCreate[] array = composed;
			for (int i = 0; i < array.Length; i++)
			{
				array[i]?.Release();
			}
			foreach (Piece piece in Pieces)
			{
				array = piece.Composed;
				for (int i = 0; i < array.Length; i++)
				{
					array[i]?.Release();
				}
			}
			Pieces.Clear();
			foreach (UnityEngine.Object item in owned)
			{
				if (item != null)
				{
					UnityEngine.Object.Destroy(item);
				}
			}
			owned.Clear();
			BodyMask = (BraMask = null);
			if (destroyRoot && Root != null)
			{
				UnityEngine.Object.Destroy(Root);
			}
			Root = null;
		}
	}
	internal sealed class ClothingSlotSubPart
	{
		public int ItemId;

		public int SourceId;

		public string SourceGuid = "";

		public ClothingSlotSubPart Clone()
		{
			return (ClothingSlotSubPart)MemberwiseClone();
		}
	}
	internal sealed class ClothingSlotRecord
	{
		public string Key = Guid.NewGuid().ToString("N");

		public int Kind;

		public int ItemId;

		public int SourceId;

		public int SourceCategory;

		public string SourceGuid = "";

		public byte State;

		public byte[] Part;

		public byte[] Appearance;

		public ClothingSlotSubPart[] SubParts;

		public ClothingSlotRecord Clone()
		{
			return new ClothingSlotRecord
			{
				Key = Key,
				Kind = Kind,
				ItemId = ItemId,
				SourceId = SourceId,
				SourceGuid = SourceGuid,
				SourceCategory = SourceCategory,
				State = State,
				Part = Copy(Part),
				Appearance = Copy(Appearance),
				SubParts = ((SubParts == null) ? null : SubParts.Select((ClothingSlotSubPart x) => x?.Clone()).ToArray())
			};
		}

		private static byte[] Copy(byte[] value)
		{
			if (value != null)
			{
				return (byte[])value.Clone();
			}
			return null;
		}
	}
	internal sealed class ClothingSlotSet
	{
		internal const int MaxExtra = 4;

		internal const int KindCount = 9;

		private readonly List<ClothingSlotRecord> items = new List<ClothingSlotRecord>();

		private readonly Dictionary<int, ClothingSlotRecord> primary = new Dictionary<int, ClothingSlotRecord>();

		public IEnumerable<ClothingSlotRecord> Items => items;

		public IEnumerable<ClothingSlotRecord> PrimaryRecords => primary.Values;

		public ClothingSlotRecord GetPrimary(int kind)
		{
			if (!primary.TryGetValue(kind, out var value))
			{
				return null;
			}
			return value;
		}

		public void SetPrimary(int kind, ClothingSlotRecord record)
		{
			if (kind < 0 || kind >= 9)
			{
				throw new ArgumentOutOfRangeException("kind");
			}
			if (record == null)
			{
				primary.Remove(kind);
				return;
			}
			ClothingSlotRecord clothingSlotRecord = record.Clone();
			clothingSlotRecord.Kind = kind;
			primary[kind] = clothingSlotRecord;
		}

		public int Count(int kind)
		{
			return items.Count((ClothingSlotRecord x) => x.Kind == kind);
		}

		public ClothingSlotRecord Get(int kind, int index)
		{
			if (index >= 1)
			{
				return items.Where((ClothingSlotRecord x) => x.Kind == kind).Skip(index - 1).FirstOrDefault();
			}
			return null;
		}

		public ClothingSlotRecord Add(int kind)
		{
			if (kind < 0 || kind >= 9 || Count(kind) >= 4)
			{
				return null;
			}
			ClothingSlotRecord clothingSlotRecord = new ClothingSlotRecord
			{
				Kind = kind
			};
			items.Add(clothingSlotRecord);
			return clothingSlotRecord;
		}

		public bool Remove(int kind, int index)
		{
			ClothingSlotRecord clothingSlotRecord = Get(kind, index);
			if (clothingSlotRecord != null)
			{
				return items.Remove(clothingSlotRecord);
			}
			return false;
		}

		public void ReplaceKind(int kind, ClothingSlotSet source)
		{
			ClothingSlotRecord[] collection = (from x in source.items
				where x.Kind == kind
				select x.Clone()).ToArray();
			items.RemoveAll((ClothingSlotRecord x) => x.Kind == kind);
			items.AddRange(collection);
			SetPrimary(kind, source.GetPrimary(kind));
		}

		public byte[] Encode(bool prune)
		{
			using MemoryStream memoryStream = new MemoryStream();
			using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
			ClothingSlotRecord[] array = items.Where((ClothingSlotRecord x) => !prune || x.ItemId != 0).ToArray();
			ClothingSlotRecord[] array2 = (from x in primary.Values
				where !prune || x.ItemId != 0
				orderby x.Kind
				select x).ToArray();
			bool flag = array.Concat(array2).Any((ClothingSlotRecord x) => x.SourceCategory != 0 || (x.SubParts != null && x.SubParts.Length != 0));
			binaryWriter.Write(1398027846);
			binaryWriter.Write(flag ? 3 : 2);
			binaryWriter.Write(array.Length);
			ClothingSlotRecord[] array3 = array;
			foreach (ClothingSlotRecord item in array3)
			{
				WriteRecord(binaryWriter, item, flag);
			}
			binaryWriter.Write(array2.Length);
			array3 = array2;
			foreach (ClothingSlotRecord item2 in array3)
			{
				WriteRecord(binaryWriter, item2, flag);
			}
			return memoryStream.ToArray();
		}

		public static ClothingSlotSet Decode(byte[] bytes)
		{
			ClothingSlotSet clothingSlotSet = new ClothingSlotSet();
			if (bytes == null || bytes.Length == 0)
			{
				return clothingSlotSet;
			}
			if (bytes.Length > 134217728)
			{
				throw new InvalidDataException("Clothing slot data exceeds the supported size.");
			}
			try
			{
				using (MemoryStream memoryStream = new MemoryStream(bytes, writable: false))
				{
					using BinaryReader binaryReader = new BinaryReader(memoryStream);
					if (binaryReader.ReadInt32() != 1398027846)
					{
						throw new InvalidDataException("Unknown clothing slot format.");
					}
					int num = binaryReader.ReadInt32();
					if (num < 1 || num > 3)
					{
						throw new InvalidDataException("Unknown clothing slot version.");
					}
					int num2 = binaryReader.ReadInt32();
					if (num2 < 0 || num2 > 36)
					{
						throw new InvalidDataException("Invalid clothing slot count.");
					}
					HashSet<string> hashSet = new HashSet<string>();
					for (int i = 0; i < num2; i++)
					{
						ClothingSlotRecord clothingSlotRecord = ReadRecord(binaryReader, num >= 3);
						if (clothingSlotRecord.Kind < 0 || clothingSlotRecord.Kind >= 9 || clothingSlotSet.Count(clothingSlotRecord.Kind) >= 4 || clothingSlotRecord.State > 2 || string.IsNullOrEmpty(clothingSlotRecord.Key) || !hashSet.Add(clothingSlotRecord.Key))
						{
							throw new InvalidDataException("Invalid clothing slot record.");
						}
						clothingSlotSet.items.Add(clothingSlotRecord);
					}
					if (num >= 2)
					{
						int num3 = binaryReader.ReadInt32();
						if (num3 < 0 || num3 > 9)
						{
							throw new InvalidDataException("Invalid native appearance count.");
						}
						for (int j = 0; j < num3; j++)
						{
							ClothingSlotRecord clothingSlotRecord2 = ReadRecord(binaryReader, num >= 3);
							if (clothingSlotRecord2.Kind < 0 || clothingSlotRecord2.Kind >= 9 || clothingSlotSet.primary.ContainsKey(clothingSlotRecord2.Kind) || clothingSlotRecord2.State > 2 || (num >= 3 && (string.IsNullOrEmpty(clothingSlotRecord2.Key) || !hashSet.Add(clothingSlotRecord2.Key))))
							{
								throw new InvalidDataException("Invalid native appearance record.");
							}
							clothingSlotSet.primary[clothingSlotRecord2.Kind] = clothingSlotRecord2;
						}
					}
					if (memoryStream.Position != memoryStream.Length)
					{
						throw new InvalidDataException("Trailing clothing slot data.");
					}
				}
				return clothingSlotSet;
			}
			catch (EndOfStreamException innerException)
			{
				throw new InvalidDataException("Truncated clothing slot data.", innerException);
			}
		}

		private static void WriteRecord(BinaryWriter writer, ClothingSlotRecord item, bool hasSubParts)
		{
			writer.Write(item.Key);
			writer.Write(item.Kind);
			writer.Write(item.ItemId);
			writer.Write(item.SourceGuid ?? "");
			writer.Write(item.SourceId);
			writer.Write(item.State);
			Blob(writer, item.Part);
			Blob(writer, item.Appearance);
			if (!hasSubParts)
			{
				return;
			}
			ValidateCategory(item.Kind, item.SourceCategory);
			writer.Write(item.SourceCategory);
			int num = ((item.SubParts != null) ? item.SubParts.Length : 0);
			if (num != 0 && (num != 3 || item.Kind != 0))
			{
				throw new InvalidDataException("Invalid composite top pieces.");
			}
			writer.Write(num);
			for (int i = 0; i < num; i++)
			{
				ClothingSlotSubPart clothingSlotSubPart = item.SubParts[i];
				if (clothingSlotSubPart == null || clothingSlotSubPart.ItemId < 0 || clothingSlotSubPart.SourceId < 0 || (clothingSlotSubPart.SourceGuid ?? "").Length > 4096)
				{
					throw new InvalidDataException("Invalid composite top piece.");
				}
				writer.Write(clothingSlotSubPart.ItemId);
				writer.Write(clothingSlotSubPart.SourceId);
				writer.Write(clothingSlotSubPart.SourceGuid ?? "");
			}
		}

		private static ClothingSlotRecord ReadRecord(BinaryReader reader, bool hasSubParts)
		{
			ClothingSlotRecord clothingSlotRecord = new ClothingSlotRecord
			{
				Key = reader.ReadString(),
				Kind = reader.ReadInt32(),
				ItemId = reader.ReadInt32(),
				SourceGuid = reader.ReadString(),
				SourceId = reader.ReadInt32(),
				State = reader.ReadByte(),
				Part = Blob(reader, 1048576),
				Appearance = Blob(reader, 67108864)
			};
			if (hasSubParts)
			{
				clothingSlotRecord.SourceCategory = reader.ReadInt32();
				ValidateCategory(clothingSlotRecord.Kind, clothingSlotRecord.SourceCategory);
				int num = reader.ReadInt32();
				if (num != 0 && (num != 3 || clothingSlotRecord.Kind != 0))
				{
					throw new InvalidDataException("Invalid composite top pieces.");
				}
				if (num != 0)
				{
					clothingSlotRecord.SubParts = new ClothingSlotSubPart[num];
				}
				for (int i = 0; i < num; i++)
				{
					ClothingSlotSubPart clothingSlotSubPart = new ClothingSlotSubPart
					{
						ItemId = reader.ReadInt32(),
						SourceId = reader.ReadInt32(),
						SourceGuid = reader.ReadString()
					};
					if (clothingSlotSubPart.ItemId < 0 || clothingSlotSubPart.SourceId < 0 || clothingSlotSubPart.SourceGuid.Length > 4096)
					{
						throw new InvalidDataException("Invalid composite top piece.");
					}
					clothingSlotRecord.SubParts[i] = clothingSlotSubPart;
				}
			}
			return clothingSlotRecord;
		}

		private static void ValidateCategory(int kind, int category)
		{
			if (category != 0 && (kind != 0 || ((category < 200 || category > 202) && (category < 210 || category > 212))))
			{
				throw new InvalidDataException("Invalid top piece category.");
			}
		}

		private static void Blob(BinaryWriter writer, byte[] value)
		{
			writer.Write((value != null) ? value.Length : 0);
			if (value != null)
			{
				writer.Write(value);
			}
		}

		private static byte[] Blob(BinaryReader reader, int maximum)
		{
			int num = reader.ReadInt32();
			if (num < 0 || num > maximum || num > reader.BaseStream.Length - reader.BaseStream.Position)
			{
				throw new InvalidDataException("Invalid clothing slot payload.");
			}
			if (num != 0)
			{
				return reader.ReadBytes(num);
			}
			return null;
		}
	}
	internal static class WardrobeClothingSlotsKoi
	{
		internal const string DataId = "com.freelancer604.wardrobe.clothingslots";

		private static bool registered;

		internal static void Register()
		{
			if (!registered)
			{
				CharacterApi.RegisterExtraBehaviour<ClothingSlotsControllerKoi>("com.freelancer604.wardrobe.clothingslots");
				registered = true;
			}
		}

		internal static ClothingSlotsControllerKoi Controller(ChaControl cha)
		{
			if (!(cha == null))
			{
				return cha.GetComponent<ClothingSlotsControllerKoi>();
			}
			return null;
		}

		internal static ClothingSlotSet Read(ChaFileCoordinate coordinate)
		{
			return Decode((coordinate == null) ? null : ExtendedSave.GetExtendedDataById(coordinate, "com.freelancer604.wardrobe.clothingslots"));
		}

		internal static ClothingSlotSet FromCard(ChaFileControl card)
		{
			return FromCard(card, 0);
		}

		internal static ClothingSlotSet FromCard(ChaFileControl card, int coordinate)
		{
			if (card == null || card.coordinate == null || coordinate < 0 || coordinate >= card.coordinate.Length)
			{
				return new ClothingSlotSet();
			}
			return DecodeForCoordinate(ExtendedSave.GetExtendedDataById((ChaFile)card, "com.freelancer604.wardrobe.clothingslots"), card, coordinate);
		}

		internal static ClothingSlotSet DecodeForCoordinate(PluginData cardData, ChaFileControl card, int coordinate)
		{
			ClothingSlotSet[] array = CardCoordinates(cardData, card);
			if (coordinate < 0 || coordinate >= array.Length)
			{
				return new ClothingSlotSet();
			}
			return array[coordinate];
		}

		internal static ClothingSlotSet[] CardCoordinates(PluginData cardData, ChaFileControl card)
		{
			if (card == null || card.coordinate == null)
			{
				return new ClothingSlotSet[0];
			}
			if (cardData != null && cardData.data.TryGetValue("coordinates", out var value) && value is byte[])
			{
				return KoiClothingCoordinateMap.Decode((byte[])value, card.coordinate.Length, DecodeBytes);
			}
			ClothingSlotSet[] array = new ClothingSlotSet[card.coordinate.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = Read(card.coordinate[i]);
			}
			return array;
		}

		internal static ClothingSlotSet Decode(PluginData data)
		{
			object value;
			ClothingSlotSet clothingSlotSet = ClothingSlotSet.Decode((data != null && data.data.TryGetValue("slots", out value)) ? (value as byte[]) : null);
			KoiClothingPartValidation.Sanitize(clothingSlotSet, delegate(byte[] bytes)
			{
				ChaFileClothes.PartsInfo partsInfo = MessagePackSerializer.Deserialize<ChaFileClothes.PartsInfo>(bytes);
				return partsInfo != null && partsInfo.colorInfo != null && partsInfo.colorInfo.Length >= 3;
			}, () => MessagePackSerializer.Serialize(new ChaFileClothes.PartsInfo()), delegate(Exception error)
			{
				WardrobePlugin.Log.LogWarning((object)("An invalid extra garment was skipped: " + error.GetBaseException().Message));
			});
			return clothingSlotSet;
		}

		private static ClothingSlotSet DecodeBytes(byte[] bytes)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Expected O, but got Unknown
			return Decode(new PluginData
			{
				version = 1,
				data = { ["slots"] = bytes }
			});
		}

		internal static PluginData Data(ClothingSlotSet set, bool prune)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Expected O, but got Unknown
			return new PluginData
			{
				version = 1,
				data = { ["slots"] = set.Encode(prune) }
			};
		}

		internal static PluginData CardData(ChaFileControl card, int active, ClothingSlotSet live)
		{
			if (card == null || card.coordinate == null)
			{
				return Data(live, prune: true);
			}
			PluginData val = Data(live, prune: true);
			ClothingSlotSet[] array = new ClothingSlotSet[card.coordinate.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = ((i == active) ? live : Read(card.coordinate[i]));
			}
			val.data["coordinates"] = KoiClothingCoordinateMap.Encode(array);
			return val;
		}

		public static byte[] CaptureState(ChaControl cha)
		{
			if (cha == null || cha.chaFile == null || cha.chaFile.coordinate == null)
			{
				return null;
			}
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = Controller(cha);
			if ((UnityEngine.Object)(object)clothingSlotsControllerKoi == null)
			{
				return null;
			}
			return (byte[])CardData(cha.chaFile, Convert.ToInt32(cha.fileStatus.coordinateType), clothingSlotsControllerKoi.Capture()).data["coordinates"];
		}

		public static void RestoreState(ChaControl cha, byte[] bytes)
		{
			if (cha == null || bytes == null || cha.chaFile == null || cha.chaFile.coordinate == null)
			{
				return;
			}
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = Controller(cha);
			if (!((UnityEngine.Object)(object)clothingSlotsControllerKoi == null))
			{
				ChaFileCoordinate[] coordinate = cha.chaFile.coordinate;
				ClothingSlotSet[] array = KoiClothingCoordinateMap.Decode(bytes, coordinate.Length, DecodeBytes);
				int num = Convert.ToInt32(cha.fileStatus.coordinateType);
				if (num < 0 || num >= array.Length)
				{
					throw new InvalidDataException("Invalid active clothing coordinate.");
				}
				for (int i = 0; i < array.Length; i++)
				{
					Attach(coordinate[i], array[i]);
				}
				Attach(cha.nowCoordinate, array[num]);
				clothingSlotsControllerKoi.Replace(array[num]);
			}
		}

		internal static void Attach(ChaFileCoordinate coordinate, ClothingSlotSet set, bool prune = false)
		{
			if (coordinate != null)
			{
				ExtendedSave.SetExtendedDataById(coordinate, "com.freelancer604.wardrobe.clothingslots", Data(set, prune));
			}
		}

		internal static void WriteLive(ChaControl cha, bool prune = false)
		{
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = Controller(cha);
			if ((UnityEngine.Object)(object)clothingSlotsControllerKoi != null)
			{
				Attach(cha.nowCoordinate, clothingSlotsControllerKoi.Capture(), prune);
			}
		}

		internal static void Apply(ChaControl cha, ChaFileCoordinate source, int kind = -1)
		{
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = Controller(cha);
			if (!((UnityEngine.Object)(object)clothingSlotsControllerKoi == null))
			{
				ClothingSlotSet set = Read(source);
				if (kind < 0)
				{
					clothingSlotsControllerKoi.Replace(set);
				}
				else
				{
					clothingSlotsControllerKoi.ReplaceKind(kind, set);
				}
			}
		}

		internal static ChaClothesComponent GetComponent(ChaControl cha, int kind, int index)
		{
			return WardrobeCompositeTopKoi.Components(cha, kind, index).FirstOrDefault();
		}

		internal static Renderer[] GetRenderers(ChaControl cha, int kind, int index)
		{
			if (cha == null)
			{
				return new Renderer[0];
			}
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = Controller(cha);
			ClothingSlotVisualKoi clothingSlotVisualKoi = (((UnityEngine.Object)(object)clothingSlotsControllerKoi == null) ? null : clothingSlotsControllerKoi.Visual(kind, index));
			if (clothingSlotVisualKoi != null)
			{
				return clothingSlotVisualKoi.Renderers;
			}
			if (index == 0 && !(cha.objClothes[kind] == null))
			{
				return cha.objClothes[kind].GetComponentsInChildren<Renderer>(includeInactive: true);
			}
			return new Renderer[0];
		}

		internal static void Repaint(ChaControl cha, int kind, int index)
		{
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = Controller(cha);
			ClothingSlotVisualKoi clothingSlotVisualKoi = (((UnityEngine.Object)(object)clothingSlotsControllerKoi == null) ? null : clothingSlotsControllerKoi.Visual(kind, index));
			if (clothingSlotVisualKoi != null)
			{
				clothingSlotVisualKoi.Repaint();
			}
			else if (index == 0)
			{
				WardrobeCompositeTopKoi.RepaintPrimary(cha, kind);
			}
		}

		internal static byte ToUiState(int kind, byte nativeState)
		{
			return KoiClothingState.ToUi(nativeState);
		}

		internal static byte ToNativeState(ChaControl cha, int kind, byte uiState)
		{
			return KoiClothingState.ToNative(uiState, cha != null && cha.IsClothesStateType(kind, 1));
		}

		internal static void CaptureAppearance(ChaControl cha, int kind, int index)
		{
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = Controller(cha);
			if ((UnityEngine.Object)(object)clothingSlotsControllerKoi != null)
			{
				clothingSlotsControllerKoi.CaptureAppearance(kind, index);
			}
		}

		internal static ChaFileClothes.PartsInfo.ColorInfo GetDefaultColor(ChaControl cha, int kind, int index, int channel)
		{
			return WardrobeCompositeTopKoi.GetDefaultColor(cha, kind, index, channel);
		}

		internal static ClothingSlotRecord CapturePrimary(ChaControl cha, int kind, bool clipboard = true)
		{
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = Controller(cha);
			if ((UnityEngine.Object)(object)clothingSlotsControllerKoi != null && clothingSlotsControllerKoi.IsManagedPrimary(kind))
			{
				clothingSlotsControllerKoi.Capture();
				return clothingSlotsControllerKoi.Record(kind, 0).Clone();
			}
			ChaFileClothes.PartsInfo partsInfo = cha.nowCoordinate.clothes.parts[kind];
			GameObject gameObject = cha.objClothes[kind];
			ClothingSlotRecord clothingSlotRecord = new ClothingSlotRecord
			{
				Kind = kind,
				ItemId = partsInfo.id,
				SourceId = partsInfo.id,
				Part = MessagePackSerializer.Serialize(partsInfo),
				State = ToUiState(kind, cha.fileStatus.clothesState[kind])
			};
			ClothingSlotIdentityKoi.Capture(kind, partsInfo.id, clothingSlotRecord);
			if (kind == 0 && WardrobeCompositeTopKoi.TopType(partsInfo.id) != 0)
			{
				clothingSlotRecord.SubParts = WardrobeCompositeTopKoi.CaptureSubParts(cha);
			}
			if (gameObject != null)
			{
				Func<Texture, bool> exclude = delegate(Texture texture)
				{
					for (int i = 0; i < 3; i++)
					{
						if (cha.ctCreateClothes[kind, i] != null && cha.ctCreateClothes[kind, i].GetCreateTexture() == texture)
						{
							return true;
						}
					}
					return false;
				};
				if (clipboard)
				{
					ClothingSlotVisualKoi clothingSlotVisualKoi = new ClothingSlotVisualKoi
					{
						Root = gameObject,
						Part = partsInfo,
						Record = clothingSlotRecord,
						Component = cha.cusClothesCmp[kind]
					};
					clothingSlotRecord.Appearance = clothingSlotVisualKoi.Capture(exclude);
				}
				else
				{
					ClothingPrimaryAppearanceOwnerKoi clothingPrimaryAppearanceOwnerKoi = gameObject.GetComponent<ClothingPrimaryAppearanceOwnerKoi>() ?? gameObject.AddComponent<ClothingPrimaryAppearanceOwnerKoi>();
					clothingSlotRecord.Appearance = clothingPrimaryAppearanceOwnerKoi.Capture(cha, kind, clothingSlotRecord, exclude);
				}
			}
			return clothingSlotRecord;
		}

		internal static void ApplyPrimaryAppearance(ChaControl cha, int kind, ClothingSlotRecord record)
		{
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = Controller(cha);
			if ((UnityEngine.Object)(object)clothingSlotsControllerKoi != null)
			{
				clothingSlotsControllerKoi.SetPrimary(kind, record);
			}
		}

		internal static void ApplyPrimaryFrom(ChaControl cha, ChaFileCoordinate source, int kind)
		{
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = Controller(cha);
			if ((UnityEngine.Object)(object)clothingSlotsControllerKoi != null)
			{
				clothingSlotsControllerKoi.SetPrimary(kind, Read(source).GetPrimary(kind));
			}
		}

		internal static void ClearPrimary(ChaControl cha, int kind)
		{
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = Controller(cha);
			if ((UnityEngine.Object)(object)clothingSlotsControllerKoi != null)
			{
				clothingSlotsControllerKoi.SetPrimary(kind, null);
			}
		}

		internal static void ReapplyPrimary(ChaControl cha, int kind)
		{
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = Controller(cha);
			if ((UnityEngine.Object)(object)clothingSlotsControllerKoi != null)
			{
				clothingSlotsControllerKoi.ReapplyPrimary(kind);
			}
		}

		internal static void RestorePrimaryAppearance(ChaControl cha, int kind, ClothingSlotRecord record)
		{
			if (!(cha.objClothes[kind] == null) && record != null && record.Appearance != null)
			{
				GameObject gameObject = cha.objClothes[kind];
				(gameObject.GetComponent<ClothingPrimaryAppearanceOwnerKoi>() ?? gameObject.AddComponent<ClothingPrimaryAppearanceOwnerKoi>()).Apply(cha, kind, record);
			}
		}
	}
	public sealed class ClothingSlotsControllerKoi : CharaCustomFunctionController
	{
		private sealed class BraMaskLease
		{
			internal Material Material;

			internal Texture Baseline;

			internal Texture2D Combined;

			internal string Signature;

			internal string FailedSignature;
		}

		private ClothingSlotSet slots = new ClothingSlotSet();

		private readonly Dictionary<string, ChaFileClothes.PartsInfo> parts = new Dictionary<string, ChaFileClothes.PartsInfo>();

		private readonly Dictionary<string, ClothingSlotVisualKoi> visuals = new Dictionary<string, ClothingSlotVisualKoi>();

		private readonly Dictionary<int, GameObject> primaryRoots = new Dictionary<int, GameObject>();

		private readonly HashSet<string> failed = new HashSet<string>();

		private Material maskMaterial;

		private Texture baseMask;

		private Texture2D combinedMask;

		private string maskSignature;

		private string maskFailureSignature;

		private readonly List<BraMaskLease> braMasks = new List<BraMaskLease>();

		private Transform skeletonRoot;

		private bool rebuild;

		private int loadedCoordinateIndex = -1;

		private ChaFileCoordinate loadedCoordinate;

		private static readonly MethodInfo loader = typeof(ChaControl).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).FirstOrDefault((MethodInfo x) => x.Name == "LoadCharaFbxData" && x.ReturnType == typeof(GameObject) && x.GetParameters().Length == 10 && x.GetParameters()[0].ParameterType == typeof(Action<ListInfoBase>));

		private IEnumerable<ClothingSlotRecord> RenderRecords => slots.Items.Concat(slots.PrimaryRecords.Where((ClothingSlotRecord x) => WardrobeCompositeTopKoi.IsPieceCategory(x.SourceCategory)));

		internal int Count(int kind)
		{
			return slots.Count(kind);
		}

		internal ClothingSlotRecord Record(int kind, int index)
		{
			if (index != 0)
			{
				return slots.Get(kind, index);
			}
			return slots.GetPrimary(kind);
		}

		internal bool IsManagedPrimary(int kind)
		{
			ClothingSlotRecord primary = slots.GetPrimary(kind);
			if (primary != null)
			{
				return WardrobeCompositeTopKoi.IsPieceCategory(primary.SourceCategory);
			}
			return false;
		}

		private int IndexOf(ClothingSlotRecord record)
		{
			if (slots.GetPrimary(record.Kind) != record)
			{
				return slots.Items.Where((ClothingSlotRecord x) => x.Kind == record.Kind).ToList().IndexOf(record) + 1;
			}
			return 0;
		}

		internal ChaFileClothes.PartsInfo Part(int kind, int index)
		{
			ClothingSlotRecord clothingSlotRecord = Record(kind, index);
			if (clothingSlotRecord == null)
			{
				return null;
			}
			if (!parts.TryGetValue(clothingSlotRecord.Key, out var value))
			{
				try
				{
					value = ((clothingSlotRecord.Part == null) ? new ChaFileClothes.PartsInfo() : MessagePackSerializer.Deserialize<ChaFileClothes.PartsInfo>(clothingSlotRecord.Part));
					if (value == null || value.colorInfo == null || value.colorInfo.Length < 3)
					{
						throw new InvalidDataException("Missing garment colors.");
					}
				}
				catch (Exception ex)
				{
					value = new ChaFileClothes.PartsInfo();
					clothingSlotRecord.ItemId = 0;
					clothingSlotRecord.SourceId = 0;
					clothingSlotRecord.SourceGuid = "";
					clothingSlotRecord.SourceCategory = 0;
					clothingSlotRecord.SubParts = null;
					clothingSlotRecord.Part = MessagePackSerializer.Serialize(value);
					clothingSlotRecord.Appearance = null;
					failed.Add(clothingSlotRecord.Key);
					WardrobePlugin.Log.LogWarning((object)("Invalid extra garment was cleared: " + ex.GetBaseException().Message));
				}
				parts[clothingSlotRecord.Key] = value;
			}
			return value;
		}

		internal ClothingSlotVisualKoi Visual(int kind, int index)
		{
			ClothingSlotRecord clothingSlotRecord = Record(kind, index);
			if (clothingSlotRecord == null || !visuals.TryGetValue(clothingSlotRecord.Key, out var value))
			{
				return null;
			}
			return value;
		}

		internal ClothingSlotSet Capture()
		{
			foreach (ClothingSlotRecord renderRecord in RenderRecords)
			{
				if (parts.TryGetValue(renderRecord.Key, out var value))
				{
					renderRecord.ItemId = ((renderRecord.SourceCategory == 0) ? value.id : checked(value.id + 1));
					renderRecord.Part = MessagePackSerializer.Serialize(value);
				}
				if (visuals.TryGetValue(renderRecord.Key, out var value2) && value2.Root != null)
				{
					renderRecord.Appearance = value2.Capture();
				}
			}
			ClothingSlotRecord[] array = slots.PrimaryRecords.ToArray();
			foreach (ClothingSlotRecord clothingSlotRecord in array)
			{
				if (!WardrobeCompositeTopKoi.IsPieceCategory(clothingSlotRecord.SourceCategory))
				{
					GameObject value3;
					if (((CharaCustomFunctionController)this).ChaControl.nowCoordinate.clothes.parts[clothingSlotRecord.Kind].id != ClothingSlotIdentityKoi.Resolve(clothingSlotRecord))
					{
						SetPrimary(clothingSlotRecord.Kind, null);
					}
					else if (primaryRoots.TryGetValue(clothingSlotRecord.Kind, out value3) && value3 != null && value3 == ((CharaCustomFunctionController)this).ChaControl.objClothes[clothingSlotRecord.Kind])
					{
						ClothingSlotRecord clothingSlotRecord2 = WardrobeClothingSlotsKoi.CapturePrimary(((CharaCustomFunctionController)this).ChaControl, clothingSlotRecord.Kind, clipboard: false);
						clothingSlotRecord.Part = clothingSlotRecord2.Part;
						clothingSlotRecord.Appearance = clothingSlotRecord2.Appearance;
						clothingSlotRecord.ItemId = clothingSlotRecord2.ItemId;
						clothingSlotRecord.SubParts = clothingSlotRecord2.SubParts;
					}
				}
			}
			return ClothingSlotSet.Decode(slots.Encode(prune: false));
		}

		internal void SetPrimary(int kind, ClothingSlotRecord record)
		{
			record = KoiClothingPartValidation.DetachPrimary(slots, record);
			ClothingSlotRecord primary = slots.GetPrimary(kind);
			if (primary != null)
			{
				Release(primary.Key);
			}
			slots.SetPrimary(kind, record);
			primaryRoots.Remove(kind);
			if (record != null)
			{
				if (WardrobeCompositeTopKoi.IsPieceCategory(record.SourceCategory))
				{
					rebuild = true;
					Mount();
				}
				else
				{
					MountPrimary();
				}
			}
		}

		internal void ReapplyPrimary(int kind)
		{
			primaryRoots.Remove(kind);
			MountPrimary();
		}

		private void MountPrimary()
		{
			if (((CharaCustomFunctionController)this).ChaControl == null || ((CharaCustomFunctionController)this).ChaControl.objTop == null)
			{
				return;
			}
			ClothingSlotRecord[] array = slots.PrimaryRecords.ToArray();
			foreach (ClothingSlotRecord clothingSlotRecord in array)
			{
				if (WardrobeCompositeTopKoi.IsPieceCategory(clothingSlotRecord.SourceCategory))
				{
					continue;
				}
				int kind = clothingSlotRecord.Kind;
				GameObject gameObject = ((CharaCustomFunctionController)this).ChaControl.objClothes[kind];
				if (gameObject == null)
				{
					continue;
				}
				if (primaryRoots.TryGetValue(kind, out var value))
				{
					if (!(value == gameObject))
					{
						slots.SetPrimary(kind, null);
						primaryRoots.Remove(kind);
					}
					continue;
				}
				int num = ClothingSlotIdentityKoi.Resolve(clothingSlotRecord);
				if (num >= 0 && ((CharaCustomFunctionController)this).ChaControl.nowCoordinate.clothes.parts[kind].id == num)
				{
					try
					{
						WardrobeClothingSlotsKoi.RestorePrimaryAppearance(((CharaCustomFunctionController)this).ChaControl, kind, clothingSlotRecord);
					}
					catch (Exception ex)
					{
						WardrobePlugin.Log.LogWarning((object)("Saved garment appearance could not be restored: " + ex.GetBaseException().Message));
					}
					primaryRoots[kind] = gameObject;
				}
			}
		}

		internal int Add(int kind)
		{
			ClothingSlotRecord clothingSlotRecord = slots.Add(kind);
			if (clothingSlotRecord == null)
			{
				return -1;
			}
			parts[clothingSlotRecord.Key] = new ChaFileClothes.PartsInfo();
			return slots.Count(kind);
		}

		internal void Remove(int kind, int index)
		{
			ClothingSlotRecord clothingSlotRecord = Record(kind, index);
			if (clothingSlotRecord != null)
			{
				Release(clothingSlotRecord.Key);
				failed.Remove(clothingSlotRecord.Key);
				slots.Remove(kind, index);
				RefreshMask();
			}
		}

		internal void Replace(ClothingSlotSet set)
		{
			ReleaseAll();
			failed.Clear();
			primaryRoots.Clear();
			slots = ClothingSlotSet.Decode(set.Encode(prune: false));
			parts.Clear();
			loadedCoordinateIndex = ((((CharaCustomFunctionController)this).ChaControl == null) ? (-1) : Convert.ToInt32(((CharaCustomFunctionController)this).ChaControl.fileStatus.coordinateType));
			loadedCoordinate = ((((CharaCustomFunctionController)this).ChaControl == null) ? null : ((CharaCustomFunctionController)this).ChaControl.nowCoordinate);
			rebuild = true;
			Mount();
			MountPrimary();
		}

		internal void ReplaceKind(int kind, ClothingSlotSet set)
		{
			ClothingSlotRecord[] array = slots.Items.Where((ClothingSlotRecord x) => x.Kind == kind).ToArray();
			foreach (ClothingSlotRecord clothingSlotRecord in array)
			{
				Release(clothingSlotRecord.Key);
				failed.Remove(clothingSlotRecord.Key);
			}
			slots.ReplaceKind(kind, set);
			rebuild = true;
			Mount();
			primaryRoots.Remove(kind);
			MountPrimary();
		}

		internal void Set(int kind, int index, ClothingSlotRecord source)
		{
			if (kind < 0 || kind >= 9)
			{
				return;
			}
			switch (index)
			{
			case 0:
				SetPrimary(kind, source);
				break;
			case 1:
			case 2:
			case 3:
			case 4:
			{
				ChaFileClothes.PartsInfo partsInfo = ((source == null || source.Part == null) ? new ChaFileClothes.PartsInfo() : MessagePackSerializer.Deserialize<ChaFileClothes.PartsInfo>(source.Part));
				while (slots.Count(kind) < index)
				{
					if (Add(kind) < 0)
					{
						return;
					}
				}
				ClothingSlotRecord clothingSlotRecord = Record(kind, index);
				string key = clothingSlotRecord.Key;
				Release(key);
				failed.Remove(key);
				clothingSlotRecord.ItemId = source?.ItemId ?? 0;
				clothingSlotRecord.SourceCategory = source?.SourceCategory ?? 0;
				clothingSlotRecord.SubParts = ((source == null || source.SubParts == null) ? null : source.SubParts.Select((ClothingSlotSubPart x) => x.Clone()).ToArray());
				partsInfo.id = ((clothingSlotRecord.SourceCategory == 0) ? clothingSlotRecord.ItemId : (clothingSlotRecord.ItemId - 1));
				clothingSlotRecord.SourceId = source?.SourceId ?? 0;
				clothingSlotRecord.SourceGuid = ((source == null) ? "" : (source.SourceGuid ?? ""));
				clothingSlotRecord.State = source?.State ?? 0;
				clothingSlotRecord.Part = MessagePackSerializer.Serialize(partsInfo);
				clothingSlotRecord.Appearance = ((source == null || source.Appearance == null) ? null : ((byte[])source.Appearance.Clone()));
				parts[key] = partsInfo;
				rebuild = true;
				Mount();
				break;
			}
			}
		}

		internal void SetItem(int kind, int index, int id, int category = 0)
		{
			ClothingSlotRecord clothingSlotRecord = Record(kind, index);
			if (index == 0 && (clothingSlotRecord == null || !IsManagedPrimary(kind)))
			{
				if (clothingSlotRecord != null)
				{
					Release(clothingSlotRecord.Key);
				}
				slots.SetPrimary(kind, new ClothingSlotRecord
				{
					Kind = kind
				});
				clothingSlotRecord = Record(kind, 0);
			}
			if (clothingSlotRecord != null)
			{
				ClothingSlotRecord clothingSlotRecord2 = new ClothingSlotRecord
				{
					Kind = kind,
					ItemId = id
				};
				if (category == 0)
				{
					ClothingSlotIdentityKoi.Capture(kind, id, clothingSlotRecord2);
				}
				else
				{
					ClothingSlotSubPart clothingSlotSubPart = WardrobeCompositeTopKoi.CapturePiece(category, id);
					clothingSlotRecord2.SourceGuid = clothingSlotSubPart.SourceGuid;
					clothingSlotRecord2.SourceId = clothingSlotSubPart.SourceId;
				}
				Release(clothingSlotRecord.Key);
				failed.Remove(clothingSlotRecord.Key);
				parts[clothingSlotRecord.Key] = new ChaFileClothes.PartsInfo
				{
					id = id
				};
				clothingSlotRecord.ItemId = ((category == 0) ? id : checked(id + 1));
				clothingSlotRecord.SourceCategory = category;
				clothingSlotRecord.SubParts = null;
				clothingSlotRecord.Part = null;
				clothingSlotRecord.State = 0;
				clothingSlotRecord.Appearance = null;
				clothingSlotRecord.SourceGuid = clothingSlotRecord2.SourceGuid;
				clothingSlotRecord.SourceId = clothingSlotRecord2.SourceId;
				rebuild = true;
				Mount();
			}
		}

		internal void State(int kind, int index, byte value)
		{
			ClothingSlotRecord clothingSlotRecord = Record(kind, index);
			if (clothingSlotRecord != null)
			{
				clothingSlotRecord.State = value;
				ClothingSlotVisualKoi clothingSlotVisualKoi = Visual(kind, index);
				if (clothingSlotVisualKoi != null)
				{
					clothingSlotVisualKoi.ApplyState();
					clothingSlotVisualKoi.SyncCharacterVisibility(((CharaCustomFunctionController)this).ChaControl);
				}
				RefreshMask();
			}
		}

		internal void Repaint(int kind, int index)
		{
			Visual(kind, index)?.Repaint();
		}

		internal void CaptureAppearance(int kind, int index)
		{
			ClothingSlotVisualKoi clothingSlotVisualKoi = Visual(kind, index);
			if (clothingSlotVisualKoi != null)
			{
				clothingSlotVisualKoi.Record.Appearance = clothingSlotVisualKoi.Capture();
			}
		}

		private void Mount()
		{
			if (!rebuild || ((CharaCustomFunctionController)this).ChaControl == null || ((CharaCustomFunctionController)this).ChaControl.objTop == null || ((CharaCustomFunctionController)this).ChaControl.objBodyBone == null)
			{
				return;
			}
			rebuild = false;
			skeletonRoot = ((CharaCustomFunctionController)this).ChaControl.objTop.transform;
			ClothingSlotRecord[] array = RenderRecords.ToArray();
			foreach (ClothingSlotRecord record in array)
			{
				if (record.ItemId == 0 || visuals.ContainsKey(record.Key) || failed.Contains(record.Key))
				{
					continue;
				}
				GameObject gameObject = null;
				try
				{
					int num = ClothingSlotIdentityKoi.Resolve(record);
					if (num < 0)
					{
						failed.Add(record.Key);
						WardrobePlugin.Log.LogWarning((object)"Missing clothing mod for extra garment; saved data retained.");
						continue;
					}
					if (loader == null)
					{
						throw new MissingMethodException("Native clothing loader unavailable.");
					}
					Func<int, int, Transform, GameObject> func = delegate(int category, int item, Transform parent)
					{
						List<object> list = new List<object>
						{
							(Action<ListInfoBase>)delegate
							{
							},
							((CharaCustomFunctionController)this).ChaControl.hiPoly,
							category,
							item,
							"F604_Clothing_" + record.Key + "_" + category,
							record.Kind < 2,
							(byte)1,
							parent,
							-1,
							false
						};
						return loader.Invoke(((CharaCustomFunctionController)this).ChaControl, list.ToArray()) as GameObject;
					};
					int arg = ((record.SourceCategory == 0) ? ((int)WardrobeSession.ClothesCategories[record.Kind]) : record.SourceCategory);
					gameObject = func(arg, num, skeletonRoot);
					if (gameObject == null)
					{
						throw new InvalidOperationException("Native loader did not return a garment.");
					}
					ListInfoComponent component = gameObject.GetComponent<ListInfoComponent>();
					if (component == null || component.data == null || component.data.Id != num)
					{
						throw new InvalidOperationException("The selected clothing asset could not be loaded.");
					}
					int index = IndexOf(record);
					ChaFileClothes.PartsInfo partsInfo = Part(record.Kind, index);
					partsInfo.id = num;
					record.ItemId = ((record.SourceCategory == 0) ? num : checked(num + 1));
					ClothingSlotVisualKoi clothingSlotVisualKoi = ((record.SourceCategory == 0) ? WardrobeCompositeTopKoi.Create(((CharaCustomFunctionController)this).ChaControl, gameObject, record, partsInfo, func) : WardrobeCompositeTopKoi.CreateStandalone(((CharaCustomFunctionController)this).ChaControl, gameObject, record, partsInfo, record.SourceCategory));
					visuals[record.Key] = clothingSlotVisualKoi;
					gameObject = null;
					if (record.Part == null)
					{
						for (int num2 = 0; num2 < 4; num2++)
						{
							partsInfo.colorInfo[num2] = WardrobeClothingSlotsKoi.GetDefaultColor(((CharaCustomFunctionController)this).ChaControl, record.Kind, index, num2);
						}
					}
					clothingSlotVisualKoi.Initialize(nativePrimary: false, ((CharaCustomFunctionController)this).ChaControl.hiPoly);
					clothingSlotVisualKoi.Restore(record.Appearance);
					clothingSlotVisualKoi.ApplyState();
					clothingSlotVisualKoi.SyncCharacterVisibility(((CharaCustomFunctionController)this).ChaControl);
				}
				catch (Exception ex)
				{
					if (gameObject != null)
					{
						UnityEngine.Object.Destroy(gameObject);
					}
					Release(record.Key);
					failed.Add(record.Key);
					WardrobePlugin.Log.LogWarning((object)("Extra clothing load failed: " + ex.GetBaseException().Message));
				}
			}
		}

		protected override void OnReload(GameMode currentGameMode, bool maintainState)
		{
			if (!maintainState)
			{
				ChaFileCoordinate[] coordinate = ((CharaCustomFunctionController)this).ChaControl.chaFile.coordinate;
				int num = Convert.ToInt32(((CharaCustomFunctionController)this).ChaControl.fileStatus.coordinateType);
				ClothingSlotSet[] array;
				try
				{
					array = WardrobeClothingSlotsKoi.CardCoordinates(((CharaCustomFunctionController)this).GetExtendedData(), ((CharaCustomFunctionController)this).ChaControl.chaFile);
				}
				catch (Exception ex)
				{
					WardrobePlugin.Log.LogWarning((object)("Extra clothing card map failed to load; previous extras cleared: " + ex.GetBaseException().Message));
					array = new ClothingSlotSet[(coordinate != null) ? coordinate.Length : 0];
					for (int i = 0; i < array.Length; i++)
					{
						array[i] = new ClothingSlotSet();
					}
				}
				for (int j = 0; j < array.Length; j++)
				{
					WardrobeClothingSlotsKoi.Attach(coordinate[j], array[j]);
				}
				ClothingSlotSet set = ((num >= 0 && num < array.Length) ? array[num] : new ClothingSlotSet());
				WardrobeClothingSlotsKoi.Attach(((CharaCustomFunctionController)this).ChaControl.nowCoordinate, set);
				Replace(set);
			}
			else
			{
				Capture();
				ReleaseAll();
				primaryRoots.Clear();
				parts.Clear();
				rebuild = true;
			}
		}

		protected override void OnCoordinateBeingLoaded(ChaFileCoordinate coordinate, bool maintainState)
		{
			CoordinateLoadFlags coordinateLoadFlags = MakerAPI.GetCoordinateLoadFlags();
			if (coordinateLoadFlags != null && !coordinateLoadFlags.Clothes)
			{
				return;
			}
			if (maintainState && coordinate == ((CharaCustomFunctionController)this).ChaControl.nowCoordinate && loadedCoordinateIndex == Convert.ToInt32(((CharaCustomFunctionController)this).ChaControl.fileStatus.coordinateType))
			{
				Capture();
				ReleaseAll();
				failed.Clear();
				primaryRoots.Clear();
				parts.Clear();
				rebuild = true;
				return;
			}
			int incomingIndex = Convert.ToInt32(((CharaCustomFunctionController)this).ChaControl.fileStatus.coordinateType);
			ClothingSlotSet set = KoiClothingPartValidation.LoadOrEmpty(delegate
			{
				ChaFileCoordinate[] coordinate2 = ((CharaCustomFunctionController)this).ChaControl.chaFile.coordinate;
				if (coordinate2 != null && loadedCoordinateIndex >= 0 && loadedCoordinateIndex < coordinate2.Length && loadedCoordinateIndex != incomingIndex)
				{
					WardrobeClothingSlotsKoi.Attach(coordinate2[loadedCoordinateIndex], Capture());
				}
				return (KoiClothingCoordinateMap.UseSavedEntry(coordinate == ((CharaCustomFunctionController)this).ChaControl.nowCoordinate, incomingIndex, loadedCoordinateIndex) && coordinate2 != null && incomingIndex >= 0 && incomingIndex < coordinate2.Length) ? WardrobeClothingSlotsKoi.Read(coordinate2[incomingIndex]) : WardrobeClothingSlotsKoi.Read(coordinate);
			}, delegate(Exception error)
			{
				WardrobePlugin.Log.LogWarning((object)("Extra clothing coordinate failed; previous extras cleared: " + error.GetBaseException().Message));
			});
			WardrobeClothingSlotsKoi.Attach(((CharaCustomFunctionController)this).ChaControl.nowCoordinate, set);
			Replace(set);
		}

		protected override void OnCardBeingSaved(GameMode currentGameMode)
		{
			ClothingSlotSet clothingSlotSet = Capture();
			((CharaCustomFunctionController)this).SetExtendedData(WardrobeClothingSlotsKoi.CardData(((CharaCustomFunctionController)this).ChaControl.chaFile, Convert.ToInt32(((CharaCustomFunctionController)this).ChaControl.fileStatus.coordinateType), clothingSlotSet));
			WardrobeClothingSlotsKoi.Attach(((CharaCustomFunctionController)this).ChaControl.nowCoordinate, clothingSlotSet, prune: true);
		}

		protected override void OnCoordinateBeingSaved(ChaFileCoordinate coordinate)
		{
			((CharaCustomFunctionController)this).SetCoordinateExtendedData(coordinate, WardrobeClothingSlotsKoi.Data((coordinate == ((CharaCustomFunctionController)this).ChaControl.nowCoordinate) ? Capture() : WardrobeClothingSlotsKoi.Read(coordinate), prune: true));
		}

		private void LateUpdate()
		{
			if (((CharaCustomFunctionController)this).ChaControl == null)
			{
				return;
			}
			int coordinate = Convert.ToInt32(((CharaCustomFunctionController)this).ChaControl.fileStatus.coordinateType);
			if (coordinate == loadedCoordinateIndex)
			{
				ClothingSlotRecord[] array = slots.PrimaryRecords.ToArray();
				foreach (ClothingSlotRecord clothingSlotRecord in array)
				{
					if (WardrobeCompositeTopKoi.IsPieceCategory(clothingSlotRecord.SourceCategory) && ((CharaCustomFunctionController)this).ChaControl.nowCoordinate.clothes.parts[clothingSlotRecord.Kind].id != 0)
					{
						SetPrimary(clothingSlotRecord.Kind, null);
					}
				}
			}
			if (coordinate != loadedCoordinateIndex && ((CharaCustomFunctionController)this).ChaControl.nowCoordinate != null)
			{
				ClothingSlotSet set = KoiClothingPartValidation.LoadOrEmpty(delegate
				{
					ChaFileCoordinate[] saved = ((CharaCustomFunctionController)this).ChaControl.chaFile.coordinate;
					if (saved == null || coordinate < 0 || coordinate >= saved.Length)
					{
						throw new InvalidDataException("Invalid clothing coordinate index.");
					}
					return KoiClothingPartValidation.SwitchCoordinate(loadedCoordinateIndex, coordinate, () => Capture(), delegate(int num, ClothingSlotSet set2)
					{
						if (num < saved.Length)
						{
							WardrobeClothingSlotsKoi.Attach(saved[num], set2);
						}
					}, (int num) => WardrobeClothingSlotsKoi.Read(saved[num]));
				}, delegate(Exception error)
				{
					WardrobePlugin.Log.LogWarning((object)("Extra clothing coordinate switch failed; previous extras cleared: " + error.GetBaseException().Message));
				});
				WardrobeClothingSlotsKoi.Attach(((CharaCustomFunctionController)this).ChaControl.nowCoordinate, set);
				Replace(set);
			}
			if (skeletonRoot != null && (((CharaCustomFunctionController)this).ChaControl.objTop == null || skeletonRoot != ((CharaCustomFunctionController)this).ChaControl.objTop.transform))
			{
				Capture();
				ReleaseAll();
				failed.Clear();
				primaryRoots.Clear();
				rebuild = true;
			}
			Mount();
			MountPrimary();
			foreach (ClothingSlotVisualKoi value in visuals.Values)
			{
				value.SyncCharacterVisibility(((CharaCustomFunctionController)this).ChaControl);
			}
			RefreshMask();
		}

		protected override void OnDestroy()
		{
			ReleaseAll();
			((CharaCustomFunctionController)this).OnDestroy();
		}

		private void Release(string key)
		{
			if (visuals.TryGetValue(key, out var value))
			{
				value.Dispose();
				visuals.Remove(key);
			}
			parts.Remove(key);
		}

		private void ReleaseAll()
		{
			RestoreMask();
			foreach (ClothingSlotVisualKoi value in visuals.Values)
			{
				value.Dispose();
			}
			visuals.Clear();
			skeletonRoot = null;
		}

		private void RestoreMask()
		{
			foreach (BraMaskLease braMask in braMasks)
			{
				ReleaseBraMask(braMask);
			}
			braMasks.Clear();
			RestoreBodyMask();
		}

		private void RestoreBodyMask()
		{
			if (maskMaterial != null && combinedMask != null && maskMaterial.GetTexture(ChaShader._AlphaMask) == combinedMask)
			{
				maskMaterial.SetTexture(ChaShader._AlphaMask, baseMask);
			}
			if (combinedMask != null)
			{
				UnityEngine.Object.Destroy(combinedMask);
			}
			combinedMask = null;
			baseMask = null;
			maskMaterial = null;
			maskSignature = null;
			maskFailureSignature = null;
		}

		private void RefreshMask()
		{
			RefreshBodyMask();
			RefreshBraMasks();
		}

		private void RefreshBodyMask()
		{
			if (((CharaCustomFunctionController)this).ChaControl == null)
			{
				return;
			}
			Material customMatBody = ((CharaCustomFunctionController)this).ChaControl.customMatBody;
			if (customMatBody != maskMaterial)
			{
				RestoreBodyMask();
				maskMaterial = customMatBody;
			}
			if (customMatBody == null || !customMatBody.HasProperty(ChaShader._AlphaMask))
			{
				return;
			}
			Texture texture = customMatBody.GetTexture(ChaShader._AlphaMask);
			if (texture != combinedMask && texture != baseMask)
			{
				if (combinedMask != null)
				{
					UnityEngine.Object.Destroy(combinedMask);
				}
				combinedMask = null;
				maskSignature = null;
				maskFailureSignature = null;
				baseMask = texture;
			}
			List<Texture> list = (from x in visuals.Values
				where x.Root != null && x.Record != null && x.Record.Kind == 0 && x.Record.State == 0 && x.BodyMask != null
				orderby x.Record.Key
				select x.BodyMask).ToList();
			if (list.Count == 0)
			{
				if (combinedMask != null)
				{
					if (texture == combinedMask)
					{
						customMatBody.SetTexture(ChaShader._AlphaMask, baseMask);
					}
					UnityEngine.Object.Destroy(combinedMask);
					combinedMask = null;
				}
				maskSignature = null;
				maskFailureSignature = null;
				return;
			}
			if (baseMask != null)
			{
				list.Insert(0, baseMask);
			}
			string text = string.Join(",", list.Select((Texture x) => x.GetInstanceID().ToString()).ToArray());
			if (text == maskSignature && combinedMask != null)
			{
				if (texture != combinedMask)
				{
					customMatBody.SetTexture(ChaShader._AlphaMask, combinedMask);
				}
			}
			else
			{
				if (text == maskFailureSignature)
				{
					return;
				}
				try
				{
					Texture2D value = CombineMasks(list);
					if (combinedMask != null)
					{
						UnityEngine.Object.Destroy(combinedMask);
					}
					combinedMask = value;
					customMatBody.SetTexture(ChaShader._AlphaMask, value);
					maskSignature = text;
					maskFailureSignature = null;
				}
				catch (Exception ex)
				{
					if (combinedMask != null)
					{
						if (customMatBody.GetTexture(ChaShader._AlphaMask) == combinedMask)
						{
							customMatBody.SetTexture(ChaShader._AlphaMask, baseMask);
						}
						UnityEngine.Object.Destroy(combinedMask);
						combinedMask = null;
					}
					maskSignature = null;
					maskFailureSignature = text;
					WardrobePlugin.Log.LogWarning((object)("Extra clothing body mask could not be combined: " + ex.GetBaseException().Message));
				}
			}
		}

		private static Texture2D CombineMasks(List<Texture> masks)
		{
			int num = masks.Max((Texture x) => x.width);
			int num2 = masks.Max((Texture x) => x.height);
			if (num < 1 || num2 < 1 || num > 4096 || num2 > 4096)
			{
				throw new InvalidDataException("Clothing mask dimensions are invalid.");
			}
			Color[] array = Enumerable.Repeat(Color.white, num * num2).ToArray();
			foreach (Texture mask in masks)
			{
				RenderTexture active = RenderTexture.active;
				RenderTexture renderTexture = null;
				Texture2D texture2D = null;
				try
				{
					renderTexture = RenderTexture.GetTemporary(num, num2, 0, RenderTextureFormat.ARGB32);
					Graphics.Blit(mask, renderTexture);
					RenderTexture.active = renderTexture;
					texture2D = new Texture2D(num, num2, TextureFormat.RGBA32, mipChain: false, linear: true);
					texture2D.ReadPixels(new Rect(0f, 0f, num, num2), 0, 0);
					texture2D.Apply();
					Color[] pixels = texture2D.GetPixels();
					for (int num3 = 0; num3 < array.Length; num3++)
					{
						array[num3] = new Color(Mathf.Min(array[num3].r, pixels[num3].r), Mathf.Min(array[num3].g, pixels[num3].g), Mathf.Min(array[num3].b, pixels[num3].b), Mathf.Min(array[num3].a, pixels[num3].a));
					}
				}
				finally
				{
					RenderTexture.active = active;
					if (renderTexture != null)
					{
						RenderTexture.ReleaseTemporary(renderTexture);
					}
					if (texture2D != null)
					{
						UnityEngine.Object.Destroy(texture2D);
					}
				}
			}
			Texture2D texture2D2 = null;
			try
			{
				texture2D2 = new Texture2D(num, num2, TextureFormat.RGBA32, mipChain: false, linear: true);
				texture2D2.SetPixels(array);
				texture2D2.Apply();
				return texture2D2;
			}
			catch
			{
				if (texture2D2 != null)
				{
					UnityEngine.Object.Destroy(texture2D2);
				}
				throw;
			}
		}

		private static void ReleaseBraMask(BraMaskLease lease)
		{
			if (lease.Material != null && lease.Combined != null && lease.Material.GetTexture(ChaShader._AlphaMask) == lease.Combined)
			{
				lease.Material.SetTexture(ChaShader._AlphaMask, lease.Baseline);
			}
			if (lease.Combined != null)
			{
				UnityEngine.Object.Destroy(lease.Combined);
			}
			lease.Combined = null;
			lease.Signature = (lease.FailedSignature = null);
		}

		private void RefreshBraMasks()
		{
			if (((CharaCustomFunctionController)this).ChaControl == null)
			{
				return;
			}
			List<Texture> list = (from x in visuals.Values
				where x.Root != null && x.Record != null && x.Record.Kind == 0 && x.Record.State == 0 && x.BraMask != null
				orderby x.Record.Key
				select x.BraMask).ToList();
			if (list.Count == 0 || ((CharaCustomFunctionController)this).ChaControl.rendBra == null)
			{
				foreach (BraMaskLease braMask in braMasks)
				{
					ReleaseBraMask(braMask);
				}
				braMasks.Clear();
				return;
			}
			List<Material> list2 = new List<Material>();
			Renderer[] rendBra = ((CharaCustomFunctionController)this).ChaControl.rendBra;
			foreach (Renderer renderer in rendBra)
			{
				if (!(renderer == null))
				{
					Material material = renderer.material;
					if (material != null && material.HasProperty(ChaShader._AlphaMask) && !list2.Contains(material))
					{
						list2.Add(material);
					}
				}
			}
			BraMaskLease[] array = braMasks.ToArray();
			foreach (BraMaskLease braMaskLease in array)
			{
				if (!list2.Contains(braMaskLease.Material))
				{
					ReleaseBraMask(braMaskLease);
					braMasks.Remove(braMaskLease);
				}
			}
			foreach (Material material2 in list2)
			{
				BraMaskLease braMaskLease2 = braMasks.FirstOrDefault((BraMaskLease x) => x.Material == material2);
				if (braMaskLease2 == null)
				{
					braMaskLease2 = new BraMaskLease
					{
						Material = material2,
						Baseline = material2.GetTexture(ChaShader._AlphaMask)
					};
					braMasks.Add(braMaskLease2);
				}
				Texture texture = material2.GetTexture(ChaShader._AlphaMask);
				if (texture != braMaskLease2.Combined && texture != braMaskLease2.Baseline)
				{
					if (braMaskLease2.Combined != null)
					{
						UnityEngine.Object.Destroy(braMaskLease2.Combined);
					}
					braMaskLease2.Combined = null;
					braMaskLease2.Signature = (braMaskLease2.FailedSignature = null);
					braMaskLease2.Baseline = texture;
				}
				List<Texture> list3 = new List<Texture>();
				if (braMaskLease2.Baseline != null)
				{
					list3.Add(braMaskLease2.Baseline);
				}
				list3.AddRange(list);
				string text = string.Join(",", list3.Select((Texture x) => x.GetInstanceID().ToString()).ToArray());
				if (text == braMaskLease2.Signature && braMaskLease2.Combined != null)
				{
					if (texture != braMaskLease2.Combined)
					{
						material2.SetTexture(ChaShader._AlphaMask, braMaskLease2.Combined);
					}
				}
				else
				{
					if (text == braMaskLease2.FailedSignature)
					{
						continue;
					}
					try
					{
						Texture2D texture2D = CombineMasks(list3);
						if (braMaskLease2.Combined != null)
						{
							UnityEngine.Object.Destroy(braMaskLease2.Combined);
						}
						braMaskLease2.Combined = texture2D;
						braMaskLease2.Signature = text;
						braMaskLease2.FailedSignature = null;
						material2.SetTexture(ChaShader._AlphaMask, texture2D);
					}
					catch (Exception ex)
					{
						ReleaseBraMask(braMaskLease2);
						braMaskLease2.FailedSignature = text;
						WardrobePlugin.Log.LogWarning((object)("Extra clothing bra mask could not be combined: " + ex.GetBaseException().Message));
					}
				}
			}
		}
	}
	internal static class ClothingSlotIdentityKoi
	{
		private static Type resolver;

		private static Type Resolver => resolver ?? (resolver = (from x in AppDomain.CurrentDomain.GetAssemblies()
			select x.GetType("Sideloader.AutoResolver.UniversalAutoResolver", throwOnError: false)).FirstOrDefault((Type x) => x != null));

		private static object Member(object instance, string name)
		{
			Type type = instance.GetType();
			FieldInfo field = type.GetField(name);
			if (field != null)
			{
				return field.GetValue(instance);
			}
			PropertyInfo property = type.GetProperty(name);
			if (!(property == null))
			{
				return property.GetValue(instance, null);
			}
			return null;
		}

		internal static void Capture(int kind, int id, ClothingSlotRecord record)
		{
			record.SourceGuid = "";
			record.SourceId = id;
			if (id >= 100000000)
			{
				if (Resolver == null)
				{
					throw new InvalidOperationException("Sideloader is required to identify this garment.");
				}
				MethodInfo methodInfo = Resolver.GetMethods(BindingFlags.Static | BindingFlags.Public).First((MethodInfo x) => x.Name == "TryGetResolutionInfo" && x.GetParameters().Length == 2 && x.GetParameters()[0].ParameterType.IsEnum);
				object obj = methodInfo.Invoke(null, new object[2]
				{
					Enum.ToObject(methodInfo.GetParameters()[0].ParameterType, (int)WardrobeSession.ClothesCategories[kind]),
					id
				});
				if (obj == null)
				{
					throw new InvalidOperationException("Could not identify this garment's source mod.");
				}
				record.SourceGuid = (string)Member(obj, "GUID");
				record.SourceId = Convert.ToInt32(Member(obj, "Slot"));
			}
		}

		internal static int Resolve(ClothingSlotRecord record)
		{
			if (record.SourceCategory != 0)
			{
				return WardrobeCompositeTopKoi.ResolvePiece(record.SourceCategory, new ClothingSlotSubPart
				{
					ItemId = record.ItemId - 1,
					SourceId = record.SourceId,
					SourceGuid = record.SourceGuid
				});
			}
			if (string.IsNullOrEmpty(record.SourceGuid))
			{
				return record.ItemId;
			}
			if (Resolver == null)
			{
				return -1;
			}
			MethodInfo methodInfo = Resolver.GetMethods(BindingFlags.Static | BindingFlags.Public).First((MethodInfo x) => x.Name == "TryGetResolutionInfo" && x.GetParameters().Length == 3 && x.GetParameters()[1].ParameterType.IsEnum);
			object obj = methodInfo.Invoke(null, new object[3]
			{
				record.SourceId,
				Enum.ToObject(methodInfo.GetParameters()[1].ParameterType, (int)WardrobeSession.ClothesCategories[record.Kind]),
				record.SourceGuid
			});
			if (obj != null)
			{
				return Convert.ToInt32(Member(obj, "LocalSlot"));
			}
			return -1;
		}
	}
	internal sealed class ClothingPrimaryAppearanceOwnerKoi : MonoBehaviour
	{
		private ClothingSlotVisualKoi visual;

		private ClothingSlotVisualKoi Ensure(ChaControl cha, int kind, ClothingSlotRecord record)
		{
			if (visual == null)
			{
				visual = new ClothingSlotVisualKoi
				{
					Root = base.gameObject,
					Component = cha.cusClothesCmp[kind],
					Part = cha.nowCoordinate.clothes.parts[kind],
					Record = record
				};
				visual.Initialize(nativePrimary: true);
			}
			else
			{
				visual.Component = cha.cusClothesCmp[kind];
				visual.Part = cha.nowCoordinate.clothes.parts[kind];
				visual.Record = record;
			}
			return visual;
		}

		internal byte[] Capture(ChaControl cha, int kind, ClothingSlotRecord record, Func<Texture, bool> exclude)
		{
			return Ensure(cha, kind, record).Capture(exclude);
		}

		internal void Apply(ChaControl cha, int kind, ClothingSlotRecord record)
		{
			Ensure(cha, kind, record).Restore(record.Appearance);
		}

		private void OnDestroy()
		{
			if (visual != null)
			{
				visual.Dispose(destroyRoot: false);
			}
		}
	}
	internal static class KoiClothingPartValidation
	{
		internal static ClothingSlotRecord DetachPrimary(ClothingSlotSet set, ClothingSlotRecord source)
		{
			if (source == null)
			{
				return null;
			}
			ClothingSlotRecord clothingSlotRecord = source.Clone();
			foreach (ClothingSlotRecord item in set.Items)
			{
				if (item.Key == clothingSlotRecord.Key)
				{
					clothingSlotRecord.Key = Guid.NewGuid().ToString("N");
					break;
				}
			}
			foreach (ClothingSlotRecord primaryRecord in set.PrimaryRecords)
			{
				if (primaryRecord.Kind != clothingSlotRecord.Kind && primaryRecord.Key == clothingSlotRecord.Key)
				{
					clothingSlotRecord.Key = Guid.NewGuid().ToString("N");
					break;
				}
			}
			return clothingSlotRecord;
		}

		internal static ClothingSlotSet LoadOrEmpty(Func<ClothingSlotSet> load, Action<Exception> report)
		{
			try
			{
				return load();
			}
			catch (Exception obj)
			{
				report(obj);
				return new ClothingSlotSet();
			}
		}

		internal static ClothingSlotSet SwitchCoordinate(int outgoing, int incoming, Func<ClothingSlotSet> capture, Action<int, ClothingSlotSet> store, Func<int, ClothingSlotSet> read)
		{
			if (outgoing >= 0 && outgoing != incoming)
			{
				store(outgoing, capture());
			}
			return read(incoming);
		}

		internal static void Sanitize(ClothingSlotSet set, Func<byte[], bool> valid, Func<byte[]> empty, Action<Exception> report)
		{
			foreach (ClothingSlotRecord item in set.Items.Concat(set.PrimaryRecords))
			{
				if (item.Part == null)
				{
					continue;
				}
				try
				{
					if (valid(item.Part))
					{
						continue;
					}
					throw new InvalidOperationException("Missing garment color data.");
				}
				catch (Exception obj)
				{
					item.ItemId = 0;
					item.SourceId = 0;
					item.SourceGuid = "";
					item.SourceCategory = 0;
					item.SubParts = null;
					item.Appearance = null;
					item.Part = empty();
					report(obj);
				}
			}
		}
	}
	internal static class KoiClothingState
	{
		internal static byte ToUi(byte native)
		{
			if (native != 3)
			{
				return (native != 0) ? ((byte)1) : ((byte)0);
			}
			return 2;
		}

		internal static byte ToNative(byte ui, bool supportsHalf)
		{
			if (ui != 2)
			{
				if (!(ui == 1 && supportsHalf))
				{
					return 0;
				}
				return 1;
			}
			return 3;
		}
	}
	internal static class WardrobeClothingTransfer
	{
		private const string Id = "com.freelancer604.wardrobe.clothingtransfer";

		private const string WeightsId = "com.freelancer604.makeup.clothingblendshapes";

		private static Type ControllerType
		{
			get
			{
				if (!Chainloader.PluginInfos.TryGetValue("com.freelancer604.makeup", out var value) || !((UnityEngine.Object)(object)value.Instance != null))
				{
					return null;
				}
				return ((object)value.Instance).GetType().Assembly.GetType("Freelancer604.MakeUp.ClothingBlendshapeController");
			}
		}

		private static object Controller(ChaControl cha)
		{
			Type controllerType = ControllerType;
			if (!(controllerType == null) && !(cha == null))
			{
				return cha.GetComponent(controllerType);
			}
			return null;
		}

		internal static byte[] Capture(ChaControl cha)
		{
			object obj = Controller(cha);
			MethodInfo methodInfo = obj?.GetType().GetMethod("CaptureCoordinateBytes");
			return Pack((methodInfo == null) ? null : ((byte[])methodInfo.Invoke(obj, null)), WardrobeClothingGeometry.Capture(cha));
		}

		internal static byte[] FromCharacter(ChaFileControl card, int coordinate)
		{
			Type controllerType = ControllerType;
			MethodInfo methodInfo = ((controllerType == null) ? null : controllerType.GetMethod("ExtractCharacterCoordinateBytes"));
			byte[] array = ((methodInfo == null) ? null : ((byte[])methodInfo.Invoke(null, new object[2]
			{
				ExtendedSave.GetExtendedDataById((ChaFile)card, "com.freelancer604.makeup.clothingblendshapes"),
				coordinate
			})));
			return Pack(array, WardrobeClothingGeometry.FromCharacter(card, coordinate, array));
		}

		internal static void WriteLive(ChaControl cha)
		{
			Attach(cha.nowCoordinate, Capture(cha));
		}

		internal static byte[] Read(ChaFileCoordinate coordinate)
		{
			PluginData extendedDataById = ExtendedSave.GetExtendedDataById(coordinate, "com.freelancer604.wardrobe.clothingtransfer");
			byte[] weights = null;
			byte[] geometry = null;
			if (extendedDataById != null && extendedDataById.data.TryGetValue("payload", out var value))
			{
				Unpack(value as byte[], out weights, out geometry);
			}
			extendedDataById = ExtendedSave.GetExtendedDataById(coordinate, "com.freelancer604.makeup.clothingblendshapes");
			if (extendedDataById != null && extendedDataById.data.TryGetValue("clothingWeights", out value))
			{
				weights = value as byte[];
			}
			return Pack(weights, geometry);
		}

		internal static void Attach(ChaFileCoordinate coordinate, byte[] payload)
		{
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Expected O, but got Unknown
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Expected O, but got Unknown
			Unpack(payload, out var weights, out var _);
			PluginData val = new PluginData
			{
				version = 1
			};
			val.data["payload"] = payload;
			ExtendedSave.SetExtendedDataById(coordinate, "com.freelancer604.wardrobe.clothingtransfer", (payload == null) ? null : val);
			PluginData val2 = new PluginData
			{
				version = ((weights != null && weights.Length >= 4) ? BitConverter.ToInt32(weights, 0) : 3)
			};
			val2.data["clothingWeights"] = weights;
			ExtendedSave.SetExtendedDataById(coordinate, "com.freelancer604.makeup.clothingblendshapes", (weights == null) ? null : val2);
		}

		internal static void Apply(ChaControl cha, ChaFileCoordinate source, int slot = -1)
		{
			Unpack(Read(source), out var weights, out var geometry);
			WardrobeClothingGeometry.Apply(cha, geometry, slot);
			object obj = Controller(cha);
			MethodInfo methodInfo = obj?.GetType().GetMethod("ApplyCoordinateBytes");
			if (methodInfo != null)
			{
				methodInfo.Invoke(obj, new object[2] { weights, slot });
			}
		}

		internal static void RestoreGeometry(ChaControl cha, ChaFileCoordinate source)
		{
			Unpack(Read(source), out var _, out var geometry);
			WardrobeClothingGeometry.Apply(cha, geometry);
		}

		private static byte[] Pack(byte[] weights, byte[] geometry)
		{
			if (weights == null && geometry == null)
			{
				return null;
			}
			using MemoryStream memoryStream = new MemoryStream();
			using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
			binaryWriter.Write(1);
			binaryWriter.Write((weights != null) ? weights.Length : 0);
			if (weights != null)
			{
				binaryWriter.Write(weights);
			}
			binaryWriter.Write((geometry != null) ? geometry.Length : 0);
			if (geometry != null)
			{
				binaryWriter.Write(geometry);
			}
			return memoryStream.ToArray();
		}

		private static void Unpack(byte[] payload, out byte[] weights, out byte[] geometry)
		{
			weights = null;
			geometry = null;
			if (payload == null)
			{
				return;
			}
			using MemoryStream memoryStream = new MemoryStream(payload, writable: false);
			using BinaryReader binaryReader = new BinaryReader(memoryStream);
			if (binaryReader.ReadInt32() != 1)
			{
				throw new InvalidDataException("Unsupported clothing transfer version.");
			}
			weights = ReadSegment(binaryReader);
			geometry = ReadSegment(binaryReader);
			if (memoryStream.Position != memoryStream.Length)
			{
				throw new InvalidDataException("Trailing clothing transfer data.");
			}
		}

		private static byte[] ReadSegment(BinaryReader reader)
		{
			int num = reader.ReadInt32();
			if (num < 0 || num > reader.BaseStream.Length - reader.BaseStream.Position)
			{
				throw new InvalidDataException("Invalid clothing transfer length.");
			}
			if (num != 0)
			{
				return reader.ReadBytes(num);
			}
			return null;
		}
	}
	internal sealed class WardrobeColor4LayoutStateKoi
	{
		internal struct Presence
		{
			internal bool Visible;

			internal bool Gloss;

			internal bool Metallic;
		}

		private int actorId;

		private int rootId;

		private string signature;

		private bool initialized;

		private Presence presence;

		internal void Refresh(bool isLayout, int actor, int root, string materialSignature, Func<Presence> probe)
		{
			if (isLayout && (!initialized || actorId != actor || rootId != root || !(signature == materialSignature)))
			{
				Presence presence = probe();
				actorId = actor;
				rootId = root;
				signature = materialSignature;
				this.presence = presence;
				initialized = true;
			}
		}

		internal bool VisibleFor(int actor, int root)
		{
			if (initialized)
			{
				return presence.Visible;
			}
			return false;
		}

		internal bool GlossFor(int actor, int root)
		{
			if (VisibleFor(actor, root))
			{
				return presence.Gloss;
			}
			return false;
		}

		internal bool MetallicFor(int actor, int root)
		{
			if (VisibleFor(actor, root))
			{
				return presence.Metallic;
			}
			return false;
		}
	}
	internal static class WardrobeColorClipboard
	{
		private static int count;

		private static readonly Color[] colors = new Color[4];

		private static bool hasShaderFourth;

		private static WardrobeMaterialColor4Koi.Value shaderFourth;

		internal static bool HasData => count > 0;

		internal static void CopyFromClothes(ChaFileClothes.PartsInfo part)
		{
			if (part == null || part.colorInfo == null)
			{
				count = 0;
				hasShaderFourth = false;
				return;
			}
			int num = Math.Min(3, part.colorInfo.Length);
			for (int i = 0; i < num; i++)
			{
				colors[i] = part.colorInfo[i].baseColor;
			}
			count = num;
			hasShaderFourth = false;
		}

		internal static void CopyFromClothes(ChaControl cha, int kind, ChaFileClothes.PartsInfo part)
		{
			CopyFromClothes(cha, kind, 0, part);
		}

		internal static void CopyFromClothes(ChaControl cha, int kind, int index, ChaFileClothes.PartsInfo part)
		{
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = WardrobeClothingSlotsKoi.Controller(cha);
			if (index == 0 && (UnityEngine.Object)(object)clothingSlotsControllerKoi != null && clothingSlotsControllerKoi.Visual(kind, 0) != null)
			{
				part = clothingSlotsControllerKoi.Part(kind, 0);
			}
			CopyFromClothes(part);
			if (WardrobeCompositeTopKoi.UsesNativeFourth(cha, kind, index) && part != null && part.colorInfo != null && part.colorInfo.Length >= 4)
			{
				colors[3] = part.colorInfo[3].baseColor;
				if (IsStandaloneThirdPiece(cha, kind, index))
				{
					colors[0] = colors[3];
				}
				count = 4;
			}
			else
			{
				hasShaderFourth = WardrobeMaterialColor4Koi.TryReadClothing(cha, kind, index, out shaderFourth);
			}
		}

		internal static void PasteToClothes(ChaFileClothes.PartsInfo part)
		{
			if (part != null && part.colorInfo != null)
			{
				for (int i = 0; i < Math.Min(3, Math.Min(count, part.colorInfo.Length)); i++)
				{
					part.colorInfo[i].baseColor = colors[i];
				}
			}
		}

		internal static void PasteToClothes(ChaControl cha, int kind, int index, ChaFileClothes.PartsInfo part)
		{
			if (count == 0 || part == null)
			{
				return;
			}
			bool flag = IsStandaloneThirdPiece(cha, kind, index);
			if (flag)
			{
				if (part.colorInfo != null)
				{
					for (int i = 1; i < Math.Min(3, Math.Min(count, part.colorInfo.Length)); i++)
					{
						part.colorInfo[i].baseColor = colors[i];
					}
				}
			}
			else
			{
				PasteToClothes(part);
			}
			bool num = WardrobeCompositeTopKoi.UsesNativeFourth(cha, kind, index);
			if (num && part.colorInfo != null && part.colorInfo.Length >= 4 && (flag || count >= 4 || hasShaderFourth))
			{
				part.colorInfo[3].baseColor = (flag ? colors[0] : ((count >= 4) ? colors[3] : shaderFourth.Color));
			}
			WardrobeClothingSlotsKoi.Repaint(cha, kind, index);
			if (!num && (hasShaderFourth || count >= 4))
			{
				WardrobeMaterialColor4Koi.ApplyClothing(cha, kind, index, FourthForMaterial());
			}
		}

		private static bool IsStandaloneThirdPiece(ChaControl cha, int kind, int index)
		{
			if (cha == null || kind != 0)
			{
				return false;
			}
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = WardrobeClothingSlotsKoi.Controller(cha);
			ClothingSlotRecord clothingSlotRecord = (((UnityEngine.Object)(object)clothingSlotsControllerKoi == null) ? null : clothingSlotsControllerKoi.Record(kind, index));
			if (clothingSlotRecord != null)
			{
				if (clothingSlotRecord.SourceCategory != 202)
				{
					return clothingSlotRecord.SourceCategory == 212;
				}
				return true;
			}
			return false;
		}

		internal static void PasteToClothes(ChaControl cha, int slot)
		{
			if (count == 0 || cha == null)
			{
				return;
			}
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = WardrobeClothingSlotsKoi.Controller(cha);
			if ((UnityEngine.Object)(object)clothingSlotsControllerKoi != null && clothingSlotsControllerKoi.Visual(slot, 0) != null)
			{
				PasteToClothes(cha, slot, 0, clothingSlotsControllerKoi.Part(slot, 0));
				return;
			}
			ChaFileClothes.PartsInfo.ColorInfo[] colorInfo = cha.nowCoordinate.clothes.parts[slot].colorInfo;
			ChaFileClothes.PartsInfo.ColorInfo[] colorInfo2 = WardrobeCompat.FileCoord(cha).clothes.parts[slot].colorInfo;
			if (colorInfo != null && colorInfo2 != null)
			{
				int num = Math.Min(3, Math.Min(count, Math.Min(colorInfo.Length, colorInfo2.Length)));
				for (int i = 0; i < num; i++)
				{
					colorInfo[i].baseColor = colors[i];
					colorInfo2[i].baseColor = colors[i];
				}
				bool num2 = WardrobeCompositeTopKoi.UsesNativeFourth(cha, slot, 0);
				if (num2 && colorInfo.Length >= 4 && colorInfo2.Length >= 4 && (count >= 4 || hasShaderFourth))
				{
					ChaFileClothes.PartsInfo.ColorInfo obj = colorInfo[3];
					Color baseColor = (colorInfo2[3].baseColor = ((count >= 4) ? colors[3] : shaderFourth.Color));
					obj.baseColor = baseColor;
				}
				WardrobeClothingSlotsKoi.Repaint(cha, slot, 0);
				if (!num2 && (hasShaderFourth || count >= 4))
				{
					WardrobeMaterialColor4Koi.ApplyClothing(cha, slot, FourthForMaterial());
				}
			}
		}

		internal static void CopyFromAccessory(ChaFileAccessory.PartsInfo part)
		{
			if (part == null || part.color == null)
			{
				count = 0;
				hasShaderFourth = false;
				return;
			}
			int num = Math.Min(colors.Length, part.color.Length);
			for (int i = 0; i < num; i++)
			{
				colors[i] = part.color[i];
			}
			count = num;
			hasShaderFourth = false;
		}

		internal static void CopyFromAccessory(ChaControl cha, int slot, ChaFileAccessory.PartsInfo part)
		{
			CopyFromAccessory(part);
			hasShaderFourth = WardrobeMaterialColor4Koi.TryReadAccessory(cha, slot, out shaderFourth);
		}

		private static WardrobeMaterialColor4Koi.Value FourthForMaterial()
		{
			if (hasShaderFourth)
			{
				return shaderFourth;
			}
			return new WardrobeMaterialColor4Koi.Value
			{
				Color = colors[3]
			};
		}

		internal static void PasteToAccessory(ChaControl cha, int slot)
		{
			if (count == 0 || cha == null)
			{
				return;
			}
			Color[] color = cha.nowCoordinate.accessory.parts[slot].color;
			Color[] color2 = WardrobeCompat.FileCoord(cha).accessory.parts[slot].color;
			if (color != null && color2 != null)
			{
				int num = Math.Min(count, Math.Min(color.Length, color2.Length));
				for (int i = 0; i < num; i++)
				{
					color[i] = colors[i];
					color2[i] = colors[i];
				}
				if (num < 4 && hasShaderFourth && color.Length >= 4 && color2.Length >= 4)
				{
					color[3] = shaderFourth.Color;
					color2[3] = shaderFourth.Color;
				}
				cha.ChangeAccessoryColor(slot);
				if (hasShaderFourth || count >= 4)
				{
					WardrobeMaterialColor4Koi.ApplyAccessory(cha, slot, FourthForMaterial());
				}
			}
		}
	}
	internal static class WardrobeCompat
	{
		internal const string CoordinateSubDir = "UserData\\coordinate";

		internal static string GameRoot => Paths.GameRootPath;

		internal static ChaListControl ListCtrl => Character.chaListCtrl;

		internal static ChaFileCoordinate FileCoord(ChaControl cha)
		{
			return cha.chaFile.coordinate[cha.fileStatus.coordinateType];
		}

		internal static void ChangeClothes(ChaControl cha, int kind, int id, bool force)
		{
			int[] subPartsId = cha.nowCoordinate.clothes.subPartsId;
			cha.ChangeClothes(kind, id, subPartsId[0], subPartsId[1], subPartsId[2], force);
		}

		internal static void RefreshNowCoordinate(ChaControl cha)
		{
		}

		internal static void SaveCoordFile(ChaFileCoordinate coord, string path)
		{
			coord.SaveFile(path);
		}

		internal static bool LoadImage(Texture2D tex, byte[] bytes)
		{
			return tex.LoadImage(bytes);
		}

		internal static byte[] EncodeToPNG(Texture2D tex)
		{
			return tex.EncodeToPNG();
		}
	}
	internal static class WardrobeCompositeTopKoi
	{
		private static readonly int[,] Defaults = new int[2, 3]
		{
			{ 0, 0, 1 },
			{ 0, 1, 1 }
		};

		private static Type resolver;

		private static Type Resolver => resolver ?? (resolver = (from a in AppDomain.CurrentDomain.GetAssemblies()
			select a.GetType("Sideloader.AutoResolver.UniversalAutoResolver", throwOnError: false)).FirstOrDefault((Type t) => t != null));

		internal static int TopType(int id)
		{
			Dictionary<int, ListInfoBase> categoryInfo = WardrobeCompat.ListCtrl.GetCategoryInfo(ChaListDefine.CategoryNo.co_top);
			if (categoryInfo == null || !categoryInfo.TryGetValue(id, out var value) || value == null || (value.Kind != 1 && value.Kind != 2))
			{
				return 0;
			}
			return value.Kind;
		}

		internal static int Category(int topType, int piece)
		{
			return ((topType == 1) ? 200 : 210) + piece;
		}

		private static object Member(object value, string name)
		{
			if (value == null)
			{
				return null;
			}
			Type type = value.GetType();
			FieldInfo field = type.GetField(name);
			if (field != null)
			{
				return field.GetValue(value);
			}
			PropertyInfo property = type.GetProperty(name);
			if (!(property == null))
			{
				return property.GetValue(value, null);
			}
			return null;
		}

		internal static int ResolvePiece(int category, ClothingSlotSubPart source)
		{
			if (source == null)
			{
				return -1;
			}
			if (string.IsNullOrEmpty(source.SourceGuid))
			{
				return source.ItemId;
			}
			if (Resolver == null)
			{
				return -1;
			}
			MethodInfo methodInfo = Resolver.GetMethods(BindingFlags.Static | BindingFlags.Public).First((MethodInfo x) => x.Name == "TryGetResolutionInfo" && x.GetParameters().Length == 3 && x.GetParameters()[1].ParameterType.IsEnum);
			object obj = methodInfo.Invoke(null, new object[3]
			{
				source.SourceId,
				Enum.ToObject(methodInfo.GetParameters()[1].ParameterType, category),
				source.SourceGuid
			});
			if (obj != null)
			{
				return Convert.ToInt32(Member(obj, "LocalSlot"));
			}
			return -1;
		}

		internal static ClothingSlotSubPart CapturePiece(int category, int id)
		{
			ClothingSlotSubPart clothingSlotSubPart = new ClothingSlotSubPart
			{
				ItemId = id,
				SourceId = id,
				SourceGuid = ""
			};
			if (id < 100000000)
			{
				return clothingSlotSubPart;
			}
			if (Resolver == null)
			{
				throw new InvalidOperationException("Sideloader is required to identify this top piece.");
			}
			MethodInfo methodInfo = Resolver.GetMethods(BindingFlags.Static | BindingFlags.Public).First((MethodInfo x) => x.Name == "TryGetResolutionInfo" && x.GetParameters().Length == 2 && x.GetParameters()[0].ParameterType.IsEnum);
			object obj = methodInfo.Invoke(null, new object[2]
			{
				Enum.ToObject(methodInfo.GetParameters()[0].ParameterType, category),
				id
			});
			if (obj == null)
			{
				throw new InvalidOperationException("Could not identify the top piece's source mod.");
			}
			clothingSlotSubPart.SourceGuid = (string)Member(obj, "GUID");
			clothingSlotSubPart.SourceId = Convert.ToInt32(Member(obj, "Slot"));
			return clothingSlotSubPart;
		}

		internal static ClothingSlotVisualKoi Create(ChaControl cha, GameObject root, ClothingSlotRecord record, ChaFileClothes.PartsInfo part, Func<int, int, Transform, GameObject> load)
		{
			if (cha == null || root == null || record == null || part == null)
			{
				throw new ArgumentNullException("composite top input");
			}
			ListInfoComponent component = root.GetComponent<ListInfoComponent>();
			ListInfoBase listInfoBase = ((component == null) ? null : component.data);
			int num = ((record.Kind == 0 && listInfoBase != null && (listInfoBase.Kind == 1 || listInfoBase.Kind == 2)) ? listInfoBase.Kind : 0);
			ClothingSlotVisualKoi clothingSlotVisualKoi = new ClothingSlotVisualKoi
			{
				Root = root,
				Component = root.GetComponent<ChaClothesComponent>(),
				Part = part,
				Record = record,
				ExtraType = cha.exType
			};
			if (num == 0)
			{
				if (clothingSlotVisualKoi.Component == null)
				{
					throw new InvalidOperationException("Garment has no native clothes component.");
				}
				return clothingSlotVisualKoi;
			}
			if (load == null)
			{
				throw new ArgumentNullException("load");
			}
			ClothingSlotSubPart[] subParts = record.SubParts;
			ClothingSlotSubPart[] array = new ClothingSlotSubPart[3];
			for (int num2 = 0; num2 < 3; num2++)
			{
				int num3 = Category(num, num2);
				int num4;
				int num5;
				if (subParts != null && num2 < subParts.Length)
				{
					num4 = ((subParts[num2] != null) ? 1 : 0);
					if (num4 != 0)
					{
						num5 = ResolvePiece(num3, subParts[num2]);
						goto IL_011d;
					}
				}
				else
				{
					num4 = 0;
				}
				num5 = Defaults[num - 1, num2];
				goto IL_011d;
				IL_011d:
				int num6 = num5;
				if (num6 < 0)
				{
					throw new InvalidOperationException("Saved top piece mod is missing.");
				}
				GameObject gameObject = load(num3, num6, root.transform);
				if (gameObject == null)
				{
					throw new InvalidOperationException("Top piece asset could not be loaded.");
				}
				if (gameObject.transform.parent != root.transform)
				{
					gameObject.transform.SetParent(root.transform, worldPositionStays: false);
				}
				ListInfoComponent component2 = gameObject.GetComponent<ListInfoComponent>();
				ListInfoBase listInfoBase2 = ((component2 == null) ? null : component2.data);
				ChaClothesComponent component3 = gameObject.GetComponent<ChaClothesComponent>();
				if (listInfoBase2 == null || component3 == null)
				{
					throw new InvalidOperationException("Top piece has no native list entry or clothes component.");
				}
				if (num4 != 0 && listInfoBase2.Id != num6)
				{
					throw new InvalidOperationException("Saved top piece fell back to a different item.");
				}
				array[num2] = CapturePiece(num3, listInfoBase2.Id);
				clothingSlotVisualKoi.AddPiece(gameObject, component3, num2);
			}
			record.SubParts = array;
			return clothingSlotVisualKoi;
		}

		internal static bool IsPieceCategory(int category)
		{
			if (category < 200 || category > 202)
			{
				if (category >= 210)
				{
					return category <= 212;
				}
				return false;
			}
			return true;
		}

		internal static int ChannelIndex(int pieceKind, int compositorChannel)
		{
			if (pieceKind != 2 || compositorChannel != 0)
			{
				return compositorChannel;
			}
			return 3;
		}

		internal static bool UsesNativeFourth(ChaControl cha, int kind, int index)
		{
			if (cha == null || kind != 0)
			{
				return false;
			}
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = WardrobeClothingSlotsKoi.Controller(cha);
			ClothingSlotVisualKoi clothingSlotVisualKoi = (((UnityEngine.Object)(object)clothingSlotsControllerKoi == null) ? null : clothingSlotsControllerKoi.Visual(kind, index));
			if (clothingSlotVisualKoi != null && (clothingSlotVisualKoi.StandaloneSubKind == 2 || clothingSlotVisualKoi.Pieces.Count != 0))
			{
				return true;
			}
			if (index == 0)
			{
				return TopType(cha.nowCoordinate.clothes.parts[0].id) != 0;
			}
			return false;
		}

		internal static ClothingSlotVisualKoi CreateStandalone(ChaControl cha, GameObject root, ClothingSlotRecord record, ChaFileClothes.PartsInfo part, int category)
		{
			if (!IsPieceCategory(category))
			{
				throw new ArgumentOutOfRangeException("category");
			}
			if (cha == null || root == null || record == null || part == null)
			{
				throw new ArgumentNullException("standalone top input");
			}
			ChaClothesComponent component = root.GetComponent<ChaClothesComponent>();
			ListInfoComponent component2 = root.GetComponent<ListInfoComponent>();
			if (component == null || component2 == null || component2.data == null)
			{
				throw new InvalidOperationException("Standalone top piece has no native component or list entry.");
			}
			if (record.ItemId < 1 || component2.data.Id != record.ItemId - 1)
			{
				throw new InvalidOperationException("Standalone top piece fell back to a different item.");
			}
			return new ClothingSlotVisualKoi
			{
				Root = root,
				Component = component,
				Part = part,
				Record = record,
				ExtraType = cha.exType,
				StandaloneSubKind = category % 10
			};
		}

		internal static void RepaintPrimary(ChaControl cha, int kind)
		{
			if (cha == null)
			{
				return;
			}
			if (kind == 0 && TopType(cha.nowCoordinate.clothes.parts[0].id) != 0)
			{
				for (int i = 0; i < 3; i++)
				{
					WardrobeClothesTextureInputs.Prepare(cha, main: false, i);
					cha.ChangeCustomClothes(main: false, i, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true, updateTex04: true);
				}
			}
			else
			{
				WardrobeClothesTextureInputs.Prepare(cha, kind);
				cha.ChangeCustomClothes(main: true, kind, updateColor: true, updateTex01: true, updateTex02: true, updateTex03: true, updateTex04: true);
			}
		}

		internal static ClothingSlotSubPart[] CaptureSubParts(ChaControl cha)
		{
			if (cha == null || TopType(cha.nowCoordinate.clothes.parts[0].id) == 0)
			{
				return null;
			}
			int topType = TopType(cha.nowCoordinate.clothes.parts[0].id);
			ClothingSlotSubPart[] array = new ClothingSlotSubPart[3];
			for (int i = 0; i < 3; i++)
			{
				int id = ((cha.infoParts != null && i < cha.infoParts.Length && cha.infoParts[i] != null) ? cha.infoParts[i].Id : cha.nowCoordinate.clothes.subPartsId[i]);
				array[i] = CapturePiece(Category(topType, i), id);
			}
			return array;
		}

		internal static ChaClothesComponent[] Components(ChaControl cha, int kind, int index)
		{
			if (cha == null || kind < 0 || kind >= 9)
			{
				return new ChaClothesComponent[0];
			}
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = WardrobeClothingSlotsKoi.Controller(cha);
			ClothingSlotVisualKoi clothingSlotVisualKoi = (((UnityEngine.Object)(object)clothingSlotsControllerKoi == null) ? null : clothingSlotsControllerKoi.Visual(kind, index));
			if (index == 0)
			{
				if (clothingSlotVisualKoi != null && clothingSlotVisualKoi.StandaloneSubKind >= 0)
				{
					return clothingSlotVisualKoi.Components;
				}
				if (kind == 0 && TopType(cha.nowCoordinate.clothes.parts[0].id) != 0)
				{
					if (cha.cusClothesSubCmp != null)
					{
						return cha.cusClothesSubCmp.Where((ChaClothesComponent x) => x != null).ToArray();
					}
					return new ChaClothesComponent[0];
				}
				if (!(cha.cusClothesCmp[kind] == null))
				{
					return new ChaClothesComponent[1] { cha.cusClothesCmp[kind] };
				}
				return new ChaClothesComponent[0];
			}
			if (clothingSlotVisualKoi == null)
			{
				return new ChaClothesComponent[0];
			}
			return clothingSlotVisualKoi.Components;
		}

		internal static ChaFileClothes.PartsInfo.ColorInfo GetDefaultColor(ChaControl cha, int kind, int index, int channel)
		{
			ChaFileClothes.PartsInfo.ColorInfo colorInfo = new ChaFileClothes.PartsInfo.ColorInfo();
			if (cha == null || channel < 0 || channel > 3)
			{
				return colorInfo;
			}
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = WardrobeClothingSlotsKoi.Controller(cha);
			ClothingSlotVisualKoi clothingSlotVisualKoi = (((UnityEngine.Object)(object)clothingSlotsControllerKoi == null) ? null : clothingSlotsControllerKoi.Visual(kind, index));
			if (kind == 0 && ((index == 0 && TopType(cha.nowCoordinate.clothes.parts[0].id) != 0) || (clothingSlotVisualKoi != null && (clothingSlotVisualKoi.Pieces.Count != 0 || clothingSlotVisualKoi.StandaloneSubKind >= 0))))
			{
				colorInfo.baseColor = ChaFileDefine.defClothesSubColor[channel];
				return colorInfo;
			}
			ChaClothesComponent[] array = Components(cha, kind, index);
			if (channel < 3 && array.Length != 0)
			{
				FieldInfo field = array[0].GetType().GetField("defMainColor" + (channel + 1).ToString("00"));
				if (field != null)
				{
					colorInfo.baseColor = (Color)field.GetValue(array[0]);
				}
			}
			return colorInfo;
		}
	}
	internal static class WardrobeContext
	{
		private static ChaControl retainedCharacter;

		internal static bool InStudio => StudioAPI.InsideStudio;

		internal static bool InMaker => MakerAPI.InsideAndLoaded;

		internal static bool Active
		{
			get
			{
				if (!InStudio)
				{
					return InMaker;
				}
				return true;
			}
		}

		internal static ChaControl GetActiveCharacter()
		{
			ChaControl selectedCharacter = GetSelectedCharacter();
			if (!InStudio)
			{
				retainedCharacter = null;
				return selectedCharacter;
			}
			if (selectedCharacter != null)
			{
				retainedCharacter = selectedCharacter;
			}
			if (!(retainedCharacter != null))
			{
				return null;
			}
			return retainedCharacter;
		}

		internal static void ClearRetainedCharacter()
		{
			retainedCharacter = null;
		}

		internal static ChaControl GetSelectedCharacter()
		{
			try
			{
				if (InStudio)
				{
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
				if (InMaker)
				{
					return MakerAPI.GetCharacterControl();
				}
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogError((object)("GetActiveCharacter failed: " + ex));
			}
			return null;
		}
	}
	internal static class WardrobeEmoji
	{
		private const int IconSize = 14;

		private const int ClothingIconSize = 20;

		private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

		private static readonly Dictionary<string, Texture2D> iconCache = new Dictionary<string, Texture2D>();

		private static readonly string[] SlotIcons = new string[9] { "top", "bottom", "bra", "underwear", "gloves", "pantyhose", "socks", "shoes", "shoes" };

		private static GUIContent[] slotLabels;

		private static int slotLabelsLang = -1;

		internal static GUIContent[] SlotLabels
		{
			get
			{
				if (slotLabels == null || slotLabelsLang != WardrobeLang.Current)
				{
					slotLabelsLang = WardrobeLang.Current;
					slotLabels = new GUIContent[9];
					for (int i = 0; i < slotLabels.Length; i++)
					{
						slotLabels[i] = C(SlotIcons[i], " " + WardrobeLang.T(WardrobeSession.ClothesSlotNames[i]));
					}
				}
				return slotLabels;
			}
		}

		internal static Texture2D Icon(string name)
		{
			if (iconCache.TryGetValue(name, out var value))
			{
				return value;
			}
			Texture2D texture2D = Tex(name);
			Texture2D texture2D2 = null;
			if (texture2D != null)
			{
				try
				{
					texture2D2 = Downscale(texture2D, IsClothingIcon(name) ? 20 : 14);
				}
				catch (Exception ex)
				{
					WardrobePlugin.Log.LogWarning((object)("Emoji '" + name + "' downscale failed: " + ex.Message));
					texture2D2 = texture2D;
				}
			}
			iconCache[name] = texture2D2;
			return texture2D2;
		}

		private static bool IsClothingIcon(string name)
		{
			switch (name)
			{
			default:
				return name == "shoes";
			case "top":
			case "bottom":
			case "bra":
			case "underwear":
			case "gloves":
			case "pantyhose":
			case "socks":
				return true;
			}
		}

		internal static Texture2D Downscale(Texture2D src, int size)
		{
			Texture2D texture2D = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
			int num = Mathf.Max(1, src.width / size);
			int num2 = Mathf.Max(1, src.height / size);
			for (int i = 0; i < size; i++)
			{
				for (int j = 0; j < size; j++)
				{
					Color clear = Color.clear;
					for (int k = 0; k < num2; k++)
					{
						for (int l = 0; l < num; l++)
						{
							clear += src.GetPixel(j * num + l, i * num2 + k);
						}
					}
					texture2D.SetPixel(j, i, clear / (num * num2));
				}
			}
			texture2D.Apply();
			return texture2D;
		}

		internal static Texture2D Tex(string name)
		{
			if (cache.TryGetValue(name, out var value))
			{
				return value;
			}
			Texture2D texture2D = null;
			try
			{
				using Stream stream = typeof(WardrobeEmoji).Assembly.GetManifestResourceStream("Freelancer604.Wardrobe.Resources." + name + ".png");
				if (stream != null)
				{
					byte[] array = new byte[stream.Length];
					int num;
					for (int i = 0; i < array.Length; i += num)
					{
						num = stream.Read(array, i, array.Length - i);
						if (num <= 0)
						{
							break;
						}
					}
					Texture2D texture2D2 = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
					if (WardrobeCompat.LoadImage(texture2D2, array))
					{
						texture2D = texture2D2;
					}
				}
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("Emoji '" + name + "' load failed: " + ex.Message));
			}
			if (texture2D == null)
			{
				WardrobePlugin.Log.LogWarning((object)("Emoji resource missing: " + name));
			}
			cache[name] = texture2D;
			return texture2D;
		}

		internal static GUIContent C(string icon, string text, string tooltip = null)
		{
			return new GUIContent(text, Icon(icon), tooltip);
		}

		internal static GUIContent SlotLabel(int kind)
		{
			return SlotLabels[kind];
		}

		internal static void IconBox(string name, float size)
		{
			Rect rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
			Texture2D texture2D = Tex(name);
			if (texture2D != null)
			{
				GUI.DrawTexture(rect, texture2D, ScaleMode.ScaleToFit);
			}
		}
	}
	internal static class WardrobeExtraClothesTabKoi
	{
		private sealed class MaterialRow
		{
			internal string Name;

			internal bool Color;

			internal bool Spec;

			internal bool Gloss;

			internal bool Metal;
		}

		private sealed class ShapeRow
		{
			internal SkinnedMeshRenderer Renderer;

			internal int Index;

			internal string Label;
		}

		private static readonly HashSet<string> openMaterials = new HashSet<string>();

		private static string pendingMaterialToggle;

		private static readonly bool[] layoutColors = new bool[4];

		private static readonly bool[] layoutPatterns = new bool[4];

		private static bool layoutHasComponent;

		private static bool layoutOpt1;

		private static bool layoutOpt2;

		private static bool layoutNativeFourth;

		private static bool layoutStandaloneThird;

		private static readonly List<MaterialRow> materialRows = new List<MaterialRow>();

		private static readonly List<ShapeRow> shapeRows = new List<ShapeRow>();

		internal static void Draw(WardrobeSession s, int kind, int index)
		{
			if (Event.current.type == EventType.Layout && pendingMaterialToggle != null)
			{
				if (!openMaterials.Add(pendingMaterialToggle))
				{
					openMaterials.Remove(pendingMaterialToggle);
				}
				pendingMaterialToggle = null;
			}
			ChaFileClothes.PartsInfo clothingSlotPart = s.GetClothingSlotPart(kind, index);
			if (clothingSlotPart == null)
			{
				return;
			}
			if (Event.current.type == EventType.Layout)
			{
				ChaClothesComponent[] array = WardrobeCompositeTopKoi.Components(s.cha, kind, index);
				layoutHasComponent = array.Length != 0;
				bool[] array2 = layoutColors;
				bool flag;
				layoutColors[1] = (flag = (layoutColors[2] = false));
				array2[0] = flag;
				layoutOpt1 = (layoutOpt2 = false);
				ChaClothesComponent[] array3 = array;
				foreach (ChaClothesComponent chaClothesComponent in array3)
				{
					if (!(chaClothesComponent == null))
					{
						layoutColors[0] |= chaClothesComponent.useColorA01 || chaClothesComponent.useColorN01;
						layoutColors[1] |= chaClothesComponent.useColorA02 || chaClothesComponent.useColorN02;
						layoutColors[2] |= chaClothesComponent.useColorA03 || chaClothesComponent.useColorN03;
						layoutOpt1 |= chaClothesComponent.objOpt01 != null && chaClothesComponent.objOpt01.Length != 0;
						layoutOpt2 |= chaClothesComponent.objOpt02 != null && chaClothesComponent.objOpt02.Length != 0;
					}
				}
				layoutOpt1 &= clothingSlotPart.hideOpt != null && clothingSlotPart.hideOpt.Length != 0;
				layoutOpt2 &= clothingSlotPart.hideOpt != null && clothingSlotPart.hideOpt.Length > 1;
				layoutNativeFourth = WardrobeCompositeTopKoi.UsesNativeFourth(s.cha, kind, index);
				ClothingSlotsControllerKoi clothingSlotsControllerKoi = WardrobeClothingSlotsKoi.Controller(s.cha);
				ClothingSlotVisualKoi clothingSlotVisualKoi = (((UnityEngine.Object)(object)clothingSlotsControllerKoi == null) ? null : clothingSlotsControllerKoi.Visual(kind, index));
				layoutStandaloneThird = clothingSlotVisualKoi != null && clothingSlotVisualKoi.StandaloneSubKind == 2;
				bool flag2 = layoutColors[0];
				if (layoutStandaloneThird)
				{
					layoutColors[0] = false;
				}
				layoutColors[3] = layoutNativeFourth && clothingSlotPart.colorInfo != null && clothingSlotPart.colorInfo.Length > 3 && (!layoutStandaloneThird || flag2);
				for (int j = 0; j < layoutPatterns.Length; j++)
				{
					layoutPatterns[j] = layoutColors[j] && clothingSlotPart.colorInfo != null && j < clothingSlotPart.colorInfo.Length && clothingSlotPart.colorInfo[j].pattern != 0;
				}
			}
			string text = "extra_cloth_" + kind + "_" + index;
			int prefCategory = (int)WardrobeSession.ClothesCategories[kind];
			int currentToken = s.ClothingSelectionId(kind, index);
			Func<List<WardrobeItem>> func = () => WardrobeKoiTopCatalog.List(kind);
			Func<List<WardrobeItem>> func2 = () => WardrobeKoiTopCatalog.HeaderList(kind, currentToken);
			Func<List<WardrobeItem>> listFunc = ((kind == 0 && currentToken > 0 && WardrobeCompositeTopKoi.TopType(currentToken) != 0) ? func2 : func);
			WardrobeWidgets.BeginZone(null);
			GUILayout.BeginHorizontal();
			GUILayout.BeginVertical(GUILayout.Width(96f));
			WardrobeWidgets.CurrentThumbBox(text, currentToken, listFunc, prefCategory);
			if (WardrobeWidgets.MiniButton(WardrobeEmoji.C("dice", " " + WardrobeLang.T("Randomize")), 96f))
			{
				List<WardrobeItem> list = func();
				if (list != null && list.Count > 0)
				{
					s.PushUndo();
					s.SetClothingSlotItem(kind, index, list[UnityEngine.Random.Range(0, list.Count)].id);
				}
			}
			GUILayout.EndVertical();
			GUILayout.Space(4f);
			GUILayout.BeginVertical();
			WardrobeWidgets.SelectorHeader("Item", text, currentToken, listFunc, 38f);
			bool flag3 = kind != 4 && kind != 6 && kind != 7 && kind != 8;
			string[] array4 = ((!flag3) ? new string[2] { "ON", "OFF" } : new string[3] { "ON", "Half", "OFF" });
			int[] array5 = ((!flag3) ? new int[2] { 0, 2 } : new int[3] { 0, 1, 2 });
			int clothingSlotState = s.GetClothingSlotState(kind, index);
			string[] array6 = new string[array4.Length + 1];
			Array.Copy(array4, array6, array4.Length);
			array6[array4.Length] = "Clear";
			int num = WardrobeWidgets.FillButtonRow("State", 38f, array6, Array.IndexOf(array5, clothingSlotState));
			if (num >= 0)
			{
				if (num == array4.Length)
				{
					s.PushUndo();
					s.ClearClothingSlot(kind, index);
				}
				else if (array5[num] != clothingSlotState)
				{
					s.PushUndo();
					s.SetClothingSlotState(kind, index, (byte)array5[num]);
				}
			}
			switch (WardrobeWidgets.FillButtonRow("Color", 38f, new string[3] { "Restore", "Copy", "Paste" }, -1, 0f, new bool[3]
			{
				true,
				true,
				WardrobeColorClipboard.HasData
			}))
			{
			case 0:
				RestoreColors(s, kind, index, -1);
				break;
			case 1:
				WardrobeColorClipboard.CopyFromClothes(s.cha, kind, index, clothingSlotPart);
				break;
			case 2:
			{
				s.PushUndo();
				ChaFileClothes.PartsInfo clothingSlotPart2 = s.GetClothingSlotPart(kind, index);
				if (clothingSlotPart2 != null)
				{
					WardrobeColorClipboard.PasteToClothes(s.cha, kind, index, clothingSlotPart2);
					Changed(s, kind, index);
				}
				break;
			}
			}
			if (WardrobeWidgets.FillButtonRow("Mat", 38f, new string[1] { "Materials & shapes" }) == 0)
			{
				pendingMaterialToggle = text;
			}
			WardrobeClothesTab.DrawNumberActions(s, kind, index, s.GetClothingSlotCount(kind));
			GUILayout.EndVertical();
			GUILayout.EndHorizontal();
			WardrobeWidgets.EndZone();
			WardrobeWidgets.SelectorBody(s, text, currentToken, delegate(int id)
			{
				s.PushUndo();
				s.SetClothingSlotItem(kind, index, id);
			}, thumbs: true, prefCategory);
			if (!layoutHasComponent)
			{
				return;
			}
			for (int num2 = 0; num2 < layoutColors.Length; num2++)
			{
				if (layoutColors[num2])
				{
					DrawColor(s, kind, index, num2);
				}
			}
			if (!layoutNativeFourth)
			{
				WardrobeShaderColor4TabKoi.DrawExtra(s, kind, index, clothingSlotPart.id);
			}
			if (openMaterials.Contains(text))
			{
				DrawMaterials(s, kind, index);
			}
			if (layoutOpt1 || layoutOpt2)
			{
				DrawOptions(s, kind, index);
			}
		}

		private static void Changed(WardrobeSession s, int kind, int index)
		{
			WardrobeClothingSlotsKoi.Repaint(s.cha, kind, index);
			s.MarkClothingSlotCustom(kind, index, colors: true);
		}

		private static void RestoreColors(WardrobeSession s, int kind, int index, int only)
		{
			s.PushUndo();
			ChaFileClothes.PartsInfo clothingSlotPart = s.GetClothingSlotPart(kind, index);
			if (clothingSlotPart == null || clothingSlotPart.colorInfo == null)
			{
				return;
			}
			int num = (WardrobeCompositeTopKoi.UsesNativeFourth(s.cha, kind, index) ? 4 : 3);
			for (int i = 0; i < num && i < clothingSlotPart.colorInfo.Length; i++)
			{
				if (only < 0 || only == i)
				{
					clothingSlotPart.colorInfo[i] = WardrobeCompositeTopKoi.GetDefaultColor(s.cha, kind, index, i);
				}
			}
			Changed(s, kind, index);
		}

		private static void DrawColor(WardrobeSession s, int kind, int index, int c)
		{
			Func<ChaFileClothes.PartsInfo> live = () => s.GetClothingSlotPart(kind, index);
			ChaFileClothes.PartsInfo partsInfo = live();
			if (partsInfo == null || partsInfo.colorInfo == null || c >= partsInfo.colorInfo.Length)
			{
				return;
			}
			string text = "extra_cloth_" + kind + "_" + index + "_z" + c;
			WardrobeWidgets.BeginZone((c == 3 && layoutStandaloneThird) ? "Color 1" : ("Color " + (c + 1)), delegate
			{
				if (WardrobeWidgets.MiniButton("Copy", 44f) && live() != null)
				{
					WardrobeChannelClipboard.CopyChannel(live(), c);
				}
				bool enabled = GUI.enabled;
				GUI.enabled = enabled && WardrobeChannelClipboard.HasData;
				if (WardrobeWidgets.MiniButton("Paste", 44f))
				{
					s.PushUndo();
					ChaFileClothes.PartsInfo partsInfo2 = live();
					if (partsInfo2 != null)
					{
						WardrobeChannelClipboard.ApplyChannel(partsInfo2, c);
						Changed(s, kind, index);
					}
				}
				GUI.enabled = enabled;
				if (WardrobeWidgets.MiniButton("Reset", 44f))
				{
					RestoreColors(s, kind, index, c);
				}
			});
			GUILayout.BeginHorizontal();
			WardrobeWidgets.SwatchColumn(s, null, text + "_color", delegate
			{
				ChaFileClothes.PartsInfo partsInfo2 = live();
				return (partsInfo2 == null || partsInfo2.colorInfo == null || c >= partsInfo2.colorInfo.Length) ? Color.white : partsInfo2.colorInfo[c].baseColor;
			}, delegate(Color v)
			{
				ChaFileClothes.PartsInfo partsInfo2 = live();
				if (partsInfo2 != null && partsInfo2.colorInfo != null && c < partsInfo2.colorInfo.Length)
				{
					partsInfo2.colorInfo[c].baseColor = v;
					Changed(s, kind, index);
				}
			});
			GUILayout.Space(4f);
			GUILayout.BeginVertical();
			WardrobeWidgets.SelectorHeader("Pattern", text + "_ptn", partsInfo.colorInfo[c].pattern, () => WardrobeCatalog.List(430), 52f);
			GUILayout.EndVertical();
			GUILayout.EndHorizontal();
			WardrobeWidgets.SelectorBody(s, text + "_ptn", partsInfo.colorInfo[c].pattern, delegate(int id)
			{
				s.PushUndo();
				ChaFileClothes.PartsInfo partsInfo2 = live();
				if (partsInfo2 != null && partsInfo2.colorInfo != null && c < partsInfo2.colorInfo.Length)
				{
					partsInfo2.colorInfo[c].pattern = id;
					Changed(s, kind, index);
				}
			}, thumbs: true, 430);
			if (layoutPatterns[c])
			{
				WardrobeWidgets.SwatchColumn(s, "Pattern Color", text + "_ptncol", delegate
				{
					ChaFileClothes.PartsInfo partsInfo2 = live();
					return (partsInfo2 == null || partsInfo2.colorInfo == null || c >= partsInfo2.colorInfo.Length) ? Color.white : partsInfo2.colorInfo[c].patternColor;
				}, delegate(Color v)
				{
					ChaFileClothes.PartsInfo partsInfo2 = live();
					if (partsInfo2 != null && partsInfo2.colorInfo != null && c < partsInfo2.colorInfo.Length)
					{
						partsInfo2.colorInfo[c].patternColor = v;
						Changed(s, kind, index);
					}
				});
			}
			WardrobeWidgets.EndZone();
		}

		private static void DrawOptions(WardrobeSession s, int kind, int index)
		{
			Func<ChaFileClothes.PartsInfo> live = () => s.GetClothingSlotPart(kind, index);
			if (!layoutOpt1 && !layoutOpt2)
			{
				return;
			}
			string text = "extra_cloth_" + kind + "_" + index;
			WardrobeWidgets.BeginZone("Options");
			GUILayout.BeginHorizontal();
			if (layoutOpt1)
			{
				WardrobeWidgets.ToggleRow(s, "Option Parts 1", text + "_opt1", () => live() != null && live().hideOpt != null && live().hideOpt.Length != 0 && !live().hideOpt[0], delegate(bool v)
				{
					ChaFileClothes.PartsInfo partsInfo = live();
					if (partsInfo != null && partsInfo.hideOpt != null && partsInfo.hideOpt.Length != 0)
					{
						partsInfo.hideOpt[0] = !v;
						Changed(s, kind, index);
					}
				});
			}
			if (layoutOpt2)
			{
				WardrobeWidgets.ToggleRow(s, "Option Parts 2", text + "_opt2", () => live() != null && live().hideOpt != null && live().hideOpt.Length > 1 && !live().hideOpt[1], delegate(bool v)
				{
					ChaFileClothes.PartsInfo partsInfo = live();
					if (partsInfo != null && partsInfo.hideOpt != null && partsInfo.hideOpt.Length > 1)
					{
						partsInfo.hideOpt[1] = !v;
						Changed(s, kind, index);
					}
				});
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			WardrobeWidgets.EndZone();
		}

		private static void DrawMaterials(WardrobeSession s, int kind, int index)
		{
			if (Event.current.type == EventType.Layout)
			{
				materialRows.Clear();
				shapeRows.Clear();
				HashSet<Renderer> borrowed = WardrobeBorrowedBodyMaterialsKoi.Targets(WardrobeMaterialColor4Koi.ClothingRoot(s.cha, kind, index));
				Renderer[] renderers = WardrobeClothingSlotsKoi.GetRenderers(s.cha, kind, index);
				foreach (Renderer renderer in renderers)
				{
					if (renderer == null)
					{
						continue;
					}
					SkinnedMeshRenderer skinnedMeshRenderer = renderer as SkinnedMeshRenderer;
					if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
					{
						for (int j = 0; j < skinnedMeshRenderer.sharedMesh.blendShapeCount; j++)
						{
							shapeRows.Add(new ShapeRow
							{
								Renderer = skinnedMeshRenderer,
								Index = j,
								Label = skinnedMeshRenderer.name + ": " + skinnedMeshRenderer.sharedMesh.GetBlendShapeName(j)
							});
						}
					}
					Material[] array = WardrobeBorrowedBodyMaterialsKoi.Materials(renderer, borrowed);
					foreach (Material material in array)
					{
						if (!(material == null))
						{
							string name = CleanName(material.name);
							MaterialRow materialRow = materialRows.Find((MaterialRow x) => x.Name == name);
							if (materialRow == null)
							{
								materialRow = new MaterialRow
								{
									Name = name
								};
								materialRows.Add(materialRow);
							}
							materialRow.Color |= material.HasProperty("_Color4");
							materialRow.Spec |= material.HasProperty("_SpecColor");
							materialRow.Gloss |= material.HasProperty("_Glossiness");
							materialRow.Metal |= material.HasProperty("_Metallic");
						}
					}
				}
			}
			for (int num = 0; num < materialRows.Count; num++)
			{
				MaterialRow row = materialRows[num];
				string text = "extra_cloth_" + kind + "_" + index + "_mat" + num;
				WardrobeWidgets.BeginZone("Material: " + row.Name);
				if (row.Color)
				{
					MaterialColor(s, kind, index, row.Name, text + "_c4", "Color 4", "_Color4");
				}
				if (row.Spec)
				{
					MaterialColor(s, kind, index, row.Name, text + "_spec", "Specular", "_SpecColor");
				}
				if (row.Gloss)
				{
					WardrobeWidgets.MiniSliderRow(s, "Gloss", text + "_gloss", () => ReadFloat(s, kind, index, row.Name, "_Glossiness"), delegate(float v)
					{
						WriteFloat(s, kind, index, row.Name, "_Glossiness", v);
					});
				}
				if (row.Metal)
				{
					WardrobeWidgets.MiniSliderRow(s, "Metal", text + "_metal", () => ReadFloat(s, kind, index, row.Name, "_Metallic"), delegate(float v)
					{
						WriteFloat(s, kind, index, row.Name, "_Metallic", v);
					});
				}
				WardrobeWidgets.EndZone();
			}
			if (shapeRows.Count == 0)
			{
				return;
			}
			WardrobeWidgets.BeginZone("Blendshapes");
			for (int num2 = 0; num2 < shapeRows.Count; num2++)
			{
				ShapeRow row2 = shapeRows[num2];
				string undoKey = "extra_cloth_" + kind + "_" + index + "_shape" + num2;
				GUILayout.Label(row2.Label);
				WardrobeWidgets.MiniSliderRow(s, "Weight", undoKey, () => (!(row2.Renderer != null)) ? 0f : (row2.Renderer.GetBlendShapeWeight(row2.Index) / 100f), delegate(float v)
				{
					if (!(row2.Renderer == null))
					{
						row2.Renderer.SetBlendShapeWeight(row2.Index, v * 100f);
						WardrobeClothingSlotsKoi.CaptureAppearance(s.cha, kind, index);
						s.MarkClothingSlotCustom(kind, index, colors: true);
					}
				});
			}
			WardrobeWidgets.EndZone();
		}

		private static string CleanName(string name)
		{
			if (name == null || !name.EndsWith(" (Instance)", StringComparison.Ordinal))
			{
				return name;
			}
			return name.Substring(0, name.Length - " (Instance)".Length);
		}

		private static Material FindMaterial(WardrobeSession s, int kind, int index, string group)
		{
			Renderer[] array = WardrobeMaterialColor4Koi.ClothingMaterialRenderers(s.cha, kind, index);
			foreach (Renderer renderer in array)
			{
				if (!(renderer != null))
				{
					continue;
				}
				Material[] sharedMaterials = renderer.sharedMaterials;
				foreach (Material material in sharedMaterials)
				{
					if (material != null && CleanName(material.name) == group)
					{
						return material;
					}
				}
			}
			return null;
		}

		private static void MaterialColor(WardrobeSession s, int kind, int index, string group, string key, string label, string property)
		{
			WardrobeWidgets.SwatchColumn(s, label, key, delegate
			{
				Material material = FindMaterial(s, kind, index, group);
				return (!(material != null) || !material.HasProperty(property)) ? Color.white : material.GetColor(property);
			}, delegate(Color value)
			{
				Renderer[] array = WardrobeMaterialColor4Koi.ClothingMaterialRenderers(s.cha, kind, index);
				foreach (Renderer renderer in array)
				{
					if (renderer != null)
					{
						Material[] sharedMaterials = renderer.sharedMaterials;
						foreach (Material material in sharedMaterials)
						{
							if (material != null && CleanName(material.name) == group && material.HasProperty(property))
							{
								material.SetColor(property, value);
							}
						}
					}
				}
				WardrobeClothingSlotsKoi.CaptureAppearance(s.cha, kind, index);
				s.MarkClothingSlotCustom(kind, index, colors: true);
			});
		}

		private static float ReadFloat(WardrobeSession s, int kind, int index, string group, string property)
		{
			Material material = FindMaterial(s, kind, index, group);
			if (!(material != null) || !material.HasProperty(property))
			{
				return 0f;
			}
			return material.GetFloat(property);
		}

		private static void WriteFloat(WardrobeSession s, int kind, int index, string group, string property, float value)
		{
			Renderer[] array = WardrobeMaterialColor4Koi.ClothingMaterialRenderers(s.cha, kind, index);
			foreach (Renderer renderer in array)
			{
				if (!(renderer != null))
				{
					continue;
				}
				Material[] sharedMaterials = renderer.sharedMaterials;
				foreach (Material material in sharedMaterials)
				{
					if (material != null && CleanName(material.name) == group && material.HasProperty(property))
					{
						material.SetFloat(property, value);
					}
				}
			}
			WardrobeClothingSlotsKoi.CaptureAppearance(s.cha, kind, index);
			s.MarkClothingSlotCustom(kind, index, colors: true);
		}
	}
	internal static class WardrobeGUI
	{
		private struct MixerRects
		{
			internal Rect Row;

			internal WardrobeMixerColumns Columns;

			internal Rect this[int i] => new Rect(Row.x + Columns.X[i], Row.y, Columns.Width[i], Row.height);
		}

		private static bool _visible;

		private static bool ornateWrapper;

		private static Rect winRect = new Rect(140f, 120f, 500f, 830f);

		private static float uiScale = 1f;

		private static float requestedUiScale = 1f;

		private static bool uiScalingAvailable = true;

		private static float scaleDragStart;

		private static Vector2 scaleDragPointer;

		private const int ScaleHandleHint = 604106;

		private const float DesignWindowHeight = 830f;

		private const float CompanionFootprintWidth = 478f;

		private static int tab;

		private static SuitePageHost suitePageHost;

		private static readonly SuiteNoSelectionStartupState noSelectionStartup = new SuiteNoSelectionStartupState();

		private static SuiteCleanupHost suiteCleanupHost;

		private static SuiteBridgeV1 suiteBridge;

		private static readonly string[] TabNames = new string[3] { "Home", "Clothes", "Accessories" };

		private static bool langOpen;

		private static Rect langBtnRect;

		private static GUIStyle tabStyle;

		private static bool suiteModeOpen;

		private static readonly PopupHoverState suiteModeHoverState = new PopupHoverState();

		private static int pendingSuitePageSel = -1;

		private static Rect suiteModeBtnRect;

		private static readonly Color SuiteCleanupRed = new Color(1f, 0.38f, 0.32f);

		private static int flashTab = -1;

		private static float flashUntil;

		private const float FlashCycle = 0.5f;

		private static Vector2 scroll;

		private static List<int> renderAccSlots = new List<int>();

		private static int[] renderClothingCounts;

		private static Vector2 editorScroll;

		private static Vector2 cleanupScroll;

		private static string cardName = "";

		private static string message = "";

		private static float messageTimer;

		private static float baselineDeadline;

		private static float saveDeadline;

		private const float SaveCountdown = 3f;

		private static byte[] snapshotPng;

		private static Texture2D previewTex;

		private static Texture2D greyTex;

		private static Texture2D winBgTex;

		private static Texture2D scaleHandleTexture;

		private static GUIStyle windowStyle;

		private static GUISkin shellStyleSkin;

		private static GUIStyle suitePlusStyle;

		private static GUISkin koiSkin;

		private static Texture2D koiScrollTrackTex;

		private static Texture2D koiScrollThumbTex;

		private static Texture2D koiScrollHoverTex;

		private static GUIStyle titleStyle;

		private static GUIStyle creditStyle;

		private static GUIStyle dressingStyle;

		private static GUIStyle closeButtonStyle;

		private static Rect snapshotRect;

		private static float nameRowLeft;

		private static float nameRowRight;

		private static GUIStyle nameLabelStyle;

		private static GUIStyle colHeaderStyle;

		private static float clearColW = 48f;

		private const int MoveIdle = 0;

		private const int MovePickSource = 1;

		private const int MovePickDest = 2;

		private static int moveState = 0;

		private static int moveSrcSel;

		private static int moveSrcSlot;

		private static float accTrailGap;

		private const string PatreonUrl = "https://www.patreon.com/Freelancer604";

		private const string PatreonLabel = "Freelancer604's Patreon";

		private static Font uiFont;

		private static bool uiFontTried;

		private static bool atResolved;

		private static object atPlugin;

		private static MethodInfo atDisable;

		private static MethodInfo atEnable;

		private static MethodInfo atIsDisabled;

		private static float bottomBarTop;

		private static GUIStyle toastStyle;

		private static int colStylesLang = -1;

		private static GUIStyle emptySrcStyle;

		private static Action pendingMove;

		private static GUISkin mixerSkin;

		private static int mixerLanguage = -1;

		private static GUIStyle mixerButton;

		private static GUIStyle mixerHeading;

		private static GUIStyle mixerSourceHeading;

		private static float mixerLabelWidth;

		private static float mixerClearWidth;

		private static float mixerRandomWidth;

		private static readonly string[] mixerStateNames = new string[3] { "ON", "Half", "OFF" };

		private static readonly float[] mixerStateWidths = new float[3];

		private static WardrobeMixerColumns mixerColumns;

		private static float mixerColumnsWidth;

		internal static bool visible
		{
			get
			{
				return _visible;
			}
			set
			{
				if (_visible != value)
				{
					_visible = value;
					if (suitePageHost != null)
					{
						suitePageHost.SetHostVisible(value);
					}
					if (!value)
					{
						WardrobeCelebration.ResetModeDust();
						suiteModeHoverState.Close();
						suiteModeOpen = false;
					}
				}
			}
		}

		internal static bool MouseOverWindow => ContainsScreenPointer(Input.mousePosition);

		private static Rect PointerBounds
		{
			get
			{
				if (ornateWrapper)
				{
					return new Rect(winRect.x - 140f, winRect.y - 48f, winRect.width + 140f + 60f, winRect.height + 48f + 148f);
				}
				return winRect;
			}
		}

		private static float EmptyLeftMargin
		{
			get
			{
				if (ornateWrapper)
				{
					return 56f;
				}
				return 0f;
			}
		}

		private static Rect InteractiveBounds
		{
			get
			{
				Rect pointerBounds = PointerBounds;
				pointerBounds.x += EmptyLeftMargin;
				pointerBounds.width -= EmptyLeftMargin;
				return pointerBounds;
			}
		}

		private static Color TabAccent
		{
			get
			{
				if (SuiteMakeUpTheme.IsAnyActive)
				{
					return SuiteMakeUpTheme.ActiveAccent;
				}
				return new Color(0.35f, 0.75f, 1f);
			}
		}

		private static float PulseT => (Mathf.Sin(Time.realtimeSinceStartup * ((float)Math.PI * 4f)) + 1f) * 0.5f;

		private static string CoordinateDir => Path.Combine(WardrobeCompat.GameRoot, "UserData\\coordinate");

		internal static bool ContainsScreenPointer(Vector2 position)
		{
			if (visible && SuiteStudioUiVisibility.IsVisible())
			{
				return InteractiveBounds.Contains(new Vector2(position.x / uiScale, ((float)Screen.height - position.y) / uiScale));
			}
			return false;
		}

		internal static void SetUserScale(float scale)
		{
			requestedUiScale = ((scale > 0f) ? scale : 1f);
		}

		internal static void SetMessage(string msg)
		{
			message = msg;
			messageTimer = 4f;
		}

		internal static object GetSuiteBridgeV1()
		{
			EnsureSuitePageHost();
			return suiteBridge;
		}

		internal static void ResetSuitePages()
		{
			WardrobeContext.ClearRetainedCharacter();
			if (suitePageHost != null)
			{
				suitePageHost.ResetPages();
			}
			noSelectionStartup.Reset();
		}

		private static void EnsureSuitePageHost()
		{
			if (suitePageHost != null)
			{
				return;
			}
			suitePageHost = new SuitePageHost(1);
			suiteCleanupHost = new SuiteCleanupHost(1);
			suitePageHost.TryRegisterBuiltIn("dressup", 0, 1, delegate
			{
			}, delegate
			{
			}, delegate
			{
			}, delegate
			{
			});
			EnsureCleanupSuitePage();
			suitePageHost.SetErrorSink(delegate(Exception error)
			{
				try
				{
					if (WardrobePlugin.Log != null)
					{
						WardrobePlugin.Log.LogError((object)("Suite page callback: " + error));
					}
				}
				catch
				{
				}
			});
			suiteBridge = new SuiteBridgeV1(1, suitePageHost, suiteCleanupHost, EnsureCleanupSuitePage);
			suiteBridge.CharacterResolver = () => WardrobeContext.GetActiveCharacter();
			suitePageHost.SetHostVisible(_visible);
		}

		private static void EnsureCleanupSuitePage()
		{
			if (suitePageHost != null)
			{
				suitePageHost.TryRegisterBuiltIn("cleanup", 200, 1, DrawCleanupUpgradePage, delegate
				{
				}, delegate
				{
				}, delegate
				{
				});
			}
		}

		private static void DrawCleanupUpgradePage()
		{
			GUIStyle style = new GUIStyle(GUI.skin.label)
			{
				fontSize = 20,
				fontStyle = FontStyle.Bold,
				alignment = TextAnchor.MiddleCenter,
				wordWrap = true
			};
			GUIStyle style2 = new GUIStyle(GUI.skin.label)
			{
				fontSize = 15,
				alignment = TextAnchor.MiddleCenter,
				wordWrap = true
			};
			GUILayout.FlexibleSpace();
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUILayout.BeginVertical(GUILayout.Width(420f));
			GUILayout.Label("Cleanup Mode Requires Vanity Studio PLUS", style);
			GUILayout.Space(24f);
			GUILayout.Label("With Clean-Up mode, preview and permanently remove unwanted zipmods from your game. Declutter your selection grids and save hard drive space with Clean-Up!", style2);
			GUILayout.Space(24f);
			GUILayout.Label("Obtainable here:", style2);
			GUILayout.Space(8f);
			if (GUILayout.Button("Get Vanity Studio PLUS", GUILayout.Height(34f)))
			{
				Application.OpenURL("https://www.patreon.com/Freelancer604/posts/dress-up-plus-165393058");
			}
			GUILayout.EndVertical();
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.FlexibleSpace();
		}

		private static void DrawCleanupSuitePage()
		{
			if (suiteCleanupHost == null)
			{
				return;
			}
			suiteCleanupHost.ApplyDeferredSelection(Event.current != null && Event.current.type == EventType.Layout);
			if (suiteCleanupHost.SelectorVisible)
			{
				GUILayout.BeginHorizontal(GUILayout.Height(28f));
				for (int i = 0; i < suiteCleanupHost.Contributors.Count; i++)
				{
					SuiteHostedCleanup suiteHostedCleanup = suiteCleanupHost.Contributors[i];
					bool num = string.Equals(suiteHostedCleanup.Id, suiteCleanupHost.ActiveContributorId, StringComparison.Ordinal);
					Color color = GUI.color;
					GUI.color = (num ? SuiteCleanupRed : Color.white);
					if (GUILayout.Button(suiteHostedCleanup.Label, GUILayout.Height(27f)))
					{
						suiteCleanupHost.RequestContributor(suiteHostedCleanup.Id);
					}
					GUI.color = color;
				}
				GUILayout.EndHorizontal();
				GUILayout.Space(4f);
			}
			cleanupScroll = GUILayout.BeginScrollView(cleanupScroll, false, true);
			if (ornateWrapper)
			{
				suiteCleanupHost.DrawActiveBody();
			}
			else
			{
				suiteCleanupHost.DrawActive();
			}
			GUILayout.EndScrollView();
			if (ornateWrapper && suiteCleanupHost.HasActiveFooter)
			{
				SuiteWrapperTheme.DeferFooter(suiteCleanupHost.DrawActiveFooter);
			}
		}

		private static void EnterCleanupSuitePage()
		{
			cleanupScroll = Vector2.zero;
			if (suiteCleanupHost != null)
			{
				suiteCleanupHost.OnEnter();
			}
		}

		private static void ExitCleanupSuitePage()
		{
			if (suiteCleanupHost != null)
			{
				suiteCleanupHost.OnExit();
			}
		}

		private static void ResetCleanupSuitePage()
		{
			cleanupScroll = Vector2.zero;
			if (suiteCleanupHost != null)
			{
				suiteCleanupHost.Reset();
			}
		}

		private static void EnsureUiFont()
		{
			if (uiFontTried)
			{
				return;
			}
			uiFontTried = true;
			try
			{
				uiFont = Font.CreateDynamicFontFromOSFont(new string[9] { "Microsoft YaHei", "Microsoft YaHei UI", "Yu Gothic", "Meiryo", "MS Gothic", "Malgun Gothic", "SimSun", "Arial Unicode MS", "Arial" }, 16);
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("CJK UI font load failed: " + ex.Message));
			}
		}

		private static Texture2D SolidTexture(Color color)
		{
			Texture2D texture2D = new Texture2D(1, 1);
			texture2D.SetPixel(0, 0, color);
			texture2D.Apply();
			return texture2D;
		}

		private static void SetStyleBackgrounds(GUIStyle style, Texture2D normal, Texture2D hover)
		{
			if (style != null)
			{
				style.normal.background = normal;
				style.onNormal.background = normal;
				style.focused.background = normal;
				style.onFocused.background = normal;
				style.hover.background = hover;
				style.onHover.background = hover;
				style.active.background = hover;
				style.onActive.background = hover;
			}
		}

		private static void EnsureKoiSkin(GUISkin source)
		{
			if (!(koiSkin != null) && !(source == null))
			{
				koiSkin = UnityEngine.Object.Instantiate(source);
				if (!(koiSkin == null))
				{
					koiScrollTrackTex = SolidTexture(new Color(0.18f, 0.19f, 0.22f, 1f));
					koiScrollThumbTex = SolidTexture(new Color(0.72f, 0.82f, 0.92f, 1f));
					koiScrollHoverTex = SolidTexture(new Color(0.92f, 0.97f, 1f, 1f));
					SetStyleBackgrounds(koiSkin.verticalScrollbar, koiScrollTrackTex, koiScrollTrackTex);
					SetStyleBackgrounds(koiSkin.horizontalScrollbar, koiScrollTrackTex, koiScrollTrackTex);
					SetStyleBackgrounds(koiSkin.verticalScrollbarThumb, koiScrollThumbTex, koiScrollHoverTex);
					SetStyleBackgrounds(koiSkin.horizontalScrollbarThumb, koiScrollThumbTex, koiScrollHoverTex);
					SetStyleBackgrounds(koiSkin.verticalScrollbarUpButton, koiScrollThumbTex, koiScrollHoverTex);
					SetStyleBackgrounds(koiSkin.verticalScrollbarDownButton, koiScrollThumbTex, koiScrollHoverTex);
					SetStyleBackgrounds(koiSkin.horizontalScrollbarLeftButton, koiScrollThumbTex, koiScrollHoverTex);
					SetStyleBackgrounds(koiSkin.horizontalScrollbarRightButton, koiScrollThumbTex, koiScrollHoverTex);
				}
			}
		}

		private static void ResolveAutoTranslator()
		{
			atResolved = true;
			try
			{
				Type type = null;
				Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
				foreach (Assembly assembly in assemblies)
				{
					if (assembly.GetName().Name == "XUnity.AutoTranslator.Plugin.Core")
					{
						type = assembly.GetType("XUnity.AutoTranslator.Plugin.Core.AutoTranslationPlugin");
						break;
					}
				}
				if (!(type == null))
				{
					atPlugin = type.GetProperty("Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null, null) ?? type.GetField("Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
					if (atPlugin != null)
					{
						atDisable = type.GetMethod("DisableAutoTranslator", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						atEnable = type.GetMethod("EnableAutoTranslator", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						atIsDisabled = type.GetMethod("IsTemporarilyDisabled", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						WardrobePlugin.Log.LogInfo((object)"Hooked XUnity.AutoTranslator disable toggle — Dress-Up's own translations will be preserved.");
					}
				}
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("AutoTranslator resolve failed: " + ex.Message));
			}
		}

		private static bool SuppressAutoTranslate()
		{
			if (!atResolved)
			{
				ResolveAutoTranslator();
			}
			if (atPlugin == null || atDisable == null)
			{
				return false;
			}
			try
			{
				if (atIsDisabled != null && (bool)atIsDisabled.Invoke(atPlugin, null))
				{
					return false;
				}
				atDisable.Invoke(atPlugin, null);
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static void ResumeAutoTranslate()
		{
			try
			{
				atEnable?.Invoke(atPlugin, null);
			}
			catch
			{
			}
		}

		internal static void Draw()
		{
			EnsureSuitePageHost();
			suitePageHost.ApplyDeferredSelection(Event.current.type == EventType.Layout);
			WardrobeClothesTab.ApplyPendingNumberChange(WardrobePlugin.GetSelectedSession());
			if (noSelectionStartup.ShouldRouteToMakeUp(WardrobeContext.GetSelectedCharacter() != null, suitePageHost.HasPage("makeup")))
			{
				suitePageHost.RequestPage("makeup");
				suitePageHost.ApplyDeferredSelection(Event.current.type == EventType.Layout);
			}
			WardrobeCelebration.ObserveMode(suitePageHost.ActivePageId);
			if (greyTex == null)
			{
				greyTex = new Texture2D(1, 1);
				greyTex.SetPixel(0, 0, new Color(0.53f, 0.53f, 0.53f));
				greyTex.Apply();
			}
			if (windowStyle == null)
			{
				winBgTex = new Texture2D(1, 1);
				winBgTex.SetPixel(0, 0, new Color(0.43f, 0.43f, 0.45f, 0.97f));
				winBgTex.Apply();
				windowStyle = new GUIStyle(GUI.skin.window);
				windowStyle.normal.background = winBgTex;
				windowStyle.onNormal.background = winBgTex;
				windowStyle.focused.background = winBgTex;
				windowStyle.onFocused.background = winBgTex;
			}
			EnsureUiFont();
			GUISkin skin = GUI.skin;
			GUISkin gUISkin = null;
			Font font = null;
			Matrix4x4 matrix = GUI.matrix;
			Color color = GUI.color;
			Color backgroundColor = GUI.backgroundColor;
			Color contentColor = GUI.contentColor;
			try
			{
				EnsureKoiSkin(skin);
				if (koiSkin != null)
				{
					GUI.skin = koiSkin;
				}
				GUI.skin = SuiteMakeUpTheme.GetPageSkin(GUI.skin, suitePageHost.ActivePageId);
				if (SuiteMakeUpTheme.IsAnyActive)
				{
					GUI.color = (GUI.backgroundColor = (GUI.contentColor = Color.white));
				}
				gUISkin = GUI.skin;
				font = gUISkin.font;
				if (!SuiteMakeUpTheme.IsAnyActive && uiFont != null)
				{
					gUISkin.font = uiFont;
				}
				EnsureShellStyleSkin();
				float num = ((suitePageHost == null) ? 0f : suitePageHost.GetActiveExtraWindowWidth());
				winRect.width = 500f + num;
				winRect.height = 830f;
				ornateWrapper = SuiteWrapperTheme.Available;
				uiScalingAvailable = suitePageHost == null || suitePageHost.ActivePageSupportsUiScale;
				uiScale = (uiScalingAvailable ? SuiteUiScalePolicy.ClampUserScale(requestedUiScale, Screen.width, Screen.height, InteractiveBounds.width + 478f, PointerBounds.height) : 1f);
				float num2 = SuiteUiScalePolicy.LogicalExtent(Screen.width, uiScale);
				float num3 = SuiteUiScalePolicy.LogicalExtent(Screen.height, uiScale);
				winRect.x = Mathf.Clamp(winRect.x, 4f, Mathf.Max(4f, num2 - winRect.width - 4f));
				winRect.y = Mathf.Clamp(winRect.y, 4f, Mathf.Max(4f, num3 - winRect.height - 4f));
				if (ornateWrapper)
				{
					float num4 = 88f;
					winRect.x = Mathf.Clamp(winRect.x, num4, Mathf.Max(num4, num2 - winRect.width - 60f - 4f));
					winRect.y = Mathf.Clamp(winRect.y, 52f, Mathf.Max(52f, num3 - winRect.height - 148f - 4f));
				}
				if (suitePageHost != null)
				{
					suitePageHost.SetActiveHostUiMetrics(uiScale, num2, num3);
				}
				GUI.matrix = matrix * Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
				if (suitePageHost != null)
				{
					winRect.width = 500f + num;
				}
				GUIStyle window = windowStyle;
				if (SuiteMakeUpTheme.IsAnyActive)
				{
					window = GUI.skin.window;
				}
				if (ornateWrapper)
				{
					Rect rect = GUI.Window(86042, PointerBounds, OrnateWindowFunc, GUIContent.none, GUIStyle.none);
					winRect.x = rect.x + 140f;
					winRect.y = rect.y + 48f;
				}
				else
				{
					winRect = GUI.Window(86042, winRect, WindowFunc, "1.0", window);
				}
				if (suitePageHost != null)
				{
					winRect.width = 500f + num;
				}
				Rect pointerBounds = PointerBounds;
				Rect frame = new Rect(winRect.x, winRect.y, winRect.width, pointerBounds.height);
				if (ornateWrapper)
				{
					frame = new Rect(winRect.x, winRect.y, 560f, 978f);
				}
				WardrobeCelebration.DrawModeDust(frame);
			}
			finally
			{
				GUI.matrix = matrix;
				GUI.color = color;
				GUI.backgroundColor = backgroundColor;
				GUI.contentColor = contentColor;
				if (gUISkin != null)
				{
					gUISkin.font = font;
				}
				GUI.skin = skin;
			}
		}

		private static void EnsureShellStyleSkin()
		{
			if (!(shellStyleSkin == GUI.skin))
			{
				shellStyleSkin = GUI.skin;
				tabStyle = (titleStyle = (creditStyle = (dressingStyle = (closeButtonStyle = null))));
				nameLabelStyle = (colHeaderStyle = (toastStyle = (emptySrcStyle = (suitePlusStyle = null))));
			}
		}

		private static void DrawHeader(WardrobeSession s)
		{
			if (ornateWrapper)
			{
				return;
			}
			if (titleStyle == null)
			{
				titleStyle = new GUIStyle(GUI.skin.label)
				{
					fontSize = 20,
					fontStyle = FontStyle.Bold,
					alignment = TextAnchor.MiddleCenter
				};
				dressingStyle = new GUIStyle(GUI.skin.label)
				{
					fontStyle = FontStyle.Bold,
					alignment = TextAnchor.MiddleLeft
				};
				creditStyle = new GUIStyle(GUI.skin.label)
				{
					fontStyle = FontStyle.Bold,
					alignment = TextAnchor.MiddleRight
				};
			}
			string text = ((suitePageHost == null) ? "dressup" : suitePageHost.ActivePageId);
			bool flag = suitePageHost != null && string.Equals(suitePageHost.ActivePageId, "cleanup", StringComparison.Ordinal);
			GUIContent content = new GUIContent(SuitePageLabels.DisplayName(text));
			Vector2 vector = titleStyle.CalcSize(content);
			float num = vector.x + 64f;
			Rect rect = GUILayoutUtility.GetRect(num, 30f, GUILayout.ExpandWidth(expand: true));
			float num2 = rect.x + (rect.width - num) / 2f;
			float y = rect.y + (rect.height - 26f) / 2f;
			Texture2D texture2D = WardrobeEmoji.Tex("ribbon");
			Color color = Color.white;
			if (string.Equals(text, "makeup", StringComparison.Ordinal))
			{
				Texture2D texture2D2 = WardrobeEmoji.Tex("lipstick");
				if (texture2D2 != null)
				{
					texture2D = texture2D2;
				}
			}
			else if (flag)
			{
				Texture2D texture2D3 = WardrobeEmoji.Tex("skull");
				if (texture2D3 != null)
				{
					texture2D = texture2D3;
				}
				color = SuiteCleanupRed;
			}
			if (texture2D != null)
			{
				GUI.DrawTexture(new Rect(num2, y, 26f, 26f), texture2D, ScaleMode.ScaleToFit);
				GUI.DrawTexture(new Rect(num2 + 26f + 6f + vector.x + 6f, y, 26f, 26f), texture2D, ScaleMode.ScaleToFit);
			}
			Color color2 = GUI.color;
			GUI.color = color;
			GUI.Label(new Rect(num2 + 26f + 6f, rect.y, vector.x, rect.height), content, titleStyle);
			GUI.color = color2;
			Rect rect2 = GUILayoutUtility.GetRect(10f, 24f, GUILayout.ExpandWidth(expand: true));
			if (SuiteProductPresentation.ShowPlusBadge(text, dressUpPlus: false, (suitePageHost == null) ? null : suitePageHost.MakeUpAssemblyName))
			{
				if (suitePlusStyle == null)
				{
					suitePlusStyle = new GUIStyle(GUI.skin.label)
					{
						fontSize = 13,
						fontStyle = FontStyle.Bold,
						alignment = TextAnchor.MiddleCenter
					};
				}
				GUIContent content2 = new GUIContent("Plus");
				Vector2 vector2 = suitePlusStyle.CalcSize(content2);
				Color color3 = GUI.color;
				GUI.color = (flag ? SuiteCleanupRed : new Color(1f, 0.84f, 0.4f));
				if (SuiteMakeUpTheme.IsAnyActive)
				{
					GUI.color = Color.white;
				}
				GUI.Label(new Rect(rect2.x + (rect2.width - vector2.x) / 2f, rect2.y - 3f, vector2.x, rect2.height), content2, suitePlusStyle);
				GUI.color = color3;
			}
			DrawSuiteModeIcons(rect2);
			Texture2D texture2D4 = WardrobeEmoji.Tex("patreon");
			GUIContent content3 = new GUIContent("Freelancer604");
			Vector2 vector3 = creditStyle.CalcSize(content3);
			Rect position = new Rect(rect2.xMax - 3f - 15f, rect2.y + (rect2.height - 15f) / 2f, 15f, 15f);
			GUI.Label(new Rect(position.x - 5f - vector3.x, rect2.y, vector3.x, rect2.height), content3, creditStyle);
			if (texture2D4 != null)
			{
				bool num3 = position.Contains(Event.current.mousePosition);
				Color color4 = GUI.color;
				if (num3)
				{
					GUI.color = new Color(1f, 0.72f, 0.35f, 0.95f);
					GUI.DrawTexture(new Rect(position.x - 12f, position.y - 12f, position.width + 24f, position.height + 24f), WardrobeCelebration.Glow, ScaleMode.StretchToFill);
					GUI.color = Color.white;
					DrawHoverOutline(new Rect(position.x - 3f, position.y - 3f, position.width + 6f, position.height + 6f));
				}
				else
				{
					GUI.color = new Color(1f, 1f, 1f, 0.8f);
				}
				GUI.DrawTexture(position, texture2D4, ScaleMode.ScaleToFit);
				GUI.color = color4;
				if (GUI.Button(position, GUIContent.none, GUIStyle.none))
				{
					Application.OpenURL("https://www.patreon.com/Freelancer604");
				}
			}
		}

		private static Rect SuiteModeOptRect(int i)
		{
			return new Rect(suiteModeBtnRect.x, suiteModeBtnRect.yMax + 2f + (float)i * 24f, 120f, 24f);
		}

		private static void DrawSuiteModeIcons(Rect row)
		{
			if (suitePageHost == null || !suitePageHost.SelectorVisible)
			{
				return;
			}
			suiteModeHoverState.Close();
			suiteModeOpen = false;
			for (int i = 0; i < suitePageHost.Pages.Count; i++)
			{
				SuiteHostedPage suiteHostedPage = suitePageHost.Pages[i];
				Rect rect = new Rect(row.x + 2f + (float)i * 28f, row.y - 4f, 24f, 24f);
				bool flag = rect.Contains(Event.current.mousePosition);
				if (GUI.Button(rect, new GUIContent(string.Empty, SuitePageLabels.DisplayName(suiteHostedPage.Id))))
				{
					pendingSuitePageSel = i;
				}
				Texture2D texture2D = SuiteModeIconTexture(suiteHostedPage.Id);
				if (texture2D != null)
				{
					Color color = GUI.color;
					GUI.color = (flag ? new Color(1.25f, 1.25f, 1.25f, 1f) : Color.white);
					GUI.DrawTexture(new Rect(rect.x + 3f, rect.y + 3f, 18f, 18f), texture2D, ScaleMode.ScaleToFit, alphaBlend: true);
					GUI.color = color;
				}
				if (string.Equals(suitePageHost.ActivePageId, suiteHostedPage.Id, StringComparison.Ordinal))
				{
					DrawModeIconOutline(rect, string.Equals(suiteHostedPage.Id, "cleanup", StringComparison.Ordinal));
				}
				else if (flag)
				{
					DrawHoverOutline(rect);
				}
			}
		}

		private static Texture2D SuiteModeIconTexture(string pageId)
		{
			if (string.Equals(pageId, "makeup", StringComparison.Ordinal))
			{
				return WardrobeEmoji.Tex("makeup");
			}
			if (string.Equals(pageId, "cleanup", StringComparison.Ordinal))
			{
				return WardrobeEmoji.Tex("skull");
			}
			return WardrobeEmoji.Tex("ribbon");
		}

		private static void DrawModeIconOutline(Rect rect, bool cleanup)
		{
			Color color = GUI.color;
			GUI.color = (cleanup ? SuiteCleanupRed : new Color(0.25f, 1f, 1f, 1f));
			GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(rect.x, rect.y, 2f, rect.height), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), Texture2D.whiteTexture);
			GUI.color = color;
		}

		private static void HandleSuiteModePopupInput()
		{
			if (!suiteModeOpen || suitePageHost == null)
			{
				return;
			}
			for (int i = 0; i < suitePageHost.Pages.Count; i++)
			{
				if (GUI.Button(SuiteModeOptRect(i), GUIContent.none, GUIStyle.none))
				{
					pendingSuitePageSel = i;
					suiteModeHoverState.Close();
					suiteModeOpen = false;
				}
			}
			if (Event.current.type == EventType.MouseDown)
			{
				bool flag = suiteModeBtnRect.Contains(Event.current.mousePosition);
				for (int j = 0; j < suitePageHost.Pages.Count; j++)
				{
					flag |= SuiteModeOptRect(j).Contains(Event.current.mousePosition);
				}
				if (!flag)
				{
					suiteModeHoverState.Close();
					suiteModeOpen = false;
				}
			}
		}

		private static void DrawSuiteModePopup()
		{
			if (!suiteModeOpen || suitePageHost == null)
			{
				return;
			}
			for (int i = 0; i < suitePageHost.Pages.Count; i++)
			{
				Rect rect = SuiteModeOptRect(i);
				Color color = GUI.color;
				GUI.color = Color.white;
				if (!SuiteMakeUpTheme.IsAnyActive)
				{
					GUI.DrawTexture(rect, Texture2D.blackTexture, ScaleMode.StretchToFill);
				}
				GUI.color = color;
				GUI.Box(rect, GUIContent.none);
				SuiteHostedPage suiteHostedPage = suitePageHost.Pages[i];
				bool flag = string.Equals(suitePageHost.ActivePageId, suiteHostedPage.Id, StringComparison.Ordinal);
				Color color2 = GUI.color;
				if (string.Equals(suiteHostedPage.Id, "cleanup", StringComparison.Ordinal))
				{
					GUI.color = SuiteCleanupRed;
				}
				GUI.Label(new Rect(rect.x + 6f, rect.y, rect.width - 8f, rect.height), (flag ? "● " : "   ") + SuitePageLabels.DisplayName(suitePageHost.Pages[i].Id), flag ? dressingStyle : new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleLeft
				});
				GUI.color = color2;
				if (rect.Contains(Event.current.mousePosition))
				{
					DrawHoverOutline(rect);
				}
			}
		}

		private static void ApplyPendingSuitePage()
		{
			if (pendingSuitePageSel < 0 || suitePageHost == null)
			{
				return;
			}
			int num = pendingSuitePageSel;
			pendingSuitePageSel = -1;
			if (num >= 0 && num < suitePageHost.Pages.Count)
			{
				string id = suitePageHost.Pages[num].Id;
				if (!string.Equals(id, suitePageHost.ActivePageId, StringComparison.Ordinal))
				{
					WardrobeWidgets.CollapseAll();
					winRect = new Rect(winRect.x, winRect.y, 500f, 830f);
					suitePageHost.RequestPage(id);
				}
			}
		}

		private static Rect LangOptRect(int i)
		{
			return new Rect(langBtnRect.x, langBtnRect.yMax + 2f + (float)i * 24f, 110f, 24f);
		}

		private static void HandleLangPopupInput()
		{
			if (!langOpen)
			{
				return;
			}
			for (int i = 0; i < WardrobeLang.NativeNames.Length; i++)
			{
				if (GUI.Button(LangOptRect(i), GUIContent.none, GUIStyle.none))
				{
					WardrobeLang.Set(i);
					langOpen = false;
				}
			}
			if (Event.current.type == EventType.MouseDown)
			{
				bool flag = langBtnRect.Contains(Event.current.mousePosition);
				for (int j = 0; j < WardrobeLang.NativeNames.Length; j++)
				{
					flag |= LangOptRect(j).Contains(Event.current.mousePosition);
				}
				if (!flag)
				{
					langOpen = false;
				}
			}
		}

		internal static void DrawLangPopup()
		{
			if (!langOpen)
			{
				return;
			}
			bool flag = SuppressAutoTranslate();
			try
			{
				for (int i = 0; i < WardrobeLang.NativeNames.Length; i++)
				{
					Rect rect = LangOptRect(i);
					if (!SuiteMakeUpTheme.IsAnyActive)
					{
						GUI.DrawTexture(rect, greyTex, ScaleMode.StretchToFill);
					}
					GUI.Box(rect, GUIContent.none);
					bool flag2 = i == WardrobeLang.Current;
					GUI.Label(new Rect(rect.x + 6f, rect.y, rect.width - 8f, rect.height), (flag2 ? "● " : "   ") + WardrobeLang.NativeNames[i], flag2 ? dressingStyle : new GUIStyle(GUI.skin.label)
					{
						alignment = TextAnchor.MiddleLeft
					});
					if (rect.Contains(Event.current.mousePosition))
					{
						DrawHoverOutline(rect);
					}
				}
			}
			finally
			{
				if (flag)
				{
					ResumeAutoTranslate();
				}
			}
		}

		private static void DrawHoverOutline(Rect r)
		{
			Color color = GUI.color;
			GUI.color = new Color(0.45f, 0.8f, 1f, 0.9f);
			GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1f), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(r.x, r.yMax - 1f, r.width, 1f), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(r.x, r.y, 1f, r.height), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(r.xMax - 1f, r.y, 1f, r.height), Texture2D.whiteTexture);
			GUI.color = color;
		}

		private static void DrawActiveOutline(Rect r)
		{
			Color color = GUI.color;
			GUI.color = new Color(0.4f, 1f, 0.55f, 1f);
			GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(r.x, r.y, 2f, r.height), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(r.xMax - 2f, r.y, 2f, r.height), Texture2D.whiteTexture);
			GUI.color = color;
		}

		private static Color PulseColor(float floorA)
		{
			return new Color(1f, 0.88f, 0.28f, floorA + (1f - floorA) * PulseT);
		}

		private static void DrawPulseBorder(Rect r)
		{
			Color color = GUI.color;
			GUI.color = PulseColor(0.5f);
			GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3f), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(r.x, r.yMax - 3f, r.width, 3f), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(r.x, r.y, 3f, r.height), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(r.xMax - 3f, r.y, 3f, r.height), Texture2D.whiteTexture);
			GUI.color = color;
		}

		private static void NavigateToClothes(int kind, int index = 0)
		{
			tab = 1;
			WardrobeClothesTab.FocusSlot(kind, index);
			WardrobeWidgets.CollapseAll();
			editorScroll = Vector2.zero;
			StartTabFlash(1);
		}

		private static void NavigateToAcc(int slot)
		{
			tab = 2;
			WardrobeAccTab.FocusSlot(slot);
			WardrobeWidgets.CollapseAll();
			editorScroll = Vector2.zero;
			StartTabFlash(2);
		}

		private static void StartTabFlash(int which)
		{
			flashTab = which;
			flashUntil = Time.realtimeSinceStartup + 1.5f;
		}

		private static void DrawTabs()
		{
			if (tabStyle == null)
			{
				tabStyle = new GUIStyle(GUI.skin.button)
				{
					fontStyle = FontStyle.Bold,
					alignment = TextAnchor.MiddleCenter,
					fontSize = 13
				};
			}
			Rect rect = GUILayoutUtility.GetRect(10f, 26f, GUILayout.ExpandWidth(expand: true), GUILayout.Height(26f));
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			float num = rect.width / (float)TabNames.Length;
			Color color = GUI.color;
			GUI.color = new Color(1f, 1f, 1f, 0.12f);
			GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Texture2D.whiteTexture);
			GUI.color = color;
			for (int i = 0; i < TabNames.Length; i++)
			{
				Rect position = new Rect(rect.x + (float)i * num, rect.y, num, rect.height);
				bool flag = tab == i;
				bool flag2 = false;
				if (i == flashTab)
				{
					float num2 = flashUntil - realtimeSinceStartup;
					if (num2 <= 0f)
					{
						flashTab = -1;
					}
					else
					{
						flag2 = num2 % 0.5f > 0.25f;
					}
				}
				Color color2 = GUI.color;
				GUI.color = (flag2 ? TabAccent : (flag ? Color.white : new Color(0.72f, 0.72f, 0.72f)));
				if (GUI.Button(position, WardrobeLang.T(TabNames[i]), tabStyle))
				{
					WardrobeWidgets.CollapseAll();
					if (!flag)
					{
						tab = i;
					}
				}
				GUI.color = color2;
				if (flag)
				{
					GUI.color = TabAccent;
					GUI.DrawTexture(new Rect(position.x + 3f, position.yMax - 3f, position.width - 6f, 3f), Texture2D.whiteTexture);
					GUI.color = color2;
				}
			}
		}

		private static void WindowFunc(int id)
		{
			bool flag = (WardrobeLang.Current != 0 || langOpen) && SuppressAutoTranslate();
			Color backgroundColor = GUI.backgroundColor;
			if (!SuiteMakeUpTheme.IsAnyActive)
			{
				GUI.backgroundColor = new Color(1.32f, 1.32f, 1.32f, 1f);
			}
			try
			{
				try
				{
					if (ornateWrapper)
					{
						DrawOrnateBody();
					}
					else
					{
						DrawTitleBarLanguage();
						WindowBody();
					}
				}
				catch (Exception ex)
				{
					WardrobePlugin.Log.LogError((object)ex);
				}
				try
				{
					DrawLangPopup();
					if (!ornateWrapper)
					{
						WardrobeCelebration.Draw(new Rect(0f, 0f, winRect.width, winRect.height));
						DrawToast();
					}
					DrawWindowCloseButton();
					DrawScaleHandle();
				}
				catch (Exception ex2)
				{
					WardrobePlugin.Log.LogError((object)ex2);
				}
				try
				{
					ApplyPendingSuitePage();
				}
				catch (Exception ex3)
				{
					WardrobePlugin.Log.LogError((object)("Suite page switch: " + ex3));
				}
				GUI.DragWindow(new Rect(EmptyLeftMargin, 0f, PointerBounds.width - EmptyLeftMargin, PointerBounds.height));
				ConsumeOwnedPointerEvent();
			}
			finally
			{
				GUI.backgroundColor = backgroundColor;
				if (flag)
				{
					ResumeAutoTranslate();
				}
				SuiteStudioUiVisibility.ObserveWindowFocus();
			}
		}

		private static void OrnateWindowFunc(int id)
		{
			WindowFunc(id);
		}

		private static void DrawOrnateBody()
		{
			string text = ((suitePageHost == null) ? "dressup" : suitePageHost.ActivePageId);
			SuiteWrapperTheme.Draw(text);
			SuiteWrapperTheme.DrawTitle(SuitePageLabels.DisplayName(text));
			SuiteWrapperTheme.DrawOrnaments();
			DrawTitleBarLanguage();
			float num = langBtnRect.xMax + 12f;
			float x = GUI.skin.label.CalcSize(new GUIContent("Freelancer604's Patreon")).x;
			Rect position = new Rect(num, 146f, x, 20f);
			bool num2 = position.Contains(Event.current.mousePosition);
			GUIStyle gUIStyle = new GUIStyle(GUI.skin.label);
			if (num2)
			{
				Color color = new Color(1f, 0.86f, 0.52f);
				gUIStyle.normal.textColor = color;
				gUIStyle.hover.textColor = color;
				Color color2 = GUI.color;
				GUI.color = color;
				GUI.DrawTexture(new Rect(position.x + 2f, position.yMax - 2f, position.width - 4f, 1f), Texture2D.whiteTexture);
				GUI.color = color2;
			}
			if (GUI.Button(position, new GUIContent("Freelancer604's Patreon", "Open Freelancer604's Patreon in your browser"), gUIStyle))
			{
				Application.OpenURL("https://www.patreon.com/Freelancer604");
			}
			float x2 = GUI.skin.label.CalcSize(new GUIContent("1.0")).x;
			GUI.Label(new Rect(num + x + 12f, 146f, x2, 20f), "1.0", GUI.skin.label);
			if (suitePageHost != null)
			{
				for (int i = 0; i < 6; i++)
				{
					Rect rect = new Rect(SuiteWrapperLayout.ModeButtonX(i), SuiteWrapperLayout.ModeButtonY(i), 24f, 28f);
					if (i < suitePageHost.Pages.Count)
					{
						SuiteHostedPage suiteHostedPage = suitePageHost.Pages[i];
						if (GUI.Button(rect, new GUIContent("", SuitePageLabels.DisplayName(suiteHostedPage.Id))))
						{
							pendingSuitePageSel = i;
						}
						Texture2D texture2D = SuiteModeIconTexture(suiteHostedPage.Id);
						if (texture2D != null)
						{
							GUI.DrawTexture(new Rect(rect.x + 3f, rect.y + 5f, 18f, 18f), texture2D, ScaleMode.ScaleToFit, alphaBlend: true);
						}
						if (text == suiteHostedPage.Id)
						{
							DrawModeIconOutline(rect, suiteHostedPage.Id == "cleanup");
						}
					}
					else
					{
						GUI.Box(rect, GUIContent.none, GUI.skin.button);
					}
				}
			}
			SuiteWrapperTheme.BeginBody();
			GUILayout.BeginArea(new Rect(180f, 198f, 480f, 682f));
			try
			{
				WindowBody();
				WardrobeCelebration.Draw(new Rect(0f, 0f, 480f, 620f));
				bottomBarTop = 682f;
				DrawToast();
			}
			finally
			{
				GUILayout.EndArea();
				SuiteWrapperTheme.EndBody();
			}
			GUILayout.BeginArea(new Rect(180f, 914f, 480f, 74f));
			try
			{
				SuiteWrapperTheme.DrawFooter();
			}
			finally
			{
				GUILayout.EndArea();
			}
		}

		private static void ConsumeOwnedPointerEvent()
		{
			Event current = Event.current;
			if (current != null && current.type != EventType.Used && (current.type == EventType.MouseDown || current.type == EventType.MouseUp || current.type == EventType.MouseDrag || current.type == EventType.ScrollWheel || current.type == EventType.ContextClick))
			{
				Rect rect = new Rect(EmptyLeftMargin, 0f, PointerBounds.width - EmptyLeftMargin, PointerBounds.height);
				if (rect.Contains(current.mousePosition) || WardrobePlugin.WindowOwnsPointerInput)
				{
					current.Use();
				}
			}
		}

		private static void DrawScaleHandle()
		{
			if (!uiScalingAvailable)
			{
				return;
			}
			float num = 16f;
			float num2 = 1f;
			if (ornateWrapper)
			{
				num2 = 15f;
			}
			Rect rect = new Rect(PointerBounds.width - num - num2, PointerBounds.height - num - num2, num, num);
			int controlID = GUIUtility.GetControlID(604106, FocusType.Passive);
			Event current = Event.current;
			switch (current.GetTypeForControl(controlID))
			{
			case EventType.MouseDown:
				if (current.button == 0 && rect.Contains(current.mousePosition))
				{
					GUIUtility.hotControl = controlID;
					scaleDragStart = uiScale;
					scaleDragPointer = Input.mousePosition;
					current.Use();
				}
				break;
			case EventType.MouseDrag:
				if (GUIUtility.hotControl == controlID)
				{
					Vector2 vector = (Vector2)Input.mousePosition - scaleDragPointer;
					requestedUiScale = SuiteUiScalePolicy.ClampUserScale(scaleDragStart + 0.5f * (vector.x / winRect.width - vector.y / winRect.height), Screen.width, Screen.height, InteractiveBounds.width + 478f, PointerBounds.height);
					current.Use();
				}
				break;
			case EventType.MouseUp:
				if (GUIUtility.hotControl == controlID)
				{
					GUIUtility.hotControl = 0;
					WardrobePlugin.SaveUiScale(requestedUiScale);
					current.Use();
				}
				break;
			}
			Texture2D texture2D = ScaleHandleTexture();
			if (texture2D != null)
			{
				float num3 = 12f;
				float num4 = 2f;
				if (ornateWrapper)
				{
					num4 = 16f;
				}
				Rect position = new Rect(PointerBounds.width - num3 - num4, PointerBounds.height - num3 - num4, num3, num3);
				Color color = GUI.color;
				GUI.color = Color.white;
				GUI.DrawTexture(position, texture2D);
				GUI.color = color;
			}
		}

		private static Texture2D ScaleHandleTexture()
		{
			if (scaleHandleTexture != null)
			{
				return scaleHandleTexture;
			}
			int num = 12;
			scaleHandleTexture = new Texture2D(num, num);
			scaleHandleTexture.name = "F604 uniform resize grip";
			scaleHandleTexture.filterMode = FilterMode.Point;
			scaleHandleTexture.wrapMode = TextureWrapMode.Clamp;
			Color color = new Color(0f, 0f, 0f, 0f);
			Color color2 = new Color(0.62f, 0.62f, 0.66f, 0.68f);
			for (int i = 0; i < num; i++)
			{
				for (int j = 0; j < num; j++)
				{
					scaleHandleTexture.SetPixel(j, i, SuiteResizeGripGeometry.IsVisiblePixel(j, i) ? color2 : color);
				}
			}
			scaleHandleTexture.Apply();
			return scaleHandleTexture;
		}

		private static void DrawWindowCloseButton()
		{
			if (closeButtonStyle == null)
			{
				closeButtonStyle = new GUIStyle(GUI.skin.button)
				{
					fontSize = 11,
					fontStyle = FontStyle.Bold,
					alignment = TextAnchor.MiddleCenter,
					padding = new RectOffset(0, 0, 0, 1)
				};
			}
			Rect position = new Rect(winRect.width - 27f, 1f, 22f, 20f);
			if (ornateWrapper)
			{
				position = SuiteWrapperTheme.CloseRect();
				if (GUI.Button(position, GUIContent.none, GUIStyle.none))
				{
					WardrobePlugin.SetWindowVisible(value: false);
				}
				SuiteWrapperTheme.DrawCloseGlyph();
			}
			else if (GUI.Button(position, "X", closeButtonStyle))
			{
				WardrobePlugin.SetWindowVisible(value: false);
			}
		}

		private static void DrawTitleBarLanguage()
		{
			if (dressingStyle == null)
			{
				dressingStyle = new GUIStyle(GUI.skin.label)
				{
					fontStyle = FontStyle.Bold,
					alignment = TextAnchor.MiddleLeft,
					padding = new RectOffset(2, 2, 0, 1)
				};
			}
			GUIContent content = new GUIContent(WardrobeLang.T("Language") + ": " + WardrobeLang.NativeNames[WardrobeLang.Current]);
			float width = Mathf.Min(dressingStyle.CalcSize(content).x + 6f, 180f);
			langBtnRect = new Rect(5f, 1f, width, 20f);
			if (ornateWrapper)
			{
				content = new GUIContent(WardrobeLang.NativeNames[WardrobeLang.Current] + " ▾");
				width = dressingStyle.CalcSize(content).x;
				float x = GUI.skin.label.CalcSize(new GUIContent("1.0")).x;
				float x2 = GUI.skin.label.CalcSize(new GUIContent("Freelancer604's Patreon")).x;
				float num = width + 12f + x2 + 12f + x;
				langBtnRect = new Rect(SuiteWrapperTheme.TitleGemCenter - 10f - num * 0.5f, 146f, width, 20f);
			}
			if (GUI.Button(langBtnRect, content, dressingStyle))
			{
				langOpen = !langOpen;
			}
			if (langBtnRect.Contains(Event.current.mousePosition) || langOpen)
			{
				DrawHoverOutline(langBtnRect);
			}
			HandleLangPopupInput();
		}

		private static void PublishHostWindowRect()
		{
			float num = winRect.width;
			float num2 = winRect.height;
			if (ornateWrapper)
			{
				num += 60f;
				num2 += 148f;
			}
			suitePageHost.SetActiveHostWindowRect(winRect.x, winRect.y, num, num2);
		}

		private static void WindowBody()
		{
			WardrobeSession s = WardrobePlugin.GetSelectedSession();
			if (s == null || !(s.cha != null))
			{
				DrawHeader(null);
				if (suitePageHost != null && string.Equals(suitePageHost.ActivePageId, "makeup", StringComparison.Ordinal))
				{
					PublishHostWindowRect();
					suitePageHost.DrawActivePage();
				}
				else
				{
					GUILayout.Space(20f);
					GUILayout.Label(WardrobeLang.T(WardrobeContext.InMaker ? "Loading the character..." : "Select a character in the Studio workspace."));
					GUILayout.Space(20f);
				}
				return;
			}
			if (messageTimer > 0f)
			{
				messageTimer -= Time.deltaTime;
			}
			if (baselineDeadline > 0f && Time.realtimeSinceStartup >= baselineDeadline && Event.current.type == EventType.Layout)
			{
				baselineDeadline = 0f;
				s.SetBaseline();
				SetMessage(WardrobeLang.T("OG baseline set."));
			}
			if (saveDeadline > 0f && Time.realtimeSinceStartup >= saveDeadline && Event.current.type == EventType.Layout)
			{
				saveDeadline = 0f;
				if (SaveClothesCard(s))
				{
					WardrobeCelebration.Begin(previewTex, snapshotRect, s.LiveTopColor());
				}
			}
			DrawHeader(s);
			if (!string.Equals(suitePageHost.ActivePageId, "dressup", StringComparison.Ordinal))
			{
				PublishHostWindowRect();
				suitePageHost.DrawActivePage();
				return;
			}
			DrawTabs();
			GUILayout.Space(2f);
			if (tab != 0)
			{
				if (WardrobeCardBrowser.IsImagePick)
				{
					WardrobeCardBrowser.Draw(s);
					DrawBottomBar(s, mixerRow: false);
					return;
				}
				editorScroll = GUILayout.BeginScrollView(editorScroll, false, true);
				if (tab == 1)
				{
					WardrobeClothesTab.Draw(s);
				}
				else
				{
					WardrobeAccTab.Draw(s);
				}
				GUILayout.EndScrollView();
				DrawBottomBar(s, mixerRow: false);
				return;
			}
			if (WardrobeCardBrowser.IsOpen)
			{
				WardrobeCardBrowser.Draw(s);
				return;
			}
			float num = winRect.width * 0.29f;
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUILayout.BeginVertical();
			DrawTargetPortrait((s.t1Tex != null) ? s.t1Tex : greyTex, num, 1);
			if (Event.current.type == EventType.Repaint)
			{
				nameRowLeft = GUILayoutUtility.GetLastRect().x;
			}
			bool enabled = GUI.enabled;
			GUI.enabled = s.t1 != null;
			if (GUILayout.Button(WardrobeLang.T("Apply T1"), GUILayout.Width(num)))
			{
				s.PushUndo();
				s.ApplyWholeCard(2);
				SetMessage(WardrobeLang.T("T1 outfit applied."));
			}
			GUI.enabled = enabled;
			if (GUILayout.Button(WardrobeEmoji.C("dice", " " + WardrobeLang.T("Random")), GUILayout.Width(num)))
			{
				PickRandomCard(s, 2);
			}
			GUILayout.EndVertical();
			GUILayout.Space(4f);
			GUILayout.BeginVertical();
			DrawTargetPortrait((s.t2Tex != null) ? s.t2Tex : greyTex, num, 2);
			GUI.enabled = s.t2 != null;
			if (GUILayout.Button(WardrobeLang.T("Apply T2"), GUILayout.Width(num)))
			{
				s.PushUndo();
				s.ApplyWholeCard(3);
				SetMessage(WardrobeLang.T("T2 outfit applied."));
			}
			GUI.enabled = enabled;
			if (GUILayout.Button(WardrobeEmoji.C("dice", " " + WardrobeLang.T("Random")), GUILayout.Width(num)))
			{
				PickRandomCard(s, 3);
			}
			GUILayout.EndVertical();
			GUILayout.Space(4f);
			GUILayout.BeginVertical();
			bool flag = saveDeadline > 0f;
			float num2 = (flag ? (saveDeadline - Time.realtimeSinceStartup) : 0f);
			Rect rect = GUILayoutUtility.GetRect(num, num * 1.333f, GUI.skin.box, GUILayout.Width(num), GUILayout.Height(num * 1.333f));
			if (Event.current.type == EventType.Repaint)
			{
				snapshotRect = rect;
			}
			Rect rect2 = rect;
			float num3 = 0f;
			float num4 = 0f;
			if (flag)
			{
				float num5 = 1f - Mathf.Clamp01(num2 / 3f);
				float realtimeSinceStartup = Time.realtimeSinceStartup;
				num4 = 0.5f + 0.5f * Mathf.Sin(realtimeSinceStartup * 10f);
				num3 = num5 * num5 * (0.55f + 0.45f * num4);
				Color color = GUI.color;
				GUI.color = new Color(1f, 0.85f, 0.4f, 0.35f * num3);
				float num6 = 8f + 10f * num5;
				GUI.DrawTexture(new Rect(rect.x - num6, rect.y - num6, rect.width + 2f * num6, rect.height + 2f * num6), WardrobeCelebration.Glow, ScaleMode.StretchToFill);
				GUI.color = color;
				float num7 = 4.5f * num5 * num5;
				rect2.x += Mathf.Sin(realtimeSinceStartup * 47f) * num7;
				rect2.y += Mathf.Cos(realtimeSinceStartup * 61f) * num7;
			}
			GUI.Box(rect2, (previewTex != null) ? previewTex : greyTex);
			if (flag)
			{
				Color color2 = GUI.color;
				GUI.color = Color.Lerp(new Color(1f, 0.78f, 0.3f, num3), new Color(1f, 1f, 0.9f, num3), num4);
				GUI.DrawTexture(rect2, WardrobeCelebration.EdgeGlow, ScaleMode.StretchToFill);
				GUI.color = color2;
			}
			else
			{
				WardrobeCelebration.DrawFlourish(rect2);
			}
			if (GUILayout.Button(WardrobeEmoji.C("camera", " " + WardrobeLang.T("Snapshot")), GUILayout.Width(num)))
			{
				((MonoBehaviour)(object)WardrobePlugin.Instance).StartCoroutine(CaptureSnapshot());
			}
			Color color3 = GUI.color;
			if (flag && num2 % 0.5f > 0.25f)
			{
				GUI.color = new Color(1f, 1f, 0.4f);
			}
			if (GUILayout.Button(WardrobeEmoji.C("save", " " + (flag ? string.Format(WardrobeLang.T("Saving in {0}s..."), Mathf.CeilToInt(num2)) : WardrobeLang.T("Save Clothes"))), GUILayout.Width(num)))
			{
				if (flag)
				{
					saveDeadline = 0f;
					SetMessage(WardrobeLang.T("Save cancelled."));
				}
				else
				{
					saveDeadline = Time.realtimeSinceStartup + 3f;
				}
			}
			GUI.color = color3;
			if (Event.current.type == EventType.Repaint)
			{
				nameRowRight = GUILayoutUtility.GetLastRect().xMax;
			}
			GUILayout.EndVertical();
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.Space(8f);
			Rect rect3 = GUILayoutUtility.GetRect(10f, 22f, GUILayout.ExpandWidth(expand: true));
			float num8 = ((nameRowLeft > 0f) ? nameRowLeft : (rect3.x + 8f));
			float num9 = ((nameRowRight > num8 + 140f) ? nameRowRight : (rect3.xMax - 8f));
			if (nameLabelStyle == null)
			{
				nameLabelStyle = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleLeft
				};
			}
			GUIContent content = new GUIContent(WardrobeLang.T("Outfit Name:"));
			float num10 = nameLabelStyle.CalcSize(content).x + 3f;
			GUI.Label(new Rect(num8, rect3.y, num10, rect3.height), content, nameLabelStyle);
			cardName = GUI.TextField(new Rect(num8 + num10 + 3f, rect3.y, num9 - (num8 + num10 + 3f), rect3.height), cardName);
			GUILayout.Space(8f);
			scroll = GUILayout.BeginScrollView(scroll, false, true);
			GUILayout.BeginVertical(GUI.skin.box);
			DrawColumnHeaders();
			DrawStdRow(s, "Clothes", WardrobeEmoji.C("clothes", " " + WardrobeLang.T("Clothes")), null, null, null, delegate(int n)
			{
				s.ApplyAllClothes(n);
			}, -1);
			DrawStdRow(s, "Accessories", WardrobeEmoji.C("ring", " " + WardrobeLang.T("Accessories")), null, null, null, delegate(int n)
			{
				s.ApplyAllAccessories(n);
			}, -1);
			DrawStdRow(s, "Colors", WardrobeEmoji.C("palette", " " + WardrobeLang.T("Colors")), null, null, null, delegate(int n)
			{
				s.ApplyColors(n);
			}, -1);
			GUILayout.Space(6f);
			if (Event.current.type == EventType.Layout)
			{
				if (renderClothingCounts == null)
				{
					renderClothingCounts = new int[9];
				}
				for (int num11 = 0; num11 < renderClothingCounts.Length; num11++)
				{
					renderClothingCounts[num11] = Mathf.Clamp(Mathf.Max(Mathf.Max(s.GetClothingSlotCount(num11), s.GetClothingSlotCount(num11, 1)), Mathf.Max(s.GetClothingSlotCount(num11, 2), s.GetClothingSlotCount(num11, 3))), 1, 5);
				}
			}
			for (int num12 = 0; num12 < 9; num12++)
			{
				int k = num12;
				DrawStdRow(s, WardrobeSession.ClothesSlotNames[num12], WardrobeEmoji.SlotLabel(num12), s.LiveClothesThumb(num12), delegate
				{
					NavigateToClothes(k);
				}, (int sel) => s.SrcClothesThumb(sel, k), delegate(int n)
				{
					s.ApplyClothesSlot(k, n);
				}, k);
				for (int num13 = 1; num13 < renderClothingCounts[num12]; num13++)
				{
					DrawExtraClothingRow(s, k, num13);
				}
			}
			GUILayout.Space(6f);
			if (Event.current.type == EventType.Layout)
			{
				renderAccSlots = s.AccUnionSlots();
			}
			DrawAccMoveHeader(s, renderAccSlots);
			foreach (int renderAccSlot in renderAccSlots)
			{
				DrawAccRow(s, renderAccSlot);
			}
			DrawAccPlusRow(s, renderAccSlots);
			GUILayout.EndVertical();
			GUILayout.EndScrollView();
			if (pendingMove != null)
			{
				Action action = pendingMove;
				pendingMove = null;
				action();
			}
			DrawBottomBar(s, mixerRow: true);
		}

		private static void DrawTargetPortrait(Texture2D texture, float width, int target)
		{
			string text = WardrobeLang.T((target == 1) ? "Set Target 1" : "Set Target 2");
			if (GUILayout.Button(new GUIContent(texture, text), GUI.skin.box, GUILayout.Width(width), GUILayout.Height(width * 1.333f)))
			{
				WardrobeCardBrowser.Open(target + 1, CoordinateDir);
			}
			Rect lastRect = GUILayoutUtility.GetLastRect();
			if (Event.current.type == EventType.Repaint && lastRect.Contains(Event.current.mousePosition))
			{
				Color color = GUI.color;
				GUI.color = new Color(0.35f, 0.7f, 1f, 0.8f);
				GUI.DrawTexture(lastRect, WardrobeCelebration.EdgeGlow, ScaleMode.StretchToFill);
				GUI.color = color;
				GUIStyle gUIStyle = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleCenter,
					fontStyle = FontStyle.Bold,
					wordWrap = true,
					padding = new RectOffset()
				};
				Rect position = new Rect(lastRect.x + 6f, lastRect.y + lastRect.height * 0.5f - 22f, lastRect.width - 12f, 44f);
				gUIStyle.normal.textColor = Color.black;
				GUI.Label(new Rect(position.x + 1f, position.y + 1f, position.width, position.height), text, gUIStyle);
				gUIStyle.normal.textColor = Color.white;
				GUI.Label(position, text, gUIStyle);
			}
		}

		private static void DrawBottomBar(WardrobeSession s, bool mixerRow)
		{
			bool itemRow = !mixerRow && (tab == 1 || tab == 2);
			bool accessoryRow = tab == 2;
			int itemKind = (accessoryRow ? WardrobeAccTab.SelectedSlot : WardrobeClothesTab.SelectedSlot);
			string text = WardrobeLang.T(accessoryRow ? "Accessory" : WardrobeSession.ClothesSlotNames[itemKind]);
			int clothingIndex = ((!accessoryRow) ? WardrobeClothesTab.SelectedNumber : 0);
			if (!accessoryRow && clothingIndex > 0)
			{
				text = text + " " + (clothingIndex + 1);
			}
			Action action = delegate
			{
				if (accessoryRow)
				{
					s.CopyAccessoryItem(itemKind);
				}
				else
				{
					s.CopyClothesItem(itemKind, clothingIndex);
				}
			};
			Action action2 = delegate
			{
				if (accessoryRow)
				{
					s.PasteAccessoryItem(itemKind);
				}
				else
				{
					s.PasteClothesItem(itemKind, clothingIndex);
				}
			};
			bool flag = (accessoryRow ? s.CanPasteAccessoryItem(itemKind) : s.CanPasteClothesItem(itemKind, clothingIndex));
			if (SuiteWrapperTheme.DeferFooter(delegate
			{
				DrawBottomBar(s, mixerRow);
			}))
			{
				return;
			}
			if (ornateWrapper)
			{
				Rect area = GUILayoutUtility.GetRect(480f, 74f, GUILayout.Width(480f), GUILayout.Height(74f));
				GUIStyle style = new GUIStyle(GUI.skin.button)
				{
					fixedWidth = 0f,
					fixedHeight = 0f,
					margin = new RectOffset(),
					border = new RectOffset(6, 6, 6, 6),
					alignment = TextAnchor.MiddleCenter,
					clipping = TextClipping.Clip
				};
				Func<int, int, Rect> func = (int column, int row) => new Rect(area.x + 12f + (float)column * 154.66667f, area.y + ((mixerRow || itemRow) ? 7f : 24f) + (float)row * 34f, 146.66667f, 26f);
				bool enabled = GUI.enabled;
				Color color = GUI.color;
				try
				{
					if (mixerRow)
					{
						if (GUI.Button(func(0, 0), new GUIContent(WardrobeLang.T("Copy Outfit"), "Copy this character's full outfit (clothes, accessories, Material Editor) — then select another character and Paste Outfit."), style))
						{
							s.CopyOutfit();
						}
						GUI.enabled = WardrobeSession.OutfitClipboard != null;
						if (GUI.Button(func(1, 0), new GUIContent(WardrobeLang.T("Paste Outfit"), "Paste the copied outfit onto this character (clothes, accessories, Material Editor)."), style))
						{
							s.PushUndo();
							s.PasteOutfit();
						}
						GUI.enabled = enabled;
						if (GUI.Button(func(2, 0), WardrobeLang.T("Reset"), style))
						{
							s.PushUndo();
							s.ResetToBaseline();
						}
					}
					if (itemRow)
					{
						if (GUI.Button(new Rect(area.x + 12f, area.y + 7f, 224f, 26f), WardrobeLang.T("Copy") + " " + text, style))
						{
							action();
						}
						GUI.enabled = enabled && flag;
						if (GUI.Button(new Rect(area.x + 244f, area.y + 7f, 224f, 26f), WardrobeLang.T("Paste") + " " + text, style))
						{
							action2();
						}
						GUI.enabled = enabled;
					}
					int arg = ((mixerRow || itemRow) ? 1 : 0);
					GUI.enabled = s.CanUndo;
					if (GUI.Button(func(0, arg), string.Format("◀ {0} ({1})", WardrobeLang.T("Undo"), s.UndoCount), style))
					{
						s.UndoStep();
					}
					GUI.enabled = true;
					if (tab == 0)
					{
						bool flag2 = baselineDeadline > 0f;
						float num = (flag2 ? (baselineDeadline - Time.realtimeSinceStartup) : 0f);
						GUI.color = ((flag2 && num % 0.5f > 0.25f) ? new Color(1f, 1f, 0.4f) : Color.white);
						string text2 = (flag2 ? string.Format(WardrobeLang.T("Saving Baseline {0}s..."), Mathf.CeilToInt(num)) : WardrobeLang.T("Set OG Baseline"));
						if (GUI.Button(func(1, arg), new GUIContent(text2, "Cement the current outfit as the new OG / [1] / Reset reference. Press to start a short countdown; press again to cancel."), style))
						{
							if (flag2)
							{
								baselineDeadline = 0f;
								SetMessage(WardrobeLang.T("Baseline save cancelled."));
							}
							else
							{
								baselineDeadline = Time.realtimeSinceStartup + 3f;
							}
						}
						GUI.color = Color.white;
					}
					else if (GUI.Button(func(1, arg), new GUIContent("◀ " + WardrobeLang.T("Back To Home"), "Return to the Mix tab."), style))
					{
						tab = 0;
						WardrobeWidgets.CollapseAll();
						editorScroll = Vector2.zero;
						StartTabFlash(0);
					}
					GUI.enabled = s.CanRedo;
					if (GUI.Button(func(2, arg), string.Format("{0} ({1}) ▶", WardrobeLang.T("Redo"), s.RedoCount), style))
					{
						s.RedoStep();
					}
					return;
				}
				finally
				{
					GUI.enabled = enabled;
					GUI.color = color;
				}
			}
			if (Event.current.type == EventType.Repaint && !ornateWrapper)
			{
				bottomBarTop = GUILayoutUtility.GetLastRect().yMax;
			}
			GUILayout.BeginVertical(GUI.skin.box);
			if (itemRow)
			{
				GUILayout.BeginHorizontal();
				if (GUILayout.Button(WardrobeLang.T("Copy") + " " + text, GUILayout.Height(28f)))
				{
					action();
				}
				bool enabled2 = GUI.enabled;
				GUI.enabled = enabled2 && flag;
				if (GUILayout.Button(WardrobeLang.T("Paste") + " " + text, GUILayout.Height(28f)))
				{
					action2();
				}
				GUI.enabled = enabled2;
				GUILayout.EndHorizontal();
			}
			if (mixerRow)
			{
				GUILayout.BeginHorizontal();
				if (GUILayout.Button(new GUIContent(WardrobeLang.T("Copy Outfit"), "Copy this character's full outfit (clothes, accessories, Material Editor) — then select another character and Paste Outfit."), GUILayout.Height(28f)))
				{
					s.CopyOutfit();
				}
				bool enabled3 = GUI.enabled;
				GUI.enabled = WardrobeSession.OutfitClipboard != null;
				if (GUILayout.Button(new GUIContent(WardrobeLang.T("Paste Outfit"), "Paste the copied outfit onto this character (clothes, accessories, Material Editor)."), GUILayout.Height(28f)))
				{
					s.PushUndo();
					s.PasteOutfit();
				}
				GUI.enabled = enabled3;
				if (GUILayout.Button(WardrobeLang.T("Reset"), GUILayout.Height(28f)))
				{
					s.PushUndo();
					s.ResetToBaseline();
				}
				GUILayout.EndHorizontal();
			}
			GUILayout.BeginHorizontal();
			bool enabled4 = GUI.enabled;
			GUI.enabled = s.CanUndo;
			if (GUILayout.Button(string.Format("◀ {0} ({1})", WardrobeLang.T("Undo"), s.UndoCount), GUILayout.Height(28f)))
			{
				s.UndoStep();
			}
			GUI.enabled = true;
			if (tab == 0)
			{
				bool flag3 = baselineDeadline > 0f;
				float num2 = (flag3 ? (baselineDeadline - Time.realtimeSinceStartup) : 0f);
				GUI.color = ((flag3 && num2 % 0.5f > 0.25f) ? new Color(1f, 1f, 0.4f) : Color.white);
				if (GUILayout.Button(new GUIContent(flag3 ? string.Format(WardrobeLang.T("Saving Baseline {0}s..."), Mathf.CeilToInt(num2)) : WardrobeLang.T("Set OG Baseline"), "Cement the current outfit as the new OG / [1] / Reset reference. Press to start a short countdown; press again to cancel."), GUILayout.Height(28f), GUILayout.Width(winRect.width * 0.3f)))
				{
					if (flag3)
					{
						baselineDeadline = 0f;
						SetMessage(WardrobeLang.T("Baseline save cancelled."));
					}
					else
					{
						baselineDeadline = Time.realtimeSinceStartup + 3f;
					}
				}
				GUI.color = Color.white;
			}
			else if (GUILayout.Button(new GUIContent("◀ " + WardrobeLang.T("Back To Home"), "Return to the Mix tab."), GUILayout.Height(28f), GUILayout.Width(winRect.width * 0.3f)))
			{
				tab = 0;
				WardrobeWidgets.CollapseAll();
				editorScroll = Vector2.zero;
				StartTabFlash(0);
			}
			GUI.enabled = s.CanRedo;
			if (GUILayout.Button(string.Format("{0} ({1}) ▶", WardrobeLang.T("Redo"), s.RedoCount), GUILayout.Height(28f)))
			{
				s.RedoStep();
			}
			GUI.enabled = enabled4;
			GUILayout.EndHorizontal();
			GUILayout.EndVertical();
		}

		private static void DrawToast()
		{
			if (!(messageTimer <= 0f) && !string.IsNullOrEmpty(message) && !(bottomBarTop <= 0f))
			{
				if (toastStyle == null)
				{
					toastStyle = new GUIStyle(GUI.skin.label)
					{
						alignment = TextAnchor.MiddleCenter,
						wordWrap = true,
						fontStyle = FontStyle.Bold
					};
				}
				GUIContent content = new GUIContent(message);
				float num = Mathf.Min(winRect.width - 16f, Mathf.Max(140f, toastStyle.CalcSize(content).x + 20f));
				float num2 = toastStyle.CalcHeight(content, num) + 8f;
				Rect position = new Rect((winRect.width - num) / 2f, bottomBarTop - num2 - 2f, num, num2);
				Color color = GUI.color;
				GUI.color = new Color(0f, 0f, 0f, 0.82f);
				GUI.DrawTexture(position, Texture2D.whiteTexture, ScaleMode.StretchToFill);
				GUI.color = color;
				GUI.Box(position, GUIContent.none);
				GUI.Label(position, content, toastStyle);
			}
		}

		private static void EnsureColStyles()
		{
			if (colHeaderStyle == null || colStylesLang != WardrobeLang.Current)
			{
				colStylesLang = WardrobeLang.Current;
				colHeaderStyle = new GUIStyle(GUI.skin.button)
				{
					fontStyle = FontStyle.Bold,
					alignment = TextAnchor.MiddleCenter
				};
				colHeaderStyle.normal.background = null;
				colHeaderStyle.hover.background = null;
				colHeaderStyle.active.background = null;
				colHeaderStyle.onNormal.background = null;
				colHeaderStyle.normal.textColor = GUI.skin.label.normal.textColor;
				clearColW = GUI.skin.button.CalcSize(new GUIContent(WardrobeLang.T("[None]"))).x;
				clearColW = Mathf.Max(clearColW, colHeaderStyle.CalcSize(new GUIContent(WardrobeLang.T("Clear") + "/+")).x);
			}
		}

		private static void DrawColumnHeaders()
		{
			DrawMixerHeadersHS2();
		}

		private static bool RowThumb(Texture2D tex, bool pulse = false)
		{
			float height = GUI.skin.button.CalcHeight(new GUIContent("[1]"), 40f);
			Rect rect = GUILayoutUtility.GetRect(20f, height, GUI.skin.button, GUILayout.Width(20f), GUILayout.Height(height));
			bool result = GUI.Button(rect, GUIContent.none, GUIStyle.none);
			GUI.Box(rect, GUIContent.none);
			if (tex != null)
			{
				GUI.DrawTexture(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f), tex, ScaleMode.ScaleToFit);
			}
			if (pulse)
			{
				Color color = GUI.color;
				GUI.color = PulseColor(0.12f);
				GUI.DrawTexture(rect, Texture2D.whiteTexture);
				GUI.color = color;
				DrawPulseBorder(rect);
				return result;
			}
			if (rect.Contains(Event.current.mousePosition))
			{
				DrawHoverOutline(rect);
			}
			return result;
		}

		private static bool SourceThumbButton(Texture2D tex, bool active, bool enabled, bool pulse)
		{
			if (emptySrcStyle == null)
			{
				emptySrcStyle = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleCenter
				};
			}
			float height = GUI.skin.button.CalcHeight(new GUIContent("[1]"), 40f);
			Rect rect = GUILayoutUtility.GetRect(40f, height, GUI.skin.button, GUILayout.Width(40f), GUILayout.Height(height));
			bool enabled2 = GUI.enabled;
			GUI.enabled = enabled;
			bool num = GUI.Button(rect, GUIContent.none);
			if (tex != null)
			{
				GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), tex, ScaleMode.ScaleToFit);
			}
			else if (enabled)
			{
				GUI.Label(rect, "—", emptySrcStyle);
			}
			if (pulse)
			{
				DrawPulseBorder(rect);
			}
			else if (active)
			{
				DrawActiveOutline(rect);
			}
			else if (rect.Contains(Event.current.mousePosition) && enabled)
			{
				DrawHoverOutline(rect);
			}
			GUI.enabled = enabled2;
			return num && enabled;
		}

		private static bool SourceNumberButton(int source, bool active, bool enabled, bool pulse)
		{
			float height = GUI.skin.button.CalcHeight(new GUIContent("[1]"), 40f);
			Rect rect = GUILayoutUtility.GetRect(40f, height, GUI.skin.button, GUILayout.Width(40f), GUILayout.Height(height));
			bool enabled2 = GUI.enabled;
			GUI.enabled = enabled;
			bool num = GUI.Button(rect, "[" + source + "]");
			if (pulse)
			{
				DrawPulseBorder(rect);
			}
			else if (active)
			{
				DrawActiveOutline(rect);
			}
			else if (rect.Contains(Event.current.mousePosition) && enabled)
			{
				DrawHoverOutline(rect);
			}
			GUI.enabled = enabled2;
			return num && enabled;
		}

		private static void DrawStdRow(WardrobeSession s, string key, GUIContent label, Texture2D thumb, Action thumbClick, Func<int, Texture2D> srcThumb, Action<int> apply, int randomKind)
		{
			DrawMixerStandardHS2(s, key, label, thumb, thumbClick, srcThumb, apply, randomKind);
		}

		private static void DrawExtraClothingRow(WardrobeSession s, int kind, int index)
		{
			DrawMixerExtraHS2(s, kind, index);
		}

		private static void DrawAccRow(WardrobeSession s, int slot)
		{
			DrawMixerAccessoryHS2(s, slot);
		}

		private static void MoveBegin()
		{
			pendingMove = delegate
			{
				moveState = 1;
				SetMessage(WardrobeLang.T("Pick a glowing accessory to copy."));
			};
		}

		private static void MoveCancel()
		{
			pendingMove = delegate
			{
				moveState = 0;
			};
		}

		private static void MovePickSourceAt(int sel, int slot)
		{
			pendingMove = delegate
			{
				moveSrcSel = sel;
				moveSrcSlot = slot;
				moveState = 2;
				SetMessage(WardrobeLang.T("Now pick an empty slot (or +) to drop it into."));
			};
		}

		private static void MoveDeposit(WardrobeSession s, int destSlot)
		{
			pendingMove = delegate
			{
				s.PushUndo();
				SetMessage(s.CopyAccessoryToSlot(moveSrcSel, moveSrcSlot, destSlot) ? WardrobeLang.T("Accessory copied to the new slot.") : WardrobeLang.T("Couldn't copy — target slot isn't empty."));
				moveState = 0;
			};
		}

		private static void MoveDepositNewSlot(WardrobeSession s)
		{
			pendingMove = delegate
			{
				byte[] array = s.SaveCoordinateBytes();
				int num = s.TryAddAccessorySlot();
				bool flag = num >= 0;
				if (flag)
				{
					if (array != null)
					{
						s.PushUndoBytes(array);
					}
					else
					{
						s.PushUndo();
					}
					flag = s.CopyAccessoryToSlot(moveSrcSel, moveSrcSlot, num);
				}
				SetMessage(flag ? WardrobeLang.T("Accessory copied to the new slot.") : WardrobeLang.T("Couldn't add a new accessory slot."));
				moveState = 0;
			};
		}

		private static void DrawAccMoveHeader(WardrobeSession s, List<int> accSlots)
		{
			if (accSlots.Count == 0)
			{
				if (moveState != 0)
				{
					MoveCancel();
				}
				return;
			}
			GUILayout.BeginHorizontal();
			GUILayout.Label(WardrobeLang.T("Accessories (populated slots)"), new GUIStyle(GUI.skin.label)
			{
				fontStyle = FontStyle.Italic
			});
			GUILayout.FlexibleSpace();
			bool flag = moveState != 0;
			Color color = GUI.color;
			if (flag)
			{
				GUI.color = new Color(1f, 0.72f, 0.4f);
			}
			if (GUILayout.Button(new GUIContent(flag ? WardrobeLang.T("Cancel Move") : WardrobeLang.T("Move Accessory"), "Copy an accessory from OG/T1/T2 into a different empty slot — keeps its colors, position and Material Editor shaders."), GUILayout.Width(130f)))
			{
				if (flag)
				{
					MoveCancel();
				}
				else
				{
					MoveBegin();
				}
			}
			GUI.color = color;
			if (accTrailGap > 0f)
			{
				GUILayout.Space(accTrailGap);
			}
			GUILayout.EndHorizontal();
		}

		private static void DrawAccPlusRow(WardrobeSession s, List<int> accSlots)
		{
			if (moveState != 2)
			{
				return;
			}
			int num = s.FirstFreeAccSlot(accSlots);
			bool flag = s.ShouldAppendAccessorySlot(num);
			if (num < 0 && !flag)
			{
				return;
			}
			Color color = GUI.color;
			GUI.color = PulseColor(0.55f);
			bool num2 = GUILayout.Button(new GUIContent("+  " + WardrobeLang.T("New accessory slot"), "Drop the accessory into a brand-new row (the next free slot)."), GUILayout.Height(24f));
			GUI.color = color;
			if (num2)
			{
				if (flag)
				{
					MoveDepositNewSlot(s);
				}
				else
				{
					MoveDeposit(s, num);
				}
			}
		}

		private static void PickRandomCard(WardrobeSession s, int which)
		{
			try
			{
				if (!Directory.Exists(CoordinateDir))
				{
					SetMessage(WardrobeLang.T("Coordinate folder not found."));
					return;
				}
				string[] files = Directory.GetFiles(CoordinateDir, "*.png", SearchOption.AllDirectories);
				if (files.Length == 0)
				{
					SetMessage(WardrobeLang.T("No coordinate cards found."));
					return;
				}
				string path = files[UnityEngine.Random.Range(0, files.Length)];
				if (s.LoadTargetCard(which, path))
				{
					SetMessage($"T{which - 1} (random) ← {Path.GetFileNameWithoutExtension(path)}");
				}
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogError((object)("PickRandomCard failed: " + ex));
			}
		}

		private static IEnumerator CaptureSnapshot()
		{
			yield return new WaitForEndOfFrame();
			Camera camera = Camera.main;
			if (camera == null)
			{
				camera = UnityEngine.Object.FindObjectOfType<Camera>();
			}
			if (!(camera == null))
			{
				int height = Screen.height;
				int num = (int)((float)height * (63f / 88f));
				int num2 = (Screen.width - num) / 2;
				RenderTexture renderTexture = new RenderTexture(Screen.width, Screen.height, 24);
				RenderTexture targetTexture = camera.targetTexture;
				camera.targetTexture = renderTexture;
				camera.Render();
				RenderTexture.active = renderTexture;
				Texture2D texture2D = new Texture2D(num, height, TextureFormat.RGB24, mipChain: false);
				texture2D.ReadPixels(new Rect(num2, 0f, num, height), 0, 0);
				texture2D.Apply();
				camera.targetTexture = targetTexture;
				RenderTexture.active = null;
				UnityEngine.Object.Destroy(renderTexture);
				snapshotPng = WardrobeCompat.EncodeToPNG(texture2D);
				if (previewTex != null && previewTex != greyTex)
				{
					UnityEngine.Object.Destroy(previewTex);
				}
				previewTex = texture2D;
				WardrobeCelebration.BeginFlourish();
				SetMessage(WardrobeLang.T("Snapshot taken!"));
			}
		}

		private static bool SaveClothesCard(WardrobeSession s)
		{
			if (snapshotPng == null)
			{
				SetMessage(WardrobeLang.T("Take a Snapshot first!"));
				return false;
			}
			string text = s.SaveOutfitCard(snapshotPng, cardName);
			if (text == null)
			{
				return false;
			}
			WardrobePlugin.Log.LogMessage((object)("Saved coordinate card: " + text));
			SetMessage(WardrobeLang.T("Coordinate card saved!"));
			return true;
		}

		private static GUIContent MixerClothingLabel(int kind, int index)
		{
			GUIContent gUIContent = WardrobeEmoji.SlotLabel(kind);
			if (index != 0)
			{
				return new GUIContent(gUIContent.text + " " + (index + 1), gUIContent.image);
			}
			return gUIContent;
		}

		private static void EnsureMixerStyles()
		{
			if (!(mixerSkin == GUI.skin) || mixerLanguage != WardrobeLang.Current)
			{
				mixerSkin = GUI.skin;
				mixerLanguage = WardrobeLang.Current;
				mixerColumns = null;
				mixerButton = new GUIStyle(GUI.skin.button)
				{
					padding = new RectOffset(2, 2, 0, 0),
					margin = new RectOffset(),
					alignment = TextAnchor.MiddleCenter
				};
				mixerHeading = new GUIStyle(mixerButton)
				{
					padding = new RectOffset()
				};
				mixerHeading.normal.background = null;
				mixerHeading.hover.background = null;
				mixerHeading.active.background = null;
				mixerHeading.normal.textColor = GUI.skin.label.normal.textColor;
				mixerLabelWidth = 0f;
				GUIContent[] array = new GUIContent[4]
				{
					WardrobeEmoji.C("clothes", " " + WardrobeLang.T("Clothes")),
					WardrobeEmoji.C("ring", " " + WardrobeLang.T("Accessories")),
					WardrobeEmoji.C("palette", " " + WardrobeLang.T("Colors")),
					new GUIContent(string.Format(WardrobeLang.T("Acc {0}"), 150))
				};
				foreach (GUIContent content in array)
				{
					mixerLabelWidth = Mathf.Max(mixerLabelWidth, GUI.skin.label.CalcSize(content).x);
				}
				for (int j = 0; j < 9; j++)
				{
					mixerLabelWidth = Mathf.Max(mixerLabelWidth, GUI.skin.label.CalcSize(MixerClothingLabel(j, 4)).x);
				}
				mixerLabelWidth = Mathf.Ceil(mixerLabelWidth) + 2f;
				mixerClearWidth = Mathf.Max(mixerHeading.CalcSize(new GUIContent(WardrobeLang.T("Clear") + "/+")).x, mixerButton.CalcSize(new GUIContent(WardrobeLang.T("[None]"))).x);
				mixerRandomWidth = Mathf.Max(24f, mixerButton.CalcSize(new GUIContent("[?]")).x);
				for (int k = 0; k < 3; k++)
				{
					mixerStateWidths[k] = Mathf.Max(24f, Mathf.Ceil(mixerButton.CalcSize(new GUIContent(WardrobeLang.T(mixerStateNames[k]))).x) + 2f);
				}
			}
		}

		private static MixerRects MixerRowRects()
		{
			EnsureMixerStyles();
			float height = GUI.skin.button.CalcHeight(new GUIContent("[1]"), 40f);
			Rect rect = GUILayoutUtility.GetRect(10f, height, GUI.skin.button, GUILayout.ExpandWidth(expand: true), GUILayout.Height(height));
			float num = ((rect.width > 1f) ? rect.width : ((mixerColumnsWidth > 1f) ? mixerColumnsWidth : (winRect.width - 50f)));
			if (mixerColumns == null || Mathf.Abs(num - mixerColumnsWidth) > 0.01f)
			{
				mixerColumnsWidth = num;
				mixerColumns = WardrobeMixerColumns.Calculate(num, mixerLabelWidth, mixerClearWidth, mixerRandomWidth, mixerStateWidths[0], mixerStateWidths[1], mixerStateWidths[2]);
				mixerSourceHeading = new GUIStyle(mixerHeading);
				while (mixerSourceHeading.fontSize > 10 && Mathf.Max(mixerSourceHeading.CalcSize(new GUIContent("OG")).x, mixerSourceHeading.CalcSize(new GUIContent("T1")).x, mixerSourceHeading.CalcSize(new GUIContent("T2")).x) > mixerColumns.Width[2])
				{
					mixerSourceHeading.fontSize--;
				}
			}
			return new MixerRects
			{
				Row = rect,
				Columns = mixerColumns
			};
		}

		private static void DrawMixerHeadersHS2()
		{
			MixerRects mixerRects = MixerRowRects();
			GUI.Label(mixerRects[0], WardrobeLang.T("Mix"), new GUIStyle(GUI.skin.label)
			{
				fontStyle = FontStyle.Bold
			});
			GUI.Label(mixerRects[1], new GUIContent(WardrobeEmoji.Icon("gear")), mixerHeading);
			GUI.Label(mixerRects[2], "OG", mixerSourceHeading);
			GUI.Label(mixerRects[3], "T1", mixerSourceHeading);
			GUI.Label(mixerRects[4], "T2", mixerSourceHeading);
			GUI.Label(mixerRects[5], new GUIContent(WardrobeEmoji.Icon("dice")), mixerHeading);
			GUI.Label(new Rect(mixerRects[6].x, mixerRects[6].y, mixerRects[7].xMax - mixerRects[6].x, mixerRects[6].height), WardrobeLang.T("Clear") + "/+", mixerHeading);
			for (int i = 0; i < 3; i++)
			{
				GUI.Label(mixerRects[8 + i], WardrobeLang.T(mixerStateNames[i]), mixerHeading);
			}
		}

		private static bool MixerThumb(Rect r, Texture2D texture, bool active, bool enabled, bool pulse = false, string number = null, bool live = false)
		{
			bool enabled2 = GUI.enabled;
			GUI.enabled = enabled2 && enabled;
			if (number != null && mixerButton.CalcSize(new GUIContent(number)).x > r.width)
			{
				number = number.Trim('[', ']');
			}
			bool num = GUI.Button(r, (number == null) ? GUIContent.none : new GUIContent(number), mixerButton);
			if (texture != null)
			{
				GUI.DrawTexture(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f), texture, ScaleMode.ScaleToFit);
			}
			else if (enabled && number == null && !live)
			{
				GUI.Label(r, "—", mixerHeading);
			}
			if (pulse)
			{
				DrawPulseBorder(r);
			}
			else if (active)
			{
				DrawActiveOutline(r);
			}
			else if (GUI.enabled && r.Contains(Event.current.mousePosition))
			{
				DrawHoverOutline(r);
			}
			GUI.enabled = enabled2;
			return num && enabled2 && enabled;
		}

		private static void DrawMixerStates(WardrobeSession s, int kind, int index, MixerRects r)
		{
			ChaFileClothes.PartsInfo clothingSlotPart = s.GetClothingSlotPart(kind, index);
			bool flag = (clothingSlotPart != null && clothingSlotPart.id != 0) || s.IsManagedClothingSlot(kind, index);
			byte clothingSlotState = s.GetClothingSlotState(kind, index);
			bool enabled = GUI.enabled;
			Color color = GUI.color;
			for (int i = 0; i < 3; i++)
			{
				Rect rect = r[8 + i];
				bool flag2 = flag && i >= 0 && i <= 2 && (i != 1 || (kind != 4 && kind != 6 && kind != 7 && kind != 8));
				bool flag3 = flag2 && clothingSlotState == i;
				bool flag4 = flag2 && enabled && rect.Contains(Event.current.mousePosition);
				GUI.enabled = enabled && flag2;
				GUI.color = ((flag3 || flag4) ? color : (color * new Color(0.65f, 0.65f, 0.65f, 1f)));
				bool num = GUI.Toggle(rect, flag3, WardrobeLang.T(mixerStateNames[i]), mixerButton);
				if (flag3)
				{
					DrawActiveOutline(rect);
				}
				else if (flag4)
				{
					DrawHoverOutline(rect);
				}
				if (num && !flag3 && GUI.enabled)
				{
					s.PushUndo();
					s.SetClothingSlotState(kind, index, (byte)i);
				}
			}
			GUI.color = color;
			GUI.enabled = enabled;
		}

		private static void DrawMixerStandardHS2(WardrobeSession s, string key, GUIContent label, Texture2D thumb, Action thumbClick, Func<int, Texture2D> srcThumb, Action<int> apply, int kind, int index = 0)
		{
			MixerRects r = MixerRowRects();
			if (!s.mixSel.TryGetValue(key, out var value))
			{
				value = 0;
			}
			GUI.Label(r[0], label);
			if (thumbClick != null && MixerThumb(r[1], thumb, active: false, enabled: true, pulse: false, null, live: true))
			{
				thumbClick();
			}
			int num = 0;
			for (int i = 1; i <= 3; i++)
			{
				if (MixerThumb(r[1 + i], srcThumb?.Invoke(i), value == i, i switch
				{
					2 => s.t1 != null, 
					1 => true, 
					_ => s.t2 != null, 
				}, pulse: false, (srcThumb == null) ? ("[" + i + "]") : null))
				{
					num = i;
				}
			}
			if (kind >= 0)
			{
				bool enabled = GUI.enabled;
				bool flag = index < s.GetClothingSlotCount(kind);
				GUI.enabled = enabled && flag;
				if (GUI.Button(r[5], "[?]", mixerButton))
				{
					if (index == 0)
					{
						s.PushUndo();
						s.RandomizeClothingItem(kind);
					}
					else
					{
						List<WardrobeItem> list = WardrobeCatalog.List((int)WardrobeSession.ClothesCategories[kind]);
						if (list != null && list.Count > 0)
						{
							s.PushUndo();
							s.SetClothingSlotItem(kind, index, list[UnityEngine.Random.Range(0, list.Count)].id);
						}
					}
				}
				if (GUI.Button(r[6], new GUIContent("×", "Clear this garment"), mixerButton))
				{
					s.PushUndo();
					s.ClearClothingSlot(kind, index);
				}
				GUI.enabled = enabled && s.GetClothingSlotCount(kind) < 5;
				if (GUI.Button(r[7], new GUIContent("+", "Add another " + WardrobeLang.T(WardrobeSession.ClothesSlotNames[kind])), mixerButton))
				{
					WardrobeClothesTab.RequestAdd(s, kind);
				}
				GUI.enabled = enabled;
				DrawMixerStates(s, kind, index, r);
			}
			if (num != 0)
			{
				s.PushUndo();
				apply(num);
			}
		}

		private static void DrawMixerExtraHS2(WardrobeSession s, int kind, int index)
		{
			bool exists = index < s.GetClothingSlotCount(kind);
			DrawMixerStandardHS2(s, "Cloth" + kind + "#" + index, MixerClothingLabel(kind, index), exists ? s.LiveClothesThumb(kind, index) : null, delegate
			{
				NavigateToClothes(kind, exists ? index : 0);
			}, (int source) => s.SrcClothesThumb(source, kind, index), delegate(int source)
			{
				s.ApplyClothingSlot(kind, index, source);
			}, kind, index);
		}

		private static void DrawMixerAccessoryHS2(WardrobeSession s, int slot)
		{
			MixerRects mixerRects = MixerRowRects();
			string key = "Acc" + slot;
			int num = s.mixSel[key];
			GUI.Label(mixerRects[0], string.Format(WardrobeLang.T("Acc {0}"), slot + 1));
			bool flag = moveState == 2 && s.AccSlotEmpty(slot);
			if (MixerThumb(mixerRects[1], s.LiveAccThumb(slot), active: false, enabled: true, flag, null, live: true))
			{
				if (moveState == 0)
				{
					NavigateToAcc(slot);
				}
				else if (flag)
				{
					MoveDeposit(s, slot);
				}
			}
			bool flag2 = moveState == 1;
			int num2 = 0;
			for (int i = 1; i <= 3; i++)
			{
				bool flag3 = i switch
				{
					2 => s.t1 != null, 
					1 => true, 
					_ => s.t2 != null, 
				};
				bool flag4 = flag3 && s.AccPopulated(i, slot);
				bool enabled = (flag2 ? flag4 : (moveState == 0 && flag3));
				if (MixerThumb(mixerRects[1 + i], s.SrcAccThumb(i, slot), num == i, enabled, flag2 && flag4))
				{
					num2 = i;
				}
			}
			bool enabled2 = GUI.enabled;
			GUI.enabled = enabled2 && moveState == 0;
			if (GUI.Button(new Rect(mixerRects[6].x, mixerRects[6].y, mixerRects[7].xMax - mixerRects[6].x, mixerRects[6].height), new GUIContent(WardrobeLang.T("[None]"), "Clear this accessory slot"), mixerButton))
			{
				s.PushUndo();
				s.ClearAccessorySlot(slot);
			}
			GUI.enabled = enabled2;
			accTrailGap = 0f;
			if (num2 != 0)
			{
				if (flag2)
				{
					MovePickSourceAt(num2, slot);
				}
				else if (moveState == 0)
				{
					s.PushUndo();
					s.ApplyAccessorySlot(slot, num2);
				}
			}
		}
	}
	internal static class WardrobeHomeColor4Koi
	{
		private static string Key(int kind, int index)
		{
			return kind + ":" + index;
		}

		internal static Dictionary<string, WardrobeMaterialColor4Koi.Value> Capture(ChaControl cha)
		{
			Dictionary<string, WardrobeMaterialColor4Koi.Value> dictionary = new Dictionary<string, WardrobeMaterialColor4Koi.Value>();
			if (cha == null)
			{
				return dictionary;
			}
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = WardrobeClothingSlotsKoi.Controller(cha);
			for (int i = 0; i < 9; i++)
			{
				if (WardrobeMaterialColor4Koi.TryReadClothing(cha, i, 0, out var value))
				{
					dictionary[Key(i, 0)] = value;
				}
				if ((UnityEngine.Object)(object)clothingSlotsControllerKoi == null)
				{
					continue;
				}
				for (int j = 1; j <= clothingSlotsControllerKoi.Count(i); j++)
				{
					if (WardrobeMaterialColor4Koi.TryReadClothing(cha, i, j, out value))
					{
						dictionary[Key(i, j)] = value;
					}
				}
			}
			return dictionary;
		}

		internal static void ApplySource(ChaControl cha, ChaFileCoordinate source)
		{
			if (cha == null || source == null)
			{
				return;
			}
			Dictionary<int, WardrobeME.Color4Override> dictionary = WardrobeME.ReadClothingColor4(source);
			ClothingSlotSet clothingSlotSet = WardrobeClothingSlotsKoi.Read(source);
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = WardrobeClothingSlotsKoi.Controller(cha);
			for (int i = 0; i < 9; i++)
			{
				int id = source.clothes.parts[i].id;
				WardrobeMaterialColor4Koi.Value value;
				bool flag = WardrobeAuthoredColor4Koi.TrySourceClothing(cha, i, id, out value);
				ClothingSlotRecord primary = clothingSlotSet.GetPrimary(i);
				WardrobeMaterialColor4Koi.Value value2;
				bool flag2 = TryReadAppearance(primary?.Appearance, out value2);
				if (primary != null && primary.SourceCategory != 0 && primary.Part != null && primary.SourceCategory % 10 == 2)
				{
					ChaFileClothes.PartsInfo partsInfo = MessagePackSerializer.Deserialize<ChaFileClothes.PartsInfo>(primary.Part);
					if (partsInfo.colorInfo != null && partsInfo.colorInfo.Length > 3)
					{
						value.Color = partsInfo.colorInfo[3].baseColor;
						flag = true;
						flag2 = false;
					}
				}
				if (flag2)
				{
					value = MergeAppearance(value, flag, value2);
					flag = true;
				}
				WardrobeME.Color4Override value3;
				bool flag3 = dictionary.TryGetValue(i, out value3);
				bool flag4 = (UnityEngine.Object)(object)clothingSlotsControllerKoi != null && clothingSlotsControllerKoi.Visual(i, 0) != null && clothingSlotsControllerKoi.Visual(i, 0).StandaloneSubKind == 2;
				if ((flag || flag3) && !flag4)
				{
					ApplyPrimary(cha, i, flag ? new WardrobeMaterialColor4Koi.Value?(value) : ((WardrobeMaterialColor4Koi.Value?)null), flag3 ? value3 : null);
				}
				if ((UnityEngine.Object)(object)clothingSlotsControllerKoi == null)
				{
					continue;
				}
				int num = clothingSlotsControllerKoi.Count(i);
				for (int j = 1; j <= num; j++)
				{
					if (clothingSlotsControllerKoi.Visual(i, j) != null && clothingSlotsControllerKoi.Visual(i, j).StandaloneSubKind == 2)
					{
						continue;
					}
					ClothingSlotRecord clothingSlotRecord = clothingSlotSet.Get(i, j);
					if (clothingSlotRecord == null)
					{
						continue;
					}
					int itemId = ClothingSlotIdentityKoi.Resolve(clothingSlotRecord);
					WardrobeMaterialColor4Koi.Value value4;
					bool flag5 = WardrobeAuthoredColor4Koi.TrySourceClothing(cha, i, itemId, out value4);
					if (TryReadAppearance(clothingSlotRecord.Appearance, out var value5))
					{
						value4 = MergeAppearance(value4, flag5, value5);
						flag5 = true;
					}
					if (clothingSlotRecord.SourceCategory != 0 && clothingSlotRecord.SourceCategory % 10 == 2 && clothingSlotRecord.Part != null)
					{
						ChaFileClothes.PartsInfo partsInfo2 = MessagePackSerializer.Deserialize<ChaFileClothes.PartsInfo>(clothingSlotRecord.Part);
						if (partsInfo2.colorInfo != null && partsInfo2.colorInfo.Length > 3)
						{
							value4.Color = partsInfo2.colorInfo[3].baseColor;
							flag5 = true;
						}
					}
					if (flag5)
					{
						ApplyExtra(cha, i, j, value4);
					}
				}
			}
			if ((UnityEngine.Object)(object)clothingSlotsControllerKoi != null)
			{
				clothingSlotsControllerKoi.Capture();
			}
		}

		private static WardrobeMaterialColor4Koi.Value MergeAppearance(WardrobeMaterialColor4Koi.Value authored, bool hasAuthored, WardrobeMaterialColor4Koi.Value appearance)
		{
			if (!hasAuthored)
			{
				return appearance;
			}
			authored.Color = appearance.Color;
			if (appearance.HasGloss)
			{
				authored.Gloss = appearance.Gloss;
				authored.HasGloss = true;
			}
			if (appearance.HasMetallic)
			{
				authored.Metallic = appearance.Metallic;
				authored.HasMetallic = true;
			}
			return authored;
		}

		internal static void Restore(ChaControl cha, Dictionary<string, WardrobeMaterialColor4Koi.Value> values)
		{
			if (cha == null || values == null)
			{
				return;
			}
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = WardrobeClothingSlotsKoi.Controller(cha);
			for (int i = 0; i < 9; i++)
			{
				if (values.TryGetValue(Key(i, 0), out var value))
				{
					ApplyPrimary(cha, i, value, null);
				}
				if ((UnityEngine.Object)(object)clothingSlotsControllerKoi == null)
				{
					continue;
				}
				for (int j = 1; j <= clothingSlotsControllerKoi.Count(i); j++)
				{
					if (values.TryGetValue(Key(i, j), out value))
					{
						ApplyExtra(cha, i, j, value);
					}
				}
			}
			if ((UnityEngine.Object)(object)clothingSlotsControllerKoi != null)
			{
				clothingSlotsControllerKoi.Capture();
			}
		}

		internal static bool ResetClothing(ChaControl cha, int kind, int index)
		{
			if (cha == null)
			{
				return false;
			}
			bool num = ((index == 0) ? ApplyPrimary(cha, kind, null, null) : ApplyExtra(cha, kind, index, null));
			if (num && index == 0)
			{
				ClothingSlotsControllerKoi clothingSlotsControllerKoi = WardrobeClothingSlotsKoi.Controller(cha);
				if (clothingSlotsControllerKoi == null)
				{
					return num;
				}
				clothingSlotsControllerKoi.Capture();
			}
			return num;
		}

		internal static bool ResetAccessory(ChaControl cha, int slot)
		{
			if (cha == null || !WardrobeME.CanEditProps(cha))
			{
				return false;
			}
			Dictionary<Material, WardrobeMaterialColor4Koi.Value> dictionary = WardrobeAuthoredColor4Koi.Accessory(cha, slot);
			if (dictionary.Count == 0)
			{
				WardrobePlugin.Log.LogWarning((object)"Accessory Color 4 authored material was unavailable; reset skipped.");
				return false;
			}
			bool flag = WardrobeME.CanEditProps(cha);
			GameObject go = WardrobeMaterialColor4Koi.AccessoryRoot(cha, slot);
			bool result = false;
			foreach (KeyValuePair<Material, WardrobeMaterialColor4Koi.Value> item in dictionary)
			{
				Material key = item.Key;
				WardrobeMaterialColor4Koi.Value value = item.Value;
				if (key == null || !key.HasProperty("_Color4"))
				{
					continue;
				}
				bool flag2 = value.HasGloss && key.HasProperty("_Glossiness4");
				bool flag3 = value.HasMetallic && key.HasProperty("_Metallic4");
				WardrobeME.ClearAccessoryColor4(cha, slot, new Material[1] { key }, color: true, flag2, flag3);
				if (flag)
				{
					WardrobeME.SetColorProp(cha, 2, slot, go, key, "Color4", value.Color);
				}
				else
				{
					key.SetColor("_Color4", value.Color);
				}
				if (flag2)
				{
					if (flag)
					{
						WardrobeME.SetFloatProp(cha, 2, slot, go, key, "Glossiness4", value.Gloss);
					}
					else
					{
						key.SetFloat("_Glossiness4", value.Gloss);
					}
				}
				if (flag3)
				{
					if (flag)
					{
						WardrobeME.SetFloatProp(cha, 2, slot, go, key, "Metallic4", value.Metallic);
					}
					else
					{
						key.SetFloat("_Metallic4", value.Metallic);
					}
				}
				result = true;
			}
			return result;
		}

		private static bool ApplyPrimary(ChaControl cha, int kind, WardrobeMaterialColor4Koi.Value? source, WardrobeME.Color4Override edited)
		{
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = WardrobeClothingSlotsKoi.Controller(cha);
			if ((UnityEngine.Object)(object)clothingSlotsControllerKoi != null && clothingSlotsControllerKoi.IsManagedPrimary(kind))
			{
				if (edited == null)
				{
					return ApplyExtra(cha, kind, 0, source);
				}
				WardrobeMaterialColor4Koi.Value value;
				if (source.HasValue)
				{
					value = source.Value;
				}
				else
				{
					WardrobeMaterialColor4Koi.TryReadClothing(cha, kind, 0, out value);
				}
				if (edited.HasColor)
				{
					value.Color = edited.Color;
				}
				if (edited.HasGloss)
				{
					value.Gloss = edited.Gloss;
					value.HasGloss = true;
				}
				if (edited.HasMetallic)
				{
					value.Metallic = edited.Metallic;
					value.HasMetallic = true;
				}
				return ApplyExtra(cha, kind, 0, value);
			}
			if (!WardrobeME.CanEditProps(cha))
			{
				return false;
			}
			GameObject gameObject = WardrobeMaterialColor4Koi.ClothingRoot(cha, kind, 0);
			if (gameObject == null)
			{
				return false;
			}
			Dictionary<Material, WardrobeMaterialColor4Koi.Value> dictionary = WardrobeAuthoredColor4Koi.Clothing(cha, kind, 0);
			if (dictionary.Count == 0 && !source.HasValue && edited == null)
			{
				WardrobePlugin.Log.LogWarning((object)"Clothing Color 4 authored material was unavailable; reset skipped.");
				return false;
			}
			bool flag = WardrobeME.CanEditProps(cha);
			bool flag2 = !source.HasValue && edited == null;
			bool flag3 = flag2 || source.HasValue || (edited?.HasColor ?? false);
			bool flag4 = flag2 || (source.HasValue && source.Value.HasGloss) || (edited?.HasGloss ?? false);
			bool flag5 = flag2 || (source.HasValue && source.Value.HasMetallic) || (edited?.HasMetallic ?? false);
			List<Material> list = (flag2 ? new List<Material>(dictionary.Keys) : WardrobeMaterialColor4Koi.ClothingMaterials(cha, kind, 0));
			if (!flag2)
			{
				WardrobeME.ClearClothingColor4(cha, kind, list, flag3, flag4, flag5);
			}
			bool result = false;
			foreach (Material item in list)
			{
				if (item == null || !item.HasProperty("_Color4"))
				{
					continue;
				}
				WardrobeMaterialColor4Koi.Value value2;
				bool flag6 = dictionary.TryGetValue(item, out value2);
				if (flag2 && !flag6)
				{
					continue;
				}
				Color color = (flag2 ? value2.Color : item.GetColor("_Color4"));
				float num = ((flag2 && value2.HasGloss) ? value2.Gloss : (item.HasProperty("_Glossiness4") ? item.GetFloat("_Glossiness4") : 0f));
				float num2 = ((flag2 && value2.HasMetallic) ? value2.Metallic : (item.HasProperty("_Metallic4") ? item.GetFloat("_Metallic4") : 0f));
				if (source.HasValue)
				{
					WardrobeMaterialColor4Koi.Value value3 = source.Value;
					color = value3.Color;
					if (value3.HasGloss)
					{
						num = value3.Gloss;
					}
					if (value3.HasMetallic)
					{
						num2 = value3.Metallic;
					}
				}
				if (edited != null)
				{
					if (edited.HasColor)
					{
						color = edited.Color;
					}
					if (edited.HasGloss)
					{
						num = edited.Gloss;
					}
					if (edited.HasMetallic)
					{
						num2 = edited.Metallic;
					}
				}
				bool flag7 = flag4 && item.HasProperty("_Glossiness4") && (!flag2 || value2.HasGloss);
				bool flag8 = flag5 && item.HasProperty("_Metallic4") && (!flag2 || value2.HasMetallic);
				if (flag2)
				{
					WardrobeME.ClearClothingColor4(cha, kind, new Material[1] { item }, color: true, flag7, flag8);
				}
				if (flag3)
				{
					if (flag)
					{
						WardrobeME.SetColorProp(cha, 1, kind, gameObject, item, "Color4", color);
					}
					else
					{
						item.SetColor("_Color4", color);
					}
				}
				if (flag7)
				{
					if (flag)
					{
						WardrobeME.SetFloatProp(cha, 1, kind, gameObject, item, "Glossiness4", num);
					}
					else
					{
						item.SetFloat("_Glossiness4", num);
					}
				}
				if (flag8)
				{
					if (flag)
					{
						WardrobeME.SetFloatProp(cha, 1, kind, gameObject, item, "Metallic4", num2);
					}
					else
					{
						item.SetFloat("_Metallic4", num2);
					}
				}
				result = true;
			}
			return result;
		}

		private static bool ApplyExtra(ChaControl cha, int kind, int index, WardrobeMaterialColor4Koi.Value? source)
		{
			if (WardrobeCompositeTopKoi.UsesNativeFourth(cha, kind, index))
			{
				ClothingSlotsControllerKoi clothingSlotsControllerKoi = WardrobeClothingSlotsKoi.Controller(cha);
				ChaFileClothes.PartsInfo partsInfo = (((UnityEngine.Object)(object)clothingSlotsControllerKoi == null) ? null : clothingSlotsControllerKoi.Part(kind, index));
				if (partsInfo == null || partsInfo.colorInfo == null || partsInfo.colorInfo.Length < 4)
				{
					return false;
				}
				partsInfo.colorInfo[3].baseColor = (source.HasValue ? source.Value.Color : WardrobeCompositeTopKoi.GetDefaultColor(cha, kind, index, 3).baseColor);
				WardrobeClothingSlotsKoi.Repaint(cha, kind, index);
				WardrobeClothingSlotsKoi.CaptureAppearance(cha, kind, index);
				return true;
			}
			Dictionary<Material, WardrobeMaterialColor4Koi.Value> dictionary = WardrobeAuthoredColor4Koi.Clothing(cha, kind, index);
			if (dictionary.Count == 0 && !source.HasValue)
			{
				WardrobePlugin.Log.LogWarning((object)"Extra clothing Color 4 authored material was unavailable; reset skipped.");
				return false;
			}
			bool flag = false;
			foreach (Material item in WardrobeMaterialColor4Koi.ClothingMaterials(cha, kind, index))
			{
				if (item == null || !item.HasProperty("_Color4"))
				{
					continue;
				}
				WardrobeMaterialColor4Koi.Value value;
				bool flag2 = dictionary.TryGetValue(item, out value);
				if (source.HasValue || flag2)
				{
					WardrobeMaterialColor4Koi.Value value2 = (source.HasValue ? source.Value : value);
					item.SetColor("_Color4", value2.Color);
					if (value2.HasGloss && item.HasProperty("_Glossiness4"))
					{
						item.SetFloat("_Glossiness4", value2.Gloss);
					}
					if (value2.HasMetallic && item.HasProperty("_Metallic4"))
					{
						item.SetFloat("_Metallic4", value2.Metallic);
					}
					flag = true;
				}
			}
			if (flag)
			{
				WardrobeClothingSlotsKoi.CaptureAppearance(cha, kind, index);
			}
			return flag;
		}

		internal static bool TryReadAppearance(byte[] bytes, out WardrobeMaterialColor4Koi.Value value)
		{
			value = default(WardrobeMaterialColor4Koi.Value);
			if (bytes == null || bytes.Length == 0)
			{
				return false;
			}
			try
			{
				using MemoryStream memoryStream = new MemoryStream(bytes, writable: false);
				using BinaryReader binaryReader = new BinaryReader(memoryStream);
				if (binaryReader.ReadInt32() != 1263289158)
				{
					return false;
				}
				int num = binaryReader.ReadInt32();
				if (num < 1 || num > 2)
				{
					return false;
				}
				bool flag = false;
				bool flag2 = false;
				int num2 = Count(binaryReader, 512);
				for (int i = 0; i < num2; i++)
				{
					binaryReader.ReadString();
					binaryReader.ReadBoolean();
					int num3 = Count(binaryReader, 128);
					for (int j = 0; j < num3; j++)
					{
						if (!binaryReader.ReadBoolean())
						{
							continue;
						}
						binaryReader.ReadString();
						binaryReader.ReadInt32();
						WardrobeMaterialColor4Koi.Value value2 = default(WardrobeMaterialColor4Koi.Value);
						bool flag3 = false;
						if (Count(binaryReader, 32) != 6)
						{
							return false;
						}
						for (int k = 0; k < 6; k++)
						{
							if (binaryReader.ReadBoolean())
							{
								Color color = new Color(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
								if (k == 3)
								{
									value2.Color = color;
									flag3 = true;
								}
							}
						}
						int num4 = Count(binaryReader, 32);
						if (num4 != ((num == 1) ? 5 : 7))
						{
							return false;
						}
						for (int l = 0; l < num4; l++)
						{
							if (binaryReader.ReadBoolean())
							{
								float num5 = binaryReader.ReadSingle();
								switch (l)
								{
								case 5:
									value2.Gloss = num5;
									value2.HasGloss = true;
									break;
								case 6:
									value2.Metallic = num5;
									value2.HasMetallic = true;
									break;
								}
							}
						}
						if (Count(binaryReader, 32) != 6)
						{
							return false;
						}
						for (int m = 0; m < 6; m++)
						{
							int length = binaryReader.ReadInt32();
							Skip(memoryStream, length, 33554432);
						}
						if (flag3)
						{
							bool flag4 = Math.Abs(value2.Color.r - 1f) > 0.004f || Math.Abs(value2.Color.g - 1f) > 0.004f || Math.Abs(value2.Color.b - 1f) > 0.004f;
							if (!flag || (flag4 && !flag2))
							{
								value = value2;
								flag = true;
								flag2 = flag4;
							}
						}
					}
					int num6 = Count(binaryReader, 4096);
					for (int n = 0; n < num6; n++)
					{
						binaryReader.ReadString();
						binaryReader.ReadSingle();
					}
				}
				if (memoryStream.Position != memoryStream.Length)
				{
					return false;
				}
				return flag;
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("Koi fourth-color snapshot could not be read: " + ex.GetBaseException().Message));
				return false;
			}
		}

		private static int Count(BinaryReader reader, int max)
		{
			int num = reader.ReadInt32();
			if (num < 0 || num > max)
			{
				throw new IOException("Invalid clothing appearance count.");
			}
			return num;
		}

		private static void Skip(Stream stream, int length, int max)
		{
			if (length < 0 || length > max || length > stream.Length - stream.Position)
			{
				throw new IOException("Invalid clothing texture length.");
			}
			stream.Position += length;
		}
	}
	internal static class WardrobeHs1CardCore
	{
		private static readonly byte[] PngSignature = new byte[8] { 137, 80, 78, 71, 13, 10, 26, 10 };

		internal static byte[] Serialize(Func<BinaryWriter, bool> writeCard)
		{
			if (writeCard == null)
			{
				throw new ArgumentNullException("writeCard");
			}
			using MemoryStream memoryStream = new MemoryStream();
			using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
			if (!writeCard(binaryWriter))
			{
				throw new InvalidDataException("HS1 clothes serializer returned false.");
			}
			binaryWriter.Flush();
			byte[] array = memoryStream.ToArray();
			if (!IsValid(array))
			{
				throw new InvalidDataException("HS1 clothes serializer produced an invalid card.");
			}
			return array;
		}

		internal static bool IsValid(byte[] bytes)
		{
			if (bytes == null || bytes.Length < PngSignature.Length + 12 + 9)
			{
				return false;
			}
			for (int i = 0; i < PngSignature.Length; i++)
			{
				if (bytes[i] != PngSignature[i])
				{
					return false;
				}
			}
			int num = PngSignature.Length;
			int num2 = -1;
			while (num <= bytes.Length - 12)
			{
				long num3 = (long)(((ulong)bytes[num] << 24) | ((ulong)bytes[num + 1] << 16) | ((ulong)bytes[num + 2] << 8) | bytes[num + 3]);
				if (num3 > int.MaxValue)
				{
					return false;
				}
				long num4 = (long)num + 12L + num3;
				if (num4 > bytes.Length)
				{
					return false;
				}
				bool num5 = bytes[num + 4] == 73 && bytes[num + 5] == 69 && bytes[num + 6] == 78 && bytes[num + 7] == 68;
				num = (int)num4;
				if (num5)
				{
					if (num3 != 0L)
					{
						return false;
					}
					num2 = num;
					break;
				}
			}
			if (num2 < 0 || bytes.Length <= num2 + 8)
			{
				return false;
			}
			if (ReadInt64LittleEndian(bytes, bytes.Length - 8) == num2)
			{
				return true;
			}
			for (int j = num2 + 8; j < bytes.Length; j++)
			{
				if ((bytes[j] != 60 && (bytes[j] != 239 || j + 2 >= bytes.Length || bytes[j + 1] != 187 || bytes[j + 2] != 191)) || ReadInt64LittleEndian(bytes, j - 8) != num2)
				{
					continue;
				}
				if (bytes.Length - j > 268435456)
				{
					return false;
				}
				try
				{
					using MemoryStream inStream = new MemoryStream(bytes, j, bytes.Length - j, writable: false);
					XmlDocument xmlDocument = new XmlDocument();
					xmlDocument.XmlResolver = null;
					xmlDocument.Load(inStream);
					return xmlDocument.DocumentElement != null && xmlDocument.DocumentElement.Name == "clothesExtData";
				}
				catch (XmlException)
				{
					return false;
				}
			}
			return false;
		}

		internal static bool WriteValidated(string path, byte[] bytes)
		{
			if (string.IsNullOrEmpty(path) || !IsValid(bytes))
			{
				return false;
			}
			string directoryName = Path.GetDirectoryName(path);
			if (string.IsNullOrEmpty(directoryName))
			{
				return false;
			}
			string text = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
			try
			{
				Directory.CreateDirectory(directoryName);
				File.WriteAllBytes(text, bytes);
				if (new FileInfo(text).Length != bytes.Length)
				{
					return false;
				}
				if (File.Exists(path))
				{
					File.Replace(text, path, null);
				}
				else
				{
					File.Move(text, path);
				}
				return true;
			}
			catch
			{
				return false;
			}
			finally
			{
				try
				{
					if (File.Exists(text))
					{
						File.Delete(text);
					}
				}
				catch
				{
				}
			}
		}

		internal static string SanitizePrefix(string name)
		{
			string text = (string.IsNullOrEmpty(name) ? "DressUpClo" : name.Trim());
			char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
			for (int i = 0; i < invalidFileNameChars.Length; i++)
			{
				text = text.Replace(invalidFileNameChars[i], '_');
			}
			text = text.Trim(' ', '.');
			if (!string.IsNullOrEmpty(text))
			{
				return text;
			}
			return "DressUpClo";
		}

		private static long ReadInt64LittleEndian(byte[] bytes, int offset)
		{
			ulong num = 0uL;
			for (int i = 0; i < 8; i++)
			{
				num |= (ulong)bytes[offset + i] << 8 * i;
			}
			return (long)num;
		}
	}
	internal static class WardrobeHs1CatalogCore
	{
		internal static int ClothesTypeForSlot(int slot)
		{
			if (slot < 0 || slot >= 11)
			{
				return -1;
			}
			return slot + 5;
		}

		internal static int[] SelectableIds(IEnumerable<int> ids)
		{
			List<int> list = new List<int>();
			HashSet<int> hashSet = new HashSet<int>();
			if (ids != null)
			{
				foreach (int id in ids)
				{
					if (id > 0 && hashSet.Add(id))
					{
						list.Add(id);
					}
				}
			}
			return list.ToArray();
		}
	}
	internal static class WardrobeHs1ClothesUiCore
	{
		internal static int StateKindForSlot(int slot)
		{
			switch (slot)
			{
			default:
				return -1;
			case 5:
			case 6:
			case 7:
			case 8:
			case 9:
			case 10:
				return slot + 1;
			case 0:
			case 1:
			case 2:
			case 3:
			case 4:
				return slot;
			}
		}
	}
	internal sealed class WardrobeHs1StatusSnapshot
	{
		private readonly byte[] clothesState;

		private readonly bool[] showAccessory;

		internal WardrobeHs1StatusSnapshot(byte[] clothesState, bool[] showAccessory)
		{
			this.clothesState = ((clothesState != null) ? ((byte[])clothesState.Clone()) : null);
			this.showAccessory = ((showAccessory != null) ? ((bool[])showAccessory.Clone()) : null);
		}

		internal void Restore(byte[] clothesTarget, bool[] accessoryTarget)
		{
			if (clothesState != null && clothesTarget != null)
			{
				Array.Copy(clothesState, clothesTarget, Math.Min(clothesState.Length, clothesTarget.Length));
			}
			if (showAccessory != null && accessoryTarget != null)
			{
				Array.Copy(showAccessory, accessoryTarget, Math.Min(showAccessory.Length, accessoryTarget.Length));
			}
		}
	}
	internal static class WardrobeHs1ToolbarCore
	{
		private const int ColumnWidth = 40;

		private const int FirstRowOffset = 80;

		private const int RowHeight = 40;

		internal static int FirstFreeCell(IList<bool> occupied)
		{
			if (occupied == null)
			{
				return 0;
			}
			for (int i = 0; i < occupied.Count; i++)
			{
				if (!occupied[i])
				{
					return i;
				}
			}
			return occupied.Count;
		}

		internal static int ColumnOffsetForCell(int cell)
		{
			if (cell < 0)
			{
				cell = 0;
			}
			return cell % 2 * 40;
		}

		internal static int RowOffsetForCell(int cell)
		{
			if (cell < 0)
			{
				cell = 0;
			}
			return 80 + cell / 2 * 40;
		}
	}
	internal class WardrobeItem
	{
		public int id;

		public string name;

		public string assetBundle;

		public string assetName;

		public string parent;
	}
	internal static class WardrobeCatalog
	{
		internal static List<WardrobeItem> List(int categoryNo)
		{
			List<WardrobeItem> list = new List<WardrobeItem>();
			bool flag = false;
			Dictionary<int, ListInfoBase> categoryInfo = WardrobeCompat.ListCtrl.GetCategoryInfo((ChaListDefine.CategoryNo)categoryNo);
			if (categoryInfo != null)
			{
				foreach (ListInfoBase value in categoryInfo.Values)
				{
					if (value != null)
					{
						if (value.Id == 0)
						{
							flag = true;
						}
						list.Add(new WardrobeItem
						{
							id = value.Id,
							name = value.Name,
							assetBundle = value.GetInfo(ChaListDefine.KeyType.ThumbAB),
							assetName = value.GetInfo(ChaListDefine.KeyType.ThumbTex)
						});
					}
				}
			}
			list.Sort((WardrobeItem a, WardrobeItem b) => a.id.CompareTo(b.id));
			if (!flag)
			{
				list.Insert(0, new WardrobeItem
				{
					id = 0,
					name = WardrobeLang.T("None")
				});
			}
			return list;
		}
	}
	internal static class WardrobeKoiAccessoryOwnership
	{
		internal static List<int> FindOrphanIds(IList<int> trackedIds, IList<int> candidateIds)
		{
			HashSet<int> hashSet = new HashSet<int>();
			if (trackedIds != null)
			{
				for (int i = 0; i < trackedIds.Count; i++)
				{
					if (trackedIds[i] != 0)
					{
						hashSet.Add(trackedIds[i]);
					}
				}
			}
			List<int> list = new List<int>();
			HashSet<int> hashSet2 = new HashSet<int>();
			if (candidateIds == null)
			{
				return list;
			}
			for (int j = 0; j < candidateIds.Count; j++)
			{
				int num = candidateIds[j];
				if (num != 0 && !hashSet.Contains(num) && hashSet2.Add(num))
				{
					list.Add(num);
				}
			}
			return list;
		}
	}
	internal static class WardrobeKoiCoordinateIntegrity
	{
		internal static bool MatchesMaterialRecord(int recordCoordinate, int recordObjectType, int recordSlot, int currentCoordinate, int targetObjectType, int targetSlot)
		{
			if (recordCoordinate == currentCoordinate && recordObjectType == targetObjectType)
			{
				if (targetSlot >= 0)
				{
					return recordSlot == targetSlot;
				}
				return true;
			}
			return false;
		}

		internal static int RequiredCapacity(int currentCount, int sourceCount, int maximum)
		{
			if (maximum < 0)
			{
				maximum = 0;
			}
			int val = Math.Max(0, currentCount);
			int val2 = Math.Max(0, sourceCount);
			return Math.Min(maximum, Math.Max(val, val2));
		}

		internal static bool AccessoryMatches(int targetType, int targetId, bool hasLoadedObject, int loadedCategory, int loadedId)
		{
			if (targetType == 120 || targetId <= 0)
			{
				return !hasLoadedObject;
			}
			if (hasLoadedObject && loadedCategory == targetType)
			{
				return loadedId == targetId;
			}
			return false;
		}

		internal static string JoinSlotNumbers(IEnumerable<int> slots)
		{
			if (slots == null)
			{
				return "";
			}
			StringBuilder stringBuilder = new StringBuilder();
			foreach (int slot in slots)
			{
				if (stringBuilder.Length > 0)
				{
					stringBuilder.Append(',');
				}
				stringBuilder.Append(slot);
			}
			return stringBuilder.ToString();
		}
	}
	internal static class WardrobeKoiTopCatalog
	{
		internal sealed class Choice
		{
			internal int Category;

			internal int Id;
		}

		private static readonly Dictionary<string, int> tokens = new Dictionary<string, int>();

		private static readonly Dictionary<int, Choice> choices = new Dictionary<int, Choice>();

		private static int next = -1;

		internal static int Token(int category, int id)
		{
			if (category == 0)
			{
				return id;
			}
			string key = category + ":" + id;
			if (!tokens.TryGetValue(key, out var value))
			{
				value = next--;
				tokens[key] = value;
				choices[value] = new Choice
				{
					Category = category,
					Id = id
				};
			}
			return value;
		}

		internal static Choice Decode(int value)
		{
			if (value >= 0)
			{
				return new Choice
				{
					Id = value
				};
			}
			if (choices.TryGetValue(value, out var value2))
			{
				return value2;
			}
			throw new InvalidOperationException("The selected top is no longer available.");
		}

		internal static List<WardrobeItem> List(int kind)
		{
			List<WardrobeItem> list = WardrobeCatalog.List((int)WardrobeSession.ClothesCategories[kind]);
			if (kind != 0)
			{
				return list;
			}
			list = list.Where((WardrobeItem x) => WardrobeCompositeTopKoi.TopType(x.id) == 0).ToList();
			for (int num = 1; num <= 2; num++)
			{
				for (int num2 = 0; num2 < 3; num2++)
				{
					int num3 = WardrobeCompositeTopKoi.Category(num, num2);
					Dictionary<int, ListInfoBase> categoryInfo = WardrobeCompat.ListCtrl.GetCategoryInfo((ChaListDefine.CategoryNo)num3);
					if (categoryInfo == null)
					{
						continue;
					}
					foreach (KeyValuePair<int, ListInfoBase> item in categoryInfo.OrderBy((KeyValuePair<int, ListInfoBase> x) => x.Key))
					{
						ListInfoBase value = item.Value;
						if (value != null)
						{
							string info = value.GetInfo(ChaListDefine.KeyType.MainData);
							if (!string.IsNullOrEmpty(info) && !(info == "0") && !(info == "p_dummy"))
							{
								list.Add(new WardrobeItem
								{
									id = Token(num3, value.Id),
									name = ((num == 1) ? "Sailor: " : "Jacket: ") + value.Name,
									assetBundle = value.GetInfo(ChaListDefine.KeyType.ThumbAB),
									assetName = value.GetInfo(ChaListDefine.KeyType.ThumbTex)
								});
							}
						}
					}
				}
			}
			return list;
		}

		internal static List<WardrobeItem> HeaderList(int kind, int token)
		{
			List<WardrobeItem> list = List(kind);
			if (kind == 0 && token >= 0 && WardrobeCompositeTopKoi.TopType(token) != 0)
			{
				WardrobeItem wardrobeItem = WardrobeCatalog.List((int)WardrobeSession.ClothesCategories[0]).FirstOrDefault((WardrobeItem x) => x.id == token);
				if (wardrobeItem != null)
				{
					list.Add(wardrobeItem);
				}
			}
			return list;
		}

		internal static Texture2D Thumb(int category, int id)
		{
			return WardrobeWidgets.ItemThumb(category, id);
		}
	}
	internal static class WardrobeLang
	{
		internal const int EN = 0;

		internal const int ZH = 1;

		internal const int JA = 2;

		internal static readonly string[] NativeNames = new string[3] { "English", "中文", "日本語" };

		private static Dictionary<string, string[]> map;

		internal static int Current { get; private set; }

		internal static void Init(string configuredNativeName)
		{
			Current = Array.IndexOf(NativeNames, configuredNativeName);
			if (Current < 0)
			{
				Current = 0;
			}
		}

		internal static void Set(int lang)
		{
			if (lang < 0 || lang > 2)
			{
				lang = 0;
			}
			Current = lang;
			try
			{
				WardrobePlugin.languageCfg.Value = NativeNames[lang];
			}
			catch
			{
			}
		}

		internal static string T(string en)
		{
			if (Current == 0 || string.IsNullOrEmpty(en))
			{
				return en;
			}
			if (map == null)
			{
				BuildMap();
			}
			if (!map.TryGetValue(en, out var value))
			{
				return en;
			}
			return value[Current - 1];
		}

		private static void Add(string en, string zh, string ja)
		{
			map[en] = new string[2] { zh, ja };
		}

		private static void BuildMap()
		{
			map = new Dictionary<string, string[]>();
			Add("Language", "语言", "言語");
			Add("Mode", "模式", "モード");
			Add("Home", "主页", "ホーム");
			Add("Clothes", "服装", "服");
			Add("Accessories", "饰品", "アクセサリー");
			Add("Colors", "颜色", "カラー");
			Add("Mix", "混搭", "ミックス");
			Add("Clear", "清除", "クリア");
			Add("Select a character in the Studio workspace.", "请在工作室中选择一个角色。", "スタジオでキャラクターを選択してください。");
			Add("Set Target 1", "选择目标1", "ターゲット1選択");
			Add("Set Target 2", "选择目标2", "ターゲット2選択");
			Add("Apply T1", "应用T1", "T1を適用");
			Add("Apply T2", "应用T2", "T2を適用");
			Add("Random", "随机", "ランダム");
			Add("Snapshot", "快照", "スナップ");
			Add("Save Clothes", "保存服装", "服を保存");
			Add("Saving in {0}s...", "{0}秒后保存...", "{0}秒後に保存...");
			Add("Outfit Name:", "服装名称:", "コーデ名:");
			Add("Top", "上衣", "トップス");
			Add("Bottom", "下装", "ボトムス");
			Add("Bra", "胸罩", "ブラ");
			Add("Underwear", "内裤", "ショーツ");
			Add("Gloves", "手套", "手袋");
			Add("Pantyhose", "连裤袜", "パンスト");
			Add("Socks", "袜子", "靴下");
			Add("Shoes", "鞋子", "靴");
			Add("Acc {0}", "饰品 {0}", "アクセ {0}");
			Add("Accessories (populated slots)", "饰品（已使用槽位）", "アクセサリー（使用中スロット）");
			Add("[None]", "[无]", "[なし]");
			Add("Add slot", "添加槽位", "スロットを追加");
			Add("Add another {0}", "再添加一件{0}", "{0}をもう1つ追加");
			Add("Remove {0} {1}", "移除{0} {1}", "{0} {1}を削除");
			Add("Shader Color 4", "着色器颜色 4", "シェーダーカラー4");
			Add("HSExtSave is required to save fourth-color edits.", "保存第四通道颜色修改需要 HSExtSave。", "カラー4の編集を保存するにはHSExtSaveが必要です。");
			Add("Accessory slot added.", "饰品槽位已添加。", "アクセサリスロットを追加しました。");
			Add("Couldn't add a new accessory slot.", "无法添加新的饰品槽位。", "新しいアクセサリスロットを追加できませんでした。");
			Add("Move Accessory", "移动饰品", "アクセを移動");
			Add("Cancel Move", "取消移动", "移動をキャンセル");
			Add("New accessory slot", "新建饰品槽位", "新規アクセスロット");
			Add("Pick a glowing accessory to copy.", "点击发光的饰品进行复制。", "光っているアクセサリーを選んでコピー。");
			Add("Now pick an empty slot (or +) to drop it into.", "现在选择一个空槽位（或 +）放入。", "空きスロット（または +）を選んでドロップ。");
			Add("Accessory copied to the new slot.", "饰品已复制到新槽位。", "アクセサリーを新しいスロットにコピーしました。");
			Add("Couldn't copy — target slot isn't empty.", "无法复制 — 目标槽位非空。", "コピーできません — 対象スロットが空ではありません。");
			Add("Copy Outfit", "复制服装", "コーデをコピー");
			Add("Paste Outfit", "粘贴服装", "コーデを貼り付け");
			Add("Reset", "重置", "リセット");
			Add("Undo", "撤销", "元に戻す");
			Add("Redo", "重做", "やり直す");
			Add("Set OG Baseline", "设定OG基准", "OG基準を設定");
			Add("Set OG Baseline? ({0}s)", "确认设定? ({0}s)", "設定します? ({0}s)");
			Add("Saving Baseline {0}s...", "{0}秒后设定基准...", "{0}秒後に基準を設定...");
			Add("OG baseline set.", "已设定OG基准。", "OG基準を設定しました。");
			Add("Baseline save cancelled.", "已取消基准设定。", "基準設定をキャンセルしました。");
			Add("Back To Home", "返回主页", "ホームに戻る");
			Add("Overlay", "叠加层", "オーバーレイ");
			Add("None", "无", "なし");
			Add("Load", "加载", "読込");
			Add("Export", "导出", "エクスポート");
			Add("Dump original", "导出原图", "元テクスチャ保存");
			Add("Hide base texture", "隐藏底层纹理", "ベーステクスチャを隠す");
			Add("Overlay loaded.", "叠加层已加载。", "オーバーレイを読み込みました。");
			Add("Could not load image.", "无法加载图片。", "画像を読み込めませんでした。");
			Add("Saved to UserData\\overlays.", "已保存到 UserData\\overlays。", "UserData\\overlays に保存しました。");
			Add("Nothing to export.", "无内容可导出。", "エクスポートするものがありません。");
			Add("Nothing to dump.", "无内容可导出。", "保存するものがありません。");
			Add("Select overlay image", "选择叠加层图片", "オーバーレイ画像を選択");
			Add("Cancel", "取消", "キャンセル");
			Add("Snapshot taken!", "快照已拍摄！", "スナップ撮影完了！");
			Add("Coordinate card saved!", "服装卡片已保存！", "コーデカードを保存しました！");
			Add("Save cancelled.", "已取消保存。", "保存をキャンセルしました。");
			Add("Take a Snapshot first!", "请先拍摄快照！", "先にスナップを撮ってください！");
			Add("T1 outfit applied.", "已应用T1服装。", "T1コーデを適用しました。");
			Add("T2 outfit applied.", "已应用T2服装。", "T2コーデを適用しました。");
			Add("Could not load coordinate card.", "无法加载服装卡片。", "コーデカードを読み込めませんでした。");
			Add("Coordinate folder not found.", "未找到服装卡片文件夹。", "コーデフォルダが見つかりません。");
			Add("No coordinate cards found.", "未找到任何服装卡片。", "コーデカードが見つかりません。");
			Add("Item", "单品", "アイテム");
			Add("State", "状态", "状態");
			Add("ON", "开", "オン");
			Add("Half", "半", "ハーフ");
			Add("OFF", "关", "オフ");
			Add("Remove", "移除", "外す");
			Add("Color", "颜色", "カラー");
			Add("Restore", "还原", "元に戻す");
			Add("Copy", "复制", "コピー");
			Add("Paste", "粘贴", "貼り付け");
			Add("Reset", "重置", "リセット");
			Add("Copy Colors", "复制颜色", "カラーをコピー");
			Add("Paste Colors", "粘贴颜色", "カラーを貼り付け");
			Add("Restore Colors", "恢复颜色", "カラーを元に戻す");
			Add("Mat", "材质", "マテリアル");
			Add("Material Editor", "材质编辑器", "マテリアルエディター");
			Add("Randomize", "随机", "ランダム");
			Add("Options", "选项", "オプション");
			Add("Option Parts 1", "可选部件 1", "オプションパーツ 1");
			Add("Option Parts 2", "可选部件 2", "オプションパーツ 2");
			Add("Break", "破损", "破れ");
			Add("Color 1", "颜色 1", "カラー 1");
			Add("Color 2", "颜色 2", "カラー 2");
			Add("Color 3", "颜色 3", "カラー 3");
			Add("Color 4", "颜色 4", "カラー 4");
			Add("Color 4 (alpha)", "颜色 4（透明度）", "カラー 4（アルファ）");
			Add("Pattern", "图案", "柄");
			Add("Rotate", "旋转", "回転");
			Add("Search:", "搜索：", "検索：");
			Add("Slot", "槽位", "スロット");
			Add("Category", "类别", "カテゴリー");
			Add("Attach To", "附加到", "取り付け先");
			Add("Visible", "显示", "表示");
			Add("No Shake", "禁止晃动", "揺れ無し");
			Add("Physics", "物理", "物理");
			Add("Hair Colors", "发色", "髪色");
			Add("Base", "底色", "ベース");
			Add("Under", "底层", "アンダー");
			Add("Specular", "高光", "スペキュラ");
			Add("Match hair:", "匹配发色：", "髪色に合わせる：");
			Add("Back", "后", "後ろ");
			Add("Front", "前", "前");
			Add("Side", "侧", "サイド");
			Add("Ext", "延长", "エクステ");
			Add("Position", "位置", "位置");
			Add("Rotation", "旋转", "回転");
			Add("Scale", "缩放", "スケール");
			Add("Adjust 1", "调整 1", "調整 1");
			Add("Adjust 2", "调整 2", "調整 2");
			Add("(empty slot — pick an item to edit its colors)", "（空槽位 — 请选择一件单品来编辑颜色）", "（空きスロット — アイテムを選んで色を編集）");
			Add("(empty slot — pick a category to add an accessory)", "（空槽位 — 请选择类别以添加饰品）", "（空きスロット — カテゴリーを選んでアクセサリーを追加）");
			Add("Male characters are not supported (female coordinate slots only).", "不支持男性角色（仅限女性服装槽位）。", "男性キャラクターは非対応です（女性コーデスロットのみ）。");
			Add("⚠ This item's shader ignores vanilla color edits — use Color 4 / Material Editor.", "⚠ 此物品的着色器忽略原版颜色编辑 — 请使用颜色 4 / 材质编辑器。", "⚠ このアイテムのシェーダーは通常のカラー編集を無視します — カラー 4 / マテリアルエディターを使用してください。");
			Add("⚠ This item's materials use a custom shader — the game's color system can't tint them. Colors below still save to the card; use Material Editor to recolor the live item.", "⚠ 此物品的材质使用自定义着色器 — 游戏的颜色系统无法为其上色。下方颜色仍会保存到卡片；请使用材质编辑器为实时物品重新上色。", "⚠ このアイテムのマテリアルはカスタムシェーダーを使用しており、ゲームのカラーシステムでは着色できません。下記のカラーはカードに保存されます。実際のアイテムの色を変えるにはマテリアルエディターを使用してください。");
			Add("Head", "头", "頭");
			Add("Ear", "耳", "耳");
			Add("Glasses", "眼镜", "メガネ");
			Add("Face", "脸", "顔");
			Add("Neck", "颈", "首");
			Add("Shoulder", "肩", "肩");
			Add("Chest", "胸", "胸");
			Add("Waist", "腰", "腰");
			Add("Arm", "手臂", "腕");
			Add("Hand", "手", "手");
			Add("Leg", "腿", "脚");
			Add("Crotch", "裆部", "股間");
			Add("No mod items in this category.", "该类别没有模组物品。", "このカテゴリーにMODアイテムはありません。");
			Add("Source zipmod:", "来源zipmod：", "提供元zipmod：");
			Add("Folder:", "文件夹：", "フォルダ：");
			Add("Also in this zipmod (removed together):", "同一zipmod中的其他物品（将一并移除）：", "同じzipmodに含まれる他のアイテム（一緒に削除されます）：");
			Add("+ {0} other items (hair, etc.)", "+ {0} 个其他物品（发型等）", "+ {0} 個のその他アイテム（髪型など）");
			Add("+ {0} Studio items", "+ {0} 个工作室物品", "+ {0} 個のスタジオアイテム");
			Add("TERMINATE ZIPMOD", "终结ZIPMOD", "ZIPMODを抹消");
			Add("Terminating in {0}s — press again to cancel", "{0}秒后终结 — 再按一次取消", "{0}秒後に抹消 — もう一度押すとキャンセル");
			Add("Termination cancelled.", "已取消终结。", "抹消をキャンセルしました。");
			Add("Core game asset — protected.", "核心游戏资源 — 受保护。", "ゲーム本体のアセット — 保護されています。");
			Add("Not a loadable zipmod — protected.", "非可加载的zipmod — 受保护。", "読み込み可能なzipmodではありません — 保護されています。");
			Add("Source file not found in mods folder.", "在mods文件夹中找不到源文件。", "modsフォルダにソースファイルが見つかりません。");
			Add("Mod trace unavailable — protected.", "模组追踪不可用 — 受保护。", "MODトレース利用不可 — 保護されています。");
			Add("Already terminated — awaiting quarantine on next start.", "已终结 — 等待下次启动时隔离。", "抹消済み — 次回起動時に隔離されます。");
			Add("Terminated \"{0}\".", "已终结“{0}”。", "「{0}」を抹消しました。");
			Add("Its items are banned from Dress-Up's grids, and the file will be moved to the Cleanup_Quarantine folder on the next game start — nothing is ever deleted; empty the bin yourself whenever you like.", "其物品已从 Dress-Up 的选择列表中屏蔽，文件将在下次游戏启动时移动到 Cleanup_Quarantine 文件夹 — 绝不会删除任何文件；请自行清空隔离文件夹。", "そのアイテムは Dress-Up のリストから除外され、ファイルは次回のゲーム起動時に Cleanup_Quarantine フォルダへ移動されます — 削除は一切行われません。隔離フォルダはご自身で空にしてください。");
			Add("Undo Termination", "撤销终结", "抹消を取り消す");
			Add("Termination undone.", "已撤销终结。", "抹消を取り消しました。");
			Add("Continue Cleaning", "继续清理", "クリーンアップを続ける");
			Add("{0} file(s) awaiting quarantine on next start.", "{0} 个文件等待下次启动时隔离。", "{0} 個のファイルが次回起動時の隔離待ちです。");
		}
	}
	internal static class WardrobeMaterialColor4Koi
	{
		internal struct Value
		{
			internal Color Color;

			internal float Gloss;

			internal float Metallic;

			internal bool HasGloss;

			internal bool HasMetallic;
		}

		private static List<Material> Materials(Renderer[] renderers)
		{
			List<Material> list = new List<Material>();
			if (renderers == null)
			{
				return list;
			}
			HashSet<Material> hashSet = new HashSet<Material>();
			foreach (Renderer renderer in renderers)
			{
				if (renderer == null)
				{
					continue;
				}
				Material[] sharedMaterials = renderer.sharedMaterials;
				foreach (Material material in sharedMaterials)
				{
					if (material != null && material.HasProperty("_Color4") && hashSet.Add(material))
					{
						list.Add(material);
					}
				}
			}
			return list;
		}

		private static List<Material> Materials(GameObject root)
		{
			return Materials((root == null) ? null : root.GetComponentsInChildren<Renderer>(includeInactive: true));
		}

		internal static int Identity(UnityEngine.Object value)
		{
			if (!(value == null))
			{
				return value.GetInstanceID();
			}
			return 0;
		}

		private static string LayoutSignature(Renderer[] renderers, int itemId)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(itemId).Append(':');
			if (renderers == null)
			{
				return stringBuilder.ToString();
			}
			foreach (Renderer renderer in renderers)
			{
				stringBuilder.Append(Identity(renderer)).Append('[');
				if (renderer != null)
				{
					Material[] sharedMaterials = renderer.sharedMaterials;
					foreach (Material material in sharedMaterials)
					{
						stringBuilder.Append(Identity(material)).Append('/');
						if (material != null)
						{
							stringBuilder.Append(Identity(material.shader)).Append('/').Append(material.HasProperty("_Color4") ? '1' : '0')
								.Append(material.HasProperty("_Glossiness4") ? '1' : '0')
								.Append(material.HasProperty("_Metallic4") ? '1' : '0');
						}
						stringBuilder.Append(',');
					}
				}
				stringBuilder.Append(']');
			}
			return stringBuilder.ToString();
		}

		internal static GameObject ClothingRoot(ChaControl cha, int kind, int index)
		{
			if (cha == null)
			{
				return null;
			}
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = WardrobeClothingSlotsKoi.Controller(cha);
			ClothingSlotVisualKoi clothingSlotVisualKoi = (((UnityEngine.Object)(object)clothingSlotsControllerKoi == null) ? null : clothingSlotsControllerKoi.Visual(kind, index));
			if (clothingSlotVisualKoi != null)
			{
				return clothingSlotVisualKoi.Root;
			}
			if (index == 0)
			{
				if (cha.objClothes != null && kind >= 0 && kind < cha.objClothes.Length)
				{
					return cha.objClothes[kind];
				}
				return null;
			}
			ClothingSlotsControllerKoi clothingSlotsControllerKoi2 = WardrobeClothingSlotsKoi.Controller(cha);
			return (((UnityEngine.Object)(object)clothingSlotsControllerKoi2 == null) ? null : clothingSlotsControllerKoi2.Visual(kind, index))?.Root;
		}

		internal static string ClothingLayoutSignature(ChaControl cha, int kind, int index, int itemId)
		{
			return kind + ":" + index + ":" + LayoutSignature((cha == null) ? null : ClothingMaterialRenderers(cha, kind, index), itemId);
		}

		internal static string AccessoryLayoutSignature(ChaControl cha, int slot, int itemId)
		{
			return LayoutSignature((cha == null) ? null : AccessoryRenderers(cha, slot), itemId);
		}

		internal static Renderer[] ClothingMaterialRenderers(ChaControl cha, int kind, int index)
		{
			return WardrobeBorrowedBodyMaterialsKoi.Filter(ClothingRoot(cha, kind, index), (cha == null) ? null : WardrobeClothingSlotsKoi.GetRenderers(cha, kind, index));
		}

		internal static List<Material> ClothingMaterials(ChaControl cha, int kind, int index)
		{
			return Materials((cha == null) ? null : ClothingMaterialRenderers(cha, kind, index));
		}

		internal static GameObject AccessoryRoot(ChaControl cha, int slot)
		{
			if (cha == null || slot < 0)
			{
				return null;
			}
			if (cha.objAccessory != null && slot < cha.objAccessory.Length && cha.objAccessory[slot] != null)
			{
				return cha.objAccessory[slot];
			}
			ChaAccessoryComponent accessoryComponent = cha.GetAccessoryComponent(slot);
			if (!(accessoryComponent == null))
			{
				return accessoryComponent.gameObject;
			}
			return null;
		}

		private static Renderer[] AccessoryRenderers(ChaControl cha, int slot)
		{
			if (cha == null)
			{
				return new Renderer[0];
			}
			ChaAccessoryComponent accessoryComponent = cha.GetAccessoryComponent(slot);
			List<Renderer> list = new List<Renderer>();
			if (accessoryComponent != null)
			{
				if (accessoryComponent.rendNormal != null)
				{
					list.AddRange(accessoryComponent.rendNormal);
				}
				if (accessoryComponent.rendAlpha != null)
				{
					list.AddRange(accessoryComponent.rendAlpha);
				}
			}
			GameObject gameObject = AccessoryRoot(cha, slot);
			if (gameObject != null)
			{
				list.AddRange(gameObject.GetComponentsInChildren<Renderer>(includeInactive: true));
			}
			return list.ToArray();
		}

		internal static List<Material> AccessoryMaterials(ChaControl cha, int slot)
		{
			return Materials(AccessoryRenderers(cha, slot));
		}

		private static bool TryRead(List<Material> materials, out Value value)
		{
			value = default(Value);
			materials.RemoveAll((Material material2) => material2 == null);
			if (materials.Count == 0)
			{
				return false;
			}
			Material material = materials[0];
			foreach (Material material2 in materials)
			{
				if (!(material2 == null))
				{
					Color color = material2.GetColor("_Color4");
					if (Mathf.Abs(color.r - 1f) > 0.004f || Mathf.Abs(color.g - 1f) > 0.004f || Mathf.Abs(color.b - 1f) > 0.004f)
					{
						material = material2;
						break;
					}
				}
			}
			value.Color = material.GetColor("_Color4");
			foreach (Material material3 in materials)
			{
				if (!(material3 == null))
				{
					if (!value.HasGloss && material3.HasProperty("_Glossiness4"))
					{
						value.Gloss = material3.GetFloat("_Glossiness4");
						value.HasGloss = true;
					}
					if (!value.HasMetallic && material3.HasProperty("_Metallic4"))
					{
						value.Metallic = material3.GetFloat("_Metallic4");
						value.HasMetallic = true;
					}
				}
			}
			return true;
		}

		private static bool TryRead(GameObject root, out Value value)
		{
			return TryRead(Materials(root), out value);
		}

		internal static bool TryReadClothing(ChaControl cha, int slot, out Value value)
		{
			return TryReadClothing(cha, slot, 0, out value);
		}

		internal static bool TryReadAccessory(ChaControl cha, int slot, out Value value)
		{
			return TryRead((cha == null) ? new List<Material>() : AccessoryMaterials(cha, slot), out value);
		}

		internal static bool TryReadClothing(ChaControl cha, int kind, int index, out Value value)
		{
			value = default(Value);
			if (cha == null)
			{
				return false;
			}
			if (WardrobeCompositeTopKoi.UsesNativeFourth(cha, kind, index))
			{
				ClothingSlotsControllerKoi clothingSlotsControllerKoi = WardrobeClothingSlotsKoi.Controller(cha);
				ChaFileClothes.PartsInfo partsInfo = (((((UnityEngine.Object)(object)clothingSlotsControllerKoi == null) ? null : clothingSlotsControllerKoi.Visual(kind, index)) != null) ? clothingSlotsControllerKoi.Part(kind, index) : ((index == 0) ? cha.nowCoordinate.clothes.parts[kind] : null));
				if (partsInfo != null && partsInfo.colorInfo != null && partsInfo.colorInfo.Length > 3)
				{
					value.Color = partsInfo.colorInfo[3].baseColor;
					return true;
				}
			}
			return TryRead(Materials(ClothingMaterialRenderers(cha, kind, index)), out value);
		}

		private static void Apply(ChaControl cha, int slot, int objectType, GameObject root, List<Material> materials, Value value)
		{
			if (cha == null || root == null || !WardrobeME.CanEditProps(cha))
			{
				return;
			}
			foreach (Material material in materials)
			{
				if (!(material == null))
				{
					WardrobeME.SetColorProp(cha, objectType, slot, root, material, "Color4", value.Color);
					if (value.HasGloss && material.HasProperty("_Glossiness4"))
					{
						WardrobeME.SetFloatProp(cha, objectType, slot, root, material, "Glossiness4", value.Gloss);
					}
					if (value.HasMetallic && material.HasProperty("_Metallic4"))
					{
						WardrobeME.SetFloatProp(cha, objectType, slot, root, material, "Metallic4", value.Metallic);
					}
				}
			}
		}

		internal static void ApplyClothing(ChaControl cha, int slot, Value value)
		{
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = WardrobeClothingSlotsKoi.Controller(cha);
			if ((UnityEngine.Object)(object)clothingSlotsControllerKoi != null && clothingSlotsControllerKoi.Visual(slot, 0) != null)
			{
				ApplyClothing(cha, slot, 0, value);
				return;
			}
			GameObject root = ClothingRoot(cha, slot, 0);
			Apply(cha, slot, 1, root, Materials(root), value);
		}

		internal static void ApplyAccessory(ChaControl cha, int slot, Value value)
		{
			Apply(cha, slot, 2, (cha == null) ? null : AccessoryRoot(cha, slot), (cha == null) ? new List<Material>() : AccessoryMaterials(cha, slot), value);
		}

		internal static void ApplyClothing(ChaControl cha, int kind, int index, Value value)
		{
			ClothingSlotsControllerKoi clothingSlotsControllerKoi = WardrobeClothingSlotsKoi.Controller(cha);
			if ((UnityEngine.Object)(object)clothingSlotsControllerKoi != null && clothingSlotsControllerKoi.Visual(kind, index) != null && WardrobeCompositeTopKoi.UsesNativeFourth(cha, kind, index))
			{
				ChaFileClothes.PartsInfo partsInfo = clothingSlotsControllerKoi.Part(kind, index);
				if (partsInfo != null && partsInfo.colorInfo != null && partsInfo.colorInfo.Length > 3)
				{
					partsInfo.colorInfo[3].baseColor = value.Color;
					clothingSlotsControllerKoi.Repaint(kind, index);
					clothingSlotsControllerKoi.CaptureAppearance(kind, index);
				}
				return;
			}
			if (index == 0 && ((UnityEngine.Object)(object)clothingSlotsControllerKoi == null || !clothingSlotsControllerKoi.IsManagedPrimary(kind)))
			{
				ApplyClothing(cha, kind, value);
				return;
			}
			foreach (Material item in Materials(ClothingMaterialRenderers(cha, kind, index)))
			{
				if (!(item == null))
				{
					item.SetColor("_Color4", value.Color);
					if (value.HasGloss && item.HasProperty("_Glossiness4"))
					{
						item.SetFloat("_Glossiness4", value.Gloss);
					}
					if (value.HasMetallic && item.HasProperty("_Metallic4"))
					{
						item.SetFloat("_Metallic4", value.Metallic);
					}
				}
			}
			WardrobeClothingSlotsKoi.CaptureAppearance(cha, kind, index);
		}

		internal static void ResetAccessory(ChaControl cha, int slot)
		{
			WardrobeHomeColor4Koi.ResetAccessory(cha, slot);
		}
	}
	internal static class WardrobeME
	{
		internal sealed class Color4Override
		{
			internal bool HasColor;

			internal bool HasGloss;

			internal bool HasMetallic;

			internal Color Color;

			internal float Gloss;

			internal float Metallic;
		}

		internal const string ME_GUID = "com.deathweasel.bepinex.materialeditor";

		internal const int OBJ_CLOTHING = 1;

		internal const int OBJ_ACCESSORY = 2;

		private static bool _init;

		private static bool _studioInit;

		private static Type _ctrlType;

		private static MethodInfo _loadData;

		private static MethodInfo _setAndGetTexId;

		private static PropertyInfo _curCoordIdx;

		private static MethodInfo _mpSerializeDef;

		private static MethodInfo _mpDeserializeDef;

		private static Dictionary<string, Type> _listTypes;

		private static Dictionary<string, FieldInfo> _listFields;

		private static MethodInfo _setMatColor;

		private static MethodInfo _setMatFloat;

		private static MethodInfo _remMatColor;

		private static MethodInfo _remMatFloat;

		private static Type _objTypeEnum;

		private static Type _meStudioType;

		private static FieldInfo _meStudioInstance;

		private static PropertyInfo _meVisibleProp;

		private static MethodInfo _mePopItemDropdown;

		private static FieldInfo _meItemTypeDropdown;

		private static readonly string[] ListKeys = new string[9] { "MaterialShaderList", "RendererPropertyList", "MaterialFloatPropertyList", "MaterialKeywordPropertyList", "MaterialColorPropertyList", "MaterialTexturePropertyList", "MaterialCopyList", "MaterialNamePropertyList", "ProjectorPropertyList" };

		private static List<string> _activeListKeys;

		internal static bool HasStudioME
		{
			get
			{
				InitStudio();
				if (_meStudioType != null && _meStudioInstance != null && _meVisibleProp != null && _mePopItemDropdown != null)
				{
					return _meItemTypeDropdown != null;
				}
				return false;
			}
		}

		private static Assembly FindMEAssembly()
		{
			return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault((Assembly a) => a.GetType("KK_Plugins.MaterialEditor.MaterialEditorCharaController") != null || a.GetType("KK_Plugins.MaterialEditor.MEStudio") != null);
		}

		private static void InitStudio()
		{
			if (_studioInit)
			{
				return;
			}
			_studioInit = true;
			Assembly assembly = FindMEAssembly();
			if (assembly == null)
			{
				return;
			}
			try
			{
				_meStudioType = assembly.GetType("KK_Plugins.MaterialEditor.MEStudio");
				if (!(_meStudioType == null))
				{
					BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
					BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
					_meStudioInstance = _meStudioType.GetField("Instance", bindingFlags);
					_meVisibleProp = _meStudioType.GetProperty("Visible", bindingFlags | BindingFlags.FlattenHierarchy) ?? assembly.GetType("MaterialEditorAPI.MaterialEditorUI")?.GetProperty("Visible", bindingFlags);
					_mePopItemDropdown = _meStudioType.GetMethod("PopulateItemTypeDropdown", bindingAttr, null, new Type[1] { typeof(ChaControl) }, null);
					_meItemTypeDropdown = _meStudioType.GetField("ItemTypeDropDown", bindingFlags);
					if (_meStudioInstance == null || _meVisibleProp == null || _mePopItemDropdown == null || _meItemTypeDropdown == null)
					{
						WardrobePlugin.Log.LogWarning((object)("ME Studio lookup incomplete: Instance=" + (_meStudioInstance != null) + " Visible=" + (_meVisibleProp != null) + " Populate=" + (_mePopItemDropdown != null) + " Dropdown=" + (_meItemTypeDropdown != null)));
					}
				}
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("ME Studio-open lookup failed: " + ex.Message));
			}
		}

		private static bool Init()
		{
			if (_init)
			{
				return _ctrlType != null;
			}
			_init = true;
			InitStudio();
			Type type = FindMEAssembly()?.GetType("KK_Plugins.MaterialEditor.MaterialEditorCharaController");
			if (type == null)
			{
				WardrobePlugin.Log.LogInfo((object)"MaterialEditor not found — shader copy disabled.");
				return false;
			}
			BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			Dictionary<string, Type> dictionary = new Dictionary<string, Type>();
			Dictionary<string, FieldInfo> dictionary2 = new Dictionary<string, FieldInfo>();
			List<string> list = new List<string>();
			string[] listKeys = ListKeys;
			foreach (string text in listKeys)
			{
				FieldInfo field = type.GetField(text, bindingAttr);
				if (field == null)
				{
					switch (text)
					{
					case "MaterialKeywordPropertyList":
					case "MaterialNamePropertyList":
					case "ProjectorPropertyList":
						WardrobePlugin.Log.LogInfo((object)("ME reflection: " + text + " unavailable on this ME version; continuing without it."));
						continue;
					}
				}
				if (field == null)
				{
					WardrobePlugin.Log.LogWarning((object)("ME reflection: missing " + text + " — shader copy disabled."));
					return false;
				}
				dictionary2[text] = field;
				dictionary[text] = field.FieldType;
				list.Add(text);
			}
			MethodInfo methodInfo = type.GetMethod("LoadData", bindingAttr, null, new Type[4]
			{
				typeof(bool),
				typeof(bool),
				typeof(bool),
				typeof(bool)
			}, null) ?? type.GetMethod("LoadData", bindingAttr, null, new Type[3]
			{
				typeof(bool),
				typeof(bool),
				typeof(bool)
			}, null);
			MethodInfo[] methods = typeof(MessagePackSerializer).GetMethods(BindingFlags.Static | BindingFlags.Public);
			MethodInfo methodInfo2 = methods.FirstOrDefault((MethodInfo m) => m.Name == "Serialize" && m.IsGenericMethod && m.GetParameters().Length == 1 && m.ReturnType == typeof(byte[]));
			MethodInfo methodInfo3 = methods.FirstOrDefault((MethodInfo m) => m.Name == "Deserialize" && m.IsGenericMethod && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(byte[]));
			if (methodInfo == null || methodInfo2 == null || methodInfo3 == null)
			{
				WardrobePlugin.Log.LogWarning((object)"ME reflection: LoadData/MessagePack lookup failed — shader copy disabled.");
				return false;
			}
			_listFields = dictionary2;
			_listTypes = dictionary;
			_activeListKeys = list;
			_loadData = methodInfo;
			_setAndGetTexId = type.GetMethod("SetAndGetTextureID", bindingAttr, null, new Type[1] { typeof(byte[]) }, null);
			_curCoordIdx = type.GetProperty("CurrentCoordinateIndex", bindingAttr);
			_mpSerializeDef = methodInfo2;
			_mpDeserializeDef = methodInfo3;
			_ctrlType = type;
			try
			{
				MethodInfo[] methods2 = type.GetMethods(bindingAttr);
				foreach (MethodInfo methodInfo4 in methods2)
				{
					switch (methodInfo4.Name)
					{
					case "SetMaterialColorProperty":
						if (methodInfo4.GetParameters().Length == 7)
						{
							_setMatColor = methodInfo4;
						}
						break;
					case "SetMaterialFloatProperty":
						if (methodInfo4.GetParameters().Length == 7)
						{
							_setMatFloat = methodInfo4;
						}
						break;
					case "RemoveMaterialColorProperty":
						if (methodInfo4.GetParameters().Length == 6)
						{
							_remMatColor = methodInfo4;
						}
						break;
					case "RemoveMaterialFloatProperty":
						if (methodInfo4.GetParameters().Length == 6)
						{
							_remMatFloat = methodInfo4;
						}
						break;
					}
				}
				if (_setMatColor != null)
				{
					_objTypeEnum = _setMatColor.GetParameters()[1].ParameterType;
				}
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("ME property-setter lookup failed (Channel 4 disabled): " + ex.Message));
			}
			return true;
		}

		internal static bool CanEditProps(ChaControl cha)
		{
			if (Init() && _setMatColor != null && _setMatFloat != null && _objTypeEnum != null)
			{
				return GetCtrl(cha) != null;
			}
			return false;
		}

		internal static bool OpenStudioClothes(ChaControl cha, int slot)
		{
			return OpenStudioItem(cha, 1, slot);
		}

		internal static bool OpenStudioAccessory(ChaControl cha, int slot)
		{
			return OpenStudioItem(cha, 2, slot);
		}

		private static bool OpenStudioItem(ChaControl cha, int objectType, int slot)
		{
			if (!HasStudioME || cha == null || slot < 0)
			{
				return false;
			}
			try
			{
				object value = _meStudioInstance.GetValue(null);
				if (value == null)
				{
					return false;
				}
				_meVisibleProp.SetValue(null, true, null);
				_mePopItemDropdown.Invoke(value, new object[1] { cha });
				object value2 = _meItemTypeDropdown.GetValue(null);
				if (value2 == null)
				{
					return false;
				}
				((Component)value2).gameObject.SetActive(value: true);
				Type type = value2.GetType();
				IList list = (IList)type.GetProperty("options").GetValue(value2, null);
				List<string> list2 = new List<string>();
				for (int i = 0; i < list.Count; i++)
				{
					list2.Add(list[i]?.GetType().GetProperty("text")?.GetValue(list[i], null) as string);
				}
				int num = WardrobeMESelection.FindOption(list2, objectType, slot);
				if (num < 0)
				{
					return false;
				}
				type.GetProperty("value").SetValue(value2, num, null);
				return true;
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("ME OpenStudioItem failed: " + ex));
				return false;
			}
		}

		internal static void SetColorProp(ChaControl cha, int objType, int slot, GameObject go, Material mat, string prop, Color v)
		{
			if (!CanEditProps(cha) || go == null || mat == null)
			{
				return;
			}
			try
			{
				_setMatColor.Invoke(GetCtrl(cha), new object[7]
				{
					slot,
					Enum.ToObject(_objTypeEnum, objType),
					mat,
					prop,
					v,
					go,
					true
				});
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("ME SetColorProp " + prop + " failed: " + ex.Message));
			}
		}

		internal static void SetFloatProp(ChaControl cha, int objType, int slot, GameObject go, Material mat, string prop, float v)
		{
			if (!CanEditProps(cha) || go == null || mat == null)
			{
				return;
			}
			try
			{
				_setMatFloat.Invoke(GetCtrl(cha), new object[7]
				{
					slot,
					Enum.ToObject(_objTypeEnum, objType),
					mat,
					prop,
					v,
					go,
					true
				});
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("ME SetFloatProp " + prop + " failed: " + ex.Message));
			}
		}

		internal static void RemoveProps(ChaControl cha, int objType, int slot, GameObject go, Material mat, string[] colorProps, string[] floatProps)
		{
			if (!CanEditProps(cha) || _remMatColor == null || _remMatFloat == null || go == null || mat == null)
			{
				return;
			}
			try
			{
				object obj = Enum.ToObject(_objTypeEnum, objType);
				object ctrl = GetCtrl(cha);
				string[] array = colorProps;
				foreach (string text in array)
				{
					_remMatColor.Invoke(ctrl, new object[6] { slot, obj, mat, text, go, true });
				}
				array = floatProps;
				foreach (string text2 in array)
				{
					_remMatFloat.Invoke(ctrl, new object[6] { slot, obj, mat, text2, go, true });
				}
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("ME RemoveProps failed: " + ex.Message));
			}
		}

		internal static PluginData CoordExt(ChaFileCoordinate coord)
		{
			if (coord != null)
			{
				return ExtendedSave.GetExtendedDataById(coord, "com.deathweasel.bepinex.materialeditor");
			}
			return null;
		}

		internal static byte[] SerializeCoordExt(ChaFileCoordinate coord)
		{
			PluginData val = CoordExt(coord);
			if (val?.data == null || val.data.Count == 0)
			{
				return null;
			}
			try
			{
				Dictionary<string, byte[]> dictionary = new Dictionary<string, byte[]>();
				foreach (KeyValuePair<string, object> datum in val.data)
				{
					dictionary[datum.Key] = datum.Value as byte[];
				}
				return MessagePackSerializer.Serialize(dictionary);
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("ME ext serialize failed: " + ex.Message));
				return null;
			}
		}

		internal static void AttachCoordExt(ChaFileCoordinate coord, byte[] meBytes)
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Expected O, but got Unknown
			if (coord == null || meBytes == null)
			{
				return;
			}
			try
			{
				Dictionary<string, byte[]> dictionary = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(meBytes);
				if (dictionary == null)
				{
					return;
				}
				PluginData val = new PluginData();
				foreach (KeyValuePair<string, byte[]> item in dictionary)
				{
					val.data[item.Key] = item.Value;
				}
				ExtendedSave.SetExtendedDataById(coord, "com.deathweasel.bepinex.materialeditor", val);
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("ME ext attach failed: " + ex.Message));
			}
		}

		private static IList DeserializeList(string key, PluginData pd)
		{
			if (pd == null || !pd.data.TryGetValue(key, out var value) || !(value is byte[] array))
			{
				return null;
			}
			try
			{
				return (IList)_mpDeserializeDef.MakeGenericMethod(_listTypes[key]).Invoke(null, new object[1] { array });
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("ME deserialize " + key + " failed: " + ex.Message));
				return null;
			}
		}

		internal static Dictionary<int, Color4Override> ReadClothingColor4(ChaFileCoordinate source)
		{
			Dictionary<int, Color4Override> dictionary = new Dictionary<int, Color4Override>();
			if (!Init() || source == null)
			{
				return dictionary;
			}
			PluginData pd = CoordExt(source);
			string[] array = new string[2] { "MaterialColorPropertyList", "MaterialFloatPropertyList" };
			for (int i = 0; i < array.Length; i++)
			{
				IList list = DeserializeList(array[i], pd);
				if (list == null)
				{
					continue;
				}
				foreach (object item in list)
				{
					if (item == null || ObjTypeOf(item) != 1)
					{
						continue;
					}
					int num = SlotOf(item);
					if (num < 0 || num >= 9)
					{
						continue;
					}
					Type type = item.GetType();
					if (!(type.GetField("Property")?.GetValue(item) is string text))
					{
						continue;
					}
					string text2 = text.TrimStart('_');
					if (text2 != "Color4" && text2 != "Glossiness4" && text2 != "Metallic4")
					{
						continue;
					}
					object obj = type.GetField("Value")?.GetValue(item);
					if (obj != null)
					{
						if (!dictionary.TryGetValue(num, out var value))
						{
							value = (dictionary[num] = new Color4Override());
						}
						if (text2 == "Color4" && obj is Color)
						{
							value.Color = (Color)obj;
							value.HasColor = true;
						}
						else if (text2 == "Glossiness4")
						{
							value.Gloss = Convert.ToSingle(obj, CultureInfo.InvariantCulture);
							value.HasGloss = true;
						}
						else if (text2 == "Metallic4")
						{
							value.Metallic = Convert.ToSingle(obj, CultureInfo.InvariantCulture);
							value.HasMetallic = true;
						}
					}
				}
			}
			return dictionary;
		}

		internal static void ClearClothingColor4(ChaControl cha, int slot)
		{
			if (!(cha == null) && slot >= 0 && slot < 9)
			{
				ClearClothingColor4(cha, slot, WardrobeMaterialColor4Koi.ClothingMaterials(cha, slot, 0), color: true, gloss: true, metallic: true);
			}
		}

		internal static void ClearClothingColor4(ChaControl cha, int slot, IEnumerable<Material> materials, bool color, bool gloss, bool metallic)
		{
			if (!(cha == null) && slot >= 0 && slot < 9)
			{
				ClearColor4(cha, 1, slot, cha.objClothes[slot], materials, color, gloss, metallic);
			}
		}

		internal static void ClearAccessoryColor4(ChaControl cha, int slot)
		{
			if (!(cha == null) && slot >= 0)
			{
				ClearAccessoryColor4(cha, slot, WardrobeMaterialColor4Koi.AccessoryMaterials(cha, slot), color: true, gloss: true, metallic: true);
			}
		}

		internal static void ClearAccessoryColor4(ChaControl cha, int slot, IEnumerable<Material> materials, bool color, bool gloss, bool metallic)
		{
			if (!(cha == null) && slot >= 0)
			{
				ClearColor4(cha, 2, slot, WardrobeMaterialColor4Koi.AccessoryRoot(cha, slot), materials, color, gloss, metallic);
			}
		}

		private static string CleanMaterialName(string name)
		{
			return (name ?? "").Replace("(Instance)", "").Trim();
		}

		private static void ClearColor4(ChaControl cha, int objectType, int slot, GameObject root, IEnumerable<Material> materials, bool color, bool gloss, bool metallic)
		{
			if (!CanEditProps(cha) || (!color && !gloss && !metallic))
			{
				return;
			}
			Material[] array = ((materials == null) ? new Material[0] : materials.Where((Material x) => x != null).Distinct().ToArray());
			if (array.Length == 0)
			{
				return;
			}
			HashSet<string> hashSet = new HashSet<string>(array.Select((Material x) => CleanMaterialName(x.name)));
			string[] colorProps = ((!color) ? new string[0] : new string[1] { "Color4" });
			List<string> list = new List<string>();
			if (gloss)
			{
				list.Add("Glossiness4");
			}
			if (metallic)
			{
				list.Add("Metallic4");
			}
			if (root != null)
			{
				Material[] array2 = array;
				foreach (Material mat in array2)
				{
					RemoveProps(cha, objectType, slot, root, mat, colorProps, list.ToArray());
				}
			}
			object ctrl = GetCtrl(cha);
			int num2 = CurCoordIdx(ctrl);
			string[] array3 = new string[2] { "MaterialColorPropertyList", "MaterialFloatPropertyList" };
			foreach (string key in array3)
			{
				if (!(_listFields[key].GetValue(ctrl) is IList list2))
				{
					continue;
				}
				for (int num3 = list2.Count - 1; num3 >= 0; num3--)
				{
					object obj = list2[num3];
					if (obj != null && ObjTypeOf(obj) == objectType && SlotOf(obj) == slot && CoordIdxOf(obj) == num2)
					{
						string name = obj.GetType().GetField("MaterialName")?.GetValue(obj) as string;
						if (hashSet.Contains(CleanMaterialName(name)) && obj.GetType().GetField("Property")?.GetValue(obj) is string text)
						{
							string text2 = text.TrimStart('_');
							if ((color && text2 == "Color4") || (gloss && text2 == "Glossiness4") || (metallic && text2 == "Metallic4"))
							{
								list2.RemoveAt(num3);
							}
						}
					}
				}
			}
		}

		private static Dictionary<int, byte[]> DeserializeTexDict(PluginData pd)
		{
			if (pd == null || !pd.data.TryGetValue("TextureDictionary", out var value) || !(value is byte[] bytes))
			{
				return new Dictionary<int, byte[]>();
			}
			try
			{
				return MessagePackSerializer.Deserialize<Dictionary<int, byte[]>>(bytes) ?? new Dictionary<int, byte[]>();
			}
			catch
			{
				return new Dictionary<int, byte[]>();
			}
		}

		private static int ObjTypeOf(object rec)
		{
			return Convert.ToInt32(rec.GetType().GetField("ObjectType").GetValue(rec));
		}

		private static int SlotOf(object rec)
		{
			return Convert.ToInt32(rec.GetType().GetField("Slot").GetValue(rec));
		}

		private static int CoordIdxOf(object rec)
		{
			FieldInfo fieldInfo = rec?.GetType().GetField("CoordinateIndex");
			if (!(fieldInfo != null))
			{
				return 0;
			}
			return Convert.ToInt32(fieldInfo.GetValue(rec));
		}

		private static object GetCtrl(ChaControl cha)
		{
			if (!(cha != null))
			{
				return null;
			}
			return cha.gameObject.GetComponent(_ctrlType);
		}

		private static int CurCoordIdx(object meCtrl)
		{
			try
			{
				return (_curCoordIdx != null) ? Convert.ToInt32(_curCoordIdx.GetValue(meCtrl, null)) : 0;
			}
			catch
			{
				return 0;
			}
		}

		private static void Replace(ChaControl cha, PluginData srcExt, int objType, int slot)
		{
			if (!Init())
			{
				return;
			}
			object meCtrl = GetCtrl(cha);
			if (meCtrl == null)
			{
				return;
			}
			Dictionary<int, byte[]> srcTex = DeserializeTexDict(srcExt);
			int num = CurCoordIdx(meCtrl);
			Dictionary<int, int> importedTextures = new Dictionary<int, int>();
			foreach (string activeListKey in _activeListKeys)
			{
				IList list = (IList)_listFields[activeListKey].GetValue(meCtrl);
				if (list == null)
				{
					continue;
				}
				for (int num2 = list.Count - 1; num2 >= 0; num2--)
				{
					if (WardrobeKoiCoordinateIntegrity.MatchesMaterialRecord(CoordIdxOf(list[num2]), ObjTypeOf(list[num2]), SlotOf(list[num2]), num, objType, slot))
					{
						list.RemoveAt(num2);
					}
				}
				IList list2 = DeserializeList(activeListKey, srcExt);
				if (list2 == null)
				{
					continue;
				}
				foreach (object item in list2)
				{
					if (ObjTypeOf(item) != objType || (slot >= 0 && SlotOf(item) != slot))
					{
						continue;
					}
					item.GetType().GetField("CoordinateIndex")?.SetValue(item, num);
					if (_setAndGetTexId != null)
					{
						WardrobeReferenceAppearance.RemapTextures(item, delegate(int id)
						{
							if (importedTextures.TryGetValue(id, out var value))
							{
								return value;
							}
							if (!srcTex.TryGetValue(id, out var value2))
							{
								throw new InvalidOperationException("Missing copied material texture " + id + ".");
							}
							value = (int)_setAndGetTexId.Invoke(meCtrl, new object[1] { value2 });
							importedTextures[id] = value;
							return value;
						});
					}
					list.Add(item);
				}
			}
		}

		internal static void ReplaceSlot(ChaControl cha, PluginData srcExt, int objType, int slot)
		{
			Replace(cha, srcExt, objType, slot);
		}

		internal static bool ApplyDetachedClothing(ChaControl cha, ChaFileCoordinate source, int slot, GameObject root)
		{
			if (source == null || root == null || !Init())
			{
				return false;
			}
			object ctrl = GetCtrl(cha);
			PluginData val = CoordExt(source);
			if (ctrl == null || val == null)
			{
				return false;
			}
			Dictionary<int, byte[]> dictionary = DeserializeTexDict(val);
			bool result = false;
			foreach (string item in _activeListKeys.OrderBy((string k) => (!(k == "MaterialCopyList")) ? ((k == "MaterialShaderList") ? 1 : 2) : 0))
			{
				IList list = DeserializeList(item, val);
				if (list == null)
				{
					continue;
				}
				foreach (object item2 in list)
				{
					if (ObjTypeOf(item2) != 1 || SlotOf(item2) != slot)
					{
						continue;
					}
					result = true;
					try
					{
						FieldInfo field = item2.GetType().GetField("TexID");
						int? num = ((field == null) ? ((int?)null) : (field.GetValue(item2) as int?));
						if (num.HasValue)
						{
							field.SetValue(item2, (_setAndGetTexId != null && dictionary.TryGetValue(num.Value, out var value)) ? new int?((int)_setAndGetTexId.Invoke(ctrl, new object[1] { value })) : ((int?)null));
						}
						WardrobeMERecordApplier.Apply((Component)ctrl, item, item2, initializeCardShader: true, null, root);
					}
					catch (Exception ex)
					{
						WardrobePlugin.Log.LogWarning((object)("Extra clothing material replay: " + ex.GetBaseException().Message));
					}
				}
			}
			return result;
		}

		internal static void ReplaceAllOfType(ChaControl cha, PluginData srcExt, int objType)
		{
			Replace(cha, srcExt, objType, -1);
		}

		internal static void CopyAccessorySlot(ChaControl cha, PluginData srcExt, int srcSlot, int destSlot)
		{
			if (!Init())
			{
				return;
			}
			object ctrl = GetCtrl(cha);
			if (ctrl == null)
			{
				return;
			}
			Dictionary<int, byte[]> dictionary = DeserializeTexDict(srcExt);
			int num = CurCoordIdx(ctrl);
			foreach (string activeListKey in _activeListKeys)
			{
				IList list = (IList)_listFields[activeListKey].GetValue(ctrl);
				if (list == null)
				{
					continue;
				}
				for (int num2 = list.Count - 1; num2 >= 0; num2--)
				{
					if (WardrobeKoiCoordinateIntegrity.MatchesMaterialRecord(CoordIdxOf(list[num2]), ObjTypeOf(list[num2]), SlotOf(list[num2]), num, 2, destSlot))
					{
						list.RemoveAt(num2);
					}
				}
				IList list2 = DeserializeList(activeListKey, srcExt);
				if (list2 == null)
				{
					continue;
				}
				foreach (object item in list2)
				{
					if (ObjTypeOf(item) != 2 || SlotOf(item) != srcSlot)
					{
						continue;
					}
					Type type = item.GetType();
					type.GetField("CoordinateIndex")?.SetValue(item, num);
					type.GetField("Slot")?.SetValue(item, destSlot);
					FieldInfo field = type.GetField("TexID");
					if (field != null && _setAndGetTexId != null)
					{
						int? num3 = (int?)field.GetValue(item);
						if (num3.HasValue)
						{
							field.SetValue(item, dictionary.TryGetValue(num3.Value, out var value) ? new int?((int)_setAndGetTexId.Invoke(ctrl, new object[1] { value })) : ((int?)null));
						}
					}
					list.Add(item);
				}
			}
		}

		internal static void WriteCoordExt(ChaControl cha, ChaFileCoordinate coord)
		{
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Expected O, but got Unknown
			if (!Init() || coord == null)
			{
				return;
			}
			object ctrl = GetCtrl(cha);
			if (ctrl == null)
			{
				return;
			}
			try
			{
				BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
				int num = CurCoordIdx(ctrl);
				PluginData val = new PluginData();
				bool flag = false;
				HashSet<int> hashSet = new HashSet<int>();
				foreach (string activeListKey in _activeListKeys)
				{
					IList list = (IList)_listFields[activeListKey].GetValue(ctrl);
					IList list2 = (IList)Activator.CreateInstance(_listTypes[activeListKey]);
					if (list != null)
					{
						foreach (object item in list)
						{
							int num2 = ObjTypeOf(item);
							if (num2 == 1 || num2 == 2)
							{
								FieldInfo field = item.GetType().GetField("CoordinateIndex");
								if (!(field != null) || Convert.ToInt32(field.GetValue(item)) == num)
								{
									list2.Add(item);
									WardrobeReferenceAppearance.CollectTextureIds(item, hashSet);
								}
							}
						}
					}
					if (list2.Count > 0)
					{
						val.data[activeListKey] = _mpSerializeDef.MakeGenericMethod(_listTypes[activeListKey]).Invoke(null, new object[1] { list2 });
						flag = true;
					}
					else
					{
						val.data[activeListKey] = null;
					}
				}
				Dictionary<int, byte[]> dictionary = new Dictionary<int, byte[]>();
				if (_ctrlType.GetField("TextureDictionary", bindingAttr)?.GetValue(ctrl) is IDictionary dictionary2)
				{
					foreach (DictionaryEntry item2 in dictionary2)
					{
						int num3 = Convert.ToInt32(item2.Key);
						if (hashSet.Contains(num3) && item2.Value != null && item2.Value.GetType().GetProperty("Data")?.GetValue(item2.Value, null) is byte[] value)
						{
							dictionary[num3] = value;
						}
					}
				}
				val.data["TextureDictionary"] = ((dictionary.Count > 0) ? MessagePackSerializer.Serialize(dictionary) : null);
				ExtendedSave.SetExtendedDataById(coord, "com.deathweasel.bepinex.materialeditor", flag ? val : null);
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("ME ext write failed: " + ex));
			}
		}

		internal static void Apply(ChaControl cha, bool clothes, bool accessories)
		{
			if ((UnityEngine.Object)(object)WardrobePlugin.Instance != null)
			{
				((MonoBehaviour)(object)WardrobePlugin.Instance).StartCoroutine(ApplyAndWait(cha, clothes, accessories));
			}
		}

		internal static IEnumerator ApplyAndWait(ChaControl cha, bool clothes, bool accessories)
		{
			if (!Init())
			{
				yield break;
			}
			object ctrl = GetCtrl(cha);
			if (ctrl == null)
			{
				yield break;
			}
			if (_loadData.GetParameters().Length != 4)
			{
				int activeCoordinate = CurCoordIdx(ctrl);
				{
					foreach (string item in _activeListKeys.OrderBy(RecordOrder))
					{
						foreach (object item2 in (IEnumerable)_listFields[item].GetValue(ctrl))
						{
							Type type = item2.GetType();
							if (WardrobeMERecordApplier.Claims(Convert.ToInt32(type.GetField("ObjectType").GetValue(item2)), Convert.ToInt32(type.GetField("CoordinateIndex").GetValue(item2)), activeCoordinate, clothes, accessories))
							{
								try
								{
									WardrobeMERecordApplier.Apply((Component)ctrl, item, item2);
								}
								catch (Exception ex)
								{
									WardrobePlugin.Log.LogWarning((object)("ME scoped legacy apply failed: " + ex.Message));
								}
							}
						}
					}
					yield break;
				}
			}
			IEnumerator co;
			try
			{
				co = (IEnumerator)_loadData.Invoke(ctrl, new object[4] { clothes, accessories, false, false });
			}
			catch (Exception ex2)
			{
				WardrobePlugin.Log.LogWarning((object)("ME apply failed: " + ex2.Message));
				yield break;
			}
			if (co != null)
			{
				while (co.MoveNext())
				{
					yield return co.Current;
				}
			}
		}

		internal static void ReapplyClothingSlot(ChaControl character, int slot)
		{
			if (character == null || !Init())
			{
				return;
			}
			object ctrl = GetCtrl(character);
			if (ctrl == null)
			{
				return;
			}
			int num = CurCoordIdx(ctrl);
			foreach (string item in _activeListKeys.OrderBy(RecordOrder))
			{
				if (!(_listFields[item].GetValue(ctrl) is IList list))
				{
					continue;
				}
				foreach (object item2 in list)
				{
					if (item2 != null && ObjTypeOf(item2) == 1 && SlotOf(item2) == slot && CoordIdxOf(item2) == num)
					{
						WardrobeMERecordApplier.Apply((Component)ctrl, item, item2);
					}
				}
			}
		}

		private static int RecordOrder(string key)
		{
			return key switch
			{
				"MaterialShaderList" => 2, 
				"MaterialNamePropertyList" => 1, 
				"MaterialCopyList" => 0, 
				_ => 3, 
			};
		}

		internal static void AttachReference(ChaFileControl card, ChaFileCoordinate coordinate, int index)
		{
			if (card == null || coordinate == null || !Init())
			{
				return;
			}
			PluginData source = ExtendedSave.GetExtendedDataById((ChaFile)card, "com.deathweasel.bepinex.materialeditor");
			if (source != null)
			{
				PluginData val = WardrobeReferenceAppearance.Materials(source, index, _listTypes, () => ReadCardTextures(source));
				ExtendedSave.SetExtendedDataById(coordinate, "com.deathweasel.bepinex.materialeditor", val);
			}
		}

		private static IDictionary<int, byte[]> ReadCardTextures(PluginData source)
		{
			Type type = _ctrlType.Assembly.GetType("KK_Plugins.MaterialEditor.TextureSaveHandler");
			if (type != null)
			{
				object obj = type.GetField("Instance", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
				MethodInfo methodInfo = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((MethodInfo m) => m.Name == "Load" && m.IsGenericMethodDefinition && m.GetParameters().Length == 3);
				FieldInfo field = _ctrlType.GetField("TextureDictionary", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (obj != null && methodInfo != null && field != null)
				{
					IDictionary dictionary = methodInfo.MakeGenericMethod(field.FieldType).Invoke(obj, new object[3] { source, "TextureDictionary", true }) as IDictionary;
					Dictionary<int, byte[]> dictionary2 = new Dictionary<int, byte[]>();
					try
					{
						if (dictionary != null)
						{
							foreach (DictionaryEntry item in dictionary)
							{
								if (item.Value?.GetType().GetProperty("Data")?.GetValue(item.Value, null) is byte[] array)
								{
									dictionary2[Convert.ToInt32(item.Key)] = (byte[])array.Clone();
								}
							}
						}
					}
					finally
					{
						if (dictionary != null)
						{
							foreach (DictionaryEntry item2 in dictionary)
							{
								(item2.Value as IDisposable)?.Dispose();
							}
						}
					}
					return dictionary2;
				}
			}
			return DeserializeTexDict(source);
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
	internal static class WardrobeMESelection
	{
		internal const int Clothing = 1;

		internal const int Accessory = 2;

		private static readonly string[] ClothesLabels = new string[9] { "Top", "Bottom", "Bra", "Underwear", "Gloves", "Pantyhose", "Legwear", "Indoor Shoes", "Outdoor Shoes" };

		internal static int FindOption(IList<string> options, int objectType, int slot)
		{
			if (options == null || slot < 0)
			{
				return -1;
			}
			string text;
			switch (objectType)
			{
			case 1:
				if (slot >= ClothesLabels.Length)
				{
					return -1;
				}
				text = "Clothes " + ClothesLabels[slot];
				break;
			case 2:
				text = "Accessory " + (slot + 1).ToString("00");
				break;
			default:
				return -1;
			}
			for (int i = 0; i < options.Count; i++)
			{
				if (options[i] == text)
				{
					return i;
				}
				if (objectType == 2 && options[i] != null && options[i].StartsWith(text + " "))
				{
					return i;
				}
			}
			if (objectType == 1 && slot == 7)
			{
				for (int j = 0; j < options.Count; j++)
				{
					if (options[j] == "Clothes Shoes")
					{
						return j;
					}
				}
			}
			return -1;
		}
	}
	internal sealed class WardrobeMixerColumns
	{
		internal readonly float[] X = new float[11];

		internal readonly float[] Width = new float[11];

		internal static WardrobeMixerColumns Calculate(float available, float label, float clear, float random, float on, float half, float off)
		{
			WardrobeMixerColumns wardrobeMixerColumns = new WardrobeMixerColumns();
			float[] width = wardrobeMixerColumns.Width;
			width[0] = label;
			width[1] = 20f;
			width[5] = random;
			width[6] = (width[7] = (Math.Max(46f, clear) - 1f) / 2f);
			width[8] = on;
			width[9] = half;
			width[10] = off;
			float num = label + width[1] + random + width[6] + width[7] + on + half + off + 10f;
			width[2] = (width[3] = (width[4] = Math.Max(0f, Math.Min(40f, (available - num) / 3f))));
			float num2 = Math.Max(0f, available - num - width[2] * 3f) / 7f;
			for (int i = 1; i < width.Length; i++)
			{
				wardrobeMixerColumns.X[i] = wardrobeMixerColumns.X[i - 1] + width[i - 1] + 1f + ((i <= 6 || i == 8) ? num2 : 0f);
			}
			return wardrobeMixerColumns;
		}

		internal static bool CanSelectState(int kind, bool equipped, int state)
		{
			if (equipped && kind >= 0 && kind < 8)
			{
				switch (state)
				{
				case 1:
					if (kind != 4 && kind != 6)
					{
						return kind != 7;
					}
					return false;
				case 0:
				case 2:
					return true;
				}
			}
			return false;
		}
	}
	internal static class WardrobeOverlay
	{
		internal const string KCOX_GUID = "KCOX";

		private static readonly string[] ClothIds = new string[9] { "ct_clothesTop", "ct_clothesBot", "ct_bra", "ct_shorts", "ct_gloves", "ct_panst", "ct_socks", "ct_shoes_inner", "ct_shoes_outer" };

		private static bool _init;

		private static Type _ctrlType;

		private static Type _texDataType;

		private static PropertyInfo _curOverlays;

		private static PropertyInfo _curSizeOverrides;

		private static MethodInfo _getOverlayTex;

		private static MethodInfo _refreshTexture;

		private static MethodInfo _refreshAll;

		private static MethodInfo _dumpBase;

		private static MethodInfo _onCoordSaved;

		private static MethodInfo _onCoordLoaded;

		private static MethodInfo _initBaseCustomTextureClothes;

		private static PropertyInfo _texTexture;

		private static PropertyInfo _texBytes;

		private static FieldInfo _texOverride;

		private static MethodInfo _mpDeserializeDef;

		private static Type _overlayDictType;

		internal static string OverlayDir
		{
			get
			{
				string text = Path.Combine(Paths.GameRootPath, "UserData\\overlays");
				try
				{
					if (!Directory.Exists(text))
					{
						Directory.CreateDirectory(text);
					}
				}
				catch
				{
				}
				return text;
			}
		}

		private static bool Init()
		{
			if (_init)
			{
				return _ctrlType != null;
			}
			_init = true;
			try
			{
				Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault((Assembly a) => a.GetType("KoiClothesOverlayX.KoiClothesOverlayController") != null);
				if (assembly == null)
				{
					WardrobePlugin.Log.LogInfo((object)"Overlay Mods not found — clothes overlay disabled.");
					return false;
				}
				Type type = assembly.GetType("KoiClothesOverlayX.KoiClothesOverlayController");
				_texDataType = assembly.GetType("KoiClothesOverlayX.ClothesTexData");
				if (type == null || _texDataType == null)
				{
					return false;
				}
				BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
				_curOverlays = type.GetProperty("CurrentOverlayTextures", bindingAttr);
				_curSizeOverrides = type.GetProperty("CurrentTextureSizeOverrides", bindingAttr);
				_getOverlayTex = type.GetMethod("GetOverlayTex", bindingAttr, null, new Type[2]
				{
					typeof(string),
					typeof(bool)
				}, null);
				_refreshTexture = type.GetMethod("RefreshTexture", bindingAttr, null, new Type[1] { typeof(string) }, null);
				_refreshAll = type.GetMethod("RefreshAllTextures", bindingAttr, null, Type.EmptyTypes, null);
				_dumpBase = type.GetMethod("DumpBaseTexture", bindingAttr, null, new Type[2]
				{
					typeof(string),
					typeof(Action<byte[]>)
				}, null);
				_onCoordSaved = type.GetMethod("OnCoordinateBeingSaved", bindingAttr, null, new Type[1] { typeof(ChaFileCoordinate) }, null);
				_onCoordLoaded = type.GetMethod("OnCoordinateBeingLoaded", bindingAttr, null, new Type[2]
				{
					typeof(ChaFileCoordinate),
					typeof(bool)
				}, null);
				_initBaseCustomTextureClothes = typeof(ChaControl).GetMethod("InitBaseCustomTextureClothes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[2]
				{
					typeof(bool),
					typeof(int)
				}, null);
				_texTexture = _texDataType.GetProperty("Texture", bindingAttr);
				_texBytes = _texDataType.GetProperty("TextureBytes", bindingAttr);
				_texOverride = _texDataType.GetField("Override", bindingAttr);
				_overlayDictType = typeof(Dictionary<, >).MakeGenericType(typeof(string), _texDataType);
				_mpDeserializeDef = typeof(MessagePackSerializer).GetMethods(BindingFlags.Static | BindingFlags.Public).FirstOrDefault((MethodInfo m) => m.Name == "Deserialize" && m.IsGenericMethod && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(byte[]));
				if (_curOverlays == null || _getOverlayTex == null || _refreshTexture == null)
				{
					WardrobePlugin.Log.LogWarning((object)"Overlay reflection incomplete — clothes overlay disabled.");
					_ctrlType = null;
					return false;
				}
				_ctrlType = type;
				return true;
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("Overlay bridge init failed: " + ex.Message));
				_ctrlType = null;
				return false;
			}
		}

		private static object GetCtrl(ChaControl cha)
		{
			if (!(cha != null) || !(_ctrlType != null))
			{
				return null;
			}
			return cha.gameObject.GetComponent(_ctrlType);
		}

		internal static bool Available(ChaControl cha)
		{
			if (Init())
			{
				return GetCtrl(cha) != null;
			}
			return false;
		}

		internal static string ClothId(int kind)
		{
			if (kind < 0 || kind >= ClothIds.Length)
			{
				return null;
			}
			return ClothIds[kind];
		}

		private static IDictionary CurrentMap(object ctrl)
		{
			if (ctrl != null)
			{
				return _curOverlays.GetValue(ctrl, null) as IDictionary;
			}
			return null;
		}

		private static IDictionary CurrentSizeMap(object ctrl)
		{
			if (ctrl != null && !(_curSizeOverrides == null))
			{
				return _curSizeOverrides.GetValue(ctrl, null) as IDictionary;
			}
			return null;
		}

		internal static Texture2D GetOverlay(ChaControl cha, int kind)
		{
			object ctrl = GetCtrl(cha);
			string text = ClothId(kind);
			if (ctrl == null || text == null)
			{
				return null;
			}
			object obj = _getOverlayTex.Invoke(ctrl, new object[2] { text, false });
			if (obj != null)
			{
				return _texTexture.GetValue(obj, null) as Texture2D;
			}
			return null;
		}

		internal static bool HasOverlay(ChaControl cha, int kind)
		{
			object ctrl = GetCtrl(cha);
			string text = ClothId(kind);
			if (ctrl == null || text == null)
			{
				return false;
			}
			object obj = _getOverlayTex.Invoke(ctrl, new object[2] { text, false });
			if (obj != null && _texBytes.GetValue(obj, null) is byte[] array && array != null)
			{
				return array.Length != 0;
			}
			return false;
		}

		internal static bool GetHideBase(ChaControl cha, int kind)
		{
			object ctrl = GetCtrl(cha);
			string text = ClothId(kind);
			if (ctrl == null || text == null)
			{
				return false;
			}
			object obj = _getOverlayTex.Invoke(ctrl, new object[2] { text, false });
			if (obj != null)
			{
				return Convert.ToBoolean(_texOverride.GetValue(obj));
			}
			return false;
		}

		internal static void SetHideBase(ChaControl cha, int kind, bool on)
		{
			object ctrl = GetCtrl(cha);
			string text = ClothId(kind);
			if (ctrl != null && text != null)
			{
				object obj = _getOverlayTex.Invoke(ctrl, new object[2] { text, on });
				if (obj != null)
				{
					_texOverride.SetValue(obj, on);
					RefreshKoiClothing(cha, ctrl, kind);
				}
			}
		}

		internal static void SetOverlay(ChaControl cha, int kind, byte[] png)
		{
			object ctrl = GetCtrl(cha);
			string text = ClothId(kind);
			if (ctrl != null && text != null)
			{
				object obj = _getOverlayTex.Invoke(ctrl, new object[2]
				{
					text,
					png != null
				});
				if (obj != null)
				{
					_texBytes.SetValue(obj, png, null);
					RefreshKoiClothing(cha, ctrl, kind);
				}
			}
		}

		internal static string ExportOverlay(ChaControl cha, int kind)
		{
			object ctrl = GetCtrl(cha);
			string text = ClothId(kind);
			if (ctrl == null || text == null)
			{
				return null;
			}
			object obj = _getOverlayTex.Invoke(ctrl, new object[2] { text, false });
			if (obj == null || !(_texBytes.GetValue(obj, null) is byte[] array) || array == null || array.Length == 0)
			{
				return null;
			}
			string text2 = Path.Combine(OverlayDir, $"{text}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
			try
			{
				File.WriteAllBytes(text2, array);
				return text2;
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("Overlay export failed: " + ex.Message));
				return null;
			}
		}

		internal static void DumpOriginal(ChaControl cha, int kind)
		{
			object ctrl = GetCtrl(cha);
			string id = ClothId(kind);
			if (ctrl == null || id == null || _dumpBase == null)
			{
				WardrobeGUI.SetMessage(WardrobeLang.T("Nothing to dump."));
				return;
			}
			Action<byte[]> action = delegate(byte[] bytes)
			{
				if (bytes == null)
				{
					WardrobeGUI.SetMessage(WardrobeLang.T("Nothing to dump."));
					return;
				}
				string path = Path.Combine(OverlayDir, $"{id}_original_{DateTime.Now:yyyyMMdd_HHmmss}.png");
				try
				{
					File.WriteAllBytes(path, bytes);
					WardrobeGUI.SetMessage(WardrobeLang.T("Saved to UserData\\overlays."));
				}
				catch (Exception ex2)
				{
					WardrobePlugin.Log.LogWarning((object)("Base dump failed: " + ex2.Message));
				}
			};
			try
			{
				_dumpBase.Invoke(ctrl, new object[2] { id, action });
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("DumpBaseTexture failed: " + ex.Message));
			}
		}

		internal static PluginData CoordExt(ChaFileCoordinate coord)
		{
			if (coord != null)
			{
				return ExtendedSave.GetExtendedDataById(coord, "KCOX");
			}
			return null;
		}

		internal static void WriteCoordExt(ChaControl cha, ChaFileCoordinate coord)
		{
			if (!Init() || coord == null)
			{
				return;
			}
			object ctrl = GetCtrl(cha);
			if (ctrl == null || _onCoordSaved == null)
			{
				return;
			}
			try
			{
				_onCoordSaved.Invoke(ctrl, new object[1] { coord });
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("Overlay ext write failed: " + ex.Message));
			}
		}

		internal static byte[] SerializeCoordExt(ChaFileCoordinate coord)
		{
			PluginData val = CoordExt(coord);
			if (val?.data == null || val.data.Count == 0)
			{
				return null;
			}
			try
			{
				Dictionary<string, byte[]> dictionary = new Dictionary<string, byte[]>();
				foreach (KeyValuePair<string, object> datum in val.data)
				{
					dictionary[datum.Key] = datum.Value as byte[];
				}
				return MessagePackSerializer.Serialize(dictionary);
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("Overlay ext serialize failed: " + ex.Message));
				return null;
			}
		}

		internal static void AttachCoordExt(ChaFileCoordinate coord, byte[] bytes)
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Expected O, but got Unknown
			if (coord == null || bytes == null)
			{
				return;
			}
			try
			{
				Dictionary<string, byte[]> dictionary = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(bytes);
				if (dictionary == null)
				{
					return;
				}
				PluginData val = new PluginData();
				foreach (KeyValuePair<string, byte[]> item in dictionary)
				{
					val.data[item.Key] = item.Value;
				}
				ExtendedSave.SetExtendedDataById(coord, "KCOX", val);
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("Overlay ext attach failed: " + ex.Message));
			}
		}

		internal static void ApplyToLive(ChaControl cha, ChaFileCoordinate src)
		{
			if (!Init() || src == null)
			{
				return;
			}
			object ctrl = GetCtrl(cha);
			if (ctrl == null)
			{
				return;
			}
			try
			{
				IDictionary dictionary = CurrentMap(ctrl);
				if (dictionary == null)
				{
					return;
				}
				dictionary.Clear();
				IDictionary dictionary2 = DeserializeOverlays(CoordExt(src));
				if (dictionary2 != null)
				{
					foreach (DictionaryEntry item in dictionary2)
					{
						dictionary[item.Key] = item.Value;
					}
				}
				IDictionary dictionary3 = CurrentSizeMap(ctrl);
				if (dictionary3 != null)
				{
					dictionary3.Clear();
					Dictionary<string, int> dictionary4 = DeserializeSizeOverrides(CoordExt(src));
					if (dictionary4 != null)
					{
						foreach (KeyValuePair<string, int> item2 in dictionary4)
						{
							dictionary3[item2.Key] = item2.Value;
						}
					}
				}
				for (int i = 0; i < ClothIds.Length; i++)
				{
					RefreshKoiClothing(cha, ctrl, i);
				}
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("Overlay apply failed: " + ex.Message));
			}
		}

		internal static void CopySlot(ChaControl cha, PluginData srcExt, int kind)
		{
			if (!Init())
			{
				return;
			}
			object ctrl = GetCtrl(cha);
			string text = ClothId(kind);
			if (ctrl == null || text == null)
			{
				return;
			}
			IDictionary dictionary = CurrentMap(ctrl);
			if (dictionary == null)
			{
				return;
			}
			try
			{
				IDictionary source = DeserializeOverlays(srcExt);
				ReplaceKoiOverlayKey(dictionary, source, text);
				ReplaceKoiOverlayKey(dictionary, source, ColorMaskId(text));
				RefreshKoiClothing(cha, ctrl, kind);
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("Overlay slot copy failed: " + ex.Message));
			}
		}

		private static string ColorMaskId(string id)
		{
			return "Colormask_" + id;
		}

		private static void ReplaceKoiOverlayKey(IDictionary live, IDictionary source, string id)
		{
			object obj = ((source != null && source.Contains(id)) ? source[id] : null);
			if (obj != null)
			{
				live[id] = obj;
			}
			else if (live.Contains(id))
			{
				live.Remove(id);
			}
		}

		private static void RefreshKoiClothing(ChaControl cha, object ctrl, int kind)
		{
			if (!(cha == null) && ctrl != null && kind >= 0 && kind < ClothIds.Length)
			{
				cha.StartCoroutine(RefreshKoiClothingWhenReady(cha, ctrl, kind));
			}
		}

		private static IEnumerator RefreshKoiClothingWhenReady(ChaControl cha, object ctrl, int kind)
		{
			int expectedId = cha.nowCoordinate.clothes.parts[kind].id;
			if (expectedId <= 0)
			{
				yield break;
			}
			int frame = 0;
			while (frame++ < 120)
			{
				bool num = cha.objClothes != null && kind < cha.objClothes.Length && cha.objClothes[kind] != null;
				bool flag = cha.infoClothes != null && kind < cha.infoClothes.Length && cha.infoClothes[kind] != null && cha.infoClothes[kind].Id == cha.nowCoordinate.clothes.parts[kind].id;
				if (num && flag)
				{
					break;
				}
				yield return null;
			}
			if (cha.objClothes == null || kind >= cha.objClothes.Length || !(cha.objClothes[kind] != null) || cha.infoClothes == null || kind >= cha.infoClothes.Length || cha.infoClothes[kind] == null || cha.infoClothes[kind].Id != expectedId)
			{
				WardrobePlugin.Log.LogWarning((object)("Overlay renderer did not settle for clothing slot " + kind + "."));
				yield break;
			}
			if (_initBaseCustomTextureClothes == null)
			{
				WardrobePlugin.Log.LogWarning((object)"Overlay clothing texture initializer was not found.");
				yield break;
			}
			_initBaseCustomTextureClothes.Invoke(cha, new object[2] { true, kind });
			ChaFileClothes.PartsInfo.ColorInfo[] colorInfo = cha.nowCoordinate.clothes.parts[kind].colorInfo;
			cha.ChangeCustomClothes(main: true, kind, updateColor: true, colorInfo != null && colorInfo.Length != 0 && colorInfo[0].pattern > 0, colorInfo != null && colorInfo.Length > 1 && colorInfo[1].pattern > 0, colorInfo != null && colorInfo.Length > 2 && colorInfo[2].pattern > 0, colorInfo != null && colorInfo.Length > 3 && colorInfo[3].pattern > 0);
			WardrobeME.ReapplyClothingSlot(cha, kind);
		}

		private static IDictionary DeserializeOverlays(PluginData srcExt)
		{
			if (srcExt?.data == null || _mpDeserializeDef == null || _overlayDictType == null)
			{
				return null;
			}
			if (!srcExt.data.TryGetValue("Overlays", out var value) || !(value is byte[] array))
			{
				return null;
			}
			try
			{
				return _mpDeserializeDef.MakeGenericMethod(_overlayDictType).Invoke(null, new object[1] { array }) as IDictionary;
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("Overlay deserialize failed: " + ex.Message));
				return null;
			}
		}

		private static Dictionary<string, int> DeserializeSizeOverrides(PluginData srcExt)
		{
			if (srcExt?.data == null || !srcExt.data.TryGetValue("TextureSizeOverride", out var value) || !(value is byte[] bytes))
			{
				return null;
			}
			try
			{
				return MessagePackSerializer.Deserialize<Dictionary<string, int>>(bytes);
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("Overlay size-override deserialize failed: " + ex.Message));
				return null;
			}
		}

		internal static void AttachReference(ChaFileControl card, ChaFileCoordinate coordinate, int index)
		{
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Expected O, but got Unknown
			if (card != null && coordinate != null && Init())
			{
				PluginData extendedDataById = ExtendedSave.GetExtendedDataById((ChaFile)card, "KCOX");
				if (extendedDataById?.data != null)
				{
					PluginData val = new PluginData
					{
						version = extendedDataById.version
					};
					AddCardMap(extendedDataById, val, "ReadOverlayExtData", "Overlays", _overlayDictType, index);
					AddCardMap(extendedDataById, val, "ReadTextureSizeOverrideExtData", "TextureSizeOverride", typeof(Dictionary<string, int>), index);
					ExtendedSave.SetExtendedDataById(coordinate, "KCOX", val);
				}
			}
		}

		private static void AddCardMap(PluginData source, PluginData output, string reader, string key, Type innerType, int index)
		{
			if (source.data.ContainsKey(key))
			{
				MethodInfo method = _ctrlType.GetMethod(reader, BindingFlags.Static | BindingFlags.NonPublic, null, new Type[1] { typeof(PluginData) }, null);
				if (method == null)
				{
					throw new InvalidOperationException("The installed overlay provider cannot read reference " + key + ".");
				}
				byte[] array = WardrobeReferenceAppearance.CoordinateMap(method.Invoke(null, new object[1] { source }) as IDictionary, index, innerType);
				if (array != null)
				{
					output.data[key] = array;
				}
			}
		}
	}
	internal static class WardrobePalette
	{
		internal static void Open(string title, Color init, bool useAlpha, Action<Color> onChanged)
		{
			try
			{
				if (WardrobeContext.InStudio)
				{
					global::Studio.Studio instance = Singleton<global::Studio.Studio>.Instance;
					if (!(instance == null) && !(instance.colorPalette == null))
					{
						instance.colorPalette.Setup(title, init, delegate(Color c)
						{
							onChanged(c);
						}, useAlpha);
						instance.colorPalette.visible = true;
					}
				}
				else if (WardrobeContext.InMaker)
				{
					OpenKoiMaker(title, init, useAlpha, onChanged);
				}
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogError((object)("Palette open failed: " + ex));
			}
		}

		private static void OpenKoiMaker(string title, Color init, bool useAlpha, Action<Color> onChanged)
		{
			GameObject gameObject = GameObject.Find("CustomScene/CustomRoot/FrontUIGroup/CustomUIGroup/CvsColor/Top");
			if (gameObject == null)
			{
				WardrobePlugin.Log.LogWarning((object)"Maker CvsColor window not found — colors disabled in maker.");
				return;
			}
			CvsColor component = gameObject.GetComponent<CvsColor>();
			if (component == null)
			{
				WardrobePlugin.Log.LogWarning((object)"Maker CvsColor component not found — colors disabled in maker.");
				return;
			}
			string text = (string.IsNullOrEmpty(title) ? "Dress-Up Color" : title);
			component.Setup(text, (CvsColor.ConnectColorKind)text.GetHashCode(), init, delegate(Color c)
			{
				onChanged(c);
			}, useAlpha);
		}
	}
	[BepInPlugin("com.freelancer604.wardrobe", "Freelancer604 Vanity Studio", "1.11.7")]
	[BepInProcess("CharaStudio")]
	[BepInProcess("KoikatsuSunshine")]
	[BepInProcess("KoikatsuSunshine_VR")]
	[BepInDependency("marco.kkapi", "1.42.2")]
	public class WardrobePlugin : BaseUnityPlugin
	{
		public const string GUID = "com.freelancer604.wardrobe";

		public const string PluginName = "Freelancer604 Vanity Studio";

		public const string Version = "1.11.7";

		internal static WardrobePlugin Instance;

		internal static ManualLogSource Log;

		internal static ConfigEntry<KeyboardShortcut> hotkey;

		internal static ConfigEntry<string> lastT1Path;

		internal static ConfigEntry<string> lastT2Path;

		internal static ConfigEntry<string> languageCfg;

		internal static ConfigEntry<float> uiScaleCfg;

		private static ToolbarToggle toolbarToggle;

		private static readonly UiPointerCaptureState windowInput = new UiPointerCaptureState();

		private static bool windowInputCapturedThisFrame;

		private static readonly Dictionary<int, WardrobeSession> sessions = new Dictionary<int, WardrobeSession>();

		internal static bool WindowOwnsPointerInput
		{
			get
			{
				if (!windowInputCapturedThisFrame)
				{
					return windowInput.GestureOwned;
				}
				return true;
			}
		}

		private void Awake()
		{
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			Instance = this;
			Log = ((BaseUnityPlugin)this).Logger;
			WardrobeClothingSlotsKoi.Register();
			if (!StudioAPI.InsideStudio)
			{
				return;
			}
			hotkey = ((BaseUnityPlugin)this).Config.Bind<KeyboardShortcut>("General", "Toggle window", new KeyboardShortcut(KeyCode.W, new KeyCode[1] { KeyCode.LeftControl }), "Show/hide the Vanity Studio window (Studio only).");
			lastT1Path = ((BaseUnityPlugin)this).Config.Bind<string>("State", "Last T1 card", "", "Path of the last Target-1 coordinate card — auto-loaded when a session starts.");
			lastT2Path = ((BaseUnityPlugin)this).Config.Bind<string>("State", "Last T2 card", "", "Path of the last Target-2 coordinate card — auto-loaded when a session starts.");
			languageCfg = ((BaseUnityPlugin)this).Config.Bind<string>("General", "Language", "English", "UI language: English, 中文 (Simplified Chinese) or 日本語 (Japanese).");
			uiScaleCfg = ((BaseUnityPlugin)this).Config.Bind<float>("General", "UI scale", 1f, "Uniform suite scale. Drag the bottom-right window handle to change it.");
			WardrobeGUI.SetUserScale(uiScaleCfg.Value);
			WardrobeLang.Init(languageCfg.Value);
			if (StudioAPI.InsideStudio)
			{
				StudioAPI.StudioLoadedChanged += delegate
				{
					try
					{
						Texture2D texture2D = WardrobeEmoji.Tex("studioicon");
						Texture2D texture2D2 = ((texture2D != null) ? WardrobeEmoji.Downscale(texture2D, 32) : null);
						if (texture2D2 != null)
						{
							texture2D2.filterMode = FilterMode.Bilinear;
						}
						toolbarToggle = CustomToolbarButtons.AddLeftToolbarToggle(texture2D2, false, (Action<bool>)delegate(bool v)
						{
							WardrobeGUI.visible = v;
						});
					}
					catch (Exception ex)
					{
						Log.LogWarning((object)("Toolbar toggle failed to register: " + ex.Message));
					}
				};
			}
			MakerAPI.RegisterCustomSubCategories += delegate(object sender, RegisterSubCategoriesEvent e)
			{
				//IL_0015: Unknown result type (might be due to invalid IL or missing references)
				//IL_001b: Expected O, but got Unknown
				//IL_002a: Unknown result type (might be due to invalid IL or missing references)
				//IL_0034: Expected O, but got Unknown
				try
				{
					MakerCategory val = new MakerCategory(Clothes.Top.CategoryName, "F604DressUp", 0, "Vanity Studio");
					e.AddSubCategory(val);
					((RegisterCustomControlsEvent)e).AddControl<MakerButton>(new MakerButton("Open Vanity Studio", val, (BaseUnityPlugin)(object)this)).OnClick.AddListener(delegate
					{
						ToggleWindow();
					});
				}
				catch (Exception ex)
				{
					Log.LogWarning((object)("Maker button registration failed: " + ex.Message));
				}
			};
			MakerAPI.MakerExiting += delegate
			{
				try
				{
					sessions.Clear();
					WardrobeGUI.visible = false;
					WardrobeGUI.ResetSuitePages();
				}
				catch (Exception ex)
				{
					Log.LogWarning((object)("Maker exit cleanup failed: " + ex.Message));
				}
			};
		}

		public object GetSuiteBridgeV1()
		{
			return WardrobeGUI.GetSuiteBridgeV1();
		}

		internal static void SaveLastTarget(int which, string path)
		{
			try
			{
				if (which == 2)
				{
					lastT1Path.Value = path;
				}
				else
				{
					lastT2Path.Value = path;
				}
			}
			catch
			{
			}
		}

		internal static void SaveUiScale(float value)
		{
			if (uiScaleCfg != null)
			{
				uiScaleCfg.Value = value;
			}
		}

		private static void TryRestoreTargets(WardrobeSession s)
		{
			try
			{
				string value = lastT1Path.Value;
				string value2 = lastT2Path.Value;
				if (s.t1 == null && !string.IsNullOrEmpty(value) && File.Exists(value))
				{
					s.LoadTargetCard(2, value);
				}
				if (s.t2 == null && !string.IsNullOrEmpty(value2) && File.Exists(value2))
				{
					s.LoadTargetCard(3, value2);
				}
			}
			catch (Exception ex)
			{
				Log.LogWarning((object)("T1/T2 restore failed: " + ex.Message));
			}
		}

		internal static void ToggleWindow()
		{
			SetWindowVisible(!WardrobeGUI.visible);
		}

		internal static void SetWindowVisible(bool value)
		{
			WardrobeGUI.visible = value;
			try
			{
				if (toolbarToggle != null)
				{
					((BaseEditableGuiEntry<bool>)(object)toolbarToggle).Value = WardrobeGUI.visible;
				}
			}
			catch
			{
			}
		}

		private void Update()
		{
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			if (hotkey == null)
			{
				return;
			}
			if (WardrobeContext.InStudio)
			{
				WardrobeTextureRenderTarget.EnsureInstalled();
			}
			if (WardrobeContext.InStudio)
			{
				WardrobePseudoMakerGuards.EnsureInstalled();
			}
			if (!WardrobeContext.Active)
			{
				windowInput.Reset();
				windowInputCapturedThisFrame = false;
				return;
			}
			KeyboardShortcut value = hotkey.Value;
			bool flag = ((KeyboardShortcut)(ref value)).IsDown();
			if (!WardrobeGUI.visible && !flag && !windowInput.GestureOwned)
			{
				windowInput.Reset();
				windowInputCapturedThisFrame = false;
				return;
			}
			if (flag)
			{
				ToggleWindow();
			}
			bool buttonDown = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
			bool buttonHeld = Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);
			bool buttonUp = Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2);
			windowInputCapturedThisFrame = windowInput.Observe(WardrobeGUI.MouseOverWindow, buttonDown, buttonHeld, buttonUp);
			if (windowInputCapturedThisFrame)
			{
				Input.ResetInputAxes();
			}
		}

		private void OnDestroy()
		{
			SuiteWrapperTheme.Dispose();
			SuiteMakeUpTheme.Dispose();
		}

		private void OnGUI()
		{
			SuiteStudioUiVisibility.ObserveGuiFocus();
			if (SuiteStudioUiVisibility.IsVisible() && WardrobeGUI.visible && WardrobeContext.Active)
			{
				WardrobeGUI.Draw();
			}
		}

		internal static WardrobeSession GetSelectedSession()
		{
			ChaControl activeCharacter = WardrobeContext.GetActiveCharacter();
			if (activeCharacter == null)
			{
				return null;
			}
			int instanceID = activeCharacter.GetInstanceID();
			if (!sessions.TryGetValue(instanceID, out var value) || value.cha == null)
			{
				value = new WardrobeSession(activeCharacter);
				sessions[instanceID] = value;
				TryRestoreTargets(value);
				List<int> list = new List<int>();
				foreach (KeyValuePair<int, WardrobeSession> session in sessions)
				{
					if (session.Value.cha == null)
					{
						list.Add(session.Key);
					}
				}
				foreach (int item in list)
				{
					sessions.Remove(item);
				}
			}
			return value;
		}
	}
	internal static class WardrobePrefs
	{
		internal const int Fav = 1;

		internal const int None = 0;

		internal const int Dislike = -1;

		private static Dictionary<string, int> prefs;

		private static string PrefsPath => Path.Combine(WardrobeCompat.GameRoot, "UserData\\DressUp604\\item_prefs.txt");

		private static string LegacyPrefsPath => Path.Combine(WardrobeCompat.GameRoot, "UserData\\Wardrobe604\\item_prefs.txt");

		private static string Key(int category, string name)
		{
			return category + ":" + name;
		}

		internal static int Get(int category, string name)
		{
			EnsureLoaded();
			if (!prefs.TryGetValue(Key(category, name), out var value))
			{
				return 0;
			}
			return value;
		}

		internal static bool IsDisliked(int category, string name)
		{
			return Get(category, name) == -1;
		}

		internal static void Toggle(int category, string name, int state)
		{
			if (!string.IsNullOrEmpty(name))
			{
				EnsureLoaded();
				string key = Key(category, name);
				prefs.TryGetValue(key, out var value);
				if (value == state)
				{
					prefs.Remove(key);
				}
				else
				{
					prefs[key] = state;
				}
				Save();
			}
		}

		private static void EnsureLoaded()
		{
			if (prefs != null)
			{
				return;
			}
			prefs = new Dictionary<string, int>();
			try
			{
				string text = (File.Exists(PrefsPath) ? PrefsPath : (File.Exists(LegacyPrefsPath) ? LegacyPrefsPath : null));
				if (text == null)
				{
					return;
				}
				int num = 0;
				string[] array = File.ReadAllLines(text);
				for (int i = 0; i < array.Length; i++)
				{
					string[] array2 = array[i].Split(new char[1] { '|' }, 4);
					if (array2.Length >= 4 && int.TryParse(array2[0], out var result))
					{
						if (string.IsNullOrEmpty(array2[3]))
						{
							num++;
						}
						else
						{
							prefs[Key(result, array2[3])] = ((array2[2] == "F") ? 1 : (-1));
						}
					}
				}
				if (num > 0)
				{
					WardrobePlugin.Log.LogWarning((object)string.Format("Item prefs: dropped {0} legacy entr{1} with no stored name (cannot re-key).", num, (num == 1) ? "y" : "ies"));
				}
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogError((object)("Item prefs load failed: " + ex));
			}
		}

		private static void Save()
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(PrefsPath));
				StringBuilder stringBuilder = new StringBuilder();
				foreach (KeyValuePair<string, int> pref in prefs)
				{
					int num = pref.Key.IndexOf(':');
					string value = pref.Key.Substring(0, num);
					string value2 = pref.Key.Substring(num + 1);
					stringBuilder.Append(value).Append("|-|").Append((pref.Value == 1) ? 'F' : 'D')
						.Append('|')
						.Append(value2)
						.Append('\n');
				}
				File.WriteAllText(PrefsPath, stringBuilder.ToString());
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogError((object)("Item prefs save failed: " + ex));
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
	internal static class WardrobePushup
	{
		private const string SnapshotGuid = "com.freelancer604.vanity.pushup.snapshot";

		private const string NativeGuid = "com.deathweasel.bepinex.pushup";

		internal static void WriteLive(ChaControl character)
		{
			if (!(character == null))
			{
				KoiPushupCompatibility.WriteLiveCoordinate(character);
			}
		}

		internal static void ApplyClothing(ChaControl character, ChaFileCoordinate source, int slot = -1)
		{
			if (!(character == null) && source != null && (slot == -1 || slot == 0 || slot == 2))
			{
				PluginData data = ExtendedSave.GetExtendedDataById(source, "com.freelancer604.vanity.pushup.snapshot") ?? KoiPushupCompatibility.CaptureCardCoordinate(source);
				KoiPushupCompatibility.ApplyClothing(character, data, slot, recalculate: false);
			}
		}

		internal static void PrepareReference(ChaFileControl card, ChaFileCoordinate source, int coordinate)
		{
			PluginData val = KoiPushupCompatibility.CaptureCardCoordinate(card, coordinate);
			if (val != null)
			{
				ExtendedSave.SetExtendedDataById(source, "com.freelancer604.vanity.pushup.snapshot", val);
			}
		}

		internal static void Recalculate(ChaControl character)
		{
			KoiPushupCompatibility.Recalculate(character);
		}

		internal static byte[] Serialize(ChaControl character)
		{
			PluginData extendedDataById = ExtendedSave.GetExtendedDataById(character.nowCoordinate, "com.deathweasel.bepinex.pushup");
			PluginData val = KoiPushupCompatibility.CaptureCoordinate(character);
			if (extendedDataById == null && val == null)
			{
				return null;
			}
			using MemoryStream memoryStream = new MemoryStream();
			using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
			binaryWriter.Write(1);
			WriteData(binaryWriter, extendedDataById);
			WriteData(binaryWriter, val);
			return memoryStream.ToArray();
		}

		internal static void Attach(ChaFileCoordinate coordinate, byte[] bytes)
		{
			if (bytes == null)
			{
				return;
			}
			using MemoryStream memoryStream = new MemoryStream(bytes);
			using BinaryReader binaryReader = new BinaryReader(memoryStream);
			if (binaryReader.ReadInt32() != 1)
			{
				throw new IOException("Unsupported Pushup snapshot.");
			}
			PluginData val = ReadData(binaryReader);
			PluginData val2 = ReadData(binaryReader);
			if (memoryStream.Position != memoryStream.Length)
			{
				throw new IOException("Unexpected Pushup snapshot content.");
			}
			ExtendedSave.SetExtendedDataById(coordinate, "com.deathweasel.bepinex.pushup", val);
			ExtendedSave.SetExtendedDataById(coordinate, "com.freelancer604.vanity.pushup.snapshot", val2);
		}

		private static void WriteData(BinaryWriter writer, PluginData data)
		{
			writer.Write(data != null);
			if (data == null)
			{
				return;
			}
			writer.Write(data.version);
			writer.Write(data.data.Count);
			foreach (KeyValuePair<string, object> datum in data.data)
			{
				writer.Write(datum.Key);
				if (datum.Value == null)
				{
					writer.Write((byte)0);
					continue;
				}
				if (datum.Value is bool)
				{
					writer.Write((byte)1);
					writer.Write((bool)datum.Value);
					continue;
				}
				if (datum.Value is float)
				{
					writer.Write((byte)2);
					writer.Write((float)datum.Value);
					continue;
				}
				if (datum.Value is byte[])
				{
					writer.Write((byte)3);
					byte[] array = (byte[])datum.Value;
					writer.Write(array.Length);
					writer.Write(array);
					continue;
				}
				if (datum.Value is int)
				{
					writer.Write((byte)4);
					writer.Write((int)datum.Value);
					continue;
				}
				throw new IOException("Unsupported Pushup setting type: " + datum.Key);
			}
		}

		private static PluginData ReadData(BinaryReader reader)
		{
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Expected O, but got Unknown
			if (!reader.ReadBoolean())
			{
				return null;
			}
			PluginData val = new PluginData
			{
				version = reader.ReadInt32()
			};
			int num = reader.ReadInt32();
			if (num < 0 || num > 256)
			{
				throw new IOException("Invalid Pushup setting count.");
			}
			for (int i = 0; i < num; i++)
			{
				string key = reader.ReadString();
				object value;
				switch (reader.ReadByte())
				{
				case 0:
					value = null;
					break;
				case 1:
					value = reader.ReadBoolean();
					break;
				case 2:
					value = reader.ReadSingle();
					break;
				case 3:
				{
					int num2 = reader.ReadInt32();
					if (num2 < 0 || num2 > reader.BaseStream.Length - reader.BaseStream.Position)
					{
						throw new IOException("Invalid Pushup data length.");
					}
					value = reader.ReadBytes(num2);
					break;
				}
				case 4:
					value = reader.ReadInt32();
					break;
				default:
					throw new IOException("Unknown Pushup setting type.");
				}
				val.data.Add(key, value);
			}
			return val;
		}
	}
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
	internal static class WardrobeReferenceAppearance
	{
		private static readonly MethodInfo SerializeMethod = typeof(MessagePackSerializer).GetMethods().First((MethodInfo m) => m.Name == "Serialize" && m.IsGenericMethod && m.GetParameters().Length == 1 && m.ReturnType == typeof(byte[]));

		private static readonly MethodInfo DeserializeMethod = typeof(MessagePackSerializer).GetMethods().First((MethodInfo m) => m.Name == "Deserialize" && m.IsGenericMethod && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(byte[]));

		internal static byte[] Serialize(Type type, object value)
		{
			return (byte[])SerializeMethod.MakeGenericMethod(type).Invoke(null, new object[1] { value });
		}

		private static object Deserialize(Type type, byte[] value)
		{
			return DeserializeMethod.MakeGenericMethod(type).Invoke(null, new object[1] { value });
		}

		private static object Field(object value, string name)
		{
			FieldInfo fieldInfo = value?.GetType().GetField(name);
			if (!(fieldInfo == null))
			{
				return fieldInfo.GetValue(value);
			}
			return null;
		}

		internal static PluginData Materials(PluginData source, int coordinate, IDictionary<string, Type> listTypes, Func<IDictionary<int, byte[]>> loadTextures)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Expected O, but got Unknown
			if (source == null || source.data == null)
			{
				return null;
			}
			PluginData val = new PluginData();
			HashSet<int> hashSet = new HashSet<int>();
			foreach (KeyValuePair<string, Type> listType in listTypes)
			{
				if (!source.data.TryGetValue(listType.Key, out var value) || !(value is byte[]))
				{
					continue;
				}
				IList list = Deserialize(listType.Value, (byte[])value) as IList;
				IList list2 = (IList)Activator.CreateInstance(listType.Value);
				if (list != null)
				{
					foreach (object item in list)
					{
						object obj = Field(item, "ObjectType");
						object obj2 = Field(item, "CoordinateIndex");
						if (obj != null && (Convert.ToInt32(obj) == 1 || Convert.ToInt32(obj) == 2) && ((obj2 != null) ? Convert.ToInt32(obj2) : 0) == coordinate)
						{
							list2.Add(item);
							CollectTextureIds(item, hashSet);
						}
					}
				}
				if (list2.Count > 0)
				{
					val.data[listType.Key] = Serialize(listType.Value, list2);
				}
			}
			Dictionary<int, byte[]> dictionary = new Dictionary<int, byte[]>();
			if (hashSet.Count > 0)
			{
				IDictionary<int, byte[]> dictionary2 = loadTextures();
				foreach (int item2 in hashSet)
				{
					if (dictionary2 == null || !dictionary2.TryGetValue(item2, out var value2) || value2 == null || value2.Length == 0)
					{
						throw new InvalidOperationException("The reference card is missing Material Editor texture " + item2 + ".");
					}
					dictionary[item2] = value2;
				}
			}
			if (val.data.Count == 0)
			{
				return null;
			}
			val.data["TextureDictionary"] = MessagePackSerializer.Serialize(dictionary);
			return val;
		}

		internal static byte[] CoordinateMap(IDictionary maps, int coordinate, Type innerType)
		{
			if (maps != null)
			{
				foreach (DictionaryEntry map in maps)
				{
					if (Convert.ToInt32(map.Key) == coordinate && map.Value != null)
					{
						return Serialize(innerType, map.Value);
					}
				}
			}
			return null;
		}

		internal static void CollectTextureIds(object record, HashSet<int> ids)
		{
			object obj = Field(record, "TexID");
			if (obj != null)
			{
				ids.Add(Convert.ToInt32(obj));
			}
			object value = Field(record, "TexAnimationDef");
			obj = Field(value, "parentTexID");
			if (obj != null)
			{
				ids.Add(Convert.ToInt32(obj));
			}
			if (!(Field(value, "frames") is IEnumerable enumerable))
			{
				return;
			}
			foreach (object item in enumerable)
			{
				obj = Field(item, "texID");
				if (obj != null)
				{
					ids.Add(Convert.ToInt32(obj));
				}
			}
		}

		internal static void RemapTextures(object record, Func<int, int> map)
		{
			RemapField(record, "TexID", map);
			object value = Field(record, "TexAnimationDef");
			RemapField(value, "parentTexID", map);
			if (Field(value, "frames") is IList list)
			{
				for (int i = 0; i < list.Count; i++)
				{
					object value2 = list[i];
					RemapField(value2, "texID", map);
					list[i] = value2;
				}
			}
		}

		private static void RemapField(object value, string name, Func<int, int> map)
		{
			FieldInfo fieldInfo = value?.GetType().GetField(name);
			object obj = ((fieldInfo == null) ? null : fieldInfo.GetValue(value));
			if (obj != null)
			{
				fieldInfo.SetValue(value, map(Convert.ToInt32(obj)));
			}
		}
	}
	internal sealed class WardrobeSearchCache<T>
	{
		private readonly Func<T, string> getName;

		private IList<T> source;

		private int sourceCount;

		private string appliedQuery;

		private List<T> results;

		internal WardrobeSearchCache(Func<T, string> getName)
		{
			this.getName = getName;
		}

		internal List<T> Get(IList<T> catalog, string query, bool isLayout)
		{
			query = query ?? "";
			if (results != null && (!isLayout || (source == catalog && sourceCount == catalog.Count && string.Equals(appliedQuery, query, StringComparison.Ordinal))))
			{
				return results;
			}
			List<T> list = new List<T>();
			if (query.Length == 0)
			{
				list.AddRange(catalog);
			}
			else
			{
				string value = query.ToLowerInvariant();
				foreach (T item in catalog)
				{
					string text = getName(item);
					if (text != null && text.ToLowerInvariant().Contains(value))
					{
						list.Add(item);
					}
				}
			}
			source = catalog;
			sourceCount = catalog.Count;
			appliedQuery = query;
			results = list;
			return results;
		}

		internal void Clear()
		{
			source = null;
			results = null;
			appliedQuery = null;
			sourceCount = 0;
		}
	}
	internal class WardrobeSession
	{
		private class UndoEntry
		{
			public byte[] coordBytes;

			public byte[] clothesStates;

			public bool[] accessoryStates;

			public Dictionary<string, int> sel;

			public WardrobePseudoMakerState.Snapshot compatibilityState;

			public int reservedSlots;
		}

		private sealed class KoiAccessoryRequest
		{
			public int slot;

			public ChaFileAccessory.PartsInfo part;

			public PluginData slotME;

			public bool replaceSlotME;

			public int copyMESourceSlot = -1;

			public bool? visibility;

			public bool invalidateOnBulkChange;

			public bool[] restoreVisibilityStates;

			public WardrobePseudoMakerState.Snapshot restoreCompatibilityState;

			public PluginData allME;

			public bool replaceAllME;

			public bool markCustom;

			public bool cleanupPreview;

			public string diagnostic;

			public int bulkGeneration;

			public ChaFileAccessory.PartsInfo[] batchTarget;

			public bool applyClothesME;
		}

		private sealed class SourceClothingPart
		{
			internal byte[] Bytes;

			internal ChaFileClothes.PartsInfo Part;
		}

		public ChaControl cha;

		public byte[] baselineBytes;

		private ChaFileCoordinate baselineCoord;

		public ChaFileCoordinate t1;

		public ChaFileCoordinate t2;

		public string t1Path;

		public string t2Path;

		public Texture2D t1Tex;

		public Texture2D t2Tex;

		public readonly Dictionary<string, int> mixSel = new Dictionary<string, int>();

		private const int UndoDepth = 10;

		private readonly List<UndoEntry> undoStack = new List<UndoEntry>();

		private readonly List<UndoEntry> redoStack = new List<UndoEntry>();

		public static readonly string[] ClothesSlotNames = new string[9] { "Top", "Bottom", "Bra", "Underwear", "Gloves", "Pantyhose", "Socks", "Shoes (In)", "Shoes (Out)" };

		internal static readonly ChaListDefine.CategoryNo[] ClothesCategories = new ChaListDefine.CategoryNo[9]
		{
			ChaListDefine.CategoryNo.co_top,
			ChaListDefine.CategoryNo.co_bot,
			ChaListDefine.CategoryNo.co_bra,
			ChaListDefine.CategoryNo.co_shorts,
			ChaListDefine.CategoryNo.co_gloves,
			ChaListDefine.CategoryNo.co_panst,
			ChaListDefine.CategoryNo.co_socks,
			ChaListDefine.CategoryNo.co_shoes,
			ChaListDefine.CategoryNo.co_shoes
		};

		public const int ClothesSlotCount = 9;

		public const int NativeAccessorySlots = 20;

		public const int MaxAccessorySlots = 150;

		private readonly HashSet<int> visibleAccSlots = new HashSet<int>();

		private static bool moreAccessoriesAddSlotResolved;

		private static MethodInfo moreAccessoriesAddSlot;

		private static MethodInfo moreAccessoriesArraySync;

		private static MethodInfo moreAccessoriesUpdateUI;

		private readonly Queue<KoiAccessoryRequest> koiAccessoryQueue = new Queue<KoiAccessoryRequest>();

		private bool koiAccessoryQueueRunning;

		private int koiBulkGeneration;

		private const int SnapMagic = 1177956420;

		private bool[] koiRestoreAccessoryStates;

		private WardrobePseudoMakerState.Snapshot koiRestoreCompatibilityState;

		public static byte[] OutfitClipboard;

		private static byte[] accessoryItemClipboard;

		private static ChaFileAccessory.PartsInfo accessoryItemPart;

		private static int accessoryItemSourceSlot;

		private static bool accessoryItemVisible;

		private static byte[] clothesItemClipboard;

		private static int clothesItemKind = -1;

		private static byte clothesItemState;

		private string lastCoalesceKey;

		private float lastCoalesceTime;

		private const float CoalesceWindow = 1.5f;

		private static ClothingSlotRecord clothingSlotClipboard;

		private static int clothingSlotClipboardIndex;

		private static readonly ConditionalWeakTable<ClothingSlotRecord, SourceClothingPart> sourceClothingParts = new ConditionalWeakTable<ClothingSlotRecord, SourceClothingPart>();

		public bool CanUndo => undoStack.Count > 0;

		public bool CanRedo => redoStack.Count > 0;

		public int UndoCount => undoStack.Count;

		public int RedoCount => redoStack.Count;

		public int AccessorySlotCount => AccessorySlotCountFor(cha);

		internal bool CanAddAccessorySlot
		{
			get
			{
				if (!WardrobeContext.Active || AccessorySlotCount >= 150)
				{
					return false;
				}
				ResolveMoreAccessoriesAddSlot();
				if (WardrobeContext.InMaker)
				{
					return moreAccessoriesAddSlot != null;
				}
				if (WardrobeContext.InStudio)
				{
					return moreAccessoriesArraySync != null;
				}
				return false;
			}
		}

		private ClothingSlotsControllerKoi ClothingSlots => WardrobeClothingSlotsKoi.Controller(cha);

		internal static int AccessorySlotCountFor(ChaControl character)
		{
			ChaFileAccessory.PartsInfo[] array = ((character != null && character.nowCoordinate != null && character.nowCoordinate.accessory != null) ? character.nowCoordinate.accessory.parts : null);
			if (array != null)
			{
				return Math.Min(150, array.Length);
			}
			return 0;
		}

		private static int SourceAccessoryCount(ChaFileCoordinate source)
		{
			ChaFileAccessory.PartsInfo[] array = source?.accessory?.parts;
			if (array != null)
			{
				return Math.Min(150, array.Length);
			}
			return 0;
		}

		private int AccessoryMixSlotCount()
		{
			int accessorySlotCount = AccessorySlotCount;
			accessorySlotCount = Math.Max(accessorySlotCount, SourceAccessoryCount(Source(1)));
			accessorySlotCount = Math.Max(accessorySlotCount, SourceAccessoryCount(t1));
			accessorySlotCount = Math.Max(accessorySlotCount, SourceAccessoryCount(t2));
			return Math.Min(150, accessorySlotCount);
		}

		private int EnsureAccessoryMixSlots()
		{
			int num = AccessoryMixSlotCount();
			for (int i = 0; i < num; i++)
			{
				string key = "Acc" + i;
				if (!mixSel.ContainsKey(key))
				{
					mixSel[key] = 1;
				}
			}
			return num;
		}

		private static void ResolveMoreAccessoriesAddSlot()
		{
			if (!moreAccessoriesAddSlotResolved)
			{
				moreAccessoriesAddSlotResolved = true;
				Type type = Type.GetType("MoreAccessoriesKOI.Accessories, MoreAccessories", throwOnError: false);
				if (type != null)
				{
					moreAccessoriesAddSlot = type.GetMethod("AddSlot", BindingFlags.Static | BindingFlags.Public, null, new Type[1] { typeof(int) }, null);
				}
				Type type2 = Type.GetType("MoreAccessoriesKOI.MoreAccessories, MoreAccessories", throwOnError: false);
				if (type2 != null)
				{
					moreAccessoriesArraySync = type2.GetMethod("ArraySync", BindingFlags.Static | BindingFlags.Public, null, new Type[1] { typeof(ChaControl) }, null);
					moreAccessoriesUpdateUI = type2.GetMethod("ManuallyUpdateUI", BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null);
				}
			}
		}

		internal bool ShouldAppendAccessorySlot(int freeSlot)
		{
			if (CanAddAccessorySlot)
			{
				if (freeSlot >= 0)
				{
					return AccessorySlotCount > 20;
				}
				return true;
			}
			return false;
		}

		internal int TryAddAccessorySlot()
		{
			if (!CanAddAccessorySlot)
			{
				return -1;
			}
			int accessorySlotCount = AccessorySlotCount;
			try
			{
				if (WardrobeContext.InMaker)
				{
					moreAccessoriesAddSlot.Invoke(null, new object[1] { 1 });
				}
				else if (!TryAddKoiStudioAccessorySlot())
				{
					return -1;
				}
				if (AccessorySlotCount > accessorySlotCount)
				{
					WardrobePseudoMakerGuards.Reserve(cha, AccessorySlotCount);
					EnsureAccessoryMixSlots();
					visibleAccSlots.Add(accessorySlotCount);
					return accessorySlotCount;
				}
				WardrobePlugin.Log.LogWarning((object)"MoreAccessories AddSlot did not expand the live accessory array.");
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogError((object)("MoreAccessories AddSlot failed: " + ex));
			}
			return -1;
		}

		private bool TryAddKoiStudioAccessorySlot()
		{
			return TryExpandKoiStudioAccessoryCapacity(AccessorySlotCount + 1);
		}

		private bool TryExpandKoiStudioAccessoryCapacity(int desiredCount)
		{
			if (cha == null || cha.nowCoordinate == null || cha.nowCoordinate.accessory == null || cha.nowCoordinate.accessory.parts == null || moreAccessoriesArraySync == null)
			{
				return false;
			}
			ChaFileCoordinate nowCoordinate = cha.nowCoordinate;
			ChaFileCoordinate chaFileCoordinate = WardrobeCompat.FileCoord(cha);
			ChaFileAccessory.PartsInfo[] parts = nowCoordinate.accessory.parts;
			ChaFileAccessory.PartsInfo[] array = ((chaFileCoordinate != null && chaFileCoordinate.accessory != null) ? chaFileCoordinate.accessory.parts : null);
			desiredCount = Math.Min(150, Math.Max(parts.Length, desiredCount));
			if (desiredCount == parts.Length)
			{
				return true;
			}
			try
			{
				ChaFileAccessory.PartsInfo[] array2 = new ChaFileAccessory.PartsInfo[desiredCount];
				Array.Copy(parts, array2, parts.Length);
				for (int i = parts.Length; i < array2.Length; i++)
				{
					array2[i] = new ChaFileAccessory.PartsInfo();
				}
				nowCoordinate.accessory.parts = array2;
				if (chaFileCoordinate != null && chaFileCoordinate != nowCoordinate && chaFileCoordinate.accessory != null)
				{
					ChaFileAccessory.PartsInfo[] array3 = new ChaFileAccessory.PartsInfo[desiredCount];
					if (array != null)
					{
						Array.Copy(array, array3, Math.Min(array.Length, desiredCount));
					}
					for (int j = ((array != null) ? array.Length : 0); j < array3.Length; j++)
					{
						array3[j] = new ChaFileAccessory.PartsInfo();
					}
					chaFileCoordinate.accessory.parts = array3;
				}
				moreAccessoriesArraySync.Invoke(null, new object[1] { cha });
				return AccessorySlotCount == desiredCount;
			}
			catch (Exception ex)
			{
				nowCoordinate.accessory.parts = parts;
				if (chaFileCoordinate != null && chaFileCoordinate != nowCoordinate && chaFileCoordinate.accessory != null)
				{
					chaFileCoordinate.accessory.parts = array;
				}
				try
				{
					moreAccessoriesArraySync.Invoke(null, new object[1] { cha });
				}
				catch
				{
				}
				WardrobePlugin.Log.LogError((object)("MoreAccessories Studio slot append failed: " + ex));
				return false;
			}
		}

		private void EnsureKoiAccessoryCapacity(ChaFileAccessory target)
		{
			int valueOrDefault = (target?.parts?.Length).GetValueOrDefault();
			int num = WardrobeKoiCoordinateIntegrity.RequiredCapacity(AccessorySlotCount, valueOrDefault, 150);
			if (num <= AccessorySlotCount)
			{
				return;
			}
			ResolveMoreAccessoriesAddSlot();
			int accessorySlotCount = AccessorySlotCount;
			try
			{
				if (WardrobeContext.InMaker && moreAccessoriesAddSlot != null)
				{
					moreAccessoriesAddSlot.Invoke(null, new object[1] { num - accessorySlotCount });
				}
				else if (WardrobeContext.InStudio && moreAccessoriesArraySync != null)
				{
					TryExpandKoiStudioAccessoryCapacity(num);
				}
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogError((object)("MoreAccessories bulk expansion failed: " + ex));
			}
			EnsureAccessoryMixSlots();
			if (AccessorySlotCount < num)
			{
				WardrobePlugin.Log.LogWarning((object)("Accessory capacity reached " + AccessorySlotCount + " of " + num + " requested slots (source had " + valueOrDefault + ")."));
			}
		}

		public WardrobeSession(ChaControl cha)
		{
			this.cha = cha;
			string[] clothesSlotNames = ClothesSlotNames;
			foreach (string key in clothesSlotNames)
			{
				mixSel[key] = 1;
			}
			mixSel["Clothes"] = 1;
			mixSel["Accessories"] = 1;
			mixSel["Colors"] = 1;
			EnsureAccessoryMixSlots();
			cha.releaseCustomInputTexture = false;
			cha.loadWithDefaultColorAndPtn = false;
			baselineBytes = SaveCoordinateBytes();
			MarkClothingSources(1);
		}

		public byte[] SaveCoordinateBytes()
		{
			return CaptureCoordinateBytes(pruneEmptySlots: false);
		}

		private byte[] CaptureCoordinateBytes(bool pruneEmptySlots)
		{
			if (cha == null)
			{
				return null;
			}
			try
			{
				WardrobeME.WriteCoordExt(cha, cha.nowCoordinate);
				WardrobeOverlay.WriteCoordExt(cha, cha.nowCoordinate);
				WardrobeClothingTransfer.WriteLive(cha);
				WardrobePushup.WriteLive(cha);
				WardrobeClothingSlotsKoi.WriteLive(cha, pruneEmptySlots);
				byte[] array = cha.nowCoordinate.SaveBytes();
				byte[] array2 = WardrobeME.SerializeCoordExt(cha.nowCoordinate);
				byte[] array3 = WardrobeOverlay.SerializeCoordExt(cha.nowCoordinate);
				byte[] array4 = null;
				using MemoryStream memoryStream = new MemoryStream();
				using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
				binaryWriter.Write(1177956420);
				binaryWriter.Write(array.Length);
				binaryWriter.Write(array);
				binaryWriter.Write((array2 != null) ? array2.Length : 0);
				if (array2 != null)
				{
					binaryWriter.Write(array2);
				}
				binaryWriter.Write((array3 != null) ? array3.Length : 0);
				if (array3 != null)
				{
					binaryWriter.Write(array3);
				}
				binaryWriter.Write((array4 != null) ? array4.Length : 0);
				if (array4 != null)
				{
					binaryWriter.Write(array4);
				}
				byte[] array5 = WardrobeClothingTransfer.Read(cha.nowCoordinate);
				binaryWriter.Write((array5 != null) ? array5.Length : 0);
				if (array5 != null)
				{
					binaryWriter.Write(array5);
				}
				byte[] array6 = WardrobePushup.Serialize(cha);
				binaryWriter.Write((array6 != null) ? array6.Length : 0);
				if (array6 != null)
				{
					binaryWriter.Write(array6);
				}
				byte[] array7 = WardrobeClothingSlotsKoi.Read(cha.nowCoordinate).Encode(pruneEmptySlots);
				binaryWriter.Write(array7.Length);
				binaryWriter.Write(array7);
				return memoryStream.ToArray();
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogError((object)("SaveCoordinateBytes failed: " + ex));
				return null;
			}
		}

		public static ChaFileCoordinate LoadCoordFromBytes(byte[] bytes)
		{
			if (bytes == null)
			{
				return null;
			}
			try
			{
				using MemoryStream memoryStream = new MemoryStream(bytes);
				using BinaryReader binaryReader = new BinaryReader(memoryStream);
				if (memoryStream.Length < 4 || binaryReader.ReadInt32() != 1177956420)
				{
					return null;
				}
				int count = binaryReader.ReadInt32();
				byte[] data = binaryReader.ReadBytes(count);
				int num = binaryReader.ReadInt32();
				byte[] meBytes = ((num > 0) ? binaryReader.ReadBytes(num) : null);
				byte[] bytes2 = null;
				if (memoryStream.Position < memoryStream.Length)
				{
					int num2 = binaryReader.ReadInt32();
					bytes2 = ((num2 > 0) ? binaryReader.ReadBytes(num2) : null);
				}
				if (memoryStream.Position < memoryStream.Length)
				{
					int num3 = binaryReader.ReadInt32();
					if (num3 > 0)
					{
						binaryReader.ReadBytes(num3);
					}
				}
				ChaFileCoordinate chaFileCoordinate = new ChaFileCoordinate();
				byte[] payload = null;
				if (memoryStream.Position < memoryStream.Length)
				{
					int num4 = binaryReader.ReadInt32();
					if (num4 < 0 || num4 > memoryStream.Length - memoryStream.Position)
					{
						throw new InvalidDataException("Invalid clothing snapshot length.");
					}
					if (num4 > 0)
					{
						payload = binaryReader.ReadBytes(num4);
					}
				}
				if (!chaFileCoordinate.LoadBytes(data, chaFileCoordinate.loadVersion))
				{
					return null;
				}
				WardrobeClothingTransfer.Attach(chaFileCoordinate, payload);
				if (memoryStream.Position < memoryStream.Length)
				{
					int num5 = binaryReader.ReadInt32();
					if (num5 < 0 || num5 > memoryStream.Length - memoryStream.Position)
					{
						throw new InvalidDataException("Invalid Pushup snapshot length.");
					}
					WardrobePushup.Attach(chaFileCoordinate, (num5 > 0) ? binaryReader.ReadBytes(num5) : null);
				}
				WardrobeME.AttachCoordExt(chaFileCoordinate, meBytes);
				if (memoryStream.Position < memoryStream.Length)
				{
					int num6 = binaryReader.ReadInt32();
					if (num6 < 0 || num6 > memoryStream.Length - memoryStream.Position)
					{
						throw new InvalidDataException("Invalid extra clothing snapshot length.");
					}
					WardrobeClothingSlotsKoi.Attach(chaFileCoordinate, ClothingSlotSet.Decode((num6 > 0) ? binaryReader.ReadBytes(num6) : null));
				}
				WardrobeOverlay.AttachCoordExt(chaFileCoordinate, bytes2);
				return chaFileCoordinate;
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogError((object)("LoadCoordFromBytes failed: " + ex));
				return null;
			}
		}

		public static ChaFileCoordinate PrepareReferenceClothing(ChaFileControl card, int coordinate)
		{
			ChaFileCoordinate chaFileCoordinate = card.coordinate[coordinate];
			WardrobeME.AttachReference(card, chaFileCoordinate, coordinate);
			WardrobeOverlay.AttachReference(card, chaFileCoordinate, coordinate);
			WardrobeClothingTransfer.Attach(chaFileCoordinate, WardrobeClothingTransfer.FromCharacter(card, coordinate));
			WardrobePushup.PrepareReference(card, chaFileCoordinate, coordinate);
			WardrobeClothingSlotsKoi.Attach(chaFileCoordinate, WardrobeClothingSlotsKoi.FromCard(card, coordinate));
			return chaFileCoordinate;
		}

		public static void ApplyReferenceClothingState(ChaControl character, ChaFileControl card, int coordinate)
		{
			ChaFileCoordinate source = PrepareReferenceClothing(card, coordinate);
			WardrobeClothingTransfer.Apply(character, source);
			WardrobePushup.ApplyClothing(character, source);
			WardrobeClothingSlotsKoi.Apply(character, source);
			WardrobePushup.Recalculate(character);
		}

		public static void RestoreCoordinateGeometry(ChaControl character, ChaFileCoordinate coordinate)
		{
			WardrobeClothingTransfer.RestoreGeometry(character, coordinate);
		}

		public bool LoadTargetCard(int which, string path)
		{
			try
			{
				ChaFileCoordinate chaFileCoordinate = new ChaFileCoordinate();
				if (!chaFileCoordinate.LoadFile(path))
				{
					WardrobeGUI.SetMessage("Could not load coordinate card.");
					return false;
				}
				Texture2D texture2D = new Texture2D(2, 2);
				bool flag = false;
				try
				{
					flag = WardrobeCompat.LoadImage(texture2D, File.ReadAllBytes(path));
				}
				catch
				{
				}
				if (which == 2)
				{
					t1 = chaFileCoordinate;
					t1Path = path;
					if (t1Tex != null)
					{
						UnityEngine.Object.Destroy(t1Tex);
					}
					t1Tex = (flag ? texture2D : null);
				}
				else
				{
					t2 = chaFileCoordinate;
					t2Path = path;
					if (t2Tex != null)
					{
						UnityEngine.Object.Destroy(t2Tex);
					}
					t2Tex = (flag ? texture2D : null);
				}
				WardrobePlugin.SaveLastTarget(which, path);
				return true;
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogError((object)("LoadTargetCard failed: " + ex));
				return false;
			}
		}

		public ChaFileCoordinate Source(int sel)
		{
			switch (sel)
			{
			case 2:
				return t1;
			case 3:
				return t2;
			default:
				if (baselineCoord == null)
				{
					baselineCoord = LoadCoordFromBytes(baselineBytes);
				}
				return baselineCoord;
			}
		}

		private static T DeepCopy<T>(T obj)
		{
			if (obj != null)
			{
				return MessagePackSerializer.Deserialize<T>(MessagePackSerializer.Serialize(obj));
			}
			return obj;
		}

		private static ChaFileAccessory.PartsInfo SourceAccessoryPart(ChaFileCoordinate src, int slot)
		{
			if (src == null || src.accessory == null || src.accessory.parts == null || slot < 0 || slot >= src.accessory.parts.Length)
			{
				return null;
			}
			return src.accessory.parts[slot];
		}

		private ChaFileAccessory.PartsInfo LiveAccessoryPart(int slot)
		{
			if (!(cha != null) || cha.nowCoordinate == null || cha.nowCoordinate.accessory == null || cha.nowCoordinate.accessory.parts == null || slot < 0 || slot >= cha.nowCoordinate.accessory.parts.Length)
			{
				return null;
			}
			return cha.nowCoordinate.accessory.parts[slot];
		}

		private bool SetLiveAccessoryPart(int slot, ChaFileAccessory.PartsInfo part)
		{
			if (cha == null || part == null || slot < 0 || slot >= cha.nowCoordinate.accessory.parts.Length)
			{
				return false;
			}
			cha.nowCoordinate.accessory.parts[slot] = part;
			WardrobeCompat.FileCoord(cha).accessory.parts[slot] = DeepCopy(part);
			return true;
		}

		public Texture2D LiveClothesThumb(int kind)
		{
			if (IsManagedClothingSlot(kind, 0))
			{
				return LiveClothesThumb(kind, 0);
			}
			ChaFileClothes.PartsInfo partsInfo = ((cha != null) ? cha.nowCoordinate.clothes.parts[kind] : null);
			if (partsInfo == null || partsInfo.id == 0)
			{
				return null;
			}
			return WardrobeWidgets.ItemThumb((int)ClothesCategories[kind], partsInfo.id);
		}

		public Texture2D SrcClothesThumb(int sel, int kind)
		{
			if (IsManagedClothingSlot(kind, 0, sel))
			{
				return SrcClothesThumb(sel, kind, 0);
			}
			ChaFileCoordinate chaFileCoordinate = Source(sel);
			ChaFileClothes.PartsInfo partsInfo = ((chaFileCoordinate != null) ? chaFileCoordinate.clothes.parts[kind] : null);
			if (partsInfo == null || partsInfo.id == 0)
			{
				return null;
			}
			return WardrobeWidgets.ItemThumb((int)ClothesCategories[kind], partsInfo.id);
		}

		public Texture2D LiveAccThumb(int slot)
		{
			ChaFileAccessory.PartsInfo partsInfo = LiveAccessoryPart(slot);
			if (!AccessoryPopulated(partsInfo))
			{
				return null;
			}
			return WardrobeWidgets.ItemThumb(partsInfo.type, partsInfo.id);
		}

		public Texture2D SrcAccThumb(int sel, int slot)
		{
			ChaFileAccessory.PartsInfo partsInfo = SourceAccessoryPart(Source(sel), slot);
			if (!AccessoryPopulated(partsInfo))
			{
				return null;
			}
			return WardrobeWidgets.ItemThumb(partsInfo.type, partsInfo.id);
		}

		public Color LiveTopColor()
		{
			try
			{
				ChaFileClothes.PartsInfo partsInfo = cha.nowCoordinate.clothes.parts[0];
				if (partsInfo.id != 0)
				{
					Color baseColor = partsInfo.colorInfo[0].baseColor;
					return new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
				}
			}
			catch
			{
			}
			return new Color(0.85f, 0.7f, 0.35f);
		}

		public string SaveOutfitCard(byte[] png, string name)
		{
			if (cha == null)
			{
				return null;
			}
			try
			{
				string text = Path.Combine(WardrobeCompat.GameRoot, "UserData\\coordinate");
				if (!Directory.Exists(text))
				{
					Directory.CreateDirectory(text);
				}
				string text2 = ((!string.IsNullOrEmpty(name)) ? name : "DressUpClo");
				string text3 = Path.Combine(text, text2 + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
				WardrobeME.WriteCoordExt(cha, cha.nowCoordinate);
				WardrobeOverlay.WriteCoordExt(cha, cha.nowCoordinate);
				WardrobeClothingTransfer.WriteLive(cha);
				WardrobePushup.WriteLive(cha);
				WardrobeClothingSlotsKoi.WriteLive(cha, prune: true);
				cha.nowCoordinate.pngData = png;
				cha.nowCoordinate.coordinateName = ((!string.IsNullOrEmpty(name)) ? name : "Dress-Up Outfit");
				WardrobeCompat.SaveCoordFile(cha.nowCoordinate, text3);
				return text3;
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogError((object)("SaveOutfitCard failed: " + ex));
				return null;
			}
		}

		private static bool AccessoryPopulated(ChaFileAccessory.PartsInfo part)
		{
			if (part == null)
			{
				return false;
			}
			return part.id != 0;
		}

		public bool AccPopulated(int sel, int slot)
		{
			return AccessoryPopulated(SourceAccessoryPart(Source(sel), slot));
		}

		internal int SourceAccessoryId(int sel, int slot)
		{
			return SourceAccessoryPart(Source(sel), slot)?.id ?? 0;
		}

		public bool LiveAccPopulated(int slot)
		{
			return AccessoryPopulated(LiveAccessoryPart(slot));
		}

		public List<int> AccUnionSlots()
		{
			int count = EnsureAccessoryMixSlots();
			for (int i = 0; i < count; i++)
			{
				if (LiveAccPopulated(i) || AccPopulated(1, i) || (t1 != null && AccPopulated(2, i)) || (t2 != null && AccPopulated(3, i)))
				{
					visibleAccSlots.Add(i);
				}
			}
			return (from result in visibleAccSlots
				where result >= 0 && result < count
				orderby result
				select result).ToList();
		}

		private void SetClothesStateOn(int kind)
		{
			if (!(cha == null) && kind >= 0 && kind < 9)
			{
				cha.SetClothesState(kind, 0);
			}
		}

		private void SetAllClothesStatesOn()
		{
			for (int i = 0; i < 9; i++)
			{
				SetClothesStateOn(i);
			}
		}

		public void ApplyClothesSlot(int kind, int sel, bool deferME = false)
		{
			ChaFileCoordinate src = Source(sel);
			ApplyClothesItem(src, kind, sel, deferME);
		}

		private void ApplyClothesItem(ChaFileCoordinate src, int kind, int sel, bool deferME)
		{
			if (src == null || cha == null)
			{
				return;
			}
			ChaFileClothes.PartsInfo partsInfo = DeepCopy(src.clothes.parts[kind]);
			cha.nowCoordinate.clothes.parts[kind] = partsInfo;
			WardrobeCompat.FileCoord(cha).clothes.parts[kind] = DeepCopy(partsInfo);
			if (kind == 0)
			{
				for (int i = 0; i < src.clothes.subPartsId.Length; i++)
				{
					cha.nowCoordinate.clothes.subPartsId[i] = src.clothes.subPartsId[i];
					WardrobeCompat.FileCoord(cha).clothes.subPartsId[i] = src.clothes.subPartsId[i];
				}
			}
			WardrobeCompat.ChangeClothes(cha, kind, partsInfo.id, force: true);
			SetClothesStateOn(kind);
			WardrobeME.ReplaceSlot(cha, WardrobeME.CoordExt(src), 1, kind);
			if (!deferME)
			{
				WardrobeME.Apply(cha, clothes: true, accessories: false);
			}
			WardrobeOverlay.CopySlot(cha, WardrobeOverlay.CoordExt(src), kind);
			WardrobeClothingTransfer.Apply(cha, src, kind);
			WardrobePushup.ApplyClothing(cha, src, kind);
			WardrobeClothingSlotsKoi.ApplyPrimaryFrom(cha, src, kind);
			WardrobePseudoMakerState.InvalidateClothing(cha, cha.fileStatus.coordinateType, kind);
			if (!deferME)
			{
				WardrobePushup.Recalculate(cha);
			}
			mixSel[ClothesSlotNames[kind]] = sel;
		}

		public void ApplyAccessorySlot(int slot, int sel, bool deferME = false)
		{
			ChaFileCoordinate src = Source(sel);
			ApplyAccessoryItem(src, slot, sel, deferME);
		}

		private void ApplyAccessoryItem(ChaFileCoordinate src, int slot, int sel, bool deferME, bool? visibility = null)
		{
			ChaFileAccessory.PartsInfo partsInfo = SourceAccessoryPart(src, slot);
			bool flag = AccessoryPopulated(partsInfo);
			ChaFileAccessory.PartsInfo part = (flag ? DeepCopy(partsInfo) : new ChaFileAccessory.PartsInfo());
			if (cha == null || slot < 0 || slot >= 150)
			{
				return;
			}
			if (flag && slot >= AccessorySlotCount)
			{
				EnsureKoiAccessoryCapacity(src.accessory);
			}
			int num = EnsureAccessoryMixSlots();
			if (slot >= num)
			{
				return;
			}
			mixSel["Acc" + slot] = sel;
			visibleAccSlots.Add(slot);
			if (slot >= AccessorySlotCount)
			{
				if (flag)
				{
					WardrobeGUI.SetMessage("Couldn't create accessory slot " + (slot + 1) + ".");
				}
				return;
			}
			QueueKoiAccessory(new KoiAccessoryRequest
			{
				slot = slot,
				part = part,
				slotME = (flag ? WardrobeME.CoordExt(src) : null),
				replaceSlotME = true,
				visibility = visibility,
				diagnostic = "mix source " + sel
			});
		}

		public void ClearClothesSlot(int kind, bool deferME = false)
		{
			if (!(cha == null))
			{
				ChaFileClothes.PartsInfo partsInfo = DeepCopy(cha.nowCoordinate.clothes.parts[kind]);
				partsInfo.id = 0;
				cha.nowCoordinate.clothes.parts[kind] = partsInfo;
				WardrobeCompat.FileCoord(cha).clothes.parts[kind] = DeepCopy(partsInfo);
				WardrobeCompat.ChangeClothes(cha, kind, 0, force: true);
				SetClothesStateOn(kind);
				WardrobeClothingSlotsKoi.ClearPrimary(cha, kind);
				WardrobeME.ReplaceSlot(cha, null, 1, kind);
				if (!deferME)
				{
					WardrobeME.Apply(cha, clothes: true, accessories: false);
				}
				mixSel[ClothesSlotNames[kind]] = 0;
			}
		}

		public void ClearAccessorySlot(int slot, bool deferME = false)
		{
			if (!(cha == null) && slot >= 0 && slot < EnsureAccessoryMixSlots())
			{
				ChaFileAccessory.PartsInfo part = new ChaFileAccessory.PartsInfo();
				visibleAccSlots.Add(slot);
				QueueKoiAccessory(new KoiAccessoryRequest
				{
					slot = slot,
					part = part,
					slotME = null,
					replaceSlotME = true,
					diagnostic = "clear"
				});
				mixSel["Acc" + slot] = 0;
			}
		}

		internal void PreviewCleanupClothes(int kind, int id)
		{
			if (!(cha == null) && kind >= 0 && kind < 9 && id != 0)
			{
				WardrobeClothingSlotsKoi.ClearPrimary(cha, kind);
				ChaFileClothes.PartsInfo partsInfo = DeepCopy(cha.nowCoordinate.clothes.parts[kind]);
				partsInfo.id = id;
				cha.nowCoordinate.clothes.parts[kind] = partsInfo;
				WardrobeCompat.FileCoord(cha).clothes.parts[kind] = DeepCopy(partsInfo);
				WardrobeCompat.ChangeClothes(cha, kind, id, force: true);
				WardrobeClothesTab.RestoreDefaultColors(cha, kind, -1);
				WardrobeME.ReplaceSlot(cha, null, 1, kind);
				WardrobeME.Apply(cha, clothes: true, accessories: false);
				WardrobeOverlay.CopySlot(cha, null, kind);
			}
		}

		internal void PreviewCleanupAccessory(int slot, int type, int id)
		{
			if (!(cha == null) && slot >= 0 && slot < AccessorySlotCount && id != 0)
			{
				ChaFileAccessory.PartsInfo partsInfo = new ChaFileAccessory.PartsInfo();
				partsInfo.type = type;
				partsInfo.id = id;
				partsInfo.parentKey = "";
				QueueKoiAccessory(new KoiAccessoryRequest
				{
					slot = slot,
					part = partsInfo,
					cleanupPreview = true,
					diagnostic = "cleanup preview"
				});
			}
		}

		public bool AccSlotEmpty(int slot)
		{
			ChaFileAccessory.PartsInfo partsInfo = LiveAccessoryPart(slot);
			if (partsInfo != null)
			{
				return !AccessoryPopulated(partsInfo);
			}
			return false;
		}

		public int FirstFreeAccSlot(List<int> shownRows)
		{
			for (int i = 0; i < AccessorySlotCount; i++)
			{
				if (AccSlotEmpty(i) && (shownRows == null || !shownRows.Contains(i)))
				{
					return i;
				}
			}
			return -1;
		}

		public bool CopyAccessoryToSlot(int srcSel, int srcSlot, int destSlot)
		{
			if (cha == null)
			{
				return false;
			}
			ChaFileCoordinate chaFileCoordinate = Source(srcSel);
			ChaFileAccessory.PartsInfo partsInfo = SourceAccessoryPart(chaFileCoordinate, srcSlot);
			if (!AccessoryPopulated(partsInfo))
			{
				return false;
			}
			if (destSlot < 0 || destSlot >= AccessorySlotCount)
			{
				return false;
			}
			if (!AccSlotEmpty(destSlot))
			{
				return false;
			}
			ChaFileAccessory.PartsInfo part = DeepCopy(partsInfo);
			EnsureAccessoryMixSlots();
			visibleAccSlots.Add(destSlot);
			mixSel["Acc" + destSlot] = 0;
			QueueKoiAccessory(new KoiAccessoryRequest
			{
				slot = destSlot,
				part = part,
				slotME = WardrobeME.CoordExt(chaFileCoordinate),
				copyMESourceSlot = srcSlot,
				diagnostic = "move from slot " + srcSlot
			});
			return true;
		}

		public void ApplyAllClothes(int sel)
		{
			ChaFileCoordinate chaFileCoordinate = Source(sel);
			if (chaFileCoordinate != null && !(cha == null))
			{
				byte[] bytes = MessagePackSerializer.Serialize(chaFileCoordinate.clothes);
				cha.nowCoordinate.clothes = MessagePackSerializer.Deserialize<ChaFileClothes>(bytes);
				WardrobeCompat.FileCoord(cha).clothes = MessagePackSerializer.Deserialize<ChaFileClothes>(bytes);
				cha.ChangeClothes(forceChange: true);
				SetAllClothesStatesOn();
				WardrobeME.ReplaceAllOfType(cha, WardrobeME.CoordExt(chaFileCoordinate), 1);
				WardrobeME.Apply(cha, clothes: true, accessories: false);
				WardrobeOverlay.ApplyToLive(cha, chaFileCoordinate);
				WardrobeClothingTransfer.Apply(cha, chaFileCoordinate);
				WardrobePushup.ApplyClothing(cha, chaFileCoordinate);
				WardrobePushup.Recalculate(cha);
				WardrobePseudoMakerState.InvalidateAllClothing(cha, cha.fileStatus.coordinateType);
				WardrobeClothingSlotsKoi.Apply(cha, chaFileCoordinate);
				MarkClothingSources(sel);
				string[] clothesSlotNames = ClothesSlotNames;
				foreach (string key in clothesSlotNames)
				{
					mixSel[key] = sel;
				}
				mixSel["Clothes"] = sel;
				mixSel["Colors"] = sel;
			}
		}

		public void ApplyAllAccessories(int sel)
		{
			ChaFileCoordinate chaFileCoordinate = Source(sel);
			if (chaFileCoordinate != null && !(cha == null))
			{
				int num = EnsureAccessoryMixSlots();
				for (int i = 0; i < num; i++)
				{
					mixSel["Acc" + i] = sel;
				}
				mixSel["Accessories"] = sel;
				QueueAllKoiAccessories(chaFileCoordinate.accessory, WardrobeME.CoordExt(chaFileCoordinate), "all accessories source " + sel);
			}
		}

		private void QueueKoiAccessory(KoiAccessoryRequest request)
		{
			if (request != null && request.part != null && request.slot >= 0 && request.slot < AccessorySlotCount)
			{
				request.part = DeepCopy(request.part);
				koiAccessoryQueue.Enqueue(request);
				if (!koiAccessoryQueueRunning)
				{
					cha.StartCoroutine(KoiAccessoryQueueCoro());
				}
			}
		}

		private void QueueAllKoiAccessories(ChaFileAccessory target, PluginData allME, string diagnostic, bool applyClothesME = false)
		{
			if (target == null)
			{
				return;
			}
			EnsureKoiAccessoryCapacity(target);
			int accessorySlotCount = AccessorySlotCount;
			if (accessorySlotCount > 0)
			{
				koiBulkGeneration++;
				int bulkGeneration = koiBulkGeneration;
				koiAccessoryQueue.Clear();
				ChaFileAccessory.PartsInfo[] array = new ChaFileAccessory.PartsInfo[accessorySlotCount];
				for (int i = 0; i < accessorySlotCount; i++)
				{
					ChaFileAccessory.PartsInfo partsInfo = ((target.parts != null && i < target.parts.Length) ? DeepCopy(target.parts[i]) : new ChaFileAccessory.PartsInfo());
					array[i] = DeepCopy(partsInfo);
					koiAccessoryQueue.Enqueue(new KoiAccessoryRequest
					{
						slot = i,
						part = partsInfo,
						allME = ((i == accessorySlotCount - 1) ? allME : null),
						replaceAllME = (i == accessorySlotCount - 1),
						bulkGeneration = bulkGeneration,
						batchTarget = ((i == accessorySlotCount - 1) ? array : null),
						restoreVisibilityStates = ((i == accessorySlotCount - 1) ? koiRestoreAccessoryStates : null),
						restoreCompatibilityState = ((i == accessorySlotCount - 1) ? koiRestoreCompatibilityState : null),
						applyClothesME = (i == accessorySlotCount - 1 && applyClothesME),
						diagnostic = diagnostic
					});
				}
				WardrobeGUI.SetMessage("Applying accessories...");
				if (!koiAccessoryQueueRunning)
				{
					cha.StartCoroutine(KoiAccessoryQueueCoro());
				}
			}
		}

		internal void QueueEditorAccessory(int slot, int type, int id, string parentKey, bool preserveParent)
		{
			if (!(cha == null) && slot >= 0 && slot < AccessorySlotCount)
			{
				ChaFileAccessory.PartsInfo partsInfo = DeepCopy(cha.nowCoordinate.accessory.parts[slot]);
				partsInfo.type = type;
				partsInfo.id = id;
				partsInfo.parentKey = (preserveParent ? (parentKey ?? "") : "");
				QueueKoiAccessory(new KoiAccessoryRequest
				{
					slot = slot,
					part = partsInfo,
					markCustom = true,
					diagnostic = "accessory editor"
				});
			}
		}

		private void RestoreKoiCleanupAccessoryDefaults(int slot)
		{
			ChaFileAccessory.PartsInfo partsInfo = cha.nowCoordinate.accessory.parts[slot];
			ChaFileAccessory.PartsInfo partsInfo2 = WardrobeCompat.FileCoord(cha).accessory.parts[slot];
			int num = ((partsInfo.color != null) ? Math.Min(partsInfo.color.Length, 4) : 0);
			for (int i = 0; i < num; i++)
			{
				Color color = Color.white;
				if (cha.GetAccessoryDefaultColor(ref color, slot, i))
				{
					partsInfo.color[i] = color;
					if (partsInfo2.color != null && i < partsInfo2.color.Length)
					{
						partsInfo2.color[i] = color;
					}
				}
			}
			cha.ChangeAccessoryColor(slot);
		}

		private IEnumerator KoiAccessoryQueueCoro()
		{
			if (koiAccessoryQueueRunning)
			{
				yield break;
			}
			koiAccessoryQueueRunning = true;
			try
			{
				while (koiAccessoryQueue.Count > 0)
				{
					KoiAccessoryRequest request = koiAccessoryQueue.Dequeue();
					if (KoiBulkRequestIsStale(request))
					{
						continue;
					}
					int slot = request.slot;
					yield return LoadKoiAccessoryTarget(slot, request.part);
					if (KoiBulkRequestIsStale(request))
					{
						continue;
					}
					bool flag = false;
					if (request.copyMESourceSlot >= 0)
					{
						ReapplyKoiAccessoryTransform(slot, request.part);
					}
					try
					{
						if (request.cleanupPreview)
						{
							RestoreKoiCleanupAccessoryDefaults(slot);
							WardrobeME.ReplaceSlot(cha, null, 2, slot);
							flag = true;
						}
						else if (request.copyMESourceSlot >= 0)
						{
							WardrobeME.CopyAccessorySlot(cha, request.slotME, request.copyMESourceSlot, slot);
							flag = true;
						}
						else if (request.replaceSlotME)
						{
							WardrobeME.ReplaceSlot(cha, request.slotME, 2, slot);
							flag = true;
						}
					}
					catch (Exception ex)
					{
						WardrobePlugin.Log.LogWarning((object)("Accessory ME carry failed (slot " + slot + "): " + ex));
					}
					if (flag)
					{
						yield return WardrobeME.ApplyAndWait(cha, clothes: false, accessories: true);
					}
					if (KoiBulkRequestIsStale(request))
					{
						continue;
					}
					if (request.markCustom)
					{
						MarkAccCustom(slot);
					}
					if (request.visibility.HasValue)
					{
						SetAccessoryVisible(slot, request.visibility.Value);
					}
					if (request.replaceAllME)
					{
						yield return StabilizeKoiAccessoryBatch(request);
						if (KoiBulkRequestIsStale(request))
						{
							continue;
						}
						ReapplyKoiAccessoryTransforms(request.batchTarget);
						bool flag2 = false;
						try
						{
							WardrobeME.ReplaceAllOfType(cha, request.allME, 2);
							flag2 = true;
						}
						catch (Exception ex2)
						{
							WardrobePlugin.Log.LogWarning((object)("Accessory ME batch carry failed: " + ex2));
						}
						if (flag2)
						{
							yield return WardrobeME.ApplyAndWait(cha, request.applyClothesME, accessories: true);
						}
						if (KoiBulkRequestIsStale(request))
						{
							continue;
						}
						RefreshKoiAccessoryUi();
						WardrobeGUI.SetMessage("Outfit accessories applied.");
					}
					else if ((request.bulkGeneration == 0 || request.invalidateOnBulkChange) && koiAccessoryQueue.Count == 0)
					{
						RefreshKoiAccessoryUi();
					}
					if (request.restoreVisibilityStates != null)
					{
						for (int i = 0; i < Math.Min(AccessorySlotCount, request.restoreVisibilityStates.Length); i++)
						{
							SetAccessoryVisible(i, request.restoreVisibilityStates[i]);
						}
					}
					if (!request.cleanupPreview)
					{
						if (request.replaceAllME)
						{
							WardrobePseudoMakerState.InvalidateAllAccessories(cha, cha.fileStatus.coordinateType);
						}
						else if (request.bulkGeneration == 0)
						{
							WardrobePseudoMakerState.InvalidateAccessory(cha, cha.fileStatus.coordinateType, slot);
						}
					}
					if (request.restoreCompatibilityState != null)
					{
						WardrobePseudoMakerState.Restore(cha, request.restoreCompatibilityState);
					}
					if (koiAccessoryQueue.Count == 0)
					{
						WardrobePushup.Recalculate(cha);
					}
					if (slot == 0 || koiAccessoryQueue.Count == 0)
					{
						yield return null;
						if (PruneKoiOrphanAccessories(request.diagnostic) > 0)
						{
							yield return null;
						}
					}
				}
			}
			finally
			{
				koiAccessoryQueueRunning = false;
			}
		}

		private void RefreshKoiAccessoryUi()
		{
			ResolveMoreAccessoriesAddSlot();
			try
			{
				if (moreAccessoriesUpdateUI != null)
				{
					moreAccessoriesUpdateUI.Invoke(null, null);
				}
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("MoreAccessories UI refresh failed: " + ex.Message));
			}
			if (!WardrobeContext.InStudio)
			{
				return;
			}
			try
			{
				global::Studio.Studio instance = Singleton<global::Studio.Studio>.Instance;
				if (instance == null || instance.manipulatePanelCtrl == null)
				{
					return;
				}
				BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
				object manipulatePanelCtrl = instance.manipulatePanelCtrl;
				FieldInfo field = manipulatePanelCtrl.GetType().GetField("charaPanelInfo", bindingAttr);
				PropertyInfo property = manipulatePanelCtrl.GetType().GetProperty("charaPanelInfo", bindingAttr);
				object obj = ((field != null) ? field.GetValue(manipulatePanelCtrl) : ((property != null) ? property.GetValue(manipulatePanelCtrl, null) : null));
				if (obj == null)
				{
					return;
				}
				FieldInfo field2 = obj.GetType().GetField("m_MPCharCtrl", bindingAttr);
				PropertyInfo property2 = obj.GetType().GetProperty("mpCharCtrl", bindingAttr);
				object obj2 = ((property2 != null) ? property2.GetValue(obj, null) : ((field2 != null) ? field2.GetValue(obj) : null));
				if (obj2 != null)
				{
					PropertyInfo property3 = obj2.GetType().GetProperty("ociChar", bindingAttr);
					if (!(property3 == null) && property3.CanRead && property3.CanWrite && property3.GetValue(obj2, null) is OCIChar oCIChar && oCIChar.charInfo == cha)
					{
						property3.SetValue(obj2, oCIChar, null);
					}
				}
			}
			catch (Exception ex2)
			{
				WardrobePlugin.Log.LogWarning((object)("Studio accessory UI refresh failed: " + ex2.Message));
			}
		}

		private bool KoiBulkRequestIsStale(KoiAccessoryRequest request)
		{
			if (request != null && (request.bulkGeneration != 0 || request.invalidateOnBulkChange))
			{
				return request.bulkGeneration != koiBulkGeneration;
			}
			return false;
		}

		private IEnumerator LoadKoiAccessoryTarget(int slot, ChaFileAccessory.PartsInfo source)
		{
			if (source != null && slot >= 0 && slot < AccessorySlotCount)
			{
				ChaFileAccessory.PartsInfo partsInfo = DeepCopy(source);
				cha.nowCoordinate.accessory.parts[slot] = partsInfo;
				WardrobeCompat.FileCoord(cha).accessory.parts[slot] = DeepCopy(partsInfo);
				yield return cha.StartCoroutine(cha.ChangeAccessoryAsync(slot, partsInfo.type, partsInfo.id, partsInfo.parentKey ?? "", forceChange: true));
				WardrobeCompat.FileCoord(cha).accessory.parts[slot] = DeepCopy(cha.nowCoordinate.accessory.parts[slot]);
			}
		}

		private List<int> FindKoiAccessoryMismatches(ChaFileAccessory.PartsInfo[] batchTarget)
		{
			List<int> list = new List<int>();
			if (batchTarget == null)
			{
				return list;
			}
			int num = Math.Min(batchTarget.Length, AccessorySlotCount);
			for (int i = 0; i < num; i++)
			{
				ChaFileAccessory.PartsInfo partsInfo = batchTarget[i] ?? new ChaFileAccessory.PartsInfo();
				bool hasLoadedObject = cha.objAccessory != null && i < cha.objAccessory.Length && cha.objAccessory[i] != null;
				int loadedCategory = -1;
				int loadedId = -1;
				if (cha.infoAccessory != null && i < cha.infoAccessory.Length && cha.infoAccessory[i] != null)
				{
					loadedCategory = cha.infoAccessory[i].Category;
					loadedId = cha.infoAccessory[i].Id;
				}
				if (!WardrobeKoiCoordinateIntegrity.AccessoryMatches(partsInfo.type, partsInfo.id, hasLoadedObject, loadedCategory, loadedId))
				{
					list.Add(i);
				}
			}
			return list;
		}

		private void ReapplyKoiAccessoryTransforms(ChaFileAccessory.PartsInfo[] batchTarget)
		{
			if (batchTarget != null)
			{
				int num = Math.Min(batchTarget.Length, AccessorySlotCount);
				for (int i = 0; i < num; i++)
				{
					ReapplyKoiAccessoryTransform(i, batchTarget[i]);
				}
			}
		}

		private void ReapplyKoiAccessoryTransform(int slot, ChaFileAccessory.PartsInfo source)
		{
			ChaFileAccessory.PartsInfo partsInfo = DeepCopy(source ?? new ChaFileAccessory.PartsInfo());
			cha.nowCoordinate.accessory.parts[slot] = partsInfo;
			if (partsInfo.type != 120 && partsInfo.id > 0 && cha.objAccessory != null && slot < cha.objAccessory.Length && cha.objAccessory[slot] != null)
			{
				cha.ChangeAccessoryColor(slot);
				string text = partsInfo.parentKey ?? "";
				if (text.Length == 0 && cha.infoAccessory != null && slot < cha.infoAccessory.Length && cha.infoAccessory[slot] != null)
				{
					text = cha.infoAccessory[slot].GetInfo(ChaListDefine.KeyType.Parent);
				}
				cha.ChangeAccessoryParent(slot, text);
				cha.UpdateAccessoryMoveFromInfo(slot);
				partsInfo.partsOfHead = ChaAccessoryDefine.CheckPartsOfHead(text);
				cha.ChangeShakeAccessory(slot);
			}
			WardrobeCompat.FileCoord(cha).accessory.parts[slot] = DeepCopy(partsInfo);
		}

		private IEnumerator StabilizeKoiAccessoryBatch(KoiAccessoryRequest request)
		{
			for (int pass = 1; pass <= 3; pass++)
			{
				if (KoiBulkRequestIsStale(request))
				{
					yield break;
				}
				yield return null;
				List<int> mismatches = FindKoiAccessoryMismatches(request.batchTarget);
				if (mismatches.Count == 0)
				{
					yield return null;
					mismatches = FindKoiAccessoryMismatches(request.batchTarget);
					if (mismatches.Count == 0)
					{
						yield break;
					}
				}
				WardrobePlugin.Log.LogWarning((object)("[AccIntegrity] retry pass " + pass + " via=" + request.diagnostic + " slots=" + WardrobeKoiCoordinateIntegrity.JoinSlotNumbers(mismatches)));
				for (int i = 0; i < mismatches.Count; i++)
				{
					if (KoiBulkRequestIsStale(request))
					{
						yield break;
					}
					int num = mismatches[i];
					yield return LoadKoiAccessoryTarget(num, request.batchTarget[num]);
				}
				yield return null;
				PruneKoiOrphanAccessories(request.diagnostic + " stabilization " + pass);
			}
			List<int> list = FindKoiAccessoryMismatches(request.batchTarget);
			if (list.Count > 0)
			{
				WardrobePlugin.Log.LogError((object)("[AccIntegrity] accessory stabilization exhausted via=" + request.diagnostic + " slots=" + WardrobeKoiCoordinateIntegrity.JoinSlotNumbers(list)));
			}
		}

		private int PruneKoiOrphanAccessories(string diagnostic)
		{
			if (cha == null || cha.objAccessory == null)
			{
				return 0;
			}
			Transform root = ((cha.objTop != null) ? cha.objTop.transform : cha.transform);
			List<int> list = new List<int>();
			for (int i = 0; i < cha.objAccessory.Length; i++)
			{
				if (cha.objAccessory[i] != null)
				{
					list.Add(cha.objAccessory[i].GetInstanceID());
				}
			}
			List<int> list2 = new List<int>();
			Dictionary<int, GameObject> dictionary = new Dictionary<int, GameObject>();
			UnityEngine.Object[] array = Resources.FindObjectsOfTypeAll(typeof(ChaAccessoryComponent));
			for (int j = 0; j < array.Length; j++)
			{
				ChaAccessoryComponent chaAccessoryComponent = array[j] as ChaAccessoryComponent;
				if (!(chaAccessoryComponent == null) && !(chaAccessoryComponent.gameObject == null))
				{
					GameObject gameObject = chaAccessoryComponent.gameObject;
					if (gameObject.name.StartsWith("ca_slot", StringComparison.OrdinalIgnoreCase) && KoiIsUnder(gameObject.transform, root))
					{
						int instanceID = gameObject.GetInstanceID();
						list2.Add(instanceID);
						dictionary[instanceID] = gameObject;
					}
				}
			}
			List<int> list3 = WardrobeKoiAccessoryOwnership.FindOrphanIds(list, list2);
			for (int k = 0; k < list3.Count; k++)
			{
				if (dictionary.TryGetValue(list3[k], out var value) && !(value == null))
				{
					WardrobePlugin.Log.LogWarning((object)("[AccOwnership] pruning orphan go=" + list3[k] + " via=" + diagnostic + " path=" + KoiTransformPath(value.transform)));
					UnityEngine.Object.Destroy(value);
				}
			}
			return list3.Count;
		}

		private static bool KoiIsUnder(Transform child, Transform root)
		{
			if (child == null || root == null)
			{
				return false;
			}
			Transform transform = child;
			while (transform != null)
			{
				if (transform == root)
				{
					return true;
				}
				transform = transform.parent;
			}
			return false;
		}

		private static string KoiTransformPath(Transform leaf)
		{
			List<string> list = new List<string>();
			Transform transform = leaf;
			while (transform != null && list.Count < 24)
			{
				list.Add(transform.name);
				transform = transform.parent;
			}
			list.Reverse();
			return string.Join("/", list.ToArray());
		}

		public void ApplyColors(int sel)
		{
			ChaFileCoordinate chaFileCoordinate = Source(sel);
			if (chaFileCoordinate != null && !(cha == null))
			{
				for (int i = 0; i < 9; i++)
				{
					ChaFileClothes.PartsInfo.ColorInfo[] array = DeepCopy(chaFileCoordinate.clothes.parts[i].colorInfo);
					cha.nowCoordinate.clothes.parts[i].colorInfo = array;
					WardrobeCompat.FileCoord(cha).clothes.parts[i].colorInfo = DeepCopy(array);
					WardrobeCompat.ChangeClothes(cha, i, cha.nowCoordinate.clothes.parts[i].id, force: true);
					WardrobeClothingSlotsKoi.ReapplyPrimary(cha, i);
				}
				ApplyExtraClothingColors(chaFileCoordinate);
				WardrobeHomeColor4Koi.ApplySource(cha, chaFileCoordinate);
				mixSel["Colors"] = sel;
				WardrobePseudoMakerState.InvalidateClothingColors(cha, cha.fileStatus.coordinateType);
			}
		}

		public void RandomizeClothingItem(int kind)
		{
			if (cha == null)
			{
				return;
			}
			if (kind == 0)
			{
				List<WardrobeItem> list = (from item in WardrobeKoiTopCatalog.List(kind)
					where item.id != 0 && !WardrobePrefs.IsDisliked((int)ClothesCategories[kind], item.name)
					select item).ToList();
				if (list.Count != 0)
				{
					SetClothingSlotItem(kind, 0, list[UnityEngine.Random.Range(0, list.Count)].id);
					SetClothingSlotState(kind, 0, 0);
				}
				return;
			}
			List<int> list2 = (from it in WardrobeCompat.ListCtrl.GetCategoryInfo(ClothesCategories[kind])?.Values
				where it.Id != 0 && !WardrobePrefs.IsDisliked((int)ClothesCategories[kind], it.Name)
				select it.Id).ToList();
			if (list2 != null && list2.Count != 0)
			{
				int id = list2[UnityEngine.Random.Range(0, list2.Count)];
				cha.nowCoordinate.clothes.parts[kind].id = id;
				WardrobeCompat.FileCoord(cha).clothes.parts[kind].id = id;
				WardrobeCompat.ChangeClothes(cha, kind, id, force: true);
				SetClothesStateOn(kind);
				WardrobeClothingSlotsKoi.ClearPrimary(cha, kind);
				mixSel[ClothesSlotNames[kind]] = 0;
				WardrobePseudoMakerState.InvalidateClothing(cha, cha.fileStatus.coordinateType, kind);
			}
		}

		public void RandomMix(bool keepColors)
		{
			if (cha == null)
			{
				return;
			}
			int num = ((t2 != null) ? 3 : ((t1 == null) ? 1 : 2));
			if (num == 1)
			{
				WardrobeGUI.SetMessage("Load a target card first.");
				return;
			}
			byte[] bytes = null;
			Dictionary<string, WardrobeMaterialColor4Koi.Value> values = (keepColors ? WardrobeHomeColor4Koi.Capture(cha) : null);
			Dictionary<string, byte[]> colors = (keepColors ? CaptureExtraClothingColors() : null);
			if (keepColors)
			{
				ChaFileClothes.PartsInfo.ColorInfo[][] array = new ChaFileClothes.PartsInfo.ColorInfo[9][];
				for (int i = 0; i < 9; i++)
				{
					array[i] = cha.nowCoordinate.clothes.parts[i].colorInfo;
				}
				bytes = MessagePackSerializer.Serialize(array);
			}
			for (int j = 0; j < 9; j++)
			{
				ApplyClothesSlot(j, UnityEngine.Random.Range(1, num + 1), deferME: true);
			}
			RandomMixExtraClothing(num);
			mixSel["Colors"] = 0;
			if (keepColors)
			{
				ChaFileClothes.PartsInfo.ColorInfo[][] array2 = MessagePackSerializer.Deserialize<ChaFileClothes.PartsInfo.ColorInfo[][]>(bytes);
				for (int k = 0; k < 9; k++)
				{
					cha.nowCoordinate.clothes.parts[k].colorInfo = array2[k];
					WardrobeCompat.FileCoord(cha).clothes.parts[k].colorInfo = DeepCopy(array2[k]);
					WardrobeCompat.ChangeClothes(cha, k, cha.nowCoordinate.clothes.parts[k].id, force: true);
					WardrobeClothingSlotsKoi.ReapplyPrimary(cha, k);
				}
				mixSel["Colors"] = 0;
				RestoreExtraClothingColors(colors);
				WardrobeHomeColor4Koi.Restore(cha, values);
			}
			WardrobeME.Apply(cha, clothes: true, accessories: false);
			mixSel["Clothes"] = 0;
			WardrobePushup.Recalculate(cha);
		}

		public void ResetToBaseline()
		{
			ChaFileCoordinate chaFileCoordinate = Source(1);
			if (chaFileCoordinate == null)
			{
				WardrobeGUI.SetMessage("No baseline stash.");
				return;
			}
			ApplyFullCoordinate(chaFileCoordinate);
			foreach (string item in mixSel.Keys.ToList())
			{
				mixSel[item] = 1;
			}
			MarkClothingSources(1);
		}

		public void ApplyWholeCard(int sel)
		{
			ChaFileCoordinate chaFileCoordinate = Source(sel);
			if (chaFileCoordinate == null)
			{
				WardrobeGUI.SetMessage("No card loaded in that slot.");
				return;
			}
			ApplyFullCoordinate(chaFileCoordinate);
			foreach (string item in mixSel.Keys.ToList())
			{
				mixSel[item] = sel;
			}
			MarkClothingSources(sel);
		}

		private void ApplyFullCoordinate(ChaFileCoordinate src)
		{
			if (src != null && !(cha == null))
			{
				WardrobePseudoMakerGuards.ClearReservation(cha);
				byte[] bytes = MessagePackSerializer.Serialize(src.clothes);
				cha.nowCoordinate.clothes = MessagePackSerializer.Deserialize<ChaFileClothes>(bytes);
				WardrobeCompat.FileCoord(cha).clothes = MessagePackSerializer.Deserialize<ChaFileClothes>(bytes);
				cha.ChangeClothes(forceChange: true);
				WardrobeClothingTransfer.Apply(cha, src);
				WardrobePushup.ApplyClothing(cha, src);
				WardrobePseudoMakerState.InvalidateOutfit(cha, cha.fileStatus.coordinateType);
				ChaFileAccessory target = MessagePackSerializer.Deserialize<ChaFileAccessory>(MessagePackSerializer.Serialize(src.accessory));
				PluginData val = WardrobeME.CoordExt(src);
				WardrobeME.ReplaceAllOfType(cha, val, 1);
				QueueAllKoiAccessories(target, val, "full coordinate", applyClothesME: true);
				WardrobeOverlay.ApplyToLive(cha, src);
				WardrobeClothingSlotsKoi.Apply(cha, src);
			}
		}

		private bool AccessoryVisible(int slot)
		{
			return cha.fileStatus.showAccessory[slot];
		}

		private void SetAccessoryVisible(int slot, bool visible)
		{
			cha.fileStatus.showAccessory[slot] = visible;
			cha.SetAccessoryState(slot, visible);
		}

		private bool[] CaptureAccessoryStates()
		{
			bool[] array = new bool[AccessorySlotCount];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = AccessoryVisible(i);
			}
			return array;
		}

		public bool CanPasteAccessoryItem(int slot)
		{
			if (cha != null && slot >= 0 && slot < AccessorySlotCount && accessoryItemClipboard != null)
			{
				return accessoryItemPart != null;
			}
			return false;
		}

		public void CopyAccessoryItem(int slot)
		{
			ChaFileAccessory.PartsInfo partsInfo = LiveAccessoryPart(slot);
			if (!AccessoryPopulated(partsInfo))
			{
				WardrobeGUI.SetMessage("Select an accessory to copy.");
				return;
			}
			byte[] array = SaveCoordinateBytes();
			if (array == null)
			{
				WardrobeGUI.SetMessage("Couldn't copy the accessory.");
				return;
			}
			accessoryItemPart = DeepCopy(partsInfo);
			accessoryItemClipboard = array;
			accessoryItemSourceSlot = slot;
			accessoryItemVisible = AccessoryVisible(slot);
			WardrobeGUI.SetMessage("Accessory copied — paste into any accessory slot or character.");
		}

		public void PasteAccessoryItem(int slot)
		{
			if (!CanPasteAccessoryItem(slot))
			{
				return;
			}
			ChaFileCoordinate chaFileCoordinate = LoadCoordFromBytes(accessoryItemClipboard);
			if (chaFileCoordinate == null)
			{
				WardrobeGUI.SetMessage("Couldn't paste the accessory.");
				return;
			}
			byte[] array = SaveCoordinateBytes();
			if (array == null)
			{
				WardrobeGUI.SetMessage("Couldn't save undo before pasting.");
				return;
			}
			PushUndoBytes(array);
			ChaFileAccessory.PartsInfo part = DeepCopy(accessoryItemPart);
			visibleAccSlots.Add(slot);
			QueueKoiAccessory(new KoiAccessoryRequest
			{
				slot = slot,
				part = part,
				slotME = WardrobeME.CoordExt(chaFileCoordinate),
				copyMESourceSlot = accessoryItemSourceSlot,
				visibility = accessoryItemVisible,
				bulkGeneration = koiBulkGeneration,
				invalidateOnBulkChange = true,
				markCustom = true,
				diagnostic = "accessory clipboard"
			});
			MarkAccCustom(slot);
			WardrobeGUI.SetMessage("Accessory pasted into slot " + (slot + 1) + ".");
		}

		public bool CanPasteClothesItem(int kind)
		{
			if (cha != null && clothesItemClipboard != null)
			{
				return clothesItemKind == kind;
			}
			return false;
		}

		public void CopyClothesItem(int kind)
		{
			CopyClothesItem(kind, 0);
		}

		public void PasteClothesItem(int kind)
		{
			PasteClothesItem(kind, 0);
		}

		public void CopyOutfit()
		{
			byte[] array = CaptureCoordinateBytes(pruneEmptySlots: true);
			if (array == null)
			{
				WardrobeGUI.SetMessage("Couldn't copy the outfit.");
				return;
			}
			OutfitClipboard = array;
			WardrobeGUI.SetMessage("Outfit copied — select another character and Paste Outfit.");
		}

		public void PasteOutfit()
		{
			if (OutfitClipboard == null)
			{
				WardrobeGUI.SetMessage("Copy an outfit first.");
				return;
			}
			ChaFileCoordinate chaFileCoordinate = LoadCoordFromBytes(OutfitClipboard);
			if (chaFileCoordinate == null)
			{
				WardrobeGUI.SetMessage("Couldn't paste the outfit.");
				return;
			}
			ApplyFullCoordinate(chaFileCoordinate);
			foreach (string item in mixSel.Keys.ToList())
			{
				mixSel[item] = 0;
			}
			MarkClothingSources(0);
			WardrobeGUI.SetMessage("Outfit pasted.");
		}

		public void PushUndo()
		{
			lastCoalesceKey = null;
			byte[] array = SaveCoordinateBytes();
			if (array != null)
			{
				redoStack.Clear();
				undoStack.Add(CaptureUndoEntry(array));
				while (undoStack.Count > 10)
				{
					undoStack.RemoveAt(0);
				}
			}
		}

		public void PushUndoBytes(byte[] preBytes)
		{
			if (preBytes != null)
			{
				lastCoalesceKey = null;
				redoStack.Clear();
				undoStack.Add(CaptureUndoEntry(preBytes));
				while (undoStack.Count > 10)
				{
					undoStack.RemoveAt(0);
				}
			}
		}

		public void PushUndoCoalesced(string key)
		{
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (key == lastCoalesceKey && realtimeSinceStartup - lastCoalesceTime < 1.5f)
			{
				lastCoalesceTime = realtimeSinceStartup;
				return;
			}
			PushUndo();
			lastCoalesceKey = key;
			lastCoalesceTime = realtimeSinceStartup;
		}

		public void MarkClothesCustom(int kind, bool colors = false, bool itemToo = true)
		{
			if (itemToo)
			{
				mixSel[ClothesSlotNames[kind]] = 0;
				mixSel["Clothes"] = 0;
			}
			if (colors)
			{
				mixSel["Colors"] = 0;
			}
		}

		public void MarkAccCustom(int slot)
		{
			mixSel["Acc" + slot] = 0;
			mixSel["Accessories"] = 0;
		}

		public void UndoStep()
		{
			if (!CanUndo)
			{
				WardrobeGUI.SetMessage("Nothing to undo.");
				return;
			}
			UndoEntry entry = undoStack[undoStack.Count - 1];
			undoStack.RemoveAt(undoStack.Count - 1);
			redoStack.Add(CaptureUndoEntry(SaveCoordinateBytes()));
			RestoreEntry(entry);
			WardrobeGUI.SetMessage($"Undo ({undoStack.Count} left)");
		}

		public void RedoStep()
		{
			if (!CanRedo)
			{
				WardrobeGUI.SetMessage("Nothing to redo.");
				return;
			}
			UndoEntry entry = redoStack[redoStack.Count - 1];
			redoStack.RemoveAt(redoStack.Count - 1);
			undoStack.Add(CaptureUndoEntry(SaveCoordinateBytes()));
			while (undoStack.Count > 10)
			{
				undoStack.RemoveAt(0);
			}
			RestoreEntry(entry);
			WardrobeGUI.SetMessage($"Redo ({redoStack.Count} left)");
		}

		private void RestoreEntry(UndoEntry entry)
		{
			ChaFileCoordinate chaFileCoordinate = LoadCoordFromBytes(entry.coordBytes);
			if (chaFileCoordinate == null)
			{
				return;
			}
			koiRestoreAccessoryStates = entry.accessoryStates;
			koiRestoreCompatibilityState = entry.compatibilityState;
			try
			{
				ApplyFullCoordinate(chaFileCoordinate);
			}
			finally
			{
				koiRestoreAccessoryStates = null;
				koiRestoreCompatibilityState = null;
			}
			WardrobePseudoMakerGuards.RestoreReservation(cha, entry.reservedSlots);
			WardrobePseudoMakerState.Restore(cha, entry.compatibilityState);
			if (entry.clothesStates != null)
			{
				for (int i = 0; i < Math.Min(9, entry.clothesStates.Length); i++)
				{
					cha.SetClothesState(i, entry.clothesStates[i]);
				}
			}
			if (entry.accessoryStates != null)
			{
				for (int j = 0; j < Math.Min(AccessorySlotCount, entry.accessoryStates.Length); j++)
				{
					SetAccessoryVisible(j, entry.accessoryStates[j]);
				}
			}
			foreach (KeyValuePair<string, int> item in entry.sel)
			{
				mixSel[item.Key] = item.Value;
			}
		}

		private UndoEntry CaptureUndoEntry(byte[] bytes)
		{
			return new UndoEntry
			{
				coordBytes = bytes,
				clothesStates = (byte[])cha.fileStatus.clothesState.Clone(),
				accessoryStates = CaptureAccessoryStates(),
				sel = new Dictionary<string, int>(mixSel),
				compatibilityState = WardrobePseudoMakerState.Capture(cha),
				reservedSlots = WardrobePseudoMakerGuards.CaptureReservation(cha)
			};
		}

		public void SetBaseline()
		{
			byte[] array = SaveCoordinateBytes();
			if (array == null)
			{
				return;
			}
			baselineBytes = array;
			MarkClothingSources(1);
			baselineCoord = null;
			foreach (string item in mixSel.Keys.ToList())
			{
				mixSel[item] = 1;
			}
			WardrobeGUI.SetMessage("Baseline cemented — [1] and Reset now return here.");
		}

		public static void ImportReferenceOutfit(ChaControl character, ChaFileControl card, int index, bool accessoriesOnly)
		{
			WardrobeSession selectedSession = WardrobePlugin.GetSelectedSession();
			if (selectedSession == null || (object)selectedSession.cha != character)
			{
				throw new InvalidOperationException("The outfit engine has a different character selected.");
			}
			ChaFileCoordinate chaFileCoordinate = PrepareReferenceClothing(card, index);
			ChaFileCoordinate chaFileCoordinate2 = selectedSession.t1;
			try
			{
				selectedSession.t1 = chaFileCoordinate;
				if (accessoriesOnly)
				{
					selectedSession.ApplyAllAccessories(2);
				}
				else
				{
					selectedSession.ApplyAllClothes(2);
				}
			}
			finally
			{
				selectedSession.t1 = chaFileCoordinate2;
			}
		}

		private static string SlotMixKey(int kind, int index)
		{
			return "Cloth" + kind + "#" + index;
		}

		public int GetClothingSlotCount(int kind, int source = 0)
		{
			if (kind < 0 || kind >= 9)
			{
				return 1;
			}
			return 1 + ((source != 0) ? WardrobeClothingSlotsKoi.Read(Source(source)).Count(kind) : (ClothingSlots?.Count(kind) ?? 0));
		}

		public ChaFileClothes.PartsInfo GetClothingSlotPart(int kind, int index, int source = 0)
		{
			if (kind < 0 || kind >= 9 || index < 0 || index > 4)
			{
				return null;
			}
			if (index == 0 && IsManagedClothingSlot(kind, index, source))
			{
				ClothingSlotRecord clothingSlotRecord = ClothingRecord(kind, index, source);
				if (source == 0)
				{
					return ClothingSlots?.Part(kind, index);
				}
				if (clothingSlotRecord.Part == null)
				{
					return null;
				}
				ChaFileClothes.PartsInfo partsInfo = MessagePackSerializer.Deserialize<ChaFileClothes.PartsInfo>(clothingSlotRecord.Part);
				int id = ClothingSlotIdentityKoi.Resolve(clothingSlotRecord);
				partsInfo.id = id;
				return partsInfo;
			}
			if (index == 0)
			{
				ChaFileCoordinate obj = ((source != 0) ? Source(source) : cha?.nowCoordinate);
				if (obj == null)
				{
					return null;
				}
				return obj.clothes.parts[kind];
			}
			if (source == 0)
			{
				return ClothingSlots?.Part(kind, index);
			}
			ClothingSlotRecord clothingSlotRecord2 = WardrobeClothingSlotsKoi.Read(Source(source)).Get(kind, index);
			if (clothingSlotRecord2?.Part == null)
			{
				return null;
			}
			SourceClothingPart orCreateValue = sourceClothingParts.GetOrCreateValue(clothingSlotRecord2);
			if (orCreateValue.Part == null || orCreateValue.Bytes != clothingSlotRecord2.Part)
			{
				ChaFileClothes.PartsInfo partsInfo2 = MessagePackSerializer.Deserialize<ChaFileClothes.PartsInfo>(clothingSlotRecord2.Part);
				int num = ClothingSlotIdentityKoi.Resolve(clothingSlotRecord2);
				partsInfo2.id = ((num >= 0) ? num : 0);
				orCreateValue.Bytes = clothingSlotRecord2.Part;
				orCreateValue.Part = partsInfo2;
			}
			return orCreateValue.Part;
		}

		private ClothingSlotRecord ClothingRecord(int kind, int index, int source = 0)
		{
			if (source == 0)
			{
				return ClothingSlots?.Record(kind, index);
			}
			ClothingSlotSet clothingSlotSet = WardrobeClothingSlotsKoi.Read(Source(source));
			if (index != 0)
			{
				return clothingSlotSet.Get(kind, index);
			}
			return clothingSlotSet.GetPrimary(kind);
		}

		internal bool IsManagedClothingSlot(int kind, int index, int source = 0)
		{
			ClothingSlotRecord clothingSlotRecord = ClothingRecord(kind, index, source);
			if (clothingSlotRecord != null)
			{
				return WardrobeCompositeTopKoi.IsPieceCategory(clothingSlotRecord.SourceCategory);
			}
			return false;
		}

		internal int ClothingSelectionId(int kind, int index, int source = 0)
		{
			ClothingSlotRecord clothingSlotRecord = ClothingRecord(kind, index, source);
			if (clothingSlotRecord != null && WardrobeCompositeTopKoi.IsPieceCategory(clothingSlotRecord.SourceCategory))
			{
				int num = ClothingSlotIdentityKoi.Resolve(clothingSlotRecord);
				if (num >= 0)
				{
					return WardrobeKoiTopCatalog.Token(clothingSlotRecord.SourceCategory, num);
				}
				return 0;
			}
			return GetClothingSlotPart(kind, index, source)?.id ?? 0;
		}

		public byte GetClothingSlotState(int kind, int index)
		{
			if (index == 0 && IsManagedClothingSlot(kind, index))
			{
				return ClothingSlots.Record(kind, index).State;
			}
			if (index > 0)
			{
				return (ClothingSlots?.Record(kind, index)?.State).GetValueOrDefault();
			}
			return WardrobeClothingSlotsKoi.ToUiState(kind, cha.fileStatus.clothesState[kind]);
		}

		public int AddClothingSlot(int kind)
		{
			if ((UnityEngine.Object)(object)ClothingSlots == null || GetClothingSlotCount(kind) >= 5)
			{
				return -1;
			}
			byte[] array = SaveCoordinateBytes();
			if (array == null)
			{
				return -1;
			}
			int num = ClothingSlots.Add(kind);
			if (num > 0)
			{
				PushUndoBytes(array);
				MarkClothingSlotCustom(kind, num);
				WardrobeGUI.SetMessage(ClothesSlotNames[kind] + " " + (num + 1) + " added.");
			}
			return num;
		}

		public void RemoveClothingSlot(int kind, int index)
		{
			if (index < 1 || ClothingSlots?.Record(kind, index) == null)
			{
				return;
			}
			byte[] array = SaveCoordinateBytes();
			if (array != null)
			{
				ClothingSlots.Remove(kind, index);
				PushUndoBytes(array);
				for (int i = index; i < 5; i++)
				{
					mixSel[SlotMixKey(kind, i)] = 0;
				}
				mixSel["Clothes"] = 0;
				mixSel["Colors"] = 0;
				WardrobeGUI.SetMessage(ClothesSlotNames[kind] + " " + (index + 1) + " slot removed.");
			}
		}

		public void ApplyClothingSlot(int kind, int index, int source)
		{
			if (index == 0)
			{
				ApplyClothesSlot(kind, source);
			}
			else
			{
				if (Source(source) == null || (UnityEngine.Object)(object)ClothingSlots == null)
				{
					return;
				}
				ClothingSlotRecord clothingSlotRecord = MixerClothingSource(kind, index, source);
				ClothingSlots.Set(kind, index, clothingSlotRecord);
				ClothingSlotRecord clothingSlotRecord2 = WardrobeClothingSlotsKoi.Read(Source(source)).Get(kind, index);
				if (clothingSlotRecord != null && clothingSlotRecord.Appearance == null && (clothingSlotRecord2 == null || clothingSlotRecord2.ItemId == 0))
				{
					ClothingSlotVisualKoi clothingSlotVisualKoi = ClothingSlots.Visual(kind, index);
					if (clothingSlotVisualKoi != null && clothingSlotVisualKoi.Root != null && WardrobeME.ApplyDetachedClothing(cha, Source(source), kind, clothingSlotVisualKoi.Root))
					{
						clothingSlotVisualKoi.AdoptEditedMaterials();
						ClothingSlots.CaptureAppearance(kind, index);
					}
				}
				mixSel[SlotMixKey(kind, index)] = source;
				mixSel["Clothes"] = 0;
				mixSel["Colors"] = 0;
			}
		}

		private ClothingSlotRecord MixerClothingSource(int kind, int index, int source)
		{
			ChaFileCoordinate coordinate = Source(source);
			if (coordinate == null)
			{
				return null;
			}
			ClothingSlotSet saved = WardrobeClothingSlotsKoi.Read(coordinate);
			return WardrobeClothingMixSource.Select(saved, kind, index, delegate
			{
				ClothingSlotRecord primary = saved.GetPrimary(kind);
				if (primary != null && WardrobeCompositeTopKoi.IsPieceCategory(primary.SourceCategory))
				{
					return primary;
				}
				ChaFileClothes.PartsInfo partsInfo = coordinate.clothes.parts[kind];
				if (partsInfo == null || partsInfo.id == 0)
				{
					return (ClothingSlotRecord)null;
				}
				ClothingSlotRecord clothingSlotRecord = ((primary != null && ClothingSlotIdentityKoi.Resolve(primary) == partsInfo.id) ? primary.Clone() : new ClothingSlotRecord
				{
					Kind = kind
				});
				clothingSlotRecord.ItemId = partsInfo.id;
				clothingSlotRecord.Part = MessagePackSerializer.Serialize(partsInfo);
				ClothingSlotIdentityKoi.Capture(kind, partsInfo.id, clothingSlotRecord);
				if (kind == 0 && clothingSlotRecord.SubParts == null)
				{
					int num = WardrobeCompositeTopKoi.TopType(partsInfo.id);
					if (num != 0)
					{
						clothingSlotRecord.SubParts = new ClothingSlotSubPart[3];
						for (int i = 0; i < 3; i++)
						{
							clothingSlotRecord.SubParts[i] = WardrobeCompositeTopKoi.CapturePiece(WardrobeCompositeTopKoi.Category(num, i), coordinate.clothes.subPartsId[i]);
						}
					}
				}
				return clothingSlotRecord;
			});
		}

		public void ClearClothingSlot(int kind, int index)
		{
			if (index == 0)
			{
				ClearClothesSlot(kind);
				return;
			}
			ClothingSlots?.Set(kind, index, null);
			MarkClothingSlotCustom(kind, index, colors: true);
		}

		public void SetClothingSlotItem(int kind, int index, int id)
		{
			WardrobeKoiTopCatalog.Choice choice = ((kind == 0) ? WardrobeKoiTopCatalog.Decode(id) : new WardrobeKoiTopCatalog.Choice
			{
				Id = id
			});
			if (choice.Category != 0)
			{
				if (index == 0)
				{
					ClearClothesSlot(kind);
				}
				ClothingSlots?.SetItem(kind, index, choice.Id, choice.Category);
				MarkClothingSlotCustom(kind, index, colors: true);
			}
			else if (index == 0)
			{
				WardrobeClothingSlotsKoi.ClearPrimary(cha, kind);
				cha.nowCoordinate.clothes.parts[kind].id = id;
				WardrobeCompat.FileCoord(cha).clothes.parts[kind].id = id;
				WardrobeCompat.ChangeClothes(cha, kind, id, force: true);
				MarkClothesCustom(kind);
			}
			else
			{
				ClothingSlots?.SetItem(kind, index, id);
				MarkClothingSlotCustom(kind, index, colors: true);
			}
		}

		public void SetClothingSlotState(int kind, int index, byte state)
		{
			if (index == 0 && IsManagedClothingSlot(kind, index))
			{
				ClothingSlots.State(kind, index, state);
			}
			else if (index == 0)
			{
				cha.SetClothesState(kind, WardrobeClothingSlotsKoi.ToNativeState(cha, kind, state));
			}
			else
			{
				ClothingSlots?.State(kind, index, state);
			}
		}

		public void MarkClothingSlotCustom(int kind, int index, bool colors = false)
		{
			if (index == 0)
			{
				MarkClothesCustom(kind, colors);
				return;
			}
			mixSel[SlotMixKey(kind, index)] = 0;
			mixSel["Clothes"] = 0;
			if (colors)
			{
				mixSel["Colors"] = 0;
			}
		}

		private void MarkClothingSources(int source)
		{
			for (int i = 0; i < 9; i++)
			{
				for (int j = 1; j < GetClothingSlotCount(i); j++)
				{
					mixSel[SlotMixKey(i, j)] = source;
				}
			}
		}

		private void ApplyExtraClothingColors(ChaFileCoordinate source)
		{
			ClothingSlotsControllerKoi clothingSlots = ClothingSlots;
			if ((UnityEngine.Object)(object)clothingSlots == null || source == null)
			{
				return;
			}
			ClothingSlotSet clothingSlotSet = WardrobeClothingSlotsKoi.Read(source);
			ClothingSlotSet clothingSlotSet2 = clothingSlots.Capture();
			ClothingSlotRecord primary = clothingSlotSet.GetPrimary(0);
			if (!clothingSlots.IsManagedPrimary(0) && primary != null && primary.SourceCategory != 0 && primary.Part != null)
			{
				ChaFileClothes.PartsInfo partsInfo = MessagePackSerializer.Deserialize<ChaFileClothes.PartsInfo>(primary.Part);
				ChaFileClothes.PartsInfo.ColorInfo[] array = MessagePackSerializer.Deserialize<ChaFileClothes.PartsInfo.ColorInfo[]>(MessagePackSerializer.Serialize(partsInfo.colorInfo));
				if (primary.SourceCategory % 10 == 2)
				{
					array[0] = MessagePackSerializer.Deserialize<ChaFileClothes.PartsInfo.ColorInfo>(MessagePackSerializer.Serialize(partsInfo.colorInfo[3]));
				}
				cha.nowCoordinate.clothes.parts[0].colorInfo = array;
				WardrobeCompositeTopKoi.RepaintPrimary(cha, 0);
			}
			for (int i = 0; i < 9; i++)
			{
				for (int j = ((!clothingSlots.IsManagedPrimary(i)) ? 1 : 0); j <= clothingSlotSet2.Count(i); j++)
				{
					ClothingSlotRecord clothingSlotRecord = ((j == 0) ? clothingSlotSet2.GetPrimary(i) : clothingSlotSet2.Get(i, j));
					ClothingSlotRecord clothingSlotRecord2 = ((j == 0) ? clothingSlotSet.GetPrimary(i) : clothingSlotSet.Get(i, j));
					if (clothingSlotRecord?.Part == null)
					{
						continue;
					}
					ChaFileClothes.PartsInfo partsInfo2 = MessagePackSerializer.Deserialize<ChaFileClothes.PartsInfo>(clothingSlotRecord.Part);
					ChaFileClothes.PartsInfo partsInfo3 = ((clothingSlotRecord2?.Part != null) ? MessagePackSerializer.Deserialize<ChaFileClothes.PartsInfo>(clothingSlotRecord2.Part) : ((j == 0) ? source.clothes.parts[i] : null));
					if (partsInfo3 != null)
					{
						partsInfo2.colorInfo = MessagePackSerializer.Deserialize<ChaFileClothes.PartsInfo.ColorInfo[]>(MessagePackSerializer.Serialize(partsInfo3.colorInfo));
						int num = ((clothingSlotRecord2 != null && clothingSlotRecord2.SourceCategory != 0 && clothingSlotRecord2.SourceCategory % 10 == 2) ? 3 : 0);
						int num2 = ((clothingSlotRecord.SourceCategory != 0 && clothingSlotRecord.SourceCategory % 10 == 2) ? 3 : 0);
						if (num != num2)
						{
							partsInfo2.colorInfo[num2] = MessagePackSerializer.Deserialize<ChaFileClothes.PartsInfo.ColorInfo>(MessagePackSerializer.Serialize(partsInfo3.colorInfo[num]));
						}
						ClothingSlotRecord clothingSlotRecord3 = clothingSlotRecord.Clone();
						clothingSlotRecord3.Part = MessagePackSerializer.Serialize(partsInfo2);
						clothingSlots.Set(i, j, clothingSlotRecord3);
						clothingSlots.Repaint(i, j);
					}
				}
			}
		}

		private Dictionary<string, byte[]> CaptureExtraClothingColors()
		{
			Dictionary<string, byte[]> dictionary = new Dictionary<string, byte[]>();
			ClothingSlotsControllerKoi clothingSlots = ClothingSlots;
			if ((UnityEngine.Object)(object)clothingSlots == null)
			{
				return dictionary;
			}
			ClothingSlotSet clothingSlotSet = clothingSlots.Capture();
			for (int i = 0; i < 9; i++)
			{
				for (int j = ((!clothingSlots.IsManagedPrimary(i)) ? 1 : 0); j <= clothingSlotSet.Count(i); j++)
				{
					ClothingSlotRecord clothingSlotRecord = ((j == 0) ? clothingSlotSet.GetPrimary(i) : clothingSlotSet.Get(i, j));
					if (clothingSlotRecord?.Part != null)
					{
						ChaFileClothes.PartsInfo partsInfo = MessagePackSerializer.Deserialize<ChaFileClothes.PartsInfo>(clothingSlotRecord.Part);
						dictionary[SlotMixKey(i, j)] = MessagePackSerializer.Serialize(partsInfo.colorInfo);
					}
				}
			}
			return dictionary;
		}

		private void RestoreExtraClothingColors(Dictionary<string, byte[]> colors)
		{
			ClothingSlotsControllerKoi clothingSlots = ClothingSlots;
			if ((UnityEngine.Object)(object)clothingSlots == null || colors == null)
			{
				return;
			}
			for (int i = 0; i < 9; i++)
			{
				for (int j = ((!clothingSlots.IsManagedPrimary(i)) ? 1 : 0); j <= clothingSlots.Count(i); j++)
				{
					if (colors.TryGetValue(SlotMixKey(i, j), out var value))
					{
						ClothingSlotRecord clothingSlotRecord = clothingSlots.Record(i, j);
						if (clothingSlotRecord?.Part != null)
						{
							ChaFileClothes.PartsInfo partsInfo = MessagePackSerializer.Deserialize<ChaFileClothes.PartsInfo>(clothingSlotRecord.Part);
							partsInfo.colorInfo = MessagePackSerializer.Deserialize<ChaFileClothes.PartsInfo.ColorInfo[]>(value);
							ClothingSlotRecord clothingSlotRecord2 = clothingSlotRecord.Clone();
							clothingSlotRecord2.Part = MessagePackSerializer.Serialize(partsInfo);
							clothingSlots.Set(i, j, clothingSlotRecord2);
							clothingSlots.Repaint(i, j);
						}
					}
				}
			}
		}

		private void RandomMixExtraClothing(int maxSource)
		{
			ClothingSlotsControllerKoi clothingSlots = ClothingSlots;
			if ((UnityEngine.Object)(object)clothingSlots == null)
			{
				return;
			}
			for (int i = 0; i < 9; i++)
			{
				int val = clothingSlots.Count(i);
				for (int j = 1; j <= maxSource; j++)
				{
					if (Source(j) != null)
					{
						val = Math.Max(val, WardrobeClothingSlotsKoi.Read(Source(j)).Count(i));
					}
				}
				val = Math.Min(val, 4);
				for (int k = 1; k <= val; k++)
				{
					int num = UnityEngine.Random.Range(1, maxSource + 1);
					ChaFileCoordinate chaFileCoordinate = Source(num);
					if (chaFileCoordinate != null)
					{
						clothingSlots.Set(i, k, WardrobeClothingSlotsKoi.Read(chaFileCoordinate).Get(i, k));
						mixSel[SlotMixKey(i, k)] = num;
					}
				}
			}
		}

		public Texture2D LiveClothesThumb(int kind, int index)
		{
			ClothingSlotRecord clothingSlotRecord = ClothingRecord(kind, index);
			if (clothingSlotRecord != null && clothingSlotRecord.SourceCategory != 0)
			{
				int num = ClothingSlotIdentityKoi.Resolve(clothingSlotRecord);
				if (num >= 0)
				{
					return WardrobeKoiTopCatalog.Thumb(clothingSlotRecord.SourceCategory, num);
				}
				return null;
			}
			ChaFileClothes.PartsInfo clothingSlotPart = GetClothingSlotPart(kind, index);
			if (clothingSlotPart == null || clothingSlotPart.id == 0)
			{
				return null;
			}
			return WardrobeWidgets.ItemThumb((int)ClothesCategories[kind], clothingSlotPart.id);
		}

		public Texture2D SrcClothesThumb(int source, int kind, int index)
		{
			if (index > 0)
			{
				ChaFileCoordinate chaFileCoordinate = Source(source);
				if (chaFileCoordinate == null)
				{
					return null;
				}
				ClothingSlotRecord clothingSlotRecord = WardrobeClothingSlotsKoi.Read(chaFileCoordinate).Get(kind, index);
				if (clothingSlotRecord == null || clothingSlotRecord.ItemId == 0)
				{
					return SrcClothesThumb(source, kind, 0);
				}
			}
			ClothingSlotRecord clothingSlotRecord2 = ClothingRecord(kind, index, source);
			if (clothingSlotRecord2 != null && clothingSlotRecord2.SourceCategory != 0)
			{
				int num = ClothingSlotIdentityKoi.Resolve(clothingSlotRecord2);
				if (num >= 0)
				{
					return WardrobeKoiTopCatalog.Thumb(clothingSlotRecord2.SourceCategory, num);
				}
				return null;
			}
			ChaFileClothes.PartsInfo clothingSlotPart = GetClothingSlotPart(kind, index, source);
			if (clothingSlotPart == null || clothingSlotPart.id == 0)
			{
				return null;
			}
			return WardrobeWidgets.ItemThumb((int)ClothesCategories[kind], clothingSlotPart.id);
		}

		public bool CanPasteClothesItem(int kind, int index)
		{
			if (cha != null && clothingSlotClipboard != null && clothesItemKind == kind && index >= 0)
			{
				return index < GetClothingSlotCount(kind);
			}
			return false;
		}

		public void CopyClothesItem(int kind, int index)
		{
			if (cha == null || kind < 0 || kind >= 9)
			{
				return;
			}
			byte[] array = SaveCoordinateBytes();
			if (array != null)
			{
				ClothingSlotRecord clothingSlotRecord = ((index == 0) ? WardrobeClothingSlotsKoi.CapturePrimary(cha, kind) : ClothingSlots?.Record(kind, index)?.Clone());
				if (clothingSlotRecord != null)
				{
					clothingSlotClipboard = clothingSlotRecord;
					clothingSlotClipboardIndex = index;
					clothesItemClipboard = array;
					clothesItemKind = kind;
					clothesItemState = clothingSlotRecord.State;
					WardrobeGUI.SetMessage(ClothesSlotNames[kind] + ((index > 0) ? (" " + (index + 1)) : "") + " copied.");
				}
			}
		}

		public void PasteClothesItem(int kind, int index)
		{
			if (!CanPasteClothesItem(kind, index))
			{
				return;
			}
			byte[] array = SaveCoordinateBytes();
			if (array == null)
			{
				return;
			}
			try
			{
				if (index == 0)
				{
					ChaFileCoordinate chaFileCoordinate = LoadCoordFromBytes(clothesItemClipboard);
					if (chaFileCoordinate == null)
					{
						throw new InvalidOperationException("Invalid clothing clipboard.");
					}
					if (clothingSlotClipboardIndex > 0)
					{
						chaFileCoordinate = new ChaFileCoordinate();
						chaFileCoordinate.clothes.parts[kind] = MessagePackSerializer.Deserialize<ChaFileClothes.PartsInfo>(clothingSlotClipboard.Part);
					}
					ChaFileClothes.PartsInfo partsInfo = MessagePackSerializer.Deserialize<ChaFileClothes.PartsInfo>(clothingSlotClipboard.Part);
					partsInfo.id = ClothingSlotIdentityKoi.Resolve(clothingSlotClipboard);
					if (partsInfo.id < 0)
					{
						throw new InvalidOperationException("The clothing source mod is missing.");
					}
					if (WardrobeCompositeTopKoi.IsPieceCategory(clothingSlotClipboard.SourceCategory))
					{
						chaFileCoordinate.clothes.parts[kind] = new ChaFileClothes.PartsInfo();
					}
					else
					{
						chaFileCoordinate.clothes.parts[kind] = partsInfo;
						if (kind == 0 && clothingSlotClipboard.SubParts != null)
						{
							for (int i = 0; i < 3; i++)
							{
								chaFileCoordinate.clothes.subPartsId[i] = WardrobeCompositeTopKoi.ResolvePiece(WardrobeCompositeTopKoi.Category(WardrobeCompositeTopKoi.TopType(partsInfo.id), i), clothingSlotClipboard.SubParts[i]);
							}
						}
					}
					ApplyClothesItem(chaFileCoordinate, kind, 0, deferME: false);
					WardrobeClothingSlotsKoi.ApplyPrimaryAppearance(cha, kind, clothingSlotClipboard);
					SetClothingSlotState(kind, 0, clothesItemState);
					MarkClothesCustom(kind, colors: true);
				}
				else
				{
					ClothingSlots.Set(kind, index, clothingSlotClipboard.Clone());
					MarkClothingSlotCustom(kind, index, colors: true);
				}
				PushUndoBytes(array);
				WardrobeGUI.SetMessage(ClothesSlotNames[kind] + ((index > 0) ? (" " + (index + 1)) : "") + " pasted.");
			}
			catch (Exception ex)
			{
				ChaFileCoordinate chaFileCoordinate2 = LoadCoordFromBytes(array);
				if (chaFileCoordinate2 != null)
				{
					ApplyFullCoordinate(chaFileCoordinate2);
				}
				WardrobeGUI.SetMessage("Clothing paste failed: " + ex.GetBaseException().Message);
			}
		}
	}
	internal static class WardrobeShaderColor4TabKoi
	{
		private static readonly WardrobeColor4LayoutStateKoi clothes = new WardrobeColor4LayoutStateKoi();

		private static readonly WardrobeColor4LayoutStateKoi accessories = new WardrobeColor4LayoutStateKoi();

		private static readonly WardrobeColor4LayoutStateKoi extras = new WardrobeColor4LayoutStateKoi();

		internal static void DrawClothing(WardrobeSession session, int kind, int itemId)
		{
			Draw(session, kind, 0, itemId, 0, clothes);
		}

		internal static void DrawAccessory(WardrobeSession session, int slot, int itemId)
		{
			Draw(session, slot, 0, itemId, 1, accessories);
		}

		internal static void DrawExtra(WardrobeSession session, int kind, int index, int itemId)
		{
			Draw(session, kind, index, itemId, 2, extras);
		}

		private static void Draw(WardrobeSession session, int slot, int index, int itemId, int target, WardrobeColor4LayoutStateKoi state)
		{
			ChaControl cha = session.cha;
			GameObject value = ((target == 1) ? WardrobeMaterialColor4Koi.AccessoryRoot(cha, slot) : WardrobeMaterialColor4Koi.ClothingRoot(cha, slot, index));
			int actor = WardrobeMaterialColor4Koi.Identity(cha);
			int root = WardrobeMaterialColor4Koi.Identity(value);
			WardrobeMaterialColor4Koi.Value value2;
			if (Event.current.type == EventType.Layout)
			{
				try
				{
					bool meReady = target == 2 || WardrobeME.CanEditProps(cha);
					string materialSignature = ((target == 1) ? WardrobeMaterialColor4Koi.AccessoryLayoutSignature(cha, slot, itemId) : WardrobeMaterialColor4Koi.ClothingLayoutSignature(cha, slot, index, itemId)) + (meReady ? ":me" : ":no-me");
					state.Refresh(isLayout: true, actor, root, materialSignature, delegate
					{
						bool flag = ((target == 1) ? WardrobeMaterialColor4Koi.TryReadAccessory(cha, slot, out value2) : WardrobeMaterialColor4Koi.TryReadClothing(cha, slot, index, out value2));
						return new WardrobeColor4LayoutStateKoi.Presence
						{
							Visible = (flag && meReady),
							Gloss = (flag && value2.HasGloss),
							Metallic = (flag && value2.HasMetallic)
						};
					});
				}
				catch (Exception ex)
				{
					WardrobePlugin.Log.LogWarning((object)("Shader Color 4 layout probe failed: " + ex.GetBaseException().Message));
				}
			}
			if (!state.VisibleFor(actor, root))
			{
				return;
			}
			string text = "shader4_" + target + "_" + slot + "_" + index;
			WardrobeWidgets.BeginZone(WardrobeLang.T("Shader Color 4"), delegate
			{
				if (WardrobeWidgets.MiniButton("Copy", 44f) && Read(cha, slot, index, target, out value2))
				{
					WardrobeChannelClipboard.CopyMaterialChannel(value2);
				}
				bool enabled = GUI.enabled;
				GUI.enabled = enabled && WardrobeChannelClipboard.HasData;
				if (WardrobeWidgets.MiniButton("Paste", 44f) && Read(cha, slot, index, target, out var value3))
				{
					session.PushUndo();
					Apply(session, slot, index, target, WardrobeChannelClipboard.ApplyMaterialChannel(value3));
				}
				GUI.enabled = enabled;
				if (WardrobeWidgets.MiniButton("Reset", 44f))
				{
					session.PushUndo();
					if ((target == 1) ? WardrobeHomeColor4Koi.ResetAccessory(cha, slot) : WardrobeHomeColor4Koi.ResetClothing(cha, slot, index))
					{
						Mark(session, slot, index, target);
					}
				}
			});
			WardrobeWidgets.SwatchColumn(session, null, text + "_color", () => (!Read(cha, slot, index, target, out value2)) ? Color.white : value2.Color, delegate(Color v)
			{
				if (Read(cha, slot, index, target, out value2))
				{
					value2.Color = v;
					Apply(session, slot, index, target, value2);
				}
			});
			if (state.GlossFor(actor, root))
			{
				WardrobeWidgets.MiniSliderRow(session, "Gloss", text + "_gloss", () => (!Read(cha, slot, index, target, out value2)) ? 0f : value2.Gloss, delegate(float v)
				{
					if (Read(cha, slot, index, target, out value2) && value2.HasGloss)
					{
						value2.Gloss = v;
						Apply(session, slot, index, target, value2);
					}
				});
			}
			if (state.MetallicFor(actor, root))
			{
				WardrobeWidgets.MiniSliderRow(session, "Metal", text + "_metal", () => (!Read(cha, slot, index, target, out value2)) ? 0f : value2.Metallic, delegate(float v)
				{
					if (Read(cha, slot, index, target, out value2) && value2.HasMetallic)
					{
						value2.Metallic = v;
						Apply(session, slot, index, target, value2);
					}
				});
			}
			WardrobeWidgets.EndZone();
		}

		private static bool Read(ChaControl cha, int slot, int index, int target, out WardrobeMaterialColor4Koi.Value value)
		{
			if (target != 1)
			{
				return WardrobeMaterialColor4Koi.TryReadClothing(cha, slot, index, out value);
			}
			return WardrobeMaterialColor4Koi.TryReadAccessory(cha, slot, out value);
		}

		private static void Apply(WardrobeSession session, int slot, int index, int target, WardrobeMaterialColor4Koi.Value value)
		{
			if (target == 1)
			{
				WardrobeMaterialColor4Koi.ApplyAccessory(session.cha, slot, value);
			}
			else
			{
				WardrobeMaterialColor4Koi.ApplyClothing(session.cha, slot, index, value);
			}
			Mark(session, slot, index, target);
		}

		private static void Mark(WardrobeSession session, int slot, int index, int target)
		{
			switch (target)
			{
			case 1:
				session.MarkAccCustom(slot);
				break;
			case 2:
				session.MarkClothingSlotCustom(slot, index, colors: true);
				break;
			default:
				session.MarkClothesCustom(slot, colors: true, itemToo: false);
				break;
			}
		}
	}
	internal static class WardrobeTextureRenderTarget
	{
		internal sealed class Snapshot
		{
			internal RenderTexture Target;

			internal bool Srgb;

			internal string GuiEvent;
		}

		private static bool attempted;

		private static int reports;

		internal static void Prefix(out Snapshot __state)
		{
			__state = new Snapshot
			{
				Target = RenderTexture.active,
				Srgb = GL.sRGBWrite,
				GuiEvent = ((Event.current == null) ? null : Event.current.type.ToString())
			};
		}

		internal static Exception Finalizer(Exception __exception, Snapshot __state)
		{
			if (__state == null)
			{
				return __exception;
			}
			RenderTexture active = RenderTexture.active;
			bool flag = (object)active != __state.Target;
			try
			{
				RenderTexture.active = __state.Target;
			}
			finally
			{
				GL.sRGBWrite = __state.Srgb;
			}
			if (flag && __state.GuiEvent != null && reports < 4)
			{
				reports++;
				WardrobePlugin.Log.LogInfo((object)("[TextureTarget] Restored drawing target after native texture bake during " + __state.GuiEvent + "; before=" + Describe(__state.Target) + "; after=" + Describe(active)));
			}
			return __exception;
		}

		private static string Describe(RenderTexture target)
		{
			if (!(target == null))
			{
				return target.name + "#" + target.GetInstanceID();
			}
			return "screen";
		}

		internal static void EnsureInstalled()
		{
			if (attempted)
			{
				return;
			}
			attempted = true;
			try
			{
				MethodInfo method = typeof(CustomTextureCreate).GetMethod("RebuildTextureAndSetMaterial", Type.EmptyTypes);
				if (method == null || method.ReturnType != typeof(Texture))
				{
					throw new MissingMethodException("Native texture compositor unavailable.");
				}
				Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
				foreach (Assembly obj in assemblies)
				{
					Type type = obj.GetType("HarmonyLib.Harmony", throwOnError: false);
					Type type2 = obj.GetType("HarmonyLib.HarmonyMethod", throwOnError: false);
					if (type == null || type2 == null)
					{
						continue;
					}
					MethodInfo methodInfo = type.GetMethods(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault((MethodInfo x) => x.Name == "Patch" && x.GetParameters().Any((ParameterInfo p) => p.Name == "finalizer"));
					if (methodInfo == null)
					{
						continue;
					}
					object obj2 = Activator.CreateInstance(type, "Freelancer604.VanityStudio.TextureTarget");
					object obj3 = Activator.CreateInstance(type2, typeof(WardrobeTextureRenderTarget).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic));
					object obj4 = Activator.CreateInstance(type2, typeof(WardrobeTextureRenderTarget).GetMethod("Finalizer", BindingFlags.Static | BindingFlags.NonPublic));
					FieldInfo field = type2.GetField("priority");
					if (field != null)
					{
						field.SetValue(obj3, 800);
						field.SetValue(obj4, 0);
					}
					ParameterInfo[] parameters = methodInfo.GetParameters();
					object[] array = new object[parameters.Length];
					array[0] = method;
					for (int num = 1; num < parameters.Length; num++)
					{
						if (parameters[num].Name == "prefix")
						{
							array[num] = obj3;
						}
						else if (parameters[num].Name == "finalizer")
						{
							array[num] = obj4;
						}
					}
					methodInfo.Invoke(obj2, array);
					WardrobePlugin.Log.LogInfo((object)"Clothing texture render-target protection enabled.");
					return;
				}
				throw new NotSupportedException("Harmony with finalizer support was not found.");
			}
			catch (Exception ex)
			{
				WardrobePlugin.Log.LogWarning((object)("Clothing texture render-target protection could not start: " + ex.GetBaseException().Message));
			}
		}
	}
	internal static class WardrobeWidgets
	{
		private struct ThumbGridFrame
		{
			internal int Rows;

			internal int FirstRow;

			internal int LastRow;

			internal float CellW;

			internal float ThumbSize;

			internal float RowH;

			internal float ContentH;

			internal float ViewH;

			internal float ScrollY;
		}

		internal const float LabelW = 105f;

		internal const float SwatchW = 108f;

		private const int GridCols = 3;

		private const float GridVisibleRows = 5f;

		private const float NameH = 18f;

		private const float MarkSize = 34f;

		private const float AdjLabelW = 56f;

		private const float AdjResetW = 20f;

		private const float AdjCoarseW = 20f;

		private const float AdjFineW = 16f;

		private const float AdjRowH = 20f;

		private static readonly Dictionary<string, bool> expanded = new Dictionary<string, bool>();

		private static readonly Dictionary<string, Vector2> scrolls = new Dictionary<string, Vector2>();

		private static readonly Dictionary<string, ThumbGridFrame> thumbGridFrames = new Dictionary<string, ThumbGridFrame>();

		private static readonly Dictionary<string, string> searches = new Dictionary<string, string>();

		private static readonly Dictionary<string, string> appliedSearches = new Dictionary<string, string>();

		private static readonly HashSet<string> pendingScrollTo = new HashSet<string>();

		private static readonly Dictionary<string, List<WardrobeItem>> listCache = new Dictionary<string, List<WardrobeItem>>();

		private static readonly Dictionary<string, List<WardrobeItem>> sortedCache = new Dictionary<string, List<WardrobeItem>>();

		private static readonly Dictionary<string, WardrobeSearchCache<WardrobeItem>> filteredCache = new Dictionary<string, WardrobeSearchCache<WardrobeItem>>();

		private static readonly Dictionary<string, Texture2D> thumbCache = new Dictionary<string, Texture2D>();

		private static Texture2D whiteTex;

		private static GUIStyle nameStyle;

		private static GUIStyle markStyle;

		private static GUIStyle rgbaStyle;

		private static GUIStyle miniLabelStyle;

		private static GUIStyle zoneTitleStyle;

		private static GUIStyle miniButtonStyle;

		private static GUIStyle valStyle;

		private static Texture2D sliderTrackTex;

		private static GUIStyle sliderTrackStyle;

		private static GUIStyle axisHeaderStyle;

		private static GUIStyle adjFieldStyle;

		private static readonly Color ZoneTitleTint = new Color(0.55f, 0.8f, 1f);

		private static readonly Dictionary<int, Dictionary<int, WardrobeItem>> catalogById = new Dictionary<int, Dictionary<int, WardrobeItem>>();

		private static readonly HashSet<string> thumbFailLogged = new HashSet<string>();

		private static int thumbBudget;

		private static int thumbBudgetFrame = -1;

		private static ThumbGridFrame PlanThumbGrid(int itemCount, float visibleRows, float scrollY)
		{
			ThumbGridFrame result = default(ThumbGridFrame);
			result.Rows = (itemCount + 3 - 1) / 3;
			result.CellW = 142f;
			result.ThumbSize = Mathf.Min(result.CellW - 10f, 92f);
			result.RowH = result.ThumbSize + 18f + 8f;
			result.ContentH = Mathf.Max(result.RowH + 10f, (float)result.Rows * result.RowH + 10f);
			result.ViewH = Mathf.Min(result.ContentH, visibleRows * result.RowH);
			result.ScrollY = scrollY;
			result.FirstRow = Mathf.Max(0, (int)(scrollY / result.RowH) - 1);
			result.LastRow = Mathf.Min(result.Rows, result.FirstRow + (int)(result.ViewH / result.RowH) + 3);
			return result;
		}

		internal static void CollapseAll()
		{
			expanded.Clear();
			scrolls.Clear();
			thumbGridFrames.Clear();
			searches.Clear();
			appliedSearches.Clear();
			pendingScrollTo.Clear();
			listCache.Clear();
			sortedCache.Clear();
			filteredCache.Clear();
		}

		internal static void SectionLabel(string text)
		{
			GUILayout.Space(6f);
			GUILayout.Label(WardrobeLang.T(text), new GUIStyle(GUI.skin.label)
			{
				fontStyle = FontStyle.Bold
			});
		}

		private static void EnsureStyles()
		{
			if (whiteTex == null)
			{
				whiteTex = new Texture2D(1, 1);
				whiteTex.SetPixel(0, 0, Color.white);
				whiteTex.Apply();
			}
			if (nameStyle == null)
			{
				nameStyle = new GUIStyle(GUI.skin.label)
				{
					fontSize = 10,
					alignment = TextAnchor.UpperCenter,
					clipping = TextClipping.Clip,
					wordWrap = false
				};
			}
			if (markStyle == null)
			{
				markStyle = new GUIStyle(GUI.skin.label)
				{
					fontSize = 24,
					fontStyle = FontStyle.Bold,
					alignment = TextAnchor.MiddleCenter,
					clipping = TextClipping.Overflow
				};
			}
			if (rgbaStyle == null)
			{
				rgbaStyle = new GUIStyle(GUI.skin.label)
				{
					fontSize = 9,
					alignment = TextAnchor.UpperCenter,
					clipping = TextClipping.Clip,
					wordWrap = false
				};
			}
			if (miniLabelStyle == null)
			{
				miniLabelStyle = new GUIStyle(GUI.skin.label)
				{
					fontSize = 10,
					alignment = TextAnchor.MiddleLeft,
					clipping = TextClipping.Clip,
					wordWrap = false
				};
			}
			if (zoneTitleStyle == null)
			{
				zoneTitleStyle = new GUIStyle(GUI.skin.label)
				{
					fontSize = 10,
					fontStyle = FontStyle.Bold,
					alignment = TextAnchor.MiddleLeft
				};
			}
			if (miniButtonStyle == null)
			{
				miniButtonStyle = new GUIStyle(GUI.skin.button)
				{
					fontSize = 10
				};
			}
			if (valStyle == null)
			{
				valStyle = new GUIStyle(GUI.skin.label)
				{
					fontSize = 10,
					alignment = TextAnchor.MiddleRight,
					clipping = TextClipping.Clip
				};
			}
			if (sliderTrackStyle == null)
			{
				sliderTrackTex = new Texture2D(1, 1);
				sliderTrackTex.SetPixel(0, 0, new Color(0.45f, 0.45f, 0.48f, 1f));
				sliderTrackTex.Apply();
				sliderTrackStyle = new GUIStyle(GUI.skin.horizontalSlider);
				sliderTrackStyle.normal.background = sliderTrackTex;
				sliderTrackStyle.hover.background = sliderTrackTex;
				sliderTrackStyle.active.background = sliderTrackTex;
				sliderTrackStyle.focused.background = sliderTrackTex;
				sliderTrackStyle.onNormal.background = sliderTrackTex;
				sliderTrackStyle.onHover.background = sliderTrackTex;
				sliderTrackStyle.onActive.background = sliderTrackTex;
				sliderTrackStyle.onFocused.background = sliderTrackTex;
			}
			if (axisHeaderStyle == null)
			{
				axisHeaderStyle = new GUIStyle(GUI.skin.label)
				{
					fontSize = 10,
					fontStyle = FontStyle.Bold,
					alignment = TextAnchor.MiddleCenter
				};
			}
			if (adjFieldStyle == null)
			{
				adjFieldStyle = new GUIStyle(GUI.skin.textField)
				{
					fontSize = 10,
					alignment = TextAnchor.MiddleCenter
				};
			}
		}

		internal static void BeginZone(string title, Action headerRight = null)
		{
			EnsureStyles();
			GUILayout.BeginVertical(GUI.skin.box);
			if (title != null || headerRight != null)
			{
				GUILayout.BeginHorizontal();
				if (title != null)
				{
					Color color = GUI.color;
					GUI.color = ZoneTitleTint;
					GUILayout.Label(WardrobeLang.T(title), zoneTitleStyle);
					GUI.color = color;
				}
				GUILayout.FlexibleSpace();
				headerRight?.Invoke();
				GUILayout.EndHorizontal();
			}
		}

		internal static void EndZone()
		{
			GUILayout.EndVertical();
			GUILayout.Space(2f);
		}

		internal static void MiniLabel(string text, float width = 105f)
		{
			EnsureStyles();
			GUILayout.Label(WardrobeLang.T(text), miniLabelStyle, GUILayout.Width(width));
		}

		internal static bool MiniButton(string text, float width = 0f)
		{
			EnsureStyles();
			string text2 = WardrobeLang.T(text);
			if (!(width > 0f))
			{
				return GUILayout.Button(text2, miniButtonStyle);
			}
			return GUILayout.Button(text2, miniButtonStyle, GUILayout.Width(width));
		}

		internal static bool MiniButton(GUIContent content, float width = 0f)
		{
			EnsureStyles();
			if (!(width > 0f))
			{
				return GUILayout.Button(content, miniButtonStyle);
			}
			return GUILayout.Button(content, miniButtonStyle, GUILayout.Width(width));
		}

		internal static int FillButtonRow(string label, float labelW, string[] buttons, int highlight = -1, float rowHeight = 0f, bool[] enabled = null)
		{
			EnsureStyles();
			if (rowHeight <= 0f)
			{
				rowHeight = GUI.skin.button.CalcHeight(new GUIContent("Xy"), 100f);
			}
			GUILayout.BeginHorizontal();
			if (label != null)
			{
				MiniLabel(label, labelW);
			}
			Rect rect = GUILayoutUtility.GetRect(10f, rowHeight, GUILayout.ExpandWidth(expand: true), GUILayout.Height(rowHeight));
			GUILayout.EndHorizontal();
			int result = -1;
			if (buttons != null && buttons.Length != 0)
			{
				bool enabled2 = GUI.enabled;
				float num = rect.width / (float)buttons.Length;
				for (int i = 0; i < buttons.Length; i++)
				{
					Rect position = new Rect(rect.x + (float)i * num, rect.y, num, rect.height);
					Color color = GUI.color;
					if (i == highlight)
					{
						GUI.color = Color.green;
					}
					GUI.enabled = enabled2 && (enabled == null || enabled[i]);
					if (GUI.Button(position, WardrobeLang.T(buttons[i])))
					{
						result = i;
					}
					GUI.enabled = enabled2;
					GUI.color = color;
				}
			}
			return result;
		}

		internal static void SwatchColumn(WardrobeSession s, string label, string undoKey, Func<Color> get, Action<Color> set, float width = 108f)
		{
			EnsureStyles();
			Color init = get();
			GUILayout.BeginVertical(GUILayout.Width(width));
			if (!string.IsNullOrEmpty(label))
			{
				GUILayout.Label(WardrobeLang.T(label), miniLabelStyle, GUILayout.Width(width));
			}
			Rect rect = GUILayoutUtility.GetRect(width - 6f, 30f, GUILayout.Width(width - 6f), GUILayout.Height(30f));
			bool num = GUI.Button(rect, GUIContent.none);
			Color color = GUI.color;
			GUI.color = new Color(init.r, init.g, init.b, 1f);
			GUI.DrawTexture(new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f), whiteTex, ScaleMode.StretchToFill);
			GUI.color = color;
			if (num)
			{
				byte[] pre = s.SaveCoordinateBytes();
				WardrobePalette.Open(WardrobeLang.T(label), init, useAlpha: true, delegate(Color c)
				{
					try
					{
						if (c != get())
						{
							if (pre != null)
							{
								s.PushUndoBytes(pre);
								pre = null;
							}
							set(c);
						}
					}
					catch (Exception ex)
					{
						WardrobePlugin.Log.LogError((object)("Color set failed: " + ex));
					}
				});
			}
			GUILayout.Label($"R{(int)(init.r * 255f)} G{(int)(init.g * 255f)} B{(int)(init.b * 255f)} A{(int)(init.a * 100f)}", rgbaStyle, GUILayout.Width(width));
			GUILayout.EndVertical();
		}

		internal static void SliderRow(WardrobeSession s, string label, string undoKey, Func<float> get, Action<float> set, float min = 0f, float max = 1f)
		{
			SliderRowW(s, label, undoKey, get, set, 105f, min, max);
		}

		internal static void MiniSliderRow(WardrobeSession s, string label, string undoKey, Func<float> get, Action<float> set, float min = 0f, float max = 1f)
		{
			SliderRowW(s, label, undoKey, get, set, 62f, min, max);
		}

		private static void SliderRowW(WardrobeSession s, string label, string undoKey, Func<float> get, Action<float> set, float labelW, float min, float max)
		{
			EnsureStyles();
			float num = get();
			GUILayout.BeginHorizontal();
			GUILayout.Label(WardrobeLang.T(label), miniLabelStyle, GUILayout.Width(labelW));
			bool changed = GUI.changed;
			GUI.changed = false;
			float num2 = GUILayout.HorizontalSlider(num, min, max, sliderTrackStyle, GUI.skin.horizontalSliderThumb, GUILayout.ExpandWidth(expand: true));
			bool changed2 = GUI.changed;
			GUI.changed |= changed;
			GUILayout.Label($"{(changed2 ? num2 : num) * 100f:F0}", GUILayout.Width(30f));
			GUILayout.EndHorizontal();
			if (changed2 && !Mathf.Approximately(num2, num))
			{
				s.PushUndoCoalesced(undoKey);
				set(num2);
			}
		}

		internal static void ToggleRow(WardrobeSession s, string label, string undoKey, Func<bool> get, Action<bool> set)
		{
			bool flag = get();
			bool flag2 = GUILayout.Toggle(flag, " " + WardrobeLang.T(label));
			if (flag2 != flag)
			{
				s.PushUndo();
				set(flag2);
			}
		}

		internal static void StatusRow(WardrobeSession s, string label, string[] names, int[] values, int cur, Action<int> set, float labelWidth = 105f, Action trailing = null)
		{
			EnsureStyles();
			GUILayout.BeginHorizontal();
			GUILayout.Label(WardrobeLang.T(label), miniLabelStyle, GUILayout.Width(labelWidth));
			for (int i = 0; i < names.Length; i++)
			{
				Color color = GUI.color;
				if (values[i] == cur)
				{
					GUI.color = Color.green;
				}
				if (GUILayout.Button(WardrobeLang.T(names[i]), GUILayout.Width(52f)) && values[i] != cur)
				{
					s.PushUndo();
					set(values[i]);
				}
				GUI.color = color;
			}
			GUILayout.FlexibleSpace();
			trailing?.Invoke();
			GUILayout.EndHorizontal();
		}

		internal static void PairSliderRow(WardrobeSession s, string label1, string key1, Func<float> get1, Action<float> set1, string label2, string key2, Func<float> get2, Action<float> set2)
		{
			EnsureStyles();
			GUILayout.BeginHorizontal();
			HalfSlider(s, label1, key1, get1, set1);
			GUILayout.Space(10f);
			HalfSlider(s, label2, key2, get2, set2);
			GUILayout.EndHorizontal();
		}

		private static void HalfSlider(WardrobeSession s, string label, string undoKey, Func<float> get, Action<float> set)
		{
			float num = get();
			GUILayout.Label(WardrobeLang.T(label), miniLabelStyle, GUILayout.Width(16f));
			bool changed = GUI.changed;
			GUI.changed = false;
			float num2 = GUILayout.HorizontalSlider(num, 0f, 1f, sliderTrackStyle, GUI.skin.horizontalSliderThumb, GUILayout.ExpandWidth(expand: true));
			bool changed2 = GUI.changed;
			GUI.changed |= changed;
			GUILayout.Label($"{(changed2 ? num2 : num) * 100f:F0}", valStyle, GUILayout.Width(24f));
			if (changed2 && !Mathf.Approximately(num2, num))
			{
				s.PushUndoCoalesced(undoKey);
				set(num2);
			}
		}

		internal static void AxisHeaderRow(string leftLabel)
		{
			EnsureStyles();
			GUILayout.BeginHorizontal();
			Color color = GUI.color;
			GUI.color = ZoneTitleTint;
			GUILayout.Label(WardrobeLang.T(leftLabel), zoneTitleStyle, GUILayout.Width(56f));
			Rect rect = GUILayoutUtility.GetRect(10f, 20f, GUILayout.ExpandWidth(expand: true), GUILayout.Height(20f));
			GUILayout.EndHorizontal();
			float num = (rect.width - 24f) / 3f;
			string[] array = new string[3] { "X", "Y", "Z" };
			for (int i = 0; i < 3; i++)
			{
				GUI.Label(new Rect(rect.x + (float)i * num, rect.y, num, rect.height), array[i], axisHeaderStyle);
			}
			GUI.color = color;
		}

		internal static void TripleValueRow(WardrobeSession s, string label, string keyBase, Func<int, float> get, Action<int, float> set, float step, float coarseStep, float def, bool wrap360 = false)
		{
			EnsureStyles();
			GUILayout.BeginHorizontal();
			GUILayout.Label(WardrobeLang.T(label), miniLabelStyle, GUILayout.Width(56f));
			Rect rect = GUILayoutUtility.GetRect(10f, 20f, GUILayout.ExpandWidth(expand: true), GUILayout.Height(20f));
			GUILayout.EndHorizontal();
			float num = (rect.width - 24f) / 3f;
			for (int i = 0; i < 3; i++)
			{
				float num2 = get(i);
				float num3 = num2;
				float num4 = rect.x + (float)i * num;
				float num5 = Mathf.Max(24f, num - 40f - 32f - 5f);
				Rect position = new Rect(num4, rect.y, 20f, rect.height);
				num4 += 21f;
				Rect position2 = new Rect(num4, rect.y, 16f, rect.height);
				num4 += 17f;
				Rect position3 = new Rect(num4, rect.y, num5, rect.height);
				num4 += num5 + 1f;
				Rect position4 = new Rect(num4, rect.y, 16f, rect.height);
				num4 += 17f;
				Rect position5 = new Rect(num4, rect.y, 20f, rect.height);
				if (GUI.RepeatButton(position, "«", miniButtonStyle))
				{
					num3 -= coarseStep;
				}
				if (GUI.RepeatButton(position2, "◀", miniButtonStyle))
				{
					num3 -= step;
				}
				string text = $"{num2:F3}";
				string text2 = GUI.TextField(position3, text, adjFieldStyle);
				if (text2 != text && float.TryParse(text2, out var result))
				{
					num3 = result;
				}
				if (GUI.RepeatButton(position4, "▶", miniButtonStyle))
				{
					num3 += step;
				}
				if (GUI.RepeatButton(position5, "»", miniButtonStyle))
				{
					num3 += coarseStep;
				}
				if (wrap360)
				{
					num3 = (num3 % 360f + 360f) % 360f;
				}
				if (!Mathf.Approximately(num3, num2))
				{
					s.PushUndoCoalesced(keyBase + i);
					set(i, num3);
				}
			}
			if (GUI.Button(new Rect(rect.xMax - 20f, rect.y, 20f, rect.height), "R", miniButtonStyle))
			{
				s.PushUndo();
				for (int j = 0; j < 3; j++)
				{
					set(j, def);
				}
			}
		}

		internal static void ToggleSelector(string key, Func<List<WardrobeItem>> listFunc)
		{
			GetList(key, listFunc);
			expanded.TryGetValue(key, out var value);
			value = !value;
			expanded[key] = value;
			if (value)
			{
				sortedCache.Remove(key);
				pendingScrollTo.Add(key);
			}
		}

		internal static bool SelectorOpen(string key)
		{
			expanded.TryGetValue(key, out var value);
			return value;
		}

		internal static void SelectorHeader(string label, string key, int curId, Func<List<WardrobeItem>> listFunc, float labelWidth = 105f)
		{
			EnsureStyles();
			List<WardrobeItem> list = GetList(key, listFunc);
			string text = "#" + curId;
			foreach (WardrobeItem item in list)
			{
				if (item.id == curId)
				{
					text = item.name;
					break;
				}
			}
			expanded.TryGetValue(key, out var value);
			GUILayout.BeginHorizontal();
			GUILayout.Label(WardrobeLang.T(label), miniLabelStyle, GUILayout.Width(labelWidth));
			if (GUILayout.Button((value ? "▼  " : "▶  ") + text, GUILayout.ExpandWidth(expand: true)))
			{
				value = !value;
				expanded[key] = value;
				if (value)
				{
					sortedCache.Remove(key);
					pendingScrollTo.Add(key);
				}
			}
			GUILayout.EndHorizontal();
		}

		internal static void SelectorBody(WardrobeSession s, string key, int curId, Action<int> onPick, bool thumbs, int prefCategory = -1, float visibleRows = 5f)
		{
			EnsureStyles();
			expanded.TryGetValue(key, out var value);
			if (!value || !listCache.TryGetValue(key, out var value2))
			{
				return;
			}
			if (!sortedCache.TryGetValue(key, out var value3))
			{
				value3 = BuildSortedList(value2, prefCategory);
				sortedCache[key] = value3;
			}
			searches.TryGetValue(key, out var value4);
			if (value4 == null)
			{
				value4 = "";
			}
			if (!appliedSearches.TryGetValue(key, out var value5))
			{
				value5 = value4;
				appliedSearches[key] = value5;
			}
			GUILayout.BeginHorizontal();
			GUILayout.Label(WardrobeLang.T("Search:"), GUILayout.Width(52f));
			value4 = GUILayout.TextField(value4, GUILayout.ExpandWidth(expand: true));
			searches[key] = value4;
			GUILayout.EndHorizontal();
			if (Event.current.type == EventType.Layout && !string.Equals(value5, value4, StringComparison.Ordinal))
			{
				value5 = value4;
				appliedSearches[key] = value5;
				scrolls[key] = Vector2.zero;
				pendingScrollTo.Remove(key);
			}
			if (!filteredCache.TryGetValue(key, out var value6))
			{
				value6 = new WardrobeSearchCache<WardrobeItem>((WardrobeItem it) => it.name);
				filteredCache[key] = value6;
			}
			List<WardrobeItem> items = value6.Get(value3, value5, Event.current.type == EventType.Layout);
			if (thumbs)
			{
				DrawThumbGrid(key, curId, items, onPick, prefCategory, visibleRows);
			}
			else
			{
				DrawNameList(key, curId, items, onPick);
			}
		}

		internal static void SelectorRow(WardrobeSession s, string label, string key, int curId, Func<List<WardrobeItem>> listFunc, Action<int> onPick, bool thumbs, int prefCategory = -1)
		{
			SelectorHeader(label, key, curId, listFunc);
			SelectorBody(s, key, curId, onPick, thumbs, prefCategory);
		}

		internal static void CurrentThumbBox(string key, int curId, Func<List<WardrobeItem>> listFunc, int prefCategory, float size = 96f)
		{
			EnsureStyles();
			List<WardrobeItem> list = GetList(key, listFunc);
			WardrobeItem wardrobeItem = null;
			foreach (WardrobeItem item in list)
			{
				if (item.id == curId)
				{
					wardrobeItem = item;
					break;
				}
			}
			Rect rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
			MarkRects(rect, out var favRect, out var disRect);
			int num;
			if (prefCategory >= 0 && curId != 0)
			{
				num = ((wardrobeItem != null) ? 1 : 0);
				if (num != 0)
				{
					StealMarkClicks(prefCategory, wardrobeItem.name, favRect, disRect);
				}
			}
			else
			{
				num = 0;
			}
			bool flag = DrawKoiThumbnailControl(rect, (wardrobeItem != null) ? GetThumb(wardrobeItem) : null);
			if (num != 0)
			{
				DrawMarks(WardrobePrefs.Get(prefCategory, wardrobeItem.name), favRect, disRect);
			}
			if (rect.Contains(Event.current.mousePosition))
			{
				Color color = GUI.color;
				GUI.color = new Color(0.45f, 0.8f, 1f, 0.9f);
				GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), whiteTex);
				GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), whiteTex);
				GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height), whiteTex);
				GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), whiteTex);
				GUI.color = color;
			}
			if (flag)
			{
				expanded.TryGetValue(key, out var value);
				value = !value;
				expanded[key] = value;
				if (value)
				{
					sortedCache.Remove(key);
					pendingScrollTo.Add(key);
				}
			}
		}

		private static bool DrawKoiThumbnailControl(Rect rect, Texture2D texture)
		{
			Color color = GUI.color;
			GUI.color = new Color(0.78f, 0.8f, 0.84f, 1f);
			GUI.DrawTexture(rect, whiteTex, ScaleMode.StretchToFill);
			Rect position = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);
			GUI.color = new Color(0.2f, 0.2f, 0.22f, 1f);
			GUI.DrawTexture(position, whiteTex, ScaleMode.StretchToFill);
			if (texture != null)
			{
				GUI.color = Color.white;
				GUI.DrawTexture(position, texture, ScaleMode.ScaleToFit, alphaBlend: true);
			}
			GUI.color = color;
			return GUI.Button(rect, GUIContent.none, GUIStyle.none);
		}

		private static void MarkRects(Rect thumbRect, out Rect favRect, out Rect disRect)
		{
			favRect = new Rect(thumbRect.x + 1f, thumbRect.y + 1f, 34f, 34f);
			disRect = new Rect(thumbRect.xMax - 34f - 1f, thumbRect.y + 1f, 34f, 34f);
		}

		private static void StealMarkClicks(int prefCategory, string name, Rect favRect, Rect disRect)
		{
			Event current = Event.current;
			if (current.type == EventType.MouseDown && current.button == 0)
			{
				Rect rect = new Rect(favRect.x + 7f, favRect.y + 7f, favRect.width - 14f, favRect.height - 14f);
				Rect rect2 = new Rect(disRect.x + 7f, disRect.y + 7f, disRect.width - 14f, disRect.height - 14f);
				if (rect.Contains(current.mousePosition))
				{
					WardrobePrefs.Toggle(prefCategory, name, 1);
					current.Use();
				}
				else if (rect2.Contains(current.mousePosition))
				{
					WardrobePrefs.Toggle(prefCategory, name, -1);
					current.Use();
				}
			}
		}

		private static void DrawMarks(int pref, Rect favRect, Rect disRect)
		{
			Color color = GUI.color;
			GUI.color = ((pref == 1) ? new Color(1f, 0.82f, 0.1f) : new Color(1f, 0.2f, 0.25f));
			GUI.Label(favRect, "♥", markStyle);
			GUI.color = ((pref == -1) ? new Color(1f, 0.82f, 0.1f) : new Color(0.35f, 0.6f, 1f));
			GUI.Label(disRect, "×", markStyle);
			GUI.color = color;
		}

		private static List<WardrobeItem> GetList(string key, Func<List<WardrobeItem>> listFunc)
		{
			if (!listCache.TryGetValue(key, out var value) || value == null)
			{
				try
				{
					value = listFunc();
				}
				catch (Exception ex)
				{
					WardrobePlugin.Log.LogError((object)("Catalog build failed (" + key + "): " + ex));
					value = new List<WardrobeItem>();
				}
				listCache[key] = value;
			}
			return value;
		}

		private static List<WardrobeItem> BuildSortedList(List<WardrobeItem> list, int prefCategory)
		{
			if (prefCategory < 0)
			{
				return list;
			}
			List<WardrobeItem> list2 = new List<WardrobeItem>();
			List<WardrobeItem> list3 = new List<WardrobeItem>();
			List<WardrobeItem> list4 = new List<WardrobeItem>();
			List<WardrobeItem> list5 = new List<WardrobeItem>();
			foreach (WardrobeItem item in list)
			{
				if (item.id == 0)
				{
					list2.Add(item);
					continue;
				}
				switch (WardrobePrefs.Get(prefCategory, item.name))
				{
				case 1:
					list3.Add(item);
					break;
				case -1:
					list5.Add(item);
					break;
				default:
					list4.Add(item);
					break;
				}
			}
			List<WardrobeItem> list6 = new List<WardrobeItem>(list.Count);
			list6.AddRange(list2);
			list6.AddRange(list3);
			list6.AddRange(list4);
			list6.AddRange(list5);
			return list6;
		}

		private static void DrawThumbGrid(string key, int curId, List<WardrobeItem> items, Action<int> onPick, int prefCategory, float visibleRows)
		{
			scrolls.TryGetValue(key, out var value);
			ThumbGridFrame thumbGridFrame;
			if (Event.current.type == EventType.Layout)
			{
				thumbGridFrame = PlanThumbGrid(items.Count, visibleRows, value.y);
				if (pendingScrollTo.Remove(key))
				{
					int num = items.FindIndex((WardrobeItem it) => it.id == curId);
					value.y = ((num >= 0) ? Mathf.Max(0f, (float)(num / 3) * thumbGridFrame.RowH - (thumbGridFrame.ViewH - thumbGridFrame.RowH) / 2f) : 0f);
				}
				value.y = Mathf.Clamp(value.y, 0f, Mathf.Max(0f, thumbGridFrame.ContentH - thumbGridFrame.ViewH));
				thumbGridFrame = PlanThumbGrid(items.Count, visibleRows, value.y);
				thumbGridFrames[key] = thumbGridFrame;
			}
			else if (!thumbGridFrames.TryGetValue(key, out thumbGridFrame))
			{
				return;
			}
			bool flag = Event.current.type == EventType.Repaint && value.y != thumbGridFrame.ScrollY;
			Vector2 scrollPosition = value;
			if (flag)
			{
				scrollPosition.y = thumbGridFrame.ScrollY;
			}
			Vector2 value2 = GUILayout.BeginScrollView(scrollPosition, GUI.skin.box, GUILayout.Height(thumbGridFrame.ViewH), GUILayout.ExpandWidth(expand: true));
			value2.x = 0f;
			if (thumbGridFrame.FirstRow > 0)
			{
				GUILayout.Space((float)thumbGridFrame.FirstRow * thumbGridFrame.RowH);
			}
			for (int num2 = thumbGridFrame.FirstRow; num2 < thumbGridFrame.LastRow; num2++)
			{
				Rect rect = GUILayoutUtility.GetRect(440f, thumbGridFrame.RowH, GUILayout.Width(440f));
				for (int num3 = 0; num3 < 3; num3++)
				{
					int num4 = num2 * 3 + num3;
					if (num4 >= items.Count)
					{
						break;
					}
					DrawGridCell(key, items[num4], curId, new Rect(rect.x + (float)num3 * thumbGridFrame.CellW, rect.y, thumbGridFrame.CellW, thumbGridFrame.RowH), thumbGridFrame.ThumbSize, onPick, prefCategory);
				}
			}
			if (thumbGridFrame.LastRow < thumbGridFrame.Rows)
			{
				GUILayout.Space((float)(thumbGridFrame.Rows - thumbGridFrame.LastRow) * thumbGridFrame.RowH);
			}
			GUILayout.EndScrollView();
			if (!flag)
			{
				scrolls[key] = value2;
			}
		}

		private static void DrawGridCell(string key, WardrobeItem it, int curId, Rect cell, float thumbS, Action<int> onPick, int prefCategory)
		{
			Rect rect = new Rect(cell.x + (cell.width - thumbS) / 2f, cell.y + 3f, thumbS, thumbS);
			Rect position = new Rect(cell.x + 2f, rect.yMax + 1f, cell.width - 4f, 18f);
			MarkRects(rect, out var favRect, out var disRect);
			bool flag = prefCategory >= 0 && it.id != 0;
			if (flag)
			{
				StealMarkClicks(prefCategory, it.name, favRect, disRect);
			}
			Event current = Event.current;
			if (current.type == EventType.MouseDown && current.button == 1 && rect.Contains(current.mousePosition))
			{
				expanded[key] = false;
				current.Use();
				onPick(it.id);
				return;
			}
			if (it.id == curId)
			{
				Color color = GUI.color;
				GUI.color = Color.cyan;
				GUI.DrawTexture(new Rect(rect.x - 3f, rect.y - 3f, rect.width + 6f, rect.height + 6f), whiteTex);
				GUI.color = color;
			}
			Texture2D thumbBudgeted = GetThumbBudgeted(it);
			if (DrawKoiThumbnailControl(rect, thumbBudgeted))
			{
				onPick(it.id);
			}
			if (flag)
			{
				DrawMarks(WardrobePrefs.Get(prefCategory, it.name), favRect, disRect);
			}
			string text = it.name ?? "";
			if (text.Length > 26)
			{
				text = text.Substring(0, 25) + "…";
			}
			GUI.Label(position, text, nameStyle);
		}

		private static void DrawNameList(string key, int curId, List<WardrobeItem> items, Action<int> onPick)
		{
			float num = Mathf.Min((float)items.Count * 26f + 8f, 300f);
			scrolls.TryGetValue(key, out var value);
			if (pendingScrollTo.Remove(key))
			{
				int num2 = items.FindIndex((WardrobeItem it) => it.id == curId);
				value.y = ((num2 >= 0) ? Mathf.Max(0f, (float)num2 * 26f - (num - 26f) / 2f) : 0f);
			}
			int num3 = Mathf.Max(0, (int)(value.y / 26f) - 1);
			int num4 = (int)(num / 26f) + 3;
			int num5 = Mathf.Min(items.Count, num3 + num4);
			Vector2 value2 = GUILayout.BeginScrollView(value, GUI.skin.box, GUILayout.Height(num));
			if (num3 > 0)
			{
				GUILayout.Space((float)num3 * 26f);
			}
			for (int num6 = num3; num6 < num5; num6++)
			{
				WardrobeItem wardrobeItem = items[num6];
				GUILayout.BeginHorizontal(GUILayout.Height(22f));
				Color color = GUI.color;
				if (wardrobeItem.id == curId)
				{
					GUI.color = Color.cyan;
				}
				if (GUILayout.Button(wardrobeItem.name, GUILayout.ExpandWidth(expand: true), GUILayout.Height(20f)))
				{
					GUI.color = color;
					expanded[key] = false;
					if (wardrobeItem.id != curId)
					{
						onPick(wardrobeItem.id);
					}
					GUILayout.EndHorizontal();
					GUILayout.EndScrollView();
					return;
				}
				GUI.color = color;
				GUILayout.EndHorizontal();
			}
			if (num5 < items.Count)
			{
				GUILayout.Space((float)(items.Count - num5) * 26f);
			}
			GUILayout.EndScrollView();
			scrolls[key] = value2;
		}

		internal static Texture2D ItemThumb(int category, int id, bool budgeted = false)
		{
			if (id == 0 && !WardrobeCompositeTopKoi.IsPieceCategory(category))
			{
				return null;
			}
			if (!catalogById.TryGetValue(category, out var value))
			{
				value = new Dictionary<int, WardrobeItem>();
				try
				{
					foreach (WardrobeItem item in WardrobeCatalog.List(category))
					{
						value[item.id] = item;
					}
				}
				catch (Exception ex)
				{
					WardrobePlugin.Log.LogWarning((object)$"ItemThumb catalog {category} failed: {ex.Message}");
				}
				catalogById[category] = value;
			}
			if (!value.TryGetValue(id, out var value2))
			{
				return null;
			}
			Texture2D texture2D = (budgeted ? GetThumbBudgeted(value2) : GetThumb(value2));
			if (!(texture2D != null) || !(texture2D != Texture2D.blackTexture))
			{
				return null;
			}
			return texture2D;
		}

		private static Texture2D GetThumb(WardrobeItem info)
		{
			if (info.assetBundle == null || info.assetName == null)
			{
				return Texture2D.blackTexture;
			}
			string text = info.assetBundle + "+" + info.assetName;
			if (!thumbCache.TryGetValue(text, out var value) || value == null)
			{
				try
				{
					value = CommonLib.LoadAsset<Texture2D>(info.assetBundle, info.assetName);
				}
				catch
				{
					value = null;
				}
				if (value == null)
				{
					if (thumbFailLogged.Add(text))
					{
						WardrobePlugin.Log.LogWarning((object)$"[Thumb] load failed: {info.name} (#{info.id}) bundle={info.assetBundle} asset={info.assetName}");
					}
					value = Texture2D.blackTexture;
				}
				thumbCache[text] = value;
			}
			return value;
		}

		private static Texture2D GetThumbBudgeted(WardrobeItem info)
		{
			if (info.assetBundle == null || info.assetName == null)
			{
				return Texture2D.blackTexture;
			}
			string key = info.assetBundle + "+" + info.assetName;
			if (thumbCache.TryGetValue(key, out var value) && value != null)
			{
				return value;
			}
			if (thumbBudgetFrame != Time.frameCount)
			{
				thumbBudgetFrame = Time.frameCount;
				thumbBudget = 4;
			}
			if (thumbBudget <= 0)
			{
				return null;
			}
			thumbBudget--;
			return GetThumb(info);
		}
	}
}
