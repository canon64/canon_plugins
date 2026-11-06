using System;
using System.Collections.Generic;
using UnityEngine;
using MapEnvironmentRuntime;

namespace MainGameAiShoujoMode
{
    // Reuse SceneCanvas; the installed EyeCatch supplies its screenshot automatically.
    internal sealed class AiModeLoadingScreen : IDisposable
    {
        internal StartupTrace StartupDiagnostics { get; set; }
        private readonly SceneFadeCanvas _fade;
        private readonly HashSet<GameObject> _roots = new HashSet<GameObject>();
        private readonly HashSet<Camera> _cameras = new HashSet<Camera>();
        private readonly AiModeVrResources _vr;
        private readonly Action<string> _log;
        private readonly Func<bool> _diagnostics;
        private bool Diagnostics => _diagnostics == null || _diagnostics();
        private Action<FadeCanvas.Fade> _completion;
        private bool _owned, _covered;
        internal AiModeLoadingScreen(Action<string> log, Func<bool> diagnostics = null)
            : this(log, diagnostics, new AiModeVrResources()) { }
        internal AiModeLoadingScreen(Action<string> log, Func<bool> diagnostics, AiModeVrResources vr)
        {
            _log = log; _diagnostics = diagnostics; _fade = Manager.Scene.sceneFadeCanvas;
            if (_fade == null || _fade.canvas == null) throw new InvalidOperationException("KKSの標準ロード画面がありません。");
            Keep(_fade.transform.root.gameObject);
            var icon = Manager.Scene.loadingIconJob;
            if (icon != null) Keep(icon.transform.root.gameObject);
            _vr = vr;
            foreach (var root in _vr.Roots)
            {
                Keep(root);
                foreach (var camera in root.GetComponentsInChildren<Camera>(true)) _cameras.Add(camera);
            }
            if (Diagnostics) _log("AI_MODE_STANDARD_LOADING_READY canvas=" + _fade.canvas.name + " root=" + _fade.transform.root.name + " renderMode=" + _fade.canvas.renderMode);
        }
        private void Keep(GameObject root)
        {
            _roots.Add(root);
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) if (canvas.worldCamera != null) _cameras.Add(canvas.worldCamera);
        }
        internal bool Protect(GameObject root) => _roots.Contains(root);
        internal bool Protect(Camera camera) => _cameras.Contains(camera);
        internal bool Protect(AudioListener listener) => _vr.Protect(listener);
        internal bool BindView(Camera camera)
        {
            _vr.Diagnostics = StartupDiagnostics;
            return _vr.BindView(camera);
        }
        internal void ObserveNextRender(string stage, GameObject root = null)
        {
            if (StartupDiagnostics?.Enabled != true) return;
            StartupDiagnostics.ArmNextRender(stage, _vr.RenderCamera ?? Camera.main, root);
        }
        internal bool HasVr => _vr.Active;
        internal Camera NativeRenderCamera => _vr.RenderCamera;
        internal Transform NativeOrigin => _vr.Origin;
        internal void CaptureNativeView() => _vr.CaptureNativeView();
        internal void RestoreView() => _vr.RestoreView();
        internal void Cover(Action covered)
        {
            StartupDiagnostics?.Mark("fade.cover.request", () => "covered=" + _covered + " owned=" + _owned + " nativeEnd=" + (_fade != null && _fade.isEnd));
            if (_covered) { covered(); return; }
            if (_fade == null || !_fade.isEnd) throw new InvalidOperationException("標準ロード画面は別の遷移を処理しています。");
            _owned = true;
            Start(FadeCanvas.Fade.In, () => { _covered = true; StartupDiagnostics?.Mark("fade.cover.complete"); if (Diagnostics) _log("AI_MODE_STANDARD_LOADING_SHOWN"); covered(); });
        }
        internal void Reveal(Action revealed)
        {
            StartupDiagnostics?.Mark("fade.reveal.request", () => "owned=" + _owned + " nativeEnd=" + (_fade != null && _fade.isEnd));
            if (!_owned || _fade == null) { revealed(); return; }
            Start(FadeCanvas.Fade.Out, () => { _covered = false; _owned = false; StartupDiagnostics?.Mark("fade.reveal.complete"); if (Diagnostics) _log("AI_MODE_STANDARD_LOADING_HIDDEN"); revealed(); });
        }
        internal void RevealAfterH(Action revealed)
        {
            // HScene covers the screen itself; it does not transfer ownership through Cover.
            // Adopt that cover explicitly so an exit without FreeHScene still reveals the island.
            if (_fade == null) { revealed(); return; }
            _owned = true;
            Reveal(revealed);
        }
        private void Start(FadeCanvas.Fade direction, Action completed)
        {
            if (_completion != null) throw new InvalidOperationException("標準ロード画面の完了待ちが重複しました。");
            _completion = type =>
            {
                if (type != direction) return;
                _fade.onComplete -= _completion; _completion = null; completed();
            };
            _fade.onComplete += _completion;
            try { StartupDiagnostics?.Mark("fade.native-start.begin", () => direction.ToString()); _fade.StartFade(direction); StartupDiagnostics?.Mark("fade.native-start.returned", () => direction.ToString()); }
            catch { _fade.onComplete -= _completion; _completion = null; throw; }
        }
        public void Dispose()
        {
            _vr.RestoreView();
            if (_fade == null) return;
            if (_completion != null) { _fade.onComplete -= _completion; _completion = null; }
            if (_owned) { _owned = false; _covered = false; _fade.StartFade(FadeCanvas.Fade.Out); }
        }
    }
}
