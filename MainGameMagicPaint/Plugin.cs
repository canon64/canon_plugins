using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using CanonGate;
using HarmonyLib;
using MainGameUnifiedUi;
using UnityEngine;

namespace MainGameMagicPaint
{
    [BepInPlugin(Guid, "MainGameMagicPaint", "1.3.1")]
    [BepInDependency(GateApi.PluginGuid, "2.4.0")]
    [BepInDependency("com.kks.maingame.unifiedui")]
    [BepInDependency("com.kks.maingame.uiinputcapture")]
    [BepInDependency("com.kks.maingame.transformgizmo")]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.kks.maingame.magicpaint";
        static Plugin _instance;
        IDisposable _registration;
        GateApi.Permit _permit;
        MonoBehaviour _scene;
        bool _active;
        ulong _lastSession;
        Harmony _harmony;
        ConfigEntry<bool> _enabled, _logging;
        ConfigEntry<PaintTool> _preferredTool;
        ConfigEntry<int> _preferredColor;
        bool _configDirty;
        ConfigEntry<float> _snap, _detach, _eraserWidth;
        readonly List<PaintActor> _actors = new List<PaintActor>();
        int _target;
        PaintActor Selected => _target >= 0 && _target < _actors.Count ? _actors[_target] : null;
        string _status = "フリーHの開始待ち";
        ToolModel _model;
        ToolHud _hud;
        PaintTool _tool;
        PaintSetting _setting = PaintSetting.Width;
        readonly StickState _stick = new StickState();
        bool _trigger, _stroke, _contact, _desktop, _desktopErase;
        PaintActor _strokeActor;
        readonly HashSet<PaintActor> _strokeActors = new HashSet<PaintActor>();
        SkinSurface _previousSurface;
        Vector3 _previousPoint;
        float _lastFlush, _gaugeUntil, _sprayBudget;
        int _colorIndex;
        readonly System.Random _random = new System.Random();
        static readonly string[] ColorNames = { "黒", "白", "赤", "オレンジ", "黄", "緑", "水色", "青", "紫", "ピンク", "消去" };
        static readonly Color[] Colors = { new Color(.025f,.025f,.025f), Color.white, new Color(.9f,.05f,.05f), new Color(1,.35f,0), new Color(1,.9f,0), new Color(.05f,.65f,.15f), new Color(.1f,.8f,1), new Color(.02f,.3f,1), new Color(.55f,.12f,.9f), new Color(1,.15f,.55f), new Color(.8f,1,1) };
        ConfigEntry<float> _markerWidth, _sprayWidth, _grain, _transparency, _flow, _feather, _variation;
        bool CanAct => _active && isActiveAndEnabled && _enabled.Value && _permit.IsValid;
        bool EraseSpray => _tool == PaintTool.Spray && _colorIndex == 10;
        Color InkColor => Colors[_colorIndex];

        void Awake()
        {
            _instance = this;
            Config.SaveOnConfigSet = false;
            _enabled = Config.Bind("General", "Enabled", true, "有効。再有効化は次のHから。");
            _logging = Config.Bind("Diagnostics", "Enabled", false, "描画・接地・入力の診断ログ。UserData/MagicPaint/Diagnostics/magicpaint.log。OFFでは診断収集・整形・ファイル出力を停止。");
            _markerWidth = Number("Marker", "WidthMm", 3, .5f, 30, "線幅 mm");
            _sprayWidth = Number("Spray", "WidthMm", 50, 5, 200, "距離10cmでの噴射直径 mm");
            _grain = Number("Spray", "GrainMm", 1, .2f, 5, "距離10cmでの粒の直径 mm");
            _transparency = Number("Ink", "Transparency", 0, 0, 100, "透明度。0=不透明、100=透明。調整項目を表示して右で増加。");
            _flow = Number("Spray", "ParticlesPerSecond", 600, 100, 3000, "1秒あたりの粒の数");
            _feather = Number("Spray", "Feather", 70, 0, 100, "外周のぼかし %");
            _variation = Number("Spray", "Variation", 50, 0, 100, "粒サイズ・濃さのばらつき %");
            _snap = Number("Contact", "AttachMm", 20, 1, 50, "自動接地の開始距離 mm");
            _detach = Number("Contact", "DetachMm", 30, 2, 60, "接地解除距離と最大位置補正 mm。開始距離より大きい値を使用。");
            _eraserWidth = Number("Marker", "EraserMm", 10, 2, 50, "後端消しゴムの直径 mm");
            _preferredTool = Config.Bind("General", "Tool", PaintTool.Marker, "最後に選んだ道具。");
            _preferredColor = Config.Bind("Ink", "ColorIndex", 7, new ConfigDescription("最後に選んだ色。0～9=着色、10=消去スプレー。初期色は外観案と同じ青。", new AcceptableValueRange<int>(0,10)));
            _tool = _preferredTool.Value; _colorIndex = Mathf.Clamp(_preferredColor.Value, 0, _tool == PaintTool.Marker ? 9 : 10);
            Config.SettingChanged += ConfigChanged; Config.Save(); SyncTrace();
            _enabled.SettingChanged += EnabledChanged;
            _registration = GateApi.Register(Guid, StartSession, StopSession);
            UnifiedUiApi.RegisterTab(Guid, "マジック・スプレー", DrawTab, 625);
            _harmony = new Harmony(Guid);
            // Recompose only when the game's own skin texture changes; no scene or material polling.
            foreach (string method in new[] { "CreateBodyTexture", "CreateFaceTexture" })
            {
                var target = AccessTools.Method(typeof(ChaControl), method, Type.EmptyTypes);
                if (target != null) _harmony.Patch(target, postfix: new HarmonyMethod(typeof(Plugin), nameof(SkinChanged)));
            }
        }
        ConfigEntry<float> Number(string section, string key, float value, float min, float max, string description)
            => Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));
        void EnabledChanged(object sender, EventArgs e) { if (!_enabled.Value) StopSession(); }
        void ConfigChanged(object sender, SettingChangedEventArgs e)
        { _configDirty = true; if (_logging != null && e.ChangedSetting == _logging) SyncTrace(); if (_logging != null && !_logging.Value && InspectionRunning) CancelInspection(); }
        void SaveConfig() { if (!_configDirty) return; Config.Save(); _configDirty = false; }
        void StartSession()
        {
            var c = GateApi.Current;
            if (!_enabled.Value || !isActiveAndEnabled || c == null || c.Kind != GateApi.SessionKind.H || c.Scene == null || c.Id == _lastSession) return;
            _lastSession = c.Id; _permit = GateApi.Capture(Guid); if (!_permit.IsValid) return;
            _scene = c.Scene; _active = true;
            _io = new System.Threading.CancellationTokenSource(); _busy = false;
            try { RefreshActors(); ConnectVr(); RefreshSaves(); _status = "マジックを呼び出してください。"; }
            catch (Exception ex) { StopSession(); Fail("開始失敗", ex); }
        }
        void RefreshActors()
        {
            if (!CanAct || _scene == null) return;
            var found = new List<ChaControl>();
            foreach (string name in new[] { "lstFemale", "chaFemale" })
            {
                var type = _scene.GetType(); object value = AccessTools.Field(type, name)?.GetValue(_scene) ?? AccessTools.Property(type, name)?.GetValue(_scene, null);
                if (value is ChaControl actor) { if (!found.Contains(actor)) found.Add(actor); }
                else if (value is IEnumerable list) foreach (object item in list) if (item is ChaControl cha && !found.Contains(cha)) found.Add(cha);
            }
            foreach (var actor in found) if (actor != null && !_actors.Exists(a => a.Actor == actor)) _actors.Add(new PaintActor(actor));
            foreach (var actor in _actors) actor.Refresh();
            _target = Mathf.Clamp(_target, 0, Mathf.Max(0, _actors.Count - 1));
        }
        static void SkinChanged(ChaControl __instance)
        {
            if(__instance!=null){SkinOverlayApi.Refresh(__instance.rendFace);SkinOverlayApi.Refresh(__instance.rendBody);}
            var p = _instance; if (p == null || !p.CanAct) return;
            var actor = p._actors.Find(a => a.Actor == __instance); if (actor == null) return;
            try { actor.Refresh(); } catch (Exception ex) { p.Fail("肌の再接続失敗", ex); }
        }
        void StopSession()
        {
            CancelInspection();
            _active = false; _permit = default;
            var io = _io; _io = null; if (io != null) { io.Cancel(); io.Dispose(); }
            try { DisconnectVr(); }
            finally
            {
                DeleteTool();
                foreach (var actor in _actors) actor.Dispose(); _actors.Clear(); _scene = null;
                _status = "フリーHの開始待ち";
                SaveConfig();
            }
        }
        void EndStroke()
        {
            foreach (var actor in _strokeActors) { actor.Commit(); actor.Flush(); TraceStrokeEnd(actor); } _strokeActors.Clear();
            _strokeActor = null; _stroke = false; _previousSurface = null; _sprayBudget = 0;
        }
        void Undo(bool redo)
        {
            if (!CanAct || _stick.Menu) return; StopPaintPress(); _mouseDown = false; EndStroke(); var actor = Selected; if (actor == null) return;
            if (redo) actor.History.Redo(); else actor.History.Undo(); actor.Flush(); _status = redo ? "REDO" : "UNDO";
        }
        void ClearDrawing()
        {
            if (!CanAct || _stick.Menu) return; StopPaintPress(); _mouseDown = false; EndStroke(); Selected?.Clear(); _status = "選択キャラの顔・体の落書きを全削除しました。UNDOで戻せます。";
        }
        void Fail(string message, Exception ex)
        { _status = message + "：" + ex.Message; if (_logging != null && _logging.Value) Logger.LogError(message + ": " + ex); }
        void OnApplicationFocus(bool focused) { if (!focused) { ReleaseHand(); SetDesktop(false); } }
        void OnApplicationPause(bool paused) { if (paused) { ReleaseHand(); SetDesktop(false); } }
        void OnDisable() => StopSession();
        void OnDestroy()
        {
            UnifiedUiApi.UnregisterTab(Guid); _enabled.SettingChanged -= EnabledChanged;
            Config.SettingChanged -= ConfigChanged;
            try { _registration?.Dispose(); } finally { StopSession(); _trace?.Dispose(); _trace = null; _harmony?.UnpatchSelf(); _instance = null; }
        }
    }
}
