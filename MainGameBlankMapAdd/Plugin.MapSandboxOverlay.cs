using System;
using System.Collections;
using System.Linq;
using BepInEx.Bootstrap;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private AssetBundle _sandboxMapBundle;
        private string _sandboxMapSceneName;
        private object SandboxApi => Chainloader.PluginInfos.TryGetValue("com.kks.maingame.mapsandbox", out var info) ? info.Instance : null;
        private object SandboxCall(string name, params object[] args)
        {
            var api = SandboxApi;
            if (api == null) throw new InvalidOperationException("MainGameMapSandboxが読み込まれていません。");
            return api.GetType().GetMethod(name).Invoke(api, args);
        }
        private bool IsSandboxMapScene(string name) => !string.IsNullOrEmpty(_sandboxMapSceneName) && name == _sandboxMapSceneName;
        private bool IsSandboxEnvironmentActive() => _sandboxMapBundle != null && IsSandboxMapScene(_roomOverlaySceneName) && _roomOverlayPositionRoot != null;
        private IEnumerator SwitchSandboxMap()
        {
            if (!CanUseRoomOverlay() || _roomOverlayLoading) yield break;
            _roomOverlayLoading = true;
            yield return UnloadRoomOverlay();
            if (!CanUseRoomOverlay()) { _roomOverlayLoading = false; yield break; }
            try
            {
                _sandboxMapSceneName = (string)SandboxCall("GetSceneName");
                _sandboxMapBundle = AssetBundle.LoadFromFile((string)SandboxCall("GetBundlePath"));
                if (_sandboxMapBundle == null) throw new InvalidOperationException("共通マップBundleを読めません。");
                string path = _sandboxMapBundle.GetAllScenePaths().Single();
                _roomOverlaySceneName = _sandboxMapSceneName;
                _roomOverlayPositionKey = "sandbox-map";
                EnsureRoomOverlayPositionConfig(_roomOverlayPositionKey);
                _roomOverlayDisplayName = "自然な空・昼夜の検証マップ";
                _roomOverlayLoadStartedAt = Time.realtimeSinceStartup;
                if (SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive) == null) throw new InvalidOperationException("共通マップSceneを読めません。");
            }
            catch (Exception ex)
            {
                LogWarn("sandbox map load: " + ex); UnloadSandboxMapBundle();
                _roomOverlaySceneName = null; _roomOverlayDisplayName = null; _roomOverlayLoading = false;
            }
        }
        private void OnSandboxMapLoaded(Scene scene)
        {
            try
            {
                var root = scene.GetRootGameObjects().Single(item => item.name == BaseMap.MAP_ROOT_NAME);
                var anchor = root.transform.Find((string)SandboxCall("GetAnchorName"));
                byte sex = (byte)SandboxCall("GetActorSex");
                var actor = (_femaleChara != null && _femaleChara.sex == sex ? _femaleChara : null);
                if (anchor == null || actor == null) throw new InvalidOperationException("Map anchor or H actor missing");
                root.transform.position += actor.transform.position - anchor.position;
                _roomOverlayPositionRoot = root;
                _roomOverlayBasePosition = root.transform.position; _roomOverlayBaseRotation = root.transform.rotation;
                _roomOverlayActorAnchor = actor.transform.position;
                ApplyRoomOverlayPosition(); HideSourceRoomForOverlay();
                SandboxCall("AttachMap", root);
                _roomOverlayLoading = false;
                QueueDefaultHPlace();
            }
            catch (Exception ex) { LogWarn("sandbox map attach: " + ex); ClearRoomOverlay(); }
        }
        private void UnloadSandboxMapBundle()
        {
            if (_sandboxMapBundle != null) _sandboxMapBundle.Unload(true);
            _sandboxMapBundle = null;
        }
    }
}
