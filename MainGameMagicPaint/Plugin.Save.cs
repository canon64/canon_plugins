using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using UnityEngine;
namespace MainGameMagicPaint
{
    public sealed partial class Plugin
    {
        [Serializable] sealed class SaveInfo { public int Version = 1; public int FaceSize = 1024, BodySize = 2048; public string Character; }
        CancellationTokenSource _io;
        bool _busy;
        string[] _saves = new string[0];
        string[] _drawSaves = new string[0];
        int _saveIndex;
        Vector2 _saveScroll;
        string SaveDirectory => Path.Combine(Paths.GameRootPath, "UserData", "MagicPaint");
        void RefreshSaves()
        {
            _saves = Directory.Exists(SaveDirectory) ? Directory.GetDirectories(SaveDirectory).Where(d => File.Exists(Path.Combine(d, "paint.json"))).Select(Path.GetFileName).OrderBy(s => s).ToArray() : new string[0];
            _saveIndex = Mathf.Clamp(_saveIndex, 0, Mathf.Max(0, _saves.Length - 1));
        }
        void DrawSaveControls()
        {
            GUILayout.BeginVertical(GUI.skin.box); GUILayout.Label("落書きの保存・読込");
            bool old = GUI.enabled; GUI.enabled = old && !_busy && Selected != null;
            GUILayout.BeginHorizontal(); GUILayout.Label("保存名", GUILayout.Width(60)); GUI.SetNextControlName("MagicPaintSaveName"); _saveName = GUILayout.TextField(_saveName, GUILayout.Width(220));
            if (GUILayout.Button("新規保存", GUILayout.Width(90))) SaveDrawing(); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("前へ", GUILayout.Width(60)) && _saves.Length > 0) _saveIndex = (_saveIndex + _saves.Length - 1) % _saves.Length;
            GUILayout.Label(_saves.Length > 0 ? _saves[_saveIndex] : "保存なし", GUILayout.Width(230));
            if (GUILayout.Button("次へ", GUILayout.Width(60)) && _saves.Length > 0) _saveIndex = (_saveIndex + 1) % _saves.Length;
            GUILayout.EndHorizontal();
            if (_drawSaves.Length > 0)
            {
                _saveScroll = GUILayout.BeginScrollView(_saveScroll, GUILayout.Width(390), GUILayout.Height(Mathf.Min(160, _drawSaves.Length * 25 + 12)));
                GUI.SetNextControlName("MagicPaintSaveList"); _saveIndex = GUILayout.SelectionGrid(Mathf.Clamp(_saveIndex,0,_drawSaves.Length-1), _drawSaves, 1, GUILayout.Width(355));
                GUILayout.EndScrollView();
            }
            GUILayout.BeginHorizontal(); if (GUILayout.Button("選択を読込", GUILayout.Width(110)) && _saves.Length > 0) LoadDrawing(_saves[_saveIndex]);
            if (GUILayout.Button("一覧を更新", GUILayout.Width(100))) RefreshSaves(); GUILayout.EndHorizontal();
            GUI.enabled = old; GUILayout.Label(_busy ? "保存・読込処理中…" : "保存先：" + SaveDirectory); GUILayout.EndVertical();
        }
        string SafeSavePath(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name == "." || name == ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Path.GetFileName(name) != name)
                throw new ArgumentException("保存名に使えない文字があります");
            return Path.Combine(SaveDirectory, name);
        }
        byte[] Encode(PaintBuffer buffer)
        {
            if (buffer == null) return null;
            var texture = new Texture2D(buffer.Size, buffer.Size, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[buffer.Pixels.Length];
                for (int i = 0; i < pixels.Length; i++) { uint p = buffer.Pixels[i]; pixels[i] = new Color32((byte)p, (byte)(p >> 8), (byte)(p >> 16), (byte)(p >> 24)); }
                texture.SetPixels32(pixels); texture.Apply(false); return texture.EncodeToPNG();
            }
            finally { Destroy(texture); }
        }
        async void SaveDrawing()
        {
            if (!CanAct || _busy || Selected == null) return; _trigger = false; _mouseDown = false; EndStroke(); _busy = true;
            var permit = _permit; ulong session = _lastSession; var token = _io.Token;
            try
            {
                string destination = SafeSavePath(_saveName); if (Directory.Exists(destination)) throw new IOException("同名の保存があります。別の保存名を指定してください。");
                byte[] face = Encode(Selected.FaceBuffer), body = Encode(Selected.BodyBuffer);
                string json = JsonUtility.ToJson(new SaveInfo { Character = Selected.Actor != null ? Selected.Actor.chaFile.parameter.fullname : "" }, true);
                await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested(); Directory.CreateDirectory(SaveDirectory);
                    string temporary = destination + ".writing-" + System.Guid.NewGuid().ToString("N"); Directory.CreateDirectory(temporary);
                    try
                    {
                        if (face != null) File.WriteAllBytes(Path.Combine(temporary, "face.png"), face);
                        if (body != null) File.WriteAllBytes(Path.Combine(temporary, "body.png"), body);
                        File.WriteAllText(Path.Combine(temporary, "paint.json"), json, new System.Text.UTF8Encoding(false));
                        token.ThrowIfCancellationRequested(); Directory.Move(temporary, destination);
                    }
                    finally
                    {
                        if (Directory.Exists(temporary)) { foreach (string f in Directory.GetFiles(temporary)) File.Delete(f); Directory.Delete(temporary); }
                    }
                }, token);
                if (!CanAct || !permit.IsValid || session != _lastSession) return;
                RefreshSaves(); _status = "落書きを保存しました。";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (CanAct && session == _lastSession) Fail("保存失敗", ex); }
            finally { if (session == _lastSession) _busy = false; }
        }
        void CaptureDrawingImages()
        {
            // User-triggered diagnostic capture only; OFF does no collection or file work.
            if (!_logging.Value || !CanAct || _busy || Selected == null) return;
            try
            {
                var actor = Selected;
                string directory = Path.Combine(SaveDirectory,"Diagnostics",DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
                Directory.CreateDirectory(directory); var report = new System.Text.StringBuilder();
                report.AppendLine("CPU ink -> GPU ink -> composed skin -> assigned _MainTex");
                CaptureActorImages(actor,directory,report);
                File.WriteAllText(Path.Combine(directory,"state.txt"),report.ToString(),new System.Text.UTF8Encoding(false));
                _status = "確認用画像を保存："+directory;
            }
            catch(Exception ex) { Fail("確認画像の保存失敗",ex); }
        }
        static byte[] ReadPng(string path)
        { if (!File.Exists(path)) return null; if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new IOException("画像が大きすぎます"); return File.ReadAllBytes(path); }
        uint[] Decode(byte[] png, int size)
        {
            if (png == null) return new uint[size * size];
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(png, false) || texture.width != size || texture.height != size) throw new InvalidDataException("落書き画像の解像度が異なります");
                var colors = texture.GetPixels32(); var result = new uint[colors.Length];
                for (int i = 0; i < result.Length; i++) result[i] = PaintBuffer.Pack(colors[i].r, colors[i].g, colors[i].b, colors[i].a); return result;
            }
            finally { Destroy(texture); }
        }
        async void LoadDrawing(string name)
        {
            if (!CanAct || _busy || Selected == null) return; _trigger = false; _mouseDown = false; EndStroke(); _busy = true;
            var actor = Selected; var permit = _permit; ulong session = _lastSession; var token = _io.Token;
            try
            {
                string directory = SafeSavePath(name);
                var data = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested(); var json = File.ReadAllText(Path.Combine(directory, "paint.json"));
                    if (json.Length > 65536) throw new InvalidDataException("保存情報が大きすぎます");
                    return Tuple.Create(json, ReadPng(Path.Combine(directory, "face.png")), ReadPng(Path.Combine(directory, "body.png")));
                }, token);
                if (!CanAct || !permit.IsValid || session != _lastSession || token.IsCancellationRequested || actor.Actor == null) return;
                var info = JsonUtility.FromJson<SaveInfo>(data.Item1);
                if (info == null || info.Version != 1 || info.FaceSize != 1024 || info.BodySize != 2048) throw new InvalidDataException("対応していない保存形式です");
                var face = Decode(data.Item2, 1024); var body = Decode(data.Item3, 2048);
                EndStroke();
                if (actor.FaceBuffer == null) actor.FaceBuffer = new PaintBuffer(1024); if (actor.BodyBuffer == null) actor.BodyBuffer = new PaintBuffer(2048);
                actor.FaceBuffer.Replace(face); actor.BodyBuffer.Replace(body);
                foreach (var probe in actor.Probes) if (actor.IsSkin(probe)) actor.Surface(probe);
                actor.Commit(); actor.Flush(); _status = "「" + name + "」を読み込みました。UNDOで戻せます。";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (CanAct && session == _lastSession) Fail("読込失敗", ex); }
            finally { if (session == _lastSession) _busy = false; }
        }
    }
}
