using System;
using System.IO;
using BepInEx;
using ExtensibleSaveFormat;
using MessagePack;
using UnityEngine;

namespace MainGameHCharacterMaker
{
    public sealed partial class Plugin
    {
        private bool _saving;
        private bool CanSaveCard => !_saving && _visible && CanEdit && _selected >= 0 && _selected < _actors.Count
            && _edits.TryGetValue(_selected, out var shape) && shape.CanCommit
            && _appearanceEdits.TryGetValue(_selected, out var appearance) && appearance.CanCommit;

        private void DrawCardSave()
        {
            GUILayout.BeginHorizontal();
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && CanSaveCard;
            if (GUILayout.Button("選択キャラをfreeHへ保存", GUILayout.Width(210), GUILayout.Height(30)))
                _pendingUi = SaveSelectedCard;
            GUI.enabled = enabled;
            GUILayout.Label("PNGカードを新規作成。元カードは上書きしません。",
                new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.EndHorizontal();
        }
        private void SaveSelectedCard()
        {
            if (!CanSaveCard) { _status = "保存できる編集対象がありません。"; return; }
            var actor = _actors[_selected];
            var file = actor.chaFile;
            var shape = _edits[_selected];
            var appearance = _appearanceEdits[_selected];
            var permit = _permit;
            ulong session = _lastSession;
            Func<bool> valid = () => _active && _visible && isActiveAndEnabled && permit.IsValid && session == _lastSession
                && actor != null && ReferenceEquals(actor.chaFile, file) && shape.CanCommit && appearance.CanCommit;
            var coordinates = file.coordinate;
            byte[] originalPng = file.pngData;
            var language = file.about.language;
            string userId = file.about.userID, dataId = file.about.dataID;
            _saving = true;
            try
            {
                int coordinate = actor.fileStatus.coordinateType;
                if (actor.nowCoordinate == null || coordinates == null || coordinate < 0 || coordinate >= coordinates.Length)
                    throw new InvalidOperationException("現在の服装・化粧を保存できません。");
                // Serialize the active outfit/makeup without mutating the stored outfit or H state.
                var staged = (ChaFileCoordinate[])coordinates.Clone();
                staged[coordinate] = actor.nowCoordinate;
                file.coordinate = staged;
                file.pngData = CardPng(file);
                file.about.userID = Manager.GameSystem.UserUUID;
                file.about.dataID = YS_Assist.CreateUUID();
                string directory = CardSaveIo.DirectoryFor(Paths.GameRootPath, actor.sex);
                string name = CardSaveIo.FileName(file.parameter.fullname, DateTime.UtcNow, System.Guid.NewGuid().ToString("N").Substring(0,8));
                string saved = CardSaveIo.SaveNew(directory, name, valid, writer =>
                {
                    // Save the live ChaFile through the native pipeline so KKAPI can locate this actor
                    // and run every registered OnCardBeingSaved controller.
                    if (!file.SaveCharaFile(writer, true)) throw new IOException("ゲームがカード保存を拒否しました。");
                }, path => ValidateSavedCard(path, file));
                _status = "カード保存済み：" + saved + "（窓の取消では保存済みカードは消えません）";
            }
            catch (Exception ex) { _status = "カード保存失敗：" + ex.Message; }
            finally
            {
                file.coordinate = coordinates;
                file.pngData = originalPng;
                file.about.language = language;
                file.about.userID = userId;
                file.about.dataID = dataId;
                _saving = false;
            }
        }
        private static bool IsPng(byte[] data) => data != null && data.Length >= 8
            && data[0] == 137 && data[1] == 80 && data[2] == 78 && data[3] == 71
            && data[4] == 13 && data[5] == 10 && data[6] == 26 && data[7] == 10;
        private static byte[] CardPng(ChaFile file)
        {
            if (IsPng(file.pngData)) return file.pngData;
            if (IsPng(file.facePngData)) return file.facePngData;
            using (var input = typeof(Plugin).Assembly.GetManifestResourceStream("MainGameHCharacterMaker.CardPlaceholder.png"))
            using (var output = new MemoryStream())
            {
                if (input == null) throw new IOException("カードの共通画像がありません。");
                input.CopyTo(output); return output.ToArray();
            }
        }
        private static void ValidateSavedCard(string path, ChaFile file)
        {
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                if (!IsPng(reader.ReadBytes(8))) throw new InvalidDataException("保存結果がPNGではありません。");
                reader.BaseStream.Position = 0;
                long pngLength = PngFile.GetPngSize(reader);
                if (pngLength <= 8 || pngLength >= reader.BaseStream.Length) throw new InvalidDataException("カード画像が不完全です。");
                reader.BaseStream.Position = pngLength;
                reader.ReadInt32();
                if (reader.ReadString() != "【KoiKatuCharaSun】") throw new InvalidDataException("キャラカード形式が不正です。");
                reader.ReadString();
                int faceLength = reader.ReadInt32();
                if (faceLength < 0 || faceLength > reader.BaseStream.Length - reader.BaseStream.Position)
                    throw new InvalidDataException("顔画像データが不完全です。");
                reader.BaseStream.Position += faceLength;
                int headerLength = reader.ReadInt32();
                if (headerLength <= 0 || headerLength > reader.BaseStream.Length - reader.BaseStream.Position)
                    throw new InvalidDataException("カードの目次が不完全です。");
                var header = MessagePackSerializer.Deserialize<BlockHeader>(reader.ReadBytes(headerLength));
                long bodyLength = reader.ReadInt64();
                long start = reader.BaseStream.Position;
                if (bodyLength <= 0 || bodyLength > reader.BaseStream.Length - start)
                    throw new InvalidDataException("カードの本体が不完全です。");
                foreach (string block in new[] { ChaFileCustom.BlockName, ChaFileCoordinate.BlockName, ChaFileParameter.BlockName,
                    ChaFileStatus.BlockName, ChaFileAbout.BlockName })
                {
                    var info = header.SearchInfo(block);
                    if (info == null || info.pos < 0 || info.size <= 0 || info.pos > bodyLength || info.size > bodyLength - info.pos)
                        throw new InvalidDataException("カードの必須データが不完全です：" + block);
                }
                var expected = ExtendedSave.GetAllExtendedData(file);
                if (expected == null || expected.Count == 0) return;
                var extended = header.SearchInfo(ExtendedSave.Marker);
                if (extended == null || extended.pos < 0 || extended.size <= 0 || extended.pos > bodyLength || extended.size > bodyLength - extended.pos)
                    throw new InvalidDataException("MODの追加データが保存されていません。");
                reader.BaseStream.Position = start + extended.pos;
                var saved = MessagePackSerializer.Deserialize<System.Collections.Generic.Dictionary<string, PluginData>>(
                    reader.ReadBytes(checked((int)extended.size)));
                foreach (var item in expected)
                    if (item.Value != null && (saved == null || !saved.TryGetValue(item.Key, out var data) || data == null))
                        throw new InvalidDataException("MODの追加データが欠けています：" + item.Key);
            }
        }
    }
}
