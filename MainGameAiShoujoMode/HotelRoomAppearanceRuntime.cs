using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using ExtensibleSaveFormat;
using MessagePack;

namespace MainGameAiShoujoMode
{
    internal static class HotelRoomAppearanceRuntime
    {
        internal static ChaFileControl Decode(string encoded)
        {
            var card = new ChaFileControl();
            var bytes = Convert.FromBase64String(encoded);
            var expected = ReadExtendedData(bytes);
            var eventsEnabled = ExtendedSave.LoadEventsEnabled;
            var wasMod = Manager.Character.isMod;
            try
            {
                // Card-list enumeration can suppress ExtendedSave globally. This
                // synchronous owned read must resolve MOD IDs, then restore the flag.
                ExtendedSave.LoadEventsEnabled = true;
                using (var stream = new MemoryStream(bytes))
                    if (!card.LoadCharaFile(stream, true, false)) throw new InvalidDataException("保存したキャラ状態を読み込めません。");
                var loaded = ExtendedSave.GetAllExtendedData(card);
                if (expected != null && expected.Keys.Any(key => loaded == null || !loaded.ContainsKey(key)))
                    throw new InvalidDataException("保存キャラのMOD情報が復元されませんでした。保存データと元カードは保持しています。");
            }
            finally { ExtendedSave.LoadEventsEnabled = eventsEnabled; Manager.Character.isMod = wasMod; }
            return card;
        }
        private static Dictionary<string, PluginData> ReadExtendedData(byte[] bytes)
            => MessagePackSerializer.Deserialize<Dictionary<string, PluginData>>(ReadSnapshotBlock(bytes, ExtendedSave.Marker));
        private static byte[] ReadSnapshotBlock(byte[] bytes, string block)
        {
            using (var reader = new BinaryReader(new MemoryStream(bytes)))
            {
                reader.ReadInt32();
                if (reader.ReadString() != "【KoiKatuCharaSun】") throw new InvalidDataException("客室の保存キャラ形式が不正です。");
                reader.ReadString();
                var face = reader.ReadInt32(); reader.ReadBytes(face);
                var header = MessagePackSerializer.Deserialize<BlockHeader>(reader.ReadBytes(reader.ReadInt32()));
                reader.ReadInt64();
                var start = reader.BaseStream.Position;
                var info = header.SearchInfo(block);
                if (info == null) throw new InvalidDataException("保存キャラの拡張情報がありません。元カードと保存ファイルは保持しています。");
                reader.BaseStream.Position = start + info.pos;
                return reader.ReadBytes(checked((int)info.size));
            }
        }
        internal static byte[] ReadStatusBytes(string encoded)
            => ReadSnapshotBlock(Convert.FromBase64String(encoded), ChaFileStatus.BlockName);
        internal static void RestoreCardStatus(ChaControl actor, string encoded)
        {
            ApplyStatus(actor, encoded, null);
        }
        private static void ApplyStatus(ChaControl actor, string encoded, Action<string> diagnostic)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (string.IsNullOrWhiteSpace(encoded)) return;
            TraceStatus(diagnostic, "before", actor);
            // Deserialize into a detached status. Never round-trip it through GetStatusBytes:
            // MoreAccessories trims that representation to twenty slots and its Copy prefix
            // resizes the destination to the source length, breaking UpdateVisible's loop.
            var source = MessagePackSerializer.Deserialize<ChaFileStatus>(ReadStatusBytes(encoded));
            ApplyDetachedStatus(actor, source);
            TraceStatus(diagnostic, "after", actor);
        }
        private static void ApplyDetachedStatus(ChaControl actor, ChaFileStatus source, bool restoreCoordinate = false)
        {
            if (restoreCoordinate)
            {
                // Native synchronous clothing reload must finish before merging status;
                // Merge deliberately preserves the identity of the loaded coordinate.
                HotelRoomCoordinateRestore.Apply(source.coordinateType, actor.chaFile.coordinate.Length,
                    () => actor.fileStatus.coordinateType,
                    () => actor.ChangeCoordinateTypeAndReload((ChaFileDefine.CoordinateType)source.coordinateType));
            }
            var status = CharacterStatusRestore.Merge(actor.fileStatus, source, ChaFileDefine.tearsFlags.GetLength(default(int)));
            int slots = actor.nowCoordinate.accessory.parts.Length;
            if (actor.fileStatus.showAccessory.Length != slots || actor.objAccessory.Length != slots)
                throw new InvalidDataException("読み込み済みキャラのアクセサリ構成と表示配列の数が一致しません。");
            for (int index = default(int); index < status.clothesState.Length; ++index) actor.SetClothesState(index, status.clothesState[index], true);
            for (int index = default(int); index < status.showAccessory.Length; ++index) actor.SetAccessoryState(index, status.showAccessory[index]);
            // SetClothesState may resolve linked top/bottom clothing states. Keep its result.
            Array.Copy(actor.fileStatus.clothesState, status.clothesState, status.clothesState.Length);
            actor.fileStatus.Copy(status);
            // Native UpdateSiru compares against a separate desired-level buffer.
            // Restoring only fileStatus would be overwritten by that buffer next frame.
            foreach (ChaFileDefine.SiruParts part in Enum.GetValues(typeof(ChaFileDefine.SiruParts)))
                actor.SetSiruFlags(part, status.siruLv[(int)part]);
            actor.ChangeSettingSkinGlossPower();
            actor.fileStatus.shoesType = status.shoesType;
            actor.ChangeEyebrowPtn(status.eyebrowPtn); actor.ChangeEyesPtn(status.eyesPtn); actor.ChangeMouthPtn(status.mouthPtn);
            actor.ChangeEyebrowOpenMax(status.eyebrowOpenMax); actor.ChangeEyesOpenMax(status.eyesOpenMax); actor.ChangeMouthOpenMax(status.mouthOpenMax);
            actor.ChangeEyesBlinkFlag(status.eyesBlink); actor.ChangeHohoAkaRate(status.hohoAkaRate); actor.HideEyeHighlight(status.hideEyesHighlight);
            actor.ChangeLookEyesPtn(status.eyesLookPtn); actor.ChangeLookNeckPtn(status.neckLookPtn);
        }
        private static void TraceStatus(Action<string> diagnostic, string stage, ChaControl actor)
        {
            if (diagnostic == null) return;
            diagnostic("HOTEL_ROOM_STATUS stage=" + stage + " sex=" + actor.sex
                + " coordinate=" + actor.fileStatus.coordinateType
                + " parts=" + actor.nowCoordinate.accessory.parts.Length
                + " objects=" + actor.objAccessory.Length + " visibility=" + actor.fileStatus.showAccessory.Length
                + " clothes=" + actor.fileStatus.clothesState.Length + " tears=" + actor.fileStatus.tearsLv);
        }
        internal static ChaFileControl CloneCard(ChaFile source)
        {
            var card = new ChaFileControl(); card.CopyAll(source);
            var extended = ExtendedSave.GetAllExtendedData(source);
            if (extended != null)
                foreach (var item in MessagePackSerializer.Deserialize<Dictionary<string, PluginData>>(MessagePackSerializer.Serialize(extended)))
                    ExtendedSave.SetExtendedDataById(card, item.Key, item.Value);
            return card;
        }
        internal static string Encode(ChaControl actor)
        {
            if (actor == null) return null;
            if (!actor.AssignCoordinate((ChaFileDefine.CoordinateType)actor.fileStatus.coordinateType)) throw new InvalidOperationException("現在の服装を保存できません。");
            using (var stream = new MemoryStream())
            {
                if (!actor.chaFile.SaveCharaFile(stream, false)) throw new InvalidDataException("現在のキャラ状態を保存できません。");
                var bytes = stream.ToArray();
                var saved = ReadExtendedData(bytes);
                var current = ExtendedSave.GetAllExtendedData(actor.chaFile);
                if (current != null && current.Any(item => item.Value != null && (saved == null || !saved.ContainsKey(item.Key))))
                    throw new InvalidDataException("キャラのMOD情報が保存結果から欠けています。以前の保存データを保持します。");
                return Convert.ToBase64String(bytes);
            }
        }
        internal static HotelRoomAppearance Capture(AiModeHSession session, string source, string[] preferences)
        {
            RequireOwnedRoom(session);
            var actors = session.RoomActors();
            return new HotelRoomAppearance
            {
                format = "resolved-v2", source = source, female = Encode(actors.First(a => a.sex != default(byte))), male = Encode(actors.FirstOrDefault(a => a.sex == default(byte))),
                preferences = (preferences ?? Array.Empty<string>()).Select(key => typeof(HFlag).GetField(key, BindingFlags.Public | BindingFlags.Instance))
                    .Where(field => field != null && (field.FieldType.IsPrimitive || field.FieldType.IsEnum))
                    .Select(field => new HotelRoomPreference { key = field.Name, value = Convert.ToString(field.GetValue(session.RoomFlags), CultureInfo.InvariantCulture) }).ToArray()
            };
        }
        internal static void Restore(AiModeHSession session, HotelRoomAppearance saved, string[] preferences, Action<string> diagnostic = null)
        {
            RequireOwnedRoom(session);
            if (saved == null) return;
            foreach (var actor in session.RoomActors())
            {
                string encoded = actor.sex == default(byte) ? saved.male : saved.female;
                if (string.IsNullOrWhiteSpace(encoded)) continue;
                // Only status is needed here. A second full card load reruns MOD
                // resolution and must not replace the correctly loaded actor's card.
                ApplyStatus(actor, encoded, diagnostic);
            }
            foreach (var value in saved.preferences ?? Array.Empty<HotelRoomPreference>())
            {
                if (!(preferences ?? Array.Empty<string>()).Contains(value.key)) continue;
                var field = typeof(HFlag).GetField(value.key, BindingFlags.Public | BindingFlags.Instance);
                if (field == null || !field.FieldType.IsPrimitive) continue;
                field.SetValue(session.RoomFlags, Convert.ChangeType(value.value, field.FieldType, CultureInfo.InvariantCulture));
            }
        }
        internal static void RestoreIndependent(AiModeHSession session, HotelRoomAppearance saved, string[] preferences, HotelRoomRestoreBatch batch)
        {
            foreach (bool female in new[] { true, false })
            {
                ChaControl actor = null;
                string key = female ? "appearance.female" : "appearance.male";
                batch.Item(female ? "女性衣装・外見" : "男性衣装・外見", key, female ? "female.ChaControl" : "male.ChaControl", () =>
                {
                    RequireOwnedRoom(session);
                    actor = session.RoomActors().FirstOrDefault(a => (a.sex != default(byte)) == female);
                    if (actor == null) throw new InvalidOperationException("適用対象のキャラがありません。");
                    string encoded = female ? saved?.female : saved?.male;
                    if (string.IsNullOrWhiteSpace(encoded)) throw new InvalidDataException("保存カードが欠損しています。");
                    return MessagePackSerializer.Deserialize<ChaFileStatus>(ReadStatusBytes(encoded));
                }, status => ApplyDetachedStatus(actor, status, true),
                status => actor.fileStatus.coordinateType == status.coordinateType && actor.fileStatus.clothesState.SequenceEqual(status.clothesState)
                    && actor.fileStatus.tearsLv == status.tearsLv && actor.fileStatus.tongueState == status.tongueState,
                StatusSummary, () => StatusSummary(actor.fileStatus), () =>
                {
                    var before = CharacterStatusRestore.Merge(actor.fileStatus, actor.fileStatus, ChaFileDefine.tearsFlags.GetLength(default(int)));
                    return () => ApplyDetachedStatus(actor, before, true);
                });
            }
            var recorded = saved?.preferences ?? Array.Empty<HotelRoomPreference>();
            foreach (var missing in recorded.Where(p => p == null)) batch.Failure("H設定", "appearance.preferences", new InvalidDataException("保存設定の項目がnullです。"));
            foreach (string key in (preferences ?? Array.Empty<string>()).Union(recorded.Where(p => p != null).Select(p => p.key)))
            {
                FieldInfo field = null;
                batch.Item("H設定:" + key, "appearance.preferences[key=" + key + "]", "HFlag." + key, () =>
                {
                    if (!(preferences ?? Array.Empty<string>()).Contains(key)) throw new InvalidDataException("現在の保存対象として未対応のキーです。");
                    var matches = recorded.Where(p => p != null && p.key == key).ToArray();
                    if (matches.Length != Convert.ToInt32(true)) throw new InvalidDataException("保存キー欠損または重複：" + matches.Length);
                    field = typeof(HFlag).GetField(key, BindingFlags.Public | BindingFlags.Instance);
                    if (field == null || !(field.FieldType.IsPrimitive || field.FieldType.IsEnum)) throw new InvalidDataException("HFlagに対応フィールドがありません。");
                    return field.FieldType.IsEnum ? Enum.Parse(field.FieldType, matches.Single().value) : Convert.ChangeType(matches.Single().value, field.FieldType, CultureInfo.InvariantCulture);
                }, value => field.SetValue(session.RoomFlags, value), value => Equals(field.GetValue(session.RoomFlags), value),
                value => Convert.ToString(value, CultureInfo.InvariantCulture), () => Convert.ToString(field.GetValue(session.RoomFlags), CultureInfo.InvariantCulture), () =>
                {
                    object before = field.GetValue(session.RoomFlags); return () => field.SetValue(session.RoomFlags, before);
                });
            }
        }
        internal static string StatusSummary(ChaFileStatus status) => "coordinate=" + status.coordinateType + " clothes=[" + string.Join(",", status.clothesState)
            + "] tears=" + status.tearsLv + " tongue=" + status.tongueState;
        private static void RequireOwnedRoom(AiModeHSession session)
        {
            if (session == null || !session.AllRoomSelections || !session.RoomReady)
                throw new InvalidOperationException("客室の保存・復元はホテル客室セッション内でのみ実行できます。");
        }
    }
}
