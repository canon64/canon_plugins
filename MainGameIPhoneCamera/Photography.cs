using System;
using System.IO;
using UnityEngine;

namespace MainGameIPhoneCamera
{
    public sealed partial class Plugin
    {
        private int CaptureTurns()
            => CaptureOrientation.FromGravity(Vector3.Dot(_phoneCamera.transform.right, Vector3.up), Vector3.Dot(_phoneCamera.transform.up, Vector3.up));
        private Texture2D RotateCaptureTexture(Texture2D source, int turns)
        {
            if ((turns & 3) == 0) return source;
            var result = new Texture2D((turns & 1) != 0 ? source.height : source.width, (turns & 1) != 0 ? source.width : source.height, TextureFormat.RGB24, false);
            try
            {
                result.SetPixels32(CaptureOrientation.Rotate(source.GetPixels32(), source.width, source.height, turns));
                result.Apply(false, false);
            }
            catch { Destroy(result); throw; }
            Destroy(source); return result;
        }
        private void CapturePhoto()
        {
            if (!CanAct || _phoneCamera == null || _image == null || _capturing || _app.Busy) return;
            _capturing = true;
            int turns = CaptureTurns();
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                _phoneCamera.Render();
                RenderTexture.active = _image;
                pixels = new Texture2D(_image.width, _image.height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, _image.width, _image.height), 0, 0);
                pixels.Apply(false, false);
                pixels = RotateCaptureTexture(pixels, turns);
                byte[] png = pixels.EncodeToPNG();
                if (png == null || png.Length == 0) throw new InvalidOperationException("PNGが空です");
                Directory.CreateDirectory(PhotoDirectory);
                string path = NewCapturePath(".png");
                using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                    file.Write(png, 0, png.Length);
                _status = "写真保存：" + Path.GetFileName(path) + "（" + pixels.width + "×" + pixels.height + "）";
                HapticPhoto();
                PlayPhoneSound(_shutterSound);
                _cameraUi?.Thumbnail(SnapshotThumbnail(turns));
                _app.Complete(true); _cameraUi?.Apply(_app); _cameraUi?.PhotoFlash();
                PhoneCaptureCompleted();
            }
            catch (Exception ex) { ReportError("写真保存失敗", ex); }
            finally
            {
                RestorePhoneAfterOwnCamera(_phoneCamera);
                RenderTexture.active = previous;
                if (pixels != null) Destroy(pixels);
                _capturing = false;
            }
        }
    }
}
