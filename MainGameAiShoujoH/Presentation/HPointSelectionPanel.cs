using System;
using MainGameAiShoujoH.Contracts;
using MainGameUiInputCapture;
using UnityEngine;

namespace MainGameAiShoujoH.Presentation
{
    internal sealed class HPointSelectionPanel : MonoBehaviour
    {
        Rect window = new Rect(30, 90, 370, 360);
        HPointChoice[] choices = Array.Empty<HPointChoice>();
        string[] labels = Array.Empty<string>();
        Vector3 origin;
        Vector2 scroll;
        Func<bool> active;
        Action<string> move, failure;
        string owner;
        bool visible, drag, focused = true;
        int selected = -1;
        internal void Open(HPointChoice[] values, Vector3 position, Func<bool> canInteract, Action<string> choose, Action<string> report)
        {
            if (!UiInputCaptureApi.IsAvailable) throw new InvalidOperationException("共通UI入力サービスがありません。");
            choices = values; origin = position; active = canInteract; move = choose; failure = report;
            labels = Array.ConvertAll(choices, point => point.Name + "  " + Vector3.Distance(origin, point.Position).ToString("0.0") + "m");
            owner = "ai-h-points:" + GetInstanceID(); visible = true; selected = Array.FindIndex(choices, point => point.Position == position);
            Capture();
        }
        void Capture()
        {
            UiInputCaptureApi.SetIdleCursorUnlock(owner, true);
            UiInputCaptureApi.SetInputBlockRects(owner, new[] { window });
        }
        void OnGUI()
        {
            if (!visible || !focused || active?.Invoke() != true) { if (visible) Clear(); return; }
            window.width = Mathf.Min(370, Screen.width); window.height = Mathf.Min(360, Screen.height);
            window.x = Mathf.Clamp(window.x, 0, Mathf.Max(0, Screen.width - window.width));
            window.y = Mathf.Clamp(window.y, 0, Mathf.Max(0, Screen.height - window.height));
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && window.Contains(e.mousePosition)) drag = true;
            if (e.type == EventType.MouseUp && e.button == 0) drag = false;
            if (e.type == EventType.KeyDown && choices.Length > 0 && GUIUtility.keyboardControl == 0 && window.Contains(e.mousePosition)
                && (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.DownArrow))
            { e.Use(); Choose(Mathf.Clamp(selected + (e.keyCode == KeyCode.UpArrow ? -1 : 1), 0, choices.Length - 1)); }
            UiInputCaptureApi.Sync(owner, "drag", drag); Capture();
            window = GUILayout.Window(GetInstanceID(), window, Draw, "近くのHポイント（10m以内）");
            if (visible) Capture();
        }
        void Draw(int id)
        {
            GUILayout.Label("現在地から10m以内：" + choices.Length + "地点");
            GUILayout.Label("地点を押すと移動し、体位一覧を切り替えます。");
            GUILayout.BeginHorizontal(); GUI.enabled = choices.Length > 0;
            if (GUILayout.Button("前へ ↑", GUILayout.ExpandWidth(false))) Choose(Mathf.Clamp(selected - 1, 0, choices.Length - 1));
            if (GUILayout.Button("次へ ↓", GUILayout.ExpandWidth(false))) Choose(Mathf.Clamp(selected + 1, 0, choices.Length - 1));
            GUI.enabled = true;
            if (GUILayout.Button("閉じる", GUILayout.ExpandWidth(false))) Release();
            GUILayout.EndHorizontal();
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Max(60, window.height - 125)));
            for (int index = 0; index < choices.Length; ++index)
                if (GUILayout.Button(labels[index], GUILayout.ExpandWidth(false))) Choose(index);
            GUILayout.EndScrollView(); GUI.DragWindow(new Rect(0, 0, window.width, 22));
        }
        void Choose(int index)
        {
            selected = index;
            try { move(choices[index].Id); }
            catch (Exception error) { failure("Hポイント移動: " + error.Message); }
        }
        internal void Release() { visible = false; Clear(); }
        void Clear()
        {
            drag = false;
            if (owner == null) return;
            UiInputCaptureApi.Sync(owner, "drag", false); UiInputCaptureApi.ClearInputBlockRects(owner);
            UiInputCaptureApi.SetIdleCursorUnlock(owner, false); UiInputCaptureApi.EndOwner(owner);
        }
        void OnApplicationFocus(bool value) { focused = value; if (!value) Clear(); else if (visible) Capture(); }
        void OnDisable() => Release();
        void OnDestroy() => Release();
    }
}
