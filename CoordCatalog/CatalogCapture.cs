using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using ChaCustom;
using UnityEngine;

namespace CoordCatalog
{
    public class CatalogCapture : MonoBehaviour
    {
        private static readonly string[] CoordTypeNames = { "Plain", "Swim", "Pajamas", "Bathing" };

        private ManualLogSource _log;
        private string _pluginDir;

        public bool IsRunning { get; private set; }

        public void Init(CoordCatalogPlugin plugin)
        {
            _log = BepInEx.Logging.Logger.CreateLogSource("CoordCatalog");
            _pluginDir = CoordCatalogPlugin.PluginDir;
        }

        private void Log(string msg)
        {
            if (!CoordCatalogPlugin.LoggingEnabled) return;
            _log.LogInfo(msg);
            CoordCatalogPlugin.FileLog?.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
        }

        private void LogWarn(string msg)
        {
            _log.LogWarning(msg);
            CoordCatalogPlugin.FileLog?.WriteLine($"[{DateTime.Now:HH:mm:ss}] WARN: {msg}");
        }

        private void LogError(string msg)
        {
            _log.LogError(msg);
            CoordCatalogPlugin.FileLog?.WriteLine($"[{DateTime.Now:HH:mm:ss}] ERROR: {msg}");
        }

        // slot 0-3: CoordTypeNames、4以降: MoreOutfitsController から取得
        private string GetDisplayName(ChaControl chaCtrl, int index)
        {
            if (index < CoordTypeNames.Length) return CoordTypeNames[index];
            string moName = TryGetMoreOutfitsName(chaCtrl, index);
            if (!string.IsNullOrEmpty(moName)) return moName;
            return index.ToString();
        }

        // KKS_MoreOutfits の MoreOutfitsController.GetCoodinateName(index) をリフレクションで呼ぶ
        private string TryGetMoreOutfitsName(ChaControl chaCtrl, int index)
        {
            try
            {
                var controller = chaCtrl.gameObject.GetComponents<MonoBehaviour>()
                    .FirstOrDefault(c => c.GetType().Name == "MoreOutfitsController");
                if (controller == null) return null;
                var method = controller.GetType().GetMethod("GetCoodinateName",
                    BindingFlags.Public | BindingFlags.Instance);
                if (method == null) return null;
                return method.Invoke(controller, new object[] { index }) as string;
            }
            catch (Exception e)
            {
                LogWarn($"MoreOutfits名前取得失敗 slot={index}: {e.Message}");
                return null;
            }
        }

        private CustomControl _captureControl;
        private int _originalCoordinate;
        private readonly List<Texture2D> _ownedTextures = new List<Texture2D>();
        public void CancelCapture()
        {
            StopAllCoroutines();
            foreach (var texture in _ownedTextures) if (texture != null) Destroy(texture);
            _ownedTextures.Clear(); IsRunning = false;
            if (_captureControl != null && _captureControl.dropdownCoordinate != null)
                _captureControl.dropdownCoordinate.value = _originalCoordinate;
            _captureControl = null;
        }
        public void StartCapture()
        {
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed) return;
            var customBase = Singleton<CustomBase>.Instance;
            if (customBase == null)
            {
                LogWarn("CustomBase が見つかりません。キャラメイク画面でのみ使用できます。");
                return;
            }

            var chaCtrl = customBase.chaCtrl;
            if (chaCtrl == null)
            {
                LogWarn("chaCtrl が null です。");
                return;
            }

            var customCtrl = customBase.customCtrl;
            if (customCtrl == null)
            {
                LogWarn("customCtrl が null です。");
                return;
            }

            if (customCtrl.dropdownCoordinate == null)
            {
                LogWarn("dropdownCoordinate が null です。");
                return;
            }

            _captureControl = customCtrl; _originalCoordinate = customCtrl.dropdownCoordinate.value;
            StartCoroutine(CaptureCoroutine(chaCtrl, customCtrl));
        }

        private IEnumerator CaptureCoroutine(ChaControl chaCtrl, CustomControl customCtrl)
        {
            IsRunning = true;
            Log("カタログ撮影を開始します...");

            var coordSlots = chaCtrl.chaFile.coordinate;
            Log($"コーデスロット数: {coordSlots.Length}");
            Log($"現在のコーデタイプ: {chaCtrl.fileStatus.coordinateType}");
            Log($"Screen: {Screen.width}x{Screen.height}");

            var screenshots = new List<(Texture2D tex, string name, int index)>();

            int fullW = Screen.width;
            int fullH = Screen.height;
            int capW = fullW / 4;
            int capX = fullW * 3 / 8;

            Log($"キャプチャ設定: fullW={fullW} fullH={fullH} capW={capW} capX={capX}");

            // 各スロットの衣装IDと名前を事前ログ
            for (int i = 0; i < coordSlots.Length; i++)
            {
                int topId = coordSlots[i].clothes.parts[0].id;
                int botId = coordSlots[i].clothes.parts[1].id;
                string previewName = GetDisplayName(chaCtrl, i);
                Log($"[slot {i}] top={topId} bot={botId} name={previewName}");
            }

            for (int i = 0; i < coordSlots.Length; i++)
            {
                string displayName = GetDisplayName(chaCtrl, i);
                Log($"[{i}] 切替開始: displayName={displayName}");

                // メイカーUIのドロップダウン経由でコーデ切替
                customCtrl.dropdownCoordinate.value = i;
                Log($"[{i}] ddCoordinate.value={i} セット完了");

                // 着替え + Destroy deferred + テクスチャフラグが全フレームに行き渡るまで待つ
                yield return null;
                yield return null;
                yield return null;

                // WaitForEndOfFrame: Unity レンダーループ完了後にスクリーンから直接読む
                // （camMain.Render() による明示レンダーを避けることで旧服の残像問題を回避）
                yield return new WaitForEndOfFrame();

                int nowTopId = chaCtrl.nowCoordinate.clothes.parts[0].id;
                Log($"[{i}] coordinateType={chaCtrl.fileStatus.coordinateType} nowTop={nowTopId}");

                var tex = ReadScreen(fullW, fullH, capX, capW);
                if (tex != null)
                    { screenshots.Add((tex, displayName, i)); _ownedTextures.Add(tex); }
                else
                    LogWarn($"[{i}] ReadScreen 失敗");
            }

            if (screenshots.Count == 0)
            {
                LogError("スクショが1枚も撮れませんでした。中断します。");
                IsRunning = false; _ownedTextures.Clear();
                yield break;
            }

            Log("合成中...");
            try
            {
                var outDir = Path.Combine(_pluginDir, "catalog_output");
                Directory.CreateDirectory(outDir);
                var baseName = $"catalog_{DateTime.Now:yyyyMMdd_HHmmss}";
                var imagePath = Path.Combine(outDir, $"{baseName}.png");
                var jsonPath = Path.Combine(outDir, $"{baseName}.json");

                ImageComposer.Compose(screenshots, imagePath);
                SaveCoordNamesJson(screenshots, jsonPath);
                Log($"保存完了: {imagePath}");
                Log($"JSON保存完了: {jsonPath}");
            }
            catch (Exception e)
            {
                LogError($"合成失敗: {e}");
            }

            foreach (var item in screenshots)
                Destroy(item.tex);

            IsRunning = false; _ownedTextures.Clear();
        }

        // WaitForEndOfFrame 後に呼ぶこと
        private Texture2D ReadScreen(int fullW, int fullH, int cropX, int cropW)
        {
            try
            {
                var fullTex = new Texture2D(fullW, fullH, TextureFormat.RGB24, false);
                fullTex.ReadPixels(new Rect(0, 0, fullW, fullH), 0, 0);
                fullTex.Apply();

                var pixels = fullTex.GetPixels(cropX, 0, cropW, fullH);
                Destroy(fullTex);

                var cropped = new Texture2D(cropW, fullH, TextureFormat.RGB24, false);
                cropped.SetPixels(pixels);
                cropped.Apply();
                return cropped;
            }
            catch (Exception e)
            {
                LogError($"ReadScreen 例外: {e}");
                return null;
            }
        }

        private void SaveCoordNamesJson(List<(Texture2D tex, string name, int index)> screenshots, string jsonPath)
        {
            var names = screenshots.Select(s => s.name).ToList();
            var json = BuildCoordNamesJson(names);
            File.WriteAllText(jsonPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private string BuildCoordNamesJson(List<string> names)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[");

            for (int i = 0; i < names.Count; i++)
            {
                var escaped = EscapeJsonString(names[i] ?? string.Empty);
                sb.Append($"  {{ \"coordName\": \"{escaped}\", \"features\": [] }}");
                if (i < names.Count - 1)
                    sb.Append(",");
                sb.AppendLine();
            }

            sb.Append("]");
            return sb.ToString();
        }

        private string EscapeJsonString(string value)
        {
            var sb = new StringBuilder(value.Length + 8);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (char.IsControl(c))
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
