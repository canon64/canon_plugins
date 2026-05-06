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
                var tex = new WebCamTexture(deviceName)
                {
                    requestedFPS = 30,
                    wrapMode     = TextureWrapMode.Clamp,
                };
                tex.Play();

                binding = new WebCamBinding { Texture = tex };
                _webCamBindings[deviceName] = binding;
                LogInfo($"[webcam] started device=\"{deviceName}\" isPlaying={tex.isPlaying}");
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
                var tex = kv.Value?.Texture;
                if (tex == null) continue;
                if (!tex.isPlaying)
                {
                    tex.Play();
                    LogInfo($"[webcam] retry Play device=\"{kv.Key}\" isPlaying={tex.isPlaying}");
                }
            }
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
