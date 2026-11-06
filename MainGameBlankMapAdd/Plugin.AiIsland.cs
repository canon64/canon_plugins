using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private const string AiIslandKey = "ai_shoujo_main";
        private bool _aiIslandSettingsOpen;
        private const string AiIslandScene = "map_00_data";
        private ConfigEntry<string> _aiIslandDirectory;
        private ConfigEntry<float> _aiMoveMultiplier, _aiFarClip, _aiLoadTimeout;
        private ConfigEntry<float> _aiMapScale, _aiDetailDistance, _aiShadowDistance;
        private ConfigEntry<bool> _aiRealtimeWater;
        private ConfigEntry<bool> _aiDiagnosticLog, _aiEventLog;
        private ConfigEntry<float> _aiDiagnosticDuration, _aiDiagnosticInterval;
        private AiIslandDiagnostics _aiDiagnostics;
        private AiIslandVisibility _aiVisibility;
        private Terrain _aiTerrain;
        private AiIslandWaterReflection[] _aiWater = Array.Empty<AiIslandWaterReflection>();
        private ConfigEntry<int> _aiWaterResolution;
        private readonly List<AssetBundle> _aiIslandBundles = new List<AssetBundle>();
        private bool _aiLoadInProgress, _aiCancelRequested, _aiIslandActive, _aiDisposed;
        private string _aiIslandStatus;
        private AssetBundleCreateRequest _aiPendingBundle;
        private AsyncOperation _aiPendingScene;
        private AiIslandEnvironment _aiIslandEnvironment;
        private AiIslandCameraScope _aiIslandCamera;
        private HSceneProc _aiIslandHScene;
        private static Plugin _aiIslandOwner;
        private Coroutine _aiLoadCoroutine;
        private int _aiEpoch;
        private bool _aiDisposingScene;

        private bool IsAiIslandScene(string name) => name == AiIslandScene;

        private void InitializeAiIsland()
        {
            _aiIslandOwner = this;
            _aiIslandDirectory = Config.Bind("13.AiIsland", "DataDirectory",
                Path.Combine(_pluginDir, "Assets", "AiShoujoMain"), "AI少女本編島の加工済みmanifest.jsonがあるフォルダー。元のゲームフォルダーは指定しない。");
            _aiMoveMultiplier = Config.Bind("13.AiIsland", "MoveSpeedMultiplier", 1f,
                new ConfigDescription("島の表示中のカメラ平行移動速度。標準1倍（通常の速度）、範囲1～100倍。", new AcceptableValueRange<float>(1f, 100f)));
            _aiFarClip = Config.Bind("13.AiIsland", "FarClipMeters", 500f,
                new ConfigDescription("島の表示中の遠方描画距離。50～10000m。補正後のKKSの単位。大きいほど負荷が増える。", new AcceptableValueRange<float>(50f, 10000f)));
            _aiMapScale = Config.Bind("13.AiIsland", "MapScale", .1f,
                new ConfigDescription("AI少女からKKSへの実寸変換。標準0.1（1/10）、範囲0.01～1。次回読み込み時に適用。", new AcceptableValueRange<float>(.01f, 1f)));
            _aiDetailDistance = Config.Bind("13.AiIsland", "DetailDrawMeters", 200f,
                new ConfigDescription("地形に植えられた木の描画距離。10～1000m。木は10m以遠で標準ビルボードへ切替。島のメッシュは距離で強制非表示にしない。", new AcceptableValueRange<float>(10f, 1000f)));
            _aiShadowDistance = Config.Bind("13.AiIsland", "ShadowDrawMeters", 25f,
                new ConfigDescription("島の表示中の影の描画距離。10～300m。40なら40m先まで影を計算する。小さくすると軽くなる。", new AcceptableValueRange<float>(10f, 300f)));
            _aiDiagnosticLog = Config.Bind("13.AiIsland", "PerformanceLogEnabled", true, "描画診断を記録する。初回はオン。時間上限で自動オフになり、設定も保存する。UIからいつでも停止・再開できる。");
            _aiEventLog = Config.Bind("13.AiIsland", "EventLogEnabled", true, "読み込み・失敗・解放ログを記録する。オフはこれらのファイル書き込みも停止する。");
            _aiDiagnosticDuration = Config.Bind("13.AiIsland", "PerformanceLogSeconds", 60f,
                new ConfigDescription("診断を自動停止するまでの秒数。10～300秒。", new AcceptableValueRange<float>(10f, 300f)));
            _aiDiagnosticInterval = Config.Bind("13.AiIsland", "PerformanceLogIntervalSeconds", 2f,
                new ConfigDescription("診断を集計して書き出す間隔。1～30秒。短いほど描画数の集計回数が増える。", new AcceptableValueRange<float>(1f, 30f)));
            _aiRealtimeWater = Config.Bind("13.AiIsland", "RealtimeWaterReflection", false,
                "オンは水面に島を再描画するため負荷が増える。オフは空色の軽量な反射。毎秒最大5回・小物描画距離まで。");
            _aiLoadTimeout = Config.Bind("13.AiIsland", "LoadTimeoutSeconds", 180f,
                new ConfigDescription("各バンドル・シーンの読み込み待ち時間。30～600秒。", new AcceptableValueRange<float>(30f, 600f)));
            _aiWaterResolution = Config.Bind("13.AiIsland", "WaterReflectionResolution", 256,
                new ConfigDescription("水の反射画像の一辺の画素数。128～1024。次回の島読み込み時に適用。", new AcceptableValueRange<int>(128, 1024)));
            var revision = Config.Bind("13.AiIsland", "RenderSettingsRevision", 0, "描画設定の移行バージョン。");
            if (revision.Value < 1)
            {
                if (_aiFarClip.Value == 3000f) _aiFarClip.Value = 500f;
                if (_aiWaterResolution.Value == 512) _aiWaterResolution.Value = 256;
                revision.Value = 1;
                Config.Save();
            }
            if (revision.Value < 2)
            {
                if (_aiMoveMultiplier.Value == 10f) _aiMoveMultiplier.Value = 1f;
                if (_aiFarClip.Value == 500f) _aiFarClip.Value = 200f;
                if (_aiDetailDistance.Value == 80f) _aiDetailDistance.Value = 40f;
                if (_aiShadowDistance.Value == 40f) _aiShadowDistance.Value = 25f;
                revision.Value = 2;
                Config.Save();
            }
            if (revision.Value < 3)
            {
                if (_aiFarClip.Value == 200f) _aiFarClip.Value = 500f;
                if (_aiDetailDistance.Value == 40f) _aiDetailDistance.Value = 200f;
                revision.Value = 3;
                Config.Save();
            }
            foreach (ConfigEntry<float> entry in new[] { _aiMoveMultiplier, _aiFarClip,
                _aiShadowDistance, _aiDetailDistance })
                entry.SettingChanged += OnAiIslandSettingsChanged;
            foreach (ConfigEntry<bool> entry in new[] { _aiRealtimeWater, _aiDiagnosticLog })
                entry.SettingChanged += OnAiIslandSettingsChanged;
        }

        private void OnAiIslandSettingsChanged(object sender, EventArgs e) => TickAiIsland();

        private void DrawAiIslandChoice()
        {
            bool previous = GUI.enabled;
            GUI.enabled = previous && !_roomOverlayLoading && !_aiDisposingScene && (_aiPendingBundle == null || _aiPendingBundle.isDone);
            if (GUILayout.Button("AI少女・本編島", GUILayout.Width(190f)))
                _mapSelectionPending = () => _aiLoadCoroutine = StartCoroutine(SwitchAiIsland());
            GUI.enabled = previous;
            if (!_aiIslandActive && !string.IsNullOrEmpty(_aiIslandStatus) &&
                (_aiIslandStatus.Contains("失敗") || _aiIslandStatus.Contains("中止"))) GUILayout.Label(_aiIslandStatus);
        }

        private bool? _aiIslandSettingsPending;
        private readonly Dictionary<ConfigEntry<float>, string> _aiIslandNumberText = new Dictionary<ConfigEntry<float>, string>();
        private bool _aiIslandUiSettingsDirty;
        private void CommitAiIslandUiSettings()
        {
            if (!_aiIslandUiSettingsDirty) return;
            Config.Save();
            _aiIslandUiSettingsDirty = false;
        }
        private void DrawAiIslandSettings()
        {
            if (!_aiIslandActive)
            {
                CommitAiIslandUiSettings();
                _aiIslandSettingsOpen = false;
                _aiIslandSettingsPending = null;
                return;
            }
            if (Event.current.type == EventType.Layout && _aiIslandSettingsPending.HasValue)
            {
                _aiIslandSettingsOpen = _aiIslandSettingsPending.Value;
                _aiIslandSettingsPending = null;
            }
            if (!string.IsNullOrEmpty(_aiIslandStatus)) GUILayout.Label(_aiIslandStatus);
            if (GUILayout.Button((_aiIslandSettingsOpen ? "▼ " : "▶ ") + "島の詳細設定", GUILayout.Width(160f), GUILayout.Height(28f)))
                _aiIslandSettingsPending = !_aiIslandSettingsOpen;
            if (!_aiIslandSettingsOpen) return;
            bool diagnostic = GUILayout.Toggle(_aiDiagnosticLog.Value, "描画診断ログを記録（時間上限で自動オフ）");
            bool events = GUILayout.Toggle(_aiEventLog.Value, "読み込み・失敗ログも記録");
            if (diagnostic != _aiDiagnosticLog.Value || events != _aiEventLog.Value)
            { _aiDiagnosticLog.Value = diagnostic; _aiEventLog.Value = events; Config.Save(); if (!diagnostic) StopAiIslandDiagnostics(); }
            DrawMapFloatSetting("診断時間（秒）", _aiDiagnosticDuration, 10f, 300f);
            DrawMapFloatSetting("記録間隔（秒）", _aiDiagnosticInterval, 1f, 30f);
            GUILayout.Label("診断保存先：" + Path.Combine(_pluginDir, "AiIsland.Performance.log"));
            if (GUILayout.Button("島設定を再読込", GUILayout.Width(160f)))
            {
                try { _aiIslandStatus = "設定ファイルの再読込は次回の開始時に行います。"; }
                catch (Exception ex) { _aiIslandStatus = "島設定の再読込失敗：" + ex.Message; WriteAiIslandLog(_aiIslandStatus); }
            }
            if (!_aiIslandActive) return;
            GUILayout.Label("自由カメラ：マウス・矢印キーで移動、PageUp / PageDownで上下。既存のマップ配置で立ち位置を合わせられます。");
            DrawMapFloatSetting("移動速度（倍）", _aiMoveMultiplier, 1f, 100f);
            GUILayout.Label("島の実寸補正：" + _aiMapScale.Value.ToString("0.###") + "倍（次回読み込み時）。10m以遠は簡易LOD、9m以内で詳細へ戻します。");
            DrawMapFloatSetting("実寸補正（倍）", _aiMapScale, .01f, 1f);
            DrawMapFloatSetting("遠方描画（m）", _aiFarClip, 50f, 10000f);
            DrawMapFloatSetting("地形の植生（m）", _aiDetailDistance, 10f, 1000f);
            DrawMapFloatSetting("影の描画（m）", _aiShadowDistance, 10f, 300f);
            bool water = GUILayout.Toggle(_aiRealtimeWater.Value, "重い水面反射を使う（通常はオフ）");
            if (water != _aiRealtimeWater.Value) { _aiRealtimeWater.Value = water; Config.Save(); }
            if (_aiVisibility != null) GUILayout.Label("簡易LOD表示中：" + _aiVisibility.SimpleCount + "グループ");
            if (Event.current.rawType == EventType.MouseUp ||
                (Event.current.rawType == EventType.KeyUp && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)))
                CommitAiIslandUiSettings();
        }

        private void DrawMapFloatSetting(string label, ConfigEntry<float> entry, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(130f));
            float shown = Mathf.Clamp(entry.Value, min, max);
            float value = DrawCapturedMapSlider(shown, min, max, Mathf.Clamp(_mapAdjustmentWidth - 240f, 140f, 260f));
            string control = "AiIslandNumber:" + entry.Definition.Key;
            if (!_aiIslandNumberText.ContainsKey(entry) || GUI.GetNameOfFocusedControl() != control)
                _aiIslandNumberText[entry] = entry.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            if (value != shown) _aiIslandNumberText[entry] = value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            GUI.SetNextControlName(control);
            string previous = _aiIslandNumberText[entry];
            string input = GUILayout.TextField(previous, GUILayout.Width(75f));
            _aiIslandNumberText[entry] = input;
            if (input != previous && float.TryParse(input, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float parsed) && !float.IsNaN(parsed) &&
                !float.IsInfinity(parsed) && parsed >= min && parsed <= max) value = parsed;
            else if (value == shown) value = entry.Value;
            if (value != entry.Value)
            {
                bool autoSave = Config.SaveOnConfigSet;
                Config.SaveOnConfigSet = false;
                try { entry.Value = value; _aiIslandUiSettingsDirty = true; }
                finally { Config.SaveOnConfigSet = autoSave; }
            }
            GUILayout.EndHorizontal();
        }
        private void DrawAiIslandIntSetting(string label, ConfigEntry<int> entry, int min, int max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(150f));
            int value = Mathf.RoundToInt(DrawCapturedMapSlider(entry.Value, min, max, Mathf.Clamp(_mapAdjustmentWidth - 240f, 140f, 260f)));
            GUILayout.Label(entry.Value.ToString(), GUILayout.Width(75f));
            if (value != entry.Value) { entry.Value = value; Config.Save(); }
            GUILayout.EndHorizontal();
        }

        private void AiIslandStage(string status)
        {
            _aiIslandStatus = status;
            _roomOverlayLoadStartedAt = Time.realtimeSinceStartup;
            WriteAiIslandLog(status);
        }

        private void WriteAiIslandLog(string message)
        {
            if (_aiEventLog != null && !_aiEventLog.Value) return;
            // Normal plugin logging is intentionally disabled; island load diagnostics are sparse and explicit.
            try { File.AppendAllText(Path.Combine(_pluginDir, "AiIsland.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + Environment.NewLine, System.Text.Encoding.UTF8); }
            catch (Exception ex) { Logger.LogWarning("AI island diagnostic log: " + ex.Message); }
        }

        private IEnumerator SwitchAiIsland()
        {
            if (!CanUseRoomOverlay() || _roomOverlayLoading || _aiDisposingScene || !enabled || (_aiPendingBundle != null && !_aiPendingBundle.isDone)) yield break;
            _aiDisposed = false;
            _aiIslandOwner = this;
            _roomOverlayLoading = true;
            yield return UnloadRoomOverlay();
            if (!CanUseRoomOverlay()) { _roomOverlayLoading = false; yield break; }
            _aiCancelRequested = false;
            _aiLoadInProgress = true;
            Exception failure = null;
            IEnumerator core = LoadAiIslandCore();
            while (true)
            {
                object step = null;
                bool more = false;
                try { more = core.MoveNext(); if (more) step = core.Current; }
                catch (Exception ex) { failure = ex; }
                if (failure != null || !more) break;
                yield return step;
            }
            (core as IDisposable)?.Dispose();
            if (failure != null || _aiCancelRequested)
            {
                string message = _aiCancelRequested ? "島の読み込みを中止しました。" : "島の読み込み失敗（" + _aiIslandStatus + "）：" + failure.Message;
                if (failure != null) WriteAiIslandLog("FAILED: " + failure);
                yield return ReleaseAiIsland();
                _roomOverlaySceneName = null;
                _roomOverlayBundleName = null;
                _roomOverlayPositionKey = null;
                _roomOverlayDisplayName = null;
                _roomOverlayPositionRoot = null;
                RestoreSourceRoomAfterOverlay();
                _aiIslandStatus = message;
            }
            _aiLoadInProgress = false;
            _roomOverlayLoading = false;
            _aiLoadCoroutine = null;
            if(failure==null&&!_aiCancelRequested)QueueDefaultHPlace();
        }

        private IEnumerator LoadAiIslandCore()
        {
            AiIslandStage("島のmanifestを確認中");
            string directory = Path.GetFullPath(_aiIslandDirectory.Value);
            string manifestPath = Path.Combine(directory, "manifest.json");
            if (!File.Exists(manifestPath)) throw new FileNotFoundException("加工済みmanifest.jsonがありません。DataDirectoryを確認してください。", manifestPath);
            AiIslandManifest manifest = AiIslandManifest.Read(File.ReadAllText(manifestPath));
            var paths = manifest.files.Select(f => ResolveAiIslandFile(directory, f.path)).ToArray();
            AiIslandStage("島データのサイズ・SHA-256を確認中");
            Task validation = Task.Run(() => ValidateAiIslandFiles(manifest.files, paths));
            while (!validation.IsCompleted)
            {
                if (_aiDisposed) yield break;
                yield return null;
            }
            if (validation.IsFaulted) throw validation.Exception.GetBaseException();
            if (_aiCancelRequested || !CanUseRoomOverlay()) { _aiCancelRequested = true; yield break; }
            for (int i = 0; i < paths.Length; i++)
            {
                AiIslandStage("島データ読み込み " + (i + 1) + "/" + paths.Length + "：" + manifest.files[i].path);
                AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(paths[i]);
                _aiPendingBundle = request;
                int epoch = _aiEpoch;
                request.completed += op =>
                {
                    if (epoch == _aiEpoch && !_aiDisposed) return;
                    if (request.assetBundle != null) request.assetBundle.Unload(true);
                    if (ReferenceEquals(_aiPendingBundle, request)) _aiPendingBundle = null;
                };
                bool timedOut = false;
                while (!request.isDone)
                {
                    if (!timedOut && Time.realtimeSinceStartup - _roomOverlayLoadStartedAt > _aiLoadTimeout.Value)
                    { timedOut = true; _aiCancelRequested = true; _aiIslandStatus = "読み込みが制限時間を超えました。現在の処理が終わり次第、元の部屋へ戻します。"; }
                    yield return null;
                }
                _aiPendingBundle = null;
                if (request.assetBundle == null) throw new InvalidDataException("AssetBundleを読めません：" + paths[i]);
                _aiIslandBundles.Add(request.assetBundle);
                if (timedOut) throw new TimeoutException("バンドルの読み込みが制限時間を超えました。");
                if (_aiCancelRequested || !CanUseRoomOverlay()) { _aiCancelRequested = true; yield break; }
            }
            AssetBundle main = _aiIslandBundles.Last();
            string scenePath = main.GetAllScenePaths().SingleOrDefault(p => Path.GetFileNameWithoutExtension(p) == AiIslandScene);
            if (scenePath == null) throw new InvalidDataException("島のシーンがバンドルにありません。");
            _aiIslandEnvironment = AiIslandEnvironment.Capture();
            _roomOverlaySceneName = AiIslandScene;
            _roomOverlayBundleName = null;
            _roomOverlayPositionKey = AiIslandKey;
            _roomOverlayDisplayName = "AI少女・本編島";
            AiIslandStage("島のシーンを追加読み込み中");
            _aiPendingScene = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            if (_aiPendingScene == null) throw new InvalidOperationException("シーンの読み込みを開始できません。");
            bool sceneTimeout = false;
            while (!_aiPendingScene.isDone)
            {
                if (!sceneTimeout && Time.realtimeSinceStartup - _roomOverlayLoadStartedAt > _aiLoadTimeout.Value)
                { sceneTimeout = true; _aiCancelRequested = true; _aiIslandStatus = "シーンの読み込みが制限時間を超えました。完了後に元の部屋へ戻します。"; }
                yield return null;
            }
            _aiPendingScene = null;
            if (sceneTimeout) throw new TimeoutException("シーンの読み込みが制限時間を超えました。");
            if (_aiCancelRequested || !CanUseRoomOverlay()) { _aiCancelRequested = true; yield break; }
            AiIslandStage("地形・開始位置・描画を設定中");
            Scene scene = SceneManager.GetSceneByName(AiIslandScene);
            GameObject root = scene.GetRootGameObjects().SingleOrDefault(o => o.name == manifest.root);
            if (root == null) throw new InvalidDataException("島のルートがありません。");
            root.AddComponent<AiIslandGeometry>().ConvertUnits(_aiMapScale.Value);
            Transform anchor = root.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == manifest.anchor);
            HSceneProc h = CanonPlugins.Shared.PluginSessionGate.HScene;
            ChaControl actor = _femaleChara;
            if (anchor == null || actor == null || h == null || h.flags == null || h.flags.ctrlCamera == null)
                throw new InvalidOperationException("島の開始位置またはHシーンのカメラ・キャラを取得できません。");
            Terrain terrain = root.GetComponentInChildren<Terrain>(true);
            if (terrain == null || terrain.terrainData == null || terrain.materialTemplate == null ||
                terrain.materialTemplate.shader == null || !terrain.materialTemplate.shader.isSupported)
                throw new InvalidOperationException("島の地形材質・CTSシェーダーを使用できません。");
            terrain.basemapDistance = 10f;
            terrain.treeBillboardDistance = 10f;
            terrain.treeCrossFadeLength = 2f;
            terrain.treeDistance = _aiDetailDistance.Value;
            terrain.detailObjectDistance = Mathf.Min(30f, _aiDetailDistance.Value);
            _aiTerrain = terrain;
            Vector3 start = anchor.position;
            Vector3 terrainPoint = start - terrain.transform.position;
            if (terrainPoint.x >= 0 && terrainPoint.z >= 0 && terrainPoint.x <= terrain.terrainData.size.x && terrainPoint.z <= terrain.terrainData.size.z)
                start.y = terrain.SampleHeight(start) + terrain.transform.position.y;
            AiIslandStage("回転対応の地面・当たり判定・植生を準備中");
            var ground = root.AddComponent<AiIslandTerrainMesh>();
            var buildGround = ground.Build(terrain, _aiMapScale.Value, () => !_aiCancelRequested && CanUseRoomOverlay());
            while (buildGround.MoveNext())
            {
                yield return buildGround.Current;
                if (_aiCancelRequested || !CanUseRoomOverlay()) { _aiCancelRequested = true; yield break; }
            }
            root.transform.position += actor.transform.position - start;
            _roomOverlayPositionRoot = root;
            _roomOverlayBasePosition = root.transform.position;
            _roomOverlayBaseRotation = root.transform.rotation;
            _roomOverlayActorAnchor = actor.transform.position;
            EnsureRoomOverlayPositionConfig(AiIslandKey);
            ApplyRoomOverlayPosition();
            _aiIslandEnvironment.RestoreKksEnvironment();
            _aiIslandCamera = new AiIslandCameraScope(h.flags.ctrlCamera);
            _aiIslandHScene = h;
            _aiIslandCamera.Apply(_aiMoveMultiplier.Value, _aiFarClip.Value, _aiShadowDistance.Value);
            int water = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.sharedMaterials.Any(m => m != null && m.HasProperty("_ReflectionTex"))) continue;
                var reflection = renderer.gameObject.AddComponent<AiIslandWaterReflection>();
                reflection.Resolution = _aiWaterResolution.Value;
                reflection.SourceCamera = h.flags.ctrlCamera.thisCmaera;
                reflection.Realtime = _aiRealtimeWater.Value;
                reflection.DrawDistance = _aiDetailDistance.Value;
                water++;
            }
            _aiWater = root.GetComponentsInChildren<AiIslandWaterReflection>(true);
            _aiVisibility = root.AddComponent<AiIslandVisibility>();
            _aiVisibility.Initialize(h.flags.ctrlCamera.thisCmaera);
            HideSourceRoomForOverlay();
            _aiIslandActive = true;
            TickAiIsland();
            AiIslandStage("AI少女・本編島を読み込みました。位置変更と自由カメラを使用できます。");
            WriteAiIslandLog("attached renderers=" + root.GetComponentsInChildren<Renderer>(true).Length + " planarWater=" + water + " terrainShader=" + terrain.materialTemplate.shader.name +
                " unitScale=" + _aiMapScale.Value + " terrainSize=" + terrain.terrainData.size + " detailDistance=" + _aiDetailDistance.Value + " realtimeWater=" + _aiRealtimeWater.Value);
        }

        internal static string ResolveAiIslandFile(string directory, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new InvalidDataException("島データの相対パスが不正です。");
            string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("島データのパスがフォルダー外を指しています。");
            return path;
        }

        private static void ValidateAiIslandFiles(AiIslandFile[] files, string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                var info = new FileInfo(paths[i]);
                if (!info.Exists || info.Length != files[i].bytes) throw new InvalidDataException("島データのサイズ不一致：" + files[i].path);
                string hash;
                using (var sha = SHA256.Create())
                using (var stream = File.OpenRead(paths[i])) hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (!string.Equals(hash, files[i].sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("島データのSHA-256不一致：" + files[i].path);
            }
        }

        private void TickAiIsland()
        {
            if (!_aiIslandActive) return;
            if (!CanUseRoomOverlay() || _roomOverlayPositionRoot == null || !IsAiIslandScene(_roomOverlaySceneName))
            { ClearRoomOverlay(); return; }
            HSceneProc h = _aiIslandHScene;
            CameraControl_Ver2 camera = h != null && h.flags != null ? h.flags.ctrlCamera : null;
            if (camera == null) return;
            if (_aiIslandCamera == null || !_aiIslandCamera.Owns(camera))
            {
                _aiIslandCamera?.Dispose();
                _aiIslandCamera = new AiIslandCameraScope(camera);
            }
            _aiIslandCamera.Apply(_aiMoveMultiplier.Value, _aiFarClip.Value, _aiShadowDistance.Value);
            _aiVisibility?.SetCamera(camera.thisCmaera);
            if (_aiTerrain != null)
            {
                if (_aiTerrain.treeDistance != _aiDetailDistance.Value) _aiTerrain.treeDistance = _aiDetailDistance.Value;
                float detailDistance = Mathf.Min(30f, _aiDetailDistance.Value);
                if (_aiTerrain.detailObjectDistance != detailDistance) _aiTerrain.detailObjectDistance = detailDistance;
            }
            foreach (var water in _aiWater)
                if (water != null) { water.SourceCamera = camera.thisCmaera; water.Realtime = _aiRealtimeWater.Value; water.DrawDistance = _aiDetailDistance.Value; }
            TickAiIslandDiagnostics(camera.thisCmaera);
        }

        private void TickAiIslandDiagnostics(Camera camera)
        {
            if (!_aiDiagnosticLog.Value) { StopAiIslandDiagnostics(); return; }
            try
            {
                if (_aiDiagnostics == null) _aiDiagnostics = new AiIslandDiagnostics(_pluginDir, _aiDiagnosticDuration.Value, _aiDiagnosticInterval.Value);
                _aiDiagnostics.Tick(_aiVisibility, camera, _aiTerrain, "lodStartMeters=10" +
                    " realtimeWater=" + _aiRealtimeWater.Value + " terrainTreeDistance=" + _aiDetailDistance.Value);
                if (!_aiDiagnostics.Completed) return;
                StopAiIslandDiagnostics(); _aiDiagnosticLog.Value = false; Config.Save();
                _aiIslandStatus = "描画診断ログを保存し、自動停止しました。チェックを入れると再開します。";
            }
            catch (Exception ex)
            {
                StopAiIslandDiagnostics(); _aiDiagnosticLog.Value = false; Config.Save();
                _aiIslandStatus = "描画診断の記録を停止しました：" + ex.Message;
            }
        }
        private void StopAiIslandDiagnostics()
        {
            AiIslandDiagnostics diagnostic = _aiDiagnostics; _aiDiagnostics = null;
            try { diagnostic?.Dispose(); }
            catch (Exception ex) { _aiIslandStatus = "描画診断ログを閉じる際のエラー：" + ex.Message; }
        }

        private IEnumerator ReleaseAiIsland()
        {
            StopAiIslandDiagnostics();
            _aiIslandStatus = null;
            WriteAiIslandLog("releasing island and restoring camera/environment");
            _aiIslandActive = false;
            _aiVisibility = null; _aiTerrain = null; _aiWater = Array.Empty<AiIslandWaterReflection>();
            _aiIslandHScene = null;
            _aiIslandCamera?.Dispose(); _aiIslandCamera = null;
            Scene scene = SceneManager.GetSceneByName(AiIslandScene);
            if (scene.IsValid() && scene.isLoaded)
            {
                AsyncOperation operation = SceneManager.UnloadSceneAsync(scene);
                if (operation != null) yield return operation;
            }
            _aiIslandEnvironment?.Dispose(); _aiIslandEnvironment = null;
            for (int i = _aiIslandBundles.Count - 1; i >= 0; i--) if (_aiIslandBundles[i] != null) _aiIslandBundles[i].Unload(true);
            _aiIslandBundles.Clear();
        }

        private void DisposeAiIsland()
        {
            StopAiIslandDiagnostics();
            _aiDisposed = true; _aiCancelRequested = true; _aiIslandActive = false;
            _aiVisibility = null; _aiTerrain = null; _aiWater = Array.Empty<AiIslandWaterReflection>();
            _aiIslandHScene = null;
            _aiEpoch++;
            if (_aiLoadCoroutine != null) { StopCoroutine(_aiLoadCoroutine); _aiLoadCoroutine = null; }
            _aiIslandCamera?.Dispose(); _aiIslandCamera = null;
            // Detach this session's resources before any later session can begin.
            DisposeAiLoadedScene();
            _aiLoadInProgress = false;
            _roomOverlayLoading = false;
            if (_aiIslandOwner == this) _aiIslandOwner = null;
        }

        private void DisposeAiLoadedScene()
        {
            if (_aiDisposingScene) return;
            AssetBundle[] bundles = _aiIslandBundles.ToArray();
            _aiIslandBundles.Clear();
            AiIslandEnvironment environment = _aiIslandEnvironment; _aiIslandEnvironment = null;
            Action release = () =>
            {
                environment?.Dispose();
                for (int i = bundles.Length - 1; i >= 0; i--) if (bundles[i] != null) bundles[i].Unload(true);
                _aiDisposingScene = false;
            };
            Action unload = () =>
            {
                Scene scene = SceneManager.GetSceneByName(AiIslandScene);
                if (scene.IsValid() && scene.isLoaded)
                {
                    AsyncOperation operation = SceneManager.UnloadSceneAsync(scene);
                    if (operation != null) { operation.completed += op => release(); return; }
                }
                release();
            };
            _aiDisposingScene = true;
            AsyncOperation pending = _aiPendingScene;
            _aiPendingScene = null;
            if (pending != null && !pending.isDone) pending.completed += op => unload();
            else unload();
        }

        private void DisableAiIsland()
        {
            if (!_aiLoadInProgress && !_aiIslandActive && _aiIslandBundles.Count == 0) return;
            ReleaseMapSkyEnvironment();
            DisposeAiIsland();
            _roomOverlaySceneName = null; _roomOverlayDisplayName = null; _roomOverlayPositionRoot = null;
            _roomOverlayPositionKey = null; _roomOverlayBundleName = null;
            RestoreSourceRoomAfterOverlay();
        }

    }
}
