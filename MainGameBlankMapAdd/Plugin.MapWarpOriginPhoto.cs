using System;
using System.IO;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private string MapWarpOriginPhotoDirectory => Path.Combine(_pluginDir, "MapWarpOriginImages");

        private void DrawMapWarpOriginPhoto(MapWarpPoint origin)
        {
            string cacheKey = "origin:" + origin.Id;
            if (!_hPlaceImages.TryGetValue(cacheKey, out var image))
            {
                image = null;
                try
                {
                    string path = MapWarpOrigin.PhotoPath(MapWarpOriginPhotoDirectory, origin);
                    if (File.Exists(path))
                    {
                        image = new Texture2D(2, 2, TextureFormat.RGB24, false);
                        if (!image.LoadImage(File.ReadAllBytes(path))) { Destroy(image); image = null; }
                    }
                }
                catch { if (image != null) Destroy(image); image = null; }
                _hPlaceImages[cacheKey] = image;
            }
            if (image != null)
            {
                Rect preview = GUILayoutUtility.GetRect(256f, 144f, GUILayout.Width(256f), GUILayout.Height(144f));
                GUI.DrawTexture(preview, image, ScaleMode.ScaleToFit);
            }
        }

        private void SaveMapWarpOriginPhoto(string key)
        {
            if (!MapWarpActionAllowed(key, out _, out _)) return;
            var origin = MapWarpOrigin.Find(_mapWarpDocument, key);
            if (origin == null) return;
            try
            {
                // Only the image changes: neither the original location nor its camera is rewritten.
                MapWarpOrigin.SavePhoto(MapWarpOriginPhotoDirectory, origin, CaptureHPlacePng());
                string cacheKey = "origin:" + origin.Id;
                if (_hPlaceImages.TryGetValue(cacheKey, out var image) && image != null) Destroy(image);
                _hPlaceImages.Remove(cacheKey);
                _mapWarpError = "元の場所のスクショだけを更新しました。配置は変更していません。";
            }
            catch (Exception ex) { _mapWarpError = "スクショを保存できません：" + ex.Message; }
        }
    }
}
