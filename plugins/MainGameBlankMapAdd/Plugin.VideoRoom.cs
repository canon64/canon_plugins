using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using HarmonyLib;
using MainGameTransformGizmo;
using UnityEngine;
using UnityEngine.Video;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private void EnsureVideoRoom(BaseMap map)
        {
            if (map == null || map.mapRoot == null) return;

            int rootId = map.mapRoot.GetInstanceID();
            if (_videoRoomRoot != null && _videoRoomRootMapId == rootId)
                return;

            TryApplySavedRoomLayoutForCurrentSelection("ensure-video-room");

            DestroyVideoRoom();

            _videoRoomRootMapId = rootId;
            _videoRoomRoot = new GameObject("__BlankMapVideoRoom");
            _gizmo = TransformGizmoApi.Attach(_videoRoomRoot);
            if (_gizmo != null)
                _gizmo.DragStateChanged += NotifyGizmoDragState;
            _videoRoomRoot.transform.SetParent(map.mapRoot.transform, false);
            _videoRoomRoot.transform.localPosition = Vector3.zero;
            _videoRoomRoot.transform.localScale =
                Vector3.one * Mathf.Clamp(_playbackRoomScale, 0.25f, 4f);
            // 菫晏ｭ俶ｸ医∩蝗櫁ｻ｢繧帝←逕ｨ
            _videoRoomRoot.transform.rotation = Quaternion.Euler(
                _settings.VideoRoomRotationX,
                _settings.VideoRoomRotationY,
                _settings.VideoRoomRotationZ);

            EnsureFemaleCharaRef();
            float width  = Mathf.Max(1f, _settings.RoomWidth);
            float depth  = Mathf.Max(1f, _settings.RoomDepth);
            float height = Mathf.Max(1f, _settings.RoomHeight);
            float cubeSide = GetCubeSideLength(width, depth, height);
            if (_femaleChara != null)
            {
                var fp = _femaleChara.transform.position;
                _femaleBaseY = fp.y;
                _hasFemaleBaseY = true;
                // Center anchor for both sphere and cube.
                _videoRoomRoot.transform.position = new Vector3(
                    fp.x + _settings.VideoRoomOffsetX,
                    _femaleBaseY + _settings.VideoRoomOffsetY,
                    fp.z + _settings.VideoRoomOffsetZ);
            }
            else
            {
                _hasFemaleBaseY = false;
            }

            string sharedVideoPath = ResolveVideoPath(_settings.VideoPath);

            if (_settings.UseSphere)
            {
                CreateSphereSurface(_videoRoomRoot.transform, sharedVideoPath);
                LogInfo($"video room created mapNo={map.no} sphere radius={_settings.SphereRadius:F1}");
            }
            else
            {
                CreateCubeVideoSurfaces(cubeSide, sharedVideoPath);
                LogInfo($"video room created mapNo={map.no} cubeSide={cubeSide:F1}");
            }

            ApplyRoomReverb(width, depth, height);

            // 蠎ｧ讓吶・隗貞ｺｦ繝ｭ繧ｰ
            LogInfo(
                $"video room world pos={_videoRoomRoot.transform.position} " +
                $"rot={_videoRoomRoot.transform.eulerAngles}");

            var chaControls = UnityEngine.Object.FindObjectsOfType<ChaControl>();
            foreach (var c in chaControls)
            {
                if (c == null) continue;
                LogInfo(
                    $"chara name={c.name} sex={c.sex} " +
                    $"pos={c.transform.position} rot={c.transform.eulerAngles}");
            }
        }

        private void CreateCubeVideoSurfaces(float cubeSide, string sharedVideoPath)
        {
            string wallPath = PickVideoPathForSurface("wall", sharedVideoPath);
            string floorPath = PickVideoPathForSurface("floor", sharedVideoPath);
            string ceilingPath = PickVideoPathForSurface("ceiling", sharedVideoPath);
            bool audioAssigned = false;
            float half = cubeSide * 0.5f;
            int tileCount = ResolveCubeFaceTileCount();
            ResolveCubeFaceTileGrid(tileCount, out int columns, out int rows);
            Vector2 panelSize = new Vector2(cubeSide / columns, cubeSide / rows);

            CreateCubeSurfaceTiles(
                "WallBack",
                new Vector3(0f, 0f, -half),
                Quaternion.Euler(0f, 180f, 0f),
                panelSize,
                wallPath,
                tileCount,
                ref audioAssigned);

            CreateCubeSurfaceTiles(
                "WallFront",
                new Vector3(0f, 0f, half),
                Quaternion.identity,
                panelSize,
                wallPath,
                tileCount,
                ref audioAssigned);

            CreateCubeSurfaceTiles(
                "WallLeft",
                new Vector3(-half, 0f, 0f),
                Quaternion.Euler(0f, -90f, 0f),
                panelSize,
                wallPath,
                tileCount,
                ref audioAssigned);

            CreateCubeSurfaceTiles(
                "WallRight",
                new Vector3(half, 0f, 0f),
                Quaternion.Euler(0f, 90f, 0f),
                panelSize,
                wallPath,
                tileCount,
                ref audioAssigned);

            CreateCubeSurfaceTiles(
                "Floor",
                new Vector3(0f, -half, 0f),
                Quaternion.Euler(90f, 0f, 0f),
                panelSize,
                floorPath,
                tileCount,
                ref audioAssigned);

            CreateCubeSurfaceTiles(
                "Ceiling",
                new Vector3(0f, half, 0f),
                Quaternion.Euler(-90f, 0f, 0f),
                panelSize,
                ceilingPath,
                tileCount,
                ref audioAssigned);
        }

        private bool TryRefreshCubeSurfaceTilesInPlace(out string detail)
        {
            detail = string.Empty;

            if (_settings == null)
            {
                detail = "settings-null";
                return false;
            }

            if (_videoRoomRoot == null)
            {
                detail = "room-null";
                return false;
            }

            if (_settings.UseSphere)
            {
                detail = "sphere-mode";
                return true;
            }

            var surfaceObjects = new List<GameObject>();
            var detachedMaterials = new HashSet<Material>();
            Transform roomTr = _videoRoomRoot.transform;
            for (int i = 0; i < roomTr.childCount; i++)
            {
                Transform child = roomTr.GetChild(i);
                if (child == null || child.gameObject == null)
                    continue;
                if (!child.name.StartsWith("VideoSurface_", StringComparison.Ordinal))
                    continue;

                var renderer = child.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                    detachedMaterials.Add(renderer.sharedMaterial);
                surfaceObjects.Add(child.gameObject);
            }

            if (surfaceObjects.Count == 0)
            {
                detail = "surface-not-found";
                return false;
            }

            DetachSurfaceMaterialsFromBindings(detachedMaterials);
            for (int i = 0; i < surfaceObjects.Count; i++)
                Destroy(surfaceObjects[i]);

            float cubeSide = GetCubeSideLength();
            string sharedVideoPath = ResolveCurrentSharedVideoPathForRetile();
            CreateCubeVideoSurfaces(cubeSide, sharedVideoPath);

            detail = $"retiled surfaces={surfaceObjects.Count} tiles={ResolveCubeFaceTileCount()}";
            return true;
        }

        private void DetachSurfaceMaterialsFromBindings(HashSet<Material> detachedMaterials)
        {
            foreach (var kv in _videoBindings)
            {
                VideoBinding binding = kv.Value;
                if (binding?.Materials == null)
                    continue;

                for (int i = binding.Materials.Count - 1; i >= 0; i--)
                {
                    Material mat = binding.Materials[i];
                    if (mat == null ||
                        (detachedMaterials != null && detachedMaterials.Contains(mat)))
                    {
                        binding.Materials.RemoveAt(i);
                    }
                }
            }
        }

        private string ResolveCurrentSharedVideoPathForRetile()
        {
            if (_mainVideoPlayer != null)
            {
                string current = _mainVideoPlayer.url;
                if (!string.IsNullOrWhiteSpace(current))
                {
                    if (current.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                        return Uri.UnescapeDataString(current.Substring(8));
                    return current;
                }
            }

            return ResolveVideoPath(_settings?.VideoPath);
        }

        private static float GetCubeSideLength(float width, float depth, float height)
        {
            return Mathf.Max(1f, Mathf.Max(width, Mathf.Max(depth, height)));
        }

        private float GetCubeSideLength()
        {
            return GetCubeSideLength(
                _settings.RoomWidth,
                _settings.RoomDepth,
                _settings.RoomHeight);
        }

        private int ResolveCubeFaceTileCount()
        {
            switch (_settings.CubeFaceTileCount)
            {
                case 4:
                case 9:
                case 16:
                case 25:
                    return _settings.CubeFaceTileCount;
                default:
                    return 1;
            }
        }

        private static void ResolveCubeFaceTileGrid(int tileCount, out int columns, out int rows)
        {
            int side = Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(1, tileCount)));
            if (side * side != tileCount)
            {
                side = 1;
            }

            columns = side;
            rows = side;
        }

        private void CreateCubeSurfaceTiles(
            string surfaceName,
            Vector3 localPos,
            Quaternion localRot,
            Vector2 size,
            string videoPath,
            int tileCount,
            ref bool audioAssigned)
        {
            ResolveCubeFaceTileGrid(tileCount, out int columns, out int rows);

            Vector3 localRight = localRot * Vector3.right;
            Vector3 localUp = localRot * Vector3.up;
            float startX = -((columns - 1) * size.x * 0.5f);
            float startY = ((rows - 1) * size.y * 0.5f);
            int index = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    if (index >= tileCount)
                    {
                        return;
                    }

                    Vector3 offset = localRight * (startX + col * size.x)
                                   + localUp * (startY - row * size.y);
                    Vector3 tilePos = localPos + offset;
                    string tileName = tileCount == 1
                        ? surfaceName
                        : $"{surfaceName}_{index + 1}";

                    bool withAudio = !audioAssigned && !string.IsNullOrWhiteSpace(videoPath);
                    CreateVideoSurface(
                        _videoRoomRoot.transform,
                        tileName,
                        tilePos,
                        localRot,
                        size,
                        videoPath,
                        withAudio);
                    if (withAudio)
                    {
                        audioAssigned = true;
                    }

                    index++;
                }
            }
        }

        private void ApplyRoomReverb(float width, float depth, float height)
        {
            if (_videoRoomRoot == null) return;

            LogInfo(
                $"[reverb] apply request enabled={_settings.EnableVoiceReverb} " +
                $"preset={_settings.VoiceReverbPreset} min={_settings.VoiceReverbMinDistance:F2} " +
                $"max={_settings.VoiceReverbMaxDistance:F2}");

            if (!_settings.EnableVoiceReverb)
            {
                if (_reverbZoneObject != null)
                {
                    Destroy(_reverbZoneObject);
                    _reverbZoneObject = null;
                    _voiceReverbZone = null;
                    LogInfo("[reverb] disabled (zone destroyed)");
                }
                else if (_voiceReverbZone != null)
                {
                    Destroy(_voiceReverbZone);
                    _voiceReverbZone = null;
                    LogInfo("[reverb] disabled (legacy zone destroyed)");
                }
                LogReverbDiagnostics("disabled");
                return;
            }

            if (_voiceReverbZone != null && _voiceReverbZone.gameObject == _videoRoomRoot)
            {
                // Migrate old layout where zone was attached to room root.
                Destroy(_voiceReverbZone);
                _voiceReverbZone = null;
                LogInfo("[reverb] migrated legacy root zone -> child zone object");
            }

            EnsureReverbZoneObject();
            if (_voiceReverbZone == null) return;

            AudioReverbPreset preset = ParseReverbPreset(_settings.VoiceReverbPreset);
            float minDistance = Mathf.Max(0f, _settings.VoiceReverbMinDistance);
            float maxDistance = Mathf.Max(minDistance + 0.1f, _settings.VoiceReverbMaxDistance);

            _voiceReverbZone.reverbPreset = preset;
            _voiceReverbZone.minDistance = minDistance;
            _voiceReverbZone.maxDistance = maxDistance;
            _voiceReverbZone.enabled = true;
            bool synced = SyncReverbZoneToFemale(true);
            if (!synced)
            {
                // Fallback while female is not resolved yet.
                _voiceReverbZone.transform.localPosition = Vector3.zero;
                LogWarn("[reverb] female position not found, temporary fallback to room origin");
            }

            LogInfo(
                $"[reverb] enabled preset={preset} min={minDistance:F1} max={maxDistance:F1} " +
                $"room=({width:F1},{depth:F1},{height:F1}) zonePos={_voiceReverbZone.transform.position}");
            LogReverbDiagnostics("enabled");
        }

        private void EnsureReverbZoneObject()
        {
            if (_videoRoomRoot == null) return;

            if (_reverbZoneObject == null)
            {
                Transform existing = _videoRoomRoot.transform.Find("__VoiceReverbZone");
                if (existing != null)
                    _reverbZoneObject = existing.gameObject;
            }

            if (_reverbZoneObject == null)
            {
                _reverbZoneObject = new GameObject("__VoiceReverbZone");
                _reverbZoneObject.transform.SetParent(_videoRoomRoot.transform, false);
                _reverbZoneObject.transform.localPosition = Vector3.zero;
                _reverbZoneObject.transform.localRotation = Quaternion.identity;
            }

            if (_voiceReverbZone == null)
                _voiceReverbZone = _reverbZoneObject.GetComponent<AudioReverbZone>();
            if (_voiceReverbZone == null)
                _voiceReverbZone = _reverbZoneObject.AddComponent<AudioReverbZone>();
        }

        private void EnsureFemaleCharaRef()
        {
            if (_femaleChara != null) return;
            var chaControls = UnityEngine.Object.FindObjectsOfType<ChaControl>();
            for (int i = 0; i < chaControls.Length; i++)
            {
                var c = chaControls[i];
                if (c == null) continue;
                if (c.sex != 1) continue;
                _femaleChara = c;
                _femaleBaseY = c.transform.position.y;
                _hasFemaleBaseY = true;
                LogInfo($"female ref assigned: name={c.name} pos={c.transform.position}");
                return;
            }
        }

        private bool SyncReverbZoneToFemale(bool withLog)
        {
            if (_voiceReverbZone == null || _reverbZoneObject == null) return false;
            EnsureFemaleCharaRef();
            if (_femaleChara == null) return false;

            Vector3 femalePos = _femaleChara.transform.position;
            _reverbZoneObject.transform.position = femalePos;

            if (withLog)
            {
                LogInfo(
                    $"[reverb] zone synced to female pos={femalePos} " +
                    $"zoneWorld={_voiceReverbZone.transform.position} roomWorld={_videoRoomRoot?.transform.position}");
            }
            return true;
        }

        private bool SyncVideoAudioSourceToFemale(bool withLog)
        {
            if (_videoRoomAudioSource == null) return false;
            EnsureFemaleCharaRef();
            if (_femaleChara == null) return false;

            Vector3 femalePos = _femaleChara.transform.position;
            _videoRoomAudioSource.transform.position = femalePos;

            if (withLog)
            {
                LogInfo(
                    $"[video-audio] source synced to female pos={femalePos} " +
                    $"sourceWorld={_videoRoomAudioSource.transform.position} " +
                    $"roomWorld={_videoRoomRoot?.transform.position}");
            }

            return true;
        }

        private void SyncActiveVoiceSourcesToVideoRoom(bool withLog)
        {
            if (_videoRoomRoot == null)
                return;

            Vector3 target = _videoRoomRoot.transform.position;
            EnsureFemaleCharaRef();
            if (_femaleChara != null)
                target.y = _femaleChara.transform.position.y;
            AudioSource[] sources = UnityEngine.Object.FindObjectsOfType<AudioSource>();
            int moved = 0;
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource s = sources[i];
                if (s == null || !s.isPlaying)
                    continue;

                string path = BuildTransformPath(s.transform);
                bool isVoice = path.IndexOf("/Voice/PlayObjectPCM/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               s.name.StartsWith("h_", StringComparison.OrdinalIgnoreCase);
                if (!isVoice)
                    continue;

                if (s.transform != null)
                {
                    s.transform.position = target;
                    s.transform.rotation = Quaternion.identity;
                }
                moved++;
            }

            if (!withLog && Time.unscaledTime < _nextVoiceSyncLogTime)
                return;
            _nextVoiceSyncLogTime = Time.unscaledTime + 2f;
            LogInfo($"[voice-sync] active voice sources moved={moved} target={target}");
        }

        private void LogReverbDiagnostics(string context)
        {
            try
            {
                var listener = UnityEngine.Object.FindObjectOfType<AudioListener>();
                var sources = UnityEngine.Object.FindObjectsOfType<AudioSource>();

                int playing = 0;
                int spatialPlaying = 0;
                int bypassPlaying = 0;
                int insideZonePlaying = 0;
                Vector3 zonePos = _voiceReverbZone != null ? _voiceReverbZone.transform.position : Vector3.zero;
                float zoneMax = _voiceReverbZone != null ? _voiceReverbZone.maxDistance : 0f;

                var sample = new List<string>(5);
                for (int i = 0; i < sources.Length; i++)
                {
                    var s = sources[i];
                    if (s == null || !s.isPlaying) continue;
                    playing++;

                    bool isSpatial = s.spatialBlend > 0.01f;
                    if (isSpatial) spatialPlaying++;
                    if (s.bypassReverbZones) bypassPlaying++;

                    float distToZone = _voiceReverbZone != null
                        ? Vector3.Distance(s.transform.position, zonePos)
                        : float.NaN;
                    if (_voiceReverbZone != null && distToZone <= zoneMax)
                        insideZonePlaying++;

                    if (sample.Count < 5)
                    {
                        sample.Add(
                            $"name={s.name} spatial={s.spatialBlend:F2} bypass={s.bypassReverbZones} " +
                            $"mix={s.reverbZoneMix:F2} dist={distToZone:F2}");
                    }
                }

                string listenerText = "none";
                if (listener != null)
                {
                    float d = _voiceReverbZone != null
                        ? Vector3.Distance(listener.transform.position, zonePos)
                        : float.NaN;
                    listenerText = $"pos={listener.transform.position} distToZone={d:F2} zoneMax={zoneMax:F2}";
                }

                LogInfo(
                    $"[reverb] diag({context}) listener={listenerText} " +
                    $"sourcesAll={sources.Length} playing={playing} spatialPlaying={spatialPlaying} " +
                    $"insideZonePlaying={insideZonePlaying} bypassPlaying={bypassPlaying}");

                for (int i = 0; i < sample.Count; i++)
                    LogInfo($"[reverb] src[{i}] {sample[i]}");

                if (_voiceReverbZone != null && listener != null)
                {
                    float listenerDist = Vector3.Distance(listener.transform.position, zonePos);
                    if (listenerDist > zoneMax)
                        LogWarn(
                            $"[reverb] listener is outside reverb zone: dist={listenerDist:F2} > max={zoneMax:F2}");
                }

                if (playing > 0 && spatialPlaying == 0)
                    LogWarn("[reverb] no spatial playing sources (spatialBlend<=0), reverb effect will be hard to hear");
            }
            catch (Exception ex)
            {
                LogWarn($"[reverb] diagnostics failed: {ex.Message}");
            }
        }

        private AudioReverbPreset ParseReverbPreset(string rawPresetName)
        {
            string name = rawPresetName?.Trim();
            if (!string.IsNullOrEmpty(name) &&
                Enum.TryParse(name, true, out AudioReverbPreset preset))
                return preset;

            LogWarn($"invalid VoiceReverbPreset='{rawPresetName}', fallback='Cave'");
            return AudioReverbPreset.Cave;
        }

        private void DestroyVideoRoom()
        {
            StopUiCapture("video room destroyed");
            _editMode = false;
            ClearPendingVideoStarts();
            if (_gizmo != null)
                _gizmo.DragStateChanged -= NotifyGizmoDragState;
            _gizmo = null; // _videoRoomRoot 縺ｨ荳邱偵↓遐ｴ譽・＆繧後ｋ
            _voiceReverbZone = null;
            _reverbZoneObject = null;
            _mainVideoPlayer = null;
            _videoRoomAudioSource = null;
            _femaleChara = null;
            _hasFemaleBaseY = false;
            _femaleBaseY = 0f;
            if (_videoRoomRoot != null)
            {
                Destroy(_videoRoomRoot);
                _videoRoomRoot = null;
            }

            for (int i = 0; i < _videoTextures.Count; i++)
            {
                if (_videoTextures[i] == null) continue;
                _videoTextures[i].Release();
                Destroy(_videoTextures[i]);
            }

            for (int i = 0; i < _generatedMeshes.Count; i++)
            {
                if (_generatedMeshes[i] == null) continue;
                Destroy(_generatedMeshes[i]);
            }

            _videoTextures.Clear();
            _generatedMeshes.Clear();
            _videoBindings.Clear();
            DestroyWebCamTextures();
            _videoRoomRootMapId = int.MinValue;
        }
    }
}
