using System;
using BepInEx;
using CanonGate;
using MainGameUiInputCapture;
using UnityEngine;

namespace MainGameNotifications
{
    [BepInPlugin(NotificationApi.PluginGuid, "共通通知", "1.0.0")]
    [BepInDependency(GateApi.PluginGuid, "2.4.0")]
    [BepInDependency("com.kks.maingame.uiinputcapture")]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    [BepInProcess("CharaStudio")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private const string Owner = NotificationApi.PluginGuid;
        private readonly NotificationStore _store = new NotificationStore();
        private Notification[] _items = new Notification[0];
        private GateApi.ActivityRegistration _activity;
        private GateApi.ActivityPermit _permit;
        private bool _running, _focused = true, _expanded, _dragging;
        private Rect _rect;
        private Vector2 _scroll;
        private GUIStyle _body;
        private int _width, _height;

        private void Awake()
        {
            NotificationApi.Initialize(_store);
            _store.Changed += Refresh;
            _activity = GateApi.RegisterActivity(Owner, GateApi.ActivityKind.StartupService, StartRuntime, StopRuntime, includeSessions: false);
        }
        private void Start() => _activity.Begin(this);
        private void OnEnable() { if (_activity != null && GateApi.Current == null) _activity.Begin(this); }
        private void StartRuntime() { _permit = _activity.Capture(); _running = true; RefreshBounds(); }
        private void StopRuntime() { _running = false; ReleaseInput(); }
        private void Refresh()
        {
            _items = _store.Snapshot();
            if (_items.Length > 0) _expanded = true;
            RefreshBounds();
        }
        private void RefreshBounds()
        {
            _width = Screen.width; _height = Screen.height;
            float width = Mathf.Min(440, Mathf.Max(180, _width - 24));
            float height = _expanded ? Mathf.Min(380, Mathf.Max(80, _height - 32)) : 40;
            _rect = new Rect(Mathf.Max(0, _width - width - 12), 12, width, height);
            RefreshInput();
        }
        private void RefreshInput()
        {
            if (!_running || !_permit.IsValid || !_focused || _items.Length == 0) { ReleaseInput(); return; }
            UiInputCaptureApi.SetInputBlockRects(Owner, new[] { _rect });
            UiInputCaptureApi.SetIdleCursorUnlock(Owner, true);
        }
        private void ReleaseInput()
        {
            _dragging = false;
            UiInputCaptureApi.Sync(Owner, "pointer", false);
            UiInputCaptureApi.ClearInputBlockRects(Owner);
            UiInputCaptureApi.SetIdleCursorUnlock(Owner, false);
            UiInputCaptureApi.EndOwner(Owner);
        }
        private void OnApplicationFocus(bool focused) { _focused = focused; if (focused) RefreshInput(); else ReleaseInput(); }
        private void OnApplicationPause(bool paused) { _focused = !paused; if (paused) ReleaseInput(); else RefreshInput(); }
        private void OnDisable() { _activity?.End(); ReleaseInput(); }
        private void OnDestroy()
        {
            _activity?.Dispose(); _activity = null;
            ReleaseInput(); _store.Changed -= Refresh; _store.Clear(); NotificationApi.Shutdown();
        }
        private void OnGUI()
        {
            if (!_running || !_permit.IsValid || !_focused || _items.Length == 0) return;
            if (_width != Screen.width || _height != Screen.height) RefreshBounds();
            if (_body == null) _body = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false };
            var e = Event.current;
            if (e.type == EventType.MouseDown && _rect.Contains(e.mousePosition))
            { _dragging = true; UiInputCaptureApi.Sync(Owner, "pointer", true); }
            if ((e.rawType == EventType.MouseUp || e.type == EventType.Ignore) && _dragging)
            { _dragging = false; UiInputCaptureApi.Sync(Owner, "pointer", false); }
            var color = GUI.color; var background = GUI.backgroundColor; var matrix = GUI.matrix; bool wasEnabled = GUI.enabled; int depth = GUI.depth;
            try
            {
                GUI.matrix = Matrix4x4.identity; GUI.color = Color.white; GUI.backgroundColor = Color.white; GUI.enabled = true; GUI.depth = -10000;
                GUILayout.BeginArea(_rect, GUI.skin.box);
                GUILayout.BeginHorizontal();
                GUILayout.Label("通知 " + _items.Length + "件", GUILayout.ExpandWidth(true));
                if (GUILayout.Button(_expanded ? "折りたたむ" : "詳細", GUILayout.Width(100), GUILayout.Height(28)))
                { _expanded = !_expanded; RefreshBounds(); GUIUtility.ExitGUI(); }
                GUILayout.EndHorizontal();
                if (_expanded)
                {
                    _scroll = GUILayout.BeginScrollView(_scroll);
                    // Snapshot is cached only on arrival/update/dismiss; drawing never enumerates plugins or scenes.
                    var displayed = _items;
                    foreach (var n in displayed)
                    {
                        GUILayout.BeginVertical(GUI.skin.box);
                        GUILayout.Label((n.Severity == NotificationSeverity.Failure ? "失敗: " : n.Severity == NotificationSeverity.Warning ? "警告: " : "情報: ") + n.Title, _body);
                        GUILayout.Label(n.Body, _body);
                        GUILayout.Label("通知元: " + n.OwnerGuid, _body);
                        if (GUILayout.Button("確認して閉じる", GUILayout.Width(130), GUILayout.Height(28)))
                        {
                            _store.Dismiss(n.OwnerGuid, n.Id);
                            GUIUtility.ExitGUI();
                        }
                        GUILayout.EndVertical();
                    }
                    GUILayout.EndScrollView();
                }
                GUILayout.EndArea();
                if (_rect.Contains(e.mousePosition) && (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel)) e.Use();
            }
            finally { GUI.color = color; GUI.backgroundColor = background; GUI.matrix = matrix; GUI.enabled = wasEnabled; GUI.depth = depth; }
        }
    }
}
