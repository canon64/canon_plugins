using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    public sealed partial class AiModeSession
    {
        private Authoring.EventAuthoringEditor _authoring;
        private void InitializeAuthoring(string content)
        {
            bool blockedByEditor = false, previousWalker = false, previousCursor = false, previousExternal = false;
            _authoring = Authoring.EventAuthoringEditor.Create(this, content,
                () => !_loading && !_closing && _hSession?.Busy != true,
                () => _host.Walker.ViewCamera,
                binding => binding == "player" ? _playerAvatar.Actor : binding == "current" ? _animationTrial.CurrentActor : null,
                () => _animationTrial.CurrentActor == null ? null : _animationTrial.PauseForExternalMotion(),
                blocked =>
                {
                    if (_host?.Session == null) return;
                    var walker = _host.Walker;
                    if (blocked && !blockedByEditor) { previousExternal = _host.Session.ExternalInputBlocked; previousWalker = walker != null && walker.enabled; previousCursor = walker != null && walker.Snapshot().cursorReleaseRequested; blockedByEditor = true; }
                    _host.Session.ExternalInputBlocked = blocked || previousExternal || _conversation?.Active == true;
                    if (walker != null) { walker.enabled = blocked ? false : previousWalker; walker.SetCursorReleased(blocked || previousCursor); }
                    if (!blocked) blockedByEditor = false;
                },
                Authoring.AuthoringMapCatalog.Build(content, _host.Island.Root));
            _conversation.AttachAuthoring(_authoring);
        }
    }
    internal sealed partial class HotelModeSession
    {
        private Authoring.EventAuthoringEditor _authoring;
        private void InitializeAuthoring(string content)
        {
            bool blockedByEditor = false, previousWalker = false, previousCursor = false, previousPanel = false;
            _authoring = Authoring.EventAuthoringEditor.Create(this, content,
                () => !_loading && !_closing && !_changingFloor && _hSession?.Busy != true,
                () => _walker.ViewCamera,
                binding => binding == "current" || binding == "reception" ? _receptionist?.Actor : null,
                () => null,
                blocked =>
                {
                    if (_walker == null) return;
                    if (blocked && !blockedByEditor) { previousWalker = _walker.enabled; previousCursor = _walker.Snapshot().cursorReleaseRequested; previousPanel = _context.Panel; blockedByEditor = true; }
                    _context.SetPanel(blocked || previousPanel); _walker.enabled = blocked ? false : previousWalker; _walker.SetCursorReleased(blocked || previousCursor);
                    if (!blocked) blockedByEditor = false;
                },
                Authoring.AuthoringMapCatalog.Build(content, null, _floors));
        }
    }
}

namespace MainGameAiShoujoMode.Authoring
{
    internal static class AuthoringMapCatalog
    {
        internal static List<StageMap> Build(string content, GameObject island, IEnumerable<HotelFloorRuntime> hotel = null)
        {
            var result = new List<StageMap>();
            if (island != null) result.Add(new StageMap { Id = "island", Name = "AI少女の島", Roots = new[] { island } });
            if (hotel != null) foreach (var floor in hotel) result.Add(new StageMap { Id = "hotel:" + floor.Definition.id, Name = "ホテル / " + (floor.Definition.room?.label ?? floor.Definition.id), Scene = floor.Scene.name, Roots = floor.Roots });
            var manifestPath = Path.Combine(content, "Hotel", "floors.json");
            if (hotel == null && File.Exists(manifestPath))
            {
                var manifest = JsonCodec.FromJson<HotelMapManifest>(File.ReadAllText(manifestPath)); manifest.Validate();
                foreach (var floor in manifest.floors)
                {
                    var runtime = new HotelFloorRuntime(floor);
                    result.Add(new StageMap { Id = "hotel:" + floor.id, Name = "ホテル / " + (floor.room?.label ?? floor.id), Scene = floor.scene,
                        Load = () => runtime.Load(Path.Combine(content, "Hotel")), GetRoots = () => runtime.Roots, Release = runtime.Dispose });
                }
            }
            // The exact live BaseMap registry includes Sideloader-added MapInfo rows.
            foreach (var map in BaseMap.LoadMapInfo().Values.Where(m => m != null).OrderBy(m => m.No))
                result.Add(new StageMap { Id = "kks:" + map.No, Name = map.DisplayName + " [" + map.No + "]", Bundle = map.AssetBundleName, Scene = map.AssetName,
                    Error = string.IsNullOrEmpty(map.AssetBundleName) || string.IsNullOrEmpty(map.AssetName) ? "読込先の登録がありません" : null });
            if (island == null)
            {
                // Use the installed extracted island loader, including its existing material/scale conversion.
                var settings = Parameters.Load(Path.Combine(content, "parameters.json"));
                var islandManifest = JsonCodec.FromJson<SandboxIslandLoader.Manifest>(File.ReadAllText(Path.Combine(settings.T("island.directory"), "manifest.json")));
                SandboxIslandLoader loader = null; GameObject host = null;
                result.Add(new StageMap { Id = "island", Name = "AI少女の島", Scene = islandManifest.scene, Load = () =>
                {
                    host = new GameObject("Event island loader"); loader = host.AddComponent<SandboxIslandLoader>(); return loader.Load(settings);
                }, GetRoots = () => { if (loader == null || !string.IsNullOrEmpty(loader.Error) || loader.Root == null) throw new InvalidOperationException(loader?.Error ?? "島を読めません。"); return new[] { loader.Root }; }, Release = () =>
                {
                    if (loader != null && loader.LoadedScene.IsValid() && loader.LoadedScene.isLoaded) UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(loader.LoadedScene);
                    if (host != null) UnityEngine.Object.Destroy(host); loader = null; host = null;
                } });
            }
            return result;
        }
    }
}
