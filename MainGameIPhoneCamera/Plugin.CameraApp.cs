using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MainGameIPhoneCamera
{
    public sealed partial class Plugin
    {
        private readonly CameraAppState _app = new CameraAppState();
        private PhoneCameraUi _cameraUi;
        private SynchronizationContext _mainThread;
        private CancellationTokenSource _captureLifetime;
        private int _captureEpoch;
        private MovieWriter _movie;
        private Task<string> _finishing;
        private GameAudioTap _audioTap;
        private RenderTexture _movieImage;
        private Texture2D _moviePixels, _movieThumbnail;
        private int _lastMovieFrame = -1, _recordFps;
        private string _zoomText = "1.00";
        private bool IsSavingMovie => _finishing != null && !_finishing.IsCompleted;

        private void SelectCameraMode(CaptureMode mode)
        {
            if (!CanAct || _root == null) return;
            if (_phone.Page != PhonePage.Camera) ShowPhonePage(PhonePage.Camera);
            if (!_app.Select(mode)) return;
            _cameraUi?.Apply(_app);
            _status = mode == CaptureMode.Photo ? "写真：トリガーで撮影" : "ビデオ：トリガーで録画開始";
        }
        private void SetCameraZoom(float zoom)
        {
            StopCameraZoom();
            if (!CanAct || !_app.SetZoom(zoom)) return;
            ApplyCameraZoom(); _cameraUi?.Apply(_app);
        }
        private void ApplyCameraZoom()
        {
            if (_phoneCamera != null)
                _phoneCamera.fieldOfView = PhoneZoomPlan.Fov(_fov.Value, _app.Zoom);
            _zoomText = _app.Zoom.ToString("0.00");
        }
        private void ShutterAction()
        {
            _zoomMotion?.Sample();
            if (!CanAct || _root == null || _app.Phase == CapturePhase.Saving || IsSavingMovie) return;
            if (_phone.Page != PhonePage.Camera) ShowPhonePage(PhonePage.Camera);
            if (_app.Mode == CaptureMode.Photo) CapturePhoto();
            else if (_movie != null) StopMovie();
            else StartMovie();
        }
        private string NewCapturePath(string extension)
            => Path.Combine(PhotoDirectory, "iPhone_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + System.Guid.NewGuid().ToString("N").Substring(0, 6) + extension);

        private void StartMovie()
        {
            if (!CanAct || _phoneCamera == null || _image == null || _movie != null || IsSavingMovie || _app.Mode != CaptureMode.Video || _app.Busy) return;
            MovieWriter writer = null;
            try
            {
                if (_mainThread == null) throw new InvalidOperationException("Unityの完了通知先を取得できません");
                var listener = FindObjectsOfType<AudioListener>().FirstOrDefault(x => x.isActiveAndEnabled);
                if (listener == null) throw new InvalidOperationException("有効なゲーム音声出力がありません");
                Directory.CreateDirectory(PhotoDirectory);
                var size = PhotoSize.FromHeight(_movieHeight.Value);
                // Fixed for this writer's whole lifetime; never re-read orientation per frame.
                int turns = CaptureTurns();
                _movieImage = new RenderTexture(size.Width, size.Height, 0, RenderTextureFormat.ARGB32);
                if (!_movieImage.Create()) throw new InvalidOperationException("録画用映像を作成できません");
                _moviePixels = new Texture2D(size.Width, size.Height, TextureFormat.RGB24, false);
                _phoneCamera.Render();
                _movieThumbnail = SnapshotThumbnail(turns);
                writer = new MovieWriter(_ffmpegPath.Value, NewCapturePath(".mp4"), size.Width, size.Height, _movieFps.Value, AudioSettings.outputSampleRate, turns);
                _audioTap = listener.gameObject.AddComponent<GameAudioTap>();
                _audioTap.Bind(writer); _audioTap.Lost = AudioOutputLost;
                _movie = writer; _lastMovieFrame = -1; _recordFps = _movieFps.Value;
                _app.BeginRecording();
                UpdatePhoneMediaActivity();
                AudioSettings.OnAudioConfigurationChanged += AudioConfigurationChanged;
                Camera.onPostRender += CaptureMovieFrame;
                _cameraUi?.Clock(0); _cameraUi?.Apply(_app);
                _status = "録画中：トリガーで停止／スティック上下でズーム";
                CaptureMovieFrame(_phoneCamera);
                if (_movie != null) PlayPhoneSound(_recordStartSound);
                HapticPhoto();
            }
            catch (Exception ex)
            {
                if (_movie == null && writer != null) _movie = writer;
                StopMovie();
                if (writer == null && _movieThumbnail != null) { Destroy(_movieThumbnail); _movieThumbnail = null; }
                _app.Complete(false); _cameraUi?.Apply(_app);
                _status = "録画開始失敗：" + ex.Message;
            }
        }
        private void CaptureMovieFrame(Camera camera)
        {
            if (camera != _phoneCamera || _movie == null || _capturing) return;
            if (!CanAct) { StopMovie(); return; }
            if (_movie.Error != null) { string error = _movie.Error; StopMovie(); _status = error; return; }
            _cameraUi?.Clock(_movie.Seconds);
            int frame = (int)(_movie.Seconds * _recordFps);
            if (frame <= _lastMovieFrame) return;
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(_image, _movieImage);
                RenderTexture.active = _movieImage;
                _moviePixels.ReadPixels(new Rect(0, 0, _movieImage.width, _movieImage.height), 0, 0, false);
                byte[] pixels = _moviePixels.GetRawTextureData();
                if (!_movie.FrameBytes(pixels, frame)) { StopMovie(); return; }
                _lastMovieFrame = frame;
            }
            catch (Exception ex) { StopMovie(); _status = "録画停止：" + ex.Message; }
            finally { RenderTexture.active = previous; }
        }
        private void StopMovie()
        {
            Camera.onPostRender -= CaptureMovieFrame;
            AudioSettings.OnAudioConfigurationChanged -= AudioConfigurationChanged;
            if (_audioTap != null)
            {
                _audioTap.Lost = null; _audioTap.Bind(null); Destroy(_audioTap); _audioTap = null;
            }
            var writer = _movie; _movie = null;
            UpdatePhoneMediaActivity();
            if (_moviePixels != null) { Destroy(_moviePixels); _moviePixels = null; }
            if (_movieImage != null) { _movieImage.Release(); Destroy(_movieImage); _movieImage = null; }
            if (writer == null) return;
            PlayPhoneSound(_recordStopSound);
            _app.BeginSaving(); _status = "音声付きMP4を保存中…";
            int epoch = _captureEpoch;
            var permit = _permit;
            var token = _captureLifetime?.Token ?? new CancellationToken(true);
            _finishing = writer.FinishAsync();
            _finishing.ContinueWith(task =>
            {
                string error = task.IsFaulted ? task.Exception.GetBaseException().Message : null;
                _mainThread.Post(_ =>
                {
                    if (token.IsCancellationRequested || epoch != _captureEpoch || !CanAct || !permit.IsValid || _root == null) return;
                    bool success = error == null;
                    _app.Complete(success);
                    if (success)
                    {
                        _cameraUi?.Thumbnail(_movieThumbnail); _movieThumbnail = null;
                        _status = "動画保存：" + Path.GetFileName(task.Result); HapticPhoto();
                        PhoneCaptureCompleted();
                    }
                    else
                    {
                        if (_movieThumbnail != null) { Destroy(_movieThumbnail); _movieThumbnail = null; }
                        _status = "動画保存失敗：" + error;
                    }
                    _cameraUi?.Apply(_app);
                }, null);
            }, TaskScheduler.Default);
            _cameraUi?.Apply(_app);
        }
        private void AudioConfigurationChanged(bool deviceChanged) { if (_movie != null) { StopMovie(); _status = "音声出力の変更により録画を停止しました。"; } }
        private void AudioOutputLost() { if (_movie != null) { StopMovie(); _status = "ゲーム音声出力の終了により録画を停止しました。"; } }
        private Texture2D SnapshotThumbnail(int turns = 0)
        {
            var temporary = RenderTexture.GetTemporary(112, 244, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                Graphics.Blit(_image, temporary); RenderTexture.active = temporary;
                pixels = new Texture2D(112, 244, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 112, 244), 0, 0); pixels.Apply();
                pixels = RotateCaptureTexture(pixels, turns); return pixels;
            }
            catch { if (pixels != null) Destroy(pixels); throw; }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary); }
        }
    }
}
