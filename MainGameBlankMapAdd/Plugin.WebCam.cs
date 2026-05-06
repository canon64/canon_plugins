using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private sealed class WebCamBinding
        {
            public WebCamTexture Texture;
            public readonly List<Material> Materials = new List<Material>();
            public string DeviceName;
            public int RequestedWidth;
            public int RequestedHeight;
            public int RequestedFps;
            public float NextStatusLogTime;
            public float NextBlackSampleTime;
            public int RetryCount;
            public bool LoggedFirstFrame;
        }

        private const string WebCamScheme = "webcam://";
        private readonly Dictionary<string, WebCamBinding> _webCamBindings =
            new Dictionary<string, WebCamBinding>(StringComparer.OrdinalIgnoreCase);

        private static bool IsWebCamUrl(string path)
        {
            return path != null &&
                   path.StartsWith(WebCamScheme, StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractWebCamDeviceName(string path)
        {
            return path.Substring(WebCamScheme.Length).Trim();
        }

        // ── 起動時にデバイス一覧をログ出力 ───────────────────────────────
        private void LogAvailableWebCamDevices()
        {
            var devices = WebCamTexture.devices;
            var lines = new System.Text.StringBuilder();
            lines.AppendLine("# 利用可能なWebCamデバイス一覧");
            lines.AppendLine("# VideoPath に webcam://デバイス名 と入力してください");
            lines.AppendLine();

            if (devices == null || devices.Length == 0)
            {
                lines.AppendLine("(デバイスなし)");
            }
            else
            {
                foreach (var d in devices)
                    lines.AppendLine($"webcam://{d.name}");
            }

            string path = System.IO.Path.Combine(_pluginDir, "webcam_devices.txt");
            System.IO.File.WriteAllText(path, lines.ToString(), System.Text.Encoding.UTF8);
            LogInfo($"[webcam] デバイス一覧を出力: {path}");
        }

        // ── WebCamTexture をマテリアルに接続 ─────────────────────────────
        private void TryAttachWebCam(Material material, string deviceName)
        {
            if (material == null || string.IsNullOrWhiteSpace(deviceName)) return;

            if (!_webCamBindings.TryGetValue(deviceName, out var binding) ||
                binding?.Texture == null)
            {
                int requestedWidth = Mathf.Max(16, _settings?.WebCamRequestedWidth ?? 1920);
                int requestedHeight = Mathf.Max(16, _settings?.WebCamRequestedHeight ?? 1080);
                int requestedFps = Mathf.Clamp(_settings?.WebCamRequestedFps ?? 30, 1, 60);

                var tex = new WebCamTexture(deviceName, requestedWidth, requestedHeight, requestedFps)
                {
                    wrapMode = TextureWrapMode.Clamp,
                };
                tex.Play();

                binding = new WebCamBinding
                {
                    Texture = tex,
                    DeviceName = deviceName,
                    RequestedWidth = requestedWidth,
                    RequestedHeight = requestedHeight,
                    RequestedFps = requestedFps,
                    NextStatusLogTime = Time.unscaledTime,
                    NextBlackSampleTime = Time.unscaledTime + 1f,
                };
                _webCamBindings[deviceName] = binding;
                LogInfo(
                    $"[webcam] started device=\"{deviceName}\" requested={requestedWidth}x{requestedHeight}@{requestedFps} " +
                    $"isPlaying={tex.isPlaying} width={tex.width} height={tex.height}");
            }

            if (!binding.Materials.Contains(material))
                binding.Materials.Add(material);

            material.mainTexture = binding.Texture;
            if (material.HasProperty("_EmissionMap"))
                material.SetTexture("_EmissionMap", binding.Texture);

            LogInfo($"[webcam] material bound device=\"{deviceName}\"");
        }

        // ── Update で未再生テクスチャを再試行 ───────────────────────────
        private float _nextWebCamRetryTime;

        private void UpdateWebCamTextures()
        {
            if (_webCamBindings.Count == 0) return;
            if (Time.unscaledTime < _nextWebCamRetryTime) return;
            _nextWebCamRetryTime = Time.unscaledTime + 1f;

            foreach (var kv in _webCamBindings)
            {
                var binding = kv.Value;
                var tex = binding?.Texture;
                if (tex == null) continue;

                LogWebCamStatus(binding, tex);

                if (!tex.isPlaying)
                {
                    binding.RetryCount++;
                    tex.Play();
                    bool listed = HasWebCamDevice(kv.Key);
                    LogWarn(
                        $"[webcam] retry Play device=\"{kv.Key}\" retry={binding.RetryCount} listed={listed} " +
                        $"isPlaying={tex.isPlaying} width={tex.width} height={tex.height}");
                }
            }
        }

        private void LogWebCamStatus(WebCamBinding binding, WebCamTexture tex)
        {
            if (binding == null || tex == null) return;

            float statusInterval = Mathf.Max(0.25f, _settings?.WebCamStatusLogIntervalSec ?? 1f);
            if (Time.unscaledTime >= binding.NextStatusLogTime)
            {
                binding.NextStatusLogTime = Time.unscaledTime + statusInterval;
                bool listed = HasWebCamDevice(binding.DeviceName);
                LogInfo(
                    $"[webcam-status] device=\"{binding.DeviceName}\" listed={listed} " +
                    $"requested={binding.RequestedWidth}x{binding.RequestedHeight}@{binding.RequestedFps} " +
                    $"isPlaying={tex.isPlaying} didUpdate={tex.didUpdateThisFrame} width={tex.width} height={tex.height} " +
                    $"rotation={tex.videoRotationAngle} mirrored={tex.videoVerticallyMirrored}");
            }

            if (!tex.isPlaying || tex.width <= 16 || tex.height <= 16)
                return;

            if (!binding.LoggedFirstFrame && tex.didUpdateThisFrame)
            {
                binding.LoggedFirstFrame = true;
                LogInfo(
                    $"[webcam-frame] first-frame device=\"{binding.DeviceName}\" size={tex.width}x{tex.height} " +
                    $"rotation={tex.videoRotationAngle} mirrored={tex.videoVerticallyMirrored}");
            }

            float sampleInterval = Mathf.Max(0.5f, _settings?.WebCamBlackSampleIntervalSec ?? 2f);
            if (Time.unscaledTime < binding.NextBlackSampleTime)
                return;

            binding.NextBlackSampleTime = Time.unscaledTime + sampleInterval;
            SampleWebCamBrightness(binding, tex);
        }

        private void SampleWebCamBrightness(WebCamBinding binding, WebCamTexture tex)
        {
            try
            {
                Color32[] pixels = tex.GetPixels32();
                if (pixels == null || pixels.Length == 0)
                {
                    LogWarn($"[webcam-sample] empty-pixels device=\"{binding.DeviceName}\"");
                    return;
                }

                int stride = Mathf.Max(1, pixels.Length / 64);
                int samples = 0;
                int min = 255;
                int max = 0;
                long sum = 0;
                for (int i = 0; i < pixels.Length; i += stride)
                {
                    Color32 c = pixels[i];
                    int luminance = (c.r * 299 + c.g * 587 + c.b * 114) / 1000;
                    if (luminance < min) min = luminance;
                    if (luminance > max) max = luminance;
                    sum += luminance;
                    samples++;
                }

                int avg = samples > 0 ? (int)(sum / samples) : 0;
                string label = max <= 2 ? "near-black" : "ok";
                LogInfo(
                    $"[webcam-sample] device=\"{binding.DeviceName}\" didUpdate={tex.didUpdateThisFrame} " +
                    $"samples={samples} avg={avg} min={min} max={max} state={label}");
            }
            catch (Exception ex)
            {
                LogWarn($"[webcam-sample] failed device=\"{binding.DeviceName}\": {ex.Message}");
            }
        }

        private static bool HasWebCamDevice(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
                return false;

            var devices = WebCamTexture.devices;
            if (devices == null)
                return false;

            for (int i = 0; i < devices.Length; i++)
            {
                if (string.Equals(devices[i].name, deviceName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        // ── 破棄 ─────────────────────────────────────────────────────────
        private void DestroyWebCamTextures()
        {
            foreach (var kv in _webCamBindings)
            {
                try
                {
                    if (kv.Value?.Texture != null)
                    {
                        kv.Value.Texture.Stop();
                        Destroy(kv.Value.Texture);
                    }
                }
                catch (Exception ex)
                {
                    LogWarn($"[webcam] destroy failed device=\"{kv.Key}\": {ex.Message}");
                }
            }
            _webCamBindings.Clear();
            LogInfo("[webcam] all textures destroyed");
        }
    }
}
