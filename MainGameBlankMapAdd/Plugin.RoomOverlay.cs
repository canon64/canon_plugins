using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private readonly List<Renderer> _roomOverlayHiddenRenderers = new List<Renderer>();
        private readonly List<Light> _roomOverlayHiddenLights = new List<Light>();
        private readonly SourceRoomMembers _roomOverlaySourceMembers = new SourceRoomMembers();
        private readonly List<MapInfo.Param> _roomOverlayChoices = new List<MapInfo.Param>();
        private string _roomOverlaySceneName;
        private string _roomOverlayBundleName;
        private string _roomOverlayDisplayName;
        private string _roomOverlayAbandonedSceneName;
        private string _roomOverlayAbandonedBundleName;
        private bool _roomOverlayLoading;
        private float _roomOverlayLoadStartedAt;
        private bool _roomOverlayDropdownOpen;
        private string _roomOverlayPositionKey;
        private GameObject _roomOverlayPositionRoot;
        private Vector3 _roomOverlayBasePosition;
        private Quaternion _roomOverlayBaseRotation;
        private Vector3 _roomOverlayActorAnchor;
        private Rect _roomOverlayButtonRect;
        private Rect _roomOverlayListRect;
        private Vector2 _roomOverlayScroll;

        private bool CanUseRoomOverlay()
        {
            return CanonPlugins.Shared.PluginSessionGate.Allowed;
        }

        private void TickRoomOverlay()
        {
            if (!CanUseRoomOverlay())
            {
                ReleaseMapSkyEnvironment();
                _roomOverlayDropdownOpen = false;
                if (_roomOverlayLoading || !string.IsNullOrEmpty(_roomOverlaySceneName)) ClearRoomOverlay();
                return;
            }
            if (_roomOverlayLoading && !_aiLoadInProgress && Time.realtimeSinceStartup - _roomOverlayLoadStartedAt > 20f)
            {
                LogWarn("room overlay load timed out: " + _roomOverlaySceneName);
                ClearRoomOverlay();
            }
            if (_roomOverlayLoading) return;
            TickMapSky();
            if (string.IsNullOrEmpty(_roomOverlaySceneName)) return;
            if (_roomOverlaySceneName == VamHotelSceneName) ApplyVamHotelTransform();
            else ApplyRoomOverlayPosition();
            // Check known parts every frame; hierarchy notifications refresh recreated media parts.
            HideSourceRoomForOverlay();
            if (_roomOverlaySceneName == StreetlightRoadSceneKey)
            {
                RebuildStreetlightRoadIfNeeded();
                EnforceStreetlightRoadLighting();
            }
        }

        private void HideSourceRoomForOverlay()
        {
            GameObject source = LastReservedMap != null ? LastReservedMap.mapRoot : null;
            _roomOverlaySourceMembers.Ensure(source);
            if (source == null) return;
            foreach (Renderer renderer in _roomOverlaySourceMembers.Renderers)
                if (renderer != null && renderer.enabled) { renderer.enabled = false; _roomOverlayHiddenRenderers.Add(renderer); }
            foreach (Light light in _roomOverlaySourceMembers.Lights)
                if (light != null && light.enabled && !(IsAiIslandScene(_roomOverlaySceneName) && light == RenderSettings.sun))
                { light.enabled = false; _roomOverlayHiddenLights.Add(light); }
        }

        private void RestoreSourceRoomAfterOverlay()
        {
            _roomOverlaySourceMembers.Dispose();
            foreach (Renderer renderer in _roomOverlayHiddenRenderers)
                if (renderer != null) renderer.enabled = true;
            foreach (Light light in _roomOverlayHiddenLights)
                if (light != null) light.enabled = true;
            _roomOverlayHiddenRenderers.Clear();
            _roomOverlayHiddenLights.Clear();
        }

        private void SelectRoomOverlay(MapInfo.Param choice)
        {
            if (!CanUseRoomOverlay() || _roomOverlayLoading || choice == null) return;
            _roomOverlayDropdownOpen = false;
            StartCoroutine(SwitchRoomOverlay(choice));
        }

        private IEnumerator SwitchRoomOverlay(MapInfo.Param choice)
        {
            _roomOverlayLoading = true;
            _roomOverlayLoadStartedAt = Time.realtimeSinceStartup;
            yield return UnloadRoomOverlay();
            if (!CanUseRoomOverlay()) { _roomOverlayLoading = false; yield break; }
            _roomOverlaySceneName = choice.AssetName;
            _roomOverlayBundleName = choice.AssetBundleName;
            _roomOverlayDisplayName = choice.DisplayName;
            _roomOverlayPositionKey = choice.No.ToString(System.Globalization.CultureInfo.InvariantCulture);
            EnsureRoomOverlayPositionConfig(_roomOverlayPositionKey);
            CaptureNativeHotelRoomEnvironment(choice.AssetName);
            try
            {
                LogInfo($"room overlay load no={choice.No} scene={choice.AssetName} bundle={choice.AssetBundleName}");
                AssetBundleManager.LoadLevel(choice.AssetBundleName, choice.AssetName, true);
            }
            catch (Exception ex)
            {
                LogWarn("room overlay load failed: " + ex);
                ReleaseNativeHotelRoomEnvironment();
                _roomOverlaySceneName = null;
                _roomOverlayBundleName = null;
                _roomOverlayDisplayName = null;
                _roomOverlayLoading = false;
            }
        }

        private void OnRoomOverlaySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!string.IsNullOrEmpty(_roomOverlayAbandonedSceneName) && scene.name == _roomOverlayAbandonedSceneName)
            {
                SceneManager.UnloadSceneAsync(scene);
                if (IsVamHotelScene(scene.name)) UnloadVamHotelBundle();
                if (!string.IsNullOrEmpty(_roomOverlayAbandonedBundleName))
                    AssetBundleManager.UnloadAssetBundle(_roomOverlayAbandonedBundleName, false);
                _roomOverlayAbandonedSceneName = null;
                _roomOverlayAbandonedBundleName = null;
                return;
            }
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed) return;
            if (string.IsNullOrEmpty(_roomOverlaySceneName) || scene.name != _roomOverlaySceneName) return;
            if (!CanUseRoomOverlay()) { ClearRoomOverlay(); return; }
            if (IsVamHotelScene(scene.name)) { OnVamHotelSceneLoaded(scene); return; }
            if (IsSandboxMapScene(scene.name)) { OnSandboxMapLoaded(scene); return; }
            if (IsAiIslandScene(scene.name)) return; // The island coroutine validates and attaches its own root.
            try
            {
                GameObject target = scene.GetRootGameObjects().FirstOrDefault(x => x != null && x.name == BaseMap.MAP_ROOT_NAME);
                ChaControl actor = _femaleChara;
                Renderer[] renderers = target != null ? target.GetComponentsInChildren<Renderer>(true).Where(x => x != null && x.enabled).ToArray() : new Renderer[0];
                if (target == null || actor == null || renderers.Length == 0)
                    throw new InvalidOperationException("map root, H actor, or visible geometry missing");
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                Transform nativeAnchor = NativeHotelRoomAnchor(scene.name, target);
                target.transform.position += nativeAnchor != null ? actor.transform.position - nativeAnchor.position :
                    new Vector3(actor.transform.position.x - bounds.center.x,
                        actor.transform.position.y - bounds.min.y, actor.transform.position.z - bounds.center.z);
                ApplyNativeHotelRoomEnvironment(scene.name, target);
                _roomOverlayPositionRoot = target;
                _roomOverlayBasePosition = target.transform.position;
                _roomOverlayBaseRotation = target.transform.rotation;
                _roomOverlayActorAnchor = actor.transform.position;
                ApplyRoomOverlayPosition();
                HideSourceRoomForOverlay();
                _roomOverlayLoading = false;
                LogInfo($"room overlay shown scene={scene.name} renderers={renderers.Length} sourceLightsOff={_roomOverlayHiddenLights.Count}");
                QueueDefaultHPlace();
            }
            catch (Exception ex)
            {
                LogWarn("room overlay display failed: " + ex);
                ClearRoomOverlay();
            }
        }

        private IEnumerator UnloadRoomOverlay()
        {
            StopHLocations();
            ReleaseMapSkyEnvironment();
            ReleaseNativeHotelRoomEnvironment();
            string oldScene = _roomOverlaySceneName;
            string oldBundle = _roomOverlayBundleName;
            _roomOverlaySceneName = null;
            _roomOverlayBundleName = null;
            _roomOverlayDisplayName = null;
            _roomOverlayPositionRoot = null;
            _roomOverlayPositionKey = null;
            RestoreSourceRoomAfterOverlay();
            if (IsAiIslandScene(oldScene)) { yield return ReleaseAiIsland(); yield break; }
            if (oldScene == StreetlightRoadSceneKey) { RestoreStreetlightCameraPreview(); DestroyStreetlightRoad(); }
            if (!string.IsNullOrEmpty(oldScene) && oldScene != StreetlightRoadSceneKey)
            {
                Scene scene = SceneManager.GetSceneByName(oldScene);
                if (scene.IsValid() && scene.isLoaded)
                {
                    AsyncOperation operation = SceneManager.UnloadSceneAsync(scene);
                    if (operation != null) yield return operation;
                }
            }
            if (!string.IsNullOrEmpty(oldBundle)) AssetBundleManager.UnloadAssetBundle(oldBundle, false);
            if (IsVamHotelScene(oldScene)) UnloadVamHotelBundle();
            if (IsSandboxMapScene(oldScene)) UnloadSandboxMapBundle();
            if(!_roomOverlayLoading&&_sessionStarted)QueueDefaultHPlace();
        }

        private void ClearRoomOverlay()
        {
            StopHLocations();
            if (_aiLoadInProgress) { _aiCancelRequested = true; return; }
            _roomOverlayDropdownOpen = false;
            if (string.IsNullOrEmpty(_roomOverlaySceneName)) { RestoreSourceRoomAfterOverlay(); return; }
            if (_roomOverlayLoading)
            {
                Scene loadingScene = SceneManager.GetSceneByName(_roomOverlaySceneName);
                if (!loadingScene.IsValid() || !loadingScene.isLoaded)
                {
                    _roomOverlayAbandonedSceneName = _roomOverlaySceneName;
                    _roomOverlayAbandonedBundleName = _roomOverlayBundleName;
                    _roomOverlaySceneName = null;
                    _roomOverlayBundleName = null;
                    _roomOverlayDisplayName = null;
                    _roomOverlayPositionRoot = null;
                    _roomOverlayPositionKey = null;
                    _roomOverlayLoading = false;
                    ReleaseMapSkyEnvironment();
                    ReleaseNativeHotelRoomEnvironment();
                    RestoreSourceRoomAfterOverlay();
                    return;
                }
            }
            StartCoroutine(UnloadRoomOverlay());
            _roomOverlayLoading = false;
        }

        private void DisposeRoomOverlay()
        {
            StopHLocations();
            ReleaseMapSkyEnvironment();
            ReleaseNativeHotelRoomEnvironment();
            DisposeAiIsland();
            _roomOverlayDropdownOpen = false;
            RestoreStreetlightCameraPreview();
            RestoreSourceRoomAfterOverlay();
            DestroyStreetlightRoad();
            if (!string.IsNullOrEmpty(_roomOverlaySceneName) && _roomOverlaySceneName != StreetlightRoadSceneKey && !IsAiIslandScene(_roomOverlaySceneName))
            {
                Scene scene = SceneManager.GetSceneByName(_roomOverlaySceneName);
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                if (!string.IsNullOrEmpty(_roomOverlayBundleName))
                    AssetBundleManager.UnloadAssetBundle(_roomOverlayBundleName, false);
            }
            UnloadVamHotelBundle();
            UnloadSandboxMapBundle();
            _roomOverlaySceneName = null;
            _roomOverlayBundleName = null;
            _roomOverlayDisplayName = null;
            _roomOverlayPositionRoot = null;
            _roomOverlayPositionKey = null;
            _roomOverlayLoading = false;
        }

        private void DrawRoomOverlayButton(Rect rect)
        {
            _roomOverlayButtonRect = rect;
            bool available = CanUseRoomOverlay();
            bool previous = GUI.enabled;
            GUI.enabled = previous && available && !_roomOverlayLoading;
            string label = _roomOverlayLoading ? "部屋 読込中" :
                string.IsNullOrEmpty(_roomOverlayDisplayName) ? "部屋: 元の部屋 ▼" : "部屋: " + _roomOverlayDisplayName + " ▼";
            if (GUI.Button(rect, new GUIContent(label, "オールポーズマップ内に別の部屋を呼び出す")))
            {
                _roomOverlayDropdownOpen = !_roomOverlayDropdownOpen;
                if (_roomOverlayDropdownOpen)
                {
                    _roomOverlayScroll = Vector2.zero;
                    RefreshRoomOverlayChoices();
                }
            }
            GUI.enabled = previous;
            if (!available) _roomOverlayDropdownOpen = false;
        }

        private void DrawRoomOverlayPopup()
        {
            _roomOverlayListRect = Rect.zero;
            if (!_roomOverlayDropdownOpen) return;


            const float rowHeight = 22f;
            float headerHeight = 0f;
            float height = Mathf.Min(330f, (_roomOverlayChoices.Count + 1) * rowHeight + 6f + headerHeight);
            float y = Mathf.Max(2f, _roomOverlayButtonRect.y - height - 3f);
            _roomOverlayListRect = new Rect(_roomOverlayButtonRect.x, y, Mathf.Max(220f, _roomOverlayButtonRect.width), height);
            using (EnterPlaybackPopup(RoomPopup, _roomOverlayListRect))
            {
                GUI.Box(_roomOverlayListRect, GUIContent.none);
                Rect viewport = new Rect(_roomOverlayListRect.x + 3f, y + 3f + headerHeight,
                    _roomOverlayListRect.width - 6f, height - 6f - headerHeight);
                Rect content = new Rect(0, 0, viewport.width - 18f, (_roomOverlayChoices.Count + 1) * rowHeight);
                _roomOverlayScroll = GUI.BeginScrollView(viewport, _roomOverlayScroll, content);
                if (GUI.Button(new Rect(0, 0, content.width, rowHeight - 1f), "元の部屋に戻す"))
                {
                    _roomOverlayDropdownOpen = false;
                    ClearRoomOverlay();
                }
                for (int i = 0; i < _roomOverlayChoices.Count; i++)
                {
                    MapInfo.Param choice = _roomOverlayChoices[i];
                    string name = string.IsNullOrEmpty(choice.DisplayName) ? choice.MapName : choice.DisplayName;
                    if (GUI.Button(new Rect(0, (i + 1) * rowHeight, content.width, rowHeight - 1f), name)) SelectRoomOverlay(choice);
                }
                GUI.EndScrollView();
            }
            if (Event.current != null && Event.current.type == EventType.MouseDown &&
                !_roomOverlayListRect.Contains(Event.current.mousePosition) && !_roomOverlayButtonRect.Contains(Event.current.mousePosition))
                _roomOverlayDropdownOpen = false;
        }
    }
}
