using System;
using System.Collections.Generic;
using MainGameBlankMapAdd;
using UnityEngine;

Plugin.Verify();

namespace MainGameBlankMapAdd
{
    // Only Unity and the playlist/decoder boundary are substituted. The linked
    // production session owner and resource scope execute unchanged.
    public sealed partial class Plugin
    {
        private bool _sessionStarted;
        private readonly Transform transform = new Transform();
        private readonly Settings _settings = new Settings();
        private Transport _mainVideoPlayer;
        private object _videoRoomAudioSource;
        private readonly Dictionary<string, Transport> _videoBindings = new();
        private enum FadePhase { None, ToBlack }
        private FadePhase _fadePhase;
        private string _fadePendingPath;
        private bool pending, deferred;
        private int attaches, scans;
        private sealed class Settings { internal bool FolderPlayEnabled; internal string VideoPath; }
        private sealed class Transport { internal string Path; internal double Time; internal float Volume; internal bool Playing; }
        private void ApplyFolderVideoPath() { scans++; _settings.VideoPath = "folder/song.mp4"; }
        private string ResolveVideoPath(string path) => path;
        private bool IsWebCamUrl(string path) => path.StartsWith("webcam://");
        private void TryAttachVideo(Material material, string path, string name, bool audio)
        {
            attaches++;
            _mainVideoPlayer = new Transport { Path = path, Volume = .6f };
            _videoRoomAudioSource = new object();
            _videoBindings.Add(path, _mainVideoPlayer);
        }
        private void ClearPendingVideoStarts() => pending = false;
        private void ClearDeferredPlayFromBar() => deferred = false;
        private void DisposeVideoBindings() { foreach (var transport in _videoBindings.Values) transport.Playing = false; }

        internal static void Verify()
        {
            int checks = 0;
            void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
            var plugin = new Plugin();
            CanonPlugins.Shared.PluginSessionGate.Allowed = true;
            plugin.EnsureSessionPlayback();
            Check(plugin._playbackResourceScope == null, "no resources before session start");
            plugin._sessionStarted = true;
            CanonPlugins.Shared.PluginSessionGate.Allowed = false;
            plugin.EnsureSessionPlayback();
            Check(plugin._playbackResourceScope == null, "no resources without gate permission");
            CanonPlugins.Shared.PluginSessionGate.Allowed = true;
            plugin.EnsureSessionPlayback();
            var scope = plugin._playbackResourceScope;
            var root = scope.Root;
            Check(root.transform.Parent == plugin.transform, "transport root belongs to plugin, independently of map and wall");
            Check(plugin.attaches == 0, "empty playlist does not create a decoder");
            plugin._settings.FolderPlayEnabled = true;
            plugin.EnsureSessionPlayback();
            var player = plugin._mainVideoPlayer;
            Check(player.Path == "folder/song.mp4" && plugin.scans == 1, "uses existing folder selection");
            player.Time = 73.25; player.Volume = .35f; player.Playing = true;
            plugin.EnsureSessionPlayback();
            Check(ReferenceEquals(player, plugin._mainVideoPlayer) && plugin.attaches == 1, "repeated ensure never replaces decoder");
            Check(player.Time == 73.25 && player.Volume == .35f && player.Playing, "position volume and playing state survive");
            player.Playing = false;
            plugin.EnsureSessionPlayback();
            Check(!player.Playing && plugin.scans == 1, "paused state and selection remain untouched");
            var texture = scope.Own(new RenderTexture());
            plugin.pending = plugin.deferred = true;
            plugin._fadePhase = FadePhase.ToBlack; plugin._fadePendingPath = "next.mp4";
            plugin._sessionStarted = false;
            plugin.DestroySessionPlayback();
            Check(scope.IsDisposed && !scope.Owns(scope.Generation), "old generation invalidated for delayed callbacks");
            Check(root.Destroyed && texture.Released && texture.Destroyed, "session releases root and textures");
            Check(plugin._videoBindings.Count == 0 && plugin._mainVideoPlayer == null && plugin._videoRoomAudioSource == null, "session clears decoder and output references");
            Check(!plugin.pending && !plugin.deferred && plugin._fadePhase == FadePhase.None && plugin._fadePendingPath == null, "deferred starts and song fades cancelled");
            plugin.DestroySessionPlayback();
            Check(plugin._playbackResourceScope == null, "repeated stop is safe");
            plugin._sessionStarted = true;
            plugin.EnsureSessionPlayback();
            Check(plugin._playbackResourceScope.Generation != scope.Generation && !ReferenceEquals(player, plugin._mainVideoPlayer), "next H has fresh ownership");
            plugin.DestroySessionPlayback();
            plugin._settings.FolderPlayEnabled = false; plugin._settings.VideoPath = "webcam://camera";
            int before = plugin.attaches;
            plugin.EnsureSessionPlayback();
            Check(plugin.attaches == before, "webcam is not opened as background music");
            plugin.DestroySessionPlayback();
            Console.WriteLine($"PASS {checks} session playback lifecycle checks");
        }
    }
}
namespace CanonPlugins.Shared { internal static class PluginSessionGate { internal static bool Allowed; } }
namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public static void Destroy(Object value) { if (value != null) value.Destroyed = true; }
    }
    public class Transform { public Transform Parent; public void SetParent(Transform parent, bool world) { Parent = parent; } }
    public class GameObject : Object { public readonly Transform transform = new(); public GameObject(string name) { } }
    public class RenderTexture : Object { public bool Released; public void Release() { Released = true; } }
    public class Mesh : Object { }
    public class Material : Object { }
}
