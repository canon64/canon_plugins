using System;
using MainGameUiInputCapture;
using UnityEngine;

namespace MainGameAiShoujoH.Presentation
{
    internal sealed class EndingTextPanel : MonoBehaviour
    {
        private string owner, speaker, text;
        private Action advanced;
        private bool focused = true;
        private Rect bounds;
        internal void Show(string ownerId, string name, string line, Action next)
        {
            if (!UiInputCaptureApi.IsAvailable) throw new InvalidOperationException("Ending caption requires the common UI input service.");
            owner = ownerId; speaker = name; text = line; advanced = next ?? throw new ArgumentNullException(nameof(next));
            bounds = new Rect(24, Mathf.Max(0, Screen.height - 200), Mathf.Max(240, Screen.width - 48), 176);
            Capture();
        }
        private void Capture()
        {
            if (!focused || advanced == null || owner == null) return;
            UiInputCaptureApi.SetIdleCursorUnlock(owner, true);
            UiInputCaptureApi.SetInputBlockRects(owner, new[] { bounds });
        }
        private void OnGUI()
        {
            if (!focused || advanced == null) return;
            GUILayout.BeginArea(bounds, GUI.skin.box);
            if (!string.IsNullOrEmpty(speaker)) GUILayout.Label(speaker);
            GUILayout.Label(text ?? string.Empty, GUILayout.ExpandHeight(true));
            if (GUILayout.Button("次へ", GUILayout.Width(120), GUILayout.Height(32)))
            {
                var next = advanced; Hide(); next();
            }
            GUILayout.EndArea();
            Capture();
        }
        internal void Hide()
        {
            advanced = null; speaker = text = null;
            ClearCapture();
        }
        private void ClearCapture()
        {
            if (owner == null) return;
            UiInputCaptureApi.ClearInputBlockRects(owner); UiInputCaptureApi.SetIdleCursorUnlock(owner, false); UiInputCaptureApi.EndOwner(owner);
        }
        private void OnApplicationFocus(bool value) { focused = value; if (value) Capture(); else ClearCapture(); }
        private void OnEnable() => Capture();
        private void OnDisable() => ClearCapture();
        private void OnDestroy() => Hide();
    }
}
