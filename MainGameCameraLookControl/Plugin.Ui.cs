using System;
using System.Reflection;
using BepInEx.Bootstrap;
using MainGameUiInputCapture;
using UnityEngine;

namespace MainGameCameraLookControl
{
    public sealed partial class Plugin
    {
        private const string InputOwner = Guid + ".window";
        private const string TabId = "camera-look-control";
        private Rect _window = new Rect(32f, 50f, 690f, 650f);
        private readonly Rect[] _windowRects = new Rect[1];
        private Vector2 _scroll;
        private int _page, _ownHotControl, _lastEmbeddedFrame = -100;
        private bool _pointerHeld, _captureRegistered;
        private GUIStyle _helpStyle;
        private MethodInfo _registerTab, _unregisterTab, _isHosting;
        private PropertyInfo _uiAvailable;
        private string _uiMessage;

        private bool ShowOwnWindow => _settings != null && _settings.WindowVisible.Value &&
            isActiveAndEnabled && Application.isFocused && _lastEmbeddedFrame < Time.frameCount - 1;

        private void DrawOpenWindowButton()
        {
            if (GUILayout.Button("首操作の設定窓を開く / 閉じる", GUILayout.MinWidth(220f)))
            {
                _settings.WindowVisible.Value = !_settings.WindowVisible.Value;
                UpdateWindowCapture();
            }
        }

        private void TryRegisterTab()
        {
            // The tab host is optional and absent in Studio. Keep its assembly out
            // of this plugin's hard references; input capture itself is direct.
            if (!isActiveAndEnabled || !Chainloader.PluginInfos.ContainsKey("com.kks.maingame.unifiedui")) return;
            try
            {
                if (_registerTab == null)
                {
                    Type type = Type.GetType("MainGameUnifiedUi.UnifiedUiApi, MainGameUnifiedUi", false);
                    if (type == null) return;
                    _registerTab = type.GetMethod("RegisterTab", new[] { typeof(string), typeof(string), typeof(Action), typeof(int) });
                    _unregisterTab = type.GetMethod("UnregisterTab", new[] { typeof(string) });
                    _isHosting = type.GetMethod("IsHosting", new[] { typeof(string) });
                    _uiAvailable = type.GetProperty("IsAvailable");
                }
                if (_uiAvailable?.GetValue(null, null) is bool available && available &&
                    !(_isHosting?.Invoke(null, new object[] { TabId }) is bool hosted && hosted))
                    _registerTab?.Invoke(null, new object[] { TabId, "首操作", (Action)DrawEmbedded, 82 });
            }
            catch (Exception ex) { Warn("Camera-look tab registration failed; use the independent settings window: " + ex.GetBaseException().Message); }
        }

        private void UnregisterTab()
        {
            try { _unregisterTab?.Invoke(null, new object[] { TabId }); }
            catch (Exception ex) { Warn("Camera-look tab removal failed: " + ex.GetBaseException().Message); }
        }

        private void DrawEmbedded()
        {
            _lastEmbeddedFrame = Time.frameCount;
            ReleaseWindowCapture();
            DrawContent();
        }

        private void OnGUI()
        {
            if (!ShowOwnWindow)
            {
                ReleaseWindowCapture();
                return;
            }
            bool ownMouseDown = Event.current.type == EventType.MouseDown && _window.Contains(Event.current.mousePosition);
            int before = GUIUtility.hotControl;
            _window = GUILayout.Window(0x434C4343, _window, DrawWindow, "首操作 — 標準カメラ注視のXYZ制御", GUILayout.Width(690f), GUILayout.Height(650f));
            if (ownMouseDown && GUIUtility.hotControl != 0 && GUIUtility.hotControl != before)
                _ownHotControl = GUIUtility.hotControl;
            if (GUIUtility.hotControl == 0) _ownHotControl = 0;
            UpdateWindowCapture();
        }

        private void DrawWindow(int id)
        {
            if (GUI.Button(new Rect(_window.width - 30f, 2f, 26f, 20f), "×"))
            {
                _settings.WindowVisible.Value = false;
                return;
            }
            DrawContent();
            GUI.DragWindow(new Rect(0f, 0f, _window.width - 35f, 23f));
        }

        private void DrawContent()
        {
            if (_helpStyle == null) _helpStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12 };
            bool enabled = GUILayout.Toggle(_settings.Enabled.Value, "標準のカメラ注視を補正する（女性キャラ・全場面）");
            if (enabled != _settings.Enabled.Value) _settings.Enabled.Value = enabled;
            GUILayout.Label("ゲーム側の「カメラを見る」を使います。注視のON/OFFや対象は変更しません。", _helpStyle);
            if (_harmony == null) GUILayout.Label("標準計算へ接続できていません。BepInExログを確認してください。", _helpStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("標準相当へ戻す")) { _settings.Standard(); _uiMessage = "全追加量0、倍率1へ戻しました。"; }
            if (GUILayout.Button("初期値（見上げ拡張）")) { _settings.Reset(); _uiMessage = "首+10°、頭+20°、上方追従+30°へ戻しました。"; }
            if (GUILayout.Button("設定を再読込"))
            {
                try { Config.Reload(); _settings.Normalize(); _policyDirty = true; _uiMessage = "保存済み設定を読み込みました。"; }
                catch (Exception ex) { _uiMessage = "再読込に失敗: " + ex.Message; }
            }
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_uiMessage)) GUILayout.Label(_uiMessage, _helpStyle);
            _page = GUILayout.Toolbar(_page, new[] { "首・頭の可動域", "曲げ方・追従", "追従範囲・状態" });
            _scroll = GUILayout.BeginScrollView(_scroll);
            try
            {
                foreach (FloatSetting row in _settings.Rows)
                {
                    bool roll = row.Entry.Definition.Key.StartsWith("Roll", StringComparison.Ordinal);
                    int page = row.Group == "Field" ? 2 : row.Group == "Aim" || roll ? 1 : 0;
                    if (page != _page) continue;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(row.Label, GUILayout.Width(210f));
                    float next = GUILayout.HorizontalSlider(row.Value, row.Minimum, row.Maximum, GUILayout.MinWidth(180f));
                    if (!Mathf.Approximately(next, row.Value)) row.Value = next;
                    GUILayout.Label(row.Value.ToString("F2"), GUILayout.Width(60f));
                    if (GUILayout.Button("戻す", GUILayout.Width(42f))) row.Value = row.Default;
                    GUILayout.EndHorizontal();
                    GUILayout.Label(row.Help, _helpStyle);
                    GUILayout.Space(5f);
                }
                if (_page == 2) DrawState();
            }
            finally { GUILayout.EndScrollView(); }
        }

        private void DrawState()
        {
            bool log = GUILayout.Toggle(_settings.Diagnostics.Value, "対象・パターンの変化をログへ出す");
            if (log != _settings.Diagnostics.Value) _settings.Diagnostics.Value = log;
            if (_latest == null || _latest.Controller == null || _latest.Female == null)
            {
                GUILayout.Label("状態: 女性キャラの標準カメラ注視待ち", _helpStyle);
                return;
            }
            GUILayout.Label("最後の計算: " + _latest.Female.name + " / パターン " + _latest.Pattern, _helpStyle);
            GUILayout.Label("首 上向き: 標準 " + _latest.NativeNeckUp.ToString("F1") + "° → 補正後 " + _latest.EffectiveNeckUp.ToString("F1") + "°", _helpStyle);
            GUILayout.Label("頭 上向き: 標準 " + _latest.NativeHeadUp.ToString("F1") + "° → 補正後 " + _latest.EffectiveHeadUp.ToString("F1") + "°", _helpStyle);
            GUILayout.Label("上下の追従範囲: 標準 " + _latest.NativeField.ToString("F1") + "° → 今の方向 " + _latest.EffectiveField.ToString("F1") + "°", _helpStyle);
            GUILayout.Label("要求角度 X=" + _latest.Pitch.ToString("F1") + "° / Y=" + _latest.Yaw.ToString("F1") + "° / " + (_latest.Broken ? "追従範囲外" : "追従範囲内"), _helpStyle);
            GUILayout.Label("首・頭の配分は標準計算を使い、各骨の可動域とZ補正を別々に変更します。", _helpStyle);
        }

        private void UpdateWindowCapture()
        {
            if (!ShowOwnWindow) { ReleaseWindowCapture(); return; }
            _window.x = Mathf.Clamp(_window.x, 0f, Mathf.Max(0f, Screen.width - _window.width));
            _window.y = Mathf.Clamp(_window.y, 0f, Mathf.Max(0f, Screen.height - 25f));
            Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (Input.GetMouseButtonDown(0) && _window.Contains(mouse)) _pointerHeld = true;
            if (!Input.GetMouseButton(0)) _pointerHeld = false;
            _windowRects[0] = _window;
            UiInputCaptureApi.SetInputBlockRects(InputOwner, _windowRects);
            UiInputCaptureApi.SetIdleCursorUnlock(InputOwner, true);
            UiInputCaptureApi.Sync(InputOwner, "pointer", _pointerHeld);
            _captureRegistered = true;
        }

        private void ReleaseWindowCapture()
        {
            _pointerHeld = false;
            if (_captureRegistered)
            {
                UiInputCaptureApi.ClearInputBlockRects(InputOwner);
                UiInputCaptureApi.SetIdleCursorUnlock(InputOwner, false);
                UiInputCaptureApi.Sync(InputOwner, "pointer", false);
                _captureRegistered = false;
            }
            if (Event.current != null && _ownHotControl != 0 && GUIUtility.hotControl == _ownHotControl)
                GUIUtility.hotControl = 0;
            _ownHotControl = 0;
        }
    }
}
