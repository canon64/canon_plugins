using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace UnityEngine
{
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } public static Vector2 zero => new(); }
    public class Transform { }
    public class RectTransform : Transform { }
    public class GameObject { public Transform transform = new(); }
    public class Texture { }
    public class Texture2D : Texture { public static Texture2D blackTexture = new(); }
    public class Sprite { }
    public class Mesh { }
    public class Camera { public bool enabled; }
    public class Material { public Texture mainTexture; }
    public static class Application { public static bool isFocused = true; }
}
namespace MainGameIPhoneCamera
{
    internal sealed class PhoneIcon { internal string Id, Title; internal Sprite Sprite; }
    internal sealed class PhoneShellUi : IDisposable
    {
        internal int GridColumns => 3;
        internal bool Visible;
        internal int Selection;
        internal bool ControlsVisible, Panning, Moving;
        internal float ZoomAxis, PanX, PanY;
        internal void ViewerUi(bool visible) { ControlsVisible = visible; }
        internal void StopViewerMotion() { Moving = false; }
        internal void ViewerAxis(float x, float y, bool pan) { Moving = x != 0 || y != 0; Panning = pan; ZoomAxis = pan ? 0 : y; PanX = x; PanY = y; }
        internal RectTransform Viewport = new();
        internal PhoneShellUi(Transform t, Mesh mesh) { }
        internal void Active(bool value) { Visible = value; }
        internal void StartPage(string title, string hint, Texture2D wallpaper = null) { Visible = true; }
        internal Sprite BuiltinIcon(int kind) => new();
        internal void Home(System.Collections.Generic.IReadOnlyList<PhoneIcon> apps, int index) { Selection = index; }
        internal void Lock() { }
        internal void Settings(string description) { }
        internal void Message(string description) { }
        internal void Clock(DateTime now) { }
        internal void Select(int index) { Selection = index; }
        internal void EditorValues(Texture2D texture, WallpaperDraft draft) { }
        public void Dispose() { Visible = false; }
    }
    internal sealed class QueueContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<Action> _queue = new();
        public override void Post(SendOrPostCallback callback, object state) => _queue.Enqueue(() => callback(state));
        internal void Drain() { while (_queue.TryDequeue(out var callback)) callback(); }
    }
    public sealed partial class Plugin
    {
        private bool CanAct = true, IsHeld;
        private sealed class Permit { internal bool IsValid = true; }
        private Permit _permit = new();
        private QueueContext _mainThread = new();
        private CancellationTokenSource _captureLifetime = new();
        private int _captureEpoch;
        private GameObject _root = new();
        private Mesh _screenMesh = new();
        private Camera _phoneCamera = new();
        private Material _screenMaterial = new();
        private Texture2D _image = new();
        private sealed class CameraUi
        {
            internal bool Visible;
            internal void Active(bool value) { Visible = value; }
            internal void Apply(CameraAppState state) { }
            internal void SystemClock(DateTime now) { }
        }
        private CameraUi _cameraUi = new();
        private CameraAppState _app = new();
        private object _movie;
        private bool _galleryPlaying, _wallpaperSaving;
        private Texture2D _viewerTexture;
        private WallpaperDraft _wallpaperDraft = new();
        private int _gallerySelection;
        private readonly string[] _galleryItems = new[] { "one.png", "two.mp4" };
        private string _status;
        internal int Shots, Stops, OpenedSettings, ClosedSettings, RefreshedGallery, OpenedMedia = -1, ViewerActions, WallpaperActions;
        private bool TickPhoneOrientation() => CanAct && _root != null && _phone.Page != PhonePage.Off;
        private void StopMovie() { if (_movie == null) return; Stops++; _movie = null; _app.BeginSaving(); UpdatePhoneMediaActivity(); }
        private void StopGalleryVideo() { _galleryPlaying = false; UpdatePhoneMediaActivity(); }
        private void RenderGallery() { _shellUi.StartPage("Photos", ""); }
        private void RenderGalleryLoading() { _shellUi.StartPage("Photos", ""); }
        private void ReloadGallery() { RefreshedGallery++; }
        private void NavigateGallery(int x, int y) { _gallerySelection = PhoneGrid.Move(_gallerySelection, _galleryItems.Length, x, y, 3); }
        private void RenderViewer() { _shellUi.StartPage("Viewer", ""); ShowViewerUi(); }
        private void RenderWallpaperEditor() { _shellUi.StartPage("Wallpaper", ""); }
        private void OpenGalleryItem(int index) { OpenedMedia = index; ShowPhonePage(PhonePage.Viewer); }
        private void ConfirmViewerControl() { ViewerActions++; }
        private void ConfirmWallpaperControl() { WallpaperActions++; }
        private void ClearViewerImage() { _viewerTexture = null; }
        private void ClearGalleryThumbnails() { }
        private string[] ViewerControls() => new[] { "List", "Prev", "Next", "Play", "Back10", "Fwd10" };
        private void LoadWallpaper(bool forLock) { }
        private void SyncWallpaperNumbers() { }
        private void OpenPhoneSettings() { OpenedSettings++; }
        private void ClosePhoneSettings() { ClosedSettings++; }
        private void ApplyCameraZoom() { }
        private void StopCameraZoom() { }
        private void CameraZoomAxis(float x, float y) { _app.Axis(x, y); }
        private void ShutterAction() { Shots++; }
        private static void Destroy(object item) { }
    }
}
